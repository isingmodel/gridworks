namespace Gridworks.Core.Release.V2;

public enum CommercialCoreChapterKind
{
    Prelude,
    CommercialCore,
}

public enum CommercialCoreObligationKind
{
    MustSupply,
    CityPromise,
    OperatingRecord,
}

public enum PromiseDecision
{
    Keep,
    Defer,
}

public sealed record CommercialStoryCard(
    string Speaker,
    string Title,
    string Body);

public sealed record CommercialCoreLoadBundle(
    string LoadId,
    string DisplayName,
    string NodeId,
    long DemandKw,
    CommercialCoreObligationKind ObligationKind,
    bool NamedEmergencyDuty);

public sealed record CommercialCoreOperatingPhase(
    string PhaseId,
    string DisplayName,
    ThermalIntervalPolicy Policy,
    IReadOnlyList<CommercialCoreLoadBundle> Loads,
    IReadOnlyList<string> UnavailableAssetIds,
    IReadOnlyList<string> ActiveRiskAreaIds,
    IReadOnlyList<ThermalLimitOverride> LimitOverrides)
{
    private IReadOnlyList<CommercialCoreLoadBundle> _loads = Array.AsReadOnly(Loads.ToArray());
    private IReadOnlyList<string> _unavailableAssetIds =
        Array.AsReadOnly(UnavailableAssetIds.ToArray());
    private IReadOnlyList<string> _activeRiskAreaIds =
        Array.AsReadOnly(ActiveRiskAreaIds.ToArray());
    private IReadOnlyList<ThermalLimitOverride> _limitOverrides =
        Array.AsReadOnly(LimitOverrides.ToArray());

    public IReadOnlyList<CommercialCoreLoadBundle> Loads
    {
        get => _loads;
        init => _loads = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> UnavailableAssetIds
    {
        get => _unavailableAssetIds;
        init => _unavailableAssetIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> ActiveRiskAreaIds
    {
        get => _activeRiskAreaIds;
        init => _activeRiskAreaIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ThermalLimitOverride> LimitOverrides
    {
        get => _limitOverrides;
        init => _limitOverrides = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record CommercialCoreDecisionWindow(
    string WindowId,
    string NextPhaseId,
    CommercialStoryCard? Story,
    int? BuildMinutesAllowance);

public sealed record CommercialCorePromise(
    string PromiseId,
    string DisplayName,
    string LoadId);

public sealed record CommercialCoreChapter(
    string ChapterId,
    CommercialCoreChapterKind Kind,
    string DisplayName,
    long SeedCashUnit,
    IReadOnlyList<string> SeedNodeIds,
    IReadOnlyList<string> SeedEdgeIds,
    CommercialStoryCard Briefing,
    string Objective,
    long GrantCashUnit,
    int DeadlineMinute,
    CommercialCorePromise? Promise,
    IReadOnlyList<CommercialCoreDecisionWindow> DecisionWindows,
    IReadOnlyList<CommercialCoreOperatingPhase> OperatingPhases,
    CommercialStoryCard StandardResult,
    CommercialStoryCard? KeptResult,
    CommercialStoryCard? DeferredResult)
{
    private IReadOnlyList<string> _seedNodeIds = Array.AsReadOnly(SeedNodeIds.ToArray());
    private IReadOnlyList<string> _seedEdgeIds = Array.AsReadOnly(SeedEdgeIds.ToArray());
    private IReadOnlyList<CommercialCoreDecisionWindow> _decisionWindows =
        Array.AsReadOnly(DecisionWindows.ToArray());
    private IReadOnlyList<CommercialCoreOperatingPhase> _operatingPhases =
        Array.AsReadOnly(OperatingPhases.ToArray());

    public IReadOnlyList<string> SeedNodeIds
    {
        get => _seedNodeIds;
        init => _seedNodeIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> SeedEdgeIds
    {
        get => _seedEdgeIds;
        init => _seedEdgeIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<CommercialCoreDecisionWindow> DecisionWindows
    {
        get => _decisionWindows;
        init => _decisionWindows = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<CommercialCoreOperatingPhase> OperatingPhases
    {
        get => _operatingPhases;
        init => _operatingPhases = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record CommercialCoreSliceDefinition(
    string SchemaVersion,
    string SliceId,
    string DisplayName,
    string WorldId,
    IReadOnlyList<CommercialCoreChapter> Chapters)
{
    public const string SupportedSchemaVersion = "gridworks.commercial.core-slice.v1";

    private IReadOnlyList<CommercialCoreChapter> _chapters = Array.AsReadOnly(Chapters.ToArray());

    public IReadOnlyList<CommercialCoreChapter> Chapters
    {
        get => _chapters;
        init => _chapters = Array.AsReadOnly(value.ToArray());
    }
}

public sealed class CommercialCoreValidationException : Exception
{
    public CommercialCoreValidationException(string message)
        : base(message)
    {
    }

    public CommercialCoreValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
