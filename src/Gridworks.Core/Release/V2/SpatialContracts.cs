namespace Gridworks.Core.Release.V2;

public readonly record struct MapPoint(int XUnit, int YUnit);

public sealed record MapBounds(
    int MinXUnit,
    int MinYUnit,
    int MaxXUnit,
    int MaxYUnit);

public enum SpatialNodeKind
{
    SourceTerminal,
    Pole,
    Substation,
    DedicatedLoadTerminal,
}

public enum TerrainKind
{
    Water,
    Building,
}

public sealed record SpatialNodeClassDefinition(
    string ClassId,
    string DisplayName,
    SpatialNodeKind Kind,
    int FootprintRadiusUnit,
    int MaxConnections,
    long CostCashUnit,
    int BuildMinutes);

public sealed record SpatialLineClassDefinition(
    string ClassId,
    string DisplayName,
    int MaxSpanUnit,
    long CostCashUnitPerDesignUnit,
    int BuildMinutesPerDesignUnit);

public sealed record TerrainPolygonDefinition(
    string TerrainId,
    string DisplayName,
    TerrainKind Kind,
    IReadOnlyList<MapPoint> Polygon)
{
    private IReadOnlyList<MapPoint> _polygon = Array.AsReadOnly(Polygon.ToArray());

    public IReadOnlyList<MapPoint> Polygon
    {
        get => _polygon;
        init => _polygon = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record SpatialRiskAreaDefinition(
    string RiskAreaId,
    string DisplayName,
    IReadOnlyList<MapPoint> Polygon)
{
    private IReadOnlyList<MapPoint> _polygon = Array.AsReadOnly(Polygon.ToArray());

    public IReadOnlyList<MapPoint> Polygon
    {
        get => _polygon;
        init => _polygon = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record SpatialNodeDefinition(
    string NodeId,
    string ClassId,
    string DisplayName,
    MapPoint Position,
    bool Commissioned,
    bool AuthoredFoundation);

public sealed record SpatialEdgeDefinition(
    string EdgeId,
    string LineClassId,
    string FromNodeId,
    string ToNodeId,
    bool Commissioned);

public sealed record SpatialWorldDefinition(
    string SchemaVersion,
    string WorldId,
    string DisplayName,
    int UnitsPerDesignUnit,
    MapBounds Bounds,
    long InitialCashUnit,
    IReadOnlyList<SpatialNodeClassDefinition> NodeClasses,
    IReadOnlyList<SpatialLineClassDefinition> LineClasses,
    IReadOnlyList<TerrainPolygonDefinition> Terrain,
    IReadOnlyList<SpatialRiskAreaDefinition> RiskAreas,
    IReadOnlyList<SpatialNodeDefinition> Nodes,
    IReadOnlyList<SpatialEdgeDefinition> Edges)
{
    public const int RequiredUnitsPerDesignUnit = 100;

    private IReadOnlyList<SpatialNodeClassDefinition> _nodeClasses =
        Array.AsReadOnly(NodeClasses.ToArray());
    private IReadOnlyList<SpatialLineClassDefinition> _lineClasses =
        Array.AsReadOnly(LineClasses.ToArray());
    private IReadOnlyList<TerrainPolygonDefinition> _terrain =
        Array.AsReadOnly(Terrain.ToArray());
    private IReadOnlyList<SpatialRiskAreaDefinition> _riskAreas =
        Array.AsReadOnly(RiskAreas.ToArray());
    private IReadOnlyList<SpatialNodeDefinition> _nodes =
        Array.AsReadOnly(Nodes.ToArray());
    private IReadOnlyList<SpatialEdgeDefinition> _edges =
        Array.AsReadOnly(Edges.ToArray());

    public IReadOnlyList<SpatialNodeClassDefinition> NodeClasses
    {
        get => _nodeClasses;
        init => _nodeClasses = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<SpatialLineClassDefinition> LineClasses
    {
        get => _lineClasses;
        init => _lineClasses = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<TerrainPolygonDefinition> Terrain
    {
        get => _terrain;
        init => _terrain = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<SpatialRiskAreaDefinition> RiskAreas
    {
        get => _riskAreas;
        init => _riskAreas = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<SpatialNodeDefinition> Nodes
    {
        get => _nodes;
        init => _nodes = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<SpatialEdgeDefinition> Edges
    {
        get => _edges;
        init => _edges = Array.AsReadOnly(value.ToArray());
    }
}

public sealed class SpatialWorldValidationException : Exception
{
    public SpatialWorldValidationException(string message)
        : base(message)
    {
    }

    public SpatialWorldValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
