namespace Gridworks.Core.Product;

public sealed partial class ProductSession
{
    private const long HospitalEnergyPerGWh = 60_000_000;

    private readonly List<ProductPoint> _primarySupportPositions = [];
    private readonly List<ProductPoint> _backupSupportPositions = [];
    private ProductProjectState _primaryLineState;
    private ProductProjectState _backupLineState;
    private long? _primaryCompletionMinute;
    private long? _backupCompletionMinute;
    private bool _primarySpatialIncidentExposed;
    private bool _backupSpatialIncidentExposed;
    private bool _incidentStarted;
    private bool _incidentActive;
    private long? _incidentStartMinute;
    private long? _incidentRecoveryMinute;
    private readonly List<string> _incidentUnavailableProjectIds = [];
    private long _incidentHospitalUtilityKw;
    private long _incidentTownUtilityKw;
    private ProductHospitalSettlementSnapshot _hospitalSettlement = EmptyHospitalSettlement();

    public ProductReliabilitySnapshot PreviewReliability()
    {
        if (!_fixture.HasHospitalStage ||
            _primaryLineState != ProductProjectState.Commissioned ||
            _backupLineState != ProductProjectState.Commissioned)
        {
            return new ProductReliabilitySnapshot(false, false, false);
        }

        ProductHospitalLineProjectDefinition primary = PrimaryDefinition();
        ProductHospitalLineProjectDefinition backup = BackupDefinition();
        bool primaryRemovalKeepsUtility =
            SelectHospitalRoute(new HashSet<string>(StringComparer.Ordinal) { primary.ProjectId })
            is not null;
        bool backupRemovalKeepsUtility =
            SelectHospitalRoute(new HashSet<string>(StringComparer.Ordinal) { backup.ProjectId })
            is not null;
        return new ProductReliabilitySnapshot(
            true,
            primaryRemovalKeepsUtility,
            backupRemovalKeepsUtility);
    }

    public ProductCommandResult AdvanceToIncident()
    {
        if (CurrentPhase() != ProductPhase.IncidentReady)
        {
            return Rejected(ProductCommandError.WrongPhase);
        }

        ProductSpatialIncident incident = IncidentDefinition();
        long startMinute = checked(_minute + incident.LeadMinutes);
        long recoveryMinute = checked(startMinute + incident.DurationMinutes);
        List<string> unavailable = [];
        if (TownLineSpatialIncidentExposed())
        {
            unavailable.Add(_fixture.LineProject.ProjectId);
        }
        if (_primarySpatialIncidentExposed)
        {
            unavailable.Add(PrimaryDefinition().ProjectId);
        }
        if (_backupSpatialIncidentExposed)
        {
            unavailable.Add(BackupDefinition().ProjectId);
        }
        unavailable.Sort(StringComparer.Ordinal);

        _minute = startMinute;
        _incidentStarted = true;
        _incidentActive = true;
        _incidentStartMinute = startMinute;
        _incidentRecoveryMinute = recoveryMinute;
        _incidentUnavailableProjectIds.Clear();
        _incidentUnavailableProjectIds.AddRange(unavailable);

        HashSet<string> unavailableSet = unavailable.ToHashSet(StringComparer.Ordinal);
        ProductHospitalLineProjectDefinition? selected = SelectHospitalRoute(unavailableSet);
        _incidentHospitalUtilityKw = selected is null ? 0 : HospitalDefinition().DemandKw;
        _incidentTownUtilityKw = ComputeTownUtilityKw(
            unavailableSet,
            _incidentHospitalUtilityKw);
        return Accepted();
    }

    public ProductCommandResult AdvanceToRecoveryAndSettlement()
    {
        if (CurrentPhase() != ProductPhase.IncidentActive)
        {
            return Rejected(ProductCommandError.WrongPhase);
        }

        ProductHospital hospital = HospitalDefinition();
        ProductSpatialIncident incident = IncidentDefinition();
        ProductHospitalEconomy economy = HospitalEconomyDefinition();
        long duration = incident.DurationMinutes;
        long hospitalUtilityEnergy = checked(_incidentHospitalUtilityKw * duration);
        long townUtilityEnergy = checked(_incidentTownUtilityKw * duration);
        long generationEnergy = checked(hospitalUtilityEnergy + townUtilityEnergy);
        long possibleEnergy = checked(checked(hospital.DemandKw + _fixture.Town.DemandKw) * duration);
        long utilityUnservedEnergy = checked(possibleEnergy - generationEnergy);

        long hospitalUtilityShortfall = checked(
            checked(hospital.DemandKw - _incidentHospitalUtilityKw) * duration);
        long upsCapacity = checked(hospital.DemandKw * hospital.UpsMinutes);
        long dieselCapacity = checked(hospital.DemandKw * hospital.DieselMinutes);
        long upsEnergy = Math.Min(hospitalUtilityShortfall, upsCapacity);
        long afterUps = checked(hospitalUtilityShortfall - upsEnergy);
        long dieselEnergy = Math.Min(afterUps, dieselCapacity);
        long p0UnservedEnergy = checked(afterUps - dieselEnergy);

        long revenue = ExactHospitalCash(
            generationEnergy,
            _fixture.Economy.SaleRateCashUnitPerGWh);
        long generationCost = ExactHospitalCash(
            generationEnergy,
            economy.VariableGenerationCostCashUnitPerGWh);
        long compensation = ExactHospitalCash(
            utilityUnservedEnergy,
            economy.UnservedCompensationCashUnitPerGWh);
        long lostSales = ExactHospitalCash(
            utilityUnservedEnergy,
            economy.LostSalesCashUnitPerGWh);
        long cashChange = checked(checked(revenue - generationCost) - compensation);
        ProductReliabilitySnapshot reliability = PreviewReliability();

        _hospitalSettlement = new ProductHospitalSettlementSnapshot(
            true,
            hospitalUtilityEnergy,
            townUtilityEnergy,
            generationEnergy,
            utilityUnservedEnergy,
            upsEnergy,
            dieselEnergy,
            p0UnservedEnergy,
            revenue,
            generationCost,
            compensation,
            lostSales,
            cashChange,
            reliability.AllSingleLineRemovalsKeepHospitalUtility,
            _incidentHospitalUtilityKw == hospital.DemandKw,
            p0UnservedEnergy == 0);
        _minute = _incidentRecoveryMinute
            ?? throw new InvalidOperationException("Active incident has no recovery minute.");
        _cash = checked(_cash + cashChange);
        _incidentActive = false;
        return Accepted();
    }

    private ProductMissionOutcome CurrentOutcome()
    {
        if (!_settlementCompleted)
        {
            return ProductMissionOutcome.Pending;
        }
        if (_settledDeliveredEnergyKwMinute == 0)
        {
            return ProductMissionOutcome.Failure;
        }
        if (!_fixture.HasHospitalStage)
        {
            return ProductMissionOutcome.Success;
        }
        if (!_hospitalSettlement.Completed)
        {
            return ProductMissionOutcome.Pending;
        }
        if (!HospitalConditionsMet())
        {
            return ProductMissionOutcome.Failure;
        }
        if (!_fixture.HasFactoryStage)
        {
            return ProductMissionOutcome.Success;
        }
        if (!_factorySettlement.Completed)
        {
            return ProductMissionOutcome.Pending;
        }
        if (!_factorySettlement.AllLoadsFullySupplied)
        {
            return ProductMissionOutcome.Failure;
        }
        if (!_fixture.HasHeatwaveStage)
        {
            return ProductMissionOutcome.Success;
        }
        if (!_heatwaveSettlement.Completed)
        {
            return ProductMissionOutcome.Pending;
        }
        return HeatwaveConditionsMet()
            ? ProductMissionOutcome.Success
            : ProductMissionOutcome.Failure;
    }

    private ProductHospitalSnapshot? BuildHospitalSnapshot()
    {
        if (!_fixture.HasHospitalStage)
        {
            return null;
        }

        ProductHospital hospital = HospitalDefinition();
        HashSet<string> unavailable = _incidentActive
            ? _incidentUnavailableProjectIds.ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        ProductHospitalLineProjectDefinition? selected = SelectHospitalRoute(unavailable);
        long hospitalUtility = selected is null ? 0 : hospital.DemandKw;
        long townUtility = ComputeTownUtilityKw(unavailable, hospitalUtility);
        long p0Delivered = hospitalUtility;
        if (_incidentActive && hospitalUtility == 0)
        {
            long internalMinutes = checked(hospital.UpsMinutes + hospital.DieselMinutes);
            if (internalMinutes > 0)
            {
                p0Delivered = hospital.DemandKw;
            }
        }

        return new ProductHospitalSnapshot(
            hospital.Id,
            ActiveHospitalDefinition()?.ProjectId,
            BuildHospitalLineSnapshot(PrimaryDefinition(), true),
            BuildHospitalLineSnapshot(BackupDefinition(), false),
            selected?.ProjectId,
            hospitalUtility,
            p0Delivered,
            townUtility,
            PreviewReliability(),
            new ProductIncidentSnapshot(
                _incidentStarted,
                _incidentActive,
                _incidentStartMinute,
                _incidentRecoveryMinute,
                Array.AsReadOnly(_incidentUnavailableProjectIds.ToArray()),
                _incidentStarted ? _incidentHospitalUtilityKw : 0,
                _incidentStarted ? _incidentTownUtilityKw : 0),
            _hospitalSettlement);
    }

    private ProductHospitalLineSnapshot BuildHospitalLineSnapshot(
        ProductHospitalLineProjectDefinition definition,
        bool primary)
    {
        IReadOnlyList<ProductPoint> supports = primary
            ? Array.AsReadOnly(_primarySupportPositions.ToArray())
            : Array.AsReadOnly(_backupSupportPositions.ToArray());
        return new ProductHospitalLineSnapshot(
            definition.ProjectId,
            supports,
            primary ? _primaryLineState : _backupLineState,
            primary ? _primaryCompletionMinute : _backupCompletionMinute,
            primary ? _primarySpatialIncidentExposed : _backupSpatialIncidentExposed);
    }

    private bool IsHospitalPlanningPhase() =>
        CurrentPhase() is ProductPhase.PrimaryPlanning or ProductPhase.BackupPlanning;

    private ProductLineSupportPreview PreviewHospitalLineSupport(ProductPoint position)
    {
        ProductHospitalLineProjectDefinition definition = ActiveHospitalDefinition()
            ?? throw new InvalidOperationException("Hospital planning has no active line definition.");
        List<ProductPoint> supports = ActiveHospitalSupports();
        ProductPoint from = supports.Count == 0
            ? _fixture.ExistingSource.Position
            : supports[^1];
        long distanceSquared = SafeDistanceSquared(from, position);
        long maxSpanSquared = checked((long)definition.MaxSpanGridUnit * definition.MaxSpanGridUnit);
        ProductCommandError? error = !ProductFixtureLoader.Contains(_fixture.MapBounds, position)
            ? ProductCommandError.OutOfBounds
            : _blockedCells.Contains(position)
                ? ProductCommandError.NotBuildable
                : IsAnyPositionOccupied(position)
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

    private ProductCommandResult AddHospitalLineSupport(ProductPoint position)
    {
        ProductLineSupportPreview preview = PreviewHospitalLineSupport(position);
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }
        ActiveHospitalSupports().Add(position);
        return Accepted();
    }

    private ProductCommandResult UndoHospitalLineSupport()
    {
        List<ProductPoint> supports = ActiveHospitalSupports();
        if (supports.Count == 0)
        {
            return Rejected(ProductCommandError.NothingToUndo);
        }
        supports.RemoveAt(supports.Count - 1);
        return Accepted();
    }

    private ProductCommandResult CancelHospitalLineDraft()
    {
        ActiveHospitalSupports().Clear();
        return Accepted();
    }

    private ProductOrderPreview PreviewHospitalLineOrder()
    {
        ProductHospitalLineProjectDefinition definition = ActiveHospitalDefinition()
            ?? throw new InvalidOperationException("Hospital planning has no active line definition.");
        List<ProductPoint> supports = ActiveHospitalSupports();
        ProductPoint from = supports.Count == 0
            ? _fixture.ExistingSource.Position
            : supports[^1];
        long targetDistanceSquared = SafeDistanceSquared(from, HospitalDefinition().Position);
        long maxSpanSquared = checked((long)definition.MaxSpanGridUnit * definition.MaxSpanGridUnit);
        long supportCount = supports.Count;
        long spanCount = checked(supportCount + 1);
        long cost = checked(
            checked(supportCount * definition.SupportCostCashUnit) +
            checked(spanCount * definition.SpanCostCashUnit));
        long buildMinutes = checked(
            checked(supportCount * definition.SupportBuildMinutes) +
            checked(spanCount * definition.SpanBuildMinutes));
        long completionMinute = checked(_minute + buildMinutes);
        bool exposed = PolylineIntersectsRisk(supports, HospitalDefinition().Position);
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
            definition.ProjectId,
            exposed);
    }

    private ProductCommandResult OrderHospitalLine()
    {
        ProductOrderPreview preview = PreviewHospitalLineOrder();
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }
        bool primary = CurrentPhase() == ProductPhase.PrimaryPlanning;
        _cash = checked(_cash - preview.CostCashUnit!.Value);
        if (primary)
        {
            _primaryLineState = ProductProjectState.Building;
            _primaryCompletionMinute = preview.CompletionMinute;
            _primarySpatialIncidentExposed = preview.SpatialIncidentExposed == true;
        }
        else
        {
            _backupLineState = ProductProjectState.Building;
            _backupCompletionMinute = preview.CompletionMinute;
            _backupSpatialIncidentExposed = preview.SpatialIncidentExposed == true;
        }
        return Accepted();
    }

    private ProductCommandResult CompleteHospitalLineConstruction(ProductPhase phase)
    {
        if (phase == ProductPhase.PrimaryBuilding)
        {
            _minute = _primaryCompletionMinute
                ?? throw new InvalidOperationException("Building primary line has no completion minute.");
            _primaryLineState = ProductProjectState.Commissioned;
        }
        else
        {
            _minute = _backupCompletionMinute
                ?? throw new InvalidOperationException("Building backup line has no completion minute.");
            _backupLineState = ProductProjectState.Commissioned;
        }
        return Accepted();
    }

    private ProductPhase CurrentHospitalPhase()
    {
        if (_settledDeliveredEnergyKwMinute == 0)
        {
            return ProductPhase.Complete;
        }
        if (_incidentActive)
        {
            return ProductPhase.IncidentActive;
        }
        if (_primaryLineState == ProductProjectState.NotOrdered)
        {
            return ProductPhase.PrimaryPlanning;
        }
        if (_primaryLineState == ProductProjectState.Building)
        {
            return ProductPhase.PrimaryBuilding;
        }
        if (_backupLineState == ProductProjectState.NotOrdered)
        {
            return ProductPhase.BackupPlanning;
        }
        if (_backupLineState == ProductProjectState.Building)
        {
            return ProductPhase.BackupBuilding;
        }
        return ProductPhase.IncidentReady;
    }

    private ProductHospitalLineProjectDefinition? ActiveHospitalDefinition() =>
        CurrentHospitalPhase() switch
        {
            ProductPhase.PrimaryPlanning or ProductPhase.PrimaryBuilding => PrimaryDefinition(),
            ProductPhase.BackupPlanning or ProductPhase.BackupBuilding => BackupDefinition(),
            _ => null,
        };

    private List<ProductPoint> ActiveHospitalSupports() =>
        CurrentHospitalPhase() == ProductPhase.PrimaryPlanning
            ? _primarySupportPositions
            : CurrentHospitalPhase() == ProductPhase.BackupPlanning
                ? _backupSupportPositions
                : throw new InvalidOperationException("No hospital line draft is active.");

    private ProductHospitalLineProjectDefinition PrimaryDefinition() =>
        HospitalLineDefinitions()
            .Single(line => line.ToTerminalId == HospitalDefinition().PrimaryTerminalId);

    private ProductHospitalLineProjectDefinition BackupDefinition() =>
        HospitalLineDefinitions()
            .Single(line => line.ToTerminalId == HospitalDefinition().BackupTerminalId);

    private IReadOnlyList<ProductHospitalLineProjectDefinition> HospitalLineDefinitions() =>
        _fixture.HospitalLineProjects
        ?? throw new InvalidOperationException("Hospital fixture has no line projects.");

    private ProductHospital HospitalDefinition() =>
        _fixture.Hospital
        ?? throw new InvalidOperationException("Fixture has no hospital stage.");

    private ProductSpatialIncident IncidentDefinition() =>
        _fixture.SpatialIncident
        ?? throw new InvalidOperationException("Hospital fixture has no spatial incident.");

    private ProductHospitalEconomy HospitalEconomyDefinition() =>
        _fixture.HospitalEconomy
        ?? throw new InvalidOperationException("Hospital fixture has no economy.");

    private ProductHospitalLineProjectDefinition? SelectHospitalRoute(
        IReadOnlySet<string> unavailable)
    {
        if (!_fixture.HasHospitalStage || _fixture.ExistingSource.CapacityKw < HospitalDefinition().DemandKw)
        {
            return null;
        }
        return HospitalLineDefinitions()
            .Where(line => HospitalLineState(line) == ProductProjectState.Commissioned)
            .Where(line => !unavailable.Contains(line.ProjectId))
            .Where(line => line.RatingKw >= HospitalDefinition().DemandKw)
            .OrderBy(line => line.RoutePriority)
            .ThenBy(line => line.ProjectId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private ProductProjectState HospitalLineState(ProductHospitalLineProjectDefinition line) =>
        line.ToTerminalId == HospitalDefinition().PrimaryTerminalId
            ? _primaryLineState
            : _backupLineState;

    private long ComputeTownUtilityKw(IReadOnlySet<string> unavailable, long hospitalUtilityKw)
    {
        if (!_settlementCompleted || CurrentSupplyFailure() != ProductSupplyFailure.None ||
            unavailable.Contains(_fixture.LineProject.ProjectId))
        {
            return 0;
        }
        long remainingSourceCapacity = checked(_fixture.ExistingSource.CapacityKw - hospitalUtilityKw);
        return remainingSourceCapacity >= _fixture.Town.DemandKw
            ? _fixture.Town.DemandKw
            : 0;
    }

    private bool IsAnyPositionOccupied(ProductPoint position) =>
        IsStaticPositionOccupied(position) ||
        position == _substationPosition ||
        _supportPositions.Contains(position) ||
        _primarySupportPositions.Contains(position) ||
        _backupSupportPositions.Contains(position) ||
        IsReservedPlantSite(position);

    private bool HospitalConditionsMet() =>
        _hospitalSettlement.Completed &&
        _hospitalSettlement.SingleLineRemovalConditionMet &&
        _hospitalSettlement.SpatialIncidentUtilityConditionMet &&
        _hospitalSettlement.HospitalP0ConditionMet;

    private bool TownLineSpatialIncidentExposed()
    {
        if (_substationPosition is null)
        {
            return false;
        }
        return PolylineIntersectsRisk(_supportPositions, _substationPosition);
    }

    private bool PolylineIntersectsRisk(
        IReadOnlyList<ProductPoint> supports,
        ProductPoint target)
    {
        ProductPoint from = _fixture.ExistingSource.Position;
        foreach (ProductPoint support in supports)
        {
            if (SegmentIntersectsClosedRect(from, support, IncidentDefinition().RiskRect))
            {
                return true;
            }
            from = support;
        }
        return SegmentIntersectsClosedRect(from, target, IncidentDefinition().RiskRect);
    }

    private static bool SegmentIntersectsClosedRect(
        ProductPoint a,
        ProductPoint b,
        ProductRiskRect rect)
    {
        if (InsideClosedRect(a, rect) || InsideClosedRect(b, rect))
        {
            return true;
        }

        ProductPoint bottomLeft = new(rect.MinX, rect.MinY);
        ProductPoint bottomRight = new(rect.MaxX, rect.MinY);
        ProductPoint topRight = new(rect.MaxX, rect.MaxY);
        ProductPoint topLeft = new(rect.MinX, rect.MaxY);
        return SegmentsIntersect(a, b, bottomLeft, bottomRight) ||
            SegmentsIntersect(a, b, bottomRight, topRight) ||
            SegmentsIntersect(a, b, topRight, topLeft) ||
            SegmentsIntersect(a, b, topLeft, bottomLeft);
    }

    private static bool InsideClosedRect(ProductPoint point, ProductRiskRect rect) =>
        point.X >= rect.MinX && point.X <= rect.MaxX &&
        point.Y >= rect.MinY && point.Y <= rect.MaxY;

    private static bool SegmentsIntersect(
        ProductPoint a,
        ProductPoint b,
        ProductPoint c,
        ProductPoint d)
    {
        long o1 = Orientation(a, b, c);
        long o2 = Orientation(a, b, d);
        long o3 = Orientation(c, d, a);
        long o4 = Orientation(c, d, b);
        if ((o1 > 0 && o2 < 0 || o1 < 0 && o2 > 0) &&
            (o3 > 0 && o4 < 0 || o3 < 0 && o4 > 0))
        {
            return true;
        }
        return o1 == 0 && OnSegment(a, b, c) ||
            o2 == 0 && OnSegment(a, b, d) ||
            o3 == 0 && OnSegment(c, d, a) ||
            o4 == 0 && OnSegment(c, d, b);
    }

    private static long Orientation(ProductPoint a, ProductPoint b, ProductPoint c) =>
        checked(
            checked((long)(b.X - a.X) * (c.Y - a.Y)) -
            checked((long)(b.Y - a.Y) * (c.X - a.X)));

    private static bool OnSegment(ProductPoint a, ProductPoint b, ProductPoint point) =>
        point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X) &&
        point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y);

    private static long ExactHospitalCash(long energyKwMinute, long rateCashUnitPerGWh)
    {
        long numerator = checked(energyKwMinute * rateCashUnitPerGWh);
        if (numerator % HospitalEnergyPerGWh != 0)
        {
            throw new InvalidOperationException("Hospital settlement does not divide into CashUnit.");
        }
        return numerator / HospitalEnergyPerGWh;
    }

    private static ProductHospitalSettlementSnapshot EmptyHospitalSettlement() =>
        new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, false, false);

    private void ResetHospitalState()
    {
        _primarySupportPositions.Clear();
        _backupSupportPositions.Clear();
        _primaryLineState = ProductProjectState.NotOrdered;
        _backupLineState = ProductProjectState.NotOrdered;
        _primaryCompletionMinute = null;
        _backupCompletionMinute = null;
        _primarySpatialIncidentExposed = false;
        _backupSpatialIncidentExposed = false;
        _incidentStarted = false;
        _incidentActive = false;
        _incidentStartMinute = null;
        _incidentRecoveryMinute = null;
        _incidentUnavailableProjectIds.Clear();
        _incidentHospitalUtilityKw = 0;
        _incidentTownUtilityKw = 0;
        _hospitalSettlement = EmptyHospitalSettlement();
    }
}
