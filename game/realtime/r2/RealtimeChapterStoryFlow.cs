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
    private sealed record ProjectedStory(
        RealtimeChapterStoryModalRequest Request,
        long TriggerMinute);

    private readonly Queue<RealtimeChapterStoryModalRequest> _pending = new();
    private readonly HashSet<string> _observedModalIds = new(StringComparer.Ordinal);

    internal RealtimeChapterStoryModalRequest? Active { get; private set; }

    internal bool IsIdle => Active is null && _pending.Count == 0;

    internal bool HasPending => _pending.Count > 0;

    internal int ClosedStoryCount { get; private set; }

    internal void Reset()
    {
        _pending.Clear();
        _observedModalIds.Clear();
        Active = null;
        ClosedStoryCount = 0;
    }

    internal void Observe(
        RealtimeTransition transition,
        RealtimeCampaignDefinition campaign)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(campaign);

        ProjectedStory? projected = Project(transition, campaign);
        if (projected is not null)
        {
            Enqueue(projected.Request);
        }
    }

    internal void Restore(
        IReadOnlyList<RealtimeTransition> history,
        RealtimeCampaignDefinition campaign,
        int? closedStoryCount,
        long savedMinute)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(campaign);
        if (savedMinute < 0)
        {
            throw new InvalidOperationException(
                "A chapter-story cursor requires a nonnegative saved minute.");
        }

        Reset();
        var candidates = new List<ProjectedStory>();
        var candidateIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (RealtimeTransition transition in history)
        {
            ProjectedStory? projected = Project(transition, campaign);
            if (projected is not null && candidateIds.Add(projected.Request.ModalId))
            {
                candidates.Add(projected);
            }
        }

        foreach (ProjectedStory candidate in candidates)
        {
            _observedModalIds.Add(candidate.Request.ModalId);
        }

        int closed = closedStoryCount ?? candidates.Count;
        if (closed < 0 || closed > candidates.Count)
        {
            throw new InvalidOperationException(
                "The chapter-story cursor is outside the projected story history.");
        }
        ClosedStoryCount = closed;
        if (closed == candidates.Count)
        {
            return;
        }

        if (closed != candidates.Count - 1)
        {
            throw new InvalidOperationException(
                "The current chapter-story cursor cannot restore a queued story suffix.");
        }
        ProjectedStory active = candidates[closed];
        if (active.Request.Purpose is not (
                RealtimeChapterStoryModalPurpose.EventStory or
                RealtimeChapterStoryModalPurpose.DecisionWindowStory) ||
            active.TriggerMinute != savedMinute)
        {
            throw new InvalidOperationException(
                "The current chapter-story cursor must identify one active " +
                "in-chapter story at the saved minute.");
        }
        Active = active.Request;
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
        ClosedStoryCount = checked(ClosedStoryCount + 1);
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

    private static ProjectedStory? Project(
        RealtimeTransition transition,
        RealtimeCampaignDefinition campaign)
    {
        RealtimeChapterStoryModalRequest? request = transition.Kind switch
        {
            RealtimeTransitionKind.ChapterStarted when
                transition.ChapterId is not null &&
                ChapterIndex(campaign, transition.ChapterId) > 0 =>
                Briefing(transition.ChapterId),
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
                Result(
                    transition.ChapterId,
                    ChapterIndex(campaign, transition.ChapterId) ==
                        campaign.Chapters.Count - 1),
            _ => null,
        };
        return request is null
            ? null
            : new ProjectedStory(request, transition.Minute);
    }

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

    private static int ChapterIndex(
        RealtimeCampaignDefinition campaign,
        string chapterId)
    {
        for (int index = 0; index < campaign.Chapters.Count; index++)
        {
            if (string.Equals(
                    campaign.Chapters[index].Content.ChapterId,
                    chapterId,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }
        throw new InvalidOperationException(
            $"Chapter '{chapterId}' is absent from the selected realtime campaign.");
    }

    private void Enqueue(RealtimeChapterStoryModalRequest request)
    {
        if (_observedModalIds.Add(request.ModalId))
        {
            _pending.Enqueue(request);
        }
    }
}
