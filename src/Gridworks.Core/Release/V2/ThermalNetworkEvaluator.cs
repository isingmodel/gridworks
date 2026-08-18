namespace Gridworks.Core.Release.V2;

public static class ThermalNetworkEvaluator
{
    public static ThermalIntervalEvaluation EvaluateInterval(
        CommercialWorldDefinition world,
        ThermalIntervalRequest request,
        ThermalState initialState)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(initialState);

        CommercialWorldLoader.Validate(world);
        var context = new EvaluationContext(world);
        ValidateState(context, initialState);
        ValidatedInterval validated = ValidateInterval(context, request);
        return EvaluateInterval(context, validated, initialState);
    }

    public static ThermalSequenceEvaluation EvaluateSequence(
        CommercialWorldDefinition world,
        ThermalSequenceRequest request,
        ThermalState initialState)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(initialState);

        CommercialWorldLoader.Validate(world);
        var context = new EvaluationContext(world);
        ValidateState(context, initialState);
        if (request.Intervals.Count == 0)
        {
            throw new ArgumentException(
                "A thermal sequence must contain at least one interval.",
                nameof(request));
        }

        var intervalIds = new HashSet<string>(StringComparer.Ordinal);
        ValidatedInterval[] intervals = new ValidatedInterval[request.Intervals.Count];
        for (int index = 0; index < request.Intervals.Count; index++)
        {
            ThermalIntervalRequest interval = request.Intervals[index];
            if (interval is null)
            {
                throw new ArgumentException(
                    $"Thermal sequence interval {index} is null.",
                    nameof(request));
            }
            intervals[index] = ValidateInterval(context, interval);
            if (!intervalIds.Add(interval.IntervalId))
            {
                throw new ArgumentException(
                    $"Thermal sequence contains duplicate interval ID '{interval.IntervalId}'.",
                    nameof(request));
            }
        }

        var evaluations = new ThermalIntervalEvaluation[intervals.Length];
        ThermalState state = initialState;
        for (int index = 0; index < intervals.Length; index++)
        {
            evaluations[index] = EvaluateInterval(context, intervals[index], state);
            state = evaluations[index].NextThermalState;
        }
        return new ThermalSequenceEvaluation(evaluations, state);
    }

    private static ThermalIntervalEvaluation EvaluateInterval(
        EvaluationContext context,
        ValidatedInterval interval,
        ThermalState initialState)
    {
        var cooling = initialState.CoolingAssetIds.ToHashSet(StringComparer.Ordinal);
        var assetUse = context.Assets.Keys.ToDictionary(
            assetId => assetId,
            _ => 0L,
            StringComparer.Ordinal);
        var sourceUse = context.Sources.Keys.ToDictionary(
            sourceId => sourceId,
            _ => 0L,
            StringComparer.Ordinal);
        var supplies = new List<ThermalLoadSupply>(interval.Request.Loads.Count);

        foreach (ThermalLoadRequest loadRequest in interval.Request.Loads)
        {
            CommercialLoadDefinition load = context.Loads[loadRequest.LoadId];
            CandidateAssessment? best = null;
            FailedCandidate? bestFailure = null;
            bool topologyPathFound = false;
            IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets =
                BuildProspectiveAssets(context, interval, assetUse, loadRequest.DemandKw);

            foreach (CommercialSourceDefinition source in context.OrderedSources)
            {
                PathCandidate? diagnosticPath = FindStaticShortestPath(
                    context,
                    source,
                    load.NodeId);
                if (diagnosticPath is not null)
                {
                    topologyPathFound = true;
                    FailedCandidate? failure = AssessFailure(
                        context,
                        interval,
                        cooling,
                        assetUse,
                        sourceUse,
                        source,
                        diagnosticPath,
                        loadRequest);
                    if (failure is not null &&
                        (bestFailure is null || CompareFailed(failure, bestFailure) < 0))
                    {
                        bestFailure = failure;
                    }
                }

                PathCandidate? availableDiagnosticPath = FindStaticShortestPath(
                    context,
                    source,
                    load.NodeId,
                    interval,
                    cooling);
                if (availableDiagnosticPath is not null)
                {
                    FailedCandidate? failure = AssessFailure(
                        context,
                        interval,
                        cooling,
                        assetUse,
                        sourceUse,
                        source,
                        availableDiagnosticPath,
                        loadRequest);
                    if (failure is not null &&
                        (bestFailure is null || CompareFailed(failure, bestFailure) < 0))
                    {
                        bestFailure = failure;
                    }
                }

                CandidateAssessment? accepted = FindBestAcceptedPath(
                    context,
                    interval,
                    cooling,
                    sourceUse,
                    source,
                    loadRequest,
                    prospectiveAssets);
                if (accepted is not null &&
                    (best is null || CompareAccepted(accepted, best) < 0))
                {
                    best = accepted;
                }
            }

            if (best is null)
            {
                ThermalSupplyFailure failure = topologyPathFound && bestFailure is not null
                    ? bestFailure.Failure
                    : new ThermalSupplyFailure(
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
                    bestFailure?.Path.NodeIds ?? Array.Empty<string>(),
                    bestFailure?.Path.EdgeIds ?? Array.Empty<string>(),
                    null,
                    failure));
                continue;
            }

            sourceUse[best.Source.SourceId] = checked(
                sourceUse[best.Source.SourceId] + loadRequest.DemandKw);
            foreach (string assetId in best.Path.AssetIds)
            {
                assetUse[assetId] = checked(assetUse[assetId] + loadRequest.DemandKw);
            }
            supplies.Add(new ThermalLoadSupply(
                loadRequest.LoadId,
                loadRequest.DemandKw,
                loadRequest.DemandKw,
                best.Source.SourceId,
                best.Path.NodeIds,
                best.Path.EdgeIds,
                best.MinimumRemainingKw,
                null));
        }

        var nextCooling = new List<string>();
        ThermalAssetUsage[] assets = context.Assets.Values
            .OrderBy(asset => asset.AssetId, StringComparer.Ordinal)
            .Select(asset =>
            {
                ThermalLimit limit = interval.Limits[asset.AssetId];
                long used = assetUse[asset.AssetId];
                ThermalOperatingState state;
                ThermalOperatingState nextState;
                if (cooling.Contains(asset.AssetId))
                {
                    state = ThermalOperatingState.ProtectiveOutage;
                    nextState = ThermalOperatingState.Continuous;
                }
                else if (used > limit.EmergencyKw)
                {
                    state = ThermalOperatingState.OverLimit;
                    nextState = ThermalOperatingState.ProtectiveOutage;
                    nextCooling.Add(asset.AssetId);
                }
                else if (used > limit.ContinuousKw)
                {
                    state = ThermalOperatingState.Emergency;
                    nextState = ThermalOperatingState.ProtectiveOutage;
                    nextCooling.Add(asset.AssetId);
                }
                else
                {
                    state = ThermalOperatingState.Continuous;
                    nextState = ThermalOperatingState.Continuous;
                }
                return new ThermalAssetUsage(
                    asset.AssetId,
                    asset.AssetKind,
                    used,
                    limit.ContinuousKw,
                    limit.EmergencyKw,
                    state,
                    nextState);
            })
            .ToArray();
        nextCooling.Sort(StringComparer.Ordinal);

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
            new ThermalState(nextCooling));
    }

    private static IReadOnlyDictionary<string, ProspectiveAsset> BuildProspectiveAssets(
        EvaluationContext context,
        ValidatedInterval interval,
        IReadOnlyDictionary<string, long> assetUse,
        long demandKw)
    {
        var result = new Dictionary<string, ProspectiveAsset>(StringComparer.Ordinal);
        foreach (string assetId in context.Assets.Keys)
        {
            ThermalLimit limit = interval.Limits[assetId];
            long prospectiveUse = checked(assetUse[assetId] + demandKw);
            if (prospectiveUse <= limit.ContinuousKw)
            {
                result.Add(assetId, new ProspectiveAsset(
                    ThermalOperatingState.Continuous,
                    checked(limit.ContinuousKw - prospectiveUse),
                    true,
                    true));
            }
            else if (prospectiveUse <= limit.EmergencyKw)
            {
                result.Add(assetId, new ProspectiveAsset(
                    ThermalOperatingState.Emergency,
                    checked(limit.EmergencyKw - prospectiveUse),
                    false,
                    true));
            }
            else
            {
                result.Add(assetId, new ProspectiveAsset(
                    ThermalOperatingState.OverLimit,
                    0,
                    false,
                    false));
            }
        }
        return result;
    }

    private static CandidateAssessment? FindBestAcceptedPath(
        EvaluationContext context,
        ValidatedInterval interval,
        IReadOnlySet<string> cooling,
        IReadOnlyDictionary<string, long> sourceUse,
        CommercialSourceDefinition source,
        ThermalLoadRequest load,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets)
    {
        long sourceRemaining = checked(source.CapacityKw - sourceUse[source.SourceId]);
        if (load.DemandKw > sourceRemaining)
        {
            return null;
        }

        bool allowEmergency = false;
        int? minimumEmergencyCount = FindMinimumEmergencyCount(
            context,
            interval,
            cooling,
            prospectiveAssets,
            source.NodeId,
            context.Loads[load.LoadId].NodeId,
            false,
            null);
        ThermalOperatingState grade = ThermalOperatingState.Continuous;
        if (minimumEmergencyCount is null)
        {
            if (load.Permission != ThermalPermission.EmergencyAllowed)
            {
                return null;
            }
            allowEmergency = true;
            grade = ThermalOperatingState.Emergency;
            minimumEmergencyCount = FindMinimumEmergencyCount(
                context,
                interval,
                cooling,
                prospectiveAssets,
                source.NodeId,
                context.Loads[load.LoadId].NodeId,
                true,
                null);
            if (minimumEmergencyCount is null)
            {
                return null;
            }
        }

        long? maximumBottleneck = null;
        IEnumerable<long> candidateMargins = context.Assets.Values
            .Where(asset => IsThermalAssetAllowed(
                asset,
                interval,
                cooling,
                prospectiveAssets,
                allowEmergency,
                null))
            .Select(asset => prospectiveAssets[asset.AssetId].AppliedMarginKw)
            .Distinct()
            .OrderByDescending(value => value);
        foreach (long candidateMargin in candidateMargins)
        {
            int? count = FindMinimumEmergencyCount(
                context,
                interval,
                cooling,
                prospectiveAssets,
                source.NodeId,
                context.Loads[load.LoadId].NodeId,
                allowEmergency,
                candidateMargin);
            if (count == minimumEmergencyCount)
            {
                maximumBottleneck = candidateMargin;
                break;
            }
        }
        if (maximumBottleneck is null)
        {
            throw new InvalidOperationException(
                "A reachable source-to-load path must have a thermal bottleneck.");
        }

        RouteLabel? route = FindBestQualifiedPath(
            context,
            interval,
            cooling,
            prospectiveAssets,
            source.NodeId,
            context.Loads[load.LoadId].NodeId,
            allowEmergency,
            maximumBottleneck.Value);
        if (route is null || route.EmergencyAssetCount != minimumEmergencyCount.Value)
        {
            throw new InvalidOperationException(
                "Qualified thermal route selection disagrees with its reachability pass.");
        }
        return CandidateAssessment.AcceptedCandidate(
            source,
            BuildPath(context, route.NodeIds, route.EdgeIds, route.LengthUnit),
            grade,
            route.EmergencyAssetCount,
            maximumBottleneck.Value);
    }

    private static int? FindMinimumEmergencyCount(
        EvaluationContext context,
        ValidatedInterval interval,
        IReadOnlySet<string> cooling,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets,
        string sourceNodeId,
        string endNodeId,
        bool allowEmergency,
        long? minimumMarginKw)
    {
        if (!IsNodeAllowed(
                context,
                interval,
                cooling,
                prospectiveAssets,
                sourceNodeId,
                allowEmergency,
                minimumMarginKw))
        {
            return null;
        }

        int initialCount = EmergencyContribution(sourceNodeId, prospectiveAssets);
        var best = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [sourceNodeId] = initialCount,
        };
        var queue = new PriorityQueue<string, int>();
        queue.Enqueue(sourceNodeId, initialCount);
        while (queue.TryDequeue(out string? nodeId, out int currentCount))
        {
            if (best[nodeId] != currentCount)
            {
                continue;
            }
            if (string.Equals(nodeId, endNodeId, StringComparison.Ordinal))
            {
                return currentCount;
            }

            foreach (GraphArc arc in context.Adjacency[nodeId])
            {
                if (!IsEdgeAllowed(
                        context,
                        interval,
                        cooling,
                        prospectiveAssets,
                        arc.EdgeId,
                        allowEmergency,
                        minimumMarginKw) ||
                    !IsNodeAllowed(
                        context,
                        interval,
                        cooling,
                        prospectiveAssets,
                        arc.OtherNodeId,
                        allowEmergency,
                        minimumMarginKw))
                {
                    continue;
                }
                int nextCount = checked(
                    currentCount +
                    EmergencyContribution(arc.EdgeId, prospectiveAssets) +
                    EmergencyContribution(arc.OtherNodeId, prospectiveAssets));
                if (best.TryGetValue(arc.OtherNodeId, out int previous) &&
                    previous <= nextCount)
                {
                    continue;
                }
                best[arc.OtherNodeId] = nextCount;
                queue.Enqueue(arc.OtherNodeId, nextCount);
            }
        }
        return null;
    }

    private static RouteLabel? FindBestQualifiedPath(
        EvaluationContext context,
        ValidatedInterval interval,
        IReadOnlySet<string> cooling,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets,
        string sourceNodeId,
        string endNodeId,
        bool allowEmergency,
        long minimumMarginKw)
    {
        if (!IsNodeAllowed(
                context,
                interval,
                cooling,
                prospectiveAssets,
                sourceNodeId,
                allowEmergency,
                minimumMarginKw))
        {
            return null;
        }

        var first = new RouteLabel(
            sourceNodeId,
            EmergencyContribution(sourceNodeId, prospectiveAssets),
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
                if (!IsEdgeAllowed(
                        context,
                        interval,
                        cooling,
                        prospectiveAssets,
                        arc.EdgeId,
                        allowEmergency,
                        minimumMarginKw) ||
                    !IsNodeAllowed(
                        context,
                        interval,
                        cooling,
                        prospectiveAssets,
                        arc.OtherNodeId,
                        allowEmergency,
                        minimumMarginKw))
                {
                    continue;
                }
                var next = new RouteLabel(
                    arc.OtherNodeId,
                    checked(
                        current.EmergencyAssetCount +
                        EmergencyContribution(arc.EdgeId, prospectiveAssets) +
                        EmergencyContribution(arc.OtherNodeId, prospectiveAssets)),
                    checked(current.LengthUnit + arc.LengthUnit),
                    Append(current.NodeIds, arc.OtherNodeId),
                    Append(current.EdgeIds, arc.EdgeId));
                if (best.TryGetValue(arc.OtherNodeId, out RouteLabel? previous) &&
                    RouteLabelComparer.Instance.Compare(previous, next) <= 0)
                {
                    continue;
                }
                best[arc.OtherNodeId] = next;
                queue.Enqueue(next, next);
            }
        }
        return null;
    }

    private static PathCandidate? FindStaticShortestPath(
        EvaluationContext context,
        CommercialSourceDefinition source,
        string endNodeId,
        ValidatedInterval? availableInterval = null,
        IReadOnlySet<string>? cooling = null)
    {
        if ((availableInterval is null) != (cooling is null))
        {
            throw new InvalidOperationException(
                "Static path availability inputs must be supplied together.");
        }
        if (availableInterval is not null &&
            !IsDiagnosticNodeAvailable(context, availableInterval, cooling!, source.NodeId))
        {
            return null;
        }

        var first = new StaticRouteLabel(
            source.NodeId,
            0,
            Array.AsReadOnly(new[] { source.NodeId }),
            Array.Empty<string>());
        var best = new Dictionary<string, StaticRouteLabel>(StringComparer.Ordinal)
        {
            [source.NodeId] = first,
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
                if (availableInterval is not null &&
                    (!IsDiagnosticEdgeAvailable(availableInterval, cooling!, arc.EdgeId) ||
                     !IsDiagnosticNodeAvailable(
                         context,
                         availableInterval,
                         cooling!,
                         arc.OtherNodeId)))
                {
                    continue;
                }
                var next = new StaticRouteLabel(
                    arc.OtherNodeId,
                    checked(current.LengthUnit + arc.LengthUnit),
                    Append(current.NodeIds, arc.OtherNodeId),
                    Append(current.EdgeIds, arc.EdgeId));
                if (best.TryGetValue(arc.OtherNodeId, out StaticRouteLabel? previous) &&
                    StaticRouteLabelComparer.Instance.Compare(previous, next) <= 0)
                {
                    continue;
                }
                best[arc.OtherNodeId] = next;
                queue.Enqueue(next, next);
            }
        }
        return null;
    }

    private static bool IsDiagnosticNodeAvailable(
        EvaluationContext context,
        ValidatedInterval interval,
        IReadOnlySet<string> cooling,
        string nodeId) =>
        !interval.UnavailableNodeIds.Contains(nodeId) &&
        (!context.Assets.ContainsKey(nodeId) || !cooling.Contains(nodeId));

    private static bool IsDiagnosticEdgeAvailable(
        ValidatedInterval interval,
        IReadOnlySet<string> cooling,
        string edgeId) =>
        !interval.UnavailableEdgeIds.Contains(edgeId) &&
        !cooling.Contains(edgeId);

    private static bool IsNodeAllowed(
        EvaluationContext context,
        ValidatedInterval interval,
        IReadOnlySet<string> cooling,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets,
        string nodeId,
        bool allowEmergency,
        long? minimumMarginKw)
    {
        if (interval.UnavailableNodeIds.Contains(nodeId))
        {
            return false;
        }
        return !context.Assets.TryGetValue(nodeId, out ThermalAssetInfo? asset) ||
            IsThermalAssetAllowed(
                asset,
                interval,
                cooling,
                prospectiveAssets,
                allowEmergency,
                minimumMarginKw);
    }

    private static bool IsEdgeAllowed(
        EvaluationContext context,
        ValidatedInterval interval,
        IReadOnlySet<string> cooling,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets,
        string edgeId,
        bool allowEmergency,
        long? minimumMarginKw)
    {
        if (interval.UnavailableEdgeIds.Contains(edgeId))
        {
            return false;
        }
        return IsThermalAssetAllowed(
            context.Assets[edgeId],
            interval,
            cooling,
            prospectiveAssets,
            allowEmergency,
            minimumMarginKw);
    }

    private static bool IsThermalAssetAllowed(
        ThermalAssetInfo asset,
        ValidatedInterval interval,
        IReadOnlySet<string> cooling,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets,
        bool allowEmergency,
        long? minimumMarginKw)
    {
        bool unavailable = asset.AssetKind switch
        {
            ThermalAssetKind.Node => interval.UnavailableNodeIds.Contains(asset.AssetId),
            ThermalAssetKind.Edge => interval.UnavailableEdgeIds.Contains(asset.AssetId),
            _ => throw new InvalidOperationException("Unknown thermal asset kind."),
        };
        if (unavailable || cooling.Contains(asset.AssetId))
        {
            return false;
        }
        ProspectiveAsset prospective = prospectiveAssets[asset.AssetId];
        bool thermallyEligible = allowEmergency
            ? prospective.EmergencyEligible
            : prospective.ContinuousEligible;
        return thermallyEligible &&
            (!minimumMarginKw.HasValue ||
             prospective.AppliedMarginKw >= minimumMarginKw.Value);
    }

    private static int EmergencyContribution(
        string assetId,
        IReadOnlyDictionary<string, ProspectiveAsset> prospectiveAssets) =>
        prospectiveAssets.TryGetValue(assetId, out ProspectiveAsset? prospective) &&
        prospective.State == ThermalOperatingState.Emergency
            ? 1
            : 0;

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

    private static FailedCandidate? AssessFailure(
        EvaluationContext context,
        ValidatedInterval interval,
        IReadOnlySet<string> cooling,
        IReadOnlyDictionary<string, long> assetUse,
        IReadOnlyDictionary<string, long> sourceUse,
        CommercialSourceDefinition source,
        PathCandidate path,
        ThermalLoadRequest load)
    {
        CandidateAssessment assessment = Assess(
            context,
            interval,
            cooling,
            assetUse,
            sourceUse,
            source,
            path,
            load);
        return assessment.Accepted
            ? null
            : new FailedCandidate(path, source, assessment.Failure!);
    }

    private static CandidateAssessment Assess(
        EvaluationContext context,
        ValidatedInterval interval,
        IReadOnlySet<string> cooling,
        IReadOnlyDictionary<string, long> assetUse,
        IReadOnlyDictionary<string, long> sourceUse,
        CommercialSourceDefinition source,
        PathCandidate path,
        ThermalLoadRequest load)
    {
        foreach (PathStep step in path.Steps)
        {
            bool unavailable = step.AssetKind switch
            {
                null => interval.UnavailableNodeIds.Contains(step.AssetId),
                ThermalAssetKind.Node =>
                    interval.UnavailableNodeIds.Contains(step.AssetId) ||
                    cooling.Contains(step.AssetId),
                ThermalAssetKind.Edge =>
                    interval.UnavailableEdgeIds.Contains(step.AssetId) ||
                    cooling.Contains(step.AssetId),
                _ => throw new InvalidOperationException("Unknown thermal asset kind."),
            };
            if (unavailable)
            {
                return CandidateAssessment.Rejected(
                    source,
                    path,
                    new ThermalSupplyFailure(
                        ThermalFailureKind.AssetUnavailable,
                        source.SourceId,
                        step.AssetId,
                        load.DemandKw,
                        0));
            }
        }

        long sourceRemaining = checked(source.CapacityKw - sourceUse[source.SourceId]);
        if (load.DemandKw > sourceRemaining)
        {
            return CandidateAssessment.Rejected(
                source,
                path,
                new ThermalSupplyFailure(
                    ThermalFailureKind.SourceCapacity,
                    source.SourceId,
                    null,
                    load.DemandKw,
                    sourceRemaining));
        }

        int emergencyCount = 0;
        long minimumRemaining = long.MaxValue;
        ThermalOperatingState grade = ThermalOperatingState.Continuous;
        foreach (string assetId in path.AssetIds)
        {
            ThermalLimit limit = interval.Limits[assetId];
            long prospective = checked(assetUse[assetId] + load.DemandKw);
            if (load.Permission == ThermalPermission.ContinuousOnly)
            {
                if (prospective > limit.ContinuousKw)
                {
                    return CandidateAssessment.Rejected(
                        source,
                        path,
                        new ThermalSupplyFailure(
                            ThermalFailureKind.ContinuousLimit,
                            source.SourceId,
                            assetId,
                            prospective,
                            limit.ContinuousKw));
                }
                minimumRemaining = Math.Min(
                    minimumRemaining,
                    checked(limit.ContinuousKw - prospective));
                continue;
            }

            if (prospective > limit.EmergencyKw)
            {
                return CandidateAssessment.Rejected(
                    source,
                    path,
                    new ThermalSupplyFailure(
                        ThermalFailureKind.EmergencyLimit,
                        source.SourceId,
                        assetId,
                        prospective,
                        limit.EmergencyKw));
            }
            if (prospective > limit.ContinuousKw)
            {
                grade = ThermalOperatingState.Emergency;
                emergencyCount++;
                minimumRemaining = Math.Min(
                    minimumRemaining,
                    checked(limit.EmergencyKw - prospective));
            }
            else
            {
                minimumRemaining = Math.Min(
                    minimumRemaining,
                    checked(limit.ContinuousKw - prospective));
            }
        }

        if (minimumRemaining == long.MaxValue)
        {
            throw new InvalidOperationException(
                "A source-to-load path must contain at least one thermal asset.");
        }
        return CandidateAssessment.AcceptedCandidate(
            source,
            path,
            grade,
            emergencyCount,
            minimumRemaining);
    }

    private static int CompareAccepted(CandidateAssessment first, CandidateAssessment second)
    {
        int comparison = first.Grade.CompareTo(second.Grade);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = first.EmergencyAssetCount.CompareTo(second.EmergencyAssetCount);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = second.MinimumRemainingKw.CompareTo(first.MinimumRemainingKw);
        if (comparison != 0)
        {
            return comparison;
        }
        return CompareStatic(first.Source, first.Path, second.Source, second.Path);
    }

    private static int CompareFailed(FailedCandidate first, FailedCandidate second)
    {
        int comparison = FailureRank(first.Failure.Kind).CompareTo(
            FailureRank(second.Failure.Kind));
        return comparison != 0
            ? comparison
            : CompareStatic(first.Source, first.Path, second.Source, second.Path);
    }

    private static int FailureRank(ThermalFailureKind kind) => kind switch
    {
        ThermalFailureKind.ContinuousLimit => 0,
        ThermalFailureKind.EmergencyLimit => 0,
        ThermalFailureKind.AssetUnavailable => 1,
        ThermalFailureKind.SourceCapacity => 2,
        ThermalFailureKind.NoTopologyPath => 3,
        _ => throw new InvalidOperationException("Unknown thermal failure kind."),
    };

    private static int CompareStatic(
        CommercialSourceDefinition firstSource,
        PathCandidate firstPath,
        CommercialSourceDefinition secondSource,
        PathCandidate secondPath)
    {
        int comparison = firstSource.DispatchOrder.CompareTo(secondSource.DispatchOrder);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = firstPath.LengthUnit.CompareTo(secondPath.LengthUnit);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = firstPath.EdgeIds.Count.CompareTo(secondPath.EdgeIds.Count);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = CompareOrdinalSequence(firstPath.EdgeIds, secondPath.EdgeIds);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = string.CompareOrdinal(firstPath.NodeIds[^1], secondPath.NodeIds[^1]);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = string.CompareOrdinal(firstSource.SourceId, secondSource.SourceId);
        return comparison != 0
            ? comparison
            : CompareOrdinalSequence(firstPath.NodeIds, secondPath.NodeIds);
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

    private static PathCandidate BuildPath(
        EvaluationContext context,
        IReadOnlyList<string> nodeIds,
        IReadOnlyList<string> edgeIds,
        long lengthUnit)
    {
        var assets = new List<string>();
        var steps = new List<PathStep>();
        for (int index = 0; index < nodeIds.Count; index++)
        {
            string nodeId = nodeIds[index];
            if (context.Assets.TryGetValue(nodeId, out ThermalAssetInfo? nodeAsset))
            {
                assets.Add(nodeId);
                steps.Add(new PathStep(nodeId, nodeAsset.AssetKind));
            }
            else
            {
                steps.Add(new PathStep(nodeId, null));
            }
            if (index < edgeIds.Count)
            {
                string edgeId = edgeIds[index];
                assets.Add(edgeId);
                steps.Add(new PathStep(edgeId, ThermalAssetKind.Edge));
            }
        }
        return new PathCandidate(
            Array.AsReadOnly(nodeIds.ToArray()),
            Array.AsReadOnly(edgeIds.ToArray()),
            Array.AsReadOnly(assets.ToArray()),
            Array.AsReadOnly(steps.ToArray()),
            lengthUnit);
    }

    private static void ValidateState(EvaluationContext context, ThermalState state)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string assetId in state.CoolingAssetIds)
        {
            RequireText(assetId, nameof(state));
            if (!seen.Add(assetId))
            {
                throw new ArgumentException(
                    $"Thermal state contains duplicate cooling asset ID '{assetId}'.",
                    nameof(state));
            }
            if (!context.Assets.ContainsKey(assetId))
            {
                throw new ArgumentException(
                    $"Thermal state references unknown or nonthermal asset '{assetId}'.",
                    nameof(state));
            }
        }
    }

    private static ValidatedInterval ValidateInterval(
        EvaluationContext context,
        ThermalIntervalRequest request)
    {
        RequireText(request.IntervalId, nameof(request));
        var loadIds = new HashSet<string>(StringComparer.Ordinal);
        long totalDemand = 0;
        foreach (ThermalLoadRequest load in request.Loads)
        {
            if (load is null)
            {
                throw new ArgumentException("Thermal interval contains a null load.", nameof(request));
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
                totalDemand = checked(totalDemand + load.DemandKw);
            }
            catch (OverflowException exception)
            {
                throw new ArgumentException(
                    "Thermal interval aggregate demand exceeds Int64.",
                    nameof(request),
                    exception);
            }
        }

        HashSet<string> unavailableNodes = ValidateUnavailableIds(
            request.UnavailableNodeIds,
            context.Nodes.Keys,
            "node",
            nameof(request));
        HashSet<string> unavailableEdges = ValidateUnavailableIds(
            request.UnavailableEdgeIds,
            context.Edges.Keys,
            "edge",
            nameof(request));

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
                    $"Override references unknown or nonthermal {item.AssetKind} class '{item.ClassId}'.",
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
                    $"Thermal interval duplicates override for '{item.AssetKind}:{item.ClassId}'.",
                    nameof(request));
            }
        }

        var limits = new Dictionary<string, ThermalLimit>(StringComparer.Ordinal);
        foreach (ThermalAssetInfo asset in context.Assets.Values)
        {
            limits[asset.AssetId] = overrides.TryGetValue(
                (asset.AssetKind, asset.ClassId),
                out ThermalLimit? current)
                ? current
                : asset.BaseLimit;
        }
        return new ValidatedInterval(
            request,
            unavailableNodes,
            unavailableEdges,
            limits);
    }

    private static HashSet<string> ValidateUnavailableIds(
        IReadOnlyList<string> values,
        IEnumerable<string> knownValues,
        string kind,
        string parameterName)
    {
        var known = knownValues.ToHashSet(StringComparer.Ordinal);
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireText(value, parameterName);
            if (!known.Contains(value))
            {
                throw new ArgumentException(
                    $"Thermal interval references unknown unavailable {kind} '{value}'.",
                    parameterName);
            }
            if (!result.Add(value))
            {
                throw new ArgumentException(
                    $"Thermal interval duplicates unavailable {kind} '{value}'.",
                    parameterName);
            }
        }
        return result;
    }

    private static void RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw new ArgumentException("Thermal identifiers must be nonblank and trimmed.", parameterName);
        }
    }

    private sealed class EvaluationContext
    {
        public EvaluationContext(CommercialWorldDefinition world)
        {
            Nodes = world.Nodes.ToDictionary(item => item.NodeId, StringComparer.Ordinal);
            Edges = world.Edges.ToDictionary(item => item.EdgeId, StringComparer.Ordinal);
            NodeClasses = world.NodeClasses.ToDictionary(item => item.ClassId, StringComparer.Ordinal);
            LineClasses = world.LineClasses.ToDictionary(item => item.ClassId, StringComparer.Ordinal);
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
                MapPoint from = Nodes[edge.FromNodeId].Position;
                MapPoint to = Nodes[edge.ToNodeId].Position;
                long length = FixedGeometry.CeilDistance(from, to);
                Adjacency[edge.FromNodeId].Add(new GraphArc(
                    edge.EdgeId,
                    edge.ToNodeId,
                    length));
                Adjacency[edge.ToNodeId].Add(new GraphArc(
                    edge.EdgeId,
                    edge.FromNodeId,
                    length));
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

    private sealed record PathStep(string AssetId, ThermalAssetKind? AssetKind);

    private sealed record PathCandidate(
        IReadOnlyList<string> NodeIds,
        IReadOnlyList<string> EdgeIds,
        IReadOnlyList<string> AssetIds,
        IReadOnlyList<PathStep> Steps,
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

    private sealed record CandidateAssessment(
        bool Accepted,
        CommercialSourceDefinition Source,
        PathCandidate Path,
        ThermalOperatingState Grade,
        int EmergencyAssetCount,
        long MinimumRemainingKw,
        ThermalSupplyFailure? Failure)
    {
        public static CandidateAssessment AcceptedCandidate(
            CommercialSourceDefinition source,
            PathCandidate path,
            ThermalOperatingState grade,
            int emergencyAssetCount,
            long minimumRemainingKw) =>
            new(
                true,
                source,
                path,
                grade,
                emergencyAssetCount,
                minimumRemainingKw,
                null);

        public static CandidateAssessment Rejected(
            CommercialSourceDefinition source,
            PathCandidate path,
            ThermalSupplyFailure failure) =>
            new(
                false,
                source,
                path,
                ThermalOperatingState.OverLimit,
                int.MaxValue,
                long.MinValue,
                failure);
    }

    private sealed record FailedCandidate(
        PathCandidate Path,
        CommercialSourceDefinition Source,
        ThermalSupplyFailure Failure);

    private sealed record ValidatedInterval(
        ThermalIntervalRequest Request,
        IReadOnlySet<string> UnavailableNodeIds,
        IReadOnlySet<string> UnavailableEdgeIds,
        IReadOnlyDictionary<string, ThermalLimit> Limits);
}
