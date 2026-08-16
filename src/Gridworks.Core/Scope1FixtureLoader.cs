using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Core;

public static class Scope1FixtureLoader
{
    private const string SupportedSchemaVersion = "gridworks.scope1.fixture.v1";
    private const string SupportedFixtureId = "S1-FIXTURE-v1";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 16,
    };

    public static Scope1LoadedFixture Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Load(Encoding.UTF8.GetBytes(json));
    }

    public static Scope1LoadedFixture Load(ReadOnlySpan<byte> utf8Json)
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

            EnsureNoDuplicateProperties(document.RootElement, "$");
            RawScope1Fixture raw = document.RootElement.Deserialize<RawScope1Fixture>(SerializerOptions)
                ?? throw new FixtureValidationException("Scope 1 fixture root cannot be null.");

            Scope1Scenario scenario = MapScenario(raw);
            ValidateScenario(scenario);
            return new Scope1LoadedFixture(scenario);
        }
        catch (FixtureValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or OverflowException or
            ArgumentException or NullReferenceException)
        {
            throw new FixtureValidationException("Scope 1 fixture JSON is invalid.", exception);
        }
    }

    internal static void ValidateScenario(Scope1Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        if (!string.Equals(scenario.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new FixtureValidationException(
                $"Unsupported Scope 1 schemaVersion '{scenario.SchemaVersion}'.");
        }
        if (!string.Equals(scenario.FixtureId, SupportedFixtureId, StringComparison.Ordinal))
        {
            throw new FixtureValidationException(
                $"Unsupported Scope 1 fixtureId '{scenario.FixtureId}'.");
        }
        if (!string.Equals(scenario.PositionUnit, "GridUnit", StringComparison.Ordinal) ||
            !string.Equals(scenario.TimeUnit, "GameMinute", StringComparison.Ordinal))
        {
            throw new FixtureValidationException("Scope 1 fixture units are unsupported.");
        }

        Scope1MapBounds bounds = scenario.MapBounds
            ?? throw new FixtureValidationException("Scope 1 mapBounds cannot be null.");
        Require(bounds.MinX <= bounds.MaxX && bounds.MinY <= bounds.MaxY,
            "Scope 1 mapBounds must be ordered and inclusive.");
        Require(bounds.Contains(scenario.Source), "Scope 1 source must be inside mapBounds.");
        Require(bounds.Contains(scenario.Target), "Scope 1 target must be inside mapBounds.");
        Require(scenario.Source != scenario.Target, "Scope 1 source and target must be unique.");
        Require(scenario.MaxSpan > 0, "Scope 1 maxSpan must be positive.");
        Require(scenario.InitialMinute >= 0, "Scope 1 initialMinute cannot be negative.");
        Require(scenario.BuildMinutes > 0, "Scope 1 buildMinutes must be positive.");

        try
        {
            _ = checked(scenario.InitialMinute + scenario.BuildMinutes);
            long maxSpanSquared = checked((long)scenario.MaxSpan * scenario.MaxSpan);
            _ = DistanceSquared(
                new Scope1Point(bounds.MinX, bounds.MinY),
                new Scope1Point(bounds.MaxX, bounds.MaxY));
            long directDistanceSquared = DistanceSquared(scenario.Source, scenario.Target);
            Require(directDistanceSquared > maxSpanSquared,
                "Scope 1 direct source-to-target span must exceed maxSpan.");
        }
        catch (OverflowException exception)
        {
            throw new FixtureValidationException("Scope 1 fixture arithmetic overflowed.", exception);
        }
    }

    internal static long DistanceSquared(Scope1Point from, Scope1Point to)
    {
        long dx = checked((long)to.X - from.X);
        long dy = checked((long)to.Y - from.Y);
        return checked(checked(dx * dx) + checked(dy * dy));
    }

    private static Scope1Scenario MapScenario(RawScope1Fixture raw)
    {
        RawScope1Units units = raw.Units
            ?? throw new FixtureValidationException("Scope 1 units cannot be null.");
        RawScope1MapBounds bounds = raw.MapBounds
            ?? throw new FixtureValidationException("Scope 1 mapBounds cannot be null.");
        RawScope1Point source = raw.Source
            ?? throw new FixtureValidationException("Scope 1 source cannot be null.");
        RawScope1Point target = raw.Target
            ?? throw new FixtureValidationException("Scope 1 target cannot be null.");

        return new Scope1Scenario(
            raw.SchemaVersion,
            raw.FixtureId,
            units.Position,
            units.Time,
            new Scope1MapBounds(bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY),
            new Scope1Point(source.X, source.Y),
            new Scope1Point(target.X, target.Y),
            raw.MaxSpan,
            raw.InitialMinute,
            raw.BuildMinutes);
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new FixtureValidationException(message);
        }
    }

    private sealed class RawScope1Fixture
    {
        [JsonRequired] public string SchemaVersion { get; set; } = null!;
        [JsonRequired] public string FixtureId { get; set; } = null!;
        [JsonRequired] public RawScope1Units Units { get; set; } = null!;
        [JsonRequired] public RawScope1MapBounds MapBounds { get; set; } = null!;
        [JsonRequired] public RawScope1Point Source { get; set; } = null!;
        [JsonRequired] public RawScope1Point Target { get; set; } = null!;
        [JsonRequired] public int MaxSpan { get; set; }
        [JsonRequired] public int InitialMinute { get; set; }
        [JsonRequired] public int BuildMinutes { get; set; }
    }

    private sealed class RawScope1Units
    {
        [JsonRequired] public string Position { get; set; } = null!;
        [JsonRequired] public string Time { get; set; } = null!;
    }

    private sealed class RawScope1MapBounds
    {
        [JsonRequired] public int MinX { get; set; }
        [JsonRequired] public int MaxX { get; set; }
        [JsonRequired] public int MinY { get; set; }
        [JsonRequired] public int MaxY { get; set; }
    }

    private sealed class RawScope1Point
    {
        [JsonRequired] public int X { get; set; }
        [JsonRequired] public int Y { get; set; }
    }
}
