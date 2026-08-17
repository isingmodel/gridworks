using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Gridworks.Core.Product;

public static class ProductCampaignLoader
{
    public const string SupportedSchemaVersion = "gridworks.campaign.v2";
    public const string SupportedCampaignId = "GRIDWORKS_CAMPAIGN_V1";
    public const string SupportedScenarioFixture = "product-heatwave-v1.json";

    private static readonly string[] RootFields =
        ["schemaVersion", "campaignId", "displayName", "scenarioFixture", "chapters"];
    private static readonly string[] ChapterFields =
    [
        "chapterId",
        "displayName",
        "briefing",
        "objective",
        "minimumStartingCashUnit",
    ];
    private static readonly string[] ExpectedChapterIds =
        ["FIRST_LIGHT", "SECOND_HEART", "HEAT_DOME"];

    public static ProductCampaignDefinition Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Load(Encoding.UTF8.GetBytes(json));
    }

    public static ProductCampaignDefinition Load(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                utf8Json.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 12,
                });
            JsonElement root = document.RootElement;
            EnsureExactObject(root, RootFields, "$");

            List<ProductCampaignChapter> chapters = [];
            JsonElement chaptersElement = root.GetProperty("chapters");
            if (chaptersElement.ValueKind != JsonValueKind.Array)
            {
                throw new ProductCampaignValidationException("$.chapters must be an array.");
            }
            int index = 0;
            foreach (JsonElement chapterElement in chaptersElement.EnumerateArray())
            {
                EnsureExactObject(chapterElement, ChapterFields, $"$.chapters[{index}]");
                chapters.Add(new ProductCampaignChapter(
                    ReadString(chapterElement.GetProperty("chapterId"), $"$.chapters[{index}].chapterId"),
                    ReadString(chapterElement.GetProperty("displayName"), $"$.chapters[{index}].displayName"),
                    ReadString(chapterElement.GetProperty("briefing"), $"$.chapters[{index}].briefing"),
                    ReadString(chapterElement.GetProperty("objective"), $"$.chapters[{index}].objective"),
                    ReadInt64(
                        chapterElement.GetProperty("minimumStartingCashUnit"),
                        $"$.chapters[{index}].minimumStartingCashUnit")));
                index++;
            }

            ProductCampaignDefinition definition = new(
                ReadString(root.GetProperty("schemaVersion"), "$.schemaVersion"),
                ReadString(root.GetProperty("campaignId"), "$.campaignId"),
                ReadString(root.GetProperty("displayName"), "$.displayName"),
                ReadString(root.GetProperty("scenarioFixture"), "$.scenarioFixture"),
                Array.AsReadOnly(chapters.ToArray()));
            Validate(definition);
            return definition;
        }
        catch (ProductCampaignValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new ProductCampaignValidationException(
                "Campaign root JSON is invalid.",
                exception);
        }
    }

    internal static void Validate(ProductCampaignDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definition.Chapters);
        if (!string.Equals(
                definition.SchemaVersion,
                SupportedSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new ProductCampaignValidationException(
                $"Unsupported campaign schemaVersion '{definition.SchemaVersion}'.");
        }
        if (!string.Equals(
                definition.CampaignId,
                SupportedCampaignId,
                StringComparison.Ordinal))
        {
            throw new ProductCampaignValidationException(
                $"Unsupported campaignId '{definition.CampaignId}'.");
        }
        RequireNonBlank(definition.DisplayName, "displayName");
        if (!string.Equals(
                definition.ScenarioFixture,
                SupportedScenarioFixture,
                StringComparison.Ordinal) ||
            Path.IsPathRooted(definition.ScenarioFixture) ||
            !string.Equals(
                Path.GetFileName(definition.ScenarioFixture),
                definition.ScenarioFixture,
                StringComparison.Ordinal))
        {
            throw new ProductCampaignValidationException(
                $"Unsupported scenarioFixture '{definition.ScenarioFixture}'.");
        }
        if (definition.Chapters.Count != ExpectedChapterIds.Length)
        {
            throw new ProductCampaignValidationException(
                $"Campaign must define exactly {ExpectedChapterIds.Length} chapters.");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int index = 0; index < ExpectedChapterIds.Length; index++)
        {
            ProductCampaignChapter chapter = definition.Chapters[index]
                ?? throw new ProductCampaignValidationException(
                    $"chapters[{index}] cannot be null.");
            RequireNonBlank(chapter.ChapterId, $"chapters[{index}].chapterId");
            RequireNonBlank(chapter.DisplayName, $"chapters[{index}].displayName");
            RequireKoreanText(chapter.Briefing, $"chapters[{index}].briefing");
            RequireKoreanText(chapter.Objective, $"chapters[{index}].objective");
            if (index == 0 && chapter.MinimumStartingCashUnit != 0)
            {
                throw new ProductCampaignValidationException(
                    "chapters[0].minimumStartingCashUnit must be 0.");
            }
            if (index > 0 && chapter.MinimumStartingCashUnit <= 0)
            {
                throw new ProductCampaignValidationException(
                    $"chapters[{index}].minimumStartingCashUnit must be positive.");
            }
            if (!ids.Add(chapter.ChapterId))
            {
                throw new ProductCampaignValidationException(
                    $"Chapter ID '{chapter.ChapterId}' is duplicated.");
            }
            if (!string.Equals(
                    chapter.ChapterId,
                    ExpectedChapterIds[index],
                    StringComparison.Ordinal))
            {
                throw new ProductCampaignValidationException(
                    "Campaign chapter IDs must be FIRST_LIGHT, SECOND_HEART, HEAT_DOME in order.");
            }
        }
    }

    private static string ReadString(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new ProductCampaignValidationException($"{path} must be a string.");
        }
        return element.GetString()
            ?? throw new ProductCampaignValidationException($"{path} cannot be null.");
    }

    private static long ReadInt64(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number)
        {
            throw new ProductCampaignValidationException($"{path} must be an integer.");
        }
        string token = element.GetRawText();
        int start = token.Length > 0 && token[0] == '-' ? 1 : 0;
        if (start == token.Length ||
            token[start..].Any(character => character is < '0' or > '9') ||
            !long.TryParse(
                token,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out long value))
        {
            throw new ProductCampaignValidationException(
                $"{path} must be a 64-bit integer token.");
        }
        return value;
    }

    private static void RequireNonBlank(string value, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ProductCampaignValidationException($"{path} cannot be blank.");
        }
    }

    private static void RequireKoreanText(string value, string path)
    {
        RequireNonBlank(value, path);
        if (!value.Any(character => character is
                (>= '\u1100' and <= '\u11ff') or
                (>= '\u3130' and <= '\u318f') or
                (>= '\uac00' and <= '\ud7af')))
        {
            throw new ProductCampaignValidationException(
                $"{path} must contain Korean text.");
        }
    }

    private static void EnsureExactObject(
        JsonElement element,
        IReadOnlyCollection<string> expectedFields,
        string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ProductCampaignValidationException($"{path} must be an object.");
        }
        HashSet<string> remaining = new(expectedFields, StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!remaining.Remove(property.Name))
            {
                throw new ProductCampaignValidationException(
                    $"Unknown or duplicate property '{property.Name}' at {path}.");
            }
        }
        if (remaining.Count != 0)
        {
            throw new ProductCampaignValidationException(
                $"Missing property '{remaining.OrderBy(value => value, StringComparer.Ordinal).First()}' at {path}.");
        }
    }
}
