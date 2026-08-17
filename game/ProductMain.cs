using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Gridworks.Core.Product;
using Godot;

namespace Gridworks.Game;

public sealed partial class ProductMain : Control
{
    private ProductLaunchOptions _options = null!;
    private ProductDiagnosticLog? _diagnostic;
    private ProductFixture _fixture = null!;
    private ProductSession _session = null!;
    private ProductSnapshot _snapshot = null!;
    private FirstLightPointerPreview? _pointerPreview;
    private string _fixtureHash = string.Empty;
    private string _buildHash = string.Empty;
    private string _lastError = string.Empty;
    private bool _finalLogged;

    private Label _phaseLabel = null!;
    private Label _timeLabel = null!;
    private Label _cashLabel = null!;
    private Label _demandLabel = null!;
    private FirstLightMapView _mapView = null!;
    private FirstLightTaskPanel _taskPanel = null!;

    public override void _Ready()
    {
        try
        {
            GetWindow().Title = "Gridworks — 첫 점등";
            _options = ProductLaunchOptions.Parse(OS.GetCmdlineUserArgs());

            string fixturePath = Path.GetFullPath(Path.Combine(
                ProjectSettings.GlobalizePath("res://"),
                "..",
                "data",
                "product-first-light-v1.json"));
            byte[] fixtureBytes = File.ReadAllBytes(fixturePath);
            _fixtureHash = LowerHex(SHA256.HashData(fixtureBytes));
            _buildHash = ComputeBuildHash();
            _fixture = ProductFixtureLoader.Load(fixtureBytes);
            _session = new ProductSession(_fixture);
            _snapshot = _session.GetSnapshot();

            BindScene();
            _diagnostic = new ProductDiagnosticLog(_options.DiagnosticPath, _options.SessionId);
            Render();
            _diagnostic.WriteReady(new
            {
                buildHash = _buildHash,
                fixtureHash = _fixtureHash,
                phase = Machine(_snapshot.Phase),
            });

            if (_options.Smoke)
            {
                CallDeferred(nameof(RunSmoke));
            }
        }
        catch (Exception exception)
        {
            GD.PushError($"First Light startup failed: {exception}");
            ShowFatalError(exception.Message);
            if (_options?.Smoke == true || OS.GetCmdlineUserArgs().Contains("--smoke"))
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

    private void BindScene()
    {
        _phaseLabel = GetNode<Label>("%PhaseLabel");
        _timeLabel = GetNode<Label>("%TimeLabel");
        _cashLabel = GetNode<Label>("%CashLabel");
        _demandLabel = GetNode<Label>("%DemandLabel");
        _mapView = GetNode<FirstLightMapView>("%FirstLightMapView");
        _taskPanel = GetNode<FirstLightTaskPanel>("%FirstLightTaskPanel");

        _mapView.PointerChanged += OnPointerChanged;
        _mapView.PointRequested += OnPointRequested;
        _taskPanel.CancelDraftRequested += OnCancelDraftRequested;
        _taskPanel.UndoRequested += OnUndoRequested;
        _taskPanel.OrderRequested += OnOrderRequested;
        _taskPanel.AdvanceRequested += OnAdvanceRequested;
        _taskPanel.SettleRequested += OnSettleRequested;
        _taskPanel.RestartRequested += OnRestartRequested;
    }

    private void OnPointerChanged(FirstLightGridPoint? point)
    {
        if (!point.HasValue)
        {
            _pointerPreview = null;
            Render();
            return;
        }

        ProductPoint productPoint = ToProduct(point.Value);
        _pointerPreview = _snapshot.Phase switch
        {
            ProductPhase.SubstationPlanning => ToMapPreview(
                _session.PreviewSubstationPlacement(productPoint)),
            ProductPhase.LinePlanning => ToMapPreview(_session.PreviewLineSupport(productPoint)),
            _ => null,
        };
        Render();
    }

    private void OnPointRequested(FirstLightGridPoint point)
    {
        ProductCommandResult? result = _snapshot.Phase switch
        {
            ProductPhase.SubstationPlanning => _session.SetSubstationDraft(ToProduct(point)),
            ProductPhase.LinePlanning => _session.AddLineSupport(ToProduct(point)),
            _ => null,
        };
        if (result is null)
        {
            return;
        }

        string commandName = _snapshot.Phase == ProductPhase.SubstationPlanning
            ? "SET_SUBSTATION_DRAFT"
            : "ADD_LINE_SUPPORT";
        ApplyCommand(commandName, result);
    }

    private void OnCancelDraftRequested()
    {
        ProductCommandResult? result = _snapshot.Phase switch
        {
            ProductPhase.SubstationPlanning => _session.CancelSubstationDraft(),
            ProductPhase.LinePlanning => _session.CancelLineDraft(),
            _ => null,
        };
        if (result is null)
        {
            return;
        }
        string commandName = _snapshot.Phase == ProductPhase.LinePlanning
            ? "CANCEL_LINE_DRAFT"
            : "CANCEL_SUBSTATION_DRAFT";
        ApplyCommand(commandName, result);
    }

    private void OnUndoRequested() =>
        ApplyCommand("UNDO_LINE_SUPPORT", _session.UndoLineSupport());

    private void OnOrderRequested()
    {
        ProductCommandResult? result = _snapshot.Phase switch
        {
            ProductPhase.SubstationPlanning => _session.OrderSubstation(),
            ProductPhase.LinePlanning => _session.OrderLine(),
            _ => null,
        };
        if (result is null)
        {
            return;
        }
        string commandName = _snapshot.Phase == ProductPhase.LinePlanning
            ? "ORDER_LINE"
            : "ORDER_SUBSTATION";
        ApplyCommand(commandName, result);
    }

    private void OnAdvanceRequested() =>
        ApplyCommand("ADVANCE_TO_CONSTRUCTION_COMPLETION", _session.AdvanceToConstructionCompletion());

    private void OnSettleRequested() =>
        ApplyCommand("ADVANCE_TO_SETTLEMENT", _session.AdvanceToSettlement());

    private void OnRestartRequested()
    {
        ProductCommandResult result = _session.RestartMission();
        _finalLogged = false;
        ApplyCommand("RESTART_MISSION", result);
    }

    private void ApplyCommand(string commandName, ProductCommandResult result)
    {
        _snapshot = result.Snapshot;
        _pointerPreview = null;
        _lastError = result.Accepted ? string.Empty : ErrorText(result.Error);
        _diagnostic?.WriteCommand(result.Accepted, new
        {
            commandName,
            errorCode = result.Error.HasValue ? Machine(result.Error.Value) : null,
            phase = Machine(_snapshot.Phase),
            supportCount = _snapshot.Line.SupportPositions.Count,
        });
        Render();
    }

    private void Render()
    {
        _snapshot = _session.GetSnapshot();
        _phaseLabel.Text = PhaseText(_snapshot.Phase);
        _phaseLabel.AccessibilityName = $"현재 단계 {PhaseText(_snapshot.Phase)}";
        _timeLabel.Text = $"시각 {_snapshot.Minute.ToString("N0", CultureInfo.InvariantCulture)}분";
        _cashLabel.Text = $"현금 {CashText(_snapshot.Cash)}";
        _demandLabel.Text =
            $"마을 공급 {PowerText(_snapshot.TownDeliveredKw)} / {PowerText(_fixture.Town.DemandKw)}";

        _mapView.SetModel(BuildMapModel());
        _taskPanel.SetModel(BuildPanelModel());

        if (_snapshot.Phase == ProductPhase.Complete && !_finalLogged)
        {
            _diagnostic?.WriteFinal(new
            {
                outcome = Machine(_snapshot.Outcome),
                endingCash = _snapshot.Cash,
                deliveredEnergyKwMinute = _snapshot.Settlement.DeliveredEnergyKwMinute,
                revenueCashUnit = _snapshot.Settlement.RevenueCashUnit,
                supportCount = _snapshot.Line.SupportPositions.Count,
            });
            _finalLogged = true;
        }
    }

    private FirstLightMapModel BuildMapModel()
    {
        FirstLightTargetPreview? targetPreview = null;
        if (_snapshot.Phase == ProductPhase.LinePlanning && _snapshot.Substation.Position is not null)
        {
            ProductOrderPreview order = _session.PreviewLineOrder();
            ProductPoint from = _snapshot.Line.SupportPositions.Count == 0
                ? _fixture.ExistingSource.Position
                : _snapshot.Line.SupportPositions[^1];
            targetPreview = new FirstLightTargetPreview(
                ToGrid(from),
                ToGrid(_snapshot.Substation.Position),
                order.Error != ProductCommandError.SpanTooLong);
        }

        return new FirstLightMapModel(
            new FirstLightGridBounds(
                _fixture.MapBounds.MinX,
                _fixture.MapBounds.MaxX,
                _fixture.MapBounds.MinY,
                _fixture.MapBounds.MaxY),
            _fixture.BlockedCells.Select(ToGrid).ToArray(),
            ToGrid(_fixture.ExistingSource.Position),
            ToGrid(_fixture.Town.Position),
            _fixture.Town.DemandKw,
            _snapshot.TownDeliveredKw,
            _snapshot.Substation.Position is null ? null : ToGrid(_snapshot.Substation.Position),
            _fixture.SubstationProject.ServiceRadiusGridUnit,
            VisualState(_snapshot.Substation.ProjectState),
            _snapshot.Line.SupportPositions.Select(ToGrid).ToArray(),
            VisualState(_snapshot.Line.ProjectState),
            _pointerPreview,
            targetPreview,
            PhaseText(_snapshot.Phase),
            SupplyText(_snapshot.SupplyFailure));
    }

    private FirstLightTaskPanelModel BuildPanelModel()
    {
        FirstLightActionPresentation hidden = Action(false, false, string.Empty, string.Empty);
        FirstLightActionPresentation cancel = hidden;
        FirstLightActionPresentation undo = hidden;
        FirstLightActionPresentation order = hidden;
        FirstLightActionPresentation advance = hidden;
        FirstLightActionPresentation settle = hidden;

        string instruction;
        string preview;
        switch (_snapshot.Phase)
        {
            case ProductPhase.SubstationPlanning:
                {
                    ProductOrderPreview quote = _session.PreviewSubstationOrder();
                    instruction =
                        "지도에서 배전 변전소의 초안을 놓으세요. 서비스 권역은 접속 가능 범위이며, 선로가 완공돼야 실제 공급됩니다.";
                    preview = SubstationPreviewText(quote);
                    cancel = Action(true, true, "변전소 초안 취소", "현재 변전소 초안을 지웁니다.");
                    order = Action(
                        true,
                        quote.Accepted,
                        quote.CostCashUnit.HasValue
                            ? $"변전소 발주 · {CashText(quote.CostCashUnit.Value)}"
                            : "변전소 발주",
                        "현재 초안 위치에 변전소 공사를 발주합니다.");
                    break;
                }
            case ProductPhase.SubstationBuilding:
                instruction = "변전소 공사가 발주됐습니다. 공사 중인 설비는 아직 전기를 전달하지 않습니다.";
                preview = CompletionText(_snapshot.Substation.CompletionMinute);
                advance = Action(true, true, "변전소 완공까지 진행", "현재 변전소 공사의 완공 시각으로 진행합니다.");
                break;
            case ProductPhase.LinePlanning:
                {
                    ProductOrderPreview quote = _session.PreviewLineOrder();
                    instruction =
                        "기존 발전원에서 완공된 변전소까지 이어지도록 지지물을 순서대로 놓으세요. 마지막 span도 거리 제한 안에 있어야 합니다.";
                    preview = LinePreviewText(quote);
                    cancel = Action(true, true, "선로 초안 전체 취소", "놓은 지지물을 모두 지웁니다.");
                    undo = Action(
                        true,
                        _snapshot.Line.SupportPositions.Count > 0,
                        "마지막 지지물 되돌리기",
                        "가장 마지막에 놓은 지지물 하나를 지웁니다.");
                    order = Action(
                        true,
                        quote.Accepted,
                        quote.CostCashUnit.HasValue
                            ? $"선로 발주 · {CashText(quote.CostCashUnit.Value)}"
                            : "선로 발주",
                        "현재 지지물 순서로 선로 공사를 발주합니다.");
                    break;
                }
            case ProductPhase.LineBuilding:
                instruction = "선로 공사가 발주됐습니다. 모든 span은 완공 전까지 통전되지 않습니다.";
                preview = CompletionText(_snapshot.Line.CompletionMinute);
                advance = Action(true, true, "선로 완공까지 진행", "선로 전체의 완공 시각으로 진행합니다.");
                break;
            case ProductPhase.SettlementReady:
                instruction = _snapshot.SupplyFailure == ProductSupplyFailure.None
                    ? "변전소와 선로가 완공됐고 마을에 전기가 도착합니다. 첫 공급 기간을 결산하세요."
                    : "공사는 끝났지만 마을 공급 조건을 충족하지 못했습니다. 결과를 결산한 뒤 임무를 다시 시작할 수 있습니다.";
                preview = $"예상 결과 · {SupplyText(_snapshot.SupplyFailure)}";
                settle = Action(true, true, "첫 결산까지 진행", "고정된 첫 공급 기간을 진행하고 실제 인도분만 결산합니다.");
                break;
            case ProductPhase.Complete:
                instruction = _snapshot.Outcome == ProductMissionOutcome.Success
                    ? "첫 점등 완료. 발전원에서 마을까지 완공된 경로와 서비스 권역이 함께 성립했습니다."
                    : "공사는 끝났지만 마을을 공급하지 못했습니다. 표시된 첫 실패 원인을 확인하고 임무를 다시 시작할 수 있습니다.";
                preview =
                    $"결산 · 매출 {CashText(_snapshot.Settlement.RevenueCashUnit)} · 기말 현금 {CashText(_snapshot.Cash)}";
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return new FirstLightTaskPanelModel(
            PhaseText(_snapshot.Phase),
            instruction,
            preview,
            SupplyText(_snapshot.SupplyFailure),
            _lastError,
            cancel,
            undo,
            order,
            advance,
            settle,
            Action(true, true, "현재 임무 다시 시작", "모든 진행을 지우고 최초 상태로 돌아갑니다."));
    }

    private string SubstationPreviewText(ProductOrderPreview quote)
    {
        if (_pointerPreview?.Mode == FirstLightPointerMode.Substation)
        {
            return _pointerPreview.Description;
        }
        if (quote.Error == ProductCommandError.NoDraft)
        {
            return "초안을 배치하면 비용·공기와 예상 공급 조건을 확인할 수 있습니다.";
        }
        return OrderPreviewText(quote);
    }

    private string LinePreviewText(ProductOrderPreview quote)
    {
        if (_pointerPreview?.Mode == FirstLightPointerMode.LineSupport)
        {
            return _pointerPreview.Description;
        }
        return OrderPreviewText(quote);
    }

    private static string OrderPreviewText(ProductOrderPreview quote)
    {
        string projected = quote.ProjectedSupplyFailure.HasValue
            ? SupplyText(quote.ProjectedSupplyFailure.Value)
            : "예상 공급을 계산할 수 없음";
        if (!quote.CostCashUnit.HasValue)
        {
            return quote.Error.HasValue ? ErrorText(quote.Error) : projected;
        }
        return
            $"견적 {CashText(quote.CostCashUnit.Value)} · 공기 {quote.BuildMinutes!.Value.ToString("N0", CultureInfo.InvariantCulture)}분\n{projected}";
    }

    private static string CompletionText(long? completionMinute) => completionMinute.HasValue
        ? $"예정 완공 · {completionMinute.Value.ToString("N0", CultureInfo.InvariantCulture)}분"
        : "예정 완공시각 없음";

    private static FirstLightActionPresentation Action(
        bool visible,
        bool enabled,
        string text,
        string description) => new(visible, enabled, text, description);

    private FirstLightPointerPreview ToMapPreview(ProductSubstationPlacementPreview preview)
    {
        string description = !preview.Accepted
            ? ErrorText(preview.Error)
            : preview.TownInServiceArea
                ? "이 위치는 마을의 서비스 권역 조건을 만족합니다. 실제 공급에는 완공된 선로도 필요합니다."
                : "배치는 가능하지만 마을이 서비스 권역 밖이라 완공 뒤에도 공급되지 않습니다.";
        return new FirstLightPointerPreview(
            FirstLightPointerMode.Substation,
            ToGrid(preview.Position),
            null,
            preview.Accepted,
            0,
            0,
            description);
    }

    private static FirstLightPointerPreview ToMapPreview(ProductLineSupportPreview preview)
    {
        string description = preview.Accepted
            ? $"span 거리² {preview.DistanceSquared} / 허용 {preview.MaxSpanSquared} · 배치 가능"
            : ErrorText(preview.Error);
        return new FirstLightPointerPreview(
            FirstLightPointerMode.LineSupport,
            ToGrid(preview.To),
            ToGrid(preview.From),
            preview.Accepted,
            preview.DistanceSquared,
            preview.MaxSpanSquared,
            description);
    }

    private async void RunSmoke()
    {
        try
        {
            await NextFrame();
            FirstLightGridPoint firstSubstation = _options.SmokeSubstations[0];
            FirstLightGridPoint finalSubstation = _options.SmokeSubstations[1];

            await ClickMapPoint(firstSubstation);
            Require(
                _snapshot.Phase == ProductPhase.SubstationPlanning &&
                _snapshot.Substation.Position == ToProduct(firstSubstation),
                "first substation draft did not round-trip through viewport input");

            await ClickMapPoint(finalSubstation);
            Require(
                _snapshot.Substation.Position == ToProduct(finalSubstation),
                "substation draft move did not round-trip through viewport input");

            EmitPanelAction(FirstLightPanelAction.CancelDraft, "cancel substation draft");
            await NextFrame();
            Require(_snapshot.Substation.Position is null, "substation draft cancel failed");

            await ClickMapPoint(finalSubstation);
            EmitPanelAction(FirstLightPanelAction.Order, "order substation");
            await NextFrame();
            Require(
                _snapshot.Phase == ProductPhase.SubstationBuilding &&
                _snapshot.TownDeliveredKw == 0,
                "substation order must enter building without supply");

            EmitPanelAction(FirstLightPanelAction.Advance, "complete substation");
            await NextFrame();
            Require(_snapshot.Phase == ProductPhase.LinePlanning, "substation completion failed");

            foreach (FirstLightGridPoint support in _options.SmokeSupports)
            {
                await ClickMapPoint(support);
            }
            Require(
                _snapshot.Line.SupportPositions.Count == _options.SmokeSupports.Count,
                "initial support clicks did not round-trip through viewport input");

            EmitPanelAction(FirstLightPanelAction.Undo, "undo line support");
            await NextFrame();
            Require(
                _snapshot.Line.SupportPositions.Count == _options.SmokeSupports.Count - 1,
                "line support undo failed");

            await ClickMapPoint(_options.SmokeSupports[^1]);
            EmitPanelAction(FirstLightPanelAction.CancelDraft, "cancel line draft");
            await NextFrame();
            Require(_snapshot.Line.SupportPositions.Count == 0, "line draft cancel failed");

            foreach (FirstLightGridPoint support in _options.SmokeSupports)
            {
                await ClickMapPoint(support);
            }
            EmitPanelAction(FirstLightPanelAction.Order, "order line");
            await NextFrame();
            Require(
                _snapshot.Phase == ProductPhase.LineBuilding &&
                _snapshot.TownDeliveredKw == 0,
                "line order must enter building without supply");

            EmitPanelAction(FirstLightPanelAction.Advance, "complete line");
            await NextFrame();
            Require(
                _snapshot.Phase == ProductPhase.SettlementReady &&
                _snapshot.TownDeliveredKw == _fixture.Town.DemandKw,
                "commissioned product path did not supply the town");

            EmitPanelAction(FirstLightPanelAction.Settle, "settle first supply period");
            await NextFrame();
            Require(
                _snapshot.Phase == ProductPhase.Complete &&
                _snapshot.Outcome == ProductMissionOutcome.Success &&
                _finalLogged,
                "First Light smoke did not reach a logged successful settlement");

            GD.Print(
                $"PRODUCT_FIRST_LIGHT_SMOKE_PASS session={_options.SessionId} endingCash={_snapshot.Cash}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"PRODUCT_FIRST_LIGHT_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task ClickMapPoint(FirstLightGridPoint point)
    {
        Vector2 viewportPoint = _mapView.ViewportPointForGridPoint(point);
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

    private void EmitPanelAction(FirstLightPanelAction action, string description)
    {
        BaseButton button = _taskPanel.GetActionButton(action);
        if (!button.Visible || button.Disabled)
        {
            throw new InvalidOperationException($"Missing enabled UI action for {description}.");
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

    private static ProductPoint ToProduct(FirstLightGridPoint point) => new(point.X, point.Y);

    private static FirstLightGridPoint ToGrid(ProductPoint point) => new(point.X, point.Y);

    private static FirstLightProjectVisualState VisualState(ProductProjectState state) => state switch
    {
        ProductProjectState.NotOrdered => FirstLightProjectVisualState.NotOrdered,
        ProductProjectState.Building => FirstLightProjectVisualState.Building,
        ProductProjectState.Commissioned => FirstLightProjectVisualState.Commissioned,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static string PhaseText(ProductPhase phase) => phase switch
    {
        ProductPhase.SubstationPlanning => "1 · 변전소 계획",
        ProductPhase.SubstationBuilding => "2 · 변전소 공사",
        ProductPhase.LinePlanning => "3 · 선로 계획",
        ProductPhase.LineBuilding => "4 · 선로 공사",
        ProductPhase.SettlementReady => "5 · 공급 확인",
        ProductPhase.Complete => "6 · 첫 결산",
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };

    private static string SupplyText(ProductSupplyFailure failure) => failure switch
    {
        ProductSupplyFailure.SubstationNotCommissioned => "마을 미공급 · 변전소가 아직 완공되지 않음",
        ProductSupplyFailure.LineNotCommissioned => "마을 미공급 · 선로가 아직 완공되지 않음",
        ProductSupplyFailure.OutsideServiceArea => "마을 미공급 · 서비스 권역 밖",
        ProductSupplyFailure.SourceCapacityInsufficient => "마을 미공급 · 발전원 정격 부족",
        ProductSupplyFailure.LineCapacityInsufficient => "마을 미공급 · 선로 정격 부족",
        ProductSupplyFailure.SubstationCapacityInsufficient => "마을 미공급 · 변전소 정격 부족",
        ProductSupplyFailure.None => "마을 공급 중 · 완공된 경로와 서비스 권역 성립",
        _ => throw new ArgumentOutOfRangeException(nameof(failure)),
    };

    private static string ErrorText(ProductCommandError? error) => error switch
    {
        ProductCommandError.WrongPhase => "WRONG_PHASE · 현재 단계에서는 실행할 수 없습니다.",
        ProductCommandError.NoDraft => "NO_DRAFT · 먼저 초안을 배치하세요.",
        ProductCommandError.OutOfBounds => "OUT_OF_BOUNDS · 지도 경계 안을 선택하세요.",
        ProductCommandError.NotBuildable => "NOT_BUILDABLE · 건설 불가 위치입니다.",
        ProductCommandError.PositionOccupied => "POSITION_OCCUPIED · 이미 사용 중인 위치입니다.",
        ProductCommandError.SpanTooLong => "SPAN_TOO_LONG · 거리 제한 안에 중간 지지물이 필요합니다.",
        ProductCommandError.NothingToUndo => "NOTHING_TO_UNDO · 되돌릴 지지물이 없습니다.",
        ProductCommandError.InsufficientCash => "INSUFFICIENT_CASH · 발주할 현금이 부족합니다.",
        null => string.Empty,
        _ => "알 수 없는 명령 오류입니다.",
    };

    private static string CashText(long cashUnit) =>
        $"{(cashUnit / 1_000_000d).ToString("0.000", CultureInfo.InvariantCulture)} M";

    private static string PowerText(long kw) =>
        $"{(kw / 1_000d).ToString("0.###", CultureInfo.InvariantCulture)} MW";

    private static string Machine<T>(T value) where T : struct, Enum =>
        value.ToString().ToUpperInvariant();

    private static string ComputeBuildHash()
    {
        string gameDirectory = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        string repositoryRoot = new DirectoryInfo(gameDirectory).Parent?.FullName
            ?? throw new InvalidOperationException("Game directory has no repository parent.");
        string coreDirectory = Path.Combine(repositoryRoot, "src", "Gridworks.Core");
        var components = new List<string>
        {
            Path.Combine(repositoryRoot, "Directory.Build.props"),
            Path.Combine(repositoryRoot, "global.json"),
            Path.Combine(coreDirectory, "Gridworks.Core.csproj"),
            Path.Combine(gameDirectory, "Gridworks.Game.csproj"),
            Path.Combine(gameDirectory, "project.godot"),
        };
        components.AddRange(Directory.EnumerateFiles(coreDirectory, "*.cs", SearchOption.AllDirectories));
        components.AddRange(Directory.EnumerateFiles(gameDirectory, "*.cs", SearchOption.TopDirectoryOnly));
        components.AddRange(Directory.EnumerateFiles(gameDirectory, "*.tscn", SearchOption.TopDirectoryOnly));

        var manifest = new StringBuilder();
        foreach (string path in components
                     .Where(path => !GeneratedPath(path))
                     .Select(Path.GetFullPath)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(path => Path.GetRelativePath(repositoryRoot, path), StringComparer.Ordinal))
        {
            string label = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
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

    private static bool GeneratedPath(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.Ordinal) ||
               normalized.Contains("/obj/", StringComparison.Ordinal) ||
               normalized.Contains("/.godot/", StringComparison.Ordinal);
    }

    private static string LowerHex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    private void ShowFatalError(string message)
    {
        var overlay = new ColorRect
        {
            Color = Color.FromHtml("071019"),
            LayoutMode = 1,
            AnchorsPreset = (int)LayoutPreset.FullRect,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both,
        };
        AddChild(overlay);
        var label = new Label
        {
            Text = $"첫 점등을 시작할 수 없습니다.\n\n{message}",
            Position = new Vector2(100f, 180f),
            Size = new Vector2(1080f, 280f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Assertive,
        };
        label.AddThemeFontSizeOverride("font_size", 20);
        overlay.AddChild(label);
    }
}
