namespace Gridworks.Core.Product;

public sealed partial class ProductSession
{
    private ProductMaintenanceChoice _maintenanceChoice;
    private ProductProjectState _maintenanceState;
    private long? _maintenanceCompletionMinute;
    private long? _heatwaveStartMinute;
    private long? _heatwaveRecoveryMinute;
    private bool _heatwaveStarted;
    private bool _heatwaveActive;
    private bool _agedFactoryFeederUnavailableDuringEvent;
    private FactoryDispatch _heatwaveDispatch = FactoryDispatch.Empty;
    private ProductHeatwaveSettlementSnapshot _heatwaveSettlement = EmptyHeatwaveSettlement();

    public ProductOrderPreview PreviewPreventiveMaintenanceOrder()
    {
        if (CurrentPhase() != ProductPhase.MaintenanceDecision)
        {
            return EmptyOrderPreview(ProductCommandError.WrongPhase);
        }

        ProductPreventiveMaintenanceDefinition maintenance = MaintenanceDefinition();
        long completionMinute = checked(_minute + maintenance.BuildMinutes);
        ProductCommandError? error = maintenance.CostCashUnit > _cash
            ? ProductCommandError.InsufficientCash
            : null;
        return new ProductOrderPreview(
            error is null,
            error,
            maintenance.CostCashUnit,
            maintenance.BuildMinutes,
            completionMinute,
            null,
            maintenance.ProjectId);
    }

    public ProductCommandResult OrderPreventiveMaintenance()
    {
        ProductOrderPreview preview = PreviewPreventiveMaintenanceOrder();
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }

        _cash = checked(_cash - preview.CostCashUnit!.Value);
        _maintenanceChoice = ProductMaintenanceChoice.Ordered;
        _maintenanceState = ProductProjectState.Building;
        _maintenanceCompletionMinute = preview.CompletionMinute;
        return Accepted();
    }

    public ProductCommandResult SkipPreventiveMaintenance()
    {
        if (CurrentPhase() != ProductPhase.MaintenanceDecision)
        {
            return Rejected(ProductCommandError.WrongPhase);
        }

        _maintenanceChoice = ProductMaintenanceChoice.Skipped;
        return Accepted();
    }

    public ProductCommandResult AdvanceToHeatwave()
    {
        if (CurrentPhase() != ProductPhase.HeatwaveReady)
        {
            return Rejected(ProductCommandError.WrongPhase);
        }

        ProductHeatwaveDefinition heatwave = HeatwaveDefinition();
        bool feederUnavailable = _maintenanceChoice == ProductMaintenanceChoice.Skipped;
        _minute = _heatwaveStartMinute
            ?? throw new InvalidOperationException("Heatwave forecast has no start minute.");
        _heatwaveStarted = true;
        _heatwaveActive = true;
        _agedFactoryFeederUnavailableDuringEvent = feederUnavailable;
        _heatwaveDispatch = ComputeFactoryDispatch(
            heatwave.TownDemandKw,
            heatwave.AgedFactoryFeederHeatwaveRatingKw,
            !feederUnavailable);
        return Accepted();
    }

    public ProductCommandResult AdvanceToHeatwaveSettlement()
    {
        if (CurrentPhase() != ProductPhase.HeatwaveActive)
        {
            return Rejected(ProductCommandError.WrongPhase);
        }

        ProductHeatwaveDefinition heatwave = HeatwaveDefinition();
        ProductHospital hospital = HospitalDefinition();
        ProductFactory factory = FactoryDefinition();
        ProductHospitalEconomy economy = HospitalEconomyDefinition();
        ProductGasPlantProjectDefinition plant = PlantDefinition();
        int duration = heatwave.DurationMinutes;

        long hospitalEnergy = checked(_heatwaveDispatch.HospitalDeliveredKw * duration);
        long townEnergy = checked(_heatwaveDispatch.TownDeliveredKw * duration);
        long factoryEnergy = checked(_heatwaveDispatch.FactoryDeliveredKw * duration);
        long existingEnergy = checked(_heatwaveDispatch.ExistingSourceDispatchKw * duration);
        long gasEnergy = checked(_heatwaveDispatch.GasPlantDispatchKw * duration);
        long possibleEnergy = checked(
            checked(checked(hospital.DemandKw + heatwave.TownDemandKw) + factory.DemandKw) *
            duration);
        long deliveredEnergy = checked(checked(hospitalEnergy + townEnergy) + factoryEnergy);
        long unservedEnergy = checked(possibleEnergy - deliveredEnergy);

        long revenue = ExactHospitalCash(
            deliveredEnergy,
            _fixture.Economy.SaleRateCashUnitPerGWh);
        long existingCost = ExactHospitalCash(
            existingEnergy,
            economy.VariableGenerationCostCashUnitPerGWh);
        long gasCost = ExactHospitalCash(
            gasEnergy,
            plant.VariableGenerationCostCashUnitPerGWh);
        long compensation = ExactHospitalCash(
            unservedEnergy,
            economy.UnservedCompensationCashUnitPerGWh);
        long lostSales = ExactHospitalCash(
            unservedEnergy,
            economy.LostSalesCashUnitPerGWh);
        long cashChange = checked(checked(checked(revenue - existingCost) - gasCost) - compensation);
        bool allSupplied = _heatwaveDispatch.HospitalDeliveredKw == hospital.DemandKw &&
            _heatwaveDispatch.TownDeliveredKw == heatwave.TownDemandKw &&
            _heatwaveDispatch.FactoryDeliveredKw == factory.DemandKw;

        _heatwaveSettlement = new ProductHeatwaveSettlementSnapshot(
            true,
            hospitalEnergy,
            townEnergy,
            factoryEnergy,
            existingEnergy,
            gasEnergy,
            unservedEnergy,
            revenue,
            existingCost,
            gasCost,
            compensation,
            lostSales,
            cashChange,
            allSupplied);
        _minute = _heatwaveRecoveryMinute
            ?? throw new InvalidOperationException("Heatwave forecast has no recovery minute.");
        _cash = checked(_cash + cashChange);
        _heatwaveActive = false;
        return Accepted();
    }

    private ProductHeatwaveSnapshot? BuildHeatwaveSnapshot()
    {
        if (!_fixture.HasHeatwaveStage)
        {
            return null;
        }

        ProductHeatwaveDefinition heatwave = HeatwaveDefinition();
        ProductFactory factory = FactoryDefinition();
        bool currentlyUnavailable = _heatwaveActive &&
            _agedFactoryFeederUnavailableDuringEvent;
        FactoryDispatch dispatch = _heatwaveStarted
            ? _heatwaveDispatch
            : _factorySettlement.Completed
                ? ComputeFactoryDispatch()
                : FactoryDispatch.Empty;
        return new ProductHeatwaveSnapshot(
            heatwave.Id,
            _heatwaveStartMinute,
            _heatwaveRecoveryMinute,
            _maintenanceChoice,
            _maintenanceState,
            _maintenanceCompletionMinute,
            _heatwaveActive,
            currentlyUnavailable,
            _agedFactoryFeederUnavailableDuringEvent,
            heatwave.TownDemandKw,
            heatwave.AgedFactoryFeederHeatwaveRatingKw,
            _heatwaveActive ? heatwave.TownDemandKw : _fixture.Town.DemandKw,
            _heatwaveActive
                ? heatwave.AgedFactoryFeederHeatwaveRatingKw
                : factory.FeederRatingKw,
            dispatch.HospitalDeliveredKw,
            dispatch.HospitalSourceAssetId,
            dispatch.TownDeliveredKw,
            dispatch.TownSourceAssetId,
            dispatch.FactoryDeliveredKw,
            dispatch.FactorySourceAssetId,
            dispatch.ExistingSourceDispatchKw,
            dispatch.GasPlantDispatchKw,
            _heatwaveSettlement);
    }

    private ProductPhase CurrentHeatwavePhase()
    {
        if (_heatwaveSettlement.Completed)
        {
            return ProductPhase.Complete;
        }
        if (_heatwaveActive)
        {
            return ProductPhase.HeatwaveActive;
        }
        if (_maintenanceChoice == ProductMaintenanceChoice.Undecided)
        {
            return ProductPhase.MaintenanceDecision;
        }
        if (_maintenanceState == ProductProjectState.Building)
        {
            return ProductPhase.MaintenanceBuilding;
        }
        return ProductPhase.HeatwaveReady;
    }

    private ProductCommandResult CompletePreventiveMaintenance()
    {
        _minute = _maintenanceCompletionMinute
            ?? throw new InvalidOperationException(
                "Building preventive maintenance has no completion minute.");
        _maintenanceState = ProductProjectState.Commissioned;
        return Accepted();
    }

    private void AnchorHeatwaveForecast()
    {
        if (!_fixture.HasHeatwaveStage || _heatwaveStartMinute is not null)
        {
            return;
        }
        ProductHeatwaveDefinition heatwave = HeatwaveDefinition();
        _heatwaveStartMinute = checked(_minute + heatwave.LeadMinutes);
        _heatwaveRecoveryMinute = checked(_heatwaveStartMinute.Value + heatwave.DurationMinutes);
    }

    private bool HeatwaveConditionsMet() =>
        _heatwaveSettlement.Completed && _heatwaveSettlement.AllLoadsFullySupplied;

    private ProductHeatwaveDefinition HeatwaveDefinition() =>
        _fixture.Heatwave
        ?? throw new InvalidOperationException("Fixture has no heatwave stage.");

    private ProductPreventiveMaintenanceDefinition MaintenanceDefinition() =>
        _fixture.PreventiveMaintenance
        ?? throw new InvalidOperationException("Fixture has no preventive maintenance project.");

    private static ProductHeatwaveSettlementSnapshot EmptyHeatwaveSettlement() =>
        new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false);

    private void ResetHeatwaveState()
    {
        _maintenanceChoice = ProductMaintenanceChoice.Undecided;
        _maintenanceState = ProductProjectState.NotOrdered;
        _maintenanceCompletionMinute = null;
        _heatwaveStartMinute = null;
        _heatwaveRecoveryMinute = null;
        _heatwaveStarted = false;
        _heatwaveActive = false;
        _agedFactoryFeederUnavailableDuringEvent = false;
        _heatwaveDispatch = FactoryDispatch.Empty;
        _heatwaveSettlement = EmptyHeatwaveSettlement();
    }
}
