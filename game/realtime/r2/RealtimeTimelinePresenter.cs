using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;

namespace Gridworks.Game.Realtime.R2;

internal static class RealtimeTimelinePresenter
{
    internal static RealtimeEventRailPresentation Present(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastSnapshot baseForecast,
        RealtimeComparisonDraftForecast comparisonDraftForecast,
        RealtimeInteractionState interaction,
        RealtimeProjectQuote? nodeOrderQuote,
        RealtimeProjectQuote? lineOrderQuote,
        IReadOnlyList<RealtimeTransition> transitionHistory)
    {
        var items = new List<RealtimeTimelineItemPresentation>();
        if (snapshot.Chapter.Content.CityPromise is CommercialCityPromiseDefinition promise &&
            snapshot.Chapter.PromiseDecisionDeadlineOffsetMinutes is int deadlineOffset)
        {
            long deadline = checked(snapshot.ChapterStartMinute + deadlineOffset);
            bool locked = snapshot.Minute >= deadline;
            bool defaulted = RealtimePromisePresentationFacts.PromiseDefaulted(snapshot, transitionHistory);
            long recordedPromiseUnservedMinutes =
                RealtimePromisePresentationFacts.RecordedPromiseUnservedMinutes(snapshot);
            bool promiseRisk = recordedPromiseUnservedMinutes > 0 ||
                baseForecast.Events.Any(item =>
                item.ChapterIndex == snapshot.ChapterIndex &&
                !item.TemporalProjection.Outcome.PromiseSatisfied);
            RealtimeTimelineSeverity promiseSeverity = defaulted
                ? RealtimeTimelineSeverity.Warning
                : promiseRisk && snapshot.PromiseDecision != CommercialPromiseDecision.Defer
                    ? RealtimeTimelineSeverity.Critical
                    : snapshot.PromiseDecision == CommercialPromiseDecision.Unset
                        ? RealtimeTimelineSeverity.Advisory
                        : RealtimeTimelineSeverity.Information;
            items.Add(new RealtimeTimelineItemPresentation(
                RealtimeR2Ids.PromiseDecisionMarker(promise.PromiseId),
                RealtimeTimelineItemKind.Decision,
                deadline,
                null,
                $"{promise.DisplayName} 선택 마감",
                "약속 마감",
                PromiseDecisionDescription(
                    promise,
                    snapshot.PromiseDecision,
                    deadline,
                    locked,
                    defaulted,
                    promiseRisk,
                    recordedPromiseUnservedMinutes),
                promiseSeverity,
                locked
                    ? RealtimeTimelineVisibility.Completed
                    : RealtimeTimelineVisibility.Announced,
                !locked && snapshot.PromiseDecision != CommercialPromiseDecision.Unset,
                !locked && !snapshot.CampaignComplete)
            {
                Lane = RealtimeTimelineLane.DemandAndDeadline,
                Priority = int.MinValue,
                // The full authored day/time remains in the description used by
                // hover and AX. Keep the closed one-line selector compact at 200%.
                TimeLabel = RealtimePresentationText.Clock(deadline),
                SeverityLabel = PromiseDecisionSeverityLabel(
                    snapshot.PromiseDecision,
                    locked,
                    defaulted,
                    promiseRisk,
                    recordedPromiseUnservedMinutes),
            });
        }
        int highestAuthoredPriority = snapshot.Chapter.ScheduledEvents.Count == 0
            ? 0
            : snapshot.Chapter.ScheduledEvents.Max(item => item.Priority);
        int thermalOrdinal = 0;
        foreach (RealtimeForecastEvent item in baseForecast.Events)
        {
            bool active = item.Status == RealtimeForecastStatus.Active;
            bool safetyRisk = !item.TemporalProjection.Outcome.SafetySatisfied;
            bool promiseRisk = !item.TemporalProjection.Outcome.PromiseSatisfied;
            RealtimeTimelineItemKind kind = EventKind(item.OperatingProfile);
            string eventName = RealtimePresentationText.PlayerEventName(displayWorld, item.OperatingProfile);
            items.Add(new RealtimeTimelineItemPresentation(
                item.EventId,
                kind,
                item.StartMinute,
                item.EndMinute,
                eventName,
                eventName,
                EventDescription(snapshot, item, eventName),
                safetyRisk || promiseRisk
                    ? RealtimeTimelineSeverity.Critical
                    : RealtimeTimelineSeverity.Advisory,
                active
                    ? RealtimeTimelineVisibility.Active
                    : RealtimeTimelineVisibility.Announced,
                active,
                true)
            {
                Lane = item.OperatingProfile.ActiveRiskAreaIds.Count > 0 ||
                       item.OperatingProfile.UnavailableEdgeIds.Count > 0 ||
                       item.OperatingProfile.UnavailableNodeIds.Count > 0
                    ? RealtimeTimelineLane.WeatherAndOutage
                    : RealtimeTimelineLane.DemandAndDeadline,
                Priority = RealtimeTimelinePolicy.ForecastPriority(snapshot, item),
                TimeLabel = RealtimePresentationText.Time(item.StartMinute),
                EndTimeLabel = RealtimePresentationText.Time(item.EndMinute),
                SeverityLabel = EventRiskLabel(
                    snapshot,
                    item.TemporalProjection.Outcome,
                    "예고"),
            });

            foreach (RealtimeThermalTransition transition in
                     item.TemporalProjection.Transitions)
            {
                string id = RealtimeR2Ids.ThermalMarker(item.EventId, transition);
                if (items.Any(existing => string.Equals(existing.Id, id,
                        StringComparison.Ordinal)))
                {
                    continue;
                }
                bool trip = transition.Kind == RealtimeThermalTransitionKind.ProtectiveTrip;
                int stableThermalOrder = thermalOrdinal++;
                items.Add(new RealtimeTimelineItemPresentation(
                    id,
                    RealtimeTimelineItemKind.ThermalProtection,
                    transition.Minute,
                    null,
                    RealtimePresentationText.ThermalTitle(displayWorld, snapshot, transition),
                    RealtimePresentationText.AssetDisplayName(displayWorld, snapshot, transition.AssetId),
                    $"{eventName} 예상 · {RealtimePresentationText.ThermalTitle(displayWorld, snapshot, transition)}",
                    trip
                        ? RealtimeTimelineSeverity.Critical
                        : RealtimeTimelineSeverity.Warning,
                    RealtimeTimelineVisibility.Announced,
                    transition.Minute == snapshot.Minute,
                    true)
                {
                    Lane = RealtimeTimelineLane.ThermalProtection,
                    Priority = highestAuthoredPriority <=
                        int.MaxValue - stableThermalOrder - 1
                            ? highestAuthoredPriority + stableThermalOrder + 1
                            : int.MaxValue,
                    TimeLabel = RealtimePresentationText.Time(transition.Minute),
                    SeverityLabel = trip ? "보호정지 예상" : "열 상태 변화",
                });
            }
        }

        // Core owns the virtual commissioning and every resulting supply/thermal
        // rule. The UI only maps that typed comparison snapshot into markers;
        // it never reconstructs or mutates a draft forecast locally.
        if (comparisonDraftForecast is { Available: true, Forecast: not null })
        {
            int comparisonThermalOrdinal = 0;
            foreach (RealtimeForecastEvent item in
                     comparisonDraftForecast.Forecast.Events)
            {
                bool active = item.Status == RealtimeForecastStatus.Active;
                bool safetyRisk = !item.TemporalProjection.Outcome.SafetySatisfied;
                bool promiseRisk = !item.TemporalProjection.Outcome.PromiseSatisfied;
                RealtimeTimelineItemKind kind = EventKind(item.OperatingProfile);
                string eventName = RealtimePresentationText.PlayerEventName(displayWorld, item.OperatingProfile);
                string markerId = RealtimeR2Ids.ComparisonEventMarker(item.EventId);
                items.Add(new RealtimeTimelineItemPresentation(
                    markerId,
                    kind,
                    item.StartMinute,
                    item.EndMinute,
                    $"현재 초안 기준 예상 · {eventName}",
                    "현재 초안 기준 예상",
                    $"현재 초안 기준 예상 · {EventDescription(snapshot, item, eventName)}",
                    safetyRisk || promiseRisk
                        ? RealtimeTimelineSeverity.Critical
                        : RealtimeTimelineSeverity.Advisory,
                    active
                        ? RealtimeTimelineVisibility.Active
                        : RealtimeTimelineVisibility.Announced,
                    active,
                    true)
                {
                    Lane = item.OperatingProfile.ActiveRiskAreaIds.Count > 0 ||
                           item.OperatingProfile.UnavailableEdgeIds.Count > 0 ||
                           item.OperatingProfile.UnavailableNodeIds.Count > 0
                        ? RealtimeTimelineLane.WeatherAndOutage
                        : RealtimeTimelineLane.DemandAndDeadline,
                    Priority = RealtimeTimelinePolicy.ForecastPriority(snapshot, item),
                    TimeLabel = RealtimePresentationText.Time(item.StartMinute),
                    EndTimeLabel = RealtimePresentationText.Time(item.EndMinute),
                    SeverityLabel = "현재 초안 기준 예상 · " + EventRiskLabel(
                        snapshot,
                        item.TemporalProjection.Outcome,
                        "예고"),
                    SourceKind = RealtimeTimelineSourceKind.Draft,
                });

                foreach (RealtimeThermalTransition transition in
                         item.TemporalProjection.Transitions)
                {
                    string id = RealtimeR2Ids.ComparisonThermalMarker(item.EventId, transition);
                    if (items.Any(existing => string.Equals(
                            existing.Id,
                            id,
                            StringComparison.Ordinal)))
                    {
                        continue;
                    }
                    bool trip = transition.Kind ==
                        RealtimeThermalTransitionKind.ProtectiveTrip;
                    int stableThermalOrder = comparisonThermalOrdinal++;
                    string thermalTitle = RealtimePresentationText.ThermalTitle(displayWorld, snapshot, transition);
                    items.Add(new RealtimeTimelineItemPresentation(
                        id,
                        RealtimeTimelineItemKind.ThermalProtection,
                        transition.Minute,
                        null,
                        $"현재 초안 기준 예상 · {thermalTitle}",
                        "현재 초안 기준 예상",
                        $"현재 초안 기준 예상 · {eventName} · {thermalTitle}",
                        trip
                            ? RealtimeTimelineSeverity.Critical
                            : RealtimeTimelineSeverity.Warning,
                        RealtimeTimelineVisibility.Announced,
                        transition.Minute == snapshot.Minute,
                        true)
                    {
                        Lane = RealtimeTimelineLane.ThermalProtection,
                        Priority = highestAuthoredPriority <=
                            int.MaxValue - stableThermalOrder - 1
                                ? highestAuthoredPriority + stableThermalOrder + 1
                                : int.MaxValue,
                        TimeLabel = RealtimePresentationText.Time(transition.Minute),
                        SeverityLabel = trip
                            ? "현재 초안 기준 예상 · 보호정지"
                            : "현재 초안 기준 예상 · 열 상태 변화",
                        SourceKind = RealtimeTimelineSourceKind.Draft,
                    });
                }
            }
        }

        foreach (RealtimeEventOutcome outcome in snapshot.CurrentChapterEvents
                     .Where(item => RealtimeTimelineTargetResolver.IsCurrentChapterOutcome(snapshot, item))
                     .Where(item => item.EndMinute <= snapshot.Minute)
                     .OrderByDescending(item => item.EndMinute)
                     .ThenBy(item => item.EventId, StringComparer.Ordinal)
                     .Take(RealtimeTimelinePolicy.HistoryLimit)
                     .OrderBy(item => item.EndMinute)
                     .ThenBy(item => item.EventId, StringComparer.Ordinal))
        {
            RealtimeScheduledEventDefinition scheduled = snapshot.Chapter.ScheduledEvents
                .Single(item => string.Equals(
                    item.EventId,
                    outcome.EventId,
                    StringComparison.Ordinal));
            string eventName = RealtimePresentationText.PlayerEventName(displayWorld, scheduled.OperatingProfile);
            RealtimeTimelineItemKind kind = EventKind(scheduled.OperatingProfile);
            items.Add(new RealtimeTimelineItemPresentation(
                outcome.EventId,
                kind,
                outcome.StartMinute,
                outcome.EndMinute,
                eventName,
                eventName,
                CompletedEventDescription(snapshot, outcome),
                outcome.SafetySatisfied && outcome.PromiseSatisfied
                    ? RealtimeTimelineSeverity.Information
                    : RealtimeTimelineSeverity.Critical,
                RealtimeTimelineVisibility.Completed,
                false,
                true)
            {
                Lane = kind is RealtimeTimelineItemKind.Weather or
                    RealtimeTimelineItemKind.PlannedOutage
                        ? RealtimeTimelineLane.WeatherAndOutage
                        : RealtimeTimelineLane.DemandAndDeadline,
                Priority = scheduled.Priority,
                TimeLabel = RealtimePresentationText.Time(outcome.StartMinute),
                EndTimeLabel = RealtimePresentationText.Time(outcome.EndMinute),
                SeverityLabel = outcome.SafetySatisfied && outcome.PromiseSatisfied
                    ? "운영 기록 완료"
                    : EventRiskLabel(snapshot, outcome, "운영 위험") +
                      " · 기록 완료",
            });
        }

        if (snapshot.Construction.ActiveConstruction is ActiveConstructionSnapshot project)
        {
            items.Add(new RealtimeTimelineItemPresentation(
                RealtimeR2Ids.ActiveConstructionMarker,
                RealtimeTimelineItemKind.Construction,
                project.CompletionMinute,
                null,
                project.Kind == ConstructionKind.Line
                    ? "실제 선로 공사"
                    : "실제 변전소 공사",
                "실제 공사 완공",
                $"발주된 실제 공사 · {RealtimePresentationText.Time(project.CompletionMinute)}에 완공 즉시 공급에 참여",
                RealtimeTimelineSeverity.Information,
                RealtimeTimelineVisibility.Active,
                true,
                true)
            {
                Lane = RealtimeTimelineLane.Construction,
                Priority = int.MinValue,
                TimeLabel = RealtimePresentationText.Time(project.CompletionMinute),
                SeverityLabel = "공사 중",
            });
        }

        foreach (RealtimeTransition transition in transitionHistory
                     .Where(item => item.Kind ==
                         RealtimeTransitionKind.ConstructionCompleted &&
                         item.Construction is not null &&
                         item.Minute <= snapshot.Minute)
                     .OrderByDescending(item => item.Minute)
                     .ThenBy(item => RealtimeR2Ids.CompletedConstructionMarker(item.Construction!),
                         StringComparer.Ordinal)
                     .Take(RealtimeTimelinePolicy.HistoryLimit)
                     .OrderBy(item => item.Minute)
                     .ThenBy(item => RealtimeR2Ids.CompletedConstructionMarker(item.Construction!),
                         StringComparer.Ordinal))
        {
            RealtimeConstructionCompletion completion = transition.Construction!;
            items.Add(new RealtimeTimelineItemPresentation(
                RealtimeR2Ids.CompletedConstructionMarker(completion),
                RealtimeTimelineItemKind.Construction,
                completion.CompletionMinute,
                null,
                completion.Kind == ConstructionKind.Line
                    ? "실제 선로 공사 완료"
                    : "실제 변전소 공사 완료",
                "실제 공사 완료",
                $"실제 완공 기록 · 설비 {completion.NodeIds.Count}곳 · " +
                $"선로 {completion.EdgeIds.Count}구간이 공급에 참여",
                RealtimeTimelineSeverity.Information,
                RealtimeTimelineVisibility.Completed,
                false,
                true)
            {
                Lane = RealtimeTimelineLane.Construction,
                Priority = int.MinValue + 1,
                TimeLabel = RealtimePresentationText.Time(completion.CompletionMinute),
                SeverityLabel = "실제 완공 기록",
            });
        }

        RealtimeProjectQuote? draftQuote = snapshot.Construction.NodeDraft is not null
            ? nodeOrderQuote
            : snapshot.Construction.LineDraft is { EndNodeId: not null }
                ? lineOrderQuote
                : null;
        if (!snapshot.CampaignComplete &&
            draftQuote is
            {
                Accepted: true,
                CompletionMinute: long draftCompletionMinute,
                BuildMinutes: long draftBuildMinutes,
            })
        {
            bool lineDraft = snapshot.Construction.LineDraft is { EndNodeId: not null };
            items.Add(new RealtimeTimelineItemPresentation(
                RealtimeR2Ids.DraftConstructionMarker,
                RealtimeTimelineItemKind.Construction,
                draftCompletionMinute,
                null,
                lineDraft ? "선로 초안 완공 예상" : "변전소 초안 완공 예상",
                "초안 완공 예상",
                $"아직 발주되지 않은 초안 · 발주하면 {draftBuildMinutes}분 뒤 " +
                $"{RealtimePresentationText.Time(draftCompletionMinute)} 완공 예상",
                RealtimeTimelineSeverity.Advisory,
                RealtimeTimelineVisibility.Announced,
                false,
                true)
            {
                Lane = RealtimeTimelineLane.Construction,
                Priority = int.MinValue + 2,
                TimeLabel = RealtimePresentationText.Time(draftCompletionMinute),
                SeverityLabel = "초안 예상 · 미발주",
                SourceKind = RealtimeTimelineSourceKind.Draft,
            });
        }

        long horizonMinutes = RealtimeTimelinePolicy.HorizonMinutes(interaction.TimelineHorizon);
        long anchor = interaction.TimelineAnchorMinute ?? snapshot.Minute;
        long horizonStart = Math.Max(0, anchor - RealtimeTimelinePolicy.HistoryMinutes);
        long horizonEnd = anchor > long.MaxValue - horizonMinutes
            ? long.MaxValue
            : anchor + horizonMinutes;
        RealtimeTimelineItemPresentation[] ordered = items
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        RealtimeForecastEvent? nextEvent = baseForecast.Events
            .Where(item => item.StartMinute > snapshot.Minute)
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => RealtimeTimelinePolicy.ForecastPriority(snapshot, item))
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .FirstOrDefault();
        RealtimeTimelineItemPresentation? nextDecision = ordered
            .Where(item => item.Kind == RealtimeTimelineItemKind.Decision &&
                item.StartMinute > snapshot.Minute)
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        RealtimeNextEventPresentation? nextEventPresentation =
            nextDecision is not null &&
            (nextEvent is null || nextDecision.StartMinute <= nextEvent.StartMinute)
                ? new RealtimeNextEventPresentation(
                    nextDecision.Id,
                    nextDecision.StartMinute,
                    nextDecision.StartMinute,
                    checked(nextDecision.StartMinute - snapshot.Minute),
                    nextDecision.Title,
                    RealtimePresentationText.Countdown(checked(nextDecision.StartMinute - snapshot.Minute)),
                    $"마감 {RealtimePresentationText.Time(nextDecision.StartMinute)}",
                    $"마감 {RealtimePresentationText.Clock(nextDecision.StartMinute)}")
                : nextEvent is null
                    ? null
                    : new RealtimeNextEventPresentation(
                nextEvent.EventId,
                nextEvent.StartMinute,
                nextEvent.EndMinute,
                checked(nextEvent.StartMinute - snapshot.Minute),
                RealtimePresentationText.PlayerEventName(displayWorld, nextEvent.OperatingProfile),
                RealtimePresentationText.Countdown(checked(nextEvent.StartMinute - snapshot.Minute)),
                $"시작 {RealtimePresentationText.Time(nextEvent.StartMinute)} · 종료 {RealtimePresentationText.Time(nextEvent.EndMinute)}",
                $"{RealtimePresentationText.Clock(nextEvent.StartMinute)}→{RealtimePresentationText.Clock(nextEvent.EndMinute)}");
        return new RealtimeEventRailPresentation(
            snapshot.Minute,
            horizonStart,
            horizonEnd,
            RealtimePresentationText.Time(snapshot.Minute),
            $"{RealtimeTimelinePolicy.HorizonLabel(interaction.TimelineHorizon)} · 지난 6시간",
            Array.AsReadOnly(ordered),
            interaction.TimelineSelectedItemId,
            nextEventPresentation)
        {
            HorizonPreset = interaction.TimelineHorizon,
            Expanded = interaction.Surface == RealtimeSurface.Timeline,
        };
    }

    private static string EventDescription(
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastEvent item,
        string eventName)
    {
        RealtimeEventOutcome outcome = item.TemporalProjection.Outcome;
        string safety = outcome.SafetySatisfied
            ? "안전 의무 충족 예상"
            : $"안전 의무 {outcome.SafetyUnservedMinutes}분 위험";
        string promise = snapshot.Chapter.Content.CityPromise is null
            ? string.Empty
            : snapshot.PromiseDecision == CommercialPromiseDecision.Defer
                ? " · Defer 기준 · 북안 수요 의무 제외"
                : outcome.PromiseSatisfied
                    ? snapshot.PromiseDecision == CommercialPromiseDecision.Unset
                        ? " · 선택 전 Keep 가정 · 약속 의무 충족 예상"
                        : " · Keep 기준 · 약속 의무 충족 예상"
                    : snapshot.PromiseDecision == CommercialPromiseDecision.Unset
                        ? $" · 선택 전 Keep 가정 · 약속 의무 " +
                          $"{outcome.PromiseUnservedMinutes}분 위험"
                        : $" · Keep 기준 · 약속 의무 " +
                          $"{outcome.PromiseUnservedMinutes}분 위험";
        return $"{eventName} · {RealtimePresentationText.Time(item.StartMinute)} · {safety}{promise}";
    }

    private static string CompletedEventDescription(
        RealtimeCampaignSnapshot snapshot,
        RealtimeEventOutcome outcome)
    {
        string safety = outcome.SafetySatisfied
            ? "안전 의무 충족"
            : $"안전 의무 {outcome.SafetyUnservedMinutes}분 미공급";
        string promise = snapshot.Chapter.Content.CityPromise is null
            ? string.Empty
            : snapshot.PromiseDecision == CommercialPromiseDecision.Defer
                ? " · Defer · 북안 수요 의무 제외"
                : outcome.PromiseSatisfied
                    ? " · Keep · 약속 의무 충족"
                    : $" · Keep · 약속 의무 {outcome.PromiseUnservedMinutes}분 미공급";
        return $"{RealtimePresentationText.Time(outcome.EndMinute)} 종료 · {safety}{promise}";
    }

    private static string EventRiskLabel(
        RealtimeCampaignSnapshot snapshot,
        RealtimeEventOutcome outcome,
        string satisfiedLabel)
    {
        var risks = new List<string>();
        if (!outcome.SafetySatisfied)
        {
            risks.Add("안전 의무 위험");
        }
        if (snapshot.Chapter.Content.CityPromise is not null &&
            snapshot.PromiseDecision != CommercialPromiseDecision.Defer &&
            !outcome.PromiseSatisfied)
        {
            risks.Add(snapshot.PromiseDecision == CommercialPromiseDecision.Unset
                ? "Keep 가정 약속 위험"
                : "약속 의무 위험");
        }
        return risks.Count == 0 ? satisfiedLabel : string.Join(" · ", risks);
    }

    private static string PromiseDecisionState(
        CommercialCityPromiseDefinition promise,
        CommercialPromiseDecision decision,
        bool defaulted) => defaulted
        ? $"자동 Defer · {promise.DeferLabel}"
        : decision switch
        {
            CommercialPromiseDecision.Unset => "미선택",
            CommercialPromiseDecision.Keep => $"Keep · {promise.KeepLabel}",
            CommercialPromiseDecision.Defer => $"Defer · {promise.DeferLabel}",
            _ => throw new ArgumentOutOfRangeException(nameof(decision)),
        };

    private static string PromiseDecisionDescription(
        CommercialCityPromiseDefinition promise,
        CommercialPromiseDecision decision,
        long deadline,
        bool locked,
        bool defaulted,
        bool promiseRisk,
        long recordedPromiseUnservedMinutes)
    {
        string state = PromiseDecisionState(promise, decision, defaulted);
        string forecast = decision switch
        {
            CommercialPromiseDecision.Keep
                when recordedPromiseUnservedMinutes > 0 =>
                $"기록된 약속 미공급 {recordedPromiseUnservedMinutes}분",
            CommercialPromiseDecision.Unset => promiseRisk
                ? "선택 전 Keep 가정에서 약속 미공급 위험"
                : "선택 전 Keep 가정에서 약속 공급 가능",
            CommercialPromiseDecision.Keep => promiseRisk
                ? "Keep 기준 약속 미공급 위험"
                : "Keep 기준 약속 공급 가능",
            CommercialPromiseDecision.Defer =>
                "Defer 기준 북안 수요를 이번 의무에서 제외",
            _ => throw new ArgumentOutOfRangeException(nameof(decision)),
        };
        return $"{promise.DisplayName} · 마감 {RealtimePresentationText.Time(deadline)} · {state} · " +
               $"{forecast} · {(locked ? "마감 완료, 선택 잠김" : "마감 전 변경 가능")}";
    }

    private static string PromiseDecisionSeverityLabel(
        CommercialPromiseDecision decision,
        bool locked,
        bool defaulted,
        bool promiseRisk,
        long recordedPromiseUnservedMinutes)
    {
        if (defaulted)
        {
            return "자동 Defer · 마감 완료";
        }
        if (locked)
        {
            if (decision == CommercialPromiseDecision.Keep &&
                recordedPromiseUnservedMinutes > 0)
            {
                return $"Keep · 약속 미공급 {recordedPromiseUnservedMinutes}분 · " +
                       "마감 완료";
            }
            return decision == CommercialPromiseDecision.Keep
                ? "Keep · 마감 완료"
                : "Defer · 마감 완료";
        }
        return decision switch
        {
            CommercialPromiseDecision.Unset => promiseRisk
                ? "미선택 · Keep 가정 위험"
                : "미선택 · Keep 가정",
            CommercialPromiseDecision.Keep => promiseRisk
                ? "Keep · 약속 위험"
                : "Keep · 선택됨",
            CommercialPromiseDecision.Defer => "Defer · 선택됨",
            _ => throw new ArgumentOutOfRangeException(nameof(decision)),
        };
    }

    private static RealtimeTimelineItemKind EventKind(
        CommercialOperatingPhaseDefinition profile)
    {
        if (profile.ActiveRiskAreaIds.Count > 0)
        {
            return RealtimeTimelineItemKind.Weather;
        }
        return profile.UnavailableNodeIds.Count > 0 || profile.UnavailableEdgeIds.Count > 0
            ? RealtimeTimelineItemKind.PlannedOutage
            : RealtimeTimelineItemKind.Demand;
    }

}

