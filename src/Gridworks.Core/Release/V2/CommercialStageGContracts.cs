namespace Gridworks.Core.Release.V2;

public enum CommercialApprovalGateKind
{
    CommandCapacity,
    ConstructionReady,
    PromiseDecision,
    ConnectionRequirement,
    SafetyDemand,
    KeptPromiseDemand,
    FutureSafety,
}

public enum CommercialPhaseComparisonApplicability
{
    Evaluated,
    AwaitingPromiseDecision,
    DeferredByPromise,
}

public enum CommercialRecoveryKind
{
    RecentProject,
    DecisionWindow,
    Chapter,
    PreviousChapter,
}

public sealed record CommercialSupplyDiagnostic(
    string PhaseId,
    string PhaseDisplayName,
    int PhaseNumber,
    int PhaseCount,
    string LoadId,
    string LoadDisplayName,
    CommercialObligationKind Obligation,
    string ObligationDisplayName,
    ThermalFailureKind FailureKind,
    string? AttemptedSourceId,
    string? AttemptedSourceDisplayName,
    ThermalAssetKind? LimitingAssetKind,
    string? LimitingAssetId,
    string? LimitingAssetDisplayName,
    long RequiredKw,
    long AvailableKw,
    long ShortfallKw,
    IReadOnlyList<string> PathNodeIds,
    IReadOnlyList<string> PathNodeDisplayNames,
    IReadOnlyList<string> PathEdgeIds,
    IReadOnlyList<string> PathEdgeDisplayNames)
{
    private IReadOnlyList<string> _pathNodeIds = Freeze(PathNodeIds);
    private IReadOnlyList<string> _pathNodeDisplayNames = Freeze(PathNodeDisplayNames);
    private IReadOnlyList<string> _pathEdgeIds = Freeze(PathEdgeIds);
    private IReadOnlyList<string> _pathEdgeDisplayNames = Freeze(PathEdgeDisplayNames);

    public IReadOnlyList<string> PathNodeIds
    {
        get => _pathNodeIds;
        init => _pathNodeIds = Freeze(value);
    }

    public IReadOnlyList<string> PathNodeDisplayNames
    {
        get => _pathNodeDisplayNames;
        init => _pathNodeDisplayNames = Freeze(value);
    }

    public IReadOnlyList<string> PathEdgeIds
    {
        get => _pathEdgeIds;
        init => _pathEdgeIds = Freeze(value);
    }

    public IReadOnlyList<string> PathEdgeDisplayNames
    {
        get => _pathEdgeDisplayNames;
        init => _pathEdgeDisplayNames = Freeze(value);
    }

    public bool Equals(CommercialSupplyDiagnostic? other) => other is not null &&
        string.Equals(PhaseId, other.PhaseId, StringComparison.Ordinal) &&
        string.Equals(PhaseDisplayName, other.PhaseDisplayName, StringComparison.Ordinal) &&
        PhaseNumber == other.PhaseNumber &&
        PhaseCount == other.PhaseCount &&
        string.Equals(LoadId, other.LoadId, StringComparison.Ordinal) &&
        string.Equals(LoadDisplayName, other.LoadDisplayName, StringComparison.Ordinal) &&
        Obligation == other.Obligation &&
        string.Equals(ObligationDisplayName, other.ObligationDisplayName, StringComparison.Ordinal) &&
        FailureKind == other.FailureKind &&
        string.Equals(AttemptedSourceId, other.AttemptedSourceId, StringComparison.Ordinal) &&
        string.Equals(
            AttemptedSourceDisplayName,
            other.AttemptedSourceDisplayName,
            StringComparison.Ordinal) &&
        LimitingAssetKind == other.LimitingAssetKind &&
        string.Equals(LimitingAssetId, other.LimitingAssetId, StringComparison.Ordinal) &&
        string.Equals(
            LimitingAssetDisplayName,
            other.LimitingAssetDisplayName,
            StringComparison.Ordinal) &&
        RequiredKw == other.RequiredKw &&
        AvailableKw == other.AvailableKw &&
        ShortfallKw == other.ShortfallKw &&
        PathNodeIds.SequenceEqual(other.PathNodeIds, StringComparer.Ordinal) &&
        PathNodeDisplayNames.SequenceEqual(
            other.PathNodeDisplayNames,
            StringComparer.Ordinal) &&
        PathEdgeIds.SequenceEqual(other.PathEdgeIds, StringComparer.Ordinal) &&
        PathEdgeDisplayNames.SequenceEqual(
            other.PathEdgeDisplayNames,
            StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(PhaseId, StringComparer.Ordinal);
        hash.Add(PhaseDisplayName, StringComparer.Ordinal);
        hash.Add(PhaseNumber);
        hash.Add(PhaseCount);
        hash.Add(LoadId, StringComparer.Ordinal);
        hash.Add(LoadDisplayName, StringComparer.Ordinal);
        hash.Add(Obligation);
        hash.Add(ObligationDisplayName, StringComparer.Ordinal);
        hash.Add(FailureKind);
        hash.Add(AttemptedSourceId, StringComparer.Ordinal);
        hash.Add(AttemptedSourceDisplayName, StringComparer.Ordinal);
        hash.Add(LimitingAssetKind);
        hash.Add(LimitingAssetId, StringComparer.Ordinal);
        hash.Add(LimitingAssetDisplayName, StringComparer.Ordinal);
        hash.Add(RequiredKw);
        hash.Add(AvailableKw);
        hash.Add(ShortfallKw);
        CommercialStageGStructural.AddSequence(ref hash, PathNodeIds, StringComparer.Ordinal);
        CommercialStageGStructural.AddSequence(
            ref hash,
            PathNodeDisplayNames,
            StringComparer.Ordinal);
        CommercialStageGStructural.AddSequence(ref hash, PathEdgeIds, StringComparer.Ordinal);
        CommercialStageGStructural.AddSequence(
            ref hash,
            PathEdgeDisplayNames,
            StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<string> Freeze(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record CommercialApprovalChecklistItem(
    string ItemId,
    string Label,
    CommercialApprovalGateKind Kind,
    bool Passed,
    string? PhaseId,
    string? PhaseDisplayName,
    int? PhaseNumber,
    int PhaseCount,
    CommercialObligationKind? Obligation,
    string? LoadId,
    string? NodeId,
    string? LimitingAssetId,
    long? Required,
    long? Current,
    long? Shortfall,
    IReadOnlyList<string> PathNodeIds,
    IReadOnlyList<string> PathEdgeIds,
    CommercialSupplyDiagnostic? FailureDiagnostic)
{
    private IReadOnlyList<string> _pathNodeIds = Freeze(PathNodeIds);
    private IReadOnlyList<string> _pathEdgeIds = Freeze(PathEdgeIds);

    public IReadOnlyList<string> PathNodeIds
    {
        get => _pathNodeIds;
        init => _pathNodeIds = Freeze(value);
    }

    public IReadOnlyList<string> PathEdgeIds
    {
        get => _pathEdgeIds;
        init => _pathEdgeIds = Freeze(value);
    }

    public bool Equals(CommercialApprovalChecklistItem? other) => other is not null &&
        string.Equals(ItemId, other.ItemId, StringComparison.Ordinal) &&
        string.Equals(Label, other.Label, StringComparison.Ordinal) &&
        Kind == other.Kind &&
        Passed == other.Passed &&
        string.Equals(PhaseId, other.PhaseId, StringComparison.Ordinal) &&
        string.Equals(PhaseDisplayName, other.PhaseDisplayName, StringComparison.Ordinal) &&
        PhaseNumber == other.PhaseNumber &&
        PhaseCount == other.PhaseCount &&
        Obligation == other.Obligation &&
        string.Equals(LoadId, other.LoadId, StringComparison.Ordinal) &&
        string.Equals(NodeId, other.NodeId, StringComparison.Ordinal) &&
        string.Equals(LimitingAssetId, other.LimitingAssetId, StringComparison.Ordinal) &&
        Required == other.Required &&
        Current == other.Current &&
        Shortfall == other.Shortfall &&
        PathNodeIds.SequenceEqual(other.PathNodeIds, StringComparer.Ordinal) &&
        PathEdgeIds.SequenceEqual(other.PathEdgeIds, StringComparer.Ordinal) &&
        Equals(FailureDiagnostic, other.FailureDiagnostic);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ItemId, StringComparer.Ordinal);
        hash.Add(Label, StringComparer.Ordinal);
        hash.Add(Kind);
        hash.Add(Passed);
        hash.Add(PhaseId, StringComparer.Ordinal);
        hash.Add(PhaseDisplayName, StringComparer.Ordinal);
        hash.Add(PhaseNumber);
        hash.Add(PhaseCount);
        hash.Add(Obligation);
        hash.Add(LoadId, StringComparer.Ordinal);
        hash.Add(NodeId, StringComparer.Ordinal);
        hash.Add(LimitingAssetId, StringComparer.Ordinal);
        hash.Add(Required);
        hash.Add(Current);
        hash.Add(Shortfall);
        CommercialStageGStructural.AddSequence(ref hash, PathNodeIds, StringComparer.Ordinal);
        CommercialStageGStructural.AddSequence(ref hash, PathEdgeIds, StringComparer.Ordinal);
        hash.Add(FailureDiagnostic);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<string> Freeze(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record CommercialApprovalChecklist(
    string? WindowId,
    int WindowNumber,
    int WindowCount,
    int FirstPhaseNumber,
    int PhaseCount,
    bool CanApprove,
    int RemainingBlockerCount,
    IReadOnlyList<CommercialApprovalChecklistItem> Items)
{
    private IReadOnlyList<CommercialApprovalChecklistItem> _items = Freeze(Items);

    public static CommercialApprovalChecklist Empty { get; } = new(
        null,
        0,
        0,
        0,
        0,
        false,
        0,
        Array.Empty<CommercialApprovalChecklistItem>());

    public IReadOnlyList<CommercialApprovalChecklistItem> Items
    {
        get => _items;
        init => _items = Freeze(value);
    }

    public bool Equals(CommercialApprovalChecklist? other) => other is not null &&
        string.Equals(WindowId, other.WindowId, StringComparison.Ordinal) &&
        WindowNumber == other.WindowNumber &&
        WindowCount == other.WindowCount &&
        FirstPhaseNumber == other.FirstPhaseNumber &&
        PhaseCount == other.PhaseCount &&
        CanApprove == other.CanApprove &&
        RemainingBlockerCount == other.RemainingBlockerCount &&
        Items.SequenceEqual(other.Items);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(WindowId, StringComparer.Ordinal);
        hash.Add(WindowNumber);
        hash.Add(WindowCount);
        hash.Add(FirstPhaseNumber);
        hash.Add(PhaseCount);
        hash.Add(CanApprove);
        hash.Add(RemainingBlockerCount);
        CommercialStageGStructural.AddSequence(ref hash, Items);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<CommercialApprovalChecklistItem> Freeze(
        IReadOnlyList<CommercialApprovalChecklistItem> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record CommercialPhaseComparisonRow(
    string PhaseId,
    string PhaseDisplayName,
    int PhaseNumber,
    int PhaseCount,
    bool IsInCurrentWindow,
    string? PhaseStoryTitle,
    string? PhaseStoryBody,
    string LoadId,
    string LoadDisplayName,
    CommercialObligationKind Obligation,
    string ObligationDisplayName,
    CommercialPhaseComparisonApplicability Applicability,
    long DemandKw,
    long DeliveredKw,
    string? SourceId,
    string? SourceDisplayName,
    long? MinimumRemainingKw,
    ThermalOperatingState? CurrentPathState,
    ThermalOperatingState? NextPathState,
    IReadOnlyList<string> PathNodeIds,
    IReadOnlyList<string> PathEdgeIds,
    CommercialSupplyDiagnostic? FailureDiagnostic)
{
    private IReadOnlyList<string> _pathNodeIds = Freeze(PathNodeIds);
    private IReadOnlyList<string> _pathEdgeIds = Freeze(PathEdgeIds);

    public IReadOnlyList<string> PathNodeIds
    {
        get => _pathNodeIds;
        init => _pathNodeIds = Freeze(value);
    }

    public IReadOnlyList<string> PathEdgeIds
    {
        get => _pathEdgeIds;
        init => _pathEdgeIds = Freeze(value);
    }

    public bool Equals(CommercialPhaseComparisonRow? other) => other is not null &&
        string.Equals(PhaseId, other.PhaseId, StringComparison.Ordinal) &&
        string.Equals(PhaseDisplayName, other.PhaseDisplayName, StringComparison.Ordinal) &&
        PhaseNumber == other.PhaseNumber &&
        PhaseCount == other.PhaseCount &&
        IsInCurrentWindow == other.IsInCurrentWindow &&
        string.Equals(PhaseStoryTitle, other.PhaseStoryTitle, StringComparison.Ordinal) &&
        string.Equals(PhaseStoryBody, other.PhaseStoryBody, StringComparison.Ordinal) &&
        string.Equals(LoadId, other.LoadId, StringComparison.Ordinal) &&
        string.Equals(LoadDisplayName, other.LoadDisplayName, StringComparison.Ordinal) &&
        Obligation == other.Obligation &&
        string.Equals(ObligationDisplayName, other.ObligationDisplayName, StringComparison.Ordinal) &&
        Applicability == other.Applicability &&
        DemandKw == other.DemandKw &&
        DeliveredKw == other.DeliveredKw &&
        string.Equals(SourceId, other.SourceId, StringComparison.Ordinal) &&
        string.Equals(SourceDisplayName, other.SourceDisplayName, StringComparison.Ordinal) &&
        MinimumRemainingKw == other.MinimumRemainingKw &&
        CurrentPathState == other.CurrentPathState &&
        NextPathState == other.NextPathState &&
        PathNodeIds.SequenceEqual(other.PathNodeIds, StringComparer.Ordinal) &&
        PathEdgeIds.SequenceEqual(other.PathEdgeIds, StringComparer.Ordinal) &&
        Equals(FailureDiagnostic, other.FailureDiagnostic);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(PhaseId, StringComparer.Ordinal);
        hash.Add(PhaseDisplayName, StringComparer.Ordinal);
        hash.Add(PhaseNumber);
        hash.Add(PhaseCount);
        hash.Add(IsInCurrentWindow);
        hash.Add(PhaseStoryTitle, StringComparer.Ordinal);
        hash.Add(PhaseStoryBody, StringComparer.Ordinal);
        hash.Add(LoadId, StringComparer.Ordinal);
        hash.Add(LoadDisplayName, StringComparer.Ordinal);
        hash.Add(Obligation);
        hash.Add(ObligationDisplayName, StringComparer.Ordinal);
        hash.Add(Applicability);
        hash.Add(DemandKw);
        hash.Add(DeliveredKw);
        hash.Add(SourceId, StringComparer.Ordinal);
        hash.Add(SourceDisplayName, StringComparer.Ordinal);
        hash.Add(MinimumRemainingKw);
        hash.Add(CurrentPathState);
        hash.Add(NextPathState);
        CommercialStageGStructural.AddSequence(ref hash, PathNodeIds, StringComparer.Ordinal);
        CommercialStageGStructural.AddSequence(ref hash, PathEdgeIds, StringComparer.Ordinal);
        hash.Add(FailureDiagnostic);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<string> Freeze(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public abstract record CommercialNextProjectPlan;

public sealed record CommercialNextNodeProjectPlan(
    string NodeClassId,
    MapPoint Position) : CommercialNextProjectPlan;

public sealed record CommercialNextLineProjectPlan(
    string StartNodeId,
    string LineClassId,
    string PoleClassId,
    IReadOnlyList<MapPoint> IntermediatePoints,
    string EndNodeId) : CommercialNextProjectPlan
{
    private IReadOnlyList<MapPoint> _intermediatePoints = Freeze(IntermediatePoints);

    public IReadOnlyList<MapPoint> IntermediatePoints
    {
        get => _intermediatePoints;
        init => _intermediatePoints = Freeze(value);
    }

    public bool Equals(CommercialNextLineProjectPlan? other) => other is not null &&
        string.Equals(StartNodeId, other.StartNodeId, StringComparison.Ordinal) &&
        string.Equals(LineClassId, other.LineClassId, StringComparison.Ordinal) &&
        string.Equals(PoleClassId, other.PoleClassId, StringComparison.Ordinal) &&
        IntermediatePoints.SequenceEqual(other.IntermediatePoints) &&
        string.Equals(EndNodeId, other.EndNodeId, StringComparison.Ordinal);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(StartNodeId, StringComparer.Ordinal);
        hash.Add(LineClassId, StringComparer.Ordinal);
        hash.Add(PoleClassId, StringComparer.Ordinal);
        CommercialStageGStructural.AddSequence(ref hash, IntermediatePoints);
        hash.Add(EndNodeId, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<MapPoint> Freeze(IReadOnlyList<MapPoint> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public enum CommercialConstructionForecastStepRole
{
    CurrentDraft,
    ExplicitNextPlan,
}

public sealed record CommercialConstructionForecastStep(
    int SequenceNumber,
    ConstructionKind Kind,
    bool Accepted,
    CommercialCampaignRunError? Error,
    ConstructionError? ConstructionError,
    long? BuildMinutes,
    long? CompletionMinute,
    long? RemainingMinutesAfterCompletion)
{
    public CommercialConstructionForecastStepRole StepRole { get; init; } =
        CommercialConstructionForecastStepRole.CurrentDraft;
}

public sealed record CommercialConstructionWindowForecast(
    string? WindowId,
    long WindowStartMinute,
    long CurrentMinute,
    long AlreadySpentMinutes,
    int? BuildMinutesAvailable,
    long? DeadlineMinute,
    long? RemainingMinutesNow,
    IReadOnlyList<CommercialConstructionForecastStep> Steps)
{
    private IReadOnlyList<CommercialConstructionForecastStep> _steps = Freeze(Steps);

    public static CommercialConstructionWindowForecast Empty { get; } = new(
        null,
        0,
        0,
        0,
        null,
        null,
        null,
        Array.Empty<CommercialConstructionForecastStep>());

    public IReadOnlyList<CommercialConstructionForecastStep> Steps
    {
        get => _steps;
        init => _steps = Freeze(value);
    }

    public bool Equals(CommercialConstructionWindowForecast? other) => other is not null &&
        string.Equals(WindowId, other.WindowId, StringComparison.Ordinal) &&
        WindowStartMinute == other.WindowStartMinute &&
        CurrentMinute == other.CurrentMinute &&
        AlreadySpentMinutes == other.AlreadySpentMinutes &&
        BuildMinutesAvailable == other.BuildMinutesAvailable &&
        DeadlineMinute == other.DeadlineMinute &&
        RemainingMinutesNow == other.RemainingMinutesNow &&
        Steps.SequenceEqual(other.Steps);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(WindowId, StringComparer.Ordinal);
        hash.Add(WindowStartMinute);
        hash.Add(CurrentMinute);
        hash.Add(AlreadySpentMinutes);
        hash.Add(BuildMinutesAvailable);
        hash.Add(DeadlineMinute);
        hash.Add(RemainingMinutesNow);
        CommercialStageGStructural.AddSequence(ref hash, Steps);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<CommercialConstructionForecastStep> Freeze(
        IReadOnlyList<CommercialConstructionForecastStep> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record CommercialRecoveryPreview(
    CommercialRecoveryKind Kind,
    bool Enabled,
    int? TargetCommandCount,
    ConstructionKind? RemovedProjectKind,
    IReadOnlyList<string> RemovedNodeIds,
    IReadOnlyList<string> RemovedEdgeIds,
    int RemovedRoutePointCount,
    long? RestoredCashUnit,
    long? RestoredMinute,
    int? RestoredChapterIndex,
    string? RestoredChapterDisplayName,
    string? RestoredPhaseId,
    string? RestoredPhaseDisplayName,
    int? RestoredPhaseNumber,
    int? RestoredPhaseCount,
    CommercialPromiseDecision? RestoredPromiseDecision,
    string? RestoredPromiseDisplayName,
    IReadOnlyList<string> RestoredCoolingAssetIds)
{
    private IReadOnlyList<string> _removedNodeIds = Freeze(RemovedNodeIds);
    private IReadOnlyList<string> _removedEdgeIds = Freeze(RemovedEdgeIds);
    private IReadOnlyList<string> _restoredCoolingAssetIds = Freeze(RestoredCoolingAssetIds);

    public IReadOnlyList<string> RemovedNodeIds
    {
        get => _removedNodeIds;
        init => _removedNodeIds = Freeze(value);
    }

    public IReadOnlyList<string> RemovedEdgeIds
    {
        get => _removedEdgeIds;
        init => _removedEdgeIds = Freeze(value);
    }

    public IReadOnlyList<string> RestoredCoolingAssetIds
    {
        get => _restoredCoolingAssetIds;
        init => _restoredCoolingAssetIds = Freeze(value);
    }

    public int RemovedNodeCount => RemovedNodeIds.Count;

    public int RemovedEdgeCount => RemovedEdgeIds.Count;

    public int RemovedCompletedNodeProjectCount { get; init; }

    public int RemovedCompletedLineProjectCount { get; init; }

    public int RemovedCompletedLineRoutePointCount { get; init; }

    public ConstructionKind? DiscardedDraftKind { get; init; }

    public int DiscardedDraftRoutePointCount { get; init; }

    public ConstructionKind? DiscardedActiveConstructionKind { get; init; }

    public int DiscardedActiveLineRoutePointCount { get; init; }

    public bool Equals(CommercialRecoveryPreview? other) => other is not null &&
        Kind == other.Kind &&
        Enabled == other.Enabled &&
        TargetCommandCount == other.TargetCommandCount &&
        RemovedProjectKind == other.RemovedProjectKind &&
        RemovedNodeIds.SequenceEqual(other.RemovedNodeIds, StringComparer.Ordinal) &&
        RemovedEdgeIds.SequenceEqual(other.RemovedEdgeIds, StringComparer.Ordinal) &&
        RemovedRoutePointCount == other.RemovedRoutePointCount &&
        RestoredCashUnit == other.RestoredCashUnit &&
        RestoredMinute == other.RestoredMinute &&
        RestoredChapterIndex == other.RestoredChapterIndex &&
        string.Equals(
            RestoredChapterDisplayName,
            other.RestoredChapterDisplayName,
            StringComparison.Ordinal) &&
        string.Equals(RestoredPhaseId, other.RestoredPhaseId, StringComparison.Ordinal) &&
        string.Equals(
            RestoredPhaseDisplayName,
            other.RestoredPhaseDisplayName,
            StringComparison.Ordinal) &&
        RestoredPhaseNumber == other.RestoredPhaseNumber &&
        RestoredPhaseCount == other.RestoredPhaseCount &&
        RestoredPromiseDecision == other.RestoredPromiseDecision &&
        string.Equals(
            RestoredPromiseDisplayName,
            other.RestoredPromiseDisplayName,
            StringComparison.Ordinal) &&
        RestoredCoolingAssetIds.SequenceEqual(
            other.RestoredCoolingAssetIds,
            StringComparer.Ordinal) &&
        RemovedCompletedNodeProjectCount == other.RemovedCompletedNodeProjectCount &&
        RemovedCompletedLineProjectCount == other.RemovedCompletedLineProjectCount &&
        RemovedCompletedLineRoutePointCount == other.RemovedCompletedLineRoutePointCount &&
        DiscardedDraftKind == other.DiscardedDraftKind &&
        DiscardedDraftRoutePointCount == other.DiscardedDraftRoutePointCount &&
        DiscardedActiveConstructionKind == other.DiscardedActiveConstructionKind &&
        DiscardedActiveLineRoutePointCount ==
            other.DiscardedActiveLineRoutePointCount;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(Enabled);
        hash.Add(TargetCommandCount);
        hash.Add(RemovedProjectKind);
        CommercialStageGStructural.AddSequence(ref hash, RemovedNodeIds, StringComparer.Ordinal);
        CommercialStageGStructural.AddSequence(ref hash, RemovedEdgeIds, StringComparer.Ordinal);
        hash.Add(RemovedRoutePointCount);
        hash.Add(RestoredCashUnit);
        hash.Add(RestoredMinute);
        hash.Add(RestoredChapterIndex);
        hash.Add(RestoredChapterDisplayName, StringComparer.Ordinal);
        hash.Add(RestoredPhaseId, StringComparer.Ordinal);
        hash.Add(RestoredPhaseDisplayName, StringComparer.Ordinal);
        hash.Add(RestoredPhaseNumber);
        hash.Add(RestoredPhaseCount);
        hash.Add(RestoredPromiseDecision);
        hash.Add(RestoredPromiseDisplayName, StringComparer.Ordinal);
        CommercialStageGStructural.AddSequence(
            ref hash,
            RestoredCoolingAssetIds,
            StringComparer.Ordinal);
        hash.Add(RemovedCompletedNodeProjectCount);
        hash.Add(RemovedCompletedLineProjectCount);
        hash.Add(RemovedCompletedLineRoutePointCount);
        hash.Add(DiscardedDraftKind);
        hash.Add(DiscardedDraftRoutePointCount);
        hash.Add(DiscardedActiveConstructionKind);
        hash.Add(DiscardedActiveLineRoutePointCount);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<string> Freeze(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

internal static class CommercialStageGStructural
{
    internal static void AddSequence<T>(
        ref HashCode hash,
        IEnumerable<T> values,
        IEqualityComparer<T>? comparer = null)
    {
        foreach (T value in values)
        {
            hash.Add(value, comparer);
        }
    }
}
