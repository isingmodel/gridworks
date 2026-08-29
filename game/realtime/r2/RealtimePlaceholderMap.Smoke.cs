#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Gridworks.Core.Release.V2;
using Gridworks.Game.Realtime.UI;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

internal sealed partial class RealtimePlaceholderMap
{
    internal IReadOnlyList<string> CandidateIdsForSmoke =>
        Array.AsReadOnly(_candidateCycle.ToArray());

    internal int CandidateIndexForSmoke => _candidateIndex;

    internal string? ActiveCandidateIdForSmoke => ActiveCandidateId;

    internal CoreMapPoint? WorldCursorForSmoke => _pointer;

    internal string? PreferredCandidateIdForSmoke => _preferredCandidateId;

    internal string ActiveCandidateVisibleLabelForSmoke =>
        ActiveCandidateVisibleLabel;

    internal string? DrawnActiveCandidateIdForSmoke => _drawnActiveCandidateId;

    internal string? DrawnGuidanceTargetNodeIdForSmoke =>
        _drawnGuidanceTargetNodeId;

    internal bool DrawnAnalysisOverlayForSmoke => _drawnAnalysisOverlay;

    internal IReadOnlyList<string> G3AssetPathsForSmoke =>
        Array.AsReadOnly(G3AssetPaths.ToArray());

    internal IReadOnlyList<string> DrawnG3AssetPathsForSmoke =>
        Array.AsReadOnly(_drawnG3AssetPaths
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray());

    internal IReadOnlyList<string> DrawnG3LayersForSmoke =>
        Array.AsReadOnly(_drawnG3Layers
            .OrderBy(layer => layer, StringComparer.Ordinal)
            .ToArray());

    internal string? DrawnG3WaterMaterialForSmoke => _drawnG3WaterMaterial;

    internal int DrawnG3SpriteCountForSmoke => _drawnG3SpriteCount;

    internal float DrawnRiverBankMaxDeviationForSmoke =>
        _drawnRiverBankMaxDeviation;

    internal float DrawnBuildingParcelAlphaForSmoke =>
        _drawnBuildingParcelAlpha;

    internal IReadOnlyList<string> DrawnCityDistrictIdsForSmoke =>
        Array.AsReadOnly(_drawnCityDistrictIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray());

    internal int DrawnCityRoadPathCountForSmoke => _drawnCityRoadPathCount;

    internal int DrawnMeasuredBridgeCountForSmoke => _drawnBridgeSpans.Count;

    internal int? DrawnServiceAreaRadiusUnitForSmoke =>
        _drawnServiceAreaRadiusUnit;

    internal bool DrawnServiceLinkForSmoke => _drawnServiceLink;

    internal bool DrawnSubstationDraftFootprintForSmoke =>
        _drawnSubstationDraftFootprint;

    internal bool MeasuredBridgesLandOnBothBanksForSmoke =>
        _drawnBridgeSpans.Count == 2 &&
        _drawnBridgeSpans.All(span => span.Length == 4 &&
            span[2].DistanceTo(span[0]) >= 10f &&
            span[3].DistanceTo(span[1]) >= 10f &&
            span[2].X < span[0].X &&
            span[3].X > span[1].X);

    internal bool PoleConductorsUseRaisedAttachmentsForSmoke
    {
        get
        {
            if (_presentation is not RealtimeWorldPresentation presentation)
            {
                return false;
            }
            int poleEndpointCount = 0;
            foreach (SpatialEdgeDefinition edge in presentation.World.Edges)
            {
                if (!_drawnConductorAnchors.TryGetValue(
                        edge.EdgeId,
                        out Vector2[]? anchors) ||
                    anchors.Length != 4)
                {
                    return false;
                }
                SpatialNodeDefinition from = presentation.World.Nodes.Single(node =>
                    string.Equals(node.NodeId, edge.FromNodeId, StringComparison.Ordinal));
                SpatialNodeDefinition to = presentation.World.Nodes.Single(node =>
                    string.Equals(node.NodeId, edge.ToNodeId, StringComparison.Ordinal));
                if (IsVisualPole(presentation, from))
                {
                    poleEndpointCount++;
                    if (anchors[1].Y > anchors[0].Y - 20f)
                    {
                        return false;
                    }
                }
                if (IsVisualPole(presentation, to))
                {
                    poleEndpointCount++;
                    if (anchors[3].Y > anchors[2].Y - 20f)
                    {
                        return false;
                    }
                }
            }
            return poleEndpointCount > 0;
        }
    }

    private static bool IsVisualPole(
        RealtimeWorldPresentation presentation,
        SpatialNodeDefinition node) =>
        !node.AuthoredFoundation &&
        presentation.World.NodeClasses.Single(item => string.Equals(
            item.ClassId,
            node.ClassId,
            StringComparison.Ordinal)).Kind == SpatialNodeKind.Pole;

    internal static long WeatherMinutePhaseForSmoke(
        RealtimeWorldPresentation presentation) => WeatherMinutePhase(presentation);

    internal bool AllG3AssetsLoadableForSmoke =>
        G3AssetPaths.All(path => G3Texture(path) is not null);

    internal IReadOnlyList<string> DrawnAnalysisRiskAreaIdsForSmoke =>
        Array.AsReadOnly(_drawnAnalysisRiskAreaIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray());

    internal IReadOnlyList<string> DrawnForecastRiskAreaIdsForSmoke =>
        Array.AsReadOnly(_drawnForecastRiskAreaIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray());

    internal IReadOnlyList<string> DrawnActiveRiskAreaIdsForSmoke =>
        Array.AsReadOnly(_drawnActiveRiskAreaIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray());

    internal bool ForecastRiskUsesPatternWithoutFillForSmoke =>
        _drawnForecastRiskAreaIds.Count > 0 &&
        _drawnActiveRiskAreaIds.Count == 0;

    internal bool ActiveRiskUsesSolidFillForSmoke =>
        _drawnActiveRiskAreaIds.Count > 0;

    internal RealtimePlaceholderStateCue? DrawnStateCueForSmoke(string assetId) =>
        _drawnStateCues.TryGetValue(assetId, out RealtimePlaceholderStateCue cue)
            ? cue
            : null;

    internal bool ActiveCandidateOutlineVisibleForSmoke =>
        ActiveCandidateId is string candidateId &&
        _presentation is RealtimeWorldPresentation presentation &&
        (presentation.World.Nodes.Any(item => string.Equals(
             item.NodeId,
             candidateId,
             StringComparison.Ordinal)) ||
         presentation.World.Edges.Any(item => string.Equals(
             item.EdgeId,
             candidateId,
             StringComparison.Ordinal)));

    internal int LabelFontSizeForSmoke => LabelFontSize;

    internal string StatusLabelForSmoke(RealtimeWorldAssetState state) =>
        StatusLabel(state);

    internal RealtimePlaceholderStateCue StateCueForSmoke(
        RealtimeWorldAssetState state) => StateCue(state);

    internal (string AssetId, Vector2 ViewportPoint)? SelectionActionForSmoke
    {
        get
        {
            if (_presentation is not RealtimeWorldPresentation world ||
                SelectionActionPoint(world) is not (string assetId, Vector2 point))
            {
                return null;
            }
            return (assetId, GetGlobalTransformWithCanvas() * point);
        }
    }

    internal (string AssetId, Vector2 CanvasPoint)? SelectionActionCanvasPointForSmoke =>
        _presentation is RealtimeWorldPresentation world &&
        SelectionActionPoint(world) is (string assetId, Vector2 point)
            ? (assetId, point)
            : null;

    internal (string AssetId, Vector2 CanvasPoint, Vector2 Normal)
        EdgeHitProbeForSmoke()
    {
        if (_presentation is not RealtimeWorldPresentation world ||
            _transform is null)
        {
            throw new InvalidOperationException("Map presentation is not ready.");
        }
        SpatialEdgeDefinition edge = world.World.Edges
            .Where(item => item.Commissioned)
            .OrderByDescending(item =>
            {
                SpatialNodeDefinition from = world.World.Nodes.Single(node =>
                    string.Equals(node.NodeId, item.FromNodeId, StringComparison.Ordinal));
                SpatialNodeDefinition to = world.World.Nodes.Single(node =>
                    string.Equals(node.NodeId, item.ToNodeId, StringComparison.Ordinal));
                return Point(from.Position).DistanceSquaredTo(Point(to.Position));
            })
            .ThenBy(item => item.EdgeId, StringComparer.Ordinal)
            .First();
        SpatialNodeDefinition start = world.World.Nodes.Single(item => string.Equals(
            item.NodeId, edge.FromNodeId, StringComparison.Ordinal));
        SpatialNodeDefinition end = world.World.Nodes.Single(item => string.Equals(
            item.NodeId, edge.ToNodeId, StringComparison.Ordinal));
        Vector2 fromPoint = Point(start.Position);
        Vector2 toPoint = Point(end.Position);
        Vector2 axis = toPoint - fromPoint;
        if (axis.LengthSquared() <= 0.001f)
        {
            throw new InvalidOperationException($"Smoke edge {edge.EdgeId} is degenerate.");
        }
        return (
            edge.EdgeId,
            (fromPoint + toPoint) / 2f,
            new Vector2(axis.Y, -axis.X).Normalized());
    }

    internal RealtimePointerResolution ResolveCanvasPointForSmoke(Vector2 canvasPoint)
    {
        if (_transform is null)
        {
            throw new InvalidOperationException("Map transform is not ready.");
        }
        return ResolveCanvasPoint(
            "MAP_ACTUAL_HIT_BOUNDARY_SMOKE",
            canvasPoint,
            ToWorld(canvasPoint));
    }

    internal void ApplyLayoutForSmoke(RealtimeLayoutProfile profile) =>
        ApplyLayout(profile);

    internal Vector2 ViewportPointForSmoke(CoreMapPoint worldPoint)
    {
        if (_transform is null)
        {
            throw new InvalidOperationException("Map transform is not ready.");
        }
        return GetGlobalTransformWithCanvas() * Point(worldPoint);
    }

    internal void MoveWorldPointerForSmoke(CoreMapPoint worldPoint)
    {
        if (_transform is null)
        {
            throw new InvalidOperationException("Map transform is not ready.");
        }
        Vector2 canvasPoint = Point(worldPoint);
        _GuiInput(new InputEventMouseMotion
        {
            Position = canvasPoint,
            GlobalPosition = canvasPoint,
        });
        GrabFocus();
    }

    internal void ClickWorldPointForSmoke(CoreMapPoint worldPoint)
    {
        if (_transform is null)
        {
            throw new InvalidOperationException("Map transform is not ready.");
        }
        Vector2 canvasPoint = Point(worldPoint);
        _GuiInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = canvasPoint,
            GlobalPosition = canvasPoint,
        });
    }
}
#endif
