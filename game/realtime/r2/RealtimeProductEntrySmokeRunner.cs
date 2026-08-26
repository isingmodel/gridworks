#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;

namespace Gridworks.Game.Realtime.R2;

/// <summary>
/// Small production-entry smoke. It uses the actual default scene and pointer
/// input, but deliberately avoids the much larger responsive UI harness.
/// </summary>
internal sealed partial class RealtimeProductEntrySmokeRunner : Control
{
    private const string SliceScenePath =
        "res://realtime/r2/RealtimeSliceMain.tscn";
    private const string SaveCreatePrefix = "--save-create=";
    private const string SaveCompletedCreatePrefix = "--save-completed-create=";
    private const string SaveCompletedNewGamePrefix = "--save-completed-new-game=";
    private const string SaveNonSaveableExitPrefix = "--save-nonsaveable-exit=";
    private const string SaveResetPrefix = "--save-reset=";
    private const string SaveContinuePrefix = "--save-continue=";
    private const string SaveLegacyContinuePrefix = "--save-legacy-continue=";
    private const string SaveInvalidPrefix = "--save-invalid=";
    private const string SaveUnsupportedPrefix = "--save-unsupported=";
    private const string SaveIoFailurePrefix = "--save-io-failure=";
    private const string SettingsCreatePrefix = "--settings-create=";
    private const string SettingsRestorePrefix = "--settings-restore=";
    private const string SettingsInvalidPrefix = "--settings-invalid=";
    private const string SettingsUnsupportedPrefix = "--settings-unsupported=";
    private const string SettingsReadFailurePrefix = "--settings-read-failure=";

    private enum EntryMode
    {
        ProductTitle,
        TechnicalFixture,
        CreateSave,
        CreateCompletedSave,
        CompletedNewGame,
        NonSaveableExit,
        ResetSave,
        ContinueSave,
        LegacyContinueSave,
        InvalidSave,
        UnsupportedSave,
        IoFailureSave,
        CreateSettings,
        RestoreSettings,
        InvalidSettings,
        UnsupportedSettings,
        ReadFailureSettings,
    }

    private sealed record EntryRequest(
        EntryMode Mode,
        string? SavePath,
        string? SettingsPath,
        bool DeleteSaveAfterRun,
        bool DeleteSettingsAfterRun);

    private sealed record SettingsFixture(
        byte[]? GuardedBytes,
        FileStream? ReadLock);

    private static RealtimeProductSettings StoredSettings { get; } = new(
        RealtimeProductSettings.SupportedSchemaVersion,
        RealtimeProductWindowMode.Fullscreen,
        200,
        0,
        25,
        75,
        ReduceMotion: true);

    private static RealtimeProductSettings RejectedSettings { get; } = new(
        RealtimeProductSettings.SupportedSchemaVersion,
        RealtimeProductWindowMode.Windowed,
        150,
        100,
        50,
        0,
        ReduceMotion: false);

    private sealed record SaveExpectation(
        string RouteId,
        long Minute,
        string CanonicalStateSha256,
        int CommandCount,
        int ClosedStoryCount,
        bool Terminal);

    private sealed record ResetExpectation(
        string BackupPath,
        byte[] OriginalBytes,
        SaveExpectation ExpectedWrite);

    private sealed record SettingsGameplayInvariant(
        string CanonicalStateSha256,
        IReadOnlyList<TimedRealtimeCommand> Commands,
        RealtimeMapCameraSnapshot Camera,
        RealtimeInteractionState Interaction,
        bool OwnsProductProgress);

    public override void _Ready() => _ = RunAsync();

    private async Task RunAsync()
    {
        int exitCode = 1;
        SubViewport? viewport = null;
        EntryRequest? request = null;
        byte[]? guardedBytes = null;
        byte[]? priorSaveBytes = null;
        byte[]? preservedSaveBytes = null;
        byte[]? preservedSettingsBytes = null;
        ResetExpectation? resetExpectation = null;
        FileStream? settingsReadLock = null;
        try
        {
            request = ParseRequest(OS.GetCmdlineUserArgs());
            guardedBytes = PrepareSaveFixture(request);
            SettingsFixture settingsFixture = PrepareSettingsFixture(request);
            settingsReadLock = settingsFixture.ReadLock;

            viewport = new SubViewport
            {
                Size = new Vector2I(1920, 1080),
                Disable3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            };
            AddChild(viewport);
            PackedScene packed = ResourceLoader.Load<PackedScene>(SliceScenePath) ??
                throw new InvalidOperationException(
                    $"Unable to load actual product scene '{SliceScenePath}'.");
            RealtimeSliceMain slice = packed.Instantiate<RealtimeSliceMain>();
            if (request.Mode == EntryMode.TechnicalFixture)
            {
                slice.SetSettingsPathOverrideForSmoke(request.SettingsPath!);
            }
            else
            {
                slice.UseProductTitleLaunchForSmoke();
                slice.SetSavePathOverrideForSmoke(request.SavePath!);
                slice.SetSettingsPathOverrideForSmoke(request.SettingsPath!);
            }
            viewport.AddChild(slice);
            await SettleFrames(4);

            SaveExpectation? expectedWrite = null;
            switch (request.Mode)
            {
                case EntryMode.TechnicalFixture:
                    preservedSettingsBytes = settingsFixture.GuardedBytes;
                    await ValidateTechnicalFixture(
                        viewport,
                        slice,
                        request.SettingsPath!);
                    break;
                case EntryMode.ProductTitle:
                    ValidateExplicitNativeRoutes();
                    await ValidateProductTitle(viewport, slice);
                    break;
                case EntryMode.CreateSave:
                    await ValidateProductTitle(viewport, slice);
                    expectedWrite = PrepareInitialBriefingProgress(slice);
                    break;
                case EntryMode.CreateCompletedSave:
                    ValidateCompletedSaveTitle(slice);
                    break;
                case EntryMode.CompletedNewGame:
                    priorSaveBytes = File.ReadAllBytes(request.SavePath!);
                    expectedWrite = await ValidateCompletedSaveNewGame(
                        viewport,
                        slice,
                        request.SavePath!,
                        priorSaveBytes);
                    break;
                case EntryMode.NonSaveableExit:
                    preservedSaveBytes = await ValidateNonSaveableExit(
                        viewport,
                        slice,
                        request.SavePath!);
                    break;
                case EntryMode.ResetSave:
                    resetExpectation = await ValidateReset(
                        viewport,
                        slice,
                        request.SavePath!);
                    priorSaveBytes = resetExpectation.OriginalBytes;
                    expectedWrite = resetExpectation.ExpectedWrite;
                    break;
                case EntryMode.ContinueSave:
                    expectedWrite = await ValidateContinue(
                        viewport,
                        slice,
                        request.SavePath!,
                        RealtimeNativeRouteCatalog.ProductCampaign);
                    break;
                case EntryMode.LegacyContinueSave:
                    expectedWrite = await ValidateContinue(
                        viewport,
                        slice,
                        request.SavePath!,
                        RealtimeNativeRouteCatalog.FirstLight);
                    break;
                case EntryMode.InvalidSave:
                case EntryMode.UnsupportedSave:
                case EntryMode.IoFailureSave:
                    await ValidateBlockedSaveTitle(
                        viewport,
                        slice,
                        request.Mode,
                        request.SavePath!);
                    break;
                case EntryMode.CreateSettings:
                    preservedSettingsBytes = await ValidateSettingsCreate(
                        viewport,
                        slice,
                        request.SettingsPath!);
                    break;
                case EntryMode.RestoreSettings:
                    preservedSettingsBytes = await ValidateSettingsRestore(
                        viewport,
                        slice,
                        request.SettingsPath!);
                    break;
                case EntryMode.InvalidSettings:
                    preservedSettingsBytes = await ValidateGuardedSettings(
                        viewport,
                        slice,
                        settingsFixture.GuardedBytes!,
                        "손상");
                    break;
                case EntryMode.UnsupportedSettings:
                    preservedSettingsBytes = await ValidateGuardedSettings(
                        viewport,
                        slice,
                        settingsFixture.GuardedBytes!,
                        "지원하지");
                    break;
                case EntryMode.ReadFailureSettings:
                    preservedSettingsBytes = await ValidateGuardedSettings(
                        viewport,
                        slice,
                        settingsFixture.GuardedBytes!,
                        "읽지 못");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            settingsReadLock?.Dispose();
            settingsReadLock = null;

            if (expectedWrite is { Terminal: true })
            {
                priorSaveBytes = StageInProgressReplacementProbe(
                    request.SavePath!,
                    expectedWrite);
            }

            viewport.QueueFree();
            await SettleFrames(2);
            viewport = null;
            if (expectedWrite is not null)
            {
                ValidateWrittenSave(
                    request.SavePath!,
                    expectedWrite,
                    priorSaveBytes);
            }
            if (preservedSaveBytes is not null)
            {
                Require(File.ReadAllBytes(request.SavePath!).SequenceEqual(
                        preservedSaveBytes),
                    "A non-saveable normal tree exit changed the prior save bytes.");
            }
            if (resetExpectation is not null)
            {
                ValidateAndDeleteResetBackup(resetExpectation);
                resetExpectation = null;
            }
            ValidateGuardedSavePreserved(request, guardedBytes);
            if (preservedSettingsBytes is not null)
            {
                Require(File.ReadAllBytes(request.SettingsPath!).SequenceEqual(
                        preservedSettingsBytes),
                    "A settings smoke changed the guarded primary bytes on tree exit.");
            }

            GD.Print(request.Mode switch
            {
                EntryMode.TechnicalFixture =>
                    "REALTIME_PRODUCT_ENTRY_FIXTURE_PASS",
                EntryMode.ProductTitle =>
                    "REALTIME_PRODUCT_ENTRY_TITLE_PASS",
                EntryMode.CreateSave =>
                    "REALTIME_PRODUCT_ENTRY_SAVE_CREATE_PASS",
                EntryMode.CreateCompletedSave =>
                    "REALTIME_PRODUCT_ENTRY_SAVE_COMPLETED_CREATE_PASS",
                EntryMode.CompletedNewGame =>
                    "REALTIME_PRODUCT_ENTRY_SAVE_COMPLETED_NEW_GAME_PASS",
                EntryMode.NonSaveableExit =>
                    "REALTIME_PRODUCT_ENTRY_SAVE_NON_SAVEABLE_EXIT_PASS",
                EntryMode.ResetSave =>
                    "REALTIME_PRODUCT_ENTRY_SAVE_RESET_PASS",
                EntryMode.ContinueSave =>
                    "REALTIME_PRODUCT_ENTRY_SAVE_CONTINUE_PASS",
                EntryMode.LegacyContinueSave =>
                    "REALTIME_PRODUCT_ENTRY_SAVE_LEGACY_CONTINUE_PASS",
                EntryMode.InvalidSave =>
                    "REALTIME_PRODUCT_ENTRY_SAVE_INVALID_PASS",
                EntryMode.UnsupportedSave =>
                    "REALTIME_PRODUCT_ENTRY_SAVE_UNSUPPORTED_PASS",
                EntryMode.IoFailureSave =>
                    "REALTIME_PRODUCT_ENTRY_SAVE_IO_FAILURE_PASS",
                EntryMode.CreateSettings =>
                    "REALTIME_PRODUCT_ENTRY_SETTINGS_CREATE_PASS",
                EntryMode.RestoreSettings =>
                    "REALTIME_PRODUCT_ENTRY_SETTINGS_RESTORE_PASS",
                EntryMode.InvalidSettings =>
                    "REALTIME_PRODUCT_ENTRY_SETTINGS_INVALID_PASS",
                EntryMode.UnsupportedSettings =>
                    "REALTIME_PRODUCT_ENTRY_SETTINGS_UNSUPPORTED_PASS",
                EntryMode.ReadFailureSettings =>
                    "REALTIME_PRODUCT_ENTRY_SETTINGS_READ_FAILURE_PASS",
                _ => throw new ArgumentOutOfRangeException(),
            });
            exitCode = 0;
        }
        catch (Exception exception)
        {
            GD.PushError(
                $"REALTIME_PRODUCT_ENTRY_FAIL {exception.GetType().Name}: " +
                exception.Message);
        }
        settingsReadLock?.Dispose();
        if (viewport is not null && GodotObject.IsInstanceValid(viewport))
        {
            viewport.QueueFree();
        }
        if (request is { DeleteSaveAfterRun: true, SavePath: not null })
        {
            try
            {
                File.Delete(request.SavePath);
            }
            catch (IOException)
            {
                // The isolated path is best-effort cleanup after the smoke outcome.
            }
        }
        if (request is
            {
                DeleteSettingsAfterRun: true,
                SettingsPath: not null,
            })
        {
            try
            {
                File.Delete(request.SettingsPath);
            }
            catch (IOException)
            {
                // The isolated path is best-effort cleanup after the smoke outcome.
            }
        }
        if (resetExpectation is not null)
        {
            try
            {
                File.Delete(resetExpectation.BackupPath);
            }
            catch (IOException)
            {
                // The exact smoke-created backup is best-effort cleanup on failure.
            }
        }
        ScheduleQuit(exitCode);
    }

    private static EntryRequest ParseRequest(string[] arguments)
    {
        if (arguments.Length == 0)
        {
            string savePath = Path.Combine(
                Path.GetTempPath(),
                $"gridworks-product-entry-{Guid.NewGuid():N}.json");
            return new EntryRequest(
                EntryMode.ProductTitle,
                savePath,
                $"{savePath}.settings",
                DeleteSaveAfterRun: true,
                DeleteSettingsAfterRun: true);
        }
        if (arguments.Length != 1)
        {
            throw new ArgumentException(
                "Product-entry smoke accepts at most one user argument.");
        }
        if (string.Equals(
                arguments[0],
                RealtimeLaunchCatalog.TechnicalFixtureArgument,
                StringComparison.Ordinal))
        {
            string technicalSettingsPath = Path.Combine(
                Path.GetTempPath(),
                $"gridworks-technical-entry-{Guid.NewGuid():N}.settings");
            return new EntryRequest(
                EntryMode.TechnicalFixture,
                null,
                technicalSettingsPath,
                DeleteSaveAfterRun: false,
                DeleteSettingsAfterRun: true);
        }

        EntryMode mode;
        string path;
        bool settingsPath = false;
        if (arguments[0].StartsWith(
                SettingsReadFailurePrefix,
                StringComparison.Ordinal))
        {
            mode = EntryMode.ReadFailureSettings;
            path = arguments[0][SettingsReadFailurePrefix.Length..];
            settingsPath = true;
        }
        else if (arguments[0].StartsWith(
                     SettingsRestorePrefix,
                     StringComparison.Ordinal))
        {
            mode = EntryMode.RestoreSettings;
            path = arguments[0][SettingsRestorePrefix.Length..];
            settingsPath = true;
        }
        else if (arguments[0].StartsWith(
                     SettingsInvalidPrefix,
                     StringComparison.Ordinal))
        {
            mode = EntryMode.InvalidSettings;
            path = arguments[0][SettingsInvalidPrefix.Length..];
            settingsPath = true;
        }
        else if (arguments[0].StartsWith(
                     SettingsUnsupportedPrefix,
                     StringComparison.Ordinal))
        {
            mode = EntryMode.UnsupportedSettings;
            path = arguments[0][SettingsUnsupportedPrefix.Length..];
            settingsPath = true;
        }
        else if (arguments[0].StartsWith(
                     SettingsCreatePrefix,
                     StringComparison.Ordinal))
        {
            mode = EntryMode.CreateSettings;
            path = arguments[0][SettingsCreatePrefix.Length..];
            settingsPath = true;
        }
        else if (arguments[0].StartsWith(
                SaveCompletedCreatePrefix,
                StringComparison.Ordinal))
        {
            mode = EntryMode.CreateCompletedSave;
            path = arguments[0][SaveCompletedCreatePrefix.Length..];
        }
        else if (arguments[0].StartsWith(
                     SaveCompletedNewGamePrefix,
                     StringComparison.Ordinal))
        {
            mode = EntryMode.CompletedNewGame;
            path = arguments[0][SaveCompletedNewGamePrefix.Length..];
        }
        else if (arguments[0].StartsWith(
                     SaveNonSaveableExitPrefix,
                     StringComparison.Ordinal))
        {
            mode = EntryMode.NonSaveableExit;
            path = arguments[0][SaveNonSaveableExitPrefix.Length..];
        }
        else if (arguments[0].StartsWith(
                     SaveResetPrefix,
                     StringComparison.Ordinal))
        {
            mode = EntryMode.ResetSave;
            path = arguments[0][SaveResetPrefix.Length..];
        }
        else if (arguments[0].StartsWith(SaveCreatePrefix, StringComparison.Ordinal))
        {
            mode = EntryMode.CreateSave;
            path = arguments[0][SaveCreatePrefix.Length..];
        }
        else if (arguments[0].StartsWith(
                     SaveContinuePrefix,
                     StringComparison.Ordinal))
        {
            mode = EntryMode.ContinueSave;
            path = arguments[0][SaveContinuePrefix.Length..];
        }
        else if (arguments[0].StartsWith(
                     SaveLegacyContinuePrefix,
                     StringComparison.Ordinal))
        {
            mode = EntryMode.LegacyContinueSave;
            path = arguments[0][SaveLegacyContinuePrefix.Length..];
        }
        else if (arguments[0].StartsWith(
                     SaveInvalidPrefix,
                     StringComparison.Ordinal))
        {
            mode = EntryMode.InvalidSave;
            path = arguments[0][SaveInvalidPrefix.Length..];
        }
        else if (arguments[0].StartsWith(
                     SaveUnsupportedPrefix,
                     StringComparison.Ordinal))
        {
            mode = EntryMode.UnsupportedSave;
            path = arguments[0][SaveUnsupportedPrefix.Length..];
        }
        else if (arguments[0].StartsWith(
                     SaveIoFailurePrefix,
                     StringComparison.Ordinal))
        {
            mode = EntryMode.IoFailureSave;
            path = arguments[0][SaveIoFailurePrefix.Length..];
        }
        else
        {
            throw new ArgumentException(
                "Product-entry smoke accepts no user argument, the technical " +
                "fixture argument, or one supported save-smoke path.");
        }
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("The smoke path must be absolute.");
        }
        return settingsPath
            ? new EntryRequest(
                mode,
                $"{path}.campaign",
                path,
                DeleteSaveAfterRun: true,
                DeleteSettingsAfterRun: false)
            : new EntryRequest(
                mode,
                path,
                $"{path}.settings",
                DeleteSaveAfterRun: false,
                DeleteSettingsAfterRun: true);
    }

    private static byte[]? PrepareSaveFixture(EntryRequest request)
    {
        if (request.Mode == EntryMode.CreateCompletedSave)
        {
            PrepareCompletedProductSave(request.SavePath!);
            return null;
        }
        if (request.Mode == EntryMode.LegacyContinueSave)
        {
            PrepareLegacyFirstLightSave(request.SavePath!);
            return null;
        }
        if (request.Mode is not (
                EntryMode.InvalidSave or
                EntryMode.UnsupportedSave or
                EntryMode.IoFailureSave))
        {
            return null;
        }
        string path = request.SavePath!;
        if (File.Exists(path) || Directory.Exists(path))
        {
            throw new InvalidOperationException(
                "A guarded-save smoke path must start absent.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (request.Mode == EntryMode.IoFailureSave)
        {
            Directory.CreateDirectory(path);
            return null;
        }

        byte[] bytes = request.Mode == EntryMode.UnsupportedSave
            ? "{\"schemaVersion\":\"gridworks.realtime.campaign-save.v4\"}"u8.ToArray()
            : "{\"broken\":true}"u8.ToArray();
        File.WriteAllBytes(path, bytes);
        return bytes;
    }

    private static SettingsFixture PrepareSettingsFixture(EntryRequest request)
    {
        if (request.Mode is not (
                EntryMode.TechnicalFixture or
                EntryMode.CreateSettings or
                EntryMode.RestoreSettings or
                EntryMode.InvalidSettings or
                EntryMode.UnsupportedSettings or
                EntryMode.ReadFailureSettings))
        {
            return new SettingsFixture(null, null);
        }

        string settingsPath = request.SettingsPath!;
        if (request.Mode == EntryMode.TechnicalFixture)
        {
            byte[] sentinel = "technical-route-settings-sentinel"u8.ToArray();
            File.WriteAllBytes(settingsPath, sentinel);
            return new SettingsFixture(sentinel, null);
        }
        string campaignPath = request.SavePath!;
        if (File.Exists(campaignPath) || Directory.Exists(campaignPath))
        {
            throw new InvalidOperationException(
                "A settings smoke campaign path must start absent.");
        }
        if (request.Mode == EntryMode.RestoreSettings)
        {
            if (!File.Exists(settingsPath))
            {
                throw new InvalidOperationException(
                    "Settings restore requires the process-A primary file.");
            }
            return new SettingsFixture(File.ReadAllBytes(settingsPath), null);
        }
        if (File.Exists(settingsPath) || Directory.Exists(settingsPath))
        {
            throw new InvalidOperationException(
                "A fresh settings smoke path must start absent.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        if (request.Mode == EntryMode.CreateSettings)
        {
            return new SettingsFixture(null, null);
        }

        byte[] bytes = request.Mode switch
        {
            EntryMode.InvalidSettings => "{\"broken\":true}"u8.ToArray(),
            EntryMode.UnsupportedSettings =>
                "{\"schemaVersion\":\"gridworks.realtime-settings.v2\"}"u8.ToArray(),
            _ => RealtimeProductSettingsCodec.Serialize(StoredSettings),
        };
        File.WriteAllBytes(settingsPath, bytes);
        if (request.Mode is EntryMode.InvalidSettings or EntryMode.UnsupportedSettings)
        {
            return new SettingsFixture(bytes, null);
        }
        return new SettingsFixture(
            bytes,
            new FileStream(
                settingsPath,
                FileMode.Open,
                System.IO.FileAccess.Read,
                FileShare.None));
    }

    private static void PrepareCompletedProductSave(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            throw new InvalidOperationException(
                "A completed-save smoke path must start absent.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var failures = new List<string>();
        RealtimeCampaignSave save =
            RealtimeR2Smoke.CreateCompletedProductSave(failures);
        if (failures.Count != 0)
        {
            throw new InvalidOperationException(
                "Unable to create the completed product save: " +
                string.Join(" | ", failures));
        }
        RealtimeCampaignSaveStore.Save(path, save);
    }

    private static byte[] StageInProgressReplacementProbe(
        string path,
        SaveExpectation terminal)
    {
        RealtimeSliceData data = RealtimeSliceResources.LoadNativeRelease(
            typeof(RealtimeSliceMain).Assembly,
            RealtimeNativeRouteCatalog.ProductCampaign);
        var run = new RealtimeCampaignRun(data.Campaign, data.World);
        RealtimeAdvanceResult initial = run.AdvanceTo(run.Minute);
        var storyFlow = new RealtimeChapterStoryFlow();
        storyFlow.Restore(
            initial.Transitions,
            data.Campaign,
            closedStoryCount: 0,
            run.Minute);
        Require(storyFlow.IsExactInitialActive(
                    data.Campaign.Chapters[0].Content.ChapterId),
            "The terminal replacement probe could not stage initial progress.");
        RealtimeCampaignSave progress = RealtimeCampaignSaveCodec.Capture(
            data.RequireSaveSourceIdentity(),
            data.Campaign,
            data.World,
            run,
            storyFlow.ClosedStoryCount);
        Require(progress.SavedMinute != terminal.Minute ||
                progress.CanonicalStateSha256 != terminal.CanonicalStateSha256 ||
                progress.Commands.Count != terminal.CommandCount ||
                progress.ClosedStoryCount != terminal.ClosedStoryCount,
            "The terminal replacement probe is not distinguishable progress.");
        RealtimeCampaignSaveStore.Save(path, progress);
        return File.ReadAllBytes(path);
    }

    private static void PrepareLegacyFirstLightSave(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            throw new InvalidOperationException(
                "A legacy-save smoke path must start absent.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        RealtimeSliceData data = RealtimeSliceResources.LoadNativeRelease(
            typeof(RealtimeSliceMain).Assembly,
            RealtimeNativeRouteCatalog.FirstLight);
        var run = new RealtimeCampaignRun(data.Campaign, data.World);
        RealtimeCommandResult draft = run.ApplyCommand(RealtimeCommand.SetNodeDraft(
            "SMALL_SUBSTATION",
            new MapPoint(2100, 700)));
        Require(draft.Accepted, "Unable to create the legacy FIRST_LIGHT node draft.");
        RealtimeCommandResult order = run.ApplyCommand(RealtimeCommand.OrderNode());
        Require(order.Accepted, "Unable to create the legacy FIRST_LIGHT node order.");
        ActiveConstructionSnapshot construction =
            order.Snapshot.Construction.ActiveConstruction ??
            throw new InvalidOperationException(
                "The legacy FIRST_LIGHT order created no construction.");
        long savedMinute = checked(order.Snapshot.Minute + 15);
        Require(savedMinute < construction.CompletionMinute,
            "The legacy FIRST_LIGHT save is not mid-construction.");
        _ = run.AdvanceTo(savedMinute);
        Require(RealtimeSession.IsJournalRestorableProgressSnapshot(
                run.GetSnapshot()),
            "The legacy FIRST_LIGHT save is not journal-restorable.");
        RealtimeCampaignSave current = RealtimeCampaignSaveCodec.Capture(
            data.RequireSaveSourceIdentity(),
            data.Campaign,
            data.World,
            run,
            closedStoryCount: 0);
        JsonObject legacy = JsonNode.Parse(
                Encoding.UTF8.GetString(
                    RealtimeCampaignSaveCodec.Serialize(current)))?.AsObject() ??
            throw new InvalidOperationException(
                "The legacy FIRST_LIGHT fixture is not a JSON object.");
        legacy["schemaVersion"] = RealtimeCampaignSave.LegacySchemaVersion;
        legacy.Remove("closedStoryCount");
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(legacy.ToJsonString()));
    }

    private static void ValidateGuardedSavePreserved(
        EntryRequest request,
        byte[]? originalBytes)
    {
        if (request.Mode == EntryMode.IoFailureSave)
        {
            Require(Directory.Exists(request.SavePath!) &&
                    !File.Exists(request.SavePath!),
                "The I/O-failure save target was changed.");
            return;
        }
        if (request.Mode is EntryMode.InvalidSave or EntryMode.UnsupportedSave)
        {
            Require(originalBytes is not null &&
                    File.ReadAllBytes(request.SavePath!).SequenceEqual(originalBytes),
                "The blocked save bytes were changed.");
        }
    }

    private async Task ValidateProductTitle(
        SubViewport viewport,
        RealtimeSliceMain slice)
    {
        ValidateAudioSceneWiring(slice);
        RealtimeUiRoot ui = slice.UiForSmoke;
        RealtimeProductTitle title = ui.ProductTitleForSmoke;
        Require(slice.LaunchForSmoke.Kind == RealtimeLaunchKind.ProductTitle,
            "No-argument boot did not select the product title.");
        Require(!slice.HasSessionForSmoke,
            "Product title bootstrapped a hidden fixture/session.");
        Require(title.Visible && !ui.HudSurfaceVisibleForSmoke &&
                !slice.WorldVisibleForSmoke,
            "Product title did not exclusively own the visible entry surface.");
        Require(title.NewGameButton.Visible && !title.NewGameButton.Disabled,
            "New Game is not a visible enabled title action.");
        Require(ReferenceEquals(ui.FocusOwnerForSmoke, title.NewGameButton),
            "Product title did not place initial focus on New Game.");
        Require(title.ContinueButton.Visible && title.ContinueButton.Disabled,
            "Continue must remain visible and disabled before R2 saves exist.");
        Require(!string.IsNullOrWhiteSpace(title.ContinueReasonText) &&
                title.ContinueReasonText.Contains("저장", StringComparison.Ordinal) &&
                string.Equals(
                    title.ContinueButton.AccessibilityDescription,
                    title.ContinueReasonText,
                    StringComparison.Ordinal),
            "Disabled Continue has no clear visible/accessibility reason.");
        Require(ui.InputRouterForSmoke.ActiveOwner == "product_title" &&
                ui.InputRouterForSmoke.ActivePriority ==
                    RealtimeInputPriority.BlockingModal,
            "Product title does not own blocking input priority.");

        PushPrimary(viewport, title.ContinueButton.GetGlobalRect().GetCenter());
        await SettleFrames(2);
        Require(!slice.HasSessionForSmoke && title.Visible,
            "Disabled Continue opened a fake recovery path.");

        PushPrimary(viewport, title.NewGameButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        ValidateStartedProductCampaign(slice, title);
    }

    private static void ValidateStartedProductCampaign(
        RealtimeSliceMain slice,
        RealtimeProductTitle title)
    {
        RealtimeUiRoot ui = slice.UiForSmoke;
        Require(slice.HasSessionForSmoke && !title.Visible &&
                ui.HudSurfaceVisibleForSmoke && slice.WorldVisibleForSmoke,
            "New Game input did not replace the title with the live R2 surface.");
        Require(slice.LaunchForSmoke.Kind == RealtimeLaunchKind.NativeRelease &&
                ReferenceEquals(
                    slice.LaunchForSmoke.NativeRoute,
                    RealtimeNativeRouteCatalog.ProductCampaign) &&
                ReferenceEquals(
                    slice.SliceDataForSmoke.NativeRoute,
                    RealtimeNativeRouteCatalog.ProductCampaign) &&
                slice.OwnsProductProgressForSmoke,
            "New Game did not select the product-owned cumulative native route.");
        Require(slice.AudioForSmoke.AmbientStartCountForSmoke == 1 &&
                slice.AudioForSmoke.LiveCuePlayCountForSmoke == 0,
            "Starting gameplay restarted ambience or replayed a historical SFX cue.");

        RealtimeSliceData data = slice.SliceDataForSmoke;
        CommercialCampaignChapterDefinition authored = data.BaseCampaign.Chapters.Single(
            item => string.Equals(
                item.ChapterId,
                RealtimeCampaignOverlayLoader.FirstReleaseChapterId,
                StringComparison.Ordinal));
        RealtimeModalPresentation briefing = slice.LatestPresentation.Modal ??
            throw new InvalidOperationException(
                "New Game did not show the FIRST_LIGHT briefing.");
        RealtimeChapterStoryModalRequest story =
            slice.ActiveChapterStoryModalForSmoke ??
            throw new InvalidOperationException(
                "New Game did not open an authored FIRST_LIGHT story request.");
        Require(data.Campaign.Chapters.Count ==
                    RealtimeNativeRouteCatalog.ProductCampaign.SelectedChapterCount &&
                data.Campaign.Chapters[0].Content.ChapterId ==
                    RealtimeCampaignOverlayLoader.FirstReleaseChapterId &&
                data.Campaign.Chapters[^1].Content.ChapterId ==
                    RealtimeNativeRouteCatalog.NativeThroughChapterId &&
                IsInitialBriefing(story) &&
                briefing.Id == story.ModalId &&
                briefing.Eyebrow == authored.Briefing.Speaker &&
                briefing.Heading == authored.Briefing.Title &&
                briefing.Body == authored.Briefing.Body,
            "New Game did not open the exact eight-chapter product route and " +
                "authored FIRST_LIGHT briefing.");
        Require(briefing.PrimaryAction.Id == RealtimeR2Ids.BriefingContinueAction &&
                briefing.PrimaryAction.Label == "도시 운영 시작" &&
                briefing.PrimaryAction.Label != title.ContinueButton.Text,
            "Story continue was confused with title Continue.");
    }

    private async Task<byte[]> ValidateSettingsCreate(
        SubViewport viewport,
        RealtimeSliceMain slice,
        string settingsPath)
    {
        RealtimeUiRoot ui = slice.UiForSmoke;
        RealtimeProductTitle title = ui.ProductTitleForSmoke;
        RealtimeSettingsSurface surface = ui.SettingsSurface;
        Require(!File.Exists(settingsPath) &&
                slice.ProductSettingsForSmoke == RealtimeProductSettings.Default &&
                !slice.SettingsStatusIsErrorForSmoke &&
                title.Visible &&
                title.SettingsButton.Visible &&
                !title.SettingsButton.Disabled &&
                ReferenceEquals(ui.FocusOwnerForSmoke, title.NewGameButton),
            "Missing settings did not open the product title with defaults.");
        ValidateSettingsRuntime(slice, RealtimeProductSettings.Default);

        PushPrimary(viewport, title.SettingsButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        ValidateOpenSettings(
            slice,
            RealtimeSettingsJourney.ProductTitle,
            surface);
        PushPrimary(viewport, surface.CloseButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        Require(!ui.SettingsVisible &&
                slice.ActiveSettingsJourneyForSmoke is null &&
                ReferenceEquals(ui.FocusOwnerForSmoke, title.SettingsButton) &&
                ui.InputRouterForSmoke.ActiveOwner == "product_title",
            "Pointer-close did not restore the title settings opener.");

        title.SettingsButton.GrabFocus();
        await SettleFrames(1);
        PushKey(viewport, Key.Enter, pressed: true);
        PushKey(viewport, Key.Enter, pressed: false);
        await SettleFrames(4);
        ValidateOpenSettings(
            slice,
            RealtimeSettingsJourney.ProductTitle,
            surface);
        SetSettingsControls(surface, StoredSettings);
        PushPrimary(viewport, surface.ApplyButton.GetGlobalRect().GetCenter());
        await SettleFrames(10);

        byte[] storedBytes = RealtimeProductSettingsCodec.Serialize(StoredSettings);
        Require(File.Exists(settingsPath) &&
                File.ReadAllBytes(settingsPath).SequenceEqual(storedBytes) &&
                !File.Exists($"{settingsPath}.tmp") &&
                !Directory.Exists($"{settingsPath}.tmp") &&
                !slice.SettingsStatusIsErrorForSmoke &&
                surface.StatusText.Contains("저장", StringComparison.Ordinal),
            "Title settings did not atomically persist the typed candidate.");
        RequireSettingsControls(surface, StoredSettings);
        ValidateSettingsRuntime(slice, StoredSettings);

        PushKey(viewport, Key.Escape, pressed: true);
        PushKey(viewport, Key.Escape, pressed: false);
        await SettleFrames(4);
        Require(!ui.SettingsVisible &&
                slice.ActiveSettingsJourneyForSmoke is null &&
                ReferenceEquals(ui.FocusOwnerForSmoke, title.SettingsButton),
            "Keyboard-close did not restore the title settings opener.");

        PushPrimary(viewport, title.NewGameButton.GetGlobalRect().GetCenter());
        await SettleFrames(5);
        ValidateStartedProductCampaign(slice, title);
        Require(slice.ClosePresentedStoryModalForSmoke() is null,
            "Settings gameplay probe could not close the initial briefing.");
        await SettleFrames(4);
        Require(slice.InteractionState.Simulation == RealtimeSimulationState.Running,
            "Fresh product gameplay was not running after its briefing.");
        slice.FreezeAutonomousClockForSmoke();
        slice.SetSpeedForSmoke(RealtimeSimulationSpeed.VeryFast);

        SpatialNodeDefinition selectedNode = slice.CoreSnapshot.Construction.World.Nodes
            .OrderBy(node => node.NodeId, StringComparer.Ordinal)
            .First();
        RequireAccepted(
            slice.ApplyIntentForSmoke(RealtimeR2Intent.Select(selectedNode.NodeId)),
            "select a gameplay settings invariant node");
        slice.RestoreCameraForSmoke(new RealtimeMapCameraSnapshot(
            new Vector2(
                selectedNode.Position.XUnit,
                selectedNode.Position.YUnit),
            ZoomIndex: 1));
        await SettleFrames(2);

        RealtimeTopHud topHud = ui.TopHudForSmoke;
        SettingsGameplayInvariant running = CaptureSettingsInvariant(slice);
        PushPrimary(viewport, topHud.SettingsButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        ValidateOpenSettings(slice, RealtimeSettingsJourney.Gameplay, surface);
        ValidateSettingsInvariant(
            slice,
            running,
            running.Interaction with
            {
                Simulation = RealtimeSimulationState.PlayerPaused,
                PauseReason = RealtimePauseReason.PlayerRequest,
            },
            "running settings open");
        PushPrimary(viewport, surface.CloseButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        ValidateSettingsInvariant(slice, running, running.Interaction,
            "running settings close");
        Require(!ui.SettingsVisible &&
                ReferenceEquals(ui.FocusOwnerForSmoke, topHud.SettingsButton),
            "Running settings close did not restore the gameplay opener.");

        slice.SetPlayerPausedForSmoke(true);
        SettingsGameplayInvariant paused = CaptureSettingsInvariant(slice);
        PushPrimary(viewport, topHud.SettingsButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        ValidateOpenSettings(slice, RealtimeSettingsJourney.Gameplay, surface);
        ValidateSettingsInvariant(slice, paused, paused.Interaction,
            "player-paused settings open");

        string blockedTemporaryPath = $"{settingsPath}.tmp";
        Directory.CreateDirectory(blockedTemporaryPath);
        try
        {
            SetSettingsControls(surface, RejectedSettings);
            PushPrimary(viewport, surface.ApplyButton.GetGlobalRect().GetCenter());
            await SettleFrames(6);
            Require(slice.SettingsStatusIsErrorForSmoke &&
                    surface.StatusText.Contains("저장하지 못", StringComparison.Ordinal) &&
                    File.ReadAllBytes(settingsPath).SequenceEqual(storedBytes),
                "A blocked settings write did not preserve the visible/raw state.");
            RequireSettingsControls(surface, StoredSettings);
            ValidateSettingsRuntime(slice, StoredSettings);
            ValidateSettingsInvariant(slice, paused, paused.Interaction,
                "failed settings write");
        }
        finally
        {
            if (Directory.Exists(blockedTemporaryPath))
            {
                Directory.Delete(blockedTemporaryPath);
            }
        }

        PushKey(viewport, Key.Escape, pressed: true);
        PushKey(viewport, Key.Escape, pressed: false);
        await SettleFrames(4);
        ValidateSettingsInvariant(slice, paused, paused.Interaction,
            "player-paused settings close");
        Require(!ui.SettingsVisible &&
                ReferenceEquals(ui.FocusOwnerForSmoke, topHud.SettingsButton),
            "Player-paused settings close did not restore the gameplay opener.");
        Require(slice.AudioForSmoke.AmbientStartCountForSmoke == 1 &&
                slice.AudioForSmoke.LiveCuePlayCountForSmoke == 0,
            "Settings lifecycle restarted ambience or emitted an unrelated SFX cue.");
        return storedBytes;
    }

    private async Task<byte[]> ValidateSettingsRestore(
        SubViewport viewport,
        RealtimeSliceMain slice,
        string settingsPath)
    {
        byte[] originalBytes = File.ReadAllBytes(settingsPath);
        byte[] expectedBytes = RealtimeProductSettingsCodec.Serialize(StoredSettings);
        RealtimeUiRoot ui = slice.UiForSmoke;
        RealtimeProductTitle title = ui.ProductTitleForSmoke;
        Require(originalBytes.SequenceEqual(expectedBytes) &&
                title.Visible &&
                !slice.HasSessionForSmoke &&
                slice.SettingsStatusForSmoke.Contains("불러", StringComparison.Ordinal) &&
                !slice.SettingsStatusIsErrorForSmoke,
            "Fresh process B did not load the process-A settings primary.");
        ValidateSettingsRuntime(slice, StoredSettings);

        PushPrimary(viewport, title.SettingsButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        ValidateOpenSettings(
            slice,
            RealtimeSettingsJourney.ProductTitle,
            ui.SettingsSurface);
        RequireSettingsControls(ui.SettingsSurface, StoredSettings);
        PushPrimary(
            viewport,
            ui.SettingsSurface.CloseButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        Require(!ui.SettingsVisible &&
                ReferenceEquals(ui.FocusOwnerForSmoke, title.SettingsButton) &&
                File.ReadAllBytes(settingsPath).SequenceEqual(originalBytes),
            "Fresh settings restore changed bytes or lost opener focus.");
        return originalBytes;
    }

    private async Task<byte[]> ValidateGuardedSettings(
        SubViewport viewport,
        RealtimeSliceMain slice,
        byte[] originalBytes,
        string expectedStatus)
    {
        RealtimeUiRoot ui = slice.UiForSmoke;
        RealtimeProductTitle title = ui.ProductTitleForSmoke;
        Require(title.Visible &&
                !slice.HasSessionForSmoke &&
                slice.ProductSettingsForSmoke == RealtimeProductSettings.Default &&
                slice.SettingsStatusIsErrorForSmoke &&
                slice.SettingsStatusForSmoke.Contains(
                    expectedStatus,
                    StringComparison.Ordinal) &&
                slice.SettingsStatusForSmoke.Contains("기본값", StringComparison.Ordinal) &&
                slice.SettingsStatusForSmoke.Contains("덮어쓰지", StringComparison.Ordinal),
            "A guarded settings load did not fail closed with visible defaults.");
        ValidateSettingsRuntime(slice, RealtimeProductSettings.Default);

        PushPrimary(viewport, title.SettingsButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        ValidateOpenSettings(
            slice,
            RealtimeSettingsJourney.ProductTitle,
            ui.SettingsSurface);
        Require(ui.SettingsSurface.StatusText.Contains(
                    expectedStatus,
                    StringComparison.Ordinal),
            "The guarded settings error was not visible on the shared surface.");
        RequireSettingsControls(ui.SettingsSurface, RealtimeProductSettings.Default);
        PushPrimary(
            viewport,
            ui.SettingsSurface.CloseButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        Require(!ui.SettingsVisible &&
                ReferenceEquals(ui.FocusOwnerForSmoke, title.SettingsButton),
            "Guarded settings close did not restore title focus.");
        return originalBytes;
    }

    private static void ValidateOpenSettings(
        RealtimeSliceMain slice,
        RealtimeSettingsJourney journey,
        RealtimeSettingsSurface surface)
    {
        RealtimeUiRoot ui = slice.UiForSmoke;
        Require(ui.SettingsVisible &&
                surface.Visible &&
                slice.ActiveSettingsJourneyForSmoke == journey &&
                ui.InputRouterForSmoke.ActiveOwner == "product_settings" &&
                ui.InputRouterForSmoke.ActivePriority ==
                    RealtimeInputPriority.BlockingModal &&
                ReferenceEquals(ui.FocusOwnerForSmoke, surface.WindowModeOption),
            "Settings did not own blocking input and initial focus for its journey.");
    }

    private static SettingsGameplayInvariant CaptureSettingsInvariant(
        RealtimeSliceMain slice) => new(
        slice.CanonicalStateSha256,
        Array.AsReadOnly(slice.AcceptedCommands.ToArray()),
        slice.CaptureCameraForSmoke(),
        slice.InteractionState,
        slice.OwnsProductProgressForSmoke);

    private static void ValidateSettingsInvariant(
        RealtimeSliceMain slice,
        SettingsGameplayInvariant expected,
        RealtimeInteractionState expectedInteraction,
        string stage)
    {
        Require(string.Equals(
                    slice.CanonicalStateSha256,
                    expected.CanonicalStateSha256,
                    StringComparison.Ordinal) &&
                slice.AcceptedCommands.SequenceEqual(expected.Commands) &&
                slice.CaptureCameraForSmoke() == expected.Camera &&
                slice.InteractionState == expectedInteraction &&
                slice.InteractionState.SelectionId ==
                    expected.Interaction.SelectionId &&
                slice.OwnsProductProgressForSmoke == expected.OwnsProductProgress,
            $"Settings changed Core/journal/camera/selection/ownership at {stage}.");
    }

    private static void ValidateSettingsRuntime(
        RealtimeSliceMain slice,
        RealtimeProductSettings expected)
    {
        RealtimeUiRoot ui = slice.UiForSmoke;
        Require(slice.ProductSettingsForSmoke == expected &&
                ui.UiScalePercent == expected.UiScalePercent &&
                slice.ReduceMotionForSmoke == expected.ReduceMotion &&
                (!slice.HasSessionForSmoke ||
                    slice.LatestPresentation.World.ReduceMotion ==
                        expected.ReduceMotion),
            "Settings did not project UI scale or Reduce Motion to current R2.");
        RequireBus("Master", expected.MasterVolumePercent);
        RequireBus("Ambient", expected.AmbientVolumePercent);
        RequireBus("SFX", expected.SfxVolumePercent);

        if (!string.Equals(
                DisplayServer.GetName(),
                "headless",
                StringComparison.OrdinalIgnoreCase))
        {
            Window.ModeEnum expectedMode = expected.WindowMode ==
                RealtimeProductWindowMode.Fullscreen
                    ? Window.ModeEnum.Fullscreen
                    : Window.ModeEnum.Windowed;
            Require(slice.GetWindow().Mode == expectedMode,
                "Settings did not project the requested native window mode.");
        }
    }

    private static void RequireBus(string busName, int percent)
    {
        int bus = AudioServer.GetBusIndex(busName);
        Require(bus >= 0 &&
                AudioServer.IsBusMute(bus) == (percent == 0) &&
                (percent == 0 || Mathf.IsEqualApprox(
                    AudioServer.GetBusVolumeLinear(bus),
                    percent / 100f)),
            $"Settings did not project {busName}={percent}% to its audio bus.");
    }

    private static void SetSettingsControls(
        RealtimeSettingsSurface surface,
        RealtimeProductSettings settings)
    {
        SelectOption(
            surface.WindowModeOption,
            settings.WindowMode == RealtimeProductWindowMode.Fullscreen ? 1 : 0);
        SelectOption(surface.UiScaleOption, settings.UiScalePercent);
        SelectOption(surface.MasterVolumeOption, settings.MasterVolumePercent);
        SelectOption(surface.AmbientVolumeOption, settings.AmbientVolumePercent);
        SelectOption(surface.SfxVolumeOption, settings.SfxVolumePercent);
        surface.ReduceMotionCheck.ButtonPressed = settings.ReduceMotion;
    }

    private static void RequireSettingsControls(
        RealtimeSettingsSurface surface,
        RealtimeProductSettings settings)
    {
        Require(SelectedOption(surface.WindowModeOption) ==
                    (settings.WindowMode == RealtimeProductWindowMode.Fullscreen ? 1 : 0) &&
                SelectedOption(surface.UiScaleOption) == settings.UiScalePercent &&
                SelectedOption(surface.MasterVolumeOption) ==
                    settings.MasterVolumePercent &&
                SelectedOption(surface.AmbientVolumeOption) ==
                    settings.AmbientVolumePercent &&
                SelectedOption(surface.SfxVolumeOption) == settings.SfxVolumePercent &&
                surface.ReduceMotionCheck.ButtonPressed == settings.ReduceMotion,
            "The shared settings controls do not show the committed values.");
    }

    private static void SelectOption(OptionButton option, int id)
    {
        for (int index = 0; index < option.ItemCount; index++)
        {
            if (option.GetItemId(index) == id)
            {
                option.Select(index);
                return;
            }
        }
        throw new InvalidOperationException($"Settings option id {id} is unavailable.");
    }

    private static int SelectedOption(OptionButton option) =>
        option.GetItemId(option.Selected);

    private static void ValidateCompletedSaveTitle(RealtimeSliceMain slice)
    {
        RealtimeUiRoot ui = slice.UiForSmoke;
        RealtimeProductTitle title = ui.ProductTitleForSmoke;
        Require(slice.LaunchForSmoke.Kind == RealtimeLaunchKind.ProductTitle &&
                !slice.HasSessionForSmoke &&
                title.Visible &&
                !title.ContinueButton.Disabled &&
                !title.NewGameButton.Disabled &&
                ReferenceEquals(ui.FocusOwnerForSmoke, title.ContinueButton) &&
                title.DetailText.Contains("완료", StringComparison.Ordinal),
            "The staged terminal save did not enable completed Continue and New Game.");
    }

    private async Task<SaveExpectation> ValidateCompletedSaveNewGame(
        SubViewport viewport,
        RealtimeSliceMain slice,
        string savePath,
        byte[] completedBytes)
    {
        ValidateCompletedSaveTitle(slice);
        RealtimeProductTitle title = slice.UiForSmoke.ProductTitleForSmoke;
        PushPrimary(viewport, title.NewGameButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        ValidateStartedProductCampaign(slice, title);

        SaveExpectation initial = PrepareInitialBriefingProgress(slice);
        Require(File.ReadAllBytes(savePath).SequenceEqual(completedBytes),
            "Selecting completed New Game changed the save before normal tree exit.");
        return initial;
    }

    private async Task<byte[]> ValidateNonSaveableExit(
        SubViewport viewport,
        RealtimeSliceMain slice,
        string savePath)
    {
        byte[] originalBytes = File.ReadAllBytes(savePath);
        RealtimeCampaignSaveLoadResult load = RealtimeCampaignSaveStore.Load(savePath);
        Require(load is
                {
                    Status: RealtimeCampaignSaveLoadStatus.Loaded,
                    Save: not null,
                },
            "The non-saveable exit probe could not read its prior save.");
        RealtimeCampaignSave save = load.Save!;
        RealtimeSliceData data = RealtimeSliceResources.LoadNativeRelease(
            typeof(RealtimeSliceMain).Assembly,
            RealtimeNativeRouteCatalog.ProductCampaign);
        RealtimeCampaignRestoreResult restore = RealtimeCampaignSaveCodec.Restore(
            data.RequireSaveSourceIdentity(),
            data.Campaign,
            data.World,
            save);
        RealtimeProgressResumePlan plan = RealtimeSession.ValidateProgressResume(
            data,
            restore);
        Require(plan.Kind == RealtimeProgressResumeKind.InProgress &&
                save.Commands.Count == 0 &&
                save.ClosedStoryCount == 0,
            "The non-saveable exit probe requires fresh c0 progress.");

        RealtimeUiRoot ui = slice.UiForSmoke;
        RealtimeProductTitle title = ui.ProductTitleForSmoke;
        Require(!title.ContinueButton.Disabled &&
                !title.NewGameButton.Disabled &&
                ReferenceEquals(ui.FocusOwnerForSmoke, title.ContinueButton),
            "Fresh c0 did not expose Continue and reset New Game.");
        PushPrimary(viewport, title.ContinueButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        RealtimeCampaignSnapshot expected = restore.Run.GetSnapshot();
        Require(slice.HasSessionForSmoke &&
                !title.Visible &&
                RealtimeStateCanonicalizer.StructuralEquals(
                    expected,
                    slice.CoreSnapshot),
            "The non-saveable exit probe did not restore exact c0 progress.");

        RealtimeChapterStoryModalRequest story =
            slice.ActiveChapterStoryModalForSmoke ??
            throw new InvalidOperationException(
                "The non-saveable exit probe did not restore the initial briefing.");
        Require(IsInitialBriefing(story) &&
                slice.ClosePresentedStoryModalForSmoke() is null,
            "The non-saveable exit probe could not close the initial briefing.");
        (string toolId, MapPoint position) = slice.AcceptedNodeDraftForSmoke();
        RequireAccepted(slice.ApplyIntentForSmoke(
            RealtimeR2Intent.SelectBuildTool(RealtimeTool.BuildNode, toolId)),
            "select a node tool for the non-saveable exit probe");
        RequireAccepted(slice.ApplyIntentForSmoke(new RealtimeR2Intent(
            RealtimeR2IntentKind.SetNodeDraft,
            FirstId: toolId[RealtimeR2Ids.NodeToolPrefix.Length..],
            Position: position)),
            "place a non-saveable node draft");
        Require(slice.CoreSnapshot.Construction.NodeDraft is not null,
            "The non-saveable exit probe did not retain its node draft.");
        bool captureRejected = false;
        try
        {
            _ = slice.CaptureProgressForSmoke();
        }
        catch (InvalidOperationException)
        {
            captureRejected = true;
        }
        Require(captureRejected,
            "A transient node draft unexpectedly became saveable progress.");
        slice.FreezeAutonomousClockForSmoke();
        Require(File.ReadAllBytes(savePath).SequenceEqual(originalBytes),
            "Staging a transient node draft changed the save before tree exit.");
        return originalBytes;
    }

    private async Task<ResetExpectation> ValidateReset(
        SubViewport viewport,
        RealtimeSliceMain slice,
        string savePath)
    {
        byte[] originalBytes = File.ReadAllBytes(savePath);
        RealtimeCampaignSaveLoadResult load = RealtimeCampaignSaveStore.Load(savePath);
        Require(load is
                {
                    Status: RealtimeCampaignSaveLoadStatus.Loaded,
                    Save: not null,
                },
            "The reset probe could not read its prior save.");
        RealtimeCampaignSave save = load.Save!;
        RealtimeSliceData data = RealtimeSliceResources.LoadNativeRelease(
            typeof(RealtimeSliceMain).Assembly,
            RealtimeNativeRouteCatalog.ProductCampaign);
        RealtimeCampaignRestoreResult restore = RealtimeCampaignSaveCodec.Restore(
            data.RequireSaveSourceIdentity(),
            data.Campaign,
            data.World,
            save);
        Require(RealtimeSession.ValidateProgressResume(data, restore).Kind ==
                    RealtimeProgressResumeKind.InProgress,
            "The reset probe requires an in-progress product save.");

        RealtimeProductTitle title = slice.UiForSmoke.ProductTitleForSmoke;
        Require(!title.ContinueButton.Disabled &&
                !title.NewGameButton.Disabled &&
                !slice.HasSessionForSmoke,
            "An in-progress save did not expose its reset action.");
        string beforeConfirmationDetail = title.DetailText;
        string[] beforeBackups = EnumerateResetBackups(savePath);

        PushPrimary(viewport, title.NewGameButton.GetGlobalRect().GetCenter());
        await SettleFrames(2);
        Require(!slice.HasSessionForSmoke &&
                !slice.OwnsProductProgressForSmoke &&
                title.Visible &&
                !title.NewGameButton.Disabled &&
                string.Equals(
                    title.ContinueButton.AccessibilityDescription,
                    "저장된 진행을 이어갑니다.",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    title.DetailText,
                    beforeConfirmationDetail,
                    StringComparison.Ordinal) &&
                File.ReadAllBytes(savePath).SequenceEqual(originalBytes) &&
                EnumerateResetBackups(savePath).SequenceEqual(beforeBackups),
            "The first reset activation did not remain a save/session-neutral confirmation.");

        using (FileStream lockedPrimary = new(
                   savePath,
                   FileMode.Open,
                   System.IO.FileAccess.Read,
                   FileShare.None))
        {
            PushPrimary(viewport, title.NewGameButton.GetGlobalRect().GetCenter());
            await SettleFrames(2);
            Require(!slice.HasSessionForSmoke &&
                    !slice.OwnsProductProgressForSmoke &&
                    title.Visible &&
                    !title.NewGameButton.Disabled &&
                    EnumerateResetBackups(savePath).SequenceEqual(beforeBackups),
                "A failed reset backup did not fail closed at the confirmation title.");
        }
        Require(File.ReadAllBytes(savePath).SequenceEqual(originalBytes),
            "A failed reset backup changed the primary save bytes.");

        PushPrimary(viewport, title.NewGameButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        string[] afterBackups = EnumerateResetBackups(savePath);
        string[] createdBackups = afterBackups.Except(
            beforeBackups,
            StringComparer.Ordinal).ToArray();
        Require(createdBackups.Length == 1,
            "The confirmed reset did not create exactly one sibling backup.");
        string backupPath = createdBackups[0];
        Require(File.ReadAllBytes(backupPath).SequenceEqual(originalBytes),
            "The reset backup is not byte-exact.");
        Require(File.ReadAllBytes(savePath).SequenceEqual(originalBytes),
            "Confirmed reset changed the primary save before normal tree exit.");
        ValidateStartedProductCampaign(slice, title);
        SaveExpectation initial = PrepareInitialBriefingProgress(slice);
        return new ResetExpectation(backupPath, originalBytes, initial);
    }

    private static string[] EnumerateResetBackups(string savePath)
    {
        string directory = Path.GetDirectoryName(savePath) ??
            throw new InvalidOperationException("The reset save has no sibling directory.");
        string fileName = Path.GetFileName(savePath);
        string absolutePrefix = Path.Combine(directory, $"{fileName}.reset-");
        return Directory.GetFiles(directory, $"{fileName}.reset-*.bak")
            .Where(path =>
            {
                if (!path.StartsWith(absolutePrefix, StringComparison.Ordinal) ||
                    !path.EndsWith(".bak", StringComparison.Ordinal))
                {
                    return false;
                }
                string suffix = path[
                    absolutePrefix.Length..^".bak".Length];
                return Guid.TryParseExact(suffix, "N", out _);
            })
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateAndDeleteResetBackup(ResetExpectation reset)
    {
        Require(File.Exists(reset.BackupPath) &&
                File.ReadAllBytes(reset.BackupPath).SequenceEqual(
                    reset.OriginalBytes),
            "The byte-exact reset backup did not survive normal tree exit.");
        File.Delete(reset.BackupPath);
        Require(!File.Exists(reset.BackupPath),
            "The verified reset smoke backup was not cleaned up.");
    }

    private static SaveExpectation PrepareInitialBriefingProgress(
        RealtimeSliceMain slice)
    {
        RealtimeCampaignSnapshot snapshot = slice.CoreSnapshot;
        RealtimeChapterStoryModalRequest story =
            slice.ActiveChapterStoryModalForSmoke ??
            throw new InvalidOperationException(
                "The initial product briefing is not active.");
        Require(IsInitialBriefing(story) &&
                snapshot.ChapterStarted &&
                snapshot.Minute == snapshot.ChapterStartMinute &&
                snapshot.CommandCount == 0 &&
                snapshot.PendingTransitions.Count == 0 &&
                snapshot.ActiveEventStates.Count == 0 &&
                snapshot.ActiveDuty is null &&
                snapshot.Construction.ActiveConstruction is null &&
                snapshot.Construction.NodeDraft is null &&
                snapshot.Construction.LineDraft is null &&
                !snapshot.CampaignComplete &&
                slice.LatestPresentation.Modal?.Id == story.ModalId &&
                slice.InteractionState is
                {
                    Simulation: RealtimeSimulationState.AutoPaused,
                    RunningSpeed: RealtimeSimulationSpeed.Normal,
                    ActiveModalId: var activeModalId,
                } &&
                activeModalId == story.ModalId,
            "New Game did not reach the exact drained initial save boundary.");
        RealtimeCampaignSave staged = slice.CaptureProgressForSmoke();
        Require(staged.SchemaVersion == RealtimeCampaignSave.SupportedSchemaVersion &&
                staged.SavedMinute == snapshot.Minute &&
                staged.Commands.Count == 0 &&
                staged.ClosedStoryCount == 0,
            "The staged initial briefing did not preserve the v3 c0 boundary.");
        slice.FreezeAutonomousClockForSmoke();
        return ExpectWrittenSave(staged);
    }

    private static SaveExpectation StageActiveFloodProgress(RealtimeSliceMain slice)
    {
        (string toolId, MapPoint position) = slice.AcceptedNodeDraftForSmoke();
        RequireAccepted(slice.ApplyIntentForSmoke(
            RealtimeR2Intent.SelectBuildTool(RealtimeTool.BuildNode, toolId)),
            "select a FIRST_LIGHT node tool");
        RequireAccepted(slice.ApplyIntentForSmoke(new RealtimeR2Intent(
            RealtimeR2IntentKind.SetNodeDraft,
            FirstId: toolId[RealtimeR2Ids.NodeToolPrefix.Length..],
            Position: position)),
            "place a FIRST_LIGHT node draft");
        RequireAccepted(slice.ApplyIntentForSmoke(new RealtimeR2Intent(
            RealtimeR2IntentKind.OrderNode)),
            "order a FIRST_LIGHT node");

        _ = slice.AdvanceToForSmoke(1320);
        Require(slice.ActiveChapterStoryModalForSmoke is
                {
                    Purpose: RealtimeChapterStoryModalPurpose.ChapterResult,
                    ChapterId: "FIRST_LIGHT",
                },
            "The product save preparation missed the FIRST_LIGHT result.");
        Require(slice.ClosePresentedStoryModalForSmoke() is not null &&
                slice.ActiveChapterStoryModalForSmoke is
                {
                    Purpose: RealtimeChapterStoryModalPurpose.ChapterBriefing,
                    ChapterId: "SECOND_HEART",
                },
            "The FIRST_LIGHT result did not open the SECOND_HEART briefing.");
        Require(slice.ClosePresentedStoryModalForSmoke() is null,
            "The SECOND_HEART briefing did not close before story save preparation.");

        _ = slice.AdvanceToForSmoke(1800);
        RealtimeCampaignSnapshot snapshot = slice.CoreSnapshot;
        RealtimeActiveEventState activeEvent = snapshot.ActiveEventStates.Single();
        RealtimeEventDutyProgress duty = snapshot.ActiveDuty ??
            throw new InvalidOperationException(
                "The FLOOD_ISOLATION_TEST event has no active duty progress.");
        RealtimeChapterStoryModalRequest story =
            slice.ActiveChapterStoryModalForSmoke ??
            throw new InvalidOperationException(
                "The FLOOD_ISOLATION_TEST story is not active.");
        Require(activeEvent.EventId == "FLOOD_ISOLATION_TEST" &&
                duty.EventId == activeEvent.EventId &&
                snapshot.PendingTransitions.Count == 0 &&
                snapshot.Construction.NodeDraft is null &&
                snapshot.Construction.LineDraft is null &&
                story is
                {
                    Purpose: RealtimeChapterStoryModalPurpose.EventStory,
                    ChapterId: "SECOND_HEART",
                    EventId: "FLOOD_ISOLATION_TEST",
                } &&
                slice.LatestPresentation.Modal?.Id == story.ModalId &&
                slice.InteractionState is
                {
                    Simulation: RealtimeSimulationState.AutoPaused,
                    ActiveModalId: var activeModalId,
                } &&
                activeModalId == story.ModalId &&
                RealtimeSession.IsJournalRestorableProgressSnapshot(snapshot),
            "The product save preparation did not reach an active FLOOD story boundary.");

        // Prevent host callbacks between this expectation and normal tree exit.
        slice.FreezeAutonomousClockForSmoke();
        return new SaveExpectation(
            slice.SliceDataForSmoke.RequireSaveSourceIdentity().RouteId,
            snapshot.Minute,
            slice.CanonicalStateSha256,
            slice.AcceptedCommandCount,
            ClosedStoryCount: 3,
            Terminal: false);
    }

    private static void ValidateWrittenSave(
        string savePath,
        SaveExpectation expected,
        byte[]? priorSaveBytes)
    {
        byte[] writtenBytes = File.ReadAllBytes(savePath);
        Require(priorSaveBytes is null ||
                !writtenBytes.SequenceEqual(priorSaveBytes),
            "Normal tree exit did not replace the prior save bytes.");
        RealtimeCampaignSaveLoadResult load = RealtimeCampaignSaveStore.Load(savePath);
        Require(load is
                {
                    Status: RealtimeCampaignSaveLoadStatus.Loaded,
                    Save: not null,
                },
            "Normal tree exit did not write a readable R2 save.");
        RealtimeCampaignSave save = load.Save!;
        Require(save.Source.RouteId == expected.RouteId &&
                save.SavedMinute == expected.Minute &&
                save.CanonicalStateSha256 == expected.CanonicalStateSha256 &&
                save.Commands.Count == expected.CommandCount &&
                save.SchemaVersion == RealtimeCampaignSave.SupportedSchemaVersion &&
                save.ClosedStoryCount == expected.ClosedStoryCount,
            "The written R2 save does not match the staged progress.");
    }

    private async Task<SaveExpectation> ValidateContinue(
        SubViewport viewport,
        RealtimeSliceMain slice,
        string savePath,
        RealtimeNativeRoute expectedRoute)
    {
        RealtimeCampaignSaveLoadResult load = RealtimeCampaignSaveStore.Load(savePath);
        Require(load is
                {
                    Status: RealtimeCampaignSaveLoadStatus.Loaded,
                    Save: not null,
                },
            "The fresh Continue process could not read the prior process save.");
        RealtimeCampaignSave save = load.Save!;
        Require(RealtimeNativeRouteCatalog.TryResolve(
                    save.Source.RouteId,
                    out RealtimeNativeRoute? route) &&
                ReferenceEquals(route, expectedRoute),
            "The saved route is unavailable in the fresh process.");
        RealtimeSliceData data = RealtimeSliceResources.LoadNativeRelease(
            typeof(RealtimeSliceMain).Assembly,
            expectedRoute);
        RealtimeCampaignRestoreResult expectedRestore =
            RealtimeCampaignSaveCodec.Restore(
                data.RequireSaveSourceIdentity(),
                data.Campaign,
                data.World,
                save);
        RealtimeProgressResumePlan expectedPlan =
            RealtimeSession.ValidateProgressResume(data, expectedRestore);
        RealtimeCampaignSnapshot expected = expectedRestore.Run.GetSnapshot();

        RealtimeUiRoot ui = slice.UiForSmoke;
        RealtimeProductTitle title = ui.ProductTitleForSmoke;
        bool completed = expectedPlan.Kind == RealtimeProgressResumeKind.Completed;
        Require(slice.LaunchForSmoke.Kind == RealtimeLaunchKind.ProductTitle &&
                !slice.HasSessionForSmoke && title.Visible,
            "A valid save bypassed the fresh product title.");
        Require(!title.ContinueButton.Disabled &&
                !title.NewGameButton.Disabled &&
                ReferenceEquals(ui.FocusOwnerForSmoke, title.ContinueButton),
            "A valid save did not expose its typed title actions and focus Continue.");
        Require(title.DetailText.Contains(
                completed
                    ? "완료"
                    : "paused",
                StringComparison.Ordinal),
            "The valid-save title did not disclose its typed resume policy.");

        PushPrimary(viewport, title.ContinueButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        Require(slice.HasSessionForSmoke && !title.Visible &&
                ui.HudSurfaceVisibleForSmoke && slice.WorldVisibleForSmoke,
            "Continue did not replace the title with restored gameplay.");
        Require(slice.LaunchForSmoke.Kind == RealtimeLaunchKind.NativeRelease &&
                ReferenceEquals(slice.LaunchForSmoke.NativeRoute, route) &&
                ReferenceEquals(slice.SliceDataForSmoke.NativeRoute, route) &&
                slice.OwnsProductProgressForSmoke,
            "Continue did not preserve the saved canonical route.");
        Require(slice.AudioForSmoke.AmbientStartCountForSmoke == 1 &&
                slice.AudioForSmoke.LiveCuePlayCountForSmoke == 0,
            "Fresh Continue restarted ambience or replayed historical SFX cues.");

        RealtimeCampaignSnapshot actual = slice.CoreSnapshot;
        Require(RealtimeStateCanonicalizer.StructuralEquals(expected, actual) &&
                actual.Minute == save.SavedMinute &&
                actual.CashUnit == expected.CashUnit &&
                slice.CanonicalStateSha256 == save.CanonicalStateSha256 &&
                slice.AcceptedCommandCount == save.Commands.Count,
            "Continue did not restore the exact clock/cash/world/journal/hash.");
        if (ReferenceEquals(expectedRoute, RealtimeNativeRouteCatalog.ProductCampaign))
        {
            if (expectedPlan.Kind == RealtimeProgressResumeKind.Completed)
            {
                return ValidateCompletedContinue(slice, actual);
            }
            RealtimeChapterStoryModalRequest story =
                slice.ActiveChapterStoryModalForSmoke ??
                throw new InvalidOperationException(
                    "Continue did not restore the active product story.");
            Require(expectedPlan.ActiveStoryModalId == story.ModalId,
                "Continue restored a different product story than the title probe.");
            if (story.Purpose == RealtimeChapterStoryModalPurpose.EventStory)
            {
                return ValidateAndStageActiveFloodContinue(
                    slice,
                    expected,
                    actual,
                    story);
            }
            else if (story.Purpose ==
                     RealtimeChapterStoryModalPurpose.ChapterResult)
            {
                return ValidateAndStageResultHandoffContinue(slice, data, story);
            }
            else if (IsInitialBriefing(story))
            {
                return ValidateAndStageInitialBriefingContinue(
                    slice,
                    expected,
                    actual,
                    story);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unexpected product Continue story purpose '{story.Purpose}'.");
            }
        }
        else
        {
            Require(expected.Construction.ActiveConstruction is not null &&
                    actual.Construction.ActiveConstruction is not null,
                "Legacy Continue lost its active construction.");
            Require(slice.InteractionState is
                    {
                        Simulation: RealtimeSimulationState.PlayerPaused,
                        RunningSpeed: RealtimeSimulationSpeed.Normal,
                        Tool: RealtimeTool.Inspect,
                        SelectionId: null,
                        ActiveModalId: null,
                        SelectedBuildToolId: null,
                    } &&
                    slice.LatestPresentation.Modal is null &&
                    slice.AccumulatorSnapshot.Paused &&
                    !slice.AccumulatorSnapshot.HasPendingTime &&
                    slice.RetainedFrameDebt.Count == 0,
                "Legacy Continue did not apply paused/no-modal/no-frame-debt policy.");
            RealtimeCampaignSave staged = slice.CaptureProgressForSmoke();
            slice.FreezeAutonomousClockForSmoke();
            return ExpectWrittenSave(staged);
        }
    }

    private static SaveExpectation ValidateCompletedContinue(
        RealtimeSliceMain slice,
        RealtimeCampaignSnapshot snapshot)
    {
        bool successfulFinal = snapshot.CompletedChapters.Count > 0 &&
            snapshot.CompletedChapters[^1].ObjectiveSatisfied;
        Require(snapshot.CampaignComplete &&
                !snapshot.ChapterStarted &&
                slice.ActiveChapterStoryModalForSmoke is null &&
                slice.ActiveEpilogueModalForSmoke is null &&
                (successfulFinal
                    ? slice.EpilogueStartedForSmoke && slice.EpilogueCompletedForSmoke
                    : !slice.EpilogueStartedForSmoke && !slice.EpilogueCompletedForSmoke) &&
                slice.LatestPresentation.Modal is null &&
                slice.InteractionState is
                {
                    Simulation: RealtimeSimulationState.Ended,
                    Tool: RealtimeTool.Inspect,
                    Surface: RealtimeSurface.World,
                    PauseReason: RealtimePauseReason.CampaignResult,
                    ActiveModalId: null,
                    SelectedBuildToolId: null,
                } &&
                slice.AccumulatorSnapshot.Paused &&
                !slice.AccumulatorSnapshot.HasPendingTime &&
                slice.RetainedFrameDebt.Count == 0,
            "Continue did not restore the exact terminal read-only product world.");
        RealtimeCampaignSave staged = slice.CaptureProgressForSmoke();
        Require(staged.SchemaVersion == RealtimeCampaignSave.SupportedSchemaVersion &&
                staged.SavedMinute == snapshot.Minute &&
                staged.CanonicalStateSha256 == slice.CanonicalStateSha256 &&
                staged.Commands.Count == slice.AcceptedCommandCount &&
                staged.ClosedStoryCount is > 0,
            "The restored terminal world could not reproduce its current save.");
        slice.FreezeAutonomousClockForSmoke();
        return ExpectWrittenSave(staged, terminal: true);
    }

    private static SaveExpectation ValidateAndStageInitialBriefingContinue(
        RealtimeSliceMain slice,
        RealtimeCampaignSnapshot expected,
        RealtimeCampaignSnapshot actual,
        RealtimeChapterStoryModalRequest story)
    {
        Require(IsInitialBriefing(story) &&
                expected.CommandCount == 0 &&
                actual.CommandCount == 0 &&
                actual.Minute == actual.ChapterStartMinute &&
                actual.PendingTransitions.Count == 0 &&
                slice.InteractionState is
                {
                    Simulation: RealtimeSimulationState.AutoPaused,
                    RunningSpeed: RealtimeSimulationSpeed.Normal,
                    ActiveModalId: var activeModalId,
                } &&
                activeModalId == story.ModalId &&
                slice.LatestPresentation.Modal?.Id == story.ModalId &&
                slice.AccumulatorSnapshot.Paused &&
                !slice.AccumulatorSnapshot.HasPendingTime &&
                slice.RetainedFrameDebt.Count == 0,
            "Continue did not restore the same drained initial briefing boundary.");
        string beforeCloseHash = slice.CanonicalStateSha256;
        int beforeCloseCommands = slice.AcceptedCommandCount;
        RealtimeTransition[] beforeCloseHistory = slice.EmittedTransitions.ToArray();
        Require(slice.ClosePresentedStoryModalForSmoke() is null &&
                slice.ActiveChapterStoryModalForSmoke is null &&
                slice.LatestPresentation.Modal is null &&
                slice.CanonicalStateSha256 == beforeCloseHash &&
                slice.AcceptedCommandCount == beforeCloseCommands &&
                slice.EmittedTransitions.SequenceEqual(beforeCloseHistory) &&
                slice.InteractionState is
                {
                    Simulation: RealtimeSimulationState.PlayerPaused,
                    RunningSpeed: RealtimeSimulationSpeed.Normal,
                    Tool: RealtimeTool.Inspect,
                    SelectionId: null,
                    ActiveModalId: null,
                    SelectedBuildToolId: null,
                },
            "Closing the restored initial briefing did not return to paused product play.");
        return StageActiveFloodProgress(slice);
    }

    private static bool IsInitialBriefing(
        RealtimeChapterStoryModalRequest story) => story is
        {
            ModalId: RealtimeR2Ids.ChapterBriefingModal,
            Purpose: RealtimeChapterStoryModalPurpose.ChapterBriefing,
            ChapterId: RealtimeCampaignOverlayLoader.FirstReleaseChapterId,
            EventId: null,
        };

    private static SaveExpectation ValidateAndStageActiveFloodContinue(
        RealtimeSliceMain slice,
        RealtimeCampaignSnapshot expected,
        RealtimeCampaignSnapshot actual,
        RealtimeChapterStoryModalRequest story)
    {
        RealtimeActiveEventState expectedEvent = expected.ActiveEventStates.Single();
        Require(actual.ActiveEventStates.Single().EventId == expectedEvent.EventId &&
                actual.ActiveDuty is
                {
                    EventId: var actualDutyEventId,
                } &&
                actualDutyEventId == expectedEvent.EventId,
            "Continue lost active product event/duty progress.");
        Require(story is
                {
                    Purpose: RealtimeChapterStoryModalPurpose.EventStory,
                    ChapterId: "SECOND_HEART",
                    EventId: "FLOOD_ISOLATION_TEST",
                } &&
                slice.InteractionState is
                {
                    Simulation: RealtimeSimulationState.AutoPaused,
                    RunningSpeed: RealtimeSimulationSpeed.Normal,
                    ActiveModalId: var activeModalId,
                } &&
                activeModalId == story.ModalId &&
                slice.LatestPresentation.Modal?.Id == story.ModalId &&
                slice.AccumulatorSnapshot.Paused &&
                !slice.AccumulatorSnapshot.HasPendingTime &&
                slice.RetainedFrameDebt.Count == 0,
            "Continue did not restore the same paused active product story.");
        string beforeCloseHash = slice.CanonicalStateSha256;
        Require(slice.ClosePresentedStoryModalForSmoke() is null &&
                slice.ActiveChapterStoryModalForSmoke is null &&
                slice.LatestPresentation.Modal is null &&
                slice.CanonicalStateSha256 == beforeCloseHash &&
                slice.InteractionState is
                {
                    Simulation: RealtimeSimulationState.PlayerPaused,
                    RunningSpeed: RealtimeSimulationSpeed.Normal,
                    Tool: RealtimeTool.Inspect,
                    SelectionId: null,
                    ActiveModalId: null,
                    SelectedBuildToolId: null,
                },
            "Closing the restored story did not return to paused product play.");

        _ = slice.AdvanceToForSmoke(1860);
        Require(slice.ActiveChapterStoryModalForSmoke is
                {
                    Purpose: RealtimeChapterStoryModalPurpose.ChapterResult,
                    ChapterId: "SECOND_HEART",
                    FinalResult: false,
                } result &&
                slice.LatestPresentation.Modal?.Id == result.ModalId,
            "The active-story Continue did not stage the SECOND_HEART result.");
        RealtimeCampaignSave staged = slice.CaptureProgressForSmoke();
        Require(staged.SchemaVersion == RealtimeCampaignSave.SupportedSchemaVersion &&
                staged.SavedMinute == 1860 &&
                staged.ClosedStoryCount == 4,
            "The staged result did not preserve the v3 closed prefix.");
        slice.FreezeAutonomousClockForSmoke();
        return ExpectWrittenSave(staged);
    }

    private static SaveExpectation ValidateAndStageResultHandoffContinue(
        RealtimeSliceMain slice,
        RealtimeSliceData data,
        RealtimeChapterStoryModalRequest story)
    {
        Require(story is
                {
                    Purpose: RealtimeChapterStoryModalPurpose.ChapterResult,
                    ChapterId: "SECOND_HEART",
                    FinalResult: false,
                } &&
                slice.InteractionState is
                {
                    Simulation: RealtimeSimulationState.AutoPaused,
                    RunningSpeed: RealtimeSimulationSpeed.Normal,
                    Surface: RealtimeSurface.BlockingModal,
                    ActiveModalKind: RealtimeModalKind.Story,
                    PauseReason: RealtimePauseReason.CampaignResult,
                    ActiveModalId: var resultModalId,
                } &&
                resultModalId == story.ModalId &&
                slice.LatestPresentation.Modal?.Id == story.ModalId,
            "Continue did not restore the same active SECOND_HEART result.");
        string beforeCloseHash = slice.CanonicalStateSha256;
        int beforeCloseCommands = slice.AcceptedCommandCount;
        RealtimeTransition[] beforeCloseHistory = slice.EmittedTransitions.ToArray();
        Require(slice.ClosePresentedStoryModalForSmoke() is not null &&
                slice.CanonicalStateSha256 == beforeCloseHash &&
                slice.AcceptedCommandCount == beforeCloseCommands &&
                slice.EmittedTransitions.SequenceEqual(beforeCloseHistory) &&
                slice.ActiveChapterStoryModalForSmoke is
                {
                    Purpose: RealtimeChapterStoryModalPurpose.ChapterBriefing,
                    ChapterId: "SECOND_SOURCE",
                } briefing &&
                slice.LatestPresentation.Modal?.Id == briefing.ModalId,
            "Closing the restored result did not open the queued briefing exactly once.");
        CommercialStoryCard authored = data.BaseCampaign.Chapters.Single(item =>
            string.Equals(
                item.ChapterId,
                "SECOND_SOURCE",
                StringComparison.Ordinal)).Briefing;
        RealtimeModalPresentation modal = slice.LatestPresentation.Modal!;
        Require(modal.Eyebrow == authored.Speaker &&
                modal.Heading == authored.Title &&
                modal.Body == authored.Body &&
                slice.EmittedTransitions.Count(item =>
                    item.Kind == RealtimeTransitionKind.ChapterStarted &&
                    item.ChapterId == "SECOND_SOURCE") == 1,
            "The queued SECOND_SOURCE briefing drifted or replayed.");
        RealtimeCampaignSave staged = slice.CaptureProgressForSmoke();
        Require(staged.SchemaVersion == RealtimeCampaignSave.SupportedSchemaVersion &&
                staged.SavedMinute == 1860 &&
                staged.ClosedStoryCount == 5,
            "The staged briefing did not preserve the v3 closed prefix.");
        slice.FreezeAutonomousClockForSmoke();
        return ExpectWrittenSave(staged);
    }

    private static SaveExpectation ExpectWrittenSave(
        RealtimeCampaignSave save,
        bool terminal = false) =>
        new(
            save.Source.RouteId,
            save.SavedMinute,
            save.CanonicalStateSha256,
            save.Commands.Count,
            save.ClosedStoryCount ?? throw new InvalidOperationException(
                "A staged current save omitted its required story cursor."),
            terminal);

    private static void RequireAccepted(
        RealtimeR2IntentResult result,
        string operation)
    {
        Require(result.Accepted, $"Unable to {operation}: {result.Error}");
    }

    private async Task ValidateBlockedSaveTitle(
        SubViewport viewport,
        RealtimeSliceMain slice,
        EntryMode mode,
        string savePath)
    {
        RealtimeUiRoot ui = slice.UiForSmoke;
        RealtimeProductTitle title = ui.ProductTitleForSmoke;
        Require(slice.LaunchForSmoke.Kind == RealtimeLaunchKind.ProductTitle &&
                !slice.HasSessionForSmoke && title.Visible,
            "A blocked save bypassed the product title.");
        bool resetEligible = mode is EntryMode.InvalidSave or EntryMode.UnsupportedSave;
        Require(title.ContinueButton.Disabled &&
                title.NewGameButton.Disabled == !resetEligible,
            "A guarded save exposed the wrong title actions.");
        string expectedReason = mode switch
        {
            EntryMode.InvalidSave => "손상",
            EntryMode.UnsupportedSave => "지원하지 않는",
            EntryMode.IoFailureSave => "읽을 수 없습니다",
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        Require(title.DetailText.Contains(expectedReason, StringComparison.Ordinal) &&
                string.Equals(
                    title.ContinueButton.AccessibilityDescription,
                    title.DetailText,
                    StringComparison.Ordinal) &&
                (resetEligible
                    ? title.NewGameButton.AccessibilityDescription.Contains(
                            "백업",
                            StringComparison.Ordinal) &&
                        title.NewGameButton.AccessibilityDescription.Contains(
                            "확인",
                            StringComparison.Ordinal)
                    : string.Equals(
                        title.NewGameButton.AccessibilityDescription,
                        title.DetailText,
                        StringComparison.Ordinal)),
            "A blocked save has no matching visible/accessibility reason.");
        Require(ui.InputRouterForSmoke.ActiveOwner == "product_title" &&
                ui.InputRouterForSmoke.ActivePriority ==
                    RealtimeInputPriority.BlockingModal,
            "A blocked-save title does not own input.");

        RequireUnavailableTitleActionRejected(
            slice.RequestContinueForSmoke,
            slice,
            "A blocked save accepted stale Continue input.");
        PushPrimary(viewport, title.ContinueButton.GetGlobalRect().GetCenter());
        if (!resetEligible)
        {
            RequireUnavailableTitleActionRejected(
                slice.RequestNewGameForSmoke,
                slice,
                "An I/O-failure save accepted stale New Game input.");
            PushPrimary(viewport, title.NewGameButton.GetGlobalRect().GetCenter());
            await SettleFrames(2);
            Require(!slice.HasSessionForSmoke && title.Visible,
                "A disabled I/O-failure action started a session.");
            return;
        }

        byte[] originalBytes = File.ReadAllBytes(savePath);
        string initialDetail = title.DetailText;
        string[] beforeBackups = EnumerateResetBackups(savePath);
        PushPrimary(viewport, title.NewGameButton.GetGlobalRect().GetCenter());
        await SettleFrames(2);
        Require(!slice.HasSessionForSmoke &&
                !slice.OwnsProductProgressForSmoke &&
                title.Visible &&
                !title.NewGameButton.Disabled &&
                !string.Equals(title.DetailText, initialDetail, StringComparison.Ordinal) &&
                File.ReadAllBytes(savePath).SequenceEqual(originalBytes) &&
                EnumerateResetBackups(savePath).SequenceEqual(beforeBackups),
            "A guarded-save first reset activation was not confirmation-only.");
    }

    private static void RequireUnavailableTitleActionRejected(
        Action action,
        RealtimeSliceMain slice,
        string failure)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected &&
                !slice.HasSessionForSmoke &&
                !slice.OwnsProductProgressForSmoke,
            failure);
    }

    private async Task ValidateTechnicalFixture(
        SubViewport viewport,
        RealtimeSliceMain slice,
        string settingsPath)
    {
        RealtimeUiRoot ui = slice.UiForSmoke;
        Require(slice.HasSessionForSmoke &&
                slice.LaunchForSmoke.Kind == RealtimeLaunchKind.TechnicalFixture &&
                slice.SliceDataForSmoke.NativeRoute is null,
            "Explicit technical fixture did not bootstrap its isolated session.");
        Require(!ui.ProductTitleForSmoke.Visible &&
                ui.HudSurfaceVisibleForSmoke && slice.WorldVisibleForSmoke,
            "Explicit technical fixture did not bypass the product title.");
        Require(slice.ClosePresentedStoryModalForSmoke() is null,
            "Explicit technical fixture could not close its initial briefing.");
        await SettleFrames(4);

        byte[] sentinel = File.ReadAllBytes(settingsPath);
        PushPrimary(
            viewport,
            ui.TopHudForSmoke.SettingsButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        RealtimeSettingsSurface surface = ui.SettingsSurface;
        Require(ui.SettingsVisible &&
                surface.Visible &&
                slice.ActiveSettingsJourneyForSmoke ==
                    RealtimeSettingsJourney.Gameplay &&
                ui.InputRouterForSmoke.ActiveOwner == "product_settings" &&
                ui.InputRouterForSmoke.ActivePriority ==
                    RealtimeInputPriority.BlockingModal,
            "Explicit technical fixture settings did not own blocking input.");
        Require(surface.WindowModeOption.Disabled &&
                surface.UiScaleOption.Disabled &&
                surface.MasterVolumeOption.Disabled &&
                surface.AmbientVolumeOption.Disabled &&
                surface.SfxVolumeOption.Disabled &&
                surface.ReduceMotionCheck.Disabled &&
                surface.ApplyButton.Disabled &&
                surface.StatusText.Contains("읽거나 쓰지", StringComparison.Ordinal) &&
                File.ReadAllBytes(settingsPath).SequenceEqual(sentinel),
            "Explicit technical fixture did not expose read-only product settings.");
        Require(ReferenceEquals(ui.FocusOwnerForSmoke, surface.CloseButton),
            "Explicit technical fixture settings did not focus the available Close action.");
        PushPrimary(viewport, surface.CloseButton.GetGlobalRect().GetCenter());
        await SettleFrames(4);
        Require(!ui.SettingsVisible &&
                ReferenceEquals(
                    ui.FocusOwnerForSmoke,
                    ui.TopHudForSmoke.SettingsButton) &&
                File.ReadAllBytes(settingsPath).SequenceEqual(sentinel),
            "Explicit technical fixture settings changed bytes or lost opener focus.");
    }

    private static void ValidateAudioSceneWiring(RealtimeSliceMain slice)
    {
        RealtimeAudio audio = slice.AudioForSmoke;
        AudioStreamWav ambient = audio.AmbientPlayerForSmoke.Stream as AudioStreamWav ??
            throw new InvalidOperationException(
                "Realtime ambient player has no generated PCM stream.");
        Require(audio.AmbientStartCountForSmoke == 1 &&
                audio.LiveCuePlayCountForSmoke == 0 &&
                audio.LastLiveCueForSmoke is null &&
                string.Equals(audio.AmbientPlayerForSmoke.Bus, "Ambient",
                    StringComparison.Ordinal) &&
                string.Equals(audio.SfxPlayerForSmoke.Bus, "SFX",
                    StringComparison.Ordinal) &&
                audio.SfxPlayerForSmoke.Stream is null &&
                ambient.Format == AudioStreamWav.FormatEnum.Format16Bits &&
                ambient.MixRate == 22_050 &&
                !ambient.Stereo &&
                ambient.LoopMode == AudioStreamWav.LoopModeEnum.Forward &&
                ambient.LoopBegin == 0 &&
                ambient.LoopEnd > 0 &&
                ambient.Data.Length == checked(ambient.LoopEnd * sizeof(short)),
            "Actual product scene did not start the generated Ambient PCM loop once.");

        foreach (RealtimeLiveAudioCue cue in Enum.GetValues<RealtimeLiveAudioCue>())
        {
            AudioStreamWav stream = audio.StreamForSmoke(cue);
            Require(stream.Format == AudioStreamWav.FormatEnum.Format16Bits &&
                    stream.MixRate == 22_050 &&
                    !stream.Stereo &&
                    stream.LoopMode == AudioStreamWav.LoopModeEnum.Disabled &&
                    stream.LoopBegin == 0 &&
                    stream.LoopEnd == 0 &&
                    stream.Data.Length > 0 &&
                    stream.Data.Length % sizeof(short) == 0,
                $"Generated {cue} SFX stream shape or loop policy drifted.");
        }
    }

    private static void ValidateExplicitNativeRoutes()
    {
        RealtimeNativeRoute[] expected =
        [
            RealtimeNativeRouteCatalog.FirstLight,
            RealtimeNativeRouteCatalog.TutorialThroughSecondSource,
            RealtimeNativeRouteCatalog.ProductCampaign,
        ];
        Require(RealtimeNativeRouteCatalog.All.Count == expected.Length &&
                RealtimeNativeRouteCatalog.SupportsProductContinuation(
                    RealtimeNativeRouteCatalog.ProductCampaign) &&
                RealtimeNativeRouteCatalog.SupportsProductContinuation(
                    RealtimeNativeRouteCatalog.FirstLight) &&
                !RealtimeNativeRouteCatalog.SupportsProductContinuation(
                    RealtimeNativeRouteCatalog.TutorialThroughSecondSource),
            "Native route catalog or product-continuation policy drifted.");
        foreach (RealtimeNativeRoute route in expected)
        {
            RealtimeLaunchSelection launch = RealtimeSliceMain.ParseLaunchArguments(
                [route.LaunchArgument]);
            Require(launch.Kind == RealtimeLaunchKind.NativeRelease &&
                    ReferenceEquals(launch.NativeRoute, route),
                $"Explicit native argument was not preserved: {route.LaunchArgument}");
            RealtimeSliceData data = RealtimeSliceResources.LoadNativeRelease(
                typeof(RealtimeSliceMain).Assembly,
                route);
            Require(ReferenceEquals(data.NativeRoute, route) &&
                    data.Campaign.Chapters.Count == route.SelectedChapterCount &&
                    data.Campaign.Chapters[^1].Content.ChapterId == route.EndChapterId,
                $"Explicit native route did not load its exact prefix: {route.LaunchArgument}");
        }
    }

    private static void PushPrimary(SubViewport viewport, Vector2 point)
    {
        viewport.PushInput(new InputEventMouseMotion
        {
            Position = point,
            GlobalPosition = point,
        }, inLocalCoords: true);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = MouseButton.Left,
            ButtonMask = MouseButtonMask.Left,
            Pressed = true,
        }, inLocalCoords: true);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = MouseButton.Left,
            ButtonMask = (MouseButtonMask)0,
            Pressed = false,
        }, inLocalCoords: true);
    }

    private static void PushKey(SubViewport viewport, Key key, bool pressed)
    {
        viewport.PushInput(new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = pressed,
        }, inLocalCoords: true);
    }

    private async Task SettleFrames(int count)
    {
        for (int index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private void ScheduleQuit(int exitCode)
    {
        SceneTree tree = GetTree();
        int remainingFrames = 3;
        void DrainAndQuit()
        {
            remainingFrames--;
            if (remainingFrames > 0)
            {
                return;
            }
            tree.ProcessFrame -= DrainAndQuit;
            tree.Quit(exitCode);
        }
        tree.ProcessFrame += DrainAndQuit;
    }
}
#endif
