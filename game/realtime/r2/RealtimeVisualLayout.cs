using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Game.Realtime.R2;

internal sealed class RealtimeVisualLayoutPoint
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}

internal sealed class RealtimeVisualDistrictLayout
{
    [JsonPropertyName("nodeId")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("center")]
    public RealtimeVisualLayoutPoint Center { get; set; } = new();

    [JsonPropertyName("spriteGround")]
    public RealtimeVisualLayoutPoint SpriteGround { get; set; } = new();

    [JsonPropertyName("footprint")]
    public RealtimeVisualLayoutPoint Footprint { get; set; } = new();

    [JsonPropertyName("worldMaxSide")]
    public int WorldMaxSide { get; set; }
}

internal sealed class RealtimeVisualSourceLayout
{
    [JsonPropertyName("nodeId")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("spriteGround")]
    public RealtimeVisualLayoutPoint SpriteGround { get; set; } = new();

    [JsonPropertyName("worldMaxSide")]
    public int WorldMaxSide { get; set; }
}

internal sealed class RealtimeVisualRoadLayout
{
    [JsonPropertyName("roadId")]
    public string RoadId { get; set; } = string.Empty;

    [JsonPropertyName("style")]
    public string Style { get; set; } = string.Empty;

    [JsonPropertyName("points")]
    public RealtimeVisualLayoutPoint[] Points { get; set; } = [];
}

internal sealed class RealtimeVisualLayoutDefinition
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("districts")]
    public RealtimeVisualDistrictLayout[] Districts { get; set; } = [];

    [JsonPropertyName("sources")]
    public RealtimeVisualSourceLayout[] Sources { get; set; } = [];

    [JsonPropertyName("roads")]
    public RealtimeVisualRoadLayout[] Roads { get; set; } = [];
}

internal static class RealtimeVisualLayoutStore
{
    internal const string SchemaVersion = "gridworks.realtime.visual-layout.v1";
    internal const string ResourcePath =
        "res://realtime/r2/realtime-visual-layout-v1.json";
    private const string EmbeddedResourceName =
        "Gridworks.Game.EmbeddedData.realtime-visual-layout-v1.json";

    private static readonly string[] DistrictIds =
    [
        "WATER_TERMINAL",
        "NORTH_RESIDENTIAL_TERMINAL",
        "EAST_RESIDENTIAL_TERMINAL",
        "HOSPITAL_TERMINAL",
        "FACTORY_TERMINAL",
    ];

    private static readonly string[] SourceIds =
    [
        "WEST_SOURCE_NODE",
        "SOUTH_SOURCE_NODE",
    ];

    private static readonly string[] RoadIds =
    [
        "west_source_service",
        "south_source_service",
        "east_city_spine",
        "waterworks_branch",
        "north_residential_branch",
        "east_residential_branch",
        "hospital_branch",
        "industrial_access",
        "south_city_spine",
    ];

    private static readonly HashSet<string> RoadStyles = new(StringComparer.Ordinal)
    {
        "source_service",
        "city_spine_primary",
        "city_spine_secondary",
        "residential_branch",
        "industrial_access",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    internal static RealtimeVisualLayoutDefinition LoadCanonical()
    {
#if DEBUG
        string sourcePath = Godot.ProjectSettings.GlobalizePath(ResourcePath);
        if (File.Exists(sourcePath))
        {
            return Parse(File.ReadAllText(sourcePath));
        }
#endif
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            EmbeddedResourceName) ?? throw new InvalidOperationException(
            "The embedded realtime visual layout is missing.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    internal static RealtimeVisualLayoutDefinition Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("The realtime visual layout is empty.");
        }
        RealtimeVisualLayoutDefinition definition;
        try
        {
            definition = JsonSerializer.Deserialize<RealtimeVisualLayoutDefinition>(
                json,
                JsonOptions) ?? throw new InvalidDataException(
                "The realtime visual layout root is null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The realtime visual layout JSON is invalid.",
                exception);
        }
        Validate(definition);
        return definition;
    }

    internal static string Serialize(RealtimeVisualLayoutDefinition definition)
    {
        Validate(definition);
        return JsonSerializer.Serialize(definition, JsonOptions) + "\n";
    }

#if DEBUG
    internal static void SaveCanonical(RealtimeVisualLayoutDefinition definition)
    {
        string path = Godot.ProjectSettings.GlobalizePath(ResourcePath);
        string temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, Serialize(definition));
        File.Move(temporaryPath, path, overwrite: true);
    }
#endif

    internal static void Validate(RealtimeVisualLayoutDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!string.Equals(definition.SchemaVersion, SchemaVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported realtime visual layout schema '{definition.SchemaVersion}'.");
        }
        ValidateExactIds(definition.Districts.Select(item => item.NodeId), DistrictIds,
            "district");
        ValidateExactIds(definition.Sources.Select(item => item.NodeId), SourceIds,
            "source");
        ValidateExactIds(definition.Roads.Select(item => item.RoadId), RoadIds, "road");

        foreach (RealtimeVisualDistrictLayout district in definition.Districts)
        {
            ValidatePoint(district.Center, $"district {district.NodeId} center");
            ValidatePoint(district.SpriteGround,
                $"district {district.NodeId} sprite ground");
            if (district.Footprint.X is < 100 or > 1200 ||
                district.Footprint.Y is < 100 or > 1200)
            {
                throw new InvalidDataException(
                    $"District {district.NodeId} footprint is outside 100–1200 units.");
            }
            ValidateMaxSide(district.WorldMaxSide, $"district {district.NodeId}");
        }
        foreach (RealtimeVisualSourceLayout source in definition.Sources)
        {
            ValidatePoint(source.SpriteGround, $"source {source.NodeId} sprite ground");
            ValidateMaxSide(source.WorldMaxSide, $"source {source.NodeId}");
        }
        foreach (RealtimeVisualRoadLayout road in definition.Roads)
        {
            if (!RoadStyles.Contains(road.Style))
            {
                throw new InvalidDataException(
                    $"Road {road.RoadId} has unknown style '{road.Style}'.");
            }
            if (road.Points.Length is < 2 or > 12)
            {
                throw new InvalidDataException(
                    $"Road {road.RoadId} requires two to twelve points.");
            }
            foreach ((RealtimeVisualLayoutPoint point, int index) in road.Points.Select(
                         (point, index) => (point, index)))
            {
                ValidatePoint(point, $"road {road.RoadId} point {index}");
            }
        }
    }

    private static void ValidateExactIds(
        IEnumerable<string> actual,
        IReadOnlyCollection<string> expected,
        string label)
    {
        string[] ids = actual.ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace) ||
            ids.Distinct(StringComparer.Ordinal).Count() != ids.Length ||
            !ids.ToHashSet(StringComparer.Ordinal).SetEquals(expected))
        {
            throw new InvalidDataException(
                $"The realtime visual layout {label} IDs are missing, duplicated, or unknown.");
        }
    }

    private static void ValidatePoint(RealtimeVisualLayoutPoint point, string label)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (point.X is < -200 or > 3400 || point.Y is < -200 or > 2300)
        {
            throw new InvalidDataException($"{label} is outside the editable world bounds.");
        }
    }

    private static void ValidateMaxSide(int value, string label)
    {
        if (value is < 200 or > 1400)
        {
            throw new InvalidDataException($"{label} size is outside 200–1400 units.");
        }
    }
}
