using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;

namespace Gridworks.Game.Realtime.R2;

internal enum RealtimeTutorialModalPurpose
{
    ChapterResult,
    ChapterBriefing,
    EventStory,
}

internal sealed record RealtimeTutorialModalRequest(
    string ModalId,
    RealtimeTutorialModalPurpose Purpose,
    string ChapterId,
    string? EventId,
    bool FinalResult)
{
    internal RealtimePauseReason PauseReason => Purpose ==
        RealtimeTutorialModalPurpose.ChapterResult
            ? RealtimePauseReason.CampaignResult
            : RealtimePauseReason.ChapterBriefing;
}

/// <summary>
/// Deterministic queue for the cumulative tutorial's same-minute story flow.
/// Core transitions remain authoritative; this class only preserves their authored
/// result, next-briefing, and event-story presentation order while one modal is active.
/// </summary>
internal sealed class RealtimeTutorialChapterFlow
{
    private const string ResultPrefix = "TUTORIAL_RESULT:";
    private const string BriefingPrefix = "TUTORIAL_BRIEFING:";
    private const string EventPrefix = "TUTORIAL_EVENT_STORY:";

    private readonly Queue<RealtimeTutorialModalRequest> _pending = new();
    private readonly HashSet<string> _observedModalIds = new(StringComparer.Ordinal);

    internal RealtimeTutorialModalRequest? Active { get; private set; }

    internal void Reset()
    {
        _pending.Clear();
        _observedModalIds.Clear();
        Active = null;
    }

    internal void Observe(
        RealtimeTransition transition,
        RealtimeCampaignSnapshot snapshot,
        CommercialCampaignDefinition campaign)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(campaign);
        RealtimeTutorialModalRequest? request = transition.Kind switch
        {
            RealtimeTransitionKind.ChapterCompleted when transition.ChapterId is not null =>
                Result(transition.ChapterId, snapshot.CampaignComplete),
            RealtimeTransitionKind.ChapterStarted when
                transition.ChapterId is not null &&
                snapshot.CompletedChapters.Count > 0 =>
                Briefing(transition.ChapterId),
            RealtimeTransitionKind.EventStarted when
                transition.ChapterId is not null &&
                transition.EventId is not null &&
                HasAuthoredStory(campaign, transition.ChapterId, transition.EventId) =>
                EventStory(transition.ChapterId, transition.EventId),
            _ => null,
        };
        if (request is not null && _observedModalIds.Add(request.ModalId))
        {
            _pending.Enqueue(request);
        }
    }

    internal RealtimeTutorialModalRequest? ActivateNext()
    {
        if (Active is null && _pending.TryDequeue(out RealtimeTutorialModalRequest? next))
        {
            Active = next;
        }
        return Active;
    }

    internal bool Close(string modalId)
    {
        if (Active is null || !string.Equals(
                Active.ModalId,
                modalId,
                StringComparison.Ordinal))
        {
            return false;
        }
        Active = null;
        return true;
    }

    internal static RealtimeTutorialModalRequest InitialBriefing(string chapterId) =>
        new(
            "CHAPTER_BRIEFING",
            RealtimeTutorialModalPurpose.ChapterBriefing,
            chapterId,
            null,
            false);

    private static RealtimeTutorialModalRequest Result(
        string chapterId,
        bool final) => new(
        $"{ResultPrefix}{chapterId}",
        RealtimeTutorialModalPurpose.ChapterResult,
        chapterId,
        null,
        final);

    private static RealtimeTutorialModalRequest Briefing(string chapterId) => new(
        $"{BriefingPrefix}{chapterId}",
        RealtimeTutorialModalPurpose.ChapterBriefing,
        chapterId,
        null,
        false);

    private static RealtimeTutorialModalRequest EventStory(
        string chapterId,
        string eventId) => new(
        $"{EventPrefix}{chapterId}:{eventId}",
        RealtimeTutorialModalPurpose.EventStory,
        chapterId,
        eventId,
        false);

    private static bool HasAuthoredStory(
        CommercialCampaignDefinition campaign,
        string chapterId,
        string eventId) => campaign.Chapters
        .Single(item => string.Equals(
            item.ChapterId,
            chapterId,
            StringComparison.Ordinal))
        .OperatingPhases
        .Single(item => string.Equals(
            item.PhaseId,
            eventId,
            StringComparison.Ordinal))
        .Story is not null;
}
