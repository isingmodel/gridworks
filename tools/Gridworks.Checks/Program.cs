using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gridworks.Core;

namespace Gridworks.Checks;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            string fixturePath = ResolveFixturePath(args);
            var checks = new Scope0BChecks(fixturePath);
            return checks.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL startup: {exception.Message}");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static string ResolveFixturePath(string[] args)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException("usage: Gridworks.Checks [fixture-json]");
        }

        string path = args.Length == 1
            ? args[0]
            : Path.Combine(Environment.CurrentDirectory, "data", "scope-0b-v1.json");
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Scope 0B fixture not found.", path);
        }

        return path;
    }
}

internal sealed class Scope0BChecks
{
    private const long CashDivisor = 60_000_000;

    private static readonly string[] SnapshotPropertyOrder =
    [
        "minute",
        "cash",
        "townProjectState",
        "corridorProjectState",
        "selectedCorridor",
        "commissionedEdgeIds",
        "eventRemovedEdgeIds",
        "activeLoadIds",
        "utilityPathByLoad",
        "hospitalInternalStage",
        "hospitalInternalRemainingKwMinute",
        "interval",
        "cumulative",
        "isComplete",
    ];

    private static readonly string[] SettlementPropertyOrder =
    [
        "revenueCashUnit",
        "gasCostCashUnit",
        "compensationCashUnit",
        "lostSalesCashUnit",
        "utilityDeliveredKwMinuteByLoad",
        "utilityUnservedKwMinuteByLoad",
        "gasInjectionKwMinute",
        "hospitalInternalUsedKwMinute",
        "hospitalP0UnservedKwMinute",
    ];

    private readonly string _fixtureJson;
    private readonly JsonObject _fixtureRoot;
    private readonly LoadedFixture _loadedFixture;
    private readonly string _hospitalLoadId;
    private readonly string _townLoadId;
    private int _assertionCount;

    public Scope0BChecks(string fixturePath)
    {
        _fixtureJson = File.ReadAllText(fixturePath, Encoding.UTF8);
        _fixtureRoot = ParseObject(_fixtureJson, "fixture root");
        _loadedFixture = FixtureLoader.Load(_fixtureJson);
        _hospitalLoadId = _loadedFixture.Scenario.HospitalInternalPower.LoadNodeId;
        _townLoadId = _loadedFixture.Scenario.Loads
            .Single(load => !string.Equals(load.NodeId, _hospitalLoadId, StringComparison.Ordinal))
            .NodeId;
    }

    public int Run()
    {
        (string Name, Action Body)[] suites =
        [
            ("strict-loader", CheckStrictLoader),
            ("full-boundary-oracles", CheckFullBoundaryOracles),
            ("pure-removal-queries", CheckPureQueries),
            ("rejected-command-invariance", CheckRejectedCommands),
            ("energy-cash-conservation", CheckEnergyAndCashConservation),
            ("deterministic-json-hash", CheckDeterminism),
            ("authored-path-no-reverse-feed", CheckNoInferredReverseFeed),
        ];

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
            Console.Error.WriteLine($"Gridworks Scope 0B checks: FAIL ({failures.Count}/{suites.Length} suites)");
            return 1;
        }

        Console.WriteLine($"Gridworks Scope 0B checks: PASS ({suites.Length} suites, {_assertionCount} assertions)");
        return 0;
    }

    private void CheckStrictLoader()
    {
        LoadedFixture loadedFromText = FixtureLoader.Load(_fixtureJson);
        LoadedFixture loadedFromBytes = FixtureLoader.Load(Encoding.UTF8.GetBytes(_fixtureJson));
        Check(loadedFromText.Scenario is not null, "text loader did not return scenario");
        Check(loadedFromText.Presentation is not null, "text loader did not return presentation");
        Check(loadedFromText.Oracle is not null, "text loader did not return oracle");
        Check(loadedFromBytes.Scenario is not null, "UTF-8 loader did not return scenario");

        string duplicateRootProperty =
            "{\"schemaVersion\":\"gridworks.scope0b.fixture.v1\"," + _fixtureJson.TrimStart()[1..];
        ExpectFixtureRejectedJson("duplicate JSON property", duplicateRootProperty);
        ExpectFixtureRejectedBytes("invalid UTF-8 JSON", [0xff, 0xfe, 0xfd]);

        ExpectFixtureRejected("unknown root field", root => root["unexpected"] = true);
        ExpectFixtureRejected(
            "unknown nested field",
            root =>
            {
                JsonObject economy = Object(root, "economy");
                economy["saleRateAlias"] = Long(economy, "saleRate");
            });
        ExpectFixtureRejected(
            "missing required field",
            root => Object(root, "economy").Remove("saleRate"));
        ExpectFixtureRejected(
            "null required object",
            root => root["economy"] = null);
        ExpectFixtureRejected(
            "duplicate node ID",
            root => Array(root, "nodes").Add(Array(root, "nodes")[0]!.DeepClone()));
        ExpectFixtureRejected(
            "null required reference",
            root => FindById(Array(root, "edges"), "MAIN_TRUNK")["fromNodeId"] = null);
        ExpectFixtureRejected(
            "broken edge reference",
            root => FindById(Array(root, "edges"), "MAIN_TRUNK")["fromNodeId"] = "MISSING_NODE");
        ExpectFixtureRejected(
            "layout includes non-corridor project",
            root =>
            {
                JsonArray variants = Array(Object(root, "presentation"), "layoutVariants");
                Array(FindById(variants, "ab"), "corridorProjectOrder")[1] =
                    "PROJECT_TOWN_FEEDER";
                Array(FindById(variants, "ba"), "corridorProjectOrder")[0] =
                    "PROJECT_TOWN_FEEDER";
            });
        ExpectFixtureRejected(
            "invalid enum",
            root => FindById(Array(root, "edges"), "TOWN_FEEDER")["initialConstructionState"] = "queued");
        ExpectFixtureRejected(
            "broken event reference",
            root => FindById(Array(root, "events"), "OLD_CORRIDOR_OUTAGE")["evaluationCaseId"] = "MISSING_CASE");
        ExpectFixtureRejected(
            "discontinuous authored path",
            root =>
            {
                JsonArray edgeIds = Array(FindById(Array(root, "permittedSupplyPaths"), "TOWN_PRIMARY_PATH"), "edgeIds");
                edgeIds[1] = "HOSPITAL_SERVICE";
            });
    }

    private void CheckFullBoundaryOracles()
    {
        foreach (CorridorDesign design in AllCorridors())
        {
            RouteRun run = RunRoute(_fixtureJson, design, compareOracle: true);
            string designId = CorridorId(design);

            foreach (CommandResult result in run.CommandResults)
            {
                Check(result.Accepted, $"{designId}: valid command was rejected");
                Check(result.ErrorCode is null, $"{designId}: accepted command returned an error code");
            }

            foreach (CommandResult result in run.CommandResults.Take(run.CommandResults.Count - 1))
            {
                Equal(0, result.BoundaryTrace.Count, $"{designId}: unexpected internal boundary trace");
            }

            CommandResult finalAdvance = run.CommandResults[^1];
            if (design == CorridorDesign.RiverParallel)
            {
                Equal(1, finalAdvance.BoundaryTrace.Count, "River final advance must expose exactly one internal boundary");
                BoundaryTrace trace = finalAdvance.BoundaryTrace[0];
                Equal("UPS_DEPLETED", trace.Id, "River internal boundary ID");
                AssertSnapshotMatchesOracle(
                    trace.Snapshot,
                    RouteOracleSnapshot(CorridorDesign.RiverParallel, "UPS_DEPLETED"),
                    RouteOracleState("RIVER_PARALLEL", "UPS_DEPLETED"),
                    "RIVER_PARALLEL/UPS_DEPLETED");
                Check(trace.Snapshot.Minute != finalAdvance.PublicSnapshot.Minute, "public target was duplicated in River trace");
            }
            else
            {
                Equal(0, finalAdvance.BoundaryTrace.Count, "North final advance must not fabricate an internal boundary");
            }

            Equal(
                SnapshotJson.Serialize(run.Snapshots[^1]),
                SnapshotJson.Serialize(run.Session.GetSnapshot()),
                $"{designId}: trace leaked into stored session state");
        }
    }

    private void CheckPureQueries()
    {
        foreach (CorridorDesign route in AllCorridors())
        {
            foreach (ReachableStage stage in Enum.GetValues<ReachableStage>())
            {
                GridworksSession session = SessionAt(route, stage);
                string before = SnapshotJson.Serialize(session.GetSnapshot());
                PublicSnapshot queryOne = session.GetSnapshot();
                PublicSnapshot queryTwo = session.GetSnapshot();
                Equal(before, SnapshotJson.Serialize(queryOne), $"{route}/{stage}: GetSnapshot mutated state");
                Equal(before, SnapshotJson.Serialize(queryTwo), $"{route}/{stage}: repeated GetSnapshot changed bytes");

                foreach (EvaluationDesign design in AllEvaluationDesigns())
                {
                    foreach (string caseId in new[] { "E1_REMOVAL", "OLD_CORRIDOR_REMOVAL" })
                    {
                        RemovalEvaluation first = session.EvaluateRemoval(design, caseId);
                        RemovalEvaluation second = session.EvaluateRemoval(design, caseId);
                        AssertRemovalMatchesOracle(first, EvaluationDesignId(design), caseId);
                        AssertRemovalEquivalent(first, second, $"{route}/{stage}/{design}/{caseId}");
                        Equal(before, SnapshotJson.Serialize(session.GetSnapshot()), $"{route}/{stage}: removal query mutated state");
                    }
                }
            }
        }
    }

    private void CheckRejectedCommands()
    {
        GridworksSession initial = SessionAt(CorridorDesign.RiverParallel, ReachableStage.Initial);
        AssertRejected(initial, initial.AdvanceToNextMilestone, "REQUIRED_ACTION_PENDING", "initial/advance");
        AssertRejected(initial, () => initial.OrderCorridor(CorridorDesign.RiverParallel), "WRONG_TIME", "initial/river-order");
        AssertRejected(initial, () => initial.OrderCorridor(CorridorDesign.NorthDetour), "WRONG_TIME", "initial/north-order");

        GridworksSession townOrdered = SessionAt(CorridorDesign.RiverParallel, ReachableStage.TownOrdered);
        AssertRejected(townOrdered, townOrdered.OrderTownFeeder, "ALREADY_ORDERED", "town-ordered/town-order");
        AssertRejected(townOrdered, () => townOrdered.OrderCorridor(CorridorDesign.RiverParallel), "WRONG_TIME", "town-ordered/river-order");
        AssertRejected(townOrdered, () => townOrdered.OrderCorridor(CorridorDesign.NorthDetour), "WRONG_TIME", "town-ordered/north-order");

        GridworksSession preChoice = SessionAt(CorridorDesign.RiverParallel, ReachableStage.PreChoice);
        AssertRejected(preChoice, preChoice.OrderTownFeeder, "WRONG_TIME", "pre-choice/town-order");
        AssertRejected(preChoice, preChoice.AdvanceToNextMilestone, "REQUIRED_ACTION_PENDING", "pre-choice/advance");

        foreach (CorridorDesign route in AllCorridors())
        {
            foreach (ReachableStage stage in new[]
                     {
                         ReachableStage.CorridorOrdered,
                         ReachableStage.CorridorCommissioned,
                         ReachableStage.EventStarted,
                         ReachableStage.Final,
                     })
            {
                GridworksSession session = SessionAt(route, stage);
                AssertRejected(session, session.OrderTownFeeder, "WRONG_TIME", $"{route}/{stage}/town-order");
                string corridorCode = stage == ReachableStage.CorridorOrdered ? "ALREADY_ORDERED" : "WRONG_TIME";
                AssertRejected(session, () => session.OrderCorridor(CorridorDesign.RiverParallel), corridorCode, $"{route}/{stage}/river-order");
                AssertRejected(session, () => session.OrderCorridor(CorridorDesign.NorthDetour), corridorCode, $"{route}/{stage}/north-order");

                if (stage == ReachableStage.Final)
                {
                    AssertRejected(session, session.AdvanceToNextMilestone, "NO_NEXT_MILESTONE", $"{route}/{stage}/advance");
                }
            }
        }
    }

    private void CheckEnergyAndCashConservation()
    {
        foreach (CorridorDesign design in AllCorridors())
        {
            RouteRun run = RunRoute(_fixtureJson, design, compareOracle: false);
            foreach (PublicSnapshot snapshot in run.Snapshots)
            {
                CheckSnapshotConservation(snapshot, $"{design}/{snapshot.Minute}");
            }

            foreach (BoundaryTrace trace in run.CommandResults.SelectMany(result => result.BoundaryTrace))
            {
                CheckSnapshotConservation(trace.Snapshot, $"{design}/{trace.Id}");
            }
        }
    }

    private void CheckDeterminism()
    {
        foreach (CorridorDesign design in AllCorridors())
        {
            RouteRun first = RunRoute(_fixtureJson, design, compareOracle: false);
            RouteRun second = RunRoute(_fixtureJson, design, compareOracle: false);
            IReadOnlyList<string> firstBytes = SerializedRun(first);
            IReadOnlyList<string> secondBytes = SerializedRun(second);
            SequenceEqual(firstBytes, secondBytes, $"{design}: identical scripts were not byte deterministic");

            foreach (PublicSnapshot snapshot in first.Snapshots)
            {
                AssertStableJsonAndHash(snapshot, $"{design}/{snapshot.Minute}");
            }
        }

        string reorderedFixture = Mutate(root =>
        {
            Reverse(Array(root, "nodes"));
            Reverse(Array(root, "edges"));
            Reverse(Array(root, "projects"));
            Reverse(Array(root, "loads"));
            Reverse(Array(root, "permittedSupplyPaths"));
            Reverse(Array(root, "evaluationCases"));
        });

        foreach (CorridorDesign design in AllCorridors())
        {
            IReadOnlyList<string> canonical = SerializedRun(RunRoute(_fixtureJson, design, compareOracle: false));
            IReadOnlyList<string> reordered = SerializedRun(RunRoute(reorderedFixture, design, compareOracle: false));
            SequenceEqual(canonical, reordered, $"{design}: output depended on fixture array order instead of ordinal IDs");
        }
    }

    private void CheckNoInferredReverseFeed()
    {
        GridworksSession session = NewSession();
        RemovalEvaluation northOld = session.EvaluateRemoval(EvaluationDesign.NorthDetour, "OLD_CORRIDOR_REMOVAL");
        Check(!northOld.TownUtilityDelivered, "North OLD removal inferred an unauthorized reverse-feed path to town");
        Check(northOld.HospitalUtilityDelivered, "North authored hospital backup path was not retained");
        Check(northOld.TownPathId is null, "North OLD removal invented a town path ID");
        Equal("HOSPITAL_NORTH_BACKUP_PATH", northOld.HospitalPathId, "North OLD hospital path");

        JsonArray permittedPaths = Array(_fixtureRoot, "permittedSupplyPaths");
        HashSet<string> permittedPathIds = permittedPaths
            .Select(node => String(Object(node, "permitted path"), "id"))
            .ToHashSet(StringComparer.Ordinal);
        JsonObject townPath = FindById(permittedPaths, "TOWN_PRIMARY_PATH");
        JsonArray townEdges = Array(townPath, "edgeIds");
        Check(!townEdges.Any(node => string.Equals(node!.GetValue<string>(), "NORTH_DETOUR", StringComparison.Ordinal)),
            "fixture itself authorizes North-to-town feed");
        Check(!townEdges.Any(node => string.Equals(node!.GetValue<string>(), "HOSPITAL_PRIMARY", StringComparison.Ordinal)),
            "fixture itself authorizes hospital-to-town feed");

        RouteRun northRun = RunRoute(_fixtureJson, CorridorDesign.NorthDetour, compareOracle: false);
        foreach (PublicSnapshot snapshot in northRun.Snapshots)
        {
            JsonObject snapshotNode = SnapshotNode(snapshot);
            foreach ((string loadId, JsonNode? pathNode) in Object(snapshotNode, "utilityPathByLoad"))
            {
                if (pathNode is not null)
                {
                    Check(permittedPathIds.Contains(pathNode.GetValue<string>()), $"{loadId}: non-authored path was returned");
                }
            }
        }

        int eventStart = _loadedFixture.Scenario.Events.Single().StartMinute;
        PublicSnapshot eventSnapshot = northRun.Snapshots.Single(snapshot => snapshot.Minute == eventStart);
        JsonObject eventPaths = Object(SnapshotNode(eventSnapshot), "utilityPathByLoad");
        Check(eventPaths[_townLoadId] is null, "actual North spatial event inferred reverse feed to town");
        Equal(northOld.HospitalPathId, eventPaths[_hospitalLoadId]!.GetValue<string>(), "actual North hospital backup path");
    }

    private RouteRun RunRoute(string fixtureJson, CorridorDesign design, bool compareOracle)
    {
        GridworksSession session = NewSession(fixtureJson);
        var snapshots = new List<PublicSnapshot>();
        var results = new List<CommandResult>();

        CaptureSnapshot(session.GetSnapshot(), "INITIAL", null);
        CaptureResult(session.OrderTownFeeder(), "TOWN_ORDERED", null);
        CaptureResult(session.AdvanceToNextMilestone(), "PRE_CHOICE", null);
        CaptureResult(session.OrderCorridor(design), "CORRIDOR_ORDERED", CorridorId(design));
        CaptureResult(session.AdvanceToNextMilestone(), "CORRIDOR_COMMISSIONED", CorridorId(design));
        CaptureResult(session.AdvanceToNextMilestone(), "EVENT_STARTED", CorridorId(design));
        CaptureResult(session.AdvanceToNextMilestone(), "FINAL", CorridorId(design));
        return new RouteRun(session, snapshots, results);

        void CaptureResult(CommandResult result, string oracleId, string? route)
        {
            Check(result.Accepted, $"{design}/{oracleId}: valid command rejected as {result.ErrorCode}");
            results.Add(result);
            CaptureSnapshot(result.PublicSnapshot, oracleId, route);
            Equal(
                SnapshotJson.Serialize(result.PublicSnapshot),
                SnapshotJson.Serialize(session.GetSnapshot()),
                $"{design}/{oracleId}: command result differs from stored state");
        }

        void CaptureSnapshot(PublicSnapshot snapshot, string oracleId, string? route)
        {
            snapshots.Add(snapshot);
            if (!compareOracle)
            {
                return;
            }

            PublicSnapshot typedOracle = route is null
                ? CommonOracleSnapshot(oracleId)
                : RouteOracleSnapshot(design, oracleId);
            JsonObject rawOracle = route is null
                ? CommonOracleState(oracleId)
                : RouteOracleState(route, oracleId);
            AssertSnapshotMatchesOracle(snapshot, typedOracle, rawOracle, $"{design}/{oracleId}");
        }
    }

    private GridworksSession SessionAt(CorridorDesign design, ReachableStage stage)
    {
        GridworksSession session = NewSession();
        if (stage == ReachableStage.Initial)
        {
            return session;
        }

        RequireAccepted(session.OrderTownFeeder(), $"build {design}/{stage}: town order");
        if (stage == ReachableStage.TownOrdered)
        {
            return session;
        }

        RequireAccepted(session.AdvanceToNextMilestone(), $"build {design}/{stage}: pre-choice");
        if (stage == ReachableStage.PreChoice)
        {
            return session;
        }

        RequireAccepted(session.OrderCorridor(design), $"build {design}/{stage}: corridor order");
        if (stage == ReachableStage.CorridorOrdered)
        {
            return session;
        }

        RequireAccepted(session.AdvanceToNextMilestone(), $"build {design}/{stage}: commission");
        if (stage == ReachableStage.CorridorCommissioned)
        {
            return session;
        }

        RequireAccepted(session.AdvanceToNextMilestone(), $"build {design}/{stage}: event start");
        if (stage == ReachableStage.EventStarted)
        {
            return session;
        }

        RequireAccepted(session.AdvanceToNextMilestone(), $"build {design}/{stage}: final");
        return session;
    }

    private GridworksSession NewSession(string? fixtureJson = null)
    {
        LoadedFixture loaded = FixtureLoader.Load(fixtureJson ?? _fixtureJson);
        return new GridworksSession(loaded.Scenario);
    }

    private void AssertRejected(
        GridworksSession session,
        Func<CommandResult> command,
        string expectedCode,
        string context)
    {
        string before = SnapshotJson.Serialize(session.GetSnapshot());
        string beforeHash = SnapshotJson.Sha256Hex(session.GetSnapshot());
        CommandResult result = command();
        Check(!result.Accepted, $"{context}: command unexpectedly accepted");
        Equal(expectedCode, ErrorCodeName(result.ErrorCode), $"{context}: rejection code");
        Equal(0, result.BoundaryTrace.Count, $"{context}: rejected command returned a trace");
        Equal(before, SnapshotJson.Serialize(result.PublicSnapshot), $"{context}: result snapshot mutated");
        Equal(before, SnapshotJson.Serialize(session.GetSnapshot()), $"{context}: session snapshot mutated");
        Equal(beforeHash, SnapshotJson.Sha256Hex(session.GetSnapshot()), $"{context}: snapshot hash mutated");
    }

    private void AssertSnapshotMatchesOracle(
        PublicSnapshot snapshot,
        PublicSnapshot typedOracle,
        JsonObject rawOracleRow,
        string context)
    {
        string actualJson = SnapshotJson.Serialize(snapshot);
        Equal(SnapshotJson.Serialize(typedOracle), actualJson, $"{context}: typed full-snapshot oracle");

        JsonObject expected = (JsonObject)rawOracleRow.DeepClone();
        expected.Remove("id");
        JsonObject actual = ParseObject(actualJson, "snapshot");
        Check(JsonNode.DeepEquals(expected, actual),
            $"{context}: typed oracle differs from its raw machine authority\nexpected: {expected.ToJsonString()}\nactual:   {actual.ToJsonString()}");
    }

    private void AssertRemovalMatchesOracle(RemovalEvaluation actual, string designId, string caseId)
    {
        EvaluationOutcomeOracle typedExpected = _loadedFixture.Oracle.EvaluationOutcomes.Single(item =>
            string.Equals(EvaluationDesignId(item.Design), designId, StringComparison.Ordinal)
            && string.Equals(item.CaseId, caseId, StringComparison.Ordinal));
        JsonObject rawExpected = EvaluationOracle(designId, caseId);
        Equal(typedExpected.Design, actual.Design, $"{designId}/{caseId}: design");
        Equal(typedExpected.CaseId, actual.CaseId, $"{designId}/{caseId}: case ID");
        SequenceEqual(
            typedExpected.RemovedEdgeIds,
            actual.RemovedEdgeIds,
            $"{designId}/{caseId}: removed edges");
        Equal(typedExpected.TownUtilityDelivered, actual.TownUtilityDelivered, $"{designId}/{caseId}: town utility");
        Equal(typedExpected.HospitalUtilityDelivered, actual.HospitalUtilityDelivered, $"{designId}/{caseId}: hospital utility");
        Equal(typedExpected.TownPathId, actual.TownPathId, $"{designId}/{caseId}: town path");
        Equal(typedExpected.HospitalPathId, actual.HospitalPathId, $"{designId}/{caseId}: hospital path");

        SequenceEqual(
            Array(rawExpected, "removedEdgeIds").Select(node => node!.GetValue<string>()),
            typedExpected.RemovedEdgeIds,
            $"{designId}/{caseId}: typed/raw removed-edge oracle");
        Equal(Bool(rawExpected, "townUtilityDelivered"), typedExpected.TownUtilityDelivered, $"{designId}/{caseId}: typed/raw town oracle");
        Equal(Bool(rawExpected, "hospitalUtilityDelivered"), typedExpected.HospitalUtilityDelivered, $"{designId}/{caseId}: typed/raw hospital oracle");
        Equal(NullableString(rawExpected, "townPathId"), typedExpected.TownPathId, $"{designId}/{caseId}: typed/raw town path oracle");
        Equal(NullableString(rawExpected, "hospitalPathId"), typedExpected.HospitalPathId, $"{designId}/{caseId}: typed/raw hospital path oracle");
    }

    private void AssertRemovalEquivalent(RemovalEvaluation first, RemovalEvaluation second, string context)
    {
        Equal(EvaluationDesignId(first.Design), EvaluationDesignId(second.Design), $"{context}: design determinism");
        Equal(first.CaseId, second.CaseId, $"{context}: case determinism");
        SequenceEqual(first.RemovedEdgeIds, second.RemovedEdgeIds, $"{context}: removed edge determinism");
        Equal(first.TownUtilityDelivered, second.TownUtilityDelivered, $"{context}: town determinism");
        Equal(first.HospitalUtilityDelivered, second.HospitalUtilityDelivered, $"{context}: hospital determinism");
        Equal(first.TownPathId, second.TownPathId, $"{context}: town path determinism");
        Equal(first.HospitalPathId, second.HospitalPathId, $"{context}: hospital path determinism");
    }

    private void CheckSnapshotConservation(PublicSnapshot snapshot, string context)
    {
        JsonObject node = SnapshotNode(snapshot);
        JsonObject cumulative = Object(node, "cumulative");
        JsonObject interval = Object(node, "interval");
        CheckSettlementCash(interval, $"{context}/interval");
        CheckSettlementCash(cumulative, $"{context}/cumulative");

        long minute = Long(node, "minute");
        JsonObject delivered = Object(cumulative, "utilityDeliveredKwMinuteByLoad");
        JsonObject unserved = Object(cumulative, "utilityUnservedKwMinuteByLoad");
        long hospitalDelivered = Long(delivered, _hospitalLoadId);
        long townDelivered = Long(delivered, _townLoadId);
        long hospitalUnserved = Long(unserved, _hospitalLoadId);
        long townUnserved = Long(unserved, _townLoadId);
        long hospitalInternal = Long(cumulative, "hospitalInternalUsedKwMinute");
        long hospitalP0Unserved = Long(cumulative, "hospitalP0UnservedKwMinute");

        ScenarioDefinition scenario = _loadedFixture.Scenario;
        long hospitalDemand = scenario.Nodes.Single(node => node.Id == _hospitalLoadId).DemandKw
            ?? throw new CheckFailure($"{context}: hospital demand is absent");
        long townDemand = scenario.Nodes.Single(node => node.Id == _townLoadId).DemandKw
            ?? throw new CheckFailure($"{context}: town demand is absent");
        int townActiveMinute = scenario.Loads.Single(load => load.NodeId == _townLoadId).ActiveMinute;
        long totalInternalEnergy = scenario.HospitalInternalPower.Stages
            .Select(stage => stage.EnergyKwMinute)
            .Aggregate(0L, checked((total, energy) => total + energy));

        Equal(checked(hospitalDemand * minute), checked(hospitalDelivered + hospitalUnserved), $"{context}: hospital utility energy");
        Equal(checked(townDemand * Math.Max(0, minute - townActiveMinute)), checked(townDelivered + townUnserved), $"{context}: town utility energy");
        Equal(hospitalUnserved, checked(hospitalInternal + hospitalP0Unserved), $"{context}: hospital P0/internal boundary");
        Equal(checked(hospitalDelivered + townDelivered), Long(cumulative, "gasInjectionKwMinute"), $"{context}: gas injection");
        Equal(checked(totalInternalEnergy - hospitalInternal), Long(node, "hospitalInternalRemainingKwMinute"), $"{context}: internal energy remaining");

        SupplyPathDefinition townPrimary = scenario.PermittedSupplyPaths.Single(path =>
            path.LoadNodeId == _townLoadId && path.Role == PathRole.Primary);
        ProjectDefinition townProject = scenario.Projects.Single(project =>
            townPrimary.EdgeIds.Contains(project.EdgeId, StringComparer.Ordinal)
            && project.AllowedOrderMinute == scenario.Milestones[0].Minute);
        long constructionSpend = String(node, "townProjectState") == "not_ordered"
            ? 0
            : townProject.CostCashUnit;
        string? selected = NullableString(node, "selectedCorridor");
        if (selected is not null)
        {
            ProjectDefinition corridorProject = scenario.Projects.Single(project =>
                string.Equals(project.EdgeId, selected, StringComparison.Ordinal));
            constructionSpend = checked(constructionSpend + corridorProject.CostCashUnit);
        }
        long expectedCash = checked(
            scenario.Economy.InitialCash - constructionSpend + Long(cumulative, "revenueCashUnit")
            - Long(cumulative, "gasCostCashUnit") - Long(cumulative, "compensationCashUnit"));
        Equal(expectedCash, Long(node, "cash"), $"{context}: cash conservation");

        HashSet<string> commissioned = Array(node, "commissionedEdgeIds")
            .Select(value => value!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        foreach (JsonNode? removed in Array(node, "eventRemovedEdgeIds"))
        {
            Check(commissioned.Contains(removed!.GetValue<string>()), $"{context}: removed edge is not commissioned");
        }

        CheckInstantaneousPathCapacity(node, context);
    }

    private void CheckSettlementCash(JsonObject settlement, string context)
    {
        JsonObject delivered = Object(settlement, "utilityDeliveredKwMinuteByLoad");
        JsonObject unserved = Object(settlement, "utilityUnservedKwMinuteByLoad");
        long totalDelivered = checked(Long(delivered, _hospitalLoadId) + Long(delivered, _townLoadId));
        long hospitalUnserved = Long(unserved, _hospitalLoadId);
        long townUnserved = Long(unserved, _townLoadId);
        EconomyDefinition economy = _loadedFixture.Scenario.Economy;
        LoadDefinition hospitalLoad = _loadedFixture.Scenario.Loads.Single(load => load.NodeId == _hospitalLoadId);
        LoadDefinition townLoad = _loadedFixture.Scenario.Loads.Single(load => load.NodeId == _townLoadId);
        Equal(totalDelivered, Long(settlement, "gasInjectionKwMinute"), $"{context}: gas injection energy");
        Equal(ExactCash(totalDelivered, economy.SaleRate, context), Long(settlement, "revenueCashUnit"), $"{context}: revenue");
        Equal(ExactCash(totalDelivered, economy.GasVariableRate, context), Long(settlement, "gasCostCashUnit"), $"{context}: gas cost");
        Equal(
            checked(
                ExactCash(hospitalUnserved, economy.GetOutageRate(hospitalLoad.OutageRateKey), context)
                + ExactCash(townUnserved, economy.GetOutageRate(townLoad.OutageRateKey), context)),
            Long(settlement, "compensationCashUnit"),
            $"{context}: compensation");
        Equal(
            ExactCash(checked(hospitalUnserved + townUnserved), economy.SaleRate, context),
            Long(settlement, "lostSalesCashUnit"),
            $"{context}: lost sales");
    }

    private void CheckInstantaneousPathCapacity(JsonObject snapshot, string context)
    {
        HashSet<string> commissioned = Array(snapshot, "commissionedEdgeIds")
            .Select(node => node!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> removed = Array(snapshot, "eventRemovedEdgeIds")
            .Select(node => node!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        JsonObject pathsByLoad = Object(snapshot, "utilityPathByLoad");
        var allocationByEdge = new Dictionary<string, long>(StringComparer.Ordinal);
        var allocationByGenerator = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach ((string loadId, JsonNode? pathValue) in pathsByLoad)
        {
            if (pathValue is null)
            {
                continue;
            }

            long demand = _loadedFixture.Scenario.Nodes.Single(node => node.Id == loadId).DemandKw
                ?? throw new CheckFailure($"{context}: load {loadId} has no demand");
            JsonObject path = FindById(Array(_fixtureRoot, "permittedSupplyPaths"), pathValue.GetValue<string>());
            string firstEdgeId = Array(path, "edgeIds")[0]!.GetValue<string>();
            string generatorId = String(FindById(Array(_fixtureRoot, "edges"), firstEdgeId), "fromNodeId");
            allocationByGenerator[generatorId] = checked(
                allocationByGenerator.GetValueOrDefault(generatorId) + demand);
            foreach (JsonNode? edgeValue in Array(path, "edgeIds"))
            {
                string edgeId = edgeValue!.GetValue<string>();
                Check(commissioned.Contains(edgeId), $"{context}: path uses uncommissioned edge {edgeId}");
                Check(!removed.Contains(edgeId), $"{context}: path uses event-removed edge {edgeId}");
                allocationByEdge[edgeId] = checked(allocationByEdge.GetValueOrDefault(edgeId) + demand);
            }
        }

        foreach ((string edgeId, long allocation) in allocationByEdge)
        {
            long rating = Long(FindById(Array(_fixtureRoot, "edges"), edgeId), "ratingKw");
            Check(allocation <= rating, $"{context}: {edgeId} allocation {allocation} exceeds {rating}");
        }

        foreach ((string generatorId, long allocation) in allocationByGenerator)
        {
            NodeDefinition generator = _loadedFixture.Scenario.Nodes.Single(node => node.Id == generatorId);
            Check(generator.InitialOnline == true, $"{context}: generator {generatorId} is offline");
            Check(generator.MaxOutputKw.HasValue && allocation <= generator.MaxOutputKw.Value,
                $"{context}: generator {generatorId} output exceeds rating");
        }
    }

    private void AssertStableJsonAndHash(PublicSnapshot snapshot, string context)
    {
        string json = SnapshotJson.Serialize(snapshot);
        byte[] bytes = SnapshotJson.SerializeToUtf8Bytes(snapshot);
        Equal(json, Encoding.UTF8.GetString(bytes), $"{context}: string/UTF-8 serializers differ");

        using JsonDocument document = JsonDocument.Parse(bytes);
        SequenceEqual(
            SnapshotPropertyOrder,
            document.RootElement.EnumerateObject().Select(property => property.Name).ToArray(),
            $"{context}: snapshot property order");
        foreach (string ledger in new[] { "interval", "cumulative" })
        {
            JsonElement settlement = document.RootElement.GetProperty(ledger);
            SequenceEqual(
                SettlementPropertyOrder,
                settlement.EnumerateObject().Select(property => property.Name).ToArray(),
                $"{context}/{ledger}: settlement property order");
            AssertObjectKeysOrdinal(settlement.GetProperty("utilityDeliveredKwMinuteByLoad"), $"{context}/{ledger}/delivered");
            AssertObjectKeysOrdinal(settlement.GetProperty("utilityUnservedKwMinuteByLoad"), $"{context}/{ledger}/unserved");
        }

        AssertObjectKeysOrdinal(document.RootElement.GetProperty("utilityPathByLoad"), $"{context}/paths");
        AssertArrayOrdinal(document.RootElement.GetProperty("commissionedEdgeIds"), $"{context}/commissioned");
        AssertArrayOrdinal(document.RootElement.GetProperty("eventRemovedEdgeIds"), $"{context}/removed");
        AssertArrayOrdinal(document.RootElement.GetProperty("activeLoadIds"), $"{context}/active-loads");

        string independentHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Equal(independentHash, SnapshotJson.Sha256Hex(snapshot), $"{context}: SHA-256");
    }

    private void AssertObjectKeysOrdinal(JsonElement element, string context)
    {
        string[] keys = element.EnumerateObject().Select(property => property.Name).ToArray();
        string[] sorted = keys.Order(StringComparer.Ordinal).ToArray();
        SequenceEqual(sorted, keys, $"{context}: object keys are not ordinal sorted");
    }

    private void AssertArrayOrdinal(JsonElement element, string context)
    {
        string[] values = element.EnumerateArray().Select(value => value.GetString()!).ToArray();
        string[] sorted = values.Order(StringComparer.Ordinal).ToArray();
        SequenceEqual(sorted, values, $"{context}: array is not ordinal sorted");
    }

    private static IReadOnlyList<string> SerializedRun(RouteRun run)
    {
        var rows = run.Snapshots.Select(SnapshotJson.Serialize).ToList();
        foreach (CommandResult result in run.CommandResults)
        {
            rows.AddRange(result.BoundaryTrace.Select(trace => $"{trace.Id}:{SnapshotJson.Serialize(trace.Snapshot)}"));
        }

        return rows;
    }

    private void ExpectFixtureRejected(string name, Action<JsonObject> mutation)
    {
        ExpectFixtureRejectedJson(name, Mutate(mutation));
    }

    private void ExpectFixtureRejectedJson(string name, string json)
    {
        try
        {
            _ = FixtureLoader.Load(json);
        }
        catch (FixtureValidationException)
        {
            _assertionCount++;
            return;
        }

        throw new CheckFailure($"{name}: malformed fixture was accepted");
    }

    private void ExpectFixtureRejectedBytes(string name, byte[] bytes)
    {
        try
        {
            _ = FixtureLoader.Load(bytes);
        }
        catch (FixtureValidationException)
        {
            _assertionCount++;
            return;
        }

        throw new CheckFailure($"{name}: malformed fixture was accepted");
    }

    private string Mutate(Action<JsonObject> mutation)
    {
        JsonObject clone = (JsonObject)_fixtureRoot.DeepClone();
        mutation(clone);
        return clone.ToJsonString();
    }

    private static void Reverse(JsonArray array)
    {
        JsonNode?[] nodes = array.Select(node => node?.DeepClone()).Reverse().ToArray();
        array.Clear();
        foreach (JsonNode? node in nodes)
        {
            array.Add(node);
        }
    }

    private JsonObject CommonOracleState(string id)
    {
        return FindById(Array(Object(_fixtureRoot, "verificationOnly"), "commonBoundaryStates"), id);
    }

    private PublicSnapshot CommonOracleSnapshot(string id)
    {
        return _loadedFixture.Oracle.CommonBoundaryStates.Single(state =>
            string.Equals(state.Id, id, StringComparison.Ordinal)).Snapshot;
    }

    private JsonObject RouteOracleState(string designId, string id)
    {
        JsonArray routes = Array(Object(_fixtureRoot, "verificationOnly"), "routeBoundaryStates");
        JsonObject route = routes
            .Select(node => Object(node, "route boundary"))
            .Single(item => string.Equals(String(item, "design"), designId, StringComparison.Ordinal));
        return FindById(Array(route, "states"), id);
    }

    private PublicSnapshot RouteOracleSnapshot(CorridorDesign design, string id)
    {
        RouteBoundaryOracle route = _loadedFixture.Oracle.RouteBoundaryStates.Single(item => item.Design == design);
        return route.States.Single(state => string.Equals(state.Id, id, StringComparison.Ordinal)).Snapshot;
    }

    private JsonObject EvaluationOracle(string designId, string caseId)
    {
        return Array(Object(_fixtureRoot, "verificationOnly"), "evaluationOutcomes")
            .Select(node => Object(node, "evaluation oracle"))
            .Single(item =>
                string.Equals(String(item, "design"), designId, StringComparison.Ordinal)
                && string.Equals(String(item, "caseId"), caseId, StringComparison.Ordinal));
    }

    private static JsonObject SnapshotNode(PublicSnapshot snapshot)
    {
        return ParseObject(SnapshotJson.Serialize(snapshot), "snapshot");
    }

    private static JsonObject ParseObject(string json, string context)
    {
        return JsonNode.Parse(json) as JsonObject
            ?? throw new CheckFailure($"{context} is not a JSON object");
    }

    private static JsonObject Object(JsonObject parent, string property)
    {
        return parent[property] as JsonObject
            ?? throw new CheckFailure($"{property} is not an object");
    }

    private static JsonObject Object(JsonNode? node, string context)
    {
        return node as JsonObject
            ?? throw new CheckFailure($"{context} is not an object");
    }

    private static JsonArray Array(JsonObject parent, string property)
    {
        return parent[property] as JsonArray
            ?? throw new CheckFailure($"{property} is not an array");
    }

    private static JsonObject FindById(JsonArray array, string id)
    {
        return array
            .Select(node => Object(node, "ID-bearing item"))
            .Single(item => string.Equals(String(item, "id"), id, StringComparison.Ordinal));
    }

    private static string String(JsonObject item, string property)
    {
        return item[property]?.GetValue<string>()
            ?? throw new CheckFailure($"{property} is not a string");
    }

    private static string? NullableString(JsonObject item, string property)
    {
        JsonNode? value = item[property];
        return value is null ? null : value.GetValue<string>();
    }

    private static long Long(JsonObject item, string property)
    {
        return item[property]?.GetValue<long>()
            ?? throw new CheckFailure($"{property} is not an integer");
    }

    private static bool Bool(JsonObject item, string property)
    {
        return item[property]?.GetValue<bool>()
            ?? throw new CheckFailure($"{property} is not a boolean");
    }

    private long ExactCash(long energyKwMinute, long rateCashUnitPerGWh, string context)
    {
        long numerator = checked(energyKwMinute * rateCashUnitPerGWh);
        Check(numerator % CashDivisor == 0, $"{context}: cash numerator is not exactly divisible");
        return numerator / CashDivisor;
    }

    private static string CorridorId(CorridorDesign design)
    {
        return design switch
        {
            CorridorDesign.RiverParallel => "RIVER_PARALLEL",
            CorridorDesign.NorthDetour => "NORTH_DETOUR",
            _ => throw new ArgumentOutOfRangeException(nameof(design), design, null),
        };
    }

    private static string EvaluationDesignId(EvaluationDesign design)
    {
        return design switch
        {
            EvaluationDesign.NoBuild => "NO_BUILD",
            EvaluationDesign.RiverParallel => "RIVER_PARALLEL",
            EvaluationDesign.NorthDetour => "NORTH_DETOUR",
            _ => throw new ArgumentOutOfRangeException(nameof(design), design, null),
        };
    }

    private static IEnumerable<CorridorDesign> AllCorridors()
    {
        yield return CorridorDesign.RiverParallel;
        yield return CorridorDesign.NorthDetour;
    }

    private static IEnumerable<EvaluationDesign> AllEvaluationDesigns()
    {
        yield return EvaluationDesign.NoBuild;
        yield return EvaluationDesign.RiverParallel;
        yield return EvaluationDesign.NorthDetour;
    }

    private static string ErrorCodeName(object? errorCode)
    {
        if (errorCode is null)
        {
            return "<null>";
        }

        string source = errorCode.ToString() ?? string.Empty;
        var result = new StringBuilder();
        for (int index = 0; index < source.Length; index++)
        {
            char character = source[index];
            if (index > 0 && char.IsUpper(character) && !char.IsUpper(source[index - 1]))
            {
                result.Append('_');
            }

            result.Append(char.ToUpperInvariant(character));
        }

        return result.ToString();
    }

    private void RequireAccepted(CommandResult result, string context)
    {
        Check(result.Accepted, $"{context}: command rejected as {result.ErrorCode}");
    }

    private void Check(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new CheckFailure(message);
        }
    }

    private void Equal<T>(T expected, T actual, string context)
    {
        Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{context}: expected {expected}, actual {actual}");
    }

    private void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string context)
    {
        T[] expectedArray = expected.ToArray();
        T[] actualArray = actual.ToArray();
        Check(
            expectedArray.SequenceEqual(actualArray),
            $"{context}: expected [{string.Join(", ", expectedArray)}], actual [{string.Join(", ", actualArray)}]");
    }

    private sealed record RouteRun(
        GridworksSession Session,
        List<PublicSnapshot> Snapshots,
        List<CommandResult> CommandResults);

    private enum ReachableStage
    {
        Initial,
        TownOrdered,
        PreChoice,
        CorridorOrdered,
        CorridorCommissioned,
        EventStarted,
        Final,
    }
}

internal sealed class CheckFailure(string message) : Exception(message);
