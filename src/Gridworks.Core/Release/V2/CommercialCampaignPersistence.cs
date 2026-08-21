using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Core.Release.V2;

public sealed record CommercialCampaignSaveV3(
    string SchemaVersion,
    string CampaignId,
    string CampaignSha256,
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

public sealed record CommercialCampaignSaveLoadResult(
    CommercialCoreDocumentLoadStatus Status,
    CommercialCampaignSaveV3? Save,
    string? ErrorMessage);

public static class CommercialCampaignSaveCodec
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

    public static CommercialCampaignSaveV3 Create(
        CommercialWorldDefinition world,
        ReadOnlySpan<byte> worldBytes,
        CommercialCampaignDefinition campaign,
        ReadOnlySpan<byte> campaignBytes,
        IReadOnlyList<CommercialCoreCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(commands);
        return new CommercialCampaignSaveV3(
            CommercialCampaignSaveV3.SupportedSchemaVersion,
            campaign.CampaignId,
            CommercialCoreSaveCodec.ComputeSha256(campaignBytes),
            world.WorldId,
            CommercialCoreSaveCodec.ComputeSha256(worldBytes),
            commands);
    }

    public static byte[] Serialize(CommercialCampaignSaveV3 save)
    {
        Validate(save);
        return JsonSerializer.SerializeToUtf8Bytes(save, Options);
    }

    public static CommercialCampaignSaveV3 Deserialize(ReadOnlySpan<byte> utf8Json)
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
            CommercialCampaignSaveV3 save = JsonSerializer.Deserialize<CommercialCampaignSaveV3>(
                bytes,
                Options) ?? throw new CommercialCorePersistenceException(
                    "저장 기록이 비어 있습니다.");
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

    public static void Validate(CommercialCampaignSaveV3 save)
    {
        ArgumentNullException.ThrowIfNull(save);
        Require(save.SchemaVersion == CommercialCampaignSaveV3.SupportedSchemaVersion,
            "지원하지 않는 저장 기록 버전입니다.");
        RequireText(save.CampaignId, "campaignId");
        RequireSha(save.CampaignSha256, "campaignSha256");
        RequireText(save.WorldId, "worldId");
        RequireSha(save.WorldSha256, "worldSha256");
        IReadOnlyList<CommercialCoreCommand> commands = save.Commands
            ?? throw new CommercialCorePersistenceException("저장 명령이 비어 있습니다.");
        Require(commands.Count <= MaximumCommandCount,
            "저장 명령 수가 허용 범위를 벗어났습니다.");
        for (int index = 0; index < commands.Count; index++)
        {
            ValidateCommand(
                commands[index] ?? throw new CommercialCorePersistenceException(
                    $"commands[{index}]가 비어 있습니다."),
                index);
        }
    }

    public static CommercialCoreRun Restore(
        CommercialCampaignSaveV3 save,
        CommercialWorldDefinition world,
        ReadOnlySpan<byte> worldBytes,
        CommercialCampaignDefinition campaign,
        ReadOnlySpan<byte> campaignBytes)
    {
        Validate(save);
        Require(save.WorldId == world.WorldId,
            "저장 기록의 지도 ID가 현재 지도와 다릅니다.");
        Require(save.WorldSha256 == CommercialCoreSaveCodec.ComputeSha256(worldBytes),
            "저장 기록의 지도 해시가 현재 지도와 다릅니다.");
        Require(save.CampaignId == campaign.CampaignId,
            "저장 기록의 캠페인 ID가 현재 데이터와 다릅니다.");
        Require(save.CampaignSha256 == CommercialCoreSaveCodec.ComputeSha256(campaignBytes),
            "저장 기록의 캠페인 해시가 현재 데이터와 다릅니다.");
        try
        {
            return CommercialCoreRun.Restore(world, campaign, save.Commands);
        }
        catch (CommercialCoreReplayException exception)
        {
            throw new CommercialCorePersistenceException(
                "저장 명령을 현재 캠페인에 재생할 수 없습니다.",
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

public static class CommercialCampaignPersistenceStore
{
    public const string SaveFileName = "release-campaign-save-v3.json";

    public static CommercialCampaignSaveLoadResult Load(string absolutePath)
    {
        ValidateAbsolutePath(absolutePath);
        if (!File.Exists(absolutePath))
        {
            return new CommercialCampaignSaveLoadResult(
                CommercialCoreDocumentLoadStatus.Missing,
                null,
                null);
        }
        try
        {
            CommercialCampaignSaveV3 save = CommercialCampaignSaveCodec.Deserialize(
                File.ReadAllBytes(absolutePath));
            return new CommercialCampaignSaveLoadResult(
                CommercialCoreDocumentLoadStatus.Loaded,
                save,
                null);
        }
        catch (Exception exception) when (
            exception is CommercialCorePersistenceException or IOException or
            UnauthorizedAccessException)
        {
            return new CommercialCampaignSaveLoadResult(
                CommercialCoreDocumentLoadStatus.Invalid,
                null,
                exception.Message);
        }
    }

    public static void Save(string absolutePath, CommercialCampaignSaveV3 save)
    {
        ValidateAbsolutePath(absolutePath);
        byte[] bytes = CommercialCampaignSaveCodec.Serialize(save);
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
