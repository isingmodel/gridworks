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

    internal bool DrawnAnalysisOverlayForSmoke => _drawnAnalysisOverlay;

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
