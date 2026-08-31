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
              context.Sections.Count == 2 &&
              context.Sections.Single(item => item.Heading == "결정과 전망") is
                  { Body: var decisionSummary } &&
              decisionSummary.Contains("현재 미선택\n", StringComparison.Ordinal) &&
              decisionSummary.Contains("Keep 가정", StringComparison.Ordinal),
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

        // FIRST_LIGHT's station now radius-serves Water as well as East. Explicit
        // Defer without another station is therefore a valid safety route, while
        // excluded North demand must still never be described as fulfilled.
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
        Check(deferSafetyFailureOutcome.ObjectiveSatisfied &&
              deferSafetyFailureOutcome.PromiseDecision ==
                  CommercialPromiseDecision.Defer &&
              deferSafetyFailureOutcome.Events.All(item => item.SafetySatisfied) &&
              deferSafetyFailureResult.Eyebrow == authoredDeferred.Speaker &&
              deferSafetyFailureResult.Heading == authoredDeferred.Title &&
              deferSafetyFailureResult.Body == authoredDeferred.Body &&
              !deferSafetyFailureResult.Body.Contains(
                  "약속 Defer 2/2 충족",
                  StringComparison.Ordinal),
            "Defer route conflated excluded North demand with promise fulfillment " +
            "or lost the authored deferred result",
            failures);
        _ = deferSafetyFailureSlice.ClosePresentedStoryModalForSmoke();
        Check(deferSafetyFailureSlice.FormativeTutorialResultChapterIdsForSmoke.Count == 3 &&
              !deferSafetyFailureSlice.FormativeTutorialFullFlowRecordedForSmoke,
            "Defer route minted a formative token",
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
                  "4d9709da70891e2a32956d53d5607b0f53cc254a0aa609d237b1dabb0bb2232e" &&
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
              slice.InteractionState.Tool == RealtimeTool.Inspect &&
              slice.InteractionState.SelectedBuildToolId is null &&
              string.IsNullOrEmpty(slice.LatestPresentation.Pointer.Message) &&
              slice.FormativeTutorialResultChapterIdsForSmoke.SequenceEqual(
                  new[] { "FIRST_LIGHT", "SECOND_HEART", "SECOND_SOURCE" },
                  StringComparer.Ordinal),
            "production result action did not preserve the three-result chain or clear " +
            "planning transients into North Bank",
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
              slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused &&
              slice.LatestPresentation.Hud.SimulationState ==
                  RealtimeSimulationState.PlayerPaused &&
              slice.LatestPresentation.Hud.Speed == RealtimeSimulationSpeed.Paused,
            "North planning window did not close into explicit planning pause",
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
            includeNorth
                ? new CoreMapPoint(2500, 500)
                : new CoreMapPoint(1900, 350),
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
              slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused,
            $"{label} HOT planning did not close into explicit planning pause",
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
            // Keep the factory service area clear of the hospital. With direct
            // radius service, the former 2050/1650 fixture also covered the
            // hospital and consumed this small station's capacity before the
            // 2,700 kW night-shift promise was evaluated.
            new CoreMapPoint(2420, 1910),
            failures,
            $"{label} factory substation");
        string lineClassId = reinforced ? "REINFORCED_LINE" : "STANDARD_LINE";
        string poleClassId = reinforced ? "REINFORCED_POLE" : "STANDARD_POLE";
        CoreMapPoint[] points = reinforced
            ?
            [
                new CoreMapPoint(700, 1850),
                new CoreMapPoint(1190, 1850),
                new CoreMapPoint(1775, 1850),
            ]
            :
            [
                new CoreMapPoint(700, 1850),
                new CoreMapPoint(1200, 1850),
                new CoreMapPoint(1775, 1850),
                new CoreMapPoint(2075, 1880),
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
              connection is { FrozenForChapter: false, Satisfied: false } &&
              connection.Facts.Single() is
              {
                  NodeId: "EAST_RESIDENTIAL_TERMINAL",
                  CurrentConnections: 1,
                  RequiredConnections: 2,
              },
            $"{label} did not inherit the exact clock and meaningful East 1/2 objective",
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
              slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused,
            $"{label} flood window did not precede the event or preserve planning pause",
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
        string eastServiceSubstationId = slice.CoreSnapshot.Construction.World.Nodes
            .Single(item => item.ClassId == "SMALL_SUBSTATION" &&
                item.Position == new CoreMapPoint(2100, 700))
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
        RealtimeProjectQuote highlandQuote = OrderTutorialLine(
            slice,
            "SOUTH_SOURCE_NODE",
            points,
            eastServiceSubstationId,
            reinforced ? "REINFORCED_LINE" : "STANDARD_LINE",
            reinforced ? "REINFORCED_POLE" : "STANDARD_POLE",
            failures,
            $"{label} flood-safe highland line");
        string branchPoleClassId = reinforced ? "REINFORCED_POLE" : "STANDARD_POLE";
        string highlandBranchPoleId = slice.CoreSnapshot.Construction.World.Nodes
            .Single(item => item.ClassId == branchPoleClassId &&
                item.Position == points[^1])
            .NodeId;
        RealtimeProjectQuote connectionQuote = OrderTutorialLine(
            slice,
            highlandBranchPoleId,
            Array.Empty<CoreMapPoint>(),
            "EAST_RESIDENTIAL_TERMINAL",
            reinforced ? "REINFORCED_LINE" : "STANDARD_LINE",
            reinforced ? "REINFORCED_POLE" : "STANDARD_POLE",
            failures,
            $"{label} East second connection");
        RealtimeConnectionRequirementAssessment connection = slice.CoreSnapshot
            .Forecast.ConnectionRequirementAssessment ?? throw new InvalidOperationException(
                $"{label} post-construction connection assessment is absent.");
        Check(highlandQuote.RiskAreaIds.Count == 0 &&
              connectionQuote.RiskAreaIds.Count == 0 &&
              connectionQuote.CompletionMinute <= 267150 &&
              slice.CoreSnapshot.Minute == connectionQuote.CompletionMinute &&
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
              slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused,
            $"{label} planning window did not close into explicit planning pause",
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
            // The reinforced junction covers Hospital + East, while its separate
            // Water terminal edge satisfies the chapter's second-connection fact.
            // Their heatwave sum creates one readable emergency arc; their smaller
            // flood sum returns inside the continuous limit after recovery.
            new CoreMapPoint(2400, 1050),
            failures,
            $"{label} continuous Water substation");
        _ = OrderTutorialLine(
            slice,
            reinforcedTrunkPoleId,
            [new CoreMapPoint(2080, 1150)],
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
        Check(slice.CoreSnapshot.Minute == 267462 &&
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
        RealtimeForecastEvent planned = slice.LatestPresentation.BaseForecast.Events
            .Single(item => item.EventId == "WEST_SOURCE_PLANNED_OUTAGE");
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
            $"{label} planned-outage forecast lost authored timing, West isolation, or supply; " +
            $"loads={string.Join(',', planned.ProjectedEvaluation.Loads.Select(item =>
                $"{item.LoadId}:{item.DeliveredKw}/{item.DemandKw}:{item.SourceId}:{item.Failure?.Kind}"))}; " +
            $"assets={string.Join(',', planned.ProjectedEvaluation.Assets.Where(item => item.UsedKw > 0).Select(item =>
                $"{item.AssetId}:{item.UsedKw}:{item.ContinuousKw}:{item.EmergencyKw}:{item.State}"))}; " +
            $"transitions={string.Join(',', planned.TemporalProjection.Transitions.Select(item =>
                $"{item.Minute}:{item.AssetKind}:{item.Kind}:{item.AssetId}"))}",
            failures);

        if (slice.CoreSnapshot.Minute < 267450)
        {
            _ = AdvanceToMinuteByFrames(
                slice,
                267450,
                RealtimeSimulationSpeed.VeryFast,
                failures);
        }
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
            $"{label} return-service forecast lost reveal order or continuous supply; " +
            $"loads={string.Join(',', returned.ProjectedEvaluation.Loads.Select(item =>
                $"{item.LoadId}:{item.DeliveredKw}/{item.DemandKw}:{item.SourceId}:{item.Failure?.Kind}"))}; " +
            $"transitions={string.Join(',', returned.TemporalProjection.Transitions.Select(item =>
                $"{item.Minute}:{item.AssetKind}:{item.Kind}:{item.AssetId}"))}",
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
            $"{label} active outage lost frozen 2/2, West isolation, or South supply; " +
            $"loads={string.Join(',', actual.Loads.Select(item =>
                $"{item.LoadId}:{item.DeliveredKw}/{item.DemandKw}:{item.SourceId}:{item.Failure?.Kind}"))}",
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
              slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused &&
              slice.EmittedTransitions.All(item =>
                  item.ChapterId != "LONGEST_NIGHT" ||
                  item.Kind != RealtimeTransitionKind.EventStarted),
            $"{label} final planning window did not preserve planning pause before events",
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
        RealtimeThermalTransition? projectedEmergency = heatwave.TemporalProjection
            .Transitions.FirstOrDefault(transition => transition is
            {
                Minute: 268770,
                AssetKind: ThermalAssetKind.Node,
                Kind: RealtimeThermalTransitionKind.EmergencyEntered,
            });
        RealtimeThermalTransition[] projectedThermalArc = projectedEmergency is null
            ? []
            : heatwave.TemporalProjection.Transitions.Where(transition =>
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
              projectedEmergency is not null &&
              projectedThermalArc is
              [
                  {
                      Minute: 268770,
                      AssetKind: ThermalAssetKind.Node,
                      Kind: RealtimeThermalTransitionKind.EmergencyEntered,
                  },
                  {
                      Minute: 268860,
                      AssetKind: ThermalAssetKind.Node,
                      Kind: RealtimeThermalTransitionKind.ProtectiveTrip,
                  },
              ],
            $"{label} heatwave forecast lost exact timing, duty, or thermal arc; " +
            $"assets={string.Join(',', heatwave.ProjectedEvaluation.Assets.Where(item => item.UsedKw > 0).Select(item =>
                $"{item.AssetId}:{item.AssetKind}:{item.UsedKw}:{item.ContinuousKw}:{item.EmergencyKw}:{item.State}"))}; " +
            $"observed={string.Join(',', heatwave.TemporalProjection.Transitions.Select(item =>
                $"{item.Minute}:{item.AssetKind}:{item.Kind}:{item.AssetId}"))}",
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
            AssetKind: ThermalAssetKind.Node,
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
                      AssetKind: ThermalAssetKind.Node,
                      Kind: RealtimeTransitionKind.ThermalEmergencyEntered,
                  },
                  {
                      Minute: 268860,
                      AssetKind: ThermalAssetKind.Node,
                      Kind: RealtimeTransitionKind.ThermalProtectiveTrip,
                  },
                  {
                      Minute: 268950,
                      AssetKind: ThermalAssetKind.Node,
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
}
#endif
