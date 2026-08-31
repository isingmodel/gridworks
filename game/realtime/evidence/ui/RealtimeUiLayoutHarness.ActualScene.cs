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
        slice.UseTechnicalFixtureLaunchForSmoke();
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
        slice.UseTechnicalFixtureLaunchForSmoke();
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
            RealtimeWorldMap map = slice.MapForSmoke;
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
                        StringComparison.Ordinal) &&
                    map.ActiveCandidateVisibleLabelForSmoke.Contains(
                        "Q/E 전환",
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
                "actual A key did not reach the WorldMap _Draw analysis/risk " +
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
                "second actual A key left the WorldMap analysis draw path active",
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
                        RealtimeR2Ids.NodeToolPrefix, StringComparison.Ordinal) == true &&
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
                        RealtimeR2Ids.LineToolPrefix, StringComparison.Ordinal) == true &&
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
                        nodeToolId[RealtimeR2Ids.NodeToolPrefix.Length..],
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
                    slice.InteractionState.Simulation ==
                        RealtimeSimulationState.PlayerPaused,
                "actual Esc-chain fixture did not preserve the build-entry planning pause",
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
                        RealtimeSimulationState.PlayerPaused &&
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
                    slice.PresentationRevision == escRevision + 4,
                "Esc step 4 did not preserve the existing typed planning pause",
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
            string lineToolId = RealtimeR2Ids.LineTool(plan.LineClassId, plan.PoleClassId);
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
                .Select(item => RealtimeR2Ids.ComparisonEventMarker(item.EventId))
                .ToArray();
            string[] liveComparisonThermalMarkerIds = liveComparisonEvents
                .SelectMany(item => item.TemporalProjection.Transitions.Select(
                    transition =>
                        RealtimeR2Ids.ComparisonThermalMarker(
                            item.EventId,
                            transition)))
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
                        PrimaryAction.Id: RealtimeR2Ids.NoticeCloseAction,
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
                        RealtimeR2Ids.NoticeCloseAction,
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
                        PrimaryAction.Id: RealtimeR2Ids.NoticeCloseAction,
                        PrimaryAction.Label: "안내 닫기",
                        PrimaryAction.Tone: RealtimeActionTone.Primary,
                        SecondaryAction: null,
                    } &&
                    string.Equals(
                        ui.ModalHostForSmoke.ActivePrimaryActionIdForSmoke,
                        RealtimeR2Ids.NoticeCloseAction,
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
                "actual mouse Next at selected-last boundary changed selection/revision " +
                $"(selected={slice.TimelineChooserFacts.SelectedMarkerId ?? "<none>"}, " +
                $"expected={lastBoundaryItem.Id}, revision=" +
                $"{slice.PresentationRevision}/{selectedLastRevision}, items=[" +
                $"{string.Join(",", slice.TimelineChooserFacts.VisibleOrderedItemIds)}])",
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
                "selection/revision " +
                $"(selected={slice.TimelineChooserFacts.SelectedMarkerId ?? "<none>"}, " +
                $"expected={firstBoundaryItem.Id}, revision=" +
                $"{slice.PresentationRevision}/{selectedFirstRevision}, items=[" +
                $"{string.Join(",", slice.TimelineChooserFacts.VisibleOrderedItemIds)}])",
                failures);
            PushViewportPrimary(
                viewport,
                mouseNavigation[RealtimeTimelineNavigation.Home].GetCenter());
            await SettleLayout();

            RealtimeTimelineItemPresentation eventItem = slice.LatestPresentation.Rail.Items
                .First(item => RealtimeTimelineTargetResolver.Resolve(
                    slice.DisplayWorldForSmoke,
                    slice.CoreSnapshot,
                    slice.LatestPresentation.BaseForecast,
                    slice.LatestPresentation.ComparisonDraftForecast,
                    item.Id).Kind == RealtimeTimelineTargetKind.Event);
            RealtimeTimelineItemPresentation thermalItem =
                slice.LatestPresentation.Rail.Items.First(item =>
                    RealtimeTimelineTargetResolver.Resolve(
                        slice.DisplayWorldForSmoke,
                        slice.CoreSnapshot,
                        slice.LatestPresentation.BaseForecast,
                        slice.LatestPresentation.ComparisonDraftForecast,
                        item.Id).Kind == RealtimeTimelineTargetKind.ThermalAsset);
            string timelineHash = slice.CanonicalStateSha256;
            int timelineCommands = slice.AcceptedCommandCount;
            ui.EventRailForSmoke.SelectAccessibleTimelineItemForSmoke(eventItem.Id);
            await SettleLayout();
            ValidateTimelineSelection(slice, eventItem.Id, failures);
            RealtimeTimelineTarget eventTarget =
                RealtimeTimelineTargetResolver.Resolve(
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
            ui.EventRailForSmoke.SelectAccessibleTimelineItemForSmoke(thermalItem.Id);
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
                        RealtimeR2Ids.CampaignResultModal,
                        StringComparison.Ordinal) &&
                    campaignModal is
                    {
                        Id: RealtimeR2Ids.CampaignResultModal,
                        Kind: RealtimeModalKind.Story,
                        Heading: "캠페인 운영 완료",
                        PrimaryAction.Id: RealtimeR2Ids.ResultCloseAction,
                        PrimaryAction.Label: "결과 확인",
                        SecondaryAction: null,
                    } &&
                    ui.ModalHostForSmoke.Depth == 1 &&
                    ui.ModalHostForSmoke.OwnsFocusForSmoke &&
                    string.Equals(
                        ui.ModalHostForSmoke.ActivePrimaryActionIdForSmoke,
                        RealtimeR2Ids.ResultCloseAction,
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
                "selection/revision/Core/journal " +
                $"(minute={slice.CurrentMinute}, selected=" +
                $"{slice.InteractionState.TimelineSelectedItemId ?? "<none>"}, " +
                $"revision={slice.PresentationRevision}/{endedNoFutureRevision}, items=[" +
                $"{string.Join(",", slice.TimelineChooserFacts.VisibleOrderedItemIds)}])",
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
        slice.UseTechnicalFixtureLaunchForSmoke();
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
            RealtimeWorldMap map = slice.MapForSmoke;
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
            string buildingClassId = buildingToolId[RealtimeR2Ids.NodeToolPrefix.Length..];
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
                        RealtimeWorldStateCue.None,
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
                RealtimeWorldStateCue expectedCue,
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
                RealtimeWorldStateCue.AuthoredUnavailableBars,
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
                RealtimeWorldStateCue.ProtectiveOutageCross,
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
                RealtimeWorldStateCue drawnCue,
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
                RealtimeWorldStateCue.EmergencyTriangle,
                "비상 운전",
                "비상 운전");
            await ValidateState(
                boundary.TripMinute!.Value,
                ThermalOperatingState.ProtectiveOutage,
                RealtimeWorldAssetState.ProtectiveOutage,
                RealtimeWorldStateCue.ProtectiveOutageCross,
                "보호정지",
                "보호정지");
            await ValidateState(
                boundary.RecoveryMinute!.Value,
                ThermalOperatingState.Emergency,
                RealtimeWorldAssetState.Emergency,
                RealtimeWorldStateCue.EmergencyTriangle,
                "비상 운전",
                "비상 운전");
            long recoveryMinute = boundary.RecoveryMinute.Value;
            Require(slice.EmittedTransitions.Any(item =>
                        item.Kind == RealtimeTransitionKind.ThermalRecovered &&
                        item.Minute == recoveryMinute &&
                        string.Equals(item.AssetId, boundary.AssetId,
                            StringComparison.Ordinal)) &&
                    slice.EmittedTransitions.Any(item =>
                        item.Kind == RealtimeTransitionKind.ThermalEmergencyEntered &&
                        item.Minute == recoveryMinute &&
                        string.Equals(item.AssetId, boundary.AssetId,
                            StringComparison.Ordinal)),
                $"actual {boundary.AssetId}@{recoveryMinute} did not retain the " +
                "same-minute recovered then emergency-reentered transition pair",
                failures);
            long idleMinute = slice.SmokeBoundaryFacts.Events
                .Where(item => item.StartMinute <= recoveryMinute &&
                    item.EndMinute > recoveryMinute)
                .Min(item => item.EndMinute);
            await ValidateState(
                idleMinute,
                ThermalOperatingState.Continuous,
                RealtimeWorldAssetState.Normal,
                RealtimeWorldStateCue.None,
                "정상",
                "연속 운전");
            GD.Print(
                "REALTIME_R2_ACTUAL_THERMAL_RECOVERY_PASS " +
                $"asset={boundary.AssetId}; reentry-minute={recoveryMinute}; " +
                $"idle-minute={idleMinute}; core=Continuous; " +
                "presentation=Normal; ax=정상; draw=None");
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
        slice.UseTechnicalFixtureLaunchForSmoke();
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
            RealtimeWorldMap map = slice.MapForSmoke;
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
                ui.EventRailForSmoke,
                snapshot,
                visibleItems,
                100,
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
        RealtimeWorldMap map,
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

        RealtimeWorldPresentation currentChapter = slice.LatestPresentation.World;
        map.SetPresentation(currentChapter with
        {
            ChapterId = currentChapter.ChapterId + "_NEXT",
        });
        map.ApplyLayout(slice.UiForSmoke.LayoutProfile);
        await SettleLayout();
        Require(map.CandidateIdsForSmoke.Count == 0 &&
                map.ActiveCandidateIdForSmoke is null &&
                map.PreferredCandidateIdForSmoke is null &&
                string.IsNullOrEmpty(map.ActiveCandidateVisibleLabelForSmoke),
            "chapter transition retained a stale overlap candidate",
            failures);
        PushViewportPointerMotion(
            viewport,
            map.ViewportPointForSmoke(worldPoint));
        await SettleLayout();
        Require(map.CandidateIdsForSmoke.Count >= 2 &&
                map.ActiveCandidateIdForSmoke is not null,
            "fresh candidate input did not re-enable chapter-local overlap selection",
            failures);
        map.SetPresentation(currentChapter);
        await SettleLayout();
    }

    private async Task<ActualMapCandidateFact?> ArmNonFirstActualCandidate(
        SubViewport viewport,
        RealtimeSliceMain slice,
        RealtimeWorldMap map,
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
        RealtimeWorldMap map,
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
        RealtimeWorldMap map)
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
                    (frame.ConsumedFrameCount + retainedFramesAfter ==
                         retainedFramesBefore + frame.RequestedFrameCount ||
                     frame.CoreSnapshot.CampaignComplete &&
                         retainedFramesAfter == 0 &&
                         frame.ConsumedFrameCount <=
                             retainedFramesBefore + frame.RequestedFrameCount) &&
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
}
#endif
