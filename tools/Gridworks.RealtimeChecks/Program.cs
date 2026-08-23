using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;

namespace Gridworks.RealtimeChecks;

internal static class Program
{
    private const string Usage =
        "usage: Gridworks.RealtimeChecks [data-directory] [--suite <exact-name>]";

    public static int Main(string[] args)
    {
        try
        {
            string repositoryDirectory = ResolveRepositoryDirectory();
            string? dataArgument;
            string? suiteName;
            if (args.Length == 0)
            {
                dataArgument = null;
                suiteName = null;
            }
            else if (args.Length == 1 && !args[0].StartsWith("--", StringComparison.Ordinal))
            {
                dataArgument = args[0];
                suiteName = null;
            }
            else if (args.Length == 2 && args[0] == "--suite")
            {
                dataArgument = null;
                suiteName = args[1];
            }
            else if (args.Length == 3 && args[1] == "--suite" &&
                     !args[0].StartsWith("--", StringComparison.Ordinal))
            {
                dataArgument = args[0];
                suiteName = args[2];
            }
            else
            {
                throw new ArgumentException(Usage);
            }
            if (suiteName is not null && string.IsNullOrWhiteSpace(suiteName))
            {
                throw new ArgumentException(Usage);
            }

            string dataDirectory = dataArgument is null
                ? Path.Combine(repositoryDirectory, "data")
                : Path.GetFullPath(dataArgument);
            string fixtureDirectory = Path.Combine(
                repositoryDirectory,
                "tools",
                "Gridworks.RealtimeChecks",
                "Fixtures");
            return new Checks(dataDirectory, fixtureDirectory).Run(suiteName);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL startup: {exception.Message}");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static string ResolveRepositoryDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")) &&
                Directory.Exists(Path.Combine(directory.FullName, "data")) &&
                Directory.Exists(Path.Combine(
                    directory.FullName,
                    "tools",
                    "Gridworks.RealtimeChecks",
                    "Fixtures")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException(
            "Gridworks repository root was not found from the realtime checker binary path.");
    }
}

internal sealed class Checks
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false),
        },
    };

    private readonly CommercialWorldDefinition _baseWorld;
    private readonly CommercialCampaignDefinition _baseCampaign;
    private readonly RealtimeWorldDefinition _world;
    private readonly RealtimeCampaignDefinition _campaign;
    private readonly string _worldV3Json;
    private readonly string _campaignV3Json;
    private readonly byte[] _releaseBaseCampaignBytes;
    private readonly byte[] _releaseCampaignOverlayBytes;
    private readonly RealtimeWorldDefinition _releaseWorld;
    private int _assertions;

    public Checks(string dataDirectory, string fixtureDirectory)
    {
        string worldV2Path = Path.Combine(dataDirectory, "release-world-v2.json");
        string campaignV2Path = Path.Combine(dataDirectory, "release-campaign-v2.json");
        string worldV3Path = Path.Combine(
            fixtureDirectory,
            "stage-r1-world-realtime-v3.json");
        string campaignV3Path = Path.Combine(
            fixtureDirectory,
            "stage-r1-first-light-realtime-v3.json");
        string releaseWorldV3Path = Path.Combine(
            dataDirectory,
            "release-world-v3.json");
        string releaseCampaignOverlayPath = Path.Combine(
            dataDirectory,
            "release-campaign-v3.json");
        byte[] worldV2 = File.ReadAllBytes(worldV2Path);
        byte[] campaignV2 = File.ReadAllBytes(campaignV2Path);
        byte[] worldV3 = File.ReadAllBytes(worldV3Path);
        byte[] campaignV3 = File.ReadAllBytes(campaignV3Path);
        byte[] releaseWorldV3 = File.ReadAllBytes(releaseWorldV3Path);
        _baseWorld = CommercialWorldLoader.Load(worldV2);
        _baseCampaign = CommercialCampaignLoader.Load(campaignV2, _baseWorld);
        _worldV3Json = Encoding.UTF8.GetString(worldV3);
        _campaignV3Json = Encoding.UTF8.GetString(campaignV3);
        _world = RealtimeWorldLoader.Load(worldV3, _baseWorld);
        _campaign = RealtimeCampaignLoader.Load(campaignV3, _baseCampaign, _world);
        _releaseBaseCampaignBytes = campaignV2;
        _releaseCampaignOverlayBytes = File.ReadAllBytes(releaseCampaignOverlayPath);
        _releaseWorld = RealtimeWorldLoader.Load(releaseWorldV3, _baseWorld);
    }

    public int Run(string? suiteName = null)
    {
        (string Name, Action Body)[] suites =
        [
            ("strict-v3-loaders-first-light-schedule", StrictLoadersAndSchedule),
            ("strict-release-v2-v3-overlay-composition", StrictReleaseOverlayComposition),
            ("concurrent-same-minute-event-composition", ConcurrentSameMinuteEvents),
            ("atomic-command-and-auto-construction", AtomicCommandAndConstruction),
            ("forecast-actual-same-minute-order", ForecastActualSameMinuteOrder),
            ("first-light-product-slice-bottlenecks", FirstLightProductSliceBottlenecks),
            ("initial-cooling-seed-forecast-actual", InitialCoolingSeedForecastActual),
            ("typed-forecast-nonblocking-outcomes-defaults", ForecastOutcomesAndDefaults),
            ("integer-change-point-chunk-invariance", ChunkInvariance),
            ("frame-speed-canonical-hash", FrameSpeedCanonicalHash),
            ("canonical-future-equivalence", CanonicalFutureEquivalence),
            ("stable-allocation-no-ghost-usage", SupplyAllocation),
            ("allocator-polynomial-layered-graph", AllocatorPolynomialLayeredGraph),
            ("allocator-optimizer-proof-matrix", AllocatorOptimizerProofMatrix),
            ("interval-duty-trip-recover", IntervalDutyTripRecover),
            ("temporal-forecast-trip-recovery-actual", TemporalForecastTripRecoveryActual),
            ("dual-unavailability-causes", DualUnavailabilityCauses),
            ("thermal-authority-atomic-profile", ThermalAuthorityAtomicProfile),
            ("event-end-trip-back-to-back", EventEndTripBackToBack),
            ("pending-trip-construction-same-boundary", PendingTripConstructionBoundary),
            ("pole-bottleneck-exposure-trip-recovery", () => Bottleneck(Archetype.Pole)),
            ("substation-bottleneck-exposure-trip-recovery", () => Bottleneck(Archetype.Substation)),
            ("line-bottleneck-exposure-trip-recovery", () => Bottleneck(Archetype.Line)),
        ];
        if (suiteName is not null)
        {
            int suiteIndex = Array.FindIndex(
                suites,
                suite => string.Equals(suite.Name, suiteName, StringComparison.Ordinal));
            if (suiteIndex < 0)
            {
                throw new ArgumentException(
                    $"Unknown realtime check suite '{suiteName}'. Available suites: " +
                    string.Join(", ", suites.Select(suite => suite.Name)));
            }
            suites = [suites[suiteIndex]];
        }
        var failures = new List<string>();
        foreach ((string name, Action body) in suites)
        {
            try
            {
                body();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures.Add($"{name}: {exception.Message}");
                Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
            }
        }
        if (failures.Count > 0)
        {
            Console.Error.WriteLine(
                $"Gridworks Realtime checks: FAIL ({failures.Count}/{suites.Length} suites)");
            return 1;
        }
        Console.WriteLine(
            $"Gridworks Realtime checks: PASS ({suites.Length} suites, {_assertions} assertions)");
        return 0;
    }

    private void StrictLoadersAndSchedule()
    {
        Equal(1, _campaign.Chapters.Count, "R1 chapter count");
        SequenceEqual(
            CommercialCampaignLoader.CanonicalChapterIds.Take(1),
            _campaign.Chapters.Select(item => item.Content.ChapterId),
            "canonical FIRST_LIGHT prefix");
        Equal(3, _campaign.Chapters[0].ScheduledEvents.Count,
            "FIRST_LIGHT product event count");
        SequenceEqual(
            new[]
            {
                "FIRST_LIGHT_LINE_BOTTLENECK",
                "FIRST_LIGHT_POLE_BOTTLENECK",
                "FIRST_LIGHT_SUBSTATION_BOTTLENECK",
            },
            _campaign.Chapters[0].ScheduledEvents.Select(item => item.EventId),
            "raw permutation was not canonicalized by event time");
        SequenceEqual(new[] { 20, 0, 10 },
            _campaign.Chapters[0].ScheduledEvents.Select(item => item.Priority),
            "later higher-priority event inverted chronological schedule");
        Check(_campaign.Chapters.SelectMany(item => item.ScheduledEvents).All(item =>
                item.DurationMinutes > 0 && item.ForecastLeadMinutes >= 0),
            "schedule duration/forecast lead invariant");
        Equal(6, _world.ThermalClasses.Count, "thermal class coverage");

        Expect<RealtimeWorldValidationException>(() =>
            RealtimeWorldLoader.Load(
                $"{{\"schemaVersion\":\"duplicate\",{_worldV3Json.TrimStart()[1..]}",
                _baseWorld),
            "duplicate realtime world key");
        JsonObject badWorld = ParseObject(_worldV3Json);
        badWorld["unexpected"] = true;
        Expect<RealtimeWorldValidationException>(() =>
            RealtimeWorldLoader.Load(badWorld.ToJsonString(), _baseWorld),
            "unknown realtime world root field");
        JsonObject badClass = ParseObject(_worldV3Json);
        Object(JsonArrayOf(badClass, "thermalClasses")[0]!)["protectiveOutageMinutes"] = 0;
        Expect<RealtimeWorldValidationException>(() =>
            RealtimeWorldLoader.Load(badClass.ToJsonString(), _baseWorld),
            "zero protection outage");
        JsonObject badRecovery = ParseObject(_worldV3Json);
        Object(JsonArrayOf(badRecovery, "thermalClasses")[0]!)[
            "emergencyExposureRecoveryPerMinute"] = 0;
        Expect<RealtimeWorldValidationException>(() =>
            RealtimeWorldLoader.Load(badRecovery.ToJsonString(), _baseWorld),
            "zero exposure recovery rate");
        JsonObject missingThermalClass = ParseObject(_worldV3Json);
        JsonArrayOf(missingThermalClass, "thermalClasses").RemoveAt(0);
        Expect<RealtimeWorldValidationException>(() =>
            RealtimeWorldLoader.Load(missingThermalClass.ToJsonString(), _baseWorld),
            "missing required thermal class");

        JsonObject badCampaign = ParseObject(_campaignV3Json);
        badCampaign["unexpected"] = true;
        Expect<RealtimeCampaignValidationException>(() =>
            RealtimeCampaignLoader.Load(
                badCampaign.ToJsonString(),
                _baseCampaign,
                _world),
            "unknown realtime campaign root field");
        JsonObject badSeed = ParseObject(_campaignV3Json);
        JsonArrayOf(Object(badSeed, "initialSeed"), "baseNodeIds")[0] =
            "UNKNOWN_BASE_NODE";
        Expect<RealtimeCampaignValidationException>(() =>
            RealtimeCampaignLoader.Load(
                badSeed.ToJsonString(),
                _baseCampaign,
                _world),
            "unknown initial seed node");
        JsonObject badTiming = ParseObject(_campaignV3Json);
        Object(JsonArrayOf(
            Object(JsonArrayOf(badTiming, "chapters")[0]!),
            "scheduledEvents")[0]!)["startOffsetMinutes"] = 239;
        Expect<RealtimeCampaignValidationException>(() =>
            RealtimeCampaignLoader.Load(
                badTiming.ToJsonString(),
                _baseCampaign,
                _world),
            "event before preparation window");
        JsonObject missingEvent = ParseObject(_campaignV3Json);
        JsonArrayOf(
            Object(JsonArrayOf(missingEvent, "chapters")[0]!),
            "scheduledEvents").Clear();
        Expect<RealtimeCampaignValidationException>(() =>
            RealtimeCampaignLoader.Load(
                missingEvent.ToJsonString(),
                _baseCampaign,
                _world),
            "missing authored event schedule");
        JsonObject reversedRaw = ParseObject(_campaignV3Json);
        JsonArray reversedEvents = JsonArrayOf(
            Object(JsonArrayOf(reversedRaw, "chapters")[0]!),
            "scheduledEvents");
        JsonNode?[] reverseCopies = reversedEvents
            .Reverse()
            .Select(item => item?.DeepClone())
            .ToArray();
        reversedEvents.Clear();
        foreach (JsonNode? item in reverseCopies)
        {
            reversedEvents.Add(item);
        }
        RealtimeCampaignDefinition reversed = RealtimeCampaignLoader.Load(
            reversedRaw.ToJsonString(),
            _baseCampaign,
            _world);
        Equal(Json(_campaign.Chapters[0].ScheduledEvents),
            Json(reversed.Chapters[0].ScheduledEvents),
            "raw event array order changed canonical schedule");
        JsonObject unknownNested = ParseObject(_campaignV3Json);
        Object(JsonArrayOf(
            Object(JsonArrayOf(unknownNested, "chapters")[0]!),
            "scheduledEvents")[0]!)["unexpected"] = true;
        Expect<RealtimeCampaignValidationException>(() =>
            RealtimeCampaignLoader.Load(
                unknownNested.ToJsonString(),
                _baseCampaign,
                _world),
            "unknown nested schedule field");
        string nestedDuplicate = _campaignV3Json.Replace(
            "\"priority\": 0,",
            "\"priority\": 0, \"priority\": 0,",
            StringComparison.Ordinal);
        Expect<RealtimeCampaignValidationException>(() =>
            RealtimeCampaignLoader.Load(nestedDuplicate, _baseCampaign, _world),
            "duplicate nested schedule field");
        string numericOverflow = _campaignV3Json.Replace(
            "\"startOffsetMinutes\": 240",
            "\"startOffsetMinutes\": 9223372036854775807",
            StringComparison.Ordinal);
        Expect<RealtimeCampaignValidationException>(() =>
            RealtimeCampaignLoader.Load(numericOverflow, _baseCampaign, _world),
            "overflowing schedule integer");

        JsonObject overflowingCash = ParseObject(_campaignV3Json);
        Object(overflowingCash, "initialSeed")["initialCashUnit"] = long.MaxValue;
        Expect<RealtimeCampaignValidationException>(() =>
            RealtimeCampaignLoader.Load(
                overflowingCash.ToJsonString(),
                _baseCampaign,
                _world),
            "initial cash plus authored chapter grant overflow");
    }

    private void StrictReleaseOverlayComposition()
    {
        RealtimeCampaignOverlayLoadResult all =
            RealtimeCampaignOverlayLoader.LoadAll(
                _releaseBaseCampaignBytes,
                _releaseCampaignOverlayBytes,
                _releaseWorld);
        RealtimeCampaignOverlayLoadResult first =
            RealtimeCampaignOverlayLoader.LoadFirstLight(
                _releaseBaseCampaignBytes,
                _releaseCampaignOverlayBytes,
                _releaseWorld);
        RealtimeCampaignOverlayLoadResult repeatedFirst =
            RealtimeCampaignOverlayLoader.LoadPrefix(
                _releaseBaseCampaignBytes,
                _releaseCampaignOverlayBytes,
                _releaseWorld,
                chapterCount: 1);

        Equal(8, all.Campaign.Chapters.Count,
            "release overlay complete chapter count");
        SequenceEqual(
            CommercialCampaignLoader.CanonicalChapterIds,
            all.Campaign.Chapters.Select(item => item.Content.ChapterId),
            "release overlay canonical eight-chapter composition");
        SequenceEqual(
            new[]
            {
                "FIRST_LIGHT:FIRST_LIGHT_SUPPLY",
                "SECOND_HEART:HOSPITAL_TRANSFER_TEST,FLOOD_ISOLATION_TEST",
                "SECOND_SOURCE:WEST_MAIN_COMMISSIONING_TEST," +
                    "SOUTH_SOURCE_COMMISSIONING_TEST",
                "NORTH_BANK_PROMISE:NORTH_BANK_COMMISSIONING," +
                    "NEXT_HOT_EVENING_FORECAST",
                "WHOSE_MARGIN:HOT_BASE,NIGHT_SHIFT,LATE_NIGHT",
                "BEFORE_WATER_RISE:FLOOD_ARRIVAL",
                "SWITCH_OFF_TO_PROTECT:WEST_SOURCE_PLANNED_OUTAGE," +
                    "WEST_SOURCE_RETURN_SERVICE",
                "LONGEST_NIGHT:MAX_DEMAND,HEATWAVE_PEAK,PROTECTIVE_STOP_FLOOD",
            },
            all.Campaign.Chapters.Select(item =>
                $"{item.Content.ChapterId}:" +
                string.Join(",", item.ScheduledEvents.Select(scheduled =>
                    scheduled.EventId))),
            "release overlay exact authored event composition");
        Equal(16, all.Campaign.Chapters.Sum(item => item.ScheduledEvents.Count),
            "release overlay complete event count");

        Equal(1, first.Campaign.Chapters.Count,
            "release overlay FIRST_LIGHT prefix chapter count");
        RealtimeChapterDefinition firstChapter = first.Campaign.Chapters.Single();
        RealtimeScheduledEventDefinition firstEvent =
            firstChapter.ScheduledEvents.Single();
        Equal(RealtimeCampaignOverlayLoader.FirstReleaseChapterId,
            firstChapter.Content.ChapterId,
            "release overlay FIRST_LIGHT prefix chapter identity");
        Equal(RealtimeCampaignOverlayLoader.FirstReleaseEventId,
            firstEvent.EventId,
            "release overlay FIRST_LIGHT prefix event identity");
        Equal(240, firstChapter.PreparationMinutes,
            "release overlay FIRST_LIGHT preparation");
        Equal(null, firstChapter.PromiseDecisionDeadlineOffsetMinutes,
            "release overlay FIRST_LIGHT promise deadline");
        Equal(0, firstEvent.Priority,
            "release overlay FIRST_LIGHT event priority");
        Equal(240, firstEvent.StartOffsetMinutes,
            "release overlay FIRST_LIGHT event start");
        Equal(60, firstEvent.DurationMinutes,
            "release overlay FIRST_LIGHT event duration");
        Equal(240, firstEvent.ForecastLeadMinutes,
            "release overlay FIRST_LIGHT event forecast lead");
        Check(ReferenceEquals(first.Campaign.Content.Chapters[0], firstChapter.Content),
            "release overlay prefix lost raw V2 authored chapter authority");
        CommercialOperatingPhaseDefinition authoredFirstPhase = first.Campaign.Content
            .Chapters[0]
            .OperatingPhases
            .Single();
        Equal(Json(authoredFirstPhase), Json(firstEvent.OperatingProfile),
            "release overlay FIRST_LIGHT event drifted from authored V2 phase");
        Equal(Json(all.Campaign.Chapters[0]), Json(firstChapter),
            "release overlay FIRST_LIGHT prefix differs from complete composition");
        Equal(Json(first.Campaign.Chapters), Json(repeatedFirst.Campaign.Chapters),
            "release overlay FIRST_LIGHT repeated composition");

        Equal(
            "078df95f9f0c833be7e1a299088b4ab6e0de4ddf13426ce5b96a1abbeee70b7a",
            all.SourceIdentity.BaseCampaignSha256,
            "release overlay exact raw V2 source hash");
        Equal(
            "ef962a272683bfd6761fbf10a0ca14cb6c8bf90cdfde810b468ad451088f2258",
            all.SourceIdentity.RealtimeOverlaySha256,
            "release overlay exact raw V3 source hash");
        Equal(
            "7bd151399040934cfcb9f7c96d2879aef6354cda79ced2af184641eb33a02f09",
            all.SourceIdentity.FullComposedCampaignSha256,
            "release overlay exact full composed hash");
        Equal(
            "94379c0e8e4dae54b760a55df8c1143c975eaa12f11079e675b2e67ba57df88e",
            first.SourceIdentity.SelectedComposedCampaignSha256,
            "release overlay exact FIRST_LIGHT prefix hash");
        Equal(8, all.SourceIdentity.FullChapterCount,
            "release overlay source identity full chapter count");
        Equal(8, all.SourceIdentity.SelectedChapterCount,
            "release overlay source identity all selected chapter count");
        Equal(1, first.SourceIdentity.SelectedChapterCount,
            "release overlay source identity FIRST_LIGHT selected chapter count");
        Equal(all.SourceIdentity.FullComposedCampaignSha256,
            first.SourceIdentity.FullComposedCampaignSha256,
            "release overlay full composition hash changed for prefix selection");
        Equal(first.SourceIdentity, repeatedFirst.SourceIdentity,
            "release overlay repeated source identity");
        Check(!string.Equals(
                first.SourceIdentity.SelectedComposedCampaignSha256,
                all.SourceIdentity.SelectedComposedCampaignSha256,
                StringComparison.Ordinal),
            "release overlay prefix source hash aliases the complete composition");
        Equal(all.SourceIdentity.FullComposedCampaignSha256,
            all.SourceIdentity.SelectedComposedCampaignSha256,
            "release overlay full selection hash identity");

        string overlayJson = Encoding.UTF8.GetString(_releaseCampaignOverlayBytes);
        string trimmedOverlay = overlayJson.TrimStart();
        Expect<RealtimeCampaignOverlayValidationException>(() =>
            RealtimeCampaignOverlayLoader.LoadAll(
                _releaseBaseCampaignBytes,
                Encoding.UTF8.GetBytes(
                    $"{{\"schemaVersion\":\"duplicate\",{trimmedOverlay[1..]}"),
                _releaseWorld),
            "release overlay duplicate root key");
        JsonObject unexpectedOverlay = ParseObject(overlayJson);
        unexpectedOverlay["unexpected"] = true;
        Expect<RealtimeCampaignOverlayValidationException>(() =>
            RealtimeCampaignOverlayLoader.LoadAll(
                _releaseBaseCampaignBytes,
                Encoding.UTF8.GetBytes(unexpectedOverlay.ToJsonString()),
                _releaseWorld),
            "release overlay unknown root field");
        JsonObject missingEventOverlay = ParseObject(overlayJson);
        JsonArrayOf(Object(JsonArrayOf(missingEventOverlay, "chapters")[1]!),
            "scheduledEvents").RemoveAt(1);
        Expect<RealtimeCampaignOverlayValidationException>(() =>
            RealtimeCampaignOverlayLoader.LoadAll(
                _releaseBaseCampaignBytes,
                Encoding.UTF8.GetBytes(missingEventOverlay.ToJsonString()),
                _releaseWorld),
            "release overlay incomplete authored phase coverage");
        JsonObject unexpectedBase = ParseObject(
            Encoding.UTF8.GetString(_releaseBaseCampaignBytes));
        unexpectedBase["unexpected"] = true;
        Expect<RealtimeCampaignOverlayValidationException>(() =>
            RealtimeCampaignOverlayLoader.LoadAll(
                Encoding.UTF8.GetBytes(unexpectedBase.ToJsonString()),
                _releaseCampaignOverlayBytes,
                _releaseWorld),
            "release overlay invalid raw V2 source");
        Expect<ArgumentOutOfRangeException>(() =>
            RealtimeCampaignOverlayLoader.LoadPrefix(
                _releaseBaseCampaignBytes,
                _releaseCampaignOverlayBytes,
                _releaseWorld,
                chapterCount: 0),
            "release overlay empty prefix");
        Expect<ArgumentOutOfRangeException>(() =>
            RealtimeCampaignOverlayLoader.LoadPrefix(
                _releaseBaseCampaignBytes,
                _releaseCampaignOverlayBytes,
                _releaseWorld,
                chapterCount: 9),
            "release overlay noncanonical overlong prefix");
    }

    private void ConcurrentSameMinuteEvents()
    {
        JsonObject raw = ParseObject(_campaignV3Json);
        JsonArray sourceEvents = JsonArrayOf(
            Object(JsonArrayOf(raw, "chapters")[0]!),
            "scheduledEvents");

        JsonObject CloneEvent(
            string sourceId,
            string eventId,
            string displayName,
            int priority)
        {
            JsonObject source = sourceEvents
                .Select(node => Object(node!))
                .Single(item => string.Equals(
                    item["eventId"]?.GetValue<string>(),
                    sourceId,
                    StringComparison.Ordinal));
            JsonObject clone = ParseObject(source.ToJsonString());
            clone["eventId"] = eventId;
            clone["displayName"] = displayName;
            clone["priority"] = priority;
            clone["startOffsetMinutes"] = 240;
            clone["durationMinutes"] = 4;
            clone["forecastLeadMinutes"] = 240;
            return clone;
        }

        JsonObject c = CloneEvent(
            "FIRST_LIGHT_POLE_BOTTLENECK",
            "CONCURRENT_C",
            "동시 사건 C",
            0);
        c["unavailableEdgeIds"] = new JsonArray("SLICE_POLE_LOAD");
        JsonObject a = CloneEvent(
            "FIRST_LIGHT_LINE_BOTTLENECK",
            "CONCURRENT_A",
            "동시 사건 A",
            1);
        a["unavailableNodeIds"] = new JsonArray("SLICE_LINE_SUBSTATION");
        JsonObject b = CloneEvent(
            "FIRST_LIGHT_SUBSTATION_BOTTLENECK",
            "CONCURRENT_B",
            "동시 사건 B",
            1);
        b["thermalLimitOverrides"] = new JsonArray(new JsonObject
        {
            ["assetKind"] = "node",
            ["classId"] = "SMALL_SUBSTATION",
            ["continuousKw"] = 900,
            ["emergencyKw"] = 2100,
        });

        // Deliberately reverse the canonical C,A,B order in the authored JSON.
        sourceEvents.Clear();
        sourceEvents.Add(b);
        sourceEvents.Add(a);
        sourceEvents.Add(c);
        RealtimeCampaignDefinition definition = RealtimeCampaignLoader.Load(
            raw.ToJsonString(),
            _baseCampaign,
            _world);
        SequenceEqual(
            new[] { "CONCURRENT_C", "CONCURRENT_A", "CONCURRENT_B" },
            definition.Chapters[0].ScheduledEvents.Select(item => item.EventId),
            "same-minute raw reverse was not canonicalized by priority then ID");

        var run = new RealtimeCampaignRun(definition, _world);
        SequenceEqual(
            new[] { "CONCURRENT_C", "CONCURRENT_A", "CONCURRENT_B" },
            run.GetForecast().Events.Select(item => item.EventId),
            "same-minute horizon priority/ID order");
        RealtimeAdvanceResult started = run.AdvanceTo(1260);
        SequenceEqual(
            new[] { "CONCURRENT_C", "CONCURRENT_A", "CONCURRENT_B" },
            started.Transitions
                .Where(item => item.Kind == RealtimeTransitionKind.EventStarted)
                .Select(item => item.EventId!),
            "same-minute start transition priority/ID order");
        SequenceEqual(
            new[] { "CONCURRENT_C", "CONCURRENT_A", "CONCURRENT_B" },
            started.Snapshot.ActiveEventStates.Select(item => item.EventId),
            "typed concurrent active state order");
        Equal("CONCURRENT_C", started.Snapshot.ActiveEvent?.EventId,
            "legacy singular active event is not priority-first compatibility view");
        SequenceEqual(
            new[] { "HOSPITAL", "EAST_RESIDENTIAL", "WATERWORKS" },
            started.Snapshot.Thermal.Evaluation.Loads.Select(item => item.LoadId),
            "composed dispatch did not use event priority/ID after obligation priority");
        Check(started.Snapshot.Thermal.Assets.Single(item =>
                    item.AssetId == "SLICE_POLE_LOAD").AuthoredUnavailable &&
                started.Snapshot.Thermal.Assets.Single(item =>
                    item.AssetId == "SLICE_LINE_SUBSTATION").AuthoredUnavailable,
            "concurrent authored unavailability was not unioned");
        ThermalAssetUsage overridden = started.Snapshot.Thermal.Evaluation.Assets.Single(item =>
            item.AssetId == "SLICE_SUBSTATION_TARGET");
        Check(overridden.ContinuousKw == 900 && overridden.EmergencyKw == 2100,
            "concurrent thermal override was not composed");
        Check(run.GetForecast().Events.All(item =>
                item.Status == RealtimeForecastStatus.Active),
            "concurrent horizon did not mark every event active");
        string fullHash = started.CanonicalStateSha256;
        string missingActiveHash = RealtimeStateCanonicalizer.Sha256(started.Snapshot with
        {
            ActiveEventStates = started.Snapshot.ActiveEventStates.Skip(1).ToArray(),
        });
        Check(!string.Equals(fullHash, missingActiveHash, StringComparison.Ordinal),
            "concurrent active state list is not canonical-hash visible");

        RealtimeAdvanceResult completed = run.AdvanceTo(1264);
        RealtimeChapterOutcome chapter = completed.Snapshot.CompletedChapters.Single();
        SequenceEqual(
            new[] { "CONCURRENT_C", "CONCURRENT_A", "CONCURRENT_B" },
            completed.Transitions
                .Where(item => item.Kind == RealtimeTransitionKind.EventCompleted)
                .Select(item => item.EventId!),
            "same-minute completion transition priority/ID order");
        SequenceEqual(
            new[] { "CONCURRENT_C", "CONCURRENT_A", "CONCURRENT_B" },
            chapter.Events.Select(item => item.EventId),
            "independent concurrent outcomes lost canonical completion order");
        Dictionary<string, RealtimeEventOutcome> outcomes = chapter.Events.ToDictionary(
            item => item.EventId,
            StringComparer.Ordinal);
        Check(outcomes["CONCURRENT_C"].FirstSafetyUnservedMinute == 1260 &&
              outcomes["CONCURRENT_A"].FirstSafetyUnservedMinute == 1260 &&
              outcomes["CONCURRENT_B"].FirstSafetyUnservedMinute == 1262 &&
              outcomes["CONCURRENT_C"].DutySegments.SelectMany(item => item.Loads)
                  .All(item => item.LoadId == "HOSPITAL") &&
              outcomes["CONCURRENT_A"].DutySegments.SelectMany(item => item.Loads)
                  .All(item => item.LoadId == "EAST_RESIDENTIAL") &&
              outcomes["CONCURRENT_B"].DutySegments.SelectMany(item => item.Loads)
                  .All(item => item.LoadId == "WATERWORKS"),
            "concurrent events did not retain independent duty truth");
    }

    private void AtomicCommandAndConstruction()
    {
        var run = new RealtimeCampaignRun(_campaign, _world);
        long start = run.GetSnapshot().Minute;
        RealtimeCommand draftCommand = RealtimeCommand.SetNodeDraft(
            "SMALL_SUBSTATION",
            new MapPoint(2800, 1050));
        string pristine = Json(run.GetSnapshot());
        RealtimeCommandResult stale = run.ApplyCommand(start - 1, 1, draftCommand);
        Equal(RealtimeRunError.ClockMismatch, stale.Error, "stale command error");
        Equal(pristine, Json(run.GetSnapshot()), "stale command mutated state");
        Equal(RealtimeStateCanonicalizer.Sha256(stale.Snapshot),
            stale.CanonicalStateSha256,
            "rejected command result canonical hash");
        RealtimeCommandResult skipped = run.ApplyCommand(start, 2, draftCommand);
        Equal(RealtimeRunError.SequenceMismatch, skipped.Error, "skipped sequence error");
        Equal(pristine, Json(run.GetSnapshot()), "skipped sequence mutated state");

        RealtimeCommandResult draft = run.ApplyCommand(start, 1, draftCommand);
        Check(draft.Accepted, $"node draft rejected: {draft.Error}/{draft.ConstructionError}");
        Equal(start, draft.Snapshot.Minute, "command advanced time");
        Equal(run.GetCanonicalStateSha256(), draft.CanonicalStateSha256,
            "command result canonical hash does not describe committed state");
        Equal(RealtimeStateCanonicalizer.Sha256(draft.Snapshot),
            draft.CanonicalStateSha256,
            "command result canonical hash was not computed atomically");
        RealtimeCommandResult changedResult = draft with
        {
            Snapshot = draft.Snapshot with
            {
                CashUnit = checked(draft.Snapshot.CashUnit + 1),
            },
        };
        Equal(RealtimeStateCanonicalizer.Sha256(changedResult.Snapshot),
            changedResult.CanonicalStateSha256,
            "command result with-snapshot retained a stale canonical hash");
        Check(!string.Equals(
                draft.CanonicalStateSha256,
                changedResult.CanonicalStateSha256,
                StringComparison.Ordinal),
            "command result with-snapshot did not change canonical hash");
        var replacementTransitions = new List<RealtimeTransition>
        {
            new(start, RealtimeTransitionKind.ChapterStarted, "VALUE_TEST"),
        };
        RealtimeCommandResult changedTransitions = draft with
        {
            Transitions = replacementTransitions,
        };
        replacementTransitions.Clear();
        Equal(draft.CanonicalStateSha256, changedTransitions.CanonicalStateSha256,
            "transition-only result clone changed state hash");
        Equal(1, changedTransitions.Transitions.Count,
            "result retained mutable transition input");
        Equal(RealtimeStateCanonicalizer.Sha256(draft.Snapshot),
            draft.CanonicalStateSha256,
            "result clone mutated original canonical hash");
        SequenceEqual(
            new[]
            {
                RealtimeTransitionKind.ChapterStarted,
                RealtimeTransitionKind.ForecastRevealed,
                RealtimeTransitionKind.ForecastRevealed,
                RealtimeTransitionKind.ForecastRevealed,
            },
            draft.Transitions.Select(item => item.Kind),
            "initial chapter/forecast transitions were lost or reordered");
        SequenceEqual(
            new string?[]
            {
                null,
                "FIRST_LIGHT_POLE_BOTTLENECK",
                "FIRST_LIGHT_SUBSTATION_BOTTLENECK",
                "FIRST_LIGHT_LINE_BOTTLENECK",
            },
            draft.Transitions.Select(item => item.EventId),
            "same-minute forecast reveals did not use priority then stable ID");
        RealtimeProjectQuote quote = run.PreviewNodeOrder();
        Check(quote.Accepted && quote.BuildMinutes is > 0 && quote.CompletionMinute.HasValue,
            $"node quote rejected: {quote.Error}/{quote.ConstructionError}");
        string drafted = Json(run.GetSnapshot());
        RealtimeCommandResult duplicate = run.ApplyCommand(
            start,
            1,
            RealtimeCommand.OrderNode());
        Equal(RealtimeRunError.SequenceMismatch, duplicate.Error,
            "duplicate sequence error");
        Equal(drafted, Json(run.GetSnapshot()), "duplicate sequence mutated state");
        RealtimeCommandResult order = run.ApplyCommand(
            start,
            2,
            RealtimeCommand.OrderNode());
        Check(order.Accepted, $"node order rejected: {order.Error}/{order.ConstructionError}");
        Equal(0, order.Transitions.Count,
            "initial transitions were delivered more than once");
        Equal(start, order.Snapshot.Minute, "order advanced time");
        Equal(2, run.AcceptedCommands.Count, "accepted command count");
        Equal(1L, run.AcceptedCommands[0].Sequence, "first command sequence");
        Equal(2L, run.AcceptedCommands[1].Sequence, "second command sequence");
        Equal(start, run.AcceptedCommands[1].Minute, "same-minute command timestamp");

        RealtimeCommandResult comparisonStart = run.ApplyCommand(
            start,
            3,
            RealtimeCommand.StartLineDraft(
                "SLICE_LINE_WEST_BANK",
                "STANDARD_LINE",
                "STANDARD_POLE"));
        Check(comparisonStart.Accepted, "comparison line start rejected during active project");
        RealtimeCommandResult comparisonFinish = run.ApplyCommand(
            start,
            4,
            RealtimeCommand.FinishLineDraft("SLICE_LINE_EAST_BANK"));
        Check(comparisonFinish.Accepted, "comparison line finish rejected during active project");
        Check(comparisonFinish.Snapshot.Construction.ActiveConstruction is not null &&
                comparisonFinish.Snapshot.Construction.LineDraft?.EndNodeId is not null,
            "active project and comparison draft did not coexist");
        string beforeSecondOrder = Json(run.GetSnapshot());
        string beforeComparisonHash = run.GetCanonicalStateSha256();
        RealtimeComparisonDraftForecast comparison = run.GetComparisonDraftForecast();
        Check(comparison.Available && comparison.DraftKind == ConstructionKind.Line &&
                comparison.Forecast is not null,
            "typed comparison-draft forecast unavailable");
        RealtimeForecastSnapshot comparisonForecast = comparison.Forecast ??
            throw new InvalidOperationException("comparison forecast missing");
        RealtimeForecastEvent virtualLine = comparisonForecast.Events.Single(item =>
            item.EventId == "FIRST_LIGHT_LINE_BOTTLENECK");
        Equal(2000L, virtualLine.ProjectedEvaluation.Loads.Single().DeliveredKw,
            "virtual comparison commissioning did not supply line event");
        Equal(beforeSecondOrder, Json(run.GetSnapshot()),
            "comparison-draft forecast mutated campaign state");
        Equal(beforeComparisonHash, run.GetCanonicalStateSha256(),
            "comparison-draft forecast mutated canonical state");
        RealtimeCommandResult secondOrder = run.ApplyCommand(
            start,
            5,
            RealtimeCommand.OrderLine());
        Equal(RealtimeRunError.ConstructionRejected, secondOrder.Error,
            "second active order error");
        Equal(ConstructionError.WrongPhase, secondOrder.ConstructionError,
            "second active order typed construction error");
        Equal(beforeSecondOrder, Json(run.GetSnapshot()),
            "second active order mutated state or created a queue");
        Equal(4, run.AcceptedCommands.Count, "rejected second order entered journal");

        long completion = quote.CompletionMinute!.Value;
        RealtimeAdvanceResult before = run.AdvanceTo(completion - 1);
        SpatialNodeDefinition newNode = before.Snapshot.Construction.World.Nodes.Single(item =>
            item.NodeId.StartsWith("PLAYER_SUBSTATION_", StringComparison.Ordinal));
        Check(!newNode.Commissioned, "construction commissioned before its change point");
        RealtimeAdvanceResult at = run.AdvanceTo(completion);
        newNode = at.Snapshot.Construction.World.Nodes.Single(item =>
            item.NodeId.StartsWith("PLAYER_SUBSTATION_", StringComparison.Ordinal));
        Check(newNode.Commissioned, "construction did not auto-complete");
        Equal(ConstructionPhase.LineDrafting, at.Snapshot.Construction.Phase,
            "completion did not preserve comparison draft");
        Check(at.Snapshot.Construction.LineDraft is not null &&
                at.Snapshot.Construction.ActiveConstruction is null,
            "completion deleted draft or retained hidden queue");
        Check(at.Transitions.Any(item =>
                item.Kind == RealtimeTransitionKind.ConstructionCompleted &&
                item.Minute == completion),
            "construction completion transition missing");

        run.AdvanceTo(1248);
        RealtimeProjectQuote lineQuote = run.PreviewLineOrder();
        Check(lineQuote.Accepted && lineQuote.CompletionMinute == 1260,
            "comparison line did not retain exact JIT quote");
        Accepted(run.ApplyCommand(1248, 5, RealtimeCommand.OrderLine()),
            "comparison line order after crew release");
        RealtimeAdvanceResult lineStart = run.AdvanceTo(1260);
        Check(lineStart.Snapshot.Construction.World.Edges.Single(item =>
                item.EdgeId == "PLAYER_EDGE_1").Commissioned,
            "actual comparison line did not commission");
        RealtimeAdvanceResult lineEnd = run.AdvanceTo(1266);
        RealtimeEventOutcome actual = lineEnd.Snapshot.CurrentChapterEvents.Single();
        Equal(Json(virtualLine.TemporalProjection.Outcome), Json(actual),
            "virtual comparison outcome did not equal later actual completion");
    }

    private void ForecastActualSameMinuteOrder()
    {
        var run = new RealtimeCampaignRun(_campaign, _world);
        StartJitLine(run);
        RealtimeForecastEvent forecast = run.GetForecast().Events.Single(item =>
            item.EventId == "FIRST_LIGHT_LINE_BOTTLENECK");
        Check(forecast.ProjectedEvaluation.Assets.Any(item =>
                item.AssetId == "PLAYER_EDGE_1"),
            "forecast clone omitted construction completing at event start");
        Equal(3, forecast.TemporalProjection.Intervals.Count,
            "line event temporal forecast interval count");
        Equal(1260L, forecast.TemporalProjection.Intervals[0].StartMinute,
            "temporal forecast start minute");
        Equal(1266L, forecast.TemporalProjection.Intervals[^1].EndMinute,
            "temporal forecast end minute");

        RealtimeAdvanceResult atStart = run.AdvanceTo(1260);
        int commissioned = IndexOf(atStart.Transitions, item =>
            item.Kind == RealtimeTransitionKind.ConstructionCompleted);
        int eventStarted = IndexOf(atStart.Transitions, item =>
            item.Kind == RealtimeTransitionKind.EventStarted);
        Check(commissioned >= 0 && eventStarted > commissioned,
            "same-minute transition order was not commissioning then event start");
        Check(atStart.Snapshot.Construction.World.Edges.Single(item =>
                item.EdgeId == "PLAYER_EDGE_1").Commissioned,
            "same-minute event saw uncommissioned project");
        Equal(Json(forecast.ProjectedEvaluation), Json(atStart.Snapshot.Thermal.Evaluation),
            "pure forecast did not equal actual event-start dispatch/state");

        RealtimeAdvanceResult ended = run.AdvanceTo(1266);
        RealtimeEventOutcome outcome = ended.Snapshot.CurrentChapterEvents.Single();
        Equal(Json(forecast.ProjectedEvaluation), Json(outcome.FinalEvaluation),
            "forecast did not equal recovered final outcome");
        Equal(Json(forecast.TemporalProjection.Outcome.DutySegments),
            Json(outcome.DutySegments),
            "temporal forecast duty did not equal actual event duty");
        Equal(Json(forecast.TemporalProjection.Outcome), Json(outcome),
            "complete temporal forecast outcome did not equal campaign actual");
        Equal(1262L, outcome.FirstSafetyUnservedMinute,
            "event first unserved minute");
        Equal(3L, outcome.SafetyUnservedMinutes,
            "event accumulated unserved minutes");
        Equal(3, outcome.DutySegments.Count, "trip/recover event duty segment count");
        Check(outcome.Incidents.Any(item =>
                item.AssetId == "PLAYER_EDGE_1" &&
                item.Minute == 1260 &&
                item.Kind == RealtimeThermalTransitionKind.EmergencyEntered),
            "campaign forecast/actual omitted event-start emergency transition");
    }

    private void FirstLightProductSliceBottlenecks()
    {
        var run = new RealtimeCampaignRun(_campaign, _world);
        StartJitLine(run);
        Dictionary<string, RealtimeForecastEvent> forecast = run.GetForecast().Events
            .ToDictionary(item => item.EventId, StringComparer.Ordinal);
        Equal(3, forecast.Count, "FIRST_LIGHT typed product forecasts");

        run.AdvanceTo(1264);
        RealtimeForecastEvent active = run.GetForecast().Events.Single(item =>
            item.Status == RealtimeForecastStatus.Active);
        Check(!active.TemporalProjection.Outcome.SafetySatisfied &&
                active.TemporalProjection.Outcome.FirstSafetyUnservedMinute == 1262,
            "mid-event forecast erased already accumulated failure");

        RealtimeAdvanceResult final = run.AdvanceTo(FinalMinute());
        IReadOnlyList<RealtimeEventOutcome> outcomes =
            final.Snapshot.CompletedChapters.Single().Events;
        Equal(3, outcomes.Count, "FIRST_LIGHT product outcomes");
        var targetIds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["FIRST_LIGHT_LINE_BOTTLENECK"] = "PLAYER_EDGE_1",
            ["FIRST_LIGHT_POLE_BOTTLENECK"] = "SLICE_POLE_TARGET",
            ["FIRST_LIGHT_SUBSTATION_BOTTLENECK"] = "SLICE_SUBSTATION_TARGET",
        };
        var targetKinds = new Dictionary<string, ThermalAssetKind>(StringComparer.Ordinal)
        {
            ["FIRST_LIGHT_LINE_BOTTLENECK"] = ThermalAssetKind.Edge,
            ["FIRST_LIGHT_POLE_BOTTLENECK"] = ThermalAssetKind.Node,
            ["FIRST_LIGHT_SUBSTATION_BOTTLENECK"] = ThermalAssetKind.Node,
        };
        var targetClasses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["FIRST_LIGHT_LINE_BOTTLENECK"] = "STANDARD_LINE",
            ["FIRST_LIGHT_POLE_BOTTLENECK"] = "STANDARD_POLE",
            ["FIRST_LIGHT_SUBSTATION_BOTTLENECK"] = "SMALL_SUBSTATION",
        };
        foreach (RealtimeEventOutcome outcome in outcomes)
        {
            RealtimeForecastEvent projected = forecast[outcome.EventId];
            string targetId = targetIds[outcome.EventId];
            ThermalAssetKind targetKind = targetKinds[outcome.EventId];
            string targetClass = targetClasses[outcome.EventId];
            Equal(Json(projected.TemporalProjection.Outcome), Json(outcome),
                $"{outcome.EventId} product forecast differs from actual");
            RealtimeThermalAssetSnapshot targetAsset = projected.TemporalProjection
                .Intervals[0].Assets.Single(item => item.AssetId == targetId);
            Equal(targetKind, targetAsset.AssetKind,
                $"{outcome.EventId} bottleneck asset kind");
            Equal(targetClass, targetAsset.ClassId,
                $"{outcome.EventId} bottleneck class identity");
            ThermalLoadSupply supplied = projected.TemporalProjection.Intervals[0]
                .Evaluation.Loads.Single();
            Check(targetKind == ThermalAssetKind.Edge
                    ? supplied.PathEdgeIds.Contains(targetId, StringComparer.Ordinal)
                    : supplied.PathNodeIds.Contains(targetId, StringComparer.Ordinal),
                $"{outcome.EventId} selected path omitted its bottleneck asset");
            SequenceEqual(
                new[] { targetId },
                projected.TemporalProjection.Intervals[0].Assets
                    .Where(item => item.State == ThermalOperatingState.Emergency)
                    .Select(item => item.AssetId),
                $"{outcome.EventId} did not author exactly one first bottleneck archetype");
            Check(projected.TemporalProjection.Transitions.Any(item =>
                    item.AssetId == targetId &&
                    item.Kind == RealtimeThermalTransitionKind.ProtectiveTrip) &&
                  projected.TemporalProjection.Transitions.Any(item =>
                    item.AssetId == targetId &&
                    item.Kind == RealtimeThermalTransitionKind.Recovered),
                $"{outcome.EventId} lacks forecast trip/recovery");
            RealtimeDutyLoadFact unavailable = outcome.DutySegments
                .SelectMany(item => item.Loads)
                .Single(item => item.Failure?.AssetId == targetId);
            ThermalSupplyFailure failure = unavailable.Failure ??
                throw new InvalidOperationException(
                    $"{outcome.EventId} protective-outage failure missing");
            Equal(ThermalFailureKind.AssetUnavailable, failure.Kind,
                $"{outcome.EventId} exact outage failure kind");
            Equal(2000L, failure.RequiredKw,
                $"{outcome.EventId} exact outage required kW");
            Equal(0L, failure.AvailableKw,
                $"{outcome.EventId} exact outage available kW");
            Equal(3L, outcome.SafetyUnservedMinutes,
                $"{outcome.EventId} exact outage duty minutes");
        }
        Equal(Json(active.TemporalProjection.Outcome), Json(outcomes[0]),
            "active forecast did not merge past duty into final outcome");
    }

    private void InitialCoolingSeedForecastActual()
    {
        var run = new RealtimeCampaignRun(_campaign, _world);
        RealtimeCampaignSnapshot initial = run.GetSnapshot();
        RealtimeThermalAssetSnapshot seeded = initial.Thermal.Assets.Single(item =>
            item.AssetId == "SLICE_POLE_TARGET");
        Check(seeded.ProtectiveOutage,
            "initial seed cooling asset was not in protective outage");
        Equal(1023L, seeded.ProtectiveOutageUntilMinute,
            "initial seed cooling recovery minute");
        RealtimeForecastEvent forecast = initial.Forecast.Events.Single(item =>
            item.EventId == "FIRST_LIGHT_POLE_BOTTLENECK");

        RealtimeAdvanceResult recovery = run.AdvanceTo(1023);
        Check(recovery.Transitions.Any(item =>
                item.Minute == 1023 &&
                item.Kind == RealtimeTransitionKind.ThermalRecovered &&
                item.AssetId == "SLICE_POLE_TARGET"),
            "authored initial protective outage did not recover automatically");
        Check(!recovery.Snapshot.Thermal.Assets.Single(item =>
                item.AssetId == "SLICE_POLE_TARGET").ProtectiveOutage,
            "initial seed cooling asset stayed unavailable after recovery");

        RealtimeAdvanceResult ended = run.AdvanceTo(1278);
        RealtimeEventOutcome actual = ended.Snapshot.CurrentChapterEvents.Single(item =>
            item.EventId == "FIRST_LIGHT_POLE_BOTTLENECK");
        Equal(Json(forecast.TemporalProjection.Outcome), Json(actual),
            "initial cooling seed forecast differed from actual recovered pole event");
    }

    private void ForecastOutcomesAndDefaults()
    {
        var run = new RealtimeCampaignRun(_campaign, _world);
        RealtimeForecastSnapshot forecast = run.GetForecast();
        Check(forecast.Events.Count > 0, "initial typed forecast empty");
        Equal("FIRST_LIGHT_LINE_BOTTLENECK", forecast.Events[0].EventId,
            "initial forecast event");
        Check(forecast.Events[0].ProjectedEvaluation is not null,
            "forecast has no typed projection");

        long finalMinute = FinalMinute();
        RealtimeAdvanceResult result = run.AdvanceTo(finalMinute);
        Check(result.Snapshot.CampaignComplete, "campaign did not finish automatically");
        Equal(1, result.Snapshot.CompletedChapters.Count,
            "nonblocking outcome did not complete FIRST_LIGHT");
        Equal(3, result.Snapshot.CompletedChapters[0].Events.Count,
            "nonblocking event outcomes did not preserve full slice");
        Check(result.Snapshot.CompletedChapters.SelectMany(item => item.Events)
                .Any(item => !item.SafetySatisfied),
            "empty network unexpectedly satisfied every safety outcome");
        Equal(0, result.Transitions.Count(item =>
            item.Kind == RealtimeTransitionKind.PromiseDefaulted),
            "FIRST_LIGHT produced a promise default");
        Equal(1, result.Transitions.Count(item =>
            item.Kind == RealtimeTransitionKind.ChapterCompleted),
            "chapter outcome transitions");
    }

    private void ChunkInvariance()
    {
        long finalMinute = FinalMinute();
        var oneShot = new RealtimeCampaignRun(_campaign, _world);
        StartJitLine(oneShot);
        RealtimeAdvanceResult one = oneShot.AdvanceTo(finalMinute);

        var chunked = new RealtimeCampaignRun(_campaign, _world);
        StartJitLine(chunked);
        var chunkTransitions = new List<RealtimeTransition>();
        int[] chunks = [1];
        int chunkIndex = 0;
        while (chunked.GetSnapshot().Minute < finalMinute)
        {
            long current = chunked.GetSnapshot().Minute;
            long target = Math.Min(finalMinute, checked(current + chunks[chunkIndex++ % chunks.Length]));
            chunkTransitions.AddRange(chunked.AdvanceTo(target).Transitions);
        }
        Equal(Json(one.Snapshot), Json(chunked.GetSnapshot()),
            "chunked final snapshot differs");
        Equal(Json(one.Transitions), Json(chunkTransitions),
            "chunked transition stream differs");
        Equal(oneShot.GetCanonicalStateSha256(), chunked.GetCanonicalStateSha256(),
            "chunked canonical state hash differs");

        (RealtimeWorldDefinition definition, CommercialWorldDefinition network, _, _) =
            BottleneckWorld(Archetype.Pole);
        var thermalOne = new RealtimeThermalSession(definition, network, 100);
        var thermalChunks = new RealtimeThermalSession(definition, network, 100);
        ThermalIntervalRequest overload = OverloadRequest("CHUNK_OVERLOAD");
        thermalOne.SetOperatingProfile(network, overload);
        thermalChunks.SetOperatingProfile(network, overload);
        IReadOnlyList<RealtimeThermalTransition> thermalOneTransitions =
            thermalOne.AdvanceTo(105);
        var thermalChunkTransitions = new List<RealtimeThermalTransition>();
        foreach (long minute in new long[] { 101, 102, 103, 105 })
        {
            thermalChunkTransitions.AddRange(thermalChunks.AdvanceTo(minute));
        }
        Equal(Json(thermalOne.GetSnapshot()), Json(thermalChunks.GetSnapshot()),
            "thermal trip/recovery chunk snapshot differs");
        Equal(Json(thermalOneTransitions), Json(thermalChunkTransitions),
            "thermal trip/recovery transition stream differs");
    }

    private void FrameSpeedCanonicalHash()
    {
        static void AddCampaignTransitions(
            List<RealtimeTransition> target,
            RealtimeFrameAdvanceResult result)
        {
            if (result.Campaign is not null)
            {
                target.AddRange(result.Campaign.Transitions);
            }
        }

        const long frameStartMinute = 1248;
        const long frameEndMinute = 1266;
        const long simulationMinutes = frameEndMinute - frameStartMinute;

        var reference = new RealtimeCampaignRun(_campaign, _world);
        StartJitLine(reference);
        RealtimeAdvanceResult referenceResult = reference.AdvanceTo(frameEndMinute);
        string referenceSnapshot = Json(referenceResult.Snapshot);
        string referenceTransitions = Json(referenceResult.Transitions);
        string referenceHash = referenceResult.CanonicalStateSha256;
        Equal(referenceHash, reference.GetCanonicalStateSha256(),
            "advance result canonical hash does not describe committed state");
        Equal(referenceHash, RealtimeStateCanonicalizer.Sha256(referenceResult.Snapshot),
            "advance result canonical hash was not computed atomically");

        const long matrixMinutes = 4;
        var matrixReference = new RealtimeCampaignRun(_campaign, _world);
        long matrixStartMinute = matrixReference.GetSnapshot().Minute;
        RealtimeAdvanceResult matrixReferenceResult = matrixReference.AdvanceTo(
            checked(matrixStartMinute + matrixMinutes));
        string matrixReferenceSnapshot = Json(matrixReferenceResult.Snapshot);
        string matrixReferenceTransitions = Json(matrixReferenceResult.Transitions);
        string matrixReferenceHash = matrixReferenceResult.CanonicalStateSha256;

        foreach (int speed in new[] { 1, 2, 4 })
        {
            foreach (int framesPerSecond in new[] { 30, 60, 120, 144 })
            {
                long matrixScaledFrames = checked(framesPerSecond * matrixMinutes);
                Check(matrixScaledFrames % speed == 0,
                    $"frame matrix interval is not exact at {framesPerSecond}/{speed}");
                long matrixFrames = matrixScaledFrames / speed;

                var perFrame = new RealtimeCampaignRun(_campaign, _world);
                var perFrameClock = new RealtimeFrameAccumulator(matrixMinutes + 1);
                var perFrameTransitions = new List<RealtimeTransition>();
                for (long frame = 0; frame < matrixFrames; frame++)
                {
                    RealtimeFrameAdvanceResult advanced = perFrameClock.AdvanceFrames(
                        perFrame,
                        1,
                        framesPerSecond,
                        speed);
                    AddCampaignTransitions(perFrameTransitions, advanced);
                }
                Equal(matrixStartMinute + matrixMinutes, perFrame.GetSnapshot().Minute,
                    $"per-frame target {framesPerSecond}/{speed}");
                Equal(matrixReferenceHash, perFrame.GetCanonicalStateSha256(),
                    $"per-frame canonical hash {framesPerSecond}/{speed}");
                Equal(matrixReferenceSnapshot, Json(perFrame.GetSnapshot()),
                    $"per-frame snapshot {framesPerSecond}/{speed}");
                Equal(matrixReferenceTransitions, Json(perFrameTransitions),
                    $"per-frame transitions {framesPerSecond}/{speed}");
                Check(!perFrameClock.GetSnapshot().HasPendingTime &&
                        perFrameClock.GetSnapshot().AppliedSimulationMinutes ==
                            matrixMinutes,
                    $"per-frame accumulator remainder {framesPerSecond}/{speed}");

                var irregular = new RealtimeCampaignRun(_campaign, _world);
                StartJitLine(irregular);
                var irregularClock = new RealtimeFrameAccumulator(simulationMinutes + 1);
                var irregularTransitions = new List<RealtimeTransition>();
                long complexFrames = checked(
                    framesPerSecond * simulationMinutes / speed);
                long[] irregularChunks =
                [
                    1,
                    complexFrames / 3,
                    7,
                    checked(complexFrames - 8 - complexFrames / 3),
                ];
                foreach (long chunk in irregularChunks)
                {
                    RealtimeFrameAdvanceResult advanced = irregularClock.AdvanceFrames(
                        irregular,
                        chunk,
                        framesPerSecond,
                        speed);
                    AddCampaignTransitions(irregularTransitions, advanced);
                }
                Equal(referenceHash, irregular.GetCanonicalStateSha256(),
                    $"irregular canonical hash {framesPerSecond}/{speed}");
                Equal(referenceSnapshot, Json(irregular.GetSnapshot()),
                    $"irregular snapshot {framesPerSecond}/{speed}");
                Equal(referenceTransitions, Json(irregularTransitions),
                    $"irregular transitions {framesPerSecond}/{speed}");
                Check(!irregularClock.GetSnapshot().HasPendingTime,
                    $"irregular accumulator remainder {framesPerSecond}/{speed}");
            }
        }

        var switched = new RealtimeCampaignRun(_campaign, _world);
        StartJitLine(switched);
        var switchedClock = new RealtimeFrameAccumulator(simulationMinutes + 1);
        var switchedTransitions = new List<RealtimeTransition>();
        AddCampaignTransitions(switchedTransitions, switchedClock.AdvanceFrames(
            switched, 90, 30, 1));   // 3 minutes
        AddCampaignTransitions(switchedTransitions, switchedClock.AdvanceFrames(
            switched, 180, 60, 2)); // 6 minutes
        AddCampaignTransitions(switchedTransitions, switchedClock.AdvanceFrames(
            switched, 324, 144, 4)); // 9 minutes
        Equal(referenceHash, switched.GetCanonicalStateSha256(),
            "refresh-rate/speed-switch canonical hash");
        Equal(referenceTransitions, Json(switchedTransitions),
            "refresh-rate/speed-switch transitions");

        var paused = new RealtimeCampaignRun(_campaign, _world);
        StartJitLine(paused);
        var pausedClock = new RealtimeFrameAccumulator(5);
        pausedClock.AdvanceFrames(paused, 37, 60, 1);
        pausedClock.Pause();
        string pausedState = Json(paused.GetSnapshot());
        string pausedHash = paused.GetCanonicalStateSha256();
        int pausedCommands = paused.AcceptedCommands.Count;
        RealtimeFrameAdvanceResult ignoredFrames = pausedClock.AdvanceFrames(
            paused,
            60_000,
            60,
            4);
        Equal(RealtimeFramePauseReason.Manual,
            ignoredFrames.Accumulator.PauseReason,
            "manual pause reason");
        Equal(0L, ignoredFrames.AppliedMinutes,
            "manual pause applied simulation time");
        Check(ignoredFrames.Campaign is null,
            "manual pause constructed a redundant campaign snapshot");
        Equal(pausedState, Json(paused.GetSnapshot()),
            "manual pause mutated world state");
        Equal(pausedHash, paused.GetCanonicalStateSha256(),
            "manual pause mutated canonical hash");
        Equal(pausedCommands, paused.AcceptedCommands.Count,
            "manual pause mutated journal");
        pausedClock.Resume();
        pausedClock.AdvanceFrames(paused, 23, 60, 1);
        var pausedControl = new RealtimeCampaignRun(_campaign, _world);
        StartJitLine(pausedControl);
        pausedControl.AdvanceTo(frameStartMinute + 1);
        Equal(pausedControl.GetCanonicalStateSha256(),
            paused.GetCanonicalStateSha256(),
            "pause/resume discarded the fractional frame remainder");

        var exactRemainder = new RealtimeCampaignRun(_campaign, _world);
        StartJitLine(exactRemainder);
        var exactRemainderClock = new RealtimeFrameAccumulator(5);
        RealtimeFrameAdvanceResult beforeWhole = exactRemainderClock.AdvanceFrames(
            exactRemainder,
            143,
            144,
            1);
        Equal(frameStartMinute, exactRemainder.GetSnapshot().Minute,
            "143/144 frame remainder advanced early");
        Equal(715, beforeWhole.Accumulator.FractionalMinuteUnits,
            "143/144 frame remainder units");
        Check(beforeWhole.Campaign is null && beforeWhole.AppliedMinutes == 0,
            "sub-minute frame chunk constructed a redundant campaign snapshot");
        RealtimeFrameAdvanceResult exactWhole = exactRemainderClock.AdvanceFrames(
            exactRemainder,
            1,
            144,
            1);
        Equal(frameStartMinute + 1, exactRemainder.GetSnapshot().Minute,
            "144th frame did not form one exact minute");
        Equal(0, exactWhole.Accumulator.FractionalMinuteUnits,
            "144th frame left a fractional remainder");
        Check(exactWhole.Campaign is not null && exactWhole.AppliedMinutes == 1,
            "whole-minute frame boundary omitted the atomic campaign result");

        var catchUp = new RealtimeCampaignRun(_campaign, _world);
        StartJitLine(catchUp);
        var catchUpClock = new RealtimeFrameAccumulator(5);
        var catchUpTransitions = new List<RealtimeTransition>();
        RealtimeFrameAdvanceResult firstCatchUp = catchUpClock.AdvanceFrames(
            catchUp,
            60 * simulationMinutes,
            60,
            1);
        AddCampaignTransitions(catchUpTransitions, firstCatchUp);
        Check(firstCatchUp.CatchUpCeilingReached &&
                firstCatchUp.Accumulator.PauseReason ==
                    RealtimeFramePauseReason.CatchUpCeiling &&
                firstCatchUp.Accumulator.PendingWholeMinutes == 13 &&
                firstCatchUp.AppliedMinutes == 5,
            "catch-up ceiling did not retain exact debt");
        catchUpClock.Pause();
        Equal(RealtimeFramePauseReason.CatchUpCeiling,
            catchUpClock.GetSnapshot().PauseReason,
            "manual pause erased catch-up stop reason");
        while (catchUpClock.GetSnapshot().HasCatchUpDebt)
        {
            catchUpClock.Resume();
            RealtimeFrameAdvanceResult drained = catchUpClock.DrainPending(catchUp);
            AddCampaignTransitions(catchUpTransitions, drained);
        }
        Equal(referenceHash, catchUp.GetCanonicalStateSha256(),
            "catch-up drain canonical hash");
        Equal(referenceTransitions, Json(catchUpTransitions),
            "catch-up drain transition stream");
        Check(!catchUpClock.GetSnapshot().HasPendingTime &&
                catchUpClock.GetSnapshot().AppliedSimulationMinutes == simulationMinutes,
            "catch-up drain lost simulation time");

        var fractionalReference = new RealtimeCampaignRun(_campaign, _world);
        StartJitLine(fractionalReference);
        RealtimeAdvanceResult fractionalReferenceResult = fractionalReference.AdvanceTo(
            frameStartMinute + simulationMinutes + 1);
        var fractionalCatchUp = new RealtimeCampaignRun(_campaign, _world);
        StartJitLine(fractionalCatchUp);
        var fractionalClock = new RealtimeFrameAccumulator(5);
        var fractionalTransitions = new List<RealtimeTransition>();
        RealtimeFrameAdvanceResult fractionalHitch = fractionalClock.AdvanceFrames(
            fractionalCatchUp,
            60 * simulationMinutes + 30,
            60,
            1);
        AddCampaignTransitions(fractionalTransitions, fractionalHitch);
        Check(fractionalHitch.AppliedMinutes == 5 &&
                fractionalHitch.Accumulator.PendingWholeMinutes == 13 &&
                fractionalHitch.Accumulator.FractionalMinuteUnits == 360 &&
                fractionalHitch.Accumulator.HasCatchUpDebt,
            "18.5-minute hitch did not split exact whole debt and fractional remainder");
        var drainBatches = new List<long>();
        while (fractionalClock.GetSnapshot().HasCatchUpDebt)
        {
            Check(drainBatches.Count < 3,
                "fractional catch-up debt did not terminate in its bounded batches");
            fractionalClock.Resume();
            RealtimeFrameAdvanceResult drained =
                fractionalClock.DrainPending(fractionalCatchUp);
            drainBatches.Add(drained.AppliedMinutes);
            AddCampaignTransitions(fractionalTransitions, drained);
        }
        SequenceEqual(new long[] { 5, 5, 3 }, drainBatches,
            "fractional catch-up bounded drain batches");
        RealtimeFrameAccumulatorSnapshot retainedFraction = fractionalClock.GetSnapshot();
        Check(!retainedFraction.HasCatchUpDebt && retainedFraction.HasPendingTime &&
                retainedFraction.PendingWholeMinutes == 0 &&
                retainedFraction.FractionalMinuteUnits == 360 &&
                retainedFraction.AppliedSimulationMinutes == simulationMinutes,
            "whole-debt drain discarded or consumed the retained half minute");
        RealtimeFrameAdvanceResult completedFraction = fractionalClock.AdvanceFrames(
            fractionalCatchUp,
            30,
            60,
            1);
        AddCampaignTransitions(fractionalTransitions, completedFraction);
        Check(completedFraction.Campaign is not null &&
                completedFraction.AppliedMinutes == 1 &&
                !completedFraction.Accumulator.HasPendingTime &&
                completedFraction.Accumulator.AppliedSimulationMinutes ==
                    simulationMinutes + 1,
            "additional half minute did not complete the retained exact minute");
        Equal(fractionalReferenceResult.CanonicalStateSha256,
            fractionalCatchUp.GetCanonicalStateSha256(),
            "fractional catch-up final canonical hash");
        Equal(Json(fractionalReferenceResult.Transitions), Json(fractionalTransitions),
            "fractional catch-up final transition stream");
    }

    private void CanonicalFutureEquivalence()
    {
        var freshA = new RealtimeCampaignRun(_campaign, _world);
        var freshB = new RealtimeCampaignRun(_campaign, _world);
        Equal(freshA.GetCanonicalStateSha256(), freshB.GetCanonicalStateSha256(),
            "equal fresh state hash");
        Check(RealtimeStateCanonicalizer.StructuralEquals(
                freshA.GetSnapshot(),
                freshB.GetSnapshot()) &&
              RealtimeStateCanonicalizer.StructuralHashCode(freshA.GetSnapshot()) ==
                RealtimeStateCanonicalizer.StructuralHashCode(freshB.GetSnapshot()),
            "equal nontrivial snapshots lack canonical structural value semantics");
        RealtimeAdvanceResult freshAOutput = freshA.AdvanceTo(
            freshA.GetSnapshot().Minute);
        RealtimeAdvanceResult freshBOutput = freshB.AdvanceTo(
            freshB.GetSnapshot().Minute);
        Equal(Json(freshAOutput), Json(freshBOutput),
            "equal canonical states produced different zero-minute output");
        var undelivered = new RealtimeCampaignRun(_campaign, _world);
        string undeliveredHash = undelivered.GetCanonicalStateSha256();
        Check(undelivered.GetSnapshot().PendingTransitions.Count > 0,
            "snapshot omitted pending public transitions");
        undelivered.AdvanceTo(undelivered.GetSnapshot().Minute);
        Check(!string.Equals(
                undeliveredHash,
                undelivered.GetCanonicalStateSha256(),
                StringComparison.Ordinal),
            "canonical hash ignored transition delivery cursor");

        var run = new RealtimeCampaignRun(_campaign, _world);
        run.AdvanceTo(1261);
        RealtimeCampaignSnapshot active = run.GetSnapshot();
        Check(active.ActiveDuty is not null &&
                active.ActiveDuty.ClosedSegments.Count == 1,
            "active canonical snapshot omitted accumulated duty progress");
        string activeHash = RealtimeStateCanonicalizer.Sha256(active);
        string withoutDuty = RealtimeStateCanonicalizer.Sha256(
            active with { ActiveDuty = null });
        Check(!string.Equals(activeHash, withoutDuty, StringComparison.Ordinal),
            "canonical hash ignored active-duty future state");

        RealtimeEventOutcome completedEvent =
            active.Forecast.Events.Single(item =>
                item.Status == RealtimeForecastStatus.Active)
                .TemporalProjection.Outcome with
            {
                ChapterId = active.Chapter.Content.ChapterId,
            };
        string withCurrentOutcome = RealtimeStateCanonicalizer.Sha256(
            active with
            {
                CurrentChapterEvents = [completedEvent],
            });
        Check(!string.Equals(activeHash, withCurrentOutcome, StringComparison.Ordinal),
            "canonical hash ignored current-chapter event truth");
        Equal(64, activeHash.Length, "canonical SHA-256 lowercase hex length");
        Check(activeHash.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            "canonical SHA-256 is not lowercase hex");

        CommercialWorldDefinition reorderedNetwork = _world.Network with
        {
            NodeClasses = _world.Network.NodeClasses.Reverse().ToArray(),
            LineClasses = _world.Network.LineClasses.Reverse().ToArray(),
            RiskAreas = _world.Network.RiskAreas.Reverse().ToArray(),
            Nodes = _world.Network.Nodes.Reverse().ToArray(),
            Edges = _world.Network.Edges.Reverse().ToArray(),
        };
        CommercialWorldLoader.Validate(reorderedNetwork);
        RealtimeWorldDefinition reorderedWorld = _world with
        {
            Network = reorderedNetwork,
        };
        RealtimeWorldLoader.Validate(reorderedWorld, reorderedNetwork);
        var reorderedRun = new RealtimeCampaignRun(_campaign, reorderedWorld);
        reorderedRun.AdvanceTo(reorderedRun.GetSnapshot().Minute);
        Equal(freshA.GetSnapshot().Authority, reorderedRun.GetSnapshot().Authority,
            "semantic authority changed with unordered world collection order");
        Equal(freshA.GetCanonicalStateSha256(), reorderedRun.GetCanonicalStateSha256(),
            "canonical state changed with unordered world collection order");

        RealtimeWorldDefinition changedAuthorityWorld = _world with
        {
            ThermalClasses = _world.ThermalClasses.Select(item =>
                item.AssetKind == ThermalAssetKind.Edge &&
                item.ClassId == "STANDARD_LINE"
                    ? item with
                    {
                        Protection = item.Protection with
                        {
                            ProtectiveOutageMinutes = checked(
                                item.Protection.ProtectiveOutageMinutes + 1),
                        },
                    }
                    : item).ToArray(),
        };
        RealtimeWorldLoader.Validate(changedAuthorityWorld, changedAuthorityWorld.Network);
        var changedAuthorityRun = new RealtimeCampaignRun(
            _campaign,
            changedAuthorityWorld);
        changedAuthorityRun.AdvanceTo(changedAuthorityRun.GetSnapshot().Minute);
        Check(!string.Equals(
                freshA.GetSnapshot().Authority.WorldDefinitionSha256,
                changedAuthorityRun.GetSnapshot().Authority.WorldDefinitionSha256,
                StringComparison.Ordinal) &&
              !string.Equals(
                freshA.GetCanonicalStateSha256(),
                changedAuthorityRun.GetCanonicalStateSha256(),
                StringComparison.Ordinal),
            "canonical authority ignored a future protection-rule change");

        CommercialWorldDefinition terrainOrderNetwork = _world.Network with
        {
            Terrain = _world.Network.Terrain.Reverse().ToArray(),
        };
        CommercialWorldLoader.Validate(terrainOrderNetwork);
        RealtimeWorldDefinition terrainOrderWorld = _world with
        {
            Network = terrainOrderNetwork,
        };
        RealtimeWorldLoader.Validate(terrainOrderWorld, terrainOrderNetwork);
        Check(!string.Equals(
                freshA.GetSnapshot().Authority.WorldDefinitionSha256,
                RealtimeStateCanonicalizer.AuthorityFor(
                    _campaign,
                    terrainOrderWorld).WorldDefinitionSha256,
                StringComparison.Ordinal),
            "authority ignored authored terrain rejection order");

        var mutableNodes = new List<string> { "NODE_A" };
        var mutableEdges = new List<string> { "EDGE_A" };
        var completion = new RealtimeConstructionCompletion(
            ConstructionKind.Line,
            42,
            mutableNodes,
            mutableEdges);
        mutableNodes.Add("NODE_MUTATED");
        mutableEdges.Clear();
        SequenceEqual(new[] { "NODE_A" }, completion.NodeIds,
            "construction completion retained mutable node input");
        SequenceEqual(new[] { "EDGE_A" }, completion.EdgeIds,
            "construction completion retained mutable edge input");
        var equalCompletion = new RealtimeConstructionCompletion(
            ConstructionKind.Line,
            42,
            new[] { "NODE_A" },
            new[] { "EDGE_A" });
        Check(completion == equalCompletion &&
                RealtimeStateCanonicalizer.StructuralValueEquals(
                    completion,
                    equalCompletion) &&
                RealtimeStateCanonicalizer.StructuralValueHashCode(completion) ==
                    RealtimeStateCanonicalizer.StructuralValueHashCode(equalCompletion),
            "immutable construction completion lacks structural value semantics");
    }

    private void SupplyAllocation()
    {
        CommercialOperatingPhaseDefinition mixed =
            _campaign.Chapters[0].Content.OperatingPhases[0] with
            {
                Loads =
                [
                    new CommercialLoadBundleDefinition(
                        "EAST_RESIDENTIAL",
                        100,
                        CommercialObligationKind.OperatingRecord),
                    new CommercialLoadBundleDefinition(
                        "WATERWORKS",
                        100,
                        CommercialObligationKind.SafetyDuty),
                    new CommercialLoadBundleDefinition(
                        "HOSPITAL",
                        100,
                        CommercialObligationKind.SafetyDuty),
                    new CommercialLoadBundleDefinition(
                        "NORTH_RESIDENTIAL",
                        100,
                        CommercialObligationKind.CityPromise),
                ],
            };
        IReadOnlyList<RealtimeDispatchLoadPlan> plan =
            RealtimeDispatchPlanner.BuildLoadPlan(
                mixed,
                CommercialPromiseDecision.Keep);
        SequenceEqual(
            new[] { "WATERWORKS", "HOSPITAL", "NORTH_RESIDENTIAL", "EAST_RESIDENTIAL" },
            plan.Select(item => item.LoadId),
            "obligation/authored/stable dispatch ordering");
        SequenceEqual(new[] { 0, 0, 1, 2 }, plan.Select(item => item.ObligationPriority),
            "typed obligation priorities");
        SequenceEqual(new[] { 1, 2, 3, 0 }, plan.Select(item =>
            item.AuthoredDispatchPriority), "typed authored dispatch priorities");

        CommercialWorldDefinition alternateWorld = _baseWorld with
        {
            Sources = _baseWorld.Sources.Select(item => item.SourceId == "WEST_GENERATION"
                ? item with { CapacityKw = 500 }
                : item).ToArray(),
        };
        ThermalIntervalEvaluation alternate = RealtimeSupplyAllocator.EvaluateInterval(
            alternateWorld,
            new ThermalIntervalRequest(
                "SAME_LOAD_ALTERNATE",
                [new ThermalLoadRequest(
                    "HOSPITAL",
                    900,
                    ThermalPermission.ContinuousOnly)],
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<ThermalLimitOverride>()),
            Array.Empty<string>());
        ThermalLoadSupply alternateSupply = alternate.Loads.Single();
        Equal("SOUTH_GENERATION", alternateSupply.SourceId,
            "same load did not fall through to viable alternate route");
        Equal(0L, alternate.Sources.Single(item =>
            item.SourceId == "WEST_GENERATION").UsedKw,
            "rejected first route left source ghost usage");

        CommercialWorldDefinition blockedWorld = alternateWorld with
        {
            Sources = alternateWorld.Sources.Select(item =>
                item.SourceId == "SOUTH_GENERATION"
                    ? item with { CapacityKw = 400 }
                    : item).ToArray(),
        };
        ThermalSupplyFailure blocker = RealtimeSupplyAllocator.EvaluateInterval(
                blockedWorld,
                new ThermalIntervalRequest(
                    "EXACT_BLOCKER",
                    [new ThermalLoadRequest(
                        "HOSPITAL",
                        900,
                        ThermalPermission.ContinuousOnly)],
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<ThermalLimitOverride>()),
                Array.Empty<string>())
            .Loads.Single().Failure ?? throw new InvalidOperationException(
                "all-route blocker was not reported");
        Equal(ThermalFailureKind.SourceCapacity, blocker.Kind, "first blocker kind");
        Equal("WEST_GENERATION", blocker.AttemptedSourceId, "first blocker source");
        Equal(900L, blocker.RequiredKw, "first blocker required kW");
        Equal(500L, blocker.AvailableKw, "first blocker available kW");

        ThermalIntervalRequest request = new(
            "NO_GHOST",
            [
                new ThermalLoadRequest("HOSPITAL", 6000, ThermalPermission.EmergencyAllowed),
                new ThermalLoadRequest("WATERWORKS", 1000, ThermalPermission.ContinuousOnly),
            ],
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<ThermalLimitOverride>());
        ThermalIntervalEvaluation evaluation = RealtimeSupplyAllocator.EvaluateInterval(
            _baseWorld,
            request,
            Array.Empty<string>());
        SequenceEqual(
            new[] { "HOSPITAL", "WATERWORKS" },
            evaluation.Loads.Select(item => item.LoadId),
            "stable load allocation order");
        Equal(0L, evaluation.Loads[0].DeliveredKw,
            "oversized rejected route delivered power");
        Equal(1000L, evaluation.Loads[1].DeliveredKw,
            "later viable route was not reconsidered");
        Equal(1000L, evaluation.Sources.Sum(item => item.UsedKw),
            "rejected route left ghost source usage");
        Check(evaluation.Assets.All(item => item.UsedKw is 0 or 1000),
            "rejected route left ghost asset usage");

        CommercialWorldDefinition diamond = AllocatorDiamondWorld();
        ThermalLoadSupply continuousPreferred = RealtimeSupplyAllocator.EvaluateInterval(
                diamond,
                new ThermalIntervalRequest(
                    "CONTINUOUS_OVER_EARLY_EMERGENCY",
                    [new ThermalLoadRequest(
                        "DIAMOND_LOAD_ONE",
                        2600,
                        ThermalPermission.EmergencyAllowed)],
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<ThermalLimitOverride>()),
                Array.Empty<string>())
            .Loads.Single();
        SequenceEqual(new[] { "B_IN", "B_OUT", "LOAD_ONE" },
            continuousPreferred.PathEdgeIds,
            "continuous route did not outrank earlier emergency route");
        Equal(400L, continuousPreferred.MinimumRemainingKw,
            "continuous route minimum thermal margin");

        ThermalLoadSupply fewerEmergencyAssets =
            RealtimeSupplyAllocator.EvaluateInterval(
                    diamond,
                    new ThermalIntervalRequest(
                        "FEWER_EMERGENCY_ASSETS",
                        [new ThermalLoadRequest(
                            "DIAMOND_LOAD_ONE",
                            3100,
                            ThermalPermission.EmergencyAllowed)],
                        Array.Empty<string>(),
                        Array.Empty<string>(),
                        [new ThermalLimitOverride(
                            ThermalAssetKind.Node,
                            "REINFORCED_POLE",
                            3100,
                            5500)]),
                    Array.Empty<string>())
                .Loads.Single();
        SequenceEqual(new[] { "B_IN", "B_OUT", "LOAD_ONE" },
            fewerEmergencyAssets.PathEdgeIds,
            "fewer-emergency-assets route was not preferred within emergency grade");
        Equal(0L, fewerEmergencyAssets.MinimumRemainingKw,
            "emergency-count rank did not precede greater minimum margin");

        ThermalIntervalEvaluation sameSource = RealtimeSupplyAllocator.EvaluateInterval(
            diamond,
            new ThermalIntervalRequest(
                "SAME_SOURCE_ALTERNATE_PATH",
                [
                    new ThermalLoadRequest(
                        "DIAMOND_LOAD_ONE",
                        2000,
                        ThermalPermission.ContinuousOnly),
                    new ThermalLoadRequest(
                        "DIAMOND_LOAD_TWO",
                        1000,
                        ThermalPermission.ContinuousOnly),
                ],
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<ThermalLimitOverride>()),
            Array.Empty<string>());
        SequenceEqual(new[] { "DIAMOND_LOAD_ONE", "DIAMOND_LOAD_TWO" },
            sameSource.Loads.Select(item => item.LoadId),
            "diamond caller load order");
        SequenceEqual(new[] { "B_IN", "B_OUT", "LOAD_ONE" },
            sameSource.Loads[0].PathEdgeIds,
            "greater-margin continuous route was not preferred");
        Equal(1000L, sameSource.Loads[0].MinimumRemainingKw,
            "greater-margin continuous route exact margin");
        SequenceEqual(new[] { "A_IN", "A_OUT", "LOAD_TWO" },
            sameSource.Loads[1].PathEdgeIds,
            "equal-quality routes did not use deterministic static path order");
        Equal(0L, sameSource.Loads[1].MinimumRemainingKw,
            "equal-quality route exact shared-asset margin");
        Equal(1000L, sameSource.Assets.Single(item => item.AssetId == "A_IN").UsedKw,
            "second accepted static path usage missing");
        Equal(2000L, sameSource.Assets.Single(item => item.AssetId == "B_IN").UsedKw,
            "first accepted greater-margin path usage missing");
        Equal(3000L, sameSource.Sources.Single().UsedKw,
            "same-source aggregate usage after rollback");
        Equal(3000L, sameSource.Assets.Single(item =>
                item.AssetId == "SUBSTATION").UsedKw,
            "shared branch/node usage was not accumulated");

        CommercialWorldDefinition rollbackDiamond = diamond with
        {
            Nodes = diamond.Nodes.Select(item => item.NodeId switch
            {
                "A_POLE" => item with { ClassId = "REINFORCED_POLE" },
                "B_POLE" => item with { ClassId = "STANDARD_POLE" },
                "SUBSTATION" => item with { ClassId = "LARGE_SUBSTATION" },
                _ => item,
            }).ToArray(),
            Edges = diamond.Edges.Select(item => item.EdgeId switch
            {
                "A_IN" or "A_OUT" => item with { LineClassId = "REINFORCED_LINE" },
                "B_IN" or "B_OUT" => item with { LineClassId = "STANDARD_LINE" },
                _ => item,
            }).ToArray(),
        };
        CommercialWorldLoader.Validate(rollbackDiamond);
        ThermalIntervalEvaluation rejectedThenAlternate =
            RealtimeSupplyAllocator.EvaluateInterval(
                rollbackDiamond,
                new ThermalIntervalRequest(
                    "SAME_SOURCE_REJECTED_THEN_ALTERNATE",
                    [
                        new ThermalLoadRequest(
                            "DIAMOND_LOAD_ONE",
                            4000,
                            ThermalPermission.ContinuousOnly),
                        new ThermalLoadRequest(
                            "DIAMOND_LOAD_TWO",
                            1000,
                            ThermalPermission.ContinuousOnly),
                    ],
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<ThermalLimitOverride>()),
                Array.Empty<string>());
        SequenceEqual(new[] { "A_IN", "A_OUT", "LOAD_ONE" },
            rejectedThenAlternate.Loads[0].PathEdgeIds,
            "setup did not reserve the first ordered branch");
        SequenceEqual(new[] { "B_IN", "B_OUT", "LOAD_TWO" },
            rejectedThenAlternate.Loads[1].PathEdgeIds,
            "same-source rejected first candidate did not fall through to alternate");
        Equal(4000L, rejectedThenAlternate.Assets.Single(item =>
                item.AssetId == "A_IN").UsedKw,
            "rejected second candidate leaked usage onto reserved branch");
        Equal(1000L, rejectedThenAlternate.Assets.Single(item =>
                item.AssetId == "B_IN").UsedKw,
            "same-source alternate branch usage missing");
        Equal(5000L, rejectedThenAlternate.Sources.Single().UsedKw,
            "same-source alternate allocation source accounting");

        ThermalIntervalEvaluation orderedAssetFailure =
            RealtimeSupplyAllocator.EvaluateInterval(
                diamond,
                new ThermalIntervalRequest(
                    "ORDERED_FIRST_ASSET_BLOCKER",
                    [new ThermalLoadRequest(
                        "DIAMOND_LOAD_ONE",
                        2600,
                        ThermalPermission.ContinuousOnly)],
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    [
                        new ThermalLimitOverride(
                            ThermalAssetKind.Edge,
                            "STANDARD_LINE",
                            2500,
                            3000),
                        new ThermalLimitOverride(
                            ThermalAssetKind.Edge,
                            "REINFORCED_LINE",
                            2500,
                            3000),
                    ]),
                Array.Empty<string>());
        ThermalSupplyFailure orderedBlocker = orderedAssetFailure.Loads.Single().Failure ??
            throw new InvalidOperationException("ordered asset blocker missing");
        Equal(ThermalFailureKind.ContinuousLimit, orderedBlocker.Kind,
            "ordered asset blocker kind");
        Equal("A_IN", orderedBlocker.AssetId,
            "all-rejected routes did not preserve first path/first asset blocker");
        Equal(2600L, orderedBlocker.RequiredKw,
            "ordered asset blocker prospective load");
        Equal(2500L, orderedBlocker.AvailableKw,
            "ordered asset blocker exact limit");

        CommercialWorldDefinition twoSources = AllocatorDiamondWorld(secondSource: true);
        ThermalIntervalEvaluation orderedSourceFailure =
            RealtimeSupplyAllocator.EvaluateInterval(
                twoSources,
                new ThermalIntervalRequest(
                    "ORDERED_FIRST_SOURCE_BLOCKER",
                    [new ThermalLoadRequest(
                        "DIAMOND_LOAD_ONE",
                        400,
                        ThermalPermission.ContinuousOnly)],
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    [new ThermalLimitOverride(
                        ThermalAssetKind.Edge,
                        "STANDARD_LINE",
                        300,
                        320)]),
                Array.Empty<string>());
        ThermalSupplyFailure sourceBlocker = orderedSourceFailure.Loads.Single().Failure ??
            throw new InvalidOperationException("ordered source blocker missing");
        Equal(ThermalFailureKind.SourceCapacity, sourceBlocker.Kind,
            "failure-kind ranking overrode first ordered candidate");
        Equal("DIAMOND_SOURCE", sourceBlocker.AttemptedSourceId,
            "exact first ordered source blocker");
        Equal(400L, sourceBlocker.RequiredKw, "source blocker required kW");
        Equal(300L, sourceBlocker.AvailableKw, "source blocker available kW");

        CommercialWorldDefinition twoSourceQuality = twoSources with
        {
            Nodes = twoSources.Nodes.Select(item => item.NodeId == "A_POLE"
                ? item with { ClassId = "REINFORCED_POLE" }
                : item).ToArray(),
            Edges = twoSources.Edges
                .Where(item => item.EdgeId != "B_IN")
                .Select(item => item.EdgeId == "C_IN"
                    ? item with { LineClassId = "REINFORCED_LINE" }
                    : item)
                .ToArray(),
            Sources = twoSources.Sources.Select(item => item with
            {
                CapacityKw = 5000,
            }).ToArray(),
        };
        CommercialWorldLoader.Validate(twoSourceQuality);
        ThermalLoadSupply laterContinuous = RealtimeSupplyAllocator.EvaluateInterval(
                twoSourceQuality,
                new ThermalIntervalRequest(
                    "TWO_SOURCE_QUALITY_BEFORE_DISPATCH",
                    [new ThermalLoadRequest(
                        "DIAMOND_LOAD_ONE",
                        2600,
                        ThermalPermission.EmergencyAllowed)],
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<ThermalLimitOverride>()),
                Array.Empty<string>())
            .Loads.Single();
        Equal("DIAMOND_SOURCE_TWO", laterContinuous.SourceId,
            "earlier dispatch order overrode a later continuous source route");
        SequenceEqual(new[] { "C_IN", "B_OUT", "LOAD_ONE" },
            laterContinuous.PathEdgeIds,
            "two-source continuous route identity");

        ThermalLoadSupply equalQualityEarlyDispatch =
            RealtimeSupplyAllocator.EvaluateInterval(
                    twoSourceQuality,
                    new ThermalIntervalRequest(
                        "TWO_SOURCE_EQUAL_QUALITY_DISPATCH_TIE",
                        [new ThermalLoadRequest(
                            "DIAMOND_LOAD_ONE",
                            2000,
                            ThermalPermission.ContinuousOnly)],
                        Array.Empty<string>(),
                        Array.Empty<string>(),
                        [
                            new ThermalLimitOverride(
                                ThermalAssetKind.Edge,
                                "STANDARD_LINE",
                                2400,
                                3000),
                            new ThermalLimitOverride(
                                ThermalAssetKind.Edge,
                                "REINFORCED_LINE",
                                2400,
                                3000),
                            new ThermalLimitOverride(
                                ThermalAssetKind.Node,
                                "REINFORCED_POLE",
                                2400,
                                3000),
                            new ThermalLimitOverride(
                                ThermalAssetKind.Node,
                                "SMALL_SUBSTATION",
                                2400,
                                3000),
                        ]),
                    Array.Empty<string>())
                .Loads.Single();
        Equal("DIAMOND_SOURCE", equalQualityEarlyDispatch.SourceId,
            "equal-quality sources ignored authored dispatch order");
        SequenceEqual(new[] { "A_IN", "A_OUT", "LOAD_ONE" },
            equalQualityEarlyDispatch.PathEdgeIds,
            "equal-quality source route identity");
    }

    private void AllocatorPolynomialLayeredGraph()
    {
        const int stages = 22;
        CommercialWorldDefinition network = AllocatorLayeredWorld(stages);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        ThermalIntervalEvaluation evaluation = RealtimeSupplyAllocator.EvaluateInterval(
            network,
            new ThermalIntervalRequest(
                "POLYNOMIAL_LAYERED_GRAPH",
                [new ThermalLoadRequest(
                    "LAYERED_LOAD",
                    1000,
                    ThermalPermission.ContinuousOnly)],
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<ThermalLimitOverride>()),
            Array.Empty<string>());
        timer.Stop();

        ThermalLoadSupply supplied = evaluation.Loads.Single();
        Equal(1000L, supplied.DeliveredKw,
            "layered graph load was not supplied");
        Equal(stages * 2 + 2, supplied.PathEdgeIds.Count,
            "layered graph selected path edge count");
        Check(supplied.PathEdgeIds
                .Where(item => item.Contains("_A_", StringComparison.Ordinal) ||
                    item.Contains("_B_", StringComparison.Ordinal))
                .All(item => item.Contains("_A_", StringComparison.Ordinal)),
            "layered graph did not use the deterministic lexicographic optimum");
        Check(timer.Elapsed < TimeSpan.FromSeconds(10),
            $"layered graph route search exceeded bounded runtime: {timer.Elapsed}");

        ThermalIntervalEvaluation repeated = RealtimeSupplyAllocator.EvaluateInterval(
            network,
            new ThermalIntervalRequest(
                "POLYNOMIAL_LAYERED_GRAPH_REPEAT",
                [new ThermalLoadRequest(
                    "LAYERED_LOAD",
                    1000,
                    ThermalPermission.ContinuousOnly)],
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<ThermalLimitOverride>()),
            Array.Empty<string>());
        Equal(Json(evaluation with
            {
                IntervalId = "POLYNOMIAL_LAYERED_GRAPH_REPEAT",
            }), Json(repeated),
            "layered graph allocation was not deterministic");
    }

    private void AllocatorOptimizerProofMatrix()
    {
        CommercialWorldDefinition World(
            IReadOnlyList<SpatialNodeDefinition> nodes,
            IReadOnlyList<SpatialEdgeDefinition> edges,
            IReadOnlyList<CommercialSourceDefinition> sources,
            IReadOnlyList<CommercialLoadDefinition> loads,
            bool reverseNodeAndEdgeCollections = false)
        {
            SpatialNodeDefinition[] orderedNodes = nodes
                .OrderBy(item => item.NodeId, StringComparer.Ordinal)
                .ToArray();
            SpatialEdgeDefinition[] orderedEdges = edges
                .OrderBy(item => item.EdgeId, StringComparer.Ordinal)
                .ToArray();
            if (reverseNodeAndEdgeCollections)
            {
                Array.Reverse(orderedNodes);
                Array.Reverse(orderedEdges);
            }
            CommercialWorldDefinition result = _baseWorld with
            {
                Terrain = Array.Empty<TerrainPolygonDefinition>(),
                RiskAreas = Array.Empty<SpatialRiskAreaDefinition>(),
                Nodes = orderedNodes,
                Edges = orderedEdges,
                Sources = sources.ToArray(),
                Loads = loads.ToArray(),
            };
            CommercialWorldLoader.Validate(result);
            return result;
        }

        void AssertSimpleAcceptedPath(ThermalLoadSupply supply)
        {
            Check(supply.DeliveredKw == supply.DemandKw && supply.Failure is null,
                $"{supply.LoadId} was not accepted");
            Equal(supply.PathNodeIds.Count,
                supply.PathNodeIds.Distinct(StringComparer.Ordinal).Count(),
                $"{supply.LoadId} accepted path repeated a node");
            Equal(supply.PathEdgeIds.Count,
                supply.PathEdgeIds.Distinct(StringComparer.Ordinal).Count(),
                $"{supply.LoadId} accepted path repeated an edge");
        }

        CommercialWorldDefinition articulation = World(
            [
                new SpatialNodeDefinition(
                    "ART_SOURCE_NODE", "SOURCE_TERMINAL", "Articulation source",
                    new MapPoint(100, 300), true, false),
                new SpatialNodeDefinition(
                    "ART_JOIN", "REINFORCED_POLE", "Articulation join",
                    new MapPoint(350, 300), true, false),
                new SpatialNodeDefinition(
                    "ART_SUBSTATION", "LARGE_SUBSTATION", "Dead-end substation",
                    new MapPoint(350, 600), true, false),
                new SpatialNodeDefinition(
                    "ART_LOAD_NODE", "LOAD_TERMINAL", "Articulation load",
                    new MapPoint(600, 300), true, false),
            ],
            [
                new SpatialEdgeDefinition(
                    "ART_IN", "REINFORCED_LINE", "ART_SOURCE_NODE", "ART_JOIN", true),
                new SpatialEdgeDefinition(
                    "ART_LOAD", "REINFORCED_LINE", "ART_JOIN", "ART_LOAD_NODE", true),
                new SpatialEdgeDefinition(
                    "ART_SUB", "REINFORCED_LINE", "ART_JOIN", "ART_SUBSTATION", true),
            ],
            [
                new CommercialSourceDefinition(
                    "ART_SOURCE", "Articulation source", "ART_SOURCE_NODE", 1000, 0),
            ],
            [
                new CommercialLoadDefinition(
                    "ART_LOAD", "Articulation load", "ART_LOAD_NODE"),
            ]);
        ThermalIntervalEvaluation articulationResult =
            RealtimeSupplyAllocator.EvaluateInterval(
                articulation,
                new ThermalIntervalRequest(
                    "OPTIMIZER_ARTICULATION_SIMPLE_PATH",
                    [new ThermalLoadRequest(
                        "ART_LOAD", 100, ThermalPermission.ContinuousOnly)],
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<ThermalLimitOverride>()),
                Array.Empty<string>());
        ThermalLoadSupply articulationSupply = articulationResult.Loads.Single();
        Equal(0L, articulationSupply.DeliveredKw,
            "repeated-node substation walk was accepted as a service path");
        ThermalSupplyFailure articulationFailure = articulationSupply.Failure ??
            throw new InvalidOperationException("articulation rejection missing");
        Equal(ThermalFailureKind.NoEligibleSubstation, articulationFailure.Kind,
            "articulation rejection kind");
        SequenceEqual(
            new[] { "ART_SOURCE_NODE", "ART_JOIN", "ART_LOAD_NODE" },
            articulationSupply.PathNodeIds,
            "articulation diagnostic path");
        Check(articulationResult.Assets.All(item => item.UsedKw == 0),
            "articulation rejection leaked ghost asset usage");
        Check(articulationResult.Sources.All(item => item.UsedKw == 0),
            "articulation rejection leaked ghost source usage");

        SpatialNodeDefinition[] tieNodes =
        [
            new SpatialNodeDefinition(
                "TIE_SOURCE_NODE", "SOURCE_TERMINAL", "Tie source",
                new MapPoint(100, 400), true, false),
            new SpatialNodeDefinition(
                "TIE_SUB_A", "LARGE_SUBSTATION", "Tie substation A",
                new MapPoint(500, 200), true, false),
            new SpatialNodeDefinition(
                "TIE_SUB_B", "LARGE_SUBSTATION", "Tie substation B",
                new MapPoint(500, 600), true, false),
            new SpatialNodeDefinition(
                "TIE_LOAD_NODE", "LOAD_TERMINAL", "Tie load",
                new MapPoint(900, 400), true, false),
        ];
        SpatialEdgeDefinition[] tieEdges =
        [
            new SpatialEdgeDefinition(
                "A_TIE_IN", "REINFORCED_LINE", "TIE_SOURCE_NODE", "TIE_SUB_A", true),
            new SpatialEdgeDefinition(
                "Z_TIE_A_OUT", "REINFORCED_LINE", "TIE_SUB_A", "TIE_LOAD_NODE", true),
            new SpatialEdgeDefinition(
                "B_TIE_IN", "REINFORCED_LINE", "TIE_SOURCE_NODE", "TIE_SUB_B", true),
            new SpatialEdgeDefinition(
                "A_TIE_B_OUT", "REINFORCED_LINE", "TIE_SUB_B", "TIE_LOAD_NODE", true),
        ];
        CommercialSourceDefinition[] tieSources =
        [
            new CommercialSourceDefinition(
                "TIE_SOURCE", "Tie source", "TIE_SOURCE_NODE", 1000, 0),
        ];
        CommercialLoadDefinition[] tieLoads =
        [
            new CommercialLoadDefinition(
                "TIE_LOAD", "Tie load", "TIE_LOAD_NODE"),
        ];
        CommercialWorldDefinition tieWorld = World(
            tieNodes, tieEdges, tieSources, tieLoads);
        CommercialWorldDefinition tieWorldPermuted = World(
            tieNodes, tieEdges, tieSources, tieLoads,
            reverseNodeAndEdgeCollections: true);
        ThermalIntervalRequest tieRequest = new(
            "OPTIMIZER_EXACT_EDGE_TIE",
            [new ThermalLoadRequest(
                "TIE_LOAD", 100, ThermalPermission.ContinuousOnly)],
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<ThermalLimitOverride>());
        ThermalIntervalEvaluation tieResult = RealtimeSupplyAllocator.EvaluateInterval(
            tieWorld,
            tieRequest,
            Array.Empty<string>());
        ThermalIntervalEvaluation tiePermutedResult =
            RealtimeSupplyAllocator.EvaluateInterval(
                tieWorldPermuted,
                tieRequest with { IntervalId = "OPTIMIZER_EXACT_EDGE_TIE_PERMUTED" },
                Array.Empty<string>());
        ThermalLoadSupply tieSupply = tieResult.Loads.Single();
        SequenceEqual(new[] { "A_TIE_IN", "Z_TIE_A_OUT" }, tieSupply.PathEdgeIds,
            "equal numeric routes did not use exact edge-ID tie order");
        SequenceEqual(
            new[] { "TIE_SOURCE_NODE", "TIE_SUB_A", "TIE_LOAD_NODE" },
            tieSupply.PathNodeIds,
            "equal numeric route node identity");
        AssertSimpleAcceptedPath(tieSupply);
        AssertSimpleAcceptedPath(tiePermutedResult.Loads.Single());
        Equal(Json(tieResult with
            {
                IntervalId = "OPTIMIZER_EXACT_EDGE_TIE_PERMUTED",
            }), Json(tiePermutedResult),
            "node/edge collection permutation changed exact allocator output");

        CommercialWorldDefinition fallbackWorld = World(
            [
                new SpatialNodeDefinition(
                    "CAP_SOURCE_EARLY_NODE", "SOURCE_TERMINAL", "Early source",
                    new MapPoint(100, 200), true, false),
                new SpatialNodeDefinition(
                    "CAP_SOURCE_LATE_NODE", "SOURCE_TERMINAL", "Late source",
                    new MapPoint(100, 600), true, false),
                new SpatialNodeDefinition(
                    "CAP_SUBSTATION", "LARGE_SUBSTATION", "Capacity substation",
                    new MapPoint(500, 400), true, false),
                new SpatialNodeDefinition(
                    "CAP_LOAD_ONE_NODE", "LOAD_TERMINAL", "Capacity load one",
                    new MapPoint(900, 300), true, false),
                new SpatialNodeDefinition(
                    "CAP_LOAD_TWO_NODE", "LOAD_TERMINAL", "Capacity load two",
                    new MapPoint(900, 500), true, false),
            ],
            [
                new SpatialEdgeDefinition(
                    "CAP_EARLY_FEED", "REINFORCED_LINE",
                    "CAP_SOURCE_EARLY_NODE", "CAP_SUBSTATION", true),
                new SpatialEdgeDefinition(
                    "CAP_LATE_FEED", "REINFORCED_LINE",
                    "CAP_SOURCE_LATE_NODE", "CAP_SUBSTATION", true),
                new SpatialEdgeDefinition(
                    "CAP_LOAD_ONE_FEED", "REINFORCED_LINE",
                    "CAP_SUBSTATION", "CAP_LOAD_ONE_NODE", true),
                new SpatialEdgeDefinition(
                    "CAP_LOAD_TWO_FEED", "REINFORCED_LINE",
                    "CAP_SUBSTATION", "CAP_LOAD_TWO_NODE", true),
            ],
            [
                new CommercialSourceDefinition(
                    "CAP_SOURCE_EARLY", "Early source", "CAP_SOURCE_EARLY_NODE", 100, 0),
                new CommercialSourceDefinition(
                    "CAP_SOURCE_LATE", "Late source", "CAP_SOURCE_LATE_NODE", 200, 1),
            ],
            [
                new CommercialLoadDefinition(
                    "CAP_LOAD_ONE", "Capacity load one", "CAP_LOAD_ONE_NODE"),
                new CommercialLoadDefinition(
                    "CAP_LOAD_TWO", "Capacity load two", "CAP_LOAD_TWO_NODE"),
            ]);
        ThermalIntervalEvaluation fallbackResult =
            RealtimeSupplyAllocator.EvaluateInterval(
                fallbackWorld,
                new ThermalIntervalRequest(
                    "OPTIMIZER_EXHAUSTED_SOURCE_FALLBACK",
                    [
                        new ThermalLoadRequest(
                            "CAP_LOAD_ONE", 100, ThermalPermission.ContinuousOnly),
                        new ThermalLoadRequest(
                            "CAP_LOAD_TWO", 100, ThermalPermission.ContinuousOnly),
                    ],
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<ThermalLimitOverride>()),
                Array.Empty<string>());
        Equal("CAP_SOURCE_EARLY", fallbackResult.Loads[0].SourceId,
            "first load did not use the earlier equal-quality source");
        Equal("CAP_SOURCE_LATE", fallbackResult.Loads[1].SourceId,
            "later load did not fall back after the earlier source was exhausted");
        Equal(100L, fallbackResult.Sources.Single(item =>
                item.SourceId == "CAP_SOURCE_EARLY").UsedKw,
            "exhausted source accounting");
        Equal(100L, fallbackResult.Sources.Single(item =>
                item.SourceId == "CAP_SOURCE_LATE").UsedKw,
            "fallback source accounting");
        Equal(200L, fallbackResult.Assets.Single(item =>
                item.AssetId == "CAP_SUBSTATION").UsedKw,
            "shared fallback substation accounting");
        foreach (ThermalLoadSupply supply in fallbackResult.Loads)
        {
            AssertSimpleAcceptedPath(supply);
        }

        CommercialWorldDefinition diagnosticWorld = World(
            [
                new SpatialNodeDefinition(
                    "DIAG_SOURCE_NODE", "SOURCE_TERMINAL", "Diagnostic source",
                    new MapPoint(100, 400), true, false),
                new SpatialNodeDefinition(
                    "DIAG_DIRECT_POLE", "REINFORCED_POLE", "Direct pole",
                    new MapPoint(500, 400), true, false),
                new SpatialNodeDefinition(
                    "DIAG_SERVICE_POLE", "REINFORCED_POLE", "Service pole",
                    new MapPoint(300, 800), true, false),
                new SpatialNodeDefinition(
                    "DIAG_SUBSTATION", "LARGE_SUBSTATION", "Diagnostic substation",
                    new MapPoint(700, 800), true, false),
                new SpatialNodeDefinition(
                    "DIAG_LOAD_NODE", "LOAD_TERMINAL", "Diagnostic load",
                    new MapPoint(900, 400), true, false),
            ],
            [
                new SpatialEdgeDefinition(
                    "A_DIRECT_IN", "REINFORCED_LINE",
                    "DIAG_SOURCE_NODE", "DIAG_DIRECT_POLE", true),
                new SpatialEdgeDefinition(
                    "A_DIRECT_OUT", "REINFORCED_LINE",
                    "DIAG_DIRECT_POLE", "DIAG_LOAD_NODE", true),
                new SpatialEdgeDefinition(
                    "Z_SERVICE_IN", "REINFORCED_LINE",
                    "DIAG_SOURCE_NODE", "DIAG_SERVICE_POLE", true),
                new SpatialEdgeDefinition(
                    "Z_SERVICE_BLOCK", "STANDARD_LINE",
                    "DIAG_SERVICE_POLE", "DIAG_SUBSTATION", true),
                new SpatialEdgeDefinition(
                    "Z_SERVICE_LOAD", "REINFORCED_LINE",
                    "DIAG_SUBSTATION", "DIAG_LOAD_NODE", true),
            ],
            [
                new CommercialSourceDefinition(
                    "DIAG_SOURCE", "Diagnostic source", "DIAG_SOURCE_NODE", 1000, 0),
            ],
            [
                new CommercialLoadDefinition(
                    "DIAG_LOAD", "Diagnostic load", "DIAG_LOAD_NODE"),
            ]);
        ThermalLimitOverride[] diagnosticLimits =
        [
            new ThermalLimitOverride(
                ThermalAssetKind.Edge, "STANDARD_LINE", 100, 100),
        ];
        ThermalIntervalEvaluation diagnosticControl =
            RealtimeSupplyAllocator.EvaluateInterval(
                diagnosticWorld,
                new ThermalIntervalRequest(
                    "OPTIMIZER_SERVICE_PATH_CONTROL",
                    [new ThermalLoadRequest(
                        "DIAG_LOAD", 100, ThermalPermission.ContinuousOnly)],
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    diagnosticLimits),
                Array.Empty<string>());
        ThermalLoadSupply diagnosticControlSupply = diagnosticControl.Loads.Single();
        SequenceEqual(
            new[] { "Z_SERVICE_IN", "Z_SERVICE_BLOCK", "Z_SERVICE_LOAD" },
            diagnosticControlSupply.PathEdgeIds,
            "service-path control did not prove the longer route exists");
        AssertSimpleAcceptedPath(diagnosticControlSupply);

        ThermalIntervalEvaluation diagnosticBlocked =
            RealtimeSupplyAllocator.EvaluateInterval(
                diagnosticWorld,
                new ThermalIntervalRequest(
                    "OPTIMIZER_FIRST_REJECTION_DIAGNOSTIC",
                    [new ThermalLoadRequest(
                        "DIAG_LOAD", 101, ThermalPermission.ContinuousOnly)],
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    diagnosticLimits),
                Array.Empty<string>());
        ThermalLoadSupply diagnosticBlockedSupply = diagnosticBlocked.Loads.Single();
        Equal(0L, diagnosticBlockedSupply.DeliveredKw,
            "thermally blocked service route unexpectedly supplied the load");
        ThermalSupplyFailure diagnosticFailure = diagnosticBlockedSupply.Failure ??
            throw new InvalidOperationException("first-rejection diagnostic missing");
        Equal(ThermalFailureKind.NoEligibleSubstation, diagnosticFailure.Kind,
            "later thermal blocker replaced the first ordered rejection");
        Equal("DIAG_SOURCE", diagnosticFailure.AttemptedSourceId,
            "first-rejection diagnostic source");
        SequenceEqual(new[] { "A_DIRECT_IN", "A_DIRECT_OUT" },
            diagnosticBlockedSupply.PathEdgeIds,
            "first-rejection diagnostic did not freeze the direct static path");
        SequenceEqual(
            new[] { "DIAG_SOURCE_NODE", "DIAG_DIRECT_POLE", "DIAG_LOAD_NODE" },
            diagnosticBlockedSupply.PathNodeIds,
            "first-rejection diagnostic node path");
        Check(diagnosticBlocked.Assets.All(item => item.UsedKw == 0),
            "all-rejected diagnostic leaked ghost asset usage");
        Check(diagnosticBlocked.Sources.All(item => item.UsedKw == 0),
            "all-rejected diagnostic leaked ghost source usage");
    }

    private void IntervalDutyTripRecover()
    {
        (RealtimeWorldDefinition definition, CommercialWorldDefinition network, string targetId,
            _) = BottleneckWorld(Archetype.Pole);
        var session = new RealtimeThermalSession(definition, network, 100);
        CommercialOperatingPhaseDefinition profile =
            _campaign.Chapters[0].Content.OperatingPhases[0] with
            {
                PhaseId = "DUTY_INTERVAL",
                ThermalPolicy = CommercialPhaseThermalPolicy.SafetyEmergencyAllowed,
                Loads = [new CommercialLoadBundleDefinition(
                    "WATERWORKS",
                    2000,
                    CommercialObligationKind.SafetyDuty)],
            };
        var scheduled = new RealtimeScheduledEventDefinition(
            "DUTY_INTERVAL",
            0,
            0,
            6,
            0,
            profile);
        var duty = new RealtimeEventDutyAccumulator(
            "FIRST_LIGHT",
            scheduled,
            CommercialPromiseDecision.Unset,
            100);
        duty.Record(session.SetOperatingProfile(network, OverloadRequest("DUTY_INTERVAL")));
        ThermalIntervalEvaluation allowance = session.GetSnapshot().Evaluation;
        ThermalAssetUsage allowanceTarget = allowance.Assets.Single(item =>
            item.AssetId == targetId);
        Equal(ThermalOperatingState.Emergency, allowanceTarget.State,
            "temporal exposure allowance current state");
        Equal(ThermalOperatingState.Emergency, allowanceTarget.NextState,
            "V2 next-state compatibility predicted an immediate trip");
        SequenceEqual(Array.Empty<string>(), allowance.NextThermalState.CoolingAssetIds,
            "V2 cooling compatibility predicted an immediate outage");

        session.AdvanceClockTo(102);
        duty.CloseSegment(102, session.GetSnapshot().Evaluation);
        IReadOnlyList<RealtimeThermalTransition> trip = session.SettleCurrentMinute();
        duty.Record(trip);
        ThermalIntervalEvaluation cooldown = session.GetSnapshot().Evaluation;
        ThermalAssetUsage cooldownTarget = cooldown.Assets.Single(item =>
            item.AssetId == targetId);
        Equal(ThermalOperatingState.ProtectiveOutage, cooldownTarget.State,
            "temporal cooldown current state");
        Equal(ThermalOperatingState.ProtectiveOutage, cooldownTarget.NextState,
            "V2 next-state compatibility predicted an immediate recovery");
        SequenceEqual(new[] { targetId }, cooldown.NextThermalState.CoolingAssetIds,
            "V2 cooling compatibility did not retain current outage");
        session.AdvanceClockTo(105);
        duty.CloseSegment(105, session.GetSnapshot().Evaluation);
        IReadOnlyList<RealtimeThermalTransition> recovery = session.SettleCurrentMinute();
        duty.Record(recovery);
        session.AdvanceClockTo(106);
        RealtimeEventOutcome outcome = duty.Complete(106, session.GetSnapshot().Evaluation);

        Check(!outcome.SafetySatisfied,
            "recovered final evaluation erased mid-event safety failure");
        Equal(102L, outcome.FirstSafetyUnservedMinute,
            "mid-event first unserved minute");
        Equal(3L, outcome.SafetyUnservedMinutes,
            "mid-event total unserved minutes");
        Equal(3, outcome.DutySegments.Count, "trip/recover duty segment count");
        Check(outcome.FinalEvaluation.Loads.Single().DeliveredKw == 2000,
            "regression did not recover before event end");
        Check(outcome.Incidents.Any(item => item.AssetId == targetId &&
                item.Kind == RealtimeThermalTransitionKind.ProtectiveTrip) &&
              outcome.Incidents.Any(item => item.AssetId == targetId &&
                item.Kind == RealtimeThermalTransitionKind.Recovered),
            "typed trip/recovery incidents missing");
    }

    private void TemporalForecastTripRecoveryActual()
    {
        foreach (Archetype archetype in Enum.GetValues<Archetype>())
        {
            (RealtimeWorldDefinition definition, CommercialWorldDefinition network,
                string targetId, _) = BottleneckWorld(archetype);
            string eventId = $"TEMPORAL_{archetype.ToString().ToUpperInvariant()}";
            CommercialOperatingPhaseDefinition profile =
                _campaign.Chapters[0].Content.OperatingPhases[0] with
                {
                    PhaseId = eventId,
                    ThermalPolicy = CommercialPhaseThermalPolicy.SafetyEmergencyAllowed,
                    Loads = [new CommercialLoadBundleDefinition(
                        "WATERWORKS",
                        2000,
                        CommercialObligationKind.SafetyDuty)],
                };
            var scheduled = new RealtimeScheduledEventDefinition(
                eventId,
                0,
                0,
                6,
                0,
                profile);
            ThermalIntervalRequest request = OverloadRequest(eventId);
            var forecastBoundary = new RealtimeThermalSession(definition, network, 100);
            RealtimeTemporalEventProjection forecast = RealtimeEventForecaster.Project(
                forecastBoundary,
                network,
                "TEMPORAL_CHECK",
                scheduled,
                request,
                CommercialPromiseDecision.Unset);

            Equal(3, forecast.Intervals.Count,
                $"{archetype} temporal forecast interval count");
            SequenceEqual(
                new[] { (100L, 102L), (102L, 105L), (105L, 106L) },
                forecast.Intervals.Select(item => (item.StartMinute, item.EndMinute)),
                $"{archetype} temporal forecast change points");
            SequenceEqual(
                new[] { 2000L, 0L, 2000L },
                forecast.Intervals.Select(item =>
                    item.Evaluation.Loads.Single().DeliveredKw),
                $"{archetype} temporal forecast delivery intervals");
            Check(forecast.Transitions.Any(item =>
                    item.AssetId == targetId &&
                    item.Minute == 102 &&
                    item.Kind == RealtimeThermalTransitionKind.ProtectiveTrip),
                $"{archetype} forecast omitted exact trip");
            Check(forecast.Transitions.Any(item =>
                    item.AssetId == targetId &&
                    item.Minute == 105 &&
                    item.Kind == RealtimeThermalTransitionKind.Recovered),
                $"{archetype} forecast omitted exact recovery");

            (RealtimeEventOutcome actualOutcome,
                IReadOnlyList<RealtimeThermalTransition> actualTransitions) =
                ExecuteEventInOneMinuteChunks(
                    definition,
                    network,
                    scheduled,
                    request);
            Equal(Json(forecast.Outcome.DutySegments), Json(actualOutcome.DutySegments),
                $"{archetype} temporal forecast duty differs from actual");
            Equal(Json(forecast.Outcome.Incidents), Json(actualOutcome.Incidents),
                $"{archetype} temporal forecast incidents differ from actual");
            Equal(Json(forecast.Transitions), Json(actualTransitions),
                $"{archetype} temporal forecast transitions differ from actual");
            Equal(Json(forecast.Outcome.FinalEvaluation),
                Json(actualOutcome.FinalEvaluation),
                $"{archetype} temporal forecast final evaluation differs from actual");
            Check(!forecast.Outcome.SafetySatisfied,
                $"{archetype} temporal forecast erased interval failure");
            Equal(102L, forecast.Outcome.FirstSafetyUnservedMinute,
                $"{archetype} forecast first safety failure minute");
            Equal(3L, forecast.Outcome.SafetyUnservedMinutes,
                $"{archetype} forecast total safety failure minutes");
        }
    }

    private static (RealtimeEventOutcome Outcome,
        IReadOnlyList<RealtimeThermalTransition> Transitions)
        ExecuteEventInOneMinuteChunks(
            RealtimeWorldDefinition definition,
            CommercialWorldDefinition network,
            RealtimeScheduledEventDefinition scheduled,
            ThermalIntervalRequest request)
    {
        var session = new RealtimeThermalSession(definition, network, 100);
        var duty = new RealtimeEventDutyAccumulator(
            "TEMPORAL_CHECK",
            scheduled,
            CommercialPromiseDecision.Unset,
            100);
        var transitions = new List<RealtimeThermalTransition>();
        IReadOnlyList<RealtimeThermalTransition> profile =
            session.SetOperatingProfile(network, request);
        duty.Record(profile);
        transitions.AddRange(profile);
        IReadOnlyList<RealtimeThermalTransition> initial = session.SettleCurrentMinute();
        duty.Record(initial);
        transitions.AddRange(initial);
        long endMinute = checked(100 + scheduled.DurationMinutes);
        for (long next = 101; next <= endMinute; next++)
        {
            ThermalIntervalEvaluation before = session.GetSnapshot().Evaluation;
            session.AdvanceClockTo(next);
            duty.CloseSegment(next, before);
            if (next < endMinute)
            {
                IReadOnlyList<RealtimeThermalTransition> settled =
                    session.SettleCurrentMinute();
                duty.Record(settled);
                transitions.AddRange(settled);
            }
        }
        IReadOnlyList<RealtimeThermalTransition> terminal =
            session.SettleCurrentMinute();
        duty.Record(terminal);
        transitions.AddRange(terminal);
        return (
            duty.Complete(endMinute, session.GetSnapshot().Evaluation),
            Array.AsReadOnly(transitions.ToArray()));
    }

    private void DualUnavailabilityCauses()
    {
        (RealtimeWorldDefinition definition, CommercialWorldDefinition network, string targetId,
            ThermalAssetKind targetKind) = BottleneckWorld(Archetype.Line);
        var cooling = new RealtimeThermalSession(definition, network, 100);
        cooling.SetOperatingProfile(network, OverloadRequest("RATE_OVERLOAD"));
        cooling.AdvanceTo(101);
        Equal(1L, Asset(cooling, targetId).EmergencyExposureMinutes,
            "data-owned exposure before recovery");
        cooling.SetIdle(network, "RATE_RECOVERY");
        cooling.AdvanceTo(102);
        Equal(0L, Asset(cooling, targetId).EmergencyExposureMinutes,
            "data-owned exposure recovery boundary");

        var pending = new RealtimeThermalSession(definition, network, 100);
        pending.SetOperatingProfile(network, OverloadRequest("PENDING_TRIP"));
        pending.AdvanceClockTo(102);
        pending.SetIdle(network, "PENDING_PROFILE_REMOVED");
        IReadOnlyList<RealtimeThermalTransition> pendingTransitions =
            pending.SettleCurrentMinute();
        Check(pendingTransitions.Any(item => item.AssetId == targetId &&
                item.Kind == RealtimeThermalTransitionKind.ProtectiveTrip),
            "exhausted prior exposure was canceled by same-minute profile removal");

        var session = new RealtimeThermalSession(definition, network, 100);
        session.SetOperatingProfile(network, OverloadRequest("DUAL_CAUSE"));
        session.AdvanceTo(102);
        ThermalIntervalRequest authored = targetKind == ThermalAssetKind.Node
            ? new ThermalIntervalRequest(
                "DUAL_CAUSE_AUTHORED",
                [new ThermalLoadRequest(
                    "WATERWORKS",
                    2000,
                    ThermalPermission.EmergencyAllowed)],
                [targetId],
                Array.Empty<string>(),
                Array.Empty<ThermalLimitOverride>())
            : new ThermalIntervalRequest(
                "DUAL_CAUSE_AUTHORED",
                [new ThermalLoadRequest(
                    "WATERWORKS",
                    2000,
                    ThermalPermission.EmergencyAllowed)],
                Array.Empty<string>(),
                [targetId],
                Array.Empty<ThermalLimitOverride>());
        session.SetOperatingProfile(network, authored);
        RealtimeThermalAssetSnapshot overlap = Asset(session, targetId);
        Check(overlap.AuthoredUnavailable && overlap.ProtectiveOutage,
            "overlapping authored/protective causes were collapsed");
        session.AdvanceTo(105);
        RealtimeThermalAssetSnapshot afterRecovery = Asset(session, targetId);
        Check(afterRecovery.AuthoredUnavailable && !afterRecovery.ProtectiveOutage,
            "protective recovery erased authored unavailability or stayed latched");
        Equal(0L, session.GetSnapshot().Evaluation.Loads.Single().DeliveredKw,
            "authored cause stopped applying after protective recovery");
    }

    private void ThermalAuthorityAtomicProfile()
    {
        (RealtimeWorldDefinition definition, CommercialWorldDefinition network,
            string targetId, _) = BottleneckWorld(Archetype.Pole);
        var session = new RealtimeThermalSession(definition, network, 100);
        session.SetOperatingProfile(network, OverloadRequest("ATOMIC_BASE"));
        session.AdvanceTo(101);
        string before = Json(session.GetSnapshot());
        long? beforeNext = session.NextTransitionMinute();

        RealtimeThermalAssetSnapshot target = Asset(session, targetId);
        CommercialWorldDefinition mismatched = network with
        {
            NodeClasses = network.NodeClasses.Select(item => item.ClassId == target.ClassId
                ? item with { ThermalLimit = new ThermalLimit(1100, 5000) }
                : item).ToArray(),
        };
        Expect<ArgumentException>(() => session.SetOperatingProfile(
                mismatched,
                OverloadRequest("MISMATCHED_AUTHORITY")),
            "runtime world class/protection authority mismatch");
        Equal(before, Json(session.GetSnapshot()),
            "class-authority rejection mutated thermal snapshot");
        Equal(beforeNext, session.NextTransitionMinute(),
            "class-authority rejection mutated next transition");

        CommercialWorldDefinition nodeReclassified = network with
        {
            Nodes = network.Nodes.Select(item => item.NodeId == targetId
                ? item with { ClassId = "REINFORCED_POLE" }
                : item).ToArray(),
        };
        Expect<ArgumentException>(() => session.SetOperatingProfile(
                nodeReclassified,
                OverloadRequest("RECLASSIFIED_NODE")),
            "existing node asset class reassignment");
        Equal(before, Json(session.GetSnapshot()),
            "node class reassignment rejection mutated thermal snapshot");
        Equal(beforeNext, session.NextTransitionMinute(),
            "node class reassignment rejection changed automatic boundary");

        (RealtimeWorldDefinition edgeDefinition, CommercialWorldDefinition edgeNetwork,
            string edgeTargetId, _) = BottleneckWorld(Archetype.Line);
        var edgeSession = new RealtimeThermalSession(edgeDefinition, edgeNetwork, 100);
        edgeSession.SetOperatingProfile(edgeNetwork, OverloadRequest("EDGE_ATOMIC_BASE"));
        edgeSession.AdvanceTo(101);
        string edgeBefore = Json(edgeSession.GetSnapshot());
        long? edgeBeforeNext = edgeSession.NextTransitionMinute();
        CommercialWorldDefinition edgeReclassified = edgeNetwork with
        {
            Edges = edgeNetwork.Edges.Select(item => item.EdgeId == edgeTargetId
                ? item with { LineClassId = "REINFORCED_LINE" }
                : item).ToArray(),
        };
        Expect<ArgumentException>(() => edgeSession.SetOperatingProfile(
                edgeReclassified,
                OverloadRequest("RECLASSIFIED_EDGE")),
            "existing edge asset class reassignment");
        Equal(edgeBefore, Json(edgeSession.GetSnapshot()),
            "edge class reassignment rejection mutated thermal snapshot");
        Equal(edgeBeforeNext, edgeSession.NextTransitionMinute(),
            "edge class reassignment rejection changed automatic boundary");

        var invalid = new ThermalIntervalRequest(
            "INVALID_ATOMIC_PROFILE",
            [new ThermalLoadRequest(
                "UNKNOWN_LOAD",
                1,
                ThermalPermission.ContinuousOnly)],
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<ThermalLimitOverride>());
        Expect<ArgumentException>(() => session.SetOperatingProfile(network, invalid),
            "invalid profile unknown load");
        Equal(before, Json(session.GetSnapshot()),
            "invalid profile rejection partially committed state");
        Equal(beforeNext, session.NextTransitionMinute(),
            "invalid profile rejection changed automatic boundary");
    }

    private void EventEndTripBackToBack()
    {
        CommercialOperatingPhaseDefinition source = _campaign.Chapters[0]
            .ScheduledEvents.Single(item =>
                item.EventId == "FIRST_LIGHT_POLE_BOTTLENECK")
            .OperatingProfile;
        RealtimeScheduledEventDefinition first = new(
            "BOUNDARY_FIRST",
            5,
            240,
            2,
            240,
            source with
            {
                PhaseId = "BOUNDARY_FIRST",
                DisplayName = "종료 경계 노출",
            });
        RealtimeScheduledEventDefinition second = new(
            "BOUNDARY_SECOND",
            0,
            242,
            3,
            242,
            source with
            {
                PhaseId = "BOUNDARY_SECOND",
                DisplayName = "연속 경계 복구",
            });
        RealtimeChapterDefinition chapter = _campaign.Chapters[0] with
        {
            ScheduledEvents = [first, second],
        };
        RealtimeCampaignDefinition definition = _campaign with
        {
            Chapters = [chapter],
        };
        var run = new RealtimeCampaignRun(definition, _world);
        RealtimeForecastEvent firstForecast = run.GetForecast().Events.Single(item =>
            item.EventId == first.EventId);
        RealtimeAdvanceResult boundary = run.AdvanceTo(1262);
        RealtimeEventOutcome firstActual = boundary.Snapshot.CurrentChapterEvents.Single();
        Check(firstActual.Incidents.Any(item =>
                item.AssetId == "SLICE_POLE_TARGET" &&
                item.Minute == 1262 &&
                item.Kind == RealtimeThermalTransitionKind.ProtectiveTrip),
            "terminal pending trip was not attributed to outgoing event");
        Equal(Json(firstForecast.TemporalProjection.Outcome), Json(firstActual),
            "terminal-trip forecast differs from outgoing actual outcome");
        int trip = IndexOf(boundary.Transitions, item =>
            item.Kind == RealtimeTransitionKind.ThermalProtectiveTrip &&
            item.EventId == first.EventId);
        int completed = IndexOf(boundary.Transitions, item =>
            item.Kind == RealtimeTransitionKind.EventCompleted &&
            item.EventId == first.EventId);
        int started = IndexOf(boundary.Transitions, item =>
            item.Kind == RealtimeTransitionKind.EventStarted &&
            item.EventId == second.EventId);
        Check(trip >= 0 && completed > trip && started > completed,
            "back-to-back boundary did not trip→complete→start in causal order");
        Equal(second.EventId, boundary.Snapshot.ActiveEvent?.EventId,
            "back-to-back event did not become active");
        Equal(0L, boundary.Snapshot.Thermal.Evaluation.Loads.Single().DeliveredKw,
            "new event supplied through outgoing event protective outage");
        RealtimeForecastSnapshot horizon = run.GetForecast();
        Equal(1, horizon.Events.Count,
            "completed same-boundary event remained in horizon");
        Equal(second.EventId, horizon.Events[0].EventId,
            "same-boundary horizon active event ordering");

        RealtimeAdvanceResult recovered = run.AdvanceTo(1265);
        RealtimeEventOutcome secondActual = recovered.Snapshot.CompletedChapters.Single()
            .Events.Single(item => item.EventId == second.EventId);
        Equal(3L, secondActual.SafetyUnservedMinutes,
            "back-to-back protective outage duty duration");
        Check(secondActual.Incidents.Any(item =>
                item.AssetId == "SLICE_POLE_TARGET" &&
                item.Minute == 1265 &&
                item.Kind == RealtimeThermalTransitionKind.Recovered),
            "terminal recovery was not attributed to active outgoing event");
    }

    private void PendingTripConstructionBoundary()
    {
        RealtimeProjectQuote PrepareComparisonBypass(
            RealtimeCampaignRun run,
            bool orderAtEventStart)
        {
            StartJitLine(run);
            long sequence = run.AcceptedCommands.Count + 1L;
            Accepted(run.ApplyCommand(
                1248,
                sequence++,
                RealtimeCommand.StartLineDraft(
                    "SLICE_LINE_WEST_BANK",
                    "STANDARD_LINE",
                    "STANDARD_POLE")),
                "parallel bypass draft start");
            Accepted(run.ApplyCommand(
                1248,
                sequence++,
                RealtimeCommand.AddLinePoint(new MapPoint(1050, 800))),
                "parallel bypass west-bank pole");
            Accepted(run.ApplyCommand(
                1248,
                sequence++,
                RealtimeCommand.AddLinePoint(new MapPoint(1620, 800))),
                "parallel bypass east-bank pole");
            Accepted(run.ApplyCommand(
                1248,
                sequence++,
                RealtimeCommand.FinishLineDraft("SLICE_LINE_EAST_BANK")),
                "parallel bypass draft finish");
            run.AdvanceTo(1260);
            RealtimeProjectQuote quote = run.PreviewLineOrder();
            Check(quote.Accepted && quote.BuildMinutes is > 2 &&
                    quote.CompletionMinute == checked(1260 + quote.BuildMinutes),
                "parallel bypass quote is not an exact positive change point");
            if (orderAtEventStart)
            {
                Accepted(run.ApplyCommand(
                    1260,
                    sequence,
                    RealtimeCommand.OrderLine()),
                    "parallel bypass order at event start");
            }
            return quote;
        }

        var probe = new RealtimeCampaignRun(_campaign, _world);
        RealtimeProjectQuote probeQuote = PrepareComparisonBypass(
            probe,
            orderAtEventStart: false);
        int buildMinutes = checked((int)probeQuote.BuildMinutes!.Value);
        RealtimeWorldDefinition world = _world with
        {
            ThermalClasses = _world.ThermalClasses.Select(item =>
                item.AssetKind == ThermalAssetKind.Edge &&
                item.ClassId == "STANDARD_LINE"
                    ? item with
                    {
                        Protection = item.Protection with
                        {
                            EmergencyExposureLimitMinutes = buildMinutes,
                        },
                    }
                    : item).ToArray(),
        };
        RealtimeWorldLoader.Validate(world, world.Network);
        RealtimeScheduledEventDefinition source = _campaign.Chapters[0].ScheduledEvents
            .Single(item => item.EventId == "FIRST_LIGHT_LINE_BOTTLENECK");
        RealtimeScheduledEventDefinition longEvent = source with
        {
            EventId = "CONSTRUCTION_TRIP_BOUNDARY",
            DurationMinutes = checked(buildMinutes + 6),
            OperatingProfile = source.OperatingProfile with
            {
                PhaseId = "CONSTRUCTION_TRIP_BOUNDARY",
                DisplayName = "공사 완공과 보호정지 동시 경계",
            },
        };
        RealtimeChapterDefinition chapter = _campaign.Chapters[0] with
        {
            ScheduledEvents = [longEvent],
        };
        RealtimeCampaignDefinition definition = _campaign with
        {
            Chapters = [chapter],
        };
        RealtimeCampaignLoader.Validate(definition, _baseCampaign, world);

        var run = new RealtimeCampaignRun(definition, world);
        RealtimeProjectQuote quote = PrepareComparisonBypass(
            run,
            orderAtEventStart: true);
        Equal((long)buildMinutes, quote.BuildMinutes!.Value,
            "custom protection changed bypass construction duration");
        long completionMinute = quote.CompletionMinute!.Value;
        RealtimeForecastEvent forecast = run.GetForecast().Events.Single();
        Check(forecast.TemporalProjection.Transitions.Any(item =>
                item.AssetId == "PLAYER_EDGE_1" &&
                item.Minute == completionMinute &&
                item.Kind == RealtimeThermalTransitionKind.ProtectiveTrip) &&
              !forecast.TemporalProjection.Transitions.Any(item =>
                item.AssetId == "PLAYER_EDGE_1" &&
                item.Minute == completionMinute &&
                item.Kind == RealtimeThermalTransitionKind.EmergencyCleared),
            "forecast did not preserve pending trip across bypass commissioning");

        RealtimeAdvanceResult boundary = run.AdvanceTo(completionMinute);
        int commissioned = IndexOf(boundary.Transitions, item =>
            item.Kind == RealtimeTransitionKind.ConstructionCompleted &&
            item.Minute == completionMinute);
        int trip = IndexOf(boundary.Transitions, item =>
            item.Kind == RealtimeTransitionKind.ThermalProtectiveTrip &&
            item.AssetId == "PLAYER_EDGE_1" &&
            item.Minute == completionMinute);
        Check(commissioned >= 0 && trip > commissioned,
            "same-boundary order was not construction commissioning then pending trip");
        Check(!boundary.Transitions.Any(item =>
                item.Kind == RealtimeTransitionKind.ThermalEmergencyCleared &&
                item.AssetId == "PLAYER_EDGE_1" &&
                item.Minute == completionMinute),
            "protective trip emitted a false EmergencyCleared transition");
        RealtimeThermalAssetSnapshot tripped = boundary.Snapshot.Thermal.Assets.Single(item =>
            item.AssetId == "PLAYER_EDGE_1");
        Check(tripped.ProtectiveOutage && tripped.UsedKw == 0,
            "commissioning canceled the already-earned pending trip");
        ThermalLoadSupply supplied = boundary.Snapshot.Thermal.Evaluation.Loads.Single();
        Check(supplied.DeliveredKw == 2000 &&
              !supplied.PathEdgeIds.Contains("PLAYER_EDGE_1", StringComparer.Ordinal) &&
              supplied.PathEdgeIds.Contains("PLAYER_EDGE_2", StringComparer.Ordinal) &&
              supplied.PathEdgeIds.Contains("PLAYER_EDGE_3", StringComparer.Ordinal),
            "post-trip supply used the tripped edge instead of the commissioned bypass");

        long eventEnd = checked(1260L + longEvent.DurationMinutes);
        RealtimeAdvanceResult ended = run.AdvanceTo(eventEnd);
        RealtimeEventOutcome actual = ended.Snapshot.CompletedChapters.Single().Events.Single();
        Equal(Json(forecast.TemporalProjection.Outcome), Json(actual),
            "pending-trip plus construction forecast differed from actual outcome");
    }

    private void Bottleneck(Archetype archetype)
    {
        (RealtimeWorldDefinition definition, CommercialWorldDefinition network, string targetId,
            ThermalAssetKind targetKind) = BottleneckWorld(archetype);
        ThermalIntervalEvaluation continuousBoundary =
            RealtimeSupplyAllocator.EvaluateInterval(
                network,
                new ThermalIntervalRequest(
                    $"{archetype}_CONTINUOUS_BOUNDARY",
                    [new ThermalLoadRequest(
                        "WATERWORKS",
                        1000,
                        ThermalPermission.ContinuousOnly)],
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<ThermalLimitOverride>()),
                Array.Empty<string>());
        Equal(ThermalOperatingState.Continuous,
            continuousBoundary.Assets.Single(item => item.AssetId == targetId).State,
            $"{archetype} exact continuous boundary");
        ThermalIntervalEvaluation emergencyBoundary =
            RealtimeSupplyAllocator.EvaluateInterval(
                network,
                new ThermalIntervalRequest(
                    $"{archetype}_EMERGENCY_BOUNDARY",
                    [new ThermalLoadRequest(
                        "WATERWORKS",
                        5000,
                        ThermalPermission.EmergencyAllowed)],
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<ThermalLimitOverride>()),
                Array.Empty<string>());
        Equal(5000L, emergencyBoundary.Loads.Single().DeliveredKw,
            $"{archetype} exact emergency boundary delivery");
        ThermalSupplyFailure overEmergency = RealtimeSupplyAllocator.EvaluateInterval(
                network,
                new ThermalIntervalRequest(
                    $"{archetype}_OVER_EMERGENCY",
                    [new ThermalLoadRequest(
                        "WATERWORKS",
                        5001,
                        ThermalPermission.EmergencyAllowed)],
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<ThermalLimitOverride>()),
                Array.Empty<string>())
            .Loads.Single().Failure ?? throw new InvalidOperationException(
                $"{archetype} over-emergency request unexpectedly supplied");
        Equal(ThermalFailureKind.EmergencyLimit, overEmergency.Kind,
            $"{archetype} over-emergency failure kind");
        Equal(targetId, overEmergency.AssetId,
            $"{archetype} over-emergency first blocker");
        var session = new RealtimeThermalSession(definition, network, 100);
        IReadOnlyList<RealtimeThermalTransition> entered = session.SetOperatingProfile(
            network,
            new ThermalIntervalRequest(
                $"{archetype}_OVERLOAD",
                [new ThermalLoadRequest(
                    "WATERWORKS",
                    2000,
                    ThermalPermission.EmergencyAllowed)],
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<ThermalLimitOverride>()));
        Check(entered.Any(item => item.AssetId == targetId &&
                item.Kind == RealtimeThermalTransitionKind.EmergencyEntered),
            $"{archetype} did not enter emergency");
        session.AdvanceTo(101);
        RealtimeThermalAssetSnapshot minute101 = Asset(session, targetId);
        Equal(1L, minute101.EmergencyExposureMinutes,
            $"{archetype} exposure after first minute");
        Check(!minute101.ProtectiveOutageUntilMinute.HasValue,
            $"{archetype} tripped one boundary early");
        IReadOnlyList<RealtimeThermalTransition> trip = session.AdvanceTo(102);
        Check(trip.Any(item => item.AssetId == targetId &&
                item.Kind == RealtimeThermalTransitionKind.ProtectiveTrip &&
                item.Minute == 102),
            $"{archetype} did not trip at M+1 pre-supply boundary");
        RealtimeThermalAssetSnapshot tripped = Asset(session, targetId);
        Equal(targetKind, tripped.AssetKind, $"{archetype} target asset kind");
        Equal(105L, tripped.ProtectiveOutageUntilMinute,
            $"{archetype} recovery change point");
        Check(session.GetSnapshot().Evaluation.Loads.Single().DeliveredKw == 0,
            $"{archetype} trip did not make the bottleneck unavailable");
        IReadOnlyList<RealtimeThermalTransition> recovered = session.AdvanceTo(105);
        Check(recovered.Any(item => item.AssetId == targetId &&
                item.Kind == RealtimeThermalTransitionKind.Recovered),
            $"{archetype} did not recover automatically");
        Check(session.GetSnapshot().Evaluation.Loads.Single().DeliveredKw == 2000,
            $"{archetype} did not resume supply on recovery");
        Check(session.GetSnapshot().Assets.Where(item => item.AssetId != targetId)
                .All(item => !item.ProtectiveOutageUntilMinute.HasValue),
            $"{archetype} caused a different archetype to trip");
    }

    private static ThermalIntervalRequest OverloadRequest(string intervalId) => new(
        intervalId,
        [new ThermalLoadRequest(
            "WATERWORKS",
            2000,
            ThermalPermission.EmergencyAllowed)],
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<ThermalLimitOverride>());

    private (RealtimeWorldDefinition Definition, CommercialWorldDefinition Network,
        string TargetId, ThermalAssetKind TargetKind) BottleneckWorld(Archetype archetype)
    {
        string[] nodeIds =
        [
            "WEST_SOURCE_NODE",
            "WEST_POLE",
            "WEST_HUB",
            "BRIDGE_NORTH",
            "EAST_NORTH_POLE",
            "NORTH_SUBSTATION",
            "WATER_TERMINAL",
        ];
        string[] edgeIds =
        [
            "EDGE_WEST_SOURCE",
            "EDGE_WEST_HUB",
            "EDGE_WEST_BRIDGE",
            "EDGE_BRIDGE_NORTH",
            "EDGE_NORTH_SUBSTATION",
            "EDGE_WATER",
        ];
        const string customPole = "CHECK_BOTTLENECK_POLE";
        const string customLine = "CHECK_BOTTLENECK_LINE";
        List<CommercialNodeClassDefinition> nodeClasses = _baseWorld.NodeClasses
            .Select(item => item.ThermalLimit is null
                ? item
                : item with { ThermalLimit = new ThermalLimit(10000, 12000) })
            .ToList();
        List<CommercialLineClassDefinition> lineClasses = _baseWorld.LineClasses
            .Select(item => item with { ThermalLimit = new ThermalLimit(10000, 12000) })
            .ToList();
        List<SpatialNodeDefinition> nodes = _baseWorld.Nodes
            .Where(item => nodeIds.Contains(item.NodeId, StringComparer.Ordinal))
            .ToList();
        List<SpatialEdgeDefinition> edges = _baseWorld.Edges
            .Where(item => edgeIds.Contains(item.EdgeId, StringComparer.Ordinal))
            .ToList();
        string targetId;
        ThermalAssetKind targetKind;
        string targetClass;
        if (archetype == Archetype.Pole)
        {
            CommercialNodeClassDefinition source = nodeClasses.Single(item =>
                item.ClassId == "STANDARD_POLE");
            nodeClasses.Add(source with
            {
                ClassId = customPole,
                DisplayName = "Check bottleneck pole",
                ThermalLimit = new ThermalLimit(1000, 5000),
            });
            int index = nodes.FindIndex(item => item.NodeId == "WEST_POLE");
            nodes[index] = nodes[index] with { ClassId = customPole };
            targetId = "WEST_POLE";
            targetKind = ThermalAssetKind.Node;
            targetClass = customPole;
        }
        else if (archetype == Archetype.Substation)
        {
            int index = nodeClasses.FindIndex(item => item.ClassId == "SMALL_SUBSTATION");
            nodeClasses[index] = nodeClasses[index] with
            {
                ThermalLimit = new ThermalLimit(1000, 5000),
            };
            targetId = "NORTH_SUBSTATION";
            targetKind = ThermalAssetKind.Node;
            targetClass = "SMALL_SUBSTATION";
        }
        else
        {
            CommercialLineClassDefinition source = lineClasses.Single(item =>
                item.ClassId == "STANDARD_LINE");
            lineClasses.Add(source with
            {
                ClassId = customLine,
                DisplayName = "Check bottleneck line",
                ThermalLimit = new ThermalLimit(1000, 5000),
            });
            int index = edges.FindIndex(item => item.EdgeId == "EDGE_WATER");
            edges[index] = edges[index] with { LineClassId = customLine };
            targetId = "EDGE_WATER";
            targetKind = ThermalAssetKind.Edge;
            targetClass = customLine;
        }

        var network = _baseWorld with
        {
            NodeClasses = nodeClasses,
            LineClasses = lineClasses,
            Nodes = nodes,
            Edges = edges,
            Sources = _baseWorld.Sources.Where(item => item.SourceId == "WEST_GENERATION")
                .Select(item => item with { CapacityKw = 10000 })
                .ToArray(),
            Loads = _baseWorld.Loads.Where(item => item.LoadId == "WATERWORKS").ToArray(),
        };
        CommercialWorldLoader.Validate(network);
        RealtimeThermalClassDefinition[] protections = network.NodeClasses
            .Where(item => item.ThermalLimit is not null)
            .Select(item => Protection(
                ThermalAssetKind.Node,
                item.ClassId,
                item.ThermalLimit!,
                item.ClassId == targetClass))
            .Concat(network.LineClasses.Select(item => Protection(
                ThermalAssetKind.Edge,
                item.ClassId,
                item.ThermalLimit,
                targetKind == ThermalAssetKind.Edge && item.ClassId == targetClass)))
            .OrderBy(item => item.AssetKind)
            .ThenBy(item => item.ClassId, StringComparer.Ordinal)
            .ToArray();
        var definition = new RealtimeWorldDefinition(
            RealtimeWorldLoader.SupportedSchemaVersion,
            network.WorldId,
            network,
            protections);
        RealtimeWorldLoader.Validate(definition, network);
        return (definition, network, targetId, targetKind);
    }

    private CommercialWorldDefinition AllocatorLayeredWorld(int stages)
    {
        if (stages <= 0 || stages > 22)
        {
            throw new ArgumentOutOfRangeException(nameof(stages));
        }

        var nodes = new List<SpatialNodeDefinition>
        {
            new(
                "LAYER_SOURCE_NODE",
                "SOURCE_TERMINAL",
                "Layered source",
                new MapPoint(100, 400),
                true,
                false),
        };
        var edges = new List<SpatialEdgeDefinition>();
        string previous = "LAYER_SOURCE_NODE";
        for (int stage = 0; stage < stages; stage++)
        {
            int originX = 100 + stage * 120;
            string prefix = $"LAYER_{stage:D2}";
            string upper = $"{prefix}_A_NODE";
            string lower = $"{prefix}_B_NODE";
            string merge = $"{prefix}_MERGE";
            nodes.Add(new SpatialNodeDefinition(
                upper,
                "REINFORCED_POLE",
                $"Layer {stage} upper",
                new MapPoint(originX + 60, 320),
                true,
                false));
            nodes.Add(new SpatialNodeDefinition(
                lower,
                "REINFORCED_POLE",
                $"Layer {stage} lower",
                new MapPoint(originX + 60, 480),
                true,
                false));
            nodes.Add(new SpatialNodeDefinition(
                merge,
                "REINFORCED_POLE",
                $"Layer {stage} merge",
                new MapPoint(originX + 120, 400),
                true,
                false));
            edges.Add(new SpatialEdgeDefinition(
                $"{prefix}_A_IN",
                "REINFORCED_LINE",
                previous,
                upper,
                true));
            edges.Add(new SpatialEdgeDefinition(
                $"{prefix}_A_OUT",
                "REINFORCED_LINE",
                upper,
                merge,
                true));
            edges.Add(new SpatialEdgeDefinition(
                $"{prefix}_B_IN",
                "REINFORCED_LINE",
                previous,
                lower,
                true));
            edges.Add(new SpatialEdgeDefinition(
                $"{prefix}_B_OUT",
                "REINFORCED_LINE",
                lower,
                merge,
                true));
            previous = merge;
        }

        nodes.Add(new SpatialNodeDefinition(
            "LAYER_SUBSTATION",
            "SMALL_SUBSTATION",
            "Layered substation",
            new MapPoint(2900, 400),
            true,
            false));
        nodes.Add(new SpatialNodeDefinition(
            "LAYER_LOAD_NODE",
            "LOAD_TERMINAL",
            "Layered load",
            new MapPoint(3100, 400),
            true,
            false));
        edges.Add(new SpatialEdgeDefinition(
            "LAYER_SUBSTATION_FEED",
            "REINFORCED_LINE",
            previous,
            "LAYER_SUBSTATION",
            true));
        edges.Add(new SpatialEdgeDefinition(
            "LAYER_LOAD_FEED",
            "REINFORCED_LINE",
            "LAYER_SUBSTATION",
            "LAYER_LOAD_NODE",
            true));

        CommercialWorldDefinition result = _baseWorld with
        {
            Terrain = Array.Empty<TerrainPolygonDefinition>(),
            RiskAreas = Array.Empty<SpatialRiskAreaDefinition>(),
            Nodes = nodes.OrderBy(item => item.NodeId, StringComparer.Ordinal).ToArray(),
            Edges = edges.OrderBy(item => item.EdgeId, StringComparer.Ordinal).ToArray(),
            Sources =
            [
                new CommercialSourceDefinition(
                    "LAYERED_SOURCE",
                    "Layered source",
                    "LAYER_SOURCE_NODE",
                    5000,
                    0),
            ],
            Loads =
            [
                new CommercialLoadDefinition(
                    "LAYERED_LOAD",
                    "Layered load",
                    "LAYER_LOAD_NODE"),
            ],
        };
        CommercialWorldLoader.Validate(result);
        return result;
    }

    private CommercialWorldDefinition AllocatorDiamondWorld(bool secondSource = false)
    {
        var nodes = new List<SpatialNodeDefinition>
        {
            new("A_POLE", "STANDARD_POLE", "A pole", new MapPoint(300, 300), true, false),
            new("B_POLE", "REINFORCED_POLE", "B pole", new MapPoint(300, 500), true, false),
            new("LOAD_ONE_NODE", "LOAD_TERMINAL", "Load one", new MapPoint(850, 200), true, false),
            new("LOAD_TWO_NODE", "LOAD_TERMINAL", "Load two", new MapPoint(850, 600), true, false),
            new("SOURCE_NODE", "SOURCE_TERMINAL", "Source", new MapPoint(100, 400), true, false),
            new("SUBSTATION", "SMALL_SUBSTATION", "Substation", new MapPoint(600, 400), true, false),
        };
        var edges = new List<SpatialEdgeDefinition>
        {
            new("A_IN", "STANDARD_LINE", "SOURCE_NODE", "A_POLE", true),
            new("A_OUT", "STANDARD_LINE", "A_POLE", "SUBSTATION", true),
            new("B_IN", "REINFORCED_LINE", "SOURCE_NODE", "B_POLE", true),
            new("B_OUT", "REINFORCED_LINE", "B_POLE", "SUBSTATION", true),
            new("LOAD_ONE", "REINFORCED_LINE", "SUBSTATION", "LOAD_ONE_NODE", true),
            new("LOAD_TWO", "REINFORCED_LINE", "SUBSTATION", "LOAD_TWO_NODE", true),
        };
        var sources = new List<CommercialSourceDefinition>
        {
            new(
                "DIAMOND_SOURCE",
                "Diamond source",
                "SOURCE_NODE",
                secondSource ? 300 : 5000,
                0),
        };
        if (secondSource)
        {
            nodes.Add(new SpatialNodeDefinition(
                "SOURCE_TWO_NODE",
                "SOURCE_TERMINAL",
                "Second source",
                new MapPoint(100, 800),
                true,
                false));
            edges.Add(new SpatialEdgeDefinition(
                "C_IN",
                "STANDARD_LINE",
                "SOURCE_TWO_NODE",
                "B_POLE",
                true));
            sources.Add(new CommercialSourceDefinition(
                "DIAMOND_SOURCE_TWO",
                "Second diamond source",
                "SOURCE_TWO_NODE",
                1000,
                1));
        }
        CommercialWorldDefinition result = _baseWorld with
        {
            Terrain = Array.Empty<TerrainPolygonDefinition>(),
            RiskAreas = Array.Empty<SpatialRiskAreaDefinition>(),
            Nodes = nodes.OrderBy(item => item.NodeId, StringComparer.Ordinal).ToArray(),
            Edges = edges.OrderBy(item => item.EdgeId, StringComparer.Ordinal).ToArray(),
            Sources = sources,
            Loads =
            [
                new CommercialLoadDefinition(
                    "DIAMOND_LOAD_ONE",
                    "Diamond load one",
                    "LOAD_ONE_NODE"),
                new CommercialLoadDefinition(
                    "DIAMOND_LOAD_TWO",
                    "Diamond load two",
                    "LOAD_TWO_NODE"),
            ],
        };
        CommercialWorldLoader.Validate(result);
        return result;
    }

    private static RealtimeThermalClassDefinition Protection(
        ThermalAssetKind kind,
        string classId,
        ThermalLimit limit,
        bool target) => new(
        kind,
        classId,
        new ThermalProtectionDefinition(
            limit.ContinuousKw,
            limit.EmergencyKw,
            target ? 2 : 100,
            1,
            target ? 3 : 100));

    private RealtimeThermalAssetSnapshot Asset(
        RealtimeThermalSession session,
        string assetId) => session.GetSnapshot().Assets.Single(item =>
        string.Equals(item.AssetId, assetId, StringComparison.Ordinal));

    private void StartJitLine(RealtimeCampaignRun run)
    {
        run.AdvanceTo(1248);
        long sequence = run.AcceptedCommands.Count + 1L;
        Accepted(run.ApplyCommand(
            1248,
            sequence++,
            RealtimeCommand.StartLineDraft(
                "SLICE_LINE_WEST_BANK",
                "STANDARD_LINE",
                "STANDARD_POLE")),
            "JIT line draft start");
        Accepted(run.ApplyCommand(
            1248,
            sequence++,
            RealtimeCommand.FinishLineDraft("SLICE_LINE_EAST_BANK")),
            "JIT line draft finish");
        RealtimeProjectQuote quote = run.PreviewLineOrder();
        Check(quote.Accepted && quote.CompletionMinute == 1260 &&
                quote.BuildMinutes == 12,
            "JIT line quote did not end at event boundary");
        Accepted(run.ApplyCommand(1248, sequence, RealtimeCommand.OrderLine()),
            "JIT line order");
    }

    private long FinalMinute()
    {
        long minute = _campaign.InitialSeed.StartMinute;
        foreach (RealtimeChapterDefinition chapter in _campaign.Chapters)
        {
            minute = checked(minute + chapter.Content.TimeAdvanceBeforeChapterMinutes);
            minute = checked(minute + chapter.EndOffsetMinutes);
        }
        return minute;
    }

    private void Accepted(RealtimeCommandResult result, string label) =>
        Check(result.Accepted, $"{label} rejected: {result.Error}/{result.ConstructionError}");

    private static int IndexOf<T>(
        IReadOnlyList<T> values,
        Func<T, bool> predicate)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (predicate(values[index]))
            {
                return index;
            }
        }
        return -1;
    }

    private void Check(bool condition, string message)
    {
        _assertions++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private void Equal<T>(T expected, T actual, string message)
    {
        _assertions++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message}: expected '{expected}', got '{actual}'.");
        }
    }

    private void SequenceEqual<T>(
        IEnumerable<T> expected,
        IEnumerable<T> actual,
        string message)
    {
        _assertions++;
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(message);
        }
    }

    private void Expect<T>(Action body, string message) where T : Exception
    {
        _assertions++;
        try
        {
            body();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException($"{message}: expected {typeof(T).Name}.");
    }

    private static JsonObject ParseObject(string json) =>
        JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("JSON root missing.");

    private static JsonObject Object(JsonObject parent, string name) =>
        parent[name]?.AsObject() ?? throw new InvalidOperationException($"{name} is not object.");

    private static JsonObject Object(JsonNode node) =>
        node.AsObject();

    private static JsonArray JsonArrayOf(JsonObject parent, string name) =>
        parent[name]?.AsArray() ?? throw new InvalidOperationException($"{name} is not array.");

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private enum Archetype
    {
        Pole,
        Substation,
        Line,
    }
}
