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

internal enum RealtimeTimelineTargetKind
{
    Event,
    Decision,
    ThermalAsset,
    ConstructionProject,
    Asset,
    Unknown,
}

internal sealed record RealtimeTimelineTarget(
    string MarkerId,
    RealtimeTimelineTargetKind Kind,
    string? SubjectId,
    string? MapSubjectId);

internal static class RealtimeSlicePresenter
{
    private const long DefaultHorizonMinutes =
        RealtimeCampaignRun.DefaultForecastHorizonMinutes;
    private const long TimelineHistoryMinutes = 6 * 60;
    private const int TimelineHistoryLimit = 3;
    private const string ActiveConstructionMarkerId = "ACTIVE_CONSTRUCTION";
    private const string DraftConstructionMarkerId = "DRAFT_CONSTRUCTION";
    private const string CompletedConstructionMarkerPrefix = "COMPLETED_CONSTRUCTION:";
    internal const string PromiseDecisionMarkerPrefix = "PROMISE_DEADLINE:";
    internal const string PromiseKeepActionId = "PROMISE_KEEP";
    internal const string PromiseDeferActionId = "PROMISE_DEFER";

    internal static RealtimeSlicePresentation Present(
        CommercialWorldDefinition displayWorld,
        RealtimeWorldDefinition realtimeWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeComparisonDraftForecast comparisonDraftForecast,
        RealtimeInteractionState interaction,
        long revision,
        CoreMapPoint? pointerPoint = null,
        bool pointerAccepted = true,
        string pointerMessage = "",
        bool reduceMotion = false,
        RealtimeProjectQuote? nodeOrderQuote = null,
        RealtimeProjectQuote? lineOrderQuote = null,
        IReadOnlyList<RealtimeTransition>? transitionHistory = null) => Present(
        displayWorld,
        realtimeWorld,
        snapshot,
        snapshot.Forecast,
        comparisonDraftForecast,
        interaction,
        revision,
        pointerPoint,
        pointerAccepted,
        pointerMessage,
        reduceMotion,
        nodeOrderQuote,
        lineOrderQuote,
        transitionHistory);

    internal static RealtimeSlicePresentation Present(
        CommercialWorldDefinition displayWorld,
        RealtimeWorldDefinition realtimeWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastSnapshot baseForecast,
        RealtimeComparisonDraftForecast comparisonDraftForecast,
        RealtimeInteractionState interaction,
        long revision,
        CoreMapPoint? pointerPoint = null,
        bool pointerAccepted = true,
        string pointerMessage = "",
        bool reduceMotion = false,
        RealtimeProjectQuote? nodeOrderQuote = null,
        RealtimeProjectQuote? lineOrderQuote = null,
        IReadOnlyList<RealtimeTransition>? transitionHistory = null)
    {
        ArgumentNullException.ThrowIfNull(displayWorld);
        ArgumentNullException.ThrowIfNull(realtimeWorld);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(baseForecast);
        ArgumentNullException.ThrowIfNull(comparisonDraftForecast);
        ArgumentNullException.ThrowIfNull(interaction);
        IReadOnlyList<RealtimeTransition> history = Array.AsReadOnly(
            (transitionHistory ?? Array.Empty<RealtimeTransition>()).ToArray());

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
            nodeOrderQuote,
            lineOrderQuote,
            history);
        return new RealtimeSlicePresentation(
            revision,
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
                reduceMotion,
                history),
            new RealtimeWorldPointerFeedback(
                pointerPoint,
                pointerAccepted,
                pointerMessage),
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
                nodeOrderQuote,
                lineOrderQuote,
                history),
            BuildShelf(
                realtimeWorld,
                snapshot,
                interaction,
                pointerAccepted,
                pointerMessage),
            ActionDock(
                snapshot,
                interaction,
                pointerAccepted,
                pointerMessage,
                nodeOrderQuote,
                lineOrderQuote),
            Modal(snapshot, interaction, pause));
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
                Guidance = BuildGuidance(
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

    internal static RealtimeTimelineTarget ResolveTimelineTarget(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        string markerId) => ResolveTimelineTarget(
        displayWorld,
        snapshot,
        snapshot.Forecast,
        new RealtimeComparisonDraftForecast(false, null, null),
        markerId);

    internal static RealtimeTimelineTarget ResolveTimelineTarget(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeComparisonDraftForecast comparisonDraftForecast,
        string markerId) => ResolveTimelineTarget(
        displayWorld,
        snapshot,
        snapshot.Forecast,
        comparisonDraftForecast,
        markerId);

    internal static RealtimeTimelineTarget ResolveTimelineTarget(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastSnapshot baseForecast,
        RealtimeComparisonDraftForecast comparisonDraftForecast,
        string markerId) => ResolveTimelineTarget(
        displayWorld,
        snapshot,
        baseForecast,
        comparisonDraftForecast,
        Array.Empty<RealtimeTransition>(),
        markerId);

    internal static RealtimeTimelineTarget ResolveTimelineTarget(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastSnapshot baseForecast,
        RealtimeComparisonDraftForecast comparisonDraftForecast,
        IReadOnlyList<RealtimeTransition> transitionHistory,
        string markerId)
    {
        ArgumentNullException.ThrowIfNull(displayWorld);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(baseForecast);
        ArgumentNullException.ThrowIfNull(comparisonDraftForecast);
        ArgumentNullException.ThrowIfNull(transitionHistory);
        ArgumentException.ThrowIfNullOrWhiteSpace(markerId);
        if (IsPromiseDecisionMarker(snapshot, markerId))
        {
            return new RealtimeTimelineTarget(
                markerId,
                RealtimeTimelineTargetKind.Decision,
                markerId,
                null);
        }
        if (TryResolveComparisonEvent(
                comparisonDraftForecast,
                markerId,
                out RealtimeForecastEvent comparisonEvent))
        {
            return new RealtimeTimelineTarget(
                markerId,
                RealtimeTimelineTargetKind.Event,
                markerId,
                EventMapSubject(
                    displayWorld,
                    snapshot,
                    comparisonEvent.ProjectedEvaluation,
                    comparisonEvent.OperatingProfile));
        }
        if (TryResolveComparisonThermalMarker(
                comparisonDraftForecast,
                markerId,
                out _,
                out RealtimeThermalTransition comparisonTransition))
        {
            return new RealtimeTimelineTarget(
                markerId,
                RealtimeTimelineTargetKind.ThermalAsset,
                markerId,
                comparisonTransition.AssetId);
        }
        RealtimeForecastEvent? selectedForecast = baseForecast.Events.FirstOrDefault(item =>
            string.Equals(item.EventId, markerId, StringComparison.Ordinal));
        if (selectedForecast is not null)
        {
            return new RealtimeTimelineTarget(
                markerId,
                RealtimeTimelineTargetKind.Event,
                markerId,
                EventMapSubject(
                    displayWorld,
                    snapshot,
                    selectedForecast.ProjectedEvaluation,
                    selectedForecast.OperatingProfile));
        }
        foreach (RealtimeForecastEvent forecast in baseForecast.Events)
        {
            foreach (RealtimeThermalTransition transition in
                     forecast.TemporalProjection.Transitions)
            {
                if (string.Equals(
                        ThermalMarkerId(forecast.EventId, transition),
                        markerId,
                        StringComparison.Ordinal))
                {
                    return new RealtimeTimelineTarget(
                        markerId,
                        RealtimeTimelineTargetKind.ThermalAsset,
                        markerId,
                        transition.AssetId);
                }
            }
        }
        RealtimeEventOutcome? selectedOutcome = snapshot.CurrentChapterEvents.FirstOrDefault(item =>
            IsCurrentChapterOutcome(snapshot, item) &&
            string.Equals(item.EventId, markerId, StringComparison.Ordinal));
        if (selectedOutcome is not null)
        {
            RealtimeScheduledEventDefinition scheduled = snapshot.Chapter.ScheduledEvents
                .Single(item => string.Equals(
                    item.EventId,
                    selectedOutcome.EventId,
                    StringComparison.Ordinal));
            return new RealtimeTimelineTarget(
                markerId,
                RealtimeTimelineTargetKind.Event,
                markerId,
                EventMapSubject(
                    displayWorld,
                    snapshot,
                    selectedOutcome.FinalEvaluation,
                    scheduled.OperatingProfile));
        }
        if (string.Equals(markerId, ActiveConstructionMarkerId, StringComparison.Ordinal))
        {
            return new RealtimeTimelineTarget(
                markerId,
                RealtimeTimelineTargetKind.ConstructionProject,
                snapshot.Construction.ActiveConstruction is null
                    ? null
                    : markerId,
                null);
        }
        if (string.Equals(markerId, DraftConstructionMarkerId, StringComparison.Ordinal))
        {
            bool hasDraft = !snapshot.CampaignComplete &&
                (snapshot.Construction.NodeDraft is not null ||
                    snapshot.Construction.LineDraft is { EndNodeId: not null });
            return new RealtimeTimelineTarget(
                markerId,
                RealtimeTimelineTargetKind.ConstructionProject,
                hasDraft ? markerId : null,
                null);
        }
        if (TryResolveCompletedConstruction(
                transitionHistory,
                markerId,
                out RealtimeConstructionCompletion completedConstruction))
        {
            string? mapSubjectId = completedConstruction.EdgeIds
                .Concat(completedConstruction.NodeIds)
                .FirstOrDefault(id => snapshot.Construction.World.Edges.Any(item =>
                        string.Equals(item.EdgeId, id, StringComparison.Ordinal)) ||
                    snapshot.Construction.World.Nodes.Any(item =>
                        string.Equals(item.NodeId, id, StringComparison.Ordinal)));
            return new RealtimeTimelineTarget(
                markerId,
                RealtimeTimelineTargetKind.ConstructionProject,
                markerId,
                mapSubjectId);
        }
        if (snapshot.Construction.World.Nodes.Any(item => string.Equals(
                item.NodeId,
                markerId,
                StringComparison.Ordinal)) ||
            snapshot.Construction.World.Edges.Any(item => string.Equals(
                item.EdgeId,
                markerId,
                StringComparison.Ordinal)))
        {
            return new RealtimeTimelineTarget(
                markerId,
                RealtimeTimelineTargetKind.Asset,
                markerId,
                markerId);
        }
        return new RealtimeTimelineTarget(
            markerId,
            RealtimeTimelineTargetKind.Unknown,
            null,
            null);
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
            MapSelectionId(
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
                      $"{AssetDisplayName(displayWorld, snapshot, item.NodeId)} " +
                      $"{item.CurrentConnections}/{item.RequiredConnections}")) +
              (connection.FrozenForChapter ? " · 시험 시작 시점 고정" : " · 현재 망");
        return new RealtimeTopHudPresentation(
            snapshot.Chapter.Content.DisplayName,
            objective,
            Time(snapshot.Minute),
            $"운영 자금 {Cash(snapshot.CashUnit)}",
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
                PromiseDecisionMarkerId(promise.PromiseId),
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
                TimeLabel = Clock(deadline),
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
            string eventName = PlayerEventName(displayWorld, item.OperatingProfile);
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
                Priority = ForecastPriority(snapshot, item),
                TimeLabel = Time(item.StartMinute),
                EndTimeLabel = Time(item.EndMinute),
                SeverityLabel = EventRiskLabel(
                    snapshot,
                    item.TemporalProjection.Outcome,
                    "예고"),
            });

            foreach (RealtimeThermalTransition transition in
                     item.TemporalProjection.Transitions)
            {
                string id = ThermalMarkerId(item.EventId, transition);
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
                    ThermalTitle(displayWorld, snapshot, transition),
                    AssetDisplayName(displayWorld, snapshot, transition.AssetId),
                    $"{eventName} 예상 · {ThermalTitle(displayWorld, snapshot, transition)}",
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
                    TimeLabel = Time(transition.Minute),
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
                string eventName = PlayerEventName(displayWorld, item.OperatingProfile);
                string markerId = ComparisonEventMarkerId(item.EventId);
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
                    Priority = ForecastPriority(snapshot, item),
                    TimeLabel = Time(item.StartMinute),
                    EndTimeLabel = Time(item.EndMinute),
                    SeverityLabel = "현재 초안 기준 예상 · " + EventRiskLabel(
                        snapshot,
                        item.TemporalProjection.Outcome,
                        "예고"),
                    SourceKind = RealtimeTimelineSourceKind.Draft,
                });

                foreach (RealtimeThermalTransition transition in
                         item.TemporalProjection.Transitions)
                {
                    string id = ComparisonThermalMarkerId(item.EventId, transition);
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
                    string thermalTitle = ThermalTitle(displayWorld, snapshot, transition);
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
                        TimeLabel = Time(transition.Minute),
                        SeverityLabel = trip
                            ? "현재 초안 기준 예상 · 보호정지"
                            : "현재 초안 기준 예상 · 열 상태 변화",
                        SourceKind = RealtimeTimelineSourceKind.Draft,
                    });
                }
            }
        }

        foreach (RealtimeEventOutcome outcome in snapshot.CurrentChapterEvents
                     .Where(item => IsCurrentChapterOutcome(snapshot, item))
                     .Where(item => item.EndMinute <= snapshot.Minute)
                     .OrderByDescending(item => item.EndMinute)
                     .ThenBy(item => item.EventId, StringComparer.Ordinal)
                     .Take(TimelineHistoryLimit)
                     .OrderBy(item => item.EndMinute)
                     .ThenBy(item => item.EventId, StringComparer.Ordinal))
        {
            RealtimeScheduledEventDefinition scheduled = snapshot.Chapter.ScheduledEvents
                .Single(item => string.Equals(
                    item.EventId,
                    outcome.EventId,
                    StringComparison.Ordinal));
            string eventName = PlayerEventName(displayWorld, scheduled.OperatingProfile);
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
                TimeLabel = Time(outcome.StartMinute),
                EndTimeLabel = Time(outcome.EndMinute),
                SeverityLabel = outcome.SafetySatisfied && outcome.PromiseSatisfied
                    ? "운영 기록 완료"
                    : EventRiskLabel(snapshot, outcome, "운영 위험") +
                      " · 기록 완료",
            });
        }

        if (snapshot.Construction.ActiveConstruction is ActiveConstructionSnapshot project)
        {
            items.Add(new RealtimeTimelineItemPresentation(
                ActiveConstructionMarkerId,
                RealtimeTimelineItemKind.Construction,
                project.CompletionMinute,
                null,
                project.Kind == ConstructionKind.Line
                    ? "실제 선로 공사"
                    : "실제 변전소 공사",
                "실제 공사 완공",
                $"발주된 실제 공사 · {Time(project.CompletionMinute)}에 완공 즉시 공급에 참여",
                RealtimeTimelineSeverity.Information,
                RealtimeTimelineVisibility.Active,
                true,
                true)
            {
                Lane = RealtimeTimelineLane.Construction,
                Priority = int.MinValue,
                TimeLabel = Time(project.CompletionMinute),
                SeverityLabel = "공사 중",
            });
        }

        foreach (RealtimeTransition transition in transitionHistory
                     .Where(item => item.Kind ==
                         RealtimeTransitionKind.ConstructionCompleted &&
                         item.Construction is not null &&
                         item.Minute <= snapshot.Minute)
                     .OrderByDescending(item => item.Minute)
                     .ThenBy(item => CompletedConstructionMarkerId(item.Construction!),
                         StringComparer.Ordinal)
                     .Take(TimelineHistoryLimit)
                     .OrderBy(item => item.Minute)
                     .ThenBy(item => CompletedConstructionMarkerId(item.Construction!),
                         StringComparer.Ordinal))
        {
            RealtimeConstructionCompletion completion = transition.Construction!;
            items.Add(new RealtimeTimelineItemPresentation(
                CompletedConstructionMarkerId(completion),
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
                TimeLabel = Time(completion.CompletionMinute),
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
                DraftConstructionMarkerId,
                RealtimeTimelineItemKind.Construction,
                draftCompletionMinute,
                null,
                lineDraft ? "선로 초안 완공 예상" : "변전소 초안 완공 예상",
                "초안 완공 예상",
                $"아직 발주되지 않은 초안 · 발주하면 {draftBuildMinutes}분 뒤 " +
                $"{Time(draftCompletionMinute)} 완공 예상",
                RealtimeTimelineSeverity.Advisory,
                RealtimeTimelineVisibility.Announced,
                false,
                true)
            {
                Lane = RealtimeTimelineLane.Construction,
                Priority = int.MinValue + 2,
                TimeLabel = Time(draftCompletionMinute),
                SeverityLabel = "초안 예상 · 미발주",
                SourceKind = RealtimeTimelineSourceKind.Draft,
            });
        }

        long horizonMinutes = HorizonMinutes(interaction.TimelineHorizon);
        long anchor = interaction.TimelineAnchorMinute ?? snapshot.Minute;
        long horizonStart = Math.Max(0, anchor - TimelineHistoryMinutes);
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
            .ThenBy(item => ForecastPriority(snapshot, item))
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
                    Countdown(checked(nextDecision.StartMinute - snapshot.Minute)),
                    $"마감 {Time(nextDecision.StartMinute)}",
                    $"마감 {Clock(nextDecision.StartMinute)}")
                : nextEvent is null
                    ? null
                    : new RealtimeNextEventPresentation(
                nextEvent.EventId,
                nextEvent.StartMinute,
                nextEvent.EndMinute,
                checked(nextEvent.StartMinute - snapshot.Minute),
                PlayerEventName(displayWorld, nextEvent.OperatingProfile),
                Countdown(checked(nextEvent.StartMinute - snapshot.Minute)),
                $"시작 {Time(nextEvent.StartMinute)} · 종료 {Time(nextEvent.EndMinute)}",
                $"{Clock(nextEvent.StartMinute)}→{Clock(nextEvent.EndMinute)}");
        return new RealtimeEventRailPresentation(
            snapshot.Minute,
            horizonStart,
            horizonEnd,
            Time(snapshot.Minute),
            $"{HorizonLabel(interaction.TimelineHorizon)} · 지난 6시간",
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

        if (IsPromiseDecisionMarker(snapshot, selectedId) &&
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
                    new("시각", Clock(deadline),
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
                    PromiseKeepActionId,
                    promise.KeepLabel,
                    actionsEnabled
                        ? "북안 수요를 두 운영 사건의 약속 의무에 포함합니다."
                        : "약속 마감이 지나 선택을 바꿀 수 없습니다.",
                    actionsEnabled),
                new RealtimeActionPresentation(
                    PromiseDeferActionId,
                    promise.DeferLabel,
                    actionsEnabled
                        ? "북안 수요를 이번 운영 의무에서 제외하고 일정을 연기합니다."
                        : "약속 마감이 지나 선택을 바꿀 수 없습니다.",
                    actionsEnabled,
                    RealtimeActionTone.Secondary));
        }

        if (string.Equals(selectedId, ActiveConstructionMarkerId, StringComparison.Ordinal) &&
            snapshot.Construction.ActiveConstruction is ActiveConstructionSnapshot project)
        {
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                "실제 공사 중",
                project.Kind == ConstructionKind.Line ? "선로 공사" : "변전소 공사",
                new RealtimeContextSectionPresentation[]
                {
                    new("완공", Time(project.CompletionMinute)),
                    new("비용", Cash(project.CostCashUnit)),
                    new("범위", $"설비 {project.NodeIds.Count}곳 · 선로 {project.EdgeIds.Count}구간"),
                })
            {
                Details = new RealtimeContextDetailPresentation[]
                {
                    new(
                        RealtimeContextDetailTab.Route,
                        "공사 범위",
                        $"설비: {JoinAssetNames(displayWorld, snapshot, project.NodeIds)}\n" +
                        $"선로: {JoinAssetNames(displayWorld, snapshot, project.EdgeIds)}",
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
            string.Equals(selectedId, DraftConstructionMarkerId, StringComparison.Ordinal) &&
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
                    new("발주 시 완공", Time(draftCompletionMinute)),
                    new("예상 공기", $"{draftBuildMinutes}분"),
                    new("발주 비용", Cash(draftCost)),
                });
        }

        if (TryResolveCompletedConstruction(
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
                    new("실제 완공", Time(completedConstruction.CompletionMinute)),
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
                        $"설비: {JoinAssetNames(displayWorld, snapshot, completedConstruction.NodeIds)}\n" +
                        $"선로: {JoinAssetNames(displayWorld, snapshot, completedConstruction.EdgeIds)}",
                        RealtimeTimelineSeverity.Information),
                },
            };
        }

        if (TryResolveComparisonEvent(
                comparisonDraftForecast,
                selectedId,
                out RealtimeForecastEvent comparisonEvent))
        {
            RealtimeEventOutcome outcome = comparisonEvent.TemporalProjection.Outcome;
            string eventName = PlayerEventName(
                displayWorld,
                comparisonEvent.OperatingProfile);
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                "현재 초안 기준 예상",
                eventName,
                EventContextSections(snapshot, outcome,
                [
                    new("발생", $"{Time(comparisonEvent.StartMinute)}–{Time(comparisonEvent.EndMinute)}"),
                    new("안전 의무", outcome.SafetySatisfied
                        ? "현재 초안 기준 예상 · 공급 충족"
                        : $"현재 초안 기준 예상 · {outcome.SafetyUnservedMinutes}분 미공급",
                        outcome.SafetySatisfied
                            ? RealtimeTimelineSeverity.Information
                            : RealtimeTimelineSeverity.Critical),
                    new("첫 병목", FirstBottleneck(
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
                        ForecastDetail(displayWorld, snapshot, comparisonEvent),
                        outcome.SafetySatisfied && outcome.PromiseSatisfied
                            ? RealtimeTimelineSeverity.Advisory
                            : RealtimeTimelineSeverity.Critical),
                },
            };
        }

        if (TryResolveComparisonThermalMarker(
                comparisonDraftForecast,
                selectedId,
                out RealtimeForecastEvent comparisonOwningForecast,
                out RealtimeThermalTransition comparisonTransition))
        {
            string eventName = PlayerEventName(
                displayWorld,
                comparisonOwningForecast.OperatingProfile);
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                "현재 초안 기준 예상 · 열 보호",
                AssetDisplayName(displayWorld, snapshot, comparisonTransition.AssetId),
                new RealtimeContextSectionPresentation[]
                {
                    new("예상 시각", Time(comparisonTransition.Minute)),
                    new("예상 변화", ThermalTitle(
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
                        ForecastDetail(
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
            string eventName = PlayerEventName(displayWorld, forecast.OperatingProfile);
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                "사건 지평선",
                eventName,
                EventContextSections(snapshot, outcome,
                [
                    new("발생", $"{Time(forecast.StartMinute)}–{Time(forecast.EndMinute)}"),
                    new("안전 의무", outcome.SafetySatisfied
                        ? "예상 공급 충족"
                        : $"{outcome.SafetyUnservedMinutes}분 미공급 예상",
                        outcome.SafetySatisfied
                            ? RealtimeTimelineSeverity.Information
                            : RealtimeTimelineSeverity.Critical),
                    new("첫 병목", FirstBottleneck(
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
                        ForecastRoutes(displayWorld, snapshot, forecast.ProjectedEvaluation),
                        outcome.SafetySatisfied && outcome.PromiseSatisfied
                            ? RealtimeTimelineSeverity.Information
                            : RealtimeTimelineSeverity.Critical),
                    new(
                        RealtimeContextDetailTab.Forecast,
                        "시간별 변화",
                        ForecastDetail(displayWorld, snapshot, forecast),
                        outcome.SafetySatisfied && outcome.PromiseSatisfied
                            ? RealtimeTimelineSeverity.Advisory
                            : RealtimeTimelineSeverity.Critical),
                },
            };
        }

        RealtimeEventOutcome? completed = snapshot.CurrentChapterEvents.FirstOrDefault(item =>
            IsCurrentChapterOutcome(snapshot, item) &&
            string.Equals(item.EventId, selectedId, StringComparison.Ordinal));
        if (completed is not null)
        {
            RealtimeScheduledEventDefinition scheduled = snapshot.Chapter.ScheduledEvents
                .Single(item => string.Equals(
                    item.EventId,
                    completed.EventId,
                    StringComparison.Ordinal));
            string eventName = PlayerEventName(displayWorld, scheduled.OperatingProfile);
            return new RealtimeContextDockPresentation(
                selectedId,
                true,
                "최근 운영 기록",
                eventName,
                EventContextSections(snapshot, completed,
                [
                    new("운영 시각", $"{Time(completed.StartMinute)}–{Time(completed.EndMinute)}"),
                    new("안전 의무", completed.SafetySatisfied
                        ? "공급 충족"
                        : $"{completed.SafetyUnservedMinutes}분 미공급",
                        completed.SafetySatisfied
                            ? RealtimeTimelineSeverity.Information
                            : RealtimeTimelineSeverity.Critical),
                    new("첫 병목", FirstBottleneck(
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
                        ForecastRoutes(displayWorld, snapshot, completed.FinalEvaluation),
                        completed.SafetySatisfied && completed.PromiseSatisfied
                            ? RealtimeTimelineSeverity.Information
                            : RealtimeTimelineSeverity.Critical),
                },
            };
        }

        if (TryResolveThermalMarker(
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
            string eventName = PlayerEventName(displayWorld, owningForecast.OperatingProfile);
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
                AssetDisplayName(displayWorld, snapshot, selectedTransition.AssetId),
                new RealtimeContextSectionPresentation[]
                {
                    new("예상 시각", Time(selectedTransition.Minute)),
                    new("예상 변화", ThermalTitle(displayWorld, snapshot, selectedTransition),
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
                        ForecastDetail(displayWorld, snapshot, owningForecast),
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
            string constructionHeading = AssetDisplayName(
                displayWorld,
                snapshot,
                selectedId);
            string constructionBody = node is not null
                ? $"{NodeClassDisplayName(snapshot, node.ClassId)} · 공사 중 · 완공 전 공급 불가"
                : $"{LineClassDisplayName(snapshot, edge!.LineClassId)} · 공사 중 · 완공 전 공급 불가";
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
                ThermalAssetDisplayKind(snapshot, asset),
                AssetDisplayName(displayWorld, snapshot, selectedId),
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
                        TransitionHistory(snapshot, asset),
                        RealtimeTimelineSeverity.Advisory),
                },
            };
        }

        string heading = AssetDisplayName(displayWorld, snapshot, selectedId);
        string body = node is not null
            ? $"{NodeClassDisplayName(snapshot, node.ClassId)} · " +
              $"{(node.Commissioned ? "사용 가능" : "공사 중")}"
            : edge is not null
                ? $"{LineClassDisplayName(snapshot, edge.LineClassId)} · " +
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
                "TOOL:INSPECT",
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
            string toolId = $"NODE:{classId}";
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
                ? $"비교 초안은 만들 수 있습니다. {Time(active.CompletionMinute)}까지 두 번째 발주는 대기합니다."
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{nodeClass.DisplayName} · 비용 {Cash(nodeClass.CostCashUnit)} · 공기 {nodeClass.BuildMinutes}분");
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
            string toolId = $"LINE:{plan.LineClassId}:{plan.PoleClassId}";
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
                ? $"비교 경로는 그릴 수 있습니다. {Time(active.CompletionMinute)}까지 두 번째 발주는 대기합니다."
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{lineClass.DisplayName} · 비용 {Cash(lineClass.CostCashUnitPerDesignUnit)}/설계단위 · " +
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
            "TOOL:ANALYSIS",
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
            Guidance = BuildGuidance(
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
                        ? "ORDER_NODE"
                        : "ORDER_LINE",
                    "운영 완료 · 공사 시작 불가",
                    RealtimeInteractionReducer.CampaignEndedReadOnlyReason,
                    false));
        }
        if (construction.NodeDraft is not null)
        {
            bool enabled = OrderQuoteEnabled(snapshot, nodeOrderQuote);
            string quoteDetail = OrderQuoteDetail(
                snapshot,
                nodeOrderQuote,
                construction.ActiveConstruction);
            return new RealtimeActionDockPresentation(
                true,
                "변전소 초안",
                FeedbackDetail(pointerAccepted, pointerMessage, quoteDetail),
                new RealtimeActionPresentation(
                    "ORDER_NODE",
                    "변전소 공사 시작",
                    quoteDetail,
                    enabled));
        }
        if (construction.LineDraft is { EndNodeId: not null })
        {
            bool enabled = OrderQuoteEnabled(snapshot, lineOrderQuote);
            string quoteDetail = OrderQuoteDetail(
                snapshot,
                lineOrderQuote,
                construction.ActiveConstruction);
            return new RealtimeActionDockPresentation(
                true,
                "선로 초안",
                FeedbackDetail(pointerAccepted, pointerMessage, quoteDetail),
                new RealtimeActionPresentation(
                    "ORDER_LINE",
                    "선로 공사 시작",
                    quoteDetail,
                    enabled));
        }
        if (construction.ActiveConstruction is ActiveConstructionSnapshot active)
        {
            return new RealtimeActionDockPresentation(
                true,
                "공사 진행",
                $"{Time(active.CompletionMinute)} 자동 완공 · 두 번째 발주 불가",
                null);
        }
        return new RealtimeActionDockPresentation(false, string.Empty, string.Empty, null);
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
                "CAMPAIGN_RESULT",
                StringComparison.Ordinal))
        {
            RealtimeChapterOutcome? outcome = snapshot.CompletedChapters.LastOrDefault();
            int satisfied = outcome?.Events.Count(item => item.SafetySatisfied) ??
                snapshot.CurrentChapterEvents.Count(item => item.SafetySatisfied);
            int total = outcome?.Events.Count ?? snapshot.CurrentChapterEvents.Count;
            string resultBody = outcome is null
                ? $"운영 종료 시각 {Time(snapshot.Minute)} · 최종 운영 자금 " +
                  Cash(snapshot.CashUnit)
                : $"{snapshot.Chapter.Content.DisplayName} 운영 완료 · " +
                  $"안전 의무 {satisfied}/{total} 충족 · " +
                  $"최종 운영 자금 {Cash(outcome.EndingCashUnit)}";
            return new RealtimeModalPresentation(
                interaction.ActiveModalId,
                interaction.ActiveModalKind.Value,
                "운영 결과",
                snapshot.CampaignComplete ? "캠페인 운영 완료" : "장 운영 완료",
                resultBody,
                new RealtimeActionPresentation(
                    "RESULT_CLOSE",
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
                    "BRIEFING_CONTINUE",
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
                "NOTICE_CLOSE",
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
            .ThenBy(item => ForecastPriority(snapshot, item))
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .FirstOrDefault();
        return new RealtimePausePresentation(
            reason,
            snapshot.Minute,
            Time(snapshot.Minute),
            next?.EventId,
            next?.StartMinute,
            next is null
                ? "예정된 사건 없음"
                : $"{PlayerEventName(displayWorld, next.OperatingProfile)} · " +
                  Time(next.StartMinute));
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
        if (string.Equals(selectedId, ActiveConstructionMarkerId, StringComparison.Ordinal) &&
            snapshot.Construction.ActiveConstruction is ActiveConstructionSnapshot project)
        {
            return new RealtimeWorldHighlight(
                project.NodeIds,
                project.EdgeIds,
                null,
                $"{Time(project.CompletionMinute)} 완공 예정 공사 대상");
        }
        if (TryResolveCompletedConstruction(
                transitionHistory,
                selectedId,
                out RealtimeConstructionCompletion completedConstruction))
        {
            return new RealtimeWorldHighlight(
                completedConstruction.NodeIds,
                completedConstruction.EdgeIds,
                null,
                $"{Time(completedConstruction.CompletionMinute)} 실제 완공 공사 범위");
        }
        ThermalIntervalEvaluation evaluation = snapshot.Thermal.Evaluation;
        string? mapSelectionId = MapSelectionId(
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
        if (TryResolveComparisonEvent(
                comparisonDraftForecast,
                selectedId,
                out RealtimeForecastEvent comparisonEvent))
        {
            forecast = comparisonEvent;
            evaluation = comparisonEvent.ProjectedEvaluation;
        }
        RealtimeEventOutcome? completed = snapshot.CurrentChapterEvents.FirstOrDefault(item =>
            IsCurrentChapterOutcome(snapshot, item) &&
            string.Equals(item.EventId, selectedId, StringComparison.Ordinal));
        if (completed is not null)
        {
            evaluation = completed.FinalEvaluation;
        }
        if (TryResolveThermalMarker(
                baseForecast,
                selectedId,
                out RealtimeForecastEvent owningForecast,
                out RealtimeThermalTransition transition))
        {
            forecast = owningForecast;
            evaluation = owningForecast.ProjectedEvaluation;
            mapSelectionId = transition.AssetId;
        }
        if (TryResolveComparisonThermalMarker(
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
                ? $"{LoadDisplayName(displayWorld, route.LoadId)} 공급 경로"
                : $"{LoadDisplayName(displayWorld, route.LoadId)} 첫 병목 · " +
                  FailureKindText(route.Failure.Kind));
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
        return $"{eventName} · {Time(item.StartMinute)} · {safety}{promise}";
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
        return $"{Time(outcome.EndMinute)} 종료 · {safety}{promise}";
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

    private static string PromiseDecisionMarkerId(string promiseId) =>
        $"{PromiseDecisionMarkerPrefix}{promiseId}";

    private static bool IsCurrentChapterOutcome(
        RealtimeCampaignSnapshot snapshot,
        RealtimeEventOutcome outcome) => string.Equals(
            outcome.ChapterId,
            snapshot.Chapter.Content.ChapterId,
            StringComparison.Ordinal);

    private static bool IsPromiseDecisionMarker(
        RealtimeCampaignSnapshot snapshot,
        string markerId) => snapshot.Chapter.Content.CityPromise is
            CommercialCityPromiseDefinition promise &&
        string.Equals(
            markerId,
            PromiseDecisionMarkerId(promise.PromiseId),
            StringComparison.Ordinal);

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
        return $"{promise.DisplayName} · 마감 {Time(deadline)} · {state} · " +
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
            .Where(item => IsCurrentChapterOutcome(snapshot, item))
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

    private static int ForecastPriority(
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastEvent forecast) =>
        forecast.ChapterIndex == snapshot.ChapterIndex
            ? snapshot.Chapter.ScheduledEvents.FirstOrDefault(item => string.Equals(
                    item.EventId,
                    forecast.EventId,
                    StringComparison.Ordinal))?.Priority ?? int.MaxValue
            : int.MaxValue;

    private static long HorizonMinutes(RealtimeTimelineHorizonPreset preset) => preset switch
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

    private static string HorizonLabel(RealtimeTimelineHorizonPreset preset) => preset switch
    {
        RealtimeTimelineHorizonPreset.SixHours => "앞으로 6시간",
        RealtimeTimelineHorizonPreset.TwentyFourHours => "앞으로 24시간",
        RealtimeTimelineHorizonPreset.SevenDays => "앞으로 7일",
        _ => throw new ArgumentOutOfRangeException(nameof(preset)),
    };

    private static string ForecastRoutes(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        ThermalIntervalEvaluation evaluation)
    {
        if (evaluation.Loads.Count == 0)
        {
            return "예상된 수요 경로가 없습니다.";
        }
        return string.Join("\n", evaluation.Loads
            .OrderBy(item => item.LoadId, StringComparer.Ordinal)
            .Select(item =>
            {
                string path = item.PathNodeIds.Count == 0
                    ? "경로 없음"
                    : string.Join(" → ", item.PathNodeIds.Select(id =>
                        AssetDisplayName(displayWorld, snapshot, id)));
                string supply = string.Create(CultureInfo.InvariantCulture,
                    $"{item.DeliveredKw:N0}/{item.DemandKw:N0} kW");
                string failure = item.Failure is null
                    ? ""
                    : $" · {FailureKindText(item.Failure.Kind)}" +
                      $" ({FailureSubjectName(displayWorld, snapshot, item.Failure)})";
                return $"{LoadDisplayName(displayWorld, item.LoadId)}: " +
                       $"{path} · {supply}{failure}";
            }));
    }

    private static string ForecastDetail(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastEvent forecast)
    {
        string intervals = string.Join(" · ", forecast.TemporalProjection.Intervals
            .Select(item => $"{Time(item.StartMinute)}–{Time(item.EndMinute)}"));
        string transitions = forecast.TemporalProjection.Transitions.Count == 0
            ? "열 전환 없음"
            : string.Join(" · ", forecast.TemporalProjection.Transitions.Select(item =>
                $"{Time(item.Minute)} {ThermalTitle(displayWorld, snapshot, item)}"));
        return $"예상 구간 {intervals}\n{transitions}";
    }

    private static string ThermalAssetDisplayKind(
        RealtimeCampaignSnapshot snapshot,
        RealtimeThermalAssetSnapshot asset)
    {
        if (asset.AssetKind == ThermalAssetKind.Edge)
        {
            return "선로 도체";
        }
        SpatialNodeDefinition? node = snapshot.Construction.World.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, asset.AssetId, StringComparison.Ordinal));
        SpatialNodeClassDefinition? nodeClass = node is null
            ? null
            : snapshot.Construction.World.NodeClasses.FirstOrDefault(item =>
                string.Equals(item.ClassId, node.ClassId, StringComparison.Ordinal));
        return nodeClass?.Kind switch
        {
            SpatialNodeKind.Pole => "전신주 접속부",
            SpatialNodeKind.Substation => "변전소 주기기",
            _ => "접속 설비",
        };
    }

    private static string TransitionHistory(
        RealtimeCampaignSnapshot snapshot,
        RealtimeThermalAssetSnapshot asset)
    {
        RealtimeTransition[] transitions = snapshot.PendingTransitions
            .Where(item => string.Equals(item.AssetId, asset.AssetId, StringComparison.Ordinal))
            .OrderBy(item => item.Minute)
            .ThenBy(item => item.Kind)
            .ToArray();
        return transitions.Length == 0
            ? "현재 운영 기록에 설비 전환이 없습니다."
            : string.Join("\n", transitions.Select(item =>
                $"{Time(item.Minute)} · {TransitionKindText(item.Kind)}"));
    }

    private static string BuildGuidance(
        RealtimeCampaignSnapshot snapshot,
        RealtimeInteractionState interaction,
        bool pointerAccepted,
        string pointerMessage)
    {
        if (snapshot.CampaignComplete ||
            interaction.Simulation == RealtimeSimulationState.Ended)
        {
            return RealtimeInteractionReducer.CampaignEndedReadOnlyReason;
        }
        RealtimeDraftToolLock? draftToolLock =
            RealtimeInteractionReducer.ResolveDraftToolLock(snapshot.Construction);
        if (draftToolLock is not null)
        {
            string feedback = string.IsNullOrWhiteSpace(pointerMessage)
                ? string.Empty
                : $"{(pointerAccepted ? "✓" : "!")} {pointerMessage} · ";
            return $"{feedback}초안 도구 잠금 · {draftToolLock.RejectionReason}";
        }
        if (!string.IsNullOrWhiteSpace(pointerMessage))
        {
            return $"{(pointerAccepted ? "✓" : "!")} {pointerMessage}";
        }
        ConstructionSnapshot construction = snapshot.Construction;
        if (construction.ActiveConstruction is ActiveConstructionSnapshot active)
        {
            return $"현재 공사는 {Time(active.CompletionMinute)}에 끝납니다. 비교 초안은 가능하지만 두 번째 발주는 대기합니다.";
        }
        if (construction.NodeDraft is not null)
        {
            return "변전소 초안입니다. 발주를 확인하거나 우클릭으로 취소하세요.";
        }
        if (construction.LineDraft is LineDraftSnapshot line)
        {
            return line.EndNodeId is null
                ? "선로 경로를 이어 주세요. Backspace는 마지막 점, 우클릭은 현재 단계를 되돌립니다."
                : "선로 경로가 닫혔습니다. 발주하거나 Backspace로 끝점을 되돌리세요.";
        }
        return interaction.Tool switch
        {
            RealtimeTool.BuildNode => "지도에서 변전소 위치를 선택하세요.",
            RealtimeTool.BuildLine => "기존 설비에서 선로 시작점을 선택하세요.",
            RealtimeTool.Analysis =>
                "망 분석 켜짐 · 예상 공급 경로와 첫 병목을 지도에서 확인합니다.",
            _ => "설비나 사건을 선택해 상태와 다음 행동을 확인하세요.",
        };
    }

    private static string DisabledConstructionReason(ActiveConstructionSnapshot active) =>
        $"현재 공사가 {Time(active.CompletionMinute)}에 끝난 뒤 발주할 수 있습니다.";

    private static bool OrderQuoteEnabled(
        RealtimeCampaignSnapshot snapshot,
        RealtimeProjectQuote? quote) => quote is
        {
            Accepted: true,
            CostCashUnit: not null,
            BuildMinutes: not null,
            CompletionMinute: not null,
        } && quote.CostCashUnit.Value <= snapshot.CashUnit;

    private static string OrderQuoteDetail(
        RealtimeCampaignSnapshot snapshot,
        RealtimeProjectQuote? quote,
        ActiveConstructionSnapshot? active)
    {
        if (quote is null)
        {
            return "발주 견적을 확인하지 못해 공사를 시작할 수 없습니다.";
        }
        if (!quote.Accepted)
        {
            string rejection = active is not null
                ? DisabledConstructionReason(active)
                : quote.ConstructionError.HasValue
                    ? RealtimeSliceMain.ConstructionErrorText(quote.ConstructionError)
                    : RealtimeSliceMain.RealtimeRunErrorText(quote.Error);
            return $"발주 불가 · {rejection}";
        }
        if (quote is not
            {
                CostCashUnit: long cost,
                BuildMinutes: long buildMinutes,
                CompletionMinute: long completionMinute,
            })
        {
            return "발주 견적 값이 완전하지 않아 공사를 시작할 수 없습니다.";
        }
        string exactQuote =
            $"발주 견적 · 비용 {Cash(cost)} · 공기 {buildMinutes}분 · " +
            $"{Time(completionMinute)} 완공";
        return cost <= snapshot.CashUnit
            ? exactQuote
            : $"{exactQuote}\n발주 불가 · 운영 자금이 {Cash(cost - snapshot.CashUnit)} 부족합니다.";
    }

    private static string FeedbackDetail(
        bool accepted,
        string message,
        string fallback) => string.IsNullOrWhiteSpace(message)
        ? fallback
        : $"{(accepted ? "✓" : "!")} {message}\n{fallback}";

    private static string JoinAssetNames(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        IReadOnlyList<string> ids) => ids.Count == 0
        ? "없음"
        : string.Join(", ", ids.Select(id =>
            AssetDisplayName(displayWorld, snapshot, id)));

    private static string FirstBottleneck(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        ThermalIntervalEvaluation evaluation)
    {
        ThermalSupplyFailure? failure = evaluation.Loads
            .Select(item => item.Failure)
            .FirstOrDefault(item => item is not null);
        return failure is null
            ? "예상 병목 없음"
            : $"{FailureKindText(failure.Kind)} · " +
              $"{FailureSubjectName(displayWorld, snapshot, failure)} · " +
              $"필요 {failure.RequiredKw:N0} / 가능 {failure.AvailableKw:N0} kW";
    }

    private static string ThermalMarkerId(
        string eventId,
        RealtimeThermalTransition transition) =>
        $"THERMAL:{eventId}:{transition.Minute}:{transition.Kind}:{transition.AssetId}";

    private static string ComparisonEventMarkerId(string eventId) =>
        $"DRAFT_FORECAST:{eventId}";

    private static string ComparisonThermalMarkerId(
        string eventId,
        RealtimeThermalTransition transition) =>
        $"DRAFT_THERMAL:{eventId}:{transition.Minute}:{transition.Kind}:{transition.AssetId}";

    private static string CompletedConstructionMarkerId(
        RealtimeConstructionCompletion completion) =>
        $"{CompletedConstructionMarkerPrefix}{completion.CompletionMinute}:" +
        $"{completion.Kind}:{string.Join('+', completion.NodeIds)}:" +
        string.Join('+', completion.EdgeIds);

    private static bool TryResolveCompletedConstruction(
        IReadOnlyList<RealtimeTransition> transitionHistory,
        string markerId,
        out RealtimeConstructionCompletion completion)
    {
        foreach (RealtimeTransition transition in transitionHistory)
        {
            if (transition is not
                {
                    Kind: RealtimeTransitionKind.ConstructionCompleted,
                    Construction: not null,
                })
            {
                continue;
            }
            if (string.Equals(
                    CompletedConstructionMarkerId(transition.Construction),
                    markerId,
                    StringComparison.Ordinal))
            {
                completion = transition.Construction;
                return true;
            }
        }
        completion = null!;
        return false;
    }

    private static bool TryResolveComparisonEvent(
        RealtimeComparisonDraftForecast comparisonDraftForecast,
        string markerId,
        out RealtimeForecastEvent forecast)
    {
        if (comparisonDraftForecast is { Available: true, Forecast: not null })
        {
            foreach (RealtimeForecastEvent candidate in
                     comparisonDraftForecast.Forecast.Events)
            {
                if (string.Equals(
                        ComparisonEventMarkerId(candidate.EventId),
                        markerId,
                        StringComparison.Ordinal))
                {
                    forecast = candidate;
                    return true;
                }
            }
        }
        forecast = null!;
        return false;
    }

    private static bool TryResolveComparisonThermalMarker(
        RealtimeComparisonDraftForecast comparisonDraftForecast,
        string markerId,
        out RealtimeForecastEvent forecast,
        out RealtimeThermalTransition transition)
    {
        if (comparisonDraftForecast is { Available: true, Forecast: not null })
        {
            foreach (RealtimeForecastEvent candidate in
                     comparisonDraftForecast.Forecast.Events)
            {
                foreach (RealtimeThermalTransition candidateTransition in
                         candidate.TemporalProjection.Transitions)
                {
                    if (string.Equals(
                            ComparisonThermalMarkerId(
                                candidate.EventId,
                                candidateTransition),
                            markerId,
                            StringComparison.Ordinal))
                    {
                        forecast = candidate;
                        transition = candidateTransition;
                        return true;
                    }
                }
            }
        }
        forecast = null!;
        transition = null!;
        return false;
    }

    private static bool TryResolveThermalMarker(
        RealtimeForecastSnapshot baseForecast,
        string markerId,
        out RealtimeForecastEvent forecast,
        out RealtimeThermalTransition transition)
    {
        foreach (RealtimeForecastEvent candidate in baseForecast.Events)
        {
            foreach (RealtimeThermalTransition candidateTransition in
                     candidate.TemporalProjection.Transitions)
            {
                if (string.Equals(
                        ThermalMarkerId(candidate.EventId, candidateTransition),
                        markerId,
                        StringComparison.Ordinal))
                {
                    forecast = candidate;
                    transition = candidateTransition;
                    return true;
                }
            }
        }
        forecast = null!;
        transition = null!;
        return false;
    }

    private static string? MapSelectionId(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastSnapshot baseForecast,
        RealtimeComparisonDraftForecast comparisonDraftForecast,
        IReadOnlyList<RealtimeTransition> transitionHistory,
        string? selectionId)
    {
        if (selectionId is null)
        {
            return null;
        }
        if (TryResolveCompletedConstruction(
                transitionHistory,
                selectionId,
                out RealtimeConstructionCompletion completedConstruction))
        {
            return completedConstruction.EdgeIds
                .Concat(completedConstruction.NodeIds)
                .FirstOrDefault(id => snapshot.Construction.World.Edges.Any(item =>
                        string.Equals(item.EdgeId, id, StringComparison.Ordinal)) ||
                    snapshot.Construction.World.Nodes.Any(item =>
                        string.Equals(item.NodeId, id, StringComparison.Ordinal)));
        }
        if (TryResolveThermalMarker(
                baseForecast,
                selectionId,
                out _,
                out RealtimeThermalTransition transition))
        {
            return transition.AssetId;
        }
        if (TryResolveComparisonThermalMarker(
                comparisonDraftForecast,
                selectionId,
                out _,
                out RealtimeThermalTransition comparisonTransition))
        {
            return comparisonTransition.AssetId;
        }
        if (TryResolveComparisonEvent(
                comparisonDraftForecast,
                selectionId,
                out RealtimeForecastEvent comparisonEvent))
        {
            return EventMapSubject(
                displayWorld,
                snapshot,
                comparisonEvent.ProjectedEvaluation,
                comparisonEvent.OperatingProfile);
        }
        RealtimeForecastEvent? forecast = baseForecast.Events.FirstOrDefault(item =>
            string.Equals(item.EventId, selectionId, StringComparison.Ordinal));
        if (forecast is not null)
        {
            return EventMapSubject(
                displayWorld,
                snapshot,
                forecast.ProjectedEvaluation,
                forecast.OperatingProfile);
        }
        RealtimeEventOutcome? outcome = snapshot.CurrentChapterEvents.FirstOrDefault(item =>
            IsCurrentChapterOutcome(snapshot, item) &&
            string.Equals(item.EventId, selectionId, StringComparison.Ordinal));
        if (outcome is not null)
        {
            RealtimeScheduledEventDefinition scheduled = snapshot.Chapter.ScheduledEvents
                .Single(item => string.Equals(
                    item.EventId,
                    outcome.EventId,
                    StringComparison.Ordinal));
            return EventMapSubject(
                displayWorld,
                snapshot,
                outcome.FinalEvaluation,
                scheduled.OperatingProfile);
        }
        bool worldAsset = snapshot.Construction.World.Nodes.Any(item => string.Equals(
                item.NodeId,
                selectionId,
                StringComparison.Ordinal)) ||
            snapshot.Construction.World.Edges.Any(item => string.Equals(
                item.EdgeId,
                selectionId,
                StringComparison.Ordinal));
        return worldAsset ? selectionId : null;
    }

    private static string? EventMapSubject(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        ThermalIntervalEvaluation evaluation,
        CommercialOperatingPhaseDefinition profile)
    {
        bool Exists(string id) => snapshot.Construction.World.Nodes.Any(item =>
                string.Equals(item.NodeId, id, StringComparison.Ordinal)) ||
            snapshot.Construction.World.Edges.Any(item =>
                string.Equals(item.EdgeId, id, StringComparison.Ordinal));

        string? unavailable = profile.UnavailableNodeIds
            .Concat(profile.UnavailableEdgeIds)
            .OrderBy(item => item, StringComparer.Ordinal)
            .FirstOrDefault(Exists);
        if (unavailable is not null)
        {
            return unavailable;
        }
        ThermalLoadSupply? primaryRoute = evaluation.Loads
            .OrderBy(item => item.LoadId, StringComparer.Ordinal)
            .FirstOrDefault();
        string? suppliedTerminal = primaryRoute?.PathNodeIds.LastOrDefault();
        if (suppliedTerminal is not null)
        {
            return suppliedTerminal;
        }
        string? blocker = primaryRoute?.Failure?.AssetId;
        if (blocker is not null)
        {
            return blocker;
        }
        string? finalEdge = primaryRoute?.PathEdgeIds.LastOrDefault();
        if (finalEdge is not null && Exists(finalEdge))
        {
            return finalEdge;
        }
        return profile.Loads
            .Select(load => displayWorld.Loads.FirstOrDefault(item => string.Equals(
                item.LoadId,
                load.LoadId,
                StringComparison.Ordinal))?.NodeId)
            .FirstOrDefault(item => item is not null && Exists(item));
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

    private static string PlayerEventName(
        CommercialWorldDefinition displayWorld,
        CommercialOperatingPhaseDefinition profile)
    {
        if (!profile.DisplayName.Contains("병목 시험", StringComparison.Ordinal))
        {
            return profile.DisplayName;
        }
        string loadName = profile.Loads.Count == 0
            ? "도시"
            : LoadDisplayName(displayWorld, profile.Loads[0].LoadId);
        return $"{loadName} 전력 수요 증가";
    }

    private static string Cash(long cashUnit) => string.Create(
        CultureInfo.InvariantCulture,
        $"{cashUnit:N0}만 원");

    private static string ThermalTitle(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeThermalTransition transition)
    {
        string assetName = AssetDisplayName(displayWorld, snapshot, transition.AssetId);
        return
        transition.Kind switch
        {
            RealtimeThermalTransitionKind.EmergencyEntered =>
                $"{assetName} 비상 운전 시작",
            RealtimeThermalTransitionKind.EmergencyCleared =>
                $"{assetName} 연속 운전 복귀",
            RealtimeThermalTransitionKind.ProtectiveTrip =>
                $"{assetName} 보호정지",
            RealtimeThermalTransitionKind.Recovered =>
                $"{assetName} 냉각 복귀",
            _ => throw new ArgumentOutOfRangeException(nameof(transition)),
        };
    }

    internal static string AssetDisplayName(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        string? assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return "공급 경로";
        }
        SpatialNodeDefinition? node = snapshot.Construction.World.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, assetId, StringComparison.Ordinal));
        if (node is not null)
        {
            return node.DisplayName;
        }
        SpatialEdgeDefinition? edge = snapshot.Construction.World.Edges.FirstOrDefault(item =>
            string.Equals(item.EdgeId, assetId, StringComparison.Ordinal));
        if (edge is not null)
        {
            string line = LineClassDisplayName(snapshot, edge.LineClassId);
            string from = snapshot.Construction.World.Nodes.FirstOrDefault(item =>
                string.Equals(item.NodeId, edge.FromNodeId, StringComparison.Ordinal))
                ?.DisplayName ?? "시작 설비";
            string to = snapshot.Construction.World.Nodes.FirstOrDefault(item =>
                string.Equals(item.NodeId, edge.ToNodeId, StringComparison.Ordinal))
                ?.DisplayName ?? "도착 설비";
            return $"{line} · {from}–{to}";
        }
        CommercialLoadDefinition? load = displayWorld.Loads.FirstOrDefault(item =>
            string.Equals(item.LoadId, assetId, StringComparison.Ordinal));
        if (load is not null)
        {
            return load.DisplayName;
        }
        CommercialSourceDefinition? source = displayWorld.Sources.FirstOrDefault(item =>
            string.Equals(item.SourceId, assetId, StringComparison.Ordinal));
        return source?.DisplayName ?? "미확인 설비";
    }

    private static string LoadDisplayName(
        CommercialWorldDefinition displayWorld,
        string loadId) => displayWorld.Loads.FirstOrDefault(item =>
            string.Equals(item.LoadId, loadId, StringComparison.Ordinal))?.DisplayName ??
            "수요 시설";

    private static string NodeClassDisplayName(
        RealtimeCampaignSnapshot snapshot,
        string classId) => snapshot.Construction.World.NodeClasses.FirstOrDefault(item =>
            string.Equals(item.ClassId, classId, StringComparison.Ordinal))?.DisplayName ??
            "접속 설비";

    private static string LineClassDisplayName(
        RealtimeCampaignSnapshot snapshot,
        string classId) => snapshot.Construction.World.LineClasses.FirstOrDefault(item =>
            string.Equals(item.ClassId, classId, StringComparison.Ordinal))?.DisplayName ??
            "배전선";

    private static string FailureSubjectName(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        ThermalSupplyFailure failure) => failure.AssetId is not null
        ? AssetDisplayName(displayWorld, snapshot, failure.AssetId)
        : failure.AttemptedSourceId is not null
            ? AssetDisplayName(displayWorld, snapshot, failure.AttemptedSourceId)
            : failure.Kind == ThermalFailureKind.NoEligibleSubstation
                ? "변전소 접속"
                : "공급 경로";

    private static string FailureKindText(ThermalFailureKind kind) => kind switch
    {
        ThermalFailureKind.NoTopologyPath => "연결 경로 없음",
        ThermalFailureKind.NoEligibleSubstation => "적합한 변전소 없음",
        ThermalFailureKind.SourceCapacity => "발전 여력 부족",
        ThermalFailureKind.AssetUnavailable => "설비 사용 불가",
        ThermalFailureKind.ContinuousLimit => "연속 한계 초과",
        ThermalFailureKind.EmergencyLimit => "비상 한계 초과",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string TransitionKindText(RealtimeTransitionKind kind) => kind switch
    {
        RealtimeTransitionKind.ChapterStarted => "새 장 시작",
        RealtimeTransitionKind.ForecastRevealed => "사건 예보 공개",
        RealtimeTransitionKind.EventStarted => "사건 시작",
        RealtimeTransitionKind.EventCompleted => "사건 종료",
        RealtimeTransitionKind.ConstructionCompleted => "공사 완료",
        RealtimeTransitionKind.ThermalEmergencyEntered => "비상 운전 시작",
        RealtimeTransitionKind.ThermalEmergencyCleared => "연속 운전 복귀",
        RealtimeTransitionKind.ThermalProtectiveTrip => "보호정지",
        RealtimeTransitionKind.ThermalRecovered => "냉각 복귀",
        RealtimeTransitionKind.PromiseDefaulted => "운영 약속 자동 선택",
        RealtimeTransitionKind.ChapterCompleted => "장 운영 완료",
        RealtimeTransitionKind.CampaignCompleted => "캠페인 운영 완료",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string Time(long minute)
    {
        long day = checked(minute / (24 * 60) + 1);
        long minuteOfDay = minute % (24 * 60);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{day}일 {minuteOfDay / 60:00}:{minuteOfDay % 60:00}");
    }

    private static string Clock(long minute)
    {
        long minuteOfDay = minute % (24 * 60);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}");
    }

    private static string Countdown(long minutes)
    {
        if (minutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes));
        }
        long days = minutes / (24 * 60);
        long hours = minutes % (24 * 60) / 60;
        long remainderMinutes = minutes % 60;
        if (days > 0)
        {
            return hours > 0
                ? $"{days}일 {hours}시간 뒤"
                : $"{days}일 뒤";
        }
        if (hours > 0)
        {
            return remainderMinutes > 0
                ? $"{hours}시간 {remainderMinutes}분 뒤"
                : $"{hours}시간 뒤";
        }
        return $"{remainderMinutes}분 뒤";
    }

    private static int ClampInt(long value) => value > int.MaxValue
        ? int.MaxValue
        : value < int.MinValue
            ? int.MinValue
            : (int)value;
}
