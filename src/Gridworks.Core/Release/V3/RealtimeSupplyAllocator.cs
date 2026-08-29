using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

/// <summary>
/// Deterministic, tentative realtime allocator. Loads retain caller order while a bounded
/// service-route optimizer ranks feasible simple paths without enumerating them. Usage is
/// committed only after the complete candidate has passed availability, source, and
/// thermal checks.
/// </summary>
public static class RealtimeSupplyAllocator
{
    public static ThermalIntervalEvaluation EvaluateInterval(
        CommercialWorldDefinition world,
        ThermalIntervalRequest request,
        IReadOnlyList<string> protectiveOutageAssetIds)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(protectiveOutageAssetIds);

        CommercialWorldLoader.Validate(world);
        var context = new AllocationContext(world);
        ValidatedRequest interval = ValidateRequest(
            context,
            request,
            protectiveOutageAssetIds);
        ThermalIntervalEvaluation evaluation = Allocate(context, interval);
        VerifyStableOrderAndAccounting(world, request, evaluation);
        return evaluation;
    }

    private static ThermalIntervalEvaluation Allocate(
        AllocationContext context,
        ValidatedRequest interval)
    {
        Dictionary<string, long> sourceUse = context.Sources.Keys.ToDictionary(
            id => id,
            _ => 0L,
            StringComparer.Ordinal);
        Dictionary<string, long> assetUse = context.Assets.Keys.ToDictionary(
            id => id,
            _ => 0L,
            StringComparer.Ordinal);
        var supplies = new List<ThermalLoadSupply>(interval.Request.Loads.Count);

        foreach (ThermalLoadRequest loadRequest in interval.Request.Loads)
        {
            CommercialLoadDefinition load = context.Loads[loadRequest.LoadId];
            if (EligibleSubstationIds(context, load.NodeId).Count == 0)
            {
                supplies.Add(new ThermalLoadSupply(
                    loadRequest.LoadId,
                    loadRequest.DemandKw,
                    0,
                    null,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    null,
                    new ThermalSupplyFailure(
                        ThermalFailureKind.NoEligibleSubstation,
                        null,
                        null,
                        loadRequest.DemandKw,
                        0)));
                continue;
            }
            CandidateRejection? firstRejection = null;
            AcceptedCandidate? accepted = null;

            foreach (CommercialSourceDefinition source in context.OrderedSources)
            {
                // A rejection is diagnostic only. Preserve the exact first candidate in
                // authored source/static-path order, but publish it only when no source
                // has an accepted route.
                if (accepted is null && firstRejection is null)
                {
                    PathCandidate? firstStaticPath = FindStaticShortestServicePath(
                        context,
                        source.NodeId,
                        load.NodeId);
                    if (firstStaticPath is not null)
                    {
                        firstRejection = AssessCandidate(
                            context,
                            interval,
                            sourceUse,
                            assetUse,
                            source,
                            load,
                            loadRequest,
                            firstStaticPath,
                            out _);
                    }
                }

                AcceptedCandidate? candidate = FindBestAcceptedCandidate(
                    context,
                    interval,
                    sourceUse,
                    assetUse,
                    source,
                    load,
                    loadRequest);
                if (candidate is not null &&
                    (accepted is null ||
                     AcceptedCandidateComparer.Instance.Compare(candidate, accepted) < 0))
                {
                    accepted = candidate;
                }
            }

            if (accepted is null)
            {
                ThermalSupplyFailure failure = firstRejection?.Failure ??
                    new ThermalSupplyFailure(
                        ThermalFailureKind.NoTopologyPath,
                        null,
                        null,
                        loadRequest.DemandKw,
                        0);
                supplies.Add(new ThermalLoadSupply(
                    loadRequest.LoadId,
                    loadRequest.DemandKw,
                    0,
                    null,
                    firstRejection?.Path.NodeIds ?? Array.Empty<string>(),
                    firstRejection?.Path.EdgeIds ?? Array.Empty<string>(),
                    null,
                    failure));
                continue;
            }

            sourceUse[accepted.Source.SourceId] = checked(
                sourceUse[accepted.Source.SourceId] + loadRequest.DemandKw);
            foreach (string assetId in accepted.Path.AssetIds)
            {
                assetUse[assetId] = checked(assetUse[assetId] + loadRequest.DemandKw);
            }
            supplies.Add(new ThermalLoadSupply(
                loadRequest.LoadId,
                loadRequest.DemandKw,
                loadRequest.DemandKw,
                accepted.Source.SourceId,
                accepted.Path.NodeIds,
                accepted.Path.EdgeIds,
                accepted.Quality.MinimumRemainingKw,
                null));
        }

        string[] currentProtectiveOutages = interval.ProtectiveOutageAssetIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        ThermalAssetUsage[] assets = context.Assets.Values
            .OrderBy(item => item.AssetId, StringComparer.Ordinal)
            .Select(asset =>
            {
                long usedKw = assetUse[asset.AssetId];
                ThermalLimit limit = interval.Limits[asset.AssetId];
                ThermalOperatingState state;
                if (interval.ProtectiveOutageAssetIds.Contains(asset.AssetId))
                {
                    state = ThermalOperatingState.ProtectiveOutage;
                }
                else if (usedKw > limit.EmergencyKw)
                {
                    state = ThermalOperatingState.OverLimit;
                }
                else if (usedKw > limit.ContinuousKw)
                {
                    state = ThermalOperatingState.Emergency;
                }
                else
                {
                    state = ThermalOperatingState.Continuous;
                }
                return new ThermalAssetUsage(
                    asset.AssetId,
                    asset.AssetKind,
                    usedKw,
                    limit.ContinuousKw,
                    limit.EmergencyKw,
                    state,
                    state);
            })
            .ToArray();

        ThermalSourceUsage[] sources = context.OrderedSources
            .Select(source => new ThermalSourceUsage(
                source.SourceId,
                sourceUse[source.SourceId],
                source.CapacityKw))
            .ToArray();
        return new ThermalIntervalEvaluation(
            interval.Request.IntervalId,
            supplies,
            assets,
            sources,
            new ThermalState(currentProtectiveOutages));
    }

    private static CandidateRejection? AssessCandidate(
        AllocationContext context,
        ValidatedRequest interval,
        IReadOnlyDictionary<string, long> sourceUse,
        IReadOnlyDictionary<string, long> assetUse,
        CommercialSourceDefinition source,
        CommercialLoadDefinition load,
        ThermalLoadRequest loadRequest,
        PathCandidate path,
        out CandidateQuality quality)
    {
        quality = default;
        if (path.NodeIds.Count == 0 ||
            !IsEligibleSubstation(context, path.NodeIds[^1], load.NodeId))
        {
            return Rejected(
                path,
                ThermalFailureKind.NoEligibleSubstation,
                source.SourceId,
                null,
                loadRequest.DemandKw,
                0);
        }

        long sourceRemainingKw = checked(
            source.CapacityKw - sourceUse[source.SourceId]);
        if (loadRequest.DemandKw > sourceRemainingKw)
        {
            return Rejected(
                path,
                ThermalFailureKind.SourceCapacity,
                source.SourceId,
                null,
                loadRequest.DemandKw,
                sourceRemainingKw);
        }

        for (int index = 0; index < path.NodeIds.Count; index++)
        {
            string nodeId = path.NodeIds[index];
            if (interval.UnavailableNodeIds.Contains(nodeId) ||
                interval.ProtectiveOutageAssetIds.Contains(nodeId))
            {
                return Rejected(
                    path,
                    ThermalFailureKind.AssetUnavailable,
                    source.SourceId,
                    nodeId,
                    loadRequest.DemandKw,
                    0);
            }
            if (index >= path.EdgeIds.Count)
            {
                continue;
            }
            string edgeId = path.EdgeIds[index];
            if (interval.UnavailableEdgeIds.Contains(edgeId) ||
                interval.ProtectiveOutageAssetIds.Contains(edgeId))
            {
                return Rejected(
                    path,
                    ThermalFailureKind.AssetUnavailable,
                    source.SourceId,
                    edgeId,
                    loadRequest.DemandKw,
                    0);
            }
        }

        long minimum = long.MaxValue;
        int emergencyAssetCount = 0;
        foreach (string assetId in path.AssetIds)
        {
            ThermalLimit limit = interval.Limits[assetId];
            long prospectiveKw = checked(assetUse[assetId] + loadRequest.DemandKw);
            if (loadRequest.Permission == ThermalPermission.ContinuousOnly)
            {
                if (prospectiveKw > limit.ContinuousKw)
                {
                    return Rejected(
                        path,
                        ThermalFailureKind.ContinuousLimit,
                        source.SourceId,
                        assetId,
                        prospectiveKw,
                        limit.ContinuousKw);
                }
                minimum = Math.Min(minimum, checked(limit.ContinuousKw - prospectiveKw));
                continue;
            }

            if (prospectiveKw > limit.EmergencyKw)
            {
                return Rejected(
                    path,
                    ThermalFailureKind.EmergencyLimit,
                    source.SourceId,
                    assetId,
                    prospectiveKw,
                    limit.EmergencyKw);
            }
            long remaining = prospectiveKw > limit.ContinuousKw
                ? checked(limit.EmergencyKw - prospectiveKw)
                : checked(limit.ContinuousKw - prospectiveKw);
            if (prospectiveKw > limit.ContinuousKw)
            {
                emergencyAssetCount++;
            }
            minimum = Math.Min(minimum, remaining);
        }
        if (minimum == long.MaxValue)
        {
            throw new InvalidOperationException(
                "A source-to-substation path must contain at least one thermal asset.");
        }
        quality = new CandidateQuality(
            emergencyAssetCount == 0
                ? ThermalOperatingState.Continuous
                : ThermalOperatingState.Emergency,
            emergencyAssetCount,
            minimum);
        return null;
    }

    private static CandidateRejection Rejected(
        PathCandidate path,
        ThermalFailureKind kind,
        string? sourceId,
        string? assetId,
        long requiredKw,
        long availableKw) => new(
        path,
        new ThermalSupplyFailure(
            kind,
            sourceId,
            assetId,
            requiredKw,
            availableKw));

    private static AcceptedCandidate? FindBestAcceptedCandidate(
        AllocationContext context,
        ValidatedRequest interval,
        IReadOnlyDictionary<string, long> sourceUse,
        IReadOnlyDictionary<string, long> assetUse,
        CommercialSourceDefinition source,
        CommercialLoadDefinition load,
        ThermalLoadRequest loadRequest)
    {
        long sourceRemainingKw = checked(
            source.CapacityKw - sourceUse[source.SourceId]);
        if (loadRequest.DemandKw > sourceRemainingKw)
        {
            return null;
        }

        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets =
            BuildProspectiveAssets(context, interval, assetUse, loadRequest);
        bool allowEmergency = false;
        int? minimumEmergencyCount = FindMinimumEmergencyCount(
            context,
            interval,
            prospectiveAssets,
            source.NodeId,
            load.NodeId,
            allowEmergency: false,
            minimumMarginKw: null);
        ThermalOperatingState thermalGrade = ThermalOperatingState.Continuous;
        if (minimumEmergencyCount is null)
        {
            if (loadRequest.Permission != ThermalPermission.EmergencyAllowed)
            {
                return null;
            }
            allowEmergency = true;
            thermalGrade = ThermalOperatingState.Emergency;
            minimumEmergencyCount = FindMinimumEmergencyCount(
                context,
                interval,
                prospectiveAssets,
                source.NodeId,
                load.NodeId,
                allowEmergency: true,
                minimumMarginKw: null);
            if (minimumEmergencyCount is null)
            {
                return null;
            }
        }

        long? maximumBottleneckKw = null;
        IEnumerable<long> candidateMargins = context.Assets.Values
            .Where(asset => IsThermalAssetAllowed(
                asset,
                interval,
                prospectiveAssets,
                allowEmergency,
                minimumMarginKw: null))
            .Select(asset => prospectiveAssets[asset.AssetId].AppliedMarginKw)
            .Distinct()
            .OrderByDescending(value => value);
        foreach (long candidateMarginKw in candidateMargins)
        {
            int? count = FindMinimumEmergencyCount(
                context,
                interval,
                prospectiveAssets,
                source.NodeId,
                load.NodeId,
                allowEmergency,
                candidateMarginKw);
            if (count == minimumEmergencyCount)
            {
                maximumBottleneckKw = candidateMarginKw;
                break;
            }
        }
        if (maximumBottleneckKw is null)
        {
            throw new InvalidOperationException(
                "A reachable source-to-substation path must have a thermal bottleneck.");
        }

        RouteLabel? route = FindBestServiceRoute(
            context,
            AcceptedPolicy(
                context,
                interval,
                prospectiveAssets,
                allowEmergency,
                maximumBottleneckKw.Value),
            source.NodeId,
            load.NodeId);
        if (route is null || route.EmergencyAssetCount != minimumEmergencyCount.Value)
        {
            throw new InvalidOperationException(
                "Qualified thermal route selection disagrees with its reachability pass.");
        }

        PathCandidate path = BuildPath(
            context,
            route.NodeIds,
            route.EdgeIds,
            route.LengthUnit);
        CandidateRejection? disagreement = AssessCandidate(
            context,
            interval,
            sourceUse,
            assetUse,
            source,
            load,
            loadRequest,
            path,
            out CandidateQuality assessedQuality);
        var expectedQuality = new CandidateQuality(
            thermalGrade,
            minimumEmergencyCount.Value,
            maximumBottleneckKw.Value);
        if (disagreement is not null || assessedQuality != expectedQuality)
        {
            throw new InvalidOperationException(
                "Bounded route selection disagrees with final candidate assessment.");
        }
        return new AcceptedCandidate(source, path, expectedQuality);
    }

    private static IReadOnlyDictionary<string, ProspectiveAsset> BuildProspectiveAssets(
        AllocationContext context,
        ValidatedRequest interval,
        IReadOnlyDictionary<string, long> assetUse,
        ThermalLoadRequest loadRequest)
    {
        var result = new Dictionary<string, ProspectiveAsset>(StringComparer.Ordinal);
        foreach (ThermalAssetInfo asset in context.Assets.Values)
        {
            ThermalLimit limit = interval.Limits[asset.AssetId];
            long prospectiveKw = checked(
                assetUse[asset.AssetId] + loadRequest.DemandKw);
            if (prospectiveKw <= limit.ContinuousKw)
            {
                result.Add(asset.AssetId, new ProspectiveAsset(
                    ThermalOperatingState.Continuous,
                    checked(limit.ContinuousKw - prospectiveKw),
                    ContinuousEligible: true,
                    EmergencyEligible: true));
            }
            else if (loadRequest.Permission == ThermalPermission.EmergencyAllowed &&
                     prospectiveKw <= limit.EmergencyKw)
            {
                result.Add(asset.AssetId, new ProspectiveAsset(
                    ThermalOperatingState.Emergency,
                    checked(limit.EmergencyKw - prospectiveKw),
                    ContinuousEligible: false,
                    EmergencyEligible: true));
            }
            else
            {
                result.Add(asset.AssetId, new ProspectiveAsset(
                    ThermalOperatingState.OverLimit,
                    0,
                    ContinuousEligible: false,
                    EmergencyEligible: false));
            }
        }
        return result;
    }

    private static int? FindMinimumEmergencyCount(
        AllocationContext context,
        ValidatedRequest interval,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets,
        string sourceNodeId,
        string endNodeId,
        bool allowEmergency,
        long? minimumMarginKw)
    {
        ServiceRouteCost? cost = FindBestServiceCost(
            context,
            AcceptedPolicy(
                context,
                interval,
                prospectiveAssets,
                allowEmergency,
                minimumMarginKw),
            sourceNodeId,
            endNodeId).Cost;
        return cost is null ? null : checked((int)cost.Value.EmergencyCount);
    }

    private static ServicePathPolicy AcceptedPolicy(
        AllocationContext context,
        ValidatedRequest interval,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets,
        bool allowEmergency,
        long? minimumMarginKw) => new(
            nodeId => IsNodeAllowed(
                context,
                interval,
                prospectiveAssets,
                nodeId,
                allowEmergency,
                minimumMarginKw),
            edgeId => IsEdgeAllowed(
                context,
                interval,
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

    private static bool IsNodeAllowed(
        AllocationContext context,
        ValidatedRequest interval,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets,
        string nodeId,
        bool allowEmergency,
        long? minimumMarginKw)
    {
        if (context.NodeClasses[context.Nodes[nodeId].ClassId].Kind ==
            SpatialNodeKind.DedicatedLoadTerminal)
        {
            return false;
        }
        if (interval.UnavailableNodeIds.Contains(nodeId))
        {
            return false;
        }
        return !context.Assets.TryGetValue(nodeId, out ThermalAssetInfo? asset) ||
            IsThermalAssetAllowed(
                asset,
                interval,
                prospectiveAssets,
                allowEmergency,
                minimumMarginKw);
    }

    private static bool IsEdgeAllowed(
        AllocationContext context,
        ValidatedRequest interval,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets,
        string edgeId,
        bool allowEmergency,
        long? minimumMarginKw) =>
        !interval.UnavailableEdgeIds.Contains(edgeId) &&
        IsThermalAssetAllowed(
            context.Assets[edgeId],
            interval,
            prospectiveAssets,
            allowEmergency,
            minimumMarginKw);

    private static bool IsThermalAssetAllowed(
        ThermalAssetInfo asset,
        ValidatedRequest interval,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets,
        bool allowEmergency,
        long? minimumMarginKw)
    {
        bool authoredUnavailable = asset.AssetKind switch
        {
            ThermalAssetKind.Node =>
                interval.UnavailableNodeIds.Contains(asset.AssetId),
            ThermalAssetKind.Edge =>
                interval.UnavailableEdgeIds.Contains(asset.AssetId),
            _ => throw new InvalidOperationException("Unknown thermal asset kind."),
        };
        if (authoredUnavailable ||
            interval.ProtectiveOutageAssetIds.Contains(asset.AssetId))
        {
            return false;
        }
        ProspectiveAsset prospective = prospectiveAssets[asset.AssetId];
        bool eligible = allowEmergency
            ? prospective.EmergencyEligible
            : prospective.ContinuousEligible;
        return eligible &&
            (!minimumMarginKw.HasValue ||
             prospective.AppliedMarginKw >= minimumMarginKw.Value);
    }

    private static long EmergencyContribution(
        string assetId,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets) =>
        prospectiveAssets.TryGetValue(assetId, out ProspectiveAsset? prospective) &&
        prospective.State == ThermalOperatingState.Emergency
            ? 1
            : 0;

    private static PathCandidate? FindStaticShortestPath(
        AllocationContext context,
        string startNodeId,
        string endNodeId)
    {
        var first = new StaticRouteLabel(
            startNodeId,
            0,
            Array.AsReadOnly(new[] { startNodeId }),
            Array.Empty<string>());
        var best = new Dictionary<string, StaticRouteLabel>(StringComparer.Ordinal)
        {
            [startNodeId] = first,
        };
        var queue = new PriorityQueue<StaticRouteLabel, StaticRouteLabel>(
            StaticRouteLabelComparer.Instance);
        queue.Enqueue(first, first);
        while (queue.TryDequeue(out StaticRouteLabel? current, out _))
        {
            if (!ReferenceEquals(best[current.NodeId], current))
            {
                continue;
            }
            if (string.Equals(current.NodeId, endNodeId, StringComparison.Ordinal))
            {
                return BuildPath(
                    context,
                    current.NodeIds,
                    current.EdgeIds,
                    current.LengthUnit);
            }
            foreach (GraphArc arc in context.Adjacency[current.NodeId])
            {
                if (context.NodeClasses[context.Nodes[arc.OtherNodeId].ClassId].Kind ==
                    SpatialNodeKind.DedicatedLoadTerminal)
                {
                    continue;
                }
                var next = new StaticRouteLabel(
                    arc.OtherNodeId,
                    checked(current.LengthUnit + arc.LengthUnit),
                    Append(current.NodeIds, arc.OtherNodeId),
                    Append(current.EdgeIds, arc.EdgeId));
                if (best.TryGetValue(next.NodeId, out StaticRouteLabel? previous) &&
                    StaticRouteLabelComparer.Instance.Compare(previous, next) <= 0)
                {
                    continue;
                }
                best[next.NodeId] = next;
                queue.Enqueue(next, next);
            }
        }
        return null;
    }

    private static PathCandidate? FindStaticShortestServicePath(
        AllocationContext context,
        string sourceNodeId,
        string loadNodeId)
    {
        PathCandidate? best = null;
        foreach (string substationId in EligibleSubstationIds(context, loadNodeId))
        {
            PathCandidate? candidate = FindStaticShortestPath(
                context,
                sourceNodeId,
                substationId);
            if (candidate is not null &&
                (best is null || CompareStaticPaths(candidate, best) < 0))
            {
                best = candidate;
            }
        }
        return best;
    }

    private static int CompareStaticPaths(PathCandidate first, PathCandidate second)
    {
        int comparison = first.LengthUnit.CompareTo(second.LengthUnit);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = first.EdgeIds.Count.CompareTo(second.EdgeIds.Count);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = CompareOrdinalSequence(first.EdgeIds, second.EdgeIds);
        return comparison != 0
            ? comparison
            : CompareOrdinalSequence(first.NodeIds, second.NodeIds);
    }

    private static (ServiceRouteCost? Cost, IReadOnlyList<string> SubstationIds)
        FindBestServiceCost(
            AllocationContext context,
            ServicePathPolicy policy,
            string sourceNodeId,
            string endNodeId)
    {
        ServiceRouteCost? best = null;
        var substations = new List<string>();
        foreach (string substationId in EligibleSubstationIds(context, endNodeId))
        {
            ServiceRouteCost? cost = MinimumSinglePathCost(
                context,
                policy,
                sourceNodeId,
                substationId,
                EmptyIds);
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
        AllocationContext context,
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
            RouteLabel? route = FindBestPolicyPath(
                context,
                policy,
                sourceNodeId,
                substationId);
            if (route is null || RouteCost(route) != bestCost.Value)
            {
                throw new InvalidOperationException(
                    "The service-route path disagrees with its optimal cost.");
            }
            if (bestRoute is null ||
                RouteLabelComparer.Instance.Compare(route, bestRoute) < 0)
            {
                bestRoute = route;
            }
        }
        return bestRoute;
    }

    private static IReadOnlyList<string> EligibleSubstationIds(
        AllocationContext context,
        string endNodeId) => context.Nodes.Values
        .Where(node => IsEligibleSubstation(context, node.NodeId, endNodeId))
        .Select(node => node.NodeId)
        .OrderBy(nodeId => nodeId, StringComparer.Ordinal)
        .ToArray();

    private static RouteLabel? FindBestPolicyPath(
        AllocationContext context,
        ServicePathPolicy policy,
        string sourceNodeId,
        string endNodeId)
    {
        if (!policy.NodeAllowed(sourceNodeId) || !policy.NodeAllowed(endNodeId))
        {
            return null;
        }
        var first = new RouteLabel(
            sourceNodeId,
            checked((int)policy.NodeCost(sourceNodeId).EmergencyCount),
            0,
            Array.AsReadOnly(new[] { sourceNodeId }),
            Array.Empty<string>());
        var best = new Dictionary<string, RouteLabel>(StringComparer.Ordinal)
        {
            [sourceNodeId] = first,
        };
        var queue = new PriorityQueue<RouteLabel, RouteLabel>(RouteLabelComparer.Instance);
        queue.Enqueue(first, first);
        while (queue.TryDequeue(out RouteLabel? current, out _))
        {
            if (!ReferenceEquals(best[current.NodeId], current))
            {
                continue;
            }
            if (string.Equals(current.NodeId, endNodeId, StringComparison.Ordinal))
            {
                return current;
            }
            foreach (GraphArc arc in context.Adjacency[current.NodeId])
            {
                if (!policy.EdgeAllowed(arc.EdgeId) ||
                    !policy.NodeAllowed(arc.OtherNodeId))
                {
                    continue;
                }
                ServiceRouteCost added = policy.EdgeCost(arc.EdgeId, arc.LengthUnit)
                    .Add(policy.NodeCost(arc.OtherNodeId));
                var next = new RouteLabel(
                    arc.OtherNodeId,
                    checked(current.EmergencyAssetCount + (int)added.EmergencyCount),
                    checked(current.LengthUnit + added.LengthUnit),
                    Append(current.NodeIds, arc.OtherNodeId),
                    Append(current.EdgeIds, arc.EdgeId));
                if (best.TryGetValue(next.NodeId, out RouteLabel? previous) &&
                    RouteLabelComparer.Instance.Compare(previous, next) <= 0)
                {
                    continue;
                }
                best[next.NodeId] = next;
                queue.Enqueue(next, next);
            }
        }
        return null;
    }

    private static ServiceRouteCost RouteCost(RouteLabel route) => new(
        route.EmergencyAssetCount,
        route.LengthUnit,
        route.EdgeIds.Count);

    private static ServiceRouteCost? MinimumSinglePathCost(
        AllocationContext context,
        ServicePathPolicy policy,
        string startNodeId,
        string endNodeId,
        IReadOnlySet<string> bannedNodeIds)
    {
        if (bannedNodeIds.Contains(startNodeId) ||
            bannedNodeIds.Contains(endNodeId) ||
            !policy.NodeAllowed(startNodeId) ||
            !policy.NodeAllowed(endNodeId))
        {
            return null;
        }
        var distances = new Dictionary<string, ServiceRouteCost>(StringComparer.Ordinal)
        {
            [startNodeId] = policy.NodeCost(startNodeId),
        };
        var queue = new PriorityQueue<string, ServiceRouteCost>(
            ServiceRouteCostComparer.Instance);
        queue.Enqueue(startNodeId, distances[startNodeId]);
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
                if (distances.TryGetValue(
                        arc.OtherNodeId,
                        out ServiceRouteCost previous) &&
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

    private static IReadOnlyList<string> Append(
        IReadOnlyList<string> values,
        string value)
    {
        var result = new string[values.Count + 1];
        for (int index = 0; index < values.Count; index++)
        {
            result[index] = values[index];
        }
        result[^1] = value;
        return Array.AsReadOnly(result);
    }

    private static PathCandidate BuildPath(
        AllocationContext context,
        IReadOnlyList<string> nodeIds,
        IReadOnlyList<string> edgeIds,
        long lengthUnit)
    {
        var assetIds = new List<string>();
        for (int index = 0; index < nodeIds.Count; index++)
        {
            if (context.Assets.ContainsKey(nodeIds[index]))
            {
                assetIds.Add(nodeIds[index]);
            }
            if (index < edgeIds.Count)
            {
                assetIds.Add(edgeIds[index]);
            }
        }
        return new PathCandidate(
            Array.AsReadOnly(nodeIds.ToArray()),
            Array.AsReadOnly(edgeIds.ToArray()),
            Array.AsReadOnly(assetIds.ToArray()),
            lengthUnit);
    }

    private static bool IsEligibleSubstation(
        AllocationContext context,
        string nodeId,
        string loadNodeId)
    {
        SpatialNodeDefinition node = context.Nodes[nodeId];
        CommercialNodeClassDefinition nodeClass = context.NodeClasses[node.ClassId];
        return nodeClass.Kind == SpatialNodeKind.Substation &&
            nodeClass.ServiceRadiusUnit is int radius &&
            FixedGeometry.CeilDistance(
                node.Position,
                context.Nodes[loadNodeId].Position) <= radius;
    }

    private static ValidatedRequest ValidateRequest(
        AllocationContext context,
        ThermalIntervalRequest request,
        IReadOnlyList<string> protectiveOutageAssetIds)
    {
        RequireText(request.IntervalId, nameof(request));
        var loadIds = new HashSet<string>(StringComparer.Ordinal);
        long aggregateDemandKw = 0;
        foreach (ThermalLoadRequest load in request.Loads)
        {
            if (load is null)
            {
                throw new ArgumentException(
                    "Thermal interval contains a null load.",
                    nameof(request));
            }
            RequireText(load.LoadId, nameof(request));
            if (!loadIds.Add(load.LoadId))
            {
                throw new ArgumentException(
                    $"Thermal interval contains duplicate load ID '{load.LoadId}'.",
                    nameof(request));
            }
            if (!context.Loads.ContainsKey(load.LoadId))
            {
                throw new ArgumentException(
                    $"Thermal interval references unknown load '{load.LoadId}'.",
                    nameof(request));
            }
            if (load.DemandKw <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    $"Load '{load.LoadId}' demand must be positive.");
            }
            if (!Enum.IsDefined(load.Permission))
            {
                throw new ArgumentException(
                    $"Load '{load.LoadId}' has an unknown thermal permission.",
                    nameof(request));
            }
            try
            {
                aggregateDemandKw = checked(aggregateDemandKw + load.DemandKw);
            }
            catch (OverflowException exception)
            {
                throw new ArgumentException(
                    "Thermal interval aggregate demand exceeds Int64.",
                    nameof(request),
                    exception);
            }
        }

        HashSet<string> unavailableNodes = ValidateIds(
            request.UnavailableNodeIds,
            context.Nodes.Keys,
            "unavailable node",
            nameof(request));
        HashSet<string> unavailableEdges = ValidateIds(
            request.UnavailableEdgeIds,
            context.Edges.Keys,
            "unavailable edge",
            nameof(request));
        HashSet<string> protectiveOutages = ValidateIds(
            protectiveOutageAssetIds,
            context.Assets.Keys,
            "protective outage asset",
            nameof(protectiveOutageAssetIds));

        var overrides = new Dictionary<(ThermalAssetKind Kind, string ClassId), ThermalLimit>();
        foreach (ThermalLimitOverride item in request.LimitOverrides)
        {
            if (item is null)
            {
                throw new ArgumentException(
                    "Thermal interval contains a null limit override.",
                    nameof(request));
            }
            if (!Enum.IsDefined(item.AssetKind))
            {
                throw new ArgumentException("Unknown override asset kind.", nameof(request));
            }
            RequireText(item.ClassId, nameof(request));
            ThermalLimit? baseLimit = item.AssetKind switch
            {
                ThermalAssetKind.Node when context.NodeClasses.TryGetValue(
                    item.ClassId,
                    out CommercialNodeClassDefinition? nodeClass) => nodeClass.ThermalLimit,
                ThermalAssetKind.Edge when context.LineClasses.TryGetValue(
                    item.ClassId,
                    out CommercialLineClassDefinition? lineClass) => lineClass.ThermalLimit,
                _ => null,
            };
            if (baseLimit is null)
            {
                throw new ArgumentException(
                    $"Override references unknown or nonthermal {item.AssetKind} class " +
                    $"'{item.ClassId}'.",
                    nameof(request));
            }
            if (item.ContinuousKw <= 0 || item.EmergencyKw < item.ContinuousKw)
            {
                throw new ArgumentException(
                    $"Override for '{item.ClassId}' needs 0 < continuous <= emergency.",
                    nameof(request));
            }
            if (item.ContinuousKw > baseLimit.ContinuousKw ||
                item.EmergencyKw > baseLimit.EmergencyKw)
            {
                throw new ArgumentException(
                    $"Override for '{item.ClassId}' may only lower authored limits.",
                    nameof(request));
            }
            if (!overrides.TryAdd(
                    (item.AssetKind, item.ClassId),
                    new ThermalLimit(item.ContinuousKw, item.EmergencyKw)))
            {
                throw new ArgumentException(
                    $"Thermal interval duplicates override for " +
                    $"'{item.AssetKind}:{item.ClassId}'.",
                    nameof(request));
            }
        }

        Dictionary<string, ThermalLimit> limits = context.Assets.Values.ToDictionary(
            asset => asset.AssetId,
            asset => overrides.TryGetValue(
                (asset.AssetKind, asset.ClassId),
                out ThermalLimit? current)
                ? current
                : asset.BaseLimit,
            StringComparer.Ordinal);
        _ = aggregateDemandKw;
        return new ValidatedRequest(
            request,
            unavailableNodes,
            unavailableEdges,
            protectiveOutages,
            limits);
    }

    private static HashSet<string> ValidateIds(
        IReadOnlyList<string> values,
        IEnumerable<string> knownIds,
        string kind,
        string parameterName)
    {
        var known = knownIds.ToHashSet(StringComparer.Ordinal);
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireText(value, parameterName);
            if (!known.Contains(value))
            {
                throw new ArgumentException(
                    $"Thermal interval references unknown {kind} '{value}'.",
                    parameterName);
            }
            if (!result.Add(value))
            {
                throw new ArgumentException(
                    $"Thermal interval duplicates {kind} '{value}'.",
                    parameterName);
            }
        }
        return result;
    }

    private static void RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw new ArgumentException(
                "Thermal identifiers must be nonblank and trimmed.",
                parameterName);
        }
    }

    private static int CompareOrdinalSequence(
        IReadOnlyList<string> first,
        IReadOnlyList<string> second)
    {
        int count = Math.Min(first.Count, second.Count);
        for (int index = 0; index < count; index++)
        {
            int comparison = string.CompareOrdinal(first[index], second[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }
        return first.Count.CompareTo(second.Count);
    }

    private static void VerifyStableOrderAndAccounting(
        CommercialWorldDefinition world,
        ThermalIntervalRequest request,
        ThermalIntervalEvaluation evaluation)
    {
        if (!evaluation.Loads.Select(item => item.LoadId).SequenceEqual(
                request.Loads.Select(item => item.LoadId),
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Realtime supply allocation changed the authored stable load order.");
        }

        string[] expectedAssetOrder = evaluation.Assets
            .Select(item => item.AssetId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (!evaluation.Assets.Select(item => item.AssetId).SequenceEqual(
                expectedAssetOrder,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Realtime supply allocation changed stable asset order.");
        }
        string[] expectedSourceOrder = world.Sources
            .OrderBy(item => item.DispatchOrder)
            .ThenBy(item => item.SourceId, StringComparer.Ordinal)
            .Select(item => item.SourceId)
            .ToArray();
        if (!evaluation.Sources.Select(item => item.SourceId).SequenceEqual(
                expectedSourceOrder,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Realtime supply allocation changed stable source order.");
        }

        Dictionary<string, long> expectedSources = world.Sources.ToDictionary(
            item => item.SourceId,
            _ => 0L,
            StringComparer.Ordinal);
        Dictionary<string, long> expectedAssets = evaluation.Assets.ToDictionary(
            item => item.AssetId,
            _ => 0L,
            StringComparer.Ordinal);
        foreach (ThermalLoadSupply supply in evaluation.Loads)
        {
            if (supply.DeliveredKw == 0)
            {
                if (supply.Failure is null || supply.SourceId is not null)
                {
                    throw new InvalidOperationException(
                        $"Rejected realtime supply '{supply.LoadId}' is not a full rollback.");
                }
                continue;
            }
            if (supply.Failure is not null || supply.DeliveredKw != supply.DemandKw ||
                supply.SourceId is null)
            {
                throw new InvalidOperationException(
                    $"Realtime supply '{supply.LoadId}' is neither atomic success nor rollback.");
            }
            expectedSources[supply.SourceId] = checked(
                expectedSources[supply.SourceId] + supply.DeliveredKw);
            foreach (string assetId in supply.PathNodeIds.Concat(supply.PathEdgeIds))
            {
                if (expectedAssets.ContainsKey(assetId))
                {
                    expectedAssets[assetId] = checked(
                        expectedAssets[assetId] + supply.DeliveredKw);
                }
            }
        }

        if (evaluation.Sources.Any(item => expectedSources[item.SourceId] != item.UsedKw) ||
            evaluation.Assets.Any(item => expectedAssets[item.AssetId] != item.UsedKw))
        {
            throw new InvalidOperationException(
                "Rejected route usage leaked into realtime source or asset accounting.");
        }
    }

    private sealed class AllocationContext
    {
        public AllocationContext(CommercialWorldDefinition world)
        {
            Nodes = world.Nodes.ToDictionary(item => item.NodeId, StringComparer.Ordinal);
            Edges = world.Edges.ToDictionary(item => item.EdgeId, StringComparer.Ordinal);
            NodeClasses = world.NodeClasses.ToDictionary(
                item => item.ClassId,
                StringComparer.Ordinal);
            LineClasses = world.LineClasses.ToDictionary(
                item => item.ClassId,
                StringComparer.Ordinal);
            Sources = world.Sources.ToDictionary(item => item.SourceId, StringComparer.Ordinal);
            Loads = world.Loads.ToDictionary(item => item.LoadId, StringComparer.Ordinal);
            OrderedSources = world.Sources
                .OrderBy(item => item.DispatchOrder)
                .ThenBy(item => item.SourceId, StringComparer.Ordinal)
                .ToArray();

            Assets = new Dictionary<string, ThermalAssetInfo>(StringComparer.Ordinal);
            foreach (SpatialNodeDefinition node in world.Nodes)
            {
                CommercialNodeClassDefinition nodeClass = NodeClasses[node.ClassId];
                if (nodeClass.ThermalLimit is not null)
                {
                    Assets.Add(node.NodeId, new ThermalAssetInfo(
                        node.NodeId,
                        ThermalAssetKind.Node,
                        node.ClassId,
                        nodeClass.ThermalLimit));
                }
            }
            foreach (SpatialEdgeDefinition edge in world.Edges)
            {
                CommercialLineClassDefinition lineClass = LineClasses[edge.LineClassId];
                Assets.Add(edge.EdgeId, new ThermalAssetInfo(
                    edge.EdgeId,
                    ThermalAssetKind.Edge,
                    edge.LineClassId,
                    lineClass.ThermalLimit));
            }

            Adjacency = world.Nodes.ToDictionary(
                item => item.NodeId,
                _ => new List<GraphArc>(),
                StringComparer.Ordinal);
            foreach (SpatialEdgeDefinition edge in world.Edges)
            {
                long lengthUnit = FixedGeometry.CeilDistance(
                    Nodes[edge.FromNodeId].Position,
                    Nodes[edge.ToNodeId].Position);
                Adjacency[edge.FromNodeId].Add(new GraphArc(
                    edge.EdgeId,
                    edge.ToNodeId,
                    lengthUnit));
                Adjacency[edge.ToNodeId].Add(new GraphArc(
                    edge.EdgeId,
                    edge.FromNodeId,
                    lengthUnit));
            }
            foreach (List<GraphArc> arcs in Adjacency.Values)
            {
                arcs.Sort(static (first, second) =>
                {
                    int comparison = string.CompareOrdinal(first.EdgeId, second.EdgeId);
                    return comparison != 0
                        ? comparison
                        : string.CompareOrdinal(first.OtherNodeId, second.OtherNodeId);
                });
            }
        }

        public Dictionary<string, SpatialNodeDefinition> Nodes { get; }

        public Dictionary<string, SpatialEdgeDefinition> Edges { get; }

        public Dictionary<string, CommercialNodeClassDefinition> NodeClasses { get; }

        public Dictionary<string, CommercialLineClassDefinition> LineClasses { get; }

        public Dictionary<string, CommercialSourceDefinition> Sources { get; }

        public Dictionary<string, CommercialLoadDefinition> Loads { get; }

        public IReadOnlyList<CommercialSourceDefinition> OrderedSources { get; }

        public Dictionary<string, ThermalAssetInfo> Assets { get; }

        public Dictionary<string, List<GraphArc>> Adjacency { get; }
    }

    private sealed record ThermalAssetInfo(
        string AssetId,
        ThermalAssetKind AssetKind,
        string ClassId,
        ThermalLimit BaseLimit);

    private sealed record GraphArc(string EdgeId, string OtherNodeId, long LengthUnit);

    private sealed record PathCandidate(
        IReadOnlyList<string> NodeIds,
        IReadOnlyList<string> EdgeIds,
        IReadOnlyList<string> AssetIds,
        long LengthUnit);

    private sealed record ProspectiveAsset(
        ThermalOperatingState State,
        long AppliedMarginKw,
        bool ContinuousEligible,
        bool EmergencyEligible);

    private sealed record RouteLabel(
        string NodeId,
        int EmergencyAssetCount,
        long LengthUnit,
        IReadOnlyList<string> NodeIds,
        IReadOnlyList<string> EdgeIds);

    private sealed class RouteLabelComparer : IComparer<RouteLabel>
    {
        public static RouteLabelComparer Instance { get; } = new();

        public int Compare(RouteLabel? first, RouteLabel? second)
        {
            if (ReferenceEquals(first, second))
            {
                return 0;
            }
            if (first is null)
            {
                return -1;
            }
            if (second is null)
            {
                return 1;
            }
            int comparison = first.EmergencyAssetCount.CompareTo(
                second.EmergencyAssetCount);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = first.LengthUnit.CompareTo(second.LengthUnit);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = first.EdgeIds.Count.CompareTo(second.EdgeIds.Count);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = CompareOrdinalSequence(first.EdgeIds, second.EdgeIds);
            return comparison != 0
                ? comparison
                : CompareOrdinalSequence(first.NodeIds, second.NodeIds);
        }
    }

    private sealed record StaticRouteLabel(
        string NodeId,
        long LengthUnit,
        IReadOnlyList<string> NodeIds,
        IReadOnlyList<string> EdgeIds);

    private sealed class StaticRouteLabelComparer : IComparer<StaticRouteLabel>
    {
        public static StaticRouteLabelComparer Instance { get; } = new();

        public int Compare(StaticRouteLabel? first, StaticRouteLabel? second)
        {
            if (ReferenceEquals(first, second))
            {
                return 0;
            }
            if (first is null)
            {
                return -1;
            }
            if (second is null)
            {
                return 1;
            }
            int comparison = first.LengthUnit.CompareTo(second.LengthUnit);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = first.EdgeIds.Count.CompareTo(second.EdgeIds.Count);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = CompareOrdinalSequence(first.EdgeIds, second.EdgeIds);
            return comparison != 0
                ? comparison
                : CompareOrdinalSequence(first.NodeIds, second.NodeIds);
        }
    }

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

        public int CompareTo(ServiceRouteCost other)
        {
            int comparison = EmergencyCount.CompareTo(other.EmergencyCount);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = LengthUnit.CompareTo(other.LengthUnit);
            return comparison != 0
                ? comparison
                : EdgeCount.CompareTo(other.EdgeCount);
        }
    }

    private sealed class ServiceRouteCostComparer : IComparer<ServiceRouteCost>
    {
        public static ServiceRouteCostComparer Instance { get; } = new();

        public int Compare(ServiceRouteCost first, ServiceRouteCost second) =>
            first.CompareTo(second);
    }

    private sealed record CandidateRejection(
        PathCandidate Path,
        ThermalSupplyFailure Failure);

    private readonly record struct CandidateQuality(
        ThermalOperatingState ThermalGrade,
        int EmergencyAssetCount,
        long MinimumRemainingKw);

    private sealed record AcceptedCandidate(
        CommercialSourceDefinition Source,
        PathCandidate Path,
        CandidateQuality Quality);

    private sealed class AcceptedCandidateComparer : IComparer<AcceptedCandidate>
    {
        public static AcceptedCandidateComparer Instance { get; } = new();

        public int Compare(AcceptedCandidate? first, AcceptedCandidate? second)
        {
            if (ReferenceEquals(first, second))
            {
                return 0;
            }
            if (first is null)
            {
                return -1;
            }
            if (second is null)
            {
                return 1;
            }
            int comparison = first.Quality.ThermalGrade.CompareTo(
                second.Quality.ThermalGrade);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = first.Quality.EmergencyAssetCount.CompareTo(
                second.Quality.EmergencyAssetCount);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = second.Quality.MinimumRemainingKw.CompareTo(
                first.Quality.MinimumRemainingKw);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = first.Source.DispatchOrder.CompareTo(second.Source.DispatchOrder);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = first.Path.LengthUnit.CompareTo(second.Path.LengthUnit);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = first.Path.EdgeIds.Count.CompareTo(second.Path.EdgeIds.Count);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = CompareOrdinalSequence(
                first.Path.EdgeIds,
                second.Path.EdgeIds);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = string.CompareOrdinal(
                first.Path.NodeIds[^1],
                second.Path.NodeIds[^1]);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = string.CompareOrdinal(
                first.Source.SourceId,
                second.Source.SourceId);
            return comparison != 0
                ? comparison
                : CompareOrdinalSequence(first.Path.NodeIds, second.Path.NodeIds);
        }
    }

    private sealed record ValidatedRequest(
        ThermalIntervalRequest Request,
        HashSet<string> UnavailableNodeIds,
        HashSet<string> UnavailableEdgeIds,
        HashSet<string> ProtectiveOutageAssetIds,
        IReadOnlyDictionary<string, ThermalLimit> Limits);
}
