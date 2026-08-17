using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
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
    private ReleaseConstructionSession _session = null!;
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
    private Action? _storyContinuation;

    private Label _phaseLabel = null!;
    private Label _timeLabel = null!;
    private Label _supplyLabel = null!;
    private Label _assetLabel = null!;
    private ReleaseMapView _map = null!;
    private ReleaseTaskPanel _panel = null!;
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
            _fixtureHash = Convert.ToHexString(SHA256.HashData(worldBytes)).ToLowerInvariant();
            ReleaseWorldDefinition world = ReleaseWorldLoader.Load(worldBytes);
            _session = new ReleaseConstructionSession(world);
            _snapshot = _session.GetSnapshot();
            BindScene();
            Render();
            ShowStory(
                "운영 브리핑",
                "동부권 확장 사전공사",
                "동부권 수요 증가에 대비해 새 변전소 부지를 확보하고 남부 분기점에서 동부 변전소로 우회선을 이으세요. " +
                "완공 전 설비는 공급에 참여하지 않으며, 분기점과 변전소의 접속 여유를 함께 확인해야 합니다.",
                "지도 확인하기");
            GD.Print($"RELEASE_READY session={_options.SessionId} fixtureHash={_fixtureHash}");
            if (_options.Smoke)
            {
                CallDeferred(nameof(RunSmoke));
            }
        }
        catch (Exception exception)
        {
            GD.PushError($"출시판 화면을 시작하지 못했습니다: {exception}");
            ShowFatal("게임을 시작하지 못했습니다. 설치 파일을 다시 확인하세요.");
            if (OS.GetCmdlineUserArgs().Contains("--release-smoke"))
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
        _map = GetNode<ReleaseMapView>("%ReleaseMapView");
        _panel = GetNode<ReleaseTaskPanel>("%ReleaseTaskPanel");
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
                ReleaseNodePlacementPreview preview = _session.PreviewNodePlacement(
                    NodeClassForTool(),
                    actual);
                _pointerAccepted = preview.Accepted;
                _pointerError = preview.Error;
            }
            else if (_snapshot.Phase == ReleaseConstructionPhase.LineDrafting)
            {
                ReleaseLinePointPreview preview = _session.PreviewLinePoint(actual);
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
        ReleaseConstructionCommandResult? result = null;
        if (_tool == Tool.Inspect)
        {
            _selectedNodeId = _hoverNodeId ?? NodeAt(point)?.NodeId;
            _selectedEdgeId = _selectedNodeId is null ? _hoverEdgeId : null;
            Render();
            return;
        }

        if (_tool is Tool.SmallSubstation or Tool.LargeSubstation)
        {
            result = _session.SetNodeDraft(NodeClassForTool(), point);
        }
        else if (_tool is Tool.StandardLine or Tool.ReinforcedLine)
        {
            if (_snapshot.Phase == ReleaseConstructionPhase.Ready)
            {
                ReleaseNodeDefinition? start = NodeAt(point);
                result = _session.StartLineDraft(
                    start?.NodeId ?? string.Empty,
                    LineClassForTool(),
                    PoleClassForTool());
            }
            else if (_snapshot.Phase == ReleaseConstructionPhase.LineDrafting)
            {
                result = _session.AddLinePoint(point);
            }
        }

        if (result is not null)
        {
            Apply(result);
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
                Apply(_snapshot.Phase == ReleaseConstructionPhase.NodeDrafting
                    ? _session.CancelNodeDraft()
                    : _session.CancelLineDraft());
                if (_lastError is null)
                {
                    _tool = Tool.Inspect;
                }
                break;
            case ReleasePanelAction.Undo:
                Apply(_session.UndoLinePoint());
                break;
            case ReleasePanelAction.Order:
                Apply(_snapshot.Phase == ReleaseConstructionPhase.NodeDrafting
                    ? _session.OrderNode()
                    : _session.OrderLine());
                break;
            case ReleasePanelAction.Advance:
                ReleaseConstructionKind? kind = _snapshot.ActiveConstruction?.Kind;
                Apply(_session.AdvanceToConstructionCompletion());
                if (_lastError is null && kind == ReleaseConstructionKind.Node)
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
                else if (_lastError is null && kind == ReleaseConstructionKind.Line)
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

    private void Render()
    {
        _phaseLabel.Text = ReleaseKoreanText.Phase(_snapshot.Phase);
        _phaseLabel.AccessibilityName = $"현재 작업 {ReleaseKoreanText.Phase(_snapshot.Phase)}";
        _timeLabel.Text = $"현재 시각 · {ReleaseKoreanText.FormatClock(_snapshot.Minute)}";
        _supplyLabel.Text =
            $"공급 중 · {ReleaseKoreanText.FormatPower(_snapshot.Evaluation.TotalDeliveredKw)} / " +
            $"{ReleaseKoreanText.FormatPower(_snapshot.World.Loads.Sum(item => item.DemandKw))}";
        _assetLabel.Text =
            $"완공 설비 {_snapshot.World.Nodes.Count(item => item.Commissioned)}곳 · " +
            $"선로 {_snapshot.World.Edges.Count(item => item.Commissioned)}구간";

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
            ? _session.PreviewNodeOrder()
            : lineDraft
                ? _session.PreviewLineOrder()
                : new ReleaseConstructionQuote(false, ReleaseConstructionError.WrongPhase, null, null, null);
        string quoteText = building
            ? ActiveConstructionText()
            : quote.Accepted
                ? $"공사비 {ReleaseKoreanText.FormatCash(quote.CostCashUnit!.Value)} · " +
                  $"공사기간 {ReleaseKoreanText.FormatDuration(quote.BuildMinutes!.Value)} · " +
                  $"완공 예정 {ReleaseKoreanText.FormatClock(quote.CompletionMinute!.Value)}"
                : string.Empty;
        string error = ReleaseKoreanText.ConstructionError(_lastError ?? _pointerError);
        ReleaseButtonPresentation toolButton(string text, string description) =>
            new(true, ready || nodeDraft, text, description);
        return new ReleaseTaskPanelModel(
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

    private void ShowFatal(string message)
    {
        if (_fatalLabel is not null)
        {
            _fatalLabel.Text = message;
            _fatalLabel.Show();
        }
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
