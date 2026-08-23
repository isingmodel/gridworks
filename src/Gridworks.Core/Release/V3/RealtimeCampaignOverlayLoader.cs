using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

public sealed record RealtimeCampaignOverlaySourceIdentity(
    string BaseCampaignSha256,
    string RealtimeOverlaySha256,
    string FullComposedCampaignSha256,
    string SelectedComposedCampaignSha256,
    int FullChapterCount,
    int SelectedChapterCount);

public sealed record RealtimeCampaignOverlayLoadResult(
    RealtimeCampaignDefinition Campaign,
    RealtimeCampaignOverlaySourceIdentity SourceIdentity);

public static class RealtimeCampaignOverlayLoader
{
    public const string FirstReleaseChapterId = "FIRST_LIGHT";
    public const string FirstReleaseEventId = "FIRST_LIGHT_SUPPLY";

    private static readonly string[] OverlayRootFields =
        ["schemaVersion", "campaignId", "chapters"];

    private static readonly string[] OverlayChapterFields =
    [
        "chapterId",
        "preparationMinutes",
        "promiseDecisionDeadlineOffsetMinutes",
        "scheduledEvents",
    ];

    private static readonly string[] OverlayEventFields =
    [
        "eventId",
        "priority",
        "startOffsetMinutes",
        "durationMinutes",
        "forecastLeadMinutes",
    ];

    private static readonly string[] AuthoredPhaseFields =
    [
        "displayName",
        "thermalPolicy",
        "loads",
        "unavailableNodeIds",
        "unavailableEdgeIds",
        "activeRiskAreaIds",
        "thermalLimitOverrides",
    ];

    public static RealtimeCampaignOverlayLoadResult LoadAll(
        ReadOnlySpan<byte> baseCampaignUtf8Json,
        ReadOnlySpan<byte> realtimeOverlayUtf8Json,
        RealtimeWorldDefinition world) => LoadPrefix(
            baseCampaignUtf8Json,
            realtimeOverlayUtf8Json,
            world,
            CommercialCampaignLoader.CanonicalChapterIds.Count);

    public static RealtimeCampaignOverlayLoadResult LoadFirstLight(
        ReadOnlySpan<byte> baseCampaignUtf8Json,
        ReadOnlySpan<byte> realtimeOverlayUtf8Json,
        RealtimeWorldDefinition world)
    {
        RealtimeCampaignOverlayLoadResult result = LoadPrefix(
            baseCampaignUtf8Json,
            realtimeOverlayUtf8Json,
            world,
            chapterCount: 1);
        RealtimeChapterDefinition chapter = result.Campaign.Chapters.Single();
        Require(
            chapter.Content.ChapterId == FirstReleaseChapterId &&
            chapter.ScheduledEvents.Count == 1 &&
            chapter.ScheduledEvents[0].EventId == FirstReleaseEventId,
            "The first release prefix is not FIRST_LIGHT/FIRST_LIGHT_SUPPLY.");
        return result;
    }

    public static RealtimeCampaignOverlayLoadResult LoadPrefix(
        ReadOnlySpan<byte> baseCampaignUtf8Json,
        ReadOnlySpan<byte> realtimeOverlayUtf8Json,
        RealtimeWorldDefinition world,
        int chapterCount)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (chapterCount < 1 ||
            chapterCount > CommercialCampaignLoader.CanonicalChapterIds.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chapterCount),
                chapterCount,
                "A realtime release prefix must contain between one and eight chapters.");
        }

        byte[] baseCampaignBytes = baseCampaignUtf8Json.ToArray();
        byte[] realtimeOverlayBytes = realtimeOverlayUtf8Json.ToArray();
        try
        {
            CommercialCampaignDefinition baseCampaign = CommercialCampaignLoader.Load(
                baseCampaignBytes,
                world.Network);
            JsonObject baseRoot = ParseObject(baseCampaignBytes, "Release V2 campaign");
            JsonObject fullRoot = ParseObject(
                realtimeOverlayBytes,
                "Release V3 campaign overlay");
            ComposeAndValidateOverlayShape(fullRoot, baseRoot, baseCampaign);

            byte[] fullComposedBytes = Serialize(fullRoot);
            RealtimeCampaignDefinition fullCampaign = RealtimeCampaignLoader.Load(
                fullComposedBytes,
                baseCampaign,
                world);
            Require(
                fullCampaign.Chapters.Count ==
                    CommercialCampaignLoader.CanonicalChapterIds.Count,
                "The release overlay did not validate as the complete eight-chapter campaign.");

            byte[] selectedComposedBytes;
            RealtimeCampaignDefinition selectedCampaign;
            if (chapterCount == fullCampaign.Chapters.Count)
            {
                selectedComposedBytes = fullComposedBytes;
                selectedCampaign = fullCampaign;
            }
            else
            {
                JsonObject selectedRoot = fullRoot.DeepClone().AsObject();
                JsonArray selectedChapters = Array(
                    selectedRoot,
                    "chapters",
                    "Release V3 campaign overlay");
                while (selectedChapters.Count > chapterCount)
                {
                    selectedChapters.RemoveAt(selectedChapters.Count - 1);
                }
                selectedComposedBytes = Serialize(selectedRoot);
                selectedCampaign = RealtimeCampaignLoader.Load(
                    selectedComposedBytes,
                    baseCampaign,
                    world);
            }

            return new RealtimeCampaignOverlayLoadResult(
                selectedCampaign,
                new RealtimeCampaignOverlaySourceIdentity(
                    Sha256(baseCampaignBytes),
                    Sha256(realtimeOverlayBytes),
                    Sha256(fullComposedBytes),
                    Sha256(selectedComposedBytes),
                    fullCampaign.Chapters.Count,
                    selectedCampaign.Chapters.Count));
        }
        catch (RealtimeCampaignOverlayValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or InvalidOperationException or
            ArgumentException or OverflowException or CommercialCampaignValidationException or
            RealtimeCampaignValidationException)
        {
            throw new RealtimeCampaignOverlayValidationException(
                "The release V2/V3 campaign overlay sources are invalid.",
                exception);
        }
    }

    private static void ComposeAndValidateOverlayShape(
        JsonObject realtimeRoot,
        JsonObject baseRoot,
        CommercialCampaignDefinition baseCampaign)
    {
        RequireExactKeys(
            realtimeRoot,
            OverlayRootFields,
            "Release V3 campaign overlay");
        Require(
            String(realtimeRoot, "schemaVersion", "Release V3 campaign overlay") ==
                RealtimeCampaignLoader.SupportedSchemaVersion,
            $"Release V3 campaign overlay schemaVersion must equal " +
            $"'{RealtimeCampaignLoader.SupportedSchemaVersion}'.");
        Require(
            String(realtimeRoot, "campaignId", "Release V3 campaign overlay") ==
                baseCampaign.CampaignId,
            "Release V3 campaign overlay campaignId must match Release V2.");

        JsonArray chapters = Array(
            realtimeRoot,
            "chapters",
            "Release V3 campaign overlay");
        Require(
            chapters.Count == CommercialCampaignLoader.CanonicalChapterIds.Count,
            "Release V3 campaign overlay must contain all eight canonical chapters.");

        IReadOnlyDictionary<string, JsonObject> baseChapters = Array(
                baseRoot,
                "chapters",
                "Release V2 campaign")
            .Select((item, index) => Object(
                item,
                $"Release V2 campaign chapters[{index}]"))
            .ToDictionary(
                chapter => String(
                    chapter,
                    "chapterId",
                    "Release V2 campaign chapter"),
                StringComparer.Ordinal);

        for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
        {
            JsonObject chapter = Object(
                chapters[chapterIndex],
                $"Release V3 campaign overlay chapters[{chapterIndex}]");
            string chapterLabel =
                $"Release V3 campaign overlay chapters[{chapterIndex}]";
            RequireExactKeys(chapter, OverlayChapterFields, chapterLabel);
            string chapterId = String(chapter, "chapterId", chapterLabel);
            Require(
                chapterId == CommercialCampaignLoader.CanonicalChapterIds[chapterIndex],
                $"{chapterLabel}.chapterId must be the canonical chapter at this index.");
            Require(
                baseChapters.TryGetValue(chapterId, out JsonObject? baseChapter),
                $"{chapterLabel} references unknown Release V2 chapter '{chapterId}'.");

            IReadOnlyDictionary<string, JsonObject> phases = Array(
                    baseChapter!,
                    "operatingPhases",
                    $"Release V2 chapter '{chapterId}'")
                .Select((item, phaseIndex) => Object(
                    item,
                    $"Release V2 chapter '{chapterId}' operatingPhases[{phaseIndex}]"))
                .ToDictionary(
                    phase => String(
                        phase,
                        "phaseId",
                        $"Release V2 chapter '{chapterId}' phase"),
                    StringComparer.Ordinal);

            JsonArray scheduledEvents = Array(
                chapter,
                "scheduledEvents",
                chapterLabel);
            var seenEventIds = new HashSet<string>(StringComparer.Ordinal);
            for (int eventIndex = 0; eventIndex < scheduledEvents.Count; eventIndex++)
            {
                JsonObject scheduledEvent = Object(
                    scheduledEvents[eventIndex],
                    $"{chapterLabel}.scheduledEvents[{eventIndex}]");
                string eventLabel =
                    $"{chapterLabel}.scheduledEvents[{eventIndex}]";
                RequireExactKeys(scheduledEvent, OverlayEventFields, eventLabel);
                string eventId = String(scheduledEvent, "eventId", eventLabel);
                Require(
                    seenEventIds.Add(eventId),
                    $"{eventLabel}.eventId '{eventId}' is duplicated.");
                Require(
                    phases.TryGetValue(eventId, out JsonObject? phase),
                    $"{eventLabel}.eventId '{eventId}' has no authored V2 operating phase.");
                foreach (string field in AuthoredPhaseFields)
                {
                    scheduledEvent[field] = phase![field]?.DeepClone() ??
                        throw new RealtimeCampaignOverlayValidationException(
                            $"Release V2 phase '{eventId}' field '{field}' is missing.");
                }
            }
            Require(
                seenEventIds.SetEquals(phases.Keys),
                $"{chapterLabel}.scheduledEvents must cover every authored V2 phase exactly once.");
        }

        realtimeRoot["initialSeed"] = baseRoot["initialSeed"]?.DeepClone() ??
            throw new RealtimeCampaignOverlayValidationException(
                "Release V2 campaign initialSeed is missing.");
    }

    private static JsonObject ParseObject(byte[] bytes, string label)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        RejectDuplicates(document.RootElement, "$", label);
        JsonNode? node = JsonNode.Parse(bytes);
        return node is JsonObject value
            ? value
            : throw new RealtimeCampaignOverlayValidationException(
                $"{label} must be a JSON object.");
    }

    private static void RejectDuplicates(JsonElement element, string path, string label)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new RealtimeCampaignOverlayValidationException(
                        $"{label} {path}.{property.Name} is duplicated.");
                }
                RejectDuplicates(property.Value, $"{path}.{property.Name}", label);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicates(item, $"{path}[{index++}]", label);
            }
        }
    }

    private static void RequireExactKeys(
        JsonObject value,
        IEnumerable<string> expected,
        string label)
    {
        if (!value.Select(item => item.Key).ToHashSet(StringComparer.Ordinal)
                .SetEquals(expected))
        {
            throw new RealtimeCampaignOverlayValidationException(
                $"{label} fields drifted.");
        }
    }

    private static JsonObject Object(JsonNode? value, string label) =>
        value is JsonObject result
            ? result
            : throw new RealtimeCampaignOverlayValidationException(
                $"{label} must be an object.");

    private static JsonArray Array(JsonObject value, string name, string label) =>
        value[name] is JsonArray result
            ? result
            : throw new RealtimeCampaignOverlayValidationException(
                $"{label}.{name} must be an array.");

    private static string String(JsonObject value, string name, string label) =>
        value[name] is JsonValue item && item.TryGetValue(out string? result) &&
        result is not null
            ? result
            : throw new RealtimeCampaignOverlayValidationException(
                $"{label}.{name} must be a string.");

    private static byte[] Serialize(JsonObject value) =>
        Encoding.UTF8.GetBytes(value.ToJsonString());

    private static string Sha256(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new RealtimeCampaignOverlayValidationException(message);
        }
    }
}

public sealed class RealtimeCampaignOverlayValidationException : Exception
{
    public RealtimeCampaignOverlayValidationException(string message)
        : base(message)
    {
    }

    public RealtimeCampaignOverlayValidationException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
