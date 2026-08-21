using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gridworks.Core.Release.V2;

namespace Gridworks.CommercialChecks;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            string fixturePath = ResolveFixturePath(args);
            return new CommercialChecks(fixturePath).Run();
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
            throw new ArgumentException(
                "usage: Gridworks.CommercialChecks [release-world-v2-json]");
        }

        string path = args.Length == 1
            ? args[0]
            : Path.Combine(
                Environment.CurrentDirectory,
                "data",
                "release-world-v2.json");
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Commercial world v2 fixture not found.", path);
        }
        return path;
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
    private readonly byte[] _commercialBytes;
    private readonly string _commercialJson;
    private readonly CommercialWorldDefinition _commercialWorld;
    private readonly byte[] _coreBytes;
    private readonly string _coreJson;
    private readonly CommercialCoreSliceDefinition _coreSlice;
    private int _assertionCount;

    public CommercialChecks(string fixturePath)
    {
        _commercialBytes = File.ReadAllBytes(fixturePath);
        _commercialJson = Encoding.UTF8.GetString(_commercialBytes);
        _commercialWorld = CommercialWorldLoader.Load(_commercialBytes);
        string corePath = Path.Combine(
            Path.GetDirectoryName(fixturePath)!,
            "commercial-core-slice-v1.json");
        _coreBytes = File.ReadAllBytes(corePath);
        _coreJson = Encoding.UTF8.GetString(_coreBytes);
        _coreSlice = CommercialCoreLoader.Load(_coreBytes, _commercialWorld);
        string spatialFixturePath = Path.Combine(
            Path.GetDirectoryName(fixturePath)!,
            "commercial-free-placement-slice-v1.json");
        _fixtureBytes = File.ReadAllBytes(spatialFixturePath);
        _fixtureJson = Encoding.UTF8.GetString(_fixtureBytes);
        _fixture = SpatialWorldLoader.Load(_fixtureBytes);
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
            ("thermal-boundaries-and-route-order", CheckThermalBoundariesAndRouteOrder),
            ("thermal-shared-permissions-and-bottleneck", CheckThermalSharedPermissionsAndBottleneck),
            ("thermal-protection-cooling-and-determinism", CheckThermalProtectionCoolingAndDeterminism),
            ("thermal-review-regressions", CheckThermalReviewRegressions),
            ("strict-commercial-core-loader", CheckStrictCommercialCoreLoader),
            ("commercial-core-flow-designs-and-facts", CheckCommercialCoreFlowDesignsAndFacts),
            ("commercial-core-choice-deadline-and-atomicity", CheckCommercialCoreChoiceDeadlineAndAtomicity),
            ("commercial-core-rollback-and-fresh-replay", CheckCommercialCoreRollbackAndFreshReplay),
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

    private void CheckStrictCommercialWorldLoader()
    {
        CommercialWorldDefinition fromText = CommercialWorldLoader.Load(_commercialJson);
        CommercialWorldDefinition fromBytes = CommercialWorldLoader.Load(_commercialBytes);
        Equal(_commercialWorld.WorldId, fromText.WorldId, "commercial text loader world ID");
        Equal(_commercialWorld.WorldId, fromBytes.WorldId, "commercial byte loader world ID");
        Equal(2, _commercialWorld.GenerationSources.Count, "commercial source count");
        Check(_commercialWorld.Spatial.Edges.Count > 0, "final world must contain an initial network");
        Check(CommercialWorldLoader.ThermalAssetIds(_commercialWorld).Contains("NORTH_SUBSTATION"),
            "substation thermal asset missing from final world");

        string trimmed = _commercialJson.TrimStart();
        ExpectCommercialRejected(
            "duplicate commercial JSON property",
            $"{{\"schemaVersion\":\"duplicate\",{trimmed[1..]}");
        ExpectCommercialRejected("unknown commercial root field", root => root["unexpected"] = true);
        ExpectCommercialRejected("missing commercial world ID", root => root.Remove("worldId"));
        ExpectCommercialRejected(
            "mismatched spatial world ID",
            root => Object(root["spatial"]!)["worldId"] = "OTHER_WORLD");
        ExpectCommercialRejected(
            "zero continuous limit",
            root => Object(JsonArrayProperty(root, "thermalNodeClasses")[0]!)["continuousLimitKw"] = 0);
        ExpectCommercialRejected(
            "continuous above emergency",
            root =>
            {
                JsonObject thermalClass = Object(JsonArrayProperty(root, "thermalLineClasses")[0]!);
                thermalClass["continuousLimitKw"] = 9000;
                thermalClass["emergencyLimitKw"] = 8000;
            });
        ExpectCommercialRejected(
            "thermal source terminal class",
            root => Object(JsonArrayProperty(root, "thermalNodeClasses")[0]!)["classId"] =
                "SOURCE_TERMINAL");
        ExpectCommercialRejected(
            "duplicate source authored order",
            root => Object(JsonArrayProperty(root, "generationSources")[1]!)["authoredOrder"] = 0);
    }

    private void CheckStrictCommercialCoreLoader()
    {
        CommercialCoreSliceDefinition fromText = CommercialCoreLoader.Load(
            _coreJson,
            _commercialWorld);
        CommercialCoreSliceDefinition fromBytes = CommercialCoreLoader.Load(
            _coreBytes,
            _commercialWorld);
        Equal(_coreSlice.SliceId, fromText.SliceId, "core slice text loader ID");
        Equal(_coreSlice.SliceId, fromBytes.SliceId, "core slice byte loader ID");
        Equal(2, _coreSlice.Chapters.Count, "Stage-D exact chapter count");
        Equal(2, _coreSlice.Chapters[1].DecisionWindows.Count, "core decision-window count");

        string trimmed = _coreJson.TrimStart();
        ExpectCoreRejected(
            "duplicate core JSON property",
            $"{{\"schemaVersion\":\"duplicate\",{trimmed[1..]}");
        ExpectCoreRejected("unknown core root field", root => root["future"] = true);
        ExpectCoreRejected("missing core slice ID", root => root.Remove("sliceId"));
        ExpectCoreRejected(
            "wrong core world ID",
            root => root["worldId"] = "OTHER_WORLD");
        ExpectCoreRejected(
            "future campaign placeholder chapter",
            root => JsonArrayProperty(root, "chapters").Add(
                JsonArrayProperty(root, "chapters")[1]!.DeepClone()));
        ExpectCoreRejected(
            "unknown seed node",
            root => JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[0]!),
                "seedNodeIds").Add("UNKNOWN_NODE"));
        ExpectCoreRejected(
            "seed edge endpoint omitted",
            root => JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[1]!),
                "seedNodeIds").RemoveAt(0));
        ExpectCoreRejected(
            "zero window allowance",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[0]!),
                "decisionWindows")[0]!)["buildMinutesAllowance"] = 0);
        ExpectCoreRejected(
            "unknown next phase",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[1]!),
                "decisionWindows")[0]!)["nextPhaseId"] = "UNKNOWN_PHASE");
        ExpectCoreRejected(
            "integer chapter kind",
            root => Object(JsonArrayProperty(root, "chapters")[0]!)["kind"] = 0);
        ExpectCoreRejected(
            "integer phase policy",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[0]!),
                "operatingPhases")[0]!)["policy"] = 0);
        ExpectCoreRejected(
            "promise without deferred result",
            root => Object(JsonArrayProperty(root, "chapters")[1]!)["deferredResult"] = null);
        ExpectCoreRejected(
            "unknown phase risk area",
            root => JsonArrayProperty(
                Object(JsonArrayProperty(
                    Object(JsonArrayProperty(root, "chapters")[1]!),
                    "operatingPhases")[0]!),
                "activeRiskAreaIds").Add("UNKNOWN_RISK"));
        ExpectCoreRejected(
            "duplicate load ID across phases",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(
                    Object(JsonArrayProperty(root, "chapters")[1]!),
                    "operatingPhases")[1]!),
                    "loads")[0]!)["loadId"] = "HOSPITAL_DUTY");
    }

    private void CheckCommercialCoreFlowDesignsAndFacts()
    {
        CommercialCoreRun standard = NewCoreRun();
        CompletePrelude(standard);
        CoreAccepted(standard.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "standard keep promise");
        CommercialDecisionPreview standardDraft = CompleteIndustryDraft(
            standard,
            "STANDARD_LINE",
            "STANDARD_POLE");
        Equal(10L, standardDraft.ProjectedMinute, "standard draft projected minute");
        Check(standardDraft.Accepted,
            $"standard draft preview failed: {standardDraft.Error}/" +
            $"{standardDraft.FailedDemandId}/{standardDraft.SupplyFailure}/" +
            $"{standardDraft.FirstBottleneckAssetId}");
        ThermalDemandResult standardPromise = standardDraft.PhaseResults[0].Demands.Single(item =>
            item.DemandId == "INDUSTRY_PROMISE");
        Check(standardPromise.Supplied, "standard prototype did not supply the promise");
        Check(standardPromise.EmergencyAssetIds.Contains("PLAYER_EDGE_1"),
            "standard prototype did not use its conductor emergency limit");

        CoreAccepted(standard.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "standard line order");
        CoreAccepted(standard.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "standard line completion");
        CommercialDecisionPreview standardCommittedPreview = standard.PreviewDecisionWindow();
        Equal(
            JsonSerializer.Serialize(standardDraft),
            JsonSerializer.Serialize(standardCommittedPreview),
            "complete-draft preview equals commissioned preview");
        CommercialCoreCommandResult hotApproval = standard.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(hotApproval, "standard hot-window approval");
        Equal(
            JsonSerializer.Serialize(standardCommittedPreview),
            JsonSerializer.Serialize(hotApproval.DecisionPreview),
            "public hot preview equals approval result");
        Check(hotApproval.Snapshot.ThermalMemory.Single(item =>
                item.AssetId == "PLAYER_EDGE_1").ProtectiveOutage,
            "standard emergency line did not enter next-phase protective memory");
        CommercialCoreCommandResult frozenChoice = standard.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Defer));
        Check(!frozenChoice.Accepted && frozenChoice.Error == CommercialCoreError.WrongPhase,
            "promise changed after its operating result was committed");
        CommercialDecisionPreview safetyPreview = standard.PreviewDecisionWindow();
        Check(safetyPreview.Accepted, "standard prototype broke next safety duties");
        CommercialCoreCommandResult standardFinish = standard.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(standardFinish, "standard next-safety approval");
        Equal(JsonSerializer.Serialize(safetyPreview),
            JsonSerializer.Serialize(standardFinish.DecisionPreview),
            "public next-safety preview equals approval result");
        Check(standardFinish.Snapshot.CampaignComplete, "standard prototype did not complete the slice");
        CommercialChapterResultRecord standardResult = standardFinish.CompletedChapter!;
        Equal(PromiseDecision.Keep, standardResult.PromiseDecision, "standard result promise decision");
        CommercialResultDemandFact standardFact = standardResult.DemandFacts.Single(item =>
            item.DemandId == "INDUSTRY_PROMISE");
        Equal(CommercialCoreObligationKind.CityPromise, standardFact.ObligationKind,
            "standard result obligation fact");
        Check(standardFact.Supplied && standardFact.PathNodeIds.Count > 1 &&
            standardFact.PathEdgeIds.Contains("PLAYER_EDGE_1") &&
            standardFact.SourceNodeId is not null,
            "standard result omitted actual source/path facts");
        Check(standardResult.EmergencyAssetIds.Contains("PLAYER_EDGE_1") &&
            standardResult.ProtectiveOutageAssetIds.Contains("PLAYER_EDGE_1"),
            "standard result omitted emergency/protective facts");

        CommercialCoreRun reinforced = NewCoreRun();
        CompletePrelude(reinforced);
        CoreAccepted(reinforced.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "reinforced keep promise");
        CommercialDecisionPreview reinforcedDraft = CompleteIndustryDraft(
            reinforced,
            "REINFORCED_LINE",
            "REINFORCED_POLE");
        Check(reinforcedDraft.Accepted, "reinforced draft preview failed");
        Equal(15L, reinforcedDraft.ProjectedMinute, "reinforced draft projected minute");
        Check(reinforcedDraft.ProjectedCashUnit < standardDraft.ProjectedCashUnit,
            "reinforced prototype was not more expensive");
        Check(reinforcedDraft.PhaseResults[0].Demands.Single(item =>
                item.DemandId == "INDUSTRY_PROMISE").EmergencyAssetIds.Count == 0,
            "reinforced prototype did not remain continuous: " + string.Join(",",
                reinforcedDraft.PhaseResults[0].Assets
                    .Where(item => item.UseKw > 0)
                    .Select(item => $"{item.AssetId}={item.UseKw}/{item.ContinuousLimitKw}")));
        CoreAccepted(reinforced.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "reinforced line order");
        CoreAccepted(reinforced.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "reinforced line completion");
        CoreAccepted(reinforced.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)), "reinforced hot approval");
        CommercialCoreCommandResult reinforcedFinish = reinforced.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(reinforcedFinish, "reinforced safety approval");
        Check(reinforcedFinish.CompletedChapter!.EmergencyAssetIds.Count == 0,
            "reinforced result reported emergency operation");
    }

    private void CheckCommercialCoreChoiceDeadlineAndAtomicity()
    {
        CommercialCoreRun missingPromise = NewCoreRun();
        CompletePrelude(missingPromise);
        string beforeChoice = JsonSerializer.Serialize(missingPromise.GetSnapshot());
        CommercialDecisionPreview choiceRequired = missingPromise.PreviewDecisionWindow();
        Check(!choiceRequired.Accepted &&
            choiceRequired.Error == CommercialCoreError.PromiseDecisionRequired,
            "core preview did not require an explicit promise decision");
        Equal(beforeChoice, JsonSerializer.Serialize(missingPromise.GetSnapshot()),
            "rejected missing-choice preview mutated state");

        CommercialCoreRun deferred = NewCoreRun();
        CompletePrelude(deferred);
        long cashBeforeChoice = deferred.GetSnapshot().CashUnit;
        CoreAccepted(deferred.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Defer)), "defer promise");
        Equal(cashBeforeChoice, deferred.GetSnapshot().CashUnit,
            "promise choice changed the authored grant");
        CommercialDecisionPreview deferPreview = deferred.PreviewDecisionWindow();
        Check(deferPreview.Accepted, "deferred promise blocked required progress");
        ThermalDemandResult deferredDemand = deferPreview.PhaseResults[0].Demands.Single(item =>
            item.DemandId == "INDUSTRY_PROMISE");
        Check(deferredDemand.Deferred && !deferredDemand.Supplied,
            "deferred promise was not excluded from supply candidates");
        CoreAccepted(deferred.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)), "deferred hot approval");
        CommercialCoreCommandResult deferFinish = deferred.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(deferFinish, "deferred safety approval");
        Equal(PromiseDecision.Defer, deferFinish.CompletedChapter!.PromiseDecision,
            "deferred result choice");

        CommercialCoreRun exactDeadline = NewCoreRun();
        CompletePrelude(exactDeadline);
        CoreAccepted(exactDeadline.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Defer)), "deadline defer promise");
        CompleteIndustryDraft(exactDeadline, "STANDARD_LINE", "STANDARD_POLE");
        CoreAccepted(exactDeadline.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "deadline first line order");
        CoreAccepted(exactDeadline.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "deadline first line completion");
        CoreAccepted(exactDeadline.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.StartLineDraft,
            StartNodeId: "NORTH_SUBSTATION",
            LineClassId: "STANDARD_LINE",
            PoleClassId: "STANDARD_POLE")), "deadline second line start");
        CoreAccepted(exactDeadline.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.FinishLineDraft,
            EndNodeId: "EAST_RESIDENTIAL_TERMINAL")), "deadline second line finish");
        CoreAccepted(exactDeadline.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "deadline second line order");
        CoreAccepted(exactDeadline.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "deadline second line completion");
        Equal(20L, exactDeadline.GetSnapshot().Construction.Minute, "exact deadline minute");
        Check(exactDeadline.PreviewDecisionWindow().Accepted,
            "exact authored deadline was rejected");

        CommercialCoreRun overdue = NewCoreRun();
        CompletePrelude(overdue);
        CoreAccepted(overdue.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "overdue keep promise");
        CoreAccepted(overdue.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.StartLineDraft,
            StartNodeId: "WATER_TERMINAL",
            LineClassId: "STANDARD_LINE",
            PoleClassId: "STANDARD_POLE")), "overdue line start");
        CoreAccepted(overdue.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AddLinePoint,
            Position: new MapPoint(2750, 1100))), "overdue line point");
        CoreAccepted(overdue.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.FinishLineDraft,
            EndNodeId: "INDUSTRY_TERMINAL")), "overdue line finish");
        CommercialDecisionPreview overdueDraft = overdue.PreviewDecisionWindow();
        Check(!overdueDraft.Accepted && overdueDraft.Error == CommercialCoreError.DeadlineExceeded &&
            overdueDraft.ProjectedMinute > 20,
            "complete draft beyond deadline was not rejected");
        CoreAccepted(overdue.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "overdue line order");
        CoreAccepted(overdue.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "overdue line completion");
        string overdueBeforeApproval = JsonSerializer.Serialize(overdue.GetSnapshot());
        CommercialCoreCommandResult overdueApproval = overdue.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        Check(!overdueApproval.Accepted && overdueApproval.Error == CommercialCoreError.DeadlineExceeded,
            "commissioned project beyond deadline was approved");
        Equal(overdueBeforeApproval, JsonSerializer.Serialize(overdue.GetSnapshot()),
            "deadline rejection mutated campaign state");

        CommercialCoreSliceDefinition bottleneckSlice = CoreSliceWithIndustryDemand(3100);
        CommercialCoreRun bottleneck = new(_commercialWorld, bottleneckSlice);
        CompletePrelude(bottleneck);
        CoreAccepted(bottleneck.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "bottleneck keep promise");
        CompleteIndustryDraft(bottleneck, "STANDARD_LINE", "STANDARD_POLE");
        CommercialDecisionPreview bottleneckPreview = bottleneck.PreviewDecisionWindow();
        Check(!bottleneckPreview.Accepted &&
            bottleneckPreview.Error == CommercialCoreError.KeptPromiseFailed &&
            bottleneckPreview.SupplyFailure == ThermalSupplyFailure.EmergencyLimit &&
            bottleneckPreview.FirstBottleneckAssetId == "PLAYER_EDGE_1",
            $"representative thermal bottleneck was not identified: " +
            $"{bottleneckPreview.Error}/{bottleneckPreview.SupplyFailure}/" +
            $"{bottleneckPreview.FirstBottleneckAssetId}");

        CommercialCoreRun invalid = NewCoreRun();
        string invalidBefore = JsonSerializer.Serialize(invalid.GetSnapshot());
        CommercialCoreCommandResult ignoredField = invalid.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AddLinePoint,
            Position: new MapPoint(1, 1),
            EndNodeId: "EXTRA"));
        Check(!ignoredField.Accepted && ignoredField.Error == CommercialCoreError.InvalidCommand,
            "command with ignored extra field was accepted");
        Equal(invalidBefore, JsonSerializer.Serialize(invalid.GetSnapshot()),
            "invalid command shape mutated state");
        CommercialCoreCommandResult invalidPromise = invalid.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: (PromiseDecision)999));
        Check(!invalidPromise.Accepted && invalidPromise.Error == CommercialCoreError.InvalidCommand,
            "undefined promise decision enum was accepted");
    }

    private void CheckCommercialCoreRollbackAndFreshReplay()
    {
        CommercialCoreRun run = NewCoreRun();
        CompletePrelude(run);
        string chapterStart = JsonSerializer.Serialize(run.GetSnapshot());
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "rollback keep promise");
        string beforeProject = JsonSerializer.Serialize(run.GetSnapshot());
        CompleteIndustryDraft(run, "STANDARD_LINE", "STANDARD_POLE");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "rollback project order");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "rollback project completion");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)), "rollback hot approval");
        Check(run.GetSnapshot().DecisionWindowIndex == 1 &&
            run.GetSnapshot().ThermalMemory.Count > 0,
            "rollback setup did not advance phase and thermal state");
        CoreAccepted(run.RollbackRecentProject(), "recent project rollback");
        Equal(beforeProject, JsonSerializer.Serialize(run.GetSnapshot()),
            "recent rollback did not restore coordinates/cash/time/phases/promise/thermal state");

        CommercialCoreRun fresh = CommercialCoreRun.Restore(
            _commercialWorld,
            _coreSlice,
            run.GetCommands());
        Equal(JsonSerializer.Serialize(run.GetSnapshot()), JsonSerializer.Serialize(fresh.GetSnapshot()),
            "fresh replay after rollback snapshot");
        Equal(JsonSerializer.Serialize(run.GetCommands()), JsonSerializer.Serialize(fresh.GetCommands()),
            "fresh replay after rollback journal");

        CompleteIndustryDraft(run, "REINFORCED_LINE", "REINFORCED_POLE");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.CancelLineDraft)), "cancel replayed draft");
        CoreAccepted(run.RestartChapter(), "chapter restart");
        Equal(chapterStart, JsonSerializer.Serialize(run.GetSnapshot()),
            "chapter restart did not restore its journal prefix");
    }

    private void CheckCommercialCoreSaveV3()
    {
        CommercialCoreRun run = NewCoreRun();
        CompletePrelude(run);
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "save keep promise");
        CompleteIndustryDraft(run, "REINFORCED_LINE", "REINFORCED_POLE");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "save line order");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "save line completion");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)), "save hot approval");

        CommercialCoreCampaignSave save = CommercialCoreSaveCodec.Create(
            _commercialWorld,
            _commercialBytes,
            _coreSlice,
            _coreBytes,
            run.GetCommands());
        byte[] serialized = CommercialCoreSaveCodec.Serialize(save);
        CommercialCoreCampaignSave decoded = CommercialCoreSaveCodec.Deserialize(serialized);
        CommercialCoreRun restored = CommercialCoreSaveCodec.Restore(
            decoded,
            _commercialWorld,
            _commercialBytes,
            _coreSlice,
            _coreBytes);
        Equal(JsonSerializer.Serialize(run.GetSnapshot()), JsonSerializer.Serialize(restored.GetSnapshot()),
            "save to fresh restore state equality");
        Equal(CommercialCoreSaveCodec.ComputeSha256(_commercialBytes), decoded.WorldSha256,
            "save world content hash");
        Equal(CommercialCoreSaveCodec.ComputeSha256(_coreBytes), decoded.SliceSha256,
            "save core-slice content hash");

        string duplicate = Encoding.UTF8.GetString(serialized).Replace(
            "\"sliceId\":",
            "\"sliceId\": \"DUPLICATE\", \"sliceId\":",
            StringComparison.Ordinal);
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialCoreSaveCodec.Deserialize(Encoding.UTF8.GetBytes(duplicate)),
            "duplicate save property");
        string unknown = Encoding.UTF8.GetString(serialized).Replace(
            "\"commands\":",
            "\"future\": true, \"commands\":",
            StringComparison.Ordinal);
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialCoreSaveCodec.Deserialize(Encoding.UTF8.GetBytes(unknown)),
            "unknown save property");
        JsonObject nullCommands = JsonNode.Parse(serialized)!.AsObject();
        nullCommands["commands"] = null;
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialCoreSaveCodec.Deserialize(
                Encoding.UTF8.GetBytes(nullCommands.ToJsonString())),
            "null save command list");
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialCoreSaveCodec.Restore(
                decoded,
                _commercialWorld,
                [.. _commercialBytes, (byte)0],
                _coreSlice,
                _coreBytes),
            "save world hash mismatch");

        string directory = Path.Combine(
            Path.GetTempPath(),
            $"gridworks-commercial-save-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, CommercialCorePersistenceStore.SaveFileName);
        try
        {
            Equal(CommercialCoreDocumentLoadStatus.Missing,
                CommercialCorePersistenceStore.Load(path).Status,
                "missing commercial save status");
            CommercialCorePersistenceStore.Save(path, save);
            CommercialCoreSaveLoadResult loaded = CommercialCorePersistenceStore.Load(path);
            Equal(CommercialCoreDocumentLoadStatus.Loaded, loaded.Status,
                "stored commercial save status");
            Check(loaded.Save is not null && !File.Exists(path + ".tmp"),
                "atomic commercial save left no load or temporary file");
            File.WriteAllText(path, "{invalid", Encoding.UTF8);
            Equal(CommercialCoreDocumentLoadStatus.Invalid,
                CommercialCorePersistenceStore.Load(path).Status,
                "invalid commercial save status");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private void CheckThermalBoundariesAndRouteOrder()
    {
        CommercialWorldDefinition boundaryWorld = ThermalWorld(
        [
            Node("S", 100, 100),
            Node("A", 300, 100, PoleClassId),
            Node("L", 500, 100, LoadClassId),
        ],
        [Edge("E1", "S", "A"), Edge("E2", "A", "L")],
        continuous: 100,
        emergency: 150);

        ThermalIntervalResult exactContinuous = EvaluateOne(
            boundaryWorld,
            Interval("P", Demand("D", "L", 100, ThermalObligationKind.SafetyDuty)));
        Check(exactContinuous.Demands[0].Supplied, "exact continuous load was rejected");
        Check(exactContinuous.Assets.All(item => item.CurrentState == ThermalOperatingState.Continuous),
            "exact continuous load entered emergency state");

        ThermalIntervalResult aboveContinuous = EvaluateOne(
            boundaryWorld,
            Interval("P", Demand(
                "D",
                "L",
                101,
                ThermalObligationKind.CityPromise,
                emergencyApproved: true)));
        Check(aboveContinuous.Demands[0].Supplied, "approved load above continuous was rejected");
        Check(aboveContinuous.Assets.Any(item => item.CurrentState == ThermalOperatingState.Emergency),
            "load above continuous did not enter emergency state");

        ThermalIntervalResult exactEmergency = EvaluateOne(
            boundaryWorld,
            Interval("P", Demand(
                "D",
                "L",
                150,
                ThermalObligationKind.CityPromise,
                emergencyApproved: true)));
        Check(exactEmergency.Demands[0].Supplied, "exact emergency load was rejected");
        ThermalIntervalResult overEmergency = EvaluateOne(
            boundaryWorld,
            Interval("P", Demand(
                "D",
                "L",
                151,
                ThermalObligationKind.CityPromise,
                emergencyApproved: true)));
        Check(!overEmergency.Demands[0].Supplied &&
            overEmergency.Demands[0].Failure == ThermalSupplyFailure.EmergencyLimit,
            "load above emergency did not return the typed limit failure");

        CommercialWorldDefinition routeWorld = ThermalWorld(
        [
            Node("S", 100, 500),
            Node("SHORT_POLE", 300, 500, PoleClassId),
            Node("LONG_POLE_1", 100, 800, PoleClassId),
            Node("LONG_POLE_2", 400, 800, PoleClassId),
            Node("L", 700, 500, LoadClassId),
        ],
        [
            Edge("SHORT_1", "S", "SHORT_POLE"),
            Edge("SHORT_2", "SHORT_POLE", "L"),
            Edge("LONG_1", "S", "LONG_POLE_1"),
            Edge("LONG_2", "LONG_POLE_1", "LONG_POLE_2"),
            Edge("LONG_3", "LONG_POLE_2", "L"),
        ],
        continuous: 500,
        emergency: 700);
        ThermalIntervalDefinition routeInterval = Interval(
            "ROUTE",
            Demand("D", "L", 200, ThermalObligationKind.CityPromise, emergencyApproved: true),
            overrides: [new ThermalLimitOverride("SHORT_2", 100, 500)]);
        ThermalDemandResult route = EvaluateOne(routeWorld, routeInterval).Demands[0];
        SequenceEqual(["LONG_1", "LONG_2", "LONG_3"], route.PathEdgeIds,
            "long continuous route must outrank short emergency route");

        CommercialWorldDefinition sourceOrderWorld = ThermalWorld(
        [
            Node("S0", 100, 50),
            Node("S1", 100, 150),
            Node("H", 300, 100, PoleClassId),
            Node("L", 500, 100, LoadClassId),
        ],
        [Edge("S0_H", "S0", "H"), Edge("S1_H", "S1", "H"), Edge("H_L", "H", "L")],
        continuous: 500,
        emergency: 700);
        Equal("S0", EvaluateOne(
                sourceOrderWorld,
                Interval("SOURCE_ORDER", Demand("D", "L", 100, ThermalObligationKind.SafetyDuty)))
            .Demands[0].SourceNodeId,
            "authored source order tie-break");
    }

    private void CheckThermalSharedPermissionsAndBottleneck()
    {
        CommercialWorldDefinition world = SharedThermalWorld();
        ThermalLimitOverride hubLimit = new("HUB", 300, 400);
        ThermalDemandDefinition first = Demand(
            "SAFETY",
            "L1",
            180,
            ThermalObligationKind.SafetyDuty);

        ThermalIntervalResult operatingRejected = EvaluateOne(
            world,
            Interval(
                "OPERATING",
                first,
                Demand("RECORD", "L2", 180, ThermalObligationKind.OperatingRecord),
                overrides: [hubLimit]));
        ThermalDemandResult rejected = operatingRejected.Demands[1];
        Check(!rejected.Supplied && rejected.Failure == ThermalSupplyFailure.ContinuousPermission,
            "operating record incorrectly used emergency capacity");
        Equal("HUB", rejected.FirstBottleneckAssetId,
            "shared connector must be the first typed bottleneck");

        ThermalIntervalResult promiseApproved = EvaluateOne(
            world,
            Interval(
                "PROMISE",
                first,
                Demand(
                    "PROMISE_LOAD",
                    "L2",
                    180,
                    ThermalObligationKind.CityPromise,
                    emergencyApproved: true),
                overrides: [hubLimit]));
        Check(promiseApproved.Demands[1].Supplied, "approved promise did not use emergency capacity");
        ThermalAssetResult hub = promiseApproved.Assets.Single(item => item.AssetId == "HUB");
        Equal(360L, hub.UseKw, "shared connector aggregate use");
        Equal(ThermalOperatingState.Emergency, hub.CurrentState, "shared connector emergency state");
        Equal(ThermalOperatingState.ProtectiveOutage, hub.NextState,
            "shared connector next protective state");

        ThermalIntervalResult promiseUnapproved = EvaluateOne(
            world,
            Interval(
                "PROMISE_UNAPPROVED",
                Demand("P", "L1", 350, ThermalObligationKind.CityPromise),
                overrides: [hubLimit]));
        Check(!promiseUnapproved.Demands[0].Supplied &&
            promiseUnapproved.Demands[0].Failure == ThermalSupplyFailure.ContinuousPermission,
            "unapproved promise used emergency capacity");

        ThermalIntervalResult ordinarySafety = EvaluateOne(
            world,
            Interval(
                "ORDINARY_SAFETY",
                Demand("S", "L1", 350, ThermalObligationKind.SafetyDuty),
                policy: ThermalIntervalPolicy.SafetyEmergencyAllowed,
                overrides: [hubLimit]));
        Check(!ordinarySafety.Demands[0].Supplied,
            "ordinary safety duty incorrectly used named-emergency permission");

        ThermalIntervalResult namedSafety = EvaluateOne(
            world,
            Interval(
                "NAMED_SAFETY",
                Demand(
                    "S",
                    "L1",
                    350,
                    ThermalObligationKind.SafetyDuty,
                    namedEmergency: true),
                policy: ThermalIntervalPolicy.SafetyEmergencyAllowed,
                overrides: [hubLimit]));
        Check(namedSafety.Demands[0].Supplied, "named emergency safety duty was rejected");

        ThermalSequenceResult futureGuard = ThermalEvaluator.Evaluate(
            world,
            new ThermalSequenceRequest(
            [
                Interval(
                    "NAMED",
                    Demand(
                        "S1",
                        "L1",
                        350,
                        ThermalObligationKind.SafetyDuty,
                        namedEmergency: true),
                    policy: ThermalIntervalPolicy.SafetyEmergencyAllowed,
                    overrides: [hubLimit]),
                Interval(
                    "PUBLIC_NEXT",
                    Demand("S2", "L1", 200, ThermalObligationKind.SafetyDuty),
                    overrides: [hubLimit]),
            ],
            Array.Empty<ThermalAssetMemory>()));
        Check(!futureGuard.Intervals[0].Demands[0].Supplied &&
            futureGuard.Intervals[0].Demands[0].Failure == ThermalSupplyFailure.FutureSafetyDuty,
            "named emergency use broke a published next safety duty");

        ThermalIntervalResult deferred = EvaluateOne(
            world,
            Interval(
                "DEFERRED",
                Demand(
                    "P",
                    "L1",
                    350,
                    ThermalObligationKind.CityPromise,
                    included: false,
                    emergencyApproved: true),
                overrides: [hubLimit]));
        Check(deferred.Demands[0].Deferred && !deferred.Demands[0].Supplied,
            "deferred promise remained an active demand candidate");
    }

    private void CheckThermalProtectionCoolingAndDeterminism()
    {
        CommercialWorldDefinition world = SharedThermalWorld();
        ThermalLimitOverride hubLimit = new("HUB", 300, 400);
        ThermalSequenceRequest request = new(
        [
            Interval(
                "HOT",
                Demand(
                    "PROMISE",
                    "L1",
                    350,
                    ThermalObligationKind.CityPromise,
                    emergencyApproved: true),
                overrides: [hubLimit]),
            Interval("COOL", overrides: [hubLimit]),
            Interval(
                "RETURN",
                Demand("SAFETY", "L1", 200, ThermalObligationKind.SafetyDuty),
                overrides: [hubLimit]),
        ],
        Array.Empty<ThermalAssetMemory>());

        ThermalSequenceResult first = ThermalEvaluator.Evaluate(world, request);
        ThermalAssetResult hotHub = first.Intervals[0].Assets.Single(item => item.AssetId == "HUB");
        ThermalAssetResult coolingHub = first.Intervals[1].Assets.Single(item => item.AssetId == "HUB");
        ThermalAssetResult returnedHub = first.Intervals[2].Assets.Single(item => item.AssetId == "HUB");
        Equal(ThermalOperatingState.Emergency, hotHub.CurrentState, "hot phase state");
        Equal(ThermalOperatingState.ProtectiveOutage, coolingHub.CurrentState,
            "next full phase protective outage");
        Equal(0L, coolingHub.UseKw, "protective phase must remain unloaded");
        Equal(ThermalOperatingState.Continuous, returnedHub.CurrentState,
            "asset did not return after one unloaded cooling phase");
        Check(first.Intervals[2].Demands[0].Supplied, "cooled asset did not resume supply");

        string evaluated = JsonSerializer.Serialize(first);
        string repeated = JsonSerializer.Serialize(ThermalEvaluator.Evaluate(world, request));
        string preview = JsonSerializer.Serialize(ThermalEvaluator.Preview(world, request));
        Equal(evaluated, repeated, "thermal sequence repeat determinism");
        Equal(evaluated, preview, "thermal preview/evaluation value equality");

        ExpectThrows<ThermalEvaluationException>(
            () => ThermalEvaluator.Evaluate(
                world,
                new ThermalSequenceRequest(
                [
                    Interval(
                        "BAD_OVERRIDE",
                        overrides: [new ThermalLimitOverride("HUB", 401, 501)]),
                ],
                Array.Empty<ThermalAssetMemory>())),
            "thermal override must only lower the class limits");
    }

    private void CheckThermalReviewRegressions()
    {
        CommercialWorldDefinition shared = SharedThermalWorld();
        ExpectThrows<ThermalEvaluationException>(
            () => EvaluateOne(
                shared,
                Interval(
                    "DEFERRED_SAFETY",
                    Demand(
                        "S",
                        "L1",
                        100,
                        ThermalObligationKind.SafetyDuty,
                        included: false))),
            "mandatory safety duty cannot be deferred");
        ExpectThrows<ThermalEvaluationException>(
            () => EvaluateOne(
                shared,
                Interval(
                    "DEFERRED_RECORD",
                    Demand(
                        "R",
                        "L1",
                        100,
                        ThermalObligationKind.OperatingRecord,
                        included: false))),
            "operating record cannot be deferred");

        ThermalIntervalResult unavailableEndpoint = EvaluateOne(
            shared,
            Interval(
                "UNAVAILABLE_ENDPOINT",
                Demand("S", "L1", 100, ThermalObligationKind.SafetyDuty),
                unavailable: ["L1"]));
        Check(!unavailableEndpoint.Demands[0].Supplied &&
            unavailableEndpoint.Demands[0].Failure == ThermalSupplyFailure.UnavailableAsset &&
            unavailableEndpoint.Demands[0].FirstBottleneckAssetId == "L1",
            "unavailable nonthermal load endpoint still received supply");

        CommercialWorldDefinition transitWorld = ThermalWorld(
        [
            Node("S", 100, 100),
            Node("TRANSIT_LOAD", 300, 100, LoadClassId),
            Node("L", 500, 100, LoadClassId),
        ],
        [Edge("E1", "S", "TRANSIT_LOAD"), Edge("E2", "TRANSIT_LOAD", "L")],
        continuous: 500,
        emergency: 700);
        ThermalDemandResult unavailableTransit = EvaluateOne(
            transitWorld,
            Interval(
                "UNAVAILABLE_TRANSIT",
                Demand("D", "L", 100, ThermalObligationKind.SafetyDuty),
                unavailable: ["TRANSIT_LOAD"]))
            .Demands[0];
        Check(!unavailableTransit.Supplied &&
            unavailableTransit.FirstBottleneckAssetId == "TRANSIT_LOAD",
            "unavailable nonthermal transit endpoint still carried supply");

        CommercialWorldDefinition manyPaths = DiamondThermalWorld(17);
        ThermalDemandResult routed = EvaluateOne(
            manyPaths,
            Interval(
                "MANY_PATHS",
                Demand("D", "L", 1, ThermalObligationKind.SafetyDuty)))
            .Demands[0];
        Check(routed.Supplied && routed.PathEdgeIds.Count == 34,
            "all-simple-path evaluation aborted above the former 100,000-path limit");
    }

    private CommercialCoreRun NewCoreRun() => new(_commercialWorld, _coreSlice);

    private void CompletePrelude(CommercialCoreRun run)
    {
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.StartLineDraft,
            StartNodeId: "WEST_SOURCE",
            LineClassId: "REINFORCED_LINE",
            PoleClassId: "STANDARD_POLE")), "prelude line start");
        foreach (MapPoint point in new[]
        {
            new MapPoint(650, 700),
            new MapPoint(1030, 500),
            new MapPoint(1560, 500),
            new MapPoint(2000, 600),
            new MapPoint(2400, 700),
        })
        {
            CoreAccepted(run.Apply(new CommercialCoreCommand(
                CommercialCoreCommandKind.AddLinePoint,
                Position: point)), "prelude line point");
        }
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.FinishLineDraft,
            EndNodeId: "EAST_RESIDENTIAL_TERMINAL")), "prelude line finish");
        CommercialDecisionPreview draftPreview = run.PreviewDecisionWindow();
        Check(draftPreview.Accepted, "prelude complete-draft preview failed");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "prelude line order");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "prelude line completion");
        CommercialDecisionPreview commissioned = run.PreviewDecisionWindow();
        Check(commissioned.Accepted, "commissioned prelude preview failed");
        Equal(JsonSerializer.Serialize(draftPreview), JsonSerializer.Serialize(commissioned),
            "prelude complete-draft preview equals commissioned preview");
        CommercialCoreCommandResult approval = run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(approval, "prelude approval");
        Equal(JsonSerializer.Serialize(commissioned),
            JsonSerializer.Serialize(approval.DecisionPreview),
            "public prelude preview equals approval result");
        Equal("WHOSE_MARGIN", approval.Snapshot.Chapter.ChapterId,
            "prelude did not transition to the commercial core");
    }

    private CommercialDecisionPreview CompleteIndustryDraft(
        CommercialCoreRun run,
        string lineClassId,
        string poleClassId)
    {
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.StartLineDraft,
            StartNodeId: "WATER_TERMINAL",
            LineClassId: lineClassId,
            PoleClassId: poleClassId)), "industry line start");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.FinishLineDraft,
            EndNodeId: "INDUSTRY_TERMINAL")), "industry line finish");
        return run.PreviewDecisionWindow();
    }

    private CommercialCoreSliceDefinition CoreSliceWithIndustryDemand(long demandKw)
    {
        CommercialCoreChapter chapter = _coreSlice.Chapters[1];
        CommercialCoreOperatingPhase hot = chapter.OperatingPhases[0];
        CommercialCoreOperatingPhase changedHot = hot with
        {
            Loads = hot.Loads.Select(load => load.LoadId == "INDUSTRY_PROMISE"
                ? load with { DemandKw = demandKw }
                : load).ToArray(),
        };
        CommercialCoreChapter changedChapter = chapter with
        {
            OperatingPhases = chapter.OperatingPhases.Select(phase =>
                phase.PhaseId == changedHot.PhaseId ? changedHot : phase).ToArray(),
        };
        CommercialCoreSliceDefinition changed = _coreSlice with
        {
            Chapters = [_coreSlice.Chapters[0], changedChapter],
        };
        CommercialCoreLoader.Validate(changed, _commercialWorld);
        return changed;
    }

    private void CoreAccepted(CommercialCoreCommandResult result, string label)
    {
        Check(result.Accepted, $"{label}: rejected with {result.Error}/{result.ConstructionError}");
        Check(result.Error is null && result.ConstructionError is null,
            $"{label}: accepted result contained an error");
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

    private static CommercialWorldDefinition ThermalWorld(
        IReadOnlyList<SpatialNodeDefinition> nodes,
        IReadOnlyList<SpatialEdgeDefinition> edges,
        long continuous,
        long emergency)
    {
        SpatialWorldDefinition spatial = World(nodes, edges);
        GenerationSourceDefinition[] sources = nodes
            .Where(item => item.ClassId == SourceClassId)
            .OrderBy(item => item.NodeId, StringComparer.Ordinal)
            .Select((item, index) => new GenerationSourceDefinition(item.NodeId, 1000, index))
            .ToArray();
        CommercialWorldDefinition world = new(
            CommercialWorldLoader.SupportedSchemaVersion,
            spatial.WorldId,
            spatial.DisplayName,
            spatial,
            [
                new ThermalNodeClassDefinition(PoleClassId, continuous, emergency),
                new ThermalNodeClassDefinition(SubstationClassId, continuous, emergency),
            ],
            [new ThermalLineClassDefinition(LineClassId, continuous, emergency)],
            sources);
        CommercialWorldLoader.Validate(world);
        return world;
    }

    private static CommercialWorldDefinition SharedThermalWorld() => ThermalWorld(
    [
        Node("S", 100, 100),
        Node("HUB", 300, 100, PoleClassId),
        Node("L1", 500, 50, LoadClassId),
        Node("L2", 500, 150, LoadClassId),
    ],
    [
        Edge("SOURCE_HUB", "S", "HUB"),
        Edge("HUB_L1", "HUB", "L1"),
        Edge("HUB_L2", "HUB", "L2"),
    ],
    continuous: 400,
    emergency: 500);

    private static CommercialWorldDefinition DiamondThermalWorld(int diamondCount)
    {
        var nodes = new List<SpatialNodeDefinition>
        {
            Node("S", 100, 1000),
            Node("L", 100 + (diamondCount * 100), 1000, LoadClassId),
        };
        var edges = new List<SpatialEdgeDefinition>();
        for (int index = 0; index < diamondCount; index++)
        {
            string left = index == 0 ? "S" : $"J{index:D2}";
            string right = index + 1 == diamondCount ? "L" : $"J{index + 1:D2}";
            string top = $"T{index:D2}";
            string bottom = $"B{index:D2}";
            int branchX = 150 + (index * 100);
            nodes.Add(Node(top, branchX, 900, PoleClassId));
            nodes.Add(Node(bottom, branchX, 1100, PoleClassId));
            if (index + 1 < diamondCount)
            {
                nodes.Add(Node(right, 200 + (index * 100), 1000, PoleClassId));
            }
            edges.Add(Edge($"D{index:D2}_A_TOP", left, top));
            edges.Add(Edge($"D{index:D2}_B_BOTTOM", left, bottom));
            edges.Add(Edge($"D{index:D2}_C_TOP", top, right));
            edges.Add(Edge($"D{index:D2}_D_BOTTOM", bottom, right));
        }
        return ThermalWorld(nodes, edges, continuous: 500, emergency: 700);
    }

    private static ThermalDemandDefinition Demand(
        string id,
        string nodeId,
        long demandKw,
        ThermalObligationKind obligation,
        bool included = true,
        bool emergencyApproved = false,
        bool namedEmergency = false) => new(
            id,
            id,
            nodeId,
            demandKw,
            obligation,
            included,
            emergencyApproved,
            namedEmergency);

    private static ThermalIntervalDefinition Interval(
        string id,
        ThermalDemandDefinition? first = null,
        ThermalDemandDefinition? second = null,
        ThermalIntervalPolicy policy = ThermalIntervalPolicy.ContinuousOnly,
        IReadOnlyList<string>? unavailable = null,
        IReadOnlyList<ThermalLimitOverride>? overrides = null) => new(
            id,
            id,
            policy,
            new[] { first, second }.Where(item => item is not null).Cast<ThermalDemandDefinition>().ToArray(),
            unavailable ?? Array.Empty<string>(),
            overrides ?? Array.Empty<ThermalLimitOverride>());

    private static ThermalIntervalResult EvaluateOne(
        CommercialWorldDefinition world,
        ThermalIntervalDefinition interval) => ThermalEvaluator.Evaluate(
            world,
            new ThermalSequenceRequest([interval], Array.Empty<ThermalAssetMemory>()))
        .Intervals[0];

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

    private void ExpectCommercialRejected(string label, Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(_commercialJson)!.AsObject();
        mutate(root);
        ExpectCommercialRejected(label, root.ToJsonString());
    }

    private void ExpectCommercialRejected(string label, string json) =>
        ExpectThrows<CommercialWorldValidationException>(
            () => CommercialWorldLoader.Load(json),
            label);

    private void ExpectCoreRejected(string label, Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(_coreJson)!.AsObject();
        mutate(root);
        ExpectCoreRejected(label, root.ToJsonString());
    }

    private void ExpectCoreRejected(string label, string json) =>
        ExpectThrows<CommercialCoreValidationException>(
            () => CommercialCoreLoader.Load(json, _commercialWorld),
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
