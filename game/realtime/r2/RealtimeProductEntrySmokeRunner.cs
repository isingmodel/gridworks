#if DEBUG
using System;
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

    public override void _Ready() => _ = RunAsync();

    private async Task RunAsync()
    {
        int exitCode = 1;
        SubViewport? viewport = null;
        try
        {
            string[] arguments = OS.GetCmdlineUserArgs();
            bool fixture = arguments.Length == 1 && string.Equals(
                arguments[0],
                RealtimeLaunchCatalog.TechnicalFixtureArgument,
                StringComparison.Ordinal);
            if (arguments.Length != (fixture ? 1 : 0))
            {
                throw new ArgumentException(
                    "Product-entry smoke accepts no user argument or exactly " +
                    $"{RealtimeLaunchCatalog.TechnicalFixtureArgument}.");
            }

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
            viewport.AddChild(slice);
            await SettleFrames(4);

            if (fixture)
            {
                ValidateTechnicalFixture(slice);
                GD.Print("REALTIME_PRODUCT_ENTRY_FIXTURE_PASS");
            }
            else
            {
                await ValidateProductTitle(viewport, slice);
                GD.Print("REALTIME_PRODUCT_ENTRY_TITLE_PASS");
            }
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
        ScheduleQuit(exitCode);
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
                    RealtimeNativeRouteCatalog.FirstLight) &&
                ReferenceEquals(
                    slice.SliceDataForSmoke.NativeRoute,
                    RealtimeNativeRouteCatalog.FirstLight),
            "New Game did not select the canonical FIRST_LIGHT native route.");

        RealtimeSliceData data = slice.SliceDataForSmoke;
        CommercialCampaignChapterDefinition authored = data.BaseCampaign.Chapters.Single(
            item => string.Equals(
                item.ChapterId,
                RealtimeCampaignOverlayLoader.FirstReleaseChapterId,
                StringComparison.Ordinal));
        RealtimeModalPresentation briefing = slice.LatestPresentation.Modal ??
            throw new InvalidOperationException(
                "New Game did not show the FIRST_LIGHT briefing.");
        Require(data.Campaign.Chapters.Count == 1 &&
                data.Campaign.Chapters[0].Content.ChapterId ==
                    RealtimeCampaignOverlayLoader.FirstReleaseChapterId &&
                briefing.Id == RealtimeR2Ids.ChapterBriefingModal &&
                briefing.Eyebrow == authored.Briefing.Speaker &&
                briefing.Heading == authored.Briefing.Title &&
                briefing.Body == authored.Briefing.Body,
            "New Game briefing is not the exact authored FIRST_LIGHT card.");
        Require(briefing.PrimaryAction.Id == RealtimeR2Ids.BriefingContinueAction &&
                briefing.PrimaryAction.Label == "도시 운영 시작" &&
                briefing.PrimaryAction.Label != title.ContinueButton.Text,
            "Story continue was confused with title Continue.");
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
