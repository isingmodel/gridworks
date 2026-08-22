using System.Text.Json;
using System.Text.Json.Serialization;
using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

public static class RealtimeWorldLoader
{
    public const string SupportedSchemaVersion = "gridworks.realtime.world.v3";

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

    public static RealtimeWorldDefinition Load(
        string json,
        CommercialWorldDefinition baseWorld)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Load(System.Text.Encoding.UTF8.GetBytes(json), baseWorld);
    }

    public static RealtimeWorldDefinition Load(
        ReadOnlySpan<byte> utf8Json,
        CommercialWorldDefinition baseWorld)
    {
        ArgumentNullException.ThrowIfNull(baseWorld);
        try
        {
            byte[] bytes = utf8Json.ToArray();
            using JsonDocument document = JsonDocument.Parse(bytes);
            RejectDuplicates(document.RootElement, "$");
            RequireRootShape(document.RootElement);
            RawWorld raw = JsonSerializer.Deserialize<RawWorld>(bytes, Options)
                ?? throw new RealtimeWorldValidationException(
                    "The realtime world document is empty.");
            RealtimeWorldDefinition result = Convert(raw, baseWorld);
            Validate(result, baseWorld);
            return result;
        }
        catch (RealtimeWorldValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or InvalidOperationException or
            NullReferenceException or OverflowException or CommercialWorldValidationException)
        {
            throw new RealtimeWorldValidationException(
                "The realtime world document is invalid.",
                exception);
        }
    }

    public static void Validate(
        RealtimeWorldDefinition definition,
        CommercialWorldDefinition baseWorld)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(baseWorld);
        CommercialWorldLoader.Validate(baseWorld);
        Require(definition.SchemaVersion == SupportedSchemaVersion,
            $"schemaVersion must equal '{SupportedSchemaVersion}'.");
        Require(definition.WorldId == baseWorld.WorldId,
            "worldId must match the base commercial world.");
        CommercialWorldLoader.Validate(definition.Network);
        Require(definition.Network.WorldId == baseWorld.WorldId,
            "The adjusted network must retain the base world ID.");

        var expected = new Dictionary<(ThermalAssetKind Kind, string ClassId), ThermalLimit>();
        foreach (CommercialNodeClassDefinition item in baseWorld.NodeClasses.Where(item =>
                     item.ThermalLimit is not null))
        {
            expected.Add((ThermalAssetKind.Node, item.ClassId), item.ThermalLimit!);
        }
        foreach (CommercialLineClassDefinition item in baseWorld.LineClasses)
        {
            expected.Add((ThermalAssetKind.Edge, item.ClassId), item.ThermalLimit);
        }

        var seen = new HashSet<(ThermalAssetKind Kind, string ClassId)>();
        string? previous = null;
        foreach (RealtimeThermalClassDefinition item in definition.ThermalClasses)
        {
            Require(Enum.IsDefined(item.AssetKind), "thermalClasses has an unknown assetKind.");
            RequireText(item.ClassId, "thermalClasses.classId");
            var key = (item.AssetKind, item.ClassId);
            Require(expected.ContainsKey(key),
                $"thermalClasses references unknown thermal class '{item.AssetKind}:{item.ClassId}'.");
            Require(seen.Add(key),
                $"thermalClasses duplicates '{item.AssetKind}:{item.ClassId}'.");
            string orderKey = $"{(int)item.AssetKind:D2}:{item.ClassId}";
            Require(previous is null || string.CompareOrdinal(previous, orderKey) < 0,
                "thermalClasses must use asset-kind then ordinal class order.");
            previous = orderKey;
            ThermalProtectionDefinition protection = item.Protection ??
                throw new RealtimeWorldValidationException(
                    $"thermalClasses '{item.ClassId}' needs protection values.");
            Require(protection.ContinuousKw > 0 &&
                    protection.EmergencyKw >= protection.ContinuousKw,
                $"thermalClasses '{item.ClassId}' needs 0 < continuous <= emergency.");
            Require(protection.EmergencyExposureLimitMinutes > 0,
                $"thermalClasses '{item.ClassId}' needs positive exposure duration.");
            Require(protection.EmergencyExposureRecoveryPerMinute > 0,
                $"thermalClasses '{item.ClassId}' needs positive exposure recovery rate.");
            Require(protection.ProtectiveOutageMinutes > 0,
                $"thermalClasses '{item.ClassId}' needs positive outage duration.");
            ThermalLimit adjustedLimit = item.AssetKind switch
            {
                ThermalAssetKind.Node => definition.Network.NodeClasses.Single(nodeClass =>
                    string.Equals(nodeClass.ClassId, item.ClassId, StringComparison.Ordinal))
                    .ThermalLimit!,
                ThermalAssetKind.Edge => definition.Network.LineClasses.Single(lineClass =>
                    string.Equals(lineClass.ClassId, item.ClassId, StringComparison.Ordinal))
                    .ThermalLimit,
                _ => throw new RealtimeWorldValidationException(
                    "thermalClasses has an unknown asset kind."),
            };
            Require(adjustedLimit.ContinuousKw == protection.ContinuousKw &&
                    adjustedLimit.EmergencyKw == protection.EmergencyKw,
                $"thermalClasses '{item.ClassId}' must match the adjusted network limits.");
        }
        Require(seen.SetEquals(expected.Keys),
            "thermalClasses must define every and only pole, substation, and line class.");
    }

    private static RealtimeWorldDefinition Convert(
        RawWorld raw,
        CommercialWorldDefinition baseWorld)
    {
        RealtimeThermalClassDefinition[] classes = raw.ThermalClasses.Select(item =>
            new RealtimeThermalClassDefinition(
                item.AssetKind,
                item.ClassId,
                new ThermalProtectionDefinition(
                    item.ContinuousKw,
                    item.EmergencyKw,
                    item.EmergencyExposureLimitMinutes,
                    item.EmergencyExposureRecoveryPerMinute,
                    item.ProtectiveOutageMinutes))).ToArray();
        var requiredKeys = new HashSet<(ThermalAssetKind Kind, string ClassId)>(
            baseWorld.NodeClasses
                .Where(item => item.ThermalLimit is not null)
                .Select(item => (ThermalAssetKind.Node, item.ClassId))
                .Concat(baseWorld.LineClasses.Select(item =>
                    (ThermalAssetKind.Edge, item.ClassId))));
        var suppliedKeys = new HashSet<(ThermalAssetKind Kind, string ClassId)>(
            classes.Select(item => (item.AssetKind, item.ClassId)));
        Require(suppliedKeys.SetEquals(requiredKeys),
            "thermalClasses must define every and only pole, substation, and line class.");
        Dictionary<(ThermalAssetKind Kind, string ClassId), ThermalProtectionDefinition> values =
            classes.ToDictionary(item => (item.AssetKind, item.ClassId), item => item.Protection);
        CommercialWorldDefinition adjusted = baseWorld with
        {
            NodeClasses = baseWorld.NodeClasses.Select(item => item.ThermalLimit is null
                ? item
                : item with
                {
                    ThermalLimit = Limit(values[(ThermalAssetKind.Node, item.ClassId)]),
                }).ToArray(),
            LineClasses = baseWorld.LineClasses.Select(item => item with
            {
                ThermalLimit = Limit(values[(ThermalAssetKind.Edge, item.ClassId)]),
            }).ToArray(),
        };
        return new RealtimeWorldDefinition(
            raw.SchemaVersion,
            raw.WorldId,
            adjusted,
            classes);
    }

    private static ThermalLimit Limit(ThermalProtectionDefinition definition) =>
        new(definition.ContinuousKw, definition.EmergencyKw);

    private static void RequireRootShape(JsonElement root)
    {
        Require(root.ValueKind == JsonValueKind.Object,
            "The realtime world root must be an object.");
        string[] expected = ["schemaVersion", "worldId", "thermalClasses"];
        HashSet<string> actual = root.EnumerateObject()
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals(expected),
            "The realtime world root fields do not match world v3.");
        Require(root.GetProperty("thermalClasses").ValueKind == JsonValueKind.Array,
            "thermalClasses must be an array.");
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
                    throw new RealtimeWorldValidationException(
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
        Require(!string.IsNullOrWhiteSpace(value) && value == value.Trim(),
            $"{path} must be nonblank and trimmed.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new RealtimeWorldValidationException(message);
        }
    }

    private sealed class RawWorld
    {
        [JsonRequired] public string SchemaVersion { get; init; } = null!;
        [JsonRequired] public string WorldId { get; init; } = null!;
        [JsonRequired] public RawThermalClass[] ThermalClasses { get; init; } = null!;
    }

    private sealed class RawThermalClass
    {
        [JsonRequired] public ThermalAssetKind AssetKind { get; init; }
        [JsonRequired] public string ClassId { get; init; } = null!;
        [JsonRequired] public long ContinuousKw { get; init; }
        [JsonRequired] public long EmergencyKw { get; init; }
        [JsonRequired] public int EmergencyExposureLimitMinutes { get; init; }
        [JsonRequired] public int EmergencyExposureRecoveryPerMinute { get; init; }
        [JsonRequired] public int ProtectiveOutageMinutes { get; init; }
    }
}
