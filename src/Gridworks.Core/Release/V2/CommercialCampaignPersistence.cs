using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Core.Release.V2;

public sealed record CommercialCampaignSave(
    string SchemaVersion,
    string CampaignId,
    string CampaignSha256,
    string WorldId,
    string WorldSha256,
    IReadOnlyList<CommercialCoreCommand> Commands)
{
    public const string SupportedSchemaVersion = "gridworks.commercial.campaign-save.v3";

    private IReadOnlyList<CommercialCoreCommand> _commands = Freeze(Commands);

    public IReadOnlyList<CommercialCoreCommand> Commands
    {
        get => _commands;
        init => _commands = Freeze(value);
    }

    public bool Equals(CommercialCampaignSave? other) => other is not null &&
        string.Equals(SchemaVersion, other.SchemaVersion, StringComparison.Ordinal) &&
        string.Equals(CampaignId, other.CampaignId, StringComparison.Ordinal) &&
        string.Equals(CampaignSha256, other.CampaignSha256, StringComparison.Ordinal) &&
        string.Equals(WorldId, other.WorldId, StringComparison.Ordinal) &&
        string.Equals(WorldSha256, other.WorldSha256, StringComparison.Ordinal) &&
        Commands.SequenceEqual(other.Commands);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SchemaVersion, StringComparer.Ordinal);
        hash.Add(CampaignId, StringComparer.Ordinal);
        hash.Add(CampaignSha256, StringComparer.Ordinal);
        hash.Add(WorldId, StringComparer.Ordinal);
        hash.Add(WorldSha256, StringComparer.Ordinal);
        foreach (CommercialCoreCommand command in Commands)
        {
            hash.Add(command);
        }
        return hash.ToHashCode();
    }

    private static IReadOnlyList<CommercialCoreCommand> Freeze(
        IReadOnlyList<CommercialCoreCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        return Array.AsReadOnly(commands.ToArray());
    }
}

public enum CommercialCampaignSaveLoadStatus
{
    Missing,
    Loaded,
    RecognizedStageD,
    Invalid,
}

public sealed record CommercialCampaignSaveLoadResult(
    CommercialCampaignSaveLoadStatus Status,
    CommercialCampaignSave? Save,
    string? ErrorMessage);

public enum CommercialCampaignSaveWriteStatus
{
    Saved,
    SavedAfterStageDBackup,
    Failed,
}

public enum CommercialCampaignSaveWriteError
{
    InvalidExistingSave,
    StageDBackupConflict,
    StageDBackupFailed,
    ExistingSaveChanged,
    CampaignWriteFailed,
}

public sealed record CommercialCampaignSaveWriteResult(
    CommercialCampaignSaveWriteStatus Status,
    CommercialCampaignSaveWriteError? Error,
    string? StageDBackupPath,
    string? ErrorMessage);

public sealed class CommercialCampaignPersistenceException : Exception
{
    public CommercialCampaignPersistenceException(string message)
        : base(message)
    {
    }

    public CommercialCampaignPersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public static class CommercialCampaignSaveCodec
{
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

    public static CommercialCampaignSave Capture(
        CommercialCampaignDefinition campaign,
        CommercialWorldDefinition world,
        string campaignSha256,
        string worldSha256,
        CommercialCampaignRun run)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(run);
        CommercialCampaignLoader.Validate(campaign, world);
        RequireSha256(campaignSha256, nameof(campaignSha256));
        RequireSha256(worldSha256, nameof(worldSha256));
        CommercialCoreCommand[] commands = run.Commands.ToArray();
        try
        {
            _ = CommercialCampaignRun.Restore(campaign, world, commands);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new CommercialCampaignPersistenceException(
                "The live campaign journal does not match the supplied campaign and world.",
                exception);
        }
        return new CommercialCampaignSave(
            CommercialCampaignSave.SupportedSchemaVersion,
            campaign.CampaignId,
            campaignSha256,
            world.WorldId,
            worldSha256,
            commands);
    }

    public static byte[] Serialize(CommercialCampaignSave save)
    {
        Validate(save);
        return JsonSerializer.SerializeToUtf8Bytes(save, Options);
    }

    public static CommercialCampaignSave Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Deserialize(System.Text.Encoding.UTF8.GetBytes(json));
    }

    public static CommercialCampaignSave Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            byte[] bytes = utf8Json.ToArray();
            using JsonDocument document = JsonDocument.Parse(bytes);
            RejectDuplicates(document.RootElement, "$");
            RequireRootShape(document.RootElement);
            RawSave raw = JsonSerializer.Deserialize<RawSave>(bytes, Options)
                ?? throw new CommercialCampaignPersistenceException(
                    "The campaign save document is empty.");
            CommercialCampaignSave save = Convert(raw, document.RootElement);
            Validate(save);
            return save;
        }
        catch (CommercialCampaignPersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or InvalidOperationException or
            NullReferenceException or OverflowException)
        {
            throw new CommercialCampaignPersistenceException(
                "The campaign save document is invalid.",
                exception);
        }
    }

    public static void Validate(CommercialCampaignSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        Require(save.SchemaVersion == CommercialCampaignSave.SupportedSchemaVersion,
            "The campaign save schemaVersion is unsupported.");
        RequireText(save.CampaignId, "campaignId");
        RequireText(save.WorldId, "worldId");
        RequireSha256(save.CampaignSha256, "campaignSha256");
        RequireSha256(save.WorldSha256, "worldSha256");
        Require(save.Commands.Count <= CommercialCampaignRun.MaximumAcceptedCommands,
            "The campaign command journal exceeds its limit.");
        for (int index = 0; index < save.Commands.Count; index++)
        {
            CommercialCoreCommand command = save.Commands[index] ??
                throw new CommercialCampaignPersistenceException($"commands[{index}] is null.");
            ValidateCommand(command, index);
        }
    }

    public static CommercialCampaignRun Restore(
        CommercialCampaignDefinition campaign,
        CommercialWorldDefinition world,
        string campaignSha256,
        string worldSha256,
        CommercialCampaignSave save)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(save);
        CommercialCampaignLoader.Validate(campaign, world);
        RequireSha256(campaignSha256, nameof(campaignSha256));
        RequireSha256(worldSha256, nameof(worldSha256));
        Validate(save);
        Require(
            save.CampaignId == campaign.CampaignId &&
            save.CampaignSha256 == campaignSha256 &&
            save.WorldId == world.WorldId &&
            save.WorldSha256 == worldSha256,
            "The campaign save does not match the active campaign and world.");
        try
        {
            return CommercialCampaignRun.Restore(campaign, world, save.Commands);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new CommercialCampaignPersistenceException(
                "The campaign command journal could not be replayed.",
                exception);
        }
    }

    private static CommercialCampaignSave Convert(RawSave raw, JsonElement root)
    {
        if (raw.Commands is null)
        {
            throw new CommercialCampaignPersistenceException("commands is null.");
        }
        JsonElement.ArrayEnumerator commandJson = root.GetProperty("commands").EnumerateArray();
        var commands = new List<CommercialCoreCommand>(raw.Commands.Length);
        int index = 0;
        foreach (JsonElement element in commandJson)
        {
            RawCommand item = raw.Commands[index] ??
                throw new CommercialCampaignPersistenceException($"commands[{index}] is null.");
            ValidateCommandJsonShape(element, item.Kind, index);
            var command = new CommercialCoreCommand(
                item.Kind,
                item.FirstId,
                item.SecondId,
                item.ThirdId,
                item.Position is null
                    ? null
                    : new MapPoint(item.Position.XUnit, item.Position.YUnit),
                item.PointIndex,
                item.PromiseDecision);
            ValidateCommand(command, index);
            commands.Add(command);
            index++;
        }
        Require(index == raw.Commands.Length,
            "The command JSON and deserialized command count differ.");
        return new CommercialCampaignSave(
            raw.SchemaVersion,
            raw.CampaignId,
            raw.CampaignSha256,
            raw.WorldId,
            raw.WorldSha256,
            commands);
    }

    private static void ValidateCommand(CommercialCoreCommand command, int index)
    {
        Require(Enum.IsDefined(command.Kind), $"commands[{index}].kind is unknown.");
        bool noIds = command.FirstId is null &&
            command.SecondId is null &&
            command.ThirdId is null;
        bool noPoint = command.Position is null && command.PointIndex is null;
        bool valid = command.Kind switch
        {
            CommercialCoreCommandKind.SetNodeDraft =>
                Text(command.FirstId) && command.SecondId is null && command.ThirdId is null &&
                command.Position.HasValue && command.PointIndex is null &&
                command.PromiseDecision is null,
            CommercialCoreCommandKind.CancelNodeDraft or
            CommercialCoreCommandKind.OrderNode or
            CommercialCoreCommandKind.UndoLinePoint or
            CommercialCoreCommandKind.CancelLineDraft or
            CommercialCoreCommandKind.OrderLine or
            CommercialCoreCommandKind.AdvanceConstruction or
            CommercialCoreCommandKind.ApproveDecisionWindow =>
                noIds && noPoint && command.PromiseDecision is null,
            CommercialCoreCommandKind.StartLineDraft =>
                Text(command.FirstId) && Text(command.SecondId) && Text(command.ThirdId) &&
                noPoint && command.PromiseDecision is null,
            CommercialCoreCommandKind.AddLinePoint =>
                noIds && command.Position.HasValue && command.PointIndex is null &&
                command.PromiseDecision is null,
            CommercialCoreCommandKind.MoveLinePoint =>
                noIds && command.Position.HasValue && command.PointIndex is >= 0 &&
                command.PromiseDecision is null,
            CommercialCoreCommandKind.FinishLineDraft =>
                Text(command.FirstId) && command.SecondId is null && command.ThirdId is null &&
                noPoint && command.PromiseDecision is null,
            CommercialCoreCommandKind.SetPromiseDecision =>
                noIds && noPoint && command.PromiseDecision is
                    CommercialPromiseDecision.Keep or CommercialPromiseDecision.Defer,
            _ => false,
        };
        Require(valid, $"commands[{index}] has an invalid field combination.");
    }

    private static void RequireRootShape(JsonElement root)
    {
        Require(root.ValueKind == JsonValueKind.Object,
            "The campaign save root must be an object.");
        string[] expected =
        [
            "schemaVersion",
            "campaignId",
            "campaignSha256",
            "worldId",
            "worldSha256",
            "commands",
        ];
        HashSet<string> actual = root.EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals(expected),
            "The campaign save root fields do not match campaign save v3.");
        Require(root.GetProperty("commands").ValueKind == JsonValueKind.Array,
            "commands must be an array.");
    }

    private static void ValidateCommandJsonShape(
        JsonElement element,
        CommercialCoreCommandKind kind,
        int index)
    {
        Require(element.ValueKind == JsonValueKind.Object,
            $"commands[{index}] must be an object.");
        string[] expected = kind switch
        {
            CommercialCoreCommandKind.SetNodeDraft => ["kind", "firstId", "position"],
            CommercialCoreCommandKind.StartLineDraft =>
                ["kind", "firstId", "secondId", "thirdId"],
            CommercialCoreCommandKind.AddLinePoint => ["kind", "position"],
            CommercialCoreCommandKind.MoveLinePoint => ["kind", "position", "pointIndex"],
            CommercialCoreCommandKind.FinishLineDraft => ["kind", "firstId"],
            CommercialCoreCommandKind.SetPromiseDecision => ["kind", "promiseDecision"],
            CommercialCoreCommandKind.CancelNodeDraft or
            CommercialCoreCommandKind.OrderNode or
            CommercialCoreCommandKind.UndoLinePoint or
            CommercialCoreCommandKind.CancelLineDraft or
            CommercialCoreCommandKind.OrderLine or
            CommercialCoreCommandKind.AdvanceConstruction or
            CommercialCoreCommandKind.ApproveDecisionWindow => ["kind"],
            _ => throw new CommercialCampaignPersistenceException(
                $"commands[{index}].kind is unknown."),
        };
        HashSet<string> actual = element.EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals(expected),
            $"commands[{index}] fields do not match {kind}.");
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
                    throw new CommercialCampaignPersistenceException(
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

    private static bool Text(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value == value.Trim();

    private static void RequireText(string value, string name) =>
        Require(Text(value), $"{name} must be nonblank and trimmed.");

    private static void RequireSha256(string value, string name) =>
        Require(value is not null && value.Length == 64 && value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            $"{name} must be a lowercase SHA-256 value.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new CommercialCampaignPersistenceException(message);
        }
    }

    private sealed class RawSave
    {
        [JsonRequired] public string SchemaVersion { get; init; } = null!;
        [JsonRequired] public string CampaignId { get; init; } = null!;
        [JsonRequired] public string CampaignSha256 { get; init; } = null!;
        [JsonRequired] public string WorldId { get; init; } = null!;
        [JsonRequired] public string WorldSha256 { get; init; } = null!;
        [JsonRequired] public RawCommand[] Commands { get; init; } = null!;
    }

    private sealed class RawCommand
    {
        [JsonRequired] public CommercialCoreCommandKind Kind { get; init; }
        public string? FirstId { get; init; }
        public string? SecondId { get; init; }
        public string? ThirdId { get; init; }
        public RawPoint? Position { get; init; }
        public int? PointIndex { get; init; }
        public CommercialPromiseDecision? PromiseDecision { get; init; }
    }

    private sealed class RawPoint
    {
        [JsonRequired] public int XUnit { get; init; }
        [JsonRequired] public int YUnit { get; init; }
    }
}

public static class CommercialCampaignSaveStore
{
    public static CommercialCampaignSaveLoadResult Load(string absolutePath)
    {
        ValidateAbsolutePath(absolutePath);
        if (!File.Exists(absolutePath))
        {
            return new CommercialCampaignSaveLoadResult(
                CommercialCampaignSaveLoadStatus.Missing,
                null,
                null);
        }
        try
        {
            return Inspect(File.ReadAllBytes(absolutePath));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            CommercialCampaignPersistenceException)
        {
            return new CommercialCampaignSaveLoadResult(
                CommercialCampaignSaveLoadStatus.Invalid,
                null,
                exception.Message);
        }
    }

    public static CommercialCampaignSaveWriteResult SaveWithStageDBackup(
        string absolutePath,
        CommercialCampaignSave save)
    {
        ValidateAbsolutePath(absolutePath);
        byte[] campaignBytes;
        try
        {
            campaignBytes = CommercialCampaignSaveCodec.Serialize(save);
        }
        catch (CommercialCampaignPersistenceException exception)
        {
            return Failed(
                CommercialCampaignSaveWriteError.CampaignWriteFailed,
                null,
                exception.Message);
        }

        byte[]? existingBytes = null;
        CommercialCampaignSaveLoadResult existing;
        try
        {
            if (File.Exists(absolutePath))
            {
                existingBytes = File.ReadAllBytes(absolutePath);
                existing = Inspect(existingBytes);
            }
            else
            {
                existing = new CommercialCampaignSaveLoadResult(
                    CommercialCampaignSaveLoadStatus.Missing,
                    null,
                    null);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            CommercialCampaignPersistenceException)
        {
            return Failed(
                CommercialCampaignSaveWriteError.InvalidExistingSave,
                null,
                exception.Message);
        }

        if (existing.Status == CommercialCampaignSaveLoadStatus.Invalid)
        {
            return Failed(
                CommercialCampaignSaveWriteError.InvalidExistingSave,
                null,
                existing.ErrorMessage ?? "The existing save is invalid.");
        }

        string? backupPath = null;
        if (existing.Status == CommercialCampaignSaveLoadStatus.RecognizedStageD)
        {
            backupPath = StageDBackupPath(absolutePath, existingBytes!);
            CommercialCampaignSaveWriteResult? backupFailure = EnsureStageDBackup(
                backupPath,
                existingBytes!);
            if (backupFailure is not null)
            {
                return backupFailure;
            }
            try
            {
                if (!File.Exists(absolutePath) ||
                    !File.ReadAllBytes(absolutePath).SequenceEqual(existingBytes!))
                {
                    return Failed(
                        CommercialCampaignSaveWriteError.ExistingSaveChanged,
                        backupPath,
                        "The active save changed after its Stage D backup was secured.");
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                return Failed(
                    CommercialCampaignSaveWriteError.ExistingSaveChanged,
                    backupPath,
                    exception.Message);
            }
        }

        try
        {
            AtomicReplace(absolutePath, campaignBytes);
            return new CommercialCampaignSaveWriteResult(
                backupPath is null
                    ? CommercialCampaignSaveWriteStatus.Saved
                    : CommercialCampaignSaveWriteStatus.SavedAfterStageDBackup,
                null,
                backupPath,
                null);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return Failed(
                CommercialCampaignSaveWriteError.CampaignWriteFailed,
                backupPath,
                exception.Message);
        }
    }

    private static CommercialCampaignSaveLoadResult Inspect(byte[] bytes)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schemaVersion", out JsonElement schemaElement) ||
                schemaElement.ValueKind != JsonValueKind.String)
            {
                return Invalid("The save has no recognized schemaVersion.");
            }
            string schema = schemaElement.GetString()!;
            if (schema == CommercialCampaignSave.SupportedSchemaVersion)
            {
                CommercialCampaignSave save = CommercialCampaignSaveCodec.Deserialize(bytes);
                return new CommercialCampaignSaveLoadResult(
                    CommercialCampaignSaveLoadStatus.Loaded,
                    save,
                    null);
            }
            if (schema == CommercialCoreSave.SupportedSchemaVersion)
            {
                _ = CommercialCoreSaveCodec.Deserialize(bytes);
                return new CommercialCampaignSaveLoadResult(
                    CommercialCampaignSaveLoadStatus.RecognizedStageD,
                    null,
                    null);
            }
            return Invalid($"Unsupported save schemaVersion '{schema}'.");
        }
        catch (Exception exception) when (
            exception is JsonException or CommercialCorePersistenceException or
            CommercialCampaignPersistenceException or ArgumentException or
            InvalidOperationException)
        {
            return Invalid(exception.Message);
        }
    }

    private static CommercialCampaignSaveWriteResult? EnsureStageDBackup(
        string backupPath,
        byte[] stageDBytes)
    {
        try
        {
            if (File.Exists(backupPath))
            {
                return File.ReadAllBytes(backupPath).SequenceEqual(stageDBytes)
                    ? null
                    : Failed(
                        CommercialCampaignSaveWriteError.StageDBackupConflict,
                        backupPath,
                        "The deterministic Stage D backup path contains different bytes.");
            }

            string? directory = Path.GetDirectoryName(backupPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return Failed(
                    CommercialCampaignSaveWriteError.StageDBackupFailed,
                    backupPath,
                    "The Stage D backup path has no directory.");
            }
            Directory.CreateDirectory(directory);
            string temporaryPath = backupPath + ".tmp";
            try
            {
                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.Create,
                           FileAccess.Write,
                           FileShare.None))
                {
                    stream.Write(stageDBytes);
                    stream.Flush(flushToDisk: true);
                }
                try
                {
                    File.Move(temporaryPath, backupPath, overwrite: false);
                }
                catch (IOException) when (File.Exists(backupPath))
                {
                    if (!File.ReadAllBytes(backupPath).SequenceEqual(stageDBytes))
                    {
                        return Failed(
                            CommercialCampaignSaveWriteError.StageDBackupConflict,
                            backupPath,
                            "The deterministic Stage D backup was created with different bytes.");
                    }
                }
            }
            finally
            {
                TryDelete(temporaryPath);
            }
            return null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return Failed(
                CommercialCampaignSaveWriteError.StageDBackupFailed,
                backupPath,
                exception.Message);
        }
    }

    private static void AtomicReplace(string absolutePath, byte[] bytes)
    {
        string? directory = Path.GetDirectoryName(absolutePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new IOException("The campaign save path has no directory.");
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
            TryDelete(temporaryPath);
        }
    }

    private static string StageDBackupPath(string absolutePath, byte[] stageDBytes)
    {
        string directory = Path.GetDirectoryName(absolutePath)!;
        string stem = Path.GetFileNameWithoutExtension(absolutePath);
        string digest = Convert.ToHexString(SHA256.HashData(stageDBytes))
            .ToLowerInvariant()[..12];
        return Path.Combine(directory, $"{stem}.stage-d.{digest}.bak.json");
    }

    private static CommercialCampaignSaveWriteResult Failed(
        CommercialCampaignSaveWriteError error,
        string? backupPath,
        string message) => new(
            CommercialCampaignSaveWriteStatus.Failed,
            error,
            backupPath,
            message);

    private static CommercialCampaignSaveLoadResult Invalid(string message) => new(
        CommercialCampaignSaveLoadStatus.Invalid,
        null,
        message);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            _ = exception;
        }
    }

    private static void ValidateAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException(
                "The campaign save path must be absolute.",
                nameof(path));
        }
    }
}
