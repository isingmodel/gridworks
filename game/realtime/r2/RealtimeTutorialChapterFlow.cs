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
    DecisionWindowStory,
    EventStory,
}

internal sealed record RealtimeTutorialModalRequest(
    string ModalId,
    RealtimeTutorialModalPurpose Purpose,
    string ChapterId,
    string? EventId,
    bool FinalResult,
    string? WindowId = null)
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
    private const string DecisionWindowPrefix = "TUTORIAL_DECISION_WINDOW:";
    private const string EventPrefix = "TUTORIAL_EVENT_STORY:";
    private const string NorthBankChapterId = "NORTH_BANK_PROMISE";
    private const string NorthBankPlanningWindowId = "NORTH_BANK_PLANNING_WINDOW";

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
        if (transition.Kind == RealtimeTransitionKind.ChapterStarted &&
            transition.ChapterId is not null &&
            snapshot.CompletedChapters.Count > 0)
        {
            Enqueue(Briefing(transition.ChapterId));
            RealtimeTutorialModalRequest? planning = DecisionWindowStory(
                campaign,
                transition.ChapterId);
            if (planning is not null)
            {
                Enqueue(planning);
            }
            return;
        }

        RealtimeTutorialModalRequest? request = transition.Kind switch
        {
            RealtimeTransitionKind.ChapterCompleted when transition.ChapterId is not null =>
                Result(transition.ChapterId, snapshot.CampaignComplete),
            RealtimeTransitionKind.EventStarted when
                transition.ChapterId is not null &&
                transition.EventId is not null &&
                HasAuthoredStory(campaign, transition.ChapterId, transition.EventId) =>
                EventStory(transition.ChapterId, transition.EventId),
            _ => null,
        };
        if (request is not null)
        {
            Enqueue(request);
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

    private static RealtimeTutorialModalRequest? DecisionWindowStory(
        CommercialCampaignDefinition campaign,
        string chapterId)
    {
        if (!string.Equals(chapterId, NorthBankChapterId, StringComparison.Ordinal))
        {
            return null;
        }
        CommercialCampaignChapterDefinition chapter = campaign.Chapters.Single(item =>
            string.Equals(item.ChapterId, chapterId, StringComparison.Ordinal));
        if (chapter.CityPromise is null)
        {
            return null;
        }
        CommercialDecisionWindowDefinition window = chapter.DecisionWindows.Single(item =>
            string.Equals(
                item.WindowId,
                NorthBankPlanningWindowId,
                StringComparison.Ordinal) &&
            item.Story is not null);
        return new RealtimeTutorialModalRequest(
            $"{DecisionWindowPrefix}{chapterId}:{window.WindowId}",
            RealtimeTutorialModalPurpose.DecisionWindowStory,
            chapterId,
            null,
            false,
            window.WindowId);
    }

    private static RealtimeTutorialModalRequest EventStory(
        string chapterId,
        string eventId) => new(
        $"{EventPrefix}{chapterId}:{eventId}",
        RealtimeTutorialModalPurpose.EventStory,
        chapterId,
        eventId,
        false);

    private void Enqueue(RealtimeTutorialModalRequest request)
    {
        if (_observedModalIds.Add(request.ModalId))
        {
            _pending.Enqueue(request);
        }
    }

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
