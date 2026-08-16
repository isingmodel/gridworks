using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gridworks.Core;

namespace Gridworks.Scope1Checks;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            string fixturePath = ResolveFixturePath(args);
            return new Scope1Checks(fixturePath).Run();
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
            throw new ArgumentException("usage: Gridworks.Scope1Checks [fixture-json]");
        }

        string path = args.Length == 1
            ? args[0]
            : Path.Combine(Environment.CurrentDirectory, "data", "scope-1-v1.json");
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Scope 1 fixture not found.", path);
        }

        return path;
    }
}

internal sealed class Scope1Checks
{
    // These two positions are a checker-only witness. They must not move into the fixture or Core defaults.
    private static readonly Scope1Point FirstWitness = new(5, 4);
    private static readonly Scope1Point SecondWitness = new(9, 4);

    private static readonly string[] ViewPropertyOrder =
    [
        "minute",
        "phase",
        "supportPositions",
        "completionMinute",
        "targetEnergized",
    ];

    private readonly string _fixtureJson;
    private readonly Scope1Fixture _fixture;
    private int _assertionCount;

    public Scope1Checks(string fixturePath)
    {
        _fixtureJson = File.ReadAllText(fixturePath, Encoding.UTF8);
        _fixture = Scope1FixtureLoader.Load(_fixtureJson);
    }

    public int Run()
    {
        (string Name, Action Body)[] suites =
        [
            ("strict-loader", CheckStrictLoader),
            ("contract-a-completion", CheckCompletionSequence),
            ("contract-b-undo", CheckUndoSequence),
            ("error-precedence-rejected-invariance", CheckRejectedCommands),
            ("preview-parity-purity", CheckPreviewParityAndPurity),
            ("distance-boundary-overflow", CheckDistanceBoundaryAndOverflow),
            ("atomic-completion-defensive-view", CheckAtomicCompletionAndDefensiveViews),
            ("deterministic-json-hash", CheckDeterministicJsonAndHash),
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
            Console.Error.WriteLine($"Gridworks Scope 1 checks: FAIL ({failures.Count}/{suites.Length} suites)");
            return 1;
        }

        Console.WriteLine($"Gridworks Scope 1 checks: PASS ({suites.Length} suites, {_assertionCount} assertions)");
        return 0;
    }

    private void CheckStrictLoader()
    {
        Scope1Fixture textFixture = Scope1FixtureLoader.Load(_fixtureJson);
        Scope1Fixture byteFixture = Scope1FixtureLoader.Load(Encoding.UTF8.GetBytes(_fixtureJson));
        Equal(_fixture, textFixture, "text load changed the fixture");
        Equal(_fixture, byteFixture, "UTF-8 load changed the fixture");

        Equal("1", _fixture.SchemaVersion, "schemaVersion");
        Equal("scope-1-v1", _fixture.FixtureId, "fixtureId");
        Equal("GridUnit", _fixture.Units.Position, "position unit");
        Equal("GameMinute", _fixture.Units.Time, "time unit");
        Equal(new Scope1Point(1, 4), _fixture.Source, "source");
        Equal(new Scope1Point(11, 4), _fixture.Target, "target");
        Equal(4, _fixture.MaxSpan, "maxSpan");
        Equal(0, _fixture.InitialMinute, "initialMinute");
        Equal(60, _fixture.BuildMinutes, "buildMinutes");

        string trimmed = _fixtureJson.TrimStart();
        ExpectFixtureRejected(
            "duplicate root property",
            "{\"schemaVersion\":\"1\"," + trimmed[1..]);
        ExpectFixtureRejected(
            "duplicate nested property",
            ReplaceRequired(
                _fixtureJson,
                "\"position\": \"GridUnit\",",
                "\"position\": \"GridUnit\",\n    \"position\": \"GridUnit\","));
        ExpectFixtureRejected("malformed JSON", "{\"schemaVersion\":");
        ExpectFixtureRejected("trailing JSON value", _fixtureJson + "{}");
        ExpectFixtureRejected("non-object root", "[]");
        ExpectFixtureRejectedBytes("invalid UTF-8", [0xff, 0xfe, 0xfd]);

        ExpectFixtureRejected("unknown root field", root => root["unexpected"] = true);
        ExpectFixtureRejected(
            "unknown nested field",
            root => Object(root, "mapBounds")["width"] = 12);
        ExpectFixtureRejected("wrongly cased field", root => root["SchemaVersion"] = "1");
        ExpectFixtureRejected("missing root field", root => root.Remove("maxSpan"));
        ExpectFixtureRejected("missing nested field", root => Object(root, "source").Remove("x"));
        ExpectFixtureRejected("null required object", root => root["units"] = null);
        ExpectFixtureRejected("null required scalar", root => root["fixtureId"] = null);
        ExpectFixtureRejected("string where integer is required", root => root["maxSpan"] = "4");
        ExpectFixtureRejected("fractional integer", root => root["maxSpan"] = 4.5);
        ExpectFixtureRejected(
            "exponent integer token",
            ReplaceRequired(_fixtureJson, "\"maxSpan\": 4", "\"maxSpan\": 4e0"));
        ExpectFixtureRejected("integer outside Int32", root => root["maxSpan"] = (long)int.MaxValue + 1);

        ExpectFixtureRejected("wrong schema version", root => root["schemaVersion"] = "2");
        ExpectFixtureRejected("wrong fixture ID", root => root["fixtureId"] = "scope-1-copy");
        ExpectFixtureRejected("wrong position unit", root => Object(root, "units")["position"] = "Pixel");
        ExpectFixtureRejected("wrong time unit", root => Object(root, "units")["time"] = "Second");

        ExpectFixtureRejected(
            "reversed horizontal bounds",
            root => Object(root, "mapBounds")["minX"] = 12);
        ExpectFixtureRejected(
            "reversed vertical bounds",
            root => Object(root, "mapBounds")["minY"] = 8);
        ExpectFixtureRejected(
            "source outside bounds",
            root => Object(root, "source")["x"] = -1);
        ExpectFixtureRejected(
            "target outside bounds",
            root => Object(root, "target")["x"] = 12);
        ExpectFixtureRejected(
            "identical endpoints",
            root => root["target"] = Object(root, "source").DeepClone());
        ExpectFixtureRejected("zero maxSpan", root => root["maxSpan"] = 0);
        ExpectFixtureRejected("negative maxSpan", root => root["maxSpan"] = -1);
        ExpectFixtureRejected("negative initial minute", root => root["initialMinute"] = -1);
        ExpectFixtureRejected("zero build minutes", root => root["buildMinutes"] = 0);
        ExpectFixtureRejected("negative build minutes", root => root["buildMinutes"] = -1);
        ExpectFixtureRejected(
            "completion minute overflow",
            root =>
            {
                root["initialMinute"] = int.MaxValue;
                root["buildMinutes"] = 1;
            });
        ExpectFixtureRejected(
            "map diagonal arithmetic overflow",
            root =>
            {
                JsonObject bounds = Object(root, "mapBounds");
                bounds["minX"] = int.MinValue;
                bounds["maxX"] = int.MaxValue;
                bounds["minY"] = int.MinValue;
                bounds["maxY"] = int.MaxValue;
            });
        ExpectFixtureRejected(
            "direct source-target span must fail",
            root =>
            {
                JsonObject target = Object(root, "target");
                target["x"] = FirstWitness.X;
                target["y"] = FirstWitness.Y;
            });
    }

    private void CheckCompletionSequence()
    {
        Scope1PlacementSession session = NewSession();
        AssertView(
            session.GetView(),
            _fixture.InitialMinute,
            Scope1Phase.Drafting,
            [],
            null,
            targetEnergized: false,
            "A/initial");

        AssertRejected(session, session.OrderLine, Scope1ErrorCode.SpanTooLong, "A/direct-order");
        AssertRejected(
            session,
            () => session.AddSupport(new Scope1Point(6, 4)),
            Scope1ErrorCode.SpanTooLong,
            "A/first-too-long");

        Scope1CommandResult first = session.AddSupport(FirstWitness);
        AssertAccepted(session, first, "A/first-support");
        AssertView(
            first.View,
            0,
            Scope1Phase.Drafting,
            [FirstWitness],
            null,
            targetEnergized: false,
            "A/after-first");

        AssertRejected(session, session.OrderLine, Scope1ErrorCode.SpanTooLong, "A/one-support-order");

        Scope1CommandResult second = session.AddSupport(SecondWitness);
        AssertAccepted(session, second, "A/second-support");
        AssertView(
            second.View,
            0,
            Scope1Phase.Drafting,
            [FirstWitness, SecondWitness],
            null,
            targetEnergized: false,
            "A/after-second");

        Scope1CommandResult ordered = session.OrderLine();
        AssertAccepted(session, ordered, "A/order");
        AssertView(
            ordered.View,
            0,
            Scope1Phase.Building,
            [FirstWitness, SecondWitness],
            60,
            targetEnergized: false,
            "A/building");

        Scope1CommandResult completed = session.AdvanceToCompletion();
        AssertAccepted(session, completed, "A/complete");
        AssertView(
            completed.View,
            60,
            Scope1Phase.Commissioned,
            [FirstWitness, SecondWitness],
            60,
            targetEnergized: true,
            "A/commissioned");
    }

    private void CheckUndoSequence()
    {
        Scope1PlacementSession drafting = SessionWithSupports(2);
        Scope1CommandResult undone = drafting.UndoSupport();
        AssertAccepted(drafting, undone, "B/drafting-undo");
        AssertView(
            undone.View,
            0,
            Scope1Phase.Drafting,
            [FirstWitness],
            null,
            targetEnergized: false,
            "B/after-undo");

        Scope1PlacementSession building = OrderedSession();
        AssertRejected(building, building.UndoSupport, Scope1ErrorCode.WrongPhase, "B/building-undo");

        Scope1PlacementSession commissioned = CommissionedSession();
        AssertRejected(
            commissioned,
            commissioned.UndoSupport,
            Scope1ErrorCode.WrongPhase,
            "B/commissioned-undo");

        Scope1PlacementSession firstRun = CommissionedSession();
        Scope1PlacementSession secondRun = CommissionedSession();
        Equal(
            Scope1ViewJson.Serialize(firstRun.GetView()),
            Scope1ViewJson.Serialize(secondRun.GetView()),
            "B/identical command sequences diverged");
    }

    private void CheckRejectedCommands()
    {
        Scope1PlacementSession initial = NewSession();
        AssertRejected(initial, initial.UndoSupport, Scope1ErrorCode.NothingToUndo, "initial/empty-undo");
        AssertRejected(
            initial,
            () => initial.AddSupport(_fixture.Source),
            Scope1ErrorCode.InvalidPosition,
            "initial/source-position");
        AssertRejected(
            initial,
            () => initial.AddSupport(_fixture.Target),
            Scope1ErrorCode.InvalidPosition,
            "initial/target-position-before-distance");
        AssertRejected(
            initial,
            () => initial.AddSupport(new Scope1Point(_fixture.MapBounds.MaxX + 1, _fixture.Source.Y)),
            Scope1ErrorCode.InvalidPosition,
            "initial/outside-before-distance");
        AssertRejected(
            initial,
            () => initial.AddSupport(new Scope1Point(6, 4)),
            Scope1ErrorCode.SpanTooLong,
            "initial/span-too-long");
        AssertRejected(initial, initial.OrderLine, Scope1ErrorCode.SpanTooLong, "initial/order-too-long");
        AssertRejected(
            initial,
            initial.AdvanceToCompletion,
            Scope1ErrorCode.WrongPhase,
            "initial/advance-wrong-phase");

        Scope1PlacementSession duplicate = SessionWithSupports(1);
        AssertRejected(
            duplicate,
            () => duplicate.AddSupport(FirstWitness),
            Scope1ErrorCode.InvalidPosition,
            "drafting/duplicate-position");

        Scope1PlacementSession building = OrderedSession();
        AssertRejected(
            building,
            () => building.AddSupport(new Scope1Point(int.MaxValue, int.MinValue)),
            Scope1ErrorCode.WrongPhase,
            "building/add-precedence");
        AssertRejected(building, building.UndoSupport, Scope1ErrorCode.WrongPhase, "building/undo");
        AssertRejected(building, building.OrderLine, Scope1ErrorCode.WrongPhase, "building/order");

        Scope1PlacementSession commissioned = CommissionedSession();
        AssertRejected(
            commissioned,
            () => commissioned.AddSupport(_fixture.Source),
            Scope1ErrorCode.WrongPhase,
            "commissioned/add-precedence");
        AssertRejected(
            commissioned,
            commissioned.UndoSupport,
            Scope1ErrorCode.WrongPhase,
            "commissioned/undo");
        AssertRejected(
            commissioned,
            commissioned.OrderLine,
            Scope1ErrorCode.WrongPhase,
            "commissioned/order");
        AssertRejected(
            commissioned,
            commissioned.AdvanceToCompletion,
            Scope1ErrorCode.WrongPhase,
            "commissioned/advance");
    }

    private void CheckPreviewParityAndPurity()
    {
        AssertSpanPreviewParity(
            NewSession,
            FirstWitness,
            expectedErrorCode: null,
            expectedFrom: _fixture.Source,
            expectedDistanceSquared: 16,
            "span/boundary");
        AssertSpanPreviewParity(
            NewSession,
            new Scope1Point(6, 4),
            Scope1ErrorCode.SpanTooLong,
            _fixture.Source,
            25,
            "span/too-long");
        AssertSpanPreviewParity(
            NewSession,
            _fixture.Target,
            Scope1ErrorCode.InvalidPosition,
            _fixture.Source,
            100,
            "span/invalid-target-precedence");
        AssertSpanPreviewParity(
            () => SessionWithSupports(1),
            SecondWitness,
            expectedErrorCode: null,
            FirstWitness,
            16,
            "span/second-boundary");
        AssertSpanPreviewParity(
            () => SessionWithSupports(1),
            FirstWitness,
            Scope1ErrorCode.InvalidPosition,
            FirstWitness,
            0,
            "span/duplicate");
        AssertSpanPreviewParity(
            OrderedSession,
            new Scope1Point(int.MaxValue, int.MinValue),
            Scope1ErrorCode.WrongPhase,
            SecondWitness,
            expectedDistanceSquared: null,
            "span/wrong-phase-precedence");

        AssertTargetPreviewParity(
            NewSession,
            Scope1ErrorCode.SpanTooLong,
            _fixture.Source,
            100,
            "target/direct");
        AssertTargetPreviewParity(
            () => SessionWithSupports(1),
            Scope1ErrorCode.SpanTooLong,
            FirstWitness,
            36,
            "target/one-support");
        AssertTargetPreviewParity(
            () => SessionWithSupports(2),
            expectedErrorCode: null,
            SecondWitness,
            4,
            "target/ready");
        AssertTargetPreviewParity(
            OrderedSession,
            Scope1ErrorCode.WrongPhase,
            SecondWitness,
            4,
            "target/wrong-phase");
    }

    private void CheckDistanceBoundaryAndOverflow()
    {
        Scope1PlacementSession exact = NewSession();
        Scope1PreviewResult exactPreview = exact.PreviewSpan(FirstWitness);
        Check(exactPreview.Accepted, "exact MaxSpan preview was rejected");
        Equal(16L, exactPreview.DistanceSquared, "exact MaxSpan distanceSquared");
        Equal(16L, exactPreview.MaxSpanSquared, "exact MaxSpan maxSpanSquared");
        AssertAccepted(exact, exact.AddSupport(FirstWitness), "exact MaxSpan command");

        Scope1PlacementSession diagonal = NewSession();
        Scope1Point justOver = new(5, 5);
        Scope1PreviewResult overPreview = diagonal.PreviewSpan(justOver);
        Check(!overPreview.Accepted, "distanceSquared 17 preview was accepted");
        Equal(Scope1ErrorCode.SpanTooLong, overPreview.ErrorCode, "distanceSquared 17 code");
        Equal(17L, overPreview.DistanceSquared, "distanceSquared 17 value");
        Equal(16L, overPreview.MaxSpanSquared, "distanceSquared 17 maximum");
        AssertRejected(
            diagonal,
            () => diagonal.AddSupport(justOver),
            Scope1ErrorCode.SpanTooLong,
            "distanceSquared 17 command");

        foreach (Scope1Point extreme in new[]
                 {
                     new Scope1Point(int.MaxValue, int.MaxValue),
                     new Scope1Point(int.MinValue, int.MinValue),
                     new Scope1Point(int.MaxValue, int.MinValue),
                     new Scope1Point(int.MinValue, int.MaxValue),
                 })
        {
            Scope1PlacementSession previewSession = NewSession();
            string before = Serialize(previewSession.GetView());
            Scope1PreviewResult preview = previewSession.PreviewSpan(extreme);
            Check(!preview.Accepted, $"extreme {extreme} preview was accepted");
            Equal(Scope1ErrorCode.InvalidPosition, preview.ErrorCode, $"extreme {extreme} preview code");
            Equal(extreme, preview.To, $"extreme {extreme} preview target");
            Check(preview.DistanceSquared >= 0, $"extreme {extreme} produced negative squared distance");
            Equal(16L, preview.MaxSpanSquared, $"extreme {extreme} maximum squared distance");
            Equal(before, Serialize(previewSession.GetView()), $"extreme {extreme} preview mutated state");

            Scope1PlacementSession commandSession = NewSession();
            AssertRejected(
                commandSession,
                () => commandSession.AddSupport(extreme),
                Scope1ErrorCode.InvalidPosition,
                $"extreme {extreme} command");
        }
    }

    private void CheckAtomicCompletionAndDefensiveViews()
    {
        Scope1PlacementSession session = NewSession();
        Scope1View initial = session.GetView();
        CheckReadOnlySupports(initial, "initial view");

        Scope1CommandResult first = session.AddSupport(FirstWitness);
        AssertAccepted(session, first, "defensive/first");
        Equal(0, initial.SupportPositions.Count, "initial view changed after first command");
        CheckReadOnlySupports(first.View, "first command view");

        Scope1CommandResult second = session.AddSupport(SecondWitness);
        AssertAccepted(session, second, "defensive/second");
        Equal(1, first.View.SupportPositions.Count, "first command view changed after second command");
        SequenceEqual([FirstWitness], first.View.SupportPositions, "first command view contents changed");

        string beforeMutationAttempt = Serialize(session.GetView());
        TryMutateReturnedSupports(second.View.SupportPositions);
        Equal(
            beforeMutationAttempt,
            Serialize(session.GetView()),
            "mutating a returned support list changed session state");

        Scope1CommandResult ordered = session.OrderLine();
        AssertAccepted(session, ordered, "atomic/order");
        Equal(Scope1Phase.Building, ordered.View.Phase, "atomic/building phase");
        Equal(0, ordered.View.Minute, "atomic/building minute");
        Equal<int?>(60, ordered.View.CompletionMinute, "atomic/completion minute");
        Check(!ordered.View.TargetEnergized, "target energized before atomic completion");
        SequenceEqual(
            [FirstWitness, SecondWitness],
            ordered.View.SupportPositions,
            "ordering changed support positions");

        AssertRejected(
            session,
            () => session.AddSupport(new Scope1Point(10, 4)),
            Scope1ErrorCode.WrongPhase,
            "atomic/building-edit");

        Scope1CommandResult completed = session.AdvanceToCompletion();
        AssertAccepted(session, completed, "atomic/complete");
        Equal(Scope1Phase.Commissioned, completed.View.Phase, "atomic/commissioned phase");
        Equal(60, completed.View.Minute, "atomic/commissioned minute");
        Equal<int?>(60, completed.View.CompletionMinute, "atomic/completion minute changed");
        Check(completed.View.TargetEnergized, "target remained unenergized after completion");
        SequenceEqual(
            [FirstWitness, SecondWitness],
            completed.View.SupportPositions,
            "completion changed support positions");
    }

    private void CheckDeterministicJsonAndHash()
    {
        IReadOnlyList<Scope1View> firstTrace = RunCompletionTrace();
        IReadOnlyList<Scope1View> secondTrace = RunCompletionTrace();
        Equal(firstTrace.Count, secondTrace.Count, "deterministic trace length");

        for (int index = 0; index < firstTrace.Count; index++)
        {
            Scope1View firstState = firstTrace[index];
            Scope1View secondState = secondTrace[index];
            string firstStateJson = Scope1ViewJson.Serialize(firstState);
            byte[] firstStateBytes = Scope1ViewJson.SerializeToUtf8Bytes(firstState);
            string expectedStateHash = Convert.ToHexString(SHA256.HashData(firstStateBytes)).ToLowerInvariant();

            Equal(
                firstStateJson,
                Scope1ViewJson.Serialize(secondState),
                $"trace {index}: identical command sequences produced different JSON");
            Equal(
                expectedStateHash,
                Scope1ViewJson.Sha256Hex(firstState),
                $"trace {index}: hash does not cover serialized UTF-8 bytes");
            Equal(
                Scope1ViewJson.Sha256Hex(firstState),
                Scope1ViewJson.Sha256Hex(secondState),
                $"trace {index}: identical command sequences produced different hashes");
        }

        Scope1View firstView = firstTrace[^1];
        Scope1View secondView = secondTrace[^1];

        string firstJson = Scope1ViewJson.Serialize(firstView);
        string repeatedJson = Scope1ViewJson.Serialize(firstView);
        string secondJson = Scope1ViewJson.Serialize(secondView);
        byte[] firstBytes = Scope1ViewJson.SerializeToUtf8Bytes(firstView);
        byte[] repeatedBytes = Scope1ViewJson.SerializeToUtf8Bytes(firstView);

        Equal(firstJson, repeatedJson, "repeated JSON changed");
        Equal(firstJson, secondJson, "identical sessions produced different JSON");
        Equal(firstJson, Encoding.UTF8.GetString(firstBytes), "string and UTF-8 JSON differ");
        SequenceEqual(firstBytes, repeatedBytes, "repeated UTF-8 JSON changed");

        string expectedHash = Convert.ToHexString(SHA256.HashData(firstBytes)).ToLowerInvariant();
        string firstHash = Scope1ViewJson.Sha256Hex(firstView);
        string secondHash = Scope1ViewJson.Sha256Hex(secondView);
        Equal(expectedHash, firstHash, "Scope1ViewJson hash does not cover its UTF-8 bytes");
        Equal(firstHash, secondHash, "identical sessions produced different hashes");
        Equal(64, firstHash.Length, "SHA-256 hex length");
        Equal(firstHash.ToLowerInvariant(), firstHash, "SHA-256 hex must be lowercase");

        using JsonDocument document = JsonDocument.Parse(firstBytes);
        JsonElement root = document.RootElement;
        Equal(JsonValueKind.Object, root.ValueKind, "view JSON root kind");
        SequenceEqual(
            ViewPropertyOrder,
            root.EnumerateObject().Select(property => property.Name),
            "view JSON property order");
        Equal(60, root.GetProperty("minute").GetInt32(), "view JSON minute");
        Equal("commissioned", root.GetProperty("phase").GetString(), "view JSON phase");
        Equal(60, root.GetProperty("completionMinute").GetInt32(), "view JSON completionMinute");
        Check(root.GetProperty("targetEnergized").GetBoolean(), "view JSON targetEnergized");

        JsonElement supports = root.GetProperty("supportPositions");
        Equal(JsonValueKind.Array, supports.ValueKind, "view JSON supportPositions kind");
        Equal(2, supports.GetArrayLength(), "view JSON support count");
        Scope1Point[] parsedSupports = supports.EnumerateArray()
            .Select(point => new Scope1Point(point.GetProperty("x").GetInt32(), point.GetProperty("y").GetInt32()))
            .ToArray();
        SequenceEqual([FirstWitness, SecondWitness], parsedSupports, "view JSON support order");

        string draftingHash = Scope1ViewJson.Sha256Hex(NewSession().GetView());
        Check(!string.Equals(firstHash, draftingHash, StringComparison.Ordinal), "different views shared one hash");
    }

    private void AssertSpanPreviewParity(
        Func<Scope1PlacementSession> factory,
        Scope1Point position,
        Scope1ErrorCode? expectedErrorCode,
        Scope1Point expectedFrom,
        long? expectedDistanceSquared,
        string label)
    {
        Scope1PlacementSession previewSession = factory();
        string before = Serialize(previewSession.GetView());
        Scope1PreviewResult first = previewSession.PreviewSpan(position);
        Scope1PreviewResult second = previewSession.PreviewSpan(position);
        Equal(first, second, $"{label}: repeated previews differ");
        Equal(before, Serialize(previewSession.GetView()), $"{label}: preview mutated state");

        Equal(expectedErrorCode is null, first.Accepted, $"{label}: preview accepted");
        Equal(expectedErrorCode, first.ErrorCode, $"{label}: preview code");
        Equal(expectedFrom, first.From, $"{label}: preview from");
        Equal(position, first.To, $"{label}: preview to");
        Equal((long)_fixture.MaxSpan * _fixture.MaxSpan, first.MaxSpanSquared, $"{label}: maximum squared");
        if (expectedDistanceSquared is not null)
        {
            Equal(expectedDistanceSquared.Value, first.DistanceSquared, $"{label}: distance squared");
        }

        Scope1PlacementSession commandSession = factory();
        Scope1CommandResult result = commandSession.AddSupport(position);
        Equal(first.Accepted, result.Accepted, $"{label}: preview/command acceptance parity");
        Equal(first.ErrorCode, result.ErrorCode, $"{label}: preview/command code parity");
    }

    private void AssertTargetPreviewParity(
        Func<Scope1PlacementSession> factory,
        Scope1ErrorCode? expectedErrorCode,
        Scope1Point expectedFrom,
        long expectedDistanceSquared,
        string label)
    {
        Scope1PlacementSession previewSession = factory();
        string before = Serialize(previewSession.GetView());
        Scope1PreviewResult first = previewSession.PreviewTarget();
        Scope1PreviewResult second = previewSession.PreviewTarget();
        Equal(first, second, $"{label}: repeated previews differ");
        Equal(before, Serialize(previewSession.GetView()), $"{label}: preview mutated state");

        Equal(expectedErrorCode is null, first.Accepted, $"{label}: preview accepted");
        Equal(expectedErrorCode, first.ErrorCode, $"{label}: preview code");
        Equal(expectedFrom, first.From, $"{label}: preview from");
        Equal(_fixture.Target, first.To, $"{label}: preview to");
        Equal(expectedDistanceSquared, first.DistanceSquared, $"{label}: distance squared");
        Equal((long)_fixture.MaxSpan * _fixture.MaxSpan, first.MaxSpanSquared, $"{label}: maximum squared");

        Scope1PlacementSession commandSession = factory();
        Scope1CommandResult result = commandSession.OrderLine();
        Equal(first.Accepted, result.Accepted, $"{label}: preview/command acceptance parity");
        Equal(first.ErrorCode, result.ErrorCode, $"{label}: preview/command code parity");
    }

    private Scope1CommandResult AssertRejected(
        Scope1PlacementSession session,
        Func<Scope1CommandResult> command,
        Scope1ErrorCode expectedCode,
        string label)
    {
        string before = Serialize(session.GetView());
        string beforeHash = Scope1ViewJson.Sha256Hex(session.GetView());
        Scope1CommandResult result = command();
        string after = Serialize(session.GetView());

        Check(!result.Accepted, $"{label}: rejected command was accepted");
        Equal(expectedCode, result.ErrorCode, $"{label}: error code");
        Equal(before, after, $"{label}: rejected command mutated state");
        Equal(before, Serialize(result.View), $"{label}: rejected result view differs from unchanged state");
        Equal(beforeHash, Scope1ViewJson.Sha256Hex(session.GetView()), $"{label}: rejected command changed hash");
        return result;
    }

    private void AssertAccepted(Scope1PlacementSession session, Scope1CommandResult result, string label)
    {
        Check(result.Accepted, $"{label}: command was rejected");
        Equal<Scope1ErrorCode?>(null, result.ErrorCode, $"{label}: accepted command returned an error");
        Equal(Serialize(session.GetView()), Serialize(result.View), $"{label}: result view differs from session");
    }

    private void AssertView(
        Scope1View view,
        int minute,
        Scope1Phase phase,
        IReadOnlyList<Scope1Point> supports,
        int? completionMinute,
        bool targetEnergized,
        string label)
    {
        Equal(minute, view.Minute, $"{label}: minute");
        Equal(phase, view.Phase, $"{label}: phase");
        SequenceEqual(supports, view.SupportPositions, $"{label}: supports");
        Equal(completionMinute, view.CompletionMinute, $"{label}: completionMinute");
        Equal(targetEnergized, view.TargetEnergized, $"{label}: targetEnergized");
        CheckReadOnlySupports(view, label);
    }

    private void CheckReadOnlySupports(Scope1View view, string label)
    {
        if (view.SupportPositions is ICollection<Scope1Point> collection)
        {
            Check(collection.IsReadOnly, $"{label}: supportPositions exposes a writable collection");
        }
    }

    private static void TryMutateReturnedSupports(IReadOnlyList<Scope1Point> supports)
    {
        if (supports is not IList<Scope1Point> list)
        {
            return;
        }

        if (list.Count > 0)
        {
            try
            {
                list[0] = new Scope1Point(int.MinValue, int.MaxValue);
            }
            catch (NotSupportedException)
            {
                // Expected for a read-only wrapper.
            }
        }

        try
        {
            list.Add(new Scope1Point(int.MaxValue, int.MinValue));
        }
        catch (NotSupportedException)
        {
            // Expected for a read-only wrapper.
        }
    }

    private Scope1PlacementSession NewSession() => new(_fixture);

    private Scope1PlacementSession SessionWithSupports(int count)
    {
        Scope1PlacementSession session = NewSession();
        if (count >= 1)
        {
            AssertAccepted(session, session.AddSupport(FirstWitness), "setup/first-support");
        }

        if (count >= 2)
        {
            AssertAccepted(session, session.AddSupport(SecondWitness), "setup/second-support");
        }

        return session;
    }

    private Scope1PlacementSession OrderedSession()
    {
        Scope1PlacementSession session = SessionWithSupports(2);
        AssertAccepted(session, session.OrderLine(), "setup/order");
        return session;
    }

    private Scope1PlacementSession CommissionedSession()
    {
        Scope1PlacementSession session = OrderedSession();
        AssertAccepted(session, session.AdvanceToCompletion(), "setup/complete");
        return session;
    }

    private IReadOnlyList<Scope1View> RunCompletionTrace()
    {
        Scope1PlacementSession session = NewSession();
        var views = new List<Scope1View> { session.GetView() };
        views.Add(session.OrderLine().View);
        views.Add(session.AddSupport(new Scope1Point(6, 4)).View);
        views.Add(session.AddSupport(FirstWitness).View);
        views.Add(session.OrderLine().View);
        views.Add(session.AddSupport(SecondWitness).View);
        views.Add(session.OrderLine().View);
        views.Add(session.AdvanceToCompletion().View);
        return views;
    }

    private void ExpectFixtureRejected(string label, Action<JsonObject> mutation)
    {
        JsonObject root = JsonNode.Parse(_fixtureJson)?.AsObject()
            ?? throw new InvalidOperationException("fixture root was not an object");
        mutation(root);
        ExpectFixtureRejected(label, root.ToJsonString());
    }

    private void ExpectFixtureRejected(string label, string json)
    {
        ExpectThrows(label + "/text", () => _ = Scope1FixtureLoader.Load(json));
        byte[] utf8 = Encoding.UTF8.GetBytes(json);
        ExpectThrows(label + "/UTF-8", () => _ = Scope1FixtureLoader.Load(utf8));
    }

    private void ExpectFixtureRejectedBytes(string label, byte[] utf8)
    {
        ExpectThrows(label, () => _ = Scope1FixtureLoader.Load(utf8));
    }

    private void ExpectThrows(string label, Action action)
    {
        try
        {
            action();
        }
        catch (FixtureValidationException)
        {
            _assertionCount++;
            return;
        }

        throw new InvalidOperationException($"{label}: fixture was accepted");
    }

    private static JsonObject Object(JsonObject root, string propertyName) =>
        root[propertyName] as JsonObject
        ?? throw new InvalidOperationException($"{propertyName} was not an object");

    private static string ReplaceRequired(string value, string oldValue, string newValue)
    {
        string replaced = value.Replace(oldValue, newValue, StringComparison.Ordinal);
        if (string.Equals(replaced, value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"fixture text did not contain required token: {oldValue}");
        }

        return replaced;
    }

    private static string Serialize(Scope1View view) => Scope1ViewJson.Serialize(view);

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

    private void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
    {
        _assertionCount++;
        T[] expectedArray = expected.ToArray();
        T[] actualArray = actual.ToArray();
        if (!expectedArray.SequenceEqual(actualArray))
        {
            throw new InvalidOperationException(
                $"{message}: expected [{string.Join(", ", expectedArray)}], " +
                $"got [{string.Join(", ", actualArray)}]");
        }
    }
}
