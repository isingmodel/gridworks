using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Gridworks.Core.Product;
using Gridworks.Core.Release;
using Godot;

namespace Gridworks.Game;

public sealed partial class ReleaseMain : Control
{
    private enum Tool
    {
        Inspect,
        SmallSubstation,
        LargeSubstation,
        StandardLine,
        ReinforcedLine,
    }

    private ReleaseLaunchOptions _options = null!;
    private ReleaseConstructionSession? _session;
    private ReleaseCampaignRun? _run;
    private ReleaseCampaignDefinition _campaign = null!;
    private ReleaseWorldDefinition _world = null!;
    private ReleaseCampaignSnapshot? _campaignSnapshot;
    private ReleaseConstructionSnapshot _snapshot = null!;
    private Tool _tool = Tool.Inspect;
    private ReleasePoint? _pointerPoint;
    private bool _pointerAccepted = true;
    private ReleaseConstructionError? _pointerError;
    private string? _hoverNodeId;
    private string? _hoverEdgeId;
    private string? _selectedNodeId;
    private string? _selectedEdgeId;
    private ReleaseConstructionError? _lastError;
    private string _fixtureHash = string.Empty;
    private string _campaignHash = string.Empty;
    private string _campaignSavePath = string.Empty;
    private string _legacyCampaignSavePath = string.Empty;
    private string _settingsPath = string.Empty;
    private ReleaseCampaignSave? _continuationSave;
    private ProductSettings _settings = ProductSettings.Default;
    private string _titleStatus = string.Empty;
    private ReleaseCampaignError? _campaignError;
    private ReleaseChapterAssessment? _assessment;
    private Action? _storyContinuation;
    private Action? _afterPauseContinuation;
    private bool _preserveCurrentSaveBeforeWrite;
    private bool _showEventProjection;
    private string? _pendingCampaignSaveError;

    private Label _phaseLabel = null!;
    private Label _timeLabel = null!;
    private Label _supplyLabel = null!;
    private Label _assetLabel = null!;
    private Label _cashLabel = null!;
    private Button _menuButton = null!;
    private ReleaseMapView _map = null!;
    private ReleaseTaskPanel _panel = null!;
    private ReleaseShellOverlay _shell = null!;
    private ReleaseAudio _audio = null!;
    private Control _storyOverlay = null!;
    private Label _storyCategory = null!;
    private Label _storyTitle = null!;
    private Label _storyBody = null!;
    private Button _storyButton = null!;
    private Label _fatalLabel = null!;
    private readonly Dictionary<Control, int> _baseFontSizes = [];

    public override void _Ready()
    {
        try
        {
            GetWindow().Title = "Gridworks · 청류시 전력운영센터";
            _options = ReleaseLaunchOptions.Parse(OS.GetCmdlineUserArgs());
            byte[] worldBytes = ReleaseEmbeddedData.ReadWorldBytes();
            byte[] campaignBytes = ReleaseEmbeddedData.ReadCampaignBytes();
            _fixtureHash = Convert.ToHexString(SHA256.HashData(worldBytes)).ToLowerInvariant();
            _campaignHash = Convert.ToHexString(SHA256.HashData(campaignBytes)).ToLowerInvariant();
            _world = ReleaseWorldLoader.Load(worldBytes);
            _campaign = ReleaseCampaignLoader.Load(campaignBytes, _world);
            BindScene();
            if (_options.Smoke)
            {
                _session = new ReleaseConstructionSession(_world);
                _snapshot = _session.GetSnapshot();
                Render();
                ShowStory(
                    "운영 브리핑",
                    "동부 공급 경로를 보강합니다",
                    "동부 지역의 전력 사용이 늘어 기존 경로 하나만으로는 정전에 대비하기 어렵습니다. " +
                    "새 변전소 부지를 정한 뒤 남부 분기 전신주에서 동부 변전소까지 우회선을 연결하세요.",
                    "공사 시작하기");
                GD.Print($"RELEASE_READY session={_options.SessionId} fixtureHash={_fixtureHash}");
                CallDeferred(nameof(RunSmoke));
                return;
            }

            ConfigurePersistencePaths();
            LoadPersistentState();
            ApplySettings(_settings);
            _shell.ShowTitle(_continuationSave is not null, _titleStatus);
            GD.Print(
                $"RELEASE_CAMPAIGN_READY session={_options.SessionId} " +
                $"worldHash={_fixtureHash} campaignHash={_campaignHash}");
            if (_options.CampaignSmokeLeg == ReleaseCampaignSmokeLeg.Save)
            {
                CallDeferred(nameof(RunCampaignSmokeSave));
            }
            else if (_options.CampaignSmokeLeg == ReleaseCampaignSmokeLeg.Continue)
            {
                CallDeferred(nameof(RunCampaignSmokeContinue));
            }
        }
        catch (Exception exception)
        {
            GD.PushError($"출시판 화면을 시작하지 못했습니다: {exception}");
            ShowFatal("게임을 시작하지 못했습니다. 설치 파일을 다시 확인하세요.");
            if (_options?.Automated == true ||
                OS.GetCmdlineUserArgs().Contains("--release-smoke"))
            {
                GetTree().Quit(1);
            }
        }
    }

    private void BindScene()
    {
        _phaseLabel = GetNode<Label>("%PhaseLabel");
        _timeLabel = GetNode<Label>("%TimeLabel");
        _supplyLabel = GetNode<Label>("%SupplyLabel");
        _assetLabel = GetNode<Label>("%AssetLabel");
        _cashLabel = GetNode<Label>("%CashLabel");
        _menuButton = GetNode<Button>("%MenuButton");
        _map = GetNode<ReleaseMapView>("%ReleaseMapView");
        _panel = GetNode<ReleaseTaskPanel>("%ReleaseTaskPanel");
        _shell = GetNode<ReleaseShellOverlay>("%ReleaseShellOverlay");
        _audio = GetNode<ReleaseAudio>("%ReleaseAudio");
        _storyOverlay = GetNode<Control>("%StoryOverlay");
        _storyCategory = GetNode<Label>("%StoryCategoryLabel");
        _storyTitle = GetNode<Label>("%StoryTitleLabel");
        _storyBody = GetNode<Label>("%StoryBodyLabel");
        _storyButton = GetNode<Button>("%StoryButton");
        _fatalLabel = GetNode<Label>("%FatalLabel");

        _map.PointerChanged += OnPointerChanged;
        _map.PointRequested += OnPointRequested;
        _map.AssetUnderPointerChanged += OnAssetUnderPointerChanged;
        _panel.ActionRequested += OnPanelAction;
        _storyButton.Pressed += HideStory;
        _menuButton.Pressed += ShowPause;
        _shell.PauseRequested += ShowPause;
        _shell.NewGameRequested += StartNewCampaign;
        _shell.ContinueRequested += ContinueCampaign;
        _shell.ResumeRequested += ResumeGameplay;
        _shell.SaveAndQuitRequested += SaveAndReturnToTitle;
        _shell.RestartChapterRequested += RestartChapter;
        _shell.RewindPreviousChapterRequested += RewindToPreviousChapter;
        _shell.FullscreenChanged += OnFullscreenChanged;
        _shell.UiScalePercentChanged += OnUiScalePercentChanged;
        _shell.MasterVolumePercentChanged += value => UpdateVolume(master: value);
        _shell.AmbientVolumePercentChanged += value => UpdateVolume(ambient: value);
        _shell.SfxVolumePercentChanged += value => UpdateVolume(sfx: value);
        _shell.ControlHelpChanged += OnControlHelpChanged;
        _shell.GameplayFocusRequested += () =>
        {
            Control target = _storyOverlay.Visible ? _storyButton : _map;
            target.CallDeferred(Control.MethodName.GrabFocus);
        };
    }

    private void OnPointerChanged(ReleasePoint? point)
    {
        _pointerPoint = point;
        _pointerAccepted = true;
        _pointerError = null;
        if (point is ReleasePoint actual)
        {
            if (_tool is Tool.SmallSubstation or Tool.LargeSubstation &&
                _snapshot.Phase is ReleaseConstructionPhase.Ready or ReleaseConstructionPhase.NodeDrafting)
            {
                ReleaseNodePlacementPreview preview = PreviewNodePlacement(
                    NodeClassForTool(),
                    actual);
                _pointerAccepted = preview.Accepted;
                _pointerError = preview.Error;
            }
            else if (_snapshot.Phase == ReleaseConstructionPhase.LineDrafting)
            {
                ReleaseLinePointPreview preview = PreviewLinePoint(actual);
                _pointerAccepted = preview.Accepted;
                _pointerError = preview.Error;
            }
        }
        Render();
    }

    private void OnAssetUnderPointerChanged(string? nodeId, string? edgeId)
    {
        _hoverNodeId = nodeId;
        _hoverEdgeId = edgeId;
        if (_tool == Tool.Inspect)
        {
            Render();
        }
    }

    private void OnPointRequested(ReleasePoint point)
    {
        if (_tool == Tool.Inspect)
        {
            _selectedNodeId = _hoverNodeId ?? NodeAt(point)?.NodeId;
            _selectedEdgeId = _selectedNodeId is null ? _hoverEdgeId : null;
            Render();
            return;
        }

        if (_tool is Tool.SmallSubstation or Tool.LargeSubstation)
        {
            ExecuteMutation(
                new ReleaseCampaignCommand(
                    ReleaseCampaignCommandKind.SetNodeDraft,
                    Position: point,
                    NodeClassId: NodeClassForTool()),
                () => _session!.SetNodeDraft(NodeClassForTool(), point));
            return;
        }
        if (_tool is Tool.StandardLine or Tool.ReinforcedLine)
        {
            if (_snapshot.Phase == ReleaseConstructionPhase.Ready)
            {
                ReleaseNodeDefinition? start = NodeAt(point);
                string startNodeId = start?.NodeId ?? string.Empty;
                ExecuteMutation(
                    new ReleaseCampaignCommand(
                        ReleaseCampaignCommandKind.StartLineDraft,
                        StartNodeId: startNodeId,
                        LineClassId: LineClassForTool(),
                        PoleClassId: PoleClassForTool()),
                    () => _session!.StartLineDraft(
                        startNodeId,
                        LineClassForTool(),
                        PoleClassForTool()));
            }
            else if (_snapshot.Phase == ReleaseConstructionPhase.LineDrafting)
            {
                ExecuteMutation(
                    new ReleaseCampaignCommand(
                        ReleaseCampaignCommandKind.AddLinePoint,
                        Position: point),
                    () => _session!.AddLinePoint(point));
            }
        }
    }

    private void OnPanelAction(ReleasePanelAction action)
    {
        switch (action)
        {
            case ReleasePanelAction.Inspect:
                _tool = Tool.Inspect;
                _lastError = null;
                break;
            case ReleasePanelAction.SmallSubstation:
                _showEventProjection = false;
                _tool = Tool.SmallSubstation;
                _lastError = null;
                break;
            case ReleasePanelAction.LargeSubstation:
                _showEventProjection = false;
                _tool = Tool.LargeSubstation;
                _lastError = null;
                break;
            case ReleasePanelAction.StandardLine:
                _showEventProjection = false;
                _tool = Tool.StandardLine;
                _lastError = null;
                break;
            case ReleasePanelAction.ReinforcedLine:
                _showEventProjection = false;
                _tool = Tool.ReinforcedLine;
                _lastError = null;
                break;
            case ReleasePanelAction.Cancel:
                bool cancelNode = _snapshot.Phase == ReleaseConstructionPhase.NodeDrafting;
                ExecuteMutation(
                    new ReleaseCampaignCommand(cancelNode
                        ? ReleaseCampaignCommandKind.CancelNodeDraft
                        : ReleaseCampaignCommandKind.CancelLineDraft),
                    () => cancelNode
                        ? _session!.CancelNodeDraft()
                        : _session!.CancelLineDraft());
                if (_lastError is null)
                {
                    _tool = Tool.Inspect;
                }
                break;
            case ReleasePanelAction.Undo:
                ExecuteMutation(
                    new ReleaseCampaignCommand(ReleaseCampaignCommandKind.UndoLinePoint),
                    () => _session!.UndoLinePoint());
                break;
            case ReleasePanelAction.Order:
                bool orderNode = _snapshot.Phase == ReleaseConstructionPhase.NodeDrafting;
                if (ExecuteMutation(
                    new ReleaseCampaignCommand(orderNode
                        ? ReleaseCampaignCommandKind.OrderNode
                        : ReleaseCampaignCommandKind.OrderLine),
                    () => orderNode
                        ? _session!.OrderNode()
                        : _session!.OrderLine()))
                {
                    _audio.PlayLive(ReleaseAudioCue.Breaker);
                }
                break;
            case ReleasePanelAction.Advance:
                ReleaseConstructionKind? kind = _snapshot.ActiveConstruction?.Kind;
                bool advanced = ExecuteMutation(
                    new ReleaseCampaignCommand(ReleaseCampaignCommandKind.AdvanceConstruction),
                    () => _session!.AdvanceToConstructionCompletion());
                if (advanced)
                {
                    _audio.PlayLive(ReleaseAudioCue.Energize);
                }
                if (_run is null && _lastError is null && kind == ReleaseConstructionKind.Node)
                {
                    _tool = Tool.Inspect;
                    ShowStory(
                        "공사 상황",
                        "변전소 공사가 끝났습니다",
                        "새 변전소는 완공됐지만 아직 전력망과 연결되지 않아 동부 지역에 전력을 보낼 수 없습니다. " +
                        "남부 분기 전신주에서 동부 변전소까지 우회선을 연결하세요.",
                        "우회선 계획하기",
                        () =>
                        {
                            _tool = Tool.ReinforcedLine;
                            Render();
                        });
                }
                else if (_run is null && _lastError is null && kind == ReleaseConstructionKind.Line)
                {
                    _tool = Tool.Inspect;
                    ShowStory(
                        "공사 결과",
                        "남부 우회선이 연결됐습니다",
                        "동부 지역에 두 번째 공급 경로가 생겼습니다. 남부 분기 전신주는 네 구간을 연결하고, " +
                        "동부 변전소에는 두 경로가 이어집니다. 설비 부하와 여유 용량을 확인하세요.",
                        "전력망 확인하기");
                }
                break;
            case ReleasePanelAction.ToggleEventView:
                _tool = Tool.Inspect;
                _showEventProjection = !_showEventProjection;
                _lastError = null;
                _campaignError = null;
                _assessment = null;
                break;
            case ReleasePanelAction.Evaluate:
                EvaluateCampaignChapter();
                break;
        }
        Render();
    }

    private void Apply(ReleaseConstructionCommandResult result)
    {
        _snapshot = result.Snapshot;
        _lastError = result.Accepted ? null : result.Error;
        if (result.Accepted)
        {
            _pointerError = null;
        }
        Render();
    }

    private bool ExecuteMutation(
        ReleaseCampaignCommand command,
        Func<ReleaseConstructionCommandResult> legacy)
    {
        if (_run is null)
        {
            ReleaseConstructionCommandResult result = legacy();
            Apply(result);
            return result.Accepted;
        }

        ReleaseCampaignCommandResult campaignResult = _run.Execute(command);
        ApplyCampaignResult(campaignResult, saveAccepted: true);
        if (_pendingCampaignSaveError is not null)
        {
            ShowPendingCampaignSaveError();
        }
        return campaignResult.Accepted;
    }

    private void ApplyCampaignResult(
        ReleaseCampaignCommandResult result,
        bool saveAccepted)
    {
        _campaignSnapshot = result.Snapshot;
        _snapshot = result.Snapshot.Construction;
        _lastError = result.ConstructionError;
        _campaignError = result.Error;
        _assessment = result.Assessment;
        if (result.Accepted)
        {
            _pointerError = null;
            if (result.CompletedChapter is not null)
            {
                _showEventProjection = false;
            }
            if (saveAccepted && !TrySaveCampaign(out string saveError))
            {
                _titleStatus = saveError;
                _pendingCampaignSaveError = saveError;
            }
            else if (saveAccepted)
            {
                _pendingCampaignSaveError = null;
            }
        }
        else if (result.Assessment?.FailedDuringEvent == true)
        {
            _showEventProjection = true;
        }
        Render();
    }

    private void Render()
    {
        ReleaseConstructionSnapshot displaySnapshot = DisplaySnapshot();
        _phaseLabel.Text = _campaignSnapshot is null
            ? ReleaseKoreanText.Phase(_snapshot.Phase)
            : $"{_campaignSnapshot.Chapter.ActLabel} · {_campaignSnapshot.Chapter.DisplayName} · " +
              (_showEventProjection ? "예고 상황" : ReleaseKoreanText.Phase(_snapshot.Phase));
        _phaseLabel.AccessibilityName = _showEventProjection
            ? $"현재 표시: {_campaignSnapshot?.Chapter.DisplayName} 예고 상황"
            : $"현재 작업: {ReleaseKoreanText.Phase(_snapshot.Phase)}";
        _timeLabel.Text = $"현재 시각 · {ReleaseKoreanText.FormatClock(_snapshot.Minute)}";
        IReadOnlyList<ReleaseLoadSupply> displayedLoads = DisplayedSupplyLoads(displaySnapshot);
        _supplyLabel.Text =
            $"{(_showEventProjection ? "예고 상황 필수 공급" : "전력 공급")} · " +
            $"{ReleaseKoreanText.FormatPower(displayedLoads.Sum(item => item.DeliveredKw))} / " +
            $"{ReleaseKoreanText.FormatPower(displayedLoads.Sum(item => item.DemandKw))}";
        _assetLabel.Text =
            $"완공 설비 {_snapshot.World.Nodes.Count(item => item.Commissioned)}곳 · " +
            $"선로 {_snapshot.World.Edges.Count(item => item.Commissioned)}구간";
        _cashLabel.Visible = _campaignSnapshot is not null;
        _cashLabel.Text = _campaignSnapshot is null
            ? string.Empty
            : $"운영 자금 · {ReleaseKoreanText.FormatCash(_campaignSnapshot.CashUnit)}";

        _map.SetPresentation(new ReleaseMapPresentation(
            displaySnapshot,
            _pointerPoint,
            _pointerAccepted,
            _selectedNodeId,
            _selectedEdgeId,
            ToolDescription()));
        _panel.SetModel(BuildPanelModel(displaySnapshot));
    }

    private ReleaseConstructionSnapshot DisplaySnapshot()
    {
        if (!_showEventProjection || _campaignSnapshot?.Chapter.Event is null)
        {
            return _snapshot;
        }

        return _snapshot with { Evaluation = _campaignSnapshot.EventEvaluation };
    }

    private ReleaseTaskPanelModel BuildPanelModel(ReleaseConstructionSnapshot displaySnapshot)
    {
        bool ready = _snapshot.Phase == ReleaseConstructionPhase.Ready;
        bool nodeDraft = _snapshot.Phase == ReleaseConstructionPhase.NodeDrafting;
        bool lineDraft = _snapshot.Phase == ReleaseConstructionPhase.LineDrafting;
        bool building = _snapshot.Phase is ReleaseConstructionPhase.NodeBuilding or ReleaseConstructionPhase.LineBuilding;
        ReleaseConstructionQuote quote = nodeDraft
            ? PreviewNodeOrder()
            : lineDraft
                ? PreviewLineOrder()
                : new ReleaseConstructionQuote(false, ReleaseConstructionError.WrongPhase, null, null, null);
        string quoteText = building
            ? ActiveConstructionText()
            : quote.Accepted
                ? $"공사비 {ReleaseKoreanText.FormatCash(quote.CostCashUnit!.Value)} · " +
                  $"공사 기간 {ReleaseKoreanText.FormatDuration(quote.BuildMinutes!.Value)} · " +
                  $"완공 예정 {ReleaseKoreanText.FormatClock(quote.CompletionMinute!.Value)}"
                : string.Empty;
        string error = CampaignErrorText();
        ReleaseButtonPresentation toolButton(string text, string description) =>
            new(true, ready || nodeDraft, text, description);
        ReleaseTaskPanelModel model = new(
            ReleaseKoreanText.Phase(_snapshot.Phase),
            Instruction(),
            SelectionText(displaySnapshot),
            NetworkText(displaySnapshot),
            quoteText,
            error,
            toolButton("현황 보기", "설비와 선로의 부하, 정격 용량과 여유 용량을 확인합니다."),
            toolButton("소형 변전소 놓기", "빈 격자에 소형 배전 변전소를 계획합니다."),
            toolButton("대형 변전소 놓기", "빈 격자에 대형 배전 변전소를 계획합니다."),
            toolButton("일반 선로 잇기", "일반 전신주와 배전선으로 완공된 두 설비를 연결합니다."),
            toolButton("보강 선로 잇기", "정격 용량이 큰 전신주와 배전선으로 완공된 두 설비를 연결합니다."),
            new ReleaseButtonPresentation(nodeDraft || lineDraft, true, "계획 취소", "발주하지 않은 현재 계획을 모두 취소합니다."),
            new ReleaseButtonPresentation(lineDraft, _snapshot.LineDraft is { IntermediatePoints.Count: > 0 } || _snapshot.LineDraft?.EndNodeId is not null,
                "한 단계 되돌리기", "선로의 끝 설비 또는 마지막 전신주 선택을 되돌립니다."),
            new ReleaseButtonPresentation(nodeDraft || lineDraft, quote.Accepted, "공사 발주", "표시된 비용과 기간으로 현재 계획을 발주합니다."),
            new ReleaseButtonPresentation(building, building, "완공까지 진행", "완공 예정 시각까지 진행해 현재 공사를 마칩니다."));
        if (_campaignSnapshot is null)
        {
            return model;
        }
        return model with
        {
            Campaign = $"{_campaignSnapshot.Chapter.ActLabel} · {_campaignSnapshot.Chapter.DisplayName}",
            Objective = $"임무 목표 · {_campaignSnapshot.Chapter.Objective}",
            Event = _campaignSnapshot.Chapter.Event is null
                ? string.Empty
                : _showEventProjection
                    ? $"표시 중 · {_campaignSnapshot.Chapter.Event.Story.Title}"
                    : $"예고 상황 · {_campaignSnapshot.Chapter.Event.Story.Title}",
            EventView = new ReleaseButtonPresentation(
                HasEquipmentOutage(_campaignSnapshot.Chapter.Event),
                ready && _tool == Tool.Inspect,
                _showEventProjection ? "평상시 보기" : "예고 상황 보기",
                _showEventProjection
                    ? "현재 임무의 평상시 공급 상태로 돌아갑니다."
                    : "예고된 설비 사용 불가 상황의 공급 경로와 용량을 미리 확인합니다."),
            Evaluate = new ReleaseButtonPresentation(
                ready && !_campaignSnapshot.CampaignComplete,
                ready && !_campaignSnapshot.CampaignComplete,
                _campaignSnapshot.ChapterIndex == _campaignSnapshot.ChapterCount - 1
                    ? "전력망 최종 점검"
                    : "운영 계획 점검",
                "평상시와 임무에서 예고한 조건 모두에서 목표를 충족하는지 확인합니다."),
        };
    }

    private string Instruction()
    {
        if (_showEventProjection)
        {
            return "예고된 상황을 적용한 결과입니다. 사용할 수 없는 설비와 우회 경로의 여유 용량을 확인하세요.";
        }
        if (_snapshot.Phase == ReleaseConstructionPhase.NodeBuilding)
        {
            return "변전소가 공사 중이라 아직 전력망에 연결되지 않았습니다. 현재 공사를 완료하세요.";
        }
        if (_snapshot.Phase == ReleaseConstructionPhase.LineBuilding)
        {
            return "선로와 전신주가 공사 중이라 아직 전기가 흐르지 않습니다. 현재 공사를 완료하세요.";
        }
        if (_snapshot.Phase == ReleaseConstructionPhase.NodeDrafting)
        {
            return "다른 빈 격자를 선택하면 위치를 바꿀 수 있습니다. 비용과 기간을 확인한 뒤 공사를 발주하세요.";
        }
        if (_snapshot.Phase == ReleaseConstructionPhase.LineDrafting)
        {
            return _snapshot.LineDraft?.EndNodeId is null
                ? "빈 격자를 선택하면 전신주가 놓입니다. 다른 완공 설비를 선택해 선로를 연결하세요."
                : "선로가 다른 완공 설비에 연결됐습니다. 비용과 기간을 확인하거나 마지막 선택을 되돌리세요.";
        }
        return _tool switch
        {
            Tool.SmallSubstation or Tool.LargeSubstation => "변전소를 배치할 빈 격자를 선택하세요.",
            Tool.StandardLine or Tool.ReinforcedLine => "선로를 시작할 완공 설비를 먼저 선택하세요.",
            _ => "지도에서 설비나 선로를 선택해 부하, 정격 용량과 여유 용량을 확인하세요.",
        };
    }

    private string SelectionText(ReleaseConstructionSnapshot displaySnapshot)
    {
        if (_selectedNodeId is string nodeId)
        {
            ReleaseNodeDefinition? node = displaySnapshot.World.Nodes.SingleOrDefault(item => item.NodeId == nodeId);
            if (node is not null)
            {
                ReleaseNodeClassDefinition nodeClass = displaySnapshot.World.NodeClasses.Single(item => item.ClassId == node.ClassId);
                ReleaseNodeUsage usage = displaySnapshot.Evaluation.Nodes.Single(item => item.NodeId == node.NodeId);
                string availability = usage.Available ? string.Empty : "현재 사용할 수 없는 설비입니다.\n";
                if (nodeClass.Kind == ReleaseNodeKind.SourceTerminal)
                {
                    ReleaseSourceDefinition? source = displaySnapshot.World.Sources.SingleOrDefault(item =>
                        string.Equals(item.NodeId, node.NodeId, StringComparison.Ordinal));
                    ReleaseSourceUsage? sourceUsage = source is null
                        ? null
                        : displaySnapshot.Evaluation.Sources.Single(item =>
                            string.Equals(item.SourceId, source.SourceId, StringComparison.Ordinal));
                    if (sourceUsage is not null)
                    {
                        return $"{node.DisplayName}\n" +
                               $"{availability}{ReleaseKoreanText.Capacity(sourceUsage.UsedKw, sourceUsage.CapacityKw)}\n" +
                               ReleaseKoreanText.Connections(usage.ConnectionCount, usage.MaxConnections);
                    }
                }
                long rating = usage.RatingKw;
                string capacity = rating > 0
                    ? ReleaseKoreanText.Capacity(usage.UsedKw, rating)
                    : "이 인입점에는 별도의 전력 통과 정격 제한이 없습니다.";
                return $"{node.DisplayName}\n" +
                       $"{availability}{capacity}\n{ReleaseKoreanText.Connections(usage.ConnectionCount, usage.MaxConnections)}";
            }
        }
        if (_selectedEdgeId is string edgeId)
        {
            ReleaseEdgeDefinition? edge = displaySnapshot.World.Edges.SingleOrDefault(item => item.EdgeId == edgeId);
            if (edge is not null)
            {
                ReleaseLineClassDefinition lineClass = displaySnapshot.World.LineClasses.Single(item => item.ClassId == edge.LineClassId);
                ReleaseEdgeUsage usage = displaySnapshot.Evaluation.Edges.Single(item => item.EdgeId == edge.EdgeId);
                string availability = usage.Available ? string.Empty : "현재 사용할 수 없는 선로입니다.\n";
                return $"{lineClass.DisplayName}\n{availability}{ReleaseKoreanText.Capacity(usage.UsedKw, usage.RatingKw)}";
            }
        }
        if (_pointerPoint is ReleasePoint point)
        {
            return $"현재 격자 위치 · 동쪽 {point.X}, 남쪽 {point.Y}";
        }
        return "지도에서 확인할 설비나 선로를 선택하세요.";
    }

    private string NetworkText(ReleaseConstructionSnapshot displaySnapshot)
    {
        IReadOnlyList<ReleaseLoadSupply> requiredLoads = DisplayedSupplyLoads(displaySnapshot);
        ReleaseLoadSupply? failed = requiredLoads.FirstOrDefault(item => item.DeliveredKw < item.DemandKw);
        if (failed is null)
        {
            if (_showEventProjection)
            {
                int restrictedGeneralLoads = displaySnapshot.Evaluation.Loads.Count(item =>
                    !_campaignSnapshot!.Chapter.RequiredEventLoadIds.Contains(item.LoadId, StringComparer.Ordinal) &&
                    item.DeliveredKw < item.DemandKw);
                return restrictedGeneralLoads == 0
                    ? "예고 상황에서도 임무 필수 공급과 일반 수요를 모두 유지하고 있습니다."
                    : $"예고 상황에서도 임무 필수 공급을 유지하고 있습니다. 일반 수요 {restrictedGeneralLoads}곳은 공급이 제한됩니다.";
            }
            return "모든 수요처에 필요한 전력을 공급하고 있습니다. 모든 설비 부하가 정격 용량 이내입니다.";
        }
        string? assetName = AssetDisplayName(failed.Failure.AssetId);
        return ReleaseKoreanText.SupplyFailure(
            failed.DisplayName(displaySnapshot.World),
            failed.Failure,
            assetName,
            _showEventProjection);
    }

    private IReadOnlyList<ReleaseLoadSupply> DisplayedSupplyLoads(
        ReleaseConstructionSnapshot displaySnapshot)
    {
        if (!_showEventProjection || _campaignSnapshot is null)
        {
            return displaySnapshot.Evaluation.Loads;
        }

        HashSet<string> required = _campaignSnapshot.Chapter.RequiredEventLoadIds
            .ToHashSet(StringComparer.Ordinal);
        return displaySnapshot.Evaluation.Loads
            .Where(item => required.Contains(item.LoadId))
            .ToArray();
    }

    private string ActiveConstructionText()
    {
        ReleaseActiveConstructionSnapshot active = _snapshot.ActiveConstruction!;
        return $"공사비 {ReleaseKoreanText.FormatCash(active.CostCashUnit)} · " +
               $"완공 예정 {ReleaseKoreanText.FormatClock(active.CompletionMinute)} · " +
               "완공 전에는 전력망에 연결되지 않습니다.";
    }

    private string ToolDescription() => _tool switch
    {
        Tool.SmallSubstation => "소형 변전소 위치를 정하고 있습니다.",
        Tool.LargeSubstation => "대형 변전소 위치를 정하고 있습니다.",
        Tool.StandardLine => "일반 선로 경로를 계획하고 있습니다.",
        Tool.ReinforcedLine => "보강 선로 경로를 계획하고 있습니다.",
        _ => _showEventProjection
            ? "예고된 상황의 전력망을 살펴보고 있습니다."
            : "전력망을 살펴보고 있습니다.",
    };

    private ReleaseNodePlacementPreview PreviewNodePlacement(
        string nodeClassId,
        ReleasePoint point) =>
        _run?.PreviewNodePlacement(nodeClassId, point)
        ?? _session!.PreviewNodePlacement(nodeClassId, point);

    private ReleaseLinePointPreview PreviewLinePoint(ReleasePoint point) =>
        _run?.PreviewLinePoint(point)
        ?? _session!.PreviewLinePoint(point);

    private ReleaseConstructionQuote PreviewNodeOrder() =>
        _run?.PreviewNodeOrder()
        ?? _session!.PreviewNodeOrder();

    private ReleaseConstructionQuote PreviewLineOrder() =>
        _run?.PreviewLineOrder()
        ?? _session!.PreviewLineOrder();

    private string CampaignErrorText()
    {
        string construction = ReleaseKoreanText.ConstructionError(_lastError ?? _pointerError);
        if (!string.IsNullOrWhiteSpace(construction))
        {
            return construction;
        }
        if (_campaignError is null)
        {
            return string.Empty;
        }
        if (_campaignError == ReleaseCampaignError.InsufficientCash)
        {
            return "공사비를 감당할 운영 자금이 부족합니다. 더 짧은 경로나 저렴한 설비로 계획을 조정하세요.";
        }
        if (_campaignError == ReleaseCampaignError.WrongPhase)
        {
            return "진행 중인 공사를 먼저 마치거나 현재 계획을 취소하세요.";
        }
        if (_campaignError == ReleaseCampaignError.ObjectiveNotMet && _assessment is not null)
        {
            if (_assessment.FailedLoadId is string loadId)
            {
                string loadName = _world.Loads.Single(item => item.LoadId == loadId).DisplayName;
                return ReleaseKoreanText.SupplyFailure(
                    loadName,
                    _assessment.SupplyFailure!,
                    AssetDisplayName(_assessment.SupplyFailure?.AssetId),
                    _assessment.FailedDuringEvent);
            }
            if (_assessment.FailedConnectionNodeId is string nodeId)
            {
                string nodeName = _world.Nodes.Single(item => item.NodeId == nodeId).DisplayName;
                return ReleaseKoreanText.ConnectionRequirement(
                    nodeName,
                    _assessment.ActualConnections!.Value,
                    _assessment.RequiredConnections!.Value);
            }
        }
        return _campaignError == ReleaseCampaignError.CampaignComplete
            ? "모든 임무를 마쳤습니다."
            : "이 작업을 마치지 못했습니다. 진행 중인 계획과 임무 목표를 확인한 뒤 다시 시도하세요.";
    }

    private string NodeClassForTool() => _tool switch
    {
        Tool.SmallSubstation => "SUBSTATION_SMALL",
        Tool.LargeSubstation => "SUBSTATION_LARGE",
        _ => throw new InvalidOperationException("변전소 도구가 선택되지 않았습니다."),
    };

    private string LineClassForTool() => _tool == Tool.StandardLine
        ? "LINE_STANDARD"
        : "LINE_REINFORCED";

    private string PoleClassForTool() => _tool == Tool.StandardLine
        ? "POLE_STANDARD"
        : "POLE_REINFORCED";

    private ReleaseNodeDefinition? NodeAt(ReleasePoint point) =>
        _snapshot.World.Nodes.SingleOrDefault(item => item.Position == point);

    private string? AssetDisplayName(string? assetId)
    {
        if (assetId is null)
        {
            return null;
        }

        string? nodeName = _snapshot.World.Nodes
            .SingleOrDefault(item => item.NodeId == assetId)
            ?.DisplayName;
        if (nodeName is not null)
        {
            return nodeName;
        }

        ReleaseEdgeDefinition? edge = _snapshot.World.Edges
            .SingleOrDefault(item => item.EdgeId == assetId);
        if (edge is not null)
        {
            string from = _snapshot.World.Nodes
                .Single(item => item.NodeId == edge.FromNodeId)
                .DisplayName;
            string to = _snapshot.World.Nodes
                .Single(item => item.NodeId == edge.ToNodeId)
                .DisplayName;
            return $"{from}–{to} 선로";
        }

        ReleaseSourceDefinition? source = _snapshot.World.Sources
            .SingleOrDefault(item => item.SourceId == assetId);
        return source is null
            ? null
            : _snapshot.World.Nodes
                .Single(item => item.NodeId == source.NodeId)
                .DisplayName;
    }

    private static bool HasEquipmentOutage(ReleaseCampaignEvent? campaignEvent) =>
        campaignEvent is not null &&
        (campaignEvent.UnavailableNodeIds.Count > 0 ||
         campaignEvent.UnavailableEdgeIds.Count > 0 ||
         campaignEvent.ActiveRiskAreaIds.Count > 0);

    private void ShowStory(
        string category,
        string title,
        string body,
        string button,
        Action? continuation = null)
    {
        _storyCategory.Text = category;
        _storyTitle.Text = title;
        _storyBody.Text = body;
        _storyButton.Text = button;
        _storyButton.AccessibilityName = button;
        _storyContinuation = continuation;
        _storyOverlay.Show();
        _storyButton.GrabFocus();
    }

    private void HideStory()
    {
        _storyOverlay.Hide();
        Action? continuation = _storyContinuation;
        _storyContinuation = null;
        continuation?.Invoke();
        _map.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void EvaluateCampaignChapter()
    {
        if (_run is null)
        {
            return;
        }
        ReleaseCampaignChapter attemptedChapter = _run.CurrentChapter;
        ReleaseCampaignCommandResult result = _run.Execute(
            new ReleaseCampaignCommand(ReleaseCampaignCommandKind.EvaluateChapter));
        ApplyCampaignResult(result, saveAccepted: true);
        if (result.Accepted && HasEquipmentOutage(attemptedChapter.Event))
        {
            _audio.PlayLive(ReleaseAudioCue.Outage);
        }

        Action presentOutcome = () =>
        {
            if (!result.Accepted)
            {
                string reason = CampaignErrorText();
                string finding = result.Assessment?.FailedDuringEvent == true
                    ? "예고 상황에서 필요한 전력을 모두 공급하지 못했습니다."
                    : "평상시에도 필요한 전력을 모두 공급하지 못했습니다.";
                ShowStory(
                    "운영 점검 결과",
                    "전력망을 더 보강해야 합니다",
                    $"{finding} {reason}",
                    "설계로 돌아가기",
                    Render);
                return;
            }

            Action afterEvent = result.CompletedChapter is not null
                ? () => ShowChapterResult(result.CompletedChapter, result.Snapshot.CampaignComplete)
                : Render;
            if (attemptedChapter.Event is not null)
            {
                ShowStory(
                    attemptedChapter.Event.Story.Speaker,
                    attemptedChapter.Event.Story.Title,
                    attemptedChapter.Event.Story.Body,
                    "결과 보고 보기",
                    afterEvent);
            }
            else
            {
                afterEvent();
            }
        };

        if (_pendingCampaignSaveError is not null)
        {
            _afterPauseContinuation = presentOutcome;
            ShowPendingCampaignSaveError();
            return;
        }

        presentOutcome();
    }

    private void ShowChapterResult(ReleaseCampaignChapter chapter, bool campaignComplete)
    {
        ShowStory(
            chapter.Result.Speaker,
            chapter.Result.Title,
            chapter.Result.Body,
            campaignComplete ? "완성한 전력망 둘러보기" : "다음 브리핑 보기",
            campaignComplete ? null : ShowCurrentBriefing);
    }

    private void ShowCurrentBriefing()
    {
        if (_run is null)
        {
            return;
        }
        ReleaseCampaignChapter chapter = _run.CurrentChapter;
        ShowStory(
            chapter.Briefing.Speaker,
            chapter.Briefing.Title,
            chapter.Briefing.Body,
            "공사 시작하기");
    }

    private void ShowFatal(string message)
    {
        if (_fatalLabel is not null)
        {
            _fatalLabel.Text = message;
            _fatalLabel.Show();
        }
    }

    private void ConfigurePersistencePaths()
    {
        string directory = Path.GetFullPath(
            _options.StorageDirectory ?? ProjectSettings.GlobalizePath("user://"));
        _campaignSavePath = Path.Combine(
            directory,
            ReleaseCampaignPersistenceStore.SaveFileName);
        _legacyCampaignSavePath = Path.Combine(
            directory,
            ProductPersistenceStore.CampaignSaveFileName);
        _settingsPath = Path.Combine(
            directory,
            ProductPersistenceStore.SettingsFileName);
    }

    private void LoadPersistentState()
    {
        var notices = new List<string>();
        ProductSettingsLoadResult settings = ProductPersistenceStore.LoadSettings(_settingsPath);
        _settings = settings.Settings;
        if (settings.Status == ProductDocumentLoadStatus.Invalid)
        {
            notices.Add("설정을 읽지 못해 기본값으로 시작합니다.");
        }

        ReleaseCampaignSaveLoadResult save = ReleaseCampaignPersistenceStore.Load(_campaignSavePath);
        if (save.Status == ReleaseDocumentLoadStatus.Invalid)
        {
            _preserveCurrentSaveBeforeWrite = true;
            notices.Add("저장 파일을 읽지 못해 이어하기를 사용할 수 없습니다. 새 게임은 시작할 수 있습니다.");
        }
        else if (save.Status == ReleaseDocumentLoadStatus.Loaded && save.Save is not null)
        {
            try
            {
                _ = ReleaseCampaignRun.Restore(
                    _campaign,
                    _world,
                    _campaignHash,
                    _fixtureHash,
                    save.Save);
                _continuationSave = save.Save;
            }
            catch (Exception exception) when (
                exception is ReleasePersistenceValidationException or
                ArgumentException or InvalidOperationException or OverflowException)
            {
                _preserveCurrentSaveBeforeWrite = true;
                notices.Add("이 버전과 호환되지 않는 저장 파일입니다. 새 게임은 시작할 수 있습니다.");
            }
        }
        if (File.Exists(_legacyCampaignSavePath))
        {
            notices.Add("이전 버전의 저장 기록은 현재 게임에서 불러올 수 없어 원본을 그대로 보존했습니다.");
        }
        _titleStatus = string.Join('\n', notices);
    }

    private void StartNewCampaign()
    {
        _afterPauseContinuation = null;
        _pendingCampaignSaveError = null;
        _run = new ReleaseCampaignRun(
            _campaign,
            _world,
            _campaignHash,
            _fixtureHash);
        RefreshCampaignSnapshot();
        if (!TryPreserveUnreadableSave(out string preserveError))
        {
            _shell.ShowPersistenceError(preserveError);
            return;
        }
        if (!TrySaveCampaign(out string error))
        {
            _shell.ShowPersistenceError(error);
            return;
        }
        _continuationSave = _run.CaptureSave();
        EnterCampaignGameplay();
    }

    private void ContinueCampaign()
    {
        if (_continuationSave is null)
        {
            _shell.ShowPersistenceError("이어할 저장 파일이 없습니다. 새 게임을 시작하세요.");
            return;
        }
        try
        {
            _afterPauseContinuation = null;
            _pendingCampaignSaveError = null;
            _run = ReleaseCampaignRun.Restore(
                _campaign,
                _world,
                _campaignHash,
                _fixtureHash,
            _continuationSave);
            RefreshCampaignSnapshot();
            EnterCampaignGameplay();
        }
        catch (Exception exception)
        {
            GD.PushWarning(exception.ToString());
            _shell.ShowPersistenceError("저장 파일을 불러오지 못했습니다. 새 게임은 시작할 수 있습니다.");
        }
    }

    private void RefreshCampaignSnapshot()
    {
        _campaignSnapshot = _run!.GetSnapshot();
        _snapshot = _campaignSnapshot.Construction;
        _showEventProjection = false;
        _lastError = null;
        _campaignError = null;
        _assessment = null;
        Render();
    }

    private void ShowPause()
    {
        if (_run is null || _storyOverlay.Visible)
        {
            return;
        }
        _shell.ShowPause(
            _run.CurrentChapter.DisplayName,
            _titleStatus,
            _run.CanRewindToPreviousChapter);
    }

    private void ResumeGameplay()
    {
        _shell.HideShell();
        Action? continuation = _afterPauseContinuation;
        _afterPauseContinuation = null;
        continuation?.Invoke();
    }

    private void ShowPendingCampaignSaveError()
    {
        if (_run is null || _pendingCampaignSaveError is null)
        {
            return;
        }

        string message = _pendingCampaignSaveError;
        _pendingCampaignSaveError = null;
        _shell.ShowPause(
            _run.CurrentChapter.DisplayName,
            message,
            _run.CanRewindToPreviousChapter);
    }

    private void SaveAndReturnToTitle()
    {
        if (!TrySaveCampaign(out string error))
        {
            _shell.ShowPersistenceError(error);
            return;
        }
        _continuationSave = _run!.CaptureSave();
        _afterPauseContinuation = null;
        _shell.ShowTitle(true, "현재 진행 상황을 저장했습니다.");
    }

    private void RestartChapter()
    {
        if (_run is null)
        {
            return;
        }
        _campaignSnapshot = _run.RestartChapter();
        _afterPauseContinuation = null;
        _snapshot = _campaignSnapshot.Construction;
        _showEventProjection = false;
        _lastError = null;
        _campaignError = null;
        _assessment = null;
        if (!TrySaveCampaign(out string error))
        {
            _shell.ShowPause(
                _run.CurrentChapter.DisplayName,
                error,
                _run.CanRewindToPreviousChapter);
            return;
        }
        _continuationSave = _run.CaptureSave();
        _shell.HideShell();
        Render();
        ShowCurrentBriefing();
    }

    private void RewindToPreviousChapter()
    {
        if (_run is null || !_run.CanRewindToPreviousChapter)
        {
            return;
        }

        _campaignSnapshot = _run.RewindToPreviousChapterStart();
        _afterPauseContinuation = null;
        _snapshot = _campaignSnapshot.Construction;
        _showEventProjection = false;
        _lastError = null;
        _campaignError = null;
        _assessment = null;
        if (!TrySaveCampaign(out string error))
        {
            _shell.ShowPause(
                _run.CurrentChapter.DisplayName,
                error,
                _run.CanRewindToPreviousChapter);
            return;
        }
        _continuationSave = _run.CaptureSave();
        _shell.HideShell();
        Render();
        ShowCurrentBriefing();
    }

    private bool TrySaveCampaign(out string error)
    {
        error = string.Empty;
        if (_run is null)
        {
            return true;
        }
        try
        {
            ReleaseCampaignPersistenceStore.Save(_campaignSavePath, _run.CaptureSave());
            _continuationSave = _run.CaptureSave();
            if (_titleStatus.StartsWith("게임을 저장하지 못했습니다.", StringComparison.Ordinal))
            {
                _titleStatus = string.Empty;
            }
            return true;
        }
        catch (Exception exception)
        {
            error = "게임을 저장하지 못했습니다. 마지막으로 저장된 진행 상황은 그대로 보존했습니다. 저장 공간과 파일 권한을 확인하세요.";
            GD.PushWarning($"{error} {exception}");
            return false;
        }
    }

    private bool TryPreserveUnreadableSave(out string error)
    {
        error = string.Empty;
        if (!_preserveCurrentSaveBeforeWrite || !File.Exists(_campaignSavePath))
        {
            return true;
        }

        string backupPath = _campaignSavePath + ".bak";
        if (File.Exists(backupPath))
        {
            backupPath += ".1";
        }
        if (File.Exists(backupPath))
        {
            error = "기존 저장 파일을 안전하게 보존할 자리를 만들지 못했습니다. 저장 폴더를 확인하세요.";
            return false;
        }

        try
        {
            File.Copy(_campaignSavePath, backupPath, overwrite: false);
            _preserveCurrentSaveBeforeWrite = false;
            _titleStatus = "읽지 못한 저장 파일을 백업한 뒤 새 게임을 시작했습니다.";
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning(exception.ToString());
            error = "기존 저장 파일을 백업하지 못했습니다. 저장 공간과 파일 권한을 확인하세요.";
            return false;
        }
    }

    private void EnterCampaignGameplay()
    {
        if (_campaignSnapshot?.CampaignComplete == true)
        {
            ShowChapterResult(_campaignSnapshot.Chapter, campaignComplete: true);
            _shell.HideShell();
            return;
        }

        ShowCurrentBriefing();
        if (_settings.ShowControlHelp)
        {
            _shell.ShowControlHelpBeforeGameplay();
        }
        else
        {
            _shell.HideShell();
        }
    }

    private void OnFullscreenChanged(bool fullscreen)
    {
        _settings = _settings with
        {
            WindowMode = fullscreen ? ProductWindowMode.Fullscreen : ProductWindowMode.Windowed,
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
        SaveSettings();
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

    private void ApplySettings(ProductSettings settings)
    {
        GetWindow().Mode = settings.WindowMode == ProductWindowMode.Fullscreen
            ? Window.ModeEnum.Fullscreen
            : Window.ModeEnum.Windowed;
        GetWindow().ContentScaleFactor = 1f;
        ApplyUiScale(this, settings.UiScalePercent / 100f);
        _audio.ApplyVolumes(
            settings.MasterVolumePercent,
            settings.AmbientVolumePercent,
            settings.SfxVolumePercent);
        _shell.SetSettings(
            settings.WindowMode == ProductWindowMode.Fullscreen,
            settings.UiScalePercent,
            settings.ShowControlHelp,
            settings.MasterVolumePercent,
            settings.AmbientVolumePercent,
            settings.SfxVolumePercent);
    }

    private void ApplyUiScale(Node node, float scale)
    {
        if (node is Control control && (control is Label || control is BaseButton))
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
            ApplyUiScale(child, scale);
        }
    }

    private void SaveSettings()
    {
        try
        {
            ProductPersistenceStore.SaveSettings(_settingsPath, _settings);
            _shell.ShowSettingsMessage(string.Empty);
            if (_titleStatus.StartsWith("설정을 저장하지 못했습니다.", StringComparison.Ordinal))
            {
                _titleStatus = string.Empty;
            }
        }
        catch (Exception exception)
        {
            _titleStatus = "설정을 저장하지 못했습니다. 기존 설정 파일은 그대로 보존했습니다.";
            _shell.ShowSettingsMessage(
                "설정을 저장하지 못했습니다. 변경 사항은 이번 실행에만 적용되며 기존 설정 파일은 그대로 보존했습니다. 저장 공간과 파일 권한을 확인하세요.");
            GD.PushWarning($"{_titleStatus} {exception}");
        }
    }

    private async void RunCampaignSmokeSave()
    {
        try
        {
            await NextFrame();
            EmitShell(ReleaseShellAction.NewGame, "새 게임");
            await NextFrame();
            await CloseControlHelpIfShown();
            await CloseStory("첫 임무 브리핑");

            await ClickMap(new ReleasePoint(2, 10));
            string sourceInspection = SelectionText(DisplaySnapshot());
            Require(
                sourceInspection.Contains("부하", StringComparison.Ordinal) &&
                sourceInspection.Contains("정격 용량", StringComparison.Ordinal) &&
                sourceInspection.Contains("여유 용량", StringComparison.Ordinal),
                "발전 접속점에 공급 부하와 여유 용량이 표시되지 않았습니다.");

            await BuildCampaignLine("CENTRAL_JUNCTION", false,
                new ReleasePoint(9, 13));
            await BuildCampaignLine("SOUTH_JUNCTION", false,
                new ReleasePoint(13, 10));
            await BuildCampaignLine("RIVER_MERGE", false,
                new ReleasePoint(17, 10));
            await CompleteCampaignChapter("PROLOGUE_FIRST_LIGHT");

            EmitPanel(ReleasePanelAction.Inspect, "전력망 살펴보기");
            await NextFrame();
            EmitPanel(ReleasePanelAction.ToggleEventView, "예고 상황 보기");
            await NextFrame();
            Require(
                _showEventProjection &&
                DisplaySnapshot().Evaluation.Edges.Count(item => !item.Available) >
                _campaignSnapshot!.NormalEvaluation.Edges.Count(item => !item.Available),
                "예고 상황 보기가 사용 불가 선로를 표시하지 않았습니다.");
            EmitPanel(ReleasePanelAction.ToggleEventView, "평상시 보기");
            await NextFrame();
            Require(!_showEventProjection, "평상시 전력망 표시로 돌아오지 못했습니다.");

            await BuildCampaignLine("EAST_SUBSTATION", false,
                new ReleasePoint(17, 7),
                new ReleasePoint(17, 5));
            await BuildCampaignLine("CENTRAL_JUNCTION", false,
                new ReleasePoint(9, 7));
            await BuildCampaignLine("NORTH_JUNCTION", false,
                new ReleasePoint(13, 5));
            await CompleteCampaignChapter("PROLOGUE_SECOND_HEART");

            await BuildCampaignLine("SOUTH_SOURCE_NODE", false,
                new ReleasePoint(13, 15));
            await BuildCampaignLine("SOUTH_SUBSTATION", false,
                new ReleasePoint(9, 13));
            await CompleteCampaignChapter("PROLOGUE_HEAT_DOME");

            await BuildCampaignLine("CENTRAL_JUNCTION", true,
                new ReleasePoint(6, 6));
            await CompleteCampaignChapter("CHAPTER_NORTH_BANK");

            EmitButton(_menuButton, "메뉴 열기");
            await NextFrame();
            EmitShell(ReleaseShellAction.PauseSettings, "설정 열기");
            await NextFrame();
            SelectOptionById(_shell.GetSfxVolumeOption(), 50);
            await NextFrame();
            Require(_settings.SfxVolumePercent == 50,
                "효과음 50% 설정이 현재 상태에 적용되지 않았습니다.");
            EmitShell(ReleaseShellAction.SettingsBack, "설정 닫기");
            await NextFrame();
            EmitShell(ReleaseShellAction.SaveAndQuit, "저장하고 타이틀로");
            await NextFrame();
            Require(_shell.Page == ReleaseShellPage.Title && File.Exists(_campaignSavePath),
                "캠페인 저장 뒤 타이틀로 돌아오지 못했습니다.");
            Require(_run!.CurrentChapter.ChapterId == "CHAPTER_SHARED_CORRIDOR",
                "저장 경계가 본편 2장에 있지 않습니다.");
            GD.Print(
                $"RELEASE_CAMPAIGN_SAVE_SMOKE_PASS session={_options.SessionId} " +
                $"chapter={_run.CurrentChapter.ChapterId} commands={_run.GetSnapshot().CommandCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"출시판 캠페인 저장 smoke 실패: {exception}");
            GetTree().Quit(1);
        }
    }

    private async void RunCampaignSmokeContinue()
    {
        try
        {
            await NextFrame();
            EmitShell(ReleaseShellAction.Continue, "이어하기");
            await NextFrame();
            int sfxBus = AudioServer.GetBusIndex("SFX");
            Require(
                _settings.SfxVolumePercent == 50 &&
                sfxBus >= 0 &&
                !AudioServer.IsBusMute(sfxBus) &&
                Mathf.IsEqualApprox(AudioServer.GetBusVolumeLinear(sfxBus), 0.5f),
                "새 프로세스에서 효과음 50% 설정과 실제 SFX 버스 음량을 복원하지 못했습니다.");
            await CloseControlHelpIfShown();
            await CloseStory("이어온 임무 브리핑");
            Require(_run!.CurrentChapter.ChapterId == "CHAPTER_SHARED_CORRIDOR",
                "이어하기가 저장된 장을 복원하지 못했습니다.");

            int openingEdges = _snapshot.World.Edges.Count;
            await BuildCampaignLine("NORTH_JUNCTION", false,
                new ReleasePoint(13, 10));
            EmitButton(_menuButton, "메뉴 열기");
            await NextFrame();
            EmitShell(ReleaseShellAction.RestartChapter, "현재 임무 다시 시작");
            await NextFrame();
            EmitShell(ReleaseShellAction.Confirm, "재시작 확인");
            await NextFrame();
            await CloseStory("재시작한 임무 브리핑");
            Require(_snapshot.World.Edges.Count == openingEdges,
                "현재 임무 재시작이 이번 장의 공사를 되돌리지 못했습니다.");

            await BuildCampaignLine("WEST_SOURCE_NODE", false,
                new ReleasePoint(5, 12),
                new ReleasePoint(9, 13));
            await CompleteCampaignChapter("CHAPTER_SHARED_CORRIDOR");

            await BuildCampaignLine("NORTH_SUBSTATION", false,
                new ReleasePoint(16, 6),
                new ReleasePoint(17, 10));
            await CompleteCampaignChapter("CHAPTER_FLOOD_FORECAST");

            await BuildCampaignLine("SOUTH_SUBSTATION", false,
                new ReleasePoint(16, 14),
                new ReleasePoint(17, 10));
            await CompleteCampaignChapter("CHAPTER_PLANNED_OUTAGE");

            await BuildCampaignLine("SOUTH_SUBSTATION", false,
                new ReleasePoint(17, 13),
                new ReleasePoint(19, 9),
                new ReleasePoint(17, 5));
            await CompleteCampaignChapter("CHAPTER_EMERGENCY_POWER");

            Require(_campaignSnapshot!.CampaignComplete,
                "8개 임무를 마친 뒤 캠페인이 완료되지 않았습니다.");
            GD.Print(
                $"RELEASE_CAMPAIGN_COMPLETE_SMOKE_PASS session={_options.SessionId} " +
                $"cash={_campaignSnapshot.CashUnit} minute={_snapshot.Minute} " +
                $"nodes={_snapshot.World.Nodes.Count} edges={_snapshot.World.Edges.Count}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"출시판 캠페인 이어하기 smoke 실패: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task BuildCampaignLine(
        string startNodeId,
        bool standard,
        params ReleasePoint[] remainingPoints)
    {
        _tool = Tool.Inspect;
        ReleaseNodeDefinition start = _snapshot.World.Nodes.Single(item => item.NodeId == startNodeId);
        EmitPanel(
            standard ? ReleasePanelAction.StandardLine : ReleasePanelAction.ReinforcedLine,
            standard ? "일반 선로 도구" : "보강 선로 도구");
        await NextFrame();
        await ClickMap(start.Position);
        foreach (ReleasePoint point in remainingPoints)
        {
            await ClickMap(point);
        }
        Require(_snapshot.LineDraft?.EndNodeId is not null,
            $"{start.DisplayName}에서 시작한 선로가 완공 설비에 닿지 않았습니다.");
        EmitPanel(ReleasePanelAction.Order, "선로 공사 발주");
        await NextFrame();
        EmitPanel(ReleasePanelAction.Advance, "선로 공사 마치기");
        await NextFrame();
        Require(_snapshot.Phase == ReleaseConstructionPhase.Ready,
            "선로 공사가 완료되지 않았습니다.");
    }

    private async Task CompleteCampaignChapter(string chapterId)
    {
        Require(_run!.CurrentChapter.ChapterId == chapterId,
            $"예상한 임무가 아닙니다: {chapterId}");
        bool final = _campaignSnapshot!.ChapterIndex == _campaignSnapshot.ChapterCount - 1;
        EmitPanel(ReleasePanelAction.Evaluate, "임무 결과 확인");
        await NextFrame();
        Require(_campaignError is null && _storyOverlay.Visible,
            $"{chapterId} 목표를 충족하지 못했습니다. {CampaignErrorText()}");
        await CloseStory("사건 알림");
        await CloseStory("임무 결과");
        if (!final)
        {
            await CloseStory("다음 임무 브리핑");
        }
    }

    private async Task CloseStory(string description)
    {
        Require(_storyOverlay.Visible, $"{description} 카드가 표시되지 않았습니다.");
        EmitButton(_storyButton, description);
        await NextFrame();
    }

    private async Task CloseControlHelpIfShown()
    {
        if (_shell.Page != ReleaseShellPage.Help)
        {
            return;
        }
        EmitShell(ReleaseShellAction.HelpBack, "조작 도움말 닫기");
        await NextFrame();
    }

    private void EmitShell(ReleaseShellAction action, string description) =>
        EmitButton(_shell.GetActionButton(action), description);

    private static void SelectOptionById(OptionButton option, int itemId)
    {
        int index = Enumerable.Range(0, option.ItemCount)
            .Single(item => option.GetItemId(item) == itemId);
        option.Select(index);
        option.EmitSignal(OptionButton.SignalName.ItemSelected, (long)index);
    }

    private async void RunSmoke()
    {
        try
        {
            await NextFrame();
            EmitButton(_storyButton, "브리핑 닫기");
            await NextFrame();

            ReleasePoint substation = _options.SmokeSubstation!.Value;
            EmitPanel(ReleasePanelAction.SmallSubstation, "소형 변전소 도구");
            await NextFrame();
            await ClickMap(new ReleasePoint(substation.X - 1, substation.Y));
            await ClickMap(substation);
            Require(_snapshot.NodeDraft?.Position == substation, "변전소 계획 좌표가 입력과 다릅니다.");
            EmitPanel(ReleasePanelAction.Cancel, "변전소 계획 취소");
            await NextFrame();
            EmitPanel(ReleasePanelAction.SmallSubstation, "소형 변전소 도구 다시 선택");
            await NextFrame();
            await ClickMap(substation);
            EmitPanel(ReleasePanelAction.Order, "변전소 공사 발주");
            await NextFrame();
            Require(_snapshot.Phase == ReleaseConstructionPhase.NodeBuilding,
                "변전소 공사가 시작되지 않았습니다.");
            EmitPanel(ReleasePanelAction.Advance, "변전소 공사 마치기");
            await NextFrame();
            Require(_snapshot.World.Nodes.Single(item => item.Position == substation).Commissioned,
                "변전소가 완공되지 않았습니다.");
            EmitButton(_storyButton, "변전소 완공 알림 닫기");
            await NextFrame();

            EmitPanel(ReleasePanelAction.ReinforcedLine, "보강 선로 도구");
            await NextFrame();
            await ClickMap(_options.SmokeLineStart!.Value);
            foreach (ReleasePoint point in _options.SmokeLinePoints)
            {
                await ClickMap(point);
            }
            await ClickMap(_options.SmokeLineEnd!.Value);
            Require(_snapshot.LineDraft is not null &&
                    _snapshot.LineDraft.IntermediatePoints.SequenceEqual(_options.SmokeLinePoints) &&
                    NodeAt(_options.SmokeLineEnd.Value)?.NodeId == _snapshot.LineDraft.EndNodeId,
                "선로 계획 좌표가 실제 지도 입력과 다릅니다.");
            EmitPanel(ReleasePanelAction.Undo, "선로 끝 되돌리기");
            await NextFrame();
            await ClickMap(_options.SmokeLineEnd.Value);
            EmitPanel(ReleasePanelAction.Order, "선로 공사 발주");
            await NextFrame();
            Require(_snapshot.Phase == ReleaseConstructionPhase.LineBuilding &&
                    _snapshot.ActiveConstruction!.EdgeIds.All(id =>
                        !_snapshot.World.Edges.Single(item => item.EdgeId == id).Commissioned),
                "선로가 발주 즉시 완공됐습니다.");
            EmitPanel(ReleasePanelAction.Advance, "선로 공사 마치기");
            await NextFrame();
            Require(_snapshot.Phase == ReleaseConstructionPhase.Ready &&
                    _snapshot.Evaluation.Nodes.Single(item => item.NodeId == "SOUTH_JUNCTION").ConnectionCount == 4 &&
                    _snapshot.Evaluation.Nodes.Single(item => item.NodeId == "EAST_SUBSTATION").ConnectionCount == 2,
                "분기·합류 선로가 완공되지 않았습니다.");
            Require(_storyOverlay.Visible, "공사 결과 카드가 표시되지 않았습니다.");
            EmitButton(_storyButton, "공사 결과 닫기");
            await NextFrame();
            await PressMapKey(Key.Right);
            await PressMapKey(Key.Enter);
            Require(_selectedNodeId is null && _selectedEdgeId is null,
                "키보드 선택이 이전 마우스 hover 설비를 다시 선택했습니다.");

            GD.Print(
                $"RELEASE_CONSTRUCTION_SMOKE_PASS session={_options.SessionId} " +
                $"minute={_snapshot.Minute} nodes={_snapshot.World.Nodes.Count} edges={_snapshot.World.Edges.Count}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"출시판 공사 smoke 실패: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task ClickMap(ReleasePoint point)
    {
        Vector2 viewportPoint = _map.ViewportPointForGridPoint(point);
        GetViewport().PushInput(new InputEventMouseMotion
        {
            Position = viewportPoint,
            GlobalPosition = viewportPoint,
        }, true);
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = viewportPoint,
            GlobalPosition = viewportPoint,
            ButtonIndex = MouseButton.Left,
            Pressed = true,
        }, true);
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = viewportPoint,
            GlobalPosition = viewportPoint,
            ButtonIndex = MouseButton.Left,
            Pressed = false,
        }, true);
        await NextFrame();
    }

    private async Task PressMapKey(Key key)
    {
        GetViewport().PushInput(new InputEventKey
        {
            Keycode = key,
            Pressed = true,
        }, true);
        GetViewport().PushInput(new InputEventKey
        {
            Keycode = key,
            Pressed = false,
        }, true);
        await NextFrame();
    }

    private void EmitPanel(ReleasePanelAction action, string description) =>
        EmitButton(_panel.GetActionButton(action), description);

    private static void EmitButton(BaseButton button, string description)
    {
        if (!button.Visible || button.Disabled)
        {
            throw new InvalidOperationException($"필요한 버튼을 사용할 수 없습니다: {description}");
        }
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private async Task NextFrame() =>
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

internal static class ReleaseLoadSupplyPresentation
{
    public static string DisplayName(
        this ReleaseLoadSupply supply,
        ReleaseWorldDefinition world) =>
        world.Loads.Single(item => item.LoadId == supply.LoadId).DisplayName;
}
