namespace Gridworks.Core;

public enum NodeKind
{
    Generator,
    Bus,
    Substation,
    Load,
}

public enum LoadPriority
{
    P0,
    P2,
}

public enum ConstructionState
{
    NotOrdered,
    Commissioned,
}

public enum ProjectState
{
    NotOrdered,
    Building,
    Commissioned,
}

public enum PathRole
{
    Primary,
    Backup,
}

public enum SelectorType
{
    ElectricalContingencyId,
    SpatialRiskGroup,
}

public enum CorridorDesign
{
    RiverParallel,
    NorthDetour,
}

public enum EvaluationDesign
{
    NoBuild,
    RiverParallel,
    NorthDetour,
}

public enum InternalPowerStage
{
    None,
    Ups,
    Diesel,
}

public enum CommandErrorCode
{
    WrongTime,
    RequiredActionPending,
    AlreadyOrdered,
    NoNextMilestone,
}

public sealed record Position(decimal X, decimal Y);

public sealed record UnitsDefinition(
    string Position,
    string Power,
    string Energy,
    string Time,
    string Cash,
    string Rate);

public sealed record CalendarDefinition(string OriginLabel, int MinutesPerDay);

public sealed record EconomyDefinition(
    long InitialCash,
    long SaleRate,
    long GasVariableRate,
    long TownOutageRate,
    long HospitalOutageRate)
{
    public long GetOutageRate(string key) => key switch
    {
        "townOutageRate" => TownOutageRate,
        "hospitalOutageRate" => HospitalOutageRate,
        _ => throw new FixtureValidationException($"Unknown outage-rate key '{key}'."),
    };
}

public sealed record NodeDefinition(
    string Id,
    NodeKind Kind,
    Position Position,
    long? MaxOutputKw,
    bool? InitialOnline,
    bool? InitialCommissioned,
    long? DemandKw,
    LoadPriority? Priority,
    string? ServiceSubstationId);

public sealed record EdgeDefinition(
    string Id,
    string FromNodeId,
    string ToNodeId,
    long RatingKw,
    string ElectricalContingencyId,
    string SpatialRiskGroup,
    ConstructionState InitialConstructionState);

public sealed record ProjectDefinition(
    string Id,
    string EdgeId,
    long CostCashUnit,
    int AllowedOrderMinute,
    int BuildMinutes);

public sealed record LoadDefinition(
    string NodeId,
    int? NoticeMinute,
    int ActiveMinute,
    string OutageRateKey);

public sealed record RequirementDefinition(
    string Id,
    int DeadlineMinute,
    IReadOnlyList<string> SatisfiedByAnyCommissionedEdgeId);

public sealed record SupplyPathDefinition(
    string Id,
    string LoadNodeId,
    PathRole Role,
    string? RequiredCommissionedEdgeId,
    IReadOnlyList<string> EdgeIds);

public sealed record EvaluationCaseDefinition(
    string Id,
    SelectorType SelectorType,
    string SelectorValue);

public sealed record EventDefinition(
    string Id,
    int StartMinute,
    int EndMinute,
    string EvaluationCaseId);

public sealed record InternalPowerStageDefinition(string Id, long EnergyKwMinute);

public sealed record HospitalInternalPowerDefinition(
    string LoadNodeId,
    long RatedPowerKw,
    IReadOnlyList<InternalPowerStageDefinition> Stages);

public sealed record MilestoneDefinition(int Minute, string Label);

public sealed record ScenarioDefinition(
    string SchemaVersion,
    string FixtureId,
    string DisplayName,
    UnitsDefinition Units,
    CalendarDefinition Calendar,
    EconomyDefinition Economy,
    IReadOnlyList<NodeDefinition> Nodes,
    IReadOnlyList<EdgeDefinition> Edges,
    IReadOnlyList<ProjectDefinition> Projects,
    IReadOnlyList<LoadDefinition> Loads,
    IReadOnlyList<RequirementDefinition> Requirements,
    IReadOnlyList<SupplyPathDefinition> PermittedSupplyPaths,
    IReadOnlyList<EvaluationCaseDefinition> EvaluationCases,
    IReadOnlyList<EventDefinition> Events,
    HospitalInternalPowerDefinition HospitalInternalPower,
    IReadOnlyList<MilestoneDefinition> Milestones);

public sealed record MapBoundsDefinition(decimal Width, decimal Height);

public sealed record ServiceAreaDefinition(
    string SubstationId,
    string Shape,
    Position Center,
    decimal RadiusX,
    decimal RadiusY);

public sealed record RiskAreaDefinition(
    string Id,
    string SpatialRiskGroup,
    IReadOnlyList<Position> Polygon);

public sealed record EdgePolylineDefinition(string EdgeId, IReadOnlyList<Position> Points);

public sealed record LayoutVariantDefinition(
    string Id,
    IReadOnlyList<string> CorridorProjectOrder);

public sealed record PresentationDefinition(
    MapBoundsDefinition MapBounds,
    IReadOnlyList<ServiceAreaDefinition> ServiceAreas,
    IReadOnlyList<RiskAreaDefinition> RiskAreas,
    IReadOnlyList<EdgePolylineDefinition> EdgePolylines,
    IReadOnlyList<LayoutVariantDefinition> LayoutVariants);

public sealed record Settlement(
    long RevenueCashUnit,
    long GasCostCashUnit,
    long CompensationCashUnit,
    long LostSalesCashUnit,
    IReadOnlyDictionary<string, long> UtilityDeliveredKwMinuteByLoad,
    IReadOnlyDictionary<string, long> UtilityUnservedKwMinuteByLoad,
    long GasInjectionKwMinute,
    long HospitalInternalUsedKwMinute,
    long HospitalP0UnservedKwMinute);

public sealed record PublicSnapshot(
    int Minute,
    long Cash,
    ProjectState TownProjectState,
    ProjectState CorridorProjectState,
    CorridorDesign? SelectedCorridor,
    IReadOnlyList<string> CommissionedEdgeIds,
    IReadOnlyList<string> EventRemovedEdgeIds,
    IReadOnlyList<string> ActiveLoadIds,
    IReadOnlyDictionary<string, string?> UtilityPathByLoad,
    InternalPowerStage HospitalInternalStage,
    long HospitalInternalRemainingKwMinute,
    Settlement Interval,
    Settlement Cumulative,
    bool IsComplete);

public sealed record BoundaryTrace(string Id, PublicSnapshot Snapshot);

public sealed record CommandResult(
    bool Accepted,
    CommandErrorCode? ErrorCode,
    PublicSnapshot PublicSnapshot,
    IReadOnlyList<BoundaryTrace> BoundaryTrace);

public sealed record RemovalEvaluation(
    EvaluationDesign Design,
    string CaseId,
    IReadOnlyList<string> RemovedEdgeIds,
    bool TownUtilityDelivered,
    bool HospitalUtilityDelivered,
    string? TownPathId,
    string? HospitalPathId);

public sealed record LoadedFixture(
    ScenarioDefinition Scenario,
    PresentationDefinition Presentation,
    FixtureOracle Oracle);

public sealed record TopologyOracle(
    int NodeCount,
    int EdgeCount,
    long NormalDemandKw,
    long SharedTrunkRatingKw,
    long GeneratorRatingKw,
    bool InitialTownServiceEligible,
    bool InitialTownUtilityPathAvailable,
    bool InitialHospitalUtilityPathAvailable);

public sealed record EvaluationOutcomeOracle(
    EvaluationDesign Design,
    string CaseId,
    IReadOnlyList<string> RemovedEdgeIds,
    bool TownUtilityDelivered,
    bool HospitalUtilityDelivered,
    string? TownPathId,
    string? HospitalPathId);

public sealed record InternalPowerOracle(
    int UpsDurationMinutes,
    int DieselDurationMinutes,
    int TotalDurationMinutes,
    long RiverEventUsedKwMinute,
    long RiverEventRemainingKwMinute,
    long RiverEventHospitalP0UnservedKwMinute,
    long NorthEventUsedKwMinute,
    long NorthEventRemainingKwMinute,
    long NorthEventHospitalP0UnservedKwMinute);

public sealed record BoundaryOracle(string Id, PublicSnapshot Snapshot);

public sealed record RouteBoundaryOracle(
    CorridorDesign Design,
    IReadOnlyList<BoundaryOracle> States);

public sealed record EventCashOracle(
    long UtilityDeliveredKwMinute,
    long TownUtilityUnservedKwMinute,
    long HospitalUtilityUnservedKwMinute,
    long RevenueCashUnit,
    long LostSalesCashUnit,
    long CompensationCashUnit,
    long GasCostCashUnit,
    long EventCashDelta,
    long EndingCash);

public sealed record CashOracle(
    long PreChoiceCash,
    long NormalPostChoiceNetCash,
    EventCashOracle RiverEvent,
    EventCashOracle NorthEvent);

public sealed record FixtureOracle(
    TopologyOracle Topology,
    IReadOnlyList<EvaluationOutcomeOracle> EvaluationOutcomes,
    InternalPowerOracle InternalPower,
    IReadOnlyList<BoundaryOracle> CommonBoundaryStates,
    IReadOnlyList<RouteBoundaryOracle> RouteBoundaryStates,
    CashOracle Cash);

public sealed class FixtureValidationException : Exception
{
    public FixtureValidationException(string message)
        : base(message)
    {
    }

    public FixtureValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
