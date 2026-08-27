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
        RunCase("stable-r2-id-protocol", () => ValidateStableR2IdProtocol(failures), failures);
        RunCase("live-audio-cue-selection",
            () => ValidateLiveAudioCueSelection(failures), failures);
        RunCase("fail-closed-routing", () => ValidateFailClosedRouting(failures), failures);
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
        RunCase("release-first-light-no-action-result",
            () => ValidateReleaseFirstLightNoActionResult(failures), failures);
        RunCase("release-tutorial-through-second-source",
            () => ValidateReleaseTutorialThroughSecondSource(failures), failures);
        RunCase("cumulative-event-duty-resume",
            () => ValidateCumulativeEventDutyResume(failures), failures);
        RunCase("release-through-longest-night-controller",
            () => ValidateReleaseThroughLongestNightController(failures), failures);
        RunCase("release-promise-result-branches",
            () => ValidateReleasePromiseResultBranches(failures), failures);
        RunCase("release-tutorial-connection-failure-result",
            () => ValidateReleaseTutorialConnectionFailureResult(failures), failures);
        RunCase("modal-restore", () => ValidateModalRestore(failures), failures);
        RunCase("pointer-priority", () => ValidatePointerPriority(failures), failures);
    }

    internal static RealtimeCampaignSave CreateCompletedProductSave(
        ICollection<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        return ValidateReleaseThroughLongestNightController(failures);
    }

    private static void ValidateStableR2IdProtocol(ICollection<string> failures)
    {
        var transition = new RealtimeThermalTransition(
            42,
            "ASSET",
            ThermalAssetKind.Edge,
            RealtimeThermalTransitionKind.ProtectiveTrip);
        var completion = new RealtimeConstructionCompletion(
            ConstructionKind.Line,
            42,
            new[] { "NODE_A", "NODE_B" },
            new[] { "EDGE_A", "EDGE_B" });
        string actual = string.Join("\n",
        [
            $"order-node={RealtimeR2Ids.OrderNodeAction}",
            $"order-line={RealtimeR2Ids.OrderLineAction}",
            $"promise-keep={RealtimeR2Ids.PromiseKeepAction}",
            $"promise-defer={RealtimeR2Ids.PromiseDeferAction}",
            $"notice-close={RealtimeR2Ids.NoticeCloseAction}",
            $"briefing-continue={RealtimeR2Ids.BriefingContinueAction}",
            $"event-continue={RealtimeR2Ids.EventStoryContinueAction}",
            $"decision-continue={RealtimeR2Ids.DecisionWindowContinueAction}",
            $"result-close={RealtimeR2Ids.ResultCloseAction}",
            $"epilogue-continue={RealtimeR2Ids.EpilogueContinueAction}",
            $"chapter-briefing={RealtimeR2Ids.ChapterBriefingModal}",
            $"campaign-result={RealtimeR2Ids.CampaignResultModal}",
            $"tutorial-result={RealtimeR2Ids.TutorialResultModal("CHAPTER")}",
            $"tutorial-briefing={RealtimeR2Ids.TutorialBriefingModal("CHAPTER")}",
            $"tutorial-decision={RealtimeR2Ids.TutorialDecisionWindowModal("CHAPTER", "WINDOW")}",
            $"tutorial-event={RealtimeR2Ids.TutorialEventStoryModal("CHAPTER", "EVENT")}",
            $"epilogue-city={RealtimeR2Ids.EpilogueModal(
                RealtimeEpiloguePurpose.CityReport)}",
            $"epilogue-medical={RealtimeR2Ids.EpilogueModal(
                RealtimeEpiloguePurpose.MedicalWitness)}",
            $"epilogue-closing={RealtimeR2Ids.EpilogueModal(
                RealtimeEpiloguePurpose.Closing)}",
            $"inspect={RealtimeR2Ids.InspectTool}",
            $"analysis={RealtimeR2Ids.AnalysisTool}",
            $"node={RealtimeR2Ids.NodeTool("CLASS")}",
            $"line={RealtimeR2Ids.LineTool("LINE", "POLE")}",
            $"active-construction={RealtimeR2Ids.ActiveConstructionMarker}",
            $"draft-construction={RealtimeR2Ids.DraftConstructionMarker}",
            $"completed-prefix={RealtimeR2Ids.CompletedConstructionMarkerPrefix}",
            $"promise-marker={RealtimeR2Ids.PromiseDecisionMarker("PROMISE")}",
            $"thermal={RealtimeR2Ids.ThermalMarker("EVENT", transition)}",
            $"actual-thermal={RealtimeR2Ids.ActualThermalMarker(new RealtimeTransition(
                42,
                RealtimeTransitionKind.ThermalProtectiveTrip,
                "CHAPTER",
                "EVENT",
                "ASSET",
                ThermalAssetKind.Edge))}",
            $"completed={RealtimeR2Ids.CompletedConstructionMarker(completion)}",
            $"comparison-event={RealtimeR2Ids.ComparisonEventMarker("EVENT")}",
            $"comparison-thermal={RealtimeR2Ids.ComparisonThermalMarker("EVENT", transition)}",
        ]);
        const string expected = """
            order-node=ORDER_NODE
            order-line=ORDER_LINE
            promise-keep=PROMISE_KEEP
            promise-defer=PROMISE_DEFER
            notice-close=NOTICE_CLOSE
            briefing-continue=BRIEFING_CONTINUE
            event-continue=EVENT_STORY_CONTINUE
            decision-continue=DECISION_WINDOW_CONTINUE
            result-close=RESULT_CLOSE
            epilogue-continue=EPILOGUE_CONTINUE
            chapter-briefing=CHAPTER_BRIEFING
            campaign-result=CAMPAIGN_RESULT
            tutorial-result=TUTORIAL_RESULT:CHAPTER
            tutorial-briefing=TUTORIAL_BRIEFING:CHAPTER
            tutorial-decision=TUTORIAL_DECISION_WINDOW:CHAPTER:WINDOW
            tutorial-event=TUTORIAL_EVENT_STORY:CHAPTER:EVENT
            epilogue-city=EPILOGUE:CITY_REPORT
            epilogue-medical=EPILOGUE:MEDICAL_WITNESS
            epilogue-closing=EPILOGUE:CLOSING
            inspect=TOOL:INSPECT
            analysis=TOOL:ANALYSIS
            node=NODE:CLASS
            line=LINE:LINE:POLE
            active-construction=ACTIVE_CONSTRUCTION
            draft-construction=DRAFT_CONSTRUCTION
            completed-prefix=COMPLETED_CONSTRUCTION:
            promise-marker=PROMISE_DEADLINE:PROMISE
            thermal=THERMAL:EVENT:42:ProtectiveTrip:ASSET
            actual-thermal=ACTUAL_THERMAL:42:ThermalProtectiveTrip:Edge:ASSET
            completed=COMPLETED_CONSTRUCTION:42:Line:NODE_A+NODE_B:EDGE_A+EDGE_B
            comparison-event=DRAFT_FORECAST:EVENT
            comparison-thermal=DRAFT_THERMAL:EVENT:42:ProtectiveTrip:ASSET
            """;
        Check(string.Equals(actual, expected, StringComparison.Ordinal),
            "the centralized R2 ID protocol drifted from its stable UI/evidence contract",
            failures);
    }

    private static void ValidateLiveAudioCueSelection(ICollection<string> failures)
    {
        IReadOnlyList<RealtimeTransition> none = Array.Empty<RealtimeTransition>();
        Check(
            RealtimeSession.SelectLiveAudioCue(
                operationAccepted: false,
                RealtimeR2IntentKind.OrderNode,
                [new RealtimeTransition(
                    42,
                    RealtimeTransitionKind.ThermalProtectiveTrip)]) is null &&
            RealtimeSession.SelectLiveAudioCue(
                operationAccepted: true,
                acceptedIntentKind: null,
                [new RealtimeTransition(
                    42,
                    RealtimeTransitionKind.ChapterStarted)]) is null,
            "rejected or unrelated live operations selected an audio cue",
            failures);

        Check(
            RealtimeSession.SelectLiveAudioCue(
                operationAccepted: true,
                RealtimeR2IntentKind.OrderNode,
                none) == RealtimeLiveAudioCue.Breaker &&
            RealtimeSession.SelectLiveAudioCue(
                operationAccepted: true,
                RealtimeR2IntentKind.OrderLine,
                none) == RealtimeLiveAudioCue.Breaker,
            "accepted node/line orders did not select the breaker cue",
            failures);

        RealtimeTransitionKind[] energizeKinds =
        [
            RealtimeTransitionKind.ConstructionCompleted,
            RealtimeTransitionKind.ThermalEmergencyCleared,
            RealtimeTransitionKind.ThermalRecovered,
        ];
        Check(energizeKinds.All(kind =>
                RealtimeSession.SelectLiveAudioCue(
                    operationAccepted: true,
                    RealtimeR2IntentKind.OrderNode,
                    [new RealtimeTransition(42, kind)]) ==
                RealtimeLiveAudioCue.Energize),
            "completion/clear/recovery did not override an order with energize",
            failures);

        RealtimeTransitionKind[] outageKinds =
        [
            RealtimeTransitionKind.ThermalEmergencyEntered,
            RealtimeTransitionKind.ThermalProtectiveTrip,
        ];
        Check(outageKinds.All(kind =>
                RealtimeSession.SelectLiveAudioCue(
                    operationAccepted: true,
                    RealtimeR2IntentKind.OrderLine,
                    [
                        new RealtimeTransition(
                            42,
                            RealtimeTransitionKind.ConstructionCompleted),
                        new RealtimeTransition(42, kind),
                        new RealtimeTransition(42, kind),
                    ]) == RealtimeLiveAudioCue.Outage),
            "emergency/trip did not select one highest-priority outage cue",
            failures);
    }

    private static void ValidateFailClosedRouting(ICollection<string> failures)
    {
        Check(Enum.GetValues<RealtimeR2IntentKind>()
                  .All(RealtimeInteractionReducer.Supports) &&
              Enum.GetValues<RealtimeTool>()
                  .All(value => RealtimeUiCapabilities.Supports(value)) &&
              Enum.GetValues<RealtimeSurface>()
                  .All(value => RealtimeUiCapabilities.Supports(value)) &&
              Enum.GetValues<RealtimePauseReason>()
                  .All(value => RealtimeUiCapabilities.Supports(value)) &&
              Enum.GetValues<RealtimeTimelineHorizonPreset>()
                  .All(value => RealtimeUiCapabilities.Supports(value)) &&
              Enum.GetValues<RealtimeTimelineNavigation>()
                  .All(value => RealtimeUiCapabilities.Supports(value)) &&
              Enum.GetValues<RealtimeModalKind>()
                  .All(value => RealtimeUiCapabilities.Supports(value)) &&
              Enum.GetValues<RealtimeInputPriority>()
                  .All(value => RealtimeUiCapabilities.Supports(value)) &&
              Enum.GetValues<RealtimeInputCommand>()
                  .All(value => RealtimeUiCapabilities.Supports(value)),
            "a current enum member is missing from its explicit R2 capability",
            failures);

        RealtimeInteractionState reducerState =
            RealtimeInteractionReducer.Initial(chapterBriefing: false);
        RealtimeInteractionReduction reduction = RealtimeInteractionReducer.Reduce(
            reducerState,
            new RealtimeR2Intent((RealtimeR2IntentKind)int.MaxValue));
        Check(!reduction.Accepted &&
              string.Equals(
                  reduction.Error,
                  RealtimeInteractionReducer.UnsupportedIntentReason,
                  StringComparison.Ordinal) &&
              ReferenceEquals(reducerState, reduction.State),
            "an unknown intent was not rejected without interaction mutation",
            failures);
        RealtimeInteractionReduction invalidModal = RealtimeInteractionReducer.Reduce(
            reducerState,
            new RealtimeR2Intent(
                RealtimeR2IntentKind.OpenModal,
                FirstId: "INVALID_MODAL",
                ModalKind: (RealtimeModalKind)int.MaxValue,
                PauseReason: (RealtimePauseReason)int.MaxValue));
        Check(!invalidModal.Accepted && ReferenceEquals(
                reducerState,
                invalidModal.State),
            "an invalid modal enum shape was not rejected without mutation",
            failures);
        Check(ThrowsArgumentOutOfRange(() => RealtimeInteractionReducer.AutoPause(
                reducerState,
                (RealtimePauseReason)int.MaxValue)),
            "an unknown automatic pause reason did not fail closed",
            failures);
        var invalidStoryRequest = new RealtimeChapterStoryModalRequest(
            "INVALID_STORY",
            (RealtimeChapterStoryModalPurpose)int.MaxValue,
            "INVALID_CHAPTER",
            null,
            false);
        Check(ThrowsArgumentOutOfRange(() => _ = invalidStoryRequest.PauseReason),
            "an unknown chapter-story purpose did not fail closed",
            failures);

        var session = new RealtimeSession(
            RealtimeSliceResources.LoadTechnicalFixture(typeof(RealtimeR2Smoke).Assembly));
        RealtimeModalPresentation briefing = session.LatestPresentation.Modal ??
            throw new InvalidOperationException("The technical session has no briefing modal.");
        Check(RealtimeR2Ids.IsSupportedModalCloseAction(
                briefing.PrimaryAction.Id),
            "the presented briefing action is outside the explicit modal capability",
            failures);
        session.HandleModalAction(briefing.Id, briefing.PrimaryAction.Id);
        Check(session.LatestPresentation.Modal is null,
            "the supported briefing action did not close its modal",
            failures);

        RealtimeRoutingFingerprint beforeIntent = RoutingFingerprint(session);
        RealtimeR2IntentResult unsupportedIntent = session.ApplyIntent(
            new RealtimeR2Intent((RealtimeR2IntentKind)int.MaxValue));
        Check(!unsupportedIntent.Accepted &&
              string.Equals(
                  unsupportedIntent.Error,
                  RealtimeInteractionReducer.UnsupportedIntentReason,
                  StringComparison.Ordinal) &&
              unsupportedIntent.CoreCommandResult is null,
            "the session did not reject an unknown intent at its application boundary",
            failures);
        Check(RoutingFingerprint(session) == beforeIntent,
            "an unknown session intent changed authoritative or presented state",
            failures);

        RealtimeR2IntentResult incompleteKnownIntent = session.ApplyIntent(
            new RealtimeR2Intent(RealtimeR2IntentKind.SetNodeDraft));
        Check(!incompleteKnownIntent.Accepted &&
              !string.Equals(
                  incompleteKnownIntent.Error,
                  RealtimeInteractionReducer.UnsupportedIntentReason,
                  StringComparison.Ordinal),
            "a supported Core intent was misclassified as an unknown intent",
            failures);

        RealtimeRoutingFingerprint beforeHandlers = RoutingFingerprint(session);
        Check(ThrowsArgumentOutOfRange(() => session.HandleAction("UNKNOWN_ACTION")),
            "an unknown action did not fail closed",
            failures);
        Check(ThrowsArgumentOutOfRange(() => session.HandleBuildTool("UNKNOWN_TOOL")),
            "an unknown build tool did not fail closed",
            failures);
        Check(ThrowsArgumentOutOfRange(() => session.HandleBuildTool("NODE:FORGED")),
            "a forged build-tool family member did not fail closed",
            failures);
        Check(ThrowsArgumentOutOfRange(() => session.HandleTimelineNavigation(
                (RealtimeTimelineNavigation)int.MaxValue)),
            "an unknown timeline navigation did not fail closed",
            failures);
        Check(ThrowsArgumentOutOfRange(() => session.SelectBuildToolFamily(
                (RealtimeBuildToolFamily)int.MaxValue)),
            "an unknown build-tool family did not fail closed",
            failures);
        session.HandleModalAction("STALE_MODAL", "UNKNOWN_MODAL_ACTION");
        Check(RoutingFingerprint(session) == beforeHandlers,
            "unknown handlers or a stale modal signal changed session state",
            failures);

        string[] supportedModalActions =
        [
            RealtimeR2Ids.NoticeCloseAction,
            RealtimeR2Ids.BriefingContinueAction,
            RealtimeR2Ids.EventStoryContinueAction,
            RealtimeR2Ids.DecisionWindowContinueAction,
            RealtimeR2Ids.ResultCloseAction,
        ];
        Check(supportedModalActions.All(RealtimeR2Ids.IsSupportedModalCloseAction) &&
              !RealtimeR2Ids.IsSupportedModalCloseAction("UNKNOWN_MODAL_ACTION"),
            "the modal close capability does not match the five production actions",
            failures);

        var inputRouter = new RealtimeInputRouter();
        try
        {
            Check(ThrowsArgumentOutOfRange(() => inputRouter.PushContext(
                    "invalid",
                    (RealtimeInputPriority)int.MaxValue)),
                "an unknown input priority entered the arbitration stack",
                failures);
            Check(ThrowsArgumentOutOfRange(() => inputRouter.CanReceive(
                    (RealtimeInputPriority)int.MaxValue)),
                "an unknown input priority participated in arbitration",
                failures);
            Check(inputRouter.PushContext("hud", RealtimeInputPriority.Hud) == 1,
                "rejected input priority consumed an arbitration token",
                failures);
        }
        finally
        {
            inputRouter.Free();
        }

        var slice = CreateRunningSlice();
        using var sliceLifetime = slice.FreeAfterSmoke();
        string shortcutHash = slice.CanonicalStateSha256;
        long shortcutRevision = slice.PresentationRevision;
        RealtimeInteractionState shortcutInteraction = slice.InteractionState;
        Check(ThrowsArgumentOutOfRange(() => slice.RequestShortcutForSmoke(
                (RealtimeInputCommand)int.MaxValue)),
            "an unknown shortcut did not fail closed",
            failures);
        Check(ThrowsArgumentOutOfRange(() => slice.RequestInputForSmoke(new RealtimeInputRequest(
                RealtimeInputCommand.TogglePause,
                (RealtimeInputPriority)int.MaxValue))),
            "an input request with unknown priority did not fail closed",
            failures);
        Check(string.Equals(
                  slice.CanonicalStateSha256,
                  shortcutHash,
                  StringComparison.Ordinal) &&
              slice.PresentationRevision == shortcutRevision &&
              slice.InteractionState == shortcutInteraction,
            "an unknown shortcut changed session or presentation state",
            failures);

        var cancellationSlice = CreateRunningSlice();
        using var cancellationLifetime = cancellationSlice.FreeAfterSmoke();
        (string nodeToolId, CoreMapPoint nodePosition) =
            cancellationSlice.AcceptedNodeDraftForSmoke();
        RequireIntent(cancellationSlice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildNode,
                    nodeToolId)),
            "fail-closed cancellation tool",
            failures,
            coreCommandExpected: false);
        RequireIntent(cancellationSlice.ApplyIntentForSmoke(new RealtimeR2Intent(
                RealtimeR2IntentKind.SetNodeDraft,
                FirstId: nodeToolId[RealtimeR2Ids.NodeToolPrefix.Length..],
                Position: nodePosition)),
            "fail-closed cancellation draft",
            failures);
        cancellationSlice.RequestShortcutForSmoke(RealtimeInputCommand.CancelOrBack);
        RealtimeR2IntentResult armedUnsupported = cancellationSlice.ApplyIntentForSmoke(
            new RealtimeR2Intent((RealtimeR2IntentKind)int.MaxValue));
        Check(!armedUnsupported.Accepted,
            "the armed-cancellation unknown intent was accepted",
            failures);
        cancellationSlice.RequestShortcutForSmoke(RealtimeInputCommand.CancelOrBack);
        Check(cancellationSlice.CoreSnapshot.Construction.NodeDraft is null,
            "unknown intent rejection disarmed an existing draft cancellation",
            failures);
    }

    private sealed record RealtimeRoutingFingerprint(
        string CanonicalStateSha256,
        long Minute,
        long CommandSequence,
        int AcceptedCommandCount,
        long PresentationRevision,
        RealtimeInteractionState Interaction,
        RealtimeFrameAccumulatorSnapshot Accumulator,
        int EmittedTransitionCount);

    private static RealtimeRoutingFingerprint RoutingFingerprint(
        RealtimeSession session) => new(
        session.CanonicalStateSha256,
        session.CurrentMinute,
        session.CommandSequence,
        session.AcceptedCommandCount,
        session.PresentationRevision,
        session.InteractionState,
        session.AccumulatorSnapshot,
        session.EmittedTransitions.Count);

    private static bool ThrowsArgumentOutOfRange(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return true;
        }
    }

    private static bool ThrowsInvalidOperation(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
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
                RealtimeR2Ids.LineTool(plan.LineClassId, plan.PoleClassId))),
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
                    RealtimeR2Ids.LineTool(plan.LineClassId, plan.PoleClassId))),
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

        string[] expectedToolIds = new[] { RealtimeR2Ids.InspectTool }
            .Concat(slice.CoreSnapshot.Chapter.Content.AvailableNodeClassIds
                .Select(id => RealtimeR2Ids.NodeTool(id)))
            .Concat(slice.CoreSnapshot.Chapter.Content.AvailableLinePlans
                .Select(plan => RealtimeR2Ids.LineTool(plan.LineClassId, plan.PoleClassId)))
            .Append(RealtimeR2Ids.AnalysisTool)
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
            RealtimeTimelineTargetResolver.Resolve(
                slice.DisplayWorldForSmoke,
                slice.CoreSnapshot,
                item.Id).SubjectId is not null).Id;
        string beforeHash = slice.CanonicalStateSha256;
        int beforeCommands = slice.AcceptedCommandCount;
        slice.ChooseTimelineClusterForSmoke(new[] { selectedMarkerId });
        RealtimeTimelineTarget target = RealtimeTimelineTargetResolver.Resolve(
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
        long sevenDayRequest = RealtimeTimelinePolicy.RequiredForecastHorizonMinutes(
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
                .Where(item => item.Id.StartsWith(RealtimeR2Ids.NodeToolPrefix, StringComparison.Ordinal))
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
                .Where(item => RealtimeTimelineTargetResolver.Resolve(
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
            .First(item => RealtimeTimelineTargetResolver.Resolve(
                thermalSlice.DisplayWorldForSmoke,
                thermalSlice.CoreSnapshot,
                item.Id).Kind == RealtimeTimelineTargetKind.ThermalAsset);
        RealtimeTimelineItemPresentation owningEvent =
            thermalSlice.LatestPresentation.Rail.Items.First(item =>
                RealtimeTimelineTargetResolver.Resolve(
                    thermalSlice.DisplayWorldForSmoke,
                    thermalSlice.CoreSnapshot,
                    item.Id).Kind == RealtimeTimelineTargetKind.Event &&
                thermal.Description.StartsWith($"{item.Title} 예상", StringComparison.Ordinal));
        thermalSlice.ChooseTimelineClusterForSmoke(new[] { thermal.Id });
        RealtimeTimelineTarget target = RealtimeTimelineTargetResolver.Resolve(
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
            .Where(item => item.Visibility == RealtimeTimelineVisibility.Completed &&
                item.EndMinute.HasValue)
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
                    RealtimeR2Ids.LineTool(plan.LineClassId, plan.PoleClassId))),
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
                    RealtimeR2Ids.LineTool(plan.LineClassId, plan.PoleClassId))),
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
                    RealtimeR2Ids.LineTool(plan.LineClassId, plan.PoleClassId))),
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
                  .Where(item => item.Id.StartsWith(RealtimeR2Ids.NodeToolPrefix, StringComparison.Ordinal) ||
                                 item.Id.StartsWith(RealtimeR2Ids.LineToolPrefix, StringComparison.Ordinal))
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
            .Where(item => item.Id.StartsWith(RealtimeR2Ids.NodeToolPrefix, StringComparison.Ordinal) ||
                           item.Id.StartsWith(RealtimeR2Ids.LineToolPrefix, StringComparison.Ordinal))
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
                RealtimeR2Ids.LineTool(plan.LineClassId, plan.PoleClassId)));
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

        slice.RequestBuildToolForSmoke(RealtimeR2Ids.AnalysisTool);
        RealtimeSlicePresentation mouseOn = slice.LatestPresentation;
        RealtimeBuildToolPresentation mouseTool = mouseOn.BuildShelf.Tools.Single(item =>
            string.Equals(item.Id, RealtimeR2Ids.AnalysisTool, StringComparison.Ordinal));
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

        slice.RequestBuildToolForSmoke(RealtimeR2Ids.AnalysisTool);
        Check(slice.InteractionState.Tool == RealtimeTool.Inspect &&
              slice.InteractionState.Surface == RealtimeSurface.Drawer &&
              !slice.LatestPresentation.World.AnalysisVisible,
            "mouse analysis re-click did not toggle the shared surface off", failures);

        slice.RequestShortcutForSmoke(RealtimeInputCommand.ToggleAnalysis);
        RealtimeSlicePresentation keyboardOn = slice.LatestPresentation;
        RealtimeBuildToolPresentation keyboardTool = keyboardOn.BuildShelf.Tools.Single(item =>
            string.Equals(item.Id, RealtimeR2Ids.AnalysisTool, StringComparison.Ordinal));
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
                    RealtimeR2Ids.LineTool(plan.LineClassId, plan.PoleClassId))),
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
                RealtimeR2Ids.ComparisonEventMarkerPrefix, StringComparison.Ordinal))
            .ToArray();
        RealtimeTimelineItemPresentation[] thermalMarkers = presentation.Rail.Items
            .Where(item => item.Id.StartsWith(
                RealtimeR2Ids.ComparisonThermalMarkerPrefix, StringComparison.Ordinal))
            .ToArray();
        RealtimeTimelineItemPresentation[] draftCompletionMarkers =
            presentation.Rail.Items
                .Where(item => string.Equals(
                    item.Id,
                    RealtimeR2Ids.DraftConstructionMarker,
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
                    RealtimeR2Ids.ComparisonEventMarker(forecast.EventId),
                    StringComparison.Ordinal));
            Check(marker.StartMinute == forecast.StartMinute &&
                  marker.EndMinute == forecast.EndMinute &&
                  marker.IsCurrent ==
                      (forecast.Status == RealtimeForecastStatus.Active),
                $"comparison event {forecast.EventId} diverged from Core timing/status",
                failures);
            RealtimeTimelineTarget target = RealtimeTimelineTargetResolver.Resolve(
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
                RealtimeR2Ids.ComparisonThermalMarker(
                    forecast.EventId,
                    transition);
            RealtimeTimelineItemPresentation marker = thermalMarkers.Single(item =>
                string.Equals(item.Id, expectedId, StringComparison.Ordinal));
            RealtimeTimelineTarget target = RealtimeTimelineTargetResolver.Resolve(
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
                  !item.Id.StartsWith(RealtimeR2Ids.ComparisonEventMarkerPrefix, StringComparison.Ordinal) &&
                  !item.Id.StartsWith(RealtimeR2Ids.ComparisonThermalMarkerPrefix, StringComparison.Ordinal) &&
                  !string.Equals(item.Id, RealtimeR2Ids.DraftConstructionMarker,
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
                    RealtimeR2Ids.LineTool(plan.LineClassId, plan.PoleClassId))),
            "rail line tool", failures, coreCommandExpected: false);
        RequireIntent(constructionSlice.ApplyIntentForSmoke(plan.Intents[0]),
            "rail line start", failures);
        RequireIntent(constructionSlice.ApplyIntentForSmoke(plan.Intents[1]),
            "rail line finish", failures);

        RealtimeSlicePresentation draftPresentation = constructionSlice.LatestPresentation;
        RealtimeTimelineItemPresentation? draft = draftPresentation.Rail.Items
            .SingleOrDefault(item => string.Equals(
                item.Id,
                RealtimeR2Ids.DraftConstructionMarker,
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
                RealtimeR2Ids.ActiveConstructionMarker,
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
                  RealtimeR2Ids.DraftConstructionMarker,
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
                    RealtimeR2Ids.CompletedConstructionMarkerPrefix,
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
                  RealtimeR2Ids.ActiveConstructionMarker,
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
            slice.BootstrapNativeReleaseForSmoke(RealtimeNativeRouteCatalog.FirstLight);
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

        Check(data.NativeRoute == RealtimeNativeRouteCatalog.FirstLight &&
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
        Check(briefing.Id == RealtimeR2Ids.ChapterBriefingModal &&
              briefing.Eyebrow == chapter.Briefing.Speaker &&
              briefing.Heading == chapter.Briefing.Title &&
              briefing.Body == chapter.Briefing.Body,
            "release FIRST_LIGHT native briefing did not reuse the exact authored story card",
            failures);
        Check(
            RealtimeSliceMain.ParseLaunchArguments(
                ["--release-chapter=FIRST_LIGHT"]).NativeRoute ==
                    RealtimeNativeRouteCatalog.FirstLight &&
            RealtimeSliceMain.ParseLaunchArguments(Array.Empty<string>()).Kind ==
                RealtimeLaunchKind.ProductTitle &&
            RealtimeSliceMain.ParseLaunchArguments(
                [RealtimeLaunchCatalog.TechnicalFixtureArgument]).Kind ==
                RealtimeLaunchKind.TechnicalFixture,
            "release FIRST_LIGHT launch route parsing drifted",
            failures);
        bool unknownRouteRejected = false;
        try
        {
            _ = RealtimeSliceMain.ParseLaunchArguments(
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
                RealtimeR2Intent.CloseModal(RealtimeR2Ids.ChapterBriefingModal)),
            "release briefing close", failures, coreCommandExpected: false);
        Check(slice.LatestPresentation.Hud.Objective.Contains(
                  "1/3",
                  StringComparison.Ordinal),
            "release FIRST_LIGHT did not begin with staged node guidance",
            failures);
        slice.RequestBuildToolForSmoke("NODE:SMALL_SUBSTATION");
        Check(slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused &&
              slice.LatestPresentation.BuildShelf.Guidance.Contains(
                  "계획 정지",
                  StringComparison.Ordinal),
            "release FIRST_LIGHT build entry did not pause protected planning",
            failures);
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
        Check(slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused &&
              slice.LatestPresentation.Hud.Objective.Contains(
                  "2/3",
                  StringComparison.Ordinal) &&
              slice.LatestPresentation.BuildShelf.Guidance.Contains(
                  "공사 완료",
                  StringComparison.Ordinal),
            "release FIRST_LIGHT node completion did not pause with the source-corridor step",
            failures);
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SetPlayerPaused(true)),
            "release pause after substation", failures, coreCommandExpected: false);

        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildLine,
                    "LINE:STANDARD_LINE:STANDARD_POLE")),
            "release west line tool", failures, coreCommandExpected: false);
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.StartLineDraft(
                    "WEST_SOURCE_NODE",
                    "STANDARD_LINE",
                    "STANDARD_POLE")),
            "release west line start", failures);
        RealtimeR2IntentResult overSpan = slice.ApplyIntentForSmoke(
            RealtimeR2Intent.FinishLineDraft(substationId));
        Check(!overSpan.Accepted &&
              slice.LatestPresentation.BuildShelf.Guidance.Contains(
                  "경간",
                  StringComparison.Ordinal) &&
              slice.LatestPresentation.BuildShelf.Guidance.Contains(
                  "허용 600",
                  StringComparison.Ordinal),
            "release FIRST_LIGHT rejected span omitted its exact length and limit",
            failures);
        foreach ((RealtimeR2Intent intent, string label) in new[]
                 {
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
        Check(slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused &&
              slice.LatestPresentation.Hud.Objective.Contains(
                  "3/3",
                  StringComparison.Ordinal),
            "release FIRST_LIGHT west-line completion did not show the service-line step",
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
        Check(slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused &&
              slice.LatestPresentation.Hud.Objective.Contains(
                  "경로 준비 완료",
                  StringComparison.Ordinal),
            "release FIRST_LIGHT service completion did not pause with ready guidance",
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
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SetSpeed(RealtimeSimulationSpeed.VeryFast)),
            "release ready network 4x resume", failures, coreCommandExpected: false);
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
                RealtimeR2Ids.CompletedConstructionMarkerPrefix, StringComparison.Ordinal))
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
        Check(result.Id == RealtimeR2Ids.CampaignResultModal &&
              result.Eyebrow == standardResult.Speaker &&
              result.Heading == standardResult.Title &&
              result.Body == standardResult.Body,
            "release FIRST_LIGHT native result did not reuse the exact authored story card",
            failures);
        bool formativeRecord = slice.ClosePresentedPrimaryModalForSmoke();
        Check(formativeRecord && slice.LatestPresentation.Modal is null,
            "successful FIRST_LIGHT result did not close through the production " +
            "handler and authorize its formative direct-play record",
            failures);
    }

    private static void ValidateReleaseFirstLightNoActionResult(
        ICollection<string> failures)
    {
        var slice = new RealtimeSliceMain();
        try
        {
            slice.BootstrapNativeReleaseForSmoke(RealtimeNativeRouteCatalog.FirstLight);
        }
        catch
        {
            slice.Free();
            throw;
        }
        using var sliceLifetime = slice.FreeAfterSmoke();
        CommercialCampaignChapterDefinition chapter = slice.SliceDataForSmoke
            .BaseCampaign.Chapters.Single(item => string.Equals(
                item.ChapterId,
                RealtimeCampaignOverlayLoader.FirstReleaseChapterId,
                StringComparison.Ordinal));
        CommercialStoryCard standardResult = chapter.ResultCards.Standard ??
            throw new InvalidOperationException(
                "Release FIRST_LIGHT has no standard authored result card.");

        bool briefingRecord = slice.ClosePresentedPrimaryModalForSmoke();
        Check(!briefingRecord && slice.LatestPresentation.Modal is null,
            "briefing close incorrectly authorized a formative completion record",
            failures);
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SetSpeed(RealtimeSimulationSpeed.VeryFast)),
            "no-action release 4x", failures, coreCommandExpected: false);
        _ = AdvanceToMinuteByFrames(
            slice,
            1320,
            RealtimeSimulationSpeed.VeryFast,
            failures);

        RealtimeCampaignSnapshot completed = slice.CoreSnapshot;
        RealtimeEventOutcome eventOutcome = completed.CompletedChapters
            .Single().Events.Single();
        RealtimeModalPresentation result = slice.LatestPresentation.Modal ??
            throw new InvalidOperationException(
                "No-action FIRST_LIGHT completion did not present a factual result.");
        Check(completed.CampaignComplete &&
              string.Equals(
                  eventOutcome.EventId,
                  RealtimeCampaignOverlayLoader.FirstReleaseEventId,
                  StringComparison.Ordinal) &&
              !eventOutcome.SafetySatisfied &&
              eventOutcome.SafetyUnservedMinutes > 0 &&
              result.Id == RealtimeR2Ids.CampaignResultModal &&
              result.Eyebrow == "운영 결과" &&
              result.Heading == "첫 불빛 공급 목표 미달" &&
              result.Body.Contains("안전 의무 0/1 충족", StringComparison.Ordinal) &&
              result.Body.Contains("연결 경로 없음", StringComparison.Ordinal) &&
              result.Body.Contains("다음 시도", StringComparison.Ordinal) &&
              (!string.Equals(result.Eyebrow, standardResult.Speaker,
                   StringComparison.Ordinal) ||
               !string.Equals(result.Heading, standardResult.Title,
                   StringComparison.Ordinal) ||
               !string.Equals(result.Body, standardResult.Body,
                   StringComparison.Ordinal)),
            "no-action FIRST_LIGHT completion counterfeited the positive authored result",
            failures);
        bool formativeRecord = slice.ClosePresentedPrimaryModalForSmoke();
        Check(!formativeRecord && slice.LatestPresentation.Modal is null,
            "no-action FIRST_LIGHT result close counterfeited the formative " +
            "direct-play PASS record",
            failures);
    }

    private static void ValidateReleaseTutorialThroughSecondSource(
        ICollection<string> failures)
    {
        var slice = new RealtimeSliceMain();
        try
        {
            slice.BootstrapNativeReleaseForSmoke(
                RealtimeNativeRouteCatalog.TutorialThroughSecondSource);
        }
        catch
        {
            slice.Free();
            throw;
        }
        using var sliceLifetime = slice.FreeAfterSmoke();
        RealtimeSliceData data = slice.SliceDataForSmoke;
        Check(data.Campaign.Chapters.Select(item => item.Content.ChapterId).SequenceEqual(
                new[] { "FIRST_LIGHT", "SECOND_HEART", "SECOND_SOURCE" },
                StringComparer.Ordinal),
            "tutorial native prefix chapter identity",
            failures);
        Check(data.NativeRoute ==
                  RealtimeNativeRouteCatalog.TutorialThroughSecondSource &&
              RealtimeSliceMain.ParseLaunchArguments(
                  ["--release-through=SECOND_SOURCE"]).NativeRoute ==
                  RealtimeNativeRouteCatalog.TutorialThroughSecondSource &&
              RealtimeSliceMain.ParseLaunchArguments(
                  ["--release-chapter=FIRST_LIGHT"]).NativeRoute ==
                  RealtimeNativeRouteCatalog.FirstLight &&
              RealtimeSliceMain.ParseLaunchArguments(
                  ["--checkpoint=A1_NORMAL_READY"]).Kind ==
                  RealtimeLaunchKind.TechnicalFixture &&
              RealtimeSliceMain.ParseLaunchArguments(
                  ["--checkpoint=A1_CONSTRUCTION_DUE_1M"]).Kind ==
                  RealtimeLaunchKind.TechnicalFixture,
            "tutorial exact launch route or FIRST_LIGHT preservation drifted",
            failures);
        Check(RealtimeNativeRouteCatalog.NativeThroughChapterId ==
                  "LONGEST_NIGHT" &&
              RealtimeNativeRouteCatalog.All.Count == 3 &&
              RealtimeNativeRouteCatalog.All.Select(item => item.LaunchArgument)
                  .SequenceEqual(
                      new[]
                      {
                          "--release-chapter=FIRST_LIGHT",
                          "--release-through=SECOND_SOURCE",
                          "--release-through=LONGEST_NIGHT",
                      },
                      StringComparer.Ordinal) &&
              RealtimeNativeRouteCatalog.All.All(item =>
                  item.SelectedChapterCount <=
                      RealtimeNativeRouteCatalog.ProductCampaign
                          .SelectedChapterCount) &&
              RealtimeNativeRouteCatalog.ProductCampaign
                  .SelectedChapterCount == 8 &&
              RealtimeNativeRouteCatalog.ProductCampaign
                  .FullFlowPassToken ==
                  "FULL_FLOW_E2E_PASS:RELEASE_FULL_CAMPAIGN_THROUGH_LONGEST_NIGHT",
            "native route catalog or explicit LONGEST_NIGHT cap drifted",
            failures);
        bool forgedRouteRejected = false;
        try
        {
            _ = RealtimeSliceResources.LoadNativeRelease(
                typeof(RealtimeSliceMain).Assembly,
                RealtimeNativeRouteCatalog.ProductCampaign with
                {
                    EndChapterId = "SWITCH_OFF_TO_PROTECT",
                    SelectedChapterCount = 7,
                    FullFlowPassToken = "FORGED_FULL_FLOW_PASS",
                });
        }
        catch (ArgumentException)
        {
            forgedRouteRejected = true;
        }
        Check(
            forgedRouteRejected,
            "a cloned native route bypassed the canonical catalog/cap boundary",
            failures);
        string[][] rejectedRoutes =
        [
            ["--release-through=SECOND_HEART"],
            ["--release-through=NORTH_BANK_PROMISE"],
            ["--release-through=WHOSE_MARGIN"],
            ["--release-through=BEFORE_WATER_RISE"],
            ["--release-through=SWITCH_OFF_TO_PROTECT"],
            ["--release-through"],
            ["--bogus"],
            ["--checkpoint=UNKNOWN"],
            ["--release-chapter=FIRST_LIGHT", "--release-through=SECOND_SOURCE"],
        ];
        foreach (string[] rejectedRoute in rejectedRoutes)
        {
            bool rejected = false;
            try
            {
                _ = RealtimeSliceMain.ParseLaunchArguments(rejectedRoute);
            }
            catch (ArgumentException)
            {
                rejected = true;
            }
            Check(rejected,
                $"tutorial invalid route was accepted: {string.Join(' ', rejectedRoute)}",
                failures);
        }

        var observedRailEvents = new List<string>();
        ObserveTutorialRail(slice, observedRailEvents);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "FIRST_LIGHT",
            null,
            data.BaseCampaign.Chapters[0].Briefing,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null &&
              slice.InteractionState.Simulation == RealtimeSimulationState.Running,
            "tutorial FIRST_LIGHT briefing did not close into realtime play",
            failures);

        string substationId = BuildTutorialFirstLightNetwork(slice, failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            1320,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        ObserveTutorialRail(slice, observedRailEvents);
        RealtimeChapterOutcome firstOutcome = slice.CoreSnapshot.CompletedChapters
            .Single(item => item.ChapterId == "FIRST_LIGHT");
        Check(firstOutcome.ObjectiveSatisfied,
            "tutorial FIRST_LIGHT positive route failed its derived objective",
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterResult,
            "FIRST_LIGHT",
            null,
            data.BaseCampaign.Chapters[0].ResultCards.Standard!,
            failures);
        RealtimeModalPresentation? secondBriefing =
            slice.ClosePresentedStoryModalForSmoke();
        Check(secondBriefing is not null &&
              slice.FormativeTutorialResultChapterIdsForSmoke.SequenceEqual(
                  new[] { "FIRST_LIGHT" },
                  StringComparer.Ordinal),
            "FIRST_LIGHT result did not synchronously queue SECOND_HEART briefing",
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "SECOND_HEART",
            null,
            data.BaseCampaign.Chapters[1].Briefing,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null &&
              slice.InteractionState.Simulation == RealtimeSimulationState.Running &&
              slice.InteractionState.PresentedSpeed == RealtimeSimulationSpeed.VeryFast,
            "SECOND_HEART briefing did not restore the prior realtime speed",
            failures);

        string hospitalSubstationId = OrderTutorialNode(
            slice,
            new CoreMapPoint(2250, 1300),
            failures,
            "hospital service substation");
        _ = OrderTutorialLine(
            slice,
            substationId,
            [new CoreMapPoint(2250, 1000)],
            hospitalSubstationId,
            "STANDARD_LINE",
            "STANDARD_POLE",
            failures,
            "safe hospital substation feed");
        _ = OrderTutorialLine(
            slice,
            hospitalSubstationId,
            Array.Empty<CoreMapPoint>(),
            "HOSPITAL_TERMINAL",
            "STANDARD_LINE",
            "STANDARD_POLE",
            failures,
            "safe hospital corridor one");
        _ = OrderTutorialLine(
            slice,
            "EAST_RESIDENTIAL_TERMINAL",
            [new CoreMapPoint(2550, 1050)],
            "HOSPITAL_TERMINAL",
            "STANDARD_LINE",
            "STANDARD_POLE",
            failures,
            "safe hospital corridor two");
        RealtimeConnectionRequirementAssessment currentTwo =
            slice.CoreSnapshot.Forecast.ConnectionRequirementAssessment ??
            throw new InvalidOperationException(
                "SECOND_HEART HUD connection assessment is absent.");
        Check(currentTwo.Satisfied &&
              currentTwo.Facts.Single().CurrentConnections == 2 &&
              slice.LatestPresentation.Hud.Objective.Contains(
                  "2/2",
                  StringComparison.Ordinal),
            "SECOND_HEART did not show the Core-owned current hospital 2/2",
            failures);

        _ = AdvanceToMinuteByFrames(
            slice,
            1500,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RequireIntent(slice.ApplyIntentForSmoke(RealtimeR2Intent.SetTimelineMarker(
                "FLOOD_ISOLATION_TEST",
                "HOSPITAL_TERMINAL",
                null,
                slice.InteractionState.TimelineHorizon)),
            "tutorial flood marker selection", failures, coreCommandExpected: false);
        RequireIntent(slice.ApplyIntentForSmoke(
                new RealtimeR2Intent(RealtimeR2IntentKind.ToggleAnalysis)),
            "tutorial flood analysis", failures, coreCommandExpected: false);
        Check(slice.LatestPresentation.World.ForecastRiskAreaIds.SequenceEqual(
                  new[] { "RIVER_FLOOD_ZONE" },
                  StringComparer.Ordinal) &&
              slice.LatestPresentation.World.ActiveRiskAreaIds.Count == 0,
            "selected announced flood did not expose forecast-only risk geometry",
            failures);

        _ = AdvanceToMinuteByFrames(
            slice,
            1680,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RealtimeConnectionRequirementAssessment frozenTwo =
            slice.CoreSnapshot.Forecast.ConnectionRequirementAssessment ??
            throw new InvalidOperationException(
                "SECOND_HEART frozen connection assessment is absent.");
        Check(frozenTwo is { FrozenForChapter: true, Satisfied: true } &&
              frozenTwo.EvaluatedMinute == 1680 &&
              frozenTwo.Facts.Single().CurrentConnections == 2,
            "SECOND_HEART did not freeze exact 2/2 at the first test",
            failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            1800,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        ObserveTutorialRail(slice, observedRailEvents);
        CommercialStoryCard floodStory = data.BaseCampaign.Chapters[1]
            .OperatingPhases.Single(item =>
                item.PhaseId == "FLOOD_ISOLATION_TEST").Story!;
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.EventStory,
            "SECOND_HEART",
            "FLOOD_ISOLATION_TEST",
            floodStory,
            failures);
        Check(slice.LatestPresentation.World.ForecastRiskAreaIds.Count == 0 &&
              slice.LatestPresentation.World.ActiveRiskAreaIds.SequenceEqual(
                  new[] { "RIVER_FLOOD_ZONE" },
                  StringComparer.Ordinal),
            "active flood did not replace forecast geometry with active geometry",
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null &&
              slice.InteractionState.Simulation == RealtimeSimulationState.Running,
            "flood story did not restore realtime play",
            failures);

        _ = AdvanceToMinuteByFrames(
            slice,
            1860,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RealtimeChapterOutcome heartOutcome = slice.CoreSnapshot.CompletedChapters
            .Single(item => item.ChapterId == "SECOND_HEART");
        Check(heartOutcome.ObjectiveSatisfied,
            "safe SECOND_HEART route failed its authored objective: " +
            TutorialOutcomeDiagnostic(heartOutcome),
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterResult,
            "SECOND_HEART",
            null,
            data.BaseCampaign.Chapters[1].ResultCards.Standard!,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is not null &&
              slice.FormativeTutorialResultChapterIdsForSmoke.SequenceEqual(
                  new[] { "FIRST_LIGHT", "SECOND_HEART" },
                  StringComparer.Ordinal),
            "SECOND_HEART result did not queue SECOND_SOURCE briefing",
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "SECOND_SOURCE",
            null,
            data.BaseCampaign.Chapters[2].Briefing,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null,
            "SECOND_SOURCE briefing did not close into realtime play",
            failures);
        ObserveTutorialRail(slice, observedRailEvents);
        RealtimeBuildToolPresentation standardLine = slice.LatestPresentation.BuildShelf.Tools
            .Single(item => item.Id == "LINE:STANDARD_LINE:STANDARD_POLE");
        Check(standardLine.Description.Contains("비용", StringComparison.Ordinal) &&
              standardLine.Description.Contains("공기", StringComparison.Ordinal) &&
              standardLine.Description.Contains("연속 2,500 kW", StringComparison.Ordinal) &&
              !standardLine.Description.Contains("경로:", StringComparison.Ordinal),
            "SECOND_SOURCE did not withdraw route hints while preserving typed line facts",
            failures);

        RealtimeProjectQuote southQuote = OrderTutorialLine(
            slice,
            "SOUTH_SOURCE_NODE",
            [
                new CoreMapPoint(700, 1650),
                new CoreMapPoint(1150, 1650),
                new CoreMapPoint(1750, 1650),
                new CoreMapPoint(2050, 1450),
            ],
            hospitalSubstationId,
            "STANDARD_LINE",
            "STANDARD_POLE",
            failures,
            "south source commissioning corridor");
        Check(southQuote.CompletionMinute < 2280,
            "south source corridor missed the first SECOND_SOURCE test",
            failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            2400,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        ObserveTutorialRail(slice, observedRailEvents);
        CommercialStoryCard southStory = data.BaseCampaign.Chapters[2]
            .OperatingPhases.Single(item =>
                item.PhaseId == "SOUTH_SOURCE_COMMISSIONING_TEST").Story!;
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.EventStory,
            "SECOND_SOURCE",
            "SOUTH_SOURCE_COMMISSIONING_TEST",
            southStory,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null,
            "south-source story did not restore realtime play",
            failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            2460,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RealtimeChapterOutcome sourceOutcome = slice.CoreSnapshot.CompletedChapters
            .Single(item => item.ChapterId == "SECOND_SOURCE");
        Check(sourceOutcome.ObjectiveSatisfied && slice.CoreSnapshot.CampaignComplete,
            "SECOND_SOURCE positive route did not complete the tutorial campaign: " +
            TutorialOutcomeDiagnostic(sourceOutcome),
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterResult,
            "SECOND_SOURCE",
            null,
            data.BaseCampaign.Chapters[2].ResultCards.Standard!,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null &&
              slice.InteractionState.Simulation == RealtimeSimulationState.Ended &&
              slice.FormativeTutorialResultChapterIdsForSmoke.SequenceEqual(
                  new[] { "FIRST_LIGHT", "SECOND_HEART", "SECOND_SOURCE" },
                  StringComparer.Ordinal) &&
              slice.FormativeTutorialFullFlowRecordedForSmoke,
            "final tutorial close did not record the ordered three results/full flow",
            failures);
        string[] exactEventOrder =
        [
            "FIRST_LIGHT_SUPPLY",
            "HOSPITAL_TRANSFER_TEST",
            "FLOOD_ISOLATION_TEST",
            "WEST_MAIN_COMMISSIONING_TEST",
            "SOUTH_SOURCE_COMMISSIONING_TEST",
        ];
        Check(slice.EmittedTransitions
                .Where(item => item.Kind == RealtimeTransitionKind.EventStarted)
                .Select(item => item.EventId!)
                .SequenceEqual(exactEventOrder, StringComparer.Ordinal),
            "tutorial native event-start order",
            failures);
        Check(observedRailEvents.SequenceEqual(exactEventOrder, StringComparer.Ordinal),
            "tutorial cumulative one-line rail event order",
            failures);
    }

    private static void ValidateCumulativeEventDutyResume(
        ICollection<string> failures)
    {
        var live = new RealtimeSliceMain();
        try
        {
            live.BootstrapNativeReleaseForSmoke(
                RealtimeNativeRouteCatalog.ProductCampaign);
        }
        catch
        {
            live.Free();
            throw;
        }
        using var liveLifetime = live.FreeAfterSmoke();
        RealtimeSliceData data = live.SliceDataForSmoke;
        RealtimeCampaignSnapshot initialSnapshot = live.CoreSnapshot;
        string initialHash = live.CanonicalStateSha256;
        RealtimeTransition[] initialHistory = live.EmittedTransitions.ToArray();
        RequireAuthoredTutorialModal(
            live,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "FIRST_LIGHT",
            null,
            data.BaseCampaign.Chapters[0].Briefing,
            failures);
        Check(initialSnapshot.CommandCount == 0 &&
              initialSnapshot.ChapterStarted &&
              initialSnapshot.ChapterIndex == 0 &&
              initialSnapshot.Minute == initialSnapshot.ChapterStartMinute &&
              initialSnapshot.PendingTransitions.Count == 0 &&
              initialHistory.Count(item =>
                  item.Kind == RealtimeTransitionKind.ChapterStarted) == 1 &&
              initialHistory.All(item => item.Minute == initialSnapshot.Minute) &&
              live.ActiveChapterStoryModalForSmoke is
              {
                  ModalId: RealtimeR2Ids.ChapterBriefingModal,
                  Purpose: RealtimeChapterStoryModalPurpose.ChapterBriefing,
                  ChapterId: "FIRST_LIGHT",
              },
            "cumulative initial delivery did not open the counted authored briefing once",
            failures);

        RealtimeCampaignSave initialActiveSave = live.CaptureProgressForSmoke();
        Check(initialActiveSave.SchemaVersion ==
                  RealtimeCampaignSave.SupportedSchemaVersion &&
              initialActiveSave.Commands.Count == 0 &&
              initialActiveSave.ClosedStoryCount == 0,
            "active initial briefing did not capture the exact current c0 boundary",
            failures);
        RealtimeSliceMain initialActiveResumed =
            ResumeProductProgress(initialActiveSave);
        using var initialActiveResumedLifetime =
            initialActiveResumed.FreeAfterSmoke();
        Check(RealtimeStateCanonicalizer.StructuralEquals(
                  initialSnapshot,
                  initialActiveResumed.CoreSnapshot) &&
              initialActiveResumed.CanonicalStateSha256 == initialHash &&
              initialActiveResumed.AcceptedCommands.Count == 0 &&
              initialActiveResumed.EmittedTransitions.SequenceEqual(initialHistory),
            "active initial briefing resume redelivered or changed Core bootstrap state",
            failures);
        RequireAuthoredTutorialModal(
            initialActiveResumed,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "FIRST_LIGHT",
            null,
            data.BaseCampaign.Chapters[0].Briefing,
            failures);
        Check(live.ClosePresentedStoryModalForSmoke() is null &&
              initialActiveResumed.ClosePresentedStoryModalForSmoke() is null,
            "cumulative resume FIRST_LIGHT briefing did not close",
            failures);

        RealtimeCampaignSave initialIdleSave =
            initialActiveResumed.CaptureProgressForSmoke();
        Check(initialIdleSave.Commands.Count == 0 &&
              initialIdleSave.ClosedStoryCount == 1,
            "closed initial briefing did not capture the exact current c1 boundary",
            failures);
        RealtimeSliceMain initialIdleResumed = ResumeProductProgress(initialIdleSave);
        using var initialIdleResumedLifetime = initialIdleResumed.FreeAfterSmoke();
        Check(RealtimeStateCanonicalizer.StructuralEquals(
                  initialSnapshot,
                  initialIdleResumed.CoreSnapshot) &&
              initialIdleResumed.CanonicalStateSha256 == initialHash &&
              initialIdleResumed.AcceptedCommands.Count == 0 &&
              initialIdleResumed.EmittedTransitions.SequenceEqual(initialHistory) &&
              initialIdleResumed.ActiveChapterStoryModalForSmoke is null &&
              initialIdleResumed.LatestPresentation.Modal is null &&
              initialIdleResumed.InteractionState is
              {
                  Simulation: RealtimeSimulationState.PlayerPaused,
                  RunningSpeed: RealtimeSimulationSpeed.Normal,
                  ActiveModalId: null,
              },
            "closed initial briefing resume did not restore exact paused story-idle state",
            failures);
        RealtimeCampaignSave skippedInitial = initialIdleSave with
        {
            ClosedStoryCount = 2,
        };
        Check(ThrowsInvalidOperation(() =>
            {
                var invalid = new RealtimeSliceMain();
                try
                {
                    invalid.BootstrapNativeResumeForSmoke(
                        RealtimeNativeRouteCatalog.ProductCampaign,
                        skippedInitial);
                }
                finally
                {
                    invalid.Free();
                }
            }),
            "zero-command initial resume accepted a cursor beyond c1",
            failures);

        (string initialToolId, CoreMapPoint initialPosition) =
            initialActiveResumed.AcceptedNodeDraftForSmoke();
        RequireIntent(initialActiveResumed.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildNode,
                    initialToolId)),
            "command-bearing initial node tool",
            failures,
            coreCommandExpected: false);
        RequireIntent(initialActiveResumed.ApplyIntentForSmoke(
                new RealtimeR2Intent(
                    RealtimeR2IntentKind.SetNodeDraft,
                    FirstId: initialToolId[
                        RealtimeR2Ids.NodeToolPrefix.Length..],
                    Position: initialPosition)),
            "command-bearing initial node draft",
            failures);
        RequireIntent(initialActiveResumed.ApplyIntentForSmoke(
                new RealtimeR2Intent(RealtimeR2IntentKind.OrderNode)),
            "command-bearing initial node order",
            failures);
        RealtimeCampaignSave commandedInitial =
            initialActiveResumed.CaptureProgressForSmoke();
        Check(commandedInitial.SavedMinute == initialSnapshot.Minute &&
              commandedInitial.Commands.Count > 0 &&
              commandedInitial.ClosedStoryCount == 1 &&
              initialActiveResumed.CoreSnapshot.Construction.ActiveConstruction
                  is not null,
            "command-bearing exact-initial story-idle state did not capture c1",
            failures);
        RealtimeCampaignSave resurrectedInitial = commandedInitial with
        {
            ClosedStoryCount = 0,
        };
        Check(ThrowsInvalidOperation(() =>
            {
                var invalid = new RealtimeSliceMain();
                try
                {
                    invalid.BootstrapNativeResumeForSmoke(
                        RealtimeNativeRouteCatalog.ProductCampaign,
                        resurrectedInitial);
                }
                finally
                {
                    invalid.Free();
                }
            }),
            "command-bearing c1 cursor tamper resurrected the initial briefing",
            failures);

        _ = initialIdleResumed.AdvanceToForSmoke(initialSnapshot.Minute + 1);
        Check(ThrowsInvalidOperation(() =>
                initialIdleResumed.CaptureProgressForSmoke()),
            "zero-command progress remained capturable after the exact initial minute",
            failures);

        _ = BuildTutorialFirstLightNetwork(live, failures);
        _ = live.AdvanceToForSmoke(1320);
        RequireAuthoredTutorialModal(
            live,
            RealtimeChapterStoryModalPurpose.ChapterResult,
            "FIRST_LIGHT",
            null,
            data.BaseCampaign.Chapters[0].ResultCards.Standard!,
            failures);
        Check(live.ClosePresentedStoryModalForSmoke() is not null,
            "cumulative resume FIRST_LIGHT result did not queue SECOND_HEART briefing",
            failures);
        RequireAuthoredTutorialModal(
            live,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "SECOND_HEART",
            null,
            data.BaseCampaign.Chapters[1].Briefing,
            failures);
        Check(live.ClosePresentedStoryModalForSmoke() is null,
            "cumulative resume did not reach a story-idle SECOND_HEART boundary",
            failures);

        _ = live.AdvanceToForSmoke(1800);
        RequireAuthoredTutorialModal(
            live,
            RealtimeChapterStoryModalPurpose.EventStory,
            "SECOND_HEART",
            "FLOOD_ISOLATION_TEST",
            data.BaseCampaign.Chapters[1].OperatingPhases[1].Story!,
            failures);
        RealtimeCampaignSnapshot eventSnapshot = live.CoreSnapshot;
        Check(eventSnapshot.ActiveEventStates.Single().EventId ==
                  "FLOOD_ISOLATION_TEST" &&
              eventSnapshot.ActiveDuty is
              {
                  EventId: "FLOOD_ISOLATION_TEST",
              } &&
              eventSnapshot.PendingTransitions.Count == 0 &&
              live.ActiveChapterStoryModalForSmoke is
              {
                  Purpose: RealtimeChapterStoryModalPurpose.EventStory,
                  ChapterId: "SECOND_HEART",
                  EventId: "FLOOD_ISOLATION_TEST",
              } activeStory &&
              live.LatestPresentation.Modal?.Id == activeStory.ModalId &&
              RealtimeSession.IsJournalRestorableProgressSnapshot(eventSnapshot),
            "cumulative resume did not reach an active FLOOD story boundary",
            failures);

        RealtimeCampaignSnapshot expectedSnapshot = live.CoreSnapshot;
        string expectedHash = live.CanonicalStateSha256;
        TimedRealtimeCommand[] expectedJournal = live.AcceptedCommands.ToArray();
        RealtimeTransition[] expectedTransitions = live.EmittedTransitions.ToArray();
        RealtimeCampaignSave save = live.CaptureProgressForSmoke();
        Check(save.SchemaVersion == RealtimeCampaignSave.SupportedSchemaVersion &&
              save.ClosedStoryCount == 3,
            "cumulative active story did not capture the exact v3 closed prefix",
            failures);
        _ = live.AdvanceToForSmoke(1801);
        Check(ThrowsInvalidOperation(() => live.CaptureProgressForSmoke()),
            "cumulative active story remained capturable after its trigger minute",
            failures);

        RealtimeCampaignSave priorV2 = save with
        {
            SchemaVersion = RealtimeCampaignSave.PriorSchemaVersion,
            ClosedStoryCount = save.ClosedStoryCount - 1,
        };
        RealtimeSliceMain priorV2Resumed = ResumeProductProgress(priorV2);
        using var priorV2ResumedLifetime = priorV2Resumed.FreeAfterSmoke();
        RequireAuthoredTutorialModal(
            priorV2Resumed,
            RealtimeChapterStoryModalPurpose.EventStory,
            "SECOND_HEART",
            "FLOOD_ISOLATION_TEST",
            data.BaseCampaign.Chapters[1].OperatingPhases[1].Story!,
            failures);
        RealtimeCampaignSave normalizedV3 =
            priorV2Resumed.CaptureProgressForSmoke();
        Check(normalizedV3.SchemaVersion ==
                  RealtimeCampaignSave.SupportedSchemaVersion &&
              normalizedV3.ClosedStoryCount == save.ClosedStoryCount,
            "prior v2 cursor was not normalized by +1 into the current v3 prefix",
            failures);
        RealtimeCampaignSave invalidPriorV2 = priorV2 with
        {
            ClosedStoryCount = save.ClosedStoryCount + 1,
        };
        Check(ThrowsInvalidOperation(() =>
            {
                var invalid = new RealtimeSliceMain();
                try
                {
                    invalid.BootstrapNativeResumeForSmoke(
                        RealtimeNativeRouteCatalog.ProductCampaign,
                        invalidPriorV2);
                }
                finally
                {
                    invalid.Free();
                }
            }),
            "prior v2 resume accepted a normalized cursor beyond projected history",
            failures);

        var resumed = new RealtimeSliceMain();
        try
        {
            resumed.BootstrapNativeResumeForSmoke(
                RealtimeNativeRouteCatalog.ProductCampaign,
                save);
        }
        catch
        {
            resumed.Free();
            throw;
        }
        using var resumedLifetime = resumed.FreeAfterSmoke();
        Check(RealtimeStateCanonicalizer.StructuralEquals(
                  expectedSnapshot,
                  resumed.CoreSnapshot) &&
              string.Equals(
                  expectedHash,
                  resumed.CanonicalStateSha256,
                  StringComparison.Ordinal) &&
              resumed.AcceptedCommands.SequenceEqual(expectedJournal) &&
              resumed.EmittedTransitions.SequenceEqual(expectedTransitions) &&
              resumed.CoreSnapshot.ChapterIndex == 1 &&
              resumed.CoreSnapshot.CompletedChapters.Select(item => item.ChapterId)
                  .SequenceEqual(new[] { "FIRST_LIGHT" }, StringComparer.Ordinal),
            "cumulative event/duty resume lost Core state, journal, or history",
            failures);
        RequireAuthoredTutorialModal(
            resumed,
            RealtimeChapterStoryModalPurpose.EventStory,
            "SECOND_HEART",
            "FLOOD_ISOLATION_TEST",
            data.BaseCampaign.Chapters[1].OperatingPhases[1].Story!,
            failures);
        Check(resumed.InteractionState is
              {
                  Simulation: RealtimeSimulationState.AutoPaused,
                  RunningSpeed: RealtimeSimulationSpeed.Normal,
                  ActiveModalId: var resumedStoryId,
              } &&
              resumed.ActiveChapterStoryModalForSmoke?.ModalId == resumedStoryId &&
              resumed.LatestPresentation.Modal?.Id == resumedStoryId &&
              resumed.AccumulatorSnapshot.Paused,
            "cumulative event/duty resume did not reopen the saved story",
            failures);
        Check(live.ClosePresentedStoryModalForSmoke() is null &&
              resumed.ClosePresentedStoryModalForSmoke() is null &&
              live.ActiveChapterStoryModalForSmoke is null &&
              resumed.ActiveChapterStoryModalForSmoke is null &&
              resumed.InteractionState is
              {
                  Simulation: RealtimeSimulationState.PlayerPaused,
                  RunningSpeed: RealtimeSimulationSpeed.Normal,
                  ActiveModalId: null,
              } &&
              resumed.LatestPresentation.Modal is null,
            "closing the restored FLOOD story did not return to paused play",
            failures);

        _ = live.AdvanceToForSmoke(1860);
        _ = resumed.AdvanceToForSmoke(1860);
        Check(RealtimeStateCanonicalizer.StructuralEquals(
                  live.CoreSnapshot,
                  resumed.CoreSnapshot) &&
              string.Equals(
                  live.CanonicalStateSha256,
                  resumed.CanonicalStateSha256,
                  StringComparison.Ordinal) &&
              live.EmittedTransitions.SequenceEqual(resumed.EmittedTransitions),
            "cumulative event/duty resume diverged at the next chapter result",
            failures);
        RealtimeChapterStoryModalRequest? nextStory =
            resumed.ActiveChapterStoryModalForSmoke;
        Check(nextStory is
              {
                  Purpose: RealtimeChapterStoryModalPurpose.ChapterResult,
                  ChapterId: "SECOND_HEART",
                  EventId: null,
              } &&
              resumed.LatestPresentation.Modal?.Id ==
                  RealtimeR2Ids.TutorialResultModal("SECOND_HEART") &&
              live.ActiveChapterStoryModalForSmoke?.ModalId == nextStory.ModalId,
            "cumulative resume replayed the closed event story or missed the next result",
            failures);
        RealtimeCampaignSnapshot resultSnapshot = resumed.CoreSnapshot;
        string resultHash = resumed.CanonicalStateSha256;
        TimedRealtimeCommand[] resultJournal = resumed.AcceptedCommands.ToArray();
        RealtimeTransition[] resultHistory = resumed.EmittedTransitions.ToArray();
        RealtimeModalPresentation resultModal = resumed.LatestPresentation.Modal!;
        RealtimeCampaignSave resultSave = resumed.CaptureProgressForSmoke();
        Check(resultSave.ClosedStoryCount == 4,
            "zero-gap result did not capture its bounded briefing suffix",
            failures);
        RealtimeSliceMain resultResumed = ResumeProductProgress(resultSave);
        using var resultResumedLifetime = resultResumed.FreeAfterSmoke();
        Check(RealtimeStateCanonicalizer.StructuralEquals(
                  resultSnapshot,
                  resultResumed.CoreSnapshot) &&
              resultResumed.CanonicalStateSha256 == resultHash &&
              resultResumed.AcceptedCommands.SequenceEqual(resultJournal) &&
              resultResumed.EmittedTransitions.SequenceEqual(resultHistory),
            "zero-gap result resume lost Core state, journal, or history",
            failures);
        Check(resultResumed.ActiveChapterStoryModalForSmoke is
              {
                  Purpose: RealtimeChapterStoryModalPurpose.ChapterResult,
                  ChapterId: "SECOND_HEART",
              } restoredResult &&
              resultResumed.LatestPresentation.Modal is { } restoredResultModal &&
              restoredResultModal.Id == restoredResult.ModalId &&
              restoredResultModal.Eyebrow == resultModal.Eyebrow &&
              restoredResultModal.Heading == resultModal.Heading &&
              restoredResultModal.Body == resultModal.Body,
            "zero-gap result resume changed the presented result card",
            failures);

        Check(live.ClosePresentedStoryModalForSmoke() is not null &&
              resumed.ClosePresentedStoryModalForSmoke() is not null &&
              resultResumed.ClosePresentedStoryModalForSmoke() is not null,
            "zero-gap result did not open its queued next briefing",
            failures);
        RequireAuthoredTutorialModal(
            resultResumed,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "SECOND_SOURCE",
            null,
            data.BaseCampaign.Chapters[2].Briefing,
            failures);
        Check(resultResumed.CanonicalStateSha256 == resultHash &&
              resultResumed.AcceptedCommands.SequenceEqual(resultJournal) &&
              resultResumed.EmittedTransitions.SequenceEqual(resultHistory),
            "zero-gap result close changed Core state or replayed its transition batch",
            failures);

        RealtimeCampaignSave briefingSave = resultResumed.CaptureProgressForSmoke();
        Check(briefingSave.ClosedStoryCount == 5,
            "zero-gap briefing did not capture its exact closed prefix",
            failures);
        RealtimeSliceMain briefingResumed = ResumeProductProgress(briefingSave);
        using var briefingResumedLifetime = briefingResumed.FreeAfterSmoke();
        Check(RealtimeStateCanonicalizer.StructuralEquals(
                  resultSnapshot,
                  briefingResumed.CoreSnapshot) &&
              briefingResumed.CanonicalStateSha256 == resultHash &&
              briefingResumed.AcceptedCommands.SequenceEqual(resultJournal) &&
              briefingResumed.EmittedTransitions.SequenceEqual(resultHistory),
            "zero-gap briefing resume lost Core state, journal, or history",
            failures);
        RequireAuthoredTutorialModal(
            briefingResumed,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "SECOND_SOURCE",
            null,
            data.BaseCampaign.Chapters[2].Briefing,
            failures);
        Check(live.ClosePresentedStoryModalForSmoke() is null &&
              resumed.ClosePresentedStoryModalForSmoke() is null &&
              resultResumed.ClosePresentedStoryModalForSmoke() is null &&
              briefingResumed.ClosePresentedStoryModalForSmoke() is null &&
              briefingResumed.InteractionState is
              {
                  Simulation: RealtimeSimulationState.PlayerPaused,
                  RunningSpeed: RealtimeSimulationSpeed.Normal,
                  ActiveModalId: null,
              },
            "zero-gap briefing resume did not close once into paused play",
            failures);

        _ = briefingResumed.AdvanceToForSmoke(2400);
        RequireAuthoredTutorialModal(
            briefingResumed,
            RealtimeChapterStoryModalPurpose.EventStory,
            "SECOND_SOURCE",
            "SOUTH_SOURCE_COMMISSIONING_TEST",
            data.BaseCampaign.Chapters[2].OperatingPhases[1].Story!,
            failures);
        Check(briefingResumed.ClosePresentedStoryModalForSmoke() is null,
            "long-gap setup did not close the SOUTH_SOURCE story",
            failures);
        _ = briefingResumed.AdvanceToForSmoke(2460);
        Check(briefingResumed.CoreSnapshot is
              {
                  ChapterStarted: false,
                  CampaignComplete: false,
                  ChapterStartMinute: 265260,
              },
            "long-gap setup did not stop at the typed between-chapter boundary",
            failures);
        Check(briefingResumed.ActiveChapterStoryModalForSmoke is
              {
                  Purpose: RealtimeChapterStoryModalPurpose.ChapterResult,
                  ChapterId: "SECOND_SOURCE",
              } && briefingResumed.LatestPresentation.Modal is not null,
            "long-gap setup did not present the SECOND_SOURCE result",
            failures);

        RealtimeCampaignSnapshot gapSnapshot = briefingResumed.CoreSnapshot;
        string gapHash = briefingResumed.CanonicalStateSha256;
        TimedRealtimeCommand[] gapJournal = briefingResumed.AcceptedCommands.ToArray();
        RealtimeTransition[] gapHistory = briefingResumed.EmittedTransitions.ToArray();
        RealtimeModalPresentation gapResultModal =
            briefingResumed.LatestPresentation.Modal!;
        RealtimeCampaignSave gapSave = briefingResumed.CaptureProgressForSmoke();
        Check(gapSave.ClosedStoryCount == 7,
            "long-gap result did not capture its exact closed prefix",
            failures);
        RealtimeCampaignSave skippedGapResult = gapSave with
        {
            ClosedStoryCount = gapSave.ClosedStoryCount + 1,
        };
        Check(ThrowsInvalidOperation(() =>
            {
                var invalid = new RealtimeSliceMain();
                try
                {
                    invalid.BootstrapNativeResumeForSmoke(
                        RealtimeNativeRouteCatalog.ProductCampaign,
                        skippedGapResult);
                }
                finally
                {
                    invalid.Free();
                }
            }),
            "between-chapter save accepted a story-idle cursor that skipped the result",
            failures);

        RealtimeSliceMain gapResumed = ResumeProductProgress(gapSave);
        using var gapResumedLifetime = gapResumed.FreeAfterSmoke();
        Check(RealtimeStateCanonicalizer.StructuralEquals(
                  gapSnapshot,
                  gapResumed.CoreSnapshot) &&
              gapResumed.CanonicalStateSha256 == gapHash &&
              gapResumed.AcceptedCommands.SequenceEqual(gapJournal) &&
              gapResumed.EmittedTransitions.SequenceEqual(gapHistory),
            "long-gap result resume lost Core state, journal, or history",
            failures);
        Check(gapResumed.ActiveChapterStoryModalForSmoke is
              {
                  Purpose: RealtimeChapterStoryModalPurpose.ChapterResult,
                  ChapterId: "SECOND_SOURCE",
              } restoredGapResult &&
              gapResumed.LatestPresentation.Modal is { } restoredGapModal &&
              restoredGapModal.Id == restoredGapResult.ModalId &&
              restoredGapModal.Eyebrow == gapResultModal.Eyebrow &&
              restoredGapModal.Heading == gapResultModal.Heading &&
              restoredGapModal.Body == gapResultModal.Body,
            "long-gap result resume changed the presented result card",
            failures);
        Check(briefingResumed.ClosePresentedStoryModalForSmoke() is not null &&
              gapResumed.ClosePresentedStoryModalForSmoke() is not null &&
              briefingResumed.CoreSnapshot.Minute == 265260 &&
              RealtimeStateCanonicalizer.StructuralEquals(
                  briefingResumed.CoreSnapshot,
                  gapResumed.CoreSnapshot) &&
              briefingResumed.CanonicalStateSha256 == gapResumed.CanonicalStateSha256 &&
              briefingResumed.EmittedTransitions.SequenceEqual(
                  gapResumed.EmittedTransitions),
            "long-gap result did not advance exactly once to the next chapter",
            failures);
        RequireAuthoredTutorialModal(
            gapResumed,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "NORTH_BANK_PROMISE",
            null,
            data.BaseCampaign.Chapters[3].Briefing,
            failures);

        RealtimeCampaignSave northBriefingSave = gapResumed.CaptureProgressForSmoke();
        Check(northBriefingSave.ClosedStoryCount == 8,
            "North briefing did not preserve its queued decision suffix",
            failures);
        RealtimeSliceMain northBriefingResumed =
            ResumeProductProgress(northBriefingSave);
        using var northBriefingResumedLifetime = northBriefingResumed.FreeAfterSmoke();
        RequireAuthoredTutorialModal(
            northBriefingResumed,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "NORTH_BANK_PROMISE",
            null,
            data.BaseCampaign.Chapters[3].Briefing,
            failures);
        Check(briefingResumed.ClosePresentedStoryModalForSmoke() is not null &&
              gapResumed.ClosePresentedStoryModalForSmoke() is not null &&
              northBriefingResumed.ClosePresentedStoryModalForSmoke() is not null,
            "restored North briefing did not open its queued decision story",
            failures);
        CommercialStoryCard northPlanning = data.BaseCampaign.Chapters[3]
            .DecisionWindows.Single(item =>
                item.WindowId == "NORTH_BANK_PLANNING_WINDOW").Story!;
        RequireAuthoredTutorialModal(
            northBriefingResumed,
            RealtimeChapterStoryModalPurpose.DecisionWindowStory,
            "NORTH_BANK_PROMISE",
            null,
            northPlanning,
            failures);
        Check(briefingResumed.ClosePresentedStoryModalForSmoke() is null &&
              gapResumed.ClosePresentedStoryModalForSmoke() is null &&
              northBriefingResumed.ClosePresentedStoryModalForSmoke() is null &&
              northBriefingResumed.InteractionState is
              {
                  Simulation: RealtimeSimulationState.PlayerPaused,
                  RunningSpeed: RealtimeSimulationSpeed.Normal,
                  ActiveModalId: null,
              } &&
              briefingResumed.EmittedTransitions.SequenceEqual(
                  gapResumed.EmittedTransitions) &&
              gapResumed.EmittedTransitions.SequenceEqual(
                  northBriefingResumed.EmittedTransitions),
            "long-gap briefing/decision handoff replayed or reordered its FIFO",
            failures);
    }

    private static RealtimeSliceMain ResumeProductProgress(
        RealtimeCampaignSave save)
    {
        var resumed = new RealtimeSliceMain();
        try
        {
            resumed.BootstrapNativeResumeForSmoke(
                RealtimeNativeRouteCatalog.ProductCampaign,
                save);
            return resumed;
        }
        catch
        {
            resumed.Free();
            throw;
        }
    }

    private static bool ProductResumeRejected(RealtimeCampaignSave save) =>
        ThrowsInvalidOperation(() =>
        {
            var invalid = new RealtimeSliceMain();
            try
            {
                invalid.BootstrapNativeResumeForSmoke(
                    RealtimeNativeRouteCatalog.ProductCampaign,
                    save);
            }
            finally
            {
                invalid.Free();
            }
        });

    private static void ValidateFailedTerminalCompletion(
        RealtimeSliceData data,
        ICollection<string> failures)
    {
        var run = new RealtimeCampaignRun(data.Campaign, data.World);
        var history = new List<RealtimeTransition>();
        history.AddRange(run.AdvanceTo(run.Minute).Transitions);
        RealtimeCommandResult draft = run.ApplyCommand(RealtimeCommand.SetNodeDraft(
            "SMALL_SUBSTATION",
            new CoreMapPoint(2100, 700)));
        RealtimeCommandResult cancel = run.ApplyCommand(RealtimeCommand.CancelNodeDraft());
        if (!draft.Accepted || !cancel.Accepted)
        {
            throw new InvalidOperationException(
                $"Unable to stage failed terminal progress: {draft.Error}; {cancel.Error}.");
        }
        history.AddRange(draft.Transitions);
        history.AddRange(cancel.Transitions);

        int finalChapterIndex = data.Campaign.Chapters.Count - 1;
        for (int guard = 0;
             guard < data.Campaign.Chapters.Count * 2 &&
             run.GetSnapshot().ChapterIndex < finalChapterIndex;
             guard++)
        {
            RealtimeCampaignSnapshot snapshot = run.GetSnapshot();
            long target = snapshot.ChapterStarted
                ? checked(snapshot.ChapterStartMinute + snapshot.Chapter.EndOffsetMinutes)
                : snapshot.ChapterStartMinute;
            history.AddRange(run.AdvanceTo(target).Transitions);
        }

        RealtimeCampaignSnapshot finalStart = run.GetSnapshot();
        var storyFlow = new RealtimeChapterStoryFlow();
        storyFlow.Restore(
            history,
            data.Campaign,
            closedStoryCount: null,
            run.Minute);
        if (finalStart.ChapterIndex != finalChapterIndex ||
            !finalStart.ChapterStarted ||
            finalStart.CampaignComplete ||
            !storyFlow.IsIdle ||
            !storyFlow.MatchesSnapshot(finalStart))
        {
            throw new InvalidOperationException(
                "Unable to stage an idle final-chapter progress boundary.");
        }
        RealtimeCampaignSave finalStartSave = RealtimeCampaignSaveCodec.Capture(
            data.RequireSaveSourceIdentity(),
            data.Campaign,
            data.World,
            run,
            storyFlow.ClosedStoryCount);

        RealtimeSliceMain failedSlice = ResumeProductProgress(finalStartSave);
        using var failedLifetime = failedSlice.FreeAfterSmoke();
        Check(failedSlice.InteractionState.Simulation ==
                  RealtimeSimulationState.PlayerPaused &&
              failedSlice.ActiveChapterStoryModalForSmoke is null &&
              failedSlice.LatestPresentation.Modal is null,
            "failed final progress did not resume at the idle paused boundary",
            failures);
        failedSlice.SetPlayerPausedForSmoke(false);
        _ = failedSlice.AdvanceToForSmoke(checked(
            finalStart.ChapterStartMinute + finalStart.Chapter.EndOffsetMinutes));

        bool closedFailedFinal = false;
        for (int guard = 0;
             guard < 16 && failedSlice.ActiveChapterStoryModalForSmoke is { } story;
             guard++)
        {
            if (story.FinalResult)
            {
                closedFailedFinal = true;
                Check(failedSlice.CoreSnapshot.CompletedChapters.Count == 8 &&
                      !failedSlice.CoreSnapshot.CompletedChapters[^1].ObjectiveSatisfied,
                    "the staged final result was not the required failed outcome",
                    failures);
            }
            _ = failedSlice.ClosePresentedStoryModalForSmoke();
        }

        RealtimeCampaignSnapshot failed = failedSlice.CoreSnapshot;
        Check(closedFailedFinal &&
              failed.CampaignComplete &&
              !failed.ChapterStarted &&
              !failed.CompletedChapters[^1].ObjectiveSatisfied &&
              failedSlice.ActiveChapterStoryModalForSmoke is null &&
              failedSlice.ActiveEpilogueModalForSmoke is null &&
              !failedSlice.EpilogueStartedForSmoke &&
              !failedSlice.EpilogueCompletedForSmoke &&
              failedSlice.LatestPresentation.Modal is null &&
              failedSlice.InteractionState.Simulation == RealtimeSimulationState.Ended,
            "failed final result did not close directly into an ended no-epilogue world",
            failures);

        RealtimeCampaignSave terminal = failedSlice.CaptureProgressForSmoke();
        RealtimeSliceMain resumed = ResumeProductProgress(terminal);
        using var resumedLifetime = resumed.FreeAfterSmoke();
        Check(RealtimeStateCanonicalizer.StructuralEquals(failed, resumed.CoreSnapshot) &&
              resumed.CanonicalStateSha256 == terminal.CanonicalStateSha256 &&
              resumed.AcceptedCommands.SequenceEqual(terminal.Commands) &&
              resumed.ActiveChapterStoryModalForSmoke is null &&
              resumed.ActiveEpilogueModalForSmoke is null &&
              !resumed.EpilogueStartedForSmoke &&
              !resumed.EpilogueCompletedForSmoke &&
              resumed.LatestPresentation.Modal is null &&
              resumed.InteractionState.Simulation == RealtimeSimulationState.Ended &&
              resumed.RetainedFrameDebt.Count == 0,
            "failed terminal Continue did not restore the exact ended no-epilogue world",
            failures);
        RealtimeCampaignSave recaptured = resumed.CaptureProgressForSmoke();
        Check(recaptured.SavedMinute == terminal.SavedMinute &&
              recaptured.CanonicalStateSha256 == terminal.CanonicalStateSha256 &&
              recaptured.Commands.SequenceEqual(terminal.Commands) &&
              recaptured.ClosedStoryCount == terminal.ClosedStoryCount,
            "failed terminal Continue could not reproduce its current save",
            failures);
    }

    private static RealtimeCampaignSave ValidateReleaseThroughLongestNightController(
        ICollection<string> failures)
    {
        var slice = new RealtimeSliceMain();
        try
        {
            slice.BootstrapNativeReleaseForSmoke(
                RealtimeNativeRouteCatalog.ProductCampaign);
        }
        catch
        {
            slice.Free();
            throw;
        }
        using var sliceLifetime = slice.FreeAfterSmoke();
        Check(!slice.OwnsProductProgressForSmoke,
            "explicit cumulative development route acquired product-save ownership",
            failures);
        (RealtimeSliceData data, string rootSubstationId) =
            AdvanceReleasePrefixToNorthBankPlanning(slice, failures);

        const string markerId =
            RealtimeR2Ids.PromiseDecisionMarkerPrefix + "NORTH_BANK_MOVE_IN_PROMISE";
        RealtimeTimelineItemPresentation deadline = slice.LatestPresentation.Rail.Items
            .Single(item => string.Equals(item.Id, markerId, StringComparison.Ordinal));
        Check(deadline.Kind == RealtimeTimelineItemKind.Decision &&
              deadline.Lane == RealtimeTimelineLane.DemandAndDeadline &&
              deadline.StartMinute == 265680 &&
              deadline.Visibility == RealtimeTimelineVisibility.Announced &&
              deadline.Description.Contains("선택 전 Keep 가정", StringComparison.Ordinal) &&
              deadline.Description.Contains("마감 전 변경 가능", StringComparison.Ordinal) &&
              slice.LatestPresentation.Rail.NextEvent is
              {
                  EventId: markerId,
                  StartMinute: 265680,
              } &&
              slice.LatestPresentation.Rail.NextEvent.StartMinute <
                  data.Campaign.Chapters[3].ScheduledEvents[0].StartOffsetMinutes +
                  slice.CoreSnapshot.ChapterStartMinute,
            "North Bank deadline did not become the compact one-line next decision",
            failures);
        AssertNoUnknownRailTargets(
            slice,
            slice.LatestPresentation,
            failures,
            "north-bank-unset");

        string beforeSelectionHash = slice.CanonicalStateSha256;
        long beforeSelectionMinute = slice.CoreSnapshot.Minute;
        int beforeSelectionCommands = slice.CoreSnapshot.CommandCount;
        RequireIntent(slice.ApplyIntentForSmoke(RealtimeR2Intent.SetTimelineMarker(
                markerId,
                markerId,
                null,
                slice.InteractionState.TimelineHorizon)),
            "North Bank deadline selection", failures, coreCommandExpected: false);
        RealtimeContextDockPresentation context = slice.LatestPresentation.Context;
        Check(slice.CanonicalStateSha256 == beforeSelectionHash &&
              slice.CoreSnapshot.Minute == beforeSelectionMinute &&
              slice.CoreSnapshot.CommandCount == beforeSelectionCommands &&
              context is
              {
                  SubjectId: markerId,
                  Visible: true,
                  PrimaryAction.Id: RealtimeR2Ids.PromiseKeepAction,
                  PrimaryAction.Enabled: true,
                  SecondaryAction.Id: RealtimeR2Ids.PromiseDeferAction,
                  SecondaryAction.Enabled: true,
              } &&
              context.Sections.Any(item => item.Body.Contains(
                  "미선택", StringComparison.Ordinal)) &&
              context.Sections.Any(item => item.Body.Contains(
                  "Keep 가정", StringComparison.Ordinal)),
            "deadline selection mutated Core or omitted its two authored actions",
            failures);

        int beforeDeferCommands = slice.CoreSnapshot.CommandCount;
        slice.RequestActionForSmoke(RealtimeR2Ids.PromiseDeferAction);
        Check(slice.CoreSnapshot.PromiseDecision == CommercialPromiseDecision.Defer &&
              slice.CoreSnapshot.Minute == beforeSelectionMinute &&
              slice.CoreSnapshot.CommandCount == beforeDeferCommands + 1 &&
              slice.LatestPresentation.BaseForecast.Events.All(item =>
                  item.TemporalProjection.Outcome.DutySegments
                      .SelectMany(segment => segment.Loads)
                      .Where(load => load.LoadId == "NORTH_RESIDENTIAL")
                      .All(load => !load.Required)) &&
              slice.LatestPresentation.Context.Sections.Any(item =>
                  item.Body.Contains("북안 생활권 수요", StringComparison.Ordinal) &&
                  item.Body.Contains("제외", StringComparison.Ordinal)),
            "production Defer action did not immediately remove North duty",
            failures);

        int beforeKeepCommands = slice.CoreSnapshot.CommandCount;
        slice.RequestActionForSmoke(RealtimeR2Ids.PromiseKeepAction);
        Check(slice.CoreSnapshot.PromiseDecision == CommercialPromiseDecision.Keep &&
              slice.CoreSnapshot.Minute == beforeSelectionMinute &&
              slice.CoreSnapshot.CommandCount == beforeKeepCommands + 1 &&
              slice.LatestPresentation.BaseForecast.Events.Any(item =>
                  item.TemporalProjection.Outcome.DutySegments
                      .SelectMany(segment => segment.Loads)
                      .Any(load => load.LoadId == "NORTH_RESIDENTIAL" && load.Required)),
            "production Keep action did not immediately restore North duty",
            failures);

        _ = BuildNorthBankService(
            slice,
            rootSubstationId,
            includeNorth: true,
            failures,
            "kept");
        Check(slice.CoreSnapshot.Minute < 265680 &&
              slice.LatestPresentation.BaseForecast.Events.All(item =>
                  item.TemporalProjection.Outcome.SafetySatisfied &&
                  item.TemporalProjection.Outcome.PromiseSatisfied),
            "completed Keep network did not clear separate safety/promise forecasts",
            failures);

        (RealtimeChapterOutcome outcome, RealtimeModalPresentation result) =
            CompleteNorthBankChapter(slice, data, failures);
        CommercialStoryCard kept = data.BaseCampaign.Chapters[3].ResultCards.Kept!;
        Check(outcome.ObjectiveSatisfied &&
              outcome.PromiseDecision == CommercialPromiseDecision.Keep &&
              result.Eyebrow == kept.Speaker &&
              result.Heading == kept.Title &&
              result.Body == kept.Body,
            "successful explicit Keep did not present the exact kept result",
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is not null &&
              slice.FormativeTutorialResultChapterIdsForSmoke.SequenceEqual(
                  new[]
                  {
                      "FIRST_LIGHT",
                      "SECOND_HEART",
                      "SECOND_SOURCE",
                      "NORTH_BANK_PROMISE",
                  },
                  StringComparer.Ordinal) &&
              !slice.FormativeTutorialFullFlowRecordedForSmoke,
            "explicit Keep did not preserve the ordered four-result chain into WHOSE_MARGIN",
            failures);
        Check(slice.EmittedTransitions
                .Where(item => item.Kind == RealtimeTransitionKind.EventStarted)
                .Select(item => item.EventId!)
                .SequenceEqual(
                    new[]
                    {
                        "FIRST_LIGHT_SUPPLY",
                        "HOSPITAL_TRANSFER_TEST",
                        "FLOOD_ISOLATION_TEST",
                        "WEST_MAIN_COMMISSIONING_TEST",
                        "SOUTH_SOURCE_COMMISSIONING_TEST",
                        "NORTH_BANK_COMMISSIONING",
                        "NEXT_HOT_EVENING_FORECAST",
                    },
                    StringComparer.Ordinal),
            "four-chapter controller event FIFO drifted",
            failures);

        EnterWhoseMarginPlanning(slice, data, failures, "reinforced-keep");
        AssertWhoseMarginPromiseEventScope(slice, failures, "reinforced-keep");
        SelectWhoseMarginPromiseDeadline(slice, failures, "reinforced-keep");
        slice.RequestActionForSmoke(RealtimeR2Ids.PromiseKeepAction);
        BuildWhoseMarginFactoryCorridor(
            slice,
            reinforced: true,
            failures,
            "reinforced-keep");
        Check(slice.CoreSnapshot.Minute < 266400 &&
              slice.LatestPresentation.BaseForecast.Events.All(item =>
                  item.TemporalProjection.Outcome.SafetySatisfied &&
                  item.TemporalProjection.Outcome.PromiseSatisfied),
            "reinforced WHOSE_MARGIN corridor did not clear the authored forecast",
            failures);
        CloseWhoseMarginLateWindow(slice, data, failures, "reinforced-keep");
        AssertWhoseMarginPromiseEventScope(slice, failures, "reinforced-keep-revealed");
        CloseWhoseMarginNightStory(slice, data, failures, "reinforced-keep");
        (RealtimeChapterOutcome whoseOutcome, RealtimeModalPresentation whoseResult) =
            CompleteWhoseMarginChapter(slice, failures, "reinforced-keep");
        CommercialStoryCard whoseKept = data.BaseCampaign.Chapters[4]
            .ResultCards.Kept!;
        Check(whoseOutcome.ObjectiveSatisfied &&
              whoseOutcome.PromiseDecision == CommercialPromiseDecision.Keep &&
              whoseOutcome.Events.Select(item => item.EventId).SequenceEqual(
                  new[] { "HOT_BASE", "NIGHT_SHIFT", "LATE_NIGHT" },
                  StringComparer.Ordinal) &&
              whoseOutcome.Events.All(item =>
                  item.SafetySatisfied && item.PromiseSatisfied) &&
              whoseResult.Eyebrow == whoseKept.Speaker &&
              whoseResult.Heading == whoseKept.Title &&
              whoseResult.Body == whoseKept.Body,
            "reinforced explicit Keep did not produce the exact WHOSE_MARGIN result",
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is not null &&
              !slice.CoreSnapshot.CampaignComplete &&
              slice.FormativeTutorialResultChapterIdsForSmoke.SequenceEqual(
                  new[]
                  {
                      "FIRST_LIGHT",
                      "SECOND_HEART",
                      "SECOND_SOURCE",
                      "NORTH_BANK_PROMISE",
                      "WHOSE_MARGIN",
                  },
                  StringComparer.Ordinal) &&
              !slice.FormativeTutorialFullFlowRecordedForSmoke,
            "WHOSE_MARGIN Keep did not preserve the five-result chain into the flood chapter",
            failures);

        EnterBeforeWaterRisePlanning(
            slice,
            data,
            whoseOutcome.EndingCashUnit,
            failures,
            "flood-keep");
        SelectBeforeWaterRisePromiseDeadline(slice, failures, "flood-keep");
        slice.RequestActionForSmoke(RealtimeR2Ids.PromiseKeepAction);
        BuildBeforeWaterRiseHighlandLine(
            slice,
            reinforced: true,
            failures,
            "flood-keep");
        AssertBeforeWaterRiseForecast(
            slice,
            CommercialPromiseDecision.Keep,
            failures,
            "flood-keep");
        CloseBeforeWaterRiseFloodStory(slice, data, failures, "flood-keep");
        (RealtimeChapterOutcome floodOutcome, RealtimeModalPresentation floodResult) =
            CompleteBeforeWaterRiseChapter(slice, failures, "flood-keep");
        CommercialStoryCard floodKept = data.BaseCampaign.Chapters[5]
            .ResultCards.Kept!;
        Check(floodOutcome.ObjectiveSatisfied &&
              floodOutcome.PromiseDecision == CommercialPromiseDecision.Keep &&
              floodOutcome.ConnectionRequirementAssessment is
              {
                  FrozenForChapter: true,
                  Satisfied: true,
              } keptConnections &&
              keptConnections.Facts.Single().CurrentConnections == 2 &&
              floodOutcome.Events.Single() is
              {
                  EventId: "FLOOD_ARRIVAL",
                  SafetySatisfied: true,
                  PromiseSatisfied: true,
              } &&
              floodResult.Eyebrow == floodKept.Speaker &&
              floodResult.Heading == floodKept.Title &&
              floodResult.Body == floodKept.Body,
            "explicit flood Keep did not preserve inherited 2/2, safe supply, or exact result",
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is not null &&
              !slice.CoreSnapshot.CampaignComplete &&
              slice.FormativeTutorialResultChapterIdsForSmoke.Count == 6 &&
              !slice.FormativeTutorialFullFlowRecordedForSmoke,
            "BEFORE_WATER_RISE Keep did not hand the six-result chain to planned outage",
            failures);

        EnterSwitchOffToProtectPlanning(
            slice,
            data,
            floodOutcome.EndingCashUnit,
            failures);
        BuildSwitchContinuousWaterBranch(slice, failures);
        AdvanceAndAssertSwitchOffToProtectForecast(slice, failures);
        CloseSwitchPlannedOutageStory(slice, data, failures);
        CloseSwitchReturnServiceStory(slice, data, failures);
        (RealtimeChapterOutcome switchOutcome, RealtimeModalPresentation switchResult) =
            CompleteSwitchOffToProtectChapter(slice, failures);
        CommercialStoryCard switchStandard = data.BaseCampaign.Chapters[6]
            .ResultCards.Standard!;
        Check(switchOutcome.ObjectiveSatisfied &&
              switchOutcome.PromiseDecision == CommercialPromiseDecision.Unset &&
              switchOutcome.ConnectionRequirementAssessment is
              {
                  EvaluatedMinute: 267690,
                  FrozenForChapter: true,
                  Satisfied: true,
              } switchConnections &&
              switchConnections.Facts.Single() is
              {
                  NodeId: "WATER_TERMINAL",
                  CurrentConnections: 2,
                  RequiredConnections: 2,
              } &&
              switchOutcome.Events.All(item =>
                  item.SafetySatisfied &&
                  item.PromiseSatisfied &&
                  item.DutySegments.Count > 0 &&
                  item.DutySegments[0].StartMinute == item.StartMinute &&
                  item.DutySegments[^1].EndMinute == item.EndMinute &&
                  item.DutySegments.All(segment =>
                      segment.Loads.Single(load =>
                          load.LoadId == "EAST_RESIDENTIAL") is
                      {
                          Obligation: CommercialObligationKind.OperatingRecord,
                          DemandKw: 700,
                          DeliveredKw: 700,
                          Required: true,
                      })) &&
              switchResult.Eyebrow == switchStandard.Speaker &&
              switchResult.Heading == switchStandard.Title &&
              switchResult.Body == switchStandard.Body,
            "planned outage did not preserve inherited 2/2 or exact standard result",
            failures);
        int longestStartCommandCount = slice.CoreSnapshot.CommandCount;
        long longestStartCashUnit = slice.CoreSnapshot.CashUnit;
        SpatialNodeDefinition[] longestStartNodes = slice.CoreSnapshot.Construction.World
            .Nodes.ToArray();
        SpatialEdgeDefinition[] longestStartEdges = slice.CoreSnapshot.Construction.World
            .Edges.ToArray();
        Check(slice.ClosePresentedStoryModalForSmoke() is not null &&
              !slice.CoreSnapshot.CampaignComplete &&
              slice.FormativeTutorialResultChapterIdsForSmoke.Count == 7 &&
              !slice.FormativeTutorialFullFlowRecordedForSmoke &&
              longestStartCashUnit == checked(
                  switchOutcome.EndingCashUnit +
                  data.BaseCampaign.Chapters[7].BudgetGrantCashUnit) &&
              slice.CoreSnapshot.CommandCount == longestStartCommandCount &&
              slice.CoreSnapshot.CashUnit == longestStartCashUnit &&
              slice.CoreSnapshot.Construction is
              {
                  Phase: ConstructionPhase.Ready,
                  NodeDraft: null,
                  LineDraft: null,
                  ActiveConstruction: null,
              } &&
              slice.CoreSnapshot.Construction.World.Nodes.SequenceEqual(
                  longestStartNodes) &&
              slice.CoreSnapshot.Construction.World.Edges.SequenceEqual(
                  longestStartEdges),
            "SWITCH_OFF_TO_PROTECT did not hand the seven-result chain to the final chapter",
            failures);

        EnterLongestNightPlanning(
            slice,
            data,
            switchOutcome.EndingCashUnit,
            failures);
        Check(slice.CoreSnapshot.CommandCount == longestStartCommandCount &&
              slice.CoreSnapshot.CashUnit == longestStartCashUnit &&
              slice.CoreSnapshot.Construction is
              {
                  Phase: ConstructionPhase.Ready,
                  NodeDraft: null,
                  LineDraft: null,
                  ActiveConstruction: null,
              } &&
              slice.CoreSnapshot.Construction.World.Nodes.SequenceEqual(
                  longestStartNodes) &&
              slice.CoreSnapshot.Construction.World.Edges.SequenceEqual(
                  longestStartEdges),
            "LONGEST_NIGHT planning changed Core command, cash, construction, or world",
            failures);
        AdvanceAndAssertLongestNightForecast(slice, failures);
        RunLongestNightEvents(slice, data, failures);
        (RealtimeChapterOutcome longestOutcome, RealtimeModalPresentation longestResult) =
            CompleteLongestNightChapter(slice, failures);
        CommercialStoryCard longestStandard = data.BaseCampaign.Chapters[7]
            .ResultCards.Standard!;
        Check(longestOutcome.ObjectiveSatisfied &&
              longestOutcome.PromiseDecision == CommercialPromiseDecision.Unset &&
              longestOutcome.ConnectionRequirementAssessment is null &&
              longestOutcome.EndingCashUnit == longestStartCashUnit &&
              slice.CoreSnapshot.CommandCount == longestStartCommandCount &&
              slice.CoreSnapshot.Construction is
              {
                  Phase: ConstructionPhase.Ready,
                  NodeDraft: null,
                  LineDraft: null,
                  ActiveConstruction: null,
              } &&
              slice.CoreSnapshot.Construction.World.Nodes.SequenceEqual(
                  longestStartNodes) &&
              slice.CoreSnapshot.Construction.World.Edges.SequenceEqual(
                  longestStartEdges) &&
              longestOutcome.Events.All(item =>
                  item.SafetySatisfied && item.PromiseSatisfied) &&
              longestResult.Eyebrow == longestStandard.Speaker &&
              longestResult.Heading == longestStandard.Title &&
              longestResult.Body == longestStandard.Body,
            "LONGEST_NIGHT did not preserve safety duty or the exact standard result",
            failures);
        string completedHash = slice.CanonicalStateSha256;
        int completedCommandCount = slice.AcceptedCommandCount;
        long completedMinute = slice.CurrentMinute;
        long completedCash = slice.CoreSnapshot.CashUnit;
        Check(ThrowsInvalidOperation(() => slice.CaptureProgressForSmoke()),
            "the active full-campaign finale was captured as terminal progress",
            failures);
        string[] expectedPromiseLines = data.BaseCampaign.Epilogue.PromiseLines
            .Select(line => slice.CoreSnapshot.CompletedChapters.Single(outcome =>
                string.Equals(
                    outcome.ChapterId,
                    line.ChapterId,
                    StringComparison.Ordinal)).PromiseDecision switch
            {
                CommercialPromiseDecision.Keep => line.Kept,
                CommercialPromiseDecision.Defer => line.Deferred,
                _ => throw new InvalidOperationException(
                    "The completed positive route has an unresolved epilogue promise."),
            })
            .ToArray();
        RealtimeChapterOutcome[] failedFinalOutcomes =
            slice.CoreSnapshot.CompletedChapters.ToArray();
        RealtimeEventOutcome[] failedFinalEvents =
            failedFinalOutcomes[^1].Events.ToArray();
        failedFinalEvents[^1] = failedFinalEvents[^1] with
        {
            SafetySatisfied = false,
        };
        failedFinalOutcomes[^1] = failedFinalOutcomes[^1] with
        {
            Events = failedFinalEvents,
        };
        var failedFinalFlow = new RealtimeEpilogueFlow();
        Check(!failedFinalFlow.TryStart(
                  data.BaseCampaign,
                  data.Campaign,
                  slice.CoreSnapshot with
                  {
                      CompletedChapters = failedFinalOutcomes,
                  }) &&
              !failedFinalFlow.Started &&
              failedFinalFlow.Active is null &&
              !failedFinalFlow.Completed,
            "a failed final chapter incorrectly entered the authored success epilogue",
            failures);
        RealtimeModalPresentation cityReport =
            slice.ClosePresentedStoryModalForSmoke() ??
            throw new InvalidOperationException(
                "The full-campaign finale did not open the city report.");
        RealtimeEpilogueModalRequest cityRequest =
            slice.ActiveEpilogueModalForSmoke ??
            throw new InvalidOperationException("The city report has no typed request.");
        Check(cityRequest.Purpose == RealtimeEpiloguePurpose.CityReport &&
              cityRequest.PromiseLines.SequenceEqual(
                  expectedPromiseLines,
                  StringComparer.Ordinal) &&
              cityRequest.RemainingCashUnit == completedCash &&
              cityReport.Eyebrow == data.BaseCampaign.Epilogue.CityReport.Speaker &&
              cityReport.Heading == data.BaseCampaign.Epilogue.CityReport.Title &&
              cityReport.Body.StartsWith(
                  data.BaseCampaign.Epilogue.CityReport.Body + "\n\n",
                  StringComparison.Ordinal) &&
              expectedPromiseLines.All(line => cityReport.Body.Contains(
                  line,
                  StringComparison.Ordinal)) &&
              cityReport.Body.EndsWith(
                  $"남은 운영 자금 {RealtimePresentationText.Cash(completedCash)}",
                  StringComparison.Ordinal) &&
              cityReport.PrimaryAction.Id == RealtimeR2Ids.EpilogueContinueAction &&
              !cityReport.DismissOnCancel &&
              slice.FormativeTutorialResultChapterIdsForSmoke.SequenceEqual(
                  data.Campaign.Chapters.Select(item => item.Content.ChapterId),
                  StringComparer.Ordinal) &&
              slice.FormativeTutorialFullFlowRecordedForSmoke,
            "the exact finale did not hand off to the authored city report and promise facts",
            failures);
        Check(ThrowsInvalidOperation(() => slice.CaptureProgressForSmoke()),
            "the active city report was captured as terminal progress",
            failures);

        RealtimeModalPresentation medicalWitness =
            slice.ClosePresentedStoryModalForSmoke() ??
            throw new InvalidOperationException(
                "The city report did not open the medical witness.");
        Check(slice.ActiveEpilogueModalForSmoke is
              {
                  Purpose: RealtimeEpiloguePurpose.MedicalWitness,
                  FinalCard: false,
              } medicalRequest &&
              medicalRequest.PromiseLines.Count == 0 &&
              medicalWitness.Eyebrow ==
                  data.BaseCampaign.Epilogue.MedicalWitness.Speaker &&
              medicalWitness.Heading ==
                  data.BaseCampaign.Epilogue.MedicalWitness.Title &&
              medicalWitness.Body == data.BaseCampaign.Epilogue.MedicalWitness.Body,
            "the city report did not hand off to the exact medical witness",
            failures);
        Check(ThrowsInvalidOperation(() => slice.CaptureProgressForSmoke()),
            "the active medical witness was captured as terminal progress",
            failures);

        RealtimeModalPresentation closing =
            slice.ClosePresentedStoryModalForSmoke() ??
            throw new InvalidOperationException(
                "The medical witness did not open the closing record.");
        Check(slice.ActiveEpilogueModalForSmoke is
              {
                  Purpose: RealtimeEpiloguePurpose.Closing,
                  FinalCard: true,
              } closingRequest &&
              closingRequest.PromiseLines.Count == 0 &&
              closing.Eyebrow == data.BaseCampaign.Epilogue.Closing.Speaker &&
              closing.Heading == data.BaseCampaign.Epilogue.Closing.Title &&
              closing.Body == data.BaseCampaign.Epilogue.Closing.Body &&
              closing.PrimaryAction.Label == "완료된 망 보기",
            "the medical witness did not hand off to the exact closing record",
            failures);
        Check(ThrowsInvalidOperation(() => slice.CaptureProgressForSmoke()),
            "the active closing record was captured as terminal progress",
            failures);

        Check(slice.ClosePresentedStoryModalForSmoke() is null &&
              slice.ActiveEpilogueModalForSmoke is null &&
              slice.EpilogueCompletedForSmoke &&
              slice.CoreSnapshot.CampaignComplete &&
              slice.InteractionState.Simulation == RealtimeSimulationState.Ended &&
              slice.CanonicalStateSha256 == completedHash &&
              slice.AcceptedCommandCount == completedCommandCount &&
              slice.CurrentMinute == completedMinute &&
              slice.CoreSnapshot.CashUnit == completedCash &&
              slice.EmittedTransitions
                  .Where(item => item.Kind == RealtimeTransitionKind.EventStarted)
                  .Select(item => item.EventId!)
                  .SequenceEqual(
                      data.Campaign.Chapters.SelectMany(chapter =>
                          chapter.ScheduledEvents).Select(item => item.EventId),
                      StringComparer.Ordinal),
            "the three-card epilogue did not close into the unchanged ended campaign",
            failures);
        RealtimeCampaignSave completedSave = slice.CaptureProgressForSmoke();
        Check(completedSave.SchemaVersion ==
                  RealtimeCampaignSave.SupportedSchemaVersion &&
              completedSave.SavedMinute == completedMinute &&
              completedSave.CanonicalStateSha256 == completedHash &&
              completedSave.Commands.Count == completedCommandCount &&
              completedSave.ClosedStoryCount is > 0,
            "the completed campaign did not produce an exact current terminal save",
            failures);
        RealtimeCampaignSave openFinalCursor = completedSave with
        {
            ClosedStoryCount = completedSave.ClosedStoryCount - 1,
        };
        Check(ProductResumeRejected(openFinalCursor),
            "a nonterminal final-result cursor was accepted as terminal completion",
            failures);
        RealtimeCampaignSave priorCompletion = completedSave with
        {
            SchemaVersion = RealtimeCampaignSave.PriorSchemaVersion,
            ClosedStoryCount = completedSave.ClosedStoryCount - 1,
        };
        Check(ProductResumeRejected(priorCompletion),
            "a prior-v2 completion was accepted as current terminal state",
            failures);
        RealtimeCampaignSave legacyCompletion = completedSave with
        {
            SchemaVersion = RealtimeCampaignSave.LegacySchemaVersion,
            ClosedStoryCount = null,
        };
        Check(ProductResumeRejected(legacyCompletion),
            "a legacy-v1 completion was accepted as current terminal state",
            failures);
        ValidateFailedTerminalCompletion(data, failures);
        return completedSave;
    }

    private static void ValidateReleasePromiseResultBranches(
        ICollection<string> failures)
    {
        // Explicit Defer: safety succeeds, exact deferred authored bytes render,
        // but the native explicit-Keep evidence chain must stay at three chapters.
        var deferredSlice = new RealtimeSliceMain();
        try
        {
            deferredSlice.BootstrapNativeReleaseForSmoke(
                RealtimeNativeRouteCatalog.ProductCampaign);
        }
        catch
        {
            deferredSlice.Free();
            throw;
        }
        using var deferredLifetime = deferredSlice.FreeAfterSmoke();
        (RealtimeSliceData deferredData, string deferredRoot) =
            AdvanceReleasePrefixToNorthBankPlanning(deferredSlice, failures);
        SelectPromiseDeadline(deferredSlice, failures, "explicit-defer");
        deferredSlice.RequestActionForSmoke(RealtimeR2Ids.PromiseDeferAction);
        _ = BuildNorthBankService(
            deferredSlice,
            deferredRoot,
            includeNorth: false,
            failures,
            "explicit-defer");
        (RealtimeChapterOutcome deferredOutcome, RealtimeModalPresentation deferredResult) =
            CompleteNorthBankChapter(deferredSlice, deferredData, failures);
        CommercialStoryCard deferred = deferredData.BaseCampaign.Chapters[3]
            .ResultCards.Deferred!;
        Check(deferredOutcome.ObjectiveSatisfied &&
              deferredOutcome.PromiseDecision == CommercialPromiseDecision.Defer &&
              deferredResult.Eyebrow == deferred.Speaker &&
              deferredResult.Heading == deferred.Title &&
              deferredResult.Body == deferred.Body,
            "explicit Defer did not present exact deferred authored bytes",
            failures);
        _ = deferredSlice.ClosePresentedStoryModalForSmoke();
        Check(deferredSlice.FormativeTutorialResultChapterIdsForSmoke.Count == 3 &&
              !deferredSlice.FormativeTutorialFullFlowRecordedForSmoke,
            "explicit Defer minted the Keep-only formative token",
            failures);

        EnterWhoseMarginPlanning(
            deferredSlice,
            deferredData,
            failures,
            "explicit-whose-defer");
        AssertWhoseMarginPromiseEventScope(
            deferredSlice,
            failures,
            "explicit-whose-defer");
        SelectWhoseMarginPromiseDeadline(
            deferredSlice,
            failures,
            "explicit-whose-defer");
        deferredSlice.RequestActionForSmoke(RealtimeR2Ids.PromiseDeferAction);
        Check(deferredSlice.CoreSnapshot.PromiseDecision ==
                  CommercialPromiseDecision.Defer,
            "WHOSE_MARGIN Defer command was not accepted",
            failures);
        CloseWhoseMarginLateWindow(
            deferredSlice,
            deferredData,
            failures,
            "explicit-whose-defer");
        AssertWhoseMarginPromiseEventScope(
            deferredSlice,
            failures,
            "explicit-whose-defer-revealed");
        RealtimeForecastEvent deferredNight = deferredSlice.LatestPresentation
            .BaseForecast.Events.Single(item => item.EventId == "NIGHT_SHIFT");
        Check(deferredNight.TemporalProjection.Outcome.DutySegments
                  .SelectMany(segment => segment.Loads)
                  .Where(load => load.LoadId == "RIVER_FACTORY")
                  .All(load => !load.Required) &&
              deferredSlice.LatestPresentation.Rail.Items.Single(item =>
                  item.Id == "NIGHT_SHIFT").Description.Contains(
                      "강변 산업단지 수요 의무 제외",
                      StringComparison.Ordinal),
            "WHOSE_MARGIN Defer did not remove only the factory promise duty",
            failures);
        CloseWhoseMarginNightStory(
            deferredSlice,
            deferredData,
            failures,
            "explicit-whose-defer");
        (RealtimeChapterOutcome whoseDeferredOutcome,
            RealtimeModalPresentation whoseDeferredResult) =
            CompleteWhoseMarginChapter(
                deferredSlice,
                failures,
                "explicit-whose-defer");
        CommercialStoryCard whoseDeferred = deferredData.BaseCampaign.Chapters[4]
            .ResultCards.Deferred!;
        Check(whoseDeferredOutcome.ObjectiveSatisfied &&
              whoseDeferredOutcome.PromiseDecision ==
                  CommercialPromiseDecision.Defer &&
              whoseDeferredResult.Eyebrow == whoseDeferred.Speaker &&
              whoseDeferredResult.Heading == whoseDeferred.Title &&
              whoseDeferredResult.Body == whoseDeferred.Body,
            "explicit WHOSE_MARGIN Defer did not present exact authored bytes",
            failures);
        Check(deferredSlice.ClosePresentedStoryModalForSmoke() is not null &&
              !deferredSlice.CoreSnapshot.CampaignComplete &&
              deferredSlice.FormativeTutorialResultChapterIdsForSmoke.Count == 3 &&
              !deferredSlice.FormativeTutorialFullFlowRecordedForSmoke,
            "WHOSE_MARGIN Defer minted the Keep-only token or blocked the flood handoff",
            failures);

        EnterBeforeWaterRisePlanning(
            deferredSlice,
            deferredData,
            whoseDeferredOutcome.EndingCashUnit,
            failures,
            "explicit-flood-defer");
        SelectBeforeWaterRisePromiseDeadline(
            deferredSlice,
            failures,
            "explicit-flood-defer");
        deferredSlice.RequestActionForSmoke(RealtimeR2Ids.PromiseDeferAction);
        BuildBeforeWaterRiseHighlandLine(
            deferredSlice,
            reinforced: false,
            failures,
            "explicit-flood-defer");
        AssertBeforeWaterRiseForecast(
            deferredSlice,
            CommercialPromiseDecision.Defer,
            failures,
            "explicit-flood-defer");
        CloseBeforeWaterRiseFloodStory(
            deferredSlice,
            deferredData,
            failures,
            "explicit-flood-defer");
        (RealtimeChapterOutcome floodDeferredOutcome,
            RealtimeModalPresentation floodDeferredResult) =
            CompleteBeforeWaterRiseChapter(
                deferredSlice,
                failures,
                "explicit-flood-defer");
        CommercialStoryCard floodDeferred = deferredData.BaseCampaign.Chapters[5]
            .ResultCards.Deferred!;
        Check(floodDeferredOutcome.ObjectiveSatisfied &&
              floodDeferredOutcome.PromiseDecision ==
                  CommercialPromiseDecision.Defer &&
              floodDeferredOutcome.ConnectionRequirementAssessment is
              {
                  FrozenForChapter: true,
                  Satisfied: true,
              } deferredConnections &&
              deferredConnections.Facts.Single().CurrentConnections == 2 &&
              floodDeferredOutcome.Events.Single() is
              {
                  EventId: "FLOOD_ARRIVAL",
                  SafetySatisfied: true,
                  PromiseSatisfied: true,
              } deferredFloodEvent &&
              deferredFloodEvent.DutySegments
                  .SelectMany(segment => segment.Loads)
                  .Where(load => load.LoadId == "EAST_RESIDENTIAL")
                  .All(load => !load.Required) &&
              floodDeferredResult.Eyebrow == floodDeferred.Speaker &&
              floodDeferredResult.Heading == floodDeferred.Title &&
              floodDeferredResult.Body == floodDeferred.Body,
            "explicit flood Defer did not exclude only East duty or present exact result",
            failures);
        Check(deferredSlice.ClosePresentedStoryModalForSmoke() is not null &&
              !deferredSlice.CoreSnapshot.CampaignComplete &&
              deferredSlice.CoreSnapshot.Chapter.Content.ChapterId ==
                  "SWITCH_OFF_TO_PROTECT" &&
              deferredSlice.FormativeTutorialResultChapterIdsForSmoke.Count == 3 &&
              !deferredSlice.FormativeTutorialFullFlowRecordedForSmoke,
            "BEFORE_WATER_RISE Defer minted the Keep-only token or blocked SWITCH handoff",
            failures);

        // Unset reaches the exact deadline once, becomes auto-Defer, stays
        // locked/text-recoverable, and discloses that automatic branch.
        var defaultedSlice = new RealtimeSliceMain();
        try
        {
            defaultedSlice.BootstrapNativeReleaseForSmoke(
                RealtimeNativeRouteCatalog.ProductCampaign);
        }
        catch
        {
            defaultedSlice.Free();
            throw;
        }
        using var defaultedLifetime = defaultedSlice.FreeAfterSmoke();
        (RealtimeSliceData defaultedData, string defaultedRoot) =
            AdvanceReleasePrefixToNorthBankPlanning(defaultedSlice, failures);
        _ = BuildNorthBankService(
            defaultedSlice,
            defaultedRoot,
            includeNorth: false,
            failures,
            "auto-defer");
        _ = AdvanceToMinuteByFrames(
            defaultedSlice,
            265680,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        Check(defaultedSlice.CoreSnapshot.PromiseDecision ==
                  CommercialPromiseDecision.Defer &&
              defaultedSlice.EmittedTransitions.Count(item =>
                  item.Kind == RealtimeTransitionKind.PromiseDefaulted &&
                  item.Minute == 265680) == 1,
            "unset promise did not default exactly once at 265680",
            failures);
        SelectPromiseDeadline(defaultedSlice, failures, "auto-defer-locked");
        RealtimeContextDockPresentation locked = defaultedSlice.LatestPresentation.Context;
        Check(locked.PrimaryAction is { Enabled: false, Visible: true } &&
              locked.SecondaryAction is { Enabled: false, Visible: true } &&
              locked.Sections.Any(item => item.Body.Contains(
                  "자동 Defer", StringComparison.Ordinal)) &&
              defaultedSlice.LatestPresentation.Rail.Items.Single(item =>
                  item.Id == RealtimeR2Ids.PromiseDecisionMarker(
                      "NORTH_BANK_MOVE_IN_PROMISE")) is
              {
                  Visibility: RealtimeTimelineVisibility.Completed,
                  IsActionable: false,
              },
            "defaulted deadline did not remain selectable, locked, and text recoverable",
            failures);
        (RealtimeChapterOutcome defaultedOutcome,
            RealtimeModalPresentation defaultedResult) = CompleteNorthBankChapter(
                defaultedSlice,
                defaultedData,
                failures);
        Check(defaultedOutcome.ObjectiveSatisfied &&
              defaultedResult.Heading == deferred.Title &&
              defaultedResult.Body.StartsWith(deferred.Body, StringComparison.Ordinal) &&
              defaultedResult.Body.Contains("자동으로 연기", StringComparison.Ordinal),
            "auto-Defer result omitted its factual disclosure",
            failures);
        _ = defaultedSlice.ClosePresentedStoryModalForSmoke();
        Check(defaultedSlice.FormativeTutorialResultChapterIdsForSmoke.Count == 3 &&
              !defaultedSlice.FormativeTutorialFullFlowRecordedForSmoke,
            "auto-Defer minted an explicit-choice formative token",
            failures);

        EnterWhoseMarginPlanning(
            defaultedSlice,
            defaultedData,
            failures,
            "standard-corridor");
        SelectWhoseMarginPromiseDeadline(
            defaultedSlice,
            failures,
            "standard-corridor");
        defaultedSlice.RequestActionForSmoke(RealtimeR2Ids.PromiseKeepAction);
        BuildWhoseMarginFactoryCorridor(
            defaultedSlice,
            reinforced: false,
            failures,
            "standard-corridor");
        CloseWhoseMarginLateWindow(
            defaultedSlice,
            defaultedData,
            failures,
            "standard-corridor");
        CloseWhoseMarginNightStory(
            defaultedSlice,
            defaultedData,
            failures,
            "standard-corridor");
        _ = AdvanceToMinuteByFrames(
            defaultedSlice,
            266760,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RealtimeTransition[] standardThermal = defaultedSlice.EmittedTransitions
            .Where(item => item.ChapterId == "WHOSE_MARGIN" &&
                RealtimeThermalPresentation.IsThermalTransition(item))
            .ToArray();
        Check(standardThermal.Any(item => item.Kind ==
                  RealtimeTransitionKind.ThermalEmergencyEntered &&
                  item.Minute == 266580) &&
              standardThermal.Any(item => item.Kind ==
                  RealtimeTransitionKind.ThermalProtectiveTrip &&
                  item.Minute == 266640) &&
              standardThermal.Any(item => item.Kind ==
                  RealtimeTransitionKind.ThermalRecovered &&
                  item.Minute == 266730),
            "standard factory corridor lost its 60-minute pole trip/recovery boundaries",
            failures);
        RealtimeTimelineItemPresentation[] actualThermalMarkers = defaultedSlice
            .LatestPresentation.Rail.Items
            .Where(item => item.Id.StartsWith(
                RealtimeR2Ids.ActualThermalMarkerPrefix,
                StringComparison.Ordinal))
            .ToArray();
        Check(actualThermalMarkers.Any(item => item.Description.Contains(
                  "비상 운전 시작",
                  StringComparison.Ordinal)) &&
              actualThermalMarkers.Any(item => item.Description.Contains(
                  "보호정지",
                  StringComparison.Ordinal)) &&
              actualThermalMarkers.Any(item => item.Description.Contains(
                  "냉각 복귀",
                  StringComparison.Ordinal)),
            "actual standard-corridor emergency/trip/recovery left the event rail",
            failures);
        AssertNoUnknownRailTargets(
            defaultedSlice,
            defaultedSlice.LatestPresentation,
            failures,
            "standard-corridor-recovered");
        RealtimeTransition selectedActualTrip = standardThermal
            .Where(item => item.Kind == RealtimeTransitionKind.ThermalProtectiveTrip)
            .OrderBy(item => item.Minute)
            .ThenBy(item => item.AssetId, StringComparer.Ordinal)
            .First();
        RealtimeTimelineItemPresentation actualTrip = actualThermalMarkers.Single(item =>
            item.Id == RealtimeR2Ids.ActualThermalMarker(selectedActualTrip));
        RequireIntent(defaultedSlice.ApplyIntentForSmoke(
                RealtimeR2Intent.SetTimelineMarker(
                    actualTrip.Id,
                    actualTrip.Id,
                    null,
                    defaultedSlice.InteractionState.TimelineHorizon)),
            "standard-corridor actual trip selection",
            failures,
            coreCommandExpected: false);
        RealtimeContextDockPresentation actualTripContext =
            defaultedSlice.LatestPresentation.Context;
        string actualTripHistory = actualTripContext.Details.Single(item =>
            item.Tab == RealtimeContextDetailTab.History).Body;
        Check(actualTripContext.Eyebrow == "실제 열 보호 기록" &&
              actualTripHistory.Contains("비상 운전 시작", StringComparison.Ordinal) &&
              actualTripHistory.Contains("보호정지", StringComparison.Ordinal) &&
              actualTripHistory.Contains("냉각 복귀", StringComparison.Ordinal),
            "actual trip detail did not retain its typed Core transition history",
            failures);
        (RealtimeChapterOutcome standardOutcome,
            RealtimeModalPresentation standardResult) =
            CompleteWhoseMarginChapter(
                defaultedSlice,
                failures,
                "standard-corridor");
        CommercialStoryCard authoredWhoseKept = defaultedData.BaseCampaign.Chapters[4]
            .ResultCards.Kept!;
        Check(!standardOutcome.ObjectiveSatisfied &&
              standardOutcome.Events.All(item => item.SafetySatisfied) &&
              standardOutcome.Events.Single(item => item.EventId == "NIGHT_SHIFT")
                  .PromiseUnservedMinutes > 0 &&
              standardResult.Body.Contains("약속 Keep 0/1 충족", StringComparison.Ordinal) &&
              (!string.Equals(standardResult.Eyebrow,
                   authoredWhoseKept.Speaker, StringComparison.Ordinal) ||
               !string.Equals(standardResult.Heading,
                   authoredWhoseKept.Title, StringComparison.Ordinal) ||
               !string.Equals(standardResult.Body,
                   authoredWhoseKept.Body, StringComparison.Ordinal)),
            "standard corridor counterfeited the authored WHOSE_MARGIN Keep success",
            failures);
        _ = defaultedSlice.ClosePresentedStoryModalForSmoke();
        Check(!defaultedSlice.FormativeTutorialFullFlowRecordedForSmoke,
            "standard WHOSE_MARGIN failure minted the six-chapter token",
            failures);

        // Explicit Defer with an unsafe water network is still a safety failure.
        // Excluded North demand must never be described as a fulfilled promise.
        var deferSafetyFailureSlice = new RealtimeSliceMain();
        try
        {
            deferSafetyFailureSlice.BootstrapNativeReleaseForSmoke(
                RealtimeNativeRouteCatalog.ProductCampaign);
        }
        catch
        {
            deferSafetyFailureSlice.Free();
            throw;
        }
        using var deferSafetyFailureLifetime = deferSafetyFailureSlice.FreeAfterSmoke();
        (RealtimeSliceData deferSafetyFailureData, _) =
            AdvanceReleasePrefixToNorthBankPlanning(
                deferSafetyFailureSlice,
                failures);
        SelectPromiseDeadline(
            deferSafetyFailureSlice,
            failures,
            "defer-safety-failure");
        deferSafetyFailureSlice.RequestActionForSmoke(
            RealtimeR2Ids.PromiseDeferAction);
        (RealtimeChapterOutcome deferSafetyFailureOutcome,
            RealtimeModalPresentation deferSafetyFailureResult) =
            CompleteNorthBankChapter(
                deferSafetyFailureSlice,
                deferSafetyFailureData,
                failures);
        CommercialStoryCard authoredDeferred = deferSafetyFailureData.BaseCampaign
            .Chapters[3].ResultCards.Deferred!;
        Check(!deferSafetyFailureOutcome.ObjectiveSatisfied &&
              deferSafetyFailureOutcome.PromiseDecision ==
                  CommercialPromiseDecision.Defer &&
              deferSafetyFailureOutcome.Events.Any(item => !item.SafetySatisfied) &&
              deferSafetyFailureResult.Eyebrow == "계통운영 기록" &&
              deferSafetyFailureResult.Heading.Contains(
                  "목표 미달",
                  StringComparison.Ordinal) &&
              deferSafetyFailureResult.Body.Contains(
                  "안전 의무",
                  StringComparison.Ordinal) &&
              deferSafetyFailureResult.Body.Contains(
                  "수요 의무 제외",
                  StringComparison.Ordinal) &&
              !deferSafetyFailureResult.Body.Contains(
                  "약속 Defer 2/2 충족",
                  StringComparison.Ordinal) &&
              (!string.Equals(
                   deferSafetyFailureResult.Eyebrow,
                   authoredDeferred.Speaker,
                   StringComparison.Ordinal) ||
               !string.Equals(
                   deferSafetyFailureResult.Heading,
                   authoredDeferred.Title,
                   StringComparison.Ordinal) ||
               !string.Equals(
                   deferSafetyFailureResult.Body,
                   authoredDeferred.Body,
                   StringComparison.Ordinal)),
            "Defer safety failure conflated excluded North demand with promise " +
            "fulfillment or counterfeited the authored deferred result",
            failures);
        _ = deferSafetyFailureSlice.ClosePresentedStoryModalForSmoke();
        Check(deferSafetyFailureSlice.FormativeTutorialResultChapterIdsForSmoke.Count == 3 &&
              !deferSafetyFailureSlice.FormativeTutorialFullFlowRecordedForSmoke,
            "Defer safety failure minted a formative token",
            failures);

        // Keep with only Water supplied is a promise-only failure. It must use
        // factual generic copy rather than the authored kept card.
        var promiseFailureSlice = new RealtimeSliceMain();
        try
        {
            promiseFailureSlice.BootstrapNativeReleaseForSmoke(
                RealtimeNativeRouteCatalog.ProductCampaign);
        }
        catch
        {
            promiseFailureSlice.Free();
            throw;
        }
        using var promiseFailureLifetime = promiseFailureSlice.FreeAfterSmoke();
        (RealtimeSliceData promiseFailureData, string promiseFailureRoot) =
            AdvanceReleasePrefixToNorthBankPlanning(promiseFailureSlice, failures);
        SelectPromiseDeadline(promiseFailureSlice, failures, "promise-failure");
        promiseFailureSlice.RequestActionForSmoke(
            RealtimeR2Ids.PromiseKeepAction);
        _ = BuildNorthBankService(
            promiseFailureSlice,
            promiseFailureRoot,
            includeNorth: false,
            failures,
            "promise-failure");
        (RealtimeChapterOutcome promiseFailureOutcome,
            RealtimeModalPresentation promiseFailureResult) = CompleteNorthBankChapter(
                promiseFailureSlice,
                promiseFailureData,
                failures);
        CommercialStoryCard kept = promiseFailureData.BaseCampaign.Chapters[3]
            .ResultCards.Kept!;
        Check(!promiseFailureOutcome.ObjectiveSatisfied &&
              promiseFailureOutcome.Events.All(item => item.SafetySatisfied) &&
              promiseFailureOutcome.Events.Any(item => !item.PromiseSatisfied) &&
              promiseFailureResult.Eyebrow == "계통운영 기록" &&
              promiseFailureResult.Heading.Contains("목표 미달", StringComparison.Ordinal) &&
              promiseFailureResult.Body.Contains("약속 Keep", StringComparison.Ordinal) &&
              (!string.Equals(promiseFailureResult.Eyebrow, kept.Speaker,
                   StringComparison.Ordinal) ||
               !string.Equals(promiseFailureResult.Heading, kept.Title,
                   StringComparison.Ordinal) ||
               !string.Equals(promiseFailureResult.Body, kept.Body,
                   StringComparison.Ordinal)),
            "promise-only failure counterfeited the authored kept result",
            failures);
        long recordedPromiseUnservedMinutes = promiseFailureOutcome.Events.Sum(item =>
            item.PromiseUnservedMinutes);
        Check(recordedPromiseUnservedMinutes > 0,
            "completed Keep failure lost its authoritative promise-unserved minutes",
            failures);
        Check(promiseFailureSlice.FormativeTutorialResultChapterIdsForSmoke.Count == 3 &&
              !promiseFailureSlice.FormativeTutorialFullFlowRecordedForSmoke,
            "promise failure minted a formative token",
            failures);
    }

    internal static (RealtimeSliceData Data, string RootSubstationId)
        AdvanceReleasePrefixToNorthBankPlanning(
            RealtimeSliceMain slice,
            ICollection<string> failures)
    {
        RealtimeSliceData data = slice.SliceDataForSmoke;
        Check(data.NativeRoute ==
                  RealtimeNativeRouteCatalog.ProductCampaign &&
              data.CampaignSha256 ==
                  "7bd151399040934cfcb9f7c96d2879aef6354cda79ced2af184641eb33a02f09" &&
              data.Campaign.Chapters.Select(item => item.Content.ChapterId)
                  .SequenceEqual(
                      new[]
                      {
                          "FIRST_LIGHT",
                          "SECOND_HEART",
                          "SECOND_SOURCE",
                          "NORTH_BANK_PROMISE",
                          "WHOSE_MARGIN",
                          "BEFORE_WATER_RISE",
                          "SWITCH_OFF_TO_PROTECT",
                          "LONGEST_NIGHT",
                      },
                      StringComparer.Ordinal) &&
              data.Campaign.Chapters.Sum(item => item.ScheduledEvents.Count) == 16 &&
              RealtimeSliceMain.ParseLaunchArguments(
                  ["--release-through=LONGEST_NIGHT"]).NativeRoute ==
                  RealtimeNativeRouteCatalog.ProductCampaign,
            "LONGEST_NIGHT exact route/prefix identity drifted: " +
            data.CampaignSha256,
            failures);
        bool isolatedRejected = false;
        try
        {
            _ = RealtimeSliceMain.ParseLaunchArguments(
                ["--release-chapter=NORTH_BANK_PROMISE"]);
        }
        catch (ArgumentException)
        {
            isolatedRejected = true;
        }
        Check(isolatedRejected,
            "standalone North Bank route counterfeited cumulative state",
            failures);

        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "FIRST_LIGHT",
            null,
            data.BaseCampaign.Chapters[0].Briefing,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null,
            "North route FIRST_LIGHT briefing did not close", failures);
        string rootSubstationId = BuildTutorialFirstLightNetwork(slice, failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            1320,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterResult,
            "FIRST_LIGHT",
            null,
            data.BaseCampaign.Chapters[0].ResultCards.Standard!,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is not null,
            "North route FIRST_LIGHT result did not queue SECOND_HEART", failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null,
            "North route SECOND_HEART briefing did not close", failures);

        string hospitalSubstationId = OrderTutorialNode(
            slice,
            new CoreMapPoint(2250, 1300),
            failures,
            "North route hospital service substation");
        _ = OrderTutorialLine(
            slice,
            rootSubstationId,
            [new CoreMapPoint(2250, 1000)],
            hospitalSubstationId,
            "STANDARD_LINE",
            "STANDARD_POLE",
            failures,
            "North route hospital feed");
        _ = OrderTutorialLine(
            slice,
            hospitalSubstationId,
            Array.Empty<CoreMapPoint>(),
            "HOSPITAL_TERMINAL",
            "STANDARD_LINE",
            "STANDARD_POLE",
            failures,
            "North route hospital corridor one");
        _ = OrderTutorialLine(
            slice,
            "EAST_RESIDENTIAL_TERMINAL",
            [new CoreMapPoint(2550, 1050)],
            "HOSPITAL_TERMINAL",
            "STANDARD_LINE",
            "STANDARD_POLE",
            failures,
            "North route hospital corridor two");
        _ = AdvanceToMinuteByFrames(
            slice,
            1800,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        CommercialStoryCard floodStory = data.BaseCampaign.Chapters[1]
            .OperatingPhases.Single(item =>
                item.PhaseId == "FLOOD_ISOLATION_TEST").Story!;
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.EventStory,
            "SECOND_HEART",
            "FLOOD_ISOLATION_TEST",
            floodStory,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null,
            "North route flood story did not close", failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            1860,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterResult,
            "SECOND_HEART",
            null,
            data.BaseCampaign.Chapters[1].ResultCards.Standard!,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is not null,
            "North route SECOND_HEART result did not queue SECOND_SOURCE", failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null,
            "North route SECOND_SOURCE briefing did not close", failures);

        _ = OrderTutorialLine(
            slice,
            "SOUTH_SOURCE_NODE",
            [
                new CoreMapPoint(700, 1650),
                new CoreMapPoint(1150, 1650),
                new CoreMapPoint(1750, 1650),
                new CoreMapPoint(2050, 1450),
            ],
            hospitalSubstationId,
            "STANDARD_LINE",
            "STANDARD_POLE",
            failures,
            "North route south source corridor");
        _ = AdvanceToMinuteByFrames(
            slice,
            2400,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        CommercialStoryCard southStory = data.BaseCampaign.Chapters[2]
            .OperatingPhases.Single(item =>
                item.PhaseId == "SOUTH_SOURCE_COMMISSIONING_TEST").Story!;
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.EventStory,
            "SECOND_SOURCE",
            "SOUTH_SOURCE_COMMISSIONING_TEST",
            southStory,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null,
            "North route south-source story did not close", failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            2460,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterResult,
            "SECOND_SOURCE",
            null,
            data.BaseCampaign.Chapters[2].ResultCards.Standard!,
            failures);
        RealtimeModalPresentation sourceResult = slice.LatestPresentation.Modal!;
        Check(!sourceResult.DismissOnCancel &&
              sourceResult.PrimaryAction.Label.Contains("6개월", StringComparison.Ordinal) &&
              sourceResult.PrimaryAction.Description.Contains(
                  "185일 05:00", StringComparison.Ordinal) &&
              slice.CoreSnapshot is
              {
                  Minute: 2460,
                  ChapterStarted: false,
                  ChapterStartMinute: 265260,
              },
            "SECOND_SOURCE result did not expose the explicit typed calendar action",
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is not null &&
              slice.CoreSnapshot is
              {
                  Minute: 265260,
                  ChapterStarted: true,
              } &&
              slice.FormativeTutorialResultChapterIdsForSmoke.SequenceEqual(
                  new[] { "FIRST_LIGHT", "SECOND_HEART", "SECOND_SOURCE" },
                  StringComparer.Ordinal),
            "production result action did not preserve the three-result chain into North Bank",
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "NORTH_BANK_PROMISE",
            null,
            data.BaseCampaign.Chapters[3].Briefing,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is not null,
            "North briefing did not queue its planning-window story", failures);
        RealtimeChapterStoryModalRequest planning = slice.ActiveChapterStoryModalForSmoke ??
            throw new InvalidOperationException("North planning window is not active.");
        CommercialStoryCard planningCard = data.BaseCampaign.Chapters[3]
            .DecisionWindows.Single(item =>
                item.WindowId == "NORTH_BANK_PLANNING_WINDOW").Story!;
        RealtimeModalPresentation planningModal = slice.LatestPresentation.Modal!;
        Check(planning.Purpose == RealtimeChapterStoryModalPurpose.DecisionWindowStory &&
              planning.WindowId == "NORTH_BANK_PLANNING_WINDOW" &&
              planningModal.Eyebrow == planningCard.Speaker &&
              planningModal.Heading == planningCard.Title &&
              planningModal.Body == planningCard.Body &&
              planningModal.PrimaryAction.Id == RealtimeR2Ids.DecisionWindowContinueAction,
            "North planning-window authored FIFO drifted",
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null &&
              slice.InteractionState.Simulation == RealtimeSimulationState.Running,
            "North planning window did not close into live streaming play",
            failures);
        return (data, rootSubstationId);
    }

    internal static string BuildNorthBankService(
        RealtimeSliceMain slice,
        string rootSubstationId,
        bool includeNorth,
        ICollection<string> failures,
        string label)
    {
        string northSubstationId = OrderTutorialNode(
            slice,
            new CoreMapPoint(2500, 500),
            failures,
            $"{label} North service substation");
        _ = OrderTutorialLine(
            slice,
            rootSubstationId,
            Array.Empty<CoreMapPoint>(),
            northSubstationId,
            "STANDARD_LINE",
            "STANDARD_POLE",
            failures,
            $"{label} North service feed");
        _ = OrderTutorialLine(
            slice,
            northSubstationId,
            Array.Empty<CoreMapPoint>(),
            "WATER_TERMINAL",
            "STANDARD_LINE",
            "STANDARD_POLE",
            failures,
            $"{label} water corridor");
        if (includeNorth)
        {
            _ = OrderTutorialLine(
                slice,
                northSubstationId,
                Array.Empty<CoreMapPoint>(),
                "NORTH_RESIDENTIAL_TERMINAL",
                "STANDARD_LINE",
                "STANDARD_POLE",
                failures,
                $"{label} residential corridor");
        }
        Check(slice.CoreSnapshot.Minute < 265680,
            $"{label} North service construction missed the promise deadline",
            failures);
        return northSubstationId;
    }

    private static void SelectPromiseDeadline(
        RealtimeSliceMain slice,
        ICollection<string> failures,
        string label)
    {
        const string markerId =
            RealtimeR2Ids.PromiseDecisionMarkerPrefix + "NORTH_BANK_MOVE_IN_PROMISE";
        RequireIntent(slice.ApplyIntentForSmoke(RealtimeR2Intent.SetTimelineMarker(
                markerId,
                markerId,
                null,
                slice.InteractionState.TimelineHorizon)),
            $"{label} promise marker selection",
            failures,
            coreCommandExpected: false);
        Check(slice.LatestPresentation.Context is
            {
                SubjectId: markerId,
                Visible: true,
            },
            $"{label} promise marker did not open ContextDock",
            failures);
    }

    internal static (RealtimeChapterOutcome Outcome, RealtimeModalPresentation Result)
        CompleteNorthBankChapter(
            RealtimeSliceMain slice,
            RealtimeSliceData data,
            ICollection<string> failures)
    {
        _ = AdvanceToMinuteByFrames(
            slice,
            265950,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        CommercialStoryCard hotStory = data.BaseCampaign.Chapters[3]
            .OperatingPhases.Single(item =>
                item.PhaseId == "NEXT_HOT_EVENING_FORECAST").Story!;
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.EventStory,
            "NORTH_BANK_PROMISE",
            "NEXT_HOT_EVENING_FORECAST",
            hotStory,
            failures);
        Check(slice.EmittedTransitions.Any(item =>
                  item.Kind == RealtimeTransitionKind.EventStarted &&
                  item.EventId == "NORTH_BANK_COMMISSIONING") &&
              slice.EmittedTransitions.All(item =>
                  item.EventId != "NORTH_BANK_COMMISSIONING" ||
                  item.Kind != RealtimeTransitionKind.EventStarted ||
                  slice.ActiveChapterStoryModalForSmoke?.EventId !=
                      "NORTH_BANK_COMMISSIONING"),
            "null commissioning story created a modal or never started",
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null,
            "North hot-evening story did not restore realtime play",
            failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            266070,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RealtimeChapterOutcome outcome = slice.CoreSnapshot.CompletedChapters.Single(item =>
            item.ChapterId == "NORTH_BANK_PROMISE");
        RealtimeModalPresentation result = slice.LatestPresentation.Modal ??
            throw new InvalidOperationException("North Bank result modal is absent.");
        Check(slice.CoreSnapshot.Minute == 266070 &&
              slice.CoreSnapshot.CompletedChapters.Count == 4 &&
              result.Id == RealtimeR2Ids.TutorialResultModal("NORTH_BANK_PROMISE"),
            "North Bank did not end with its stable result state",
            failures);
        return (outcome, result);
    }

    private static void EnterWhoseMarginPlanning(
        RealtimeSliceMain slice,
        RealtimeSliceData data,
        ICollection<string> failures,
        string label)
    {
        CommercialCampaignChapterDefinition authored = data.BaseCampaign.Chapters[4];
        Check(slice.CoreSnapshot is
              {
                  Minute: 266070,
                  ChapterStarted: true,
                  CampaignComplete: false,
              } &&
              slice.CoreSnapshot.Chapter.Content.ChapterId == "WHOSE_MARGIN",
            $"{label} did not inherit the cumulative clock into WHOSE_MARGIN",
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "WHOSE_MARGIN",
            null,
            authored.Briefing,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is not null,
            $"{label} WHOSE_MARGIN briefing did not queue HOT planning",
            failures);
        RequireDecisionWindow(
            slice,
            authored,
            "WHOSE_MARGIN",
            "HOT_EVENING_PLANNING_WINDOW",
            failures,
            label);
        Check(slice.ClosePresentedStoryModalForSmoke() is null &&
              slice.InteractionState.Simulation == RealtimeSimulationState.Running,
            $"{label} HOT planning did not close into realtime play",
            failures);
    }

    private static void AssertWhoseMarginPromiseEventScope(
        RealtimeSliceMain slice,
        ICollection<string> failures,
        string label)
    {
        RealtimeForecastEvent[] events = slice.LatestPresentation.BaseForecast.Events
            .Where(item => item.ChapterId == "WHOSE_MARGIN")
            .OrderBy(item => item.StartMinute)
            .ToArray();
        Check(events.Length > 0 &&
              events.Select(item => item.EventId).SequenceEqual(
                  new[] { "HOT_BASE", "NIGHT_SHIFT", "LATE_NIGHT" }
                      .Take(events.Length),
                  StringComparer.Ordinal) &&
              events.All(item =>
                  RealtimePromisePresentationFacts.HasPromiseDuty(
                      item.OperatingProfile) ==
                  string.Equals(
                      item.EventId,
                      "NIGHT_SHIFT",
                      StringComparison.Ordinal)) &&
              events.Count(item => RealtimePromisePresentationFacts.HasPromiseDuty(
                  item.OperatingProfile)) ==
                  events.Count(item => item.EventId == "NIGHT_SHIFT"),
            $"{label} promise duty was not isolated to NIGHT_SHIFT",
            failures);
        RealtimeEventRailPresentation rail = slice.LatestPresentation.Rail;
        Check(events.All(item =>
        {
            string description = rail.Items.Single(candidate =>
                candidate.Id == item.EventId).Description;
            bool hasPromiseCopy = description.Contains(
                    "약속",
                    StringComparison.Ordinal) ||
                description.Contains("수요 의무 제외", StringComparison.Ordinal);
            return hasPromiseCopy == (item.EventId == "NIGHT_SHIFT");
        }),
            $"{label} rail attached promise copy to a non-promise event",
            failures);
    }

    private static void SelectWhoseMarginPromiseDeadline(
        RealtimeSliceMain slice,
        ICollection<string> failures,
        string label)
    {
        string markerId = RealtimeR2Ids.PromiseDecisionMarker(
            "FACTORY_NIGHT_SHIFT_PROMISE");
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SetTimelineMarker(
                    markerId,
                    markerId,
                    null,
                    slice.InteractionState.TimelineHorizon)),
            $"{label} factory promise marker selection",
            failures,
            coreCommandExpected: false);
        Check(slice.LatestPresentation.Context is
            {
                SubjectId: var selected,
                PrimaryAction.Id: RealtimeR2Ids.PromiseKeepAction,
                SecondaryAction.Id: RealtimeR2Ids.PromiseDeferAction,
            } && string.Equals(selected, markerId, StringComparison.Ordinal),
            $"{label} factory promise actions were not presented",
            failures);
    }

    private static string BuildWhoseMarginFactoryCorridor(
        RealtimeSliceMain slice,
        bool reinforced,
        ICollection<string> failures,
        string label)
    {
        string nodeId = OrderTutorialNode(
            slice,
            new CoreMapPoint(2050, 1650),
            failures,
            $"{label} factory substation");
        string lineClassId = reinforced ? "REINFORCED_LINE" : "STANDARD_LINE";
        string poleClassId = reinforced ? "REINFORCED_POLE" : "STANDARD_POLE";
        CoreMapPoint[] points = reinforced
            ?
            [
                new CoreMapPoint(700, 1850),
                new CoreMapPoint(1190, 1850),
                new CoreMapPoint(1760, 1850),
            ]
            :
            [
                new CoreMapPoint(700, 1850),
                new CoreMapPoint(1200, 1850),
                new CoreMapPoint(1760, 1850),
            ];
        _ = OrderTutorialLine(
            slice,
            "SOUTH_SOURCE_NODE",
            points,
            nodeId,
            lineClassId,
            poleClassId,
            failures,
            $"{label} source corridor");
        _ = OrderTutorialLine(
            slice,
            nodeId,
            Array.Empty<CoreMapPoint>(),
            "FACTORY_TERMINAL",
            lineClassId,
            poleClassId,
            failures,
            $"{label} factory service");
        Check(slice.CoreSnapshot.Minute < 266400 &&
              slice.CoreSnapshot.Construction.World.Edges
                  .Where(item => item.FromNodeId == nodeId || item.ToNodeId == nodeId)
                  .Count(item => item.Commissioned &&
                      item.LineClassId == lineClassId) == 2,
            $"{label} factory corridor missed the recovery-window reveal",
            failures);
        return nodeId;
    }

    private static void CloseWhoseMarginLateWindow(
        RealtimeSliceMain slice,
        RealtimeSliceData data,
        ICollection<string> failures,
        string label)
    {
        _ = AdvanceToMinuteByFrames(
            slice,
            266400,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RequireDecisionWindow(
            slice,
            data.BaseCampaign.Chapters[4],
            "WHOSE_MARGIN",
            "LATE_NIGHT_RECOVERY_WINDOW",
            failures,
            label);
        Check(slice.EmittedTransitions.All(item =>
                  item.ChapterId != "WHOSE_MARGIN" ||
                  item.Kind != RealtimeTransitionKind.EventStarted) &&
              slice.ClosePresentedStoryModalForSmoke() is null,
            $"{label} late-night planning did not precede the first event",
            failures);
    }

    private static void CloseWhoseMarginNightStory(
        RealtimeSliceMain slice,
        RealtimeSliceData data,
        ICollection<string> failures,
        string label)
    {
        _ = AdvanceToMinuteByFrames(
            slice,
            266580,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        CommercialStoryCard story = data.BaseCampaign.Chapters[4].OperatingPhases
            .Single(item => item.PhaseId == "NIGHT_SHIFT").Story!;
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.EventStory,
            "WHOSE_MARGIN",
            "NIGHT_SHIFT",
            story,
            failures);
        Check(slice.EmittedTransitions
                  .Where(item => item.ChapterId == "WHOSE_MARGIN" &&
                      item.Kind == RealtimeTransitionKind.EventStarted)
                  .Select(item => item.EventId!)
                  .SequenceEqual(
                      new[] { "HOT_BASE", "NIGHT_SHIFT" },
                      StringComparer.Ordinal) &&
              slice.ClosePresentedStoryModalForSmoke() is null,
            $"{label} event/story FIFO drifted before NIGHT_SHIFT",
            failures);
    }

    private static (RealtimeChapterOutcome Outcome, RealtimeModalPresentation Result)
        CompleteWhoseMarginChapter(
            RealtimeSliceMain slice,
            ICollection<string> failures,
            string label)
    {
        _ = AdvanceToMinuteByFrames(
            slice,
            266850,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RealtimeChapterOutcome outcome = slice.CoreSnapshot.CompletedChapters.Single(
            item => item.ChapterId == "WHOSE_MARGIN");
        RealtimeModalPresentation result = slice.LatestPresentation.Modal ??
            throw new InvalidOperationException(
                $"{label} WHOSE_MARGIN result modal is absent.");
        Check(slice.CoreSnapshot.Minute == 266850 &&
              slice.CoreSnapshot.CompletedChapters.Count == 5 &&
              result.Id == RealtimeR2Ids.TutorialResultModal("WHOSE_MARGIN") &&
              slice.EmittedTransitions
                  .Where(item => item.ChapterId == "WHOSE_MARGIN" &&
                      item.Kind == RealtimeTransitionKind.EventStarted)
                  .Select(item => item.EventId!)
                  .SequenceEqual(
                      new[] { "HOT_BASE", "NIGHT_SHIFT", "LATE_NIGHT" },
                      StringComparer.Ordinal),
            $"{label} WHOSE_MARGIN did not end with exact event/result FIFO",
            failures);
        return (outcome, result);
    }

    private static void EnterBeforeWaterRisePlanning(
        RealtimeSliceMain slice,
        RealtimeSliceData data,
        long previousEndingCashUnit,
        ICollection<string> failures,
        string label)
    {
        CommercialCampaignChapterDefinition authored = data.BaseCampaign.Chapters[5];
        RealtimeConnectionRequirementAssessment connection = slice.CoreSnapshot
            .Forecast.ConnectionRequirementAssessment ?? throw new InvalidOperationException(
                $"{label} BEFORE_WATER_RISE connection assessment is absent.");
        Check(slice.CoreSnapshot is
              {
                  Minute: 266850,
                  ChapterStarted: true,
                  CampaignComplete: false,
                  PromiseDecision: CommercialPromiseDecision.Unset,
              } &&
              slice.CoreSnapshot.Chapter.Content.ChapterId == "BEFORE_WATER_RISE" &&
              slice.CoreSnapshot.CashUnit == checked(
                  previousEndingCashUnit + authored.BudgetGrantCashUnit) &&
              connection is { FrozenForChapter: false, Satisfied: true } &&
              connection.Facts.Single() is
              {
                  NodeId: "EAST_RESIDENTIAL_TERMINAL",
                  CurrentConnections: 2,
                  RequiredConnections: 2,
              },
            $"{label} did not inherit the exact clock and already-built East 2/2",
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "BEFORE_WATER_RISE",
            null,
            authored.Briefing,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is not null,
            $"{label} flood briefing did not queue BEFORE_FLOOD_WINDOW",
            failures);

        RequireDecisionWindow(
            slice,
            authored,
            "BEFORE_WATER_RISE",
            "BEFORE_FLOOD_WINDOW",
            failures,
            label);
        Check(slice.EmittedTransitions.All(item =>
                  item.ChapterId != "BEFORE_WATER_RISE" ||
                  item.Kind != RealtimeTransitionKind.EventStarted) &&
              slice.ClosePresentedStoryModalForSmoke() is null &&
              slice.InteractionState.Simulation == RealtimeSimulationState.Running,
            $"{label} flood window did not precede the event and close into realtime play",
            failures);
    }

    private static void SelectBeforeWaterRisePromiseDeadline(
        RealtimeSliceMain slice,
        ICollection<string> failures,
        string label)
    {
        string markerId = RealtimeR2Ids.PromiseDecisionMarker(
            "EAST_CONTINUITY_PROMISE");
        RealtimeTimelineItemPresentation marker = slice.LatestPresentation.Rail.Items
            .Single(item => item.Id == markerId);
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SetTimelineMarker(
                    markerId,
                    markerId,
                    null,
                    slice.InteractionState.TimelineHorizon)),
            $"{label} East promise marker selection",
            failures,
            coreCommandExpected: false);
        Check(marker.StartMinute == 267090 &&
              marker.Visibility == RealtimeTimelineVisibility.Announced &&
              marker.Description.Contains("동부 생활권", StringComparison.Ordinal) &&
              slice.LatestPresentation.Context is
              {
                  SubjectId: var selected,
                  PrimaryAction.Id: RealtimeR2Ids.PromiseKeepAction,
                  SecondaryAction.Id: RealtimeR2Ids.PromiseDeferAction,
              } &&
              selected == markerId,
            $"{label} East promise deadline/actions drifted",
            failures);
    }

    private static void BuildBeforeWaterRiseHighlandLine(
        RealtimeSliceMain slice,
        bool reinforced,
        ICollection<string> failures,
        string label)
    {
        string hospitalSubstationId = slice.CoreSnapshot.Construction.World.Nodes
            .Single(item => item.ClassId == "SMALL_SUBSTATION" &&
                item.Position == new CoreMapPoint(2250, 1300))
            .NodeId;
        CoreMapPoint[] points = reinforced
            ?
            [
                new CoreMapPoint(550, 1100),
                new CoreMapPoint(990, 750),
                new CoreMapPoint(1640, 750),
                new CoreMapPoint(1950, 850),
            ]
            :
            [
                new CoreMapPoint(450, 1200),
                new CoreMapPoint(650, 750),
                new CoreMapPoint(1040, 750),
                new CoreMapPoint(1620, 750),
                new CoreMapPoint(1900, 800),
                new CoreMapPoint(2100, 1000),
            ];
        RealtimeProjectQuote quote = OrderTutorialLine(
            slice,
            "SOUTH_SOURCE_NODE",
            points,
            hospitalSubstationId,
            reinforced ? "REINFORCED_LINE" : "STANDARD_LINE",
            reinforced ? "REINFORCED_POLE" : "STANDARD_POLE",
            failures,
            $"{label} flood-safe highland line");
        RealtimeConnectionRequirementAssessment connection = slice.CoreSnapshot
            .Forecast.ConnectionRequirementAssessment ?? throw new InvalidOperationException(
                $"{label} post-construction connection assessment is absent.");
        Check(quote.RiskAreaIds.Count == 0 &&
              quote.CompletionMinute <= 267150 &&
              slice.CoreSnapshot.Minute == quote.CompletionMinute &&
              connection.Facts.Single().CurrentConnections == 2,
            $"{label} highland line was late, crossed flood risk, or forged East 2/2",
            failures);
    }

    private static void AssertBeforeWaterRiseForecast(
        RealtimeSliceMain slice,
        CommercialPromiseDecision decision,
        ICollection<string> failures,
        string label)
    {
        RealtimeForecastEvent flood = slice.LatestPresentation.BaseForecast.Events
            .Single(item => item.EventId == "FLOOD_ARRIVAL");
        RealtimeEventOutcome projected = flood.TemporalProjection.Outcome;
        bool eastRequired = projected.DutySegments
            .SelectMany(segment => segment.Loads)
            .Where(load => load.LoadId == "EAST_RESIDENTIAL")
            .Any(load => load.Required);
        RealtimeTimelineItemPresentation rail = slice.LatestPresentation.Rail.Items
            .Single(item => item.Id == "FLOOD_ARRIVAL");
        RequireIntent(slice.ApplyIntentForSmoke(RealtimeR2Intent.SetTimelineMarker(
                "FLOOD_ARRIVAL",
                "EAST_RESIDENTIAL_TERMINAL",
                null,
                slice.InteractionState.TimelineHorizon)),
            $"{label} flood marker selection",
            failures,
            coreCommandExpected: false);
        RequireIntent(slice.ApplyIntentForSmoke(
                new RealtimeR2Intent(RealtimeR2IntentKind.ToggleAnalysis)),
            $"{label} flood analysis",
            failures,
            coreCommandExpected: false);
        Check(slice.CoreSnapshot.PromiseDecision == decision &&
              flood.StartMinute == 267150 &&
              flood.EndMinute == 267270 &&
              flood.OperatingProfile.ActiveRiskAreaIds.SequenceEqual(
                  new[] { "RIVER_FLOOD_ZONE" },
                  StringComparer.Ordinal) &&
              flood.OperatingProfile.UnavailableNodeIds.SequenceEqual(
                  new[] { "WEST_SOURCE_NODE" },
                  StringComparer.Ordinal) &&
              projected.SafetySatisfied &&
              projected.PromiseSatisfied &&
              eastRequired == (decision == CommercialPromiseDecision.Keep) &&
              rail.Kind == RealtimeTimelineItemKind.Weather &&
              rail.Lane == RealtimeTimelineLane.WeatherAndOutage &&
              slice.LatestPresentation.World.ForecastRiskAreaIds.SequenceEqual(
                  new[] { "RIVER_FLOOD_ZONE" },
                  StringComparer.Ordinal) &&
              slice.LatestPresentation.World.ActiveRiskAreaIds.Count == 0,
            $"{label} flood forecast lost timing, risk/unavailability, or promise scope",
            failures);
    }

    private static void CloseBeforeWaterRiseFloodStory(
        RealtimeSliceMain slice,
        RealtimeSliceData data,
        ICollection<string> failures,
        string label)
    {
        _ = AdvanceToMinuteByFrames(
            slice,
            267150,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        CommercialStoryCard story = data.BaseCampaign.Chapters[5].OperatingPhases
            .Single(item => item.PhaseId == "FLOOD_ARRIVAL").Story!;
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.EventStory,
            "BEFORE_WATER_RISE",
            "FLOOD_ARRIVAL",
            story,
            failures);
        RealtimeConnectionRequirementAssessment frozen = slice.CoreSnapshot.Forecast
            .ConnectionRequirementAssessment ?? throw new InvalidOperationException(
                $"{label} frozen flood connection assessment is absent.");
        RealtimeActiveEventState activeFlood = slice.CoreSnapshot.ActiveEventStates.Single();
        ThermalIntervalEvaluation actualFlood = slice.CoreSnapshot.Thermal.Evaluation;
        Check(activeFlood.EventId == "FLOOD_ARRIVAL" &&
              activeFlood.Event.OperatingProfile.UnavailableNodeIds.SequenceEqual(
                  new[] { "WEST_SOURCE_NODE" },
                  StringComparer.Ordinal) &&
              actualFlood.Sources.Single(source =>
                  source.SourceId == "WEST_GENERATION").UsedKw == 0 &&
              actualFlood.Sources.Single(source =>
                  source.SourceId == "SOUTH_GENERATION").UsedKw ==
                  actualFlood.Loads.Sum(load => load.DeliveredKw) &&
              actualFlood.Loads.All(load =>
                  load.DeliveredKw == load.DemandKw &&
                  load.SourceId == "SOUTH_GENERATION") &&
              frozen is
              {
                  EvaluatedMinute: 267150,
                  FrozenForChapter: true,
                  Satisfied: true,
              } &&
              frozen.Facts.Single().CurrentConnections == 2 &&
              slice.LatestPresentation.World.ForecastRiskAreaIds.Count == 0 &&
              slice.LatestPresentation.World.ActiveRiskAreaIds.SequenceEqual(
                  new[] { "RIVER_FLOOD_ZONE" },
                  StringComparer.Ordinal) &&
              slice.LatestPresentation.World.Weather == RealtimeWorldWeather.Storm &&
              slice.EmittedTransitions
                  .Where(item => item.ChapterId == "BEFORE_WATER_RISE" &&
                      item.Kind == RealtimeTransitionKind.EventStarted)
                  .Select(item => item.EventId!)
                  .SequenceEqual(new[] { "FLOOD_ARRIVAL" }, StringComparer.Ordinal),
            $"{label} active flood lost frozen 2/2, risk geometry, or West isolation",
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null &&
              slice.InteractionState.Simulation == RealtimeSimulationState.Running,
            $"{label} flood story did not restore realtime play",
            failures);
    }

    private static (RealtimeChapterOutcome Outcome, RealtimeModalPresentation Result)
        CompleteBeforeWaterRiseChapter(
            RealtimeSliceMain slice,
            ICollection<string> failures,
            string label)
    {
        _ = AdvanceToMinuteByFrames(
            slice,
            267270,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RealtimeChapterOutcome outcome = slice.CoreSnapshot.CompletedChapters.Single(item =>
            item.ChapterId == "BEFORE_WATER_RISE");
        RealtimeModalPresentation result = slice.LatestPresentation.Modal ??
            throw new InvalidOperationException(
                $"{label} BEFORE_WATER_RISE result modal is absent.");
        Check(slice.CoreSnapshot.Minute == 267270 &&
              slice.CoreSnapshot.CompletedChapters.Count == 6 &&
              result.Id == RealtimeR2Ids.TutorialResultModal("BEFORE_WATER_RISE") &&
              outcome.Events.Single() is
              {
                  EventId: "FLOOD_ARRIVAL",
                  StartMinute: 267150,
                  EndMinute: 267270,
              },
            $"{label} BEFORE_WATER_RISE did not end with exact event/result FIFO",
            failures);
        return (outcome, result);
    }

    private static void EnterSwitchOffToProtectPlanning(
        RealtimeSliceMain slice,
        RealtimeSliceData data,
        long previousEndingCashUnit,
        ICollection<string> failures)
    {
        const string label = "SWITCH_OFF_TO_PROTECT";
        CommercialCampaignChapterDefinition authored = data.BaseCampaign.Chapters[6];
        RealtimeConnectionRequirementAssessment connection = slice.CoreSnapshot
            .Forecast.ConnectionRequirementAssessment ?? throw new InvalidOperationException(
                $"{label} SWITCH_OFF_TO_PROTECT connection assessment is absent.");
        var inheritedWater = slice.CoreSnapshot.Construction.World.Edges.Single(
            edge => edge.Commissioned &&
                (edge.FromNodeId == "WATER_TERMINAL" ||
                 edge.ToNodeId == "WATER_TERMINAL"));
        string northPlayerSubstationId = slice.CoreSnapshot.Construction.World.Nodes
            .Single(node => node.Position == new CoreMapPoint(2500, 500))
            .NodeId;
        Check(slice.CoreSnapshot is
              {
                  Minute: 267270,
                  ChapterStarted: true,
                  CampaignComplete: false,
                  PromiseDecision: CommercialPromiseDecision.Unset,
              } &&
              slice.CoreSnapshot.Chapter.Content.ChapterId ==
                  "SWITCH_OFF_TO_PROTECT" &&
              slice.CoreSnapshot.CashUnit == checked(
                  previousEndingCashUnit + authored.BudgetGrantCashUnit) &&
              inheritedWater.EdgeId.StartsWith("PLAYER_EDGE_", StringComparison.Ordinal) &&
              (inheritedWater.FromNodeId == northPlayerSubstationId ||
               inheritedWater.ToNodeId == northPlayerSubstationId) &&
              connection is { FrozenForChapter: false, Satisfied: false } &&
              connection.Facts.Single() is
              {
                  NodeId: "WATER_TERMINAL",
                  CurrentConnections: 1,
                  RequiredConnections: 2,
              },
            $"{label} did not inherit the exact clock, cash, and North Water 1/2",
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "SWITCH_OFF_TO_PROTECT",
            null,
            authored.Briefing,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is not null,
            $"{label} briefing did not queue BEFORE_PLANNED_OUTAGE_WINDOW",
            failures);

        RequireDecisionWindow(
            slice,
            authored,
            "SWITCH_OFF_TO_PROTECT",
            "BEFORE_PLANNED_OUTAGE_WINDOW",
            failures,
            label);
        Check(slice.ClosePresentedStoryModalForSmoke() is null &&
              slice.InteractionState.Simulation == RealtimeSimulationState.Running,
            $"{label} planning window did not close into realtime play",
            failures);
    }

    private static void BuildSwitchContinuousWaterBranch(
        RealtimeSliceMain slice,
        ICollection<string> failures)
    {
        const string label = "SWITCH_OFF_TO_PROTECT";
        string reinforcedTrunkPoleId = slice.CoreSnapshot.Construction.World.Nodes
            .Single(node => node.ClassId == "REINFORCED_POLE" &&
                node.Position == new CoreMapPoint(1950, 850))
            .NodeId;
        string waterSubstationId = OrderTutorialNode(
            slice,
            new CoreMapPoint(2300, 900),
            failures,
            $"{label} continuous Water substation");
        _ = OrderTutorialLine(
            slice,
            reinforcedTrunkPoleId,
            Array.Empty<CoreMapPoint>(),
            waterSubstationId,
            "REINFORCED_LINE",
            "REINFORCED_POLE",
            failures,
            $"{label} continuous Water feed");
        _ = OrderTutorialLine(
            slice,
            waterSubstationId,
            Array.Empty<CoreMapPoint>(),
            "WATER_TERMINAL",
            "REINFORCED_LINE",
            "REINFORCED_POLE",
            failures,
            $"{label} continuous Water service");
        RealtimeConnectionRequirementAssessment connection = slice.CoreSnapshot.Forecast
            .ConnectionRequirementAssessment ?? throw new InvalidOperationException(
                $"{label} completed Water connection assessment is absent.");
        Check(slice.CoreSnapshot.Minute == 267417 &&
              connection is { FrozenForChapter: false, Satisfied: true } &&
              connection.Facts.Single().CurrentConnections == 2,
            $"{label} continuous Water branch lost its exact completion or 2/2",
            failures);
    }

    private static void AdvanceAndAssertSwitchOffToProtectForecast(
        RealtimeSliceMain slice,
        ICollection<string> failures)
    {
        const string label = "SWITCH_OFF_TO_PROTECT";
        RealtimeForecastEvent planned = slice.LatestPresentation.BaseForecast.Events.Single();
        Check(planned.EventId == "WEST_SOURCE_PLANNED_OUTAGE" &&
              planned.RevealMinute == 267270 &&
              planned.StartMinute == 267690 &&
              planned.EndMinute == 267810 &&
              planned.OperatingProfile.ThermalPolicy ==
                  CommercialPhaseThermalPolicy.SafetyEmergencyAllowed &&
              planned.OperatingProfile.UnavailableNodeIds.SequenceEqual(
                  new[] { "WEST_SOURCE_NODE" },
                  StringComparer.Ordinal) &&
              planned.ProjectedEvaluation.Loads.Single(load =>
                  load.LoadId == "HOSPITAL").DeliveredKw == 1800 &&
              planned.ProjectedEvaluation.Loads.Single(load =>
                  load.LoadId == "WATERWORKS").DeliveredKw == 1400 &&
              planned.ProjectedEvaluation.Loads.Single(load =>
                  load.LoadId == "EAST_RESIDENTIAL").DeliveredKw == 700 &&
              planned.TemporalProjection.Transitions.Count == 0 &&
              planned.TemporalProjection.Outcome.SafetySatisfied &&
              planned.TemporalProjection.Outcome.PromiseSatisfied,
            $"{label} planned-outage forecast lost authored timing, West isolation, or supply",
            failures);

        _ = AdvanceToMinuteByFrames(
            slice,
            267450,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RealtimeForecastEvent[] revealed = slice.LatestPresentation.BaseForecast.Events
            .ToArray();
        RealtimeForecastEvent returned = revealed.Single(item =>
            item.EventId == "WEST_SOURCE_RETURN_SERVICE");
        Check(revealed.Select(item => item.EventId).SequenceEqual(
                  new[]
                  {
                      "WEST_SOURCE_PLANNED_OUTAGE",
                      "WEST_SOURCE_RETURN_SERVICE",
                  },
                  StringComparer.Ordinal) &&
              returned.RevealMinute == 267450 &&
              returned.StartMinute == 267870 &&
              returned.EndMinute == 267990 &&
              returned.OperatingProfile.ThermalPolicy ==
                  CommercialPhaseThermalPolicy.ContinuousOnly &&
              returned.OperatingProfile.UnavailableNodeIds.Count == 0 &&
              returned.ProjectedEvaluation.Loads.Single(load =>
                  load.LoadId == "HOSPITAL").DeliveredKw == 900 &&
              returned.ProjectedEvaluation.Loads.Single(load =>
                  load.LoadId == "WATERWORKS").DeliveredKw == 900 &&
              returned.ProjectedEvaluation.Loads.Single(load =>
                  load.LoadId == "EAST_RESIDENTIAL").DeliveredKw == 700 &&
              returned.TemporalProjection.Transitions.Count == 0 &&
              returned.TemporalProjection.Outcome.SafetySatisfied &&
              returned.TemporalProjection.Outcome.PromiseSatisfied,
            $"{label} return-service forecast lost reveal order or continuous supply",
            failures);
    }

    private static void CloseSwitchPlannedOutageStory(
        RealtimeSliceMain slice,
        RealtimeSliceData data,
        ICollection<string> failures)
    {
        const string label = "SWITCH_OFF_TO_PROTECT";
        _ = AdvanceToMinuteByFrames(
            slice,
            267690,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        CommercialStoryCard story = data.BaseCampaign.Chapters[6].OperatingPhases
            .Single(item => item.PhaseId == "WEST_SOURCE_PLANNED_OUTAGE").Story!;
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.EventStory,
            "SWITCH_OFF_TO_PROTECT",
            "WEST_SOURCE_PLANNED_OUTAGE",
            story,
            failures);
        RealtimeConnectionRequirementAssessment frozen = slice.CoreSnapshot.Forecast
            .ConnectionRequirementAssessment ?? throw new InvalidOperationException(
                $"{label} frozen Water connection assessment is absent.");
        RealtimeActiveEventState active = slice.CoreSnapshot.ActiveEventStates.Single();
        ThermalIntervalEvaluation actual = slice.CoreSnapshot.Thermal.Evaluation;
        Check(active.EventId == "WEST_SOURCE_PLANNED_OUTAGE" &&
              active.Event.OperatingProfile.UnavailableNodeIds.SequenceEqual(
                  new[] { "WEST_SOURCE_NODE" },
                  StringComparer.Ordinal) &&
              actual.Sources.Single(source =>
                  source.SourceId == "WEST_GENERATION").UsedKw == 0 &&
              actual.Sources.Single(source =>
                  source.SourceId == "SOUTH_GENERATION").UsedKw == 3900 &&
              actual.Loads.Single(load => load.LoadId == "HOSPITAL") is
                  { DemandKw: 1800, DeliveredKw: 1800, SourceId: "SOUTH_GENERATION" } &&
              actual.Loads.Single(load => load.LoadId == "WATERWORKS") is
                  { DemandKw: 1400, DeliveredKw: 1400, SourceId: "SOUTH_GENERATION" } &&
              actual.Loads.Single(load => load.LoadId == "EAST_RESIDENTIAL") is
                  { DemandKw: 700, DeliveredKw: 700, SourceId: "SOUTH_GENERATION" } &&
              frozen is
              {
                  EvaluatedMinute: 267690,
                  FrozenForChapter: true,
                  Satisfied: true,
              } &&
              frozen.Facts.Single().CurrentConnections == 2,
            $"{label} active outage lost frozen 2/2, West isolation, or South supply",
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null &&
              slice.InteractionState.Simulation == RealtimeSimulationState.Running,
            $"{label} planned-outage story did not restore realtime play",
            failures);

        _ = AdvanceToMinuteByFrames(
            slice,
            267810,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RealtimeEventOutcome outcome = slice.CoreSnapshot.CurrentChapterEvents.Single(item =>
            item.EventId == "WEST_SOURCE_PLANNED_OUTAGE");
        RealtimeTransition[] thermal = slice.EmittedTransitions.Where(item =>
                item.ChapterId == "SWITCH_OFF_TO_PROTECT" &&
                item.EventId == "WEST_SOURCE_PLANNED_OUTAGE" &&
                RealtimeThermalPresentation.IsThermalTransition(item))
            .ToArray();
        Check(outcome.SafetySatisfied && outcome.PromiseSatisfied &&
              thermal.Length == 0,
            $"{label} continuous outage route lost duty or entered thermal protection",
            failures);
    }

    private static void CloseSwitchReturnServiceStory(
        RealtimeSliceMain slice,
        RealtimeSliceData data,
        ICollection<string> failures)
    {
        const string label = "SWITCH_OFF_TO_PROTECT";
        _ = AdvanceToMinuteByFrames(
            slice,
            267870,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        CommercialStoryCard story = data.BaseCampaign.Chapters[6].OperatingPhases
            .Single(item => item.PhaseId == "WEST_SOURCE_RETURN_SERVICE").Story!;
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.EventStory,
            "SWITCH_OFF_TO_PROTECT",
            "WEST_SOURCE_RETURN_SERVICE",
            story,
            failures);
        RealtimeActiveEventState active = slice.CoreSnapshot.ActiveEventStates.Single();
        ThermalIntervalEvaluation actual = slice.CoreSnapshot.Thermal.Evaluation;
        Check(active.EventId == "WEST_SOURCE_RETURN_SERVICE" &&
              active.Event.OperatingProfile.UnavailableNodeIds.Count == 0 &&
              actual.Sources.Single(source =>
                  source.SourceId == "WEST_GENERATION").UsedKw > 0 &&
              actual.Loads.Single(load => load.LoadId == "HOSPITAL") is
                  { DemandKw: 900, DeliveredKw: 900 } &&
              actual.Loads.Single(load => load.LoadId == "WATERWORKS") is
                  { DemandKw: 900, DeliveredKw: 900 } &&
              actual.Loads.Single(load => load.LoadId == "EAST_RESIDENTIAL") is
                  { DemandKw: 700, DeliveredKw: 700 } &&
              slice.CoreSnapshot.Thermal.Assets.All(asset => !asset.ProtectiveOutage) &&
              slice.CoreSnapshot.Thermal.Assets
                  .Where(asset => asset.UsedKw > 0)
                  .All(asset =>
                      asset.State == ThermalOperatingState.Continuous),
            $"{label} return service did not reuse West on continuous surviving paths",
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null &&
              slice.InteractionState.Simulation == RealtimeSimulationState.Running,
            $"{label} return-service story did not restore realtime play",
            failures);
    }

    private static (RealtimeChapterOutcome Outcome, RealtimeModalPresentation Result)
        CompleteSwitchOffToProtectChapter(
            RealtimeSliceMain slice,
            ICollection<string> failures)
    {
        const string label = "SWITCH_OFF_TO_PROTECT";
        _ = AdvanceToMinuteByFrames(
            slice,
            267990,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RealtimeChapterOutcome outcome = slice.CoreSnapshot.CompletedChapters.Single(item =>
            item.ChapterId == "SWITCH_OFF_TO_PROTECT");
        RealtimeModalPresentation result = slice.LatestPresentation.Modal ??
            throw new InvalidOperationException(
                $"{label} SWITCH_OFF_TO_PROTECT result modal is absent.");
        Check(slice.CoreSnapshot.Minute == 267990 &&
              slice.CoreSnapshot.CompletedChapters.Count == 7 &&
              result.Id ==
                  RealtimeR2Ids.TutorialResultModal("SWITCH_OFF_TO_PROTECT") &&
              outcome.Events.Select(item =>
                      (item.EventId, item.StartMinute, item.EndMinute))
                  .SequenceEqual(
                  [
                      ("WEST_SOURCE_PLANNED_OUTAGE", 267690L, 267810L),
                      ("WEST_SOURCE_RETURN_SERVICE", 267870L, 267990L),
                  ]),
            $"{label} SWITCH_OFF_TO_PROTECT did not end with exact event/result FIFO",
            failures);
        return (outcome, result);
    }

    private static void EnterLongestNightPlanning(
        RealtimeSliceMain slice,
        RealtimeSliceData data,
        long previousEndingCashUnit,
        ICollection<string> failures)
    {
        const string label = "LONGEST_NIGHT";
        CommercialCampaignChapterDefinition authored = data.BaseCampaign.Chapters[7];
        Check(slice.CoreSnapshot is
              {
                  Minute: 267990,
                  ChapterStarted: true,
                  CampaignComplete: false,
                  PromiseDecision: CommercialPromiseDecision.Unset,
              } &&
              slice.CoreSnapshot.Chapter.Content.ChapterId == "LONGEST_NIGHT" &&
              slice.CoreSnapshot.CashUnit == checked(
                  previousEndingCashUnit + authored.BudgetGrantCashUnit) &&
              slice.CoreSnapshot.Forecast.ConnectionRequirementAssessment is null,
            $"{label} did not inherit the exact clock, cash, or gate-free state",
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "LONGEST_NIGHT",
            null,
            authored.Briefing,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is not null,
            $"{label} briefing did not queue FINAL_OPERATING_PLAN_WINDOW",
            failures);

        RequireDecisionWindow(
            slice,
            authored,
            "LONGEST_NIGHT",
            "FINAL_OPERATING_PLAN_WINDOW",
            failures,
            label);
        Check(slice.ClosePresentedStoryModalForSmoke() is null &&
              slice.InteractionState.Simulation == RealtimeSimulationState.Running &&
              slice.EmittedTransitions.All(item =>
                  item.ChapterId != "LONGEST_NIGHT" ||
                  item.Kind != RealtimeTransitionKind.EventStarted),
            $"{label} final planning window did not precede actual events",
            failures);
    }

    private static void AdvanceAndAssertLongestNightForecast(
        RealtimeSliceMain slice,
        ICollection<string> failures)
    {
        const string label = "LONGEST_NIGHT";
        CommercialOperatingPhaseDefinition[] authoredPhases = slice.SliceDataForSmoke
            .BaseCampaign.Chapters[7].OperatingPhases.ToArray();
        CommercialOperatingPhaseDefinition authoredMaximum = authoredPhases.Single(item =>
            item.PhaseId == "MAX_DEMAND");
        CommercialOperatingPhaseDefinition authoredHeatwave = authoredPhases.Single(item =>
            item.PhaseId == "HEATWAVE_PEAK");
        CommercialOperatingPhaseDefinition authoredFlood = authoredPhases.Single(item =>
            item.PhaseId == "PROTECTIVE_STOP_FLOOD");
        RealtimeForecastEvent[] revealed = slice.LatestPresentation.BaseForecast.Events
            .Where(item => item.ChapterId == "LONGEST_NIGHT")
            .OrderBy(item => item.StartMinute)
            .ToArray();
        RealtimeForecastEvent maximum = revealed.Single();
        Check(maximum.EventId == "MAX_DEMAND" &&
              maximum.RevealMinute == 267990 &&
              maximum.StartMinute == 268590 &&
              maximum.EndMinute == 268710 &&
              maximum.OperatingProfile.ThermalPolicy ==
                  CommercialPhaseThermalPolicy.ContinuousOnly &&
              maximum.OperatingProfile.Loads.SequenceEqual(authoredMaximum.Loads) &&
              maximum.ProjectedEvaluation.Loads.Single(load =>
                  load.LoadId == "HOSPITAL").DeliveredKw == 900 &&
              maximum.ProjectedEvaluation.Loads.Single(load =>
                  load.LoadId == "WATERWORKS").DeliveredKw == 900 &&
              maximum.ProjectedEvaluation.Loads.All(load =>
                  load.DeliveredKw == load.DemandKw) &&
              maximum.TemporalProjection.Transitions.Count == 0 &&
              slice.CoreSnapshot.Minute == 267990,
            $"{label} maximum forecast lost exact timing, duty, or continuous supply",
            failures);

        _ = AdvanceToMinuteByFrames(
            slice,
            268170,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RealtimeForecastEvent heatwave = slice.LatestPresentation.BaseForecast.Events
            .Single(item => item.EventId == "HEATWAVE_PEAK");
        RealtimeThermalTransition projectedEmergency = heatwave.TemporalProjection
            .Transitions.Single(transition => transition is
            {
                Minute: 268770,
                AssetKind: ThermalAssetKind.Edge,
                Kind: RealtimeThermalTransitionKind.EmergencyEntered,
            });
        RealtimeThermalTransition[] projectedThermalArc = heatwave.TemporalProjection
            .Transitions.Where(transition =>
                transition.AssetId == projectedEmergency.AssetId).ToArray();
        Check(heatwave.RevealMinute == 268170 &&
              heatwave.StartMinute == 268770 &&
              heatwave.EndMinute == 268890 &&
              heatwave.OperatingProfile.ThermalPolicy ==
                  CommercialPhaseThermalPolicy.SafetyEmergencyAllowed &&
              heatwave.OperatingProfile.Loads.SequenceEqual(authoredHeatwave.Loads) &&
              heatwave.ProjectedEvaluation.Loads.Single(load =>
                  load.LoadId == "HOSPITAL").DeliveredKw == 1600 &&
              heatwave.ProjectedEvaluation.Loads.Single(load =>
                  load.LoadId == "WATERWORKS").DeliveredKw == 1400 &&
              projectedThermalArc is
              [
                  {
                      Minute: 268770,
                      AssetKind: ThermalAssetKind.Edge,
                      Kind: RealtimeThermalTransitionKind.EmergencyEntered,
                  },
                  {
                      Minute: 268845,
                      AssetKind: ThermalAssetKind.Edge,
                      Kind: RealtimeThermalTransitionKind.ProtectiveTrip,
                  },
              ],
            $"{label} heatwave forecast lost exact timing, duty, or thermal arc",
            failures);

        _ = AdvanceToMinuteByFrames(
            slice,
            268350,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RealtimeForecastEvent flood = slice.LatestPresentation.BaseForecast.Events
            .Single(item => item.EventId == "PROTECTIVE_STOP_FLOOD");
        Check(flood.RevealMinute == 268350 &&
              flood.StartMinute == 268950 &&
              flood.EndMinute == 269070 &&
              flood.OperatingProfile.ThermalPolicy ==
                  CommercialPhaseThermalPolicy.SafetyEmergencyAllowed &&
              flood.OperatingProfile.ActiveRiskAreaIds.SequenceEqual(
                  new[] { "RIVER_FLOOD_ZONE" },
                  StringComparer.Ordinal) &&
              flood.OperatingProfile.Loads.SequenceEqual(authoredFlood.Loads) &&
              flood.ProjectedEvaluation.Loads.Single(load =>
                  load.LoadId == "HOSPITAL").DeliveredKw == 900 &&
              flood.ProjectedEvaluation.Loads.Single(load =>
                  load.LoadId == "WATERWORKS").DeliveredKw == 900,
            $"{label} protective-flood forecast lost exact timing, risk, or safety duty",
            failures);
    }

    private static void RunLongestNightEvents(
        RealtimeSliceMain slice,
        RealtimeSliceData data,
        ICollection<string> failures)
    {
        const string label = "LONGEST_NIGHT";
        _ = AdvanceToMinuteByFrames(
            slice,
            268590,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        ThermalIntervalEvaluation maximum = slice.CoreSnapshot.Thermal.Evaluation;
        Check(slice.ActiveChapterStoryModalForSmoke is null &&
              slice.LatestPresentation.Modal is null &&
              slice.CoreSnapshot.ActiveEventStates.Single().EventId == "MAX_DEMAND" &&
              slice.LatestPresentation.World.Weather == RealtimeWorldWeather.Clear &&
              maximum.Loads.Single(load => load.LoadId == "HOSPITAL").DeliveredKw == 900 &&
              maximum.Loads.Single(load => load.LoadId == "WATERWORKS").DeliveredKw == 900 &&
              maximum.Loads.All(load => load.DeliveredKw == load.DemandKw) &&
              maximum.Assets.Where(asset => asset.UsedKw > 0).All(asset =>
                  asset.State == ThermalOperatingState.Continuous),
            $"{label} storyless maximum demand lost continuous safety supply",
            failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            268770,
            RealtimeSimulationSpeed.VeryFast,
            failures);

        CommercialStoryCard heatwaveStory = data.BaseCampaign.Chapters[7]
            .OperatingPhases.Single(item => item.PhaseId == "HEATWAVE_PEAK").Story!;
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.EventStory,
            "LONGEST_NIGHT",
            "HEATWAVE_PEAK",
            heatwaveStory,
            failures);
        ThermalIntervalEvaluation heatwave = slice.CoreSnapshot.Thermal.Evaluation;
        Check(slice.LatestPresentation.World.Weather == RealtimeWorldWeather.Heat &&
              heatwave.Loads.Single(load => load.LoadId == "HOSPITAL").DeliveredKw == 1600 &&
              heatwave.Loads.Single(load => load.LoadId == "WATERWORKS").DeliveredKw == 1400 &&
              heatwave.Assets.Any(asset =>
                  asset.State == ThermalOperatingState.Emergency),
            $"{label} active heatwave lost heat presentation, safety supply, or emergency operation",
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null,
            $"{label} heatwave story did not restore realtime play",
            failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            268890,
            RealtimeSimulationSpeed.VeryFast,
            failures);

        _ = AdvanceToMinuteByFrames(
            slice,
            268950,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        CommercialStoryCard floodStory = data.BaseCampaign.Chapters[7]
            .OperatingPhases.Single(item =>
                item.PhaseId == "PROTECTIVE_STOP_FLOOD").Story!;
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.EventStory,
            "LONGEST_NIGHT",
            "PROTECTIVE_STOP_FLOOD",
            floodStory,
            failures);
        ThermalIntervalEvaluation flood = slice.CoreSnapshot.Thermal.Evaluation;
        Check(slice.LatestPresentation.World.Weather == RealtimeWorldWeather.Storm &&
              slice.LatestPresentation.World.ActiveRiskAreaIds.SequenceEqual(
                  new[] { "RIVER_FLOOD_ZONE" },
                  StringComparer.Ordinal) &&
              flood.Loads.Single(load => load.LoadId == "HOSPITAL").DeliveredKw == 900 &&
              flood.Loads.Single(load => load.LoadId == "WATERWORKS").DeliveredKw == 900 &&
              flood.Assets.All(asset =>
                  asset.State != ThermalOperatingState.ProtectiveOutage) &&
              flood.Assets.Where(asset => asset.UsedKw > 0).All(asset =>
                  asset.State == ThermalOperatingState.Continuous),
            $"{label} active protective flood lost storm risk or continuous safety reroute",
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null,
            $"{label} protective-flood story did not restore realtime play",
            failures);
    }

    private static (RealtimeChapterOutcome Outcome, RealtimeModalPresentation Result)
        CompleteLongestNightChapter(
            RealtimeSliceMain slice,
            ICollection<string> failures)
    {
        const string label = "LONGEST_NIGHT";
        _ = AdvanceToMinuteByFrames(
            slice,
            269069,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RealtimeR2FrameResult terminalOverrun = slice.InjectFramesForSmoke(
            frameCount: 30,
            framesPerSecond: 60);
        Check(terminalOverrun.RequestedFrameCount == 30 &&
              terminalOverrun.ConsumedFrameCount == 15 &&
              terminalOverrun.RetainedFrameDebt.Count == 0 &&
              slice.RetainedFrameDebt.Count == 0,
            $"{label} retained host-frame overrun beyond the terminal minute",
            failures);
        RealtimeChapterOutcome outcome = slice.CoreSnapshot.CompletedChapters.Single(item =>
            item.ChapterId == "LONGEST_NIGHT");
        RealtimeModalPresentation result = slice.LatestPresentation.Modal ??
            throw new InvalidOperationException(
                $"{label} LONGEST_NIGHT result modal is absent.");
        RealtimeTransition[] thermal = slice.EmittedTransitions
            .Where(item => item.ChapterId == "LONGEST_NIGHT" &&
                RealtimeThermalPresentation.IsThermalTransition(item))
            .ToArray();
        RealtimeTransition emergency = thermal.Single(item => item is
        {
            Minute: 268770,
            AssetKind: ThermalAssetKind.Edge,
            Kind: RealtimeTransitionKind.ThermalEmergencyEntered,
        });
        RealtimeTransition[] thermalArc = thermal.Where(item =>
            item.AssetId == emergency.AssetId).ToArray();
        RealtimeTransition[] finalTransitions = slice.EmittedTransitions
            .Where(item => item.Minute == 269070)
            .ToArray();
        CommercialOperatingPhaseDefinition[] authoredPhases = slice.SliceDataForSmoke
            .BaseCampaign.Chapters[7].OperatingPhases.ToArray();
        RealtimeEventOutcome maximum = outcome.Events.Single(item =>
            item.EventId == "MAX_DEMAND");
        RealtimeEventOutcome heatwave = outcome.Events.Single(item =>
            item.EventId == "HEATWAVE_PEAK");
        RealtimeEventOutcome flood = outcome.Events.Single(item =>
            item.EventId == "PROTECTIVE_STOP_FLOOD");
        Check(slice.CoreSnapshot is
              {
                  Minute: 269070,
                  CampaignComplete: true,
              } &&
              slice.CoreSnapshot.CompletedChapters.Count == 8 &&
              result.Id == RealtimeR2Ids.TutorialResultModal("LONGEST_NIGHT") &&
              outcome.Events.Select(item =>
                      (item.EventId, item.StartMinute, item.EndMinute))
                  .SequenceEqual(
                  [
                      ("MAX_DEMAND", 268590L, 268710L),
                      ("HEATWAVE_PEAK", 268770L, 268890L),
                      ("PROTECTIVE_STOP_FLOOD", 268950L, 269070L),
                  ]) &&
              HasAuthoredSafetyDutyCoverage(
                  maximum,
                  authoredPhases.Single(item => item.PhaseId == maximum.EventId)) &&
              maximum.DutySegments.All(segment => segment.Loads.All(load =>
                  load.DeliveredKw == load.DemandKw)) &&
              HasAuthoredSafetyDutyCoverage(
                  heatwave,
                  authoredPhases.Single(item => item.PhaseId == heatwave.EventId)) &&
              HasAuthoredSafetyDutyCoverage(
                  flood,
                  authoredPhases.Single(item => item.PhaseId == flood.EventId)) &&
              thermalArc is
              [
                  {
                      Minute: 268770,
                      AssetKind: ThermalAssetKind.Edge,
                      Kind: RealtimeTransitionKind.ThermalEmergencyEntered,
                  },
                  {
                      Minute: 268845,
                      AssetKind: ThermalAssetKind.Edge,
                      Kind: RealtimeTransitionKind.ThermalProtectiveTrip,
                  },
                  {
                      Minute: 268935,
                      AssetKind: ThermalAssetKind.Edge,
                      Kind: RealtimeTransitionKind.ThermalRecovered,
                  },
              ] &&
              thermal.All(item =>
                  item.Minute < 268950 || item.Kind is not
                      (RealtimeTransitionKind.ThermalEmergencyEntered or
                       RealtimeTransitionKind.ThermalProtectiveTrip)) &&
              finalTransitions is
              [
                  {
                      Kind: RealtimeTransitionKind.EventCompleted,
                      ChapterId: "LONGEST_NIGHT",
                      EventId: "PROTECTIVE_STOP_FLOOD",
                  },
                  {
                      Kind: RealtimeTransitionKind.ChapterCompleted,
                      ChapterId: "LONGEST_NIGHT",
                      EventId: null,
                  },
                  {
                      Kind: RealtimeTransitionKind.CampaignCompleted,
                      ChapterId: "LONGEST_NIGHT",
                      EventId: null,
                  },
              ],
            $"{label} did not close exact safety segments, thermal arc, or result FIFO",
            failures);
        return (outcome, result);
    }

    private static bool HasAuthoredSafetyDutyCoverage(
        RealtimeEventOutcome outcome,
        CommercialOperatingPhaseDefinition authored)
    {
        (string LoadId, CommercialObligationKind Obligation, long DemandKw, bool Required)[]
            expectedLoads = authored.Loads.Select(load =>
                (load.LoadId, load.Obligation, load.DemandKw, true)).ToArray();
        return outcome.EventId == authored.PhaseId &&
            outcome.DutySegments.Count > 0 &&
            outcome.DutySegments[0].StartMinute == outcome.StartMinute &&
            outcome.DutySegments[^1].EndMinute == outcome.EndMinute &&
            outcome.DutySegments.Select((segment, index) =>
                index == 0 || outcome.DutySegments[index - 1].EndMinute ==
                    segment.StartMinute).All(connected => connected) &&
            outcome.DutySegments.All(segment =>
                segment.StartMinute < segment.EndMinute &&
                segment.Loads.Select(load =>
                        (load.LoadId, load.Obligation, load.DemandKw, load.Required))
                    .SequenceEqual(expectedLoads) &&
                segment.Loads.Where(load =>
                        load.Obligation == CommercialObligationKind.SafetyDuty)
                    .All(load => load.DeliveredKw == load.DemandKw));
    }

    private static void RequireDecisionWindow(
        RealtimeSliceMain slice,
        CommercialCampaignChapterDefinition authored,
        string chapterId,
        string windowId,
        ICollection<string> failures,
        string label)
    {
        CommercialDecisionWindowDefinition window = authored.DecisionWindows.Single(item =>
            item.WindowId == windowId);
        RealtimeChapterStoryModalRequest request =
            slice.ActiveChapterStoryModalForSmoke ??
            throw new InvalidOperationException($"{label} {windowId} is not active.");
        RealtimeModalPresentation modal = slice.LatestPresentation.Modal ??
            throw new InvalidOperationException($"{label} {windowId} modal is absent.");
        Check(request is
              {
                  Purpose: RealtimeChapterStoryModalPurpose.DecisionWindowStory,
                  ChapterId: var requestedChapterId,
              } &&
              requestedChapterId == chapterId &&
              request.WindowId == windowId &&
              modal.Id == RealtimeR2Ids.TutorialDecisionWindowModal(
                  chapterId,
                  windowId) &&
              modal.Eyebrow == window.Story!.Speaker &&
              modal.Heading == window.Story.Title &&
              modal.Body == window.Story.Body &&
              modal.PrimaryAction.Id == RealtimeR2Ids.DecisionWindowContinueAction,
            $"{label} {windowId} authored modal drifted",
            failures);
    }

    private static void ValidateReleaseTutorialConnectionFailureResult(
        ICollection<string> failures)
    {
        var slice = new RealtimeSliceMain();
        try
        {
            slice.BootstrapNativeReleaseForSmoke(
                RealtimeNativeRouteCatalog.TutorialThroughSecondSource);
        }
        catch
        {
            slice.Free();
            throw;
        }
        using var sliceLifetime = slice.FreeAfterSmoke();
        RealtimeSliceData data = slice.SliceDataForSmoke;
        Check(slice.ClosePresentedStoryModalForSmoke() is null,
            "failure route FIRST_LIGHT briefing did not close",
            failures);
        _ = BuildTutorialFirstLightNetwork(slice, failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            1320,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterResult,
            "FIRST_LIGHT",
            null,
            data.BaseCampaign.Chapters[0].ResultCards.Standard!,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is not null,
            "failure route FIRST_LIGHT result did not queue SECOND_HEART briefing",
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "SECOND_HEART",
            null,
            data.BaseCampaign.Chapters[1].Briefing,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null,
            "failure route SECOND_HEART briefing did not close",
            failures);

        RealtimeProjectQuote oneRoute = OrderTutorialLine(
            slice,
            "EAST_RESIDENTIAL_TERMINAL",
            [new CoreMapPoint(2550, 1050)],
            "HOSPITAL_TERMINAL",
            "STANDARD_LINE",
            "STANDARD_POLE",
            failures,
            "single hospital corridor");
        Check(oneRoute.CompletionMinute < 1680,
            "single hospital corridor missed the connection-freeze boundary",
            failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            1680,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        RealtimeConnectionRequirementAssessment frozenOne =
            slice.CoreSnapshot.Forecast.ConnectionRequirementAssessment ??
            throw new InvalidOperationException(
                "SECOND_HEART failure route has no frozen connection assessment.");
        Check(frozenOne is { FrozenForChapter: true, Satisfied: false } &&
              frozenOne.Facts.Single() is
              {
                  CurrentConnections: 1,
                  RequiredConnections: 2,
              } &&
              slice.LatestPresentation.Hud.Objective.Contains(
                  "1/2",
                  StringComparison.Ordinal),
            "SECOND_HEART failure route did not freeze/show exact Core-owned 1/2",
            failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            1800,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        CommercialStoryCard floodStory = data.BaseCampaign.Chapters[1]
            .OperatingPhases.Single(item =>
                item.PhaseId == "FLOOD_ISOLATION_TEST").Story!;
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.EventStory,
            "SECOND_HEART",
            "FLOOD_ISOLATION_TEST",
            floodStory,
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null,
            "failure route flood story did not close",
            failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            1860,
            RealtimeSimulationSpeed.VeryFast,
            failures);

        RealtimeChapterOutcome outcome = slice.CoreSnapshot.CompletedChapters.Single(item =>
            item.ChapterId == "SECOND_HEART");
        CommercialStoryCard standard = data.BaseCampaign.Chapters[1]
            .ResultCards.Standard!;
        RealtimeModalPresentation result = slice.LatestPresentation.Modal ??
            throw new InvalidOperationException(
                "SECOND_HEART failure route did not present its factual result.");
        Check(!outcome.ObjectiveSatisfied &&
              outcome.ConnectionRequirementAssessment is
              {
                  FrozenForChapter: true,
                  Satisfied: false,
              } &&
              result.Id == RealtimeR2Ids.TutorialResultModal("SECOND_HEART") &&
              result.Eyebrow == "계통운영 기록" &&
              result.Heading.Contains("목표 미달", StringComparison.Ordinal) &&
              result.Body.Contains("1/2", StringComparison.Ordinal) &&
              (!string.Equals(result.Eyebrow, standard.Speaker, StringComparison.Ordinal) ||
               !string.Equals(result.Heading, standard.Title, StringComparison.Ordinal) ||
               !string.Equals(result.Body, standard.Body, StringComparison.Ordinal)),
            "SECOND_HEART 1/2 failure counterfeited the authored positive result",
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is not null &&
              slice.FormativeTutorialResultChapterIdsForSmoke.SequenceEqual(
                  new[] { "FIRST_LIGHT" },
                  StringComparer.Ordinal) &&
              !slice.FormativeTutorialFullFlowRecordedForSmoke,
            "failed SECOND_HEART minted a formative token or blocked the next briefing",
            failures);
        RequireAuthoredTutorialModal(
            slice,
            RealtimeChapterStoryModalPurpose.ChapterBriefing,
            "SECOND_SOURCE",
            null,
            data.BaseCampaign.Chapters[2].Briefing,
            failures);
    }

    private static string BuildTutorialFirstLightNetwork(
        RealtimeSliceMain slice,
        ICollection<string> failures)
    {
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildNode,
                    "NODE:SMALL_SUBSTATION")),
            "tutorial node tool", failures, coreCommandExpected: false);
        RequireIntent(slice.ApplyIntentForSmoke(new RealtimeR2Intent(
                RealtimeR2IntentKind.SetNodeDraft,
                FirstId: "SMALL_SUBSTATION",
                Position: new CoreMapPoint(2100, 700))),
            "tutorial substation draft", failures);
        RealtimeProjectQuote nodeQuote = slice.PreviewNodeOrderForSmoke();
        RequireIntent(slice.ApplyIntentForSmoke(
                new RealtimeR2Intent(RealtimeR2IntentKind.OrderNode)),
            "tutorial substation order", failures);
        string substationId = slice.CoreSnapshot.Construction.ActiveConstruction!
            .NodeIds.Single();
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SetSpeed(RealtimeSimulationSpeed.VeryFast)),
            "tutorial first-light speed", failures, coreCommandExpected: false);
        _ = AdvanceToMinuteByFrames(
            slice,
            nodeQuote.CompletionMinute!.Value,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        _ = OrderTutorialLine(
            slice,
            "WEST_SOURCE_NODE",
            [
                new CoreMapPoint(750, 650),
                new CoreMapPoint(1050, 650),
                new CoreMapPoint(1600, 650),
            ],
            substationId,
            "STANDARD_LINE",
            "STANDARD_POLE",
            failures,
            "tutorial west source corridor");
        _ = OrderTutorialLine(
            slice,
            substationId,
            Array.Empty<CoreMapPoint>(),
            "EAST_RESIDENTIAL_TERMINAL",
            "STANDARD_LINE",
            "STANDARD_POLE",
            failures,
            "tutorial east service corridor");
        return substationId;
    }

    private static RealtimeProjectQuote OrderTutorialLine(
        RealtimeSliceMain slice,
        string startNodeId,
        IReadOnlyList<CoreMapPoint> points,
        string endNodeId,
        string lineClassId,
        string poleClassId,
        ICollection<string> failures,
        string label)
    {
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildLine,
                    RealtimeR2Ids.LineTool(lineClassId, poleClassId))),
            $"{label} tool", failures, coreCommandExpected: false);
        RequireIntent(slice.ApplyIntentForSmoke(RealtimeR2Intent.StartLineDraft(
                startNodeId,
                lineClassId,
                poleClassId)),
            $"{label} start", failures);
        foreach (CoreMapPoint point in points)
        {
            RequireIntent(slice.ApplyIntentForSmoke(
                    RealtimeR2Intent.AddLinePoint(point)),
                $"{label} point", failures);
        }
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.FinishLineDraft(endNodeId)),
            $"{label} finish", failures);
        RealtimeProjectQuote quote = slice.PreviewLineOrderForSmoke();
        Check(quote.Accepted && quote.BuildMinutes is > 0 &&
              quote.CompletionMinute.HasValue,
            $"{label} quote rejected", failures);
        RequireIntent(slice.ApplyIntentForSmoke(RealtimeR2Intent.OrderLine()),
            $"{label} order", failures);
        _ = AdvanceToMinuteByFrames(
            slice,
            quote.CompletionMinute!.Value,
            slice.InteractionState.PresentedSpeed,
            failures);
        return quote;
    }

    private static string OrderTutorialNode(
        RealtimeSliceMain slice,
        CoreMapPoint position,
        ICollection<string> failures,
        string label)
    {
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildNode,
                    "NODE:SMALL_SUBSTATION")),
            $"{label} tool", failures, coreCommandExpected: false);
        RequireIntent(slice.ApplyIntentForSmoke(new RealtimeR2Intent(
                RealtimeR2IntentKind.SetNodeDraft,
                FirstId: "SMALL_SUBSTATION",
                Position: position)),
            $"{label} draft", failures);
        RealtimeProjectQuote quote = slice.PreviewNodeOrderForSmoke();
        Check(quote.Accepted && quote.CompletionMinute.HasValue,
            $"{label} quote rejected", failures);
        RequireIntent(slice.ApplyIntentForSmoke(
                new RealtimeR2Intent(RealtimeR2IntentKind.OrderNode)),
            $"{label} order", failures);
        string nodeId = slice.CoreSnapshot.Construction.ActiveConstruction!
            .NodeIds.Single();
        _ = AdvanceToMinuteByFrames(
            slice,
            quote.CompletionMinute!.Value,
            slice.InteractionState.PresentedSpeed,
            failures);
        return nodeId;
    }

    private static void RequireAuthoredTutorialModal(
        RealtimeSliceMain slice,
        RealtimeChapterStoryModalPurpose purpose,
        string chapterId,
        string? eventId,
        CommercialStoryCard card,
        ICollection<string> failures)
    {
        RealtimeModalPresentation? modal = slice.LatestPresentation.Modal;
        RealtimeChapterStoryModalRequest request =
            slice.ActiveChapterStoryModalForSmoke ??
            (string.Equals(
                    modal?.Id,
                    RealtimeR2Ids.ChapterBriefingModal,
                    StringComparison.Ordinal)
                ? RealtimeChapterStoryFlow.InitialBriefing(chapterId)
                : throw new InvalidOperationException(
                    "Tutorial flow has no active modal step."));
        Check(modal is not null && request.Purpose == purpose &&
              request.ChapterId == chapterId && request.EventId == eventId &&
              modal.Eyebrow == card.Speaker && modal.Heading == card.Title &&
              modal.Body == card.Body,
            $"tutorial authored modal mismatch: {purpose}/{chapterId}/{eventId}",
            failures);
    }

    private static void ObserveTutorialRail(
        RealtimeSliceMain slice,
        List<string> observed)
    {
        HashSet<string> authored = new(StringComparer.Ordinal)
        {
            "FIRST_LIGHT_SUPPLY",
            "HOSPITAL_TRANSFER_TEST",
            "FLOOD_ISOLATION_TEST",
            "WEST_MAIN_COMMISSIONING_TEST",
            "SOUTH_SOURCE_COMMISSIONING_TEST",
        };
        foreach (RealtimeTimelineItemPresentation item in slice.LatestPresentation.Rail.Items)
        {
            if (authored.Contains(item.Id) &&
                !observed.Contains(item.Id, StringComparer.Ordinal))
            {
                observed.Add(item.Id);
            }
        }
    }

    private static string TutorialOutcomeDiagnostic(RealtimeChapterOutcome outcome) =>
        string.Join(
            "; ",
            outcome.Events.Select(item =>
                $"{item.EventId}:safety={item.SafetySatisfied}," +
                $"promise={item.PromiseSatisfied}," +
                $"safety-unserved={item.SafetyUnservedMinutes}," +
                $"promise-unserved={item.PromiseUnservedMinutes}," +
                $"loads=[{string.Join(",", item.FinalEvaluation.Loads.Select(load =>
                    $"{load.LoadId}:{load.DeliveredKw}/{load.DemandKw}:" +
                    $"{load.Failure?.Kind.ToString() ?? "ok"}"))}]")) +
        $"; connection={outcome.ConnectionRequirementAssessment?.Satisfied}";

    private static void AssertNoUnknownRailTargets(
        RealtimeSliceMain slice,
        RealtimeSlicePresentation presentation,
        ICollection<string> failures,
        string phase)
    {
        string[] unknown = presentation.Rail.Items
            .Where(item => RealtimeTimelineTargetResolver.Resolve(
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
