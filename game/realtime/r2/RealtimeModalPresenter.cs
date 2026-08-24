using System;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;

namespace Gridworks.Game.Realtime.R2;

/// <summary>
/// Owns the complete modal projection, from the generic interaction modal through
/// route-specific authored story content.
/// </summary>
internal static class RealtimeModalPresenter
{
    internal static RealtimeModalPresentation? Present(
        RealtimePresentationSource source,
        RealtimePausePresentation pause)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(pause);
        RealtimeModalPresentation? modal = Modal(
            source.Snapshot,
            source.Interaction,
            pause);
        return AuthoredReleaseModal(source, modal);
    }

    private static RealtimeModalPresentation? Modal(
        RealtimeCampaignSnapshot snapshot,
        RealtimeInteractionState interaction,
        RealtimePausePresentation pause)
    {
        if (interaction.ActiveModalId is null || interaction.ActiveModalKind is null)
        {
            return null;
        }
        if (string.Equals(
                interaction.ActiveModalId,
                RealtimeR2Ids.CampaignResultModal,
                StringComparison.Ordinal))
        {
            RealtimeChapterOutcome? outcome = snapshot.CompletedChapters.LastOrDefault();
            int satisfied = outcome?.Events.Count(item => item.SafetySatisfied) ??
                snapshot.CurrentChapterEvents.Count(item => item.SafetySatisfied);
            int total = outcome?.Events.Count ?? snapshot.CurrentChapterEvents.Count;
            string resultBody = outcome is null
                ? $"운영 종료 시각 {RealtimePresentationText.Time(snapshot.Minute)} · 최종 운영 자금 " +
                  RealtimePresentationText.Cash(snapshot.CashUnit)
                : $"{snapshot.Chapter.Content.DisplayName} 운영 완료 · " +
                  $"안전 의무 {satisfied}/{total} 충족 · " +
                  $"최종 운영 자금 {RealtimePresentationText.Cash(outcome.EndingCashUnit)}";
            return new RealtimeModalPresentation(
                interaction.ActiveModalId,
                interaction.ActiveModalKind.Value,
                "운영 결과",
                snapshot.CampaignComplete ? "캠페인 운영 완료" : "장 운영 완료",
                resultBody,
                new RealtimeActionPresentation(
                    RealtimeR2Ids.ResultCloseAction,
                    "결과 확인",
                    "종료 상태를 유지하고 결과 창을 닫습니다.",
                    true),
                null,
                true,
                true)
            {
                Pause = pause,
            };
        }
        if (interaction.ActiveModalKind == RealtimeModalKind.ChapterStory)
        {
            return new RealtimeModalPresentation(
                interaction.ActiveModalId,
                interaction.ActiveModalKind.Value,
                "새 임무",
                snapshot.Chapter.Content.Briefing.Title,
                snapshot.Chapter.Content.Briefing.Body,
                new RealtimeActionPresentation(
                    RealtimeR2Ids.BriefingContinueAction,
                    "도시 운영 시작",
                    "임무 안내를 닫고 실시간 운영을 시작합니다.",
                    true),
                null,
                true,
                false)
            {
                Pause = pause,
            };
        }

        // R2 has no production command for destructive new-game, recovery, or
        // title navigation. Never label its implemented close operation as one
        // of those unsupported mutations; the only exposed action is an exact,
        // non-destructive return to the state captured by the modal reducer.
        return new RealtimeModalPresentation(
            interaction.ActiveModalId,
            interaction.ActiveModalKind.Value,
            "운영 안내",
            "현재 운영 화면에서 실행할 수 없는 작업입니다",
            "현재 기록과 운영 상태는 변경되지 않았습니다. " +
            "이 안내를 닫고 기존 운영 화면으로 돌아갑니다.",
            new RealtimeActionPresentation(
                RealtimeR2Ids.NoticeCloseAction,
                "안내 닫기",
                "아무 기록도 바꾸지 않고 기존 운영 화면으로 돌아갑니다.",
                true),
            null,
            true,
            true)
        {
            Pause = pause,
        };
    }

    private static RealtimeModalPresentation? AuthoredReleaseModal(
        RealtimePresentationSource source,
        RealtimeModalPresentation? modal)
    {
        if (modal is null)
        {
            return modal;
        }
        if (source.Data.NativeRoute?.UsesChapterStoryFlow == true)
        {
            RealtimeChapterStoryModalRequest? request = source.ActiveStoryRequest;
            return request is null || !string.Equals(
                    request.ModalId,
                    modal.Id,
                    StringComparison.Ordinal)
                ? modal
                : AuthoredChapterStoryModal(source, modal, request);
        }
        if (source.Data.NativeRoute?.IsStandaloneChapter != true)
        {
            return modal;
        }
        RealtimeChapterDefinition realtimeChapter = source.Data.Campaign.Chapters.Single();
        CommercialCampaignChapterDefinition chapter = realtimeChapter.Content;
        CommercialStoryCard? card = modal.Id switch
        {
            RealtimeR2Ids.ChapterBriefingModal => chapter.Briefing,
            RealtimeR2Ids.CampaignResultModal when
                source.SuccessfulStandaloneCompletion => chapter.ResultCards.Standard,
            _ => null,
        };
        return card is null
            ? modal
            : modal with
            {
                Eyebrow = card.Speaker,
                Heading = card.Title,
                Body = card.Body,
            };
    }

    private static RealtimeModalPresentation AuthoredChapterStoryModal(
        RealtimePresentationSource source,
        RealtimeModalPresentation modal,
        RealtimeChapterStoryModalRequest request)
    {
        RealtimeChapterDefinition realtimeChapter = source.Data.Campaign.Chapters.Single(item =>
            string.Equals(
                item.Content.ChapterId,
                request.ChapterId,
                StringComparison.Ordinal));
        CommercialCampaignChapterDefinition chapter = realtimeChapter.Content;
        if (request.Purpose == RealtimeChapterStoryModalPurpose.ChapterResult)
        {
            RealtimeCampaignSnapshot snapshot = source.Snapshot;
            RealtimeChapterOutcome outcome = snapshot.CompletedChapters
                .Single(item => string.Equals(
                    item.ChapterId,
                    request.ChapterId,
                    StringComparison.Ordinal));
            bool autoDefaulted = source.TransitionHistory.Any(item =>
                item.Kind == RealtimeTransitionKind.PromiseDefaulted &&
                string.Equals(
                    item.ChapterId,
                    request.ChapterId,
                    StringComparison.Ordinal));
            CommercialStoryCard? authored = outcome.ObjectiveSatisfied
                ? chapter.CityPromise is null
                    ? chapter.ResultCards.Standard
                    : outcome.PromiseDecision switch
                    {
                        CommercialPromiseDecision.Keep => chapter.ResultCards.Kept,
                        CommercialPromiseDecision.Defer => chapter.ResultCards.Deferred,
                        _ => null,
                    }
                : null;
            string requirement = outcome.ConnectionRequirementAssessment is null
                ? string.Empty
                : " · 접속 조건 " + string.Join(
                    ", ",
                    outcome.ConnectionRequirementAssessment.Facts.Select(item =>
                        $"{RealtimePresentationText.AssetDisplayName(
                            source.Data.BaseWorld,
                            snapshot,
                            item.NodeId)} " +
                        $"{item.CurrentConnections}/{item.RequiredConnections}"));
            int safeEvents = outcome.Events.Count(item => item.SafetySatisfied);
            int promisedEvents = outcome.Events.Count(item => item.PromiseSatisfied);
            long promiseUnservedMinutes = outcome.Events.Sum(item =>
                item.PromiseUnservedMinutes);
            string promiseFacts = chapter.CityPromise is null
                ? string.Empty
                : outcome.PromiseDecision == CommercialPromiseDecision.Defer
                    ? $" · 약속 {(autoDefaulted ? "자동 Defer" : "Defer")} · " +
                      $"{RealtimePresentationText.AssetDisplayName(
                          source.Data.BaseWorld,
                          snapshot,
                          chapter.CityPromise.LoadId)} 수요 의무 제외"
                    : $" · 약속 {outcome.PromiseDecision} " +
                      $"{promisedEvents}/{outcome.Events.Count} 충족" +
                      (promiseUnservedMinutes > 0
                          ? $" · {promiseUnservedMinutes}분 미공급"
                          : string.Empty);
            bool calendarTransition = source.StoryResultAdvancesCalendar;
            string authoredBody = authored?.Body ?? string.Empty;
            if (authored is not null && autoDefaulted)
            {
                authoredBody += "\n\n마감까지 선택하지 않아 입주 일정은 자동으로 연기됐습니다.";
            }
            return modal with
            {
                Eyebrow = authored?.Speaker ?? "계통운영 기록",
                Heading = authored?.Title ?? $"{chapter.DisplayName} 목표 미달",
                Body = authored is not null
                    ? authoredBody
                    : $"안전 의무 {safeEvents}/{outcome.Events.Count} 충족" +
                    promiseFacts +
                    requirement +
                    $" · 운영 자금 {outcome.EndingCashUnit:N0}만 원. " +
                    "충족하지 못한 사실과 첫 병목을 확인하세요.",
                PrimaryAction = new RealtimeActionPresentation(
                    RealtimeR2Ids.ResultCloseAction,
                    calendarTransition
                        ? "6개월 뒤 북안 검토로"
                        : request.FinalResult
                            ? chapter.CityPromise is not null
                                ? "북안 운영 결과 확인"
                                : "튜토리얼 결과 확인"
                            : "다음 장으로",
                    calendarTransition
                        ? $"결과를 닫고 실제 망·현금·공사를 보존한 채 " +
                          $"{RealtimePresentationText.Time(snapshot.ChapterStartMinute)}의 " +
                          "북안 검토로 이동합니다."
                        : request.FinalResult
                            ? $"누적 {source.Data.Campaign.Chapters.Count}장의 운영 결과를 확인합니다."
                            : "결과를 확인하고 다음 임무 안내로 이동합니다.",
                    true),
                DismissOnCancel = !calendarTransition,
            };
        }

        CommercialStoryCard card = request.Purpose switch
        {
            RealtimeChapterStoryModalPurpose.ChapterBriefing => chapter.Briefing,
            RealtimeChapterStoryModalPurpose.DecisionWindowStory =>
                chapter.DecisionWindows
                    .Single(item => string.Equals(
                        item.WindowId,
                        request.WindowId,
                        StringComparison.Ordinal))
                    .Story ?? throw new InvalidOperationException(
                        $"Tutorial window '{request.WindowId}' has no authored story."),
            RealtimeChapterStoryModalPurpose.EventStory => chapter.OperatingPhases
                .Single(item => string.Equals(
                    item.PhaseId,
                    request.EventId,
                    StringComparison.Ordinal))
                .Story ?? throw new InvalidOperationException(
                    $"Tutorial event '{request.EventId}' has no authored story."),
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };
        bool eventStory = request.Purpose == RealtimeChapterStoryModalPurpose.EventStory;
        bool decisionWindow = request.Purpose ==
            RealtimeChapterStoryModalPurpose.DecisionWindowStory;
        bool promiseBriefing = request.Purpose ==
            RealtimeChapterStoryModalPurpose.ChapterBriefing &&
            chapter.CityPromise is not null;
        return modal with
        {
            Eyebrow = card.Speaker,
            Heading = card.Title,
            Body = card.Body,
            PrimaryAction = new RealtimeActionPresentation(
                eventStory
                    ? RealtimeR2Ids.EventStoryContinueAction
                    : decisionWindow
                        ? RealtimeR2Ids.DecisionWindowContinueAction
                        : RealtimeR2Ids.BriefingContinueAction,
                eventStory
                    ? "시험 계속"
                    : decisionWindow
                        ? "약속 결정 화면 열기"
                        : promiseBriefing
                            ? "계획 원칙 보기"
                            : "도시 운영 시작",
                eventStory
                    ? "사건 설명을 닫고 정지 전의 실시간 속도로 돌아갑니다."
                    : decisionWindow
                        ? "계획 설명을 닫고 한 줄 마감 표식에서 Keep 또는 Defer를 선택합니다."
                        : promiseBriefing
                            ? "임무 안내 다음에 북안 서비스권역 계획 원칙을 확인합니다."
                            : "임무 안내를 닫고 실시간 운영을 시작합니다.",
                true),
            DismissOnCancel = false,
        };
    }
}
