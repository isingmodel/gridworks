using System.Collections;
using System.Text.Json.Serialization;
using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

internal static class RealtimeStructural
{
    public static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values is FrozenStructuralList<T>
            ? values
            : new FrozenStructuralList<T>(values);
    }

    public static RealtimeConstructionCompletion? FreezeCompletion(
        RealtimeConstructionCompletion? completion) => completion is null
        ? null
        : completion with
        {
            NodeIds = Freeze(completion.NodeIds),
            EdgeIds = Freeze(completion.EdgeIds),
        };

    private sealed class FrozenStructuralList<T> : IReadOnlyList<T>
    {
        private readonly T[] _items;

        public FrozenStructuralList(IReadOnlyList<T> values)
        {
            _items = new T[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                _items[index] = values[index];
            }
        }

        public int Count => _items.Length;
        public T this[int index] => _items[index];

        public IEnumerator<T> GetEnumerator() =>
            ((IEnumerable<T>)_items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj is not FrozenStructuralList<T> other ||
                other._items.Length != _items.Length)
            {
                return false;
            }
            var comparer = EqualityComparer<T>.Default;
            for (int index = 0; index < _items.Length; index++)
            {
                if (!comparer.Equals(_items[index], other._items[index]))
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(_items.Length);
            foreach (T item in _items)
            {
                hash.Add(item);
            }
            return hash.ToHashCode();
        }
    }
}

/// <summary>
/// Identifies the exact authored authorities that give a snapshot its future meaning.
/// Definition hashes are semantic hashes of the strictly loaded definitions, not hashes
/// of source-file formatting.
/// </summary>
public sealed record RealtimeStateAuthority(
    string CampaignSchemaVersion,
    string CampaignId,
    string CampaignDefinitionSha256,
    string WorldSchemaVersion,
    string WorldId,
    string WorldDefinitionSha256);

public enum RealtimeCommandKind
{
    SetNodeDraft,
    CancelNodeDraft,
    OrderNode,
    StartLineDraft,
    AddLinePoint,
    MoveLinePoint,
    UndoLinePoint,
    FinishLineDraft,
    CancelLineDraft,
    OrderLine,
    SetPromiseDecision,
}

public enum RealtimeRunError
{
    WrongState,
    InvalidCommandShape,
    ToolUnavailable,
    ConstructionRejected,
    InsufficientCash,
    PromiseUnavailable,
    PromiseDeadlinePassed,
    ClockMismatch,
    SequenceMismatch,
    TimeInPast,
    CommandLimit,
    ArithmeticOverflow,
}

public sealed record RealtimeCommand(
    RealtimeCommandKind Kind,
    string? FirstId = null,
    string? SecondId = null,
    string? ThirdId = null,
    MapPoint? Position = null,
    int? PointIndex = null,
    CommercialPromiseDecision? PromiseDecision = null)
{
    public static RealtimeCommand SetNodeDraft(string nodeClassId, MapPoint position) =>
        new(RealtimeCommandKind.SetNodeDraft, nodeClassId, Position: position);

    public static RealtimeCommand CancelNodeDraft() =>
        new(RealtimeCommandKind.CancelNodeDraft);

    public static RealtimeCommand OrderNode() => new(RealtimeCommandKind.OrderNode);

    public static RealtimeCommand StartLineDraft(
        string startNodeId,
        string lineClassId,
        string poleClassId) => new(
        RealtimeCommandKind.StartLineDraft,
        startNodeId,
        lineClassId,
        poleClassId);

    public static RealtimeCommand AddLinePoint(MapPoint position) =>
        new(RealtimeCommandKind.AddLinePoint, Position: position);

    public static RealtimeCommand MoveLinePoint(int pointIndex, MapPoint position) =>
        new(RealtimeCommandKind.MoveLinePoint, Position: position, PointIndex: pointIndex);

    public static RealtimeCommand UndoLinePoint() =>
        new(RealtimeCommandKind.UndoLinePoint);

    public static RealtimeCommand FinishLineDraft(string endNodeId) =>
        new(RealtimeCommandKind.FinishLineDraft, endNodeId);

    public static RealtimeCommand CancelLineDraft() =>
        new(RealtimeCommandKind.CancelLineDraft);

    public static RealtimeCommand OrderLine() => new(RealtimeCommandKind.OrderLine);

    public static RealtimeCommand SetPromiseDecision(CommercialPromiseDecision decision) =>
        new(RealtimeCommandKind.SetPromiseDecision, PromiseDecision: decision);
}

public sealed record TimedRealtimeCommand(long Sequence, long Minute, RealtimeCommand Command);

public enum RealtimeTransitionKind
{
    ChapterStarted,
    ForecastRevealed,
    PromiseDefaulted,
    ConstructionCompleted,
    EventStarted,
    EventCompleted,
    ChapterCompleted,
    CampaignCompleted,
    ThermalEmergencyEntered,
    ThermalEmergencyCleared,
    ThermalProtectiveTrip,
    ThermalRecovered,
}

public sealed record RealtimeEventOutcome(
    string ChapterId,
    string EventId,
    long StartMinute,
    long EndMinute,
    ThermalIntervalEvaluation FinalEvaluation,
    bool SafetySatisfied,
    bool PromiseSatisfied,
    IReadOnlyList<RealtimeDutySegment> DutySegments,
    IReadOnlyList<RealtimeEventIncident> Incidents,
    long? FirstSafetyUnservedMinute,
    long SafetyUnservedMinutes,
    long? FirstPromiseUnservedMinute,
    long PromiseUnservedMinutes)
{
    private IReadOnlyList<RealtimeDutySegment> _dutySegments =
        RealtimeStructural.Freeze(DutySegments);
    private IReadOnlyList<RealtimeEventIncident> _incidents =
        RealtimeStructural.Freeze(Incidents);

    public IReadOnlyList<RealtimeDutySegment> DutySegments
    {
        get => _dutySegments;
        init => _dutySegments = RealtimeStructural.Freeze(value);
    }

    public IReadOnlyList<RealtimeEventIncident> Incidents
    {
        get => _incidents;
        init => _incidents = RealtimeStructural.Freeze(value);
    }
}

public sealed record RealtimeDutyLoadFact(
    string LoadId,
    CommercialObligationKind Obligation,
    long DemandKw,
    long DeliveredKw,
    bool Required,
    ThermalSupplyFailure? Failure);

public sealed record RealtimeDutySegment(
    long StartMinute,
    long EndMinute,
    IReadOnlyList<RealtimeDutyLoadFact> Loads,
    bool SafetySatisfied,
    bool PromiseSatisfied)
{
    private IReadOnlyList<RealtimeDutyLoadFact> _loads =
        RealtimeStructural.Freeze(Loads);

    public IReadOnlyList<RealtimeDutyLoadFact> Loads
    {
        get => _loads;
        init => _loads = RealtimeStructural.Freeze(value);
    }
}

public sealed record RealtimeEventIncident(
    long Minute,
    string AssetId,
    ThermalAssetKind AssetKind,
    RealtimeThermalTransitionKind Kind);

public sealed record RealtimeEventDutyProgress(
    string ChapterId,
    string EventId,
    long SegmentStartMinute,
    IReadOnlyList<RealtimeDutySegment> ClosedSegments,
    IReadOnlyList<RealtimeEventIncident> Incidents)
{
    private IReadOnlyList<RealtimeDutySegment> _closedSegments =
        RealtimeStructural.Freeze(ClosedSegments);
    private IReadOnlyList<RealtimeEventIncident> _incidents =
        RealtimeStructural.Freeze(Incidents);

    public IReadOnlyList<RealtimeDutySegment> ClosedSegments
    {
        get => _closedSegments;
        init => _closedSegments = RealtimeStructural.Freeze(value);
    }

    public IReadOnlyList<RealtimeEventIncident> Incidents
    {
        get => _incidents;
        init => _incidents = RealtimeStructural.Freeze(value);
    }
}

/// <summary>
/// Canonical, presentation-safe state for every concurrently active authored event.
/// EventIndex is the stable index in the current chapter's canonical schedule.
/// </summary>
public sealed record RealtimeActiveEventState(
    int EventIndex,
    string EventId,
    RealtimeScheduledEventDefinition Event,
    RealtimeEventDutyProgress Duty);

public sealed record RealtimeConnectionRequirementFact(
    string NodeId,
    int CurrentConnections,
    int RequiredConnections)
{
    public bool Satisfied => CurrentConnections >= RequiredConnections;
}

public sealed record RealtimeConnectionRequirementAssessment(
    long EvaluatedMinute,
    bool FrozenForChapter,
    IReadOnlyList<RealtimeConnectionRequirementFact> Facts)
{
    private IReadOnlyList<RealtimeConnectionRequirementFact> _facts =
        RealtimeStructural.Freeze(Facts);

    public IReadOnlyList<RealtimeConnectionRequirementFact> Facts
    {
        get => _facts;
        init => _facts = RealtimeStructural.Freeze(value);
    }

    public bool Satisfied => Facts.All(item => item.Satisfied);
}

public sealed record RealtimeChapterOutcome(
    string ChapterId,
    long StartMinute,
    long EndMinute,
    CommercialPromiseDecision PromiseDecision,
    IReadOnlyList<RealtimeEventOutcome> Events,
    long EndingCashUnit,
    RealtimeConnectionRequirementAssessment? ConnectionRequirementAssessment = null)
{
    private IReadOnlyList<RealtimeEventOutcome> _events =
        RealtimeStructural.Freeze(Events);

    public IReadOnlyList<RealtimeEventOutcome> Events
    {
        get => _events;
        init => _events = RealtimeStructural.Freeze(value);
    }

    [JsonIgnore]
    public bool ObjectiveSatisfied =>
        Events.All(item => item.SafetySatisfied && item.PromiseSatisfied) &&
        (ConnectionRequirementAssessment?.Satisfied ?? true);
}

public sealed record RealtimeTransition(
    long Minute,
    RealtimeTransitionKind Kind,
    string? ChapterId = null,
    string? EventId = null,
    string? AssetId = null,
    ThermalAssetKind? AssetKind = null,
    RealtimeConstructionCompletion? Construction = null,
    RealtimeEventOutcome? EventOutcome = null)
{
    private RealtimeConstructionCompletion? _construction =
        RealtimeStructural.FreezeCompletion(Construction);

    public RealtimeConstructionCompletion? Construction
    {
        get => _construction;
        init => _construction = RealtimeStructural.FreezeCompletion(value);
    }
}

public enum RealtimeForecastStatus
{
    Upcoming,
    Active,
}

public sealed record RealtimeForecastEvent(
    int ChapterIndex,
    string ChapterId,
    string EventId,
    string DisplayName,
    long RevealMinute,
    long StartMinute,
    long EndMinute,
    RealtimeForecastStatus Status,
    CommercialOperatingPhaseDefinition OperatingProfile,
    ThermalIntervalEvaluation ProjectedEvaluation,
    RealtimeTemporalEventProjection TemporalProjection);

public sealed record RealtimeForecastThermalInterval(
    long StartMinute,
    long EndMinute,
    ThermalIntervalEvaluation Evaluation,
    IReadOnlyList<RealtimeThermalAssetSnapshot> Assets)
{
    private IReadOnlyList<RealtimeThermalAssetSnapshot> _assets =
        RealtimeStructural.Freeze(Assets);

    public IReadOnlyList<RealtimeThermalAssetSnapshot> Assets
    {
        get => _assets;
        init => _assets = RealtimeStructural.Freeze(value);
    }
}

public sealed record RealtimeTemporalEventProjection(
    IReadOnlyList<RealtimeForecastThermalInterval> Intervals,
    IReadOnlyList<RealtimeThermalTransition> Transitions,
    RealtimeEventOutcome Outcome)
{
    private IReadOnlyList<RealtimeForecastThermalInterval> _intervals =
        RealtimeStructural.Freeze(Intervals);
    private IReadOnlyList<RealtimeThermalTransition> _transitions =
        RealtimeStructural.Freeze(Transitions);

    public IReadOnlyList<RealtimeForecastThermalInterval> Intervals
    {
        get => _intervals;
        init => _intervals = RealtimeStructural.Freeze(value);
    }

    public IReadOnlyList<RealtimeThermalTransition> Transitions
    {
        get => _transitions;
        init => _transitions = RealtimeStructural.Freeze(value);
    }
}

public sealed record RealtimeForecastSnapshot(
    long NowMinute,
    long? ConstructionCompletionMinute,
    IReadOnlyList<RealtimeForecastEvent> Events,
    RealtimeConnectionRequirementAssessment? ConnectionRequirementAssessment = null)
{
    private IReadOnlyList<RealtimeForecastEvent> _events =
        RealtimeStructural.Freeze(Events);

    public IReadOnlyList<RealtimeForecastEvent> Events
    {
        get => _events;
        init => _events = RealtimeStructural.Freeze(value);
    }
}

public sealed record RealtimeComparisonDraftForecast(
    bool Available,
    ConstructionKind? DraftKind,
    RealtimeForecastSnapshot? Forecast);

public sealed record RealtimeCampaignSnapshot(
    long Minute,
    int ChapterIndex,
    RealtimeChapterDefinition Chapter,
    bool ChapterStarted,
    long ChapterStartMinute,
    int? ActiveEventIndex,
    RealtimeScheduledEventDefinition? ActiveEvent,
    long CashUnit,
    CommercialPromiseDecision PromiseDecision,
    ConstructionSnapshot Construction,
    RealtimeThermalSnapshot Thermal,
    RealtimeForecastSnapshot Forecast,
    IReadOnlyList<RealtimeChapterOutcome> CompletedChapters,
    IReadOnlyList<RealtimeEventOutcome> CurrentChapterEvents,
    RealtimeEventDutyProgress? ActiveDuty,
    IReadOnlyList<RealtimeActiveEventState> ActiveEventStates,
    IReadOnlyList<RealtimeTransition> PendingTransitions,
    bool CampaignComplete,
    int CommandCount,
    RealtimeStateAuthority Authority)
{
    private IReadOnlyList<RealtimeChapterOutcome> _completedChapters =
        RealtimeStructural.Freeze(CompletedChapters);
    private IReadOnlyList<RealtimeEventOutcome> _currentChapterEvents =
        RealtimeStructural.Freeze(CurrentChapterEvents);
    private IReadOnlyList<RealtimeActiveEventState> _activeEventStates =
        RealtimeStructural.Freeze(ActiveEventStates);
    private IReadOnlyList<RealtimeTransition> _pendingTransitions =
        RealtimeStructural.Freeze(PendingTransitions);

    public IReadOnlyList<RealtimeChapterOutcome> CompletedChapters
    {
        get => _completedChapters;
        init => _completedChapters = RealtimeStructural.Freeze(value);
    }

    public IReadOnlyList<RealtimeEventOutcome> CurrentChapterEvents
    {
        get => _currentChapterEvents;
        init => _currentChapterEvents = RealtimeStructural.Freeze(value);
    }

    public IReadOnlyList<RealtimeActiveEventState> ActiveEventStates
    {
        get => _activeEventStates;
        init => _activeEventStates = RealtimeStructural.Freeze(value);
    }

    public IReadOnlyList<RealtimeTransition> PendingTransitions
    {
        get => _pendingTransitions;
        init => _pendingTransitions = RealtimeStructural.Freeze(value);
    }
}

public sealed record RealtimeAdvanceResult(
    RealtimeCampaignSnapshot Snapshot,
    IReadOnlyList<RealtimeTransition> Transitions)
{
    private RealtimeCampaignSnapshot _snapshot = Snapshot;
    private IReadOnlyList<RealtimeTransition> _transitions =
        RealtimeStructural.Freeze(Transitions);
    private string _canonicalStateSha256 =
        RealtimeStateCanonicalizer.Sha256(Snapshot);

    public RealtimeCampaignSnapshot Snapshot
    {
        get => _snapshot;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _snapshot = value;
            _canonicalStateSha256 = RealtimeStateCanonicalizer.Sha256(value);
        }
    }

    public IReadOnlyList<RealtimeTransition> Transitions
    {
        get => _transitions;
        init => _transitions = RealtimeStructural.Freeze(value);
    }

    public string CanonicalStateSha256 => _canonicalStateSha256;
}

public sealed record RealtimeCommandResult(
    bool Accepted,
    RealtimeRunError? Error,
    ConstructionError? ConstructionError,
    RealtimeCampaignSnapshot Snapshot,
    IReadOnlyList<RealtimeTransition> Transitions)
{
    private RealtimeCampaignSnapshot _snapshot = Snapshot;
    private IReadOnlyList<RealtimeTransition> _transitions =
        RealtimeStructural.Freeze(Transitions);
    private string _canonicalStateSha256 =
        RealtimeStateCanonicalizer.Sha256(Snapshot);

    public RealtimeCampaignSnapshot Snapshot
    {
        get => _snapshot;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _snapshot = value;
            _canonicalStateSha256 = RealtimeStateCanonicalizer.Sha256(value);
        }
    }

    public IReadOnlyList<RealtimeTransition> Transitions
    {
        get => _transitions;
        init => _transitions = RealtimeStructural.Freeze(value);
    }

    public string CanonicalStateSha256 => _canonicalStateSha256;
}

public sealed record RealtimeProjectQuote(
    bool Accepted,
    RealtimeRunError? Error,
    ConstructionError? ConstructionError,
    long? CostCashUnit,
    long? BuildMinutes,
    long? CompletionMinute,
    IReadOnlyList<string> RiskAreaIds)
{
    private IReadOnlyList<string> _riskAreaIds =
        RealtimeStructural.Freeze(RiskAreaIds);

    public IReadOnlyList<string> RiskAreaIds
    {
        get => _riskAreaIds;
        init => _riskAreaIds = RealtimeStructural.Freeze(value);
    }
}
