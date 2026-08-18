namespace Gridworks.Core.Release.V2;

public sealed record ThermalLimit(long ContinuousKw, long EmergencyKw);

public sealed record CommercialNodeClassDefinition(
    string ClassId,
    string DisplayName,
    SpatialNodeKind Kind,
    int FootprintRadiusUnit,
    int MaxConnections,
    long CostCashUnit,
    int BuildMinutes,
    ThermalLimit? ThermalLimit,
    int? ServiceRadiusUnit = null);

public sealed record CommercialLineClassDefinition(
    string ClassId,
    string DisplayName,
    int MaxSpanUnit,
    long CostCashUnitPerDesignUnit,
    int BuildMinutesPerDesignUnit,
    ThermalLimit ThermalLimit);

public sealed record CommercialSourceDefinition(
    string SourceId,
    string DisplayName,
    string NodeId,
    long CapacityKw,
    int DispatchOrder);

public sealed record CommercialLoadDefinition(
    string LoadId,
    string DisplayName,
    string NodeId);

public sealed record CommercialWorldDefinition(
    string SchemaVersion,
    string WorldId,
    string DisplayName,
    int UnitsPerDesignUnit,
    MapBounds Bounds,
    long InitialCashUnit,
    IReadOnlyList<CommercialNodeClassDefinition> NodeClasses,
    IReadOnlyList<CommercialLineClassDefinition> LineClasses,
    IReadOnlyList<TerrainPolygonDefinition> Terrain,
    IReadOnlyList<SpatialRiskAreaDefinition> RiskAreas,
    IReadOnlyList<SpatialNodeDefinition> Nodes,
    IReadOnlyList<SpatialEdgeDefinition> Edges,
    IReadOnlyList<CommercialSourceDefinition> Sources,
    IReadOnlyList<CommercialLoadDefinition> Loads)
{
    private IReadOnlyList<CommercialNodeClassDefinition> _nodeClasses =
        Frozen(NodeClasses);
    private IReadOnlyList<CommercialLineClassDefinition> _lineClasses =
        Frozen(LineClasses);
    private IReadOnlyList<TerrainPolygonDefinition> _terrain = Frozen(Terrain);
    private IReadOnlyList<SpatialRiskAreaDefinition> _riskAreas = Frozen(RiskAreas);
    private IReadOnlyList<SpatialNodeDefinition> _nodes = Frozen(Nodes);
    private IReadOnlyList<SpatialEdgeDefinition> _edges = Frozen(Edges);
    private IReadOnlyList<CommercialSourceDefinition> _sources = Frozen(Sources);
    private IReadOnlyList<CommercialLoadDefinition> _loads = Frozen(Loads);

    public IReadOnlyList<CommercialNodeClassDefinition> NodeClasses
    {
        get => _nodeClasses;
        init => _nodeClasses = Frozen(value);
    }

    public IReadOnlyList<CommercialLineClassDefinition> LineClasses
    {
        get => _lineClasses;
        init => _lineClasses = Frozen(value);
    }

    public IReadOnlyList<TerrainPolygonDefinition> Terrain
    {
        get => _terrain;
        init => _terrain = Frozen(value);
    }

    public IReadOnlyList<SpatialRiskAreaDefinition> RiskAreas
    {
        get => _riskAreas;
        init => _riskAreas = Frozen(value);
    }

    public IReadOnlyList<SpatialNodeDefinition> Nodes
    {
        get => _nodes;
        init => _nodes = Frozen(value);
    }

    public IReadOnlyList<SpatialEdgeDefinition> Edges
    {
        get => _edges;
        init => _edges = Frozen(value);
    }

    public IReadOnlyList<CommercialSourceDefinition> Sources
    {
        get => _sources;
        init => _sources = Frozen(value);
    }

    public IReadOnlyList<CommercialLoadDefinition> Loads
    {
        get => _loads;
        init => _loads = Frozen(value);
    }

    public SpatialWorldDefinition ToSpatialWorld() => new(
        SpatialWorldLoader.SupportedSchemaVersion,
        WorldId,
        DisplayName,
        UnitsPerDesignUnit,
        Bounds,
        InitialCashUnit,
        NodeClasses.Select(item => new SpatialNodeClassDefinition(
            item.ClassId,
            item.DisplayName,
            item.Kind,
            item.FootprintRadiusUnit,
            item.MaxConnections,
            item.CostCashUnit,
            item.BuildMinutes)).ToArray(),
        LineClasses.Select(item => new SpatialLineClassDefinition(
            item.ClassId,
            item.DisplayName,
            item.MaxSpanUnit,
            item.CostCashUnitPerDesignUnit,
            item.BuildMinutesPerDesignUnit)).ToArray(),
        Terrain,
        RiskAreas,
        Nodes,
        Edges);

    private static IReadOnlyList<T> Frozen<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public enum ThermalPermission
{
    ContinuousOnly,
    EmergencyAllowed,
}

public enum ThermalAssetKind
{
    Node,
    Edge,
}

public enum ThermalOperatingState
{
    Continuous,
    Emergency,
    ProtectiveOutage,
    OverLimit,
}

public enum ThermalFailureKind
{
    NoTopologyPath,
    NoEligibleSubstation,
    SourceCapacity,
    AssetUnavailable,
    ContinuousLimit,
    EmergencyLimit,
}

public sealed record ThermalLoadRequest(
    string LoadId,
    long DemandKw,
    ThermalPermission Permission);

public sealed record ThermalLimitOverride(
    ThermalAssetKind AssetKind,
    string ClassId,
    long ContinuousKw,
    long EmergencyKw);

public sealed record ThermalState(IReadOnlyList<string> CoolingAssetIds)
{
    private IReadOnlyList<string> _coolingAssetIds = Frozen(CoolingAssetIds);

    public static ThermalState Empty { get; } = new(Array.Empty<string>());

    public IReadOnlyList<string> CoolingAssetIds
    {
        get => _coolingAssetIds;
        init => _coolingAssetIds = Frozen(value);
    }

    public bool Equals(ThermalState? other) => other is not null &&
        CoolingAssetIds.SequenceEqual(other.CoolingAssetIds, StringComparer.Ordinal);

    public override int GetHashCode() => SequenceHash(CoolingAssetIds);

    private static IReadOnlyList<string> Frozen(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }

    private static int SequenceHash(IEnumerable<string> values)
    {
        var hash = new HashCode();
        foreach (string value in values)
        {
            hash.Add(value, StringComparer.Ordinal);
        }
        return hash.ToHashCode();
    }
}

public sealed record ThermalIntervalRequest(
    string IntervalId,
    IReadOnlyList<ThermalLoadRequest> Loads,
    IReadOnlyList<string> UnavailableNodeIds,
    IReadOnlyList<string> UnavailableEdgeIds,
    IReadOnlyList<ThermalLimitOverride> LimitOverrides)
{
    private IReadOnlyList<ThermalLoadRequest> _loads = Frozen(Loads);
    private IReadOnlyList<string> _unavailableNodeIds = Frozen(UnavailableNodeIds);
    private IReadOnlyList<string> _unavailableEdgeIds = Frozen(UnavailableEdgeIds);
    private IReadOnlyList<ThermalLimitOverride> _limitOverrides = Frozen(LimitOverrides);

    public IReadOnlyList<ThermalLoadRequest> Loads
    {
        get => _loads;
        init => _loads = Frozen(value);
    }

    public IReadOnlyList<string> UnavailableNodeIds
    {
        get => _unavailableNodeIds;
        init => _unavailableNodeIds = Frozen(value);
    }

    public IReadOnlyList<string> UnavailableEdgeIds
    {
        get => _unavailableEdgeIds;
        init => _unavailableEdgeIds = Frozen(value);
    }

    public IReadOnlyList<ThermalLimitOverride> LimitOverrides
    {
        get => _limitOverrides;
        init => _limitOverrides = Frozen(value);
    }

    public bool Equals(ThermalIntervalRequest? other) => other is not null &&
        string.Equals(IntervalId, other.IntervalId, StringComparison.Ordinal) &&
        Loads.SequenceEqual(other.Loads) &&
        UnavailableNodeIds.SequenceEqual(other.UnavailableNodeIds, StringComparer.Ordinal) &&
        UnavailableEdgeIds.SequenceEqual(other.UnavailableEdgeIds, StringComparer.Ordinal) &&
        LimitOverrides.SequenceEqual(other.LimitOverrides);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(IntervalId, StringComparer.Ordinal);
        AddSequence(ref hash, Loads);
        AddSequence(ref hash, UnavailableNodeIds, StringComparer.Ordinal);
        AddSequence(ref hash, UnavailableEdgeIds, StringComparer.Ordinal);
        AddSequence(ref hash, LimitOverrides);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<T> Frozen<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }

    private static void AddSequence<T>(
        ref HashCode hash,
        IEnumerable<T> values,
        IEqualityComparer<T>? comparer = null)
    {
        foreach (T value in values)
        {
            hash.Add(value, comparer);
        }
    }
}

public sealed record ThermalSequenceRequest(IReadOnlyList<ThermalIntervalRequest> Intervals)
{
    private IReadOnlyList<ThermalIntervalRequest> _intervals = Frozen(Intervals);

    public IReadOnlyList<ThermalIntervalRequest> Intervals
    {
        get => _intervals;
        init => _intervals = Frozen(value);
    }

    public bool Equals(ThermalSequenceRequest? other) => other is not null &&
        Intervals.SequenceEqual(other.Intervals);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (ThermalIntervalRequest value in Intervals)
        {
            hash.Add(value);
        }
        return hash.ToHashCode();
    }

    private static IReadOnlyList<ThermalIntervalRequest> Frozen(
        IReadOnlyList<ThermalIntervalRequest> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record ThermalSupplyFailure(
    ThermalFailureKind Kind,
    string? AttemptedSourceId,
    string? AssetId,
    long RequiredKw,
    long AvailableKw);

public sealed record ThermalLoadSupply(
    string LoadId,
    long DemandKw,
    long DeliveredKw,
    string? SourceId,
    IReadOnlyList<string> PathNodeIds,
    IReadOnlyList<string> PathEdgeIds,
    long? MinimumRemainingKw,
    ThermalSupplyFailure? Failure)
{
    private IReadOnlyList<string> _pathNodeIds = Frozen(PathNodeIds);
    private IReadOnlyList<string> _pathEdgeIds = Frozen(PathEdgeIds);

    public IReadOnlyList<string> PathNodeIds
    {
        get => _pathNodeIds;
        init => _pathNodeIds = Frozen(value);
    }

    public IReadOnlyList<string> PathEdgeIds
    {
        get => _pathEdgeIds;
        init => _pathEdgeIds = Frozen(value);
    }

    public bool Equals(ThermalLoadSupply? other) => other is not null &&
        string.Equals(LoadId, other.LoadId, StringComparison.Ordinal) &&
        DemandKw == other.DemandKw &&
        DeliveredKw == other.DeliveredKw &&
        string.Equals(SourceId, other.SourceId, StringComparison.Ordinal) &&
        PathNodeIds.SequenceEqual(other.PathNodeIds, StringComparer.Ordinal) &&
        PathEdgeIds.SequenceEqual(other.PathEdgeIds, StringComparer.Ordinal) &&
        MinimumRemainingKw == other.MinimumRemainingKw &&
        Equals(Failure, other.Failure);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(LoadId, StringComparer.Ordinal);
        hash.Add(DemandKw);
        hash.Add(DeliveredKw);
        hash.Add(SourceId, StringComparer.Ordinal);
        foreach (string value in PathNodeIds)
        {
            hash.Add(value, StringComparer.Ordinal);
        }
        foreach (string value in PathEdgeIds)
        {
            hash.Add(value, StringComparer.Ordinal);
        }
        hash.Add(MinimumRemainingKw);
        hash.Add(Failure);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<string> Frozen(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record ThermalAssetUsage(
    string AssetId,
    ThermalAssetKind AssetKind,
    long UsedKw,
    long ContinuousKw,
    long EmergencyKw,
    ThermalOperatingState State,
    ThermalOperatingState NextState);

public sealed record ThermalSourceUsage(
    string SourceId,
    long UsedKw,
    long CapacityKw);

public sealed record ThermalIntervalEvaluation(
    string IntervalId,
    IReadOnlyList<ThermalLoadSupply> Loads,
    IReadOnlyList<ThermalAssetUsage> Assets,
    IReadOnlyList<ThermalSourceUsage> Sources,
    ThermalState NextThermalState)
{
    private IReadOnlyList<ThermalLoadSupply> _loads = Frozen(Loads);
    private IReadOnlyList<ThermalAssetUsage> _assets = Frozen(Assets);
    private IReadOnlyList<ThermalSourceUsage> _sources = Frozen(Sources);

    public IReadOnlyList<ThermalLoadSupply> Loads
    {
        get => _loads;
        init => _loads = Frozen(value);
    }

    public IReadOnlyList<ThermalAssetUsage> Assets
    {
        get => _assets;
        init => _assets = Frozen(value);
    }

    public IReadOnlyList<ThermalSourceUsage> Sources
    {
        get => _sources;
        init => _sources = Frozen(value);
    }

    public bool Equals(ThermalIntervalEvaluation? other) => other is not null &&
        string.Equals(IntervalId, other.IntervalId, StringComparison.Ordinal) &&
        Loads.SequenceEqual(other.Loads) &&
        Assets.SequenceEqual(other.Assets) &&
        Sources.SequenceEqual(other.Sources) &&
        Equals(NextThermalState, other.NextThermalState);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(IntervalId, StringComparer.Ordinal);
        AddSequence(ref hash, Loads);
        AddSequence(ref hash, Assets);
        AddSequence(ref hash, Sources);
        hash.Add(NextThermalState);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<T> Frozen<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }

    private static void AddSequence<T>(ref HashCode hash, IEnumerable<T> values)
    {
        foreach (T value in values)
        {
            hash.Add(value);
        }
    }
}

public sealed record ThermalSequenceEvaluation(
    IReadOnlyList<ThermalIntervalEvaluation> Intervals,
    ThermalState FinalThermalState)
{
    private IReadOnlyList<ThermalIntervalEvaluation> _intervals = Frozen(Intervals);

    public IReadOnlyList<ThermalIntervalEvaluation> Intervals
    {
        get => _intervals;
        init => _intervals = Frozen(value);
    }

    public bool Equals(ThermalSequenceEvaluation? other) => other is not null &&
        Intervals.SequenceEqual(other.Intervals) &&
        Equals(FinalThermalState, other.FinalThermalState);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (ThermalIntervalEvaluation value in Intervals)
        {
            hash.Add(value);
        }
        hash.Add(FinalThermalState);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<ThermalIntervalEvaluation> Frozen(
        IReadOnlyList<ThermalIntervalEvaluation> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed class CommercialWorldValidationException : Exception
{
    public CommercialWorldValidationException(string message)
        : base(message)
    {
    }

    public CommercialWorldValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
