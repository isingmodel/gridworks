using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;

namespace Gridworks.Game.Realtime.R2;

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

/// <summary>
/// Resolves presentation marker IDs back to the one Core or map subject they represent.
/// </summary>
internal static class RealtimeTimelineTargetResolver
{
    internal static RealtimeTimelineTarget Resolve(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        string markerId) => Resolve(
        displayWorld,
        snapshot,
        snapshot.Forecast,
        new RealtimeComparisonDraftForecast(false, null, null),
        markerId);

    internal static RealtimeTimelineTarget Resolve(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeComparisonDraftForecast comparisonDraftForecast,
        string markerId) => Resolve(
        displayWorld,
        snapshot,
        snapshot.Forecast,
        comparisonDraftForecast,
        markerId);

    internal static RealtimeTimelineTarget Resolve(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastSnapshot baseForecast,
        RealtimeComparisonDraftForecast comparisonDraftForecast,
        string markerId) => Resolve(
        displayWorld,
        snapshot,
        baseForecast,
        comparisonDraftForecast,
        Array.Empty<RealtimeTransition>(),
        markerId);

    internal static RealtimeTimelineTarget Resolve(
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
                        RealtimeR2Ids.ThermalMarker(forecast.EventId, transition),
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
        if (TryResolveActualThermalMarker(
                transitionHistory,
                markerId,
                out RealtimeTransition actualThermal))
        {
            return new RealtimeTimelineTarget(
                markerId,
                RealtimeTimelineTargetKind.ThermalAsset,
                markerId,
                actualThermal.AssetId);
        }
        RealtimeEventOutcome? selectedOutcome = snapshot.CurrentChapterEvents.FirstOrDefault(
            item => IsCurrentChapterOutcome(snapshot, item) &&
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
        if (string.Equals(
                markerId,
                RealtimeR2Ids.ActiveConstructionMarker,
                StringComparison.Ordinal))
        {
            return new RealtimeTimelineTarget(
                markerId,
                RealtimeTimelineTargetKind.ConstructionProject,
                snapshot.Construction.ActiveConstruction is null
                    ? null
                    : markerId,
                null);
        }
        if (string.Equals(
                markerId,
                RealtimeR2Ids.DraftConstructionMarker,
                StringComparison.Ordinal))
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

    internal static bool TryResolveCompletedConstruction(
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
                    RealtimeR2Ids.CompletedConstructionMarker(transition.Construction),
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

    internal static bool TryResolveActualThermalMarker(
        IReadOnlyList<RealtimeTransition> transitionHistory,
        string markerId,
        out RealtimeTransition transition)
    {
        transition = transitionHistory.FirstOrDefault(item =>
            RealtimeThermalPresentation.IsThermalTransition(item) &&
            string.Equals(
                RealtimeR2Ids.ActualThermalMarker(item),
                markerId,
                StringComparison.Ordinal))!;
        return transition is not null;
    }

    internal static bool TryResolveComparisonEvent(
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
                        RealtimeR2Ids.ComparisonEventMarker(candidate.EventId),
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

    internal static bool TryResolveComparisonThermalMarker(
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
                            RealtimeR2Ids.ComparisonThermalMarker(
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

    internal static bool TryResolveThermalMarker(
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
                        RealtimeR2Ids.ThermalMarker(
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
        forecast = null!;
        transition = null!;
        return false;
    }

    internal static string? MapSelectionId(
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
        if (TryResolveActualThermalMarker(
                transitionHistory,
                selectionId,
                out RealtimeTransition actualThermal))
        {
            return actualThermal.AssetId;
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

    internal static string? EventMapSubject(
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

    internal static bool IsCurrentChapterOutcome(
        RealtimeCampaignSnapshot snapshot,
        RealtimeEventOutcome outcome) => string.Equals(
            outcome.ChapterId,
            snapshot.Chapter.Content.ChapterId,
            StringComparison.Ordinal);

    internal static bool IsPromiseDecisionMarker(
        RealtimeCampaignSnapshot snapshot,
        string markerId) => snapshot.Chapter.Content.CityPromise is
            CommercialCityPromiseDefinition promise &&
        string.Equals(
            markerId,
            RealtimeR2Ids.PromiseDecisionMarker(promise.PromiseId),
            StringComparison.Ordinal);
}
