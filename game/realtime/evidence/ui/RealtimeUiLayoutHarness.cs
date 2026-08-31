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
    private const string NorthBankPromiseDeadlineMarkerId =
        RealtimeR2Ids.PromiseDecisionMarkerPrefix + "NORTH_BANK_MOVE_IN_PROMISE";
    private const string NorthResidentialLoadId = "NORTH_RESIDENTIAL";

    private RealtimeUiRoot _uiRoot = null!;
    private Control _mapWorkspace = null!;
    private bool _offscreenReadbackVerified;

    public override void _Ready()
    {
#if DEBUG
        _uiRoot = GetNode<RealtimeUiRoot>("RealtimeUiRoot");
        _mapWorkspace = GetNode<Control>("MapWorkspace");
        _ = RunHarness();
#else
        GD.PushWarning("RealtimeUiLayoutHarness is DEBUG-only evidence.");
#endif
    }

#if DEBUG
    private async Task RunHarness()
    {
        var failures = new List<string>();
        try
        {
            RealtimeSlicePresentation presentation =
                RealtimeR2Smoke.CreateLayoutPresentation(failures);
            RealtimeR2LayoutPresentationSet presentationStates =
                RealtimeR2Smoke.CreateLayoutPresentations(failures);
            var northBankSlice = new RealtimeSliceMain();
            using var northBankSliceLifetime = northBankSlice.FreeAfterSmoke();
            northBankSlice.BootstrapNativeReleaseForSmoke(
                RealtimeNativeRouteCatalog.ProductCampaign);
            (RealtimeSliceData northBankData, string northBankRootSubstationId) =
                RealtimeR2Smoke.AdvanceReleasePrefixToNorthBankPlanning(
                    northBankSlice,
                    failures);
            northBankSlice.ChooseTimelineClusterForSmoke(
                new[] { NorthBankPromiseDeadlineMarkerId });
            RealtimeSlicePresentation northBankSelectedPresentation =
                northBankSlice.LatestPresentation;
            northBankSlice.NavigateTimelineForSmoke(
                RealtimeTimelineNavigation.Home);
            Present(presentation);
            await SettleLayout();
            ValidateRuntimeBridgeParity(failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE native-offscreen-profile-matrix begin");
            await ValidateLiveProfiles(
                presentation,
                northBankSelectedPresentation,
                failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE native-offscreen-profile-matrix end");
            GD.Print("REALTIME_R2_SMOKE_PHASE settings-surface-profiles begin");
            await ValidateSettingsSurfaceProfiles(presentation, failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE settings-surface-profiles end");
            GD.Print("REALTIME_R2_SMOKE_PHASE live-presentation-state-matrix begin");
            await ValidatePresentationStates(presentationStates, failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE live-presentation-state-matrix end");
            GD.Print("REALTIME_R2_SMOKE_PHASE g3-visual-renderer begin");
            await ValidateG3VisualRenderer(presentation, failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE g3-visual-renderer end");
            GD.Print("REALTIME_R2_SMOKE_PHASE g3-ui-chrome begin");
            await ValidateG3UiChrome(presentation, failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE g3-ui-chrome end");
            GD.Print("REALTIME_R2_SMOKE_PHASE audit-presentation-semantics begin");
            await ValidateAuditPresentationSemantics(presentation, failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE audit-presentation-semantics end");
            GD.Print("REALTIME_R2_SMOKE_PHASE actual-live-clock begin");
            await ValidateActualLiveClock(failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE actual-live-clock end");
            GD.Print("REALTIME_R2_SMOKE_PHASE actual-slice-scene-e2e begin");
            await ValidateActualSliceSceneEndToEnd(failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE actual-slice-scene-e2e end");
            GD.Print("REALTIME_R2_SMOKE_PHASE actual-thermal-cue-pipeline begin");
            await ValidateActualThermalCuePipeline(failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE actual-thermal-cue-pipeline end");
            GD.Print("REALTIME_R2_SMOKE_PHASE gpu-uhd-render-target begin");
            await ValidateActualSliceUhdGpuRenderTarget(failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE gpu-uhd-render-target end");
            GD.Print("REALTIME_R2_SMOKE_PHASE native-window-roundtrip begin");
            await ValidateNativeWindowRoundTrip(failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE native-window-roundtrip end");
            GD.Print("REALTIME_R2_SMOKE_PHASE live-input-and-modal begin");
            await ValidateNorthBankPromiseLiveInput(
                northBankSlice,
                northBankData,
                northBankRootSubstationId,
                failures);
            await ValidateLivePrimaryCta(presentation, failures);
            await ValidateTimelineFocusRestore(presentation, failures);
            await ValidateSelectedTimelineTogglePersistence(presentation, failures);
            await ValidateEmptyTimelineMouseNavigation(presentation, failures);
            ValidateInputPriority(failures);
            await ValidateInjectedKeyDelivery(presentation, failures);
            await ValidateModalFocusAndPause(failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE live-input-and-modal end");
        }
        catch (Exception exception)
        {
            failures.Add(
                $"Harness threw {exception.GetType().Name}: {exception.Message}");
        }

        if (failures.Count == 0)
        {
            GD.Print(_offscreenReadbackVerified
                ? "REALTIME_R2_NATIVE_OFFSCREEN_UHD_PASS actual-r1-slice " +
                  "SubViewport-image-readback=10 FHD/UHD=100/125/150/200 " +
                  "QHD=100/200 fixed-logical=1920x1080; OS window/fullscreen and " +
                  "physical keyboard gates are separate"
                : "REALTIME_R2_OFFSCREEN_CONTROL_TREE_PASS actual-r1-slice " +
                  "SubViewport-control-trees/textures=10 FHD/UHD=100/125/150/200 " +
                  "QHD=100/200 fixed-logical=1920x1080; headless dummy-renderer " +
                  "image readback and OS hardware gates are separate");
            FinishAndQuit(0);
            return;
        }
        foreach (string failure in failures.Distinct(StringComparer.Ordinal))
        {
            GD.PushError($"REALTIME_R2_SMOKE_FAIL {failure}");
        }
        FinishAndQuit(1);
    }

    private void Present(RealtimeSlicePresentation presentation)
        => Present(_uiRoot, presentation);

    private static void Present(
        RealtimeUiRoot uiRoot,
        RealtimeSlicePresentation presentation)
    {
        if (presentation.Modal is null && uiRoot.ModalHostForSmoke.Depth > 0)
        {
            uiRoot.PopModal();
        }
        uiRoot.SetTopHud(presentation.Hud);
        uiRoot.SetEventRail(presentation.Rail);
        uiRoot.SetContextDock(presentation.Context);
        uiRoot.SetBuildShelf(presentation.BuildShelf);
        uiRoot.SetActionDock(presentation.ActionDock);
        if (presentation.Modal is not null &&
            !string.Equals(
                uiRoot.ModalHostForSmoke.ActiveModal?.Id,
                presentation.Modal.Id,
                StringComparison.Ordinal) &&
            !uiRoot.PushModal(presentation.Modal))
        {
            throw new InvalidOperationException("Actual presentation modal was rejected.");
        }
    }
#endif
}
