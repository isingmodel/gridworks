using System.Text;
using System.Text.Json.Nodes;
using Gridworks.Core.Product;

namespace Gridworks.ProductChecks;

internal sealed class SecondHeartChecks
{
    // Checker-only routes. They are deliberately absent from runtime data and Game UI.
    private static readonly ProductPoint SuccessSubstation = new(14, 6);
    private static readonly ProductPoint[] TownSupports = [new(6, 6), new(10, 6)];
    private static readonly ProductPoint[] ExposedPrimarySupports =
        [new(5, 5), new(9, 5), new(13, 5), new(16, 4)];
    private static readonly ProductPoint[] SafeBackupSupports =
        [new(4, 3), new(7, 1), new(11, 1), new(15, 1)];
    private static readonly ProductPoint[] ExposedBackupSupports =
        [new(5, 7), new(9, 7), new(13, 7), new(16, 5)];
    private static readonly ProductPoint[] ClosedBoundarySupports =
        [new(4, 4), new(7, 3), new(8, 2), new(12, 2), new(15, 2)];

    private readonly string _fixtureJson;
    private readonly ProductFixture _fixture;
    private int _assertionCount;

    public SecondHeartChecks(string fixturePath)
    {
        _fixtureJson = File.ReadAllText(fixturePath, Encoding.UTF8);
        _fixture = ProductFixtureLoader.Load(_fixtureJson);
    }

    public int Run()
    {
        (string Name, Action Body)[] suites =
        [
            ("second-heart-loader-shape-references", CheckLoaderShapeAndReferences),
            ("second-heart-reliability-priority-success", CheckReliabilityPriorityAndSuccess),
            ("second-heart-common-risk-internal-ledger", CheckCommonRiskInternalPowerAndLedger),
            ("second-heart-closed-risk-boundary", CheckClosedRiskBoundary),
            ("second-heart-rejected-restart-invariance", CheckRejectedAndRestartInvariance),
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
                $"Gridworks Second Heart checks: FAIL ({failures.Count}/{suites.Length} suites)");
            return 1;
        }

        Console.WriteLine(
            $"Gridworks Second Heart checks: PASS ({suites.Length} suites, {_assertionCount} assertions)");
        return 0;
    }

    private void CheckLoaderShapeAndReferences()
    {
        ProductFixture bytesFixture = ProductFixtureLoader.Load(
            Encoding.UTF8.GetBytes(_fixtureJson));
        Equal("gridworks.product.second-heart.v1", _fixture.SchemaVersion, "schema version");
        Equal("SECOND_HEART_V1", _fixture.FixtureId, "fixture ID");
        True(_fixture.HasHospitalStage, "hospital stage missing");
        Equal(1, _fixture.Town.Priority, "town priority");

        ProductHospital hospital = Hospital(_fixture);
        Equal("REGIONAL_HOSPITAL", hospital.Id, "hospital ID");
        Equal(new ProductPoint(18, 3), hospital.Position, "hospital position");
        Equal(0, hospital.Priority, "hospital priority");
        Equal(15, hospital.UpsMinutes, "UPS minutes");
        Equal(285, hospital.DieselMinutes, "diesel minutes");

        IReadOnlyList<ProductHospitalLineProjectDefinition> lines = HospitalLines(_fixture);
        Equal(2, lines.Count, "hospital line count");
        Equal("HOSPITAL_PRIMARY_LINE", lines[0].ProjectId, "primary project ID");
        Equal(0, lines[0].RoutePriority, "primary route priority");
        Equal(hospital.PrimaryTerminalId, lines[0].ToTerminalId, "primary terminal reference");
        Equal("HOSPITAL_BACKUP_LINE", lines[1].ProjectId, "backup project ID");
        Equal(1, lines[1].RoutePriority, "backup route priority");
        Equal(hospital.BackupTerminalId, lines[1].ToTerminalId, "backup terminal reference");
        Equal(_fixture.SchemaVersion, bytesFixture.SchemaVersion, "UTF-8 loader schema");
        Equal(Hospital(_fixture), Hospital(bytesFixture), "UTF-8 loader hospital");

        ExpectFixtureRejected("unknown hospital field", root =>
            Object(root, "hospital")["unexpected"] = true);
        ExpectFixtureRejected("missing hospital", root => root.Remove("hospital"));
        ExpectFixtureRejected("one hospital line", root =>
            Array(root, "hospitalLineProjects").RemoveAt(1));
        ExpectFixtureRejected("duplicate project ID", root =>
            Object(Array(root, "hospitalLineProjects"), 1)["projectId"] =
                Object(Array(root, "hospitalLineProjects"), 0)["projectId"]!.DeepClone());
        ExpectFixtureRejected("wrong primary terminal", root =>
            Object(Array(root, "hospitalLineProjects"), 0)["toTerminalId"] = "UNKNOWN");
        ExpectFixtureRejected("wrong hospital source terminal", root =>
            Object(Array(root, "hospitalLineProjects"), 1)["fromTerminalId"] = "UNKNOWN");
        ExpectFixtureRejected("duplicate route priority", root =>
            Object(Array(root, "hospitalLineProjects"), 1)["routePriority"] = 0);
        ExpectFixtureRejected("reversed risk x bounds", root =>
            Object(Object(root, "spatialIncident"), "riskRect")["minX"] = 12);
        ExpectFixtureRejected("risk rectangle outside map", root =>
            Object(Object(root, "spatialIncident"), "riskRect")["maxY"] = 13);
        ExpectFixtureRejected("hospital outside map", root =>
            Object(Object(root, "hospital"), "position")["x"] = 21);
        ExpectFixtureRejected("zero incident duration", root =>
            Object(root, "spatialIncident")["durationMinutes"] = 0);
        ExpectFixtureRejected("fractional hospital demand", root =>
            Object(root, "hospital")["demandKw"] = 1000.5);
    }

    private void CheckReliabilityPriorityAndSuccess()
    {
        ProductSession session = IncidentReadySession(
            _fixture,
            ExposedPrimarySupports,
            SafeBackupSupports);
        ProductSnapshot ready = session.GetSnapshot();
        ProductHospitalSnapshot hospital = Hospital(ready);
        Equal(ProductPhase.IncidentReady, ready.Phase, "success incident-ready phase");
        Equal("HOSPITAL_PRIMARY_LINE", hospital.SelectedHospitalProjectId, "route priority");
        Equal(1_000L, hospital.HospitalUtilityKw, "ready hospital utility");
        Equal(1_000L, hospital.TownUtilityKw, "ready town utility");
        True(hospital.PrimaryLine.SpatialIncidentExposed, "primary should be exposed");
        False(hospital.BackupLine.SpatialIncidentExposed, "backup should be spatially safe");

        ProductReliabilitySnapshot reliability = PreviewPure(
            session,
            session.PreviewReliability,
            "reliability preview");
        True(reliability.Evaluated, "reliability not evaluated");
        True(reliability.PrimaryRemovalKeepsHospitalUtility, "primary removal lost hospital");
        True(reliability.BackupRemovalKeepsHospitalUtility, "backup removal lost hospital");
        True(reliability.AllSingleLineRemovalsKeepHospitalUtility, "N-1 aggregate failed");

        ProductFixture constrainedFixture = _fixture with
        {
            ExistingSource = _fixture.ExistingSource with { CapacityKw = 1_500 },
        };
        ProductSession constrained = IncidentReadySession(
            constrainedFixture,
            ExposedPrimarySupports,
            SafeBackupSupports);
        ProductHospitalSnapshot constrainedHospital = Hospital(constrained.GetSnapshot());
        Equal(1_000L, constrainedHospital.HospitalUtilityKw, "priority hospital utility");
        Equal(0L, constrainedHospital.TownUtilityKw, "priority town must not be partially served");
        Equal(
            "HOSPITAL_PRIMARY_LINE",
            constrainedHospital.SelectedHospitalProjectId,
            "priority scenario route choice");

        AssertAccepted(session, session.AdvanceToIncident(), "advance to incident");
        ProductSnapshot active = session.GetSnapshot();
        ProductHospitalSnapshot activeHospital = Hospital(active);
        Equal(ProductPhase.IncidentActive, active.Phase, "incident active phase");
        Equal("HOSPITAL_BACKUP_LINE", activeHospital.SelectedHospitalProjectId, "event backup choice");
        Equal(1_000L, activeHospital.HospitalUtilityKw, "event hospital utility");
        Equal(0L, activeHospital.TownUtilityKw, "event town utility");
        True(
            activeHospital.Incident.UnavailableProjectIds.Contains("HOSPITAL_PRIMARY_LINE"),
            "event did not remove exposed primary");
        False(
            activeHospital.Incident.UnavailableProjectIds.Contains("HOSPITAL_BACKUP_LINE"),
            "event removed safe backup");

        AssertAccepted(
            session,
            session.AdvanceToRecoveryAndSettlement(),
            "recover and settle success");
        ProductSnapshot complete = session.GetSnapshot();
        ProductHospitalSettlementSnapshot settlement = Hospital(complete).Settlement;
        Equal(ProductPhase.Complete, complete.Phase, "success complete phase");
        Equal(ProductMissionOutcome.Success, complete.Outcome, "success outcome");
        True(settlement.SingleLineRemovalConditionMet, "final N-1 condition");
        True(settlement.SpatialIncidentUtilityConditionMet, "final spatial utility condition");
        True(settlement.HospitalP0ConditionMet, "final P0 condition");
        Equal(0L, settlement.HospitalP0UnservedEnergyKwMinute, "success P0 unserved");
    }

    private void CheckCommonRiskInternalPowerAndLedger()
    {
        ProductSession session = IncidentReadySession(
            _fixture,
            ExposedPrimarySupports,
            ExposedBackupSupports);
        ProductHospitalSnapshot ready = Hospital(session.GetSnapshot());
        True(ready.Reliability.AllSingleLineRemovalsKeepHospitalUtility, "electrical N-1 failed");
        True(ready.PrimaryLine.SpatialIncidentExposed, "counterexample primary not exposed");
        True(ready.BackupLine.SpatialIncidentExposed, "counterexample backup not exposed");

        AssertAccepted(session, session.AdvanceToIncident(), "counterexample incident");
        ProductHospitalSnapshot active = Hospital(session.GetSnapshot());
        Equal(null, active.SelectedHospitalProjectId, "counterexample selected a route");
        Equal(0L, active.HospitalUtilityKw, "counterexample hospital utility");
        Equal(1_000L, active.HospitalP0DeliveredKw, "internal power did not cover P0");
        Equal(0L, active.TownUtilityKw, "counterexample town utility");
        True(
            active.Incident.UnavailableProjectIds.Contains("HOSPITAL_PRIMARY_LINE"),
            "counterexample primary not removed");
        True(
            active.Incident.UnavailableProjectIds.Contains("HOSPITAL_BACKUP_LINE"),
            "counterexample backup not removed");

        long cashBefore = session.GetSnapshot().Cash;
        AssertAccepted(
            session,
            session.AdvanceToRecoveryAndSettlement(),
            "counterexample recovery");
        ProductSnapshot complete = session.GetSnapshot();
        ProductHospitalSettlementSnapshot settlement = Hospital(complete).Settlement;
        Equal(ProductMissionOutcome.Failure, complete.Outcome, "counterexample outcome");
        True(settlement.SingleLineRemovalConditionMet, "counterexample electrical N-1");
        False(settlement.SpatialIncidentUtilityConditionMet, "counterexample spatial condition");
        True(settlement.HospitalP0ConditionMet, "counterexample P0 condition");
        Equal(15_000L, settlement.UpsEnergyKwMinute, "UPS energy");
        Equal(225_000L, settlement.DieselEnergyKwMinute, "diesel energy");
        Equal(0L, settlement.HospitalP0UnservedEnergyKwMinute, "P0 unserved energy");
        Equal(0L, settlement.UtilityGenerationEnergyKwMinute, "utility generation energy");
        Equal(480_000L, settlement.UtilityUnservedEnergyKwMinute, "utility unserved energy");
        Equal(0L, settlement.UtilityRevenueCashUnit, "utility revenue");
        Equal(0L, settlement.GenerationCostCashUnit, "generation cost");
        Equal(1_600_000L, settlement.UnservedCompensationCashUnit, "compensation");
        Equal(800_000L, settlement.LostSalesCashUnit, "lost sales diagnostic");

        long cashEquation = checked(
            settlement.UtilityRevenueCashUnit -
            settlement.GenerationCostCashUnit -
            settlement.UnservedCompensationCashUnit);
        Equal(cashEquation, settlement.CashChangeCashUnit, "cash ledger equation");
        Equal(
            checked(cashBefore + settlement.CashChangeCashUnit),
            complete.Cash,
            "cash settlement application");
        False(
            settlement.CashChangeCashUnit ==
            checked(cashEquation - settlement.LostSalesCashUnit),
            "LostSales was deducted from cash");
    }

    private void CheckClosedRiskBoundary()
    {
        ProductSession boundary = PrimaryPlanningSession(_fixture);
        AddSupportsRequired(boundary, ClosedBoundarySupports, "closed boundary");
        ProductOrderPreview boundaryPreview = PreviewPure(
            boundary,
            boundary.PreviewLineOrder,
            "closed boundary order preview");
        True(boundaryPreview.Accepted, "closed boundary route rejected");
        Equal(true, boundaryPreview.SpatialIncidentExposed, "risk boundary must be included");

        ProductSession outside = PrimaryPlanningSession(_fixture);
        AddSupportsRequired(outside, SafeBackupSupports, "outside boundary");
        ProductOrderPreview outsidePreview = PreviewPure(
            outside,
            outside.PreviewLineOrder,
            "outside boundary order preview");
        True(outsidePreview.Accepted, "safe route rejected");
        Equal(false, outsidePreview.SpatialIncidentExposed, "safe route marked exposed");
    }

    private void CheckRejectedAndRestartInvariance()
    {
        ProductSession session = PrimaryPlanningSession(_fixture);
        RequireAccepted(session.AddLineSupport(ExposedPrimarySupports[0]), "rejection setup");
        AssertRejected(
            session,
            () => session.AddLineSupport(TownSupports[0]),
            ProductCommandError.PositionOccupied,
            "hospital support cannot reuse town support");
        AddSupportsRequired(session, ExposedPrimarySupports[1..], "finish primary");
        RequireAccepted(session.OrderLine(), "order primary");
        RequireAccepted(session.AdvanceToConstructionCompletion(), "complete primary");

        AssertRejected(
            session,
            () => session.AddLineSupport(ExposedPrimarySupports[0]),
            ProductCommandError.PositionOccupied,
            "backup cannot reuse primary support");
        BuildActiveHospitalLineRequired(session, SafeBackupSupports, "restart backup");
        RequireAccepted(session.AdvanceToIncident(), "restart incident setup");

        ProductSnapshot expectedInitial = new ProductSession(_fixture).GetSnapshot();
        AssertAccepted(session, session.RestartMission(), "hospital restart");
        Equal(expectedInitial, session.GetSnapshot(), "hospital restart initial state");
        ProductSnapshot once = session.GetSnapshot();
        AssertAccepted(session, session.RestartMission(), "hospital restart again");
        Equal(once, session.GetSnapshot(), "hospital restart idempotence");
    }

    private ProductSession IncidentReadySession(
        ProductFixture fixture,
        IReadOnlyList<ProductPoint> primarySupports,
        IReadOnlyList<ProductPoint> backupSupports)
    {
        ProductSession session = PrimaryPlanningSession(fixture);
        BuildActiveHospitalLineRequired(session, primarySupports, "primary");
        BuildActiveHospitalLineRequired(session, backupSupports, "backup");
        Equal(ProductPhase.IncidentReady, session.GetSnapshot().Phase, "incident-ready setup");
        return session;
    }

    private ProductSession PrimaryPlanningSession(ProductFixture fixture)
    {
        ProductSession session = new(fixture);
        RequireAccepted(session.SetSubstationDraft(SuccessSubstation), "setup substation draft");
        RequireAccepted(session.OrderSubstation(), "setup substation order");
        RequireAccepted(
            session.AdvanceToConstructionCompletion(),
            "setup substation completion");
        AddSupportsRequired(session, TownSupports, "town");
        RequireAccepted(session.OrderLine(), "setup town line order");
        RequireAccepted(session.AdvanceToConstructionCompletion(), "setup town line completion");
        RequireAccepted(session.AdvanceToSettlement(), "setup first settlement");
        Equal(ProductPhase.PrimaryPlanning, session.GetSnapshot().Phase, "primary planning setup");
        return session;
    }

    private static void BuildActiveHospitalLineRequired(
        ProductSession session,
        IReadOnlyList<ProductPoint> supports,
        string label)
    {
        AddSupportsRequired(session, supports, label);
        ProductOrderPreview preview = session.PreviewLineOrder();
        if (!preview.Accepted)
        {
            throw new InvalidOperationException(
                $"{label} order preview was rejected with {preview.Error}.");
        }
        RequireAccepted(session.OrderLine(), $"{label} order");
        RequireAccepted(session.AdvanceToConstructionCompletion(), $"{label} completion");
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

    private static ProductHospital Hospital(ProductFixture fixture) =>
        fixture.Hospital
        ?? throw new InvalidOperationException("Second Heart fixture has no hospital.");

    private static IReadOnlyList<ProductHospitalLineProjectDefinition> HospitalLines(
        ProductFixture fixture) =>
        fixture.HospitalLineProjects
        ?? throw new InvalidOperationException("Second Heart fixture has no hospital lines.");

    private static ProductHospitalSnapshot Hospital(ProductSnapshot snapshot) =>
        snapshot.Hospital
        ?? throw new InvalidOperationException("Second Heart snapshot has no hospital state.");

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
