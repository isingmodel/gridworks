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
    private void ValidateRuntimeBridgeParity(ICollection<string> failures)
    {
        Vector2I physical = DisplayServer.WindowGetSize();
        Vector2 logical = GetViewportRect().Size;
        if (physical.X <= 0 || physical.Y <= 0 || logical.X <= 0 || logical.Y <= 0)
        {
            GD.Print(
                "REALTIME_R2_NATIVE_GATE_PENDING headless display has no physical " +
                "window; OS resize/fullscreen round-trip FHD@100/200 was not claimed");
            return;
        }
        try
        {
            _uiRoot.ApplyRuntimeThenSyntheticParityForSmoke(
                physical,
                logical,
                uiScalePercent: 100);
            GD.Print(
                $"REALTIME_R2_NATIVE_WINDOW_SAMPLE physical={physical.X}x{physical.Y} " +
                $"logical={logical.X:0}x{logical.Y:0}; one current-window sample only; " +
                "NATIVE_WINDOW_MATRIX_PENDING FHD@100/200 windowed/fullscreen");
        }
        catch (Exception exception)
        {
            failures.Add($"Synthetic layout bridge parity failed: {exception.Message}");
        }
    }

    private async Task ValidateLiveProfiles(
        RealtimeSlicePresentation presentation,
        RealtimeSlicePresentation northBankPresentation,
        ICollection<string> failures)
    {
        (Vector2I Physical, int Scale, RealtimeResolutionTier Tier, float Density)[] profiles =
        [
            (new Vector2I(1920, 1080), 100, RealtimeResolutionTier.FullHd, 1f),
            (new Vector2I(1920, 1080), 125, RealtimeResolutionTier.FullHd, 1f),
            (new Vector2I(1920, 1080), 150, RealtimeResolutionTier.FullHd, 1f),
            (new Vector2I(1920, 1080), 200, RealtimeResolutionTier.FullHd, 1f),
            (new Vector2I(3840, 2160), 100, RealtimeResolutionTier.UltraHd, 2f),
            (new Vector2I(3840, 2160), 125, RealtimeResolutionTier.UltraHd, 2f),
            (new Vector2I(3840, 2160), 150, RealtimeResolutionTier.UltraHd, 2f),
            (new Vector2I(3840, 2160), 200, RealtimeResolutionTier.UltraHd, 2f),
            (new Vector2I(2560, 1440), 100, RealtimeResolutionTier.FullHd, 4f / 3f),
            (new Vector2I(2560, 1440), 200, RealtimeResolutionTier.FullHd, 4f / 3f),
        ];
        Vector2 logical = RealtimeUiMetrics.ReferenceResolution;
        bool supportsImageReadback = !string.Equals(
            DisplayServer.GetName(),
            "headless",
            StringComparison.OrdinalIgnoreCase);
        _offscreenReadbackVerified = supportsImageReadback;
        if (!supportsImageReadback)
        {
            GD.Print(
                "REALTIME_R2_NATIVE_OFFSCREEN_READBACK_GATE_PENDING " +
                "headless dummy renderer; exact native SubViewport allocation and " +
                "live control-tree geometry remain enforced");
        }
        RealtimeTimelineItemPresentation[] expectedLinearItems = presentation.Rail.Items
            .Where(item => item.Visibility != RealtimeTimelineVisibility.Hidden)
            .Where(item => item.StartMinute <= presentation.Rail.HorizonEndMinute &&
                           (item.EndMinute ?? item.StartMinute) >=
                               presentation.Rail.HorizonStartMinute)
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        RealtimeTimelineItemPresentation[] northBankLinearItems =
            northBankPresentation.Rail.Items
                .Where(item => item.Visibility != RealtimeTimelineVisibility.Hidden)
                .Where(item =>
                    item.StartMinute <= northBankPresentation.Rail.HorizonEndMinute &&
                    (item.EndMinute ?? item.StartMinute) >=
                        northBankPresentation.Rail.HorizonStartMinute)
                .OrderBy(item => item.StartMinute)
                .ThenBy(item => item.Priority)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();

        foreach ((Vector2I physical, int scale, RealtimeResolutionTier tier, float density)
                 in profiles)
        {
            string label = $"{physical.X}x{physical.Y}@{scale}%";
            (SubViewport viewport, RealtimeUiRoot profileRoot) =
                await CreateOffscreenUi(physical, logical, scale, presentation);
            RealtimeUiSmokeLayoutSnapshot snapshot;
            try
            {
                snapshot = profileRoot.CaptureLayoutForSmoke(logical);
                Vector2 renderTargetSize = viewport.GetTexture().GetSize();
                Vector2I renderedFrameSize = Vector2I.Zero;
                if (supportsImageReadback)
                {
                    RenderingServer.ForceDraw(swapBuffers: false);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    Image? renderedFrame = viewport.GetTexture().GetImage();
                    renderedFrameSize = renderedFrame?.GetSize() ?? Vector2I.Zero;
                    renderedFrame?.Dispose();
                }
                Require(viewport.Size == physical &&
                        viewport.Size2DOverride == new Vector2I(
                            Mathf.RoundToInt(logical.X),
                            Mathf.RoundToInt(logical.Y)) &&
                        viewport.Size2DOverrideStretch &&
                        renderTargetSize.IsEqualApprox(new Vector2(physical.X, physical.Y)) &&
                        (!supportsImageReadback || renderedFrameSize == physical),
                    $"{label} did not create the requested native SubViewport render " +
                    $"target (viewport={viewport.Size}, override=" +
                    $"{viewport.Size2DOverride}, texture={renderTargetSize}, " +
                    $"frame={renderedFrameSize})",
                    failures);

                Require(snapshot.Profile.PhysicalSize == physical &&
                        snapshot.Profile.Tier == tier &&
                        Mathf.IsEqualApprox(snapshot.Profile.PhysicalRenderScale, density) &&
                        Mathf.IsEqualApprox(snapshot.Profile.AccessibilityScale, scale / 100f),
                    $"{label} selected the wrong physical/layout profile", failures);
                Require(snapshot.Profile.MinimumHitTarget ==
                        Mathf.RoundToInt(44f * scale / 100f),
                    $"{label} did not scale the 44px interaction target", failures);
                ValidateSurfaceGeometry(snapshot, logical, presentation, label, failures);
                ValidateButtons(
                    snapshot,
                    ExpectedPrimaryCtaCount(presentation),
                    label,
                    failures);
                ValidateText(snapshot, label, failures);
                ValidateScroll(profileRoot, snapshot, presentation, label, failures);
                ValidateTimeline(
                    profileRoot.EventRailForSmoke,
                    snapshot,
                    expectedLinearItems,
                    scale,
                    label,
                    failures);
                if (tier == RealtimeResolutionTier.FullHd &&
                    scale is 100 or 125)
                {
                    RealtimeNextEventPresentation? next = presentation.Rail.NextEvent;
                    RealtimeTimelineItemPresentation? draftConstruction =
                        expectedLinearItems.SingleOrDefault(item => string.Equals(
                            item.Id,
                            RealtimeR2Ids.DraftConstructionMarker,
                            StringComparison.Ordinal));
                    RealtimeUiSmokeMarkerFact? draftMarker = draftConstruction is null
                        ? null
                        : profileRoot.EventRailForSmoke.MarkerFactsForSmoke()
                            .SingleOrDefault(marker => marker.ItemIds.Contains(
                                draftConstruction.Id,
                                StringComparer.Ordinal));
                    RealtimeTimelineTooltipOverlayFact? draftDetail =
                        draftConstruction is null || draftMarker is null
                            ? null
                            : profileRoot.EventRailForSmoke.TooltipOverlayFactForSmoke(
                                draftConstruction.Id);
                    Require(next is not null &&
                            profileRoot.EventRailForSmoke.NextEventCountdownTextForSmoke
                                .Contains(next.CountdownLabel, StringComparison.Ordinal) &&
                            profileRoot.EventRailForSmoke.NextEventWindowTextForSmoke
                                .Contains(next.CompactWindowLabel,
                                    StringComparison.Ordinal) &&
                            draftConstruction is not null &&
                            draftConstruction.SourceKind ==
                                RealtimeTimelineSourceKind.Draft &&
                            draftMarker is not null &&
                            draftMarker.VisibleText.Contains("◇",
                                StringComparison.Ordinal) &&
                            draftDetail is { CustomOverlay: true } &&
                            draftDetail.Text.Contains("초안 예상",
                                StringComparison.Ordinal) &&
                            draftDetail.Text.Contains(draftConstruction.TimingLabel,
                                StringComparison.Ordinal) &&
                            draftDetail.Text.Contains(draftConstruction.Title,
                                StringComparison.Ordinal) &&
                            draftMarker.AccessibilityName.Contains("초안 예상",
                                StringComparison.Ordinal) &&
                            draftMarker.LeftNeighborItemIds.Count > 0 &&
                            draftMarker.RightNeighborItemIds.Count > 0,
                        $"{label} lost persistent next-event start/end/countdown, " +
                        "draft form/copy, or resolved target coverage",
                        failures);
                }
                await ValidateNonModalFocusTraversal(
                    viewport,
                    profileRoot,
                    label,
                    failures);

                Present(profileRoot, northBankPresentation);
                profileRoot.ApplyLayoutForSmoke(physical, logical, scale);
                await SettleLayout();
                profileRoot.ApplyLayoutForSmoke(physical, logical, scale);
                await SettleLayout();
                RealtimeUiSmokeLayoutSnapshot northBankSnapshot =
                    profileRoot.CaptureLayoutForSmoke(logical);
                string northBankLabel = $"{label}-north-bank-promise";
                ValidateSurfaceGeometry(
                    northBankSnapshot,
                    logical,
                    northBankPresentation,
                    northBankLabel,
                    failures);
                ValidateButtons(
                    northBankSnapshot,
                    ExpectedPrimaryCtaCount(northBankPresentation),
                    northBankLabel,
                    failures);
                ValidateText(northBankSnapshot, northBankLabel, failures);
                ValidateScroll(
                    profileRoot,
                    northBankSnapshot,
                    northBankPresentation,
                    northBankLabel,
                    failures);
                ValidateTimeline(
                    profileRoot.EventRailForSmoke,
                    northBankSnapshot,
                    northBankLinearItems,
                    scale,
                    northBankLabel,
                    failures);
                ValidateNorthBankPromiseTimeline(
                    profileRoot,
                    northBankSnapshot,
                    northBankPresentation,
                    northBankLabel,
                    failures);
                await ValidateNonModalFocusTraversal(
                    viewport,
                    profileRoot,
                    northBankLabel,
                    failures);
            }
            finally
            {
                RemoveAndFree(viewport);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
    }

    private async Task ValidateSettingsSurfaceProfiles(
        RealtimeSlicePresentation presentation,
        ICollection<string> failures)
    {
        Vector2 logical = RealtimeUiMetrics.ReferenceResolution;
        foreach (int scale in new[] { 100, 200 })
        {
            string label = $"settings/FHD@{scale}%";
            (SubViewport viewport, RealtimeUiRoot root) = await CreateOffscreenUi(
                new Vector2I(1920, 1080),
                logical,
                scale,
                presentation with { Modal = null });
            try
            {
                RealtimeSettingsSurface settings = root.SettingsSurfaceForSmoke;
                Button opener = root.TopHudForSmoke.SettingsButton;
                var settingsPresentation = new RealtimeSettingsPresentation(
                    new RealtimeSettingsValues(
                        Fullscreen: false,
                        UiScalePercent: scale,
                        MasterVolumePercent: 100,
                        AmbientVolumePercent: 75,
                        SfxVolumePercent: 50,
                        ReduceMotion: true),
                    "설정 layout smoke",
                    CanApply: true);
                root.SettingsOpenRequested += journey =>
                {
                    if (journey != RealtimeSettingsJourney.Gameplay)
                    {
                        failures.Add($"{label} opened the wrong settings journey.");
                        return;
                    }
                    root.ShowSettings(settingsPresentation);
                };
                root.SettingsCloseRequested += _ => root.HideSettings();

                opener.GrabFocus();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                opener.EmitSignal(BaseButton.SignalName.Pressed);
                await SettleLayout();

                BaseButton[] targets =
                [
                    settings.WindowModeOption,
                    settings.UiScaleOption,
                    settings.MasterVolumeOption,
                    settings.AmbientVolumeOption,
                    settings.SfxVolumeOption,
                    settings.ReduceMotionCheck,
                    settings.CloseButton,
                    settings.ApplyButton,
                ];
                PanelContainer panel = settings.GetNode<PanelContainer>("%SettingsPanel");
                Rect2 panelRect = panel.GetGlobalRect();
                var safeRect = new Rect2(24f, 24f, logical.X - 48f, logical.Y - 48f);
                RealtimeLayoutProfile profile = RealtimeUiMetrics.ForWindow(
                    new Vector2I(1920, 1080),
                    scale);
                Require(settings.Visible &&
                        root.InputRouterForSmoke.ActiveOwner == "product_settings" &&
                        root.InputRouterForSmoke.ActivePriority ==
                            RealtimeInputPriority.BlockingModal &&
                        ReferenceEquals(root.FocusOwnerForSmoke, settings.WindowModeOption),
                    $"{label} did not own input and initial focus", failures);
                Require(safeRect.Encloses(panelRect) &&
                        panelRect.Size.X > 0f && panelRect.Size.Y > 0f,
                    $"{label} panel escaped the 24px safe bounds: {panelRect}", failures);
                Require(targets.Length == 8 && targets.All(control =>
                            control.IsVisibleInTree() &&
                            !control.Disabled &&
                            control.FocusMode != Control.FocusModeEnum.None &&
                            control.Size.X >= profile.MinimumHitTarget &&
                            control.Size.Y >= profile.MinimumHitTarget &&
                            panelRect.Encloses(control.GetGlobalRect())),
                    $"{label} did not keep all eight focus targets visible, enabled, " +
                    "inside the panel, and at the minimum hit target",
                    failures);
                Require(settings.CloseButton.Size.Y >= profile.PrimaryHitTarget &&
                        settings.ApplyButton.Size.Y >= profile.PrimaryHitTarget,
                    $"{label} action buttons missed the primary hit target", failures);

                string[] targetPaths = targets
                    .Select(control => control.GetPath().ToString())
                    .ToArray();
                await ValidateFocusDirection(
                    viewport,
                    root,
                    targets[0],
                    targetPaths,
                    backwards: false,
                    label,
                    failures);
                await ValidateFocusDirection(
                    viewport,
                    root,
                    targets[^1],
                    targetPaths,
                    backwards: true,
                    label,
                    failures);

                settings.CloseButton.EmitSignal(BaseButton.SignalName.Pressed);
                await SettleLayout();
                Require(!settings.Visible &&
                        ReferenceEquals(root.FocusOwnerForSmoke, opener) &&
                        root.InputRouterForSmoke.ActivePriority ==
                            RealtimeInputPriority.EmptyTerrain,
                    $"{label} close did not restore its exact opener and input owner",
                    failures);
            }
            finally
            {
                RemoveAndFree(viewport);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
    }

    private async Task<(SubViewport Viewport, RealtimeUiRoot UiRoot)> CreateOffscreenUi(
        Vector2I physicalSize,
        Vector2 logicalSize,
        int uiScalePercent,
        RealtimeSlicePresentation presentation)
    {
        var viewport = new SubViewport
        {
            Name = $"SmokeViewport_{physicalSize.X}x{physicalSize.Y}_{uiScalePercent}",
            Size = physicalSize,
            Size2DOverride = new Vector2I(
                Mathf.RoundToInt(logicalSize.X),
                Mathf.RoundToInt(logicalSize.Y)),
            Size2DOverrideStretch = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            HandleInputLocally = true,
        };
        AddChild(viewport);
        PackedScene uiScene = GD.Load<PackedScene>(
            "res://realtime/ui/RealtimeUiRoot.tscn");
        RealtimeUiRoot root = uiScene.Instantiate<RealtimeUiRoot>();
        viewport.AddChild(root);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Present(root, presentation);
        root.ApplyLayoutForSmoke(physicalSize, logicalSize, uiScalePercent);
        await SettleLayout();
        root.ApplyLayoutForSmoke(physicalSize, logicalSize, uiScalePercent);
        viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        await SettleLayout();
        return (viewport, root);
    }

    private static void ValidateNorthBankPromiseTimeline(
        RealtimeUiRoot uiRoot,
        RealtimeUiSmokeLayoutSnapshot snapshot,
        RealtimeSlicePresentation presentation,
        string label,
        ICollection<string> failures)
    {
        RealtimeTimelineItemPresentation deadline = presentation.Rail.Items.Single(item =>
            string.Equals(
                item.Id,
                NorthBankPromiseDeadlineMarkerId,
                StringComparison.Ordinal));
        RealtimeTimelineItemPresentation commissioning =
            presentation.Rail.Items.Single(item => string.Equals(
                item.Id,
                "NORTH_BANK_COMMISSIONING",
                StringComparison.Ordinal));
        RealtimeUiSmokeMarkerFact marker = snapshot.Markers.Single(item =>
            item.ItemIds.Contains(
                NorthBankPromiseDeadlineMarkerId,
                StringComparer.Ordinal));
        RealtimeUiSmokeAccessibleTimelineItemFact accessible =
            snapshot.AccessibleTimelineItems.Single(item => string.Equals(
                item.ItemId,
                NorthBankPromiseDeadlineMarkerId,
                StringComparison.Ordinal));
        RealtimeTimelineTooltipOverlayFact hover = uiRoot.EventRailForSmoke
            .TooltipOverlayFactForSmoke(NorthBankPromiseDeadlineMarkerId);
        RealtimeContextDockPresentation context = presentation.Context;
        Button keep = uiRoot.ContextDockForSmoke.GetNode<Button>(
            "Margin/Column/Footer/PrimaryButton");
        Button defer = uiRoot.ContextDockForSmoke.GetNode<Button>(
            "Margin/Column/Footer/SecondaryButton");

        Require(deadline is
                {
                    Kind: RealtimeTimelineItemKind.Decision,
                    Lane: RealtimeTimelineLane.DemandAndDeadline,
                    StartMinute: 265680,
                    Visibility: RealtimeTimelineVisibility.Announced,
                    IsActionable: true,
                } &&
                presentation.Rail.NextEvent is
                {
                    EventId: NorthBankPromiseDeadlineMarkerId,
                    StartMinute: 265680,
                } &&
                deadline.StartMinute < commissioning.StartMinute &&
                deadline.Description.Contains(
                    "선택 전 Keep 가정",
                    StringComparison.Ordinal) &&
                deadline.Description.Contains(
                    "마감 전 변경 가능",
                    StringComparison.Ordinal),
            $"{label} did not preserve the exact deadline-before-commissioning truth",
            failures);
        Require(snapshot.VisibleTimelineLanes == 1 &&
                snapshot.TimelineLaneLabels == 0 &&
                marker.DisplayLane == 0 &&
                marker.AccessibilityName.Contains(
                    "운영 결정",
                    StringComparison.Ordinal) &&
                marker.AccessibilityName.Contains(
                    "선택 전 Keep 가정",
                    StringComparison.Ordinal) &&
                hover.CustomOverlay &&
                hover.Text.Contains(deadline.TimingLabel, StringComparison.Ordinal) &&
                hover.Text.Contains(deadline.Title, StringComparison.Ordinal) &&
                hover.Text.Contains(deadline.Description, StringComparison.Ordinal) &&
                accessible.Text.Contains("운영 결정", StringComparison.Ordinal) &&
                accessible.Tooltip.Contains(
                    deadline.Description,
                    StringComparison.Ordinal),
            $"{label} deadline lost its one-line compact marker, hover detail, or AX copy",
            failures);
        Require(context is
                {
                    SubjectId: NorthBankPromiseDeadlineMarkerId,
                    Visible: true,
                    PrimaryAction:
                    {
                        Id: RealtimeR2Ids.PromiseKeepAction,
                        Enabled: true,
                        Visible: true,
                    },
                    SecondaryAction:
                    {
                        Id: RealtimeR2Ids.PromiseDeferAction,
                        Enabled: true,
                        Visible: true,
                    },
                } &&
                keep.IsVisibleInTree() &&
                !keep.Disabled &&
                keep.Text == context.PrimaryAction.Label &&
                keep.AccessibilityName == context.PrimaryAction.Label &&
                keep.AccessibilityDescription == context.PrimaryAction.Description &&
                defer.IsVisibleInTree() &&
                !defer.Disabled &&
                defer.Text == context.SecondaryAction.Label &&
                defer.AccessibilityName == context.SecondaryAction.Label &&
                defer.AccessibilityDescription == context.SecondaryAction.Description,
            $"{label} did not expose both authored Keep/Defer actions with AX text",
            failures);
    }
}
#endif
