namespace Gridworks.Core.Release.V2;

public static class PlacementValidator
{
    public static NodePlacementPreview PreviewNodePlacement(
        SpatialWorldDefinition world,
        string nodeClassId,
        MapPoint position)
    {
        ArgumentNullException.ThrowIfNull(world);
        SpatialNodeClassDefinition? nodeClass = world.NodeClasses.FirstOrDefault(item =>
            string.Equals(item.ClassId, nodeClassId, StringComparison.Ordinal));
        if (nodeClass is null)
        {
            return NodePreview(false, ConstructionError.UnknownNodeClass, nodeClassId, position);
        }
        if (nodeClass.Kind != SpatialNodeKind.Substation)
        {
            return NodePreview(false, ConstructionError.InvalidNodeClass, nodeClassId, position);
        }

        ConstructionError? error = ValidateFootprint(
            world,
            nodeClass,
            position,
            Array.Empty<PlannedCircle>(),
            Array.Empty<PlannedSegment>());
        return NodePreview(
            error is null,
            error,
            nodeClassId,
            position,
            RiskAreasForCircle(world, position, nodeClass.FootprintRadiusUnit));
    }

    public static LineStartPreview PreviewLineStart(
        SpatialWorldDefinition world,
        string startNodeId,
        string lineClassId,
        string poleClassId)
    {
        ArgumentNullException.ThrowIfNull(world);
        SpatialNodeDefinition? start = FindNode(world, startNodeId);
        if (start is null)
        {
            return LineStart(false, ConstructionError.EndpointNotFound, startNodeId, lineClassId, poleClassId);
        }
        if (!start.Commissioned)
        {
            return LineStart(
                false,
                ConstructionError.EndpointNotCommissioned,
                startNodeId,
                lineClassId,
                poleClassId);
        }
        if (FindLineClass(world, lineClassId) is null)
        {
            return LineStart(false, ConstructionError.UnknownLineClass, startNodeId, lineClassId, poleClassId);
        }
        SpatialNodeClassDefinition? poleClass = FindNodeClass(world, poleClassId);
        if (poleClass is null)
        {
            return LineStart(false, ConstructionError.UnknownPoleClass, startNodeId, lineClassId, poleClassId);
        }
        if (poleClass.Kind != SpatialNodeKind.Pole || poleClass.MaxConnections < 2)
        {
            return LineStart(false, ConstructionError.InvalidPoleClass, startNodeId, lineClassId, poleClassId);
        }
        if (!HasConnectionRoom(world, startNodeId, 1))
        {
            return LineStart(false, ConstructionError.ConnectionLimit, startNodeId, lineClassId, poleClassId);
        }
        return LineStart(true, null, startNodeId, lineClassId, poleClassId);
    }

    public static LinePointPreview PreviewLinePoint(
        SpatialWorldDefinition world,
        LineDraftSnapshot draft,
        MapPoint position)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(draft);
        SpatialLineClassDefinition? lineClass = FindLineClass(world, draft.LineClassId);
        SpatialNodeClassDefinition? poleClass = FindNodeClass(world, draft.PoleClassId);
        SpatialNodeDefinition? start = FindNode(world, draft.StartNodeId);
        if (lineClass is null)
        {
            return LinePoint(false, ConstructionError.UnknownLineClass, position, null, null);
        }
        if (poleClass is null)
        {
            return LinePoint(false, ConstructionError.UnknownPoleClass, position, null, lineClass.MaxSpanUnit);
        }
        if (poleClass.Kind != SpatialNodeKind.Pole || poleClass.MaxConnections < 2)
        {
            return LinePoint(false, ConstructionError.InvalidPoleClass, position, null, lineClass.MaxSpanUnit);
        }
        if (start is null)
        {
            return LinePoint(false, ConstructionError.EndpointNotFound, position, null, lineClass.MaxSpanUnit);
        }
        if (draft.EndNodeId is not null)
        {
            return LinePoint(false, ConstructionError.WrongPhase, position, null, lineClass.MaxSpanUnit);
        }

        MapPoint from = draft.IntermediatePoints.Count == 0
            ? start.Position
            : draft.IntermediatePoints[^1];
        if (from == position)
        {
            return LinePoint(
                false,
                ConstructionError.ZeroLengthSegment,
                position,
                0,
                lineClass.MaxSpanUnit,
                RiskAreasForCircle(world, position, poleClass.FootprintRadiusUnit));
        }
        var planned = draft.IntermediatePoints
            .Select(item => new PlannedCircle(item, poleClass.FootprintRadiusUnit))
            .ToArray();
        ConstructionError? footprintError = ValidateFootprint(
            world,
            poleClass,
            position,
            planned,
            DraftSegments(world, draft));
        IReadOnlyList<string> riskAreaIds = RiskAreasForLinePoint(
            world,
            from,
            position,
            poleClass.FootprintRadiusUnit);
        if (footprintError is not null)
        {
            return LinePoint(
                false,
                footprintError,
                position,
                null,
                lineClass.MaxSpanUnit,
                riskAreaIds);
        }

        long length = DistanceCeilingUnit(from, position);
        ConstructionError? segmentError = ValidateSegment(
            world,
            draft,
            from,
            position,
            draft.IntermediatePoints.Count == 0 ? draft.StartNodeId : null,
            null,
            lineClass,
            includeCurrentDraftSegments: true);
        return LinePoint(
            segmentError is null,
            segmentError,
            position,
            length,
            lineClass.MaxSpanUnit,
            riskAreaIds);
    }

    public static LineFinishPreview PreviewLineFinish(
        SpatialWorldDefinition world,
        LineDraftSnapshot draft,
        string endNodeId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(draft);
        SpatialLineClassDefinition? lineClass = FindLineClass(world, draft.LineClassId);
        if (lineClass is null)
        {
            return LineFinish(false, ConstructionError.UnknownLineClass, endNodeId, null, null);
        }
        SpatialNodeDefinition? start = FindNode(world, draft.StartNodeId);
        SpatialNodeDefinition? end = FindNode(world, endNodeId);
        if (start is null || end is null)
        {
            return LineFinish(false, ConstructionError.EndpointNotFound, endNodeId, null, lineClass.MaxSpanUnit);
        }
        if (!start.Commissioned || !end.Commissioned)
        {
            return LineFinish(
                false,
                ConstructionError.EndpointNotCommissioned,
                endNodeId,
                null,
                lineClass.MaxSpanUnit);
        }
        if (string.Equals(draft.StartNodeId, endNodeId, StringComparison.Ordinal))
        {
            return LineFinish(false, ConstructionError.SameEndpoint, endNodeId, null, lineClass.MaxSpanUnit);
        }
        if (!HasConnectionRoom(world, draft.StartNodeId, 1) ||
            !HasConnectionRoom(world, endNodeId, 1))
        {
            return LineFinish(false, ConstructionError.ConnectionLimit, endNodeId, null, lineClass.MaxSpanUnit);
        }
        if (draft.IntermediatePoints.Count == 0 && HasUnorderedEdge(world, draft.StartNodeId, endNodeId))
        {
            return LineFinish(false, ConstructionError.DuplicateSegment, endNodeId, null, lineClass.MaxSpanUnit);
        }

        MapPoint from = draft.IntermediatePoints.Count == 0
            ? start.Position
            : draft.IntermediatePoints[^1];
        long length = DistanceCeilingUnit(from, end.Position);
        IReadOnlyList<string> riskAreaIds = RiskAreasForSegment(world, from, end.Position);
        ConstructionError? error = ValidateSegment(
            world,
            draft,
            from,
            end.Position,
            draft.IntermediatePoints.Count == 0 ? draft.StartNodeId : null,
            endNodeId,
            lineClass,
            includeCurrentDraftSegments: true);
        return LineFinish(
            error is null,
            error,
            endNodeId,
            length,
            lineClass.MaxSpanUnit,
            riskAreaIds);
    }

    public static LinePointMovePreview PreviewMoveLinePoint(
        SpatialWorldDefinition world,
        LineDraftSnapshot draft,
        int pointIndex,
        MapPoint position)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(draft);
        SpatialLineClassDefinition? lineClass = FindLineClass(world, draft.LineClassId);
        if (lineClass is null)
        {
            return LineMove(
                false,
                ConstructionError.UnknownLineClass,
                pointIndex,
                position,
                null,
                null,
                null,
                Array.Empty<string>());
        }
        if (pointIndex < 0 || pointIndex >= draft.IntermediatePoints.Count)
        {
            return LineMove(
                false,
                ConstructionError.InvalidPointIndex,
                pointIndex,
                position,
                null,
                null,
                lineClass.MaxSpanUnit,
                draft.RiskAreaIds);
        }

        MapPoint[] points = draft.IntermediatePoints.ToArray();
        points[pointIndex] = position;
        LineDraftSnapshot candidate = draft with { IntermediatePoints = points };
        IReadOnlyList<string> riskAreaIds = RiskAreasForLineDraft(world, candidate);
        ConstructionError? error = ValidateLineDraft(world, candidate, requireEnd: false);
        SpatialNodeDefinition? start = FindNode(world, draft.StartNodeId);
        if (start is null)
        {
            return LineMove(
                false,
                ConstructionError.EndpointNotFound,
                pointIndex,
                position,
                null,
                null,
                lineClass.MaxSpanUnit,
                riskAreaIds);
        }

        MapPoint previous = pointIndex == 0
            ? start.Position
            : points[pointIndex - 1];
        long previousLength = DistanceCeilingUnit(previous, position);
        long? nextLength = pointIndex + 1 < points.Length
            ? DistanceCeilingUnit(position, points[pointIndex + 1])
            : draft.EndNodeId is not null && FindNode(world, draft.EndNodeId) is { } end
                ? DistanceCeilingUnit(position, end.Position)
                : null;
        return LineMove(
            error is null,
            error,
            pointIndex,
            position,
            previousLength,
            nextLength,
            lineClass.MaxSpanUnit,
            riskAreaIds);
    }

    public static ConstructionError? ValidateCompleteLineDraft(
        SpatialWorldDefinition world,
        LineDraftSnapshot draft)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(draft);
        return ValidateLineDraft(world, draft, requireEnd: true);
    }

    private static ConstructionError? ValidateLineDraft(
        SpatialWorldDefinition world,
        LineDraftSnapshot draft,
        bool requireEnd)
    {
        LineStartPreview start = PreviewLineStart(
            world,
            draft.StartNodeId,
            draft.LineClassId,
            draft.PoleClassId);
        if (!start.Accepted)
        {
            return start.Error;
        }

        var prefix = new List<MapPoint>();
        foreach (MapPoint point in draft.IntermediatePoints)
        {
            var partial = draft with
            {
                IntermediatePoints = prefix.ToArray(),
                EndNodeId = null,
            };
            LinePointPreview preview = PreviewLinePoint(world, partial, point);
            if (!preview.Accepted)
            {
                return preview.Error;
            }
            prefix.Add(point);
        }
        if (draft.EndNodeId is null)
        {
            return requireEnd ? ConstructionError.DraftIncomplete : null;
        }
        return PreviewLineFinish(
            world,
            draft with { EndNodeId = null },
            draft.EndNodeId).Error;
    }

    public static long DistanceCeilingUnit(MapPoint from, MapPoint to)
        => FixedGeometry.CeilDistance(from, to);

    internal static IReadOnlyList<string> RiskAreasForLineDraft(
        SpatialWorldDefinition world,
        LineDraftSnapshot draft)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(draft);
        SpatialNodeDefinition? start = FindNode(world, draft.StartNodeId);
        SpatialNodeClassDefinition? poleClass = FindNodeClass(world, draft.PoleClassId);
        if (start is null || poleClass is null)
        {
            return Array.Empty<string>();
        }

        var result = new SortedSet<string>(StringComparer.Ordinal);
        MapPoint from = start.Position;
        foreach (MapPoint point in draft.IntermediatePoints)
        {
            result.UnionWith(RiskAreasForCircle(world, point, poleClass.FootprintRadiusUnit));
            result.UnionWith(RiskAreasForSegment(world, from, point));
            from = point;
        }
        if (draft.EndNodeId is not null && FindNode(world, draft.EndNodeId) is { } end)
        {
            result.UnionWith(RiskAreasForSegment(world, from, end.Position));
        }
        return Array.AsReadOnly(result.ToArray());
    }

    private static ConstructionError? ValidateFootprint(
        SpatialWorldDefinition world,
        SpatialNodeClassDefinition nodeClass,
        MapPoint position,
        IReadOnlyList<PlannedCircle> planned,
        IReadOnlyList<PlannedSegment> plannedSegments)
    {
        int radius = nodeClass.FootprintRadiusUnit;
        if (!FixedGeometry.CircleWithinBounds(position, radius, world.Bounds))
        {
            return ConstructionError.OutsideBounds;
        }

        foreach (TerrainPolygonDefinition terrain in world.Terrain)
        {
            if (!FixedGeometry.CircleIntersectsPolygon(position, radius, terrain.Polygon))
            {
                continue;
            }
            return terrain.Kind == TerrainKind.Water
                ? ConstructionError.WaterFootprint
                : ConstructionError.BuildingFootprint;
        }

        Dictionary<string, SpatialNodeClassDefinition> classes = world.NodeClasses
            .ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        foreach (SpatialNodeDefinition node in world.Nodes)
        {
            int otherRadius = classes[node.ClassId].FootprintRadiusUnit;
            if (FixedGeometry.CirclesTouchOrOverlap(position, radius, node.Position, otherRadius))
            {
                return ConstructionError.PositionOccupied;
            }
        }
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes
            .ToDictionary(item => item.NodeId, StringComparer.Ordinal);
        foreach (SpatialEdgeDefinition edge in world.Edges)
        {
            if (FixedGeometry.SegmentTouchesCircle(
                    nodes[edge.FromNodeId].Position,
                    nodes[edge.ToNodeId].Position,
                    position,
                    radius))
            {
                return ConstructionError.ExistingLineTouch;
            }
        }
        foreach (PlannedCircle other in planned)
        {
            if (FixedGeometry.CirclesTouchOrOverlap(
                    position,
                    radius,
                    other.Center,
                    other.RadiusUnit))
            {
                return ConstructionError.PositionOccupied;
            }
        }
        foreach (PlannedSegment segment in plannedSegments)
        {
            if (FixedGeometry.SegmentTouchesCircle(
                    segment.From,
                    segment.To,
                    position,
                    radius))
            {
                return ConstructionError.ThirdNodeTouch;
            }
        }
        return null;
    }

    private static ConstructionError? ValidateSegment(
        SpatialWorldDefinition world,
        LineDraftSnapshot draft,
        MapPoint from,
        MapPoint to,
        string? fromNodeId,
        string? toNodeId,
        SpatialLineClassDefinition lineClass,
        bool includeCurrentDraftSegments)
    {
        if (from == to)
        {
            return ConstructionError.ZeroLengthSegment;
        }
        if (FixedGeometry.CeilDistance(from, to) > lineClass.MaxSpanUnit)
        {
            return ConstructionError.SpanTooLong;
        }

        foreach (TerrainPolygonDefinition building in world.Terrain.Where(item =>
                     item.Kind == TerrainKind.Building))
        {
            if (FixedGeometry.SegmentIntersectsPolygon(from, to, building.Polygon))
            {
                return ConstructionError.BuildingCrossing;
            }
        }

        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes
            .ToDictionary(item => item.NodeId, StringComparer.Ordinal);
        foreach (SpatialEdgeDefinition edge in world.Edges)
        {
            MapPoint edgeFrom = nodes[edge.FromNodeId].Position;
            MapPoint edgeTo = nodes[edge.ToNodeId].Position;
            if (FixedGeometry.CollinearPositiveOverlap(from, to, edgeFrom, edgeTo))
            {
                return ConstructionError.CollinearOverlap;
            }
        }

        if (includeCurrentDraftSegments)
        {
            SpatialNodeDefinition start = nodes[draft.StartNodeId];
            MapPoint[] existingPath = [start.Position, .. draft.IntermediatePoints];
            for (int index = 0; index + 1 < existingPath.Length; index++)
            {
                if (FixedGeometry.CollinearPositiveOverlap(
                        from,
                        to,
                        existingPath[index],
                        existingPath[index + 1]))
                {
                    return ConstructionError.CollinearOverlap;
                }
            }
        }

        Dictionary<string, SpatialNodeClassDefinition> classes = world.NodeClasses
            .ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        foreach (SpatialNodeDefinition node in world.Nodes)
        {
            if (string.Equals(node.NodeId, fromNodeId, StringComparison.Ordinal) ||
                string.Equals(node.NodeId, toNodeId, StringComparison.Ordinal))
            {
                continue;
            }
            if (FixedGeometry.SegmentTouchesCircle(
                    from,
                    to,
                    node.Position,
                    classes[node.ClassId].FootprintRadiusUnit))
            {
                return ConstructionError.ThirdNodeTouch;
            }
        }

        SpatialNodeClassDefinition poleClass = FindNodeClass(world, draft.PoleClassId)!;
        foreach (MapPoint point in draft.IntermediatePoints)
        {
            if (point == from || point == to)
            {
                continue;
            }
            if (FixedGeometry.SegmentTouchesCircle(
                    from,
                    to,
                    point,
                    poleClass.FootprintRadiusUnit))
            {
                return ConstructionError.ThirdNodeTouch;
            }
        }
        return null;
    }

    private static bool HasConnectionRoom(
        SpatialWorldDefinition world,
        string nodeId,
        int additionalConnections)
    {
        SpatialNodeDefinition node = FindNode(world, nodeId)!;
        SpatialNodeClassDefinition nodeClass = FindNodeClass(world, node.ClassId)!;
        int current = world.Edges.Count(edge =>
            string.Equals(edge.FromNodeId, nodeId, StringComparison.Ordinal) ||
            string.Equals(edge.ToNodeId, nodeId, StringComparison.Ordinal));
        return current + additionalConnections <= nodeClass.MaxConnections;
    }

    private static bool HasUnorderedEdge(
        SpatialWorldDefinition world,
        string firstNodeId,
        string secondNodeId) =>
        world.Edges.Any(edge =>
            (string.Equals(edge.FromNodeId, firstNodeId, StringComparison.Ordinal) &&
             string.Equals(edge.ToNodeId, secondNodeId, StringComparison.Ordinal)) ||
            (string.Equals(edge.FromNodeId, secondNodeId, StringComparison.Ordinal) &&
             string.Equals(edge.ToNodeId, firstNodeId, StringComparison.Ordinal)));

    private static SpatialNodeDefinition? FindNode(SpatialWorldDefinition world, string nodeId) =>
        world.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, nodeId, StringComparison.Ordinal));

    private static SpatialNodeClassDefinition? FindNodeClass(
        SpatialWorldDefinition world,
        string classId) =>
        world.NodeClasses.FirstOrDefault(item =>
            string.Equals(item.ClassId, classId, StringComparison.Ordinal));

    private static SpatialLineClassDefinition? FindLineClass(
        SpatialWorldDefinition world,
        string classId) =>
        world.LineClasses.FirstOrDefault(item =>
            string.Equals(item.ClassId, classId, StringComparison.Ordinal));

    private static NodePlacementPreview NodePreview(
        bool accepted,
        ConstructionError? error,
        string nodeClassId,
        MapPoint position,
        IReadOnlyList<string>? riskAreaIds = null) =>
        new(accepted, error, nodeClassId, position)
        {
            RiskAreaIds = riskAreaIds ?? Array.Empty<string>(),
        };

    private static LineStartPreview LineStart(
        bool accepted,
        ConstructionError? error,
        string startNodeId,
        string lineClassId,
        string poleClassId) =>
        new(accepted, error, startNodeId, lineClassId, poleClassId);

    private static LinePointPreview LinePoint(
        bool accepted,
        ConstructionError? error,
        MapPoint position,
        long? length,
        int? maxSpan,
        IReadOnlyList<string>? riskAreaIds = null) =>
        new(accepted, error, position, length, maxSpan)
        {
            RiskAreaIds = riskAreaIds ?? Array.Empty<string>(),
        };

    private static LineFinishPreview LineFinish(
        bool accepted,
        ConstructionError? error,
        string endNodeId,
        long? length,
        int? maxSpan,
        IReadOnlyList<string>? riskAreaIds = null) =>
        new(accepted, error, endNodeId, length, maxSpan)
        {
            RiskAreaIds = riskAreaIds ?? Array.Empty<string>(),
        };

    private static LinePointMovePreview LineMove(
        bool accepted,
        ConstructionError? error,
        int pointIndex,
        MapPoint position,
        long? previousLength,
        long? nextLength,
        int? maxSpan,
        IReadOnlyList<string> riskAreaIds) =>
        new(
            accepted,
            error,
            pointIndex,
            position,
            previousLength,
            nextLength,
            maxSpan)
        {
            RiskAreaIds = riskAreaIds,
        };

    private static IReadOnlyList<string> RiskAreasForLinePoint(
        SpatialWorldDefinition world,
        MapPoint from,
        MapPoint to,
        int radiusUnit)
    {
        var result = new SortedSet<string>(RiskAreasForCircle(world, to, radiusUnit),
            StringComparer.Ordinal);
        result.UnionWith(RiskAreasForSegment(world, from, to));
        return Array.AsReadOnly(result.ToArray());
    }

    private static IReadOnlyList<string> RiskAreasForCircle(
        SpatialWorldDefinition world,
        MapPoint center,
        int radiusUnit) =>
        Array.AsReadOnly(world.RiskAreas
            .Where(area => FixedGeometry.CircleIntersectsPolygon(center, radiusUnit, area.Polygon))
            .Select(area => area.RiskAreaId)
            .Order(StringComparer.Ordinal)
            .ToArray());

    private static IReadOnlyList<string> RiskAreasForSegment(
        SpatialWorldDefinition world,
        MapPoint from,
        MapPoint to) =>
        Array.AsReadOnly(world.RiskAreas
            .Where(area => FixedGeometry.SegmentIntersectsPolygon(from, to, area.Polygon))
            .Select(area => area.RiskAreaId)
            .Order(StringComparer.Ordinal)
            .ToArray());

    private static IReadOnlyList<PlannedSegment> DraftSegments(
        SpatialWorldDefinition world,
        LineDraftSnapshot draft)
    {
        SpatialNodeDefinition? start = FindNode(world, draft.StartNodeId);
        if (start is null || draft.IntermediatePoints.Count == 0)
        {
            return Array.Empty<PlannedSegment>();
        }

        MapPoint[] path = [start.Position, .. draft.IntermediatePoints];
        var segments = new PlannedSegment[path.Length - 1];
        for (int index = 0; index < segments.Length; index++)
        {
            segments[index] = new PlannedSegment(path[index], path[index + 1]);
        }
        return Array.AsReadOnly(segments);
    }

    private readonly record struct PlannedCircle(MapPoint Center, int RadiusUnit);
    private readonly record struct PlannedSegment(MapPoint From, MapPoint To);
}
