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
                "usage: Gridworks.CommercialChecks [commercial-spatial-json]");
        }

        string path = args.Length == 1
            ? args[0]
            : Path.Combine(
                Environment.CurrentDirectory,
                "data",
                "commercial-free-placement-slice-v1.json");
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Commercial spatial fixture not found.", path);
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
    private int _assertionCount;

    public CommercialChecks(string fixturePath)
    {
        _fixtureBytes = File.ReadAllBytes(fixturePath);
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
