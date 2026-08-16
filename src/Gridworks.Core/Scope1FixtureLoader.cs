using System.Text;
using System.Text.Json;

namespace Gridworks.Core;

public static class Scope1FixtureLoader
{
    private const string SupportedSchemaVersion = "1";
    private const string SupportedFixtureId = "scope-1-v1";
    private const string SupportedPositionUnit = "GridUnit";
    private const string SupportedTimeUnit = "GameMinute";

    private static readonly string[] RootFields =
    [
        "schemaVersion",
        "fixtureId",
        "units",
        "mapBounds",
        "source",
        "target",
        "maxSpan",
        "initialMinute",
        "buildMinutes",
    ];

    private static readonly string[] UnitFields = ["position", "time"];
    private static readonly string[] BoundsFields = ["minX", "maxX", "minY", "maxY"];
    private static readonly string[] PointFields = ["x", "y"];

    public static Scope1Fixture Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Load(Encoding.UTF8.GetBytes(json));
    }

    public static Scope1Fixture Load(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                utf8Json.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 16,
                });

            JsonElement root = document.RootElement;
            EnsureNoDuplicateProperties(root, "$");
            EnsureExactObject(root, RootFields, "$");

            Scope1Fixture fixture = new(
                ReadString(root.GetProperty("schemaVersion"), "$.schemaVersion"),
                ReadString(root.GetProperty("fixtureId"), "$.fixtureId"),
                ReadUnits(root.GetProperty("units"), "$.units"),
                ReadBounds(root.GetProperty("mapBounds"), "$.mapBounds"),
                ReadPoint(root.GetProperty("source"), "$.source"),
                ReadPoint(root.GetProperty("target"), "$.target"),
                ReadInteger(root.GetProperty("maxSpan"), "$.maxSpan"),
                ReadInteger(root.GetProperty("initialMinute"), "$.initialMinute"),
                ReadInteger(root.GetProperty("buildMinutes"), "$.buildMinutes"));

            Validate(fixture);
            return fixture;
        }
        catch (FixtureValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or OverflowException or
            InvalidOperationException or KeyNotFoundException)
        {
            throw new FixtureValidationException("Scope 1 fixture JSON is invalid.", exception);
        }
    }

    internal static void Validate(Scope1Fixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(fixture.Units);
        ArgumentNullException.ThrowIfNull(fixture.MapBounds);
        ArgumentNullException.ThrowIfNull(fixture.Source);
        ArgumentNullException.ThrowIfNull(fixture.Target);

        if (!string.Equals(fixture.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new FixtureValidationException(
                $"Unsupported Scope 1 schemaVersion '{fixture.SchemaVersion}'.");
        }

        if (!string.Equals(fixture.FixtureId, SupportedFixtureId, StringComparison.Ordinal))
        {
            throw new FixtureValidationException(
                $"Unsupported Scope 1 fixtureId '{fixture.FixtureId}'.");
        }

        if (!string.Equals(fixture.Units.Position, SupportedPositionUnit, StringComparison.Ordinal) ||
            !string.Equals(fixture.Units.Time, SupportedTimeUnit, StringComparison.Ordinal))
        {
            throw new FixtureValidationException("Scope 1 fixture units are unsupported.");
        }

        Scope1MapBounds bounds = fixture.MapBounds;
        if (bounds.MinX > bounds.MaxX || bounds.MinY > bounds.MaxY)
        {
            throw new FixtureValidationException("Scope 1 map bounds are reversed.");
        }

        if (!Contains(bounds, fixture.Source) || !Contains(bounds, fixture.Target))
        {
            throw new FixtureValidationException("Scope 1 endpoints must be inside map bounds.");
        }

        if (fixture.Source == fixture.Target)
        {
            throw new FixtureValidationException("Scope 1 source and target must be unique.");
        }

        if (fixture.MaxSpan <= 0)
        {
            throw new FixtureValidationException("Scope 1 maxSpan must be positive.");
        }

        if (fixture.InitialMinute < 0 || fixture.BuildMinutes <= 0)
        {
            throw new FixtureValidationException(
                "Scope 1 initialMinute must be non-negative and buildMinutes must be positive.");
        }

        try
        {
            _ = checked(fixture.InitialMinute + fixture.BuildMinutes);

            long width = (long)bounds.MaxX - bounds.MinX;
            long height = (long)bounds.MaxY - bounds.MinY;
            _ = checked(checked(width * width) + checked(height * height));

            long directDistanceSquared = DistanceSquared(fixture.Source, fixture.Target);
            long maxSpanSquared = checked((long)fixture.MaxSpan * fixture.MaxSpan);
            if (directDistanceSquared <= maxSpanSquared)
            {
                throw new FixtureValidationException(
                    "Scope 1 source-to-target span must exceed maxSpan.");
            }
        }
        catch (OverflowException exception)
        {
            throw new FixtureValidationException(
                "Scope 1 fixture arithmetic exceeds the supported integer range.", exception);
        }
    }

    internal static bool Contains(Scope1MapBounds bounds, Scope1Point point) =>
        point.X >= bounds.MinX && point.X <= bounds.MaxX &&
        point.Y >= bounds.MinY && point.Y <= bounds.MaxY;

    internal static long DistanceSquared(Scope1Point from, Scope1Point to)
    {
        long dx = (long)to.X - from.X;
        long dy = (long)to.Y - from.Y;
        return checked(checked(dx * dx) + checked(dy * dy));
    }

    private static Scope1Units ReadUnits(JsonElement element, string path)
    {
        EnsureExactObject(element, UnitFields, path);
        return new Scope1Units(
            ReadString(element.GetProperty("position"), $"{path}.position"),
            ReadString(element.GetProperty("time"), $"{path}.time"));
    }

    private static Scope1MapBounds ReadBounds(JsonElement element, string path)
    {
        EnsureExactObject(element, BoundsFields, path);
        return new Scope1MapBounds(
            ReadInteger(element.GetProperty("minX"), $"{path}.minX"),
            ReadInteger(element.GetProperty("maxX"), $"{path}.maxX"),
            ReadInteger(element.GetProperty("minY"), $"{path}.minY"),
            ReadInteger(element.GetProperty("maxY"), $"{path}.maxY"));
    }

    private static Scope1Point ReadPoint(JsonElement element, string path)
    {
        EnsureExactObject(element, PointFields, path);
        return new Scope1Point(
            ReadInteger(element.GetProperty("x"), $"{path}.x"),
            ReadInteger(element.GetProperty("y"), $"{path}.y"));
    }

    private static string ReadString(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new FixtureValidationException($"{path} must be a string.");
        }

        return element.GetString()
            ?? throw new FixtureValidationException($"{path} cannot be null.");
    }

    private static int ReadInteger(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number ||
            !IsIntegerToken(element.GetRawText()) ||
            !element.TryGetInt32(out int value))
        {
            throw new FixtureValidationException($"{path} must be a 32-bit integer.");
        }

        return value;
    }

    private static bool IsIntegerToken(string token)
    {
        int index = token.Length > 0 && token[0] == '-' ? 1 : 0;
        if (index == token.Length)
        {
            return false;
        }

        for (; index < token.Length; index++)
        {
            if (token[index] is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static void EnsureExactObject(
        JsonElement element,
        IReadOnlyCollection<string> expectedFields,
        string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FixtureValidationException($"{path} must be an object.");
        }

        HashSet<string> remaining = new(expectedFields, StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!remaining.Remove(property.Name))
            {
                throw new FixtureValidationException(
                    $"Unknown or duplicate property '{property.Name}' at {path}.");
            }
        }

        if (remaining.Count != 0)
        {
            throw new FixtureValidationException(
                $"Missing property '{remaining.OrderBy(value => value, StringComparer.Ordinal).First()}' at {path}.");
        }
    }

    private static void EnsureNoDuplicateProperties(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new FixtureValidationException(
                        $"Duplicate JSON property '{property.Name}' at {path}.");
                }

                EnsureNoDuplicateProperties(property.Value, $"{path}.{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                EnsureNoDuplicateProperties(item, $"{path}[{index}]");
                index++;
            }
        }
    }
}
