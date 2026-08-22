using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

public static class RealtimeEventForecaster
{
    public static RealtimeTemporalEventProjection Project(
        RealtimeThermalSession boundarySession,
        CommercialWorldDefinition world,
        string chapterId,
        RealtimeScheduledEventDefinition scheduledEvent,
        ThermalIntervalRequest request,
        CommercialPromiseDecision promiseDecision,
        bool profileAlreadyActive = false,
        long? eventEndMinute = null,
        RealtimeEventDutyProgress? priorDuty = null)
    {
        ArgumentNullException.ThrowIfNull(boundarySession);
        ArgumentNullException.ThrowIfNull(world);
        if (string.IsNullOrWhiteSpace(chapterId))
        {
            throw new ArgumentException("Chapter ID is required.", nameof(chapterId));
        }
        ArgumentNullException.ThrowIfNull(scheduledEvent);
        ArgumentNullException.ThrowIfNull(request);
        RealtimeThermalSession thermal = boundarySession.Fork();
        long startMinute = thermal.Minute;
        long endMinute = eventEndMinute ??
            checked(startMinute + scheduledEvent.DurationMinutes);
        if (endMinute < startMinute)
        {
            throw new ArgumentOutOfRangeException(nameof(eventEndMinute));
        }
        var duty = new RealtimeEventDutyAccumulator(
            chapterId,
            scheduledEvent,
            promiseDecision,
            startMinute,
            priorDuty);
        var transitions = new List<RealtimeThermalTransition>();
        if (!profileAlreadyActive)
        {
            IReadOnlyList<RealtimeThermalTransition> profile =
                thermal.SetOperatingProfile(world, request);
            duty.Record(profile);
            transitions.AddRange(profile);
            IReadOnlyList<RealtimeThermalTransition> settled = thermal.SettleCurrentMinute();
            duty.Record(settled);
            transitions.AddRange(settled);
        }

        var intervals = new List<RealtimeForecastThermalInterval>();
        while (thermal.Minute < endMinute)
        {
            long next = endMinute;
            long? automatic = thermal.NextTransitionMinute();
            if (automatic.HasValue && automatic.Value > thermal.Minute &&
                automatic.Value < next)
            {
                next = automatic.Value;
            }
            RealtimeThermalSnapshot before = thermal.GetSnapshot();
            intervals.Add(new RealtimeForecastThermalInterval(
                thermal.Minute,
                next,
                before.Evaluation,
                before.Assets));
            thermal.AdvanceClockTo(next);
            duty.CloseSegment(next, before.Evaluation);
            if (next < endMinute)
            {
                IReadOnlyList<RealtimeThermalTransition> settled =
                    thermal.SettleCurrentMinute();
                duty.Record(settled);
                transitions.AddRange(settled);
            }
        }
        IReadOnlyList<RealtimeThermalTransition> terminal =
            thermal.SettleCurrentMinute();
        duty.Record(terminal);
        transitions.AddRange(terminal);
        RealtimeEventOutcome outcome = duty.Complete(
            endMinute,
            thermal.GetSnapshot().Evaluation);
        return new RealtimeTemporalEventProjection(intervals, transitions, outcome);
    }
}
