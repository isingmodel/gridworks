using System.Text;
using System.Text.Json.Nodes;
using Gridworks.Core.Product;

namespace Gridworks.ProductChecks;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            string fixturePath = ResolveFixturePath(args);
            return new FirstLightChecks(fixturePath).Run();
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
            throw new ArgumentException("usage: Gridworks.ProductChecks [fixture-json]");
        }

        string path = args.Length == 1
            ? args[0]
            : Path.Combine(
                Environment.CurrentDirectory,
                "data",
                "product-first-light-v1.json");
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("First Light product fixture not found.", path);
        }
        return path;
    }
}

internal sealed class FirstLightChecks
{
    // Checker-only witnesses. They must not move into runtime data, Core defaults, or Game UI.
    private static readonly ProductPoint SuccessSubstation = new(14, 6);
    private static readonly ProductPoint OutsideSubstation = new(13, 6);
    private static readonly ProductPoint FirstSupport = new(6, 6);
    private static readonly ProductPoint SecondSupport = new(10, 6);

    private readonly string _fixtureJson;
    private readonly ProductFixture _fixture;
    private int _assertionCount;

    public FirstLightChecks(string fixturePath)
    {
        _fixtureJson = File.ReadAllText(fixturePath, Encoding.UTF8);
        _fixture = ProductFixtureLoader.Load(_fixtureJson);
    }

    public int Run()
    {
        (string Name, Action Body)[] suites =
        [
            ("strict-loader-shape-types", CheckStrictLoaderShapeAndTypes),
            ("loader-references-ranges-arithmetic", CheckLoaderReferencesRangesAndArithmetic),
            ("substation-draft-preview-errors", CheckSubstationDraftPreviewAndErrors),
            ("substation-order-lifecycle-cash", CheckSubstationOrderLifecycleAndCash),
            ("line-draft-preview-errors", CheckLineDraftPreviewAndErrors),
            ("line-order-lifecycle-cash", CheckLineOrderLifecycleAndCash),
            ("supply-capacity-precedence", CheckSupplyCapacityPrecedence),
            ("success-and-failure-settlement", CheckSuccessAndFailureSettlement),
            ("restart-all-phases", CheckRestartAllPhases),
            ("determinism-defensive-results", CheckDeterminismAndDefensiveResults),
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
                $"Gridworks Product checks: FAIL ({failures.Count}/{suites.Length} suites)");
            return 1;
        }

        Console.WriteLine(
            $"Gridworks Product checks: PASS ({suites.Length} suites, {_assertionCount} assertions)");
        return 0;
    }

    private void CheckStrictLoaderShapeAndTypes()
    {
        ProductFixture textFixture = ProductFixtureLoader.Load(_fixtureJson);
        ProductFixture bytesFixture = ProductFixtureLoader.Load(Encoding.UTF8.GetBytes(_fixtureJson));
        AssertFixtureValueEqual(_fixture, textFixture, "text loader");
        AssertFixtureValueEqual(_fixture, bytesFixture, "UTF-8 loader");

        Equal("gridworks.product.first-light.v1", _fixture.SchemaVersion, "schemaVersion");
        Equal("FIRST_LIGHT_V1", _fixture.FixtureId, "fixtureId");
        Equal("첫 점등", _fixture.DisplayName, "displayName");
        Equal(new ProductMapBounds(0, 20, 0, 12), _fixture.MapBounds, "map bounds");
        SequenceEqual(
            [new ProductPoint(9, 4), new ProductPoint(10, 4)],
            _fixture.BlockedCells,
            "blocked cells");
        Equal(0, _fixture.InitialMinute, "initial minute");
        Equal(60, _fixture.SettlementMinutes, "settlement minutes");
        Equal(5_000_000L, _fixture.Economy.InitialCash, "initial cash");
        Equal(100_000_000L, _fixture.Economy.SaleRateCashUnitPerGWh, "sale rate");
        Equal(new ProductPoint(2, 6), _fixture.ExistingSource.Position, "source position");
        Equal(3_000L, _fixture.ExistingSource.CapacityKw, "source capacity");
        Equal(new ProductPoint(18, 6), _fixture.Town.Position, "town position");
        Equal(1_000L, _fixture.Town.DemandKw, "town demand");
        Equal(2_000_000L, _fixture.SubstationProject.CostCashUnit, "substation cost");
        Equal(120, _fixture.SubstationProject.BuildMinutes, "substation build minutes");
        Equal(4, _fixture.SubstationProject.ServiceRadiusGridUnit, "service radius");
        Equal(4, _fixture.LineProject.MaxSpanGridUnit, "max span");

        string trimmed = _fixtureJson.TrimStart();
        ExpectFixtureRejected(
            "duplicate root property",
            "{\"schemaVersion\":\"gridworks.product.first-light.v1\"," + trimmed[1..]);
        ExpectFixtureRejected(
            "duplicate nested property",
            ReplaceRequired(
                _fixtureJson,
                "\"position\": \"GridUnit\",",
                "\"position\": \"GridUnit\",\n    \"position\": \"GridUnit\","));
        ExpectFixtureRejected("malformed JSON", "{\"schemaVersion\":");
        ExpectFixtureRejected("trailing JSON", _fixtureJson + "{}");
        ExpectFixtureRejected("JSON comment", "// forbidden\n" + _fixtureJson);
        ExpectFixtureRejected(
            "trailing comma",
            ReplaceRequired(_fixtureJson, "\n}", ",\n}"));
        ExpectFixtureRejected("non-object root", "[]");
        ExpectFixtureRejectedBytes("invalid UTF-8", [0xff, 0xfe, 0xfd]);

        ExpectFixtureRejected("unknown root field", root => root["unexpected"] = true);
        ExpectFixtureRejected(
            "unknown nested field",
            root => Object(root, "economy")["unexpected"] = 1);
        ExpectFixtureRejected("wrong field case", root => root["SchemaVersion"] = "x");
        ExpectFixtureRejected("missing root field", root => root.Remove("town"));
        ExpectFixtureRejected(
            "missing nested field",
            root => Object(root, "lineProject").Remove("ratingKw"));
        ExpectFixtureRejected("null object", root => root["economy"] = null);
        ExpectFixtureRejected(
            "null scalar",
            root => Object(root, "existingSource")["assetId"] = null);
        ExpectFixtureRejected(
            "null blocked element",
            root => Array(root, "blockedCells").Add(null));
        ExpectFixtureRejected(
            "string integer",
            root => Object(root, "town")["demandKw"] = "1000");
        ExpectFixtureRejected(
            "fractional integer",
            root => Object(root, "town")["demandKw"] = 1000.5);
        ExpectFixtureRejected(
            "exponent integer",
            ReplaceRequired(_fixtureJson, "\"demandKw\": 1000", "\"demandKw\": 1e3"));
        ExpectFixtureRejected(
            "position outside int32",
            root => Object(Object(root, "town"), "position")["x"] = (long)int.MaxValue + 1);
        ExpectFixtureRejected(
            "cash outside int64",
            ReplaceRequired(
                _fixtureJson,
                "\"initialCash\": 5000000",
                "\"initialCash\": 9223372036854775808"));
    }

    private void CheckLoaderReferencesRangesAndArithmetic()
    {
        ExpectFixtureRejected(
            "wrong schema",
            root => root["schemaVersion"] = "gridworks.product.first-light.v2");
        ExpectFixtureRejected("wrong fixture ID", root => root["fixtureId"] = "COPY");
        ExpectFixtureRejected("blank display name", root => root["displayName"] = "  ");
        ExpectFixtureRejected(
            "wrong unit",
            root => Object(root, "units")["energy"] = "MWh");
        ExpectFixtureRejected(
            "reversed x bounds",
            root => Object(root, "mapBounds")["minX"] = 21);
        ExpectFixtureRejected(
            "reversed y bounds",
            root => Object(root, "mapBounds")["minY"] = 13);
        ExpectFixtureRejected(
            "zero-width map",
            root => Object(root, "mapBounds")["maxX"] = 0);
        ExpectFixtureRejected(
            "zero-height map",
            root => Object(root, "mapBounds")["maxY"] = 0);
        ExpectFixtureRejected(
            "source outside bounds",
            root => Object(Object(root, "existingSource"), "position")["x"] = -1);
        ExpectFixtureRejected(
            "town outside bounds",
            root => Object(Object(root, "town"), "position")["x"] = 21);
        ExpectFixtureRejected(
            "source town collision",
            root => Object(root, "town")["position"] =
                Object(root, "existingSource")["position"]!.DeepClone());
        ExpectFixtureRejected(
            "blocked outside bounds",
            root => Object(Array(root, "blockedCells"), 0)["x"] = 21);
        ExpectFixtureRejected(
            "duplicate blocked cell",
            root => Array(root, "blockedCells").Add(
                Array(root, "blockedCells")[0]!.DeepClone()));
        ExpectFixtureRejected(
            "blocked source collision",
            root => Array(root, "blockedCells").Add(
                Object(root, "existingSource")["position"]!.DeepClone()));
        ExpectFixtureRejected(
            "blocked town collision",
            root => Array(root, "blockedCells").Add(
                Object(root, "town")["position"]!.DeepClone()));

        ExpectFixtureRejected(
            "blank stable ID",
            root => Object(root, "existingSource")["assetId"] = "");
        ExpectFixtureRejected(
            "duplicate stable ID",
            root => Object(root, "town")["id"] = "EXISTING_SOURCE");
        ExpectFixtureRejected(
            "wrong from terminal",
            root => Object(root, "lineProject")["fromTerminalId"] = "UNKNOWN");
        ExpectFixtureRejected(
            "wrong to terminal",
            root => Object(root, "lineProject")["toTerminalId"] = "UNKNOWN");

        ExpectFixtureRejected("negative initial minute", root => root["initialMinute"] = -1);
        ExpectFixtureRejected(
            "negative initial cash",
            root => Object(root, "economy")["initialCash"] = -1);
        ExpectFixtureRejected("zero settlement", root => root["settlementMinutes"] = 0);
        ExpectFixtureRejected(
            "zero sale rate",
            root => Object(root, "economy")["saleRateCashUnitPerGWh"] = 0);
        ExpectFixtureRejected(
            "zero demand",
            root => Object(root, "town")["demandKw"] = 0);
        ExpectFixtureRejected(
            "zero source capacity",
            root => Object(root, "existingSource")["capacityKw"] = 0);
        ExpectFixtureRejected(
            "zero substation capacity",
            root => Object(root, "substationProject")["capacityKw"] = 0);
        ExpectFixtureRejected(
            "zero service radius",
            root => Object(root, "substationProject")["serviceRadiusGridUnit"] = 0);
        ExpectFixtureRejected(
            "zero substation cost",
            root => Object(root, "substationProject")["costCashUnit"] = 0);
        ExpectFixtureRejected(
            "zero substation build",
            root => Object(root, "substationProject")["buildMinutes"] = 0);
        ExpectFixtureRejected(
            "zero line rating",
            root => Object(root, "lineProject")["ratingKw"] = 0);
        ExpectFixtureRejected(
            "zero max span",
            root => Object(root, "lineProject")["maxSpanGridUnit"] = 0);
        ExpectFixtureRejected(
            "zero support cost",
            root => Object(root, "lineProject")["supportCostCashUnit"] = 0);
        ExpectFixtureRejected(
            "zero span cost",
            root => Object(root, "lineProject")["spanCostCashUnit"] = 0);
        ExpectFixtureRejected(
            "zero support build",
            root => Object(root, "lineProject")["supportBuildMinutes"] = 0);
        ExpectFixtureRejected(
            "zero span build",
            root => Object(root, "lineProject")["spanBuildMinutes"] = 0);

        ExpectFixtureRejected(
            "map diagonal overflow",
            root =>
            {
                JsonObject bounds = Object(root, "mapBounds");
                bounds["minX"] = int.MinValue;
                bounds["maxX"] = int.MaxValue;
                bounds["minY"] = int.MinValue;
                bounds["maxY"] = int.MaxValue;
            });
        ExpectFixtureRejected(
            "map cell count overflow runtime list",
            root =>
            {
                JsonObject bounds = Object(root, "mapBounds");
                bounds["maxX"] = 50_000;
                bounds["maxY"] = 50_000;
            });
        ExpectFixtureRejected(
            "maximum line quote overflow",
            root => Object(root, "lineProject")["supportCostCashUnit"] = long.MaxValue);
        ExpectFixtureRejected(
            "potential revenue overflow",
            root => Object(root, "economy")["saleRateCashUnitPerGWh"] = long.MaxValue);
        ExpectFixtureRejected(
            "non-divisible settlement",
            root => Object(root, "economy")["saleRateCashUnitPerGWh"] = 1);

        JsonObject insufficientCash = Root();
        Object(insufficientCash, "economy")["initialCash"] = 0;
        ProductFixtureLoader.Load(insufficientCash.ToJsonString());
        Pass("loader accepts insufficient initial cash");

        JsonObject insufficientCapacity = Root();
        Object(insufficientCapacity, "existingSource")["capacityKw"] = 999;
        Object(insufficientCapacity, "lineProject")["ratingKw"] = 998;
        Object(insufficientCapacity, "substationProject")["capacityKw"] = 997;
        ProductFixtureLoader.Load(insufficientCapacity.ToJsonString());
        Pass("loader accepts insufficient capacities");
    }

    private void CheckSubstationDraftPreviewAndErrors()
    {
        ProductSession session = NewSession();
        AssertInitial(session.GetSnapshot(), "substation/initial");

        ProductSubstationPlacementPreview boundary = PreviewPure(
            session,
            () => session.PreviewSubstationPlacement(SuccessSubstation),
            "substation/boundary-preview");
        True(boundary.Accepted, "service boundary placement accepted");
        Equal(null, boundary.Error, "service boundary error");
        True(boundary.TownInServiceArea, "service boundary included");
        Equal(ProductSupplyFailure.None, boundary.ProjectedSupplyFailure, "boundary projection");

        ProductSubstationPlacementPreview outside = PreviewPure(
            session,
            () => session.PreviewSubstationPlacement(OutsideSubstation),
            "substation/outside-preview");
        True(outside.Accepted, "outside-service placement stays accepted");
        False(outside.TownInServiceArea, "outside-service geometry");
        Equal(
            ProductSupplyFailure.OutsideServiceArea,
            outside.ProjectedSupplyFailure,
            "outside-service warning");

        AssertPlacementError(session, new ProductPoint(-1, 6), ProductCommandError.OutOfBounds);
        AssertPlacementError(session, new ProductPoint(9, 4), ProductCommandError.NotBuildable);
        AssertPlacementError(
            session,
            _fixture.ExistingSource.Position,
            ProductCommandError.PositionOccupied);
        AssertPlacementError(session, _fixture.Town.Position, ProductCommandError.PositionOccupied);

        AssertAccepted(session, session.SetSubstationDraft(SuccessSubstation), "set success draft");
        ProductSnapshot firstDraft = session.GetSnapshot();
        Equal(SuccessSubstation, firstDraft.Substation.Position, "draft position");
        True(firstDraft.TownInServiceArea, "draft service eligibility");
        Equal(
            ProductSupplyFailure.SubstationNotCommissioned,
            firstDraft.SupplyFailure,
            "draft is not supply");

        ProductSnapshot beforeSame = session.GetSnapshot();
        AssertAccepted(
            session,
            session.SetSubstationDraft(SuccessSubstation),
            "set same draft idempotently");
        Equal(beforeSame, session.GetSnapshot(), "same draft changed state");

        AssertAccepted(session, session.SetSubstationDraft(OutsideSubstation), "move draft");
        Equal(OutsideSubstation, session.GetSnapshot().Substation.Position, "moved draft position");
        AssertRejected(
            session,
            () => session.SetSubstationDraft(new ProductPoint(9, 4)),
            ProductCommandError.NotBuildable,
            "invalid move preserves draft");
        Equal(OutsideSubstation, session.GetSnapshot().Substation.Position, "invalid move replaced draft");

        AssertAccepted(session, session.CancelSubstationDraft(), "cancel draft");
        Equal(null, session.GetSnapshot().Substation.Position, "cancel did not clear draft");
        ProductSnapshot beforeEmptyCancel = session.GetSnapshot();
        AssertAccepted(session, session.CancelSubstationDraft(), "empty cancel accepted");
        Equal(beforeEmptyCancel, session.GetSnapshot(), "empty cancel changed state");
        AssertRejected(
            session,
            session.OrderSubstation,
            ProductCommandError.NoDraft,
            "order requires draft");
    }

    private void CheckSubstationOrderLifecycleAndCash()
    {
        ProductSession session = NewSession();
        AssertAccepted(session, session.SetSubstationDraft(SuccessSubstation), "sub/order draft");
        ProductOrderPreview preview = PreviewPure(
            session,
            session.PreviewSubstationOrder,
            "sub/order preview");
        True(preview.Accepted, "substation order preview accepted");
        Equal(2_000_000L, preview.CostCashUnit, "substation quote cost");
        Equal(120L, preview.BuildMinutes, "substation quote build");
        Equal(120L, preview.CompletionMinute, "substation quote completion");
        Equal(ProductSupplyFailure.None, preview.ProjectedSupplyFailure, "substation projection");

        ProductCommandResult ordered = session.OrderSubstation();
        AssertAccepted(session, ordered, "substation order");
        ProductSnapshot building = ordered.Snapshot;
        Equal(ProductPhase.SubstationBuilding, building.Phase, "substation building phase");
        Equal(ProductProjectState.Building, building.Substation.ProjectState, "substation state");
        Equal(120L, building.Substation.CompletionMinute, "substation completion minute");
        Equal(3_000_000L, building.Cash, "substation order cash");
        Equal(0L, building.Minute, "order advanced time");
        Equal(0L, building.TownDeliveredKw, "building delivered power");
        Equal(
            ProductSupplyFailure.SubstationNotCommissioned,
            building.SupplyFailure,
            "building supply failure");

        Equal(
            ProductCommandError.WrongPhase,
            session.PreviewSubstationPlacement(new ProductPoint(int.MinValue, int.MaxValue)).Error,
            "wrong-phase placement precedence");
        Equal(ProductCommandError.WrongPhase, session.PreviewSubstationOrder().Error, "order repeat");
        AssertRejected(
            session,
            session.CancelSubstationDraft,
            ProductCommandError.WrongPhase,
            "cannot cancel building asset");

        AssertAccepted(
            session,
            session.AdvanceToConstructionCompletion(),
            "complete substation");
        ProductSnapshot complete = session.GetSnapshot();
        Equal(120L, complete.Minute, "substation completion advances minute");
        Equal(ProductPhase.LinePlanning, complete.Phase, "line planning phase");
        Equal(ProductProjectState.Commissioned, complete.Substation.ProjectState, "commissioned sub");
        Equal(
            ProductSupplyFailure.LineNotCommissioned,
            complete.SupplyFailure,
            "commissioned sub still no line");
        Equal(0L, complete.TownDeliveredKw, "commissioned sub alone delivered power");

        ProductFixture poorFixture = _fixture with
        {
            Economy = _fixture.Economy with { InitialCash = 1_999_999 },
        };
        ProductSession poor = NewSession(poorFixture);
        AssertAccepted(poor, poor.SetSubstationDraft(SuccessSubstation), "poor sub draft");
        ProductOrderPreview poorPreview = poor.PreviewSubstationOrder();
        False(poorPreview.Accepted, "poor sub preview accepted");
        Equal(ProductCommandError.InsufficientCash, poorPreview.Error, "poor sub error");
        Equal(2_000_000L, poorPreview.CostCashUnit, "poor sub still exposes quote");
        AssertRejected(
            poor,
            poor.OrderSubstation,
            ProductCommandError.InsufficientCash,
            "poor sub order invariant");
        True(poor.GetSnapshot().Cash >= 0, "poor sub cash became negative");
    }

    private void CheckLineDraftPreviewAndErrors()
    {
        ProductSession session = CommissionedSubstationSession(SuccessSubstation);

        AssertRejected(
            session,
            session.UndoLineSupport,
            ProductCommandError.NothingToUndo,
            "empty line undo");
        ProductLineSupportPreview boundary = PreviewPure(
            session,
            () => session.PreviewLineSupport(FirstSupport),
            "line/first boundary");
        True(boundary.Accepted, "first boundary support accepted");
        Equal(new ProductPoint(2, 6), boundary.From, "first span from");
        Equal(16L, boundary.DistanceSquared, "first span distance squared");
        Equal(16L, boundary.MaxSpanSquared, "max span squared");

        AssertLineSupportError(session, new ProductPoint(-1, 6), ProductCommandError.OutOfBounds);
        AssertLineSupportError(session, new ProductPoint(9, 4), ProductCommandError.NotBuildable);
        AssertLineSupportError(
            session,
            _fixture.ExistingSource.Position,
            ProductCommandError.PositionOccupied);
        AssertLineSupportError(session, _fixture.Town.Position, ProductCommandError.PositionOccupied);
        AssertLineSupportError(session, new ProductPoint(7, 6), ProductCommandError.SpanTooLong);

        ProductSnapshot beforeFirst = session.GetSnapshot();
        ProductCommandResult first = session.AddLineSupport(FirstSupport);
        AssertAccepted(session, first, "add first support");
        Equal(0, beforeFirst.Line.SupportPositions.Count, "past snapshot mutated after add");
        SequenceEqual([FirstSupport], first.Snapshot.Line.SupportPositions, "first support order");

        ProductOrderPreview incomplete = PreviewPure(
            session,
            session.PreviewLineOrder,
            "line/incomplete order preview");
        False(incomplete.Accepted, "too-long final span accepted");
        Equal(ProductCommandError.SpanTooLong, incomplete.Error, "final span error");
        Equal(850_000L, incomplete.CostCashUnit, "incomplete quote cost");
        Equal(110L, incomplete.BuildMinutes, "incomplete quote build");

        ProductLineSupportPreview second = session.PreviewLineSupport(SecondSupport);
        True(second.Accepted, "second support accepted");
        Equal(FirstSupport, second.From, "second span from");
        Equal(16L, second.DistanceSquared, "second span boundary");
        AssertAccepted(session, session.AddLineSupport(SecondSupport), "add second support");
        SequenceEqual(
            [FirstSupport, SecondSupport],
            session.GetSnapshot().Line.SupportPositions,
            "support input order");
        AssertLineSupportError(session, SecondSupport, ProductCommandError.PositionOccupied);
        AssertLineSupportError(
            session,
            SuccessSubstation,
            ProductCommandError.PositionOccupied);

        AssertAccepted(session, session.UndoLineSupport(), "undo second support");
        SequenceEqual([FirstSupport], session.GetSnapshot().Line.SupportPositions, "undo order");
        AssertAccepted(session, session.AddLineSupport(SecondSupport), "re-add second support");
        AssertAccepted(session, session.CancelLineDraft(), "cancel whole line draft");
        Equal(0, session.GetSnapshot().Line.SupportPositions.Count, "cancel line support count");
        ProductSnapshot beforeEmptyCancel = session.GetSnapshot();
        AssertAccepted(session, session.CancelLineDraft(), "empty line cancel");
        Equal(beforeEmptyCancel, session.GetSnapshot(), "empty line cancel changed state");
    }

    private void CheckLineOrderLifecycleAndCash()
    {
        ProductSession session = CommissionedSubstationSession(SuccessSubstation);
        AddReferenceSupports(session);

        ProductOrderPreview preview = PreviewPure(session, session.PreviewLineOrder, "line/order preview");
        True(preview.Accepted, "line order preview accepted");
        Equal(1_400_000L, preview.CostCashUnit, "line quote cost");
        Equal(180L, preview.BuildMinutes, "line quote build");
        Equal(300L, preview.CompletionMinute, "line quote completion");
        Equal(ProductSupplyFailure.None, preview.ProjectedSupplyFailure, "line projection");

        AssertAccepted(session, session.OrderLine(), "order line");
        ProductSnapshot building = session.GetSnapshot();
        Equal(ProductPhase.LineBuilding, building.Phase, "line building phase");
        Equal(ProductProjectState.Building, building.Line.ProjectState, "line state building");
        Equal(300L, building.Line.CompletionMinute, "line completion minute");
        Equal(1_600_000L, building.Cash, "line order cash");
        Equal(120L, building.Minute, "line order advanced time");
        Equal(ProductSupplyFailure.LineNotCommissioned, building.SupplyFailure, "building line supply");
        Equal(0L, building.TownDeliveredKw, "building line delivered power");

        Equal(
            ProductCommandError.WrongPhase,
            session.PreviewLineSupport(new ProductPoint(int.MaxValue, int.MinValue)).Error,
            "line wrong-phase precedence");
        Equal(ProductCommandError.WrongPhase, session.PreviewLineOrder().Error, "line repeat preview");
        AssertRejected(
            session,
            session.CancelLineDraft,
            ProductCommandError.WrongPhase,
            "cannot cancel building line");

        AssertAccepted(
            session,
            session.AdvanceToConstructionCompletion(),
            "complete line");
        ProductSnapshot commissioned = session.GetSnapshot();
        Equal(300L, commissioned.Minute, "line completion advances minute");
        Equal(ProductPhase.SettlementReady, commissioned.Phase, "settlement ready phase");
        Equal(ProductProjectState.Commissioned, commissioned.Line.ProjectState, "commissioned line");
        Equal(ProductSupplyFailure.None, commissioned.SupplyFailure, "commissioned supply result");
        Equal(1_000L, commissioned.TownDeliveredKw, "commissioned delivered kW");

        ProductSession poor = CommissionedSubstationSession(SuccessSubstation);
        ProductPoint[] fiveSupports =
        [
            new(4, 6),
            new(6, 6),
            new(8, 6),
            new(10, 6),
            new(12, 6),
        ];
        foreach (ProductPoint support in fiveSupports)
        {
            AssertAccepted(poor, poor.AddLineSupport(support), $"poor line support {support}");
        }
        ProductOrderPreview poorPreview = poor.PreviewLineOrder();
        Equal(3_050_000L, poorPreview.CostCashUnit, "poor line quote");
        Equal(ProductCommandError.InsufficientCash, poorPreview.Error, "poor line cash error");
        AssertRejected(
            poor,
            poor.OrderLine,
            ProductCommandError.InsufficientCash,
            "poor line rejected invariant");
        Equal(3_000_000L, poor.GetSnapshot().Cash, "poor line cash changed");

        ProductSession precedence = CommissionedSubstationSession(SuccessSubstation);
        ProductPoint[] expensiveButIncomplete =
        [
            new(4, 6),
            new(6, 6),
            new(8, 6),
            new(6, 8),
            new(8, 8),
        ];
        foreach (ProductPoint support in expensiveButIncomplete)
        {
            AssertAccepted(
                precedence,
                precedence.AddLineSupport(support),
                $"precedence support {support}");
        }
        ProductOrderPreview precedencePreview = precedence.PreviewLineOrder();
        Equal(ProductCommandError.SpanTooLong, precedencePreview.Error, "span must precede cash");
        Equal(3_050_000L, precedencePreview.CostCashUnit, "precedence quote remains visible");
    }

    private void CheckSupplyCapacityPrecedence()
    {
        ProductFixture sourceLow = WithCapacities(source: 999, line: 500, substation: 500);
        ProductFixture lineLow = WithCapacities(source: 1_000, line: 999, substation: 500);
        ProductFixture subLow = WithCapacities(source: 1_000, line: 1_000, substation: 999);
        ProductFixture exact = WithCapacities(source: 1_000, line: 1_000, substation: 1_000);

        AssertCapacityResult(sourceLow, ProductSupplyFailure.SourceCapacityInsufficient, 0);
        AssertCapacityResult(lineLow, ProductSupplyFailure.LineCapacityInsufficient, 0);
        AssertCapacityResult(subLow, ProductSupplyFailure.SubstationCapacityInsufficient, 0);
        AssertCapacityResult(exact, ProductSupplyFailure.None, 1_000);

        ProductSession outside = NewSession(sourceLow);
        ProductSubstationPlacementPreview outsidePreview =
            outside.PreviewSubstationPlacement(OutsideSubstation);
        Equal(
            ProductSupplyFailure.OutsideServiceArea,
            outsidePreview.ProjectedSupplyFailure,
            "outside service precedes capacity");

        ProductSession lifecycle = NewSession(sourceLow);
        AssertAccepted(lifecycle, lifecycle.SetSubstationDraft(SuccessSubstation), "capacity lifecycle draft");
        AssertAccepted(lifecycle, lifecycle.OrderSubstation(), "capacity lifecycle order sub");
        Equal(
            ProductSupplyFailure.SubstationNotCommissioned,
            lifecycle.GetSnapshot().SupplyFailure,
            "substation lifecycle precedes source capacity");
        AssertAccepted(
            lifecycle,
            lifecycle.AdvanceToConstructionCompletion(),
            "capacity lifecycle complete sub");
        Equal(
            ProductSupplyFailure.LineNotCommissioned,
            lifecycle.GetSnapshot().SupplyFailure,
            "line lifecycle precedes source capacity");
    }

    private void CheckSuccessAndFailureSettlement()
    {
        ProductSession success = SettlementReadySession(SuccessSubstation);
        ProductSnapshot beforeSuccess = success.GetSnapshot();
        Equal(300L, beforeSuccess.Minute, "success pre-settlement minute");
        Equal(1_600_000L, beforeSuccess.Cash, "success pre-settlement cash");
        Equal(ProductMissionOutcome.Pending, beforeSuccess.Outcome, "success pending outcome");
        Equal(0L, beforeSuccess.Settlement.RevenueCashUnit, "pre-settlement revenue");

        AssertAccepted(success, success.AdvanceToSettlement(), "success settlement");
        ProductSnapshot completed = success.GetSnapshot();
        Equal(ProductPhase.Complete, completed.Phase, "success complete phase");
        Equal(360L, completed.Minute, "success settlement minute");
        Equal(60_000L, completed.Settlement.DeliveredEnergyKwMinute, "success delivered energy");
        Equal(100_000L, completed.Settlement.RevenueCashUnit, "success revenue");
        Equal(1_700_000L, completed.Cash, "success ending cash");
        Equal(ProductMissionOutcome.Success, completed.Outcome, "success outcome");
        Equal(ProductSupplyFailure.None, completed.SupplyFailure, "success final supply");
        AssertRejected(
            success,
            success.AdvanceToSettlement,
            ProductCommandError.WrongPhase,
            "double settlement");
        AssertRejected(
            success,
            success.AdvanceToConstructionCompletion,
            ProductCommandError.WrongPhase,
            "complete construction after mission");

        ProductSession failure = SettlementReadySession(OutsideSubstation);
        ProductSnapshot beforeFailure = failure.GetSnapshot();
        False(beforeFailure.TownInServiceArea, "outside final service eligibility");
        Equal(
            ProductSupplyFailure.OutsideServiceArea,
            beforeFailure.SupplyFailure,
            "outside final supply failure");
        Equal(0L, beforeFailure.TownDeliveredKw, "outside delivered power");
        Equal(1_600_000L, beforeFailure.Cash, "outside pre-settlement cash");

        AssertAccepted(failure, failure.AdvanceToSettlement(), "failure settlement");
        ProductSnapshot failed = failure.GetSnapshot();
        Equal(360L, failed.Minute, "failure settlement minute");
        Equal(0L, failed.Settlement.DeliveredEnergyKwMinute, "failure energy");
        Equal(0L, failed.Settlement.RevenueCashUnit, "failure revenue");
        Equal(1_600_000L, failed.Cash, "failure ending cash");
        Equal(ProductMissionOutcome.Failure, failed.Outcome, "failure outcome");
    }

    private void CheckRestartAllPhases()
    {
        List<(string Name, Func<ProductSession> Factory)> states =
        [
            ("initial", NewSession),
            ("substation-draft", () =>
            {
                ProductSession session = NewSession();
                RequireAccepted(session.SetSubstationDraft(SuccessSubstation), "restart draft setup");
                return session;
            }
            ),
            ("substation-building", () =>
            {
                ProductSession session = NewSession();
                RequireAccepted(session.SetSubstationDraft(SuccessSubstation), "restart sub draft");
                RequireAccepted(session.OrderSubstation(), "restart sub order");
                return session;
            }
            ),
            ("line-planning", () =>
            {
                ProductSession session = CommissionedSubstationSession(SuccessSubstation);
                RequireAccepted(session.AddLineSupport(FirstSupport), "restart line support");
                return session;
            }
            ),
            ("line-building", () =>
            {
                ProductSession session = CommissionedSubstationSession(SuccessSubstation);
                AddReferenceSupportsRequired(session);
                RequireAccepted(session.OrderLine(), "restart line order");
                return session;
            }
            ),
            ("settlement-ready", () => SettlementReadySession(SuccessSubstation)),
            ("complete-success", () =>
            {
                ProductSession session = SettlementReadySession(SuccessSubstation);
                RequireAccepted(session.AdvanceToSettlement(), "restart success settlement");
                return session;
            }
            ),
            ("complete-failure", () =>
            {
                ProductSession session = SettlementReadySession(OutsideSubstation);
                RequireAccepted(session.AdvanceToSettlement(), "restart failure settlement");
                return session;
            }
            ),
        ];

        ProductSnapshot expectedInitial = NewSession().GetSnapshot();
        foreach ((string name, Func<ProductSession> factory) in states)
        {
            ProductSession session = factory();
            ProductCommandResult restarted = session.RestartMission();
            AssertAccepted(session, restarted, $"restart/{name}");
            Equal(expectedInitial, restarted.Snapshot, $"restart/{name} initial value");
            AssertInitial(restarted.Snapshot, $"restart/{name}");
            ProductSnapshot once = session.GetSnapshot();
            AssertAccepted(session, session.RestartMission(), $"restart/{name}/again");
            Equal(once, session.GetSnapshot(), $"restart/{name}/idempotent");
        }
    }

    private void CheckDeterminismAndDefensiveResults()
    {
        ProductSession first = SettlementReadySession(SuccessSubstation);
        ProductSession second = SettlementReadySession(SuccessSubstation);
        Equal(
            first.GetSnapshot(),
            second.GetSnapshot(),
            "identical command sequences diverged");
        RequireAccepted(first.AdvanceToSettlement(), "determinism first settle");
        RequireAccepted(second.AdvanceToSettlement(), "determinism second settle");
        Equal(
            first.GetSnapshot(),
            second.GetSnapshot(),
            "identical settlements diverged");

        ProductSession defensive = CommissionedSubstationSession(SuccessSubstation);
        ProductSnapshot empty = defensive.GetSnapshot();
        ProductCommandResult firstResult = defensive.AddLineSupport(FirstSupport);
        RequireAccepted(firstResult, "defensive first support");
        ProductSnapshot one = firstResult.Snapshot;
        RequireAccepted(defensive.AddLineSupport(SecondSupport), "defensive second support");
        Equal(0, empty.Line.SupportPositions.Count, "empty past snapshot mutated");
        SequenceEqual([FirstSupport], one.Line.SupportPositions, "one-support past result mutated");

        if (one.Line.SupportPositions is ICollection<ProductPoint> collection)
        {
            bool rejectedMutation = false;
            try
            {
                collection.Add(new ProductPoint(0, 0));
            }
            catch (NotSupportedException)
            {
                rejectedMutation = true;
            }
            True(rejectedMutation, "snapshot support collection allowed mutation");
        }
        else
        {
            Pass("snapshot supports are not a mutable collection");
        }

        if (_fixture.BlockedCells is ICollection<ProductPoint> blocked)
        {
            bool rejectedMutation = false;
            try
            {
                blocked.Add(new ProductPoint(0, 0));
            }
            catch (NotSupportedException)
            {
                rejectedMutation = true;
            }
            True(rejectedMutation, "fixture blocked cells allowed mutation");
        }
        else
        {
            Pass("fixture blocked cells are not a mutable collection");
        }

        ProductSession previewSession = CommissionedSubstationSession(SuccessSubstation);
        ProductSnapshot beforePreview = previewSession.GetSnapshot();
        _ = previewSession.PreviewLineSupport(FirstSupport);
        _ = previewSession.PreviewLineOrder();
        Equal(beforePreview, previewSession.GetSnapshot(), "line previews mutated state");
    }

    private void AssertCapacityResult(
        ProductFixture fixture,
        ProductSupplyFailure expectedFailure,
        long expectedDeliveredKw)
    {
        ProductSession session = NewSession(fixture);
        ProductSubstationPlacementPreview placement =
            session.PreviewSubstationPlacement(SuccessSubstation);
        True(placement.Accepted, $"capacity/{expectedFailure}/placement accepted");
        Equal(
            expectedFailure,
            placement.ProjectedSupplyFailure,
            $"capacity/{expectedFailure}/placement projection");

        ProductSession ready = SettlementReadySession(SuccessSubstation, fixture);
        ProductSnapshot snapshot = ready.GetSnapshot();
        Equal(expectedFailure, snapshot.SupplyFailure, $"capacity/{expectedFailure}/failure");
        Equal(expectedDeliveredKw, snapshot.TownDeliveredKw, $"capacity/{expectedFailure}/delivered");
    }

    private ProductFixture WithCapacities(long source, long line, long substation) => _fixture with
    {
        ExistingSource = _fixture.ExistingSource with { CapacityKw = source },
        LineProject = _fixture.LineProject with { RatingKw = line },
        SubstationProject = _fixture.SubstationProject with { CapacityKw = substation },
    };

    private ProductSession CommissionedSubstationSession(
        ProductPoint position,
        ProductFixture? fixture = null)
    {
        ProductSession session = NewSession(fixture);
        RequireAccepted(session.SetSubstationDraft(position), "commissioned-sub setup draft");
        RequireAccepted(session.OrderSubstation(), "commissioned-sub setup order");
        RequireAccepted(
            session.AdvanceToConstructionCompletion(),
            "commissioned-sub setup complete");
        return session;
    }

    private ProductSession SettlementReadySession(
        ProductPoint position,
        ProductFixture? fixture = null)
    {
        ProductSession session = CommissionedSubstationSession(position, fixture);
        AddReferenceSupportsRequired(session);
        RequireAccepted(session.OrderLine(), "settlement-ready setup order line");
        RequireAccepted(
            session.AdvanceToConstructionCompletion(),
            "settlement-ready setup complete line");
        return session;
    }

    private void AddReferenceSupports(ProductSession session)
    {
        AssertAccepted(session, session.AddLineSupport(FirstSupport), "reference first support");
        AssertAccepted(session, session.AddLineSupport(SecondSupport), "reference second support");
    }

    private static void AddReferenceSupportsRequired(ProductSession session)
    {
        RequireAccepted(session.AddLineSupport(FirstSupport), "reference first support setup");
        RequireAccepted(session.AddLineSupport(SecondSupport), "reference second support setup");
    }

    private ProductSession NewSession() => new(_fixture);

    private ProductSession NewSession(ProductFixture? fixture) => new(fixture ?? _fixture);

    private void AssertInitial(ProductSnapshot snapshot, string label)
    {
        Equal(0L, snapshot.Minute, $"{label}/minute");
        Equal(5_000_000L, snapshot.Cash, $"{label}/cash");
        Equal(ProductPhase.SubstationPlanning, snapshot.Phase, $"{label}/phase");
        Equal(null, snapshot.Substation.Position, $"{label}/sub position");
        Equal(ProductProjectState.NotOrdered, snapshot.Substation.ProjectState, $"{label}/sub state");
        Equal(null, snapshot.Substation.CompletionMinute, $"{label}/sub completion");
        Equal(0, snapshot.Line.SupportPositions.Count, $"{label}/support count");
        Equal(ProductProjectState.NotOrdered, snapshot.Line.ProjectState, $"{label}/line state");
        Equal(null, snapshot.Line.CompletionMinute, $"{label}/line completion");
        False(snapshot.TownInServiceArea, $"{label}/service");
        Equal(
            ProductSupplyFailure.SubstationNotCommissioned,
            snapshot.SupplyFailure,
            $"{label}/supply failure");
        Equal(0L, snapshot.TownDeliveredKw, $"{label}/delivered");
        False(snapshot.Settlement.Completed, $"{label}/settlement completed");
        Equal(0L, snapshot.Settlement.DeliveredEnergyKwMinute, $"{label}/energy");
        Equal(0L, snapshot.Settlement.RevenueCashUnit, $"{label}/revenue");
        Equal(ProductMissionOutcome.Pending, snapshot.Outcome, $"{label}/outcome");
    }

    private void AssertPlacementError(
        ProductSession session,
        ProductPoint position,
        ProductCommandError expected)
    {
        ProductSubstationPlacementPreview preview = PreviewPure(
            session,
            () => session.PreviewSubstationPlacement(position),
            $"placement/{expected}/preview");
        False(preview.Accepted, $"placement/{expected}/accepted");
        Equal(expected, preview.Error, $"placement/{expected}/error");
        Equal(null, preview.ProjectedSupplyFailure, $"placement/{expected}/projection");
        AssertRejected(
            session,
            () => session.SetSubstationDraft(position),
            expected,
            $"placement/{expected}/command");
    }

    private void AssertLineSupportError(
        ProductSession session,
        ProductPoint position,
        ProductCommandError expected)
    {
        ProductLineSupportPreview preview = PreviewPure(
            session,
            () => session.PreviewLineSupport(position),
            $"line-support/{expected}/preview");
        False(preview.Accepted, $"line-support/{expected}/accepted");
        Equal(expected, preview.Error, $"line-support/{expected}/error");
        AssertRejected(
            session,
            () => session.AddLineSupport(position),
            expected,
            $"line-support/{expected}/command");
    }

    private T PreviewPure<T>(ProductSession session, Func<T> action, string label)
    {
        ProductSnapshot before = session.GetSnapshot();
        T result = action();
        Equal(before, session.GetSnapshot(), $"{label} mutated state");
        return result;
    }

    private void AssertAccepted(
        ProductSession session,
        ProductCommandResult result,
        string label)
    {
        True(result.Accepted, $"{label}/accepted");
        Equal(null, result.Error, $"{label}/error");
        Equal(session.GetSnapshot(), result.Snapshot, $"{label}/snapshot");
    }

    private void AssertRejected(
        ProductSession session,
        Func<ProductCommandResult> command,
        ProductCommandError expected,
        string label)
    {
        ProductSnapshot before = session.GetSnapshot();
        ProductCommandResult result = command();
        False(result.Accepted, $"{label}/accepted");
        Equal(expected, result.Error, $"{label}/error");
        Equal(before, result.Snapshot, $"{label}/returned snapshot");
        Equal(before, session.GetSnapshot(), $"{label}/session invariant");
    }

    private static void RequireAccepted(ProductCommandResult result, string label)
    {
        if (!result.Accepted)
        {
            throw new InvalidOperationException($"{label} was rejected with {result.Error}.");
        }
    }

    private void AssertFixtureValueEqual(
        ProductFixture expected,
        ProductFixture actual,
        string label)
    {
        Equal(expected.SchemaVersion, actual.SchemaVersion, $"{label}/schema");
        Equal(expected.FixtureId, actual.FixtureId, $"{label}/fixture ID");
        Equal(expected.DisplayName, actual.DisplayName, $"{label}/display name");
        Equal(expected.Units, actual.Units, $"{label}/units");
        Equal(expected.MapBounds, actual.MapBounds, $"{label}/map bounds");
        SequenceEqual(expected.BlockedCells, actual.BlockedCells, $"{label}/blocked cells");
        Equal(expected.InitialMinute, actual.InitialMinute, $"{label}/initial minute");
        Equal(expected.SettlementMinutes, actual.SettlementMinutes, $"{label}/settlement minutes");
        Equal(expected.Economy, actual.Economy, $"{label}/economy");
        Equal(expected.ExistingSource, actual.ExistingSource, $"{label}/source");
        Equal(expected.Town, actual.Town, $"{label}/town");
        Equal(expected.SubstationProject, actual.SubstationProject, $"{label}/substation");
        Equal(expected.LineProject, actual.LineProject, $"{label}/line");
    }

    private JsonObject Root() => JsonNode.Parse(_fixtureJson)?.AsObject()
        ?? throw new InvalidOperationException("Fixture root did not parse as an object.");

    private void ExpectFixtureRejected(string label, Action<JsonObject> mutation)
    {
        JsonObject root = Root();
        mutation(root);
        ExpectFixtureRejected(label, root.ToJsonString());
    }

    private void ExpectFixtureRejected(string label, string json)
    {
        bool rejected = false;
        try
        {
            _ = ProductFixtureLoader.Load(json);
        }
        catch (ProductFixtureValidationException)
        {
            rejected = true;
        }
        True(rejected, $"loader accepted {label}");
    }

    private void ExpectFixtureRejectedBytes(string label, byte[] bytes)
    {
        bool rejected = false;
        try
        {
            _ = ProductFixtureLoader.Load(bytes);
        }
        catch (ProductFixtureValidationException)
        {
            rejected = true;
        }
        True(rejected, $"loader accepted {label}");
    }

    private static JsonObject Object(JsonObject parent, string property) =>
        parent[property]?.AsObject()
        ?? throw new InvalidOperationException($"Missing object '{property}'.");

    private static JsonObject Object(JsonArray parent, int index) =>
        parent[index]?.AsObject()
        ?? throw new InvalidOperationException($"Missing object at index {index}.");

    private static JsonArray Array(JsonObject parent, string property) =>
        parent[property]?.AsArray()
        ?? throw new InvalidOperationException($"Missing array '{property}'.");

    private static string ReplaceRequired(string text, string from, string to)
    {
        if (!text.Contains(from, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Required fixture text '{from}' was not found.");
        }
        return text.Replace(from, to, StringComparison.Ordinal);
    }

    private void SequenceEqual<T>(
        IEnumerable<T> expected,
        IEnumerable<T> actual,
        string message)
    {
        _assertionCount++;
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(message);
        }
    }

    private void Equal<T>(T expected, T actual, string message)
    {
        _assertionCount++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message}: expected '{expected}', got '{actual}'.");
        }
    }

    private void True(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private void False(bool condition, string message) => True(!condition, message);

    private void Pass(string message)
    {
        _ = message;
        _assertionCount++;
    }
}
