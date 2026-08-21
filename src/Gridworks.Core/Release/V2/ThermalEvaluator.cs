namespace Gridworks.Core.Release.V2;

public static class ThermalEvaluator
{
    public static ThermalSequenceResult Evaluate(
        CommercialWorldDefinition world,
        ThermalSequenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(request);
        CommercialWorldLoader.Validate(world);
        ValidateRequest(world, request);

        Dictionary<string, bool> memory = CommercialWorldLoader.ThermalAssetIds(world)
            .ToDictionary(item => item, _ => false, StringComparer.Ordinal);
        foreach (ThermalAssetMemory item in request.InitialAssetMemory)
        {
            memory[item.AssetId] = item.ProtectiveOutage;
        }

        List<ThermalIntervalResult> results = [];
        for (int index = 0; index < request.Intervals.Count; index++)
        {
            ThermalIntervalResult result = EvaluateInterval(
                world,
                request.Intervals[index],
                memory,
                index + 1 < request.Intervals.Count ? request.Intervals[index + 1] : null,
                enforceFutureSafety: true);
            results.Add(result);
            memory = result.NextAssetMemory.ToDictionary(
                item => item.AssetId,
                item => item.ProtectiveOutage,
                StringComparer.Ordinal);
        }

        return new ThermalSequenceResult(
            results,
            MemorySnapshot(memory));
    }

    public static ThermalSequenceResult Preview(
        CommercialWorldDefinition world,
        ThermalSequenceRequest request) => Evaluate(world, request);

    private static ThermalIntervalResult EvaluateInterval(
        CommercialWorldDefinition world,
        ThermalIntervalDefinition interval,
        IReadOnlyDictionary<string, bool> memory,
        ThermalIntervalDefinition? nextInterval,
        bool enforceFutureSafety)
    {
        HashSet<string> unavailable = interval.UnavailableAssetIds.ToHashSet(StringComparer.Ordinal);
        Dictionary<string, ThermalLimitDefinition> limits = AppliedLimits(world, interval);
        Dictionary<string, long> assetUse = limits.Keys.ToDictionary(
            item => item,
            _ => 0L,
            StringComparer.Ordinal);
        Dictionary<string, long> sourceUse = world.GenerationSources.ToDictionary(
            item => item.NodeId,
            _ => 0L,
            StringComparer.Ordinal);
        Dictionary<string, SpatialNodeClassDefinition> spatialNodeClasses = world.Spatial.NodeClasses
            .ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        Dictionary<string, SpatialNodeDefinition> spatialNodes = world.Spatial.Nodes
            .ToDictionary(item => item.NodeId, StringComparer.Ordinal);
        List<ThermalDemandResult> demandResults = [];

        foreach (ThermalDemandDefinition demand in interval.Demands)
        {
            if (!demand.Included)
            {
                demandResults.Add(FailedDemand(
                    demand,
                    ThermalSupplyFailure.Deferred,
                    null,
                    deferred: true));
                continue;
            }

            ScoredCandidate? chosen = null;
            CandidateFailure? preferredFailure = null;
            bool sawPath = false;
            bool allowEmergency = AllowsEmergency(interval, demand);
            HashSet<string> serviceSubstationNodeIds = demand.RequireSubstationPath
                ? world.Spatial.Nodes
                    .Where(node => node.Commissioned &&
                        spatialNodeClasses[node.ClassId].Kind == SpatialNodeKind.Substation &&
                        spatialNodeClasses[node.ClassId].ServiceRadiusUnit > 0 &&
                        FixedGeometry.CeilDistance(
                            node.Position,
                            spatialNodes[demand.NodeId].Position) <=
                            spatialNodeClasses[node.ClassId].ServiceRadiusUnit)
                    .Select(item => item.NodeId)
                    .ToHashSet(StringComparer.Ordinal)
                : [];
            VisitPaths(world, demand.NodeId, path =>
            {
                if (demand.RequireSubstationPath &&
                    !path.NodeIds.Any(serviceSubstationNodeIds.Contains))
                {
                    return;
                }
                sawPath = true;
                CandidateEvaluation evaluation = EvaluateCandidate(
                    world,
                    path,
                    demand.DemandKw,
                    allowEmergency,
                    unavailable,
                    memory,
                    limits,
                    assetUse,
                    sourceUse);
                if (!evaluation.Accepted)
                {
                    var failure = new CandidateFailure(
                        path,
                        evaluation.Failure,
                        evaluation.BottleneckAssetId);
                    if (preferredFailure is null || CompareFailure(failure, preferredFailure) < 0)
                    {
                        preferredFailure = failure;
                    }
                    return;
                }

                if (enforceFutureSafety &&
                    demand.ObligationKind == ThermalObligationKind.SafetyDuty &&
                    demand.NamedEmergencyDuty &&
                    evaluation.EmergencyAssetIds.Count != 0 &&
                    nextInterval is not null &&
                    !PreservesNextSafetyDuties(
                        world,
                        nextInterval,
                        memory,
                        assetUse,
                        path.AssetIds,
                        demand.DemandKw,
                        limits))
                {
                    var failure = new CandidateFailure(
                        path,
                        ThermalSupplyFailure.FutureSafetyDuty,
                        evaluation.EmergencyAssetIds[0]);
                    if (preferredFailure is null || CompareFailure(failure, preferredFailure) < 0)
                    {
                        preferredFailure = failure;
                    }
                    return;
                }

                var candidate = new ScoredCandidate(path, evaluation);
                if (chosen is null || CompareCandidate(candidate, chosen) < 0)
                {
                    chosen = candidate;
                }
            });

            if (chosen is null)
            {
                demandResults.Add(FailedDemand(
                    demand,
                    sawPath
                        ? preferredFailure?.Failure ?? ThermalSupplyFailure.NoPath
                        : ThermalSupplyFailure.NoPath,
                    preferredFailure?.BottleneckAssetId));
                continue;
            }

            sourceUse[chosen.Path.Source.NodeId] = checked(
                sourceUse[chosen.Path.Source.NodeId] + demand.DemandKw);
            foreach (string assetId in chosen.Path.AssetIds)
            {
                assetUse[assetId] = checked(assetUse[assetId] + demand.DemandKw);
            }
            demandResults.Add(new ThermalDemandResult(
                demand.DemandId,
                true,
                false,
                demand.DemandKw,
                chosen.Path.Source.NodeId,
                chosen.Path.NodeIds,
                chosen.Path.EdgeIds,
                chosen.Evaluation.EmergencyAssetIds,
                chosen.Evaluation.MinimumRemainingLimitKw,
                ThermalSupplyFailure.None,
                null));
        }

        List<ThermalAssetResult> assets = [];
        foreach (string assetId in limits.Keys.OrderBy(item => item, StringComparer.Ordinal))
        {
            ThermalLimitDefinition limit = limits[assetId];
            long use = assetUse[assetId];
            bool protectiveOutage = memory[assetId];
            ThermalOperatingState current = protectiveOutage
                ? ThermalOperatingState.ProtectiveOutage
                : use <= limit.ContinuousLimitKw
                    ? ThermalOperatingState.Continuous
                    : use <= limit.EmergencyLimitKw
                        ? ThermalOperatingState.Emergency
                        : ThermalOperatingState.OverLimit;
            ThermalOperatingState next = current == ThermalOperatingState.Emergency
                ? ThermalOperatingState.ProtectiveOutage
                : ThermalOperatingState.Continuous;
            assets.Add(new ThermalAssetResult(
                assetId,
                use,
                limit.ContinuousLimitKw,
                limit.EmergencyLimitKw,
                current,
                next,
                unavailable.Contains(assetId)));
        }

        IReadOnlyList<ThermalAssetMemory> nextMemory = assets
            .Select(item => new ThermalAssetMemory(
                item.AssetId,
                item.CurrentState == ThermalOperatingState.Emergency))
            .ToArray();
        return new ThermalIntervalResult(interval.IntervalId, demandResults, assets, nextMemory);
    }

    private static bool PreservesNextSafetyDuties(
        CommercialWorldDefinition world,
        ThermalIntervalDefinition nextInterval,
        IReadOnlyDictionary<string, bool> currentMemory,
        IReadOnlyDictionary<string, long> currentUse,
        IReadOnlyList<string> candidateAssetIds,
        long candidateDemandKw,
        IReadOnlyDictionary<string, ThermalLimitDefinition> currentLimits)
    {
        Dictionary<string, bool> projected = currentMemory.Keys.ToDictionary(
            item => item,
            _ => false,
            StringComparer.Ordinal);
        HashSet<string> candidateAssets = candidateAssetIds.ToHashSet(StringComparer.Ordinal);
        foreach (string assetId in projected.Keys.ToArray())
        {
            long use = checked(currentUse[assetId] +
                (candidateAssets.Contains(assetId) ? candidateDemandKw : 0));
            projected[assetId] = use > currentLimits[assetId].ContinuousLimitKw;
        }
        ThermalIntervalDefinition safetyOnly = nextInterval with
        {
            Demands = nextInterval.Demands.Where(item =>
                item.Included && item.ObligationKind == ThermalObligationKind.SafetyDuty).ToArray(),
        };
        ThermalIntervalResult result = EvaluateInterval(
            world,
            safetyOnly,
            projected,
            null,
            enforceFutureSafety: false);
        return result.Demands.All(item => item.Supplied);
    }

    private static CandidateEvaluation EvaluateCandidate(
        CommercialWorldDefinition world,
        PathCandidate path,
        long demandKw,
        bool allowEmergency,
        IReadOnlySet<string> unavailable,
        IReadOnlyDictionary<string, bool> memory,
        IReadOnlyDictionary<string, ThermalLimitDefinition> limits,
        IReadOnlyDictionary<string, long> assetUse,
        IReadOnlyDictionary<string, long> sourceUse)
    {
        foreach (string assetId in path.RouteAssetIds)
        {
            if (unavailable.Contains(assetId))
            {
                return CandidateEvaluation.Rejected(
                    ThermalSupplyFailure.UnavailableAsset,
                    assetId);
            }
        }
        if (checked(sourceUse[path.Source.NodeId] + demandKw) > path.Source.OutputCapacityKw)
        {
            return CandidateEvaluation.Rejected(
                ThermalSupplyFailure.SourceCapacity,
                path.Source.NodeId);
        }

        List<string> emergencyAssets = [];
        long minimumRemaining = long.MaxValue;
        foreach (string assetId in path.AssetIds)
        {
            if (unavailable.Contains(assetId) || memory[assetId])
            {
                return CandidateEvaluation.Rejected(
                    ThermalSupplyFailure.UnavailableAsset,
                    assetId);
            }
            long nextUse = checked(assetUse[assetId] + demandKw);
            ThermalLimitDefinition limit = limits[assetId];
            if (nextUse > limit.EmergencyLimitKw)
            {
                return CandidateEvaluation.Rejected(
                    ThermalSupplyFailure.EmergencyLimit,
                    assetId);
            }
            if (nextUse > limit.ContinuousLimitKw)
            {
                if (!allowEmergency)
                {
                    return CandidateEvaluation.Rejected(
                        ThermalSupplyFailure.ContinuousPermission,
                        assetId);
                }
                emergencyAssets.Add(assetId);
                minimumRemaining = Math.Min(minimumRemaining, limit.EmergencyLimitKw - nextUse);
            }
            else
            {
                minimumRemaining = Math.Min(minimumRemaining, limit.ContinuousLimitKw - nextUse);
            }
        }
        return CandidateEvaluation.AcceptedResult(
            emergencyAssets,
            minimumRemaining == long.MaxValue ? 0 : minimumRemaining);
    }

    private static void VisitPaths(
        CommercialWorldDefinition world,
        string targetNodeId,
        Action<PathCandidate> visitor)
    {
        Dictionary<string, List<(string EdgeId, string NextNodeId)>> adjacency =
            world.Spatial.Nodes.ToDictionary(
                item => item.NodeId,
                _ => new List<(string, string)>(),
                StringComparer.Ordinal);
        foreach (SpatialEdgeDefinition edge in world.Spatial.Edges.Where(item => item.Commissioned))
        {
            adjacency[edge.FromNodeId].Add((edge.EdgeId, edge.ToNodeId));
            adjacency[edge.ToNodeId].Add((edge.EdgeId, edge.FromNodeId));
        }
        foreach (List<(string EdgeId, string NextNodeId)> neighbors in adjacency.Values)
        {
            neighbors.Sort((left, right) =>
            {
                int edge = StringComparer.Ordinal.Compare(left.EdgeId, right.EdgeId);
                return edge != 0 ? edge : StringComparer.Ordinal.Compare(left.NextNodeId, right.NextNodeId);
            });
        }

        Dictionary<string, SpatialNodeDefinition> nodes = world.Spatial.Nodes.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        HashSet<string> thermalNodeClasses = world.ThermalNodeClasses
            .Select(item => item.ClassId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (GenerationSourceDefinition source in world.GenerationSources.OrderBy(item => item.AuthoredOrder))
        {
            if (!nodes[source.NodeId].Commissioned)
            {
                continue;
            }
            List<string> nodePath = [source.NodeId];
            List<string> edgePath = [];
            HashSet<string> visited = new(StringComparer.Ordinal) { source.NodeId };
            Enumerate(source.NodeId);

            void Enumerate(string current)
            {
                if (current == targetNodeId)
                {
                    long length = 0;
                    for (int index = 0; index + 1 < nodePath.Count; index++)
                    {
                        length = checked(length + FixedGeometry.CeilDistance(
                            nodes[nodePath[index]].Position,
                            nodes[nodePath[index + 1]].Position));
                    }
                    List<string> assets = [];
                    List<string> routeAssets = [nodePath[0]];
                    if (thermalNodeClasses.Contains(nodes[nodePath[0]].ClassId))
                    {
                        assets.Add(nodePath[0]);
                    }
                    for (int index = 0; index < edgePath.Count; index++)
                    {
                        routeAssets.Add(edgePath[index]);
                        routeAssets.Add(nodePath[index + 1]);
                        assets.Add(edgePath[index]);
                        string nodeId = nodePath[index + 1];
                        if (thermalNodeClasses.Contains(nodes[nodeId].ClassId))
                        {
                            assets.Add(nodeId);
                        }
                    }
                    visitor(new PathCandidate(
                        source,
                        nodePath.ToArray(),
                        edgePath.ToArray(),
                        assets.ToArray(),
                        routeAssets.ToArray(),
                        length));
                    return;
                }
                foreach ((string edgeId, string nextNodeId) in adjacency[current])
                {
                    if (!visited.Add(nextNodeId))
                    {
                        continue;
                    }
                    nodePath.Add(nextNodeId);
                    edgePath.Add(edgeId);
                    Enumerate(nextNodeId);
                    edgePath.RemoveAt(edgePath.Count - 1);
                    nodePath.RemoveAt(nodePath.Count - 1);
                    visited.Remove(nextNodeId);
                }
            }
        }
    }

    private static Dictionary<string, ThermalLimitDefinition> AppliedLimits(
        CommercialWorldDefinition world,
        ThermalIntervalDefinition interval)
    {
        Dictionary<string, ThermalLimitDefinition> result = CommercialWorldLoader
            .ThermalAssetIds(world)
            .ToDictionary(
                item => item,
                item => CommercialWorldLoader.LimitForAsset(world, item),
                StringComparer.Ordinal);
        foreach (ThermalLimitOverride item in interval.LimitOverrides)
        {
            result[item.AssetId] = new ThermalLimitDefinition(
                item.ContinuousLimitKw,
                item.EmergencyLimitKw);
        }
        return result;
    }

    private static bool AllowsEmergency(
        ThermalIntervalDefinition interval,
        ThermalDemandDefinition demand) => demand.ObligationKind switch
        {
            ThermalObligationKind.OperatingRecord => false,
            ThermalObligationKind.CityPromise => demand.EmergencyUseApproved,
            ThermalObligationKind.SafetyDuty =>
                demand.NamedEmergencyDuty && interval.Policy == ThermalIntervalPolicy.SafetyEmergencyAllowed,
            _ => false,
        };

    private static void ValidateRequest(
        CommercialWorldDefinition world,
        ThermalSequenceRequest request)
    {
        HashSet<string> thermalAssets = CommercialWorldLoader.ThermalAssetIds(world)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> allAssets = world.Spatial.Nodes.Select(item => item.NodeId)
            .Concat(world.Spatial.Edges.Select(item => item.EdgeId))
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> loadNodes = world.Spatial.Nodes
            .Where(node => world.Spatial.NodeClasses.Single(item => item.ClassId == node.ClassId).Kind ==
                SpatialNodeKind.DedicatedLoadTerminal)
            .Select(item => item.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> memoryIds = new(StringComparer.Ordinal);
        foreach (ThermalAssetMemory memory in request.InitialAssetMemory)
        {
            Require(thermalAssets.Contains(memory.AssetId),
                $"Initial thermal memory references unknown asset '{memory.AssetId}'.");
            Require(memoryIds.Add(memory.AssetId),
                $"Initial thermal memory duplicates asset '{memory.AssetId}'.");
        }

        HashSet<string> intervalIds = new(StringComparer.Ordinal);
        foreach (ThermalIntervalDefinition interval in request.Intervals)
        {
            RequireText(interval.IntervalId, "intervalId");
            RequireText(interval.DisplayName, $"Interval '{interval.IntervalId}' displayName");
            Require(intervalIds.Add(interval.IntervalId),
                $"Duplicate thermal interval ID '{interval.IntervalId}'.");
            HashSet<string> demandIds = new(StringComparer.Ordinal);
            foreach (ThermalDemandDefinition demand in interval.Demands)
            {
                RequireText(demand.DemandId, "demandId");
                RequireText(demand.DisplayName, $"Demand '{demand.DemandId}' displayName");
                Require(demandIds.Add(demand.DemandId),
                    $"Interval '{interval.IntervalId}' duplicates demand '{demand.DemandId}'.");
                Require(loadNodes.Contains(demand.NodeId),
                    $"Demand '{demand.DemandId}' references a non-load endpoint.");
                Require(demand.DemandKw > 0, $"Demand '{demand.DemandId}' must be positive.");
                Require(demand.Included ||
                    demand.ObligationKind == ThermalObligationKind.CityPromise,
                    $"Only a city promise can be deferred from an interval.");
                Require(!demand.EmergencyUseApproved ||
                    demand.ObligationKind == ThermalObligationKind.CityPromise,
                    $"Only a city promise can carry direct emergency approval.");
                Require(!demand.NamedEmergencyDuty ||
                    demand.ObligationKind == ThermalObligationKind.SafetyDuty,
                    $"Only a safety duty can be a named emergency duty.");
            }
            HashSet<string> unavailable = new(StringComparer.Ordinal);
            foreach (string assetId in interval.UnavailableAssetIds)
            {
                Require(allAssets.Contains(assetId),
                    $"Interval '{interval.IntervalId}' references unknown unavailable asset '{assetId}'.");
                Require(unavailable.Add(assetId),
                    $"Interval '{interval.IntervalId}' duplicates unavailable asset '{assetId}'.");
            }
            HashSet<string> overrides = new(StringComparer.Ordinal);
            foreach (ThermalLimitOverride item in interval.LimitOverrides)
            {
                Require(thermalAssets.Contains(item.AssetId),
                    $"Interval '{interval.IntervalId}' overrides unknown thermal asset '{item.AssetId}'.");
                Require(overrides.Add(item.AssetId),
                    $"Interval '{interval.IntervalId}' duplicates override '{item.AssetId}'.");
                ThermalLimitDefinition baseLimit = CommercialWorldLoader.LimitForAsset(world, item.AssetId);
                Require(item.ContinuousLimitKw > 0 &&
                    item.ContinuousLimitKw <= item.EmergencyLimitKw &&
                    item.ContinuousLimitKw <= baseLimit.ContinuousLimitKw &&
                    item.EmergencyLimitKw <= baseLimit.EmergencyLimitKw,
                    $"Interval '{interval.IntervalId}' override '{item.AssetId}' must only lower valid limits.");
            }
        }
    }

    private static ThermalDemandResult FailedDemand(
        ThermalDemandDefinition demand,
        ThermalSupplyFailure failure,
        string? bottleneck,
        bool deferred = false) => new(
            demand.DemandId,
            false,
            deferred,
            demand.DemandKw,
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            failure,
            bottleneck);

    private static IReadOnlyList<ThermalAssetMemory> MemorySnapshot(
        IReadOnlyDictionary<string, bool> memory) => memory
        .OrderBy(item => item.Key, StringComparer.Ordinal)
        .Select(item => new ThermalAssetMemory(item.Key, item.Value))
        .ToArray();

    private static int CompareCandidate(ScoredCandidate left, ScoredCandidate right)
    {
        int result = (left.Evaluation.EmergencyAssetIds.Count == 0 ? 0 : 1).CompareTo(
            right.Evaluation.EmergencyAssetIds.Count == 0 ? 0 : 1);
        if (result != 0) return result;
        result = left.Evaluation.EmergencyAssetIds.Count.CompareTo(
            right.Evaluation.EmergencyAssetIds.Count);
        if (result != 0) return result;
        result = right.Evaluation.MinimumRemainingLimitKw.CompareTo(
            left.Evaluation.MinimumRemainingLimitKw);
        if (result != 0) return result;
        result = left.Path.Source.AuthoredOrder.CompareTo(right.Path.Source.AuthoredOrder);
        if (result != 0) return result;
        result = left.Path.LengthUnit.CompareTo(right.Path.LengthUnit);
        if (result != 0) return result;
        result = left.Path.EdgeIds.Count.CompareTo(right.Path.EdgeIds.Count);
        if (result != 0) return result;
        result = CompareOrdinalLists(left.Path.EdgeIds, right.Path.EdgeIds);
        return result != 0
            ? result
            : StringComparer.Ordinal.Compare(left.Path.NodeIds[^1], right.Path.NodeIds[^1]);
    }

    private static int CompareFailure(CandidateFailure left, CandidateFailure right)
    {
        int result = left.Path.Source.AuthoredOrder.CompareTo(right.Path.Source.AuthoredOrder);
        if (result != 0) return result;
        result = left.Path.LengthUnit.CompareTo(right.Path.LengthUnit);
        if (result != 0) return result;
        result = left.Path.EdgeIds.Count.CompareTo(right.Path.EdgeIds.Count);
        if (result != 0) return result;
        result = CompareOrdinalLists(left.Path.EdgeIds, right.Path.EdgeIds);
        return result != 0
            ? result
            : StringComparer.Ordinal.Compare(left.Path.NodeIds[^1], right.Path.NodeIds[^1]);
    }

    private static int CompareOrdinalLists(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        int common = Math.Min(left.Count, right.Count);
        for (int index = 0; index < common; index++)
        {
            int result = StringComparer.Ordinal.Compare(left[index], right[index]);
            if (result != 0)
            {
                return result;
            }
        }
        return left.Count.CompareTo(right.Count);
    }

    private static void RequireText(string? value, string path) =>
        Require(!string.IsNullOrWhiteSpace(value), $"{path} must be nonempty text.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ThermalEvaluationException(message);
        }
    }

    private sealed record PathCandidate(
        GenerationSourceDefinition Source,
        IReadOnlyList<string> NodeIds,
        IReadOnlyList<string> EdgeIds,
        IReadOnlyList<string> AssetIds,
        IReadOnlyList<string> RouteAssetIds,
        long LengthUnit);

    private sealed record CandidateEvaluation(
        bool Accepted,
        ThermalSupplyFailure Failure,
        string? BottleneckAssetId,
        IReadOnlyList<string> EmergencyAssetIds,
        long MinimumRemainingLimitKw)
    {
        public static CandidateEvaluation Rejected(
            ThermalSupplyFailure failure,
            string bottleneckAssetId) => new(
                false,
                failure,
                bottleneckAssetId,
                Array.Empty<string>(),
                0);

        public static CandidateEvaluation AcceptedResult(
            IReadOnlyList<string> emergencyAssetIds,
            long minimumRemainingLimitKw) => new(
                true,
                ThermalSupplyFailure.None,
                null,
                Array.AsReadOnly(emergencyAssetIds.ToArray()),
                minimumRemainingLimitKw);
    }

    private sealed record ScoredCandidate(
        PathCandidate Path,
        CandidateEvaluation Evaluation);

    private sealed record CandidateFailure(
        PathCandidate Path,
        ThermalSupplyFailure Failure,
        string? BottleneckAssetId);
}
