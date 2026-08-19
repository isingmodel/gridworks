using System.Text.Json;
using System.Text.Json.Serialization;
using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

public static class RealtimeCampaignLoader
{
    public const string SupportedSchemaVersion = "gridworks.realtime.campaign.v3";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
        },
    };

    public static RealtimeCampaignDefinition Load(
        string json,
        CommercialCampaignDefinition baseCampaign,
        RealtimeWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Load(System.Text.Encoding.UTF8.GetBytes(json), baseCampaign, world);
    }

    public static RealtimeCampaignDefinition Load(
        ReadOnlySpan<byte> utf8Json,
        CommercialCampaignDefinition baseCampaign,
        RealtimeWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(baseCampaign);
        ArgumentNullException.ThrowIfNull(world);
        try
        {
            byte[] bytes = utf8Json.ToArray();
            using JsonDocument document = JsonDocument.Parse(bytes);
            RejectDuplicates(document.RootElement, "$");
            RequireRootShape(document.RootElement);
            RawCampaign raw = JsonSerializer.Deserialize<RawCampaign>(bytes, Options)
                ?? throw new RealtimeCampaignValidationException(
                    "The realtime campaign document is empty.");
            RealtimeCampaignDefinition result = Convert(raw, baseCampaign);
            Validate(result, baseCampaign, world);
            return result;
        }
        catch (RealtimeCampaignValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or InvalidOperationException or
            NullReferenceException or OverflowException or CommercialCampaignValidationException)
        {
            throw new RealtimeCampaignValidationException(
                "The realtime campaign document is invalid.",
                exception);
        }
    }

    public static void Validate(
        RealtimeCampaignDefinition definition,
        CommercialCampaignDefinition baseCampaign,
        RealtimeWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(baseCampaign);
        ArgumentNullException.ThrowIfNull(world);
        Require(definition.SchemaVersion == SupportedSchemaVersion,
            $"schemaVersion must equal '{SupportedSchemaVersion}'.");
        Require(definition.CampaignId == baseCampaign.CampaignId,
            "campaignId must match the base commercial campaign.");
        Require(ReferenceEquals(definition.Content, baseCampaign),
            "The realtime campaign must retain its validated base campaign authority.");
        ValidateInitialSeed(definition.InitialSeed, world.Network);
        Require(definition.Chapters.Count > 0 &&
                definition.Chapters.Count <=
                    CommercialCampaignLoader.CanonicalChapterIds.Count,
            "The realtime campaign must schedule a nonempty canonical chapter prefix.");

        long authoredCash = definition.InitialSeed.InitialCashUnit;
        for (int chapterIndex = 0; chapterIndex < baseCampaign.Chapters.Count; chapterIndex++)
        {
            try
            {
                authoredCash = checked(
                    authoredCash + baseCampaign.Chapters[chapterIndex].BudgetGrantCashUnit);
            }
            catch (OverflowException exception)
            {
                throw new RealtimeCampaignValidationException(
                    $"initialSeed cash plus grants through base chapters[{chapterIndex}] overflows Int64.",
                    exception);
            }
        }

        for (int chapterIndex = 0; chapterIndex < definition.Chapters.Count; chapterIndex++)
        {
            RealtimeChapterDefinition chapter = definition.Chapters[chapterIndex];
            CommercialCampaignChapterDefinition content = baseCampaign.Chapters[chapterIndex];
            Require(ReferenceEquals(chapter.Content, content) || chapter.Content == content,
                $"chapters[{chapterIndex}] content does not match its base chapter.");
            Require(chapter.Content.ChapterId ==
                    CommercialCampaignLoader.CanonicalChapterIds[chapterIndex],
                $"chapters[{chapterIndex}] is not canonical.");
            Require(chapter.PreparationMinutes >= 0,
                $"chapters[{chapterIndex}].preparationMinutes must be nonnegative.");
            Require(chapter.ScheduledEvents.Count > 0,
                $"chapters[{chapterIndex}] must schedule at least one event.");

            int previousStart = -1;
            int previousPriority = -1;
            string? previousEventId = null;
            var seenEventIds = new HashSet<string>(StringComparer.Ordinal);
            for (int eventIndex = 0; eventIndex < chapter.ScheduledEvents.Count; eventIndex++)
            {
                RealtimeScheduledEventDefinition scheduled = chapter.ScheduledEvents[eventIndex];
                CommercialOperatingPhaseDefinition profile = scheduled.OperatingProfile;
                string path = $"chapters[{chapterIndex}].scheduledEvents[{eventIndex}]";
                Require(scheduled.EventId == profile.PhaseId,
                    $"{path}.eventId must equal its source phase ID.");
                Require(seenEventIds.Add(scheduled.EventId),
                    $"{path}.eventId is duplicated.");
                Require(scheduled.Priority >= 0,
                    $"{path}.priority must be nonnegative.");
                Require(scheduled.StartOffsetMinutes > previousStart ||
                        scheduled.StartOffsetMinutes == previousStart &&
                        (scheduled.Priority > previousPriority ||
                         scheduled.Priority == previousPriority &&
                         previousEventId is not null &&
                         string.CompareOrdinal(previousEventId, scheduled.EventId) < 0),
                    $"{path} must follow start minute, priority, then stable event-ID order.");
                previousStart = scheduled.StartOffsetMinutes;
                previousPriority = scheduled.Priority;
                previousEventId = scheduled.EventId;
                ValidateProfile(profile, world.Network, definition.InitialSeed, path);
                Require(scheduled.StartOffsetMinutes >= chapter.PreparationMinutes,
                    $"{path}.startOffsetMinutes must not precede preparation.");
                Require(scheduled.DurationMinutes > 0,
                    $"{path}.durationMinutes must be positive.");
                Require(scheduled.ForecastLeadMinutes >= 0 &&
                        scheduled.ForecastLeadMinutes <= scheduled.StartOffsetMinutes,
                    $"{path}.forecastLeadMinutes must fit before the event.");
            }

            if (content.CityPromise is null)
            {
                Require(chapter.PromiseDecisionDeadlineOffsetMinutes is null,
                    $"chapters[{chapterIndex}] cannot have a promise deadline.");
            }
            else
            {
                RealtimeScheduledEventDefinition firstPromiseEvent =
                    chapter.ScheduledEvents.FirstOrDefault(item =>
                        item.OperatingProfile.Loads.Any(load =>
                            load.Obligation == CommercialObligationKind.CityPromise))
                    ?? throw new RealtimeCampaignValidationException(
                        $"chapters[{chapterIndex}] has a promise without a promise event.");
                Require(chapter.PromiseDecisionDeadlineOffsetMinutes is int deadline &&
                        deadline >= 0 && deadline <= firstPromiseEvent.StartOffsetMinutes,
                    $"chapters[{chapterIndex}] needs a deadline before its first promise event.");
            }
        }
    }

    private static RealtimeCampaignDefinition Convert(
        RawCampaign raw,
        CommercialCampaignDefinition baseCampaign)
    {
        if (raw.Chapters.Length == 0 || raw.Chapters.Length > baseCampaign.Chapters.Count)
        {
            throw new RealtimeCampaignValidationException(
                "The realtime chapter count must be a nonempty base campaign prefix.");
        }
        RealtimeChapterDefinition[] chapters = raw.Chapters.Select((item, chapterIndex) =>
        {
            CommercialCampaignChapterDefinition content = baseCampaign.Chapters[chapterIndex];
            RealtimeScheduledEventDefinition[] events = RealtimeEventOrdering.BySchedule(
                item.ScheduledEvents.Select(
                scheduled => new RealtimeScheduledEventDefinition(
                    scheduled.EventId,
                    scheduled.Priority,
                    scheduled.StartOffsetMinutes,
                    scheduled.DurationMinutes,
                    scheduled.ForecastLeadMinutes,
                    Profile(scheduled))))
                .ToArray();
            if (!string.Equals(item.ChapterId, content.ChapterId, StringComparison.Ordinal))
            {
                throw new RealtimeCampaignValidationException(
                    $"chapters[{chapterIndex}].chapterId does not match the base campaign.");
            }
            return new RealtimeChapterDefinition(
                content,
                item.PreparationMinutes,
                item.PromiseDecisionDeadlineOffsetMinutes,
                events);
        }).ToArray();
        return new RealtimeCampaignDefinition(
            raw.SchemaVersion,
            raw.CampaignId,
            baseCampaign,
            Seed(raw.InitialSeed),
            chapters);
    }

    private static CommercialCoreSeedDefinition Seed(RawSeed raw) => new(
        raw.SeedId,
        raw.StartMinute,
        raw.InitialCashUnit,
        raw.BaseNodeIds,
        raw.BaseEdgeIds,
        raw.ConstructedNodes.Select(item => new SpatialNodeDefinition(
            item.NodeId,
            item.ClassId,
            item.DisplayName,
            new MapPoint(item.Position.XUnit, item.Position.YUnit),
            item.Commissioned,
            item.AuthoredFoundation)).ToArray(),
        raw.ConstructedEdges.Select(item => new SpatialEdgeDefinition(
            item.EdgeId,
            item.LineClassId,
            item.FromNodeId,
            item.ToNodeId,
            item.Commissioned)).ToArray(),
        raw.CoolingAssetIds);

    private static CommercialOperatingPhaseDefinition Profile(
        RawScheduledEvent scheduled) => new(
        scheduled.EventId,
        scheduled.DisplayName,
        scheduled.ThermalPolicy,
        null,
        scheduled.Loads.Select(item => new CommercialLoadBundleDefinition(
            item.LoadId,
            item.DemandKw,
            item.Obligation)).ToArray(),
        scheduled.UnavailableNodeIds,
        scheduled.UnavailableEdgeIds,
        scheduled.ActiveRiskAreaIds,
        scheduled.ThermalLimitOverrides.Select(item => new ThermalLimitOverride(
            item.AssetKind,
            item.ClassId,
            item.ContinuousKw,
            item.EmergencyKw)).ToArray());

    private static void ValidateInitialSeed(
        CommercialCoreSeedDefinition seed,
        CommercialWorldDefinition world)
    {
        RequireText(seed.SeedId, "initialSeed.seedId");
        Require(seed.StartMinute >= 0, "initialSeed.startMinute must be nonnegative.");
        Require(seed.InitialCashUnit >= 0,
            "initialSeed.initialCashUnit must be nonnegative.");
        RequireUniqueOrdered(seed.BaseNodeIds, "initialSeed.baseNodeIds");
        RequireUniqueOrdered(seed.BaseEdgeIds, "initialSeed.baseEdgeIds");
        RequireUniqueOrdered(seed.ConstructedNodes.Select(item => item.NodeId),
            "initialSeed.constructedNodes");
        RequireUniqueOrdered(seed.ConstructedEdges.Select(item => item.EdgeId),
            "initialSeed.constructedEdges");
        RequireUniqueOrdered(seed.CoolingAssetIds, "initialSeed.coolingAssetIds");
        HashSet<string> knownBaseNodes = world.Nodes.Select(item => item.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> knownBaseEdges = world.Edges.Select(item => item.EdgeId)
            .ToHashSet(StringComparer.Ordinal);
        Require(seed.BaseNodeIds.All(knownBaseNodes.Contains),
            "initialSeed.baseNodeIds references an unknown base node.");
        Require(seed.BaseEdgeIds.All(knownBaseEdges.Contains),
            "initialSeed.baseEdgeIds references an unknown base edge.");
        Dictionary<string, CommercialNodeClassDefinition> nodeClasses =
            world.NodeClasses.ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        Require(seed.ConstructedNodes.All(item =>
                item.Commissioned && !item.AuthoredFoundation &&
                nodeClasses.TryGetValue(item.ClassId, out var nodeClass) &&
                nodeClass.Kind is SpatialNodeKind.Pole or SpatialNodeKind.Substation),
            "initialSeed.constructedNodes must be commissioned player assets.");
        Require(seed.ConstructedEdges.All(item => item.Commissioned),
            "initialSeed.constructedEdges must be commissioned.");
        CommercialWorldDefinition seedWorld;
        try
        {
            seedWorld = CommercialCampaignLoader.BuildInitialWorld(world, seed);
            CommercialWorldLoader.Validate(seedWorld);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
            CommercialWorldValidationException)
        {
            throw new RealtimeCampaignValidationException(
                "initialSeed does not form a valid realtime world.",
                exception);
        }
        HashSet<string> thermalIds = seedWorld.Edges.Select(item => item.EdgeId)
            .Concat(seedWorld.Nodes.Where(item => seedWorld.NodeClasses.Single(nodeClass =>
                    nodeClass.ClassId == item.ClassId).ThermalLimit is not null)
                .Select(item => item.NodeId))
            .ToHashSet(StringComparer.Ordinal);
        Require(seed.CoolingAssetIds.All(thermalIds.Contains),
            "initialSeed.coolingAssetIds references an unknown thermal asset.");
    }

    private static void ValidateProfile(
        CommercialOperatingPhaseDefinition profile,
        CommercialWorldDefinition world,
        CommercialCoreSeedDefinition seed,
        string path)
    {
        RequireText(profile.PhaseId, $"{path}.eventId");
        RequireText(profile.DisplayName, $"{path}.displayName");
        Require(Enum.IsDefined(profile.ThermalPolicy),
            $"{path}.thermalPolicy is unknown.");
        Require(profile.Loads.Count > 0, $"{path}.loads must not be empty.");
        HashSet<string> knownLoads = world.Loads.Select(item => item.LoadId)
            .ToHashSet(StringComparer.Ordinal);
        var seenLoads = new HashSet<string>(StringComparer.Ordinal);
        foreach (CommercialLoadBundleDefinition load in profile.Loads)
        {
            Require(knownLoads.Contains(load.LoadId),
                $"{path}.loads references unknown load '{load.LoadId}'.");
            Require(seenLoads.Add(load.LoadId),
                $"{path}.loads duplicates '{load.LoadId}'.");
            Require(load.DemandKw > 0, $"{path}.loads demand must be positive.");
            Require(Enum.IsDefined(load.Obligation),
                $"{path}.loads obligation is unknown.");
        }
        CommercialWorldDefinition seedWorld =
            CommercialCampaignLoader.BuildInitialWorld(world, seed);
        HashSet<string> nodeIds = seedWorld.Nodes.Select(item => item.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> edgeIds = seedWorld.Edges.Select(item => item.EdgeId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> riskIds = seedWorld.RiskAreas.Select(item => item.RiskAreaId)
            .ToHashSet(StringComparer.Ordinal);
        RequireUniqueKnown(profile.UnavailableNodeIds, nodeIds,
            $"{path}.unavailableNodeIds");
        RequireUniqueKnown(profile.UnavailableEdgeIds, edgeIds,
            $"{path}.unavailableEdgeIds");
        RequireUniqueKnown(profile.ActiveRiskAreaIds, riskIds,
            $"{path}.activeRiskAreaIds");

        Dictionary<(ThermalAssetKind Kind, string ClassId), ThermalLimit> limits =
            world.NodeClasses.Where(item => item.ThermalLimit is not null)
                .ToDictionary(
                    item => (ThermalAssetKind.Node, item.ClassId),
                    item => item.ThermalLimit!)
                .Concat(world.LineClasses.ToDictionary(
                    item => (ThermalAssetKind.Edge, item.ClassId),
                    item => item.ThermalLimit))
                .ToDictionary(item => item.Key, item => item.Value);
        var seenOverrides = new HashSet<(ThermalAssetKind Kind, string ClassId)>();
        foreach (ThermalLimitOverride item in profile.ThermalLimitOverrides)
        {
            var key = (item.AssetKind, item.ClassId);
            Require(seenOverrides.Add(key), $"{path}.thermalLimitOverrides duplicates a class.");
            Require(limits.TryGetValue(key, out ThermalLimit? limit),
                $"{path}.thermalLimitOverrides references an unknown thermal class.");
            Require(item.ContinuousKw > 0 && item.EmergencyKw >= item.ContinuousKw &&
                    item.ContinuousKw <= limit!.ContinuousKw &&
                    item.EmergencyKw <= limit.EmergencyKw,
                $"{path}.thermalLimitOverrides has invalid limits.");
        }
    }

    private static void RequireUniqueKnown(
        IReadOnlyList<string> values,
        IReadOnlySet<string> known,
        string path)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            Require(known.Contains(value), $"{path} references unknown ID '{value}'.");
            Require(seen.Add(value), $"{path} duplicates '{value}'.");
        }
    }

    private static void RequireUniqueOrdered(IEnumerable<string> values, string path)
    {
        string[] items = values.ToArray();
        Require(items.All(item => !string.IsNullOrWhiteSpace(item) && item == item.Trim()),
            $"{path} contains an invalid ID.");
        Require(items.Distinct(StringComparer.Ordinal).Count() == items.Length,
            $"{path} contains a duplicate ID.");
        Require(items.SequenceEqual(items.OrderBy(item => item, StringComparer.Ordinal),
                StringComparer.Ordinal),
            $"{path} must use stable ordinal ID order.");
    }

    private static void RequireText(string value, string path) =>
        Require(!string.IsNullOrWhiteSpace(value) && value == value.Trim(),
            $"{path} must be nonblank and trimmed.");

    private static void RequireRootShape(JsonElement root)
    {
        Require(root.ValueKind == JsonValueKind.Object,
            "The realtime campaign root must be an object.");
        string[] expected = ["schemaVersion", "campaignId", "initialSeed", "chapters"];
        HashSet<string> actual = root.EnumerateObject()
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals(expected),
            "The realtime campaign root fields do not match campaign v3.");
        Require(root.GetProperty("chapters").ValueKind == JsonValueKind.Array,
            "chapters must be an array.");
        Require(root.GetProperty("initialSeed").ValueKind == JsonValueKind.Object,
            "initialSeed must be an object.");
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
                    throw new RealtimeCampaignValidationException(
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new RealtimeCampaignValidationException(message);
        }
    }

    private sealed class RawCampaign
    {
        [JsonRequired] public string SchemaVersion { get; init; } = null!;
        [JsonRequired] public string CampaignId { get; init; } = null!;
        [JsonRequired] public RawSeed InitialSeed { get; init; } = null!;
        [JsonRequired] public RawChapter[] Chapters { get; init; } = null!;
    }

    private sealed class RawSeed
    {
        [JsonRequired] public string SeedId { get; init; } = null!;
        [JsonRequired] public int StartMinute { get; init; }
        [JsonRequired] public long InitialCashUnit { get; init; }
        [JsonRequired] public string[] BaseNodeIds { get; init; } = null!;
        [JsonRequired] public string[] BaseEdgeIds { get; init; } = null!;
        [JsonRequired] public RawNode[] ConstructedNodes { get; init; } = null!;
        [JsonRequired] public RawEdge[] ConstructedEdges { get; init; } = null!;
        [JsonRequired] public string[] CoolingAssetIds { get; init; } = null!;
    }

    private sealed class RawNode
    {
        [JsonRequired] public string NodeId { get; init; } = null!;
        [JsonRequired] public string ClassId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public RawPoint Position { get; init; } = null!;
        [JsonRequired] public bool Commissioned { get; init; }
        [JsonRequired] public bool AuthoredFoundation { get; init; }
    }

    private sealed class RawPoint
    {
        [JsonRequired] public int XUnit { get; init; }
        [JsonRequired] public int YUnit { get; init; }
    }

    private sealed class RawEdge
    {
        [JsonRequired] public string EdgeId { get; init; } = null!;
        [JsonRequired] public string LineClassId { get; init; } = null!;
        [JsonRequired] public string FromNodeId { get; init; } = null!;
        [JsonRequired] public string ToNodeId { get; init; } = null!;
        [JsonRequired] public bool Commissioned { get; init; }
    }

    private sealed class RawChapter
    {
        [JsonRequired] public string ChapterId { get; init; } = null!;
        [JsonRequired] public int PreparationMinutes { get; init; }
        [JsonRequired] public int? PromiseDecisionDeadlineOffsetMinutes { get; init; }
        [JsonRequired] public RawScheduledEvent[] ScheduledEvents { get; init; } = null!;
    }

    private sealed class RawScheduledEvent
    {
        [JsonRequired] public string EventId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public int Priority { get; init; }
        [JsonRequired] public int StartOffsetMinutes { get; init; }
        [JsonRequired] public int DurationMinutes { get; init; }
        [JsonRequired] public int ForecastLeadMinutes { get; init; }
        [JsonRequired] public CommercialPhaseThermalPolicy ThermalPolicy { get; init; }
        [JsonRequired] public RawLoad[] Loads { get; init; } = null!;
        [JsonRequired] public string[] UnavailableNodeIds { get; init; } = null!;
        [JsonRequired] public string[] UnavailableEdgeIds { get; init; } = null!;
        [JsonRequired] public string[] ActiveRiskAreaIds { get; init; } = null!;
        [JsonRequired] public RawOverride[] ThermalLimitOverrides { get; init; } = null!;
    }

    private sealed class RawLoad
    {
        [JsonRequired] public string LoadId { get; init; } = null!;
        [JsonRequired] public long DemandKw { get; init; }
        [JsonRequired] public CommercialObligationKind Obligation { get; init; }
    }

    private sealed class RawOverride
    {
        [JsonRequired] public ThermalAssetKind AssetKind { get; init; }
        [JsonRequired] public string ClassId { get; init; } = null!;
        [JsonRequired] public long ContinuousKw { get; init; }
        [JsonRequired] public long EmergencyKw { get; init; }
    }
}
