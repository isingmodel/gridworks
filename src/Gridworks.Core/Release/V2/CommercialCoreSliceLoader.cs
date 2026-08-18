using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Core.Release.V2;

public static class CommercialCoreSliceLoader
{
    public const string SupportedSchemaVersion = "gridworks.commercial.core-slice.v1";

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

    public static CommercialCoreSliceDefinition Load(
        string json,
        CommercialWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Load(System.Text.Encoding.UTF8.GetBytes(json), world);
    }

    public static CommercialCoreSliceDefinition Load(
        ReadOnlySpan<byte> utf8Json,
        CommercialWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(world);
        try
        {
            byte[] bytes = utf8Json.ToArray();
            using JsonDocument document = JsonDocument.Parse(bytes);
            RejectDuplicates(document.RootElement, "$");
            RawSlice raw = JsonSerializer.Deserialize<RawSlice>(bytes, Options)
                ?? throw new CommercialCoreSliceValidationException("Commercial core slice is empty.");
            CommercialCoreSliceDefinition result = Convert(raw);
            Validate(result, world);
            return result;
        }
        catch (CommercialCoreSliceValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or InvalidOperationException or
            NullReferenceException or OverflowException or SpatialWorldValidationException or
            CommercialWorldValidationException)
        {
            throw new CommercialCoreSliceValidationException(
                "Commercial core slice contains an invalid value.",
                exception);
        }
    }

    public static void Validate(
        CommercialCoreSliceDefinition slice,
        CommercialWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(slice);
        ArgumentNullException.ThrowIfNull(world);
        CommercialWorldLoader.Validate(world);
        Require(slice.SchemaVersion == SupportedSchemaVersion,
            $"schemaVersion must equal '{SupportedSchemaVersion}'.");
        RequireId(slice.SliceId, "sliceId");
        RequireText(slice.DisplayName, "displayName");
        CommercialCoreSegmentDefinition prelude = slice.Prelude ??
            throw new CommercialCoreSliceValidationException("prelude is required.");
        CommercialCoreSegmentDefinition main = slice.Main ??
            throw new CommercialCoreSliceValidationException("main is required.");
        Require(prelude.SegmentId != main.SegmentId,
            "Prelude and main segment IDs must differ.");
        Require(prelude.Seed.SeedId != main.Seed.SeedId,
            "Prelude and main seed IDs must differ.");
        Require(prelude.Chapter.ChapterId != main.Chapter.ChapterId,
            "Prelude and main chapter IDs must differ.");

        ValidateSegment(prelude, world, expectsPromise: false, "prelude");
        ValidateSegment(main, world, expectsPromise: true, "main");
    }

    public static CommercialWorldDefinition BuildSeedWorld(
        CommercialWorldDefinition world,
        CommercialCoreSeedDefinition seed)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(seed);
        return world with
        {
            InitialCashUnit = seed.InitialCashUnit,
            Nodes = world.Nodes.Where(node => seed.BaseNodeIds.Contains(
                    node.NodeId,
                    StringComparer.Ordinal))
                .Concat(seed.ConstructedNodes)
                .ToArray(),
            Edges = world.Edges.Where(edge => seed.BaseEdgeIds.Contains(
                    edge.EdgeId,
                    StringComparer.Ordinal))
                .Concat(seed.ConstructedEdges)
                .ToArray(),
        };
    }

    private static void ValidateSegment(
        CommercialCoreSegmentDefinition segment,
        CommercialWorldDefinition world,
        bool expectsPromise,
        string path)
    {
        RequireId(segment.SegmentId, $"{path}.segmentId");
        CommercialCoreSeedDefinition seed = segment.Seed ??
            throw new CommercialCoreSliceValidationException($"{path}.seed is required.");
        CommercialCoreChapterDefinition chapter = segment.Chapter ??
            throw new CommercialCoreSliceValidationException($"{path}.chapter is required.");
        CommercialWorldDefinition seedWorld = ValidateSeed(seed, world, $"{path}.seed");
        ValidateChapter(chapter, seedWorld, expectsPromise, $"{path}.chapter");
    }

    private static CommercialWorldDefinition ValidateSeed(
        CommercialCoreSeedDefinition seed,
        CommercialWorldDefinition world,
        string path)
    {
        RequireId(seed.SeedId, $"{path}.seedId");
        Require(seed.StartMinute >= 0, $"{path}.startMinute must be nonnegative.");
        Require(seed.InitialCashUnit >= 0, $"{path}.initialCashUnit must be nonnegative.");

        HashSet<string> knownBaseNodes = world.Nodes.Select(item => item.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> knownBaseEdges = world.Edges.Select(item => item.EdgeId)
            .ToHashSet(StringComparer.Ordinal);
        ValidateUniqueIds(seed.BaseNodeIds, $"{path}.baseNodeIds", knownBaseNodes);
        ValidateUniqueIds(seed.BaseEdgeIds, $"{path}.baseEdgeIds", knownBaseEdges);
        Require(seed.BaseNodeIds.SequenceEqual(
                seed.BaseNodeIds.OrderBy(item => item, StringComparer.Ordinal),
                StringComparer.Ordinal),
            $"{path}.baseNodeIds must use ordinal order.");
        Require(seed.BaseEdgeIds.SequenceEqual(
                seed.BaseEdgeIds.OrderBy(item => item, StringComparer.Ordinal),
                StringComparer.Ordinal),
            $"{path}.baseEdgeIds must use ordinal order.");
        foreach (string terminalNodeId in world.Sources.Select(item => item.NodeId)
                     .Concat(world.Loads.Select(item => item.NodeId)))
        {
            Require(seed.BaseNodeIds.Contains(terminalNodeId, StringComparer.Ordinal),
                $"{path}.baseNodeIds must retain terminal '{terminalNodeId}'.");
        }
        HashSet<string> retainedNodes = seed.BaseNodeIds.ToHashSet(StringComparer.Ordinal);
        foreach (SpatialEdgeDefinition edge in world.Edges.Where(item =>
                     seed.BaseEdgeIds.Contains(item.EdgeId, StringComparer.Ordinal)))
        {
            Require(retainedNodes.Contains(edge.FromNodeId) && retainedNodes.Contains(edge.ToNodeId),
                $"{path}.baseEdgeIds retains an edge without both endpoints.");
        }

        Dictionary<string, CommercialNodeClassDefinition> nodeClasses = world.NodeClasses
            .ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        foreach (SpatialNodeDefinition node in seed.ConstructedNodes)
        {
            Require(node.Commissioned, $"{path}.constructedNodes must be commissioned.");
            Require(!node.AuthoredFoundation,
                $"{path}.constructedNodes cannot claim authored foundations.");
            Require(nodeClasses.TryGetValue(node.ClassId, out CommercialNodeClassDefinition? nodeClass) &&
                nodeClass.Kind is SpatialNodeKind.Pole or SpatialNodeKind.Substation,
                $"{path}.constructedNodes may contain only player pole/substation classes.");
        }
        foreach (SpatialEdgeDefinition edge in seed.ConstructedEdges)
        {
            Require(edge.Commissioned, $"{path}.constructedEdges must be commissioned.");
        }

        CommercialWorldDefinition seedWorld = BuildSeedWorld(world, seed);
        CommercialWorldLoader.Validate(seedWorld);
        Dictionary<string, SpatialNodeDefinition> nodes = seedWorld.Nodes.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        HashSet<string> thermalAssetIds = seedWorld.Edges.Select(item => item.EdgeId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (SpatialNodeDefinition node in seedWorld.Nodes)
        {
            if (nodeClasses[node.ClassId].Kind is SpatialNodeKind.Pole or SpatialNodeKind.Substation)
            {
                thermalAssetIds.Add(node.NodeId);
            }
        }
        ValidateUniqueIds(seed.CoolingAssetIds, $"{path}.coolingAssetIds", thermalAssetIds);
        Require(seed.CoolingAssetIds.SequenceEqual(
                seed.CoolingAssetIds.OrderBy(item => item, StringComparer.Ordinal),
                StringComparer.Ordinal),
            $"{path}.coolingAssetIds must use ordinal order.");
        _ = nodes;
        return seedWorld;
    }

    private static void ValidateChapter(
        CommercialCoreChapterDefinition chapter,
        CommercialWorldDefinition world,
        bool expectsPromise,
        string path)
    {
        RequireId(chapter.ChapterId, $"{path}.chapterId");
        RequireText(chapter.DisplayName, $"{path}.displayName");
        ValidateStory(chapter.Briefing, $"{path}.briefing");
        RequireText(chapter.Objective, $"{path}.objective");
        Require(chapter.BudgetGrantCashUnit >= 0,
            $"{path}.budgetGrantCashUnit must be nonnegative.");
        _ = checked(world.InitialCashUnit + chapter.BudgetGrantCashUnit);
        Require((chapter.CityPromise is not null) == expectsPromise,
            expectsPromise
                ? $"{path} requires exactly one cityPromise."
                : $"{path} cannot contain a cityPromise.");

        HashSet<string> loadIds = world.Loads.Select(item => item.LoadId)
            .ToHashSet(StringComparer.Ordinal);
        if (chapter.CityPromise is CommercialCityPromiseDefinition promise)
        {
            RequireId(promise.PromiseId, $"{path}.cityPromise.promiseId");
            RequireText(promise.DisplayName, $"{path}.cityPromise.displayName");
            RequireId(promise.LoadId, $"{path}.cityPromise.loadId");
            Require(loadIds.Contains(promise.LoadId),
                $"{path}.cityPromise references unknown load '{promise.LoadId}'.");
            RequireText(promise.KeepLabel, $"{path}.cityPromise.keepLabel");
            RequireText(promise.DeferLabel, $"{path}.cityPromise.deferLabel");
        }

        Require(chapter.OperatingPhases.Count is >= 1 and <= 3,
            $"{path}.operatingPhases must contain 1..3 items.");
        Require(chapter.DecisionWindows.Count is >= 1 and <= 3,
            $"{path}.decisionWindows must contain 1..3 items.");
        Require(chapter.DecisionWindows.Count <= chapter.OperatingPhases.Count,
            $"{path} cannot have more windows than phases.");

        var phaseIds = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < chapter.OperatingPhases.Count; index++)
        {
            CommercialOperatingPhaseDefinition phase = chapter.OperatingPhases[index];
            RequireId(phase.PhaseId, $"{path}.operatingPhases[{index}].phaseId");
            Require(phaseIds.TryAdd(phase.PhaseId, index),
                $"{path} duplicates phase ID '{phase.PhaseId}'.");
            ValidatePhase(phase, chapter.CityPromise, world,
                $"{path}.operatingPhases[{index}]");
        }
        Require(chapter.OperatingPhases.All(phase => phase.Loads.Any(
                load => load.Obligation == CommercialObligationKind.SafetyDuty)),
            $"{path} requires at least one safety duty in every operating phase.");
        Require(!expectsPromise || chapter.OperatingPhases.Any(phase => phase.Loads.Any(
                load => load.Obligation == CommercialObligationKind.CityPromise)),
            $"{path} must use its city promise in at least one operating phase.");

        var windowIds = new HashSet<string>(StringComparer.Ordinal);
        int previousPhaseIndex = -1;
        for (int index = 0; index < chapter.DecisionWindows.Count; index++)
        {
            CommercialDecisionWindowDefinition window = chapter.DecisionWindows[index];
            RequireId(window.WindowId, $"{path}.decisionWindows[{index}].windowId");
            Require(windowIds.Add(window.WindowId),
                $"{path} duplicates window ID '{window.WindowId}'.");
            RequireId(window.BeforePhaseId,
                $"{path}.decisionWindows[{index}].beforePhaseId");
            Require(phaseIds.TryGetValue(window.BeforePhaseId, out int phaseIndex),
                $"{path} window '{window.WindowId}' references unknown phase '{window.BeforePhaseId}'.");
            Require(phaseIndex > previousPhaseIndex,
                $"{path}.decisionWindows must follow phase order.");
            Require(index > 0 || phaseIndex == 0,
                $"{path}.decisionWindows must start before the first phase.");
            if (window.Story is not null)
            {
                ValidateStory(window.Story, $"{path}.decisionWindows[{index}].story");
            }
            Require(window.BuildMinutesAvailable is null or > 0,
                $"{path}.decisionWindows[{index}].buildMinutesAvailable must be null or positive.");
            previousPhaseIndex = phaseIndex;
        }

        CommercialResultCardsDefinition resultCards = chapter.ResultCards ??
            throw new CommercialCoreSliceValidationException($"{path}.resultCards is required.");
        if (expectsPromise)
        {
            Require(resultCards.Standard is null &&
                    resultCards.Kept is not null &&
                    resultCards.Deferred is not null,
                $"{path}.resultCards must contain kept/deferred only.");
            ValidateStory(resultCards.Kept!, $"{path}.resultCards.kept");
            ValidateStory(resultCards.Deferred!, $"{path}.resultCards.deferred");
        }
        else
        {
            Require(resultCards.Standard is not null &&
                    resultCards.Kept is null &&
                    resultCards.Deferred is null,
                $"{path}.resultCards must contain standard only.");
            ValidateStory(resultCards.Standard!, $"{path}.resultCards.standard");
        }
    }

    private static void ValidatePhase(
        CommercialOperatingPhaseDefinition phase,
        CommercialCityPromiseDefinition? promise,
        CommercialWorldDefinition world,
        string path)
    {
        RequireText(phase.DisplayName, $"{path}.displayName");
        Require(Enum.IsDefined(phase.ThermalPolicy), $"{path}.thermalPolicy is unknown.");
        if (phase.Story is not null)
        {
            ValidateStory(phase.Story, $"{path}.story");
        }
        Require(phase.Loads.Count > 0, $"{path}.loads cannot be empty.");
        HashSet<string> knownLoadIds = world.Loads.Select(item => item.LoadId)
            .ToHashSet(StringComparer.Ordinal);
        var phaseLoadIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CommercialLoadBundleDefinition load in phase.Loads)
        {
            RequireId(load.LoadId, $"{path}.loads[].loadId");
            Require(knownLoadIds.Contains(load.LoadId),
                $"{path} references unknown load '{load.LoadId}'.");
            Require(phaseLoadIds.Add(load.LoadId),
                $"{path} duplicates load '{load.LoadId}'.");
            Require(load.DemandKw > 0, $"{path} load demand must be positive.");
            Require(Enum.IsDefined(load.Obligation), $"{path} load obligation is unknown.");
            if (load.Obligation == CommercialObligationKind.CityPromise)
            {
                Require(promise is not null && load.LoadId == promise.LoadId,
                    $"{path} city-promise load must match the chapter promise.");
            }
        }

        HashSet<string> nodeIds = world.Nodes.Select(item => item.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> edgeIds = world.Edges.Select(item => item.EdgeId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> riskIds = world.RiskAreas.Select(item => item.RiskAreaId)
            .ToHashSet(StringComparer.Ordinal);
        ValidateUniqueIds(phase.UnavailableNodeIds, $"{path}.unavailableNodeIds", nodeIds);
        ValidateUniqueIds(phase.UnavailableEdgeIds, $"{path}.unavailableEdgeIds", edgeIds);
        ValidateUniqueIds(phase.ActiveRiskAreaIds, $"{path}.activeRiskAreaIds", riskIds);
        ValidateOverrides(phase.ThermalLimitOverrides, world, $"{path}.thermalLimitOverrides");
    }

    private static void ValidateOverrides(
        IReadOnlyList<ThermalLimitOverride> overrides,
        CommercialWorldDefinition world,
        string path)
    {
        Dictionary<string, CommercialNodeClassDefinition> nodeClasses = world.NodeClasses
            .ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        Dictionary<string, CommercialLineClassDefinition> lineClasses = world.LineClasses
            .ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        var seen = new HashSet<(ThermalAssetKind Kind, string ClassId)>();
        foreach (ThermalLimitOverride item in overrides)
        {
            Require(Enum.IsDefined(item.AssetKind), $"{path} contains an unknown asset kind.");
            RequireId(item.ClassId, $"{path}[].classId");
            Require(seen.Add((item.AssetKind, item.ClassId)),
                $"{path} duplicates {item.AssetKind}/{item.ClassId}.");
            ThermalLimit? baseLimit = item.AssetKind switch
            {
                ThermalAssetKind.Node when nodeClasses.TryGetValue(
                    item.ClassId,
                    out CommercialNodeClassDefinition? nodeClass) => nodeClass.ThermalLimit,
                ThermalAssetKind.Edge when lineClasses.TryGetValue(
                    item.ClassId,
                    out CommercialLineClassDefinition? lineClass) => lineClass.ThermalLimit,
                _ => null,
            };
            if (baseLimit is null)
            {
                throw new CommercialCoreSliceValidationException(
                    $"{path} references a nonthermal or unknown class.");
            }
            Require(item.ContinuousKw > 0 && item.EmergencyKw >= item.ContinuousKw,
                $"{path} requires 0 < continuous <= emergency.");
            Require(item.ContinuousKw <= baseLimit.ContinuousKw &&
                    item.EmergencyKw <= baseLimit.EmergencyKw,
                $"{path} cannot raise a base thermal limit.");
        }
    }

    private static void ValidateStory(CommercialStoryCard story, string path)
    {
        ArgumentNullException.ThrowIfNull(story);
        RequireText(story.Speaker, $"{path}.speaker");
        RequireText(story.Title, $"{path}.title");
        RequireText(story.Body, $"{path}.body");
    }

    private static void ValidateUniqueIds(
        IReadOnlyList<string> values,
        string path,
        IReadOnlySet<string> known)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireId(value, $"{path}[]");
            Require(known.Contains(value), $"{path} references unknown ID '{value}'.");
            Require(seen.Add(value), $"{path} duplicates ID '{value}'.");
        }
    }

    private static CommercialCoreSliceDefinition Convert(RawSlice raw) => new(
        raw.SchemaVersion,
        raw.SliceId,
        raw.DisplayName,
        Segment(raw.Prelude),
        Segment(raw.Main));

    private static CommercialCoreSegmentDefinition Segment(RawSegment raw) => new(
        raw.SegmentId,
        new CommercialCoreSeedDefinition(
            raw.Seed.SeedId,
            raw.Seed.StartMinute,
            raw.Seed.InitialCashUnit,
            raw.Seed.BaseNodeIds,
            raw.Seed.BaseEdgeIds,
            raw.Seed.ConstructedNodes.Select(Node).ToArray(),
            raw.Seed.ConstructedEdges.Select(Edge).ToArray(),
            raw.Seed.CoolingAssetIds),
        Chapter(raw.Chapter));

    private static CommercialCoreChapterDefinition Chapter(RawChapter raw) => new(
        raw.ChapterId,
        raw.DisplayName,
        Story(raw.Briefing),
        raw.Objective,
        raw.BudgetGrantCashUnit,
        raw.CityPromise is null
            ? null
            : new CommercialCityPromiseDefinition(
                raw.CityPromise.PromiseId,
                raw.CityPromise.DisplayName,
                raw.CityPromise.LoadId,
                raw.CityPromise.KeepLabel,
                raw.CityPromise.DeferLabel),
        raw.DecisionWindows.Select(item => new CommercialDecisionWindowDefinition(
            item.WindowId,
            item.BeforePhaseId,
            item.Story is null ? null : Story(item.Story),
            item.BuildMinutesAvailable)).ToArray(),
        raw.OperatingPhases.Select(item => new CommercialOperatingPhaseDefinition(
            item.PhaseId,
            item.DisplayName,
            item.ThermalPolicy,
            item.Story is null ? null : Story(item.Story),
            item.Loads.Select(load => new CommercialLoadBundleDefinition(
                load.LoadId,
                load.DemandKw,
                load.Obligation)).ToArray(),
            item.UnavailableNodeIds,
            item.UnavailableEdgeIds,
            item.ActiveRiskAreaIds,
            item.ThermalLimitOverrides.Select(value => new ThermalLimitOverride(
                value.AssetKind,
                value.ClassId,
                value.ContinuousKw,
                value.EmergencyKw)).ToArray())).ToArray(),
        new CommercialResultCardsDefinition(
            raw.ResultCards.Standard is null ? null : Story(raw.ResultCards.Standard),
            raw.ResultCards.Kept is null ? null : Story(raw.ResultCards.Kept),
            raw.ResultCards.Deferred is null ? null : Story(raw.ResultCards.Deferred)));

    private static CommercialStoryCard Story(RawStory raw) =>
        new(raw.Speaker, raw.Title, raw.Body);

    private static SpatialNodeDefinition Node(RawNode raw) => new(
        raw.NodeId,
        raw.ClassId,
        raw.DisplayName,
        new MapPoint(raw.Position.XUnit, raw.Position.YUnit),
        raw.Commissioned,
        raw.AuthoredFoundation);

    private static SpatialEdgeDefinition Edge(RawEdge raw) => new(
        raw.EdgeId,
        raw.LineClassId,
        raw.FromNodeId,
        raw.ToNodeId,
        raw.Commissioned);

    private static void RejectDuplicates(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new CommercialCoreSliceValidationException(
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

    private static void RequireId(string value, string path) =>
        Require(!string.IsNullOrWhiteSpace(value) && value == value.Trim(),
            $"{path} must be nonblank and trimmed.");

    private static void RequireText(string value, string path) =>
        Require(!string.IsNullOrWhiteSpace(value) && value == value.Trim(),
            $"{path} must be nonblank and trimmed.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new CommercialCoreSliceValidationException(message);
        }
    }

    private sealed class RawSlice
    {
        [JsonRequired] public string SchemaVersion { get; init; } = null!;
        [JsonRequired] public string SliceId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public RawSegment Prelude { get; init; } = null!;
        [JsonRequired] public RawSegment Main { get; init; } = null!;
    }

    private sealed class RawSegment
    {
        [JsonRequired] public string SegmentId { get; init; } = null!;
        [JsonRequired] public RawSeed Seed { get; init; } = null!;
        [JsonRequired] public RawChapter Chapter { get; init; } = null!;
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

    private sealed class RawChapter
    {
        [JsonRequired] public string ChapterId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public RawStory Briefing { get; init; } = null!;
        [JsonRequired] public string Objective { get; init; } = null!;
        [JsonRequired] public long BudgetGrantCashUnit { get; init; }
        [JsonRequired] public RawPromise? CityPromise { get; init; }
        [JsonRequired] public RawWindow[] DecisionWindows { get; init; } = null!;
        [JsonRequired] public RawPhase[] OperatingPhases { get; init; } = null!;
        [JsonRequired] public RawResults ResultCards { get; init; } = null!;
    }

    private sealed class RawStory
    {
        [JsonRequired] public string Speaker { get; init; } = null!;
        [JsonRequired] public string Title { get; init; } = null!;
        [JsonRequired] public string Body { get; init; } = null!;
    }

    private sealed class RawPromise
    {
        [JsonRequired] public string PromiseId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public string LoadId { get; init; } = null!;
        [JsonRequired] public string KeepLabel { get; init; } = null!;
        [JsonRequired] public string DeferLabel { get; init; } = null!;
    }

    private sealed class RawWindow
    {
        [JsonRequired] public string WindowId { get; init; } = null!;
        [JsonRequired] public string BeforePhaseId { get; init; } = null!;
        [JsonRequired] public RawStory? Story { get; init; }
        [JsonRequired] public int? BuildMinutesAvailable { get; init; }
    }

    private sealed class RawPhase
    {
        [JsonRequired] public string PhaseId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public CommercialPhaseThermalPolicy ThermalPolicy { get; init; }
        [JsonRequired] public RawStory? Story { get; init; }
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

    private sealed class RawResults
    {
        [JsonRequired] public RawStory? Standard { get; init; }
        [JsonRequired] public RawStory? Kept { get; init; }
        [JsonRequired] public RawStory? Deferred { get; init; }
    }

    private sealed class RawPoint
    {
        [JsonRequired] public int XUnit { get; init; }
        [JsonRequired] public int YUnit { get; init; }
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

    private sealed class RawEdge
    {
        [JsonRequired] public string EdgeId { get; init; } = null!;
        [JsonRequired] public string LineClassId { get; init; } = null!;
        [JsonRequired] public string FromNodeId { get; init; } = null!;
        [JsonRequired] public string ToNodeId { get; init; } = null!;
        [JsonRequired] public bool Commissioned { get; init; }
    }
}
