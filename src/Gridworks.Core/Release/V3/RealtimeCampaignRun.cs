using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

/// <summary>
/// Deterministic, event-driven campaign clock. Time advances only through AdvanceTo;
/// commands are stamped at the current integer minute and never advance time themselves.
/// </summary>
public sealed class RealtimeCampaignRun
{
    public const int MaximumAcceptedCommands = 20_000;
    public const long DefaultForecastHorizonMinutes = 24 * 60;

    private readonly RealtimeCampaignDefinition _definition;
    private readonly RealtimeWorldDefinition _worldDefinition;
    private readonly RealtimeStateAuthority _stateAuthority;
    private readonly List<TimedRealtimeCommand> _commands = [];
    private readonly List<RealtimeChapterOutcome> _completedChapters = [];
    private readonly List<RealtimeEventOutcome> _currentEventOutcomes = [];
    private readonly List<RealtimeTransition> _pendingPublicTransitions = [];
    private readonly HashSet<string> _revealedEventKeys = new(StringComparer.Ordinal);
    private readonly HashSet<int> _activeEventIndexes = [];
    private readonly Dictionary<int, RealtimeEventDutyAccumulator> _activeDuties = [];
    private RealtimeConstructionSession _construction;
    private RealtimeThermalSession _thermal;
    private long _minute;
    private long _cashUnit;
    private int _chapterIndex;
    private long _chapterStartMinute;
    private bool _chapterStarted;
    private int _nextEventIndex;
    private CommercialPromiseDecision _promiseDecision = CommercialPromiseDecision.Unset;
    private bool _campaignComplete;

    public RealtimeCampaignRun(
        RealtimeCampaignDefinition definition,
        RealtimeWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(world);
        RealtimeCampaignLoader.Validate(definition, definition.Content, world);
        _definition = definition;
        _worldDefinition = world;
        _stateAuthority = RealtimeStateCanonicalizer.AuthorityFor(definition, world);

        CommercialCoreSeedDefinition seed = definition.InitialSeed;
        CommercialWorldDefinition initialWorld = CommercialCampaignLoader.BuildInitialWorld(
            world.Network,
            seed);
        _minute = seed.StartMinute;
        _cashUnit = seed.InitialCashUnit;
        _construction = new RealtimeConstructionSession(initialWorld.ToSpatialWorld(), _minute);
        _thermal = new RealtimeThermalSession(
            world,
            initialWorld,
            _minute,
            seed.CoolingAssetIds);
        _chapterStartMinute = checked(
            _minute + definition.Chapters[0].Content.TimeAdvanceBeforeChapterMinutes);

        var ignored = new List<RealtimeTransition>();
        ProcessCurrentMinute(ignored);
        _pendingPublicTransitions.AddRange(ignored);
    }

    public IReadOnlyList<TimedRealtimeCommand> AcceptedCommands =>
        Array.AsReadOnly(_commands.ToArray());

    public string GetCanonicalStateSha256() =>
        RealtimeStateCanonicalizer.Sha256(GetSnapshot());

    public RealtimeCampaignSnapshot GetSnapshot() => new(
        _minute,
        _chapterIndex,
        Chapter,
        _chapterStarted,
        _chapterStartMinute,
        PrimaryActiveEventIndex,
        ActiveEvent,
        _cashUnit,
        _promiseDecision,
        _construction.GetSnapshot(),
        _thermal.GetSnapshot(),
        GetForecast(),
        _completedChapters,
        _currentEventOutcomes,
        PrimaryActiveDuty?.GetProgress(),
        GetActiveEventStates(),
        _pendingPublicTransitions,
        _campaignComplete,
        _commands.Count,
        _stateAuthority);

    public RealtimeForecastSnapshot GetForecast(
        long horizonMinutes = DefaultForecastHorizonMinutes) =>
        GetForecastCore(horizonMinutes, null);

    public RealtimeComparisonDraftForecast GetComparisonDraftForecast(
        long horizonMinutes = DefaultForecastHorizonMinutes)
    {
        ConstructionSnapshot snapshot = _construction.GetSnapshot();
        ConstructionKind? kind = snapshot.NodeDraft is not null
            ? ConstructionKind.Node
            : snapshot.LineDraft?.EndNodeId is not null
                ? ConstructionKind.Line
                : null;
        RealtimeConstructionSession? virtualConstruction =
            _construction.ForkWithComparisonDraftCommissioned();
        return virtualConstruction is null
            ? new RealtimeComparisonDraftForecast(false, kind, null)
            : new RealtimeComparisonDraftForecast(
                true,
                kind,
                GetForecastCore(horizonMinutes, virtualConstruction));
    }

    private RealtimeForecastSnapshot GetForecastCore(
        long horizonMinutes,
        RealtimeConstructionSession? constructionOverride)
    {
        if (horizonMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(horizonMinutes));
        }
        long horizonEnd = horizonMinutes > long.MaxValue - _minute
            ? long.MaxValue
            : _minute + horizonMinutes;
        var items = new List<RealtimeForecastEvent>();
        foreach (TimelineEvent item in Timeline())
        {
            if (item.RevealMinute > _minute || item.EndMinute <= _minute ||
                item.StartMinute > horizonEnd)
            {
                continue;
            }
            RealtimeTemporalEventProjection temporal = ProjectForecast(
                item,
                constructionOverride);
            ThermalIntervalEvaluation projection = temporal.Intervals.Count > 0
                ? temporal.Intervals[0].Evaluation
                : temporal.Outcome.FinalEvaluation;
            items.Add(new RealtimeForecastEvent(
                item.ChapterIndex,
                item.ChapterId,
                item.Event.EventId,
                item.Event.OperatingProfile.DisplayName,
                item.RevealMinute,
                item.StartMinute,
                item.EndMinute,
                item.ChapterIndex == _chapterIndex &&
                ActiveEventById(item.Event.EventId)
                    ? RealtimeForecastStatus.Active
                    : RealtimeForecastStatus.Upcoming,
                item.Event.OperatingProfile,
                projection,
                temporal));
        }
        return new RealtimeForecastSnapshot(
            _minute,
            (constructionOverride ?? _construction).GetSnapshot()
                .ActiveConstruction?.CompletionMinute,
            items);
    }

    public RealtimeAdvanceResult AdvanceTo(long targetMinute)
    {
        if (targetMinute < _minute)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetMinute),
                "Realtime campaign time cannot move backward.");
        }

        var transitions = DrainPendingPublicTransitions();
        ProcessCurrentMinute(transitions);
        while (_minute < targetMinute)
        {
            long nextMinute = NextChangeMinute(targetMinute);
            AdvanceSubsystems(nextMinute, transitions);
            ProcessCurrentMinute(transitions);
        }
        return new RealtimeAdvanceResult(GetSnapshot(), transitions);
    }

    public RealtimeCommandResult ApplyCommand(
        long currentMinute,
        long sequence,
        RealtimeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (currentMinute != _minute)
        {
            return Rejected(RealtimeRunError.ClockMismatch);
        }
        if (sequence != _commands.Count + 1L)
        {
            return Rejected(RealtimeRunError.SequenceMismatch);
        }
        if (_campaignComplete || !_chapterStarted)
        {
            return Rejected(RealtimeRunError.WrongState);
        }
        if (_commands.Count >= MaximumAcceptedCommands)
        {
            return Rejected(RealtimeRunError.CommandLimit);
        }
        if (!ValidShape(command))
        {
            return Rejected(RealtimeRunError.InvalidCommandShape);
        }

        var transitions = new List<RealtimeTransition>();
        RealtimeCommandResult result = command.Kind switch
        {
            RealtimeCommandKind.SetNodeDraft => SetNodeDraft(command),
            RealtimeCommandKind.CancelNodeDraft => FromConstruction(
                _construction.CancelNodeDraft()),
            RealtimeCommandKind.OrderNode => OrderNode(),
            RealtimeCommandKind.StartLineDraft => StartLineDraft(command),
            RealtimeCommandKind.AddLinePoint => FromConstruction(
                _construction.AddLinePoint(command.Position!.Value)),
            RealtimeCommandKind.MoveLinePoint => FromConstruction(
                _construction.MoveLinePoint(
                    command.PointIndex!.Value,
                    command.Position!.Value)),
            RealtimeCommandKind.UndoLinePoint => FromConstruction(
                _construction.UndoLinePoint()),
            RealtimeCommandKind.FinishLineDraft => FromConstruction(
                _construction.FinishLineDraft(command.FirstId!)),
            RealtimeCommandKind.CancelLineDraft => FromConstruction(
                _construction.CancelLineDraft()),
            RealtimeCommandKind.OrderLine => OrderLine(),
            RealtimeCommandKind.SetPromiseDecision => SetPromiseDecision(command, transitions),
            _ => Rejected(RealtimeRunError.InvalidCommandShape),
        };
        if (!result.Accepted)
        {
            return result;
        }

        _commands.Add(new TimedRealtimeCommand(_commands.Count + 1L, _minute, command));
        transitions.InsertRange(0, DrainPendingPublicTransitions());
        ProcessCurrentMinute(transitions);
        return new RealtimeCommandResult(
            true,
            null,
            null,
            GetSnapshot(),
            transitions);
    }

    public RealtimeCommandResult ApplyCommand(RealtimeCommand command) =>
        ApplyCommand(_minute, _commands.Count + 1L, command);

    public RealtimeCommandResult Execute(RealtimeCommand command) => ApplyCommand(command);

    public NodePlacementPreview PreviewNodePlacement(string nodeClassId, MapPoint position)
    {
        if (!_chapterStarted || !Chapter.Content.AvailableNodeClassIds.Contains(
                nodeClassId,
                StringComparer.Ordinal))
        {
            return new NodePlacementPreview(
                false,
                ConstructionError.UnknownNodeClass,
                nodeClassId,
                position);
        }
        return _construction.PreviewNodePlacement(nodeClassId, position);
    }

    public RealtimeProjectQuote PreviewNodeOrder()
    {
        ConstructionSnapshot snapshot = _construction.GetSnapshot();
        if (snapshot.NodeDraft is not null && !Chapter.Content.AvailableNodeClassIds.Contains(
                snapshot.NodeDraft.NodeClassId,
                StringComparer.Ordinal))
        {
            return QuoteRejected(RealtimeRunError.ToolUnavailable);
        }
        return Quote(_construction.PreviewNodeOrder());
    }

    public LineStartPreview PreviewLineStart(
        string startNodeId,
        string lineClassId,
        string poleClassId)
    {
        if (!_chapterStarted || !LinePlanAvailable(lineClassId, poleClassId))
        {
            return new LineStartPreview(
                false,
                ConstructionError.UnknownLineClass,
                startNodeId,
                lineClassId,
                poleClassId);
        }
        return _construction.PreviewLineStart(startNodeId, lineClassId, poleClassId);
    }

    public LinePointPreview PreviewLinePoint(MapPoint position) =>
        _construction.PreviewLinePoint(position);

    public LinePointMovePreview PreviewMoveLinePoint(int pointIndex, MapPoint position) =>
        _construction.PreviewMoveLinePoint(pointIndex, position);

    public LineFinishPreview PreviewLineFinish(string endNodeId) =>
        _construction.PreviewLineFinish(endNodeId);

    public RealtimeProjectQuote PreviewLineOrder()
    {
        ConstructionSnapshot snapshot = _construction.GetSnapshot();
        if (snapshot.LineDraft is not null && !LinePlanAvailable(
                snapshot.LineDraft.LineClassId,
                snapshot.LineDraft.PoleClassId))
        {
            return QuoteRejected(RealtimeRunError.ToolUnavailable);
        }
        return Quote(_construction.PreviewLineOrder());
    }

    private RealtimeChapterDefinition Chapter => _definition.Chapters[_chapterIndex];

    private int? PrimaryActiveEventIndex => OrderedActiveEventIndexes().FirstOrDefault(-1) is
        int index and >= 0
            ? index
            : null;

    private RealtimeScheduledEventDefinition? ActiveEvent =>
        PrimaryActiveEventIndex is int index ? Chapter.ScheduledEvents[index] : null;

    private RealtimeEventDutyAccumulator? PrimaryActiveDuty =>
        PrimaryActiveEventIndex is int index ? _activeDuties[index] : null;

    private void ProcessCurrentMinute(List<RealtimeTransition> transitions)
    {
        bool changed;
        do
        {
            changed = false;
            if (!_campaignComplete && !_chapterStarted && _minute >= _chapterStartMinute)
            {
                StartChapter(transitions);
                changed = true;
            }
            if (_campaignComplete || !_chapterStarted)
            {
                continue;
            }

            if (RevealEvents(transitions))
            {
                changed = true;
            }

            long? promiseDeadline = PromiseDeadlineMinute();
            if (_promiseDecision == CommercialPromiseDecision.Unset &&
                promiseDeadline.HasValue && promiseDeadline.Value <= _minute)
            {
                _promiseDecision = CommercialPromiseDecision.Defer;
                transitions.Add(new RealtimeTransition(
                    _minute,
                    RealtimeTransitionKind.PromiseDefaulted,
                    Chapter.Content.ChapterId));
                changed = true;
            }

            // Close the previous [minute,next) duty before applying target-minute
            // availability. Commissioned construction is already present at this point.
            // Pending trips and recoveries must therefore be installed atomically before
            // any outgoing profile is evaluated or any authored event is removed/started.
            RefreshThermalAtBoundary(transitions);

            int[] endingIndexes = OrderedActiveEventIndexes()
                .Where(index => checked(
                    _chapterStartMinute +
                    Chapter.ScheduledEvents[index].EndOffsetMinutes) <= _minute)
                .ToArray();
            foreach (int endingIndex in endingIndexes)
            {
                CompleteEvent(endingIndex, transitions);
                changed = true;
            }

            var startingIndexes = new List<int>();
            while (_nextEventIndex < Chapter.ScheduledEvents.Count)
            {
                RealtimeScheduledEventDefinition next =
                    Chapter.ScheduledEvents[_nextEventIndex];
                if (checked(_chapterStartMinute + next.StartOffsetMinutes) > _minute)
                {
                    break;
                }
                startingIndexes.Add(_nextEventIndex++);
            }
            foreach (int startingIndex in startingIndexes
                         .OrderBy(index => Chapter.ScheduledEvents[index].Priority)
                         .ThenBy(index => Chapter.ScheduledEvents[index].EventId,
                             StringComparer.Ordinal))
            {
                StartEvent(startingIndex, transitions);
                changed = true;
            }

            if (endingIndexes.Length > 0 || startingIndexes.Count > 0)
            {
                ApplyActiveOperatingProfile(transitions);
            }

            if (_activeEventIndexes.Count == 0 &&
                _nextEventIndex == Chapter.ScheduledEvents.Count &&
                checked(_chapterStartMinute + Chapter.EndOffsetMinutes) <= _minute)
            {
                CompleteChapter(transitions);
                changed = true;
            }
        } while (changed);
    }

    private void StartChapter(List<RealtimeTransition> transitions)
    {
        _chapterStarted = true;
        _nextEventIndex = 0;
        _activeEventIndexes.Clear();
        _activeDuties.Clear();
        _promiseDecision = CommercialPromiseDecision.Unset;
        _currentEventOutcomes.Clear();
        _cashUnit = checked(_cashUnit + Chapter.Content.BudgetGrantCashUnit);
        if (Chapter.Content.ResetThermalStateBeforeChapter)
        {
            _thermal = new RealtimeThermalSession(
                _worldDefinition,
                CurrentCommercialWorld(),
                _minute);
        }
        transitions.Add(new RealtimeTransition(
            _minute,
            RealtimeTransitionKind.ChapterStarted,
            Chapter.Content.ChapterId));
    }

    private bool RevealEvents(List<RealtimeTransition> transitions)
    {
        bool changed = false;
        var due = new List<RealtimeScheduledEventDefinition>();
        for (int index = 0; index < Chapter.ScheduledEvents.Count; index++)
        {
            RealtimeScheduledEventDefinition item = Chapter.ScheduledEvents[index];
            long revealMinute = checked(
                _chapterStartMinute + item.StartOffsetMinutes - item.ForecastLeadMinutes);
            string key = EventKey(_chapterIndex, item.EventId);
            if (revealMinute <= _minute && _revealedEventKeys.Add(key))
            {
                due.Add(item);
                changed = true;
            }
        }
        foreach (RealtimeScheduledEventDefinition item in
                 RealtimeEventOrdering.ByPriority(due))
        {
            transitions.Add(new RealtimeTransition(
                _minute,
                RealtimeTransitionKind.ForecastRevealed,
                Chapter.Content.ChapterId,
                item.EventId));
        }
        return changed;
    }

    private void StartEvent(int eventIndex, List<RealtimeTransition> transitions)
    {
        RealtimeScheduledEventDefinition item = Chapter.ScheduledEvents[eventIndex];
        if (!_activeEventIndexes.Add(eventIndex))
        {
            throw new InvalidOperationException($"Event '{item.EventId}' started twice.");
        }
        _activeDuties.Add(eventIndex, new RealtimeEventDutyAccumulator(
            Chapter.Content.ChapterId,
            item,
            _promiseDecision,
            _minute));
        transitions.Add(new RealtimeTransition(
            _minute,
            RealtimeTransitionKind.EventStarted,
            Chapter.Content.ChapterId,
            item.EventId));
    }

    private void CompleteEvent(
        int eventIndex,
        List<RealtimeTransition> transitions)
    {
        RealtimeScheduledEventDefinition item = Chapter.ScheduledEvents[eventIndex];
        ThermalIntervalEvaluation evaluation = _thermal.GetSnapshot().Evaluation;
        RealtimeEventOutcome outcome = (_activeDuties.TryGetValue(
                eventIndex,
                out RealtimeEventDutyAccumulator? duty)
            ? duty
            : throw new InvalidOperationException("Active event has no duty accumulator."))
            .Complete(_minute, evaluation);
        _currentEventOutcomes.Add(outcome);
        _activeEventIndexes.Remove(eventIndex);
        _activeDuties.Remove(eventIndex);
        transitions.Add(new RealtimeTransition(
            _minute,
            RealtimeTransitionKind.EventCompleted,
            Chapter.Content.ChapterId,
            item.EventId,
            EventOutcome: outcome));
    }

    private void CompleteChapter(List<RealtimeTransition> transitions)
    {
        string chapterId = Chapter.Content.ChapterId;
        var outcome = new RealtimeChapterOutcome(
            chapterId,
            _chapterStartMinute,
            _minute,
            _promiseDecision,
            _currentEventOutcomes,
            _cashUnit);
        _completedChapters.Add(outcome);
        transitions.Add(new RealtimeTransition(
            _minute,
            RealtimeTransitionKind.ChapterCompleted,
            chapterId));
        _chapterStarted = false;
        if (_chapterIndex + 1 >= _definition.Chapters.Count)
        {
            _campaignComplete = true;
            transitions.Add(new RealtimeTransition(
                _minute,
                RealtimeTransitionKind.CampaignCompleted,
                chapterId));
            return;
        }
        _chapterIndex++;
        _chapterStartMinute = checked(
            _minute + Chapter.Content.TimeAdvanceBeforeChapterMinutes);
    }

    private long NextChangeMinute(long targetMinute)
    {
        long next = targetMinute;
        Consider(ref next, _construction.GetSnapshot().ActiveConstruction?.CompletionMinute);
        Consider(ref next, _thermal.NextTransitionMinute());
        if (_campaignComplete)
        {
            return next;
        }
        if (!_chapterStarted)
        {
            Consider(ref next, _chapterStartMinute);
            return next;
        }
        Consider(ref next, PromiseDeadlineMinute());
        foreach (int activeIndex in _activeEventIndexes)
        {
            Consider(
                ref next,
                checked(_chapterStartMinute +
                    Chapter.ScheduledEvents[activeIndex].EndOffsetMinutes));
        }
        if (_nextEventIndex < Chapter.ScheduledEvents.Count)
        {
            Consider(
                ref next,
                checked(_chapterStartMinute +
                    Chapter.ScheduledEvents[_nextEventIndex].StartOffsetMinutes));
        }
        foreach (RealtimeScheduledEventDefinition item in Chapter.ScheduledEvents)
        {
            if (_revealedEventKeys.Contains(EventKey(_chapterIndex, item.EventId)))
            {
                continue;
            }
            Consider(
                ref next,
                checked(_chapterStartMinute + item.StartOffsetMinutes -
                    item.ForecastLeadMinutes));
        }
        return next;
    }

    private void AdvanceSubsystems(
        long nextMinute,
        List<RealtimeTransition> transitions)
    {
        _thermal.AdvanceClockTo(nextMinute);
        CloseActiveDutySegment(nextMinute);
        RealtimeConstructionAdvanceResult construction = _construction.AdvanceTo(nextMinute);
        _minute = nextMinute;
        if (construction.Completion is not null)
        {
            transitions.Add(new RealtimeTransition(
                construction.Completion.CompletionMinute,
                RealtimeTransitionKind.ConstructionCompleted,
                !_campaignComplete ? Chapter.Content.ChapterId : null,
                Construction: construction.Completion));
        }
    }

    private void CloseActiveDutySegment(long endMinute)
    {
        ThermalIntervalEvaluation evaluation = _thermal.GetSnapshot().Evaluation;
        foreach (RealtimeEventDutyAccumulator duty in _activeDuties.Values)
        {
            duty.CloseSegment(endMinute, evaluation);
        }
    }

    private RealtimeCommandResult SetNodeDraft(RealtimeCommand command)
    {
        if (!Chapter.Content.AvailableNodeClassIds.Contains(
                command.FirstId!,
                StringComparer.Ordinal))
        {
            return Rejected(RealtimeRunError.ToolUnavailable);
        }
        return FromConstruction(_construction.SetNodeDraft(
            command.FirstId!,
            command.Position!.Value));
    }

    private RealtimeCommandResult StartLineDraft(RealtimeCommand command)
    {
        if (!LinePlanAvailable(command.SecondId!, command.ThirdId!))
        {
            return Rejected(RealtimeRunError.ToolUnavailable);
        }
        return FromConstruction(_construction.StartLineDraft(
            command.FirstId!,
            command.SecondId!,
            command.ThirdId!));
    }

    private RealtimeCommandResult OrderNode()
    {
        RealtimeProjectQuote quote = PreviewNodeOrder();
        if (!quote.Accepted)
        {
            return Rejected(quote.Error!.Value, quote.ConstructionError);
        }
        if (quote.CostCashUnit!.Value > _cashUnit)
        {
            return Rejected(RealtimeRunError.InsufficientCash);
        }
        ConstructionCommandResult order = _construction.OrderNode();
        if (!order.Accepted)
        {
            return FromConstruction(order);
        }
        _cashUnit = checked(_cashUnit - quote.CostCashUnit.Value);
        return AcceptedWithoutTransitions();
    }

    private RealtimeCommandResult OrderLine()
    {
        RealtimeProjectQuote quote = PreviewLineOrder();
        if (!quote.Accepted)
        {
            return Rejected(quote.Error!.Value, quote.ConstructionError);
        }
        if (quote.CostCashUnit!.Value > _cashUnit)
        {
            return Rejected(RealtimeRunError.InsufficientCash);
        }
        ConstructionCommandResult order = _construction.OrderLine();
        if (!order.Accepted)
        {
            return FromConstruction(order);
        }
        _cashUnit = checked(_cashUnit - quote.CostCashUnit.Value);
        return AcceptedWithoutTransitions();
    }

    private RealtimeCommandResult SetPromiseDecision(
        RealtimeCommand command,
        List<RealtimeTransition> transitions)
    {
        if (Chapter.Content.CityPromise is null)
        {
            return Rejected(RealtimeRunError.PromiseUnavailable);
        }
        long deadline = PromiseDeadlineMinute()!.Value;
        if (_minute >= deadline)
        {
            return Rejected(RealtimeRunError.PromiseDeadlinePassed);
        }
        _promiseDecision = command.PromiseDecision!.Value;
        if (_activeEventIndexes.Count > 0)
        {
            ApplyActiveOperatingProfile(transitions);
        }
        return AcceptedWithoutTransitions();
    }

    private RealtimeCommandResult FromConstruction(ConstructionCommandResult result) =>
        result.Accepted
            ? AcceptedWithoutTransitions()
            : Rejected(RealtimeRunError.ConstructionRejected, result.Error);

    private RealtimeCommandResult AcceptedWithoutTransitions() => new(
        true,
        null,
        null,
        GetSnapshot(),
        Array.Empty<RealtimeTransition>());

    private RealtimeCommandResult Rejected(
        RealtimeRunError error,
        ConstructionError? constructionError = null) => new(
        false,
        error,
        constructionError,
        GetSnapshot(),
        Array.Empty<RealtimeTransition>());

    private RealtimeProjectQuote Quote(ConstructionQuote quote)
    {
        if (!quote.Accepted)
        {
            return new RealtimeProjectQuote(
                false,
                RealtimeRunError.ConstructionRejected,
                quote.Error,
                null,
                null,
                null,
                Array.Empty<string>());
        }
        return new RealtimeProjectQuote(
            true,
            null,
            null,
            quote.CostCashUnit,
            quote.BuildMinutes,
            quote.CompletionMinute,
            quote.RiskAreaIds);
    }

    private static RealtimeProjectQuote QuoteRejected(RealtimeRunError error) => new(
        false,
        error,
        null,
        null,
        null,
        null,
        Array.Empty<string>());

    private List<RealtimeTransition> DrainPendingPublicTransitions()
    {
        var result = new List<RealtimeTransition>(_pendingPublicTransitions);
        _pendingPublicTransitions.Clear();
        return result;
    }

    private bool LinePlanAvailable(string lineClassId, string poleClassId) =>
        Chapter.Content.AvailableLinePlans.Any(item =>
            string.Equals(item.LineClassId, lineClassId, StringComparison.Ordinal) &&
            string.Equals(item.PoleClassId, poleClassId, StringComparison.Ordinal));

    private long? PromiseDeadlineMinute() =>
        Chapter.PromiseDecisionDeadlineOffsetMinutes is int offset &&
        Chapter.Content.CityPromise is not null
            ? checked(_chapterStartMinute + offset)
            : null;

    private CommercialWorldDefinition CurrentCommercialWorld()
        => CommercialWorldFor(_construction);

    private CommercialWorldDefinition CommercialWorldFor(
        RealtimeConstructionSession construction)
    {
        SpatialWorldDefinition spatial = construction.GetSnapshot().World;
        return _worldDefinition.Network with
        {
            InitialCashUnit = _cashUnit,
            Nodes = spatial.Nodes.Where(item => item.Commissioned).ToArray(),
            Edges = spatial.Edges.Where(item => item.Commissioned).ToArray(),
        };
    }

    private IEnumerable<int> OrderedActiveEventIndexes() => _activeEventIndexes
        .OrderBy(index => Chapter.ScheduledEvents[index].Priority)
        .ThenBy(index => Chapter.ScheduledEvents[index].EventId, StringComparer.Ordinal);

    private bool ActiveEventById(string eventId) => _activeEventIndexes.Any(index =>
        string.Equals(
            Chapter.ScheduledEvents[index].EventId,
            eventId,
            StringComparison.Ordinal));

    private IReadOnlyList<RealtimeScheduledEventDefinition> ActiveEvents() =>
        EventsForIndexes(Chapter, _activeEventIndexes);

    private IReadOnlyList<RealtimeActiveEventState> GetActiveEventStates() =>
        Array.AsReadOnly(OrderedActiveEventIndexes()
            .Select(index => new RealtimeActiveEventState(
                index,
                Chapter.ScheduledEvents[index].EventId,
                Chapter.ScheduledEvents[index],
                _activeDuties[index].GetProgress()))
            .ToArray());

    private static IReadOnlyList<RealtimeScheduledEventDefinition> EventsForIndexes(
        RealtimeChapterDefinition chapter,
        IEnumerable<int> indexes) => Array.AsReadOnly(indexes
        .Select(index => chapter.ScheduledEvents[index])
        .OrderBy(item => item.Priority)
        .ThenBy(item => item.EventId, StringComparer.Ordinal)
        .ToArray());

    private void RefreshThermalAtBoundary(List<RealtimeTransition> transitions)
    {
        CommercialWorldDefinition world = CurrentCommercialWorld();
        IReadOnlyList<RealtimeScheduledEventDefinition> active = ActiveEvents();
        ThermalIntervalRequest request = active.Count == 0
            ? IdleIntervalRequest("REALTIME_IDLE")
            : BuildIntervalRequest(active, world, _promiseDecision);
        AddThermalTransitions(
            _thermal.SettleCurrentMinute(world, request),
            transitions);
    }

    private void ApplyActiveOperatingProfile(List<RealtimeTransition> transitions)
    {
        CommercialWorldDefinition world = CurrentCommercialWorld();
        IReadOnlyList<RealtimeScheduledEventDefinition> active = ActiveEvents();
        IReadOnlyList<RealtimeThermalTransition> thermalTransitions = active.Count == 0
            ? _thermal.SetIdle(world, "REALTIME_IDLE")
            : _thermal.SetOperatingProfile(
                world,
                BuildIntervalRequest(active, world, _promiseDecision));
        AddThermalTransitions(thermalTransitions, transitions);
    }

    private static ThermalIntervalRequest IdleIntervalRequest(string intervalId) => new(
        intervalId,
        Array.Empty<ThermalLoadRequest>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<ThermalLimitOverride>());

    private static ThermalIntervalRequest BuildIntervalRequest(
        IReadOnlyList<RealtimeScheduledEventDefinition> activeEvents,
        CommercialWorldDefinition world,
        CommercialPromiseDecision promiseDecision)
    {
        ArgumentNullException.ThrowIfNull(activeEvents);
        if (activeEvents.Count == 0)
        {
            throw new ArgumentException(
                "At least one active event is required for a composed profile.",
                nameof(activeEvents));
        }
        RealtimeScheduledEventDefinition[] ordered = RealtimeEventOrdering
            .ByPriority(activeEvents)
            .ToArray();
        if (ordered.Length == 1)
        {
            return BuildIntervalRequest(
                ordered[0].OperatingProfile,
                world,
                promiseDecision);
        }

        var candidates = new List<ComposedLoadCandidate>();
        foreach (RealtimeScheduledEventDefinition scheduled in ordered)
        {
            candidates.AddRange(RealtimeDispatchPlanner.BuildLoadPlan(
                    scheduled.OperatingProfile,
                    promiseDecision)
                .Select(item => new ComposedLoadCandidate(
                    scheduled.Priority,
                    scheduled.EventId,
                    item)));
        }
        ThermalLoadRequest[] loads = candidates
            .GroupBy(item => item.Plan.LoadId, StringComparer.Ordinal)
            .Select(group =>
            {
                ComposedLoadCandidate authority = group
                    .OrderBy(item => item.Plan.ObligationPriority)
                    .ThenBy(item => item.EventPriority)
                    .ThenBy(item => item.EventId, StringComparer.Ordinal)
                    .ThenBy(item => item.Plan.AuthoredDispatchPriority)
                    .First();
                return new ComposedLoad(
                    authority.Plan.ObligationPriority,
                    authority.EventPriority,
                    authority.EventId,
                    authority.Plan.AuthoredDispatchPriority,
                    group.Key,
                    group.Max(item => item.Plan.Request.DemandKw),
                    authority.Plan.Request.Permission);
            })
            .OrderBy(item => item.ObligationPriority)
            .ThenBy(item => item.EventPriority)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .ThenBy(item => item.AuthoredDispatchPriority)
            .ThenBy(item => item.LoadId, StringComparer.Ordinal)
            .Select(item => new ThermalLoadRequest(
                item.LoadId,
                item.DemandKw,
                item.Permission))
            .ToArray();

        var unavailableNodes = new SortedSet<string>(StringComparer.Ordinal);
        var unavailableEdges = new SortedSet<string>(StringComparer.Ordinal);
        foreach (RealtimeScheduledEventDefinition scheduled in ordered)
        {
            (IReadOnlyList<string> nodes, IReadOnlyList<string> edges) =
                UnavailableAssets(scheduled.OperatingProfile, world);
            unavailableNodes.UnionWith(nodes);
            unavailableEdges.UnionWith(edges);
        }
        ThermalLimitOverride[] overrides = ordered
            .SelectMany(item => item.OperatingProfile.ThermalLimitOverrides)
            .GroupBy(item => (item.AssetKind, item.ClassId))
            .OrderBy(group => group.Key.AssetKind)
            .ThenBy(group => group.Key.ClassId, StringComparer.Ordinal)
            .Select(group => new ThermalLimitOverride(
                group.Key.AssetKind,
                group.Key.ClassId,
                group.Min(item => item.ContinuousKw),
                group.Min(item => item.EmergencyKw)))
            .ToArray();
        string intervalId = "REALTIME_COMPOSITE:" + string.Concat(ordered.Select(item =>
            $"{item.EventId.Length}:{item.EventId};"));
        return new ThermalIntervalRequest(
            intervalId,
            loads,
            unavailableNodes.ToArray(),
            unavailableEdges.ToArray(),
            overrides);
    }

    private RealtimeTemporalEventProjection ProjectForecast(
        TimelineEvent target,
        RealtimeConstructionSession? constructionOverride = null)
    {
        if (target.ChapterIndex != _chapterIndex)
        {
            CommercialWorldDefinition world = CommercialWorldFor(
                constructionOverride ?? _construction);
            CommercialPromiseDecision futureDecision = CommercialPromiseDecision.Keep;
            var boundary = new RealtimeThermalSession(
                _worldDefinition,
                world,
                target.StartMinute);
            RealtimeChapterDefinition targetChapter =
                _definition.Chapters[target.ChapterIndex];
            long targetOffset = checked(target.StartMinute - TimelineChapterStart(
                target.ChapterIndex));
            RealtimeScheduledEventDefinition[] concurrent = RealtimeEventOrdering.ByPriority(
                    targetChapter.ScheduledEvents.Where(item =>
                        item.StartOffsetMinutes <= targetOffset &&
                        item.EndOffsetMinutes > targetOffset))
                .ToArray();
            return RealtimeEventForecaster.Project(
                boundary,
                world,
                target.ChapterId,
                target.Event,
                BuildIntervalRequest(concurrent, world, futureDecision),
                futureDecision);
        }

        RealtimeConstructionSession construction =
            (constructionOverride ?? _construction).Fork();
        RealtimeThermalSession thermal = _thermal.Fork();
        long minute = _minute;
        var activeEventIndexes = new HashSet<int>(_activeEventIndexes);
        int nextEventIndex = _nextEventIndex;
        CommercialPromiseDecision decision = _promiseDecision == CommercialPromiseDecision.Unset
            ? CommercialPromiseDecision.Keep
            : _promiseDecision;
        int targetEventIndex = Chapter.ScheduledEvents
            .Select((item, index) => (item, index))
            .Single(pair => string.Equals(
                pair.item.EventId,
                target.Event.EventId,
                StringComparison.Ordinal))
            .index;
        RealtimeEventDutyAccumulator? targetDuty = activeEventIndexes.Contains(targetEventIndex)
            ? new RealtimeEventDutyAccumulator(
                target.ChapterId,
                target.Event,
                decision,
                minute,
                _activeDuties[targetEventIndex].GetProgress())
            : null;
        var intervals = new List<RealtimeForecastThermalInterval>();
        var targetTransitions = new List<RealtimeThermalTransition>();
        RealtimeEventOutcome? targetOutcome = null;

        if (constructionOverride is not null)
        {
            CommercialWorldDefinition comparisonWorld = CommercialWorldFor(construction);
            IReadOnlyList<RealtimeThermalTransition> comparisonTransitions =
                activeEventIndexes.Count == 0
                    ? thermal.SetIdle(comparisonWorld, "FORECAST_IDLE")
                    : thermal.SetOperatingProfile(
                        comparisonWorld,
                        BuildIntervalRequest(
                            EventsForIndexes(Chapter, activeEventIndexes),
                            comparisonWorld,
                            decision));
            if (targetDuty is not null)
            {
                targetDuty.Record(comparisonTransitions);
                targetTransitions.AddRange(comparisonTransitions);
            }
        }

        while (minute < target.EndMinute && targetOutcome is null)
        {
            long next = target.EndMinute;
            long? constructionMinute = construction.GetSnapshot()
                .ActiveConstruction?.CompletionMinute;
            if (constructionMinute is > 0 && constructionMinute.Value > minute &&
                constructionMinute.Value < next)
            {
                next = constructionMinute.Value;
            }
            long? thermalMinute = thermal.NextTransitionMinute();
            if (thermalMinute.HasValue && thermalMinute.Value > minute &&
                thermalMinute.Value < next)
            {
                next = thermalMinute.Value;
            }
            foreach (int active in activeEventIndexes)
            {
                long eventEnd = checked(
                    _chapterStartMinute + Chapter.ScheduledEvents[active].EndOffsetMinutes);
                if (eventEnd > minute && eventEnd < next)
                {
                    next = eventEnd;
                }
            }
            if (nextEventIndex < Chapter.ScheduledEvents.Count)
            {
                long eventStart = checked(
                    _chapterStartMinute +
                    Chapter.ScheduledEvents[nextEventIndex].StartOffsetMinutes);
                if (eventStart > minute && eventStart < next)
                {
                    next = eventStart;
                }
            }

            RealtimeThermalSnapshot before = thermal.GetSnapshot();
            if (targetDuty is not null)
            {
                intervals.Add(new RealtimeForecastThermalInterval(
                    minute,
                    next,
                    before.Evaluation,
                    before.Assets));
            }
            thermal.AdvanceClockTo(next);
            targetDuty?.CloseSegment(next, before.Evaluation);
            RealtimeConstructionAdvanceResult constructionAdvance =
                construction.AdvanceTo(next);
            minute = next;

            CommercialWorldDefinition projectedWorld = CommercialWorldFor(construction);
            ThermalIntervalRequest outgoingRequest = activeEventIndexes.Count == 0
                ? IdleIntervalRequest("FORECAST_IDLE")
                : BuildIntervalRequest(
                    EventsForIndexes(Chapter, activeEventIndexes),
                    projectedWorld,
                    decision);
            IReadOnlyList<RealtimeThermalTransition> boundaryTransitions =
                thermal.SettleCurrentMinute(projectedWorld, outgoingRequest);
            if (targetDuty is not null)
            {
                targetDuty.Record(boundaryTransitions);
                targetTransitions.AddRange(boundaryTransitions);
            }

            int[] endingIndexes = activeEventIndexes
                .Where(index => checked(
                    _chapterStartMinute +
                    Chapter.ScheduledEvents[index].EndOffsetMinutes) <= minute)
                .OrderBy(index => Chapter.ScheduledEvents[index].Priority)
                .ThenBy(index => Chapter.ScheduledEvents[index].EventId,
                    StringComparer.Ordinal)
                .ToArray();
            if (endingIndexes.Contains(targetEventIndex))
            {
                targetOutcome = (targetDuty ?? throw new InvalidOperationException(
                        "Forecast target ended without an active duty accumulator."))
                    .Complete(minute, thermal.GetSnapshot().Evaluation);
            }
            foreach (int endingIndex in endingIndexes)
            {
                activeEventIndexes.Remove(endingIndex);
            }

            var startingIndexes = new List<int>();
            while (nextEventIndex < Chapter.ScheduledEvents.Count && checked(
                       _chapterStartMinute +
                       Chapter.ScheduledEvents[nextEventIndex].StartOffsetMinutes) <= minute)
            {
                startingIndexes.Add(nextEventIndex++);
            }
            foreach (int startingIndex in startingIndexes
                         .OrderBy(index => Chapter.ScheduledEvents[index].Priority)
                         .ThenBy(index => Chapter.ScheduledEvents[index].EventId,
                             StringComparer.Ordinal))
            {
                activeEventIndexes.Add(startingIndex);
                if (startingIndex == targetEventIndex)
                {
                    targetDuty = new RealtimeEventDutyAccumulator(
                        target.ChapterId,
                        target.Event,
                        decision,
                        minute);
                }
            }

            if (endingIndexes.Length > 0 || startingIndexes.Count > 0)
            {
                projectedWorld = CommercialWorldFor(construction);
                IReadOnlyList<RealtimeThermalTransition> profileTransitions =
                    activeEventIndexes.Count == 0
                        ? thermal.SetIdle(projectedWorld, "FORECAST_IDLE")
                        : thermal.SetOperatingProfile(
                            projectedWorld,
                            BuildIntervalRequest(
                                EventsForIndexes(Chapter, activeEventIndexes),
                                projectedWorld,
                                decision));
                if (targetDuty is not null && targetOutcome is null)
                {
                    targetDuty.Record(profileTransitions);
                    targetTransitions.AddRange(profileTransitions);
                }
            }
            _ = constructionAdvance;
        }

        if (targetOutcome is null)
        {
            throw new InvalidOperationException(
                $"Forecast target '{target.Event.EventId}' did not complete at its boundary.");
        }
        return new RealtimeTemporalEventProjection(
            intervals,
            targetTransitions,
            targetOutcome);
    }

    private static ThermalIntervalRequest BuildIntervalRequest(
        CommercialOperatingPhaseDefinition phase,
        CommercialWorldDefinition world,
        CommercialPromiseDecision promiseDecision)
    {
        ThermalLoadRequest[] loads = RealtimeDispatchPlanner.BuildLoadPlan(
                phase,
                promiseDecision)
            .Select(item => item.Request)
            .ToArray();
        (IReadOnlyList<string> unavailableNodes, IReadOnlyList<string> unavailableEdges) =
            UnavailableAssets(phase, world);
        return new ThermalIntervalRequest(
            phase.PhaseId,
            loads,
            unavailableNodes,
            unavailableEdges,
            phase.ThermalLimitOverrides);
    }

    private static (IReadOnlyList<string> Nodes, IReadOnlyList<string> Edges)
        UnavailableAssets(
            CommercialOperatingPhaseDefinition phase,
            CommercialWorldDefinition world)
    {
        HashSet<string> knownNodes = world.Nodes.Select(item => item.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> knownEdges = world.Edges.Select(item => item.EdgeId)
            .ToHashSet(StringComparer.Ordinal);
        var nodes = new SortedSet<string>(
            phase.UnavailableNodeIds.Where(knownNodes.Contains),
            StringComparer.Ordinal);
        var edges = new SortedSet<string>(
            phase.UnavailableEdgeIds.Where(knownEdges.Contains),
            StringComparer.Ordinal);
        if (phase.ActiveRiskAreaIds.Count == 0)
        {
            return (nodes.ToArray(), edges.ToArray());
        }

        Dictionary<string, SpatialRiskAreaDefinition> riskAreas = world.RiskAreas
            .ToDictionary(item => item.RiskAreaId, StringComparer.Ordinal);
        IReadOnlyList<SpatialRiskAreaDefinition> activeRiskAreas = phase.ActiveRiskAreaIds
            .Select(id => riskAreas[id])
            .ToArray();
        Dictionary<string, CommercialNodeClassDefinition> nodeClasses = world.NodeClasses
            .ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        Dictionary<string, SpatialNodeDefinition> worldNodes = world.Nodes
            .ToDictionary(item => item.NodeId, StringComparer.Ordinal);
        foreach (SpatialNodeDefinition node in world.Nodes)
        {
            int radius = nodeClasses[node.ClassId].FootprintRadiusUnit;
            if (activeRiskAreas.Any(area => FixedGeometry.CircleIntersectsPolygon(
                    node.Position,
                    radius,
                    area.Polygon)))
            {
                nodes.Add(node.NodeId);
            }
        }
        foreach (SpatialEdgeDefinition edge in world.Edges)
        {
            MapPoint from = worldNodes[edge.FromNodeId].Position;
            MapPoint to = worldNodes[edge.ToNodeId].Position;
            if (activeRiskAreas.Any(area => FixedGeometry.SegmentIntersectsPolygon(
                    from,
                    to,
                    area.Polygon)))
            {
                edges.Add(edge.EdgeId);
            }
        }
        return (nodes.ToArray(), edges.ToArray());
    }

    private void AddThermalTransitions(
        IReadOnlyList<RealtimeThermalTransition> thermalTransitions,
        List<RealtimeTransition> transitions)
    {
        foreach (RealtimeEventDutyAccumulator duty in _activeDuties.Values)
        {
            duty.Record(thermalTransitions);
        }
        foreach (RealtimeThermalTransition item in thermalTransitions)
        {
            transitions.Add(new RealtimeTransition(
                item.Minute,
                item.Kind switch
                {
                    RealtimeThermalTransitionKind.EmergencyEntered =>
                        RealtimeTransitionKind.ThermalEmergencyEntered,
                    RealtimeThermalTransitionKind.EmergencyCleared =>
                        RealtimeTransitionKind.ThermalEmergencyCleared,
                    RealtimeThermalTransitionKind.ProtectiveTrip =>
                        RealtimeTransitionKind.ThermalProtectiveTrip,
                    RealtimeThermalTransitionKind.Recovered =>
                        RealtimeTransitionKind.ThermalRecovered,
                    _ => throw new InvalidOperationException("Unknown thermal transition."),
                },
                !_campaignComplete ? Chapter.Content.ChapterId : null,
                ActiveEvent?.EventId,
                item.AssetId,
                item.AssetKind));
        }
    }

    private IReadOnlyList<TimelineEvent> Timeline()
    {
        var result = new List<TimelineEvent>();
        long chapterStart = _definition.InitialSeed.StartMinute;
        for (int chapterIndex = 0; chapterIndex < _definition.Chapters.Count; chapterIndex++)
        {
            RealtimeChapterDefinition chapter = _definition.Chapters[chapterIndex];
            chapterStart = checked(
                chapterStart + chapter.Content.TimeAdvanceBeforeChapterMinutes);
            foreach (RealtimeScheduledEventDefinition item in chapter.ScheduledEvents)
            {
                long start = checked(chapterStart + item.StartOffsetMinutes);
                result.Add(new TimelineEvent(
                    chapterIndex,
                    chapter.Content.ChapterId,
                    item,
                    checked(start - item.ForecastLeadMinutes),
                    start,
                    checked(chapterStart + item.EndOffsetMinutes)));
            }
            chapterStart = checked(chapterStart + chapter.EndOffsetMinutes);
        }
        return Array.AsReadOnly(result
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => item.Event.Priority)
            .ThenBy(item => item.Event.EventId, StringComparer.Ordinal)
            .ThenBy(item => item.ChapterIndex)
            .ToArray());
    }

    private long TimelineChapterStart(int targetChapterIndex)
    {
        long chapterStart = _definition.InitialSeed.StartMinute;
        for (int chapterIndex = 0; chapterIndex <= targetChapterIndex; chapterIndex++)
        {
            RealtimeChapterDefinition chapter = _definition.Chapters[chapterIndex];
            chapterStart = checked(
                chapterStart + chapter.Content.TimeAdvanceBeforeChapterMinutes);
            if (chapterIndex == targetChapterIndex)
            {
                return chapterStart;
            }
            chapterStart = checked(chapterStart + chapter.EndOffsetMinutes);
        }
        throw new ArgumentOutOfRangeException(nameof(targetChapterIndex));
    }

    private static string EventKey(int chapterIndex, string eventId) =>
        $"{chapterIndex}:{eventId}";

    private void Consider(ref long current, long? candidate)
    {
        if (candidate.HasValue && candidate.Value > _minute && candidate.Value < current)
        {
            current = candidate.Value;
        }
    }

    private static bool ValidShape(RealtimeCommand command)
    {
        bool NoIds() => command.FirstId is null && command.SecondId is null &&
            command.ThirdId is null;
        bool NoPoint() => command.Position is null && command.PointIndex is null;
        bool NoPromise() => command.PromiseDecision is null;
        bool Id(string? value) => !string.IsNullOrWhiteSpace(value);

        return command.Kind switch
        {
            RealtimeCommandKind.SetNodeDraft =>
                Id(command.FirstId) && command.SecondId is null && command.ThirdId is null &&
                command.Position.HasValue && command.PointIndex is null && NoPromise(),
            RealtimeCommandKind.CancelNodeDraft or RealtimeCommandKind.OrderNode or
                RealtimeCommandKind.UndoLinePoint or
                RealtimeCommandKind.CancelLineDraft or RealtimeCommandKind.OrderLine =>
                NoIds() && NoPoint() && NoPromise(),
            RealtimeCommandKind.StartLineDraft =>
                Id(command.FirstId) && Id(command.SecondId) && Id(command.ThirdId) &&
                NoPoint() && NoPromise(),
            RealtimeCommandKind.AddLinePoint =>
                NoIds() && command.Position.HasValue && command.PointIndex is null &&
                NoPromise(),
            RealtimeCommandKind.MoveLinePoint =>
                NoIds() && command.Position.HasValue && command.PointIndex.HasValue &&
                command.PointIndex.Value >= 0 && NoPromise(),
            RealtimeCommandKind.FinishLineDraft =>
                Id(command.FirstId) && command.SecondId is null && command.ThirdId is null &&
                NoPoint() && NoPromise(),
            RealtimeCommandKind.SetPromiseDecision =>
                NoIds() && NoPoint() && command.PromiseDecision is
                    CommercialPromiseDecision.Keep or CommercialPromiseDecision.Defer,
            _ => false,
        };
    }

    private sealed record TimelineEvent(
        int ChapterIndex,
        string ChapterId,
        RealtimeScheduledEventDefinition Event,
        long RevealMinute,
        long StartMinute,
        long EndMinute);

    private sealed record ComposedLoadCandidate(
        int EventPriority,
        string EventId,
        RealtimeDispatchLoadPlan Plan);

    private sealed record ComposedLoad(
        int ObligationPriority,
        int EventPriority,
        string EventId,
        int AuthoredDispatchPriority,
        string LoadId,
        long DemandKw,
        ThermalPermission Permission);
}
