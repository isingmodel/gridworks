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
    private string _settingsPath = string.Empty;
    private ReleaseCampaignSave? _continuationSave;
    private ProductSettings _settings = ProductSettings.Default;
    private string _titleStatus = string.Empty;
    private ReleaseCampaignError? _campaignError;
    private ReleaseChapterAssessment? _assessment;
    private Action? _storyContinuation;

    private Label _phaseLabel = null!;
    private Label _timeLabel = null!;
    private Label _supplyLabel = null!;
    private Label _assetLabel = null!;
    private Label _cashLabel = null!;
    private Button _menuButton = null!;
    private ReleaseMapView _map = null!;
    private ReleaseTaskPanel _panel = null!;
    private ReleaseShellOverlay _shell = null!;
    private Control _storyOverlay = null!;
    private Label _storyCategory = null!;
    private Label _storyTitle = null!;
    private Label _storyBody = null!;
    private Button _storyButton = null!;
    private Label _fatalLabel = null!;

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
                    "동부권 확장 사전공사",
                    "동부권 수요 증가에 대비해 새 변전소 부지를 확보하고 남부 분기점에서 동부 변전소로 우회선을 이으세요. " +
                    "완공 전 설비는 공급에 참여하지 않으며, 분기점과 변전소의 접속 여유를 함께 확인해야 합니다.",
                    "지도 확인하기");
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
        _shell.ResumeRequested += _shell.HideShell;
        _shell.SaveAndQuitRequested += SaveAndReturnToTitle;
        _shell.RestartChapterRequested += RestartChapter;
        _shell.FullscreenChanged += OnFullscreenChanged;
        _shell.UiScalePercentChanged += OnUiScalePercentChanged;
        _shell.MasterVolumePercentChanged += value => UpdateVolume(master: value);
        _shell.AmbientVolumePercentChanged += value => UpdateVolume(ambient: value);
        _shell.SfxVolumePercentChanged += value => UpdateVolume(sfx: value);
        _shell.ControlHelpChanged += OnControlHelpChanged;
        _shell.GameplayFocusRequested += () => _map.CallDeferred(Control.MethodName.GrabFocus);
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
                _tool = Tool.SmallSubstation;
                _lastError = null;
                break;
            case ReleasePanelAction.LargeSubstation:
                _tool = Tool.LargeSubstation;
                _lastError = null;
                break;
            case ReleasePanelAction.StandardLine:
                _tool = Tool.StandardLine;
                _lastError = null;
                break;
            case ReleasePanelAction.ReinforcedLine:
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
                ExecuteMutation(
                    new ReleaseCampaignCommand(orderNode
                        ? ReleaseCampaignCommandKind.OrderNode
                        : ReleaseCampaignCommandKind.OrderLine),
                    () => orderNode
                        ? _session!.OrderNode()
                        : _session!.OrderLine());
                break;
            case ReleasePanelAction.Advance:
                ReleaseConstructionKind? kind = _snapshot.ActiveConstruction?.Kind;
                ExecuteMutation(
                    new ReleaseCampaignCommand(ReleaseCampaignCommandKind.AdvanceConstruction),
                    () => _session!.AdvanceToConstructionCompletion());
                if (_run is null && _lastError is null && kind == ReleaseConstructionKind.Node)
                {
                    _tool = Tool.Inspect;
                    ShowStory(
                        "운영 알림",
                        "변전소 공사가 끝났습니다",
                        "새 변전소는 완공됐지만 아직 선로와 이어지지 않았습니다. 공급 경로에는 변화가 없습니다. " +
                        "이제 남부 분기점에서 동부 변전소로 우회선을 이으세요.",
                        "선로 계획하기",
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
                        "남부 분기점은 네 구간을 연결하고 동부 변전소는 두 경로로 이어집니다. " +
                        "지도에서 설비를 선택해 현재 사용량과 남은 정격, 접속 여유를 확인하세요.",
                        "결과 확인하기");
                }
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

    private void ExecuteMutation(
        ReleaseCampaignCommand command,
        Func<ReleaseConstructionCommandResult> legacy)
    {
        if (_run is null)
        {
            Apply(legacy());
            return;
        }

        ApplyCampaignResult(_run.Execute(command), saveAccepted: true);
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
            if (saveAccepted && !TrySaveCampaign(out string saveError))
            {
                _titleStatus = saveError;
            }
        }
        Render();
    }

    private void Render()
    {
        _phaseLabel.Text = _campaignSnapshot is null
            ? ReleaseKoreanText.Phase(_snapshot.Phase)
            : $"{_campaignSnapshot.Chapter.ActLabel} · {_campaignSnapshot.Chapter.DisplayName} · " +
              ReleaseKoreanText.Phase(_snapshot.Phase);
        _phaseLabel.AccessibilityName = $"현재 작업 {ReleaseKoreanText.Phase(_snapshot.Phase)}";
        _timeLabel.Text = $"현재 시각 · {ReleaseKoreanText.FormatClock(_snapshot.Minute)}";
        _supplyLabel.Text =
            $"공급 중 · {ReleaseKoreanText.FormatPower(_snapshot.Evaluation.TotalDeliveredKw)} / " +
            $"{ReleaseKoreanText.FormatPower(_snapshot.World.Loads.Sum(item => item.DemandKw))}";
        _assetLabel.Text =
            $"완공 설비 {_snapshot.World.Nodes.Count(item => item.Commissioned)}곳 · " +
            $"선로 {_snapshot.World.Edges.Count(item => item.Commissioned)}구간";
        _cashLabel.Visible = _campaignSnapshot is not null;
        _cashLabel.Text = _campaignSnapshot is null
            ? string.Empty
            : $"운영 자금 · {ReleaseKoreanText.FormatCash(_campaignSnapshot.CashUnit)}";

        _map.SetPresentation(new ReleaseMapPresentation(
            _snapshot,
            _pointerPoint,
            _pointerAccepted,
            _selectedNodeId,
            _selectedEdgeId,
            ToolDescription()));
        _panel.SetModel(BuildPanelModel());
    }

    private ReleaseTaskPanelModel BuildPanelModel()
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
                  $"공사기간 {ReleaseKoreanText.FormatDuration(quote.BuildMinutes!.Value)} · " +
                  $"완공 예정 {ReleaseKoreanText.FormatClock(quote.CompletionMinute!.Value)}"
                : string.Empty;
        string error = CampaignErrorText();
        ReleaseButtonPresentation toolButton(string text, string description) =>
            new(true, ready || nodeDraft, text, description);
        ReleaseTaskPanelModel model = new(
            ReleaseKoreanText.Phase(_snapshot.Phase),
            Instruction(),
            SelectionText(),
            NetworkText(),
            quoteText,
            error,
            toolButton("망 살펴보기", "설비와 선로의 현재 사용량과 정격을 확인합니다."),
            toolButton("소형 변전소 놓기", "빈 격자에 소형 배전 변전소 계획을 놓습니다."),
            toolButton("대형 변전소 놓기", "빈 격자에 대형 배전 변전소 계획을 놓습니다."),
            toolButton("일반 선로 잇기", "일반 전신주와 일반 배전선으로 두 완공 설비를 잇습니다."),
            toolButton("보강 선로 잇기", "보강 전신주와 보강 배전선으로 두 완공 설비를 잇습니다."),
            new ReleaseButtonPresentation(nodeDraft || lineDraft, true, "계획 취소", "현재 계획 전체를 취소합니다."),
            new ReleaseButtonPresentation(lineDraft, _snapshot.LineDraft is { IntermediatePoints.Count: > 0 } || _snapshot.LineDraft?.EndNodeId is not null,
                "마지막 전신주 되돌리기", "선로의 끝 접속점 또는 마지막 전신주를 되돌립니다."),
            new ReleaseButtonPresentation(nodeDraft || lineDraft, quote.Accepted, "공사 발주", "표시된 비용과 공사기간으로 현재 계획을 발주합니다."),
            new ReleaseButtonPresentation(building, building, "공사 마치기", "표시된 완공 시각까지 진행해 공사를 한꺼번에 마칩니다."));
        if (_campaignSnapshot is null)
        {
            return model;
        }
        return model with
        {
            Campaign = $"{_campaignSnapshot.Chapter.ActLabel} · {_campaignSnapshot.Chapter.DisplayName}",
            Objective = $"이번 임무 · {_campaignSnapshot.Chapter.Objective}",
            Event = _campaignSnapshot.Chapter.Event is null
                ? string.Empty
                : $"예고 · {_campaignSnapshot.Chapter.Event.Story.Title}",
            Evaluate = new ReleaseButtonPresentation(
                ready,
                ready,
                _campaignSnapshot.ChapterIndex == _campaignSnapshot.ChapterCount - 1
                    ? "도시 운영 결과 확인"
                    : "임무 결과 확인",
                "현재 공급과 예고된 사고 조건에서 임무 목표를 확인합니다."),
        };
    }

    private string Instruction()
    {
        if (_snapshot.Phase == ReleaseConstructionPhase.NodeBuilding)
        {
            return "변전소는 공사 중이며 아직 공급에 참여하지 않습니다. 공사를 마치세요.";
        }
        if (_snapshot.Phase == ReleaseConstructionPhase.LineBuilding)
        {
            return "선로와 전신주는 공사 중이며 아직 전기가 흐르지 않습니다. 공사를 마치세요.";
        }
        if (_snapshot.Phase == ReleaseConstructionPhase.NodeDrafting)
        {
            return "다른 빈 격자점을 선택하면 계획을 옮길 수 있습니다. 견적을 확인한 뒤 공사를 발주하세요.";
        }
        if (_snapshot.Phase == ReleaseConstructionPhase.LineDrafting)
        {
            return _snapshot.LineDraft?.EndNodeId is null
                ? "빈 격자에는 전신주가 놓입니다. 다른 완공 설비를 선택하면 선로 계획이 끝납니다."
                : "선로가 다른 완공 설비에 닿았습니다. 견적을 확인하거나 마지막 선택을 되돌리세요.";
        }
        return _tool switch
        {
            Tool.SmallSubstation or Tool.LargeSubstation => "변전소를 놓을 빈 격자점을 선택하세요.",
            Tool.StandardLine or Tool.ReinforcedLine => "선로를 시작할 완공 설비를 먼저 선택하세요.",
            _ => "지도에서 설비나 선로를 선택해 사용량, 정격과 남은 여유를 확인하세요.",
        };
    }

    private string SelectionText()
    {
        if (_selectedNodeId is string nodeId)
        {
            ReleaseNodeDefinition? node = _snapshot.World.Nodes.SingleOrDefault(item => item.NodeId == nodeId);
            if (node is not null)
            {
                ReleaseNodeClassDefinition nodeClass = _snapshot.World.NodeClasses.Single(item => item.ClassId == node.ClassId);
                ReleaseNodeUsage usage = _snapshot.Evaluation.Nodes.Single(item => item.NodeId == node.NodeId);
                long rating = usage.RatingKw;
                string capacity = rating > 0
                    ? $"현재 사용 {ReleaseKoreanText.FormatPower(usage.UsedKw)} / 정격 {ReleaseKoreanText.FormatPower(rating)} · " +
                      $"남은 여유 {ReleaseKoreanText.FormatPower(rating - usage.UsedKw)}"
                    : "별도 통과 정격이 없는 접속점";
                return $"{node.DisplayName} · {ReleaseKoreanText.NodeKind(nodeClass.Kind)}\n" +
                       $"{capacity}\n접속 {usage.ConnectionCount} / {usage.MaxConnections}";
            }
        }
        if (_selectedEdgeId is string edgeId)
        {
            ReleaseEdgeDefinition? edge = _snapshot.World.Edges.SingleOrDefault(item => item.EdgeId == edgeId);
            if (edge is not null)
            {
                ReleaseLineClassDefinition lineClass = _snapshot.World.LineClasses.Single(item => item.ClassId == edge.LineClassId);
                ReleaseEdgeUsage usage = _snapshot.Evaluation.Edges.Single(item => item.EdgeId == edge.EdgeId);
                return $"{lineClass.DisplayName}\n현재 사용 {ReleaseKoreanText.FormatPower(usage.UsedKw)} / " +
                       $"정격 {ReleaseKoreanText.FormatPower(usage.RatingKw)} · " +
                       $"남은 여유 {ReleaseKoreanText.FormatPower(usage.RatingKw - usage.UsedKw)}";
            }
        }
        if (_pointerPoint is ReleasePoint point)
        {
            return $"가리킨 격자 · 동쪽 {point.X}, 남쪽 {point.Y}";
        }
        return "선택한 설비가 없습니다.";
    }

    private string NetworkText()
    {
        ReleaseLoadSupply? failed = _snapshot.Evaluation.Loads.FirstOrDefault(item => item.DeliveredKw == 0);
        if (failed is null)
        {
            return "현재 수요처는 모두 정격 안에서 공급 중입니다.";
        }
        string? assetName = AssetDisplayName(failed.Failure.AssetId);
        return $"{failed.DisplayName(_snapshot.World)} · {ReleaseKoreanText.SupplyFailure(failed.Failure, assetName)}";
    }

    private string ActiveConstructionText()
    {
        ReleaseActiveConstructionSnapshot active = _snapshot.ActiveConstruction!;
        return $"공사비 {ReleaseKoreanText.FormatCash(active.CostCashUnit)} · " +
               $"완공 예정 {ReleaseKoreanText.FormatClock(active.CompletionMinute)} · 완공 전 공급 제외";
    }

    private string ToolDescription() => _tool switch
    {
        Tool.SmallSubstation => "소형 변전소를 놓는 중입니다.",
        Tool.LargeSubstation => "대형 변전소를 놓는 중입니다.",
        Tool.StandardLine => "일반 선로를 잇는 중입니다.",
        Tool.ReinforcedLine => "보강 선로를 잇는 중입니다.",
        _ => "망을 살펴보는 중입니다.",
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
            return "운영 자금이 부족합니다. 더 저렴한 경로나 설비 형식을 선택하세요.";
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
                string cause = ReleaseKoreanText.SupplyFailure(
                    _assessment.SupplyFailure!,
                    AssetDisplayName(_assessment.SupplyFailure?.AssetId));
                return $"{(_assessment.FailedDuringEvent ? "예고된 사고에서 " : string.Empty)}" +
                       $"{loadName}에 필요한 전력을 보내지 못했습니다. {cause}";
            }
            if (_assessment.FailedConnectionNodeId is string nodeId)
            {
                string nodeName = _world.Nodes.Single(item => item.NodeId == nodeId).DisplayName;
                return $"{nodeName}의 연결은 {_assessment.ActualConnections}개입니다. " +
                       $"이 임무에는 {_assessment.RequiredConnections}개 이상이 필요합니다.";
            }
        }
        return _campaignError == ReleaseCampaignError.CampaignComplete
            ? "모든 임무를 마쳤습니다."
            : "현재 작업을 완료하지 못했습니다. 선택과 목표를 다시 확인하세요.";
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
            return _snapshot.World.LineClasses
                .Single(item => item.ClassId == edge.LineClassId)
                .DisplayName;
        }

        return _snapshot.World.Sources
            .SingleOrDefault(item => item.SourceId == assetId)
            ?.DisplayName;
    }

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

        Action afterEvent = result.Accepted && result.CompletedChapter is not null
            ? () => ShowChapterResult(result.CompletedChapter, result.Snapshot.CampaignComplete)
            : Render;
        if (attemptedChapter.Event is not null)
        {
            ShowStory(
                attemptedChapter.Event.Story.Speaker,
                attemptedChapter.Event.Story.Title,
                attemptedChapter.Event.Story.Body,
                result.Accepted ? "결과 보기" : "망 보강하기",
                afterEvent);
        }
        else
        {
            afterEvent();
        }
    }

    private void ShowChapterResult(ReleaseCampaignChapter chapter, bool campaignComplete)
    {
        ShowStory(
            chapter.Result.Speaker,
            chapter.Result.Title,
            chapter.Result.Body,
            campaignComplete ? "완성된 전력망 보기" : "다음 임무 보기",
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
            chapter.Briefing.Body + "\n\n" + chapter.Objective,
            "지도 확인하기");
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
            notices.Add("화면 설정을 읽지 못해 기본값으로 시작합니다.");
        }

        ReleaseCampaignSaveLoadResult save = ReleaseCampaignPersistenceStore.Load(_campaignSavePath);
        if (save.Status == ReleaseDocumentLoadStatus.Invalid)
        {
            notices.Add("저장 기록이 손상되어 이어하기를 사용할 수 없습니다.");
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
                notices.Add("현재 캠페인과 맞지 않는 저장이라 이어하기를 사용할 수 없습니다.");
            }
        }
        _titleStatus = string.Join('\n', notices);
    }

    private void StartNewCampaign()
    {
        _run = new ReleaseCampaignRun(
            _campaign,
            _world,
            _campaignHash,
            _fixtureHash);
        RefreshCampaignSnapshot();
        if (!TrySaveCampaign(out string error))
        {
            _shell.ShowPersistenceError(error);
            return;
        }
        _continuationSave = _run.CaptureSave();
        _shell.HideShell();
        ShowCurrentBriefing();
    }

    private void ContinueCampaign()
    {
        if (_continuationSave is null)
        {
            _shell.ShowPersistenceError("이어할 수 있는 저장 기록이 없습니다.");
            return;
        }
        try
        {
            _run = ReleaseCampaignRun.Restore(
                _campaign,
                _world,
                _campaignHash,
                _fixtureHash,
                _continuationSave);
            RefreshCampaignSnapshot();
            _shell.HideShell();
            ShowCurrentBriefing();
        }
        catch (Exception exception)
        {
            GD.PushWarning(exception.ToString());
            _shell.ShowPersistenceError("저장 기록을 복원하지 못했습니다. 새 게임은 시작할 수 있습니다.");
        }
    }

    private void RefreshCampaignSnapshot()
    {
        _campaignSnapshot = _run!.GetSnapshot();
        _snapshot = _campaignSnapshot.Construction;
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
        _shell.ShowPause(_run.CurrentChapter.DisplayName, _titleStatus);
    }

    private void SaveAndReturnToTitle()
    {
        if (!TrySaveCampaign(out string error))
        {
            _shell.ShowPersistenceError(error);
            return;
        }
        _continuationSave = _run!.CaptureSave();
        _shell.ShowTitle(true, "현재 임무까지 저장했습니다.");
    }

    private void RestartChapter()
    {
        if (_run is null)
        {
            return;
        }
        _campaignSnapshot = _run.RestartChapter();
        _snapshot = _campaignSnapshot.Construction;
        _lastError = null;
        _campaignError = null;
        _assessment = null;
        if (!TrySaveCampaign(out string error))
        {
            _shell.ShowPersistenceError(error);
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
            return true;
        }
        catch (Exception exception)
        {
            error = "캠페인을 저장하지 못했습니다. 저장 공간과 권한을 확인하세요.";
            GD.PushWarning($"{error} {exception}");
            return false;
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
        GetWindow().ContentScaleFactor = settings.UiScalePercent / 100f;
        SetBusVolume("Master", settings.MasterVolumePercent);
        SetBusVolume("Ambient", settings.AmbientVolumePercent);
        SetBusVolume("SFX", settings.SfxVolumePercent);
        _shell.SetSettings(
            settings.WindowMode == ProductWindowMode.Fullscreen,
            settings.UiScalePercent,
            settings.ShowControlHelp,
            settings.MasterVolumePercent,
            settings.AmbientVolumePercent,
            settings.SfxVolumePercent);
    }

    private static void SetBusVolume(string name, int percent)
    {
        int index = AudioServer.GetBusIndex(name);
        if (index >= 0)
        {
            AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(percent / 100f));
        }
    }

    private void SaveSettings()
    {
        try
        {
            ProductPersistenceStore.SaveSettings(_settingsPath, _settings);
        }
        catch (Exception exception)
        {
            _titleStatus = "화면 설정을 저장하지 못했습니다.";
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
            await CloseStory("첫 임무 브리핑");

            await BuildCampaignLine("CENTRAL_JUNCTION", false,
                new ReleasePoint(9, 13));
            await BuildCampaignLine("SOUTH_JUNCTION", false,
                new ReleasePoint(13, 10));
            await BuildCampaignLine("RIVER_MERGE", false,
                new ReleasePoint(17, 10));
            await CompleteCampaignChapter("PROLOGUE_FIRST_LIGHT");

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

            await BuildCampaignLine("NORTH_JUNCTION", false,
                new ReleasePoint(13, 10));
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

    private void EmitShell(ReleaseShellAction action, string description) =>
        EmitButton(_shell.GetActionButton(action), description);

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
