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
              result.Body.Contains(
                  "04:00 평가 시점 접속 조건",
                  StringComparison.Ordinal) &&
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
            slice.InteractionState.RunningSpeed,
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
            slice.InteractionState.RunningSpeed,
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
        bool bodyMatches = modal is not null &&
            purpose == RealtimeChapterStoryModalPurpose.ChapterResult &&
            string.Equals(chapterId, "FIRST_LIGHT", StringComparison.Ordinal)
                ? modal.Body.StartsWith(card.Body, StringComparison.Ordinal) &&
                  modal.Body.Contains("첫 공급 성공", StringComparison.Ordinal)
                : string.Equals(modal?.Body, card.Body, StringComparison.Ordinal);
        Check(modal is not null && request.Purpose == purpose &&
              request.ChapterId == chapterId && request.EventId == eventId &&
              modal.Eyebrow == card.Speaker && modal.Heading == card.Title &&
              bodyMatches,
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

        RealtimePointerResolution lineEndpoint = RealtimePointerOwnerResolver.Resolve(
            new RealtimePointerProbe(
                "LINE_ENDPOINT_OVERLAP",
                new CoreMapPoint(0, 0),
                [
                    new RealtimeMapCandidate(
                        "OVERLAPPING_EDGE",
                        RealtimeMapCandidateKind.Edge,
                        RealtimePointerOwner.WorldCandidate,
                        0),
                    new RealtimeMapCandidate(
                        "COMPATIBLE_SUBSTATION",
                        RealtimeMapCandidateKind.Node,
                        RealtimePointerOwner.WorldCandidate,
                        100),
                ]),
            ["COMPATIBLE_SUBSTATION"]);
        Check(lineEndpoint.ResolvedId == "COMPATIBLE_SUBSTATION" &&
              lineEndpoint.OrderedWorldCandidateIds.SequenceEqual(
                  new[] { "COMPATIBLE_SUBSTATION" },
                  StringComparer.Ordinal),
            "BuildLine overlap did not isolate the compatible node from the edge",
            failures);
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
        if (speed == RealtimeSimulationSpeed.Paused)
        {
            throw new ArgumentOutOfRangeException(
                nameof(speed),
                speed,
                "Frame advancement requires an explicit running speed.");
        }
        if (slice.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused)
        {
            RequireIntent(
                slice.ApplyIntentForSmoke(RealtimeR2Intent.SetSpeed(speed)),
                "resume explicit planning pause",
                failures,
                coreCommandExpected: false);
        }
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

    private static RealtimeCampaignSave? RunCase(
        SmokeCase smokeCase,
        ICollection<string> failures)
    {
        try
        {
            return smokeCase.Run(failures);
        }
        catch (Exception exception)
        {
            failures.Add($"R2 smoke {smokeCase.Name} threw " +
                         $"{exception.GetType().Name}: " +
                         exception.Message + Environment.NewLine + exception.StackTrace);
            return null;
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
