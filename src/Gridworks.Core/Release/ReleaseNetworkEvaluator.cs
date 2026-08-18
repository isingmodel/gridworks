namespace Gridworks.Core.Release;

public static class ReleaseNetworkEvaluator
{
    public static ReleaseNetworkEvaluation Evaluate(
        ReleaseWorldDefinition world,
        ReleaseContingency? contingency = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ReleaseWorldLoader.Validate(world);
        contingency ??= ReleaseContingency.None;

        var nodeClasses = world.NodeClasses.ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        var lineClasses = world.LineClasses.ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        var nodes = world.Nodes.ToDictionary(item => item.NodeId, StringComparer.Ordinal);
        var edges = world.Edges.ToDictionary(item => item.EdgeId, StringComparer.Ordinal);
        var riskAreas = world.RiskAreas.ToDictionary(item => item.RiskAreaId, StringComparer.Ordinal);

        ValidateContingencyReferences(contingency, nodes, edges, riskAreas);

        HashSet<string> unavailableNodes = new(contingency.UnavailableNodeIds, StringComparer.Ordinal);
        HashSet<string> unavailableEdges = new(contingency.UnavailableEdgeIds, StringComparer.Ordinal);
        foreach (string riskAreaId in contingency.ActiveRiskAreaIds)
        {
            ReleaseRiskAreaDefinition area = riskAreas[riskAreaId];
            foreach (ReleaseNodeDefinition node in world.Nodes)
            {
                if (ReleaseGridMath.PointInPolygon(node.Position, area.Polygon))
                {
                    unavailableNodes.Add(node.NodeId);
                }
            }

            foreach (ReleaseEdgeDefinition edge in world.Edges)
            {
                if (SegmentIntersectsPolygon(
                        nodes[edge.FromNodeId].Position,
                        nodes[edge.ToNodeId].Position,
                        area.Polygon))
                {
                    unavailableEdges.Add(edge.EdgeId);
                }
            }
        }

        Dictionary<string, bool> nodeAvailable = world.Nodes.ToDictionary(
            item => item.NodeId,
            item => item.Commissioned && !unavailableNodes.Contains(item.NodeId),
            StringComparer.Ordinal);
        Dictionary<string, bool> edgeAvailable = world.Edges.ToDictionary(
            item => item.EdgeId,
            item => item.Commissioned &&
                    !unavailableEdges.Contains(item.EdgeId) &&
                    nodeAvailable[item.FromNodeId] &&
                    nodeAvailable[item.ToNodeId],
            StringComparer.Ordinal);

        Dictionary<string, List<AdjacentEdge>> adjacency = BuildAdjacency(world, nodes);
        Dictionary<string, long> nodeUsed = world.Nodes.ToDictionary(
            item => item.NodeId,
            _ => 0L,
            StringComparer.Ordinal);
        Dictionary<string, long> edgeUsed = world.Edges.ToDictionary(
            item => item.EdgeId,
            _ => 0L,
            StringComparer.Ordinal);
        Dictionary<string, long> sourceUsed = world.Sources.ToDictionary(
            item => item.SourceId,
            _ => 0L,
            StringComparer.Ordinal);

        ReleaseSourceDefinition[] orderedSources = world.Sources
            .OrderBy(item => item.DispatchOrder)
            .ThenBy(item => item.SourceId, StringComparer.Ordinal)
            .ToArray();
        var loadResults = new List<ReleaseLoadSupply>(world.Loads.Count);

        foreach (ReleaseLoadDefinition load in world.Loads
                     .OrderBy(item => item.Priority)
                     .ThenBy(item => item.LoadId, StringComparer.Ordinal))
        {
            string[] physicalEndpoints = EligibleEndpoints(
                load,
                world,
                nodes,
                nodeClasses,
                requireAvailable: false,
                nodeAvailable);
            if (physicalEndpoints.Length == 0)
            {
                loadResults.Add(FailedLoad(
                    load,
                    load.ConnectionKind == ReleaseLoadConnectionKind.ServiceArea
                        ? ReleaseSupplyFailureKind.NoEligibleSubstation
                        : ReleaseSupplyFailureKind.Disconnected,
                    null,
                    load.DemandKw));
                continue;
            }

            HashSet<string> availableEndpoints = new(
                physicalEndpoints.Where(item => nodeAvailable[item]),
                StringComparer.Ordinal);
            if (load.ConnectionKind == ReleaseLoadConnectionKind.ServiceArea &&
                availableEndpoints.Count == 0)
            {
                loadResults.Add(FailedLoad(
                    load,
                    ReleaseSupplyFailureKind.NoEligibleSubstation,
                    null,
                    load.DemandKw));
                continue;
            }
            PathSnapshot? selectedPath = null;
            ReleaseSourceDefinition? selectedSource = null;
            ReleaseSupplyFailure? firstSourceCapacityFailure = null;
            ReleaseSupplyFailure? firstPathCapacityFailure = null;

            foreach (ReleaseSourceDefinition source in orderedSources)
            {
                if (!nodeAvailable[source.NodeId])
                {
                    continue;
                }

                PathSnapshot? topologyPath = FindBestPath(
                    source.NodeId,
                    availableEndpoints,
                    load.DemandKw,
                    requireResidualCapacity: false,
                    adjacency,
                    nodes,
                    edges,
                    nodeClasses,
                    lineClasses,
                    nodeAvailable,
                    edgeAvailable,
                    nodeUsed,
                    edgeUsed);
                if (topologyPath is null)
                {
                    continue;
                }

                long sourceRemaining = source.CapacityKw - sourceUsed[source.SourceId];
                if (sourceRemaining < load.DemandKw)
                {
                    firstSourceCapacityFailure ??= new ReleaseSupplyFailure(
                        ReleaseSupplyFailureKind.SourceCapacity,
                        source.SourceId,
                        load.DemandKw - sourceRemaining,
                        source.SourceId);
                    continue;
                }

                PathSnapshot? feasible = FindBestPath(
                    source.NodeId,
                    availableEndpoints,
                    load.DemandKw,
                    requireResidualCapacity: true,
                    adjacency,
                    nodes,
                    edges,
                    nodeClasses,
                    lineClasses,
                    nodeAvailable,
                    edgeAvailable,
                    nodeUsed,
                    edgeUsed);
                if (feasible is not null)
                {
                    selectedPath = feasible;
                    selectedSource = source;
                    break;
                }

                if (firstPathCapacityFailure is null &&
                    FirstCapacityFailure(
                        topologyPath,
                        load.DemandKw,
                        nodes,
                        edges,
                        nodeClasses,
                        lineClasses,
                        nodeUsed,
                        edgeUsed) is ReleaseSupplyFailure pathFailure)
                {
                    firstPathCapacityFailure = pathFailure with
                    {
                        AttemptedSourceId = source.SourceId,
                    };
                }
            }

            if (selectedPath is null || selectedSource is null)
            {
                ReleaseSupplyFailure failure = firstPathCapacityFailure ??
                    firstSourceCapacityFailure ??
                    new ReleaseSupplyFailure(
                        ReleaseSupplyFailureKind.Disconnected,
                        null,
                        load.DemandKw);
                loadResults.Add(FailedLoad(load, failure));
                continue;
            }

            sourceUsed[selectedSource.SourceId] += load.DemandKw;
            foreach (string edgeId in selectedPath.EdgeIds)
            {
                edgeUsed[edgeId] += load.DemandKw;
            }
            foreach (string nodeId in selectedPath.NodeIds.Skip(1).Distinct(StringComparer.Ordinal))
            {
                if (NodeRating(nodes[nodeId], nodeClasses) is not null)
                {
                    nodeUsed[nodeId] += load.DemandKw;
                }
            }

            loadResults.Add(new ReleaseLoadSupply(
                load.LoadId,
                load.DemandKw,
                load.DemandKw,
                selectedSource.SourceId,
                selectedPath.NodeIds[^1],
                selectedPath.NodeIds,
                selectedPath.EdgeIds,
                new ReleaseSupplyFailure(ReleaseSupplyFailureKind.None, null, 0)));
        }

        ReleaseNodeUsage[] nodeResults = world.Nodes
            .OrderBy(item => item.NodeId, StringComparer.Ordinal)
            .Select(node =>
            {
                ReleaseNodeClassDefinition nodeClass = nodeClasses[node.ClassId];
                return new ReleaseNodeUsage(
                    node.NodeId,
                    nodeUsed[node.NodeId],
                    NodeRating(node, nodeClasses) ?? 0,
                    world.Edges.Count(edge =>
                        (string.Equals(edge.FromNodeId, node.NodeId, StringComparison.Ordinal) ||
                         string.Equals(edge.ToNodeId, node.NodeId, StringComparison.Ordinal))),
                    nodeClass.MaxConnections,
                    nodeAvailable[node.NodeId]);
            })
            .ToArray();
        ReleaseEdgeUsage[] edgeResults = world.Edges
            .OrderBy(item => item.EdgeId, StringComparer.Ordinal)
            .Select(edge => new ReleaseEdgeUsage(
                edge.EdgeId,
                edgeUsed[edge.EdgeId],
                lineClasses[edge.LineClassId].RatingKw,
                edgeAvailable[edge.EdgeId]))
            .ToArray();
        ReleaseSourceUsage[] sourceResults = orderedSources
            .Select(source => new ReleaseSourceUsage(
                source.SourceId,
                sourceUsed[source.SourceId],
                source.CapacityKw,
                nodeAvailable[source.NodeId]))
            .ToArray();

        long totalDelivered = loadResults.Sum(item => item.DeliveredKw);
        return new ReleaseNetworkEvaluation(
            loadResults.ToArray(),
            nodeResults,
            edgeResults,
            sourceResults,
            totalDelivered,
            sourceResults.Sum(item => item.UsedKw));
    }

    private static ReleaseLoadSupply FailedLoad(
        ReleaseLoadDefinition load,
        ReleaseSupplyFailureKind kind,
        string? assetId,
        long shortfallKw) => new(
        load.LoadId,
        load.DemandKw,
        0,
        null,
        null,
        Array.Empty<string>(),
        Array.Empty<string>(),
        new ReleaseSupplyFailure(kind, assetId, shortfallKw));

    private static ReleaseLoadSupply FailedLoad(
        ReleaseLoadDefinition load,
        ReleaseSupplyFailure failure) => new(
        load.LoadId,
        load.DemandKw,
        0,
        null,
        null,
        Array.Empty<string>(),
        Array.Empty<string>(),
        failure);

    private static string[] EligibleEndpoints(
        ReleaseLoadDefinition load,
        ReleaseWorldDefinition world,
        IReadOnlyDictionary<string, ReleaseNodeDefinition> nodes,
        IReadOnlyDictionary<string, ReleaseNodeClassDefinition> nodeClasses,
        bool requireAvailable,
        IReadOnlyDictionary<string, bool> nodeAvailable)
    {
        if (load.ConnectionKind == ReleaseLoadConnectionKind.DedicatedNode)
        {
            return load.DedicatedNodeId is string nodeId &&
                   (!requireAvailable || nodeAvailable[nodeId])
                ? [nodeId]
                : [];
        }

        return world.Nodes
            .Where(node =>
            {
                ReleaseNodeClassDefinition nodeClass = nodeClasses[node.ClassId];
                if (nodeClass.Kind != ReleaseNodeKind.Substation ||
                    nodeClass.ServiceRadiusCells is not int radius ||
                    (requireAvailable && !nodeAvailable[node.NodeId]))
                {
                    return false;
                }
                long dx = (long)node.Position.X - load.Position.X;
                long dy = (long)node.Position.Y - load.Position.Y;
                return (dx * dx) + (dy * dy) <= (long)radius * radius;
            })
            .Select(item => item.NodeId)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static Dictionary<string, List<AdjacentEdge>> BuildAdjacency(
        ReleaseWorldDefinition world,
        IReadOnlyDictionary<string, ReleaseNodeDefinition> nodes)
    {
        Dictionary<string, List<AdjacentEdge>> adjacency = nodes.Keys.ToDictionary(
            item => item,
            _ => new List<AdjacentEdge>(),
            StringComparer.Ordinal);
        foreach (ReleaseEdgeDefinition edge in world.Edges)
        {
            long length = ReleaseGridMath.EdgeLengthMilliCells(
                nodes[edge.FromNodeId].Position,
                nodes[edge.ToNodeId].Position);
            adjacency[edge.FromNodeId].Add(new AdjacentEdge(edge.EdgeId, edge.ToNodeId, length));
            adjacency[edge.ToNodeId].Add(new AdjacentEdge(edge.EdgeId, edge.FromNodeId, length));
        }
        foreach (List<AdjacentEdge> list in adjacency.Values)
        {
            list.Sort((left, right) => string.CompareOrdinal(left.EdgeId, right.EdgeId));
        }
        return adjacency;
    }

    private static PathSnapshot? FindBestPath(
        string sourceNodeId,
        IReadOnlySet<string> endpointNodeIds,
        long demandKw,
        bool requireResidualCapacity,
        IReadOnlyDictionary<string, List<AdjacentEdge>> adjacency,
        IReadOnlyDictionary<string, ReleaseNodeDefinition> nodes,
        IReadOnlyDictionary<string, ReleaseEdgeDefinition> edges,
        IReadOnlyDictionary<string, ReleaseNodeClassDefinition> nodeClasses,
        IReadOnlyDictionary<string, ReleaseLineClassDefinition> lineClasses,
        IReadOnlyDictionary<string, bool> nodeAvailable,
        IReadOnlyDictionary<string, bool> edgeAvailable,
        IReadOnlyDictionary<string, long> nodeUsed,
        IReadOnlyDictionary<string, long> edgeUsed)
    {
        if (!nodeAvailable[sourceNodeId] || endpointNodeIds.Count == 0)
        {
            return null;
        }

        var initial = new PathSnapshot([sourceNodeId], Array.Empty<string>(), 0);
        var frontier = new List<PathSnapshot> { initial };
        var best = new Dictionary<string, PathSnapshot>(StringComparer.Ordinal)
        {
            [sourceNodeId] = initial,
        };

        while (frontier.Count != 0)
        {
            int bestIndex = 0;
            for (int index = 1; index < frontier.Count; index++)
            {
                if (ComparePaths(frontier[index], frontier[bestIndex]) < 0)
                {
                    bestIndex = index;
                }
            }
            PathSnapshot current = frontier[bestIndex];
            frontier.RemoveAt(bestIndex);
            string currentNodeId = current.NodeIds[^1];
            if (!ReferenceEquals(best[currentNodeId], current))
            {
                continue;
            }
            if (endpointNodeIds.Contains(currentNodeId) &&
                !string.Equals(currentNodeId, sourceNodeId, StringComparison.Ordinal))
            {
                return current;
            }

            foreach (AdjacentEdge adjacent in adjacency[currentNodeId])
            {
                if (!edgeAvailable[adjacent.EdgeId] ||
                    !nodeAvailable[adjacent.OtherNodeId] ||
                    current.NodeIds.Contains(adjacent.OtherNodeId, StringComparer.Ordinal))
                {
                    continue;
                }
                if (requireResidualCapacity &&
                    (lineClasses[edges[adjacent.EdgeId].LineClassId].RatingKw -
                        edgeUsed[adjacent.EdgeId] < demandKw ||
                     NodeRating(nodes[adjacent.OtherNodeId], nodeClasses) is long nodeRating &&
                        nodeRating - nodeUsed[adjacent.OtherNodeId] < demandKw))
                {
                    continue;
                }

                var candidate = new PathSnapshot(
                    current.NodeIds.Append(adjacent.OtherNodeId).ToArray(),
                    current.EdgeIds.Append(adjacent.EdgeId).ToArray(),
                    checked(current.LengthMilliCells + adjacent.LengthMilliCells));
                if (!best.TryGetValue(adjacent.OtherNodeId, out PathSnapshot? previous) ||
                    ComparePaths(candidate, previous) < 0)
                {
                    best[adjacent.OtherNodeId] = candidate;
                    frontier.Add(candidate);
                }
            }
        }
        return null;
    }

    private static ReleaseSupplyFailure? FirstCapacityFailure(
        PathSnapshot path,
        long demandKw,
        IReadOnlyDictionary<string, ReleaseNodeDefinition> nodes,
        IReadOnlyDictionary<string, ReleaseEdgeDefinition> edges,
        IReadOnlyDictionary<string, ReleaseNodeClassDefinition> nodeClasses,
        IReadOnlyDictionary<string, ReleaseLineClassDefinition> lineClasses,
        IReadOnlyDictionary<string, long> nodeUsed,
        IReadOnlyDictionary<string, long> edgeUsed)
    {
        for (int index = 0; index < path.EdgeIds.Count; index++)
        {
            string edgeId = path.EdgeIds[index];
            long edgeRemaining = lineClasses[edges[edgeId].LineClassId].RatingKw - edgeUsed[edgeId];
            if (edgeRemaining < demandKw)
            {
                return new ReleaseSupplyFailure(
                    ReleaseSupplyFailureKind.EdgeCapacity,
                    edgeId,
                    demandKw - edgeRemaining);
            }

            string nodeId = path.NodeIds[index + 1];
            if (NodeRating(nodes[nodeId], nodeClasses) is long rating)
            {
                long nodeRemaining = rating - nodeUsed[nodeId];
                if (nodeRemaining < demandKw)
                {
                    ReleaseNodeKind kind = nodeClasses[nodes[nodeId].ClassId].Kind;
                    return new ReleaseSupplyFailure(
                        kind == ReleaseNodeKind.Substation
                            ? ReleaseSupplyFailureKind.TransformerCapacity
                            : ReleaseSupplyFailureKind.NodeCapacity,
                        nodeId,
                        demandKw - nodeRemaining);
                }
            }
        }
        return null;
    }

    private static long? NodeRating(
        ReleaseNodeDefinition node,
        IReadOnlyDictionary<string, ReleaseNodeClassDefinition> nodeClasses)
    {
        ReleaseNodeClassDefinition nodeClass = nodeClasses[node.ClassId];
        return nodeClass.Kind switch
        {
            ReleaseNodeKind.Pole => nodeClass.ThroughputKw,
            ReleaseNodeKind.Substation => nodeClass.TransformerRatingKw,
            _ => null,
        };
    }

    private static int ComparePaths(PathSnapshot left, PathSnapshot right)
    {
        int comparison = left.LengthMilliCells.CompareTo(right.LengthMilliCells);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = left.EdgeIds.Count.CompareTo(right.EdgeIds.Count);
        if (comparison != 0)
        {
            return comparison;
        }
        for (int index = 0; index < left.EdgeIds.Count; index++)
        {
            comparison = string.CompareOrdinal(left.EdgeIds[index], right.EdgeIds[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }
        return string.CompareOrdinal(left.NodeIds[^1], right.NodeIds[^1]);
    }

    private static void ValidateContingencyReferences(
        ReleaseContingency contingency,
        IReadOnlyDictionary<string, ReleaseNodeDefinition> nodes,
        IReadOnlyDictionary<string, ReleaseEdgeDefinition> edges,
        IReadOnlyDictionary<string, ReleaseRiskAreaDefinition> riskAreas)
    {
        string? unknownNode = contingency.UnavailableNodeIds
            .FirstOrDefault(item => !nodes.ContainsKey(item));
        string? unknownEdge = contingency.UnavailableEdgeIds
            .FirstOrDefault(item => !edges.ContainsKey(item));
        string? unknownRisk = contingency.ActiveRiskAreaIds
            .FirstOrDefault(item => !riskAreas.ContainsKey(item));
        if (unknownNode is not null || unknownEdge is not null || unknownRisk is not null)
        {
            throw new ArgumentException("Contingency references an unknown release-world asset.", nameof(contingency));
        }
    }

    private static bool SegmentIntersectsPolygon(
        ReleasePoint from,
        ReleasePoint to,
        IReadOnlyList<ReleasePoint> polygon)
    {
        if (ReleaseGridMath.PointInPolygon(from, polygon) ||
            ReleaseGridMath.PointInPolygon(to, polygon))
        {
            return true;
        }
        for (int index = 0; index < polygon.Count; index++)
        {
            if (SegmentsIntersect(from, to, polygon[index], polygon[(index + 1) % polygon.Count]))
            {
                return true;
            }
        }
        return false;
    }

    private static bool SegmentsIntersect(
        ReleasePoint a,
        ReleasePoint b,
        ReleasePoint c,
        ReleasePoint d)
    {
        long abC = Orientation(a, b, c);
        long abD = Orientation(a, b, d);
        long cdA = Orientation(c, d, a);
        long cdB = Orientation(c, d, b);
        return (abC == 0 && OnSegment(a, c, b)) ||
               (abD == 0 && OnSegment(a, d, b)) ||
               (cdA == 0 && OnSegment(c, a, d)) ||
               (cdB == 0 && OnSegment(c, b, d)) ||
               ((abC > 0) != (abD > 0) && (cdA > 0) != (cdB > 0));
    }

    private static long Orientation(ReleasePoint a, ReleasePoint b, ReleasePoint c) =>
        checked(((long)b.X - a.X) * ((long)c.Y - a.Y) -
                ((long)b.Y - a.Y) * ((long)c.X - a.X));

    private static bool OnSegment(ReleasePoint a, ReleasePoint point, ReleasePoint b) =>
        point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X) &&
        point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y);

    private sealed record AdjacentEdge(string EdgeId, string OtherNodeId, long LengthMilliCells);

    private sealed record PathSnapshot(
        IReadOnlyList<string> NodeIds,
        IReadOnlyList<string> EdgeIds,
        long LengthMilliCells);
}
