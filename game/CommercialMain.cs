using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Gridworks.Core.Release.V2;
using Godot;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game;

internal enum CommercialTool
{
    None,
    Substation,
    Line,
}

internal sealed partial class CommercialMain : Control
{
    private const string FixtureResource =
        "Gridworks.Game.EmbeddedData.release-world-v2.json";
    private const string CampaignResource =
        "Gridworks.Game.EmbeddedData.release-campaign-v2.json";
    private const string BuildIdentityResource =
        "Gridworks.Game.EmbeddedData.commercial-build-identity-v1.json";
    private const string SubstationClassId = "SMALL_SUBSTATION";
    private const string StandardLineClassId = "STANDARD_LINE";
    private const string ReinforcedLineClassId = "REINFORCED_LINE";
    private const string StandardPoleClassId = "STANDARD_POLE";
    private const string ReinforcedPoleClassId = "REINFORCED_POLE";
    private const string StageFSmokeSaveEnvironment = "GRIDWORKS_STAGE_F_SMOKE_SAVE_PATH";
    private const string StorageDirectoryEnvironment = "GRIDWORKS_COMMERCIAL_STORAGE_DIRECTORY";
    private const string G3FinalCaptureDirectoryEnvironment =
        "GRIDWORKS_G3_FINAL_CAPTURE_DIRECTORY";

    private CommercialLaunchOptions _options = null!;
    private CommercialWorldDefinition _commercialWorld = null!;
    private CommercialCampaignDefinition _campaign = null!;
    private CommercialCoreRun? _coreRun;
    private ConstructionSession? _legacySession;
    private ConstructionSnapshot _snapshot = null!;
    private byte[] _worldBytes = null!;
    private byte[] _campaignBytes = null!;
    private byte[] _buildIdentityBytes = null!;
    private string? _savePath;
    private string _settingsPath = null!;
    private bool _saveWritable = true;
    private bool _incompatibleSavePending;
    private bool _hasContinuation;
    private CommercialSettingsV3 _settings = CommercialSettingsV3.Default;
    private string _titleStatus = string.Empty;
    private CommercialMapView _map = null!;
    private CommercialTaskPanel _panel = null!;
    private CommercialEventTimeline _timeline = null!;
    private ReleaseShellOverlay _shell = null!;
    private ReleaseAudio _audio = null!;
    private Label _titleLabel = null!;
    private Label _zoomLabel = null!;
    private Label _summaryLabel = null!;
    private Label _boundaryLabel = null!;
    private Label _cashLabel = null!;
    private Label _supplyLabel = null!;
    private Label _controlHelp = null!;
    private Button _helpButton = null!;
    private Label _fatalLabel = null!;
    private readonly Dictionary<Control, int> _baseFontSizes = [];

    private CommercialTool _tool;
    private CoreMapPoint? _pointerPoint;
    private string? _candidateNodeId;
    private bool _pointerAccepted = true;
    private ConstructionError? _pointerError;
    private string _pointerMessage = string.Empty;
    private IReadOnlyList<string> _pointerRiskAreaIds = Array.Empty<string>();
    private string _lastStatus = "아직 발주한 공사가 없습니다.";
    private string _lastError = string.Empty;
    private ThermalSequenceResult _thermalSequence = null!;
    private int _thermalProjectionIndex;
    private string _selectedThermalAssetId = "NORTH_SUBSTATION";
    private string? _selectedDemandId;
    private string _lineClassId = ReinforcedLineClassId;
    private string _poleClassId = StandardPoleClassId;
    private CommercialChapterResultRecord? _presentedResult;
    private bool _showEpilogue;
    private int _completedChapterSelectionIndex;

    public override void _Ready()
    {
        try
        {
            GetWindow().Title = "Gridworks — 첫 불빛에서 북안의 약속까지";
            GetWindow().MinSize = new Vector2I(1920, 1080);
            _options = CommercialLaunchOptions.Parse(OS.GetCmdlineUserArgs());
            ConfigurePersistencePaths();
            LoadSettings();
            _worldBytes = ReadEmbeddedBytes(FixtureResource, "상용 지도 데이터를 열 수 없습니다.");
            _campaignBytes = ReadEmbeddedBytes(CampaignResource, "상용 캠페인 데이터를 열 수 없습니다.");
            _buildIdentityBytes = ReadEmbeddedBytes(
                BuildIdentityResource,
                "상용 빌드 식별자를 열 수 없습니다.");
            _commercialWorld = CommercialWorldLoader.Load(_worldBytes);
            _campaign = CommercialCampaignLoader.Load(_campaignBytes, _commercialWorld);
            if (_options.PlacementSmoke || _options.ThermalSmoke)
            {
                _legacySession = new ConstructionSession(_commercialWorld.Spatial);
                _snapshot = _legacySession.GetSnapshot();
                _poleClassId = ReinforcedPoleClassId;
            }
            else
            {
                InitializeCoreRun(loadSave: !_options.StartsFreshCampaign);
            }
            RefreshThermalProjection();
            BindScene();
            ApplySettings(_settings);
            Render();
            if (_options.AnySmoke && !_options.PresentationSmoke)
            {
                _shell.HideShell();
                _map.CallDeferred(Control.MethodName.GrabFocus);
            }
            else
            {
                _shell.ShowTitle(_hasContinuation, _titleStatus);
            }
#if DEBUG || COMMERCIAL_INTERNAL
            if (_options.PlacementSmoke)
            {
                CallDeferred(nameof(RunPlacementSmoke));
            }
            else if (_options.ThermalSmoke)
            {
                CallDeferred(nameof(RunThermalSmoke));
            }
            else if (_options.CampaignSmoke || _options.CampaignCheckpointSmoke)
            {
                CallDeferred(nameof(RunCampaignSmoke));
            }
            else if (_options.CampaignCompletionSmoke)
            {
                CallDeferred(nameof(RunCampaignCompletionSmoke));
            }
            else if (_options.CampaignCompletedResumeSmoke)
            {
                CallDeferred(nameof(RunCampaignCompletedResumeSmoke));
            }
            else if (_options.PresentationSmoke)
            {
                CallDeferred(nameof(RunPresentationSmoke));
            }
#endif
        }
        catch (Exception exception)
        {
            GD.PushError($"상용 자유 배치 화면을 시작하지 못했습니다: {exception}");
            ShowFatal(exception.Message);
#if DEBUG || COMMERCIAL_INTERNAL
            if (OS.GetCmdlineUserArgs().Any(argument => argument.EndsWith(
                    "-smoke",
                    StringComparison.Ordinal)))
            {
                GetTree().Quit(1);
            }
#endif
        }
    }

    public override void _UnhandledKeyInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey
            {
                Pressed: true,
                Echo: false,
                Keycode: Key.Escape,
            })
        {
            return;
        }

        if (_snapshot.Phase is ConstructionPhase.NodeDrafting or ConstructionPhase.LineDrafting)
        {
            CancelDraft();
        }
        else
        {
            return;
        }
        GetViewport().SetInputAsHandled();
    }

    private void BindScene()
    {
        _map = GetNode<CommercialMapView>("%CommercialMapView");
        _panel = GetNode<CommercialTaskPanel>("%CommercialTaskPanel");
        _timeline = GetNode<CommercialEventTimeline>("%CommercialEventTimeline");
        _shell = GetNode<ReleaseShellOverlay>("%CommercialShellOverlay");
        _audio = GetNode<ReleaseAudio>("%CommercialAudio");
        _titleLabel = GetNode<Label>("%TitleLabel");
        _zoomLabel = GetNode<Label>("%ZoomLabel");
        _summaryLabel = GetNode<Label>("%SummaryLabel");
        _boundaryLabel = GetNode<Label>("%BoundaryLabel");
        _cashLabel = GetNode<Label>("%CashLabel");
        _supplyLabel = GetNode<Label>("%SupplyLabel");
        _controlHelp = GetNode<Label>("%ControlHelp");
        _helpButton = GetNode<Button>("%HelpButton");
        _fatalLabel = GetNode<Label>("%FatalLabel");

        _map.PointerChanged += OnPointerChanged;
        _map.PointRequested += OnPointRequested;
        _map.UndoRequested += () =>
            ApplyConstruction(
                new CommercialCoreCommand(CommercialCoreCommandKind.UndoLinePoint),
                () => _legacySession!.UndoLinePoint(),
                "마지막 선로 지점을 되돌렸습니다.");
        _map.DraftPointMoveRequested += MoveDraftPoint;
        _map.DraftPointDragPreviewChanged += OnDraftPointDragPreviewChanged;
        _map.BuildRailActionRequested += OnPanelAction;
        _map.CameraChanged += () =>
        {
            _zoomLabel.Text = $"지도 · {_map.ZoomLabel}";
        };
        _panel.ActionRequested += OnPanelAction;
        _helpButton.Pressed += ShowPause;
        _shell.PauseRequested += ShowPause;
        _shell.NewGameRequested += StartNewGame;
        _shell.ContinueRequested += ContinueGame;
        _shell.ResumeRequested += () => _shell.HideShell();
        _shell.SaveAndQuitRequested += SaveAndReturnToTitle;
        _shell.RestartChapterRequested += RestartChapter;
        _shell.RewindPreviousChapterRequested += RewindPreviousChapter;
        _shell.FullscreenChanged += OnFullscreenChanged;
        _shell.UiScalePercentChanged += OnUiScalePercentChanged;
        _shell.MasterVolumePercentChanged += value => UpdateVolume(master: value);
        _shell.AmbientVolumePercentChanged += value => UpdateVolume(ambient: value);
        _shell.SfxVolumePercentChanged += value => UpdateVolume(sfx: value);
        _shell.ControlHelpChanged += OnControlHelpChanged;
        _shell.ReduceMotionChanged += OnReduceMotionChanged;
        _shell.GameplayFocusRequested += () =>
            _map.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void OnPointerChanged(CoreMapPoint? point, string? candidateNodeId)
    {
        _pointerPoint = point;
        _candidateNodeId = candidateNodeId;
        RefreshPointerPreview();
        Render();
    }

    private void OnPointRequested(CoreMapPoint point, string? candidateNodeId)
    {
        _pointerPoint = point;
        _candidateNodeId = candidateNodeId;
        switch (_tool)
        {
            case CommercialTool.Substation when
                _snapshot.Phase is ConstructionPhase.Ready or ConstructionPhase.NodeDrafting:
                ApplyConstruction(
                    new CommercialCoreCommand(
                        CommercialCoreCommandKind.SetNodeDraft,
                        Position: point,
                        NodeClassId: SubstationClassId),
                    () => _legacySession!.SetNodeDraft(SubstationClassId, point),
                    "변전소 계획 위치를 정했습니다.");
                return;

            case CommercialTool.Line when _snapshot.Phase == ConstructionPhase.Ready:
                if (candidateNodeId is null)
                {
                    RejectLocally("선로를 시작할 접속점 가까이에서 선택하세요.");
                    return;
                }
                ApplyConstruction(
                    new CommercialCoreCommand(
                        CommercialCoreCommandKind.StartLineDraft,
                        StartNodeId: candidateNodeId,
                        LineClassId: _lineClassId,
                        PoleClassId: _poleClassId),
                    () => _legacySession!.StartLineDraft(
                        candidateNodeId,
                        _lineClassId,
                        _poleClassId),
                    "첫 접속점을 정했습니다. 다음 전신주 위치를 이어서 선택하세요.");
                return;

            case CommercialTool.Line when
                _snapshot.Phase == ConstructionPhase.LineDrafting &&
                _snapshot.LineDraft?.EndNodeId is null:
                CommercialCoreCommand command = candidateNodeId is null
                    ? new CommercialCoreCommand(
                        CommercialCoreCommandKind.AddLinePoint,
                        Position: point)
                    : new CommercialCoreCommand(
                        CommercialCoreCommandKind.FinishLineDraft,
                        EndNodeId: candidateNodeId);
                ApplyConstruction(
                    command,
                    () => candidateNodeId is null
                        ? _legacySession!.AddLinePoint(point)
                        : _legacySession!.FinishLineDraft(candidateNodeId),
                    candidateNodeId is null
                    ? "전신주 위치를 계획에 더했습니다."
                    : "마지막 접속점을 정했습니다. 견적을 확인하고 발주하세요.");
                return;

            case CommercialTool.None when candidateNodeId is not null:
                _selectedThermalAssetId = candidateNodeId;
                ThermalIntervalResult interval = _thermalSequence.Intervals[_thermalProjectionIndex];
                ThermalDemandResult? demandAtNode = interval.Demands.FirstOrDefault(item =>
                    DemandNodeId(interval.IntervalId, item.DemandId) == candidateNodeId);
                if (demandAtNode is not null)
                {
                    _selectedDemandId = demandAtNode.DemandId;
                }
                _lastStatus = demandAtNode is null
                    ? "선택 설비의 열 한계와 다음 상태를 표시합니다."
                    : "선택 시설의 발전원부터 전체 실제 공급 경로를 표시합니다.";
                _lastError = string.Empty;
                Render();
                return;

            default:
                RejectLocally("현재 작업에서 지도 위치를 더 선택할 수 없습니다.");
                return;
        }
    }

    private void OnPanelAction(CommercialPanelAction action)
    {
        switch (action)
        {
            case CommercialPanelAction.PlaceSubstation:
                SelectTool(CommercialTool.Substation);
                break;
            case CommercialPanelAction.StartLine:
                SelectTool(CommercialTool.Line);
                break;
            case CommercialPanelAction.UndoPoint:
                ApplyConstruction(
                    new CommercialCoreCommand(CommercialCoreCommandKind.UndoLinePoint),
                    () => _legacySession!.UndoLinePoint(),
                    "마지막 선로 지점을 되돌렸습니다.");
                break;
            case CommercialPanelAction.CancelDraft:
                CancelDraft();
                break;
            case CommercialPanelAction.Commission:
                OrderOrComplete();
                break;
            case CommercialPanelAction.CycleLineClass:
                CycleLineClass();
                break;
            case CommercialPanelAction.KeepPromise:
                SetPromise(PromiseDecision.Keep);
                break;
            case CommercialPanelAction.DeferPromise:
                SetPromise(PromiseDecision.Defer);
                break;
            case CommercialPanelAction.ApproveWindow:
                if (_coreRun?.GetSnapshot().CampaignComplete == true)
                {
                    _presentedResult = null;
                    _showEpilogue = true;
                    _lastStatus = "여덟 임무의 실제 운영 기록과 장 시작 상태를 표시합니다.";
                    Render();
                }
                else
                {
                    ApproveWindow();
                }
                break;
            case CommercialPanelAction.RollbackProject:
                RollbackProject();
                break;
            case CommercialPanelAction.RestartChapter:
                ShowPause();
                break;
            case CommercialPanelAction.NewGame:
                _shell.ShowTitle(_hasContinuation, "새 게임은 확인 뒤 시작할 수 있습니다.");
                break;
            case CommercialPanelAction.NextThermalPhase:
                if (_coreRun?.GetSnapshot().CampaignComplete == true)
                {
                    _completedChapterSelectionIndex =
                        (_completedChapterSelectionIndex + 1) % _campaign.Chapters.Count;
                    _lastStatus = "다시 시작할 장의 시작 상태를 선택했습니다.";
                }
                else
                {
                    _thermalProjectionIndex =
                        (_thermalProjectionIndex + 1) % _thermalSequence.Intervals.Count;
                    _lastStatus = "작성된 다음 열 국면으로 지도를 전환했습니다.";
                }
                RefreshSelectedDemand();
                Render();
                break;
            case CommercialPanelAction.NextDemand:
                ThermalIntervalResult interval = _thermalSequence.Intervals[_thermalProjectionIndex];
                if (interval.Demands.Count > 0)
                {
                    int selectedIndex = Math.Max(0, interval.Demands.ToList().FindIndex(item =>
                        item.DemandId == _selectedDemandId));
                    _selectedDemandId = interval.Demands[
                        (selectedIndex + 1) % interval.Demands.Count].DemandId;
                    _lastStatus = "선택 수요의 발전원부터 시설까지 실제 공급 경로를 표시합니다.";
                    _lastError = string.Empty;
                    Render();
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private void SelectTool(CommercialTool tool)
    {
        if (_snapshot.Phase != ConstructionPhase.Ready)
        {
            RejectLocally("작성 중인 계획이나 공사를 먼저 마치세요.");
            return;
        }
        _presentedResult = null;
        _tool = tool;
        _lastError = string.Empty;
        _lastStatus = tool == CommercialTool.Substation
            ? "지도에서 변전소 원 전체가 들어갈 위치를 선택하세요."
            : "완공된 접속점에서 선로를 시작하세요.";
        RefreshPointerPreview();
        Render();
        _map.GrabFocus();
    }

    private void CycleLineClass()
    {
        if (_snapshot.Phase != ConstructionPhase.Ready)
        {
            RejectLocally("선종은 새 계획을 시작하기 전에 바꾸세요.");
            return;
        }
        _presentedResult = null;
        _lineClassId = _lineClassId == ReinforcedLineClassId
            ? StandardLineClassId
            : ReinforcedLineClassId;
        _poleClassId = _lineClassId == StandardLineClassId
            ? StandardPoleClassId
            : ReinforcedPoleClassId;
        _lastError = string.Empty;
        _lastStatus = _lineClassId == StandardLineClassId
            ? "값싸고 빠르지만 열여유가 작은 일반 배전선을 선택했습니다."
            : "비싸고 느리지만 연속 열여유가 큰 보강 배전선을 선택했습니다.";
        Render();
    }

    private void SetPromise(PromiseDecision decision)
    {
        if (_coreRun is null)
        {
            RejectLocally("이 열 연습에는 도시 약속 선택이 없습니다.");
            return;
        }
        ApplyCoreResult(
            _coreRun.Apply(new CommercialCoreCommand(
                CommercialCoreCommandKind.SetPromiseDecision,
                PromiseDecision: decision)),
            decision == PromiseDecision.Keep
                ? "산업단지 야간 증산 약속을 지키기로 했습니다."
                : "산업단지 야간 증산 약속을 미루기로 했습니다.");
    }

    private void ApproveWindow()
    {
        if (_coreRun is null)
        {
            RejectLocally("이 열 연습에는 승인할 운영 경계가 없습니다.");
            return;
        }
        CommercialCoreCommandResult result = _coreRun.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        string success = result.CompletedChapter is null
            ? "예고한 운영 국면을 승인했습니다. 다음 결정 경계를 확인하세요."
            : $"{result.CompletedChapter.ChapterDisplayName} 결과를 실제 공급 기록으로 확정했습니다.";
        if (result.Accepted)
        {
            bool protectiveOutage = result.DecisionPreview?.PhaseResults
                .SelectMany(item => item.Assets)
                .Any(item => item.NextState == ThermalOperatingState.ProtectiveOutage) == true;
            _audio.PlayLive(protectiveOutage
                ? ReleaseAudioCue.Outage
                : ReleaseAudioCue.Energize);
        }
        ApplyCoreResult(result, success);
    }

    private void RollbackProject()
    {
        if (_coreRun is null)
        {
            RejectLocally("이 열 연습에는 복구할 캠페인 공사가 없습니다.");
            return;
        }
        CommercialCoreCommandResult result = _coreRun.RollbackRecentProject();
        if (result.Accepted)
        {
            _audio.PlayLive(ReleaseAudioCue.Breaker);
        }
        ApplyCoreResult(
            result,
            "현재 장의 최근 완공 공사 직전으로 좌표·현금·시각·국면·약속·열 상태를 복구했습니다.");
    }

    private void RestartChapter()
    {
        if (_coreRun is null)
        {
            RejectLocally("이 열 연습에는 다시 시작할 캠페인 장이 없습니다.");
            return;
        }
        if (_coreRun.GetSnapshot().CampaignComplete)
        {
            StartCompletedChapter();
            return;
        }
        CommercialCoreCommandResult result = _coreRun.RestartChapter();
        if (result.Accepted)
        {
            _audio.PlayLive(ReleaseAudioCue.Breaker);
        }
        ApplyCoreResult(
            result,
            "현재 장 시작 journal로 좌표·현금·시각·국면·약속·열 상태를 복구했습니다.");
        if (result.Accepted && _shell.Page != ReleaseShellPage.Hidden)
        {
            _shell.HideShell();
        }
    }

    private void StartNewGame()
    {
        if (_coreRun is null)
        {
            RejectLocally("이 열 연습에는 새로 시작할 캠페인이 없습니다.");
            return;
        }
        bool fromTitle = _shell.Page == ReleaseShellPage.Title ||
            _shell.Page == ReleaseShellPage.Confirm;
        bool preservedIncompatible = false;
        if (_incompatibleSavePending && _savePath is not null && File.Exists(_savePath))
        {
            try
            {
                _ = CommercialCampaignPersistenceStore.PreserveIncompatible(_savePath);
                preservedIncompatible = true;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                _lastError = "이전 저장을 보존하지 못해 새 게임을 시작하지 않았습니다.";
                Render();
                if (_shell.Page != ReleaseShellPage.Hidden)
                {
                    _shell.ShowPersistenceError(_lastError);
                }
                return;
            }
        }
        _coreRun = new CommercialCoreRun(_commercialWorld, _campaign);
        _snapshot = _coreRun.GetSnapshot().Construction;
        _tool = CommercialTool.None;
        _presentedResult = null;
        _showEpilogue = false;
        _completedChapterSelectionIndex = 0;
        _thermalProjectionIndex = 0;
        _saveWritable = true;
        _incompatibleSavePending = false;
        _lastStatus = preservedIncompatible
            ? "새 게임을 시작했습니다. 이전 비호환 저장은 별도 파일로 보존했습니다."
            : "새 게임을 첫 불빛부터 시작했습니다.";
        _lastError = string.Empty;
        bool saved = PersistCoreRun();
        _hasContinuation = saved;
        RefreshThermalProjection();
        RefreshPointerPreview();
        Render();
        if (_shell.Page != ReleaseShellPage.Hidden)
        {
            _shell.HideShell();
        }
        if (fromTitle)
        {
            if (_settings.ShowControlHelp)
            {
                _shell.ShowControlHelpBeforeGameplay();
            }
            else
            {
                _shell.HideShell();
            }
        }
    }

    private void StartCompletedChapter()
    {
        CommercialCoreSnapshot snapshot = _coreRun?.GetSnapshot()
            ?? throw new InvalidOperationException("완료 캠페인 runner가 없습니다.");
        if (!snapshot.CampaignComplete ||
            _completedChapterSelectionIndex < 0 ||
            _completedChapterSelectionIndex >= snapshot.ChapterStartCommandCounts.Count)
        {
            RejectLocally("완료한 캠페인에서 다시 시작할 장을 선택하세요.");
            return;
        }
        int commandCount = snapshot.ChapterStartCommandCounts[_completedChapterSelectionIndex];
        _coreRun = CommercialCoreRun.Restore(
            _commercialWorld,
            _campaign,
            _coreRun!.GetCommands().Take(commandCount).ToArray());
        _snapshot = _coreRun.GetSnapshot().Construction;
        _tool = CommercialTool.None;
        _presentedResult = null;
        _showEpilogue = false;
        _thermalProjectionIndex = 0;
        _lastStatus = $"{_coreRun.GetSnapshot().Chapter.DisplayName} 시작 상태에서 다시 시작합니다.";
        _lastError = string.Empty;
        PersistCoreRun();
        RefreshThermalProjection();
        RefreshPointerPreview();
        Render();
        if (_shell.Page != ReleaseShellPage.Hidden)
        {
            _shell.HideShell();
        }
    }

    private void ApplyCoreResult(CommercialCoreCommandResult result, string success)
    {
        _snapshot = result.Snapshot.Construction;
        if (result.Accepted)
        {
            _presentedResult = result.CompletedChapter;
            _lastStatus = success;
            _lastError = string.Empty;
            _tool = CommercialTool.None;
            PersistCoreRun();
            if (result.CompletedChapter is not null)
            {
                int completedIndex = result.Snapshot.ChapterResults.Count - 1;
                _audio.QueueResultLive(
                    completedIndex == 0 || result.Snapshot.CampaignComplete,
                    result.Snapshot.CampaignComplete);
            }
        }
        else
        {
            _lastError = CoreErrorText(result.Error, result.ConstructionError);
            _audio.PlayLive(ReleaseAudioCue.Warning);
        }
        RefreshThermalProjection();
        RefreshPointerPreview();
        Render();
    }

    private void CancelDraft()
    {
        CommercialCoreCommandKind? kind = _snapshot.Phase switch
        {
            ConstructionPhase.NodeDrafting => CommercialCoreCommandKind.CancelNodeDraft,
            ConstructionPhase.LineDrafting => CommercialCoreCommandKind.CancelLineDraft,
            _ => null,
        };
        if (kind is null)
        {
            RejectLocally("취소할 작성 중 계획이 없습니다.");
            return;
        }
        bool accepted = ApplyConstruction(
            new CommercialCoreCommand(kind.Value),
            () => kind == CommercialCoreCommandKind.CancelNodeDraft
                ? _legacySession!.CancelNodeDraft()
                : _legacySession!.CancelLineDraft(),
            "작성 중인 계획을 취소했습니다.");
        if (!accepted) return;
        _tool = CommercialTool.None;
        RefreshPointerPreview();
        Render();
    }

    private void OrderOrComplete()
    {
        CommercialCoreCommandKind? kind;
        string success;
        switch (_snapshot.Phase)
        {
            case ConstructionPhase.NodeDrafting:
                kind = CommercialCoreCommandKind.OrderNode;
                success = "변전소 공사를 발주했습니다. 완공 시각까지 진행할 수 있습니다.";
                break;
            case ConstructionPhase.LineDrafting:
                kind = CommercialCoreCommandKind.OrderLine;
                success = "선로 공사를 발주했습니다. 완공 시각까지 진행할 수 있습니다.";
                break;
            case ConstructionPhase.NodeBuilding:
            case ConstructionPhase.LineBuilding:
                kind = CommercialCoreCommandKind.AdvanceConstruction;
                success = "공사가 끝났습니다. 완공 설비를 다음 계획에 바로 연결할 수 있습니다.";
                break;
            default:
                kind = null;
                success = string.Empty;
                break;
        }
        if (kind is null)
        {
            RejectLocally("지금 발주하거나 완공할 공사가 없습니다.");
            return;
        }
        bool accepted = ApplyConstruction(
            new CommercialCoreCommand(kind.Value),
            () => kind switch
            {
                CommercialCoreCommandKind.OrderNode => _legacySession!.OrderNode(),
                CommercialCoreCommandKind.OrderLine => _legacySession!.OrderLine(),
                _ => _legacySession!.AdvanceToConstructionCompletion(),
            },
            success);
        if (accepted && _snapshot.Phase == ConstructionPhase.Ready)
        {
            _tool = CommercialTool.None;
            RefreshPointerPreview();
            Render();
        }
    }

    private bool ApplyConstruction(
        CommercialCoreCommand command,
        Func<ConstructionCommandResult> legacyCommand,
        string success)
    {
        if (_legacySession is not null)
        {
            ConstructionCommandResult result = legacyCommand();
            _snapshot = result.Snapshot;
            if (result.Accepted)
            {
                _presentedResult = null;
                _lastStatus = success;
                _lastError = string.Empty;
                PlayConstructionCue(command.Kind);
            }
            else
            {
                _lastError = ErrorText(result.Error);
                _audio.PlayLive(ReleaseAudioCue.Warning);
            }
            AfterStateChange(result.Accepted);
            return result.Accepted;
        }

        CommercialCoreCommandResult coreResult = _coreRun!.Apply(command);
        _snapshot = coreResult.Snapshot.Construction;
        if (coreResult.Accepted)
        {
            _presentedResult = null;
            _lastStatus = success;
            _lastError = string.Empty;
            PlayConstructionCue(command.Kind);
            PersistCoreRun();
        }
        else
        {
            _lastError = CoreErrorText(coreResult.Error, coreResult.ConstructionError);
            _audio.PlayLive(ReleaseAudioCue.Warning);
        }
        AfterStateChange(coreResult.Accepted);
        return coreResult.Accepted;
    }

    private void PlayConstructionCue(CommercialCoreCommandKind kind)
    {
        if (kind is CommercialCoreCommandKind.OrderNode or CommercialCoreCommandKind.OrderLine)
        {
            _audio.PlayLive(ReleaseAudioCue.Order);
        }
        else if (kind == CommercialCoreCommandKind.AdvanceConstruction)
        {
            _audio.PlayLive(ReleaseAudioCue.Complete);
        }
    }

    private void AfterStateChange(bool accepted)
    {
        if (accepted &&
            _snapshot.Phase is not
                (ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding))
        {
            RefreshThermalProjection();
        }
        RefreshPointerPreview();
        Render();
    }

    private NodePlacementPreview PreviewNodePlacement(string classId, CoreMapPoint position) =>
        _legacySession?.PreviewNodePlacement(classId, position) ??
        _coreRun!.PreviewNodePlacement(classId, position);

    private LineStartPreview PreviewLineStart(
        string startNodeId,
        string lineClassId,
        string poleClassId) =>
        _legacySession?.PreviewLineStart(startNodeId, lineClassId, poleClassId) ??
        _coreRun!.PreviewLineStart(startNodeId, lineClassId, poleClassId);

    private LinePointPreview PreviewLinePoint(CoreMapPoint position) =>
        _legacySession?.PreviewLinePoint(position) ?? _coreRun!.PreviewLinePoint(position);

    private LinePointMovePreview PreviewMoveLinePoint(int index, CoreMapPoint position) =>
        _legacySession?.PreviewMoveLinePoint(index, position) ??
        _coreRun!.PreviewMoveLinePoint(index, position);

    private LineFinishPreview PreviewLineFinish(string endNodeId) =>
        _legacySession?.PreviewLineFinish(endNodeId) ??
        _coreRun!.PreviewLineFinish(endNodeId);

    private ConstructionQuote PreviewNodeOrder() =>
        _legacySession?.PreviewNodeOrder() ?? _coreRun!.PreviewNodeOrder();

    private ConstructionQuote PreviewLineOrder() =>
        _legacySession?.PreviewLineOrder() ?? _coreRun!.PreviewLineOrder();

    private void MoveDraftPoint(CommercialDraftPointDrag drag)
    {
        if (_snapshot.Phase != ConstructionPhase.LineDrafting ||
            _snapshot.LineDraft is null ||
            drag.PointIndex < 0 ||
            drag.PointIndex >= _snapshot.LineDraft.IntermediatePoints.Count)
        {
            RejectLocally("옮길 수 있는 작성 중 전신주가 없습니다.");
            return;
        }

        ApplyConstruction(
            new CommercialCoreCommand(
                CommercialCoreCommandKind.MoveLinePoint,
                Position: drag.Position,
                PointIndex: drag.PointIndex),
            () => _legacySession!.MoveLinePoint(drag.PointIndex, drag.Position),
            "작성 중인 전신주 위치를 옮겼습니다.");
    }

    private void OnDraftPointDragPreviewChanged(CommercialDraftPointDrag? drag)
    {
        if (drag is not CommercialDraftPointDrag actual ||
            _snapshot.Phase != ConstructionPhase.LineDrafting ||
            _snapshot.LineDraft is null ||
            actual.PointIndex < 0 ||
            actual.PointIndex >= _snapshot.LineDraft.IntermediatePoints.Count)
        {
            RefreshPointerPreview();
            Render();
            return;
        }

        _pointerPoint = actual.Position;
        LinePointMovePreview preview = PreviewMoveLinePoint(
            actual.PointIndex,
            actual.Position);
        string lengths = preview.NextSegmentLengthUnit is long next
            ? $"앞 {FormatDesignDistance(preview.PreviousSegmentLengthUnit)} · 뒤 {FormatDesignDistance(next)}"
            : $"앞 {FormatDesignDistance(preview.PreviousSegmentLengthUnit)}";
        SetPreview(
            preview.Accepted,
            preview.Error,
            preview.RiskAreaIds,
            preview.Accepted
                ? $"전신주 이동 가능 · {lengths} / 허용 {FormatDesignDistance(preview.MaxSpanUnit)}"
                : null);
        Render();
    }

    private void RejectLocally(string message)
    {
        _lastError = message;
        _audio.PlayLive(ReleaseAudioCue.Warning);
        Render();
    }

    private void RefreshPointerPreview()
    {
        _pointerAccepted = true;
        _pointerError = null;
        _pointerMessage = ToolInstruction();
        _pointerRiskAreaIds = Array.Empty<string>();
        if (_pointerPoint is not CoreMapPoint point)
        {
            return;
        }

        if (_tool == CommercialTool.Substation &&
            _snapshot.Phase is ConstructionPhase.Ready or ConstructionPhase.NodeDrafting)
        {
            NodePlacementPreview preview = PreviewNodePlacement(SubstationClassId, point);
            SetPreview(
                preview.Accepted,
                preview.Error,
                preview.RiskAreaIds,
                preview.Accepted ? "배치 가능 · 클릭하여 계획 위치 확정" : null);
            return;
        }

        if (_tool == CommercialTool.Line && _snapshot.Phase == ConstructionPhase.Ready)
        {
            if (_candidateNodeId is null)
            {
                _pointerAccepted = false;
                _pointerMessage = "선로를 시작할 접속점 가까이로 이동하세요.";
                return;
            }
            LineStartPreview preview = PreviewLineStart(
                _candidateNodeId,
                _lineClassId,
                _poleClassId);
            SetPreview(preview.Accepted, preview.Error, Array.Empty<string>(),
                preview.Accepted ? "연결 시작 가능 · 클릭하여 확정" : null);
            _pointerMessage += $" · {ConnectionChangeText(_candidateNodeId)}";
            return;
        }

        if (_tool == CommercialTool.Line &&
            _snapshot.Phase == ConstructionPhase.LineDrafting &&
            _snapshot.LineDraft?.EndNodeId is null)
        {
            if (_candidateNodeId is string candidate)
            {
                LineFinishPreview preview = PreviewLineFinish(candidate);
                SetPreview(
                    preview.Accepted,
                    preview.Error,
                    preview.RiskAreaIds,
                    preview.Accepted
                        ? $"접속 가능 · 마지막 구간 {FormatDesignDistance(preview.SegmentLengthUnit)} / 허용 {FormatDesignDistance(preview.MaxSpanUnit)}"
                        : null);
                _pointerMessage += $" · {ConnectionChangeText(candidate)}";
            }
            else
            {
                LinePointPreview preview = PreviewLinePoint(point);
                SetPreview(
                    preview.Accepted,
                    preview.Error,
                    preview.RiskAreaIds,
                    preview.Accepted
                        ? $"전신주 추가 가능 · 현재 구간 {FormatDesignDistance(preview.SegmentLengthUnit)} / 허용 {FormatDesignDistance(preview.MaxSpanUnit)}"
                        : null);
            }
        }
    }

    private void SetPreview(
        bool accepted,
        ConstructionError? error,
        IReadOnlyList<string> riskAreaIds,
        string? acceptedMessage)
    {
        _pointerAccepted = accepted;
        _pointerError = error;
        _pointerRiskAreaIds = riskAreaIds;
        _pointerMessage = accepted
            ? AppendRisk(acceptedMessage ?? "선택할 수 있습니다.", riskAreaIds)
            : ErrorText(error);
    }

    private void Render()
    {
        if (_map is null || _panel is null)
        {
            return;
        }
        ThermalIntervalResult thermal = _thermalSequence.Intervals[_thermalProjectionIndex];
        ThermalDemandResult? selectedDemand = SelectedDemand(thermal);
        string? selectedDemandNodeId = selectedDemand is null
            ? null
            : DemandNodeId(thermal.IntervalId, selectedDemand.DemandId);
        CommercialCoreSnapshot? core = _coreRun?.GetSnapshot();
        int chapterIndex = core?.ChapterIndex ?? 0;
        _map.SetPresentation(new CommercialMapPresentation(
            _snapshot,
            _pointerPoint,
            _pointerAccepted,
            _pointerMessage,
            ToolName(),
            PointerFootprintRadius(),
            _tool is CommercialTool.Line or CommercialTool.None,
            thermal,
            _selectedThermalAssetId,
            selectedDemandNodeId,
            selectedDemand?.PathNodeIds ?? Array.Empty<string>(),
            selectedDemand?.PathEdgeIds ?? Array.Empty<string>(),
            ComparisonPathEdgeIds(core),
            ActiveRiskAreaIds(thermal.IntervalId),
            FacilityPresentations(thermal),
            chapterIndex,
            _settings.ReduceMotion));
        // The pinned references do not use one invariant inspector height. The
        // first construction screen has a compact equipment card, while route,
        // siting, heat, and flood decisions use a docked full-height comparison
        // inspector. Preserve that chapter hierarchy without changing gameplay
        // facts or covering the independent event timeline.
        bool decisionComparisonReady = _coreRun is not null &&
            _snapshot.Phase == ConstructionPhase.Ready &&
            _coreRun.PreviewDecisionWindow().Accepted;
        _panel.OffsetBottom = chapterIndex switch
        {
            0 => -560f,
            4 when decisionComparisonReady => -520f,
            1 or 2 or 5 when decisionComparisonReady => -88f,
            _ => -430f,
        };
        _panel.SetModel(BuildPanelModel());
        _timeline.SetPresentation(BuildTimelinePresentation(core));
        _zoomLabel.Text = _map.ZoomLabel;
        _titleLabel.Text = "⚙";
        _titleLabel.TooltipText = core is null
            ? "GRIDWORKS · 열 운영"
            : $"GRIDWORKS · {_presentedResult?.ChapterDisplayName ?? core.Chapter.DisplayName}";
        _audio.SetAtmosphere(core?.ChapterIndex ?? 0);
        int commissionedEdges = _snapshot.World.Edges.Count(edge => edge.Commissioned);
        string boundaryText = core is null
            ? $"▣ {ThermalIntervalName(thermal.IntervalId)}"
            : core.CampaignComplete
                ? "▣ 여덟 임무 완료"
                : $"▣ 경계 {core.DecisionWindowIndex + 1}/{core.Chapter.DecisionWindows.Count} · " +
                  ThermalIntervalName(thermal.IntervalId);
        _boundaryLabel.Text = chapterIndex == 4
            ? $"☀ 폭염 +8°C · {boundaryText}"
            : boundaryText;
        _cashLabel.Text = core is null
            ? $"◷ {_snapshot.Minute}분"
            : $"₩ {FormatWon(core.CashUnit)}";
        _supplyLabel.Text = "⚡ " + HardSupplySummary(thermal)
            .Replace("필수 공급 ", "필수 공급 · ", StringComparison.Ordinal);
        _summaryLabel.Text = core is null
            ? $"완공 선로 {commissionedEdges}구간 · 지도 설비를 선택해 현재와 다음 상태를 확인하세요."
            : $"{core.Chapter.Objective} · 완공 선로 {commissionedEdges}구간";
    }

    private IReadOnlyList<IReadOnlyList<string>> ComparisonPathEdgeIds(
        CommercialCoreSnapshot? core)
    {
        if (core?.ChapterIndex != 1)
        {
            return Array.Empty<IReadOnlyList<string>>();
        }

        return _thermalSequence.Intervals
            .SelectMany(interval => interval.Demands)
            .Where(demand => demand.Supplied && demand.PathEdgeIds.Count > 0)
            .Select(demand => (IReadOnlyList<string>)demand.PathEdgeIds.ToArray())
            .DistinctBy(path => string.Join("\u001f", path), StringComparer.Ordinal)
            .Take(2)
            .ToArray();
    }

    private CommercialEventTimelinePresentation BuildTimelinePresentation(
        CommercialCoreSnapshot? core)
    {
        if (core is null)
        {
            CommercialTimelineStep[] thermalSteps = _thermalSequence.Intervals
                .Select((interval, index) => new CommercialTimelineStep(
                    ThermalIntervalName(interval.IntervalId),
                    "열 운영 projection",
                    index < _thermalProjectionIndex
                        ? CommercialTimelineStepState.Completed
                        : index == _thermalProjectionIndex
                            ? CommercialTimelineStepState.Current
                            : CommercialTimelineStepState.Upcoming))
                .ToArray();
            return new CommercialEventTimelinePresentation(
                "열 운영 연습",
                $"공사 시각 {_snapshot.Minute}분",
                0f,
                thermalSteps);
        }

        if (_showEpilogue && core.CampaignComplete)
        {
            var epilogueSteps = _campaign.Chapters
                .Select(chapter => new CommercialTimelineStep(
                    chapter.DisplayName,
                    "완료한 임무",
                    CommercialTimelineStepState.Completed))
                .Append(new CommercialTimelineStep(
                    "에필로그",
                    _campaign.Epilogue.Title,
                    CommercialTimelineStepState.Current))
                .ToArray();
            return new CommercialEventTimelinePresentation(
                "청류시 전체 기록",
                "8/8 임무 완료",
                1f,
                epilogueSteps);
        }

        CommercialCoreChapter chapter = _presentedResult is null
            ? core.Chapter
            : _campaign.Chapters.Single(item => item.ChapterId == _presentedResult.ChapterId);
        bool showingResult = _presentedResult is not null || core.CampaignComplete;
        int chapterIndex = _campaign.Chapters.ToList().FindIndex(item => item.ChapterId == chapter.ChapterId);
        int activeWindowIndex = Math.Clamp(
            core.DecisionWindowIndex,
            0,
            Math.Max(0, chapter.DecisionWindows.Count - 1));
        CommercialCoreDecisionWindow activeWindow = chapter.DecisionWindows[activeWindowIndex];
        CommercialCoreOperatingPhase activePhase = chapter.OperatingPhases.First(item =>
            item.PhaseId == activeWindow.NextPhaseId);
        var steps = new List<CommercialTimelineStep>
        {
            new("브리핑", chapter.Briefing.Title, CommercialTimelineStepState.Completed),
            new(
                activePhase.DisplayName,
                activeWindow.Story?.Title ?? $"결정 경계 {activeWindowIndex + 1}",
                showingResult
                    ? CommercialTimelineStepState.Completed
                    : CommercialTimelineStepState.Current),
        };
        CommercialChapterResultRecord? presentedResult = _presentedResult ??
            (core.CampaignComplete && core.ChapterResults.Count > 0
                ? core.ChapterResults[^1]
                : null);
        steps.Add(new CommercialTimelineStep(
            "결과",
            presentedResult?.Story.Title ?? chapter.StandardResult.Title,
            showingResult ? CommercialTimelineStepState.Current : CommercialTimelineStepState.Upcoming));
        steps.Add(new CommercialTimelineStep(
            "다음",
            chapterIndex + 1 < _campaign.Chapters.Count
                ? _campaign.Chapters[chapterIndex + 1].DisplayName
                : _campaign.Epilogue.Title,
            CommercialTimelineStepState.Upcoming));
        string progress = showingResult
            ? "운영 결과 확정"
            : $"공사 {_snapshot.Minute}/{chapter.DeadlineMinute}분 · {ToolName()}";
        float ratio = showingResult
            ? 1f
            : Math.Clamp(_snapshot.Minute / (float)Math.Max(1, chapter.DeadlineMinute), 0f, 1f);
        return new CommercialEventTimelinePresentation(
            $"{chapterIndex + 1}/{_campaign.Chapters.Count} · {chapter.DisplayName}",
            progress,
            ratio,
            steps);
    }

    private IReadOnlyList<string> ActiveRiskAreaIds(string intervalId) =>
        _campaign.Chapters.SelectMany(item => item.OperatingPhases)
            .FirstOrDefault(item => item.PhaseId == intervalId)?.ActiveRiskAreaIds ??
        Array.Empty<string>();

    private CommercialTaskPanelModel BuildPanelModel()
    {
        ConstructionQuote? quote = _snapshot.Phase switch
        {
            ConstructionPhase.NodeDrafting => PreviewNodeOrder(),
            ConstructionPhase.LineDrafting => PreviewLineOrder(),
            _ => null,
        };
        bool draft = _snapshot.Phase is ConstructionPhase.NodeDrafting or ConstructionPhase.LineDrafting;
        bool building = _snapshot.Phase is ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding;
        CommercialCoreSnapshot? core = _coreRun?.GetSnapshot();
        string risk = RiskText(CurrentRiskAreaIds());
        string quoteText = quote?.Accepted == true
            ? $"예상 공사비 {FormatWon(quote.CostCashUnit!.Value)} · 공사 {quote.BuildMinutes}분" +
              (string.IsNullOrEmpty(risk) ? string.Empty : $"\n{risk}")
            : string.IsNullOrEmpty(risk)
                ? core is null
                    ? "자유 배치와 열 상태를 확인하세요."
                    : $"현금 {FormatWon(core.CashUnit)} · 기한 {core.Chapter.DeadlineMinute}분"
                : risk;
        string error = !string.IsNullOrEmpty(_lastError)
            ? _lastError
            : _pointerAccepted
                ? string.Empty
                : _pointerMessage;
        ThermalIntervalResult thermalInterval = _thermalSequence.Intervals[_thermalProjectionIndex];
        ThermalDemandResult? selectedDemand = SelectedDemand(thermalInterval);
        ThermalAssetResult? selectedThermal = thermalInterval.Assets.FirstOrDefault(item =>
            item.AssetId == _selectedThermalAssetId);
        SpatialNodeDefinition? selectedSpatialNode = _snapshot.World.Nodes.FirstOrDefault(item =>
            item.NodeId == _selectedThermalAssetId);
        int selectedServiceRadius = selectedSpatialNode is null
            ? 0
            : _snapshot.World.NodeClasses.Single(item =>
                item.ClassId == selectedSpatialNode.ClassId).ServiceRadiusUnit;
        string serviceAreaText = selectedServiceRadius > 0
            ? $"\n서비스 권역 · 반경 " +
              (selectedServiceRadius / 100m).ToString("0.##", CultureInfo.InvariantCulture) +
              " 설계거리"
            : string.Empty;
        string thermalText = selectedThermal is null
            ? $"열 국면 · {ThermalIntervalName(thermalInterval.IntervalId)}\n" +
              "열 한계가 있는 선로·변전소·접속부를 지도에서 선택하세요."
            : $"열 국면 · {ThermalIntervalName(thermalInterval.IntervalId)}\n" +
              $"{AssetDisplayName(_selectedThermalAssetId)}\n" +
              $"사용 {FormatPower(selectedThermal.UseKw)} / 연속 {FormatPower(selectedThermal.ContinuousLimitKw)} / " +
              $"비상 {FormatPower(selectedThermal.EmergencyLimitKw)}\n" +
              $"현재 {ThermalStateText(selectedThermal.CurrentState)} · 다음 {ThermalStateText(selectedThermal.NextState)}" +
              serviceAreaText;
        if (selectedDemand is not null)
        {
            string demandName = DemandDisplayName(thermalInterval.IntervalId, selectedDemand.DemandId);
            string sourceName = selectedDemand.SourceNodeId is null
                ? "발전원 미확정"
                : AssetDisplayName(selectedDemand.SourceNodeId);
            string facilityName = DemandNodeId(thermalInterval.IntervalId, selectedDemand.DemandId) is string nodeId
                ? AssetDisplayName(nodeId)
                : "수요 시설";
            string route = selectedDemand.Supplied
                ? $"{sourceName} → 선로 {selectedDemand.PathEdgeIds.Count}구간 → {facilityName}"
                : $"공급 실패 · {SupplyFailureText(selectedDemand.Failure)}";
            string margin = selectedDemand.MinimumRemainingLimitKw is long remaining
                ? $"최소 열여유 {FormatPower(remaining)}"
                : "최소 열여유 없음";
            string bottleneck = selectedDemand.FirstBottleneckAssetId is string bottleneckId
                ? $" · 첫 병목 {AssetDisplayName(bottleneckId)}"
                : string.Empty;
            thermalText = $"선택 수요 · {demandName}\n{route}\n{margin}{bottleneck}\n" + thermalText;
        }
        if (core is not null && !core.CampaignComplete)
        {
            CommercialDecisionPreview forecast = _coreRun!.PreviewDecisionWindow();
            thermalText += forecast.Accepted
                ? "\n예고 · 공개된 안전 의무와 선택한 약속을 공급할 수 있습니다."
                : $"\n예고 · {CoreErrorText(forecast.Error, null)}";
        }
        bool promiseAvailable = core is not null && !core.CampaignComplete &&
            core.Chapter.Promise is not null && core.DecisionWindowIndex == 0 &&
            _snapshot.Phase == ConstructionPhase.Ready;
        bool approvalAvailable = core is not null && !core.CampaignComplete &&
            _snapshot.Phase == ConstructionPhase.Ready &&
            (core.Chapter.Promise is null || core.PromiseDecision is not null);
        approvalAvailable |= core?.CampaignComplete == true && !_showEpilogue;
        bool canConstruct = _snapshot.Phase == ConstructionPhase.Ready &&
            core?.CampaignComplete != true;
        ThermalAssetResult? facilityThermal = selectedThermal ?? thermalInterval.Assets
            .OrderByDescending(item => item.AuthoredUnavailable ||
                item.CurrentState == ThermalOperatingState.ProtectiveOutage)
            .ThenByDescending(item => item.CurrentState == ThermalOperatingState.Emergency)
            .ThenByDescending(item => item.UseKw)
            .FirstOrDefault();
        bool facilityUnavailable = facilityThermal is not null &&
            (facilityThermal.AuthoredUnavailable ||
             facilityThermal.CurrentState == ThermalOperatingState.ProtectiveOutage);
        string facilityType = facilityThermal is null
            ? "배전 변전소"
            : AssetDisplayName(facilityThermal.AssetId);
        string facilityCapacity = facilityThermal is null
            ? "4.0 MW"
            : facilityUnavailable
                ? "0 MW"
                : FormatPower(facilityThermal.UseKw);
        string facilityState = facilityThermal is null
            ? "연속 운전 · 정상"
            : facilityThermal.AuthoredUnavailable
                ? "사용 불가 · 계통 분리"
                : ThermalStateText(facilityThermal.CurrentState);
        bool routeComparisonCards = string.Equals(
            core?.Chapter.ChapterId,
            "SECOND_HEART",
            StringComparison.Ordinal);
        bool sourceAcceptanceCards = string.Equals(
            core?.Chapter.ChapterId,
            "SECOND_SOURCE",
            StringComparison.Ordinal);
        bool showComparisonCards = routeComparisonCards || sourceAcceptanceCards;
        ThermalIntervalResult comparisonAInterval = _thermalSequence.Intervals[0];
        ThermalIntervalResult comparisonBInterval = routeComparisonCards &&
            _thermalSequence.Intervals.Count > 1
                ? _thermalSequence.Intervals[1]
                : _thermalSequence.Intervals[0];
        ThermalDemandResult? comparisonADemand = comparisonAInterval.Demands.FirstOrDefault();
        ThermalDemandResult? comparisonBDemand = sourceAcceptanceCards
            ? comparisonBInterval.Demands.Skip(1).FirstOrDefault() ??
              comparisonBInterval.Demands.FirstOrDefault()
            : comparisonBInterval.Demands.FirstOrDefault();
        (string Metric, string Detail) ComparisonFact(
            ThermalIntervalResult interval,
            ThermalDemandResult? demand)
        {
            if (demand is null)
            {
                return ("경로 계산 중", ThermalIntervalName(interval.IntervalId));
            }
            string metric = demand.Supplied
                ? $"{FormatPower(demand.DemandKw)}  ✓"
                : "0 kW  ×";
            string margin = demand.MinimumRemainingLimitKw is long remaining
                ? $"여유 {FormatPower(remaining)}"
                : "열여유 없음";
            string detail = demand.Supplied
                ? $"{ThermalIntervalName(interval.IntervalId)}\n" +
                  $"실제 경로 {demand.PathEdgeIds.Count}구간\n" +
                  $"{margin} · 전력 흐름 검증"
                : $"{ThermalIntervalName(interval.IntervalId)} · " +
                  SupplyFailureText(demand.Failure);
            return (metric, detail);
        }
        (string comparisonAMetric, string comparisonADetail) =
            ComparisonFact(comparisonAInterval, comparisonADemand);
        (string comparisonBMetric, string comparisonBDetail) =
            ComparisonFact(comparisonBInterval, comparisonBDemand);
        return new CommercialTaskPanelModel(
            HeadingText(),
            SpeakerPresentation(),
            InstructionText(),
            ObligationsText(core),
            string.IsNullOrWhiteSpace(_pointerMessage)
                ? "지도에서 작업 위치를 선택하세요."
                : _pointerMessage,
            quoteText,
            thermalText,
            core is null
                ? $"공사 기준 시각 {_snapshot.Minute}분 · {_lastStatus}"
                : $"공사 {_snapshot.Minute}/{core.Chapter.DeadlineMinute}분 · " +
                  $"현금 {FormatWon(core.CashUnit)} · {_lastStatus}",
            error,
            facilityType,
            facilityCapacity,
            facilityState,
            facilityUnavailable,
            showComparisonCards,
            routeComparisonCards ? "북안 A · 차단시험" : "생활권 A · 인수 경로",
            comparisonAMetric,
            comparisonADetail,
            routeComparisonCards ? "강변 B · 차단시험" : "의료원 B · 인수 경로",
            comparisonBMetric,
            comparisonBDetail,
            new CommercialActionPresentation(
                canConstruct,
                "변전소 놓기",
                "소형 배전 변전소의 점유영역을 지형 위에서 자유롭게 계획합니다.",
                Visible: false),
            new CommercialActionPresentation(
                canConstruct,
                "선로 잇기",
                "접속점에서 시작해 전신주 위치를 차례로 놓고 다른 접속점에 연결합니다.",
                Visible: false),
            new CommercialActionPresentation(
                _snapshot.Phase == ConstructionPhase.LineDrafting,
                "마지막 점 되돌리기",
                "계획한 마지막 접속점 또는 전신주 한 곳을 되돌립니다."),
            new CommercialActionPresentation(
                draft,
                "계획 취소",
                "작성 중인 계획 전체를 취소합니다."),
            new CommercialActionPresentation(
                building || quote?.Accepted == true,
                building ? "완공까지 진행" : "공사 발주",
                building
                    ? "표시된 완공 시각까지 진행해 설비를 사용할 수 있게 합니다."
                    : "현재 계획과 견적을 확정하고 공사를 시작합니다."),
            new CommercialActionPresentation(
                canConstruct,
                _lineClassId == StandardLineClassId
                    ? "선종 · 일반 배전선"
                    : "선종 · 보강 배전선",
                "일반선은 값싸고 빠르며 비상 열여유를 쓸 수 있고, 보강선은 비싸고 느리지만 연속 열여유가 큽니다.",
                Visible: false),
            new CommercialActionPresentation(
                promiseAvailable,
                core?.PromiseDecision == PromiseDecision.Keep ? "약속 지킴 ✓" : "약속 지킴",
                "도시 약속 수요를 공급 후보에 포함합니다. 첫 네 임무에서는 연속 한계만 사용합니다.",
                Visible: core?.Chapter.Promise is not null),
            new CommercialActionPresentation(
                promiseAvailable,
                core?.PromiseDecision == PromiseDecision.Defer ? "약속 미룸 ✓" : "약속 미룸",
                "도시 약속 수요를 이번 공급 후보에서 제외하지만 지원금과 필수 진행은 바꾸지 않습니다.",
                Visible: core?.Chapter.Promise is not null),
            new CommercialActionPresentation(
                approvalAvailable,
                core?.CampaignComplete == true ? "에필로그 보기" : "다음 경계 승인",
                core?.CampaignComplete == true
                    ? "여덟 임무의 실제 의무·약속·비상 운전·남은 자금 기록을 엽니다."
                    : "예고 결과와 같은 운영 국면을 승인합니다. 안전 의무 실패 시 아무 상태도 바뀌지 않습니다."),
            new CommercialActionPresentation(
                core?.RecentProjectCheckpointCommandCount is not null,
                "최근 공사 복구",
                "현재 장에서 마지막으로 완공한 공사 묶음 직전 journal 상태를 fresh replay합니다."),
            new CommercialActionPresentation(
                core is not null,
                core?.CampaignComplete == true ? "선택 장부터 다시" : "현재 장 처음부터",
                core?.CampaignComplete == true
                    ? "선택한 완료 장의 시작 journal 상태에서 새 진행을 시작합니다."
                    : "현재 장 시작 journal로 되돌려 좌표·현금·시각·국면·약속·열 상태를 다시 만듭니다.",
                Visible: false),
            new CommercialActionPresentation(
                core is not null,
                "새 게임",
                "현재 진행을 지우고 첫 불빛부터 시작합니다. 비호환 저장은 별도 파일로 보존합니다.",
                Visible: false),
            new CommercialActionPresentation(
                core?.CampaignComplete == true || _thermalSequence.Intervals.Count > 1,
                core?.CampaignComplete == true
                    ? $"완료 장 선택 · {_completedChapterSelectionIndex + 1}/{_campaign.Chapters.Count}"
                    : $"다음 국면 보기 · {_thermalProjectionIndex + 1}/{_thermalSequence.Intervals.Count}",
                core?.CampaignComplete == true
                    ? "다시 시작할 장의 시작 상태를 다음 장으로 바꿉니다."
                    : "작성된 국면을 바꾸어 사용 불가, 실제 경로, 보호정지와 복귀 상태를 함께 비교합니다.",
                Visible: core?.CampaignComplete == true || _thermalSequence.Intervals.Count > 1),
            new CommercialActionPresentation(
                thermalInterval.Demands.Count > 1,
                thermalInterval.Demands.Count == 0
                    ? "수요 경로 없음"
                    : $"수요 경로 · {SelectedDemandOrdinal(thermalInterval)}/{thermalInterval.Demands.Count}",
                "다음 수요를 선택해 발전원, 전체 실제 경로, 최소 열여유와 수요 시설을 함께 강조합니다.",
                Visible: thermalInterval.Demands.Count > 0));
    }

    private string HeadingText()
    {
        CommercialCoreSnapshot? core = _coreRun?.GetSnapshot();
        if (_presentedResult is not null)
        {
            return $"{_presentedResult.ChapterDisplayName} · 결과";
        }
        if (core is not null && core.CampaignComplete)
        {
            return _showEpilogue
                ? $"에필로그 · {_campaign.Epilogue.Title}"
                : "여덟 임무 완료 · 마지막 결과";
        }
        if (core?.ChapterIndex == 1 && _snapshot.Phase == ConstructionPhase.Ready)
        {
            return "경로 비교 · 북안 A / 강변 B";
        }
        if (core?.ChapterIndex == 2 && _snapshot.Phase == ConstructionPhase.Ready)
        {
            return "인수 경로 · 생활권 A / 의료원 B";
        }
        return _snapshot.Phase switch
        {
            ConstructionPhase.NodeDrafting => "변전소 계획",
            ConstructionPhase.LineDrafting => "선로 계획",
            ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding => "공사 진행",
            _ when core?.DecisionWindow?.Story is not null =>
                $"{core.Chapter.DisplayName} · {core.DecisionWindow.Story.Title}",
            _ when core is not null =>
                $"{core.Chapter.DisplayName} · 결정 {core.DecisionWindowIndex + 1}/{core.Chapter.DecisionWindows.Count}",
            _ => "첫 불빛 · 자유 배치",
        };
    }

    private CommercialSpeakerPresentation SpeakerPresentation()
    {
        string speaker = _presentedResult?.Story.Speaker ??
            (_showEpilogue ? _campaign.Epilogue.Speaker : null) ??
            _coreRun?.GetSnapshot().DecisionWindow?.Story?.Speaker ??
            _coreRun?.GetSnapshot().Chapter.Briefing.Speaker ??
            "윤서진";
        return speaker switch
        {
            "박지현" => new CommercialSpeakerPresentation(
                "park",
                "박지현 · 계통운영 책임자",
                Color.FromHtml("79b8d6")),
            "강민호" => new CommercialSpeakerPresentation(
                "kang",
                "강민호 · 발전운영 책임자",
                Color.FromHtml("e2b461")),
            "이도윤" => new CommercialSpeakerPresentation(
                "lee",
                "이도윤 · 정수운영 책임자",
                Color.FromHtml("71c8a8")),
            _ => new CommercialSpeakerPresentation(
                "yoon",
                "윤서진 · 청류시 전력운영센터장",
                Color.FromHtml("d39acb")),
        };
    }

    private ThermalDemandResult? SelectedDemand(ThermalIntervalResult interval) =>
        interval.Demands.FirstOrDefault(item => item.DemandId == _selectedDemandId) ??
        interval.Demands.FirstOrDefault();

    private int SelectedDemandOrdinal(ThermalIntervalResult interval)
    {
        int index = interval.Demands.ToList().FindIndex(item => item.DemandId == _selectedDemandId);
        return index < 0 ? (interval.Demands.Count == 0 ? 0 : 1) : index + 1;
    }

    private void RefreshSelectedDemand()
    {
        ThermalIntervalResult interval = _thermalSequence.Intervals[_thermalProjectionIndex];
        if (interval.Demands.All(item => item.DemandId != _selectedDemandId))
        {
            _selectedDemandId = interval.Demands.FirstOrDefault(item => item.Supplied)?.DemandId ??
                interval.Demands.FirstOrDefault()?.DemandId;
        }
    }

    private string DemandDisplayName(string intervalId, string demandId)
    {
        CommercialCoreLoadBundle? load = FindCoreLoad(intervalId, demandId);
        if (load is not null)
        {
            return load.DisplayName;
        }
        return BuildThermalRequest().Intervals
            .FirstOrDefault(item => item.IntervalId == intervalId)?.Demands
            .FirstOrDefault(item => item.DemandId == demandId)?.DisplayName ?? "선택 수요";
    }

    private string? DemandNodeId(string intervalId, string demandId)
    {
        CommercialCoreLoadBundle? load = FindCoreLoad(intervalId, demandId);
        if (load is not null)
        {
            return load.NodeId;
        }
        return BuildThermalRequest().Intervals
            .FirstOrDefault(item => item.IntervalId == intervalId)?.Demands
            .FirstOrDefault(item => item.DemandId == demandId)?.NodeId;
    }

    private CommercialCoreLoadBundle? FindCoreLoad(string intervalId, string demandId) =>
        _campaign.Chapters.SelectMany(chapter => chapter.OperatingPhases)
            .FirstOrDefault(phase => phase.PhaseId == intervalId)?.Loads
            .FirstOrDefault(load => load.LoadId == demandId);

    private IReadOnlyList<CommercialFacilityPresentation> FacilityPresentations(
        ThermalIntervalResult interval) => interval.Demands.Select(demand =>
        {
            string? nodeId = DemandNodeId(interval.IntervalId, demand.DemandId);
            CommercialFacilityState state = demand.Deferred
                ? CommercialFacilityState.Deferred
                : demand.Supplied
                    ? CommercialFacilityState.Supplied
                    : demand.Failure == ThermalSupplyFailure.UnavailableAsset
                        ? CommercialFacilityState.Unavailable
                        : CommercialFacilityState.Waiting;
            string status = state switch
            {
                CommercialFacilityState.Supplied => "공급됨 · 체크 아이콘",
                CommercialFacilityState.Deferred => "미룸 · 점선 아이콘",
                CommercialFacilityState.Unavailable => "사용불가 · 잠금 아이콘",
                _ => "미공급 · 빈 아이콘",
            };
            return nodeId is null
                ? null
                : new CommercialFacilityPresentation(nodeId, state, status);
        }).Where(item => item is not null).Select(item => item!).ToArray();

    private string HardSupplySummary(ThermalIntervalResult interval)
    {
        HashSet<string> hardIds = _campaign.Chapters.SelectMany(chapter => chapter.OperatingPhases)
            .Where(phase => phase.PhaseId == interval.IntervalId)
            .SelectMany(phase => phase.Loads)
            .Where(load => load.ObligationKind == CommercialCoreObligationKind.MustSupply)
            .Select(load => load.LoadId)
            .ToHashSet(StringComparer.Ordinal);
        int supplied = interval.Demands.Count(item => hardIds.Contains(item.DemandId) && item.Supplied);
        return hardIds.Count == 0
            ? "필수 공급 없음"
            : $"필수 공급 {supplied}/{hardIds.Count} {(supplied == hardIds.Count ? "✓" : "!")}";
    }

    private static string SupplyFailureText(ThermalSupplyFailure failure) => failure switch
    {
        ThermalSupplyFailure.Deferred => "도시 약속을 이번 국면에서 미룸",
        ThermalSupplyFailure.NoPath => "발전원에서 시설까지 이어진 경로 없음",
        ThermalSupplyFailure.UnavailableAsset => "경로의 설비가 정비·사용불가 상태",
        ThermalSupplyFailure.SourceCapacity => "발전원 남은 출력 부족",
        ThermalSupplyFailure.ContinuousPermission => "연속 열한계 안의 경로 없음",
        ThermalSupplyFailure.EmergencyLimit => "비상 열한계 안의 경로 없음",
        ThermalSupplyFailure.FutureSafetyDuty => "뒤의 안전 의무를 위해 여유 보존",
        _ => "공급 경로를 확정하지 못함",
    };

    private string InstructionText()
    {
        CommercialCoreSnapshot? core = _coreRun?.GetSnapshot();
        if (_presentedResult is not null)
        {
            return ResultPresentationText(_presentedResult);
        }
        if (core is not null && core.CampaignComplete)
        {
            return _showEpilogue
                ? $"{_campaign.Epilogue.Speaker}\n{_campaign.Epilogue.Body}\n" +
                  $"선택된 시작 상태 · {_completedChapterSelectionIndex + 1}/{_campaign.Chapters.Count} " +
                  _campaign.Chapters[_completedChapterSelectionIndex].DisplayName
                : ResultPresentationText(core.ChapterResults[^1]);
        }
        return _snapshot.Phase switch
        {
            ConstructionPhase.NodeDrafting => "점유영역과 견적을 확인한 뒤 발주하세요. 다른 위치를 클릭하면 계획 위치가 바뀝니다.",
            ConstructionPhase.LineDrafting when _snapshot.LineDraft?.EndNodeId is not null =>
                "전체 경로와 견적을 확인한 뒤 발주하세요.",
            ConstructionPhase.LineDrafting =>
                "빈 지형에는 전신주가 놓입니다. 작성 중 전신주는 드래그로 옮기고 마지막 점은 Backspace 또는 오른쪽 클릭으로 되돌립니다.",
            ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding =>
                "발주한 공사는 임의로 움직일 수 없습니다. 완공 시각까지 진행하세요.",
            _ when core?.DecisionWindow?.Story is CommercialStoryCard story =>
                $"{story.Title}\n{story.Body}\n목표 · {core.Chapter.Objective}" +
                ChapterGuidance(core),
            _ when core is not null =>
                $"{core.Chapter.Briefing.Title}\n{core.Chapter.Briefing.Body}\n목표 · {core.Chapter.Objective}" +
                ChapterGuidance(core),
            _ => "보이는 격자 없이 지형을 읽고, 발전 접속점과 생활권을 직접 이어 보세요.",
        };
    }

    private static string ChapterGuidance(CommercialCoreSnapshot core)
    {
        string guidance = core.ChapterIndex switch
        {
            0 => "\n조작 · 선로 잇기에서 접속점을 고르고 전신주를 놓습니다. Q/E는 후보 전환, 휠은 확대, Home은 전체 보기입니다.",
            1 => "\n확인 · 다음 국면 보기로 북안·강변 차단시험의 실제 공급 경로를 번갈아 확인하세요.",
            _ => string.Empty,
        };
        return core.Chapter.ResetThermalMemoryAtStart
            ? guidance + $"\n열 상태 · {core.Chapter.Briefing.Title}. " +
              "작성된 장간 시간경과로 모든 설비가 연속 운전 가능 상태로 복귀했습니다."
            : guidance;
    }

    private string ObligationsText(CommercialCoreSnapshot? core)
    {
        if (core is null)
        {
            return "열 연습 · 작성 수요를 확인하세요.";
        }
        if (core.CampaignComplete)
        {
            return CampaignRecordText(core);
        }
        string[] obligations = core.Chapter.OperatingPhases
            .SelectMany(item => item.Loads)
            .Select(item =>
            {
                string decision = item.ObligationKind == CommercialCoreObligationKind.CityPromise
                    ? core.PromiseDecision switch
                    {
                        PromiseDecision.Keep => " · 지킴 선택",
                        PromiseDecision.Defer => " · 미룸 선택",
                        _ => " · 선택 필요",
                    }
                    : string.Empty;
                return $"{ObligationText(item.ObligationKind)} · {item.DisplayName} " +
                       $"{FormatPower(item.DemandKw)}{decision}";
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return "현재 의무\n" + string.Join("\n", obligations);
    }

    private static string CampaignRecordText(CommercialCoreSnapshot core)
    {
        string[] records = core.ChapterResults.Select(result =>
        {
            int suppliedHard = result.DemandFacts.Count(item =>
                item.ObligationKind == CommercialCoreObligationKind.MustSupply && item.Supplied);
            int hard = result.DemandFacts.Count(item =>
                item.ObligationKind == CommercialCoreObligationKind.MustSupply);
            string promise = result.PromiseDecision switch
            {
                PromiseDecision.Keep => "약속 지킴",
                PromiseDecision.Defer => "약속 미룸",
                _ => "약속 없음",
            };
            return $"{result.ChapterDisplayName} · 안전 의무 {suppliedHard}/{hard} · {promise} · " +
                   $"비상 {result.EmergencyAssetIds.Count}곳 · 잔액 {FormatWon(result.RemainingCashUnit)}";
        }).ToArray();
        return "여덟 임무 실제 기록\n" + string.Join("\n", records);
    }

    private string ResultPresentationText(CommercialChapterResultRecord result)
    {
        CommercialResultDemandFact[] supplied = result.DemandFacts
            .Where(item => item.Supplied)
            .ToArray();
        string obligations = supplied.Length == 0
            ? "공급된 의무 없음"
            : string.Join(" · ", supplied.Select(item =>
                $"{ObligationText(item.ObligationKind)} {NodeFactDisplayName(item.FacilityNodeId)} 공급"));
        CommercialResultDemandFact? focus = supplied.FirstOrDefault(item =>
            item.ObligationKind == CommercialCoreObligationKind.CityPromise) ??
            supplied.FirstOrDefault();
        string path = focus is null
            ? "실제 공급 경로 없음"
            : $"실제 경로 · {string.Join(" → ", focus.PathNodeIds.Select(NodeFactDisplayName))}";
        string promise = result.PromiseDecision switch
        {
            PromiseDecision.Keep => "도시 약속 · 지킴",
            PromiseDecision.Defer => "도시 약속 · 미룸",
            _ => "도시 약속 · 해당 없음",
        };
        string emergency = result.EmergencyAssetIds.Count == 0
            ? "비상 운전 · 없음"
            : "비상 운전 · " + string.Join(", ", result.EmergencyAssetIds.Select(AssetFactDisplayName));
        string outage = result.ProtectiveOutageAssetIds.Count == 0
            ? "보호정지 · 없음"
            : "보호정지 · " + string.Join(", ",
                result.ProtectiveOutageAssetIds.Select(AssetFactDisplayName));
        return $"{result.Story.Title}\n{result.Story.Body}\n" +
               $"실제 의무 · {obligations}\n{promise}\n{path}\n{emergency}\n{outage}";
    }

    private string NodeFactDisplayName(string nodeId)
    {
        SpatialNodeDefinition? node = _snapshot.World.Nodes.FirstOrDefault(item =>
            item.NodeId == nodeId) ?? _commercialWorld.Spatial.Nodes.FirstOrDefault(item =>
            item.NodeId == nodeId);
        if (node is not null)
        {
            return node.DisplayName;
        }
        if (nodeId.StartsWith("PLAYER_POLE_", StringComparison.Ordinal))
        {
            string ordinal = nodeId["PLAYER_POLE_".Length..];
            return $"신설 전신주 {ordinal}";
        }
        if (nodeId.StartsWith("PLAYER_SUBSTATION_", StringComparison.Ordinal))
        {
            string ordinal = nodeId["PLAYER_SUBSTATION_".Length..];
            return $"신설 변전소 {ordinal}";
        }
        return "신설 접속점";
    }

    private string AssetFactDisplayName(string assetId)
    {
        SpatialNodeDefinition? node = _snapshot.World.Nodes.FirstOrDefault(item =>
            item.NodeId == assetId) ?? _commercialWorld.Spatial.Nodes.FirstOrDefault(item =>
            item.NodeId == assetId);
        if (node is not null)
        {
            return node.DisplayName;
        }
        SpatialEdgeDefinition? edge = _snapshot.World.Edges.FirstOrDefault(item =>
            item.EdgeId == assetId) ?? _commercialWorld.Spatial.Edges.FirstOrDefault(item =>
            item.EdgeId == assetId);
        return edge is null
            ? "신설 선로"
            : $"{NodeFactDisplayName(edge.FromNodeId)}–{NodeFactDisplayName(edge.ToNodeId)} 선로";
    }

    private static string ObligationText(CommercialCoreObligationKind kind) => kind switch
    {
        CommercialCoreObligationKind.MustSupply => "안전 의무",
        CommercialCoreObligationKind.CityPromise => "도시 약속",
        CommercialCoreObligationKind.OperatingRecord => "운영 기록",
        _ => "공급 기록",
    };

    private string ToolInstruction() => _tool switch
    {
        CommercialTool.Substation => "변전소 위치를 선택하세요.",
        CommercialTool.Line => "선로 경로를 선택하세요.",
        _ => "오른쪽에서 시작할 작업을 선택하세요.",
    };

    private string ToolName() => _tool switch
    {
        CommercialTool.Substation => "변전소 배치",
        CommercialTool.Line => "선로 계획",
        _ => "작업 선택",
    };

    private int? PointerFootprintRadius()
    {
        if (_tool == CommercialTool.Substation &&
            _snapshot.Phase is ConstructionPhase.Ready or ConstructionPhase.NodeDrafting)
        {
            return _snapshot.World.NodeClasses.Single(item => item.ClassId == SubstationClassId)
                .FootprintRadiusUnit;
        }
        if (_tool == CommercialTool.Line &&
            _snapshot.Phase == ConstructionPhase.LineDrafting &&
            _candidateNodeId is null &&
            _snapshot.LineDraft?.EndNodeId is null)
        {
            return _snapshot.World.NodeClasses.Single(item => item.ClassId == _poleClassId)
                .FootprintRadiusUnit;
        }
        return null;
    }

    private string ConnectionChangeText(string nodeId)
    {
        SpatialNodeDefinition node = _snapshot.World.Nodes.Single(item => item.NodeId == nodeId);
        SpatialNodeClassDefinition nodeClass = _snapshot.World.NodeClasses.Single(
            item => item.ClassId == node.ClassId);
        int current = _snapshot.World.Edges.Count(edge =>
            edge.FromNodeId == nodeId || edge.ToNodeId == nodeId);
        return $"연결 회선 {current}/{nodeClass.MaxConnections} → {current + 1}/{nodeClass.MaxConnections}";
    }

    private IReadOnlyList<string> CurrentRiskAreaIds()
    {
        if (_pointerRiskAreaIds.Count != 0)
        {
            return _pointerRiskAreaIds;
        }
        return _snapshot.NodeDraft?.RiskAreaIds ??
               _snapshot.LineDraft?.RiskAreaIds ??
               _snapshot.ActiveConstruction?.RiskAreaIds ??
               Array.Empty<string>();
    }

    private string RiskText(IReadOnlyList<string> riskAreaIds)
    {
        if (riskAreaIds.Count == 0)
        {
            return string.Empty;
        }
        Dictionary<string, string> names = _snapshot.World.RiskAreas.ToDictionary(
            item => item.RiskAreaId,
            item => item.DisplayName,
            StringComparer.Ordinal);
        string joined = string.Join(", ", riskAreaIds.Select(id =>
            names.TryGetValue(id, out string? name) ? name : "예고 위험구역"));
        return $"주의 · {joined}에 노출됩니다. 배치는 가능하지만 이후 사고의 영향을 받을 수 있습니다.";
    }

    private string AppendRisk(string message, IReadOnlyList<string> riskAreaIds)
    {
        string risk = RiskText(riskAreaIds);
        return string.IsNullOrEmpty(risk) ? message : $"{message} · {risk}";
    }

    private static string FormatDesignDistance(long? internalUnits)
    {
        if (internalUnits is null)
        {
            return "확인 불가";
        }
        decimal designUnits = internalUnits.Value / 100m;
        return $"설계 거리 {designUnits.ToString("0.##", CultureInfo.InvariantCulture)}";
    }

    private static string FormatWon(long cashUnit) =>
        cashUnit.ToString("N0", CultureInfo.GetCultureInfo("ko-KR")) + "원";

    private void RefreshThermalProjection()
    {
        if (_legacySession is not null)
        {
            CommercialWorldDefinition currentWorld = _commercialWorld with { Spatial = _snapshot.World };
            _thermalSequence = ThermalEvaluator.Preview(currentWorld, BuildThermalRequest());
        }
        else
        {
            CommercialDecisionPreview preview = _coreRun!.PreviewDecisionWindow();
            if (preview.PhaseResults.Count == 0 &&
                _coreRun.GetSnapshot().PromiseDecision is null &&
                _coreRun.GetSnapshot().Chapter.Promise is not null)
            {
                CommercialCoreRun presentationRun = CommercialCoreRun.Restore(
                    _commercialWorld,
                    _campaign,
                    _coreRun.GetCommands());
                _ = presentationRun.Apply(new CommercialCoreCommand(
                    CommercialCoreCommandKind.SetPromiseDecision,
                    PromiseDecision: PromiseDecision.Defer));
                preview = presentationRun.PreviewDecisionWindow();
            }
            IReadOnlyList<ThermalIntervalResult> intervals = preview.PhaseResults;
            if (intervals.Count == 0)
            {
                intervals = _coreRun.GetSnapshot().CommittedPhaseResults.TakeLast(1).ToArray();
            }
            _thermalSequence = new ThermalSequenceResult(
                intervals,
                intervals.Count == 0
                    ? Array.Empty<ThermalAssetMemory>()
                    : intervals[^1].NextAssetMemory);
        }
        if (_thermalSequence.Intervals.Count == 0)
        {
            throw new InvalidOperationException("표시할 상용 운영 국면이 없습니다.");
        }
        _thermalProjectionIndex = Math.Clamp(
            _thermalProjectionIndex,
            0,
            _thermalSequence.Intervals.Count - 1);
        if (!_thermalSequence.Intervals[_thermalProjectionIndex].Assets.Any(item =>
                item.AssetId == _selectedThermalAssetId))
        {
            _selectedThermalAssetId = _thermalSequence.Intervals[_thermalProjectionIndex].Assets
                .FirstOrDefault()?.AssetId ?? string.Empty;
        }
        RefreshSelectedDemand();
    }

    private static ThermalSequenceRequest BuildThermalRequest() => new(
    [
        new ThermalIntervalDefinition(
            "HOT_PROMISE",
            "더운 저녁 · 도시 약속",
            ThermalIntervalPolicy.ContinuousOnly,
            [
                new ThermalDemandDefinition(
                    "EAST_PROMISE",
                    "동부 생활권 저녁 약속",
                    "EAST_RESIDENTIAL_TERMINAL",
                    3900,
                    ThermalObligationKind.CityPromise,
                    true,
                    true,
                    false),
            ],
            Array.Empty<string>(),
            Array.Empty<ThermalLimitOverride>()),
        new ThermalIntervalDefinition(
            "PROTECTIVE_COOLING",
            "다음 경계 · 보호정지와 냉각",
            ThermalIntervalPolicy.ContinuousOnly,
            Array.Empty<ThermalDemandDefinition>(),
            Array.Empty<string>(),
            Array.Empty<ThermalLimitOverride>()),
        new ThermalIntervalDefinition(
            "RETURNED_SERVICE",
            "그다음 경계 · 자동 복귀",
            ThermalIntervalPolicy.ContinuousOnly,
            [
                new ThermalDemandDefinition(
                    "HOSPITAL_DUTY",
                    "의료원 안전 의무",
                    "HOSPITAL_TERMINAL",
                    1600,
                    ThermalObligationKind.SafetyDuty,
                    true,
                    false,
                    false),
            ],
            Array.Empty<string>(),
            Array.Empty<ThermalLimitOverride>()),
    ],
    Array.Empty<ThermalAssetMemory>());

    private string AssetDisplayName(string assetId)
    {
        SpatialNodeDefinition? node = _snapshot.World.Nodes.FirstOrDefault(item => item.NodeId == assetId);
        if (node is not null)
        {
            return node.DisplayName;
        }
        SpatialEdgeDefinition? edge = _snapshot.World.Edges.FirstOrDefault(item => item.EdgeId == assetId);
        return edge is null ? "선택 설비" : $"선로 {edge.EdgeId}";
    }

    private string ThermalIntervalName(string intervalId) => intervalId switch
    {
        "HOT_PROMISE" => "더운 저녁 · 도시 약속",
        "PROTECTIVE_COOLING" => "다음 경계 · 보호정지와 냉각",
        "RETURNED_SERVICE" => "그다음 경계 · 자동 복귀",
        _ => _campaign.Chapters.SelectMany(item => item.OperatingPhases)
            .FirstOrDefault(item => item.PhaseId == intervalId)?.DisplayName ?? "작성 국면",
    };

    private static string ThermalStateText(ThermalOperatingState state) => state switch
    {
        ThermalOperatingState.Continuous => "연속 운전 ●",
        ThermalOperatingState.Emergency => "비상 운전 ▲",
        ThermalOperatingState.ProtectiveOutage => "보호정지 ✕",
        ThermalOperatingState.OverLimit => "한계 초과 !",
        _ => "확인 불가",
    };

    private static string FormatPower(long powerKw) => powerKw >= 1000
        ? $"{powerKw / 1000m:0.0} MW"
        : $"{powerKw} kW";

    private static string ErrorText(ConstructionError? error) => error switch
    {
        null => string.Empty,
        ConstructionError.WrongPhase => "지금은 이 작업을 실행할 수 없습니다.",
        ConstructionError.UnknownNodeClass or
        ConstructionError.InvalidNodeClass or
        ConstructionError.UnknownLineClass or
        ConstructionError.UnknownPoleClass or
        ConstructionError.InvalidPoleClass => "공사 도구 설정을 불러오지 못했습니다.",
        ConstructionError.OutsideBounds => "설비 전체가 지도 안에 들어오도록 위치를 옮기세요.",
        ConstructionError.WaterFootprint => "강물 위에는 새 설비를 놓을 수 없습니다. 선로는 양쪽 육지 사이로 건널 수 있습니다.",
        ConstructionError.BuildingFootprint => "건물과 겹치거나 닿지 않도록 설비를 옮기세요.",
        ConstructionError.PositionOccupied => "다른 설비의 점유영역과 겹치거나 닿지 않도록 간격을 두세요.",
        ConstructionError.ExistingLineTouch => "기존 선로 위에는 새 설비를 놓을 수 없습니다.",
        ConstructionError.EndpointNotFound => "연결할 접속점을 찾지 못했습니다.",
        ConstructionError.EndpointNotCommissioned => "완공된 접속점만 선로에 연결할 수 있습니다.",
        ConstructionError.SameEndpoint => "선로의 시작과 끝은 서로 다른 접속점이어야 합니다.",
        ConstructionError.ConnectionLimit => "이 접속점은 더 많은 선로를 받을 수 없습니다.",
        ConstructionError.SpanTooLong => "현재 구간이 허용 경간을 넘습니다. 사이에 전신주를 한 곳 더 놓으세요.",
        ConstructionError.ZeroLengthSegment => "같은 위치에 연속해서 전신주를 놓을 수 없습니다.",
        ConstructionError.ThirdNodeTouch => "선로가 연결 대상이 아닌 다른 설비와 닿습니다.",
        ConstructionError.DuplicateSegment => "같은 두 접속점을 잇는 선로가 이미 있습니다.",
        ConstructionError.CollinearOverlap => "선로가 기존 구간과 같은 방향으로 포개집니다.",
        ConstructionError.BuildingCrossing => "선로가 건물을 가로지릅니다. 건물 밖으로 경로를 돌리세요.",
        ConstructionError.DraftIncomplete => "다른 접속점까지 경로를 이은 뒤 발주하세요.",
        ConstructionError.NothingToUndo => "되돌릴 전신주나 접속점이 없습니다.",
        ConstructionError.InvalidPointIndex => "옮길 전신주를 찾지 못했습니다.",
        ConstructionError.ArithmeticOverflow => "좌표 또는 견적이 계산 가능한 범위를 벗어났습니다.",
        ConstructionError.InvalidCompletion => "완공 결과가 공간 규칙을 만족하지 않아 공사를 진행하지 않았습니다.",
        _ => "공사 계획을 적용하지 못했습니다.",
    };

    private static string CoreErrorText(
        CommercialCoreError? error,
        ConstructionError? constructionError) => error switch
        {
            CommercialCoreError.ConstructionRejected => ErrorText(constructionError),
            CommercialCoreError.WrongPhase => "현재 결정 경계에서는 이 행동을 실행할 수 없습니다.",
            CommercialCoreError.InvalidCommand => "현재 화면 행동의 형식을 적용할 수 없습니다.",
            CommercialCoreError.InsufficientCash => "현재 현금으로 이 공사를 발주할 수 없습니다.",
            CommercialCoreError.PromiseDecisionRequired => "도시 약속을 지킬지 미룰지 먼저 선택하세요.",
            CommercialCoreError.DeadlineExceeded => "예상 완공 시각이 이 장의 공사 기한을 넘습니다.",
            CommercialCoreError.SafetyDutyFailed => "공개된 안전 의무를 공급하지 못해 승인을 보류했습니다.",
            CommercialCoreError.KeptPromiseFailed => "지키기로 한 도시 약속을 공급하지 못해 승인을 보류했습니다.",
            CommercialCoreError.CampaignComplete => "현재 공개된 네 임무를 모두 완료했습니다.",
            CommercialCoreError.NothingToRollback => "현재 장에서 되돌릴 최근 완공 공사가 없습니다.",
            _ => "현재 행동을 적용하지 못했습니다.",
        };

    private void InitializeCoreRun(bool loadSave)
    {
        _coreRun = new CommercialCoreRun(_commercialWorld, _campaign);
        string? stageFSmokeSavePath = System.Environment.GetEnvironmentVariable(
            StageFSmokeSaveEnvironment);
        if ((_options.CampaignCheckpointSmoke || _options.CampaignCompletionSmoke ||
                _options.CampaignCompletedResumeSmoke) &&
            string.IsNullOrWhiteSpace(stageFSmokeSavePath))
        {
            throw new InvalidOperationException(
                $"Stage-F smoke에는 {StageFSmokeSaveEnvironment} 절대 경로가 필요합니다.");
        }
        if (loadSave)
        {
            CommercialCampaignSaveLoadResult load = CommercialCampaignPersistenceStore.Load(_savePath!);
            if (load.Status == CommercialCoreDocumentLoadStatus.Loaded)
            {
                try
                {
                    _coreRun = CommercialCampaignSaveCodec.Restore(
                        load.Save!,
                        _commercialWorld,
                        _worldBytes,
                        _campaign,
                        _campaignBytes);
                    _lastStatus = "저장한 상용 캠페인을 fresh replay로 이어갑니다.";
                    _showEpilogue = _coreRun.GetSnapshot().CampaignComplete;
                    _hasContinuation = true;
                }
                catch (CommercialCorePersistenceException)
                {
                    _saveWritable = false;
                    _incompatibleSavePending = true;
                    _lastError = "현재 데이터와 맞지 않는 저장 기록을 보존했습니다. 새 저장으로 덮어쓰지 않습니다.";
                    _titleStatus = _lastError;
                }
            }
            else if (load.Status == CommercialCoreDocumentLoadStatus.Invalid)
            {
                _saveWritable = false;
                _incompatibleSavePending = true;
                _lastError = "이전 단계 또는 읽을 수 없는 저장 기록을 보존했습니다. 새 저장으로 덮어쓰지 않습니다.";
                _titleStatus = _lastError;
            }
            string previousPath = ProjectSettings.GlobalizePath("user://release-campaign-save-v2.json");
            if (load.Status == CommercialCoreDocumentLoadStatus.Missing && File.Exists(previousPath))
            {
                _lastStatus = "이전 내부 후보 저장은 호환되지 않아 원본 그대로 보존했습니다.";
            }
        }
        _snapshot = _coreRun.GetSnapshot().Construction;
    }

    private bool PersistCoreRun()
    {
        if (_coreRun is null || !_saveWritable || _savePath is null)
        {
            _lastError = "현재 진행을 저장할 수 없습니다. 저장 기록을 보존한 뒤 새 게임으로 복구하세요.";
            Render();
            return false;
        }
        try
        {
            CommercialCampaignSaveV3 save = CommercialCampaignSaveCodec.Create(
                _commercialWorld,
                _worldBytes,
                _campaign,
                _campaignBytes,
                _coreRun.GetCommands());
            CommercialCampaignPersistenceStore.Save(_savePath, save);
            _hasContinuation = true;
            return true;
        }
        catch (Exception exception) when (
            exception is CommercialCorePersistenceException or IOException or
            UnauthorizedAccessException)
        {
            _saveWritable = false;
            _lastError = "현재 진행을 저장하지 못했습니다. 화면 진행은 유지되지만 저장을 다시 덮어쓰지 않습니다.";
            Render();
            return false;
        }
    }

    private void ConfigurePersistencePaths()
    {
        string? storageDirectory = System.Environment.GetEnvironmentVariable(
            StorageDirectoryEnvironment);
        string? stageFSmokeSavePath = System.Environment.GetEnvironmentVariable(
            StageFSmokeSaveEnvironment);
        if (!string.IsNullOrWhiteSpace(storageDirectory))
        {
            storageDirectory = Path.GetFullPath(storageDirectory);
        }
        _savePath = !string.IsNullOrWhiteSpace(stageFSmokeSavePath)
            ? Path.GetFullPath(stageFSmokeSavePath)
            : !string.IsNullOrWhiteSpace(storageDirectory)
                ? Path.Combine(storageDirectory, CommercialCampaignPersistenceStore.SaveFileName)
                : ProjectSettings.GlobalizePath(
                    $"user://{CommercialCampaignPersistenceStore.SaveFileName}");
        _settingsPath = !string.IsNullOrWhiteSpace(storageDirectory)
            ? Path.Combine(storageDirectory, CommercialSettingsPersistenceStore.SettingsFileName)
            : ProjectSettings.GlobalizePath(
                $"user://{CommercialSettingsPersistenceStore.SettingsFileName}");
    }

    private void LoadSettings()
    {
        CommercialSettingsLoadResult load = CommercialSettingsPersistenceStore.Load(_settingsPath);
        _settings = load.Settings;
        if (load.Status == CommercialCoreDocumentLoadStatus.Invalid)
        {
            _titleStatus = load.MigratedFromV2
                ? "기존 화면·음량 값은 적용했지만 settings v3 승격 저장에 실패했습니다."
                : "설정을 읽지 못해 안전한 기본값을 적용했습니다.";
        }
        else if (load.MigratedFromV2)
        {
            _titleStatus = "기존 화면·음량 설정을 보존하고 움직임 줄이기 설정을 추가했습니다.";
        }
    }

    private void ContinueGame()
    {
        if (!_hasContinuation)
        {
            _shell.ShowTitle(false, "이어할 수 있는 유효한 저장 기록이 없습니다.");
            return;
        }
        if (_settings.ShowControlHelp)
        {
            _shell.ShowControlHelpBeforeGameplay();
        }
        else
        {
            _shell.HideShell();
        }
    }

    private void ShowPause()
    {
        if (_snapshot.Phase is ConstructionPhase.NodeDrafting or ConstructionPhase.LineDrafting)
        {
            CancelDraft();
            return;
        }
        CommercialCoreSnapshot? core = _coreRun?.GetSnapshot();
        string chapterName = core?.CampaignComplete == true
            ? _campaign.Chapters[_completedChapterSelectionIndex].DisplayName
            : core?.Chapter.DisplayName ?? "열 운영 연습";
        _shell.ShowPause(
            chapterName,
            _lastError,
            canRewindPreviousChapter: core is { CampaignComplete: false, ChapterIndex: > 0 });
    }

    private void SaveAndReturnToTitle()
    {
        if (!PersistCoreRun())
        {
            _shell.ShowPersistenceError(_lastError);
            return;
        }
        _shell.ShowTitle(true, "현재 진행을 원자적으로 저장했습니다.");
    }

    private void RewindPreviousChapter()
    {
        CommercialCoreSnapshot snapshot = _coreRun?.GetSnapshot()
            ?? throw new InvalidOperationException("캠페인 runner가 없습니다.");
        if (snapshot.CampaignComplete || snapshot.ChapterIndex <= 0)
        {
            _shell.ShowPersistenceError("이전 임무로 돌아갈 수 없습니다.");
            return;
        }
        int targetIndex = snapshot.ChapterIndex - 1;
        int commandCount = snapshot.ChapterStartCommandCounts[targetIndex];
        _coreRun = CommercialCoreRun.Restore(
            _commercialWorld,
            _campaign,
            _coreRun!.GetCommands().Take(commandCount).ToArray());
        _snapshot = _coreRun.GetSnapshot().Construction;
        _tool = CommercialTool.None;
        _presentedResult = null;
        _showEpilogue = false;
        _thermalProjectionIndex = 0;
        _lastStatus = $"{_coreRun.GetSnapshot().Chapter.DisplayName} 시작 상태로 돌아왔습니다.";
        _lastError = string.Empty;
        if (!PersistCoreRun())
        {
            _shell.ShowPersistenceError(_lastError);
            return;
        }
        RefreshThermalProjection();
        RefreshPointerPreview();
        Render();
        _shell.HideShell();
    }

    private void OnFullscreenChanged(bool fullscreen)
    {
        _settings = _settings with
        {
            WindowMode = fullscreen
                ? CommercialWindowMode.Fullscreen
                : CommercialWindowMode.Windowed,
        };
        ApplySettings(_settings);
        SaveSettings();
    }

    private void OnUiScalePercentChanged(int percent)
    {
        _settings = _settings with { UiScalePercent = percent };
        ApplySettings(_settings);
        SaveSettings();
    }

    private void OnControlHelpChanged(bool enabled)
    {
        _settings = _settings with { ShowControlHelp = enabled };
        ApplySettings(_settings);
        SaveSettings();
    }

    private void OnReduceMotionChanged(bool enabled)
    {
        _settings = _settings with { ReduceMotion = enabled };
        ApplySettings(_settings);
        SaveSettings();
        Render();
    }

    private void UpdateVolume(int? master = null, int? ambient = null, int? sfx = null)
    {
        _settings = _settings with
        {
            MasterVolumePercent = master ?? _settings.MasterVolumePercent,
            AmbientVolumePercent = ambient ?? _settings.AmbientVolumePercent,
            SfxVolumePercent = sfx ?? _settings.SfxVolumePercent,
        };
        ApplySettings(_settings);
        SaveSettings();
    }

    private void ApplySettings(CommercialSettingsV3 settings)
    {
        GetWindow().Mode = settings.WindowMode == CommercialWindowMode.Fullscreen
            ? Window.ModeEnum.Fullscreen
            : Window.ModeEnum.Windowed;
        GetWindow().ContentScaleFactor = 1f;
        ApplyRuntimeUiScale(this, settings.UiScalePercent / 100f);
        _timeline.SetUiScale(settings.UiScalePercent / 100f);
        // The help overlay remains the authoritative visible control guide. Keeping
        // a second footer under the event timeline weakens the reference HUD and
        // duplicates the same instructions, so the gameplay label stays semantic-only.
        _controlHelp.Visible = false;
        _audio.ApplyVolumes(
            settings.MasterVolumePercent,
            settings.AmbientVolumePercent,
            settings.SfxVolumePercent);
        _shell.SetSettings(
            settings.WindowMode == CommercialWindowMode.Fullscreen,
            settings.UiScalePercent,
            settings.ShowControlHelp,
            settings.MasterVolumePercent,
            settings.AmbientVolumePercent,
            settings.SfxVolumePercent,
            settings.ReduceMotion);
    }

    private void ApplyRuntimeUiScale(Node node, float scale)
    {
        if (node is Control control && control is Label or BaseButton)
        {
            if (!_baseFontSizes.TryGetValue(control, out int baseFontSize))
            {
                baseFontSize = control.GetThemeFontSize("font_size");
                _baseFontSizes.Add(control, baseFontSize);
            }
            if (baseFontSize > 0)
            {
                control.AddThemeFontSizeOverride(
                    "font_size",
                    Math.Max(1, (int)MathF.Round(baseFontSize * scale)));
            }
        }
        foreach (Node child in node.GetChildren())
        {
            ApplyRuntimeUiScale(child, scale);
        }
    }

    private void SaveSettings()
    {
        try
        {
            CommercialSettingsPersistenceStore.Save(_settingsPath, _settings);
            _shell.ShowSettingsMessage(string.Empty);
        }
        catch (Exception exception) when (
            exception is CommercialCorePersistenceException or IOException or
            UnauthorizedAccessException)
        {
            _shell.ShowSettingsMessage(
                "설정을 저장하지 못했습니다. 변경은 현재 실행에만 적용됩니다.");
        }
    }

    private void ShowFatal(string message)
    {
        if (_fatalLabel is null)
        {
            _fatalLabel = GetNodeOrNull<Label>("%FatalLabel") ?? new Label();
            if (_fatalLabel.GetParent() is null)
            {
                AddChild(_fatalLabel);
            }
        }
        _fatalLabel.Text = $"게임을 시작하지 못했습니다.\n{message}";
        _fatalLabel.Visible = true;
    }

    private static byte[] ReadEmbeddedBytes(string resourceName, string missingMessage)
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(missingMessage);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

}
