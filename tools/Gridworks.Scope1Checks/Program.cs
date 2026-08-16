using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Gridworks.Core;

namespace Gridworks.Scope1Checks;

internal static class Program
{
    private static readonly Scope1Point FirstWitness = new(5, 4);
    private static readonly Scope1Point SecondWitness = new(9, 4);

    private static int _assertionCount;

    public static int Main(string[] args)
    {
        try
        {
            string fixturePath = ResolveFixturePath(args);
            string fixtureJson = File.ReadAllText(fixturePath, Encoding.UTF8);
            Scope1Scenario scenario = Scope1FixtureLoader.Load(fixtureJson).Scenario;
            CheckContext context = new(fixtureJson, scenario);

            (string Name, Action<CheckContext> Run)[] suites =
            [
                ("strict loader negatives", CheckStrictLoaderNegatives),
                ("fixture geometry and checker witness", CheckFixtureGeometryAndWitness),
                ("oracle A completion sequence", CheckOracleA),
                ("oracle B undo sequence", CheckOracleB),
                ("error reachability and rejected invariance", CheckErrorsAndInvariance),
                ("PreviewSpan parity", CheckPreviewSpanParity),
                ("PreviewTarget parity", CheckPreviewTargetParity),
                ("preview purity and wrong phase", CheckPreviewPurityAndWrongPhase),
                ("atomic commissioning", CheckAtomicCommissioning),
                ("deterministic canonical JSON and hash", CheckDeterministicCanonicalJsonAndHash),
            ];

            List<string> failures = [];
            foreach ((string name, Action<CheckContext> run) in suites)
            {
                int assertionsBefore = _assertionCount;
                try
                {
                    run(context);
                    Console.WriteLine($"PASS {name} ({_assertionCount - assertionsBefore} assertions)");
                }
                catch (Exception exception)
                {
                    failures.Add($"{name}: {exception.Message}");
                    Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
                }
            }

            if (failures.Count > 0)
            {
                Console.Error.WriteLine(
                    $"Scope 1 checks failed: {failures.Count}/{suites.Length} suites; " +
                    $"{_assertionCount} assertions reached.");
                return 1;
            }

            Console.WriteLine(
                $"Scope 1 checks passed: {suites.Length}/{suites.Length} suites, " +
                $"{_assertionCount} assertions.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Scope 1 checks could not start: {exception.Message}");
            return 1;
        }
    }

    private static void CheckStrictLoaderNegatives(CheckContext context)
    {
        Scope1LoadedFixture loaded = Scope1FixtureLoader.Load(context.FixtureJson);
        Equal(context.Scenario, loaded.Scenario, "the authoritative fixture must load unchanged");
        Scope1LoadedFixture loadedBytes = Scope1FixtureLoader.Load(Encoding.UTF8.GetBytes(context.FixtureJson));
        Equal(context.Scenario, loadedBytes.Scenario, "the UTF-8 fixture must load unchanged");

        ExpectFixtureRejectedBytes([0xff, 0xfe, 0xfd], "invalid UTF-8 JSON");
        ExpectFixtureRejected(
            context.FixtureJson.Replace("\n}", ",\n}\n", StringComparison.Ordinal),
            "trailing comma");
        ExpectFixtureRejected(
            "// comment\n" + context.FixtureJson,
            "JSON comment");

        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root => root["unexpected"] = true),
            "unknown root field");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root => root.Remove("fixtureId")),
            "missing root field");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root => root["source"] = null),
            "null required object");
        ExpectFixtureRejected(
            InsertBeforeFinalBrace(
                context.FixtureJson,
                $",\n  \"maxSpan\": {context.Scenario.MaxSpan}\n"),
            "duplicate root field");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root =>
                RequiredObject(root, "source")["unexpected"] = 1),
            "unknown nested field");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root =>
                RequiredObject(root, "source").Remove("y")),
            "missing nested field");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root =>
                RequiredObject(root, "source")["x"] = 1.5m),
            "non-integer coordinate");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root => root["schemaVersion"] = "unsupported"),
            "unsupported schema");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root => root["fixtureId"] = "unsupported"),
            "unsupported fixture id");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root =>
                RequiredObject(root, "units")["position"] = "Meter"),
            "unsupported units");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root =>
                RequiredObject(root, "target")["x"] = context.Scenario.Source.X),
            "duplicate endpoints");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root =>
                root["maxSpan"] = SmallestCoveringIntegerSpan(
                    DistanceSquared(context.Scenario.Source, context.Scenario.Target))),
            "fixture that no longer requires an intermediate support");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root => root["maxSpan"] = 0),
            "non-positive maxSpan");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root => root["initialMinute"] = -1),
            "negative initialMinute");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root => root["buildMinutes"] = 0),
            "non-positive buildMinutes");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root =>
                RequiredObject(root, "mapBounds")["maxX"] = -1),
            "reversed bounds");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root =>
                RequiredObject(root, "source")["x"] = -1),
            "source outside bounds");
        ExpectFixtureRejected(
            Mutate(context.FixtureJson, root =>
            {
                root["initialMinute"] = int.MaxValue;
                root["buildMinutes"] = 1;
            }),
            "completion minute overflow");
    }

    private static void CheckFixtureGeometryAndWitness(CheckContext context)
    {
        Scope1Scenario scenario = context.Scenario;
        long maxSpanSquared = checked((long)scenario.MaxSpan * scenario.MaxSpan);
        long directDistanceSquared = DistanceSquared(scenario.Source, scenario.Target);
        Require(
            directDistanceSquared > maxSpanSquared,
            "the fixture's direct source-to-target span must fail");

        Scope1PlacementSession directSession = NewSession(scenario);
        string directBefore = Canonical(directSession);
        Scope1PreviewResult directPreview = directSession.PreviewTarget();
        AssertPreview(
            directPreview,
            accepted: false,
            Scope1ErrorCode.SpanTooLong,
            scenario.Source,
            scenario.Target,
            directDistanceSquared,
            maxSpanSquared,
            "direct target preview");
        Equal(directBefore, Canonical(directSession), "direct preview must be pure");
        AssertRejectedUnchanged(
            directSession.OrderLine(),
            Scope1ErrorCode.SpanTooLong,
            directBefore,
            directSession,
            "direct OrderLine");

        Scope1PlacementSession witnessSession = NewSession(scenario);
        AssertAccepted(witnessSession.AddSupport(FirstWitness), "first checker witness");
        AssertAccepted(witnessSession.AddSupport(SecondWitness), "second checker witness");
        Scope1PreviewResult witnessTarget = witnessSession.PreviewTarget();
        AssertPreview(
            witnessTarget,
            accepted: true,
            expectedError: null,
            SecondWitness,
            scenario.Target,
            DistanceSquared(SecondWitness, scenario.Target),
            maxSpanSquared,
            "checker witness target preview");
        AssertAccepted(witnessSession.OrderLine(), "checker witness OrderLine");

        Scope1Point positiveYBoundary = new(3, 7);
        Scope1Point positiveYTooLong = new(4, 7);
        Scope1PreviewResult positiveYAccepted = NewSession(scenario).PreviewSpan(positiveYBoundary);
        AssertPreview(
            positiveYAccepted,
            accepted: true,
            expectedError: null,
            scenario.Source,
            positiveYBoundary,
            expectedDistanceSquared: 13,
            maxSpanSquared,
            "+Y-axis valid preview");
        Scope1PreviewResult positiveYRejected = NewSession(scenario).PreviewSpan(positiveYTooLong);
        AssertPreview(
            positiveYRejected,
            accepted: false,
            Scope1ErrorCode.SpanTooLong,
            scenario.Source,
            positiveYTooLong,
            expectedDistanceSquared: 18,
            maxSpanSquared,
            "+Y-axis over-limit preview");
    }

    private static void CheckOracleA(CheckContext context)
    {
        Scope1Scenario scenario = context.Scenario;
        Scope1PlacementSession session = NewSession(scenario);
        int completionMinute = checked(scenario.InitialMinute + scenario.BuildMinutes);

        AssertView(
            session.GetView(),
            scenario.InitialMinute,
            Scope1Phase.Drafting,
            [],
            completionMinute: null,
            targetEnergized: false,
            "A0 initial");

        string before = Canonical(session);
        AssertRejectedUnchanged(
            session.OrderLine(), Scope1ErrorCode.SpanTooLong, before, session, "A1 direct order");

        before = Canonical(session);
        AssertRejectedUnchanged(
            session.AddSupport(new Scope1Point(6, 4)),
            Scope1ErrorCode.SpanTooLong,
            before,
            session,
            "A2 over-limit support");

        Scope1CommandResult first = session.AddSupport(FirstWitness);
        AssertAccepted(first, "A3 first support");
        AssertView(
            first.View,
            scenario.InitialMinute,
            Scope1Phase.Drafting,
            [FirstWitness],
            completionMinute: null,
            targetEnergized: false,
            "A3 view");

        before = Canonical(session);
        AssertRejectedUnchanged(
            session.OrderLine(), Scope1ErrorCode.SpanTooLong, before, session, "A4 early order");

        Scope1CommandResult second = session.AddSupport(SecondWitness);
        AssertAccepted(second, "A5 second support");
        AssertView(
            second.View,
            scenario.InitialMinute,
            Scope1Phase.Drafting,
            [FirstWitness, SecondWitness],
            completionMinute: null,
            targetEnergized: false,
            "A5 view");

        Scope1CommandResult order = session.OrderLine();
        AssertAccepted(order, "A6 order");
        AssertView(
            order.View,
            scenario.InitialMinute,
            Scope1Phase.Building,
            [FirstWitness, SecondWitness],
            completionMinute,
            targetEnergized: false,
            "A6 building view");

        Scope1CommandResult completion = session.AdvanceToCompletion();
        AssertAccepted(completion, "A7 completion");
        AssertView(
            completion.View,
            completionMinute,
            Scope1Phase.Commissioned,
            [FirstWitness, SecondWitness],
            completionMinute,
            targetEnergized: true,
            "A7 commissioned view");
    }

    private static void CheckOracleB(CheckContext context)
    {
        Scope1Scenario scenario = context.Scenario;

        Scope1PlacementSession drafting = NewSession(scenario);
        AssertAccepted(drafting.AddSupport(FirstWitness), "B1 first support");
        AssertAccepted(drafting.AddSupport(SecondWitness), "B1 second support");
        Scope1CommandResult undo = drafting.UndoSupport();
        AssertAccepted(undo, "B1 undo");
        AssertView(
            undo.View,
            scenario.InitialMinute,
            Scope1Phase.Drafting,
            [FirstWitness],
            completionMinute: null,
            targetEnergized: false,
            "B1 view");

        Scope1PlacementSession building = BuildSession(scenario);
        string before = Canonical(building);
        AssertRejectedUnchanged(
            building.UndoSupport(),
            Scope1ErrorCode.WrongPhase,
            before,
            building,
            "B2 building undo");

        Scope1PlacementSession commissioned = CommissionedSession(scenario);
        before = Canonical(commissioned);
        AssertRejectedUnchanged(
            commissioned.UndoSupport(),
            Scope1ErrorCode.WrongPhase,
            before,
            commissioned,
            "B3 commissioned undo");
    }

    private static void CheckErrorsAndInvariance(CheckContext context)
    {
        Scope1Scenario scenario = context.Scenario;
        Scope1PlacementSession session = NewSession(scenario);

        string before = Canonical(session);
        AssertRejectedUnchanged(
            session.UndoSupport(),
            Scope1ErrorCode.NothingToUndo,
            before,
            session,
            "empty UndoSupport");

        before = Canonical(session);
        AssertRejectedUnchanged(
            session.AddSupport(new Scope1Point(6, 4)),
            Scope1ErrorCode.SpanTooLong,
            before,
            session,
            "over-limit AddSupport");

        Scope1Point outsideAndFar = new(
            checked(scenario.MapBounds.MaxX + 1),
            checked(scenario.MapBounds.MaxY + 1));
        before = Canonical(session);
        AssertRejectedUnchanged(
            session.AddSupport(outsideAndFar),
            Scope1ErrorCode.InvalidPosition,
            before,
            session,
            "invalid position must precede span length");

        AssertAccepted(session.AddSupport(FirstWitness), "duplicate setup support");
        before = Canonical(session);
        AssertRejectedUnchanged(
            session.AddSupport(FirstWitness),
            Scope1ErrorCode.InvalidPosition,
            before,
            session,
            "duplicate support");

        Scope1PlacementSession building = BuildSession(scenario);
        before = Canonical(building);
        AssertRejectedUnchanged(
            building.AddSupport(outsideAndFar),
            Scope1ErrorCode.WrongPhase,
            before,
            building,
            "wrong phase must precede invalid position and span length");

        Scope1PlacementSession draftingAdvance = NewSession(scenario);
        before = Canonical(draftingAdvance);
        AssertRejectedUnchanged(
            draftingAdvance.AdvanceToCompletion(),
            Scope1ErrorCode.WrongPhase,
            before,
            draftingAdvance,
            "drafting AdvanceToCompletion");
    }

    private static void CheckPreviewSpanParity(CheckContext context)
    {
        Scope1Scenario scenario = context.Scenario;

        AssertSpanParity(
            () => NewSession(scenario), FirstWitness, expectedError: null, "boundary support");
        AssertSpanParity(
            () => SessionWithFirstSupport(scenario),
            SecondWitness,
            expectedError: null,
            "boundary support after existing support");
        AssertSpanParity(
            () => NewSession(scenario),
            new Scope1Point(6, 4),
            Scope1ErrorCode.SpanTooLong,
            "over-limit support");
        AssertSpanParity(
            () => NewSession(scenario),
            scenario.Source,
            Scope1ErrorCode.InvalidPosition,
            "invalid endpoint position");
        AssertSpanParity(
            () => BuildSession(scenario),
            new Scope1Point(6, 4),
            Scope1ErrorCode.WrongPhase,
            "building phase");
    }

    private static void CheckPreviewTargetParity(CheckContext context)
    {
        Scope1Scenario scenario = context.Scenario;

        AssertTargetParity(
            () => NewSession(scenario), Scope1ErrorCode.SpanTooLong, "direct target");
        AssertTargetParity(
            () => SessionWithFirstSupport(scenario),
            Scope1ErrorCode.SpanTooLong,
            "target after one support");
        AssertTargetParity(
            () => SessionWithWitnesses(scenario), expectedError: null, "target after witness path");
        AssertTargetParity(
            () => BuildSession(scenario), Scope1ErrorCode.WrongPhase, "building phase target");
    }

    private static void CheckPreviewPurityAndWrongPhase(CheckContext context)
    {
        Scope1Scenario scenario = context.Scenario;
        Scope1PlacementSession drafting = NewSession(scenario);
        string before = Canonical(drafting);

        Scope1PreviewResult firstSpan = drafting.PreviewSpan(FirstWitness);
        Scope1PreviewResult secondSpan = drafting.PreviewSpan(FirstWitness);
        Equal(firstSpan, secondSpan, "repeated PreviewSpan calls must be deterministic");
        Equal(before, Canonical(drafting), "drafting PreviewSpan must be pure");

        Scope1PreviewResult firstTarget = drafting.PreviewTarget();
        Scope1PreviewResult secondTarget = drafting.PreviewTarget();
        Equal(firstTarget, secondTarget, "repeated PreviewTarget calls must be deterministic");
        Equal(before, Canonical(drafting), "drafting PreviewTarget must be pure");

        foreach (Scope1PlacementSession session in
                 new[] { BuildSession(scenario), CommissionedSession(scenario) })
        {
            before = Canonical(session);
            Scope1PreviewResult span = session.PreviewSpan(scenario.Source);
            Equal(false, span.Accepted, $"{session.GetView().Phase} PreviewSpan accepted");
            Equal(
                Scope1ErrorCode.WrongPhase,
                span.ErrorCode,
                $"{session.GetView().Phase} PreviewSpan error");
            Equal(before, Canonical(session), $"{session.GetView().Phase} PreviewSpan purity");

            Scope1PreviewResult target = session.PreviewTarget();
            Equal(false, target.Accepted, $"{session.GetView().Phase} PreviewTarget accepted");
            Equal(
                Scope1ErrorCode.WrongPhase,
                target.ErrorCode,
                $"{session.GetView().Phase} PreviewTarget error");
            Equal(before, Canonical(session), $"{session.GetView().Phase} PreviewTarget purity");
        }
    }

    private static void CheckAtomicCommissioning(CheckContext context)
    {
        Scope1Scenario scenario = context.Scenario;
        Scope1PlacementSession session = NewSession(scenario);
        int completionMinute = checked(scenario.InitialMinute + scenario.BuildMinutes);

        Equal(false, session.GetView().TargetEnergized, "initial target energy");
        AssertAccepted(session.AddSupport(FirstWitness), "atomic first support");
        Equal(false, session.GetView().TargetEnergized, "target after first support");
        AssertAccepted(session.AddSupport(SecondWitness), "atomic second support");
        Equal(false, session.GetView().TargetEnergized, "target after second support");

        Scope1CommandResult order = session.OrderLine();
        AssertAccepted(order, "atomic order");
        Equal(Scope1Phase.Building, order.View.Phase, "phase immediately after order");
        Equal(scenario.InitialMinute, order.View.Minute, "minute immediately after order");
        Equal(completionMinute, order.View.CompletionMinute, "scheduled completion minute");
        Equal(false, order.View.TargetEnergized, "building target must remain de-energized");
        Equal(
            new[] { FirstWitness, SecondWitness },
            order.View.SupportPositions,
            "building supports");

        Scope1CommandResult complete = session.AdvanceToCompletion();
        AssertAccepted(complete, "atomic completion");
        Equal(Scope1Phase.Commissioned, complete.View.Phase, "phase at completion");
        Equal(completionMinute, complete.View.Minute, "minute at completion");
        Equal(completionMinute, complete.View.CompletionMinute, "retained completion minute");
        Equal(true, complete.View.TargetEnergized, "target energy switches on at completion");
        Equal(
            new[] { FirstWitness, SecondWitness },
            complete.View.SupportPositions,
            "commissioned supports");

        string before = Canonical(session);
        AssertRejectedUnchanged(
            session.AdvanceToCompletion(),
            Scope1ErrorCode.WrongPhase,
            before,
            session,
            "second completion attempt");
    }

    private static void CheckDeterministicCanonicalJsonAndHash(CheckContext context)
    {
        Scope1Scenario scenario = context.Scenario;
        Scope1PlacementSession first = CommissionedSession(scenario);
        Scope1PlacementSession second = CommissionedSession(scenario);
        Scope1View firstView = first.GetView();
        Scope1View secondView = second.GetView();
        int completionMinute = checked(scenario.InitialMinute + scenario.BuildMinutes);

        string expected =
            $"{{\"minute\":{completionMinute},\"phase\":\"commissioned\"," +
            $"\"supportPositions\":[{{\"x\":{FirstWitness.X},\"y\":{FirstWitness.Y}}}," +
            $"{{\"x\":{SecondWitness.X},\"y\":{SecondWitness.Y}}}]," +
            $"\"completionMinute\":{completionMinute},\"targetEnergized\":true}}";
        string firstJson = Scope1ViewJson.Serialize(firstView);
        string secondJson = Scope1ViewJson.Serialize(secondView);
        Equal(expected, firstJson, "canonical field order and values");
        Equal(firstJson, secondJson, "identical command sequences must serialize identically");
        Equal(
            Encoding.UTF8.GetBytes(expected),
            Scope1ViewJson.SerializeToUtf8Bytes(firstView),
            "canonical UTF-8 bytes");

        string independentlyHashed = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(expected))).ToLowerInvariant();
        string firstHash = Scope1ViewJson.Sha256Hex(firstView);
        string secondHash = Scope1ViewJson.Sha256Hex(secondView);
        Equal(independentlyHashed, firstHash, "snapshot hash must match independent SHA-256");
        Equal(firstHash, secondHash, "identical command sequences must hash identically");
        Equal(64, firstHash.Length, "SHA-256 hex length");
        Require(
            firstHash.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            "SHA-256 must use lowercase hexadecimal");

        string before = Scope1ViewJson.Serialize(firstView);
        string beforeHash = Scope1ViewJson.Sha256Hex(firstView);
        Scope1CommandResult rejected = first.UndoSupport();
        Equal(false, rejected.Accepted, "determinism rejected command accepted");
        Equal(Scope1ErrorCode.WrongPhase, rejected.ErrorCode, "determinism rejected error");
        Equal(before, Canonical(first), "rejected command canonical JSON invariance");
        Equal(beforeHash, Scope1ViewJson.Sha256Hex(first.GetView()), "rejected command hash invariance");
    }

    private static void AssertSpanParity(
        Func<Scope1PlacementSession> sessionFactory,
        Scope1Point position,
        Scope1ErrorCode? expectedError,
        string label)
    {
        Scope1PlacementSession previewSession = sessionFactory();
        Scope1View previewView = previewSession.GetView();
        Scope1Point expectedFrom = previewView.SupportPositions.Count == 0
            ? InferSourceFromFreshFactory(sessionFactory)
            : previewView.SupportPositions[^1];
        string previewBefore = Canonical(previewSession);
        Scope1PreviewResult preview = previewSession.PreviewSpan(position);

        Equal(expectedError is null, preview.Accepted, $"{label} preview accepted");
        Equal(expectedError, preview.ErrorCode, $"{label} preview error");
        Equal(expectedFrom, preview.From, $"{label} preview from");
        Equal(position, preview.To, $"{label} preview to");
        Equal(
            DistanceSquared(preview.From, preview.To),
            preview.DistanceSquared,
            $"{label} preview squared distance");
        Require(preview.MaxSpanSquared > 0, $"{label} positive max-span square");
        Equal(previewBefore, Canonical(previewSession), $"{label} preview purity");

        Scope1PlacementSession commandSession = sessionFactory();
        Scope1CommandResult command = commandSession.AddSupport(position);
        Equal(preview.Accepted, command.Accepted, $"{label} accepted parity");
        Equal(preview.ErrorCode, command.ErrorCode, $"{label} error parity");
    }

    private static void AssertTargetParity(
        Func<Scope1PlacementSession> sessionFactory,
        Scope1ErrorCode? expectedError,
        string label)
    {
        Scope1PlacementSession previewSession = sessionFactory();
        string previewBefore = Canonical(previewSession);
        Scope1PreviewResult preview = previewSession.PreviewTarget();
        Equal(expectedError is null, preview.Accepted, $"{label} preview accepted");
        Equal(expectedError, preview.ErrorCode, $"{label} preview error");
        Equal(
            DistanceSquared(preview.From, preview.To),
            preview.DistanceSquared,
            $"{label} preview squared distance");
        Require(preview.MaxSpanSquared > 0, $"{label} positive max-span square");
        Equal(previewBefore, Canonical(previewSession), $"{label} preview purity");

        Scope1PlacementSession commandSession = sessionFactory();
        Scope1CommandResult command = commandSession.OrderLine();
        Equal(preview.Accepted, command.Accepted, $"{label} accepted parity");
        Equal(preview.ErrorCode, command.ErrorCode, $"{label} error parity");
    }

    private static Scope1Point InferSourceFromFreshFactory(
        Func<Scope1PlacementSession> sessionFactory)
    {
        Scope1PreviewResult target = sessionFactory().PreviewTarget();
        return target.From;
    }

    private static void AssertPreview(
        Scope1PreviewResult actual,
        bool accepted,
        Scope1ErrorCode? expectedError,
        Scope1Point expectedFrom,
        Scope1Point expectedTo,
        long expectedDistanceSquared,
        long maxSpanSquared,
        string label)
    {
        Equal(accepted, actual.Accepted, $"{label} accepted");
        Equal(expectedError, actual.ErrorCode, $"{label} error");
        Equal(expectedFrom, actual.From, $"{label} from");
        Equal(expectedTo, actual.To, $"{label} to");
        Equal(expectedDistanceSquared, actual.DistanceSquared, $"{label} squared distance");
        Equal(maxSpanSquared, actual.MaxSpanSquared, $"{label} max-span square");
    }

    private static void AssertView(
        Scope1View actual,
        int minute,
        Scope1Phase phase,
        IReadOnlyList<Scope1Point> supports,
        int? completionMinute,
        bool targetEnergized,
        string label)
    {
        Equal(minute, actual.Minute, $"{label} minute");
        Equal(phase, actual.Phase, $"{label} phase");
        Equal(supports, actual.SupportPositions, $"{label} supports");
        Equal(completionMinute, actual.CompletionMinute, $"{label} completion minute");
        Equal(targetEnergized, actual.TargetEnergized, $"{label} target energy");
    }

    private static void AssertAccepted(Scope1CommandResult result, string label)
    {
        Equal(true, result.Accepted, $"{label} accepted");
        Equal<Scope1ErrorCode?>(null, result.ErrorCode, $"{label} error");
    }

    private static void AssertRejectedUnchanged(
        Scope1CommandResult result,
        Scope1ErrorCode expectedError,
        string before,
        Scope1PlacementSession session,
        string label)
    {
        Equal(false, result.Accepted, $"{label} accepted");
        Equal(expectedError, result.ErrorCode, $"{label} error");
        Equal(before, Scope1ViewJson.Serialize(result.View), $"{label} result view invariance");
        Equal(before, Canonical(session), $"{label} session invariance");
    }

    private static Scope1PlacementSession NewSession(Scope1Scenario scenario) => new(scenario);

    private static Scope1PlacementSession SessionWithFirstSupport(Scope1Scenario scenario)
    {
        Scope1PlacementSession session = NewSession(scenario);
        EnsureSetupAccepted(session.AddSupport(FirstWitness), "first witness setup");
        return session;
    }

    private static Scope1PlacementSession SessionWithWitnesses(Scope1Scenario scenario)
    {
        Scope1PlacementSession session = SessionWithFirstSupport(scenario);
        EnsureSetupAccepted(session.AddSupport(SecondWitness), "second witness setup");
        return session;
    }

    private static Scope1PlacementSession BuildSession(Scope1Scenario scenario)
    {
        Scope1PlacementSession session = SessionWithWitnesses(scenario);
        EnsureSetupAccepted(session.OrderLine(), "order setup");
        return session;
    }

    private static Scope1PlacementSession CommissionedSession(Scope1Scenario scenario)
    {
        Scope1PlacementSession session = BuildSession(scenario);
        EnsureSetupAccepted(session.AdvanceToCompletion(), "completion setup");
        return session;
    }

    private static void EnsureSetupAccepted(Scope1CommandResult result, string label)
    {
        if (!result.Accepted || result.ErrorCode is not null)
        {
            throw new CheckFailureException($"{label} unexpectedly failed with {result.ErrorCode}.");
        }
    }

    private static string Canonical(Scope1PlacementSession session) =>
        Scope1ViewJson.Serialize(session.GetView());

    private static long DistanceSquared(Scope1Point from, Scope1Point to)
    {
        long dx = checked((long)to.X - from.X);
        long dy = checked((long)to.Y - from.Y);
        return checked(checked(dx * dx) + checked(dy * dy));
    }

    private static int SmallestCoveringIntegerSpan(long distanceSquared)
    {
        int span = 1;
        while (checked((long)span * span) < distanceSquared)
        {
            span = checked(span + 1);
        }
        return span;
    }

    private static void ExpectFixtureRejected(string json, string label)
    {
        _assertionCount++;
        try
        {
            _ = Scope1FixtureLoader.Load(json);
        }
        catch (FixtureValidationException)
        {
            return;
        }

        throw new CheckFailureException($"{label} was accepted by the strict fixture loader.");
    }

    private static void ExpectFixtureRejectedBytes(byte[] bytes, string label)
    {
        _assertionCount++;
        try
        {
            _ = Scope1FixtureLoader.Load(bytes);
        }
        catch (FixtureValidationException)
        {
            return;
        }

        throw new CheckFailureException($"{label} was accepted by the strict fixture loader.");
    }

    private static string Mutate(string json, Action<JsonObject> mutation)
    {
        JsonObject root = JsonNode.Parse(json)?.AsObject()
            ?? throw new CheckFailureException("authoritative fixture root is not an object");
        mutation(root);
        return root.ToJsonString();
    }

    private static JsonObject RequiredObject(JsonObject parent, string propertyName) =>
        parent[propertyName]?.AsObject()
        ?? throw new CheckFailureException($"fixture property '{propertyName}' is not an object");

    private static string InsertBeforeFinalBrace(string json, string insertion)
    {
        int finalBrace = json.LastIndexOf('}');
        if (finalBrace < 0)
        {
            throw new CheckFailureException("authoritative fixture has no closing root brace");
        }
        return json.Insert(finalBrace, insertion);
    }

    private static string ResolveFixturePath(string[] args)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException(
                "Usage: Gridworks.Scope1Checks [fixture-path]; default is data/scope-1-v1.json.");
        }

        string path = args.Length == 1
            ? args[0]
            : Path.Combine("data", "scope-1-v1.json");
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Scope 1 fixture was not found.", fullPath);
        }
        return fullPath;
    }

    private static void Require(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new CheckFailureException(message);
        }
    }

    private static void Equal<T>(T expected, T actual, string label)
    {
        _assertionCount++;
        if (expected is IEnumerable<Scope1Point> expectedPoints &&
            actual is IEnumerable<Scope1Point> actualPoints)
        {
            if (!expectedPoints.SequenceEqual(actualPoints))
            {
                throw new CheckFailureException(
                    $"{label}: expected [{string.Join(", ", expectedPoints)}], " +
                    $"got [{string.Join(", ", actualPoints)}].");
            }
            return;
        }

        if (expected is byte[] expectedBytes && actual is byte[] actualBytes)
        {
            if (!expectedBytes.AsSpan().SequenceEqual(actualBytes))
            {
                throw new CheckFailureException($"{label}: byte sequences differ.");
            }
            return;
        }

        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new CheckFailureException($"{label}: expected '{expected}', got '{actual}'.");
        }
    }

    private sealed record CheckContext(string FixtureJson, Scope1Scenario Scenario);

    private sealed class CheckFailureException(string message) : Exception(message);
}
