namespace Gridworks.Core.Product;

public sealed partial class ProductSession
{
    private const long RevenueDenominator = 60_000_000;

    private readonly ProductFixture _fixture;
    private readonly HashSet<ProductPoint> _blockedCells;
    private readonly long _maxSpanSquared;
    private readonly long _serviceRadiusSquared;

    private long _minute;
    private long _cash;
    private ProductPoint? _substationPosition;
    private ProductProjectState _substationState;
    private long? _substationCompletionMinute;
    private readonly List<ProductPoint> _supportPositions = [];
    private ProductProjectState _lineState;
    private long? _lineCompletionMinute;
    private bool _settlementCompleted;
    private long _settledDeliveredEnergyKwMinute;
    private long _settledRevenueCashUnit;

    public ProductSession(ProductFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ProductFixtureLoader.Validate(fixture);

        _fixture = fixture;
        _blockedCells = fixture.BlockedCells.ToHashSet();
        _maxSpanSquared = checked(
            (long)fixture.LineProject.MaxSpanGridUnit * fixture.LineProject.MaxSpanGridUnit);
        _serviceRadiusSquared = checked(
            (long)fixture.SubstationProject.ServiceRadiusGridUnit *
            fixture.SubstationProject.ServiceRadiusGridUnit);
        ResetState();
    }

    public ProductSnapshot GetSnapshot()
    {
        ProductHospitalSnapshot? hospitalSnapshot = BuildHospitalSnapshot();
        ProductFactorySnapshot? factorySnapshot = BuildFactorySnapshot();
        bool townInServiceArea = IsTownInServiceArea(_substationPosition);
        ProductSupplyFailure supplyFailure = CurrentSupplyFailure();
        long deliveredKw = _fixture.HasHospitalStage && _settlementCompleted
            ? hospitalSnapshot?.TownUtilityKw ?? 0
            : supplyFailure == ProductSupplyFailure.None
                ? _fixture.Town.DemandKw
                : 0;
        ProductMissionOutcome outcome = CurrentOutcome();

        return new ProductSnapshot(
            _minute,
            _cash,
            CurrentPhase(),
            new ProductSubstationSnapshot(
                _substationPosition,
                _substationState,
                _substationCompletionMinute),
            new ProductLineSnapshot(
                Array.AsReadOnly(_supportPositions.ToArray()),
                _lineState,
                _lineCompletionMinute),
            townInServiceArea,
            supplyFailure,
            deliveredKw,
            new ProductSettlementSnapshot(
                _settlementCompleted,
                _settledDeliveredEnergyKwMinute,
                _settledRevenueCashUnit),
            outcome,
            hospitalSnapshot,
            factorySnapshot);
    }

    public ProductSubstationPlacementPreview PreviewSubstationPlacement(ProductPoint position)
    {
        ArgumentNullException.ThrowIfNull(position);

        ProductCommandError? error = CurrentPhase() != ProductPhase.SubstationPlanning
            ? ProductCommandError.WrongPhase
            : !ProductFixtureLoader.Contains(_fixture.MapBounds, position)
                ? ProductCommandError.OutOfBounds
                : _blockedCells.Contains(position)
                    ? ProductCommandError.NotBuildable
                    : IsStaticPositionOccupied(position)
                        ? ProductCommandError.PositionOccupied
                        : null;

        bool serviceEligible = error is null && IsTownInServiceArea(position);
        ProductSupplyFailure? projected = error is null
            ? ProjectedSupplyFailure(position)
            : null;
        return new ProductSubstationPlacementPreview(
            error is null,
            error,
            position,
            serviceEligible,
            projected);
    }

    public ProductCommandResult SetSubstationDraft(ProductPoint position)
    {
        ProductSubstationPlacementPreview preview = PreviewSubstationPlacement(position);
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }

        _substationPosition = position;
        return Accepted();
    }

    public ProductCommandResult CancelSubstationDraft()
    {
        if (CurrentPhase() != ProductPhase.SubstationPlanning)
        {
            return Rejected(ProductCommandError.WrongPhase);
        }

        _substationPosition = null;
        return Accepted();
    }

    public ProductOrderPreview PreviewSubstationOrder()
    {
        if (CurrentPhase() != ProductPhase.SubstationPlanning)
        {
            return EmptyOrderPreview(ProductCommandError.WrongPhase);
        }
        if (_substationPosition is null)
        {
            return EmptyOrderPreview(ProductCommandError.NoDraft);
        }

        long cost = _fixture.SubstationProject.CostCashUnit;
        long buildMinutes = _fixture.SubstationProject.BuildMinutes;
        long completionMinute = checked(_minute + buildMinutes);
        ProductSupplyFailure projected = ProjectedSupplyFailure(_substationPosition);
        ProductCommandError? error = cost > _cash
            ? ProductCommandError.InsufficientCash
            : null;
        return new ProductOrderPreview(
            error is null,
            error,
            cost,
            buildMinutes,
            completionMinute,
            projected);
    }

    public ProductCommandResult OrderSubstation()
    {
        ProductOrderPreview preview = PreviewSubstationOrder();
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }

        long cost = preview.CostCashUnit!.Value;
        long completionMinute = preview.CompletionMinute!.Value;
        long remainingCash = checked(_cash - cost);

        _cash = remainingCash;
        _substationState = ProductProjectState.Building;
        _substationCompletionMinute = completionMinute;
        return Accepted();
    }

    public ProductLineSupportPreview PreviewLineSupport(ProductPoint position)
    {
        ArgumentNullException.ThrowIfNull(position);

        if (IsPlantConnectionPlanningPhase())
        {
            return PreviewPlantConnectionSupport(position);
        }
        if (IsHospitalPlanningPhase())
        {
            return PreviewHospitalLineSupport(position);
        }

        ProductPoint from = LastLineEndpoint();
        long distanceSquared = SafeDistanceSquared(from, position);
        ProductCommandError? error = CurrentPhase() != ProductPhase.LinePlanning
            ? ProductCommandError.WrongPhase
            : !ProductFixtureLoader.Contains(_fixture.MapBounds, position)
                ? ProductCommandError.OutOfBounds
                : _blockedCells.Contains(position)
                    ? ProductCommandError.NotBuildable
                    : IsLinePositionOccupied(position)
                        ? ProductCommandError.PositionOccupied
                        : distanceSquared > _maxSpanSquared
                            ? ProductCommandError.SpanTooLong
                            : null;

        return new ProductLineSupportPreview(
            error is null,
            error,
            from,
            position,
            distanceSquared,
            _maxSpanSquared);
    }

    public ProductCommandResult AddLineSupport(ProductPoint position)
    {
        if (IsPlantConnectionPlanningPhase())
        {
            return AddPlantConnectionSupport(position);
        }
        if (IsHospitalPlanningPhase())
        {
            return AddHospitalLineSupport(position);
        }
        ProductLineSupportPreview preview = PreviewLineSupport(position);
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }

        _supportPositions.Add(position);
        return Accepted();
    }

    public ProductCommandResult UndoLineSupport()
    {
        if (IsPlantConnectionPlanningPhase())
        {
            return UndoPlantConnectionSupport();
        }
        if (IsHospitalPlanningPhase())
        {
            return UndoHospitalLineSupport();
        }
        if (CurrentPhase() != ProductPhase.LinePlanning)
        {
            return Rejected(ProductCommandError.WrongPhase);
        }
        if (_supportPositions.Count == 0)
        {
            return Rejected(ProductCommandError.NothingToUndo);
        }

        _supportPositions.RemoveAt(_supportPositions.Count - 1);
        return Accepted();
    }

    public ProductCommandResult CancelLineDraft()
    {
        if (IsPlantConnectionPlanningPhase())
        {
            return CancelPlantConnectionDraft();
        }
        if (IsHospitalPlanningPhase())
        {
            return CancelHospitalLineDraft();
        }
        if (CurrentPhase() != ProductPhase.LinePlanning)
        {
            return Rejected(ProductCommandError.WrongPhase);
        }

        _supportPositions.Clear();
        return Accepted();
    }

    public ProductOrderPreview PreviewLineOrder()
    {
        if (IsPlantConnectionPlanningPhase())
        {
            return PreviewPlantConnectionOrder();
        }
        if (IsHospitalPlanningPhase())
        {
            return PreviewHospitalLineOrder();
        }
        if (CurrentPhase() != ProductPhase.LinePlanning)
        {
            return EmptyOrderPreview(ProductCommandError.WrongPhase);
        }

        ProductPoint target = _substationPosition
            ?? throw new InvalidOperationException(
                "Line planning requires a commissioned substation position.");
        long targetDistanceSquared = SafeDistanceSquared(LastLineEndpoint(), target);
        (long cost, long buildMinutes) = GetLineQuote();
        long completionMinute = checked(_minute + buildMinutes);
        ProductSupplyFailure projected = ProjectedSupplyFailure(target);

        ProductCommandError? error = targetDistanceSquared > _maxSpanSquared
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
            projected);
    }

    public ProductCommandResult OrderLine()
    {
        if (IsPlantConnectionPlanningPhase())
        {
            return OrderPlantConnection();
        }
        if (IsHospitalPlanningPhase())
        {
            return OrderHospitalLine();
        }
        ProductOrderPreview preview = PreviewLineOrder();
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }

        long cost = preview.CostCashUnit!.Value;
        long completionMinute = preview.CompletionMinute!.Value;
        long remainingCash = checked(_cash - cost);

        _cash = remainingCash;
        _lineState = ProductProjectState.Building;
        _lineCompletionMinute = completionMinute;
        return Accepted();
    }

    public ProductCommandResult AdvanceToConstructionCompletion()
    {
        ProductPhase phase = CurrentPhase();
        if (phase == ProductPhase.SubstationBuilding)
        {
            _minute = _substationCompletionMinute
                ?? throw new InvalidOperationException(
                    "Building substation has no completion minute.");
            _substationState = ProductProjectState.Commissioned;
            return Accepted();
        }
        if (phase == ProductPhase.LineBuilding)
        {
            _minute = _lineCompletionMinute
                ?? throw new InvalidOperationException("Building line has no completion minute.");
            _lineState = ProductProjectState.Commissioned;
            return Accepted();
        }
        if (phase is ProductPhase.PrimaryBuilding or ProductPhase.BackupBuilding)
        {
            return CompleteHospitalLineConstruction(phase);
        }
        if (phase is ProductPhase.PlantBuilding or ProductPhase.PlantConnectionBuilding)
        {
            return CompleteFactoryConstruction(phase);
        }
        return Rejected(ProductCommandError.WrongPhase);
    }

    public ProductCommandResult AdvanceToSettlement()
    {
        if (CurrentPhase() != ProductPhase.SettlementReady)
        {
            return Rejected(ProductCommandError.WrongPhase);
        }

        ProductSupplyFailure supplyFailure = CurrentSupplyFailure();
        long deliveredKw = supplyFailure == ProductSupplyFailure.None
            ? _fixture.Town.DemandKw
            : 0;
        long deliveredEnergy = checked(deliveredKw * _fixture.SettlementMinutes);
        long revenueNumerator = checked(
            deliveredEnergy * _fixture.Economy.SaleRateCashUnitPerGWh);
        if (revenueNumerator % RevenueDenominator != 0)
        {
            throw new InvalidOperationException(
                "Settlement revenue is not exactly divisible into CashUnit.");
        }
        long revenue = revenueNumerator / RevenueDenominator;
        long settledMinute = checked(_minute + _fixture.SettlementMinutes);
        long settledCash = checked(_cash + revenue);

        _minute = settledMinute;
        _cash = settledCash;
        _settlementCompleted = true;
        _settledDeliveredEnergyKwMinute = deliveredEnergy;
        _settledRevenueCashUnit = revenue;
        return Accepted();
    }

    public ProductCommandResult RestartMission()
    {
        ResetState();
        return Accepted();
    }

    private ProductPhase CurrentPhase()
    {
        if (_factorySettlement.Completed)
        {
            return ProductPhase.Complete;
        }
        if (_hospitalSettlement.Completed)
        {
            return _fixture.HasFactoryStage && HospitalConditionsMet()
                ? CurrentFactoryPhase()
                : ProductPhase.Complete;
        }
        if (_settlementCompleted && _fixture.HasHospitalStage)
        {
            return CurrentHospitalPhase();
        }
        if (_settlementCompleted)
        {
            return ProductPhase.Complete;
        }
        return _lineState switch
        {
            ProductProjectState.Commissioned => ProductPhase.SettlementReady,
            ProductProjectState.Building => ProductPhase.LineBuilding,
            ProductProjectState.NotOrdered => _substationState switch
            {
                ProductProjectState.Commissioned => ProductPhase.LinePlanning,
                ProductProjectState.Building => ProductPhase.SubstationBuilding,
                ProductProjectState.NotOrdered => ProductPhase.SubstationPlanning,
                _ => throw new ArgumentOutOfRangeException(),
            },
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private ProductSupplyFailure CurrentSupplyFailure()
    {
        if (_substationState != ProductProjectState.Commissioned)
        {
            return ProductSupplyFailure.SubstationNotCommissioned;
        }
        if (_lineState != ProductProjectState.Commissioned)
        {
            return ProductSupplyFailure.LineNotCommissioned;
        }
        ProductPoint position = _substationPosition
            ?? throw new InvalidOperationException(
                "Commissioned substation has no position.");
        return ProjectedSupplyFailure(position);
    }

    private ProductSupplyFailure ProjectedSupplyFailure(ProductPoint substationPosition)
    {
        if (!IsTownInServiceArea(substationPosition))
        {
            return ProductSupplyFailure.OutsideServiceArea;
        }
        if (_fixture.ExistingSource.CapacityKw < _fixture.Town.DemandKw)
        {
            return ProductSupplyFailure.SourceCapacityInsufficient;
        }
        if (_fixture.LineProject.RatingKw < _fixture.Town.DemandKw)
        {
            return ProductSupplyFailure.LineCapacityInsufficient;
        }
        if (_fixture.SubstationProject.CapacityKw < _fixture.Town.DemandKw)
        {
            return ProductSupplyFailure.SubstationCapacityInsufficient;
        }
        return ProductSupplyFailure.None;
    }

    private bool IsTownInServiceArea(ProductPoint? substationPosition)
    {
        if (substationPosition is null)
        {
            return false;
        }
        return ProductFixtureLoader.DistanceSquared(
            substationPosition,
            _fixture.Town.Position) <= _serviceRadiusSquared;
    }

    private bool IsStaticPositionOccupied(ProductPoint position) =>
        position == _fixture.ExistingSource.Position ||
        position == _fixture.Town.Position ||
        position == _fixture.Hospital?.Position ||
        position == _fixture.Factory?.Position;

    private bool IsLinePositionOccupied(ProductPoint position) =>
        IsStaticPositionOccupied(position) ||
        position == _substationPosition ||
        _supportPositions.Contains(position) ||
        IsReservedPlantSite(position);

    private ProductPoint LastLineEndpoint() => _supportPositions.Count == 0
        ? _fixture.ExistingSource.Position
        : _supportPositions[^1];

    private (long Cost, long BuildMinutes) GetLineQuote()
    {
        long supportCount = _supportPositions.Count;
        long spanCount = checked(supportCount + 1);
        long cost = checked(
            checked(supportCount * _fixture.LineProject.SupportCostCashUnit) +
            checked(spanCount * _fixture.LineProject.SpanCostCashUnit));
        long buildMinutes = checked(
            checked(supportCount * _fixture.LineProject.SupportBuildMinutes) +
            checked(spanCount * _fixture.LineProject.SpanBuildMinutes));
        return (cost, buildMinutes);
    }

    private long SafeDistanceSquared(ProductPoint from, ProductPoint to)
    {
        try
        {
            return ProductFixtureLoader.DistanceSquared(from, to);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    private ProductOrderPreview EmptyOrderPreview(ProductCommandError error) =>
        new(false, error, null, null, null, null);

    private ProductCommandResult Accepted() => new(true, null, GetSnapshot());

    private ProductCommandResult Rejected(ProductCommandError error) =>
        new(false, error, GetSnapshot());

    private void ResetState()
    {
        _minute = _fixture.InitialMinute;
        _cash = _fixture.Economy.InitialCash;
        _substationPosition = null;
        _substationState = ProductProjectState.NotOrdered;
        _substationCompletionMinute = null;
        _supportPositions.Clear();
        _lineState = ProductProjectState.NotOrdered;
        _lineCompletionMinute = null;
        _settlementCompleted = false;
        _settledDeliveredEnergyKwMinute = 0;
        _settledRevenueCashUnit = 0;
        ResetHospitalState();
        ResetFactoryState();
    }
}
