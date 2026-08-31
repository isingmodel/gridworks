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
                RealtimeR2Ids.LineTool(plan.LineClassId, plan.PoleClassId)));
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

    private async Task ValidateNorthBankPromiseLiveInput(
        RealtimeSliceMain slice,
        RealtimeSliceData data,
        string rootSubstationId,
        ICollection<string> failures)
    {
        Vector2 logical = RealtimeUiMetrics.ReferenceResolution;
        (SubViewport viewport, RealtimeUiRoot ui) = await CreateOffscreenUi(
            new Vector2I(1920, 1080),
            logical,
            uiScalePercent: 100,
            slice.LatestPresentation);
        RealtimeEventRail rail = ui.EventRailForSmoke;
        slice.AttachTimelineUiForSmoke(ui);
        slice.AttachActionUiForSmoke(ui);
        void PresentTimelineSelection(IReadOnlyList<string> _) =>
            Present(ui, slice.LatestPresentation);
        void PresentAction(string _) => Present(ui, slice.LatestPresentation);
        rail.ItemsRequested += PresentTimelineSelection;
        ui.ActionRequested += PresentAction;
        try
        {
            long planningMinute = slice.CoreSnapshot.Minute;
            int beforeSelectionCommands = slice.CoreSnapshot.CommandCount;
            string beforeSelectionHash = slice.CanonicalStateSha256;
            Require(planningMinute == 265260 &&
                    NorthDutyFlags(slice).Any(required => required),
                "North Bank live UI fixture did not begin at the authored planning minute " +
                "with the Unset/Keep forecast assumption",
                failures);

            rail.GrabMarkerFocusOnlyForSmoke(NorthBankPromiseDeadlineMarkerId);
            await SettleLayout();
            PushViewportKey(viewport, Key.Enter, pressed: true);
            PushViewportKey(viewport, Key.Enter, pressed: false);
            await SettleLayout();
            Require(slice.CanonicalStateSha256 == beforeSelectionHash &&
                    slice.CoreSnapshot.Minute == planningMinute &&
                    slice.CoreSnapshot.CommandCount == beforeSelectionCommands &&
                    string.Equals(
                        slice.InteractionState.TimelineSelectedItemId,
                        NorthBankPromiseDeadlineMarkerId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        rail.FocusedItemIdForSmoke,
                        NorthBankPromiseDeadlineMarkerId,
                        StringComparison.Ordinal) &&
                    slice.LatestPresentation.Context is
                    {
                        SubjectId: NorthBankPromiseDeadlineMarkerId,
                        Visible: true,
                    } &&
                    ui.ContextDockForSmoke.AccessibilitySummaryForSmoke.Contains(
                        "미선택",
                        StringComparison.Ordinal) &&
                    ui.ContextDockForSmoke.AccessibilitySummaryForSmoke.Contains(
                        "Keep 가정",
                        StringComparison.Ordinal),
                "deadline Enter selection mutated Core, moved time, lost marker focus, " +
                "or failed to open the authored ContextDock",
                failures);

            Button defer = ui.ContextDockForSmoke.GetNode<Button>(
                "Margin/Column/Footer/SecondaryButton");
            int beforeDeferCommands = slice.CoreSnapshot.CommandCount;
            PushViewportPrimary(viewport, defer.GetGlobalRect().GetCenter());
            await SettleLayout();
            bool[] deferredNorthDuty = NorthDutyFlags(slice);
            Require(slice.CoreSnapshot.PromiseDecision ==
                        CommercialPromiseDecision.Defer &&
                    slice.CoreSnapshot.Minute == planningMinute &&
                    slice.CoreSnapshot.CommandCount == beforeDeferCommands + 1 &&
                    deferredNorthDuty.Length > 0 &&
                    deferredNorthDuty.All(required => !required) &&
                    slice.LatestPresentation.Context.Sections.Any(item =>
                        item.Body.Contains("북안 생활권 수요", StringComparison.Ordinal) &&
                        item.Body.Contains("제외", StringComparison.Ordinal)),
                "actual Defer pointer click did not send exactly one command, preserve " +
                "the live minute, and remove North demand from the forecast duty",
                failures);

            Button keep = ui.ContextDockForSmoke.GetNode<Button>(
                "Margin/Column/Footer/PrimaryButton");
            keep.GrabFocus();
            await SettleLayout();
            int beforeKeepCommands = slice.CoreSnapshot.CommandCount;
            PushViewportKey(viewport, Key.Enter, pressed: true);
            PushViewportKey(viewport, Key.Enter, pressed: false);
            await SettleLayout();
            bool[] keptNorthDuty = NorthDutyFlags(slice);
            Require(slice.CoreSnapshot.PromiseDecision ==
                        CommercialPromiseDecision.Keep &&
                    slice.CoreSnapshot.Minute == planningMinute &&
                    slice.CoreSnapshot.CommandCount == beforeKeepCommands + 1 &&
                    keptNorthDuty.Length > 0 &&
                    keptNorthDuty.Any(required => required) &&
                    slice.LatestPresentation.Context.Sections.Any(item =>
                        item.Body.Contains("Keep", StringComparison.Ordinal)),
                "actual Keep keyboard activation did not send exactly one command, " +
                "preserve the live minute, and restore North demand to the forecast duty",
                failures);

            _ = RealtimeR2Smoke.BuildNorthBankService(
                slice,
                rootSubstationId,
                includeNorth: true,
                failures,
                "live-ui-keep");
            (RealtimeChapterOutcome outcome, RealtimeModalPresentation result) =
                RealtimeR2Smoke.CompleteNorthBankChapter(slice, data, failures);
            CommercialStoryCard kept = data.BaseCampaign.Chapters[3].ResultCards.Kept!;
            Present(ui, slice.LatestPresentation);
            ui.ApplyLayoutForSmoke(
                new Vector2I(1920, 1080),
                logical,
                uiScalePercent: 100);
            await SettleLayout();
            Require(slice.CoreSnapshot.Minute == 266070 &&
                    outcome.ObjectiveSatisfied &&
                    outcome.PromiseDecision == CommercialPromiseDecision.Keep &&
                    outcome.Events.All(item =>
                        item.SafetySatisfied && item.PromiseSatisfied) &&
                    result.Eyebrow == kept.Speaker &&
                    result.Heading == kept.Title &&
                    result.Body == kept.Body &&
                    ui.ModalHostForSmoke.Depth == 1 &&
                    ui.ModalHostForSmoke.OwnsFocusForSmoke &&
                    ui.ModalHostForSmoke.AccessibilitySummaryForSmoke.Contains(
                        kept.Title,
                        StringComparison.Ordinal) &&
                    ui.ModalHostForSmoke.AccessibilitySummaryForSmoke.Contains(
                        kept.Body,
                        StringComparison.Ordinal) &&
                    ui.ModalHostForSmoke.FocusLinksForSmoke().All(link =>
                        link.NextInsideModal && link.PreviousInsideModal),
                "North Bank Keep input flow did not end at the exact authored result " +
                "with safety/promise truth and modal focus/AX preserved",
                failures);
            GD.Print(
                "REALTIME_R2_NORTH_BANK_UI_PASS one-line-deadline-hover-AX; " +
                "Enter-select; pointer-Defer; keyboard-Keep; exact-kept-result");
        }
        finally
        {
            rail.ItemsRequested -= PresentTimelineSelection;
            ui.ActionRequested -= PresentAction;
            slice.DetachTimelineUiForSmoke(ui);
            slice.DetachActionUiForSmoke(ui);
            RemoveAndFree(viewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static bool[] NorthDutyFlags(RealtimeSliceMain slice) =>
        slice.LatestPresentation.BaseForecast.Events
            .SelectMany(item => item.TemporalProjection.Outcome.DutySegments)
            .SelectMany(segment => segment.Loads)
            .Where(load => string.Equals(
                load.LoadId,
                NorthResidentialLoadId,
                StringComparison.Ordinal))
            .Select(load => load.Required)
            .ToArray();

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
        Require(Math.Abs(
                    snapshot.ExpectedLayout.MapInteraction.End.X -
                    (logical.X - snapshot.Profile.SafeMargin)) <= 0.5f,
            $"{label} left a dead full-height strip under the compact context overlay",
            failures);
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
        RealtimeEventRail eventRail,
        RealtimeUiSmokeLayoutSnapshot snapshot,
        IReadOnlyList<RealtimeTimelineItemPresentation> expectedLinearItems,
        int scale,
        string label,
        ICollection<string> failures)
    {
        string[] expectedLinearIds = expectedLinearItems.Select(item => item.Id).ToArray();
        Require(snapshot.VisibleTimelineLanes == 1 &&
                snapshot.TimelineLaneLabels == 0,
            $"{label} timeline did not render exactly one unlabeled chronological track",
            failures);
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
            RealtimeTimelineSeverity displaySeverity = markerItems.Max(item => item.Severity);
            string stateGlyph = markerItems.Any(item => item.IsCurrent)
                ? "▶"
                : markerItems.All(item =>
                    item.Visibility == RealtimeTimelineVisibility.Completed)
                    ? "✓"
                    : "○";
            bool hasActual = markerItems.Any(item =>
                item.SourceKind == RealtimeTimelineSourceKind.Actual);
            bool hasDraft = markerItems.Any(item =>
                item.SourceKind == RealtimeTimelineSourceKind.Draft);
            string sourceGlyph = hasActual && hasDraft
                ? "■◇"
                : hasDraft
                    ? "◇"
                    : hasActual
                        ? "■"
                        : string.Empty;
            string expectedPrefix = stateGlyph +
                                    SeverityGlyphForSmoke(displaySeverity) +
                                    sourceGlyph;
            Require(marker.VisibleText.StartsWith(expectedPrefix,
                        StringComparison.Ordinal) &&
                    markerItems.Any(item => marker.VisibleText.Contains(
                        item.KindIcon, StringComparison.Ordinal)) &&
                    !marker.VisibleText.Contains(' ') &&
                    !marker.VisibleText.Contains('·') &&
                    (markerItems.Length == 1 || marker.VisibleText.Contains(
                        $"+{markerItems.Length - 1}", StringComparison.Ordinal)),
                $"{label} marker did not preserve compact state/severity/source/kind/count " +
                $"cues ({marker.VisibleText}, expectedPrefix={expectedPrefix})",
                failures);
            RealtimeTimelineTooltipOverlayFact detail =
                eventRail.TooltipOverlayFactForSmoke(marker.ItemIds[0]);
            float expectedDetailWidth = Math.Clamp(
                420f * snapshot.Profile.AccessibilityScale,
                320f,
                620f);
            int expectedDetailFontSize = Math.Max(
                14,
                Mathf.RoundToInt(15f * snapshot.Profile.AccessibilityScale));
            Require(detail.CustomOverlay &&
                    Mathf.IsEqualApprox(detail.MinimumWidth, expectedDetailWidth) &&
                    detail.FontSize == expectedDetailFontSize &&
                    detail.MouseFilter == Control.MouseFilterEnum.Ignore,
                $"{label} marker hover detail did not use the scaled custom, " +
                $"mouse-transparent panel (custom={detail.CustomOverlay}, " +
                $"width={detail.MinimumWidth}/{expectedDetailWidth}, " +
                $"font={detail.FontSize}/{expectedDetailFontSize}, " +
                $"mouse={detail.MouseFilter})",
                failures);
            RealtimeTimelineItemPresentation[] detailedItems = markerItems.Length <= 6
                ? markerItems
                : markerItems.Where(item => detail.Text.Contains(
                    item.Title,
                    StringComparison.Ordinal)).Take(6).ToArray();
            Require(detailedItems.Length == Math.Min(6, markerItems.Length) &&
                    (markerItems.Length <= 6 || detail.Text.Contains(
                        $"외 {markerItems.Length - 6}건 · 시간순 사건 목록에서 전체 보기",
                        StringComparison.Ordinal)),
                $"{label} marker hover detail did not bound a dense cluster to six " +
                "items with an exact full-list fallback",
                failures);
            foreach (RealtimeTimelineItemPresentation item in detailedItems)
            {
                string stateLabel = item.IsCurrent
                    ? "진행 중"
                    : item.Visibility == RealtimeTimelineVisibility.Completed
                        ? "완료"
                        : "예정";
                Require(detail.Text.Contains(stateLabel, StringComparison.Ordinal) &&
                        detail.Text.Contains(item.SourceLabel, StringComparison.Ordinal) &&
                        detail.Text.Contains(item.KindLabel, StringComparison.Ordinal) &&
                        detail.Text.Contains(item.SeverityLabel, StringComparison.Ordinal) &&
                        detail.Text.Contains(item.TimingLabel, StringComparison.Ordinal) &&
                        detail.Text.Contains(item.Title, StringComparison.Ordinal) &&
                        detail.Text.Contains(item.Description, StringComparison.Ordinal),
                    $"{label} marker hover detail lost full state/source/kind/severity/" +
                    $"timing/title/description for {item.Id}",
                    failures);
            }
        }
        Require(snapshot.Markers.Select(marker => marker.Rect.GetCenter().Y)
                .DistinctBy(y => Mathf.RoundToInt(y * 2f)).Count() == 1,
            $"{label} markers did not share one chronological track centerline",
            failures);
        for (int leftIndex = 0; leftIndex < snapshot.Markers.Count; leftIndex++)
        for (int rightIndex = leftIndex + 1;
             rightIndex < snapshot.Markers.Count;
             rightIndex++)
        {
            RealtimeUiSmokeMarkerFact left = snapshot.Markers[leftIndex];
            RealtimeUiSmokeMarkerFact right = snapshot.Markers[rightIndex];
            float horizontalOverlap = Math.Min(left.Rect.End.X, right.Rect.End.X) -
                Math.Max(left.Rect.Position.X, right.Rect.Position.X);
            Require(horizontalOverlap <= 0.5f,
                $"{label} single-track marker rectangles intersect by " +
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
                $"{label} timeline marker left the event rail", failures);
            Require(marker.DisplayLane == 0,
                $"{label} marker mapped outside the sole chronological track", failures);
            if (index > 0)
            {
                RealtimeUiSmokeMarkerFact prior = chronologicalGroups[index - 1];
                Require(prior.Rect.Position.X <= marker.Rect.Position.X + 0.5f &&
                        prior.Rect.End.X <= marker.Rect.Position.X + 0.5f,
                    $"{label} single-track marker geometry did not follow chronological " +
                    $"non-overlapping order", failures);
            }
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
                        marker.AccessibilityName.Contains(item.SourceLabel,
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
        $"{SeverityGlyphForSmoke(item.Severity)} {item.TimingLabel} · " +
        $"{item.SourceLabel} {item.KindLabel} · {item.ShortLabel}";

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
                RealtimeTimelinePolicy.RequiredForecastHorizonMinutes(
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
        RealtimeTimelineTarget target = RealtimeTimelineTargetResolver.Resolve(
            slice.DisplayWorldForSmoke,
            slice.CoreSnapshot,
            presentation.BaseForecast,
            presentation.ComparisonDraftForecast,
            presentation.TransitionHistory,
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
                RealtimeR2Ids.ComparisonThermalMarkerPrefix,
                StringComparison.Ordinal);
            bool actual = expectedMarkerId.StartsWith(
                RealtimeR2Ids.ActualThermalMarkerPrefix,
                StringComparison.Ordinal);
            bool commonThermalContext = presentation.Context.Sections.Any(item =>
                    item.Heading == (actual ? "기록 시각" : "예상 시각") &&
                    string.Equals(item.Body, marker.TimeLabel,
                        StringComparison.Ordinal)) &&
                presentation.Context.Sections.Any(item =>
                    item.Heading == (actual ? "실제 변화" : "예상 변화"));
            Require(commonThermalContext &&
                    (actual
                        ? string.Equals(
                              presentation.Context.Eyebrow,
                              "실제 열 보호 기록",
                              StringComparison.Ordinal) &&
                          presentation.Context.Sections.Any(item =>
                              item.Heading == "운영 구간")
                        : comparison
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
                actual
                    ? $"actual thermal marker {expectedMarkerId} context lost " +
                      "record/time/change/evidence causality"
                    : comparison
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
                RealtimeR2Ids.ComparisonEventMarkerPrefix,
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
}
#endif
