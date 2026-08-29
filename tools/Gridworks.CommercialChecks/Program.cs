using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;

namespace Gridworks.CommercialChecks;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] is "--story-manifest" or "--story-part")
            {
                return RunStoryCommand(args);
            }
            (string spatialPath, string worldPath, string coreSlicePath, string campaignPath) =
                ResolveFixturePaths(args);
            return new CommercialChecks(
                spatialPath,
                worldPath,
                coreSlicePath,
                campaignPath).Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL startup: {exception.Message}");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    internal static CommercialStoryPartHarness LoadCurrentStoryHarness()
    {
        string dataDirectory = ResolveCurrentDataDirectory();
        byte[] baseWorldBytes = File.ReadAllBytes(
            Path.Combine(dataDirectory, "release-world-v2.json"));
        byte[] baseCampaignBytes = File.ReadAllBytes(
            Path.Combine(dataDirectory, "release-campaign-v2.json"));
        byte[] realtimeWorldBytes = File.ReadAllBytes(
            Path.Combine(dataDirectory, "release-world-v3.json"));
        byte[] realtimeCampaignOverlayBytes = File.ReadAllBytes(
            Path.Combine(dataDirectory, "release-campaign-v3.json"));
        CommercialWorldDefinition baseWorld = CommercialWorldLoader.Load(baseWorldBytes);
        RealtimeWorldDefinition realtimeWorld = RealtimeWorldLoader.Load(
            realtimeWorldBytes,
            baseWorld);
        RealtimeCampaignOverlayLoadResult realtimeCampaign =
            RealtimeCampaignOverlayLoader.LoadAll(
            baseCampaignBytes,
            realtimeCampaignOverlayBytes,
            realtimeWorld);
        return new CommercialStoryPartHarness(
            realtimeCampaign.Campaign.Content,
            realtimeCampaign.Campaign);
    }

    private static int RunStoryCommand(string[] args)
    {
        if (args[0] == "--story-manifest" && args.Length != 1 ||
            args[0] == "--story-part" && args.Length != 2)
        {
            throw new ArgumentException(
                "usage: Gridworks.CommercialChecks --story-manifest | " +
                "--story-part SELECTOR");
        }

        CommercialStoryPartHarness harness = LoadCurrentStoryHarness();
        try
        {
            byte[] output = args[0] == "--story-manifest"
                ? harness.SerializeManifest()
                : harness.Serialize(harness.Select(args[1]));
            WriteJsonLine(Console.OpenStandardOutput(), output);
            return 0;
        }
        catch (CommercialStoryPartSelectionException exception)
        {
            WriteJsonLine(
                Console.OpenStandardError(),
                CommercialStoryPartHarness.SerializeError(exception));
            return 2;
        }
    }

    private static void WriteJsonLine(Stream stream, byte[] bytes)
    {
        stream.Write(bytes);
        stream.WriteByte((byte)'\n');
        stream.Flush();
    }

    private static string ResolveCurrentDataDirectory()
    {
        foreach (string start in new[]
                 {
                     Environment.CurrentDirectory,
                     AppContext.BaseDirectory,
                 })
        {
            DirectoryInfo? directory = new(Path.GetFullPath(start));
            while (directory is not null)
            {
                string candidate = Path.Combine(directory.FullName, "data");
                if (File.Exists(Path.Combine(candidate, "release-world-v2.json")) &&
                    File.Exists(Path.Combine(candidate, "release-campaign-v2.json")) &&
                    File.Exists(Path.Combine(candidate, "release-world-v3.json")) &&
                    File.Exists(Path.Combine(candidate, "release-campaign-v3.json")))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException(
            "Current release V2/V3 data directory was not found.");
    }

    private static (
        string SpatialPath,
        string WorldPath,
        string CoreSlicePath,
        string CampaignPath)
        ResolveFixturePaths(string[] args)
    {
        if (args.Length > 4)
        {
            throw new ArgumentException(
                "usage: Gridworks.CommercialChecks [commercial-spatial-json] " +
                "[release-world-v2-json] [commercial-core-slice-v1-json] " +
                "[release-campaign-v2-json]");
        }

        string spatialPath = args.Length >= 1
            ? args[0]
            : Path.Combine(
                Environment.CurrentDirectory,
                "data",
                "commercial-free-placement-slice-v1.json");
        spatialPath = Path.GetFullPath(spatialPath);
        string worldPath = args.Length >= 2
            ? Path.GetFullPath(args[1])
            : Path.Combine(
                Path.GetDirectoryName(spatialPath)!,
                "release-world-v2.json");
        string coreSlicePath = args.Length >= 3
            ? Path.GetFullPath(args[2])
            : Path.Combine(
                Path.GetDirectoryName(worldPath)!,
                "commercial-core-slice-v1.json");
        string campaignPath = args.Length == 4
            ? Path.GetFullPath(args[3])
            : Path.Combine(
                Path.GetDirectoryName(worldPath)!,
                "release-campaign-v2.json");
        if (!File.Exists(spatialPath))
        {
            throw new FileNotFoundException("Commercial spatial fixture not found.", spatialPath);
        }
        if (!File.Exists(worldPath))
        {
            throw new FileNotFoundException("Commercial world v2 fixture not found.", worldPath);
        }
        if (!File.Exists(coreSlicePath))
        {
            throw new FileNotFoundException(
                "Commercial core slice fixture not found.",
                coreSlicePath);
        }
        if (!File.Exists(campaignPath))
        {
            throw new FileNotFoundException(
                "Commercial campaign v2 fixture not found.",
                campaignPath);
        }
        return (spatialPath, worldPath, coreSlicePath, campaignPath);
    }
}

internal sealed class CommercialChecks
{
    private const string SourceClassId = "CHECK_SOURCE";
    private const string LoadClassId = "CHECK_LOAD";
    private const string PoleClassId = "CHECK_POLE";
    private const string SubstationClassId = "CHECK_SUBSTATION";
    private const string LineClassId = "CHECK_LINE";
    private const string ServiceLineClassId = "CHECK_SERVICE_LINE";

    private readonly byte[] _fixtureBytes;
    private readonly string _fixtureJson;
    private readonly SpatialWorldDefinition _fixture;
    private readonly byte[] _worldBytes;
    private readonly string _worldJson;
    private readonly CommercialWorldDefinition _commercialWorld;
    private readonly byte[] _coreSliceBytes;
    private readonly string _coreSliceJson;
    private readonly CommercialCoreSliceDefinition _coreSlice;
    private readonly byte[] _campaignBytes;
    private readonly string _campaignJson;
    private readonly CommercialCampaignDefinition _campaign;
    private int _assertionCount;

    public CommercialChecks(
        string fixturePath,
        string worldPath,
        string coreSlicePath,
        string campaignPath)
    {
        _fixtureBytes = File.ReadAllBytes(fixturePath);
        _fixtureJson = Encoding.UTF8.GetString(_fixtureBytes);
        _fixture = SpatialWorldLoader.Load(_fixtureBytes);
        _worldBytes = File.ReadAllBytes(worldPath);
        _worldJson = Encoding.UTF8.GetString(_worldBytes);
        _commercialWorld = CommercialWorldLoader.Load(_worldBytes);
        _coreSliceBytes = File.ReadAllBytes(coreSlicePath);
        _coreSliceJson = Encoding.UTF8.GetString(_coreSliceBytes);
        _coreSlice = CommercialCoreSliceLoader.Load(_coreSliceBytes, _commercialWorld);
        _campaignBytes = File.ReadAllBytes(campaignPath);
        _campaignJson = Encoding.UTF8.GetString(_campaignBytes);
        _campaign = CommercialCampaignLoader.Load(_campaignBytes, _commercialWorld);
    }

    public int Run()
    {
        (string Name, Action Body)[] suites =
        [
            ("strict-spatial-loader", CheckStrictSpatialLoader),
            ("integer-geometry-and-tangency", CheckIntegerGeometryAndTangency),
            ("node-placement-and-risk", CheckNodePlacementAndRisk),
            ("line-geometry-and-risk", CheckLineGeometryAndRisk),
            ("construction-lifecycle-quote-atomicity", CheckConstructionLifecycle),
            ("rejected-invariance-and-determinism", CheckRejectedInvarianceAndDeterminism),
            ("crossing-nonconnection-and-replay", CheckCrossingNonConnectionAndReplay),
            ("strict-commercial-world-loader", CheckStrictCommercialWorldLoader),
            ("thermal-boundaries-permission-shared-sum", CheckThermalBoundariesPermissionAndSharedSum),
            ("thermal-all-candidates-and-tiebreak", CheckThermalAllCandidatesAndTieBreak),
            ("thermal-unavailable-and-overrides", CheckThermalUnavailableAndOverrides),
            ("thermal-protective-outage-sequence", CheckThermalProtectiveOutageSequence),
            ("thermal-repeat-value-equality", CheckThermalRepeatValueEquality),
            ("strict-commercial-core-slice-loader", CheckStrictCommercialCoreSliceLoader),
            ("commercial-core-designs-preview-outcomes", CheckCommercialCoreDesignsPreviewAndOutcomes),
            ("commercial-core-risk-projection-approval", CheckCommercialCoreRiskProjectionApproval),
            ("commercial-core-rejections-and-recovery", CheckCommercialCoreRejectionsAndRecovery),
            ("commercial-core-replay-determinism", CheckCommercialCoreReplayDeterminism),
            ("commercial-core-save-v3", CheckCommercialCoreSaveV3),
            ("commercial-service-radius", CheckCommercialServiceRadius),
            ("strict-commercial-campaign-loader", CheckStrictCommercialCampaignLoader),
            ("realtime-authored-story-manifest", CheckRealtimeAuthoredStoryManifest),
            ("commercial-campaign-canonical-four-run", CheckCommercialCampaignCanonicalRun),
            ("commercial-campaign-archetypes-recovery", CheckCommercialCampaignArchetypesAndRecovery),
            ("commercial-campaign-rewind-replay", CheckCommercialCampaignRewindAndReplay),
            ("commercial-campaign-window-safety", CheckCommercialCampaignWindowSafety),
            ("commercial-campaign-save-v3", CheckCommercialCampaignSaveV3),
            ("commercial-campaign-canonical-eight-run", CheckCommercialCampaignCanonicalEightRun),
            ("commercial-campaign-stage-f-archetypes-recovery", CheckCommercialCampaignStageFArchetypesAndRecovery),
            ("commercial-campaign-completed-save-replay", CheckCommercialCampaignCompletedSaveAndReplay),
            ("stage-g-typed-ux-and-settings-v3", CheckStageGTypedUxAndSettingsV3),
        ];
        List<string> failures = [];
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

        if (failures.Count != 0)
        {
            Console.Error.WriteLine(
                $"Gridworks Commercial checks: FAIL ({failures.Count}/{suites.Length} suites)");
            return 1;
        }

        Console.WriteLine(
            $"Gridworks Commercial checks: PASS ({suites.Length} suites, {_assertionCount} assertions)");
        return 0;
    }

    private void CheckStrictSpatialLoader()
    {
        SpatialWorldDefinition fromText = SpatialWorldLoader.Load(_fixtureJson);
        SpatialWorldDefinition fromBytes = SpatialWorldLoader.Load(_fixtureBytes);
        Equal(_fixture.WorldId, fromText.WorldId, "text loader world ID");
        Equal(_fixture.WorldId, fromBytes.WorldId, "UTF-8 loader world ID");
        Equal(100, _fixture.UnitsPerDesignUnit, "fixed-point units per design unit");
        Equal(6, _fixture.Nodes.Count, "authored fixture node count");

        string trimmed = _fixtureJson.TrimStart();
        ExpectLoaderRejected(
            "duplicate JSON property",
            $"{{\"schemaVersion\":\"duplicate\",{trimmed[1..]}");
        ExpectLoaderRejected("invalid UTF-8", [0xff, 0xfe, 0xfd]);
        ExpectLoaderRejected("unknown root field", root => root["unexpected"] = true);
        ExpectLoaderRejected("missing required field", root => root.Remove("worldId"));
        ExpectLoaderRejected(
            "wrong fixed-point scale",
            root => root["unitsPerDesignUnit"] = 10);
        ExpectLoaderRejected(
            "future node capacity field",
            root => Object(JsonArrayProperty(root, "nodeClasses")[0]!)["capacityKw"] = 5000);
        ExpectLoaderRejected(
            "future line rating field",
            root => Object(JsonArrayProperty(root, "lineClasses")[0]!)["ratingKw"] = 2500);
        ExpectLoaderRejected(
            "duplicate node identifier",
            root => JsonArrayProperty(root, "nodes").Add(
                JsonArrayProperty(root, "nodes")[0]!.DeepClone()));
        ExpectLoaderRejected(
            "self-intersecting risk polygon",
            root => Object(JsonArrayProperty(root, "riskAreas")[0]!)["polygon"] = new JsonArray(
                PointJson(100, 100),
                PointJson(700, 100),
                PointJson(100, 500),
                PointJson(600, 700)));
        ExpectLoaderRejected(
            "adjacent polygon edge retrace",
            root => Object(JsonArrayProperty(root, "riskAreas")[0]!)["polygon"] = new JsonArray(
                PointJson(100, 100),
                PointJson(700, 100),
                PointJson(400, 100),
                PointJson(400, 700),
                PointJson(100, 700)));
        ExpectLoaderRejected(
            "zero-edge risk polygon",
            root =>
            {
                JsonArray polygon = JsonArrayProperty(
                    Object(JsonArrayProperty(root, "riskAreas")[0]!),
                    "polygon");
                polygon[1] = polygon[0]!.DeepClone();
            });
        ExpectLoaderRejected(
            "authored edge through building",
            root =>
            {
                JsonArrayProperty(root, "nodes").Add(NodeJson(
                    "BUILDING_END",
                    "LOAD_TERMINAL",
                    3150,
                    800,
                    authoredFoundation: true));
                JsonArrayProperty(root, "edges").Add(EdgeJson(
                    "BUILDING_EDGE",
                    "REINFORCED_LINE",
                    "EAST_RESIDENTIAL_TERMINAL",
                    "BUILDING_END"));
            });
        ExpectLoaderRejected(
            "authored edge through third-node footprint",
            root =>
            {
                JsonArrayProperty(root, "nodes").Add(NodeJson(
                    "THIRD_NODE_END",
                    "LOAD_TERMINAL",
                    300,
                    1150,
                    authoredFoundation: false));
                JsonArrayProperty(root, "edges").Add(EdgeJson(
                    "THIRD_NODE_EDGE",
                    "STANDARD_LINE",
                    "WEST_SOURCE",
                    "THIRD_NODE_END"));
            });
    }

    private void CheckIntegerGeometryAndTangency()
    {
        Equal(5L, FixedGeometry.CeilDistance(new MapPoint(0, 0), new MapPoint(3, 4)),
            "3-4-5 integer distance");
        Equal(2L, FixedGeometry.CeilDistance(new MapPoint(0, 0), new MapPoint(1, 1)),
            "irrational distance rounds upward");
        Equal(5L, FixedGeometry.CeilSquareRoot(17), "integer square-root ceiling");

        var bounds = new MapBounds(0, 0, 100, 100);
        Check(FixedGeometry.CircleWithinBounds(new MapPoint(10, 10), 10, bounds),
            "boundary-tangent footprint must remain inside");
        Check(!FixedGeometry.CircleWithinBounds(new MapPoint(9, 10), 10, bounds),
            "footprint crossing bounds was accepted");

        MapPoint[] square =
        [
            new(20, 20),
            new(80, 20),
            new(80, 80),
            new(20, 80),
        ];
        Check(FixedGeometry.ContainsPointInclusive(new MapPoint(20, 50), square),
            "polygon boundary must be inclusive");
        Check(FixedGeometry.CircleIntersectsPolygon(new MapPoint(10, 50), 10, square),
            "circle-polygon tangency must count as intersection");
        Check(FixedGeometry.SegmentTouchesCircle(
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(50, 10),
                10),
            "segment-circle tangency must count as contact");
        Check(!FixedGeometry.SegmentTouchesCircle(
                new MapPoint(int.MinValue, int.MinValue),
                new MapPoint(int.MaxValue, int.MinValue),
                new MapPoint(0, int.MaxValue),
                int.MaxValue),
            "extreme segment-circle comparison overflowed or reported a false contact");
        Check(FixedGeometry.SegmentsIntersectInclusive(
                new MapPoint(0, 50),
                new MapPoint(100, 50),
                new MapPoint(50, 0),
                new MapPoint(50, 100)),
            "noncollinear crossing was missed");
        Check(FixedGeometry.CollinearPositiveOverlap(
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(50, 0),
                new MapPoint(150, 0)),
            "positive collinear overlap was missed");
        Check(!FixedGeometry.CollinearPositiveOverlap(
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(100, 0),
                new MapPoint(150, 0)),
            "endpoint-only contact became positive overlap");
    }

    private void CheckStrictCommercialWorldLoader()
    {
        CommercialWorldDefinition fromText = CommercialWorldLoader.Load(_worldJson);
        CommercialWorldDefinition fromBytes = CommercialWorldLoader.Load(_worldBytes);
        Equal(_commercialWorld.WorldId, fromText.WorldId, "commercial text loader world ID");
        Equal(_commercialWorld.WorldId, fromBytes.WorldId, "commercial byte loader world ID");
        Equal(2, _commercialWorld.Sources.Count, "commercial authored source count");
        Equal(5, _commercialWorld.Loads.Count, "commercial authored load count");
        Check(_commercialWorld.NodeClasses
                .Where(item => item.Kind is SpatialNodeKind.Pole or SpatialNodeKind.Substation)
                .All(item => item.ThermalLimit is not null),
            "thermal node class lacks a limit");
        Check(_commercialWorld.NodeClasses
                .Where(item => item.Kind is SpatialNodeKind.SourceTerminal or
                    SpatialNodeKind.DedicatedLoadTerminal)
                .All(item => item.ThermalLimit is null),
            "terminal class owns a thermal limit");
        Equal(550, _commercialWorld.NodeClasses.Single(item =>
                item.ClassId == "SMALL_SUBSTATION").ServiceRadiusUnit,
            "small substation service radius");
        Equal(850, _commercialWorld.NodeClasses.Single(item =>
                item.ClassId == "LARGE_SUBSTATION").ServiceRadiusUnit,
            "large substation service radius");
        Check(_commercialWorld.NodeClasses
                .Where(item => item.Kind != SpatialNodeKind.Substation)
                .All(item => item.ServiceRadiusUnit is null),
            "non-substation class owns a service radius");
        Equal("NORTH_RESIDENTIAL_TERMINAL", _commercialWorld.Loads.Single(item =>
                item.LoadId == "NORTH_RESIDENTIAL").NodeId,
            "north-bank load terminal identity");

        string trimmed = _worldJson.TrimStart();
        ExpectCommercialLoaderRejected(
            "commercial duplicate JSON property",
            $"{{\"schemaVersion\":\"duplicate\",{trimmed[1..]}");
        ExpectCommercialLoaderRejected(
            "commercial unknown root field",
            root => root["unexpected"] = true);
        ExpectCommercialLoaderRejected(
            "commercial wrong schema",
            root => root["schemaVersion"] = "gridworks.commercial.world.future");
        ExpectCommercialLoaderRejected(
            "source identifier surrounding whitespace",
            root => Object(JsonArrayProperty(root, "sources")[0]!)["sourceId"] =
                " SOURCE_WITH_SPACES ");
        ExpectCommercialLoaderRejected(
            "load identifier surrounding whitespace",
            root => Object(JsonArrayProperty(root, "loads")[0]!)["loadId"] =
                " LOAD_WITH_SPACES ");
        ExpectCommercialLoaderRejected(
            "source terminal thermal limit",
            root => Object(JsonArrayProperty(root, "nodeClasses")[0]!)["thermalLimit"] =
                ThermalLimitJson(100, 120));
        ExpectCommercialLoaderRejected(
            "pole missing thermal limit",
            root => Object(JsonArrayProperty(root, "nodeClasses")[2]!)["thermalLimit"] = null);
        ExpectCommercialLoaderRejected(
            "substation missing service radius",
            root => Object(JsonArrayProperty(root, "nodeClasses")[4]!).Remove(
                "serviceRadiusUnit"));
        ExpectCommercialLoaderRejected(
            "substation null service radius",
            root => Object(JsonArrayProperty(root, "nodeClasses")[4]!)[
                "serviceRadiusUnit"] = null);
        ExpectCommercialLoaderRejected(
            "pole owns service radius",
            root => Object(JsonArrayProperty(root, "nodeClasses")[2]!)[
                "serviceRadiusUnit"] = 550);
        ExpectCommercialLoaderRejected(
            "nonpositive continuous limit",
            root => Object(
                Object(JsonArrayProperty(root, "lineClasses")[0]!)["thermalLimit"]!)[
                    "continuousKw"] = 0);
        ExpectCommercialLoaderRejected(
            "emergency limit below continuous",
            root => Object(
                Object(JsonArrayProperty(root, "lineClasses")[0]!)["thermalLimit"]!)[
                    "emergencyKw"] = 1);
        ExpectCommercialLoaderRejected(
            "source references load terminal",
            root => Object(JsonArrayProperty(root, "sources")[0]!)["nodeId"] =
                "HOSPITAL_TERMINAL");
        ExpectCommercialLoaderRejected(
            "load references source terminal",
            root => Object(JsonArrayProperty(root, "loads")[0]!)["nodeId"] =
                "WEST_SOURCE_NODE");
        ExpectCommercialLoaderRejected(
            "duplicate source dispatch order",
            root => Object(JsonArrayProperty(root, "sources")[1]!)["dispatchOrder"] = 0);
        ExpectCommercialLoaderRejected(
            "node-edge asset identifier collision",
            root => Object(JsonArrayProperty(root, "edges")[0]!)["edgeId"] =
                "WEST_SOURCE_NODE");
    }

    private void CheckStrictCommercialCoreSliceLoader()
    {
        CommercialCoreSliceDefinition fromText = CommercialCoreSliceLoader.Load(
            _coreSliceJson,
            _commercialWorld);
        CommercialCoreSliceDefinition fromBytes = CommercialCoreSliceLoader.Load(
            _coreSliceBytes,
            _commercialWorld);
        Equal(_coreSlice.SliceId, fromText.SliceId, "core slice text loader ID");
        Equal(_coreSlice.SliceId, fromBytes.SliceId, "core slice byte loader ID");
        Equal("FIRST_LIGHT_PRELUDE", _coreSlice.Prelude.Chapter.ChapterId,
            "prelude chapter identity");
        Equal("WHOSE_MARGIN", _coreSlice.Main.Chapter.ChapterId,
            "main chapter identity");
        Check(_coreSlice.Prelude.Seed.SeedId != _coreSlice.Main.Seed.SeedId,
            "prelude and four-chapter-complete seeds were merged");
        Check(_coreSlice.Prelude.Chapter.CityPromise is null,
            "no-penalty prelude gained a city promise");
        Equal("RIVER_FACTORY", _coreSlice.Main.Chapter.CityPromise!.LoadId,
            "chapter-five promise load");
        Equal(2, _coreSlice.Main.Chapter.DecisionWindows.Count,
            "bounded main decision-window count");
        Equal(2, _coreSlice.Main.Chapter.OperatingPhases.Count,
            "fixed main operating-phase count");
        Equal(200, _coreSlice.Main.Chapter.DecisionWindows[0].BuildMinutesAvailable,
            "main construction deadline");

        CommercialWorldDefinition preludeWorld = CommercialCoreSliceLoader.BuildSeedWorld(
            _commercialWorld,
            _coreSlice.Prelude.Seed);
        CommercialWorldDefinition mainWorld = CommercialCoreSliceLoader.BuildSeedWorld(
            _commercialWorld,
            _coreSlice.Main.Seed);
        Equal(8, preludeWorld.Nodes.Count,
            "prelude seed retains fixed terminals and east substation only");
        Equal(1, preludeWorld.Edges.Count,
            "prelude seed retains only the east service edge");
        Equal(_commercialWorld.Nodes.Count, mainWorld.Nodes.Count,
            "main seed world node count");
        Equal(_commercialWorld.Edges.Count + 1, mainWorld.Edges.Count,
            "main seed retains the four-chapter-complete graph and hospital bypass");
        SpatialEdgeDefinition hospitalBypass = mainWorld.Edges.Single(item =>
            item.EdgeId == "SEED_EDGE_EAST_HOSPITAL");
        Check(hospitalBypass.Commissioned &&
                hospitalBypass.LineClassId == "STANDARD_LINE" &&
                hospitalBypass.FromNodeId == "EAST_SUBSTATION" &&
                hospitalBypass.ToNodeId == "HOSPITAL_TERMINAL",
            "main seed hospital bypass identity");
        Equal(1000000L, preludeWorld.InitialCashUnit,
            "prelude seed independent cash");
        Equal(700000L, mainWorld.InitialCashUnit,
            "four-chapter-complete seed independent cash");

        string trimmed = _coreSliceJson.TrimStart();
        ExpectCoreSliceLoaderRejected(
            "core slice duplicate JSON property",
            $"{{\"schemaVersion\":\"duplicate\",{trimmed[1..]}");
        ExpectCoreSliceLoaderRejected(
            "core slice unknown root field",
            root => root["unexpected"] = true);
        ExpectCoreSliceLoaderRejected(
            "core slice eight-mission placeholder",
            root => root["missions"] = new JsonArray());
        ExpectCoreSliceLoaderRejected(
            "core slice wrong schema",
            root => root["schemaVersion"] = "gridworks.commercial.core-slice.future");
        ExpectCoreSliceLoaderRejected(
            "core slice merged prelude and main seed",
            root => Object(Object(root["main"]!)["seed"]!)["seedId"] =
                "FIRST_LIGHT_PRELUDE_SEED");
        ExpectCoreSliceLoaderRejected(
            "core slice seed drops a fixed terminal",
            root => JsonArrayProperty(
                Object(Object(root["prelude"]!)["seed"]!),
                "baseNodeIds").RemoveAt(0));
        ExpectCoreSliceLoaderRejected(
            "core slice seed edge lacks retained endpoint",
            root => JsonArrayProperty(
                Object(Object(root["prelude"]!)["seed"]!),
                "baseEdgeIds").Add("EDGE_WEST_SOURCE"));
        ExpectCoreSliceLoaderRejected(
            "core slice seed base IDs out of order",
            root =>
            {
                JsonArray ids = JsonArrayProperty(
                    Object(Object(root["main"]!)["seed"]!),
                    "baseNodeIds");
                string first = ids[0]!.GetValue<string>();
                string second = ids[1]!.GetValue<string>();
                ids[0] = second;
                ids[1] = first;
            });
        ExpectCoreSliceLoaderRejected(
            "core slice prelude promise",
            root => Object(Object(root["prelude"]!)["chapter"]!)["cityPromise"] =
                Object(Object(root["main"]!)["chapter"]!)["cityPromise"]!.DeepClone());
        ExpectCoreSliceLoaderRejected(
            "core slice missing main promise",
            root => Object(Object(root["main"]!)["chapter"]!)["cityPromise"] = null);
        ExpectCoreSliceLoaderRejected(
            "core slice null decision windows",
            root => Object(Object(root["main"]!)["chapter"]!)["decisionWindows"] = null);
        ExpectCoreSliceLoaderRejected(
            "core slice fourth decision window",
            root =>
            {
                JsonArray windows = JsonArrayProperty(
                    Object(Object(root["main"]!)["chapter"]!),
                    "decisionWindows");
                JsonObject third = Object(windows[0]!.DeepClone());
                third["windowId"] = "EXTRA_WINDOW_THREE";
                JsonObject fourth = Object(windows[0]!.DeepClone());
                fourth["windowId"] = "EXTRA_WINDOW_FOUR";
                windows.Add(third);
                windows.Add(fourth);
            });
        ExpectCoreSliceLoaderRejected(
            "core slice nonpositive construction deadline",
            root => Object(JsonArrayProperty(
                Object(Object(root["main"]!)["chapter"]!),
                "decisionWindows")[0]!)["buildMinutesAvailable"] = 0);
        ExpectCoreSliceLoaderRejected(
            "core slice window unknown phase",
            root => Object(JsonArrayProperty(
                Object(Object(root["main"]!)["chapter"]!),
                "decisionWindows")[0]!)["beforePhaseId"] = "UNKNOWN_PHASE");
        ExpectCoreSliceLoaderRejected(
            "core slice promise does not match phase load",
            root => Object(Object(
                Object(root["main"]!)["chapter"]!)["cityPromise"]!)["loadId"] =
                "HOSPITAL");
        ExpectCoreSliceLoaderRejected(
            "core slice phase unknown load",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(
                    Object(Object(root["main"]!)["chapter"]!),
                    "operatingPhases")[0]!),
                "loads")[0]!)["loadId"] = "UNKNOWN_LOAD");
        ExpectCoreSliceLoaderRejected(
            "core slice raised thermal override",
            root => JsonArrayProperty(
                Object(JsonArrayProperty(
                    Object(Object(root["main"]!)["chapter"]!),
                    "operatingPhases")[0]!),
                "thermalLimitOverrides").Add(new JsonObject
                {
                    ["assetKind"] = "edge",
                    ["classId"] = "STANDARD_LINE",
                    ["continuousKw"] = 2501,
                    ["emergencyKw"] = 3201,
                }));
        ExpectCoreSliceLoaderRejected(
            "core slice main standard result card",
            root =>
            {
                JsonObject results = Object(Object(
                    Object(root["main"]!)["chapter"]!)["resultCards"]!);
                results["standard"] = results["kept"]!.DeepClone();
            });
    }

    private void CheckThermalBoundariesPermissionAndSharedSum()
    {
        CommercialWorldDefinition direct = ThermalWorld(
            [
                Node("S", 100, 100),
                Node("T", 300, 100, SubstationClassId),
                Node("L", 500, 100, LoadClassId),
            ],
            [
                ThermalEdge("SERVICE", ServiceLineClassId, "S", "T"),
                ThermalEdge("E", LineClassId, "T", "L"),
            ],
            [ThermalSource("SOURCE", "S", 1000, 0)],
            [ThermalLoad("LOAD", "L")]);

        ThermalIntervalEvaluation exactContinuous = ThermalNetworkEvaluator.EvaluateInterval(
            direct,
            Interval("C", [LoadRequest("LOAD", 100, ThermalPermission.ContinuousOnly)]),
            ThermalState.Empty);
        ThermalLoadSupply continuousSupply = Supply(exactContinuous, "LOAD");
        Equal(100L, continuousSupply.DeliveredKw, "exact continuous delivery");
        Equal(0L, continuousSupply.MinimumRemainingKw, "exact continuous remaining limit");
        ThermalAssetUsage continuousEdge = Asset(exactContinuous, "E");
        Equal(ThermalOperatingState.Continuous, continuousEdge.State,
            "exact continuous state");
        Equal(ThermalOperatingState.Continuous, continuousEdge.NextState,
            "exact continuous next state");
        Equal(100L, continuousEdge.UsedKw, "exact continuous edge use");

        ThermalIntervalEvaluation exactEmergency = ThermalNetworkEvaluator.EvaluateInterval(
            direct,
            Interval("E", [LoadRequest("LOAD", 150, ThermalPermission.EmergencyAllowed)]),
            ThermalState.Empty);
        Equal(150L, Supply(exactEmergency, "LOAD").DeliveredKw,
            "exact emergency delivery");
        ThermalAssetUsage emergencyEdge = Asset(exactEmergency, "E");
        Equal(ThermalOperatingState.Emergency, emergencyEdge.State,
            "exact emergency state");
        Equal(ThermalOperatingState.ProtectiveOutage, emergencyEdge.NextState,
            "emergency next state");
        SequenceEqual(["E"], exactEmergency.NextThermalState.CoolingAssetIds,
            "emergency cooling state");

        ThermalIntervalEvaluation overEmergency = ThermalNetworkEvaluator.EvaluateInterval(
            direct,
            Interval("E_PLUS_ONE", [LoadRequest("LOAD", 151, ThermalPermission.EmergencyAllowed)]),
            ThermalState.Empty);
        ThermalLoadSupply overEmergencySupply = Supply(overEmergency, "LOAD");
        Equal(0L, overEmergencySupply.DeliveredKw, "emergency plus one must not deliver");
        Failure(
            overEmergencySupply,
            ThermalFailureKind.EmergencyLimit,
            "E",
            151,
            150,
            "emergency plus one failure");

        ThermalIntervalEvaluation continuousOnly = ThermalNetworkEvaluator.EvaluateInterval(
            direct,
            Interval("CONTINUOUS_ONLY", [LoadRequest("LOAD", 101, ThermalPermission.ContinuousOnly)]),
            ThermalState.Empty);
        Failure(
            Supply(continuousOnly, "LOAD"),
            ThermalFailureKind.ContinuousLimit,
            "E",
            101,
            100,
            "continuous-only permission");
        ThermalIntervalEvaluation emergencyAllowed = ThermalNetworkEvaluator.EvaluateInterval(
            direct,
            Interval("EMERGENCY_ALLOWED", [LoadRequest("LOAD", 101, ThermalPermission.EmergencyAllowed)]),
            ThermalState.Empty);
        Equal(101L, Supply(emergencyAllowed, "LOAD").DeliveredKw,
            "emergency permission did not admit demand above continuous");

        CommercialWorldDefinition shared = ThermalWorld(
        [
            Node("S", 100, 500),
            Node("P", 400, 500, PoleClassId),
            Node("T", 700, 500, SubstationClassId),
            Node("L1", 1000, 300, LoadClassId),
            Node("L2", 1000, 700, LoadClassId),
        ],
        [
            ThermalEdge("SHARED_A", LineClassId, "S", "P"),
            ThermalEdge("SHARED_B", LineClassId, "P", "T"),
            ThermalEdge("BRANCH_A", LineClassId, "T", "L1"),
            ThermalEdge("BRANCH_B", LineClassId, "T", "L2"),
        ],
        [ThermalSource("SOURCE", "S", 1000, 0)],
        [ThermalLoad("LOAD_1", "L1"), ThermalLoad("LOAD_2", "L2")]);
        ThermalIntervalEvaluation sharedEvaluation = ThermalNetworkEvaluator.EvaluateInterval(
            shared,
            Interval(
                "SHARED",
                [
                    LoadRequest("LOAD_1", 60, ThermalPermission.ContinuousOnly),
                    LoadRequest("LOAD_2", 40, ThermalPermission.ContinuousOnly),
                ]),
            ThermalState.Empty);
        Equal(60L, Supply(sharedEvaluation, "LOAD_1").DeliveredKw, "first shared load");
        Equal(40L, Supply(sharedEvaluation, "LOAD_2").DeliveredKw, "second shared load");
        Equal(100L, Asset(sharedEvaluation, "SHARED_A").UsedKw, "shared line A sum");
        Equal(100L, Asset(sharedEvaluation, "SHARED_B").UsedKw, "shared line B sum");
        Equal(100L, Asset(sharedEvaluation, "P").UsedKw, "shared pole sum");
        Equal(100L, Asset(sharedEvaluation, "T").UsedKw, "shared substation sum");
        Equal(60L, Asset(sharedEvaluation, "BRANCH_A").UsedKw, "first branch use");
        Equal(40L, Asset(sharedEvaluation, "BRANCH_B").UsedKw, "second branch use");
    }

    private void CheckThermalAllCandidatesAndTieBreak()
    {
        CommercialLineClassDefinition hot = ThermalLineClass("HOT_LINE", 50, 200);
        CommercialLineClassDefinition cool = ThermalLineClass("COOL_LINE", 200, 250);
        CommercialWorldDefinition allSources = ThermalWorld(
        [
            Node("S_PRIMARY", 500, 500),
            Node("S_SECONDARY", 100, 100),
            Node("T_PRIMARY", 650, 500, SubstationClassId),
            Node("T_SECONDARY", 300, 300, SubstationClassId),
            Node("P_SECONDARY", 100, 900, PoleClassId),
            Node("L", 900, 500, LoadClassId),
        ],
        [
            ThermalEdge("SERVICE_PRIMARY", ServiceLineClassId, "S_PRIMARY", "T_PRIMARY"),
            ThermalEdge("HOT_SHORT", "HOT_LINE", "T_PRIMARY", "L"),
            ThermalEdge("SERVICE_SECONDARY", ServiceLineClassId, "S_SECONDARY", "T_SECONDARY"),
            ThermalEdge("COOL_LONG_A", "COOL_LINE", "T_SECONDARY", "P_SECONDARY"),
            ThermalEdge("COOL_LONG_B", "COOL_LINE", "P_SECONDARY", "L"),
        ],
        [
            ThermalSource("PRIMARY", "S_PRIMARY", 500, 0),
            ThermalSource("SECONDARY", "S_SECONDARY", 500, 1),
        ],
        [ThermalLoad("LOAD", "L")],
        lineClasses: [hot, cool]);
        ThermalLoadSupply allSourceSupply = Supply(
            ThermalNetworkEvaluator.EvaluateInterval(
                allSources,
                Interval("ALL_SOURCES", [LoadRequest("LOAD", 100, ThermalPermission.EmergencyAllowed)]),
                ThermalState.Empty),
            "LOAD");
        Equal("SECONDARY", allSourceSupply.SourceId,
            "later source continuous path must beat first source emergency path");
        SequenceEqual(
            ["SERVICE_SECONDARY", "COOL_LONG_A", "COOL_LONG_B"],
            allSourceSupply.PathEdgeIds,
            "all-source continuous route");

        CommercialWorldDefinition allPaths = ThermalWorld(
        [
            Node("S", 100, 500),
            Node("T", 300, 500, SubstationClassId),
            Node("P", 500, 300, PoleClassId),
            Node("L", 900, 500, LoadClassId),
        ],
        [
            ThermalEdge("SERVICE", ServiceLineClassId, "S", "T"),
            ThermalEdge("HOT_DIRECT", "HOT_LINE", "T", "L"),
            ThermalEdge("COOL_A", "COOL_LINE", "T", "P"),
            ThermalEdge("COOL_B", "COOL_LINE", "P", "L"),
        ],
        [ThermalSource("SOURCE", "S", 500, 0)],
        [ThermalLoad("LOAD", "L")],
        lineClasses: [hot, cool]);
        ThermalLoadSupply allPathSupply = Supply(
            ThermalNetworkEvaluator.EvaluateInterval(
                allPaths,
                Interval("ALL_PATHS", [LoadRequest("LOAD", 100, ThermalPermission.EmergencyAllowed)]),
                ThermalState.Empty),
            "LOAD");
        SequenceEqual(["SERVICE", "COOL_A", "COOL_B"], allPathSupply.PathEdgeIds,
            "long continuous path must beat short emergency path");

        CommercialWorldDefinition tie = ThermalWorld(
        [
            Node("S", 100, 500),
            Node("T", 300, 500, SubstationClassId),
            Node("P_A", 500, 300, PoleClassId),
            Node("P_B", 500, 700, PoleClassId),
            Node("L", 900, 500, LoadClassId),
        ],
        [
            ThermalEdge("SERVICE", ServiceLineClassId, "S", "T"),
            ThermalEdge("A_1", LineClassId, "T", "P_A"),
            ThermalEdge("A_2", LineClassId, "P_A", "L"),
            ThermalEdge("B_1", LineClassId, "T", "P_B"),
            ThermalEdge("B_2", LineClassId, "P_B", "L"),
        ],
        [ThermalSource("SOURCE", "S", 500, 0)],
        [ThermalLoad("LOAD", "L")]);
        ThermalLoadSupply tieSupply = Supply(
            ThermalNetworkEvaluator.EvaluateInterval(
                tie,
                Interval("TIE", [LoadRequest("LOAD", 50, ThermalPermission.ContinuousOnly)]),
                ThermalState.Empty),
            "LOAD");
        SequenceEqual(["SERVICE", "A_1", "A_2"], tieSupply.PathEdgeIds,
            "edge-ID deterministic tie-break");
        SequenceEqual(["S", "T", "P_A", "L"], tieSupply.PathNodeIds,
            "node path for deterministic tie-break");
    }

    private void CheckThermalUnavailableAndOverrides()
    {
        CommercialWorldDefinition direct = ThermalWorld(
            [
                Node("S", 100, 100),
                Node("T", 300, 100, SubstationClassId),
                Node("L", 500, 100, LoadClassId),
            ],
            [
                ThermalEdge("SERVICE", ServiceLineClassId, "S", "T"),
                ThermalEdge("E", LineClassId, "T", "L"),
            ],
            [ThermalSource("SOURCE", "S", 1000, 0)],
            [ThermalLoad("LOAD", "L")]);
        ThermalLoadSupply unavailable = Supply(
            ThermalNetworkEvaluator.EvaluateInterval(
                direct,
                Interval(
                    "UNAVAILABLE",
                    [LoadRequest("LOAD", 50, ThermalPermission.ContinuousOnly)],
                    unavailableEdges: ["E"]),
                ThermalState.Empty),
            "LOAD");
        Failure(
            unavailable,
            ThermalFailureKind.AssetUnavailable,
            "E",
            50,
            0,
            "authored unavailable edge");

        CommercialWorldDefinition nodePath = ThermalWorld(
            [
                Node("S", 100, 300),
                Node("P", 400, 300, PoleClassId),
                Node("T", 550, 300, SubstationClassId),
                Node("L", 700, 300, LoadClassId),
            ],
            [
                ThermalEdge("E1", "COOL_LINE", "S", "P"),
                ThermalEdge("E2", "COOL_LINE", "P", "T"),
                ThermalEdge("SERVICE_OUT", ServiceLineClassId, "T", "L"),
            ],
            [ThermalSource("SOURCE", "S", 1000, 0)],
            [ThermalLoad("LOAD", "L")],
            lineClasses: [ThermalLineClass("COOL_LINE", 200, 250)]);
        ThermalLoadSupply unavailableNode = Supply(
            ThermalNetworkEvaluator.EvaluateInterval(
                nodePath,
                Interval(
                    "UNAVAILABLE_NODE",
                    [LoadRequest("LOAD", 50, ThermalPermission.ContinuousOnly)],
                    unavailableNodes: ["P"]),
                ThermalState.Empty),
            "LOAD");
        Failure(
            unavailableNode,
            ThermalFailureKind.AssetUnavailable,
            "P",
            50,
            0,
            "authored unavailable node");

        var lowered = new ThermalLimitOverride(
            ThermalAssetKind.Edge,
            LineClassId,
            80,
            90);
        ThermalIntervalEvaluation overridden = ThermalNetworkEvaluator.EvaluateInterval(
            direct,
            Interval(
                "OVERRIDE",
                [LoadRequest("LOAD", 100, ThermalPermission.EmergencyAllowed)],
                overrides: [lowered]),
            ThermalState.Empty);
        Failure(
            Supply(overridden, "LOAD"),
            ThermalFailureKind.EmergencyLimit,
            "E",
            100,
            90,
            "lowered authored class limit");
        Equal(80L, Asset(overridden, "E").ContinuousKw, "applied continuous override");
        Equal(90L, Asset(overridden, "E").EmergencyKw, "applied emergency override");

        ThermalIntervalEvaluation nodeOverride = ThermalNetworkEvaluator.EvaluateInterval(
            nodePath,
            Interval(
                "NODE_OVERRIDE",
                [LoadRequest("LOAD", 100, ThermalPermission.EmergencyAllowed)],
                overrides:
                [
                    new ThermalLimitOverride(
                        ThermalAssetKind.Node,
                        PoleClassId,
                        80,
                        90),
                ]),
            ThermalState.Empty);
        Failure(
            Supply(nodeOverride, "LOAD"),
            ThermalFailureKind.EmergencyLimit,
            "P",
            100,
            90,
            "lowered authored node-class limit");
        Equal(80L, Asset(nodeOverride, "P").ContinuousKw,
            "applied node continuous override");
        Equal(90L, Asset(nodeOverride, "P").EmergencyKw,
            "applied node emergency override");

        ThermalIntervalEvaluation cooling = ThermalNetworkEvaluator.EvaluateInterval(
            direct,
            Interval("COOLING", [LoadRequest("LOAD", 50, ThermalPermission.ContinuousOnly)]),
            new ThermalState(["E"]));
        Failure(
            Supply(cooling, "LOAD"),
            ThermalFailureKind.AssetUnavailable,
            "E",
            50,
            0,
            "protective-outage path");
        Equal(ThermalOperatingState.ProtectiveOutage, Asset(cooling, "E").State,
            "cooling asset current state");
        Equal(ThermalOperatingState.Continuous, Asset(cooling, "E").NextState,
            "cooling asset next state");

        CommercialWorldDefinition competingFailures = ThermalWorld(
            [
                Node("S", 100, 100),
                Node("T", 250, 100, SubstationClassId),
                Node("P", 300, 500, PoleClassId),
                Node("L", 500, 100, LoadClassId),
            ],
            [
                ThermalEdge("SERVICE", ServiceLineClassId, "S", "T"),
                ThermalEdge("SHORT_UNAVAILABLE", "HIGH_LINE", "T", "L"),
                ThermalEdge("LONG_LIMIT_A", "LOW_LINE", "T", "P"),
                ThermalEdge("LONG_LIMIT_B", "LOW_LINE", "P", "L"),
            ],
            [ThermalSource("SOURCE", "S", 1000, 0)],
            [ThermalLoad("LOAD", "L")],
            lineClasses:
            [
                ThermalLineClass("HIGH_LINE", 200, 250),
                ThermalLineClass("LOW_LINE", 80, 90),
            ]);
        ThermalLoadSupply competingFailure = Supply(
            ThermalNetworkEvaluator.EvaluateInterval(
                competingFailures,
                Interval(
                    "COMPETING_FAILURES",
                    [LoadRequest("LOAD", 100, ThermalPermission.EmergencyAllowed)],
                    unavailableEdges: ["SHORT_UNAVAILABLE"]),
                ThermalState.Empty),
            "LOAD");
        Failure(
            competingFailure,
            ThermalFailureKind.EmergencyLimit,
            "LONG_LIMIT_A",
            100,
            90,
            "longer thermal failure outranks short unavailable path");
        SequenceEqual(
            ["SERVICE", "LONG_LIMIT_A", "LONG_LIMIT_B"],
            competingFailure.PathEdgeIds,
            "failure diagnostic selects the relevant longer path");

        ExpectThrows<ArgumentException>(
            () => ThermalNetworkEvaluator.EvaluateInterval(
                direct,
                Interval(
                    "RAISED_OVERRIDE",
                    [LoadRequest("LOAD", 50, ThermalPermission.ContinuousOnly)],
                    overrides:
                    [
                        new ThermalLimitOverride(
                            ThermalAssetKind.Edge,
                            LineClassId,
                            101,
                            151),
                    ]),
                ThermalState.Empty),
            "limit override may not raise authored limits");
    }

    private void CheckThermalProtectiveOutageSequence()
    {
        CommercialWorldDefinition world = ThermalWorld(
            [
                Node("S", 100, 100),
                Node("T", 300, 100, SubstationClassId),
                Node("L", 500, 100, LoadClassId),
            ],
            [
                ThermalEdge("SERVICE", ServiceLineClassId, "S", "T"),
                ThermalEdge("E", LineClassId, "T", "L"),
            ],
            [ThermalSource("SOURCE", "S", 1000, 0)],
            [ThermalLoad("LOAD", "L")]);
        ThermalSequenceRequest request = CoolingSequence();
        ThermalSequenceEvaluation sequence = ThermalNetworkEvaluator.EvaluateSequence(
            world,
            request,
            ThermalState.Empty);
        Equal(3, sequence.Intervals.Count, "thermal transition interval count");

        ThermalIntervalEvaluation emergency = sequence.Intervals[0];
        Equal(120L, Supply(emergency, "LOAD").DeliveredKw,
            "emergency interval delivery");
        Equal(ThermalOperatingState.Emergency, Asset(emergency, "E").State,
            "emergency interval state");
        SequenceEqual(["E"], emergency.NextThermalState.CoolingAssetIds,
            "emergency schedules cooling");

        ThermalIntervalEvaluation outage = sequence.Intervals[1];
        Equal(0L, Supply(outage, "LOAD").DeliveredKw,
            "protective-outage interval delivery");
        Equal(ThermalOperatingState.ProtectiveOutage, Asset(outage, "E").State,
            "next interval protective outage");
        Equal(0, outage.NextThermalState.CoolingAssetIds.Count,
            "protective outage lasts exactly one interval");

        ThermalIntervalEvaluation returned = sequence.Intervals[2];
        Equal(100L, Supply(returned, "LOAD").DeliveredKw,
            "asset did not return after one cooling interval");
        Equal(ThermalOperatingState.Continuous, Asset(returned, "E").State,
            "returned asset state");
        Equal(0, sequence.FinalThermalState.CoolingAssetIds.Count,
            "final thermal state must be cooled");
    }

    private void CheckThermalRepeatValueEquality()
    {
        CommercialWorldDefinition world = ThermalWorld(
            [
                Node("S", 100, 100),
                Node("T", 300, 100, SubstationClassId),
                Node("L", 500, 100, LoadClassId),
            ],
            [
                ThermalEdge("SERVICE", ServiceLineClassId, "S", "T"),
                ThermalEdge("E", LineClassId, "T", "L"),
            ],
            [ThermalSource("SOURCE", "S", 1000, 0)],
            [ThermalLoad("LOAD", "L")]);
        ThermalSequenceRequest request = CoolingSequence();
        ThermalSequenceEvaluation first = ThermalNetworkEvaluator.EvaluateSequence(
            world,
            request,
            ThermalState.Empty);
        ThermalSequenceEvaluation second = ThermalNetworkEvaluator.EvaluateSequence(
            world,
            request,
            ThermalState.Empty);
        Equal(first, second, "repeated thermal sequence literal value equality");
        Equal(first.FinalThermalState, second.FinalThermalState,
            "repeated next thermal state literal value equality");
        Equal(first.GetHashCode(), second.GetHashCode(),
            "equal thermal sequence hash code");
    }

    private void CheckCommercialServiceRadius()
    {
        CommercialWorldDefinition direct = ThermalWorld(
            [Node("S", 100, 100), Node("L", 500, 100, LoadClassId)],
            [ThermalEdge("DIRECT", LineClassId, "S", "L")],
            [ThermalSource("SOURCE", "S", 1000, 0)],
            [ThermalLoad("LOAD", "L")]);
        Failure(
            Supply(
                ThermalNetworkEvaluator.EvaluateInterval(
                    direct,
                    Interval(
                        "DIRECT_WITHOUT_SUBSTATION",
                        [LoadRequest("LOAD", 50, ThermalPermission.ContinuousOnly)]),
                    ThermalState.Empty),
                "LOAD"),
            ThermalFailureKind.NoEligibleSubstation,
            null,
            50,
            0,
            "direct source-to-load service rejection");

        IReadOnlyList<CommercialNodeClassDefinition> serviceClasses =
        [
            new(SourceClassId, "검사 발전 접속점", SpatialNodeKind.SourceTerminal,
                10, 6, 0, 0, null),
            new(LoadClassId, "검사 부하 접속점", SpatialNodeKind.DedicatedLoadTerminal,
                10, 6, 0, 0, null),
            new("CHECK_SMALL_SUBSTATION", "검사 소형 변전소", SpatialNodeKind.Substation,
                20, 6, 100, 10, new ThermalLimit(1000, 1500), 299),
            new("CHECK_LARGE_SUBSTATION", "검사 대형 변전소", SpatialNodeKind.Substation,
                20, 6, 100, 10, new ThermalLimit(1000, 1500), 300),
        ];
        CommercialWorldDefinition small = ThermalWorld(
            [
                Node("S", 100, 100),
                Node("T", 300, 100, "CHECK_SMALL_SUBSTATION"),
                Node("L", 600, 100, LoadClassId),
            ],
            [
                ThermalEdge("SERVICE_IN", ServiceLineClassId, "S", "T"),
                ThermalEdge("SERVICE_OUT", ServiceLineClassId, "T", "L"),
            ],
            [ThermalSource("SOURCE", "S", 1000, 0)],
            [ThermalLoad("LOAD", "L")],
            nodeClasses: serviceClasses,
            lineClasses: Array.Empty<CommercialLineClassDefinition>());
        Failure(
            Supply(
                ThermalNetworkEvaluator.EvaluateInterval(
                    small,
                    Interval(
                        "OUTSIDE_SMALL_RADIUS",
                        [LoadRequest("LOAD", 50, ThermalPermission.ContinuousOnly)]),
                    ThermalState.Empty),
                "LOAD"),
            ThermalFailureKind.NoEligibleSubstation,
            null,
            50,
            0,
            "small substation service boundary");

        CommercialWorldDefinition large = ThermalWorld(
            [
                Node("S", 100, 100),
                Node("T", 300, 100, "CHECK_LARGE_SUBSTATION"),
                Node("L", 600, 100, LoadClassId),
            ],
            [
                ThermalEdge("SERVICE_IN", ServiceLineClassId, "S", "T"),
                ThermalEdge("SERVICE_OUT", ServiceLineClassId, "T", "L"),
            ],
            [ThermalSource("SOURCE", "S", 1000, 0)],
            [ThermalLoad("LOAD", "L")],
            nodeClasses: serviceClasses,
            lineClasses: Array.Empty<CommercialLineClassDefinition>());
        ThermalLoadSupply eligible = Supply(
            ThermalNetworkEvaluator.EvaluateInterval(
                large,
                Interval(
                    "AT_LARGE_RADIUS",
                    [LoadRequest("LOAD", 50, ThermalPermission.ContinuousOnly)]),
                ThermalState.Empty),
            "LOAD");
        Equal(50L, eligible.DeliveredKw,
            "large substation exact-radius service delivery");
        SequenceEqual(["S", "T", "L"], eligible.PathNodeIds,
            "eligible substation remains on the actual supply path");

        CommercialWorldDefinition simpleService = ThermalWorld(
            [
                Node("S", 100, 100),
                Node("J", 250, 100, PoleClassId),
                Node("L", 700, 100, LoadClassId),
                Node("DANGLING_SUB", 250, 300, SubstationClassId),
                Node("A", 100, 500, PoleClassId),
                Node("VALID_SUB", 400, 500, SubstationClassId),
                Node("B", 700, 500, PoleClassId),
            ],
            [
                ThermalEdge("SHORT_A", ServiceLineClassId, "S", "J"),
                ThermalEdge("SHORT_B", ServiceLineClassId, "J", "L"),
                ThermalEdge("DANGLING_SPUR", ServiceLineClassId, "J", "DANGLING_SUB"),
                ThermalEdge("LONG_A", ServiceLineClassId, "S", "A"),
                ThermalEdge("LONG_B", ServiceLineClassId, "A", "VALID_SUB"),
                ThermalEdge("LONG_C", ServiceLineClassId, "VALID_SUB", "B"),
                ThermalEdge("LONG_D", ServiceLineClassId, "B", "L"),
            ],
            [ThermalSource("SOURCE", "S", 1000, 0)],
            [ThermalLoad("LOAD", "L")]);
        ThermalIntervalRequest simpleRequest = Interval(
            "SIMPLE_SERVICE_ROUTE",
            [LoadRequest("LOAD", 50, ThermalPermission.ContinuousOnly)]);
        ThermalIntervalEvaluation firstSimple = ThermalNetworkEvaluator.EvaluateInterval(
            simpleService,
            simpleRequest,
            ThermalState.Empty);
        ThermalLoadSupply simpleSupply = Supply(firstSimple, "LOAD");
        Equal(50L, simpleSupply.DeliveredKw,
            "longer valid simple service route delivery");
        SequenceEqual(
            ["S", "A", "VALID_SUB", "B", "L"],
            simpleSupply.PathNodeIds,
            "dangling-substation walk must not beat a valid simple route");
        SequenceEqual(
            ["LONG_A", "LONG_B", "LONG_C", "LONG_D"],
            simpleSupply.PathEdgeIds,
            "longer valid simple service route edges");
        Equal(
            0,
            simpleSupply.PathNodeIds.Count - simpleSupply.PathNodeIds
                .Distinct(StringComparer.Ordinal).Count(),
            "accepted service route repeated a node");
        Equal(
            0,
            simpleSupply.PathEdgeIds.Count - simpleSupply.PathEdgeIds
                .Distinct(StringComparer.Ordinal).Count(),
            "accepted service route repeated an edge");
        Check(!simpleSupply.PathNodeIds.Contains("DANGLING_SUB", StringComparer.Ordinal),
            "dangling substation spur qualified the short source/load route");
        Equal(
            firstSimple,
            ThermalNetworkEvaluator.EvaluateInterval(
                simpleService,
                simpleRequest,
                ThermalState.Empty),
            "simple service path repeat value equality");
    }

    private void CheckStrictCommercialCampaignLoader()
    {
        CommercialCampaignDefinition fromText = CommercialCampaignLoader.Load(
            _campaignJson,
            _commercialWorld);
        CommercialCampaignDefinition fromBytes = CommercialCampaignLoader.Load(
            _campaignBytes,
            _commercialWorld);
        Equal(_campaign.CampaignId, fromText.CampaignId,
            "campaign text loader identity");
        Equal(_campaign.CampaignId, fromBytes.CampaignId,
            "campaign byte loader identity");
        SequenceEqual(
            [
                "FIRST_LIGHT",
                "SECOND_HEART",
                "SECOND_SOURCE",
                "NORTH_BANK_PROMISE",
                "WHOSE_MARGIN",
                "BEFORE_WATER_RISE",
                "SWITCH_OFF_TO_PROTECT",
                "LONGEST_NIGHT",
            ],
            _campaign.Chapters.Select(item => item.ChapterId).ToArray(),
            "canonical final campaign chapter identities");
        SequenceEqual(
            [
                "첫 불빛",
                "두 번째 심장",
                "두 번째 전원",
                "북안의 약속",
                "누구의 여유인가",
                "물이 닿기 전에",
                "꺼야 지킬 수 있다",
                "가장 긴 밤",
            ],
            _campaign.Chapters.Select(item => item.DisplayName).ToArray(),
            "canonical final campaign chapter titles");

        CommercialWorldDefinition initial = CommercialCampaignLoader.BuildInitialWorld(
            _commercialWorld,
            _campaign.InitialSeed);
        Equal(7, initial.Nodes.Count,
            "campaign starts with fixed source/load terminals only");
        Equal(0, initial.Edges.Count,
            "campaign starts without an inherited network");
        Check(!initial.Nodes.Any(node => _commercialWorld.NodeClasses.Single(item =>
                    item.ClassId == node.ClassId).Kind == SpatialNodeKind.Substation),
            "campaign initial seed inherited a substation");

        CommercialCampaignChapterDefinition firstLight = _campaign.Chapters[0];
        SequenceEqual(["SMALL_SUBSTATION"], firstLight.AvailableNodeClassIds,
            "first-light substation tools");
        Equal(
            new CommercialCampaignLinePlanDefinition("STANDARD_LINE", "STANDARD_POLE"),
            firstLight.AvailableLinePlans.Single(),
            "first-light line plan");
        Equal(0, firstLight.ConnectionRequirements.Count,
            "first-light connection requirements");

        CommercialCampaignChapterDefinition secondHeart = _campaign.Chapters[1];
        Equal(
            new CommercialCampaignConnectionRequirement("HOSPITAL_TERMINAL", 2),
            secondHeart.ConnectionRequirements.Single(),
            "hospital two-connection gate");
        Equal(2, secondHeart.OperatingPhases.Count,
            "hospital transfer and flood-test phases");
        SequenceEqual(
            ["RIVER_FLOOD_ZONE"],
            secondHeart.OperatingPhases[1].ActiveRiskAreaIds,
            "hospital flood-test risk activation");

        CommercialCampaignChapterDefinition secondSource = _campaign.Chapters[2];
        Check(secondSource.TimeAdvanceBeforeChapterMinutes == 0 &&
                !secondSource.ResetThermalStateBeforeChapter,
            "six-month reset happened before the second-source chapter");
        SequenceEqual(
            ["LARGE_SUBSTATION", "SMALL_SUBSTATION"],
            secondSource.AvailableNodeClassIds,
            "second-source substation tools");
        SequenceEqual(
            [
                new CommercialCampaignLinePlanDefinition(
                    "REINFORCED_LINE",
                    "REINFORCED_POLE"),
                new CommercialCampaignLinePlanDefinition(
                    "STANDARD_LINE",
                    "STANDARD_POLE"),
            ],
            secondSource.AvailableLinePlans,
            "second-source standard and reinforced plans");

        CommercialCampaignChapterDefinition northBank = _campaign.Chapters[3];
        Equal(262800, northBank.TimeAdvanceBeforeChapterMinutes,
            "six-month transition before north-bank chapter");
        Check(northBank.ResetThermalStateBeforeChapter,
            "north-bank transition did not reset thermal state");
        Equal("NORTH_RESIDENTIAL", northBank.CityPromise!.LoadId,
            "north-bank city-promise identity");
        Check(northBank.OperatingPhases.All(phase =>
                phase.ThermalPolicy == CommercialPhaseThermalPolicy.ContinuousOnly),
            "Stage-E chapter opened emergency operation");
        Equal("NEXT_HOT_EVENING_FORECAST", northBank.OperatingPhases[1].PhaseId,
            "chapter-five heat is forecast only");
        Equal(3, northBank.OperatingPhases[1].ThermalLimitOverrides.Count,
            "bounded heat-forecast override count");
        Check(_campaign.Chapters.Take(3).All(chapter =>
                chapter.CityPromise is null &&
                chapter.ResultCards.Standard is not null &&
                chapter.ResultCards.Kept is null &&
                chapter.ResultCards.Deferred is null &&
                chapter.ResultFactTemplates.KeptPromise is null &&
                chapter.ResultFactTemplates.DeferredPromise is null),
            "chapters one through three gained promise-only content");
        Check(northBank.ResultCards.Standard is null &&
                northBank.ResultCards.Kept is not null &&
                northBank.ResultCards.Deferred is not null &&
                northBank.ResultFactTemplates.KeptPromise is not null &&
                northBank.ResultFactTemplates.DeferredPromise is not null,
            "north-bank promise result shape");

        CommercialCampaignChapterDefinition whoseMargin = _campaign.Chapters[4];
        SequenceEqual(
            ["HOT_BASE", "NIGHT_SHIFT", "LATE_NIGHT"],
            whoseMargin.OperatingPhases.Select(phase => phase.PhaseId).ToArray(),
            "whose-margin operating sequence");
        Check(whoseMargin.OperatingPhases.All(phase =>
                phase.ThermalPolicy == CommercialPhaseThermalPolicy.ContinuousOnly),
            "whose-margin authored policy changed safety emergency permission");
        Equal("RIVER_FACTORY", whoseMargin.CityPromise!.LoadId,
            "whose-margin factory promise load");

        CommercialCampaignChapterDefinition beforeWaterRise = _campaign.Chapters[5];
        Equal(300, beforeWaterRise.DecisionWindows.Single().BuildMinutesAvailable,
            "before-water-rise deadline");
        Equal(
            new CommercialCampaignConnectionRequirement(
                "EAST_RESIDENTIAL_TERMINAL",
                2),
            beforeWaterRise.ConnectionRequirements.Single(),
            "before-water-rise east connection gate");
        CommercialOperatingPhaseDefinition floodArrival =
            beforeWaterRise.OperatingPhases.Single();
        Equal("FLOOD_ARRIVAL", floodArrival.PhaseId,
            "before-water-rise phase identity");
        SequenceEqual(["RIVER_FLOOD_ZONE"], floodArrival.ActiveRiskAreaIds,
            "before-water-rise flood activation");

        CommercialCampaignChapterDefinition switchOff = _campaign.Chapters[6];
        Equal(
            new CommercialCampaignConnectionRequirement("WATER_TERMINAL", 2),
            switchOff.ConnectionRequirements.Single(),
            "planned-outage water connection gate");
        SequenceEqual(
            ["WEST_SOURCE_PLANNED_OUTAGE", "WEST_SOURCE_RETURN_SERVICE"],
            switchOff.OperatingPhases.Select(phase => phase.PhaseId).ToArray(),
            "planned-outage operating sequence");
        Equal(
            CommercialPhaseThermalPolicy.SafetyEmergencyAllowed,
            switchOff.OperatingPhases[0].ThermalPolicy,
            "planned-outage emergency policy");
        SequenceEqual(
            ["WEST_SOURCE_NODE"],
            switchOff.OperatingPhases[0].UnavailableNodeIds,
            "planned-outage west-source isolation");
        Equal(
            CommercialPhaseThermalPolicy.ContinuousOnly,
            switchOff.OperatingPhases[1].ThermalPolicy,
            "return-service continuous policy");
        Check(!switchOff.OperatingPhases[1].UnavailableNodeIds.Contains(
                "WEST_SOURCE_NODE",
                StringComparer.Ordinal),
            "return-service retained the west-source outage");

        CommercialCampaignChapterDefinition longestNight = _campaign.Chapters[7];
        Equal(1, longestNight.DecisionWindows.Count,
            "longest-night decision-window count");
        SequenceEqual(
            ["MAX_DEMAND", "HEATWAVE_PEAK", "PROTECTIVE_STOP_FLOOD"],
            longestNight.OperatingPhases.Select(phase => phase.PhaseId).ToArray(),
            "longest-night operating sequence");
        Equal(
            CommercialPhaseThermalPolicy.ContinuousOnly,
            longestNight.OperatingPhases[0].ThermalPolicy,
            "maximum-demand continuous policy");
        Check(longestNight.OperatingPhases.Skip(1).All(phase =>
                phase.ThermalPolicy ==
                    CommercialPhaseThermalPolicy.SafetyEmergencyAllowed),
            "longest-night peak/final emergency policy");
        Check(longestNight.OperatingPhases[1].ThermalLimitOverrides.Count > 0 &&
                longestNight.OperatingPhases[2].ThermalLimitOverrides.Count > 0,
            "longest-night heat limits are not present in both stressed phases");
        ThermalLimitOverride heatwaveSmall = longestNight.OperatingPhases[1]
            .ThermalLimitOverrides.Single(item =>
                item.AssetKind == ThermalAssetKind.Node &&
                item.ClassId == "SMALL_SUBSTATION");
        ThermalLimitOverride floodSmall = longestNight.OperatingPhases[2]
            .ThermalLimitOverrides.Single(item =>
                item.AssetKind == ThermalAssetKind.Node &&
                item.ClassId == "SMALL_SUBSTATION");
        Check(heatwaveSmall is { ContinuousKw: 1500, EmergencyKw: 3000 } &&
                floodSmall is { ContinuousKw: 1800, EmergencyKw: 3000 },
            "longest-night small-substation heat derating/recovery profile drifted");
        SequenceEqual(
            longestNight.OperatingPhases[1].ThermalLimitOverrides.Where(item =>
                item.AssetKind != ThermalAssetKind.Node ||
                item.ClassId != "SMALL_SUBSTATION").ToArray(),
            longestNight.OperatingPhases[2].ThermalLimitOverrides.Where(item =>
                item.AssetKind != ThermalAssetKind.Node ||
                item.ClassId != "SMALL_SUBSTATION").ToArray(),
            "longest-night non-substation final heat limits differ from the heatwave");
        SequenceEqual(
            ["RIVER_FLOOD_ZONE"],
            longestNight.OperatingPhases[2].ActiveRiskAreaIds,
            "longest-night final flood activation");

        Equal(3, _campaign.Epilogue.PromiseLines.Count,
            "epilogue promise-line count");
        SequenceEqual(
            ["NORTH_BANK_PROMISE", "WHOSE_MARGIN", "BEFORE_WATER_RISE"],
            _campaign.Epilogue.PromiseLines.Select(line => line.ChapterId).ToArray(),
            "epilogue promise-line chapter order");
        string[] resultBodies = _campaign.Chapters
            .SelectMany(chapter => new[]
            {
                chapter.ResultCards.Standard,
                chapter.ResultCards.Kept,
                chapter.ResultCards.Deferred,
            })
            .Where(card => card is not null)
            .Select(card => card!.Body)
            .ToArray();
        Check(resultBodies.All(body =>
                !body.Contains("남긴 공간", StringComparison.Ordinal) &&
                !body.Contains("비워 둔 분기 공간", StringComparison.Ordinal)),
            "result card claims unmeasured future branch space");

        var speakers = new HashSet<string>(StringComparer.Ordinal);
        foreach (CommercialCampaignChapterDefinition chapter in _campaign.Chapters)
        {
            speakers.Add(chapter.Briefing.Speaker);
            foreach (CommercialDecisionWindowDefinition window in chapter.DecisionWindows)
            {
                if (window.Story is not null)
                {
                    speakers.Add(window.Story.Speaker);
                }
            }
            foreach (CommercialOperatingPhaseDefinition phase in chapter.OperatingPhases)
            {
                if (phase.Story is not null)
                {
                    speakers.Add(phase.Story.Speaker);
                }
            }
            foreach (CommercialStoryCard? card in new[]
                     {
                         chapter.ResultCards.Standard,
                         chapter.ResultCards.Kept,
                         chapter.ResultCards.Deferred,
                     })
            {
                if (card is not null)
                {
                    speakers.Add(card.Speaker);
                }
            }
        }
        Check(speakers.SetEquals(
            [
                "운영센터장 윤서진",
                "계통운영관 강민호",
                "의료원 시설책임자 박지현",
                "재난대응관 이도윤",
            ]),
            "campaign speakers differ from the fixed four characters");

        string trimmed = _campaignJson.TrimStart();
        ExpectCampaignLoaderRejected(
            "campaign duplicate JSON property",
            $"{{\"schemaVersion\":\"duplicate\",{trimmed[1..]}");
        ExpectCampaignLoaderRejected(
            "campaign unknown root field",
            root => root["unexpected"] = true);
        ExpectCampaignLoaderRejected(
            "campaign wrong schema",
            root => root["schemaVersion"] = "gridworks.release.campaign.future");
        ExpectCampaignLoaderRejected(
            "campaign omits canonical eighth chapter",
            root => JsonArrayProperty(root, "chapters").RemoveAt(7));
        ExpectCampaignLoaderRejected(
            "campaign canonical chapter order",
            root =>
            {
                JsonArray chapters = JsonArrayProperty(root, "chapters");
                JsonNode first = chapters[0]!.DeepClone();
                chapters[0] = chapters[1]!.DeepClone();
                chapters[1] = first;
            });
        ExpectCampaignLoaderRejected(
            "campaign seed drops a fixed terminal",
            root => JsonArrayProperty(Object(root["initialSeed"]!), "baseNodeIds")
                .RemoveAt(0));
        ExpectCampaignLoaderRejected(
            "campaign node tool references a pole class",
            root => JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[0]!),
                "availableNodeClassIds")[0] = "STANDARD_POLE");
        ExpectCampaignLoaderRejected(
            "campaign node tools out of ordinal order",
            root =>
            {
                JsonArray ids = JsonArrayProperty(
                    Object(JsonArrayProperty(root, "chapters")[2]!),
                    "availableNodeClassIds");
                JsonNode first = ids[0]!.DeepClone();
                ids[0] = ids[1]!.DeepClone();
                ids[1] = first;
            });
        ExpectCampaignLoaderRejected(
            "campaign line plan references a non-pole class",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[0]!),
                "availableLinePlans")[0]!)["poleClassId"] = "SMALL_SUBSTATION");
        ExpectCampaignLoaderRejected(
            "campaign duplicate line plan",
            root =>
            {
                JsonArray plans = JsonArrayProperty(
                    Object(JsonArrayProperty(root, "chapters")[2]!),
                    "availableLinePlans");
                plans.Add(plans[0]!.DeepClone());
            });
        ExpectCampaignLoaderRejected(
            "campaign invalid connection minimum",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[1]!),
                "connectionRequirements")[0]!)["minimumConnections"] = 1);
        ExpectCampaignLoaderRejected(
            "campaign removes canonical hospital connection gate",
            root => JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[1]!),
                "connectionRequirements").Clear());
        ExpectCampaignLoaderRejected(
            "campaign adds connection gate to another chapter",
            root => JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[0]!),
                "connectionRequirements").Add(new JsonObject
                {
                    ["nodeId"] = "EAST_RESIDENTIAL_TERMINAL",
                    ["minimumConnections"] = 2,
                }));
        ExpectCampaignLoaderRejected(
            "campaign cumulative grant overflow",
            root => Object(root["initialSeed"]!)["initialCashUnit"] =
                long.MaxValue - 2000000L);
        ExpectCampaignLoaderRejected(
            "campaign emergency policy before chapter five",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[0]!),
                "operatingPhases")[0]!)["thermalPolicy"] = "safetyEmergencyAllowed");
        ExpectCampaignLoaderRejected(
            "campaign phase without a safety duty",
            root => Object(JsonArrayProperty(Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[0]!),
                "operatingPhases")[0]!), "loads")[0]!)["obligation"] =
                "operatingRecord");
        ExpectCampaignLoaderRejected(
            "campaign null decision windows",
            root => Object(JsonArrayProperty(root, "chapters")[1]!)[
                "decisionWindows"] = null);
        ExpectCampaignLoaderRejected(
            "campaign inert emergency fact template",
            root => Object(Object(JsonArrayProperty(root, "chapters")[0]!)[
                "resultFactTemplates"]!)["emergencyAsset"] = "unused");
        ExpectCampaignLoaderRejected(
            "campaign supplied fact token order",
            root => Object(Object(JsonArrayProperty(root, "chapters")[0]!)[
                "resultFactTemplates"]!)["suppliedLoad"] =
                "{load} {phase} {source} {demandKw} {minimumRemainingKw}");
        ExpectCampaignLoaderRejected(
            "campaign promise fact in non-promise chapter",
            root => Object(Object(JsonArrayProperty(root, "chapters")[0]!)[
                "resultFactTemplates"]!)["keptPromise"] = "{promise}: 지킴");
        ExpectCampaignLoaderRejected(
            "campaign missing epilogue",
            root => root.Remove("epilogue"));
        ExpectCampaignLoaderRejected(
            "campaign epilogue promise order",
            root =>
            {
                JsonArray lines = JsonArrayProperty(Object(root["epilogue"]!), "promiseLines");
                JsonNode first = lines[0]!.DeepClone();
                lines[0] = lines[1]!.DeepClone();
                lines[1] = first;
            });
        ExpectCampaignLoaderRejected(
            "campaign whose-margin phase order",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[4]!),
                "operatingPhases")[0]!)["phaseId"] = "NIGHT_SHIFT");
        ExpectCampaignLoaderRejected(
            "campaign before-water-rise missing deadline",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[5]!),
                "decisionWindows")[0]!)["buildMinutesAvailable"] = null);
        ExpectCampaignLoaderRejected(
            "campaign before-water-rise missing east gate",
            root => JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[5]!),
                "connectionRequirements").Clear());
        ExpectCampaignLoaderRejected(
            "campaign return-service emergency policy",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[6]!),
                "operatingPhases")[1]!)["thermalPolicy"] =
                    "safetyEmergencyAllowed");
        ExpectCampaignLoaderRejected(
            "campaign planned-outage missing water gate",
            root => JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[6]!),
                "connectionRequirements").Clear());
        ExpectCampaignLoaderRejected(
            "campaign return-service keeps west unavailable",
            root => JsonArrayProperty(Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[6]!),
                "operatingPhases")[1]!), "unavailableNodeIds").Add(
                    "WEST_SOURCE_NODE"));
        ExpectCampaignLoaderRejected(
            "campaign longest-night extra window",
            root =>
            {
                JsonArray windows = JsonArrayProperty(
                    Object(JsonArrayProperty(root, "chapters")[7]!),
                    "decisionWindows");
                windows.Add(windows[0]!.DeepClone());
            });
        ExpectCampaignLoaderRejected(
            "campaign longest-night final heat limits missing",
            root => JsonArrayProperty(Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[7]!),
                "operatingPhases")[2]!), "thermalLimitOverrides").Clear());
        ExpectCampaignLoaderRejected(
            "campaign longest-night final flood missing",
            root => JsonArrayProperty(Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[7]!),
                "operatingPhases")[2]!), "activeRiskAreaIds").Clear());
    }

    private void CheckRealtimeAuthoredStoryManifest()
    {
        CommercialStoryPartHarness harness = Program.LoadCurrentStoryHarness();
        string[] expectedSelectors =
        [
            "FIRST_LIGHT/briefing",
            "FIRST_LIGHT/result/standard",
            "SECOND_HEART/briefing",
            "SECOND_HEART/result/standard",
            "SECOND_SOURCE/briefing",
            "SECOND_SOURCE/result/standard",
            "NORTH_BANK_PROMISE/briefing",
            "NORTH_BANK_PROMISE/window/NORTH_BANK_PLANNING_WINDOW",
            "NORTH_BANK_PROMISE/result/keep",
            "NORTH_BANK_PROMISE/result/defer",
            "WHOSE_MARGIN/briefing",
            "WHOSE_MARGIN/window/HOT_EVENING_PLANNING_WINDOW",
            "WHOSE_MARGIN/window/LATE_NIGHT_RECOVERY_WINDOW",
            "WHOSE_MARGIN/result/keep",
            "WHOSE_MARGIN/result/defer",
            "BEFORE_WATER_RISE/briefing",
            "BEFORE_WATER_RISE/window/BEFORE_FLOOD_WINDOW",
            "BEFORE_WATER_RISE/result/keep",
            "BEFORE_WATER_RISE/result/defer",
            "SWITCH_OFF_TO_PROTECT/briefing",
            "SWITCH_OFF_TO_PROTECT/window/BEFORE_PLANNED_OUTAGE_WINDOW",
            "SWITCH_OFF_TO_PROTECT/result/standard",
            "LONGEST_NIGHT/briefing",
            "LONGEST_NIGHT/window/FINAL_OPERATING_PLAN_WINDOW",
            "LONGEST_NIGHT/result/standard",
            "campaign/epilogue/card/city-report",
            "campaign/epilogue/card/medical-witness",
            "campaign/epilogue/card/closing",
            "campaign/epilogue/promise/NORTH_BANK_PROMISE/keep",
            "campaign/epilogue/promise/NORTH_BANK_PROMISE/defer",
            "campaign/epilogue/promise/WHOSE_MARGIN/keep",
            "campaign/epilogue/promise/WHOSE_MARGIN/defer",
            "campaign/epilogue/promise/BEFORE_WATER_RISE/keep",
            "campaign/epilogue/promise/BEFORE_WATER_RISE/defer",
        ];

        Equal(
            "gridworks.release.campaign.v2",
            harness.Campaign.SchemaVersion,
            "story authored-content schema");
        Equal(
            "gridworks.realtime.campaign.v3",
            harness.RealtimeCampaign.SchemaVersion,
            "story realtime-schedule schema");
        Equal(8, harness.RealtimeCampaign.Chapters.Count,
            "story realtime chapter schedule count");
        Equal(34, harness.Parts.Count, "authored narrative atom count");
        Equal(
            28,
            harness.Parts.Count(part => part.Content is CommercialStoryCardContent),
            "authored story-card atom count");
        Equal(
            6,
            harness.Parts.Count(part => part.Content is CommercialPromiseLineContent),
            "authored promise-line branch atom count");
        SequenceEqual(
            expectedSelectors,
            harness.Parts.Select(part => part.Selector).ToArray(),
            "authored narrative selector topology");

        byte[] firstManifest = harness.SerializeManifest();
        byte[] secondManifest = harness.SerializeManifest();
        SequenceEqual(firstManifest, secondManifest,
            "story manifest repeated raw serialization");
        using JsonDocument manifestDocument = JsonDocument.Parse(firstManifest);
        JsonElement manifest = manifestDocument.RootElement;
        Equal(6, manifest.EnumerateObject().Count(),
            "story manifest exact root field count");
        Equal(CommercialStoryPartHarness.ManifestSchemaVersion,
            manifest.GetProperty("schemaVersion").GetString(),
            "story manifest schema");
        Equal(harness.Campaign.CampaignId,
            manifest.GetProperty("campaignId").GetString(),
            "story manifest campaign identity");
        Equal(harness.Campaign.SchemaVersion,
            manifest.GetProperty("baseCampaignSchemaVersion").GetString(),
            "story manifest base campaign schema binding");
        Equal(harness.RealtimeCampaign.SchemaVersion,
            manifest.GetProperty("realtimeCampaignSchemaVersion").GetString(),
            "story manifest realtime campaign schema binding");
        Equal(34, manifest.GetProperty("count").GetInt32(),
            "story manifest JSON atom count");
        Equal(34, manifest.GetProperty("parts").GetArrayLength(),
            "story manifest JSON part count");

        IReadOnlyDictionary<string, RealtimeChapterDefinition> schedules =
            harness.RealtimeCampaign.Chapters.ToDictionary(
                chapter => chapter.Content.ChapterId,
                StringComparer.Ordinal);
        foreach (string selector in expectedSelectors)
        {
            CommercialStoryPart part = harness.Select(selector);
            Equal(selector, part.Selector, $"selected story identity {selector}");
            byte[] firstPart = harness.Serialize(part);
            byte[] secondPart = harness.Serialize(harness.Select(selector));
            SequenceEqual(
                firstPart,
                secondPart,
                $"selected story repeated serialization {selector}");
            CheckSerializedStoryPartContract(part, firstPart);
            if (part.ChapterId is null)
            {
                Check(part.RealtimeSchedule is null,
                    $"chapterless epilogue card gained a schedule: {selector}");
            }
            else
            {
                RealtimeChapterDefinition expectedSchedule = schedules[part.ChapterId];
                Check(part.RealtimeSchedule is not null,
                    $"chapter-owned story lacks schedule: {selector}");
                Equal(expectedSchedule.Content.ChapterId,
                    part.RealtimeSchedule!.ChapterId,
                    $"story schedule chapter {selector}");
                Equal(expectedSchedule.PreparationMinutes,
                    part.RealtimeSchedule.PreparationMinutes,
                    $"story preparation minutes {selector}");
                Equal(expectedSchedule.PromiseDecisionDeadlineOffsetMinutes,
                    part.RealtimeSchedule.PromiseDecisionDeadlineOffsetMinutes,
                    $"story promise deadline {selector}");
                SequenceEqual(
                    StoryScheduleEvents(expectedSchedule),
                    part.RealtimeSchedule.ScheduledEvents,
                    $"story ordered realtime events {selector}");
            }
        }

        CheckStoryScheduleNumberAuthority(harness);

        ExpectStorySelectionFailure(
            harness,
            "FIRST_LIGHT//briefing",
            CommercialStoryPartErrorCode.InvalidSelector,
            "malformed story selector");
        ExpectStorySelectionFailure(
            harness,
            "UNKNOWN_CHAPTER/briefing",
            CommercialStoryPartErrorCode.UnknownChapter,
            "unknown story chapter");
        ExpectStorySelectionFailure(
            harness,
            "FIRST_LIGHT/result/keep",
            CommercialStoryPartErrorCode.UnreachableStoryPart,
            "unreachable authored story branch");
    }

    private void CheckSerializedStoryPartContract(
        CommercialStoryPart part,
        byte[] serialized)
    {
        using JsonDocument document = JsonDocument.Parse(serialized);
        JsonElement root = document.RootElement;
        SequenceEqual(
            new[]
            {
                "schemaVersion",
                "campaignId",
                "selector",
                "kind",
                "chapterId",
                "windowId",
                "authoredReachable",
                "requiredPromiseBranch",
                "realtimeSchedule",
                "content",
            },
            root.EnumerateObject().Select(property => property.Name).ToArray(),
            $"story part exact root fields {part.Selector}");
        Equal(CommercialStoryPartHarness.OutputSchemaVersion,
            root.GetProperty("schemaVersion").GetString(),
            $"story part schema {part.Selector}");
        Equal(part.CampaignId,
            root.GetProperty("campaignId").GetString(),
            $"story part campaign {part.Selector}");
        Equal(part.Selector,
            root.GetProperty("selector").GetString(),
            $"story part selector {part.Selector}");
        Equal(StoryKindText(part.Kind),
            root.GetProperty("kind").GetString(),
            $"story part kind {part.Selector}");
        CheckNullableJsonString(
            root.GetProperty("chapterId"),
            part.ChapterId,
            $"story part chapter {part.Selector}");
        CheckNullableJsonString(
            root.GetProperty("windowId"),
            part.WindowId,
            $"story part window {part.Selector}");
        Check(root.GetProperty("authoredReachable").GetBoolean(),
            $"authored content must be reachable {part.Selector}");
        CheckNullableJsonString(
            root.GetProperty("requiredPromiseBranch"),
            StoryPromiseBranchText(part.RequiredPromiseBranch),
            $"story part promise branch {part.Selector}");

        JsonElement schedule = root.GetProperty("realtimeSchedule");
        if (part.RealtimeSchedule is null)
        {
            Equal(JsonValueKind.Null, schedule.ValueKind,
                $"story part null schedule {part.Selector}");
        }
        else
        {
            SequenceEqual(
                new[]
                {
                    "chapterId",
                    "preparationMinutes",
                    "promiseDecisionDeadlineOffsetMinutes",
                    "scheduledEvents",
                },
                schedule.EnumerateObject().Select(property => property.Name).ToArray(),
                $"story part exact schedule fields {part.Selector}");
            Equal(part.RealtimeSchedule.ChapterId,
                schedule.GetProperty("chapterId").GetString(),
                $"serialized story schedule chapter {part.Selector}");
            Equal(part.RealtimeSchedule.PreparationMinutes,
                schedule.GetProperty("preparationMinutes").GetInt32(),
                $"serialized story preparation minutes {part.Selector}");
            JsonElement deadline =
                schedule.GetProperty("promiseDecisionDeadlineOffsetMinutes");
            if (part.RealtimeSchedule.PromiseDecisionDeadlineOffsetMinutes is int value)
            {
                Equal(value, deadline.GetInt32(),
                    $"serialized story deadline {part.Selector}");
            }
            else
            {
                Equal(JsonValueKind.Null, deadline.ValueKind,
                    $"serialized story null deadline {part.Selector}");
            }
            JsonElement scheduledEvents = schedule.GetProperty("scheduledEvents");
            Equal(part.RealtimeSchedule.ScheduledEvents.Count,
                scheduledEvents.GetArrayLength(),
                $"serialized story event count {part.Selector}");
            for (int index = 0;
                 index < part.RealtimeSchedule.ScheduledEvents.Count;
                 index++)
            {
                CommercialRealtimeScheduledEventBinding expected =
                    part.RealtimeSchedule.ScheduledEvents[index];
                JsonElement actual = scheduledEvents[index];
                SequenceEqual(
                    new[]
                    {
                        "eventId",
                        "priority",
                        "startOffsetMinutes",
                        "durationMinutes",
                        "forecastLeadMinutes",
                    },
                    actual.EnumerateObject()
                        .Select(property => property.Name)
                        .ToArray(),
                    $"serialized story event exact fields {part.Selector}/{index}");
                Equal(expected.EventId,
                    actual.GetProperty("eventId").GetString(),
                    $"serialized story event identity {part.Selector}/{index}");
                Equal(expected.Priority,
                    actual.GetProperty("priority").GetInt32(),
                    $"serialized story event priority {part.Selector}/{index}");
                Equal(expected.StartOffsetMinutes,
                    actual.GetProperty("startOffsetMinutes").GetInt32(),
                    $"serialized story event start {part.Selector}/{index}");
                Equal(expected.DurationMinutes,
                    actual.GetProperty("durationMinutes").GetInt32(),
                    $"serialized story event duration {part.Selector}/{index}");
                Equal(expected.ForecastLeadMinutes,
                    actual.GetProperty("forecastLeadMinutes").GetInt32(),
                    $"serialized story event forecast lead {part.Selector}/{index}");
            }
        }

        JsonElement content = root.GetProperty("content");
        switch (part.Content)
        {
            case CommercialStoryCardContent story:
                SequenceEqual(
                    new[] { "contentType", "speaker", "title", "body" },
                    content.EnumerateObject().Select(property => property.Name).ToArray(),
                    $"story-card exact content fields {part.Selector}");
                Equal("story-card", content.GetProperty("contentType").GetString(),
                    $"story-card content type {part.Selector}");
                Equal(story.Card.Speaker, content.GetProperty("speaker").GetString(),
                    $"story-card speaker {part.Selector}");
                Equal(story.Card.Title, content.GetProperty("title").GetString(),
                    $"story-card title {part.Selector}");
                Equal(story.Card.Body, content.GetProperty("body").GetString(),
                    $"story-card body {part.Selector}");
                break;
            case CommercialPromiseLineContent promise:
                SequenceEqual(
                    new[] { "contentType", "promiseId", "branch", "text" },
                    content.EnumerateObject().Select(property => property.Name).ToArray(),
                    $"promise-line exact content fields {part.Selector}");
                Equal("promise-line", content.GetProperty("contentType").GetString(),
                    $"promise-line content type {part.Selector}");
                Equal(part.PromiseId, content.GetProperty("promiseId").GetString(),
                    $"promise-line promise identity {part.Selector}");
                Equal(StoryPromiseBranchText(part.RequiredPromiseBranch),
                    content.GetProperty("branch").GetString(),
                    $"promise-line branch {part.Selector}");
                Equal(promise.Text, content.GetProperty("text").GetString(),
                    $"promise-line text {part.Selector}");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(part));
        }
    }

    private void CheckStoryScheduleNumberAuthority(
        CommercialStoryPartHarness originalHarness)
    {
        RealtimeChapterDefinition sourceChapter = originalHarness.RealtimeCampaign
            .Chapters.Single(chapter => chapter.Content.ChapterId == "WHOSE_MARGIN");
        RealtimeScheduledEventDefinition sourceEvent = sourceChapter.ScheduledEvents
            .Single(scheduled => scheduled.EventId == "LATE_NIGHT");
        RealtimeScheduledEventDefinition changedEvent = sourceEvent with
        {
            Priority = checked(sourceEvent.Priority + 10),
            StartOffsetMinutes = checked(sourceEvent.StartOffsetMinutes + 7),
            DurationMinutes = checked(sourceEvent.DurationMinutes + 11),
            ForecastLeadMinutes = checked(sourceEvent.ForecastLeadMinutes + 5),
        };
        List<RealtimeScheduledEventDefinition> mutableEvents = sourceChapter
            .ScheduledEvents
            .Select(scheduled => scheduled.EventId == sourceEvent.EventId
                ? changedEvent
                : scheduled)
            .Reverse()
            .ToList();
        RealtimeChapterDefinition changedChapter = sourceChapter with
        {
            ScheduledEvents = mutableEvents,
        };
        mutableEvents.Clear();
        Equal(sourceChapter.ScheduledEvents.Count,
            changedChapter.ScheduledEvents.Count,
            "story changed V3 chapter defensively froze source event list");

        List<RealtimeChapterDefinition> mutableChapters = originalHarness
            .RealtimeCampaign.Chapters
            .Select(chapter => chapter.Content.ChapterId == sourceChapter.Content.ChapterId
                ? changedChapter
                : chapter)
            .ToList();
        RealtimeCampaignDefinition changedCampaign =
            originalHarness.RealtimeCampaign with
            {
                Chapters = mutableChapters,
            };
        mutableChapters.Clear();
        Equal(originalHarness.RealtimeCampaign.Chapters.Count,
            changedCampaign.Chapters.Count,
            "story changed V3 campaign defensively froze source chapter list");

        var changedHarness = new CommercialStoryPartHarness(
            originalHarness.Campaign,
            changedCampaign);
        CommercialStoryPart changedPart = changedHarness.Select(
            "WHOSE_MARGIN/briefing");
        CommercialRealtimeScheduleBinding changedSchedule =
            changedPart.RealtimeSchedule ??
            throw new InvalidOperationException(
                "Changed WHOSE_MARGIN story part lost realtime schedule.");
        SequenceEqual(
            StoryScheduleEvents(changedChapter),
            changedSchedule.ScheduledEvents,
            "story V3 schedule canonicalized reversed source event order");

        CommercialRealtimeScheduledEventBinding changedBinding = changedSchedule
            .ScheduledEvents.Single(scheduled => scheduled.EventId == sourceEvent.EventId);
        Equal(changedEvent.Priority,
            changedBinding.Priority,
            "story priority number follows V3 schedule authority");
        Equal(changedEvent.StartOffsetMinutes,
            changedBinding.StartOffsetMinutes,
            "story start number follows V3 schedule authority");
        Equal(changedEvent.DurationMinutes,
            changedBinding.DurationMinutes,
            "story duration number follows V3 schedule authority");
        Equal(changedEvent.ForecastLeadMinutes,
            changedBinding.ForecastLeadMinutes,
            "story forecast number follows V3 schedule authority");

        using JsonDocument changedDocument = JsonDocument.Parse(
            changedHarness.Serialize(changedPart));
        JsonElement changedEventJson = changedDocument.RootElement
            .GetProperty("realtimeSchedule")
            .GetProperty("scheduledEvents")
            .EnumerateArray()
            .Single(item => item.GetProperty("eventId").GetString() ==
                sourceEvent.EventId);
        Equal(changedEvent.Priority,
            changedEventJson.GetProperty("priority").GetInt32(),
            "serialized story priority follows V3 schedule authority");
        Equal(changedEvent.StartOffsetMinutes,
            changedEventJson.GetProperty("startOffsetMinutes").GetInt32(),
            "serialized story start follows V3 schedule authority");
        Equal(changedEvent.DurationMinutes,
            changedEventJson.GetProperty("durationMinutes").GetInt32(),
            "serialized story duration follows V3 schedule authority");
        Equal(changedEvent.ForecastLeadMinutes,
            changedEventJson.GetProperty("forecastLeadMinutes").GetInt32(),
            "serialized story forecast follows V3 schedule authority");

        List<CommercialRealtimeScheduledEventBinding> mutableBindings =
            changedSchedule.ScheduledEvents.ToList();
        CommercialRealtimeScheduleBinding defensivelyCopiedSchedule =
            changedSchedule with
            {
                ScheduledEvents = mutableBindings,
            };
        mutableBindings.Clear();
        Equal(changedSchedule.ScheduledEvents.Count,
            defensivelyCopiedSchedule.ScheduledEvents.Count,
            "story schedule binding defensively froze source event list");

        CommercialRealtimeScheduledEventBinding originalBinding = originalHarness
            .Select("WHOSE_MARGIN/briefing")
            .RealtimeSchedule!
            .ScheduledEvents.Single(scheduled => scheduled.EventId == sourceEvent.EventId);
        Equal(sourceEvent.Priority,
            originalBinding.Priority,
            "story schedule numeric mutation left original priority unchanged");
        Equal(sourceEvent.StartOffsetMinutes,
            originalBinding.StartOffsetMinutes,
            "story schedule numeric mutation left original start unchanged");
        Equal(sourceEvent.DurationMinutes,
            originalBinding.DurationMinutes,
            "story schedule numeric mutation left original duration unchanged");
        Equal(sourceEvent.ForecastLeadMinutes,
            originalBinding.ForecastLeadMinutes,
            "story schedule numeric mutation left original forecast unchanged");
    }

    private static CommercialRealtimeScheduledEventBinding[] StoryScheduleEvents(
        RealtimeChapterDefinition chapter) => chapter.ScheduledEvents
        .OrderBy(item => item.StartOffsetMinutes)
        .ThenBy(item => item.Priority)
        .ThenBy(item => item.EventId, StringComparer.Ordinal)
        .Select(item => new CommercialRealtimeScheduledEventBinding(
            item.EventId,
            item.Priority,
            item.StartOffsetMinutes,
            item.DurationMinutes,
            item.ForecastLeadMinutes))
        .ToArray();

    private void CheckNullableJsonString(
        JsonElement element,
        string? expected,
        string label)
    {
        if (expected is null)
        {
            Equal(JsonValueKind.Null, element.ValueKind, label);
        }
        else
        {
            Equal(JsonValueKind.String, element.ValueKind, $"{label} kind");
            Equal(expected, element.GetString(), label);
        }
    }

    private static string StoryKindText(CommercialStoryPartKind kind) => kind switch
    {
        CommercialStoryPartKind.Briefing => "briefing",
        CommercialStoryPartKind.Window => "window",
        CommercialStoryPartKind.Result => "result",
        CommercialStoryPartKind.EpilogueCard => "epilogue-card",
        CommercialStoryPartKind.EpiloguePromiseLine => "epilogue-promise-line",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string? StoryPromiseBranchText(
        CommercialPromiseDecision? branch) => branch switch
    {
        null => null,
        CommercialPromiseDecision.Keep => "keep",
        CommercialPromiseDecision.Defer => "defer",
        _ => throw new ArgumentOutOfRangeException(nameof(branch)),
    };

    private void ExpectStorySelectionFailure(
        CommercialStoryPartHarness harness,
        string selector,
        CommercialStoryPartErrorCode expected,
        string label)
    {
        try
        {
            _ = harness.Select(selector);
        }
        catch (CommercialStoryPartSelectionException exception)
        {
            Equal(expected, exception.ErrorCode, $"{label} error code");
            byte[] first = CommercialStoryPartHarness.SerializeError(exception);
            byte[] second = CommercialStoryPartHarness.SerializeError(exception);
            SequenceEqual(first, second, $"{label} deterministic error JSON");
            using JsonDocument document = JsonDocument.Parse(first);
            SequenceEqual(
                new[] { "schemaVersion", "selector", "errorCode", "message" },
                document.RootElement.EnumerateObject()
                    .Select(property => property.Name)
                    .ToArray(),
                $"{label} exact error fields");
            Equal(CommercialStoryPartHarness.ErrorSchemaVersion,
                document.RootElement.GetProperty("schemaVersion").GetString(),
                $"{label} error schema");
            Equal(selector,
                document.RootElement.GetProperty("selector").GetString(),
                $"{label} error selector");
            Equal(StoryErrorCodeText(expected),
                document.RootElement.GetProperty("errorCode").GetString(),
                $"{label} serialized error code");
            Equal(exception.Message,
                document.RootElement.GetProperty("message").GetString(),
                $"{label} serialized error message");
            return;
        }
        throw new InvalidOperationException(
            $"{label}: expected CommercialStoryPartSelectionException");
    }

    private static string StoryErrorCodeText(
        CommercialStoryPartErrorCode errorCode) => errorCode switch
    {
        CommercialStoryPartErrorCode.InvalidSelector => "INVALID_SELECTOR",
        CommercialStoryPartErrorCode.UnknownChapter => "UNKNOWN_CHAPTER",
        CommercialStoryPartErrorCode.UnreachableStoryPart =>
            "UNREACHABLE_STORY_PART",
        _ => throw new ArgumentOutOfRangeException(nameof(errorCode)),
    };

    private void CheckCommercialCampaignCanonicalRun()
    {
        var run = new CommercialCampaignRun(_campaign, _commercialWorld);
        CampaignRouteState routes = CompleteCampaignFirstLight(
            run,
            "canonical campaign");
        routes = CompleteCampaignSecondHeart(
            run,
            routes,
            secondCorridorSafe: false,
            "canonical campaign");
        routes = CompleteCampaignSecondSource(
            run,
            routes,
            reinforced: true,
            "canonical campaign");
        routes = CompleteCampaignNorthBank(
            run,
            routes,
            CommercialPromiseDecision.Keep,
            "canonical campaign");

        CommercialCampaignSnapshot completed = run.GetSnapshot();
        Check(!completed.CampaignComplete && completed.Chapter.ChapterId == "WHOSE_MARGIN",
            "canonical first-four run did not enter chapter five");
        Equal(4, completed.CompletedChapterOutcomes.Count,
            "canonical campaign outcome count");
        SequenceEqual(
            ["FIRST_LIGHT", "SECOND_HEART", "SECOND_SOURCE", "NORTH_BANK_PROMISE"],
            completed.CompletedChapterOutcomes.Select(item => item.ChapterId).ToArray(),
            "canonical campaign outcome order");
        Check(completed.CompletedChapterOutcomes.All(outcome =>
                outcome.RenderedFacts.Count > 0 &&
                outcome.RenderedFacts.All(fact =>
                    !fact.Contains('{', StringComparison.Ordinal) &&
                    !fact.Contains('}', StringComparison.Ordinal))),
            "campaign outcome retained an unresolved fact token");
        Equal(
            CommercialPromiseDecision.Keep,
            completed.CompletedChapterOutcomes[^1].PromiseDecision,
            "canonical north-bank promise outcome");
        Check(completed.CompletedChapterOutcomes[^1].RenderedFacts.Any(fact =>
                fact == "북안 입주 일정: 지킴"),
            "canonical outcome omitted kept promise fact");
        Check(completed.CompletedChapterOutcomes[^1].Facts.Loads.Any(fact =>
                fact.LoadId == "NORTH_RESIDENTIAL" &&
                fact.DeliveredKw == fact.DemandKw),
            "canonical outcome omitted actual north-bank delivery");
    }

    private CampaignRouteState CompleteCampaignFirstLight(
        CommercialCampaignRun run,
        string label)
    {
        CommercialCampaignSnapshot start = run.GetSnapshot();
        Equal("FIRST_LIGHT", start.Chapter.ChapterId,
            $"{label}: first-light chapter");
        Equal(7, start.Construction.World.Nodes.Count,
            $"{label}: first-light initial node count");
        Equal(0, start.Construction.World.Edges.Count,
            $"{label}: first-light initial edge count");
        Check(!start.CanApprove && start.FirstBlockingFailure is not null,
            $"{label}: first light did not require construction");
        Equal(ThermalFailureKind.NoTopologyPath, start.FirstBlockingFailure!.Kind,
            $"{label}: first-light initial failure");
        CampaignRejectedPreserves(
            run,
            CommercialCoreCommand.SetNodeDraft(
                "LARGE_SUBSTATION",
                new MapPoint(2250, 700)),
            CommercialCampaignRunError.ToolUnavailable,
            null,
            $"{label}: unavailable large substation");

        string eastSubstationId = BuildCampaignNode(
            run,
            "SMALL_SUBSTATION",
            new MapPoint(2250, 700),
            $"{label}: east substation");
        BuildCampaignLine(
            run,
            "WEST_SOURCE_NODE",
            "STANDARD_LINE",
            "STANDARD_POLE",
            [
                new MapPoint(750, 650),
                new MapPoint(1050, 650),
                new MapPoint(1600, 650),
                new MapPoint(2050, 650),
            ],
            eastSubstationId,
            $"{label}: west-to-east corridor");

        DraftCampaignLine(
            run,
            eastSubstationId,
            "STANDARD_LINE",
            "STANDARD_POLE",
            Array.Empty<MapPoint>(),
            "EAST_RESIDENTIAL_TERMINAL",
            $"{label}: east service line");
        CommercialCampaignSnapshot draft = run.GetSnapshot();
        Check(draft.ProjectionIncludesCurrentConstruction,
            $"{label}: first-light draft omitted from projection");
        ThermalIntervalEvaluation preview = CampaignProjection(
            draft,
            "FIRST_LIGHT_SUPPLY").Evaluation;
        Equal(800L, Supply(preview, "EAST_RESIDENTIAL").DeliveredKw,
            $"{label}: first-light draft delivery");
        FinishCampaignLine(run, $"{label}: east service line");
        Equal(
            preview,
            CampaignProjection(run.GetSnapshot(), "FIRST_LIGHT_SUPPLY").Evaluation,
            $"{label}: first-light draft/complete projection equality");
        Check(run.CommandCount > start.ChapterStartCommandCount,
            $"{label}: first-light completed without work");
        string carry = CampaignWorldSignature(run.GetSnapshot().Construction.World);
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            $"{label}: approve first light");
        CommercialCampaignSnapshot secondHeart = run.GetSnapshot();
        Equal("SECOND_HEART", secondHeart.Chapter.ChapterId,
            $"{label}: second-heart transition");
        Equal(carry, CampaignWorldSignature(secondHeart.Construction.World),
            $"{label}: first-light network carry identity");
        Equal(preview, secondHeart.LastOutcome!.Phases.Single().Evaluation,
            $"{label}: first-light preview/approved result equality");
        return new CampaignRouteState(eastSubstationId, null, null);
    }

    private CampaignRouteState CompleteCampaignSecondHeart(
        CommercialCampaignRun run,
        CampaignRouteState routes,
        bool secondCorridorSafe,
        string label)
    {
        CommercialCampaignSnapshot start = run.GetSnapshot();
        Equal("SECOND_HEART", start.Chapter.ChapterId,
            $"{label}: second-heart chapter");
        Equal(
            new CommercialCampaignConnectionFailure("HOSPITAL_TERMINAL", 0, 2),
            start.ConnectionFailures.Single(),
            $"{label}: hospital initial connection failure");
        Check(!start.CanApprove && start.FirstBlockingFailure is not null,
            $"{label}: second heart did not require construction");

        string highSubstationId = BuildCampaignNode(
            run,
            "SMALL_SUBSTATION",
            new MapPoint(2200, 1250),
            $"{label}: high hospital substation");
        string secondSubstationId = BuildCampaignNode(
            run,
            "SMALL_SUBSTATION",
            new MapPoint(2300, 1550),
            $"{label}: second hospital substation");
        BuildCampaignLine(
            run,
            "WEST_SOURCE_NODE",
            "STANDARD_LINE",
            "STANDARD_POLE",
            [
                new MapPoint(650, 900),
                new MapPoint(1050, 900),
                new MapPoint(1650, 900),
                new MapPoint(2050, 900),
            ],
            highSubstationId,
            $"{label}: high hospital corridor",
            quote => Equal(0, quote.RiskAreaIds.Count,
                $"{label}: high corridor risk exposure"));

        IReadOnlyList<MapPoint> secondPoints = secondCorridorSafe
            ? [
                new MapPoint(650, 750),
                new MapPoint(1050, 750),
                new MapPoint(1650, 750),
                new MapPoint(1950, 750),
                new MapPoint(2450, 1000),
            ]
            : [
                new MapPoint(650, 1080),
                new MapPoint(1050, 1200),
                new MapPoint(1150, 1500),
                new MapPoint(1725, 1500),
                new MapPoint(2050, 1550),
            ];
        BuildCampaignLine(
            run,
            "WEST_SOURCE_NODE",
            "STANDARD_LINE",
            "STANDARD_POLE",
            secondPoints,
            secondSubstationId,
            $"{label}: second hospital corridor",
            quote =>
            {
                if (secondCorridorSafe)
                {
                    Equal(0, quote.RiskAreaIds.Count,
                        $"{label}: two-safe corridor risk exposure");
                }
                else
                {
                    SequenceEqual(["RIVER_FLOOD_ZONE"], quote.RiskAreaIds,
                        $"{label}: cheap corridor risk exposure");
                }
            });

        BuildCampaignLine(
            run,
            highSubstationId,
            "STANDARD_LINE",
            "STANDARD_POLE",
            Array.Empty<MapPoint>(),
            "HOSPITAL_TERMINAL",
            $"{label}: high hospital service line");
        CommercialCampaignSnapshot oneConnection = run.GetSnapshot();
        Equal(
            new CommercialCampaignConnectionFailure("HOSPITAL_TERMINAL", 1, 2),
            oneConnection.ConnectionFailures.Single(),
            $"{label}: one hospital connection detail");
        CampaignRejectedPreserves(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            CommercialCampaignRunError.ConnectionRequirementUnmet,
            new CommercialCampaignConnectionFailure("HOSPITAL_TERMINAL", 1, 2),
            $"{label}: hospital two-connection approval gate");

        DraftCampaignLine(
            run,
            secondSubstationId,
            "STANDARD_LINE",
            "STANDARD_POLE",
            Array.Empty<MapPoint>(),
            "HOSPITAL_TERMINAL",
            $"{label}: second hospital service line");
        CommercialCampaignSnapshot draft = run.GetSnapshot();
        Check(draft.ProjectionIncludesCurrentConstruction,
            $"{label}: second hospital line omitted from projection");
        Equal(0, draft.ConnectionFailures.Count,
            $"{label}: projected second connection not counted");
        ThermalIntervalEvaluation[] previews = draft.Projections
            .Select(item => item.Evaluation)
            .ToArray();
        Equal(900L, Supply(previews[0], "HOSPITAL").DeliveredKw,
            $"{label}: hospital transfer-test preview");
        Equal(900L, Supply(previews[1], "HOSPITAL").DeliveredKw,
            $"{label}: hospital flood-test preview");
        FinishCampaignLine(run, $"{label}: second hospital service line");
        Check(run.CommandCount > start.ChapterStartCommandCount,
            $"{label}: second heart completed without work");
        string carry = CampaignWorldSignature(run.GetSnapshot().Construction.World);
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            $"{label}: approve second heart");
        CommercialCampaignSnapshot secondSource = run.GetSnapshot();
        Equal("SECOND_SOURCE", secondSource.Chapter.ChapterId,
            $"{label}: second-source transition");
        Equal(carry, CampaignWorldSignature(secondSource.Construction.World),
            $"{label}: second-heart network carry identity");
        SequenceEqual(
            previews,
            secondSource.LastOutcome!.Phases.Select(item => item.Evaluation).ToArray(),
            $"{label}: second-heart preview/approved result equality");
        return routes with
        {
            HospitalHighSubstationId = highSubstationId,
            HospitalSecondSubstationId = secondSubstationId,
        };
    }

    private CampaignRouteState CompleteCampaignSecondSource(
        CommercialCampaignRun run,
        CampaignRouteState routes,
        bool reinforced,
        string label)
    {
        CommercialCampaignSnapshot start = run.GetSnapshot();
        Equal("SECOND_SOURCE", start.Chapter.ChapterId,
            $"{label}: second-source chapter");
        Check(!start.CanApprove && start.FirstBlockingFailure is not null,
            $"{label}: second source did not require construction");
        string lineClassId = reinforced ? "REINFORCED_LINE" : "STANDARD_LINE";
        string poleClassId = reinforced ? "REINFORCED_POLE" : "STANDARD_POLE";
        BuildCampaignLine(
            run,
            "SOUTH_SOURCE_NODE",
            lineClassId,
            poleClassId,
            [
                new MapPoint(700, 1650),
                new MapPoint(1150, 1650),
                new MapPoint(1750, 1650),
                new MapPoint(2050, 1450),
            ],
            routes.HospitalHighSubstationId!,
            $"{label}: south-source main corridor");
        DraftCampaignLine(
            run,
            "HOSPITAL_TERMINAL",
            lineClassId,
            poleClassId,
            [
                new MapPoint(2550, 1050),
                new MapPoint(2550, 800),
            ],
            routes.EastSubstationId,
            $"{label}: south-to-east tie");
        CommercialCampaignSnapshot draft = run.GetSnapshot();
        Check(draft.ProjectionIncludesCurrentConstruction,
            $"{label}: south-to-east tie omitted from projection");
        ThermalIntervalEvaluation[] previews = draft.Projections
            .Select(item => item.Evaluation)
            .ToArray();
        Equal("WEST_GENERATION", Supply(previews[0], "EAST_RESIDENTIAL").SourceId,
            $"{label}: west-main test source");
        Equal("SOUTH_GENERATION", Supply(previews[1], "HOSPITAL").SourceId,
            $"{label}: south-source test source");
        Equal(800L, Supply(previews[1], "EAST_RESIDENTIAL").DeliveredKw,
            $"{label}: south-source east delivery");
        FinishCampaignLine(run, $"{label}: south-to-east tie");
        Check(run.CommandCount > start.ChapterStartCommandCount,
            $"{label}: second source completed without work");
        long beforeTransitionMinute = run.GetSnapshot().Minute;
        string carry = CampaignWorldSignature(run.GetSnapshot().Construction.World);
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            $"{label}: approve second source");
        CommercialCampaignSnapshot northBank = run.GetSnapshot();
        Equal("NORTH_BANK_PROMISE", northBank.Chapter.ChapterId,
            $"{label}: north-bank transition");
        Equal(carry, CampaignWorldSignature(northBank.Construction.World),
            $"{label}: second-source network carry identity");
        Equal(checked(beforeTransitionMinute + 262800L), northBank.Minute,
            $"{label}: six-month transition minute");
        Equal(ThermalState.Empty, northBank.ThermalState,
            $"{label}: six-month thermal reset");
        SequenceEqual(
            previews,
            northBank.LastOutcome!.Phases.Select(item => item.Evaluation).ToArray(),
            $"{label}: second-source preview/approved result equality");
        return routes;
    }

    private CampaignRouteState CompleteCampaignNorthBank(
        CommercialCampaignRun run,
        CampaignRouteState routes,
        CommercialPromiseDecision decision,
        string label)
    {
        CommercialCampaignSnapshot start = run.GetSnapshot();
        Equal("NORTH_BANK_PROMISE", start.Chapter.ChapterId,
            $"{label}: north-bank chapter");
        Check(!start.CanApprove,
            $"{label}: north bank approved without promise/work");
        CampaignAccepted(
            run,
            CommercialCoreCommand.SetPromiseDecision(decision),
            $"{label}: set north-bank promise");

        string? northLargeSubstationId = null;
        if (decision == CommercialPromiseDecision.Defer)
        {
            DraftCampaignLine(
                run,
                routes.EastSubstationId,
                "STANDARD_LINE",
                "STANDARD_POLE",
                Array.Empty<MapPoint>(),
                "WATER_TERMINAL",
                $"{label}: deferred water service line");
        }
        else
        {
            northLargeSubstationId = BuildCampaignNode(
                run,
                "LARGE_SUBSTATION",
                new MapPoint(2050, 400),
                $"{label}: north-bank large substation");
            BuildCampaignLine(
                run,
                routes.EastSubstationId,
                "REINFORCED_LINE",
                "REINFORCED_POLE",
                Array.Empty<MapPoint>(),
                northLargeSubstationId,
                $"{label}: north-bank reinforced feed");
            BuildCampaignLine(
                run,
                northLargeSubstationId,
                "REINFORCED_LINE",
                "REINFORCED_POLE",
                [
                    new MapPoint(2100, 600),
                    new MapPoint(2500, 460),
                ],
                "NORTH_RESIDENTIAL_TERMINAL",
                $"{label}: north residential service line");
            DraftCampaignLine(
                run,
                northLargeSubstationId,
                "STANDARD_LINE",
                "STANDARD_POLE",
                Array.Empty<MapPoint>(),
                "WATER_TERMINAL",
                $"{label}: north-bank water service line");
        }

        CommercialCampaignSnapshot draft = run.GetSnapshot();
        Check(draft.ProjectionIncludesCurrentConstruction,
            $"{label}: north-bank final draft omitted from projection");
        ThermalIntervalEvaluation[] previews = draft.Projections
            .Select(item => item.Evaluation)
            .ToArray();
        Equal(900L, Supply(previews[1], "WATERWORKS").DeliveredKw,
            $"{label}: heat-forecast water delivery");
        if (decision == CommercialPromiseDecision.Keep)
        {
            Equal(1100L, Supply(previews[1], "NORTH_RESIDENTIAL").DeliveredKw,
                $"{label}: heat-forecast north delivery");
        }
        else
        {
            Check(!previews[1].Loads.Any(item => item.LoadId == "NORTH_RESIDENTIAL"),
                $"{label}: deferred north load entered dispatch");
        }
        FinishCampaignLine(run, $"{label}: north-bank final line");
        Check(run.CommandCount > start.ChapterStartCommandCount,
            $"{label}: north bank completed without work");
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            $"{label}: approve north bank");
        CommercialCampaignSnapshot complete = run.GetSnapshot();
        Check(!complete.CampaignComplete && complete.Chapter.ChapterId == "WHOSE_MARGIN",
            $"{label}: north-bank approval did not enter chapter five");
        Equal(decision, complete.LastOutcome!.PromiseDecision,
            $"{label}: north-bank promise outcome");
        SequenceEqual(
            previews,
            complete.LastOutcome.Phases.Select(item => item.Evaluation).ToArray(),
            $"{label}: north-bank preview/approved result equality");
        return routes with { NorthLargeSubstationId = northLargeSubstationId };
    }

    private void CheckCommercialCampaignArchetypesAndRecovery()
    {
        var twoSafe = new CommercialCampaignRun(_campaign, _commercialWorld);
        CampaignRouteState routes = CompleteCampaignFirstLight(
            twoSafe,
            "two-safe archetype");
        routes = CompleteCampaignSecondHeart(
            twoSafe,
            routes,
            secondCorridorSafe: true,
            "two-safe archetype");
        Equal("SECOND_SOURCE", twoSafe.GetSnapshot().Chapter.ChapterId,
            "two-safe hospital design did not clear the flood test");
        Equal(900L, twoSafe.GetSnapshot().LastOutcome!.Facts.Loads.Single(fact =>
                fact.PhaseId == "FLOOD_ISOLATION_TEST" &&
                fact.LoadId == "HOSPITAL").DeliveredKw,
            "two-safe hospital flood-test outcome");

        var reinforced = CommercialCampaignRun.Restore(
            _campaign,
            _commercialWorld,
            twoSafe.Commands);
        CampaignRouteState standardRoutes = CompleteCampaignSecondSource(
            twoSafe,
            routes,
            reinforced: false,
            "standard second-source archetype");
        CompleteCampaignSecondSource(
            reinforced,
            routes,
            reinforced: true,
            "reinforced second-source archetype");
        CommercialCampaignSnapshot standardStart = twoSafe.GetSnapshot();
        CommercialCampaignSnapshot reinforcedStart = reinforced.GetSnapshot();
        Check(standardStart.CashUnit > reinforcedStart.CashUnit,
            "standard second-source corridor did not preserve more cash");
        long standardMargin = standardStart.LastOutcome!.Facts.Loads.Single(fact =>
            fact.PhaseId == "SOUTH_SOURCE_COMMISSIONING_TEST" &&
            fact.LoadId == "EAST_RESIDENTIAL").MinimumRemainingKw!.Value;
        long reinforcedMargin = reinforcedStart.LastOutcome!.Facts.Loads.Single(fact =>
            fact.PhaseId == "SOUTH_SOURCE_COMMISSIONING_TEST" &&
            fact.LoadId == "EAST_RESIDENTIAL").MinimumRemainingKw!.Value;
        Equal(standardMargin, reinforcedMargin,
            "second-source shared downstream bottleneck changed by line class");
        long standardLargestUsedLimit = standardStart.LastOutcome.Facts.ThermalAssets
            .Where(asset => asset.PhaseId == "SOUTH_SOURCE_COMMISSIONING_TEST" &&
                asset.UsedKw > 0)
            .Max(asset => asset.ContinuousLimitKw);
        long reinforcedLargestUsedLimit = reinforcedStart.LastOutcome.Facts.ThermalAssets
            .Where(asset => asset.PhaseId == "SOUTH_SOURCE_COMMISSIONING_TEST" &&
                asset.UsedKw > 0)
            .Max(asset => asset.ContinuousLimitKw);
        Check(reinforcedLargestUsedLimit > standardLargestUsedLimit,
            "reinforced second-source corridor omitted its higher local line limit");

        CampaignAccepted(
            twoSafe,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Keep),
            "promise recovery: keep north-bank promise");
        BuildCampaignLine(
            twoSafe,
            standardRoutes.EastSubstationId,
            "STANDARD_LINE",
            "STANDARD_POLE",
            Array.Empty<MapPoint>(),
            "WATER_TERMINAL",
            "promise recovery: water-only construction");
        CommercialCampaignSnapshot keptFailure = twoSafe.GetSnapshot();
        Equal(ThermalFailureKind.NoTopologyPath, keptFailure.FirstBlockingFailure!.Kind,
            "promise recovery: missing north-bank path diagnostic");
        CampaignRejectedPreserves(
            twoSafe,
            CommercialCoreCommand.ApproveDecisionWindow(),
            CommercialCampaignRunError.KeptPromiseUnserved,
            null,
            "promise recovery: kept promise without north route");
        CampaignAccepted(
            twoSafe,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Defer),
            "promise recovery: defer north-bank promise");
        ThermalIntervalEvaluation[] deferredPreview = twoSafe.GetSnapshot().Projections
            .Select(item => item.Evaluation)
            .ToArray();
        CampaignAccepted(
            twoSafe,
            CommercialCoreCommand.ApproveDecisionWindow(),
            "promise recovery: approve deferred north-bank plan");
        CommercialCampaignSnapshot deferred = twoSafe.GetSnapshot();
        Check(!deferred.CampaignComplete && deferred.Chapter.ChapterId == "WHOSE_MARGIN",
            "deferred north-bank recovery did not enter chapter five");
        Equal(CommercialPromiseDecision.Defer, deferred.LastOutcome!.PromiseDecision,
            "deferred north-bank recovery outcome");
        Check(deferred.LastOutcome.RenderedFacts.Contains("북안 입주 일정: 미룸"),
            "deferred promise fact missing");
        Check(!deferred.LastOutcome.Facts.Loads.Any(fact =>
                fact.LoadId == "NORTH_RESIDENTIAL"),
            "deferred north load entered committed facts");
        SequenceEqual(
            deferredPreview,
            deferred.LastOutcome.Phases.Select(item => item.Evaluation).ToArray(),
            "deferred north-bank preview/approved result equality");
    }

    private void CheckCommercialCampaignRewindAndReplay()
    {
        var recent = new CommercialCampaignRun(_campaign, _commercialWorld);
        string recentStart = CampaignStateJson(recent.GetSnapshot());
        BuildCampaignNode(
            recent,
            "SMALL_SUBSTATION",
            new MapPoint(2250, 700),
            "campaign recent rollback node");
        Check(recent.GetSnapshot().CanRollbackRecentProject,
            "campaign recent project rollback unavailable");
        Check(recent.UndoRecentConstruction(),
            "campaign recent project rollback rejected");
        Equal(recentStart, CampaignStateJson(recent.GetSnapshot()),
            "campaign recent project rollback state");
        Equal(0, recent.CommandCount,
            "campaign recent project rollback journal prefix");

        var window = new CommercialCampaignRun(_campaign, _commercialWorld);
        string windowStart = CampaignStateJson(window.GetSnapshot());
        BuildCampaignNode(
            window,
            "SMALL_SUBSTATION",
            new MapPoint(2250, 700),
            "campaign window restart node");
        Check(window.RestartDecisionWindow(),
            "campaign decision-window restart rejected");
        Equal(windowStart, CampaignStateJson(window.GetSnapshot()),
            "campaign decision-window restart state");

        var chapter = new CommercialCampaignRun(_campaign, _commercialWorld);
        CampaignRouteState routes = CompleteCampaignFirstLight(
            chapter,
            "campaign chapter restart setup");
        string secondHeartStart = CampaignStateJson(chapter.GetSnapshot());
        BuildCampaignNode(
            chapter,
            "SMALL_SUBSTATION",
            new MapPoint(2200, 1250),
            "campaign chapter restart node");
        Check(chapter.RestartChapter(), "campaign chapter restart rejected");
        Equal(secondHeartStart, CampaignStateJson(chapter.GetSnapshot()),
            "campaign chapter restart state");

        routes = CompleteCampaignSecondHeart(
            chapter,
            routes,
            secondCorridorSafe: false,
            "campaign previous rewind setup");
        CommercialCampaignCommandResult acceptedDraft = chapter.Execute(
            CommercialCoreCommand.SetNodeDraft(
                "LARGE_SUBSTATION",
                new MapPoint(2050, 400)));
        Check(acceptedDraft.Accepted,
            "campaign previous rewind current-chapter mutation rejected");
        Check(chapter.RewindToPreviousChapter(),
            "campaign previous-chapter rewind rejected");
        CommercialCampaignSnapshot rewound = chapter.GetSnapshot();
        Equal("SECOND_HEART", rewound.Chapter.ChapterId,
            "campaign previous-chapter rewind target");
        Equal(secondHeartStart, CampaignStateJson(rewound),
            "campaign previous-chapter rewind state");
        Equal(1, rewound.CompletedChapterOutcomes.Count,
            "campaign previous-chapter rewind outcome prefix");

        CommercialCampaignRun replayed = CommercialCampaignRun.Restore(
            _campaign,
            _commercialWorld,
            chapter.Commands);
        Equal(CampaignStateJson(chapter.GetSnapshot()), CampaignStateJson(replayed.GetSnapshot()),
            "campaign fresh replay state equality");
        SequenceEqual(chapter.Commands, replayed.Commands,
            "campaign fresh replay journal equality");
    }

    private void CheckCommercialCampaignWindowSafety()
    {
        CommercialCoreSeedDefinition fullSeed = new(
            "WINDOW_SAFETY_FULL_SEED",
            1020,
            8500000,
            _commercialWorld.Nodes.Select(item => item.NodeId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            _commercialWorld.Edges.Select(item => item.EdgeId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            Array.Empty<SpatialNodeDefinition>(),
            Array.Empty<SpatialEdgeDefinition>(),
            Array.Empty<string>());
        CommercialOperatingPhaseDefinition currentPhase =
            _campaign.Chapters[0].OperatingPhases[0];
        CommercialOperatingPhaseDefinition futurePhase = currentPhase with
        {
            PhaseId = "FUTURE_NORTH_SUPPLY",
            DisplayName = "다음 경계 북안 공급",
            Story = null,
            Loads =
            [
                new CommercialLoadBundleDefinition(
                    "NORTH_RESIDENTIAL",
                    500,
                    CommercialObligationKind.SafetyDuty),
            ],
            UnavailableNodeIds = Array.Empty<string>(),
        };
        CommercialCampaignChapterDefinition first = _campaign.Chapters[0] with
        {
            DecisionWindows =
            [
                new CommercialDecisionWindowDefinition(
                    "CURRENT_SUPPLY_WINDOW",
                    currentPhase.PhaseId,
                    null,
                    null),
                new CommercialDecisionWindowDefinition(
                    "FUTURE_NORTH_BUILD_WINDOW",
                    futurePhase.PhaseId,
                    null,
                    null),
            ],
            OperatingPhases = [currentPhase, futurePhase],
        };
        var definition = _campaign with
        {
            InitialSeed = fullSeed,
            Chapters = [first, .. _campaign.Chapters.Skip(1)],
        };
        CommercialCampaignLoader.Validate(definition, _commercialWorld);
        var run = new CommercialCampaignRun(definition, _commercialWorld);
        CommercialCampaignSnapshot firstWindow = run.GetSnapshot();
        Equal(800L, Supply(
                CampaignProjection(firstWindow, "FIRST_LIGHT_SUPPLY").Evaluation,
                "EAST_RESIDENTIAL").DeliveredKw,
            "window safety current delivery");
        Equal(0L, Supply(
                CampaignProjection(firstWindow, "FUTURE_NORTH_SUPPLY").Evaluation,
                "NORTH_RESIDENTIAL").DeliveredKw,
            "window safety future disconnected delivery");
        Check(firstWindow.FirstBlockingFailure is null && firstWindow.CanApprove,
            "future topology work incorrectly blocked the current window");
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            "window safety approve current phase");
        CommercialCampaignSnapshot futureWindow = run.GetSnapshot();
        Equal("FUTURE_NORTH_BUILD_WINDOW", futureWindow.CurrentWindow!.WindowId,
            "window safety second decision boundary");
        Check(!futureWindow.CanApprove &&
                futureWindow.FirstBlockingFailure?.Kind == ThermalFailureKind.NoTopologyPath,
            "window safety missing second-window construction requirement");
        BuildCampaignLine(
            run,
            "EAST_SUBSTATION",
            "STANDARD_LINE",
            "STANDARD_POLE",
            [new MapPoint(2550, 450)],
            "NORTH_RESIDENTIAL_TERMINAL",
            "window safety future north line");
        Check(run.GetSnapshot().CanApprove,
            "window safety future construction did not clear approval");
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            "window safety approve future phase");
        Equal("SECOND_HEART", run.GetSnapshot().Chapter.ChapterId,
            "window safety did not finish the two-window chapter");
        Equal(2, run.GetSnapshot().LastOutcome!.Phases.Count,
            "window safety committed phase count");
    }

    private void CheckCommercialCampaignCanonicalEightRun()
    {
        var run = new CommercialCampaignRun(_campaign, _commercialWorld);
        CampaignRouteState routes = CompleteCampaignFirstFour(
            run,
            "canonical eight-run");
        CompleteCampaignWhoseMargin(
            run,
            routes,
            reinforcedFactoryRoute: false,
            CommercialPromiseDecision.Keep,
            "canonical eight-run");
        CompleteCampaignBeforeWaterRise(
            run,
            routes,
            reinforcedHighlandRoute: false,
            CommercialPromiseDecision.Defer,
            "canonical eight-run");
        CompleteCampaignSwitchOffToProtect(
            run,
            routes,
            continuousSplit: false,
            "canonical eight-run");
        CompleteCampaignLongestNight(
            run,
            routes,
            useLargeRefuge: false,
            "canonical eight-run");

        CommercialCampaignSnapshot completed = run.GetSnapshot();
        Check(completed.CampaignComplete,
            "canonical eight-run did not complete the campaign");
        Equal(8, completed.CompletedChapterOutcomes.Count,
            "canonical eight-run outcome count");
        SequenceEqual(
            CommercialCampaignLoader.CanonicalChapterIds,
            completed.CompletedChapterOutcomes.Select(item => item.ChapterId).ToArray(),
            "canonical eight-run outcome order");
        Check(completed.Epilogue is not null,
            "canonical eight-run omitted the epilogue");
        Equal(8, completed.Epilogue!.ChapterFacts.Count,
            "canonical epilogue chapter-fact count");
        Equal(3, completed.Epilogue.PromiseFacts.Count,
            "canonical epilogue promise-fact count");
        SequenceEqual(
            [
                "NORTH_BANK_MOVE_IN_PROMISE",
                "FACTORY_NIGHT_SHIFT_PROMISE",
                "EAST_CONTINUITY_PROMISE",
            ],
            completed.Epilogue.PromiseFacts.Select(item => item.PromiseId).ToArray(),
            "canonical epilogue promise order");
        Check(completed.CompletedChapterOutcomes.All(outcome =>
                outcome.RenderedFacts.Count > 0 &&
                outcome.RenderedFacts.All(fact =>
                    !fact.Contains('{', StringComparison.Ordinal) &&
                    !fact.Contains('}', StringComparison.Ordinal))),
            "canonical eight-run retained unresolved fact tokens");
    }

    private CampaignRouteState CompleteCampaignFirstFour(
        CommercialCampaignRun run,
        string label)
    {
        CampaignRouteState routes = CompleteCampaignFirstLight(run, label);
        routes = CompleteCampaignSecondHeart(
            run,
            routes,
            secondCorridorSafe: false,
            label);
        routes = CompleteCampaignSecondSource(
            run,
            routes,
            reinforced: true,
            label);
        return CompleteCampaignNorthBank(
            run,
            routes,
            CommercialPromiseDecision.Keep,
            label);
    }

    private void CheckCommercialCampaignStageFArchetypesAndRecovery()
    {
        var firstFour = new CommercialCampaignRun(_campaign, _commercialWorld);
        CampaignRouteState routes = CompleteCampaignFirstFour(
            firstFour,
            "Stage-F archetype seed");
        CommercialCoreCommand[] chapterFiveStart = firstFour.Commands.ToArray();
        string chapterFiveState = CampaignStateJson(firstFour.GetSnapshot());
        CommercialApprovalChecklistItem promiseDecisionGate = firstFour.GetSnapshot()
            .ApprovalChecklist.Items.Single(item =>
                item.Kind == CommercialApprovalGateKind.PromiseDecision);
        Check(!promiseDecisionGate.Passed &&
                promiseDecisionGate.LoadId == "RIVER_FACTORY",
            "Stage-G promise-decision checklist gate was not persistent");

        var missingFactory = CommercialCampaignRun.Restore(
            _campaign,
            _commercialWorld,
            chapterFiveStart);
        CampaignAccepted(
            missingFactory,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Keep),
            "Stage-F missing factory: keep promise");
        CommercialApprovalChecklistItem promiseSupplyGate = missingFactory.GetSnapshot()
            .ApprovalChecklist.Items.Single(item =>
                item.Kind == CommercialApprovalGateKind.KeptPromiseDemand);
        Check(!promiseSupplyGate.Passed &&
                promiseSupplyGate.LoadId == "RIVER_FACTORY" &&
                promiseSupplyGate.FailureDiagnostic is not null,
            "Stage-G kept-promise supply checklist gate was not typed");
        Check(!missingFactory.GetSnapshot().CanApprove &&
                missingFactory.GetSnapshot().FirstBlockingFailure?.Kind ==
                    ThermalFailureKind.NoTopologyPath,
            "Stage-F missing factory was not diagnosed before approval");
        CampaignRejectedPreserves(
            missingFactory,
            CommercialCoreCommand.ApproveDecisionWindow(),
            CommercialCampaignRunError.KeptPromiseUnserved,
            null,
            "Stage-F missing factory rejection");
        Check(missingFactory.RestartChapter(),
            "Stage-F missing factory chapter restart rejected");
        Equal(chapterFiveState, CampaignStateJson(missingFactory.GetSnapshot()),
            "Stage-F missing factory chapter restart state");

        var cheapLineage = CommercialCampaignRun.Restore(
            _campaign,
            _commercialWorld,
            chapterFiveStart);
        var reinforcedFactory = CommercialCampaignRun.Restore(
            _campaign,
            _commercialWorld,
            chapterFiveStart);
        CompleteCampaignWhoseMargin(
            cheapLineage,
            routes,
            reinforcedFactoryRoute: false,
            CommercialPromiseDecision.Keep,
            "Stage-F short factory archetype");
        CompleteCampaignWhoseMargin(
            reinforcedFactory,
            routes,
            reinforcedFactoryRoute: true,
            CommercialPromiseDecision.Keep,
            "Stage-F reinforced factory archetype");
        CommercialCampaignChapterOutcome shortFactory = cheapLineage
            .GetSnapshot().LastOutcome!;
        CommercialCampaignChapterOutcome strongFactory = reinforcedFactory
            .GetSnapshot().LastOutcome!;
        Check(shortFactory.Facts.ThermalAssets.Any(asset =>
                asset.PhaseId == "NIGHT_SHIFT" &&
                asset.State == ThermalOperatingState.Emergency),
            "Stage-F short factory archetype omitted its emergency tradeoff");
        Check(!strongFactory.Facts.ThermalAssets.Any(asset =>
                asset.PhaseId == "NIGHT_SHIFT" &&
                asset.State == ThermalOperatingState.Emergency),
            "Stage-F reinforced factory archetype did not remain continuous");
        Check(shortFactory.EndingCashUnit > strongFactory.EndingCashUnit,
            "Stage-F short factory archetype did not preserve more cash");

        CommercialCoreCommand[] chapterSixCheapStart = cheapLineage.Commands.ToArray();
        var floodRecovery = CommercialCampaignRun.Restore(
            _campaign,
            _commercialWorld,
            chapterSixCheapStart);
        string floodChapterStart = CampaignStateJson(floodRecovery.GetSnapshot());
        CampaignAccepted(
            floodRecovery,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Defer),
            "Stage-F flood recovery: defer promise");
        var eastFailure = new CommercialCampaignConnectionFailure(
            "EAST_RESIDENTIAL_TERMINAL",
            1,
            2);
        Equal(eastFailure, floodRecovery.GetSnapshot().ConnectionFailures.Single(),
            "Stage-F flood recovery east gate detail");
        CommercialApprovalChecklistItem connectionGate = floodRecovery.GetSnapshot()
            .ApprovalChecklist.Items.Single(item =>
                item.Kind == CommercialApprovalGateKind.ConnectionRequirement);
        Check(!connectionGate.Passed &&
                connectionGate.NodeId == eastFailure.NodeId &&
                connectionGate.Current == eastFailure.CurrentConnections &&
                connectionGate.Required == eastFailure.RequiredConnections,
            "Stage-G connection checklist gate lost its typed target/counts");
        CampaignRejectedPreserves(
            floodRecovery,
            CommercialCoreCommand.ApproveDecisionWindow(),
            CommercialCampaignRunError.ConnectionRequirementUnmet,
            eastFailure,
            "Stage-F flood recovery connection rejection");
        string beforeRecentProject = CampaignStateJson(floodRecovery.GetSnapshot());
        int beforeRecentProjectCommands = floodRecovery.CommandCount;
        BuildCampaignLine(
            floodRecovery,
            "SOUTH_SOURCE_NODE",
            "REINFORCED_LINE",
            "REINFORCED_POLE",
            [
                new MapPoint(550, 1100),
                new MapPoint(990, 750),
                new MapPoint(1640, 750),
                new MapPoint(1950, 850),
            ],
            routes.HospitalHighSubstationId!,
            "Stage-F flood recovery recent project");
        CampaignAccepted(
            floodRecovery,
            CommercialCoreCommand.StartLineDraft(
                routes.NorthLargeSubstationId!,
                "REINFORCED_LINE",
                "REINFORCED_POLE"),
            "Stage-F flood recovery long line start");
        foreach (MapPoint point in new[]
                 {
                     new MapPoint(1900, 300),
                     new MapPoint(1850, 50),
                     new MapPoint(2200, 30),
                     new MapPoint(2280, 500),
                 })
        {
            LinePointPreview pointPreview = floodRecovery.PreviewLinePoint(point);
            Check(pointPreview.Accepted,
                $"Stage-F flood recovery long line point {point} preview: " +
                pointPreview.Error);
            CampaignAccepted(
                floodRecovery,
                CommercialCoreCommand.AddLinePoint(point),
                $"Stage-F flood recovery long line point {point}");
        }
        Check(floodRecovery.PreviewLineFinish("EAST_RESIDENTIAL_TERMINAL").Accepted,
            "Stage-F flood recovery long line finish preview");
        CampaignAccepted(
            floodRecovery,
            CommercialCoreCommand.FinishLineDraft("EAST_RESIDENTIAL_TERMINAL"),
            "Stage-F flood recovery long line finish");
        CommercialCampaignProjectQuote deadlineQuote =
            floodRecovery.PreviewLineOrder();
        Check(!deadlineQuote.Accepted &&
                deadlineQuote.Error == CommercialCampaignRunError.DeadlineExceeded &&
                deadlineQuote.BuildMinutes is > 0,
            "Stage-F flood recovery long line did not expose deadline rejection");
        CommercialConstructionWindowForecast deadlineForecast =
            floodRecovery.PreviewConstructionWindowForecast(
                new CommercialNextNodeProjectPlan(
                    "SMALL_SUBSTATION",
                    new MapPoint(2400, 900)));
        Equal(2, deadlineForecast.Steps.Count,
            "Stage-G deadline current-plus-next forecast count");
        Check(deadlineForecast.Steps[0].Kind == ConstructionKind.Line &&
                deadlineForecast.Steps[0].StepRole ==
                    CommercialConstructionForecastStepRole.CurrentDraft &&
                deadlineForecast.Steps[0].Error ==
                    CommercialCampaignRunError.DeadlineExceeded &&
                deadlineForecast.Steps[0].BuildMinutes is > 0 &&
                deadlineForecast.Steps[0].CompletionMinute is > 0,
            "Stage-G deadline current forecast lost its typed quote");
        Check(deadlineForecast.Steps[1].Kind == ConstructionKind.Node &&
                deadlineForecast.Steps[1].StepRole ==
                    CommercialConstructionForecastStepRole.ExplicitNextPlan &&
                deadlineForecast.Steps[1].Error ==
                    CommercialCampaignRunError.WrongState &&
                deadlineForecast.Steps[1].ConstructionError is null &&
                deadlineForecast.Steps[1].BuildMinutes is null &&
                deadlineForecast.Steps[1].CompletionMinute is null &&
                deadlineForecast.Steps[1].RemainingMinutesAfterCompletion is null,
            "Stage-G deadline next forecast reused or mislabeled the current quote");
        CampaignRejectedPreserves(
            floodRecovery,
            CommercialCoreCommand.OrderLine(),
            CommercialCampaignRunError.DeadlineExceeded,
            null,
            "Stage-F flood recovery deadline rejection");
        CampaignAccepted(
            floodRecovery,
            CommercialCoreCommand.CancelLineDraft(),
            "Stage-F flood recovery cancel rejected line");
        Check(floodRecovery.GetSnapshot().CanRollbackRecentProject &&
                floodRecovery.UndoRecentConstruction(),
            "Stage-F flood recovery recent rollback rejected");
        Equal(beforeRecentProject, CampaignStateJson(floodRecovery.GetSnapshot()),
            "Stage-F flood recovery recent rollback state");
        Equal(beforeRecentProjectCommands, floodRecovery.CommandCount,
            "Stage-F flood recovery recent rollback journal");
        Check(floodRecovery.RestartChapter(),
            "Stage-F flood recovery chapter restart rejected");
        Equal(floodChapterStart, CampaignStateJson(floodRecovery.GetSnapshot()),
            "Stage-F flood recovery chapter restart state");

        var keptFlood = CommercialCampaignRun.Restore(
            _campaign,
            _commercialWorld,
            chapterSixCheapStart);
        CommercialCampaignSnapshot standardFloodStart = cheapLineage.GetSnapshot();
        CommercialCampaignSnapshot reinforcedFloodStart = keptFlood.GetSnapshot();
        CompleteCampaignBeforeWaterRise(
            cheapLineage,
            routes,
            reinforcedHighlandRoute: false,
            CommercialPromiseDecision.Defer,
            "Stage-F deferred flood archetype");
        CompleteCampaignBeforeWaterRise(
            keptFlood,
            routes,
            reinforcedHighlandRoute: true,
            CommercialPromiseDecision.Keep,
            "Stage-F kept flood archetype");
        CommercialCampaignChapterOutcome standardFlood =
            cheapLineage.GetSnapshot().LastOutcome!;
        CommercialCampaignChapterOutcome reinforcedFlood =
            keptFlood.GetSnapshot().LastOutcome!;
        long standardFloodCost = checked(
            standardFloodStart.CashUnit - standardFlood.EndingCashUnit);
        long reinforcedFloodCost = checked(
            reinforcedFloodStart.CashUnit - reinforcedFlood.EndingCashUnit);
        long standardFloodMinutes = checked(
            standardFlood.EndingMinute - standardFloodStart.Minute);
        long reinforcedFloodMinutes = checked(
            reinforcedFlood.EndingMinute - reinforcedFloodStart.Minute);
        Check(standardFloodCost < reinforcedFloodCost,
            "Stage-F standard flood archetype did not cost less");
        Check(standardFloodMinutes < reinforcedFloodMinutes &&
                reinforcedFloodMinutes <= 300,
            "Stage-F reinforced flood archetype missed its bounded time tradeoff");
        Check(new[] { standardFlood, reinforcedFlood }.All(outcome =>
                outcome.Facts.Loads.Where(fact =>
                    fact.PhaseId == "FLOOD_ARRIVAL" &&
                    (fact.LoadId == "HOSPITAL" || fact.LoadId == "WATERWORKS"))
                    .All(fact => fact.DeliveredKw == fact.DemandKw)),
            "Stage-F flood archetype failed a hard safety duty");
        Equal(
            CommercialPromiseDecision.Defer,
            standardFlood.PromiseDecision,
            "Stage-F deferred flood decision outcome");
        Equal(
            CommercialPromiseDecision.Keep,
            reinforcedFlood.PromiseDecision,
            "Stage-F kept flood decision outcome");
        Equal(
            1200L,
            reinforcedFlood.Facts.Loads.Single(fact =>
                fact.PhaseId == "FLOOD_ARRIVAL" &&
                fact.LoadId == "EAST_RESIDENTIAL").DeliveredKw,
            "Stage-F kept flood promise delivery");

        CommercialCoreCommand[] chapterSixStrongStart =
            reinforcedFactory.Commands.ToArray();
        var continuousLineage = CommercialCampaignRun.Restore(
            _campaign,
            _commercialWorld,
            chapterSixStrongStart);
        CompleteCampaignBeforeWaterRise(
            continuousLineage,
            routes,
            reinforcedHighlandRoute: false,
            CommercialPromiseDecision.Defer,
            "Stage-F continuous lineage flood plan");

        var cheapOutage = CommercialCampaignRun.Restore(
            _campaign,
            _commercialWorld,
            cheapLineage.Commands);
        CommercialCampaignSnapshot outageStart = cheapOutage.GetSnapshot();
        var waterFailure = new CommercialCampaignConnectionFailure(
            "WATER_TERMINAL",
            1,
            2);
        Equal(waterFailure, outageStart.ConnectionFailures.Single(),
            "Stage-F planned-outage initial water gate");
        Check(!outageStart.CanApprove,
            "Stage-F planned outage auto-approved without work");
        CampaignRejectedPreserves(
            cheapOutage,
            CommercialCoreCommand.ApproveDecisionWindow(),
            CommercialCampaignRunError.ConnectionRequirementUnmet,
            waterFailure,
            "Stage-F planned-outage connection rejection");

        string? cheapRefuge = CompleteCampaignSwitchOffToProtect(
            cheapOutage,
            routes,
            continuousSplit: false,
            "Stage-F shared-emergency outage archetype");
        string? continuousRefuge = CompleteCampaignSwitchOffToProtect(
            continuousLineage,
            routes,
            continuousSplit: true,
            "Stage-F continuous-split outage archetype");
        Check(cheapRefuge is null && continuousRefuge is not null,
            "Stage-F outage archetype refuge identity");
        CommercialCampaignChapterOutcome sharedOutage =
            cheapOutage.GetSnapshot().LastOutcome!;
        CommercialCampaignChapterOutcome splitOutage =
            continuousLineage.GetSnapshot().LastOutcome!;
        Check(sharedOutage.Facts.ThermalAssets.Any(asset =>
                asset.PhaseId == "WEST_SOURCE_PLANNED_OUTAGE" &&
                asset.State == ThermalOperatingState.Emergency),
            "Stage-F shared outage omitted emergency operation");
        Check(!splitOutage.Facts.ThermalAssets.Any(asset =>
                asset.PhaseId == "WEST_SOURCE_PLANNED_OUTAGE" &&
                asset.State == ThermalOperatingState.Emergency),
            "Stage-F continuous split entered emergency operation");
        Check(sharedOutage.EndingCashUnit > splitOutage.EndingCashUnit,
            "Stage-F shared outage did not preserve more cash");
        Check(new[] { sharedOutage, splitOutage }.All(outcome =>
                outcome.Facts.Loads.Where(fact =>
                    fact.PhaseId == "WEST_SOURCE_RETURN_SERVICE" &&
                    (fact.LoadId == "HOSPITAL" || fact.LoadId == "WATERWORKS"))
                    .All(fact => fact.DeliveredKw == fact.DemandKw)),
            "Stage-F outage archetype failed return-service safety");

        CommercialCampaignSnapshot longestNightStart = cheapOutage.GetSnapshot();
        Check(!longestNightStart.CanApprove &&
                longestNightStart.FirstBlockingFailure is not null,
            "Stage-F longest night passed without final work");
        CommercialCampaignRun m8WitnessRun = BuildStageGM8DiagnosticWitness();
        CommercialCampaignSnapshot m8Witness = m8WitnessRun.GetSnapshot();
        CommercialSupplyDiagnostic m8Diagnostic =
            m8Witness.FirstBlockingDiagnostic ??
            throw new InvalidOperationException(
                "Stage-G M8 omitted its named blocking diagnostic.");
        Equal("HEATWAVE_PEAK", m8Diagnostic.PhaseId,
            "Stage-G M8 diagnostic phase");
        Equal("폭염 정점", m8Diagnostic.PhaseDisplayName,
            "Stage-G M8 diagnostic phase display name");
        Equal(2, m8Diagnostic.PhaseNumber,
            "Stage-G M8 diagnostic phase number");
        Equal(3, m8Diagnostic.PhaseCount,
            "Stage-G M8 diagnostic phase count");
        Equal("WATERWORKS", m8Diagnostic.LoadId,
            "Stage-G M8 diagnostic load");
        Equal("청류 정수장", m8Diagnostic.LoadDisplayName,
            "Stage-G M8 diagnostic load display name");
        Equal(CommercialObligationKind.SafetyDuty, m8Diagnostic.Obligation,
            "Stage-G M8 diagnostic obligation");
        Equal(ThermalFailureKind.EmergencyLimit, m8Diagnostic.FailureKind,
            "Stage-G M8 diagnostic failure kind");
        Equal("WEST_GENERATION", m8Diagnostic.AttemptedSourceId,
            "Stage-G M8 diagnostic attempted source");
        Equal("서부 발전소", m8Diagnostic.AttemptedSourceDisplayName,
            "Stage-G M8 diagnostic attempted source display name");
        Equal(ThermalAssetKind.Edge, m8Diagnostic.LimitingAssetKind,
            "Stage-G M8 diagnostic limiter kind");
        Equal("PLAYER_EDGE_22", m8Diagnostic.LimitingAssetId,
            "Stage-G M8 diagnostic limiter ID");
        Equal("일반 배전선 · 소형 배전 변전소 5–정수장 접속점",
            m8Diagnostic.LimitingAssetDisplayName,
            "Stage-G M8 diagnostic limiter display name");
        Equal(3000L, m8Diagnostic.RequiredKw,
            "Stage-G M8 diagnostic required load");
        Equal(2500L, m8Diagnostic.AvailableKw,
            "Stage-G M8 diagnostic available capacity");
        Equal(500L, m8Diagnostic.ShortfallKw,
            "Stage-G M8 exact shortfall");
        SequenceEqual(
            [
                "WEST_SOURCE_NODE",
                "PLAYER_POLE_1",
                "PLAYER_POLE_2",
                "PLAYER_POLE_3",
                "PLAYER_POLE_4",
                "PLAYER_SUBSTATION_5",
                "WATER_TERMINAL",
            ],
            m8Diagnostic.PathNodeIds,
            "Stage-G M8 diagnostic ordered node path");
        SequenceEqual(
            [
                "서부 발전 접속점",
                "일반 전신주 접속부 1",
                "일반 전신주 접속부 2",
                "일반 전신주 접속부 3",
                "일반 전신주 접속부 4",
                "소형 배전 변전소 5",
                "정수장 접속점",
            ],
            m8Diagnostic.PathNodeDisplayNames,
            "Stage-G M8 diagnostic ordered node display path");
        SequenceEqual(
            [
                "PLAYER_EDGE_1",
                "PLAYER_EDGE_2",
                "PLAYER_EDGE_3",
                "PLAYER_EDGE_4",
                "PLAYER_EDGE_23",
                "PLAYER_EDGE_22",
            ],
            m8Diagnostic.PathEdgeIds,
            "Stage-G M8 diagnostic ordered edge path");
        SequenceEqual(
            [
                "일반 배전선 · 서부 발전 접속점–일반 전신주 접속부 1",
                "일반 배전선 · 일반 전신주 접속부 1–일반 전신주 접속부 2",
                "일반 배전선 · 일반 전신주 접속부 2–일반 전신주 접속부 3",
                "일반 배전선 · 일반 전신주 접속부 3–일반 전신주 접속부 4",
                "일반 배전선 · 소형 배전 변전소 5–일반 전신주 접속부 4",
                "일반 배전선 · 소형 배전 변전소 5–정수장 접속점",
            ],
            m8Diagnostic.PathEdgeDisplayNames,
            "Stage-G M8 diagnostic ordered edge display path");
        Check(m8Diagnostic.LimitingAssetKind switch
            {
                ThermalAssetKind.Node => m8Diagnostic.PathNodeIds.Contains(
                    m8Diagnostic.LimitingAssetId!,
                    StringComparer.Ordinal),
                ThermalAssetKind.Edge => m8Diagnostic.PathEdgeIds.Contains(
                    m8Diagnostic.LimitingAssetId!,
                    StringComparer.Ordinal),
                _ => false,
            },
            "Stage-G M8 limiter is not an asset on its exact path");
        CommercialCampaignSnapshot repeatedM8Witness =
            BuildStageGM8DiagnosticWitness().GetSnapshot();
        CommercialPhaseProjection m8Projection = CampaignProjection(
            m8Witness,
            "HEATWAVE_PEAK");
        CommercialPhaseProjection repeatedM8Projection = CampaignProjection(
            repeatedM8Witness,
            "HEATWAVE_PEAK");
        Equal(m8Projection, repeatedM8Projection,
            "Stage-G M8 independent projection structural equality");
        Equal(m8Projection.GetHashCode(), repeatedM8Projection.GetHashCode(),
            "Stage-G M8 independent projection structural hash");
        Check(
            m8Projection.ProjectedWorld is not null &&
            repeatedM8Projection.ProjectedWorld is not null &&
            m8Projection.ProjectedWorld.Nodes.SequenceEqual(
                repeatedM8Projection.ProjectedWorld.Nodes) &&
            m8Projection.ProjectedWorld.Edges.SequenceEqual(
                repeatedM8Projection.ProjectedWorld.Edges),
            "Stage-G independent projection omitted structural projected geometry");
        Equal(m8Diagnostic, repeatedM8Witness.FirstBlockingDiagnostic,
            "Stage-G M8 independent diagnostic structural equality");
        CommercialPhaseProjection floodProjection = CampaignProjection(
            m8Witness,
            "PROTECTIVE_STOP_FLOOD");
        (IReadOnlyList<string> expectedUnavailableNodes,
            IReadOnlyList<string> expectedUnavailableEdges) =
            ExpectedEffectiveUnavailableAssets(
                floodProjection.Phase,
                m8Witness.Construction.World);
        SequenceEqual(
            expectedUnavailableNodes,
            floodProjection.EffectiveUnavailableNodeIds,
            "Stage-G effective unavailable node IDs");
        SequenceEqual(
            expectedUnavailableEdges,
            floodProjection.EffectiveUnavailableEdgeIds,
            "Stage-G effective unavailable edge IDs");
        Check(floodProjection.EffectiveUnavailableNodeIds.SequenceEqual(
                    floodProjection.EffectiveUnavailableNodeIds.OrderBy(
                        id => id,
                        StringComparer.Ordinal),
                    StringComparer.Ordinal) &&
                floodProjection.EffectiveUnavailableEdgeIds.SequenceEqual(
                    floodProjection.EffectiveUnavailableEdgeIds.OrderBy(
                        id => id,
                        StringComparer.Ordinal),
                    StringComparer.Ordinal) &&
                ((IList<string>)floodProjection.EffectiveUnavailableNodeIds).IsReadOnly &&
                ((IList<string>)floodProjection.EffectiveUnavailableEdgeIds).IsReadOnly,
            "Stage-G effective unavailable IDs were not sorted and frozen");
        var riskDraftPosition = new MapPoint(1900, 1800);
        CampaignAccepted(
            m8WitnessRun,
            CommercialCoreCommand.SetNodeDraft("SMALL_SUBSTATION", riskDraftPosition),
            "Stage-G risk-derived projected node draft");
        CommercialCampaignSnapshot riskDraftSnapshot = m8WitnessRun.GetSnapshot();
        CommercialPhaseProjection riskDraftFlood = CampaignProjection(
            riskDraftSnapshot,
            "PROTECTIVE_STOP_FLOOD");
        SpatialWorldDefinition riskDraftWorld = riskDraftFlood.ProjectedWorld ??
            throw new InvalidOperationException(
                "Stage-G risk draft projection omitted projected geometry.");
        SpatialNodeDefinition projectedRiskNode = riskDraftWorld.Nodes.Single(node =>
            node.Position == riskDraftPosition &&
            !riskDraftSnapshot.Construction.World.Nodes.Any(live =>
                live.NodeId == node.NodeId));
        Check(
            riskDraftFlood.EffectiveUnavailableNodeIds.Contains(
                projectedRiskNode.NodeId,
                StringComparer.Ordinal) &&
            riskDraftFlood.EffectiveUnavailableNodeIds.Count >
                riskDraftFlood.Phase.UnavailableNodeIds.Count,
            "Stage-G effective unavailable IDs omitted a risk-derived projected asset");
        CampaignRejectedPreserves(
            cheapOutage,
            CommercialCoreCommand.ApproveDecisionWindow(),
            CommercialCampaignRunError.SafetyDutyUnserved,
            null,
            "Stage-F longest-night missing-refuge rejection");
        var cheapFinal = CommercialCampaignRun.Restore(
            _campaign,
            _commercialWorld,
            cheapOutage.Commands);
        CompleteCampaignLongestNight(
            cheapFinal,
            routes,
            useLargeRefuge: false,
            "Stage-F delayed refuge archetype");
        CompleteCampaignLongestNight(
            continuousLineage,
            routes,
            useLargeRefuge: false,
            "Stage-F prepared refuge archetype",
            continuousRefuge);
        CommercialCampaignChapterOutcome delayedFinal =
            cheapFinal.GetSnapshot().LastOutcome!;
        CommercialCampaignChapterOutcome preparedFinal =
            continuousLineage.GetSnapshot().LastOutcome!;
        Check(delayedFinal.Facts.ThermalAssets.Any(asset =>
                asset.PhaseId == "PROTECTIVE_STOP_FLOOD" &&
                asset.State == ThermalOperatingState.Emergency),
            "Stage-F delayed refuge omitted final emergency operation");
        Check(!preparedFinal.Facts.ThermalAssets.Any(asset =>
                asset.PhaseId == "PROTECTIVE_STOP_FLOOD" &&
                asset.State == ThermalOperatingState.Emergency),
            "Stage-F prepared refuge did not remain continuous in the final phase");
        Check(delayedFinal.EndingCashUnit > preparedFinal.EndingCashUnit,
            "Stage-F delayed refuge did not preserve more cash");
        Check(cheapFinal.GetSnapshot().CampaignComplete &&
                continuousLineage.GetSnapshot().CampaignComplete,
            "Stage-F final archetypes did not complete the campaign");

        CommercialCampaignSnapshot keptSoftlock = keptFlood.GetSnapshot();
        Check(!keptSoftlock.CanApprove &&
                keptSoftlock.ThermalState.CoolingAssetIds.Count > 0,
            "Stage-F kept flood consequence did not carry into the next chapter");
        Check(keptFlood.RewindToPreviousChapter(),
            "Stage-F kept flood consequence could not rewind");
        CommercialCampaignSnapshot recoveredFloodStart = keptFlood.GetSnapshot();
        Equal("BEFORE_WATER_RISE", recoveredFloodStart.Chapter.ChapterId,
            "Stage-F kept flood rewind target");
        Equal(5, recoveredFloodStart.CompletedChapterOutcomes.Count,
            "Stage-F kept flood rewind outcome prefix");
        CompleteCampaignBeforeWaterRise(
            keptFlood,
            routes,
            reinforcedHighlandRoute: false,
            CommercialPromiseDecision.Defer,
            "Stage-F kept flood rewind recovery");
        _ = CompleteCampaignSwitchOffToProtect(
            keptFlood,
            routes,
            continuousSplit: false,
            "Stage-F kept flood recovered outage");
        Equal("LONGEST_NIGHT", keptFlood.GetSnapshot().Chapter.ChapterId,
            "Stage-F kept flood recovery remained softlocked");
    }

    private CommercialCampaignRun BuildStageGM8DiagnosticWitness()
    {
        var run = new CommercialCampaignRun(_campaign, _commercialWorld);
        string substation1 = BuildCampaignNode(
            run,
            "SMALL_SUBSTATION",
            new MapPoint(2282, 729),
            "Stage-G M8 observed substation 1");
        Equal("PLAYER_SUBSTATION_1", substation1,
            "Stage-G M8 observed substation 1 ID");
        BuildCampaignLine(
            run,
            "WEST_SOURCE_NODE",
            "STANDARD_LINE",
            "STANDARD_POLE",
            [
                new MapPoint(810, 548),
                new MapPoint(1015, 548),
                new MapPoint(1573, 548),
                new MapPoint(2000, 629),
            ],
            substation1,
            "Stage-G M8 observed west feed");
        BuildCampaignLine(
            run,
            substation1,
            "STANDARD_LINE",
            "STANDARD_POLE",
            Array.Empty<MapPoint>(),
            "EAST_RESIDENTIAL_TERMINAL",
            "Stage-G M8 observed east service");
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            "Stage-G M8 observed chapter 1");

        BuildCampaignLine(
            run,
            substation1,
            "STANDARD_LINE",
            "STANDARD_POLE",
            [new MapPoint(2343, 1032)],
            "HOSPITAL_TERMINAL",
            "Stage-G M8 observed hospital route 1");
        CampaignAccepted(
            run,
            CommercialCoreCommand.StartLineDraft(
                substation1,
                "STANDARD_LINE",
                "STANDARD_POLE"),
            "Stage-G M8 observed canceled hospital route start");
        CampaignAccepted(
            run,
            CommercialCoreCommand.AddLinePoint(new MapPoint(2120, 912)),
            "Stage-G M8 observed canceled hospital route point 1");
        CampaignAccepted(
            run,
            CommercialCoreCommand.AddLinePoint(new MapPoint(2120, 1234)),
            "Stage-G M8 observed canceled hospital route point 2");
        CampaignAccepted(
            run,
            CommercialCoreCommand.FinishLineDraft("HOSPITAL_TERMINAL"),
            "Stage-G M8 observed canceled hospital route finish");
        CampaignAccepted(
            run,
            CommercialCoreCommand.CancelLineDraft(),
            "Stage-G M8 observed canceled hospital route cancel");
        string substation2 = BuildCampaignNode(
            run,
            "SMALL_SUBSTATION",
            new MapPoint(2201, 1275),
            "Stage-G M8 observed substation 2");
        Equal("PLAYER_SUBSTATION_2", substation2,
            "Stage-G M8 observed substation 2 ID");
        BuildCampaignLine(
            run,
            substation1,
            "STANDARD_LINE",
            "STANDARD_POLE",
            Array.Empty<MapPoint>(),
            substation2,
            "Stage-G M8 observed substation tie");
        BuildCampaignLine(
            run,
            substation2,
            "STANDARD_LINE",
            "STANDARD_POLE",
            Array.Empty<MapPoint>(),
            "HOSPITAL_TERMINAL",
            "Stage-G M8 observed hospital route 2");
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            "Stage-G M8 observed chapter 2");

        BuildCampaignLine(
            run,
            "SOUTH_SOURCE_NODE",
            "STANDARD_LINE",
            "STANDARD_POLE",
            [
                new MapPoint(709, 1435),
                new MapPoint(769, 1032),
                new MapPoint(1051, 1032),
                new MapPoint(1637, 991),
                new MapPoint(1919, 951),
            ],
            substation2,
            "Stage-G M8 observed south feed");
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            "Stage-G M8 observed chapter 3");

        CampaignAccepted(
            run,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Keep),
            "Stage-G M8 observed north promise");
        string substation3 = BuildCampaignNode(
            run,
            "SMALL_SUBSTATION",
            new MapPoint(2527, 909),
            "Stage-G M8 observed substation 3");
        Equal("PLAYER_SUBSTATION_3", substation3,
            "Stage-G M8 observed substation 3 ID");
        BuildCampaignLine(
            run,
            substation2,
            "STANDARD_LINE",
            "STANDARD_POLE",
            Array.Empty<MapPoint>(),
            substation3,
            "Stage-G M8 observed east tie");
        BuildCampaignLine(
            run,
            substation3,
            "STANDARD_LINE",
            "STANDARD_POLE",
            Array.Empty<MapPoint>(),
            "WATER_TERMINAL",
            "Stage-G M8 observed water service");
        BuildCampaignLine(
            run,
            substation3,
            "STANDARD_LINE",
            "STANDARD_POLE",
            [new MapPoint(2527, 450)],
            "NORTH_RESIDENTIAL_TERMINAL",
            "Stage-G M8 observed north service");
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            "Stage-G M8 observed chapter 4");

        CampaignAccepted(
            run,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Keep),
            "Stage-G M8 observed factory promise");
        string substation4 = BuildCampaignNode(
            run,
            "SMALL_SUBSTATION",
            new MapPoint(2322, 1554),
            "Stage-G M8 observed substation 4");
        Equal("PLAYER_SUBSTATION_4", substation4,
            "Stage-G M8 observed substation 4 ID");
        CampaignAccepted(
            run,
            CommercialCoreCommand.StartLineDraft(
                "SOUTH_SOURCE_NODE",
                "REINFORCED_LINE",
                "REINFORCED_POLE"),
            "Stage-G M8 observed canceled highland route start");
        foreach (MapPoint point in new[]
                 {
                     new MapPoint(522, 1381),
                     new MapPoint(761, 905),
                     new MapPoint(1042, 883),
                     new MapPoint(1641, 883),
                     new MapPoint(1974, 731),
                     new MapPoint(2148, 1035),
                     new MapPoint(2061, 1425),
                 })
        {
            CampaignAccepted(
                run,
                CommercialCoreCommand.AddLinePoint(point),
                $"Stage-G M8 observed canceled highland route point {point}");
        }
        CampaignAccepted(
            run,
            CommercialCoreCommand.FinishLineDraft(substation4),
            "Stage-G M8 observed canceled highland route finish");
        CampaignAccepted(
            run,
            CommercialCoreCommand.CancelLineDraft(),
            "Stage-G M8 observed canceled highland route cancel");
        CampaignAccepted(
            run,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Defer),
            "Stage-G M8 observed deferred factory promise");
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            "Stage-G M8 observed chapter 5");
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            "Stage-G M8 observed chapter 6");

        CampaignAccepted(
            run,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Keep),
            "Stage-G M8 observed continuity promise");
        BuildCampaignLine(
            run,
            substation3,
            "STANDARD_LINE",
            "STANDARD_POLE",
            Array.Empty<MapPoint>(),
            "EAST_RESIDENTIAL_TERMINAL",
            "Stage-G M8 observed continuity service");
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            "Stage-G M8 observed chapter 7");

        string substation5 = BuildCampaignNode(
            run,
            "SMALL_SUBSTATION",
            new MapPoint(1919, 345),
            "Stage-G M8 observed substation 5");
        Equal("PLAYER_SUBSTATION_5", substation5,
            "Stage-G M8 observed substation 5 ID");
        BuildCampaignLine(
            run,
            substation5,
            "STANDARD_LINE",
            "STANDARD_POLE",
            Array.Empty<MapPoint>(),
            "WATER_TERMINAL",
            "Stage-G M8 observed water emergency service");
        BuildCampaignLine(
            run,
            substation5,
            "STANDARD_LINE",
            "STANDARD_POLE",
            Array.Empty<MapPoint>(),
            "PLAYER_POLE_4",
            "Stage-G M8 observed west tie");
        BuildCampaignLine(
            run,
            "SOUTH_SOURCE_NODE",
            "REINFORCED_LINE",
            "REINFORCED_POLE",
            [
                new MapPoint(528, 1354),
                new MapPoint(709, 891),
                new MapPoint(1032, 891),
                new MapPoint(951, 387),
                new MapPoint(951, 185),
                new MapPoint(1516, 185),
            ],
            substation5,
            "Stage-G M8 observed reinforced emergency feed");
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            "Stage-G M8 observed chapter 8 entry");
        Equal("LONGEST_NIGHT", run.CurrentChapterId,
            "Stage-G M8 observed witness chapter");
        Equal(111, run.CommandCount,
            "Stage-G M8 observed witness command count");
        return run;
    }

    private static (IReadOnlyList<string> Nodes, IReadOnlyList<string> Edges)
        ExpectedEffectiveUnavailableAssets(
            CommercialOperatingPhaseDefinition phase,
            SpatialWorldDefinition world)
    {
        var nodes = new SortedSet<string>(
            phase.UnavailableNodeIds,
            StringComparer.Ordinal);
        var edges = new SortedSet<string>(
            phase.UnavailableEdgeIds,
            StringComparer.Ordinal);
        Dictionary<string, SpatialRiskAreaDefinition> riskAreas = world.RiskAreas
            .ToDictionary(item => item.RiskAreaId, StringComparer.Ordinal);
        SpatialRiskAreaDefinition[] activeRiskAreas = phase.ActiveRiskAreaIds
            .Select(id => riskAreas[id])
            .ToArray();
        Dictionary<string, SpatialNodeClassDefinition> nodeClasses = world.NodeClasses
            .ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        Dictionary<string, SpatialNodeDefinition> worldNodes = world.Nodes
            .ToDictionary(item => item.NodeId, StringComparer.Ordinal);

        foreach (SpatialNodeDefinition node in world.Nodes)
        {
            if (activeRiskAreas.Any(area => FixedGeometry.CircleIntersectsPolygon(
                    node.Position,
                    nodeClasses[node.ClassId].FootprintRadiusUnit,
                    area.Polygon)))
            {
                nodes.Add(node.NodeId);
            }
        }
        foreach (SpatialEdgeDefinition edge in world.Edges)
        {
            if (activeRiskAreas.Any(area => FixedGeometry.SegmentIntersectsPolygon(
                    worldNodes[edge.FromNodeId].Position,
                    worldNodes[edge.ToNodeId].Position,
                    area.Polygon)))
            {
                edges.Add(edge.EdgeId);
            }
        }
        return (Array.AsReadOnly(nodes.ToArray()), Array.AsReadOnly(edges.ToArray()));
    }

    private void CheckCommercialCampaignCompletedSaveAndReplay()
    {
        var run = new CommercialCampaignRun(_campaign, _commercialWorld);
        CampaignRouteState routes = CompleteCampaignFirstFour(
            run,
            "completed save");
        CompleteCampaignWhoseMargin(
            run,
            routes,
            reinforcedFactoryRoute: false,
            CommercialPromiseDecision.Keep,
            "completed save");
        CompleteCampaignBeforeWaterRise(
            run,
            routes,
            reinforcedHighlandRoute: false,
            CommercialPromiseDecision.Defer,
            "completed save");
        CompleteCampaignSwitchOffToProtect(
            run,
            routes,
            continuousSplit: false,
            "completed save");
        CompleteCampaignLongestNight(
            run,
            routes,
            useLargeRefuge: false,
            "completed save");

        CommercialCampaignSnapshot completed = run.GetSnapshot();
        Check(completed.CampaignComplete && completed.Epilogue is not null,
            "completed save setup did not reach the epilogue");
        Equal(8, completed.ChapterReplayOptions.Count,
            "completed campaign replay-option count");
        SequenceEqual(
            CommercialCampaignLoader.CanonicalChapterIds,
            completed.ChapterReplayOptions.Select(option => option.ChapterId).ToArray(),
            "completed campaign replay-option order");
        Check(completed.ChapterReplayOptions.Zip(
                completed.ChapterReplayOptions.Skip(1),
                (left, right) => left.ChapterStartCommandCount <
                    right.ChapterStartCommandCount).All(value => value),
            "completed campaign replay checkpoints are not increasing");
        SequenceEqual(
            [
                CommercialPromiseDecision.Keep,
                CommercialPromiseDecision.Keep,
                CommercialPromiseDecision.Defer,
            ],
            completed.Epilogue!.PromiseFacts.Select(fact => fact.Decision).ToArray(),
            "completed campaign epilogue promise decisions");
        Check(completed.Epilogue.ChapterFacts.All(fact =>
                fact.SummaryLines.Count > 0 &&
                fact.SummaryLines.All(line =>
                    !line.Contains('{', StringComparison.Ordinal) &&
                    !line.Contains('}', StringComparison.Ordinal))),
            "completed campaign epilogue retained unresolved summary tokens");

        string campaignSha256 = LowerSha256(_campaignBytes);
        string worldSha256 = LowerSha256(_worldBytes);
        CommercialCampaignSave save = CommercialCampaignSaveCodec.Capture(
            _campaign,
            _commercialWorld,
            campaignSha256,
            worldSha256,
            run);
        CommercialCampaignRun restored = CommercialCampaignSaveCodec.Restore(
            _campaign,
            _commercialWorld,
            campaignSha256,
            worldSha256,
            CommercialCampaignSaveCodec.Deserialize(
                CommercialCampaignSaveCodec.Serialize(save)));
        Equal(CampaignStateJson(completed), CampaignStateJson(restored.GetSnapshot()),
            "completed campaign save fresh restore snapshot");
        SequenceEqual(run.Commands, restored.Commands,
            "completed campaign save fresh restore journal");

        string beforeInvalidReplay = CampaignStateJson(restored.GetSnapshot());
        int beforeInvalidReplayCommands = restored.CommandCount;
        Check(!restored.ReplayCompletedChapterStart("UNKNOWN_CHAPTER"),
            "completed campaign accepted an unknown replay option");
        Equal(beforeInvalidReplay, CampaignStateJson(restored.GetSnapshot()),
            "invalid completed replay changed state");
        Equal(beforeInvalidReplayCommands, restored.CommandCount,
            "invalid completed replay changed the journal");

        CommercialCampaignChapterReplayOption chapterFive = restored
            .GetSnapshot().ChapterReplayOptions.Single(option =>
                option.ChapterId == "WHOSE_MARGIN");
        Check(restored.ReplayCompletedChapterStart(chapterFive.ChapterId),
            "completed campaign rejected a canonical chapter replay");
        CommercialCampaignSnapshot replay = restored.GetSnapshot();
        Check(!replay.CampaignComplete && replay.Epilogue is null,
            "chapter replay retained completed presentation state");
        Equal("WHOSE_MARGIN", replay.Chapter.ChapterId,
            "chapter replay target identity");
        Equal(4, replay.CompletedChapterOutcomes.Count,
            "chapter replay outcome prefix");
        Equal(chapterFive.ChapterStartCommandCount, replay.CommandCount,
            "chapter replay journal checkpoint");
    }

    private void CheckStageGTypedUxAndSettingsV3()
    {
        var run = new CommercialCampaignRun(_campaign, _commercialWorld);
        CommercialCampaignSnapshot start = run.GetSnapshot();
        CommercialSupplyDiagnostic diagnostic = start.FirstBlockingDiagnostic ??
            throw new InvalidOperationException("Stage-G start omitted blocking diagnostic.");
        Equal("FIRST_LIGHT_SUPPLY", diagnostic.PhaseId,
            "Stage-G diagnostic phase ID");
        Equal("동부 첫 공급", diagnostic.PhaseDisplayName,
            "Stage-G diagnostic phase name");
        Equal(1, diagnostic.PhaseNumber, "Stage-G diagnostic phase number");
        Equal(start.Chapter.OperatingPhases.Count, diagnostic.PhaseCount,
            "Stage-G diagnostic phase count");
        Equal("EAST_RESIDENTIAL", diagnostic.LoadId,
            "Stage-G diagnostic load ID");
        Check(!string.IsNullOrWhiteSpace(diagnostic.LoadDisplayName),
            "Stage-G diagnostic omitted load display name");
        Equal(CommercialObligationKind.SafetyDuty, diagnostic.Obligation,
            "Stage-G diagnostic obligation");
        Equal(start.FirstBlockingFailure!.Kind, diagnostic.FailureKind,
            "Stage-G diagnostic failure kind");
        Equal(
            checked(diagnostic.RequiredKw - diagnostic.AvailableKw),
            diagnostic.ShortfallKw,
            "Stage-G diagnostic shortfall");
        Equal(diagnostic.PathNodeIds.Count, diagnostic.PathNodeDisplayNames.Count,
            "Stage-G diagnostic named path nodes");
        Equal(diagnostic.PathEdgeIds.Count, diagnostic.PathEdgeDisplayNames.Count,
            "Stage-G diagnostic named path edges");

        CommercialApprovalChecklist checklist = start.ApprovalChecklist;
        Equal(start.CanApprove, checklist.CanApprove,
            "Stage-G checklist approval authority");
        Equal(1, checklist.WindowNumber, "Stage-G checklist window number");
        Equal(start.Chapter.DecisionWindows.Count, checklist.WindowCount,
            "Stage-G checklist window count");
        Equal(1, checklist.FirstPhaseNumber, "Stage-G checklist first phase");
        Check(checklist.RemainingBlockerCount > 0 &&
                checklist.RemainingBlockerCount == checklist.Items.Count(item => !item.Passed),
            "Stage-G checklist blocker count");
        CommercialApprovalChecklistItem failedDemand = checklist.Items.Single(item =>
            item.Kind == CommercialApprovalGateKind.SafetyDemand && !item.Passed);
        Equal(diagnostic.LoadId, failedDemand.LoadId,
            "Stage-G checklist clickable failed load");
        Equal(diagnostic.FailureKind, failedDemand.FailureDiagnostic!.FailureKind,
            "Stage-G checklist shared failure authority");

        int expectedRows = start.Projections.Sum(item => item.Phase.Loads.Count);
        Equal(expectedRows, start.PhaseComparisonRows.Count,
            "Stage-G demand-by-phase row count");
        CommercialPhaseComparisonRow firstRow = start.PhaseComparisonRows.Single(item =>
            item.PhaseId == "FIRST_LIGHT_SUPPLY" &&
            item.LoadId == "EAST_RESIDENTIAL");
        Equal(CommercialPhaseComparisonApplicability.Evaluated, firstRow.Applicability,
            "Stage-G phase comparison applicability");
        Equal(diagnostic.LoadDisplayName, firstRow.LoadDisplayName,
            "Stage-G phase comparison load name");
        Equal(diagnostic.FailureKind, firstRow.FailureDiagnostic!.FailureKind,
            "Stage-G phase comparison failure authority");

        CommercialCampaignSnapshot repeatedStart = run.GetSnapshot();
        Equal(diagnostic, repeatedStart.FirstBlockingDiagnostic,
            "Stage-G repeated diagnostic structural equality");
        Equal(diagnostic.GetHashCode(), repeatedStart.FirstBlockingDiagnostic!.GetHashCode(),
            "Stage-G repeated diagnostic structural hash");
        Equal(checklist, repeatedStart.ApprovalChecklist,
            "Stage-G repeated checklist structural equality");
        Equal(checklist.GetHashCode(), repeatedStart.ApprovalChecklist.GetHashCode(),
            "Stage-G repeated checklist structural hash");
        SequenceEqual(start.PhaseComparisonRows, repeatedStart.PhaseComparisonRows,
            "Stage-G repeated phase-row structural equality");
        Equal(
            start.ConstructionWindowForecast,
            repeatedStart.ConstructionWindowForecast,
            "Stage-G repeated construction forecast structural equality");

        CampaignAccepted(
            run,
            CommercialCoreCommand.SetNodeDraft(
                "SMALL_SUBSTATION",
                new MapPoint(2250, 700)),
            "Stage-G construction forecast draft");
        CommercialConstructionWindowForecast currentForecast =
            run.PreviewConstructionWindowForecast();
        Equal("FIRST_LIGHT_BUILD_WINDOW", currentForecast.WindowId,
            "Stage-G construction forecast window");
        Equal(0L, currentForecast.AlreadySpentMinutes,
            "Stage-G construction forecast spent minutes");
        Equal(1, currentForecast.Steps.Count,
            "Stage-G current construction forecast count");
        Check(currentForecast.Steps[0].Accepted &&
                currentForecast.Steps[0].BuildMinutes is > 0,
            "Stage-G current construction forecast rejected");
        int commandCountBeforeSequence = run.CommandCount;
        string stateBeforeSequence = CampaignStateJson(run.GetSnapshot());
        CommercialConstructionWindowForecast sequence =
            run.PreviewConstructionWindowForecast(new CommercialNextNodeProjectPlan(
                "SMALL_SUBSTATION",
                new MapPoint(2200, 1250)));
        Equal(2, sequence.Steps.Count,
            "Stage-G current-plus-next construction forecast count");
        Check(sequence.Steps.All(item => item.Accepted),
            "Stage-G current-plus-next construction forecast rejection");
        Check(sequence.Steps[0].StepRole ==
                CommercialConstructionForecastStepRole.CurrentDraft &&
                sequence.Steps[1].StepRole ==
                CommercialConstructionForecastStepRole.ExplicitNextPlan,
            "Stage-G current-plus-next forecast roles");
        Check(sequence.Steps[1].CompletionMinute > sequence.Steps[0].CompletionMinute,
            "Stage-G construction forecast was not cumulative");
        Equal(commandCountBeforeSequence, run.CommandCount,
            "Stage-G construction forecast changed the journal");
        Equal(stateBeforeSequence, CampaignStateJson(run.GetSnapshot()),
            "Stage-G construction forecast changed the state");
        var linePlanA = new CommercialNextLineProjectPlan(
            "WEST_SOURCE_NODE",
            "STANDARD_LINE",
            "STANDARD_POLE",
            [new MapPoint(750, 650), new MapPoint(1050, 650)],
            "EAST_RESIDENTIAL_TERMINAL");
        var linePlanB = new CommercialNextLineProjectPlan(
            "WEST_SOURCE_NODE",
            "STANDARD_LINE",
            "STANDARD_POLE",
            [new MapPoint(750, 650), new MapPoint(1050, 650)],
            "EAST_RESIDENTIAL_TERMINAL");
        Equal(linePlanA, linePlanB,
            "Stage-G next-line plan structural equality");
        Equal(linePlanA.GetHashCode(), linePlanB.GetHashCode(),
            "Stage-G next-line plan structural hash");

        CampaignAccepted(run, CommercialCoreCommand.OrderNode(),
            "Stage-G recovery ordered node");
        CampaignAccepted(run, CommercialCoreCommand.AdvanceConstruction(),
            "Stage-G recovery completed node");
        CommercialRecoveryPreview recent = run.PreviewRecovery(
            CommercialRecoveryKind.RecentProject);
        Check(recent.Enabled && recent.TargetCommandCount == 0,
            "Stage-G recent recovery preview unavailable");
        Equal(ConstructionKind.Node, recent.RemovedProjectKind,
            "Stage-G recent recovery project kind");
        Equal(1, recent.RemovedNodeIds.Count,
            "Stage-G recent recovery removed node count");
        Equal(0, recent.RemovedEdgeIds.Count,
            "Stage-G recent recovery removed edge count");
        Equal(0, recent.RemovedRoutePointCount,
            "Stage-G recent recovery route-point count");
        Equal(1, recent.RemovedCompletedNodeProjectCount,
            "Stage-G recent recovery completed-node count");
        Equal(0, recent.RemovedCompletedLineProjectCount,
            "Stage-G recent recovery completed-line count");
        Equal(start.CashUnit, recent.RestoredCashUnit,
            "Stage-G recent recovery restored cash");
        Equal(start.Minute, recent.RestoredMinute,
            "Stage-G recent recovery restored minute");
        SequenceEqual(start.ThermalState.CoolingAssetIds, recent.RestoredCoolingAssetIds,
            "Stage-G recent recovery thermal state");

        string eastSubstationId = recent.RemovedNodeIds.Single();
        BuildCampaignLine(
            run,
            "WEST_SOURCE_NODE",
            "STANDARD_LINE",
            "STANDARD_POLE",
            [
                new MapPoint(750, 650),
                new MapPoint(1050, 650),
                new MapPoint(1600, 650),
                new MapPoint(2050, 650),
            ],
            eastSubstationId,
            "Stage-G mixed recovery completed line");
        CampaignAccepted(
            run,
            CommercialCoreCommand.StartLineDraft(
                eastSubstationId,
                "STANDARD_LINE",
                "STANDARD_POLE"),
            "Stage-G mixed recovery line draft start");
        CampaignAccepted(
            run,
            CommercialCoreCommand.AddLinePoint(new MapPoint(2400, 700)),
            "Stage-G mixed recovery line draft point");
        CommercialRecoveryPreview mixed = run.PreviewRecovery(
            CommercialRecoveryKind.Chapter);
        Check(mixed.Enabled && mixed.TargetCommandCount == 0,
            "Stage-G mixed chapter recovery preview unavailable");
        Equal(null, mixed.RemovedProjectKind,
            "Stage-G mixed recovery falsely claimed one project kind");
        Equal(1, mixed.RemovedCompletedNodeProjectCount,
            "Stage-G mixed recovery completed-node count");
        Equal(1, mixed.RemovedCompletedLineProjectCount,
            "Stage-G mixed recovery completed-line count");
        Equal(6, mixed.RemovedCompletedLineRoutePointCount,
            "Stage-G mixed recovery completed-line route points");
        Equal(ConstructionKind.Line, mixed.DiscardedDraftKind,
            "Stage-G mixed recovery discarded draft kind");
        Equal(2, mixed.DiscardedDraftRoutePointCount,
            "Stage-G mixed recovery discarded draft route points");
        Equal(null, mixed.DiscardedActiveConstructionKind,
            "Stage-G mixed recovery falsely claimed active construction");
        Equal(0, mixed.DiscardedActiveLineRoutePointCount,
            "Stage-G mixed recovery falsely claimed an active line route");
        Equal(5, mixed.RemovedNodeCount,
            "Stage-G mixed recovery exact removed node count");
        Equal(5, mixed.RemovedEdgeCount,
            "Stage-G mixed recovery exact removed edge count");
        Equal(8, mixed.RemovedRoutePointCount,
            "Stage-G mixed recovery aggregate route points");
        IReadOnlyList<CommercialRecoveryPreview> repeatedRecoveries =
            run.GetRecoveryPreviews();
        SequenceEqual(repeatedRecoveries, run.GetRecoveryPreviews(),
            "Stage-G repeated recovery structural equality");
        Check(ReferenceEquals(repeatedRecoveries, run.GetRecoveryPreviews()),
            "Stage-G recovery preview lazy cache was not reused");
        CommercialRecoveryPreview mixedCopy = mixed with
        {
            RemovedNodeIds = mixed.RemovedNodeIds.ToArray(),
            RemovedEdgeIds = mixed.RemovedEdgeIds.ToArray(),
            RestoredCoolingAssetIds = mixed.RestoredCoolingAssetIds.ToArray(),
        };
        Equal(mixed, mixedCopy,
            "Stage-G copied recovery structural equality");
        Equal(mixed.GetHashCode(), mixedCopy.GetHashCode(),
            "Stage-G copied recovery structural hash");

        CheckStageGRecoveryPhasePreviewsAndCache();
        CheckStageGForecastDependencyBlocking();
        CheckStageGFutureSafetyGate();
        CheckStageGCommandCapacity();

        CheckCommercialSettingsV3();
    }

    private void CheckStageGRecoveryPhasePreviewsAndCache()
    {
        var nodeDraftRun = new CommercialCampaignRun(_campaign, _commercialWorld);
        IReadOnlyList<CommercialRecoveryPreview> initialCache =
            nodeDraftRun.GetRecoveryPreviews();
        CampaignAccepted(
            nodeDraftRun,
            CommercialCoreCommand.SetNodeDraft(
                "SMALL_SUBSTATION",
                new MapPoint(2250, 700)),
            "Stage-G node-draft recovery setup");
        IReadOnlyList<CommercialRecoveryPreview> nodeDraftCache =
            nodeDraftRun.GetRecoveryPreviews();
        Check(!ReferenceEquals(initialCache, nodeDraftCache),
            "Stage-G recovery cache survived an accepted command");
        CommercialRecoveryPreview nodeDraft = nodeDraftCache.Single(item =>
            item.Kind == CommercialRecoveryKind.Chapter);
        Check(nodeDraft.Enabled &&
                nodeDraft.RemovedProjectKind == ConstructionKind.Node &&
                nodeDraft.DiscardedDraftKind == ConstructionKind.Node &&
                nodeDraft.DiscardedDraftRoutePointCount == 0 &&
                nodeDraft.DiscardedActiveConstructionKind is null &&
                nodeDraft.DiscardedActiveLineRoutePointCount == 0 &&
                nodeDraft.RemovedNodeCount == 0 &&
                nodeDraft.RemovedEdgeCount == 0,
            "Stage-G node-draft recovery consequence was not exact");
        Check(nodeDraftRun.RestartChapter(),
            "Stage-G node-draft recovery replay failed");
        IReadOnlyList<CommercialRecoveryPreview> replayedCache =
            nodeDraftRun.GetRecoveryPreviews();
        Check(!ReferenceEquals(nodeDraftCache, replayedCache) &&
                replayedCache.All(item => !item.Enabled),
            "Stage-G recovery cache survived replay or retained stale actions");

        var nodeBuildingRun = new CommercialCampaignRun(_campaign, _commercialWorld);
        CampaignAccepted(
            nodeBuildingRun,
            CommercialCoreCommand.SetNodeDraft(
                "SMALL_SUBSTATION",
                new MapPoint(2250, 700)),
            "Stage-G node-building recovery draft");
        CampaignAccepted(
            nodeBuildingRun,
            CommercialCoreCommand.OrderNode(),
            "Stage-G node-building recovery order");
        CommercialRecoveryPreview nodeBuilding = nodeBuildingRun.PreviewRecovery(
            CommercialRecoveryKind.Chapter);
        Check(nodeBuilding.RemovedProjectKind == ConstructionKind.Node &&
                nodeBuilding.DiscardedDraftKind is null &&
                nodeBuilding.DiscardedActiveConstructionKind == ConstructionKind.Node &&
                nodeBuilding.RemovedCompletedNodeProjectCount == 0 &&
                nodeBuilding.RemovedNodeCount == 1 &&
                nodeBuilding.RemovedEdgeCount == 0 &&
                nodeBuilding.DiscardedActiveLineRoutePointCount == 0 &&
                nodeBuilding.RemovedRoutePointCount == 0,
            "Stage-G node-building recovery consequence was not exact");

        var lineDraftRun = new CommercialCampaignRun(_campaign, _commercialWorld);
        CampaignAccepted(
            lineDraftRun,
            CommercialCoreCommand.StartLineDraft(
                "WEST_SOURCE_NODE",
                "STANDARD_LINE",
                "STANDARD_POLE"),
            "Stage-G line-draft recovery start");
        CampaignAccepted(
            lineDraftRun,
            CommercialCoreCommand.AddLinePoint(new MapPoint(750, 650)),
            "Stage-G line-draft recovery point");
        CommercialRecoveryPreview lineDraft = lineDraftRun.PreviewRecovery(
            CommercialRecoveryKind.Chapter);
        Check(lineDraft.RemovedProjectKind == ConstructionKind.Line &&
                lineDraft.DiscardedDraftKind == ConstructionKind.Line &&
                lineDraft.DiscardedDraftRoutePointCount == 2 &&
                lineDraft.DiscardedActiveConstructionKind is null &&
                lineDraft.DiscardedActiveLineRoutePointCount == 0 &&
                lineDraft.RemovedNodeCount == 0 &&
                lineDraft.RemovedEdgeCount == 0 &&
                lineDraft.RemovedRoutePointCount == 2,
            "Stage-G line-draft recovery consequence was not exact");

        var lineBuildingRun = new CommercialCampaignRun(_campaign, _commercialWorld);
        string substationId = BuildCampaignNode(
            lineBuildingRun,
            "SMALL_SUBSTATION",
            new MapPoint(2250, 700),
            "Stage-G line-building recovery substation");
        CampaignAccepted(
            lineBuildingRun,
            CommercialCoreCommand.StartLineDraft(
                "WEST_SOURCE_NODE",
                "STANDARD_LINE",
                "STANDARD_POLE"),
            "Stage-G endpoint-undo line start");
        foreach (MapPoint point in new[]
                 {
                     new MapPoint(750, 650),
                     new MapPoint(1050, 650),
                     new MapPoint(1600, 650),
                     new MapPoint(2050, 650),
                 })
        {
            CampaignAccepted(
                lineBuildingRun,
                CommercialCoreCommand.AddLinePoint(point),
                $"Stage-G endpoint-undo line point {point}");
        }
        CampaignAccepted(
            lineBuildingRun,
            CommercialCoreCommand.FinishLineDraft(substationId),
            "Stage-G endpoint-undo first finish");
        CampaignAccepted(
            lineBuildingRun,
            CommercialCoreCommand.UndoLinePoint(),
            "Stage-G endpoint-undo remove endpoint");
        LineDraftSnapshot undone = lineBuildingRun.GetSnapshot().Construction.LineDraft ??
            throw new InvalidOperationException(
                "Stage-G endpoint undo discarded the line draft.");
        Check(undone.EndNodeId is null && undone.IntermediatePoints.Count == 4,
            "Stage-G endpoint undo removed an intermediate route point");
        CampaignAccepted(
            lineBuildingRun,
            CommercialCoreCommand.FinishLineDraft(substationId),
            "Stage-G endpoint-undo second finish");
        CampaignAccepted(
            lineBuildingRun,
            CommercialCoreCommand.OrderLine(),
            "Stage-G line-building recovery order");
        CommercialRecoveryPreview lineBuilding = lineBuildingRun.PreviewRecovery(
            CommercialRecoveryKind.Chapter);
        Check(lineBuilding.RemovedProjectKind is null &&
                lineBuilding.RemovedCompletedNodeProjectCount == 1 &&
                lineBuilding.RemovedCompletedLineProjectCount == 0 &&
                lineBuilding.DiscardedActiveConstructionKind == ConstructionKind.Line &&
                lineBuilding.DiscardedActiveLineRoutePointCount == 6 &&
                lineBuilding.RemovedNodeCount == 5 &&
                lineBuilding.RemovedEdgeCount == 5 &&
                lineBuilding.RemovedRoutePointCount == 6,
            "Stage-G line-building recovery consequence was not exact");
        CampaignAccepted(
            lineBuildingRun,
            CommercialCoreCommand.AdvanceConstruction(),
            "Stage-G endpoint-undo line completion");
        CommercialRecoveryPreview completedLine = lineBuildingRun.PreviewRecovery(
            CommercialRecoveryKind.Chapter);
        Check(completedLine.RemovedCompletedNodeProjectCount == 1 &&
                completedLine.RemovedCompletedLineProjectCount == 1 &&
                completedLine.RemovedCompletedLineRoutePointCount == 6 &&
                completedLine.DiscardedActiveConstructionKind is null &&
                completedLine.DiscardedActiveLineRoutePointCount == 0 &&
                completedLine.RemovedRoutePointCount == 6,
            "Stage-G endpoint undo corrupted completed-line recovery counts");
    }

    private void CheckStageGForecastDependencyBlocking()
    {
        var run = new CommercialCampaignRun(_campaign, _commercialWorld);
        CampaignAccepted(
            run,
            CommercialCoreCommand.StartLineDraft(
                "WEST_SOURCE_NODE",
                "STANDARD_LINE",
                "STANDARD_POLE"),
            "Stage-G incomplete-line forecast start");
        int commandCount = run.CommandCount;
        string state = CampaignStateJson(run.GetSnapshot());
        CommercialConstructionWindowForecast forecast =
            run.PreviewConstructionWindowForecast(
                new CommercialNextNodeProjectPlan(
                    "SMALL_SUBSTATION",
                    new MapPoint(2250, 700)));
        Equal(2, forecast.Steps.Count,
            "Stage-G incomplete-line current-plus-next forecast count");
        CommercialConstructionForecastStep current = forecast.Steps[0];
        Check(current.SequenceNumber == 1 &&
                current.StepRole ==
                    CommercialConstructionForecastStepRole.CurrentDraft &&
                current.Kind == ConstructionKind.Line &&
                !current.Accepted &&
                current.Error == CommercialCampaignRunError.ConstructionRejected &&
                current.ConstructionError == ConstructionError.DraftIncomplete &&
                current.BuildMinutes is null &&
                current.CompletionMinute is null &&
                current.RemainingMinutesAfterCompletion is null,
            "Stage-G incomplete line was not the typed first forecast step");
        CommercialConstructionForecastStep next = forecast.Steps[1];
        Check(next.SequenceNumber == 2 &&
                next.StepRole ==
                    CommercialConstructionForecastStepRole.ExplicitNextPlan &&
                next.Kind == ConstructionKind.Node &&
                !next.Accepted &&
                next.Error == CommercialCampaignRunError.WrongState &&
                next.ConstructionError is null &&
                next.BuildMinutes is null &&
                next.CompletionMinute is null &&
                next.RemainingMinutesAfterCompletion is null,
            "Stage-G next project reused or mislabeled a rejected current quote");
        Equal(commandCount, run.CommandCount,
            "Stage-G incomplete-line forecast changed the journal");
        Equal(state, CampaignStateJson(run.GetSnapshot()),
            "Stage-G incomplete-line forecast changed state");
    }

    private void CheckStageGFutureSafetyGate()
    {
        CommercialCampaignChapterDefinition[] chapters = _campaign.Chapters.ToArray();
        for (int index = 0; index < 4; index++)
        {
            CommercialCampaignChapterDefinition chapter = chapters[index];
            CommercialOperatingPhaseDefinition originalPhase = chapter.OperatingPhases[0];
            CommercialLoadBundleDefinition[] loads = index == 3
                ?
                [
                    new CommercialLoadBundleDefinition(
                        "HOSPITAL",
                        1,
                        CommercialObligationKind.SafetyDuty),
                    new CommercialLoadBundleDefinition(
                        "NORTH_RESIDENTIAL",
                        1,
                        CommercialObligationKind.CityPromise),
                ]
                :
                [
                    new CommercialLoadBundleDefinition(
                        "HOSPITAL",
                        1,
                        CommercialObligationKind.SafetyDuty),
                ];
            CommercialOperatingPhaseDefinition phase = originalPhase with
            {
                ThermalPolicy = CommercialPhaseThermalPolicy.ContinuousOnly,
                Loads = loads,
                UnavailableNodeIds = Array.Empty<string>(),
                UnavailableEdgeIds = Array.Empty<string>(),
                ActiveRiskAreaIds = Array.Empty<string>(),
                ThermalLimitOverrides = Array.Empty<ThermalLimitOverride>(),
            };
            chapters[index] = chapter with
            {
                DecisionWindows =
                [
                    chapter.DecisionWindows[0] with
                    {
                        BeforePhaseId = phase.PhaseId,
                        BuildMinutesAvailable = null,
                    },
                ],
                OperatingPhases = [phase],
            };
        }

        CommercialOperatingPhaseDefinition coreHot = _coreSlice.Main.Chapter
            .OperatingPhases.Single(phase => phase.PhaseId == "HOT_EVENING");
        CommercialOperatingPhaseDefinition coreRecovery = _coreSlice.Main.Chapter
            .OperatingPhases.Single(phase => phase.PhaseId == "NIGHT_RECOVERY");
        CommercialOperatingPhaseDefinition hotBase = coreHot with
        {
            PhaseId = "HOT_BASE",
            DisplayName = "Stage-G future gate base",
            ThermalPolicy = CommercialPhaseThermalPolicy.ContinuousOnly,
            Story = null,
            Loads = coreHot.Loads.Where(load =>
                load.Obligation != CommercialObligationKind.CityPromise).ToArray(),
        };
        CommercialOperatingPhaseDefinition nightShift = coreHot with
        {
            PhaseId = "NIGHT_SHIFT",
            DisplayName = "Stage-G future gate emergency promise",
            ThermalPolicy = CommercialPhaseThermalPolicy.ContinuousOnly,
            Story = null,
        };
        CommercialOperatingPhaseDefinition lateNight = coreRecovery with
        {
            PhaseId = "LATE_NIGHT",
            DisplayName = "Stage-G future gate recovery",
            ThermalPolicy = CommercialPhaseThermalPolicy.ContinuousOnly,
            Story = null,
        };
        chapters[4] = chapters[4] with
        {
            OperatingPhases = [hotBase, nightShift, lateNight],
        };
        CommercialCampaignDefinition campaign = _campaign with
        {
            InitialSeed = _coreSlice.Main.Seed,
            Chapters = chapters,
        };
        CommercialCampaignLoader.Validate(campaign, _commercialWorld);

        var run = new CommercialCampaignRun(campaign, _commercialWorld);
        for (int chapterIndex = 0; chapterIndex < 3; chapterIndex++)
        {
            CampaignAccepted(
                run,
                CommercialCoreCommand.ApproveDecisionWindow(),
                $"Stage-G future-gate seed chapter {chapterIndex + 1}");
        }
        CampaignAccepted(
            run,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Defer),
            "Stage-G future-gate seed promise");
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            "Stage-G future-gate seed chapter 4");
        Equal("WHOSE_MARGIN", run.CurrentChapterId,
            "Stage-G future-gate chapter entry");
        CampaignAccepted(
            run,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Keep),
            "Stage-G future-gate keep promise");
        BuildCampaignLine(
            run,
            "SOUTH_SUBSTATION",
            "STANDARD_LINE",
            "STANDARD_POLE",
            [new MapPoint(2100, 1800)],
            "FACTORY_TERMINAL",
            "Stage-G future-gate unsafe shared line");

        CommercialCampaignSnapshot preview = run.GetSnapshot();
        Check(CampaignProjection(preview, "HOT_BASE").SafetySatisfied &&
                CampaignProjection(preview, "NIGHT_SHIFT").SafetySatisfied &&
                CampaignProjection(preview, "NIGHT_SHIFT").PromiseSatisfied &&
                !CampaignProjection(preview, "LATE_NIGHT").SafetySatisfied,
            "Stage-G future-gate witness did not isolate thermal carryover");
        CommercialApprovalChecklistItem failedGate = preview.ApprovalChecklist.Items
            .First(item =>
                item.Kind == CommercialApprovalGateKind.FutureSafety && !item.Passed);
        Check(!preview.CanApprove &&
                failedGate.PhaseId == "LATE_NIGHT" &&
                failedGate.Obligation == CommercialObligationKind.SafetyDuty &&
                failedGate.FailureDiagnostic is not null,
            "Stage-G failing future-safety checklist gate was not typed");
        int commandCount = run.CommandCount;
        string state = CampaignStateJson(preview);
        CommercialCampaignCommandResult rejected = run.Execute(
            CommercialCoreCommand.ApproveDecisionWindow());
        Check(!rejected.Accepted &&
                rejected.Error == CommercialCampaignRunError.FutureSafetyAtRisk,
            "Stage-G checklist/apply future-safety authority diverged");
        Equal(commandCount, run.CommandCount,
            "Stage-G future-safety rejection changed the journal");
        Equal(state, CampaignStateJson(rejected.Snapshot),
            "Stage-G future-safety rejection changed state");
    }

    private void CheckStageGCommandCapacity()
    {
        var seed = new CommercialCampaignRun(_campaign, _commercialWorld);
        _ = CompleteCampaignFirstFour(seed, "Stage-G command-capacity seed");
        var saturatedJournal = seed.Commands.ToList();
        while (saturatedJournal.Count < CommercialCampaignRun.MaximumAcceptedCommands)
        {
            saturatedJournal.Add(CommercialCoreCommand.SetPromiseDecision(
                CommercialPromiseDecision.Defer));
        }
        CommercialCampaignRun saturated = CommercialCampaignRun.Restore(
            _campaign,
            _commercialWorld,
            saturatedJournal);
        CommercialCampaignSnapshot saturatedSnapshot = saturated.GetSnapshot();
        CommercialApprovalChecklistItem capacityGate = saturatedSnapshot
            .ApprovalChecklist.Items.Single(item =>
                item.Kind == CommercialApprovalGateKind.CommandCapacity);
        Check(!saturatedSnapshot.CanApprove &&
                !capacityGate.Passed &&
                capacityGate.Current == 0 &&
                capacityGate.Shortfall == 1,
            "Stage-G saturated approval checklist omitted command capacity");
        CampaignRejectedPreserves(
            saturated,
            CommercialCoreCommand.ApproveDecisionWindow(),
            CommercialCampaignRunError.CommandLimit,
            null,
            "Stage-G saturated approval command limit");
        CommercialConstructionWindowForecast saturatedForecast =
            saturated.PreviewConstructionWindowForecast(
                new CommercialNextNodeProjectPlan(
                    "SMALL_SUBSTATION",
                    new MapPoint(2400, 900)));
        Equal(1, saturatedForecast.Steps.Count,
            "Stage-G saturated next-plan forecast count");
        Check(!saturatedForecast.Steps[0].Accepted &&
                saturatedForecast.Steps[0].StepRole ==
                    CommercialConstructionForecastStepRole.ExplicitNextPlan &&
                saturatedForecast.Steps[0].Error ==
                    CommercialCampaignRunError.CommandLimit,
            "Stage-G saturated forecast ignored draft/order/advance slots");

        CommercialCoreCommand[] nearLimitJournal = saturatedJournal
            .Take(CommercialCampaignRun.MaximumAcceptedCommands - 2)
            .Append(CommercialCoreCommand.SetNodeDraft(
                "SMALL_SUBSTATION",
                new MapPoint(2400, 900)))
            .ToArray();
        CommercialCampaignRun nearLimit = CommercialCampaignRun.Restore(
            _campaign,
            _commercialWorld,
            nearLimitJournal);
        Equal(
            CommercialCampaignRun.MaximumAcceptedCommands - 1,
            nearLimit.CommandCount,
            "Stage-G near-limit draft journal count");
        CommercialCampaignProjectQuote strandedQuote = nearLimit.PreviewNodeOrder();
        Check(!strandedQuote.Accepted &&
                strandedQuote.Error == CommercialCampaignRunError.CommandLimit,
            "Stage-G near-limit quote would strand an ordered project");
        CampaignRejectedPreserves(
            nearLimit,
            CommercialCoreCommand.OrderNode(),
            CommercialCampaignRunError.CommandLimit,
            null,
            "Stage-G near-limit order stranded-building prevention");
        Equal(ConstructionPhase.NodeDrafting, nearLimit.GetSnapshot().Construction.Phase,
            "Stage-G rejected near-limit order changed the draft");
        CommercialConstructionWindowForecast nearLimitForecast =
            nearLimit.PreviewConstructionWindowForecast(
                new CommercialNextLineProjectPlan(
                    "WEST_SOURCE_NODE",
                    "STANDARD_LINE",
                    "STANDARD_POLE",
                    Array.Empty<MapPoint>(),
                    "EAST_RESIDENTIAL_TERMINAL"));
        Equal(2, nearLimitForecast.Steps.Count,
            "Stage-G near-limit current-plus-next forecast count");
        Check(nearLimitForecast.Steps[0].Kind == ConstructionKind.Node &&
                nearLimitForecast.Steps[0].StepRole ==
                    CommercialConstructionForecastStepRole.CurrentDraft &&
                nearLimitForecast.Steps[0].Error ==
                    CommercialCampaignRunError.CommandLimit,
            "Stage-G near-limit current forecast lost command-limit authority");
        Check(nearLimitForecast.Steps[1].Kind == ConstructionKind.Line &&
                nearLimitForecast.Steps[1].StepRole ==
                    CommercialConstructionForecastStepRole.ExplicitNextPlan &&
                nearLimitForecast.Steps[1].Error ==
                    CommercialCampaignRunError.WrongState &&
                nearLimitForecast.Steps[1].ConstructionError is null &&
                nearLimitForecast.Steps[1].BuildMinutes is null &&
                nearLimitForecast.Steps[1].CompletionMinute is null &&
                nearLimitForecast.Steps[1].RemainingMinutesAfterCompletion is null,
            "Stage-G near-limit next forecast reused or mislabeled the current quote");
    }

    private void CheckCommercialSettingsV3()
    {
        CommercialSettings settings = new(
            CommercialSettings.SupportedSchemaVersion,
            true,
            125,
            0,
            50,
            100,
            true);
        byte[] bytes = CommercialSettingsCodec.Serialize(settings);
        Equal(settings, CommercialSettingsCodec.DeserializeV3(bytes),
            "commercial settings v3 round trip");
        SequenceEqual(bytes, CommercialSettingsCodec.Serialize(settings),
            "commercial settings v3 canonical bytes");
        Equal(CommercialSettingsDocumentKind.Version3,
            CommercialSettingsCodec.Decode(bytes).Kind,
            "commercial settings v3 document kind");

        byte[] version2 = Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":\"gridworks.settings.v2\",\"windowMode\":\"fullscreen\",\"uiScalePercent\":125,\"showControlHelp\":false,\"masterVolumePercent\":25,\"ambientVolumePercent\":50,\"sfxVolumePercent\":75}");
        CommercialSettingsDecodeResult imported = CommercialSettingsCodec.Decode(version2);
        Equal(CommercialSettingsDocumentKind.ImportedVersion2, imported.Kind,
            "commercial settings v2 import kind");
        Check(imported.Settings.Fullscreen &&
                imported.Settings.UiScalePercent == 125 &&
                imported.Settings.MasterVolumePercent == 25 &&
                imported.Settings.AmbientVolumePercent == 50 &&
                imported.Settings.SfxVolumePercent == 75 &&
                !imported.Settings.ReduceMotion,
            "commercial settings v2 fields were not preserved");
        Equal(imported.Settings, CommercialSettingsCodec.ImportV2(version2),
            "commercial settings explicit v2 import");

        foreach ((string label, byte[] invalid) in new[]
                 {
                     ("unknown v3 field", Encoding.UTF8.GetBytes(
                         "{\"schemaVersion\":\"gridworks.settings.v3\",\"fullscreen\":false,\"uiScalePercent\":100,\"masterVolumePercent\":100,\"ambientVolumePercent\":100,\"sfxVolumePercent\":100,\"reduceMotion\":false,\"extra\":true}")),
                     ("duplicate v3 field", Encoding.UTF8.GetBytes(
                         "{\"schemaVersion\":\"gridworks.settings.v3\",\"fullscreen\":false,\"fullscreen\":true,\"uiScalePercent\":100,\"masterVolumePercent\":100,\"ambientVolumePercent\":100,\"sfxVolumePercent\":100,\"reduceMotion\":false}")),
                     ("negative v3 volume", Encoding.UTF8.GetBytes(
                         "{\"schemaVersion\":\"gridworks.settings.v3\",\"fullscreen\":false,\"uiScalePercent\":100,\"masterVolumePercent\":-1,\"ambientVolumePercent\":100,\"sfxVolumePercent\":100,\"reduceMotion\":false}")),
                     ("unsupported v3 volume step", Encoding.UTF8.GetBytes(
                         "{\"schemaVersion\":\"gridworks.settings.v3\",\"fullscreen\":false,\"uiScalePercent\":100,\"masterVolumePercent\":100,\"ambientVolumePercent\":33,\"sfxVolumePercent\":100,\"reduceMotion\":false}")),
                     ("invalid v2 volume", Encoding.UTF8.GetBytes(
                         "{\"schemaVersion\":\"gridworks.settings.v2\",\"windowMode\":\"windowed\",\"uiScalePercent\":100,\"showControlHelp\":true,\"masterVolumePercent\":100,\"ambientVolumePercent\":33,\"sfxVolumePercent\":100}")),
                     ("unsupported v1", Encoding.UTF8.GetBytes(
                         "{\"schemaVersion\":\"gridworks.settings.v1\"}")),
                 })
        {
            ExpectThrows<CommercialSettingsValidationException>(
                () => _ = CommercialSettingsCodec.Decode(invalid),
                label);
        }

        string directory = Path.Combine(
            Path.GetTempPath(),
            $"gridworks-commercial-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, CommercialSettingsStore.SettingsFileName);
        try
        {
            Equal(CommercialSettingsLoadStatus.Missing,
                CommercialSettingsStore.Load(path).Status,
                "commercial settings missing status");
            CommercialSettingsWriteResult write = CommercialSettingsStore.Save(path, settings);
            Equal(CommercialSettingsWriteStatus.Saved, write.Status,
                "commercial settings atomic write status");
            Check(!File.Exists(path + ".tmp"),
                "commercial settings write left a temp file");
            Equal(settings, CommercialSettingsStore.Load(path).Settings,
                "commercial settings stored value");

            File.WriteAllBytes(path, version2);
            CommercialSettingsLoadResult migration = CommercialSettingsStore.Load(path);
            Equal(CommercialSettingsLoadStatus.MigratedFromVersion2, migration.Status,
                "commercial settings v2 migration status");
            Equal(imported.Settings, migration.Settings,
                "commercial settings v2 migration value");
            Equal(CommercialSettingsDocumentKind.Version3,
                CommercialSettingsCodec.Decode(File.ReadAllBytes(path)).Kind,
                "commercial settings migration did not write v3");

            File.WriteAllBytes(path, version2);
            Directory.CreateDirectory(path + ".tmp");
            CommercialSettingsLoadResult failedMigration =
                CommercialSettingsStore.Load(path);
            Equal(
                CommercialSettingsLoadStatus.MigrationWriteFailed,
                failedMigration.Status,
                "commercial settings migration write-failure status");
            Equal(
                CommercialSettingsLoadError.MigrationWriteFailed,
                failedMigration.Error,
                "commercial settings migration write-failure typed error");
            Equal(imported.Settings, failedMigration.Settings,
                "commercial settings failed migration lost imported settings");
            SequenceEqual(version2, File.ReadAllBytes(path),
                "commercial settings failed migration replaced legacy bytes");
            Directory.Delete(path + ".tmp");

            byte[] invalidV3 = Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":\"gridworks.settings.v3\",\"fullscreen\":false}");
            File.WriteAllBytes(path, invalidV3);
            CommercialSettingsLoadResult invalidLoad = CommercialSettingsStore.Load(path);
            Equal(CommercialSettingsLoadStatus.Invalid, invalidLoad.Status,
                "commercial settings invalid v3 load status");
            Equal(CommercialSettingsLoadError.InvalidDocument, invalidLoad.Error,
                "commercial settings invalid v3 typed error");
            SequenceEqual(invalidV3, File.ReadAllBytes(path),
                "commercial settings invalid v3 was overwritten");

            byte[] invalidV2 = Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":\"gridworks.settings.v2\",\"windowMode\":\"windowed\"}");
            File.WriteAllBytes(path, invalidV2);
            invalidLoad = CommercialSettingsStore.Load(path);
            Equal(CommercialSettingsLoadStatus.Invalid, invalidLoad.Status,
                "commercial settings invalid v2 load status");
            SequenceEqual(invalidV2, File.ReadAllBytes(path),
                "commercial settings invalid v2 was overwritten");

            byte[] committed = CommercialSettingsCodec.Serialize(settings);
            File.WriteAllBytes(path, committed);
            CommercialSettingsWriteResult invalidWrite = CommercialSettingsStore.Save(
                path,
                settings with { UiScalePercent = 150 });
            Equal(CommercialSettingsWriteStatus.Failed, invalidWrite.Status,
                "commercial settings invalid write status");
            Equal(CommercialSettingsWriteError.InvalidSettings, invalidWrite.Error,
                "commercial settings invalid write error");
            SequenceEqual(committed, File.ReadAllBytes(path),
                "commercial settings invalid write replaced committed bytes");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private void CompleteCampaignWhoseMargin(
        CommercialCampaignRun run,
        CampaignRouteState routes,
        bool reinforcedFactoryRoute,
        CommercialPromiseDecision decision,
        string label)
    {
        CommercialCampaignSnapshot start = run.GetSnapshot();
        Equal("WHOSE_MARGIN", start.Chapter.ChapterId,
            $"{label}: whose-margin chapter");
        CampaignAccepted(
            run,
            CommercialCoreCommand.SetPromiseDecision(decision),
            $"{label}: set factory promise");
        if (decision == CommercialPromiseDecision.Keep)
        {
            if (reinforcedFactoryRoute)
            {
                BuildCampaignLine(
                    run,
                    "SOUTH_SOURCE_NODE",
                    "REINFORCED_LINE",
                    "REINFORCED_POLE",
                    [
                        new MapPoint(700, 1750),
                        new MapPoint(1150, 1750),
                        new MapPoint(1760, 1750),
                    ],
                    routes.HospitalSecondSubstationId!,
                    $"{label}: separate reinforced factory feed");
            }
            BuildCampaignLine(
                run,
                routes.HospitalSecondSubstationId!,
                reinforcedFactoryRoute ? "REINFORCED_LINE" : "STANDARD_LINE",
                reinforcedFactoryRoute ? "REINFORCED_POLE" : "STANDARD_POLE",
                Array.Empty<MapPoint>(),
                "FACTORY_TERMINAL",
                $"{label}: factory service");
        }
        else
        {
            BuildCampaignLine(
                run,
                routes.HospitalSecondSubstationId!,
                "STANDARD_LINE",
                "STANDARD_POLE",
                Array.Empty<MapPoint>(),
                "FACTORY_TERMINAL",
                $"{label}: deferred factory-ready connection");
        }

        CommercialCampaignSnapshot planned = run.GetSnapshot();
        Check(run.CommandCount > start.ChapterStartCommandCount,
            $"{label}: whose-margin completed without work");
        CommercialApprovalChecklistItem[] futureSafetyGates = planned.ApprovalChecklist.Items
            .Where(item => item.Kind == CommercialApprovalGateKind.FutureSafety)
            .ToArray();
        Check(futureSafetyGates.Length >= 2 && futureSafetyGates.All(item =>
                item.Passed &&
                item.PhaseId == "LATE_NIGHT" &&
                item.Obligation == CommercialObligationKind.SafetyDuty),
            $"{label}: future thermal checklist gates were absent or inconsistent");
        ThermalIntervalEvaluation hotBase =
            CampaignProjection(planned, "HOT_BASE").Evaluation;
        ThermalIntervalEvaluation nightShift =
            CampaignProjection(planned, "NIGHT_SHIFT").Evaluation;
        Equal(900L, Supply(hotBase, "HOSPITAL").DeliveredKw,
            $"{label}: hot-base hospital supply");
        if (decision == CommercialPromiseDecision.Keep)
        {
            Equal(2700L, Supply(nightShift, "RIVER_FACTORY").DeliveredKw,
                $"{label}: night-shift factory supply");
            bool factoryEmergency = nightShift.Assets.Any(asset =>
                asset.State == ThermalOperatingState.Emergency &&
                Supply(nightShift, "RIVER_FACTORY").PathEdgeIds.Contains(
                    asset.AssetId,
                    StringComparer.Ordinal));
            Equal(!reinforcedFactoryRoute, factoryEmergency,
                $"{label}: factory route emergency character");
        }
        else
        {
            Check(!nightShift.Loads.Any(load => load.LoadId == "RIVER_FACTORY"),
                $"{label}: deferred factory entered dispatch");
        }
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            $"{label}: approve hot base and night shift");
        CommercialCampaignSnapshot recovery = run.GetSnapshot();
        Equal("LATE_NIGHT_RECOVERY_WINDOW", recovery.CurrentWindow!.WindowId,
            $"{label}: late-night recovery window");
        ThermalIntervalEvaluation lateNight =
            CampaignProjection(recovery, "LATE_NIGHT").Evaluation;
        Equal(900L, Supply(lateNight, "HOSPITAL").DeliveredKw,
            $"{label}: late-night hospital supply");
        Equal(900L, Supply(lateNight, "WATERWORKS").DeliveredKw,
            $"{label}: late-night water supply");
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            $"{label}: approve late-night recovery");
        Equal("BEFORE_WATER_RISE", run.GetSnapshot().Chapter.ChapterId,
            $"{label}: transition to flood chapter");
    }

    private void CompleteCampaignBeforeWaterRise(
        CommercialCampaignRun run,
        CampaignRouteState routes,
        bool reinforcedHighlandRoute,
        CommercialPromiseDecision decision,
        string label)
    {
        CommercialCampaignSnapshot start = run.GetSnapshot();
        Equal("BEFORE_WATER_RISE", start.Chapter.ChapterId,
            $"{label}: before-water-rise chapter");
        CampaignAccepted(
            run,
            CommercialCoreCommand.SetPromiseDecision(decision),
            $"{label}: set east-continuity promise");
        string lineClassId = reinforcedHighlandRoute
            ? "REINFORCED_LINE"
            : "STANDARD_LINE";
        string poleClassId = reinforcedHighlandRoute
            ? "REINFORCED_POLE"
            : "STANDARD_POLE";
        IReadOnlyList<MapPoint> highlandPoints = reinforcedHighlandRoute
            ? [
                new MapPoint(550, 1100),
                new MapPoint(990, 750),
                new MapPoint(1640, 750),
                new MapPoint(1950, 850),
            ]
            : [
                new MapPoint(450, 1200),
                new MapPoint(650, 750),
                new MapPoint(1040, 750),
                new MapPoint(1620, 750),
                new MapPoint(1900, 800),
            ];
        BuildCampaignLine(
            run,
            "SOUTH_SOURCE_NODE",
            lineClassId,
            poleClassId,
            highlandPoints,
            routes.HospitalHighSubstationId!,
            $"{label}: flood-safe parallel hospital route",
            quote => Equal(0, quote.RiskAreaIds.Count,
                $"{label}: highland route risk exposure"));
        BuildCampaignLine(
            run,
            routes.NorthLargeSubstationId!,
            reinforcedHighlandRoute ? "REINFORCED_LINE" : "STANDARD_LINE",
            reinforcedHighlandRoute ? "REINFORCED_POLE" : "STANDARD_POLE",
            [
                new MapPoint(1950, 500),
                new MapPoint(2500, 600),
            ],
            "EAST_RESIDENTIAL_TERMINAL",
            $"{label}: second east connection",
            quote => Equal(0, quote.RiskAreaIds.Count,
                $"{label}: east connection risk exposure"));
        CommercialCampaignSnapshot planned = run.GetSnapshot();
        Check(run.CommandCount > start.ChapterStartCommandCount,
            $"{label}: flood chapter completed without work");
        Equal(0, planned.ConnectionFailures.Count,
            $"{label}: east two-connection gate");
        ThermalIntervalEvaluation flood =
            CampaignProjection(planned, "FLOOD_ARRIVAL").Evaluation;
        Equal(900L, Supply(flood, "HOSPITAL").DeliveredKw,
            $"{label}: flood hospital supply");
        Equal(900L, Supply(flood, "WATERWORKS").DeliveredKw,
            $"{label}: flood water supply");
        if (decision == CommercialPromiseDecision.Keep)
        {
            Equal(1200L, Supply(flood, "EAST_RESIDENTIAL").DeliveredKw,
                $"{label}: flood east-continuity supply");
        }
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            $"{label}: approve flood chapter");
        Equal("SWITCH_OFF_TO_PROTECT", run.GetSnapshot().Chapter.ChapterId,
            $"{label}: transition to planned outage");
    }

    private string? CompleteCampaignSwitchOffToProtect(
        CommercialCampaignRun run,
        CampaignRouteState routes,
        bool continuousSplit,
        string label)
    {
        CommercialCampaignSnapshot start = run.GetSnapshot();
        Equal("SWITCH_OFF_TO_PROTECT", start.Chapter.ChapterId,
            $"{label}: switch-off chapter");
        Equal(
            new CommercialCampaignConnectionFailure("WATER_TERMINAL", 1, 2),
            start.ConnectionFailures.Single(),
            $"{label}: initial water connection gate");
        Check(!start.CanApprove,
            $"{label}: planned outage approved without construction");
        string? continuousSubstationId = null;
        if (continuousSplit)
        {
            continuousSubstationId = BuildCampaignNode(
                run,
                "SMALL_SUBSTATION",
                new MapPoint(2300, 900),
                $"{label}: continuous split substation");
            BuildCampaignLine(
                run,
                "SOUTH_SOURCE_NODE",
                "REINFORCED_LINE",
                "REINFORCED_POLE",
                [
                    new MapPoint(700, 1850),
                    new MapPoint(1150, 1900),
                    new MapPoint(1780, 1900),
                    new MapPoint(1900, 1750),
                    new MapPoint(1850, 1350),
                    new MapPoint(1800, 1150),
                ],
                continuousSubstationId,
                $"{label}: split reinforced water feed");
            BuildCampaignLine(
                run,
                continuousSubstationId,
                "REINFORCED_LINE",
                "REINFORCED_POLE",
                [
                    new MapPoint(2400, 650),
                    new MapPoint(2350, 450),
                ],
                "WATER_TERMINAL",
                $"{label}: split reinforced water service");
        }
        else
        {
            BuildCampaignLine(
                run,
                routes.HospitalSecondSubstationId!,
                "STANDARD_LINE",
                "STANDARD_POLE",
                [
                    new MapPoint(1900, 1250),
                    new MapPoint(1800, 1050),
                    new MapPoint(1800, 700),
                ],
                "WATER_TERMINAL",
                $"{label}: shared hospital-to-water tie");
        }
        CommercialCampaignSnapshot planned = run.GetSnapshot();
        Check(run.CommandCount > start.ChapterStartCommandCount,
            $"{label}: planned-outage chapter completed without work");
        Equal(0, planned.ConnectionFailures.Count,
            $"{label}: water two-connection gate");
        ThermalIntervalEvaluation outage =
            CampaignProjection(planned, "WEST_SOURCE_PLANNED_OUTAGE").Evaluation;
        ThermalIntervalEvaluation returned =
            CampaignProjection(planned, "WEST_SOURCE_RETURN_SERVICE").Evaluation;
        Equal(1800L, Supply(outage, "HOSPITAL").DeliveredKw,
            $"{label}: outage hospital supply");
        Equal(1400L, Supply(outage, "WATERWORKS").DeliveredKw,
            $"{label}: outage water supply");
        Equal(!continuousSplit,
            outage.Assets.Any(asset => asset.State == ThermalOperatingState.Emergency),
            $"{label}: planned-outage thermal character");
        Equal(900L, Supply(returned, "HOSPITAL").DeliveredKw,
            $"{label}: return-service hospital supply");
        Equal(900L, Supply(returned, "WATERWORKS").DeliveredKw,
            $"{label}: return-service water supply");
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            $"{label}: approve planned outage and return");
        Equal("LONGEST_NIGHT", run.GetSnapshot().Chapter.ChapterId,
            $"{label}: transition to longest night");
        return continuousSubstationId;
    }

    private void CompleteCampaignLongestNight(
        CommercialCampaignRun run,
        CampaignRouteState routes,
        bool useLargeRefuge,
        string label,
        string? existingRefugeId = null)
    {
        CommercialCampaignSnapshot start = run.GetSnapshot();
        Equal("LONGEST_NIGHT", start.Chapter.ChapterId,
            $"{label}: longest-night chapter");
        bool reinforcedRefuge = useLargeRefuge || existingRefugeId is not null;
        string refugeId = existingRefugeId ?? BuildCampaignNode(
            run,
            useLargeRefuge ? "LARGE_SUBSTATION" : "SMALL_SUBSTATION",
            new MapPoint(2400, 900),
            $"{label}: final refuge substation");
        BuildCampaignLine(
            run,
            "WEST_SOURCE_NODE",
            reinforcedRefuge ? "REINFORCED_LINE" : "STANDARD_LINE",
            reinforcedRefuge ? "REINFORCED_POLE" : "STANDARD_POLE",
            [
                new MapPoint(650, 450),
                new MapPoint(990, 400),
                new MapPoint(1570, 400),
                new MapPoint(1700, 850),
                new MapPoint(1950, 1000),
            ],
            refugeId,
            $"{label}: final flood-safe feed",
            quote => Equal(0, quote.RiskAreaIds.Count,
                $"{label}: final feed risk exposure"));
        BuildCampaignLine(
            run,
            refugeId,
            existingRefugeId is not null ? "REINFORCED_LINE" : "STANDARD_LINE",
            existingRefugeId is not null ? "REINFORCED_POLE" : "STANDARD_POLE",
            Array.Empty<MapPoint>(),
            "HOSPITAL_TERMINAL",
            $"{label}: final hospital service");
        if (existingRefugeId is null)
        {
            BuildCampaignLine(
                run,
                refugeId,
                "STANDARD_LINE",
                "STANDARD_POLE",
                [new MapPoint(2350, 450)],
                "WATER_TERMINAL",
                $"{label}: final water service");
        }
        if (useLargeRefuge)
        {
            BuildCampaignLine(
                run,
                refugeId,
                "STANDARD_LINE",
                "STANDARD_POLE",
                Array.Empty<MapPoint>(),
                "EAST_RESIDENTIAL_TERMINAL",
                $"{label}: large-refuge east service");
        }

        CommercialCampaignSnapshot planned = run.GetSnapshot();
        Check(run.CommandCount > start.ChapterStartCommandCount,
            $"{label}: longest night completed without work");
        ThermalIntervalEvaluation maximum =
            CampaignProjection(planned, "MAX_DEMAND").Evaluation;
        ThermalIntervalEvaluation heatwave =
            CampaignProjection(planned, "HEATWAVE_PEAK").Evaluation;
        ThermalIntervalEvaluation final =
            CampaignProjection(planned, "PROTECTIVE_STOP_FLOOD").Evaluation;
        Equal(900L, Supply(maximum, "HOSPITAL").DeliveredKw,
            $"{label}: maximum-demand hospital supply");
        Check(heatwave.Assets.Any(asset => asset.State == ThermalOperatingState.Emergency),
            $"{label}: heatwave omitted emergency operation");
        Equal(900L, Supply(final, "HOSPITAL").DeliveredKw,
            $"{label}: final hospital supply");
        Equal(900L, Supply(final, "WATERWORKS").DeliveredKw,
            $"{label}: final water supply");
        ThermalIntervalEvaluation[] preview =
            planned.Projections.Select(item => item.Evaluation).ToArray();
        CampaignAccepted(
            run,
            CommercialCoreCommand.ApproveDecisionWindow(),
            $"{label}: approve longest night");
        CommercialCampaignSnapshot completed = run.GetSnapshot();
        Check(completed.CampaignComplete,
            $"{label}: longest-night approval did not complete campaign");
        SequenceEqual(
            preview,
            completed.LastOutcome!.Phases.Select(item => item.Evaluation).ToArray(),
            $"{label}: longest-night preview/commit equality");
    }

    private void CheckCommercialCampaignSaveV3()
    {
        var run = new CommercialCampaignRun(_campaign, _commercialWorld);
        _ = CompleteCampaignFirstLight(run, "campaign save setup");
        string campaignSha256 = LowerSha256(_campaignBytes);
        string worldSha256 = LowerSha256(_worldBytes);
        CommercialCampaignSave save = CommercialCampaignSaveCodec.Capture(
            _campaign,
            _commercialWorld,
            campaignSha256,
            worldSha256,
            run);
        Equal(CommercialCampaignSave.SupportedSchemaVersion, save.SchemaVersion,
            "campaign save schema");
        Equal(_campaign.CampaignId, save.CampaignId,
            "campaign save campaign identity");
        Equal(campaignSha256, save.CampaignSha256,
            "campaign save campaign hash");
        Equal(_commercialWorld.WorldId, save.WorldId,
            "campaign save world identity");
        Equal(worldSha256, save.WorldSha256,
            "campaign save world hash");
        SequenceEqual(run.Commands, save.Commands,
            "campaign save command journal");

        byte[] firstBytes = CommercialCampaignSaveCodec.Serialize(save);
        byte[] secondBytes = CommercialCampaignSaveCodec.Serialize(save);
        void ExpectCampaignSaveRejected(string label, Action<JsonObject> mutate)
        {
            JsonObject candidate = JsonNode.Parse(firstBytes)!.AsObject();
            mutate(candidate);
            ExpectThrows<CommercialCampaignPersistenceException>(
                () => CommercialCampaignSaveCodec.Deserialize(candidate.ToJsonString()),
                label);
        }
        Check(firstBytes.SequenceEqual(secondBytes),
            "campaign save serialization is not deterministic");
        Equal(save, CommercialCampaignSaveCodec.Deserialize(firstBytes),
            "campaign save byte round trip");
        Equal(
            save,
            CommercialCampaignSaveCodec.Deserialize(Encoding.UTF8.GetString(firstBytes)),
            "campaign save text round trip");
        using (JsonDocument document = JsonDocument.Parse(firstBytes))
        {
            HashSet<string> fields = document.RootElement.EnumerateObject()
                .Select(item => item.Name)
                .ToHashSet(StringComparer.Ordinal);
            Check(fields.SetEquals(
                [
                    "schemaVersion",
                    "campaignId",
                    "campaignSha256",
                    "worldId",
                    "worldSha256",
                    "commands",
                ]),
                "campaign save root is not the exact six-field shape");
        }

        CommercialCampaignRun restored = CommercialCampaignSaveCodec.Restore(
            _campaign,
            _commercialWorld,
            campaignSha256,
            worldSha256,
            save);
        Equal(CampaignStateJson(run.GetSnapshot()), CampaignStateJson(restored.GetSnapshot()),
            "campaign save fresh restore snapshot");
        SequenceEqual(run.Commands, restored.Commands,
            "campaign save fresh restore journal");
        ExpectThrows<CommercialCampaignPersistenceException>(
            () => CommercialCampaignSaveCodec.Restore(
                _campaign,
                _commercialWorld,
                new string('0', 64),
                worldSha256,
                save),
            "campaign save hash mismatch");
        ExpectThrows<CommercialCampaignPersistenceException>(
            () => CommercialCampaignSaveCodec.Restore(
                _campaign,
                _commercialWorld,
                campaignSha256,
                worldSha256,
                save with { CampaignId = "OTHER_CAMPAIGN" }),
            "campaign save campaign identity mismatch");
        ExpectThrows<CommercialCampaignPersistenceException>(
            () => CommercialCampaignSaveCodec.Restore(
                _campaign,
                _commercialWorld,
                campaignSha256,
                worldSha256,
                save with { WorldId = "OTHER_WORLD" }),
            "campaign save world identity mismatch");

        string firstText = Encoding.UTF8.GetString(firstBytes);
        ExpectThrows<CommercialCampaignPersistenceException>(
            () =>
            {
                string trimmed = firstText.TrimStart();
                _ = CommercialCampaignSaveCodec.Deserialize(
                    $"{{\"schemaVersion\":\"duplicate\",{trimmed[1..]}");
            },
            "campaign save duplicate root property");
        ExpectCampaignSaveRejected(
            "campaign save unknown root field",
            root => root["unexpected"] = true);
        ExpectCampaignSaveRejected(
            "campaign save missing root field",
            root => root.Remove("campaignId"));
        ExpectCampaignSaveRejected(
            "campaign save null command journal",
            root => root["commands"] = null);
        ExpectCampaignSaveRejected(
            "campaign save unknown command kind",
            root => Object(JsonArrayProperty(root, "commands")[0]!)["kind"] =
                "futureCommand");
        ExpectCampaignSaveRejected(
            "campaign save extra command field",
            root => Object(JsonArrayProperty(root, "commands")[0]!)["unexpected"] = true);
        ExpectThrows<CommercialCampaignPersistenceException>(
            () =>
            {
                JsonObject candidate = JsonNode.Parse(firstBytes)!.AsObject();
                Object(JsonArrayProperty(candidate, "commands")[0]!)["firstId"] =
                    "UNKNOWN_NODE_CLASS";
                CommercialCampaignSave invalidReplay =
                    CommercialCampaignSaveCodec.Deserialize(candidate.ToJsonString());
                _ = CommercialCampaignSaveCodec.Restore(
                    _campaign,
                    _commercialWorld,
                    campaignSha256,
                    worldSha256,
                    invalidReplay);
            },
            "campaign save invalid replay command");

        string coreSliceSha256 = LowerSha256(_coreSliceBytes);
        var stageDRun = new CommercialCoreSliceRun(_coreSlice, _commercialWorld);
        CommercialCoreSave stageDSave = CommercialCoreSaveCodec.Capture(
            _coreSlice,
            _commercialWorld,
            coreSliceSha256,
            worldSha256,
            stageDRun);
        byte[] stageDBytes = CommercialCoreSaveCodec.Serialize(stageDSave);
        ExpectThrows<CommercialCampaignPersistenceException>(
            () => CommercialCampaignSaveCodec.Deserialize(stageDBytes),
            "Stage-D save decoded as final campaign save");

        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"gridworks-campaign-save-check-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            string savePath = Path.Combine(
                temporaryDirectory,
                "release-campaign-save-v3.json");
            Equal(
                CommercialCampaignSaveLoadStatus.Missing,
                CommercialCampaignSaveStore.Load(savePath).Status,
                "campaign store missing status");

            File.WriteAllBytes(savePath, stageDBytes);
            Equal(
                CommercialCampaignSaveLoadStatus.RecognizedStageD,
                CommercialCampaignSaveStore.Load(savePath).Status,
                "campaign store Stage-D recognition");
            string digest12 = LowerSha256(stageDBytes)[..12];
            string expectedBackupPath = Path.Combine(
                temporaryDirectory,
                $"release-campaign-save-v3.stage-d.{digest12}.bak.json");
            CommercialCampaignSaveWriteResult migrated =
                CommercialCampaignSaveStore.SaveWithStageDBackup(savePath, save);
            Equal(
                CommercialCampaignSaveWriteStatus.SavedAfterStageDBackup,
                migrated.Status,
                "campaign Stage-D migration status");
            Equal(expectedBackupPath, migrated.StageDBackupPath,
                "campaign deterministic Stage-D backup path");
            Check(File.ReadAllBytes(expectedBackupPath).SequenceEqual(stageDBytes),
                "campaign Stage-D backup bytes");
            Equal(
                CommercialCampaignSaveLoadStatus.Loaded,
                CommercialCampaignSaveStore.Load(savePath).Status,
                "campaign migrated active save status");

            File.WriteAllBytes(savePath, stageDBytes);
            CommercialCampaignSaveWriteResult repeated =
                CommercialCampaignSaveStore.SaveWithStageDBackup(savePath, save);
            Equal(
                CommercialCampaignSaveWriteStatus.SavedAfterStageDBackup,
                repeated.Status,
                "campaign idempotent Stage-D preservation status");
            Equal(expectedBackupPath, repeated.StageDBackupPath,
                "campaign idempotent Stage-D backup path");
            Check(File.ReadAllBytes(expectedBackupPath).SequenceEqual(stageDBytes),
                "campaign idempotent Stage-D backup bytes");

            string conflictDirectory = Path.Combine(temporaryDirectory, "conflict");
            Directory.CreateDirectory(conflictDirectory);
            string conflictPath = Path.Combine(conflictDirectory, "save.json");
            File.WriteAllBytes(conflictPath, stageDBytes);
            string conflictBackup = Path.Combine(
                conflictDirectory,
                $"save.stage-d.{digest12}.bak.json");
            File.WriteAllText(conflictBackup, "different preserved bytes");
            CommercialCampaignSaveWriteResult conflict =
                CommercialCampaignSaveStore.SaveWithStageDBackup(conflictPath, save);
            Equal(CommercialCampaignSaveWriteStatus.Failed, conflict.Status,
                "campaign Stage-D backup conflict status");
            Equal(CommercialCampaignSaveWriteError.StageDBackupConflict, conflict.Error,
                "campaign Stage-D backup conflict error");
            Check(File.ReadAllBytes(conflictPath).SequenceEqual(stageDBytes),
                "campaign backup conflict overwrote active Stage-D save");

            string blockedDirectory = Path.Combine(temporaryDirectory, "blocked");
            Directory.CreateDirectory(blockedDirectory);
            string blockedPath = Path.Combine(blockedDirectory, "save.json");
            File.WriteAllBytes(blockedPath, stageDBytes);
            string blockedBackup = Path.Combine(
                blockedDirectory,
                $"save.stage-d.{digest12}.bak.json");
            Directory.CreateDirectory(blockedBackup);
            CommercialCampaignSaveWriteResult blocked =
                CommercialCampaignSaveStore.SaveWithStageDBackup(blockedPath, save);
            Equal(CommercialCampaignSaveWriteStatus.Failed, blocked.Status,
                "campaign Stage-D preservation failure status");
            Equal(CommercialCampaignSaveWriteError.StageDBackupFailed, blocked.Error,
                "campaign Stage-D preservation failure error");
            Check(File.ReadAllBytes(blockedPath).SequenceEqual(stageDBytes),
                "campaign preservation failure overwrote active Stage-D save");

            string malformedPath = Path.Combine(temporaryDirectory, "malformed.json");
            byte[] malformedStageD = Encoding.UTF8.GetBytes(
                $"{{\"schemaVersion\":\"{CommercialCoreSave.SupportedSchemaVersion}\"}}");
            File.WriteAllBytes(malformedPath, malformedStageD);
            Equal(
                CommercialCampaignSaveLoadStatus.Invalid,
                CommercialCampaignSaveStore.Load(malformedPath).Status,
                "campaign malformed Stage-D status");
            CommercialCampaignSaveWriteResult malformed =
                CommercialCampaignSaveStore.SaveWithStageDBackup(malformedPath, save);
            Equal(CommercialCampaignSaveWriteStatus.Failed, malformed.Status,
                "campaign malformed Stage-D write status");
            Equal(CommercialCampaignSaveWriteError.InvalidExistingSave, malformed.Error,
                "campaign malformed Stage-D write error");
            Check(File.ReadAllBytes(malformedPath).SequenceEqual(malformedStageD),
                "campaign malformed Stage-D save was overwritten");

            string freshPath = Path.Combine(temporaryDirectory, "fresh.json");
            File.WriteAllText(freshPath + ".tmp", "stale campaign temporary save");
            CommercialCampaignSaveWriteResult fresh =
                CommercialCampaignSaveStore.SaveWithStageDBackup(freshPath, save);
            Equal(CommercialCampaignSaveWriteStatus.Saved, fresh.Status,
                "campaign fresh save status");
            Check(!File.Exists(freshPath + ".tmp"),
                "campaign stale temporary file survived atomic save");
            Equal(save, CommercialCampaignSaveStore.Load(freshPath).Save,
                "campaign fresh stored value");
            var emptyRun = new CommercialCampaignRun(_campaign, _commercialWorld);
            CommercialCampaignSave emptySave = CommercialCampaignSaveCodec.Capture(
                _campaign,
                _commercialWorld,
                campaignSha256,
                worldSha256,
                emptyRun);
            CommercialCampaignSaveWriteResult overwritten =
                CommercialCampaignSaveStore.SaveWithStageDBackup(freshPath, emptySave);
            Equal(CommercialCampaignSaveWriteStatus.Saved, overwritten.Status,
                "campaign overwrite status");
            Equal(emptySave, CommercialCampaignSaveStore.Load(freshPath).Save,
                "campaign overwrite value");
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private string BuildCampaignNode(
        CommercialCampaignRun run,
        string nodeClassId,
        MapPoint position,
        string label)
    {
        HashSet<string> before = run.GetSnapshot().Construction.World.Nodes
            .Select(item => item.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        NodePlacementPreview preview = run.PreviewNodePlacement(nodeClassId, position);
        Check(preview.Accepted,
            $"{label}: preview rejected with {preview.Error}");
        CampaignAccepted(
            run,
            CommercialCoreCommand.SetNodeDraft(nodeClassId, position),
            $"{label}: set draft");
        CommercialCampaignProjectQuote quote = run.PreviewNodeOrder();
        Check(quote.Accepted && quote.Error is null && quote.ConstructionError is null,
            $"{label}: quote rejected with {quote.Error}/{quote.ConstructionError}");
        Check(quote.CostCashUnit is > 0 && quote.BuildMinutes is > 0,
            $"{label}: node quote lacks positive cost/time");
        CampaignAccepted(run, CommercialCoreCommand.OrderNode(), $"{label}: order");
        CampaignAccepted(
            run,
            CommercialCoreCommand.AdvanceConstruction(),
            $"{label}: complete");
        SpatialNodeDefinition node = run.GetSnapshot().Construction.World.Nodes.Single(item =>
            !before.Contains(item.NodeId));
        Equal(nodeClassId, node.ClassId, $"{label}: completed node class");
        Equal(position, node.Position, $"{label}: completed node position");
        Check(node.Commissioned, $"{label}: completed node remains uncommissioned");
        return node.NodeId;
    }

    private CommercialCampaignProjectQuote DraftCampaignLine(
        CommercialCampaignRun run,
        string startNodeId,
        string lineClassId,
        string poleClassId,
        IReadOnlyList<MapPoint> points,
        string endNodeId,
        string label)
    {
        LineStartPreview start = run.PreviewLineStart(
            startNodeId,
            lineClassId,
            poleClassId);
        Check(start.Accepted, $"{label}: start preview rejected with {start.Error}");
        CampaignAccepted(
            run,
            CommercialCoreCommand.StartLineDraft(
                startNodeId,
                lineClassId,
                poleClassId),
            $"{label}: start draft");
        for (int index = 0; index < points.Count; index++)
        {
            LinePointPreview preview = run.PreviewLinePoint(points[index]);
            Check(preview.Accepted,
                $"{label}: point {index} preview rejected with {preview.Error}");
            CampaignAccepted(
                run,
                CommercialCoreCommand.AddLinePoint(points[index]),
                $"{label}: add point {index}");
        }
        LineFinishPreview finish = run.PreviewLineFinish(endNodeId);
        Check(finish.Accepted,
            $"{label}: finish preview rejected with {finish.Error}");
        CampaignAccepted(
            run,
            CommercialCoreCommand.FinishLineDraft(endNodeId),
            $"{label}: finish draft");
        CommercialCampaignProjectQuote quote = run.PreviewLineOrder();
        Check(quote.Accepted && quote.Error is null && quote.ConstructionError is null,
            $"{label}: quote rejected with {quote.Error}/{quote.ConstructionError}");
        Check(quote.CostCashUnit is > 0 && quote.BuildMinutes is > 0,
            $"{label}: line quote lacks positive cost/time");
        return quote;
    }

    private CommercialCampaignProjectQuote BuildCampaignLine(
        CommercialCampaignRun run,
        string startNodeId,
        string lineClassId,
        string poleClassId,
        IReadOnlyList<MapPoint> points,
        string endNodeId,
        string label,
        Action<CommercialCampaignProjectQuote>? inspectQuote = null)
    {
        CommercialCampaignProjectQuote quote = DraftCampaignLine(
            run,
            startNodeId,
            lineClassId,
            poleClassId,
            points,
            endNodeId,
            label);
        inspectQuote?.Invoke(quote);
        FinishCampaignLine(run, label);
        return quote;
    }

    private void FinishCampaignLine(CommercialCampaignRun run, string label)
    {
        CampaignAccepted(run, CommercialCoreCommand.OrderLine(), $"{label}: order");
        CampaignAccepted(
            run,
            CommercialCoreCommand.AdvanceConstruction(),
            $"{label}: complete");
    }

    private void CampaignAccepted(
        CommercialCampaignRun run,
        CommercialCoreCommand command,
        string label)
    {
        CommercialCampaignCommandResult result = run.Execute(command);
        Check(result.Accepted,
            $"{label}: rejected with {result.Error}/{result.ConstructionError}");
        Check(result.Error is null && result.ConstructionError is null &&
                result.ConnectionFailure is null,
            $"{label}: accepted command retained an error");
    }

    private void CampaignRejectedPreserves(
        CommercialCampaignRun run,
        CommercialCoreCommand command,
        CommercialCampaignRunError expectedError,
        CommercialCampaignConnectionFailure? expectedConnectionFailure,
        string label)
    {
        string before = CampaignStateJson(run.GetSnapshot());
        int commandCount = run.CommandCount;
        CommercialCampaignCommandResult result = run.Execute(command);
        Check(!result.Accepted, $"{label}: command was accepted");
        Equal(expectedError, result.Error, $"{label}: typed error");
        Equal(expectedConnectionFailure, result.ConnectionFailure,
            $"{label}: connection failure detail");
        Equal(commandCount, run.CommandCount, $"{label}: rejected command was journaled");
        Equal(before, CampaignStateJson(result.Snapshot),
            $"{label}: returned snapshot changed");
        Equal(before, CampaignStateJson(run.GetSnapshot()),
            $"{label}: live snapshot changed");
    }

    private static CommercialPhaseProjection CampaignProjection(
        CommercialCampaignSnapshot snapshot,
        string phaseId) =>
        snapshot.Projections.Single(item => item.Phase.PhaseId == phaseId);

    private static string CampaignStateJson(CommercialCampaignSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot);

    private static string CampaignWorldSignature(SpatialWorldDefinition world) =>
        JsonSerializer.Serialize(new
        {
            world.Nodes,
            world.Edges,
        });

    private sealed record CampaignRouteState(
        string EastSubstationId,
        string? HospitalHighSubstationId,
        string? HospitalSecondSubstationId,
        string? NorthLargeSubstationId = null);

    private void CheckCommercialCoreDesignsPreviewAndOutcomes()
    {
        CommercialCoreSliceRun shortRun = EnterCommercialMain("short shared design");
        CoreAccepted(
            shortRun,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Keep),
            "keep promise for short shared design");
        CommercialCoreProjectQuote shortQuote = DraftShortSharedFactoryLine(shortRun);
        CoreQuote(shortQuote, 100000, 40, 6880, "short shared design quote");
        SequenceEqual(["RIVER_FLOOD_ZONE"], shortQuote.RiskAreaIds,
            "short shared design risk exposure");
        CommercialCoreSnapshot shortDraft = shortRun.GetSnapshot();
        Check(shortDraft.ProjectionIncludesCurrentConstruction,
            "short design projection omitted the completed draft");
        CommercialPhaseProjection shortHotProjection = Projection(shortDraft, "HOT_EVENING");
        Check(shortHotProjection.SafetySatisfied && shortHotProjection.PromiseSatisfied,
            "short design preview did not satisfy current obligations");
        Check(
            shortHotProjection.ProjectedWorld is SpatialWorldDefinition shortProjectedWorld &&
            shortProjectedWorld.Edges.Any(edge => edge.EdgeId == "PLAYER_EDGE_1") &&
            shortProjectedWorld.Nodes.Any(node => node.NodeId == "PLAYER_POLE_1") &&
            ((IList<SpatialNodeDefinition>)shortProjectedWorld.Nodes).IsReadOnly &&
            ((IList<SpatialEdgeDefinition>)shortProjectedWorld.Edges).IsReadOnly,
            "short design projection did not expose its exact frozen projected geometry");
        ThermalIntervalEvaluation shortPreview = shortHotProjection.Evaluation;
        CoreAccepted(shortRun, CommercialCoreCommand.OrderLine(), "order short shared design");
        CoreAccepted(shortRun, CommercialCoreCommand.AdvanceConstruction(),
            "complete short shared design");
        Equal(600000L, shortRun.GetSnapshot().CashUnit, "short design remaining cash");
        Equal(6880L, shortRun.GetSnapshot().Minute, "short design completion minute");
        CoreAccepted(shortRun, CommercialCoreCommand.ApproveDecisionWindow(),
            "approve short design hot evening");

        CommercialCoreSnapshot shortRecovery = shortRun.GetSnapshot();
        ThermalIntervalEvaluation shortCommitted = shortRecovery.CommittedPhases
            .Single(item => item.PhaseId == "HOT_EVENING")
            .Evaluation;
        Equal(shortPreview, shortCommitted, "short design preview must equal approval");
        ThermalLoadSupply shortFactory = Supply(shortCommitted, "RIVER_FACTORY");
        Equal(2700L, shortFactory.DeliveredKw, "short design factory delivery");
        Equal("SOUTH_GENERATION", shortFactory.SourceId,
            "short design factory source");
        Equal(300L, shortFactory.MinimumRemainingKw,
            "short design representative shared bottleneck margin");
        Check(shortFactory.PathEdgeIds.Contains("PLAYER_EDGE_1", StringComparer.Ordinal) &&
                shortFactory.PathEdgeIds.Contains("PLAYER_EDGE_2", StringComparer.Ordinal),
            "short design result omitted its actual player path");
        Equal(2700L, Asset(shortCommitted, "EDGE_SOUTH_SOURCE").UsedKw,
            "short design shared mainline use");
        Equal(ThermalOperatingState.Emergency,
            Asset(shortCommitted, "EDGE_SOUTH_SOURCE").State,
            "short design shared mainline state");
        Equal(ThermalOperatingState.Emergency,
            Asset(shortCommitted, "PLAYER_POLE_1").State,
            "short design connection state");

        CommercialPhaseProjection recoveryProjection = Projection(shortRecovery, "NIGHT_RECOVERY");
        Check(recoveryProjection.SafetySatisfied,
            "short design did not preserve next-phase safety duties");
        Equal(ThermalOperatingState.ProtectiveOutage,
            Asset(recoveryProjection.Evaluation, "EDGE_SOUTH_SOURCE").State,
            "short design emergency mainline did not enter protective outage");
        Equal(400L, Supply(recoveryProjection.Evaluation, "HOSPITAL").DeliveredKw,
            "short design recovery hospital delivery");
        Equal(400L, Supply(recoveryProjection.Evaluation, "WATERWORKS").DeliveredKw,
            "short design recovery waterworks delivery");
        CoreAccepted(shortRun, CommercialCoreCommand.ApproveDecisionWindow(),
            "approve short design recovery");
        CommercialCoreSnapshot shortComplete = shortRun.GetSnapshot();
        Check(shortComplete.CampaignComplete, "short design did not complete the slice");
        Equal(CommercialPromiseDecision.Keep, shortComplete.LastOutcome!.PromiseDecision,
            "short design kept outcome");
        Equal("증산 약속과 다음 국면을 함께 확인했습니다",
            shortComplete.LastOutcome.ResultCard.Title,
            "short design kept result card");
        Equal(2, shortComplete.LastOutcome.Phases.Count,
            "short design typed result phase facts");
        Equal(0, shortComplete.ThermalState.CoolingAssetIds.Count,
            "short design cooling did not clear after one phase");

        CommercialCoreSliceRun longRun = EnterCommercialMain("long separate design");
        CoreAccepted(
            longRun,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Keep),
            "keep promise for long separate design");
        CommercialCoreProjectQuote longQuote = DraftLongSeparateFactoryLine(longRun);
        CoreQuote(longQuote, 568000, 198, 7038, "long separate design quote");
        SequenceEqual(["RIVER_FLOOD_ZONE"], longQuote.RiskAreaIds,
            "long separate design risk exposure");
        CommercialCoreSnapshot longDraft = longRun.GetSnapshot();
        Check(longDraft.ProjectionIncludesCurrentConstruction,
            "long design projection omitted the completed draft");
        CommercialPhaseProjection longHotProjection = Projection(longDraft, "HOT_EVENING");
        Check(longHotProjection.SafetySatisfied && longHotProjection.PromiseSatisfied,
            "long design preview did not satisfy current obligations");
        ThermalIntervalEvaluation longPreview = longHotProjection.Evaluation;
        ThermalLoadSupply longFactoryPreview = Supply(longPreview, "RIVER_FACTORY");
        Equal("SOUTH_GENERATION", longFactoryPreview.SourceId,
            "long design factory source");
        Equal(300L, longFactoryPreview.MinimumRemainingKw,
            "long design continuous corridor margin");
        Check(longFactoryPreview.PathEdgeIds.SequenceEqual(
                [
                    "PLAYER_EDGE_1",
                    "PLAYER_EDGE_2",
                    "PLAYER_EDGE_3",
                    "PLAYER_EDGE_4",
                    "PLAYER_EDGE_5",
                    "EDGE_FACTORY",
                ]),
            "long design did not use its separate corridor");
        Check(longFactoryPreview.PathEdgeIds.All(id =>
                Asset(longPreview, id).State == ThermalOperatingState.Continuous),
            "long design corridor exceeded continuous limits");
        CoreAccepted(longRun, CommercialCoreCommand.OrderLine(), "order long separate design");
        CoreAccepted(longRun, CommercialCoreCommand.AdvanceConstruction(),
            "complete long separate design");
        CoreAccepted(longRun, CommercialCoreCommand.ApproveDecisionWindow(),
            "approve long design hot evening");
        Equal(longPreview,
            longRun.GetSnapshot().CommittedPhases.Single(item => item.PhaseId == "HOT_EVENING")
                .Evaluation,
            "long design preview must equal approval");
        Equal(0, longRun.GetSnapshot().ThermalState.CoolingAssetIds.Count,
            "long continuous design unexpectedly scheduled cooling");
        CoreAccepted(longRun, CommercialCoreCommand.ApproveDecisionWindow(),
            "approve long design recovery");
        CommercialCoreSnapshot longComplete = longRun.GetSnapshot();
        Check(longComplete.CampaignComplete, "long design did not complete the slice");
        Equal(CommercialPromiseDecision.Keep, longComplete.LastOutcome!.PromiseDecision,
            "long design kept outcome");
        Equal(132000L, longComplete.LastOutcome.EndingCashUnit,
            "long design ending cash fact");
        Equal(7038L, longComplete.LastOutcome.EndingMinute,
            "long design ending minute fact");

        CommercialCoreSliceRun deferredRun = EnterCommercialMain("deferred promise");
        Check(!deferredRun.GetSnapshot().CanApprove,
            "main window approved before an explicit promise decision");
        CoreAccepted(
            deferredRun,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Defer),
            "defer factory promise");
        Check(deferredRun.GetSnapshot().CanApprove,
            "deferred promise should allow the safety-only plan");
        CoreAccepted(deferredRun, CommercialCoreCommand.ApproveDecisionWindow(),
            "approve deferred hot evening");
        Check(!deferredRun.GetSnapshot().CommittedPhases[0].Evaluation.Loads.Any(item =>
                item.LoadId == "RIVER_FACTORY"),
            "deferred factory remained in the thermal request");
        CoreAccepted(deferredRun, CommercialCoreCommand.ApproveDecisionWindow(),
            "approve deferred recovery");
        CommercialCoreSnapshot deferredComplete = deferredRun.GetSnapshot();
        Check(deferredComplete.CampaignComplete, "deferred plan did not complete the slice");
        Equal(CommercialPromiseDecision.Defer, deferredComplete.LastOutcome!.PromiseDecision,
            "deferred outcome decision");
        Equal("증산 약속을 이번에는 미뤘습니다",
            deferredComplete.LastOutcome.ResultCard.Title,
            "deferred result card");
        Equal(700000L, deferredComplete.LastOutcome.EndingCashUnit,
            "deferred plan must not change the fixed grant or seed cash");
    }

    private void CheckCommercialCoreRiskProjectionApproval()
    {
        var focusedRisk = new SpatialRiskAreaDefinition(
            "CHECK_FACTORY_RISK",
            "산업단지 인입선 점검구역",
            [
                new MapPoint(2330, 1590),
                new MapPoint(2380, 1590),
                new MapPoint(2380, 1660),
                new MapPoint(2330, 1660),
            ]);
        CommercialWorldDefinition riskWorld = _commercialWorld with
        {
            RiskAreas = _commercialWorld.RiskAreas.Concat([focusedRisk]).ToArray(),
        };
        var phase = new CommercialOperatingPhaseDefinition(
            "RISK_CROSSING_RECORD",
            "산업단지 인입선 점검",
            CommercialPhaseThermalPolicy.ContinuousOnly,
            null,
            [
                new CommercialLoadBundleDefinition(
                    "WATERWORKS",
                    100,
                    CommercialObligationKind.SafetyDuty),
                new CommercialLoadBundleDefinition(
                    "HOSPITAL",
                    100,
                    CommercialObligationKind.CityPromise),
                new CommercialLoadBundleDefinition(
                    "RIVER_FACTORY",
                    500,
                    CommercialObligationKind.OperatingRecord),
            ],
            Array.Empty<string>(),
            Array.Empty<string>(),
            ["CHECK_FACTORY_RISK"],
            Array.Empty<ThermalLimitOverride>());
        CommercialCoreChapterDefinition riskChapter = _coreSlice.Main.Chapter with
        {
            CityPromise = _coreSlice.Main.Chapter.CityPromise! with
            {
                PromiseId = "CHECK_HOSPITAL_PROMISE",
                DisplayName = "의료원 점검 중 공급",
                LoadId = "HOSPITAL",
            },
            DecisionWindows =
            [
                new CommercialDecisionWindowDefinition(
                    "BEFORE_RISK_CROSSING_RECORD",
                    phase.PhaseId,
                    null,
                    null),
            ],
            OperatingPhases = [phase],
        };
        CommercialCoreSliceDefinition riskSlice = _coreSlice with
        {
            Main = _coreSlice.Main with { Chapter = riskChapter },
        };
        CommercialCoreSliceLoader.Validate(riskSlice, riskWorld);

        CommercialCoreSliceRun run = EnterCommercialMain(
            riskSlice,
            riskWorld,
            "risk-crossing projection");
        CoreAccepted(
            run,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Defer),
            "defer promise for risk-crossing record");
        CommercialCoreSnapshot previewSnapshot = run.GetSnapshot();
        Check(previewSnapshot.CanApprove,
            "nonblocking risk operating record could not be approved");
        ThermalIntervalEvaluation preview = Projection(
            previewSnapshot,
            "RISK_CROSSING_RECORD").Evaluation;
        ThermalLoadSupply factory = Supply(preview, "RIVER_FACTORY");
        Failure(
            factory,
            ThermalFailureKind.AssetUnavailable,
            "EDGE_FACTORY",
            500,
            0,
            "active risk crossing");
        CoreAccepted(run, CommercialCoreCommand.ApproveDecisionWindow(),
            "approve risk-crossing operating record");
        CommercialCoreSnapshot completed = run.GetSnapshot();
        Check(completed.CampaignComplete,
            "risk-crossing operating record did not complete");
        Equal(preview, completed.LastOutcome!.Phases.Single().Evaluation,
            "active-risk projection must equal approved result");
    }

    private void CheckCommercialCoreRejectionsAndRecovery()
    {
        CommercialCoreSliceRun unsafeRun = EnterCommercialMain("future-safety rejection");
        CoreAccepted(
            unsafeRun,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Keep),
            "keep promise for unsafe design");
        CommercialCoreProjectQuote unsafeQuote = DraftUnsafeSharedFactoryLine(unsafeRun);
        CoreQuote(unsafeQuote, 90000, 36, 6876, "unsafe shared design quote");
        CoreAccepted(unsafeRun, CommercialCoreCommand.OrderLine(), "order unsafe shared design");
        CoreAccepted(unsafeRun, CommercialCoreCommand.AdvanceConstruction(),
            "complete unsafe shared design");
        CommercialCoreSnapshot unsafePreview = unsafeRun.GetSnapshot();
        Check(Projection(unsafePreview, "HOT_EVENING").SafetySatisfied &&
                Projection(unsafePreview, "HOT_EVENING").PromiseSatisfied,
            "unsafe counterexample did not satisfy the current phase");
        Check(!Projection(unsafePreview, "NIGHT_RECOVERY").SafetySatisfied,
            "unsafe counterexample did not expose future safety loss");
        Equal(ThermalFailureKind.AssetUnavailable,
            unsafePreview.FirstBlockingFailure!.Kind,
            "future-safety blocking failure kind");
        int unsafeCommandCount = unsafeRun.CommandCount;
        string unsafeState = CoreStateJson(unsafeRun.GetSnapshot());
        CoreRejected(
            unsafeRun,
            CommercialCoreCommand.ApproveDecisionWindow(),
            CommercialCoreRunError.FutureSafetyAtRisk,
            "future-safety approval");
        Equal(unsafeCommandCount, unsafeRun.CommandCount,
            "future-safety rejection changed the command journal");
        Equal(unsafeState, CoreStateJson(unsafeRun.GetSnapshot()),
            "future-safety rejection changed run state");

        CommercialCoreSliceRun cashRun = EnterCommercialMain("cash rejection");
        MapPoint expensivePosition = new(400, 1100);
        Check(cashRun.PreviewNodePlacement("LARGE_SUBSTATION", expensivePosition).Accepted,
            "cash counterexample placement was invalid");
        CoreAccepted(cashRun,
            CommercialCoreCommand.SetNodeDraft("LARGE_SUBSTATION", expensivePosition),
            "draft unaffordable substation");
        CommercialCoreProjectQuote cashQuote = cashRun.PreviewNodeOrder();
        Check(!cashQuote.Accepted, "unaffordable substation quote was accepted");
        Equal(CommercialCoreRunError.InsufficientCash, cashQuote.Error,
            "unaffordable substation quote error");
        CoreRejected(cashRun, CommercialCoreCommand.OrderNode(),
            CommercialCoreRunError.InsufficientCash,
            "unaffordable substation order");
        CoreAccepted(cashRun, CommercialCoreCommand.CancelNodeDraft(),
            "cancel unaffordable substation");

        CommercialCoreSliceRun deadlineRun = EnterCommercialMain("deadline rejection");
        CoreQuote(
            DraftCoreLine(
                deadlineRun,
                "NORTH_SUBSTATION",
                "STANDARD_LINE",
                "STANDARD_POLE",
                Array.Empty<MapPoint>(),
                "EAST_SUBSTATION",
                "deadline setup tie"),
            20000,
            8,
            6848,
            "deadline setup tie quote");
        CoreAccepted(deadlineRun, CommercialCoreCommand.OrderLine(),
            "order deadline setup tie");
        CoreAccepted(deadlineRun, CommercialCoreCommand.AdvanceConstruction(),
            "complete deadline setup tie");
        DraftLongSeparateFactoryLine(deadlineRun);
        CommercialCoreProjectQuote deadlineQuote = deadlineRun.PreviewLineOrder();
        Check(!deadlineQuote.Accepted, "over-deadline long route quote was accepted");
        Equal(CommercialCoreRunError.DeadlineExceeded, deadlineQuote.Error,
            "over-deadline route error");
        Equal(568000L, deadlineQuote.CostCashUnit,
            "deadline rejection must preserve the valid project cost");
        Equal(198L, deadlineQuote.BuildMinutes,
            "deadline rejection must preserve the valid project duration");
        int deadlineCommands = deadlineRun.CommandCount;
        CoreRejected(deadlineRun, CommercialCoreCommand.OrderLine(),
            CommercialCoreRunError.DeadlineExceeded,
            "over-deadline route order");
        Equal(deadlineCommands, deadlineRun.CommandCount,
            "deadline rejection changed the command journal");

        CommercialCoreSliceRun rollbackRun = EnterCommercialMain("recent project rollback");
        CoreAccepted(
            rollbackRun,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Defer),
            "set rollback baseline promise");
        string rollbackBaseline = CoreStateJson(rollbackRun.GetSnapshot());
        DraftShortSharedFactoryLine(rollbackRun);
        CoreAccepted(rollbackRun, CommercialCoreCommand.OrderLine(),
            "order rollback project");
        CoreAccepted(rollbackRun, CommercialCoreCommand.AdvanceConstruction(),
            "complete rollback project");
        Check(rollbackRun.GetSnapshot().CanRollbackRecentProject,
            "completed project did not expose recent rollback");
        Check(rollbackRun.UndoRecentConstruction(), "recent project rollback was rejected");
        Equal(rollbackBaseline, CoreStateJson(rollbackRun.GetSnapshot()),
            "recent rollback did not restore coordinates/cash/minute/promise/thermal state");

        CommercialCoreSliceRun windowRun = EnterCommercialMain("window restart");
        string windowBaseline = CoreStateJson(windowRun.GetSnapshot());
        CoreAccepted(
            windowRun,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Defer),
            "set window restart promise");
        DraftShortSharedFactoryLine(windowRun);
        CoreAccepted(windowRun, CommercialCoreCommand.OrderLine(),
            "order window restart project");
        CoreAccepted(windowRun, CommercialCoreCommand.AdvanceConstruction(),
            "complete window restart project");
        Check(windowRun.RestartDecisionWindow(), "decision-window restart was rejected");
        Equal(windowBaseline, CoreStateJson(windowRun.GetSnapshot()),
            "decision-window restart did not restore its checkpoint");

        CommercialCoreSliceRun chapterRun = EnterCommercialMain("chapter restart");
        string chapterBaseline = CoreStateJson(chapterRun.GetSnapshot());
        CoreAccepted(
            chapterRun,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Defer),
            "set chapter restart promise");
        CoreAccepted(chapterRun, CommercialCoreCommand.ApproveDecisionWindow(),
            "advance chapter restart run to second window");
        Check(chapterRun.RestartChapter(), "chapter restart was rejected");
        Equal(chapterBaseline, CoreStateJson(chapterRun.GetSnapshot()),
            "chapter restart did not restore the four-chapter-complete seed");
    }

    private void CheckCommercialCoreReplayDeterminism()
    {
        CommercialCoreSliceRun original = EnterCommercialMain("runner replay");
        CoreAccepted(
            original,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Keep),
            "set replay promise");
        DraftShortSharedFactoryLine(original);
        CoreAccepted(original, CommercialCoreCommand.OrderLine(), "order replay project");
        CoreAccepted(original, CommercialCoreCommand.AdvanceConstruction(),
            "complete replay project");
        CoreAccepted(original, CommercialCoreCommand.ApproveDecisionWindow(),
            "approve replay first window");

        CommercialCoreCommand[] journal = original.Commands.ToArray();
        CommercialCoreSliceRun restored = CommercialCoreSliceRun.Restore(
            _coreSlice,
            _commercialWorld,
            journal);
        SequenceEqual(journal, restored.Commands, "fresh runner restore command journal");
        Equal(CoreStateJson(original.GetSnapshot()), CoreStateJson(restored.GetSnapshot()),
            "fresh runner restore state");
        CoreAccepted(original, CommercialCoreCommand.ApproveDecisionWindow(),
            "complete original replay run");
        CoreAccepted(restored, CommercialCoreCommand.ApproveDecisionWindow(),
            "complete restored replay run");
        Equal(CoreStateJson(original.GetSnapshot()), CoreStateJson(restored.GetSnapshot()),
            "restored runner next transition determinism");
        Equal(original.Commands.Count, restored.Commands.Count,
            "restored runner final command count");
    }

    private void CheckCommercialCoreSaveV3()
    {
        string sliceSha256 = LowerSha256(_coreSliceBytes);
        string worldSha256 = LowerSha256(_worldBytes);
        CommercialCoreSliceRun run = EnterCommercialMain("save-v3 fresh restore");
        CoreAccepted(
            run,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Keep),
            "set save-v3 promise");
        DraftShortSharedFactoryLine(run);
        CoreAccepted(run, CommercialCoreCommand.OrderLine(), "order save-v3 project");
        CoreAccepted(run, CommercialCoreCommand.AdvanceConstruction(),
            "complete save-v3 project");
        CoreAccepted(run, CommercialCoreCommand.ApproveDecisionWindow(),
            "advance save-v3 run to second window");

        CommercialCoreSave save = CommercialCoreSaveCodec.Capture(
            _coreSlice,
            _commercialWorld,
            sliceSha256,
            worldSha256,
            run);
        Equal(CommercialCoreSave.SupportedSchemaVersion, save.SchemaVersion,
            "save-v3 schema");
        Equal(_coreSlice.SliceId, save.SliceId, "save-v3 slice identity");
        Equal(_commercialWorld.WorldId, save.WorldId, "save-v3 world identity");
        SequenceEqual(run.Commands, save.Commands, "save-v3 captured command journal");

        byte[] firstBytes = CommercialCoreSaveCodec.Serialize(save);
        byte[] secondBytes = CommercialCoreSaveCodec.Serialize(save);
        void ExpectSaveRejected(string label, Func<string, string> mutate) =>
            ExpectThrows<CommercialCorePersistenceException>(
                () => CommercialCoreSaveCodec.Deserialize(
                    mutate(Encoding.UTF8.GetString(firstBytes))),
                label);
        SequenceEqual(firstBytes, secondBytes, "save-v3 deterministic bytes");
        CommercialCoreSave fromBytes = CommercialCoreSaveCodec.Deserialize(firstBytes);
        CommercialCoreSave fromText = CommercialCoreSaveCodec.Deserialize(
            Encoding.UTF8.GetString(firstBytes));
        Equal(save, fromBytes, "save-v3 byte round trip");
        Equal(save, fromText, "save-v3 text round trip");
        JsonObject root = JsonNode.Parse(firstBytes)!.AsObject();
        Equal(6, root.Count, "save-v3 exact root field count");
        SequenceEqual(
            ["commands", "schemaVersion", "sliceId", "sliceSha256", "worldId", "worldSha256"],
            root.Select(item => item.Key).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            "save-v3 exact root fields");

        CommercialCoreSliceRun fresh = CommercialCoreSaveCodec.Restore(
            _coreSlice,
            _commercialWorld,
            sliceSha256,
            worldSha256,
            fromBytes);
        Equal(CoreStateJson(run.GetSnapshot()), CoreStateJson(fresh.GetSnapshot()),
            "save-v3 fresh restore state");
        SequenceEqual(run.Commands, fresh.Commands,
            "save-v3 fresh restore journal");
        CoreAccepted(run, CommercialCoreCommand.ApproveDecisionWindow(),
            "complete live save-v3 run");
        CoreAccepted(fresh, CommercialCoreCommand.ApproveDecisionWindow(),
            "complete restored save-v3 run");
        Equal(CoreStateJson(run.GetSnapshot()), CoreStateJson(fresh.GetSnapshot()),
            "save-v3 post-restore determinism");

        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialCoreSaveCodec.Restore(
                _coreSlice,
                _commercialWorld,
                new string('0', 64),
                worldSha256,
                save),
            "save-v3 slice hash mismatch");
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialCoreSaveCodec.Restore(
                _coreSlice,
                _commercialWorld,
                sliceSha256,
                worldSha256,
                save with { WorldId = "OTHER_WORLD" }),
            "save-v3 world identity mismatch");

        ExpectSaveRejected("save-v3 duplicate root property", json =>
        {
            string trimmed = json.TrimStart();
            return $"{{\"schemaVersion\":\"duplicate\",{trimmed[1..]}";
        });
        ExpectSaveRejected("save-v3 unknown root field", json =>
        {
            JsonObject candidate = JsonNode.Parse(json)!.AsObject();
            candidate["unexpected"] = true;
            return candidate.ToJsonString();
        });
        ExpectSaveRejected("save-v3 missing root field", json =>
        {
            JsonObject candidate = JsonNode.Parse(json)!.AsObject();
            candidate.Remove("sliceId");
            return candidate.ToJsonString();
        });
        ExpectSaveRejected("save-v3 null command journal", json =>
        {
            JsonObject candidate = JsonNode.Parse(json)!.AsObject();
            candidate["commands"] = null;
            return candidate.ToJsonString();
        });
        ExpectSaveRejected("save-v3 unknown command kind", json =>
        {
            JsonObject candidate = JsonNode.Parse(json)!.AsObject();
            Object(JsonArrayProperty(candidate, "commands")[0]!)["kind"] = "futureCommand";
            return candidate.ToJsonString();
        });
        ExpectSaveRejected("save-v3 extra command field", json =>
        {
            JsonObject candidate = JsonNode.Parse(json)!.AsObject();
            Object(JsonArrayProperty(candidate, "commands")[0]!)["unexpected"] = true;
            return candidate.ToJsonString();
        });
        ExpectThrows<CommercialCorePersistenceException>(
            () =>
            {
                JsonObject candidate = JsonNode.Parse(firstBytes)!.AsObject();
                Object(JsonArrayProperty(candidate, "commands")[0]!)["firstId"] =
                    "UNKNOWN_NODE";
                CommercialCoreSave invalidReplay = CommercialCoreSaveCodec.Deserialize(
                    candidate.ToJsonString());
                _ = CommercialCoreSaveCodec.Restore(
                    _coreSlice,
                    _commercialWorld,
                    sliceSha256,
                    worldSha256,
                    invalidReplay);
            },
            "save-v3 invalid replay command");

        var oversized = new CommercialCoreSave(
            CommercialCoreSave.SupportedSchemaVersion,
            _coreSlice.SliceId,
            sliceSha256,
            _commercialWorld.WorldId,
            worldSha256,
            Enumerable.Repeat(
                    CommercialCoreCommand.CancelNodeDraft(),
                    CommercialCoreSliceRun.MaximumAcceptedCommands + 1)
                .ToArray());
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialCoreSaveCodec.Validate(oversized),
            "save-v3 command journal maximum");

        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"gridworks-commercial-save-check-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string savePath = Path.Combine(temporaryDirectory, "release-campaign-save-v3.json");
        try
        {
            CommercialCoreSaveLoadResult missing = CommercialCoreSaveStore.Load(savePath);
            Equal(CommercialCoreSaveLoadStatus.Missing, missing.Status,
                "save-v3 missing store state");
            Check(missing.Save is null && missing.ErrorMessage is null,
                "save-v3 missing store payload");

            File.WriteAllText(savePath + ".tmp", "stale temporary save");
            CommercialCoreSaveStore.Save(savePath, save);
            Check(!File.Exists(savePath + ".tmp"),
                "save-v3 stale temporary file survived atomic save");
            CommercialCoreSaveLoadResult loaded = CommercialCoreSaveStore.Load(savePath);
            Equal(CommercialCoreSaveLoadStatus.Loaded, loaded.Status,
                "save-v3 loaded store state");
            Equal(save, loaded.Save, "save-v3 stored value");

            CommercialCoreSave completedSave = CommercialCoreSaveCodec.Capture(
                _coreSlice,
                _commercialWorld,
                sliceSha256,
                worldSha256,
                run);
            CommercialCoreSaveStore.Save(savePath, completedSave);
            CommercialCoreSaveLoadResult overwritten = CommercialCoreSaveStore.Load(savePath);
            Equal(CommercialCoreSaveLoadStatus.Loaded, overwritten.Status,
                "save-v3 overwrite load state");
            Equal(completedSave, overwritten.Save, "save-v3 atomic overwrite value");
            Check(!save.Equals(overwritten.Save),
                "save-v3 overwrite retained the previous journal");

            File.WriteAllText(savePath, "{ invalid save");
            CommercialCoreSaveLoadResult invalid = CommercialCoreSaveStore.Load(savePath);
            Equal(CommercialCoreSaveLoadStatus.Invalid, invalid.Status,
                "save-v3 invalid store state");
            Check(invalid.Save is null && !string.IsNullOrWhiteSpace(invalid.ErrorMessage),
                "save-v3 invalid store diagnostics");
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private void CheckNodePlacementAndRisk()
    {
        TerrainPolygonDefinition water = Terrain("WATER", TerrainKind.Water, 300, 300, 500, 500);
        TerrainPolygonDefinition building = Terrain("BUILDING", TerrainKind.Building, 700, 700, 900, 900);
        SpatialRiskAreaDefinition riskZ = Risk("Z_RISK", 1100, 1100, 1300, 1300);
        SpatialRiskAreaDefinition riskA = Risk("A_RISK", 1050, 1050, 1350, 1350);
        SpatialWorldDefinition world = World(
            [Node("A", 100, 100)],
            terrain: [water, building],
            risks: [riskZ, riskA]);
        SpatialWorldLoader.Validate(world);

        Check(PlacementValidator.PreviewNodePlacement(
                world,
                SubstationClassId,
                new MapPoint(1500, 1500)).Accepted,
            "safe node placement was rejected");
        Error(
            ConstructionError.OutsideBounds,
            PlacementValidator.PreviewNodePlacement(
                world,
                SubstationClassId,
                new MapPoint(10, 1500)),
            "full footprint map bounds");
        Error(
            ConstructionError.WaterFootprint,
            PlacementValidator.PreviewNodePlacement(
                world,
                SubstationClassId,
                new MapPoint(400, 400)),
            "water footprint");
        Error(
            ConstructionError.BuildingFootprint,
            PlacementValidator.PreviewNodePlacement(
                world,
                SubstationClassId,
                new MapPoint(800, 800)),
            "building footprint");
        Error(
            ConstructionError.PositionOccupied,
            PlacementValidator.PreviewNodePlacement(
                world,
                SubstationClassId,
                new MapPoint(130, 100)),
            "node-footprint tangency");

        NodePlacementPreview risky = PlacementValidator.PreviewNodePlacement(
            world,
            SubstationClassId,
            new MapPoint(1200, 1200));
        Check(risky.Accepted, "risk area incorrectly blocked placement");
        SequenceEqual(["A_RISK", "Z_RISK"], risky.RiskAreaIds,
            "risk identifiers must be stable and sorted");
        ExpectThrows<NotSupportedException>(
            () => ((IList<string>)risky.RiskAreaIds).Add("MUTATION"),
            "risk preview collection must be immutable");

        SpatialWorldDefinition edgeWorld = World(
            [Node("A", 100, 200), Node("B", 700, 200, LoadClassId)],
            [Edge("E", "A", "B")]);
        SpatialWorldLoader.Validate(edgeWorld);
        Error(
            ConstructionError.ExistingLineTouch,
            PlacementValidator.PreviewNodePlacement(
                edgeWorld,
                SubstationClassId,
                new MapPoint(400, 220)),
            "new footprint touching existing line body");
    }

    private void CheckLineGeometryAndRisk()
    {
        SpatialWorldDefinition thirdNodeWorld = World(
        [
            Node("A", 100, 100),
            Node("B", 500, 100, LoadClassId),
            Node("C", 300, 110, PoleClassId),
            Node("D", 800, 100, LoadClassId),
        ]);
        SpatialWorldLoader.Validate(thirdNodeWorld);
        LineDraftSnapshot thirdNodeDraft = Draft("A");
        Error(
            ConstructionError.ZeroLengthSegment,
            PlacementValidator.PreviewLinePoint(
                thirdNodeWorld,
                thirdNodeDraft,
                new MapPoint(100, 100)),
            "positive intermediate segment length");
        Error(
            ConstructionError.ThirdNodeTouch,
            PlacementValidator.PreviewLineFinish(thirdNodeWorld, thirdNodeDraft, "B"),
            "third-node footprint tangency");
        Error(
            ConstructionError.SpanTooLong,
            PlacementValidator.PreviewLineFinish(thirdNodeWorld, thirdNodeDraft, "D"),
            "maximum span");
        Error(
            ConstructionError.SameEndpoint,
            PlacementValidator.PreviewLineFinish(thirdNodeWorld, thirdNodeDraft, "A"),
            "explicit same endpoint");

        SpatialWorldDefinition duplicateWorld = World(
            [Node("A", 100, 100), Node("B", 500, 100, LoadClassId)],
            [Edge("E", "A", "B")]);
        SpatialWorldLoader.Validate(duplicateWorld);
        Error(
            ConstructionError.DuplicateSegment,
            PlacementValidator.PreviewLineFinish(duplicateWorld, Draft("B"), "A"),
            "unordered duplicate endpoints");

        SpatialWorldDefinition overlapWorld = World(
        [
            Node("A", 100, 100),
            Node("B", 700, 100, LoadClassId),
            Node("C", 300, 100, PoleClassId),
            Node("D", 500, 100, PoleClassId),
        ],
        [Edge("EXISTING", "C", "D")]);
        SpatialWorldLoader.Validate(overlapWorld);
        Error(
            ConstructionError.CollinearOverlap,
            PlacementValidator.PreviewLineFinish(overlapWorld, Draft("A"), "B"),
            "positive collinear overlap");

        SpatialWorldDefinition buildingWorld = World(
            [Node("A", 100, 500), Node("B", 700, 500, LoadClassId)],
            terrain: [Terrain("BLOCK", TerrainKind.Building, 300, 400, 500, 600)]);
        SpatialWorldLoader.Validate(buildingWorld);
        Error(
            ConstructionError.BuildingCrossing,
            PlacementValidator.PreviewLineFinish(buildingWorld, Draft("A"), "B"),
            "building crossing");

        SpatialWorldDefinition waterWorld = World(
            [Node("A", 100, 500), Node("B", 700, 500, LoadClassId)],
            terrain: [Terrain("RIVER", TerrainKind.Water, 300, 400, 500, 600)]);
        SpatialWorldLoader.Validate(waterWorld);
        Check(PlacementValidator.PreviewLineFinish(waterWorld, Draft("A"), "B").Accepted,
            "line crossing water was rejected");

        SpatialWorldDefinition riskWorld = World(
            [Node("A", 800, 1200), Node("B", 1400, 1200, LoadClassId)],
            risks:
            [
                Risk("Z_RISK", 1000, 1100, 1300, 1300),
                Risk("A_RISK", 1050, 1050, 1250, 1350),
            ]);
        SpatialWorldLoader.Validate(riskWorld);
        LineFinishPreview riskPreview = PlacementValidator.PreviewLineFinish(
            riskWorld,
            Draft("A"),
            "B");
        Check(riskPreview.Accepted, "risk area incorrectly blocked line");
        SequenceEqual(["A_RISK", "Z_RISK"], riskPreview.RiskAreaIds,
            "line risk identifiers must be stable and sorted");

        SpatialWorldDefinition draftContactWorld = World(
        [
            Node("A", 100, 100),
            Node("B", 100, 900, LoadClassId),
        ]);
        var draftContact = new ConstructionSession(draftContactWorld);
        Accepted(draftContact.StartLineDraft("A", LineClassId, PoleClassId),
            "start draft-contact path");
        Accepted(draftContact.AddLinePoint(new MapPoint(500, 100)),
            "draft-contact first point");
        Accepted(draftContact.AddLinePoint(new MapPoint(500, 500)),
            "draft-contact second point");
        Error(
            ConstructionError.ThirdNodeTouch,
            draftContact.PreviewLinePoint(new MapPoint(300, 110)),
            "new pole footprint touching non-adjacent draft segment");
        string beforeBadPoint = JsonSerializer.Serialize(draftContact.GetSnapshot());
        ConstructionCommandResult badPoint = draftContact.AddLinePoint(new MapPoint(300, 110));
        Check(!badPoint.Accepted && badPoint.Error == ConstructionError.ThirdNodeTouch,
            "draft-segment contact preview/command mismatch");
        Equal(beforeBadPoint, JsonSerializer.Serialize(draftContact.GetSnapshot()),
            "rejected draft-segment contact changed the draft");
        Accepted(draftContact.AddLinePoint(new MapPoint(100, 500)),
            "draft-contact third point");
        string beforeMove = JsonSerializer.Serialize(draftContact.GetSnapshot());
        LinePointMovePreview badMove = draftContact.PreviewMoveLinePoint(
            1,
            new MapPoint(300, 110));
        Check(!badMove.Accepted && badMove.Error == ConstructionError.ThirdNodeTouch,
            "moved pole contact must validate the whole candidate path");
        ConstructionCommandResult rejectedMove = draftContact.MoveLinePoint(
            1,
            new MapPoint(300, 110));
        Check(!rejectedMove.Accepted && rejectedMove.Error == badMove.Error,
            "move preview/command error mismatch");
        Equal(beforeMove, JsonSerializer.Serialize(draftContact.GetSnapshot()),
            "rejected pole move changed the draft");
    }

    private void CheckConstructionLifecycle()
    {
        SpatialWorldDefinition nodeWorld = World([Node("A", 100, 100)]);
        var nodeSession = new ConstructionSession(nodeWorld);
        Accepted(nodeSession.SetNodeDraft(SubstationClassId, new MapPoint(500, 500)),
            "set node draft");
        Accepted(nodeSession.SetNodeDraft(SubstationClassId, new MapPoint(600, 500)),
            "move node draft");
        Equal(1, nodeSession.GetSnapshot().World.Nodes.Count,
            "drafting must not create a node");
        Accepted(nodeSession.CancelNodeDraft(), "cancel node draft");
        Equal(ConstructionPhase.Ready, nodeSession.GetSnapshot().Phase,
            "node cancel phase");

        Accepted(nodeSession.SetNodeDraft(SubstationClassId, new MapPoint(600, 500)),
            "set final node draft");
        ConstructionQuote nodeQuote = nodeSession.PreviewNodeOrder();
        Quote(nodeQuote, 100, 10, 10, "node quote");
        Accepted(nodeSession.OrderNode(), "order node");
        ConstructionSnapshot nodeOrdered = nodeSession.GetSnapshot();
        Equal(ConstructionPhase.NodeBuilding, nodeOrdered.Phase, "node building phase");
        Check(!nodeOrdered.World.Nodes.Single(node => node.NodeId == "PLAYER_SUBSTATION_1").Commissioned,
            "ordered node commissioned before completion");
        Accepted(nodeSession.AdvanceToConstructionCompletion(), "complete node");
        ConstructionSnapshot nodeComplete = nodeSession.GetSnapshot();
        Equal(10L, nodeComplete.Minute, "node completion minute");
        Check(nodeComplete.World.Nodes.Single(node => node.NodeId == "PLAYER_SUBSTATION_1").Commissioned,
            "completed node remains uncommissioned");

        SpatialWorldDefinition lineWorld = World(
            [Node("A", 100, 100), Node("B", 700, 100, LoadClassId)],
            risks: [Risk("WORK_RISK", 350, 50, 450, 150)]);
        var lineSession = new ConstructionSession(lineWorld);
        Accepted(lineSession.StartLineDraft("A", LineClassId, PoleClassId),
            "start explicit-node line draft");
        LinePointPreview pointPreview = lineSession.PreviewLinePoint(new MapPoint(400, 100));
        Check(pointPreview.Accepted, "valid intermediate preview rejected");
        SequenceEqual(["WORK_RISK"], pointPreview.RiskAreaIds,
            "intermediate risk exposure");
        Accepted(lineSession.AddLinePoint(new MapPoint(400, 100)), "add intermediate point");
        Accepted(lineSession.FinishLineDraft("B"), "finish at explicit node");
        LinePointMovePreview movePreview = lineSession.PreviewMoveLinePoint(
            0,
            new MapPoint(400, 120));
        Check(movePreview.Accepted, "valid completed-draft pole move was rejected");
        Equal(301L, movePreview.PreviousSegmentLengthUnit,
            "moved pole previous segment length");
        Equal(301L, movePreview.NextSegmentLengthUnit,
            "moved pole next segment length");
        Accepted(lineSession.MoveLinePoint(0, new MapPoint(400, 120)),
            "move pole before order");
        Equal(new MapPoint(400, 120), lineSession.GetSnapshot().LineDraft!.IntermediatePoints[0],
            "moved pole coordinate");
        Accepted(lineSession.MoveLinePoint(0, new MapPoint(400, 100)),
            "restore pole before undo checks");
        Accepted(lineSession.UndoLinePoint(), "undo explicit end");
        Error(ConstructionError.DraftIncomplete, lineSession.PreviewLineOrder(),
            "unfinished draft quote");
        Accepted(lineSession.UndoLinePoint(), "undo intermediate point");
        Equal(0, lineSession.GetSnapshot().LineDraft!.IntermediatePoints.Count,
            "intermediate undo count");
        ConstructionCommandResult emptyUndo = lineSession.UndoLinePoint();
        Check(!emptyUndo.Accepted && emptyUndo.Error == ConstructionError.NothingToUndo,
            "empty line undo must be typed rejection");
        Accepted(lineSession.CancelLineDraft(), "cancel line draft");

        Accepted(lineSession.StartLineDraft("A", LineClassId, PoleClassId),
            "restart line draft");
        Accepted(lineSession.AddLinePoint(new MapPoint(400, 100)),
            "add final intermediate point");
        Accepted(lineSession.FinishLineDraft("B"), "finish final line draft");
        ConstructionQuote lineQuote = lineSession.PreviewLineOrder();
        Quote(lineQuote, 80, 15, 15, "segmented line quote");
        SequenceEqual(["WORK_RISK"], lineQuote.RiskAreaIds, "line quote risk exposure");
        Accepted(lineSession.OrderLine(), "order line");
        ConstructionSnapshot ordered = lineSession.GetSnapshot();
        Equal(ConstructionPhase.LineBuilding, ordered.Phase, "line building phase");
        Equal(3, ordered.World.Nodes.Count, "atomic order node count");
        Equal(2, ordered.World.Edges.Count, "atomic order edge count");
        Check(ordered.ActiveConstruction!.NodeIds.All(id =>
                !ordered.World.Nodes.Single(node => node.NodeId == id).Commissioned),
            "ordered poles must all remain uncommissioned");
        Check(ordered.ActiveConstruction.EdgeIds.All(id =>
                !ordered.World.Edges.Single(edge => edge.EdgeId == id).Commissioned),
            "ordered edges must all remain uncommissioned");
        SequenceEqual(["WORK_RISK"], ordered.ActiveConstruction.RiskAreaIds,
            "ordered risk exposure");

        Accepted(lineSession.AdvanceToConstructionCompletion(), "atomic line completion");
        ConstructionSnapshot complete = lineSession.GetSnapshot();
        Equal(15L, complete.Minute, "line completion minute");
        Equal(ConstructionPhase.Ready, complete.Phase, "line completion phase");
        Check(complete.World.Nodes.Where(node => node.NodeId.StartsWith("PLAYER_", StringComparison.Ordinal))
                .All(node => node.Commissioned),
            "line completion left a player node uncommissioned");
        Check(complete.World.Edges.All(edge => edge.Commissioned),
            "line completion left an edge uncommissioned");

        SpatialWorldDefinition shortSegmentsWorld = World(
            [Node("A", 100, 100), Node("B", 200, 100, LoadClassId)]);
        var shortSegments = new ConstructionSession(shortSegmentsWorld);
        Accepted(shortSegments.StartLineDraft("A", LineClassId, PoleClassId),
            "start short-segment quote");
        Accepted(shortSegments.AddLinePoint(new MapPoint(150, 100)),
            "add short-segment pole");
        Accepted(shortSegments.FinishLineDraft("B"), "finish short-segment line");
        Quote(shortSegments.PreviewLineOrder(), 55, 5, 5,
            "path-level design-unit rounding");
    }

    private void CheckRejectedInvarianceAndDeterminism()
    {
        SpatialWorldDefinition world = World(
            [Node("A", 100, 100), Node("B", 500, 300, LoadClassId)]);
        var session = new ConstructionSession(world);
        AssertRejectedPreserves(
            session,
            () => session.SetNodeDraft(SubstationClassId, new MapPoint(5, 5)),
            ConstructionError.OutsideBounds,
            "rejected node set");

        Accepted(session.StartLineDraft("A", LineClassId, PoleClassId), "start invariant draft");
        AssertRejectedPreserves(
            session,
            () => session.MoveLinePoint(0, new MapPoint(200, 200)),
            ConstructionError.InvalidPointIndex,
            "invalid pole index");
        LinePointPreview preview = session.PreviewLinePoint(new MapPoint(100, 100));
        AssertRejectedPreserves(
            session,
            () => session.AddLinePoint(new MapPoint(100, 100)),
            preview.Error!.Value,
            "preview/command zero-length parity");
        AssertRejectedPreserves(
            session,
            () => session.FinishLineDraft("MISSING"),
            ConstructionError.EndpointNotFound,
            "unknown explicit endpoint");
        AssertRejectedPreserves(
            session,
            session.OrderLine,
            ConstructionError.DraftIncomplete,
            "incomplete line order");

        string first = ExecuteReplay(world);
        string second = ExecuteReplay(world);
        Equal(first, second, "identical command replay must be deterministic");
    }

    private void CheckCrossingNonConnectionAndReplay()
    {
        SpatialWorldDefinition crossingWorld = World(
        [
            Node("A", 100, 500),
            Node("B", 700, 500, LoadClassId),
            Node("C", 400, 200),
            Node("D", 400, 800, LoadClassId),
        ],
        [Edge("VERTICAL", "C", "D")]);
        SpatialWorldLoader.Validate(crossingWorld);

        var session = new ConstructionSession(crossingWorld);
        Accepted(session.StartLineDraft("A", LineClassId, PoleClassId),
            "start crossing line");
        Check(session.PreviewLineFinish("B").Accepted,
            "noncollinear line crossing was rejected");
        Accepted(session.FinishLineDraft("B"), "finish crossing line");
        Accepted(session.OrderLine(), "order crossing line");
        Accepted(session.AdvanceToConstructionCompletion(), "complete crossing line");
        SpatialWorldDefinition completed = session.GetSnapshot().World;
        Equal(4, completed.Nodes.Count, "crossing created an implicit node");
        Check(!completed.Nodes.Any(node => node.Position == new MapPoint(400, 500)),
            "crossing intersection became a node");
        Check(!Reachable(completed, "A", "C"),
            "crossing lines became electrically connected");

        SpatialWorldDefinition replayWorld = World(
            [Node("A", 100, 100), Node("B", 500, 300, LoadClassId)]);
        ConstructionSnapshot replay = ExecuteReplaySnapshot(replayWorld);
        SpatialNodeDefinition pole = replay.World.Nodes.Single(node =>
            node.NodeId == "PLAYER_POLE_1");
        Equal(new MapPoint(250, 200), pole.Position,
            "replay must preserve exact fixed-point coordinates");
        Equal("A", replay.World.Edges.Single(edge => edge.EdgeId == "PLAYER_EDGE_1").FromNodeId,
            "replay first edge start identifier");
        Equal("PLAYER_POLE_1",
            replay.World.Edges.Single(edge => edge.EdgeId == "PLAYER_EDGE_1").ToNodeId,
            "replay first edge end identifier");
    }

    private string ExecuteReplay(SpatialWorldDefinition world) =>
        JsonSerializer.Serialize(ExecuteReplaySnapshot(world));

    private ConstructionSnapshot ExecuteReplaySnapshot(SpatialWorldDefinition world)
    {
        var session = new ConstructionSession(world);
        Accepted(session.StartLineDraft("A", LineClassId, PoleClassId), "replay start");
        Accepted(session.AddLinePoint(new MapPoint(250, 200)), "replay add point");
        Accepted(session.FinishLineDraft("B"), "replay finish");
        ConstructionQuote quote = session.PreviewLineOrder();
        Quote(quote, 75, 13, 13, "replay quote");
        Accepted(session.OrderLine(), "replay order");
        Accepted(session.AdvanceToConstructionCompletion(), "replay completion");
        return session.GetSnapshot();
    }

    private void AssertRejectedPreserves(
        ConstructionSession session,
        Func<ConstructionCommandResult> command,
        ConstructionError expected,
        string label)
    {
        string before = JsonSerializer.Serialize(session.GetSnapshot());
        ConstructionCommandResult result = command();
        Check(!result.Accepted, $"{label}: command was accepted");
        Equal(expected, result.Error, $"{label}: typed error");
        Equal(before, JsonSerializer.Serialize(result.Snapshot),
            $"{label}: returned snapshot changed");
        Equal(before, JsonSerializer.Serialize(session.GetSnapshot()),
            $"{label}: session state changed");
    }

    private CommercialCoreSliceRun EnterCommercialMain(string label) =>
        EnterCommercialMain(_coreSlice, _commercialWorld, label);

    private CommercialCoreSliceRun EnterCommercialMain(
        CommercialCoreSliceDefinition definition,
        CommercialWorldDefinition world,
        string label)
    {
        var run = new CommercialCoreSliceRun(definition, world);
        CommercialCoreSnapshot preludeStart = run.GetSnapshot();
        Equal("FIRST_LIGHT_PRELUDE_SEGMENT", preludeStart.SegmentId,
            $"{label}: prelude segment");
        Equal(8, preludeStart.Construction.World.Nodes.Count,
            $"{label}: sparse prelude terminal and substation count");
        Equal(1, preludeStart.Construction.World.Edges.Count,
            $"{label}: sparse prelude service edge count");
        Check(!preludeStart.CanApprove,
            $"{label}: prelude approved without construction work");
        Equal(ThermalFailureKind.NoTopologyPath, preludeStart.FirstBlockingFailure!.Kind,
            $"{label}: prelude work-required failure");

        CommercialCoreProjectQuote preludeQuote = DraftCoreLine(
            run,
            "WEST_SOURCE_NODE",
            "STANDARD_LINE",
            "STANDARD_POLE",
            [
                new MapPoint(800, 650),
                new MapPoint(1050, 650),
                new MapPoint(1600, 650),
                new MapPoint(2050, 500),
            ],
            "EAST_SUBSTATION",
            $"{label}: first-light line");
        CoreQuote(preludeQuote, 320000, 128, 1148,
            $"{label}: first-light quote");
        CommercialCoreSnapshot preludeDraft = run.GetSnapshot();
        Check(preludeDraft.ProjectionIncludesCurrentConstruction,
            $"{label}: first-light draft projection");
        ThermalIntervalEvaluation preludePreview = Projection(
            preludeDraft,
            "FIRST_LIGHT_SUPPLY").Evaluation;
        Equal(800L, Supply(preludePreview, "EAST_RESIDENTIAL").DeliveredKw,
            $"{label}: first-light preview delivery");
        CoreAccepted(run, CommercialCoreCommand.OrderLine(),
            $"{label}: order first-light line");
        CoreAccepted(run, CommercialCoreCommand.AdvanceConstruction(),
            $"{label}: complete first-light line");
        Check(run.GetSnapshot().CanApprove,
            $"{label}: completed first-light route cannot approve");
        CoreAccepted(run, CommercialCoreCommand.ApproveDecisionWindow(),
            $"{label}: approve first-light prelude");

        CommercialCoreSnapshot main = run.GetSnapshot();
        Equal("CHAPTER_FIVE_SEGMENT", main.SegmentId,
            $"{label}: main segment transition");
        Equal("WHOSE_MARGIN", main.Chapter.ChapterId,
            $"{label}: main chapter transition");
        Equal(19, main.Construction.World.Nodes.Count,
            $"{label}: independent main seed node count");
        Equal(_commercialWorld.Edges.Count + 1, main.Construction.World.Edges.Count,
            $"{label}: independent main seed edge count");
        Check(!main.Construction.World.Nodes.Any(item =>
                item.NodeId.StartsWith("PLAYER_", StringComparison.Ordinal)),
            $"{label}: prelude nodes leaked into main seed");
        Check(!main.Construction.World.Edges.Any(item =>
                item.EdgeId.StartsWith("PLAYER_", StringComparison.Ordinal)),
            $"{label}: prelude edges leaked into main seed");
        Equal(700000L, main.CashUnit, $"{label}: independent main seed cash");
        Equal(6840L, main.Minute, $"{label}: independent main seed minute");
        Equal(CommercialPromiseDecision.Unset, main.PromiseDecision,
            $"{label}: independent main promise state");
        Equal(0, main.ThermalState.CoolingAssetIds.Count,
            $"{label}: independent main thermal state");
        Equal("FIRST_LIGHT_PRELUDE", main.LastOutcome!.ChapterId,
            $"{label}: prelude typed outcome");
        Equal(preludePreview, main.LastOutcome.Phases.Single().Evaluation,
            $"{label}: prelude preview must equal approved result");
        Equal(680000L, main.LastOutcome.EndingCashUnit,
            $"{label}: prelude ending cash fact");
        Equal(1148L, main.LastOutcome.EndingMinute,
            $"{label}: prelude ending minute fact");
        return run;
    }

    private CommercialCoreProjectQuote DraftShortSharedFactoryLine(
        CommercialCoreSliceRun run) =>
        DraftCoreLine(
            run,
            "BRIDGE_SOUTH",
            "STANDARD_LINE",
            "STANDARD_POLE",
            [new MapPoint(1950, 1750)],
            "SOUTH_SUBSTATION",
            "short shared factory line");

    private CommercialCoreProjectQuote DraftLongSeparateFactoryLine(
        CommercialCoreSliceRun run) =>
        DraftCoreLine(
            run,
            "SOUTH_SOURCE_NODE",
            "REINFORCED_LINE",
            "REINFORCED_POLE",
            [
                new MapPoint(750, 1900),
                new MapPoint(1140, 1850),
                new MapPoint(1780, 1900),
                new MapPoint(2250, 1950),
            ],
            "SOUTH_SUBSTATION",
            "long separate factory line");

    private CommercialCoreProjectQuote DraftUnsafeSharedFactoryLine(
        CommercialCoreSliceRun run) =>
        DraftCoreLine(
            run,
            "SOUTH_SUBSTATION",
            "STANDARD_LINE",
            "STANDARD_POLE",
            [new MapPoint(2100, 1800)],
            "FACTORY_TERMINAL",
            "unsafe safety-shared factory line");

    private CommercialCoreProjectQuote DraftCoreLine(
        CommercialCoreSliceRun run,
        string startNodeId,
        string lineClassId,
        string poleClassId,
        IReadOnlyList<MapPoint> points,
        string endNodeId,
        string label)
    {
        LineStartPreview startPreview = run.PreviewLineStart(
            startNodeId,
            lineClassId,
            poleClassId);
        Check(startPreview.Accepted, $"{label}: start preview {startPreview.Error}");
        CoreAccepted(
            run,
            CommercialCoreCommand.StartLineDraft(startNodeId, lineClassId, poleClassId),
            $"{label}: start");
        for (int index = 0; index < points.Count; index++)
        {
            LinePointPreview pointPreview = run.PreviewLinePoint(points[index]);
            Check(pointPreview.Accepted,
                $"{label}: point {index} preview {pointPreview.Error}");
            CoreAccepted(run, CommercialCoreCommand.AddLinePoint(points[index]),
                $"{label}: point {index}");
        }
        LineFinishPreview finishPreview = run.PreviewLineFinish(endNodeId);
        Check(finishPreview.Accepted, $"{label}: finish preview {finishPreview.Error}");
        CoreAccepted(run, CommercialCoreCommand.FinishLineDraft(endNodeId),
            $"{label}: finish");
        return run.PreviewLineOrder();
    }

    private CommercialPhaseProjection Projection(
        CommercialCoreSnapshot snapshot,
        string phaseId) =>
        snapshot.Projections.Single(item => item.Phase.PhaseId == phaseId);

    private void CoreAccepted(
        CommercialCoreSliceRun run,
        CommercialCoreCommand command,
        string label)
    {
        CommercialCoreCommandResult result = run.Execute(command);
        Check(result.Accepted, $"{label}: rejected with {result.Error}/{result.ConstructionError}");
        Check(result.Error is null && result.ConstructionError is null,
            $"{label}: accepted result retained an error");
    }

    private void CoreRejected(
        CommercialCoreSliceRun run,
        CommercialCoreCommand command,
        CommercialCoreRunError expected,
        string label)
    {
        CommercialCoreCommandResult result = run.Execute(command);
        Check(!result.Accepted, $"{label}: command was accepted");
        Equal(expected, result.Error, $"{label}: typed error");
    }

    private void CoreQuote(
        CommercialCoreProjectQuote quote,
        long cost,
        long minutes,
        long completion,
        string label)
    {
        Check(quote.Accepted, $"{label}: rejected with {quote.Error}/{quote.ConstructionError}");
        Check(quote.Error is null && quote.ConstructionError is null,
            $"{label}: accepted quote retained an error");
        Equal(cost, quote.CostCashUnit, $"{label}: cost");
        Equal(minutes, quote.BuildMinutes, $"{label}: build minutes");
        Equal(completion, quote.CompletionMinute, $"{label}: completion minute");
    }

    private static string CoreStateJson(CommercialCoreSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot);

    private static string LowerSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static CommercialWorldDefinition ThermalWorld(
        IReadOnlyList<SpatialNodeDefinition> nodes,
        IReadOnlyList<SpatialEdgeDefinition> edges,
        IReadOnlyList<CommercialSourceDefinition> sources,
        IReadOnlyList<CommercialLoadDefinition> loads,
        IReadOnlyList<CommercialNodeClassDefinition>? nodeClasses = null,
        IReadOnlyList<CommercialLineClassDefinition>? lineClasses = null)
    {
        var world = new CommercialWorldDefinition(
            CommercialWorldLoader.SupportedSchemaVersion,
            "THERMAL_CHECK_WORLD",
            "열 검사 세계",
            100,
            new MapBounds(0, 0, 2200, 2200),
            10000,
            nodeClasses ?? ThermalNodeClasses(),
            lineClasses is null
                ? [
                    ThermalLineClass(LineClassId, 100, 150),
                    ThermalLineClass(ServiceLineClassId, 1000, 1500),
                ]
                : [
                    .. lineClasses,
                    ThermalLineClass(ServiceLineClassId, 1000, 1500),
                ],
            Array.Empty<TerrainPolygonDefinition>(),
            Array.Empty<SpatialRiskAreaDefinition>(),
            nodes,
            edges,
            sources,
            loads);
        CommercialWorldLoader.Validate(world);
        return world;
    }

    private static IReadOnlyList<CommercialNodeClassDefinition> ThermalNodeClasses() =>
    [
        new(SourceClassId, "검사 발전 접속점", SpatialNodeKind.SourceTerminal,
            10, 6, 0, 0, null),
        new(LoadClassId, "검사 부하 접속점", SpatialNodeKind.DedicatedLoadTerminal,
            10, 6, 0, 0, null),
        new(PoleClassId, "검사 전신주 접속부", SpatialNodeKind.Pole,
            10, 6, 50, 3, new ThermalLimit(100, 150)),
        new(SubstationClassId, "검사 변전소", SpatialNodeKind.Substation,
            20, 6, 100, 10, new ThermalLimit(1000, 1500), 5000),
    ];

    private static CommercialLineClassDefinition ThermalLineClass(
        string classId,
        long continuousKw,
        long emergencyKw) =>
        new(
            classId,
            classId,
            2000,
            5,
            2,
            new ThermalLimit(continuousKw, emergencyKw));

    private static SpatialEdgeDefinition ThermalEdge(
        string edgeId,
        string lineClassId,
        string fromNodeId,
        string toNodeId) =>
        new(edgeId, lineClassId, fromNodeId, toNodeId, true);

    private static CommercialSourceDefinition ThermalSource(
        string sourceId,
        string nodeId,
        long capacityKw,
        int dispatchOrder) =>
        new(sourceId, sourceId, nodeId, capacityKw, dispatchOrder);

    private static CommercialLoadDefinition ThermalLoad(string loadId, string nodeId) =>
        new(loadId, loadId, nodeId);

    private static ThermalLoadRequest LoadRequest(
        string loadId,
        long demandKw,
        ThermalPermission permission) =>
        new(loadId, demandKw, permission);

    private static ThermalIntervalRequest Interval(
        string intervalId,
        IReadOnlyList<ThermalLoadRequest> loads,
        IReadOnlyList<string>? unavailableNodes = null,
        IReadOnlyList<string>? unavailableEdges = null,
        IReadOnlyList<ThermalLimitOverride>? overrides = null) =>
        new(
            intervalId,
            loads,
            unavailableNodes ?? Array.Empty<string>(),
            unavailableEdges ?? Array.Empty<string>(),
            overrides ?? Array.Empty<ThermalLimitOverride>());

    private static ThermalSequenceRequest CoolingSequence() =>
        new(
        [
            Interval(
                "EMERGENCY",
                [LoadRequest("LOAD", 120, ThermalPermission.EmergencyAllowed)]),
            Interval(
                "PROTECTIVE_OUTAGE",
                [LoadRequest("LOAD", 100, ThermalPermission.ContinuousOnly)]),
            Interval(
                "RETURN",
                [LoadRequest("LOAD", 100, ThermalPermission.ContinuousOnly)]),
        ]);

    private static ThermalLoadSupply Supply(
        ThermalIntervalEvaluation evaluation,
        string loadId) =>
        evaluation.Loads.Single(item =>
            string.Equals(item.LoadId, loadId, StringComparison.Ordinal));

    private static ThermalAssetUsage Asset(
        ThermalIntervalEvaluation evaluation,
        string assetId) =>
        evaluation.Assets.Single(item =>
            string.Equals(item.AssetId, assetId, StringComparison.Ordinal));

    private void Failure(
        ThermalLoadSupply supply,
        ThermalFailureKind kind,
        string? assetId,
        long requiredKw,
        long availableKw,
        string label)
    {
        Equal(0L, supply.DeliveredKw, $"{label}: delivered power");
        Check(supply.Failure is not null, $"{label}: missing typed failure");
        Equal(kind, supply.Failure!.Kind, $"{label}: failure kind");
        Equal(assetId, supply.Failure.AssetId, $"{label}: failure asset");
        Equal(requiredKw, supply.Failure.RequiredKw, $"{label}: required power");
        Equal(availableKw, supply.Failure.AvailableKw, $"{label}: available power");
    }

    private static SpatialWorldDefinition World(
        IReadOnlyList<SpatialNodeDefinition> nodes,
        IReadOnlyList<SpatialEdgeDefinition>? edges = null,
        IReadOnlyList<TerrainPolygonDefinition>? terrain = null,
        IReadOnlyList<SpatialRiskAreaDefinition>? risks = null) =>
        new(
            SpatialWorldLoader.SupportedSchemaVersion,
            "COMMERCIAL_CHECK_WORLD",
            "상용 검사 세계",
            100,
            new MapBounds(0, 0, 2000, 2000),
            10000,
            NodeClasses(),
            LineClasses(),
            terrain ?? Array.Empty<TerrainPolygonDefinition>(),
            risks ?? Array.Empty<SpatialRiskAreaDefinition>(),
            nodes,
            edges ?? Array.Empty<SpatialEdgeDefinition>());

    private static IReadOnlyList<SpatialNodeClassDefinition> NodeClasses() =>
    [
        new(SourceClassId, "검사 발전 접속점", SpatialNodeKind.SourceTerminal,
            10, 6, 0, 0),
        new(LoadClassId, "검사 부하 접속점", SpatialNodeKind.DedicatedLoadTerminal,
            10, 6, 0, 0),
        new(PoleClassId, "검사 전신주", SpatialNodeKind.Pole,
            10, 4, 50, 3),
        new(SubstationClassId, "검사 변전소", SpatialNodeKind.Substation,
            20, 4, 100, 10),
    ];

    private static IReadOnlyList<SpatialLineClassDefinition> LineClasses() =>
    [
        new(LineClassId, "검사 선로", 600, 5, 2),
    ];

    private static SpatialNodeDefinition Node(
        string id,
        int x,
        int y,
        string classId = SourceClassId) =>
        new(id, classId, id, new MapPoint(x, y), true, false);

    private static SpatialEdgeDefinition Edge(string id, string from, string to) =>
        new(id, LineClassId, from, to, true);

    private static LineDraftSnapshot Draft(string start) =>
        new(start, LineClassId, PoleClassId, Array.Empty<MapPoint>(), null);

    private static TerrainPolygonDefinition Terrain(
        string id,
        TerrainKind kind,
        int minX,
        int minY,
        int maxX,
        int maxY) =>
        new(id, id, kind, Rectangle(minX, minY, maxX, maxY));

    private static SpatialRiskAreaDefinition Risk(
        string id,
        int minX,
        int minY,
        int maxX,
        int maxY) =>
        new(id, id, Rectangle(minX, minY, maxX, maxY));

    private static IReadOnlyList<MapPoint> Rectangle(
        int minX,
        int minY,
        int maxX,
        int maxY) =>
    [
        new(minX, minY),
        new(maxX, minY),
        new(maxX, maxY),
        new(minX, maxY),
    ];

    private static bool Reachable(SpatialWorldDefinition world, string start, string target)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal) { start };
        var pending = new Queue<string>();
        pending.Enqueue(start);
        while (pending.Count != 0)
        {
            string current = pending.Dequeue();
            foreach (SpatialEdgeDefinition edge in world.Edges.Where(edge =>
                         edge.FromNodeId == current || edge.ToNodeId == current))
            {
                string next = edge.FromNodeId == current ? edge.ToNodeId : edge.FromNodeId;
                if (reached.Add(next))
                {
                    pending.Enqueue(next);
                }
            }
        }
        return reached.Contains(target);
    }

    private void ExpectLoaderRejected(string label, Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(_fixtureJson)!.AsObject();
        mutate(root);
        ExpectLoaderRejected(label, root.ToJsonString());
    }

    private void ExpectLoaderRejected(string label, string json) =>
        ExpectThrows<SpatialWorldValidationException>(
            () => SpatialWorldLoader.Load(json),
            label);

    private void ExpectLoaderRejected(string label, byte[] bytes) =>
        ExpectThrows<SpatialWorldValidationException>(
            () => SpatialWorldLoader.Load(bytes),
            label);

    private void ExpectCommercialLoaderRejected(string label, Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(_worldJson)!.AsObject();
        mutate(root);
        ExpectCommercialLoaderRejected(label, root.ToJsonString());
    }

    private void ExpectCommercialLoaderRejected(string label, string json) =>
        ExpectThrows<CommercialWorldValidationException>(
            () => CommercialWorldLoader.Load(json),
            label);

    private void ExpectCoreSliceLoaderRejected(string label, Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(_coreSliceJson)!.AsObject();
        mutate(root);
        ExpectCoreSliceLoaderRejected(label, root.ToJsonString());
    }

    private void ExpectCoreSliceLoaderRejected(string label, string json) =>
        ExpectThrows<CommercialCoreSliceValidationException>(
            () => CommercialCoreSliceLoader.Load(json, _commercialWorld),
            label);

    private void ExpectCampaignLoaderRejected(string label, Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(_campaignJson)!.AsObject();
        mutate(root);
        ExpectCampaignLoaderRejected(label, root.ToJsonString());
    }

    private void ExpectCampaignLoaderRejected(string label, string json) =>
        ExpectThrows<CommercialCampaignValidationException>(
            () => CommercialCampaignLoader.Load(json, _commercialWorld),
            label);

    private void ExpectThrows<T>(Action body, string label)
        where T : Exception
    {
        try
        {
            body();
        }
        catch (T)
        {
            _assertionCount++;
            return;
        }
        throw new InvalidOperationException($"{label}: expected {typeof(T).Name}");
    }

    private static JsonObject Object(JsonNode node) => node.AsObject();

    private static JsonArray JsonArrayProperty(JsonObject parent, string property) =>
        parent[property]!.AsArray();

    private static JsonObject PointJson(int x, int y) =>
        new() { ["xUnit"] = x, ["yUnit"] = y };

    private static JsonObject ThermalLimitJson(long continuousKw, long emergencyKw) =>
        new() { ["continuousKw"] = continuousKw, ["emergencyKw"] = emergencyKw };

    private static JsonObject NodeJson(
        string id,
        string classId,
        int x,
        int y,
        bool authoredFoundation) =>
        new()
        {
            ["nodeId"] = id,
            ["classId"] = classId,
            ["displayName"] = id,
            ["position"] = PointJson(x, y),
            ["commissioned"] = true,
            ["authoredFoundation"] = authoredFoundation,
        };

    private static JsonObject EdgeJson(
        string id,
        string classId,
        string from,
        string to) =>
        new()
        {
            ["edgeId"] = id,
            ["lineClassId"] = classId,
            ["fromNodeId"] = from,
            ["toNodeId"] = to,
            ["commissioned"] = true,
        };

    private void Error(ConstructionError expected, NodePlacementPreview actual, string label)
    {
        Check(!actual.Accepted, $"{label}: preview was accepted");
        Equal(expected, actual.Error, $"{label}: error");
    }

    private void Error(ConstructionError expected, LinePointPreview actual, string label)
    {
        Check(!actual.Accepted, $"{label}: preview was accepted");
        Equal(expected, actual.Error, $"{label}: error");
    }

    private void Error(ConstructionError expected, LineFinishPreview actual, string label)
    {
        Check(!actual.Accepted, $"{label}: preview was accepted");
        Equal(expected, actual.Error, $"{label}: error");
    }

    private void Error(ConstructionError expected, ConstructionQuote actual, string label)
    {
        Check(!actual.Accepted, $"{label}: quote was accepted");
        Equal(expected, actual.Error, $"{label}: error");
    }

    private void Accepted(ConstructionCommandResult result, string label)
    {
        Check(result.Accepted, $"{label}: rejected with {result.Error}");
        Check(result.Error is null, $"{label}: accepted result has an error");
    }

    private void Quote(
        ConstructionQuote quote,
        long cost,
        long minutes,
        long completion,
        string label)
    {
        Check(quote.Accepted, $"{label}: rejected with {quote.Error}");
        Equal(cost, quote.CostCashUnit, $"{label}: cost");
        Equal(minutes, quote.BuildMinutes, $"{label}: build minutes");
        Equal(completion, quote.CompletionMinute, $"{label}: completion minute");
    }

    private void SequenceEqual<T>(
        IReadOnlyList<T> expected,
        IReadOnlyList<T> actual,
        string label)
    {
        Check(expected.SequenceEqual(actual),
            $"{label}: expected [{string.Join(", ", expected)}], " +
            $"actual [{string.Join(", ", actual)}]");
    }

    private void Equal<T>(T expected, T actual, string label)
    {
        Check(EqualityComparer<T>.Default.Equals(expected, actual),
            $"{label}: expected {expected}, actual {actual}");
    }

    private void Check(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
