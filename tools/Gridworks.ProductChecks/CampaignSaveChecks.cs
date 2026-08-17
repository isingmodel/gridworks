using System.Text;
using System.Text.Json.Nodes;
using Gridworks.Core.Product;

namespace Gridworks.ProductChecks;

internal sealed class CampaignSaveChecks
{
    // Checker-only completion witnesses. They are not runtime recommendations.
    private static readonly ProductPoint SuccessSubstation = new(14, 6);
    private static readonly ProductPoint OutsideSubstation = new(13, 6);
    private static readonly ProductPoint[] TownSupports = [new(6, 6), new(10, 6)];
    private static readonly ProductPoint[] PrimarySupports =
        [new(5, 5), new(9, 5), new(13, 5), new(16, 4)];
    private static readonly ProductPoint[] BackupSupports =
        [new(4, 3), new(7, 1), new(11, 1), new(15, 1)];
    private static readonly ProductPoint PlantSite = new(6, 10);
    private static readonly ProductPoint PlantConnectionSupport = new(4, 8);

    private const int SecondHeartStartCommandCount = 8;
    private const int HeatDomeStartCommandCount = 22;

    private readonly string _campaignJson;
    private readonly byte[] _campaignBytes;
    private readonly byte[] _fixtureBytes;
    private readonly ProductCampaignDefinition _campaign;
    private readonly ProductFixture _fixture;
    private readonly string _campaignHash;
    private readonly string _fixtureHash;
    private int _assertionCount;

    public CampaignSaveChecks(string campaignPath)
    {
        string absoluteCampaignPath = Path.GetFullPath(campaignPath);
        _campaignBytes = File.ReadAllBytes(absoluteCampaignPath);
        _campaignJson = Encoding.UTF8.GetString(_campaignBytes);
        _campaign = ProductCampaignLoader.Load(_campaignBytes);

        string directory = Path.GetDirectoryName(absoluteCampaignPath)
            ?? throw new ArgumentException("Campaign path has no directory.", nameof(campaignPath));
        string fixturePath = Path.GetFullPath(Path.Combine(directory, _campaign.ScenarioFixture));
        _fixtureBytes = File.ReadAllBytes(fixturePath);
        _fixture = ProductFixtureLoader.Load(_fixtureBytes);
        _campaignHash = ProductContentHash.ComputeSha256(_campaignBytes);
        _fixtureHash = ProductContentHash.ComputeSha256(_fixtureBytes);
    }

    public int Run()
    {
        (string Name, Action Body)[] suites =
        [
            ("campaign-save-settings-strict-codecs", CheckStrictCodecs),
            ("campaign-all-prefix-save-replay", CheckEveryAcceptedPrefixReplay),
            ("campaign-chapter-boundaries-restart", CheckChapterBoundariesAndRestart),
            ("campaign-invalid-save-safe-rejection", CheckInvalidSaveRejection),
            ("campaign-atomic-store-settings", CheckAtomicStoreAndSettingsPersistence),
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
                $"Gridworks Campaign Save checks: FAIL ({failures.Count}/{suites.Length} suites)");
            return 1;
        }

        Console.WriteLine(
            $"Gridworks Campaign Save checks: PASS ({suites.Length} suites, {_assertionCount} assertions)");
        return 0;
    }

    private void CheckStrictCodecs()
    {
        ProductCampaignDefinition fromText = ProductCampaignLoader.Load(_campaignJson);
        ProductCampaignDefinition fromBytes = ProductCampaignLoader.Load(_campaignBytes);
        Equal(ProductCampaignLoader.SupportedSchemaVersion, fromText.SchemaVersion, "campaign schema");
        Equal(ProductCampaignLoader.SupportedCampaignId, fromText.CampaignId, "campaign ID");
        Equal("Gridworks", fromText.DisplayName, "campaign display name");
        Equal(ProductCampaignLoader.SupportedScenarioFixture, fromText.ScenarioFixture, "scenario fixture");
        SequenceEqual(
            ["FIRST_LIGHT", "SECOND_HEART", "HEAT_DOME"],
            fromText.Chapters.Select(chapter => chapter.ChapterId),
            "chapter order");
        SequenceEqual(
            fromText.Chapters.Select(chapter => chapter.DisplayName),
            fromBytes.Chapters.Select(chapter => chapter.DisplayName),
            "UTF-8 campaign load");

        ExpectCampaignRejected("unknown campaign field", root => root["unexpected"] = true);
        ExpectCampaignRejected("parent campaign path", root =>
            root["scenarioFixture"] = "../product-heatwave-v1.json");
        ExpectCampaignRejected("wrong chapter order", root =>
        {
            JsonArray chapters = Array(root, "chapters");
            Object(chapters, 0)["chapterId"] = "SECOND_HEART";
            Object(chapters, 1)["chapterId"] = "FIRST_LIGHT";
        });

        ProductCampaignSave fresh = NewRun().CaptureSave();
        byte[] saveBytes = ProductCampaignSaveCodec.Serialize(fresh);
        ProductCampaignSave decoded = ProductCampaignSaveCodec.Deserialize(saveBytes);
        Equal(ProductCampaignSaveCodec.SupportedSchemaVersion, decoded.SchemaVersion, "save schema");
        Equal(_campaign.CampaignId, decoded.CampaignId, "save campaign ID");
        Equal(_campaignHash, decoded.CampaignRootSha256, "save campaign hash");
        Equal(_fixture.FixtureId, decoded.FixtureId, "save fixture ID");
        Equal(_fixtureHash, decoded.FixtureSha256, "save fixture hash");
        Equal(0, decoded.Commands.Count, "fresh save command count");
        SequenceEqual(saveBytes, ProductCampaignSaveCodec.Serialize(decoded), "stable save bytes");

        JsonObject saveRoot = ParseObject(saveBytes, "save root");
        saveRoot["unexpected"] = true;
        ExpectSaveCodecRejected("unknown save field", Encoding.UTF8.GetBytes(saveRoot.ToJsonString()));

        ProductSettings settings = new(
            ProductSettings.SupportedSchemaVersion,
            ProductWindowMode.Fullscreen,
            125,
            false);
        byte[] settingsBytes = ProductSettingsCodec.Serialize(settings);
        Equal(settings, ProductSettingsCodec.Deserialize(settingsBytes), "settings round-trip");
        SequenceEqual(settingsBytes, ProductSettingsCodec.Serialize(settings), "stable settings bytes");
        ExpectSettingsCodecRejected(
            "unsupported UI scale",
            Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":\"gridworks.settings.v1\",\"windowMode\":\"windowed\",\"uiScalePercent\":150,\"showControlHelp\":true}"));
    }

    private void CheckEveryAcceptedPrefixReplay()
    {
        ProductCampaignCommand[] commands = FullSuccessCommands();
        ProductCampaignRun live = NewRun();

        for (int prefixLength = 0; prefixLength <= commands.Length; prefixLength++)
        {
            ProductCampaignSave encodedSave = ProductCampaignSaveCodec.Deserialize(
                ProductCampaignSaveCodec.Serialize(live.CaptureSave()));
            ProductCampaignRun restored = Restore(encodedSave);
            Equal(live.GetSnapshot(), restored.GetSnapshot(), $"prefix {prefixLength} snapshot");
            Equal(live.CurrentChapterId, restored.CurrentChapterId, $"prefix {prefixLength} chapter");
            Equal(
                live.ChapterStartCommandCount,
                restored.ChapterStartCommandCount,
                $"prefix {prefixLength} checkpoint");
            Equal(prefixLength, restored.CommandCount, $"prefix {prefixLength} command count");

            if (prefixLength < commands.Length)
            {
                AssertAccepted(live, live.Execute(commands[prefixLength]), $"prefix {prefixLength} execute");
            }
        }
    }

    private void CheckChapterBoundariesAndRestart()
    {
        ProductCampaignCommand[] commands = FullSuccessCommands();
        ProductCampaignRun secondHeart = NewRun();
        ExecutePrefix(secondHeart, commands, SecondHeartStartCommandCount, "first chapter");
        Equal("SECOND_HEART", secondHeart.CurrentChapterId, "second chapter ID");
        Equal(
            SecondHeartStartCommandCount,
            secondHeart.ChapterStartCommandCount,
            "second chapter checkpoint");
        ProductSnapshot secondStart = secondHeart.GetSnapshot();
        AssertAccepted(
            secondHeart,
            secondHeart.Execute(Positioned(ProductCampaignCommandKind.AddLineSupport, PrimarySupports[0])),
            "second chapter draft");
        AssertAccepted(secondHeart, secondHeart.RestartChapter(), "restart second chapter");
        Equal(secondStart, secondHeart.GetSnapshot(), "second chapter restart snapshot");
        Equal(SecondHeartStartCommandCount, secondHeart.CommandCount, "second chapter truncated count");

        ProductCampaignRun heatDome = NewRun();
        ExecutePrefix(heatDome, commands, HeatDomeStartCommandCount, "second chapter");
        Equal("HEAT_DOME", heatDome.CurrentChapterId, "third chapter ID");
        Equal(
            HeatDomeStartCommandCount,
            heatDome.ChapterStartCommandCount,
            "third chapter checkpoint");
        ProductSnapshot heatDomeStart = heatDome.GetSnapshot();
        AssertAccepted(
            heatDome,
            heatDome.Execute(Positioned(ProductCampaignCommandKind.SetPlantDraft, PlantSite)),
            "third chapter draft");
        AssertAccepted(heatDome, heatDome.RestartChapter(), "restart third chapter");
        Equal(heatDomeStart, heatDome.GetSnapshot(), "third chapter restart snapshot");
        Equal(HeatDomeStartCommandCount, heatDome.CommandCount, "third chapter truncated count");

        ProductCampaignRun firstFailure = NewRun();
        ProductCampaignCommand[] failureCommands = FirstLightCommands(OutsideSubstation);
        ExecutePrefix(firstFailure, failureCommands, failureCommands.Length, "first chapter failure");
        Equal(ProductPhase.Complete, firstFailure.GetSnapshot().Phase, "first failure phase");
        Equal(ProductMissionOutcome.Failure, firstFailure.GetSnapshot().Outcome, "first failure outcome");
        Equal("FIRST_LIGHT", firstFailure.CurrentChapterId, "first failure chapter");
        Equal(0, firstFailure.ChapterStartCommandCount, "first failure checkpoint");
    }

    private void CheckInvalidSaveRejection()
    {
        ProductCampaignRun run = NewRun();
        AssertAccepted(
            run,
            run.Execute(Positioned(ProductCampaignCommandKind.SetSubstationDraft, SuccessSubstation)),
            "invalid-save valid setup");
        ProductCampaignSave valid = run.CaptureSave();
        byte[] validBytes = ProductCampaignSaveCodec.Serialize(valid);

        ExpectSaveCodecRejected("truncated save", validBytes[..^1]);
        ExpectSaveCodecRejected("malformed save", Encoding.UTF8.GetBytes("{"));

        JsonObject unknownVersion = ParseObject(validBytes, "unknown version save");
        unknownVersion["schemaVersion"] = "gridworks.campaign-save.v999";
        ExpectSaveCodecRejected(
            "unknown save version",
            Encoding.UTF8.GetBytes(unknownVersion.ToJsonString()));

        JsonObject unknownCommand = ParseObject(validBytes, "unknown command save");
        Object(Array(unknownCommand, "commands"), 0)["kind"] = "UnknownCommand";
        ExpectSaveCodecRejected(
            "unknown command",
            Encoding.UTF8.GetBytes(unknownCommand.ToJsonString()));

        ExpectRestoreRejected(
            "campaign hash mismatch",
            valid with { CampaignRootSha256 = new string('0', 64) });
        ExpectRestoreRejected(
            "fixture hash mismatch",
            valid with { FixtureSha256 = new string('1', 64) });
        ExpectRestoreRejected(
            "rejected replay command",
            valid with
            {
                Commands = System.Array.AsReadOnly(
                    new[] { Plain(ProductCampaignCommandKind.OrderSubstation) }),
            });

        ProductCampaignRun clean = NewRun();
        Equal(0, clean.CommandCount, "invalid saves leaked commands");
        Equal(ProductPhase.SubstationPlanning, clean.GetSnapshot().Phase, "invalid saves leaked state");
    }

    private void CheckAtomicStoreAndSettingsPersistence()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"gridworks-campaign-save-checks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string savePath = Path.Combine(directory, ProductPersistenceStore.CampaignSaveFileName);
        string settingsPath = Path.Combine(directory, ProductPersistenceStore.SettingsFileName);
        try
        {
            ProductCampaignRun run = NewRun();
            ProductPersistenceStore.SaveCampaign(savePath, run.CaptureSave());
            ProductCampaignSaveLoadResult firstLoad = ProductPersistenceStore.LoadCampaignSave(savePath);
            Equal(ProductDocumentLoadStatus.Loaded, firstLoad.Status, "first atomic save status");
            Equal(0, firstLoad.Save?.Commands.Count, "first atomic save commands");

            AssertAccepted(
                run,
                run.Execute(Positioned(ProductCampaignCommandKind.SetSubstationDraft, SuccessSubstation)),
                "atomic overwrite setup");
            ProductPersistenceStore.SaveCampaign(savePath, run.CaptureSave());
            ProductCampaignSaveLoadResult overwritten = ProductPersistenceStore.LoadCampaignSave(savePath);
            Equal(ProductDocumentLoadStatus.Loaded, overwritten.Status, "overwrite status");
            Equal(1, overwritten.Save?.Commands.Count, "overwrite commands");
            False(File.Exists(savePath + ".tmp"), "successful write left a temp file");

            byte[] committedBytes = File.ReadAllBytes(savePath);
            File.WriteAllText(savePath + ".tmp", "{truncated", Encoding.UTF8);
            ProductCampaignSaveLoadResult withStaleTemp =
                ProductPersistenceStore.LoadCampaignSave(savePath);
            Equal(ProductDocumentLoadStatus.Loaded, withStaleTemp.Status, "stale temp load status");
            SequenceEqual(committedBytes, File.ReadAllBytes(savePath), "stale temp changed committed save");

            ProductSettings settings = new(
                ProductSettings.SupportedSchemaVersion,
                ProductWindowMode.Fullscreen,
                125,
                false);
            ProductPersistenceStore.SaveSettings(settingsPath, settings);
            ProductSettingsLoadResult settingsLoad = ProductPersistenceStore.LoadSettings(settingsPath);
            Equal(ProductDocumentLoadStatus.Loaded, settingsLoad.Status, "settings load status");
            Equal(settings, settingsLoad.Settings, "settings persisted value");

            File.WriteAllText(settingsPath, "{", Encoding.UTF8);
            ProductSettingsLoadResult corruptSettings = ProductPersistenceStore.LoadSettings(settingsPath);
            Equal(ProductDocumentLoadStatus.Invalid, corruptSettings.Status, "corrupt settings status");
            Equal(ProductSettings.Default, corruptSettings.Settings, "corrupt settings default");

            File.WriteAllText(savePath, "{", Encoding.UTF8);
            ProductCampaignSaveLoadResult corruptSave =
                ProductPersistenceStore.LoadCampaignSave(savePath);
            Equal(ProductDocumentLoadStatus.Invalid, corruptSave.Status, "corrupt save status");
            Equal(null, corruptSave.Save, "corrupt save exposed partial state");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private ProductCampaignRun NewRun() => new(
        _campaign,
        _fixture,
        _campaignHash,
        _fixtureHash);

    private ProductCampaignRun Restore(ProductCampaignSave save) => ProductCampaignRun.Restore(
        _campaign,
        _fixture,
        _campaignHash,
        _fixtureHash,
        save);

    private ProductCampaignCommand[] FullSuccessCommands() =>
    [
        .. FirstLightCommands(SuccessSubstation),
        .. PrimarySupports.Select(point => Positioned(ProductCampaignCommandKind.AddLineSupport, point)),
        Plain(ProductCampaignCommandKind.OrderLine),
        Plain(ProductCampaignCommandKind.AdvanceToConstructionCompletion),
        .. BackupSupports.Select(point => Positioned(ProductCampaignCommandKind.AddLineSupport, point)),
        Plain(ProductCampaignCommandKind.OrderLine),
        Plain(ProductCampaignCommandKind.AdvanceToConstructionCompletion),
        Plain(ProductCampaignCommandKind.AdvanceToIncident),
        Plain(ProductCampaignCommandKind.AdvanceToRecoveryAndSettlement),
        Positioned(ProductCampaignCommandKind.SetPlantDraft, PlantSite),
        Plain(ProductCampaignCommandKind.OrderPlant),
        Plain(ProductCampaignCommandKind.AdvanceToConstructionCompletion),
        Positioned(ProductCampaignCommandKind.AddLineSupport, PlantConnectionSupport),
        Plain(ProductCampaignCommandKind.OrderLine),
        Plain(ProductCampaignCommandKind.AdvanceToConstructionCompletion),
        Plain(ProductCampaignCommandKind.AdvanceToFactorySettlement),
        Plain(ProductCampaignCommandKind.OrderPreventiveMaintenance),
        Plain(ProductCampaignCommandKind.AdvanceToConstructionCompletion),
        Plain(ProductCampaignCommandKind.AdvanceToHeatwave),
        Plain(ProductCampaignCommandKind.AdvanceToHeatwaveSettlement),
    ];

    private static ProductCampaignCommand[] FirstLightCommands(ProductPoint substation) =>
    [
        Positioned(ProductCampaignCommandKind.SetSubstationDraft, substation),
        Plain(ProductCampaignCommandKind.OrderSubstation),
        Plain(ProductCampaignCommandKind.AdvanceToConstructionCompletion),
        .. TownSupports.Select(point => Positioned(ProductCampaignCommandKind.AddLineSupport, point)),
        Plain(ProductCampaignCommandKind.OrderLine),
        Plain(ProductCampaignCommandKind.AdvanceToConstructionCompletion),
        Plain(ProductCampaignCommandKind.AdvanceToSettlement),
    ];

    private void ExecutePrefix(
        ProductCampaignRun run,
        IReadOnlyList<ProductCampaignCommand> commands,
        int count,
        string label)
    {
        for (int index = 0; index < count; index++)
        {
            AssertAccepted(run, run.Execute(commands[index]), $"{label}/{index}");
        }
    }

    private void ExpectCampaignRejected(string label, Action<JsonObject> mutation)
    {
        JsonObject root = JsonNode.Parse(_campaignJson)?.AsObject()
            ?? throw new InvalidOperationException("Campaign JSON is not an object.");
        mutation(root);
        try
        {
            _ = ProductCampaignLoader.Load(root.ToJsonString());
            throw new InvalidOperationException($"{label} was accepted");
        }
        catch (ProductCampaignValidationException)
        {
            _assertionCount++;
        }
    }

    private void ExpectSaveCodecRejected(string label, ReadOnlySpan<byte> json)
    {
        try
        {
            _ = ProductCampaignSaveCodec.Deserialize(json);
            throw new InvalidOperationException($"{label} was accepted");
        }
        catch (ProductPersistenceValidationException)
        {
            _assertionCount++;
        }
    }

    private void ExpectSettingsCodecRejected(string label, ReadOnlySpan<byte> json)
    {
        try
        {
            _ = ProductSettingsCodec.Deserialize(json);
            throw new InvalidOperationException($"{label} was accepted");
        }
        catch (ProductPersistenceValidationException)
        {
            _assertionCount++;
        }
    }

    private void ExpectRestoreRejected(string label, ProductCampaignSave save)
    {
        try
        {
            _ = Restore(save);
            throw new InvalidOperationException($"{label} was accepted");
        }
        catch (ProductPersistenceValidationException)
        {
            _assertionCount++;
        }
    }

    private void AssertAccepted(
        ProductCampaignRun run,
        ProductCommandResult result,
        string label)
    {
        True(result.Accepted, $"{label}/accepted");
        Equal(null, result.Error, $"{label}/error");
        Equal(run.GetSnapshot(), result.Snapshot, $"{label}/snapshot");
    }

    private static ProductCampaignCommand Positioned(
        ProductCampaignCommandKind kind,
        ProductPoint position) => new(kind, position);

    private static ProductCampaignCommand Plain(ProductCampaignCommandKind kind) => new(kind);

    private static JsonObject ParseObject(ReadOnlySpan<byte> json, string label) =>
        JsonNode.Parse(Encoding.UTF8.GetString(json))?.AsObject()
        ?? throw new InvalidOperationException($"{label} is not an object.");

    private static JsonArray Array(JsonObject parent, string property) =>
        parent[property]?.AsArray()
        ?? throw new InvalidOperationException($"{property} is not an array.");

    private static JsonObject Object(JsonArray parent, int index) =>
        parent[index]?.AsObject()
        ?? throw new InvalidOperationException($"array item {index} is not an object.");

    private void True(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private void False(bool condition, string message) => True(!condition, message);

    private void Equal<T>(T expected, T actual, string message)
    {
        _assertionCount++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message}: expected {expected}, actual {actual}");
        }
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
}
