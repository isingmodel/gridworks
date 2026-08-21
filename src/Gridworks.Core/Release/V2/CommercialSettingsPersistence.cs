using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Core.Release.V2;

public enum CommercialWindowMode
{
    Windowed,
    Fullscreen,
}

public sealed record CommercialSettingsV3(
    string SchemaVersion,
    CommercialWindowMode WindowMode,
    int UiScalePercent,
    bool ShowControlHelp,
    int MasterVolumePercent,
    int AmbientVolumePercent,
    int SfxVolumePercent,
    bool ReduceMotion)
{
    public const string SupportedSchemaVersion = "gridworks.settings.v3";
    public const string MigratedSchemaVersion = "gridworks.settings.v2";

    public static CommercialSettingsV3 Default { get; } = new(
        SupportedSchemaVersion,
        CommercialWindowMode.Windowed,
        100,
        true,
        100,
        100,
        100,
        false);
}

public sealed record CommercialSettingsLoadResult(
    CommercialCoreDocumentLoadStatus Status,
    CommercialSettingsV3 Settings,
    bool MigratedFromV2,
    string? ErrorMessage);

public static class CommercialSettingsCodec
{
    private sealed record SettingsV2(
        string SchemaVersion,
        CommercialWindowMode WindowMode,
        int UiScalePercent,
        bool ShowControlHelp,
        int MasterVolumePercent,
        int AmbientVolumePercent,
        int SfxVolumePercent);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
        },
    };

    public static byte[] Serialize(CommercialSettingsV3 settings)
    {
        Validate(settings);
        return JsonSerializer.SerializeToUtf8Bytes(settings, Options);
    }

    public static CommercialSettingsV3 Deserialize(
        ReadOnlySpan<byte> utf8Json,
        out bool migratedFromV2)
    {
        try
        {
            byte[] bytes = utf8Json.ToArray();
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
            RejectDuplicates(document.RootElement, "$");
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("schemaVersion", out JsonElement schema) ||
                schema.ValueKind != JsonValueKind.String)
            {
                throw new CommercialCorePersistenceException(
                    "설정의 schemaVersion이 비어 있습니다.");
            }

            switch (schema.GetString())
            {
                case CommercialSettingsV3.SupportedSchemaVersion:
                    RequireExactProperties(document.RootElement,
                    [
                        "schemaVersion",
                        "windowMode",
                        "uiScalePercent",
                        "showControlHelp",
                        "masterVolumePercent",
                        "ambientVolumePercent",
                        "sfxVolumePercent",
                        "reduceMotion",
                    ]);
                    migratedFromV2 = false;
                    CommercialSettingsV3 current = JsonSerializer.Deserialize<CommercialSettingsV3>(
                        bytes,
                        Options) ?? throw new CommercialCorePersistenceException(
                            "설정 기록이 비어 있습니다.");
                    Validate(current);
                    return current;

                case CommercialSettingsV3.MigratedSchemaVersion:
                    RequireExactProperties(document.RootElement,
                    [
                        "schemaVersion",
                        "windowMode",
                        "uiScalePercent",
                        "showControlHelp",
                        "masterVolumePercent",
                        "ambientVolumePercent",
                        "sfxVolumePercent",
                    ]);
                    migratedFromV2 = true;
                    SettingsV2 previous = JsonSerializer.Deserialize<SettingsV2>(bytes, Options)
                        ?? throw new CommercialCorePersistenceException(
                            "이전 설정 기록이 비어 있습니다.");
                    CommercialSettingsV3 migrated = new(
                        CommercialSettingsV3.SupportedSchemaVersion,
                        previous.WindowMode,
                        previous.UiScalePercent,
                        previous.ShowControlHelp,
                        previous.MasterVolumePercent,
                        previous.AmbientVolumePercent,
                        previous.SfxVolumePercent,
                        false);
                    Validate(migrated);
                    return migrated;

                default:
                    throw new CommercialCorePersistenceException(
                        "지원하지 않는 설정 기록 버전입니다.");
            }
        }
        catch (CommercialCorePersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or OverflowException or
            NullReferenceException)
        {
            throw new CommercialCorePersistenceException(
                "설정 기록 형식이 올바르지 않습니다.",
                exception);
        }
    }

    public static void Validate(CommercialSettingsV3 settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Require(
            settings.SchemaVersion == CommercialSettingsV3.SupportedSchemaVersion,
            "write 설정은 settings v3여야 합니다.");
        Require(Enum.IsDefined(settings.WindowMode), "화면 모드를 지원하지 않습니다.");
        Require(settings.UiScalePercent is 100 or 125, "UI 크기는 100% 또는 125%여야 합니다.");
        RequireVolume(settings.MasterVolumePercent, "Master");
        RequireVolume(settings.AmbientVolumePercent, "Ambient");
        RequireVolume(settings.SfxVolumePercent, "SFX");
    }

    private static void RejectDuplicates(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                Require(names.Add(property.Name), $"{path}.{property.Name}이 중복됐습니다.");
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

    private static void RequireExactProperties(
        JsonElement root,
        IReadOnlyList<string> expectedNames)
    {
        HashSet<string> actual = root.EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals(expectedNames),
            "설정 기록의 필수 필드 구성이 올바르지 않습니다.");
    }

    private static void RequireVolume(int percent, string name) => Require(
        percent is 0 or 25 or 50 or 75 or 100,
        $"{name} 음량은 0, 25, 50, 75, 100 중 하나여야 합니다.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new CommercialCorePersistenceException(message);
        }
    }
}

public static class CommercialSettingsPersistenceStore
{
    public const string SettingsFileName = "settings.json";

    public static CommercialSettingsLoadResult Load(string absolutePath)
    {
        ValidateAbsolutePath(absolutePath);
        if (!File.Exists(absolutePath))
        {
            return new CommercialSettingsLoadResult(
                CommercialCoreDocumentLoadStatus.Missing,
                CommercialSettingsV3.Default,
                false,
                null);
        }

        CommercialSettingsV3 settings = CommercialSettingsV3.Default;
        bool migrated = false;
        try
        {
            settings = CommercialSettingsCodec.Deserialize(
                File.ReadAllBytes(absolutePath),
                out migrated);
            if (migrated)
            {
                WriteAtomic(absolutePath, CommercialSettingsCodec.Serialize(settings));
            }
            return new CommercialSettingsLoadResult(
                CommercialCoreDocumentLoadStatus.Loaded,
                settings,
                migrated,
                null);
        }
        catch (Exception exception) when (
            exception is CommercialCorePersistenceException or IOException or
            UnauthorizedAccessException)
        {
            return new CommercialSettingsLoadResult(
                CommercialCoreDocumentLoadStatus.Invalid,
                settings,
                migrated,
                exception.Message);
        }
    }

    public static void Save(string absolutePath, CommercialSettingsV3 settings)
    {
        ValidateAbsolutePath(absolutePath);
        WriteAtomic(absolutePath, CommercialSettingsCodec.Serialize(settings));
    }

    private static void WriteAtomic(string absolutePath, ReadOnlySpan<byte> bytes)
    {
        string directory = Path.GetDirectoryName(absolutePath)!;
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
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, absolutePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void ValidateAbsolutePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path) || Path.GetDirectoryName(path) is null)
        {
            throw new ArgumentException("설정 경로는 절대경로여야 합니다.", nameof(path));
        }
    }
}
