using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Core.Release.V2;

public static class CommercialCampaignLoader
{
    public const string SupportedSchemaVersion = "gridworks.release.campaign.v2";

    public static IReadOnlyList<string> CanonicalChapterIds { get; } = Array.AsReadOnly(
        new[]
        {
            "FIRST_LIGHT",
            "SECOND_HEART",
            "SECOND_SOURCE",
            "NORTH_BANK_PROMISE",
            "WHOSE_MARGIN",
            "BEFORE_WATER_RISE",
            "SWITCH_OFF_TO_PROTECT",
            "LONGEST_NIGHT",
        });

    private static readonly string[] CanonicalChapterNames =
    [
        "첫 불빛",
        "두 번째 심장",
        "두 번째 전원",
        "북안의 약속",
        "누구의 여유인가",
        "물이 닿기 전에",
        "꺼야 지킬 수 있다",
        "가장 긴 밤",
    ];

    private static readonly (string PromiseId, string DisplayName, string LoadId)?[]
        CanonicalPromises =
    [
        null,
        null,
        null,
        ("NORTH_BANK_MOVE_IN_PROMISE", "북안 입주 일정", "NORTH_RESIDENTIAL"),
        ("FACTORY_NIGHT_SHIFT_PROMISE", "산업 야간 증산", "RIVER_FACTORY"),
        ("EAST_CONTINUITY_PROMISE", "동부 생활권 공급 유지", "EAST_RESIDENTIAL"),
        null,
        null,
    ];

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
            using JsonDocument document = JsonDocument.Parse(bytes);
            RejectDuplicates(document.RootElement, "$");
            RawCampaign raw = JsonSerializer.Deserialize<RawCampaign>(bytes, Options)
                ?? throw new CommercialCampaignValidationException("Commercial campaign is empty.");
            CommercialCampaignDefinition campaign = Convert(raw);
            Validate(campaign, world);
            return campaign;
        }
        catch (CommercialCampaignValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or InvalidOperationException or
            NullReferenceException or OverflowException or SpatialWorldValidationException or
            CommercialWorldValidationException or CommercialCoreSliceValidationException)
        {
            throw new CommercialCampaignValidationException(
                "Commercial campaign contains an invalid value.",
                exception);
        }
    }

    public static void Validate(
        CommercialCampaignDefinition campaign,
        CommercialWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(world);
        CommercialWorldLoader.Validate(world);
        Require(campaign.SchemaVersion == SupportedSchemaVersion,
            $"schemaVersion must equal '{SupportedSchemaVersion}'.");
        RequireId(campaign.CampaignId, "campaignId");
        RequireText(campaign.DisplayName, "displayName");
        CommercialCoreSeedDefinition seed = campaign.InitialSeed ??
            throw new CommercialCampaignValidationException("initialSeed is required.");
        CommercialWorldDefinition seedWorld = ValidateSeed(seed, world, "initialSeed");

        Require(campaign.Chapters.Count == CanonicalChapterIds.Count,
            "Final campaign v2 must contain exactly the canonical eight chapters.");
        long authoredMinute = seed.StartMinute;
        long authoredCash = seed.InitialCashUnit;
        for (int index = 0; index < campaign.Chapters.Count; index++)
        {
            CommercialCampaignChapterDefinition chapter = campaign.Chapters[index] ??
                throw new CommercialCampaignValidationException($"chapters[{index}] is null.");
            Require(chapter.ChapterId == CanonicalChapterIds[index],
                $"chapters[{index}].chapterId must equal '{CanonicalChapterIds[index]}'.");
            Require(chapter.DisplayName == CanonicalChapterNames[index],
                $"chapters[{index}].displayName must equal '{CanonicalChapterNames[index]}'.");
            ValidateChapter(
                chapter,
                seedWorld,
                expectsPromise: CanonicalPromises[index].HasValue,
                allowsEmergencyPolicy: index >= 4,
                $"chapters[{index}]");
            ValidateCanonicalPromise(chapter, index, $"chapters[{index}]");
            ValidateCanonicalConnectionRequirements(chapter, index, $"chapters[{index}]");
            ValidateCanonicalChapterShape(chapter, index, $"chapters[{index}]");
            if (index == 0)
            {
                Require(chapter.TimeAdvanceBeforeChapterMinutes == 0 &&
                        !chapter.ResetThermalStateBeforeChapter,
                    "The first chapter cannot advance time or reset thermal state.");
            }
            Require(!chapter.ResetThermalStateBeforeChapter ||
                    chapter.TimeAdvanceBeforeChapterMinutes > 0,
                $"chapters[{index}] may reset thermal state only after a positive time advance.");
            authoredMinute = checked(authoredMinute + chapter.TimeAdvanceBeforeChapterMinutes);
            authoredCash = checked(authoredCash + chapter.BudgetGrantCashUnit);
        }
        _ = authoredMinute;
        _ = authoredCash;
        ValidateEpilogue(
            campaign.Epilogue ?? throw new CommercialCampaignValidationException(
                "epilogue is required."),
            campaign.Chapters,
            "epilogue");
    }

    public static CommercialWorldDefinition BuildInitialWorld(
        CommercialWorldDefinition world,
        CommercialCoreSeedDefinition seed) =>
        CommercialCoreSliceLoader.BuildSeedWorld(world, seed);

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
        RequireOrdinalOrder(seed.BaseNodeIds, $"{path}.baseNodeIds");
        RequireOrdinalOrder(seed.BaseEdgeIds, $"{path}.baseEdgeIds");
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

        CommercialWorldDefinition seedWorld = BuildInitialWorld(world, seed);
        CommercialWorldLoader.Validate(seedWorld);
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
        RequireOrdinalOrder(seed.CoolingAssetIds, $"{path}.coolingAssetIds");
        return seedWorld;
    }

    private static void ValidateChapter(
        CommercialCampaignChapterDefinition chapter,
        CommercialWorldDefinition world,
        bool expectsPromise,
        bool allowsEmergencyPolicy,
        string path)
    {
        RequireId(chapter.ChapterId, $"{path}.chapterId");
        RequireText(chapter.DisplayName, $"{path}.displayName");
        ValidateStory(chapter.Briefing, $"{path}.briefing");
        RequireText(chapter.Objective, $"{path}.objective");
        Require(chapter.TimeAdvanceBeforeChapterMinutes >= 0,
            $"{path}.timeAdvanceBeforeChapterMinutes must be nonnegative.");
        Require(chapter.BudgetGrantCashUnit >= 0,
            $"{path}.budgetGrantCashUnit must be nonnegative.");
        _ = checked(world.InitialCashUnit + chapter.BudgetGrantCashUnit);
        ValidateTools(chapter, world, path);
        ValidateConnectionRequirements(chapter.ConnectionRequirements, world, path);
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
            CommercialOperatingPhaseDefinition phase = chapter.OperatingPhases[index] ??
                throw new CommercialCampaignValidationException(
                    $"{path}.operatingPhases[{index}] is null.");
            RequireId(phase.PhaseId, $"{path}.operatingPhases[{index}].phaseId");
            Require(phaseIds.TryAdd(phase.PhaseId, index),
                $"{path} duplicates phase ID '{phase.PhaseId}'.");
            ValidatePhase(phase, chapter.CityPromise, world, allowsEmergencyPolicy,
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
            CommercialDecisionWindowDefinition window = chapter.DecisionWindows[index] ??
                throw new CommercialCampaignValidationException(
                    $"{path}.decisionWindows[{index}] is null.");
            RequireId(window.WindowId, $"{path}.decisionWindows[{index}].windowId");
            Require(windowIds.Add(window.WindowId),
                $"{path} duplicates window ID '{window.WindowId}'.");
            RequireId(window.BeforePhaseId, $"{path}.decisionWindows[{index}].beforePhaseId");
            Require(phaseIds.TryGetValue(window.BeforePhaseId, out int phaseIndex),
                $"{path} window '{window.WindowId}' references an unknown phase.");
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

        CommercialResultCardsDefinition results = chapter.ResultCards ??
            throw new CommercialCampaignValidationException($"{path}.resultCards is required.");
        if (expectsPromise)
        {
            Require(results.Standard is null && results.Kept is not null && results.Deferred is not null,
                $"{path}.resultCards must contain kept/deferred only.");
            ValidateStory(results.Kept!, $"{path}.resultCards.kept");
            ValidateStory(results.Deferred!, $"{path}.resultCards.deferred");
        }
        else
        {
            Require(results.Standard is not null && results.Kept is null && results.Deferred is null,
                $"{path}.resultCards must contain standard only.");
            ValidateStory(results.Standard!, $"{path}.resultCards.standard");
        }
        ValidateFactTemplates(
            chapter.ResultFactTemplates,
            expectsPromise,
            $"{path}.resultFactTemplates");
    }

    private static void ValidateTools(
        CommercialCampaignChapterDefinition chapter,
        CommercialWorldDefinition world,
        string path)
    {
        Require(chapter.AvailableNodeClassIds.Count > 0,
            $"{path}.availableNodeClassIds cannot be empty.");
        Require(chapter.AvailableLinePlans.Count > 0,
            $"{path}.availableLinePlans cannot be empty.");
        Dictionary<string, CommercialNodeClassDefinition> nodeClasses = world.NodeClasses
            .ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        HashSet<string> lineClassIds = world.LineClasses.Select(item => item.ClassId)
            .ToHashSet(StringComparer.Ordinal);
        var seenNodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (string nodeClassId in chapter.AvailableNodeClassIds)
        {
            RequireId(nodeClassId, $"{path}.availableNodeClassIds[]");
            Require(seenNodes.Add(nodeClassId),
                $"{path}.availableNodeClassIds duplicates '{nodeClassId}'.");
            Require(nodeClasses.TryGetValue(nodeClassId, out CommercialNodeClassDefinition? nodeClass) &&
                    nodeClass.Kind == SpatialNodeKind.Substation,
                $"{path}.availableNodeClassIds must reference substation classes.");
        }
        RequireOrdinalOrder(chapter.AvailableNodeClassIds, $"{path}.availableNodeClassIds");

        var seenPlans = new HashSet<(string LineClassId, string PoleClassId)>();
        string? previousKey = null;
        foreach (CommercialCampaignLinePlanDefinition plan in chapter.AvailableLinePlans)
        {
            RequireId(plan.LineClassId, $"{path}.availableLinePlans[].lineClassId");
            RequireId(plan.PoleClassId, $"{path}.availableLinePlans[].poleClassId");
            Require(lineClassIds.Contains(plan.LineClassId),
                $"{path}.availableLinePlans references unknown line class '{plan.LineClassId}'.");
            Require(nodeClasses.TryGetValue(plan.PoleClassId, out CommercialNodeClassDefinition? poleClass) &&
                    poleClass.Kind == SpatialNodeKind.Pole,
                $"{path}.availableLinePlans must reference pole classes.");
            Require(seenPlans.Add((plan.LineClassId, plan.PoleClassId)),
                $"{path}.availableLinePlans duplicates a line/pole pair.");
            string key = plan.LineClassId + "\0" + plan.PoleClassId;
            Require(previousKey is null || string.CompareOrdinal(previousKey, key) < 0,
                $"{path}.availableLinePlans must use ordinal line/pole order.");
            previousKey = key;
        }
    }

    private static void ValidateConnectionRequirements(
        IReadOnlyList<CommercialCampaignConnectionRequirement> requirements,
        CommercialWorldDefinition world,
        string path)
    {
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        Dictionary<string, CommercialNodeClassDefinition> nodeClasses = world.NodeClasses
            .ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? previous = null;
        foreach (CommercialCampaignConnectionRequirement requirement in requirements)
        {
            RequireId(requirement.NodeId, $"{path}.connectionRequirements[].nodeId");
            Require(seen.Add(requirement.NodeId),
                $"{path}.connectionRequirements duplicates '{requirement.NodeId}'.");
            Require(previous is null || string.CompareOrdinal(previous, requirement.NodeId) < 0,
                $"{path}.connectionRequirements must use ordinal node order.");
            Require(nodes.TryGetValue(requirement.NodeId, out SpatialNodeDefinition? node),
                $"{path}.connectionRequirements references unknown node '{requirement.NodeId}'.");
            Require(requirement.MinimumConnections >= 2 &&
                    requirement.MinimumConnections <= nodeClasses[node!.ClassId].MaxConnections,
                $"{path}.connectionRequirements minimum must be 2..node maxConnections.");
            previous = requirement.NodeId;
        }
    }

    private static void ValidateCanonicalConnectionRequirements(
        CommercialCampaignChapterDefinition chapter,
        int chapterIndex,
        string path)
    {
        if (chapterIndex == 1)
        {
            Require(chapter.ConnectionRequirements.Count == 1 &&
                    chapter.ConnectionRequirements[0].NodeId == "HOSPITAL_TERMINAL" &&
                    chapter.ConnectionRequirements[0].MinimumConnections == 2,
                $"{path}.connectionRequirements must require HOSPITAL_TERMINAL >= 2.");
        }
        else if (chapterIndex == 5)
        {
            Require(chapter.ConnectionRequirements.Count == 1 &&
                    chapter.ConnectionRequirements[0].NodeId ==
                        "EAST_RESIDENTIAL_TERMINAL" &&
                    chapter.ConnectionRequirements[0].MinimumConnections == 2,
                $"{path}.connectionRequirements must require EAST_RESIDENTIAL_TERMINAL >= 2.");
        }
        else if (chapterIndex == 6)
        {
            Require(chapter.ConnectionRequirements.Count == 1 &&
                    chapter.ConnectionRequirements[0].NodeId == "WATER_TERMINAL" &&
                    chapter.ConnectionRequirements[0].MinimumConnections == 2,
                $"{path}.connectionRequirements must require WATER_TERMINAL >= 2.");
        }
        else
        {
            Require(chapter.ConnectionRequirements.Count == 0,
                $"{path}.connectionRequirements must be empty.");
        }
    }

    private static void ValidateCanonicalPromise(
        CommercialCampaignChapterDefinition chapter,
        int chapterIndex,
        string path)
    {
        (string PromiseId, string DisplayName, string LoadId)? expected =
            CanonicalPromises[chapterIndex];
        if (!expected.HasValue)
        {
            Require(chapter.CityPromise is null,
                $"{path}.cityPromise must be null.");
            return;
        }

        CommercialCityPromiseDefinition promise = chapter.CityPromise!;
        Require(
            promise.PromiseId == expected.Value.PromiseId &&
            promise.DisplayName == expected.Value.DisplayName &&
            promise.LoadId == expected.Value.LoadId,
            $"{path}.cityPromise must match the canonical chapter promise.");
    }

    private static void ValidateCanonicalChapterShape(
        CommercialCampaignChapterDefinition chapter,
        int chapterIndex,
        string path)
    {
        if (chapterIndex == 4)
        {
            Require(chapter.OperatingPhases.Count == 3,
                $"{path} must contain hot base, night-shift promise, and late-night recovery.");
            Require(
                chapter.OperatingPhases[0].PhaseId == "HOT_BASE" &&
                chapter.OperatingPhases[0].ThermalPolicy ==
                    CommercialPhaseThermalPolicy.ContinuousOnly &&
                chapter.OperatingPhases[0].Loads.All(load =>
                    load.Obligation != CommercialObligationKind.CityPromise) &&
                chapter.OperatingPhases[1].PhaseId == "NIGHT_SHIFT" &&
                chapter.OperatingPhases[1].ThermalPolicy ==
                    CommercialPhaseThermalPolicy.ContinuousOnly &&
                chapter.OperatingPhases[1].Loads.Any(load =>
                    load.Obligation == CommercialObligationKind.CityPromise) &&
                chapter.OperatingPhases[2].PhaseId == "LATE_NIGHT" &&
                chapter.OperatingPhases[2].ThermalPolicy ==
                    CommercialPhaseThermalPolicy.ContinuousOnly &&
                chapter.OperatingPhases[2].Loads.All(load =>
                    load.Obligation != CommercialObligationKind.CityPromise),
                $"{path} must keep safety duties continuous while the night-shift promise alone may use emergency headroom before recovery.");
        }

        if (chapterIndex == 5)
        {
            Require(chapter.DecisionWindows.Count == 1 &&
                    chapter.DecisionWindows[0].BuildMinutesAvailable.HasValue &&
                    chapter.OperatingPhases.Count == 1 &&
                    chapter.OperatingPhases[0].PhaseId == "FLOOD_ARRIVAL" &&
                    chapter.OperatingPhases[0].ActiveRiskAreaIds.Count > 0 &&
                    chapter.OperatingPhases[0].Loads.Any(load =>
                        load.Obligation == CommercialObligationKind.CityPromise),
                $"{path} must exercise the east continuity promise under a bounded build deadline and active flood risk.");
        }

        if (chapterIndex == 6)
        {
            Require(chapter.OperatingPhases.Count == 2 &&
                    chapter.OperatingPhases[0].PhaseId ==
                        "WEST_SOURCE_PLANNED_OUTAGE" &&
                    chapter.OperatingPhases[1].PhaseId ==
                        "WEST_SOURCE_RETURN_SERVICE" &&
                    chapter.OperatingPhases[0].ThermalPolicy ==
                        CommercialPhaseThermalPolicy.SafetyEmergencyAllowed &&
                    chapter.OperatingPhases[1].ThermalPolicy ==
                        CommercialPhaseThermalPolicy.ContinuousOnly &&
                    chapter.OperatingPhases[0].UnavailableNodeIds.Contains(
                        "WEST_SOURCE_NODE",
                        StringComparer.Ordinal) &&
                    !chapter.OperatingPhases[1].UnavailableNodeIds.Contains(
                        "WEST_SOURCE_NODE",
                        StringComparer.Ordinal),
                $"{path} must contain the planned source outage and return-service phases.");
        }

        if (chapterIndex == 7)
        {
            Require(chapter.OperatingPhases.Count == 3 &&
                    chapter.DecisionWindows.Count == 1 &&
                    chapter.OperatingPhases[0].PhaseId == "MAX_DEMAND" &&
                    chapter.OperatingPhases[0].ThermalPolicy ==
                        CommercialPhaseThermalPolicy.ContinuousOnly &&
                    chapter.OperatingPhases[1].PhaseId == "HEATWAVE_PEAK" &&
                    chapter.OperatingPhases[1].ThermalPolicy ==
                        CommercialPhaseThermalPolicy.SafetyEmergencyAllowed &&
                    chapter.OperatingPhases[1].ThermalLimitOverrides.Count > 0 &&
                    chapter.OperatingPhases[2].PhaseId == "PROTECTIVE_STOP_FLOOD" &&
                    chapter.OperatingPhases[2].ThermalPolicy ==
                        CommercialPhaseThermalPolicy.SafetyEmergencyAllowed &&
                    chapter.OperatingPhases[2].ThermalLimitOverrides.Count > 0 &&
                    chapter.OperatingPhases[2].ActiveRiskAreaIds.Count > 0,
                $"{path} must contain three locked operating phases behind one decision window.");
        }
    }

    private static void ValidateEpilogue(
        CommercialCampaignEpilogueDefinition epilogue,
        IReadOnlyList<CommercialCampaignChapterDefinition> chapters,
        string path)
    {
        RequireText(epilogue.DisplayName, $"{path}.displayName");
        ValidateStory(epilogue.CityReport, $"{path}.cityReport");
        ValidateStory(epilogue.MedicalWitness, $"{path}.medicalWitness");
        ValidateStory(epilogue.Closing, $"{path}.closing");

        int[] promiseChapterIndices = [3, 4, 5];
        Require(epilogue.PromiseLines.Count == promiseChapterIndices.Length,
            $"{path}.promiseLines must contain exactly the three campaign promises.");
        for (int index = 0; index < promiseChapterIndices.Length; index++)
        {
            CommercialCampaignChapterDefinition chapter = chapters[promiseChapterIndices[index]];
            CommercialCampaignEpiloguePromiseLineDefinition line =
                epilogue.PromiseLines[index] ??
                throw new CommercialCampaignValidationException(
                    $"{path}.promiseLines[{index}] is null.");
            Require(
                line.ChapterId == chapter.ChapterId &&
                line.PromiseId == chapter.CityPromise!.PromiseId,
                $"{path}.promiseLines[{index}] must match chapter '{chapter.ChapterId}'.");
            RequireText(line.Kept, $"{path}.promiseLines[{index}].kept");
            RequireText(line.Deferred, $"{path}.promiseLines[{index}].deferred");
            Require(!line.Kept.Contains('{', StringComparison.Ordinal) &&
                    !line.Kept.Contains('}', StringComparison.Ordinal) &&
                    !line.Deferred.Contains('{', StringComparison.Ordinal) &&
                    !line.Deferred.Contains('}', StringComparison.Ordinal),
                $"{path}.promiseLines[{index}] must be authored text, not templates.");
        }
    }

    private static void ValidatePhase(
        CommercialOperatingPhaseDefinition phase,
        CommercialCityPromiseDefinition? promise,
        CommercialWorldDefinition world,
        bool allowsEmergencyPolicy,
        string path)
    {
        RequireText(phase.DisplayName, $"{path}.displayName");
        Require(Enum.IsDefined(phase.ThermalPolicy),
            $"{path}.thermalPolicy is unknown.");
        Require(allowsEmergencyPolicy ||
                phase.ThermalPolicy == CommercialPhaseThermalPolicy.ContinuousOnly,
            $"{path}.thermalPolicy must remain continuousOnly in the first four chapters.");
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
                    item.ClassId, out CommercialNodeClassDefinition? nodeClass) =>
                    nodeClass.ThermalLimit,
                ThermalAssetKind.Edge when lineClasses.TryGetValue(
                    item.ClassId, out CommercialLineClassDefinition? lineClass) =>
                    lineClass.ThermalLimit,
                _ => null,
            };
            Require(baseLimit is not null, $"{path} references a nonthermal or unknown class.");
            Require(item.ContinuousKw > 0 && item.EmergencyKw >= item.ContinuousKw,
                $"{path} requires 0 < continuous <= emergency.");
            Require(item.ContinuousKw <= baseLimit!.ContinuousKw &&
                    item.EmergencyKw <= baseLimit.EmergencyKw,
                $"{path} cannot raise a base thermal limit.");
        }
    }

    private static void ValidateFactTemplates(
        CommercialCampaignResultFactTemplatesDefinition templates,
        bool expectsPromise,
        string path)
    {
        ArgumentNullException.ThrowIfNull(templates);
        RequireText(templates.SuppliedLoad, $"{path}.suppliedLoad");
        RequireText(templates.UnservedLoad, $"{path}.unservedLoad");
        ValidateTemplate(
            templates.SuppliedLoad,
            ["phase", "load", "source", "demandKw", "minimumRemainingKw"],
            $"{path}.suppliedLoad");
        ValidateTemplate(
            templates.UnservedLoad,
            ["phase", "load", "demandKw", "deliveredKw"],
            $"{path}.unservedLoad");
        Require((templates.KeptPromise is not null) == expectsPromise &&
                (templates.DeferredPromise is not null) == expectsPromise,
            expectsPromise
                ? $"{path} requires keptPromise/deferredPromise."
                : $"{path} cannot contain keptPromise/deferredPromise.");
        if (expectsPromise)
        {
            RequireText(templates.KeptPromise!, $"{path}.keptPromise");
            RequireText(templates.DeferredPromise!, $"{path}.deferredPromise");
            ValidateTemplate(templates.KeptPromise!, ["promise"], $"{path}.keptPromise");
            ValidateTemplate(templates.DeferredPromise!, ["promise"], $"{path}.deferredPromise");
        }
        RequireText(templates.RemainingCash, $"{path}.remainingCash");
        ValidateTemplate(templates.RemainingCash, ["cashUnit"], $"{path}.remainingCash");
    }

    private static void ValidateTemplate(
        string template,
        IReadOnlyList<string> requiredTokens,
        string path)
    {
        var found = new List<string>();
        int index = 0;
        while (index < template.Length)
        {
            int open = template.IndexOf('{', index);
            if (open < 0)
            {
                Require(template.IndexOf('}', index) < 0, $"{path} has an unmatched closing brace.");
                break;
            }
            Require(template.IndexOf('}', index, open - index) < 0,
                $"{path} has an unmatched closing brace.");
            int close = template.IndexOf('}', open + 1);
            Require(close > open + 1, $"{path} has an invalid placeholder.");
            string token = template[(open + 1)..close];
            Require(!token.Contains('{', StringComparison.Ordinal),
                $"{path} has a nested placeholder.");
            found.Add(token);
            index = close + 1;
        }
        Require(found.SequenceEqual(requiredTokens, StringComparer.Ordinal),
            $"{path} placeholders must be exactly: {string.Join(", ", requiredTokens.Select(token => $"{{{token}}}"))}.");
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

    private static void RequireOrdinalOrder(IReadOnlyList<string> values, string path) =>
        Require(values.SequenceEqual(values.OrderBy(item => item, StringComparer.Ordinal),
                StringComparer.Ordinal),
            $"{path} must use ordinal order.");

    private static CommercialCampaignDefinition Convert(RawCampaign raw) => new(
        raw.SchemaVersion,
        raw.CampaignId,
        raw.DisplayName,
        Seed(raw.InitialSeed),
        raw.Chapters.Select(Chapter).ToArray(),
        Epilogue(raw.Epilogue));

    private static CommercialCampaignEpilogueDefinition Epilogue(RawEpilogue raw) => new(
        raw.DisplayName,
        Story(raw.CityReport),
        Story(raw.MedicalWitness),
        Story(raw.Closing),
        raw.PromiseLines.Select(item =>
            new CommercialCampaignEpiloguePromiseLineDefinition(
                item.ChapterId,
                item.PromiseId,
                item.Kept,
                item.Deferred)).ToArray());

    private static CommercialCoreSeedDefinition Seed(RawSeed raw) => new(
        raw.SeedId,
        raw.StartMinute,
        raw.InitialCashUnit,
        raw.BaseNodeIds,
        raw.BaseEdgeIds,
        raw.ConstructedNodes.Select(Node).ToArray(),
        raw.ConstructedEdges.Select(Edge).ToArray(),
        raw.CoolingAssetIds);

    private static CommercialCampaignChapterDefinition Chapter(RawChapter raw) => new(
        raw.ChapterId,
        raw.DisplayName,
        Story(raw.Briefing),
        raw.Objective,
        raw.TimeAdvanceBeforeChapterMinutes,
        raw.ResetThermalStateBeforeChapter,
        raw.BudgetGrantCashUnit,
        raw.AvailableNodeClassIds,
        raw.AvailableLinePlans.Select(item => new CommercialCampaignLinePlanDefinition(
            item.LineClassId,
            item.PoleClassId)).ToArray(),
        raw.ConnectionRequirements.Select(item => new CommercialCampaignConnectionRequirement(
            item.NodeId,
            item.MinimumConnections)).ToArray(),
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
            raw.ResultCards.Deferred is null ? null : Story(raw.ResultCards.Deferred)),
        new CommercialCampaignResultFactTemplatesDefinition(
            raw.ResultFactTemplates.SuppliedLoad,
            raw.ResultFactTemplates.UnservedLoad,
            raw.ResultFactTemplates.KeptPromise,
            raw.ResultFactTemplates.DeferredPromise,
            raw.ResultFactTemplates.RemainingCash));

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
                    throw new CommercialCampaignValidationException(
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

    private static void RequireText(string value, string path) => RequireId(value, path);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new CommercialCampaignValidationException(message);
        }
    }

    private sealed class RawCampaign
    {
        [JsonRequired] public string SchemaVersion { get; init; } = null!;
        [JsonRequired] public string CampaignId { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public RawSeed InitialSeed { get; init; } = null!;
        [JsonRequired] public RawChapter[] Chapters { get; init; } = null!;
        [JsonRequired] public RawEpilogue Epilogue { get; init; } = null!;
    }

    private sealed class RawEpilogue
    {
        [JsonRequired] public string DisplayName { get; init; } = null!;
        [JsonRequired] public RawStory CityReport { get; init; } = null!;
        [JsonRequired] public RawStory MedicalWitness { get; init; } = null!;
        [JsonRequired] public RawStory Closing { get; init; } = null!;
        [JsonRequired] public RawEpiloguePromiseLine[] PromiseLines { get; init; } = null!;
    }

    private sealed class RawEpiloguePromiseLine
    {
        [JsonRequired] public string ChapterId { get; init; } = null!;
        [JsonRequired] public string PromiseId { get; init; } = null!;
        [JsonRequired] public string Kept { get; init; } = null!;
        [JsonRequired] public string Deferred { get; init; } = null!;
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
        [JsonRequired] public int TimeAdvanceBeforeChapterMinutes { get; init; }
        [JsonRequired] public bool ResetThermalStateBeforeChapter { get; init; }
        [JsonRequired] public long BudgetGrantCashUnit { get; init; }
        [JsonRequired] public string[] AvailableNodeClassIds { get; init; } = null!;
        [JsonRequired] public RawLinePlan[] AvailableLinePlans { get; init; } = null!;
        [JsonRequired] public RawConnectionRequirement[] ConnectionRequirements { get; init; } = null!;
        [JsonRequired] public RawPromise? CityPromise { get; init; }
        [JsonRequired] public RawWindow[] DecisionWindows { get; init; } = null!;
        [JsonRequired] public RawPhase[] OperatingPhases { get; init; } = null!;
        [JsonRequired] public RawResults ResultCards { get; init; } = null!;
        [JsonRequired] public RawFactTemplates ResultFactTemplates { get; init; } = null!;
    }

    private sealed class RawLinePlan
    {
        [JsonRequired] public string LineClassId { get; init; } = null!;
        [JsonRequired] public string PoleClassId { get; init; } = null!;
    }

    private sealed class RawConnectionRequirement
    {
        [JsonRequired] public string NodeId { get; init; } = null!;
        [JsonRequired] public int MinimumConnections { get; init; }
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

    private sealed class RawFactTemplates
    {
        [JsonRequired] public string SuppliedLoad { get; init; } = null!;
        [JsonRequired] public string UnservedLoad { get; init; } = null!;
        [JsonRequired] public string? KeptPromise { get; init; }
        [JsonRequired] public string? DeferredPromise { get; init; }
        [JsonRequired] public string RemainingCash { get; init; } = null!;
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
