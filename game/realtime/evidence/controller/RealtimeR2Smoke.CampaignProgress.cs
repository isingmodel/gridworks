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
                  "818124ba86c0ec4be7dd033390c6aa623152ae193425189d8394fc9fc501e484" &&
              data.WorldSha256 ==
                  "746bd2706c2b1d02141f70a70322e3610917e286f67a9fa60492fb8fa997a79f" &&
              data.CampaignOverlaySha256 ==
                  "ef962a272683bfd6761fbf10a0ca14cb6c8bf90cdfde810b468ad451088f2258" &&
              data.FullComposedCampaignSha256 ==
                  "4d9709da70891e2a32956d53d5607b0f53cc254a0aa609d237b1dabb0bb2232e" &&
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
                  "1/2",
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
        Check(slice.LatestPresentation.ActionDock.PrimaryAction?.Id ==
                  RealtimeR2Ids.AdvanceFirstLightAction,
            "release substation construction omitted its completion advance action",
            failures);
        slice.RequestActionForSmoke(RealtimeR2Ids.AdvanceFirstLightAction);
        Check(slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused &&
              slice.LatestPresentation.Hud.Objective.Contains(
                  "2/2",
                  StringComparison.Ordinal) &&
              slice.LatestPresentation.BuildShelf.Guidance.Contains(
                  "공사 완료",
                  StringComparison.Ordinal),
            "release FIRST_LIGHT node completion did not pause with the source-corridor step",
            failures);
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
        slice.RequestActionForSmoke(RealtimeR2Ids.AdvanceFirstLightAction);
        Check(slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused &&
              slice.LatestPresentation.Hud.Objective.Contains(
                  "경로 준비 완료",
                  StringComparison.Ordinal),
            "release FIRST_LIGHT source-feed completion did not pause with ready guidance",
            failures);

        RealtimeNextEventPresentation? next = slice.LatestPresentation.Rail.NextEvent;
        Check(next is
              {
                  EventId: "FIRST_LIGHT_SUPPLY",
                  StartMinute: 1260,
                  EndMinute: 1320,
                  MinutesUntilStart: 22,
              } &&
              slice.CoreSnapshot.CashUnit == 7_055_000,
            "release live state lost its exact next-event countdown or construction cost",
            failures);
        Check(slice.LatestPresentation.ActionDock.PrimaryAction is
              {
                  Id: RealtimeR2Ids.AdvanceFirstLightAction,
                  Label: "첫 공급 시험까지 진행",
              },
            "release ready network omitted its event-start advance action",
            failures);
        slice.RequestActionForSmoke(RealtimeR2Ids.AdvanceFirstLightAction);
        Check(slice.CoreSnapshot.ActiveEventStates.Single().EventId ==
                  "FIRST_LIGHT_SUPPLY" &&
              slice.EmittedTransitions.Any(item =>
                  item.Kind == RealtimeTransitionKind.EventStarted &&
                  item.Minute == 1260 &&
                  item.EventId == "FIRST_LIGHT_SUPPLY"),
            "release FIRST_LIGHT event did not begin on its authored live boundary",
            failures);
        Check(slice.LatestPresentation.ActionDock.PrimaryAction is
              {
                  Id: RealtimeR2Ids.AdvanceFirstLightAction,
                  Label: "시험 결과까지 진행",
              },
            "release active event omitted its result advance action",
            failures);
        slice.RequestActionForSmoke(RealtimeR2Ids.AdvanceFirstLightAction);

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
              completed.CashUnit == 7_055_000 &&
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
              completedConstructionIds.Length == 2,
            "release FIRST_LIGHT did not complete safely through exact event/chapter/campaign transitions",
            failures);
        Check(result.Id == RealtimeR2Ids.CampaignResultModal &&
              result.Eyebrow == standardResult.Speaker &&
              result.Heading == standardResult.Title &&
              result.Body.StartsWith(standardResult.Body, StringComparison.Ordinal) &&
              result.Body.Contains("첫 공급 성공", StringComparison.Ordinal) &&
              result.Body.Contains("미공급 0분", StringComparison.Ordinal) &&
              result.Body.Contains("남은 운영 자금 705만 5,000원", StringComparison.Ordinal) &&
              result.PrimaryAction.Id == RealtimeR2Ids.FirstLightReplayAction &&
              result.SecondaryAction?.Id == RealtimeR2Ids.FirstLightReturnAction,
            "release FIRST_LIGHT result omitted its payoff or terminal choices",
            failures);
        bool formativeRecord = slice.ClosePresentedPrimaryModalForSmoke();
        Check(formativeRecord &&
              slice.LatestPresentation.Modal?.Id == RealtimeR2Ids.ChapterBriefingModal &&
              slice.CoreSnapshot.Minute == slice.CoreSnapshot.ChapterStartMinute,
            "successful FIRST_LIGHT replay did not restart through the production " +
            "handler after authorizing its formative direct-play record",
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
            1260,
            RealtimeSimulationSpeed.VeryFast,
            failures);
        Check(slice.CoreSnapshot.ActiveEventStates.Single().EventId ==
                  RealtimeCampaignOverlayLoader.FirstReleaseEventId &&
              slice.LatestPresentation.Hud.Objective.Contains(
                  "1/2",
                  StringComparison.Ordinal) &&
              slice.LatestPresentation.ActionDock.PrimaryAction is null,
            "incomplete FIRST_LIGHT exposed a dominant result fast-forward action",
            failures);
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
              result.Body.Contains(
                  "적합한 변전소 없음 · 변전소 접속",
                  StringComparison.Ordinal) &&
              result.Body.Contains("진행 0/2", StringComparison.Ordinal) &&
              result.Body.Contains("다음 시도", StringComparison.Ordinal) &&
              result.Body.Contains("최종 운영 자금 850만 원", StringComparison.Ordinal) &&
              result.PrimaryAction.Id == RealtimeR2Ids.FirstLightReplayAction &&
              result.SecondaryAction?.Id == RealtimeR2Ids.FirstLightReturnAction &&
              (!string.Equals(result.Eyebrow, standardResult.Speaker,
                   StringComparison.Ordinal) ||
               !string.Equals(result.Heading, standardResult.Title,
                   StringComparison.Ordinal) ||
               !string.Equals(result.Body, standardResult.Body,
                   StringComparison.Ordinal)),
            "no-action FIRST_LIGHT completion counterfeited the positive authored result",
            failures);
        bool formativeRecord = slice.ClosePresentedPrimaryModalForSmoke();
        Check(!formativeRecord &&
              slice.LatestPresentation.Modal?.Id == RealtimeR2Ids.ChapterBriefingModal &&
              slice.CoreSnapshot.Minute == slice.CoreSnapshot.ChapterStartMinute,
            "no-action FIRST_LIGHT replay counterfeited the formative PASS or failed " +
            "to restart the chapter",
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
              slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused,
            "tutorial FIRST_LIGHT briefing did not close into explicit planning pause",
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
        Check(slice.InteractionState.Tool == RealtimeTool.Inspect &&
              slice.InteractionState.SelectedBuildToolId is null &&
              string.IsNullOrEmpty(slice.LatestPresentation.Pointer.Message),
            "SECOND_HEART activation retained FIRST_LIGHT planning transients",
            failures);
        Check(slice.ClosePresentedStoryModalForSmoke() is null &&
              slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused &&
              slice.InteractionState.RunningSpeed == RealtimeSimulationSpeed.VeryFast,
            "SECOND_HEART briefing did not preserve speed behind planning pause",
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
        Check(slice.ClosePresentedStoryModalForSmoke() is null &&
              slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused,
            "SECOND_SOURCE briefing did not close into planning pause",
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
                RealtimeModalPresentation failedResult =
                    failedSlice.LatestPresentation.Modal ??
                    throw new InvalidOperationException(
                        "The failed final story has no result modal.");
                Check(failedSlice.CoreSnapshot.CompletedChapters.Count == 8 &&
                      !failedSlice.CoreSnapshot.CompletedChapters[^1].ObjectiveSatisfied &&
                      failedResult.PrimaryAction.Label == "종료된 도시 보기" &&
                      failedResult.Body.Contains(
                          "성공한 최종 계획에서 세 도시 기록이 열립니다.",
                          StringComparison.Ordinal),
                    "the staged final result omitted its failed-terminal orientation",
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
}
#endif
