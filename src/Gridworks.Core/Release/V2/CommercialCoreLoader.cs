using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Core.Release.V2;

public static class CommercialCoreLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
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
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
            RejectDuplicateProperties(document.RootElement, "$" );
            CommercialCoreSliceDefinition definition = JsonSerializer.Deserialize<CommercialCoreSliceDefinition>(
                bytes,
                Options) ?? throw new CommercialCoreValidationException("Core slice root cannot be null.");
            Validate(definition, world);
            return definition;
        }
        catch (CommercialCoreValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or OverflowException or NullReferenceException or
            CommercialWorldValidationException or SpatialWorldValidationException)
        {
            throw new CommercialCoreValidationException("Commercial core slice JSON is invalid.", exception);
        }
    }

    public static void Validate(
        CommercialCoreSliceDefinition definition,
        CommercialWorldDefinition world)
    {
        Validate(definition, world, requireStageDSliceShape: true);
    }

    internal static void ValidateChapters(
        IReadOnlyList<CommercialCoreChapter> chapters,
        CommercialWorldDefinition world)
    {
        Validate(
            new CommercialCoreSliceDefinition(
                CommercialCoreSliceDefinition.SupportedSchemaVersion,
                "CAMPAIGN_CONTENT_VALIDATION",
                "Campaign content validation",
                world.WorldId,
                chapters),
            world,
            requireStageDSliceShape: false);
    }

    private static void Validate(
        CommercialCoreSliceDefinition definition,
        CommercialWorldDefinition world,
        bool requireStageDSliceShape)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(world);
        CommercialWorldLoader.Validate(world);
        Require(definition.SchemaVersion == CommercialCoreSliceDefinition.SupportedSchemaVersion,
            $"$.schemaVersion must equal '{CommercialCoreSliceDefinition.SupportedSchemaVersion}'.");
        RequireText(definition.SliceId, "$.sliceId");
        RequireText(definition.DisplayName, "$.displayName");
        Require(definition.WorldId == world.WorldId, "$.worldId must match the commercial world.");
        if (requireStageDSliceShape)
        {
            Require(definition.Chapters.Count == 2,
                "Stage-D core slice must contain exactly the prelude and commercial core chapter.");
            Require(definition.Chapters[0].Kind == CommercialCoreChapterKind.Prelude,
                "The first core-slice chapter must be Prelude.");
            Require(definition.Chapters[1].Kind == CommercialCoreChapterKind.CommercialCore,
                "The second core-slice chapter must be CommercialCore.");
        }
        RequireUnique(definition.Chapters.Select(item => item.ChapterId), "$.chapters[].chapterId");

        HashSet<string> worldNodeIds = world.Spatial.Nodes.Select(item => item.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, SpatialNodeKind> worldNodeKinds = world.Spatial.Nodes.ToDictionary(
            item => item.NodeId,
            item => world.Spatial.NodeClasses.Single(nodeClass =>
                nodeClass.ClassId == item.ClassId).Kind,
            StringComparer.Ordinal);
        Dictionary<string, SpatialEdgeDefinition> worldEdges = world.Spatial.Edges.ToDictionary(
            item => item.EdgeId,
            StringComparer.Ordinal);
        HashSet<string> riskIds = world.Spatial.RiskAreas.Select(item => item.RiskAreaId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> thermalAssetIds = CommercialWorldLoader.ThermalAssetIds(world)
            .ToHashSet(StringComparer.Ordinal);

        for (int chapterIndex = 0; chapterIndex < definition.Chapters.Count; chapterIndex++)
        {
            CommercialCoreChapter chapter = definition.Chapters[chapterIndex];
            string path = $"$.chapters[{chapterIndex}]";
            Require(Enum.IsDefined(chapter.Kind), $"{path}.kind is not supported.");
            RequireText(chapter.ChapterId, $"{path}.chapterId");
            RequireText(chapter.DisplayName, $"{path}.displayName");
            Require(chapter.SeedCashUnit >= 0 && chapter.GrantCashUnit >= 0,
                $"{path} cash values must be nonnegative.");
            _ = checked(chapter.SeedCashUnit + chapter.GrantCashUnit);
            Require(chapter.DeadlineMinute > 0, $"{path}.deadlineMinute must be positive.");
            ValidateStory(chapter.Briefing, $"{path}.briefing");
            RequireText(chapter.Objective, $"{path}.objective");
            ValidateStory(chapter.StandardResult, $"{path}.standardResult");

            RequireUnique(chapter.SeedNodeIds, $"{path}.seedNodeIds");
            Require(chapter.SeedNodeIds.Count > 0, $"{path}.seedNodeIds cannot be empty.");
            foreach (string nodeId in chapter.SeedNodeIds)
            {
                Require(worldNodeIds.Contains(nodeId), $"{path}.seedNodeIds references '{nodeId}'.");
            }
            HashSet<string> seedNodes = chapter.SeedNodeIds.ToHashSet(StringComparer.Ordinal);
            RequireUnique(chapter.SeedEdgeIds, $"{path}.seedEdgeIds");
            foreach (string edgeId in chapter.SeedEdgeIds)
            {
                Require(worldEdges.TryGetValue(edgeId, out SpatialEdgeDefinition? edge),
                    $"{path}.seedEdgeIds references '{edgeId}'.");
                Require(seedNodes.Contains(edge!.FromNodeId) && seedNodes.Contains(edge.ToNodeId),
                    $"{path}.seed edge '{edgeId}' has an endpoint outside seedNodeIds.");
            }
            CommercialWorldDefinition chapterWorld = CreateChapterWorld(world, chapter);
            CommercialWorldLoader.Validate(chapterWorld);

            if (chapter.Promise is null)
            {
                Require(chapter.KeptResult is null && chapter.DeferredResult is null,
                    $"{path} without a promise cannot have kept/deferred result cards.");
            }
            else
            {
                RequireText(chapter.Promise.PromiseId, $"{path}.promise.promiseId");
                RequireText(chapter.Promise.DisplayName, $"{path}.promise.displayName");
                RequireText(chapter.Promise.LoadId, $"{path}.promise.loadId");
                Require(chapter.KeptResult is not null && chapter.DeferredResult is not null,
                    $"{path} promise needs kept and deferred result cards.");
                ValidateStory(chapter.KeptResult!, $"{path}.keptResult");
                ValidateStory(chapter.DeferredResult!, $"{path}.deferredResult");
            }

            Require(chapter.DecisionWindows.Count is >= 1 and <= 3,
                $"{path}.decisionWindows must contain one to three windows.");
            Require(chapter.OperatingPhases.Count > 0,
                $"{path}.operatingPhases cannot be empty.");
            RequireUnique(chapter.DecisionWindows.Select(item => item.WindowId),
                $"{path}.decisionWindows[].windowId");
            RequireUnique(chapter.OperatingPhases.Select(item => item.PhaseId),
                $"{path}.operatingPhases[].phaseId");
            Dictionary<string, int> phaseIndexes = chapter.OperatingPhases
                .Select((item, index) => (item.PhaseId, index))
                .ToDictionary(item => item.PhaseId, item => item.index, StringComparer.Ordinal);
            int lastPhaseIndex = -1;
            long cumulativeAllowance = 0;
            foreach (CommercialCoreDecisionWindow window in chapter.DecisionWindows)
            {
                RequireText(window.WindowId, $"{path}.decisionWindows[].windowId");
                Require(phaseIndexes.TryGetValue(window.NextPhaseId, out int phaseIndex),
                    $"{path} window '{window.WindowId}' references an unknown next phase.");
                Require(phaseIndex > lastPhaseIndex,
                    $"{path} decision windows must advance through phases in order.");
                lastPhaseIndex = phaseIndex;
                if (window.Story is not null)
                {
                    ValidateStory(window.Story, $"{path}.decisionWindows[].story");
                }
                if (window.BuildMinutesAllowance is int allowance)
                {
                    Require(allowance > 0,
                        $"{path}.decisionWindows[].buildMinutesAllowance must be positive when present.");
                    cumulativeAllowance = checked(cumulativeAllowance + allowance);
                    Require(cumulativeAllowance <= chapter.DeadlineMinute,
                        $"{path} cumulative build allowance exceeds the chapter deadline.");
                }
            }
            Require(lastPhaseIndex >= 0 &&
                chapter.DecisionWindows[0].NextPhaseId == chapter.OperatingPhases[0].PhaseId,
                $"{path} first decision window must begin at the first operating phase.");

            HashSet<string> chapterLoadIds = new(StringComparer.Ordinal);
            int promiseLoadOccurrences = 0;
            foreach (CommercialCoreOperatingPhase phase in chapter.OperatingPhases)
            {
                string phasePath = $"{path}.operatingPhases[{phaseIndexes[phase.PhaseId]}]";
                RequireText(phase.PhaseId, $"{phasePath}.phaseId");
                RequireText(phase.DisplayName, $"{phasePath}.displayName");
                Require(Enum.IsDefined(phase.Policy), $"{phasePath}.policy is not supported.");
                Require(phase.Loads.Count > 0, $"{phasePath}.loads cannot be empty.");
                RequireUnique(phase.Loads.Select(item => item.LoadId), $"{phasePath}.loads[].loadId");
                foreach (CommercialCoreLoadBundle load in phase.Loads)
                {
                    RequireText(load.LoadId, $"{phasePath}.loads[].loadId");
                    RequireText(load.DisplayName, $"{phasePath}.loads[].displayName");
                    Require(seedNodes.Contains(load.NodeId),
                        $"{phasePath} load '{load.LoadId}' references a node outside the chapter seed.");
                    Require(worldNodeKinds[load.NodeId] == SpatialNodeKind.DedicatedLoadTerminal,
                        $"{phasePath} load '{load.LoadId}' must reference a dedicated load terminal.");
                    Require(load.DemandKw > 0, $"{phasePath} load '{load.LoadId}' must be positive.");
                    Require(Enum.IsDefined(load.ObligationKind),
                        $"{phasePath} load '{load.LoadId}' obligation kind is not supported.");
                    Require(!load.NamedEmergencyDuty ||
                        load.ObligationKind == CommercialCoreObligationKind.MustSupply,
                        $"{phasePath} only MustSupply can be a named emergency duty.");
                    Require(chapterLoadIds.Add(load.LoadId),
                        $"{path} duplicates load ID '{load.LoadId}' across phases.");
                    if (load.ObligationKind == CommercialCoreObligationKind.CityPromise)
                    {
                        promiseLoadOccurrences++;
                        Require(chapter.Promise is not null && chapter.Promise.LoadId == load.LoadId,
                            $"{phasePath} city promise load does not match the chapter promise.");
                    }
                }
                RequireUnique(phase.UnavailableAssetIds, $"{phasePath}.unavailableAssetIds");
                foreach (string assetId in phase.UnavailableAssetIds)
                {
                    Require(seedNodes.Contains(assetId) || chapter.SeedEdgeIds.Contains(assetId),
                        $"{phasePath} unavailable asset '{assetId}' is outside the chapter seed.");
                }
                RequireUnique(phase.ActiveRiskAreaIds, $"{phasePath}.activeRiskAreaIds");
                foreach (string riskId in phase.ActiveRiskAreaIds)
                {
                    Require(riskIds.Contains(riskId),
                        $"{phasePath} references unknown risk area '{riskId}'.");
                }
                RequireUnique(phase.LimitOverrides.Select(item => item.AssetId),
                    $"{phasePath}.limitOverrides[].assetId");
                foreach (ThermalLimitOverride item in phase.LimitOverrides)
                {
                    Require(thermalAssetIds.Contains(item.AssetId) &&
                        (seedNodes.Contains(item.AssetId) || chapter.SeedEdgeIds.Contains(item.AssetId)),
                        $"{phasePath} override '{item.AssetId}' is outside the chapter thermal seed.");
                    ThermalLimitDefinition baseLimit = CommercialWorldLoader.LimitForAsset(world, item.AssetId);
                    Require(item.ContinuousLimitKw > 0 &&
                        item.ContinuousLimitKw <= item.EmergencyLimitKw &&
                        item.ContinuousLimitKw <= baseLimit.ContinuousLimitKw &&
                        item.EmergencyLimitKw <= baseLimit.EmergencyLimitKw,
                        $"{phasePath} override '{item.AssetId}' must only lower valid limits.");
                }
            }
            Require((chapter.Promise is null && promiseLoadOccurrences == 0) ||
                (chapter.Promise is not null && promiseLoadOccurrences == 1),
                $"{path} promise must map to exactly one CityPromise load.");
        }
    }

    public static CommercialWorldDefinition CreateChapterWorld(
        CommercialWorldDefinition world,
        CommercialCoreChapter chapter)
    {
        HashSet<string> nodeIds = chapter.SeedNodeIds.ToHashSet(StringComparer.Ordinal);
        HashSet<string> edgeIds = chapter.SeedEdgeIds.ToHashSet(StringComparer.Ordinal);
        SpatialWorldDefinition spatial = world.Spatial with
        {
            InitialCashUnit = checked(chapter.SeedCashUnit + chapter.GrantCashUnit),
            Nodes = world.Spatial.Nodes.Where(item => nodeIds.Contains(item.NodeId)).ToArray(),
            Edges = world.Spatial.Edges.Where(item => edgeIds.Contains(item.EdgeId)).ToArray(),
        };
        return world with
        {
            Spatial = spatial,
            GenerationSources = world.GenerationSources
                .Where(item => nodeIds.Contains(item.NodeId))
                .ToArray(),
        };
    }

    private static void ValidateStory(CommercialStoryCard story, string path)
    {
        ArgumentNullException.ThrowIfNull(story);
        RequireText(story.Speaker, $"{path}.speaker");
        RequireText(story.Title, $"{path}.title");
        RequireText(story.Body, $"{path}.body");
    }

    private static void RequireUnique(IEnumerable<string> values, string path)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireText(value, path);
            Require(seen.Add(value), $"{path} contains duplicate value '{value}'.");
        }
    }

    private static void RejectDuplicateProperties(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                Require(names.Add(property.Name), $"{path}.{property.Name} is duplicated.");
                RejectDuplicateProperties(property.Value, $"{path}.{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item, $"{path}[{index++}]");
            }
        }
    }

    private static void RequireText(string? value, string path) =>
        Require(!string.IsNullOrWhiteSpace(value), $"{path} must be nonempty text.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new CommercialCoreValidationException(message);
        }
    }
}
