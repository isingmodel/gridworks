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
        string expectedCash =
            $"운영 자금 {RealtimePresentationText.Cash(slice.CoreSnapshot.CashUnit)}";
        Check(!string.IsNullOrWhiteSpace(presentation.Hud.Objective) &&
              string.Equals(presentation.Hud.Cash, expectedCash,
                  StringComparison.Ordinal),
            "HUD lost its authored objective or player cash unit", failures);
        Check(presentation.BuildShelf.Tools
                .Where(item => item.Id.StartsWith(RealtimeR2Ids.NodeToolPrefix, StringComparison.Ordinal))
                .All(item => item.Description.Contains("원", StringComparison.Ordinal)),
            "node tool cost copy does not use the player-facing won unit", failures);

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
                      presentation.Context.Sections.Count is >= 1 and <= 4 &&
                      presentation.Context.Sections.Single(item =>
                          item.Heading == "보호").Body == expectedSummary &&
                      presentation.Context.Details.Single(item =>
                          item.Tab == RealtimeContextDetailTab.Thermal).Body == expectedDetail &&
                      worldStatus.State == ExpectedWorldState(state),
                    $"{kindLabel}/{state} lost bounded summary, exact allowance/cooldown " +
                    "copy, or world state",
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
}
#endif
