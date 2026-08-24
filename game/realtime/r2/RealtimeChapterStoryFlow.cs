using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;

namespace Gridworks.Game.Realtime.R2;

internal enum RealtimeChapterStoryModalPurpose
{
    ChapterResult,
    ChapterBriefing,
    DecisionWindowStory,
    EventStory,
}

internal sealed record RealtimeChapterStoryModalRequest(
    string ModalId,
    RealtimeChapterStoryModalPurpose Purpose,
    string ChapterId,
    string? EventId,
    bool FinalResult,
    string? WindowId = null)
{
    internal RealtimePauseReason PauseReason => Purpose switch
    {
        RealtimeChapterStoryModalPurpose.ChapterResult =>
            RealtimePauseReason.CampaignResult,
        RealtimeChapterStoryModalPurpose.ChapterBriefing or
            RealtimeChapterStoryModalPurpose.DecisionWindowStory or
            RealtimeChapterStoryModalPurpose.EventStory =>
            RealtimePauseReason.ChapterBriefing,
        _ => throw new ArgumentOutOfRangeException(
            nameof(Purpose),
            Purpose,
            "Unsupported chapter-story modal purpose."),
    };
}

/// <summary>
/// Deterministic queue for cumulative native chapter story. Core transitions and
/// the selected composed campaign are the only authorities for modal timing and content.
/// </summary>
internal sealed class RealtimeChapterStoryFlow
{
    private readonly Queue<RealtimeChapterStoryModalRequest> _pending = new();
    private readonly HashSet<string> _observedModalIds = new(StringComparer.Ordinal);

    internal RealtimeChapterStoryModalRequest? Active { get; private set; }

    internal void Reset()
    {
        _pending.Clear();
        _observedModalIds.Clear();
        Active = null;
    }

    internal void Observe(
        RealtimeTransition transition,
        RealtimeCampaignSnapshot snapshot,
        RealtimeCampaignDefinition campaign)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(campaign);

        RealtimeChapterStoryModalRequest? request = transition.Kind switch
        {
            RealtimeTransitionKind.ChapterStarted when
                transition.ChapterId is not null &&
                snapshot.CompletedChapters.Count > 0 => Briefing(transition.ChapterId),
            RealtimeTransitionKind.ForecastRevealed when
                transition.ChapterId is not null &&
                transition.EventId is not null => DecisionWindowStory(
                    Chapter(campaign, transition.ChapterId),
                    transition.EventId),
            RealtimeTransitionKind.EventStarted when
                transition.ChapterId is not null &&
                transition.EventId is not null => EventStory(
                    Chapter(campaign, transition.ChapterId),
                    transition.EventId),
            RealtimeTransitionKind.ChapterCompleted when transition.ChapterId is not null =>
                Result(transition.ChapterId, snapshot.CampaignComplete),
            _ => null,
        };
        if (request is not null)
        {
            Enqueue(request);
        }
    }

    internal RealtimeChapterStoryModalRequest? ActivateNext()
    {
        if (Active is null &&
            _pending.TryDequeue(out RealtimeChapterStoryModalRequest? next))
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

    internal long? CalendarAdvanceTarget(
        RealtimeChapterStoryModalRequest? closedRequest,
        RealtimeCampaignSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (closedRequest is not
            {
                Purpose: RealtimeChapterStoryModalPurpose.ChapterResult,
                FinalResult: false,
            } || snapshot.ChapterStarted)
        {
            return null;
        }
        if (snapshot.CampaignComplete ||
            snapshot.CompletedChapters.Count == 0 ||
            snapshot.ChapterIndex != snapshot.CompletedChapters.Count ||
            !string.Equals(
                snapshot.CompletedChapters[^1].ChapterId,
                closedRequest.ChapterId,
                StringComparison.Ordinal) ||
            snapshot.Minute >= snapshot.ChapterStartMinute)
        {
            throw new InvalidOperationException(
                "The cumulative result lost its typed next-chapter calendar boundary.");
        }
        return snapshot.ChapterStartMinute;
    }

    internal static RealtimeChapterStoryModalRequest InitialBriefing(string chapterId) =>
        new(
            RealtimeR2Ids.ChapterBriefingModal,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            chapterId,
            null,
            false);

    private static RealtimeChapterStoryModalRequest Result(
        string chapterId,
        bool final) => new(
        RealtimeR2Ids.TutorialResultModal(chapterId),
        RealtimeChapterStoryModalPurpose.ChapterResult,
        chapterId,
        null,
        final);

    private static RealtimeChapterStoryModalRequest Briefing(string chapterId) => new(
        RealtimeR2Ids.TutorialBriefingModal(chapterId),
        RealtimeChapterStoryModalPurpose.ChapterBriefing,
        chapterId,
        null,
        false);

    private static RealtimeChapterStoryModalRequest? DecisionWindowStory(
        RealtimeChapterDefinition chapter,
        string eventId)
    {
        CommercialDecisionWindowDefinition? window = chapter.Content.DecisionWindows
            .SingleOrDefault(item => string.Equals(
                item.BeforePhaseId,
                eventId,
                StringComparison.Ordinal));
        if (window?.Story is null)
        {
            return null;
        }
        return new RealtimeChapterStoryModalRequest(
            RealtimeR2Ids.TutorialDecisionWindowModal(
                chapter.Content.ChapterId,
                window.WindowId),
            RealtimeChapterStoryModalPurpose.DecisionWindowStory,
            chapter.Content.ChapterId,
            null,
            false,
            window.WindowId);
    }

    private static RealtimeChapterStoryModalRequest? EventStory(
        RealtimeChapterDefinition chapter,
        string eventId)
    {
        CommercialOperatingPhaseDefinition phase = chapter.Content.OperatingPhases.Single(item =>
            string.Equals(item.PhaseId, eventId, StringComparison.Ordinal));
        return phase.Story is null
            ? null
            : new RealtimeChapterStoryModalRequest(
                RealtimeR2Ids.TutorialEventStoryModal(
                    chapter.Content.ChapterId,
                    eventId),
                RealtimeChapterStoryModalPurpose.EventStory,
                chapter.Content.ChapterId,
                eventId,
                false);
    }

    private static RealtimeChapterDefinition Chapter(
        RealtimeCampaignDefinition campaign,
        string chapterId) => campaign.Chapters.Single(item => string.Equals(
        item.Content.ChapterId,
        chapterId,
        StringComparison.Ordinal));

    private void Enqueue(RealtimeChapterStoryModalRequest request)
    {
        if (_observedModalIds.Add(request.ModalId))
        {
            _pending.Enqueue(request);
        }
    }
}
