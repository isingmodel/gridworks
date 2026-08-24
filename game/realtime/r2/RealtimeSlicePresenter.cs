using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

internal sealed record RealtimeSlicePresentation(
    long Revision,
    RealtimeCampaignSnapshot CoreSnapshot,
    RealtimeForecastSnapshot BaseForecast,
    RealtimeComparisonDraftForecast ComparisonDraftForecast,
    IReadOnlyList<RealtimeTransition> TransitionHistory,
    RealtimeInteractionPresentation Interaction,
    RealtimeWorldPresentation World,
    RealtimeWorldPointerFeedback Pointer,
    RealtimeTopHudPresentation Hud,
    RealtimeEventRailPresentation Rail,
    RealtimeContextDockPresentation Context,
    RealtimeBuildShelfPresentation BuildShelf,
    RealtimeActionDockPresentation ActionDock,
    RealtimeModalPresentation? Modal)
{
    private IReadOnlyList<RealtimeTransition> _transitionHistory =
        Array.AsReadOnly(TransitionHistory.ToArray());

    public IReadOnlyList<RealtimeTransition> TransitionHistory
    {
        get => _transitionHistory;
        init => _transitionHistory = Array.AsReadOnly(value.ToArray());
    }
}

internal static class RealtimeSlicePresenter
{
    internal static RealtimeSlicePresentation Present(RealtimePresentationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.Data);
        ArgumentNullException.ThrowIfNull(source.Data.BaseWorld);
        ArgumentNullException.ThrowIfNull(source.Data.World);
        ArgumentNullException.ThrowIfNull(source.Snapshot);
        ArgumentNullException.ThrowIfNull(source.BaseForecast);
        ArgumentNullException.ThrowIfNull(source.ComparisonDraftForecast);
        ArgumentNullException.ThrowIfNull(source.Interaction);
        ArgumentNullException.ThrowIfNull(source.Pointer);

        CommercialWorldDefinition displayWorld = source.Data.BaseWorld;
        RealtimeWorldDefinition realtimeWorld = source.Data.World;
        RealtimeCampaignSnapshot snapshot = source.Snapshot;
        RealtimeForecastSnapshot baseForecast = source.BaseForecast;
        RealtimeComparisonDraftForecast comparisonDraftForecast =
            source.ComparisonDraftForecast;
        RealtimeInteractionState interaction = source.Interaction;
        IReadOnlyList<RealtimeTransition> history = source.TransitionHistory;

        RealtimePausePresentation pause = Pause(
            displayWorld,
            snapshot,
            baseForecast,
            interaction.PauseReason);
        RealtimeInteractionPresentation interactionPresentation =
            interaction.ToPresentation(pause);
        RealtimeEventRailPresentation rail = Rail(
            displayWorld,
            snapshot,
            baseForecast,
            comparisonDraftForecast,
            interaction,
            source.NodeOrderQuote,
            source.LineOrderQuote,
            history);
        return new RealtimeSlicePresentation(
            source.Revision,
            snapshot,
            baseForecast,
            comparisonDraftForecast,
            history,
            interactionPresentation,
            World(
                displayWorld,
                snapshot,
                baseForecast,
                comparisonDraftForecast,
                interaction,
                source.ReduceMotion,
                history),
            source.Pointer,
            Hud(displayWorld, snapshot, interaction, pause),
            rail,
            Context(
                displayWorld,
                realtimeWorld,
                snapshot,
                baseForecast,
                comparisonDraftForecast,
                interaction.Surface == RealtimeSurface.Inspector
                    ? interaction.SelectionId
                    : null,
                source.NodeOrderQuote,
                source.LineOrderQuote,
                history),
            BuildShelf(
                realtimeWorld,
                snapshot,
                interaction,
                source.Pointer.Accepted,
                source.Pointer.Message),
            ActionDock(
                snapshot,
                interaction,
                source.Pointer.Accepted,
                source.Pointer.Message,
                source.NodeOrderQuote,
                source.LineOrderQuote),
            RealtimeModalPresenter.Present(source, pause));
    }

    /// <summary>
    /// Projects pointer feedback from the last authoritative presentation. This path performs no
    /// snapshot fetch or forecast calculation and only changes the world pointer, build guidance,
    /// and action-dock DTOs that can visibly depend on hover feedback.
    /// </summary>
    internal static RealtimeSlicePresentation PresentPointerFeedback(
        RealtimeSlicePresentation current,
        RealtimeInteractionState interaction,
        long revision,
        RealtimeWorldPointerFeedback pointer,
        RealtimeProjectQuote? nodeOrderQuote,
        RealtimeProjectQuote? lineOrderQuote)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(interaction);
        ArgumentNullException.ThrowIfNull(pointer);
        return current with
        {
            Revision = revision,
            Pointer = pointer,
            BuildShelf = current.BuildShelf with
            {
                Guidance = RealtimePresentationText.BuildGuidance(
                    current.CoreSnapshot,
                    interaction,
                    pointer.Accepted,
                    pointer.Message),
            },
            ActionDock = ActionDock(
                current.CoreSnapshot,
                interaction,
                pointer.Accepted,
                pointer.Message,
                nodeOrderQuote,
                lineOrderQuote),
        };
    }

    private static RealtimeWorldPresentation World(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastSnapshot baseForecast,
        RealtimeComparisonDraftForecast comparisonDraftForecast,
        RealtimeInteractionState interaction,
        bool reduceMotion,
        IReadOnlyList<RealtimeTransition> transitionHistory)
    {
        IReadOnlyDictionary<string, RealtimeThermalAssetSnapshot> thermalById =
            snapshot.Thermal.Assets.ToDictionary(
                item => item.AssetId,
                StringComparer.Ordinal);
        RealtimeWorldAssetStatus[] statuses = snapshot.Construction.World.Nodes
            .Select(item => WorldStatus(
                item.NodeId,
                item.Commissioned,
                thermalById.GetValueOrDefault(item.NodeId)))
            .Concat(snapshot.Construction.World.Edges.Select(item => WorldStatus(
                item.EdgeId,
                item.Commissioned,
                thermalById.GetValueOrDefault(item.EdgeId))))
            .OrderBy(item => item.AssetId, StringComparer.Ordinal)
            .ToArray();
        string[] riskIds = snapshot.ActiveEventStates
            .SelectMany(item => item.Event.OperatingProfile.ActiveRiskAreaIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        string[] forecastRiskIds = interaction.Tool == RealtimeTool.Analysis
            ? baseForecast.Events
                .Where(item =>
                    item.Status == RealtimeForecastStatus.Upcoming &&
                    string.Equals(
                        item.EventId,
                        interaction.TimelineSelectedItemId,
                        StringComparison.Ordinal))
                .SelectMany(item => item.OperatingProfile.ActiveRiskAreaIds)
                .Distinct(StringComparer.Ordinal)
                .Except(riskIds, StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();
        return new RealtimeWorldPresentation(
            snapshot.Construction.World,
            Array.AsReadOnly(statuses),
            Draft(snapshot.Construction),
            !snapshot.CampaignComplete &&
                interaction.Simulation != RealtimeSimulationState.Ended &&
                (interaction.Tool is RealtimeTool.BuildNode or
                    RealtimeTool.BuildLine or RealtimeTool.MoveDraft),
            RealtimeTimelineTargetResolver.MapSelectionId(
                displayWorld,
                snapshot,
                baseForecast,
                comparisonDraftForecast,
                transitionHistory,
                interaction.SelectionId),
            interaction.Tool == RealtimeTool.Analysis,
            Weather(snapshot),
            snapshot.Minute,
            Array.AsReadOnly(forecastRiskIds),
            Array.AsReadOnly(riskIds),
            Highlight(
                displayWorld,
                snapshot,
                baseForecast,
                comparisonDraftForecast,
                transitionHistory,
                interaction.SelectionId),
            reduceMotion,
            interaction.Tool,
            interaction.Surface);
    }

    private static RealtimeWorldDraftPresentation Draft(ConstructionSnapshot construction)
    {
        var handles = new List<RealtimeWorldDraftHandle>();
        if (construction.NodeDraft is NodeDraftSnapshot nodeDraft)
        {
            handles.Add(new RealtimeWorldDraftHandle(
                RealtimeWorldIds.DraftNode,
                nodeDraft.Position));
        }

        var linePath = new List<CoreMapPoint>();
        bool extendLineToPointer = false;
        if (construction.LineDraft is LineDraftSnapshot lineDraft)
        {
            SpatialNodeDefinition start = construction.World.Nodes.Single(item =>
                string.Equals(
                    item.NodeId,
                    lineDraft.StartNodeId,
                    StringComparison.Ordinal));
            linePath.Add(start.Position);
            for (int index = 0; index < lineDraft.IntermediatePoints.Count; index++)
            {
                CoreMapPoint point = lineDraft.IntermediatePoints[index];
                linePath.Add(point);
                handles.Add(new RealtimeWorldDraftHandle(
                    RealtimeWorldIds.DraftPoint(index),
                    point));
            }
            if (lineDraft.EndNodeId is string endNodeId)
            {
                linePath.Add(construction.World.Nodes.Single(item => string.Equals(
                    item.NodeId,
                    endNodeId,
                    StringComparison.Ordinal)).Position);
            }
            else
            {
                extendLineToPointer = true;
            }
        }
        return new RealtimeWorldDraftPresentation(
            Array.AsReadOnly(handles.ToArray()),
            Array.AsReadOnly(linePath.ToArray()),
            extendLineToPointer);
    }

    private static RealtimeTopHudPresentation Hud(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeInteractionState interaction,
        RealtimePausePresentation pause)
    {
        RealtimeReliabilityState reliability = Reliability(snapshot);
        string reliabilityLabel = reliability switch
        {
            RealtimeReliabilityState.Stable => "안정",
            RealtimeReliabilityState.Watch => "공급 주의",
            RealtimeReliabilityState.Emergency => "비상 열운전",
            RealtimeReliabilityState.Outage => "보호정지",
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
        };
        string? warning = snapshot.Thermal.Assets.Any(item => item.ProtectiveOutage)
            ? "보호정지 설비 있음 · 공급 경로 제외"
            : snapshot.Thermal.Assets.Any(item => item.State == ThermalOperatingState.Emergency)
                ? "비상 열운전 · 허용시간 감소"
                : snapshot.Thermal.Evaluation.Loads.Any(item => item.DeliveredKw < item.DemandKw)
                    ? "필수 수요 미공급 · 경로 확인"
                    : null;
        RealtimeConnectionRequirementAssessment? connection =
            snapshot.Forecast.ConnectionRequirementAssessment;
        string objective = connection is null
            ? snapshot.Chapter.Content.Objective
            : snapshot.Chapter.Content.Objective + " · 접속 조건 " +
              string.Join(
                  ", ",
                  connection.Facts.Select(item =>
                      $"{RealtimePresentationText.AssetDisplayName(displayWorld, snapshot, item.NodeId)} " +
                      $"{item.CurrentConnections}/{item.RequiredConnections}")) +
              (connection.FrozenForChapter ? " · 시험 시작 시점 고정" : " · 현재 망");
        return new RealtimeTopHudPresentation(
            snapshot.Chapter.Content.DisplayName,
            objective,
            RealtimePresentationText.Time(snapshot.Minute),
            $"운영 자금 {RealtimePresentationText.Cash(snapshot.CashUnit)}",
            reliabilityLabel,
            reliability,
            interaction.PresentedSpeed,
            warning)
        {
            SimulationState = interaction.Simulation,
            Pause = pause,
            ToolShelfVisible = interaction.Surface == RealtimeSurface.Drawer,
            BuildModeActive = !snapshot.CampaignComplete &&
                interaction.Simulation != RealtimeSimulationState.Ended &&
                (snapshot.Construction.NodeDraft is not null ||
                 snapshot.Construction.LineDraft is not null),
        };
    }

    private static RealtimeEventRailPresentation Rail(
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
            bool defaulted = PromiseDefaulted(snapshot, transitionHistory);
            long recordedPromiseUnservedMinutes =
                RecordedPromiseUnservedMinutes(snapshot);
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

    private static RealtimeContextDockPresentation Context(
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
            bool defaulted = PromiseDefaulted(snapshot, transitionHistory);
            long recordedPromiseUnservedMinutes =
                RecordedPromiseUnservedMinutes(snapshot);
            bool promiseRisk = recordedPromiseUnservedMinutes > 0 ||
                baseForecast.Events.Any(item =>
                item.ChapterIndex == snapshot.ChapterIndex &&
                !item.TemporalProjection.Outcome.PromiseSatisfied);
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
                    "북안 수요 의무 제외",
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
                    new("선택", decision,
                        defaulted
                            ? RealtimeTimelineSeverity.Warning
                            : RealtimeTimelineSeverity.Information),
                    new("전망", promiseForecastState,
                        promiseRisk && snapshot.PromiseDecision !=
                            CommercialPromiseDecision.Defer
                                ? RealtimeTimelineSeverity.Critical
                                : RealtimeTimelineSeverity.Information),
                },
                new RealtimeActionPresentation(
                    RealtimeR2Ids.PromiseKeepAction,
                    promise.KeepLabel,
                    actionsEnabled
                        ? "북안 수요를 두 운영 사건의 약속 의무에 포함합니다."
                        : "약속 마감이 지나 선택을 바꿀 수 없습니다.",
                    actionsEnabled),
                new RealtimeActionPresentation(
                    RealtimeR2Ids.PromiseDeferAction,
                    promise.DeferLabel,
                    actionsEnabled
                        ? "북안 수요를 이번 운영 의무에서 제외하고 일정을 연기합니다."
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
                EventContextSections(snapshot, outcome,
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
                EventContextSections(snapshot, outcome,
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
                EventContextSections(snapshot, completed,
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
                        RealtimePresentationText.TransitionHistory(snapshot, asset),
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

    private static RealtimeBuildShelfPresentation BuildShelf(
        RealtimeWorldDefinition realtimeWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeInteractionState interaction,
        bool pointerAccepted,
        string pointerMessage)
    {
        bool ended = snapshot.CampaignComplete ||
            interaction.Simulation == RealtimeSimulationState.Ended;
        RealtimeDraftToolLock? draftToolLock = ended
            ? null
            : RealtimeInteractionReducer.ResolveDraftToolLock(snapshot.Construction);
        bool inspectEnabled = draftToolLock is null;
        var tools = new List<RealtimeBuildToolPresentation>
        {
            new(
                RealtimeR2Ids.InspectTool,
                "선택·검사",
                "I",
                inspectEnabled
                    ? "설비와 사건을 선택해 현재 상태를 확인합니다."
                    : draftToolLock!.RejectionReason,
                inspectEnabled,
                draftToolLock is null && interaction.Tool == RealtimeTool.Inspect),
        };
        foreach (string classId in snapshot.Chapter.Content.AvailableNodeClassIds)
        {
            SpatialNodeClassDefinition nodeClass = snapshot.Construction.World.NodeClasses
                .Single(item => string.Equals(
                    item.ClassId,
                    classId,
                    StringComparison.Ordinal));
            string toolId = RealtimeR2Ids.NodeTool(classId);
            bool enabled = !ended && (draftToolLock is null || string.Equals(
                draftToolLock.RequiredBuildToolId,
                toolId,
                StringComparison.Ordinal));
            string nodeDescription = ended
                ? RealtimeInteractionReducer.CampaignEndedReadOnlyReason
                : !enabled
                ? draftToolLock!.RejectionReason
                : snapshot.Construction.ActiveConstruction is
                ActiveConstructionSnapshot active
                ? $"비교 초안은 만들 수 있습니다. {RealtimePresentationText.Time(active.CompletionMinute)}까지 두 번째 발주는 대기합니다."
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{nodeClass.DisplayName} · 비용 {RealtimePresentationText.Cash(nodeClass.CostCashUnit)} · 공기 {nodeClass.BuildMinutes}분");
            tools.Add(new RealtimeBuildToolPresentation(
                toolId,
                $"{nodeClass.DisplayName} 배치",
                "N",
                nodeDescription,
                enabled,
                !ended && (draftToolLock is not null
                    ? string.Equals(
                        draftToolLock.RequiredBuildToolId,
                        toolId,
                        StringComparison.Ordinal)
                    : interaction.Tool == RealtimeTool.BuildNode &&
                      string.Equals(
                          interaction.SelectedBuildToolId,
                          toolId,
                          StringComparison.Ordinal))));
        }
        foreach (CommercialCampaignLinePlanDefinition plan in
                 snapshot.Chapter.Content.AvailableLinePlans)
        {
            SpatialLineClassDefinition lineClass = snapshot.Construction.World.LineClasses
                .Single(item => string.Equals(
                    item.ClassId,
                    plan.LineClassId,
                    StringComparison.Ordinal));
            SpatialNodeClassDefinition poleClass = snapshot.Construction.World.NodeClasses
                .Single(item => string.Equals(
                    item.ClassId,
                    plan.PoleClassId,
                    StringComparison.Ordinal));
            ThermalProtectionDefinition lineProtection = realtimeWorld.ProtectionFor(
                ThermalAssetKind.Edge,
                plan.LineClassId);
            string toolId = RealtimeR2Ids.LineTool(
                plan.LineClassId,
                plan.PoleClassId);
            bool enabled = !ended && (draftToolLock is null || string.Equals(
                draftToolLock.RequiredBuildToolId,
                toolId,
                StringComparison.Ordinal));
            string lineDescription = ended
                ? RealtimeInteractionReducer.CampaignEndedReadOnlyReason
                : !enabled
                ? draftToolLock!.RejectionReason
                : snapshot.Construction.ActiveConstruction is
                ActiveConstructionSnapshot active
                ? $"비교 경로는 그릴 수 있습니다. {RealtimePresentationText.Time(active.CompletionMinute)}까지 두 번째 발주는 대기합니다."
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{lineClass.DisplayName} · 비용 {RealtimePresentationText.Cash(lineClass.CostCashUnitPerDesignUnit)}/설계단위 · " +
                    $"공기 {lineClass.BuildMinutesPerDesignUnit}분/설계단위 · " +
                    $"연속 {lineProtection.ContinuousKw:N0} kW · {poleClass.DisplayName} 접속부");
            tools.Add(new RealtimeBuildToolPresentation(
                toolId,
                $"{lineClass.DisplayName} 건설",
                "L",
                lineDescription,
                enabled,
                !ended && (draftToolLock is not null
                    ? string.Equals(
                        draftToolLock.RequiredBuildToolId,
                        toolId,
                        StringComparison.Ordinal)
                    : interaction.Tool == RealtimeTool.BuildLine &&
                      string.Equals(
                          interaction.SelectedBuildToolId,
                          toolId,
                          StringComparison.Ordinal))));
        }
        bool analysisEnabled = draftToolLock is null;
        tools.Add(new RealtimeBuildToolPresentation(
            RealtimeR2Ids.AnalysisTool,
            interaction.Tool == RealtimeTool.Analysis
                ? "망 분석 켜짐"
                : "망 분석",
            "A",
            analysisEnabled
                ? interaction.Tool == RealtimeTool.Analysis
                    ? "망 분석 켜짐 · 공급 경로와 첫 병목을 지도 위에 겹쳐 보고 있습니다."
                    : "공급 경로와 첫 병목을 지도 위에 겹쳐 봅니다."
                : draftToolLock!.RejectionReason,
            analysisEnabled,
            draftToolLock is null && interaction.Tool == RealtimeTool.Analysis));
        return new RealtimeBuildShelfPresentation(
            interaction.Surface == RealtimeSurface.Drawer,
            Array.AsReadOnly(tools.ToArray()))
        {
            Guidance = RealtimePresentationText.BuildGuidance(
                snapshot,
                interaction,
                pointerAccepted,
                pointerMessage),
        };
    }

    private static RealtimeActionDockPresentation ActionDock(
        RealtimeCampaignSnapshot snapshot,
        RealtimeInteractionState interaction,
        bool pointerAccepted,
        string pointerMessage,
        RealtimeProjectQuote? nodeOrderQuote,
        RealtimeProjectQuote? lineOrderQuote)
    {
        ConstructionSnapshot construction = snapshot.Construction;
        bool ended = snapshot.CampaignComplete ||
            interaction.Simulation == RealtimeSimulationState.Ended;
        if (ended && (construction.NodeDraft is not null ||
                      construction.LineDraft is not null))
        {
            return new RealtimeActionDockPresentation(
                true,
                "운영 완료 · 읽기 전용 초안",
                RealtimeInteractionReducer.CampaignEndedReadOnlyReason,
                new RealtimeActionPresentation(
                    construction.NodeDraft is not null
                        ? RealtimeR2Ids.OrderNodeAction
                        : RealtimeR2Ids.OrderLineAction,
                    "운영 완료 · 공사 시작 불가",
                    RealtimeInteractionReducer.CampaignEndedReadOnlyReason,
                    false));
        }
        if (construction.NodeDraft is not null)
        {
            bool enabled = OrderQuoteEnabled(snapshot, nodeOrderQuote);
            string quoteDetail = RealtimePresentationText.OrderQuoteDetail(
                snapshot,
                nodeOrderQuote,
                construction.ActiveConstruction);
            return new RealtimeActionDockPresentation(
                true,
                "변전소 초안",
                RealtimePresentationText.FeedbackDetail(pointerAccepted, pointerMessage, quoteDetail),
                new RealtimeActionPresentation(
                    RealtimeR2Ids.OrderNodeAction,
                    "변전소 공사 시작",
                    quoteDetail,
                    enabled));
        }
        if (construction.LineDraft is { EndNodeId: not null })
        {
            bool enabled = OrderQuoteEnabled(snapshot, lineOrderQuote);
            string quoteDetail = RealtimePresentationText.OrderQuoteDetail(
                snapshot,
                lineOrderQuote,
                construction.ActiveConstruction);
            return new RealtimeActionDockPresentation(
                true,
                "선로 초안",
                RealtimePresentationText.FeedbackDetail(pointerAccepted, pointerMessage, quoteDetail),
                new RealtimeActionPresentation(
                    RealtimeR2Ids.OrderLineAction,
                    "선로 공사 시작",
                    quoteDetail,
                    enabled));
        }
        if (construction.ActiveConstruction is ActiveConstructionSnapshot active)
        {
            return new RealtimeActionDockPresentation(
                true,
                "공사 진행",
                $"{RealtimePresentationText.Time(active.CompletionMinute)} 자동 완공 · 두 번째 발주 불가",
                null);
        }
        return new RealtimeActionDockPresentation(false, string.Empty, string.Empty, null);
    }

    private static RealtimePausePresentation Pause(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastSnapshot baseForecast,
        RealtimePauseReason reason)
    {
        // An event already in progress is current operational context, not the
        // next event. Auto-pause and modal copy therefore advance strictly to
        // the first authoritative forecast whose start lies in the future.
        RealtimeForecastEvent? next = baseForecast.Events
            .Where(item => item.StartMinute > snapshot.Minute)
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => RealtimeTimelinePolicy.ForecastPriority(snapshot, item))
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .FirstOrDefault();
        return new RealtimePausePresentation(
            reason,
            snapshot.Minute,
            RealtimePresentationText.Time(snapshot.Minute),
            next?.EventId,
            next?.StartMinute,
            next is null
                ? "예정된 사건 없음"
                : $"{RealtimePresentationText.PlayerEventName(displayWorld, next.OperatingProfile)} · " +
                  RealtimePresentationText.Time(next.StartMinute));
    }

    private static RealtimeWorldHighlight? Highlight(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastSnapshot baseForecast,
        RealtimeComparisonDraftForecast comparisonDraftForecast,
        IReadOnlyList<RealtimeTransition> transitionHistory,
        string? selectedId)
    {
        if (selectedId is null)
        {
            return null;
        }
        if (string.Equals(selectedId, RealtimeR2Ids.ActiveConstructionMarker, StringComparison.Ordinal) &&
            snapshot.Construction.ActiveConstruction is ActiveConstructionSnapshot project)
        {
            return new RealtimeWorldHighlight(
                project.NodeIds,
                project.EdgeIds,
                null,
                $"{RealtimePresentationText.Time(project.CompletionMinute)} 완공 예정 공사 대상");
        }
        if (RealtimeTimelineTargetResolver.TryResolveCompletedConstruction(
                transitionHistory,
                selectedId,
                out RealtimeConstructionCompletion completedConstruction))
        {
            return new RealtimeWorldHighlight(
                completedConstruction.NodeIds,
                completedConstruction.EdgeIds,
                null,
                $"{RealtimePresentationText.Time(completedConstruction.CompletionMinute)} 실제 완공 공사 범위");
        }
        ThermalIntervalEvaluation evaluation = snapshot.Thermal.Evaluation;
        string? mapSelectionId = RealtimeTimelineTargetResolver.MapSelectionId(
            displayWorld,
            snapshot,
            baseForecast,
            comparisonDraftForecast,
            transitionHistory,
            selectedId);
        RealtimeForecastEvent? forecast = baseForecast.Events.FirstOrDefault(item =>
            string.Equals(item.EventId, selectedId, StringComparison.Ordinal));
        if (forecast is not null)
        {
            evaluation = forecast.ProjectedEvaluation;
        }
        if (RealtimeTimelineTargetResolver.TryResolveComparisonEvent(
                comparisonDraftForecast,
                selectedId,
                out RealtimeForecastEvent comparisonEvent))
        {
            forecast = comparisonEvent;
            evaluation = comparisonEvent.ProjectedEvaluation;
        }
        RealtimeEventOutcome? completed = snapshot.CurrentChapterEvents.FirstOrDefault(item =>
            RealtimeTimelineTargetResolver.IsCurrentChapterOutcome(snapshot, item) &&
            string.Equals(item.EventId, selectedId, StringComparison.Ordinal));
        if (completed is not null)
        {
            evaluation = completed.FinalEvaluation;
        }
        if (RealtimeTimelineTargetResolver.TryResolveThermalMarker(
                baseForecast,
                selectedId,
                out RealtimeForecastEvent owningForecast,
                out RealtimeThermalTransition transition))
        {
            forecast = owningForecast;
            evaluation = owningForecast.ProjectedEvaluation;
            mapSelectionId = transition.AssetId;
        }
        if (RealtimeTimelineTargetResolver.TryResolveComparisonThermalMarker(
                comparisonDraftForecast,
                selectedId,
                out RealtimeForecastEvent comparisonOwningForecast,
                out RealtimeThermalTransition comparisonTransition))
        {
            forecast = comparisonOwningForecast;
            evaluation = comparisonOwningForecast.ProjectedEvaluation;
            mapSelectionId = comparisonTransition.AssetId;
        }
        ThermalLoadSupply[] orderedRoutes = evaluation.Loads
            .OrderBy(item => item.LoadId, StringComparer.Ordinal)
            .ToArray();
        ThermalLoadSupply? route = orderedRoutes.FirstOrDefault(item =>
                mapSelectionId is not null &&
                (item.PathNodeIds.Contains(mapSelectionId, StringComparer.Ordinal) ||
                 item.PathEdgeIds.Contains(mapSelectionId, StringComparer.Ordinal) ||
                 string.Equals(item.Failure?.AssetId, mapSelectionId,
                     StringComparison.Ordinal))) ??
            (forecast is not null || completed is not null
                ? orderedRoutes.FirstOrDefault()
                : null);
        if (route is null)
        {
            return null;
        }
        string[] nodeIds = route.PathNodeIds
            .Concat(mapSelectionId is not null && snapshot.Construction.World.Nodes.Any(
                    item => string.Equals(item.NodeId, mapSelectionId,
                        StringComparison.Ordinal))
                ? new[] { mapSelectionId }
                : Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] edgeIds = route.PathEdgeIds
            .Concat(mapSelectionId is not null && snapshot.Construction.World.Edges.Any(
                    item => string.Equals(item.EdgeId, mapSelectionId,
                        StringComparison.Ordinal))
                ? new[] { mapSelectionId }
                : Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new RealtimeWorldHighlight(
            Array.AsReadOnly(nodeIds),
            Array.AsReadOnly(edgeIds),
            route.Failure?.AssetId ??
                (forecast is null && completed is null ? mapSelectionId : null),
            route.Failure is null
                ? $"{RealtimePresentationText.LoadDisplayName(displayWorld, route.LoadId)} 공급 경로"
                : $"{RealtimePresentationText.LoadDisplayName(displayWorld, route.LoadId)} 첫 병목 · " +
                  RealtimePresentationText.FailureKindText(route.Failure.Kind));
    }

    private static RealtimeReliabilityState Reliability(RealtimeCampaignSnapshot snapshot)
    {
        if (snapshot.Thermal.Assets.Any(item => item.ProtectiveOutage))
        {
            return RealtimeReliabilityState.Outage;
        }
        if (snapshot.Thermal.Assets.Any(item => item.State is
                ThermalOperatingState.Emergency or ThermalOperatingState.OverLimit))
        {
            return RealtimeReliabilityState.Emergency;
        }
        return snapshot.Thermal.Evaluation.Loads.Any(item =>
                item.DeliveredKw < item.DemandKw)
            ? RealtimeReliabilityState.Watch
            : RealtimeReliabilityState.Stable;
    }

    private static RealtimeWorldAssetState WorldState(ThermalOperatingState state) =>
        state switch
    {
        ThermalOperatingState.Continuous => RealtimeWorldAssetState.Normal,
        ThermalOperatingState.Emergency => RealtimeWorldAssetState.Emergency,
        ThermalOperatingState.ProtectiveOutage =>
            RealtimeWorldAssetState.ProtectiveOutage,
        ThermalOperatingState.OverLimit => RealtimeWorldAssetState.OverLimit,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static RealtimeWorldAssetStatus WorldStatus(
        string assetId,
        bool commissioned,
        RealtimeThermalAssetSnapshot? thermal)
    {
        RealtimeWorldAssetState state = !commissioned
            ? RealtimeWorldAssetState.Building
            : thermal is null
                ? RealtimeWorldAssetState.Normal
                : thermal.ProtectiveOutage
                    ? RealtimeWorldAssetState.ProtectiveOutage
                    : thermal.AuthoredUnavailable
                        ? RealtimeWorldAssetState.AuthoredUnavailable
                        : WorldState(thermal.State);
        return new RealtimeWorldAssetStatus(
            assetId,
            state,
            thermal?.UsedKw ?? 0,
            thermal?.ContinuousKw ?? 0,
            thermal?.EmergencyKw ?? 0,
            thermal is null ? 0 : ClampInt(thermal.EmergencyExposureMinutes),
            thermal?.EmergencyExposureLimitMinutes ?? 0,
            thermal?.AuthoredUnavailable ?? false,
            thermal?.ProtectiveOutage ?? false);
    }

    private static RealtimeWorldWeather Weather(RealtimeCampaignSnapshot snapshot) =>
        snapshot.ActiveEventStates.Any(item =>
            item.Event.OperatingProfile.ActiveRiskAreaIds.Count > 0)
            ? RealtimeWorldWeather.Storm
            : RealtimeWorldWeather.Clear;

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

    private static IReadOnlyList<RealtimeContextSectionPresentation>
        EventContextSections(
            RealtimeCampaignSnapshot snapshot,
            RealtimeEventOutcome outcome,
            IReadOnlyList<RealtimeContextSectionPresentation> baseSections,
            string prefix)
    {
        if (snapshot.Chapter.Content.CityPromise is null)
        {
            return baseSections;
        }
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
                $"{prefix}Defer · 북안 수요 의무 제외",
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

    private static bool PromiseDefaulted(
        RealtimeCampaignSnapshot snapshot,
        IReadOnlyList<RealtimeTransition> transitionHistory) =>
        transitionHistory.Any(item =>
            item.Kind == RealtimeTransitionKind.PromiseDefaulted &&
            string.Equals(
                item.ChapterId,
                snapshot.Chapter.Content.ChapterId,
                StringComparison.Ordinal));

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

    private static long RecordedPromiseUnservedMinutes(
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

    private static bool OrderQuoteEnabled(
        RealtimeCampaignSnapshot snapshot,
        RealtimeProjectQuote? quote) => quote is
        {
            Accepted: true,
            CostCashUnit: not null,
            BuildMinutes: not null,
            CompletionMinute: not null,
        } && quote.CostCashUnit.Value <= snapshot.CashUnit;

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

    private static int ClampInt(long value) => value > int.MaxValue
        ? int.MaxValue
        : value < int.MinValue
            ? int.MinValue
            : (int)value;
}
