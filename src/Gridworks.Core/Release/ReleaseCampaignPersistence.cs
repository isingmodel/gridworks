using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Core.Release;

public static class ReleaseCampaignSaveCodec
{
    private const int MaximumCommandCount = 20_000;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
        },
    };

    public static byte[] Serialize(ReleaseCampaignSave save)
    {
        Validate(save);
        return JsonSerializer.SerializeToUtf8Bytes(save, Options);
    }

    public static ReleaseCampaignSave Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            byte[] bytes = utf8Json.ToArray();
            using JsonDocument document = JsonDocument.Parse(bytes);
            RejectDuplicates(document.RootElement, "$");
            ReleaseCampaignSave save = JsonSerializer.Deserialize<ReleaseCampaignSave>(bytes, Options)
                ?? throw new ReleasePersistenceValidationException("저장 기록이 비어 있습니다.");
            Validate(save);
            return save;
        }
        catch (ReleasePersistenceValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or OverflowException)
        {
            throw new ReleasePersistenceValidationException(
                "저장 기록 형식이 올바르지 않습니다.",
                exception);
        }
    }

    public static void Validate(ReleaseCampaignSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (!string.Equals(
                save.SchemaVersion,
                ReleaseCampaignSave.SupportedSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new ReleasePersistenceValidationException("지원하지 않는 저장 기록 버전입니다.");
        }
        RequireText(save.CampaignId, "campaignId");
        RequireText(save.WorldId, "worldId");
        RequireSha(save.CampaignSha256, "campaignSha256");
        RequireSha(save.WorldSha256, "worldSha256");
        if (save.Commands is null || save.Commands.Count > MaximumCommandCount)
        {
            throw new ReleasePersistenceValidationException("저장 명령 수가 허용 범위를 벗어났습니다.");
        }
        for (int index = 0; index < save.Commands.Count; index++)
        {
            ReleaseCampaignCommand command = save.Commands[index]
                ?? throw new ReleasePersistenceValidationException(
                    $"commands[{index}]가 비어 있습니다.");
            ValidateCommand(command, index);
        }
    }

    private static void ValidateCommand(ReleaseCampaignCommand command, int index)
    {
        if (!Enum.IsDefined(command.Kind))
        {
            throw new ReleasePersistenceValidationException(
                $"commands[{index}].kind를 지원하지 않습니다.");
        }
        bool hasPosition = command.Position is not null;
        bool hasNodeClass = command.NodeClassId is not null;
        bool hasStart = command.StartNodeId is not null;
        bool hasLineClass = command.LineClassId is not null;
        bool hasPoleClass = command.PoleClassId is not null;
        bool valid = command.Kind switch
        {
            ReleaseCampaignCommandKind.SetNodeDraft =>
                hasPosition && hasNodeClass && !hasStart && !hasLineClass && !hasPoleClass,
            ReleaseCampaignCommandKind.AddLinePoint =>
                hasPosition && !hasNodeClass && !hasStart && !hasLineClass && !hasPoleClass,
            ReleaseCampaignCommandKind.StartLineDraft =>
                !hasPosition && !hasNodeClass && hasStart && hasLineClass && hasPoleClass,
            _ => !hasPosition && !hasNodeClass && !hasStart && !hasLineClass && !hasPoleClass,
        };
        if (!valid)
        {
            throw new ReleasePersistenceValidationException(
                $"commands[{index}]의 필드 조합이 올바르지 않습니다.");
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ReleasePersistenceValidationException($"{name}이 비어 있습니다.");
        }
    }

    private static void RequireSha(string value, string name)
    {
        if (value is null || value.Length != 64 || value.Any(character =>
                character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f')))
        {
            throw new ReleasePersistenceValidationException(
                $"{name}은 소문자 SHA-256이어야 합니다.");
        }
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
                    throw new ReleasePersistenceValidationException(
                        $"{path}.{property.Name}이 중복됐습니다.");
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
}

public static class ReleaseCampaignPersistenceStore
{
    public const string SaveFileName = "release-campaign-save-v2.json";

    public static ReleaseCampaignSaveLoadResult Load(string absolutePath)
    {
        ValidateAbsolutePath(absolutePath);
        if (!File.Exists(absolutePath))
        {
            return new ReleaseCampaignSaveLoadResult(
                ReleaseDocumentLoadStatus.Missing,
                null,
                null);
        }
        try
        {
            ReleaseCampaignSave save = ReleaseCampaignSaveCodec.Deserialize(
                File.ReadAllBytes(absolutePath));
            return new ReleaseCampaignSaveLoadResult(
                ReleaseDocumentLoadStatus.Loaded,
                save,
                null);
        }
        catch (Exception exception) when (
            exception is ReleasePersistenceValidationException or IOException or
            UnauthorizedAccessException)
        {
            return new ReleaseCampaignSaveLoadResult(
                ReleaseDocumentLoadStatus.Invalid,
                null,
                exception.Message);
        }
    }

    public static void Save(string absolutePath, ReleaseCampaignSave save)
    {
        ValidateAbsolutePath(absolutePath);
        byte[] bytes = ReleaseCampaignSaveCodec.Serialize(save);
        string? directory = Path.GetDirectoryName(absolutePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("저장 경로에 폴더가 필요합니다.", nameof(absolutePath));
        }
        Directory.CreateDirectory(directory);
        string temporaryPath = absolutePath + ".tmp";
        try
        {
            using (var stream = new FileStream(
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
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("저장 경로는 절대경로여야 합니다.", nameof(path));
        }
    }
}
