using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

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
        "res://realtime/r2/world/RealtimeVisualLayoutAuthoring.tscn";

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
        WriteIndented = true,
    };

    internal static RealtimeVisualLayoutDefinition LoadCanonical()
    {
        PackedScene scene = GD.Load<PackedScene>(ResourcePath) ??
            throw new InvalidDataException(
                $"The realtime visual authoring scene is missing: {ResourcePath}");
        Node root = scene.Instantiate();
        try
        {
            return Project(root);
        }
        finally
        {
            root.Free();
        }
    }

    internal static RealtimeVisualLayoutDefinition Project(Node root)
    {
        ArgumentNullException.ThrowIfNull(root);
        string schemaVersion = RequiredStringMeta(root, "schema_version");
        Node districtsRoot = RequiredChild(root, "Districts");
        Node sourcesRoot = RequiredChild(root, "Sources");
        Node roadsRoot = RequiredChild(root, "Roads");

        RealtimeVisualDistrictLayout[] districts = districtsRoot.GetChildren()
            .Select(child => child is Sprite2D sprite
                ? ProjectDistrict(sprite)
                : throw new InvalidDataException(
                    $"District '{child.Name}' must be a Sprite2D."))
            .ToArray();
        RealtimeVisualSourceLayout[] sources = sourcesRoot.GetChildren()
            .Select(child => child is Sprite2D sprite
                ? ProjectSource(sprite)
                : throw new InvalidDataException(
                    $"Source '{child.Name}' must be a Sprite2D."))
            .ToArray();
        RealtimeVisualRoadLayout[] roads = roadsRoot.GetChildren()
            .Select(child => child is Line2D line
                ? ProjectRoad(line)
                : throw new InvalidDataException(
                    $"Road '{child.Name}' must be a Line2D."))
            .ToArray();

        var definition = new RealtimeVisualLayoutDefinition
        {
            SchemaVersion = schemaVersion,
            Districts = districts,
            Sources = sources,
            Roads = roads,
        };
        Validate(definition);
        return definition;
    }

    internal static string Serialize(RealtimeVisualLayoutDefinition definition)
    {
        Validate(definition);
        return JsonSerializer.Serialize(definition, JsonOptions) + "\n";
    }

    private static RealtimeVisualDistrictLayout ProjectDistrict(Sprite2D sprite)
    {
        RealtimeVisualLayoutPoint ground = ProjectPoint(sprite.Position);
        Vector2 centerOffset = RequiredVector2Meta(sprite, "center_offset");
        Vector2 footprint = RequiredVector2Meta(sprite, "footprint");
        return new RealtimeVisualDistrictLayout
        {
            NodeId = sprite.Name.ToString(),
            Center = ProjectPoint(sprite.Position + centerOffset),
            SpriteGround = ground,
            Footprint = ProjectPoint(footprint),
            WorldMaxSide = ProjectWorldMaxSide(sprite),
        };
    }

    private static RealtimeVisualSourceLayout ProjectSource(Sprite2D sprite) => new()
    {
        NodeId = sprite.Name.ToString(),
        SpriteGround = ProjectPoint(sprite.Position),
        WorldMaxSide = ProjectWorldMaxSide(sprite),
    };

    private static RealtimeVisualRoadLayout ProjectRoad(Line2D line) => new()
    {
        RoadId = line.Name.ToString(),
        Style = RequiredStringMeta(line, "style"),
        Points = line.Points.Select(ProjectPoint).ToArray(),
    };

    private static int ProjectWorldMaxSide(Sprite2D sprite)
    {
        if (sprite.Texture is null)
        {
            throw new InvalidDataException($"Sprite '{sprite.Name}' has no texture.");
        }
        if (sprite.Scale.X <= 0f || sprite.Scale.Y <= 0f ||
            !Mathf.IsEqualApprox(sprite.Scale.X, sprite.Scale.Y))
        {
            throw new InvalidDataException(
                $"Sprite '{sprite.Name}' requires positive uniform scale.");
        }
        return Mathf.RoundToInt(Math.Max(
            sprite.Texture.GetWidth(),
            sprite.Texture.GetHeight()) * sprite.Scale.X);
    }

    private static RealtimeVisualLayoutPoint ProjectPoint(Vector2 point) => new()
    {
        X = Mathf.RoundToInt(point.X),
        Y = Mathf.RoundToInt(point.Y),
    };

    private static Node RequiredChild(Node root, string name) =>
        root.GetNodeOrNull<Node>(name) ?? throw new InvalidDataException(
            $"The visual authoring scene is missing '{name}'.");

    private static string RequiredStringMeta(Node node, string key)
    {
        if (!node.HasMeta(key))
        {
            throw new InvalidDataException(
                $"Node '{node.Name}' is missing metadata '{key}'.");
        }
        string value = node.GetMeta(key).AsString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Node '{node.Name}' metadata '{key}' is empty.");
        }
        return value;
    }

    private static Vector2 RequiredVector2Meta(Node node, string key)
    {
        if (!node.HasMeta(key))
        {
            throw new InvalidDataException(
                $"Node '{node.Name}' is missing metadata '{key}'.");
        }
        Variant value = node.GetMeta(key);
        if (value.VariantType != Variant.Type.Vector2)
        {
            throw new InvalidDataException(
                $"Node '{node.Name}' metadata '{key}' must be Vector2.");
        }
        return value.AsVector2();
    }

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
