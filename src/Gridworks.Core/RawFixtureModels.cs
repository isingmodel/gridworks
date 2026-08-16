using System.Text.Json.Serialization;

namespace Gridworks.Core;

internal sealed class RawFixture
{
    [JsonRequired] public string SchemaVersion { get; set; } = null!;
    [JsonRequired] public string FixtureId { get; set; } = null!;
    [JsonRequired] public string DisplayName { get; set; } = null!;
    [JsonRequired] public RawUnits Units { get; set; } = null!;
    [JsonRequired] public RawCalendar Calendar { get; set; } = null!;
    [JsonRequired] public RawEconomy Economy { get; set; } = null!;
    [JsonRequired] public RawNode[] Nodes { get; set; } = null!;
    [JsonRequired] public RawEdge[] Edges { get; set; } = null!;
    [JsonRequired] public RawProject[] Projects { get; set; } = null!;
    [JsonRequired] public RawLoad[] Loads { get; set; } = null!;
    [JsonRequired] public RawRequirement[] Requirements { get; set; } = null!;
    [JsonRequired] public RawSupplyPath[] PermittedSupplyPaths { get; set; } = null!;
    [JsonRequired] public RawEvaluationCase[] EvaluationCases { get; set; } = null!;
    [JsonRequired] public RawEvent[] Events { get; set; } = null!;
    [JsonRequired] public RawHospitalInternalPower HospitalInternalPower { get; set; } = null!;
    [JsonRequired] public RawMilestone[] Milestones { get; set; } = null!;
    [JsonRequired] public RawPresentation Presentation { get; set; } = null!;
    [JsonRequired] public RawOracle VerificationOnly { get; set; } = null!;
}

internal sealed class RawUnits
{
    [JsonRequired] public string Position { get; set; } = null!;
    [JsonRequired] public string Power { get; set; } = null!;
    [JsonRequired] public string Energy { get; set; } = null!;
    [JsonRequired] public string Time { get; set; } = null!;
    [JsonRequired] public string Cash { get; set; } = null!;
    [JsonRequired] public string Rate { get; set; } = null!;
}

internal sealed class RawCalendar
{
    [JsonRequired] public string OriginLabel { get; set; } = null!;
    [JsonRequired] public int MinutesPerDay { get; set; }
}

internal sealed class RawEconomy
{
    [JsonRequired] public long InitialCash { get; set; }
    [JsonRequired] public long SaleRate { get; set; }
    [JsonRequired] public long GasVariableRate { get; set; }
    [JsonRequired] public long TownOutageRate { get; set; }
    [JsonRequired] public long HospitalOutageRate { get; set; }
}

internal sealed class RawPosition
{
    [JsonRequired] public decimal X { get; set; }
    [JsonRequired] public decimal Y { get; set; }
}

internal sealed class RawNode
{
    [JsonRequired] public string Id { get; set; } = null!;
    [JsonRequired] public string Kind { get; set; } = null!;
    [JsonRequired] public RawPosition Position { get; set; } = null!;
    public long? MaxOutputKw { get; set; }
    public bool? InitialOnline { get; set; }
    public bool? InitialCommissioned { get; set; }
    public long? DemandKw { get; set; }
    public string? Priority { get; set; }
    public string? ServiceSubstationId { get; set; }
}

internal sealed class RawEdge
{
    [JsonRequired] public string Id { get; set; } = null!;
    [JsonRequired] public string FromNodeId { get; set; } = null!;
    [JsonRequired] public string ToNodeId { get; set; } = null!;
    [JsonRequired] public long RatingKw { get; set; }
    [JsonRequired] public string ElectricalContingencyId { get; set; } = null!;
    [JsonRequired] public string SpatialRiskGroup { get; set; } = null!;
    [JsonRequired] public string InitialConstructionState { get; set; } = null!;
}

internal sealed class RawProject
{
    [JsonRequired] public string Id { get; set; } = null!;
    [JsonRequired] public string EdgeId { get; set; } = null!;
    [JsonRequired] public long CostCashUnit { get; set; }
    [JsonRequired] public int AllowedOrderMinute { get; set; }
    [JsonRequired] public int BuildMinutes { get; set; }
}

internal sealed class RawLoad
{
    [JsonRequired] public string NodeId { get; set; } = null!;
    public int? NoticeMinute { get; set; }
    [JsonRequired] public int ActiveMinute { get; set; }
    [JsonRequired] public string OutageRateKey { get; set; } = null!;
}

internal sealed class RawRequirement
{
    [JsonRequired] public string Id { get; set; } = null!;
    [JsonRequired] public int DeadlineMinute { get; set; }
    [JsonRequired] public string[] SatisfiedByAnyCommissionedEdgeId { get; set; } = null!;
}

internal sealed class RawSupplyPath
{
    [JsonRequired] public string Id { get; set; } = null!;
    [JsonRequired] public string LoadNodeId { get; set; } = null!;
    [JsonRequired] public string Role { get; set; } = null!;
    public string? RequiredCommissionedEdgeId { get; set; }
    [JsonRequired] public string[] EdgeIds { get; set; } = null!;
}

internal sealed class RawEvaluationCase
{
    [JsonRequired] public string Id { get; set; } = null!;
    [JsonRequired] public string SelectorType { get; set; } = null!;
    [JsonRequired] public string SelectorValue { get; set; } = null!;
}

internal sealed class RawEvent
{
    [JsonRequired] public string Id { get; set; } = null!;
    [JsonRequired] public int StartMinute { get; set; }
    [JsonRequired] public int EndMinute { get; set; }
    [JsonRequired] public string EvaluationCaseId { get; set; } = null!;
}

internal sealed class RawInternalPowerStage
{
    [JsonRequired] public string Id { get; set; } = null!;
    [JsonRequired] public long EnergyKwMinute { get; set; }
}

internal sealed class RawHospitalInternalPower
{
    [JsonRequired] public string LoadNodeId { get; set; } = null!;
    [JsonRequired] public long RatedPowerKw { get; set; }
    [JsonRequired] public RawInternalPowerStage[] Stages { get; set; } = null!;
}

internal sealed class RawMilestone
{
    [JsonRequired] public int Minute { get; set; }
    [JsonRequired] public string Label { get; set; } = null!;
}

internal sealed class RawMapBounds
{
    [JsonRequired] public decimal Width { get; set; }
    [JsonRequired] public decimal Height { get; set; }
}

internal sealed class RawServiceArea
{
    [JsonRequired] public string SubstationId { get; set; } = null!;
    [JsonRequired] public string Shape { get; set; } = null!;
    [JsonRequired] public RawPosition Center { get; set; } = null!;
    [JsonRequired] public decimal RadiusX { get; set; }
    [JsonRequired] public decimal RadiusY { get; set; }
}

internal sealed class RawRiskArea
{
    [JsonRequired] public string Id { get; set; } = null!;
    [JsonRequired] public string SpatialRiskGroup { get; set; } = null!;
    [JsonRequired] public RawPosition[] Polygon { get; set; } = null!;
}

internal sealed class RawEdgePolyline
{
    [JsonRequired] public string EdgeId { get; set; } = null!;
    [JsonRequired] public RawPosition[] Points { get; set; } = null!;
}

internal sealed class RawLayoutVariant
{
    [JsonRequired] public string Id { get; set; } = null!;
    [JsonRequired] public string[] CorridorProjectOrder { get; set; } = null!;
}

internal sealed class RawPresentation
{
    [JsonRequired] public RawMapBounds MapBounds { get; set; } = null!;
    [JsonRequired] public RawServiceArea[] ServiceAreas { get; set; } = null!;
    [JsonRequired] public RawRiskArea[] RiskAreas { get; set; } = null!;
    [JsonRequired] public RawEdgePolyline[] EdgePolylines { get; set; } = null!;
    [JsonRequired] public RawLayoutVariant[] LayoutVariants { get; set; } = null!;
}

internal sealed class RawTopologyOracle
{
    [JsonRequired] public int NodeCount { get; set; }
    [JsonRequired] public int EdgeCount { get; set; }
    [JsonRequired] public long NormalDemandKw { get; set; }
    [JsonRequired] public long SharedTrunkRatingKw { get; set; }
    [JsonRequired] public long GeneratorRatingKw { get; set; }
    [JsonRequired] public bool InitialTownServiceEligible { get; set; }
    [JsonRequired] public bool InitialTownUtilityPathAvailable { get; set; }
    [JsonRequired] public bool InitialHospitalUtilityPathAvailable { get; set; }
}

internal sealed class RawEvaluationOutcome
{
    [JsonRequired] public string Design { get; set; } = null!;
    [JsonRequired] public string CaseId { get; set; } = null!;
    [JsonRequired] public string[] RemovedEdgeIds { get; set; } = null!;
    [JsonRequired] public bool TownUtilityDelivered { get; set; }
    [JsonRequired] public bool HospitalUtilityDelivered { get; set; }
    [JsonRequired] public string? TownPathId { get; set; }
    [JsonRequired] public string? HospitalPathId { get; set; }
}

internal sealed class RawInternalPowerOracle
{
    [JsonRequired] public int UpsDurationMinutes { get; set; }
    [JsonRequired] public int DieselDurationMinutes { get; set; }
    [JsonRequired] public int TotalDurationMinutes { get; set; }
    [JsonRequired] public long RiverEventUsedKwMinute { get; set; }
    [JsonRequired] public long RiverEventRemainingKwMinute { get; set; }
    [JsonRequired] public long RiverEventHospitalP0UnservedKwMinute { get; set; }
    [JsonRequired] public long NorthEventUsedKwMinute { get; set; }
    [JsonRequired] public long NorthEventRemainingKwMinute { get; set; }
    [JsonRequired] public long NorthEventHospitalP0UnservedKwMinute { get; set; }
}

internal sealed class RawSettlement
{
    [JsonRequired] public long RevenueCashUnit { get; set; }
    [JsonRequired] public long GasCostCashUnit { get; set; }
    [JsonRequired] public long CompensationCashUnit { get; set; }
    [JsonRequired] public long LostSalesCashUnit { get; set; }
    [JsonRequired] public Dictionary<string, long> UtilityDeliveredKwMinuteByLoad { get; set; } = null!;
    [JsonRequired] public Dictionary<string, long> UtilityUnservedKwMinuteByLoad { get; set; } = null!;
    [JsonRequired] public long GasInjectionKwMinute { get; set; }
    [JsonRequired] public long HospitalInternalUsedKwMinute { get; set; }
    [JsonRequired] public long HospitalP0UnservedKwMinute { get; set; }
}

internal sealed class RawBoundaryState
{
    [JsonRequired] public string Id { get; set; } = null!;
    [JsonRequired] public int Minute { get; set; }
    [JsonRequired] public long Cash { get; set; }
    [JsonRequired] public string TownProjectState { get; set; } = null!;
    [JsonRequired] public string CorridorProjectState { get; set; } = null!;
    [JsonRequired] public string? SelectedCorridor { get; set; }
    [JsonRequired] public string[] CommissionedEdgeIds { get; set; } = null!;
    [JsonRequired] public string[] EventRemovedEdgeIds { get; set; } = null!;
    [JsonRequired] public string[] ActiveLoadIds { get; set; } = null!;
    [JsonRequired] public Dictionary<string, string?> UtilityPathByLoad { get; set; } = null!;
    [JsonRequired] public string HospitalInternalStage { get; set; } = null!;
    [JsonRequired] public long HospitalInternalRemainingKwMinute { get; set; }
    [JsonRequired] public RawSettlement Interval { get; set; } = null!;
    [JsonRequired] public RawSettlement Cumulative { get; set; } = null!;
    [JsonRequired] public bool IsComplete { get; set; }
}

internal sealed class RawRouteBoundaryStates
{
    [JsonRequired] public string Design { get; set; } = null!;
    [JsonRequired] public RawBoundaryState[] States { get; set; } = null!;
}

internal sealed class RawEventCashOracle
{
    [JsonRequired] public long UtilityDeliveredKwMinute { get; set; }
    [JsonRequired] public long TownUtilityUnservedKwMinute { get; set; }
    [JsonRequired] public long HospitalUtilityUnservedKwMinute { get; set; }
    [JsonRequired] public long RevenueCashUnit { get; set; }
    [JsonRequired] public long LostSalesCashUnit { get; set; }
    [JsonRequired] public long CompensationCashUnit { get; set; }
    [JsonRequired] public long GasCostCashUnit { get; set; }
    [JsonRequired] public long EventCashDelta { get; set; }
    [JsonRequired] public long EndingCash { get; set; }
}

internal sealed class RawCashOracle
{
    [JsonRequired] public long PreChoiceCash { get; set; }
    [JsonRequired] public long NormalPostChoiceNetCash { get; set; }
    [JsonRequired] public RawEventCashOracle RiverEvent { get; set; } = null!;
    [JsonRequired] public RawEventCashOracle NorthEvent { get; set; } = null!;
}

internal sealed class RawOracle
{
    [JsonRequired] public RawTopologyOracle Topology { get; set; } = null!;
    [JsonRequired] public RawEvaluationOutcome[] EvaluationOutcomes { get; set; } = null!;
    [JsonRequired] public RawInternalPowerOracle InternalPower { get; set; } = null!;
    [JsonRequired] public RawBoundaryState[] CommonBoundaryStates { get; set; } = null!;
    [JsonRequired] public RawRouteBoundaryStates[] RouteBoundaryStates { get; set; } = null!;
    [JsonRequired] public RawCashOracle Cash { get; set; } = null!;
}
