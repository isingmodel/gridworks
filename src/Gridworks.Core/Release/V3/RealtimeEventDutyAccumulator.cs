using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

/// <summary>
/// Accumulates duty truth over every constant-dispatch segment of an active event.
/// A recovered final snapshot cannot erase earlier unserved time.
/// </summary>
public sealed class RealtimeEventDutyAccumulator
{
    private readonly string _chapterId;
    private readonly RealtimeScheduledEventDefinition _event;
    private readonly CommercialPromiseDecision _promiseDecision;
    private readonly List<RealtimeDutySegment> _segments = [];
    private readonly List<RealtimeEventIncident> _incidents = [];
    private long _segmentStartMinute;

    public RealtimeEventDutyAccumulator(
        string chapterId,
        RealtimeScheduledEventDefinition scheduledEvent,
        CommercialPromiseDecision promiseDecision,
        long startMinute,
        RealtimeEventDutyProgress? priorProgress = null)
    {
        if (string.IsNullOrWhiteSpace(chapterId))
        {
            throw new ArgumentException("Chapter ID is required.", nameof(chapterId));
        }
        ArgumentNullException.ThrowIfNull(scheduledEvent);
        if (startMinute < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startMinute));
        }
        _chapterId = chapterId;
        _event = scheduledEvent;
        _promiseDecision = promiseDecision;
        _segmentStartMinute = startMinute;
        if (priorProgress is not null)
        {
            if (!string.Equals(priorProgress.ChapterId, chapterId, StringComparison.Ordinal) ||
                !string.Equals(priorProgress.EventId, scheduledEvent.EventId,
                    StringComparison.Ordinal) ||
                priorProgress.SegmentStartMinute != startMinute)
            {
                throw new ArgumentException(
                    "Prior duty progress does not match the resumed event boundary.",
                    nameof(priorProgress));
            }
            _segments.AddRange(priorProgress.ClosedSegments);
            _incidents.AddRange(priorProgress.Incidents);
        }
    }

    public long SegmentStartMinute => _segmentStartMinute;

    public RealtimeEventDutyProgress GetProgress() => new(
        _chapterId,
        _event.EventId,
        _segmentStartMinute,
        _segments,
        _incidents);

    public void CloseSegment(long endMinute, ThermalIntervalEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        if (endMinute < _segmentStartMinute)
        {
            throw new ArgumentOutOfRangeException(nameof(endMinute));
        }
        if (endMinute == _segmentStartMinute)
        {
            return;
        }
        Dictionary<string, ThermalLoadSupply> supplies = evaluation.Loads.ToDictionary(
            item => item.LoadId,
            StringComparer.Ordinal);
        RealtimeDutyLoadFact[] loads = _event.OperatingProfile.Loads.Select(item =>
        {
            bool required = item.Obligation != CommercialObligationKind.CityPromise ||
                _promiseDecision == CommercialPromiseDecision.Keep;
            supplies.TryGetValue(item.LoadId, out ThermalLoadSupply? supply);
            long delivered = required
                ? Math.Min(item.DemandKw, supply?.DeliveredKw ?? 0)
                : 0;
            return new RealtimeDutyLoadFact(
                item.LoadId,
                item.Obligation,
                item.DemandKw,
                delivered,
                required,
                required && delivered < item.DemandKw ? supply?.Failure : null);
        }).ToArray();
        bool safety = loads.Where(item =>
                item.Required && item.Obligation == CommercialObligationKind.SafetyDuty)
            .All(item => item.DeliveredKw == item.DemandKw);
        bool promise = loads.Where(item =>
                item.Required && item.Obligation == CommercialObligationKind.CityPromise)
            .All(item => item.DeliveredKw == item.DemandKw);
        var segment = new RealtimeDutySegment(
            _segmentStartMinute,
            endMinute,
            loads,
            safety,
            promise);
        if (_segments.Count > 0)
        {
            RealtimeDutySegment previous = _segments[^1];
            if (previous.EndMinute == segment.StartMinute &&
                previous.SafetySatisfied == segment.SafetySatisfied &&
                previous.PromiseSatisfied == segment.PromiseSatisfied &&
                previous.Loads.SequenceEqual(segment.Loads))
            {
                _segments[^1] = previous with { EndMinute = segment.EndMinute };
            }
            else
            {
                _segments.Add(segment);
            }
        }
        else
        {
            _segments.Add(segment);
        }
        _segmentStartMinute = endMinute;
    }

    public void Record(IReadOnlyList<RealtimeThermalTransition> transitions)
    {
        ArgumentNullException.ThrowIfNull(transitions);
        _incidents.AddRange(transitions.Select(item => new RealtimeEventIncident(
            item.Minute,
            item.AssetId,
            item.AssetKind,
            item.Kind)));
    }

    public RealtimeEventOutcome Complete(
        long endMinute,
        ThermalIntervalEvaluation segmentEvaluation)
    {
        CloseSegment(endMinute, segmentEvaluation);
        long safetyMinutes = _segments.Where(item => !item.SafetySatisfied)
            .Sum(item => checked(item.EndMinute - item.StartMinute));
        long promiseMinutes = _segments.Where(item => !item.PromiseSatisfied)
            .Sum(item => checked(item.EndMinute - item.StartMinute));
        return new RealtimeEventOutcome(
            _chapterId,
            _event.EventId,
            checked(endMinute - _event.DurationMinutes),
            endMinute,
            segmentEvaluation,
            safetyMinutes == 0,
            promiseMinutes == 0,
            _segments,
            _incidents,
            _segments.FirstOrDefault(item => !item.SafetySatisfied)?.StartMinute,
            safetyMinutes,
            _segments.FirstOrDefault(item => !item.PromiseSatisfied)?.StartMinute,
            promiseMinutes);
    }
}
