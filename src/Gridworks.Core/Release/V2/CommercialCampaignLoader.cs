using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Core.Release.V2;

public static class CommercialCampaignLoader
{
    private static readonly string[] FinalChapterIds =
    [
        "FIRST_LIGHT",
        "SECOND_HEART",
        "SECOND_SOURCE",
        "NORTH_BANK_PROMISE",
        "WHOSE_MARGIN",
        "BEFORE_WATER_REACHES",
        "SHUT_DOWN_TO_KEEP",
        "LONGEST_NIGHT",
    ];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
    };

    public static CommercialCampaignDefinition Load(
        string json,
        CommercialWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Load(System.Text.Encoding.UTF8.GetBytes(json), world);
    }

    public static CommercialCampaignDefinition Load(
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
            RejectDuplicateProperties(document.RootElement, "$");
            CommercialCampaignDefinition definition =
                JsonSerializer.Deserialize<CommercialCampaignDefinition>(bytes, Options)
                ?? throw new CommercialCampaignValidationException(
                    "Commercial campaign root cannot be null.");
            Validate(definition, world);
            return definition;
        }
        catch (CommercialCampaignValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or OverflowException or
            NullReferenceException or CommercialCoreValidationException or
            CommercialWorldValidationException or SpatialWorldValidationException)
        {
            throw new CommercialCampaignValidationException(
                "Commercial campaign JSON is invalid.",
                exception);
        }
    }

    public static void Validate(
        CommercialCampaignDefinition definition,
        CommercialWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(world);
        Require(
            definition.SchemaVersion == CommercialCampaignDefinition.SupportedSchemaVersion,
            $"$.schemaVersion must equal '{CommercialCampaignDefinition.SupportedSchemaVersion}'.");
        RequireText(definition.CampaignId, "$.campaignId");
        RequireText(definition.DisplayName, "$.displayName");
        Require(definition.WorldId == world.WorldId,
            "$.worldId must match the commercial world.");
        Require(definition.Chapters.Count == FinalChapterIds.Length,
            "Final campaign must contain exactly eight authored missions.");
        Require(
            definition.Chapters.Select(item => item.ChapterId)
                .SequenceEqual(FinalChapterIds, StringComparer.Ordinal),
            "Final campaign missions must use the authored eight-mission order.");
        Require(definition.Chapters.Take(3).All(item =>
                item.Kind == CommercialCoreChapterKind.Prelude) &&
            definition.Chapters.Skip(3).All(item =>
                item.Kind == CommercialCoreChapterKind.CommercialCore),
            "Final campaign must contain three onboarding missions and five commercial chapters.");
        CommercialStoryCard epilogue = definition.Epilogue ??
            throw new CommercialCampaignValidationException("$.epilogue is required.");
        RequireText(epilogue.Speaker, "$.epilogue.speaker");
        RequireText(epilogue.Title, "$.epilogue.title");
        RequireText(epilogue.Body, "$.epilogue.body");

        CommercialCoreLoader.ValidateChapters(definition.Chapters, world);
        CommercialCoreChapter first = definition.Chapters[0];
        Require(first.SeedCashUnit > 0,
            "The first mission must define starting cash.");
        Require(definition.Chapters.Skip(1).All(item => item.SeedCashUnit == 0),
            "Later missions carry cash and must not reset seedCashUnit.");
        HashSet<string> previousNodes = first.SeedNodeIds.ToHashSet(StringComparer.Ordinal);
        HashSet<string> previousEdges = first.SeedEdgeIds.ToHashSet(StringComparer.Ordinal);
        foreach (CommercialCoreChapter chapter in definition.Chapters.Skip(1))
        {
            HashSet<string> currentNodes = chapter.SeedNodeIds.ToHashSet(StringComparer.Ordinal);
            HashSet<string> currentEdges = chapter.SeedEdgeIds.ToHashSet(StringComparer.Ordinal);
            Require(previousNodes.IsSubsetOf(currentNodes) && previousEdges.IsSubsetOf(currentEdges),
                "Authored map seeds must grow monotonically across missions.");
            previousNodes = currentNodes;
            previousEdges = currentEdges;
        }
        Require(definition.Chapters.Take(3).All(item => item.Promise is null) &&
            definition.Chapters[3].Promise is not null,
            "The fourth mission must introduce the first city promise after promise-free onboarding.");
        Require(world.Spatial.NodeClasses.Any(item =>
                item.Kind == SpatialNodeKind.Substation && item.ServiceRadiusUnit > 0),
            "The final world must define a positive substation service area.");
        foreach (CommercialCoreChapter chapter in definition.Chapters.Take(4))
        {
            Require(chapter.OperatingPhases.All(phase =>
                    phase.Policy == ThermalIntervalPolicy.ContinuousOnly &&
                    phase.Loads.All(load => !load.NamedEmergencyDuty && load.RequireSubstationPath)),
                "The first four missions must require a substation service path and cannot authorize emergency thermal use.");
        }
        Require(definition.Chapters.Skip(4).SelectMany(item => item.OperatingPhases).All(phase =>
                phase.Loads.All(load => load.RequireSubstationPath)),
            "Every final-campaign load must use a substation service path.");
        Require(definition.Chapters[4].OperatingPhases.Any(phase =>
                phase.Policy == ThermalIntervalPolicy.SafetyEmergencyAllowed),
            "Mission five must introduce the campaign emergency-permission boundary.");
        Require(definition.Chapters.Skip(5).All(chapter => chapter.OperatingPhases.All(phase =>
                phase.Policy is ThermalIntervalPolicy.ContinuousOnly or
                    ThermalIntervalPolicy.SafetyEmergencyAllowed)),
            "Missions six through eight may only recombine the established thermal policies.");
        Require(definition.Chapters.Select((chapter, index) =>
                chapter.ResetThermalMemoryAtStart == (index == 6)).All(item => item),
            "Only mission seven may apply the authored long-gap thermal reset.");
    }

    public static CommercialWorldDefinition CreateInitialWorld(
        CommercialWorldDefinition world,
        CommercialCampaignDefinition campaign)
    {
        HashSet<string> reservedNodeIds = campaign.Chapters.SelectMany(item => item.SeedNodeIds)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> reservedEdgeIds = campaign.Chapters.SelectMany(item => item.SeedEdgeIds)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> activeNodeIds = campaign.Chapters[0].SeedNodeIds
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> activeEdgeIds = campaign.Chapters[0].SeedEdgeIds
            .ToHashSet(StringComparer.Ordinal);
        SpatialWorldDefinition spatial = world.Spatial with
        {
            InitialCashUnit = checked(
                campaign.Chapters[0].SeedCashUnit + campaign.Chapters[0].GrantCashUnit),
            Nodes = world.Spatial.Nodes
                .Where(item => reservedNodeIds.Contains(item.NodeId))
                .Select(item => item with
                {
                    Commissioned = activeNodeIds.Contains(item.NodeId),
                    Reserved = !activeNodeIds.Contains(item.NodeId),
                })
                .ToArray(),
            Edges = world.Spatial.Edges
                .Where(item => reservedEdgeIds.Contains(item.EdgeId))
                .Select(item => item with
                {
                    Commissioned = activeEdgeIds.Contains(item.EdgeId),
                    Reserved = !activeEdgeIds.Contains(item.EdgeId),
                })
                .ToArray(),
        };
        return WorldForSpatial(world, spatial);
    }

    public static SpatialWorldDefinition ActivateChapterAssets(
        CommercialWorldDefinition world,
        SpatialWorldDefinition carried,
        CommercialCoreChapter chapter)
    {
        HashSet<string> activeNodeIds = chapter.SeedNodeIds.ToHashSet(StringComparer.Ordinal);
        HashSet<string> activeEdgeIds = chapter.SeedEdgeIds.ToHashSet(StringComparer.Ordinal);
        Dictionary<string, SpatialNodeDefinition> authoredNodes = world.Spatial.Nodes.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        Dictionary<string, SpatialEdgeDefinition> authoredEdges = world.Spatial.Edges.ToDictionary(
            item => item.EdgeId,
            StringComparer.Ordinal);
        return carried with
        {
            Nodes = carried.Nodes.Select(item => activeNodeIds.Contains(item.NodeId) &&
                    authoredNodes.TryGetValue(item.NodeId, out SpatialNodeDefinition? authored)
                    ? authored with { Commissioned = true, Reserved = false }
                    : item)
                .ToArray(),
            Edges = carried.Edges.Select(item => activeEdgeIds.Contains(item.EdgeId) &&
                    authoredEdges.TryGetValue(item.EdgeId, out SpatialEdgeDefinition? authored)
                    ? authored with { Commissioned = true, Reserved = false }
                    : item)
                .ToArray(),
        };
    }

    public static CommercialWorldDefinition WorldForSpatial(
        CommercialWorldDefinition world,
        SpatialWorldDefinition spatial)
    {
        HashSet<string> nodeIds = spatial.Nodes.Select(item => item.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        return world with
        {
            Spatial = spatial,
            GenerationSources = world.GenerationSources
                .Where(item => nodeIds.Contains(item.NodeId))
                .ToArray(),
        };
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
            throw new CommercialCampaignValidationException(message);
        }
    }
}
