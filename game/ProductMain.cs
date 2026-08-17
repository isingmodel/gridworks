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
    private ProductHospital _hospitalFixture = null!;
    private ProductSpatialIncident _incidentFixture = null!;
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
            GetWindow().Title = "Gridworks — 두 번째 심장";
            _options = ProductLaunchOptions.Parse(OS.GetCmdlineUserArgs());

            string fixturePath = Path.GetFullPath(Path.Combine(
                ProjectSettings.GlobalizePath("res://"),
                "..",
                "data",
                "product-second-heart-v1.json"));
            byte[] fixtureBytes = File.ReadAllBytes(fixturePath);
            _fixtureHash = LowerHex(SHA256.HashData(fixtureBytes));
            _buildHash = ComputeBuildHash();
            _fixture = ProductFixtureLoader.Load(fixtureBytes);
            _hospitalFixture = _fixture.Hospital
                ?? throw new InvalidOperationException("Second Heart fixture is missing the hospital.");
            _incidentFixture = _fixture.SpatialIncident
                ?? throw new InvalidOperationException("Second Heart fixture is missing the spatial incident.");
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
            GD.PushError($"Second Heart startup failed: {exception}");
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
        _taskPanel.SettleRequested += OnMilestoneRequested;
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
            _ when IsLinePlanning(_snapshot.Phase) =>
                ToMapPreview(_session.PreviewLineSupport(productPoint)),
            _ => null,
        };
        Render();
    }

    private void OnPointRequested(FirstLightGridPoint point)
    {
        ProductCommandResult? result = _snapshot.Phase switch
        {
            ProductPhase.SubstationPlanning => _session.SetSubstationDraft(ToProduct(point)),
            _ when IsLinePlanning(_snapshot.Phase) => _session.AddLineSupport(ToProduct(point)),
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
            _ when IsLinePlanning(_snapshot.Phase) => _session.CancelLineDraft(),
            _ => null,
        };
        if (result is null)
        {
            return;
        }
        string commandName = _snapshot.Phase == ProductPhase.SubstationPlanning
            ? "CANCEL_SUBSTATION_DRAFT"
            : "CANCEL_LINE_DRAFT";
        ApplyCommand(commandName, result);
    }

    private void OnUndoRequested() =>
        ApplyCommand("UNDO_LINE_SUPPORT", _session.UndoLineSupport());

    private void OnOrderRequested()
    {
        ProductCommandResult? result = _snapshot.Phase switch
        {
            ProductPhase.SubstationPlanning => _session.OrderSubstation(),
            _ when IsLinePlanning(_snapshot.Phase) => _session.OrderLine(),
            _ => null,
        };
        if (result is null)
        {
            return;
        }
        string commandName = _snapshot.Phase == ProductPhase.SubstationPlanning
            ? "ORDER_SUBSTATION"
            : "ORDER_LINE";
        ApplyCommand(commandName, result);
    }

    private void OnAdvanceRequested() =>
        ApplyCommand("ADVANCE_TO_CONSTRUCTION_COMPLETION", _session.AdvanceToConstructionCompletion());

    private void OnMilestoneRequested()
    {
        (string Name, ProductCommandResult Result)? command = _snapshot.Phase switch
        {
            ProductPhase.SettlementReady =>
                ("ADVANCE_TO_SETTLEMENT", _session.AdvanceToSettlement()),
            ProductPhase.IncidentReady =>
                ("ADVANCE_TO_INCIDENT", _session.AdvanceToIncident()),
            ProductPhase.IncidentActive =>
                ("ADVANCE_TO_RECOVERY_AND_SETTLEMENT", _session.AdvanceToRecoveryAndSettlement()),
            _ => null,
        };
        if (command.HasValue)
        {
            ApplyCommand(command.Value.Name, command.Value.Result);
        }
    }

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
            activeProjectId = ActiveProjectId(_snapshot),
            supportCount = ActiveSupports(_snapshot).Count,
        });
        Render();
    }

    private void Render()
    {
        _snapshot = _session.GetSnapshot();
        ProductHospitalSnapshot hospital = HospitalSnapshot();
        string phaseText = CurrentPhaseText(hospital);
        _phaseLabel.Text = phaseText;
        _phaseLabel.AccessibilityName = $"현재 단계 {phaseText}";
        _timeLabel.Text = $"시각 {_snapshot.Minute.ToString("N0", CultureInfo.InvariantCulture)}분";
        _cashLabel.Text = $"현금 {CashText(_snapshot.Cash)}";
        _demandLabel.Text =
            $"마을 {PowerText(DisplayTownUtility(hospital))} / {PowerText(_fixture.Town.DemandKw)} · " +
            $"병원 {PowerText(hospital.HospitalUtilityKw)} / {PowerText(_hospitalFixture.DemandKw)} · " +
            $"P0 {PowerText(hospital.HospitalP0DeliveredKw)}";

        _mapView.SetModel(BuildMapModel(hospital));
        _taskPanel.SetModel(BuildPanelModel(hospital));

        if (_snapshot.Phase == ProductPhase.Complete && !_finalLogged)
        {
            ProductHospitalSettlementSnapshot ledger = hospital.Settlement;
            if (!ledger.Completed)
            {
                _diagnostic?.WriteFinal(new
                {
                    outcome = Machine(_snapshot.Outcome),
                    firstLight = new
                    {
                        supplyFailure = Machine(_snapshot.SupplyFailure),
                        deliveredEnergyKwMinute = _snapshot.Settlement.DeliveredEnergyKwMinute,
                        revenueCashUnit = _snapshot.Settlement.RevenueCashUnit,
                        endingCashUnit = _snapshot.Cash,
                    },
                });
            }
            else
            {
                _diagnostic?.WriteFinal(new
                {
                    outcome = Machine(_snapshot.Outcome),
                    hardConditions = new
                    {
                        singleLineRemoval = ledger.SingleLineRemovalConditionMet,
                        spatialIncidentUtility = ledger.SpatialIncidentUtilityConditionMet,
                        hospitalP0 = ledger.HospitalP0ConditionMet,
                    },
                    removedProjectIds = hospital.Incident.UnavailableProjectIds,
                    utility = new
                    {
                        hospitalKw = hospital.Incident.HospitalUtilityKw,
                        townKw = hospital.Incident.TownUtilityKw,
                        hospitalP0DeliveredKw = hospital.HospitalP0DeliveredKw,
                    },
                    energy = new
                    {
                        hospitalUtilityKwMinute = ledger.HospitalUtilityEnergyKwMinute,
                        townUtilityKwMinute = ledger.TownUtilityEnergyKwMinute,
                        generationKwMinute = ledger.UtilityGenerationEnergyKwMinute,
                        utilityUnservedKwMinute = ledger.UtilityUnservedEnergyKwMinute,
                        upsKwMinute = ledger.UpsEnergyKwMinute,
                        dieselKwMinute = ledger.DieselEnergyKwMinute,
                        hospitalP0UnservedKwMinute = ledger.HospitalP0UnservedEnergyKwMinute,
                    },
                    cash = new
                    {
                        revenueCashUnit = ledger.UtilityRevenueCashUnit,
                        generationCostCashUnit = ledger.GenerationCostCashUnit,
                        compensationCashUnit = ledger.UnservedCompensationCashUnit,
                        lostSalesCashUnit = ledger.LostSalesCashUnit,
                        changeCashUnit = ledger.CashChangeCashUnit,
                        endingCashUnit = _snapshot.Cash,
                    },
                });
            }
            _finalLogged = true;
        }
    }

    private FirstLightMapModel BuildMapModel(ProductHospitalSnapshot hospital)
    {
        FirstLightTargetPreview? targetPreview = null;
        if (IsLinePlanning(_snapshot.Phase))
        {
            ProductOrderPreview order = _session.PreviewLineOrder();
            IReadOnlyList<ProductPoint> supports = ActiveSupports(_snapshot);
            ProductPoint from = supports.Count == 0
                ? _fixture.ExistingSource.Position
                : supports[^1];
            targetPreview = new FirstLightTargetPreview(
                ToGrid(from),
                ToGrid(ActiveTarget()),
                order.Error != ProductCommandError.SpanTooLong);
        }

        bool IsUnavailable(string projectId) =>
            hospital.Incident.Active &&
            hospital.Incident.UnavailableProjectIds.Contains(projectId, StringComparer.Ordinal);

        FirstLightLineVisual[] lines =
        [
            new(
                FirstLightLineKind.Town,
                "마을 회선",
                ToGrid(_snapshot.Substation.Position ?? _fixture.ExistingSource.Position),
                _snapshot.Line.SupportPositions.Select(ToGrid).ToArray(),
                VisualState(_snapshot.Line.ProjectState, IsUnavailable(_fixture.LineProject.ProjectId)),
                _snapshot.Phase == ProductPhase.LinePlanning),
            new(
                FirstLightLineKind.HospitalPrimary,
                "병원 주회선",
                ToGrid(_hospitalFixture.Position),
                hospital.PrimaryLine.SupportPositions.Select(ToGrid).ToArray(),
                VisualState(
                    hospital.PrimaryLine.ProjectState,
                    IsUnavailable(hospital.PrimaryLine.ProjectId)),
                _snapshot.Phase == ProductPhase.PrimaryPlanning),
            new(
                FirstLightLineKind.HospitalBackup,
                "병원 예비회선",
                ToGrid(_hospitalFixture.Position),
                hospital.BackupLine.SupportPositions.Select(ToGrid).ToArray(),
                VisualState(
                    hospital.BackupLine.ProjectState,
                    IsUnavailable(hospital.BackupLine.ProjectId)),
                _snapshot.Phase == ProductPhase.BackupPlanning),
        ];

        ProductRiskRect risk = _incidentFixture.RiskRect;

        return new FirstLightMapModel(
            new FirstLightGridBounds(
                _fixture.MapBounds.MinX,
                _fixture.MapBounds.MaxX,
                _fixture.MapBounds.MinY,
                _fixture.MapBounds.MaxY),
            _fixture.BlockedCells.Select(ToGrid).ToArray(),
            ToGrid(_fixture.ExistingSource.Position),
            ToGrid(_fixture.Town.Position),
            DisplayTownUtility(hospital),
            ToGrid(_hospitalFixture.Position),
            hospital.HospitalUtilityKw,
            hospital.HospitalP0DeliveredKw,
            new FirstLightRiskRect(
                new FirstLightGridPoint(risk.MinX, risk.MinY),
                new FirstLightGridPoint(risk.MaxX, risk.MaxY),
                hospital.Incident.Active),
            _snapshot.Substation.Position is null ? null : ToGrid(_snapshot.Substation.Position),
            _fixture.SubstationProject.ServiceRadiusGridUnit,
            VisualState(_snapshot.Substation.ProjectState, false),
            lines,
            _pointerPreview,
            targetPreview,
            CurrentPhaseText(hospital),
            StatusText(hospital));
    }

    private FirstLightTaskPanelModel BuildPanelModel(ProductHospitalSnapshot hospital)
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
                    ? "마을에 전기가 도착했습니다. 첫 결산 뒤 병원 회선 건설로 이어집니다."
                    : "공사는 끝났지만 마을 공급 조건을 충족하지 못했습니다. 결과를 결산한 뒤 임무를 다시 시작할 수 있습니다.";
                preview = $"첫 점등 확인 · {SupplyText(_snapshot.SupplyFailure)}";
                settle = Action(true, true, "첫 결산까지 진행", "고정된 첫 공급 기간을 진행하고 실제 인도분만 결산합니다.");
                break;
            case ProductPhase.PrimaryPlanning:
            case ProductPhase.BackupPlanning:
                {
                    bool primary = _snapshot.Phase == ProductPhase.PrimaryPlanning;
                    string label = primary ? "병원 주회선" : "병원 예비회선";
                    ProductOrderPreview quote = _session.PreviewLineOrder();
                    instruction = primary
                        ? "병원 주회선의 지지물을 순서대로 놓으세요. 위험구역 노출은 발주 전에 표시됩니다."
                        : "주회선과 support를 공유하지 않는 예비회선을 만드세요. 공간 우회는 더 길고 비쌀 수 있습니다.";
                    preview = HospitalLinePreviewText(quote);
                    cancel = Action(true, true, $"{label} 초안 전체 취소", "놓은 지지물을 모두 지웁니다.");
                    undo = Action(
                        true,
                        ActiveSupports(_snapshot).Count > 0,
                        "마지막 지지물 되돌리기",
                        "가장 마지막에 놓은 지지물 하나를 지웁니다.");
                    order = Action(
                        true,
                        quote.Accepted,
                        quote.CostCashUnit.HasValue
                            ? $"{label} 발주 · {CashText(quote.CostCashUnit.Value)}"
                            : $"{label} 발주",
                        "현재 지지물 순서로 회선 공사를 발주합니다.");
                    break;
                }
            case ProductPhase.PrimaryBuilding:
                instruction = "병원 주회선 공사가 진행 중입니다. 완공 전에는 병원 경로로 쓸 수 없습니다.";
                preview = CompletionText(hospital.PrimaryLine.CompletionMinute);
                advance = Action(true, true, "주회선 완공까지 진행", "주회선 전체의 완공 시각으로 진행합니다.");
                break;
            case ProductPhase.BackupBuilding:
                instruction = "병원 예비회선 공사가 진행 중입니다. 완공 뒤 단일회선 제거 결과를 확인합니다.";
                preview = CompletionText(hospital.BackupLine.CompletionMinute);
                advance = Action(true, true, "예비회선 완공까지 진행", "예비회선 전체의 완공 시각으로 진행합니다.");
                break;
            case ProductPhase.IncidentReady:
                instruction =
                    "두 회선이 완공됐습니다. 각 회선을 하나씩 제거한 결과를 확인하고 고정 공간사건을 시작하세요.";
                preview = ReliabilityText(_session.PreviewReliability());
                settle = Action(true, true, "고정 공간사건 시작", "사건 시작 경계로 진행하고 닿는 회선을 사용불가로 만듭니다.");
                break;
            case ProductPhase.IncidentActive:
                instruction =
                    "공간사건이 진행 중입니다. 사용불가 회선과 병원 utility·P0를 확인하고 복구·결산까지 진행하세요.";
                preview = IncidentText(hospital);
                settle = Action(true, true, "복구·결산까지 진행", "사건을 적분하고 회선을 복구한 뒤 경제를 결산합니다.");
                break;
            case ProductPhase.Complete:
                if (!hospital.Settlement.Completed)
                {
                    instruction =
                        "첫 점등에서 마을 공급 조건을 충족하지 못해 병원 공사를 시작하지 않았습니다. 표시된 원인을 확인하고 임무를 다시 시작할 수 있습니다.";
                    preview =
                        $"첫 결산 · {SupplyText(_snapshot.SupplyFailure)}\n" +
                        $"매출 {CashText(_snapshot.Settlement.RevenueCashUnit)} · 기말 현금 {CashText(_snapshot.Cash)}";
                }
                else
                {
                    instruction = _snapshot.Outcome == ProductMissionOutcome.Success
                        ? "두 번째 심장 완료. 단일회선 제거, 실제 공간사건 utility, 병원 P0 조건을 모두 지켰습니다."
                        : "두 번째 심장 실패. 세 안전 조건을 각각 확인하고 전체 임무를 다시 시작할 수 있습니다.";
                    preview = FinalLedgerText(hospital.Settlement);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return new FirstLightTaskPanelModel(
            CurrentPhaseText(hospital),
            instruction,
            preview,
            StatusText(hospital),
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

    private string HospitalLinePreviewText(ProductOrderPreview quote)
    {
        if (_pointerPreview?.Mode == FirstLightPointerMode.LineSupport)
        {
            return _pointerPreview.Description;
        }
        string quoteText = quote.CostCashUnit.HasValue
            ? $"견적 {CashText(quote.CostCashUnit.Value)} · 공기 {quote.BuildMinutes!.Value.ToString("N0", CultureInfo.InvariantCulture)}분"
            : string.Empty;
        string exposure = quote.SpatialIncidentExposed switch
        {
            true => "공간사건 노출 · 있음",
            false => "공간사건 노출 · 없음",
            null => "공간사건 노출 · 경로를 완성하면 확인 가능",
        };
        string error = quote.Error.HasValue ? ErrorText(quote.Error) : string.Empty;
        return string.Join("\n", new[] { quoteText, exposure, error }.Where(text => text.Length > 0));
    }

    private static string ReliabilityText(ProductReliabilitySnapshot reliability) =>
        reliability.Evaluated
            ? $"주회선 제거 시 병원 utility · {YesNo(reliability.PrimaryRemovalKeepsHospitalUtility)}\n" +
              $"예비회선 제거 시 병원 utility · {YesNo(reliability.BackupRemovalKeepsHospitalUtility)}"
            : "두 회선이 완공되면 단일회선 제거 결과를 확인할 수 있습니다.";

    private static string IncidentText(ProductHospitalSnapshot hospital)
    {
        string unavailable = hospital.Incident.UnavailableProjectIds.Count == 0
            ? "사용불가 회선 · 없음"
            : $"사용불가 회선 · {string.Join(", ", hospital.Incident.UnavailableProjectIds)}";
        return
            $"{unavailable}\n병원 utility {PowerText(hospital.HospitalUtilityKw)} · " +
            $"P0 {PowerText(hospital.HospitalP0DeliveredKw)}\n" +
            $"마을 utility {PowerText(hospital.TownUtilityKw)}";
    }

    private static string FinalLedgerText(ProductHospitalSettlementSnapshot ledger) =>
        $"단일회선 제거 {YesNo(ledger.SingleLineRemovalConditionMet)} · " +
        $"공간사건 utility {YesNo(ledger.SpatialIncidentUtilityConditionMet)} · " +
        $"병원 P0 {YesNo(ledger.HospitalP0ConditionMet)}\n" +
        $"매출 {CashText(ledger.UtilityRevenueCashUnit)} · 발전비 {CashText(ledger.GenerationCostCashUnit)} · " +
        $"미공급 보상 {CashText(ledger.UnservedCompensationCashUnit)}\n" +
        $"현금 변화 {SignedCashText(ledger.CashChangeCashUnit)} · LostSales {CashText(ledger.LostSalesCashUnit)} (현금 미반영)\n" +
        $"UPS {EnergyText(ledger.UpsEnergyKwMinute)} · 디젤 {EnergyText(ledger.DieselEnergyKwMinute)} · " +
        $"P0 미공급 {EnergyText(ledger.HospitalP0UnservedEnergyKwMinute)}";

    private string StatusText(ProductHospitalSnapshot hospital)
    {
        if (!IsHospitalDisplayStage(_snapshot.Phase, hospital))
        {
            return SupplyText(_snapshot.SupplyFailure);
        }
        string reliability = hospital.Reliability.Evaluated
            ? $" · 단일회선 제거 안전 {YesNo(hospital.Reliability.AllSingleLineRemovalsKeepHospitalUtility)}"
            : string.Empty;
        return
            $"병원 utility {PowerText(hospital.HospitalUtilityKw)} / {PowerText(_hospitalFixture.DemandKw)} · " +
            $"P0 {PowerText(hospital.HospitalP0DeliveredKw)}{reliability}\n" +
            $"마을 utility {PowerText(hospital.TownUtilityKw)} / {PowerText(_fixture.Town.DemandKw)}";
    }

    private static string OrderPreviewText(ProductOrderPreview quote)
    {
        string projected = quote.ProjectedSupplyFailure.HasValue
            ? ProjectedSupplyText(quote.ProjectedSupplyFailure.Value)
            : "예상 공급을 계산할 수 없음";
        string error = quote.Error.HasValue ? ErrorText(quote.Error) : string.Empty;
        if (!quote.CostCashUnit.HasValue)
        {
            return error.Length > 0 ? error : projected;
        }
        string quoteText =
            $"견적 {CashText(quote.CostCashUnit.Value)} · 공기 {quote.BuildMinutes!.Value.ToString("N0", CultureInfo.InvariantCulture)}분";
        return error.Length > 0
            ? $"{quoteText}\n{error}\n{projected}"
            : $"{quoteText}\n{projected}";
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
            await ClickMapPoint(finalSubstation);
            Require(
                _snapshot.Phase == ProductPhase.SubstationPlanning &&
                _snapshot.Substation.Position == ToProduct(finalSubstation),
                "substation draft move did not round-trip through viewport input");
            EmitPanelAction(FirstLightPanelAction.Order, "order substation");
            await NextFrame();
            Require(_snapshot.Phase == ProductPhase.SubstationBuilding, "substation order failed");
            EmitPanelAction(FirstLightPanelAction.Advance, "complete substation");
            await NextFrame();
            Require(_snapshot.Phase == ProductPhase.LinePlanning, "substation completion failed");

            await BuildLineThroughUi(
                _options.SmokeSupports,
                ProductPhase.LineBuilding,
                ProductPhase.SettlementReady,
                "town line");
            Require(
                _snapshot.TownDeliveredKw == _fixture.Town.DemandKw,
                "commissioned product path did not supply the town");
            EmitPanelAction(FirstLightPanelAction.Settle, "settle first light");
            await NextFrame();
            Require(_snapshot.Phase == ProductPhase.PrimaryPlanning, "first settlement did not open primary planning");

            await BuildLineThroughUi(
                _options.SmokePrimarySupports,
                ProductPhase.PrimaryBuilding,
                ProductPhase.BackupPlanning,
                "hospital primary line");
            await BuildLineThroughUi(
                _options.SmokeBackupSupports,
                ProductPhase.BackupBuilding,
                ProductPhase.IncidentReady,
                "hospital backup line");

            ProductReliabilitySnapshot reliability = _session.PreviewReliability();
            Require(
                reliability.AllSingleLineRemovalsKeepHospitalUtility,
                "hospital lines did not survive each single-line removal");
            EmitPanelAction(FirstLightPanelAction.Settle, "start spatial incident");
            await NextFrame();
            ProductHospitalSnapshot incident = HospitalSnapshot();
            Require(
                _snapshot.Phase == ProductPhase.IncidentActive &&
                incident.Incident.Active &&
                incident.HospitalUtilityKw == _hospitalFixture.DemandKw &&
                incident.HospitalP0DeliveredKw == _hospitalFixture.DemandKw,
                "spatial incident did not preserve hospital utility and P0");

            EmitPanelAction(FirstLightPanelAction.Settle, "recover and settle incident");
            await NextFrame();
            Require(
                _snapshot.Phase == ProductPhase.Complete &&
                _snapshot.Outcome == ProductMissionOutcome.Success &&
                _finalLogged,
                "Second Heart smoke did not reach a logged successful settlement");

            GD.Print(
                $"PRODUCT_SECOND_HEART_SMOKE_PASS session={_options.SessionId} endingCash={_snapshot.Cash}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"PRODUCT_SECOND_HEART_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task BuildLineThroughUi(
        IReadOnlyList<FirstLightGridPoint> supports,
        ProductPhase buildingPhase,
        ProductPhase completedPhase,
        string description)
    {
        foreach (FirstLightGridPoint support in supports)
        {
            await ClickMapPoint(support);
        }
        Require(
            ActiveSupports(_snapshot).Select(ToGrid).SequenceEqual(supports),
            $"{description} support clicks did not round-trip through viewport input");
        EmitPanelAction(FirstLightPanelAction.Order, $"order {description}");
        await NextFrame();
        Require(_snapshot.Phase == buildingPhase, $"{description} order failed");
        EmitPanelAction(FirstLightPanelAction.Advance, $"complete {description}");
        await NextFrame();
        Require(_snapshot.Phase == completedPhase, $"{description} completion failed");
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

    private ProductHospitalSnapshot HospitalSnapshot() =>
        _snapshot.Hospital
        ?? throw new InvalidOperationException("Second Heart session has no hospital snapshot.");

    private ProductPoint ActiveTarget() => _snapshot.Phase switch
    {
        ProductPhase.LinePlanning => _snapshot.Substation.Position
            ?? throw new InvalidOperationException("Town line target is missing."),
        ProductPhase.PrimaryPlanning or ProductPhase.BackupPlanning => _hospitalFixture.Position,
        _ => throw new InvalidOperationException("There is no active line target."),
    };

    private static IReadOnlyList<ProductPoint> ActiveSupports(ProductSnapshot snapshot) =>
        snapshot.Phase switch
        {
            ProductPhase.LinePlanning or ProductPhase.LineBuilding => snapshot.Line.SupportPositions,
            ProductPhase.PrimaryPlanning or ProductPhase.PrimaryBuilding =>
                snapshot.Hospital?.PrimaryLine.SupportPositions ?? Array.Empty<ProductPoint>(),
            ProductPhase.BackupPlanning or ProductPhase.BackupBuilding =>
                snapshot.Hospital?.BackupLine.SupportPositions ?? Array.Empty<ProductPoint>(),
            _ => Array.Empty<ProductPoint>(),
        };

    private string? ActiveProjectId(ProductSnapshot snapshot) => snapshot.Phase switch
    {
        ProductPhase.SubstationPlanning or ProductPhase.SubstationBuilding =>
            _fixture.SubstationProject.ProjectId,
        ProductPhase.LinePlanning or ProductPhase.LineBuilding => _fixture.LineProject.ProjectId,
        _ => snapshot.Hospital?.ActiveProjectId,
    };

    private static bool IsLinePlanning(ProductPhase phase) => phase is
        ProductPhase.LinePlanning or ProductPhase.PrimaryPlanning or ProductPhase.BackupPlanning;

    private static bool IsHospitalStage(ProductPhase phase) => phase is
        ProductPhase.PrimaryPlanning or ProductPhase.PrimaryBuilding or
        ProductPhase.BackupPlanning or ProductPhase.BackupBuilding or
        ProductPhase.IncidentReady or ProductPhase.IncidentActive;

    private static bool IsHospitalDisplayStage(
        ProductPhase phase,
        ProductHospitalSnapshot hospital) =>
        IsHospitalStage(phase) ||
        phase == ProductPhase.Complete && hospital.Settlement.Completed;

    private string CurrentPhaseText(ProductHospitalSnapshot hospital) =>
        _snapshot.Phase == ProductPhase.Complete && !hospital.Settlement.Completed
            ? "6 · 첫 결산"
            : PhaseText(_snapshot.Phase);

    private long DisplayTownUtility(ProductHospitalSnapshot hospital) =>
        IsHospitalDisplayStage(_snapshot.Phase, hospital)
            ? hospital.TownUtilityKw
            : _snapshot.TownDeliveredKw;

    private static FirstLightProjectVisualState VisualState(
        ProductProjectState state,
        bool unavailable)
    {
        if (unavailable)
        {
            return FirstLightProjectVisualState.Unavailable;
        }
        return state switch
        {
            ProductProjectState.NotOrdered => FirstLightProjectVisualState.NotOrdered,
            ProductProjectState.Building => FirstLightProjectVisualState.Building,
            ProductProjectState.Commissioned => FirstLightProjectVisualState.Commissioned,
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
    }

    private static string PhaseText(ProductPhase phase) => phase switch
    {
        ProductPhase.SubstationPlanning => "1 · 변전소 계획",
        ProductPhase.SubstationBuilding => "2 · 변전소 공사",
        ProductPhase.LinePlanning => "3 · 선로 계획",
        ProductPhase.LineBuilding => "4 · 선로 공사",
        ProductPhase.SettlementReady => "5 · 공급 확인",
        ProductPhase.PrimaryPlanning => "6 · 병원 주회선 계획",
        ProductPhase.PrimaryBuilding => "7 · 병원 주회선 공사",
        ProductPhase.BackupPlanning => "8 · 병원 예비회선 계획",
        ProductPhase.BackupBuilding => "9 · 병원 예비회선 공사",
        ProductPhase.IncidentReady => "10 · 신뢰도 확인",
        ProductPhase.IncidentActive => "11 · 공간사건",
        ProductPhase.Complete => "12 · 복구와 결산",
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

    private static string ProjectedSupplyText(ProductSupplyFailure failure) => failure switch
    {
        ProductSupplyFailure.OutsideServiceArea => "완공 후 예상 미공급 · 서비스 권역 밖",
        ProductSupplyFailure.SourceCapacityInsufficient => "완공 후 예상 미공급 · 발전원 정격 부족",
        ProductSupplyFailure.LineCapacityInsufficient => "완공 후 예상 미공급 · 선로 정격 부족",
        ProductSupplyFailure.SubstationCapacityInsufficient => "완공 후 예상 미공급 · 변전소 정격 부족",
        ProductSupplyFailure.None => "완공 후 예상 공급 가능 · 경로와 서비스 권역 성립",
        ProductSupplyFailure.SubstationNotCommissioned => "완공 후 예상 미공급 · 변전소 조건 미충족",
        ProductSupplyFailure.LineNotCommissioned => "완공 후 예상 미공급 · 선로 조건 미충족",
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

    private static string SignedCashText(long cashUnit) =>
        $"{(cashUnit / 1_000_000d).ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture)} M";

    private static string EnergyText(long kwMinute) =>
        $"{(kwMinute / 60_000d).ToString("0.###", CultureInfo.InvariantCulture)} MWh";

    private static string YesNo(bool value) => value ? "충족" : "미충족";

    private static string Machine<T>(T value) where T : struct, Enum
    {
        string source = value.ToString();
        var result = new StringBuilder(source.Length + 8);
        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];
            if (index > 0 && char.IsUpper(current) &&
                (char.IsLower(source[index - 1]) ||
                 (index + 1 < source.Length && char.IsLower(source[index + 1]))))
            {
                result.Append('_');
            }
            result.Append(char.ToUpperInvariant(current));
        }
        return result.ToString();
    }

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
            Text = $"두 번째 심장을 시작할 수 없습니다.\n\n{message}",
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
