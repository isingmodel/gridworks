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
        "Gridworks.Game.EmbeddedData.commercial-free-placement-slice-v1.json";
    private const string SubstationClassId = "SMALL_SUBSTATION";
    private const string LineClassId = "REINFORCED_LINE";
    private const string PoleClassId = "REINFORCED_POLE";

    private CommercialLaunchOptions _options = null!;
    private ConstructionSession _session = null!;
    private ConstructionSnapshot _snapshot = null!;
    private CommercialWorldDefinition? _thermalWorld = null;
    private ThermalSequenceEvaluation? _thermalEvaluation = null;
    private int _thermalProjectionIndex;
    private string? _selectedThermalAssetId;
    private CommercialMapView _map = null!;
    private CommercialTaskPanel _panel = null!;
    private Label _zoomLabel = null!;
    private Label _summaryLabel = null!;
    private Label _controlHelpLabel = null!;
    private Label _helpBodyLabel = null!;
    private Button _helpButton = null!;
    private Control _helpOverlay = null!;
    private Button _closeHelpButton = null!;
    private Label _fatalLabel = null!;

    private CommercialTool _tool;
    private CoreMapPoint? _pointerPoint;
    private string? _candidateNodeId;
    private bool _pointerAccepted = true;
    private ConstructionError? _pointerError;
    private string _pointerMessage = string.Empty;
    private IReadOnlyList<string> _pointerRiskAreaIds = Array.Empty<string>();
    private string _lastStatus = "아직 발주한 공사가 없습니다.";
    private string _lastError = string.Empty;

    public override void _Ready()
    {
        try
        {
            _options = CommercialLaunchOptions.Parse(OS.GetCmdlineUserArgs());
#if DEBUG
            if (_options.ThermalSmoke)
            {
                InitializeThermalSmokeMode();
            }
            else
#endif
            {
                InitializePlacementMode();
            }
            BindScene();
            Render();
            _map.CallDeferred(Control.MethodName.GrabFocus);
#if DEBUG
            if (_options.PlacementSmoke)
            {
                CallDeferred(nameof(RunPlacementSmoke));
            }
            else if (_options.ThermalSmoke)
            {
                CallDeferred(nameof(RunThermalSmoke));
            }
#endif
        }
        catch (Exception exception)
        {
            GD.PushError($"상용 자유 배치 화면을 시작하지 못했습니다: {exception}");
            ShowFatal(exception.Message);
#if DEBUG
            if (OS.GetCmdlineUserArgs().Any(argument => argument is
                    "--commercial-placement-smoke" or "--commercial-thermal-smoke"))
            {
                GetTree().Quit(1);
            }
#endif
        }
    }

    private void InitializePlacementMode()
    {
        GetWindow().Title = "Gridworks — 첫 불빛 자유 배치";
        SpatialWorldDefinition world = SpatialWorldLoader.Load(
            ReadEmbeddedResourceBytes(FixtureResource));
        _session = new ConstructionSession(world);
        _snapshot = _session.GetSnapshot();
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
        else if (_helpOverlay.Visible)
        {
            HideHelp();
        }
        else
        {
            ShowHelp();
        }
        GetViewport().SetInputAsHandled();
    }

    private void BindScene()
    {
        _map = GetNode<CommercialMapView>("%CommercialMapView");
        _panel = GetNode<CommercialTaskPanel>("%CommercialTaskPanel");
        _zoomLabel = GetNode<Label>("%ZoomLabel");
        _summaryLabel = GetNode<Label>("%SummaryLabel");
        _controlHelpLabel = GetNode<Label>("%ControlHelp");
        _helpBodyLabel = GetNode<Label>("%HelpBody");
        _helpButton = GetNode<Button>("%HelpButton");
        _helpOverlay = GetNode<Control>("%HelpOverlay");
        _closeHelpButton = GetNode<Button>("%CloseHelpButton");
        _fatalLabel = GetNode<Label>("%FatalLabel");

        _map.PointerChanged += OnPointerChanged;
        _map.PointRequested += OnPointRequested;
        _map.UndoRequested += () =>
            Apply(_session.UndoLinePoint(), "마지막 선로 지점을 되돌렸습니다.");
        _map.DraftPointMoveRequested += MoveDraftPoint;
        _map.DraftPointDragPreviewChanged += OnDraftPointDragPreviewChanged;
        _map.ThermalAssetRequested += SelectThermalAsset;
        _map.CameraChanged += () =>
        {
            _zoomLabel.Text = $"지도 · {_map.ZoomLabel}";
        };
        _panel.ActionRequested += OnPanelAction;
        _panel.ProjectionDeltaRequested += ChangeThermalProjection;
        _helpButton.Pressed += ShowHelp;
        _closeHelpButton.Pressed += HideHelp;
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
                Apply(_session.SetNodeDraft(SubstationClassId, point),
                    "변전소 계획 위치를 정했습니다.");
                return;

            case CommercialTool.Line when _snapshot.Phase == ConstructionPhase.Ready:
                if (candidateNodeId is null)
                {
                    RejectLocally("선로를 시작할 접속점 가까이에서 선택하세요.");
                    return;
                }
                Apply(_session.StartLineDraft(candidateNodeId, LineClassId, PoleClassId),
                    "첫 접속점을 정했습니다. 다음 전신주 위치를 이어서 선택하세요.");
                return;

            case CommercialTool.Line when
                _snapshot.Phase == ConstructionPhase.LineDrafting &&
                _snapshot.LineDraft?.EndNodeId is null:
                ConstructionCommandResult result = candidateNodeId is null
                    ? _session.AddLinePoint(point)
                    : _session.FinishLineDraft(candidateNodeId);
                Apply(result, candidateNodeId is null
                    ? "전신주 위치를 계획에 더했습니다."
                    : "마지막 접속점을 정했습니다. 견적을 확인하고 발주하세요.");
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
                Apply(_session.UndoLinePoint(), "마지막 선로 지점을 되돌렸습니다.");
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
            : "완공된 접속점에서 선로를 시작하세요.";
        RefreshPointerPreview();
        Render();
        _map.GrabFocus();
    }

    private void CancelDraft()
    {
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
    }

    private void OrderOrComplete()
    {
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
    }

    private void Apply(ConstructionCommandResult result, string success)
    {
        _snapshot = result.Snapshot;
        if (result.Accepted)
        {
            _lastStatus = success;
            _lastError = string.Empty;
        }
        else
        {
            _lastError = ErrorText(result.Error);
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

        Apply(_session.MoveLinePoint(drag.PointIndex, drag.Position), "작성 중인 전신주 위치를 옮겼습니다.");
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
        LinePointMovePreview preview = _session.PreviewMoveLinePoint(
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
            NodePlacementPreview preview = _session.PreviewNodePlacement(SubstationClassId, point);
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
            LineStartPreview preview = _session.PreviewLineStart(
                _candidateNodeId,
                LineClassId,
                PoleClassId);
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
                LineFinishPreview preview = _session.PreviewLineFinish(candidate);
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
                LinePointPreview preview = _session.PreviewLinePoint(point);
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
            _tool == CommercialTool.Line));
        _panel.SetModel(BuildPanelModel());
        _zoomLabel.Text = $"지도 · {_map.ZoomLabel}";
        int commissionedEdges = _snapshot.World.Edges.Count(edge => edge.Commissioned);
        _summaryLabel.Text = commissionedEdges == 0
            ? "무벌점 연습 · 발전소와 마을 사이에 첫 선로를 완성하세요."
            : $"자유 배치 연습 · 완공 선로 {commissionedEdges}구간 · 다른 경로도 계속 시험할 수 있습니다.";
    }

    private void RenderThermalMode()
    {
        ThermalIntervalEvaluation interval = CurrentThermalInterval();
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
        _helpBodyLabel.Text =
            "지도 확대: 마우스 휠 또는 + / −\n" +
            "지도 이동: 가운데 버튼 드래그 또는 Space+드래그\n" +
            "전체 보기: Home\n" +
            "설비 선택: 마우스로 클릭하거나 방향키로 커서를 옮긴 뒤 Enter\n" +
            "열 운전 국면: 오른쪽 패널의 이전 국면 / 다음 국면\n\n" +
            "✓ 연속 운전은 실선, ! 비상 운전은 이중선과 사선, × 보호정지는 점선, !! 비상 한계 초과는 교차선으로 표시합니다.\n" +
            "색을 구분하기 어려워도 패턴·아이콘·오른쪽 상태 문장으로 같은 정보를 확인할 수 있습니다.";
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
            HeadingText(),
            InstructionText(),
            string.IsNullOrWhiteSpace(_pointerMessage)
                ? "지도에서 작업 위치를 선택하세요."
                : _pointerMessage,
            quoteText,
            $"공사 기준 시각 {_snapshot.Minute}분 · {_lastStatus}",
            error,
            new CommercialActionPresentation(
                _snapshot.Phase == ConstructionPhase.Ready,
                "변전소 놓기",
                "소형 배전 변전소의 점유영역을 지형 위에서 자유롭게 계획합니다."),
            new CommercialActionPresentation(
                _snapshot.Phase == ConstructionPhase.Ready,
                "선로 잇기",
                "접속점에서 시작해 전신주 위치를 차례로 놓고 다른 접속점에 연결합니다."),
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
                    : "현재 계획과 견적을 확정하고 공사를 시작합니다."));
    }

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
            "열 운전 확인",
            "작성된 고정 전력망의 계산 결과입니다. 국면을 바꾸고 설비를 선택해 열 한계와 다음 상태를 비교하세요.",
            selection,
            limits,
            state,
            string.Empty,
            hidden,
            hidden,
            hidden,
            hidden,
            hidden,
            new CommercialProjectionPresentation(
                ThermalProjectionLabel(),
                _thermalProjectionIndex > 0,
                _thermalProjectionIndex + 1 < count));
    }

    private void SelectThermalAsset(string assetId)
    {
        if (_thermalEvaluation is null ||
            !CurrentThermalInterval().Assets.Any(item =>
                string.Equals(item.AssetId, assetId, StringComparison.Ordinal)))
        {
            return;
        }
        _selectedThermalAssetId = assetId;
        Render();
    }

    private void ChangeThermalProjection(int delta)
    {
        if (_thermalEvaluation is null || delta == 0)
        {
            return;
        }
        int next = Math.Clamp(
            _thermalProjectionIndex + Math.Sign(delta),
            0,
            _thermalEvaluation.Intervals.Count - 1);
        if (next == _thermalProjectionIndex)
        {
            return;
        }
        _thermalProjectionIndex = next;
        EnsureThermalSelection(CurrentThermalInterval());
        Render();
    }

    private ThermalIntervalEvaluation CurrentThermalInterval()
    {
        if (_thermalEvaluation is null || _thermalEvaluation.Intervals.Count == 0)
        {
            throw new InvalidOperationException("열 운전 결과가 준비되지 않았습니다.");
        }
        return _thermalEvaluation.Intervals[_thermalProjectionIndex];
    }

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

    private string ThermalProjectionLabel() =>
        $"국면 {_thermalProjectionIndex + 1} / {_thermalEvaluation!.Intervals.Count}";

    private string ThermalSelectionText(ThermalIntervalEvaluation interval)
    {
        ThermalAssetUsage? usage = interval.Assets.FirstOrDefault(item =>
            string.Equals(item.AssetId, _selectedThermalAssetId, StringComparison.Ordinal));
        return usage is null
            ? "선택한 열 설비가 없습니다."
            : $"{ThermalAssetName(usage)}. 현재 사용 {FormatPower(usage.UsedKw)}, " +
              $"연속 한계 {FormatPower(usage.ContinuousKw)}, " +
              $"비상 한계 {FormatPower(usage.EmergencyKw)}. " +
              $"현재 상태 {ThermalStateText(usage.State)}, " +
              $"다음 상태 {ThermalStateText(usage.NextState)}.";
    }

    private string ThermalAssetName(ThermalAssetUsage usage)
    {
        CommercialWorldDefinition world = _thermalWorld ??
            throw new InvalidOperationException("열 운전 전력망이 준비되지 않았습니다.");
        if (usage.AssetKind == ThermalAssetKind.Node)
        {
            SpatialNodeDefinition node = world.Nodes.Single(item => item.NodeId == usage.AssetId);
            CommercialNodeClassDefinition nodeClass = world.NodeClasses.Single(
                item => item.ClassId == node.ClassId);
            string equipment = nodeClass.Kind switch
            {
                SpatialNodeKind.Pole => "전신주 접속부",
                SpatialNodeKind.Substation => "변전소 제한 요소",
                _ => "열 설비",
            };
            return $"{node.DisplayName} · {equipment}";
        }

        SpatialEdgeDefinition edge = world.Edges.Single(item => item.EdgeId == usage.AssetId);
        SpatialNodeDefinition from = world.Nodes.Single(item => item.NodeId == edge.FromNodeId);
        SpatialNodeDefinition to = world.Nodes.Single(item => item.NodeId == edge.ToNodeId);
        CommercialLineClassDefinition lineClass = world.LineClasses.Single(
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

    private static string FormatPower(long kilowatts) =>
        kilowatts.ToString("N0", CultureInfo.GetCultureInfo("ko-KR")) + " kW";

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
            return _snapshot.World.NodeClasses.Single(item => item.ClassId == PoleClassId)
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
        _helpOverlay.Visible = true;
        _closeHelpButton.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void HideHelp()
    {
        _helpOverlay.Visible = false;
        _map.CallDeferred(Control.MethodName.GrabFocus);
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

    private static byte[] ReadEmbeddedResourceBytes(string resourceName)
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("상용 게임 데이터를 열 수 없습니다.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

}
