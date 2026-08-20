using System.Text.Json;

namespace Gridworks.Core.Release.V2;

public static class CommercialWorldLoader
{
    public const string SupportedSchemaVersion = "gridworks.commercial.world.v2";

    public static CommercialWorldDefinition Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Load(System.Text.Encoding.UTF8.GetBytes(json));
    }

    public static CommercialWorldDefinition Load(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8Json.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
            JsonElement root = document.RootElement;
            RequireObject(root, "$", RootFields);
            CommercialWorldDefinition world = new(
                String(root, "schemaVersion", "$"),
                String(root, "worldId", "$"),
                String(root, "displayName", "$"),
                SpatialWorldLoader.Load(Property(root, "spatial", "$", JsonValueKind.Object).GetRawText()),
                Array(root, "thermalNodeClasses", "$", ThermalNodeClass),
                Array(root, "thermalLineClasses", "$", ThermalLineClass),
                Array(root, "generationSources", "$", GenerationSource));
            Validate(world);
            return world;
        }
        catch (CommercialWorldValidationException)
        {
            throw;
        }
        catch (SpatialWorldValidationException exception)
        {
            throw new CommercialWorldValidationException(
                "$.spatial is not a valid fixed-point spatial world.",
                exception);
        }
        catch (JsonException exception)
        {
            throw new CommercialWorldValidationException(
                "Commercial world v2 is not valid strict JSON.",
                exception);
        }
        catch (Exception exception) when (
            exception is OverflowException or InvalidOperationException or FormatException)
        {
            throw new CommercialWorldValidationException(
                "Commercial world v2 contains an invalid value.",
                exception);
        }
    }

    public static void Validate(CommercialWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(world);
        Require(world.SchemaVersion == SupportedSchemaVersion,
            $"$.schemaVersion must equal '{SupportedSchemaVersion}'.");
        RequireText(world.WorldId, "$.worldId");
        RequireText(world.DisplayName, "$.displayName");
        Require(world.WorldId == world.Spatial.WorldId,
            "$.worldId must equal $.spatial.worldId.");
        SpatialWorldLoader.Validate(world.Spatial);

        Dictionary<string, SpatialNodeClassDefinition> spatialNodeClasses =
            world.Spatial.NodeClasses.ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        Dictionary<string, SpatialLineClassDefinition> spatialLineClasses =
            world.Spatial.LineClasses.ToDictionary(item => item.ClassId, StringComparer.Ordinal);

        Dictionary<string, ThermalNodeClassDefinition> thermalNodeClasses =
            Unique(world.ThermalNodeClasses, item => item.ClassId, "$.thermalNodeClasses");
        foreach (ThermalNodeClassDefinition definition in world.ThermalNodeClasses)
        {
            Require(spatialNodeClasses.TryGetValue(definition.ClassId, out SpatialNodeClassDefinition? nodeClass),
                $"Thermal node class '{definition.ClassId}' does not exist in the spatial world.");
            Require(nodeClass!.Kind is SpatialNodeKind.Pole or SpatialNodeKind.Substation,
                $"Thermal node class '{definition.ClassId}' must represent a pole or substation.");
            ValidateLimits(definition.ContinuousLimitKw, definition.EmergencyLimitKw,
                $"Thermal node class '{definition.ClassId}'");
        }

        foreach (SpatialNodeClassDefinition nodeClass in world.Spatial.NodeClasses.Where(item =>
                     item.Kind is SpatialNodeKind.Pole or SpatialNodeKind.Substation))
        {
            Require(thermalNodeClasses.ContainsKey(nodeClass.ClassId),
                $"Pole/substation class '{nodeClass.ClassId}' needs thermal limits.");
        }

        Dictionary<string, ThermalLineClassDefinition> thermalLineClasses =
            Unique(world.ThermalLineClasses, item => item.ClassId, "$.thermalLineClasses");
        foreach (ThermalLineClassDefinition definition in world.ThermalLineClasses)
        {
            Require(spatialLineClasses.ContainsKey(definition.ClassId),
                $"Thermal line class '{definition.ClassId}' does not exist in the spatial world.");
            ValidateLimits(definition.ContinuousLimitKw, definition.EmergencyLimitKw,
                $"Thermal line class '{definition.ClassId}'");
        }

        foreach (SpatialLineClassDefinition lineClass in world.Spatial.LineClasses)
        {
            Require(thermalLineClasses.ContainsKey(lineClass.ClassId),
                $"Line class '{lineClass.ClassId}' needs thermal limits.");
        }

        Dictionary<string, SpatialNodeDefinition> nodes = world.Spatial.Nodes.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        Unique(world.GenerationSources, item => item.NodeId, "$.generationSources");
        HashSet<int> authoredOrders = [];
        foreach (GenerationSourceDefinition source in world.GenerationSources)
        {
            Require(nodes.TryGetValue(source.NodeId, out SpatialNodeDefinition? node),
                $"Generation source '{source.NodeId}' does not exist.");
            Require(spatialNodeClasses[node!.ClassId].Kind == SpatialNodeKind.SourceTerminal,
                $"Generation source '{source.NodeId}' is not a source terminal.");
            Require(source.OutputCapacityKw > 0,
                $"Generation source '{source.NodeId}' needs positive output capacity.");
            Require(source.AuthoredOrder >= 0 && authoredOrders.Add(source.AuthoredOrder),
                "Generation source authoredOrder values must be unique and nonnegative.");
        }
        Require(world.GenerationSources.Count > 0, "At least one generation source is required.");
        foreach (SpatialNodeDefinition sourceNode in world.Spatial.Nodes.Where(item =>
                     spatialNodeClasses[item.ClassId].Kind == SpatialNodeKind.SourceTerminal))
        {
            Require(world.GenerationSources.Any(item => item.NodeId == sourceNode.NodeId),
                $"Source terminal '{sourceNode.NodeId}' needs a generation source definition.");
        }
    }

    public static ThermalLimitDefinition LimitForAsset(
        CommercialWorldDefinition world,
        string assetId)
    {
        SpatialEdgeDefinition? edge = world.Spatial.Edges.FirstOrDefault(item => item.EdgeId == assetId);
        if (edge is not null)
        {
            ThermalLineClassDefinition limit = world.ThermalLineClasses.Single(item =>
                item.ClassId == edge.LineClassId);
            return new ThermalLimitDefinition(limit.ContinuousLimitKw, limit.EmergencyLimitKw);
        }

        SpatialNodeDefinition? node = world.Spatial.Nodes.FirstOrDefault(item => item.NodeId == assetId);
        if (node is not null)
        {
            ThermalNodeClassDefinition? limit = world.ThermalNodeClasses.FirstOrDefault(item =>
                item.ClassId == node.ClassId);
            if (limit is not null)
            {
                return new ThermalLimitDefinition(limit.ContinuousLimitKw, limit.EmergencyLimitKw);
            }
        }
        throw new KeyNotFoundException($"Thermal asset '{assetId}' was not found.");
    }

    public static IReadOnlyList<string> ThermalAssetIds(CommercialWorldDefinition world)
    {
        HashSet<string> nodeClassIds = world.ThermalNodeClasses
            .Select(item => item.ClassId)
            .ToHashSet(StringComparer.Ordinal);
        return world.Spatial.Nodes
            .Where(item => item.Commissioned && nodeClassIds.Contains(item.ClassId))
            .Select(item => item.NodeId)
            .Concat(world.Spatial.Edges.Where(item => item.Commissioned).Select(item => item.EdgeId))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static ThermalNodeClassDefinition ThermalNodeClass(JsonElement element, string path)
    {
        RequireObject(element, path, ThermalClassFields);
        return new ThermalNodeClassDefinition(
            String(element, "classId", path),
            Int64(element, "continuousLimitKw", path),
            Int64(element, "emergencyLimitKw", path));
    }

    private static ThermalLineClassDefinition ThermalLineClass(JsonElement element, string path)
    {
        RequireObject(element, path, ThermalClassFields);
        return new ThermalLineClassDefinition(
            String(element, "classId", path),
            Int64(element, "continuousLimitKw", path),
            Int64(element, "emergencyLimitKw", path));
    }

    private static GenerationSourceDefinition GenerationSource(JsonElement element, string path)
    {
        RequireObject(element, path, GenerationSourceFields);
        return new GenerationSourceDefinition(
            String(element, "nodeId", path),
            Int64(element, "outputCapacityKw", path),
            Int32(element, "authoredOrder", path));
    }

    private static IReadOnlyList<T> Array<T>(
        JsonElement parent,
        string propertyName,
        string path,
        Func<JsonElement, string, T> parser)
    {
        JsonElement array = Property(parent, propertyName, path, JsonValueKind.Array);
        List<T> values = [];
        int index = 0;
        foreach (JsonElement element in array.EnumerateArray())
        {
            values.Add(parser(element, $"{path}.{propertyName}[{index}]") );
            index++;
        }
        return values.AsReadOnly();
    }

    private static JsonElement Property(
        JsonElement parent,
        string propertyName,
        string path,
        JsonValueKind expectedKind)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value))
        {
            throw new CommercialWorldValidationException($"{path}.{propertyName} is required.");
        }
        if (value.ValueKind != expectedKind)
        {
            throw new CommercialWorldValidationException(
                $"{path}.{propertyName} must be {expectedKind}.");
        }
        return value;
    }

    private static string String(JsonElement parent, string propertyName, string path)
    {
        string? value = Property(parent, propertyName, path, JsonValueKind.String).GetString();
        RequireText(value, $"{path}.{propertyName}");
        return value!;
    }

    private static long Int64(JsonElement parent, string propertyName, string path)
    {
        JsonElement value = Property(parent, propertyName, path, JsonValueKind.Number);
        if (!value.TryGetInt64(out long number))
        {
            throw new CommercialWorldValidationException($"{path}.{propertyName} must be an integer.");
        }
        return number;
    }

    private static int Int32(JsonElement parent, string propertyName, string path)
    {
        long value = Int64(parent, propertyName, path);
        if (value < int.MinValue || value > int.MaxValue)
        {
            throw new CommercialWorldValidationException($"{path}.{propertyName} is outside Int32 range.");
        }
        return (int)value;
    }

    private static void RequireObject(
        JsonElement element,
        string path,
        IReadOnlySet<string> expectedFields)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new CommercialWorldValidationException($"{path} must be an object.");
        }
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            Require(seen.Add(property.Name), $"{path} contains duplicate property '{property.Name}'.");
            Require(expectedFields.Contains(property.Name),
                $"{path} contains unknown property '{property.Name}'.");
        }
        foreach (string required in expectedFields)
        {
            Require(seen.Contains(required), $"{path}.{required} is required.");
        }
    }

    private static Dictionary<string, T> Unique<T>(
        IReadOnlyList<T> values,
        Func<T, string> keySelector,
        string path)
    {
        Dictionary<string, T> result = new(StringComparer.Ordinal);
        foreach (T value in values)
        {
            string key = keySelector(value);
            RequireText(key, path);
            Require(result.TryAdd(key, value), $"{path} contains duplicate ID '{key}'.");
        }
        return result;
    }

    private static void ValidateLimits(long continuous, long emergency, string path) =>
        Require(continuous > 0 && continuous <= emergency,
            $"{path} must satisfy 0 < continuousLimitKw <= emergencyLimitKw.");

    private static void RequireText(string? value, string path) =>
        Require(!string.IsNullOrWhiteSpace(value), $"{path} must be nonempty text.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new CommercialWorldValidationException(message);
        }
    }

    private static readonly IReadOnlySet<string> RootFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "schemaVersion",
        "worldId",
        "displayName",
        "spatial",
        "thermalNodeClasses",
        "thermalLineClasses",
        "generationSources",
    };

    private static readonly IReadOnlySet<string> ThermalClassFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "classId",
        "continuousLimitKw",
        "emergencyLimitKw",
    };

    private static readonly IReadOnlySet<string> GenerationSourceFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "nodeId",
        "outputCapacityKw",
        "authoredOrder",
    };
}
