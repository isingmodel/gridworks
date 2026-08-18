using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Core.Release.V2;

public static class CommercialWorldLoader
{
    public const string SupportedSchemaVersion = "gridworks.commercial.world.v2";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
        },
    };

    public static CommercialWorldDefinition Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Load(System.Text.Encoding.UTF8.GetBytes(json));
    }

    public static CommercialWorldDefinition Load(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            byte[] bytes = utf8Json.ToArray();
            using JsonDocument document = JsonDocument.Parse(bytes);
            RejectDuplicates(document.RootElement, "$");
            RawWorld raw = JsonSerializer.Deserialize<RawWorld>(bytes, Options)
                ?? throw new CommercialWorldValidationException("Commercial world is empty.");
            CommercialWorldDefinition world = Convert(raw);
            Validate(world);
            return world;
        }
        catch (CommercialWorldValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or InvalidOperationException or
            OverflowException or SpatialWorldValidationException)
        {
            throw new CommercialWorldValidationException(
                "Commercial world contains an invalid value.",
                exception);
        }
    }

    public static void Validate(CommercialWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(world);
        Require(world.SchemaVersion == SupportedSchemaVersion,
            $"schemaVersion must equal '{SupportedSchemaVersion}'.");
        RequireText(world.WorldId, "worldId");
        RequireText(world.DisplayName, "displayName");
        Require(world.UnitsPerDesignUnit == SpatialWorldDefinition.RequiredUnitsPerDesignUnit,
            "unitsPerDesignUnit must equal 100.");
        Require(world.InitialCashUnit >= 0, "initialCashUnit must be nonnegative.");

        Dictionary<string, CommercialNodeClassDefinition> nodeClasses = Unique(
            world.NodeClasses,
            item => item.ClassId,
            "nodeClasses");
        foreach (CommercialNodeClassDefinition item in world.NodeClasses)
        {
            RequireText(item.DisplayName, $"nodeClasses[{item.ClassId}].displayName");
            bool thermalAsset = item.Kind is SpatialNodeKind.Pole or SpatialNodeKind.Substation;
            Require(thermalAsset == (item.ThermalLimit is not null),
                $"Node class '{item.ClassId}' has the wrong thermal-limit shape.");
            if (item.ThermalLimit is not null)
            {
                ValidateLimit(item.ThermalLimit, $"nodeClasses[{item.ClassId}].thermalLimit");
            }
        }

        Dictionary<string, CommercialLineClassDefinition> lineClasses = Unique(
            world.LineClasses,
            item => item.ClassId,
            "lineClasses");
        foreach (CommercialLineClassDefinition item in world.LineClasses)
        {
            RequireText(item.DisplayName, $"lineClasses[{item.ClassId}].displayName");
            ValidateLimit(item.ThermalLimit, $"lineClasses[{item.ClassId}].thermalLimit");
        }

        SpatialWorldDefinition spatial = world.ToSpatialWorld();
        SpatialWorldLoader.Validate(spatial);
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        HashSet<string> assetIds = world.Nodes.Select(item => item.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (SpatialEdgeDefinition edge in world.Edges)
        {
            Require(assetIds.Add(edge.EdgeId),
                $"Node and edge asset IDs must be globally unique: '{edge.EdgeId}'.");
        }

        Dictionary<string, CommercialSourceDefinition> sources = Unique(
            world.Sources,
            item => item.SourceId,
            "sources");
        Require(sources.Count > 0, "At least one source is required.");
        var sourceNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var dispatchOrders = new HashSet<int>();
        foreach (CommercialSourceDefinition source in world.Sources)
        {
            RequireText(source.DisplayName, $"sources[{source.SourceId}].displayName");
            Require(source.CapacityKw > 0, $"Source '{source.SourceId}' needs positive capacity.");
            Require(source.DispatchOrder >= 0,
                $"Source '{source.SourceId}' needs nonnegative dispatchOrder.");
            Require(dispatchOrders.Add(source.DispatchOrder),
                $"Source dispatchOrder '{source.DispatchOrder}' is duplicated.");
            Require(nodes.TryGetValue(source.NodeId, out SpatialNodeDefinition? node) &&
                nodeClasses[node.ClassId].Kind == SpatialNodeKind.SourceTerminal,
                $"Source '{source.SourceId}' must reference a source terminal.");
            Require(sourceNodeIds.Add(source.NodeId),
                $"Source terminal '{source.NodeId}' is assigned more than once.");
        }

        Dictionary<string, CommercialLoadDefinition> loads = Unique(
            world.Loads,
            item => item.LoadId,
            "loads");
        Require(loads.Count > 0, "At least one load is required.");
        var loadNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CommercialLoadDefinition load in world.Loads)
        {
            RequireText(load.DisplayName, $"loads[{load.LoadId}].displayName");
            Require(nodes.TryGetValue(load.NodeId, out SpatialNodeDefinition? node) &&
                nodeClasses[node.ClassId].Kind == SpatialNodeKind.DedicatedLoadTerminal,
                $"Load '{load.LoadId}' must reference a dedicated load terminal.");
            Require(loadNodeIds.Add(load.NodeId),
                $"Load terminal '{load.NodeId}' is assigned more than once.");
        }

        _ = lineClasses;
    }

    private static CommercialWorldDefinition Convert(RawWorld raw) => new(
        raw.SchemaVersion,
        raw.WorldId,
        raw.DisplayName,
        raw.UnitsPerDesignUnit,
        new MapBounds(
            raw.Bounds.MinXUnit,
            raw.Bounds.MinYUnit,
            raw.Bounds.MaxXUnit,
            raw.Bounds.MaxYUnit),
        raw.InitialCashUnit,
        raw.NodeClasses.Select(item => new CommercialNodeClassDefinition(
            item.ClassId,
            item.DisplayName,
            item.Kind,
            item.FootprintRadiusUnit,
            item.MaxConnections,
            item.CostCashUnit,
            item.BuildMinutes,
            item.ThermalLimit is null
                ? null
                : new ThermalLimit(
                    item.ThermalLimit.ContinuousKw,
                    item.ThermalLimit.EmergencyKw))).ToArray(),
        raw.LineClasses.Select(item => new CommercialLineClassDefinition(
            item.ClassId,
            item.DisplayName,
            item.MaxSpanUnit,
            item.CostCashUnitPerDesignUnit,
            item.BuildMinutesPerDesignUnit,
            new ThermalLimit(
                item.ThermalLimit.ContinuousKw,
                item.ThermalLimit.EmergencyKw))).ToArray(),
        raw.Terrain.Select(item => new TerrainPolygonDefinition(
            item.TerrainId,
            item.DisplayName,
            item.Kind,
            item.Polygon.Select(Point).ToArray())).ToArray(),
        raw.RiskAreas.Select(item => new SpatialRiskAreaDefinition(
            item.RiskAreaId,
            item.DisplayName,
            item.Polygon.Select(Point).ToArray())).ToArray(),
        raw.Nodes.Select(item => new SpatialNodeDefinition(
            item.NodeId,
            item.ClassId,
            item.DisplayName,
            Point(item.Position),
            item.Commissioned,
            item.AuthoredFoundation)).ToArray(),
        raw.Edges.Select(item => new SpatialEdgeDefinition(
            item.EdgeId,
            item.LineClassId,
            item.FromNodeId,
            item.ToNodeId,
            item.Commissioned)).ToArray(),
        raw.Sources.Select(item => new CommercialSourceDefinition(
            item.SourceId,
            item.DisplayName,
            item.NodeId,
            item.CapacityKw,
            item.DispatchOrder)).ToArray(),
        raw.Loads.Select(item => new CommercialLoadDefinition(
            item.LoadId,
            item.DisplayName,
            item.NodeId)).ToArray());

    private static MapPoint Point(RawPoint point) => new(point.XUnit, point.YUnit);

    private static void ValidateLimit(ThermalLimit limit, string path)
    {
        Require(limit.ContinuousKw > 0, $"{path}.continuousKw must be positive.");
        Require(limit.EmergencyKw >= limit.ContinuousKw,
            $"{path}.emergencyKw must be at least continuousKw.");
    }

    private static Dictionary<string, T> Unique<T>(
        IReadOnlyList<T> values,
        Func<T, string> id,
        string path)
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (T value in values)
        {
            string key = id(value);
            RequireText(key, $"{path}[].id");
            Require(result.TryAdd(key, value), $"{path} contains duplicate ID '{key}'.");
        }
        return result;
    }

    private static void RejectDuplicates(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new CommercialWorldValidationException(
                        $"{path}.{property.Name} is duplicated.");
                }
                RejectDuplicates(property.Value, $"{path}.{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicates(item, $"{path}[{index++}]");
            }
        }
    }

    private static void RequireText(string value, string path) =>
        Require(
            !string.IsNullOrWhiteSpace(value) && value == value.Trim(),
            $"{path} must be nonblank and trimmed.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new CommercialWorldValidationException(message);
        }
    }

    private sealed class RawWorld
    {
        [JsonRequired] public string SchemaVersion { get; init; } = null!;
        [JsonRequired] public string WorldId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public int UnitsPerDesignUnit { get; init; }
        [JsonRequired] public RawBounds Bounds { get; init; } = null!;
        [JsonRequired] public long InitialCashUnit { get; init; }
        [JsonRequired] public RawNodeClass[] NodeClasses { get; init; } = null!;
        [JsonRequired] public RawLineClass[] LineClasses { get; init; } = null!;
        [JsonRequired] public RawTerrain[] Terrain { get; init; } = null!;
        [JsonRequired] public RawRiskArea[] RiskAreas { get; init; } = null!;
        [JsonRequired] public RawNode[] Nodes { get; init; } = null!;
        [JsonRequired] public RawEdge[] Edges { get; init; } = null!;
        [JsonRequired] public RawSource[] Sources { get; init; } = null!;
        [JsonRequired] public RawLoad[] Loads { get; init; } = null!;
    }

    private sealed class RawBounds
    {
        [JsonRequired] public int MinXUnit { get; init; }
        [JsonRequired] public int MinYUnit { get; init; }
        [JsonRequired] public int MaxXUnit { get; init; }
        [JsonRequired] public int MaxYUnit { get; init; }
    }

    private sealed class RawThermalLimit
    {
        [JsonRequired] public long ContinuousKw { get; init; }
        [JsonRequired] public long EmergencyKw { get; init; }
    }

    private sealed class RawNodeClass
    {
        [JsonRequired] public string ClassId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public SpatialNodeKind Kind { get; init; }
        [JsonRequired] public int FootprintRadiusUnit { get; init; }
        [JsonRequired] public int MaxConnections { get; init; }
        [JsonRequired] public long CostCashUnit { get; init; }
        [JsonRequired] public int BuildMinutes { get; init; }
        [JsonRequired] public RawThermalLimit? ThermalLimit { get; init; }
    }

    private sealed class RawLineClass
    {
        [JsonRequired] public string ClassId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public int MaxSpanUnit { get; init; }
        [JsonRequired] public long CostCashUnitPerDesignUnit { get; init; }
        [JsonRequired] public int BuildMinutesPerDesignUnit { get; init; }
        [JsonRequired] public RawThermalLimit ThermalLimit { get; init; } = null!;
    }

    private sealed class RawTerrain
    {
        [JsonRequired] public string TerrainId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public TerrainKind Kind { get; init; }
        [JsonRequired] public RawPoint[] Polygon { get; init; } = null!;
    }

    private sealed class RawRiskArea
    {
        [JsonRequired] public string RiskAreaId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public RawPoint[] Polygon { get; init; } = null!;
    }

    private sealed class RawPoint
    {
        [JsonRequired] public int XUnit { get; init; }
        [JsonRequired] public int YUnit { get; init; }
    }

    private sealed class RawNode
    {
        [JsonRequired] public string NodeId { get; init; } = null!;
        [JsonRequired] public string ClassId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public RawPoint Position { get; init; } = null!;
        [JsonRequired] public bool Commissioned { get; init; }
        [JsonRequired] public bool AuthoredFoundation { get; init; }
    }

    private sealed class RawEdge
    {
        [JsonRequired] public string EdgeId { get; init; } = null!;
        [JsonRequired] public string LineClassId { get; init; } = null!;
        [JsonRequired] public string FromNodeId { get; init; } = null!;
        [JsonRequired] public string ToNodeId { get; init; } = null!;
        [JsonRequired] public bool Commissioned { get; init; }
    }

    private sealed class RawSource
    {
        [JsonRequired] public string SourceId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public string NodeId { get; init; } = null!;
        [JsonRequired] public long CapacityKw { get; init; }
        [JsonRequired] public int DispatchOrder { get; init; }
    }

    private sealed class RawLoad
    {
        [JsonRequired] public string LoadId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public string NodeId { get; init; } = null!;
    }
}
