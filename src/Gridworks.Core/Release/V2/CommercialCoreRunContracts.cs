namespace Gridworks.Core.Release.V2;

public enum CommercialCoreCommandKind
{
    SetNodeDraft,
    CancelNodeDraft,
    StartLineDraft,
    AddLinePoint,
    MoveLinePoint,
    FinishLineDraft,
    UndoLinePoint,
    CancelLineDraft,
    OrderNode,
    OrderLine,
    AdvanceConstruction,
    SetPromiseDecision,
    ApproveDecisionWindow,
}

public sealed record CommercialCoreCommand(
    CommercialCoreCommandKind Kind,
    MapPoint? Position = null,
    string? NodeClassId = null,
    string? StartNodeId = null,
    string? LineClassId = null,
    string? PoleClassId = null,
    string? EndNodeId = null,
    int? PointIndex = null,
    PromiseDecision? PromiseDecision = null);

public enum CommercialCoreError
{
    WrongPhase,
    InvalidCommand,
    ConstructionRejected,
    InsufficientCash,
    PromiseDecisionRequired,
    DeadlineExceeded,
    SafetyDutyFailed,
    KeptPromiseFailed,
    CampaignComplete,
    NothingToRollback,
}

public sealed record CommercialDecisionPreview(
    bool Accepted,
    CommercialCoreError? Error,
    long ProjectedMinute,
    long ProjectedCashUnit,
    IReadOnlyList<ThermalIntervalResult> PhaseResults,
    string? FailedDemandId,
    ThermalSupplyFailure? SupplyFailure,
    string? FirstBottleneckAssetId)
{
    private IReadOnlyList<ThermalIntervalResult> _phaseResults =
        Array.AsReadOnly(PhaseResults.ToArray());

    public IReadOnlyList<ThermalIntervalResult> PhaseResults
    {
        get => _phaseResults;
        init => _phaseResults = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record CommercialChapterResultRecord(
    string ChapterId,
    string ChapterDisplayName,
    CommercialStoryCard Story,
    PromiseDecision? PromiseDecision,
    IReadOnlyList<CommercialResultDemandFact> DemandFacts,
    IReadOnlyList<string> SuppliedDemandIds,
    IReadOnlyList<string> SourceNodeIds,
    IReadOnlyList<string> EmergencyAssetIds,
    IReadOnlyList<string> ProtectiveOutageAssetIds,
    long RemainingCashUnit)
{
    private IReadOnlyList<CommercialResultDemandFact> _demandFacts =
        Array.AsReadOnly(DemandFacts.ToArray());
    private IReadOnlyList<string> _suppliedDemandIds =
        Array.AsReadOnly(SuppliedDemandIds.ToArray());
    private IReadOnlyList<string> _sourceNodeIds = Array.AsReadOnly(SourceNodeIds.ToArray());
    private IReadOnlyList<string> _emergencyAssetIds =
        Array.AsReadOnly(EmergencyAssetIds.ToArray());
    private IReadOnlyList<string> _protectiveOutageAssetIds =
        Array.AsReadOnly(ProtectiveOutageAssetIds.ToArray());

    public IReadOnlyList<CommercialResultDemandFact> DemandFacts
    {
        get => _demandFacts;
        init => _demandFacts = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> SuppliedDemandIds
    {
        get => _suppliedDemandIds;
        init => _suppliedDemandIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> SourceNodeIds
    {
        get => _sourceNodeIds;
        init => _sourceNodeIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> EmergencyAssetIds
    {
        get => _emergencyAssetIds;
        init => _emergencyAssetIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> ProtectiveOutageAssetIds
    {
        get => _protectiveOutageAssetIds;
        init => _protectiveOutageAssetIds = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record CommercialResultDemandFact(
    string PhaseId,
    string DemandId,
    string FacilityNodeId,
    CommercialCoreObligationKind ObligationKind,
    bool Supplied,
    bool Deferred,
    string? SourceNodeId,
    IReadOnlyList<string> PathNodeIds,
    IReadOnlyList<string> PathEdgeIds,
    IReadOnlyList<string> EmergencyAssetIds,
    ThermalSupplyFailure Failure,
    string? FirstBottleneckAssetId)
{
    private IReadOnlyList<string> _pathNodeIds = Array.AsReadOnly(PathNodeIds.ToArray());
    private IReadOnlyList<string> _pathEdgeIds = Array.AsReadOnly(PathEdgeIds.ToArray());
    private IReadOnlyList<string> _emergencyAssetIds =
        Array.AsReadOnly(EmergencyAssetIds.ToArray());

    public IReadOnlyList<string> PathNodeIds
    {
        get => _pathNodeIds;
        init => _pathNodeIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> PathEdgeIds
    {
        get => _pathEdgeIds;
        init => _pathEdgeIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> EmergencyAssetIds
    {
        get => _emergencyAssetIds;
        init => _emergencyAssetIds = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record CommercialCoreSnapshot(
    CommercialCoreChapter Chapter,
    int ChapterIndex,
    int ChapterCount,
    CommercialCoreDecisionWindow? DecisionWindow,
    int DecisionWindowIndex,
    ConstructionSnapshot Construction,
    long CashUnit,
    PromiseDecision? PromiseDecision,
    IReadOnlyList<ThermalAssetMemory> ThermalMemory,
    IReadOnlyList<ThermalIntervalResult> CommittedPhaseResults,
    IReadOnlyList<CommercialChapterResultRecord> ChapterResults,
    bool CampaignComplete,
    int CommandCount,
    int ChapterStartCommandCount,
    int? RecentProjectCheckpointCommandCount)
{
    private IReadOnlyList<ThermalAssetMemory> _thermalMemory =
        Array.AsReadOnly(ThermalMemory.ToArray());
    private IReadOnlyList<ThermalIntervalResult> _committedPhaseResults =
        Array.AsReadOnly(CommittedPhaseResults.ToArray());
    private IReadOnlyList<CommercialChapterResultRecord> _chapterResults =
        Array.AsReadOnly(ChapterResults.ToArray());

    public IReadOnlyList<ThermalAssetMemory> ThermalMemory
    {
        get => _thermalMemory;
        init => _thermalMemory = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ThermalIntervalResult> CommittedPhaseResults
    {
        get => _committedPhaseResults;
        init => _committedPhaseResults = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<CommercialChapterResultRecord> ChapterResults
    {
        get => _chapterResults;
        init => _chapterResults = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record CommercialCoreCommandResult(
    bool Accepted,
    CommercialCoreError? Error,
    ConstructionError? ConstructionError,
    CommercialCoreSnapshot Snapshot,
    CommercialDecisionPreview? DecisionPreview,
    CommercialChapterResultRecord? CompletedChapter);

public sealed class CommercialCoreReplayException : Exception
{
    public CommercialCoreReplayException(string message)
        : base(message)
    {
    }
}
