#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

internal sealed record RealtimeR2LayoutPresentationSet(
    RealtimeSlicePresentation World,
    RealtimeSlicePresentation BuildShelf,
    RealtimeSlicePresentation Inspector,
    RealtimeSlicePresentation Action,
    RealtimeSlicePresentation Timeline,
    RealtimeSlicePresentation Modal);

/// <summary>
/// Deterministic R2 integration evidence. Every assertion enters through the
/// real slice host, interaction reducer, frame adapter, Core run, and presenter.
/// It deliberately owns no fixture coordinates, authored minutes, or outcomes.
/// </summary>
internal static class RealtimeR2Smoke
{
    internal static void Validate(ICollection<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        RunCase("clock-pause", () => ValidateClockAndPause(failures), failures);
        RunCase("frame-rate-matrix", () => ValidateFrameRateMatrix(failures), failures);
        RunCase("callback-partition-matrix",
            () => ValidateCallbackPartitionMatrix(failures), failures);
        RunCase("typed-order-quote",
            () => ValidateTypedOrderQuote(failures), failures);
        RunCase("line-command-completion", () => ValidateLineConstruction(failures), failures);
        RunCase("speed-authored-outcome", () => ValidateSpeedEquivalence(failures), failures);
        RunCase("critical-pause-boundary", () => ValidateCriticalPauseBoundary(failures), failures);
        RunCase("presentation-authority", () => ValidatePresentationAuthority(failures), failures);
        RunCase("follow-now-timeline", () => ValidateFollowNowTimeline(failures), failures);
        RunCase("player-facing-copy", () => ValidatePlayerFacingCopy(failures), failures);
        RunCase("completed-history-thermal-context",
            () => ValidateCompletedHistoryAndThermalContext(failures), failures);
        RunCase("thermal-protection-copy",
            () => ValidateThermalProtectionCopy(failures), failures);
        RunCase("draft-menu-cancel",
            () => ValidateDraftMenuCancelPolicy(failures), failures);
        RunCase("ended-read-only-shell",
            () => ValidateEndedReadOnlyShell(failures), failures);
        RunCase("analysis-surface-policy",
            () => ValidateAnalysisSurfacePolicy(failures), failures);
        RunCase("comparison-draft-forecast",
            () => ValidateComparisonDraftForecast(failures), failures);
        RunCase("future-event-actual-draft-construction",
            () => ValidateFutureEventActualDraftConstruction(failures), failures);
        RunCase("release-first-light-controller-story",
            () => ValidateReleaseFirstLightControllerStory(failures), failures);
        RunCase("modal-restore", () => ValidateModalRestore(failures), failures);
        RunCase("pointer-priority", () => ValidatePointerPriority(failures), failures);
    }

    /// <summary>
    /// Produces a dense but genuine R1-backed presentation for the live UI
    /// matrix. The line is a comparison draft, so the bottom action dock has
    /// exactly one primary action without inventing a second project or queue.
    /// </summary>
    internal static RealtimeSlicePresentation CreateLayoutPresentation(
        ICollection<string> failures)
    {
        var slice = CreateRunningSlice();
        using var sliceLifetime = slice.FreeAfterSmoke();
        RealtimeSmokeLinePlan plan = slice.SmokeLinePlan;
        slice.AdvanceToForSmoke(plan.OrderMinute);
        RequireIntent(slice.ApplyIntentForSmoke(
            RealtimeR2Intent.SelectBuildTool(
                RealtimeTool.BuildLine,
                $"LINE:{plan.LineClassId}:{plan.PoleClassId}")),
            "layout build tool", failures, coreCommandExpected: false);
        RequireIntent(slice.ApplyIntentForSmoke(plan.Intents[0]),
            "layout line start", failures);
        RequireIntent(slice.ApplyIntentForSmoke(plan.Intents[1]),
            "layout line finish", failures);
        string eventId = slice.SmokeBoundaryFacts.Events[0].EventId;
        RequireIntent(slice.ApplyIntentForSmoke(RealtimeR2Intent.Select(eventId)),
            "layout event selection", failures, coreCommandExpected: false);
        return slice.LatestPresentation;
    }

    internal static RealtimeR2LayoutPresentationSet CreateLayoutPresentations(
        ICollection<string> failures)
    {
        var worldSlice = CreateRunningSlice();
        using var worldLifetime = worldSlice.FreeAfterSmoke();
        RealtimeSlicePresentation world = worldSlice.LatestPresentation;

        var buildSlice = CreateRunningSlice();
        using var buildLifetime = buildSlice.FreeAfterSmoke();
        RequireIntent(buildSlice.ApplyIntentForSmoke(new RealtimeR2Intent(
                RealtimeR2IntentKind.OpenSurface,
                Surface: RealtimeSurface.Drawer)),
            "layout build shelf", failures, coreCommandExpected: false);
        RealtimeSlicePresentation buildShelf = buildSlice.LatestPresentation;

        var inspectorSlice = CreateRunningSlice();
        using var inspectorLifetime = inspectorSlice.FreeAfterSmoke();
        string eventId = inspectorSlice.SmokeBoundaryFacts.Events[0].EventId;
        RequireIntent(inspectorSlice.ApplyIntentForSmoke(
                RealtimeR2Intent.Select(eventId)),
            "layout inspector selection", failures, coreCommandExpected: false);
        RealtimeSlicePresentation inspector = inspectorSlice.LatestPresentation;

        var actionSlice = CreateRunningSlice();
        using var actionLifetime = actionSlice.FreeAfterSmoke();
        RealtimeSmokeLinePlan plan = actionSlice.SmokeLinePlan;
        actionSlice.AdvanceToForSmoke(plan.OrderMinute);
        RequireIntent(actionSlice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildLine,
                    $"LINE:{plan.LineClassId}:{plan.PoleClassId}")),
            "layout action tool", failures, coreCommandExpected: false);
        RequireIntent(actionSlice.ApplyIntentForSmoke(plan.Intents[0]),
            "layout action start", failures);
        RequireIntent(actionSlice.ApplyIntentForSmoke(plan.Intents[1]),
            "layout action finish", failures);
        RealtimeSlicePresentation action = actionSlice.LatestPresentation;

        var timelineSlice = CreateRunningSlice();
        using var timelineLifetime = timelineSlice.FreeAfterSmoke();
        RequireIntent(timelineSlice.ApplyIntentForSmoke(new RealtimeR2Intent(
                RealtimeR2IntentKind.OpenSurface,
                Surface: RealtimeSurface.Timeline)),
            "layout expanded timeline", failures, coreCommandExpected: false);
        RealtimeSlicePresentation timeline = timelineSlice.LatestPresentation;

        var modalSlice = new RealtimeSliceMain();
        using var modalLifetime = modalSlice.FreeAfterSmoke();
        modalSlice.BootstrapForSmoke();
        return new RealtimeR2LayoutPresentationSet(
            world,
            buildShelf,
            inspector,
            action,
            timeline,
            modalSlice.LatestPresentation);
    }

    private static void ValidateClockAndPause(ICollection<string> failures)
    {
        var slice = CreateRunningSlice();
        using var sliceLifetime = slice.FreeAfterSmoke();
        long minute = slice.CurrentMinute;
        string hash = slice.CanonicalStateSha256;
        int commands = slice.AcceptedCommandCount;

        RealtimeR2FrameResult running =
            slice.InjectElapsedNanosecondsForSmoke(1_000_000_000);
        Check(slice.CurrentMinute == minute + 1,
            "one wall-clock second at 1x did not advance one simulation minute", failures);
        Check(running.Frame?.AppliedMinutes == 1,
            "running frame did not expose its one applied minute", failures);
        Check(slice.AcceptedCommandCount == commands,
            "clock advancement created a click/command journal entry", failures);
        Check(!string.Equals(hash, slice.CanonicalStateSha256, StringComparison.Ordinal),
            "advancing the authoritative minute retained the old canonical hash", failures);

        slice.SetPlayerPausedForSmoke(true);
        long pausedMinute = slice.CurrentMinute;
        string pausedHash = slice.CanonicalStateSha256;
        int pausedCommands = slice.AcceptedCommandCount;
        RealtimeFrameAccumulatorSnapshot pausedAccumulator = slice.AccumulatorSnapshot;
        RealtimeR2FrameResult ignored =
            slice.InjectElapsedNanosecondsForSmoke(60_000_000_000);
        Check(ignored.Frame is null && ignored.Transitions.Count == 0,
            "player pause produced a Core frame result or transition", failures);
        Check(slice.CurrentMinute == pausedMinute &&
              string.Equals(slice.CanonicalStateSha256, pausedHash, StringComparison.Ordinal) &&
              slice.AcceptedCommandCount == pausedCommands,
            "player pause changed minute, canonical state, or journal", failures);
        Check(slice.AccumulatorSnapshot == pausedAccumulator,
            "ignored paused wall-clock time changed accumulator remainder/debt", failures);
        Check(slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused &&
              slice.InteractionState.PauseReason == RealtimePauseReason.PlayerRequest,
            "player pause lost its typed interaction reason", failures);

        slice.SetPlayerPausedForSmoke(false);
        slice.InjectElapsedNanosecondsForSmoke(1_000_000_000);
        Check(slice.CurrentMinute == pausedMinute + 1,
            "resume did not continue from the exact paused minute", failures);
    }

    private static void ValidateLineConstruction(ICollection<string> failures)
    {
        var slice = CreateRunningSlice();
        using var sliceLifetime = slice.FreeAfterSmoke();
        RealtimeSmokeLinePlan plan = slice.SmokeLinePlan;
        slice.AdvanceToForSmoke(plan.OrderMinute);
        int expectedCommands = slice.AcceptedCommandCount;

        foreach ((RealtimeR2Intent intent, int index) in
                 plan.Intents.Select((intent, index) => (intent, index)))
        {
            RealtimeR2IntentResult result = slice.ApplyIntentForSmoke(intent);
            RequireIntent(result, $"line intent {index}", failures);
            Check(result.JournalDelta == 1 &&
                  result.AfterCommandSequence == result.BeforeCommandSequence + 1,
                $"line intent {index} was not exactly one command/result", failures);
            Check(result.BeforeMinute == plan.OrderMinute &&
                  result.AfterMinute == plan.OrderMinute,
                $"line intent {index} advanced simulation time", failures);
            Check(result.PresentationRevisionDelta == 1,
                $"line intent {index} did not publish exactly one presentation revision",
                failures);
            expectedCommands++;
            Check(slice.AcceptedCommandCount == expectedCommands,
                $"line intent {index} journal count mismatch", failures);
        }

        RealtimeR2IntentResult secondOrder =
            slice.ApplyIntentForSmoke(RealtimeR2Intent.OrderLine());
        Check(!secondOrder.Accepted && secondOrder.CoreCommandResult is not null &&
              secondOrder.CoreCommandResult.Error == plan.ExpectedSecondOrderError &&
              secondOrder.CoreCommandResult.ConstructionError ==
                  plan.ExpectedSecondOrderConstructionError,
            "second line order did not preserve the fixture-derived typed rejection",
            failures);
        Check(secondOrder.JournalDelta == 0 &&
              slice.AcceptedCommandCount == expectedCommands,
            "rejected second line order entered the journal/hidden queue", failures);
        Check(slice.CoreSnapshot.Construction.ActiveConstruction is not null,
            "accepted line order did not create the sole active project", failures);

        IReadOnlyList<RealtimeTransition> completionTransitions = AdvanceToMinuteByFrames(
            slice,
            plan.ExpectedCompletionMinute,
            RealtimeSimulationSpeed.Normal,
            failures);
        int commissioned = IndexOf(completionTransitions, transition =>
            transition.Kind == RealtimeTransitionKind.ConstructionCompleted &&
            transition.Minute == plan.ExpectedCompletionMinute);
        int sameMinuteEvent = IndexOf(completionTransitions, transition =>
            transition.Kind == RealtimeTransitionKind.EventStarted &&
            transition.Minute == plan.ExpectedCompletionMinute);
        Check(commissioned >= 0,
            "line did not auto-complete from injected frames", failures);
        Check(sameMinuteEvent < 0 || commissioned < sameMinuteEvent,
            "same-minute event ran before construction commissioning", failures);
        Check(slice.CoreSnapshot.Construction.ActiveConstruction is null,
            "commissioned line retained an active project/hidden queue", failures);
        foreach (string edgeId in plan.ExpectedEdgeIds)
        {
            Check(slice.CoreSnapshot.Construction.World.Edges.Single(edge =>
                    string.Equals(edge.EdgeId, edgeId, StringComparison.Ordinal)).Commissioned,
                $"expected commissioned edge {edgeId} is unavailable", failures);
        }
        foreach (string nodeId in plan.ExpectedNodeIds)
        {
            Check(slice.CoreSnapshot.Construction.World.Nodes.Single(node =>
                    string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)).Commissioned,
                $"expected commissioned node {nodeId} is unavailable", failures);
        }
    }

    private static void ValidateTypedOrderQuote(ICollection<string> failures)
    {
        var nodeSlice = CreateRunningSlice();
        using var nodeLifetime = nodeSlice.FreeAfterSmoke();
        (string nodeToolId, CoreMapPoint nodePosition) =
            nodeSlice.AcceptedNodeDraftForSmoke();
        RequireIntent(nodeSlice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildNode,
                    nodeToolId)),
            "node quote tool", failures, coreCommandExpected: false);
        RequireIntent(nodeSlice.ApplyIntentForSmoke(new RealtimeR2Intent(
                RealtimeR2IntentKind.SetNodeDraft,
                FirstId: nodeToolId["NODE:".Length..],
                Position: nodePosition)),
            "node quote draft", failures);
        AssertAcceptedOrderQuote(
            nodeSlice.PreviewNodeOrderForSmoke(),
            nodeSlice.LatestPresentation.ActionDock,
            "ORDER_NODE",
            "node",
            failures);

        var lineSlice = CreateRunningSlice();
        using var lineLifetime = lineSlice.FreeAfterSmoke();
        RealtimeSmokeLinePlan plan = lineSlice.SmokeLinePlan;
        lineSlice.AdvanceToForSmoke(plan.OrderMinute);
        RequireIntent(lineSlice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildLine,
                    $"LINE:{plan.LineClassId}:{plan.PoleClassId}")),
            "line quote tool", failures, coreCommandExpected: false);
        RequireIntent(lineSlice.ApplyIntentForSmoke(plan.Intents[0]),
            "line quote start", failures);
        RequireIntent(lineSlice.ApplyIntentForSmoke(plan.Intents[1]),
            "line quote finish", failures);
        AssertAcceptedOrderQuote(
            lineSlice.PreviewLineOrderForSmoke(),
            lineSlice.LatestPresentation.ActionDock,
            "ORDER_LINE",
            "line",
            failures);

        RequireIntent(lineSlice.ApplyIntentForSmoke(RealtimeR2Intent.OrderLine()),
            "line quote order", failures);
        (string comparisonNodeToolId, CoreMapPoint comparisonNodePosition) =
            lineSlice.AcceptedNodeDraftForSmoke();
        RequireIntent(lineSlice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildNode,
                    comparisonNodeToolId)),
            "rejected node quote tool", failures, coreCommandExpected: false);
        RequireIntent(lineSlice.ApplyIntentForSmoke(new RealtimeR2Intent(
                RealtimeR2IntentKind.SetNodeDraft,
                FirstId: comparisonNodeToolId["NODE:".Length..],
                Position: comparisonNodePosition)),
            "rejected node quote draft", failures);
        RealtimeProjectQuote rejected = lineSlice.PreviewNodeOrderForSmoke();
        RealtimeActionPresentation? rejectedAction =
            lineSlice.LatestPresentation.ActionDock.PrimaryAction;
        Check(rejected is
              {
                  Accepted: false,
                  Error: RealtimeRunError.ConstructionRejected,
                  ConstructionError: ConstructionError.WrongPhase,
              } &&
              rejectedAction is { Id: "ORDER_NODE", Enabled: false } &&
              rejectedAction.Description.StartsWith(
                  "발주 불가 · 현재 공사가 ",
                  StringComparison.Ordinal) &&
              rejectedAction.Description.EndsWith(
                  "에 끝난 뒤 발주할 수 있습니다.",
                  StringComparison.Ordinal),
            "typed rejected node quote did not disable the exact pre-order action " +
            "with a visible rejection reason",
            failures);
    }

    private static void AssertAcceptedOrderQuote(
        RealtimeProjectQuote quote,
        RealtimeActionDockPresentation dock,
        string expectedActionId,
        string label,
        ICollection<string> failures)
    {
        if (quote is not
            {
                Accepted: true,
                CostCashUnit: long cost,
                BuildMinutes: long buildMinutes,
                CompletionMinute: long completionMinute,
            })
        {
            Check(false, $"{label} Core pre-order quote was not complete", failures);
            return;
        }
        long day = checked(completionMinute / (24 * 60) + 1);
        long minuteOfDay = completionMinute % (24 * 60);
        string expected = string.Create(
            CultureInfo.InvariantCulture,
            $"발주 견적 · 비용 {cost:N0}만 원 · 공기 {buildMinutes}분 · " +
            $"{day}일 {minuteOfDay / 60:00}:{minuteOfDay % 60:00} 완공");
        Check(dock is
              {
                  Visible: true,
                  PrimaryAction.Enabled: true,
              } &&
              string.Equals(
                  dock.PrimaryAction!.Id,
                  expectedActionId,
                  StringComparison.Ordinal) &&
              string.Equals(
                  dock.PrimaryAction.Description,
                  expected,
                  StringComparison.Ordinal) &&
              dock.Detail.Contains(expected, StringComparison.Ordinal),
            $"{label} action did not render the exact typed Core cost/build/completion quote",
            failures);
    }

    private static void ValidateFrameRateMatrix(ICollection<string> failures)
    {
        string? referenceHash = null;
        string? referenceTransitions = null;
        foreach (RealtimeSimulationSpeed speed in new[]
                 {
                     RealtimeSimulationSpeed.Normal,
                     RealtimeSimulationSpeed.Fast,
                     RealtimeSimulationSpeed.VeryFast,
                 })
        foreach (int framesPerSecond in new[] { 30, 60, 120, 144 })
        {
            var slice = CreateRunningSlice();
            using var sliceLifetime = slice.FreeAfterSmoke();
            slice.SetSpeedForSmoke(speed);
            long startMinute = slice.CurrentMinute;
            int commands = slice.AcceptedCommandCount;
            const int targetMinutes = 4;
            long frameCount = checked(
                (long)framesPerSecond * targetMinutes / (int)speed);

            RealtimeR2FrameResult result =
                slice.InjectFramesForSmoke(frameCount, framesPerSecond);
            string label = $"{framesPerSecond}fps/{(int)speed}x";
            Check(result.RequestedFrameCount == frameCount &&
                  result.ConsumedFrameCount == frameCount &&
                  result.FramesPerSecond == framesPerSecond &&
                  result.RetainedFrameDebt.Count == 0,
                $"{label} did not consume the exact deterministic frame batch",
                failures);
            Check(slice.CurrentMinute == startMinute + targetMinutes,
                $"{label} did not advance exactly {targetMinutes} minutes", failures);
            Check(slice.AcceptedCommandCount == commands,
                $"{label} frame injection created a command", failures);
            Check(slice.AccumulatorSnapshot.FractionalMinuteUnits == 0,
                $"{label} retained a fractional remainder after an exact batch", failures);

            string transitions = Fingerprint(slice.EmittedTransitions);
            if (referenceHash is null)
            {
                referenceHash = slice.CanonicalStateSha256;
                referenceTransitions = transitions;
            }
            else
            {
                Check(string.Equals(referenceHash, slice.CanonicalStateSha256,
                        StringComparison.Ordinal),
                    $"{label} diverged from the canonical 30fps/1x outcome", failures);
                Check(string.Equals(referenceTransitions, transitions,
                        StringComparison.Ordinal),
                    $"{label} diverged from the ordered 30fps/1x transitions", failures);
            }
        }
    }

    private static void ValidateCallbackPartitionMatrix(ICollection<string> failures)
    {
        string? referenceHash = null;
        string? referenceTransitions = null;
        foreach (RealtimeSimulationSpeed speed in new[]
                 {
                     RealtimeSimulationSpeed.Normal,
                     RealtimeSimulationSpeed.Fast,
                     RealtimeSimulationSpeed.VeryFast,
                 })
        foreach (int callbackRate in new[] { 30, 60, 120, 144 })
        {
            var slice = CreateRunningSlice();
            using var sliceLifetime = slice.FreeAfterSmoke();
            slice.SetSpeedForSmoke(speed);
            long startMinute = slice.CurrentMinute;
            int commands = slice.AcceptedCommandCount;
            const int targetMinutes = 2;
            int callbackCount = checked(
                callbackRate * targetMinutes / (int)speed);
            RealtimeR2FrameResult? final = null;
            int remainingCallbacks = callbackCount;
            foreach (int requestedChunk in new[] { 1, 2, 3, 5, 7, 11, 13 })
            {
                if (remainingCallbacks == 0)
                {
                    break;
                }
                int chunk = Math.Min(requestedChunk, remainingCallbacks);
                final = slice.InjectElapsedSecondsForSmoke(
                    chunk / (double)callbackRate);
                remainingCallbacks -= chunk;
            }
            if (remainingCallbacks > 0)
            {
                final = slice.InjectElapsedSecondsForSmoke(
                    remainingCallbacks / (double)callbackRate);
            }

            string label = $"wall-callback/{callbackRate}Hz/{(int)speed}x";
            Check(final is not null &&
                  slice.CurrentMinute == startMinute + targetMinutes &&
                  slice.AcceptedCommandCount == commands &&
                  slice.RetainedFrameDebt.Count == 0 &&
                  slice.AccumulatorSnapshot.FractionalMinuteUnits == 0 &&
                  final.WallClockRemainderUnits == 0,
                $"{label} lost time, created a command/debt, or retained a remainder",
                failures);

            string transitions = Fingerprint(slice.EmittedTransitions);
            if (referenceHash is null)
            {
                referenceHash = slice.CanonicalStateSha256;
                referenceTransitions = transitions;
            }
            else
            {
                Check(string.Equals(referenceHash, slice.CanonicalStateSha256,
                        StringComparison.Ordinal) &&
                      string.Equals(referenceTransitions, transitions,
                        StringComparison.Ordinal),
                    $"{label} diverged from the 30Hz/1x wall-clock partition",
                    failures);
            }
        }
    }

    private static void ValidateSpeedEquivalence(ICollection<string> failures)
    {
        string? referenceHash = null;
        string? referenceTransitions = null;
        foreach (RealtimeSimulationSpeed speed in new[]
                 {
                     RealtimeSimulationSpeed.Normal,
                     RealtimeSimulationSpeed.Fast,
                     RealtimeSimulationSpeed.VeryFast,
                 })
        {
            var slice = CreateRunningSlice();
            using var sliceLifetime = slice.FreeAfterSmoke();
            RealtimeSmokeLinePlan plan = slice.SmokeLinePlan;
            RealtimeSmokeBoundaryFacts facts = slice.SmokeBoundaryFacts;
            slice.AdvanceToForSmoke(plan.OrderMinute);
            foreach (RealtimeR2Intent intent in plan.Intents)
            {
                RequireIntent(slice.ApplyIntentForSmoke(intent),
                    $"{speed} authored line setup", failures);
            }
            int commands = slice.AcceptedCommandCount;
            slice.SetSpeedForSmoke(speed);
            long target = facts.Events.Max(item => item.EndMinute);
            _ = AdvanceToMinuteByFrames(slice, target, speed, failures);

            Check(slice.CurrentMinute == target,
                $"{speed} did not reach the authored outcome minute", failures);
            Check(slice.AcceptedCommandCount == commands,
                $"{speed} time flow created a click/command", failures);
            foreach (RealtimeSmokeEventBoundary expected in facts.Events)
            {
                Check(slice.EmittedTransitions.Any(item =>
                        item.Kind == RealtimeTransitionKind.EventStarted &&
                        item.Minute == expected.StartMinute &&
                        string.Equals(item.EventId, expected.EventId,
                            StringComparison.Ordinal)) &&
                      slice.EmittedTransitions.Any(item =>
                        item.Kind == RealtimeTransitionKind.EventCompleted &&
                        item.Minute == expected.EndMinute &&
                        string.Equals(item.EventId, expected.EventId,
                            StringComparison.Ordinal)),
                    $"{speed} lost authored event boundaries for {expected.EventId}",
                    failures);
            }
            ValidateThermalTransitions(slice.EmittedTransitions, facts, speed.ToString(), failures);

            string transitionFingerprint = Fingerprint(slice.EmittedTransitions);
            if (referenceHash is null)
            {
                referenceHash = slice.CanonicalStateSha256;
                referenceTransitions = transitionFingerprint;
            }
            else
            {
                Check(string.Equals(referenceHash, slice.CanonicalStateSha256,
                        StringComparison.Ordinal),
                    $"{speed} produced a different canonical authored outcome", failures);
                Check(string.Equals(referenceTransitions, transitionFingerprint,
                        StringComparison.Ordinal),
                    $"{speed} produced a different ordered transition stream", failures);
            }
        }
    }

    private static void ValidateCriticalPauseBoundary(ICollection<string> failures)
    {
        var slice = CreateRunningSlice();
        using var sliceLifetime = slice.FreeAfterSmoke();
        RealtimeSmokeLinePlan plan = slice.SmokeLinePlan;
        RealtimeSmokeThermalBoundary boundary = slice.SmokeBoundaryFacts.Thermal
            .Where(item => item.TripMinute.HasValue)
            .OrderBy(item => item.TripMinute)
            .ThenBy(item => item.AssetId, StringComparer.Ordinal)
            .First();
        long tripMinute = boundary.TripMinute!.Value;
        slice.AdvanceToForSmoke(plan.OrderMinute);
        foreach (RealtimeR2Intent intent in plan.Intents)
        {
            RequireIntent(slice.ApplyIntentForSmoke(intent),
                "critical boundary line setup", failures);
        }
        slice.SetSpeedForSmoke(RealtimeSimulationSpeed.VeryFast);
        _ = AdvanceToMinuteByFrames(
            slice,
            tripMinute - 1,
            RealtimeSimulationSpeed.VeryFast,
            failures);

        RealtimeR2FrameResult hitch = slice.InjectFramesForSmoke(60, 60);
        Check(slice.CurrentMinute == tripMinute,
            "a large frame crossed the first protective trip before auto-pause", failures);
        Check(slice.InteractionState.Simulation == RealtimeSimulationState.AutoPaused &&
              slice.InteractionState.PauseReason == RealtimePauseReason.CriticalIncident,
            "protective trip did not create the typed critical auto-pause", failures);
        Check(hitch.Transitions.Any(item =>
                item.Kind == RealtimeTransitionKind.ThermalProtectiveTrip &&
                item.Minute == tripMinute &&
                string.Equals(item.AssetId, boundary.AssetId, StringComparison.Ordinal)),
            "critical frame omitted the exact protective-trip transition", failures);
        Check(!hitch.Transitions.Any(item =>
                item.Kind == RealtimeTransitionKind.ThermalRecovered &&
                item.Minute > tripMinute),
            "critical frame emitted recovery before the player acknowledged auto-pause",
            failures);
        Check(hitch.RequestedFrameCount == 60 &&
              hitch.ConsumedFrameCount > 0 &&
              hitch.ConsumedFrameCount < hitch.RequestedFrameCount &&
              hitch.RetainedFrameDebt.Count == 1 &&
              hitch.RetainedFrameDebt[0] is
              {
                  FramesPerSecond: 60,
                  SpeedMultiplier: 4,
              } &&
              hitch.RetainedFrameDebt[0].FrameCount ==
                  hitch.RequestedFrameCount - hitch.ConsumedFrameCount &&
              slice.RetainedFrameDebt.SequenceEqual(hitch.RetainedFrameDebt),
            "critical auto-pause did not retain exact typed frame debt", failures);
    }

    private static void ValidatePresentationAuthority(ICollection<string> failures)
    {
        var slice = CreateRunningSlice();
        using var sliceLifetime = slice.FreeAfterSmoke();
        RealtimeSlicePresentation presentation = slice.LatestPresentation;
        RealtimeSmokeBoundaryFacts facts = slice.SmokeBoundaryFacts;
        Check(RealtimeStateCanonicalizer.StructuralEquals(
                  presentation.CoreSnapshot,
                  slice.CoreSnapshot) &&
              string.Equals(
                  RealtimeStateCanonicalizer.Sha256(presentation.CoreSnapshot),
                  slice.CanonicalStateSha256,
                  StringComparison.Ordinal),
            "presentation source snapshot is not structurally/canonically equal to Core",
            failures);
        Check(presentation.Revision == slice.PresentationRevision &&
              presentation.World.Minute == slice.CurrentMinute &&
              presentation.Rail.NowMinute == slice.CurrentMinute &&
              presentation.Hud.Pause.CurrentMinute == slice.CurrentMinute &&
              string.Equals(presentation.Hud.Clock, presentation.Rail.NowLabel,
                  StringComparison.Ordinal) &&
              string.Equals(presentation.Hud.Clock,
                  presentation.Hud.Pause.CurrentTimeLabel,
                  StringComparison.Ordinal),
            "world/HUD rail presentation revisions or minutes diverged", failures);

        Check(presentation.World.World.Nodes
                  .Select(item => (item.NodeId, item.Commissioned))
                  .SequenceEqual(slice.CoreSnapshot.Construction.World.Nodes
                      .Select(item => (item.NodeId, item.Commissioned))) &&
              presentation.World.World.Edges
                  .Select(item => (item.EdgeId, item.FromNodeId, item.ToNodeId,
                      item.Commissioned))
                  .SequenceEqual(slice.CoreSnapshot.Construction.World.Edges
                      .Select(item => (item.EdgeId, item.FromNodeId, item.ToNodeId,
                          item.Commissioned))),
            "world presentation lost Core node/edge IDs, topology, order, or commissioning",
            failures);
        RealtimeWorldAssetStatus[] statuses = presentation.World.AssetStatuses
            .OrderBy(item => item.AssetId, StringComparer.Ordinal)
            .ToArray();
        RealtimeThermalAssetSnapshot[] thermal = slice.CoreSnapshot.Thermal.Assets
            .OrderBy(item => item.AssetId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, RealtimeThermalAssetSnapshot> thermalById = thermal
            .ToDictionary(item => item.AssetId, StringComparer.Ordinal);
        (string AssetId, bool Commissioned)[] expectedWorldAssets =
            slice.CoreSnapshot.Construction.World.Nodes
                .Select(item => (item.NodeId, item.Commissioned))
                .Concat(slice.CoreSnapshot.Construction.World.Edges
                    .Select(item => (item.EdgeId, item.Commissioned)))
                .OrderBy(item => item.Item1, StringComparer.Ordinal)
                .ToArray();
        Check(statuses.Length == expectedWorldAssets.Length &&
              statuses.Select(item => item.AssetId).SequenceEqual(
                  expectedWorldAssets.Select(item => item.AssetId),
                  StringComparer.Ordinal) &&
              statuses.Zip(expectedWorldAssets).All(pair =>
              {
                  RealtimeThermalAssetSnapshot? coreThermal =
                      thermalById.GetValueOrDefault(pair.Second.AssetId);
                  return string.Equals(
                             pair.First.AssetId,
                             pair.Second.AssetId,
                             StringComparison.Ordinal) &&
                         pair.First.State == ExpectedWorldState(
                             pair.Second.Commissioned,
                             coreThermal) &&
                         pair.First.UsedKw == (coreThermal?.UsedKw ?? 0) &&
                         pair.First.ContinuousLimitKw ==
                             (coreThermal?.ContinuousKw ?? 0) &&
                         pair.First.EmergencyLimitKw ==
                             (coreThermal?.EmergencyKw ?? 0) &&
                         pair.First.EmergencyExposureMinutes ==
                             (coreThermal?.EmergencyExposureMinutes ?? 0) &&
                         pair.First.EmergencyExposureLimitMinutes ==
                             (coreThermal?.EmergencyExposureLimitMinutes ?? 0) &&
                         pair.First.AuthoredUnavailable ==
                             (coreThermal?.AuthoredUnavailable ?? false) &&
                         pair.First.ProtectiveOutage ==
                             (coreThermal?.ProtectiveOutage ?? false);
              }),
            "world presentation did not map every Core node/edge with exact " +
            "commissioning, thermal cause, state, limits, loading, or exposure",
            failures);

        string[] expectedToolIds = new[] { "TOOL:INSPECT" }
            .Concat(slice.CoreSnapshot.Chapter.Content.AvailableNodeClassIds
                .Select(id => $"NODE:{id}"))
            .Concat(slice.CoreSnapshot.Chapter.Content.AvailableLinePlans
                .Select(plan => $"LINE:{plan.LineClassId}:{plan.PoleClassId}"))
            .Append("TOOL:ANALYSIS")
            .ToArray();
        Check(presentation.BuildShelf.Tools.Select(item => item.Id)
                .SequenceEqual(expectedToolIds, StringComparer.Ordinal),
            "build shelf tool IDs/order diverged from authored Core availability",
            failures);

        RealtimeTimelineItemPresentation[] ordered = presentation.Rail.Items
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        Check(presentation.Rail.Items.SequenceEqual(ordered),
            "event rail did not preserve minute, Core priority, stable-ID order",
            failures);
        foreach (RealtimeSmokeEventBoundary expected in facts.Events)
        {
            RealtimeTimelineItemPresentation marker = presentation.Rail.Items.Single(item =>
                string.Equals(item.Id, expected.EventId, StringComparison.Ordinal));
            Check(marker.StartMinute == expected.StartMinute &&
                  marker.EndMinute == expected.EndMinute &&
                  marker.Priority == expected.Priority,
                $"rail marker {expected.EventId} erased Core time/priority", failures);
        }
        foreach (RealtimeSmokeThermalBoundary expected in facts.Thermal)
        {
            foreach (long minute in new[]
                     {
                         expected.EmergencyStartMinute,
                         expected.TripMinute,
                         expected.RecoveryMinute,
                     }.Where(value => value.HasValue).Select(value => value!.Value))
            {
                Check(presentation.Rail.Items.Any(item =>
                        item.Lane == RealtimeTimelineLane.ThermalProtection &&
                        item.StartMinute == minute &&
                        item.Id.Contains(expected.AssetId, StringComparison.Ordinal)),
                    $"rail omitted typed thermal marker {expected.AssetId}@{minute}",
                    failures);
            }
        }

        string selectedMarkerId = presentation.Rail.Items.First(item =>
            RealtimeSlicePresenter.ResolveTimelineTarget(
                slice.DisplayWorldForSmoke,
                slice.CoreSnapshot,
                item.Id).SubjectId is not null).Id;
        string beforeHash = slice.CanonicalStateSha256;
        int beforeCommands = slice.AcceptedCommandCount;
        slice.ChooseTimelineClusterForSmoke(new[] { selectedMarkerId });
        RealtimeTimelineTarget target = RealtimeSlicePresenter.ResolveTimelineTarget(
            slice.DisplayWorldForSmoke,
            slice.CoreSnapshot,
            selectedMarkerId);
        RealtimeSlicePresentation selected = slice.LatestPresentation;
        Check(string.Equals(beforeHash, slice.CanonicalStateSha256,
                  StringComparison.Ordinal) &&
              slice.AcceptedCommandCount == beforeCommands &&
              string.Equals(selected.Rail.SelectedItemId, selectedMarkerId,
                  StringComparison.Ordinal) &&
              string.Equals(selected.Interaction.SelectionId, target.SubjectId,
                  StringComparison.Ordinal) &&
              string.Equals(selected.Context.SubjectId, target.SubjectId,
                  StringComparison.Ordinal) &&
              string.Equals(selected.World.SelectedAssetId, target.MapSubjectId,
                  StringComparison.Ordinal),
            "selected Core marker did not project one subject consistently across surfaces",
            failures);
    }

    private static void ValidateFollowNowTimeline(ICollection<string> failures)
    {
        var slice = CreateRunningSlice();
        using var sliceLifetime = slice.FreeAfterSmoke();
        RealtimeTimelineItemPresentation selected = slice.LatestPresentation.Rail.Items
            .First(item => item.Visibility != RealtimeTimelineVisibility.Hidden);
        long initialMinute = slice.CurrentMinute;
        string initialHash = slice.CanonicalStateSha256;
        int initialCommands = slice.AcceptedCommandCount;

        slice.ChooseTimelineClusterForSmoke(new[] { selected.Id });
        Check(slice.InteractionState.TimelineAnchorMinute is null &&
              string.Equals(slice.InteractionState.TimelineSelectedItemId,
                  selected.Id, StringComparison.Ordinal) &&
              slice.LatestPresentation.Rail.HorizonStartMinute ==
                  Math.Max(0, slice.CurrentMinute - (6 * 60)) &&
              slice.LatestPresentation.Rail.HorizonEndMinute ==
                  slice.CurrentMinute + (24 * 60) &&
              string.Equals(initialHash, slice.CanonicalStateSha256,
                  StringComparison.Ordinal) &&
              slice.AcceptedCommandCount == initialCommands,
            "timeline selection froze its anchor or changed Core/journal", failures);

        string[] boundaryIds = slice.TimelineChooserFacts.VisibleOrderedItemIds.ToArray();
        slice.ChooseTimelineClusterForSmoke(new[] { boundaryIds[0] });
        long firstBoundaryRevision = slice.PresentationRevision;
        slice.NavigateTimelineForSmoke(RealtimeTimelineNavigation.PreviousEvent);
        Check(boundaryIds.Length > 1 &&
              string.Equals(
                  slice.TimelineChooserFacts.SelectedMarkerId,
                  boundaryIds[0],
                  StringComparison.Ordinal) &&
              slice.PresentationRevision == firstBoundaryRevision,
            "selected-first Previous did not preserve selection/revision as a true no-op",
            failures);
        slice.ChooseTimelineClusterForSmoke(new[] { boundaryIds[^1] });
        long lastBoundaryRevision = slice.PresentationRevision;
        slice.NavigateTimelineForSmoke(RealtimeTimelineNavigation.NextEvent);
        Check(string.Equals(
                  slice.TimelineChooserFacts.SelectedMarkerId,
                  boundaryIds[^1],
                  StringComparison.Ordinal) &&
              slice.PresentationRevision == lastBoundaryRevision,
            "selected-last Next did not preserve selection/revision as a true no-op",
            failures);
        slice.ChooseTimelineClusterForSmoke(new[] { selected.Id });

        _ = slice.InjectElapsedNanosecondsForSmoke(1_000_000_000);
        Check(slice.CurrentMinute == initialMinute + 1 &&
              slice.InteractionState.TimelineAnchorMinute is null &&
              string.Equals(slice.InteractionState.TimelineSelectedItemId,
                  selected.Id, StringComparison.Ordinal) &&
              slice.LatestPresentation.Rail.HorizonStartMinute ==
                  Math.Max(0, slice.CurrentMinute - (6 * 60)) &&
              slice.LatestPresentation.Rail.HorizonEndMinute ==
                  slice.CurrentMinute + (24 * 60) &&
              !string.Equals(initialHash, slice.CanonicalStateSha256,
                  StringComparison.Ordinal) &&
              slice.AcceptedCommandCount == initialCommands,
            "selected timeline did not follow the authoritative now cursor over a tick",
            failures);

        string beforePresetHash = slice.CanonicalStateSha256;
        slice.AdjustTimelineHorizonForSmoke(-1);
        Check(slice.InteractionState.TimelineHorizon ==
                  RealtimeTimelineHorizonPreset.SixHours &&
              slice.InteractionState.TimelineAnchorMinute is null &&
              string.Equals(slice.InteractionState.TimelineSelectedItemId,
                  selected.Id, StringComparison.Ordinal) &&
              slice.LatestPresentation.Rail.HorizonEndMinute ==
                  slice.CurrentMinute + (6 * 60) &&
              string.Equals(beforePresetHash, slice.CanonicalStateSha256,
                  StringComparison.Ordinal) &&
              slice.AcceptedCommandCount == initialCommands,
            "timeline preset change stopped following now or changed Core/journal", failures);

        long beforePresetTick = slice.CurrentMinute;
        _ = slice.InjectElapsedNanosecondsForSmoke(1_000_000_000);
        Check(slice.CurrentMinute == beforePresetTick + 1 &&
              slice.InteractionState.TimelineAnchorMinute is null &&
              slice.LatestPresentation.Rail.HorizonEndMinute ==
                  slice.CurrentMinute + (6 * 60) &&
              string.Equals(slice.InteractionState.TimelineSelectedItemId,
                  selected.Id, StringComparison.Ordinal),
            "preset timeline horizon froze after the next authoritative tick", failures);

        slice.NavigateTimelineForSmoke(RealtimeTimelineNavigation.Home);
        Check(slice.InteractionState.TimelineAnchorMinute is null &&
              slice.InteractionState.TimelineSelectedItemId is null &&
              slice.InteractionState.SelectionId is null &&
              slice.LatestPresentation.Rail.HorizonStartMinute ==
                  Math.Max(0, slice.CurrentMinute - (6 * 60)),
            "timeline Home did not clear selection while preserving follow-now", failures);
        long beforeHomeTick = slice.CurrentMinute;
        _ = slice.InjectElapsedNanosecondsForSmoke(1_000_000_000);
        Check(slice.CurrentMinute == beforeHomeTick + 1 &&
              slice.InteractionState.TimelineAnchorMinute is null &&
              slice.LatestPresentation.Rail.HorizonStartMinute ==
                  Math.Max(0, slice.CurrentMinute - (6 * 60)) &&
              slice.LatestPresentation.Rail.HorizonEndMinute ==
                  slice.CurrentMinute + (6 * 60) &&
              slice.AcceptedCommandCount == initialCommands,
            "timeline Home anchor did not continue following now over a tick", failures);

        string beforeSevenDayHash = slice.CanonicalStateSha256;
        slice.AdjustTimelineHorizonForSmoke(1);
        Check(slice.InteractionState.TimelineHorizon ==
                  RealtimeTimelineHorizonPreset.TwentyFourHours &&
              slice.LatestPresentation.Rail.HorizonEndMinute ==
                  slice.CurrentMinute + (24 * 60),
            "timeline did not restore the authoritative 24-hour preset before 7-day",
            failures);
        slice.AdjustTimelineHorizonForSmoke(1);
        long sevenDayRequest = RealtimeSlicePresenter.RequiredForecastHorizonMinutes(
            slice.CurrentMinute,
            slice.InteractionState.TimelineAnchorMinute,
            RealtimeTimelineHorizonPreset.SevenDays);
        RealtimeForecastSnapshot authoritativeSevenDay =
            slice.ForecastForHorizonForSmoke(sevenDayRequest);
        RealtimeSlicePresentation sevenDay = slice.LatestPresentation;
        bool hasBeyondTwentyFourHours = authoritativeSevenDay.Events.Any(item =>
            item.StartMinute > slice.CurrentMinute + (24 * 60));
        bool everyBeyondTwentyFourHourEventRendered = authoritativeSevenDay.Events
            .Where(item => item.StartMinute > slice.CurrentMinute + (24 * 60))
            .All(item => sevenDay.Rail.Items.Any(marker => string.Equals(
                marker.Id,
                item.EventId,
                StringComparison.Ordinal)));
        Check(slice.InteractionState.TimelineHorizon ==
                  RealtimeTimelineHorizonPreset.SevenDays &&
              slice.InteractionState.TimelineAnchorMinute is null &&
              sevenDayRequest == 7 * 24 * 60 &&
              sevenDay.Rail.HorizonEndMinute ==
                  slice.CurrentMinute + sevenDayRequest &&
              string.Equals(
                  sevenDay.Rail.HorizonLabel,
                  "앞으로 7일 · 지난 6시간",
                  StringComparison.Ordinal) &&
              ForecastFingerprint(sevenDay.BaseForecast) ==
                  ForecastFingerprint(authoritativeSevenDay) &&
              (!hasBeyondTwentyFourHours || everyBeyondTwentyFourHourEventRendered) &&
              string.Equals(
                  beforeSevenDayHash,
                  slice.CanonicalStateSha256,
                  StringComparison.Ordinal) &&
              slice.AcceptedCommandCount == initialCommands,
            "7-day preset did not request/present the exact 10,080-minute Core " +
            "forecast authority or render its beyond-24h events",
            failures);

        var endedNavigation = CreateRunningSlice();
        using var endedNavigationLifetime = endedNavigation.FreeAfterSmoke();
        long finalEventEnd = endedNavigation.SmokeBoundaryFacts.Events.Max(item =>
            item.EndMinute);
        endedNavigation.AdvanceToForSmoke(finalEventEnd);
        if (endedNavigation.InteractionState.ActiveModalId is string resultModalId)
        {
            RequireIntent(endedNavigation.ApplyIntentForSmoke(
                    RealtimeR2Intent.CloseModal(resultModalId)),
                "ended navigation result close", failures,
                coreCommandExpected: false);
        }
        endedNavigation.NavigateTimelineForSmoke(RealtimeTimelineNavigation.Home);
        long noFutureRevision = endedNavigation.PresentationRevision;
        string noFutureHash = endedNavigation.CanonicalStateSha256;
        int noFutureCommands = endedNavigation.AcceptedCommandCount;
        endedNavigation.NavigateTimelineForSmoke(RealtimeTimelineNavigation.NextEvent);
        Check(endedNavigation.InteractionState.TimelineSelectedItemId is null &&
              endedNavigation.InteractionState.SelectionId is null &&
              endedNavigation.PresentationRevision == noFutureRevision &&
              string.Equals(
                  endedNavigation.CanonicalStateSha256,
                  noFutureHash,
                  StringComparison.Ordinal) &&
              endedNavigation.AcceptedCommandCount == noFutureCommands,
            "no-selection Next with no strict-future target cleared state or revised " +
            "instead of remaining a true no-op",
            failures);
    }

    private static void ValidatePlayerFacingCopy(ICollection<string> failures)
    {
        var slice = CreateRunningSlice();
        using var sliceLifetime = slice.FreeAfterSmoke();
        RealtimeSlicePresentation presentation = slice.LatestPresentation;
        string expectedCash = string.Create(
            CultureInfo.InvariantCulture,
            $"운영 자금 {slice.CoreSnapshot.CashUnit:N0}만 원");
        Check(!string.IsNullOrWhiteSpace(presentation.Hud.Objective) &&
              string.Equals(presentation.Hud.Cash, expectedCash,
                  StringComparison.Ordinal),
            "HUD lost its authored objective or player cash unit", failures);
        Check(presentation.BuildShelf.Tools
                .Where(item => item.Id.StartsWith("NODE:", StringComparison.Ordinal))
                .All(item => item.Description.Contains("만 원", StringComparison.Ordinal)),
            "node tool cost copy does not use the player-facing 만 원 unit", failures);

        string[] forbidden = { "병목 시험", "원자적", "스냅샷", "투영" };
        string[] visibleCopy = PlayerFacingStrings(presentation).ToArray();
        foreach (string term in forbidden)
        {
            Check(visibleCopy.All(value => !value.Contains(term, StringComparison.Ordinal)),
                $"player-facing presentation leaked internal term '{term}'", failures);
        }
        Check(presentation.Rail.Items
                .Where(item => RealtimeSlicePresenter.ResolveTimelineTarget(
                    slice.DisplayWorldForSmoke,
                    slice.CoreSnapshot,
                    item.Id).Kind == RealtimeTimelineTargetKind.Event)
                .All(item => !item.Title.Contains("시험", StringComparison.Ordinal) &&
                    item.Title.Contains("전력 수요 증가", StringComparison.Ordinal)),
            "authored bottleneck fixture names were not converted to player event copy",
            failures);
    }

    private static void ValidateCompletedHistoryAndThermalContext(
        ICollection<string> failures)
    {
        var thermalSlice = CreateRunningSlice();
        using var thermalLifetime = thermalSlice.FreeAfterSmoke();
        RealtimeTimelineItemPresentation thermal = thermalSlice.LatestPresentation.Rail.Items
            .First(item => RealtimeSlicePresenter.ResolveTimelineTarget(
                thermalSlice.DisplayWorldForSmoke,
                thermalSlice.CoreSnapshot,
                item.Id).Kind == RealtimeTimelineTargetKind.ThermalAsset);
        RealtimeTimelineItemPresentation owningEvent =
            thermalSlice.LatestPresentation.Rail.Items.First(item =>
                RealtimeSlicePresenter.ResolveTimelineTarget(
                    thermalSlice.DisplayWorldForSmoke,
                    thermalSlice.CoreSnapshot,
                    item.Id).Kind == RealtimeTimelineTargetKind.Event &&
                thermal.Description.StartsWith($"{item.Title} 예상", StringComparison.Ordinal));
        thermalSlice.ChooseTimelineClusterForSmoke(new[] { thermal.Id });
        RealtimeTimelineTarget target = RealtimeSlicePresenter.ResolveTimelineTarget(
            thermalSlice.DisplayWorldForSmoke,
            thermalSlice.CoreSnapshot,
            thermal.Id);
        RealtimeContextDockPresentation context = thermalSlice.LatestPresentation.Context;
        Check(string.Equals(context.SubjectId, thermal.Id, StringComparison.Ordinal) &&
              context.Visible &&
              context.Eyebrow.Contains(owningEvent.Title, StringComparison.Ordinal) &&
              context.Eyebrow.Contains("열 보호 예상", StringComparison.Ordinal) &&
              context.Sections.Any(item => item.Heading == "예상 시각" &&
                  string.Equals(item.Body, thermal.TimeLabel, StringComparison.Ordinal)) &&
              context.Sections.Any(item => item.Heading == "예상 변화") &&
              context.Sections.Any(item => item.Heading == "현재 상태") &&
              string.Equals(thermalSlice.LatestPresentation.World.SelectedAssetId,
                  target.MapSubjectId, StringComparison.Ordinal),
            "thermal marker context lost its owning event, exact time, current state, or map asset",
            failures);

        var historySlice = CreateRunningSlice();
        using var historyLifetime = historySlice.FreeAfterSmoke();
        long firstEnd = historySlice.SmokeBoundaryFacts.Events
            .OrderBy(item => item.EndMinute)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .First().EndMinute;
        historySlice.AdvanceToForSmoke(firstEnd);
        RealtimeTimelineItemPresentation[] completed = historySlice.LatestPresentation.Rail.Items
            .Where(item => item.Visibility == RealtimeTimelineVisibility.Completed)
            .ToArray();
        Check(completed.Length > 0 && completed.All(item =>
                !item.IsCurrent &&
                item.EndMinute <= historySlice.CurrentMinute &&
                item.Description.Contains("종료", StringComparison.Ordinal) &&
                item.SeverityLabel.Contains("완료", StringComparison.Ordinal)) &&
              historySlice.LatestPresentation.Rail.HorizonStartMinute ==
                  Math.Max(0, historySlice.CurrentMinute - (6 * 60)) &&
              historySlice.LatestPresentation.Rail.HorizonLabel.Contains(
                  "지난 6시간", StringComparison.Ordinal),
            "completed authored outcome did not enter bounded recent history", failures);
        if (completed.Length > 0)
        {
            historySlice.ChooseTimelineClusterForSmoke(new[] { completed[0].Id });
            Check(historySlice.LatestPresentation.Context.Visible &&
                  historySlice.LatestPresentation.Context.Eyebrow == "최근 운영 기록" &&
                  historySlice.LatestPresentation.Context.Sections.Any(item =>
                      item.Heading == "운영 시각"),
                "completed history marker did not open its factual recent-operation context",
                failures);
        }
    }

    private static void ValidateThermalProtectionCopy(ICollection<string> failures)
    {
        var slice = CreateRunningSlice();
        using var sliceLifetime = slice.FreeAfterSmoke();
        RealtimeCampaignSnapshot baseline = slice.CoreSnapshot;

        RealtimeThermalAssetSnapshot NodeAsset(SpatialNodeKind kind) =>
            baseline.Thermal.Assets
                .Where(item => item.AssetKind == ThermalAssetKind.Node)
                .First(item =>
                {
                    SpatialNodeDefinition node = baseline.Construction.World.Nodes.Single(
                        candidate => string.Equals(
                            candidate.NodeId,
                            item.AssetId,
                            StringComparison.Ordinal));
                    return baseline.Construction.World.NodeClasses.Single(candidate =>
                        string.Equals(
                            candidate.ClassId,
                            node.ClassId,
                            StringComparison.Ordinal)).Kind == kind;
                });

        (string KindLabel, RealtimeThermalAssetSnapshot Asset)[] representatives =
        [
            ("선로 도체", baseline.Thermal.Assets.First(item =>
                item.AssetKind == ThermalAssetKind.Edge)),
            ("전신주 접속부", NodeAsset(SpatialNodeKind.Pole)),
            ("변전소 주기기", NodeAsset(SpatialNodeKind.Substation)),
        ];
        foreach ((string kindLabel, RealtimeThermalAssetSnapshot source) in representatives)
        {
            ThermalProtectionDefinition protection = slice.RealtimeWorldForSmoke.ProtectionFor(
                source.AssetKind,
                source.ClassId);
            foreach (ThermalOperatingState state in new[]
                     {
                         ThermalOperatingState.Continuous,
                         ThermalOperatingState.Emergency,
                         ThermalOperatingState.ProtectiveOutage,
                     })
            {
                long exposure = state switch
                {
                    ThermalOperatingState.Continuous =>
                        Math.Min(3L, protection.EmergencyExposureLimitMinutes),
                    ThermalOperatingState.Emergency =>
                        Math.Max(0L, protection.EmergencyExposureLimitMinutes - 2L),
                    ThermalOperatingState.ProtectiveOutage =>
                        protection.EmergencyExposureLimitMinutes,
                    _ => throw new ArgumentOutOfRangeException(nameof(state)),
                };
                long outageRemaining = state == ThermalOperatingState.ProtectiveOutage
                    ? Math.Max(1L, protection.ProtectiveOutageMinutes / 2L)
                    : 0L;
                var asset = source with
                {
                    EmergencyExposureMinutes = exposure,
                    EmergencyExposureLimitMinutes =
                        protection.EmergencyExposureLimitMinutes,
                    ProtectiveOutage = state == ThermalOperatingState.ProtectiveOutage,
                    ProtectiveOutageUntilMinute =
                        state == ThermalOperatingState.ProtectiveOutage
                            ? baseline.Minute + outageRemaining
                            : null,
                    State = state,
                };
                var thermal = baseline.Thermal with
                {
                    Assets = baseline.Thermal.Assets.Select(item => string.Equals(
                            item.AssetId,
                            source.AssetId,
                            StringComparison.Ordinal)
                        ? asset
                        : item).ToArray(),
                };
                var snapshot = baseline with { Thermal = thermal };
                RealtimeInteractionState interaction = slice.InteractionState with
                {
                    Tool = RealtimeTool.Inspect,
                    Surface = RealtimeSurface.Inspector,
                    SelectionId = asset.AssetId,
                    TimelineSelectedItemId = null,
                };
                RealtimeSlicePresentation presentation = slice.PresentSnapshotForSmoke(
                    snapshot,
                    interaction);
                long emergencyRemaining = Math.Max(
                    0L,
                    (long)protection.EmergencyExposureLimitMinutes - exposure);
                string expectedSummary = state switch
                {
                    ThermalOperatingState.Continuous =>
                        $"비상 운전 여유 {emergencyRemaining}분 · " +
                        $"이후 {protection.ProtectiveOutageMinutes}분 보호정지",
                    ThermalOperatingState.Emergency =>
                        $"보호정지까지 {emergencyRemaining}분 · " +
                        $"이후 {protection.ProtectiveOutageMinutes}분 보호정지",
                    ThermalOperatingState.ProtectiveOutage =>
                        $"복귀까지 {outageRemaining}분 · " +
                        $"보호정지 기준 {protection.ProtectiveOutageMinutes}분",
                    _ => throw new ArgumentOutOfRangeException(nameof(state)),
                };
                string expectedDetail = string.Format(
                    CultureInfo.InvariantCulture,
                    "사용 {0:N0} kW\n연속 한계 {1:N0} kW\n비상 한계 {2:N0} kW\n" +
                    "비상 노출 {3}/{4}분\n{5}",
                    asset.UsedKw,
                    asset.ContinuousKw,
                    asset.EmergencyKw,
                    exposure,
                    protection.EmergencyExposureLimitMinutes,
                    expectedSummary);
                RealtimeWorldAssetStatus worldStatus =
                    presentation.World.AssetStatuses.Single(item => string.Equals(
                        item.AssetId,
                        asset.AssetId,
                        StringComparison.Ordinal));
                Check(string.Equals(presentation.Context.SubjectId, asset.AssetId,
                          StringComparison.Ordinal) &&
                      string.Equals(presentation.Context.Eyebrow, kindLabel,
                          StringComparison.Ordinal) &&
                      presentation.Context.Sections.Single(item =>
                          item.Heading == "보호").Body == expectedSummary &&
                      presentation.Context.Details.Single(item =>
                          item.Tab == RealtimeContextDetailTab.Thermal).Body == expectedDetail &&
                      worldStatus.State == ExpectedWorldState(state),
                    $"{kindLabel}/{state} lost exact allowance/cooldown copy or world state",
                    failures);
            }
        }
    }

    private static void ValidateDraftMenuCancelPolicy(ICollection<string> failures)
    {
        var slice = CreateRunningSlice();
        using var sliceLifetime = slice.FreeAfterSmoke();
        RealtimeSmokeLinePlan plan = slice.SmokeLinePlan;
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildLine,
                    $"LINE:{plan.LineClassId}:{plan.PoleClassId}")),
            "draft menu tool", failures, coreCommandExpected: false);
        string noDraftHash = slice.CanonicalStateSha256;
        int noDraftCommands = slice.AcceptedCommandCount;
        Check(!slice.LatestPresentation.Hud.BuildModeActive &&
              slice.CoreSnapshot.Construction.LineDraft is null &&
              slice.InteractionState.Tool == RealtimeTool.BuildLine &&
              slice.InteractionState.Surface == RealtimeSurface.Drawer,
            "selected construction tool without a Core draft claimed cancel mode",
            failures);
        slice.RequestShortcutForSmoke(RealtimeInputCommand.ToggleBuildShelf);
        Check(slice.InteractionState.Tool == RealtimeTool.Inspect &&
              slice.InteractionState.Surface == RealtimeSurface.World &&
              slice.CoreSnapshot.Construction.LineDraft is null &&
              slice.AcceptedCommandCount == noDraftCommands &&
              string.Equals(
                  slice.CanonicalStateSha256,
                  noDraftHash,
                  StringComparison.Ordinal),
            "one no-draft B press did not truthfully close the selected tool/drawer",
            failures);
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildLine,
                    $"LINE:{plan.LineClassId}:{plan.PoleClassId}")),
            "draft menu tool restore", failures, coreCommandExpected: false);
        RequireIntent(slice.ApplyIntentForSmoke(plan.Intents[0]),
            "draft menu line start", failures);
        RequireIntent(slice.ApplyIntentForSmoke(RealtimeR2Intent.Select(
                slice.SmokeBoundaryFacts.Events[0].EventId)),
            "draft menu competing inspector", failures,
            coreCommandExpected: false);
        string draftHash = slice.CanonicalStateSha256;
        int draftCommands = slice.AcceptedCommandCount;

        slice.RequestShortcutForSmoke(RealtimeInputCommand.ToggleBuildShelf);
        Check(slice.CoreSnapshot.Construction.LineDraft is not null &&
              slice.InteractionState.Tool == RealtimeTool.BuildLine &&
              slice.InteractionState.Surface == RealtimeSurface.Drawer &&
              slice.AcceptedCommandCount == draftCommands &&
              string.Equals(slice.CanonicalStateSha256, draftHash,
                  StringComparison.Ordinal) &&
              slice.LatestPresentation.BuildShelf.Guidance.Contains(
                  "초안을 모두 취소하려면 B 또는 Esc를 한 번 더 누르세요.",
                  StringComparison.Ordinal),
            "draft HUD cancel did not enter the explicit first confirmation without " +
            "claiming an impossible Inspect transition", failures);

        slice.RequestShortcutForSmoke(RealtimeInputCommand.ToggleBuildShelf);
        Check(slice.CoreSnapshot.Construction.LineDraft is null &&
              slice.AcceptedCommandCount == draftCommands + 1 &&
              !string.Equals(slice.CanonicalStateSha256, draftHash,
                  StringComparison.Ordinal),
            "second draft HUD cancel did not dispatch exactly one authoritative cancel",
            failures);
    }

    private static void ValidateEndedReadOnlyShell(ICollection<string> failures)
    {
        var slice = CreateRunningSlice();
        using var sliceLifetime = slice.FreeAfterSmoke();
        RealtimeSmokeLinePlan plan = slice.SmokeLinePlan;
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildLine,
                    $"LINE:{plan.LineClassId}:{plan.PoleClassId}")),
            "ended shell line tool", failures, coreCommandExpected: false);
        RequireIntent(slice.ApplyIntentForSmoke(plan.Intents[0]),
            "ended shell line start", failures);
        RequireIntent(slice.ApplyIntentForSmoke(plan.Intents[1]),
            "ended shell line finish", failures);
        string endedHash = slice.CanonicalStateSha256;
        int endedCommands = slice.AcceptedCommandCount;
        RealtimeSlicePresentation completeSnapshotDefense =
            slice.PresentSnapshotForSmoke(
                slice.CoreSnapshot with { CampaignComplete = true },
                slice.InteractionState);
        Check(!completeSnapshotDefense.World.PlacementMode &&
              !completeSnapshotDefense.Hud.BuildModeActive &&
              completeSnapshotDefense.BuildShelf.Tools
                  .Where(item => item.Id.StartsWith("NODE:", StringComparison.Ordinal) ||
                                 item.Id.StartsWith("LINE:", StringComparison.Ordinal))
                  .All(item => !item.Enabled && string.Equals(
                      item.Description,
                      RealtimeInteractionReducer.CampaignEndedReadOnlyReason,
                      StringComparison.Ordinal)) &&
              completeSnapshotDefense.ActionDock.PrimaryAction is { Enabled: false },
            "CampaignComplete snapshot retained a writable construction illusion before " +
            "interaction-state alignment", failures);

        slice.EnterCampaignEndedForSmoke();
        RealtimeSlicePresentation ended = slice.LatestPresentation;
        RealtimeBuildToolPresentation[] constructionTools = ended.BuildShelf.Tools
            .Where(item => item.Id.StartsWith("NODE:", StringComparison.Ordinal) ||
                           item.Id.StartsWith("LINE:", StringComparison.Ordinal))
            .ToArray();
        Check(slice.InteractionState is
              {
                  Simulation: RealtimeSimulationState.Ended,
                  Tool: RealtimeTool.Inspect,
                  Surface: RealtimeSurface.World,
                  SelectedBuildToolId: null,
              } &&
              !ended.World.PlacementMode &&
              !ended.World.AnalysisVisible,
            "campaign completion retained a writable construction/map mode", failures);
        Check(constructionTools.Length > 0 && constructionTools.All(item =>
                  !item.Enabled && !item.Selected && string.Equals(
                      item.Description,
                      RealtimeInteractionReducer.CampaignEndedReadOnlyReason,
                      StringComparison.Ordinal)),
            "ended build entry was not disabled with the exact visible/AX reason",
            failures);
        Check(ended.ActionDock is
              {
                  Visible: true,
                  PrimaryAction.Enabled: false,
              } &&
              string.Equals(
                  ended.ActionDock.Detail,
                  RealtimeInteractionReducer.CampaignEndedReadOnlyReason,
                  StringComparison.Ordinal) &&
              string.Equals(
                  ended.ActionDock.PrimaryAction!.Description,
                  RealtimeInteractionReducer.CampaignEndedReadOnlyReason,
                  StringComparison.Ordinal),
            "ended draft action did not become a truthful disabled read-only surface",
            failures);

        RealtimeR2IntentResult order = slice.ApplyIntentForSmoke(
            RealtimeR2Intent.OrderLine());
        RealtimeR2IntentResult buildEntry = slice.ApplyIntentForSmoke(
            RealtimeR2Intent.SelectBuildTool(
                RealtimeTool.BuildLine,
                $"LINE:{plan.LineClassId}:{plan.PoleClassId}"));
        RealtimeR2IntentResult promiseWrite = slice.ApplyIntentForSmoke(
            new RealtimeR2Intent(
                RealtimeR2IntentKind.SetPromiseDecision,
                PromiseDecision: CommercialPromiseDecision.Keep));
        Check(!order.Accepted && order.CoreCommandResult is null &&
              string.Equals(
                  order.Error,
                  RealtimeInteractionReducer.CampaignEndedReadOnlyReason,
                  StringComparison.Ordinal) &&
              !buildEntry.Accepted && buildEntry.CoreCommandResult is null &&
              string.Equals(
                  buildEntry.Error,
                  RealtimeInteractionReducer.CampaignEndedReadOnlyReason,
                  StringComparison.Ordinal) &&
              !promiseWrite.Accepted && promiseWrite.CoreCommandResult is null &&
              string.Equals(
                  promiseWrite.Error,
                  RealtimeInteractionReducer.CampaignEndedReadOnlyReason,
                  StringComparison.Ordinal) &&
              slice.AcceptedCommandCount == endedCommands &&
              string.Equals(slice.CanonicalStateSha256, endedHash,
                  StringComparison.Ordinal),
            "ended construction entry/action crossed ApplyCommand or changed Core",
            failures);

        RequireIntent(slice.ApplyIntentForSmoke(new RealtimeR2Intent(
                RealtimeR2IntentKind.OpenSurface,
                Surface: RealtimeSurface.Drawer)),
            "ended read-only drawer", failures, coreCommandExpected: false);
        Check(slice.LatestPresentation.BuildShelf.Visible &&
              string.Equals(
                  slice.LatestPresentation.BuildShelf.Guidance,
                  RealtimeInteractionReducer.CampaignEndedReadOnlyReason,
                  StringComparison.Ordinal),
            "ended tool drawer did not expose the exact read-only reason", failures);
    }

    private static void ValidateAnalysisSurfacePolicy(ICollection<string> failures)
    {
        var slice = CreateRunningSlice();
        using var sliceLifetime = slice.FreeAfterSmoke();
        string hash = slice.CanonicalStateSha256;
        int commands = slice.AcceptedCommandCount;

        slice.RequestBuildToolForSmoke("TOOL:ANALYSIS");
        RealtimeSlicePresentation mouseOn = slice.LatestPresentation;
        RealtimeBuildToolPresentation mouseTool = mouseOn.BuildShelf.Tools.Single(item =>
            string.Equals(item.Id, "TOOL:ANALYSIS", StringComparison.Ordinal));
        Check(slice.InteractionState.Tool == RealtimeTool.Analysis &&
              slice.InteractionState.Surface == RealtimeSurface.Drawer &&
              mouseOn.World.AnalysisVisible && mouseOn.BuildShelf.Visible &&
              mouseTool.Enabled && mouseTool.Selected &&
              string.Equals(mouseTool.Label, "망 분석 켜짐", StringComparison.Ordinal) &&
              mouseTool.Description.Contains("망 분석 켜짐", StringComparison.Ordinal) &&
              mouseOn.BuildShelf.Guidance.Contains("망 분석 켜짐",
                  StringComparison.Ordinal),
            "mouse analysis activation lost its persistent visible/AX active state",
            failures);

        slice.RequestBuildToolForSmoke("TOOL:ANALYSIS");
        Check(slice.InteractionState.Tool == RealtimeTool.Inspect &&
              slice.InteractionState.Surface == RealtimeSurface.Drawer &&
              !slice.LatestPresentation.World.AnalysisVisible,
            "mouse analysis re-click did not toggle the shared surface off", failures);

        slice.RequestShortcutForSmoke(RealtimeInputCommand.ToggleAnalysis);
        RealtimeSlicePresentation keyboardOn = slice.LatestPresentation;
        RealtimeBuildToolPresentation keyboardTool = keyboardOn.BuildShelf.Tools.Single(item =>
            string.Equals(item.Id, "TOOL:ANALYSIS", StringComparison.Ordinal));
        Check(slice.InteractionState.Tool == RealtimeTool.Analysis &&
              slice.InteractionState.Surface == RealtimeSurface.Drawer &&
              keyboardOn.World.AnalysisVisible && keyboardOn.BuildShelf.Visible &&
              keyboardTool.Enabled && keyboardTool.Selected &&
              string.Equals(keyboardTool.Label, "망 분석 켜짐", StringComparison.Ordinal) &&
              string.Equals(keyboardTool.Description, mouseTool.Description,
                  StringComparison.Ordinal) &&
              string.Equals(keyboardOn.BuildShelf.Guidance,
                  mouseOn.BuildShelf.Guidance, StringComparison.Ordinal),
            "keyboard analysis activation diverged from the mouse surface policy",
            failures);
        RequireIntent(slice.ApplyIntentForSmoke(new RealtimeR2Intent(
                RealtimeR2IntentKind.CloseSurface,
                Surface: RealtimeSurface.Drawer)),
            "close analysis drawer", failures, coreCommandExpected: false);
        Check(slice.InteractionState.Tool == RealtimeTool.Inspect &&
              slice.InteractionState.Surface == RealtimeSurface.World &&
              !slice.LatestPresentation.World.AnalysisVisible &&
              !slice.LatestPresentation.BuildShelf.Visible,
            "closing the analysis surface left an invisible active Analysis tool",
            failures);
        Check(slice.AcceptedCommandCount == commands &&
              string.Equals(slice.CanonicalStateSha256, hash, StringComparison.Ordinal),
            "analysis surface toggles mutated authoritative Core state", failures);
    }

    private static void ValidateComparisonDraftForecast(ICollection<string> failures)
    {
        var slice = CreateRunningSlice();
        using var sliceLifetime = slice.FreeAfterSmoke();
        RealtimeSmokeLinePlan plan = slice.SmokeLinePlan;
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildLine,
                    $"LINE:{plan.LineClassId}:{plan.PoleClassId}")),
            "comparison line tool", failures, coreCommandExpected: false);
        RequireIntent(slice.ApplyIntentForSmoke(plan.Intents[0]),
            "comparison line start", failures);
        RequireIntent(slice.ApplyIntentForSmoke(plan.Intents[1]),
            "comparison line finish", failures);
        string draftHash = slice.CanonicalStateSha256;
        int draftCommands = slice.AcceptedCommandCount;

        RealtimeComparisonDraftForecast coreComparison =
            slice.ComparisonDraftForecastForSmoke;
        RealtimeSlicePresentation presentation = slice.LatestPresentation;
        Check(coreComparison is
              {
                  Available: true,
                  DraftKind: ConstructionKind.Line,
                  Forecast: not null,
              } &&
              presentation.ComparisonDraftForecast is
              {
                  Available: true,
                  DraftKind: ConstructionKind.Line,
                  Forecast: not null,
              },
            "closed Core draft did not reach Presenter through the typed comparison API",
            failures);
        if (coreComparison.Forecast is null ||
            presentation.ComparisonDraftForecast.Forecast is null)
        {
            return;
        }

        RealtimeTimelineItemPresentation[] eventMarkers = presentation.Rail.Items
            .Where(item => item.Id.StartsWith(
                "DRAFT_FORECAST:", StringComparison.Ordinal))
            .ToArray();
        RealtimeTimelineItemPresentation[] thermalMarkers = presentation.Rail.Items
            .Where(item => item.Id.StartsWith(
                "DRAFT_THERMAL:", StringComparison.Ordinal))
            .ToArray();
        RealtimeTimelineItemPresentation[] draftCompletionMarkers =
            presentation.Rail.Items
                .Where(item => string.Equals(
                    item.Id,
                    "DRAFT_CONSTRUCTION",
                    StringComparison.Ordinal))
                .ToArray();
        int expectedThermalMarkers = coreComparison.Forecast.Events.Sum(item =>
            item.TemporalProjection.Transitions.Count);
        Check(eventMarkers.Length == coreComparison.Forecast.Events.Count &&
              thermalMarkers.Length == expectedThermalMarkers &&
              expectedThermalMarkers > 0 &&
              draftCompletionMarkers.Length == 1 &&
              draftCompletionMarkers[0].SourceKind ==
                  RealtimeTimelineSourceKind.Draft &&
              string.Equals(draftCompletionMarkers[0].SourceGlyph, "◇",
                  StringComparison.Ordinal) &&
              eventMarkers.Concat(thermalMarkers).All(item =>
                  string.Equals(item.ShortLabel, "현재 초안 기준 예상",
                      StringComparison.Ordinal) &&
                  item.Title.StartsWith("현재 초안 기준 예상 · ",
                      StringComparison.Ordinal) &&
                  item.Description.StartsWith("현재 초안 기준 예상 · ",
                      StringComparison.Ordinal) &&
                  item.SourceKind == RealtimeTimelineSourceKind.Draft),
            "typed comparison events/thermal transitions were not rendered with the exact " +
            "draft label", failures);

        foreach (RealtimeForecastEvent forecast in coreComparison.Forecast.Events)
        {
            RealtimeTimelineItemPresentation marker = eventMarkers.Single(item =>
                string.Equals(
                    item.Id,
                    $"DRAFT_FORECAST:{forecast.EventId}",
                    StringComparison.Ordinal));
            Check(marker.StartMinute == forecast.StartMinute &&
                  marker.EndMinute == forecast.EndMinute &&
                  marker.IsCurrent ==
                      (forecast.Status == RealtimeForecastStatus.Active),
                $"comparison event {forecast.EventId} diverged from Core timing/status",
                failures);
            RealtimeTimelineTarget target = RealtimeSlicePresenter.ResolveTimelineTarget(
                slice.DisplayWorldForSmoke,
                slice.CoreSnapshot,
                presentation.ComparisonDraftForecast,
                marker.Id);
            Check(target.Kind == RealtimeTimelineTargetKind.Event &&
                  string.Equals(target.SubjectId, marker.Id, StringComparison.Ordinal),
                $"comparison event {forecast.EventId} lost its typed selection target",
                failures);
        }
        foreach (RealtimeForecastEvent forecast in coreComparison.Forecast.Events)
        foreach (RealtimeThermalTransition transition in
                 forecast.TemporalProjection.Transitions)
        {
            string expectedId =
                $"DRAFT_THERMAL:{forecast.EventId}:{transition.Minute}:" +
                $"{transition.Kind}:{transition.AssetId}";
            RealtimeTimelineItemPresentation marker = thermalMarkers.Single(item =>
                string.Equals(item.Id, expectedId, StringComparison.Ordinal));
            RealtimeTimelineTarget target = RealtimeSlicePresenter.ResolveTimelineTarget(
                slice.DisplayWorldForSmoke,
                slice.CoreSnapshot,
                presentation.ComparisonDraftForecast,
                marker.Id);
            Check(marker.StartMinute == transition.Minute &&
                  target.Kind == RealtimeTimelineTargetKind.ThermalAsset &&
                  string.Equals(target.MapSubjectId, transition.AssetId,
                      StringComparison.Ordinal),
                $"comparison thermal marker {expectedId} diverged from Core", failures);
        }

        RealtimeTimelineItemPresentation selected = eventMarkers[0];
        slice.ChooseTimelineClusterForSmoke(new[] { selected.Id });
        Check(string.Equals(
                  slice.LatestPresentation.Context.SubjectId,
                  selected.Id,
                  StringComparison.Ordinal) &&
              string.Equals(
                  slice.LatestPresentation.Context.Eyebrow,
                  "현재 초안 기준 예상",
                  StringComparison.Ordinal) &&
              slice.LatestPresentation.Context.Details.Any(item => string.Equals(
                  item.Heading,
                  "현재 초안 기준 예상",
                  StringComparison.Ordinal)) &&
              slice.AcceptedCommandCount == draftCommands &&
              string.Equals(slice.CanonicalStateSha256, draftHash,
                  StringComparison.Ordinal),
            "comparison marker selection lost its draft identity or mutated Core",
            failures);

        RequireIntent(slice.ApplyIntentForSmoke(new RealtimeR2Intent(
                RealtimeR2IntentKind.CancelLineDraft)),
            "selected comparison draft cancel", failures);
        RealtimeSlicePresentation cancelled = slice.LatestPresentation;
        Check(slice.CoreSnapshot.Construction.LineDraft is null &&
              !cancelled.ComparisonDraftForecast.Available &&
              cancelled.Rail.Items.All(item =>
                  !item.Id.StartsWith("DRAFT_FORECAST:", StringComparison.Ordinal) &&
                  !item.Id.StartsWith("DRAFT_THERMAL:", StringComparison.Ordinal) &&
                  !string.Equals(item.Id, "DRAFT_CONSTRUCTION",
                      StringComparison.Ordinal)) &&
              slice.InteractionState.SelectionId is null &&
              slice.InteractionState.TimelineSelectedItemId is null &&
              cancelled.Rail.SelectedItemId is null &&
              !cancelled.Context.Visible &&
              cancelled.World.SelectedAssetId is null &&
              slice.AcceptedCommandCount == draftCommands + 1,
            "accepted comparison-draft cancellation retained a vanished timeline, " +
            "inspector, or map selection",
            failures);
    }

    private static void ValidateFutureEventActualDraftConstruction(
        ICollection<string> failures)
    {
        var countdownSlice = CreateRunningSlice();
        using var countdownLifetime = countdownSlice.FreeAfterSmoke();
        RealtimeSlicePresentation countdownBefore = countdownSlice.LatestPresentation;
        RealtimeNextEventPresentation? nextBefore = countdownBefore.Rail.NextEvent;
        RealtimeForecastEvent? expectedNext = countdownBefore.BaseForecast.Events
            .Where(item => item.StartMinute > countdownSlice.CurrentMinute)
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .FirstOrDefault();
        Check(nextBefore is not null && expectedNext is not null &&
              string.Equals(nextBefore.EventId, expectedNext.EventId,
                  StringComparison.Ordinal) &&
              nextBefore.StartMinute == expectedNext.StartMinute &&
              nextBefore.EndMinute == expectedNext.EndMinute &&
              nextBefore.MinutesUntilStart ==
                  expectedNext.StartMinute - countdownSlice.CurrentMinute &&
              nextBefore.CountdownLabel.EndsWith("뒤", StringComparison.Ordinal) &&
              nextBefore.WindowLabel.Contains("시작", StringComparison.Ordinal) &&
              nextBefore.WindowLabel.Contains("종료", StringComparison.Ordinal),
            "persistent next-event status lost typed ID/start/end/countdown authority",
            failures);
        if (nextBefore is not null && nextBefore.MinutesUntilStart > 1)
        {
            _ = countdownSlice.InjectElapsedNanosecondsForSmoke(1_000_000_000);
            RealtimeNextEventPresentation? nextAfter =
                countdownSlice.LatestPresentation.Rail.NextEvent;
            Check(nextAfter is not null &&
                  string.Equals(nextAfter.EventId, nextBefore.EventId,
                      StringComparison.Ordinal) &&
                  nextAfter.StartMinute == nextBefore.StartMinute &&
                  nextAfter.EndMinute == nextBefore.EndMinute &&
                  nextAfter.MinutesUntilStart == nextBefore.MinutesUntilStart - 1 &&
                  !string.Equals(nextAfter.CountdownLabel, nextBefore.CountdownLabel,
                      StringComparison.Ordinal),
                "next-event countdown did not persist and decrement from the same typed minute",
                failures);
        }

        var constructionSlice = CreateRunningSlice();
        using var constructionLifetime = constructionSlice.FreeAfterSmoke();
        RealtimeSmokeLinePlan plan = constructionSlice.SmokeLinePlan;
        constructionSlice.AdvanceToForSmoke(plan.OrderMinute);
        RequireIntent(constructionSlice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildLine,
                    $"LINE:{plan.LineClassId}:{plan.PoleClassId}")),
            "rail line tool", failures, coreCommandExpected: false);
        RequireIntent(constructionSlice.ApplyIntentForSmoke(plan.Intents[0]),
            "rail line start", failures);
        RequireIntent(constructionSlice.ApplyIntentForSmoke(plan.Intents[1]),
            "rail line finish", failures);

        RealtimeSlicePresentation draftPresentation = constructionSlice.LatestPresentation;
        RealtimeTimelineItemPresentation? draft = draftPresentation.Rail.Items
            .SingleOrDefault(item => string.Equals(
                item.Id,
                "DRAFT_CONSTRUCTION",
                StringComparison.Ordinal));
        RealtimeProjectQuote draftQuote = constructionSlice.PreviewLineOrderForSmoke();
        Check(draft is not null && draftQuote is
              {
                  Accepted: true,
                  CompletionMinute: long draftCompletionMinute,
              } &&
              draft.StartMinute == draftCompletionMinute &&
              draft.Kind == RealtimeTimelineItemKind.Construction &&
              draft.SourceKind == RealtimeTimelineSourceKind.Draft &&
              string.Equals(draft.SourceGlyph, "◇", StringComparison.Ordinal) &&
              draft.Title.Contains("초안", StringComparison.Ordinal) &&
              draft.Description.Contains("아직 발주되지 않은", StringComparison.Ordinal) &&
              draft.SeverityLabel.Contains("미발주", StringComparison.Ordinal),
            "closed construction draft lacked a typed outlined completion marker and explicit copy",
            failures);
        if (draft is not null)
        {
            constructionSlice.ChooseTimelineClusterForSmoke(new[] { draft.Id });
            Check(constructionSlice.LatestPresentation.Context.Visible &&
                  constructionSlice.LatestPresentation.Context.Eyebrow.Contains(
                      "초안", StringComparison.Ordinal) &&
                  constructionSlice.LatestPresentation.Context.Sections.Any(item =>
                      item.Body.Contains("실제 공사 아님", StringComparison.Ordinal)),
                "draft completion selection did not retain explicit draft-versus-actual copy",
                failures);
        }
        AssertNoUnknownRailTargets(constructionSlice, draftPresentation, failures, "draft");

        RequireIntent(constructionSlice.ApplyIntentForSmoke(RealtimeR2Intent.OrderLine()),
            "rail actual line order", failures);
        RealtimeSlicePresentation activePresentation = constructionSlice.LatestPresentation;
        RealtimeTimelineItemPresentation? active = activePresentation.Rail.Items
            .SingleOrDefault(item => string.Equals(
                item.Id,
                "ACTIVE_CONSTRUCTION",
                StringComparison.Ordinal));
        Check(active is not null &&
              active.Kind == RealtimeTimelineItemKind.Construction &&
              active.SourceKind == RealtimeTimelineSourceKind.Actual &&
              string.Equals(active.SourceGlyph, "■", StringComparison.Ordinal) &&
              active.Visibility == RealtimeTimelineVisibility.Active &&
              active.IsCurrent &&
              active.Description.Contains("발주된 실제 공사", StringComparison.Ordinal) &&
              activePresentation.Rail.Items.All(item => !string.Equals(
                  item.Id,
                  "DRAFT_CONSTRUCTION",
                  StringComparison.Ordinal)),
            "accepted order did not replace the outlined draft with the exact filled actual marker",
            failures);
        AssertNoUnknownRailTargets(constructionSlice, activePresentation, failures, "active");

        _ = AdvanceToMinuteByFrames(
            constructionSlice,
            plan.ExpectedCompletionMinute,
            RealtimeSimulationSpeed.Normal,
            failures);
        RealtimeSlicePresentation completedPresentation =
            constructionSlice.LatestPresentation;
        RealtimeTimelineItemPresentation[] completedConstruction =
            completedPresentation.Rail.Items
                .Where(item => item.Id.StartsWith(
                    "COMPLETED_CONSTRUCTION:",
                    StringComparison.Ordinal))
                .ToArray();
        Check(completedPresentation.TransitionHistory.Any(item =>
                  item.Kind == RealtimeTransitionKind.ConstructionCompleted &&
                  item.Construction?.CompletionMinute == plan.ExpectedCompletionMinute) &&
              completedConstruction.Length == 1 &&
              completedConstruction[0].StartMinute == plan.ExpectedCompletionMinute &&
              completedConstruction[0].Visibility ==
                  RealtimeTimelineVisibility.Completed &&
              completedConstruction[0].SourceKind == RealtimeTimelineSourceKind.Actual &&
              completedConstruction[0].Description.Contains(
                  "실제 완공 기록", StringComparison.Ordinal) &&
              completedPresentation.Rail.Items.All(item => !string.Equals(
                  item.Id,
                  "ACTIVE_CONSTRUCTION",
                  StringComparison.Ordinal)),
            "actual completion transition did not persist as one factual construction history marker",
            failures);
        if (completedConstruction.Length == 1)
        {
            constructionSlice.ChooseTimelineClusterForSmoke(
                new[] { completedConstruction[0].Id });
            Check(constructionSlice.LatestPresentation.Context.Visible &&
                  string.Equals(
                      constructionSlice.LatestPresentation.Context.Eyebrow,
                      "실제 완공 기록",
                      StringComparison.Ordinal) &&
                  constructionSlice.LatestPresentation.Context.Sections.Any(item =>
                      item.Heading == "상태" &&
                      item.Body.Contains("공급망에 반영", StringComparison.Ordinal)),
                "completed construction marker did not open its factual history context",
                failures);
        }
        AssertNoUnknownRailTargets(
            constructionSlice,
            completedPresentation,
            failures,
            "completed");
    }

    private static void ValidateReleaseFirstLightControllerStory(
        ICollection<string> failures)
    {
        var slice = new RealtimeSliceMain();
        try
        {
            slice.BootstrapReleaseFirstLightForSmoke();
        }
        catch
        {
            slice.Free();
            throw;
        }
        using var sliceLifetime = slice.FreeAfterSmoke();
        RealtimeSliceData data = slice.SliceDataForSmoke;
        RealtimeChapterDefinition realtimeChapter = data.Campaign.Chapters.Single();
        CommercialCampaignChapterDefinition chapter = data.BaseCampaign.Chapters.Single(
            item => string.Equals(
                item.ChapterId,
                RealtimeCampaignOverlayLoader.FirstReleaseChapterId,
                StringComparison.Ordinal));
        RealtimeScheduledEventDefinition scheduled =
            realtimeChapter.ScheduledEvents.Single();
        CommercialStoryCard standardResult = chapter.ResultCards.Standard ??
            throw new InvalidOperationException(
                "Release FIRST_LIGHT has no standard authored result card.");
        RealtimeModalPresentation briefing = slice.LatestPresentation.Modal ??
            throw new InvalidOperationException(
                "Release FIRST_LIGHT did not present its briefing modal.");

        Check(data.SourceRoute == RealtimeSliceSourceRoute.ReleaseFirstLight &&
              data.BaseCampaignSha256 ==
                  "078df95f9f0c833be7e1a299088b4ab6e0de4ddf13426ce5b96a1abbeee70b7a" &&
              data.WorldSha256 ==
                  "a0a837717bbd6d35f655d8094dfa6daac182d47b2d03f24b18c4883c04feecdf" &&
              data.CampaignOverlaySha256 ==
                  "ef962a272683bfd6761fbf10a0ca14cb6c8bf90cdfde810b468ad451088f2258" &&
              data.FullComposedCampaignSha256 ==
                  "7bd151399040934cfcb9f7c96d2879aef6354cda79ced2af184641eb33a02f09" &&
              data.CampaignSha256 ==
                  "94379c0e8e4dae54b760a55df8c1143c975eaa12f11079e675b2e67ba57df88e" &&
              chapter.ChapterId == RealtimeCampaignOverlayLoader.FirstReleaseChapterId &&
              scheduled.EventId == RealtimeCampaignOverlayLoader.FirstReleaseEventId &&
              realtimeChapter.PreparationMinutes == 240 &&
              scheduled.StartOffsetMinutes == 240 &&
              scheduled.DurationMinutes == 60,
            "release FIRST_LIGHT route/source/prefix identity drifted",
            failures);
        Check(briefing.Id == "CHAPTER_BRIEFING" &&
              briefing.Eyebrow == chapter.Briefing.Speaker &&
              briefing.Heading == chapter.Briefing.Title &&
              briefing.Body == chapter.Briefing.Body,
            "release FIRST_LIGHT native briefing did not reuse the exact authored story card",
            failures);
        Check(
            RealtimeSliceMain.ParseSourceRoute(
                ["--release-chapter=FIRST_LIGHT"]) ==
                    RealtimeSliceSourceRoute.ReleaseFirstLight &&
            RealtimeSliceMain.ParseSourceRoute(Array.Empty<string>()) ==
                    RealtimeSliceSourceRoute.TechnicalCheckpointFixture,
            "release FIRST_LIGHT launch route parsing drifted",
            failures);
        bool unknownRouteRejected = false;
        try
        {
            _ = RealtimeSliceMain.ParseSourceRoute(
                ["--release-chapter=SECOND_HEART"]);
        }
        catch (ArgumentException)
        {
            unknownRouteRejected = true;
        }
        Check(unknownRouteRejected,
            "unopened release chapter launch route was accepted",
            failures);

        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.CloseModal("CHAPTER_BRIEFING")),
            "release briefing close", failures, coreCommandExpected: false);
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SetPlayerPaused(true)),
            "release player pause", failures, coreCommandExpected: false);

        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildNode,
                    "NODE:SMALL_SUBSTATION")),
            "release node tool", failures, coreCommandExpected: false);
        RequireIntent(slice.ApplyIntentForSmoke(new RealtimeR2Intent(
                RealtimeR2IntentKind.SetNodeDraft,
                FirstId: "SMALL_SUBSTATION",
                Position: new CoreMapPoint(2100, 700))),
            "release substation draft", failures);
        RealtimeProjectQuote nodeQuote = slice.PreviewNodeOrderForSmoke();
        Check(nodeQuote is
              {
                  Accepted: true,
                  CostCashUnit: 1_200_000,
                  BuildMinutes: 120,
                  CompletionMinute: 1140,
              },
            "release substation quote drifted from the playable preparation window",
            failures);
        RequireIntent(slice.ApplyIntentForSmoke(
                new RealtimeR2Intent(RealtimeR2IntentKind.OrderNode)),
            "release substation order", failures);
        ActiveConstructionSnapshot nodeConstruction =
            slice.CoreSnapshot.Construction.ActiveConstruction ??
            throw new InvalidOperationException(
                "Release substation order created no active construction.");
        string substationId = nodeConstruction.NodeIds.Single();
        Check(substationId == "PLAYER_SUBSTATION_1",
            "release substation generated an unexpected stable ID",
            failures);
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SetSpeed(RealtimeSimulationSpeed.VeryFast)),
            "release substation 4x resume", failures, coreCommandExpected: false);
        _ = AdvanceToMinuteByFrames(
            slice,
            nodeQuote.CompletionMinute!.Value,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SetPlayerPaused(true)),
            "release pause after substation", failures, coreCommandExpected: false);

        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildLine,
                    "LINE:STANDARD_LINE:STANDARD_POLE")),
            "release west line tool", failures, coreCommandExpected: false);
        foreach ((RealtimeR2Intent intent, string label) in new[]
                 {
                     (RealtimeR2Intent.StartLineDraft(
                         "WEST_SOURCE_NODE",
                         "STANDARD_LINE",
                         "STANDARD_POLE"), "start"),
                     (RealtimeR2Intent.AddLinePoint(new CoreMapPoint(750, 650)), "point-1"),
                     (RealtimeR2Intent.AddLinePoint(new CoreMapPoint(1050, 650)), "point-2"),
                     (RealtimeR2Intent.AddLinePoint(new CoreMapPoint(1600, 650)), "point-3"),
                     (RealtimeR2Intent.FinishLineDraft(substationId), "finish"),
                 })
        {
            RequireIntent(slice.ApplyIntentForSmoke(intent),
                $"release west line {label}", failures);
        }
        RealtimeProjectQuote westQuote = slice.PreviewLineOrderForSmoke();
        Check(westQuote is
              {
                  Accepted: true,
                  CostCashUnit: 245_000,
                  BuildMinutes: 98,
                  CompletionMinute: 1238,
              },
            "release west line quote drifted from the playable preparation window",
            failures);
        RequireIntent(slice.ApplyIntentForSmoke(RealtimeR2Intent.OrderLine()),
            "release west line order", failures);
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SetSpeed(RealtimeSimulationSpeed.VeryFast)),
            "release west line 4x resume", failures, coreCommandExpected: false);
        _ = AdvanceToMinuteByFrames(
            slice,
            westQuote.CompletionMinute!.Value,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SetPlayerPaused(true)),
            "release pause after west line", failures, coreCommandExpected: false);

        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildLine,
                    "LINE:STANDARD_LINE:STANDARD_POLE")),
            "release service line tool", failures, coreCommandExpected: false);
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.StartLineDraft(
                    substationId,
                    "STANDARD_LINE",
                    "STANDARD_POLE")),
            "release service line start", failures);
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.FinishLineDraft("EAST_RESIDENTIAL_TERMINAL")),
            "release service line finish", failures);
        RealtimeProjectQuote serviceQuote = slice.PreviewLineOrderForSmoke();
        Check(serviceQuote is
              {
                  Accepted: true,
                  CostCashUnit: 25_000,
                  BuildMinutes: 10,
                  CompletionMinute: 1248,
              },
            "release service line quote drifted from the playable preparation window",
            failures);
        RequireIntent(slice.ApplyIntentForSmoke(RealtimeR2Intent.OrderLine()),
            "release service line order", failures);
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SetSpeed(RealtimeSimulationSpeed.VeryFast)),
            "release service line 4x resume", failures, coreCommandExpected: false);
        _ = AdvanceToMinuteByFrames(
            slice,
            serviceQuote.CompletionMinute!.Value,
            RealtimeSimulationSpeed.VeryFast,
            failures);

        RealtimeNextEventPresentation? next = slice.LatestPresentation.Rail.NextEvent;
        Check(next is
              {
                  EventId: "FIRST_LIGHT_SUPPLY",
                  StartMinute: 1260,
                  EndMinute: 1320,
                  MinutesUntilStart: 12,
              } &&
              slice.CoreSnapshot.CashUnit == 7_030_000,
            "release live state lost its exact next-event countdown or construction cost",
            failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            1260,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        Check(slice.CoreSnapshot.ActiveEventStates.Single().EventId ==
                  "FIRST_LIGHT_SUPPLY" &&
              slice.EmittedTransitions.Any(item =>
                  item.Kind == RealtimeTransitionKind.EventStarted &&
                  item.Minute == 1260 &&
                  item.EventId == "FIRST_LIGHT_SUPPLY"),
            "release FIRST_LIGHT event did not begin on its authored live boundary",
            failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            1320,
            RealtimeSimulationSpeed.VeryFast,
            failures);

        RealtimeCampaignSnapshot completed = slice.CoreSnapshot;
        RealtimeChapterOutcome outcome = completed.CompletedChapters.Single();
        RealtimeEventOutcome eventOutcome = outcome.Events.Single();
        RealtimeTransition[] finalTransitions = slice.EmittedTransitions
            .Where(item => item.Minute == 1320)
            .ToArray();
        int eventCompletedIndex = IndexOf(finalTransitions, item =>
            item.Kind == RealtimeTransitionKind.EventCompleted);
        int chapterCompletedIndex = IndexOf(finalTransitions, item =>
            item.Kind == RealtimeTransitionKind.ChapterCompleted);
        int campaignCompletedIndex = IndexOf(finalTransitions, item =>
            item.Kind == RealtimeTransitionKind.CampaignCompleted);
        RealtimeModalPresentation result = slice.LatestPresentation.Modal ??
            throw new InvalidOperationException(
                "Release FIRST_LIGHT completion did not present a result modal.");
        string[] completedConstructionIds = slice.LatestPresentation.Rail.Items
            .Where(item => item.Id.StartsWith(
                "COMPLETED_CONSTRUCTION:", StringComparison.Ordinal))
            .Select(item => item.Id)
            .ToArray();
        Check(completed.CampaignComplete && completed.Minute == 1320 &&
              completed.CashUnit == 7_030_000 &&
              outcome.ChapterId == "FIRST_LIGHT" &&
              eventOutcome.EventId == "FIRST_LIGHT_SUPPLY" &&
              eventOutcome.SafetySatisfied &&
              eventOutcome.SafetyUnservedMinutes == 0 &&
              eventOutcome.PromiseSatisfied &&
              eventCompletedIndex >= 0 &&
              eventCompletedIndex < chapterCompletedIndex &&
              chapterCompletedIndex < campaignCompletedIndex &&
              !slice.EmittedTransitions.Any(item => item.Kind is
                  RealtimeTransitionKind.ThermalEmergencyEntered or
                  RealtimeTransitionKind.ThermalProtectiveTrip) &&
              completedConstructionIds.Length == 3,
            "release FIRST_LIGHT did not complete safely through exact event/chapter/campaign transitions",
            failures);
        Check(result.Id == "CAMPAIGN_RESULT" &&
              result.Eyebrow == standardResult.Speaker &&
              result.Heading == standardResult.Title &&
              result.Body == standardResult.Body,
            "release FIRST_LIGHT native result did not reuse the exact authored story card",
            failures);
    }

    private static void AssertNoUnknownRailTargets(
        RealtimeSliceMain slice,
        RealtimeSlicePresentation presentation,
        ICollection<string> failures,
        string phase)
    {
        string[] unknown = presentation.Rail.Items
            .Where(item => RealtimeSlicePresenter.ResolveTimelineTarget(
                    slice.DisplayWorldForSmoke,
                    presentation.CoreSnapshot,
                    presentation.BaseForecast,
                    presentation.ComparisonDraftForecast,
                    presentation.TransitionHistory,
                    item.Id).Kind == RealtimeTimelineTargetKind.Unknown)
            .Select(item => item.Id)
            .ToArray();
        Check(unknown.Length == 0,
            $"{phase} rail exposed unknown marker targets: {string.Join(",", unknown)}",
            failures);
    }

    private static IEnumerable<string> PlayerFacingStrings(
        RealtimeSlicePresentation presentation)
    {
        yield return presentation.Hud.Chapter;
        yield return presentation.Hud.Objective;
        yield return presentation.Hud.Clock;
        yield return presentation.Hud.Cash;
        yield return presentation.Hud.Reliability;
        yield return presentation.Hud.MajorWarning ?? string.Empty;
        yield return presentation.Rail.NextEvent?.EventLabel ?? string.Empty;
        yield return presentation.Rail.NextEvent?.CountdownLabel ?? string.Empty;
        yield return presentation.Rail.NextEvent?.WindowLabel ?? string.Empty;
        foreach (RealtimeTimelineItemPresentation item in presentation.Rail.Items)
        {
            yield return item.Title;
            yield return item.ShortLabel;
            yield return item.Description;
            yield return item.KindLabel;
            yield return item.TimeLabel;
            yield return item.EndTimeLabel ?? string.Empty;
            yield return item.TimingLabel;
            yield return item.SourceLabel;
            yield return item.SeverityLabel;
        }
        yield return presentation.Context.Eyebrow;
        yield return presentation.Context.Heading;
        foreach (RealtimeContextSectionPresentation section in presentation.Context.Sections)
        {
            yield return section.Heading;
            yield return section.Body;
        }
        foreach (RealtimeContextDetailPresentation detail in presentation.Context.Details)
        {
            yield return detail.Heading;
            yield return detail.Body;
        }
        foreach (RealtimeBuildToolPresentation tool in presentation.BuildShelf.Tools)
        {
            yield return tool.Label;
            yield return tool.Description;
        }
        yield return presentation.BuildShelf.Guidance;
        yield return presentation.ActionDock.Context;
        yield return presentation.ActionDock.Detail;
    }

    private static void ValidateModalRestore(ICollection<string> failures)
    {
        var slice = CreateRunningSlice();
        using var sliceLifetime = slice.FreeAfterSmoke();
        slice.SetSpeedForSmoke(RealtimeSimulationSpeed.VeryFast);
        slice.SetPlayerPausedForSmoke(true);
        RealtimeR2Intent open = RealtimeR2Intent.OpenModal(
            "SMOKE_RECOVERY",
            RealtimeModalKind.RecoveryConfirmation,
            RealtimePauseReason.RecoveryConfirmation,
            "SMOKE_RETURN_FOCUS");
        RequireIntent(slice.ApplyIntentForSmoke(open),
            "open recovery modal", failures, coreCommandExpected: false);
        Check(slice.InteractionState.Simulation == RealtimeSimulationState.AutoPaused &&
              slice.InteractionState.Surface == RealtimeSurface.BlockingModal &&
              slice.InteractionState.PauseReason == RealtimePauseReason.RecoveryConfirmation &&
              slice.InteractionState.ModalRestore is
              {
                  Simulation: RealtimeSimulationState.PlayerPaused,
                  RunningSpeed: RealtimeSimulationSpeed.VeryFast,
              },
            "modal did not preserve player pause and prior running speed", failures);

        RealtimeR2IntentResult nested = slice.ApplyIntentForSmoke(
            RealtimeR2Intent.OpenModal(
                "SMOKE_NESTED",
                RealtimeModalKind.NewGameConfirmation,
                RealtimePauseReason.RecoveryConfirmation));
        Check(!nested.Accepted && nested.PresentationRevisionDelta == 0 &&
              string.Equals(slice.InteractionState.ActiveModalId, "SMOKE_RECOVERY",
                  StringComparison.Ordinal),
            "nested modal was accepted or replaced the active modal", failures);

        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.CloseModal("SMOKE_RECOVERY")),
            "close recovery modal", failures, coreCommandExpected: false);
        Check(slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused &&
              slice.InteractionState.RunningSpeed == RealtimeSimulationSpeed.VeryFast &&
              slice.InteractionState.PauseReason == RealtimePauseReason.PlayerRequest &&
              string.Equals(slice.InteractionState.ReturnFocusId,
                  "SMOKE_RETURN_FOCUS", StringComparison.Ordinal),
            "modal close did not restore pause reason, speed, and focus token", failures);

        slice.SetPlayerPausedForSmoke(false);
        RequireIntent(slice.ApplyIntentForSmoke(RealtimeR2Intent.OpenModal(
                "SMOKE_RUNNING_MODAL",
                RealtimeModalKind.RecoveryConfirmation,
                RealtimePauseReason.RecoveryConfirmation,
                "SMOKE_RUNNING_FOCUS")),
            "open modal while running", failures, coreCommandExpected: false);
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.CloseModal("SMOKE_RUNNING_MODAL")),
            "close modal while running", failures, coreCommandExpected: false);
        Check(slice.InteractionState.Simulation == RealtimeSimulationState.Running &&
              slice.InteractionState.RunningSpeed == RealtimeSimulationSpeed.VeryFast,
            "running modal close failed to restore 4x simulation", failures);
    }

    private static void ValidatePointerPriority(ICollection<string> failures)
    {
        var slice = CreateRunningSlice();
        using var sliceLifetime = slice.FreeAfterSmoke();
        var expected = new Dictionary<RealtimeSmokePointerProbeKind, RealtimePointerOwner>
        {
            [RealtimeSmokePointerProbeKind.Fatal] = RealtimePointerOwner.Fatal,
            [RealtimeSmokePointerProbeKind.Modal] = RealtimePointerOwner.BlockingModal,
            [RealtimeSmokePointerProbeKind.Hud] = RealtimePointerOwner.Hud,
            [RealtimeSmokePointerProbeKind.Draft] = RealtimePointerOwner.DraftHandle,
            [RealtimeSmokePointerProbeKind.SelectionAction] =
                RealtimePointerOwner.SelectionAction,
            [RealtimeSmokePointerProbeKind.World] = RealtimePointerOwner.WorldCandidate,
            [RealtimeSmokePointerProbeKind.Empty] = RealtimePointerOwner.EmptyTerrain,
            [RealtimeSmokePointerProbeKind.Overlay] = RealtimePointerOwner.WorldCandidate,
            [RealtimeSmokePointerProbeKind.Weather] = RealtimePointerOwner.WorldCandidate,
        };

        foreach ((RealtimeSmokePointerProbeKind kind, RealtimePointerOwner owner) in expected)
        {
            slice.ResetPointerClickCountersForSmoke();
            RealtimePointerProbeResult result = slice.RoutePointerForSmoke(
                slice.CreatePointerProbeForSmoke(kind));
            Check(result.Resolution.Owner == owner,
                $"pointer probe {kind} resolved to {result.Resolution.Owner}, expected {owner}",
                failures);
            Check(result.ClickCounters.Values.Sum() == 1 &&
                  result.ClickCounters[owner] == 1,
                $"pointer probe {kind} was delivered to more or less than one owner",
                failures);
            Check(result.AfterCommandCount == result.BeforeCommandCount,
                $"pointer probe {kind} created an unrelated Core command", failures);
            if (kind is RealtimeSmokePointerProbeKind.Overlay or
                RealtimeSmokePointerProbeKind.Weather)
            {
                Check(result.Resolution.Owner == RealtimePointerOwner.WorldCandidate,
                    $"decorative {kind} intercepted the world hit", failures);
            }
        }
    }

    private static RealtimeSliceMain CreateRunningSlice()
    {
        var slice = new RealtimeSliceMain();
        try
        {
            slice.BootstrapForSmoke();
            string modalId = slice.InteractionState.ActiveModalId ??
                throw new InvalidOperationException(
                    "Bootstrap did not expose the chapter briefing.");
            RealtimeR2IntentResult close = slice.ApplyIntentForSmoke(
                RealtimeR2Intent.CloseModal(modalId));
            if (!close.Accepted || slice.InteractionState.Simulation !=
                RealtimeSimulationState.Running)
            {
                throw new InvalidOperationException(
                    $"Could not close the chapter briefing: {close.Error}");
            }
            return slice;
        }
        catch
        {
            slice.Free();
            throw;
        }
    }

    private static IReadOnlyList<RealtimeTransition> AdvanceToMinuteByFrames(
        RealtimeSliceMain slice,
        long targetMinute,
        RealtimeSimulationSpeed speed,
        ICollection<string> failures)
    {
        var transitions = new List<RealtimeTransition>();
        while (slice.CurrentMinute < targetMinute)
        {
            if (slice.InteractionState.Simulation == RealtimeSimulationState.AutoPaused &&
                slice.InteractionState.ActiveModalId is null)
            {
                RequireIntent(slice.ApplyIntentForSmoke(
                        RealtimeR2Intent.AcknowledgeAutoPause()),
                    "acknowledge critical auto-pause", failures,
                    coreCommandExpected: false);
            }
            Check(slice.InteractionState.Simulation == RealtimeSimulationState.Running,
                $"simulation was not running before minute {slice.CurrentMinute + 1}",
                failures);
            long before = slice.CurrentMinute;
            long frameCount = 60 / (int)speed;
            RealtimeR2FrameResult result =
                slice.InjectFramesForSmoke(frameCount, framesPerSecond: 60);
            transitions.AddRange(result.Transitions);
            Check(result.RequestedFrameCount == frameCount &&
                  result.ConsumedFrameCount == frameCount &&
                  result.FramesPerSecond == 60,
                $"{speed} did not consume its exact one-minute frame batch at {before}",
                failures);
            Check(slice.CurrentMinute == before + 1,
                $"{speed} injected interval did not advance exactly one minute at {before}",
                failures);
            if (slice.CurrentMinute <= before)
            {
                break;
            }
        }
        return Array.AsReadOnly(transitions.ToArray());
    }

    private static void ValidateThermalTransitions(
        IReadOnlyList<RealtimeTransition> transitions,
        RealtimeSmokeBoundaryFacts facts,
        string label,
        ICollection<string> failures)
    {
        foreach (RealtimeSmokeThermalBoundary boundary in facts.Thermal)
        {
            CheckTransition(boundary.EmergencyStartMinute,
                RealtimeTransitionKind.ThermalEmergencyEntered, boundary, label,
                transitions, failures);
            CheckTransition(boundary.TripMinute,
                RealtimeTransitionKind.ThermalProtectiveTrip, boundary, label,
                transitions, failures);
            CheckTransition(boundary.RecoveryMinute,
                RealtimeTransitionKind.ThermalRecovered, boundary, label,
                transitions, failures);
        }
    }

    private static void CheckTransition(
        long? minute,
        RealtimeTransitionKind kind,
        RealtimeSmokeThermalBoundary boundary,
        string label,
        IReadOnlyList<RealtimeTransition> transitions,
        ICollection<string> failures)
    {
        if (!minute.HasValue)
        {
            return;
        }
        Check(transitions.Any(item => item.Kind == kind &&
                item.Minute == minute.Value &&
                string.Equals(item.AssetId, boundary.AssetId, StringComparison.Ordinal) &&
                item.AssetKind == boundary.AssetKind),
            $"{label} lost {kind} for {boundary.AssetId}@{minute}", failures);
    }

    private static RealtimeWorldAssetState ExpectedWorldState(
        ThermalOperatingState state) => state switch
    {
        ThermalOperatingState.Continuous => RealtimeWorldAssetState.Normal,
        ThermalOperatingState.Emergency => RealtimeWorldAssetState.Emergency,
        ThermalOperatingState.ProtectiveOutage =>
            RealtimeWorldAssetState.ProtectiveOutage,
        ThermalOperatingState.OverLimit => RealtimeWorldAssetState.OverLimit,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static RealtimeWorldAssetState ExpectedWorldState(
        bool commissioned,
        RealtimeThermalAssetSnapshot? thermal) => !commissioned
            ? RealtimeWorldAssetState.Building
            : thermal is null
                ? RealtimeWorldAssetState.Normal
                : thermal.ProtectiveOutage
                    ? RealtimeWorldAssetState.ProtectiveOutage
                    : thermal.AuthoredUnavailable
                        ? RealtimeWorldAssetState.AuthoredUnavailable
                        : ExpectedWorldState(thermal.State);

    private static void RequireIntent(
        RealtimeR2IntentResult result,
        string label,
        ICollection<string> failures,
        bool coreCommandExpected = true)
    {
        Check(result.Accepted,
            $"{label} rejected: {result.Error}/" +
            $"{result.CoreCommandResult?.Error}/{result.CoreCommandResult?.ConstructionError}",
            failures);
        Check((result.CoreCommandResult is not null) == coreCommandExpected,
            $"{label} did not traverse the expected command boundary", failures);
        if (coreCommandExpected)
        {
            Check(result.CoreCommandResult?.Accepted == true && result.JournalDelta == 1,
                $"{label} did not produce one accepted Core result/journal entry", failures);
        }
    }

    private static string Fingerprint(IEnumerable<RealtimeTransition> transitions) =>
        string.Join("\n", transitions.Select(item => string.Join("|",
            item.Minute,
            item.Kind,
            item.ChapterId ?? string.Empty,
            item.EventId ?? string.Empty,
            item.AssetKind?.ToString() ?? string.Empty,
            item.AssetId ?? string.Empty,
            item.Construction?.Kind.ToString() ?? string.Empty,
            item.Construction is null
                ? string.Empty
                : string.Join(",", item.Construction.NodeIds),
            item.Construction is null
                ? string.Empty
                : string.Join(",", item.Construction.EdgeIds))));

    private static string ForecastFingerprint(RealtimeForecastSnapshot forecast) =>
        string.Join("\n",
            new[]
            {
                $"NOW|{forecast.NowMinute}|" +
                $"{forecast.ConstructionCompletionMinute?.ToString() ?? string.Empty}",
            }.Concat(forecast.Events.Select(item => string.Join("|",
                "EVENT",
                item.ChapterIndex,
                item.ChapterId,
                item.EventId,
                item.RevealMinute,
                item.StartMinute,
                item.EndMinute,
                item.Status,
                string.Join(",", item.TemporalProjection.Transitions.Select(
                    transition =>
                        $"{transition.Minute}:{transition.Kind}:" +
                        $"{transition.AssetKind}:{transition.AssetId}"))))));

    private static int IndexOf<T>(IReadOnlyList<T> values, Func<T, bool> predicate)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (predicate(values[index]))
            {
                return index;
            }
        }
        return -1;
    }

    private static void RunCase(
        string label,
        Action action,
        ICollection<string> failures)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failures.Add($"R2 smoke {label} threw {exception.GetType().Name}: " +
                         exception.Message);
        }
    }

    private static void Check(
        bool condition,
        string message,
        ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add($"R2 smoke: {message}");
        }
    }
}
#endif
