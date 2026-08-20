using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

public enum RealtimeThermalTransitionKind
{
    EmergencyEntered,
    EmergencyCleared,
    ProtectiveTrip,
    Recovered,
}

public sealed record RealtimeThermalTransition(
    long Minute,
    string AssetId,
    ThermalAssetKind AssetKind,
    RealtimeThermalTransitionKind Kind);

public sealed record RealtimeThermalAssetSnapshot(
    string AssetId,
    ThermalAssetKind AssetKind,
    string ClassId,
    long UsedKw,
    long ContinuousKw,
    long EmergencyKw,
    long EmergencyExposureMinutes,
    int EmergencyExposureLimitMinutes,
    bool AuthoredUnavailable,
    bool ProtectiveOutage,
    long? ProtectiveOutageUntilMinute,
    ThermalOperatingState State);

public sealed record RealtimeThermalSnapshot(
    long Minute,
    ThermalIntervalEvaluation Evaluation,
    IReadOnlyList<RealtimeThermalAssetSnapshot> Assets)
{
    private IReadOnlyList<RealtimeThermalAssetSnapshot> _assets =
        Array.AsReadOnly(Assets.ToArray());

    public IReadOnlyList<RealtimeThermalAssetSnapshot> Assets
    {
        get => _assets;
        init => _assets = Array.AsReadOnly(value.ToArray());
    }
}

/// <summary>
/// Deterministic temporal session around the realtime supply allocator.
/// It models protection exposure, trips, and recovery without temperature claims;
/// V2 contracts remain the shared network and thermal value vocabulary.
/// </summary>
public sealed class RealtimeThermalSession
{
    private readonly RealtimeWorldDefinition _definition;
    private Dictionary<string, RuntimeAsset> _runtime =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _pendingTripAssetIds = new(StringComparer.Ordinal);
    private CommercialWorldDefinition _world;
    private ThermalIntervalRequest _request;
    private ThermalIntervalEvaluation _evaluation;
    private long _minute;

    public RealtimeThermalSession(
        RealtimeWorldDefinition definition,
        CommercialWorldDefinition world,
        long startMinute,
        IReadOnlyList<string>? initialProtectiveOutageAssetIds = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(world);
        if (startMinute < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startMinute));
        }
        RealtimeWorldLoader.Validate(definition, definition.Network);
        _definition = definition;
        ValidateRuntimeWorld(world);
        _world = world with { };
        _minute = startMinute;
        _request = IdleRequest("REALTIME_IDLE");
        EnsureRuntimeAssets(_world, _runtime);
        foreach (string assetId in initialProtectiveOutageAssetIds ?? Array.Empty<string>())
        {
            RuntimeAsset asset = _runtime.TryGetValue(assetId, out RuntimeAsset? value)
                ? value
                : throw new ArgumentException(
                    $"Initial protective outage references unknown asset '{assetId}'.",
                    nameof(initialProtectiveOutageAssetIds));
            asset.OutageUntilMinute = checked(
                startMinute + Protection(asset).ProtectiveOutageMinutes);
        }
        _evaluation = Evaluate();
    }

    public long Minute => _minute;

    public RealtimeThermalSession Fork()
    {
        var result = new RealtimeThermalSession(_definition, _world, _minute)
        {
            _request = _request,
        };
        foreach ((string assetId, RuntimeAsset source) in _runtime)
        {
            if (!result._runtime.TryGetValue(assetId, out RuntimeAsset? target))
            {
                continue;
            }
            target.ExposureMinutes = source.ExposureMinutes;
            target.OutageUntilMinute = source.OutageUntilMinute;
        }
        result._pendingTripAssetIds.UnionWith(_pendingTripAssetIds);
        result._evaluation = result.Evaluate();
        return result;
    }

    public RealtimeThermalSnapshot GetSnapshot()
    {
        Dictionary<string, ThermalAssetUsage> usage = _evaluation.Assets.ToDictionary(
            item => item.AssetId,
            StringComparer.Ordinal);
        RealtimeThermalAssetSnapshot[] assets = _runtime.Values
            .Where(item => usage.ContainsKey(item.AssetId))
            .OrderBy(item => item.AssetId, StringComparer.Ordinal)
            .Select(item =>
            {
                ThermalAssetUsage current = usage[item.AssetId];
                ThermalProtectionDefinition protection = Protection(item);
                bool authoredUnavailable = item.AssetKind switch
                {
                    ThermalAssetKind.Node => _request.UnavailableNodeIds.Contains(
                        item.AssetId,
                        StringComparer.Ordinal),
                    ThermalAssetKind.Edge => _request.UnavailableEdgeIds.Contains(
                        item.AssetId,
                        StringComparer.Ordinal),
                    _ => false,
                };
                ThermalOperatingState state = item.OutageUntilMinute.HasValue
                    ? ThermalOperatingState.ProtectiveOutage
                    : current.UsedKw > current.ContinuousKw
                        ? ThermalOperatingState.Emergency
                        : ThermalOperatingState.Continuous;
                return new RealtimeThermalAssetSnapshot(
                    item.AssetId,
                    item.AssetKind,
                    item.ClassId,
                    current.UsedKw,
                    current.ContinuousKw,
                    current.EmergencyKw,
                    item.ExposureMinutes,
                    protection.EmergencyExposureLimitMinutes,
                    authoredUnavailable,
                    item.OutageUntilMinute.HasValue,
                    item.OutageUntilMinute,
                    state);
            }).ToArray();
        return new RealtimeThermalSnapshot(_minute, _evaluation, assets);
    }

    public IReadOnlyList<RealtimeThermalTransition> SetOperatingProfile(
        CommercialWorldDefinition world,
        ThermalIntervalRequest request)
        => ApplyOperatingProfile(world, request, settleAvailability: false);

    /// <summary>
    /// Applies target-minute recovery and pending protective trips before evaluating the
    /// supplied world/profile. This is the atomic pre-supply boundary used when construction
    /// commissioning and authored event effects share a minute with a pending trip.
    /// </summary>
    public IReadOnlyList<RealtimeThermalTransition> SettleCurrentMinute(
        CommercialWorldDefinition world,
        ThermalIntervalRequest request)
        => ApplyOperatingProfile(world, request, settleAvailability: true);

    private IReadOnlyList<RealtimeThermalTransition> ApplyOperatingProfile(
        CommercialWorldDefinition world,
        ThermalIntervalRequest request,
        bool settleAvailability)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(request);
        ValidateRuntimeWorld(world);
        ValidateExistingAssetIdentity(world);
        Dictionary<string, RuntimeAsset> candidateRuntime = CloneRuntime(_runtime);
        EnsureRuntimeAssets(world, candidateRuntime);
        List<AvailabilityTransition> availability = settleAvailability
            ? ApplyAvailabilityTransitions(candidateRuntime)
            : [];
        ThermalIntervalEvaluation candidateEvaluation = Evaluate(
            world,
            request,
            candidateRuntime);
        ThermalIntervalEvaluation before = _evaluation;
        _world = world with { };
        _request = request;
        _runtime = candidateRuntime;
        _evaluation = candidateEvaluation;
        if (settleAvailability)
        {
            _pendingTripAssetIds.Clear();
        }

        var result = new List<RealtimeThermalTransition>(availability.Count + 4);
        result.AddRange(availability.Select(item => Transition(
            _runtime[item.AssetId],
            item.Kind)));
        IReadOnlySet<string> suppressedClears = settleAvailability
            ? availability.Where(item =>
                    item.Kind == RealtimeThermalTransitionKind.ProtectiveTrip)
                .Select(item => item.AssetId)
                .ToHashSet(StringComparer.Ordinal)
            : _pendingTripAssetIds;
        result.AddRange(EmergencyStateTransitions(
            before,
            _evaluation,
            suppressedClears));
        return Array.AsReadOnly(result.ToArray());
    }

    public IReadOnlyList<RealtimeThermalTransition> SetIdle(
        CommercialWorldDefinition world,
        string intervalId) => SetOperatingProfile(world, IdleRequest(intervalId));

    public long? NextTransitionMinute()
    {
        if (_pendingTripAssetIds.Count > 0)
        {
            return _minute;
        }
        long? next = null;
        Dictionary<string, ThermalAssetUsage> usage = _evaluation.Assets.ToDictionary(
            item => item.AssetId,
            StringComparer.Ordinal);
        foreach (RuntimeAsset asset in _runtime.Values)
        {
            if (asset.OutageUntilMinute is long recovery && recovery > _minute)
            {
                next = !next.HasValue || recovery < next.Value ? recovery : next;
                continue;
            }
            if (!usage.TryGetValue(asset.AssetId, out ThermalAssetUsage? current) ||
                current.UsedKw <= current.ContinuousKw)
            {
                continue;
            }
            long remaining = checked(
                Protection(asset).EmergencyExposureLimitMinutes - asset.ExposureMinutes);
            long trip = checked(_minute + Math.Max(0, remaining));
            next = !next.HasValue || trip < next.Value ? trip : next;
        }
        return next;
    }

    public IReadOnlyList<RealtimeThermalTransition> AdvanceTo(long targetMinute)
    {
        if (targetMinute < _minute)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetMinute),
                "Thermal time cannot move backward.");
        }
        var transitions = new List<RealtimeThermalTransition>();
        transitions.AddRange(SettleCurrentMinute());
        while (_minute < targetMinute)
        {
            long next = targetMinute;
            long? automatic = NextTransitionMinute();
            if (automatic.HasValue && automatic.Value > _minute && automatic.Value < next)
            {
                next = automatic.Value;
            }
            AdvanceClockTo(next);
            transitions.AddRange(SettleCurrentMinute());
        }
        return Array.AsReadOnly(transitions.ToArray());
    }

    /// <summary>
    /// Integrates the currently dispatched profile through [Minute, targetMinute) without
    /// applying target-minute recovery/trip transitions. A campaign orchestrator can then
    /// commission construction and change the event profile before calling SettleCurrentMinute.
    /// </summary>
    public void AdvanceClockTo(long targetMinute)
    {
        if (targetMinute < _minute)
        {
            throw new ArgumentOutOfRangeException(nameof(targetMinute));
        }
        long? automatic = NextTransitionMinute();
        if (automatic.HasValue && automatic.Value <= _minute && targetMinute > _minute)
        {
            throw new InvalidOperationException(
                "Thermal clock has an unsettled current-minute transition.");
        }
        if (automatic.HasValue && automatic.Value > _minute && automatic.Value < targetMinute)
        {
            throw new InvalidOperationException(
                "Thermal clock cannot cross an unsettled automatic change point.");
        }
        long delta = checked(targetMinute - _minute);
        if (delta == 0)
        {
            return;
        }
        Dictionary<string, ThermalAssetUsage> usage = _evaluation.Assets.ToDictionary(
            item => item.AssetId,
            StringComparer.Ordinal);
        foreach (RuntimeAsset asset in _runtime.Values)
        {
            if (asset.OutageUntilMinute.HasValue ||
                !usage.TryGetValue(asset.AssetId, out ThermalAssetUsage? current))
            {
                continue;
            }
            if (current.UsedKw > current.ContinuousKw)
            {
                int limit = Protection(asset).EmergencyExposureLimitMinutes;
                long before = asset.ExposureMinutes;
                long remaining = checked(limit - before);
                asset.ExposureMinutes = delta >= remaining
                    ? limit
                    : checked(before + delta);
                if (before < limit && asset.ExposureMinutes >= limit)
                {
                    _pendingTripAssetIds.Add(asset.AssetId);
                }
            }
            else
            {
                int rate = Protection(asset).EmergencyExposureRecoveryPerMinute;
                long recovered = delta > long.MaxValue / rate
                    ? long.MaxValue
                    : checked(delta * rate);
                asset.ExposureMinutes = Math.Max(
                    0,
                    asset.ExposureMinutes - Math.Min(asset.ExposureMinutes, recovered));
            }
        }
        _minute = targetMinute;
    }

    public IReadOnlyList<RealtimeThermalTransition> SettleCurrentMinute() =>
        SettleCurrentMinute(_world, _request);

    private List<AvailabilityTransition> ApplyAvailabilityTransitions(
        IReadOnlyDictionary<string, RuntimeAsset> runtime)
    {
        var result = new List<AvailabilityTransition>();
        foreach (RuntimeAsset asset in runtime.Values
                     .Where(item => item.OutageUntilMinute is long recovery && recovery <= _minute)
                     .OrderBy(item => item.AssetId, StringComparer.Ordinal))
        {
            asset.OutageUntilMinute = null;
            result.Add(new AvailabilityTransition(
                asset.AssetId,
                RealtimeThermalTransitionKind.Recovered));
        }
        foreach (RuntimeAsset asset in _pendingTripAssetIds
                     .Select(id => runtime[id])
                     .Where(item => !item.OutageUntilMinute.HasValue)
                     .OrderBy(item => item.AssetId, StringComparer.Ordinal))
        {
            asset.ExposureMinutes = 0;
            asset.OutageUntilMinute = checked(
                _minute + Protection(asset).ProtectiveOutageMinutes);
            result.Add(new AvailabilityTransition(
                asset.AssetId,
                RealtimeThermalTransitionKind.ProtectiveTrip));
        }
        return result;
    }

    private RealtimeThermalTransition Transition(
        RuntimeAsset asset,
        RealtimeThermalTransitionKind kind) => new(
        _minute,
        asset.AssetId,
        asset.AssetKind,
        kind);

    private IReadOnlyList<RealtimeThermalTransition> EmergencyStateTransitions(
        ThermalIntervalEvaluation before,
        ThermalIntervalEvaluation after,
        IReadOnlySet<string>? suppressedClears = null)
    {
        Dictionary<string, ThermalAssetUsage> previous = before.Assets.ToDictionary(
            item => item.AssetId,
            StringComparer.Ordinal);
        var result = new List<RealtimeThermalTransition>();
        foreach (ThermalAssetUsage current in after.Assets.OrderBy(
                     item => item.AssetId,
                     StringComparer.Ordinal))
        {
            bool wasEmergency = previous.TryGetValue(
                    current.AssetId,
                    out ThermalAssetUsage? old) &&
                old.UsedKw > old.ContinuousKw &&
                old.State != ThermalOperatingState.ProtectiveOutage;
            bool isEmergency = current.UsedKw > current.ContinuousKw &&
                current.State != ThermalOperatingState.ProtectiveOutage;
            if (wasEmergency == isEmergency || !_runtime.TryGetValue(
                    current.AssetId,
                    out RuntimeAsset? asset))
            {
                continue;
            }
            if (!isEmergency && suppressedClears?.Contains(current.AssetId) == true)
            {
                // A protective trip is the causal transition. Publishing an additional
                // EmergencyCleared for the same asset/minute falsely describes recovery.
                continue;
            }
            result.Add(Transition(
                asset,
                isEmergency
                    ? RealtimeThermalTransitionKind.EmergencyEntered
                    : RealtimeThermalTransitionKind.EmergencyCleared));
        }
        return result;
    }

    private ThermalIntervalEvaluation Evaluate()
        => Evaluate(_world, _request, _runtime);

    private static ThermalIntervalEvaluation Evaluate(
        CommercialWorldDefinition world,
        ThermalIntervalRequest request,
        IReadOnlyDictionary<string, RuntimeAsset> runtime)
    {
        string[] outageIds = runtime.Values
            .Where(item => item.OutageUntilMinute.HasValue)
            .Select(item => item.AssetId)
            .Where(id => world.Nodes.Any(node => node.NodeId == id) ||
                         world.Edges.Any(edge => edge.EdgeId == id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        return RealtimeSupplyAllocator.EvaluateInterval(
            world,
            request,
            outageIds);
    }

    private static void EnsureRuntimeAssets(
        CommercialWorldDefinition world,
        IDictionary<string, RuntimeAsset> runtime)
    {
        Dictionary<string, CommercialNodeClassDefinition> nodeClasses = world.NodeClasses
            .ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        foreach (SpatialNodeDefinition node in world.Nodes)
        {
            if (nodeClasses[node.ClassId].ThermalLimit is not null &&
                !runtime.ContainsKey(node.NodeId))
            {
                runtime.Add(node.NodeId, new RuntimeAsset(
                    node.NodeId,
                    ThermalAssetKind.Node,
                    node.ClassId));
            }
        }
        foreach (SpatialEdgeDefinition edge in world.Edges)
        {
            if (!runtime.ContainsKey(edge.EdgeId))
            {
                runtime.Add(edge.EdgeId, new RuntimeAsset(
                    edge.EdgeId,
                    ThermalAssetKind.Edge,
                    edge.LineClassId));
            }
        }
    }

    private static Dictionary<string, RuntimeAsset> CloneRuntime(
        IReadOnlyDictionary<string, RuntimeAsset> source)
    {
        var result = new Dictionary<string, RuntimeAsset>(StringComparer.Ordinal);
        foreach ((string assetId, RuntimeAsset asset) in source)
        {
            result.Add(assetId, new RuntimeAsset(
                asset.AssetId,
                asset.AssetKind,
                asset.ClassId)
            {
                ExposureMinutes = asset.ExposureMinutes,
                OutageUntilMinute = asset.OutageUntilMinute,
            });
        }
        return result;
    }

    private void ValidateRuntimeWorld(CommercialWorldDefinition world)
    {
        CommercialWorldLoader.Validate(world);
        Dictionary<string, CommercialNodeClassDefinition> authoritativeNodes =
            _definition.Network.NodeClasses.ToDictionary(
                item => item.ClassId,
                StringComparer.Ordinal);
        Dictionary<string, CommercialNodeClassDefinition> runtimeNodes =
            world.NodeClasses.ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        Dictionary<string, CommercialLineClassDefinition> authoritativeLines =
            _definition.Network.LineClasses.ToDictionary(
                item => item.ClassId,
                StringComparer.Ordinal);
        Dictionary<string, CommercialLineClassDefinition> runtimeLines =
            world.LineClasses.ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        if (runtimeNodes.Count != authoritativeNodes.Count ||
            runtimeNodes.Any(item =>
                !authoritativeNodes.TryGetValue(item.Key, out var expected) ||
                item.Value != expected) ||
            runtimeLines.Count != authoritativeLines.Count ||
            runtimeLines.Any(item =>
                !authoritativeLines.TryGetValue(item.Key, out var expected) ||
                item.Value != expected))
        {
            throw new ArgumentException(
                "Runtime world class definitions must exactly match the realtime world authority.",
                nameof(world));
        }
    }

    private void ValidateExistingAssetIdentity(CommercialWorldDefinition candidate)
    {
        Dictionary<string, SpatialNodeDefinition> existingNodes = _world.Nodes
            .ToDictionary(item => item.NodeId, StringComparer.Ordinal);
        Dictionary<string, SpatialEdgeDefinition> existingEdges = _world.Edges
            .ToDictionary(item => item.EdgeId, StringComparer.Ordinal);
        foreach (SpatialNodeDefinition node in candidate.Nodes)
        {
            bool conflictsWithKnownThermalAsset = _runtime.TryGetValue(
                    node.NodeId,
                    out RuntimeAsset? known) &&
                (known.AssetKind != ThermalAssetKind.Node ||
                 !string.Equals(known.ClassId, node.ClassId, StringComparison.Ordinal));
            if (conflictsWithKnownThermalAsset ||
                existingEdges.ContainsKey(node.NodeId) ||
                existingNodes.TryGetValue(node.NodeId, out SpatialNodeDefinition? existing) &&
                !string.Equals(existing.ClassId, node.ClassId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Existing asset '{node.NodeId}' cannot change kind or node class.",
                    nameof(candidate));
            }
        }
        foreach (SpatialEdgeDefinition edge in candidate.Edges)
        {
            bool conflictsWithKnownThermalAsset = _runtime.TryGetValue(
                    edge.EdgeId,
                    out RuntimeAsset? known) &&
                (known.AssetKind != ThermalAssetKind.Edge ||
                 !string.Equals(
                     known.ClassId,
                     edge.LineClassId,
                     StringComparison.Ordinal));
            if (conflictsWithKnownThermalAsset ||
                existingNodes.ContainsKey(edge.EdgeId) ||
                existingEdges.TryGetValue(edge.EdgeId, out SpatialEdgeDefinition? existing) &&
                !string.Equals(
                    existing.LineClassId,
                    edge.LineClassId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Existing asset '{edge.EdgeId}' cannot change kind or line class.",
                    nameof(candidate));
            }
        }
    }

    private ThermalProtectionDefinition Protection(RuntimeAsset asset) =>
        _definition.ProtectionFor(asset.AssetKind, asset.ClassId);

    private static ThermalIntervalRequest IdleRequest(string intervalId) => new(
        intervalId,
        Array.Empty<ThermalLoadRequest>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<ThermalLimitOverride>());

    private sealed class RuntimeAsset(
        string assetId,
        ThermalAssetKind assetKind,
        string classId)
    {
        public string AssetId { get; } = assetId;
        public ThermalAssetKind AssetKind { get; } = assetKind;
        public string ClassId { get; } = classId;
        public long ExposureMinutes { get; set; }
        public long? OutageUntilMinute { get; set; }
    }

    private sealed record AvailabilityTransition(
        string AssetId,
        RealtimeThermalTransitionKind Kind);
}
