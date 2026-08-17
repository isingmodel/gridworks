namespace Gridworks.Core.Release;

public sealed record ReleaseStoryCard(
    string Speaker,
    string Title,
    string Body);

public sealed record ReleaseCampaignLoad(
    string LoadId,
    long DemandKw);

public sealed record ReleaseConnectionRequirement(
    string NodeId,
    int MinimumConnections);

public sealed record ReleaseCampaignEvent(
    ReleaseStoryCard Story,
    IReadOnlyList<string> UnavailableNodeIds,
    IReadOnlyList<string> UnavailableEdgeIds,
    IReadOnlyList<string> ActiveRiskAreaIds)
{
    private IReadOnlyList<string> _unavailableNodeIds =
        Array.AsReadOnly(UnavailableNodeIds.ToArray());
    private IReadOnlyList<string> _unavailableEdgeIds =
        Array.AsReadOnly(UnavailableEdgeIds.ToArray());
    private IReadOnlyList<string> _activeRiskAreaIds =
        Array.AsReadOnly(ActiveRiskAreaIds.ToArray());

    public IReadOnlyList<string> UnavailableNodeIds
    {
        get => _unavailableNodeIds;
        init => _unavailableNodeIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> UnavailableEdgeIds
    {
        get => _unavailableEdgeIds;
        init => _unavailableEdgeIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> ActiveRiskAreaIds
    {
        get => _activeRiskAreaIds;
        init => _activeRiskAreaIds = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record ReleaseCampaignChapter(
    string ChapterId,
    string ActLabel,
    string DisplayName,
    ReleaseStoryCard Briefing,
    string Objective,
    ReleaseCampaignEvent? Event,
    ReleaseStoryCard Result,
    long BudgetGrantCashUnit,
    IReadOnlyList<ReleaseCampaignLoad> ActiveLoads,
    IReadOnlyList<string> RequiredNormalLoadIds,
    IReadOnlyList<string> RequiredEventLoadIds,
    IReadOnlyList<ReleaseConnectionRequirement> ConnectionRequirements)
{
    private IReadOnlyList<ReleaseCampaignLoad> _activeLoads =
        Array.AsReadOnly(ActiveLoads.ToArray());
    private IReadOnlyList<string> _requiredNormalLoadIds =
        Array.AsReadOnly(RequiredNormalLoadIds.ToArray());
    private IReadOnlyList<string> _requiredEventLoadIds =
        Array.AsReadOnly(RequiredEventLoadIds.ToArray());
    private IReadOnlyList<ReleaseConnectionRequirement> _connectionRequirements =
        Array.AsReadOnly(ConnectionRequirements.ToArray());

    public IReadOnlyList<ReleaseCampaignLoad> ActiveLoads
    {
        get => _activeLoads;
        init => _activeLoads = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> RequiredNormalLoadIds
    {
        get => _requiredNormalLoadIds;
        init => _requiredNormalLoadIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> RequiredEventLoadIds
    {
        get => _requiredEventLoadIds;
        init => _requiredEventLoadIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ReleaseConnectionRequirement> ConnectionRequirements
    {
        get => _connectionRequirements;
        init => _connectionRequirements = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record ReleaseCampaignDefinition(
    string SchemaVersion,
    string CampaignId,
    string DisplayName,
    long InitialCashUnit,
    IReadOnlyList<string> InitialEdgeIds,
    IReadOnlyList<ReleaseCampaignChapter> Chapters)
{
    public const string SupportedSchemaVersion = "gridworks.release.campaign.v1";

    private IReadOnlyList<string> _initialEdgeIds =
        Array.AsReadOnly(InitialEdgeIds.ToArray());
    private IReadOnlyList<ReleaseCampaignChapter> _chapters =
        Array.AsReadOnly(Chapters.ToArray());

    public IReadOnlyList<string> InitialEdgeIds
    {
        get => _initialEdgeIds;
        init => _initialEdgeIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ReleaseCampaignChapter> Chapters
    {
        get => _chapters;
        init => _chapters = Array.AsReadOnly(value.ToArray());
    }
}

public enum ReleaseCampaignCommandKind
{
    SetNodeDraft,
    CancelNodeDraft,
    StartLineDraft,
    AddLinePoint,
    UndoLinePoint,
    CancelLineDraft,
    OrderNode,
    OrderLine,
    AdvanceConstruction,
    EvaluateChapter,
}

public sealed record ReleaseCampaignCommand(
    ReleaseCampaignCommandKind Kind,
    ReleasePoint? Position = null,
    string? NodeClassId = null,
    string? StartNodeId = null,
    string? LineClassId = null,
    string? PoleClassId = null);

public enum ReleaseCampaignError
{
    WrongPhase,
    InsufficientCash,
    ObjectiveNotMet,
    CampaignComplete,
    InvalidCommand,
    ConstructionRejected,
}

public sealed record ReleaseChapterAssessment(
    bool Passed,
    string? FailedLoadId,
    ReleaseSupplyFailure? SupplyFailure,
    bool FailedDuringEvent,
    string? FailedConnectionNodeId,
    int? RequiredConnections,
    int? ActualConnections);

public sealed record ReleaseCampaignSnapshot(
    ReleaseConstructionSnapshot Construction,
    ReleaseCampaignChapter Chapter,
    int ChapterIndex,
    int ChapterCount,
    long CashUnit,
    ReleaseNetworkEvaluation NormalEvaluation,
    ReleaseNetworkEvaluation EventEvaluation,
    bool CampaignComplete,
    int CommandCount,
    int ChapterStartCommandCount);

public sealed record ReleaseCampaignCommandResult(
    bool Accepted,
    ReleaseCampaignError? Error,
    ReleaseConstructionError? ConstructionError,
    ReleaseCampaignSnapshot Snapshot,
    ReleaseChapterAssessment? Assessment,
    ReleaseCampaignChapter? CompletedChapter);

public sealed record ReleaseCampaignSave(
    string SchemaVersion,
    string CampaignId,
    string CampaignSha256,
    string WorldId,
    string WorldSha256,
    IReadOnlyList<ReleaseCampaignCommand> Commands)
{
    public const string SupportedSchemaVersion = "gridworks.release.campaign-save.v2";

    private IReadOnlyList<ReleaseCampaignCommand> _commands =
        Array.AsReadOnly(Commands.ToArray());

    public IReadOnlyList<ReleaseCampaignCommand> Commands
    {
        get => _commands;
        init => _commands = Array.AsReadOnly(value.ToArray());
    }
}

public enum ReleaseDocumentLoadStatus
{
    Missing,
    Loaded,
    Invalid,
}

public sealed record ReleaseCampaignSaveLoadResult(
    ReleaseDocumentLoadStatus Status,
    ReleaseCampaignSave? Save,
    string? ErrorMessage);

public sealed class ReleaseCampaignValidationException : Exception
{
    public ReleaseCampaignValidationException(string message)
        : base(message)
    {
    }

    public ReleaseCampaignValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class ReleasePersistenceValidationException : Exception
{
    public ReleasePersistenceValidationException(string message)
        : base(message)
    {
    }

    public ReleasePersistenceValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
