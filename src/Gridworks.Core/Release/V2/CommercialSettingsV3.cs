using System.Globalization;
using System.Text.Json;

namespace Gridworks.Core.Release.V2;

public sealed record CommercialSettings(
    string SchemaVersion,
    bool Fullscreen,
    int UiScalePercent,
    int MasterVolumePercent,
    int AmbientVolumePercent,
    int SfxVolumePercent,
    bool ReduceMotion)
{
    public const string SupportedSchemaVersion = "gridworks.settings.v3";

    public static CommercialSettings Default { get; } = new(
        SupportedSchemaVersion,
        false,
        100,
        100,
        100,
        100,
        false);
}

public enum CommercialSettingsDocumentKind
{
    Version3,
    ImportedVersion2,
}

public sealed record CommercialSettingsDecodeResult(
    CommercialSettingsDocumentKind Kind,
    CommercialSettings Settings);

public enum CommercialSettingsLoadStatus
{
    Missing,
    Loaded,
    MigratedFromVersion2,
    Invalid,
    MigrationWriteFailed,
}

public enum CommercialSettingsLoadError
{
    InvalidDocument,
    ReadFailed,
    MigrationWriteFailed,
}

public sealed record CommercialSettingsLoadResult(
    CommercialSettingsLoadStatus Status,
    CommercialSettings Settings,
    CommercialSettingsLoadError? Error,
    string? ErrorMessage);

public enum CommercialSettingsWriteStatus
{
    Saved,
    Failed,
}

public enum CommercialSettingsWriteError
{
    InvalidSettings,
    WriteFailed,
}

public sealed record CommercialSettingsWriteResult(
    CommercialSettingsWriteStatus Status,
    CommercialSettingsWriteError? Error,
    string? ErrorMessage);

public sealed class CommercialSettingsValidationException : Exception
{
    public CommercialSettingsValidationException(string message)
        : base(message)
    {
    }

    public CommercialSettingsValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public static class CommercialSettingsCodec
{
    public const string SupportedSchemaVersion = CommercialSettings.SupportedSchemaVersion;
    public const string LegacySchemaVersion = "gridworks.settings.v2";

    private static readonly string[] Version3Fields =
    [
        "schemaVersion",
        "fullscreen",
        "uiScalePercent",
        "masterVolumePercent",
        "ambientVolumePercent",
        "sfxVolumePercent",
        "reduceMotion",
    ];

    private static readonly string[] Version2Fields =
    [
        "schemaVersion",
        "windowMode",
        "uiScalePercent",
        "showControlHelp",
        "masterVolumePercent",
        "ambientVolumePercent",
        "sfxVolumePercent",
    ];

    public static byte[] Serialize(CommercialSettings settings)
    {
        Validate(settings);
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(
            stream,
            new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", settings.SchemaVersion);
            writer.WriteBoolean("fullscreen", settings.Fullscreen);
            writer.WriteNumber("uiScalePercent", settings.UiScalePercent);
            writer.WriteNumber("masterVolumePercent", settings.MasterVolumePercent);
            writer.WriteNumber("ambientVolumePercent", settings.AmbientVolumePercent);
            writer.WriteNumber("sfxVolumePercent", settings.SfxVolumePercent);
            writer.WriteBoolean("reduceMotion", settings.ReduceMotion);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    public static CommercialSettings DeserializeV3(ReadOnlySpan<byte> utf8Json)
    {
        CommercialSettingsDecodeResult result = Decode(utf8Json);
        if (result.Kind != CommercialSettingsDocumentKind.Version3)
        {
            throw new CommercialSettingsValidationException(
                "A legacy settings document cannot be deserialized as v3.");
        }
        return result.Settings;
    }

    public static CommercialSettings ImportV2(ReadOnlySpan<byte> utf8Json)
    {
        CommercialSettingsDecodeResult result = Decode(utf8Json);
        if (result.Kind != CommercialSettingsDocumentKind.ImportedVersion2)
        {
            throw new CommercialSettingsValidationException(
                "A v3 settings document cannot be imported as v2.");
        }
        return result.Settings;
    }

    public static CommercialSettingsDecodeResult Decode(ReadOnlySpan<byte> utf8Json)
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
            Require(root.ValueKind == JsonValueKind.Object, "$ must be an object.");
            string schemaVersion = ReadSchemaVersion(root);
            return schemaVersion switch
            {
                SupportedSchemaVersion => DecodeV3(root),
                LegacySchemaVersion => DecodeV2(root),
                _ => throw new CommercialSettingsValidationException(
                    $"Unsupported settings schemaVersion '{schemaVersion}'."),
            };
        }
        catch (CommercialSettingsValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or KeyNotFoundException or
            OverflowException)
        {
            throw new CommercialSettingsValidationException(
                "Settings JSON is invalid.",
                exception);
        }
    }

    public static void Validate(CommercialSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Require(
            string.Equals(
                settings.SchemaVersion,
                SupportedSchemaVersion,
                StringComparison.Ordinal),
            $"Unsupported settings schemaVersion '{settings.SchemaVersion}'.");
        Require(settings.UiScalePercent is 100 or 125,
            "uiScalePercent must be 100 or 125.");
        ValidateVolume(settings.MasterVolumePercent, "masterVolumePercent");
        ValidateVolume(settings.AmbientVolumePercent, "ambientVolumePercent");
        ValidateVolume(settings.SfxVolumePercent, "sfxVolumePercent");
    }

    private static CommercialSettingsDecodeResult DecodeV3(JsonElement root)
    {
        EnsureExactObject(root, Version3Fields, "$");
        CommercialSettings settings = new(
            SupportedSchemaVersion,
            ReadBoolean(root.GetProperty("fullscreen"), "$.fullscreen"),
            ReadInt32(root.GetProperty("uiScalePercent"), "$.uiScalePercent"),
            ReadInt32(root.GetProperty("masterVolumePercent"), "$.masterVolumePercent"),
            ReadInt32(root.GetProperty("ambientVolumePercent"), "$.ambientVolumePercent"),
            ReadInt32(root.GetProperty("sfxVolumePercent"), "$.sfxVolumePercent"),
            ReadBoolean(root.GetProperty("reduceMotion"), "$.reduceMotion"));
        Validate(settings);
        return new CommercialSettingsDecodeResult(
            CommercialSettingsDocumentKind.Version3,
            settings);
    }

    private static CommercialSettingsDecodeResult DecodeV2(JsonElement root)
    {
        EnsureExactObject(root, Version2Fields, "$");
        string windowMode = ReadString(root.GetProperty("windowMode"), "$.windowMode");
        bool fullscreen = windowMode switch
        {
            "windowed" => false,
            "fullscreen" => true,
            _ => throw new CommercialSettingsValidationException(
                $"Unsupported windowMode '{windowMode}'."),
        };
        _ = ReadBoolean(root.GetProperty("showControlHelp"), "$.showControlHelp");
        CommercialSettings settings = new(
            SupportedSchemaVersion,
            fullscreen,
            ReadInt32(root.GetProperty("uiScalePercent"), "$.uiScalePercent"),
            ReadInt32(root.GetProperty("masterVolumePercent"), "$.masterVolumePercent"),
            ReadInt32(root.GetProperty("ambientVolumePercent"), "$.ambientVolumePercent"),
            ReadInt32(root.GetProperty("sfxVolumePercent"), "$.sfxVolumePercent"),
            false);
        Validate(settings);
        return new CommercialSettingsDecodeResult(
            CommercialSettingsDocumentKind.ImportedVersion2,
            settings);
    }

    private static string ReadSchemaVersion(JsonElement root)
    {
        JsonProperty[] matches = root.EnumerateObject()
            .Where(property => string.Equals(
                property.Name,
                "schemaVersion",
                StringComparison.Ordinal))
            .ToArray();
        Require(matches.Length == 1, matches.Length == 0
            ? "Missing property 'schemaVersion' at $."
            : "Unknown or duplicate property 'schemaVersion' at $.");
        return ReadString(matches[0].Value, "$.schemaVersion");
    }

    private static void EnsureExactObject(
        JsonElement element,
        IReadOnlyCollection<string> expectedFields,
        string path)
    {
        Require(element.ValueKind == JsonValueKind.Object, $"{path} must be an object.");
        var remaining = new HashSet<string>(expectedFields, StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            Require(remaining.Remove(property.Name),
                $"Unknown or duplicate property '{property.Name}' at {path}.");
        }
        Require(remaining.Count == 0,
            remaining.Count == 0
                ? string.Empty
                : $"Missing property '{remaining.OrderBy(value => value, StringComparer.Ordinal).First()}' at {path}.");
    }

    private static bool ReadBoolean(JsonElement element, string path)
    {
        Require(element.ValueKind is JsonValueKind.True or JsonValueKind.False,
            $"{path} must be a boolean.");
        return element.GetBoolean();
    }

    private static string ReadString(JsonElement element, string path)
    {
        Require(element.ValueKind == JsonValueKind.String, $"{path} must be a string.");
        return element.GetString() ??
            throw new CommercialSettingsValidationException($"{path} cannot be null.");
    }

    private static int ReadInt32(JsonElement element, string path)
    {
        Require(element.ValueKind == JsonValueKind.Number, $"{path} must be an integer.");
        string token = element.GetRawText();
        Require(
            token.Length > 0 &&
            token.All(character => character is >= '0' and <= '9'),
            $"{path} must be a non-negative integer token.");
        if (!int.TryParse(
                token,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int value))
        {
            throw new CommercialSettingsValidationException(
                $"{path} must fit in a 32-bit integer.");
        }
        return value;
    }

    private static void ValidateVolume(int value, string propertyName) =>
        Require(
            value is 0 or 25 or 50 or 75 or 100,
            $"{propertyName} must be 0, 25, 50, 75, or 100.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new CommercialSettingsValidationException(message);
        }
    }
}

public static class CommercialSettingsStore
{
    public const string SettingsFileName = "settings.json";

    public static CommercialSettingsLoadResult Load(string absolutePath)
    {
        ValidateAbsolutePath(absolutePath);
        if (!File.Exists(absolutePath))
        {
            return new CommercialSettingsLoadResult(
                CommercialSettingsLoadStatus.Missing,
                CommercialSettings.Default,
                null,
                null);
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(absolutePath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return new CommercialSettingsLoadResult(
                CommercialSettingsLoadStatus.Invalid,
                CommercialSettings.Default,
                CommercialSettingsLoadError.ReadFailed,
                exception.Message);
        }

        CommercialSettingsDecodeResult decoded;
        try
        {
            decoded = CommercialSettingsCodec.Decode(bytes);
        }
        catch (CommercialSettingsValidationException exception)
        {
            return new CommercialSettingsLoadResult(
                CommercialSettingsLoadStatus.Invalid,
                CommercialSettings.Default,
                CommercialSettingsLoadError.InvalidDocument,
                exception.Message);
        }

        if (decoded.Kind == CommercialSettingsDocumentKind.Version3)
        {
            return new CommercialSettingsLoadResult(
                CommercialSettingsLoadStatus.Loaded,
                decoded.Settings,
                null,
                null);
        }

        CommercialSettingsWriteResult migration = Save(absolutePath, decoded.Settings);
        if (migration.Status != CommercialSettingsWriteStatus.Saved)
        {
            return new CommercialSettingsLoadResult(
                CommercialSettingsLoadStatus.MigrationWriteFailed,
                decoded.Settings,
                CommercialSettingsLoadError.MigrationWriteFailed,
                migration.ErrorMessage);
        }
        return new CommercialSettingsLoadResult(
            CommercialSettingsLoadStatus.MigratedFromVersion2,
            decoded.Settings,
            null,
            null);
    }

    public static CommercialSettingsWriteResult Save(
        string absolutePath,
        CommercialSettings settings)
    {
        ValidateAbsolutePath(absolutePath);
        byte[] bytes;
        try
        {
            bytes = CommercialSettingsCodec.Serialize(settings);
        }
        catch (CommercialSettingsValidationException exception)
        {
            return new CommercialSettingsWriteResult(
                CommercialSettingsWriteStatus.Failed,
                CommercialSettingsWriteError.InvalidSettings,
                exception.Message);
        }

        try
        {
            CommercialSettingsAtomicFile.Write(absolutePath, bytes);
            return new CommercialSettingsWriteResult(
                CommercialSettingsWriteStatus.Saved,
                null,
                null);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return new CommercialSettingsWriteResult(
                CommercialSettingsWriteStatus.Failed,
                CommercialSettingsWriteError.WriteFailed,
                exception.Message);
        }
    }

    private static void ValidateAbsolutePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path) || Path.GetDirectoryName(path) is null)
        {
            throw new ArgumentException("Settings path must be absolute.", nameof(path));
        }
    }
}

internal static class CommercialSettingsAtomicFile
{
    internal static void Write(string absolutePath, ReadOnlySpan<byte> content)
    {
        string directory = Path.GetDirectoryName(absolutePath) ??
            throw new ArgumentException(
                "Settings path must include a directory.",
                nameof(absolutePath));
        Directory.CreateDirectory(directory);
        string temporaryPath = string.Concat(absolutePath, ".tmp");
        try
        {
            using (FileStream stream = new(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, absolutePath, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception cleanupException) when (
                cleanupException is IOException or UnauthorizedAccessException)
            {
            }
            throw;
        }
    }
}
