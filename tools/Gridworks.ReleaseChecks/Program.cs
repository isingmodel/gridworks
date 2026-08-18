using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using Gridworks.Core.Release;

namespace Gridworks.ReleaseChecks;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            (string worldPath, string campaignPath) = ResolveFixturePaths(args);
            return new ReleaseChecks(worldPath, campaignPath).Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL startup: {exception.Message}");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static (string WorldPath, string CampaignPath) ResolveFixturePaths(string[] args)
    {
        if (args.Length > 2)
        {
            throw new ArgumentException(
                "usage: Gridworks.ReleaseChecks [release-world-json] [release-campaign-json]");
        }

        string worldPath = args.Length >= 1
            ? args[0]
            : Path.Combine(Environment.CurrentDirectory, "data", "release-world-v1.json");
        string campaignPath = args.Length == 2
            ? args[1]
            : Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(worldPath))!,
                "release-campaign-v1.json");
        worldPath = Path.GetFullPath(worldPath);
        campaignPath = Path.GetFullPath(campaignPath);
        if (!File.Exists(worldPath))
        {
            throw new FileNotFoundException("Release world fixture not found.", worldPath);
        }
        if (!File.Exists(campaignPath))
        {
            throw new FileNotFoundException("Release campaign fixture not found.", campaignPath);
        }

        return (worldPath, campaignPath);
    }
}

internal sealed class ReleaseChecks
{
    private const string SourceClassId = "CHECK_SOURCE";
    private const string Pole3ClassId = "CHECK_POLE_3";
    private const string Pole4ClassId = "CHECK_POLE_4";
    private const string SubstationClassId = "CHECK_SUBSTATION";
    private const string DedicatedClassId = "CHECK_DEDICATED";
    private const string Line100ClassId = "CHECK_LINE_100";
    private const string Line50ClassId = "CHECK_LINE_50";
    private const string Line40ClassId = "CHECK_LINE_40";

    private readonly string _fixtureJson;
    private readonly ReleaseWorldDefinition _fixture;
    private readonly string _campaignJson;
    private readonly ReleaseCampaignDefinition _campaign;
    private readonly string _worldSha256;
    private readonly string _campaignSha256;
    private int _assertionCount;

    public ReleaseChecks(string fixturePath, string campaignPath)
    {
        byte[] worldBytes = File.ReadAllBytes(fixturePath);
        byte[] campaignBytes = File.ReadAllBytes(campaignPath);
        _fixtureJson = Encoding.UTF8.GetString(worldBytes);
        _fixture = ReleaseWorldLoader.Load(worldBytes);
        _campaignJson = Encoding.UTF8.GetString(campaignBytes);
        _campaign = ReleaseCampaignLoader.Load(campaignBytes, _fixture);
        _worldSha256 = Convert.ToHexString(SHA256.HashData(worldBytes)).ToLowerInvariant();
        _campaignSha256 = Convert.ToHexString(SHA256.HashData(campaignBytes)).ToLowerInvariant();
    }

    public int Run()
    {
        (string Name, Action Body)[] suites =
        [
            ("strict-loader-negatives", CheckStrictLoaderNegatives),
            ("branch-merge-connection-rating-boundaries", CheckBranchMergeAndBoundaries),
            ("route-tiebreak-cycle-safety", CheckRouteTieBreakAndCycleSafety),
            ("shared-edge-node-substation-usage", CheckSharedUsage),
            ("priority-source-order-conservation", CheckPrioritySourceOrderAndConservation),
            ("service-area-dedicated-loads", CheckServiceAreaAndDedicatedLoads),
            ("crossing-is-not-connection", CheckCrossingIsNotConnection),
            ("node-edge-polygon-outage-reroute", CheckOutageRerouting),
            ("immutability-repeat-determinism", CheckImmutabilityAndDeterminism),
            ("construction-node-lifecycle", CheckConstructionNodeLifecycle),
            ("construction-line-branch-merge", CheckConstructionLineBranchMerge),
            ("construction-rejection-atomicity", CheckConstructionRejectionAndAtomicity),
            ("campaign-loader-content", CheckCampaignLoaderAndContent),
            ("campaign-eight-chapter-witness", CheckCampaignEightChapterWitness),
            ("campaign-save-restart", CheckCampaignSaveAndRestart),
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
                $"Gridworks Release checks: FAIL ({failures.Count}/{suites.Length} suites)");
            return 1;
        }

        Console.WriteLine(
            $"Gridworks Release checks: PASS ({suites.Length} suites, {_assertionCount} assertions)");
        return 0;
    }

    private void CheckStrictLoaderNegatives()
    {
        ReleaseWorldDefinition fromText = ReleaseWorldLoader.Load(_fixtureJson);
        ReleaseWorldDefinition fromBytes = ReleaseWorldLoader.Load(Encoding.UTF8.GetBytes(_fixtureJson));
        Equal(_fixture.WorldId, fromText.WorldId, "text loader world ID");
        Equal(_fixture.WorldId, fromBytes.WorldId, "UTF-8 loader world ID");
        Equal(0, _fixture.Grid.MinX, "release grid minimum X");
        Equal(0, _fixture.Grid.MinY, "release grid minimum Y");
        Equal(32, _fixture.Grid.MaxX, "release grid maximum X");
        Equal(20, _fixture.Grid.MaxY, "release grid maximum Y");
        Equal(4, _fixture.WaterPolygon.Count, "release water polygon point count");

        string trimmed = _fixtureJson.TrimStart();
        ExpectLoaderRejected(
            "duplicate JSON property",
            $"{{\"schemaVersion\":\"duplicate\",{trimmed[1..]}");
        ExpectLoaderRejectedBytes("invalid UTF-8", [0xff, 0xfe, 0xfd]);
        ExpectLoaderRejected("unknown root field", root => root["unexpected"] = true);
        ExpectLoaderRejected("missing water polygon", root => root.Remove("waterPolygon"));
        ExpectLoaderRejected(
            "water polygon with fewer than three points",
            root =>
            {
                JsonArray polygon = Array(root, "waterPolygon");
                polygon.RemoveAt(3);
                polygon.RemoveAt(2);
            });
        ExpectLoaderRejected(
            "water polygon point outside grid",
            root => Object(Array(root, "waterPolygon")[0]!)["x"] = -1);
        ExpectLoaderRejected(
            "water polygon with fewer than three distinct points",
            root =>
            {
                JsonArray polygon = Array(root, "waterPolygon");
                polygon[2] = polygon[0]!.DeepClone();
                polygon[3] = polygon[1]!.DeepClone();
            });
        ExpectLoaderRejected(
            "collinear water polygon",
            root =>
            {
                JsonArray polygon = Array(root, "waterPolygon");
                for (int index = 0; index < polygon.Count; index++)
                {
                    Object(polygon[index]!)["x"] = 10 + index;
                    Object(polygon[index]!)["y"] = 0;
                }
            });
        ExpectLoaderRejected(
            "unknown nested field",
            root => Object(root, "grid")["unexpected"] = true);
        ExpectLoaderRejected("missing required field", root => root.Remove("worldId"));
        ExpectLoaderRejected("null required object", root => root["grid"] = null);
        ExpectLoaderRejected(
            "duplicate node ID",
            root => Array(root, "nodes").Add(Array(root, "nodes")[0]!.DeepClone()));
        ExpectLoaderRejected(
            "duplicate node coordinate",
            root => Object(Array(root, "nodes")[1]!)["position"] =
                Object(Array(root, "nodes")[0]!, "position").DeepClone());
        ExpectLoaderRejected(
            "broken edge reference",
            root => Object(Array(root, "edges")[0]!)["fromNodeId"] = "MISSING_NODE");
        ExpectLoaderRejected(
            "zero line rating",
            root => Object(Array(root, "lineClasses")[0]!)["ratingKw"] = 0);
        ExpectLoaderRejected(
            "zero connection limit",
            root => Object(Array(root, "nodeClasses")[0]!)["maxConnections"] = 0);
        ExpectLoaderRejected(
            "reversed grid bounds",
            root => Object(root, "grid")["maxX"] = -1);
    }

    private void CheckBranchMergeAndBoundaries()
    {
        ReleaseWorldDefinition world = BranchMergeWorld();
        ReleaseWorldLoader.Validate(world);
        ReleaseNetworkEvaluation evaluation = ReleaseNetworkEvaluator.Evaluate(world);

        ReleaseNodeUsage branch = NodeUsage(evaluation, "A");
        ReleaseNodeUsage merge = NodeUsage(evaluation, "M");
        Equal(4, branch.ConnectionCount, "reinforced branch degree");
        Equal(4, branch.MaxConnections, "reinforced branch connection limit");
        Equal(3, merge.ConnectionCount, "merge degree");
        Equal(40L, branch.UsedKw, "branch shared usage");
        Equal(30L, merge.UsedKw, "merge service usage");
        Equal(40L, evaluation.TotalDeliveredKw, "branch/merge delivered power");

        ReleaseWorldDefinition degreeOverflow = world with
        {
            Nodes = world.Nodes.Select(node => node.NodeId == "A"
                ? node with { ClassId = Pole3ClassId }
                : node).ToArray(),
        };
        ExpectWorldRejected("four connections on three-connection pole", degreeOverflow);

        ReleaseWorldDefinition exactSpan = ExactSpanWorld(new ReleasePoint(3, 4));
        ReleaseWorldLoader.Validate(exactSpan);
        Check(LoadSupply(ReleaseNetworkEvaluator.Evaluate(exactSpan), "LOAD").DeliveredKw == 50,
            "exact span/rating boundary was rejected");

        ReleaseWorldDefinition spanOverflow = ExactSpanWorld(new ReleasePoint(4, 4));
        ExpectWorldRejected("line longer than maxSpanCells", spanOverflow);

        ReleaseWorldDefinition ratingOverflow = exactSpan with
        {
            Loads = exactSpan.Loads.Select(load => load with { DemandKw = 51 }).ToArray(),
        };
        ReleaseLoadSupply failed = LoadSupply(
            ReleaseNetworkEvaluator.Evaluate(ratingOverflow),
            "LOAD");
        Equal(0L, failed.DeliveredKw, "rating overflow must be all-or-none");
        Equal(ReleaseSupplyFailureKind.EdgeCapacity, failed.Failure.Kind, "rating failure kind");
        Equal("E_DIRECT", failed.Failure.AssetId, "rating failure asset");
    }

    private void CheckRouteTieBreakAndCycleSafety()
    {
        ReleaseWorldDefinition world = TieCycleWorld();
        ReleaseWorldLoader.Validate(world);
        ReleaseNetworkEvaluation first = ReleaseNetworkEvaluator.Evaluate(world);
        ReleaseLoadSupply supply = LoadSupply(first, "LOAD");

        Equal(20L, supply.DeliveredKw, "tie-cycle delivered power");
        SequenceEqual(
            ["A_SOURCE_UPPER", "A_UPPER_MERGE", "E_MERGE_LOAD"],
            supply.PathEdgeIds,
            "equal-length path must use lexicographically first edge sequence");
        Equal(supply.PathNodeIds.Count - 1, supply.PathEdgeIds.Count, "path must be simple");
        Equal(
            supply.PathNodeIds.Count,
            supply.PathNodeIds.Distinct(StringComparer.Ordinal).Count(),
            "cycle leaked into selected path");

        string canonical = JsonSerializer.Serialize(first);
        for (int iteration = 0; iteration < 20; iteration++)
        {
            Equal(
                canonical,
                JsonSerializer.Serialize(ReleaseNetworkEvaluator.Evaluate(world)),
                $"tie-cycle evaluation changed on iteration {iteration}");
        }
    }

    private void CheckSharedUsage()
    {
        ReleaseWorldDefinition world = SharedSubstationWorld();
        ReleaseNetworkEvaluation evaluation = ReleaseNetworkEvaluator.Evaluate(world);

        Equal(20L, LoadSupply(evaluation, "LOAD_A").DeliveredKw, "first service load");
        Equal(30L, LoadSupply(evaluation, "LOAD_B").DeliveredKw, "second service load");
        Equal(50L, EdgeUsage(evaluation, "E_SOURCE_POLE").UsedKw, "shared source edge usage");
        Equal(50L, EdgeUsage(evaluation, "E_POLE_SUB").UsedKw, "shared substation edge usage");
        Equal(50L, NodeUsage(evaluation, "POLE").UsedKw, "shared pole usage");
        Equal(50L, NodeUsage(evaluation, "SUB").UsedKw, "transformer usage counted once per load");
        Equal(50L, SourceUsage(evaluation, "SOURCE").UsedKw, "source usage");
        Equal(50L, evaluation.TotalDeliveredKw, "shared total delivered");
        Equal(evaluation.TotalDeliveredKw, evaluation.TotalGenerationKw, "shared conservation");

        ReleaseWorldDefinition plannedConnection = world with
        {
            Nodes = world.Nodes.Append(Node("PLANNED_POLE", Pole3ClassId, 6, 4)).ToArray(),
            Edges = world.Edges.Append(
                new ReleaseEdgeDefinition(
                    "E_PLANNED",
                    Line100ClassId,
                    "POLE",
                    "PLANNED_POLE",
                    false)).ToArray(),
        };
        ReleaseNetworkEvaluation plannedEvaluation = ReleaseNetworkEvaluator.Evaluate(plannedConnection);
        Equal(3, NodeUsage(plannedEvaluation, "POLE").ConnectionCount,
            "planned line must reserve a connection slot");
        Check(!EdgeUsage(plannedEvaluation, "E_PLANNED").Available,
            "planned line entered the supply graph");

        foreach (ReleaseEdgeUsage edge in evaluation.Edges)
        {
            Check(edge.UsedKw <= edge.RatingKw, $"edge {edge.EdgeId} exceeds rating");
        }
        foreach (ReleaseNodeUsage node in evaluation.Nodes)
        {
            Check(node.UsedKw <= node.RatingKw, $"node {node.NodeId} exceeds rating");
        }
    }

    private void CheckPrioritySourceOrderAndConservation()
    {
        ReleaseWorldDefinition priorityWorld = PriorityWorld();
        ReleaseNetworkEvaluation priority = ReleaseNetworkEvaluator.Evaluate(priorityWorld);
        ReleaseLoadSupply high = LoadSupply(priority, "Z_HIGH_PRIORITY");
        ReleaseLoadSupply low = LoadSupply(priority, "A_LOW_PRIORITY");
        Equal(30L, high.DeliveredKw, "life-safety load must win shared bottleneck");
        Equal(0L, low.DeliveredKw, "lower-priority load must not take partial supply");
        Equal(ReleaseSupplyFailureKind.EdgeCapacity, low.Failure.Kind, "priority loser failure");
        Equal(30L, priority.TotalDeliveredKw, "priority delivered total");
        Equal(priority.TotalDeliveredKw, priority.TotalGenerationKw, "priority conservation");

        ReleaseWorldDefinition sourceWorld = SourceOrderWorld();
        ReleaseNetworkEvaluation sourceEvaluation = ReleaseNetworkEvaluator.Evaluate(sourceWorld);
        ReleaseLoadSupply supplied = LoadSupply(sourceEvaluation, "LOAD");
        Equal("SOURCE_FIRST", supplied.SourceId, "dispatch order must precede source ID/path order");
        Equal(40L, SourceUsage(sourceEvaluation, "SOURCE_FIRST").UsedKw, "first source usage");
        Equal(0L, SourceUsage(sourceEvaluation, "SOURCE_SECOND").UsedKw, "second source usage");
        Equal(sourceEvaluation.TotalDeliveredKw, sourceEvaluation.TotalGenerationKw, "source conservation");
        Equal(
            sourceEvaluation.TotalGenerationKw,
            sourceEvaluation.Sources.Sum(source => source.UsedKw),
            "generation usage sum");

        ReleaseLoadSupply connectedFailure = LoadSupply(
            ReleaseNetworkEvaluator.Evaluate(DisconnectedFirstSourceWorld()),
            "LOAD");
        Equal(
            ReleaseSupplyFailureKind.EdgeCapacity,
            connectedFailure.Failure.Kind,
            "disconnected earlier source must not own the failure reason");
        Equal(
            "E_CONNECTED",
            connectedFailure.Failure.AssetId,
            "failure must identify the first bottleneck on a connected source path");

        ReleaseLoadSupply exhaustedFirstFailure = LoadSupply(
            ReleaseNetworkEvaluator.Evaluate(ExhaustedFirstSourceBottleneckWorld()),
            "Z_TARGET");
        Equal(
            ReleaseSupplyFailureKind.EdgeCapacity,
            exhaustedFirstFailure.Failure.Kind,
            "later adequate source path bottleneck must outrank earlier exhausted source");
        Equal(
            "E_SECOND_TARGET",
            exhaustedFirstFailure.Failure.AssetId,
            "later source failure must identify its exact path bottleneck");
        Equal(
            "SOURCE_SECOND",
            exhaustedFirstFailure.Failure.AttemptedSourceId,
            "path bottleneck must identify the attempted source");
        Equal(10L, exhaustedFirstFailure.Failure.ShortfallKw,
            "later source path bottleneck shortfall");
    }

    private void CheckServiceAreaAndDedicatedLoads()
    {
        ReleaseWorldDefinition world = MixedConnectionWorld();
        ReleaseNetworkEvaluation evaluation = ReleaseNetworkEvaluator.Evaluate(world);
        ReleaseLoadSupply service = LoadSupply(evaluation, "SERVICE_INSIDE");
        ReleaseLoadSupply dedicated = LoadSupply(evaluation, "DEDICATED");

        Equal(20L, service.DeliveredKw, "inside service-area load");
        Equal("SUB", service.EndpointNodeId, "service-area endpoint");
        Equal(ReleaseSupplyFailureKind.None, service.Failure.Kind, "inside service failure");
        Equal(15L, dedicated.DeliveredKw, "dedicated load");
        Equal("DEDICATED_NODE", dedicated.EndpointNodeId, "dedicated endpoint");
        Equal(20L, NodeUsage(evaluation, "SUB").UsedKw, "dedicated load must not reserve transformer");
        Equal(35L, evaluation.TotalDeliveredKw, "mixed connection delivered total");

        ReleaseNetworkEvaluation substationOutage = ReleaseNetworkEvaluator.Evaluate(
            world,
            Contingency(nodeIds: Set("SUB")));
        ReleaseLoadSupply unavailableService = LoadSupply(substationOutage, "SERVICE_INSIDE");
        Equal(0L, unavailableService.DeliveredKw, "unavailable service-area substation");
        Equal(
            ReleaseSupplyFailureKind.NoEligibleSubstation,
            unavailableService.Failure.Kind,
            "unavailable service-area failure");
        Equal(15L, LoadSupply(substationOutage, "DEDICATED").DeliveredKw,
            "substation outage interrupted dedicated load");

        ReleaseWorldDefinition noSubstation = World(
            "NO_SUBSTATION",
            nodes: [Node("S", SourceClassId, 0, 0)],
            edges: [],
            sources: [Source("SOURCE", "S", 0, 100)],
            loads: [ServiceLoad("SERVICE", 20, 4, 4)]);
        ReleaseLoadSupply noEligible = LoadSupply(
            ReleaseNetworkEvaluator.Evaluate(noSubstation),
            "SERVICE");
        Equal(0L, noEligible.DeliveredKw,
            "service load without a built substation must remain a valid unsupplied state");
        Equal(ReleaseSupplyFailureKind.NoEligibleSubstation, noEligible.Failure.Kind,
            "missing service substation failure reason");
    }

    private void CheckCrossingIsNotConnection()
    {
        ReleaseWorldDefinition world = CrossingWorld();
        ReleaseWorldLoader.Validate(world);
        ReleaseNetworkEvaluation evaluation = ReleaseNetworkEvaluator.Evaluate(world);
        ReleaseLoadSupply supply = LoadSupply(evaluation, "LOAD");

        Equal(0L, supply.DeliveredKw, "geometric crossing energized disconnected load");
        Equal(ReleaseSupplyFailureKind.Disconnected, supply.Failure.Kind, "crossing failure kind");
        Equal(0L, evaluation.TotalGenerationKw, "crossing generated power");
        Check(!supply.PathNodeIds.Contains("SOURCE_END", StringComparer.Ordinal),
            "crossing fabricated a shared node");
    }

    private void CheckOutageRerouting()
    {
        ReleaseWorldDefinition world = OutageWorld();
        ReleaseLoadSupply normal = LoadSupply(ReleaseNetworkEvaluator.Evaluate(world), "LOAD");
        SequenceEqual(
            ["A_UPPER_IN", "A_UPPER_OUT"],
            normal.PathEdgeIds,
            "normal outage-world route");

        ReleaseContingency edgeOutage = Contingency(edgeIds: Set("A_UPPER_IN"));
        AssertLowerReroute(world, edgeOutage, "single-edge outage");

        ReleaseContingency nodeOutage = Contingency(nodeIds: Set("UPPER"));
        AssertLowerReroute(world, nodeOutage, "single-node outage");

        ReleaseContingency polygonOutage = Contingency(riskAreaIds: Set("UPPER_CORRIDOR"));
        ReleaseNetworkEvaluation polygonEvaluation = ReleaseNetworkEvaluator.Evaluate(world, polygonOutage);
        AssertLowerReroute(world, polygonOutage, "polygon outage");
        Check(!EdgeUsage(polygonEvaluation, "A_UPPER_IN").Available, "polygon kept intersecting edge available");
        Check(!NodeUsage(polygonEvaluation, "UPPER").Available, "polygon kept enclosed node available");
        Check(EdgeUsage(polygonEvaluation, "B_LOWER_IN").Available, "polygon removed safe lower edge");
    }

    private void CheckImmutabilityAndDeterminism()
    {
        string fixtureBefore = JsonSerializer.Serialize(_fixture);
        ReleaseNetworkEvaluation fixtureEvaluation = ReleaseNetworkEvaluator.Evaluate(_fixture);
        string fixtureResult = JsonSerializer.Serialize(fixtureEvaluation);
        Equal(fixtureEvaluation.TotalDeliveredKw, fixtureEvaluation.TotalGenerationKw,
            "fixture conservation");
        Check(fixtureEvaluation.Loads.All(load => load.DeliveredKw == 0 || load.DeliveredKw == load.DemandKw),
            "fixture contains partial load delivery");

        for (int iteration = 0; iteration < 20; iteration++)
        {
            Equal(fixtureBefore, JsonSerializer.Serialize(_fixture), "evaluation mutated fixture");
            Equal(
                fixtureResult,
                JsonSerializer.Serialize(ReleaseNetworkEvaluator.Evaluate(_fixture)),
                $"fixture evaluation changed on iteration {iteration}");
        }

        var mutableEdges = new HashSet<string>(StringComparer.Ordinal) { "A_UPPER_IN" };
        ReleaseContingency copied = new(
            new HashSet<string>(StringComparer.Ordinal),
            mutableEdges,
            new HashSet<string>(StringComparer.Ordinal));
        mutableEdges.Clear();
        Check(copied.UnavailableEdgeIds.Contains("A_UPPER_IN"), "contingency retained caller-owned set");

        ReleaseWorldDefinition originalWorld = OutageWorld();
        var mutableNodes = originalWorld.Nodes.ToList();
        ReleaseWorldDefinition copiedWorld = originalWorld with { Nodes = mutableNodes };
        int copiedCount = copiedWorld.Nodes.Count;
        mutableNodes.Clear();
        Equal(copiedCount, copiedWorld.Nodes.Count, "world retained caller-owned node list");
    }

    private void CheckConstructionNodeLifecycle()
    {
        var session = new ReleaseConstructionSession(_fixture);
        ReleaseConstructionSnapshot initial = session.GetSnapshot();
        Equal(ReleaseConstructionPhase.Ready, initial.Phase, "construction initial phase");

        ReleaseNodePlacementPreview waterPreview = session.PreviewNodePlacement(
            "SUBSTATION_SMALL",
            new ReleasePoint(12, 10));
        Check(!waterPreview.Accepted, "water substation preview accepted");
        Equal(ReleaseConstructionError.WaterSurface, waterPreview.Error,
            "water substation preview error");
        ReleaseNodePlacementPreview waterBoundary = session.PreviewNodePlacement(
            "SUBSTATION_SMALL",
            new ReleasePoint(10, 0));
        Check(!waterBoundary.Accepted, "water polygon boundary accepted");
        Equal(ReleaseConstructionError.WaterSurface, waterBoundary.Error,
            "water polygon boundary error");
        ReleaseConstructionCommandResult waterPlacement = session.SetNodeDraft(
            "SUBSTATION_SMALL",
            new ReleasePoint(12, 10));
        Check(!waterPlacement.Accepted, "water substation placement accepted");
        Equal(ReleaseConstructionError.WaterSurface, waterPlacement.Error,
            "water substation placement error");
        Equal(JsonSerializer.Serialize(initial), JsonSerializer.Serialize(session.GetSnapshot()),
            "rejected water substation changed state");

        Check(session.PreviewNodePlacement(
            "SUBSTATION_SMALL",
            new ReleasePoint(24, 6)).Accepted, "valid substation preview rejected");
        Check(session.SetNodeDraft(
            "SUBSTATION_SMALL",
            new ReleasePoint(24, 6)).Accepted, "substation draft rejected");
        Check(session.SetNodeDraft(
            "SUBSTATION_SMALL",
            new ReleasePoint(25, 6)).Accepted, "substation draft move rejected");
        Equal(new ReleasePoint(25, 6), session.GetSnapshot().NodeDraft!.Position,
            "substation draft did not move");
        Check(session.CancelNodeDraft().Accepted, "substation draft cancel rejected");
        Equal(ReleaseConstructionPhase.Ready, session.GetSnapshot().Phase,
            "substation cancel phase");
        Equal(initial.World.Nodes.Count, session.GetSnapshot().World.Nodes.Count,
            "substation cancel changed world");

        Check(session.SetNodeDraft(
            "SUBSTATION_SMALL",
            new ReleasePoint(25, 6)).Accepted, "final substation draft rejected");
        ReleaseConstructionQuote quote = session.PreviewNodeOrder();
        Check(quote.Accepted, "substation quote rejected");
        Equal(2_200_000L, quote.CostCashUnit, "substation quote cost");
        Equal(720L, quote.BuildMinutes, "substation quote duration");
        Equal(720L, quote.CompletionMinute, "substation quote completion");

        Check(session.OrderNode().Accepted, "substation order rejected");
        ReleaseConstructionSnapshot building = session.GetSnapshot();
        Equal(ReleaseConstructionPhase.NodeBuilding, building.Phase, "substation building phase");
        Check(building.World.Nodes is System.Collections.IList { IsReadOnly: true },
            "snapshot world nodes are externally mutable");
        Check(building.ActiveConstruction!.NodeIds is System.Collections.IList { IsReadOnly: true },
            "snapshot construction targets are externally mutable");
        ReleaseNodeDefinition planned = building.World.Nodes.Single(node =>
            node.Position == new ReleasePoint(25, 6));
        Check(!planned.Commissioned, "ordered substation commissioned early");
        Equal(initial.Evaluation.TotalDeliveredKw, building.Evaluation.TotalDeliveredKw,
            "ordered substation changed supply before completion");

        Check(session.AdvanceToConstructionCompletion().Accepted,
            "substation completion rejected");
        ReleaseConstructionSnapshot completed = session.GetSnapshot();
        Equal(ReleaseConstructionPhase.Ready, completed.Phase, "substation completed phase");
        Equal(720L, completed.Minute, "substation completion minute");
        Check(completed.World.Nodes.Single(node => node.NodeId == planned.NodeId).Commissioned,
            "substation did not commission atomically");
    }

    private void CheckConstructionLineBranchMerge()
    {
        var session = new ReleaseConstructionSession(_fixture);
        Check(session.StartLineDraft(
            "SOUTH_SUBSTATION",
            "LINE_REINFORCED",
            "POLE_STANDARD").Accepted, "line draft start rejected");
        Check(session.AddLinePoint(new ReleasePoint(17, 14)).Accepted,
            "first line pole rejected");
        ReleaseConstructionCommandResult end = session.AddLinePoint(new ReleasePoint(17, 10));
        Check(end.Accepted, "line endpoint rejected");
        Equal("EAST_SUBSTATION", session.GetSnapshot().LineDraft!.EndNodeId,
            "line did not end at existing substation");
        Check(session.UndoLinePoint().Accepted, "line endpoint undo rejected");
        Check(session.GetSnapshot().LineDraft!.EndNodeId is null,
            "line endpoint undo did not reopen draft");
        Check(session.AddLinePoint(new ReleasePoint(17, 10)).Accepted,
            "line endpoint re-add rejected");

        ReleaseConstructionQuote quote = session.PreviewLineOrder();
        Check(quote.Accepted, "line quote rejected");
        Equal(1_338_600L, quote.CostCashUnit, "line quote cost");
        Equal(16_338L, quote.BuildMinutes, "line quote duration");
        Equal(16_338L, quote.CompletionMinute, "line quote completion");

        long deliveredBefore = session.GetSnapshot().Evaluation.TotalDeliveredKw;
        Check(session.OrderLine().Accepted, "line order rejected");
        ReleaseConstructionSnapshot building = session.GetSnapshot();
        Equal(ReleaseConstructionPhase.LineBuilding, building.Phase, "line building phase");
        Equal(1, building.ActiveConstruction!.NodeIds.Count, "planned pole count");
        Equal(2, building.ActiveConstruction.EdgeIds.Count, "planned edge count");
        Check(building.ActiveConstruction.EdgeIds.All(edgeId =>
            !EdgeUsage(building.Evaluation, edgeId).Available),
            "ordered line entered supply before atomic completion");
        Equal(3, NodeUsage(building.Evaluation, "SOUTH_SUBSTATION").ConnectionCount,
            "planned branch did not reserve connection slot");
        Equal(deliveredBefore, building.Evaluation.TotalDeliveredKw,
            "planned line changed supply before completion");

        Check(session.AdvanceToConstructionCompletion().Accepted,
            "line completion rejected");
        ReleaseConstructionSnapshot completed = session.GetSnapshot();
        Equal(16_338L, completed.Minute, "line completion minute");
        Check(completed.ActiveConstruction is null, "line construction remained active");
        Check(building.ActiveConstruction.EdgeIds.All(edgeId =>
            EdgeUsage(completed.Evaluation, edgeId).Available),
            "line edges did not commission atomically");
        Equal(3, NodeUsage(completed.Evaluation, "SOUTH_SUBSTATION").ConnectionCount,
            "completed branch degree");
        Equal(2, NodeUsage(completed.Evaluation, "EAST_SUBSTATION").ConnectionCount,
            "completed merge degree");
    }

    private void CheckConstructionRejectionAndAtomicity()
    {
        var session = new ReleaseConstructionSession(_fixture);
        string initial = JsonSerializer.Serialize(session.GetSnapshot());
        ReleaseConstructionCommandResult occupied = session.SetNodeDraft(
            "SUBSTATION_SMALL",
            new ReleasePoint(6, 10));
        Check(!occupied.Accepted, "occupied node placement accepted");
        Equal(ReleaseConstructionError.PositionOccupied, occupied.Error,
            "occupied node placement error");
        Equal(initial, JsonSerializer.Serialize(session.GetSnapshot()),
            "rejected node placement changed state");

        ReleaseConstructionCommandResult fullStart = session.StartLineDraft(
            "CENTRAL_JUNCTION",
            "LINE_REINFORCED",
            "POLE_STANDARD");
        Check(!fullStart.Accepted, "full branch node accepted another line");
        Equal(ReleaseConstructionError.ConnectionLimit, fullStart.Error,
            "full branch connection error");
        Equal(initial, JsonSerializer.Serialize(session.GetSnapshot()),
            "rejected line start changed state");

        ReleaseLineStartPreview fullStartPreview = session.PreviewLineStart(
            "CENTRAL_JUNCTION",
            "LINE_REINFORCED",
            "POLE_STANDARD");
        Check(!fullStartPreview.Accepted, "full branch start preview accepted");
        Equal(ReleaseConstructionError.ConnectionLimit, fullStartPreview.Error,
            "full branch start preview error");
        Equal(initial, JsonSerializer.Serialize(session.GetSnapshot()),
            "rejected line start preview changed state");

        Check(session.StartLineDraft(
            "SOUTH_JUNCTION",
            "LINE_REINFORCED",
            "POLE_STANDARD").Accepted, "rejection test line start");
        string beforeWaterPoint = JsonSerializer.Serialize(session.GetSnapshot());
        ReleaseConstructionCommandResult waterPoint = session.AddLinePoint(
            new ReleasePoint(12, 13));
        Check(!waterPoint.Accepted, "water intermediate pole accepted");
        Equal(ReleaseConstructionError.WaterSurface, waterPoint.Error,
            "water intermediate pole error");
        Equal(beforeWaterPoint, JsonSerializer.Serialize(session.GetSnapshot()),
            "rejected water intermediate pole changed state");
        Check(session.AddLinePoint(new ReleasePoint(10, 14)).Accepted,
            "rejection test first point");
        string beforeRejectedPoint = JsonSerializer.Serialize(session.GetSnapshot());
        ReleaseConstructionCommandResult duplicate = session.AddLinePoint(new ReleasePoint(10, 14));
        Check(!duplicate.Accepted, "duplicate line point accepted");
        Equal(ReleaseConstructionError.PositionOccupied, duplicate.Error,
            "duplicate line point error");
        Equal(beforeRejectedPoint, JsonSerializer.Serialize(session.GetSnapshot()),
            "rejected duplicate point changed state");
        ReleaseConstructionCommandResult tooFar = session.AddLinePoint(new ReleasePoint(25, 20));
        Check(!tooFar.Accepted, "overlong line span accepted");
        Equal(ReleaseConstructionError.SpanTooLong, tooFar.Error,
            "overlong line span error");
        Equal(beforeRejectedPoint, JsonSerializer.Serialize(session.GetSnapshot()),
            "rejected long span changed state");
        Check(session.CancelLineDraft().Accepted, "line cancel rejected");
        Equal(initial, JsonSerializer.Serialize(session.GetSnapshot()),
            "cancelled line draft changed authoritative state");

        var first = new ReleaseConstructionSession(_fixture);
        var second = new ReleaseConstructionSession(_fixture);
        CompleteCanonicalLine(first);
        CompleteCanonicalLine(second);
        Equal(
            JsonSerializer.Serialize(first.GetSnapshot()),
            JsonSerializer.Serialize(second.GetSnapshot()),
            "identical construction commands were nondeterministic");
    }

    private static void CompleteCanonicalLine(ReleaseConstructionSession session)
    {
        _ = session.StartLineDraft("SOUTH_SUBSTATION", "LINE_REINFORCED", "POLE_STANDARD");
        _ = session.AddLinePoint(new ReleasePoint(17, 14));
        _ = session.AddLinePoint(new ReleasePoint(17, 10));
        _ = session.OrderLine();
        _ = session.AdvanceToConstructionCompletion();
    }

    private void CheckCampaignLoaderAndContent()
    {
        ReleaseCampaignDefinition fromText = ReleaseCampaignLoader.Load(_campaignJson, _fixture);
        ReleaseCampaignDefinition fromBytes = ReleaseCampaignLoader.Load(
            Encoding.UTF8.GetBytes(_campaignJson),
            _fixture);
        Equal(_campaign.CampaignId, fromText.CampaignId, "campaign text loader ID");
        Equal(_campaign.CampaignId, fromBytes.CampaignId, "campaign UTF-8 loader ID");
        Equal(8, _campaign.Chapters.Count, "campaign chapter count");
        SequenceEqual(
            [
                "첫 불빛",
                "두 번째 심장",
                "열돔 아래",
                "북안 확장",
                "공유 구간",
                "범람 예보",
                "계획 정지",
                "청류 비상전력",
            ],
            _campaign.Chapters.Select(chapter => chapter.DisplayName).ToArray(),
            "campaign chapter order");
        SequenceEqual(
            [1, 2, 3, 5, 5, 5, 5, 5],
            _campaign.Chapters.Select(chapter => chapter.ActiveLoads.Count).ToArray(),
            "future loads must stay hidden until introduced");
        SequenceEqual(
            ["EDGE_WEST_TRUNK", "EDGE_NORTH_HOSPITAL"],
            _campaign.InitialEdgeIds,
            "campaign sparse initial edge set");

        HashSet<string> allowedSpeakers = new(StringComparer.Ordinal)
        {
            "운영센터장 윤서진",
            "계통운영관 강민호",
            "청류의료원 시설책임자 박지현",
            "재난대응관 이도윤",
        };
        for (int index = 0; index < _campaign.Chapters.Count; index++)
        {
            ReleaseCampaignChapter chapter = _campaign.Chapters[index];
            Check(chapter.Event is not null, $"{chapter.DisplayName}: event card missing");
            if (index < 3)
            {
                Check(chapter.ConnectionRequirements.Count != 0,
                    $"{chapter.DisplayName}: tutorial connection requirement missing");
            }
            else
            {
                Equal(0, chapter.ConnectionRequirements.Count,
                    $"{chapter.DisplayName}: main chapter prescribed one exact construction");
            }
            Check(allowedSpeakers.Contains(chapter.Briefing.Speaker),
                $"{chapter.DisplayName}: unknown briefing speaker");
            Check(allowedSpeakers.Contains(chapter.Event!.Story.Speaker),
                $"{chapter.DisplayName}: unknown event speaker");
            Check(allowedSpeakers.Contains(chapter.Result.Speaker),
                $"{chapter.DisplayName}: unknown result speaker");
        }

        ReleaseCampaignSnapshot initial = NewCampaignRun().GetSnapshot();
        Equal(2, initial.Construction.World.Edges.Count, "campaign initial edge count");
        Equal(1, initial.Construction.World.Loads.Count, "campaign initial visible load count");
        Equal("EAST_RESIDENTIAL", initial.Construction.World.Loads[0].LoadId,
            "campaign first visible load");

        ExpectCampaignRejected(
            "unknown campaign root field",
            root => root["unexpected"] = true);
        ExpectCampaignRejected(
            "campaign must contain eight chapters",
            root => Array(root, "chapters").RemoveAt(7));
        ExpectCampaignRejected(
            "unknown campaign event asset",
            root => Array(
                Object(Array(root, "chapters")[1]!, "event"),
                "unavailableNodeIds").Add("MISSING_NODE"));
        ExpectCampaignRejected(
            "missing required campaign array",
            root => Object(Array(root, "chapters")[0]!).Remove("requiredEventLoadIds"));
        ExpectCampaignRejected(
            "incident edge absent from opening network",
            root => Object(Array(root, "chapters")[0]!, "event")["unavailableEdgeIds"] =
                new JsonArray("EDGE_CENTRAL_NORTH"));
        ExpectCampaignRejected(
            "cumulative campaign grant overflow",
            root => root["initialCashUnit"] = long.MaxValue);
    }

    private void CheckCampaignEightChapterWitness()
    {
        ReleaseCampaignRun run = NewCampaignRun();
        Equal(4_500_000L, run.GetSnapshot().CashUnit, "first chapter opening cash");

        AssertCurrentChapterNeedsWork(run, "PROLOGUE_FIRST_LIGHT");
        BuildLine(run, "CENTRAL_JUNCTION", "LINE_REINFORCED", "POLE_REINFORCED",
            new ReleasePoint(9, 13));
        BuildLine(run, "SOUTH_JUNCTION", "LINE_REINFORCED", "POLE_REINFORCED",
            new ReleasePoint(13, 10));
        BuildLine(run, "RIVER_MERGE", "LINE_REINFORCED", "POLE_REINFORCED",
            new ReleasePoint(17, 10));
        CompleteCampaignChapter(run, "PROLOGUE_FIRST_LIGHT");

        AssertCurrentChapterNeedsWork(run, "PROLOGUE_SECOND_HEART");
        BuildLine(run, "EAST_SUBSTATION", "LINE_REINFORCED", "POLE_REINFORCED",
            new ReleasePoint(17, 7),
            new ReleasePoint(17, 5));
        BuildLine(run, "CENTRAL_JUNCTION", "LINE_REINFORCED", "POLE_REINFORCED",
            new ReleasePoint(9, 7));
        BuildLine(run, "NORTH_JUNCTION", "LINE_REINFORCED", "POLE_REINFORCED",
            new ReleasePoint(13, 5));
        CompleteCampaignChapter(run, "PROLOGUE_SECOND_HEART");

        AssertCurrentChapterNeedsWork(run, "PROLOGUE_HEAT_DOME");
        BuildLine(run, "SOUTH_SOURCE_NODE", "LINE_REINFORCED", "POLE_REINFORCED",
            new ReleasePoint(13, 15));
        BuildLine(run, "SOUTH_SUBSTATION", "LINE_REINFORCED", "POLE_REINFORCED",
            new ReleasePoint(9, 13));
        CompleteCampaignChapter(run, "PROLOGUE_HEAT_DOME");

        ReleaseCampaignRun alternateNorthBank = ReleaseCampaignRun.Restore(
            _campaign,
            _fixture,
            _campaignSha256,
            _worldSha256,
            run.CaptureSave());
        AssertCurrentChapterNeedsWork(alternateNorthBank, "CHAPTER_NORTH_BANK");
        BuildLine(alternateNorthBank, "NORTH_JUNCTION", "LINE_STANDARD", "POLE_STANDARD",
            new ReleasePoint(6, 6));
        CompleteCampaignChapter(alternateNorthBank, "CHAPTER_NORTH_BANK");
        Equal("CHAPTER_SHARED_CORRIDOR", alternateNorthBank.GetSnapshot().Chapter.ChapterId,
            "alternate north-bank route did not reach the next chapter");

        AssertCurrentChapterNeedsWork(run, "CHAPTER_NORTH_BANK");
        BuildLine(run, "CENTRAL_JUNCTION", "LINE_STANDARD", "POLE_STANDARD",
            new ReleasePoint(6, 6));
        CompleteCampaignChapter(run, "CHAPTER_NORTH_BANK");

        AssertCurrentChapterNeedsWork(run, "CHAPTER_SHARED_CORRIDOR");
        BuildLine(run, "WEST_SOURCE_NODE", "LINE_REINFORCED", "POLE_REINFORCED",
            new ReleasePoint(5, 12),
            new ReleasePoint(9, 13));
        CompleteCampaignChapter(run, "CHAPTER_SHARED_CORRIDOR");

        AssertCurrentChapterNeedsWork(run, "CHAPTER_FLOOD_FORECAST");
        BuildLine(run, "NORTH_SUBSTATION", "LINE_REINFORCED", "POLE_REINFORCED",
            new ReleasePoint(16, 6),
            new ReleasePoint(17, 10));
        CompleteCampaignChapter(run, "CHAPTER_FLOOD_FORECAST");

        AssertCurrentChapterNeedsWork(run, "CHAPTER_PLANNED_OUTAGE");
        BuildLine(run, "SOUTH_SUBSTATION", "LINE_REINFORCED", "POLE_REINFORCED",
            new ReleasePoint(17, 14),
            new ReleasePoint(17, 10));
        CompleteCampaignChapter(run, "CHAPTER_PLANNED_OUTAGE");

        AssertCurrentChapterNeedsWork(run, "CHAPTER_EMERGENCY_POWER");
        BuildLine(run, "SOUTH_SUBSTATION", "LINE_REINFORCED", "POLE_REINFORCED",
            new ReleasePoint(17, 13),
            new ReleasePoint(19, 9),
            new ReleasePoint(17, 5));
        CompleteCampaignChapter(run, "CHAPTER_EMERGENCY_POWER");

        ReleaseCampaignSnapshot final = run.GetSnapshot();
        Check(final.CampaignComplete, "campaign witness did not reach the epilogue boundary");
        Check(final.CashUnit >= 0, "campaign witness ended with negative cash");
        Equal(8_000L, final.NormalEvaluation.TotalDeliveredKw,
            "final maximum-demand normal supply");
        Equal(1_200L, LoadSupply(final.EventEvaluation, "HOSPITAL_LIFE_SAFETY").DeliveredKw,
            "final incident hospital supply");
        Equal(900L, LoadSupply(final.EventEvaluation, "WATER_ESSENTIAL").DeliveredKw,
            "final incident water supply");
        Equal(3, NodeUsage(final.NormalEvaluation, "HOSPITAL_TERMINAL").ConnectionCount,
            "final hospital route count");

        Check(run.CanRewindToPreviousChapter,
            "completed campaign did not offer previous-chapter recovery");
        ReleaseCampaignSnapshot previous = run.RewindToPreviousChapterStart();
        Equal("CHAPTER_PLANNED_OUTAGE", previous.Chapter.ChapterId,
            "completed campaign recovery did not return to the previous chapter start");
        Check(!previous.CampaignComplete,
            "previous-chapter recovery retained campaign completion state");
        Equal(previous.ChapterStartCommandCount, previous.CommandCount,
            "previous-chapter recovery retained later commands");

        ReleaseCampaignSnapshot repeated = run.RewindToPreviousChapterStart();
        Equal("CHAPTER_FLOOD_FORECAST", repeated.Chapter.ChapterId,
            "previous-chapter recovery could not be repeated");
        Equal(repeated.ChapterStartCommandCount, repeated.CommandCount,
            "repeated previous-chapter recovery retained later commands");
    }

    private void CheckCampaignSaveAndRestart()
    {
        ReleaseCampaignRun run = NewCampaignRun();
        ExecuteCampaign(run, new ReleaseCampaignCommand(
            ReleaseCampaignCommandKind.StartLineDraft,
            StartNodeId: "CENTRAL_JUNCTION",
            LineClassId: "LINE_REINFORCED",
            PoleClassId: "POLE_REINFORCED"), "save witness line start");
        ExecuteCampaign(run, new ReleaseCampaignCommand(
            ReleaseCampaignCommandKind.AddLinePoint,
            Position: new ReleasePoint(9, 13)), "save witness line endpoint");

        ReleaseCampaignSave captured = run.CaptureSave();
        byte[] encoded = ReleaseCampaignSaveCodec.Serialize(captured);
        ReleaseCampaignSave decoded = ReleaseCampaignSaveCodec.Deserialize(encoded);
        ReleaseCampaignRun restored = ReleaseCampaignRun.Restore(
            _campaign,
            _fixture,
            _campaignSha256,
            _worldSha256,
            decoded);
        Equal(
            JsonSerializer.Serialize(run.GetSnapshot()),
            JsonSerializer.Serialize(restored.GetSnapshot()),
            "campaign fresh replay snapshot");
        SequenceEqual(encoded, ReleaseCampaignSaveCodec.Serialize(decoded),
            "campaign save codec determinism");

        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"gridworks-release-checks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            string savePath = Path.Combine(
                temporaryDirectory,
                ReleaseCampaignPersistenceStore.SaveFileName);
            ReleaseCampaignPersistenceStore.Save(savePath, captured);
            ReleaseCampaignSaveLoadResult loaded = ReleaseCampaignPersistenceStore.Load(savePath);
            Equal(ReleaseDocumentLoadStatus.Loaded, loaded.Status,
                "campaign persisted save status");
            Equal(captured.Commands.Count, loaded.Save!.Commands.Count,
                "campaign persisted command count");
            Check(!File.Exists(savePath + ".tmp"),
                "campaign persisted temporary file remained after replacement");

            JsonObject missingCommands = ParseObject(Encoding.UTF8.GetString(encoded));
            missingCommands.Remove("commands");
            File.WriteAllText(savePath, missingCommands.ToJsonString());
            ReleaseCampaignSaveLoadResult invalid =
                ReleaseCampaignPersistenceStore.Load(savePath);
            Equal(ReleaseDocumentLoadStatus.Invalid, invalid.Status,
                "missing commands must be an invalid save, not a startup exception");
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }

        ExecuteCampaign(restored, new ReleaseCampaignCommand(ReleaseCampaignCommandKind.OrderLine),
            "restored line order");
        ExecuteCampaign(restored, new ReleaseCampaignCommand(ReleaseCampaignCommandKind.AdvanceConstruction),
            "restored line completion");
        BuildLine(restored, "SOUTH_JUNCTION", "LINE_REINFORCED", "POLE_REINFORCED",
            new ReleasePoint(13, 10));
        BuildLine(restored, "RIVER_MERGE", "LINE_REINFORCED", "POLE_REINFORCED",
            new ReleasePoint(17, 10));
        CompleteCampaignChapter(restored, "PROLOGUE_FIRST_LIGHT");

        ReleaseCampaignSnapshot chapterStart = restored.GetSnapshot();
        int inheritedEdgeCount = chapterStart.Construction.World.Edges.Count;
        long chapterStartCash = chapterStart.CashUnit;
        BuildLine(restored, "EAST_SUBSTATION", "LINE_REINFORCED", "POLE_REINFORCED",
            new ReleasePoint(17, 7),
            new ReleasePoint(17, 5));
        Check(restored.GetSnapshot().Construction.World.Edges.Count > inheritedEdgeCount,
            "second chapter construction did not change carried world");
        ReleaseCampaignSnapshot restarted = restored.RestartChapter();
        Equal("PROLOGUE_SECOND_HEART", restarted.Chapter.ChapterId,
            "chapter restart changed chapter");
        Equal(restarted.ChapterStartCommandCount, restarted.CommandCount,
            "chapter restart did not trim accepted journal");
        Equal(inheritedEdgeCount, restarted.Construction.World.Edges.Count,
            "chapter restart did not remove current-chapter assets");
        Equal(chapterStartCash, restarted.CashUnit,
            "chapter restart did not restore opening cash");
        Equal(ReleaseConstructionPhase.Ready, restarted.Construction.Phase,
            "chapter restart did not restore a safe construction phase");

        Check(restored.CanRewindToPreviousChapter,
            "second chapter did not offer previous-chapter recovery");
        ReleaseCampaignSnapshot rewound = restored.RewindToPreviousChapterStart();
        Equal("PROLOGUE_FIRST_LIGHT", rewound.Chapter.ChapterId,
            "previous-chapter recovery did not return to the first chapter");
        Equal(0, rewound.CommandCount,
            "previous-chapter recovery retained later accepted commands");
        Equal(2, rewound.Construction.World.Edges.Count,
            "previous-chapter recovery did not restore the opening network");
        Equal(4_500_000L, rewound.CashUnit,
            "previous-chapter recovery did not restore opening cash");
        Check(!restored.CanRewindToPreviousChapter,
            "first chapter offered an invalid previous-chapter recovery");

        ExpectPersistenceRejected(
            "campaign hash mismatch",
            () => ReleaseCampaignRun.Restore(
                _campaign,
                _fixture,
                new string('0', 64),
                _worldSha256,
                decoded));
    }

    private ReleaseCampaignRun NewCampaignRun() => new(
        _campaign,
        _fixture,
        _campaignSha256,
        _worldSha256);

    private void AssertCurrentChapterNeedsWork(ReleaseCampaignRun run, string chapterId)
    {
        ReleaseCampaignSnapshot before = run.GetSnapshot();
        Equal(chapterId, before.Chapter.ChapterId, $"{chapterId}: current chapter");
        ReleaseCampaignCommandResult result = run.Execute(
            new ReleaseCampaignCommand(ReleaseCampaignCommandKind.EvaluateChapter));
        Check(!result.Accepted, $"{chapterId}: chapter passed without new work");
        Equal(ReleaseCampaignError.ObjectiveNotMet, result.Error,
            $"{chapterId}: initial chapter rejection");
        Check(result.Assessment is not null && !result.Assessment.Passed,
            $"{chapterId}: missing failed assessment");
        Equal(before.CommandCount, run.GetSnapshot().CommandCount,
            $"{chapterId}: rejected assessment entered journal");
    }

    private void CompleteCampaignChapter(ReleaseCampaignRun run, string chapterId)
    {
        ReleaseCampaignCommandResult result = run.Execute(
            new ReleaseCampaignCommand(ReleaseCampaignCommandKind.EvaluateChapter));
        if (!result.Accepted)
        {
            string detail = result.Assessment is null
                ? result.Error?.ToString() ?? "unknown"
                : $"load={result.Assessment.FailedLoadId}, " +
                  $"event={result.Assessment.FailedDuringEvent}, " +
                  $"connection={result.Assessment.FailedConnectionNodeId}, " +
                  $"failure={result.Assessment.SupplyFailure?.Kind}";
            throw new InvalidOperationException($"{chapterId}: completion rejected ({detail})");
        }
        Check(result.Assessment?.Passed == true, $"{chapterId}: passing assessment missing");
        Equal(chapterId, result.CompletedChapter?.ChapterId,
            $"{chapterId}: completed chapter identity");
        Check(result.Snapshot.CashUnit >= 0, $"{chapterId}: negative carry-over cash");
    }

    private void BuildLine(
        ReleaseCampaignRun run,
        string startNodeId,
        string lineClassId,
        string poleClassId,
        params ReleasePoint[] points)
    {
        ExecuteCampaign(run, new ReleaseCampaignCommand(
            ReleaseCampaignCommandKind.StartLineDraft,
            StartNodeId: startNodeId,
            LineClassId: lineClassId,
            PoleClassId: poleClassId), $"line from {startNodeId}: start");
        foreach (ReleasePoint point in points)
        {
            ExecuteCampaign(run, new ReleaseCampaignCommand(
                ReleaseCampaignCommandKind.AddLinePoint,
                Position: point), $"line from {startNodeId}: add {point}");
        }
        ExecuteCampaign(run, new ReleaseCampaignCommand(ReleaseCampaignCommandKind.OrderLine),
            $"line from {startNodeId}: order");
        ExecuteCampaign(run, new ReleaseCampaignCommand(
            ReleaseCampaignCommandKind.AdvanceConstruction),
            $"line from {startNodeId}: complete");
    }

    private void ExecuteCampaign(
        ReleaseCampaignRun run,
        ReleaseCampaignCommand command,
        string context)
    {
        ReleaseCampaignCommandResult result = run.Execute(command);
        if (!result.Accepted)
        {
            throw new InvalidOperationException(
                $"{context}: rejected ({result.Error}, {result.ConstructionError})");
        }
        _assertionCount++;
    }

    private void ExpectCampaignRejected(string label, Action<JsonObject> mutate)
    {
        JsonObject root = ParseObject(_campaignJson);
        mutate(root);
        try
        {
            _ = ReleaseCampaignLoader.Load(root.ToJsonString(), _fixture);
        }
        catch (ReleaseCampaignValidationException)
        {
            _assertionCount++;
            return;
        }

        throw new InvalidOperationException($"{label}: loader accepted invalid campaign");
    }

    private void ExpectPersistenceRejected(string label, Action action)
    {
        try
        {
            action();
        }
        catch (ReleasePersistenceValidationException)
        {
            _assertionCount++;
            return;
        }

        throw new InvalidOperationException($"{label}: persistence accepted invalid content");
    }

    private void AssertLowerReroute(
        ReleaseWorldDefinition world,
        ReleaseContingency contingency,
        string context)
    {
        ReleaseLoadSupply rerouted = LoadSupply(
            ReleaseNetworkEvaluator.Evaluate(world, contingency),
            "LOAD");
        Equal(20L, rerouted.DeliveredKw, $"{context}: delivered power");
        SequenceEqual(
            ["B_LOWER_IN", "B_LOWER_OUT"],
            rerouted.PathEdgeIds,
            $"{context}: lower route");
    }

    private static ReleaseWorldDefinition BranchMergeWorld()
    {
        return World(
            "BRANCH_MERGE",
            nodes:
            [
                Node("S", SourceClassId, 0, 4),
                Node("A", Pole4ClassId, 2, 4),
                Node("B", Pole3ClassId, 4, 2),
                Node("C", Pole3ClassId, 4, 6),
                Node("M", Pole3ClassId, 6, 4),
                Node("SUB", SubstationClassId, 8, 4),
                Node("D", DedicatedClassId, 4, 4),
            ],
            edges:
            [
                Edge("E_S_A", "S", "A"),
                Edge("E_A_B", "A", "B"),
                Edge("E_A_C", "A", "C"),
                Edge("E_B_M", "B", "M"),
                Edge("E_C_M", "C", "M"),
                Edge("E_M_SUB", "M", "SUB"),
                Edge("E_A_D", "A", "D"),
            ],
            sources: [Source("SOURCE", "S", 0, 100)],
            loads:
            [
                ServiceLoad("SERVICE", 30, 9, 4),
                DedicatedLoad("DEDICATED", 10, "D", 4, 4),
            ]);
    }

    private static ReleaseWorldDefinition ExactSpanWorld(ReleasePoint endpoint)
    {
        return World(
            "EXACT_SPAN",
            nodes:
            [
                Node("S", SourceClassId, 0, 0),
                Node("D", DedicatedClassId, endpoint.X, endpoint.Y),
            ],
            edges: [Edge("E_DIRECT", "S", "D", Line50ClassId)],
            sources: [Source("SOURCE", "S", 0, 100)],
            loads: [DedicatedLoad("LOAD", 50, "D", endpoint.X, endpoint.Y)]);
    }

    private static ReleaseWorldDefinition TieCycleWorld()
    {
        return World(
            "TIE_CYCLE",
            nodes:
            [
                Node("S", SourceClassId, 0, 4),
                Node("UPPER", Pole3ClassId, 2, 2),
                Node("LOWER", Pole3ClassId, 2, 6),
                Node("MERGE", Pole3ClassId, 4, 4),
                Node("D", DedicatedClassId, 6, 4),
            ],
            edges:
            [
                Edge("A_SOURCE_UPPER", "S", "UPPER"),
                Edge("A_UPPER_MERGE", "UPPER", "MERGE"),
                Edge("B_SOURCE_LOWER", "S", "LOWER"),
                Edge("B_LOWER_MERGE", "LOWER", "MERGE"),
                Edge("C_UPPER_LOWER", "UPPER", "LOWER"),
                Edge("E_MERGE_LOAD", "MERGE", "D"),
            ],
            sources: [Source("SOURCE", "S", 0, 100)],
            loads: [DedicatedLoad("LOAD", 20, "D", 6, 4)]);
    }

    private static ReleaseWorldDefinition SharedSubstationWorld()
    {
        return World(
            "SHARED_SUBSTATION",
            nodes:
            [
                Node("S", SourceClassId, 0, 4),
                Node("POLE", Pole3ClassId, 2, 4),
                Node("SUB", SubstationClassId, 4, 4),
            ],
            edges:
            [
                Edge("E_SOURCE_POLE", "S", "POLE"),
                Edge("E_POLE_SUB", "POLE", "SUB"),
            ],
            sources: [Source("SOURCE", "S", 0, 100)],
            loads:
            [
                ServiceLoad("LOAD_A", 20, 5, 3),
                ServiceLoad("LOAD_B", 30, 5, 5),
            ]);
    }

    private static ReleaseWorldDefinition PriorityWorld()
    {
        return World(
            "PRIORITY",
            nodes:
            [
                Node("S", SourceClassId, 0, 4),
                Node("P", Pole3ClassId, 2, 4),
                Node("HIGH", DedicatedClassId, 4, 2),
                Node("LOW", DedicatedClassId, 4, 6),
            ],
            edges:
            [
                Edge("E_SHARED", "S", "P", Line40ClassId),
                Edge("E_HIGH", "P", "HIGH"),
                Edge("E_LOW", "P", "LOW"),
            ],
            sources: [Source("SOURCE", "S", 0, 100)],
            loads:
            [
                DedicatedLoad("Z_HIGH_PRIORITY", 30, "HIGH", 4, 2, ReleaseLoadPriority.LifeSafety),
                DedicatedLoad("A_LOW_PRIORITY", 30, "LOW", 4, 6, ReleaseLoadPriority.Residential),
            ]);
    }

    private static ReleaseWorldDefinition SourceOrderWorld()
    {
        return World(
            "SOURCE_ORDER",
            nodes:
            [
                Node("S_FIRST", SourceClassId, 0, 2),
                Node("S_SECOND", SourceClassId, 0, 6),
                Node("P", Pole3ClassId, 2, 4),
                Node("D", DedicatedClassId, 4, 4),
            ],
            edges:
            [
                Edge("Z_FIRST_PATH", "S_FIRST", "P"),
                Edge("A_SECOND_PATH", "S_SECOND", "P"),
                Edge("E_LOAD", "P", "D"),
            ],
            sources:
            [
                Source("SOURCE_FIRST", "S_FIRST", 0, 40),
                Source("SOURCE_SECOND", "S_SECOND", 1, 40),
            ],
            loads: [DedicatedLoad("LOAD", 40, "D", 4, 4)]);
    }

    private static ReleaseWorldDefinition DisconnectedFirstSourceWorld()
    {
        return World(
            "DISCONNECTED_FIRST_SOURCE",
            nodes:
            [
                Node("S_DISCONNECTED", SourceClassId, 0, 0),
                Node("S_CONNECTED", SourceClassId, 0, 4),
                Node("D", DedicatedClassId, 4, 4),
            ],
            edges: [Edge("E_CONNECTED", "S_CONNECTED", "D", Line40ClassId)],
            sources:
            [
                Source("SOURCE_DISCONNECTED", "S_DISCONNECTED", 0, 10),
                Source("SOURCE_CONNECTED", "S_CONNECTED", 1, 100),
            ],
            loads: [DedicatedLoad("LOAD", 50, "D", 4, 4)]);
    }

    private static ReleaseWorldDefinition ExhaustedFirstSourceBottleneckWorld()
    {
        return World(
            "EXHAUSTED_FIRST_SOURCE_BOTTLENECK",
            nodes:
            [
                Node("S_FIRST", SourceClassId, 0, 2),
                Node("S_SECOND", SourceClassId, 0, 6),
                Node("D_PRIORITY", DedicatedClassId, 4, 2),
                Node("D_TARGET", DedicatedClassId, 4, 6),
            ],
            edges:
            [
                Edge("E_FIRST_PRIORITY", "S_FIRST", "D_PRIORITY"),
                Edge("E_FIRST_TARGET", "S_FIRST", "D_TARGET"),
                Edge("E_SECOND_TARGET", "S_SECOND", "D_TARGET", Line40ClassId),
            ],
            sources:
            [
                Source("SOURCE_FIRST", "S_FIRST", 0, 100),
                Source("SOURCE_SECOND", "S_SECOND", 1, 50),
            ],
            loads:
            [
                DedicatedLoad(
                    "A_PRIORITY",
                    100,
                    "D_PRIORITY",
                    4,
                    2,
                    ReleaseLoadPriority.LifeSafety),
                DedicatedLoad(
                    "Z_TARGET",
                    50,
                    "D_TARGET",
                    4,
                    6,
                    ReleaseLoadPriority.Industrial),
            ]);
    }

    private static ReleaseWorldDefinition MixedConnectionWorld()
    {
        return World(
            "MIXED_CONNECTIONS",
            nodes:
            [
                Node("S", SourceClassId, 0, 4),
                Node("P", Pole3ClassId, 2, 4),
                Node("SUB", SubstationClassId, 4, 4),
                Node("DEDICATED_NODE", DedicatedClassId, 4, 8),
            ],
            edges:
            [
                Edge("E_SOURCE_POLE", "S", "P"),
                Edge("E_POLE_SUB", "P", "SUB"),
                Edge("E_POLE_DEDICATED", "P", "DEDICATED_NODE"),
            ],
            sources: [Source("SOURCE", "S", 0, 100)],
            loads:
            [
                ServiceLoad("SERVICE_INSIDE", 20, 6, 4),
                DedicatedLoad("DEDICATED", 15, "DEDICATED_NODE", 4, 8),
            ]);
    }

    private static ReleaseWorldDefinition CrossingWorld()
    {
        return World(
            "CROSSING",
            nodes:
            [
                Node("S", SourceClassId, 0, 0),
                Node("SOURCE_END", Pole3ClassId, 4, 4),
                Node("ISLAND_START", Pole3ClassId, 0, 4),
                Node("D", DedicatedClassId, 4, 0),
            ],
            edges:
            [
                Edge("E_SOURCE_DIAGONAL", "S", "SOURCE_END"),
                Edge("E_LOAD_DIAGONAL", "ISLAND_START", "D"),
            ],
            sources: [Source("SOURCE", "S", 0, 100)],
            loads: [DedicatedLoad("LOAD", 20, "D", 4, 0)]);
    }

    private static ReleaseWorldDefinition OutageWorld()
    {
        return World(
            "OUTAGE",
            nodes:
            [
                Node("S", SourceClassId, 0, 4),
                Node("UPPER", Pole3ClassId, 4, 2),
                Node("LOWER", Pole3ClassId, 4, 6),
                Node("D", DedicatedClassId, 8, 4),
            ],
            edges:
            [
                Edge("A_UPPER_IN", "S", "UPPER"),
                Edge("A_UPPER_OUT", "UPPER", "D"),
                Edge("B_LOWER_IN", "S", "LOWER"),
                Edge("B_LOWER_OUT", "LOWER", "D"),
            ],
            sources: [Source("SOURCE", "S", 0, 100)],
            loads: [DedicatedLoad("LOAD", 20, "D", 8, 4)],
            riskAreas:
            [
                new ReleaseRiskAreaDefinition(
                    "UPPER_CORRIDOR",
                    "상부 회랑",
                    [
                        new ReleasePoint(1, 1),
                        new ReleasePoint(5, 1),
                        new ReleasePoint(5, 3),
                        new ReleasePoint(1, 3),
                    ]),
            ]);
    }

    private static ReleaseWorldDefinition World(
        string worldId,
        IReadOnlyList<ReleaseNodeDefinition> nodes,
        IReadOnlyList<ReleaseEdgeDefinition> edges,
        IReadOnlyList<ReleaseSourceDefinition> sources,
        IReadOnlyList<ReleaseLoadDefinition> loads,
        IReadOnlyList<ReleaseRiskAreaDefinition>? riskAreas = null)
    {
        return new ReleaseWorldDefinition(
            "gridworks.release.world.v1",
            worldId,
            worldId,
            new ReleaseGridDefinition(0, 0, 32, 20, 4),
            NodeClasses(),
            LineClasses(),
            nodes,
            edges,
            sources,
            loads,
            riskAreas ?? []);
    }

    private static IReadOnlyList<ReleaseNodeClassDefinition> NodeClasses() =>
    [
        new(SourceClassId, "전원 접속점", ReleaseNodeKind.SourceTerminal, 4, null, null, null, 0, 0),
        new(Pole3ClassId, "일반 전신주", ReleaseNodeKind.Pole, 3, 100, null, null, 10, 1),
        new(Pole4ClassId, "보강 전신주", ReleaseNodeKind.Pole, 4, 100, null, null, 20, 1),
        new(SubstationClassId, "배전 변전소", ReleaseNodeKind.Substation, 4, null, 100, 3, 50, 2),
        new(DedicatedClassId, "전용 수요 접속점", ReleaseNodeKind.DedicatedLoadTerminal, 4, null, null, null, 0, 0),
    ];

    private static IReadOnlyList<ReleaseLineClassDefinition> LineClasses() =>
    [
        new(Line100ClassId, "보강 배전선", 100, 6, 1, 1),
        new(Line50ClassId, "일반 배전선", 50, 5, 1, 1),
        new(Line40ClassId, "제한 배전선", 40, 6, 1, 1),
    ];

    private static ReleaseNodeDefinition Node(string id, string classId, int x, int y) =>
        new(id, classId, id, new ReleasePoint(x, y), true);

    private static ReleaseEdgeDefinition Edge(
        string id,
        string from,
        string to,
        string lineClassId = Line100ClassId) =>
        new(id, lineClassId, from, to, true);

    private static ReleaseSourceDefinition Source(
        string id,
        string nodeId,
        int order,
        long capacityKw) =>
        new(id, nodeId, id, order, capacityKw);

    private static ReleaseLoadDefinition ServiceLoad(
        string id,
        long demandKw,
        int x,
        int y,
        ReleaseLoadPriority priority = ReleaseLoadPriority.Residential) =>
        new(
            id,
            id,
            priority,
            demandKw,
            ReleaseLoadConnectionKind.ServiceArea,
            new ReleasePoint(x, y),
            null);

    private static ReleaseLoadDefinition DedicatedLoad(
        string id,
        long demandKw,
        string nodeId,
        int x,
        int y,
        ReleaseLoadPriority priority = ReleaseLoadPriority.Residential) =>
        new(
            id,
            id,
            priority,
            demandKw,
            ReleaseLoadConnectionKind.DedicatedNode,
            new ReleasePoint(x, y),
            nodeId);

    private static ReleaseContingency Contingency(
        IReadOnlySet<string>? nodeIds = null,
        IReadOnlySet<string>? edgeIds = null,
        IReadOnlySet<string>? riskAreaIds = null) =>
        new(
            nodeIds ?? new HashSet<string>(StringComparer.Ordinal),
            edgeIds ?? new HashSet<string>(StringComparer.Ordinal),
            riskAreaIds ?? new HashSet<string>(StringComparer.Ordinal));

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);

    private static ReleaseLoadSupply LoadSupply(ReleaseNetworkEvaluation evaluation, string loadId) =>
        evaluation.Loads.Single(load => string.Equals(load.LoadId, loadId, StringComparison.Ordinal));

    private static ReleaseNodeUsage NodeUsage(ReleaseNetworkEvaluation evaluation, string nodeId) =>
        evaluation.Nodes.Single(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal));

    private static ReleaseEdgeUsage EdgeUsage(ReleaseNetworkEvaluation evaluation, string edgeId) =>
        evaluation.Edges.Single(edge => string.Equals(edge.EdgeId, edgeId, StringComparison.Ordinal));

    private static ReleaseSourceUsage SourceUsage(ReleaseNetworkEvaluation evaluation, string sourceId) =>
        evaluation.Sources.Single(source => string.Equals(source.SourceId, sourceId, StringComparison.Ordinal));

    private void ExpectLoaderRejected(string label, Action<JsonObject> mutate)
    {
        JsonObject root = ParseObject(_fixtureJson);
        mutate(root);
        ExpectLoaderRejected(label, root.ToJsonString());
    }

    private void ExpectLoaderRejected(string label, string json)
    {
        try
        {
            _ = ReleaseWorldLoader.Load(json);
        }
        catch (ReleaseWorldValidationException)
        {
            _assertionCount++;
            return;
        }

        throw new InvalidOperationException($"{label}: loader accepted invalid world");
    }

    private void ExpectLoaderRejectedBytes(string label, byte[] bytes)
    {
        try
        {
            _ = ReleaseWorldLoader.Load(bytes);
        }
        catch (ReleaseWorldValidationException)
        {
            _assertionCount++;
            return;
        }

        throw new InvalidOperationException($"{label}: loader accepted invalid bytes");
    }

    private void ExpectWorldRejected(string label, ReleaseWorldDefinition world)
    {
        try
        {
            ReleaseWorldLoader.Validate(world);
        }
        catch (ReleaseWorldValidationException)
        {
            _assertionCount++;
            return;
        }

        throw new InvalidOperationException($"{label}: validator accepted invalid world");
    }

    private static JsonObject ParseObject(string json) =>
        JsonNode.Parse(json) as JsonObject
        ?? throw new InvalidOperationException("fixture root is not an object");

    private static JsonObject Object(JsonNode node, string propertyName) =>
        node[propertyName] as JsonObject
        ?? throw new InvalidOperationException($"{propertyName} is not an object");

    private static JsonObject Object(JsonNode node) =>
        node as JsonObject
        ?? throw new InvalidOperationException("node is not an object");

    private static JsonArray Array(JsonNode node, string propertyName) =>
        node[propertyName] as JsonArray
        ?? throw new InvalidOperationException($"{propertyName} is not an array");

    private void Check(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private void Equal<T>(T expected, T actual, string message)
    {
        _assertionCount++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
        }
    }

    private void SequenceEqual<T>(
        IReadOnlyList<T> expected,
        IReadOnlyList<T> actual,
        string message)
    {
        _assertionCount++;
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"{message}: expected [{string.Join(", ", expected)}], " +
                $"got [{string.Join(", ", actual)}]");
        }
    }
}
