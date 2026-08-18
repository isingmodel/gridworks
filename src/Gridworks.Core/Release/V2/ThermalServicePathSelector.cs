namespace Gridworks.Core.Release.V2;

public static partial class ThermalNetworkEvaluator
{
    private static ServiceRouteCost? FindAcceptedServiceCost(
        EvaluationContext context,
        ValidatedInterval interval,
        IReadOnlySet<string> cooling,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets,
        string sourceNodeId,
        string endNodeId,
        bool allowEmergency,
        long? minimumMarginKw)
    {
        ServicePathPolicy policy = AcceptedPolicy(
            context,
            interval,
            cooling,
            prospectiveAssets,
            allowEmergency,
            minimumMarginKw);
        return FindBestServiceCost(context, policy, sourceNodeId, endNodeId).Cost;
    }

    private static RouteLabel? FindAcceptedServiceRoute(
        EvaluationContext context,
        ValidatedInterval interval,
        IReadOnlySet<string> cooling,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets,
        string sourceNodeId,
        string endNodeId,
        bool allowEmergency,
        long minimumMarginKw)
    {
        ServicePathPolicy policy = AcceptedPolicy(
            context,
            interval,
            cooling,
            prospectiveAssets,
            allowEmergency,
            minimumMarginKw);
        return FindBestServiceRoute(context, policy, sourceNodeId, endNodeId);
    }

    private static PathCandidate? FindDiagnosticServicePath(
        EvaluationContext context,
        string sourceNodeId,
        string endNodeId,
        ValidatedInterval? availableInterval,
        IReadOnlySet<string>? cooling)
    {
        var policy = new ServicePathPolicy(
            nodeId => availableInterval is null ||
                IsDiagnosticNodeAvailable(context, availableInterval, cooling!, nodeId),
            edgeId => availableInterval is null ||
                IsDiagnosticEdgeAvailable(availableInterval, cooling!, edgeId),
            _ => ServiceRouteCost.Zero,
            (_, lengthUnit) => new ServiceRouteCost(0, lengthUnit, 1));
        RouteLabel? route = FindBestServiceRoute(context, policy, sourceNodeId, endNodeId);
        return route is null
            ? null
            : BuildPath(context, route.NodeIds, route.EdgeIds, route.LengthUnit);
    }

    private static ServicePathPolicy AcceptedPolicy(
        EvaluationContext context,
        ValidatedInterval interval,
        IReadOnlySet<string> cooling,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets,
        bool allowEmergency,
        long? minimumMarginKw) => new(
            nodeId => IsNodeAllowed(
                context,
                interval,
                cooling,
                prospectiveAssets,
                nodeId,
                allowEmergency,
                minimumMarginKw),
            edgeId => IsEdgeAllowed(
                context,
                interval,
                cooling,
                prospectiveAssets,
                edgeId,
                allowEmergency,
                minimumMarginKw),
            nodeId => new ServiceRouteCost(
                EmergencyContribution(nodeId, prospectiveAssets),
                0,
                0),
            (edgeId, lengthUnit) => new ServiceRouteCost(
                EmergencyContribution(edgeId, prospectiveAssets),
                lengthUnit,
                1));

    private static (ServiceRouteCost? Cost, IReadOnlyList<string> SubstationIds)
        FindBestServiceCost(
            EvaluationContext context,
            ServicePathPolicy policy,
            string sourceNodeId,
            string endNodeId)
    {
        ServiceRouteCost? best = null;
        var substations = new List<string>();
        foreach (string substationId in EligibleSubstationIds(context, endNodeId))
        {
            ServiceRouteCost? cost = MinimumTwoArmCost(
                context,
                policy,
                substationId,
                sourceNodeId,
                endNodeId,
                EmptyIds,
                zeroCostNodeId: null);
            if (!cost.HasValue)
            {
                continue;
            }
            int comparison = best.HasValue ? cost.Value.CompareTo(best.Value) : -1;
            if (!best.HasValue || comparison < 0)
            {
                best = cost;
                substations.Clear();
                substations.Add(substationId);
            }
            else if (comparison == 0)
            {
                substations.Add(substationId);
            }
        }
        return (best, Array.AsReadOnly(substations.ToArray()));
    }

    private static RouteLabel? FindBestServiceRoute(
        EvaluationContext context,
        ServicePathPolicy policy,
        string sourceNodeId,
        string endNodeId)
    {
        (ServiceRouteCost? bestCost, IReadOnlyList<string> substations) =
            FindBestServiceCost(context, policy, sourceNodeId, endNodeId);
        if (!bestCost.HasValue)
        {
            return null;
        }

        RouteLabel? bestRoute = null;
        foreach (string substationId in substations)
        {
            RouteLabel route = ReconstructLexicographicServiceRoute(
                context,
                policy,
                substationId,
                sourceNodeId,
                endNodeId,
                bestCost.Value);
            if (bestRoute is null || RouteLabelComparer.Instance.Compare(route, bestRoute) < 0)
            {
                bestRoute = route;
            }
        }
        return bestRoute;
    }

    private static IReadOnlyList<string> EligibleSubstationIds(
        EvaluationContext context,
        string endNodeId) => context.Nodes.Values
        .Where(node => IsEligibleSubstation(context, node.NodeId, endNodeId))
        .Select(node => node.NodeId)
        .OrderBy(nodeId => nodeId, StringComparer.Ordinal)
        .ToArray();

    private static RouteLabel ReconstructLexicographicServiceRoute(
        EvaluationContext context,
        ServicePathPolicy policy,
        string substationId,
        string sourceNodeId,
        string endNodeId,
        ServiceRouteCost targetCost)
    {
        string currentNodeId = sourceNodeId;
        bool passedSubstation = string.Equals(
            sourceNodeId,
            substationId,
            StringComparison.Ordinal);
        var nodeIds = new List<string> { sourceNodeId };
        var edgeIds = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { sourceNodeId };
        ServiceRouteCost prefixCost = policy.NodeCost(sourceNodeId);

        while (!string.Equals(currentNodeId, endNodeId, StringComparison.Ordinal))
        {
            bool advanced = false;
            foreach (GraphArc arc in context.Adjacency[currentNodeId])
            {
                if (visited.Contains(arc.OtherNodeId) ||
                    !policy.EdgeAllowed(arc.EdgeId) ||
                    !policy.NodeAllowed(arc.OtherNodeId) ||
                    (!passedSubstation &&
                     string.Equals(arc.OtherNodeId, endNodeId, StringComparison.Ordinal)))
                {
                    continue;
                }

                bool nextPassedSubstation = passedSubstation || string.Equals(
                    arc.OtherNodeId,
                    substationId,
                    StringComparison.Ordinal);
                ServiceRouteCost candidatePrefix = prefixCost
                    .Add(policy.EdgeCost(arc.EdgeId, arc.LengthUnit))
                    .Add(policy.NodeCost(arc.OtherNodeId));
                var candidateVisited = new HashSet<string>(visited, StringComparer.Ordinal)
                {
                    arc.OtherNodeId,
                };
                var banned = new HashSet<string>(candidateVisited, StringComparer.Ordinal);
                banned.Remove(arc.OtherNodeId);
                ServiceRouteCost? remainder = nextPassedSubstation
                    ? MinimumSinglePathCost(
                        context,
                        policy,
                        arc.OtherNodeId,
                        endNodeId,
                        banned)
                    : MinimumTwoArmCost(
                        context,
                        policy,
                        substationId,
                        arc.OtherNodeId,
                        endNodeId,
                        banned,
                        arc.OtherNodeId);
                if (!remainder.HasValue ||
                    candidatePrefix.Add(remainder.Value) != targetCost)
                {
                    continue;
                }

                currentNodeId = arc.OtherNodeId;
                passedSubstation = nextPassedSubstation;
                prefixCost = candidatePrefix;
                nodeIds.Add(currentNodeId);
                edgeIds.Add(arc.EdgeId);
                visited.Add(currentNodeId);
                advanced = true;
                break;
            }
            if (!advanced)
            {
                throw new InvalidOperationException(
                    "The service-route reconstruction oracle disagrees with its optimal cost.");
            }
        }

        if (!passedSubstation || prefixCost != targetCost ||
            nodeIds.Count != nodeIds.Distinct(StringComparer.Ordinal).Count() ||
            edgeIds.Count != edgeIds.Distinct(StringComparer.Ordinal).Count())
        {
            throw new InvalidOperationException(
                "The reconstructed service route is not an exact simple path.");
        }
        return new RouteLabel(
            endNodeId,
            checked((int)targetCost.EmergencyCount),
            targetCost.LengthUnit,
            Array.AsReadOnly(nodeIds.ToArray()),
            Array.AsReadOnly(edgeIds.ToArray()));
    }

    private static ServiceRouteCost? MinimumTwoArmCost(
        EvaluationContext context,
        ServicePathPolicy policy,
        string substationId,
        string firstTargetNodeId,
        string secondTargetNodeId,
        IReadOnlySet<string> bannedNodeIds,
        string? zeroCostNodeId)
    {
        if (string.Equals(firstTargetNodeId, secondTargetNodeId, StringComparison.Ordinal) ||
            bannedNodeIds.Contains(substationId) ||
            bannedNodeIds.Contains(firstTargetNodeId) ||
            bannedNodeIds.Contains(secondTargetNodeId) ||
            !policy.NodeAllowed(substationId) ||
            !policy.NodeAllowed(firstTargetNodeId) ||
            !policy.NodeAllowed(secondTargetNodeId))
        {
            return null;
        }

        string[] allowedNodes = context.Nodes.Keys
            .Where(nodeId => !bannedNodeIds.Contains(nodeId) && policy.NodeAllowed(nodeId))
            .OrderBy(nodeId => nodeId, StringComparer.Ordinal)
            .ToArray();
        var nodeOrdinal = allowedNodes
            .Select((nodeId, index) => (nodeId, index))
            .ToDictionary(item => item.nodeId, item => item.index, StringComparer.Ordinal);
        if (!nodeOrdinal.ContainsKey(substationId) ||
            !nodeOrdinal.ContainsKey(firstTargetNodeId) ||
            !nodeOrdinal.ContainsKey(secondTargetNodeId))
        {
            return null;
        }

        int sink = checked(allowedNodes.Length * 2);
        var network = new ServiceFlowNetwork(sink + 1);
        int In(string nodeId) => checked(nodeOrdinal[nodeId] * 2);
        int Out(string nodeId) => checked(In(nodeId) + 1);
        foreach (string nodeId in allowedNodes)
        {
            bool isSubstation = string.Equals(nodeId, substationId, StringComparison.Ordinal);
            ServiceRouteCost nodeCost = isSubstation || string.Equals(
                nodeId,
                zeroCostNodeId,
                StringComparison.Ordinal)
                ? ServiceRouteCost.Zero
                : policy.NodeCost(nodeId);
            network.AddArc(In(nodeId), Out(nodeId), isSubstation ? 2 : 1, nodeCost);
        }
        foreach (SpatialEdgeDefinition edge in context.Edges.Values)
        {
            if (!nodeOrdinal.ContainsKey(edge.FromNodeId) ||
                !nodeOrdinal.ContainsKey(edge.ToNodeId) ||
                !policy.EdgeAllowed(edge.EdgeId))
            {
                continue;
            }
            long length = FixedGeometry.CeilDistance(
                context.Nodes[edge.FromNodeId].Position,
                context.Nodes[edge.ToNodeId].Position);
            ServiceRouteCost edgeCost = policy.EdgeCost(edge.EdgeId, length);
            network.AddArc(Out(edge.FromNodeId), In(edge.ToNodeId), 1, edgeCost);
            network.AddArc(Out(edge.ToNodeId), In(edge.FromNodeId), 1, edgeCost);
        }
        network.AddArc(Out(firstTargetNodeId), sink, 1, ServiceRouteCost.Zero);
        network.AddArc(Out(secondTargetNodeId), sink, 1, ServiceRouteCost.Zero);
        ServiceRouteCost? flowCost = network.MinimumCostFlow(In(substationId), sink, 2);
        return flowCost?.Add(policy.NodeCost(substationId));
    }

    private static ServiceRouteCost? MinimumSinglePathCost(
        EvaluationContext context,
        ServicePathPolicy policy,
        string startNodeId,
        string endNodeId,
        IReadOnlySet<string> bannedNodeIds)
    {
        if (bannedNodeIds.Contains(startNodeId) || bannedNodeIds.Contains(endNodeId) ||
            !policy.NodeAllowed(startNodeId) || !policy.NodeAllowed(endNodeId))
        {
            return null;
        }
        var distances = new Dictionary<string, ServiceRouteCost>(StringComparer.Ordinal)
        {
            [startNodeId] = ServiceRouteCost.Zero,
        };
        var queue = new PriorityQueue<string, ServiceRouteCost>(ServiceRouteCostComparer.Instance);
        queue.Enqueue(startNodeId, ServiceRouteCost.Zero);
        while (queue.TryDequeue(out string? current, out ServiceRouteCost currentCost))
        {
            if (distances[current] != currentCost)
            {
                continue;
            }
            if (string.Equals(current, endNodeId, StringComparison.Ordinal))
            {
                return currentCost;
            }
            foreach (GraphArc arc in context.Adjacency[current])
            {
                if (bannedNodeIds.Contains(arc.OtherNodeId) ||
                    !policy.EdgeAllowed(arc.EdgeId) ||
                    !policy.NodeAllowed(arc.OtherNodeId))
                {
                    continue;
                }
                ServiceRouteCost nextCost = currentCost
                    .Add(policy.EdgeCost(arc.EdgeId, arc.LengthUnit))
                    .Add(policy.NodeCost(arc.OtherNodeId));
                if (distances.TryGetValue(arc.OtherNodeId, out ServiceRouteCost previous) &&
                    previous.CompareTo(nextCost) <= 0)
                {
                    continue;
                }
                distances[arc.OtherNodeId] = nextCost;
                queue.Enqueue(arc.OtherNodeId, nextCost);
            }
        }
        return null;
    }

    private static readonly IReadOnlySet<string> EmptyIds =
        new HashSet<string>(StringComparer.Ordinal);

    private sealed record ServicePathPolicy(
        Func<string, bool> NodeAllowed,
        Func<string, bool> EdgeAllowed,
        Func<string, ServiceRouteCost> NodeCost,
        Func<string, long, ServiceRouteCost> EdgeCost);

    private readonly record struct ServiceRouteCost(
        long EmergencyCount,
        long LengthUnit,
        long EdgeCount) : IComparable<ServiceRouteCost>
    {
        public static ServiceRouteCost Zero { get; } = new(0, 0, 0);

        public ServiceRouteCost Add(ServiceRouteCost other) => new(
            checked(EmergencyCount + other.EmergencyCount),
            checked(LengthUnit + other.LengthUnit),
            checked(EdgeCount + other.EdgeCount));

        public ServiceRouteCost Negate() => new(
            checked(-EmergencyCount),
            checked(-LengthUnit),
            checked(-EdgeCount));

        public int CompareTo(ServiceRouteCost other)
        {
            int comparison = EmergencyCount.CompareTo(other.EmergencyCount);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = LengthUnit.CompareTo(other.LengthUnit);
            return comparison != 0 ? comparison : EdgeCount.CompareTo(other.EdgeCount);
        }
    }

    private sealed class ServiceRouteCostComparer : IComparer<ServiceRouteCost>
    {
        public static ServiceRouteCostComparer Instance { get; } = new();

        public int Compare(ServiceRouteCost first, ServiceRouteCost second) =>
            first.CompareTo(second);
    }

    private sealed class ServiceFlowNetwork
    {
        private readonly List<ServiceFlowArc>[] _adjacency;

        public ServiceFlowNetwork(int nodeCount)
        {
            _adjacency = Enumerable.Range(0, nodeCount)
                .Select(_ => new List<ServiceFlowArc>())
                .ToArray();
        }

        public void AddArc(
            int from,
            int to,
            int capacity,
            ServiceRouteCost cost)
        {
            var forward = new ServiceFlowArc(to, _adjacency[to].Count, capacity, cost);
            var reverse = new ServiceFlowArc(
                from,
                _adjacency[from].Count,
                0,
                cost.Negate());
            _adjacency[from].Add(forward);
            _adjacency[to].Add(reverse);
        }

        public ServiceRouteCost? MinimumCostFlow(int source, int sink, int requiredFlow)
        {
            ServiceRouteCost total = ServiceRouteCost.Zero;
            for (int sent = 0; sent < requiredFlow; sent++)
            {
                ServiceRouteCost?[] distance = new ServiceRouteCost?[_adjacency.Length];
                int[] previousNode = Enumerable.Repeat(-1, _adjacency.Length).ToArray();
                int[] previousArc = Enumerable.Repeat(-1, _adjacency.Length).ToArray();
                distance[source] = ServiceRouteCost.Zero;
                for (int pass = 0; pass < _adjacency.Length - 1; pass++)
                {
                    bool changed = false;
                    for (int node = 0; node < _adjacency.Length; node++)
                    {
                        if (!distance[node].HasValue)
                        {
                            continue;
                        }
                        for (int arcIndex = 0; arcIndex < _adjacency[node].Count; arcIndex++)
                        {
                            ServiceFlowArc arc = _adjacency[node][arcIndex];
                            if (arc.Capacity <= 0)
                            {
                                continue;
                            }
                            ServiceRouteCost candidate = distance[node]!.Value.Add(arc.Cost);
                            if (distance[arc.To].HasValue &&
                                distance[arc.To]!.Value.CompareTo(candidate) <= 0)
                            {
                                continue;
                            }
                            distance[arc.To] = candidate;
                            previousNode[arc.To] = node;
                            previousArc[arc.To] = arcIndex;
                            changed = true;
                        }
                    }
                    if (!changed)
                    {
                        break;
                    }
                }
                if (!distance[sink].HasValue)
                {
                    return null;
                }
                for (int node = sink; node != source; node = previousNode[node])
                {
                    int from = previousNode[node];
                    int arcIndex = previousArc[node];
                    if (from < 0 || arcIndex < 0)
                    {
                        throw new InvalidOperationException(
                            "The service flow predecessor chain is incomplete.");
                    }
                    ServiceFlowArc arc = _adjacency[from][arcIndex];
                    arc.Capacity--;
                    _adjacency[node][arc.ReverseIndex].Capacity++;
                }
                total = total.Add(distance[sink]!.Value);
            }
            return total;
        }
    }

    private sealed class ServiceFlowArc(
        int to,
        int reverseIndex,
        int capacity,
        ServiceRouteCost cost)
    {
        public int To { get; } = to;

        public int ReverseIndex { get; } = reverseIndex;

        public int Capacity { get; set; } = capacity;

        public ServiceRouteCost Cost { get; } = cost;
    }
}
