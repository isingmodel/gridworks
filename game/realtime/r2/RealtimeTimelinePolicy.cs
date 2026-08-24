using System;
using System.Linq;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;

namespace Gridworks.Game.Realtime.R2;

/// <summary>
/// Owns the visible timeline window independently of its UI projection.
/// </summary>
internal static class RealtimeTimelinePolicy
{
    private const long DefaultHorizonMinutes =
        RealtimeCampaignRun.DefaultForecastHorizonMinutes;

    internal const long HistoryMinutes = 6 * 60;
    internal const int HistoryLimit = 3;

    internal static int ForecastPriority(
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastEvent forecast) =>
        forecast.ChapterIndex == snapshot.ChapterIndex
            ? snapshot.Chapter.ScheduledEvents.FirstOrDefault(item => string.Equals(
                    item.EventId,
                    forecast.EventId,
                    StringComparison.Ordinal))?.Priority ?? int.MaxValue
            : int.MaxValue;

    internal static long HorizonMinutes(RealtimeTimelineHorizonPreset preset) => preset switch
    {
        RealtimeTimelineHorizonPreset.SixHours => 6 * 60,
        RealtimeTimelineHorizonPreset.TwentyFourHours => DefaultHorizonMinutes,
        RealtimeTimelineHorizonPreset.SevenDays => 7 * 24 * 60,
        _ => throw new ArgumentOutOfRangeException(nameof(preset)),
    };

    internal static long RequiredForecastHorizonMinutes(
        long currentMinute,
        long? anchorMinute,
        RealtimeTimelineHorizonPreset preset)
    {
        if (currentMinute < 0 || anchorMinute is < 0)
        {
            throw new ArgumentOutOfRangeException(
                currentMinute < 0 ? nameof(currentMinute) : nameof(anchorMinute));
        }
        long anchor = anchorMinute ?? currentMinute;
        long displayHorizon = HorizonMinutes(preset);
        long horizonEnd = anchor > long.MaxValue - displayHorizon
            ? long.MaxValue
            : anchor + displayHorizon;
        long minutesFromNow = horizonEnd <= currentMinute
            ? 0
            : horizonEnd - currentMinute;
        // Preserve Core's existing 24-hour operational forecast for the HUD
        // while widening it whenever a fixed anchor or the seven-day preset
        // places the visible rail farther into the future. A null anchor keeps
        // following the authoritative current minute on every presentation.
        return Math.Max(DefaultHorizonMinutes, minutesFromNow);
    }

    internal static string HorizonLabel(RealtimeTimelineHorizonPreset preset) => preset switch
    {
        RealtimeTimelineHorizonPreset.SixHours => "앞으로 6시간",
        RealtimeTimelineHorizonPreset.TwentyFourHours => "앞으로 24시간",
        RealtimeTimelineHorizonPreset.SevenDays => "앞으로 7일",
        _ => throw new ArgumentOutOfRangeException(nameof(preset)),
    };
}
