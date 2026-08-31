namespace Gridworks.Core.Product;

public sealed partial class ProductSession
{
    private ProductPoint? _plantPosition;
    private string? _selectedPlantSiteId;
    private ProductProjectState _plantState;
    private long? _plantCompletionMinute;
    private readonly List<ProductPoint> _plantConnectionSupportPositions = [];
    private ProductProjectState _plantConnectionState;
    private long? _plantConnectionCompletionMinute;
    private ProductFactorySettlementSnapshot _factorySettlement = EmptyFactorySettlement();

    public ProductPlantPlacementPreview PreviewPlantPlacement(ProductPoint position)
    {
        ArgumentNullException.ThrowIfNull(position);

        ProductGasPlantSite? site = PlantSites()
            .FirstOrDefault(candidate => candidate.Position == position);
        ProductCommandError? error = CurrentPhase() != ProductPhase.PlantPlanning
            ? ProductCommandError.WrongPhase
            : !ProductFixtureLoader.Contains(_fixture.MapBounds, position)
                ? ProductCommandError.OutOfBounds
                : site is null
                    ? ProductCommandError.NotBuildable
                    : _substationPosition == position
                        ? ProductCommandError.PositionOccupied
                        : null;
        return new ProductPlantPlacementPreview(
            error is null,
            error,
            position,
            error is null ? site!.SiteId : null,
            error is null ? site!.SiteCostCashUnit : null);
    }

    public ProductCommandResult SetPlantDraft(ProductPoint position)
    {
        ProductPlantPlacementPreview preview = PreviewPlantPlacement(position);
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }

        _plantPosition = position;
        _selectedPlantSiteId = preview.SiteId;
        return Accepted();
    }

    public ProductCommandResult CancelPlantDraft()
    {
        if (CurrentPhase() != ProductPhase.PlantPlanning)
        {
            return Rejected(ProductCommandError.WrongPhase);
        }
        _plantPosition = null;
        _selectedPlantSiteId = null;
        return Accepted();
    }

    public ProductOrderPreview PreviewPlantOrder()
    {
        if (CurrentPhase() != ProductPhase.PlantPlanning)
        {
            return EmptyOrderPreview(ProductCommandError.WrongPhase);
        }
        ProductGasPlantSite? site = SelectedPlantSite();
        if (_plantPosition is null || site is null)
        {
            return EmptyOrderPreview(ProductCommandError.NoDraft);
        }

        ProductGasPlantProjectDefinition plant = PlantDefinition();
        long cost = checked(plant.BaseCostCashUnit + site.SiteCostCashUnit);
        long completionMinute = checked(_minute + plant.BuildMinutes);
        ProductCommandError? error = cost > _cash
            ? ProductCommandError.InsufficientCash
            : null;
        return new ProductOrderPreview(
            error is null,
            error,
            cost,
            plant.BuildMinutes,
            completionMinute,
            null,
            plant.ProjectId);
    }

    public ProductCommandResult OrderPlant()
    {
        ProductOrderPreview preview = PreviewPlantOrder();
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }

        _cash = checked(_cash - preview.CostCashUnit!.Value);
        _plantState = ProductProjectState.Building;
        _plantCompletionMinute = preview.CompletionMinute;
        return Accepted();
    }

    public ProductCommandResult AdvanceToFactorySettlement()
    {
        if (CurrentPhase() != ProductPhase.FactorySettlementReady)
        {
            return Rejected(ProductCommandError.WrongPhase);
        }

        FactoryDispatch dispatch = ComputeFactoryDispatch();
        int duration = _fixture.FactorySettlementMinutes
            ?? throw new InvalidOperationException("Factory settlement duration is missing.");
        ProductFactory factory = FactoryDefinition();
        ProductHospital hospital = HospitalDefinition();
        ProductHospitalEconomy economy = HospitalEconomyDefinition();
        ProductGasPlantProjectDefinition plant = PlantDefinition();

        long hospitalEnergy = checked(dispatch.HospitalDeliveredKw * duration);
        long townEnergy = checked(dispatch.TownDeliveredKw * duration);
        long factoryEnergy = checked(dispatch.FactoryDeliveredKw * duration);
        long existingEnergy = checked(dispatch.ExistingSourceDispatchKw * duration);
        long gasEnergy = checked(dispatch.GasPlantDispatchKw * duration);
        long possibleEnergy = checked(
            checked(checked(hospital.DemandKw + _fixture.Town.DemandKw) + factory.DemandKw) *
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
        bool allSupplied = dispatch.HospitalDeliveredKw == hospital.DemandKw &&
            dispatch.TownDeliveredKw == _fixture.Town.DemandKw &&
            dispatch.FactoryDeliveredKw == factory.DemandKw;

        _factorySettlement = new ProductFactorySettlementSnapshot(
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
        _minute = checked(_minute + duration);
        _cash = checked(_cash + cashChange);
        if (allSupplied)
        {
            AnchorHeatwaveForecast();
        }
        return Accepted();
    }

    private ProductFactorySnapshot? BuildFactorySnapshot()
    {
        if (!_fixture.HasFactoryStage)
        {
            return null;
        }

        FactoryDispatch dispatch = ComputeFactoryDispatch();
        return new ProductFactorySnapshot(
            FactoryDefinition().Id,
            _selectedPlantSiteId,
            _plantPosition,
            _plantState,
            _plantCompletionMinute,
            new ProductLineSnapshot(
                Array.AsReadOnly(_plantConnectionSupportPositions.ToArray()),
                _plantConnectionState,
                _plantConnectionCompletionMinute),
            _plantState == ProductProjectState.Commissioned &&
                _plantConnectionState == ProductProjectState.Commissioned,
            dispatch.HospitalDeliveredKw,
            dispatch.HospitalSourceAssetId,
            dispatch.TownDeliveredKw,
            dispatch.TownSourceAssetId,
            dispatch.FactoryDeliveredKw,
            dispatch.FactorySourceAssetId,
            dispatch.ExistingSourceDispatchKw,
            dispatch.GasPlantDispatchKw,
            _factorySettlement);
    }

    private ProductPhase CurrentFactoryPhase()
    {
        if (_plantState == ProductProjectState.NotOrdered)
        {
            return ProductPhase.PlantPlanning;
        }
        if (_plantState == ProductProjectState.Building)
        {
            return ProductPhase.PlantBuilding;
        }
        return _plantConnectionState switch
        {
            ProductProjectState.NotOrdered => ProductPhase.PlantConnectionPlanning,
            ProductProjectState.Building => ProductPhase.PlantConnectionBuilding,
            ProductProjectState.Commissioned => ProductPhase.FactorySettlementReady,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private bool IsPlantConnectionPlanningPhase() =>
        CurrentPhase() == ProductPhase.PlantConnectionPlanning;

    private ProductLineSupportPreview PreviewPlantConnectionSupport(ProductPoint position)
    {
        ProductLineProjectDefinition definition = PlantConnectionDefinition();
        ProductPoint from = LastPlantConnectionEndpoint();
        long distanceSquared = SafeDistanceSquared(from, position);
        long maxSpanSquared = checked(
            (long)definition.MaxSpanGridUnit * definition.MaxSpanGridUnit);
        ProductCommandError? error = !ProductFixtureLoader.Contains(_fixture.MapBounds, position)
            ? ProductCommandError.OutOfBounds
            : _blockedCells.Contains(position)
                ? ProductCommandError.NotBuildable
                : IsAnyPositionOccupied(position) ||
                    _plantConnectionSupportPositions.Contains(position)
                    ? ProductCommandError.PositionOccupied
                    : distanceSquared > maxSpanSquared
                        ? ProductCommandError.SpanTooLong
                        : null;
        return new ProductLineSupportPreview(
            error is null,
            error,
            from,
            position,
            distanceSquared,
            maxSpanSquared);
    }

    private ProductCommandResult AddPlantConnectionSupport(ProductPoint position)
    {
        ProductLineSupportPreview preview = PreviewPlantConnectionSupport(position);
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }
        _plantConnectionSupportPositions.Add(position);
        return Accepted();
    }

    private ProductCommandResult UndoPlantConnectionSupport()
    {
        if (_plantConnectionSupportPositions.Count == 0)
        {
            return Rejected(ProductCommandError.NothingToUndo);
        }
        _plantConnectionSupportPositions.RemoveAt(_plantConnectionSupportPositions.Count - 1);
        return Accepted();
    }

    private ProductCommandResult CancelPlantConnectionDraft()
    {
        _plantConnectionSupportPositions.Clear();
        return Accepted();
    }

    private ProductOrderPreview PreviewPlantConnectionOrder()
    {
        ProductLineProjectDefinition definition = PlantConnectionDefinition();
        long targetDistanceSquared = SafeDistanceSquared(
            LastPlantConnectionEndpoint(),
            _fixture.ExistingSource.Position);
        long maxSpanSquared = checked(
            (long)definition.MaxSpanGridUnit * definition.MaxSpanGridUnit);
        long supportCount = _plantConnectionSupportPositions.Count;
        long spanCount = checked(supportCount + 1);
        long cost = checked(
            checked(supportCount * definition.SupportCostCashUnit) +
            checked(spanCount * definition.SpanCostCashUnit));
        long buildMinutes = checked(
            checked(supportCount * definition.SupportBuildMinutes) +
            checked(spanCount * definition.SpanBuildMinutes));
        long completionMinute = checked(_minute + buildMinutes);
        ProductCommandError? error = targetDistanceSquared > maxSpanSquared
            ? ProductCommandError.SpanTooLong
            : cost > _cash
                ? ProductCommandError.InsufficientCash
                : null;
        return new ProductOrderPreview(
            error is null,
            error,
            cost,
            buildMinutes,
            completionMinute,
            null,
            definition.ProjectId);
    }

    private ProductCommandResult OrderPlantConnection()
    {
        ProductOrderPreview preview = PreviewPlantConnectionOrder();
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }
        _cash = checked(_cash - preview.CostCashUnit!.Value);
        _plantConnectionState = ProductProjectState.Building;
        _plantConnectionCompletionMinute = preview.CompletionMinute;
        return Accepted();
    }

    private ProductCommandResult CompleteFactoryConstruction(ProductPhase phase)
    {
        if (phase == ProductPhase.PlantBuilding)
        {
            _minute = _plantCompletionMinute
                ?? throw new InvalidOperationException("Building plant has no completion minute.");
            _plantState = ProductProjectState.Commissioned;
        }
        else
        {
            _minute = _plantConnectionCompletionMinute
                ?? throw new InvalidOperationException(
                    "Building plant connection has no completion minute.");
            _plantConnectionState = ProductProjectState.Commissioned;
        }
        return Accepted();
    }

    private FactoryDispatch ComputeFactoryDispatch() => ComputeFactoryDispatch(
        _fixture.Town.DemandKw,
        FactoryDefinition().FeederRatingKw,
        true);

    private FactoryDispatch ComputeFactoryDispatch(
        long townDemandKw,
        long factoryFeederRatingKw,
        bool factoryFeederAvailable)
    {
        if (!_fixture.HasFactoryStage || !HospitalConditionsMet())
        {
            return FactoryDispatch.Empty;
        }

        ProductHospital hospital = HospitalDefinition();
        ProductFactory factory = FactoryDefinition();
        bool hospitalPathAvailable = HospitalLineDefinitions()
            .Any(line => HospitalLineState(line) == ProductProjectState.Commissioned &&
                line.RatingKw >= hospital.DemandKw);
        bool townPathAvailable = _substationState == ProductProjectState.Commissioned &&
            _lineState == ProductProjectState.Commissioned &&
            _substationPosition is not null &&
            IsTownInServiceArea(_substationPosition) &&
            _fixture.LineProject.RatingKw >= townDemandKw &&
            _fixture.SubstationProject.CapacityKw >= townDemandKw;
        bool factoryPathAvailable = factoryFeederAvailable &&
            factoryFeederRatingKw >= factory.DemandKw;

        long existingRemaining = _fixture.ExistingSource.CapacityKw;
        bool gasAvailable = _plantState == ProductProjectState.Commissioned &&
            _plantConnectionState == ProductProjectState.Commissioned &&
            PlantConnectionDefinition().RatingKw > 0;
        long gasRemaining = gasAvailable
            ? Math.Min(
                PlantDefinition().CapacityKw,
                PlantConnectionDefinition().RatingKw)
            : 0;
        var assignments = new Dictionary<string, (long Delivered, string? Source)>(
            StringComparer.Ordinal);
        (string Id, int Priority, long Demand, bool PathAvailable)[] loads =
        [
            (hospital.Id, hospital.Priority, hospital.DemandKw, hospitalPathAvailable),
            (_fixture.Town.Id, _fixture.Town.Priority, townDemandKw, townPathAvailable),
            (factory.Id, factory.Priority, factory.DemandKw, factoryPathAvailable),
        ];

        long existingDispatch = 0;
        long gasDispatch = 0;
        foreach ((string id, _, long demand, bool pathAvailable) in loads
            .OrderBy(load => load.Priority)
            .ThenBy(load => load.Id, StringComparer.Ordinal))
        {
            if (!pathAvailable)
            {
                assignments[id] = (0, null);
            }
            else if (existingRemaining >= demand)
            {
                existingRemaining = checked(existingRemaining - demand);
                existingDispatch = checked(existingDispatch + demand);
                assignments[id] = (demand, _fixture.ExistingSource.AssetId);
            }
            else if (gasRemaining >= demand)
            {
                gasRemaining = checked(gasRemaining - demand);
                gasDispatch = checked(gasDispatch + demand);
                assignments[id] = (demand, PlantDefinition().AssetId);
            }
            else
            {
                assignments[id] = (0, null);
            }
        }

        (long hospitalDelivered, string? hospitalSource) = assignments[hospital.Id];
        (long townDelivered, string? townSource) = assignments[_fixture.Town.Id];
        (long factoryDelivered, string? factorySource) = assignments[factory.Id];
        return new FactoryDispatch(
            hospitalDelivered,
            hospitalSource,
            townDelivered,
            townSource,
            factoryDelivered,
            factorySource,
            existingDispatch,
            gasDispatch);
    }

    private ProductPoint LastPlantConnectionEndpoint() =>
        _plantConnectionSupportPositions.Count == 0
            ? _plantPosition
                ?? throw new InvalidOperationException(
                    "Plant connection planning requires a commissioned plant position.")
            : _plantConnectionSupportPositions[^1];

    private ProductGasPlantSite? SelectedPlantSite() =>
        _selectedPlantSiteId is null
            ? null
            : PlantSites().SingleOrDefault(site => site.SiteId == _selectedPlantSiteId);

    private bool IsReservedPlantSite(ProductPoint position) =>
        _fixture.GasPlantSites?.Any(site => site.Position == position) == true;

    private ProductFactory FactoryDefinition() =>
        _fixture.Factory
        ?? throw new InvalidOperationException("Fixture has no factory stage.");

    private ProductGasPlantProjectDefinition PlantDefinition() =>
        _fixture.GasPlantProject
        ?? throw new InvalidOperationException("Fixture has no gas plant project.");

    private IReadOnlyList<ProductGasPlantSite> PlantSites() =>
        _fixture.GasPlantSites
        ?? throw new InvalidOperationException("Fixture has no gas plant sites.");

    private ProductLineProjectDefinition PlantConnectionDefinition() =>
        _fixture.PlantConnectionLineProject
        ?? throw new InvalidOperationException("Fixture has no plant connection project.");

    private static ProductFactorySettlementSnapshot EmptyFactorySettlement() =>
        new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false);

    private void ResetFactoryState()
    {
        _plantPosition = null;
        _selectedPlantSiteId = null;
        _plantState = ProductProjectState.NotOrdered;
        _plantCompletionMinute = null;
        _plantConnectionSupportPositions.Clear();
        _plantConnectionState = ProductProjectState.NotOrdered;
        _plantConnectionCompletionMinute = null;
        _factorySettlement = EmptyFactorySettlement();
    }

    private sealed record FactoryDispatch(
        long HospitalDeliveredKw,
        string? HospitalSourceAssetId,
        long TownDeliveredKw,
        string? TownSourceAssetId,
        long FactoryDeliveredKw,
        string? FactorySourceAssetId,
        long ExistingSourceDispatchKw,
        long GasPlantDispatchKw)
    {
        internal static FactoryDispatch Empty { get; } =
            new(0, null, 0, null, 0, null, 0, 0);
    }
}
