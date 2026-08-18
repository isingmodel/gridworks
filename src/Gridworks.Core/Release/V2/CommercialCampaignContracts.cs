namespace Gridworks.Core.Release.V2;

public sealed record CommercialCampaignResultFactTemplatesDefinition(
    string SuppliedLoad,
    string UnservedLoad,
    string? KeptPromise,
    string? DeferredPromise,
    string RemainingCash);

public sealed record CommercialCampaignLinePlanDefinition(
    string LineClassId,
    string PoleClassId);

public sealed record CommercialCampaignConnectionRequirement(
    string NodeId,
    int MinimumConnections);

public sealed record CommercialCampaignChapterDefinition(
    string ChapterId,
    string DisplayName,
    CommercialStoryCard Briefing,
    string Objective,
    int TimeAdvanceBeforeChapterMinutes,
    bool ResetThermalStateBeforeChapter,
    long BudgetGrantCashUnit,
    IReadOnlyList<string> AvailableNodeClassIds,
    IReadOnlyList<CommercialCampaignLinePlanDefinition> AvailableLinePlans,
    IReadOnlyList<CommercialCampaignConnectionRequirement> ConnectionRequirements,
    CommercialCityPromiseDefinition? CityPromise,
    IReadOnlyList<CommercialDecisionWindowDefinition> DecisionWindows,
    IReadOnlyList<CommercialOperatingPhaseDefinition> OperatingPhases,
    CommercialResultCardsDefinition ResultCards,
    CommercialCampaignResultFactTemplatesDefinition ResultFactTemplates)
{
    private IReadOnlyList<CommercialDecisionWindowDefinition> _decisionWindows =
        Freeze(DecisionWindows);
    private IReadOnlyList<CommercialOperatingPhaseDefinition> _operatingPhases =
        Freeze(OperatingPhases);
    private IReadOnlyList<string> _availableNodeClassIds = Freeze(AvailableNodeClassIds);
    private IReadOnlyList<CommercialCampaignLinePlanDefinition> _availableLinePlans =
        Freeze(AvailableLinePlans);
    private IReadOnlyList<CommercialCampaignConnectionRequirement> _connectionRequirements =
        Freeze(ConnectionRequirements);

    public IReadOnlyList<string> AvailableNodeClassIds
    {
        get => _availableNodeClassIds;
        init => _availableNodeClassIds = Freeze(value);
    }

    public IReadOnlyList<CommercialCampaignLinePlanDefinition> AvailableLinePlans
    {
        get => _availableLinePlans;
        init => _availableLinePlans = Freeze(value);
    }

    public IReadOnlyList<CommercialCampaignConnectionRequirement> ConnectionRequirements
    {
        get => _connectionRequirements;
        init => _connectionRequirements = Freeze(value);
    }

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

    public CommercialCoreChapterDefinition ToCoreChapter() => new(
        ChapterId,
        DisplayName,
        Briefing,
        Objective,
        BudgetGrantCashUnit,
        CityPromise,
        DecisionWindows,
        OperatingPhases,
        ResultCards);

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record CommercialCampaignDefinition(
    string SchemaVersion,
    string CampaignId,
    string DisplayName,
    CommercialCoreSeedDefinition InitialSeed,
    IReadOnlyList<CommercialCampaignChapterDefinition> Chapters)
{
    private IReadOnlyList<CommercialCampaignChapterDefinition> _chapters = Freeze(Chapters);

    public IReadOnlyList<CommercialCampaignChapterDefinition> Chapters
    {
        get => _chapters;
        init => _chapters = Freeze(value);
    }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record CommercialCampaignLoadFact(
    string PhaseId,
    CommercialObligationKind Obligation,
    string LoadId,
    long DemandKw,
    long DeliveredKw,
    string? SourceId,
    long? MinimumRemainingKw,
    ThermalSupplyFailure? Failure);

public sealed record CommercialCampaignThermalFact(
    string PhaseId,
    string AssetId,
    ThermalAssetKind AssetKind,
    long UsedKw,
    long ContinuousLimitKw,
    long EmergencyLimitKw,
    ThermalOperatingState State,
    ThermalOperatingState NextState);

public sealed record CommercialCampaignOutcomeFacts(
    IReadOnlyList<CommercialCampaignLoadFact> Loads,
    IReadOnlyList<CommercialCampaignThermalFact> ThermalAssets)
{
    private IReadOnlyList<CommercialCampaignLoadFact> _loads = Freeze(Loads);
    private IReadOnlyList<CommercialCampaignThermalFact> _thermalAssets = Freeze(ThermalAssets);

    public IReadOnlyList<CommercialCampaignLoadFact> Loads
    {
        get => _loads;
        init => _loads = Freeze(value);
    }

    public IReadOnlyList<CommercialCampaignThermalFact> ThermalAssets
    {
        get => _thermalAssets;
        init => _thermalAssets = Freeze(value);
    }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record CommercialCampaignChapterOutcome(
    string ChapterId,
    CommercialStoryCard ResultCard,
    CommercialPromiseDecision PromiseDecision,
    IReadOnlyList<CommercialCommittedPhaseResult> Phases,
    CommercialCampaignOutcomeFacts Facts,
    IReadOnlyList<string> RenderedFacts,
    long EndingCashUnit,
    long EndingMinute)
{
    private IReadOnlyList<CommercialCommittedPhaseResult> _phases = Freeze(Phases);
    private IReadOnlyList<string> _renderedFacts = Freeze(RenderedFacts);

    public IReadOnlyList<CommercialCommittedPhaseResult> Phases
    {
        get => _phases;
        init => _phases = Freeze(value);
    }

    public IReadOnlyList<string> RenderedFacts
    {
        get => _renderedFacts;
        init => _renderedFacts = Freeze(value);
    }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed class CommercialCampaignValidationException : Exception
{
    public CommercialCampaignValidationException(string message)
        : base(message)
    {
    }

    public CommercialCampaignValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
