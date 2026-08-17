using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Gridworks.Core.Product;

public static class ProductContentHash
{
    public static string ComputeSha256(ReadOnlySpan<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}

public static class ProductCampaignSaveCodec
{
    public const string SupportedSchemaVersion = ProductCampaignSave.SupportedSchemaVersion;

    private const int MaximumCommandCount = 10_000;
    private static readonly string[] RootFields =
    [
        "schemaVersion",
        "campaignId",
        "campaignRootSha256",
        "fixtureId",
        "fixtureSha256",
        "commands",
    ];
    private static readonly string[] CommandFields = ["kind"];
    private static readonly string[] PositionedCommandFields = ["kind", "position"];
    private static readonly string[] PointFields = ["x", "y"];

    public static byte[] Serialize(ProductCampaignSave save)
    {
        Validate(save);
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(
            stream,
            new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", save.SchemaVersion);
            writer.WriteString("campaignId", save.CampaignId);
            writer.WriteString("campaignRootSha256", save.CampaignRootSha256);
            writer.WriteString("fixtureId", save.FixtureId);
            writer.WriteString("fixtureSha256", save.FixtureSha256);
            writer.WriteStartArray("commands");
            foreach (ProductCampaignCommand command in save.Commands)
            {
                writer.WriteStartObject();
                writer.WriteString("kind", command.Kind.ToString());
                if (command.Position is ProductPoint position)
                {
                    writer.WriteStartObject("position");
                    writer.WriteNumber("x", position.X);
                    writer.WriteNumber("y", position.Y);
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    public static ProductCampaignSave Deserialize(ReadOnlySpan<byte> utf8Json)
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
            EnsureExactObject(root, RootFields, "$");
            JsonElement commandsElement = root.GetProperty("commands");
            if (commandsElement.ValueKind != JsonValueKind.Array)
            {
                throw new ProductPersistenceValidationException("$.commands must be an array.");
            }

            List<ProductCampaignCommand> commands = [];
            int index = 0;
            foreach (JsonElement commandElement in commandsElement.EnumerateArray())
            {
                if (index >= MaximumCommandCount)
                {
                    throw new ProductPersistenceValidationException(
                        $"Campaign save cannot contain more than {MaximumCommandCount} commands.");
                }
                ProductCampaignCommandKind kind = ReadCommandKind(
                    commandElement,
                    $"$.commands[{index}]");
                bool requiresPosition = RequiresPosition(kind);
                EnsureExactObject(
                    commandElement,
                    requiresPosition ? PositionedCommandFields : CommandFields,
                    $"$.commands[{index}]");
                ProductPoint? position = requiresPosition
                    ? ReadPoint(
                        commandElement.GetProperty("position"),
                        $"$.commands[{index}].position")
                    : null;
                commands.Add(new ProductCampaignCommand(kind, position));
                index++;
            }

            ProductCampaignSave save = new(
                ReadString(root.GetProperty("schemaVersion"), "$.schemaVersion"),
                ReadString(root.GetProperty("campaignId"), "$.campaignId"),
                ReadString(root.GetProperty("campaignRootSha256"), "$.campaignRootSha256"),
                ReadString(root.GetProperty("fixtureId"), "$.fixtureId"),
                ReadString(root.GetProperty("fixtureSha256"), "$.fixtureSha256"),
                Array.AsReadOnly(commands.ToArray()));
            Validate(save);
            return save;
        }
        catch (ProductPersistenceValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new ProductPersistenceValidationException(
                "Campaign save JSON is invalid.",
                exception);
        }
    }

    internal static void Validate(ProductCampaignSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(save.Commands);
        if (!string.Equals(
                save.SchemaVersion,
                SupportedSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new ProductPersistenceValidationException(
                $"Unsupported campaign save schemaVersion '{save.SchemaVersion}'.");
        }
        RequireNonBlank(save.CampaignId, "campaignId");
        RequireNonBlank(save.FixtureId, "fixtureId");
        ValidateSha256(save.CampaignRootSha256, "campaignRootSha256");
        ValidateSha256(save.FixtureSha256, "fixtureSha256");
        if (save.Commands.Count > MaximumCommandCount)
        {
            throw new ProductPersistenceValidationException(
                $"Campaign save cannot contain more than {MaximumCommandCount} commands.");
        }
        for (int index = 0; index < save.Commands.Count; index++)
        {
            ProductCampaignCommand command = save.Commands[index]
                ?? throw new ProductPersistenceValidationException(
                    $"commands[{index}] cannot be null.");
            if (!Enum.IsDefined(command.Kind))
            {
                throw new ProductPersistenceValidationException(
                    $"commands[{index}].kind is unsupported.");
            }
            if (RequiresPosition(command.Kind) != (command.Position is not null))
            {
                throw new ProductPersistenceValidationException(
                    $"commands[{index}] has an invalid position field.");
            }
        }
    }

    private static ProductCampaignCommandKind ReadCommandKind(
        JsonElement commandElement,
        string path)
    {
        if (commandElement.ValueKind != JsonValueKind.Object ||
            !commandElement.TryGetProperty("kind", out JsonElement kindElement))
        {
            throw new ProductPersistenceValidationException($"{path}.kind is required.");
        }
        string name = ReadString(kindElement, $"{path}.kind");
        if (!Enum.TryParse(name, false, out ProductCampaignCommandKind kind) ||
            !Enum.IsDefined(kind))
        {
            throw new ProductPersistenceValidationException(
                $"Unsupported campaign command kind '{name}'.");
        }
        return kind;
    }

    private static bool RequiresPosition(ProductCampaignCommandKind kind) => kind is
        ProductCampaignCommandKind.SetSubstationDraft or
        ProductCampaignCommandKind.AddLineSupport or
        ProductCampaignCommandKind.SetPlantDraft;

    private static ProductPoint ReadPoint(JsonElement element, string path)
    {
        EnsureExactObject(element, PointFields, path);
        return new ProductPoint(
            ReadInt32(element.GetProperty("x"), $"{path}.x"),
            ReadInt32(element.GetProperty("y"), $"{path}.y"));
    }

    private static int ReadInt32(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number)
        {
            throw new ProductPersistenceValidationException($"{path} must be an integer.");
        }
        string token = element.GetRawText();
        int start = token.Length > 0 && token[0] == '-' ? 1 : 0;
        if (start == token.Length || token[start..].Any(character => character is < '0' or > '9') ||
            !int.TryParse(
                token,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out int value))
        {
            throw new ProductPersistenceValidationException(
                $"{path} must be a 32-bit integer token.");
        }
        return value;
    }

    private static string ReadString(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new ProductPersistenceValidationException($"{path} must be a string.");
        }
        return element.GetString()
            ?? throw new ProductPersistenceValidationException($"{path} cannot be null.");
    }

    private static void RequireNonBlank(string value, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ProductPersistenceValidationException($"{path} cannot be blank.");
        }
    }

    private static void ValidateSha256(string value, string path)
    {
        if (value is null || value.Length != 64 || value.Any(character =>
                character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f')))
        {
            throw new ProductPersistenceValidationException(
                $"{path} must be 64 lowercase hexadecimal characters.");
        }
    }

    private static void EnsureExactObject(
        JsonElement element,
        IReadOnlyCollection<string> expectedFields,
        string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ProductPersistenceValidationException($"{path} must be an object.");
        }
        HashSet<string> remaining = new(expectedFields, StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!remaining.Remove(property.Name))
            {
                throw new ProductPersistenceValidationException(
                    $"Unknown or duplicate property '{property.Name}' at {path}.");
            }
        }
        if (remaining.Count != 0)
        {
            throw new ProductPersistenceValidationException(
                $"Missing property '{remaining.OrderBy(value => value, StringComparer.Ordinal).First()}' at {path}.");
        }
    }
}

public static class ProductSettingsCodec
{
    public const string SupportedSchemaVersion = ProductSettings.SupportedSchemaVersion;

    private static readonly string[] RootFields =
        ["schemaVersion", "windowMode", "uiScalePercent", "showControlHelp"];

    public static byte[] Serialize(ProductSettings settings)
    {
        Validate(settings);
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(
            stream,
            new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", settings.SchemaVersion);
            writer.WriteString(
                "windowMode",
                settings.WindowMode == ProductWindowMode.Windowed
                    ? "windowed"
                    : "fullscreen");
            writer.WriteNumber("uiScalePercent", settings.UiScalePercent);
            writer.WriteBoolean("showControlHelp", settings.ShowControlHelp);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    public static ProductSettings Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                utf8Json.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 8,
                });
            JsonElement root = document.RootElement;
            EnsureExactObject(root, RootFields, "$");
            string windowModeName = ReadString(
                root.GetProperty("windowMode"),
                "$.windowMode");
            ProductWindowMode windowMode = windowModeName switch
            {
                "windowed" => ProductWindowMode.Windowed,
                "fullscreen" => ProductWindowMode.Fullscreen,
                _ => throw new ProductPersistenceValidationException(
                    $"Unsupported windowMode '{windowModeName}'."),
            };
            JsonElement helpElement = root.GetProperty("showControlHelp");
            if (helpElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new ProductPersistenceValidationException(
                    "$.showControlHelp must be a boolean.");
            }
            ProductSettings settings = new(
                ReadString(root.GetProperty("schemaVersion"), "$.schemaVersion"),
                windowMode,
                ReadInt32(root.GetProperty("uiScalePercent"), "$.uiScalePercent"),
                helpElement.GetBoolean());
            Validate(settings);
            return settings;
        }
        catch (ProductPersistenceValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new ProductPersistenceValidationException(
                "Settings JSON is invalid.",
                exception);
        }
    }

    internal static void Validate(ProductSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!string.Equals(
                settings.SchemaVersion,
                SupportedSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new ProductPersistenceValidationException(
                $"Unsupported settings schemaVersion '{settings.SchemaVersion}'.");
        }
        if (!Enum.IsDefined(settings.WindowMode))
        {
            throw new ProductPersistenceValidationException("windowMode is unsupported.");
        }
        if (settings.UiScalePercent is not (100 or 125))
        {
            throw new ProductPersistenceValidationException(
                "uiScalePercent must be 100 or 125.");
        }
    }

    private static int ReadInt32(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number)
        {
            throw new ProductPersistenceValidationException($"{path} must be an integer.");
        }
        string token = element.GetRawText();
        if (token.Length == 0 || token.Any(character => character is < '0' or > '9') ||
            !int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
        {
            throw new ProductPersistenceValidationException($"{path} must be an integer token.");
        }
        return value;
    }

    private static string ReadString(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new ProductPersistenceValidationException($"{path} must be a string.");
        }
        return element.GetString()
            ?? throw new ProductPersistenceValidationException($"{path} cannot be null.");
    }

    private static void EnsureExactObject(
        JsonElement element,
        IReadOnlyCollection<string> expectedFields,
        string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ProductPersistenceValidationException($"{path} must be an object.");
        }
        HashSet<string> remaining = new(expectedFields, StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!remaining.Remove(property.Name))
            {
                throw new ProductPersistenceValidationException(
                    $"Unknown or duplicate property '{property.Name}' at {path}.");
            }
        }
        if (remaining.Count != 0)
        {
            throw new ProductPersistenceValidationException(
                $"Missing property '{remaining.OrderBy(value => value, StringComparer.Ordinal).First()}' at {path}.");
        }
    }
}
