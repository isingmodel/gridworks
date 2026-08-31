#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.R2;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.UI;

internal sealed partial class RealtimeUiLayoutHarness : Control
{
    private async Task ValidateNativeWindowRoundTrip(
        ICollection<string> failures)
    {
        if (string.Equals(
                DisplayServer.GetName(),
                "headless",
                StringComparison.OrdinalIgnoreCase))
        {
            GD.Print(
                "REALTIME_R2_NATIVE_WINDOW_ROUNDTRIP_PENDING headless; " +
                "FHD@100/FHD@200 windowed-fullscreen-windowed and hardware focus " +
                "delivery were not claimed");
            return;
        }

        Window window = GetWindow();
        Window.ModeEnum originalMode = window.Mode;
        Vector2I originalSize = window.Size;
        Vector2I originalPosition = window.Position;
        Vector2I originalContentScaleSize = window.ContentScaleSize;
        Window.ContentScaleModeEnum originalContentScaleMode = window.ContentScaleMode;
        Window.ContentScaleAspectEnum originalContentScaleAspect =
            window.ContentScaleAspect;
        Control? originalFocus = GetViewport().GuiGetFocusOwner();
        int originalStandaloneUiScale = _uiRoot.UiScalePercent;
        int failureCountBefore = failures.Count;
        RealtimeSliceMain? slice = null;
        _uiRoot.SetLayersVisibleForSmoke(false);
        try
        {
            PackedScene scene = GD.Load<PackedScene>(
                "res://realtime/r2/RealtimeSliceMain.tscn");
            slice = scene.Instantiate<RealtimeSliceMain>();
            slice.UseTechnicalFixtureLaunchForSmoke();
            AddChild(slice);
            await SettleNativeWindow();
            RealtimeUiRoot ui = slice.UiForSmoke;
            RealtimeWorldMap map = slice.MapForSmoke;
            ui.ModalHostForSmoke.PressPrimaryForSmoke();
            await SettleNativeWindow();
            ui.TopHudForSmoke.PressSpeedForSmoke(RealtimeSimulationSpeed.Paused);
            string selectedEventId = slice.LatestPresentation.Rail.Items.First(item =>
                RealtimeTimelineTargetResolver.Resolve(
                    slice.DisplayWorldForSmoke,
                    slice.CoreSnapshot,
                    item.Id).Kind == RealtimeTimelineTargetKind.Event).Id;
            slice.ChooseTimelineClusterForSmoke(new[] { selectedEventId });
            await SettleNativeWindow();
            map.GrabFocus();
            await SettleNativeWindow();

            RealtimeMapCameraSnapshot homeCamera = slice.CaptureCameraForSmoke();
            var bounds = slice.CoreSnapshot.Construction.World.Bounds;
            var requestedCamera = new RealtimeMapCameraSnapshot(
                new Vector2(
                    (float)(bounds.MinXUnit +
                        ((bounds.MaxXUnit - bounds.MinXUnit) * 0.35d)),
                    (float)(bounds.MinYUnit +
                        ((bounds.MaxYUnit - bounds.MinYUnit) * 0.65d))),
                ZoomIndex: 2);
            slice.RestoreCameraForSmoke(requestedCamera);
            await SettleNativeWindow();

            string savedHash = slice.CanonicalStateSha256;
            int savedCommands = slice.AcceptedCommandCount;
            RealtimeMapCameraSnapshot savedCamera = slice.CaptureCameraForSmoke();
            string savedFocusSemanticId = ReferenceEquals(
                ui.FocusOwnerForSmoke,
                map)
                ? "MAP"
                : "UNKNOWN";
            Require(savedFocusSemanticId == "MAP" &&
                    savedCamera.ZoomIndex == 2 &&
                    !savedCamera.Center.IsEqualApprox(homeCamera.Center) &&
                    string.Equals(
                        slice.InteractionState.TimelineSelectedItemId,
                        selectedEventId,
                        StringComparison.Ordinal),
                "native roundtrip fixture did not save non-default pan/zoom, map focus, " +
                "and selected event",
                failures);

            foreach ((Vector2I targetSize, int uiScale, string profileLabel) in new[]
                     {
                         (new Vector2I(1920, 1080), 100, "FHD@100"),
                         (new Vector2I(1920, 1080), 200, "FHD@200"),
                     })
            {
                window.Mode = Window.ModeEnum.Windowed;
                Require(await SettleNativeWindow(Window.ModeEnum.Windowed),
                    $"native {profileLabel} did not settle into requested windowed mode",
                    failures);
                window.Size = targetSize;
                ui.UiScalePercent = uiScale;
                Require(await SettleNativeWindow(
                        Window.ModeEnum.Windowed,
                        targetSize),
                    $"native {profileLabel} windowed-before did not settle at " +
                    $"{targetSize} (actual mode={window.Mode}, " +
                    $"size={DisplayServer.WindowGetSize()})",
                    failures);
                ValidateNativeWindowStage(
                    slice,
                    ui,
                    map,
                    targetSize,
                    uiScale,
                    requireExactWindowSize: true,
                    expectedMode: Window.ModeEnum.Windowed,
                    label: $"{profileLabel}/windowed-before",
                    savedHash,
                    savedCommands,
                    savedCamera,
                    selectedEventId,
                    savedFocusSemanticId,
                    failures);

                window.Mode = Window.ModeEnum.Fullscreen;
                Require(await SettleNativeWindow(Window.ModeEnum.Fullscreen),
                    $"native {profileLabel} did not settle into requested fullscreen mode " +
                    $"(actual mode={window.Mode}, size={DisplayServer.WindowGetSize()})",
                    failures);
                ValidateNativeWindowStage(
                    slice,
                    ui,
                    map,
                    DisplayServer.WindowGetSize(),
                    uiScale,
                    requireExactWindowSize: false,
                    expectedMode: Window.ModeEnum.Fullscreen,
                    label: $"{profileLabel}/fullscreen",
                    savedHash,
                    savedCommands,
                    savedCamera,
                    selectedEventId,
                    savedFocusSemanticId,
                    failures);

                window.Mode = Window.ModeEnum.Windowed;
                Require(await SettleNativeWindow(Window.ModeEnum.Windowed),
                    $"native {profileLabel} did not return from fullscreen to windowed " +
                    $"mode (actual mode={window.Mode}, " +
                    $"size={DisplayServer.WindowGetSize()})",
                    failures);
                window.Size = targetSize;
                Require(await SettleNativeWindow(
                        Window.ModeEnum.Windowed,
                        targetSize),
                    $"native {profileLabel} windowed-after did not settle at " +
                    $"{targetSize} (actual mode={window.Mode}, " +
                    $"size={DisplayServer.WindowGetSize()})",
                    failures);
                ValidateNativeWindowStage(
                    slice,
                    ui,
                    map,
                    targetSize,
                    uiScale,
                    requireExactWindowSize: true,
                    expectedMode: Window.ModeEnum.Windowed,
                    label: $"{profileLabel}/windowed-after",
                    savedHash,
                    savedCommands,
                    savedCamera,
                    selectedEventId,
                    savedFocusSemanticId,
                    failures);
            }

            Vector2I screen = DisplayServer.ScreenGetSize();
            if (screen.X < RealtimeUiMetrics.UltraHdResolution.X ||
                screen.Y < RealtimeUiMetrics.UltraHdResolution.Y)
            {
                GD.Print(
                    $"REALTIME_R2_NATIVE_UHD_HARDWARE_UNAVAILABLE screen=" +
                    $"{screen.X}x{screen.Y}; 4K is covered by native SubViewport " +
                    "image readback, not an OS 4K-window claim");
            }
        }
        finally
        {
            if (slice is not null && GodotObject.IsInstanceValid(slice))
            {
                RemoveAndFree(slice);
            }
            window.Mode = Window.ModeEnum.Windowed;
            Require(await SettleNativeWindow(Window.ModeEnum.Windowed),
                "native cleanup could not settle into windowed mode before restore",
                failures);
            window.Size = originalSize;
            window.Position = originalPosition;
            window.ContentScaleSize = originalContentScaleSize;
            window.ContentScaleMode = originalContentScaleMode;
            window.ContentScaleAspect = originalContentScaleAspect;
            if (originalMode != Window.ModeEnum.Windowed)
            {
                window.Mode = originalMode;
            }
            _uiRoot.UiScalePercent = originalStandaloneUiScale;
            _uiRoot.SetLayersVisibleForSmoke(true);
            if (originalFocus is not null &&
                GodotObject.IsInstanceValid(originalFocus) &&
                originalFocus.IsInsideTree() &&
                originalFocus.IsVisibleInTree())
            {
                originalFocus.GrabFocus();
            }
            Require(await SettleNativeWindow(
                    originalMode,
                    originalMode == Window.ModeEnum.Windowed ? originalSize : null),
                "native cleanup did not settle into the original mode/size",
                failures);
            Require(window.Mode == originalMode &&
                    window.Size == originalSize &&
                    window.ContentScaleSize == originalContentScaleSize &&
                    window.ContentScaleMode == originalContentScaleMode &&
                    window.ContentScaleAspect == originalContentScaleAspect &&
                    _uiRoot.UiScalePercent == originalStandaloneUiScale,
                "native window roundtrip did not restore original window/content scale",
                failures);
        }

        if (failures.Count == failureCountBefore)
        {
            GD.Print(
                "REALTIME_R2_NATIVE_WINDOW_ROUNDTRIP_PASS " +
                "FHD@100,FHD@200 windowed-fullscreen-windowed state/focus/camera/" +
                "selection/scale/bounds preserved");
        }
    }

    private static void ValidateNativeWindowStage(
        RealtimeSliceMain slice,
        RealtimeUiRoot ui,
        RealtimeWorldMap map,
        Vector2I expectedPhysical,
        int expectedUiScale,
        bool requireExactWindowSize,
        Window.ModeEnum expectedMode,
        string label,
        string savedHash,
        int savedCommands,
        RealtimeMapCameraSnapshot savedCamera,
        string selectedEventId,
        string savedFocusSemanticId,
        ICollection<string> failures)
    {
        Vector2I physical = DisplayServer.WindowGetSize();
        Vector2 logical = slice.GetViewport().GetVisibleRect().Size;
        RealtimeUiSmokeLayoutSnapshot layout = ui.CaptureLayoutForSmoke(logical);
        RealtimeMapCameraSnapshot camera = slice.CaptureCameraForSmoke();
        string focusSemanticId = ReferenceEquals(ui.FocusOwnerForSmoke, map)
            ? "MAP"
            : "UNKNOWN";
        Require(slice.GetWindow().Mode == expectedMode &&
                (!requireExactWindowSize || physical == expectedPhysical) &&
                physical.X >= RealtimeUiMetrics.MinimumSupportedWidth &&
                physical.Y >= RealtimeUiMetrics.MinimumSupportedHeight &&
                layout.Profile.PhysicalSize == physical &&
                ui.UiScalePercent == expectedUiScale &&
                Mathf.IsEqualApprox(
                    layout.Profile.AccessibilityScale,
                    expectedUiScale / 100f),
            $"native {label} selected wrong OS mode/physical/UI scale " +
            $"(expected={expectedPhysical}, actual={physical}, " +
            $"mode={slice.GetWindow().Mode}/{expectedMode}, " +
            $"profile={layout.Profile.PhysicalSize}, ui={ui.UiScalePercent}%)",
            failures);
        Require(slice.GetWindow().ContentScaleSize ==
                    RealtimeUiMetrics.ReferenceResolution &&
                slice.GetWindow().ContentScaleMode ==
                    Window.ContentScaleModeEnum.CanvasItems &&
                slice.GetWindow().ContentScaleAspect ==
                    Window.ContentScaleAspectEnum.Expand,
            $"native {label} lost the production FHD logical-canvas contract",
            failures);
        Require(string.Equals(slice.CanonicalStateSha256, savedHash,
                    StringComparison.Ordinal) &&
                slice.AcceptedCommandCount == savedCommands &&
                string.Equals(
                    slice.InteractionState.TimelineSelectedItemId,
                    selectedEventId,
                    StringComparison.Ordinal) &&
                camera == savedCamera &&
                string.Equals(focusSemanticId, savedFocusSemanticId,
                    StringComparison.Ordinal),
            $"native {label} changed Core/journal/event/camera/focus across OS mode",
            failures);
        ValidateSurfaceGeometry(
            layout,
            logical,
            slice.LatestPresentation,
            $"native-{label}",
            failures);
        ValidateButtons(
            layout,
            ExpectedPrimaryCtaCount(slice.LatestPresentation),
            $"native-{label}",
            failures);
        ValidateText(layout, $"native-{label}", failures);
    }

    private async Task<bool> SettleNativeWindow(
        Window.ModeEnum? expectedMode = null,
        Vector2I? expectedSize = null,
        int maximumPolls = 80)
    {
        Window window = GetWindow();
        Window.ModeEnum previousMode = window.Mode;
        Vector2I previousSize = DisplayServer.WindowGetSize();
        int stablePolls = 0;
        for (int poll = 0; poll < maximumPolls; poll++)
        {
            SceneTreeTimer timer = GetTree().CreateTimer(
                0.1d,
                processAlways: true,
                processInPhysics: false,
                ignoreTimeScale: true);
            await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
            Window.ModeEnum mode = window.Mode;
            Vector2I size = DisplayServer.WindowGetSize();
            bool requestedState = (!expectedMode.HasValue || mode == expectedMode.Value) &&
                (!expectedSize.HasValue || size == expectedSize.Value);
            if (requestedState && mode == previousMode && size == previousSize)
            {
                stablePolls++;
                // Native macOS fullscreen uses a separate desktop. Keeping the
                // requested state stable for a real second avoids issuing the
                // return transition while its entrance animation is still live.
                if (stablePolls >= 10)
                {
                    return true;
                }
            }
            else
            {
                stablePolls = 0;
            }
            previousMode = mode;
            previousSize = size;
            if ((poll + 1) % 10 == 0)
            {
                if (expectedMode.HasValue && window.Mode != expectedMode.Value)
                {
                    window.Mode = expectedMode.Value;
                }
                if (expectedSize.HasValue &&
                    (!expectedMode.HasValue || window.Mode == expectedMode.Value) &&
                    DisplayServer.WindowGetSize() != expectedSize.Value)
                {
                    window.Size = expectedSize.Value;
                }
            }
        }
        return (!expectedMode.HasValue || window.Mode == expectedMode.Value) &&
            (!expectedSize.HasValue ||
                DisplayServer.WindowGetSize() == expectedSize.Value);
    }
}
#endif
