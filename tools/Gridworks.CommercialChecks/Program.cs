using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using Gridworks.Core.Release.V2;

namespace Gridworks.CommercialChecks;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            (string spatialPath, string worldPath, string coreSlicePath) =
                ResolveFixturePaths(args);
            return new CommercialChecks(spatialPath, worldPath, coreSlicePath).Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL startup: {exception.Message}");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static (string SpatialPath, string WorldPath, string CoreSlicePath)
        ResolveFixturePaths(string[] args)
    {
        if (args.Length > 3)
        {
            throw new ArgumentException(
                "usage: Gridworks.CommercialChecks [commercial-spatial-json] " +
                "[release-world-v2-json] [commercial-core-slice-v1-json]");
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
        string coreSlicePath = args.Length == 3
            ? Path.GetFullPath(args[2])
            : Path.Combine(
                Path.GetDirectoryName(worldPath)!,
                "commercial-core-slice-v1.json");
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
        return (spatialPath, worldPath, coreSlicePath);
    }
}

internal sealed class CommercialChecks
{
    private const string SourceClassId = "CHECK_SOURCE";
    private const string LoadClassId = "CHECK_LOAD";
    private const string PoleClassId = "CHECK_POLE";
    private const string SubstationClassId = "CHECK_SUBSTATION";
    private const string LineClassId = "CHECK_LINE";

    private readonly byte[] _fixtureBytes;
    private readonly string _fixtureJson;
    private readonly SpatialWorldDefinition _fixture;
    private readonly byte[] _worldBytes;
    private readonly string _worldJson;
    private readonly CommercialWorldDefinition _commercialWorld;
    private readonly byte[] _coreSliceBytes;
    private readonly string _coreSliceJson;
    private readonly CommercialCoreSliceDefinition _coreSlice;
    private int _assertionCount;

    public CommercialChecks(string fixturePath, string worldPath, string coreSlicePath)
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
        Equal(4, _commercialWorld.Loads.Count, "commercial authored load count");
        Check(_commercialWorld.NodeClasses
                .Where(item => item.Kind is SpatialNodeKind.Pole or SpatialNodeKind.Substation)
                .All(item => item.ThermalLimit is not null),
            "thermal node class lacks a limit");
        Check(_commercialWorld.NodeClasses
                .Where(item => item.Kind is SpatialNodeKind.SourceTerminal or
                    SpatialNodeKind.DedicatedLoadTerminal)
                .All(item => item.ThermalLimit is null),
            "terminal class owns a thermal limit");

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
        Equal(6, preludeWorld.Nodes.Count,
            "prelude seed retains fixed terminals only");
        Equal(0, preludeWorld.Edges.Count,
            "prelude seed must not inherit the complete main network");
        Equal(_commercialWorld.Nodes.Count, mainWorld.Nodes.Count,
            "main seed world node count");
        Equal(_commercialWorld.Edges.Count, mainWorld.Edges.Count,
            "main seed retains the four-chapter-complete graph");
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
            [Node("S", 100, 100), Node("L", 500, 100, LoadClassId)],
            [ThermalEdge("E", LineClassId, "S", "L")],
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
            Node("P_SECONDARY", 100, 900, PoleClassId),
            Node("L", 900, 500, LoadClassId),
        ],
        [
            ThermalEdge("HOT_SHORT", "HOT_LINE", "S_PRIMARY", "L"),
            ThermalEdge("COOL_LONG_A", "COOL_LINE", "S_SECONDARY", "P_SECONDARY"),
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
        SequenceEqual(["COOL_LONG_A", "COOL_LONG_B"], allSourceSupply.PathEdgeIds,
            "all-source continuous route");

        CommercialWorldDefinition allPaths = ThermalWorld(
        [
            Node("S", 100, 500),
            Node("P", 500, 300, PoleClassId),
            Node("L", 900, 500, LoadClassId),
        ],
        [
            ThermalEdge("HOT_DIRECT", "HOT_LINE", "S", "L"),
            ThermalEdge("COOL_A", "COOL_LINE", "S", "P"),
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
        SequenceEqual(["COOL_A", "COOL_B"], allPathSupply.PathEdgeIds,
            "long continuous path must beat short emergency path");

        CommercialWorldDefinition tie = ThermalWorld(
        [
            Node("S", 100, 500),
            Node("P_A", 500, 300, PoleClassId),
            Node("P_B", 500, 700, PoleClassId),
            Node("L", 900, 500, LoadClassId),
        ],
        [
            ThermalEdge("A_1", LineClassId, "S", "P_A"),
            ThermalEdge("A_2", LineClassId, "P_A", "L"),
            ThermalEdge("B_1", LineClassId, "S", "P_B"),
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
        SequenceEqual(["A_1", "A_2"], tieSupply.PathEdgeIds,
            "edge-ID deterministic tie-break");
        SequenceEqual(["S", "P_A", "L"], tieSupply.PathNodeIds,
            "node path for deterministic tie-break");
    }

    private void CheckThermalUnavailableAndOverrides()
    {
        CommercialWorldDefinition direct = ThermalWorld(
            [Node("S", 100, 100), Node("L", 500, 100, LoadClassId)],
            [ThermalEdge("E", LineClassId, "S", "L")],
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
                Node("L", 700, 300, LoadClassId),
            ],
            [
                ThermalEdge("E1", "COOL_LINE", "S", "P"),
                ThermalEdge("E2", "COOL_LINE", "P", "L"),
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
                Node("P", 300, 500, PoleClassId),
                Node("L", 500, 100, LoadClassId),
            ],
            [
                ThermalEdge("SHORT_UNAVAILABLE", "HIGH_LINE", "S", "L"),
                ThermalEdge("LONG_LIMIT_A", "LOW_LINE", "S", "P"),
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
        SequenceEqual(["LONG_LIMIT_A", "LONG_LIMIT_B"], competingFailure.PathEdgeIds,
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
            [Node("S", 100, 100), Node("L", 500, 100, LoadClassId)],
            [ThermalEdge("E", LineClassId, "S", "L")],
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
            [Node("S", 100, 100), Node("L", 500, 100, LoadClassId)],
            [ThermalEdge("E", LineClassId, "S", "L")],
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

    private void CheckCommercialCoreDesignsPreviewAndOutcomes()
    {
        CommercialCoreSliceRun shortRun = EnterCommercialMain("short shared design");
        CoreAccepted(
            shortRun,
            CommercialCoreCommand.SetPromiseDecision(CommercialPromiseDecision.Keep),
            "keep promise for short shared design");
        CommercialCoreProjectQuote shortQuote = DraftShortSharedFactoryLine(shortRun);
        CoreQuote(shortQuote, 110000, 44, 6884, "short shared design quote");
        SequenceEqual(["RIVER_FLOOD_ZONE"], shortQuote.RiskAreaIds,
            "short shared design risk exposure");
        CommercialCoreSnapshot shortDraft = shortRun.GetSnapshot();
        Check(shortDraft.ProjectionIncludesCurrentConstruction,
            "short design projection omitted the completed draft");
        CommercialPhaseProjection shortHotProjection = Projection(shortDraft, "HOT_EVENING");
        Check(shortHotProjection.SafetySatisfied && shortHotProjection.PromiseSatisfied,
            "short design preview did not satisfy current obligations");
        ThermalIntervalEvaluation shortPreview = shortHotProjection.Evaluation;
        CoreAccepted(shortRun, CommercialCoreCommand.OrderLine(), "order short shared design");
        CoreAccepted(shortRun, CommercialCoreCommand.AdvanceConstruction(),
            "complete short shared design");
        Equal(590000L, shortRun.GetSnapshot().CashUnit, "short design remaining cash");
        Equal(6884L, shortRun.GetSnapshot().Minute, "short design completion minute");
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
        Equal(500L, shortFactory.MinimumRemainingKw,
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
        CoreQuote(longQuote, 552000, 192, 7032, "long separate design quote");
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
        Equal(1800L, longFactoryPreview.MinimumRemainingKw,
            "long design continuous corridor margin");
        Check(longFactoryPreview.PathEdgeIds.SequenceEqual(
                ["PLAYER_EDGE_1", "PLAYER_EDGE_2", "PLAYER_EDGE_3", "PLAYER_EDGE_4", "PLAYER_EDGE_5"]),
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
        Equal(148000L, longComplete.LastOutcome.EndingCashUnit,
            "long design ending cash fact");
        Equal(7032L, longComplete.LastOutcome.EndingMinute,
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
        CoreQuote(DraftShortSharedFactoryLine(deadlineRun), 110000, 44, 6884,
            "deadline setup shared-line quote");
        CoreAccepted(deadlineRun, CommercialCoreCommand.OrderLine(),
            "order deadline setup shared line");
        CoreAccepted(deadlineRun, CommercialCoreCommand.AdvanceConstruction(),
            "complete deadline setup shared line");
        DraftLongSeparateFactoryLine(deadlineRun);
        CommercialCoreProjectQuote deadlineQuote = deadlineRun.PreviewLineOrder();
        Check(!deadlineQuote.Accepted, "over-deadline long route quote was accepted");
        Equal(CommercialCoreRunError.DeadlineExceeded, deadlineQuote.Error,
            "over-deadline route error");
        Equal(552000L, deadlineQuote.CostCashUnit,
            "deadline rejection must preserve the valid project cost");
        Equal(192L, deadlineQuote.BuildMinutes,
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
        Equal(6, preludeStart.Construction.World.Nodes.Count,
            $"{label}: sparse prelude terminal count");
        Equal(0, preludeStart.Construction.World.Edges.Count,
            $"{label}: prelude inherited main edges");
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
                new MapPoint(2100, 650),
            ],
            "EAST_RESIDENTIAL_TERMINAL",
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
        Equal(18, main.Construction.World.Nodes.Count,
            $"{label}: independent main seed node count");
        Equal(17, main.Construction.World.Edges.Count,
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
            "FACTORY_TERMINAL",
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
            "FACTORY_TERMINAL",
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
            lineClasses ?? [ThermalLineClass(LineClassId, 100, 150)],
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
            20, 6, 100, 10, new ThermalLimit(100, 150)),
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
