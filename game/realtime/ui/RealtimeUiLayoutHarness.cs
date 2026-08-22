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
            GD.Print("REALTIME_R2_SMOKE_PHASE deterministic-core-controller begin");
            RealtimeR2Smoke.Validate(failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE deterministic-core-controller end");
            RealtimeSlicePresentation presentation =
                RealtimeR2Smoke.CreateLayoutPresentation(failures);
            RealtimeR2LayoutPresentationSet presentationStates =
                RealtimeR2Smoke.CreateLayoutPresentations(failures);
            Present(presentation);
            await SettleLayout();
            ValidateRuntimeBridgeParity(failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE native-offscreen-profile-matrix begin");
            await ValidateLiveProfiles(presentation, failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE native-offscreen-profile-matrix end");
            GD.Print("REALTIME_R2_SMOKE_PHASE live-presentation-state-matrix begin");
            await ValidatePresentationStates(presentationStates, failures);
            GD.Print("REALTIME_R2_SMOKE_PHASE live-presentation-state-matrix end");
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
                    snapshot,
                    expectedLinearItems,
                    scale,
                    presentation.Rail.Expanded,
                    label,
                    failures);
                await ValidateNonModalFocusTraversal(
                    viewport,
                    profileRoot,
                    label,
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

    private async Task ValidatePresentationStates(
        RealtimeR2LayoutPresentationSet presentations,
        ICollection<string> failures)
    {
        (string Name, RealtimeSlicePresentation Presentation)[] states =
        [
            ("world", presentations.World),
            ("build-shelf", presentations.BuildShelf),
            ("inspector", presentations.Inspector),
            ("action", presentations.Action),
            ("expanded-timeline", presentations.Timeline),
            ("modal", presentations.Modal),
        ];
        (Vector2I Physical, int Scale)[] profiles =
        [
            (new Vector2I(1920, 1080), 100),
            (new Vector2I(1920, 1080), 125),
            (new Vector2I(1920, 1080), 150),
            (new Vector2I(1920, 1080), 200),
            (new Vector2I(3840, 2160), 100),
            (new Vector2I(3840, 2160), 125),
            (new Vector2I(3840, 2160), 150),
            (new Vector2I(3840, 2160), 200),
            (new Vector2I(2560, 1440), 100),
            (new Vector2I(2560, 1440), 200),
        ];
        foreach ((string stateName, RealtimeSlicePresentation presentation) in states)
        foreach ((Vector2I physical, int scale) in profiles)
        {
            string label =
                $"state={stateName}/{physical.X}x{physical.Y}@{scale}%";
            (SubViewport viewport, RealtimeUiRoot stateRoot) = await CreateOffscreenUi(
                physical,
                RealtimeUiMetrics.ReferenceResolution,
                scale,
                presentation);
            try
            {
                RealtimeUiSmokeLayoutSnapshot snapshot = stateRoot.CaptureLayoutForSmoke(
                    RealtimeUiMetrics.ReferenceResolution);
                ValidateSurfaceGeometry(
                    snapshot,
                    RealtimeUiMetrics.ReferenceResolution,
                    presentation,
                    label,
                    failures);
                ValidateButtons(
                    snapshot,
                    ExpectedPrimaryCtaCount(presentation),
                    label,
                    failures);
                ValidateText(snapshot, label, failures);
                ValidateScroll(stateRoot, snapshot, presentation, label, failures);
                RealtimeTimelineItemPresentation[] visibleItems = presentation.Rail.Items
                    .Where(item => item.Visibility != RealtimeTimelineVisibility.Hidden)
                    .Where(item => item.StartMinute <= presentation.Rail.HorizonEndMinute &&
                                   (item.EndMinute ?? item.StartMinute) >=
                                       presentation.Rail.HorizonStartMinute)
                    .OrderBy(item => item.StartMinute)
                    .ThenBy(item => item.Priority)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .ToArray();
                ValidateTimeline(
                    snapshot,
                    visibleItems,
                    scale,
                    presentation.Rail.Expanded,
                    label,
                    failures);
                if (presentation.Modal is null)
                {
                    await ValidateNonModalFocusTraversal(
                        viewport,
                        stateRoot,
                        label,
                        failures);
                }
                if (scale == 200 && presentation.Context.Visible &&
                    presentation.Context.Details.Count > 0)
                {
                    stateRoot.ContextDockForSmoke.PressFirstDetailTabForSmoke();
                    await SettleLayout();
                    RealtimeUiSmokeLayoutSnapshot detailSnapshot =
                        stateRoot.CaptureLayoutForSmoke(
                            RealtimeUiMetrics.ReferenceResolution);
                    ValidateSurfaceGeometry(
                        detailSnapshot,
                        RealtimeUiMetrics.ReferenceResolution,
                        presentation,
                        $"{label}/detail-tab",
                        failures);
                    ValidateText(detailSnapshot, $"{label}/detail-tab", failures);
                    ValidateScroll(
                        stateRoot,
                        detailSnapshot,
                        presentation,
                        $"{label}/detail-tab",
                        failures);
                    await ValidateNonModalFocusTraversal(
                        viewport,
                        stateRoot,
                        $"{label}/detail-tab",
                        failures);
                }
            }
            finally
            {
                RemoveAndFree(viewport);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
    }

    private async Task ValidateAuditPresentationSemantics(
        RealtimeSlicePresentation baseline,
        ICollection<string> failures)
    {
        await ValidateLockedSpeedPresentation(
            baseline,
            RealtimeSimulationState.AutoPaused,
            RealtimePauseReason.ChapterBriefing,
            "장 안내 정지",
            failures);
        await ValidateLockedSpeedPresentation(
            baseline,
            RealtimeSimulationState.Ended,
            RealtimePauseReason.CampaignResult,
            "운영 종료",
            failures);

        var typedSlice = CreateRunningAuditSlice();
        using var typedLifetime = typedSlice.FreeAfterSmoke();
        RealtimeTimelineItemPresentation thermalKindItem =
            typedSlice.LatestPresentation.Rail.Items.First(item =>
                item.Kind == RealtimeTimelineItemKind.ThermalProtection);
        RealtimeCampaignSnapshot typedBase = typedSlice.CoreSnapshot;
        RealtimeForecastEvent sourceForecast = typedBase.Forecast.Events[0];
        RealtimeScheduledEventDefinition sourceScheduled =
            typedBase.Chapter.ScheduledEvents.Single(item => string.Equals(
                item.EventId,
                sourceForecast.EventId,
                StringComparison.Ordinal));
        CommercialOperatingPhaseDefinition weatherProfile =
            sourceForecast.OperatingProfile with
            {
                PhaseId = "SMOKE_WEATHER_PROFILE",
                DisplayName = "동부 생활권 폭우",
                ActiveRiskAreaIds = new[] { "SMOKE_RISK_AREA" },
                UnavailableNodeIds = Array.Empty<string>(),
                UnavailableEdgeIds = Array.Empty<string>(),
            };
        string authoredUnavailableNodeId = typedBase.Construction.World.Nodes
            .OrderBy(item => item.NodeId, StringComparer.Ordinal)
            .First().NodeId;
        CommercialOperatingPhaseDefinition outageProfile =
            sourceForecast.OperatingProfile with
            {
                PhaseId = "SMOKE_OUTAGE_PROFILE",
                DisplayName = "배전 설비 계획 사용불가",
                ActiveRiskAreaIds = Array.Empty<string>(),
                UnavailableNodeIds = new[] { authoredUnavailableNodeId },
                UnavailableEdgeIds = Array.Empty<string>(),
            };
        RealtimeForecastEvent weatherForecast = sourceForecast with
        {
            EventId = "SMOKE_WEATHER_EVENT",
            DisplayName = weatherProfile.DisplayName,
            OperatingProfile = weatherProfile,
        };
        RealtimeForecastEvent outageForecast = sourceForecast with
        {
            EventId = "SMOKE_OUTAGE_EVENT",
            DisplayName = outageProfile.DisplayName,
            StartMinute = sourceForecast.StartMinute + 90,
            EndMinute = sourceForecast.EndMinute + 90,
            OperatingProfile = outageProfile,
        };
        RealtimeCampaignSnapshot typedSnapshot = typedBase with
        {
            Chapter = typedBase.Chapter with
            {
                ScheduledEvents = new[]
                {
                    sourceScheduled with
                    {
                        EventId = weatherForecast.EventId,
                        OperatingProfile = weatherProfile,
                    },
                    sourceScheduled with
                    {
                        EventId = outageForecast.EventId,
                        StartOffsetMinutes = sourceScheduled.StartOffsetMinutes + 90,
                        OperatingProfile = outageProfile,
                    },
                },
            },
            Forecast = typedBase.Forecast with
            {
                Events = new[] { weatherForecast, outageForecast },
            },
        };
        RealtimeSlicePresentation typedPresentation =
            typedSlice.PresentSnapshotForSmoke(typedSnapshot);
        RealtimeTimelineItemPresentation weatherItem =
            typedPresentation.Rail.Items.Single(item => string.Equals(
                item.Id, weatherForecast.EventId, StringComparison.Ordinal));
        RealtimeTimelineItemPresentation outageItem =
            typedPresentation.Rail.Items.Single(item => string.Equals(
                item.Id, outageForecast.EventId, StringComparison.Ordinal));
        RealtimeInteractionState typedSelection = typedSlice.InteractionState with
        {
            Tool = RealtimeTool.Inspect,
            Surface = RealtimeSurface.Inspector,
            SelectionId = outageItem.Id,
            TimelineSelectedItemId = outageItem.Id,
        };
        RealtimeSlicePresentation selectedOutagePresentation =
            typedSlice.PresentSnapshotForSmoke(typedSnapshot, typedSelection);
        RealtimeTimelineTarget outageTarget = RealtimeSlicePresenter.ResolveTimelineTarget(
            typedSlice.DisplayWorldForSmoke,
            typedSnapshot,
            outageItem.Id);
        RealtimeWorldHighlight? outageHighlight =
            selectedOutagePresentation.World.Highlight;
        Require(weatherItem.Kind == RealtimeTimelineItemKind.Weather &&
                weatherItem.Lane == RealtimeTimelineLane.WeatherAndOutage &&
                outageItem.Kind == RealtimeTimelineItemKind.PlannedOutage &&
                outageItem.Lane == RealtimeTimelineLane.WeatherAndOutage &&
                thermalKindItem.Lane == RealtimeTimelineLane.ThermalProtection &&
                !string.Equals(outageItem.KindIcon, thermalKindItem.KindIcon,
                    StringComparison.Ordinal) &&
                !string.Equals(outageItem.KindLabel, thermalKindItem.KindLabel,
                    StringComparison.Ordinal),
            "authored risk/unavailability profiles were flattened into demand markers",
            failures);
        (SubViewport typedViewport, RealtimeUiRoot typedRoot) = await CreateOffscreenUi(
            RealtimeUiMetrics.ReferenceResolution,
            RealtimeUiMetrics.ReferenceResolution,
            100,
            typedPresentation);
        try
        {
            RealtimeUiSmokeMarkerFact weatherMarker = typedRoot.EventRailForSmoke
                .MarkerFactsForSmoke().Single(marker => marker.ItemIds.Contains(
                    weatherItem.Id, StringComparer.Ordinal));
            RealtimeUiSmokeMarkerFact outageMarker = typedRoot.EventRailForSmoke
                .MarkerFactsForSmoke().Single(marker => marker.ItemIds.Contains(
                    outageItem.Id, StringComparer.Ordinal));
            Require(weatherMarker.AccessibilityName.Contains(
                        weatherItem.KindLabel, StringComparison.Ordinal) &&
                    outageMarker.AccessibilityName.Contains(
                        outageItem.KindLabel, StringComparison.Ordinal) &&
                    !string.Equals(weatherItem.KindLabel, weatherItem.KindIcon,
                        StringComparison.Ordinal) &&
                    !string.Equals(outageItem.KindLabel, outageItem.KindIcon,
                        StringComparison.Ordinal) &&
                    outageMarker.AccessibilityName.Contains(
                        "계획 사용불가", StringComparison.Ordinal) &&
                    !outageMarker.AccessibilityName.Contains(
                        "열 보호", StringComparison.Ordinal),
                "weather/outage markers exposed glyphs instead of typed AX kind labels",
                failures);
        Require(string.Equals(outageTarget.MapSubjectId, authoredUnavailableNodeId,
                    StringComparison.Ordinal) &&
                string.Equals(selectedOutagePresentation.World.SelectedAssetId,
                    authoredUnavailableNodeId, StringComparison.Ordinal) &&
                string.Equals(selectedOutagePresentation.Context.SubjectId,
                    outageItem.Id, StringComparison.Ordinal) &&
                outageHighlight is not null &&
                outageHighlight.NodeIds.Contains(
                    authoredUnavailableNodeId, StringComparer.Ordinal),
            "planned-outage target did not preserve its independently authored " +
            $"unavailable node {authoredUnavailableNodeId}",
            failures);
        }
        finally
        {
            RemoveAndFree(typedViewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var activeSlice = CreateRunningAuditSlice();
        using var activeLifetime = activeSlice.FreeAfterSmoke();
        long activeMinute = activeSlice.CoreSnapshot.Forecast.Events
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .First().StartMinute;
        activeSlice.AdvanceToForSmoke(activeMinute);
        RealtimeTimelineItemPresentation activeItem = activeSlice.LatestPresentation.Rail.Items
            .First(item => item.IsCurrent &&
                item.Visibility == RealtimeTimelineVisibility.Active &&
                RealtimeSlicePresenter.ResolveTimelineTarget(
                    activeSlice.DisplayWorldForSmoke,
                    activeSlice.CoreSnapshot,
                    item.Id).Kind == RealtimeTimelineTargetKind.Event);
        RealtimeTimelineItemPresentation selectedItem = activeSlice.LatestPresentation.Rail.Items
            .Where(item => !item.IsCurrent &&
                item.Lane != activeItem.Lane)
            .OrderByDescending(item => item.StartMinute)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .First();
        activeSlice.ChooseTimelineClusterForSmoke(new[] { selectedItem.Id });
        (SubViewport activeViewport, RealtimeUiRoot activeRoot) = await CreateOffscreenUi(
            RealtimeUiMetrics.ReferenceResolution,
            RealtimeUiMetrics.ReferenceResolution,
            100,
            activeSlice.LatestPresentation);
        try
        {
            IReadOnlyList<RealtimeUiSmokeMarkerFact> markerFacts =
                activeRoot.EventRailForSmoke.MarkerFactsForSmoke();
            RealtimeUiSmokeMarkerFact activeMarker = markerFacts.Single(marker =>
                marker.ItemIds.Contains(activeItem.Id, StringComparer.Ordinal));
            RealtimeUiSmokeMarkerFact selectedMarker = markerFacts.Single(marker =>
                marker.ItemIds.Contains(selectedItem.Id, StringComparer.Ordinal));
            Require(!activeMarker.ItemIds.Contains(selectedItem.Id,
                        StringComparer.Ordinal) &&
                    !activeMarker.Selected &&
                    activeMarker.OutlineSize >= 2 &&
                    activeMarker.VisibleText.StartsWith("진행 중 · ",
                        StringComparison.Ordinal) &&
                    selectedMarker.Selected &&
                    !selectedMarker.VisibleText.StartsWith("진행 중 · ",
                        StringComparison.Ordinal),
                "timeline conflated active-now outline/text with selected pressed state " +
                $"(activeText={activeMarker.VisibleText}, " +
                $"activeSelected={activeMarker.Selected}, " +
                $"activeOutline={activeMarker.OutlineSize}, " +
                $"selectedText={selectedMarker.VisibleText}, " +
                $"selectedPressed={selectedMarker.Selected}, " +
                $"selectedOutline={selectedMarker.OutlineSize})",
                failures);
        }
        finally
        {
            RemoveAndFree(activeViewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        RealtimeTimelineItemPresentation mixedCurrent = activeItem with
        {
            Id = "SMOKE_MIXED_CURRENT",
            StartMinute = activeMinute,
            EndMinute = activeMinute + 30,
            IsCurrent = true,
            Visibility = RealtimeTimelineVisibility.Active,
            Lane = RealtimeTimelineLane.DemandAndDeadline,
            Priority = 10,
            TimeLabel = "현재",
        };
        RealtimeTimelineItemPresentation mixedSelected = selectedItem with
        {
            Id = "SMOKE_MIXED_SELECTED",
            StartMinute = activeMinute,
            EndMinute = null,
            IsCurrent = false,
            Visibility = RealtimeTimelineVisibility.Announced,
            Lane = RealtimeTimelineLane.DemandAndDeadline,
            Priority = 20,
            TimeLabel = "곧",
        };
        RealtimeSlicePresentation mixedPresentation = activeSlice.LatestPresentation with
        {
            Rail = activeSlice.LatestPresentation.Rail with
            {
                Items = new[] { mixedCurrent, mixedSelected },
                SelectedItemId = mixedSelected.Id,
            },
        };
        (SubViewport mixedViewport, RealtimeUiRoot mixedRoot) = await CreateOffscreenUi(
            RealtimeUiMetrics.ReferenceResolution,
            RealtimeUiMetrics.ReferenceResolution,
            100,
            mixedPresentation);
        try
        {
            RealtimeUiSmokeMarkerFact mixedMarker = mixedRoot.EventRailForSmoke
                .MarkerFactsForSmoke().Single();
            Require(mixedMarker.ItemIds.Count == 2 &&
                    mixedMarker.ItemIds.Contains(mixedCurrent.Id, StringComparer.Ordinal) &&
                    mixedMarker.ItemIds.Contains(mixedSelected.Id, StringComparer.Ordinal) &&
                    mixedMarker.Selected &&
                    mixedMarker.OutlineSize >= 2 &&
                    string.Equals(mixedMarker.SemanticItemId, mixedSelected.Id,
                        StringComparison.Ordinal) &&
                    !mixedMarker.VisibleText.StartsWith("진행 중 · ",
                        StringComparison.Ordinal) &&
                    mixedMarker.VisibleText.Contains("진행 1건", StringComparison.Ordinal) &&
                    mixedMarker.AccessibilityName.Contains("진행 중.",
                        StringComparison.Ordinal),
                "mixed current-sibling/non-current-selected cluster lost pressed, " +
                "outline, semantic-ID, visible-current, or AX-current semantics",
                failures);

            (long beforeBoundary, long afterBoundary) = mixedRoot.EventRailForSmoke
                .LegacyBucketBoundaryPairForSmoke();
            RealtimeTimelineItemPresentation boundaryBefore = mixedSelected with
            {
                Id = "SMOKE_BUCKET_BOUNDARY_BEFORE",
                StartMinute = beforeBoundary,
                Priority = 1,
                TimeLabel = "경계 직전",
            };
            RealtimeTimelineItemPresentation boundaryAfter = mixedSelected with
            {
                Id = "SMOKE_BUCKET_BOUNDARY_AFTER",
                StartMinute = afterBoundary,
                Priority = 2,
                TimeLabel = "경계 직후",
            };
            RealtimeSlicePresentation boundaryPresentation = mixedPresentation with
            {
                Rail = mixedPresentation.Rail with
                {
                    Items = new[] { boundaryBefore, boundaryAfter },
                    SelectedItemId = null,
                },
            };
            Present(mixedRoot, boundaryPresentation);
            await SettleLayout();
            RealtimeUiSmokeMarkerFact[] boundaryMarkers = mixedRoot.EventRailForSmoke
                .MarkerFactsForSmoke().ToArray();
            Require(boundaryMarkers.Length == 1 &&
                    boundaryMarkers[0].ItemIds.SequenceEqual(
                        new[] { boundaryBefore.Id, boundaryAfter.Id },
                        StringComparer.Ordinal),
                "same-lane markers straddling a legacy bucket edge were not clustered " +
                "before their visible rectangles could intersect",
                failures);
        }
        finally
        {
            RemoveAndFree(mixedViewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var completedSlice = CreateRunningAuditSlice();
        using var completedLifetime = completedSlice.FreeAfterSmoke();
        long completedMinute = completedSlice.SmokeBoundaryFacts.Events
            .OrderBy(item => item.EndMinute)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .First().EndMinute;
        completedSlice.AdvanceToForSmoke(completedMinute);
        RealtimeTimelineItemPresentation completedItem =
            completedSlice.LatestPresentation.Rail.Items.First(item =>
                item.Visibility == RealtimeTimelineVisibility.Completed);
        (SubViewport completedViewport, RealtimeUiRoot completedRoot) =
            await CreateOffscreenUi(
                RealtimeUiMetrics.ReferenceResolution,
                RealtimeUiMetrics.ReferenceResolution,
                100,
                completedSlice.LatestPresentation);
        try
        {
            RealtimeUiSmokeMarkerFact completedMarker = completedRoot.EventRailForSmoke
                .MarkerFactsForSmoke().Single(marker => marker.ItemIds.Contains(
                    completedItem.Id, StringComparer.Ordinal));
            Require(completedMarker.VisibleText.StartsWith("완료 · ",
                        StringComparison.Ordinal) &&
                    completedMarker.AccessibilityName.Contains("완료됨.",
                        StringComparison.Ordinal),
                "recent completed event lacked distinct visible and AX completion state",
                failures);
        }
        finally
        {
            RemoveAndFree(completedViewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        RealtimeLayoutProfile label100 = RealtimeUiMetrics.ForWindow(
            new Vector2I(1920, 1080), 100);
        RealtimeLayoutProfile label200 = RealtimeUiMetrics.ForWindow(
            new Vector2I(1920, 1080), 200);
        var map = new RealtimePlaceholderMap();
        try
        {
            map.ApplyLayoutForSmoke(label100);
            int font100 = map.LabelFontSizeForSmoke;
            map.ApplyLayoutForSmoke(label200);
            int font200 = map.LabelFontSizeForSmoke;
            Require(font100 == Mathf.RoundToInt(12f * label100.AccessibilityScale) &&
                    font200 == Mathf.RoundToInt(12f * label200.AccessibilityScale) &&
                    font200 == font100 * 2,
                "world map labels ignored the 100/200% UI accessibility scale",
                failures);
            Require(map.StatusLabelForSmoke(
                        RealtimeWorldAssetState.Emergency) == "비상 운전" &&
                    map.StatusLabelForSmoke(
                        RealtimeWorldAssetState.ProtectiveOutage) == "보호정지" &&
                    map.StatusLabelForSmoke(
                        RealtimeWorldAssetState.OverLimit) == "한계 초과",
                "map thermal states lost their non-color text/AX labels",
                failures);
            Require(map.StateCueForSmoke(
                        RealtimeWorldAssetState.Emergency) ==
                        RealtimePlaceholderStateCue.EmergencyTriangle &&
                    map.StateCueForSmoke(
                        RealtimeWorldAssetState.ProtectiveOutage) ==
                        RealtimePlaceholderStateCue.ProtectiveOutageCross &&
                    map.StateCueForSmoke(
                        RealtimeWorldAssetState.OverLimit) ==
                        RealtimePlaceholderStateCue.OverLimitDiamond,
                "map thermal states lost their non-color geometric cues",
                failures);
        }
        finally
        {
            map.Free();
        }
    }

    private async Task ValidateLockedSpeedPresentation(
        RealtimeSlicePresentation baseline,
        RealtimeSimulationState simulation,
        RealtimePauseReason reason,
        string expectedVisibleStatus,
        ICollection<string> failures)
    {
        RealtimeTimelineItemPresentation next = baseline.Rail.Items
            .Where(item => item.StartMinute > baseline.Rail.NowMinute)
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .First();
        var pause = new RealtimePausePresentation(
            reason,
            baseline.Rail.NowMinute,
            baseline.Rail.NowLabel,
            next.Id,
            next.StartMinute,
            $"{next.TimeLabel} · {next.ShortLabel}");
        RealtimeSlicePresentation locked = baseline with
        {
            Hud = baseline.Hud with
            {
                SimulationState = simulation,
                Speed = RealtimeSimulationSpeed.Paused,
                Pause = pause,
            },
            Modal = null,
        };
        (SubViewport viewport, RealtimeUiRoot root) = await CreateOffscreenUi(
            RealtimeUiMetrics.ReferenceResolution,
            RealtimeUiMetrics.ReferenceResolution,
            100,
            locked);
        int requests = 0;
        void Observe(RealtimeSimulationSpeed _) => requests++;
        root.SpeedRequested += Observe;
        try
        {
            RealtimeUiSmokeSpeedFact[] before = root.TopHudForSmoke.SpeedFactsForSmoke
                .ToArray();
            Require(before.All(item => !item.Enabled) &&
                    before.Count(item => item.Pressed) == 1 &&
                    before.Single(item => item.Pressed).Speed ==
                        RealtimeSimulationSpeed.Paused &&
                    root.TopHudForSmoke.PauseStatusTextForSmoke.Contains(
                        expectedVisibleStatus, StringComparison.Ordinal) &&
                    before.Where(item => item.Speed != RealtimeSimulationSpeed.Paused)
                        .All(item => item.Tooltip.Contains(
                            "바꿀 수 없습니다", StringComparison.Ordinal)),
                $"{simulation} HUD did not visibly lock every speed at paused state",
                failures);
            if (simulation == RealtimeSimulationState.Ended)
            {
                RealtimeUiSmokeSpeedFact ended = before.Single(item =>
                    item.Speed == RealtimeSimulationSpeed.Paused);
                Require(!ended.Tooltip.Contains("재개", StringComparison.Ordinal) &&
                        !ended.Tooltip.Contains("(P)", StringComparison.Ordinal) &&
                        ended.AccessibilityName == "운영 종료" &&
                        ended.AccessibilityDescription.Contains(
                            "운영이 종료", StringComparison.Ordinal),
                    "ended pause control advertised a nonexistent resume shortcut",
                    failures);
            }
            PushViewportPrimary(
                viewport,
                before.Single(item => item.Speed == RealtimeSimulationSpeed.Normal)
                    .Rect.GetCenter());
            await SettleLayout();
            RealtimeUiSmokeSpeedFact[] after = root.TopHudForSmoke.SpeedFactsForSmoke
                .ToArray();
            Require(requests == 0 &&
                    after.Select(item => (item.Speed, item.Enabled, item.Pressed))
                        .SequenceEqual(before.Select(item =>
                            (item.Speed, item.Enabled, item.Pressed))),
                $"disabled {simulation} speed control emitted or visually desynchronized",
                failures);
        }
        finally
        {
            root.SpeedRequested -= Observe;
            RemoveAndFree(viewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static RealtimeSliceMain CreateRunningAuditSlice()
    {
        var slice = new RealtimeSliceMain();
        try
        {
            slice.BootstrapForSmoke();
            string modalId = slice.InteractionState.ActiveModalId ??
                throw new InvalidOperationException(
                    "Audit slice did not expose its chapter briefing.");
            RealtimeR2IntentResult close = slice.ApplyIntentForSmoke(
                RealtimeR2Intent.CloseModal(modalId));
            if (!close.Accepted ||
                slice.InteractionState.Simulation != RealtimeSimulationState.Running)
            {
                throw new InvalidOperationException(
                    $"Audit slice could not close its briefing: {close.Error}");
            }
            return slice;
        }
        catch
        {
            slice.Free();
            throw;
        }
    }

    private static void ValidateScaledCustomMapHitTargets(
        RealtimePlaceholderMap map,
        string selectedAssetId,
        RealtimeLayoutProfile restoreProfile,
        ICollection<string> failures)
    {
        (Vector2I Physical, int Scale)[] profiles =
        [
            (new Vector2I(1920, 1080), 100),
            (new Vector2I(1920, 1080), 125),
            (new Vector2I(1920, 1080), 150),
            (new Vector2I(1920, 1080), 200),
            (new Vector2I(3840, 2160), 100),
            (new Vector2I(3840, 2160), 125),
            (new Vector2I(3840, 2160), 150),
            (new Vector2I(3840, 2160), 200),
            (new Vector2I(2560, 1440), 100),
            (new Vector2I(2560, 1440), 200),
        ];
        try
        {
            foreach ((Vector2I physical, int scale) in profiles)
            {
                string label = $"{physical.X}x{physical.Y}@{scale}%";
                RealtimeLayoutProfile profile = RealtimeUiMetrics.ForWindow(
                    physical,
                    scale);
                map.ApplyLayoutForSmoke(profile);
                float insideOffset = profile.MinimumHitTarget / 2f - 0.25f;
                float outsideOffset = profile.MinimumHitTarget / 2f + 0.75f;

                (string AssetId, Vector2 CanvasPoint)? action =
                    map.SelectionActionCanvasPointForSmoke;
                Require(action is not null && string.Equals(
                            action.Value.AssetId,
                            selectedAssetId,
                            StringComparison.Ordinal),
                    $"{label} did not expose the selected action hit probe",
                    failures);
                if (action is not null)
                {
                    string actionId = $"ACTION:INSPECT:{selectedAssetId}";
                    RealtimePointerResolution actionInside =
                        map.ResolveCanvasPointForSmoke(
                            action.Value.CanvasPoint + Vector2.Right * insideOffset);
                    RealtimePointerResolution actionOutside =
                        map.ResolveCanvasPointForSmoke(
                            action.Value.CanvasPoint + Vector2.Right * outsideOffset);
                    Require(actionInside.Owner == RealtimePointerOwner.SelectionAction &&
                            string.Equals(actionInside.ResolvedId, actionId,
                                StringComparison.Ordinal) &&
                            actionInside.OrderedCandidates.Any(item => string.Equals(
                                item.Id, actionId, StringComparison.Ordinal)) &&
                            actionOutside.OrderedCandidates.All(item => !string.Equals(
                                item.Id, actionId, StringComparison.Ordinal)),
                        $"{label} actual selection-action resolver missed its scaled " +
                        "minimum boundary or leaked beyond it",
                        failures);
                }

                (string edgeId, Vector2 edgePoint, Vector2 edgeNormal) =
                    map.EdgeHitProbeForSmoke();
                RealtimePointerResolution edgeInside = map.ResolveCanvasPointForSmoke(
                    edgePoint + edgeNormal * insideOffset);
                RealtimePointerResolution edgeOutside = map.ResolveCanvasPointForSmoke(
                    edgePoint + edgeNormal * outsideOffset);
                Require(edgeInside.OrderedCandidates.Any(item => string.Equals(
                            item.Id, edgeId, StringComparison.Ordinal)) &&
                        edgeOutside.OrderedCandidates.All(item => !string.Equals(
                            item.Id, edgeId, StringComparison.Ordinal)),
                    $"{label} actual edge resolver missed its scaled minimum boundary " +
                    $"or leaked beyond it ({edgeId})",
                    failures);
            }
        }
        finally
        {
            map.ApplyLayoutForSmoke(restoreProfile);
        }
    }

    private async Task ValidateNonModalFocusTraversal(
        SubViewport viewport,
        RealtimeUiRoot uiRoot,
        string label,
        ICollection<string> failures)
    {
        BaseButton[] targets = uiRoot.FocusableButtonsForSmoke().ToArray();
        Require(targets.Length > 0,
            $"{label} has no enabled nonmodal keyboard target", failures);
        if (targets.Length == 0)
        {
            return;
        }

        string[] expectedPaths = targets
            .Select(item => item.GetPath().ToString())
            .ToArray();
        await ValidateFocusDirection(
            viewport,
            uiRoot,
            targets[0],
            expectedPaths,
            backwards: false,
            label,
            failures);
        await ValidateFocusDirection(
            viewport,
            uiRoot,
            targets[^1],
            expectedPaths,
            backwards: true,
            label,
            failures);
        uiRoot.FocusOwnerForSmoke?.ReleaseFocus();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task ValidateFocusDirection(
        SubViewport viewport,
        RealtimeUiRoot uiRoot,
        BaseButton start,
        IReadOnlyList<string> expectedPaths,
        bool backwards,
        string label,
        ICollection<string> failures)
    {
        var expected = expectedPaths.ToHashSet(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        start.GrabFocus();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (int step = 0; step < expectedPaths.Count; step++)
        {
            Control? focus = uiRoot.FocusOwnerForSmoke;
            if (focus is null)
            {
                break;
            }
            visited.Add(focus.GetPath().ToString());
            PushViewportKey(
                viewport,
                Key.Tab,
                pressed: true,
                shiftPressed: backwards);
            PushViewportKey(
                viewport,
                Key.Tab,
                pressed: false,
                shiftPressed: backwards);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        string? finalPath = uiRoot.FocusOwnerForSmoke?.GetPath().ToString();
        string startPath = start.GetPath().ToString();
        Require(visited.SetEquals(expected) &&
                string.Equals(finalPath, startPath, StringComparison.Ordinal),
            $"{label} actual {(backwards ? "Shift+Tab" : "Tab")} traversal " +
            $"did not reach every enabled target exactly once and wrap " +
            $"(expected=[{string.Join(",", expected.OrderBy(item => item, StringComparer.Ordinal))}], " +
            $"visited=[{string.Join(",", visited.OrderBy(item => item, StringComparer.Ordinal))}], " +
            $"final={finalPath ?? "<none>"}, start={startPath})",
            failures);
    }

    private static IReadOnlySet<string> ExpectedVisibleSurfaces(
        RealtimeSlicePresentation presentation)
    {
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "TopHud",
            "EventRail",
        };
        bool timelineOwnsWorkspace = presentation.Rail.Expanded;
        bool contextVisible = presentation.Context.Visible && !timelineOwnsWorkspace;
        bool contextOwnsPrimary = contextVisible &&
            presentation.Context.PrimaryAction is { Visible: true };
        bool actionVisible = !contextOwnsPrimary && !timelineOwnsWorkspace &&
            presentation.ActionDock is
            {
                Visible: true,
                PrimaryAction.Visible: true,
            };
        bool buildVisible = !contextOwnsPrimary && !actionVisible &&
            !timelineOwnsWorkspace && presentation.BuildShelf.Visible;
        if (contextVisible)
        {
            expected.Add("ContextDock");
        }
        if (actionVisible)
        {
            expected.Add("ActionDock");
        }
        if (buildVisible)
        {
            expected.Add("BuildShelf");
        }
        return expected;
    }

    private static int ExpectedPrimaryCtaCount(RealtimeSlicePresentation presentation)
    {
        IReadOnlySet<string> visible = ExpectedVisibleSurfaces(presentation);
        int count = 0;
        if (visible.Contains("ContextDock") &&
            presentation.Context.PrimaryAction is
            {
                Visible: true,
                Tone: RealtimeActionTone.Primary,
            })
        {
            count++;
        }
        if (visible.Contains("ActionDock") &&
            presentation.ActionDock.PrimaryAction is
            {
                Visible: true,
                Tone: not RealtimeActionTone.Destructive,
            })
        {
            count++;
        }
        if (presentation.Modal?.PrimaryAction is
            {
                Visible: true,
                Tone: RealtimeActionTone.Primary,
            })
        {
            count++;
        }
        return count;
    }

    private async Task ValidateActualLiveClock(ICollection<string> failures)
    {
        Vector2I logical = new(
            Mathf.RoundToInt(RealtimeUiMetrics.ReferenceResolution.X),
            Mathf.RoundToInt(RealtimeUiMetrics.ReferenceResolution.Y));
        var viewport = new SubViewport
        {
            Name = "ActualRealtimeLiveClockSmokeViewport",
            Size = logical,
            Size2DOverride = logical,
            Size2DOverrideStretch = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            HandleInputLocally = true,
        };
        AddChild(viewport);
        viewport.NotifyMouseEntered();
        PackedScene scene = GD.Load<PackedScene>(
            "res://realtime/r2/RealtimeSliceMain.tscn");
        RealtimeSliceMain slice = scene.Instantiate<RealtimeSliceMain>();
        viewport.AddChild(slice);
        try
        {
            await SettleLayout();
            string modalId = slice.InteractionState.ActiveModalId ?? string.Empty;
            Require(slice.ApplyIntentForSmoke(
                    RealtimeR2Intent.CloseModal(modalId)).Accepted,
                "actual live-clock fixture could not close its chapter modal",
                failures);
            await SettleLayout();
            Require(slice.InteractionState.Simulation ==
                        RealtimeSimulationState.Running &&
                    slice.AutonomousClockEnabledForSmoke,
                "actual RealtimeSliceMain did not enable its autonomous _Process clock",
                failures);

            long beforeMinute = slice.CurrentMinute;
            string beforeHash = slice.CanonicalStateSha256;
            int beforeCommands = slice.AcceptedCommandCount;
            long beforeRevision = slice.PresentationRevision;
            int beforePointerClaims = slice.PointerClickCounters.Values.Sum();
            RealtimeFrameAccumulatorSnapshot beforeAccumulator =
                slice.AccumulatorSnapshot;
            int observedProcessFrames = 0;
            const int maximumProcessFrames = 480;
            while (slice.CurrentMinute == beforeMinute &&
                   observedProcessFrames < maximumProcessFrames)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                observedProcessFrames++;
            }
            long observedMinutes = slice.CurrentMinute - beforeMinute;
            Require(observedProcessFrames > 0 &&
                    observedProcessFrames <= maximumProcessFrames &&
                    observedMinutes > 0 &&
                    slice.AccumulatorSnapshot.AppliedSimulationMinutes ==
                        beforeAccumulator.AppliedSimulationMinutes + observedMinutes &&
                    slice.LatestPresentation.World.Minute == slice.CurrentMinute &&
                    slice.PresentationRevision > beforeRevision &&
                    !string.Equals(
                        slice.CanonicalStateSha256,
                        beforeHash,
                        StringComparison.Ordinal) &&
                    slice.AcceptedCommandCount == beforeCommands &&
                    slice.PointerClickCounters.Values.Sum() == beforePointerClaims &&
                    slice.InteractionState.Simulation ==
                        RealtimeSimulationState.Running,
                "actual live _Process did not advance the no-click Core clock " +
                $"(frames={observedProcessFrames}/{maximumProcessFrames}, " +
                $"minutes={observedMinutes}, " +
                $"revision={slice.PresentationRevision}/{beforeRevision}+)",
                failures);

            RealtimeUiRoot ui = slice.UiForSmoke;
            PushViewportPrimary(
                viewport,
                ui.TopHudForSmoke.SpeedCenterForSmoke(
                    RealtimeSimulationSpeed.Paused));
            await SettleLayout();
            long pausedMinute = slice.CurrentMinute;
            string pausedHash = slice.CanonicalStateSha256;
            int pausedCommands = slice.AcceptedCommandCount;
            long pausedRevision = slice.PresentationRevision;
            RealtimeFrameAccumulatorSnapshot pausedAccumulator =
                slice.AccumulatorSnapshot;
            Require(slice.InteractionState.Simulation ==
                        RealtimeSimulationState.PlayerPaused &&
                    slice.InteractionState.PauseReason ==
                        RealtimePauseReason.PlayerRequest &&
                    pausedAccumulator.Paused &&
                    slice.AutonomousClockEnabledForSmoke,
                "actual live-clock pause control did not pause reducer/frame state",
                failures);
            for (int frame = 0; frame < 120; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Require(slice.CurrentMinute == pausedMinute &&
                    string.Equals(
                        slice.CanonicalStateSha256,
                        pausedHash,
                        StringComparison.Ordinal) &&
                    slice.AcceptedCommandCount == pausedCommands &&
                    slice.PresentationRevision == pausedRevision &&
                    slice.AccumulatorSnapshot == pausedAccumulator &&
                    slice.InteractionState.Simulation ==
                        RealtimeSimulationState.PlayerPaused,
                "actual paused _Process frames changed Core minute/hash/journal/" +
                "presentation/accumulator",
                failures);
            GD.Print(
                "REALTIME_R2_ACTUAL_LIVE_CLOCK_PASS no-click-process-minute " +
                $"frames={observedProcessFrames}; minutes={observedMinutes}; " +
                "paused-process-frames=120");
        }
        finally
        {
            RemoveAndFree(viewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task ValidateActualSliceSceneEndToEnd(
        ICollection<string> failures)
    {
        int failureCountBefore = failures.Count;
        Vector2I logical = new(
            Mathf.RoundToInt(RealtimeUiMetrics.ReferenceResolution.X),
            Mathf.RoundToInt(RealtimeUiMetrics.ReferenceResolution.Y));
        var viewport = new SubViewport
        {
            Name = "ActualRealtimeSliceSceneSmokeViewport",
            Size = logical,
            Size2DOverride = logical,
            Size2DOverrideStretch = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            HandleInputLocally = true,
        };
        AddChild(viewport);
        PackedScene scene = GD.Load<PackedScene>(
            "res://realtime/r2/RealtimeSliceMain.tscn");
        RealtimeSliceMain slice = scene.Instantiate<RealtimeSliceMain>();
        viewport.AddChild(slice);
        var actualInputRequests = new List<RealtimeInputRequest>();
        RealtimeUiRoot? observedUi = null;
        Action<RealtimeInputRequest>? observeInput = null;
        viewport.NotifyMouseEntered();
        try
        {
            await SettleLayout();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            RealtimeUiRoot ui = slice.UiForSmoke;
            RealtimePlaceholderMap map = slice.MapForSmoke;
            observedUi = ui;
            observeInput = request => actualInputRequests.Add(request);
            ui.InputRequested += observeInput;
            RealtimeSlicePresentation bootstrapPresentation = slice.LatestPresentation;
            Require(slice.InteractionState.Surface == RealtimeSurface.BlockingModal &&
                    ui.ModalHostForSmoke.Depth == 1 &&
                    ui.ModalHostForSmoke.OwnsFocusForSmoke &&
                    !string.IsNullOrWhiteSpace(
                        ui.TopHudForSmoke.ObjectiveTextForSmoke) &&
                    ui.TopHudForSmoke.ObjectiveAccessibilityForSmoke.Contains(
                        bootstrapPresentation.Hud.Objective,
                        StringComparison.Ordinal) &&
                    ui.TopHudForSmoke.PauseStatusTextForSmoke.Contains(
                        "장 안내 정지", StringComparison.Ordinal) &&
                    ui.TopHudForSmoke.PauseStatusAccessibilityForSmoke.Contains(
                        "장 안내 정지", StringComparison.Ordinal),
                "actual slice scene did not bootstrap/wire its chapter modal",
                failures);
            RealtimeModalPresentation bootstrapModal = bootstrapPresentation.Modal ??
                throw new InvalidOperationException(
                    "Bootstrap interaction has no visible chapter modal presentation.");
            Require(ui.ModalHostForSmoke.PauseStatusTextForSmoke.Contains(
                        bootstrapModal.Pause.CurrentTimeLabel,
                        StringComparison.Ordinal) &&
                    ui.ModalHostForSmoke.PauseStatusTextForSmoke.Contains(
                        bootstrapModal.Pause.NextEventLabel,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        ui.ModalHostForSmoke.PauseStatusTextForSmoke,
                        ui.ModalHostForSmoke.PauseStatusAccessibilityForSmoke,
                        StringComparison.Ordinal) &&
                    ui.ModalHostForSmoke.AccessibilitySummaryForSmoke.Contains(
                        bootstrapModal.Pause.CurrentTimeLabel,
                        StringComparison.Ordinal) &&
                    ui.ModalHostForSmoke.AccessibilitySummaryForSmoke.Contains(
                        bootstrapModal.Pause.NextEventLabel,
                        StringComparison.Ordinal) &&
                    ui.TopHudForSmoke.SpeedFactsForSmoke.All(item =>
                        !item.Enabled) &&
                    ui.TopHudForSmoke.SpeedFactsForSmoke.Count(item => item.Pressed) == 1 &&
                    ui.TopHudForSmoke.SpeedFactsForSmoke.Single(item => item.Pressed)
                        .Speed == RealtimeSimulationSpeed.Paused,
                "chapter modal did not visibly expose pause reason/current time/next " +
                "event or lock the HUD at paused state",
                failures);

            slice.ResetPointerClickCountersForSmoke();
            int blockedCommands = slice.AcceptedCommandCount;
            long blockedRevision = slice.PresentationRevision;
            PushViewportPrimary(
                viewport,
                ui.TopHudForSmoke.SpeedCenterForSmoke(
                    RealtimeSimulationSpeed.Paused));
            await SettleLayout();
            Require(slice.InteractionState.Surface == RealtimeSurface.BlockingModal &&
                    ui.ModalHostForSmoke.Depth == 1 &&
                    slice.AcceptedCommandCount == blockedCommands &&
                    slice.PresentationRevision == blockedRevision &&
                    slice.PointerClickCounters.Values.Sum() == 0,
                "actual blocking modal did not intercept an underlying HUD click",
                failures);
            int modalKeyRequests = actualInputRequests.Count;
            PushViewportKey(viewport, Key.A, pressed: true);
            PushViewportKey(viewport, Key.A, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == modalKeyRequests &&
                    slice.InteractionState.Surface == RealtimeSurface.BlockingModal &&
                    slice.InteractionState.Tool == RealtimeTool.Inspect &&
                    slice.PresentationRevision == blockedRevision,
                "blocking modal allowed a lower-priority analysis key to escape",
                failures);

            int modalCommands = slice.AcceptedCommandCount;
            long modalRevision = slice.PresentationRevision;
            PushViewportPrimary(
                viewport,
                ui.ModalHostForSmoke.PrimaryCenterForSmoke);
            await SettleLayout();
            Require(slice.InteractionState.Simulation == RealtimeSimulationState.Running &&
                    slice.InteractionState.ActiveModalId is null &&
                    ui.ModalHostForSmoke.Depth == 0 &&
                    ReferenceEquals(ui.FocusOwnerForSmoke, map) &&
                    slice.AcceptedCommandCount == modalCommands &&
                    slice.PresentationRevision == modalRevision + 1 &&
                    slice.PointerClickCounters.Values.Sum() == 0,
                "actual bootstrap modal did not close once and restore map fallback focus",
                failures);

            int hudCommands = slice.AcceptedCommandCount;
            long hudRevision = slice.PresentationRevision;
            PushViewportPrimary(
                viewport,
                ui.TopHudForSmoke.SpeedCenterForSmoke(
                    RealtimeSimulationSpeed.Paused));
            await SettleLayout();
            Require(slice.InteractionState.Simulation ==
                        RealtimeSimulationState.PlayerPaused &&
                    slice.AcceptedCommandCount == hudCommands &&
                    slice.PresentationRevision == hudRevision + 1 &&
                    slice.PointerClickCounters.Values.Sum() == 0,
                "actual top-HUD click did not reach exactly one pause owner",
                failures);

            long resumeRevision = slice.PresentationRevision;
            PushViewportPrimary(
                viewport,
                ui.TopHudForSmoke.SpeedCenterForSmoke(
                    RealtimeSimulationSpeed.Paused));
            await SettleLayout();
            Require(slice.InteractionState.Simulation == RealtimeSimulationState.Running &&
                    slice.AcceptedCommandCount == hudCommands &&
                    slice.PresentationRevision == resumeRevision + 1,
                "second actual top-HUD pause click did not resume exactly once",
                failures);

            // The actual scene has proven mouse pause/resume. Freeze the
            // remaining input assertions so an autonomous minute cannot race
            // an exact one-revision UI reduction on a slower smoke host.
            slice.SetPlayerPausedForSmoke(true);
            Require(slice.InteractionState.Simulation ==
                    RealtimeSimulationState.PlayerPaused,
                "actual pointer/input fixture could not freeze autonomous time",
                failures);

            var worldPoint = slice.SmokeBoundaryFacts.PointerPoints.Single(item =>
                string.Equals(item.Id, "WORLD", StringComparison.Ordinal)).WorldPoint;
            slice.RestoreCameraForSmoke(new RealtimeMapCameraSnapshot(
                new Vector2(worldPoint.XUnit, worldPoint.YUnit),
                ZoomIndex: 0));
            await SettleLayout();
            Vector2 worldCanvas = map.ViewportPointForSmoke(worldPoint);
            PushViewportPointerMotion(viewport, worldCanvas);
            await SettleLayout();
            slice.ResetPointerClickCountersForSmoke();
            int worldCommands = slice.AcceptedCommandCount;
            long worldRevision = slice.PresentationRevision;
            PushViewportPrimary(viewport, worldCanvas, movePointer: false);
            await SettleLayout();
            Require(slice.PointerClickCounters.Values.Sum() == 1 &&
                    slice.PointerClickCounters[RealtimePointerOwner.WorldCandidate] == 1 &&
                    slice.InteractionState.SelectionId is not null &&
                    slice.AcceptedCommandCount == worldCommands &&
                    slice.PresentationRevision == worldRevision + 1,
                "actual viewport mouse click did not reach exactly one world " +
                $"owner/selection (point={worldCanvas}, map={map.GetGlobalRect()}, " +
                $"counters=[{string.Join(",", slice.PointerClickCounters.Select(item =>
                    $"{item.Key}:{item.Value}"))}], selection=" +
                $"{slice.InteractionState.SelectionId ?? "<none>"}, " +
                $"commands={slice.AcceptedCommandCount}/{worldCommands}, " +
                $"revision={slice.PresentationRevision}/{worldRevision + 1})",
                failures);

            string actionSelection = slice.InteractionState.SelectionId ?? string.Empty;
            Require(!string.IsNullOrWhiteSpace(actionSelection) &&
                    ui.ContextDockForSmoke.CloseAccessibilityNameForSmoke.Contains(
                        "상황 패널 닫기", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(
                        ui.ContextDockForSmoke.CloseAccessibilityDescriptionForSmoke),
                "selected context drawer close utility lost its explicit AX semantics",
                failures);
            int closeCommands = slice.AcceptedCommandCount;
            long closeRevision = slice.PresentationRevision;
            PushViewportPrimary(viewport, ui.ContextDockForSmoke.CloseCenterForSmoke);
            await SettleLayout();
            Require(slice.InteractionState.Surface == RealtimeSurface.World &&
                    string.Equals(slice.InteractionState.SelectionId, actionSelection,
                        StringComparison.Ordinal) &&
                    slice.AcceptedCommandCount == closeCommands &&
                    slice.PresentationRevision == closeRevision + 1,
                "actual context close did not preserve its selected world asset",
                failures);
            ValidateScaledCustomMapHitTargets(
                map,
                actionSelection,
                ui.LayoutProfile,
                failures);
            (string AssetId, Vector2 ViewportPoint)? actionHit =
                map.SelectionActionForSmoke;
            Require(actionHit is not null &&
                    string.Equals(actionHit.Value.AssetId, actionSelection,
                        StringComparison.Ordinal),
                "selected asset did not expose a live selection-action hit target",
                failures);
            if (actionHit is not null)
            {
                PushViewportPointerMotion(viewport, actionHit.Value.ViewportPoint);
                await SettleLayout();
                slice.ResetPointerClickCountersForSmoke();
                int actionHitCommands = slice.AcceptedCommandCount;
                long actionHitRevision = slice.PresentationRevision;
                PushViewportPrimary(
                    viewport,
                    actionHit.Value.ViewportPoint,
                    movePointer: false);
                await SettleLayout();
                Require(slice.PointerClickCounters.Values.Sum() == 1 &&
                        slice.PointerClickCounters[
                            RealtimePointerOwner.SelectionAction] == 1 &&
                        slice.PointerClickCounters[
                            RealtimePointerOwner.WorldCandidate] == 0 &&
                        slice.InteractionState.Surface ==
                            RealtimeSurface.Inspector &&
                        string.Equals(slice.InteractionState.SelectionId,
                            actionSelection, StringComparison.Ordinal) &&
                        string.Equals(slice.LatestPresentation.Context.SubjectId,
                            actionSelection, StringComparison.Ordinal) &&
                        slice.AcceptedCommandCount == actionHitCommands &&
                        slice.PresentationRevision == actionHitRevision + 1,
                    "live selection-action overlap did not exclusively reopen its " +
                    "owning asset inspector",
                    failures);
            }

            Require(slice.InteractionState.Simulation ==
                    RealtimeSimulationState.PlayerPaused,
                "actual keyboard-ownership fixture lost its autonomous-time freeze",
                failures);
            map.GrabFocus();
            PushViewportPointerMotion(
                viewport,
                map.ViewportPointForSmoke(worldPoint));
            await SettleLayout();
            int candidateCount = map.CandidateIdsForSmoke.Count;
            Require(candidateCount >= 2,
                "actual Q/E fixture needs overlapping stable world candidates", failures);
            int candidateBefore = map.CandidateIndexForSmoke;
            int keyCommands = slice.AcceptedCommandCount;
            long keyRevision = slice.PresentationRevision;
            int keyRequests = actualInputRequests.Count;
            PushViewportKey(viewport, Key.E, pressed: true);
            PushViewportKey(viewport, Key.E, pressed: false);
            await SettleLayout();
            map.QueueRedraw();
            viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            RenderingServer.ForceDraw(swapBuffers: false);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(candidateCount >= 2 &&
                    actualInputRequests.Count == keyRequests + 1 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.CycleCandidateNext &&
                    map.CandidateIndexForSmoke ==
                        (candidateBefore + 1) % Math.Max(1, candidateCount) &&
                    slice.InputOwnershipFacts.LastRequest?.Command ==
                        RealtimeInputCommand.CycleCandidateNext &&
                    slice.AcceptedCommandCount == keyCommands &&
                    slice.PresentationRevision == keyRevision,
                "actual E key was not owned exactly once by the candidate router",
                failures);
            string candidateAfterE = map.ActiveCandidateIdForSmoke ?? string.Empty;
            string candidateAtEIndex = map.CandidateIndexForSmoke >= 0 &&
                map.CandidateIndexForSmoke < map.CandidateIdsForSmoke.Count
                    ? map.CandidateIdsForSmoke[map.CandidateIndexForSmoke]
                    : string.Empty;
            Require(!string.IsNullOrWhiteSpace(candidateAfterE) &&
                    string.Equals(
                        candidateAfterE,
                        candidateAtEIndex,
                        StringComparison.Ordinal) &&
                    map.ActiveCandidateOutlineVisibleForSmoke &&
                    string.Equals(
                        map.DrawnActiveCandidateIdForSmoke,
                        candidateAfterE,
                        StringComparison.Ordinal) &&
                    map.ActiveCandidateVisibleLabelForSmoke.StartsWith(
                        $"후보 {map.CandidateIndexForSmoke + 1}/{candidateCount} · ",
                        StringComparison.Ordinal),
                "actual E key did not expose its exact active candidate with a visible " +
                "badge and geometry outline",
                failures);
            PushViewportKey(viewport, Key.Q, pressed: true);
            PushViewportKey(viewport, Key.Q, pressed: false);
            await SettleLayout();
            map.QueueRedraw();
            viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            RenderingServer.ForceDraw(swapBuffers: false);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(map.CandidateIndexForSmoke == candidateBefore &&
                    actualInputRequests.Count == keyRequests + 2 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.CycleCandidatePrevious &&
                    slice.InputOwnershipFacts.LastRequest?.Command ==
                        RealtimeInputCommand.CycleCandidatePrevious &&
                    slice.AcceptedCommandCount == keyCommands,
                "actual Q key was not owned exactly once by the candidate router",
                failures);
            Require(string.Equals(
                        map.ActiveCandidateIdForSmoke,
                        map.CandidateIndexForSmoke >= 0 &&
                        map.CandidateIndexForSmoke < map.CandidateIdsForSmoke.Count
                            ? map.CandidateIdsForSmoke[map.CandidateIndexForSmoke]
                            : string.Empty,
                        StringComparison.Ordinal) &&
                    map.ActiveCandidateOutlineVisibleForSmoke &&
                    string.Equals(
                        map.DrawnActiveCandidateIdForSmoke,
                        map.ActiveCandidateIdForSmoke,
                        StringComparison.Ordinal) &&
                    map.ActiveCandidateVisibleLabelForSmoke.StartsWith(
                        $"후보 {map.CandidateIndexForSmoke + 1}/{candidateCount} · ",
                        StringComparison.Ordinal),
                "actual Q key did not restore an exact visibly outlined candidate",
                failures);

            PushViewportKey(viewport, Key.E, pressed: true);
            PushViewportKey(viewport, Key.E, pressed: false);
            await SettleLayout();
            map.QueueRedraw();
            viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            RenderingServer.ForceDraw(swapBuffers: false);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string candidateToConfirm = map.ActiveCandidateIdForSmoke ?? string.Empty;
            Require(actualInputRequests.Count == keyRequests + 3 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.CycleCandidateNext &&
                    !string.IsNullOrWhiteSpace(candidateToConfirm) &&
                    string.Equals(candidateToConfirm, candidateAfterE,
                        StringComparison.Ordinal) &&
                    map.ActiveCandidateOutlineVisibleForSmoke &&
                    string.Equals(
                        map.DrawnActiveCandidateIdForSmoke,
                        candidateToConfirm,
                        StringComparison.Ordinal) &&
                    map.ActiveCandidateVisibleLabelForSmoke.StartsWith(
                        $"후보 {map.CandidateIndexForSmoke + 1}/{candidateCount} · ",
                        StringComparison.Ordinal),
                "second actual E key did not expose the same exact visible candidate " +
                "chosen for confirmation",
                failures);
            int candidateConfirmRequests = actualInputRequests.Count;
            int candidateConfirmCommands = slice.AcceptedCommandCount;
            PushViewportKey(viewport, Key.Enter, pressed: true);
            PushViewportKey(viewport, Key.Enter, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == candidateConfirmRequests + 1 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.ConfirmOrSelect &&
                    string.Equals(
                        slice.InteractionState.SelectionId,
                        candidateToConfirm,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        slice.LatestPresentation.Context.SubjectId,
                        candidateToConfirm,
                        StringComparison.Ordinal) &&
                    slice.AcceptedCommandCount == candidateConfirmCommands,
                "actual Enter did not select the exact ID named and outlined by Q/E",
                failures);

            await ValidateActualMapCandidatePersistence(
                viewport,
                slice,
                map,
                worldPoint,
                actualInputRequests,
                failures);

            var textEntry = new LineEdit
            {
                Name = "ActualViewportTextEntry",
                Position = new Vector2(32f, 500f),
                Size = new Vector2(260f, 48f),
            };
            viewport.AddChild(textEntry);
            textEntry.GrabFocus();
            await SettleLayout();
            int textEntryRequests = actualInputRequests.Count;
            PushViewportKey(viewport, Key.Space, pressed: true);
            PushViewportKey(viewport, Key.Space, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == textEntryRequests &&
                    !slice.InputOwnershipFacts.Panning,
                "actual text-entry-focused Space escaped into map pan ownership",
                failures);
            textEntry.ReleaseFocus();
            RemoveAndFree(textEntry);
            map.GrabFocus();
            await SettleLayout();

            int panRequests = actualInputRequests.Count;
            PushViewportKey(viewport, Key.Space, pressed: true);
            await SettleLayout();
            Require(slice.InputOwnershipFacts.LastRequest?.Command ==
                        RealtimeInputCommand.BeginPan &&
                    actualInputRequests.Count == panRequests + 1 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.BeginPan &&
                    slice.InputOwnershipFacts.Panning &&
                    slice.AcceptedCommandCount == keyCommands,
                "actual Space press did not enter one routed pan capture",
                failures);
            var releaseTextEntry = new LineEdit
            {
                Name = "ActualViewportPanReleaseTextEntry",
                Position = new Vector2(32f, 560f),
                Size = new Vector2(260f, 48f),
            };
            viewport.AddChild(releaseTextEntry);
            releaseTextEntry.GrabFocus();
            await SettleLayout();
            PushViewportKey(viewport, Key.Space, pressed: false);
            await SettleLayout();
            Require(slice.InputOwnershipFacts.LastRequest?.Command ==
                        RealtimeInputCommand.EndPan &&
                    actualInputRequests.Count == panRequests + 2 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.EndPan &&
                    !slice.InputOwnershipFacts.Panning &&
                    slice.AcceptedCommandCount == keyCommands,
                "actual Space release did not end the same routed pan capture " +
                $"(requests={actualInputRequests.Count}/{panRequests + 2}, " +
                $"last={actualInputRequests.LastOrDefault().Command}, " +
                $"router={slice.InputOwnershipFacts.LastRequest?.Command}, " +
                $"panning={slice.InputOwnershipFacts.Panning}, " +
                $"commands={slice.AcceptedCommandCount}/{keyCommands}, " +
                $"revision={slice.PresentationRevision}/{keyRevision})",
                failures);
            releaseTextEntry.ReleaseFocus();
            RemoveAndFree(releaseTextEntry);
            map.GrabFocus();
            await SettleLayout();

            BaseButton panFocusedButton = ui.TopHudForSmoke.MenuButtonForSmoke;
            panFocusedButton.GrabFocus();
            await SettleLayout();
            int buttonPanRequests = actualInputRequests.Count;
            int buttonPanCommands = slice.AcceptedCommandCount;
            long buttonPanRevision = slice.PresentationRevision;
            RealtimeSurface buttonPanSurface = slice.InteractionState.Surface;
            PushViewportKey(viewport, Key.Space, pressed: true);
            await SettleLayout();
            Require(actualInputRequests.Count == buttonPanRequests + 1 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.BeginPan &&
                    slice.InputOwnershipFacts.Panning &&
                    slice.InteractionState.Surface == buttonPanSurface &&
                    slice.AcceptedCommandCount == buttonPanCommands &&
                    slice.PresentationRevision == buttonPanRevision,
                "focused BaseButton consumed physical Space or activated instead of pan",
                failures);
            PushViewportKey(viewport, Key.Space, pressed: true, echo: true);
            await SettleLayout();
            Require(actualInputRequests.Count == buttonPanRequests + 1 &&
                    slice.InputOwnershipFacts.Panning &&
                    slice.InteractionState.Surface == buttonPanSurface &&
                    slice.PresentationRevision == buttonPanRevision,
                "captured Space echo emitted a duplicate pan/button action",
                failures);
            PushViewportKey(viewport, Key.Space, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == buttonPanRequests + 2 &&
                    actualInputRequests[^1].Command == RealtimeInputCommand.EndPan &&
                    !slice.InputOwnershipFacts.Panning &&
                    slice.InteractionState.Surface == buttonPanSurface &&
                    slice.AcceptedCommandCount == buttonPanCommands &&
                    slice.PresentationRevision == buttonPanRevision,
                "focused-button Space capture did not release exactly once",
                failures);
            map.GrabFocus();
            await SettleLayout();

            int analysisRequests = actualInputRequests.Count;
            int analysisCommands = slice.AcceptedCommandCount;
            long analysisRevision = slice.PresentationRevision;
            PushViewportKey(viewport, Key.A, pressed: true);
            PushViewportKey(viewport, Key.A, pressed: false);
            await SettleLayout();
            Require(slice.InputOwnershipFacts.LastRequest?.Command ==
                        RealtimeInputCommand.ToggleAnalysis &&
                    actualInputRequests.Count == analysisRequests + 1 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.ToggleAnalysis &&
                    slice.InteractionState.Tool == RealtimeTool.Analysis &&
                    slice.AcceptedCommandCount == analysisCommands &&
                    slice.PresentationRevision == analysisRevision + 1,
                "actual A key did not toggle analysis through the sole input router " +
                $"(requests={actualInputRequests.Count}/{analysisRequests + 1}, " +
                $"last={actualInputRequests.LastOrDefault().Command}, " +
                $"tool={slice.InteractionState.Tool}, " +
                $"revision={slice.PresentationRevision}/{analysisRevision + 1})",
                failures);
            await ForceActualMapDraw(viewport, map);
            string[] expectedDrawnRiskIds = slice.LatestPresentation.World
                .ActiveRiskAreaIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            Require(slice.LatestPresentation.World.AnalysisVisible &&
                    map.DrawnAnalysisOverlayForSmoke &&
                    map.DrawnAnalysisRiskAreaIdsForSmoke.SequenceEqual(
                        expectedDrawnRiskIds,
                        StringComparer.Ordinal),
                "actual A key did not reach the PlaceholderMap _Draw analysis/risk " +
                $"path (expected=[{string.Join(",", expectedDrawnRiskIds)}], " +
                $"drawn=[{string.Join(",", map.DrawnAnalysisRiskAreaIdsForSmoke)}])",
                failures);
            int inspectRequests = actualInputRequests.Count;
            long inspectRevision = slice.PresentationRevision;
            PushViewportKey(viewport, Key.A, pressed: true);
            PushViewportKey(viewport, Key.A, pressed: false);
            await SettleLayout();
            Require(slice.InteractionState.Tool == RealtimeTool.Inspect &&
                    actualInputRequests.Count == inspectRequests + 1 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.ToggleAnalysis &&
                    slice.AcceptedCommandCount == analysisCommands &&
                    slice.PresentationRevision == inspectRevision + 1,
                "second actual A key did not leave analysis exactly once " +
                $"(requests={actualInputRequests.Count}/{inspectRequests + 1}, " +
                $"last={actualInputRequests.LastOrDefault().Command}, " +
                $"tool={slice.InteractionState.Tool}, " +
                $"revision={slice.PresentationRevision}/{inspectRevision + 1})",
                failures);
            await ForceActualMapDraw(viewport, map);
            Require(!slice.LatestPresentation.World.AnalysisVisible &&
                    !map.DrawnAnalysisOverlayForSmoke &&
                    map.DrawnAnalysisRiskAreaIdsForSmoke.Count == 0,
                "second actual A key left the PlaceholderMap analysis draw path active",
                failures);

            slice.SetPlayerPausedForSmoke(false);
            Require(slice.InteractionState.Simulation == RealtimeSimulationState.Running,
                "actual keyboard-ownership fixture did not restore realtime flow",
                failures);
            // Keep the reducer truthfully Running for the direct-tool and Esc
            // semantics below, but stop unrelated wall-clock frames from crossing
            // a minute and being mistaken for an extra input-driven presentation.
            slice.FreezeAutonomousClockForSmoke();
            Require(slice.InteractionState.Simulation == RealtimeSimulationState.Running,
                "autonomous-clock freeze changed the live reducer Running state",
                failures);
            Require(!slice.AutonomousClockEnabledForSmoke,
                "DEBUG autonomous-clock seam did not stop Godot _Process before " +
                "exact revision assertions",
                failures);

            int directToolRequests = actualInputRequests.Count;
            int directToolCommands = slice.AcceptedCommandCount;
            long directToolRevision = slice.PresentationRevision;
            PushViewportKey(viewport, Key.N, pressed: true);
            PushViewportKey(viewport, Key.N, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == directToolRequests + 1 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.SelectFirstNodeTool &&
                    slice.InteractionState.Tool == RealtimeTool.BuildNode &&
                    slice.InteractionState.Surface == RealtimeSurface.Drawer &&
                    slice.InteractionState.SelectedBuildToolId?.StartsWith(
                        "NODE:", StringComparison.Ordinal) == true &&
                    !slice.LatestPresentation.Hud.BuildModeActive &&
                    string.Equals(
                        ui.TopHudForSmoke.MenuTextForSmoke,
                        "도구 닫기",
                        StringComparison.Ordinal) &&
                    ui.TopHudForSmoke.MenuTooltipForSmoke.Contains(
                        "도구를 닫습니다",
                        StringComparison.Ordinal) &&
                    slice.AcceptedCommandCount == directToolCommands &&
                    slice.PresentationRevision == directToolRevision + 1,
                "actual N key did not select the first visible node tool exactly once",
                failures);
            PushViewportKey(viewport, Key.L, pressed: true);
            PushViewportKey(viewport, Key.L, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == directToolRequests + 2 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.SelectFirstLineTool &&
                    slice.InteractionState.Tool == RealtimeTool.BuildLine &&
                    slice.InteractionState.Surface == RealtimeSurface.Drawer &&
                    slice.InteractionState.SelectedBuildToolId?.StartsWith(
                        "LINE:", StringComparison.Ordinal) == true &&
                    slice.AcceptedCommandCount == directToolCommands &&
                    slice.PresentationRevision == directToolRevision + 2,
                "actual L key did not select the first visible line tool exactly once",
                failures);
            PushViewportKey(viewport, Key.I, pressed: true);
            PushViewportKey(viewport, Key.I, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == directToolRequests + 3 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.SelectInspectTool &&
                    slice.InteractionState.Tool == RealtimeTool.Inspect &&
                    slice.InteractionState.Surface == RealtimeSurface.World &&
                    slice.InteractionState.SelectedBuildToolId is null &&
                    slice.AcceptedCommandCount == directToolCommands &&
                    slice.PresentationRevision == directToolRevision + 3,
                "actual I key did not restore Inspect/world exactly once",
                failures);

            Vector2 menuCenter = ui.TopHudForSmoke.MenuCenterForSmoke;
            PushViewportPointerMotion(viewport, menuCenter);
            await SettleLayout();
            int menuCommands = slice.AcceptedCommandCount;
            long menuRevision = slice.PresentationRevision;
            slice.ResetPointerClickCountersForSmoke();
            PushViewportPrimary(viewport, menuCenter, movePointer: false);
            await SettleLayout();
            Require(slice.InteractionState.Surface == RealtimeSurface.Drawer &&
                    slice.AcceptedCommandCount == menuCommands &&
                    slice.PresentationRevision == menuRevision + 1 &&
                    slice.PointerClickCounters.Values.Sum() == 0,
                "actual HUD tool click did not open exactly one build drawer " +
                $"(surface={slice.InteractionState.Surface}, " +
                $"commands={slice.AcceptedCommandCount}/{menuCommands}, " +
                $"revision={slice.PresentationRevision}/{menuRevision + 1}, " +
                $"counters={slice.PointerClickCounters.Values.Sum()})",
                failures);

            var (nodeToolId, emptyPoint) = slice.AcceptedNodeDraftForSmoke();
            Require(slice.LatestPresentation.BuildShelf.Tools.Any(item =>
                    string.Equals(item.Id, nodeToolId, StringComparison.Ordinal) &&
                    item.Enabled),
                "authority-selected node smoke tool is not live and enabled", failures);
            await SettleStableRect(() =>
                ui.BuildShelfForSmoke.ToolHitFactForSmoke(nodeToolId).Rect);
            var nodeToolHit =
                ui.BuildShelfForSmoke.ToolHitFactForSmoke(nodeToolId);
            Vector2 nodeToolPoint = nodeToolHit.Rect.GetCenter();
            PushViewportPointerMotion(viewport, nodeToolPoint);
            await SettleStableRect(() =>
                ui.BuildShelfForSmoke.ToolHitFactForSmoke(nodeToolId).Rect);
            nodeToolHit = ui.BuildShelfForSmoke.ToolHitFactForSmoke(nodeToolId);
            nodeToolPoint = nodeToolHit.Rect.GetCenter();
            long nodeToolRevision = slice.PresentationRevision;
            PushViewportPrimary(viewport, nodeToolPoint, movePointer: false);
            await SettleUntil(() =>
                slice.InteractionState.Tool == RealtimeTool.BuildNode &&
                string.Equals(
                    slice.InteractionState.SelectedBuildToolId,
                    nodeToolId,
                    StringComparison.Ordinal));
            var nodeToolAfter =
                ui.BuildShelfForSmoke.ToolHitFactForSmoke(nodeToolId);
            Require(slice.InteractionState.Tool == RealtimeTool.BuildNode &&
                    slice.InteractionState.Surface == RealtimeSurface.Drawer &&
                    slice.LatestPresentation.BuildShelf.Visible &&
                    string.Equals(
                        slice.InteractionState.SelectedBuildToolId,
                        nodeToolId,
                        StringComparison.Ordinal) &&
                    slice.AcceptedCommandCount == menuCommands &&
                    slice.PresentationRevision == nodeToolRevision + 1,
                "actual build-shelf node tool click did not select its exact tool ID once " +
                $"(point={nodeToolPoint}, beforeRect={nodeToolHit.Rect}, " +
                $"beforeVisible={nodeToolHit.Visible}, " +
                $"beforeEnabled={nodeToolHit.Enabled}, " +
                $"beforePressed={nodeToolHit.Pressed}, " +
                $"pointInside={nodeToolHit.Rect.HasPoint(nodeToolPoint)}, " +
                $"afterRect={nodeToolAfter.Rect}, afterPressed={nodeToolAfter.Pressed}, " +
                $"surface={slice.InteractionState.Surface}, " +
                $"tool={slice.InteractionState.Tool}, selectedTool=" +
                $"{slice.InteractionState.SelectedBuildToolId ?? "<none>"}, " +
                $"presentedSelected=[{string.Join(",", slice.LatestPresentation.BuildShelf
                    .Tools.Where(item => item.Selected).Select(item => item.Id))}], " +
                $"commands={slice.AcceptedCommandCount}/{menuCommands}, " +
                $"revision={slice.PresentationRevision}/{nodeToolRevision + 1})",
                failures);
            Require(map.SelectionActionForSmoke is null,
                "placement mode exposed a selection action above the placement ghost",
                failures);

            var rejectedPoint = slice.RejectedNodeDraftForSmoke(nodeToolId);
            slice.RestoreCameraForSmoke(new RealtimeMapCameraSnapshot(
                new Vector2(rejectedPoint.XUnit, rejectedPoint.YUnit),
                ZoomIndex: 0));
            await SettleLayout();
            PushViewportPointerMotion(
                viewport,
                map.ViewportPointForSmoke(rejectedPoint));
            await SettleLayout();
            Require(slice.LatestPresentation.Pointer.Point == rejectedPoint &&
                    !slice.LatestPresentation.Pointer.Accepted &&
                    !string.IsNullOrWhiteSpace(
                        slice.LatestPresentation.Pointer.Message) &&
                    slice.LatestPresentation.BuildShelf.Guidance.StartsWith(
                        "!", StringComparison.Ordinal) &&
                    string.Equals(
                        ui.BuildShelfForSmoke.GuidanceTextForSmoke,
                        slice.LatestPresentation.BuildShelf.Guidance,
                        StringComparison.Ordinal) &&
                    slice.CoreSnapshot.Construction.NodeDraft is null,
                "rejected Core node preview did not expose an invalid ghost and exact " +
                "visible BuildShelf feedback without mutating a draft",
                failures);

            slice.RestoreCameraForSmoke(new RealtimeMapCameraSnapshot(
                new Vector2(emptyPoint.XUnit, emptyPoint.YUnit),
                ZoomIndex: 0));
            await SettleLayout();
            Vector2 emptyCanvas = map.ViewportPointForSmoke(emptyPoint);
            PushViewportPointerMotion(viewport, emptyCanvas);
            await SettleLayout();
            Require(map.CandidateIdsForSmoke.Count == 0,
                "accepted node placement unexpectedly resolved a world candidate", failures);
            Require(slice.LatestPresentation.Pointer.Point == emptyPoint &&
                    slice.LatestPresentation.Pointer.Accepted &&
                    slice.LatestPresentation.BuildShelf.Guidance.StartsWith(
                        "✓", StringComparison.Ordinal) &&
                    string.Equals(
                        ui.BuildShelfForSmoke.GuidanceTextForSmoke,
                        slice.LatestPresentation.BuildShelf.Guidance,
                        StringComparison.Ordinal),
                "accepted Core node preview did not expose its exact visible " +
                "BuildShelf feedback",
                failures);
            string escSelection = slice.InteractionState.SelectionId ?? string.Empty;
            slice.ResetPointerClickCountersForSmoke();
            int emptyCommands = slice.AcceptedCommandCount;
            long emptyRevision = slice.PresentationRevision;
            PushViewportPrimary(viewport, emptyCanvas, movePointer: false);
            await SettleLayout();
            Require(slice.PointerClickCounters.Values.Sum() == 1 &&
                    slice.PointerClickCounters[RealtimePointerOwner.EmptyTerrain] == 1 &&
                    slice.CoreSnapshot.Construction.NodeDraft is { } nodeDraft &&
                    string.Equals(
                        nodeDraft.NodeClassId,
                        nodeToolId["NODE:".Length..],
                        StringComparison.Ordinal) &&
                    slice.AcceptedCommandCount == emptyCommands + 1 &&
                    slice.PresentationRevision == emptyRevision + 1,
                "actual empty-terrain click did not create one node draft with the " +
                $"selected Core node class/command/result (point={emptyCanvas}, " +
                $"map={map.GetGlobalRect()}, candidates=" +
                $"[{string.Join(",", map.CandidateIdsForSmoke)}], counters=" +
                $"[{string.Join(",", slice.PointerClickCounters.Select(item =>
                    $"{item.Key}:{item.Value}"))}], draft=" +
                $"{slice.CoreSnapshot.Construction.NodeDraft?.NodeClassId ?? "<none>"}, " +
                $"commands={slice.AcceptedCommandCount}/{emptyCommands + 1}, " +
                $"revision={slice.PresentationRevision}/{emptyRevision + 1})",
                failures);
            Require(string.Equals(
                        ui.ActionDockForSmoke.DetailTextForSmoke,
                        slice.LatestPresentation.ActionDock.Detail,
                        StringComparison.Ordinal) &&
                    slice.LatestPresentation.ActionDock.Detail.Contains(
                        slice.LatestPresentation.Pointer.Message,
                        StringComparison.Ordinal),
                "accepted node-draft pointer feedback was not visible as exact " +
                "ActionDock text",
                failures);
            await ValidateActualDraftToolLock(
                "node draft",
                RealtimeDraftToolLockKind.NodeDraft,
                RealtimeTool.BuildNode,
                nodeToolId,
                viewport,
                slice,
                ui,
                actualInputRequests,
                failures);

            Vector2 draftCanvas = map.ViewportPointForSmoke(emptyPoint);
            PushViewportPointerMotion(viewport, draftCanvas);
            await SettleLayout();
            slice.ResetPointerClickCountersForSmoke();
            int draftCommands = slice.AcceptedCommandCount;
            long draftRevision = slice.PresentationRevision;
            PushViewportPrimary(viewport, draftCanvas, movePointer: false);
            await SettleLayout();
            Require(slice.PointerClickCounters.Values.Sum() == 1 &&
                    slice.PointerClickCounters[RealtimePointerOwner.DraftHandle] == 1 &&
                    slice.CoreSnapshot.Construction.NodeDraft is not null &&
                    slice.AcceptedCommandCount == draftCommands &&
                    slice.PresentationRevision >= draftRevision + 1,
                "actual draft-handle click did not stop at one guidance owner with no Core " +
                $"fall-through command (counters=[{string.Join(",", slice.PointerClickCounters
                    .Select(item => $"{item.Key}:{item.Value}"))}], " +
                $"draft={slice.CoreSnapshot.Construction.NodeDraft is not null}, " +
                $"commands={slice.AcceptedCommandCount}/{draftCommands}, " +
                $"revision={slice.PresentationRevision}/{draftRevision}+)",
                failures);
            Require(string.Equals(
                        ui.ActionDockForSmoke.DetailTextForSmoke,
                        slice.LatestPresentation.ActionDock.Detail,
                        StringComparison.Ordinal) &&
                    slice.LatestPresentation.ActionDock.Detail.Contains(
                        slice.LatestPresentation.Pointer.Message,
                        StringComparison.Ordinal),
                "draft-handle guidance was not exposed as exact visible ActionDock text",
                failures);

            int backspaceRequests = actualInputRequests.Count;
            int backspaceCommands = slice.AcceptedCommandCount;
            PushViewportKey(viewport, Key.Backspace, pressed: true);
            PushViewportKey(viewport, Key.Backspace, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == backspaceRequests + 1 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.UndoDraftStep &&
                    slice.CoreSnapshot.Construction.NodeDraft is null &&
                    slice.AcceptedCommandCount == backspaceCommands + 1,
                "actual Backspace did not cancel one draft through the sole key owner",
                failures);

            emptyCanvas = map.ViewportPointForSmoke(emptyPoint);
            PushViewportPointerMotion(viewport, emptyCanvas);
            await SettleLayout();
            PushViewportPrimary(viewport, emptyCanvas);
            await SettleLayout();
            Require(slice.CoreSnapshot.Construction.NodeDraft is not null &&
                    slice.InteractionState.Tool == RealtimeTool.BuildNode &&
                    slice.InteractionState.Surface == RealtimeSurface.Drawer &&
                    !string.IsNullOrWhiteSpace(escSelection) &&
                    string.Equals(
                        slice.InteractionState.SelectionId,
                        escSelection,
                        StringComparison.Ordinal) &&
                    slice.InteractionState.Simulation == RealtimeSimulationState.Running,
                "actual Esc-chain fixture did not preserve draft/tool/surface/selection/run",
                failures);

            int escRequests = actualInputRequests.Count;
            int escCommands = slice.AcceptedCommandCount;
            long escRevision = slice.PresentationRevision;
            PushViewportKey(viewport, Key.Escape, pressed: true);
            PushViewportKey(viewport, Key.Escape, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == escRequests + 1 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.CancelOrBack &&
                    slice.CoreSnapshot.Construction.NodeDraft is not null &&
                    slice.InteractionState.Tool == RealtimeTool.BuildNode &&
                    slice.InteractionState.Surface == RealtimeSurface.Drawer &&
                    string.Equals(slice.InteractionState.SelectionId, escSelection,
                        StringComparison.Ordinal) &&
                    slice.AcceptedCommandCount == escCommands &&
                    slice.PresentationRevision == escRevision + 1 &&
                    slice.LatestPresentation.ActionDock.Detail.Contains(
                        "초안을 모두 취소하려면 B 또는 Esc를 한 번 더 누르세요.",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        ui.ActionDockForSmoke.DetailTextForSmoke,
                        slice.LatestPresentation.ActionDock.Detail,
                        StringComparison.Ordinal),
                "Esc step 1 did not arm an inline whole-draft confirmation without " +
                "mutating Core/tool/surface/selection",
                failures);

            PushViewportKey(viewport, Key.Escape, pressed: true);
            PushViewportKey(viewport, Key.Escape, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == escRequests + 2 &&
                    slice.CoreSnapshot.Construction.NodeDraft is null &&
                    !slice.LatestPresentation.Hud.BuildModeActive &&
                    string.Equals(
                        ui.TopHudForSmoke.MenuTextForSmoke,
                        "도구",
                        StringComparison.Ordinal) &&
                    slice.InteractionState.Tool == RealtimeTool.Inspect &&
                    slice.InteractionState.Surface == RealtimeSurface.World &&
                    string.Equals(slice.InteractionState.SelectionId, escSelection,
                        StringComparison.Ordinal) &&
                    slice.AcceptedCommandCount == escCommands + 1 &&
                    slice.PresentationRevision == escRevision + 3,
                "Esc step 2 did not confirm the active draft cancellation and " +
                "restore Inspect/world exactly",
                failures);

            PushViewportKey(viewport, Key.Escape, pressed: true);
            PushViewportKey(viewport, Key.Escape, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == escRequests + 3 &&
                    slice.InteractionState.Tool == RealtimeTool.Inspect &&
                    slice.InteractionState.Surface == RealtimeSurface.World &&
                    slice.InteractionState.SelectionId is null &&
                    slice.InteractionState.Simulation ==
                        RealtimeSimulationState.Running &&
                    slice.AcceptedCommandCount == escCommands + 1 &&
                    slice.PresentationRevision == escRevision + 4,
                "Esc step 3 did not clear only the selection after confirmed cancel",
                failures);

            PushViewportKey(viewport, Key.Escape, pressed: true);
            PushViewportKey(viewport, Key.Escape, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == escRequests + 4 &&
                    slice.InteractionState.SelectionId is null &&
                    slice.InteractionState.Simulation ==
                        RealtimeSimulationState.PlayerPaused &&
                    slice.InteractionState.PauseReason ==
                        RealtimePauseReason.PlayerRequest &&
                    slice.PresentationRevision == escRevision + 5,
                "Esc step 4 did not enter the typed player pause",
                failures);

            long pausedRevision = slice.PresentationRevision;
            PushViewportKey(viewport, Key.Escape, pressed: true);
            PushViewportKey(viewport, Key.Escape, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == escRequests + 5 &&
                    slice.InteractionState.SelectionId is null &&
                    slice.InteractionState.Simulation ==
                        RealtimeSimulationState.PlayerPaused &&
                    slice.InteractionState.PauseReason ==
                        RealtimePauseReason.PlayerRequest &&
                    slice.PresentationRevision == pausedRevision,
                "Esc step 5 silently resumed or rewrote an existing typed pause",
                failures);

            RealtimeSmokeLinePlan plan = slice.SmokeLinePlan;
            slice.AdvanceToForSmoke(plan.OrderMinute);
            PushViewportPrimary(viewport, ui.TopHudForSmoke.MenuCenterForSmoke);
            await SettleLayout();
            string lineToolId = $"LINE:{plan.LineClassId}:{plan.PoleClassId}";
            Require(slice.LatestPresentation.BuildShelf.Tools.Any(item =>
                    string.Equals(item.Id, lineToolId, StringComparison.Ordinal) &&
                    item.Enabled),
                "authority-selected line smoke tool is not live and enabled", failures);
            PushViewportPrimary(
                viewport,
                ui.BuildShelfForSmoke.ToolCenterForSmoke(lineToolId));
            await SettleLayout();
            Require(slice.InteractionState.Tool == RealtimeTool.BuildLine &&
                    string.Equals(
                        slice.InteractionState.SelectedBuildToolId,
                        lineToolId,
                        StringComparison.Ordinal),
                "actual build-shelf line click did not select its exact tool ID",
                failures);

            var selectedToolBefore =
                ui.BuildShelfForSmoke.ToolHitFactForSmoke(lineToolId);
            Require(selectedToolBefore.Visible &&
                    selectedToolBefore.Enabled &&
                    selectedToolBefore.Pressed,
                "selected build tool did not expose its authoritative pressed state",
                failures);
            long selectedToolRevision = slice.PresentationRevision;
            int selectedToolCommands = slice.AcceptedCommandCount;
            PushViewportPrimary(
                viewport,
                ui.BuildShelfForSmoke.ToolCenterForSmoke(lineToolId));
            await SettleLayout();
            var selectedToolAfter =
                ui.BuildShelfForSmoke.ToolHitFactForSmoke(lineToolId);
            Require(selectedToolAfter.Visible &&
                    selectedToolAfter.Enabled &&
                    selectedToolAfter.Pressed &&
                    slice.InteractionState.Tool == RealtimeTool.BuildLine &&
                    string.Equals(
                        slice.InteractionState.SelectedBuildToolId,
                        lineToolId,
                        StringComparison.Ordinal) &&
                    slice.PresentationRevision == selectedToolRevision &&
                    slice.AcceptedCommandCount == selectedToolCommands,
                "same-selected build-tool re-click visually unpressed or mutated " +
                "the no-op controller state",
                failures);

            var start = slice.CoreSnapshot.Construction.World.Nodes.Single(item =>
                string.Equals(item.NodeId, plan.StartNodeId, StringComparison.Ordinal));
            var end = slice.CoreSnapshot.Construction.World.Nodes.Single(item =>
                string.Equals(item.NodeId, plan.EndNodeId, StringComparison.Ordinal));
            int beforeBuildCommands = slice.AcceptedCommandCount;
            slice.RestoreCameraForSmoke(new RealtimeMapCameraSnapshot(
                new Vector2(start.Position.XUnit, start.Position.YUnit),
                ZoomIndex: 0));
            await SettleLayout();
            PushViewportPointerMotion(
                viewport,
                map.ViewportPointForSmoke(start.Position));
            await SettleLayout();
            Require(map.CandidateIdsForSmoke.Contains(
                    plan.StartNodeId, StringComparer.Ordinal),
                "actual map keyboard cursor did not expose the authored start node",
                failures);
            int startRequests = actualInputRequests.Count;
            PushViewportKey(viewport, Key.Enter, pressed: true);
            PushViewportKey(viewport, Key.Enter, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == startRequests + 1 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.ConfirmOrSelect &&
                    slice.AcceptedCommandCount == beforeBuildCommands + 1 &&
                    slice.CoreSnapshot.Construction.LineDraft is { } lineStartDraft &&
                    string.Equals(lineStartDraft.StartNodeId, plan.StartNodeId,
                        StringComparison.Ordinal) &&
                    string.Equals(lineStartDraft.LineClassId, plan.LineClassId,
                        StringComparison.Ordinal) &&
                    string.Equals(lineStartDraft.PoleClassId, plan.PoleClassId,
                        StringComparison.Ordinal),
                "actual Enter/cursor path did not create one line-start command with " +
                "the selected Core line/pole classes",
                failures);
            await ValidateActualDraftToolLock(
                "open line draft",
                RealtimeDraftToolLockKind.OpenLineDraft,
                RealtimeTool.BuildLine,
                lineToolId,
                viewport,
                slice,
                ui,
                actualInputRequests,
                failures);

            slice.RestoreCameraForSmoke(new RealtimeMapCameraSnapshot(
                new Vector2(end.Position.XUnit, end.Position.YUnit),
                ZoomIndex: 0));
            await SettleLayout();
            PushViewportPointerMotion(
                viewport,
                map.ViewportPointForSmoke(end.Position));
            await SettleLayout();
            Require(map.CandidateIdsForSmoke.Contains(
                    plan.EndNodeId, StringComparer.Ordinal),
                "actual map keyboard cursor did not expose the authored end node",
                failures);
            int finishRequests = actualInputRequests.Count;
            PushViewportKey(viewport, Key.Enter, pressed: true);
            PushViewportKey(viewport, Key.Enter, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == finishRequests + 1 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.ConfirmOrSelect &&
                    slice.AcceptedCommandCount == beforeBuildCommands + 2 &&
                    string.Equals(
                        slice.CoreSnapshot.Construction.LineDraft?.EndNodeId,
                        plan.EndNodeId,
                        StringComparison.Ordinal),
                "actual Enter/cursor path did not create one Core line-finish command",
                failures);
            await ValidateActualDraftToolLock(
                "closed line draft",
                RealtimeDraftToolLockKind.ClosedLineDraft,
                RealtimeTool.BuildLine,
                lineToolId,
                viewport,
                slice,
                ui,
                actualInputRequests,
                failures);

            slice.ResetPointerClickCountersForSmoke();
            int actionCommands = slice.AcceptedCommandCount;
            long actionRevision = slice.PresentationRevision;
            PushViewportPrimary(viewport, ui.PrimaryActionCenterForSmoke());
            await SettleLayout();
            Require(slice.AcceptedCommandCount == actionCommands + 1 &&
                    slice.PresentationRevision == actionRevision + 1 &&
                    slice.PointerClickCounters.Values.Sum() == 0 &&
                    slice.AcceptedCommandCount == beforeBuildCommands + 3 &&
                    slice.CoreSnapshot.Construction.ActiveConstruction is
                        { } orderedLine &&
                    orderedLine.EdgeIds.All(edgeId =>
                        slice.CoreSnapshot.Construction.World.Edges.Any(edge =>
                            string.Equals(edge.EdgeId, edgeId,
                                StringComparison.Ordinal) &&
                            string.Equals(edge.LineClassId, plan.LineClassId,
                                StringComparison.Ordinal))) &&
                    orderedLine.NodeIds.All(nodeId =>
                        slice.CoreSnapshot.Construction.World.Nodes.Any(node =>
                            string.Equals(node.NodeId, nodeId,
                                StringComparison.Ordinal) &&
                            string.Equals(node.ClassId, plan.PoleClassId,
                                StringComparison.Ordinal))) &&
                    slice.CoreSnapshot.Construction.LineDraft is null,
                "actual action-dock CTA did not order exactly one project using the " +
                "selected Core line/pole classes",
                failures);

            var activeBeforeComparison =
                slice.CoreSnapshot.Construction.ActiveConstruction!;
            var (comparisonNodeToolId, comparisonPoint) =
                slice.AcceptedNodeDraftForSmoke();
            Require(slice.LatestPresentation.BuildShelf.Tools.Any(item =>
                        string.Equals(item.Id, lineToolId, StringComparison.Ordinal) &&
                        item.Enabled) &&
                    slice.LatestPresentation.BuildShelf.Tools.Any(item =>
                        string.Equals(
                            item.Id,
                            comparisonNodeToolId,
                            StringComparison.Ordinal) &&
                        item.Enabled),
                "active project disabled an allowed line/node comparison tool",
                failures);
            PushViewportPrimary(
                viewport,
                ui.BuildShelfForSmoke.ToolCenterForSmoke(comparisonNodeToolId));
            await SettleLayout();
            slice.RestoreCameraForSmoke(new RealtimeMapCameraSnapshot(
                new Vector2(comparisonPoint.XUnit, comparisonPoint.YUnit),
                ZoomIndex: 0));
            await SettleLayout();
            PushViewportPointerMotion(
                viewport,
                map.ViewportPointForSmoke(comparisonPoint));
            await SettleLayout();
            slice.ResetPointerClickCountersForSmoke();
            int comparisonCommands = slice.AcceptedCommandCount;
            PushViewportPrimary(
                viewport,
                map.ViewportPointForSmoke(comparisonPoint));
            await SettleLayout();
            Require(slice.CoreSnapshot.Construction.NodeDraft is not null &&
                    slice.CoreSnapshot.Construction.ActiveConstruction is
                        { } activeDuringComparison &&
                    activeDuringComparison.Kind == activeBeforeComparison.Kind &&
                    activeDuringComparison.CompletionMinute ==
                        activeBeforeComparison.CompletionMinute &&
                    activeDuringComparison.NodeIds.SequenceEqual(
                        activeBeforeComparison.NodeIds, StringComparer.Ordinal) &&
                    activeDuringComparison.EdgeIds.SequenceEqual(
                        activeBeforeComparison.EdgeIds, StringComparer.Ordinal) &&
                    slice.PointerClickCounters.Values.Sum() == 1 &&
                    slice.PointerClickCounters[RealtimePointerOwner.EmptyTerrain] == 1 &&
                    slice.AcceptedCommandCount == comparisonCommands + 1 &&
                    slice.LatestPresentation.ActionDock.PrimaryAction is
                        { Visible: true, Enabled: false },
                "active project did not accept one comparison draft while keeping the " +
                "second order disabled",
                failures);

            RealtimeComparisonDraftForecast liveComparison =
                slice.LatestPresentation.ComparisonDraftForecast;
            RealtimeForecastEvent[] liveComparisonEvents =
                liveComparison.Forecast?.Events.ToArray() ??
                Array.Empty<RealtimeForecastEvent>();
            string[] liveComparisonEventMarkerIds = liveComparisonEvents
                .Select(item => $"DRAFT_FORECAST:{item.EventId}")
                .ToArray();
            string[] liveComparisonThermalMarkerIds = liveComparisonEvents
                .SelectMany(item => item.TemporalProjection.Transitions.Select(
                    transition =>
                        $"DRAFT_THERMAL:{item.EventId}:{transition.Minute}:" +
                        $"{transition.Kind}:{transition.AssetId}"))
                .ToArray();
            string[] liveComparisonMarkerIds = liveComparisonEventMarkerIds
                .Concat(liveComparisonThermalMarkerIds)
                .ToArray();
            RealtimeTimelineItemPresentation[] liveComparisonItems =
                slice.LatestPresentation.Rail.Items
                    .Where(item => liveComparisonMarkerIds.Contains(
                        item.Id,
                        StringComparer.Ordinal))
                    .ToArray();
            string[] liveRenderedMarkerIds = ui.EventRailForSmoke
                .MarkerFactsForSmoke()
                .SelectMany(marker => marker.ItemIds)
                .ToArray();
            string[] liveAccessibleMarkerIds = ui.EventRailForSmoke
                .AccessibleTimelineItemsForSmoke
                .Select(item => item.ItemId)
                .ToArray();
            Require(liveComparison is
                        { Available: true, DraftKind: ConstructionKind.Node,
                          Forecast: not null } &&
                    liveComparisonEvents.Length > 0 &&
                    liveComparisonEventMarkerIds.Length > 0 &&
                    liveComparisonThermalMarkerIds.Length > 0 &&
                    liveComparisonItems.Length == liveComparisonMarkerIds.Length &&
                    liveComparisonItems.All(item =>
                        string.Equals(
                            item.ShortLabel,
                            "현재 초안 기준 예상",
                            StringComparison.Ordinal) &&
                        item.Title.StartsWith(
                            "현재 초안 기준 예상 · ",
                            StringComparison.Ordinal) &&
                        item.Description.StartsWith(
                            "현재 초안 기준 예상 · ",
                            StringComparison.Ordinal) &&
                        item.SeverityLabel.StartsWith(
                            "현재 초안 기준 예상",
                            StringComparison.Ordinal)) &&
                    liveComparisonMarkerIds.All(id =>
                        liveRenderedMarkerIds.Count(candidate => string.Equals(
                            candidate,
                            id,
                            StringComparison.Ordinal)) == 1 &&
                        liveAccessibleMarkerIds.Count(candidate => string.Equals(
                            candidate,
                            id,
                            StringComparison.Ordinal)) == 1) &&
                    slice.CoreSnapshot.Construction.ActiveConstruction is { } &&
                    slice.CoreSnapshot.Construction.NodeDraft is { },
                "actual active-project + node-comparison scene did not render each " +
                "typed comparison event/thermal marker exactly once with its qualifier " +
                $"(events={liveComparisonEventMarkerIds.Length}, " +
                $"thermal={liveComparisonThermalMarkerIds.Length}, " +
                $"presented={liveComparisonItems.Length}, " +
                $"drawn={liveComparisonMarkerIds.Count(id => liveRenderedMarkerIds.Contains(id, StringComparer.Ordinal))}, " +
                $"accessible={liveComparisonMarkerIds.Count(id => liveAccessibleMarkerIds.Contains(id, StringComparer.Ordinal))})",
                failures);

            int disabledOrderCommands = slice.AcceptedCommandCount;
            long disabledOrderRevision = slice.PresentationRevision;
            slice.ResetPointerClickCountersForSmoke();
            PushViewportPrimary(viewport, ui.PrimaryActionCenterForSmoke());
            await SettleLayout();
            Require(slice.AcceptedCommandCount == disabledOrderCommands &&
                    slice.PresentationRevision == disabledOrderRevision &&
                    slice.PointerClickCounters.Values.Sum() == 0 &&
                    slice.CoreSnapshot.Construction.ActiveConstruction is not null &&
                    slice.CoreSnapshot.Construction.NodeDraft is not null,
                "disabled second-order CTA dispatched or destroyed comparison state",
                failures);
            RealtimeR2IntentResult rejectedSecondOrder = slice.ApplyIntentForSmoke(
                new RealtimeR2Intent(RealtimeR2IntentKind.OrderNode));
            Require(!rejectedSecondOrder.Accepted &&
                    rejectedSecondOrder.CoreCommandResult?.Accepted == false &&
                    rejectedSecondOrder.JournalDelta == 0 &&
                    slice.AcceptedCommandCount == disabledOrderCommands &&
                    slice.CoreSnapshot.Construction.ActiveConstruction is not null &&
                    slice.CoreSnapshot.Construction.NodeDraft is not null,
                "direct second-order attempt created a hidden queue or lost the comparison",
                failures);

            slice.SetPlayerPausedForSmoke(false);
            slice.SetSpeedForSmoke(RealtimeSimulationSpeed.Normal);
            Require(slice.InteractionState.Simulation ==
                        RealtimeSimulationState.Running &&
                    slice.InteractionState.RunningSpeed ==
                        RealtimeSimulationSpeed.Normal,
                "actual commissioning fixture did not enter exact normal realtime flow",
                failures);
            IReadOnlyList<RealtimeTransition> actualCommissioningTransitions =
                AdvanceActualSceneByExactMinutes(
                    slice,
                    plan.ExpectedCompletionMinute,
                    "actual commissioning",
                    failures);
            await SettleLayout();
            int commissioningTransitionIndex = Array.FindIndex(
                actualCommissioningTransitions.ToArray(),
                transition =>
                    transition.Kind ==
                        RealtimeTransitionKind.ConstructionCompleted &&
                    transition.Minute == plan.ExpectedCompletionMinute);
            int sameMinuteEventIndex = Array.FindIndex(
                actualCommissioningTransitions.ToArray(),
                transition =>
                    transition.Kind == RealtimeTransitionKind.EventStarted &&
                    transition.Minute == plan.ExpectedCompletionMinute);
            RealtimeTransition? actualCommissioning =
                commissioningTransitionIndex < 0
                    ? null
                    : actualCommissioningTransitions[commissioningTransitionIndex];
            RealtimeConstructionCompletion? actualCompletion =
                actualCommissioning?.Construction;
            Require(actualCompletion is { Kind: ConstructionKind.Line } &&
                    actualCompletion.CompletionMinute ==
                        plan.ExpectedCompletionMinute &&
                    actualCompletion.NodeIds.SequenceEqual(
                        plan.ExpectedNodeIds,
                        StringComparer.Ordinal) &&
                    actualCompletion.EdgeIds.SequenceEqual(
                        plan.ExpectedEdgeIds,
                        StringComparer.Ordinal) &&
                    (sameMinuteEventIndex < 0 ||
                        commissioningTransitionIndex < sameMinuteEventIndex) &&
                    slice.CurrentMinute == plan.ExpectedCompletionMinute &&
                    slice.LatestPresentation.World.Minute ==
                        plan.ExpectedCompletionMinute &&
                    slice.CoreSnapshot.Construction.ActiveConstruction is null &&
                    plan.ExpectedNodeIds.All(nodeId =>
                        slice.CoreSnapshot.Construction.World.Nodes.Single(node =>
                            string.Equals(
                                node.NodeId,
                                nodeId,
                                StringComparison.Ordinal)).Commissioned &&
                        slice.LatestPresentation.World.World.Nodes.Single(node =>
                            string.Equals(
                                node.NodeId,
                                nodeId,
                                StringComparison.Ordinal)).Commissioned) &&
                    plan.ExpectedEdgeIds.All(edgeId =>
                        slice.CoreSnapshot.Construction.World.Edges.Single(edge =>
                            string.Equals(
                                edge.EdgeId,
                                edgeId,
                                StringComparison.Ordinal)).Commissioned &&
                        slice.LatestPresentation.World.World.Edges.Single(edge =>
                            string.Equals(
                                edge.EdgeId,
                                edgeId,
                                StringComparison.Ordinal)).Commissioned) &&
                    slice.AcceptedCommandCount == disabledOrderCommands &&
                    slice.CoreSnapshot.Construction.NodeDraft is not null,
                "actual UI-ordered project did not automatically commission through " +
                "the exact frame/CollectTransitions/presentation path",
                failures);
            GD.Print(
                "REALTIME_R2_ACTUAL_COMMISSIONING_PASS " +
                $"minute={slice.CurrentMinute}; nodes=" +
                $"{string.Join(",", plan.ExpectedNodeIds)}; edges=" +
                string.Join(",", plan.ExpectedEdgeIds));
            if (slice.InteractionState.Simulation ==
                    RealtimeSimulationState.AutoPaused &&
                slice.InteractionState.ActiveModalId is null)
            {
                RealtimeR2IntentResult acknowledged = slice.ApplyIntentForSmoke(
                    RealtimeR2Intent.AcknowledgeAutoPause());
                Require(acknowledged.Accepted,
                    "actual commissioning fixture could not acknowledge its typed " +
                    "critical auto-pause",
                    failures);
            }
            Require(slice.InteractionState.Simulation ==
                    RealtimeSimulationState.Running,
                "actual commissioning did not leave a resumable running controller",
                failures);
            slice.SetPlayerPausedForSmoke(true);

            BaseButton modalOpener = ui.TopHudForSmoke.MenuButtonForSmoke;
            modalOpener.GrabFocus();
            await SettleLayout();
            Require(ReferenceEquals(ui.FocusOwnerForSmoke, modalOpener),
                "actual HUD button could not own focus before modal entry", failures);
            Require(slice.ApplyIntentForSmoke(RealtimeR2Intent.OpenModal(
                    "ACTUAL_SCENE_FOCUS_MODAL",
                    RealtimeModalKind.RecoveryConfirmation,
                    RealtimePauseReason.RecoveryConfirmation,
                    "ACTUAL_HUD_MENU_FOCUS")).Accepted,
                "actual scene could not open its typed focus-return modal", failures);
            await SettleLayout();
            RealtimeModalPresentation? unsupportedModal =
                slice.LatestPresentation.Modal;
            Require(ui.ModalHostForSmoke.OwnsFocusForSmoke &&
                    slice.InteractionState.PauseReason ==
                        RealtimePauseReason.RecoveryConfirmation &&
                    unsupportedModal is
                    {
                        Id: "ACTUAL_SCENE_FOCUS_MODAL",
                        Kind: RealtimeModalKind.RecoveryConfirmation,
                        Eyebrow: "운영 안내",
                        Heading: "현재 운영 화면에서 실행할 수 없는 작업입니다",
                        PrimaryAction.Id: "NOTICE_CLOSE",
                        PrimaryAction.Label: "안내 닫기",
                        PrimaryAction.Tone: RealtimeActionTone.Primary,
                        SecondaryAction: null,
                        DismissOnCancel: true,
                    } &&
                    unsupportedModal.Body.Contains(
                        "변경되지 않았습니다",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        ui.ModalHostForSmoke.ActivePrimaryActionIdForSmoke,
                        "NOTICE_CLOSE",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        ui.ModalHostForSmoke.PrimaryTextForSmoke,
                        "안내 닫기",
                        StringComparison.Ordinal) &&
                    ui.ModalHostForSmoke.PrimaryVisibleForSmoke &&
                    ui.ModalHostForSmoke.PrimaryEnabledForSmoke &&
                    string.Equals(
                        ui.ModalHostForSmoke.PrimaryThemeForSmoke,
                        "PrimaryButton",
                        StringComparison.Ordinal) &&
                    ui.ModalHostForSmoke.ActiveSecondaryActionIdForSmoke is null &&
                    !ui.ModalHostForSmoke.SecondaryVisibleForSmoke &&
                    !ui.ModalHostForSmoke.PrimaryTextForSmoke.Contains(
                        "복구",
                        StringComparison.Ordinal) &&
                    !ui.ModalHostForSmoke.PrimaryTextForSmoke.Contains(
                        "버리기",
                        StringComparison.Ordinal),
                "actual unsupported modal did not expose the sole implemented " +
                "NOTICE_CLOSE label/action with non-destructive close-only semantics",
                failures);
            PushViewportPrimary(
                viewport,
                ui.ModalHostForSmoke.PrimaryCenterForSmoke);
            await SettleLayout();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(ReferenceEquals(ui.FocusOwnerForSmoke, modalOpener) &&
                    slice.InteractionState.Simulation ==
                        RealtimeSimulationState.PlayerPaused,
                "actual modal close did not restore its exact button opener and prior pause",
                failures);

            Require(slice.ApplyIntentForSmoke(RealtimeR2Intent.OpenModal(
                    "ACTUAL_SCENE_NEW_GAME_NOTICE",
                    RealtimeModalKind.NewGameConfirmation,
                    RealtimePauseReason.PlayerRequest,
                    "ACTUAL_HUD_MENU_FOCUS")).Accepted,
                "actual scene could not open its new-game unsupported-action notice",
                failures);
            await SettleLayout();
            RealtimeModalPresentation? newGameNotice =
                slice.LatestPresentation.Modal;
            Require(newGameNotice is
                    {
                        Id: "ACTUAL_SCENE_NEW_GAME_NOTICE",
                        Kind: RealtimeModalKind.NewGameConfirmation,
                        PrimaryAction.Id: "NOTICE_CLOSE",
                        PrimaryAction.Label: "안내 닫기",
                        PrimaryAction.Tone: RealtimeActionTone.Primary,
                        SecondaryAction: null,
                    } &&
                    string.Equals(
                        ui.ModalHostForSmoke.ActivePrimaryActionIdForSmoke,
                        "NOTICE_CLOSE",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        ui.ModalHostForSmoke.PrimaryTextForSmoke,
                        "안내 닫기",
                        StringComparison.Ordinal) &&
                    ui.ModalHostForSmoke.PrimaryVisibleForSmoke &&
                    ui.ModalHostForSmoke.PrimaryEnabledForSmoke &&
                    !ui.ModalHostForSmoke.SecondaryVisibleForSmoke &&
                    !newGameNotice.Heading.Contains(
                        "새 게임",
                        StringComparison.Ordinal) &&
                    !newGameNotice.PrimaryAction.Label.Contains(
                        "버리기",
                        StringComparison.Ordinal),
                "actual unsupported new-game modal exposed an unimplemented " +
                "destructive label/action instead of NOTICE_CLOSE",
                failures);
            PushViewportPrimary(
                viewport,
                ui.ModalHostForSmoke.PrimaryCenterForSmoke);
            await SettleLayout();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(slice.InteractionState.ActiveModalId is null &&
                    slice.InteractionState.Simulation ==
                        RealtimeSimulationState.PlayerPaused &&
                    ReferenceEquals(ui.FocusOwnerForSmoke, modalOpener),
                "actual new-game notice close did not restore the unchanged prior state",
                failures);

            RealtimeTimelineItemPresentation seedTimelineItem =
                slice.LatestPresentation.Rail.Items
                    .Where(item =>
                        item.Visibility != RealtimeTimelineVisibility.Hidden)
                    .OrderBy(item => item.StartMinute)
                    .ThenBy(item => item.Priority)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .First();
            ui.EventRailForSmoke.FocusMarkerForSmoke(seedTimelineItem.Id);
            await SettleLayout();
            int timelineKeyRequests = actualInputRequests.Count;
            int timelineKeyCommands = slice.AcceptedCommandCount;
            long timelineKeyRevision = slice.PresentationRevision;
            string timelineKeyHash = slice.CanonicalStateSha256;
            PushViewportKey(viewport, Key.Home, pressed: true);
            PushViewportKey(viewport, Key.Home, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == timelineKeyRequests + 1 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.TimelineHome &&
                    slice.PresentationRevision == timelineKeyRevision + 1 &&
                    slice.TimelineChooserFacts.SelectedMarkerId is null &&
                    slice.TimelineChooserFacts.SelectedSubjectId is null &&
                    string.Equals(
                        ui.EventRailForSmoke.FocusedItemIdForSmoke,
                        seedTimelineItem.Id,
                        StringComparison.Ordinal) &&
                    slice.AcceptedCommandCount == timelineKeyCommands &&
                    string.Equals(slice.CanonicalStateSha256, timelineKeyHash,
                        StringComparison.Ordinal),
                "actual Home key was not singly owned or did not restore the current-time " +
                "anchor while preserving semantic marker focus",
                failures);

            RealtimeTimelineItemPresentation[] keyboardTimelineItems =
                slice.LatestPresentation.Rail.Items
                    .Where(item =>
                        item.Visibility != RealtimeTimelineVisibility.Hidden)
                    .Where(item =>
                        item.StartMinute <=
                            slice.LatestPresentation.Rail.HorizonEndMinute &&
                        (item.EndMinute ?? item.StartMinute) >=
                            slice.LatestPresentation.Rail.HorizonStartMinute)
                    .OrderBy(item => item.StartMinute)
                    .ThenBy(item => item.Priority)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .ToArray();
            RealtimeTimelineItemPresentation expectedNext =
                keyboardTimelineItems.FirstOrDefault(item =>
                    item.StartMinute > slice.CurrentMinute) ?? keyboardTimelineItems[^1];
            PushViewportKey(viewport, Key.Bracketright, pressed: true);
            PushViewportKey(viewport, Key.Bracketright, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count == timelineKeyRequests + 2 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.TimelineNext &&
                    slice.PresentationRevision == timelineKeyRevision + 2 &&
                    string.Equals(
                        slice.TimelineChooserFacts.SelectedMarkerId,
                        expectedNext.Id,
                        StringComparison.Ordinal) &&
                    ui.EventRailForSmoke.FocusedMarkerItemIdsForSmoke.Contains(
                        expectedNext.Id, StringComparer.Ordinal) &&
                    slice.AcceptedCommandCount == timelineKeyCommands &&
                    string.Equals(slice.CanonicalStateSha256, timelineKeyHash,
                        StringComparison.Ordinal),
                "actual ] key was not singly owned or did not move selection/focus to " +
                $"the exact Core item {expectedNext.Id}",
                failures);

            int expectedNextIndex = Array.FindIndex(
                keyboardTimelineItems,
                item => string.Equals(item.Id, expectedNext.Id,
                    StringComparison.Ordinal));
            int previousSourceIndex = expectedNextIndex;
            int requestOffset = 2;
            int revisionOffset = 2;
            if (previousSourceIndex == 0 && keyboardTimelineItems.Length > 1)
            {
                RealtimeTimelineItemPresentation secondItem = keyboardTimelineItems[1];
                PushViewportKey(viewport, Key.Bracketright, pressed: true);
                PushViewportKey(viewport, Key.Bracketright, pressed: false);
                await SettleLayout();
                requestOffset++;
                revisionOffset++;
                previousSourceIndex = 1;
                Require(actualInputRequests.Count ==
                            timelineKeyRequests + requestOffset &&
                        actualInputRequests[^1].Command ==
                            RealtimeInputCommand.TimelineNext &&
                        slice.PresentationRevision ==
                            timelineKeyRevision + revisionOffset &&
                        string.Equals(
                            slice.TimelineChooserFacts.SelectedMarkerId,
                            secondItem.Id,
                            StringComparison.Ordinal) &&
                        ui.EventRailForSmoke.FocusedMarkerItemIdsForSmoke.Contains(
                            secondItem.Id, StringComparer.Ordinal),
                    "actual ] boundary setup did not advance from the first to the " +
                    $"second exact Core item {secondItem.Id}",
                    failures);
            }
            RealtimeTimelineItemPresentation expectedPrevious =
                keyboardTimelineItems[Math.Max(0, previousSourceIndex - 1)];
            PushViewportKey(viewport, Key.Bracketleft, pressed: true);
            PushViewportKey(viewport, Key.Bracketleft, pressed: false);
            await SettleLayout();
            Require(actualInputRequests.Count ==
                        timelineKeyRequests + requestOffset + 1 &&
                    actualInputRequests[^1].Command ==
                        RealtimeInputCommand.TimelinePrevious &&
                    slice.PresentationRevision ==
                        timelineKeyRevision + revisionOffset + 1 &&
                    string.Equals(
                        slice.TimelineChooserFacts.SelectedMarkerId,
                        expectedPrevious.Id,
                        StringComparison.Ordinal) &&
                    ui.EventRailForSmoke.FocusedMarkerItemIdsForSmoke.Contains(
                        expectedPrevious.Id, StringComparer.Ordinal) &&
                    slice.AcceptedCommandCount == timelineKeyCommands &&
                    string.Equals(slice.CanonicalStateSha256, timelineKeyHash,
                        StringComparison.Ordinal),
                "actual [ key was not singly owned or did not move selection/focus to " +
                $"the exact Core item {expectedPrevious.Id} " +
                $"(requests={actualInputRequests.Count}/" +
                $"{timelineKeyRequests + requestOffset + 1}, " +
                $"last={actualInputRequests.LastOrDefault().Command}, " +
                $"revision={slice.PresentationRevision}/" +
                $"{timelineKeyRevision + revisionOffset + 1}, " +
                $"selected={slice.TimelineChooserFacts.SelectedMarkerId ?? "<none>"}, " +
                $"subject={slice.TimelineChooserFacts.SelectedSubjectId ?? "<none>"}, " +
                $"focused=[{string.Join(",", ui.EventRailForSmoke
                    .FocusedMarkerItemIdsForSmoke)}], " +
                $"commands={slice.AcceptedCommandCount}/{timelineKeyCommands}, " +
                $"hashSame={string.Equals(slice.CanonicalStateSha256, timelineKeyHash,
                    StringComparison.Ordinal)})",
                failures);

            IReadOnlyDictionary<RealtimeTimelineNavigation, Rect2> mouseNavigation =
                ui.EventRailForSmoke.NavigationFactsForSmoke.ToDictionary(
                    item => item.Navigation,
                    item => item.Rect);
            long mouseHomeRevision = slice.PresentationRevision;
            PushViewportPrimary(
                viewport,
                mouseNavigation[RealtimeTimelineNavigation.Home].GetCenter());
            await SettleLayout();
            Require(slice.PresentationRevision == mouseHomeRevision + 1 &&
                    slice.TimelineChooserFacts.SelectedMarkerId is null &&
                    slice.TimelineChooserFacts.SelectedSubjectId is null,
                "actual mouse 현재 control did not restore exact Home/current-time state",
                failures);

            long mouseNextRevision = slice.PresentationRevision;
            PushViewportPrimary(
                viewport,
                mouseNavigation[RealtimeTimelineNavigation.NextEvent].GetCenter());
            await SettleLayout();
            Require(slice.PresentationRevision == mouseNextRevision + 1,
                "actual mouse 다음 control did not reduce exactly once", failures);
            ValidateTimelineSelection(slice, expectedNext.Id, failures);

            int mousePreviousSourceIndex = expectedNextIndex;
            if (mousePreviousSourceIndex == 0 && keyboardTimelineItems.Length > 1)
            {
                long secondMouseNextRevision = slice.PresentationRevision;
                PushViewportPrimary(
                    viewport,
                    mouseNavigation[RealtimeTimelineNavigation.NextEvent].GetCenter());
                await SettleLayout();
                Require(slice.PresentationRevision == secondMouseNextRevision + 1,
                    "actual second mouse 다음 control did not advance off the first " +
                    "boundary event",
                    failures);
                mousePreviousSourceIndex = 1;
                ValidateTimelineSelection(
                    slice,
                    keyboardTimelineItems[mousePreviousSourceIndex].Id,
                    failures);
            }
            RealtimeTimelineItemPresentation expectedMousePrevious =
                keyboardTimelineItems[Math.Max(0, mousePreviousSourceIndex - 1)];
            long mousePreviousRevision = slice.PresentationRevision;
            PushViewportPrimary(
                viewport,
                mouseNavigation[RealtimeTimelineNavigation.PreviousEvent].GetCenter());
            await SettleLayout();
            Require(slice.PresentationRevision == mousePreviousRevision + 1,
                "actual mouse 이전 control did not reduce exactly once", failures);
            ValidateTimelineSelection(slice, expectedMousePrevious.Id, failures);

            long mouseResetRevision = slice.PresentationRevision;
            PushViewportPrimary(
                viewport,
                mouseNavigation[RealtimeTimelineNavigation.Home].GetCenter());
            await SettleLayout();
            Require(slice.PresentationRevision == mouseResetRevision + 1 &&
                    slice.TimelineChooserFacts.SelectedMarkerId is null &&
                    slice.TimelineChooserFacts.SelectedSubjectId is null &&
                    slice.AcceptedCommandCount == timelineKeyCommands &&
                    string.Equals(slice.CanonicalStateSha256, timelineKeyHash,
                        StringComparison.Ordinal),
                "actual mouse previous/now/next controls changed Core/journal or " +
                "failed exact Home reset",
                failures);

            RealtimeTimelineItemPresentation firstBoundaryItem =
                keyboardTimelineItems[0];
            RealtimeTimelineItemPresentation lastBoundaryItem =
                keyboardTimelineItems[^1];
            for (int attempt = 0;
                 attempt <= keyboardTimelineItems.Length &&
                 !string.Equals(
                     slice.TimelineChooserFacts.SelectedMarkerId,
                     lastBoundaryItem.Id,
                     StringComparison.Ordinal);
                 attempt++)
            {
                ui.EventRailForSmoke.FocusMarkerForSmoke(lastBoundaryItem.Id);
                await SettleLayout();
            }
            Require(string.Equals(
                        slice.TimelineChooserFacts.SelectedMarkerId,
                        lastBoundaryItem.Id,
                        StringComparison.Ordinal) &&
                    ui.EventRailForSmoke.FocusedMarkerItemIdsForSmoke.Contains(
                        lastBoundaryItem.Id,
                        StringComparer.Ordinal),
                "actual boundary setup did not select the exact last semantic ID " +
                "through its possibly clustered marker",
                failures);
            long selectedLastRevision = slice.PresentationRevision;
            Rect2 selectedLastNextRect = ui.EventRailForSmoke
                .NavigationFactsForSmoke.Single(item =>
                    item.Navigation == RealtimeTimelineNavigation.NextEvent)
                .Rect;
            PushViewportPrimary(
                viewport,
                selectedLastNextRect.GetCenter());
            await SettleLayout();
            Require(slice.PresentationRevision == selectedLastRevision &&
                    string.Equals(
                        slice.TimelineChooserFacts.SelectedMarkerId,
                        lastBoundaryItem.Id,
                        StringComparison.Ordinal) &&
                    ui.EventRailForSmoke.FocusedMarkerItemIdsForSmoke.Contains(
                        lastBoundaryItem.Id,
                        StringComparer.Ordinal) &&
                    slice.AcceptedCommandCount == timelineKeyCommands &&
                    string.Equals(
                        slice.CanonicalStateSha256,
                        timelineKeyHash,
                        StringComparison.Ordinal),
                "actual mouse Next at selected-last boundary changed selection/revision",
                failures);
            for (int attempt = 0;
                 attempt <= keyboardTimelineItems.Length &&
                 !string.Equals(
                     slice.TimelineChooserFacts.SelectedMarkerId,
                     firstBoundaryItem.Id,
                     StringComparison.Ordinal);
                 attempt++)
            {
                ui.EventRailForSmoke.FocusMarkerForSmoke(firstBoundaryItem.Id);
                await SettleLayout();
            }
            Require(string.Equals(
                        slice.TimelineChooserFacts.SelectedMarkerId,
                        firstBoundaryItem.Id,
                        StringComparison.Ordinal) &&
                    ui.EventRailForSmoke.FocusedMarkerItemIdsForSmoke.Contains(
                        firstBoundaryItem.Id,
                        StringComparer.Ordinal),
                "actual boundary setup did not select the exact first semantic ID " +
                "through its possibly clustered marker",
                failures);
            long selectedFirstRevision = slice.PresentationRevision;
            Rect2 selectedFirstPreviousRect = ui.EventRailForSmoke
                .NavigationFactsForSmoke.Single(item =>
                    item.Navigation == RealtimeTimelineNavigation.PreviousEvent)
                .Rect;
            PushViewportPrimary(
                viewport,
                selectedFirstPreviousRect.GetCenter());
            await SettleLayout();
            Require(slice.PresentationRevision == selectedFirstRevision &&
                    string.Equals(
                        slice.TimelineChooserFacts.SelectedMarkerId,
                        firstBoundaryItem.Id,
                        StringComparison.Ordinal) &&
                    ui.EventRailForSmoke.FocusedMarkerItemIdsForSmoke.Contains(
                        firstBoundaryItem.Id,
                        StringComparer.Ordinal) &&
                    slice.AcceptedCommandCount == timelineKeyCommands &&
                    string.Equals(
                        slice.CanonicalStateSha256,
                        timelineKeyHash,
                        StringComparison.Ordinal),
                "actual mouse Previous at selected-first boundary changed " +
                "selection/revision",
                failures);
            PushViewportPrimary(
                viewport,
                mouseNavigation[RealtimeTimelineNavigation.Home].GetCenter());
            await SettleLayout();

            RealtimeTimelineItemPresentation eventItem = slice.LatestPresentation.Rail.Items
                .First(item => RealtimeSlicePresenter.ResolveTimelineTarget(
                    slice.DisplayWorldForSmoke,
                    slice.CoreSnapshot,
                    slice.LatestPresentation.BaseForecast,
                    slice.LatestPresentation.ComparisonDraftForecast,
                    item.Id).Kind == RealtimeTimelineTargetKind.Event);
            RealtimeTimelineItemPresentation thermalItem =
                slice.LatestPresentation.Rail.Items.First(item =>
                    RealtimeSlicePresenter.ResolveTimelineTarget(
                        slice.DisplayWorldForSmoke,
                        slice.CoreSnapshot,
                        slice.LatestPresentation.BaseForecast,
                        slice.LatestPresentation.ComparisonDraftForecast,
                        item.Id).Kind == RealtimeTimelineTargetKind.ThermalAsset);
            string timelineHash = slice.CanonicalStateSha256;
            int timelineCommands = slice.AcceptedCommandCount;
            ui.EventRailForSmoke.FocusMarkerForSmoke(eventItem.Id);
            await SettleLayout();
            ValidateTimelineSelection(slice, eventItem.Id, failures);
            RealtimeTimelineTarget eventTarget =
                RealtimeSlicePresenter.ResolveTimelineTarget(
                    slice.DisplayWorldForSmoke,
                    slice.CoreSnapshot,
                    slice.LatestPresentation.BaseForecast,
                    slice.LatestPresentation.ComparisonDraftForecast,
                    eventItem.Id);
            Require(eventTarget.MapSubjectId is not null &&
                    slice.LatestPresentation.World.Highlight is not null &&
                    !map.AccessibilityDescription.Contains(
                        "선택 없음", StringComparison.Ordinal),
                "actual event marker did not project a visible/AX map subject cue",
                failures);
            ui.EventRailForSmoke.FocusMarkerForSmoke(thermalItem.Id);
            await SettleLayout();
            ValidateTimelineSelection(slice, thermalItem.Id, failures);
            Require(string.Equals(timelineHash, slice.CanonicalStateSha256,
                        StringComparison.Ordinal) &&
                    slice.AcceptedCommandCount == timelineCommands,
                "actual event/thermal rail selection changed Core or its journal",
                failures);

            Require(slice.InteractionState.Simulation ==
                    RealtimeSimulationState.PlayerPaused,
                "actual CampaignCompleted fixture did not begin from the typed player pause",
                failures);
            int campaignCommands = slice.AcceptedCommandCount;
            slice.SetPlayerPausedForSmoke(false);
            slice.SetSpeedForSmoke(RealtimeSimulationSpeed.Normal);
            long campaignCompletionMinute = slice.SmokeBoundaryFacts.Events
                .Max(item => item.EndMinute);
            IReadOnlyList<RealtimeTransition> actualCampaignTransitions =
                AdvanceActualSceneByExactMinutes(
                    slice,
                    campaignCompletionMinute,
                    "actual campaign completion",
                    failures);
            await SettleLayout();
            int chapterCompletedIndex = Array.FindIndex(
                actualCampaignTransitions.ToArray(),
                transition =>
                    transition.Kind == RealtimeTransitionKind.ChapterCompleted &&
                    transition.Minute == campaignCompletionMinute);
            int campaignCompletedIndex = Array.FindIndex(
                actualCampaignTransitions.ToArray(),
                transition =>
                    transition.Kind == RealtimeTransitionKind.CampaignCompleted &&
                    transition.Minute == campaignCompletionMinute);
            RealtimeModalPresentation? campaignModal =
                slice.LatestPresentation.Modal;
            RealtimeFrameAccumulatorSnapshot campaignAccumulator =
                slice.AccumulatorSnapshot;
            Require(chapterCompletedIndex >= 0 &&
                    campaignCompletedIndex > chapterCompletedIndex &&
                    slice.CurrentMinute == campaignCompletionMinute &&
                    slice.CoreSnapshot.CampaignComplete &&
                    slice.CoreSnapshot.CompletedChapters.Count == 1 &&
                    slice.InteractionState.Simulation ==
                        RealtimeSimulationState.Ended &&
                    slice.InteractionState.PauseReason ==
                        RealtimePauseReason.CampaignResult &&
                    slice.InteractionState.Surface ==
                        RealtimeSurface.BlockingModal &&
                    string.Equals(
                        slice.InteractionState.ActiveModalId,
                        "CAMPAIGN_RESULT",
                        StringComparison.Ordinal) &&
                    campaignModal is
                    {
                        Id: "CAMPAIGN_RESULT",
                        Kind: RealtimeModalKind.ChapterStory,
                        Heading: "캠페인 운영 완료",
                        PrimaryAction.Id: "RESULT_CLOSE",
                        PrimaryAction.Label: "결과 확인",
                        SecondaryAction: null,
                    } &&
                    ui.ModalHostForSmoke.Depth == 1 &&
                    ui.ModalHostForSmoke.OwnsFocusForSmoke &&
                    string.Equals(
                        ui.ModalHostForSmoke.ActivePrimaryActionIdForSmoke,
                        "RESULT_CLOSE",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        ui.ModalHostForSmoke.PrimaryTextForSmoke,
                        "결과 확인",
                        StringComparison.Ordinal) &&
                    ui.ModalHostForSmoke.PrimaryVisibleForSmoke &&
                    ui.ModalHostForSmoke.PrimaryEnabledForSmoke &&
                    !ui.ModalHostForSmoke.SecondaryVisibleForSmoke &&
                    campaignAccumulator.Paused &&
                    campaignAccumulator.PauseReason ==
                        RealtimeFramePauseReason.Manual &&
                    campaignAccumulator.PendingWholeMinutes == 0 &&
                    campaignAccumulator.FractionalMinuteUnits == 0 &&
                    slice.AcceptedCommandCount == campaignCommands,
                "actual R1 CampaignCompleted did not traverse production " +
                "CollectTransitions into Ended/CampaignResult/modal/frame pause",
                failures);
            GD.Print(
                "REALTIME_R2_ACTUAL_CAMPAIGN_COMPLETED_PASS " +
                $"minute={slice.CurrentMinute}; chapter-index={chapterCompletedIndex}; " +
                $"campaign-index={campaignCompletedIndex}; modal=CAMPAIGN_RESULT");

            string campaignEndedHash = slice.CanonicalStateSha256;
            long campaignModalRevision = slice.PresentationRevision;
            PushViewportPrimary(
                viewport,
                ui.ModalHostForSmoke.PrimaryCenterForSmoke);
            await SettleLayout();
            Require(slice.InteractionState.Simulation ==
                        RealtimeSimulationState.Ended &&
                    slice.InteractionState.PauseReason ==
                        RealtimePauseReason.CampaignResult &&
                    slice.InteractionState.ActiveModalId is null &&
                    ui.ModalHostForSmoke.Depth == 0 &&
                    slice.AccumulatorSnapshot == campaignAccumulator &&
                    slice.CurrentMinute == campaignCompletionMinute &&
                    string.Equals(
                        slice.CanonicalStateSha256,
                        campaignEndedHash,
                        StringComparison.Ordinal) &&
                    slice.AcceptedCommandCount == campaignCommands &&
                    slice.PresentationRevision == campaignModalRevision + 1,
                "actual result close did not preserve Ended/Core/journal/frame state",
                failures);
            IReadOnlyDictionary<RealtimeTimelineNavigation, Rect2> endedNavigation =
                ui.EventRailForSmoke.NavigationFactsForSmoke.ToDictionary(
                    item => item.Navigation,
                    item => item.Rect);
            PushViewportPrimary(
                viewport,
                endedNavigation[RealtimeTimelineNavigation.Home].GetCenter());
            await SettleLayout();
            long endedNoFutureRevision = slice.PresentationRevision;
            string endedNoFutureHash = slice.CanonicalStateSha256;
            int endedNoFutureCommands = slice.AcceptedCommandCount;
            PushViewportPrimary(
                viewport,
                endedNavigation[RealtimeTimelineNavigation.NextEvent].GetCenter());
            await SettleLayout();
            Require(slice.InteractionState.TimelineSelectedItemId is null &&
                    slice.InteractionState.SelectionId is null &&
                    slice.PresentationRevision == endedNoFutureRevision &&
                    string.Equals(
                        slice.CanonicalStateSha256,
                        endedNoFutureHash,
                        StringComparison.Ordinal) &&
                    slice.AcceptedCommandCount == endedNoFutureCommands,
                "actual no-selection Next with no strict-future target changed " +
                "selection/revision/Core/journal",
                failures);
            RealtimeUiSmokeSpeedFact[] endedSpeedFacts = ui.TopHudForSmoke
                .SpeedFactsForSmoke
                .ToArray();
            Require(slice.InteractionState.Simulation ==
                        RealtimeSimulationState.Ended &&
                    slice.InteractionState.PauseReason ==
                        RealtimePauseReason.CampaignResult &&
                    endedSpeedFacts.Length == 4 &&
                    endedSpeedFacts.All(item => !item.Enabled) &&
                    endedSpeedFacts.Count(item => item.Pressed) == 1 &&
                    endedSpeedFacts.Single(item => item.Pressed).Speed ==
                        RealtimeSimulationSpeed.Paused &&
                    endedSpeedFacts.Single(item =>
                        item.Speed == RealtimeSimulationSpeed.Paused).Text == "■" &&
                    endedSpeedFacts.Single(item =>
                            item.Speed == RealtimeSimulationSpeed.Paused)
                        .Tooltip.Contains(
                            "운영이 종료되어 시간 제어를 사용할 수 없습니다.",
                            StringComparison.Ordinal) &&
                    endedSpeedFacts.Where(item =>
                            item.Speed != RealtimeSimulationSpeed.Paused)
                        .All(item => item.Tooltip.Contains(
                            "운영이 종료되어 배속을 바꿀 수 없습니다.",
                            StringComparison.Ordinal) &&
                            item.AccessibilityDescription.Contains(
                                "운영이 종료되어 배속을 바꿀 수 없습니다.",
                                StringComparison.Ordinal)),
                "actual Ended presentation did not disable P/1/2/4 with exact " +
                "paused state and visible/AX lock copy",
                failures);

            string endedHash = slice.CanonicalStateSha256;
            int endedCommands = slice.AcceptedCommandCount;
            long endedRevision = slice.PresentationRevision;
            RealtimeSimulationSpeed endedRunningSpeed =
                slice.InteractionState.RunningSpeed;
            string? endedSelection = slice.InteractionState.SelectionId;
            foreach ((Key key, RealtimeInputCommand command) in new[]
                     {
                         (Key.P, RealtimeInputCommand.TogglePause),
                         (Key.Key1, RealtimeInputCommand.SetNormalSpeed),
                         (Key.Key2, RealtimeInputCommand.SetFastSpeed),
                         (Key.Key4, RealtimeInputCommand.SetVeryFastSpeed),
                     })
            {
                int endedRequests = actualInputRequests.Count;
                PushViewportKey(viewport, key, pressed: true);
                PushViewportKey(viewport, key, pressed: false);
                await SettleLayout();
                Require(actualInputRequests.Count == endedRequests + 1 &&
                        actualInputRequests[^1].Command == command &&
                        slice.InteractionState.Simulation ==
                            RealtimeSimulationState.Ended &&
                        slice.InteractionState.PauseReason ==
                            RealtimePauseReason.CampaignResult &&
                        slice.InteractionState.RunningSpeed == endedRunningSpeed &&
                        string.Equals(slice.InteractionState.SelectionId,
                            endedSelection, StringComparison.Ordinal) &&
                        string.Equals(slice.CanonicalStateSha256, endedHash,
                            StringComparison.Ordinal) &&
                        slice.AcceptedCommandCount == endedCommands &&
                        slice.PresentationRevision == endedRevision,
                    $"actual Ended controller allowed {key}/{command} to mutate " +
                    "simulation, selection, Core, journal, or presentation",
                    failures);
            }

            int disabledSpeedSignals = 0;
            void ObserveDisabledSpeed(RealtimeSimulationSpeed _) =>
                disabledSpeedSignals++;
            ui.TopHudForSmoke.SpeedRequested += ObserveDisabledSpeed;
            try
            {
                slice.ResetPointerClickCountersForSmoke();
                foreach (RealtimeUiSmokeSpeedFact speedFact in endedSpeedFacts)
                {
                    PushViewportPrimary(viewport, speedFact.Rect.GetCenter());
                    await SettleLayout();
                }
                Require(disabledSpeedSignals == 0 &&
                        slice.PointerClickCounters.Values.Sum() == 0 &&
                        slice.InteractionState.Simulation ==
                            RealtimeSimulationState.Ended &&
                        slice.InteractionState.RunningSpeed == endedRunningSpeed &&
                        string.Equals(slice.InteractionState.SelectionId,
                            endedSelection, StringComparison.Ordinal) &&
                        string.Equals(slice.CanonicalStateSha256, endedHash,
                            StringComparison.Ordinal) &&
                        slice.AcceptedCommandCount == endedCommands &&
                        slice.PresentationRevision == endedRevision,
                    "disabled Ended speed-control mouse clicks emitted a signal, fell " +
                    "through to the map, or mutated controller/Core state",
                    failures);
            }
            finally
            {
                ui.TopHudForSmoke.SpeedRequested -= ObserveDisabledSpeed;
            }

            viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            RenderingServer.ForceDraw(swapBuffers: false);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (failures.Count == failureCountBefore)
            {
                GD.Print(
                    "REALTIME_R2_ACTUAL_SCENE_E2E_PASS production-scene-ready-wire-map-ui " +
                    "actual-SubViewport-PushInput; OS hardware keyboard/window gates separate");
            }
        }
        finally
        {
            if (observedUi is not null && observeInput is not null &&
                GodotObject.IsInstanceValid(observedUi))
            {
                observedUi.InputRequested -= observeInput;
            }
            viewport.NotifyMouseExited();
            RemoveAndFree(viewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task ValidateActualDraftToolLock(
        string label,
        RealtimeDraftToolLockKind expectedKind,
        RealtimeTool expectedTool,
        string expectedBuildToolId,
        SubViewport viewport,
        RealtimeSliceMain slice,
        RealtimeUiRoot ui,
        IReadOnlyList<RealtimeInputRequest> actualInputRequests,
        ICollection<string> failures)
    {
        RealtimeDraftToolLock? initialLock =
            RealtimeInteractionReducer.ResolveDraftToolLock(
                slice.CoreSnapshot.Construction);
        Require(initialLock is not null &&
                initialLock.Kind == expectedKind &&
                initialLock.RequiredTool == expectedTool &&
                string.Equals(
                    initialLock.RequiredBuildToolId,
                    expectedBuildToolId,
                    StringComparison.Ordinal) &&
                slice.LatestPresentation.Hud.BuildModeActive &&
                string.Equals(
                    ui.TopHudForSmoke.MenuTextForSmoke,
                    "건설 취소",
                    StringComparison.Ordinal) &&
                ui.TopHudForSmoke.MenuTooltipForSmoke.Contains(
                    "B 또는 Esc",
                    StringComparison.Ordinal),
            $"{label} did not expose its exact authoritative draft-tool lock/" +
            "actual HUD cancel promise",
            failures);
        if (initialLock is null)
        {
            return;
        }

        bool restoreRunning = slice.InteractionState.Simulation ==
            RealtimeSimulationState.Running;
        if (restoreRunning)
        {
            slice.SetPlayerPausedForSmoke(true);
            await SettleLayout();
        }
        string coreHash = slice.CanonicalStateSha256;
        int coreCommands = slice.AcceptedCommandCount;
        foreach ((Key key, RealtimeInputCommand command) in new[]
                 {
                     (Key.B, RealtimeInputCommand.ToggleBuildShelf),
                     (Key.I, RealtimeInputCommand.SelectInspectTool),
                     (Key.N, RealtimeInputCommand.SelectFirstNodeTool),
                     (Key.L, RealtimeInputCommand.SelectFirstLineTool),
                     (Key.A, RealtimeInputCommand.ToggleAnalysis),
                 })
        {
            int requestCount = actualInputRequests.Count;
            long requestRevision = slice.PresentationRevision;
            PushViewportKey(viewport, key, pressed: true);
            PushViewportKey(viewport, key, pressed: false);
            await SettleLayout();
            RealtimeDraftToolLock? currentLock =
                RealtimeInteractionReducer.ResolveDraftToolLock(
                    slice.CoreSnapshot.Construction);
            bool shelfShowsReason =
                slice.LatestPresentation.BuildShelf.Visible &&
                string.Equals(
                    ui.BuildShelfForSmoke.GuidanceTextForSmoke,
                    slice.LatestPresentation.BuildShelf.Guidance,
                    StringComparison.Ordinal) &&
                slice.LatestPresentation.BuildShelf.Guidance.Contains(
                    "초안 도구 잠금",
                    StringComparison.Ordinal) &&
                slice.LatestPresentation.BuildShelf.Guidance.Contains(
                    initialLock.RejectionReason,
                    StringComparison.Ordinal);
            bool actionShowsReason =
                slice.LatestPresentation.ActionDock.Visible &&
                string.Equals(
                    ui.ActionDockForSmoke.DetailTextForSmoke,
                    slice.LatestPresentation.ActionDock.Detail,
                    StringComparison.Ordinal) &&
                slice.LatestPresentation.ActionDock.Detail.Contains(
                    initialLock.RejectionReason,
                    StringComparison.Ordinal);
            if (key == Key.B)
            {
                bool menuConfirmation =
                    slice.LatestPresentation.BuildShelf.Guidance.Contains(
                        "초안을 모두 취소하려면 B 또는 Esc를 한 번 더 누르세요.",
                        StringComparison.Ordinal);
                bool menuShelfVisible = ui.BuildShelfForSmoke.Visible &&
                    string.Equals(
                        ui.BuildShelfForSmoke.GuidanceTextForSmoke,
                        slice.LatestPresentation.BuildShelf.Guidance,
                        StringComparison.Ordinal) &&
                    ui.BuildShelfForSmoke.GuidanceTextForSmoke.Contains(
                        "초안을 모두 취소하려면 B 또는 Esc를 한 번 더 누르세요.",
                        StringComparison.Ordinal);
                bool menuActionVisible = ui.ActionDockForSmoke.Visible &&
                    string.Equals(
                        ui.ActionDockForSmoke.DetailTextForSmoke,
                        slice.LatestPresentation.ActionDock.Detail,
                        StringComparison.Ordinal) &&
                    ui.ActionDockForSmoke.DetailTextForSmoke.Contains(
                        "초안을 모두 취소하려면 B 또는 Esc를 한 번 더 누르세요.",
                        StringComparison.Ordinal);
                bool menuHashSame = string.Equals(
                    slice.CanonicalStateSha256,
                    coreHash,
                    StringComparison.Ordinal);
                Require(actualInputRequests.Count == requestCount + 1 &&
                        actualInputRequests[^1].Command == command &&
                        currentLock is not null &&
                        currentLock.Kind == expectedKind &&
                        slice.InteractionState.Tool == expectedTool &&
                        slice.InteractionState.Surface == RealtimeSurface.Drawer &&
                        string.Equals(
                            slice.InteractionState.SelectedBuildToolId,
                            expectedBuildToolId,
                            StringComparison.Ordinal) &&
                        slice.LatestPresentation.BuildShelf.Visible &&
                        slice.LatestPresentation.Hud.BuildModeActive &&
                        string.Equals(
                            ui.TopHudForSmoke.MenuTextForSmoke,
                            "건설 취소",
                            StringComparison.Ordinal) &&
                        menuConfirmation &&
                        (menuShelfVisible || menuActionVisible) &&
                        slice.PresentationRevision == requestRevision + 1 &&
                        menuHashSame &&
                        slice.AcceptedCommandCount == coreCommands,
                    $"{label}: B did not enter the visible first stage of the " +
                    "shared authoritative draft-cancel flow without claiming " +
                    "Inspect or mutating Core " +
                    $"(requests={actualInputRequests.Count}/{requestCount + 1}, " +
                    $"last={actualInputRequests.LastOrDefault().Command}, " +
                    $"lock={currentLock?.Kind.ToString() ?? "<none>"}/" +
                    $"{expectedKind}, tool={slice.InteractionState.Tool}/" +
                    $"{expectedTool}, surface={slice.InteractionState.Surface}, " +
                    $"selected={slice.InteractionState.SelectedBuildToolId ?? "<none>"}/" +
                    $"{expectedBuildToolId}, shelfVisible=" +
                    $"{slice.LatestPresentation.BuildShelf.Visible}, " +
                    $"confirmation={menuConfirmation}, liveShelf=" +
                    $"{menuShelfVisible}, liveAction={menuActionVisible}, revision=" +
                    $"{slice.PresentationRevision}/{requestRevision + 1}, " +
                    $"hashSame={menuHashSame}, commands=" +
                    $"{slice.AcceptedCommandCount}/{coreCommands})",
                    failures);
                continue;
            }
            Require(actualInputRequests.Count == requestCount + 1 &&
                    actualInputRequests[^1].Command == command &&
                    currentLock is not null &&
                    currentLock.Kind == expectedKind &&
                    currentLock.RequiredTool == expectedTool &&
                    string.Equals(
                        currentLock.RequiredBuildToolId,
                        expectedBuildToolId,
                        StringComparison.Ordinal) &&
                    slice.InteractionState.Tool == expectedTool &&
                    slice.InteractionState.Surface == RealtimeSurface.Drawer &&
                    string.Equals(
                        slice.InteractionState.SelectedBuildToolId,
                        expectedBuildToolId,
                        StringComparison.Ordinal) &&
                    (shelfShowsReason || actionShowsReason) &&
                    string.Equals(
                        slice.CanonicalStateSha256,
                        coreHash,
                        StringComparison.Ordinal) &&
                    slice.AcceptedCommandCount == coreCommands,
                $"{label}: {key} did not stay on exact {expectedTool}/" +
                $"{expectedBuildToolId} with the visible authored lock reason " +
                $"and invariant Core journal (requests={actualInputRequests.Count}/" +
                $"{requestCount + 1}, last={actualInputRequests.LastOrDefault().Command}, " +
                $"lock={currentLock?.Kind.ToString() ?? "<none>"}, " +
                $"tool={slice.InteractionState.Tool}, selected=" +
                $"{slice.InteractionState.SelectedBuildToolId ?? "<none>"}, " +
                $"shelfVisible={slice.LatestPresentation.BuildShelf.Visible}, " +
                $"shelf={slice.LatestPresentation.BuildShelf.Guidance}, " +
                $"actionVisible={slice.LatestPresentation.ActionDock.Visible}, " +
                $"action={slice.LatestPresentation.ActionDock.Detail})",
                failures);
        }
        if (restoreRunning)
        {
            slice.SetPlayerPausedForSmoke(false);
            await SettleLayout();
        }
    }

    private async Task ValidateActualThermalCuePipeline(
        ICollection<string> failures)
    {
        Vector2I logical = new(
            Mathf.RoundToInt(RealtimeUiMetrics.ReferenceResolution.X),
            Mathf.RoundToInt(RealtimeUiMetrics.ReferenceResolution.Y));
        var viewport = new SubViewport
        {
            Name = "ActualThermalCuePipelineSmokeViewport",
            Size = logical,
            Size2DOverride = logical,
            Size2DOverrideStretch = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        AddChild(viewport);
        PackedScene scene = GD.Load<PackedScene>(
            "res://realtime/r2/RealtimeSliceMain.tscn");
        RealtimeSliceMain slice = scene.Instantiate<RealtimeSliceMain>();
        viewport.AddChild(slice);
        try
        {
            await SettleLayout();
            string modalId = slice.InteractionState.ActiveModalId ?? string.Empty;
            Require(slice.ApplyIntentForSmoke(
                    RealtimeR2Intent.CloseModal(modalId)).Accepted,
                "actual thermal cue fixture could not close the chapter modal",
                failures);
            slice.SetPlayerPausedForSmoke(true);

            RealtimeUiRoot ui = slice.UiForSmoke;
            RealtimePlaceholderMap map = slice.MapForSmoke;
            RealtimeCampaignSnapshot statusBaseline = slice.CoreSnapshot;
            CommercialWorldDefinition statusRuntimeWorld =
                slice.RealtimeWorldForSmoke.Network with
                {
                    Nodes = statusBaseline.Construction.World.Nodes
                        .Where(item => item.Commissioned)
                        .ToArray(),
                    Edges = statusBaseline.Construction.World.Edges
                        .Where(item => item.Commissioned)
                        .ToArray(),
                };
            var (buildingToolId, buildingPoint) =
                slice.AcceptedNodeDraftForSmoke();
            string buildingClassId = buildingToolId["NODE:".Length..];
            var constructionAuthority = new RealtimeConstructionSession(
                statusBaseline.Construction.World,
                statusBaseline.Minute);
            Require(constructionAuthority.SetNodeDraft(
                        buildingClassId,
                        buildingPoint).Accepted &&
                    constructionAuthority.OrderNode().Accepted,
                "actual Building status fixture could not create an authoritative " +
                "Core node project",
                failures);
            ConstructionSnapshot buildingConstruction =
                constructionAuthority.GetSnapshot();
            string buildingAssetId = buildingConstruction.ActiveConstruction?.NodeIds
                .Single() ?? string.Empty;
            RealtimeCampaignSnapshot buildingSnapshot = statusBaseline with
            {
                Construction = buildingConstruction,
            };
            RealtimeInteractionState buildingInteraction = slice.InteractionState with
            {
                Tool = RealtimeTool.Inspect,
                Surface = RealtimeSurface.Inspector,
                SelectionId = buildingAssetId,
                SelectedBuildToolId = null,
            };
            RealtimeSlicePresentation buildingPresentation =
                slice.PresentSnapshotForSmoke(buildingSnapshot, buildingInteraction);
            map.SetPresentation(buildingPresentation.World);
            map.SetPointerFeedback(buildingPresentation.Pointer);
            ui.SetContextDock(buildingPresentation.Context);
            await SettleLayout();
            await ForceActualMapDraw(viewport, map);
            RealtimeWorldAssetStatus buildingStatus = buildingPresentation.World
                .AssetStatuses.Single(item => string.Equals(
                    item.AssetId,
                    buildingAssetId,
                    StringComparison.Ordinal));
            Require(!string.IsNullOrWhiteSpace(buildingAssetId) &&
                    buildingPresentation.World.AssetStatuses.Count ==
                        buildingConstruction.World.Nodes.Count +
                        buildingConstruction.World.Edges.Count &&
                    buildingConstruction.World.Nodes.Single(item => string.Equals(
                        item.NodeId,
                        buildingAssetId,
                        StringComparison.Ordinal)).Commissioned == false &&
                    buildingStatus.State ==
                        RealtimeWorldAssetState.Building &&
                    !buildingStatus.AuthoredUnavailable &&
                    !buildingStatus.ProtectiveOutage &&
                    buildingPresentation.Context.Eyebrow == "공사 중 설비" &&
                    buildingPresentation.Context.Sections.Any(section =>
                        section.Heading == "현재 상태" &&
                        section.Body == "공사 중") &&
                    buildingPresentation.Context.Sections.Any(section =>
                        section.Heading == "운영" &&
                        section.Body.Contains(
                            "완공 전 공급 불가",
                            StringComparison.Ordinal)) &&
                    ui.ContextDockForSmoke.AccessibilitySummaryForSmoke.Contains(
                        "공사 중",
                        StringComparison.Ordinal) &&
                    map.AccessibilityDescription.Contains(
                        "공사 중",
                        StringComparison.Ordinal) &&
                    map.DrawnStateCueForSmoke(buildingAssetId) ==
                        RealtimePlaceholderStateCue.None,
                "authoritative Core uncommissioned node did not reach exact Building " +
                "map/status/context/AX/draw facts",
                failures);

            RealtimeThermalAssetSnapshot unavailableTarget = statusBaseline.Thermal.Assets
                .Where(item => item.AssetKind == ThermalAssetKind.Node)
                .Where(item => statusBaseline.Construction.World.Nodes.Any(node =>
                    node.Commissioned && string.Equals(
                        node.NodeId,
                        item.AssetId,
                        StringComparison.Ordinal)))
                .OrderBy(item => item.AssetId, StringComparer.Ordinal)
                .First();
            ThermalIntervalRequest AuthoredUnavailableRequest(string intervalId) => new(
                intervalId,
                Array.Empty<ThermalLoadRequest>(),
                new[] { unavailableTarget.AssetId },
                Array.Empty<string>(),
                Array.Empty<ThermalLimitOverride>());

            async Task ValidateUnavailableStatus(
                RealtimeThermalSnapshot thermal,
                RealtimeWorldAssetState expectedState,
                bool expectedProtectiveOutage,
                string expectedStateCopy,
                string expectedCauseCopy,
                RealtimePlaceholderStateCue expectedCue,
                string label)
            {
                RealtimeThermalAssetSnapshot coreAsset = thermal.Assets.Single(item =>
                    string.Equals(
                        item.AssetId,
                        unavailableTarget.AssetId,
                        StringComparison.Ordinal));
                RealtimeCampaignSnapshot projectedSnapshot = statusBaseline with
                {
                    Thermal = thermal,
                };
                RealtimeInteractionState projectedInteraction =
                    slice.InteractionState with
                    {
                        Tool = RealtimeTool.Inspect,
                        Surface = RealtimeSurface.Inspector,
                        SelectionId = unavailableTarget.AssetId,
                        SelectedBuildToolId = null,
                    };
                RealtimeSlicePresentation projected = slice.PresentSnapshotForSmoke(
                    projectedSnapshot,
                    projectedInteraction);
                map.SetPresentation(projected.World);
                map.SetPointerFeedback(projected.Pointer);
                ui.SetContextDock(projected.Context);
                await SettleLayout();
                await ForceActualMapDraw(viewport, map);
                RealtimeWorldAssetStatus status = projected.World.AssetStatuses
                    .Single(item => string.Equals(
                        item.AssetId,
                        unavailableTarget.AssetId,
                        StringComparison.Ordinal));
                Require(coreAsset.AuthoredUnavailable &&
                        coreAsset.ProtectiveOutage == expectedProtectiveOutage &&
                        status.State == expectedState &&
                        status.AuthoredUnavailable &&
                        status.ProtectiveOutage == expectedProtectiveOutage &&
                        projected.Context.Sections.Any(section =>
                            section.Heading == "현재 상태" &&
                            string.Equals(
                                section.Body,
                                expectedStateCopy,
                                StringComparison.Ordinal)) &&
                        projected.Context.Sections.Any(section =>
                            section.Heading == "사용불가 원인" &&
                            string.Equals(
                                section.Body,
                                expectedCauseCopy,
                                StringComparison.Ordinal)) &&
                        ui.ContextDockForSmoke.AccessibilitySummaryForSmoke.Contains(
                            expectedStateCopy,
                            StringComparison.Ordinal) &&
                        ui.ContextDockForSmoke.AccessibilitySummaryForSmoke.Contains(
                            expectedCauseCopy,
                            StringComparison.Ordinal) &&
                        map.AccessibilityDescription.Contains(
                            expectedStateCopy,
                            StringComparison.Ordinal) &&
                        map.DrawnStateCueForSmoke(unavailableTarget.AssetId) ==
                            expectedCue,
                    $"{label} Core cause flags did not reach exact map/status/context/" +
                    "AX/draw precedence",
                    failures);
            }

            var authoredUnavailableAuthority = new RealtimeThermalSession(
                slice.RealtimeWorldForSmoke,
                statusRuntimeWorld,
                statusBaseline.Minute);
            authoredUnavailableAuthority.SetOperatingProfile(
                statusRuntimeWorld,
                AuthoredUnavailableRequest("R2_AUTHORED_UNAVAILABLE"));
            await ValidateUnavailableStatus(
                authoredUnavailableAuthority.GetSnapshot(),
                RealtimeWorldAssetState.AuthoredUnavailable,
                expectedProtectiveOutage: false,
                "계획 사용불가",
                "작성된 계획 사용불가가 적용 중이며 공급 경로에서 제외됩니다.",
                RealtimePlaceholderStateCue.AuthoredUnavailableBars,
                "authored unavailable");

            var dualCauseAuthority = new RealtimeThermalSession(
                slice.RealtimeWorldForSmoke,
                statusRuntimeWorld,
                statusBaseline.Minute,
                new[] { unavailableTarget.AssetId });
            dualCauseAuthority.SetOperatingProfile(
                statusRuntimeWorld,
                AuthoredUnavailableRequest("R2_DUAL_UNAVAILABLE"));
            await ValidateUnavailableStatus(
                dualCauseAuthority.GetSnapshot(),
                RealtimeWorldAssetState.ProtectiveOutage,
                expectedProtectiveOutage: true,
                "보호정지 · 계획 사용불가 겹침",
                "작성된 사용불가와 열 보호정지가 함께 적용 중입니다. 더 늦은 복귀 시각까지 공급 경로에서 제외됩니다.",
                RealtimePlaceholderStateCue.ProtectiveOutageCross,
                "dual authored/protective outage");
            GD.Print(
                "REALTIME_R2_ACTUAL_STATUS_AUTHORITY_PASS " +
                $"building={buildingAssetId}; unavailable={unavailableTarget.AssetId}; " +
                "dual-cause-preserved");

            RealtimeSmokeLinePlan plan = slice.SmokeLinePlan;
            slice.AdvanceToForSmoke(plan.OrderMinute);
            foreach (RealtimeR2Intent intent in plan.Intents)
            {
                RealtimeR2IntentResult result = slice.ApplyIntentForSmoke(intent);
                Require(result.Accepted,
                    $"actual thermal cue line setup rejected {intent.Kind}: " +
                    $"{result.Error ?? "<none>"}",
                    failures);
            }
            RealtimeSmokeThermalBoundary boundary = slice.SmokeBoundaryFacts.Thermal
                .Where(item => item.EmergencyStartMinute.HasValue &&
                    item.TripMinute.HasValue &&
                    item.RecoveryMinute.HasValue)
                .OrderBy(item => item.EmergencyStartMinute)
                .ThenBy(item => item.TripMinute)
                .ThenBy(item => item.AssetId, StringComparer.Ordinal)
                .First();
            async Task ValidateState(
                long minute,
                ThermalOperatingState coreState,
                RealtimeWorldAssetState presentationState,
                RealtimePlaceholderStateCue drawnCue,
                string visibleStateLabel,
                string contextStateCopy)
            {
                slice.AdvanceToForSmoke(minute);
                RealtimeR2IntentResult selection = slice.ApplyIntentForSmoke(
                    RealtimeR2Intent.Select(boundary.AssetId));
                Require(selection.Accepted,
                    $"actual thermal cue could not select {boundary.AssetId} at {minute}",
                    failures);
                map.QueueRedraw();
                viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                RenderingServer.ForceDraw(swapBuffers: false);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                RealtimeThermalAssetSnapshot core = slice.CoreSnapshot.Thermal.Assets
                    .Single(item => string.Equals(
                        item.AssetId,
                        boundary.AssetId,
                        StringComparison.Ordinal));
                RealtimeWorldAssetStatus presented = slice.LatestPresentation.World
                    .AssetStatuses
                    .Single(item => string.Equals(
                        item.AssetId,
                        boundary.AssetId,
                        StringComparison.Ordinal));
                RealtimeContextSectionPresentation[] currentStateSections =
                    slice.LatestPresentation.Context.Sections
                        .Where(section => string.Equals(
                            section.Heading,
                            "현재 상태",
                            StringComparison.Ordinal))
                        .ToArray();
                Require(slice.CurrentMinute == minute &&
                        slice.LatestPresentation.CoreSnapshot.Minute == minute &&
                        slice.LatestPresentation.World.Minute == minute &&
                        core.State == coreState &&
                        presented.State == presentationState &&
                        string.Equals(
                            slice.LatestPresentation.World.SelectedAssetId,
                            boundary.AssetId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            slice.LatestPresentation.Context.SubjectId,
                            boundary.AssetId,
                            StringComparison.Ordinal) &&
                        map.AccessibilityDescription.Contains(
                            "선택 ",
                            StringComparison.Ordinal) &&
                        map.AccessibilityDescription.Contains(
                            visibleStateLabel,
                            StringComparison.Ordinal) &&
                        currentStateSections.Length == 1 &&
                        string.Equals(
                            currentStateSections[0].Body,
                            contextStateCopy,
                            StringComparison.Ordinal) &&
                        map.DrawnStateCueForSmoke(boundary.AssetId) == drawnCue,
                    $"actual {boundary.AssetId}@{minute} did not preserve exact Core " +
                    $"{coreState} -> presentation {presentationState} -> selected AX " +
                    $"'{visibleStateLabel}' -> _Draw cue {drawnCue} facts " +
                    $"(core={core.State}, presentation={presented.State}, " +
                    $"context={string.Join("/", currentStateSections.Select(item => item.Body))}, " +
                    $"ax={map.AccessibilityDescription}, " +
                    $"draw={map.DrawnStateCueForSmoke(boundary.AssetId)?.ToString() ?? "<none>"})",
                    failures);
            }

            await ValidateState(
                boundary.EmergencyStartMinute!.Value,
                ThermalOperatingState.Emergency,
                RealtimeWorldAssetState.Emergency,
                RealtimePlaceholderStateCue.EmergencyTriangle,
                "비상 운전",
                "비상 운전");
            await ValidateState(
                boundary.TripMinute!.Value,
                ThermalOperatingState.ProtectiveOutage,
                RealtimeWorldAssetState.ProtectiveOutage,
                RealtimePlaceholderStateCue.ProtectiveOutageCross,
                "보호정지",
                "보호정지");
            await ValidateState(
                boundary.RecoveryMinute!.Value,
                ThermalOperatingState.Continuous,
                RealtimeWorldAssetState.Normal,
                RealtimePlaceholderStateCue.None,
                "정상",
                "연속 운전");
            GD.Print(
                "REALTIME_R2_ACTUAL_THERMAL_RECOVERY_PASS " +
                $"asset={boundary.AssetId}; minute={boundary.RecoveryMinute.Value}; " +
                "core=Continuous; presentation=Normal; ax=정상; draw=None");
        }
        finally
        {
            RemoveAndFree(viewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task ValidateActualSliceUhdGpuRenderTarget(
        ICollection<string> failures)
    {
        Vector2I physical = new(3840, 2160);
        Vector2 logical = RealtimeUiMetrics.ReferenceResolution;
        Vector2I logicalPixels = new(
            Mathf.RoundToInt(logical.X),
            Mathf.RoundToInt(logical.Y));
        bool headless = string.Equals(
            DisplayServer.GetName(),
            "headless",
            StringComparison.OrdinalIgnoreCase);
        var viewport = new SubViewport
        {
            Name = "ActualRealtimeSliceNativeUhdRenderTarget",
            Size = physical,
            Size2DOverride = logicalPixels,
            Size2DOverrideStretch = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            HandleInputLocally = true,
        };
        AddChild(viewport);
        PackedScene scene = GD.Load<PackedScene>(
            "res://realtime/r2/RealtimeSliceMain.tscn");
        RealtimeSliceMain slice = scene.Instantiate<RealtimeSliceMain>();
        viewport.AddChild(slice);
        try
        {
            await SettleLayout();
            string modalId = slice.InteractionState.ActiveModalId ?? string.Empty;
            Require(slice.ApplyIntentForSmoke(
                    RealtimeR2Intent.CloseModal(modalId)).Accepted,
                "native UHD actual-slice fixture could not close its chapter modal",
                failures);
            slice.SetPlayerPausedForSmoke(true);
            string selectedAssetId = slice.CoreSnapshot.Construction.World.Nodes
                .Where(item => item.Commissioned)
                .OrderBy(item => item.NodeId, StringComparer.Ordinal)
                .First().NodeId;
            Require(slice.ApplyIntentForSmoke(
                    RealtimeR2Intent.Select(selectedAssetId)).Accepted,
                $"native UHD actual slice could not select {selectedAssetId}",
                failures);

            RealtimeUiRoot ui = slice.UiForSmoke;
            RealtimePlaceholderMap map = slice.MapForSmoke;
            ui.ApplyLayoutForSmoke(physical, logical, uiScalePercent: 100);
            map.ApplyLayoutForSmoke(RealtimeUiMetrics.ForWindow(
                physical,
                uiScalePercent: 100));
            await SettleLayout();
            ui.ApplyLayoutForSmoke(physical, logical, uiScalePercent: 100);
            map.ApplyLayoutForSmoke(RealtimeUiMetrics.ForWindow(
                physical,
                uiScalePercent: 100));
            await SettleLayout();

            RealtimeUiSmokeLayoutSnapshot snapshot = ui.CaptureLayoutForSmoke(logical);
            Vector2 textureSize = viewport.GetTexture().GetSize();
            Require(viewport.Size == physical &&
                    viewport.Size2DOverride == logicalPixels &&
                    viewport.Size2DOverrideStretch &&
                    textureSize.IsEqualApprox(new Vector2(physical.X, physical.Y)) &&
                    snapshot.Profile.PhysicalSize == physical &&
                    snapshot.Profile.Tier == RealtimeResolutionTier.UltraHd &&
                    Mathf.IsEqualApprox(
                        snapshot.Profile.PhysicalRenderScale,
                        2f) &&
                    Mathf.IsEqualApprox(
                        snapshot.Profile.AccessibilityScale,
                        1f),
                "actual RealtimeSliceMain UHD SubViewport lost exact native target, " +
                "fixed logical stretch, or layout profile semantics " +
                $"(viewport={viewport.Size}, override={viewport.Size2DOverride}, " +
                $"stretch={viewport.Size2DOverrideStretch}, texture={textureSize}, " +
                $"profile={snapshot.Profile.PhysicalSize}/" +
                $"{snapshot.Profile.PhysicalRenderScale:0.##})",
                failures);
            Require(string.Equals(
                        slice.InteractionState.SelectionId,
                        selectedAssetId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        slice.LatestPresentation.World.SelectedAssetId,
                        selectedAssetId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        slice.LatestPresentation.Context.SubjectId,
                        selectedAssetId,
                        StringComparison.Ordinal) &&
                    map.AccessibilityDescription.Contains(
                        "선택 ",
                        StringComparison.Ordinal) &&
                    !map.AccessibilityDescription.Contains(
                        "선택 없음",
                        StringComparison.Ordinal),
                "actual UHD slice lost exact controller -> world/context -> map AX " +
                $"selection semantics for {selectedAssetId}",
                failures);
            ValidateSurfaceGeometry(
                snapshot,
                logical,
                slice.LatestPresentation,
                "actual-slice/3840x2160@100%",
                failures);
            ValidateButtons(
                snapshot,
                ExpectedPrimaryCtaCount(slice.LatestPresentation),
                "actual-slice/3840x2160@100%",
                failures);
            ValidateText(snapshot, "actual-slice/3840x2160@100%", failures);
            ValidateScroll(
                ui,
                snapshot,
                slice.LatestPresentation,
                "actual-slice/3840x2160@100%",
                failures);
            RealtimeTimelineItemPresentation[] visibleItems =
                slice.LatestPresentation.Rail.Items
                    .Where(item => item.Visibility != RealtimeTimelineVisibility.Hidden)
                    .Where(item =>
                        item.StartMinute <=
                            slice.LatestPresentation.Rail.HorizonEndMinute &&
                        (item.EndMinute ?? item.StartMinute) >=
                            slice.LatestPresentation.Rail.HorizonStartMinute)
                    .OrderBy(item => item.StartMinute)
                    .ThenBy(item => item.Priority)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .ToArray();
            ValidateTimeline(
                snapshot,
                visibleItems,
                100,
                slice.LatestPresentation.Rail.Expanded,
                "actual-slice/3840x2160@100%",
                failures);

            if (headless)
            {
                GD.Print(
                    "REALTIME_R2_GPU_UHD_RENDER_TARGET_OPEN headless renderer; actual " +
                    "RealtimeSliceMain 3840x2160 native texture allocation, fixed-logical " +
                    "stretch, layout, and selection were checked, but GPU pixel readback " +
                    "and checksum were not claimed");
            }
            else
            {
                viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
                map.QueueRedraw();
                RenderingServer.ForceDraw(swapBuffers: false);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                RenderingServer.ForceDraw(swapBuffers: false);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Image? image = viewport.GetTexture().GetImage();
                try
                {
                    byte[] pixels = image?.GetData() ?? Array.Empty<byte>();
                    bool nonBlank = pixels.Length > 0 &&
                        pixels.Skip(1).Any(value => value != pixels[0]);
                    string checksum = pixels.Length == 0
                        ? string.Empty
                        : Convert.ToHexString(SHA256.HashData(pixels))
                            .ToLowerInvariant();
                    Require(image is not null &&
                            image.GetSize() == physical &&
                            nonBlank &&
                            checksum.Length == 64,
                        "actual UHD native Godot render-target readback was blank, " +
                        "wrong-sized, or lacked a SHA-256 checksum",
                        failures);
                    if (image is not null && image.GetSize() == physical && nonBlank &&
                        checksum.Length == 64)
                    {
                        _offscreenReadbackVerified = true;
                        GD.Print(
                            "REALTIME_R2_GPU_UHD_RENDER_TARGET_PASS " +
                            "actual-RealtimeSliceMain native-Godot-SubViewport " +
                            $"3840x2160 fixed-logical=1920x1080 nonblank-sha256={checksum}; " +
                            "this is offscreen render-target evidence, not physical-display " +
                            "evidence");
                    }
                }
                finally
                {
                    image?.Dispose();
                }
            }

            Vector2I screen = headless
                ? Vector2I.Zero
                : DisplayServer.ScreenGetSize();
            GD.Print(
                "REALTIME_R2_PHYSICAL_UHD_DISPLAY_OPEN " +
                (headless
                    ? "unavailable=headless-no-physical-display"
                    : screen.X < physical.X || screen.Y < physical.Y
                        ? $"unavailable=screen-{screen.X}x{screen.Y}"
                        : $"external-panel-observation-not-collected screen={screen.X}x{screen.Y}") +
                "; automated SubViewport/window evidence is not a physical UHD claim");
        }
        finally
        {
            RemoveAndFree(viewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private sealed record ActualMapCandidateFact(
        CoreMapPoint WorldCursor,
        string[] CandidateIds,
        int CandidateIndex,
        string CandidateId,
        string VisibleBadge,
        string PreferredCandidateId,
        Vector2 ProjectedViewportPoint);

    private async Task ValidateActualMapCandidatePersistence(
        SubViewport viewport,
        RealtimeSliceMain slice,
        RealtimePlaceholderMap map,
        CoreMapPoint worldPoint,
        List<RealtimeInputRequest> actualInputRequests,
        ICollection<string> failures)
    {
        RealtimeR2IntentResult resizeReset = slice.ApplyIntentForSmoke(
            RealtimeR2Intent.Select(null));
        await SettleLayout();
        Rect2 originalRect = slice.MapInteractionRectForSmoke;
        Require(resizeReset.Accepted &&
                slice.InteractionState.SelectionId is null &&
                slice.InteractionState.Surface == RealtimeSurface.World,
            "map-resize candidate fixture could not reset the prior selection",
            failures);
        ActualMapCandidateFact? resizeFact = await ArmNonFirstActualCandidate(
            viewport,
            slice,
            map,
            worldPoint,
            actualInputRequests,
            "map-resize",
            failures);
        if (resizeFact is null)
        {
            return;
        }

        var resizedRect = new Rect2(
            originalRect.Position + new Vector2(17f, 11f),
            new Vector2(
                Math.Max(320f, originalRect.Size.X - 157f),
                Math.Max(240f, originalRect.Size.Y - 91f)));
        Require(!RectApproximatelyEqual(originalRect, resizedRect),
            "map-resize fixture did not produce a distinct interaction rect",
            failures);
        slice.ApplyMapInteractionRectForSmoke(resizedRect);
        await SettleLayout();
        Vector2 resizedProjection = map.ViewportPointForSmoke(worldPoint);
        Require(RectApproximatelyEqual(
                    slice.MapInteractionRectForSmoke,
                    resizedRect) &&
                resizeFact.ProjectedViewportPoint.DistanceTo(resizedProjection) > 1f,
            "actual map rect did not resize/reproject the stored world cursor",
            failures);
        await RequireActualCandidateFact(
            viewport,
            map,
            resizeFact,
            "map resize without pointer motion",
            failures);
        await ConfirmActualCandidateFact(
            viewport,
            slice,
            resizeFact,
            actualInputRequests,
            "map resize without pointer motion",
            failures);

        slice.ApplyMapInteractionRectForSmoke(originalRect);
        await SettleLayout();
        RealtimeR2IntentResult modalReset = slice.ApplyIntentForSmoke(
            RealtimeR2Intent.Select(null));
        await SettleLayout();
        Require(modalReset.Accepted &&
                RectApproximatelyEqual(
                    slice.MapInteractionRectForSmoke,
                    originalRect) &&
                slice.InteractionState.SelectionId is null &&
                slice.InteractionState.Surface == RealtimeSurface.World,
            "modal candidate fixture could not restore its map rect/selection",
            failures);
        ActualMapCandidateFact? modalFact = await ArmNonFirstActualCandidate(
            viewport,
            slice,
            map,
            worldPoint,
            actualInputRequests,
            "blocking-modal",
            failures);
        if (modalFact is null)
        {
            return;
        }
        Require(modalFact.CandidateIds.SequenceEqual(
                    resizeFact.CandidateIds,
                    StringComparer.Ordinal) &&
                string.Equals(
                    modalFact.CandidateId,
                    resizeFact.CandidateId,
                    StringComparison.Ordinal),
            "candidate reset did not reproduce the same stable non-first overlap",
            failures);

        const string modalId = "ACTUAL_MAP_CANDIDATE_PERSISTENCE_MODAL";
        int modalInputRequests = actualInputRequests.Count;
        RealtimeR2IntentResult open = slice.ApplyIntentForSmoke(
            RealtimeR2Intent.OpenModal(
                modalId,
                RealtimeModalKind.RecoveryConfirmation,
                RealtimePauseReason.RecoveryConfirmation,
                "MAP"));
        await SettleLayout();
        Require(open.Accepted &&
                slice.InteractionState.Surface == RealtimeSurface.BlockingModal &&
                slice.UiForSmoke.ModalHostForSmoke.Depth == 1 &&
                map.WorldCursorForSmoke == modalFact.WorldCursor &&
                map.CandidateIdsForSmoke.Count == 0 &&
                map.ActiveCandidateIdForSmoke is null &&
                string.Equals(
                    map.PreferredCandidateIdForSmoke,
                    modalFact.CandidateId,
                    StringComparison.Ordinal) &&
                actualInputRequests.Count == modalInputRequests,
            "blocking modal did not retain the exact semantic candidate/world cursor",
            failures);
        RealtimeR2IntentResult close = slice.ApplyIntentForSmoke(
            RealtimeR2Intent.CloseModal(modalId));
        await SettleLayout();
        Require(close.Accepted &&
                slice.InteractionState.Simulation ==
                    RealtimeSimulationState.PlayerPaused &&
                slice.InteractionState.Surface == RealtimeSurface.World &&
                slice.UiForSmoke.ModalHostForSmoke.Depth == 0 &&
                ReferenceEquals(slice.UiForSmoke.FocusOwnerForSmoke, map) &&
                actualInputRequests.Count == modalInputRequests,
            "blocking modal roundtrip did not restore the actual map owner",
            failures);
        await RequireActualCandidateFact(
            viewport,
            map,
            modalFact,
            "blocking modal close without pointer motion",
            failures);
        await ConfirmActualCandidateFact(
            viewport,
            slice,
            modalFact,
            actualInputRequests,
            "blocking modal close without pointer motion",
            failures);
    }

    private async Task<ActualMapCandidateFact?> ArmNonFirstActualCandidate(
        SubViewport viewport,
        RealtimeSliceMain slice,
        RealtimePlaceholderMap map,
        CoreMapPoint worldPoint,
        List<RealtimeInputRequest> actualInputRequests,
        string label,
        ICollection<string> failures)
    {
        map.GrabFocus();
        PushViewportPointerMotion(
            viewport,
            map.ViewportPointForSmoke(worldPoint));
        await SettleLayout();
        string[] stableIds = map.CandidateIdsForSmoke.ToArray();
        Require(stableIds.Length >= 2,
            $"{label} fixture needs a two-or-more-candidate overlap",
            failures);
        if (stableIds.Length < 2)
        {
            return null;
        }

        int requestsBefore = actualInputRequests.Count;
        int acceptedBefore = slice.AcceptedCommandCount;
        long revisionBefore = slice.PresentationRevision;
        var expectedRequests = new List<RealtimeInputCommand>();
        if (map.CandidateIndexForSmoke == 1)
        {
            PushViewportKey(viewport, Key.Q, pressed: true);
            PushViewportKey(viewport, Key.Q, pressed: false);
            expectedRequests.Add(RealtimeInputCommand.CycleCandidatePrevious);
            await SettleLayout();
        }
        for (int guard = 0;
             map.CandidateIndexForSmoke != 1 && guard <= stableIds.Length;
             guard++)
        {
            PushViewportKey(viewport, Key.E, pressed: true);
            PushViewportKey(viewport, Key.E, pressed: false);
            expectedRequests.Add(RealtimeInputCommand.CycleCandidateNext);
            await SettleLayout();
        }
        if (expectedRequests.Count > 0 &&
            expectedRequests[^1] == RealtimeInputCommand.CycleCandidatePrevious)
        {
            PushViewportKey(viewport, Key.E, pressed: true);
            PushViewportKey(viewport, Key.E, pressed: false);
            expectedRequests.Add(RealtimeInputCommand.CycleCandidateNext);
            await SettleLayout();
        }
        Require(map.CandidateIndexForSmoke == 1 &&
                map.CandidateIdsForSmoke.SequenceEqual(
                    stableIds,
                    StringComparer.Ordinal) &&
                actualInputRequests.Count == requestsBefore +
                    expectedRequests.Count &&
                actualInputRequests.Skip(requestsBefore)
                    .Select(request => request.Command)
                    .SequenceEqual(expectedRequests) &&
                slice.AcceptedCommandCount == acceptedBefore &&
                slice.PresentationRevision == revisionBefore,
            $"{label} Q/E route did not arm exact stable candidate index 1",
            failures);

        await ForceActualMapDraw(viewport, map);
        CoreMapPoint? cursor = map.WorldCursorForSmoke;
        string candidateId = map.ActiveCandidateIdForSmoke ?? string.Empty;
        Require(cursor.HasValue &&
                cursor.Value == worldPoint &&
                !string.IsNullOrWhiteSpace(candidateId) &&
                string.Equals(candidateId, stableIds[1], StringComparison.Ordinal) &&
                string.Equals(
                    map.PreferredCandidateIdForSmoke,
                    candidateId,
                    StringComparison.Ordinal) &&
                map.ActiveCandidateVisibleLabelForSmoke.StartsWith(
                    $"후보 2/{stableIds.Length} · ",
                    StringComparison.Ordinal) &&
                map.ActiveCandidateOutlineVisibleForSmoke &&
                string.Equals(
                    map.DrawnActiveCandidateIdForSmoke,
                    candidateId,
                    StringComparison.Ordinal),
            $"{label} did not visibly arm the exact non-first semantic candidate",
            failures);
        if (!cursor.HasValue || string.IsNullOrWhiteSpace(candidateId))
        {
            return null;
        }
        return new ActualMapCandidateFact(
            cursor.Value,
            stableIds,
            1,
            candidateId,
            map.ActiveCandidateVisibleLabelForSmoke,
            map.PreferredCandidateIdForSmoke ?? string.Empty,
            map.ViewportPointForSmoke(worldPoint));
    }

    private async Task RequireActualCandidateFact(
        SubViewport viewport,
        RealtimePlaceholderMap map,
        ActualMapCandidateFact expected,
        string label,
        ICollection<string> failures)
    {
        await ForceActualMapDraw(viewport, map);
        Require(map.WorldCursorForSmoke == expected.WorldCursor &&
                map.CandidateIdsForSmoke.SequenceEqual(
                    expected.CandidateIds,
                    StringComparer.Ordinal) &&
                map.CandidateIndexForSmoke == expected.CandidateIndex &&
                string.Equals(
                    map.ActiveCandidateIdForSmoke,
                    expected.CandidateId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    map.PreferredCandidateIdForSmoke,
                    expected.PreferredCandidateId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    map.ActiveCandidateVisibleLabelForSmoke,
                    expected.VisibleBadge,
                    StringComparison.Ordinal) &&
                map.ActiveCandidateOutlineVisibleForSmoke &&
                string.Equals(
                    map.DrawnActiveCandidateIdForSmoke,
                    expected.CandidateId,
                    StringComparison.Ordinal),
            $"{label} changed world cursor/candidate order/index/ID/badge/drawn outline",
            failures);
    }

    private async Task ConfirmActualCandidateFact(
        SubViewport viewport,
        RealtimeSliceMain slice,
        ActualMapCandidateFact expected,
        List<RealtimeInputRequest> actualInputRequests,
        string label,
        ICollection<string> failures)
    {
        int requestsBefore = actualInputRequests.Count;
        int acceptedBefore = slice.AcceptedCommandCount;
        long revisionBefore = slice.PresentationRevision;
        PushViewportKey(viewport, Key.Enter, pressed: true);
        PushViewportKey(viewport, Key.Enter, pressed: false);
        await SettleLayout();
        Require(actualInputRequests.Count == requestsBefore + 1 &&
                actualInputRequests[^1].Command ==
                    RealtimeInputCommand.ConfirmOrSelect &&
                string.Equals(
                    slice.InteractionState.SelectionId,
                    expected.CandidateId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    slice.LatestPresentation.Context.SubjectId,
                    expected.CandidateId,
                    StringComparison.Ordinal) &&
                slice.AcceptedCommandCount == acceptedBefore &&
                slice.PresentationRevision == revisionBefore + 1,
            $"{label} Enter did not select the exact retained candidate ID",
            failures);
    }

    private async Task ForceActualMapDraw(
        SubViewport viewport,
        RealtimePlaceholderMap map)
    {
        map.QueueRedraw();
        viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        RenderingServer.ForceDraw(swapBuffers: false);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static IReadOnlyList<RealtimeTransition>
        AdvanceActualSceneByExactMinutes(
            RealtimeSliceMain slice,
            long targetMinute,
            string label,
            ICollection<string> failures)
    {
        var transitions = new List<RealtimeTransition>();
        while (slice.CurrentMinute < targetMinute)
        {
            if (slice.InteractionState.Simulation ==
                    RealtimeSimulationState.AutoPaused &&
                slice.InteractionState.ActiveModalId is null)
            {
                RealtimeR2IntentResult acknowledgement = slice.ApplyIntentForSmoke(
                    RealtimeR2Intent.AcknowledgeAutoPause());
                Require(acknowledgement.Accepted &&
                        acknowledgement.CoreCommandResult is null &&
                        acknowledgement.JournalDelta == 0 &&
                        slice.InteractionState.Simulation ==
                            RealtimeSimulationState.Running,
                    $"{label} could not resume a typed critical auto-pause",
                    failures);
            }
            Require(slice.InteractionState.Simulation ==
                        RealtimeSimulationState.Running &&
                    slice.InteractionState.RunningSpeed ==
                        RealtimeSimulationSpeed.Normal,
                $"{label} was not in exact normal realtime flow before minute " +
                $"{slice.CurrentMinute + 1}",
                failures);
            if (slice.InteractionState.Simulation !=
                    RealtimeSimulationState.Running ||
                slice.InteractionState.RunningSpeed !=
                    RealtimeSimulationSpeed.Normal)
            {
                break;
            }

            long beforeMinute = slice.CurrentMinute;
            RealtimeFrameAccumulatorSnapshot beforeAccumulator =
                slice.AccumulatorSnapshot;
            long retainedFramesBefore = slice.RetainedFrameDebt.Sum(item =>
                item.FrameCount);
            RealtimeR2FrameResult frame = slice.InjectFramesForSmoke(
                frameCount: 60,
                framesPerSecond: 60);
            transitions.AddRange(frame.Transitions);
            long retainedFramesAfter = frame.RetainedFrameDebt.Sum(item =>
                item.FrameCount);
            Require(frame.RequestedFrameCount == 60 &&
                    frame.FramesPerSecond == 60 &&
                    frame.Frame?.AppliedMinutes == 1 &&
                    frame.Frame.Campaign is not null &&
                    frame.Frame.Campaign.Transitions.SequenceEqual(
                        frame.Transitions) &&
                    frame.CoreSnapshot.Minute == beforeMinute + 1 &&
                    slice.CurrentMinute == beforeMinute + 1 &&
                    frame.PresentationRevisionDelta == 1 &&
                    frame.Frame.Accumulator == slice.AccumulatorSnapshot &&
                    slice.AccumulatorSnapshot.AppliedSimulationMinutes ==
                        beforeAccumulator.AppliedSimulationMinutes + 1 &&
                    frame.ConsumedFrameCount + retainedFramesAfter ==
                        retainedFramesBefore + frame.RequestedFrameCount &&
                    frame.RetainedFrameDebt.All(item =>
                        item.FramesPerSecond == 60 &&
                        item.SpeedMultiplier == 1),
                $"{label} did not apply one exact aggregate frame minute at " +
                $"{beforeMinute} (requested={frame.RequestedFrameCount}, " +
                $"consumed={frame.ConsumedFrameCount}, " +
                $"applied={frame.Frame?.AppliedMinutes}, " +
                $"minute={slice.CurrentMinute}, " +
                $"revisionDelta={frame.PresentationRevisionDelta}, " +
                $"retained={retainedFramesBefore}->{retainedFramesAfter}, " +
                $"fractional={beforeAccumulator.FractionalMinuteUnits}->" +
                $"{slice.AccumulatorSnapshot.FractionalMinuteUnits})",
                failures);
            if (slice.CurrentMinute <= beforeMinute)
            {
                break;
            }
        }
        Require(slice.CurrentMinute == targetMinute,
            $"{label} stopped at {slice.CurrentMinute}, expected {targetMinute}",
            failures);
        return Array.AsReadOnly(transitions.ToArray());
    }

    private static void PushViewportKey(
        SubViewport viewport,
        Key key,
        bool pressed,
        bool shiftPressed = false,
        bool echo = false) =>
        viewport.PushInput(
            KeyEvent(key, pressed, shiftPressed, echo),
            inLocalCoords: true);

    private static void PushViewportPointerMotion(
        SubViewport viewport,
        Vector2 point) => viewport.PushInput(
        new InputEventMouseMotion
        {
            Position = point,
            GlobalPosition = point,
        },
        inLocalCoords: true);

    private static void PushViewportPrimary(
        SubViewport viewport,
        Vector2 point,
        bool movePointer = true)
    {
        if (movePointer)
        {
            PushViewportPointerMotion(viewport, point);
        }
        viewport.PushInput(
            new InputEventMouseButton
            {
                Position = point,
                GlobalPosition = point,
                ButtonIndex = MouseButton.Left,
                ButtonMask = MouseButtonMask.Left,
                Pressed = true,
            },
            inLocalCoords: true);
        viewport.PushInput(
            new InputEventMouseButton
            {
                Position = point,
                GlobalPosition = point,
                ButtonIndex = MouseButton.Left,
                ButtonMask = (MouseButtonMask)0,
                Pressed = false,
            },
            inLocalCoords: true);
    }

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
            AddChild(slice);
            await SettleNativeWindow();
            RealtimeUiRoot ui = slice.UiForSmoke;
            RealtimePlaceholderMap map = slice.MapForSmoke;
            ui.ModalHostForSmoke.PressPrimaryForSmoke();
            await SettleNativeWindow();
            ui.TopHudForSmoke.PressSpeedForSmoke(RealtimeSimulationSpeed.Paused);
            string selectedEventId = slice.LatestPresentation.Rail.Items.First(item =>
                RealtimeSlicePresenter.ResolveTimelineTarget(
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
        RealtimePlaceholderMap map,
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

    private async Task ValidateLivePrimaryCta(
        RealtimeSlicePresentation restorePresentation,
        ICollection<string> failures)
    {
        var slice = new RealtimeSliceMain();
        using var sliceLifetime = slice.FreeAfterSmoke();
        slice.BootstrapForSmoke();
        string modalId = slice.InteractionState.ActiveModalId ?? string.Empty;
        RealtimeR2IntentResult close = slice.ApplyIntentForSmoke(
            RealtimeR2Intent.CloseModal(modalId));
        RealtimeSmokeLinePlan plan = slice.SmokeLinePlan;
        slice.AdvanceToForSmoke(plan.OrderMinute);
        RealtimeR2IntentResult tool = slice.ApplyIntentForSmoke(
            RealtimeR2Intent.SelectBuildTool(
                RealtimeTool.BuildLine,
                $"LINE:{plan.LineClassId}:{plan.PoleClassId}"));
        RealtimeR2IntentResult start = slice.ApplyIntentForSmoke(plan.Intents[0]);
        RealtimeR2IntentResult finish = slice.ApplyIntentForSmoke(plan.Intents[1]);
        Require(close.Accepted && tool.Accepted && start.CoreCommandResult?.Accepted == true &&
                finish.CoreCommandResult?.Accepted == true,
            "live CTA fixture could not reach the actual R1 line comparison state",
            failures);

        RealtimeSlicePresentation actionPresentation = slice.LatestPresentation;
        Present(actionPresentation);
        _uiRoot.ApplyLayoutForSmoke(
            RealtimeUiMetrics.ReferenceResolution,
            RealtimeUiMetrics.ReferenceResolution,
            uiScalePercent: 100);
        await SettleLayout();
        BaseButton primary = _uiRoot.PrimaryActionForSmoke();
        int beforeCommands = slice.AcceptedCommandCount;
        long beforeSequence = slice.CommandSequence;
        long beforeRevision = slice.PresentationRevision;
        long beforeMinute = slice.CurrentMinute;
        string beforeHash = slice.CanonicalStateSha256;

        slice.AttachActionUiForSmoke(_uiRoot);
        try
        {
            primary.EmitSignal(BaseButton.SignalName.Pressed);
        }
        finally
        {
            slice.DetachActionUiForSmoke(_uiRoot);
        }
        Require(slice.AcceptedCommandCount == beforeCommands + 1 &&
                slice.CommandSequence == beforeSequence + 1 &&
                slice.PresentationRevision == beforeRevision + 1 &&
                slice.CurrentMinute == beforeMinute &&
                !string.Equals(beforeHash, slice.CanonicalStateSha256,
                    StringComparison.Ordinal) &&
                slice.CoreSnapshot.Construction.ActiveConstruction is not null &&
                slice.CoreSnapshot.Construction.LineDraft is null,
            "one live primary CTA press did not yield exactly one accepted Core " +
            "command/result and one presentation revision",
            failures);
        Present(restorePresentation);
        await SettleLayout();
    }

    private static void ValidateSurfaceGeometry(
        RealtimeUiSmokeLayoutSnapshot snapshot,
        Vector2 logical,
        RealtimeSlicePresentation presentation,
        string label,
        ICollection<string> failures)
    {
        var viewport = new Rect2(Vector2.Zero, logical);
        var expected = new Dictionary<string, Rect2>(StringComparer.Ordinal)
        {
            ["TopHud"] = snapshot.ExpectedLayout.TopHud,
            ["EventRail"] = snapshot.ExpectedLayout.EventRail,
            ["ContextDock"] = snapshot.ExpectedLayout.ContextDock,
            ["BuildShelf"] = snapshot.ExpectedLayout.BuildShelf,
            ["ActionDock"] = snapshot.ExpectedLayout.ActionDock,
        };
        RealtimeUiSmokeSurfaceFact[] visible = snapshot.Surfaces
            .Where(item => item.Visible)
            .ToArray();
        foreach (RealtimeUiSmokeSurfaceFact surface in visible)
        {
            string childMinimums = ChildMinimumBreakdown(snapshot, surface.Id);
            Require(viewport.Encloses(surface.Rect),
                $"{label} places {surface.Id} outside the logical canvas " +
                $"(actual={surface.Rect}, min={surface.CombinedMinimumSize}, " +
                $"expected={expected[surface.Id]}, children={childMinimums})", failures);
            Require(RectApproximatelyEqual(surface.Rect, expected[surface.Id]),
                $"{label} live {surface.Id} rect diverges from layout authority " +
                $"(actual={surface.Rect}, min={surface.CombinedMinimumSize}, " +
                $"expected={expected[surface.Id]}, children={childMinimums})", failures);
            Require(surface.Rect.Size.X + 0.5f >= surface.CombinedMinimumSize.X &&
                    surface.Rect.Size.Y + 0.5f >= surface.CombinedMinimumSize.Y,
                $"{label} allocates {surface.Id} below its live combined minimum " +
                $"(actual={surface.Rect.Size}, min={surface.CombinedMinimumSize})",
                failures);
        }
        for (int left = 0; left < visible.Length; left++)
        {
            for (int right = left + 1; right < visible.Length; right++)
            {
                Require(!visible[left].Rect.Intersects(visible[right].Rect),
                    $"{label} overlaps {visible[left].Id} and {visible[right].Id}",
                    failures);
            }
        }
        Require(snapshot.ExpectedLayout.MapInteraction.Size.X >=
                RealtimeUiMetrics.ReferenceResolution.X / 2f,
            $"{label} reduces world interaction below half the FHD width", failures);
        IReadOnlySet<string> expectedVisible = ExpectedVisibleSurfaces(presentation);
        Require(snapshot.Surfaces
                    .Where(item => item.Visible)
                    .Select(item => item.Id)
                    .ToHashSet(StringComparer.Ordinal)
                    .SetEquals(expectedVisible),
            $"{label} did not enforce the single bottom-command-dock hierarchy",
            failures);

        IReadOnlyDictionary<string, RealtimeUiSmokeSurfaceFact> surfaceById =
            snapshot.Surfaces.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (RealtimeUiSmokeControlFact control in snapshot.Controls)
        {
            if (!control.Visible)
            {
                continue;
            }
            RealtimeUiSmokeSurfaceFact owner = surfaceById[control.OwnerSurfaceId];
            Require(!control.EffectiveVisibleRect.HasArea() ||
                    owner.GlobalRect.Encloses(control.EffectiveVisibleRect),
                $"{label} visibly overflows {control.Path} beyond {owner.Id} " +
                $"(visible={control.EffectiveVisibleRect}, owner={owner.GlobalRect}, " +
                $"raw={control.Rect}, min={control.CombinedMinimumSize}, " +
                $"clipped={control.HasClipAncestor})",
                failures);
        }
        Require(snapshot.ModalPanel.Visible == (presentation.Modal is not null),
            $"{label} modal panel visibility diverges from actual R1 presentation",
            failures);
        if (snapshot.ModalPanel.Visible)
        {
            Require(new Rect2(Vector2.Zero, logical).Encloses(
                        snapshot.ModalPanel.GlobalRect) &&
                    snapshot.ModalPanel.GlobalRect.Size.X + 0.5f >=
                        snapshot.ModalPanel.CombinedMinimumSize.X &&
                    snapshot.ModalPanel.GlobalRect.Size.Y + 0.5f >=
                        snapshot.ModalPanel.CombinedMinimumSize.Y,
                $"{label} modal panel overflows or violates its combined minimum " +
                $"(actual={snapshot.ModalPanel.GlobalRect}, " +
                $"min={snapshot.ModalPanel.CombinedMinimumSize})",
                failures);
        }
    }

    private static string ChildMinimumBreakdown(
        RealtimeUiSmokeLayoutSnapshot snapshot,
        string surfaceId)
    {
        IEnumerable<RealtimeUiSmokeControlFact> controls = snapshot.Controls.Where(item =>
            string.Equals(item.OwnerSurfaceId, surfaceId, StringComparison.Ordinal));
        if (surfaceId == "ContextDock")
        {
            string[] targets =
            [
                "/Header",
                "/SummarySections",
                "/Details",
                "/DetailTabs",
                "/Footer",
                "/DetailScroll",
                "/Column",
                "/Margin",
            ];
            controls = controls.Where(item => targets.Any(suffix =>
                item.Path.EndsWith(suffix, StringComparison.Ordinal)));
        }
        else if (surfaceId == "BuildShelf")
        {
            controls = controls.Where(item =>
                item.Path.Contains("/BuildShelf/Margin", StringComparison.Ordinal));
        }
        else
        {
            controls = controls.Where(item =>
                item.Path.Count(character => character == '/') <= 8);
        }
        return string.Join(",", controls
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .Select(item =>
                $"{item.Path[(item.Path.LastIndexOf('/') + 1)..]}:" +
                $"size={item.Rect.Size}/min={item.CombinedMinimumSize}"));
    }

    private static void ValidateButtons(
        RealtimeUiSmokeLayoutSnapshot snapshot,
        int expectedPrimaryCtaCount,
        string label,
        ICollection<string> failures)
    {
        foreach (RealtimeUiSmokeButtonFact button in snapshot.Buttons)
        {
            Require(button.Size.X + 0.5f >= snapshot.Profile.MinimumHitTarget &&
                    button.Size.Y + 0.5f >= snapshot.Profile.MinimumHitTarget,
                $"{label} undersizes target {button.Path} to {button.Size}", failures);
            if (button.Enabled)
            {
                Require(button.FocusMode != Control.FocusModeEnum.None,
                    $"{label} leaves enabled button {button.Path} outside focus order",
                    failures);
            }
            if (button.Primary)
            {
                Require(button.Size.Y + 0.5f >= snapshot.Profile.PrimaryHitTarget,
                    $"{label} undersizes primary target {button.Path}", failures);
            }
        }
        Require(snapshot.Buttons.Count(item => item.Primary) == expectedPrimaryCtaCount,
            $"{label} expected {expectedPrimaryCtaCount} nonmodal primary CTA(s), " +
            $"found {snapshot.Buttons.Count(item => item.Primary)}", failures);
    }

    private static void ValidateText(
        RealtimeUiSmokeLayoutSnapshot snapshot,
        string label,
        ICollection<string> failures)
    {
        foreach (RealtimeUiSmokeTextFact text in snapshot.Text)
        {
            Require(text.FullyVisible,
                $"{label} clips '{text.Text}' at {text.Path} " +
                $"(required={text.RequiredWidth:0.0}, available={text.AvailableWidth:0.0}, " +
                $"lines={text.VisibleLineCount}/{text.LineCount})",
                failures);
        }
    }

    private static void ValidateScroll(
        RealtimeUiRoot uiRoot,
        RealtimeUiSmokeLayoutSnapshot snapshot,
        RealtimeSlicePresentation presentation,
        string label,
        ICollection<string> failures)
    {
        Require(snapshot.Scrolls.Count == 1,
            $"{label} must contain only the context detail scroll", failures);
        if (snapshot.Scrolls.Count != 1)
        {
            return;
        }
        RealtimeUiSmokeScrollFact scroll = snapshot.Scrolls[0];
        bool contextVisible = snapshot.Surfaces.Single(item =>
            item.Id == "ContextDock").Visible;
        bool hasDetails = presentation.Context.Details.Count > 0;
        bool expectedScrollVisible = contextVisible && hasDetails &&
            !uiRoot.ContextDockForSmoke.CompactOverviewActiveForSmoke;
        Require(scroll.Path.EndsWith("/DetailScroll", StringComparison.Ordinal) &&
                scroll.HorizontalMode == ScrollContainer.ScrollMode.Disabled &&
                scroll.VerticalMode != ScrollContainer.ScrollMode.Disabled &&
                scroll.Visible == expectedScrollVisible,
            $"{label} sole scroll is not vertical ContextDock/DetailScroll", failures);
        Require(!uiRoot.ContextDockForSmoke.CompactOverviewActiveForSmoke ||
                contextVisible && hasDetails && !scroll.Visible,
            $"{label} compact overview did not explicitly own the hidden detail scroll",
            failures);
        Require(!uiRoot.ContextDockForSmoke.DetailTabActiveForSmoke || scroll.Visible,
            $"{label} selected detail tab did not expose the sole vertical scroll",
            failures);
        ScrollContainer actual = uiRoot.ContextDockForSmoke.DetailScroll;
        Node header = uiRoot.ContextDockForSmoke.GetNode("Margin/Column/Header");
        Node summary = uiRoot.ContextDockForSmoke.GetNode("Margin/Column/SummarySections");
        Node footer = uiRoot.ContextDockForSmoke.GetNode("Margin/Column/Footer");
        Require(!actual.IsAncestorOf(header) && !actual.IsAncestorOf(summary) &&
                !actual.IsAncestorOf(footer),
            $"{label} scroll captures fixed identity/summary/action content", failures);
        foreach (RealtimeUiSmokeControlFact control in snapshot.Controls.Where(item =>
                     item.Visible && item.OwnerSurfaceId == "ContextDock" &&
                     item.HasClipAncestor))
        {
            Require(control.ClipAncestorPaths.All(path =>
                    path.Contains("/DetailScroll", StringComparison.Ordinal)),
                $"{label} clips fixed context content outside DetailScroll at {control.Path}",
                failures);
        }
    }

    private static void ValidateTimeline(
        RealtimeUiSmokeLayoutSnapshot snapshot,
        IReadOnlyList<RealtimeTimelineItemPresentation> expectedLinearItems,
        int scale,
        bool presentationExpanded,
        string label,
        ICollection<string> failures)
    {
        string[] expectedLinearIds = expectedLinearItems.Select(item => item.Id).ToArray();
        int expectedLanes = presentationExpanded || scale < 125 ? 4 : 2;
        Require(snapshot.VisibleTimelineLanes == expectedLanes &&
                snapshot.TimelineLaneLabels == expectedLanes,
            $"{label} timeline rendered mismatched track/label lane counts", failures);
        Require(snapshot.LinearTimelineItemIds.SequenceEqual(expectedLinearIds),
            $"{label} timeline linear view lost Core chronological order", failures);
        Rect2 rail = snapshot.Surfaces.Single(item => item.Id == "EventRail").GlobalRect;
        Require(snapshot.AccessibleTimelineItems.Count == expectedLinearIds.Length &&
                snapshot.AccessibleTimelineItems.Select(item => item.ItemId)
                    .SequenceEqual(expectedLinearIds, StringComparer.Ordinal) &&
                snapshot.AccessibleTimelineItems.Select(item => item.ItemId)
                    .Distinct(StringComparer.Ordinal).Count() == expectedLinearIds.Length &&
                snapshot.AccessibleTimelineItems.Select(item => item.OptionIndex)
                    .SequenceEqual(Enumerable.Range(1, expectedLinearIds.Length)) &&
                snapshot.AccessibleTimelineItems.Select((item, index) =>
                    !item.Disabled &&
                    string.Equals(
                        item.Text,
                        AccessibleSelectorTextForSmoke(expectedLinearItems[index]),
                        StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(item.Tooltip)).All(item => item),
            $"{label} real accessible selector did not expose exactly one enabled " +
            "full-text option per chronological event",
            failures);
        RealtimeUiSmokeAccessibleTimelinePopupFact popup =
            snapshot.AccessibleTimelinePopup;
        int expectedPopupFontSize = Math.Max(
            14,
            Mathf.RoundToInt(14f * snapshot.Profile.AccessibilityScale));
        int expectedPopupPadding = Math.Max(
            12,
            Mathf.RoundToInt(12f * snapshot.Profile.AccessibilityScale));
        Require(popup.ItemCount == expectedLinearIds.Length + 1 &&
                popup.EnabledItemCount == expectedLinearIds.Length &&
                popup.FontSize == expectedPopupFontSize &&
                popup.FontHeight > 0f &&
                popup.VerticalSeparation >= 0 &&
                popup.EffectiveRowHeight + 0.5f >=
                    snapshot.Profile.MinimumHitTarget &&
                popup.StartPadding >= expectedPopupPadding &&
                popup.EndPadding >= expectedPopupPadding,
            $"{label} accessible selector popup lost scaled font/row hit/padding " +
            $"semantics (items={popup.ItemCount}, enabled={popup.EnabledItemCount}, " +
            $"font={popup.FontSize}/{popup.FontHeight:0.##}, " +
            $"row={popup.EffectiveRowHeight:0.##}, " +
            $"target={snapshot.Profile.MinimumHitTarget:0.##}, " +
            $"padding={popup.StartPadding}/{popup.EndPadding})",
            failures);
        Require(snapshot.TimelineNavigation.Select(item => item.Navigation).SequenceEqual(
                    new[]
                    {
                        RealtimeTimelineNavigation.PreviousEvent,
                        RealtimeTimelineNavigation.Home,
                        RealtimeTimelineNavigation.NextEvent,
                    }) &&
                snapshot.TimelineNavigation.All(item =>
                    item.Enabled && rail.Encloses(item.Rect) &&
                    item.Rect.Size.X + 0.5f >= snapshot.Profile.MinimumHitTarget &&
                    item.Rect.Size.Y + 0.5f >= snapshot.Profile.MinimumHitTarget &&
                    !string.IsNullOrWhiteSpace(item.Text) &&
                    !string.IsNullOrWhiteSpace(item.AccessibilityName) &&
                    !string.IsNullOrWhiteSpace(item.AccessibilityDescription)),
            $"{label} previous/current/next mouse controls lost hit or AX semantics",
            failures);
        Require(snapshot.Markers.Count > 0,
            $"{label} produced no actual R1 forecast markers", failures);
        string[] renderedIds = snapshot.Markers
            .SelectMany(marker => marker.ItemIds)
            .ToArray();
        Require(renderedIds.Length == expectedLinearIds.Length &&
                renderedIds.Distinct(StringComparer.Ordinal).Count() == renderedIds.Length &&
                renderedIds.ToHashSet(StringComparer.Ordinal).SetEquals(expectedLinearIds),
            $"{label} did not render every visible Core item exactly once " +
            $"(expected=[{string.Join(",", expectedLinearIds)}], " +
            $"rendered=[{string.Join(",", renderedIds)}])",
            failures);
        IReadOnlyDictionary<string, int> coreIndex = expectedLinearIds
            .Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);
        foreach (RealtimeUiSmokeMarkerFact marker in snapshot.Markers)
        {
            int expectedFontSize = Math.Max(1, Mathf.RoundToInt(14f * scale / 100f));
            int expectedFocusBorder = Math.Max(3, Mathf.RoundToInt(3f * scale / 100f));
            float expectedFocusExpand = Math.Max(2f, 2f * scale / 100f);
            Require(marker.FontSize == expectedFontSize &&
                    marker.FocusBorderWidth == expectedFocusBorder &&
                    Mathf.IsEqualApprox(marker.FocusExpandMargin, expectedFocusExpand),
                $"{label} marker {marker.SemanticItemId} lost scaled font/focus tokens " +
                $"(font={marker.FontSize}/{expectedFontSize}, border=" +
                $"{marker.FocusBorderWidth}/{expectedFocusBorder}, expand=" +
                $"{marker.FocusExpandMargin}/{expectedFocusExpand})",
                failures);
            Require(marker.ItemIds.Count > 0 && marker.ItemIds.All(coreIndex.ContainsKey) &&
                    marker.ItemIds.SequenceEqual(marker.ItemIds
                        .OrderBy(id => coreIndex.TryGetValue(id, out int index)
                            ? index
                            : int.MaxValue), StringComparer.Ordinal),
                $"{label} marker cluster lost Core-relative item order " +
                $"([{string.Join(",", marker.ItemIds)}])", failures);
            RealtimeTimelineItemPresentation[] markerItems = marker.ItemIds
                .Select(id => expectedLinearItems.Single(item => string.Equals(
                    item.Id,
                    id,
                    StringComparison.Ordinal)))
                .ToArray();
            bool hasExactLeadSemantics = markerItems.Any(item =>
                marker.VisibleText.Contains(SeverityGlyphForSmoke(item.Severity),
                    StringComparison.Ordinal) &&
                marker.VisibleText.Contains(item.KindIcon, StringComparison.Ordinal) &&
                marker.VisibleText.Contains(item.TimeLabel, StringComparison.Ordinal) &&
                marker.VisibleText.Contains(item.ShortLabel, StringComparison.Ordinal));
            Require(hasExactLeadSemantics &&
                    (markerItems.Length == 1 || marker.VisibleText.Contains(
                        $"+{markerItems.Length - 1}", StringComparison.Ordinal)),
                $"{label} marker visible text lost severity/kind/time/short-label " +
                $"semantics ({marker.VisibleText})",
                failures);
        }
        for (int leftIndex = 0; leftIndex < snapshot.Markers.Count; leftIndex++)
        for (int rightIndex = leftIndex + 1;
             rightIndex < snapshot.Markers.Count;
             rightIndex++)
        {
            RealtimeUiSmokeMarkerFact left = snapshot.Markers[leftIndex];
            RealtimeUiSmokeMarkerFact right = snapshot.Markers[rightIndex];
            if (left.DisplayLane != right.DisplayLane)
            {
                continue;
            }
            float horizontalOverlap = Math.Min(left.Rect.End.X, right.Rect.End.X) -
                Math.Max(left.Rect.Position.X, right.Rect.Position.X);
            Require(horizontalOverlap <= 0.5f,
                $"{label} same-lane marker rectangles intersect by " +
                $"{horizontalOverlap:0.00}px ([{string.Join(",", left.ItemIds)}] / " +
                $"[{string.Join(",", right.ItemIds)}])",
                failures);
        }
        RealtimeUiSmokeMarkerFact[] chronologicalGroups = snapshot.Markers
            .OrderBy(marker => marker.ItemIds
                .Select(id => coreIndex.TryGetValue(id, out int index)
                    ? index
                    : int.MaxValue)
                .DefaultIfEmpty(int.MaxValue)
                .Min())
            .ThenBy(marker => marker.ItemIds.FirstOrDefault() ?? string.Empty,
                StringComparer.Ordinal)
            .ToArray();
        Require(snapshot.Markers.Select(marker => marker.ItemIds.FirstOrDefault())
                .SequenceEqual(
                    chronologicalGroups.Select(marker => marker.ItemIds.FirstOrDefault()),
                    StringComparer.Ordinal),
            $"{label} marker focus order does not follow chronological cluster minima",
            failures);
        for (int index = 0; index < chronologicalGroups.Length; index++)
        {
            RealtimeUiSmokeMarkerFact marker = chronologicalGroups[index];
            Require(rail.Encloses(marker.Rect),
                $"{label} timeline marker left the clipped rail", failures);
            Require(marker.DisplayLane >= 0 && marker.DisplayLane < expectedLanes,
                $"{label} marker mapped outside visible lanes", failures);
            Require(marker.LeftNeighborItemIds.Count > 0 &&
                    marker.RightNeighborItemIds.Count > 0,
                $"{label} marker is not reachable in chronological arrow order", failures);
            RealtimeUiSmokeMarkerFact previous = chronologicalGroups[
                (index - 1 + chronologicalGroups.Length) % chronologicalGroups.Length];
            RealtimeUiSmokeMarkerFact next = chronologicalGroups[
                (index + 1) % chronologicalGroups.Length];
            Require(marker.LeftNeighborItemIds.SequenceEqual(previous.ItemIds) &&
                    marker.RightNeighborItemIds.SequenceEqual(next.ItemIds),
                $"{label} marker arrow neighbors do not follow exact chronological groups",
                failures);
            foreach (string itemId in marker.ItemIds)
            {
                RealtimeTimelineItemPresentation item = expectedLinearItems.Single(candidate =>
                    string.Equals(candidate.Id, itemId, StringComparison.Ordinal));
                Require(marker.AccessibilityName.Contains(item.TimeLabel,
                            StringComparison.Ordinal) &&
                        marker.AccessibilityName.Contains(item.KindLabel,
                            StringComparison.Ordinal) &&
                        marker.AccessibilityName.Contains(item.SeverityLabel,
                            StringComparison.Ordinal) &&
                        marker.AccessibilityDescription.Contains("운영 예측",
                            StringComparison.Ordinal),
                    $"{label} marker {itemId} lost time/kind/severity/player AX semantics",
                    failures);
            }
        }
    }

    private static string SeverityGlyphForSmoke(
        RealtimeTimelineSeverity severity) => severity switch
    {
        RealtimeTimelineSeverity.Information => "●",
        RealtimeTimelineSeverity.Advisory => "◆",
        RealtimeTimelineSeverity.Warning => "▲",
        RealtimeTimelineSeverity.Critical => "■",
        _ => throw new ArgumentOutOfRangeException(nameof(severity)),
    };

    private static string AccessibleSelectorTextForSmoke(
        RealtimeTimelineItemPresentation item) =>
        $"{SeverityGlyphForSmoke(item.Severity)} {item.TimeLabel} · " +
        $"{item.KindLabel} · {item.ShortLabel}";

    private async Task ValidateSelectedTimelineTogglePersistence(
        RealtimeSlicePresentation baseline,
        ICollection<string> failures)
    {
        RealtimeTimelineItemPresentation? seed = baseline.Rail.Items
            .Where(item => item.Visibility != RealtimeTimelineVisibility.Hidden)
            .Where(item => item.StartMinute <= baseline.Rail.HorizonEndMinute &&
                           (item.EndMinute ?? item.StartMinute) >=
                               baseline.Rail.HorizonStartMinute)
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (seed is null)
        {
            failures.Add("selected timeline-toggle fixture has no visible seed item");
            return;
        }

        RealtimeTimelineItemPresentation clusterSibling = seed with
        {
            Id = $"{seed.Id}:SMOKE_CLUSTER",
            Title = $"{seed.Title} 비교",
            ShortLabel = $"{seed.ShortLabel} 비교",
        };
        (string Label, RealtimeTimelineItemPresentation[] Items)[] cases =
        [
            ("singleton", new[] { seed }),
            ("cluster", new[] { seed, clusterSibling }),
        ];
        foreach ((string label, RealtimeTimelineItemPresentation[] items) in cases)
        {
            RealtimeSlicePresentation selectedTimeline = baseline with
            {
                Rail = baseline.Rail with
                {
                    Items = items,
                    SelectedItemId = seed.Id,
                },
                Modal = null,
            };
            (SubViewport viewport, RealtimeUiRoot root) = await CreateOffscreenUi(
                RealtimeUiMetrics.ReferenceResolution,
                RealtimeUiMetrics.ReferenceResolution,
                100,
                selectedTimeline);
            var requests = new List<IReadOnlyList<string>>();
            void ObserveItems(IReadOnlyList<string> itemIds) =>
                requests.Add(Array.AsReadOnly(itemIds.ToArray()));
            root.TimelineItemsRequested += ObserveItems;
            try
            {
                RealtimeEventRail rail = root.EventRailForSmoke;
                RealtimeUiSmokeMarkerFact before = rail.MarkerFactsForSmoke().Single();
                Require(before.ItemIds.Count == items.Length && before.Selected,
                    $"selected {label} marker fixture did not start authoritatively pressed",
                    failures);
                PushViewportPrimary(
                    viewport,
                    rail.MarkerCenterForSmoke(seed.Id));
                await SettleLayout();
                RealtimeUiSmokeMarkerFact after = rail.MarkerFactsForSmoke().Single();
                Require(after.Selected &&
                        requests.Count == 1 &&
                        requests[0].SequenceEqual(
                            before.ItemIds,
                            StringComparer.Ordinal),
                    $"same-selected {label} marker re-click visually unpressed or " +
                    "changed its stable item request",
                    failures);
            }
            finally
            {
                root.TimelineItemsRequested -= ObserveItems;
                RemoveAndFree(viewport);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
    }

    private async Task ValidateEmptyTimelineMouseNavigation(
        RealtimeSlicePresentation baseline,
        ICollection<string> failures)
    {
        RealtimeSlicePresentation emptyTimeline = baseline with
        {
            Rail = baseline.Rail with
            {
                Items = Array.Empty<RealtimeTimelineItemPresentation>(),
                SelectedItemId = null,
            },
            Modal = null,
        };
        (SubViewport viewport, RealtimeUiRoot root) = await CreateOffscreenUi(
            RealtimeUiMetrics.ReferenceResolution,
            RealtimeUiMetrics.ReferenceResolution,
            100,
            emptyTimeline);
        var requests = new List<RealtimeTimelineNavigation>();
        void ObserveNavigation(RealtimeTimelineNavigation navigation) =>
            requests.Add(navigation);
        root.TimelineNavigationRequested += ObserveNavigation;
        try
        {
            RealtimeEventRail rail = root.EventRailForSmoke;
            Require(rail.LinearItemIds.Count == 0,
                "empty-horizon mouse fixture unexpectedly rendered an event",
                failures);
            PushViewportPrimary(viewport, rail.PreviousEventCenterForSmoke);
            await SettleLayout();
            PushViewportPrimary(viewport, rail.CurrentTimeCenterForSmoke);
            await SettleLayout();
            PushViewportPrimary(viewport, rail.NextEventCenterForSmoke);
            await SettleLayout();
            Require(requests.SequenceEqual(
                        new[] { RealtimeTimelineNavigation.Home }),
                    "empty visible horizon did not route exactly one mouse Current/Home " +
                    "request while retaining item-relative Previous/Next guards",
                failures);
        }
        finally
        {
            root.TimelineNavigationRequested -= ObserveNavigation;
            RemoveAndFree(viewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task ValidateTimelineFocusRestore(
        RealtimeSlicePresentation restorePresentation,
        ICollection<string> failures)
    {
        var slice = new RealtimeSliceMain();
        using var sliceLifetime = slice.FreeAfterSmoke();
        slice.BootstrapForSmoke();
        string modalId = slice.InteractionState.ActiveModalId ?? string.Empty;
        Require(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.CloseModal(modalId)).Accepted,
            "timeline fixture could not close the actual chapter modal", failures);
        Present(slice.LatestPresentation);
        _uiRoot.ApplyLayoutForSmoke(
            RealtimeUiMetrics.ReferenceResolution,
            RealtimeUiMetrics.ReferenceResolution,
            uiScalePercent: 100);
        await SettleLayout();
        RealtimeEventRail rail = _uiRoot.EventRailForSmoke;
        RealtimeUiSmokeLayoutSnapshot before = _uiRoot.CaptureLayoutForSmoke(
            RealtimeUiMetrics.ReferenceResolution);
        if (before.Markers.Count == 0)
        {
            failures.Add("Timeline focus restore has no marker to focus.");
            return;
        }
        string initialHash = string.Empty;
        int initialCommands = 0;
        slice.AttachTimelineUiForSmoke(_uiRoot);
        void PresentSynchronousItems(IReadOnlyList<string> _) =>
            Present(slice.LatestPresentation);
        rail.ItemsRequested += PresentSynchronousItems;
        void PresentSynchronousNavigation(RealtimeTimelineNavigation _) =>
            Present(slice.LatestPresentation);
        rail.NavigationRequested += PresentSynchronousNavigation;
        try
        {
            Require(before.Markers.Count >= 2,
                "timeline passive-focus fixture needs two distinct rendered groups",
                failures);
            if (before.Markers.Count >= 2)
            {
                string focusedA = before.Markers[0].ItemIds[0];
                string selectedB = before.Markers[1].ItemIds[0];
                slice.ChooseTimelineClusterForSmoke(new[] { selectedB });
                Present(slice.LatestPresentation);
                await SettleLayout();
                rail.GrabMarkerFocusOnlyForSmoke(focusedA);
                await SettleLayout();
                RealtimeUiSmokeMarkerFact beforeTickFact = rail.MarkerFactsForSmoke()
                    .Single(marker => marker.ItemIds.Contains(
                        focusedA,
                        StringComparer.Ordinal));
                Require(string.Equals(
                            rail.FocusedItemIdForSmoke,
                            focusedA,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            slice.TimelineChooserFacts.SelectedMarkerId,
                            selectedB,
                            StringComparison.Ordinal),
                    "timeline could not establish distinct A-focused/B-selected state",
                    failures);

                slice.AdvanceToForSmoke(slice.CurrentMinute + 1);
                Present(slice.LatestPresentation);
                await SettleLayout();
                RealtimeUiSmokeMarkerFact afterTickFact = rail.MarkerFactsForSmoke()
                    .Single(marker => marker.ItemIds.Contains(
                        focusedA,
                        StringComparer.Ordinal));
                Require(string.Equals(
                            rail.FocusedItemIdForSmoke,
                            focusedA,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            slice.TimelineChooserFacts.SelectedMarkerId,
                            selectedB,
                            StringComparison.Ordinal) &&
                        afterTickFact.ButtonInstanceId ==
                            beforeTickFact.ButtonInstanceId &&
                        afterTickFact.FontSize == beforeTickFact.FontSize &&
                        afterTickFact.FontSize == 14 &&
                        afterTickFact.FocusBorderWidth ==
                            beforeTickFact.FocusBorderWidth &&
                        afterTickFact.FocusBorderWidth == 3 &&
                        Mathf.IsEqualApprox(
                            afterTickFact.FocusExpandMargin,
                            beforeTickFact.FocusExpandMargin) &&
                        Mathf.IsEqualApprox(afterTickFact.FocusExpandMargin, 2f),
                    "passive minute refresh moved A focus/B selection, replaced the " +
                    "marker Button object, or drifted its 100% font/focus tokens",
                    failures);

                Require(rail.Navigate(RealtimeTimelineNavigation.Home),
                    "explicit focus-follow fixture could not navigate Home", failures);
                Present(slice.LatestPresentation);
                await SettleLayout();
                Require(rail.Navigate(RealtimeTimelineNavigation.NextEvent),
                    "explicit focus-follow fixture could not navigate to next event",
                    failures);
                string explicitlySelected =
                    slice.TimelineChooserFacts.SelectedMarkerId ?? string.Empty;
                Present(slice.LatestPresentation);
                await SettleLayout();
                Require(!string.IsNullOrWhiteSpace(explicitlySelected) &&
                        string.Equals(
                            rail.FocusedItemIdForSmoke,
                            explicitlySelected,
                            StringComparison.Ordinal),
                    "explicit timeline Navigate did not move focus to selected stable ID",
                    failures);
            }

            initialHash = slice.CanonicalStateSha256;
            initialCommands = slice.AcceptedCommandCount;
            string[] mouseOrderedIds = slice.TimelineChooserFacts.VisibleOrderedItemIds
                .ToArray();
            Require(mouseOrderedIds.Length > 1,
                "timeline mouse-navigation fixture needs at least two events",
                failures);

            long mouseHomeRevision = slice.PresentationRevision;
            rail.PressCurrentTimeForSmoke();
            await SettleLayout();
            Require(slice.PresentationRevision == mouseHomeRevision + 1 &&
                    slice.TimelineChooserFacts.SelectedMarkerId is null &&
                    slice.TimelineChooserFacts.SelectedSubjectId is null &&
                    slice.LatestPresentation.Rail.SelectedItemId is null,
                "mouse 현재 control did not apply exact Home/current-time semantics",
                failures);

            if (mouseOrderedIds.Length > 0)
            {
                long firstRevision = slice.PresentationRevision;
                rail.PressNextEventForSmoke();
                await SettleLayout();
                Require(slice.PresentationRevision == firstRevision + 1,
                    "mouse 다음 control did not reduce exactly once", failures);
                ValidateTimelineSelection(slice, mouseOrderedIds[0], failures);
            }
            if (mouseOrderedIds.Length > 1)
            {
                long secondRevision = slice.PresentationRevision;
                rail.PressNextEventForSmoke();
                await SettleLayout();
                Require(slice.PresentationRevision == secondRevision + 1,
                    "second mouse 다음 control did not reduce exactly once", failures);
                ValidateTimelineSelection(slice, mouseOrderedIds[1], failures);

                long previousRevision = slice.PresentationRevision;
                rail.PressPreviousEventForSmoke();
                await SettleLayout();
                Require(slice.PresentationRevision == previousRevision + 1,
                    "mouse 이전 control did not reduce exactly once", failures);
                ValidateTimelineSelection(slice, mouseOrderedIds[0], failures);
            }

            IReadOnlyList<RealtimeUiSmokeAccessibleTimelineItemFact>
                accessibleTimelineItems = rail.AccessibleTimelineItemsForSmoke;
            IReadOnlyDictionary<string, RealtimeTimelineItemPresentation>
                accessibleExpectedById = slice.LatestPresentation.Rail.Items
                    .Where(item => mouseOrderedIds.Contains(
                        item.Id,
                        StringComparer.Ordinal))
                    .ToDictionary(item => item.Id, StringComparer.Ordinal);
            Require(accessibleTimelineItems.Count == mouseOrderedIds.Length &&
                    accessibleTimelineItems.Select(item => item.ItemId).SequenceEqual(
                        mouseOrderedIds,
                        StringComparer.Ordinal) &&
                    accessibleTimelineItems.Select(item => item.OptionIndex).SequenceEqual(
                        Enumerable.Range(1, mouseOrderedIds.Length)) &&
                    accessibleTimelineItems.All(item =>
                        !item.Disabled &&
                        accessibleExpectedById.ContainsKey(item.ItemId) &&
                        string.Equals(
                            item.Text,
                            AccessibleSelectorTextForSmoke(
                                accessibleExpectedById[item.ItemId]),
                            StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(item.Tooltip)),
                "real accessible timeline selector did not expose exactly one enabled " +
                "full-text option per chronological event",
                failures);
            foreach (string eventId in mouseOrderedIds)
            {
                long accessibleHomeRevision = slice.PresentationRevision;
                rail.PressCurrentTimeForSmoke();
                await SettleLayout();
                Require(slice.PresentationRevision == accessibleHomeRevision + 1 &&
                        slice.TimelineChooserFacts.SelectedMarkerId is null,
                    $"accessible selector setup could not reset Home before {eventId}",
                    failures);
                long accessibleRevision = slice.PresentationRevision;
                rail.SelectAccessibleTimelineItemForSmoke(eventId);
                await SettleLayout();
                Require(slice.PresentationRevision == accessibleRevision + 1,
                    $"accessible selector activation for {eventId} did not reduce once",
                    failures);
                ValidateTimelineSelection(slice, eventId, failures);
                RealtimeTimelineItemPresentation selectedItem =
                    accessibleExpectedById[eventId];
                int selectedIndex = Array.IndexOf(mouseOrderedIds, eventId);
                RealtimeUiSmokeAccessibleTimelineClosedFact closed =
                    rail.AccessibleTimelineClosedForSmoke;
                string expectedClosed =
                    $"{SeverityGlyphForSmoke(selectedItem.Severity)} " +
                    $"{selectedIndex + 1}/{mouseOrderedIds.Length} · " +
                    selectedItem.TimeLabel;
                Require(string.Equals(
                            closed.Text,
                            expectedClosed,
                            StringComparison.Ordinal) &&
                        closed.RequiredWidth <= closed.AvailableWidth + 1f &&
                        closed.RequiredHeight <= closed.AvailableHeight + 1f &&
                        rail.GetGlobalRect().Encloses(closed.Rect) &&
                        closed.AccessibilityName.Contains(
                            selectedItem.TimeLabel,
                            StringComparison.Ordinal) &&
                        closed.AccessibilityName.Contains(
                            selectedItem.KindLabel,
                            StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(closed.AccessibilityDescription),
                    $"accessible selector closed label for {eventId} was not the " +
                    $"exact compact unclipped label (text='{closed.Text}', " +
                    $"required={closed.RequiredWidth:0.0}x" +
                    $"{closed.RequiredHeight:0.0}, available=" +
                    $"{closed.AvailableWidth:0.0}x{closed.AvailableHeight:0.0})",
                    failures);
            }

            long resetRevision = slice.PresentationRevision;
            rail.PressCurrentTimeForSmoke();
            await SettleLayout();
            Require(slice.PresentationRevision == resetRevision + 1 &&
                    slice.TimelineChooserFacts.SelectedMarkerId is null &&
                    slice.TimelineChooserFacts.SelectedSubjectId is null,
                "mouse 현재 control did not restore Home after selector activation",
                failures);

            RealtimeUiSmokeMarkerFact[] currentMarkerFacts = rail.MarkerFactsForSmoke()
                .ToArray();
            RealtimeUiSmokeMarkerFact clickGroup = currentMarkerFacts
                .OrderByDescending(marker => marker.ItemIds.Count)
                .ThenBy(marker => marker.ItemIds[0], StringComparer.Ordinal)
                .First();
            Require(clickGroup.ItemIds.Count > 1,
                "timeline semantic-focus fixture did not render an overlapping cluster",
                failures);
            RealtimeUiSmokeMarkerFact? resetGroup = currentMarkerFacts.FirstOrDefault(marker =>
                !marker.ItemIds.SequenceEqual(clickGroup.ItemIds, StringComparer.Ordinal));
            if (resetGroup is not null)
            {
                slice.ChooseTimelineClusterForSmoke(resetGroup.ItemIds);
                Present(slice.LatestPresentation);
                await SettleLayout();
            }
            for (int index = 0; index < clickGroup.ItemIds.Count; index++)
            {
                long beforeRevision = slice.PresentationRevision;
                rail.FocusMarkerForSmoke(clickGroup.ItemIds[0]);
                string expectedId = clickGroup.ItemIds[index];
                Require(slice.PresentationRevision == beforeRevision + 1,
                    $"timeline cluster choice {index} did not reduce exactly once",
                    failures);
                ValidateTimelineSelection(slice, expectedId, failures);
                Present(slice.LatestPresentation);
                await SettleLayout();
                Require(string.Equals(
                            rail.FocusedItemIdForSmoke,
                            expectedId,
                            StringComparison.Ordinal),
                    "timeline cluster refresh lost the exact selected semantic item focus " +
                    $"(expected={expectedId}, focused=" +
                    $"{rail.FocusedItemIdForSmoke ?? "<none>"}, selected=" +
                    $"{slice.TimelineChooserFacts.SelectedMarkerId ?? "<none>"}, " +
                    $"cluster=[{string.Join(",", slice.TimelineChooserFacts.ClusterItemIds)}], " +
                    $"index={slice.TimelineChooserFacts.ClusterIndex}, " +
                    $"focusedGroup=[{string.Join(",", rail.FocusedMarkerItemIdsForSmoke)}])",
                    failures);
            }
            if (clickGroup.ItemIds.Count > 1)
            {
                long beforeRevision = slice.PresentationRevision;
                rail.FocusMarkerForSmoke(clickGroup.ItemIds[0]);
                Require(slice.PresentationRevision == beforeRevision + 1 &&
                        string.Equals(
                            slice.TimelineChooserFacts.SelectedMarkerId,
                            clickGroup.ItemIds[0],
                            StringComparison.Ordinal),
                    "repeated cluster choices did not cycle back to its first Core item",
                    failures);
                Present(slice.LatestPresentation);
                await SettleLayout();
            }

            long homeRevision = slice.PresentationRevision;
            Require(rail.Navigate(RealtimeTimelineNavigation.Home),
                "timeline Home was not routed from the live rail", failures);
            Require(slice.PresentationRevision == homeRevision + 1 &&
                    slice.TimelineChooserFacts.SelectedMarkerId is null &&
                    slice.TimelineChooserFacts.SelectedSubjectId is null &&
                    slice.LatestPresentation.Rail.SelectedItemId is null,
                "timeline Home did not restore the authoritative current-time anchor",
                failures);
            Present(slice.LatestPresentation);
            await SettleLayout();

            string[] orderedIds = slice.TimelineChooserFacts.VisibleOrderedItemIds.ToArray();
            for (int index = 0; index < orderedIds.Length; index++)
            {
                long beforeRevision = slice.PresentationRevision;
                Require(rail.Navigate(RealtimeTimelineNavigation.NextEvent),
                    $"timeline next request {index} was rejected by the live rail",
                    failures);
                Require(slice.PresentationRevision == beforeRevision + 1,
                    $"timeline next request {index} did not reduce exactly once",
                    failures);
                ValidateTimelineSelection(slice, orderedIds[index], failures);
                Present(slice.LatestPresentation);
                await SettleLayout();
                RealtimeUiSmokeLayoutSnapshot selectedLayout =
                    _uiRoot.CaptureLayoutForSmoke(
                        RealtimeUiMetrics.ReferenceResolution);
                RealtimeUiSmokeMarkerFact selectedGroup = selectedLayout.Markers.Single(
                    marker => marker.ItemIds.Contains(
                        orderedIds[index], StringComparer.Ordinal));
                Require(selectedGroup.ItemIds.Contains(
                            rail.FocusedItemIdForSmoke ?? string.Empty,
                            StringComparer.Ordinal),
                    $"timeline focus did not remain on the group for {orderedIds[index]}",
                    failures);
            }
            if (orderedIds.Length > 1)
            {
                long beforeRevision = slice.PresentationRevision;
                Require(rail.Navigate(RealtimeTimelineNavigation.PreviousEvent),
                    "timeline previous request was rejected by the live rail", failures);
                Require(slice.PresentationRevision == beforeRevision + 1,
                    "timeline previous request did not reduce exactly once", failures);
                ValidateTimelineSelection(slice, orderedIds[^2], failures);
                Present(slice.LatestPresentation);
                await SettleLayout();
            }

            RealtimeTimelineHorizonPreset initialHorizon =
                slice.LatestPresentation.Rail.HorizonPreset;
            long shorterRevision = slice.PresentationRevision;
            rail.PressShorterHorizonForSmoke();
            Require(slice.PresentationRevision == shorterRevision + 1 &&
                    slice.LatestPresentation.Rail.HorizonPreset < initialHorizon,
                "live shorter-horizon CTA did not reduce one typed controller request",
                failures);
            Present(slice.LatestPresentation);
            await SettleLayout();
            long longerRevision = slice.PresentationRevision;
            rail.PressLongerHorizonForSmoke();
            Require(slice.PresentationRevision == longerRevision + 1 &&
                    slice.LatestPresentation.Rail.HorizonPreset == initialHorizon,
                "live longer-horizon CTA did not restore the authored horizon preset",
                failures);
            Present(slice.LatestPresentation);
            await SettleLayout();
            long sevenDayRevision = slice.PresentationRevision;
            rail.PressLongerHorizonForSmoke();
            long requiredSevenDayHorizon =
                RealtimeSlicePresenter.RequiredForecastHorizonMinutes(
                    slice.CurrentMinute,
                    slice.InteractionState.TimelineAnchorMinute,
                    RealtimeTimelineHorizonPreset.SevenDays);
            RealtimeForecastSnapshot sevenDayCore =
                slice.ForecastForHorizonForSmoke(requiredSevenDayHorizon);
            RealtimeSlicePresentation sevenDayPresentation =
                slice.LatestPresentation;
            string[] expectedSevenDayForecastFacts = sevenDayCore.Events
                .Select(item =>
                    $"{item.EventId}|{item.StartMinute}|{item.EndMinute}|{item.Status}|" +
                    string.Join(",", item.TemporalProjection.Transitions.Select(
                        transition =>
                            $"{transition.Minute}:{transition.Kind}:" +
                            $"{transition.AssetKind}:{transition.AssetId}")))
                .ToArray();
            string[] presentedSevenDayForecastFacts =
                sevenDayPresentation.BaseForecast.Events
                    .Select(item =>
                        $"{item.EventId}|{item.StartMinute}|{item.EndMinute}|{item.Status}|" +
                        string.Join(",", item.TemporalProjection.Transitions.Select(
                            transition =>
                                $"{transition.Minute}:{transition.Kind}:" +
                                $"{transition.AssetKind}:{transition.AssetId}")))
                    .ToArray();
            Require(slice.PresentationRevision == sevenDayRevision + 1 &&
                    sevenDayPresentation.Rail.HorizonPreset ==
                        RealtimeTimelineHorizonPreset.SevenDays &&
                    requiredSevenDayHorizon == 7 * 24 * 60 &&
                    sevenDayPresentation.Rail.HorizonEndMinute ==
                        slice.CurrentMinute + requiredSevenDayHorizon &&
                    string.Equals(
                        sevenDayPresentation.Rail.HorizonLabel,
                        "앞으로 7일 · 지난 6시간",
                        StringComparison.Ordinal) &&
                    sevenDayPresentation.BaseForecast.NowMinute ==
                        sevenDayCore.NowMinute &&
                    presentedSevenDayForecastFacts.SequenceEqual(
                        expectedSevenDayForecastFacts,
                        StringComparer.Ordinal),
                "live longer-horizon CTA did not bind the exact 10,080-minute Core " +
                "forecast authority to the 7-day presentation",
                failures);
            Present(sevenDayPresentation);
            await SettleLayout();
            Require(sevenDayCore.Events.All(item =>
                    rail.LinearItemIds.Count(id => string.Equals(
                        id,
                        item.EventId,
                        StringComparison.Ordinal)) == 1 &&
                    rail.AccessibleTimelineItemsForSmoke.Count(option => string.Equals(
                        option.ItemId,
                        item.EventId,
                        StringComparison.Ordinal)) == 1),
                "live 7-day rail did not render each authoritative Core forecast event " +
                "exactly once in visual and accessible order",
                failures);
        }
        finally
        {
            rail.ItemsRequested -= PresentSynchronousItems;
            rail.NavigationRequested -= PresentSynchronousNavigation;
            slice.DetachTimelineUiForSmoke(_uiRoot);
        }
        Require(string.Equals(initialHash, slice.CanonicalStateSha256,
                    StringComparison.Ordinal) &&
                slice.AcceptedCommandCount == initialCommands,
            "timeline navigation changed authoritative Core state or command journal",
            failures);
        Present(restorePresentation);
        await SettleLayout();
    }

    private static void ValidateTimelineSelection(
        RealtimeSliceMain slice,
        string expectedMarkerId,
        ICollection<string> failures)
    {
        RealtimeSlicePresentation presentation = slice.LatestPresentation;
        RealtimeTimelineTarget target = RealtimeSlicePresenter.ResolveTimelineTarget(
            slice.DisplayWorldForSmoke,
            slice.CoreSnapshot,
            presentation.BaseForecast,
            presentation.ComparisonDraftForecast,
            expectedMarkerId);
        RealtimeR2TimelineChooserFacts facts = slice.TimelineChooserFacts;
        Require(string.Equals(facts.SelectedMarkerId, expectedMarkerId,
                    StringComparison.Ordinal) &&
                string.Equals(facts.SelectedSubjectId, target.SubjectId,
                    StringComparison.Ordinal) &&
                string.Equals(presentation.Rail.SelectedItemId, expectedMarkerId,
                    StringComparison.Ordinal) &&
                string.Equals(presentation.Interaction.SelectionId, target.SubjectId,
                    StringComparison.Ordinal) &&
                string.Equals(presentation.World.SelectedAssetId, target.MapSubjectId,
                    StringComparison.Ordinal) &&
                (target.SubjectId is null ||
                 presentation.Context.Visible &&
                 string.Equals(presentation.Context.SubjectId, target.SubjectId,
                     StringComparison.Ordinal)),
            $"timeline marker {expectedMarkerId} did not project its typed " +
            $"{target.Kind} target {target.SubjectId}/{target.MapSubjectId} " +
            "consistently to rail/context/world",
            failures);
        if (target.Kind == RealtimeTimelineTargetKind.ThermalAsset)
        {
            RealtimeTimelineItemPresentation marker = presentation.Rail.Items.Single(item =>
                string.Equals(item.Id, expectedMarkerId, StringComparison.Ordinal));
            bool comparison = expectedMarkerId.StartsWith(
                "DRAFT_THERMAL:",
                StringComparison.Ordinal);
            bool commonThermalContext = presentation.Context.Sections.Any(item =>
                    item.Heading == "예상 시각" &&
                    string.Equals(item.Body, marker.TimeLabel,
                        StringComparison.Ordinal)) &&
                presentation.Context.Sections.Any(item =>
                    item.Heading == "예상 변화");
            Require(commonThermalContext &&
                    (comparison
                        ? string.Equals(
                              presentation.Context.Eyebrow,
                              "현재 초안 기준 예상 · 열 보호",
                              StringComparison.Ordinal) &&
                          presentation.Context.Sections.Any(item =>
                              item.Heading == "근거" &&
                              item.Body.Contains(
                                  "현재 초안 기준 예상",
                                  StringComparison.Ordinal))
                        : presentation.Context.Eyebrow.Contains(
                              "열 보호 예상",
                              StringComparison.Ordinal) &&
                          presentation.Context.Sections.Any(item =>
                              item.Heading == "현재 상태")),
                comparison
                    ? $"comparison thermal marker {expectedMarkerId} context lost " +
                      "draft/time/change/evidence causality"
                    : $"thermal marker {expectedMarkerId} context lost " +
                      "event/time/state causality",
                failures);
        }
        else if (target.Kind == RealtimeTimelineTargetKind.Event)
        {
            RealtimeWorldHighlight? highlight = presentation.World.Highlight;
            bool comparison = expectedMarkerId.StartsWith(
                "DRAFT_FORECAST:",
                StringComparison.Ordinal);
            bool comparisonContext = !comparison ||
                string.Equals(
                    presentation.Context.Eyebrow,
                    "현재 초안 기준 예상",
                    StringComparison.Ordinal) &&
                presentation.Context.Sections.Any(item =>
                    item.Heading == "발생") &&
                presentation.Context.Sections.Any(item =>
                    item.Heading == "안전 의무" &&
                    item.Body.StartsWith(
                        "현재 초안 기준 예상",
                        StringComparison.Ordinal)) &&
                presentation.Context.Sections.Any(item =>
                    item.Heading == "첫 병목") &&
                presentation.Context.Details.Any(item =>
                    item.Tab == RealtimeContextDetailTab.Forecast &&
                    string.Equals(
                        item.Heading,
                        "현재 초안 기준 예상",
                        StringComparison.Ordinal));
            Require(comparisonContext &&
                    target.MapSubjectId is not null &&
                    presentation.World.SelectedAssetId is not null &&
                    highlight is not null &&
                    (highlight.NodeIds.Contains(target.MapSubjectId,
                         StringComparer.Ordinal) ||
                     highlight.EdgeIds.Contains(target.MapSubjectId,
                         StringComparer.Ordinal) ||
                     string.Equals(highlight.LimitingAssetId,
                         target.MapSubjectId,
                         StringComparison.Ordinal)),
                $"event marker {expectedMarkerId} did not expose its exact context, " +
                $"affected map asset, and route cue (target=" +
                $"{target.MapSubjectId ?? "<none>"}, " +
                $"selected={presentation.World.SelectedAssetId ?? "<none>"}, " +
                $"nodes=[{string.Join(",", highlight?.NodeIds ?? Array.Empty<string>())}], " +
                $"edges=[{string.Join(",", highlight?.EdgeIds ?? Array.Empty<string>())}], " +
                $"limiter={highlight?.LimitingAssetId ?? "<none>"})",
                failures);
        }
    }

    private void ValidateInputPriority(ICollection<string> failures)
    {
        long draft = _uiRoot.ClaimInput(
            "smoke_draft", RealtimeInputPriority.DraftHandle);
        long hud = _uiRoot.ClaimInput("smoke_hud", RealtimeInputPriority.Hud);
        long fatal = _uiRoot.ClaimInput("smoke_fatal", RealtimeInputPriority.Fatal);
        Require(_uiRoot.InputRouterForSmoke.ActiveOwner == "smoke_fatal" &&
                !_uiRoot.CanWorldReceiveInput,
            "fatal input did not pre-empt every lower owner", failures);
        _uiRoot.ReleaseInput(fatal);
        Require(_uiRoot.InputRouterForSmoke.ActiveOwner == "smoke_hud",
            "HUD input did not pre-empt the active draft", failures);
        _uiRoot.ReleaseInput(hud);
        Require(_uiRoot.InputRouterForSmoke.ActiveOwner == "smoke_draft",
            "draft input did not pre-empt world candidates", failures);
        _uiRoot.ReleaseInput(draft);
        Require(_uiRoot.InputRouterForSmoke.ActivePriority ==
                RealtimeInputPriority.EmptyTerrain,
            "input stack did not restore empty-terrain/world authority", failures);
    }

    private async Task ValidateInjectedKeyDelivery(
        RealtimeSlicePresentation restorePresentation,
        ICollection<string> failures)
    {
        var slice = new RealtimeSliceMain();
        using var sliceLifetime = slice.FreeAfterSmoke();
        slice.BootstrapForSmoke();
        string modalId = slice.InteractionState.ActiveModalId ?? string.Empty;
        Require(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.CloseModal(modalId)).Accepted,
            "key-delivery fixture could not close the actual chapter modal", failures);
        Present(slice.LatestPresentation);
        await SettleLayout();
        _uiRoot.FocusOwnerForSmoke?.ReleaseFocus();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var delivered = new List<RealtimeInputRequest>();
        void Observe(RealtimeInputRequest request) => delivered.Add(request);
        _uiRoot.InputRequested += Observe;
        slice.AttachInputUiForSmoke(_uiRoot);
        try
        {
            await InjectKeyAndRequire(
                Key.P,
                pressed: true,
                RealtimeInputCommand.TogglePause,
                delivered,
                slice,
                failures);
            Require(slice.InteractionState.Simulation ==
                    RealtimeSimulationState.PlayerPaused,
                "P did not pause the live reducer", failures);
            await InjectKeyAndRequire(
                Key.P,
                pressed: true,
                RealtimeInputCommand.TogglePause,
                delivered,
                slice,
                failures);
            Require(slice.InteractionState.Simulation == RealtimeSimulationState.Running,
                "second P did not resume the live reducer", failures);

            foreach ((Key key, RealtimeInputCommand command,
                      RealtimeSimulationSpeed speed) in new[]
                     {
                         (Key.Key1, RealtimeInputCommand.SetNormalSpeed,
                             RealtimeSimulationSpeed.Normal),
                         (Key.Key2, RealtimeInputCommand.SetFastSpeed,
                             RealtimeSimulationSpeed.Fast),
                         (Key.Key4, RealtimeInputCommand.SetVeryFastSpeed,
                             RealtimeSimulationSpeed.VeryFast),
                     })
            {
                await InjectKeyAndRequire(
                    key,
                    pressed: true,
                    command,
                    delivered,
                    slice,
                    failures);
                Require(slice.InteractionState.RunningSpeed == speed,
                    $"{key} did not select {speed}", failures);
            }

            var textEntry = new LineEdit
            {
                Name = "SmokeTextEntry",
                Text = string.Empty,
                Position = Vector2.Zero,
                Size = new Vector2(240f, 44f),
            };
            AddChild(textEntry);
            textEntry.GrabFocus();
            await SettleLayout();
            int textEntryBefore = delivered.Count;
            Input.ParseInputEvent(KeyEvent(Key.Space, pressed: true));
            await SettleLayout();
            Input.ParseInputEvent(KeyEvent(Key.Space, pressed: false));
            await SettleLayout();
            Require(delivered.Count == textEntryBefore &&
                    !_uiRoot.InputRouterForSmoke.PanCaptured,
                "text-entry-focused Space escaped as BeginPan/ui_accept or captured pan",
                failures);
            textEntry.ReleaseFocus();
            RemoveAndFree(textEntry);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            await InjectKeyAndRequire(
                Key.Space,
                pressed: true,
                RealtimeInputCommand.BeginPan,
                delivered,
                slice,
                failures);
            Require(_uiRoot.InputRouterForSmoke.PanCaptured,
                "Space press did not capture pan input", failures);
            var releaseFocus = new LineEdit
            {
                Name = "SmokePanReleaseTextEntry",
                Position = Vector2.Zero,
                Size = new Vector2(240f, 44f),
            };
            AddChild(releaseFocus);
            releaseFocus.GrabFocus();
            await SettleLayout();
            await InjectKeyAndRequire(
                Key.Space,
                pressed: false,
                RealtimeInputCommand.EndPan,
                delivered,
                slice,
                failures);
            Require(!_uiRoot.InputRouterForSmoke.PanCaptured,
                "Space release after a focus change did not release pan input", failures);
            releaseFocus.ReleaseFocus();
            RemoveAndFree(releaseFocus);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            foreach ((Key key, RealtimeInputCommand command) in new[]
                     {
                         (Key.Q, RealtimeInputCommand.CycleCandidatePrevious),
                         (Key.E, RealtimeInputCommand.CycleCandidateNext),
                         (Key.Enter, RealtimeInputCommand.ConfirmOrSelect),
                         (Key.Backspace, RealtimeInputCommand.UndoDraftStep),
                         (Key.Home, RealtimeInputCommand.TimelineHome),
                         (Key.Bracketleft, RealtimeInputCommand.TimelinePrevious),
                         (Key.Bracketright, RealtimeInputCommand.TimelineNext),
                         (Key.I, RealtimeInputCommand.SelectInspectTool),
                         (Key.N, RealtimeInputCommand.SelectFirstNodeTool),
                         (Key.L, RealtimeInputCommand.SelectFirstLineTool),
                         (Key.Escape, RealtimeInputCommand.CancelOrBack),
                     })
            {
                await InjectKeyAndRequire(
                    key,
                    pressed: true,
                    command,
                    delivered,
                    slice,
                    failures);
            }

            long draft = _uiRoot.ClaimInput(
                "key_delivery_draft",
                RealtimeInputPriority.DraftHandle);
            int blockedBefore = delivered.Count;
            Input.ParseInputEvent(KeyEvent(Key.Q, pressed: true));
            await SettleLayout();
            Require(delivered.Count == blockedBefore,
                "lower-priority Q escaped an active draft owner", failures);
            Input.ParseInputEvent(KeyEvent(Key.Q, pressed: false));
            await SettleLayout();
            _uiRoot.ReleaseInput(draft);
        }
        finally
        {
            slice.DetachInputUiForSmoke(_uiRoot);
            _uiRoot.InputRequested -= Observe;
        }
        GD.Print(
            "REALTIME_R2_INPUT_SMOKE synthetic SceneTree key injection passed; " +
            "physical keyboard hardware delivery remains a native/manual gate");
        Present(restorePresentation);
        await SettleLayout();
    }

    private async Task InjectKeyAndRequire(
        Key key,
        bool pressed,
        RealtimeInputCommand expected,
        ICollection<RealtimeInputRequest> delivered,
        RealtimeSliceMain slice,
        ICollection<string> failures)
    {
        int before = delivered.Count;
        Input.ParseInputEvent(KeyEvent(key, pressed));
        await SettleLayout();
        Require(delivered.Count == before + 1 &&
                delivered.LastOrDefault().Command == expected &&
                slice.InputOwnershipFacts.LastRequest?.Command == expected,
            $"{key} {(pressed ? "press" : "release")} was not delivered exactly once " +
            $"as {expected}",
            failures);
        if (pressed && key != Key.Space)
        {
            int afterPress = delivered.Count;
            Input.ParseInputEvent(KeyEvent(key, pressed: false));
            await SettleLayout();
            Require(delivered.Count == afterPress,
                $"{key} release emitted a duplicate command", failures);
        }
    }

    private static InputEventKey KeyEvent(
        Key key,
        bool pressed,
        bool shiftPressed = false,
        bool echo = false) => new()
    {
        Keycode = key,
        PhysicalKeycode = key,
        Pressed = pressed,
        ShiftPressed = shiftPressed,
        Echo = echo,
    };

    private async Task ValidateModalFocusAndPause(ICollection<string> failures)
    {
        var source = new RealtimeSliceMain();
        using var sourceLifetime = source.FreeAfterSmoke();
        source.BootstrapForSmoke();
        RealtimeModalPresentation modal = source.LatestPresentation.Modal ??
            throw new InvalidOperationException(
                "Actual R1 briefing did not create the allowed chapter modal.");
        BaseButton returnFocus = _uiRoot.PrimaryActionForSmoke();
        returnFocus.GrabFocus();
        await SettleLayout();
        Require(ReferenceEquals(_uiRoot.FocusOwnerForSmoke, returnFocus),
            "background primary action could not take focus", failures);

        Require(_uiRoot.PushModal(modal),
            "single actual chapter modal was rejected", failures);
        await SettleLayout();
        Require(_uiRoot.ModalHostForSmoke.Depth == 1 &&
                _uiRoot.ModalHostForSmoke.ActivePause == modal.Pause &&
                _uiRoot.ModalHostForSmoke.OwnsFocusForSmoke &&
                _uiRoot.InputRouterForSmoke.ActivePriority ==
                    RealtimeInputPriority.BlockingModal,
            "modal did not own focus/input with its typed pause reason", failures);
        Require(_uiRoot.ModalHostForSmoke.AccessibilitySummaryForSmoke.Contains(
                    modal.Pause.CurrentTimeLabel, StringComparison.Ordinal) &&
                _uiRoot.ModalHostForSmoke.AccessibilitySummaryForSmoke.Contains(
                    modal.Pause.NextEventLabel, StringComparison.Ordinal),
            "modal accessibility omitted authoritative current time or next event",
            failures);
        IReadOnlyList<RealtimeUiSmokeFocusLinkFact> modalLinks =
            _uiRoot.ModalHostForSmoke.FocusLinksForSmoke();
        Require(modalLinks.Count > 0 && modalLinks.All(link =>
                    link.NextInsideModal && link.PreviousInsideModal &&
                    !string.IsNullOrWhiteSpace(link.NextPath) &&
                    !string.IsNullOrWhiteSpace(link.PreviousPath)),
            "modal Tab/Shift+Tab cycle can escape the single focus scope", failures);
        Require(!_uiRoot.PushModal(modal) &&
                _uiRoot.ModalHostForSmoke.Depth == 1,
            "nested modal was accepted", failures);
        Require(_uiRoot.PopModal(), "active modal could not close", failures);
        await SettleLayout();
        Require(_uiRoot.ModalHostForSmoke.Depth == 0 &&
                ReferenceEquals(_uiRoot.FocusOwnerForSmoke, returnFocus) &&
                _uiRoot.InputRouterForSmoke.ActivePriority ==
                    RealtimeInputPriority.EmptyTerrain,
            "modal close did not restore focus and prior input authority", failures);
    }

    private async Task SettleLayout()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task<bool> SettleUntil(
        Func<bool> condition,
        int maximumFrames = 30)
    {
        ArgumentNullException.ThrowIfNull(condition);
        for (int frame = 0; frame < maximumFrames; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (condition())
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                return true;
            }
        }
        return condition();
    }

    private async Task<Rect2> SettleStableRect(
        Func<Rect2> capture,
        int maximumFrames = 30)
    {
        ArgumentNullException.ThrowIfNull(capture);
        Rect2 previous = capture();
        int stableFrames = 0;
        for (int frame = 0; frame < maximumFrames; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Rect2 current = capture();
            if (current.Position.IsEqualApprox(previous.Position) &&
                current.Size.IsEqualApprox(previous.Size))
            {
                stableFrames++;
                if (stableFrames >= 3)
                {
                    return current;
                }
            }
            else
            {
                stableFrames = 0;
            }
            previous = current;
        }
        return capture();
    }

    private void FinishAndQuit(int exitCode)
    {
        SceneTree tree = GetTree();
        ScheduleQuitAfterCleanup(tree, exitCode);
        if (ReferenceEquals(tree.CurrentScene, this))
        {
            tree.CurrentScene = null;
        }
        if (GetParent() is Node parent)
        {
            parent.RemoveChild(this);
        }
        Free();
    }

    private static void ScheduleQuitAfterCleanup(SceneTree tree, int exitCode)
    {
        int remainingFrames = 3;
        void DrainAndQuit()
        {
            remainingFrames--;
            if (remainingFrames > 0)
            {
                return;
            }
            tree.ProcessFrame -= DrainAndQuit;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            tree.Quit(exitCode);
        }
        tree.ProcessFrame += DrainAndQuit;
    }

    private static void RemoveAndFree(Node node)
    {
        if (!GodotObject.IsInstanceValid(node))
        {
            return;
        }
        node.GetParent()?.RemoveChild(node);
        node.Free();
    }

    private static bool RectApproximatelyEqual(Rect2 left, Rect2 right) =>
        left.Position.DistanceTo(right.Position) <= 1f &&
        left.Size.DistanceTo(right.Size) <= 1f;

    private static void Require(
        bool condition,
        string message,
        ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }
#endif
}
