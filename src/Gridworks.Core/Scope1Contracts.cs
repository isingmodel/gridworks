using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Gridworks.Core;

public readonly record struct Scope1Point(int X, int Y);

public sealed record Scope1MapBounds(int MinX, int MaxX, int MinY, int MaxY)
{
    public bool Contains(Scope1Point point) =>
        point.X >= MinX && point.X <= MaxX && point.Y >= MinY && point.Y <= MaxY;
}

public sealed record Scope1Scenario(
    string SchemaVersion,
    string FixtureId,
    string PositionUnit,
    string TimeUnit,
    Scope1MapBounds MapBounds,
    Scope1Point Source,
    Scope1Point Target,
    int MaxSpan,
    int InitialMinute,
    int BuildMinutes);

public sealed record Scope1LoadedFixture(Scope1Scenario Scenario);

public enum Scope1Phase
{
    Drafting,
    Building,
    Commissioned,
}

public enum Scope1ErrorCode
{
    WrongPhase,
    InvalidPosition,
    SpanTooLong,
    NothingToUndo,
}

public sealed record Scope1View(
    int Minute,
    Scope1Phase Phase,
    IReadOnlyList<Scope1Point> SupportPositions,
    int? CompletionMinute)
{
    public bool TargetEnergized => Phase == Scope1Phase.Commissioned;
}

public sealed record Scope1CommandResult(
    bool Accepted,
    Scope1ErrorCode? ErrorCode,
    Scope1View View);

public sealed record Scope1PreviewResult(
    bool Accepted,
    Scope1ErrorCode? ErrorCode,
    Scope1Point From,
    Scope1Point To,
    long DistanceSquared,
    long MaxSpanSquared);

public static class Scope1ViewJson
{
    public static string Serialize(Scope1View view) =>
        Encoding.UTF8.GetString(SerializeToUtf8Bytes(view));

    public static byte[] SerializeToUtf8Bytes(Scope1View view)
    {
        ArgumentNullException.ThrowIfNull(view);

        using MemoryStream stream = new();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false,
        }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("minute", view.Minute);
            writer.WriteString("phase", PhaseValue(view.Phase));
            writer.WritePropertyName("supportPositions");
            writer.WriteStartArray();
            foreach (Scope1Point point in view.SupportPositions)
            {
                writer.WriteStartObject();
                writer.WriteNumber("x", point.X);
                writer.WriteNumber("y", point.Y);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            if (view.CompletionMinute.HasValue)
            {
                writer.WriteNumber("completionMinute", view.CompletionMinute.Value);
            }
            else
            {
                writer.WriteNull("completionMinute");
            }
            writer.WriteBoolean("targetEnergized", view.TargetEnergized);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static string Sha256Hex(Scope1View view) =>
        Convert.ToHexString(SHA256.HashData(SerializeToUtf8Bytes(view))).ToLowerInvariant();

    private static string PhaseValue(Scope1Phase phase) => phase switch
    {
        Scope1Phase.Drafting => "drafting",
        Scope1Phase.Building => "building",
        Scope1Phase.Commissioned => "commissioned",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };
}
