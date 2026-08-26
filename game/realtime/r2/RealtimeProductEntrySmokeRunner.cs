#if DEBUG
using System;
using System.IO;
using System.Linq;
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
    private const string SaveContinuePrefix = "--save-continue=";
    private const string SaveLegacyContinuePrefix = "--save-legacy-continue=";
    private const string SaveInvalidPrefix = "--save-invalid=";
    private const string SaveUnsupportedPrefix = "--save-unsupported=";
    private const string SaveIoFailurePrefix = "--save-io-failure=";

    private enum EntryMode
    {
        ProductTitle,
        TechnicalFixture,
        CreateSave,
        ContinueSave,
        LegacyContinueSave,
        InvalidSave,
        UnsupportedSave,
        IoFailureSave,
    }

    private sealed record EntryRequest(
        EntryMode Mode,
        string? SavePath,
        bool DeleteSaveAfterRun);

    private sealed record SaveExpectation(
        string RouteId,
        long Minute,
        string CanonicalStateSha256,
        int CommandCount,
        int ClosedStoryCount);

    public override void _Ready() => _ = RunAsync();

    private async Task RunAsync()
    {
        int exitCode = 1;
        SubViewport? viewport = null;
        EntryRequest? request = null;
        byte[]? guardedBytes = null;
        try
        {
            request = ParseRequest(OS.GetCmdlineUserArgs());
            guardedBytes = PrepareSaveFixture(request);

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
            if (request.Mode != EntryMode.TechnicalFixture)
            {
                slice.UseProductTitleLaunchForSmoke();
                slice.SetSavePathOverrideForSmoke(request.SavePath!);
            }
            viewport.AddChild(slice);
            await SettleFrames(4);

            SaveExpectation? created = null;
            switch (request.Mode)
            {
                case EntryMode.TechnicalFixture:
                    ValidateTechnicalFixture(slice);
                    break;
                case EntryMode.ProductTitle:
                    ValidateExplicitNativeRoutes();
                    await ValidateProductTitle(viewport, slice);
                    break;
                case EntryMode.CreateSave:
                    await ValidateProductTitle(viewport, slice);
                    created = PrepareActiveStoryProgress(slice);
                    break;
                case EntryMode.ContinueSave:
                    await ValidateContinue(
                        viewport,
                        slice,
                        request.SavePath!,
                        RealtimeNativeRouteCatalog.ProductCampaign);
                    break;
                case EntryMode.LegacyContinueSave:
                    await ValidateContinue(
                        viewport,
                        slice,
                        request.SavePath!,
                        RealtimeNativeRouteCatalog.FirstLight);
                    break;
                case EntryMode.InvalidSave:
                case EntryMode.UnsupportedSave:
                case EntryMode.IoFailureSave:
                    await ValidateBlockedSaveTitle(viewport, slice, request.Mode);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            viewport.QueueFree();
            await SettleFrames(2);
            viewport = null;
            if (created is not null)
            {
                ValidateWrittenSave(request.SavePath!, created);
            }
            ValidateGuardedSavePreserved(request, guardedBytes);

            GD.Print(request.Mode switch
            {
                EntryMode.TechnicalFixture =>
                    "REALTIME_PRODUCT_ENTRY_FIXTURE_PASS",
                EntryMode.ProductTitle =>
                    "REALTIME_PRODUCT_ENTRY_TITLE_PASS",
                EntryMode.CreateSave =>
                    "REALTIME_PRODUCT_ENTRY_SAVE_CREATE_PASS",
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
        ScheduleQuit(exitCode);
    }

    private static EntryRequest ParseRequest(string[] arguments)
    {
        if (arguments.Length == 0)
        {
            return new EntryRequest(
                EntryMode.ProductTitle,
                Path.Combine(
                    Path.GetTempPath(),
                    $"gridworks-product-entry-{Guid.NewGuid():N}.json"),
                DeleteSaveAfterRun: true);
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
            return new EntryRequest(EntryMode.TechnicalFixture, null, false);
        }

        EntryMode mode;
        string path;
        if (arguments[0].StartsWith(SaveCreatePrefix, StringComparison.Ordinal))
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
            throw new ArgumentException("The smoke save path must be absolute.");
        }
        return new EntryRequest(mode, path, DeleteSaveAfterRun: false);
    }

    private static byte[]? PrepareSaveFixture(EntryRequest request)
    {
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
            ? "{\"schemaVersion\":\"gridworks.realtime.campaign-save.v3\"}"u8.ToArray()
            : "{\"broken\":true}"u8.ToArray();
        File.WriteAllBytes(path, bytes);
        return bytes;
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
        RealtimeCampaignSaveStore.Save(
            path,
            RealtimeCampaignSaveCodec.CaptureLegacyV1(
                data.RequireSaveSourceIdentity(),
                data.Campaign,
                data.World,
                run));
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

        RealtimeSliceData data = slice.SliceDataForSmoke;
        CommercialCampaignChapterDefinition authored = data.BaseCampaign.Chapters.Single(
            item => string.Equals(
                item.ChapterId,
                RealtimeCampaignOverlayLoader.FirstReleaseChapterId,
                StringComparison.Ordinal));
        RealtimeModalPresentation briefing = slice.LatestPresentation.Modal ??
            throw new InvalidOperationException(
                "New Game did not show the FIRST_LIGHT briefing.");
        Require(data.Campaign.Chapters.Count ==
                    RealtimeNativeRouteCatalog.ProductCampaign.SelectedChapterCount &&
                data.Campaign.Chapters[0].Content.ChapterId ==
                    RealtimeCampaignOverlayLoader.FirstReleaseChapterId &&
                data.Campaign.Chapters[^1].Content.ChapterId ==
                    RealtimeNativeRouteCatalog.NativeThroughChapterId &&
                briefing.Id == RealtimeR2Ids.ChapterBriefingModal &&
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

    private static SaveExpectation PrepareActiveStoryProgress(RealtimeSliceMain slice)
    {
        Require(slice.ClosePresentedStoryModalForSmoke() is null,
            "FIRST_LIGHT briefing did not close before save preparation.");

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
            ClosedStoryCount: 2);
    }

    private static void ValidateWrittenSave(
        string savePath,
        SaveExpectation expected)
    {
        RealtimeCampaignSaveLoadResult load = RealtimeCampaignSaveStore.Load(savePath);
        Require(load is
                {
                    Status: RealtimeCampaignSaveLoadStatus.Loaded,
                    Save: not null,
                },
            "Normal tree exit did not write a readable R2 save.");
        RealtimeCampaignSave save = load.Save!;
        Require(save.Source.RouteId == expected.RouteId &&
                save.Source.RouteId ==
                    RealtimeNativeRouteCatalog.ProductCampaign.LaunchArgument &&
                save.SavedMinute == expected.Minute &&
                save.CanonicalStateSha256 == expected.CanonicalStateSha256 &&
                save.Commands.Count == expected.CommandCount &&
                save.SchemaVersion == RealtimeCampaignSave.SupportedSchemaVersion &&
                save.ClosedStoryCount == expected.ClosedStoryCount,
            "The written R2 save does not match the product active-story boundary.");
    }

    private async Task ValidateContinue(
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
        RealtimeCampaignSnapshot expected = expectedRestore.Run.GetSnapshot();

        RealtimeUiRoot ui = slice.UiForSmoke;
        RealtimeProductTitle title = ui.ProductTitleForSmoke;
        Require(slice.LaunchForSmoke.Kind == RealtimeLaunchKind.ProductTitle &&
                !slice.HasSessionForSmoke && title.Visible,
            "A valid save bypassed the fresh product title.");
        Require(!title.ContinueButton.Disabled && title.NewGameButton.Disabled &&
                ReferenceEquals(ui.FocusOwnerForSmoke, title.ContinueButton),
            "A valid save did not exclusively enable and focus Continue.");
        Require(title.DetailText.Contains("paused", StringComparison.Ordinal),
            "The valid-save title did not disclose the resume pause policy.");
        RequireUnavailableTitleActionRejected(
            slice.RequestNewGameForSmoke,
            slice,
            "A valid save accepted stale New Game input.");

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

        RealtimeCampaignSnapshot actual = slice.CoreSnapshot;
        Require(RealtimeStateCanonicalizer.StructuralEquals(expected, actual) &&
                actual.Minute == save.SavedMinute &&
                actual.CashUnit == expected.CashUnit &&
                slice.CanonicalStateSha256 == save.CanonicalStateSha256 &&
                slice.AcceptedCommandCount == save.Commands.Count,
            "Continue did not restore the exact clock/cash/world/journal/hash.");
        if (ReferenceEquals(expectedRoute, RealtimeNativeRouteCatalog.ProductCampaign))
        {
            RealtimeActiveEventState expectedEvent = expected.ActiveEventStates.Single();
            Require(actual.ActiveEventStates.Single().EventId == expectedEvent.EventId &&
                    actual.ActiveDuty is
                    {
                        EventId: var actualDutyEventId,
                    } &&
                    actualDutyEventId == expectedEvent.EventId,
                "Continue lost active product event/duty progress.");
            RealtimeChapterStoryModalRequest story =
                slice.ActiveChapterStoryModalForSmoke ??
                throw new InvalidOperationException(
                    "Continue did not restore the active product story.");
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
        }
    }

    private static void RequireAccepted(
        RealtimeR2IntentResult result,
        string operation)
    {
        Require(result.Accepted, $"Unable to {operation}: {result.Error}");
    }

    private async Task ValidateBlockedSaveTitle(
        SubViewport viewport,
        RealtimeSliceMain slice,
        EntryMode mode)
    {
        RealtimeUiRoot ui = slice.UiForSmoke;
        RealtimeProductTitle title = ui.ProductTitleForSmoke;
        Require(slice.LaunchForSmoke.Kind == RealtimeLaunchKind.ProductTitle &&
                !slice.HasSessionForSmoke && title.Visible,
            "A blocked save bypassed the product title.");
        Require(title.ContinueButton.Disabled && title.NewGameButton.Disabled,
            "A blocked save left a destructive title action enabled.");
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
                string.Equals(
                    title.NewGameButton.AccessibilityDescription,
                    title.DetailText,
                    StringComparison.Ordinal),
            "A blocked save has no matching visible/accessibility reason.");
        Require(ui.InputRouterForSmoke.ActiveOwner == "product_title" &&
                ui.InputRouterForSmoke.ActivePriority ==
                    RealtimeInputPriority.BlockingModal,
            "A blocked-save title does not own input.");

        RequireUnavailableTitleActionRejected(
            slice.RequestContinueForSmoke,
            slice,
            "A blocked save accepted stale Continue input.");
        RequireUnavailableTitleActionRejected(
            slice.RequestNewGameForSmoke,
            slice,
            "A blocked save accepted stale New Game input.");

        PushPrimary(viewport, title.ContinueButton.GetGlobalRect().GetCenter());
        PushPrimary(viewport, title.NewGameButton.GetGlobalRect().GetCenter());
        await SettleFrames(2);
        Require(!slice.HasSessionForSmoke && title.Visible,
            "A disabled blocked-save action started a session.");
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

    private static void ValidateTechnicalFixture(RealtimeSliceMain slice)
    {
        RealtimeUiRoot ui = slice.UiForSmoke;
        Require(slice.HasSessionForSmoke &&
                slice.LaunchForSmoke.Kind == RealtimeLaunchKind.TechnicalFixture &&
                slice.SliceDataForSmoke.NativeRoute is null,
            "Explicit technical fixture did not bootstrap its isolated session.");
        Require(!ui.ProductTitleForSmoke.Visible &&
                ui.HudSurfaceVisibleForSmoke && slice.WorldVisibleForSmoke,
            "Explicit technical fixture did not bypass the product title.");
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
