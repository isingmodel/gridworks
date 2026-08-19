using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

public sealed record RealtimeScheduledEventDefinition(
    string EventId,
    int Priority,
    int StartOffsetMinutes,
    int DurationMinutes,
    int ForecastLeadMinutes,
    CommercialOperatingPhaseDefinition OperatingProfile)
{
    public long EndOffsetMinutes => checked((long)StartOffsetMinutes + DurationMinutes);
}

internal static class RealtimeEventOrdering
{
    public static IOrderedEnumerable<RealtimeScheduledEventDefinition> BySchedule(
        IEnumerable<RealtimeScheduledEventDefinition> events) => events
        .OrderBy(item => item.StartOffsetMinutes)
        .ThenBy(item => item.Priority)
        .ThenBy(item => item.EventId, StringComparer.Ordinal);

    public static IOrderedEnumerable<RealtimeScheduledEventDefinition> ByPriority(
        IEnumerable<RealtimeScheduledEventDefinition> events) => events
        .OrderBy(item => item.Priority)
        .ThenBy(item => item.EventId, StringComparer.Ordinal);
}

public sealed record RealtimeChapterDefinition(
    CommercialCampaignChapterDefinition Content,
    int PreparationMinutes,
    int? PromiseDecisionDeadlineOffsetMinutes,
    IReadOnlyList<RealtimeScheduledEventDefinition> ScheduledEvents)
{
    private IReadOnlyList<RealtimeScheduledEventDefinition> _scheduledEvents =
        Freeze(ScheduledEvents);

    public IReadOnlyList<RealtimeScheduledEventDefinition> ScheduledEvents
    {
        get => _scheduledEvents;
        init => _scheduledEvents = Freeze(value);
    }

    public long EndOffsetMinutes => ScheduledEvents.Max(item => item.EndOffsetMinutes);

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record RealtimeCampaignDefinition(
    string SchemaVersion,
    string CampaignId,
    CommercialCampaignDefinition Content,
    CommercialCoreSeedDefinition InitialSeed,
    IReadOnlyList<RealtimeChapterDefinition> Chapters)
{
    private IReadOnlyList<RealtimeChapterDefinition> _chapters = Freeze(Chapters);

    public IReadOnlyList<RealtimeChapterDefinition> Chapters
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

public sealed class RealtimeCampaignValidationException : Exception
{
    public RealtimeCampaignValidationException(string message)
        : base(message)
    {
    }

    public RealtimeCampaignValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
