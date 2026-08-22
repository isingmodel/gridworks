using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

public sealed record RealtimeSeedNode(
    string NodeId,
    string ClassId,
    string DisplayName,
    int XUnit,
    int YUnit,
    bool Commissioned,
    bool AuthoredFoundation);

public sealed record RealtimeSeedEdge(
    string EdgeId,
    string LineClassId,
    string FromNodeId,
    string ToNodeId,
    bool Commissioned);

public sealed record RealtimeBaseStateSeed(
    string SeedId,
    int StartMinute,
    long InitialCashUnit,
    IReadOnlyList<string> BaseNodeIds,
    IReadOnlyList<string> BaseEdgeIds,
    IReadOnlyList<RealtimeSeedNode> ConstructedNodes,
    IReadOnlyList<RealtimeSeedEdge> ConstructedEdges,
    IReadOnlyList<string> CoolingAssetIds)
{
    private IReadOnlyList<string> _baseNodeIds = Freeze(BaseNodeIds);
    private IReadOnlyList<string> _baseEdgeIds = Freeze(BaseEdgeIds);
    private IReadOnlyList<RealtimeSeedNode> _constructedNodes = Freeze(ConstructedNodes);
    private IReadOnlyList<RealtimeSeedEdge> _constructedEdges = Freeze(ConstructedEdges);
    private IReadOnlyList<string> _coolingAssetIds = Freeze(CoolingAssetIds);

    public IReadOnlyList<string> BaseNodeIds
    {
        get => _baseNodeIds;
        init => _baseNodeIds = Freeze(value);
    }

    public IReadOnlyList<string> BaseEdgeIds
    {
        get => _baseEdgeIds;
        init => _baseEdgeIds = Freeze(value);
    }

    public IReadOnlyList<RealtimeSeedNode> ConstructedNodes
    {
        get => _constructedNodes;
        init => _constructedNodes = Freeze(value);
    }

    public IReadOnlyList<RealtimeSeedEdge> ConstructedEdges
    {
        get => _constructedEdges;
        init => _constructedEdges = Freeze(value);
    }

    public IReadOnlyList<string> CoolingAssetIds
    {
        get => _coolingAssetIds;
        init => _coolingAssetIds = Freeze(value);
    }

    public static RealtimeBaseStateSeed From(CommercialCoreSeedDefinition seed) => new(
        seed.SeedId,
        seed.StartMinute,
        seed.InitialCashUnit,
        seed.BaseNodeIds,
        seed.BaseEdgeIds,
        seed.ConstructedNodes.Select(item => new RealtimeSeedNode(
            item.NodeId,
            item.ClassId,
            item.DisplayName,
            item.Position.XUnit,
            item.Position.YUnit,
            item.Commissioned,
            item.AuthoredFoundation)).ToArray(),
        seed.ConstructedEdges.Select(item => new RealtimeSeedEdge(
            item.EdgeId,
            item.LineClassId,
            item.FromNodeId,
            item.ToNodeId,
            item.Commissioned)).ToArray(),
        seed.CoolingAssetIds);

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record RealtimeCampaignSave(
    string SchemaVersion,
    string TransformVersion,
    string CampaignId,
    string CampaignSha256,
    string WorldId,
    string WorldSha256,
    long AnchorMinute,
    RealtimeBaseStateSeed BaseStateSeed,
    string BaseStateSeedSha256,
    long SavedMinute,
    IReadOnlyList<TimedRealtimeCommand> Commands)
{
    public const string SupportedSchemaVersion = "gridworks.realtime.campaign-save.v4";

    private IReadOnlyList<TimedRealtimeCommand> _commands =
        Array.AsReadOnly(Commands.ToArray());

    public IReadOnlyList<TimedRealtimeCommand> Commands
    {
        get => _commands;
        init => _commands = Array.AsReadOnly(value.ToArray());
    }

    public bool Equals(RealtimeCampaignSave? other) => other is not null &&
        string.Equals(SchemaVersion, other.SchemaVersion, StringComparison.Ordinal) &&
        string.Equals(TransformVersion, other.TransformVersion, StringComparison.Ordinal) &&
        string.Equals(CampaignId, other.CampaignId, StringComparison.Ordinal) &&
        string.Equals(CampaignSha256, other.CampaignSha256, StringComparison.Ordinal) &&
        string.Equals(WorldId, other.WorldId, StringComparison.Ordinal) &&
        string.Equals(WorldSha256, other.WorldSha256, StringComparison.Ordinal) &&
        AnchorMinute == other.AnchorMinute &&
        string.Equals(BaseStateSeedSha256, other.BaseStateSeedSha256, StringComparison.Ordinal) &&
        SavedMinute == other.SavedMinute &&
        Commands.SequenceEqual(other.Commands);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SchemaVersion, StringComparer.Ordinal);
        hash.Add(TransformVersion, StringComparer.Ordinal);
        hash.Add(CampaignId, StringComparer.Ordinal);
        hash.Add(CampaignSha256, StringComparer.Ordinal);
        hash.Add(WorldId, StringComparer.Ordinal);
        hash.Add(WorldSha256, StringComparer.Ordinal);
        hash.Add(AnchorMinute);
        hash.Add(BaseStateSeedSha256, StringComparer.Ordinal);
        hash.Add(SavedMinute);
        foreach (TimedRealtimeCommand command in Commands)
        {
            hash.Add(command);
        }
        return hash.ToHashCode();
    }
}

public sealed class RealtimeCampaignPersistenceException : Exception
{
    public RealtimeCampaignPersistenceException(string message)
        : base(message)
    {
    }

    public RealtimeCampaignPersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public static class RealtimeCampaignSaveCodec
{
    public const string SupportedTransformVersion = "gridworks.realtime.seed-transform.v1";

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

    private static readonly JsonSerializerOptions CanonicalSeedOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static RealtimeCampaignSave Capture(
        RealtimeCampaignDefinition campaign,
        RealtimeWorldDefinition world,
        string campaignSha256,
        string worldSha256,
        RealtimeCampaignRun run)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(run);
        RealtimeCampaignLoader.Validate(campaign, campaign.Content, world);
        RequireSha256(campaignSha256, nameof(campaignSha256));
        RequireSha256(worldSha256, nameof(worldSha256));
        RealtimeCampaignSnapshot live = run.GetSnapshot();
        RealtimeBaseStateSeed seed = RealtimeBaseStateSeed.From(
            campaign.Content.InitialSeed);
        var save = new RealtimeCampaignSave(
            RealtimeCampaignSave.SupportedSchemaVersion,
            SupportedTransformVersion,
            campaign.CampaignId,
            campaignSha256,
            world.WorldId,
            worldSha256,
            seed.StartMinute,
            seed,
            CanonicalSeedSha256(seed),
            live.Minute,
            run.AcceptedCommands);
        Validate(save);

        RealtimeCampaignRun replay = Restore(
            campaign,
            world,
            campaignSha256,
            worldSha256,
            save);
        byte[] liveBytes = JsonSerializer.SerializeToUtf8Bytes(live, Options);
        byte[] replayBytes = JsonSerializer.SerializeToUtf8Bytes(replay.GetSnapshot(), Options);
        if (!liveBytes.SequenceEqual(replayBytes))
        {
            throw new RealtimeCampaignPersistenceException(
                "The live realtime state is not reproduced by its timed command journal.");
        }
        return save;
    }

    public static byte[] Serialize(RealtimeCampaignSave save)
    {
        Validate(save);
        return JsonSerializer.SerializeToUtf8Bytes(save, Options);
    }

    public static RealtimeCampaignSave Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Deserialize(System.Text.Encoding.UTF8.GetBytes(json));
    }

    public static RealtimeCampaignSave Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            byte[] bytes = utf8Json.ToArray();
            using JsonDocument document = JsonDocument.Parse(bytes);
            RejectDuplicates(document.RootElement, "$" );
            RequireRootShape(document.RootElement);
            RawSave raw = JsonSerializer.Deserialize<RawSave>(bytes, Options)
                ?? throw new RealtimeCampaignPersistenceException(
                    "The realtime save document is empty.");
            RealtimeCampaignSave save = Convert(raw, document.RootElement);
            Validate(save);
            return save;
        }
        catch (RealtimeCampaignPersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or InvalidOperationException or
            NullReferenceException or OverflowException)
        {
            throw new RealtimeCampaignPersistenceException(
                "The realtime save document is invalid.",
                exception);
        }
    }

    public static void Validate(RealtimeCampaignSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        Require(save.SchemaVersion == RealtimeCampaignSave.SupportedSchemaVersion,
            "The realtime save schemaVersion is unsupported.");
        Require(save.TransformVersion == SupportedTransformVersion,
            "The realtime save transformVersion is unsupported.");
        RequireText(save.CampaignId, "campaignId");
        RequireText(save.WorldId, "worldId");
        RequireSha256(save.CampaignSha256, "campaignSha256");
        RequireSha256(save.WorldSha256, "worldSha256");
        ValidateSeed(save.BaseStateSeed);
        Require(save.AnchorMinute == save.BaseStateSeed.StartMinute,
            "anchorMinute must equal the immutable seed startMinute.");
        RequireSha256(save.BaseStateSeedSha256, "baseStateSeedSha256");
        Require(save.BaseStateSeedSha256 == CanonicalSeedSha256(save.BaseStateSeed),
            "baseStateSeedSha256 does not match the canonical immutable seed.");
        Require(save.SavedMinute >= save.AnchorMinute,
            "savedMinute must not precede anchorMinute.");
        Require(save.Commands.Count <= RealtimeCampaignRun.MaximumAcceptedCommands,
            "The realtime command journal exceeds its limit.");
        long previousMinute = 0;
        for (int index = 0; index < save.Commands.Count; index++)
        {
            TimedRealtimeCommand timed = save.Commands[index] ??
                throw new RealtimeCampaignPersistenceException($"commands[{index}] is null.");
            Require(timed.Sequence == index + 1L,
                $"commands[{index}].sequence must be the canonical one-based sequence.");
            Require(timed.Minute >= save.AnchorMinute && timed.Minute <= save.SavedMinute,
                $"commands[{index}].minute must fit the saved time.");
            Require(index == 0 || timed.Minute >= previousMinute,
                "Timed commands must use nondecreasing minutes.");
            previousMinute = timed.Minute;
            ValidateCommand(timed.Command, index);
        }
    }

    public static RealtimeCampaignRun Restore(
        RealtimeCampaignDefinition campaign,
        RealtimeWorldDefinition world,
        string campaignSha256,
        string worldSha256,
        RealtimeCampaignSave save)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(save);
        RealtimeCampaignLoader.Validate(campaign, campaign.Content, world);
        RequireSha256(campaignSha256, nameof(campaignSha256));
        RequireSha256(worldSha256, nameof(worldSha256));
        Validate(save);
        Require(save.CampaignId == campaign.CampaignId &&
                save.CampaignSha256 == campaignSha256 &&
                save.WorldId == world.WorldId &&
                save.WorldSha256 == worldSha256,
            "The realtime save does not match the active campaign and world.");
        RealtimeBaseStateSeed expectedSeed = RealtimeBaseStateSeed.From(
            campaign.Content.InitialSeed);
        Require(save.TransformVersion == SupportedTransformVersion &&
                save.AnchorMinute == expectedSeed.StartMinute &&
                save.BaseStateSeedSha256 == CanonicalSeedSha256(expectedSeed) &&
                CanonicalSeedBytes(save.BaseStateSeed).SequenceEqual(
                    CanonicalSeedBytes(expectedSeed)),
            "The realtime save immutable base-state seed does not match the campaign.");

        try
        {
            var run = new RealtimeCampaignRun(campaign, world);
            for (int index = 0; index < save.Commands.Count; index++)
            {
                TimedRealtimeCommand timed = save.Commands[index];
                if (timed.Minute < run.GetSnapshot().Minute)
                {
                    throw new RealtimeCampaignPersistenceException(
                        $"commands[{index}] precedes the campaign clock.");
                }
                run.AdvanceTo(timed.Minute);
                RealtimeCommandResult result = run.ApplyCommand(
                    timed.Minute,
                    timed.Sequence,
                    timed.Command);
                if (!result.Accepted)
                {
                    throw new RealtimeCampaignPersistenceException(
                        $"commands[{index}] was rejected during replay: {result.Error}.");
                }
            }
            if (save.SavedMinute < run.GetSnapshot().Minute)
            {
                throw new RealtimeCampaignPersistenceException(
                    "savedMinute precedes the replayed campaign clock.");
            }
            run.AdvanceTo(save.SavedMinute);
            return run;
        }
        catch (RealtimeCampaignPersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new RealtimeCampaignPersistenceException(
                "The realtime timed command journal could not be replayed.",
                exception);
        }
    }

    private static RealtimeCampaignSave Convert(RawSave raw, JsonElement root)
    {
        RawTimedCommand[] rawCommands = raw.Commands ??
            throw new RealtimeCampaignPersistenceException("commands is null.");
        JsonElement.ArrayEnumerator commandJson = root.GetProperty("commands").EnumerateArray();
        var commands = new List<TimedRealtimeCommand>(rawCommands.Length);
        int index = 0;
        foreach (JsonElement timedElement in commandJson)
        {
            Require(timedElement.ValueKind == JsonValueKind.Object,
                $"commands[{index}] must be an object.");
            Require(timedElement.EnumerateObject().Select(item => item.Name)
                    .ToHashSet(StringComparer.Ordinal)
                    .SetEquals(["sequence", "minute", "command"]),
                $"commands[{index}] fields do not match a timed command.");
            RawTimedCommand timed = rawCommands[index] ??
                throw new RealtimeCampaignPersistenceException($"commands[{index}] is null.");
            JsonElement commandElement = timedElement.GetProperty("command");
            ValidateCommandJsonShape(commandElement, timed.Command.Kind, index);
            RawCommand item = timed.Command;
            var command = new RealtimeCommand(
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
            commands.Add(new TimedRealtimeCommand(timed.Sequence, timed.Minute, command));
            index++;
        }
        Require(index == rawCommands.Length,
            "The command JSON and deserialized command count differ.");
        RealtimeBaseStateSeed seed = ConvertSeed(raw.BaseStateSeed);
        return new RealtimeCampaignSave(
            raw.SchemaVersion,
            raw.TransformVersion,
            raw.CampaignId,
            raw.CampaignSha256,
            raw.WorldId,
            raw.WorldSha256,
            raw.AnchorMinute,
            seed,
            raw.BaseStateSeedSha256,
            raw.SavedMinute,
            commands);
    }

    private static void RequireRootShape(JsonElement root)
    {
        Require(root.ValueKind == JsonValueKind.Object,
            "The realtime save root must be an object.");
        HashSet<string> actual = root.EnumerateObject()
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals([
                "schemaVersion",
                "transformVersion",
                "campaignId",
                "campaignSha256",
                "worldId",
                "worldSha256",
                "anchorMinute",
                "baseStateSeed",
                "baseStateSeedSha256",
                "savedMinute",
                "commands",
            ]),
            "The realtime save root fields do not match save v4.");
        Require(root.GetProperty("commands").ValueKind == JsonValueKind.Array,
            "commands must be an array.");
        RequireSeedJsonShape(root.GetProperty("baseStateSeed"));
    }

    private static void ValidateCommandJsonShape(
        JsonElement element,
        RealtimeCommandKind kind,
        int index)
    {
        Require(element.ValueKind == JsonValueKind.Object,
            $"commands[{index}].command must be an object.");
        string[] expected = kind switch
        {
            RealtimeCommandKind.SetNodeDraft => ["kind", "firstId", "position"],
            RealtimeCommandKind.StartLineDraft =>
                ["kind", "firstId", "secondId", "thirdId"],
            RealtimeCommandKind.AddLinePoint => ["kind", "position"],
            RealtimeCommandKind.MoveLinePoint => ["kind", "position", "pointIndex"],
            RealtimeCommandKind.FinishLineDraft => ["kind", "firstId"],
            RealtimeCommandKind.SetPromiseDecision => ["kind", "promiseDecision"],
            RealtimeCommandKind.CancelNodeDraft or RealtimeCommandKind.OrderNode or
                RealtimeCommandKind.UndoLinePoint or
                RealtimeCommandKind.CancelLineDraft or RealtimeCommandKind.OrderLine =>
                ["kind"],
            _ => throw new RealtimeCampaignPersistenceException(
                $"commands[{index}].command.kind is unknown."),
        };
        HashSet<string> actual = element.EnumerateObject()
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals(expected),
            $"commands[{index}].command fields do not match {kind}.");
    }

    private static void ValidateCommand(RealtimeCommand command, int index)
    {
        ArgumentNullException.ThrowIfNull(command);
        Require(Enum.IsDefined(command.Kind), $"commands[{index}].command.kind is unknown.");
        bool noIds = command.FirstId is null && command.SecondId is null &&
            command.ThirdId is null;
        bool noPoint = command.Position is null && command.PointIndex is null;
        bool valid = command.Kind switch
        {
            RealtimeCommandKind.SetNodeDraft =>
                Text(command.FirstId) && command.SecondId is null && command.ThirdId is null &&
                command.Position.HasValue && command.PointIndex is null &&
                command.PromiseDecision is null,
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
                Text(command.FirstId) && command.SecondId is null && command.ThirdId is null &&
                noPoint && command.PromiseDecision is null,
            RealtimeCommandKind.SetPromiseDecision =>
                noIds && noPoint && command.PromiseDecision is
                    CommercialPromiseDecision.Keep or CommercialPromiseDecision.Defer,
            _ => false,
        };
        Require(valid, $"commands[{index}].command has an invalid field combination.");
    }

    private static RealtimeBaseStateSeed ConvertSeed(RawSeed raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        return new RealtimeBaseStateSeed(
            raw.SeedId,
            raw.StartMinute,
            raw.InitialCashUnit,
            raw.BaseNodeIds ?? throw new RealtimeCampaignPersistenceException(
                "baseStateSeed.baseNodeIds is null."),
            raw.BaseEdgeIds ?? throw new RealtimeCampaignPersistenceException(
                "baseStateSeed.baseEdgeIds is null."),
            (raw.ConstructedNodes ?? throw new RealtimeCampaignPersistenceException(
                "baseStateSeed.constructedNodes is null.")).Select(item =>
                new RealtimeSeedNode(
                    item.NodeId,
                    item.ClassId,
                    item.DisplayName,
                    item.XUnit,
                    item.YUnit,
                    item.Commissioned,
                    item.AuthoredFoundation)).ToArray(),
            (raw.ConstructedEdges ?? throw new RealtimeCampaignPersistenceException(
                "baseStateSeed.constructedEdges is null.")).Select(item =>
                new RealtimeSeedEdge(
                    item.EdgeId,
                    item.LineClassId,
                    item.FromNodeId,
                    item.ToNodeId,
                    item.Commissioned)).ToArray(),
            raw.CoolingAssetIds ?? throw new RealtimeCampaignPersistenceException(
                "baseStateSeed.coolingAssetIds is null."));
    }

    private static void ValidateSeed(RealtimeBaseStateSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        RequireText(seed.SeedId, "baseStateSeed.seedId");
        Require(seed.StartMinute >= 0, "baseStateSeed.startMinute must be nonnegative.");
        Require(seed.InitialCashUnit >= 0,
            "baseStateSeed.initialCashUnit must be nonnegative.");
        ValidateIdList(seed.BaseNodeIds, "baseStateSeed.baseNodeIds");
        ValidateIdList(seed.BaseEdgeIds, "baseStateSeed.baseEdgeIds");
        ValidateIdList(seed.CoolingAssetIds, "baseStateSeed.coolingAssetIds");
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < seed.ConstructedNodes.Count; index++)
        {
            RealtimeSeedNode item = seed.ConstructedNodes[index] ??
                throw new RealtimeCampaignPersistenceException(
                    $"baseStateSeed.constructedNodes[{index}] is null.");
            RequireText(item.NodeId, $"baseStateSeed.constructedNodes[{index}].nodeId");
            RequireText(item.ClassId, $"baseStateSeed.constructedNodes[{index}].classId");
            RequireText(item.DisplayName,
                $"baseStateSeed.constructedNodes[{index}].displayName");
            Require(nodeIds.Add(item.NodeId),
                "baseStateSeed.constructedNodes has duplicate node IDs.");
            Require(item.Commissioned && !item.AuthoredFoundation,
                "Immutable constructed seed nodes must be commissioned player assets.");
        }
        var edgeIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < seed.ConstructedEdges.Count; index++)
        {
            RealtimeSeedEdge item = seed.ConstructedEdges[index] ??
                throw new RealtimeCampaignPersistenceException(
                    $"baseStateSeed.constructedEdges[{index}] is null.");
            RequireText(item.EdgeId, $"baseStateSeed.constructedEdges[{index}].edgeId");
            RequireText(item.LineClassId,
                $"baseStateSeed.constructedEdges[{index}].lineClassId");
            RequireText(item.FromNodeId,
                $"baseStateSeed.constructedEdges[{index}].fromNodeId");
            RequireText(item.ToNodeId,
                $"baseStateSeed.constructedEdges[{index}].toNodeId");
            Require(edgeIds.Add(item.EdgeId),
                "baseStateSeed.constructedEdges has duplicate edge IDs.");
            Require(item.Commissioned,
                "Immutable constructed seed edges must be commissioned.");
        }
    }

    private static void ValidateIdList(IReadOnlyList<string> values, string path)
    {
        ArgumentNullException.ThrowIfNull(values);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? previous = null;
        foreach (string value in values)
        {
            RequireText(value, path);
            Require(seen.Add(value), $"{path} contains duplicate IDs.");
            Require(previous is null || string.CompareOrdinal(previous, value) < 0,
                $"{path} must use strict ordinal order.");
            previous = value;
        }
    }

    private static void RequireSeedJsonShape(JsonElement seed)
    {
        RequireObjectFields(seed, "baseStateSeed", [
            "seedId",
            "startMinute",
            "initialCashUnit",
            "baseNodeIds",
            "baseEdgeIds",
            "constructedNodes",
            "constructedEdges",
            "coolingAssetIds",
        ]);
        foreach (string arrayName in new[]
                 {
                     "baseNodeIds",
                     "baseEdgeIds",
                     "constructedNodes",
                     "constructedEdges",
                     "coolingAssetIds",
                 })
        {
            Require(seed.GetProperty(arrayName).ValueKind == JsonValueKind.Array,
                $"baseStateSeed.{arrayName} must be an array.");
        }
        int nodeIndex = 0;
        foreach (JsonElement node in seed.GetProperty("constructedNodes").EnumerateArray())
        {
            RequireObjectFields(node, $"baseStateSeed.constructedNodes[{nodeIndex++}]", [
                "nodeId",
                "classId",
                "displayName",
                "xUnit",
                "yUnit",
                "commissioned",
                "authoredFoundation",
            ]);
        }
        int edgeIndex = 0;
        foreach (JsonElement edge in seed.GetProperty("constructedEdges").EnumerateArray())
        {
            RequireObjectFields(edge, $"baseStateSeed.constructedEdges[{edgeIndex++}]", [
                "edgeId",
                "lineClassId",
                "fromNodeId",
                "toNodeId",
                "commissioned",
            ]);
        }
    }

    private static void RequireObjectFields(
        JsonElement element,
        string path,
        IReadOnlyList<string> expected)
    {
        Require(element.ValueKind == JsonValueKind.Object, $"{path} must be an object.");
        HashSet<string> actual = element.EnumerateObject()
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals(expected), $"{path} has unexpected or missing fields.");
    }

    private static byte[] CanonicalSeedBytes(RealtimeBaseStateSeed seed) =>
        JsonSerializer.SerializeToUtf8Bytes(seed, CanonicalSeedOptions);

    private static string CanonicalSeedSha256(RealtimeBaseStateSeed seed) =>
        System.Convert.ToHexString(SHA256.HashData(CanonicalSeedBytes(seed))).ToLowerInvariant();

    private static void RejectDuplicates(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new RealtimeCampaignPersistenceException(
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

    private static void RequireText(string value, string path) =>
        Require(Text(value), $"{path} must be nonblank and trimmed.");

    private static void RequireSha256(string value, string path) =>
        Require(value is not null && value.Length == 64 && value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            $"{path} must be a lowercase SHA-256 value.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new RealtimeCampaignPersistenceException(message);
        }
    }

    private sealed class RawSave
    {
        [JsonRequired] public string SchemaVersion { get; init; } = null!;
        [JsonRequired] public string TransformVersion { get; init; } = null!;
        [JsonRequired] public string CampaignId { get; init; } = null!;
        [JsonRequired] public string CampaignSha256 { get; init; } = null!;
        [JsonRequired] public string WorldId { get; init; } = null!;
        [JsonRequired] public string WorldSha256 { get; init; } = null!;
        [JsonRequired] public long AnchorMinute { get; init; }
        [JsonRequired] public RawSeed BaseStateSeed { get; init; } = null!;
        [JsonRequired] public string BaseStateSeedSha256 { get; init; } = null!;
        [JsonRequired] public long SavedMinute { get; init; }
        [JsonRequired] public RawTimedCommand[] Commands { get; init; } = null!;
    }

    private sealed class RawSeed
    {
        [JsonRequired] public string SeedId { get; init; } = null!;
        [JsonRequired] public int StartMinute { get; init; }
        [JsonRequired] public long InitialCashUnit { get; init; }
        [JsonRequired] public string[] BaseNodeIds { get; init; } = null!;
        [JsonRequired] public string[] BaseEdgeIds { get; init; } = null!;
        [JsonRequired] public RawSeedNode[] ConstructedNodes { get; init; } = null!;
        [JsonRequired] public RawSeedEdge[] ConstructedEdges { get; init; } = null!;
        [JsonRequired] public string[] CoolingAssetIds { get; init; } = null!;
    }

    private sealed class RawSeedNode
    {
        [JsonRequired] public string NodeId { get; init; } = null!;
        [JsonRequired] public string ClassId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public int XUnit { get; init; }
        [JsonRequired] public int YUnit { get; init; }
        [JsonRequired] public bool Commissioned { get; init; }
        [JsonRequired] public bool AuthoredFoundation { get; init; }
    }

    private sealed class RawSeedEdge
    {
        [JsonRequired] public string EdgeId { get; init; } = null!;
        [JsonRequired] public string LineClassId { get; init; } = null!;
        [JsonRequired] public string FromNodeId { get; init; } = null!;
        [JsonRequired] public string ToNodeId { get; init; } = null!;
        [JsonRequired] public bool Commissioned { get; init; }
    }

    private sealed class RawTimedCommand
    {
        [JsonRequired] public long Sequence { get; init; }
        [JsonRequired] public long Minute { get; init; }
        [JsonRequired] public RawCommand Command { get; init; } = null!;
    }

    private sealed class RawCommand
    {
        [JsonRequired] public RealtimeCommandKind Kind { get; init; }
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
