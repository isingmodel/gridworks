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
#if DEBUG
    private const string FixtureResource =
        "Gridworks.Game.EmbeddedData.commercial-free-placement-slice-v1.json";
#endif
    private const string SubstationClassId = "SMALL_SUBSTATION";
    private const string LargeSubstationClassId = "LARGE_SUBSTATION";
    private const string StandardLineClassId = "STANDARD_LINE";
    private const string StandardPoleClassId = "STANDARD_POLE";
    private const string LineClassId = "REINFORCED_LINE";
    private const string PoleClassId = "REINFORCED_POLE";
    private const string ProductSaveUri = "user://release-campaign-save-v3.json";
    private const string LegacySaveUri = "user://release-campaign-save-v2.json";
    private const string SettingsUri = "user://settings.json";
    private const string PlacementHelpText =
        "지도 확대: 마우스 휠 또는 + / −\n" +
        "지도 이동: 가운데 버튼 드래그 또는 Space+드래그\n" +
        "전체 보기: Home\n" +
        "키보드 커서: 방향키, 크게 이동: Shift+방향키\n" +
        "가까운 접속점 선택: Q / E, 확정: Enter\n" +
        "작성 중 전신주: 드래그로 이동, 마지막 점은 Backspace 또는 오른쪽 클릭으로 되돌리기\n\n" +
        "Esc는 작성 중인 계획을 먼저 취소합니다. 계획이 없으면 일시정지 메뉴를 엽니다.";
    private const string ProductHelpText = PlacementHelpText +
        "\n\n일반 선로는 짧고 저렴하지만 열여유가 작고, 보강 선로는 더 긴 별도 회랑에 적합합니다." +
        "\n변전소의 서비스 권역은 접속 가능 범위이며, 발전원에서 이어진 실제 경로가 있어야 공급됩니다." +
        "\n운영 국면을 바꾸며 의무 공급과 설비의 현재 사용·연속 한계를 확인하세요." +
        "\n승인한 행동은 한 슬롯에 자동 저장됩니다." +
        "\n캠페인을 마치면 완료한 장의 시작 상태를 골라 다시 설계할 수 있습니다.";

    private CommercialLaunchOptions _options = null!;
#if DEBUG
    private ConstructionSession _session = null!;
#endif
    private ConstructionSnapshot _snapshot = null!;
#if DEBUG
    private CommercialWorldDefinition? _thermalWorld = null;
    private ThermalSequenceEvaluation? _thermalEvaluation = null;
#endif
    private CommercialProductData? _productData;
    private CommercialCampaignRun? _coreRun;
    private CommercialCampaignSnapshot? _coreSnapshot;
    private CommercialCampaignSave? _loadedSave;
    private CommercialCampaignSaveLoadStatus _saveLoadStatus =
        CommercialCampaignSaveLoadStatus.Missing;
    private string? _productSavePath;
    private string? _settingsPath;
    private CommercialSettings _settings = CommercialSettings.Default;
    private string _settingsStatus = string.Empty;
    private bool _settingsFailure;
    private bool _canContinue;
    private string _persistenceStatus = string.Empty;
    private bool _persistenceFailurePending;
    private long _placementInputSequence;
#if DEBUG
    private int _placementOutcomePresentationCount;
#endif
    private int _thermalProjectionIndex;
    private string? _selectedThermalAssetId;
    private CommercialMapView _map = null!;
    private CommercialTaskPanel _panel = null!;
    private CommercialShell _shell = null!;
    private CommercialAudio _audio = null!;
    private Label _titleLabel = null!;
    private Label _zoomLabel = null!;
    private Label _summaryLabel = null!;
    private Label _controlHelpLabel = null!;
    private Button _helpButton = null!;
    private Label _fatalLabel = null!;

    private CommercialTool _tool;
    private string _activeNodeClassId = SubstationClassId;
    private string _activeLineClassId = LineClassId;
    private string _activePoleClassId = PoleClassId;
    private CoreMapPoint? _pointerPoint;
    private string? _candidateNodeId;
    private bool _pointerAccepted = true;
    private ConstructionError? _pointerError;
    private string _pointerMessage = string.Empty;
    private IReadOnlyList<string> _pointerRiskAreaIds = Array.Empty<string>();
    private string _lastStatus = "아직 발주한 공사가 없습니다.";
    private string _lastError = string.Empty;
    private string? _selectedApprovalChecklistId;
    private string? _selectedPhaseComparisonId;
    private CommercialNextProjectPlan? _nextProjectComparison;
    private string _nextProjectComparisonLabel = string.Empty;
    private readonly Queue<CommercialStoryPresentation> _storyQueue = new();
    private readonly Dictionary<Control, int> _baseFontSizes = [];
    private IReadOnlyList<CommercialStoryPresentation>? _pendingStoriesAfterPause;

    public override void _Ready()
    {
        try
        {
            _options = CommercialLaunchOptions.Parse(OS.GetCmdlineUserArgs());
#if DEBUG
            if (_options.PlacementSmoke)
            {
                InitializePlacementMode();
            }
            else if (_options.ThermalSmoke)
            {
                InitializeThermalSmokeMode();
            }
            else
#endif
            {
                InitializeProductMode();
            }
            BindScene();
            Render();
            if (IsProductMode)
            {
                ShowProductTitle();
            }
            if (!IsProductMode)
            {
                _map.CallDeferred(Control.MethodName.GrabFocus);
            }
#if DEBUG
            if (_options.PlacementSmoke)
            {
                CallDeferred(nameof(RunPlacementSmoke));
            }
            else if (_options.ThermalSmoke)
            {
                CallDeferred(nameof(RunThermalSmoke));
            }
            else if (_options.StageGLayoutSmoke)
            {
                CallDeferred(nameof(RunStageGLayoutSmoke));
            }
            else if (_options.CampaignSmokeLeg != CommercialCampaignSmokeLeg.None)
            {
                CallDeferred(nameof(RunCommercialCampaignSmoke));
            }
#endif
        }
        catch (Exception exception)
        {
            GD.PushError($"상용 자유 배치 화면을 시작하지 못했습니다: {exception}");
            ShowFatal(exception.Message);
#if DEBUG
            if (OS.GetCmdlineUserArgs().Any(argument =>
                argument is "--commercial-placement-smoke" or "--commercial-thermal-smoke" ||
                argument == "--commercial-stage-g-layout-smoke" ||
                argument.StartsWith("--commercial-campaign-smoke=", StringComparison.Ordinal)))
            {
                GetTree().Quit(1);
            }
#endif
        }
    }

#if DEBUG
    private void InitializePlacementMode()
    {
        GetWindow().Title = "Gridworks — 첫 불빛 자유 배치";
        SpatialWorldDefinition world = SpatialWorldLoader.Load(
            ReadEmbeddedResourceBytes(FixtureResource));
        _session = new ConstructionSession(world);
        _snapshot = _session.GetSnapshot();
    }
#endif

    private void InitializeProductMode()
    {
        _productData = CommercialProductResources.Load(Assembly.GetExecutingAssembly());
        _coreRun = new CommercialCampaignRun(_productData.Campaign, _productData.World);
        _coreSnapshot = _coreRun.GetSnapshot();
        _snapshot = _coreSnapshot.Construction;
        _productSavePath = ProjectSettings.GlobalizePath(ProductSaveUri);
        _settingsPath = ProjectSettings.GlobalizePath(SettingsUri);
#if DEBUG
        if (_options.CampaignSmokeLeg != CommercialCampaignSmokeLeg.None)
        {
            _productSavePath = _options.SmokeSavePath!;
        }
#endif
        RefreshContinueState();
        LoadProductSettings();
        GetWindow().Title = "Gridworks";
    }

    private bool IsProductMode => _coreRun is not null;

    private void RefreshContinueState()
    {
        if (_productData is null || _productSavePath is null)
        {
            return;
        }
        _loadedSave = null;
        _canContinue = false;
        CommercialCampaignSaveLoadResult load =
            CommercialCampaignSaveStore.Load(_productSavePath);
        _saveLoadStatus = load.Status;
        switch (load.Status)
        {
            case CommercialCampaignSaveLoadStatus.Missing:
                string legacyPath = ProjectSettings.GlobalizePath(LegacySaveUri);
                _persistenceStatus = File.Exists(legacyPath)
                    ? "이전 출시 후보의 저장 기록은 이 버전과 호환되지 않아 원본을 그대로 보존했습니다."
                    : "이어할 저장 기록이 없습니다.";
                return;
            case CommercialCampaignSaveLoadStatus.RecognizedStageD:
                _persistenceStatus =
                    "이전 개발판의 호환되지 않는 저장 기록을 확인했습니다. 새 게임을 시작하면 원본을 별도 백업으로 보존한 뒤 현재 캠페인 저장을 만듭니다.";
                return;
            case CommercialCampaignSaveLoadStatus.Invalid:
                _persistenceStatus =
                    "현재 저장 기록을 읽지 못했습니다. 원본은 덮어쓰지 않으며 새 게임은 메모리에서 시작할 수 있습니다.";
                return;
            case CommercialCampaignSaveLoadStatus.Loaded:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        try
        {
            _ = CommercialCampaignSaveCodec.Restore(
                _productData.Campaign,
                _productData.World,
                _productData.CampaignSha256,
                _productData.WorldSha256,
                load.Save!);
            _loadedSave = load.Save;
            _canContinue = true;
            _persistenceStatus = "저장된 진행 상황을 이어갈 수 있습니다.";
        }
        catch (CommercialCampaignPersistenceException)
        {
            _saveLoadStatus = CommercialCampaignSaveLoadStatus.Invalid;
            _persistenceStatus =
                "저장 기록이 현재 캠페인 데이터와 맞지 않아 이어하기를 사용할 수 없습니다. 원본은 그대로 보존했습니다.";
        }
    }

    private void ShowProductTitle(string? status = null)
    {
        string displayedStatus = status ?? _persistenceStatus;
        if (!string.IsNullOrWhiteSpace(_settingsStatus))
        {
            displayedStatus += "\n" + _settingsStatus;
        }
        _shell.ShowTitle(new CommercialTitlePresentation(
            _productData!.Campaign.DisplayName,
            _canContinue,
            displayedStatus));
    }

    private void LoadProductSettings()
    {
        if (_settingsPath is null)
        {
            return;
        }
        CommercialSettingsLoadResult load = CommercialSettingsStore.Load(_settingsPath);
        _settings = load.Settings;
        (_settingsStatus, _settingsFailure) = load.Status switch
        {
            CommercialSettingsLoadStatus.Missing =>
                ("설정 v3 기본값을 사용합니다.", false),
            CommercialSettingsLoadStatus.Loaded =>
                ("설정 v3를 불러왔습니다.", false),
            CommercialSettingsLoadStatus.MigratedFromVersion2 =>
                ("기존 설정 v2를 한 번만 settings v3로 승격했습니다. 움직임 줄이기는 기본으로 꺼져 있습니다.", false),
            CommercialSettingsLoadStatus.Invalid =>
                ("설정 파일을 읽지 못해 안전한 기본값을 사용합니다. 원본은 덮어쓰지 않았습니다.", true),
            CommercialSettingsLoadStatus.MigrationWriteFailed =>
                ("기존 설정은 읽었지만 settings v3 승격 파일을 쓰지 못했습니다. 원본은 덮어쓰지 않았습니다.", true),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private bool SaveProductRun() => SaveProductRun(_coreRun!);

    private bool SaveProductRun(CommercialCampaignRun run)
    {
        if (_productData is null || _productSavePath is null)
        {
            return false;
        }
        try
        {
            CommercialCampaignSave save = CommercialCampaignSaveCodec.Capture(
                _productData.Campaign,
                _productData.World,
                _productData.CampaignSha256,
                _productData.WorldSha256,
                run);
            CommercialCampaignSaveWriteResult write =
                CommercialCampaignSaveStore.SaveWithStageDBackup(_productSavePath, save);
            if (write.Status == CommercialCampaignSaveWriteStatus.Failed)
            {
                _lastError = SaveFailureText(write.Error);
                _persistenceStatus = _lastError;
                _persistenceFailurePending = true;
                GD.PushError($"상용 캠페인 저장 실패: {write.Error} · {write.ErrorMessage}");
                return false;
            }
            _loadedSave = save;
            _canContinue = true;
            _saveLoadStatus = CommercialCampaignSaveLoadStatus.Loaded;
            _persistenceStatus = write.Status ==
                CommercialCampaignSaveWriteStatus.SavedAfterStageDBackup
                ? "이전 개발판 저장을 별도 백업으로 보존하고 현재 진행 상황을 저장했습니다."
                : "현재 진행 상황을 저장했습니다.";
            _persistenceFailurePending = false;
            return true;
        }
        catch (Exception exception) when (
            exception is CommercialCampaignPersistenceException or IOException or
            UnauthorizedAccessException or ArgumentException)
        {
            _lastError =
                "게임을 저장하지 못했습니다. 기존 저장 원본은 그대로 두었습니다. 저장 공간과 파일 권한을 확인하세요.";
            _persistenceStatus = _lastError;
            _persistenceFailurePending = true;
            GD.PushError($"상용 캠페인 저장 실패: {exception.Message}");
            return false;
        }
    }

    private static string SaveFailureText(CommercialCampaignSaveWriteError? error) => error switch
    {
        CommercialCampaignSaveWriteError.InvalidExistingSave =>
            "현재 저장 기록을 읽지 못해 덮어쓰지 않았습니다. 새 게임은 메모리에서 계속할 수 있지만 저장은 만들지 못했습니다.",
        CommercialCampaignSaveWriteError.StageDBackupConflict or
        CommercialCampaignSaveWriteError.StageDBackupFailed =>
            "이전 개발판 저장의 별도 백업을 안전하게 만들지 못해 새 저장을 쓰지 않았습니다. 저장 공간과 파일 권한을 확인하세요.",
        CommercialCampaignSaveWriteError.ExistingSaveChanged =>
            "백업하는 동안 저장 기록이 바뀌어 새 저장을 쓰지 않았습니다. 제목 화면에서 다시 시도하세요.",
        CommercialCampaignSaveWriteError.CampaignWriteFailed =>
            "현재 진행 상황을 저장하지 못했습니다. 기존 저장과 이전 개발판 백업은 그대로 보존했습니다.",
        _ => "현재 진행 상황을 저장하지 못했습니다. 기존 저장 원본은 그대로 보존했습니다.",
    };

    private void ReplaceProductRun(CommercialCampaignRun run)
    {
        _coreRun = run;
        _coreSnapshot = run.GetSnapshot();
        _snapshot = _coreSnapshot.Construction;
        SetDefaultAvailableTools(_coreSnapshot);
        if (_snapshot.LineDraft is LineDraftSnapshot lineDraft)
        {
            _activeLineClassId = lineDraft.LineClassId;
            _activePoleClassId = lineDraft.PoleClassId;
        }
        _tool = CommercialTool.None;
        _pointerPoint = null;
        _candidateNodeId = null;
        _thermalProjectionIndex = 0;
        _selectedThermalAssetId = null;
        _selectedApprovalChecklistId = null;
        _selectedPhaseComparisonId = null;
        _nextProjectComparison = null;
        _nextProjectComparisonLabel = string.Empty;
        _storyQueue.Clear();
        _pendingStoriesAfterPause = null;
        _persistenceFailurePending = false;
        _lastError = string.Empty;
        _lastStatus = "운영안을 검토하고 필요한 공사를 계획하세요.";
        RefreshPointerPreview();
        Render();
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

        if (_shell.Surface != CommercialShellSurface.Hidden)
        {
            _shell.HandleEscape();
        }
        else if (_snapshot.Phase is ConstructionPhase.NodeDrafting or ConstructionPhase.LineDrafting)
        {
            CancelDraft();
        }
        else if (!_shell.HandleEscape())
        {
            _shell.ShowPause(new CommercialPausePresentation(
                IsProductMode ? _coreSnapshot!.Chapter.DisplayName : "현재 게임",
                IsProductMode
                    ? "승인한 행동은 자동 저장됩니다."
                    : "지도와 계획은 그대로 유지됩니다.",
                IsProductMode,
                IsProductMode));
        }
        GetViewport().SetInputAsHandled();
    }

    private void BindScene()
    {
        _map = GetNode<CommercialMapView>("%CommercialMapView");
        _panel = GetNode<CommercialTaskPanel>("%CommercialTaskPanel");
        _shell = GetNode<CommercialShell>("%CommercialShell");
        _audio = GetNode<CommercialAudio>("%CommercialAudio");
        _titleLabel = GetNode<Label>("%TitleLabel");
        _zoomLabel = GetNode<Label>("%ZoomLabel");
        _summaryLabel = GetNode<Label>("%SummaryLabel");
        _controlHelpLabel = GetNode<Label>("%ControlHelp");
        _helpButton = GetNode<Button>("%HelpButton");
        _fatalLabel = GetNode<Label>("%FatalLabel");

        _map.PointerChanged += OnPointerChanged;
        _map.PointRequested += OnPointRequested;
        _map.UndoRequested += () =>
            ExecuteConstruction(
                CommercialCoreCommand.UndoLinePoint(),
#if DEBUG
                () => _session.UndoLinePoint(),
#endif
                "마지막 선로 지점을 되돌렸습니다.");
        _map.DraftPointMoveRequested += MoveDraftPoint;
        _map.DraftPointDragPreviewChanged += OnDraftPointDragPreviewChanged;
        _map.ThermalAssetRequested += SelectThermalAsset;
        _map.CameraChanged += () =>
        {
            _zoomLabel.Text = $"지도 · {_map.ZoomLabel}";
        };
        _panel.ActionRequested += OnPanelAction;
        _panel.ProjectionDeltaRequested += ChangeThermalProjection;
        _panel.PromiseRequested += decision => ApplyCore(
            _coreRun!.Execute(CommercialCoreCommand.SetPromiseDecision(decision)),
            decision == CommercialPromiseDecision.Keep
                ? "도시 약속을 지키기로 선택했습니다."
                : "도시 약속을 미루기로 선택했습니다.");
        _panel.ProductActionRequested += OnProductAction;
        _panel.ChapterReplayRequested += ReplayCompletedChapter;
        _panel.ApprovalChecklistRequested += InspectApprovalChecklistItem;
        _panel.PhaseComparisonRequested += InspectPhaseComparisonRow;
        _shell.GameplayFocusRequested += OnGameplayFocusRequested;
        _shell.ActionRequested += OnShellAction;
        _shell.StoryAcknowledged += OnStoryAcknowledged;
        _shell.SettingsChanged += OnSettingsChanged;
        _shell.ConfirmationAccepted += OnConfirmationAccepted;
        _helpButton.Pressed += ShowHelp;
        _shell.SetHelpText(PlacementHelpText);
        if (IsProductMode)
        {
            _shell.SetSettings(SettingsPresentation(_settings));
            _shell.SetSettingsStatus(_settingsStatus, _settingsFailure);
            ApplyProductSettings(_settings);
        }
    }

    private void OnSettingsChanged(CommercialSettingsPresentation presentation)
    {
        if (!IsProductMode || _settingsPath is null)
        {
            return;
        }
        var candidate = new CommercialSettings(
            CommercialSettings.SupportedSchemaVersion,
            presentation.Fullscreen,
            presentation.UiScalePercent,
            presentation.MasterVolumePercent,
            presentation.AmbientVolumePercent,
            presentation.SfxVolumePercent,
            presentation.ReduceMotion);
        CommercialSettingsWriteResult write = CommercialSettingsStore.Save(
            _settingsPath,
            candidate);
        if (write.Status != CommercialSettingsWriteStatus.Saved)
        {
            _settingsStatus =
                "설정을 저장하지 못했습니다. 기존 settings.json은 그대로 두고 이전 설정을 유지합니다.";
            _settingsFailure = true;
            _shell.SetSettings(SettingsPresentation(_settings));
            _shell.SetSettingsStatus(_settingsStatus, true);
            _audio.PlayLive(CommercialAudioCue.Warning);
            return;
        }
        _settings = candidate;
        _settingsStatus = "설정 v3를 저장하고 적용했습니다.";
        _settingsFailure = false;
        ApplyProductSettings(_settings);
        _shell.SetSettingsStatus(_settingsStatus);
        Render();
    }

    private void ApplyProductSettings(CommercialSettings settings)
    {
        GetWindow().Mode = settings.Fullscreen
            ? Window.ModeEnum.Fullscreen
            : Window.ModeEnum.Windowed;
        GetWindow().ContentScaleFactor = 1f;
        ApplyProductUiScale(this, settings.UiScalePercent / 100f);
        _audio.ApplyVolumes(
            settings.MasterVolumePercent,
            settings.AmbientVolumePercent,
            settings.SfxVolumePercent);
    }

    private void ApplyProductUiScale(Node node, float scale)
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
            ApplyProductUiScale(child, scale);
        }
    }

    private static CommercialSettingsPresentation SettingsPresentation(
        CommercialSettings settings) => new(
        settings.Fullscreen,
        settings.UiScalePercent,
        settings.MasterVolumePercent,
        settings.AmbientVolumePercent,
        settings.SfxVolumePercent,
        settings.ReduceMotion);

    private void OnPointerChanged(CoreMapPoint? point, string? candidateNodeId)
    {
        _pointerPoint = point;
        _candidateNodeId = candidateNodeId;
        RefreshPointerPreview();
        Render();
    }

    private void OnPointRequested(CoreMapPoint point, string? candidateNodeId)
    {
        long inputId = checked(++_placementInputSequence);
        _pointerPoint = point;
        _candidateNodeId = candidateNodeId;
        switch (_tool)
        {
            case CommercialTool.Substation when
                _snapshot.Phase is ConstructionPhase.Ready or ConstructionPhase.NodeDrafting:
                ExecutePlacementInput(
                    inputId,
                    CommercialCoreCommand.SetNodeDraft(_activeNodeClassId, point),
#if DEBUG
                    () => _session.SetNodeDraft(_activeNodeClassId, point),
#endif
                    "변전소 계획 위치를 정했습니다.");
                return;

            case CommercialTool.Line when _snapshot.Phase == ConstructionPhase.Ready:
                if (candidateNodeId is null)
                {
                    RejectPlacementInput(
                        inputId,
                        "선로를 시작할 접속점 가까이에서 선택하세요.");
                    return;
                }
                ExecutePlacementInput(
                    inputId,
                    CommercialCoreCommand.StartLineDraft(
                        candidateNodeId,
                        _activeLineClassId,
                        _activePoleClassId),
#if DEBUG
                    () => _session.StartLineDraft(
                        candidateNodeId,
                        _activeLineClassId,
                        _activePoleClassId),
#endif
                    "첫 접속점을 정했습니다. 다음 전신주 위치를 이어서 선택하세요.");
                return;

            case CommercialTool.Line when
                _snapshot.Phase == ConstructionPhase.LineDrafting &&
                _snapshot.LineDraft?.EndNodeId is null:
                ExecutePlacementInput(
                    inputId,
                    candidateNodeId is null
                        ? CommercialCoreCommand.AddLinePoint(point)
                        : CommercialCoreCommand.FinishLineDraft(candidateNodeId),
#if DEBUG
                    () => candidateNodeId is null
                        ? _session.AddLinePoint(point)
                        : _session.FinishLineDraft(candidateNodeId),
#endif
                    candidateNodeId is null
                    ? "전신주 위치를 계획에 더했습니다."
                    : "마지막 접속점을 정했습니다. 견적을 확인하고 발주하세요.");
                return;

            default:
                RejectPlacementInput(
                    inputId,
                    "현재 작업에서 지도 위치를 더 선택할 수 없습니다.");
                return;
        }
    }

    private void OnPanelAction(CommercialPanelAction action)
    {
        switch (action)
        {
            case CommercialPanelAction.PlaceSubstation:
                SelectNodeTool(SubstationClassId);
                break;
            case CommercialPanelAction.PlaceLargeSubstation:
                SelectNodeTool(LargeSubstationClassId);
                break;
            case CommercialPanelAction.StartStandardLine:
                SelectLineTool(StandardLineClassId, StandardPoleClassId);
                break;
            case CommercialPanelAction.StartLine:
                SelectLineTool(LineClassId, PoleClassId);
                break;
            case CommercialPanelAction.UndoPoint:
                ExecuteConstruction(
                    CommercialCoreCommand.UndoLinePoint(),
#if DEBUG
                    () => _session.UndoLinePoint(),
#endif
                    "마지막 선로 지점을 되돌렸습니다.");
                break;
            case CommercialPanelAction.CancelDraft:
                CancelDraft();
                break;
            case CommercialPanelAction.Commission:
                OrderOrComplete();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private void OnProductAction(CommercialProductAction action)
    {
        if (!IsProductMode)
        {
            return;
        }
        switch (action)
        {
            case CommercialProductAction.ApproveWindow:
                {
                    CommercialCampaignSnapshot before = _coreSnapshot!;
                    CommercialCampaignCommandResult result = _coreRun!.Execute(
                        CommercialCoreCommand.ApproveDecisionWindow());
                    ApplyCore(
                        result,
                        "현재 운영안을 승인했습니다.",
                        ApprovalRejectionDiagnostic(result));
                    if (result.Accepted)
                    {
                        PlayApprovalCue(before, result.Snapshot);
                        PresentApprovalStories(before, result.Snapshot);
                    }
                    break;
                }
            case CommercialProductAction.StoreNextProjectComparison:
                StoreNextProjectComparison();
                break;
            case CommercialProductAction.ClearNextProjectComparison:
                _nextProjectComparison = null;
                _nextProjectComparisonLabel = string.Empty;
                _lastStatus = "다음 계획 비교 칸을 비웠습니다. 현재 초안과 창구 시간만 표시합니다.";
                _lastError = string.Empty;
                Render();
                break;
            case CommercialProductAction.RollbackRecentConstruction:
                RequestRecoveryConfirmation(CommercialRecoveryKind.RecentProject);
                break;
            case CommercialProductAction.RestartWindow:
                RequestRecoveryConfirmation(CommercialRecoveryKind.DecisionWindow);
                break;
            case CommercialProductAction.RestartChapter:
                RequestRecoveryConfirmation(CommercialRecoveryKind.Chapter);
                break;
            case CommercialProductAction.RewindPreviousChapter:
                RequestRecoveryConfirmation(CommercialRecoveryKind.PreviousChapter);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private static string? ApprovalRejectionDiagnostic(
        CommercialCampaignCommandResult result)
    {
        if (result.Accepted ||
            result.Error is not (
                CommercialCampaignRunError.SafetyDutyUnserved or
                CommercialCampaignRunError.KeptPromiseUnserved or
                CommercialCampaignRunError.FutureSafetyAtRisk) ||
            result.Snapshot.FirstBlockingDiagnostic is not CommercialSupplyDiagnostic diagnostic)
        {
            return null;
        }
        return FormatSupplyDiagnostic(diagnostic);
    }

    private void RequestRecoveryConfirmation(CommercialRecoveryKind kind)
    {
        CommercialRecoveryPreview preview = _coreRun!.PreviewRecovery(kind);
        if (!preview.Enabled)
        {
            _lastError = "현재 상태에서는 해당 복구 지점으로 돌아갈 수 없습니다.";
            Render();
            return;
        }
        _shell.ShowConfirmation(
            RecoveryConfirmationId(kind),
            RecoveryKindText(kind) + " 복구 확인",
            RecoveryConfirmationText(preview),
            "표시된 상태로 복구");
    }

    private void OnConfirmationAccepted(string confirmationId)
    {
        CommercialRecoveryKind? kind = confirmationId switch
        {
            "recovery:recent-project" => CommercialRecoveryKind.RecentProject,
            "recovery:decision-window" => CommercialRecoveryKind.DecisionWindow,
            "recovery:chapter" => CommercialRecoveryKind.Chapter,
            "recovery:previous-chapter" => CommercialRecoveryKind.PreviousChapter,
            _ => null,
        };
        if (kind is null)
        {
            return;
        }
        _shell.HideShell();
        ExecuteRecovery(kind.Value);
    }

    private void ExecuteRecovery(CommercialRecoveryKind kind)
    {
        switch (kind)
        {
            case CommercialRecoveryKind.RecentProject:
                ApplyJournalRewind(
                    _coreRun!.UndoRecentConstruction(),
                    "최근 완공 공사 직전 상태로 복구했습니다.",
                    null);
                break;
            case CommercialRecoveryKind.DecisionWindow:
                ApplyJournalRewind(
                    _coreRun!.RestartDecisionWindow(),
                    "이번 운영 단계를 처음부터 다시 시작했습니다.",
                    () => WindowStoryPresentations(_coreRun.GetSnapshot()));
                break;
            case CommercialRecoveryKind.Chapter:
                ApplyJournalRewind(
                    _coreRun!.RestartChapter(),
                    "현재 장을 처음부터 다시 시작했습니다.",
                    () => ChapterOpeningPresentations(_coreRun.GetSnapshot()));
                break;
            case CommercialRecoveryKind.PreviousChapter:
                ApplyJournalRewind(
                    _coreRun!.RewindToPreviousChapter(),
                    "이전 장의 시작 상태로 돌아갔습니다.",
                    () => ChapterOpeningPresentations(_coreRun.GetSnapshot()));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static string RecoveryConfirmationId(CommercialRecoveryKind kind) => kind switch
    {
        CommercialRecoveryKind.RecentProject => "recovery:recent-project",
        CommercialRecoveryKind.DecisionWindow => "recovery:decision-window",
        CommercialRecoveryKind.Chapter => "recovery:chapter",
        CommercialRecoveryKind.PreviousChapter => "recovery:previous-chapter",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private void ApplyJournalRewind(
        bool accepted,
        string success,
        Func<IReadOnlyList<CommercialStoryPresentation>>? stories)
    {
        if (!accepted)
        {
            _lastError = "현재 상태에서는 해당 복구 지점으로 돌아갈 수 없습니다.";
            Render();
            return;
        }
        RefreshCoreSnapshot(resetProjection: true);
        _lastStatus = success;
        _lastError = string.Empty;
        _tool = CommercialTool.None;
        _ = SaveProductRun();
        Render();
        if (stories is not null)
        {
            PresentStories(stories());
        }
    }

    private void ReplayCompletedChapter(string chapterId)
    {
        if (!IsProductMode || !_coreSnapshot!.CampaignComplete)
        {
            return;
        }
        CommercialCampaignChapterReplayOption? option = _coreSnapshot.ChapterReplayOptions
            .FirstOrDefault(item => string.Equals(
                item.ChapterId,
                chapterId,
                StringComparison.Ordinal));
        if (option is null)
        {
            _lastError = "선택한 장의 시작 기록을 찾을 수 없습니다.";
            Render();
            return;
        }
        ApplyJournalRewind(
            _coreRun!.ReplayCompletedChapterStart(option.ChapterId),
            $"{option.DisplayName} 시작 상태로 돌아갔습니다.",
            () => ChapterOpeningPresentations(_coreRun.GetSnapshot()));
    }

    private void OnShellAction(CommercialShellAction action)
    {
        if (!IsProductMode)
        {
            return;
        }
        switch (action)
        {
            case CommercialShellAction.NewGame:
                if (_canContinue && _shell.Surface != CommercialShellSurface.Confirm)
                {
                    _shell.ShowConfirmation(
                        CommercialShellAction.NewGame,
                        "새 게임 시작",
                        "현재 한 슬롯 저장을 새 진행으로 바꿉니다. 기존 진행은 되돌릴 수 없습니다.",
                        "새 게임 시작");
                    return;
                }
                var newRun = new CommercialCampaignRun(
                    _productData!.Campaign,
                    _productData.World);
                if (!SaveProductRun(newRun))
                {
                    string saveFailure = _lastError;
                    ReplaceProductRun(newRun);
                    _lastError = saveFailure;
                    Render();
                    _pendingStoriesAfterPause = ChapterOpeningPresentations(_coreSnapshot!);
                    _shell.ShowPause(new CommercialPausePresentation(
                        _coreSnapshot!.Chapter.DisplayName,
                        "새 게임은 메모리에서 시작됐지만 첫 저장을 만들지 못했습니다. " +
                        saveFailure,
                        true,
                        false));
                    return;
                }
                ReplaceProductRun(newRun);
                PresentStories(ChapterOpeningPresentations(_coreSnapshot!));
                break;
            case CommercialShellAction.Continue:
                ContinueProductRun();
                break;
            case CommercialShellAction.SaveAndQuit:
                if (SaveProductRun())
                {
                    ShowProductTitle("현재 진행 상황을 저장하고 제목 화면으로 돌아왔습니다.");
                }
                else
                {
                    _shell.ShowPause(new CommercialPausePresentation(
                        _coreSnapshot!.Chapter.DisplayName,
                        _lastError,
                        true,
                        true));
                    Render();
                }
                break;
            case CommercialShellAction.ReturnToTitle:
                ShowProductTitle();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private void ContinueProductRun()
    {
        if (!_canContinue || _loadedSave is null)
        {
            ShowProductTitle("이어할 수 있는 저장 기록이 없습니다. 새 게임을 시작하세요.");
            return;
        }
        try
        {
            CommercialCampaignRun restored = CommercialCampaignSaveCodec.Restore(
                _productData!.Campaign,
                _productData.World,
                _productData.CampaignSha256,
                _productData.WorldSha256,
                _loadedSave);
            ReplaceProductRun(restored);
            CommercialCampaignSnapshot snapshot = _coreSnapshot!;
            if (snapshot.CampaignComplete)
            {
                PresentStories(BuildEpiloguePresentations(snapshot));
            }
            else if (snapshot.CommandCount == snapshot.ChapterStartCommandCount)
            {
                PresentStories(ChapterOpeningPresentations(snapshot));
            }
            else if (snapshot.CommandCount == snapshot.WindowStartCommandCount)
            {
                PresentStories(WindowStoryPresentations(snapshot));
            }
            else
            {
                _shell.HideShell();
            }
        }
        catch (CommercialCampaignPersistenceException)
        {
            _loadedSave = null;
            _canContinue = false;
            _saveLoadStatus = CommercialCampaignSaveLoadStatus.Invalid;
            _persistenceStatus =
                "저장 기록을 복원하지 못해 이어하기를 사용할 수 없습니다. 원본은 그대로 보존했습니다.";
            ShowProductTitle();
        }
    }

    private void OnStoryAcknowledged()
    {
        if (_storyQueue.Count > 0)
        {
            _shell.ShowStory(_storyQueue.Dequeue());
            return;
        }
        _shell.HideShell();
    }

    private void OnGameplayFocusRequested()
    {
        if (_pendingStoriesAfterPause is { Count: > 0 } pending)
        {
            _pendingStoriesAfterPause = null;
            PresentStories(pending);
            return;
        }
        _map.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void PresentApprovalStories(
        CommercialCampaignSnapshot before,
        CommercialCampaignSnapshot after)
    {
        if (after.LastOutcome is CommercialCampaignChapterOutcome outcome &&
            !string.Equals(
                before.LastOutcome?.ChapterId,
                outcome.ChapterId,
                StringComparison.Ordinal))
        {
            var cards = new List<CommercialStoryPresentation>
            {
                BuildResultPresentation(outcome),
            };
            if (!after.CampaignComplete)
            {
                cards.AddRange(ChapterOpeningPresentations(after));
            }
            else
            {
                cards.AddRange(BuildEpiloguePresentations(after));
            }
            PresentStories(cards);
            return;
        }
        if (!string.Equals(
                before.CurrentWindow?.WindowId,
                after.CurrentWindow?.WindowId,
                StringComparison.Ordinal))
        {
            PresentStories(WindowStoryPresentations(after));
        }
    }

    private IReadOnlyList<CommercialStoryPresentation> ChapterOpeningPresentations(
        CommercialCampaignSnapshot snapshot)
    {
        var cards = new List<CommercialStoryPresentation>();
        if (snapshot.Chapter.TimeAdvanceBeforeChapterMinutes > 0)
        {
            string reset = snapshot.Chapter.ResetThermalStateBeforeChapter
                ? " 충분한 시간이 지나 이전 열 상태가 모두 해제됐습니다."
                : string.Empty;
            cards.Add(new CommercialStoryPresentation(
                new CommercialStoryCard(
                    "운영 기록",
                    "도시의 시간이 흘렀습니다",
                    $"{FormatElapsedDuration(snapshot.Chapter.TimeAdvanceBeforeChapterMinutes)}가 지나 " +
                    $"현재 시각은 {FormatCampaignMinute(snapshot.Minute)}입니다.{reset}"),
                false,
                "새 장 보기"));
        }
        cards.Add(new CommercialStoryPresentation(
            snapshot.Chapter.Briefing,
            false,
            "예고 확인"));
        cards.AddRange(WindowStoryPresentations(snapshot));
        return cards;
    }

    private static IReadOnlyList<CommercialStoryPresentation> WindowStoryPresentations(
        CommercialCampaignSnapshot snapshot)
    {
        var cards = new List<CommercialStoryPresentation>();
        if (snapshot.CurrentWindow?.Story is CommercialStoryCard windowStory)
        {
            cards.Add(new CommercialStoryPresentation(windowStory, false, "계속"));
        }
        return cards;
    }

    private void PresentStories(IReadOnlyList<CommercialStoryPresentation> stories)
    {
        _storyQueue.Clear();
        if (stories.Count == 0)
        {
            _shell.HideShell();
            return;
        }
        IReadOnlyList<CommercialStoryPresentation> presented = stories;
        if (_persistenceFailurePending)
        {
            _persistenceFailurePending = false;
            string exactReason = string.IsNullOrWhiteSpace(_lastError)
                ? "현재 진행 상황을 저장하지 못했습니다. 기존 저장 원본은 덮어쓰지 않았습니다."
                : _lastError;
            var warning = new CommercialStoryPresentation(
                new CommercialStoryCard(
                    "시스템",
                    "진행 상황을 저장하지 못했습니다",
                    "방금 선택한 결과는 현재 실행의 메모리에 반영됐지만 저장 파일에는 기록하지 못했습니다. " +
                    "기존 저장 원본은 덮어쓰지 않았습니다.\n\n" + exactReason),
                false,
                "결과 계속 보기",
                true);
            presented = new[] { warning }.Concat(stories).ToArray();
        }
        presented = presented.Select(AttachStoryPortrait).ToArray();
        for (int index = 1; index < presented.Count; index++)
        {
            _storyQueue.Enqueue(presented[index]);
        }
        _shell.ShowStory(presented[0]);
    }

    private static CommercialStoryPresentation AttachStoryPortrait(
        CommercialStoryPresentation presentation)
    {
        CommercialStoryPortraitPresentation? portrait = presentation.Card.Speaker switch
        {
            "운영센터장 윤서진" => new(
                "res://assets/commercial/portraits/yoon_seojin.png",
                "운영센터장 윤서진 초상"),
            "계통운영관 강민호" => new(
                "res://assets/commercial/portraits/kang_minho.png",
                "계통운영관 강민호 초상"),
            "의료원 시설책임자 박지현" => new(
                "res://assets/commercial/portraits/park_jihyeon.png",
                "의료원 시설책임자 박지현 초상"),
            "재난대응관 이도윤" => new(
                "res://assets/commercial/portraits/lee_doyoon.png",
                "재난대응관 이도윤 초상"),
            _ => null,
        };
        return presentation with { Portrait = portrait };
    }

    private CommercialStoryPresentation BuildResultPresentation(
        CommercialCampaignChapterOutcome outcome)
    {
        CommercialStoryCard authored = outcome.ResultCard;
        string facts = string.Join(" ", BuildOutcomeFacts(outcome));
        var card = authored with
        {
            Body = string.IsNullOrWhiteSpace(facts)
                ? authored.Body
                : authored.Body + "\n\n" + facts,
        };
        return new CommercialStoryPresentation(card, true, "계속");
    }

    private static IReadOnlyList<CommercialStoryPresentation> BuildEpiloguePresentations(
        CommercialCampaignSnapshot snapshot)
    {
        if (!snapshot.CampaignComplete || snapshot.Epilogue is null)
        {
            throw new InvalidOperationException(
                "완료한 캠페인의 에필로그 기록을 불러올 수 없습니다.");
        }
        CommercialCampaignEpiloguePresentation epilogue = snapshot.Epilogue;
        var cards = new List<CommercialStoryPresentation>
        {
            new(epilogue.CityReport, false, "운영 기록 보기", KindLabel: "에필로그"),
        };
        const int FactsPerCard = 2;
        for (int start = 0; start < epilogue.ChapterFacts.Count; start += FactsPerCard)
        {
            CommercialCampaignEpilogueChapterFact[] facts = epilogue.ChapterFacts
                .Skip(start)
                .Take(FactsPerCard)
                .ToArray();
            string title = string.Join(" · ", facts.Select(fact => fact.DisplayName));
            string body = string.Join("\n\n", facts.Select(fact =>
                $"{fact.DisplayName}\n" +
                string.Join("\n", fact.SummaryLines.Select(line => $"• {line}"))));
            cards.Add(new CommercialStoryPresentation(
                new CommercialStoryCard("운영 기록", title, body),
                false,
                "다음 기록",
                KindLabel: "에필로그"));
        }
        var decisionLines = epilogue.PromiseFacts
            .Select(fact => $"• {fact.Line}")
            .ToList();
        decisionLines.Add($"• 완료 시 남은 운영 자금 {FormatWon(epilogue.RemainingCashUnit)}");
        cards.Add(new CommercialStoryPresentation(
            new CommercialStoryCard(
                "운영 기록",
                "도시와의 약속",
                string.Join("\n", decisionLines)),
            false,
            "현장 기록 보기",
            KindLabel: "에필로그"));
        cards.Add(new CommercialStoryPresentation(
            epilogue.MedicalWitness,
            false,
            "마지막 기록 보기",
            KindLabel: "에필로그"));
        cards.Add(new CommercialStoryPresentation(
            epilogue.Closing,
            true,
            "완료 화면으로",
            KindLabel: "에필로그"));
        return cards;
    }

    private IReadOnlyList<string> BuildOutcomeFacts(CommercialCampaignChapterOutcome outcome)
        => outcome.RenderedFacts;

    private void PlayApprovalCue(
        CommercialCampaignSnapshot before,
        CommercialCampaignSnapshot after)
    {
        IReadOnlyList<ThermalIntervalEvaluation> evaluations;
        if (after.LastOutcome is CommercialCampaignChapterOutcome outcome &&
            !string.Equals(
                before.LastOutcome?.ChapterId,
                outcome.ChapterId,
                StringComparison.Ordinal))
        {
            evaluations = outcome.Phases.Select(item => item.Evaluation).ToArray();
        }
        else
        {
            evaluations = after.CommittedPhases
                .Skip(before.CommittedPhases.Count)
                .Select(item => item.Evaluation)
                .ToArray();
        }
        bool chapterCompleted = after.CompletedChapterOutcomes.Count >
            before.CompletedChapterOutcomes.Count;
        foreach (CommercialAudioCue cue in SelectApprovalCues(
                     evaluations,
                     chapterCompleted,
                     after.CampaignComplete,
                     after.CompletedChapterOutcomes.Count))
        {
            _audio.PlayLive(cue);
        }
    }

    private static IReadOnlyList<CommercialAudioCue> SelectApprovalCues(
        IReadOnlyList<ThermalIntervalEvaluation> evaluations,
        bool chapterCompleted,
        bool campaignComplete,
        int completedChapterCount)
    {
        bool protectiveStop = evaluations
            .SelectMany(item => item.Assets)
            .Any(item => item.State == ThermalOperatingState.ProtectiveOutage);
        bool protectiveStopScheduled = evaluations
            .SelectMany(item => item.Assets)
            .Any(item => item.State == ThermalOperatingState.Emergency &&
                         item.NextState == ThermalOperatingState.ProtectiveOutage);
        var cues = new List<CommercialAudioCue>();
        if (protectiveStop)
        {
            cues.Add(CommercialAudioCue.ProtectiveStop);
        }
        else if (protectiveStopScheduled)
        {
            cues.Add(CommercialAudioCue.Warning);
        }
        if (chapterCompleted)
        {
            cues.Add(CommercialAudioCue.Result);
            if (campaignComplete)
            {
                cues.Add(CommercialAudioCue.FinalRerouteMotif);
            }
            else if (completedChapterCount == 1)
            {
                cues.Add(CommercialAudioCue.FirstLightMotif);
            }
        }
        else if (!protectiveStop && !protectiveStopScheduled)
        {
            cues.Add(CommercialAudioCue.Energized);
        }
        return cues;
    }

    private void RefreshCoreSnapshot(bool resetProjection)
    {
        _coreSnapshot = _coreRun!.GetSnapshot();
        _snapshot = _coreSnapshot.Construction;
        if (!_coreSnapshot.AvailableNodeClassIds.Contains(
                _activeNodeClassId,
                StringComparer.Ordinal) ||
            !_coreSnapshot.AvailableLinePlans.Any(plan =>
                string.Equals(plan.LineClassId, _activeLineClassId, StringComparison.Ordinal) &&
                string.Equals(plan.PoleClassId, _activePoleClassId, StringComparison.Ordinal)))
        {
            SetDefaultAvailableTools(_coreSnapshot);
            _tool = CommercialTool.None;
        }
        if (resetProjection)
        {
            _thermalProjectionIndex = 0;
            _selectedThermalAssetId = null;
            _selectedApprovalChecklistId = null;
            _selectedPhaseComparisonId = null;
        }
        RefreshPointerPreview();
    }

    private void SetDefaultAvailableTools(CommercialCampaignSnapshot snapshot)
    {
        _activeNodeClassId = snapshot.AvailableNodeClassIds.Contains(
            SubstationClassId,
            StringComparer.Ordinal)
            ? SubstationClassId
            : snapshot.AvailableNodeClassIds.First();
        CommercialCampaignLinePlanDefinition linePlan = snapshot.AvailableLinePlans
            .FirstOrDefault(plan => string.Equals(
                plan.LineClassId,
                StandardLineClassId,
                StringComparison.Ordinal)) ?? snapshot.AvailableLinePlans.First();
        _activeLineClassId = linePlan.LineClassId;
        _activePoleClassId = linePlan.PoleClassId;
    }

    private void SelectLineTool(string lineClassId, string poleClassId)
    {
        _activeLineClassId = lineClassId;
        _activePoleClassId = poleClassId;
        SelectTool(CommercialTool.Line);
    }

    private void SelectNodeTool(string nodeClassId)
    {
        _activeNodeClassId = nodeClassId;
        SelectTool(CommercialTool.Substation);
    }

    private void SelectTool(CommercialTool tool)
    {
        if (_snapshot.Phase != ConstructionPhase.Ready)
        {
            RejectLocally("작성 중인 계획이나 공사를 먼저 마치세요.");
            return;
        }
        _tool = tool;
        _lastError = string.Empty;
        _lastStatus = tool == CommercialTool.Substation
            ? "지도에서 변전소 원 전체가 들어갈 위치를 선택하세요."
            : $"{ActiveLineDisplayName()}로 이을 첫 접속점을 선택하세요.";
        RefreshPointerPreview();
        Render();
        _map.GrabFocus();
    }

    private void CancelDraft()
    {
        if (IsProductMode)
        {
            CommercialCoreCommand command = _snapshot.Phase switch
            {
                ConstructionPhase.NodeDrafting => CommercialCoreCommand.CancelNodeDraft(),
                ConstructionPhase.LineDrafting => CommercialCoreCommand.CancelLineDraft(),
                _ => CommercialCoreCommand.CancelNodeDraft(),
            };
            CommercialCampaignCommandResult coreResult = _coreRun!.Execute(command);
            ApplyCore(coreResult, "작성 중인 계획을 취소했습니다.");
            if (coreResult.Accepted)
            {
                _tool = CommercialTool.None;
                RefreshPointerPreview();
                Render();
            }
            return;
        }
#if DEBUG
        ConstructionCommandResult result = _snapshot.Phase switch
        {
            ConstructionPhase.NodeDrafting => _session.CancelNodeDraft(),
            ConstructionPhase.LineDrafting => _session.CancelLineDraft(),
            _ => new ConstructionCommandResult(
                false,
                ConstructionError.WrongPhase,
                _snapshot),
        };
        Apply(result, "작성 중인 계획을 취소했습니다.");
        if (result.Accepted)
        {
            _tool = CommercialTool.None;
            RefreshPointerPreview();
            Render();
        }
#endif
    }

    private void OrderOrComplete()
    {
        if (IsProductMode)
        {
            CommercialCoreCommand command = _snapshot.Phase switch
            {
                ConstructionPhase.NodeDrafting => CommercialCoreCommand.OrderNode(),
                ConstructionPhase.LineDrafting => CommercialCoreCommand.OrderLine(),
                ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding =>
                    CommercialCoreCommand.AdvanceConstruction(),
                _ => CommercialCoreCommand.OrderNode(),
            };
            string productSuccess = _snapshot.Phase switch
            {
                ConstructionPhase.NodeDrafting => "변전소 공사를 발주했습니다.",
                ConstructionPhase.LineDrafting => "선로 공사를 발주했습니다.",
                ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding =>
                    "공사가 끝났습니다.",
                _ => string.Empty,
            };
            CommercialCampaignCommandResult coreResult = _coreRun!.Execute(command);
            ApplyCore(coreResult, productSuccess);
            if (coreResult.Accepted && _snapshot.Phase == ConstructionPhase.Ready)
            {
                _tool = CommercialTool.None;
                RefreshPointerPreview();
                Render();
            }
            return;
        }
#if DEBUG
        ConstructionCommandResult result;
        string success;
        switch (_snapshot.Phase)
        {
            case ConstructionPhase.NodeDrafting:
                result = _session.OrderNode();
                success = "변전소 공사를 발주했습니다. 완공 시각까지 진행할 수 있습니다.";
                break;
            case ConstructionPhase.LineDrafting:
                result = _session.OrderLine();
                success = "선로 공사를 발주했습니다. 완공 시각까지 진행할 수 있습니다.";
                break;
            case ConstructionPhase.NodeBuilding:
            case ConstructionPhase.LineBuilding:
                result = _session.AdvanceToConstructionCompletion();
                success = "공사가 끝났습니다. 완공 설비를 다음 계획에 바로 연결할 수 있습니다.";
                break;
            default:
                result = new ConstructionCommandResult(
                    false,
                    ConstructionError.WrongPhase,
                    _snapshot);
                success = string.Empty;
                break;
        }
        Apply(result, success);
        if (result.Accepted && _snapshot.Phase == ConstructionPhase.Ready)
        {
            _tool = CommercialTool.None;
            RefreshPointerPreview();
            Render();
        }
#endif
    }

#if DEBUG
    private void Apply(
        ConstructionCommandResult result,
        string success,
        string? rejectionOverride = null)
    {
        _snapshot = result.Snapshot;
        if (result.Accepted)
        {
            _lastStatus = success;
            _lastError = string.Empty;
        }
        else
        {
            _lastError = rejectionOverride ?? ErrorText(result.Error);
        }
        RefreshPointerPreview();
        Render();
    }
#endif

    private void ExecuteConstruction(
        CommercialCoreCommand productCommand,
#if DEBUG
        Func<ConstructionCommandResult> legacyCommand,
#endif
        string success)
    {
        if (IsProductMode)
        {
            ApplyCore(_coreRun!.Execute(productCommand), success);
            return;
        }
#if DEBUG
        Apply(legacyCommand(), success);
#endif
    }

    private void ExecutePlacementInput(
        long inputId,
        CommercialCoreCommand productCommand,
#if DEBUG
        Func<ConstructionCommandResult> legacyCommand,
#endif
        string success)
    {
        ConstructionSnapshot before = _snapshot;
        if (IsProductMode)
        {
            CommercialCampaignCommandResult result = _coreRun!.Execute(productCommand);
            VerifyRejectedPlacementIsAtomic(inputId, before, result.Accepted, result.Snapshot.Construction);
            if (result.Accepted)
            {
                ClearPlacementPointer();
            }
            string? rejection = null;
            if (!result.Accepted)
            {
                _lastStatus = "직전 배치 입력은 아래 한 결과로 끝났습니다.";
                rejection = PlacementRejectionMessage(
                    inputId,
                    CoreErrorText(result.Error, result.ConstructionError, result.ConnectionFailure));
            }
#if DEBUG
            _placementOutcomePresentationCount++;
#endif
            ApplyCore(
                result,
                PlacementInputMessage(inputId, success, result.Snapshot.Construction),
                rejection);
            return;
        }

#if DEBUG
        ConstructionCommandResult legacyResult = legacyCommand();
        VerifyRejectedPlacementIsAtomic(inputId, before, legacyResult.Accepted, legacyResult.Snapshot);
        if (legacyResult.Accepted)
        {
            ClearPlacementPointer();
        }
        string? legacyRejection = null;
        if (!legacyResult.Accepted)
        {
            _lastStatus = "직전 배치 입력은 아래 한 결과로 끝났습니다.";
            legacyRejection = PlacementRejectionMessage(
                inputId,
                ErrorText(legacyResult.Error));
        }
#if DEBUG
        _placementOutcomePresentationCount++;
#endif
        Apply(
            legacyResult,
            PlacementInputMessage(inputId, success, legacyResult.Snapshot),
            legacyRejection);
#endif
    }

    private void RejectPlacementInput(long inputId, string reason)
    {
        _lastStatus = "직전 배치 입력은 아래 한 결과로 끝났습니다.";
        _lastError = PlacementRejectionMessage(inputId, reason);
#if DEBUG
        _placementOutcomePresentationCount++;
#endif
        Render();
    }

    private static void VerifyRejectedPlacementIsAtomic(
        long inputId,
        ConstructionSnapshot before,
        bool accepted,
        ConstructionSnapshot after)
    {
        if (!accepted && !Equals(before, after))
        {
            throw new InvalidOperationException(
                $"배치 입력 #{inputId} 거부가 도시망·경로·초안을 변경했습니다.");
        }
    }

    private void ClearPlacementPointer()
    {
        _pointerPoint = null;
        _candidateNodeId = null;
        _pointerAccepted = true;
        _pointerError = null;
        _pointerRiskAreaIds = Array.Empty<string>();
        _pointerMessage = string.Empty;
    }

    private static string PlacementInputMessage(
        long inputId,
        string success,
        ConstructionSnapshot snapshot)
    {
        string count = snapshot.LineDraft is LineDraftSnapshot lineDraft
            ? $"현재 경로점 {1 + lineDraft.IntermediatePoints.Count + (lineDraft.EndNodeId is null ? 0 : 1)}곳"
            : snapshot.NodeDraft is not null
                ? "현재 배치점 1곳"
                : "현재 초안 반영 완료";
        return $"배치 입력 #{inputId} · 적용 · {success} {count}.";
    }

    private static string PlacementRejectionMessage(long inputId, string reason) =>
        $"배치 입력 #{inputId} · 거부 · {reason} 도시망·경로·초안은 바뀌지 않았습니다.";

    private void ApplyCore(
        CommercialCampaignCommandResult result,
        string success,
        string? rejectionOverride = null)
    {
        CommercialCampaignSnapshot? before = _coreSnapshot;
        _coreSnapshot = result.Snapshot;
        _snapshot = result.Snapshot.Construction;
        if (result.Accepted)
        {
            _lastStatus = success;
            _lastError = string.Empty;
            bool boundaryChanged = before is not null &&
                (before.ChapterIndex != result.Snapshot.ChapterIndex ||
                 !string.Equals(
                     before.CurrentWindow?.WindowId,
                     result.Snapshot.CurrentWindow?.WindowId,
                     StringComparison.Ordinal));
            if (boundaryChanged)
            {
                _thermalProjectionIndex = 0;
                _selectedThermalAssetId = null;
                _nextProjectComparison = null;
                _nextProjectComparisonLabel = string.Empty;
            }
            if (before is not null && before.ChapterIndex != result.Snapshot.ChapterIndex)
            {
                SetDefaultAvailableTools(result.Snapshot);
                _tool = CommercialTool.None;
            }
            if (before is not null &&
                before.Construction.Phase is ConstructionPhase.NodeDrafting or
                    ConstructionPhase.LineDrafting &&
                result.Snapshot.Construction.Phase is ConstructionPhase.NodeBuilding or
                    ConstructionPhase.LineBuilding)
            {
                _audio.PlayLive(CommercialAudioCue.ConstructionOrdered);
            }
            else if (before is not null &&
                     before.Construction.Phase is ConstructionPhase.NodeBuilding or
                         ConstructionPhase.LineBuilding &&
                     result.Snapshot.Construction.Phase == ConstructionPhase.Ready)
            {
                _audio.PlayLive(CommercialAudioCue.ConstructionCompleted);
            }
            _ = SaveProductRun();
        }
        else
        {
            _audio.PlayLive(CommercialAudioCue.Warning);
            _lastError = rejectionOverride ?? CoreErrorText(
                    result.Error,
                    result.ConstructionError,
                    result.ConnectionFailure);
            if (result.Snapshot.FirstBlockingDiagnostic is CommercialSupplyDiagnostic diagnostic)
            {
                int diagnosticProjection = ProjectionIndexForPhase(
                    result.Snapshot,
                    diagnostic.PhaseId);
                if (diagnosticProjection >= 0)
                {
                    _thermalProjectionIndex = diagnosticProjection;
                }
                _selectedThermalAssetId = diagnostic.LimitingAssetId;
                _selectedApprovalChecklistId = null;
                _selectedPhaseComparisonId = null;
            }
        }
        RefreshPointerPreview();
        Render();
    }

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

        ExecuteConstruction(
            CommercialCoreCommand.MoveLinePoint(drag.PointIndex, drag.Position),
#if DEBUG
            () => _session.MoveLinePoint(drag.PointIndex, drag.Position),
#endif
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
            NodePlacementPreview preview = PreviewNodePlacement(_activeNodeClassId, point);
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
                _activeLineClassId,
                _activePoleClassId);
            SetPreview(preview.Accepted, preview.Error, Array.Empty<string>(),
                preview.Accepted ? "연결 시작 가능 · 클릭하여 확정" : null);
            _pointerMessage = $"{CandidateSelectionText(_candidateNodeId)} · " +
                $"{_pointerMessage} · {ConnectionChangeText(_candidateNodeId)}";
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
                _pointerMessage = $"{CandidateSelectionText(candidate)} · " +
                    $"{_pointerMessage} · {ConnectionChangeText(candidate)}";
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

    private NodePlacementPreview PreviewNodePlacement(string classId, CoreMapPoint point)
    {
#if DEBUG
        if (!IsProductMode)
        {
            return _session.PreviewNodePlacement(classId, point);
        }
#endif
        return _coreRun!.PreviewNodePlacement(classId, point);
    }

    private LineStartPreview PreviewLineStart(
        string nodeId,
        string lineClassId,
        string poleClassId)
    {
#if DEBUG
        if (!IsProductMode)
        {
            return _session.PreviewLineStart(nodeId, lineClassId, poleClassId);
        }
#endif
        return _coreRun!.PreviewLineStart(nodeId, lineClassId, poleClassId);
    }

    private LinePointPreview PreviewLinePoint(CoreMapPoint point)
    {
#if DEBUG
        if (!IsProductMode)
        {
            return _session.PreviewLinePoint(point);
        }
#endif
        return _coreRun!.PreviewLinePoint(point);
    }

    private LinePointMovePreview PreviewMoveLinePoint(int pointIndex, CoreMapPoint point)
    {
#if DEBUG
        if (!IsProductMode)
        {
            return _session.PreviewMoveLinePoint(pointIndex, point);
        }
#endif
        return _coreRun!.PreviewMoveLinePoint(pointIndex, point);
    }

    private LineFinishPreview PreviewLineFinish(string nodeId)
    {
#if DEBUG
        if (!IsProductMode)
        {
            return _session.PreviewLineFinish(nodeId);
        }
#endif
        return _coreRun!.PreviewLineFinish(nodeId);
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
        if (IsProductMode)
        {
            RenderProductMode();
            return;
        }
#if DEBUG
        if (_thermalEvaluation is not null)
        {
            RenderThermalMode();
            return;
        }
        _map.SetPresentation(new CommercialMapPresentation(
            _snapshot,
            _pointerPoint,
            _pointerAccepted,
            _pointerMessage,
            ToolName(),
            PointerFootprintRadius(),
            _tool == CommercialTool.Line,
            true,
            PointerError: _pointerError,
            PointerRiskAreaIds: _pointerRiskAreaIds));
        _panel.SetModel(BuildPanelModel());
        _zoomLabel.Text = $"지도 · {_map.ZoomLabel}";
        int commissionedEdges = _snapshot.World.Edges.Count(edge => edge.Commissioned);
        _summaryLabel.Text = commissionedEdges == 0
            ? "무벌점 연습 · 발전소와 마을 사이에 첫 선로를 완성하세요."
            : $"자유 배치 연습 · 완공 선로 {commissionedEdges}구간 · 다른 경로도 계속 시험할 수 있습니다.";
#endif
    }

    private void RenderProductMode()
    {
        CommercialCampaignSnapshot snapshot = _coreSnapshot!;
        string displayName = snapshot.CampaignComplete
            ? snapshot.Epilogue?.DisplayName ?? snapshot.Chapter.DisplayName
            : snapshot.Chapter.DisplayName;
        _titleLabel.Text = $"GRIDWORKS · {displayName}";
        if (snapshot.Projections.Count > 0)
        {
            _thermalProjectionIndex = Math.Clamp(
                _thermalProjectionIndex,
                0,
                snapshot.Projections.Count - 1);
        }
        CommercialPhaseProjection? projection = snapshot.Projections.Count == 0
            ? null
            : snapshot.Projections[_thermalProjectionIndex];
        _audio.SetWeather(WeatherProfile(projection));
        if (projection is not null)
        {
            EnsureThermalSelection(projection.Evaluation);
        }
        string projectionLabel = projection is null
            ? "운영 결과"
            : $"{projection.Phase.DisplayName} · {_thermalProjectionIndex + 1}/{snapshot.Projections.Count}";
        CommercialThermalMapPresentation? thermal = projection is null
            ? null
            : new CommercialThermalMapPresentation(
                projectionLabel,
                projection.Evaluation.Assets,
                _selectedThermalAssetId,
                ThermalSelectionText(
                    projection.Evaluation,
                    projection.Phase.ThermalPolicy ==
                        CommercialPhaseThermalPolicy.ContinuousOnly,
                    projection.ProjectedWorld).Replace('\n', ' '),
                projection.Phase.ThermalPolicy ==
                    CommercialPhaseThermalPolicy.ContinuousOnly,
                projection.Phase.ActiveRiskAreaIds);
        bool constructionInput =
            _snapshot.Phase is ConstructionPhase.NodeDrafting or ConstructionPhase.LineDrafting ||
            (_snapshot.Phase == ConstructionPhase.Ready && _tool != CommercialTool.None);
        _map.SetPresentation(new CommercialMapPresentation(
            _snapshot,
            _pointerPoint,
            _pointerAccepted,
            string.IsNullOrWhiteSpace(_pointerMessage) ? "지도에서 설비를 선택하세요." : _pointerMessage,
            ToolName(),
            PointerFootprintRadius(),
            _tool == CommercialTool.Line,
            constructionInput,
            thermal,
            PointerServiceRadius(),
            DraftServiceRadius(),
            SelectedServiceArea(projection?.ProjectedWorld),
            CurrentMapHighlight(snapshot, projection),
            _pointerError,
            _pointerRiskAreaIds,
            _settings.ReduceMotion,
            BuildCityMapPresentation(snapshot, projection),
            projection?.ProjectedWorld));
        _panel.SetModel(BuildProductPanelModel(snapshot, projection));
        _zoomLabel.Text = $"지도 · {_map.ZoomLabel}";
        string progress = snapshot.CampaignComplete
            ? "캠페인 완료"
            : $"다음 확인 · {projection?.Phase.DisplayName ?? "운영 확인"}";
        _summaryLabel.Text =
            $"{displayName} · {progress} · " +
            $"운영 자금 {FormatWon(snapshot.CashUnit)}";
        _controlHelpLabel.Text =
            "휠/+/− 확대 · 가운데/Space+드래그 이동 · Home 전체 보기 · 클릭/방향키+Enter 설비 선택 · Esc 일시정지";
        _shell.SetHelpText(ProductHelpText);
        ApplyProductUiScale(this, _settings.UiScalePercent / 100f);
    }

    private static CommercialWeatherProfile WeatherProfile(
        CommercialPhaseProjection? projection)
    {
        if (projection is null)
        {
            return CommercialWeatherProfile.Clear;
        }
        bool riskActive = projection.Phase.ActiveRiskAreaIds.Count > 0;
        bool effectiveUnavailable = projection.EffectiveUnavailableNodeIds.Count > 0 ||
            projection.EffectiveUnavailableEdgeIds.Count > 0;
        if (riskActive && effectiveUnavailable)
        {
            return CommercialWeatherProfile.Storm;
        }
        if (riskActive)
        {
            return CommercialWeatherProfile.Rain;
        }
        bool heat = HasThermalHeatStress(projection);
        return heat ? CommercialWeatherProfile.Heat : CommercialWeatherProfile.Clear;
    }

    private static bool HasThermalHeatStress(CommercialPhaseProjection? projection) =>
        projection is not null &&
        (projection.Phase.ThermalLimitOverrides.Count > 0 ||
         projection.Evaluation.Assets.Any(asset =>
             asset.State is ThermalOperatingState.Emergency or
                 ThermalOperatingState.OverLimit));

    private CommercialCityMapPresentation BuildCityMapPresentation(
        CommercialCampaignSnapshot snapshot,
        CommercialPhaseProjection? projection)
    {
        CommercialWeatherProfile weather = WeatherProfile(projection);
        bool heatStress = HasThermalHeatStress(projection);
        SpatialWorldDefinition cityWorld = projection?.ProjectedWorld ??
            snapshot.Construction.World;
        IReadOnlyList<string> unavailableNodes = projection?.EffectiveUnavailableNodeIds ??
            Array.Empty<string>();
        IReadOnlyList<string> unavailableEdges = projection?.EffectiveUnavailableEdgeIds ??
            Array.Empty<string>();
        var facilities = new List<CommercialCityFacilityPresentation>();
        foreach (CommercialLoadDefinition load in _productData!.World.Loads)
        {
            ThermalLoadSupply? supply = projection?.Evaluation.Loads.FirstOrDefault(item =>
                string.Equals(item.LoadId, load.LoadId, StringComparison.Ordinal));
            bool pathEmergency = supply is not null && projection is not null &&
                projection.Evaluation.Assets.Any(asset =>
                    asset.State == ThermalOperatingState.Emergency &&
                    (supply.PathNodeIds.Contains(asset.AssetId, StringComparer.Ordinal) ||
                     supply.PathEdgeIds.Contains(asset.AssetId, StringComparer.Ordinal)));
            CommercialCityResponseState state = supply switch
            {
                null => CommercialCityResponseState.Normal,
                _ when supply.DeliveredKw < supply.DemandKw =>
                    CommercialCityResponseState.Unserved,
                _ when pathEmergency => CommercialCityResponseState.Stressed,
                _ => CommercialCityResponseState.Normal,
            };
            string status = supply switch
            {
                null => $"{load.DisplayName} · 현재 국면 수요 없음",
                _ when state == CommercialCityResponseState.Unserved =>
                    $"{load.DisplayName} · 공급 중단 · {FormatPower(supply.DeliveredKw)}/{FormatPower(supply.DemandKw)}",
                _ when state == CommercialCityResponseState.Stressed =>
                    $"{load.DisplayName} · 비상 경로 공급 · {FormatPower(supply.DeliveredKw)}",
                _ => $"{load.DisplayName} · 정상 공급 · {FormatPower(supply.DeliveredKw)}",
            };
            facilities.Add(new CommercialCityFacilityPresentation(
                load.NodeId,
                load.DisplayName,
                FacilityVisualKind(load.LoadId),
                state,
                status));
        }
        string weatherText = weather switch
        {
            CommercialWeatherProfile.Clear => "맑고 안정된 도시 조명",
            CommercialWeatherProfile.Heat => "열 한계가 낮아진 더운 날씨",
            CommercialWeatherProfile.Rain => "예고 위험구역에 비가 내리는 상태",
            CommercialWeatherProfile.Storm => "사용불가 설비와 위험구역이 함께 적용된 폭우 상태",
            _ => throw new ArgumentOutOfRangeException(nameof(weather)),
        };
        if (heatStress && weather != CommercialWeatherProfile.Heat)
        {
            weatherText += "이며 열 위험도 함께 표시";
        }
        Dictionary<string, SpatialNodeDefinition> worldNodes = cityWorld.Nodes
            .ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        string[] unavailableNames =
        [
            .. unavailableNodes.Select(nodeId => worldNodes.TryGetValue(
                    nodeId,
                    out SpatialNodeDefinition? node)
                ? node.DisplayName
                : nodeId),
            .. unavailableEdges.Select(edgeId =>
            {
                SpatialEdgeDefinition? edge = cityWorld.Edges.FirstOrDefault(
                    candidate => string.Equals(
                        candidate.EdgeId,
                        edgeId,
                        StringComparison.Ordinal));
                return edge is not null &&
                       worldNodes.TryGetValue(edge.FromNodeId, out SpatialNodeDefinition? from) &&
                       worldNodes.TryGetValue(edge.ToNodeId, out SpatialNodeDefinition? to)
                    ? $"{from.DisplayName}–{to.DisplayName} 선로"
                    : edgeId;
            }),
        ];
        string unavailableText = unavailableNames.Length == 0
            ? "사용불가 설비 없음"
            : $"사용불가 설비 · 접속 설비 {unavailableNodes.Count}곳, " +
              $"선로 {unavailableEdges.Count}구간 · {string.Join(", ", unavailableNames)}";
        return new CommercialCityMapPresentation(
            weather,
            snapshot.Minute,
            projection?.Phase.ActiveRiskAreaIds.Count > 0,
            heatStress,
            unavailableNodes,
            unavailableEdges,
            facilities,
            $"도시 표현: {weatherText}. {unavailableText}. " +
            string.Join(". ", facilities.Select(facility => facility.StatusText)));
    }

    private static CommercialFacilityVisualKind FacilityVisualKind(string loadId) => loadId switch
    {
        "HOSPITAL" => CommercialFacilityVisualKind.Medical,
        "WATERWORKS" => CommercialFacilityVisualKind.Water,
        "RIVER_FACTORY" => CommercialFacilityVisualKind.Industrial,
        "EAST_RESIDENTIAL" or "NORTH_RESIDENTIAL" =>
            CommercialFacilityVisualKind.Residential,
        _ => CommercialFacilityVisualKind.Residential,
    };

#if DEBUG
    private void RenderThermalMode()
    {
        ThermalIntervalEvaluation interval = CurrentThermalInterval();
        _titleLabel.Text = "GRIDWORKS · 열 운전 확인";
        EnsureThermalSelection(interval);
        string projection = ThermalProjectionLabel();
        string selection = ThermalSelectionText(interval);
        _map.SetPresentation(new CommercialMapPresentation(
            _snapshot,
            _pointerPoint,
            true,
            "클릭하여 설비 선택",
            "열 설비 선택",
            null,
            false,
            false,
            new CommercialThermalMapPresentation(
                projection,
                interval.Assets,
                _selectedThermalAssetId,
                selection.Replace('\n', ' '))));
        _panel.SetModel(BuildThermalPanelModel(interval));
        _zoomLabel.Text = $"지도 · {_map.ZoomLabel}";
        _summaryLabel.Text =
            $"작성된 고정 전력망 · {projection} · 선택 설비의 사용량과 다음 상태를 확인하세요.";
        _controlHelpLabel.Text =
            "휠/+/− 확대 · 가운데/Space+드래그 이동 · Home 전체 보기 · 클릭/방향키+Enter 설비 선택 · 오른쪽 패널 국면 전환";
        string thermalHelp =
            "지도 확대: 마우스 휠 또는 + / −\n" +
            "지도 이동: 가운데 버튼 드래그 또는 Space+드래그\n" +
            "전체 보기: Home\n" +
            "설비 선택: 마우스로 클릭하거나 방향키로 커서를 옮긴 뒤 Enter\n" +
            "열 운전 국면: 오른쪽 패널의 이전 국면 / 다음 국면\n\n" +
            "✓ 연속 운전은 실선, ! 비상 운전은 이중선과 사선, × 보호정지는 점선, !! 비상 한계 초과는 교차선으로 표시합니다.\n" +
            "색을 구분하기 어려워도 패턴·아이콘·오른쪽 상태 문장으로 같은 정보를 확인할 수 있습니다.";
        _shell.SetHelpText(thermalHelp);
        GetWindow().Title = "Gridworks — 열 운전 확인";
    }

    private CommercialTaskPanelModel BuildPanelModel()
    {
        ConstructionQuote? quote = _snapshot.Phase switch
        {
            ConstructionPhase.NodeDrafting => _session.PreviewNodeOrder(),
            ConstructionPhase.LineDrafting => _session.PreviewLineOrder(),
            _ => null,
        };
        bool draft = _snapshot.Phase is ConstructionPhase.NodeDrafting or ConstructionPhase.LineDrafting;
        bool building = _snapshot.Phase is ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding;
        string risk = RiskText(CurrentRiskAreaIds());
        string quoteText = quote?.Accepted == true
            ? $"예상 공사비 {FormatWon(quote.CostCashUnit!.Value)} · 공사 {quote.BuildMinutes}분" +
              (string.IsNullOrEmpty(risk) ? string.Empty : $"\n{risk}")
            : string.IsNullOrEmpty(risk)
                ? "이 연습에서는 공사비가 진행을 막지 않습니다."
                : risk;
        string error = !string.IsNullOrEmpty(_lastError)
            ? _lastError
            : _pointerAccepted
                ? string.Empty
                : _pointerMessage;
        return new CommercialTaskPanelModel(
            Heading: HeadingText(),
            Instruction: InstructionText(),
            Selection: string.IsNullOrWhiteSpace(_pointerMessage)
                ? "지도에서 작업 위치를 선택하세요."
                : _pointerMessage,
            Quote: quoteText,
            Status: $"공사 기준 시각 {_snapshot.Minute}분 · {_lastStatus}",
            Error: error,
            ToolStatus: $"현재 도구 · {ToolName()} · {ConstructionPhaseText()}",
            PlaceSubstation: new CommercialActionPresentation(
                _snapshot.Phase == ConstructionPhase.Ready,
                "변전소 놓기",
                "소형 배전 변전소의 점유영역을 지형 위에서 자유롭게 계획합니다."),
            StartLine: new CommercialActionPresentation(
                _snapshot.Phase == ConstructionPhase.Ready,
                "선로 잇기",
                "접속점에서 시작해 전신주 위치를 차례로 놓고 다른 접속점에 연결합니다."),
            UndoPoint: new CommercialActionPresentation(
                _snapshot.Phase == ConstructionPhase.LineDrafting,
                "마지막 점 되돌리기",
                "계획한 마지막 접속점 또는 전신주 한 곳을 되돌립니다."),
            CancelDraft: new CommercialActionPresentation(
                draft,
                "계획 취소",
                "작성 중인 계획 전체를 취소합니다."),
            Commission: new CommercialActionPresentation(
                building || quote?.Accepted == true,
                building ? "완공까지 진행" : "공사 발주",
                building
                    ? "표시된 완공 시각까지 진행해 설비를 사용할 수 있게 합니다."
                    : "현재 계획과 견적을 확정하고 공사를 시작합니다."));
    }
#endif

    private void StoreNextProjectComparison()
    {
        CommercialNextProjectPlan? plan = CaptureCurrentDraftAsNextPlan();
        if (plan is null)
        {
            _lastError = "끝 접속점까지 완성한 현재 초안만 다음 계획 비교에 보관할 수 있습니다.";
            Render();
            return;
        }
        _nextProjectComparison = plan;
        _nextProjectComparisonLabel = plan switch
        {
            CommercialNextNodeProjectPlan node => _snapshot.World.NodeClasses.Single(
                item => item.ClassId == node.NodeClassId).DisplayName,
            CommercialNextLineProjectPlan line => _snapshot.World.LineClasses.Single(
                item => item.ClassId == line.LineClassId).DisplayName,
            _ => throw new ArgumentOutOfRangeException(nameof(plan)),
        };
        _lastStatus = $"현재 초안 ‘{_nextProjectComparisonLabel}’을 저장하지 않는 다음 계획 비교 칸에 보관했습니다. " +
            "계획 취소 뒤 다른 현재 공사를 설계하면 두 공사의 완료 경계를 순서대로 비교합니다.";
        _lastError = string.Empty;
        Render();
    }

    private CommercialNextProjectPlan? CaptureCurrentDraftAsNextPlan() =>
        _snapshot switch
        {
            { Phase: ConstructionPhase.NodeDrafting, NodeDraft: NodeDraftSnapshot node } =>
                new CommercialNextNodeProjectPlan(node.NodeClassId, node.Position),
            {
                Phase: ConstructionPhase.LineDrafting,
                LineDraft: LineDraftSnapshot { EndNodeId: not null } line,
            } => new CommercialNextLineProjectPlan(
                line.StartNodeId,
                line.LineClassId,
                line.PoleClassId,
                line.IntermediatePoints,
                line.EndNodeId),
            _ => null,
        };

    private string FormatConstructionWindowForecast(
        CommercialConstructionWindowForecast forecast)
    {
        if (forecast.WindowId is null)
        {
            return "공사 창구 · 캠페인 완료 기록";
        }
        var lines = new List<string>
        {
            $"공사 창구 · 시작 {FormatCampaignMinute(forecast.WindowStartMinute)} · " +
            $"이미 사용 {FormatElapsedDuration(checked((int)forecast.AlreadySpentMinutes))}",
        };
        if (forecast.DeadlineMinute is long deadline)
        {
            lines.Add($"결정 경계 {FormatCampaignMinute(deadline)} · 현재 남은 여유 " +
                FormatSignedDuration(forecast.RemainingMinutesNow));
        }
        else
        {
            lines.Add("결정 경계 · 별도 공사시간 제한 없음");
        }
        foreach (CommercialConstructionForecastStep step in forecast.Steps)
        {
            string order = step.StepRole switch
            {
                CommercialConstructionForecastStepRole.CurrentDraft => "현재 공사",
                CommercialConstructionForecastStepRole.ExplicitNextPlan => "다음 계획",
                _ => throw new ArgumentOutOfRangeException(nameof(step.StepRole)),
            };
            string kind = step.Kind == ConstructionKind.Node ? "설비" : "선로";
            if (!step.Accepted)
            {
                lines.Add($"{step.SequenceNumber}. {order} {kind} · " +
                    CoreErrorText(step.Error, step.ConstructionError, null));
                continue;
            }
            string label = step.StepRole ==
                               CommercialConstructionForecastStepRole.ExplicitNextPlan &&
                           !string.IsNullOrWhiteSpace(_nextProjectComparisonLabel)
                ? $" {_nextProjectComparisonLabel}"
                : string.Empty;
            lines.Add($"{step.SequenceNumber}. {order}{label} · 공사 {step.BuildMinutes}분 · " +
                $"완료 {FormatCampaignMinute(step.CompletionMinute!.Value)} · " +
                $"경계 여유 {FormatSignedDuration(step.RemainingMinutesAfterCompletion)}");
        }
        if (_nextProjectComparison is null)
        {
            lines.Add("다음 계획 · 비어 있음. 완성 초안을 명시적으로 보관하면 현재 공사 뒤 한 건만 비교합니다.");
        }
        return string.Join("\n", lines);
    }

    private static string FormatSignedDuration(long? minutes) => minutes switch
    {
        null => "제한 없음",
        >= 0 => $"{minutes.Value:N0}분 남음",
        _ => $"{-minutes.Value:N0}분 초과",
    };

    private CommercialTaskPanelModel BuildProductPanelModel(
        CommercialCampaignSnapshot snapshot,
        CommercialPhaseProjection? projection)
    {
        CommercialCampaignProjectQuote? quote = _snapshot.Phase switch
        {
            ConstructionPhase.NodeDrafting => _coreRun!.PreviewNodeOrder(),
            ConstructionPhase.LineDrafting => _coreRun!.PreviewLineOrder(),
            _ => null,
        };
        bool draft = _snapshot.Phase is ConstructionPhase.NodeDrafting or
            ConstructionPhase.LineDrafting;
        bool building = _snapshot.Phase is ConstructionPhase.NodeBuilding or
            ConstructionPhase.LineBuilding;
        string quoteText = quote?.Accepted == true
            ? $"예상 공사비 {FormatWon(quote.CostCashUnit!.Value)} · " +
              $"완공 시각 {FormatCampaignMinute(quote.CompletionMinute!.Value)}"
            : quote is null
                ? "설비를 선택하면 비용과 완공 시각을 확인합니다."
                : CoreErrorText(quote.Error, quote.ConstructionError, null);
        string selection = projection is null
            ? "운영 결과를 확인하세요."
            : ThermalSelectionText(
                projection.Evaluation,
                projection.Phase.ThermalPolicy ==
                    CommercialPhaseThermalPolicy.ContinuousOnly,
                projection.ProjectedWorld);
        bool constructionInput = draft ||
            (_snapshot.Phase == ConstructionPhase.Ready && _tool != CommercialTool.None);
        if (constructionInput)
        {
            selection = string.IsNullOrWhiteSpace(_pointerMessage)
                ? ToolInstruction()
                : _pointerMessage;
        }
        var obligations = projection?.Phase.Loads.Select(load =>
        {
            CommercialLoadDefinition loadDefinition = _productData!.World.Loads.Single(
                item => item.LoadId == load.LoadId);
            ThermalLoadSupply? supply = projection.Evaluation.Loads.FirstOrDefault(
                item => item.LoadId == load.LoadId);
            string status = load.Obligation == CommercialObligationKind.CityPromise &&
                            snapshot.PromiseDecision == CommercialPromiseDecision.Defer
                ? "미룸"
                : supply?.DeliveredKw == load.DemandKw
                    ? "충족"
                    : "미충족";
            return new CommercialObligationPresentation(
                $"{ObligationText(load.Obligation)} · {loadDefinition.DisplayName} · " +
                FormatPower(load.DemandKw),
                status);
        }).ToList() ?? [];
        foreach (CommercialCampaignConnectionRequirement requirement in
                 snapshot.Chapter.ConnectionRequirements)
        {
            SpatialNodeDefinition node = _snapshot.World.Nodes.Single(
                item => item.NodeId == requirement.NodeId);
            int current = _snapshot.World.Edges.Count(edge =>
                edge.Commissioned &&
                (edge.FromNodeId == requirement.NodeId ||
                 edge.ToNodeId == requirement.NodeId));
            obligations.Add(new CommercialObligationPresentation(
                $"접속 회선 {current}/{requirement.MinimumConnections} · {node.DisplayName}",
                current >= requirement.MinimumConnections ? "충족" : "미충족"));
        }
        if (snapshot.CampaignComplete)
        {
            quoteText = $"완료 시 남은 운영 자금 " +
                FormatWon(snapshot.Epilogue!.RemainingCashUnit);
            selection = "에필로그에서 각 장의 실제 공급·열 상태·약속 결과를 확인했습니다.";
            obligations.Clear();
            obligations.Add(new CommercialObligationPresentation(
                "완료한 장의 시작 상태를 선택해 이후 도시망을 다시 설계할 수 있습니다.",
                "기록"));
        }
        CommercialCityPromiseDefinition? promise = snapshot.Chapter.CityPromise;
        CommercialPromisePresentation? promisePresentation =
            promise is null || snapshot.CampaignComplete
            ? null
            : new CommercialPromisePresentation(
                promise.DisplayName,
                PromiseText(snapshot.PromiseDecision),
                promise.KeepLabel,
                promise.DeferLabel,
                !snapshot.CampaignComplete &&
                snapshot.CommittedPhases.Count == 0 &&
                _snapshot.Phase == ConstructionPhase.Ready);
        string deadline = FormatConstructionWindowForecast(
            _coreRun!.PreviewConstructionWindowForecast(_nextProjectComparison));
        if (snapshot.CampaignComplete)
        {
            deadline = "완료 기록 · 선택한 장부터 다시 시작하면 이후 진행을 새로 저장합니다.";
        }
        string objective = snapshot.CampaignComplete
            ? snapshot.Epilogue!.DisplayName
            : snapshot.Chapter.Objective;
        IReadOnlyList<CommercialApprovalChecklistPresentation> approvalChecklist =
            BuildApprovalChecklistPresentations(snapshot);
        IReadOnlyList<CommercialPhaseComparisonPresentation> phaseComparisons =
            BuildPhaseComparisonPresentations(snapshot);
        string approvalHeading = ApprovalChecklistHeading(snapshot);
        IReadOnlyList<CommercialRecoveryPreview> recoveryBatch =
            _coreRun!.GetRecoveryPreviews();
        IReadOnlyDictionary<CommercialRecoveryKind, CommercialRecoveryPreview> recoveries =
            recoveryBatch.ToDictionary(item => item.Kind);
        string recoveryPreview = BuildRecoveryPreviewSummary(recoveryBatch);
        var product = new CommercialProductPanelPresentation(
            Objective: objective,
            Obligations: obligations,
            Deadline: deadline,
            ApprovalHeading: approvalHeading,
            ApprovalChecklist: approvalChecklist,
            PhaseComparisons: phaseComparisons,
            RecoveryPreview: recoveryPreview,
            StoreNextProjectComparison: new CommercialActionPresentation(
                !snapshot.CampaignComplete && CaptureCurrentDraftAsNextPlan() is not null,
                "초안을 다음 비교에 보관",
                "완성된 현재 초안의 정확한 기하를 저장하지 않는 비교 칸에 보관합니다. " +
                "보관 뒤 계획 취소와 다른 현재 초안 작성을 직접 해야 두 공사를 순서대로 비교할 수 있습니다. 공사 queue가 아닙니다.",
                !snapshot.CampaignComplete),
            ClearNextProjectComparison: new CommercialActionPresentation(
                !snapshot.CampaignComplete && _nextProjectComparison is not null,
                "다음 비교 지우기",
                "저장하지 않는 다음 계획 비교 칸만 비웁니다. 도시망과 현재 초안은 바뀌지 않습니다.",
                !snapshot.CampaignComplete),
            Promise: promisePresentation,
            ApproveWindow: new CommercialActionPresentation(
                snapshot.CanApprove,
                "운영안 승인",
                "현재 화면에 공개된 운영 국면을 순서대로 확정합니다.",
                !snapshot.CampaignComplete),
            RollbackRecentConstruction: RecoveryActionPresentation(
                recoveries[CommercialRecoveryKind.RecentProject],
                snapshot.CanRollbackRecentProject,
                "최근 공사 복구",
                !snapshot.CampaignComplete),
            RestartWindow: RecoveryActionPresentation(
                recoveries[CommercialRecoveryKind.DecisionWindow],
                snapshot.CanRestartWindow,
                "이번 운영 단계 다시 시작",
                !snapshot.CampaignComplete),
            RestartChapter: RecoveryActionPresentation(
                recoveries[CommercialRecoveryKind.Chapter],
                snapshot.CanRestartChapter,
                "현재 장 다시 시작",
                !snapshot.CampaignComplete),
            RewindPreviousChapter: RecoveryActionPresentation(
                recoveries[CommercialRecoveryKind.PreviousChapter],
                snapshot.CanRewindPreviousChapter,
                "이전 장부터 다시 설계",
                !snapshot.CampaignComplete),
            ChapterReplayOptions: snapshot.ChapterReplayOptions);
        string error = !string.IsNullOrEmpty(_lastError)
            ? _lastError
            : snapshot.ConnectionFailures.FirstOrDefault() is
                CommercialCampaignConnectionFailure connectionFailure
                ? ConnectionFailureText(connectionFailure)
            : snapshot.FirstBlockingDiagnostic is null
                ? string.Empty
                : FormatSupplyDiagnostic(snapshot.FirstBlockingDiagnostic);
        bool ready = !snapshot.CampaignComplete &&
            _snapshot.Phase == ConstructionPhase.Ready;
        CommercialActionPresentation smallSubstation = NodeToolPresentation(
            snapshot,
            SubstationClassId,
            ready);
        CommercialActionPresentation largeSubstation = NodeToolPresentation(
            snapshot,
            LargeSubstationClassId,
            ready);
        CommercialActionPresentation standardLine = LineToolPresentation(
            snapshot,
            StandardLineClassId,
            StandardPoleClassId,
            ready);
        CommercialActionPresentation reinforcedLine = LineToolPresentation(
            snapshot,
            LineClassId,
            PoleClassId,
            ready);
        return new CommercialTaskPanelModel(
            Heading: snapshot.CampaignComplete
                ? snapshot.Epilogue!.DisplayName
                : snapshot.Chapter.DisplayName,
            Instruction: objective,
            Selection: selection,
            Quote: quoteText,
            Status: $"현재 시각 {FormatCampaignMinute(snapshot.Minute)} · {_lastStatus}",
            Error: error,
            ToolStatus: $"현재 도구 · {ToolName()} · {ConstructionPhaseText()}",
            PlaceSubstation: smallSubstation,
            StartLine: reinforcedLine,
            UndoPoint: new CommercialActionPresentation(
                _snapshot.Phase == ConstructionPhase.LineDrafting,
                "마지막 점 되돌리기",
                "계획한 마지막 전신주를 되돌립니다."),
            CancelDraft: new CommercialActionPresentation(
                draft,
                "계획 취소",
                "작성 중인 계획을 취소합니다."),
            Commission: new CommercialActionPresentation(
                building || quote?.Accepted == true,
                building ? "완공까지 진행" : "공사 발주",
                building ? "표시된 완공 시각까지 진행합니다." : "현재 공사를 확정합니다."),
            Projection: projection is null
                ? null
                : new CommercialProjectionPresentation(
                    $"{projection.Phase.DisplayName} · {_thermalProjectionIndex + 1}/{snapshot.Projections.Count}",
                    _thermalProjectionIndex > 0,
                    _thermalProjectionIndex + 1 < snapshot.Projections.Count),
            ShowConstructionActions: !snapshot.CampaignComplete,
            Product: product,
            StandardLine: standardLine,
            LargeSubstation: largeSubstation);
    }

#if DEBUG
    private CommercialTaskPanelModel BuildThermalPanelModel(
        ThermalIntervalEvaluation interval)
    {
        EnsureThermalSelection(interval);
        ThermalAssetUsage? usage = interval.Assets.FirstOrDefault(item =>
            string.Equals(item.AssetId, _selectedThermalAssetId, StringComparison.Ordinal));
        string selection = usage is null
            ? "지도에서 선로나 전신주 접속부, 변전소를 선택하세요."
            : ThermalAssetName(usage);
        string limits = usage is null
            ? "선택한 설비의 사용량과 두 운전 한계를 여기에 표시합니다."
            : $"현재 사용 {FormatPower(usage.UsedKw)}\n" +
              $"연속 한계 {FormatPower(usage.ContinuousKw)} · " +
              $"비상 한계 {FormatPower(usage.EmergencyKw)}";
        string state = usage is null
            ? "현재 상태와 다음 상태를 함께 확인할 수 있습니다."
            : $"현재 상태 · {ThermalStateText(usage.State)}\n" +
              $"다음 상태 · {ThermalStateText(usage.NextState)}";
        var hidden = new CommercialActionPresentation(false, string.Empty, string.Empty);
        int count = _thermalEvaluation!.Intervals.Count;
        return new CommercialTaskPanelModel(
            Heading: "열 운전 확인",
            Instruction: "작성된 고정 전력망의 계산 결과입니다. 국면을 바꾸고 설비를 선택해 열 한계와 다음 상태를 비교하세요.",
            Selection: selection,
            Quote: limits,
            Status: state,
            Error: string.Empty,
            ToolStatus: $"현재 도구 · 열 설비 선택 · {ThermalProjectionLabel()}",
            PlaceSubstation: hidden,
            StartLine: hidden,
            UndoPoint: hidden,
            CancelDraft: hidden,
            Commission: hidden,
            Projection: new CommercialProjectionPresentation(
                ThermalProjectionLabel(),
                _thermalProjectionIndex > 0,
                _thermalProjectionIndex + 1 < count),
            ShowConstructionActions: false);
    }
#endif

    private IReadOnlyList<CommercialApprovalChecklistPresentation>
        BuildApprovalChecklistPresentations(CommercialCampaignSnapshot snapshot) =>
        snapshot.ApprovalChecklist.Items.Select(item =>
            new CommercialApprovalChecklistPresentation(
                item.ItemId,
                item.Passed,
                item.Label,
                ApprovalChecklistDescription(item),
                !item.Passed || item.PhaseId is not null || item.NodeId is not null ||
                item.LimitingAssetId is not null || item.PathNodeIds.Count > 0 ||
                item.PathEdgeIds.Count > 0)).ToArray();

    private string ApprovalChecklistHeading(CommercialCampaignSnapshot snapshot)
    {
        CommercialApprovalChecklist checklist = snapshot.ApprovalChecklist;
        if (snapshot.CampaignComplete || checklist.Items.Count == 0)
        {
            return "승인 조건 · 캠페인 완료";
        }
        int selectedPhase = snapshot.Projections.Count == 0
            ? checklist.FirstPhaseNumber
            : Math.Clamp(
                checklist.FirstPhaseNumber + _thermalProjectionIndex,
                1,
                checklist.PhaseCount);
        return $"승인 조건 · 국면 {selectedPhase}/{checklist.PhaseCount} · " +
            $"남은 blocker {checklist.RemainingBlockerCount}개";
    }

    private string ApprovalChecklistDescription(CommercialApprovalChecklistItem item)
    {
        if (item.FailureDiagnostic is CommercialSupplyDiagnostic diagnostic)
        {
            return FormatSupplyDiagnostic(diagnostic);
        }
        var facts = new List<string>
        {
            item.Passed ? "통과" : "미통과",
            item.Label,
        };
        if (item.PhaseDisplayName is string phase)
        {
            facts.Add($"국면 {item.PhaseNumber}/{item.PhaseCount} {phase}");
        }
        if (item.Required is long required && item.Current is long current)
        {
            facts.Add($"필요 {required:N0} · 현재 {current:N0} · 부족 {item.Shortfall ?? 0:N0}");
        }
        facts.Add(ApprovalNextAction(item.Kind));
        return string.Join(" · ", facts);
    }

    private static string ApprovalNextAction(CommercialApprovalGateKind kind) => kind switch
    {
        CommercialApprovalGateKind.CommandCapacity =>
            "다음 행동: 복구 미리보기를 확인하고 최근 공사나 현재 운영 단계로 돌아가 명령 기록을 줄이세요.",
        CommercialApprovalGateKind.ConstructionReady =>
            "다음 행동: 현재 초안을 발주·완공하거나 계획을 취소하세요.",
        CommercialApprovalGateKind.PromiseDecision =>
            "다음 행동: 도시 약속을 지킬지 미룰지 선택하세요.",
        CommercialApprovalGateKind.ConnectionRequirement =>
            "다음 행동: 표시된 설비의 필요한 접속 회선을 완성하세요.",
        CommercialApprovalGateKind.SafetyDemand or
        CommercialApprovalGateKind.KeptPromiseDemand or
        CommercialApprovalGateKind.FutureSafety =>
            "다음 행동: 표시된 수요 경로와 첫 제한 설비를 확인해 망을 보강하세요.",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private IReadOnlyList<CommercialPhaseComparisonPresentation>
        BuildPhaseComparisonPresentations(CommercialCampaignSnapshot snapshot) =>
        snapshot.PhaseComparisonRows.Select(row =>
        {
            string supply = row.Applicability switch
            {
                CommercialPhaseComparisonApplicability.AwaitingPromiseDecision => "약속 선택 전",
                CommercialPhaseComparisonApplicability.DeferredByPromise => "미룸",
                _ => $"{row.DeliveredKw:N0}/{row.DemandKw:N0} kW",
            };
            string source = row.SourceDisplayName ?? "—";
            string margin = row.MinimumRemainingKw is long remaining
                ? $"{remaining:N0} kW"
                : "—";
            string state = row.CurrentPathState is ThermalOperatingState current
                ? $"{ThermalStateText(current)}→" +
                  (row.NextPathState is ThermalOperatingState next
                      ? ThermalStateText(next)
                      : "—")
                : "—";
            string note = row.PhaseStoryTitle is null
                ? string.Empty
                : $" · 안내 {row.PhaseStoryTitle}";
            string cells = $"{row.LoadDisplayName} | {row.PhaseNumber}/{row.PhaseCount} " +
                $"{row.PhaseDisplayName} | {source} | {supply} | {margin} | {state}{note}";
            string description =
                $"수요 {row.LoadDisplayName}. 국면 {row.PhaseNumber}/{row.PhaseCount} " +
                $"{row.PhaseDisplayName}. 공급원 {source}. 공급 {supply}. 최소 여유 {margin}. " +
                $"현재와 다음 상태 {state}." +
                (row.PhaseStoryTitle is null
                    ? string.Empty
                    : $" 국면 안내 {row.PhaseStoryTitle}. {row.PhaseStoryBody}") +
                (row.FailureDiagnostic is null
                    ? string.Empty
                    : " " + FormatSupplyDiagnostic(row.FailureDiagnostic));
            return new CommercialPhaseComparisonPresentation(
                $"{row.PhaseId}:{row.LoadId}",
                cells,
                description,
                row.PathNodeIds.Count > 0 || row.PathEdgeIds.Count > 0 ||
                row.FailureDiagnostic is not null);
        }).ToArray();

    private static string BuildRecoveryPreviewSummary(
        IReadOnlyList<CommercialRecoveryPreview> previews)
    {
        CommercialRecoveryPreview[] enabled = previews
            .Where(item => item.Enabled)
            .ToArray();
        return enabled.Length == 0
            ? "현재 사용할 수 있는 복구 지점이 없습니다."
            : "복구 전 확인 · " + string.Join(" / ", enabled.Select(item =>
                $"{RecoveryKindText(item.Kind)}: {RecoveryShortText(item)}"));
    }

    private CommercialActionPresentation RecoveryActionPresentation(
        CommercialRecoveryPreview preview,
        bool enabled,
        string label,
        bool visible)
    {
        return new CommercialActionPresentation(
            visible && enabled && preview.Enabled,
            label,
            RecoveryConfirmationText(preview),
            visible);
    }

    private static string RecoveryKindText(CommercialRecoveryKind kind) => kind switch
    {
        CommercialRecoveryKind.RecentProject => "최근 공사",
        CommercialRecoveryKind.DecisionWindow => "이번 운영 단계",
        CommercialRecoveryKind.Chapter => "현재 장",
        CommercialRecoveryKind.PreviousChapter => "이전 장",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string RecoveryShortText(CommercialRecoveryPreview preview)
    {
        string removal = RecoveryRemovalText(preview, compact: true);
        string phase = preview.RestoredPhaseDisplayName is null
            ? preview.RestoredChapterDisplayName ?? "복구 지점"
            : $"{preview.RestoredPhaseDisplayName} " +
              $"{preview.RestoredPhaseNumber}/{preview.RestoredPhaseCount}";
        return $"{removal} · {phase}";
    }

    private static string RecoveryConfirmationText(CommercialRecoveryPreview preview)
    {
        if (!preview.Enabled)
        {
            return "현재 상태에서는 이 복구 지점을 사용할 수 없습니다.";
        }
        string removal = RecoveryRemovalText(preview, compact: false);
        string cash = preview.RestoredCashUnit is long restoredCash
            ? FormatWon(restoredCash)
            : "변경 없음";
        string minute = preview.RestoredMinute is long restoredMinute
            ? FormatCampaignMinute(restoredMinute)
            : "변경 없음";
        string phase = preview.RestoredPhaseDisplayName is null
            ? preview.RestoredChapterDisplayName ?? "현재 기록"
            : $"{preview.RestoredPhaseDisplayName} · " +
              $"국면 {preview.RestoredPhaseNumber}/{preview.RestoredPhaseCount}";
        string promise = preview.RestoredPromiseDisplayName is string promiseName
            ? $"{promiseName} · {RecoveryPromiseStateText(preview.RestoredPromiseDecision)}"
            : "도시 약속 없음";
        string cooling = preview.RestoredCoolingAssetIds.Count == 0
            ? "복원 후 냉각 상태 설비 없음"
            : $"냉각 상태로 복원할 설비 {preview.RestoredCoolingAssetIds.Count}곳 · " +
              string.Join(", ", preview.RestoredCoolingAssetIds);
        return $"{removal}\n복원될 상태 · 운영 자금 {cash} · 시각 {minute}\n" +
            $"장·국면 · {phase}\n약속 · {promise}\n열 상태 · {cooling}";
    }

    private static string RecoveryRemovalText(
        CommercialRecoveryPreview preview,
        bool compact)
    {
        var removed = new List<string>();
        if (preview.RemovedCompletedNodeProjectCount > 0)
        {
            removed.Add($"완공 설비 공사 {preview.RemovedCompletedNodeProjectCount}건");
        }
        if (preview.RemovedCompletedLineProjectCount > 0)
        {
            removed.Add(
                $"완공 선로 공사 {preview.RemovedCompletedLineProjectCount}건" +
                $"(선로 {preview.RemovedEdgeCount}구간·경로점 " +
                $"{preview.RemovedCompletedLineRoutePointCount}곳)");
        }
        if (preview.DiscardedDraftKind is ConstructionKind draftKind)
        {
            removed.Add(draftKind == ConstructionKind.Line
                ? $"작성 중 선로 초안(경로점 {preview.DiscardedDraftRoutePointCount}곳)"
                : "작성 중 설비 초안");
        }
        if (preview.DiscardedActiveConstructionKind is ConstructionKind activeKind)
        {
            removed.Add(activeKind == ConstructionKind.Line
                ? $"진행 중 선로 공사(경로점 {preview.DiscardedActiveLineRoutePointCount}곳)"
                : "진행 중 설비 공사");
        }
        if (removed.Count == 0)
        {
            removed.Add("선택한 복구 지점 이후 명령 기록");
        }
        string prefix = compact ? string.Empty : "제거·폐기할 작업 · ";
        return prefix + string.Join(" · ", removed);
    }

    private static string RecoveryPromiseStateText(
        CommercialPromiseDecision? decision) => decision switch
        {
            CommercialPromiseDecision.Unset => "미정",
            CommercialPromiseDecision.Keep => "지킴",
            CommercialPromiseDecision.Defer => "미룸",
            null => "해당 없음",
            _ => throw new ArgumentOutOfRangeException(nameof(decision)),
        };

    private static string FormatSupplyDiagnostic(CommercialSupplyDiagnostic diagnostic)
    {
        string source = diagnostic.AttemptedSourceDisplayName ?? "사용 가능한 발전원 없음";
        string path = diagnostic.PathNodeDisplayNames.Count == 0
            ? "사용 가능한 경로가 성립하지 않음"
            : string.Join(" → ", diagnostic.PathNodeDisplayNames);
        string limiter = diagnostic.LimitingAssetDisplayName ??
            FailureKindText(diagnostic.FailureKind);
        return $"국면 {diagnostic.PhaseNumber}/{diagnostic.PhaseCount} " +
            $"{diagnostic.PhaseDisplayName} · {diagnostic.ObligationDisplayName} " +
            $"{diagnostic.LoadDisplayName} · 발전원 {source} · 경로 {path} · " +
            $"첫 제한 설비 {limiter} · 필요 {FormatPower(diagnostic.RequiredKw)} · " +
            $"현재 {FormatPower(diagnostic.AvailableKw)} · 부족 {FormatPower(diagnostic.ShortfallKw)}";
    }

    private static string FailureKindText(ThermalFailureKind kind) => kind switch
    {
        ThermalFailureKind.NoTopologyPath => "연결 경로 없음",
        ThermalFailureKind.NoEligibleSubstation => "서비스 권역 밖",
        ThermalFailureKind.SourceCapacity => "발전원 출력",
        ThermalFailureKind.AssetUnavailable => "현재 사용불가 설비",
        ThermalFailureKind.ContinuousLimit => "연속 운전 한계",
        ThermalFailureKind.EmergencyLimit => "비상 운전 한계",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private CommercialActionPresentation NodeToolPresentation(
        CommercialCampaignSnapshot snapshot,
        string nodeClassId,
        bool ready)
    {
        CommercialNodeClassDefinition nodeClass = _productData!.World.NodeClasses.Single(
            item => item.ClassId == nodeClassId);
        bool visible = !snapshot.CampaignComplete && snapshot.AvailableNodeClassIds.Contains(
            nodeClassId,
            StringComparer.Ordinal);
        return new CommercialActionPresentation(
            visible && ready,
            nodeClass.DisplayName,
            $"{nodeClass.DisplayName}의 점유영역과 서비스 권역을 지형 위에서 계획합니다.",
            visible);
    }

    private CommercialActionPresentation LineToolPresentation(
        CommercialCampaignSnapshot snapshot,
        string lineClassId,
        string poleClassId,
        bool ready)
    {
        CommercialLineClassDefinition lineClass = _productData!.World.LineClasses.Single(
            item => item.ClassId == lineClassId);
        CommercialNodeClassDefinition poleClass = _productData.World.NodeClasses.Single(
            item => item.ClassId == poleClassId);
        bool visible = !snapshot.CampaignComplete && snapshot.AvailableLinePlans.Any(plan =>
            string.Equals(plan.LineClassId, lineClassId, StringComparison.Ordinal) &&
            string.Equals(plan.PoleClassId, poleClassId, StringComparison.Ordinal));
        return new CommercialActionPresentation(
            visible && ready,
            lineClass.DisplayName,
            $"{lineClass.DisplayName}과 {poleClass.DisplayName}으로 회랑을 계획합니다.",
            visible);
    }

    private string ConnectionFailureText(CommercialCampaignConnectionFailure failure)
    {
        string nodeName = _snapshot.World.Nodes.Single(
            item => item.NodeId == failure.NodeId).DisplayName;
        return $"{nodeName}의 접속 회선이 {failure.CurrentConnections}/" +
            $"{failure.RequiredConnections}입니다. 필요한 접속 회선을 먼저 완성하세요.";
    }

    private void SelectThermalAsset(string assetId)
    {
        ThermalIntervalEvaluation? interval = CurrentProductThermalInterval();
#if DEBUG
        if (!IsProductMode)
        {
            interval = _thermalEvaluation is null ? null : CurrentThermalInterval();
        }
#endif
        if (interval is null ||
            !interval.Assets.Any(item =>
                string.Equals(item.AssetId, assetId, StringComparison.Ordinal)))
        {
            return;
        }
        _selectedThermalAssetId = assetId;
        Render();
    }

    private void InspectApprovalChecklistItem(string itemId)
    {
        if (!IsProductMode)
        {
            return;
        }
        CommercialApprovalChecklistItem? item = _coreSnapshot!.ApprovalChecklist.Items
            .FirstOrDefault(candidate => string.Equals(
                candidate.ItemId,
                itemId,
                StringComparison.Ordinal));
        if (item is null)
        {
            return;
        }
        _selectedApprovalChecklistId = item.ItemId;
        _selectedPhaseComparisonId = null;
        SelectProjectionForPhase(item.PhaseId);
        _selectedThermalAssetId = item.LimitingAssetId ??
            item.FailureDiagnostic?.LimitingAssetId;
        _lastStatus = item.Passed
            ? $"통과한 승인 조건 ‘{item.Label}’의 근거를 지도에 표시했습니다."
            : $"남은 승인 조건 ‘{item.Label}’의 경로와 첫 제한 설비를 지도에 표시했습니다.";
        Render();
        switch (item.Kind)
        {
            case CommercialApprovalGateKind.CommandCapacity:
                _panel.FocusRecoveryResolution();
                break;
            case CommercialApprovalGateKind.PromiseDecision:
                _panel.FocusPromiseDecision();
                break;
            case CommercialApprovalGateKind.ConstructionReady:
                _panel.FocusConstructionResolution();
                break;
        }
    }

    private void InspectPhaseComparisonRow(string rowId)
    {
        if (!IsProductMode)
        {
            return;
        }
        CommercialPhaseComparisonRow? row = _coreSnapshot!.PhaseComparisonRows
            .FirstOrDefault(candidate => string.Equals(
                $"{candidate.PhaseId}:{candidate.LoadId}",
                rowId,
                StringComparison.Ordinal));
        if (row is null)
        {
            return;
        }
        _selectedApprovalChecklistId = null;
        _selectedPhaseComparisonId = rowId;
        SelectProjectionForPhase(row.PhaseId);
        _selectedThermalAssetId = row.FailureDiagnostic?.LimitingAssetId;
        _lastStatus = $"{row.LoadDisplayName} · {row.PhaseDisplayName} 경로를 지도에 표시했습니다.";
        Render();
    }

    private void SelectProjectionForPhase(string? phaseId)
    {
        if (phaseId is null || _coreSnapshot is null)
        {
            return;
        }
        int index = ProjectionIndexForPhase(_coreSnapshot, phaseId);
        if (index >= 0)
        {
            _thermalProjectionIndex = index;
        }
    }

    private static int ProjectionIndexForPhase(
        CommercialCampaignSnapshot snapshot,
        string phaseId) => snapshot.Projections.ToList().FindIndex(item =>
        string.Equals(item.Phase.PhaseId, phaseId, StringComparison.Ordinal));

    private CommercialMapHighlightPresentation? CurrentMapHighlight(
        CommercialCampaignSnapshot snapshot,
        CommercialPhaseProjection? projection)
    {
        if (projection is null)
        {
            return null;
        }
        string selectedPhaseId = projection.Phase.PhaseId;
        if (_selectedApprovalChecklistId is string checklistId)
        {
            CommercialApprovalChecklistItem? item = snapshot.ApprovalChecklist.Items
                .FirstOrDefault(candidate => string.Equals(
                    candidate.ItemId,
                    checklistId,
                    StringComparison.Ordinal));
            if (item is null || !string.Equals(
                    item.PhaseId,
                    selectedPhaseId,
                    StringComparison.Ordinal))
            {
                return null;
            }
            CommercialSupplyDiagnostic? diagnostic = item.FailureDiagnostic;
            IReadOnlyList<string> nodeIds = HighlightNodeIds(
                item.PathNodeIds,
                diagnostic);
            if (item.NodeId is string itemNodeId &&
                !nodeIds.Contains(itemNodeId, StringComparer.Ordinal))
            {
                nodeIds = [.. nodeIds, itemNodeId];
            }
            return new CommercialMapHighlightPresentation(
                nodeIds,
                item.PathEdgeIds,
                item.LimitingAssetId ?? diagnostic?.LimitingAssetId,
                diagnostic is null
                    ? $"승인 조건 {item.Label} 지도 강조."
                    : FormatSupplyDiagnostic(diagnostic));
        }
        if (_selectedPhaseComparisonId is string comparisonId)
        {
            CommercialPhaseComparisonRow? row = snapshot.PhaseComparisonRows
                .FirstOrDefault(candidate => string.Equals(
                    $"{candidate.PhaseId}:{candidate.LoadId}",
                    comparisonId,
                    StringComparison.Ordinal));
            if (row is null || !string.Equals(
                    row.PhaseId,
                    selectedPhaseId,
                    StringComparison.Ordinal))
            {
                return null;
            }
            IReadOnlyList<string> nodeIds = HighlightNodeIds(
                row.PathNodeIds,
                row.FailureDiagnostic);
            return new CommercialMapHighlightPresentation(
                nodeIds,
                row.PathEdgeIds,
                row.FailureDiagnostic?.LimitingAssetId,
                $"{row.PhaseDisplayName} {row.LoadDisplayName} 경로 강조. " +
                (row.FailureDiagnostic is null
                    ? string.Empty
                    : FormatSupplyDiagnostic(row.FailureDiagnostic)));
        }
        if (snapshot.FirstBlockingDiagnostic is CommercialSupplyDiagnostic firstBlocking &&
            string.Equals(
                firstBlocking.PhaseId,
                selectedPhaseId,
                StringComparison.Ordinal))
        {
            return new CommercialMapHighlightPresentation(
                HighlightNodeIds(firstBlocking.PathNodeIds, firstBlocking),
                firstBlocking.PathEdgeIds,
                firstBlocking.LimitingAssetId,
                FormatSupplyDiagnostic(firstBlocking));
        }
        return null;
    }

    private IReadOnlyList<string> HighlightNodeIds(
        IReadOnlyList<string> pathNodeIds,
        CommercialSupplyDiagnostic? diagnostic)
    {
        if (diagnostic?.FailureKind != ThermalFailureKind.NoTopologyPath)
        {
            return pathNodeIds;
        }
        string? terminalNodeId = DiagnosticTerminalNodeId(diagnostic);
        if (terminalNodeId is null || pathNodeIds.Contains(terminalNodeId, StringComparer.Ordinal))
        {
            return pathNodeIds;
        }
        return [.. pathNodeIds, terminalNodeId];
    }

    private string? DiagnosticTerminalNodeId(CommercialSupplyDiagnostic? diagnostic)
    {
        if (diagnostic?.FailureKind != ThermalFailureKind.NoTopologyPath)
        {
            return null;
        }
        return _productData?.World.Loads.FirstOrDefault(load => string.Equals(
            load.LoadId,
            diagnostic.LoadId,
            StringComparison.Ordinal))?.NodeId;
    }

    private void ChangeThermalProjection(int delta)
    {
        int count = _coreSnapshot?.Projections.Count ?? 0;
#if DEBUG
        if (!IsProductMode)
        {
            count = _thermalEvaluation?.Intervals.Count ?? 0;
        }
#endif
        if (count == 0 || delta == 0)
        {
            return;
        }
        int next = Math.Clamp(
            _thermalProjectionIndex + Math.Sign(delta),
            0,
            count - 1);
        if (next == _thermalProjectionIndex)
        {
            return;
        }
        _thermalProjectionIndex = next;
        _selectedApprovalChecklistId = null;
        _selectedPhaseComparisonId = null;
        ThermalIntervalEvaluation interval;
#if DEBUG
        if (!IsProductMode)
        {
            interval = CurrentThermalInterval();
            _lastStatus = $"열 시험 국면 {_thermalProjectionIndex + 1}을 표시했습니다.";
        }
        else
#endif
        {
            CommercialPhaseProjection projection =
                _coreSnapshot!.Projections[_thermalProjectionIndex];
            interval = projection.Evaluation;
            _lastStatus = $"{projection.Phase.DisplayName} 운영 국면을 표시했습니다.";
        }
        EnsureThermalSelection(interval);
        Render();
    }

    private ThermalIntervalEvaluation? CurrentProductThermalInterval()
    {
        if (_coreSnapshot is null || _coreSnapshot.Projections.Count == 0)
        {
            return null;
        }
        _thermalProjectionIndex = Math.Clamp(
            _thermalProjectionIndex,
            0,
            _coreSnapshot.Projections.Count - 1);
        return _coreSnapshot.Projections[_thermalProjectionIndex].Evaluation;
    }

#if DEBUG
    private ThermalIntervalEvaluation CurrentThermalInterval()
    {
        if (_thermalEvaluation is null || _thermalEvaluation.Intervals.Count == 0)
        {
            throw new InvalidOperationException("열 운전 결과가 준비되지 않았습니다.");
        }
        return _thermalEvaluation.Intervals[_thermalProjectionIndex];
    }
#endif

    private void EnsureThermalSelection(ThermalIntervalEvaluation interval)
    {
        if (_selectedThermalAssetId is not null && interval.Assets.Any(item =>
                string.Equals(item.AssetId, _selectedThermalAssetId, StringComparison.Ordinal)))
        {
            return;
        }
        _selectedThermalAssetId = interval.Assets
            .OrderBy(item => item.AssetId, StringComparer.Ordinal)
            .FirstOrDefault()
            ?.AssetId;
    }

#if DEBUG
    private string ThermalProjectionLabel() =>
        $"국면 {_thermalProjectionIndex + 1} / {_thermalEvaluation!.Intervals.Count}";
#endif

    private string ThermalSelectionText(
        ThermalIntervalEvaluation interval,
        bool continuousOnly = false,
        SpatialWorldDefinition? projectedWorld = null)
    {
        ThermalAssetUsage? usage = interval.Assets.FirstOrDefault(item =>
            string.Equals(item.AssetId, _selectedThermalAssetId, StringComparison.Ordinal));
        if (usage is null)
        {
            return "선택한 열 설비가 없습니다.";
        }
        string text = continuousOnly
            ? $"{ThermalAssetName(usage, projectedWorld)}. 현재 사용 {FormatPower(usage.UsedKw)}, " +
              $"연속 한계 {FormatPower(usage.ContinuousKw)}. " +
              $"현재 상태 {ThermalStateText(usage.State)}."
            : $"{ThermalAssetName(usage, projectedWorld)}. 현재 사용 {FormatPower(usage.UsedKw)}, " +
              $"연속 한계 {FormatPower(usage.ContinuousKw)}, " +
              $"비상 한계 {FormatPower(usage.EmergencyKw)}. " +
              $"현재 상태 {ThermalStateText(usage.State)}, " +
              $"다음 상태 {ThermalStateText(usage.NextState)}.";
        CommercialServiceAreaPresentation? service = SelectedServiceArea(projectedWorld);
        return service is null
            ? text
            : text + $" {service.Label}. 점유영역과 다음 분기를 놓을 공간을 함께 확인하세요.";
    }

    private string ThermalAssetName(
        ThermalAssetUsage usage,
        SpatialWorldDefinition? projectedWorld = null)
    {
        CommercialWorldDefinition thermalWorld = _productData?.World
#if DEBUG
            ?? _thermalWorld
#endif
            ??
            throw new InvalidOperationException("열 운전 전력망이 준비되지 않았습니다.");
        SpatialWorldDefinition spatialWorld = projectedWorld ?? _snapshot.World;
        if (usage.AssetKind == ThermalAssetKind.Node)
        {
            SpatialNodeDefinition? node = spatialWorld.Nodes.FirstOrDefault(
                item => item.NodeId == usage.AssetId);
            if (node is null)
            {
                string? draftClassId = _snapshot.LineDraft?.PoleClassId ??
                    _snapshot.NodeDraft?.NodeClassId;
                CommercialNodeClassDefinition? draftClass = thermalWorld.NodeClasses
                    .FirstOrDefault(item => item.ClassId == draftClassId);
                return draftClass is null
                    ? "계획 중인 접속 설비"
                    : $"계획 중인 {draftClass.DisplayName} 접속부";
            }
            CommercialNodeClassDefinition nodeClass = thermalWorld.NodeClasses.Single(
                item => item.ClassId == node.ClassId);
            string equipment = nodeClass.Kind switch
            {
                SpatialNodeKind.Pole => "전신주 접속부",
                SpatialNodeKind.Substation => "변전소 제한 요소",
                _ => "열 설비",
            };
            return $"{node.DisplayName} · {equipment}";
        }

        SpatialEdgeDefinition? edge = spatialWorld.Edges.FirstOrDefault(
            item => item.EdgeId == usage.AssetId);
        if (edge is null)
        {
            CommercialLineClassDefinition? draftClass = thermalWorld.LineClasses
                .FirstOrDefault(item => item.ClassId == _snapshot.LineDraft?.LineClassId);
            return draftClass is null
                ? "계획 중인 선로 도체"
                : $"계획 중인 {draftClass.DisplayName} 도체";
        }
        SpatialNodeDefinition from = spatialWorld.Nodes.Single(
            item => item.NodeId == edge.FromNodeId);
        SpatialNodeDefinition to = spatialWorld.Nodes.Single(
            item => item.NodeId == edge.ToNodeId);
        CommercialLineClassDefinition lineClass = thermalWorld.LineClasses.Single(
            item => item.ClassId == edge.LineClassId);
        return $"{from.DisplayName}–{to.DisplayName} · {lineClass.DisplayName} 도체";
    }

    private static string ThermalStateText(ThermalOperatingState state) => state switch
    {
        ThermalOperatingState.Continuous => "연속 운전",
        ThermalOperatingState.Emergency => "비상 운전",
        ThermalOperatingState.ProtectiveOutage => "보호정지",
        ThermalOperatingState.OverLimit => "비상 한계 초과",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private string CoreErrorText(
        CommercialCampaignRunError? error,
        ConstructionError? constructionError,
        CommercialCampaignConnectionFailure? connectionFailure) => error switch
        {
            CommercialCampaignRunError.ConstructionRejected => ErrorText(constructionError),
            CommercialCampaignRunError.InsufficientCash => "현재 운영 자금으로 이 공사를 발주할 수 없습니다.",
            CommercialCampaignRunError.DeadlineExceeded => "이 공사는 이번 운영 단계의 공사 기한을 넘깁니다.",
            CommercialCampaignRunError.PromiseDecisionRequired =>
                "운영안을 승인하기 전에 도시 약속을 지킬지 미룰지 선택하세요.",
            CommercialCampaignRunError.SafetyDutyUnserved =>
                "이번 운영 단계에서 필수 안전 수요를 공급하지 못합니다.",
            CommercialCampaignRunError.KeptPromiseUnserved =>
                "지키기로 한 도시 약속의 수요를 현재 운영안으로 공급하지 못합니다.",
            CommercialCampaignRunError.FutureSafetyAtRisk =>
                "현재 운전 뒤 보호정지 때문에 다음 공개 국면의 필수 공급이 끊깁니다.",
            CommercialCampaignRunError.ConnectionRequirementUnmet when
                connectionFailure is not null => ConnectionFailureText(connectionFailure),
            CommercialCampaignRunError.ArithmeticOverflow =>
                "공사 완료 시각을 계산할 수 없어 발주하지 않았습니다. " +
                "현재 저장과 공사 계획은 바뀌지 않았습니다.",
            CommercialCampaignRunError.CommandLimit =>
                "이 저장에서 처리할 수 있는 작업 수를 넘었습니다. 현재 진행을 저장하고 다시 시작하세요.",
            CommercialCampaignRunError.WrongState or CommercialCampaignRunError.InvalidCommandShape or
            CommercialCampaignRunError.ToolUnavailable =>
                "현재 진행 상태에서는 이 작업을 실행할 수 없습니다.",
            null when constructionError is not null => ErrorText(constructionError),
            _ => "현재 작업을 완료하지 못했습니다. 계획 상태를 다시 확인하세요.",
        };

    private static string SupplyFailureText(ThermalSupplyFailure failure)
    {
        long shortage = Math.Max(0, failure.RequiredKw - failure.AvailableKw);
        string amount = shortage == 0 ? string.Empty : $" {FormatPower(shortage)}가 부족합니다.";
        return failure.Kind switch
        {
            ThermalFailureKind.NoTopologyPath => "수요처까지 이어지는 공급 경로가 없습니다.",
            ThermalFailureKind.NoEligibleSubstation =>
                "수요처가 공급 경로의 배전 변전소 서비스 권역 밖에 있습니다.",
            ThermalFailureKind.SourceCapacity => "발전원 출력 여유가 부족합니다." + amount,
            ThermalFailureKind.AssetUnavailable => "현재 국면에 사용할 수 없는 설비가 경로를 막습니다.",
            ThermalFailureKind.ContinuousLimit => "경로의 연속 운전 여유가 부족합니다." + amount,
            ThermalFailureKind.EmergencyLimit => "경로의 비상 운전 여유도 부족합니다." + amount,
            _ => throw new ArgumentOutOfRangeException(nameof(failure)),
        };
    }

    private static string ObligationText(CommercialObligationKind obligation) => obligation switch
    {
        CommercialObligationKind.SafetyDuty => "안전 의무",
        CommercialObligationKind.CityPromise => "도시 약속",
        CommercialObligationKind.OperatingRecord => "운영 기록",
        _ => throw new ArgumentOutOfRangeException(nameof(obligation)),
    };

    private static string PromiseText(CommercialPromiseDecision decision) => decision switch
    {
        CommercialPromiseDecision.Unset => "아직 선택하지 않았습니다.",
        CommercialPromiseDecision.Keep => "이번 약속을 지킵니다.",
        CommercialPromiseDecision.Defer => "이번 약속을 미룹니다.",
        _ => throw new ArgumentOutOfRangeException(nameof(decision)),
    };

    private static string FormatCampaignMinute(long minute)
    {
        long day = Math.DivRem(minute, 24L * 60L, out long minuteOfDay) + 1L;
        long hour = minuteOfDay / 60L;
        long minutePart = minuteOfDay % 60L;
        return $"{day}일차 {hour:00}:{minutePart:00}";
    }

    private static string FormatElapsedDuration(int minutes)
    {
        int days = Math.DivRem(minutes, 24 * 60, out int minuteOfDay);
        int hours = Math.DivRem(minuteOfDay, 60, out int minutePart);
        var parts = new List<string>(3);
        if (days > 0)
        {
            parts.Add($"{days}일");
        }
        if (hours > 0)
        {
            parts.Add($"{hours}시간");
        }
        if (minutePart > 0 || parts.Count == 0)
        {
            parts.Add($"{minutePart}분");
        }
        return string.Join(" ", parts);
    }

    private static string FormatPower(long kilowatts) =>
        kilowatts.ToString("N0", CultureInfo.GetCultureInfo("ko-KR")) + " kW";

#if DEBUG
    private string HeadingText() => _snapshot.Phase switch
    {
        ConstructionPhase.NodeDrafting => "변전소 계획",
        ConstructionPhase.LineDrafting => "선로 계획",
        ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding => "공사 진행",
        _ => "첫 불빛 · 자유 배치",
    };

    private string InstructionText() => _snapshot.Phase switch
    {
        ConstructionPhase.NodeDrafting => "점유영역과 견적을 확인한 뒤 발주하세요. 다른 위치를 클릭하면 계획 위치가 바뀝니다.",
        ConstructionPhase.LineDrafting when _snapshot.LineDraft?.EndNodeId is not null =>
            "전체 경로와 견적을 확인한 뒤 발주하세요.",
        ConstructionPhase.LineDrafting =>
            "빈 지형에는 전신주가 놓입니다. 작성 중 전신주는 드래그로 옮기고 마지막 점은 Backspace 또는 오른쪽 클릭으로 되돌립니다.",
        ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding =>
            "발주한 공사는 임의로 움직일 수 없습니다. 완공 시각까지 진행하세요.",
        _ => "보이는 격자 없이 지형을 읽고, 발전 접속점과 생활권을 직접 이어 보세요.",
    };
#endif

    private string ActiveLineDisplayName() => _snapshot.World.LineClasses
        .Single(item => item.ClassId == _activeLineClassId)
        .DisplayName;

    private string ActiveNodeDisplayName() => _snapshot.World.NodeClasses
        .Single(item => item.ClassId == _activeNodeClassId)
        .DisplayName;

    private string ToolInstruction() => _tool switch
    {
        CommercialTool.Substation => $"{ActiveNodeDisplayName()} 위치를 선택하세요.",
        CommercialTool.Line => $"{ActiveLineDisplayName()} 경로를 선택하세요.",
        _ => "오른쪽에서 시작할 작업을 선택하세요.",
    };

    private string ToolName() => _tool switch
    {
        CommercialTool.Substation => ActiveNodeDisplayName() + " 배치",
        CommercialTool.Line => ActiveLineDisplayName() + " 계획",
        _ => "작업 선택",
    };

    private string ConstructionPhaseText() => _snapshot.Phase switch
    {
        ConstructionPhase.Ready => "새 계획 가능",
        ConstructionPhase.NodeDrafting => "변전소 초안",
        ConstructionPhase.LineDrafting => "선로 초안",
        ConstructionPhase.NodeBuilding => "변전소 공사 중",
        ConstructionPhase.LineBuilding => "선로 공사 중",
        _ => throw new ArgumentOutOfRangeException(),
    };

    private int? PointerFootprintRadius()
    {
        if (_tool == CommercialTool.Substation &&
            _snapshot.Phase is ConstructionPhase.Ready or ConstructionPhase.NodeDrafting)
        {
            return _snapshot.World.NodeClasses.Single(item => item.ClassId == _activeNodeClassId)
                .FootprintRadiusUnit;
        }
        if (_tool == CommercialTool.Line &&
            _snapshot.Phase == ConstructionPhase.LineDrafting &&
            _candidateNodeId is null &&
            _snapshot.LineDraft?.EndNodeId is null)
        {
            return _snapshot.World.NodeClasses.Single(item => item.ClassId == _activePoleClassId)
                .FootprintRadiusUnit;
        }
        return null;
    }

    private int? PointerServiceRadius()
    {
        if (!IsProductMode || _tool != CommercialTool.Substation ||
            _snapshot.Phase is not (ConstructionPhase.Ready or ConstructionPhase.NodeDrafting))
        {
            return null;
        }
        return _productData!.World.NodeClasses.Single(
            item => item.ClassId == _activeNodeClassId).ServiceRadiusUnit;
    }

    private int? DraftServiceRadius()
    {
        if (!IsProductMode || _snapshot.NodeDraft is not NodeDraftSnapshot draft)
        {
            return null;
        }
        return _productData!.World.NodeClasses.Single(
            item => item.ClassId == draft.NodeClassId).ServiceRadiusUnit;
    }

    private CommercialServiceAreaPresentation? SelectedServiceArea(
        SpatialWorldDefinition? projectedWorld = null)
    {
        if (!IsProductMode || _tool != CommercialTool.None ||
            _selectedThermalAssetId is not string selectedId)
        {
            return null;
        }
        SpatialWorldDefinition spatialWorld = projectedWorld ?? _snapshot.World;
        SpatialNodeDefinition? node = spatialWorld.Nodes.FirstOrDefault(
            item => item.NodeId == selectedId);
        if (node is null)
        {
            return null;
        }
        CommercialNodeClassDefinition nodeClass = _productData!.World.NodeClasses.Single(
            item => item.ClassId == node.ClassId);
        if (nodeClass.ServiceRadiusUnit is not int radius)
        {
            return null;
        }
        int connections = spatialWorld.Edges.Count(edge =>
            edge.Commissioned &&
            (edge.FromNodeId == node.NodeId || edge.ToNodeId == node.NodeId));
        return new CommercialServiceAreaPresentation(
            node.Position,
            radius,
            $"서비스 권역 · 연결 회선 {connections}/{nodeClass.MaxConnections}");
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

    private string CandidateSelectionText(string nodeId)
    {
        SpatialNodeDefinition node = _snapshot.World.Nodes.Single(item => item.NodeId == nodeId);
        SpatialNodeClassDefinition nodeClass = _snapshot.World.NodeClasses.Single(
            item => item.ClassId == node.ClassId);
        string kind = nodeClass.Kind switch
        {
            SpatialNodeKind.SourceTerminal => "발전 접속점",
            SpatialNodeKind.Substation => "변전소",
            SpatialNodeKind.DedicatedLoadTerminal => "수요 접속점",
            SpatialNodeKind.Pole => "전신주",
            _ => "접속 설비",
        };
        int index = _map.CandidateNodeIds.ToList().FindIndex(id => string.Equals(
            id,
            nodeId,
            StringComparison.Ordinal));
        int count = _map.CandidateNodeIds.Count;
        string order = index >= 0 && count > 0
            ? $"후보 {index + 1}/{count}"
            : "선택 후보";
        return $"{order} · {kind} · {node.DisplayName} · 위치 " +
            $"{FormatDesignCoordinate(node.Position.XUnit)}, " +
            FormatDesignCoordinate(node.Position.YUnit) +
            (count > 1 ? " · Q/E로 변경, Enter로 확정" : " · Enter로 확정");
    }

    private static string FormatDesignCoordinate(int internalUnit) =>
        (internalUnit / 100m).ToString("0.##", CultureInfo.InvariantCulture);

#if DEBUG
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
#endif

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

    private void ShowHelp()
    {
        _shell.ShowHelp();
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

#if DEBUG
    private static byte[] ReadEmbeddedResourceBytes(string resourceName)
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("상용 게임 데이터를 열 수 없습니다.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
#endif

}
