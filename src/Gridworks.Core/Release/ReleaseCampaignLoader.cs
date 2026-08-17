using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Core.Release;

public static class ReleaseCampaignLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static ReleaseCampaignDefinition Load(
        string json,
        ReleaseWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Load(System.Text.Encoding.UTF8.GetBytes(json), world);
    }

    public static ReleaseCampaignDefinition Load(
        ReadOnlySpan<byte> utf8Json,
        ReleaseWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(world);
        try
        {
            byte[] bytes = utf8Json.ToArray();
            using JsonDocument document = JsonDocument.Parse(bytes);
            RejectDuplicateProperties(document.RootElement, "$");
            RawCampaign raw = JsonSerializer.Deserialize<RawCampaign>(bytes, Options)
                ?? throw new ReleaseCampaignValidationException("Campaign root cannot be null.");
            ReleaseCampaignDefinition campaign = Map(raw);
            Validate(campaign, world);
            return campaign;
        }
        catch (ReleaseCampaignValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or OverflowException)
        {
            throw new ReleaseCampaignValidationException("Campaign JSON is invalid.", exception);
        }
    }

    public static void Validate(
        ReleaseCampaignDefinition campaign,
        ReleaseWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(world);
        ReleaseWorldLoader.Validate(world);
        RequireEqual(
            ReleaseCampaignDefinition.SupportedSchemaVersion,
            campaign.SchemaVersion,
            "$.schemaVersion");
        RequireText(campaign.CampaignId, "$.campaignId");
        RequireText(campaign.DisplayName, "$.displayName");
        if (campaign.InitialCashUnit < 0)
        {
            Fail("$.initialCashUnit must be nonnegative.");
        }
        RequireUnique(campaign.InitialEdgeIds, "$.initialEdgeIds");
        HashSet<string> initialEdgeIds = campaign.InitialEdgeIds
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> edgeIds = world.Edges.Select(item => item.EdgeId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string edgeId in campaign.InitialEdgeIds)
        {
            RequireReference(edgeIds, edgeId, "$.initialEdgeIds");
        }
        if (campaign.Chapters.Count != 8)
        {
            Fail("$.chapters must contain exactly eight chapters.");
        }
        RequireUnique(campaign.Chapters.Select(item => item.ChapterId), "$.chapters[].chapterId");

        HashSet<string> loadIds = world.Loads.Select(item => item.LoadId)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, ReleaseNodeDefinition> nodes = world.Nodes
            .ToDictionary(item => item.NodeId, StringComparer.Ordinal);
        HashSet<string> riskAreaIds = world.RiskAreas.Select(item => item.RiskAreaId)
            .ToHashSet(StringComparer.Ordinal);
        long cumulativeCash = campaign.InitialCashUnit;
        for (int index = 0; index < campaign.Chapters.Count; index++)
        {
            ReleaseCampaignChapter chapter = campaign.Chapters[index];
            string path = $"$.chapters[{index}]";
            RequireText(chapter.ChapterId, $"{path}.chapterId");
            RequireText(chapter.ActLabel, $"{path}.actLabel");
            RequireText(chapter.DisplayName, $"{path}.displayName");
            ValidateStory(chapter.Briefing, $"{path}.briefing");
            RequireText(chapter.Objective, $"{path}.objective");
            ValidateStory(chapter.Result, $"{path}.result");
            if (chapter.BudgetGrantCashUnit < 0)
            {
                Fail($"{path}.budgetGrantCashUnit must be nonnegative.");
            }
            try
            {
                cumulativeCash = checked(cumulativeCash + chapter.BudgetGrantCashUnit);
            }
            catch (OverflowException)
            {
                Fail("Campaign cash grants exceed the supported range.");
            }
            if (chapter.ActiveLoads.Count == 0)
            {
                Fail($"{path}.activeLoads cannot be empty.");
            }
            RequireUnique(chapter.ActiveLoads.Select(item => item.LoadId), $"{path}.activeLoads[].loadId");
            HashSet<string> activeLoadIds = new(StringComparer.Ordinal);
            foreach (ReleaseCampaignLoad load in chapter.ActiveLoads)
            {
                RequireReference(loadIds, load.LoadId, $"{path}.activeLoads[].loadId");
                if (load.DemandKw <= 0)
                {
                    Fail($"{path}.activeLoads[].demandKw must be positive.");
                }
                activeLoadIds.Add(load.LoadId);
            }
            ValidateRequiredLoads(
                chapter.RequiredNormalLoadIds,
                activeLoadIds,
                $"{path}.requiredNormalLoadIds");
            ValidateRequiredLoads(
                chapter.RequiredEventLoadIds,
                activeLoadIds,
                $"{path}.requiredEventLoadIds");
            if (chapter.RequiredNormalLoadIds.Count == 0)
            {
                Fail($"{path}.requiredNormalLoadIds cannot be empty.");
            }

            RequireUnique(
                chapter.ConnectionRequirements.Select(item => item.NodeId),
                $"{path}.connectionRequirements[].nodeId");
            foreach (ReleaseConnectionRequirement requirement in chapter.ConnectionRequirements)
            {
                if (!nodes.TryGetValue(requirement.NodeId, out ReleaseNodeDefinition? node))
                {
                    Fail($"{path}.connectionRequirements references unknown node '{requirement.NodeId}'.");
                }
                ReleaseNodeClassDefinition nodeClass = world.NodeClasses.Single(item =>
                    string.Equals(item.ClassId, node!.ClassId, StringComparison.Ordinal));
                if (requirement.MinimumConnections <= 0 ||
                    requirement.MinimumConnections > nodeClass.MaxConnections)
                {
                    Fail($"{path}.connectionRequirements has an invalid minimumConnections value.");
                }
            }

            if (chapter.Event is null)
            {
                if (chapter.RequiredEventLoadIds.Count != 0)
                {
                    Fail($"{path}.requiredEventLoadIds requires an event.");
                }
            }
            else
            {
                ValidateStory(chapter.Event.Story, $"{path}.event.story");
                ValidateReferences(
                    chapter.Event.UnavailableNodeIds,
                    nodes.Keys.ToHashSet(StringComparer.Ordinal),
                    $"{path}.event.unavailableNodeIds");
                ValidateReferences(
                    chapter.Event.UnavailableEdgeIds,
                    initialEdgeIds,
                    $"{path}.event.unavailableEdgeIds");
                ValidateReferences(
                    chapter.Event.ActiveRiskAreaIds,
                    riskAreaIds,
                    $"{path}.event.activeRiskAreaIds");
            }
        }
    }

    private static ReleaseCampaignDefinition Map(RawCampaign raw) => new(
        raw.SchemaVersion ?? string.Empty,
        raw.CampaignId ?? string.Empty,
        raw.DisplayName ?? string.Empty,
        raw.InitialCashUnit,
        raw.InitialEdgeIds ?? [],
        (raw.Chapters ?? []).Select(chapter => new ReleaseCampaignChapter(
            chapter.ChapterId ?? string.Empty,
            chapter.ActLabel ?? string.Empty,
            chapter.DisplayName ?? string.Empty,
            MapStory(chapter.Briefing),
            chapter.Objective ?? string.Empty,
            chapter.Event is null
                ? null
                : new ReleaseCampaignEvent(
                    MapStory(chapter.Event.Story),
                    chapter.Event.UnavailableNodeIds ?? [],
                    chapter.Event.UnavailableEdgeIds ?? [],
                    chapter.Event.ActiveRiskAreaIds ?? []),
            MapStory(chapter.Result),
            chapter.BudgetGrantCashUnit,
            (chapter.ActiveLoads ?? []).Select(load =>
                new ReleaseCampaignLoad(load.LoadId ?? string.Empty, load.DemandKw)).ToArray(),
            chapter.RequiredNormalLoadIds ?? [],
            chapter.RequiredEventLoadIds ?? [],
            (chapter.ConnectionRequirements ?? []).Select(requirement =>
                new ReleaseConnectionRequirement(
                    requirement.NodeId ?? string.Empty,
                    requirement.MinimumConnections)).ToArray())).ToArray());

    private static ReleaseStoryCard MapStory(RawStory? story) => new(
        story?.Speaker ?? string.Empty,
        story?.Title ?? string.Empty,
        story?.Body ?? string.Empty);

    private static void ValidateStory(ReleaseStoryCard story, string path)
    {
        ArgumentNullException.ThrowIfNull(story);
        RequireText(story.Speaker, $"{path}.speaker");
        RequireText(story.Title, $"{path}.title");
        RequireText(story.Body, $"{path}.body");
    }

    private static void ValidateRequiredLoads(
        IReadOnlyList<string> required,
        IReadOnlySet<string> active,
        string path)
    {
        RequireUnique(required, path);
        foreach (string loadId in required)
        {
            RequireReference(active, loadId, path);
        }
    }

    private static void ValidateReferences(
        IReadOnlyList<string> values,
        IReadOnlySet<string> valid,
        string path)
    {
        RequireUnique(values, path);
        foreach (string value in values)
        {
            RequireReference(valid, value, path);
        }
    }

    private static void RequireUnique(IEnumerable<string> values, string path)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireText(value, path);
            if (!seen.Add(value))
            {
                Fail($"{path} contains duplicate value '{value}'.");
            }
        }
    }

    private static void RequireReference(IReadOnlySet<string> valid, string value, string path)
    {
        if (!valid.Contains(value))
        {
            Fail($"{path} references unknown value '{value}'.");
        }
    }

    private static void RequireText(string value, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Fail($"{path} cannot be blank.");
        }
    }

    private static void RequireEqual(string expected, string actual, string path)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            Fail($"{path} must be '{expected}'.");
        }
    }

    private static void RejectDuplicateProperties(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    Fail($"{path}.{property.Name} is duplicated.");
                }
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

    private static void Fail(string message) =>
        throw new ReleaseCampaignValidationException(message);

    private sealed class RawCampaign
    {
        public required string? SchemaVersion { get; set; }
        public required string? CampaignId { get; set; }
        public required string? DisplayName { get; set; }
        public required long InitialCashUnit { get; set; }
        public required string[]? InitialEdgeIds { get; set; }
        public required RawChapter[]? Chapters { get; set; }
    }

    private sealed class RawChapter
    {
        public required string? ChapterId { get; set; }
        public required string? ActLabel { get; set; }
        public required string? DisplayName { get; set; }
        public required RawStory? Briefing { get; set; }
        public required string? Objective { get; set; }
        public required RawEvent? Event { get; set; }
        public required RawStory? Result { get; set; }
        public required long BudgetGrantCashUnit { get; set; }
        public required RawLoad[]? ActiveLoads { get; set; }
        public required string[]? RequiredNormalLoadIds { get; set; }
        public required string[]? RequiredEventLoadIds { get; set; }
        public required RawConnectionRequirement[]? ConnectionRequirements { get; set; }
    }

    private sealed class RawStory
    {
        public required string? Speaker { get; set; }
        public required string? Title { get; set; }
        public required string? Body { get; set; }
    }

    private sealed class RawEvent
    {
        public required RawStory? Story { get; set; }
        public required string[]? UnavailableNodeIds { get; set; }
        public required string[]? UnavailableEdgeIds { get; set; }
        public required string[]? ActiveRiskAreaIds { get; set; }
    }

    private sealed class RawLoad
    {
        public required string? LoadId { get; set; }
        public required long DemandKw { get; set; }
    }

    private sealed class RawConnectionRequirement
    {
        public required string? NodeId { get; set; }
        public required int MinimumConnections { get; set; }
    }
}
