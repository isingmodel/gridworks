using System.Text;
using System.Text.Json.Nodes;
using Gridworks.Core.Product;

namespace Gridworks.ProductChecks;

internal sealed class FactoryChecks
{
    // Checker-only witnesses. Runtime data and the Game must not expose these as answers.
    private static readonly ProductPoint SuccessSubstation = new(14, 6);
    private static readonly ProductPoint[] TownSupports = [new(6, 6), new(10, 6)];
    private static readonly ProductPoint[] PrimarySupports =
        [new(5, 5), new(9, 5), new(13, 5), new(16, 4)];
    private static readonly ProductPoint[] BackupSupports =
        [new(4, 3), new(7, 1), new(11, 1), new(15, 1)];
    private static readonly ProductPoint[] NearConnectionSupports = [new(4, 8)];
    private static readonly ProductPoint[] FarConnectionSupports =
        [new(14, 11), new(11, 10), new(8, 9), new(5, 8)];

    private readonly string _fixtureJson;
    private readonly ProductFixture _fixture;
    private int _assertionCount;

    public FactoryChecks(string fixturePath)
    {
        _fixtureJson = File.ReadAllText(fixturePath, Encoding.UTF8);
        _fixture = ProductFixtureLoader.Load(_fixtureJson);
    }

    public int Run()
    {
        (string Name, Action Body)[] suites =
        [
            ("factory-loader-references-site-reservation", CheckLoaderReferencesAndSiteReservation),
            ("factory-sites-feasible-non-dominating", CheckSitesFeasibleAndNonDominating),
            ("factory-zero-before-atomic-connection", CheckZeroBeforeAtomicConnection),
            ("factory-fixed-dispatch-capacity-boundary", CheckFixedDispatchAndCapacityBoundary),
            ("factory-settlement-restart-invariance", CheckSettlementRestartAndRejectedInvariance),
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
                $"Gridworks Factory checks: FAIL ({failures.Count}/{suites.Length} suites)");
            return 1;
        }

        Console.WriteLine(
            $"Gridworks Factory checks: PASS ({suites.Length} suites, {_assertionCount} assertions)");
        return 0;
    }

    private void CheckLoaderReferencesAndSiteReservation()
    {
        ProductFixture bytesFixture = ProductFixtureLoader.Load(Encoding.UTF8.GetBytes(_fixtureJson));
        Equal("gridworks.product.factory.v1", _fixture.SchemaVersion, "schema version");
        Equal("FACTORY_CAPACITY_V1", _fixture.FixtureId, "fixture ID");
        True(_fixture.HasHospitalStage, "hospital stage missing");
        True(_fixture.HasFactoryStage, "factory stage missing");
        Equal<int?>(60, _fixture.FactorySettlementMinutes, "factory settlement minutes");

        ProductFactory factory = Factory(_fixture);
        Equal("RIVER_FACTORY", factory.Id, "factory ID");
        Equal("RIVER_FACTORY_TERMINAL", factory.TerminalId, "factory terminal");
        Equal(new ProductPoint(18, 9), factory.Position, "factory position");
        Equal(2_000L, factory.DemandKw, "factory demand");
        Equal(2, factory.Priority, "factory priority");
        Equal(2_500L, factory.FeederRatingKw, "factory feeder rating");

        ProductGasPlantProjectDefinition plant = Plant(_fixture);
        Equal("NEW_GAS_PLANT", plant.AssetId, "plant asset ID");
        Equal("NEW_GAS_PLANT_TERMINAL", plant.TerminalId, "plant terminal ID");
        Equal(2_000L, plant.CapacityKw, "plant capacity");
        Equal(2_500_000L, plant.BaseCostCashUnit, "plant base cost");
        Equal(180, plant.BuildMinutes, "plant build minutes");
        Equal(70_000_000L, plant.VariableGenerationCostCashUnitPerGWh, "plant rate");

        IReadOnlyList<ProductGasPlantSite> sites = Sites(_fixture);
        Equal(2, sites.Count, "site count");
        Equal("NEAR_EXPENSIVE_SITE", sites[0].SiteId, "near site ID");
        Equal(new ProductPoint(6, 10), sites[0].Position, "near site position");
        Equal(1_500_000L, sites[0].SiteCostCashUnit, "near site cost");
        Equal("FAR_CHEAP_SITE", sites[1].SiteId, "far site ID");
        Equal(new ProductPoint(18, 11), sites[1].Position, "far site position");
        Equal(100_000L, sites[1].SiteCostCashUnit, "far site cost");

        ProductLineProjectDefinition connection = Connection(_fixture);
        Equal(plant.TerminalId, connection.FromTerminalId, "connection from terminal");
        Equal(_fixture.ExistingSource.TerminalId, connection.ToTerminalId, "connection to terminal");
        Equal(2_500L, connection.RatingKw, "connection rating");
        Equal(_fixture.SchemaVersion, bytesFixture.SchemaVersion, "UTF-8 loader schema");
        Equal(Factory(_fixture), Factory(bytesFixture), "UTF-8 loader factory");

        ExpectFixtureRejected("unknown factory field", root =>
            Object(root, "factory")["unexpected"] = true);
        ExpectFixtureRejected("missing factory", root => root.Remove("factory"));
        ExpectFixtureRejected("string settlement minutes", root =>
            root["factorySettlementMinutes"] = "60");
        ExpectFixtureRejected("fractional factory demand", root =>
            Object(root, "factory")["demandKw"] = 2_000.5);
        ExpectFixtureRejected("one plant site", root =>
            Array(root, "gasPlantSites").RemoveAt(1));
        ExpectFixtureRejected("duplicate site ID", root =>
            Object(Array(root, "gasPlantSites"), 1)["siteId"] =
                Object(Array(root, "gasPlantSites"), 0)["siteId"]!.DeepClone());
        ExpectFixtureRejected("duplicate site position", root =>
            Object(Array(root, "gasPlantSites"), 1)["position"] =
                Object(Array(root, "gasPlantSites"), 0)["position"]!.DeepClone());
        ExpectFixtureRejected("wrong plant connection source", root =>
            Object(root, "plantConnectionLineProject")["fromTerminalId"] = "UNKNOWN");
        ExpectFixtureRejected("wrong plant connection target", root =>
            Object(root, "plantConnectionLineProject")["toTerminalId"] = "UNKNOWN");
        ExpectFixtureRejected("factory priority collision", root =>
            Object(root, "factory")["priority"] = 1);

        ProductSession nearReservation = CommissionedSubstationSession(_fixture);
        RequireAccepted(nearReservation.AddLineSupport(new ProductPoint(4, 8)), "near approach");
        AssertRejected(
            nearReservation,
            () => nearReservation.AddLineSupport(sites[0].Position),
            ProductCommandError.PositionOccupied,
            "near site reserved from supports");

        ProductSession farReservation = CommissionedSubstationSession(_fixture);
        foreach (ProductPoint support in
            new[] { new ProductPoint(5, 8), new ProductPoint(8, 9), new ProductPoint(11, 10), new ProductPoint(14, 11) })
        {
            RequireAccepted(farReservation.AddLineSupport(support), $"far approach {support}");
        }
        AssertRejected(
            farReservation,
            () => farReservation.AddLineSupport(sites[1].Position),
            ProductCommandError.PositionOccupied,
            "far site reserved from supports");
    }

    private void CheckSitesFeasibleAndNonDominating()
    {
        ProductSession near = PlantPlanningSession(_fixture);
        ProductOrderPreview nearPlant = OrderPlantRequired(near, Sites(_fixture)[0]);
        Equal(4_000_000L, nearPlant.CostCashUnit, "near plant cost");
        Equal(180L, nearPlant.BuildMinutes, "near plant build time");
        ProductOrderPreview nearLine = OrderConnectionRequired(near, NearConnectionSupports);
        Equal(400_000L, nearLine.CostCashUnit, "near connection cost");
        Equal(65L, nearLine.BuildMinutes, "near connection build time");
        ProductSnapshot nearReady = near.GetSnapshot();
        Equal(ProductPhase.FactorySettlementReady, nearReady.Phase, "near ready phase");
        Equal(1_365L, nearReady.Minute, "near ready minute");
        Equal(5_640_000L, nearReady.Cash, "near ready cash");
        Equal(2_000L, Factory(nearReady).FactoryDeliveredKw, "near factory delivery");

        ProductSession far = PlantPlanningSession(_fixture);
        ProductOrderPreview farPlant = OrderPlantRequired(far, Sites(_fixture)[1]);
        Equal(2_600_000L, farPlant.CostCashUnit, "far plant cost");
        Equal(180L, farPlant.BuildMinutes, "far plant build time");
        ProductOrderPreview farLine = OrderConnectionRequired(far, FarConnectionSupports);
        Equal(1_150_000L, farLine.CostCashUnit, "far connection cost");
        Equal(185L, farLine.BuildMinutes, "far connection build time");
        ProductSnapshot farReady = far.GetSnapshot();
        Equal(ProductPhase.FactorySettlementReady, farReady.Phase, "far ready phase");
        Equal(1_485L, farReady.Minute, "far ready minute");
        Equal(6_290_000L, farReady.Cash, "far ready cash");
        Equal(2_000L, Factory(farReady).FactoryDeliveredKw, "far factory delivery");

        True(nearReady.Minute < farReady.Minute, "near site was not faster");
        True(nearReady.Cash < farReady.Cash, "near site was not more expensive");
    }

    private void CheckZeroBeforeAtomicConnection()
    {
        ProductSession session = PlantPlanningSession(_fixture);
        ProductFactorySnapshot planning = Factory(session.GetSnapshot());
        Equal(ProductProjectState.NotOrdered, planning.PlantProjectState, "planning plant state");
        Equal(0L, planning.FactoryDeliveredKw, "unbuilt factory delivery");
        Equal(0L, planning.GasPlantDispatchKw, "unbuilt plant dispatch");

        ProductGasPlantSite near = Sites(_fixture)[0];
        AssertAccepted(session, session.SetPlantDraft(near.Position), "set near plant draft");
        AssertAccepted(session, session.OrderPlant(), "order near plant");
        ProductFactorySnapshot building = Factory(session.GetSnapshot());
        Equal(ProductProjectState.Building, building.PlantProjectState, "building plant state");
        Equal(0L, building.FactoryDeliveredKw, "building factory delivery");
        Equal(0L, building.GasPlantDispatchKw, "building plant dispatch");

        AssertAccepted(session, session.AdvanceToConstructionCompletion(), "complete plant");
        ProductFactorySnapshot disconnected = Factory(session.GetSnapshot());
        Equal(ProductProjectState.Commissioned, disconnected.PlantProjectState, "commissioned plant state");
        False(disconnected.PlantGridConnected, "disconnected plant marked connected");
        Equal(0L, disconnected.FactoryDeliveredKw, "disconnected factory delivery");
        Equal(0L, disconnected.GasPlantDispatchKw, "disconnected plant dispatch");

        AddSupportsRequired(session, NearConnectionSupports, "atomic connection");
        AssertAccepted(session, session.OrderLine(), "order connection");
        ProductFactorySnapshot lineBuilding = Factory(session.GetSnapshot());
        Equal(ProductProjectState.Building, lineBuilding.ConnectionLine.ProjectState, "connection state");
        False(lineBuilding.PlantGridConnected, "building connection marked connected");
        Equal(0L, lineBuilding.FactoryDeliveredKw, "building connection factory delivery");
        Equal(0L, lineBuilding.GasPlantDispatchKw, "building connection plant dispatch");

        AssertAccepted(session, session.AdvanceToConstructionCompletion(), "complete connection");
        ProductFactorySnapshot connected = Factory(session.GetSnapshot());
        Equal(ProductProjectState.Commissioned, connected.ConnectionLine.ProjectState, "completed connection");
        True(connected.PlantGridConnected, "completed connection not connected");
        Equal(2_000L, connected.FactoryDeliveredKw, "atomic factory delivery");
        Equal(2_000L, connected.GasPlantDispatchKw, "atomic plant dispatch");
    }

    private void CheckFixedDispatchAndCapacityBoundary()
    {
        ProductFactorySnapshot baseline = Factory(FactoryReadySession(_fixture).GetSnapshot());
        AssertDispatch(
            baseline,
            1_000, "EXISTING_SOURCE",
            1_000, "EXISTING_SOURCE",
            2_000, "NEW_GAS_PLANT",
            2_000, 2_000,
            "baseline");

        ProductFixture abundantExisting = _fixture with
        {
            ExistingSource = _fixture.ExistingSource with { CapacityKw = 4_000 },
        };
        ProductFactorySnapshot abundant = Factory(FactoryReadySession(abundantExisting).GetSnapshot());
        AssertDispatch(
            abundant,
            1_000, "EXISTING_SOURCE",
            1_000, "EXISTING_SOURCE",
            2_000, "EXISTING_SOURCE",
            4_000, 0,
            "existing-source merit");

        ProductFixture shortPlant = _fixture with
        {
            GasPlantProject = Plant(_fixture) with { CapacityKw = 1_999 },
        };
        ProductFactorySnapshot noPartialFactory = Factory(FactoryReadySession(shortPlant).GetSnapshot());
        AssertDispatch(
            noPartialFactory,
            1_000, "EXISTING_SOURCE",
            1_000, "EXISTING_SOURCE",
            0, null,
            2_000, 0,
            "factory all-or-none boundary");

        ProductFixture shortConnection = _fixture with
        {
            PlantConnectionLineProject = Connection(_fixture) with { RatingKw = 1_999 },
        };
        ProductFactorySnapshot noOverratedConnection =
            Factory(FactoryReadySession(shortConnection).GetSnapshot());
        AssertDispatch(
            noOverratedConnection,
            1_000, "EXISTING_SOURCE",
            1_000, "EXISTING_SOURCE",
            0, null,
            2_000, 0,
            "plant connection all-or-none boundary");

        ProductFixture constrainedExisting = _fixture with
        {
            ExistingSource = _fixture.ExistingSource with { CapacityKw = 1_500 },
        };
        ProductFactorySnapshot priority = Factory(FactoryReadySession(constrainedExisting).GetSnapshot());
        AssertDispatch(
            priority,
            1_000, "EXISTING_SOURCE",
            1_000, "NEW_GAS_PLANT",
            0, null,
            1_000, 1_000,
            "load priority and no split");
    }

    private void CheckSettlementRestartAndRejectedInvariance()
    {
        ProductSession planning = PlantPlanningSession(_fixture);
        AssertRejected(
            planning,
            () => planning.SetPlantDraft(new ProductPoint(7, 10)),
            ProductCommandError.NotBuildable,
            "non-site plant position");
        AssertRejected(
            planning,
            planning.AdvanceToFactorySettlement,
            ProductCommandError.WrongPhase,
            "early factory settlement");

        ProductSession session = FactoryReadySession(_fixture);
        ProductSnapshot before = session.GetSnapshot();
        Equal(1_365L, before.Minute, "pre-settlement minute");
        Equal(5_640_000L, before.Cash, "pre-settlement cash");
        AssertAccepted(session, session.AdvanceToFactorySettlement(), "factory settlement");

        ProductSnapshot complete = session.GetSnapshot();
        ProductFactorySettlementSnapshot settlement = Factory(complete).Settlement;
        Equal(ProductPhase.Complete, complete.Phase, "complete phase");
        Equal(ProductMissionOutcome.Success, complete.Outcome, "complete outcome");
        Equal(1_425L, complete.Minute, "complete minute");
        Equal(5_820_000L, complete.Cash, "complete cash");
        True(settlement.Completed, "settlement completion");
        Equal(60_000L, settlement.HospitalDeliveredEnergyKwMinute, "hospital energy");
        Equal(60_000L, settlement.TownDeliveredEnergyKwMinute, "town energy");
        Equal(120_000L, settlement.FactoryDeliveredEnergyKwMinute, "factory energy");
        Equal(120_000L, settlement.ExistingSourceGenerationEnergyKwMinute, "existing energy");
        Equal(120_000L, settlement.GasPlantGenerationEnergyKwMinute, "gas energy");
        Equal(0L, settlement.UtilityUnservedEnergyKwMinute, "unserved energy");
        Equal(400_000L, settlement.UtilityRevenueCashUnit, "revenue");
        Equal(80_000L, settlement.ExistingSourceGenerationCostCashUnit, "existing cost");
        Equal(140_000L, settlement.GasPlantGenerationCostCashUnit, "gas cost");
        Equal(0L, settlement.UnservedCompensationCashUnit, "compensation");
        Equal(0L, settlement.LostSalesCashUnit, "lost sales diagnostic");
        Equal(180_000L, settlement.CashChangeCashUnit, "cash change");
        True(settlement.AllLoadsFullySupplied, "all-load condition");
        Equal(
            checked(before.Cash + settlement.CashChangeCashUnit),
            complete.Cash,
            "cash ledger application");

        AssertRejected(
            session,
            session.AdvanceToFactorySettlement,
            ProductCommandError.WrongPhase,
            "duplicate settlement");
        ProductSnapshot expectedInitial = new ProductSession(_fixture).GetSnapshot();
        AssertAccepted(session, session.RestartMission(), "factory restart");
        Equal(expectedInitial, session.GetSnapshot(), "factory restart initial state");
        ProductSnapshot once = session.GetSnapshot();
        AssertAccepted(session, session.RestartMission(), "factory restart again");
        Equal(once, session.GetSnapshot(), "factory restart idempotence");
    }

    private ProductSession FactoryReadySession(ProductFixture fixture)
    {
        ProductSession session = PlantPlanningSession(fixture);
        _ = OrderPlantRequired(session, Sites(fixture)[0]);
        _ = OrderConnectionRequired(session, NearConnectionSupports);
        return session;
    }

    private ProductOrderPreview OrderPlantRequired(
        ProductSession session,
        ProductGasPlantSite site)
    {
        ProductPlantPlacementPreview placement = PreviewPure(
            session,
            () => session.PreviewPlantPlacement(site.Position),
            $"{site.SiteId} placement");
        True(placement.Accepted, $"{site.SiteId} placement rejected");
        Equal(site.SiteId, placement.SiteId, $"{site.SiteId} placement site");
        AssertAccepted(session, session.SetPlantDraft(site.Position), $"{site.SiteId} draft");
        ProductOrderPreview preview = PreviewPure(
            session,
            session.PreviewPlantOrder,
            $"{site.SiteId} plant order");
        True(preview.Accepted, $"{site.SiteId} plant order rejected");
        AssertAccepted(session, session.OrderPlant(), $"{site.SiteId} plant order");
        AssertAccepted(
            session,
            session.AdvanceToConstructionCompletion(),
            $"{site.SiteId} plant completion");
        return preview;
    }

    private ProductOrderPreview OrderConnectionRequired(
        ProductSession session,
        IReadOnlyList<ProductPoint> supports)
    {
        AddSupportsRequired(session, supports, "plant connection");
        ProductOrderPreview preview = PreviewPure(
            session,
            session.PreviewLineOrder,
            "plant connection order");
        True(preview.Accepted, "plant connection order rejected");
        AssertAccepted(session, session.OrderLine(), "plant connection order");
        AssertAccepted(
            session,
            session.AdvanceToConstructionCompletion(),
            "plant connection completion");
        return preview;
    }

    private ProductSession PlantPlanningSession(ProductFixture fixture)
    {
        ProductSession session = CommissionedSubstationSession(fixture);
        AddSupportsRequired(session, TownSupports, "town");
        RequireAccepted(session.OrderLine(), "town line order");
        RequireAccepted(session.AdvanceToConstructionCompletion(), "town line completion");
        RequireAccepted(session.AdvanceToSettlement(), "first settlement");
        BuildHospitalLineRequired(session, PrimarySupports, "primary");
        BuildHospitalLineRequired(session, BackupSupports, "backup");
        RequireAccepted(session.AdvanceToIncident(), "incident start");
        RequireAccepted(session.AdvanceToRecoveryAndSettlement(), "hospital settlement");
        Equal(ProductPhase.PlantPlanning, session.GetSnapshot().Phase, "plant planning setup");
        Equal(ProductMissionOutcome.Pending, session.GetSnapshot().Outcome, "factory-stage pending outcome");
        return session;
    }

    private static ProductSession CommissionedSubstationSession(ProductFixture fixture)
    {
        ProductSession session = new(fixture);
        RequireAccepted(session.SetSubstationDraft(SuccessSubstation), "substation draft");
        RequireAccepted(session.OrderSubstation(), "substation order");
        RequireAccepted(session.AdvanceToConstructionCompletion(), "substation completion");
        return session;
    }

    private static void BuildHospitalLineRequired(
        ProductSession session,
        IReadOnlyList<ProductPoint> supports,
        string label)
    {
        AddSupportsRequired(session, supports, label);
        RequireAccepted(session.OrderLine(), $"{label} line order");
        RequireAccepted(session.AdvanceToConstructionCompletion(), $"{label} line completion");
    }

    private static void AddSupportsRequired(
        ProductSession session,
        IEnumerable<ProductPoint> supports,
        string label)
    {
        foreach (ProductPoint support in supports)
        {
            RequireAccepted(session.AddLineSupport(support), $"{label} support {support}");
        }
    }

    private void AssertDispatch(
        ProductFactorySnapshot snapshot,
        long hospitalKw,
        string? hospitalSource,
        long townKw,
        string? townSource,
        long factoryKw,
        string? factorySource,
        long existingKw,
        long gasKw,
        string label)
    {
        Equal(hospitalKw, snapshot.HospitalDeliveredKw, $"{label}/hospital delivery");
        Equal(hospitalSource, snapshot.HospitalSourceAssetId, $"{label}/hospital source");
        Equal(townKw, snapshot.TownDeliveredKw, $"{label}/town delivery");
        Equal(townSource, snapshot.TownSourceAssetId, $"{label}/town source");
        Equal(factoryKw, snapshot.FactoryDeliveredKw, $"{label}/factory delivery");
        Equal(factorySource, snapshot.FactorySourceAssetId, $"{label}/factory source");
        Equal(existingKw, snapshot.ExistingSourceDispatchKw, $"{label}/existing dispatch");
        Equal(gasKw, snapshot.GasPlantDispatchKw, $"{label}/gas dispatch");
    }

    private static ProductFactory Factory(ProductFixture fixture) =>
        fixture.Factory
        ?? throw new InvalidOperationException("Factory fixture has no factory.");

    private static ProductGasPlantProjectDefinition Plant(ProductFixture fixture) =>
        fixture.GasPlantProject
        ?? throw new InvalidOperationException("Factory fixture has no gas plant project.");

    private static IReadOnlyList<ProductGasPlantSite> Sites(ProductFixture fixture) =>
        fixture.GasPlantSites
        ?? throw new InvalidOperationException("Factory fixture has no plant sites.");

    private static ProductLineProjectDefinition Connection(ProductFixture fixture) =>
        fixture.PlantConnectionLineProject
        ?? throw new InvalidOperationException("Factory fixture has no connection project.");

    private static ProductFactorySnapshot Factory(ProductSnapshot snapshot) =>
        snapshot.Factory
        ?? throw new InvalidOperationException("Factory snapshot has no factory state.");

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

    private JsonObject Root() => JsonNode.Parse(_fixtureJson)?.AsObject()
        ?? throw new InvalidOperationException("Fixture root did not parse as an object.");

    private void ExpectFixtureRejected(string label, Action<JsonObject> mutation)
    {
        JsonObject root = Root();
        mutation(root);
        bool rejected = false;
        try
        {
            _ = ProductFixtureLoader.Load(root.ToJsonString());
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
}
