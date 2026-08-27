using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

internal static class RealtimeWorldPresenter
{
    internal static RealtimeWorldPresentation Present(
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
            interaction.Surface,
            snapshot.Chapter.Content.ChapterId);
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

    private static RealtimeWorldWeather Weather(RealtimeCampaignSnapshot snapshot)
    {
        if (snapshot.ActiveEventStates.Any(item =>
                item.Event.OperatingProfile.ActiveRiskAreaIds.Count > 0))
        {
            return RealtimeWorldWeather.Storm;
        }

        return snapshot.ActiveEventStates.Any(item =>
            item.Event.OperatingProfile.ThermalLimitOverrides.Count > 0)
            ? RealtimeWorldWeather.Heat
            : RealtimeWorldWeather.Clear;
    }

    private static int ClampInt(long value) => value > int.MaxValue
        ? int.MaxValue
        : value < int.MinValue
            ? int.MinValue
            : (int)value;
}
