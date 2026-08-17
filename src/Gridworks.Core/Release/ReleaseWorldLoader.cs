using System.Text.Json;
using System.Text.RegularExpressions;

namespace Gridworks.Core.Release;

public static partial class ReleaseWorldLoader
{
    public const string SchemaVersion = "gridworks.release.world.v1";

    public static ReleaseWorldDefinition Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Load(System.Text.Encoding.UTF8.GetBytes(json));
    }

    public static ReleaseWorldDefinition Load(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            using var document = JsonDocument.Parse(
                utf8Json.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32,
                });

            var root = Object(
                document.RootElement,
                "$",
                "schemaVersion",
                "worldId",
                "displayName",
                "grid",
                "nodeClasses",
                "lineClasses",
                "nodes",
                "edges",
                "sources",
                "loads",
                "riskAreas");

            var world = new ReleaseWorldDefinition(
                String(root, "schemaVersion", "$"),
                String(root, "worldId", "$"),
                String(root, "displayName", "$"),
                ParseGrid(root["grid"]),
                Array(root, "nodeClasses", "$", ParseNodeClass),
                Array(root, "lineClasses", "$", ParseLineClass),
                Array(root, "nodes", "$", ParseNode),
                Array(root, "edges", "$", ParseEdge),
                Array(root, "sources", "$", ParseSource),
                Array(root, "loads", "$", ParseLoad),
                Array(root, "riskAreas", "$", ParseRiskArea));

            Validate(world);
            return world;
        }
        catch (ReleaseWorldValidationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new ReleaseWorldValidationException($"Invalid JSON: {exception.Message}");
        }
        catch (OverflowException exception)
        {
            throw new ReleaseWorldValidationException($"Numeric value is outside the supported range: {exception.Message}");
        }
    }

    private static ReleaseGridDefinition ParseGrid(JsonElement element)
    {
        const string path = "$.grid";
        var value = Object(element, path, "minX", "minY", "maxX", "maxY", "majorStep");
        return new ReleaseGridDefinition(
            Int32(value, "minX", path),
            Int32(value, "minY", path),
            Int32(value, "maxX", path),
            Int32(value, "maxY", path),
            Int32(value, "majorStep", path));
    }

    private static ReleaseNodeClassDefinition ParseNodeClass(JsonElement element, int index)
    {
        var path = $"$.nodeClasses[{index}]";
        var value = Object(
            element,
            path,
            "classId",
            "displayName",
            "kind",
            "maxConnections",
            "throughputKw",
            "transformerRatingKw",
            "serviceRadiusCells",
            "costCashUnit",
            "buildMinutes");

        return new ReleaseNodeClassDefinition(
            String(value, "classId", path),
            String(value, "displayName", path),
            ParseNodeKind(String(value, "kind", path), $"{path}.kind"),
            Int32(value, "maxConnections", path),
            NullableInt64(value, "throughputKw", path),
            NullableInt64(value, "transformerRatingKw", path),
            NullableInt32(value, "serviceRadiusCells", path),
            Int64(value, "costCashUnit", path),
            Int32(value, "buildMinutes", path));
    }

    private static ReleaseLineClassDefinition ParseLineClass(JsonElement element, int index)
    {
        var path = $"$.lineClasses[{index}]";
        var value = Object(
            element,
            path,
            "classId",
            "displayName",
            "ratingKw",
            "maxSpanCells",
            "costCashUnitPerMilliCell",
            "buildMinutesPerMilliCell");

        return new ReleaseLineClassDefinition(
            String(value, "classId", path),
            String(value, "displayName", path),
            Int64(value, "ratingKw", path),
            Int32(value, "maxSpanCells", path),
            Int64(value, "costCashUnitPerMilliCell", path),
            Int32(value, "buildMinutesPerMilliCell", path));
    }

    private static ReleaseNodeDefinition ParseNode(JsonElement element, int index)
    {
        var path = $"$.nodes[{index}]";
        var value = Object(element, path, "nodeId", "classId", "displayName", "position", "commissioned");
        return new ReleaseNodeDefinition(
            String(value, "nodeId", path),
            String(value, "classId", path),
            String(value, "displayName", path),
            ParsePoint(value["position"], $"{path}.position"),
            Boolean(value, "commissioned", path));
    }

    private static ReleaseEdgeDefinition ParseEdge(JsonElement element, int index)
    {
        var path = $"$.edges[{index}]";
        var value = Object(
            element,
            path,
            "edgeId",
            "lineClassId",
            "fromNodeId",
            "toNodeId",
            "commissioned");

        return new ReleaseEdgeDefinition(
            String(value, "edgeId", path),
            String(value, "lineClassId", path),
            String(value, "fromNodeId", path),
            String(value, "toNodeId", path),
            Boolean(value, "commissioned", path));
    }

    private static ReleaseSourceDefinition ParseSource(JsonElement element, int index)
    {
        var path = $"$.sources[{index}]";
        var value = Object(element, path, "sourceId", "nodeId", "displayName", "dispatchOrder", "capacityKw");
        return new ReleaseSourceDefinition(
            String(value, "sourceId", path),
            String(value, "nodeId", path),
            String(value, "displayName", path),
            Int32(value, "dispatchOrder", path),
            Int64(value, "capacityKw", path));
    }

    private static ReleaseLoadDefinition ParseLoad(JsonElement element, int index)
    {
        var path = $"$.loads[{index}]";
        var value = Object(
            element,
            path,
            "loadId",
            "displayName",
            "priority",
            "demandKw",
            "connectionKind",
            "position",
            "dedicatedNodeId");

        return new ReleaseLoadDefinition(
            String(value, "loadId", path),
            String(value, "displayName", path),
            ParsePriority(String(value, "priority", path), $"{path}.priority"),
            Int64(value, "demandKw", path),
            ParseConnectionKind(String(value, "connectionKind", path), $"{path}.connectionKind"),
            ParsePoint(value["position"], $"{path}.position"),
            NullableString(value, "dedicatedNodeId", path));
    }

    private static ReleaseRiskAreaDefinition ParseRiskArea(JsonElement element, int index)
    {
        var path = $"$.riskAreas[{index}]";
        var value = Object(element, path, "riskAreaId", "displayName", "polygon");
        return new ReleaseRiskAreaDefinition(
            String(value, "riskAreaId", path),
            String(value, "displayName", path),
            Array(value, "polygon", path, (point, pointIndex) => ParsePoint(point, $"{path}.polygon[{pointIndex}]")));
    }

    private static ReleasePoint ParsePoint(JsonElement element, string path)
    {
        var value = Object(element, path, "x", "y");
        return new ReleasePoint(Int32(value, "x", path), Int32(value, "y", path));
    }

    public static void Validate(ReleaseWorldDefinition world)
    {
        Require(world.SchemaVersion == SchemaVersion, $"$.schemaVersion must equal '{SchemaVersion}'.");
        Id(world.WorldId, "$.worldId");
        Text(world.DisplayName, "$.displayName");
        Require(
            world.Grid == new ReleaseGridDefinition(0, 0, 32, 20, 4),
            "$.grid must be the canonical 33x21 grid with majorStep 4.");

        Require(world.NodeClasses.Count > 0, "$.nodeClasses must not be empty.");
        Require(world.LineClasses.Count > 0, "$.lineClasses must not be empty.");
        Require(world.Nodes.Count > 0, "$.nodes must not be empty.");
        Require(world.Sources.Count > 0, "$.sources must not be empty.");
        Require(world.Loads.Count > 0, "$.loads must not be empty.");

        Unique(world.NodeClasses.Select(item => item.ClassId), "$.nodeClasses[].classId");
        Unique(world.LineClasses.Select(item => item.ClassId), "$.lineClasses[].classId");
        Unique(world.Nodes.Select(item => item.NodeId), "$.nodes[].nodeId");
        Unique(world.Edges.Select(item => item.EdgeId), "$.edges[].edgeId");
        Unique(world.Sources.Select(item => item.SourceId), "$.sources[].sourceId");
        Unique(world.Loads.Select(item => item.LoadId), "$.loads[].loadId");
        Unique(world.RiskAreas.Select(item => item.RiskAreaId), "$.riskAreas[].riskAreaId");

        var nodeClasses = world.NodeClasses.ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        var lineClasses = world.LineClasses.ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        var nodes = world.Nodes.ToDictionary(item => item.NodeId, StringComparer.Ordinal);

        foreach (var definition in world.NodeClasses)
        {
            Id(definition.ClassId, $"node class '{definition.ClassId}'");
            Text(definition.DisplayName, $"node class '{definition.ClassId}'.displayName");
            Require(definition.MaxConnections is >= 1 and <= 8, $"node class '{definition.ClassId}' maxConnections must be 1..8.");
            Require(definition.CostCashUnit >= 0, $"node class '{definition.ClassId}' cost must be nonnegative.");
            Require(definition.BuildMinutes >= 0, $"node class '{definition.ClassId}' build time must be nonnegative.");

            switch (definition.Kind)
            {
                case ReleaseNodeKind.Pole:
                    Positive(definition.ThroughputKw, $"node class '{definition.ClassId}'.throughputKw");
                    Require(definition.TransformerRatingKw is null, $"node class '{definition.ClassId}' cannot define transformerRatingKw.");
                    Require(definition.ServiceRadiusCells is null, $"node class '{definition.ClassId}' cannot define serviceRadiusCells.");
                    break;
                case ReleaseNodeKind.DedicatedLoadTerminal:
                    Require(definition.ThroughputKw is null, $"node class '{definition.ClassId}' cannot define throughputKw.");
                    Require(definition.TransformerRatingKw is null, $"node class '{definition.ClassId}' cannot define transformerRatingKw.");
                    Require(definition.ServiceRadiusCells is null, $"node class '{definition.ClassId}' cannot define serviceRadiusCells.");
                    break;
                case ReleaseNodeKind.Substation:
                    Positive(definition.TransformerRatingKw, $"node class '{definition.ClassId}'.transformerRatingKw");
                    Require(definition.ServiceRadiusCells is > 0 and <= 16, $"node class '{definition.ClassId}' serviceRadiusCells must be 1..16.");
                    Require(definition.ThroughputKw is null, $"node class '{definition.ClassId}' cannot define throughputKw.");
                    break;
                case ReleaseNodeKind.SourceTerminal:
                    Require(definition.ThroughputKw is null, $"node class '{definition.ClassId}' cannot define throughputKw.");
                    Require(definition.TransformerRatingKw is null, $"node class '{definition.ClassId}' cannot define transformerRatingKw.");
                    Require(definition.ServiceRadiusCells is null, $"node class '{definition.ClassId}' cannot define serviceRadiusCells.");
                    break;
                default:
                    throw new ReleaseWorldValidationException($"node class '{definition.ClassId}' has an unsupported kind.");
            }
        }

        foreach (var definition in world.LineClasses)
        {
            Id(definition.ClassId, $"line class '{definition.ClassId}'");
            Text(definition.DisplayName, $"line class '{definition.ClassId}'.displayName");
            Require(definition.RatingKw > 0, $"line class '{definition.ClassId}' ratingKw must be positive.");
            Require(definition.MaxSpanCells is >= 1 and <= 12, $"line class '{definition.ClassId}' maxSpanCells must be 1..12.");
            Require(definition.CostCashUnitPerMilliCell > 0, $"line class '{definition.ClassId}' cost rate must be positive.");
            Require(definition.BuildMinutesPerMilliCell > 0, $"line class '{definition.ClassId}' build rate must be positive.");
        }

        var occupied = new HashSet<ReleasePoint>();
        foreach (var node in world.Nodes)
        {
            Id(node.NodeId, $"node '{node.NodeId}'");
            Id(node.ClassId, $"node '{node.NodeId}'.classId");
            Text(node.DisplayName, $"node '{node.NodeId}'.displayName");
            Require(nodeClasses.ContainsKey(node.ClassId), $"node '{node.NodeId}' references unknown class '{node.ClassId}'.");
            InGrid(node.Position, world.Grid, $"node '{node.NodeId}'.position");
            Require(occupied.Add(node.Position), $"node '{node.NodeId}' duplicates position ({node.Position.X},{node.Position.Y}).");
        }

        var endpointPairs = new HashSet<string>(StringComparer.Ordinal);
        var degree = world.Nodes.ToDictionary(item => item.NodeId, _ => 0, StringComparer.Ordinal);
        foreach (var edge in world.Edges)
        {
            Id(edge.EdgeId, $"edge '{edge.EdgeId}'");
            Require(lineClasses.TryGetValue(edge.LineClassId, out var lineClass), $"edge '{edge.EdgeId}' references unknown line class '{edge.LineClassId}'.");
            Require(nodes.TryGetValue(edge.FromNodeId, out var from), $"edge '{edge.EdgeId}' references unknown from node '{edge.FromNodeId}'.");
            Require(nodes.TryGetValue(edge.ToNodeId, out var to), $"edge '{edge.EdgeId}' references unknown to node '{edge.ToNodeId}'.");
            Require(edge.FromNodeId != edge.ToNodeId, $"edge '{edge.EdgeId}' cannot connect a node to itself.");
            var pair = StringComparer.Ordinal.Compare(edge.FromNodeId, edge.ToNodeId) < 0
                ? $"{edge.FromNodeId}\0{edge.ToNodeId}"
                : $"{edge.ToNodeId}\0{edge.FromNodeId}";
            Require(endpointPairs.Add(pair), $"edge '{edge.EdgeId}' duplicates an endpoint pair.");
            var distanceSquared = DistanceSquared(from!.Position, to!.Position);
            var maxSpanSquared = checked((long)lineClass!.MaxSpanCells * lineClass.MaxSpanCells);
            Require(distanceSquared <= maxSpanSquared, $"edge '{edge.EdgeId}' exceeds its line class maximum span.");
            degree[edge.FromNodeId]++;
            degree[edge.ToNodeId]++;
        }

        foreach (var node in world.Nodes)
        {
            var maxConnections = nodeClasses[node.ClassId].MaxConnections;
            Require(degree[node.NodeId] <= maxConnections, $"node '{node.NodeId}' exceeds maxConnections {maxConnections}.");
        }

        foreach (var source in world.Sources)
        {
            Id(source.SourceId, $"source '{source.SourceId}'");
            Text(source.DisplayName, $"source '{source.SourceId}'.displayName");
            Require(source.DispatchOrder >= 0, $"source '{source.SourceId}' dispatchOrder must be nonnegative.");
            Require(source.CapacityKw > 0, $"source '{source.SourceId}' capacityKw must be positive.");
            Require(nodes.TryGetValue(source.NodeId, out var node), $"source '{source.SourceId}' references unknown node '{source.NodeId}'.");
            Require(nodeClasses[node!.ClassId].Kind == ReleaseNodeKind.SourceTerminal, $"source '{source.SourceId}' node must be a source terminal.");
        }

        Require(world.Sources.Select(item => item.NodeId).Distinct(StringComparer.Ordinal).Count() == world.Sources.Count, "Each source must use a different source terminal.");

        foreach (var load in world.Loads)
        {
            Id(load.LoadId, $"load '{load.LoadId}'");
            Text(load.DisplayName, $"load '{load.LoadId}'.displayName");
            Require(load.DemandKw > 0, $"load '{load.LoadId}' demandKw must be positive.");
            InGrid(load.Position, world.Grid, $"load '{load.LoadId}'.position");

            if (load.ConnectionKind == ReleaseLoadConnectionKind.DedicatedNode)
            {
                Require(load.DedicatedNodeId is not null, $"load '{load.LoadId}' requires dedicatedNodeId.");
                Require(nodes.TryGetValue(load.DedicatedNodeId!, out var node), $"load '{load.LoadId}' references unknown dedicated node '{load.DedicatedNodeId}'.");
                Require(nodeClasses[node!.ClassId].Kind == ReleaseNodeKind.DedicatedLoadTerminal, $"load '{load.LoadId}' dedicated node must be a load terminal.");
                Require(node.Position == load.Position, $"load '{load.LoadId}' position must match its dedicated node.");
            }
            else
            {
                Require(load.DedicatedNodeId is null, $"service-area load '{load.LoadId}' cannot define dedicatedNodeId.");
            }
        }

        foreach (var area in world.RiskAreas)
        {
            Id(area.RiskAreaId, $"risk area '{area.RiskAreaId}'");
            Text(area.DisplayName, $"risk area '{area.RiskAreaId}'.displayName");
            Require(area.Polygon.Count >= 3, $"risk area '{area.RiskAreaId}' polygon needs at least three points.");
            foreach (var point in area.Polygon)
            {
                InGrid(point, world.Grid, $"risk area '{area.RiskAreaId}'.polygon");
            }

            Require(area.Polygon.Distinct().Count() >= 3, $"risk area '{area.RiskAreaId}' polygon needs three distinct points.");
            Require(SignedAreaTwice(area.Polygon) != 0, $"risk area '{area.RiskAreaId}' polygon cannot be collinear.");
        }
    }

    private static long DistanceSquared(ReleasePoint left, ReleasePoint right)
    {
        var dx = (long)left.X - right.X;
        var dy = (long)left.Y - right.Y;
        return checked((dx * dx) + (dy * dy));
    }

    private static long SignedAreaTwice(IReadOnlyList<ReleasePoint> polygon)
    {
        long sum = 0;
        for (var index = 0; index < polygon.Count; index++)
        {
            var current = polygon[index];
            var next = polygon[(index + 1) % polygon.Count];
            sum = checked(sum + ((long)current.X * next.Y) - ((long)next.X * current.Y));
        }

        return sum;
    }

    private static void InGrid(ReleasePoint point, ReleaseGridDefinition grid, string path) =>
        Require(
            point.X >= grid.MinX && point.X <= grid.MaxX && point.Y >= grid.MinY && point.Y <= grid.MaxY,
            $"{path} must be inside the grid.");

    private static ReleaseNodeKind ParseNodeKind(string value, string path) => value switch
    {
        "sourceTerminal" => ReleaseNodeKind.SourceTerminal,
        "pole" => ReleaseNodeKind.Pole,
        "substation" => ReleaseNodeKind.Substation,
        "dedicatedLoadTerminal" => ReleaseNodeKind.DedicatedLoadTerminal,
        _ => throw new ReleaseWorldValidationException($"{path} has unsupported value '{value}'."),
    };

    private static ReleaseLoadPriority ParsePriority(string value, string path) => value switch
    {
        "lifeSafety" => ReleaseLoadPriority.LifeSafety,
        "essentialService" => ReleaseLoadPriority.EssentialService,
        "residential" => ReleaseLoadPriority.Residential,
        "industrial" => ReleaseLoadPriority.Industrial,
        _ => throw new ReleaseWorldValidationException($"{path} has unsupported value '{value}'."),
    };

    private static ReleaseLoadConnectionKind ParseConnectionKind(string value, string path) => value switch
    {
        "serviceArea" => ReleaseLoadConnectionKind.ServiceArea,
        "dedicatedNode" => ReleaseLoadConnectionKind.DedicatedNode,
        _ => throw new ReleaseWorldValidationException($"{path} has unsupported value '{value}'."),
    };

    private static Dictionary<string, JsonElement> Object(JsonElement element, string path, params string[] fields)
    {
        Require(element.ValueKind == JsonValueKind.Object, $"{path} must be an object.");
        var expected = new HashSet<string>(fields, StringComparer.Ordinal);
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            Require(expected.Contains(property.Name), $"{path} contains unknown field '{property.Name}'.");
            Require(values.TryAdd(property.Name, property.Value), $"{path} contains duplicate field '{property.Name}'.");
        }

        foreach (var field in fields)
        {
            Require(values.ContainsKey(field), $"{path} is missing field '{field}'.");
        }

        return values;
    }

    private static IReadOnlyList<T> Array<T>(
        IReadOnlyDictionary<string, JsonElement> value,
        string field,
        string path,
        Func<JsonElement, int, T> parser)
    {
        var element = value[field];
        Require(element.ValueKind == JsonValueKind.Array, $"{path}.{field} must be an array.");
        var result = new List<T>();
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            result.Add(parser(item, index));
            index++;
        }

        return result;
    }

    private static string String(IReadOnlyDictionary<string, JsonElement> value, string field, string path)
    {
        var element = value[field];
        Require(element.ValueKind == JsonValueKind.String, $"{path}.{field} must be a string.");
        return element.GetString()!;
    }

    private static string? NullableString(IReadOnlyDictionary<string, JsonElement> value, string field, string path)
    {
        var element = value[field];
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        Require(element.ValueKind == JsonValueKind.String, $"{path}.{field} must be a string or null.");
        return element.GetString();
    }

    private static int Int32(IReadOnlyDictionary<string, JsonElement> value, string field, string path)
    {
        var element = value[field];
        var result = 0;
        Require(element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out result), $"{path}.{field} must be a 32-bit integer.");
        return result;
    }

    private static int? NullableInt32(IReadOnlyDictionary<string, JsonElement> value, string field, string path)
    {
        var element = value[field];
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var result = 0;
        Require(element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out result), $"{path}.{field} must be a 32-bit integer or null.");
        return result;
    }

    private static long Int64(IReadOnlyDictionary<string, JsonElement> value, string field, string path)
    {
        var element = value[field];
        long result = 0;
        Require(element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out result), $"{path}.{field} must be a 64-bit integer.");
        return result;
    }

    private static long? NullableInt64(IReadOnlyDictionary<string, JsonElement> value, string field, string path)
    {
        var element = value[field];
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        long result = 0;
        Require(element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out result), $"{path}.{field} must be a 64-bit integer or null.");
        return result;
    }

    private static bool Boolean(IReadOnlyDictionary<string, JsonElement> value, string field, string path)
    {
        var element = value[field];
        Require(element.ValueKind is JsonValueKind.True or JsonValueKind.False, $"{path}.{field} must be a boolean.");
        return element.GetBoolean();
    }

    private static void Id(string value, string path)
    {
        Require(!string.IsNullOrWhiteSpace(value), $"{path} must not be blank.");
        Require(IdPattern().IsMatch(value), $"{path} must use uppercase ASCII letters, digits and underscores.");
    }

    private static void Text(string value, string path) =>
        Require(!string.IsNullOrWhiteSpace(value), $"{path} must not be blank.");

    private static void Positive(long? value, string path) =>
        Require(value is > 0, $"{path} must be positive.");

    private static void Unique(IEnumerable<string> values, string path)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            Id(value, path);
            Require(seen.Add(value), $"{path} contains duplicate '{value}'.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ReleaseWorldValidationException(message);
        }
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}
