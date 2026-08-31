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
    private static void ValidateVisualLayoutData(ICollection<string> failures)
    {
        RealtimeVisualLayoutDefinition layout = RealtimeVisualLayoutStore.LoadCanonical();
        string first = RealtimeVisualLayoutStore.Serialize(layout);
        string second = RealtimeVisualLayoutStore.Serialize(
            RealtimeVisualLayoutStore.LoadCanonical());
        Check(string.Equals(first, second, StringComparison.Ordinal),
            "visual authoring scene projection is not deterministic", failures);

        bool invalidSceneNodeRejected = false;
        PackedScene authoringScene = GD.Load<PackedScene>(
            RealtimeVisualLayoutStore.ResourcePath)!;
        Node root = authoringScene.Instantiate();
        try
        {
            Node districts = root.GetNode<Node>("Districts");
            districts.AddChild(new Node2D { Name = "INVALID_DISTRICT_TYPE" });
            _ = RealtimeVisualLayoutStore.Project(root);
        }
        catch (InvalidDataException)
        {
            invalidSceneNodeRejected = true;
        }
        finally
        {
            root.Free();
        }
        Check(invalidSceneNodeRejected,
            "visual authoring scene accepted an invalid district node type", failures);

        bool duplicateDistrictRejected = false;
        try
        {
            layout.Districts.Single(item =>
                item.NodeId == "EAST_RESIDENTIAL_TERMINAL").NodeId = "WATER_TERMINAL";
            RealtimeVisualLayoutStore.Validate(layout);
        }
        catch (InvalidDataException)
        {
            duplicateDistrictRejected = true;
        }
        Check(duplicateDistrictRejected,
            "visual layout accepted a duplicate/missing district ID", failures);
    }

    private static void ValidateReleaseFirstLightLateConstructionBoundary(
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
        using var lifetime = slice.FreeAfterSmoke();
        Check(slice.ClosePresentedStoryModalForSmoke() is null,
            "late FIRST_LIGHT fixture did not close the initial briefing",
            failures);

        string substationId = OrderTutorialNode(
            slice,
            new CoreMapPoint(2100, 700),
            failures,
            "late FIRST_LIGHT substation");
        _ = OrderTutorialLine(
            slice,
            substationId,
            Array.Empty<CoreMapPoint>(),
            "EAST_RESIDENTIAL_TERMINAL",
            "STANDARD_LINE",
            "STANDARD_POLE",
            failures,
            "late FIRST_LIGHT east service");
        RealtimeR2AdvanceResult preparationDelay = slice.AdvanceToForSmoke(1200);
        Check(preparationDelay.Advance.Snapshot.Minute == 1200 &&
              preparationDelay.Advance.Transitions.All(item =>
                  item.Kind != RealtimeTransitionKind.EventStarted),
            "late FIRST_LIGHT fixture did not stop before the authored test",
            failures);

        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.SelectBuildTool(
                    RealtimeTool.BuildLine,
                    RealtimeR2Ids.LineTool("STANDARD_LINE", "STANDARD_POLE"))),
            "late FIRST_LIGHT west tool", failures, coreCommandExpected: false);
        Check(slice.LatestPresentation.World.GuidanceTarget is
              {
                  NodeId: "WEST_SOURCE_NODE",
              } &&
              slice.LatestPresentation.World.CompatibleLineNodeIds.Contains(
                  "WEST_SOURCE_NODE",
                  StringComparer.Ordinal),
            "FIRST_LIGHT line guidance did not expose the exact compatible west source node",
            failures);
        RequireIntent(slice.ApplyIntentForSmoke(RealtimeR2Intent.StartLineDraft(
                "WEST_SOURCE_NODE",
                "STANDARD_LINE",
                "STANDARD_POLE")),
            "late FIRST_LIGHT west start", failures);
        CoreMapPoint[] corridor =
        [
            new(750, 650),
            new(1050, 650),
            new(1600, 650),
        ];
        foreach (CoreMapPoint point in corridor)
        {
            RequireIntent(slice.ApplyIntentForSmoke(
                    RealtimeR2Intent.AddLinePoint(point)),
                $"late FIRST_LIGHT west point {point.XUnit},{point.YUnit}", failures);
        }
        RequireIntent(slice.ApplyIntentForSmoke(
                RealtimeR2Intent.FinishLineDraft(substationId)),
            "late FIRST_LIGHT west finish", failures);
        RealtimeProjectQuote lateQuote = slice.PreviewLineOrderForSmoke();
        long chapterEnd = checked(
            slice.CoreSnapshot.ChapterStartMinute +
            slice.CoreSnapshot.Chapter.EndOffsetMinutes);
        long eventStart = checked(
            slice.CoreSnapshot.ChapterStartMinute +
            slice.CoreSnapshot.Chapter.ScheduledEvents.Single().StartOffsetMinutes);
        Check(lateQuote is { Accepted: true, CompletionMinute: long completion } &&
              completion > eventStart && completion < chapterEnd,
            "late FIRST_LIGHT fixture did not produce a legal post-test completion",
            failures);
        Check(slice.LatestPresentation.ActionDock.Detail.Contains(
                  "늦음",
                  StringComparison.Ordinal),
            "post-test FIRST_LIGHT quote did not warn about its exact lateness",
            failures);
        RequireIntent(slice.ApplyIntentForSmoke(RealtimeR2Intent.OrderLine()),
            "late FIRST_LIGHT west order", failures);

        slice.RequestActionForSmoke(RealtimeR2Ids.AdvanceFirstLightAction);
        Check(slice.CoreSnapshot.Minute == eventStart &&
              slice.CoreSnapshot.ActiveEventStates.Single().EventId ==
                  "FIRST_LIGHT_SUPPLY" &&
              slice.ActiveChapterStoryModalForSmoke is null &&
              slice.LatestPresentation.ActionDock.PrimaryAction?.Label ==
                  "공사 완료까지 진행",
            "late construction did not stop at the visible FIRST_LIGHT test boundary",
            failures);
        slice.RequestActionForSmoke(RealtimeR2Ids.AdvanceFirstLightAction);
        long completionMinute = lateQuote.CompletionMinute!.Value;
        Check(slice.CoreSnapshot.Minute == completionMinute &&
              slice.CoreSnapshot.Construction.ActiveConstruction is null &&
              slice.LatestPresentation.ActionDock.PrimaryAction?.Label ==
                  "시험 결과까지 진행",
            "late FIRST_LIGHT construction did not commission at its exact in-test minute",
            failures);
        RequireIntent(slice.ApplyIntentForSmoke(RealtimeR2Intent.StartLineDraft(
                substationId,
                "STANDARD_LINE",
                "STANDARD_POLE")),
            "late FIRST_LIGHT stale draft start", failures);
        RealtimeR2IntentResult sameEndpoint = slice.ApplyIntentForSmoke(
            RealtimeR2Intent.FinishLineDraft(substationId));
        Check(!sameEndpoint.Accepted &&
              slice.CoreSnapshot.Construction.LineDraft is not null,
            "late FIRST_LIGHT fixture did not retain the native stale open draft",
            failures);
        slice.RequestActionForSmoke(RealtimeR2Ids.AdvanceFirstLightAction);
        Check(slice.CoreSnapshot.Minute == chapterEnd &&
              slice.CoreSnapshot.CompletedChapters.Any(item =>
                  string.Equals(item.ChapterId, "FIRST_LIGHT", StringComparison.Ordinal)) &&
              slice.ActiveChapterStoryModalForSmoke is
              {
                  Purpose: RealtimeChapterStoryModalPurpose.ChapterResult,
                  ChapterId: "FIRST_LIGHT",
              },
            "late construction advance crossed the typed FIRST_LIGHT result boundary",
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is not null &&
              slice.CoreSnapshot.Construction.LineDraft is null &&
              slice.InteractionState.Tool == RealtimeTool.Inspect &&
              string.IsNullOrEmpty(slice.LatestPresentation.Pointer.Message) &&
              slice.ActiveChapterStoryModalForSmoke is
              {
                  Purpose: RealtimeChapterStoryModalPurpose.ChapterBriefing,
                  ChapterId: "SECOND_HEART",
              },
            "late FIRST_LIGHT result did not hand off to SECOND_HEART briefing",
            failures);
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
}
#endif
