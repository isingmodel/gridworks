#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;
using GD = Godot.GD;
using Node = Godot.Node;
using Node2D = Godot.Node2D;
using PackedScene = Godot.PackedScene;

namespace Gridworks.Game.Realtime.R2;

internal static partial class RealtimeR2Smoke
{
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
                FirstId: nodeToolId[RealtimeR2Ids.NodeToolPrefix.Length..],
                Position: nodePosition)),
            "node quote draft", failures);
        AssertAcceptedOrderQuote(
            nodeSlice.PreviewNodeOrderForSmoke(),
            nodeSlice.LatestPresentation.ActionDock,
            RealtimeR2Ids.OrderNodeAction,
            "node",
            failures);

        var lineSlice = CreateRunningSlice();
        using var lineLifetime = lineSlice.FreeAfterSmoke();
        RealtimeSmokeLinePlan plan = lineSlice.SmokeLinePlan;
        lineSlice.AdvanceToForSmoke(plan.OrderMinute);
        RequireIntent(lineSlice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildLine,
                    RealtimeR2Ids.LineTool(plan.LineClassId, plan.PoleClassId))),
            "line quote tool", failures, coreCommandExpected: false);
        RequireIntent(lineSlice.ApplyIntentForSmoke(plan.Intents[0]),
            "line quote start", failures);
        RequireIntent(lineSlice.ApplyIntentForSmoke(plan.Intents[1]),
            "line quote finish", failures);
        AssertAcceptedOrderQuote(
            lineSlice.PreviewLineOrderForSmoke(),
            lineSlice.LatestPresentation.ActionDock,
            RealtimeR2Ids.OrderLineAction,
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
                FirstId: comparisonNodeToolId[RealtimeR2Ids.NodeToolPrefix.Length..],
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
              rejectedAction is { Id: RealtimeR2Ids.OrderNodeAction, Enabled: false } &&
              rejectedAction.Description.StartsWith(
                  "서비스 반경 R ",
                  StringComparison.Ordinal) &&
              rejectedAction.Description.Contains(
                  "\n발주 불가 · 현재 공사가 ",
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
            $"발주 견적 · 비용 {RealtimePresentationText.Cash(cost)} · 공기 {buildMinutes}분 · " +
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
              (expectedActionId == RealtimeR2Ids.OrderNodeAction
                  ? dock.PrimaryAction.Description.StartsWith(
                        "서비스 반경 R ", StringComparison.Ordinal) &&
                    dock.PrimaryAction.Description.EndsWith(
                        expected, StringComparison.Ordinal)
                  : string.Equals(
                        dock.PrimaryAction.Description,
                        expected,
                        StringComparison.Ordinal)) &&
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
        Check(slice.LatestPresentation.BuildShelf.Guidance.Contains(
                  "P",
                  StringComparison.Ordinal) &&
              slice.LatestPresentation.BuildShelf.Guidance.Contains(
                  "▶",
                  StringComparison.Ordinal),
            "critical auto-pause omitted explicit resume guidance", failures);
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
}
#endif
