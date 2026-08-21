using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Core.Release.V2;

public sealed record CommercialCoreCampaignSave(
    string SchemaVersion,
    string SliceId,
    string SliceSha256,
    string WorldId,
    string WorldSha256,
    IReadOnlyList<CommercialCoreCommand> Commands)
{
    public const string SupportedSchemaVersion = "gridworks.commercial.campaign-save.v3";

    private IReadOnlyList<CommercialCoreCommand> _commands =
        Array.AsReadOnly(Commands.ToArray());

    public IReadOnlyList<CommercialCoreCommand> Commands
    {
        get => _commands;
        init => _commands = Array.AsReadOnly(value.ToArray());
    }
}

public enum CommercialCoreDocumentLoadStatus
{
    Missing,
    Loaded,
    Invalid,
}

public sealed record CommercialCoreSaveLoadResult(
    CommercialCoreDocumentLoadStatus Status,
    CommercialCoreCampaignSave? Save,
    string? ErrorMessage);

public sealed class CommercialCorePersistenceException : Exception
{
    public CommercialCorePersistenceException(string message)
        : base(message)
    {
    }

    public CommercialCorePersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public static class CommercialCoreSaveCodec
{
    private const int MaximumCommandCount = 50_000;

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

    public static string ComputeSha256(ReadOnlySpan<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    public static CommercialCoreCampaignSave Create(
        CommercialWorldDefinition world,
        ReadOnlySpan<byte> worldBytes,
        CommercialCoreSliceDefinition slice,
        ReadOnlySpan<byte> sliceBytes,
        IReadOnlyList<CommercialCoreCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(slice);
        ArgumentNullException.ThrowIfNull(commands);
        return new CommercialCoreCampaignSave(
            CommercialCoreCampaignSave.SupportedSchemaVersion,
            slice.SliceId,
            ComputeSha256(sliceBytes),
            world.WorldId,
            ComputeSha256(worldBytes),
            commands);
    }

    public static byte[] Serialize(CommercialCoreCampaignSave save)
    {
        Validate(save);
        return JsonSerializer.SerializeToUtf8Bytes(save, Options);
    }

    public static CommercialCoreCampaignSave Deserialize(ReadOnlySpan<byte> utf8Json)
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
            CommercialCoreCampaignSave save =
                JsonSerializer.Deserialize<CommercialCoreCampaignSave>(bytes, Options)
                ?? throw new CommercialCorePersistenceException("저장 기록이 비어 있습니다.");
            Validate(save);
            return save;
        }
        catch (CommercialCorePersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or OverflowException or NullReferenceException)
        {
            throw new CommercialCorePersistenceException(
                "저장 기록 형식이 올바르지 않습니다.",
                exception);
        }
    }

    public static void Validate(CommercialCoreCampaignSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        Require(save.SchemaVersion == CommercialCoreCampaignSave.SupportedSchemaVersion,
            "지원하지 않는 저장 기록 버전입니다.");
        RequireText(save.SliceId, "sliceId");
        RequireSha(save.SliceSha256, "sliceSha256");
        RequireText(save.WorldId, "worldId");
        RequireSha(save.WorldSha256, "worldSha256");
        IReadOnlyList<CommercialCoreCommand> commands = save.Commands
            ?? throw new CommercialCorePersistenceException("저장 명령이 비어 있습니다.");
        Require(commands.Count <= MaximumCommandCount,
            "저장 명령 수가 허용 범위를 벗어났습니다.");
        for (int index = 0; index < commands.Count; index++)
        {
            CommercialCoreCommand command = commands[index]
                ?? throw new CommercialCorePersistenceException(
                    $"commands[{index}]가 비어 있습니다.");
            ValidateCommand(command, index);
        }
    }

    public static CommercialCoreRun Restore(
        CommercialCoreCampaignSave save,
        CommercialWorldDefinition world,
        ReadOnlySpan<byte> worldBytes,
        CommercialCoreSliceDefinition slice,
        ReadOnlySpan<byte> sliceBytes)
    {
        Validate(save);
        Require(save.WorldId == world.WorldId, "저장 기록의 지도 ID가 현재 지도와 다릅니다.");
        Require(save.WorldSha256 == ComputeSha256(worldBytes),
            "저장 기록의 지도 해시가 현재 지도와 다릅니다.");
        Require(save.SliceId == slice.SliceId, "저장 기록의 핵심 흐름 ID가 현재 데이터와 다릅니다.");
        Require(save.SliceSha256 == ComputeSha256(sliceBytes),
            "저장 기록의 핵심 흐름 해시가 현재 데이터와 다릅니다.");
        try
        {
            return CommercialCoreRun.Restore(world, slice, save.Commands);
        }
        catch (CommercialCoreReplayException exception)
        {
            throw new CommercialCorePersistenceException(
                "저장 명령을 현재 핵심 흐름에 재생할 수 없습니다.",
                exception);
        }
    }

    private static void ValidateCommand(CommercialCoreCommand command, int index)
    {
        Require(Enum.IsDefined(command.Kind), $"commands[{index}].kind를 지원하지 않습니다.");
        bool position = command.Position is not null;
        bool nodeClass = command.NodeClassId is not null;
        bool startNode = command.StartNodeId is not null;
        bool lineClass = command.LineClassId is not null;
        bool poleClass = command.PoleClassId is not null;
        bool endNode = command.EndNodeId is not null;
        bool pointIndex = command.PointIndex is not null;
        bool promise = command.PromiseDecision is not null;
        if (promise)
        {
            Require(Enum.IsDefined(command.PromiseDecision!.Value),
                $"commands[{index}].promiseDecision을 지원하지 않습니다.");
        }
        bool valid = command.Kind switch
        {
            CommercialCoreCommandKind.SetNodeDraft =>
                position && nodeClass && !startNode && !lineClass && !poleClass && !endNode &&
                !pointIndex && !promise,
            CommercialCoreCommandKind.StartLineDraft =>
                !position && !nodeClass && startNode && lineClass && poleClass && !endNode &&
                !pointIndex && !promise,
            CommercialCoreCommandKind.AddLinePoint =>
                position && !nodeClass && !startNode && !lineClass && !poleClass && !endNode &&
                !pointIndex && !promise,
            CommercialCoreCommandKind.MoveLinePoint =>
                position && !nodeClass && !startNode && !lineClass && !poleClass && !endNode &&
                pointIndex && command.PointIndex >= 0 && !promise,
            CommercialCoreCommandKind.FinishLineDraft =>
                !position && !nodeClass && !startNode && !lineClass && !poleClass && endNode &&
                !pointIndex && !promise,
            CommercialCoreCommandKind.SetPromiseDecision =>
                !position && !nodeClass && !startNode && !lineClass && !poleClass && !endNode &&
                !pointIndex && promise,
            CommercialCoreCommandKind.CancelNodeDraft or
            CommercialCoreCommandKind.UndoLinePoint or
            CommercialCoreCommandKind.CancelLineDraft or
            CommercialCoreCommandKind.OrderNode or
            CommercialCoreCommandKind.OrderLine or
            CommercialCoreCommandKind.AdvanceConstruction or
            CommercialCoreCommandKind.ApproveDecisionWindow =>
                !position && !nodeClass && !startNode && !lineClass && !poleClass && !endNode &&
                !pointIndex && !promise,
            _ => false,
        };
        Require(valid, $"commands[{index}]의 필드 조합이 올바르지 않습니다.");
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

    private static void RequireText(string? value, string name) =>
        Require(!string.IsNullOrWhiteSpace(value), $"{name}이 비어 있습니다.");

    private static void RequireSha(string? value, string name) => Require(
        value is not null && value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f'),
        $"{name}은 소문자 SHA-256이어야 합니다.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new CommercialCorePersistenceException(message);
        }
    }
}

public static class CommercialCorePersistenceStore
{
    public const string SaveFileName = "release-campaign-save-v3.json";

    public static CommercialCoreSaveLoadResult Load(string absolutePath)
    {
        ValidateAbsolutePath(absolutePath);
        if (!File.Exists(absolutePath))
        {
            return new CommercialCoreSaveLoadResult(
                CommercialCoreDocumentLoadStatus.Missing,
                null,
                null);
        }
        try
        {
            CommercialCoreCampaignSave save = CommercialCoreSaveCodec.Deserialize(
                File.ReadAllBytes(absolutePath));
            return new CommercialCoreSaveLoadResult(
                CommercialCoreDocumentLoadStatus.Loaded,
                save,
                null);
        }
        catch (Exception exception) when (
            exception is CommercialCorePersistenceException or IOException or
            UnauthorizedAccessException)
        {
            return new CommercialCoreSaveLoadResult(
                CommercialCoreDocumentLoadStatus.Invalid,
                null,
                exception.Message);
        }
    }

    public static void Save(string absolutePath, CommercialCoreCampaignSave save)
    {
        ValidateAbsolutePath(absolutePath);
        byte[] bytes = CommercialCoreSaveCodec.Serialize(save);
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
            throw new ArgumentException("저장 경로는 절대경로여야 합니다.", nameof(path));
        }
    }
}
