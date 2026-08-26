using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Gridworks.Game.Realtime.R2;

internal enum RealtimeProductWindowMode
{
    Windowed,
    Fullscreen,
}

internal sealed record RealtimeProductSettings(
    string SchemaVersion,
    RealtimeProductWindowMode WindowMode,
    int UiScalePercent,
    int MasterVolumePercent,
    int AmbientVolumePercent,
    int SfxVolumePercent,
    bool ReduceMotion)
{
    internal const string SupportedSchemaVersion = "gridworks.realtime-settings.v1";

    internal static RealtimeProductSettings Default { get; } = new(
        SupportedSchemaVersion,
        RealtimeProductWindowMode.Windowed,
        100,
        100,
        100,
        100,
        false);
}

internal enum RealtimeProductSettingsLoadStatus
{
    Missing,
    Loaded,
    Invalid,
    Unsupported,
    ReadFailure,
}

internal sealed record RealtimeProductSettingsLoadResult(
    RealtimeProductSettingsLoadStatus Status,
    RealtimeProductSettings Settings,
    string? Message);

internal enum RealtimeProductSettingsSaveStatus
{
    Saved,
    Invalid,
    WriteFailure,
}

internal sealed record RealtimeProductSettingsSaveResult(
    RealtimeProductSettingsSaveStatus Status,
    string? Message);

internal enum RealtimeProductSettingsPersistenceFailureKind
{
    Invalid,
    Unsupported,
}

internal sealed class RealtimeProductSettingsPersistenceException : Exception
{
    internal RealtimeProductSettingsPersistenceException(
        RealtimeProductSettingsPersistenceFailureKind kind,
        string message)
        : base(message)
    {
        Kind = kind;
    }

    internal RealtimeProductSettingsPersistenceException(
        RealtimeProductSettingsPersistenceFailureKind kind,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }

    internal RealtimeProductSettingsPersistenceFailureKind Kind { get; }
}

/// <summary>
/// Owns the strict, current-R2 settings document. Historical settings types are
/// deliberately not accepted or imported here.
/// </summary>
internal static class RealtimeProductSettingsCodec
{
    private static readonly string[] Fields =
    [
        "schemaVersion",
        "windowMode",
        "uiScalePercent",
        "masterVolumePercent",
        "ambientVolumePercent",
        "sfxVolumePercent",
        "reduceMotion",
    ];

    internal static byte[] Serialize(RealtimeProductSettings settings)
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
                settings.WindowMode == RealtimeProductWindowMode.Windowed
                    ? "windowed"
                    : "fullscreen");
            writer.WriteNumber("uiScalePercent", settings.UiScalePercent);
            writer.WriteNumber("masterVolumePercent", settings.MasterVolumePercent);
            writer.WriteNumber("ambientVolumePercent", settings.AmbientVolumePercent);
            writer.WriteNumber("sfxVolumePercent", settings.SfxVolumePercent);
            writer.WriteBoolean("reduceMotion", settings.ReduceMotion);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    internal static RealtimeProductSettings Deserialize(ReadOnlySpan<byte> utf8Json)
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
            if (!string.Equals(
                    schemaVersion,
                    RealtimeProductSettings.SupportedSchemaVersion,
                    StringComparison.Ordinal))
            {
                throw new RealtimeProductSettingsPersistenceException(
                    RealtimeProductSettingsPersistenceFailureKind.Unsupported,
                    $"Unsupported realtime settings schemaVersion '{schemaVersion}'.");
            }

            EnsureExactObject(root, Fields, "$");
            RealtimeProductSettings settings = new(
                schemaVersion,
                ReadWindowMode(root.GetProperty("windowMode")),
                ReadInt32(root.GetProperty("uiScalePercent"), "$.uiScalePercent"),
                ReadInt32(
                    root.GetProperty("masterVolumePercent"),
                    "$.masterVolumePercent"),
                ReadInt32(
                    root.GetProperty("ambientVolumePercent"),
                    "$.ambientVolumePercent"),
                ReadInt32(
                    root.GetProperty("sfxVolumePercent"),
                    "$.sfxVolumePercent"),
                ReadBoolean(root.GetProperty("reduceMotion"), "$.reduceMotion"));
            Validate(settings);
            return settings;
        }
        catch (RealtimeProductSettingsPersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or
            KeyNotFoundException or OverflowException)
        {
            throw new RealtimeProductSettingsPersistenceException(
                RealtimeProductSettingsPersistenceFailureKind.Invalid,
                "Realtime settings JSON is invalid.",
                exception);
        }
    }

    internal static void Validate(RealtimeProductSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Require(
            string.Equals(
                settings.SchemaVersion,
                RealtimeProductSettings.SupportedSchemaVersion,
                StringComparison.Ordinal),
            $"Unsupported realtime settings schemaVersion '{settings.SchemaVersion}'.");
        Require(
            Enum.IsDefined(settings.WindowMode),
            $"Unsupported windowMode '{settings.WindowMode}'.");
        Require(
            settings.UiScalePercent is 100 or 125 or 150 or 200,
            "uiScalePercent must be 100, 125, 150, or 200.");
        ValidateVolume(settings.MasterVolumePercent, "masterVolumePercent");
        ValidateVolume(settings.AmbientVolumePercent, "ambientVolumePercent");
        ValidateVolume(settings.SfxVolumePercent, "sfxVolumePercent");
    }

    private static string ReadSchemaVersion(JsonElement root)
    {
        JsonProperty[] matches = root.EnumerateObject()
            .Where(property => string.Equals(
                property.Name,
                "schemaVersion",
                StringComparison.Ordinal))
            .ToArray();
        Require(
            matches.Length == 1,
            matches.Length == 0
                ? "Missing property 'schemaVersion' at $."
                : "Unknown or duplicate property 'schemaVersion' at $.");
        return ReadString(matches[0].Value, "$.schemaVersion");
    }

    private static RealtimeProductWindowMode ReadWindowMode(JsonElement element)
    {
        string value = ReadString(element, "$.windowMode");
        return value switch
        {
            "windowed" => RealtimeProductWindowMode.Windowed,
            "fullscreen" => RealtimeProductWindowMode.Fullscreen,
            _ => throw new RealtimeProductSettingsPersistenceException(
                RealtimeProductSettingsPersistenceFailureKind.Invalid,
                $"Unsupported windowMode '{value}'."),
        };
    }

    private static void EnsureExactObject(
        JsonElement element,
        IReadOnlyCollection<string> expectedFields,
        string path)
    {
        Require(element.ValueKind == JsonValueKind.Object, $"{path} must be an object.");
        HashSet<string> remaining = new(expectedFields, StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            Require(
                remaining.Remove(property.Name),
                $"Unknown or duplicate property '{property.Name}' at {path}.");
        }
        Require(
            remaining.Count == 0,
            remaining.Count == 0
                ? string.Empty
                : $"Missing property '{remaining.OrderBy(value => value, StringComparer.Ordinal).First()}' at {path}.");
    }

    private static bool ReadBoolean(JsonElement element, string path)
    {
        Require(
            element.ValueKind is JsonValueKind.True or JsonValueKind.False,
            $"{path} must be a boolean.");
        return element.GetBoolean();
    }

    private static string ReadString(JsonElement element, string path)
    {
        Require(element.ValueKind == JsonValueKind.String, $"{path} must be a string.");
        return element.GetString() ??
            throw new RealtimeProductSettingsPersistenceException(
                RealtimeProductSettingsPersistenceFailureKind.Invalid,
                $"{path} cannot be null.");
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
            throw new RealtimeProductSettingsPersistenceException(
                RealtimeProductSettingsPersistenceFailureKind.Invalid,
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
            throw new RealtimeProductSettingsPersistenceException(
                RealtimeProductSettingsPersistenceFailureKind.Invalid,
                message);
        }
    }
}

/// <summary>
/// Loads without mutating bytes and commits settings through one same-directory
/// temporary file so a failed write leaves the primary settings file unchanged.
/// </summary>
internal static class RealtimeProductSettingsStore
{
    internal const string FileName = "realtime-settings-v1.json";

    internal static RealtimeProductSettingsLoadResult Load(string absolutePath)
    {
        ValidatePath(absolutePath);
        try
        {
            RealtimeProductSettings settings = RealtimeProductSettingsCodec.Deserialize(
                File.ReadAllBytes(absolutePath));
            return new RealtimeProductSettingsLoadResult(
                RealtimeProductSettingsLoadStatus.Loaded,
                settings,
                null);
        }
        catch (RealtimeProductSettingsPersistenceException exception)
        {
            return new RealtimeProductSettingsLoadResult(
                exception.Kind == RealtimeProductSettingsPersistenceFailureKind.Unsupported
                    ? RealtimeProductSettingsLoadStatus.Unsupported
                    : RealtimeProductSettingsLoadStatus.Invalid,
                RealtimeProductSettings.Default,
                exception.Message);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new RealtimeProductSettingsLoadResult(
                RealtimeProductSettingsLoadStatus.Missing,
                RealtimeProductSettings.Default,
                null);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return new RealtimeProductSettingsLoadResult(
                RealtimeProductSettingsLoadStatus.ReadFailure,
                RealtimeProductSettings.Default,
                exception.Message);
        }
    }

    internal static RealtimeProductSettingsSaveResult Save(
        string absolutePath,
        RealtimeProductSettings settings)
    {
        ValidatePath(absolutePath);
        byte[] bytes;
        try
        {
            bytes = RealtimeProductSettingsCodec.Serialize(settings);
        }
        catch (RealtimeProductSettingsPersistenceException exception)
        {
            return new RealtimeProductSettingsSaveResult(
                RealtimeProductSettingsSaveStatus.Invalid,
                exception.Message);
        }

        string directory = Path.GetDirectoryName(absolutePath)!;
        string temporaryPath = $"{absolutePath}.tmp";
        try
        {
            Directory.CreateDirectory(directory);
            using (FileStream stream = new(
                       temporaryPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, absolutePath, overwrite: true);
            return new RealtimeProductSettingsSaveResult(
                RealtimeProductSettingsSaveStatus.Saved,
                null);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return new RealtimeProductSettingsSaveResult(
                RealtimeProductSettingsSaveStatus.WriteFailure,
                exception.Message);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // The primary outcome is already decided. A private temp cleanup
                // failure must not overwrite the typed save result.
            }
        }
    }

    private static void ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path) || Path.GetDirectoryName(path) is null)
        {
            throw new ArgumentException(
                "The realtime settings path must be an absolute file path.",
                nameof(path));
        }
    }
}
