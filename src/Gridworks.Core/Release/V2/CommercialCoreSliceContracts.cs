namespace Gridworks.Core.Release.V2;

public enum CommercialObligationKind
{
    SafetyDuty,
    CityPromise,
    OperatingRecord,
}

public enum CommercialPhaseThermalPolicy
{
    ContinuousOnly,
    SafetyEmergencyAllowed,
}

public enum CommercialPromiseDecision
{
    Unset,
    Keep,
    Defer,
}

public sealed record CommercialStoryCard(
    string Speaker,
    string Title,
    string Body);

public sealed record CommercialCityPromiseDefinition(
    string PromiseId,
    string DisplayName,
    string LoadId,
    string KeepLabel,
    string DeferLabel);

public sealed record CommercialLoadBundleDefinition(
    string LoadId,
    long DemandKw,
    CommercialObligationKind Obligation);

public sealed record CommercialOperatingPhaseDefinition(
    string PhaseId,
    string DisplayName,
    CommercialPhaseThermalPolicy ThermalPolicy,
    CommercialStoryCard? Story,
    IReadOnlyList<CommercialLoadBundleDefinition> Loads,
    IReadOnlyList<string> UnavailableNodeIds,
    IReadOnlyList<string> UnavailableEdgeIds,
    IReadOnlyList<string> ActiveRiskAreaIds,
    IReadOnlyList<ThermalLimitOverride> ThermalLimitOverrides)
{
    private IReadOnlyList<CommercialLoadBundleDefinition> _loads = Freeze(Loads);
    private IReadOnlyList<string> _unavailableNodeIds = Freeze(UnavailableNodeIds);
    private IReadOnlyList<string> _unavailableEdgeIds = Freeze(UnavailableEdgeIds);
    private IReadOnlyList<string> _activeRiskAreaIds = Freeze(ActiveRiskAreaIds);
    private IReadOnlyList<ThermalLimitOverride> _thermalLimitOverrides =
        Freeze(ThermalLimitOverrides);

    public IReadOnlyList<CommercialLoadBundleDefinition> Loads
    {
        get => _loads;
        init => _loads = Freeze(value);
    }

    public IReadOnlyList<string> UnavailableNodeIds
    {
        get => _unavailableNodeIds;
        init => _unavailableNodeIds = Freeze(value);
    }

    public IReadOnlyList<string> UnavailableEdgeIds
    {
        get => _unavailableEdgeIds;
        init => _unavailableEdgeIds = Freeze(value);
    }

    public IReadOnlyList<string> ActiveRiskAreaIds
    {
        get => _activeRiskAreaIds;
        init => _activeRiskAreaIds = Freeze(value);
    }

    public IReadOnlyList<ThermalLimitOverride> ThermalLimitOverrides
    {
        get => _thermalLimitOverrides;
        init => _thermalLimitOverrides = Freeze(value);
    }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record CommercialDecisionWindowDefinition(
    string WindowId,
    string BeforePhaseId,
    CommercialStoryCard? Story,
    int? BuildMinutesAvailable);

public sealed record CommercialResultCardsDefinition(
    CommercialStoryCard? Standard,
    CommercialStoryCard? Kept,
    CommercialStoryCard? Deferred);

public sealed record CommercialCoreChapterDefinition(
    string ChapterId,
    string DisplayName,
    CommercialStoryCard Briefing,
    string Objective,
    long BudgetGrantCashUnit,
    CommercialCityPromiseDefinition? CityPromise,
    IReadOnlyList<CommercialDecisionWindowDefinition> DecisionWindows,
    IReadOnlyList<CommercialOperatingPhaseDefinition> OperatingPhases,
    CommercialResultCardsDefinition ResultCards)
{
    private IReadOnlyList<CommercialDecisionWindowDefinition> _decisionWindows =
        Freeze(DecisionWindows);
    private IReadOnlyList<CommercialOperatingPhaseDefinition> _operatingPhases =
        Freeze(OperatingPhases);

    public IReadOnlyList<CommercialDecisionWindowDefinition> DecisionWindows
    {
        get => _decisionWindows;
        init => _decisionWindows = Freeze(value);
    }

    public IReadOnlyList<CommercialOperatingPhaseDefinition> OperatingPhases
    {
        get => _operatingPhases;
        init => _operatingPhases = Freeze(value);
    }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record CommercialCoreSeedDefinition(
    string SeedId,
    int StartMinute,
    long InitialCashUnit,
    IReadOnlyList<string> BaseNodeIds,
    IReadOnlyList<string> BaseEdgeIds,
    IReadOnlyList<SpatialNodeDefinition> ConstructedNodes,
    IReadOnlyList<SpatialEdgeDefinition> ConstructedEdges,
    IReadOnlyList<string> CoolingAssetIds)
{
    private IReadOnlyList<string> _baseNodeIds = Freeze(BaseNodeIds);
    private IReadOnlyList<string> _baseEdgeIds = Freeze(BaseEdgeIds);
    private IReadOnlyList<SpatialNodeDefinition> _constructedNodes = Freeze(ConstructedNodes);
    private IReadOnlyList<SpatialEdgeDefinition> _constructedEdges = Freeze(ConstructedEdges);
    private IReadOnlyList<string> _coolingAssetIds = Freeze(CoolingAssetIds);

    public IReadOnlyList<string> BaseNodeIds
    {
        get => _baseNodeIds;
        init => _baseNodeIds = Freeze(value);
    }

    public IReadOnlyList<string> BaseEdgeIds
    {
        get => _baseEdgeIds;
        init => _baseEdgeIds = Freeze(value);
    }

    public IReadOnlyList<SpatialNodeDefinition> ConstructedNodes
    {
        get => _constructedNodes;
        init => _constructedNodes = Freeze(value);
    }

    public IReadOnlyList<SpatialEdgeDefinition> ConstructedEdges
    {
        get => _constructedEdges;
        init => _constructedEdges = Freeze(value);
    }

    public IReadOnlyList<string> CoolingAssetIds
    {
        get => _coolingAssetIds;
        init => _coolingAssetIds = Freeze(value);
    }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record CommercialCoreSegmentDefinition(
    string SegmentId,
    CommercialCoreSeedDefinition Seed,
    CommercialCoreChapterDefinition Chapter);

public sealed record CommercialCoreSliceDefinition(
    string SchemaVersion,
    string SliceId,
    string DisplayName,
    CommercialCoreSegmentDefinition Prelude,
    CommercialCoreSegmentDefinition Main);

public sealed class CommercialCoreSliceValidationException : Exception
{
    public CommercialCoreSliceValidationException(string message)
        : base(message)
    {
    }

    public CommercialCoreSliceValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
