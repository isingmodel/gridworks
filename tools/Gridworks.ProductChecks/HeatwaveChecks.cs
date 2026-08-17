using System.Text;
using System.Text.Json.Nodes;
using Gridworks.Core.Product;

namespace Gridworks.ProductChecks;

internal sealed class HeatwaveChecks
{
    // Checker-only witnesses. They are not runtime recommendations.
    private static readonly ProductPoint SuccessSubstation = new(14, 6);
    private static readonly ProductPoint[] TownSupports = [new(6, 6), new(10, 6)];
    private static readonly ProductPoint[] PrimarySupports =
        [new(5, 5), new(9, 5), new(13, 5), new(16, 4)];
    private static readonly ProductPoint[] BackupSupports =
        [new(4, 3), new(7, 1), new(11, 1), new(15, 1)];
    private static readonly ProductPoint[] PlantConnectionSupports = [new(4, 8)];

    private readonly string _fixtureJson;
    private readonly ProductFixture _fixture;
    private int _assertionCount;

    public HeatwaveChecks(string fixturePath)
    {
        _fixtureJson = File.ReadAllText(fixturePath, Encoding.UTF8);
        _fixture = ProductFixtureLoader.Load(_fixtureJson);
    }

    public int Run()
    {
        (string Name, Action Body)[] suites =
        [
            ("heatwave-loader-inequalities-anchored-time", CheckLoaderInequalitiesAndAnchoredTime),
            ("heatwave-maintained-success-ledger", CheckMaintainedSuccessAndLedger),
            ("heatwave-skipped-outage-recovery-ledger", CheckSkippedOutageRecoveryAndLedger),
            ("heatwave-rating-demand-boundaries", CheckRatingAndDemandBoundaries),
            ("heatwave-rejected-restart-invariance", CheckRejectedAndRestartInvariance),
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
                $"Gridworks Heatwave checks: FAIL ({failures.Count}/{suites.Length} suites)");
            return 1;
        }

        Console.WriteLine(
            $"Gridworks Heatwave checks: PASS ({suites.Length} suites, {_assertionCount} assertions)");
        return 0;
    }

    private void CheckLoaderInequalitiesAndAnchoredTime()
    {
        ProductFixture bytesFixture = ProductFixtureLoader.Load(Encoding.UTF8.GetBytes(_fixtureJson));
        Equal("gridworks.product.heatwave.v1", _fixture.SchemaVersion, "schema version");
        Equal("HEATWAVE_MAINTENANCE_V1", _fixture.FixtureId, "fixture ID");
        True(_fixture.HasHospitalStage, "hospital stage missing");
        True(_fixture.HasFactoryStage, "factory stage missing");
        True(_fixture.HasHeatwaveStage, "heatwave stage missing");

        ProductHeatwaveDefinition heatwave = Heatwave(_fixture);
        Equal("FIXED_HEAT_DOME", heatwave.Id, "heatwave ID");
        Equal(180, heatwave.LeadMinutes, "heatwave lead");
        Equal(240, heatwave.DurationMinutes, "heatwave duration");
        Equal(1_500L, heatwave.TownDemandKw, "forecast town demand");
        Equal("RIVER_FACTORY_OLD_FEEDER", heatwave.AgedFactoryFeederId, "aged feeder ID");
        Equal(2_000L, heatwave.AgedFactoryFeederHeatwaveRatingKw, "forecast feeder rating");

        ProductPreventiveMaintenanceDefinition maintenance = Maintenance(_fixture);
        Equal(
            "FACTORY_FEEDER_PREVENTIVE_MAINTENANCE",
            maintenance.ProjectId,
            "maintenance project ID");
        Equal(heatwave.AgedFactoryFeederId, maintenance.TargetAssetId, "maintenance target");
        Equal(2_000_000L, maintenance.CostCashUnit, "maintenance cost");
        Equal(120, maintenance.BuildMinutes, "maintenance build time");
        Equal(heatwave, Heatwave(bytesFixture), "UTF-8 heatwave load");
        Equal(maintenance, Maintenance(bytesFixture), "UTF-8 maintenance load");

        ExpectFixtureRejected("unknown heatwave field", root =>
            Object(root, "heatwave")["unexpected"] = true);
        ExpectFixtureRejected("missing maintenance", root =>
            root.Remove("preventiveMaintenance"));
        ExpectFixtureRejected("string lead time", root =>
            Object(root, "heatwave")["leadMinutes"] = "180");
        ExpectFixtureRejected("fractional heatwave demand", root =>
            Object(root, "heatwave")["townDemandKw"] = 1_500.5);
        ExpectFixtureRejected("no town demand increase", root =>
            Object(root, "heatwave")["townDemandKw"] = 1_000);
        ExpectFixtureRejected("zero heatwave feeder rating", root =>
            Object(root, "heatwave")["agedFactoryFeederHeatwaveRatingKw"] = 0);
        ExpectFixtureRejected("no feeder derating", root =>
            Object(root, "heatwave")["agedFactoryFeederHeatwaveRatingKw"] = 2_500);
        ExpectFixtureRejected("wrong maintenance target", root =>
            Object(root, "preventiveMaintenance")["targetAssetId"] = "UNKNOWN");
        ExpectFixtureRejected("maintenance misses forecast", root =>
            Object(root, "preventiveMaintenance")["buildMinutes"] = 181);

        ProductSession maintained = MaintenanceDecisionSession(_fixture);
        ProductHeatwaveSnapshot initial = Heatwave(maintained.GetSnapshot());
        Equal(1_605L, initial.StartMinute, "anchored start at decision");
        Equal(1_845L, initial.RecoveryMinute, "anchored recovery at decision");
        Equal(ProductMaintenanceChoice.Undecided, initial.MaintenanceChoice, "initial choice");
        Equal(1_500L, initial.ForecastTownDemandKw, "snapshot forecast demand");
        Equal(2_000L, initial.ForecastFactoryFeederRatingKw, "snapshot forecast rating");

        ProductOrderPreview quote = PreviewPure(
            maintained,
            maintained.PreviewPreventiveMaintenanceOrder,
            "maintenance quote");
        Equal(2_000_000L, quote.CostCashUnit, "maintenance quote cost");
        Equal(120L, quote.BuildMinutes, "maintenance quote build");
        Equal(1_545L, quote.CompletionMinute, "maintenance completion quote");
        AssertAccepted(maintained, maintained.OrderPreventiveMaintenance(), "order maintenance");
        AssertAccepted(
            maintained,
            maintained.AdvanceToConstructionCompletion(),
            "complete maintenance");
        ProductSnapshot maintainedReady = maintained.GetSnapshot();
        Equal(ProductPhase.HeatwaveReady, maintainedReady.Phase, "maintained ready phase");
        Equal(1_545L, maintainedReady.Minute, "maintenance completion minute");
        Equal(1_605L, Heatwave(maintainedReady).StartMinute, "maintained anchored start");
        Equal(1_845L, Heatwave(maintainedReady).RecoveryMinute, "maintained anchored recovery");

        ProductSession skipped = MaintenanceDecisionSession(_fixture);
        AssertAccepted(skipped, skipped.SkipPreventiveMaintenance(), "skip maintenance");
        ProductSnapshot skippedReady = skipped.GetSnapshot();
        Equal(ProductPhase.HeatwaveReady, skippedReady.Phase, "skipped ready phase");
        Equal(1_425L, skippedReady.Minute, "skip does not advance time");
        Equal(1_605L, Heatwave(skippedReady).StartMinute, "skipped anchored start");
        Equal(1_845L, Heatwave(skippedReady).RecoveryMinute, "skipped anchored recovery");
    }

    private void CheckMaintainedSuccessAndLedger()
    {
        ProductSession session = MaintainedActiveSession(_fixture);
        ProductSnapshot active = session.GetSnapshot();
        ProductHeatwaveSnapshot heatwave = Heatwave(active);
        Equal(ProductPhase.HeatwaveActive, active.Phase, "maintained active phase");
        Equal(1_605L, active.Minute, "maintained heatwave start");
        Equal(3_820_000L, active.Cash, "maintained pre-event cash");
        True(heatwave.Active, "heatwave not active");
        Equal(ProductMaintenanceChoice.Ordered, heatwave.MaintenanceChoice, "maintained choice");
        Equal(ProductProjectState.Commissioned, heatwave.MaintenanceProjectState, "maintenance state");
        False(heatwave.AgedFactoryFeederCurrentlyUnavailable, "maintained feeder unavailable");
        False(heatwave.AgedFactoryFeederUnavailableDuringEvent, "maintained outage recorded");
        Equal(1_500L, heatwave.CurrentTownDemandKw, "active town demand");
        Equal(2_000L, heatwave.CurrentFactoryFeederRatingKw, "active feeder rating");
        AssertDispatch(
            heatwave,
            1_000, "EXISTING_SOURCE",
            1_500, "EXISTING_SOURCE",
            2_000, "NEW_GAS_PLANT",
            2_500, 2_000,
            "maintained active dispatch");

        long cashBefore = active.Cash;
        AssertAccepted(
            session,
            session.AdvanceToHeatwaveSettlement(),
            "maintained recovery settlement");
        ProductSnapshot complete = session.GetSnapshot();
        ProductHeatwaveSnapshot recovered = Heatwave(complete);
        ProductHeatwaveSettlementSnapshot ledger = recovered.Settlement;
        Equal(ProductPhase.Complete, complete.Phase, "maintained complete phase");
        Equal(ProductMissionOutcome.Success, complete.Outcome, "maintained outcome");
        Equal(1_845L, complete.Minute, "maintained recovery minute");
        Equal(4_660_000L, complete.Cash, "maintained ending cash");
        False(recovered.Active, "heatwave remained active");
        False(recovered.AgedFactoryFeederCurrentlyUnavailable, "feeder not recovered");
        Equal(1_000L, recovered.CurrentTownDemandKw, "town demand not restored");
        Equal(2_500L, recovered.CurrentFactoryFeederRatingKw, "feeder rating not restored");
        True(ledger.Completed, "maintained settlement incomplete");
        Equal(240_000L, ledger.HospitalDeliveredEnergyKwMinute, "maintained hospital energy");
        Equal(360_000L, ledger.TownDeliveredEnergyKwMinute, "maintained town energy");
        Equal(480_000L, ledger.FactoryDeliveredEnergyKwMinute, "maintained factory energy");
        Equal(600_000L, ledger.ExistingSourceGenerationEnergyKwMinute, "maintained existing energy");
        Equal(480_000L, ledger.GasPlantGenerationEnergyKwMinute, "maintained gas energy");
        Equal(0L, ledger.UtilityUnservedEnergyKwMinute, "maintained unserved energy");
        Equal(1_800_000L, ledger.UtilityRevenueCashUnit, "maintained revenue");
        Equal(400_000L, ledger.ExistingSourceGenerationCostCashUnit, "maintained existing cost");
        Equal(560_000L, ledger.GasPlantGenerationCostCashUnit, "maintained gas cost");
        Equal(0L, ledger.UnservedCompensationCashUnit, "maintained compensation");
        Equal(0L, ledger.LostSalesCashUnit, "maintained lost sales");
        Equal(840_000L, ledger.CashChangeCashUnit, "maintained cash change");
        True(ledger.AllLoadsFullySupplied, "maintained all-load condition");
        Equal(checked(cashBefore + ledger.CashChangeCashUnit), complete.Cash, "maintained cash ledger");
    }

    private void CheckSkippedOutageRecoveryAndLedger()
    {
        ProductSession session = MaintenanceDecisionSession(_fixture);
        ProductSnapshot beforeSkip = session.GetSnapshot();
        AssertAccepted(session, session.SkipPreventiveMaintenance(), "skip maintenance");
        Equal(beforeSkip.Cash, session.GetSnapshot().Cash, "skip changed cash");
        Equal(beforeSkip.Minute, session.GetSnapshot().Minute, "skip changed time");
        AssertAccepted(session, session.AdvanceToHeatwave(), "start skipped heatwave");

        ProductSnapshot active = session.GetSnapshot();
        ProductHeatwaveSnapshot heatwave = Heatwave(active);
        Equal(ProductPhase.HeatwaveActive, active.Phase, "skipped active phase");
        Equal(ProductMaintenanceChoice.Skipped, heatwave.MaintenanceChoice, "skipped choice");
        Equal(ProductProjectState.NotOrdered, heatwave.MaintenanceProjectState, "skipped project state");
        True(heatwave.AgedFactoryFeederCurrentlyUnavailable, "skipped feeder remained available");
        True(heatwave.AgedFactoryFeederUnavailableDuringEvent, "skipped outage not recorded");
        Equal(1_500L, heatwave.CurrentTownDemandKw, "skipped active town demand");
        AssertDispatch(
            heatwave,
            1_000, "EXISTING_SOURCE",
            1_500, "EXISTING_SOURCE",
            0, null,
            2_500, 0,
            "skipped active dispatch");

        long cashBefore = active.Cash;
        AssertAccepted(
            session,
            session.AdvanceToHeatwaveSettlement(),
            "skipped recovery settlement");
        ProductSnapshot complete = session.GetSnapshot();
        ProductHeatwaveSnapshot recovered = Heatwave(complete);
        ProductHeatwaveSettlementSnapshot ledger = recovered.Settlement;
        Equal(ProductPhase.Complete, complete.Phase, "skipped complete phase");
        Equal(ProductMissionOutcome.Failure, complete.Outcome, "skipped outcome");
        Equal(1_845L, complete.Minute, "skipped recovery minute");
        Equal(4_820_000L, complete.Cash, "skipped ending cash");
        False(recovered.Active, "skipped heatwave remained active");
        False(recovered.AgedFactoryFeederCurrentlyUnavailable, "skipped feeder not recovered");
        True(recovered.AgedFactoryFeederUnavailableDuringEvent, "skipped outage history lost");
        Equal(1_000L, recovered.CurrentTownDemandKw, "skipped town demand not restored");
        Equal(2_500L, recovered.CurrentFactoryFeederRatingKw, "skipped feeder rating not restored");
        True(ledger.Completed, "skipped settlement incomplete");
        Equal(240_000L, ledger.HospitalDeliveredEnergyKwMinute, "skipped hospital energy");
        Equal(360_000L, ledger.TownDeliveredEnergyKwMinute, "skipped town energy");
        Equal(0L, ledger.FactoryDeliveredEnergyKwMinute, "skipped factory energy");
        Equal(600_000L, ledger.ExistingSourceGenerationEnergyKwMinute, "skipped existing energy");
        Equal(0L, ledger.GasPlantGenerationEnergyKwMinute, "skipped gas energy");
        Equal(480_000L, ledger.UtilityUnservedEnergyKwMinute, "skipped unserved energy");
        Equal(1_000_000L, ledger.UtilityRevenueCashUnit, "skipped revenue");
        Equal(400_000L, ledger.ExistingSourceGenerationCostCashUnit, "skipped existing cost");
        Equal(0L, ledger.GasPlantGenerationCostCashUnit, "skipped gas cost");
        Equal(1_600_000L, ledger.UnservedCompensationCashUnit, "skipped compensation");
        Equal(800_000L, ledger.LostSalesCashUnit, "skipped lost sales");
        Equal(-1_000_000L, ledger.CashChangeCashUnit, "skipped cash change");
        False(ledger.AllLoadsFullySupplied, "skipped all-load condition");
        Equal(checked(cashBefore + ledger.CashChangeCashUnit), complete.Cash, "skipped cash ledger");
    }

    private void CheckRatingAndDemandBoundaries()
    {
        ProductFixture shortFeeder = _fixture with
        {
            Heatwave = Heatwave(_fixture) with { AgedFactoryFeederHeatwaveRatingKw = 1_999 },
        };
        ProductHeatwaveSnapshot shortRating = Heatwave(MaintainedActiveSession(shortFeeder).GetSnapshot());
        False(shortRating.AgedFactoryFeederCurrentlyUnavailable, "short maintained feeder unavailable");
        Equal(1_999L, shortRating.CurrentFactoryFeederRatingKw, "short feeder rating");
        AssertDispatch(
            shortRating,
            1_000, "EXISTING_SOURCE",
            1_500, "EXISTING_SOURCE",
            0, null,
            2_500, 0,
            "feeder below-demand boundary");

        ProductFixture exactTown = _fixture with
        {
            Heatwave = Heatwave(_fixture) with { TownDemandKw = 2_000 },
        };
        ProductHeatwaveSnapshot exactDemand = Heatwave(MaintainedActiveSession(exactTown).GetSnapshot());
        AssertDispatch(
            exactDemand,
            1_000, "EXISTING_SOURCE",
            2_000, "EXISTING_SOURCE",
            2_000, "NEW_GAS_PLANT",
            3_000, 2_000,
            "town exact path boundary");

        ProductFixture excessiveTown = _fixture with
        {
            Heatwave = Heatwave(_fixture) with { TownDemandKw = 2_001 },
        };
        ProductHeatwaveSnapshot noPartialTown = Heatwave(
            MaintainedActiveSession(excessiveTown).GetSnapshot());
        AssertDispatch(
            noPartialTown,
            1_000, "EXISTING_SOURCE",
            0, null,
            2_000, "EXISTING_SOURCE",
            3_000, 0,
            "town above-path all-or-none boundary");
    }

    private void CheckRejectedAndRestartInvariance()
    {
        ProductSession session = MaintenanceDecisionSession(_fixture);
        AssertRejected(
            session,
            session.AdvanceToHeatwave,
            ProductCommandError.WrongPhase,
            "heatwave before maintenance choice");
        AssertRejected(
            session,
            session.AdvanceToHeatwaveSettlement,
            ProductCommandError.WrongPhase,
            "settlement before heatwave");

        ProductFixture unaffordable = _fixture with
        {
            PreventiveMaintenance = Maintenance(_fixture) with { CostCashUnit = 6_000_000 },
        };
        ProductSession poor = MaintenanceDecisionSession(unaffordable);
        ProductOrderPreview poorQuote = PreviewPure(
            poor,
            poor.PreviewPreventiveMaintenanceOrder,
            "unaffordable maintenance quote");
        Equal(ProductCommandError.InsufficientCash, poorQuote.Error, "unaffordable quote error");
        AssertRejected(
            poor,
            poor.OrderPreventiveMaintenance,
            ProductCommandError.InsufficientCash,
            "unaffordable maintenance");

        AssertAccepted(session, session.SkipPreventiveMaintenance(), "restart skip setup");
        AssertRejected(
            session,
            session.SkipPreventiveMaintenance,
            ProductCommandError.WrongPhase,
            "duplicate skip");
        AssertRejected(
            session,
            session.OrderPreventiveMaintenance,
            ProductCommandError.WrongPhase,
            "order after skip");
        AssertAccepted(session, session.AdvanceToHeatwave(), "restart active setup");

        ProductSnapshot expectedInitial = new ProductSession(_fixture).GetSnapshot();
        AssertAccepted(session, session.RestartMission(), "heatwave restart");
        Equal(expectedInitial, session.GetSnapshot(), "heatwave restart initial state");
        ProductSnapshot once = session.GetSnapshot();
        AssertAccepted(session, session.RestartMission(), "heatwave restart again");
        Equal(once, session.GetSnapshot(), "heatwave restart idempotence");
    }

    private ProductSession MaintainedActiveSession(ProductFixture fixture)
    {
        ProductSession session = MaintenanceDecisionSession(fixture);
        RequireAccepted(session.OrderPreventiveMaintenance(), "maintained order");
        RequireAccepted(session.AdvanceToConstructionCompletion(), "maintained completion");
        RequireAccepted(session.AdvanceToHeatwave(), "maintained heatwave start");
        return session;
    }

    private ProductSession MaintenanceDecisionSession(ProductFixture fixture)
    {
        ProductSession session = new(fixture);
        RequireAccepted(session.SetSubstationDraft(SuccessSubstation), "substation draft");
        RequireAccepted(session.OrderSubstation(), "substation order");
        RequireAccepted(session.AdvanceToConstructionCompletion(), "substation completion");
        AddSupportsRequired(session, TownSupports, "town");
        RequireAccepted(session.OrderLine(), "town line order");
        RequireAccepted(session.AdvanceToConstructionCompletion(), "town line completion");
        RequireAccepted(session.AdvanceToSettlement(), "first settlement");
        BuildHospitalLineRequired(session, PrimarySupports, "primary");
        BuildHospitalLineRequired(session, BackupSupports, "backup");
        RequireAccepted(session.AdvanceToIncident(), "incident start");
        RequireAccepted(session.AdvanceToRecoveryAndSettlement(), "hospital settlement");
        RequireAccepted(session.SetPlantDraft(new ProductPoint(6, 10)), "plant draft");
        RequireAccepted(session.OrderPlant(), "plant order");
        RequireAccepted(session.AdvanceToConstructionCompletion(), "plant completion");
        AddSupportsRequired(session, PlantConnectionSupports, "plant connection");
        RequireAccepted(session.OrderLine(), "plant connection order");
        RequireAccepted(session.AdvanceToConstructionCompletion(), "plant connection completion");
        RequireAccepted(session.AdvanceToFactorySettlement(), "factory settlement");
        Equal(ProductPhase.MaintenanceDecision, session.GetSnapshot().Phase, "maintenance decision setup");
        Equal(ProductMissionOutcome.Pending, session.GetSnapshot().Outcome, "heatwave-stage pending outcome");
        Equal(1_425L, session.GetSnapshot().Minute, "maintenance decision minute");
        Equal(5_820_000L, session.GetSnapshot().Cash, "maintenance decision cash");
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
        ProductHeatwaveSnapshot snapshot,
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

    private static ProductHeatwaveDefinition Heatwave(ProductFixture fixture) =>
        fixture.Heatwave
        ?? throw new InvalidOperationException("Heatwave fixture has no heatwave definition.");

    private static ProductPreventiveMaintenanceDefinition Maintenance(ProductFixture fixture) =>
        fixture.PreventiveMaintenance
        ?? throw new InvalidOperationException("Heatwave fixture has no maintenance definition.");

    private static ProductHeatwaveSnapshot Heatwave(ProductSnapshot snapshot) =>
        snapshot.Heatwave
        ?? throw new InvalidOperationException("Heatwave snapshot has no heatwave state.");

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
