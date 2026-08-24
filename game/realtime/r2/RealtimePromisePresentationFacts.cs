using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V3;

namespace Gridworks.Game.Realtime.R2;

internal static class RealtimePromisePresentationFacts
{
    internal static bool PromiseDefaulted(
        RealtimeCampaignSnapshot snapshot,
        IReadOnlyList<RealtimeTransition> transitionHistory) =>
        transitionHistory.Any(item =>
            item.Kind == RealtimeTransitionKind.PromiseDefaulted &&
            string.Equals(
                item.ChapterId,
                snapshot.Chapter.Content.ChapterId,
                StringComparison.Ordinal));

    internal static long RecordedPromiseUnservedMinutes(
        RealtimeCampaignSnapshot snapshot)
    {
        RealtimeEventOutcome[] current = snapshot.CurrentChapterEvents
            .Where(item => RealtimeTimelineTargetResolver.IsCurrentChapterOutcome(snapshot, item))
            .ToArray();
        if (current.Length > 0)
        {
            return current.Sum(item => item.PromiseUnservedMinutes);
        }
        return snapshot.CompletedChapters
            .LastOrDefault(item => string.Equals(
                item.ChapterId,
                snapshot.Chapter.Content.ChapterId,
                StringComparison.Ordinal))?
            .Events.Sum(item => item.PromiseUnservedMinutes) ?? 0;
    }

}

