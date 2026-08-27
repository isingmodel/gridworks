using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;
using Godot;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

internal sealed record RealtimeContinuation(
    RealtimeNativeRoute Route,
    RealtimeSliceData Data,
    RealtimeCampaignRestoreResult Restore);

/// <summary>
/// Godot scene adapter for the R2 realtime session. This class owns route/resource bootstrap,
/// node lifecycle, input routing, focus, canvas, camera, and UI publication only.
/// </summary>
internal sealed partial class RealtimeSliceMain : Control
{
    private enum QualificationContinuationClass
    {
        Missing,
        Restorable,
        Completed,
        Invalid,
        Unsupported,
        IoFailure,
    }

    private enum ProgressPersistenceOwnership
    {
        None,
        Product,
    }

    private const string QualificationUserDataEnvironment =
        "GRIDWORKS_R2_QUALIFICATION_USER_DATA_DIR";
    private static readonly Vector2I RequiredLogicalCanvas = new(1920, 1080);

    private readonly Dictionary<RealtimePointerOwner, int> _clickCounters = [];
    private RealtimeSession? _session;
    private RealtimeContinuation? _continuation;
    private RealtimeProductTitlePresentation? _productTitlePresentation;
    private RealtimeProductSettings _settings = RealtimeProductSettings.Default;
    private RealtimeProductSettingsLoadStatus _settingsLoadStatus =
        RealtimeProductSettingsLoadStatus.Missing;
    private QualificationContinuationClass _qualificationContinuation =
        QualificationContinuationClass.Missing;
    private string? _qualificationUserDataDirectory;
    private string _settingsStatus = "기본 설정을 사용합니다.";
    private bool _settingsStatusIsError;
    private string? _settingsPath;
    private RealtimeSettingsJourney? _activeSettingsJourney;
    private bool _resumeRunningAfterSettings;
    private RealtimeAudio? _audio;
    private Control? _worldControl;
    private IRealtimeWorldView? _worldView;
    private RealtimeUiRoot? _ui;
    private string? _presentedModalId;
    private Vector2I _priorContentScaleSize;
    private Window.ContentScaleModeEnum _priorContentScaleMode;
    private Window.ContentScaleAspectEnum _priorContentScaleAspect;
    private bool _logicalCanvasApplied;
    private bool _gameplayNodesWired;
    private ProgressPersistenceOwnership _progressPersistenceOwnership =
        ProgressPersistenceOwnership.None;
    private RealtimeLaunchSelection _launch =
        RealtimeLaunchSelection.TechnicalFixture;

    private RealtimeSession Session => RequireSession();

    private RealtimeSession RequireSession() => _session ??
        throw new InvalidOperationException("Realtime R2 slice is not bootstrapped.");

    public override void _Ready()
    {
        string[] userArguments = OS.GetCmdlineUserArgs();
        try
        {
            _qualificationUserDataDirectory =
                ResolveQualificationUserDataDirectory();
            ConfigureLifecycleQualification(userArguments.Length);
        }
        catch (InvalidOperationException exception)
        {
            GD.PushError($"R2 qualification user-data rejected: {exception.Message}");
            GetTree().Quit(1);
            return;
        }

#if DEBUG
        _launch = _launchOverrideForSmoke ??
            ParseLaunchArguments(userArguments);
#else
        _launch = ParseLaunchArguments(userArguments);
#endif
        _audio = GetNode<RealtimeAudio>("%RealtimeAudio");
        _worldControl = GetNode<Control>("%WorldView");
        _worldView = _worldControl as IRealtimeWorldView ??
            throw new InvalidOperationException(
                "The WorldView scene node must implement IRealtimeWorldView.");
        _ui = GetNode<RealtimeUiRoot>("%UiRoot");
        _ui.NewGameRequested += StartNewGame;
        _ui.ContinueRequested += ContinueGame;
        _ui.SettingsOpenRequested += OpenSettings;
        _ui.SettingsCandidateRequested += SaveSettingsCandidate;
        _ui.SettingsCloseRequested += CloseSettings;
        ApplyLogicalCanvas();
        GetWindow().Title = "Gridworks";
        if (_launch.Kind == RealtimeLaunchKind.ProductTitle)
        {
            LoadProductSettings();
            PresentProductTitle();
            GD.Print("REALTIME_R2_PRODUCT_TITLE_READY");
            PrintQualificationDataMarker(userArguments.Length);
        }
        else
        {
            WireGameplayNodes();
            ShowGameplaySurface();
            Bootstrap();
        }
        _audio.StartAmbient();
        SetProcess(true);
        StartLifecycleQualificationIfConfigured();
    }

    public override void _ExitTree()
    {
        RestoreSettingsJourney();
        PersistProgress();
        DetachSession();
        if (_logicalCanvasApplied && GetWindow() is Window window)
        {
            window.ContentScaleSize = _priorContentScaleSize;
            window.ContentScaleMode = _priorContentScaleMode;
            window.ContentScaleAspect = _priorContentScaleAspect;
        }
        _logicalCanvasApplied = false;
    }

    public override void _Process(double delta)
    {
        if (_session is null || delta <= 0)
        {
            return;
        }
        _ = InjectElapsedSeconds(delta);
#if DEBUG
        StopInteractiveTargetAtBoundaryForDebug();
#endif
    }

    private void Bootstrap(
        RealtimeSliceData? restoredData = null,
        RealtimeCampaignRestoreResult? restore = null)
    {
        Assembly assembly = typeof(RealtimeSliceMain).Assembly;
        RealtimeSliceData data = restoredData ?? (_launch.Kind switch
        {
            RealtimeLaunchKind.TechnicalFixture =>
                RealtimeSliceResources.LoadTechnicalFixture(assembly),
            RealtimeLaunchKind.NativeRelease when _launch.NativeRoute is not null =>
                RealtimeSliceResources.LoadNativeRelease(
                    assembly,
                    _launch.NativeRoute),
            _ => throw new InvalidOperationException(
                "Product title must choose a run before session bootstrap."),
        });
        if (!ReferenceEquals(data.NativeRoute, _launch.NativeRoute))
        {
            throw new InvalidOperationException(
                "Realtime slice resource route does not match its launch route.");
        }

        DetachSession();
        _session = restore is null
            ? new RealtimeSession(data, _settings.ReduceMotion)
            : RealtimeSession.Resume(data, restore, _settings.ReduceMotion);
        Session.PresentationPublished += PublishPresentation;
        Session.PointerPresentationPublished += PublishPointerPresentation;
        Session.EvidenceRecorded += RecordEvidence;
        Session.LiveAudioCueRequested += PlayLiveAudioCue;
        Session.FirstLightReplayRequested += ReplayFirstLight;
        Session.TerminalReturnRequested += ReturnFromTerminalResult;

        _clickCounters.Clear();
        foreach (RealtimePointerOwner owner in Enum.GetValues<RealtimePointerOwner>())
        {
            _clickCounters.Add(owner, 0);
        }
        _presentedModalId = null;
#if DEBUG
        if (data.NativeRoute is null)
        {
            _smokeLinePlan = BuildSmokeLinePlan(data);
            _smokeBoundaryFacts = BuildSmokeBoundaryFacts(
                Session.CoreSnapshot,
                _smokeLinePlan);
        }
        else
        {
            _smokeLinePlan = null;
            _smokeBoundaryFacts = null;
        }
        _lastInputRequest = null;
        _suppressFormativeDirectPlayOutputForSmoke = false;
#endif
        PublishPresentation(Session.LatestPresentation);
    }

    private void DetachSession()
    {
        if (_session is null)
        {
            return;
        }
        _session.PresentationPublished -= PublishPresentation;
        _session.PointerPresentationPublished -= PublishPointerPresentation;
        _session.EvidenceRecorded -= RecordEvidence;
        _session.LiveAudioCueRequested -= PlayLiveAudioCue;
        _session.FirstLightReplayRequested -= ReplayFirstLight;
        _session.TerminalReturnRequested -= ReturnFromTerminalResult;
        _session = null;
    }

    private void ReplayFirstLight()
    {
        if (!ReferenceEquals(_launch.NativeRoute, RealtimeNativeRouteCatalog.FirstLight))
        {
            throw new InvalidOperationException(
                "FIRST_LIGHT replay requires the standalone native route.");
        }
        _ui!.PopModal();
        _presentedModalId = null;
        Bootstrap();
    }

    private void ReturnFromTerminalResult()
    {
        if (_progressPersistenceOwnership == ProgressPersistenceOwnership.Product)
        {
            PersistProgress();
            _ui!.PopModal();
            _presentedModalId = null;
            _launch = RealtimeLaunchSelection.ProductTitle;
            _productTitlePresentation = null;
            PresentProductTitle();
            return;
        }
        GetTree().Quit();
    }

    private void PlayLiveAudioCue(RealtimeLiveAudioCue cue) =>
        _audio?.PlayLive(cue);

    private void WireGameplayNodes()
    {
        if (_gameplayNodesWired)
        {
            return;
        }
        _worldView!.PrimaryRequested += HandleMapPrimary;
        _worldView.PointerMoved += HandleMapPointerMoved;
        _worldView.CancelRequested += HandleUndoDraftStep;
        _ui!.SpeedRequested += HandleSpeedRequested;
        _ui.MenuRequested += () => HandleShortcut(RealtimeInputCommand.ToggleBuildShelf);
        _ui.TimelineItemsRequested += HandleTimelineItems;
        _ui.TimelineHorizonDeltaRequested += HandleTimelineHorizonDelta;
        _ui.TimelineNavigationRequested += HandleTimelineNavigation;
        _ui.TimelineExpansionRequested += expanded => _ = ApplyIntent(
            new RealtimeR2Intent(
                expanded
                    ? RealtimeR2IntentKind.OpenSurface
                    : RealtimeR2IntentKind.CloseSurface,
                Surface: RealtimeSurface.Timeline));
        _ui.ContextCloseRequested += () => _ = ApplyIntent(new RealtimeR2Intent(
            RealtimeR2IntentKind.CloseSurface,
            Surface: RealtimeSurface.Inspector));
        _ui.ActionRequested += HandleAction;
        _ui.BuildToolRequested += HandleBuildTool;
        _ui.ModalActionRequested += HandleModalAction;
        _ui.ModalDismissRequested += HandleModalDismiss;
        _ui.InputRequested += HandleInputRequest;
        _ui.MapInteractionRectChanged += ApplyMapInteractionRect;
        _gameplayNodesWired = true;
    }

    private void PresentProductTitle()
    {
        DetachSession();
        _worldControl!.Visible = false;
        _productTitlePresentation = ProbeContinuation();
        _ui!.ShowProductTitle(_productTitlePresentation);
    }

    private void StartNewGame()
    {
        if (_launch.Kind != RealtimeLaunchKind.ProductTitle ||
            _session is not null ||
            _productTitlePresentation is not RealtimeProductTitlePresentation
                presentation ||
            !presentation.CanStartNewGame)
        {
            throw new InvalidOperationException(
                "New Game is available only from the product title.");
        }

        switch (presentation.NewGameAction)
        {
            case RealtimeProductNewGameAction.Reset:
                _productTitlePresentation = presentation with
                {
                    Status = "새 게임을 한 번 더 선택해 확인하세요.",
                    Detail =
                        "원본 저장을 별도 백업 파일로 보존한 뒤 첫 장부터 시작합니다.",
                    NewGameAction = RealtimeProductNewGameAction.ConfirmReset,
                };
                _ui!.ShowProductTitle(_productTitlePresentation);
                return;
            case RealtimeProductNewGameAction.ConfirmReset:
                try
                {
                    _ = RealtimeCampaignSaveStore.CreateUniqueSiblingBackup(
                        ResolveSavePath());
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or
                        ArgumentException or NotSupportedException)
                {
                    GD.PushError($"R2 progress backup failed: {exception.Message}");
                    _productTitlePresentation = presentation with
                    {
                        Status = "원본 저장을 백업하지 못했습니다.",
                        Detail = presentation.CanContinue
                            ? "원본을 바꾸지 않았습니다. 다시 시도하거나 이어하기를 선택하세요."
                            : "원본을 바꾸지 않았습니다. 새 게임을 다시 선택해 시도하세요.",
                    };
                    _ui!.ShowProductTitle(_productTitlePresentation);
                    return;
                }
                break;
            case RealtimeProductNewGameAction.Immediate:
                break;
            default:
                throw new InvalidOperationException(
                    "The product title does not expose a New Game action.");
        }

        _progressPersistenceOwnership = ProgressPersistenceOwnership.Product;
        _launch = RealtimeLaunchSelection.Native(
            RealtimeNativeRouteCatalog.ProductCampaign);
        _continuation = null;
        _productTitlePresentation = null;
        WireGameplayNodes();
        ShowGameplaySurface();
        Bootstrap();
    }

    private void ContinueGame()
    {
        if (_launch.Kind != RealtimeLaunchKind.ProductTitle ||
            _session is not null ||
            _productTitlePresentation?.CanContinue != true ||
            _continuation is not RealtimeContinuation continuation)
        {
            throw new InvalidOperationException(
                "Continue is available only for a validated product-title save.");
        }
        _progressPersistenceOwnership = ProgressPersistenceOwnership.Product;
        _continuation = null;
        _productTitlePresentation = null;
        _launch = RealtimeLaunchSelection.Native(continuation.Route);
        WireGameplayNodes();
        ShowGameplaySurface();
        Bootstrap(continuation.Data, continuation.Restore);
    }

    private RealtimeProductTitlePresentation ProbeContinuation()
    {
        _continuation = null;
        RealtimeCampaignSaveLoadResult load = RealtimeCampaignSaveStore.Load(
            ResolveSavePath());
        _qualificationContinuation = load.Status switch
        {
            RealtimeCampaignSaveLoadStatus.Missing =>
                QualificationContinuationClass.Missing,
            RealtimeCampaignSaveLoadStatus.Unsupported =>
                QualificationContinuationClass.Unsupported,
            RealtimeCampaignSaveLoadStatus.IoFailure =>
                QualificationContinuationClass.IoFailure,
            _ => QualificationContinuationClass.Invalid,
        };
        if (load.Status == RealtimeCampaignSaveLoadStatus.Missing)
        {
            return new RealtimeProductTitlePresentation(
                "새로운 청류시 운영을 시작할 준비가 됐습니다.",
                "저장된 진행이 없어 이어하기를 사용할 수 없습니다. 새 게임을 시작하세요.",
                CanContinue: false,
                NewGameAction: RealtimeProductNewGameAction.Immediate);
        }
        if (load.Status != RealtimeCampaignSaveLoadStatus.Loaded || load.Save is null)
        {
            string reason = load.Status switch
            {
                RealtimeCampaignSaveLoadStatus.Unsupported =>
                    "이 버전에서 지원하지 않는 저장입니다. 새 게임을 선택하면 원본 백업 확인 단계가 열립니다.",
                RealtimeCampaignSaveLoadStatus.IoFailure =>
                    "저장 파일을 읽을 수 없습니다. 원본을 바꾸지 않았습니다.",
                _ => "저장 파일이 손상됐습니다. 새 게임을 선택하면 원본 백업 확인 단계가 열립니다.",
            };
            return new RealtimeProductTitlePresentation(
                "저장된 진행을 확인하지 못했습니다.",
                reason,
                CanContinue: false,
                NewGameAction: load.Status == RealtimeCampaignSaveLoadStatus.IoFailure
                    ? RealtimeProductNewGameAction.Unavailable
                    : RealtimeProductNewGameAction.Reset);
        }

        try
        {
            RealtimeCampaignSave save = load.Save;
            if (!RealtimeNativeRouteCatalog.TryResolve(
                    save.Source.RouteId,
                    out RealtimeNativeRoute? route) ||
                !RealtimeNativeRouteCatalog.SupportsProductContinuation(route!))
            {
                throw new RealtimeCampaignPersistenceException(
                    RealtimeCampaignPersistenceFailureKind.Invalid,
                    "The save route is not a supported product continuation.");
            }
            RealtimeSliceData data = RealtimeSliceResources.LoadNativeRelease(
                typeof(RealtimeSliceMain).Assembly,
                route!);
            RealtimeCampaignRestoreResult restore =
                RealtimeCampaignSaveCodec.Restore(
                    data.RequireSaveSourceIdentity(),
                    data.Campaign,
                    data.World,
                    save);
            RealtimeProgressResumePlan resumePlan =
                RealtimeSession.ValidateProgressResume(data, restore);
            _continuation = new RealtimeContinuation(route!, data, restore);
            RealtimeCampaignSnapshot snapshot = restore.Run.GetSnapshot();
            bool completed = resumePlan.Kind == RealtimeProgressResumeKind.Completed;
            _qualificationContinuation = completed
                ? QualificationContinuationClass.Completed
                : QualificationContinuationClass.Restorable;
            return new RealtimeProductTitlePresentation(
                completed
                    ? "청류시 8장 운영을 완료한 저장입니다."
                    : "저장된 청류시 운영을 이어갈 수 있습니다.",
                $"{RealtimePresentationText.Time(snapshot.Minute)} · " +
                $"운영 자금 {RealtimePresentationText.Cash(snapshot.CashUnit)} · " +
                (completed
                    ? "이어하기는 완료된 망을 읽기 전용으로 엽니다. " +
                        "새 게임은 첫 장부터 시작하며 저장 가능한 지점에서 " +
                        "정상 종료할 때 이 저장을 교체합니다."
                    : resumePlan.ActiveStoryModalId is null
                        ? "이어하기는 paused 상태로 열립니다. " +
                            "새 게임은 원본 백업 확인 후 첫 장부터 시작합니다."
                        : "저장된 story를 먼저 열고, 닫으면 paused 상태로 이어집니다. " +
                            "새 게임은 원본 백업 확인 후 첫 장부터 시작합니다."),
                CanContinue: true,
                NewGameAction: completed
                    ? RealtimeProductNewGameAction.Immediate
                    : RealtimeProductNewGameAction.Reset);
        }
        catch (Exception exception) when (
            exception is RealtimeCampaignPersistenceException or
                ArgumentException or InvalidOperationException)
        {
            return new RealtimeProductTitlePresentation(
                "저장된 진행이 현재 콘텐츠와 일치하지 않습니다.",
                "원본을 바꾸지 않았습니다. 새 게임을 선택하면 원본 백업 확인 단계가 열립니다.",
                CanContinue: false,
                NewGameAction: RealtimeProductNewGameAction.Reset);
        }
    }

    private void LoadProductSettings()
    {
        _settingsPath = ResolveSettingsPath();
        RealtimeProductSettingsLoadResult load =
            RealtimeProductSettingsStore.Load(_settingsPath);
        _settings = load.Settings;
        _settingsLoadStatus = load.Status;
        (_settingsStatus, _settingsStatusIsError) = load.Status switch
        {
            RealtimeProductSettingsLoadStatus.Missing =>
                ("current R2 기본 설정을 사용합니다.", false),
            RealtimeProductSettingsLoadStatus.Loaded =>
                ("저장된 current R2 설정을 불러왔습니다.", false),
            RealtimeProductSettingsLoadStatus.Unsupported =>
                ("지원하지 않는 설정 파일이라 기본값을 사용합니다. 원본은 덮어쓰지 않았습니다.", true),
            RealtimeProductSettingsLoadStatus.Invalid =>
                ("설정 파일이 손상되어 기본값을 사용합니다. 원본은 덮어쓰지 않았습니다.", true),
            RealtimeProductSettingsLoadStatus.ReadFailure =>
                ("설정 파일을 읽지 못해 기본값을 사용합니다. 원본은 덮어쓰지 않았습니다.", true),
            _ => throw new ArgumentOutOfRangeException(),
        };
        ApplyProductSettings(_settings);
    }

    private void OpenSettings(RealtimeSettingsJourney journey)
    {
        if (_activeSettingsJourney.HasValue)
        {
            return;
        }
        bool validJourney = journey switch
        {
            RealtimeSettingsJourney.ProductTitle =>
                _session is null && _productTitlePresentation is not null,
            RealtimeSettingsJourney.Gameplay =>
                _session is not null && _worldControl?.Visible == true,
            _ => false,
        };
        if (!validJourney)
        {
            throw new InvalidOperationException(
                "Settings can open only from the matching title or gameplay journey.");
        }

        _resumeRunningAfterSettings = false;
        if (journey == RealtimeSettingsJourney.Gameplay &&
            Session.InteractionState.Simulation == RealtimeSimulationState.Running)
        {
            RealtimeR2IntentResult paused = ApplyIntent(
                RealtimeR2Intent.SetPlayerPaused(true));
            if (!paused.Accepted)
            {
                throw new InvalidOperationException(
                    $"Settings could not pause the running session: {paused.Error}");
            }
            _resumeRunningAfterSettings = true;
        }
        _activeSettingsJourney = journey;
        _ui!.ShowSettings(SettingsPresentation());
    }

    private void SaveSettingsCandidate(RealtimeSettingsValues values)
    {
        if (!_activeSettingsJourney.HasValue)
        {
            throw new InvalidOperationException(
                "A settings candidate requires the shared settings surface.");
        }
        if (_settingsPath is null)
        {
            _settingsStatus =
                "명시적 개발 경로는 제품 설정 파일을 읽거나 쓰지 않습니다.";
            _settingsStatusIsError = true;
            _ui!.UpdateSettings(SettingsPresentation());
            return;
        }

        var candidate = new RealtimeProductSettings(
            RealtimeProductSettings.SupportedSchemaVersion,
            values.Fullscreen
                ? RealtimeProductWindowMode.Fullscreen
                : RealtimeProductWindowMode.Windowed,
            values.UiScalePercent,
            values.MasterVolumePercent,
            values.AmbientVolumePercent,
            values.SfxVolumePercent,
            values.ReduceMotion);
        RealtimeProductSettingsSaveResult save =
            RealtimeProductSettingsStore.Save(_settingsPath, candidate);
        if (save.Status != RealtimeProductSettingsSaveStatus.Saved)
        {
            _settingsStatus = save.Status == RealtimeProductSettingsSaveStatus.Invalid
                ? "지원하지 않는 설정 값입니다. 이전 설정을 유지합니다."
                : "설정을 저장하지 못했습니다. 기존 파일과 이전 설정을 유지합니다.";
            _settingsStatusIsError = true;
            GD.PushError($"R2 settings save failed: {save.Message}");
            _ui!.UpdateSettings(SettingsPresentation());
            return;
        }

        _settings = candidate;
        _settingsStatus = "current R2 설정을 저장하고 적용했습니다.";
        _settingsStatusIsError = false;
        ApplyProductSettings(_settings);
        _ui!.UpdateSettings(SettingsPresentation());
    }

    private void CloseSettings(RealtimeSettingsJourney journey)
    {
        if (_activeSettingsJourney != journey)
        {
            throw new InvalidOperationException(
                "Settings close did not match the journey that opened it.");
        }
        _ui!.HideSettings();
        RestoreSettingsJourney();
    }

    private void RestoreSettingsJourney()
    {
        bool resume = _activeSettingsJourney == RealtimeSettingsJourney.Gameplay &&
            _resumeRunningAfterSettings;
        _activeSettingsJourney = null;
        _resumeRunningAfterSettings = false;
        if (!resume || _session is null ||
            Session.InteractionState.Simulation != RealtimeSimulationState.PlayerPaused ||
            Session.InteractionState.PauseReason != RealtimePauseReason.PlayerRequest)
        {
            return;
        }
        RealtimeR2IntentResult restored = ApplyIntent(
            RealtimeR2Intent.SetPlayerPaused(false));
        if (!restored.Accepted)
        {
            throw new InvalidOperationException(
                $"Settings could not restore the running session: {restored.Error}");
        }
    }

    private RealtimeSettingsPresentation SettingsPresentation() => new(
        new RealtimeSettingsValues(
            _settings.WindowMode == RealtimeProductWindowMode.Fullscreen,
            _settings.UiScalePercent,
            _settings.MasterVolumePercent,
            _settings.AmbientVolumePercent,
            _settings.SfxVolumePercent,
            _settings.ReduceMotion),
        _settingsPath is null
            ? "명시적 개발 경로는 제품 설정 파일을 읽거나 쓰지 않습니다."
            : _settingsStatus,
        CanApply: _settingsPath is not null,
        IsError: _settingsPath is null || _settingsStatusIsError);

    private void ApplyProductSettings(RealtimeProductSettings settings)
    {
        GetWindow().Mode = settings.WindowMode == RealtimeProductWindowMode.Fullscreen
            ? Window.ModeEnum.Fullscreen
            : Window.ModeEnum.Windowed;
        _ui!.UiScalePercent = settings.UiScalePercent;
        ApplyBusVolume("Master", settings.MasterVolumePercent);
        ApplyBusVolume("Ambient", settings.AmbientVolumePercent);
        ApplyBusVolume("SFX", settings.SfxVolumePercent);
        _session?.SetReduceMotion(settings.ReduceMotion);
    }

    private static void ApplyBusVolume(string busName, int percent)
    {
        int bus = AudioServer.GetBusIndex(busName);
        if (bus < 0)
        {
            throw new InvalidOperationException(
                $"Required audio bus '{busName}' is missing.");
        }
        AudioServer.SetBusMute(bus, percent == 0);
        AudioServer.SetBusVolumeLinear(bus, percent / 100f);
    }

    private void PersistProgress()
    {
        if (_session is null ||
            _progressPersistenceOwnership != ProgressPersistenceOwnership.Product)
        {
            return;
        }
        try
        {
            if (Session.TryCaptureProgress(
                    Session.Data.RequireSaveSourceIdentity(),
                    out RealtimeCampaignSave? save) &&
                save is not null)
            {
                RealtimeCampaignSaveStore.Save(ResolveSavePath(), save);
            }
        }
        catch (Exception exception) when (
            exception is RealtimeCampaignPersistenceException or IOException or
                UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            GD.PushError($"R2 progress save failed: {exception.Message}");
        }
    }

    private string ResolveSavePath()
    {
#if DEBUG
        if (!string.IsNullOrWhiteSpace(_savePathOverrideForSmoke))
        {
            return _savePathOverrideForSmoke;
        }
#endif
        return ResolveProductUserDataPath(RealtimeCampaignSaveStore.FileName);
    }

    private string ResolveSettingsPath()
    {
#if DEBUG
        if (!string.IsNullOrWhiteSpace(_settingsPathOverrideForSmoke))
        {
            return _settingsPathOverrideForSmoke;
        }
#endif
        return ResolveProductUserDataPath(RealtimeProductSettingsStore.FileName);
    }

    private string ResolveProductUserDataPath(string fileName) =>
        _qualificationUserDataDirectory is null
            ? ProjectSettings.GlobalizePath($"user://{fileName}")
            : Path.Combine(_qualificationUserDataDirectory, fileName);

    private static string? ResolveQualificationUserDataDirectory()
    {
        string? value = System.Environment.GetEnvironmentVariable(
            QualificationUserDataEnvironment);
        if (value is null)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
        {
            throw new InvalidOperationException(
                $"{QualificationUserDataEnvironment} must be an absolute directory.");
        }

        try
        {
            string fullPath = Path.GetFullPath(value);
            var directory = new DirectoryInfo(fullPath);
            if (!directory.Exists)
            {
                throw new InvalidOperationException(
                    $"{QualificationUserDataEnvironment} must already exist.");
            }
            if (directory.LinkTarget is not null ||
                (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    $"{QualificationUserDataEnvironment} cannot be a symlink.");
            }
            return fullPath;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or
                NotSupportedException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"{QualificationUserDataEnvironment} is not a usable directory.",
                exception);
        }
    }

    private void PrintQualificationDataMarker(int userArgumentCount)
    {
        if (_qualificationUserDataDirectory is null)
        {
            return;
        }

        string settings = _settingsLoadStatus switch
        {
            RealtimeProductSettingsLoadStatus.Missing => "MISSING",
            RealtimeProductSettingsLoadStatus.Loaded => "LOADED",
            RealtimeProductSettingsLoadStatus.Invalid => "INVALID",
            RealtimeProductSettingsLoadStatus.Unsupported => "UNSUPPORTED",
            RealtimeProductSettingsLoadStatus.ReadFailure => "READ_FAILURE",
            _ => throw new ArgumentOutOfRangeException(),
        };
        string continuation = _qualificationContinuation switch
        {
            QualificationContinuationClass.Missing => "MISSING",
            QualificationContinuationClass.Restorable => "RESTORABLE",
            QualificationContinuationClass.Completed => "COMPLETED",
            QualificationContinuationClass.Invalid => "INVALID",
            QualificationContinuationClass.Unsupported => "UNSUPPORTED",
            QualificationContinuationClass.IoFailure => "IO_FAILURE",
            _ => throw new ArgumentOutOfRangeException(),
        };
        GD.Print(
            "REALTIME_R2_QUALIFICATION_DATA_READY " +
            $"user_args={userArgumentCount} settings={settings} " +
            $"continuation={continuation}");
    }

    private void ShowGameplaySurface()
    {
        _ui!.HideProductTitle();
        _worldControl!.Visible = true;
    }

    private void PublishPresentation(RealtimeSlicePresentation presentation)
    {
        if (_worldView is null || _ui is null || !IsInsideTree())
        {
            return;
        }
        _worldView.SetPresentation(presentation.World);
        _worldView.SetPointerFeedback(presentation.Pointer);
        _ui.SetTopHud(presentation.Hud);
        _ui.SetEventRail(presentation.Rail);
        _ui.SetContextDock(presentation.Context);
        _ui.SetBuildShelf(presentation.BuildShelf);
        _ui.SetActionDock(presentation.ActionDock);
        PresentModal(presentation.Modal);
    }

    private void PublishPointerPresentation(RealtimeSlicePresentation presentation)
    {
        if (_worldView is null || _ui is null || !IsInsideTree())
        {
            return;
        }
        _worldView.SetPointerFeedback(presentation.Pointer);
        _ui.SetBuildShelf(presentation.BuildShelf);
        _ui.SetActionDock(presentation.ActionDock);
    }

    private void RecordEvidence(string evidence)
    {
#if DEBUG
        if (_suppressFormativeDirectPlayOutputForSmoke)
        {
            return;
        }
#endif
        GD.Print(evidence);
    }

    private void PresentModal(RealtimeModalPresentation? modal)
    {
        if (_worldView is null || _ui is null || !IsInsideTree())
        {
            return;
        }
        if (modal is null)
        {
            if (_presentedModalId is not null)
            {
                _ui.PopModal();
                _presentedModalId = null;
            }
            return;
        }
        if (string.Equals(_presentedModalId, modal.Id, StringComparison.Ordinal))
        {
            return;
        }
        if (_presentedModalId is not null)
        {
            _ui.PopModal();
        }
        _ui.InputRouter.CancelPanCapture();
        _worldView.EndPan();
        Control? opener = GetViewport().GuiGetFocusOwner();
        if (!IsValidReturnFocus(opener))
        {
            _worldView.RequestFocus();
        }
        if (!_ui.PushModal(modal))
        {
            throw new InvalidOperationException("R2 modal host rejected the single modal.");
        }
        _presentedModalId = modal.Id;
    }

    private static bool IsValidReturnFocus(Control? control) => control is not null &&
        control.IsInsideTree() &&
        control.IsVisibleInTree() &&
        control.FocusMode != Control.FocusModeEnum.None &&
        (control is not BaseButton button || !button.Disabled);

    private void HandleMapPrimary(
        RealtimePointerResolution resolution,
        CoreMapPoint worldPoint)
    {
        EnsureBootstrapped();
        _clickCounters[resolution.Owner]++;
        Session.HandleMapPrimary(resolution, worldPoint);
    }

    private void HandleMapPointerMoved(
        RealtimePointerResolution resolution,
        CoreMapPoint worldPoint)
    {
        EnsureBootstrapped();
        Session.HandleMapPointerMoved(
            resolution,
            worldPoint,
            _worldView?.IsPanning == true);
    }

    private void HandleAction(string id) => Session.HandleAction(id);

    private void HandleModalAction(string modalId, string actionId) =>
        Session.HandleModalAction(modalId, actionId);

    private void HandleModalDismiss(string modalId) =>
        Session.HandleModalDismiss(modalId);

    private void HandleBuildTool(string id) => Session.HandleBuildTool(id);

    private void HandleSpeedRequested(RealtimeSimulationSpeed speed) =>
        Session.HandleSpeedRequested(speed);

    private void HandleTimelineHorizonDelta(int delta) =>
        Session.HandleTimelineHorizonDelta(delta);

    private void HandleTimelineItems(IReadOnlyList<string> ids) =>
        Session.HandleTimelineItems(ids);

    private void HandleTimelineNavigation(RealtimeTimelineNavigation navigation) =>
        Session.HandleTimelineNavigation(navigation);

    private void HandleUndoDraftStep() => Session.HandleUndoDraftStep();

    private RealtimeR2IntentResult ApplyIntent(RealtimeR2Intent intent) =>
        Session.ApplyIntent(intent);

    private RealtimeR2FrameResult InjectElapsedNanoseconds(long elapsedNanoseconds) =>
        Session.InjectElapsedNanoseconds(
            elapsedNanoseconds,
            MaximumInteractiveVirtualFrames());

    private RealtimeR2FrameResult InjectElapsedSeconds(double elapsedSeconds) =>
        Session.InjectElapsedSeconds(
            elapsedSeconds,
            MaximumInteractiveVirtualFrames());

    private RealtimeR2FrameResult InjectExactFrames(
        long frameCount,
        int framesPerSecond) => Session.InjectExactFrames(frameCount, framesPerSecond);

    private long? MaximumInteractiveVirtualFrames()
    {
#if DEBUG
        return _interactiveCheckpoint is null
            ? null
            : ClampInteractiveVirtualFramesAtBoundaryForDebug(long.MaxValue);
#else
        return null;
#endif
    }

    private void ApplyMapInteractionRect(Rect2 rect)
    {
        if (_worldView is null || _ui is null || rect.Size.X <= 0 || rect.Size.Y <= 0)
        {
            return;
        }
        _worldView.SetInteractionRect(rect, _ui.LayoutProfile);
    }

    private void HandleInputRequest(RealtimeInputRequest request)
    {
        if (!RealtimeUiCapabilities.Supports(request.Command) ||
            !RealtimeUiCapabilities.Supports(request.SourcePriority))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request,
                "Unsupported realtime input request.");
        }
#if DEBUG
        _lastInputRequest = request;
#endif
        HandleShortcut(request.Command);
    }

    private void HandleShortcut(RealtimeInputCommand command)
    {
        if (!RealtimeUiCapabilities.Supports(command))
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                command,
                "Unsupported realtime input command.");
        }
        if (command is not RealtimeInputCommand.CancelOrBack and
            not RealtimeInputCommand.ToggleBuildShelf)
        {
            Session.DisarmDraftCancellation();
        }
        switch (command)
        {
            case RealtimeInputCommand.TogglePause:
                Session.HandleTogglePause();
                break;
            case RealtimeInputCommand.SetNormalSpeed:
                _ = ApplyIntent(RealtimeR2Intent.SetSpeed(RealtimeSimulationSpeed.Normal));
                break;
            case RealtimeInputCommand.SetFastSpeed:
                _ = ApplyIntent(RealtimeR2Intent.SetSpeed(RealtimeSimulationSpeed.Fast));
                break;
            case RealtimeInputCommand.SetVeryFastSpeed:
                _ = ApplyIntent(RealtimeR2Intent.SetSpeed(RealtimeSimulationSpeed.VeryFast));
                break;
            case RealtimeInputCommand.ToggleAnalysis:
                _ = ApplyIntent(new RealtimeR2Intent(RealtimeR2IntentKind.ToggleAnalysis));
                break;
            case RealtimeInputCommand.ToggleBuildShelf:
                Session.HandleToggleBuildShelf();
                break;
            case RealtimeInputCommand.CancelOrBack:
                Session.HandleCancel();
                break;
            case RealtimeInputCommand.UndoDraftStep:
                Session.HandleUndoDraftStep();
                break;
            case RealtimeInputCommand.CycleCandidatePrevious:
                _worldView?.CycleCandidate(-1);
                break;
            case RealtimeInputCommand.CycleCandidateNext:
                _worldView?.CycleCandidate(1);
                break;
            case RealtimeInputCommand.BeginPan:
                _worldView?.BeginPan();
                break;
            case RealtimeInputCommand.EndPan:
                _worldView?.EndPan();
                break;
            case RealtimeInputCommand.ConfirmOrSelect:
                _worldView?.ConfirmCurrentCandidate();
                break;
            case RealtimeInputCommand.TimelineHome:
                RouteTimelineNavigation(RealtimeTimelineNavigation.Home);
                break;
            case RealtimeInputCommand.TimelinePrevious:
                RouteTimelineNavigation(RealtimeTimelineNavigation.PreviousEvent);
                break;
            case RealtimeInputCommand.TimelineNext:
                RouteTimelineNavigation(RealtimeTimelineNavigation.NextEvent);
                break;
            case RealtimeInputCommand.SelectInspectTool:
                Session.HandleBuildTool(RealtimeR2Ids.InspectTool);
                break;
            case RealtimeInputCommand.SelectFirstNodeTool:
                Session.SelectBuildToolFamily(RealtimeBuildToolFamily.Node);
                break;
            case RealtimeInputCommand.SelectFirstLineTool:
                Session.SelectBuildToolFamily(RealtimeBuildToolFamily.Line);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(command),
                    command,
                    "Unsupported realtime input command.");
        }
    }

    private void RouteTimelineNavigation(RealtimeTimelineNavigation navigation)
    {
        if (_ui?.NavigateTimeline(navigation) != true)
        {
            Session.HandleTimelineNavigation(navigation);
        }
    }

    private void ApplyLogicalCanvas()
    {
        Window window = GetWindow();
        _priorContentScaleSize = window.ContentScaleSize;
        _priorContentScaleMode = window.ContentScaleMode;
        _priorContentScaleAspect = window.ContentScaleAspect;
        window.ContentScaleSize = RequiredLogicalCanvas;
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
        _logicalCanvasApplied = true;
    }

    internal static RealtimeLaunchSelection ParseLaunchArguments(string[] arguments) =>
        RealtimeLaunchCatalog.Parse(arguments);

    private void EnsureBootstrapped()
    {
        _ = Session;
    }

}
