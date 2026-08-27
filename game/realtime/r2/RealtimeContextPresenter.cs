using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;

namespace Gridworks.Game.Realtime.R2;

internal static class RealtimeContextPresenter
{
    internal static RealtimeContextDockPresentation Present(
        CommercialWorldDefinition displayWorld,
        RealtimeWorldDefinition realtimeWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastSnapshot baseForecast,
        RealtimeComparisonDraftForecast comparisonDraftForecast,
        string? selectedId,
        RealtimeProjectQuote? nodeOrderQuote,
        RealtimeProjectQuote? lineOrderQuote,
        IReadOnlyList<RealtimeTransition> transitionHistory)
    {
        if (selectedId is null)
        {
            return new RealtimeContextDockPresentation(
                string.Empty,
                false,
                string.Empty,
                string.Empty,
                Array.Empty<RealtimeContextSectionPresentation>());
        }

        if (RealtimeTimelineTargetResolver.IsPromiseDecisionMarker(snapshot, selectedId) &&
            snapshot.Chapter.Content.CityPromise is CommercialCityPromiseDefinition promise &&
            snapshot.Chapter.PromiseDecisionDeadlineOffsetMinutes is int deadlineOffset)
        {
            long deadline = checked(snapshot.ChapterStartMinute + deadlineOffset);
            bool locked = snapshot.Minute >= deadline || snapshot.CampaignComplete;
            bool defaulted = RealtimePromisePresentationFacts.PromiseDefaulted(snapshot, transitionHistory);
            long recordedPromiseUnservedMinutes =
                RealtimePromisePresentationFacts.RecordedPromiseUnservedMinutes(snapshot);
            bool promiseRisk = recordedPromiseUnservedMinutes > 0 ||
                baseForecast.Events.Any(item =>
                item.ChapterIndex == snapshot.ChapterIndex &&
                !item.TemporalProjection.Outcome.PromiseSatisfied);
            string promiseLoadName = RealtimePresentationText.LoadDisplayName(
                displayWorld,
                promise.LoadId);
            string decision = defaulted
                ? "자동 Defer"
                : snapshot.PromiseDecision switch
                {
                    CommercialPromiseDecision.Unset => "미선택",
                    CommercialPromiseDecision.Keep => "Keep",
                    CommercialPromiseDecision.Defer => "Defer",
                    _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
                };
            string promiseForecastState = snapshot.PromiseDecision switch
            {
                CommercialPromiseDecision.Keep
                    when recordedPromiseUnservedMinutes > 0 =>
                    $"약속 {recordedPromiseUnservedMinutes}분 미공급",
                CommercialPromiseDecision.Unset => promiseRisk
                    ? "Keep 가정 위험"
                    : "Keep 가정 가능",
                CommercialPromiseDecision.Keep => promiseRisk
                    ? "약속 위험"
                    : "약속 가능",
                CommercialPromiseDecision.Defer =>
                    $"{promiseLoadName} 수요 의무 제외",
                _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
            };
            bool actionsEnabled = !locked;
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                "운영 약속 마감",
                promise.DisplayName,
                new RealtimeContextSectionPresentation[]
                {
                    new("시각", RealtimePresentationText.Clock(deadline),
                        locked
                            ? RealtimeTimelineSeverity.Warning
                            : RealtimeTimelineSeverity.Advisory),
                    new("결정과 전망", $"현재 {decision}\n{promiseForecastState}",
                        promiseRisk && snapshot.PromiseDecision !=
                            CommercialPromiseDecision.Defer
                                ? RealtimeTimelineSeverity.Critical
                                : defaulted
                                    ? RealtimeTimelineSeverity.Warning
                                    : RealtimeTimelineSeverity.Information),
                },
                new RealtimeActionPresentation(
                    RealtimeR2Ids.PromiseKeepAction,
                    promise.KeepLabel,
                    actionsEnabled
                        ? $"{promiseLoadName} 수요를 약속 의무에 포함합니다."
                        : "약속 마감이 지나 선택을 바꿀 수 없습니다.",
                    actionsEnabled),
                new RealtimeActionPresentation(
                    RealtimeR2Ids.PromiseDeferAction,
                    promise.DeferLabel,
                    actionsEnabled
                        ? $"{promiseLoadName} 수요를 이번 운영 의무에서 제외합니다."
                        : "약속 마감이 지나 선택을 바꿀 수 없습니다.",
                    actionsEnabled,
                    RealtimeActionTone.Secondary));
        }

        if (string.Equals(selectedId, RealtimeR2Ids.ActiveConstructionMarker, StringComparison.Ordinal) &&
            snapshot.Construction.ActiveConstruction is ActiveConstructionSnapshot project)
        {
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                "실제 공사 중",
                project.Kind == ConstructionKind.Line ? "선로 공사" : "변전소 공사",
                new RealtimeContextSectionPresentation[]
                {
                    new("완공", RealtimePresentationText.Time(project.CompletionMinute)),
                    new("비용", RealtimePresentationText.Cash(project.CostCashUnit)),
                    new("범위", $"설비 {project.NodeIds.Count}곳 · 선로 {project.EdgeIds.Count}구간"),
                })
            {
                Details = new RealtimeContextDetailPresentation[]
                {
                    new(
                        RealtimeContextDetailTab.Route,
                        "공사 범위",
                        $"설비: {RealtimePresentationText.JoinAssetNames(displayWorld, snapshot, project.NodeIds)}\n" +
                        $"선로: {RealtimePresentationText.JoinAssetNames(displayWorld, snapshot, project.EdgeIds)}",
                        RealtimeTimelineSeverity.Information),
                },
            };
        }

        RealtimeProjectQuote? selectedDraftQuote = snapshot.Construction.NodeDraft is not null
            ? nodeOrderQuote
            : snapshot.Construction.LineDraft is { EndNodeId: not null }
                ? lineOrderQuote
                : null;
        if (!snapshot.CampaignComplete &&
            string.Equals(selectedId, RealtimeR2Ids.DraftConstructionMarker, StringComparison.Ordinal) &&
            selectedDraftQuote is
            {
                Accepted: true,
                CostCashUnit: long draftCost,
                BuildMinutes: long draftBuildMinutes,
                CompletionMinute: long draftCompletionMinute,
            })
        {
            bool lineDraft = snapshot.Construction.LineDraft is { EndNodeId: not null };
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                "초안 예상 · 아직 미발주",
                lineDraft ? "선로 초안" : "변전소 초안",
                new RealtimeContextSectionPresentation[]
                {
                    new("상태", "초안 · 실제 공사 아님",
                        RealtimeTimelineSeverity.Advisory),
                    new("발주 시 완공", RealtimePresentationText.Time(draftCompletionMinute)),
                    new("예상 공기", $"{draftBuildMinutes}분"),
                    new("발주 비용", RealtimePresentationText.Cash(draftCost)),
                });
        }

        if (RealtimeTimelineTargetResolver.TryResolveCompletedConstruction(
                transitionHistory,
                selectedId,
                out RealtimeConstructionCompletion completedConstruction))
        {
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                "실제 완공 기록",
                completedConstruction.Kind == ConstructionKind.Line
                    ? "선로 공사 완료"
                    : "변전소 공사 완료",
                new RealtimeContextSectionPresentation[]
                {
                    new("실제 완공", RealtimePresentationText.Time(completedConstruction.CompletionMinute)),
                    new("상태", "완공됨 · 현재 공급망에 반영"),
                    new("범위", $"설비 {completedConstruction.NodeIds.Count}곳 · " +
                        $"선로 {completedConstruction.EdgeIds.Count}구간"),
                })
            {
                Details = new RealtimeContextDetailPresentation[]
                {
                    new(
                        RealtimeContextDetailTab.History,
                        "실제 공사 이력",
                        $"설비: {RealtimePresentationText.JoinAssetNames(displayWorld, snapshot, completedConstruction.NodeIds)}\n" +
                        $"선로: {RealtimePresentationText.JoinAssetNames(displayWorld, snapshot, completedConstruction.EdgeIds)}",
                        RealtimeTimelineSeverity.Information),
                },
            };
        }

        if (RealtimeTimelineTargetResolver.TryResolveComparisonEvent(
                comparisonDraftForecast,
                selectedId,
                out RealtimeForecastEvent comparisonEvent))
        {
            RealtimeEventOutcome outcome = comparisonEvent.TemporalProjection.Outcome;
            string eventName = RealtimePresentationText.PlayerEventName(
                displayWorld,
                comparisonEvent.OperatingProfile);
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                "현재 초안 기준 예상",
                eventName,
                EventContextSections(displayWorld, snapshot, outcome,
                [
                    new("발생", $"{RealtimePresentationText.Time(comparisonEvent.StartMinute)}–{RealtimePresentationText.Time(comparisonEvent.EndMinute)}"),
                    new("안전 의무", outcome.SafetySatisfied
                        ? "현재 초안 기준 예상 · 공급 충족"
                        : $"현재 초안 기준 예상 · {outcome.SafetyUnservedMinutes}분 미공급",
                        outcome.SafetySatisfied
                            ? RealtimeTimelineSeverity.Information
                            : RealtimeTimelineSeverity.Critical),
                    new("첫 병목", RealtimePresentationText.FirstBottleneck(
                        displayWorld,
                        snapshot,
                        comparisonEvent.ProjectedEvaluation)),
                ], "현재 초안 기준 예상 · "))
            {
                Details = new RealtimeContextDetailPresentation[]
                {
                    new(
                        RealtimeContextDetailTab.Forecast,
                        "현재 초안 기준 예상",
                        RealtimePresentationText.ForecastDetail(displayWorld, snapshot, comparisonEvent),
                        outcome.SafetySatisfied && outcome.PromiseSatisfied
                            ? RealtimeTimelineSeverity.Advisory
                            : RealtimeTimelineSeverity.Critical),
                },
            };
        }

        if (RealtimeTimelineTargetResolver.TryResolveComparisonThermalMarker(
                comparisonDraftForecast,
                selectedId,
                out RealtimeForecastEvent comparisonOwningForecast,
                out RealtimeThermalTransition comparisonTransition))
        {
            string eventName = RealtimePresentationText.PlayerEventName(
                displayWorld,
                comparisonOwningForecast.OperatingProfile);
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                "현재 초안 기준 예상 · 열 보호",
                RealtimePresentationText.AssetDisplayName(displayWorld, snapshot, comparisonTransition.AssetId),
                new RealtimeContextSectionPresentation[]
                {
                    new("예상 시각", RealtimePresentationText.Time(comparisonTransition.Minute)),
                    new("예상 변화", RealtimePresentationText.ThermalTitle(
                            displayWorld,
                            snapshot,
                            comparisonTransition),
                        comparisonTransition.Kind ==
                            RealtimeThermalTransitionKind.ProtectiveTrip
                            ? RealtimeTimelineSeverity.Critical
                            : RealtimeTimelineSeverity.Warning),
                    new("근거", $"현재 초안 기준 예상 · {eventName}"),
                })
            {
                Details = new RealtimeContextDetailPresentation[]
                {
                    new(
                        RealtimeContextDetailTab.Forecast,
                        "현재 초안 기준 예상",
                        RealtimePresentationText.ForecastDetail(
                            displayWorld,
                            snapshot,
                            comparisonOwningForecast),
                        comparisonTransition.Kind ==
                            RealtimeThermalTransitionKind.ProtectiveTrip
                            ? RealtimeTimelineSeverity.Critical
                            : RealtimeTimelineSeverity.Warning),
                },
            };
        }

        RealtimeForecastEvent? forecast = baseForecast.Events.FirstOrDefault(item =>
            string.Equals(item.EventId, selectedId, StringComparison.Ordinal));
        if (forecast is not null)
        {
            RealtimeEventOutcome outcome = forecast.TemporalProjection.Outcome;
            string eventName = RealtimePresentationText.PlayerEventName(displayWorld, forecast.OperatingProfile);
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                "사건 지평선",
                eventName,
                EventContextSections(displayWorld, snapshot, outcome,
                [
                    new("발생", $"{RealtimePresentationText.Time(forecast.StartMinute)}–{RealtimePresentationText.Time(forecast.EndMinute)}"),
                    new("안전 의무", outcome.SafetySatisfied
                        ? "예상 공급 충족"
                        : $"{outcome.SafetyUnservedMinutes}분 미공급 예상",
                        outcome.SafetySatisfied
                            ? RealtimeTimelineSeverity.Information
                            : RealtimeTimelineSeverity.Critical),
                    new("첫 병목", RealtimePresentationText.FirstBottleneck(
                        displayWorld,
                        snapshot,
                        forecast.ProjectedEvaluation)),
                ], string.Empty))
            {
                Details = new RealtimeContextDetailPresentation[]
                {
                    new(
                        RealtimeContextDetailTab.Route,
                        "예상 공급 경로",
                        RealtimePresentationText.ForecastRoutes(displayWorld, snapshot, forecast.ProjectedEvaluation),
                        outcome.SafetySatisfied && outcome.PromiseSatisfied
                            ? RealtimeTimelineSeverity.Information
                            : RealtimeTimelineSeverity.Critical),
                    new(
                        RealtimeContextDetailTab.Forecast,
                        "시간별 변화",
                        RealtimePresentationText.ForecastDetail(displayWorld, snapshot, forecast),
                        outcome.SafetySatisfied && outcome.PromiseSatisfied
                            ? RealtimeTimelineSeverity.Advisory
                            : RealtimeTimelineSeverity.Critical),
                },
            };
        }

        RealtimeEventOutcome? completed = snapshot.CurrentChapterEvents.FirstOrDefault(item =>
            RealtimeTimelineTargetResolver.IsCurrentChapterOutcome(snapshot, item) &&
            string.Equals(item.EventId, selectedId, StringComparison.Ordinal));
        if (completed is not null)
        {
            RealtimeScheduledEventDefinition scheduled = snapshot.Chapter.ScheduledEvents
                .Single(item => string.Equals(
                    item.EventId,
                    completed.EventId,
                    StringComparison.Ordinal));
            string eventName = RealtimePresentationText.PlayerEventName(displayWorld, scheduled.OperatingProfile);
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                "최근 운영 기록",
                eventName,
                EventContextSections(displayWorld, snapshot, completed,
                [
                    new("운영 시각", $"{RealtimePresentationText.Time(completed.StartMinute)}–{RealtimePresentationText.Time(completed.EndMinute)}"),
                    new("안전 의무", completed.SafetySatisfied
                        ? "공급 충족"
                        : $"{completed.SafetyUnservedMinutes}분 미공급",
                        completed.SafetySatisfied
                            ? RealtimeTimelineSeverity.Information
                            : RealtimeTimelineSeverity.Critical),
                    new("첫 병목", RealtimePresentationText.FirstBottleneck(
                        displayWorld,
                        snapshot,
                        completed.FinalEvaluation)),
                ], string.Empty))
            {
                Details = new RealtimeContextDetailPresentation[]
                {
                    new(
                        RealtimeContextDetailTab.History,
                        "완료된 공급 기록",
                        RealtimePresentationText.ForecastRoutes(displayWorld, snapshot, completed.FinalEvaluation),
                        completed.SafetySatisfied && completed.PromiseSatisfied
                            ? RealtimeTimelineSeverity.Information
                            : RealtimeTimelineSeverity.Critical),
                },
            };
        }

        if (RealtimeTimelineTargetResolver.TryResolveThermalMarker(
                baseForecast,
                selectedId,
                out RealtimeForecastEvent owningForecast,
                out RealtimeThermalTransition selectedTransition))
        {
            RealtimeThermalAssetSnapshot? current = snapshot.Thermal.Assets.FirstOrDefault(item =>
                string.Equals(
                    item.AssetId,
                    selectedTransition.AssetId,
                    StringComparison.Ordinal));
            string eventName = RealtimePresentationText.PlayerEventName(displayWorld, owningForecast.OperatingProfile);
            string currentState = current is null
                ? "현재 열 상태 기록 없음"
                : current.State switch
                {
                    ThermalOperatingState.Continuous => "현재 연속 운전",
                    ThermalOperatingState.Emergency => "현재 비상 운전",
                    ThermalOperatingState.ProtectiveOutage => "현재 보호정지",
                    ThermalOperatingState.OverLimit => "현재 한계 초과",
                    _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
                };
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                $"{eventName} · 열 보호 예상",
                RealtimePresentationText.AssetDisplayName(displayWorld, snapshot, selectedTransition.AssetId),
                new RealtimeContextSectionPresentation[]
                {
                    new("예상 시각", RealtimePresentationText.Time(selectedTransition.Minute)),
                    new("예상 변화", RealtimePresentationText.ThermalTitle(displayWorld, snapshot, selectedTransition),
                        selectedTransition.Kind == RealtimeThermalTransitionKind.ProtectiveTrip
                            ? RealtimeTimelineSeverity.Critical
                            : RealtimeTimelineSeverity.Warning),
                    new("현재 상태", currentState),
                })
            {
                Details = new RealtimeContextDetailPresentation[]
                {
                    new(
                        RealtimeContextDetailTab.Forecast,
                        "사건별 열 보호 예상",
                        RealtimePresentationText.ForecastDetail(displayWorld, snapshot, owningForecast),
                        selectedTransition.Kind == RealtimeThermalTransitionKind.ProtectiveTrip
                            ? RealtimeTimelineSeverity.Critical
                            : RealtimeTimelineSeverity.Warning),
                },
            };
        }

        if (RealtimeTimelineTargetResolver.TryResolveActualThermalMarker(
                transitionHistory,
                selectedId,
                out RealtimeTransition actualThermal))
        {
            RealtimeThermalTransition actualTransition =
                RealtimeThermalPresentation.FromTransition(actualThermal);
            RealtimeThermalAssetSnapshot? current = snapshot.Thermal.Assets
                .FirstOrDefault(item => string.Equals(
                    item.AssetId,
                    actualTransition.AssetId,
                    StringComparison.Ordinal));
            string source = actualThermal.EventId is null
                ? "실시간 운영"
                : snapshot.Chapter.ScheduledEvents
                    .Where(item => string.Equals(
                        item.EventId,
                        actualThermal.EventId,
                        StringComparison.Ordinal))
                    .Select(item => RealtimePresentationText.PlayerEventName(
                        displayWorld,
                        item.OperatingProfile))
                    .SingleOrDefault() ?? "실시간 운영";
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                "실제 열 보호 기록",
                RealtimePresentationText.AssetDisplayName(
                    displayWorld,
                    snapshot,
                    actualTransition.AssetId),
                new RealtimeContextSectionPresentation[]
                {
                    new("기록 시각", RealtimePresentationText.Time(
                        actualTransition.Minute)),
                    new("실제 변화", RealtimePresentationText.ThermalTitle(
                            displayWorld,
                            snapshot,
                            actualTransition),
                        actualTransition.Kind switch
                        {
                            RealtimeThermalTransitionKind.ProtectiveTrip =>
                                RealtimeTimelineSeverity.Critical,
                            RealtimeThermalTransitionKind.EmergencyCleared or
                                RealtimeThermalTransitionKind.Recovered =>
                                RealtimeTimelineSeverity.Information,
                            _ => RealtimeTimelineSeverity.Warning,
                        }),
                    new("운영 구간", source),
                })
            {
                Details = current is null
                    ? Array.Empty<RealtimeContextDetailPresentation>()
                    : new RealtimeContextDetailPresentation[]
                    {
                        new(
                            RealtimeContextDetailTab.History,
                            "최근 상태 변화",
                            RealtimePresentationText.TransitionHistory(
                                transitionHistory,
                                current),
                            RealtimeTimelineSeverity.Advisory),
                    },
            };
        }

        SpatialNodeDefinition? node = snapshot.Construction.World.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, selectedId, StringComparison.Ordinal));
        SpatialEdgeDefinition? edge = snapshot.Construction.World.Edges.FirstOrDefault(item =>
            string.Equals(item.EdgeId, selectedId, StringComparison.Ordinal));
        if ((node is not null && !node.Commissioned) ||
            (edge is not null && !edge.Commissioned))
        {
            string constructionHeading = RealtimePresentationText.AssetDisplayName(
                displayWorld,
                snapshot,
                selectedId);
            string constructionBody = node is not null
                ? $"{RealtimePresentationText.NodeClassDisplayName(snapshot, node.ClassId)} · 공사 중 · 완공 전 공급 불가"
                : $"{RealtimePresentationText.LineClassDisplayName(snapshot, edge!.LineClassId)} · 공사 중 · 완공 전 공급 불가";
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                "공사 중 설비",
                constructionHeading,
                new[]
                {
                    new RealtimeContextSectionPresentation(
                        "현재 상태",
                        "공사 중",
                        RealtimeTimelineSeverity.Advisory),
                    new RealtimeContextSectionPresentation("운영", constructionBody),
                });
        }

        RealtimeThermalAssetSnapshot? asset = snapshot.Thermal.Assets.FirstOrDefault(item =>
            string.Equals(item.AssetId, selectedId, StringComparison.Ordinal));
        if (asset is not null)
        {
            RealtimeThermalProtectionCopy protection = RealtimeThermalPresentation.For(
                realtimeWorld,
                snapshot.Thermal,
                asset);
            string state = asset.ProtectiveOutage && asset.AuthoredUnavailable
                ? "보호정지 · 계획 사용불가 겹침"
                : asset.ProtectiveOutage
                    ? "보호정지"
                    : asset.AuthoredUnavailable
                        ? "계획 사용불가"
                        : asset.State switch
            {
                ThermalOperatingState.Continuous => "연속 운전",
                ThermalOperatingState.Emergency => "비상 운전",
                ThermalOperatingState.ProtectiveOutage => "보호정지",
                ThermalOperatingState.OverLimit => "한계 초과",
                _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
            };
            RealtimeTimelineSeverity stateSeverity = asset.ProtectiveOutage
                ? RealtimeTimelineSeverity.Critical
                : asset.AuthoredUnavailable
                    ? RealtimeTimelineSeverity.Warning
                    : asset.State switch
                    {
                        ThermalOperatingState.Continuous =>
                            RealtimeTimelineSeverity.Information,
                        ThermalOperatingState.Emergency =>
                            RealtimeTimelineSeverity.Warning,
                        _ => RealtimeTimelineSeverity.Critical,
                    };
            var sections = new List<RealtimeContextSectionPresentation>
            {
                new("현재 상태", state, stateSeverity),
                new("열여유", string.Create(CultureInfo.InvariantCulture,
                    $"사용 {asset.UsedKw:N0} / 연속 {asset.ContinuousKw:N0} / 비상 {asset.EmergencyKw:N0} kW")),
                new("보호", protection.Summary),
            };
            if (asset.AuthoredUnavailable || asset.ProtectiveOutage)
            {
                sections.Add(new RealtimeContextSectionPresentation(
                    "사용불가 원인",
                    asset.AuthoredUnavailable && asset.ProtectiveOutage
                        ? "작성된 사용불가와 열 보호정지가 함께 적용 중입니다. 더 늦은 복귀 시각까지 공급 경로에서 제외됩니다."
                        : asset.AuthoredUnavailable
                            ? "작성된 계획 사용불가가 적용 중이며 공급 경로에서 제외됩니다."
                            : "비상 허용시간 소진으로 열 보호정지가 적용 중입니다.",
                    asset.ProtectiveOutage
                        ? RealtimeTimelineSeverity.Critical
                        : RealtimeTimelineSeverity.Warning));
            }
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                RealtimePresentationText.ThermalAssetDisplayKind(snapshot, asset),
                RealtimePresentationText.AssetDisplayName(displayWorld, snapshot, selectedId),
                Array.AsReadOnly(sections.ToArray()))
            {
                Details = new RealtimeContextDetailPresentation[]
                {
                    new(
                        RealtimeContextDetailTab.Thermal,
                        "열 보호 상태",
                        protection.Detail,
                        stateSeverity),
                    new(
                        RealtimeContextDetailTab.History,
                        "최근 상태 변화",
                        RealtimePresentationText.TransitionHistory(
                            transitionHistory,
                            asset),
                        RealtimeTimelineSeverity.Advisory),
                },
            };
        }

        string heading = RealtimePresentationText.AssetDisplayName(displayWorld, snapshot, selectedId);
        string body = node is not null
            ? $"{RealtimePresentationText.NodeClassDisplayName(snapshot, node.ClassId)} · " +
              $"{(node.Commissioned ? "사용 가능" : "공사 중")}"
            : edge is not null
                ? $"{RealtimePresentationText.LineClassDisplayName(snapshot, edge.LineClassId)} · " +
                  $"{(edge.Commissioned ? "사용 가능" : "공사 중")}"
                : "현재 지평선에서 찾을 수 없습니다.";
        return new RealtimeContextDockPresentation(
            selectedId,
            true,
            "망 설비",
            heading,
            new[] { new RealtimeContextSectionPresentation("식별", body) });
    }

    private static IReadOnlyList<RealtimeContextSectionPresentation>
        EventContextSections(
            CommercialWorldDefinition displayWorld,
            RealtimeCampaignSnapshot snapshot,
            RealtimeEventOutcome outcome,
            IReadOnlyList<RealtimeContextSectionPresentation> baseSections,
            string prefix)
    {
        if (snapshot.Chapter.Content.CityPromise is not
                CommercialCityPromiseDefinition promise ||
            !RealtimePromisePresentationFacts.HasPromiseDuty(outcome))
        {
            return baseSections;
        }
        string promiseLoadName = RealtimePresentationText.LoadDisplayName(
            displayWorld,
            promise.LoadId);
        string body = snapshot.PromiseDecision switch
        {
            CommercialPromiseDecision.Unset => outcome.PromiseSatisfied
                ? $"{prefix}선택 전 Keep 가정 · 공급 충족"
                : $"{prefix}선택 전 Keep 가정 · " +
                  $"{outcome.PromiseUnservedMinutes}분 미공급",
            CommercialPromiseDecision.Keep => outcome.PromiseSatisfied
                ? $"{prefix}Keep · 공급 충족"
                : $"{prefix}Keep · {outcome.PromiseUnservedMinutes}분 미공급",
            CommercialPromiseDecision.Defer =>
                $"{prefix}Defer · {promiseLoadName} 수요 의무 제외",
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
        };
        var sections = new List<RealtimeContextSectionPresentation>(baseSections)
        {
            new(
                "약속 의무",
                body,
                !outcome.PromiseSatisfied && snapshot.PromiseDecision !=
                    CommercialPromiseDecision.Defer
                        ? RealtimeTimelineSeverity.Critical
                        : RealtimeTimelineSeverity.Information),
        };
        return Array.AsReadOnly(sections.ToArray());
    }

}
