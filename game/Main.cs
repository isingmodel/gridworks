using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gridworks.Core;
using Godot;

namespace Gridworks.Game;

public sealed partial class Main : Control
{
    private static readonly Color BackgroundColor = Color.FromHtml("071019");
    private static readonly Color PanelColor = Color.FromHtml("101d27");
    private static readonly Color PanelBorderColor = Color.FromHtml("385166");
    private static readonly Color TextColor = Color.FromHtml("e6eef2");
    private static readonly Color MutedTextColor = Color.FromHtml("9fb0b9");
    private static readonly Color AccentColor = Color.FromHtml("5bc0be");
    private static readonly Color WarningColor = Color.FromHtml("e0a458");
    private static readonly Color ErrorColor = Color.FromHtml("e66d66");

    private readonly Dictionary<PredictionKey, PredictionValue> _predictions = new();
    private readonly Dictionary<PredictionKey, Dictionary<PredictionValue, CheckButton>> _predictionButtons = new();
    private readonly Dictionary<CorridorDesign, CheckButton> _corridorButtons = new();
    private readonly Dictionary<(CorridorDesign Design, string CaseId), RemovalEvaluation> _reveal = new();
    private readonly List<Control> _focusControls = new();

    private LaunchOptions _options = null!;
    private DiagnosticLog? _diagnostic;
    private ScenarioDefinition _scenario = null!;
    private PresentationDefinition _presentation = null!;
    private GridworksSession _session = null!;
    private PublicSnapshot _snapshot = null!;
    private MapDefinition _mapDefinition = null!;
    private string _fixtureHash = string.Empty;
    private string _buildHash = string.Empty;

    private string _townLoadId = string.Empty;
    private string _hospitalLoadId = string.Empty;
    private string _townSubstationId = string.Empty;
    private string _hospitalSubstationId = string.Empty;
    private ProjectDefinition _townProject = null!;
    private Dictionary<CorridorDesign, ProjectDefinition> _corridorProjects = null!;
    private EvaluationCaseDefinition _electricalCase = null!;
    private EvaluationCaseDefinition _spatialCase = null!;
    private EventDefinition _event = null!;
    private RequirementDefinition _requirement = null!;
    private IReadOnlyList<CorridorDesign> _orderedCorridors = null!;

    private Label _header = null!;
    private GridMapView _mapView = null!;
    private VBoxContainer _rightBody = null!;
    private TimelineView _timelineView = null!;
    private HBoxContainer _timelineLabels = null!;
    private Label? _errorLabel;
    private Button? _townOrderButton;
    private Button? _advanceButton;
    private Button? _confirmButton;
    private CorridorDesign? _selectedCorridor;
    private bool _predictionsLocked;
    private bool _revealOpened;
    private bool _finalLogged;

    public override void _Ready()
    {
        try
        {
            GetWindow().Title = "Gridworks";
            _options = LaunchOptions.Parse(OS.GetCmdlineUserArgs());

            var fixturePath = Path.GetFullPath(Path.Combine(
                ProjectSettings.GlobalizePath("res://"), "..", "data", "scope-0b-v1.json"));
            var fixtureBytes = File.ReadAllBytes(fixturePath);
            _fixtureHash = LowerHex(SHA256.HashData(fixtureBytes));
            _buildHash = ComputeBuildHash();

            var loaded = FixtureLoader.Load(fixtureBytes);
            _scenario = loaded.Scenario;
            _presentation = loaded.Presentation;
            GetWindow().Title = $"Gridworks — {_scenario.DisplayName}";
            BindScenarioSemantics();

            _session = new GridworksSession(_scenario);
            _snapshot = _session.GetSnapshot();
            _mapDefinition = BuildMapDefinition();

            _diagnostic = new DiagnosticLog(_options.DiagnosticPath, _options.SessionId, _options.Variant);
            BuildChrome();
            Render();
            _diagnostic.Write("READY", true, SnapshotHash(_snapshot), new ReadyPayload(_buildHash, _fixtureHash));

            if (_options.Smoke)
            {
                CallDeferred(nameof(RunSmoke));
            }
        }
        catch (Exception exception)
        {
            GD.PushError($"Scope 0B startup failed: {exception}");
            ShowFatalError(exception.Message);
            if (_options?.Smoke == true)
            {
                GetTree().Quit(1);
            }
        }
    }

    public override void _ExitTree()
    {
        _diagnostic?.Dispose();
        _diagnostic = null;
    }

    private void BindScenarioSemantics()
    {
        var loadNodes = _scenario.Nodes.Where(node => node.Kind == NodeKind.Load).ToArray();
        var town = loadNodes.Single(node => node.Priority == LoadPriority.P2);
        var hospital = loadNodes.Single(node => node.Priority == LoadPriority.P0);
        _townLoadId = town.Id;
        _hospitalLoadId = hospital.Id;
        _townSubstationId = town.ServiceSubstationId
            ?? throw new FixtureValidationException("Town load has no service substation.");
        _hospitalSubstationId = hospital.ServiceSubstationId
            ?? throw new FixtureValidationException("Hospital load has no service substation.");

        var edgesById = _scenario.Edges.ToDictionary(edge => edge.Id, StringComparer.Ordinal);
        _townProject = _scenario.Projects.Single(project =>
            edgesById[project.EdgeId].ToNodeId == _townSubstationId);

        _event = _scenario.Events.Single();
        _spatialCase = _scenario.EvaluationCases.Single(evaluationCase =>
            evaluationCase.Id == _event.EvaluationCaseId &&
            evaluationCase.SelectorType == SelectorType.SpatialRiskGroup);
        _electricalCase = _scenario.EvaluationCases.Single(evaluationCase =>
            evaluationCase.SelectorType == SelectorType.ElectricalContingencyId);
        _requirement = _scenario.Requirements.Single();

        var corridorProjects = _scenario.Projects
            .Where(project => edgesById[project.EdgeId].ToNodeId == _hospitalSubstationId)
            .ToArray();
        var riverProject = corridorProjects.Single(project =>
            edgesById[project.EdgeId].SpatialRiskGroup == _spatialCase.SelectorValue);
        var northProject = corridorProjects.Single(project => project.Id != riverProject.Id);
        _corridorProjects = new Dictionary<CorridorDesign, ProjectDefinition>
        {
            [CorridorDesign.RiverParallel] = riverProject,
            [CorridorDesign.NorthDetour] = northProject,
        };
        var projectToDesign = _corridorProjects.ToDictionary(pair => pair.Value.Id, pair => pair.Key, StringComparer.Ordinal);
        var layout = _presentation.LayoutVariants.Single(item => item.Id == _options.Variant);
        _orderedCorridors = layout.CorridorProjectOrder.Select(projectId => projectToDesign[projectId]).ToArray();
        if (_orderedCorridors.Count != 2 || _orderedCorridors.Distinct().Count() != 2)
        {
            throw new FixtureValidationException("Layout variant must contain each corridor exactly once.");
        }
    }

    private MapDefinition BuildMapDefinition()
    {
        var nodes = _scenario.Nodes.Select(node => new MapNodeDefinition(
            node.Id,
            node.Kind switch
            {
                NodeKind.Generator => MapNodeKind.Generator,
                NodeKind.Bus => MapNodeKind.Bus,
                NodeKind.Substation => MapNodeKind.Substation,
                NodeKind.Load when node.Id == _townLoadId => MapNodeKind.Town,
                NodeKind.Load when node.Id == _hospitalLoadId => MapNodeKind.Hospital,
                _ => throw new FixtureValidationException($"Unsupported map node '{node.Id}'."),
            },
            ToMapPoint(node.Position))).ToArray();

        var displayNameByEdge = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [_townProject.EdgeId] = "마을 상위 피더",
            [_corridorProjects[CorridorDesign.RiverParallel].EdgeId] = "강변 병렬선",
            [_corridorProjects[CorridorDesign.NorthDetour].EdgeId] = "북부 우회선",
        };
        var edgePolylines = _presentation.EdgePolylines.Select(polyline => new MapEdgeDefinition(
            polyline.EdgeId,
            polyline.Points.Select(ToMapPoint).ToArray(),
            displayNameByEdge.GetValueOrDefault(polyline.EdgeId, string.Empty))).ToArray();
        var risks = _presentation.RiskAreas.Select(area =>
            new MapRiskArea(area.Polygon.Select(ToMapPoint).ToArray())).ToArray();
        var services = _presentation.ServiceAreas.Select(area => new MapServiceArea(
            ToMapPoint(area.Center), (float)area.RadiusX, (float)area.RadiusY)).ToArray();
        var paths = _scenario.PermittedSupplyPaths.ToDictionary(
            path => path.Id,
            path => path.EdgeIds,
            StringComparer.Ordinal);

        return new MapDefinition(
            (float)_presentation.MapBounds.Width,
            (float)_presentation.MapBounds.Height,
            nodes,
            edgePolylines,
            risks,
            services,
            paths);
    }

    private void BuildChrome()
    {
        Theme = new Theme { DefaultFontSize = 15 };

        AddChild(new ColorRect
        {
            Color = BackgroundColor,
            Position = Vector2.Zero,
            Size = new Vector2(1280, 720),
            MouseFilter = MouseFilterEnum.Ignore,
        });

        _header = NewLabel(string.Empty, 21, 34, TextColor);
        _header.Position = new Vector2(14, 8);
        _header.Size = new Vector2(1252, 34);
        _header.HorizontalAlignment = HorizontalAlignment.Left;
        AddChild(_header);

        var mapPanel = NewPanel(new Rect2(12, 50, 746, 500));
        AddChild(mapPanel);
        _mapView = new GridMapView
        {
            Position = new Vector2(8, 8),
            Size = new Vector2(730, 484),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        mapPanel.AddChild(_mapView);

        var rightPanel = NewPanel(new Rect2(768, 50, 500, 500));
        AddChild(rightPanel);
        _rightBody = new VBoxContainer
        {
            Position = new Vector2(14, 10),
            Size = new Vector2(472, 480),
            ClipContents = true,
        };
        _rightBody.AddThemeConstantOverride("separation", 3);
        rightPanel.AddChild(_rightBody);

        var timelinePanel = NewPanel(new Rect2(12, 560, 1256, 148));
        AddChild(timelinePanel);
        var timelineTitle = NewLabel("예고 타임라인", 16, 24, TextColor);
        timelineTitle.Position = new Vector2(12, 6);
        timelineTitle.Size = new Vector2(220, 24);
        timelinePanel.AddChild(timelineTitle);
        _timelineView = new TimelineView
        {
            Position = new Vector2(8, 27),
            Size = new Vector2(1240, 42),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        timelinePanel.AddChild(_timelineView);
        _timelineLabels = new HBoxContainer
        {
            Position = new Vector2(10, 72),
            Size = new Vector2(1236, 66),
        };
        _timelineLabels.AddThemeConstantOverride("separation", 5);
        timelinePanel.AddChild(_timelineLabels);
    }

    private void Render()
    {
        _header.Text = $"GRIDWORKS  |  {_scenario.DisplayName}  |  {StageLabel()}  |  {CurrentTimeLabel()}  |  현금 {FormatMoney(_snapshot.Cash)}";
        _header.AccessibilityName = _header.Text;

        _mapView.SetModel(_mapDefinition, BuildMapState());
        UpdateTimeline();
        ClearRightBody();

        if (_snapshot.IsComplete)
        {
            BuildFinalPanel();
        }
        else if (_snapshot.EventRemovedEdgeIds.Count > 0)
        {
            BuildEventPanel();
        }
        else if (_snapshot.SelectedCorridor is not null)
        {
            BuildConstructionPanel();
        }
        else if (_snapshot.Minute >= _corridorProjects.Values.Min(project => project.AllowedOrderMinute))
        {
            BuildDecisionPanel();
        }
        else
        {
            BuildStartPanel();
        }

        WireFocusOrder();
        CallDeferred(nameof(GrabFirstActionFocus));
    }

    private MapState BuildMapState()
    {
        var commissioned = _snapshot.CommissionedEdgeIds.ToHashSet(StringComparer.Ordinal);
        var removed = _snapshot.EventRemovedEdgeIds.ToHashSet(StringComparer.Ordinal);
        var building = new HashSet<string>(StringComparer.Ordinal);
        if (_snapshot.TownProjectState == ProjectState.Building)
        {
            building.Add(_townProject.EdgeId);
        }

        if (_snapshot.CorridorProjectState == ProjectState.Building && _snapshot.SelectedCorridor is { } selected)
        {
            building.Add(_corridorProjects[selected].EdgeId);
        }

        var energized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pathId in _snapshot.UtilityPathByLoad.Values.Where(value => value is not null))
        {
            foreach (var edgeId in _mapDefinition.PathEdgeIds[pathId!])
            {
                energized.Add(edgeId);
            }
        }

        var townUtility = _snapshot.UtilityPathByLoad.TryGetValue(_townLoadId, out var townPath) && townPath is not null;
        var hospitalUtility = _snapshot.UtilityPathByLoad.TryGetValue(_hospitalLoadId, out var hospitalPath) && hospitalPath is not null;
        var p0Source = hospitalUtility ? "utility" : _snapshot.HospitalInternalStage switch
        {
            InternalPowerStage.Ups => "UPS",
            InternalPowerStage.Diesel => "diesel",
            _ => "전원 없음",
        };
        var selectedEdge = _snapshot.SelectedCorridor is { } corridor ? _corridorProjects[corridor].EdgeId : null;

        return new MapState(
            commissioned,
            building,
            removed,
            energized,
            selectedEdge,
            townUtility,
            hospitalUtility,
            p0Source);
    }

    private void BuildStartPanel()
    {
        AddSectionTitle("1 / 5  마을 접속");
        AddCopy("서비스 권역은 이 변전소에 접속할 수 있다는 뜻입니다. 발전소까지 이어진 통전 경로가 있어야 실제 전력 공급이 됩니다.", 14, 58);
        AddStatusLine("마을", "권역 안 · utility 공급경로 없음", WarningColor);
        AddStatusLine("병원", "utility 공급 중", AccentColor);

        if (_snapshot.TownProjectState == ProjectState.NotOrdered)
        {
            _townOrderButton = AddActionButton(
                $"마을 접속공사 발주  {FormatMoney(_townProject.CostCashUnit)}",
                "마을 상위 피더 공사를 발주합니다. 비용은 즉시 한 번 차감되고 완공 전에는 공급에 사용할 수 없습니다.",
                OnTownOrderPressed,
                false);
            AddCopy($"예상 완공: {TimeLabel(_townProject.AllowedOrderMinute + _townProject.BuildMinutes)}", 13, 24, MutedTextColor);
        }
        else
        {
            AddStatusLine("마을 상위 피더", $"공사 중 · {TimeLabel(_townProject.AllowedOrderMinute + _townProject.BuildMinutes)} 완공", WarningColor);
        }

        _advanceButton = AddActionButton(
            "다음 이정표",
            "다음 공개 이정표까지 시간을 진행합니다.",
            OnAdvancePressed,
            _snapshot.TownProjectState == ProjectState.NotOrdered);
        AddErrorLine();
    }

    private void BuildDecisionPanel()
    {
        AddSectionTitle("2 / 5  회랑 결정");
        AddCopy($"병원 소유 내부전원: UPS {InternalStageMinutes(0)}분 + diesel {InternalStageMinutes(1)}분. P0는 지키지만 전력회사의 인도·판매가 아니며 utility 미공급 보상과도 별개입니다.", 13, 50);
        AddCopy("두 계획은 모두 기한 안에 완공되며 병원 주 회로 E1과 다른 차단 회로를 씁니다. 아래 네 utility 경로 결과를 먼저 예측하세요.", 13, 42, MutedTextColor);

        var grid = new GridContainer { Columns = 3, CustomMinimumSize = new Vector2(462, 142) };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 2);
        grid.AddChild(NewGridHeader("계획 / 사건", 104));
        grid.AddChild(NewGridHeader("E1만 사용불가", 164));
        grid.AddChild(NewGridHeader("강변 통로 전체", 184));

        foreach (var corridor in _orderedCorridors)
        {
            grid.AddChild(NewGridHeader(CorridorShortName(corridor), 104));
            grid.AddChild(BuildPredictionCell(new PredictionKey(corridor, PredictionCase.Electrical)));
            grid.AddChild(BuildPredictionCell(new PredictionKey(corridor, PredictionCase.Spatial)));
        }

        _rightBody.AddChild(grid);

        AddCopy("네 칸을 모두 고르면 건설 계획을 선택할 수 있습니다.", 12, 18, MutedTextColor);
        var corridorGroup = new ButtonGroup { AllowUnpress = false };
        foreach (var corridor in _orderedCorridors)
        {
            var project = _corridorProjects[corridor];
            var button = NewCheckButton(
                $"{CorridorLongName(corridor)} · {FormatMoney(project.CostCashUnit)} · {CorridorLocation(corridor)}",
                $"{CorridorLongName(corridor)} 건설 계획을 선택합니다.");
            button.ButtonGroup = corridorGroup;
            button.Disabled = true;
            button.Toggled += pressed => OnCorridorToggled(corridor, pressed);
            _corridorButtons[corridor] = button;
            _rightBody.AddChild(button);
            _focusControls.Add(button);
        }

        _confirmButton = AddActionButton(
            "예측·계획 확정",
            "네 예측을 잠그고 선택한 회랑을 한 번에 발주합니다. 성공하면 결과가 공개됩니다.",
            OnConfirmPressed,
            true);
        AddErrorLine();
        UpdateDecisionEnablement();
    }

    private Control BuildPredictionCell(PredictionKey key)
    {
        var box = new HBoxContainer { CustomMinimumSize = new Vector2(0, 52) };
        box.AddThemeConstantOverride("separation", 1);
        var group = new ButtonGroup { AllowUnpress = false };
        var values = new Dictionary<PredictionValue, CheckButton>();
        foreach (var value in new[] { PredictionValue.Remains, PredictionValue.Cut })
        {
            var visible = value == PredictionValue.Remains ? "남음" : "끊김";
            var caseName = key.Case == PredictionCase.Electrical ? "E1 단독" : "강변 통로 전체";
            var axName = $"{CorridorShortName(key.Corridor)} / {caseName} / {visible}";
            var option = NewCheckButton(visible, $"{axName}. 전력회사의 병원 utility 공급경로 결과 예측.");
            option.AccessibilityName = axName;
            option.ButtonGroup = group;
            option.CustomMinimumSize = new Vector2(78, 28);
            option.Toggled += pressed => OnPredictionToggled(key, value, pressed);
            values[value] = option;
            box.AddChild(option);
            _focusControls.Add(option);
        }

        _predictionButtons[key] = values;
        return box;
    }

    private void BuildConstructionPanel()
    {
        if (!_revealOpened)
        {
            throw new InvalidOperationException("Construction reveal cannot render before prediction lock.");
        }

        AddSectionTitle("3 / 5  공사·의무");
        var corridor = _snapshot.SelectedCorridor
            ?? throw new InvalidOperationException("Construction panel requires a selected corridor.");
        var state = _snapshot.CorridorProjectState == ProjectState.Building ? "공사 중" : "완공·통전 가능";
        AddStatusLine(CorridorLongName(corridor), state,
            _snapshot.CorridorProjectState == ProjectState.Building ? WarningColor : AccentColor);
        AddCopy("잠근 예측과 완공 가정 검증 결과", 14, 24, TextColor);

        foreach (var design in _orderedCorridors)
        {
            var e1 = _reveal[(design, _electricalCase.Id)];
            var spatial = _reveal[(design, _spatialCase.Id)];
            AddCopy(
                $"{CorridorShortName(design)}  |  E1: {PredictionText(new PredictionKey(design, PredictionCase.Electrical))} → 검증 {OutcomeText(e1)}  |  " +
                $"강변 통로: {PredictionText(new PredictionKey(design, PredictionCase.Spatial))} → 검증 {OutcomeText(spatial)}",
                12,
                42,
                MutedTextColor);
        }

        AddCopy("전기회로 사고는 E1 한 회로만 끊습니다. 공간 통로 사고는 전기적으로 다른 회로라도 같은 강변 통로 안에 있으면 함께 사용불가로 만듭니다.", 13, 55);
        AddCopy("이 결과는 각 계획이 완공됐다고 가정해 두 사건에서 utility 경로만 검증한 것입니다. 병원 내부전원 결과는 섞지 않습니다.", 12, 38, MutedTextColor);
        _advanceButton = AddActionButton(
            _snapshot.CorridorProjectState == ProjectState.Building ? "회랑 완공 이정표로" : "강변 사건 시작으로",
            "다음 공개 이정표까지 시간을 진행합니다.",
            OnAdvancePressed,
            false);
        AddErrorLine();
    }

    private void BuildEventPanel()
    {
        AddSectionTitle("4 / 5  강변 통로 사용불가");
        AddCopy($"{TimeLabel(_event.StartMinute)}부터 {TimeLabel(_event.EndMinute)}까지 강변 기존 통로 전체가 사용불가입니다.", 14, 44);

        var townDelivered = UtilityDelivered(_townLoadId);
        var hospitalDelivered = UtilityDelivered(_hospitalLoadId);
        AddStatusLine("마을 utility", townDelivered ? "공급 중" : "끊김", townDelivered ? AccentColor : ErrorColor);
        AddStatusLine("병원 utility", hospitalDelivered ? "공급 중" : "끊김", hospitalDelivered ? AccentColor : ErrorColor);

        if (hospitalDelivered)
        {
            AddStatusLine("병원 P0", "utility가 공급 · 내부전원 미사용", AccentColor);
        }
        else
        {
            var upsMinutes = InternalStageMinutes(0);
            var dieselMinutes = InternalStageMinutes(1);
            AddStatusLine("병원 P0", $"현재 UPS · {upsMinutes}분 뒤 diesel 자동 절체", WarningColor);
            AddCopy($"UPS {upsMinutes}분과 diesel {dieselMinutes}분은 P0를 무공백으로 지킵니다. 하지만 utility 인도·판매는 끊겼고 보상경계는 그대로입니다.", 13, 58);
        }

        AddCopy("지도에서 ×와 파선은 사건으로 제거된 설비, 굵은 실선은 실제 살아 있는 utility 공급경로입니다.", 12, 38, MutedTextColor);
        var eventHours = (_event.EndMinute - _event.StartMinute) / 60;
        _advanceButton = AddActionButton(
            $"{eventHours}시간 사건 진행 및 복구",
            "사건 복구 시각까지 진행합니다. 내부 UPS 고갈은 별도 정지 없이 자동 정산됩니다.",
            OnAdvancePressed,
            false);
        AddErrorLine();
    }

    private void BuildFinalPanel()
    {
        AddSectionTitle("5 / 5  복구·결산");
        var hospitalHadOutage = _snapshot.Interval.UtilityUnservedKwMinuteByLoad[_hospitalLoadId] > 0;
        AddStatusLine(
            "utility",
            hospitalHadOutage ? "마을·병원 복구" : "마을 복구 · 병원 공급 유지",
            AccentColor);
        AddCopy("사건 구간 결산", 14, 23);
        AddLedgerRow("전력 판매", _snapshot.Interval.RevenueCashUnit, false);
        AddLedgerRow("가스 변동비", _snapshot.Interval.GasCostCashUnit, true);
        AddLedgerRow("미공급 보상", _snapshot.Interval.CompensationCashUnit, true);
        AddStatusLine(
            "LostSales · 진단값",
            $"{FormatMoney(_snapshot.Interval.LostSalesCashUnit)} · 현금 미반영",
            MutedTextColor);
        var eventCashDelta = checked(
            _snapshot.Interval.RevenueCashUnit -
            _snapshot.Interval.GasCostCashUnit -
            _snapshot.Interval.CompensationCashUnit);
        AddStatusLine("사건 현금 변화", FormatSignedMoney(eventCashDelta), eventCashDelta < 0 ? ErrorColor : AccentColor);
        AddStatusLine("현재 현금", FormatMoney(_snapshot.Cash), TextColor);

        var usedMinutes = _snapshot.Interval.HospitalInternalUsedKwMinute / _scenario.HospitalInternalPower.RatedPowerKw;
        var remainingMinutes = _snapshot.HospitalInternalRemainingKwMinute / _scenario.HospitalInternalPower.RatedPowerKw;
        if (usedMinutes > 0)
        {
            var upsMinutes = InternalStageMinutes(0);
            var dieselUsed = usedMinutes - upsMinutes;
            AddCopy($"병원 P0: UPS {upsMinutes}분 → diesel {dieselUsed}분으로 무공백 유지. 내부전원 {remainingMinutes}분 잔여.", 13, 48);
        }
        else
        {
            AddCopy($"병원 P0: utility 경로가 유지되어 내부전원은 사용하지 않음. 내부전원 {remainingMinutes}분 잔여.", 13, 48);
        }

        AddCopy("병원 내부전원이 P0를 지킨 것과 전력회사가 병원에 전기를 인도·판매한 것은 별도입니다. LostSales는 보상 외에 현금에서 다시 빼지 않는 진단값입니다.", 12, 52, MutedTextColor);

        if (!_finalLogged)
        {
            _diagnostic?.Write("FINAL", true, SnapshotHash(_snapshot), new EmptyPayload());
            _finalLogged = true;
        }
    }

    private void OnTownOrderPressed()
    {
        var result = _session.OrderTownFeeder();
        LogCommand("OrderTownFeeder", result);
        ApplyCommandResult(result);
    }

    private void OnAdvancePressed()
    {
        var result = _session.AdvanceToNextMilestone();
        LogCommand("AdvanceToNextMilestone", result);
        ApplyCommandResult(result);
    }

    private void OnPredictionToggled(PredictionKey key, PredictionValue value, bool pressed)
    {
        if (!pressed || _predictionsLocked)
        {
            return;
        }

        _predictions[key] = value;
        SetError(string.Empty);
        UpdateDecisionEnablement();
    }

    private void OnCorridorToggled(CorridorDesign corridor, bool pressed)
    {
        if (!pressed || _predictionsLocked)
        {
            return;
        }

        if (_predictions.Count != 4)
        {
            SetError("INPUT_INCOMPLETE · 네 예측을 먼저 입력하세요.");
            return;
        }

        _selectedCorridor = corridor;
        SetError(string.Empty);
        UpdateDecisionEnablement();
    }

    private void OnConfirmPressed()
    {
        if (_predictions.Count != 4 || _selectedCorridor is null)
        {
            SetError("INPUT_INCOMPLETE · 네 예측과 회랑 계획 하나를 모두 선택하세요.");
            return;
        }

        var selected = _selectedCorridor.Value;
        var result = _session.OrderCorridor(selected);
        LogCommand("OrderCorridor", result);
        if (!result.Accepted)
        {
            SetError(CommandErrorText(result.ErrorCode));
            return;
        }

        _snapshot = result.PublicSnapshot;
        _predictionsLocked = true;
        _diagnostic?.Write(
            "PREDICTION_LOCKED",
            true,
            SnapshotHash(_snapshot),
            new PredictionPayload(
                PredictionMachineValue(new PredictionKey(CorridorDesign.RiverParallel, PredictionCase.Electrical)),
                PredictionMachineValue(new PredictionKey(CorridorDesign.RiverParallel, PredictionCase.Spatial)),
                PredictionMachineValue(new PredictionKey(CorridorDesign.NorthDetour, PredictionCase.Electrical)),
                PredictionMachineValue(new PredictionKey(CorridorDesign.NorthDetour, PredictionCase.Spatial)),
                CorridorMachineValue(selected)));

        foreach (var design in new[] { CorridorDesign.RiverParallel, CorridorDesign.NorthDetour })
        {
            var evaluationDesign = design == CorridorDesign.RiverParallel
                ? EvaluationDesign.RiverParallel
                : EvaluationDesign.NorthDetour;
            _reveal[(design, _electricalCase.Id)] = _session.EvaluateRemoval(evaluationDesign, _electricalCase.Id);
            _reveal[(design, _spatialCase.Id)] = _session.EvaluateRemoval(evaluationDesign, _spatialCase.Id);
        }

        _revealOpened = true;
        Render();
        _diagnostic?.Write("REVEAL_OPENED", true, SnapshotHash(_snapshot), new EmptyPayload());
    }

    private void ApplyCommandResult(CommandResult result)
    {
        if (!result.Accepted)
        {
            SetError(CommandErrorText(result.ErrorCode));
            return;
        }

        _snapshot = result.PublicSnapshot;
        Render();
    }

    private void LogCommand(string commandName, CommandResult result)
    {
        _diagnostic?.Write(
            "COMMAND",
            result.Accepted,
            SnapshotHash(result.PublicSnapshot),
            new CommandPayload(commandName, result.ErrorCode is null ? null : CommandErrorMachineValue(result.ErrorCode.Value)));
    }

    private void UpdateDecisionEnablement()
    {
        var predictionsComplete = _predictions.Count == 4;
        foreach (var button in _corridorButtons.Values)
        {
            button.Disabled = !predictionsComplete || _predictionsLocked;
        }

        if (_confirmButton is not null)
        {
            _confirmButton.Disabled = !predictionsComplete || _selectedCorridor is null || _predictionsLocked;
        }
    }

    private void UpdateTimeline()
    {
        var constructionMinute = ResolveConstructionMarkerMinute();
        var constructionName = _snapshot.Minute < _townProject.AllowedOrderMinute + _townProject.BuildMinutes
            ? "마을 피더 예상 완공"
            : "회랑 예상 완공";
        var markers = new List<TimelineMarker>
        {
            new(_snapshot.Minute, TimelineMarkerKind.Current, $"● 현재 {CurrentTimeLabel()}"),
            new(constructionMinute, TimelineMarkerKind.Construction, $"■ {constructionName} {TimeLabel(constructionMinute)}"),
            new(_requirement.DeadlineMinute, TimelineMarkerKind.Deadline, $"◆ 병원 2회로 기한 {TimeLabel(_requirement.DeadlineMinute)}"),
            new(_event.StartMinute, TimelineMarkerKind.EventStart, $"▲ 강변 사건 {TimeLabel(_event.StartMinute)}"),
            new(_event.EndMinute, TimelineMarkerKind.Recovery, $"▣ 복구 {TimeLabel(_event.EndMinute)}"),
        };
        _timelineView.SetMarkers(markers, _event.EndMinute);

        foreach (var child in _timelineLabels.GetChildren())
        {
            _timelineLabels.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var marker in markers)
        {
            var label = NewLabel(marker.Text, 12, 58, marker.Kind == TimelineMarkerKind.Current ? TextColor : MutedTextColor);
            label.CustomMinimumSize = new Vector2(242, 58);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.AccessibilityName = marker.Text;
            _timelineLabels.AddChild(label);
        }
    }

    private int ResolveConstructionMarkerMinute()
    {
        if (_snapshot.Minute < _townProject.AllowedOrderMinute + _townProject.BuildMinutes)
        {
            return _townProject.AllowedOrderMinute + _townProject.BuildMinutes;
        }

        if (_snapshot.SelectedCorridor is { } corridor)
        {
            var project = _corridorProjects[corridor];
            return project.AllowedOrderMinute + project.BuildMinutes;
        }

        var completionMinutes = _corridorProjects.Values
            .Select(project => project.AllowedOrderMinute + project.BuildMinutes)
            .Distinct()
            .ToArray();
        if (completionMinutes.Length != 1)
        {
            throw new FixtureValidationException("Both corridor options must share one advertised completion minute.");
        }

        return completionMinutes[0];
    }

    private void ClearRightBody()
    {
        foreach (var child in _rightBody.GetChildren())
        {
            _rightBody.RemoveChild(child);
            child.QueueFree();
        }

        _focusControls.Clear();
        _predictionButtons.Clear();
        _corridorButtons.Clear();
        _townOrderButton = null;
        _advanceButton = null;
        _confirmButton = null;
        _errorLabel = null;
    }

    private void AddSectionTitle(string text)
    {
        _rightBody.AddChild(NewLabel(text, 21, 30, TextColor));
    }

    private void AddCopy(string text, int fontSize, float minimumHeight, Color? color = null)
    {
        _rightBody.AddChild(NewLabel(text, fontSize, minimumHeight, color ?? TextColor));
    }

    private void AddStatusLine(string name, string value, Color color)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 28) };
        row.AddChild(NewLabel(name, 13, 26, MutedTextColor, 132));
        row.AddChild(NewLabel(value, 13, 26, color, 325));
        _rightBody.AddChild(row);
    }

    private void AddLedgerRow(string name, long amount, bool cost)
    {
        var prefix = cost ? "− " : "+ ";
        var color = cost ? ErrorColor : AccentColor;
        AddStatusLine(name, prefix + FormatMoney(amount), color);
    }

    private Button AddActionButton(
        string text,
        string description,
        Action handler,
        bool disabled)
    {
        var button = new Button
        {
            Text = text,
            Disabled = disabled,
            FocusMode = FocusModeEnum.All,
            AccessibilityName = text,
            AccessibilityDescription = description,
            CustomMinimumSize = new Vector2(0, 34),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        button.Pressed += handler;
        _rightBody.AddChild(button);
        _focusControls.Add(button);
        return button;
    }

    private static CheckButton NewCheckButton(string text, string description)
    {
        return new CheckButton
        {
            Text = text,
            FocusMode = FocusModeEnum.All,
            AccessibilityName = text,
            AccessibilityDescription = description,
            CustomMinimumSize = new Vector2(0, 28),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
    }

    private void AddErrorLine()
    {
        _errorLabel = NewLabel(string.Empty, 12, 18, ErrorColor);
        _errorLabel.AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Assertive;
        _rightBody.AddChild(_errorLabel);
    }

    private void SetError(string text)
    {
        if (_errorLabel is not null)
        {
            _errorLabel.Text = text;
            _errorLabel.AccessibilityName = text;
        }
    }

    private void WireFocusOrder()
    {
        if (_focusControls.Count == 0)
        {
            return;
        }

        for (var index = 0; index < _focusControls.Count; index++)
        {
            var current = _focusControls[index];
            var previous = _focusControls[(index + _focusControls.Count - 1) % _focusControls.Count];
            var next = _focusControls[(index + 1) % _focusControls.Count];
            var previousPath = current.GetPathTo(previous);
            var nextPath = current.GetPathTo(next);
            current.FocusPrevious = previousPath;
            current.FocusNext = nextPath;
            current.FocusNeighborTop = previousPath;
            current.FocusNeighborLeft = previousPath;
            current.FocusNeighborBottom = nextPath;
            current.FocusNeighborRight = nextPath;
        }
    }

    private void GrabFirstActionFocus()
    {
        var first = _focusControls.FirstOrDefault(control =>
            GodotObject.IsInstanceValid(control) && control.IsInsideTree() && control.Visible &&
            (control is not BaseButton button || !button.Disabled));
        first?.GrabFocus();
    }

    private async void RunSmoke()
    {
        try
        {
            RequireButton(_townOrderButton, "town order").EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            RequireButton(_advanceButton, "advance to decision").EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            foreach (var key in AllPredictionKeys())
            {
                _predictionButtons[key][PredictionValue.Remains].ButtonPressed = true;
            }

            _corridorButtons[_orderedCorridors[0]].ButtonPressed = true;
            RequireButton(_confirmButton, "prediction confirmation").EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            RequireButton(_advanceButton, "advance to commissioning").EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            RequireButton(_advanceButton, "advance to event").EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            RequireButton(_advanceButton, "advance to final").EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            if (!_snapshot.IsComplete || !_finalLogged)
            {
                throw new InvalidOperationException("Smoke flow did not reach the final scene boundary.");
            }

            GD.Print($"SCOPE0B_SMOKE_PASS session={_options.SessionId} variant={_options.Variant} finalSnapshotHash={SnapshotHash(_snapshot)}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"SCOPE0B_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static BaseButton RequireButton(BaseButton? button, string description)
    {
        return button ?? throw new InvalidOperationException($"Missing enabled UI handler for {description}.");
    }

    private IEnumerable<PredictionKey> AllPredictionKeys()
    {
        foreach (var corridor in new[] { CorridorDesign.RiverParallel, CorridorDesign.NorthDetour })
        {
            yield return new PredictionKey(corridor, PredictionCase.Electrical);
            yield return new PredictionKey(corridor, PredictionCase.Spatial);
        }
    }

    private string StageLabel()
    {
        if (_snapshot.IsComplete)
        {
            return "복구·결산";
        }

        if (_snapshot.EventRemovedEdgeIds.Count > 0)
        {
            return "강변 사건";
        }

        if (_snapshot.SelectedCorridor is not null)
        {
            return "공사·의무";
        }

        return _snapshot.Minute >= _corridorProjects.Values.Min(project => project.AllowedOrderMinute)
            ? "회랑 결정"
            : "마을 접속";
    }

    private string PredictionText(PredictionKey key) =>
        _predictions[key] == PredictionValue.Remains ? "남음" : "끊김";

    private string PredictionMachineValue(PredictionKey key) =>
        _predictions[key] == PredictionValue.Remains ? "remains" : "cut";

    private static string OutcomeText(RemovalEvaluation evaluation) =>
        evaluation.HospitalUtilityDelivered ? "남음" : "끊김";

    private bool UtilityDelivered(string loadId) =>
        _snapshot.UtilityPathByLoad.TryGetValue(loadId, out var path) && path is not null;

    private int InternalStageMinutes(int index)
    {
        var stage = _scenario.HospitalInternalPower.Stages[index];
        return checked((int)(stage.EnergyKwMinute / _scenario.HospitalInternalPower.RatedPowerKw));
    }

    private string CurrentTimeLabel() => TimeLabel(_snapshot.Minute);

    private string TimeLabel(int minute) =>
        _scenario.Milestones.Single(milestone => milestone.Minute == minute).Label;

    private static string CorridorShortName(CorridorDesign design) => design switch
    {
        CorridorDesign.RiverParallel => "강변",
        CorridorDesign.NorthDetour => "북부",
        _ => throw new ArgumentOutOfRangeException(nameof(design)),
    };

    private static string CorridorLongName(CorridorDesign design) => design switch
    {
        CorridorDesign.RiverParallel => "강변 병렬선",
        CorridorDesign.NorthDetour => "북부 우회선",
        _ => throw new ArgumentOutOfRangeException(nameof(design)),
    };

    private static string CorridorLocation(CorridorDesign design) => design switch
    {
        CorridorDesign.RiverParallel => "기존 강변 통로 안",
        CorridorDesign.NorthDetour => "강변과 떨어진 북부 경로",
        _ => throw new ArgumentOutOfRangeException(nameof(design)),
    };

    private static string CorridorMachineValue(CorridorDesign design) => design switch
    {
        CorridorDesign.RiverParallel => "RIVER_PARALLEL",
        CorridorDesign.NorthDetour => "NORTH_DETOUR",
        _ => throw new ArgumentOutOfRangeException(nameof(design)),
    };

    private static string CommandErrorMachineValue(CommandErrorCode code) => code switch
    {
        CommandErrorCode.WrongTime => "WRONG_TIME",
        CommandErrorCode.RequiredActionPending => "REQUIRED_ACTION_PENDING",
        CommandErrorCode.AlreadyOrdered => "ALREADY_ORDERED",
        CommandErrorCode.NoNextMilestone => "NO_NEXT_MILESTONE",
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };

    private static string CommandErrorText(CommandErrorCode? code) => code switch
    {
        CommandErrorCode.WrongTime => "WRONG_TIME · 이 시각에는 발주할 수 없습니다.",
        CommandErrorCode.RequiredActionPending => "REQUIRED_ACTION_PENDING · 현재 필수 행동을 먼저 완료하세요.",
        CommandErrorCode.AlreadyOrdered => "ALREADY_ORDERED · 이미 발주된 공사입니다.",
        CommandErrorCode.NoNextMilestone => "NO_NEXT_MILESTONE · 더 진행할 이정표가 없습니다.",
        null => "명령이 거부되었습니다.",
        _ => "알 수 없는 명령 오류입니다.",
    };

    private static string FormatMoney(long cashUnit)
    {
        var millions = cashUnit / 1_000_000m;
        return $"{millions.ToString("0.###", CultureInfo.InvariantCulture)} M";
    }

    private static string FormatSignedMoney(long cashUnit) =>
        cashUnit < 0 ? $"− {FormatMoney(-cashUnit)}" : $"+ {FormatMoney(cashUnit)}";

    private static MapPoint ToMapPoint(Position position) => new((float)position.X, (float)position.Y);

    private static string SnapshotHash(PublicSnapshot snapshot) => SnapshotJson.Sha256Hex(snapshot);

    private static string ComputeBuildHash()
    {
        var gameDirectory = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        var repositoryRoot = new DirectoryInfo(gameDirectory).Parent?.FullName
            ?? throw new InvalidOperationException("Game directory has no repository parent.");
        var coreDirectory = Path.Combine(repositoryRoot, "src", "Gridworks.Core");
        var components = new List<string>
        {
            Path.Combine(repositoryRoot, "Directory.Build.props"),
            Path.Combine(repositoryRoot, "global.json"),
            Path.Combine(coreDirectory, "Gridworks.Core.csproj"),
            Path.Combine(gameDirectory, "Gridworks.Game.csproj"),
            Path.Combine(gameDirectory, "Main.tscn"),
            Path.Combine(gameDirectory, "project.godot"),
        };
        components.AddRange(Directory.EnumerateFiles(coreDirectory, "*.cs"));
        components.AddRange(Directory.EnumerateFiles(gameDirectory, "*.cs"));

        var manifest = new StringBuilder();
        foreach (string path in components
                     .Select(Path.GetFullPath)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(path => Path.GetRelativePath(repositoryRoot, path), StringComparer.Ordinal))
        {
            var label = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Build-hash component '{label}' was not found.", path);
            }

            manifest.Append(label)
                .Append(':')
                .Append(LowerHex(SHA256.HashData(File.ReadAllBytes(path))))
                .Append('\n');
        }

        return LowerHex(SHA256.HashData(Encoding.UTF8.GetBytes(manifest.ToString())));
    }

    private static string LowerHex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    private static Panel NewPanel(Rect2 rect)
    {
        var style = new StyleBoxFlat { BgColor = PanelColor, BorderColor = PanelBorderColor };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(3);
        var panel = new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static Label NewLabel(string text, int fontSize, float minimumHeight, Color color, float minimumWidth = 0)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(minimumWidth, minimumHeight),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            AccessibilityName = text,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Label NewGridHeader(string text, float width)
    {
        var label = NewLabel(text, 11, 32, MutedTextColor, width);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    private void ShowFatalError(string message)
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var background = new ColorRect
        {
            Color = BackgroundColor,
            Position = Vector2.Zero,
            Size = new Vector2(1280, 720),
        };
        AddChild(background);
        var label = NewLabel($"FIXTURE_INVALID\n\n{message}", 22, 300, ErrorColor, 1000);
        label.Position = new Vector2(140, 180);
        label.Size = new Vector2(1000, 300);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        background.AddChild(label);
    }

    private sealed record ReadyPayload(
        [property: System.Text.Json.Serialization.JsonPropertyName("buildHash")] string BuildHash,
        [property: System.Text.Json.Serialization.JsonPropertyName("fixtureHash")] string FixtureHash);

    private sealed record CommandPayload(
        [property: System.Text.Json.Serialization.JsonPropertyName("commandName")] string CommandName,
        [property: System.Text.Json.Serialization.JsonPropertyName("errorCode")] string? ErrorCode);

    private sealed record PredictionPayload(
        [property: System.Text.Json.Serialization.JsonPropertyName("RIVER_E1")] string RiverE1,
        [property: System.Text.Json.Serialization.JsonPropertyName("RIVER_OLD")] string RiverOld,
        [property: System.Text.Json.Serialization.JsonPropertyName("NORTH_E1")] string NorthE1,
        [property: System.Text.Json.Serialization.JsonPropertyName("NORTH_OLD")] string NorthOld,
        [property: System.Text.Json.Serialization.JsonPropertyName("selectedCorridor")] string SelectedCorridor);

    private sealed record EmptyPayload;

    private readonly record struct PredictionKey(CorridorDesign Corridor, PredictionCase Case);

    private enum PredictionCase
    {
        Electrical,
        Spatial,
    }

    private enum PredictionValue
    {
        Remains,
        Cut,
    }
}
