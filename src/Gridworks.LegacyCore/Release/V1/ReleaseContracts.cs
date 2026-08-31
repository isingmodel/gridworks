namespace Gridworks.Core.Release;

public readonly record struct ReleasePoint(int X, int Y);

public enum ReleaseNodeKind
{
    SourceTerminal,
    Pole,
    Substation,
    DedicatedLoadTerminal,
}

public enum ReleaseLoadPriority
{
    LifeSafety = 0,
    EssentialService = 1,
    Residential = 2,
    Industrial = 3,
}

public enum ReleaseLoadConnectionKind
{
    ServiceArea,
    DedicatedNode,
}

public enum ReleaseSupplyFailureKind
{
    None,
    NoEligibleSubstation,
    Disconnected,
    SourceCapacity,
    EdgeCapacity,
    NodeCapacity,
    TransformerCapacity,
}

public sealed record ReleaseGridDefinition(
    int MinX,
    int MinY,
    int MaxX,
    int MaxY,
    int MajorStep);

public sealed record ReleaseNodeClassDefinition(
    string ClassId,
    string DisplayName,
    ReleaseNodeKind Kind,
    int MaxConnections,
    long? ThroughputKw,
    long? TransformerRatingKw,
    int? ServiceRadiusCells,
    long CostCashUnit,
    int BuildMinutes);

public sealed record ReleaseLineClassDefinition(
    string ClassId,
    string DisplayName,
    long RatingKw,
    int MaxSpanCells,
    long CostCashUnitPerMilliCell,
    int BuildMinutesPerMilliCell);

public sealed record ReleaseNodeDefinition(
    string NodeId,
    string ClassId,
    string DisplayName,
    ReleasePoint Position,
    bool Commissioned);

public sealed record ReleaseEdgeDefinition(
    string EdgeId,
    string LineClassId,
    string FromNodeId,
    string ToNodeId,
    bool Commissioned);

public sealed record ReleaseSourceDefinition(
    string SourceId,
    string NodeId,
    string DisplayName,
    int DispatchOrder,
    long CapacityKw);

public sealed record ReleaseLoadDefinition(
    string LoadId,
    string DisplayName,
    ReleaseLoadPriority Priority,
    long DemandKw,
    ReleaseLoadConnectionKind ConnectionKind,
    ReleasePoint Position,
    string? DedicatedNodeId);

public sealed record ReleaseRiskAreaDefinition(
    string RiskAreaId,
    string DisplayName,
    IReadOnlyList<ReleasePoint> Polygon)
{
    private IReadOnlyList<ReleasePoint> _polygon = Array.AsReadOnly(Polygon.ToArray());

    public IReadOnlyList<ReleasePoint> Polygon
    {
        get => _polygon;
        init => _polygon = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record ReleaseWorldDefinition(
    string SchemaVersion,
    string WorldId,
    string DisplayName,
    ReleaseGridDefinition Grid,
    IReadOnlyList<ReleaseNodeClassDefinition> NodeClasses,
    IReadOnlyList<ReleaseLineClassDefinition> LineClasses,
    IReadOnlyList<ReleaseNodeDefinition> Nodes,
    IReadOnlyList<ReleaseEdgeDefinition> Edges,
    IReadOnlyList<ReleaseSourceDefinition> Sources,
    IReadOnlyList<ReleaseLoadDefinition> Loads,
    IReadOnlyList<ReleaseRiskAreaDefinition> RiskAreas)
{
    private IReadOnlyList<ReleaseNodeClassDefinition> _nodeClasses = Array.AsReadOnly(NodeClasses.ToArray());
    private IReadOnlyList<ReleaseLineClassDefinition> _lineClasses = Array.AsReadOnly(LineClasses.ToArray());
    private IReadOnlyList<ReleaseNodeDefinition> _nodes = Array.AsReadOnly(Nodes.ToArray());
    private IReadOnlyList<ReleaseEdgeDefinition> _edges = Array.AsReadOnly(Edges.ToArray());
    private IReadOnlyList<ReleaseSourceDefinition> _sources = Array.AsReadOnly(Sources.ToArray());
    private IReadOnlyList<ReleaseLoadDefinition> _loads = Array.AsReadOnly(Loads.ToArray());
    private IReadOnlyList<ReleaseRiskAreaDefinition> _riskAreas = Array.AsReadOnly(RiskAreas.ToArray());
    private IReadOnlyList<ReleasePoint> _waterPolygon = Array.AsReadOnly(Array.Empty<ReleasePoint>());

    public IReadOnlyList<ReleaseNodeClassDefinition> NodeClasses
    {
        get => _nodeClasses;
        init => _nodeClasses = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ReleaseLineClassDefinition> LineClasses
    {
        get => _lineClasses;
        init => _lineClasses = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ReleaseNodeDefinition> Nodes
    {
        get => _nodes;
        init => _nodes = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ReleaseEdgeDefinition> Edges
    {
        get => _edges;
        init => _edges = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ReleaseSourceDefinition> Sources
    {
        get => _sources;
        init => _sources = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ReleaseLoadDefinition> Loads
    {
        get => _loads;
        init => _loads = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ReleaseRiskAreaDefinition> RiskAreas
    {
        get => _riskAreas;
        init => _riskAreas = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ReleasePoint> WaterPolygon
    {
        get => _waterPolygon;
        init => _waterPolygon = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record ReleaseContingency(
    IReadOnlySet<string> UnavailableNodeIds,
    IReadOnlySet<string> UnavailableEdgeIds,
    IReadOnlySet<string> ActiveRiskAreaIds)
{
    public static ReleaseContingency None { get; } = new(
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));

    private IReadOnlySet<string> _unavailableNodeIds =
        new HashSet<string>(UnavailableNodeIds, StringComparer.Ordinal);
    private IReadOnlySet<string> _unavailableEdgeIds =
        new HashSet<string>(UnavailableEdgeIds, StringComparer.Ordinal);
    private IReadOnlySet<string> _activeRiskAreaIds =
        new HashSet<string>(ActiveRiskAreaIds, StringComparer.Ordinal);

    public IReadOnlySet<string> UnavailableNodeIds
    {
        get => _unavailableNodeIds;
        init => _unavailableNodeIds = new HashSet<string>(value, StringComparer.Ordinal);
    }

    public IReadOnlySet<string> UnavailableEdgeIds
    {
        get => _unavailableEdgeIds;
        init => _unavailableEdgeIds = new HashSet<string>(value, StringComparer.Ordinal);
    }

    public IReadOnlySet<string> ActiveRiskAreaIds
    {
        get => _activeRiskAreaIds;
        init => _activeRiskAreaIds = new HashSet<string>(value, StringComparer.Ordinal);
    }
}

public sealed record ReleaseSupplyFailure(
    ReleaseSupplyFailureKind Kind,
    string? AssetId,
    long ShortfallKw,
    string? AttemptedSourceId = null);

public sealed record ReleaseLoadSupply(
    string LoadId,
    long DemandKw,
    long DeliveredKw,
    string? SourceId,
    string? EndpointNodeId,
    IReadOnlyList<string> PathNodeIds,
    IReadOnlyList<string> PathEdgeIds,
    ReleaseSupplyFailure Failure)
{
    private IReadOnlyList<string> _pathNodeIds = Array.AsReadOnly(PathNodeIds.ToArray());
    private IReadOnlyList<string> _pathEdgeIds = Array.AsReadOnly(PathEdgeIds.ToArray());

    public IReadOnlyList<string> PathNodeIds
    {
        get => _pathNodeIds;
        init => _pathNodeIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> PathEdgeIds
    {
        get => _pathEdgeIds;
        init => _pathEdgeIds = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record ReleaseNodeUsage(
    string NodeId,
    long UsedKw,
    long RatingKw,
    int ConnectionCount,
    int MaxConnections,
    bool Available);

public sealed record ReleaseEdgeUsage(
    string EdgeId,
    long UsedKw,
    long RatingKw,
    bool Available);

public sealed record ReleaseSourceUsage(
    string SourceId,
    long UsedKw,
    long CapacityKw,
    bool Available);

public sealed record ReleaseNetworkEvaluation(
    IReadOnlyList<ReleaseLoadSupply> Loads,
    IReadOnlyList<ReleaseNodeUsage> Nodes,
    IReadOnlyList<ReleaseEdgeUsage> Edges,
    IReadOnlyList<ReleaseSourceUsage> Sources,
    long TotalDeliveredKw,
    long TotalGenerationKw)
{
    private IReadOnlyList<ReleaseLoadSupply> _loads = Array.AsReadOnly(Loads.ToArray());
    private IReadOnlyList<ReleaseNodeUsage> _nodes = Array.AsReadOnly(Nodes.ToArray());
    private IReadOnlyList<ReleaseEdgeUsage> _edges = Array.AsReadOnly(Edges.ToArray());
    private IReadOnlyList<ReleaseSourceUsage> _sources = Array.AsReadOnly(Sources.ToArray());

    public IReadOnlyList<ReleaseLoadSupply> Loads
    {
        get => _loads;
        init => _loads = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ReleaseNodeUsage> Nodes
    {
        get => _nodes;
        init => _nodes = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ReleaseEdgeUsage> Edges
    {
        get => _edges;
        init => _edges = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ReleaseSourceUsage> Sources
    {
        get => _sources;
        init => _sources = Array.AsReadOnly(value.ToArray());
    }
}

public sealed class ReleaseWorldValidationException : Exception
{
    public ReleaseWorldValidationException(string message)
        : base(message)
    {
    }
}
