using System.Text.Json;

namespace Gridworks.Core.Release.V2;

public static class SpatialWorldLoader
{
    public const string SupportedSchemaVersion =
        "gridworks.commercial.free-placement-slice.v1";

    public static SpatialWorldDefinition Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Load(System.Text.Encoding.UTF8.GetBytes(json));
    }

    public static SpatialWorldDefinition Load(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8Json.ToArray());
            JsonElement root = document.RootElement;
            RequireObject(root, "$", RootFields);
            SpatialWorldDefinition world = new(
                String(root, "schemaVersion", "$"),
                String(root, "worldId", "$"),
                String(root, "displayName", "$"),
                Int32(root, "unitsPerDesignUnit", "$"),
                Bounds(Property(root, "bounds", "$"), "$.bounds"),
                Int64(root, "initialCashUnit", "$"),
                Array(root, "nodeClasses", "$", NodeClass),
                Array(root, "lineClasses", "$", LineClass),
                Array(root, "terrain", "$", Terrain),
                Array(root, "riskAreas", "$", RiskArea),
                Array(root, "nodes", "$", Node),
                Array(root, "edges", "$", Edge));
            Validate(world);
            return world;
        }
        catch (SpatialWorldValidationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new SpatialWorldValidationException(
                "Stage-B spatial fixture is not valid strict JSON.",
                exception);
        }
        catch (Exception exception) when (
            exception is OverflowException or InvalidOperationException or FormatException)
        {
            throw new SpatialWorldValidationException(
                "Stage-B spatial fixture contains an invalid value.",
                exception);
        }
    }

    public static void Validate(SpatialWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(world);
        Require(world.SchemaVersion == SupportedSchemaVersion,
            $"$.schemaVersion must equal '{SupportedSchemaVersion}'.");
        RequireText(world.WorldId, "$.worldId");
        RequireText(world.DisplayName, "$.displayName");
        Require(world.UnitsPerDesignUnit == SpatialWorldDefinition.RequiredUnitsPerDesignUnit,
            "$.unitsPerDesignUnit must equal 100.");
        Require(world.Bounds.MinXUnit < world.Bounds.MaxXUnit,
            "$.bounds x range must increase.");
        Require(world.Bounds.MinYUnit < world.Bounds.MaxYUnit,
            "$.bounds y range must increase.");
        Require(world.InitialCashUnit >= 0, "$.initialCashUnit must be nonnegative.");

        Dictionary<string, SpatialNodeClassDefinition> nodeClasses =
            Unique(world.NodeClasses, item => item.ClassId, "$.nodeClasses");
        foreach (SpatialNodeClassDefinition definition in world.NodeClasses)
        {
            RequireText(definition.DisplayName, "$.nodeClasses[].displayName");
            Require(definition.FootprintRadiusUnit > 0,
                "Node footprint radius must be positive.");
            Require(definition.MaxConnections > 0,
                "Node maxConnections must be positive.");
            Require(definition.CostCashUnit >= 0 && definition.BuildMinutes >= 0,
                "Node cost and build time must be nonnegative.");
            if (definition.Kind is SpatialNodeKind.Pole or SpatialNodeKind.Substation)
            {
                Require(definition.CostCashUnit > 0 && definition.BuildMinutes > 0,
                    "Buildable nodes need positive cost and build time.");
            }
        }

        Dictionary<string, SpatialLineClassDefinition> lineClasses =
            Unique(world.LineClasses, item => item.ClassId, "$.lineClasses");
        foreach (SpatialLineClassDefinition definition in world.LineClasses)
        {
            RequireText(definition.DisplayName, "$.lineClasses[].displayName");
            Require(definition.MaxSpanUnit > 0,
                "Line max span must be positive.");
            Require(definition.CostCashUnitPerDesignUnit > 0 &&
                definition.BuildMinutesPerDesignUnit > 0,
                "Line rates must be positive.");
        }

        Unique(world.Terrain, item => item.TerrainId, "$.terrain");
        foreach (TerrainPolygonDefinition terrain in world.Terrain)
        {
            RequireText(terrain.DisplayName, "$.terrain[].displayName");
            ValidatePolygon(terrain.Polygon, world.Bounds, "$.terrain[].polygon");
        }

        Unique(world.RiskAreas, item => item.RiskAreaId, "$.riskAreas");
        foreach (SpatialRiskAreaDefinition riskArea in world.RiskAreas)
        {
            RequireText(riskArea.DisplayName, "$.riskAreas[].displayName");
            ValidatePolygon(riskArea.Polygon, world.Bounds, "$.riskAreas[].polygon");
        }

        Dictionary<string, SpatialNodeDefinition> nodes =
            Unique(world.Nodes, item => item.NodeId, "$.nodes");
        for (int index = 0; index < world.Nodes.Count; index++)
        {
            SpatialNodeDefinition node = world.Nodes[index];
            RequireText(node.DisplayName, "$.nodes[].displayName");
            Require(node.Commissioned, "All Stage-B authored nodes must be commissioned.");
            Require(nodeClasses.TryGetValue(node.ClassId, out SpatialNodeClassDefinition? nodeClass),
                $"Node '{node.NodeId}' references an unknown class.");
            Require(FixedGeometry.CircleWithinBounds(
                    node.Position,
                    nodeClass!.FootprintRadiusUnit,
                    world.Bounds),
                $"Node '{node.NodeId}' footprint leaves the map.");

            bool touchesWater = false;
            foreach (TerrainPolygonDefinition terrain in world.Terrain)
            {
                if (!FixedGeometry.CircleIntersectsPolygon(
                        node.Position,
                        nodeClass.FootprintRadiusUnit,
                        terrain.Polygon))
                {
                    continue;
                }

                Require(terrain.Kind != TerrainKind.Building,
                    $"Node '{node.NodeId}' overlaps a building polygon.");
                touchesWater = true;
            }
            if (node.AuthoredFoundation)
            {
                Require(nodeClass.Kind == SpatialNodeKind.Pole && touchesWater,
                    $"Node '{node.NodeId}' foundation must be a fixed pole touching water.");
            }
            else
            {
                Require(!touchesWater,
                    $"Node '{node.NodeId}' overlaps water without a foundation.");
            }

            for (int previous = 0; previous < index; previous++)
            {
                SpatialNodeDefinition other = world.Nodes[previous];
                SpatialNodeClassDefinition otherClass = nodeClasses[other.ClassId];
                Require(!FixedGeometry.CirclesTouchOrOverlap(
                        node.Position,
                        nodeClass.FootprintRadiusUnit,
                        other.Position,
                        otherClass.FootprintRadiusUnit),
                    $"Nodes '{node.NodeId}' and '{other.NodeId}' have touching footprints.");
            }
        }

        Dictionary<string, SpatialEdgeDefinition> edges =
            Unique(world.Edges, item => item.EdgeId, "$.edges");
        HashSet<(string First, string Second)> endpointPairs = new();
        Dictionary<string, int> connections = nodes.Keys.ToDictionary(
            key => key,
            _ => 0,
            StringComparer.Ordinal);
        List<(SpatialEdgeDefinition Edge, MapPoint Start, MapPoint End)> validatedEdges = new();
        foreach (SpatialEdgeDefinition edge in edges.Values)
        {
            Require(edge.Commissioned, "All Stage-B authored edges must be commissioned.");
            Require(lineClasses.TryGetValue(edge.LineClassId, out SpatialLineClassDefinition? lineClass),
                $"Edge '{edge.EdgeId}' references an unknown line class.");
            Require(nodes.TryGetValue(edge.FromNodeId, out SpatialNodeDefinition? from),
                $"Edge '{edge.EdgeId}' references an unknown from node.");
            Require(nodes.TryGetValue(edge.ToNodeId, out SpatialNodeDefinition? to),
                $"Edge '{edge.EdgeId}' references an unknown to node.");
            Require(edge.FromNodeId != edge.ToNodeId,
                $"Edge '{edge.EdgeId}' must have different endpoints.");
            (string First, string Second) pair = OrderedPair(edge.FromNodeId, edge.ToNodeId);
            Require(endpointPairs.Add(pair), "Duplicate unordered edge endpoints are not allowed.");
            long length = FixedGeometry.CeilDistance(from!.Position, to!.Position);
            Require(length > 0 && length <= lineClass!.MaxSpanUnit,
                $"Edge '{edge.EdgeId}' violates its span limit.");
            foreach (TerrainPolygonDefinition terrain in world.Terrain.Where(item =>
                         item.Kind == TerrainKind.Building))
            {
                Require(!FixedGeometry.SegmentIntersectsPolygon(
                        from.Position,
                        to.Position,
                        terrain.Polygon),
                    $"Edge '{edge.EdgeId}' crosses a building polygon.");
            }
            foreach (SpatialNodeDefinition node in world.Nodes)
            {
                if (string.Equals(node.NodeId, edge.FromNodeId, StringComparison.Ordinal) ||
                    string.Equals(node.NodeId, edge.ToNodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                Require(!FixedGeometry.SegmentTouchesCircle(
                        from.Position,
                        to.Position,
                        node.Position,
                        nodeClasses[node.ClassId].FootprintRadiusUnit),
                    $"Edge '{edge.EdgeId}' touches third node '{node.NodeId}'.");
            }
            foreach ((SpatialEdgeDefinition _, MapPoint start, MapPoint end) in validatedEdges)
            {
                Require(!FixedGeometry.CollinearPositiveOverlap(
                        from.Position,
                        to.Position,
                        start,
                        end),
                    $"Edge '{edge.EdgeId}' overlaps an existing collinear segment.");
            }

            connections[edge.FromNodeId]++;
            connections[edge.ToNodeId]++;
            validatedEdges.Add((edge, from.Position, to.Position));
        }

        foreach ((string nodeId, int count) in connections)
        {
            Require(count <= nodeClasses[nodes[nodeId].ClassId].MaxConnections,
                $"Node '{nodeId}' exceeds its connection limit.");
        }
    }

    private static SpatialNodeClassDefinition NodeClass(JsonElement element, string path)
    {
        RequireObject(element, path, NodeClassFields);
        return new SpatialNodeClassDefinition(
            String(element, "classId", path),
            String(element, "displayName", path),
            EnumValue<SpatialNodeKind>(element, "kind", path),
            Int32(element, "footprintRadiusUnit", path),
            Int32(element, "maxConnections", path),
            Int64(element, "costCashUnit", path),
            Int32(element, "buildMinutes", path));
    }

    private static SpatialLineClassDefinition LineClass(JsonElement element, string path)
    {
        RequireObject(element, path, LineClassFields);
        return new SpatialLineClassDefinition(
            String(element, "classId", path),
            String(element, "displayName", path),
            Int32(element, "maxSpanUnit", path),
            Int64(element, "costCashUnitPerDesignUnit", path),
            Int32(element, "buildMinutesPerDesignUnit", path));
    }

    private static TerrainPolygonDefinition Terrain(JsonElement element, string path)
    {
        RequireObject(element, path, TerrainFields);
        return new TerrainPolygonDefinition(
            String(element, "terrainId", path),
            String(element, "displayName", path),
            EnumValue<TerrainKind>(element, "kind", path),
            Array(element, "polygon", path, Point));
    }

    private static SpatialRiskAreaDefinition RiskArea(JsonElement element, string path)
    {
        RequireObject(element, path, RiskAreaFields);
        return new SpatialRiskAreaDefinition(
            String(element, "riskAreaId", path),
            String(element, "displayName", path),
            Array(element, "polygon", path, Point));
    }

    private static SpatialNodeDefinition Node(JsonElement element, string path)
    {
        RequireObject(element, path, NodeFields);
        return new SpatialNodeDefinition(
            String(element, "nodeId", path),
            String(element, "classId", path),
            String(element, "displayName", path),
            Point(Property(element, "position", path), $"{path}.position"),
            Boolean(element, "commissioned", path),
            Boolean(element, "authoredFoundation", path));
    }

    private static SpatialEdgeDefinition Edge(JsonElement element, string path)
    {
        RequireObject(element, path, EdgeFields);
        return new SpatialEdgeDefinition(
            String(element, "edgeId", path),
            String(element, "lineClassId", path),
            String(element, "fromNodeId", path),
            String(element, "toNodeId", path),
            Boolean(element, "commissioned", path));
    }

    private static MapBounds Bounds(JsonElement element, string path)
    {
        RequireObject(element, path, BoundsFields);
        return new MapBounds(
            Int32(element, "minXUnit", path),
            Int32(element, "minYUnit", path),
            Int32(element, "maxXUnit", path),
            Int32(element, "maxYUnit", path));
    }

    private static MapPoint Point(JsonElement element, string path)
    {
        RequireObject(element, path, PointFields);
        return new MapPoint(
            Int32(element, "xUnit", path),
            Int32(element, "yUnit", path));
    }

    private static T EnumValue<T>(JsonElement element, string name, string path)
        where T : struct, Enum
    {
        string value = String(element, name, path);
        if (!Enum.TryParse(value, ignoreCase: false, out T result) ||
            !Enum.IsDefined(result))
        {
            throw new SpatialWorldValidationException(
                $"{path}.{name} has an unknown value.");
        }

        return result;
    }

    private static IReadOnlyList<T> Array<T>(
        JsonElement parent,
        string name,
        string path,
        Func<JsonElement, string, T> parser)
    {
        JsonElement element = Property(parent, name, path);
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new SpatialWorldValidationException($"{path}.{name} must be an array.");
        }

        List<T> values = new();
        int index = 0;
        foreach (JsonElement item in element.EnumerateArray())
        {
            values.Add(parser(item, $"{path}.{name}[{index}]"));
            index++;
        }

        return System.Array.AsReadOnly(values.ToArray());
    }

    private static JsonElement Property(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out JsonElement value))
        {
            throw new SpatialWorldValidationException($"{path}.{name} is required.");
        }

        return value;
    }

    private static string String(JsonElement parent, string name, string path)
    {
        JsonElement element = Property(parent, name, path);
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new SpatialWorldValidationException($"{path}.{name} must be a string.");
        }

        return element.GetString()!;
    }

    private static int Int32(JsonElement parent, string name, string path)
    {
        JsonElement element = Property(parent, name, path);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value))
        {
            throw new SpatialWorldValidationException($"{path}.{name} must be an Int32.");
        }

        return value;
    }

    private static long Int64(JsonElement parent, string name, string path)
    {
        JsonElement element = Property(parent, name, path);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out long value))
        {
            throw new SpatialWorldValidationException($"{path}.{name} must be an Int64.");
        }

        return value;
    }

    private static bool Boolean(JsonElement parent, string name, string path)
    {
        JsonElement element = Property(parent, name, path);
        if (element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new SpatialWorldValidationException($"{path}.{name} must be a boolean.");
        }

        return element.GetBoolean();
    }

    private static void RequireObject(
        JsonElement element,
        string path,
        IReadOnlySet<string> expectedFields)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new SpatialWorldValidationException($"{path} must be an object.");
        }

        HashSet<string> actual = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            Require(actual.Add(property.Name), $"{path}.{property.Name} is duplicated.");
        }

        Require(actual.SetEquals(expectedFields),
            $"{path} must contain exactly: {string.Join(", ", expectedFields.Order())}.");
    }

    private static void ValidatePolygon(
        IReadOnlyList<MapPoint> polygon,
        MapBounds bounds,
        string path)
    {
        Require(polygon.Count >= 3, $"{path} needs at least three points.");
        Require(polygon.Distinct().Count() == polygon.Count,
            $"{path} cannot repeat a point.");
        Require(FixedGeometry.SignedAreaTwice(polygon) != 0, $"{path} must have nonzero area.");
        foreach (MapPoint point in polygon)
        {
            Require(FixedGeometry.PointWithinBounds(point, bounds),
                $"{path} must remain inside map bounds.");
        }

        for (int first = 0; first < polygon.Count; first++)
        {
            MapPoint firstStart = polygon[first];
            MapPoint firstEnd = polygon[(first + 1) % polygon.Count];
            Require(firstStart != firstEnd, $"{path} cannot contain a zero-length edge.");
            for (int second = first + 1; second < polygon.Count; second++)
            {
                bool adjacent = second == first + 1 ||
                    (first == 0 && second == polygon.Count - 1);
                if (adjacent)
                {
                    Require(!FixedGeometry.CollinearPositiveOverlap(
                            firstStart,
                            firstEnd,
                            polygon[second],
                            polygon[(second + 1) % polygon.Count]),
                        $"{path} cannot retrace an adjacent edge.");
                    continue;
                }

                MapPoint secondStart = polygon[second];
                MapPoint secondEnd = polygon[(second + 1) % polygon.Count];
                Require(!FixedGeometry.SegmentsIntersectInclusive(
                        firstStart,
                        firstEnd,
                        secondStart,
                        secondEnd),
                    $"{path} must be a simple polygon without self-intersection.");
            }
        }
    }

    private static Dictionary<string, T> Unique<T>(
        IReadOnlyList<T> values,
        Func<T, string> id,
        string path)
    {
        Dictionary<string, T> result = new(StringComparer.Ordinal);
        foreach (T value in values)
        {
            string key = id(value);
            RequireText(key, $"{path}[].id");
            Require(result.TryAdd(key, value), $"{path} contains duplicate id '{key}'.");
        }

        return result;
    }

    private static (string First, string Second) OrderedPair(string first, string second) =>
        string.CompareOrdinal(first, second) <= 0 ? (first, second) : (second, first);

    private static void RequireText(string value, string path) =>
        Require(!string.IsNullOrWhiteSpace(value) && value == value.Trim(),
            $"{path} must be nonblank and trimmed.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new SpatialWorldValidationException(message);
        }
    }

    private static readonly IReadOnlySet<string> RootFields = Fields(
        "schemaVersion", "worldId", "displayName", "unitsPerDesignUnit", "bounds",
        "initialCashUnit", "nodeClasses", "lineClasses", "terrain", "riskAreas", "nodes", "edges");
    private static readonly IReadOnlySet<string> BoundsFields = Fields(
        "minXUnit", "minYUnit", "maxXUnit", "maxYUnit");
    private static readonly IReadOnlySet<string> PointFields = Fields("xUnit", "yUnit");
    private static readonly IReadOnlySet<string> NodeClassFields = Fields(
        "classId", "displayName", "kind", "footprintRadiusUnit", "maxConnections",
        "costCashUnit", "buildMinutes");
    private static readonly IReadOnlySet<string> LineClassFields = Fields(
        "classId", "displayName", "maxSpanUnit",
        "costCashUnitPerDesignUnit", "buildMinutesPerDesignUnit");
    private static readonly IReadOnlySet<string> TerrainFields = Fields(
        "terrainId", "displayName", "kind", "polygon");
    private static readonly IReadOnlySet<string> RiskAreaFields = Fields(
        "riskAreaId", "displayName", "polygon");
    private static readonly IReadOnlySet<string> NodeFields = Fields(
        "nodeId", "classId", "displayName", "position", "commissioned", "authoredFoundation");
    private static readonly IReadOnlySet<string> EdgeFields = Fields(
        "edgeId", "lineClassId", "fromNodeId", "toNodeId", "commissioned");

    private static IReadOnlySet<string> Fields(params string[] names) =>
        new HashSet<string>(names, StringComparer.Ordinal);
}
