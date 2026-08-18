using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Core.Release.V2;

public sealed record CommercialCoreSave(
    string SchemaVersion,
    string SliceId,
    string SliceSha256,
    string WorldId,
    string WorldSha256,
    IReadOnlyList<CommercialCoreCommand> Commands)
{
    public const string SupportedSchemaVersion = "gridworks.commercial.core-save.v3";

    private IReadOnlyList<CommercialCoreCommand> _commands = Freeze(Commands);

    public IReadOnlyList<CommercialCoreCommand> Commands
    {
        get => _commands;
        init => _commands = Freeze(value);
    }

    public bool Equals(CommercialCoreSave? other) => other is not null &&
        string.Equals(SchemaVersion, other.SchemaVersion, StringComparison.Ordinal) &&
        string.Equals(SliceId, other.SliceId, StringComparison.Ordinal) &&
        string.Equals(SliceSha256, other.SliceSha256, StringComparison.Ordinal) &&
        string.Equals(WorldId, other.WorldId, StringComparison.Ordinal) &&
        string.Equals(WorldSha256, other.WorldSha256, StringComparison.Ordinal) &&
        Commands.SequenceEqual(other.Commands);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SchemaVersion, StringComparer.Ordinal);
        hash.Add(SliceId, StringComparer.Ordinal);
        hash.Add(SliceSha256, StringComparer.Ordinal);
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

public enum CommercialCoreSaveLoadStatus
{
    Missing,
    Loaded,
    Invalid,
}

public sealed record CommercialCoreSaveLoadResult(
    CommercialCoreSaveLoadStatus Status,
    CommercialCoreSave? Save,
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

    public static CommercialCoreSave Capture(
        CommercialCoreSliceDefinition slice,
        CommercialWorldDefinition world,
        string sliceSha256,
        string worldSha256,
        CommercialCoreSliceRun run)
    {
        ArgumentNullException.ThrowIfNull(slice);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(run);
        CommercialCoreSliceLoader.Validate(slice, world);
        RequireSha256(sliceSha256, nameof(sliceSha256));
        RequireSha256(worldSha256, nameof(worldSha256));

        CommercialCoreCommand[] commands = run.Commands.ToArray();
        try
        {
            _ = CommercialCoreSliceRun.Restore(slice, world, commands);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new CommercialCorePersistenceException(
                "The live commercial command journal does not match the supplied slice and world.",
                exception);
        }

        return new CommercialCoreSave(
            CommercialCoreSave.SupportedSchemaVersion,
            slice.SliceId,
            sliceSha256,
            world.WorldId,
            worldSha256,
            commands);
    }

    public static byte[] Serialize(CommercialCoreSave save)
    {
        Validate(save);
        return JsonSerializer.SerializeToUtf8Bytes(save, Options);
    }

    public static CommercialCoreSave Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Deserialize(System.Text.Encoding.UTF8.GetBytes(json));
    }

    public static CommercialCoreSave Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            byte[] bytes = utf8Json.ToArray();
            using JsonDocument document = JsonDocument.Parse(bytes);
            RejectDuplicates(document.RootElement, "$");
            RequireRootShape(document.RootElement);
            RawSave raw = JsonSerializer.Deserialize<RawSave>(bytes, Options)
                ?? throw new CommercialCorePersistenceException(
                    "The commercial save document is empty.");
            CommercialCoreSave save = Convert(raw, document.RootElement);
            Validate(save);
            return save;
        }
        catch (CommercialCorePersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or InvalidOperationException or
            OverflowException)
        {
            throw new CommercialCorePersistenceException(
                "The commercial save document is invalid.",
                exception);
        }
    }

    public static void Validate(CommercialCoreSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        Require(
            string.Equals(
                save.SchemaVersion,
                CommercialCoreSave.SupportedSchemaVersion,
                StringComparison.Ordinal),
            "The commercial save schemaVersion is unsupported.");
        RequireText(save.SliceId, "sliceId");
        RequireText(save.WorldId, "worldId");
        RequireSha256(save.SliceSha256, "sliceSha256");
        RequireSha256(save.WorldSha256, "worldSha256");
        Require(save.Commands.Count <= CommercialCoreSliceRun.MaximumAcceptedCommands,
            "The commercial command journal exceeds its limit.");
        for (int index = 0; index < save.Commands.Count; index++)
        {
            CommercialCoreCommand command = save.Commands[index]
                ?? throw new CommercialCorePersistenceException(
                    $"commands[{index}] is null.");
            ValidateCommand(command, index);
        }
    }

    public static CommercialCoreSliceRun Restore(
        CommercialCoreSliceDefinition slice,
        CommercialWorldDefinition world,
        string sliceSha256,
        string worldSha256,
        CommercialCoreSave save)
    {
        ArgumentNullException.ThrowIfNull(slice);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(save);
        CommercialCoreSliceLoader.Validate(slice, world);
        RequireSha256(sliceSha256, nameof(sliceSha256));
        RequireSha256(worldSha256, nameof(worldSha256));
        Validate(save);
        Require(
            string.Equals(save.SliceId, slice.SliceId, StringComparison.Ordinal) &&
            string.Equals(save.SliceSha256, sliceSha256, StringComparison.Ordinal) &&
            string.Equals(save.WorldId, world.WorldId, StringComparison.Ordinal) &&
            string.Equals(save.WorldSha256, worldSha256, StringComparison.Ordinal),
            "The commercial save does not match the active slice and world.");

        try
        {
            return CommercialCoreSliceRun.Restore(slice, world, save.Commands);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new CommercialCorePersistenceException(
                "The commercial command journal could not be replayed.",
                exception);
        }
    }

    private static CommercialCoreSave Convert(RawSave raw, JsonElement root)
    {
        if (raw.Commands is null)
        {
            throw new CommercialCorePersistenceException("commands is null.");
        }
        JsonElement.ArrayEnumerator commandJson = root.GetProperty("commands").EnumerateArray();
        var commands = new List<CommercialCoreCommand>(raw.Commands.Length);
        int index = 0;
        foreach (JsonElement element in commandJson)
        {
            RawCommand item = raw.Commands[index]
                ?? throw new CommercialCorePersistenceException(
                    $"commands[{index}] is null.");
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
        return new CommercialCoreSave(
            raw.SchemaVersion,
            raw.SliceId,
            raw.SliceSha256,
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
            "The commercial save root must be an object.");
        string[] expected =
        [
            "schemaVersion",
            "sliceId",
            "sliceSha256",
            "worldId",
            "worldSha256",
            "commands",
        ];
        HashSet<string> actual = root.EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals(expected),
            "The commercial save root fields do not match save v3.");
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
            _ => throw new CommercialCorePersistenceException(
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
                    throw new CommercialCorePersistenceException(
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
            throw new CommercialCorePersistenceException(message);
        }
    }

    private sealed class RawSave
    {
        [JsonRequired] public string SchemaVersion { get; init; } = null!;
        [JsonRequired] public string SliceId { get; init; } = null!;
        [JsonRequired] public string SliceSha256 { get; init; } = null!;
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

public static class CommercialCoreSaveStore
{
    public static CommercialCoreSaveLoadResult Load(string absolutePath)
    {
        ValidateAbsolutePath(absolutePath);
        if (!File.Exists(absolutePath))
        {
            return new CommercialCoreSaveLoadResult(
                CommercialCoreSaveLoadStatus.Missing,
                null,
                null);
        }
        try
        {
            CommercialCoreSave save = CommercialCoreSaveCodec.Deserialize(
                File.ReadAllBytes(absolutePath));
            return new CommercialCoreSaveLoadResult(
                CommercialCoreSaveLoadStatus.Loaded,
                save,
                null);
        }
        catch (Exception exception) when (
            exception is CommercialCorePersistenceException or IOException or
            UnauthorizedAccessException)
        {
            return new CommercialCoreSaveLoadResult(
                CommercialCoreSaveLoadStatus.Invalid,
                null,
                exception.Message);
        }
    }

    public static void Save(string absolutePath, CommercialCoreSave save)
    {
        ValidateAbsolutePath(absolutePath);
        byte[] bytes = CommercialCoreSaveCodec.Serialize(save);
        string? directory = Path.GetDirectoryName(absolutePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException(
                "The commercial save path must contain a directory.",
                nameof(absolutePath));
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
            throw new ArgumentException(
                "The commercial save path must be absolute.",
                nameof(path));
        }
    }
}
