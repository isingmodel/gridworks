using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

public sealed record RealtimeCampaignSourceIdentity(
    string RouteId,
    string BaseWorldSha256,
    string BaseCampaignSha256,
    string WorldSha256,
    string RealtimeOverlaySha256,
    string SelectedComposedCampaignSha256,
    string FullComposedCampaignSha256);

public sealed record RealtimeCampaignSave(
    string SchemaVersion,
    RealtimeCampaignSourceIdentity Source,
    long SavedMinute,
    string CanonicalStateSha256,
    IReadOnlyList<TimedRealtimeCommand> Commands)
{
    public const string SupportedSchemaVersion =
        "gridworks.realtime.campaign-save.v1";

    private IReadOnlyList<TimedRealtimeCommand> _commands =
        RealtimeStructural.Freeze(Commands);

    public IReadOnlyList<TimedRealtimeCommand> Commands
    {
        get => _commands;
        init => _commands = RealtimeStructural.Freeze(value);
    }
}

public sealed record RealtimeCampaignRestoreResult(
    RealtimeCampaignRun Run,
    IReadOnlyList<RealtimeTransition> Transitions)
{
    private IReadOnlyList<RealtimeTransition> _transitions =
        RealtimeStructural.Freeze(Transitions);

    public IReadOnlyList<RealtimeTransition> Transitions
    {
        get => _transitions;
        init => _transitions = RealtimeStructural.Freeze(value);
    }
}

public enum RealtimeCampaignPersistenceFailureKind
{
    Invalid,
    Unsupported,
}

public sealed class RealtimeCampaignPersistenceException : Exception
{
    public RealtimeCampaignPersistenceException(
        RealtimeCampaignPersistenceFailureKind kind,
        string message)
        : base(message)
    {
        Kind = kind;
    }

    public RealtimeCampaignPersistenceException(
        RealtimeCampaignPersistenceFailureKind kind,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public RealtimeCampaignPersistenceFailureKind Kind { get; }
}

public static class RealtimeCampaignSaveCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false),
        },
    };

    public static RealtimeCampaignSave Capture(
        RealtimeCampaignSourceIdentity identity,
        RealtimeCampaignDefinition campaign,
        RealtimeWorldDefinition world,
        RealtimeCampaignRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        ValidateContext(identity, campaign, world);

        try
        {
            IReadOnlyList<TimedRealtimeCommand> journal = run.AcceptedCommands;
            string liveHash = run.GetCanonicalStateSha256();
            var save = new RealtimeCampaignSave(
                RealtimeCampaignSave.SupportedSchemaVersion,
                identity,
                run.Minute,
                liveHash,
                journal);
            ValidateSave(save);

            RealtimeCampaignRestoreResult replay = Restore(
                identity,
                campaign,
                world,
                save);
            Require(
                SameHash(liveHash, replay.Run.GetCanonicalStateSha256()),
                "The accepted journal did not reproduce the live canonical state.");
            Require(
                journal.SequenceEqual(replay.Run.AcceptedCommands),
                "The accepted journal did not reproduce itself during replay.");
            return save;
        }
        catch (RealtimeCampaignPersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceInputFailure(exception))
        {
            throw Invalid("The realtime campaign could not be captured.", exception);
        }
    }

    public static byte[] Serialize(RealtimeCampaignSave save)
    {
        ValidateSave(save);
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(save, JsonOptions);
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            throw Invalid("The realtime save could not be serialized.", exception);
        }
    }

    public static RealtimeCampaignSave Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Deserialize(Encoding.UTF8.GetBytes(json));
    }

    public static RealtimeCampaignSave Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8Json.ToArray());
            JsonElement root = document.RootElement;
            RejectDuplicateFields(root, "$");
            Require(root.ValueKind == JsonValueKind.Object,
                "The realtime save root must be an object.");

            string schema = ReadString(root, "schemaVersion", "$");
            ValidateSchema(schema);
            RequireFields(root, "$", [
                "schemaVersion",
                "source",
                "savedMinute",
                "canonicalStateSha256",
                "commands",
            ]);

            var save = new RealtimeCampaignSave(
                schema,
                ReadSource(root.GetProperty("source")),
                ReadInt64(root, "savedMinute", "$"),
                ReadString(root, "canonicalStateSha256", "$"),
                ReadCommands(root.GetProperty("commands")));
            ValidateSave(save);
            return save;
        }
        catch (RealtimeCampaignPersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or
                FormatException or OverflowException)
        {
            throw Invalid("The realtime save document is invalid.", exception);
        }
    }

    public static RealtimeCampaignRestoreResult Restore(
        RealtimeCampaignSourceIdentity identity,
        RealtimeCampaignDefinition campaign,
        RealtimeWorldDefinition world,
        RealtimeCampaignSave save)
    {
        ValidateContext(identity, campaign, world);
        ValidateSave(save);
        Require(SameSource(identity, save.Source),
            "The realtime save source does not match the active source.");

        try
        {
            var run = new RealtimeCampaignRun(campaign, world);
            Require(save.SavedMinute >= run.Minute,
                "savedMinute precedes the campaign clock.");
            var transitions = new List<RealtimeTransition>();

            for (int index = 0; index < save.Commands.Count; index++)
            {
                TimedRealtimeCommand timed = save.Commands[index];
                Require(timed.Minute >= run.Minute,
                    $"commands[{index}] precedes the campaign clock.");

                RealtimeAdvanceResult advance = run.AdvanceTo(timed.Minute);
                transitions.AddRange(advance.Transitions);
                RealtimeCommandResult command = run.ApplyCommand(
                    timed.Minute,
                    timed.Sequence,
                    timed.Command);
                transitions.AddRange(command.Transitions);
                Require(command.Accepted,
                    $"commands[{index}] was rejected during replay: {command.Error}.");
            }

            RealtimeAdvanceResult finalAdvance = run.AdvanceTo(save.SavedMinute);
            transitions.AddRange(finalAdvance.Transitions);
            Require(SameHash(
                    run.GetCanonicalStateSha256(),
                    save.CanonicalStateSha256),
                "The replayed canonical state hash does not match the save.");
            Require(run.AcceptedCommands.SequenceEqual(save.Commands),
                "The replayed accepted journal does not match the save.");
            return new RealtimeCampaignRestoreResult(run, transitions);
        }
        catch (RealtimeCampaignPersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceInputFailure(exception))
        {
            throw Invalid("The realtime save journal could not be replayed.", exception);
        }
    }

    private static RealtimeCampaignSourceIdentity ReadSource(JsonElement element)
    {
        RequireFields(element, "$.source", [
            "routeId",
            "baseWorldSha256",
            "baseCampaignSha256",
            "worldSha256",
            "realtimeOverlaySha256",
            "selectedComposedCampaignSha256",
            "fullComposedCampaignSha256",
        ]);
        return new RealtimeCampaignSourceIdentity(
            ReadString(element, "routeId", "$.source"),
            ReadString(element, "baseWorldSha256", "$.source"),
            ReadString(element, "baseCampaignSha256", "$.source"),
            ReadString(element, "worldSha256", "$.source"),
            ReadString(element, "realtimeOverlaySha256", "$.source"),
            ReadString(element, "selectedComposedCampaignSha256", "$.source"),
            ReadString(element, "fullComposedCampaignSha256", "$.source"));
    }

    private static IReadOnlyList<TimedRealtimeCommand> ReadCommands(JsonElement element)
    {
        Require(element.ValueKind == JsonValueKind.Array,
            "$.commands must be an array.");
        Require(element.GetArrayLength() <= RealtimeCampaignRun.MaximumAcceptedCommands,
            "The realtime command journal exceeds its limit.");
        var commands = new List<TimedRealtimeCommand>(element.GetArrayLength());
        int index = 0;
        foreach (JsonElement item in element.EnumerateArray())
        {
            string path = $"$.commands[{index}]";
            RequireFields(item, path, ["sequence", "minute", "command"]);
            commands.Add(new TimedRealtimeCommand(
                ReadInt64(item, "sequence", path),
                ReadInt64(item, "minute", path),
                ReadCommand(item.GetProperty("command"), $"{path}.command")));
            index++;
        }
        return commands;
    }

    private static RealtimeCommand ReadCommand(JsonElement element, string path)
    {
        Require(element.ValueKind == JsonValueKind.Object,
            $"{path} must be an object.");
        RealtimeCommandKind kind = ReadEnum<RealtimeCommandKind>(
            element,
            "kind",
            path);
        string[] fields = kind switch
        {
            RealtimeCommandKind.SetNodeDraft => ["kind", "firstId", "position"],
            RealtimeCommandKind.StartLineDraft =>
                ["kind", "firstId", "secondId", "thirdId"],
            RealtimeCommandKind.AddLinePoint => ["kind", "position"],
            RealtimeCommandKind.MoveLinePoint => ["kind", "position", "pointIndex"],
            RealtimeCommandKind.FinishLineDraft => ["kind", "firstId"],
            RealtimeCommandKind.SetPromiseDecision => ["kind", "promiseDecision"],
            _ => ["kind"],
        };
        RequireFields(element, path, fields);

        return kind switch
        {
            RealtimeCommandKind.SetNodeDraft => RealtimeCommand.SetNodeDraft(
                ReadString(element, "firstId", path),
                ReadPoint(element.GetProperty("position"), $"{path}.position")),
            RealtimeCommandKind.CancelNodeDraft => RealtimeCommand.CancelNodeDraft(),
            RealtimeCommandKind.OrderNode => RealtimeCommand.OrderNode(),
            RealtimeCommandKind.StartLineDraft => RealtimeCommand.StartLineDraft(
                ReadString(element, "firstId", path),
                ReadString(element, "secondId", path),
                ReadString(element, "thirdId", path)),
            RealtimeCommandKind.AddLinePoint => RealtimeCommand.AddLinePoint(
                ReadPoint(element.GetProperty("position"), $"{path}.position")),
            RealtimeCommandKind.MoveLinePoint => RealtimeCommand.MoveLinePoint(
                ReadInt32(element, "pointIndex", path),
                ReadPoint(element.GetProperty("position"), $"{path}.position")),
            RealtimeCommandKind.UndoLinePoint => RealtimeCommand.UndoLinePoint(),
            RealtimeCommandKind.FinishLineDraft => RealtimeCommand.FinishLineDraft(
                ReadString(element, "firstId", path)),
            RealtimeCommandKind.CancelLineDraft => RealtimeCommand.CancelLineDraft(),
            RealtimeCommandKind.OrderLine => RealtimeCommand.OrderLine(),
            RealtimeCommandKind.SetPromiseDecision => RealtimeCommand.SetPromiseDecision(
                ReadEnum<CommercialPromiseDecision>(
                    element,
                    "promiseDecision",
                    path)),
            _ => throw Invalid($"{path}.kind is invalid."),
        };
    }

    private static MapPoint ReadPoint(JsonElement element, string path)
    {
        RequireFields(element, path, ["xUnit", "yUnit"]);
        return new MapPoint(
            ReadInt32(element, "xUnit", path),
            ReadInt32(element, "yUnit", path));
    }

    private static void ValidateContext(
        RealtimeCampaignSourceIdentity identity,
        RealtimeCampaignDefinition campaign,
        RealtimeWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(world);
        ValidateSource(identity);
        try
        {
            RealtimeCampaignLoader.Validate(campaign, campaign.Content, world);
        }
        catch (Exception exception) when (IsPersistenceInputFailure(exception))
        {
            throw Invalid("The active realtime campaign source is invalid.", exception);
        }
    }

    private static void ValidateSave(RealtimeCampaignSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        ValidateSchema(save.SchemaVersion);
        ValidateSource(save.Source);
        Require(save.SavedMinute >= 0, "savedMinute must be nonnegative.");
        RequireSha256(save.CanonicalStateSha256, "canonicalStateSha256");
        IReadOnlyList<TimedRealtimeCommand> commands = save.Commands ??
            throw Invalid("commands is null.");
        Require(commands.Count <= RealtimeCampaignRun.MaximumAcceptedCommands,
            "The realtime command journal exceeds its limit.");

        long previousMinute = 0;
        for (int index = 0; index < commands.Count; index++)
        {
            TimedRealtimeCommand timed = commands[index] ??
                throw Invalid($"commands[{index}] is null.");
            Require(timed.Sequence == index + 1L,
                $"commands[{index}].sequence is not canonical.");
            Require(timed.Minute >= 0 && timed.Minute <= save.SavedMinute,
                $"commands[{index}].minute is outside the saved time.");
            Require(index == 0 || timed.Minute >= previousMinute,
                "Timed commands are not in minute order.");
            ValidateCommand(timed.Command, index);
            previousMinute = timed.Minute;
        }
    }

    private static void ValidateSchema(string schemaVersion)
    {
        Require(Text(schemaVersion), "schemaVersion must be nonblank and trimmed.");
        if (!string.Equals(
                schemaVersion,
                RealtimeCampaignSave.SupportedSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new RealtimeCampaignPersistenceException(
                RealtimeCampaignPersistenceFailureKind.Unsupported,
                $"The realtime save schema '{schemaVersion}' is unsupported.");
        }
    }

    private static void ValidateSource(RealtimeCampaignSourceIdentity source)
    {
        if (source is null)
        {
            throw Invalid("source is null.");
        }
        Require(Text(source.RouteId), "source.routeId must be nonblank and trimmed.");
        RequireSha256(source.BaseWorldSha256, "source.baseWorldSha256");
        RequireSha256(source.BaseCampaignSha256, "source.baseCampaignSha256");
        RequireSha256(source.WorldSha256, "source.worldSha256");
        RequireSha256(source.RealtimeOverlaySha256, "source.realtimeOverlaySha256");
        RequireSha256(
            source.SelectedComposedCampaignSha256,
            "source.selectedComposedCampaignSha256");
        RequireSha256(
            source.FullComposedCampaignSha256,
            "source.fullComposedCampaignSha256");
    }

    private static void ValidateCommand(RealtimeCommand command, int index)
    {
        if (command is null)
        {
            throw Invalid($"commands[{index}].command is null.");
        }
        bool noIds = command.FirstId is null && command.SecondId is null &&
            command.ThirdId is null;
        bool noPoint = command.Position is null && command.PointIndex is null;
        bool valid = command.Kind switch
        {
            RealtimeCommandKind.SetNodeDraft =>
                Text(command.FirstId) && command.SecondId is null &&
                command.ThirdId is null && command.Position.HasValue &&
                command.PointIndex is null && command.PromiseDecision is null,
            RealtimeCommandKind.CancelNodeDraft or RealtimeCommandKind.OrderNode or
                RealtimeCommandKind.UndoLinePoint or
                RealtimeCommandKind.CancelLineDraft or RealtimeCommandKind.OrderLine =>
                noIds && noPoint && command.PromiseDecision is null,
            RealtimeCommandKind.StartLineDraft =>
                Text(command.FirstId) && Text(command.SecondId) && Text(command.ThirdId) &&
                noPoint && command.PromiseDecision is null,
            RealtimeCommandKind.AddLinePoint =>
                noIds && command.Position.HasValue && command.PointIndex is null &&
                command.PromiseDecision is null,
            RealtimeCommandKind.MoveLinePoint =>
                noIds && command.Position.HasValue && command.PointIndex is >= 0 &&
                command.PromiseDecision is null,
            RealtimeCommandKind.FinishLineDraft =>
                Text(command.FirstId) && command.SecondId is null &&
                command.ThirdId is null && noPoint && command.PromiseDecision is null,
            RealtimeCommandKind.SetPromiseDecision =>
                noIds && noPoint && command.PromiseDecision is
                    CommercialPromiseDecision.Keep or CommercialPromiseDecision.Defer,
            _ => false,
        };
        Require(valid, $"commands[{index}].command has an invalid shape.");
    }

    private static bool SameSource(
        RealtimeCampaignSourceIdentity left,
        RealtimeCampaignSourceIdentity right) =>
        string.Equals(left.RouteId, right.RouteId, StringComparison.Ordinal) &&
        SameHash(left.BaseWorldSha256, right.BaseWorldSha256) &&
        SameHash(left.BaseCampaignSha256, right.BaseCampaignSha256) &&
        SameHash(left.WorldSha256, right.WorldSha256) &&
        SameHash(left.RealtimeOverlaySha256, right.RealtimeOverlaySha256) &&
        SameHash(
            left.SelectedComposedCampaignSha256,
            right.SelectedComposedCampaignSha256) &&
        SameHash(
            left.FullComposedCampaignSha256,
            right.FullComposedCampaignSha256);

    private static bool SameHash(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);

    private static T ReadEnum<T>(JsonElement parent, string name, string path)
        where T : struct, Enum
    {
        string text = ReadString(parent, name, path);
        foreach (T value in Enum.GetValues<T>())
        {
            if (string.Equals(
                    text,
                    JsonNamingPolicy.CamelCase.ConvertName(value.ToString()),
                    StringComparison.Ordinal))
            {
                return value;
            }
        }
        throw Invalid($"{path}.{name} is not a supported enum value.");
    }

    private static string ReadString(JsonElement parent, string name, string path)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
        {
            throw Invalid($"{path}.{name} must be a string.");
        }
        return value.GetString()!;
    }

    private static long ReadInt64(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long result))
        {
            throw Invalid($"{path}.{name} must be a 64-bit integer.");
        }
        return result;
    }

    private static int ReadInt32(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result))
        {
            throw Invalid($"{path}.{name} must be a 32-bit integer.");
        }
        return result;
    }

    private static void RequireFields(
        JsonElement element,
        string path,
        IReadOnlyList<string> expected)
    {
        Require(element.ValueKind == JsonValueKind.Object, $"{path} must be an object.");
        HashSet<string> actual = element.EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals(expected),
            $"{path} has unknown or missing fields.");
    }

    private static void RejectDuplicateFields(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                Require(names.Add(property.Name),
                    $"{path}.{property.Name} is duplicated.");
                RejectDuplicateFields(property.Value, $"{path}.{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicateFields(item, $"{path}[{index++}]");
            }
        }
    }

    private static bool Text(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static void RequireSha256(string value, string path) =>
        Require(value is not null && value.Length == 64 && value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            $"{path} must be a canonical lowercase SHA-256 hex value.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw Invalid(message);
        }
    }

    private static RealtimeCampaignPersistenceException Invalid(string message) =>
        new(RealtimeCampaignPersistenceFailureKind.Invalid, message);

    private static RealtimeCampaignPersistenceException Invalid(
        string message,
        Exception innerException) => new(
        RealtimeCampaignPersistenceFailureKind.Invalid,
        message,
        innerException);

    private static bool IsPersistenceInputFailure(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or OverflowException or
            RealtimeCampaignValidationException;
}
