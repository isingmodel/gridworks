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

internal sealed record RealtimeR2LayoutPresentationSet(
    RealtimeSlicePresentation World,
    RealtimeSlicePresentation BuildShelf,
    RealtimeSlicePresentation Inspector,
    RealtimeSlicePresentation Action,
    RealtimeSlicePresentation Timeline,
    RealtimeSlicePresentation Modal);

internal sealed record RealtimeR2SmokeResult(
    int ExecutedCaseCount,
    RealtimeCampaignSave? CompletedProductSave);

/// <summary>
/// Deterministic R2 integration evidence. Every assertion enters through the
/// real slice host, interaction reducer, frame adapter, Core run, and presenter.
/// It deliberately owns no fixture coordinates, authored minutes, or outcomes.
/// </summary>
internal static partial class RealtimeR2Smoke
{
    internal const string CompletedProductRouteCaseName =
        "release-through-longest-night-controller";

    private sealed record SmokeCase(
        string Name,
        Func<ICollection<string>, RealtimeCampaignSave?> Run);

    private static readonly SmokeCase[] Cases =
    [
        DefineCase("visual-layout-data", ValidateVisualLayoutData),
        DefineCase("stable-r2-id-protocol", ValidateStableR2IdProtocol),
        DefineCase("live-audio-cue-selection", ValidateLiveAudioCueSelection),
        DefineCase("fail-closed-routing", ValidateFailClosedRouting),
        DefineCase("clock-pause", ValidateClockAndPause),
        DefineCase("frame-rate-matrix", ValidateFrameRateMatrix),
        DefineCase("callback-partition-matrix", ValidateCallbackPartitionMatrix),
        DefineCase("typed-order-quote", ValidateTypedOrderQuote),
        DefineCase("line-command-completion", ValidateLineConstruction),
        DefineCase("speed-authored-outcome", ValidateSpeedEquivalence),
        DefineCase("critical-pause-boundary", ValidateCriticalPauseBoundary),
        DefineCase("presentation-authority", ValidatePresentationAuthority),
        DefineCase("follow-now-timeline", ValidateFollowNowTimeline),
        DefineCase("player-facing-copy", ValidatePlayerFacingCopy),
        DefineCase(
            "completed-history-thermal-context",
            ValidateCompletedHistoryAndThermalContext),
        DefineCase("thermal-protection-copy", ValidateThermalProtectionCopy),
        DefineCase("draft-menu-cancel", ValidateDraftMenuCancelPolicy),
        DefineCase("ended-read-only-shell", ValidateEndedReadOnlyShell),
        DefineCase("analysis-surface-policy", ValidateAnalysisSurfacePolicy),
        DefineCase("comparison-draft-forecast", ValidateComparisonDraftForecast),
        DefineCase(
            "future-event-actual-draft-construction",
            ValidateFutureEventActualDraftConstruction),
        DefineCase(
            "release-first-light-controller-story",
            ValidateReleaseFirstLightControllerStory),
        DefineCase(
            "release-first-light-late-construction-boundary",
            ValidateReleaseFirstLightLateConstructionBoundary),
        DefineCase(
            "release-first-light-no-action-result",
            ValidateReleaseFirstLightNoActionResult),
        DefineCase(
            "release-tutorial-through-second-source",
            ValidateReleaseTutorialThroughSecondSource),
        DefineCase("cumulative-event-duty-resume", ValidateCumulativeEventDutyResume),
        new SmokeCase(
            CompletedProductRouteCaseName,
            ValidateReleaseThroughLongestNightController),
        DefineCase(
            "release-promise-result-branches",
            ValidateReleasePromiseResultBranches),
        DefineCase(
            "release-tutorial-connection-failure-result",
            ValidateReleaseTutorialConnectionFailureResult),
        DefineCase("modal-restore", ValidateModalRestore),
        DefineCase("pointer-priority", ValidatePointerPriority),
    ];

    private static readonly IReadOnlyList<string> KnownCaseNames =
        Array.AsReadOnly(Cases.Select(item => item.Name).ToArray());

    internal static IReadOnlyList<string> CaseNames => KnownCaseNames;

    internal static RealtimeR2SmokeResult Validate(
        ICollection<string> failures,
        string? exactCaseName = null)
    {
        ArgumentNullException.ThrowIfNull(failures);
        IEnumerable<SmokeCase> selectedCases = Cases;
        if (exactCaseName is not null)
        {
            SmokeCase? selected = Cases.SingleOrDefault(item =>
                string.Equals(item.Name, exactCaseName, StringComparison.Ordinal));
            if (selected is null)
            {
                throw new ArgumentException(
                    $"Unknown R2 smoke case '{exactCaseName}'; known cases: " +
                    $"{string.Join(", ", KnownCaseNames)}.",
                    nameof(exactCaseName));
            }
            selectedCases = new[] { selected };
        }

        int executedCaseCount = 0;
        RealtimeCampaignSave? completedProductSave = null;
        foreach (SmokeCase smokeCase in selectedCases)
        {
            int failureCountBeforeCase = failures.Count;
            RealtimeCampaignSave? candidate = RunCase(smokeCase, failures);
            executedCaseCount++;
            if (string.Equals(
                    smokeCase.Name,
                    CompletedProductRouteCaseName,
                    StringComparison.Ordinal) &&
                candidate is not null &&
                failures.Count == failureCountBeforeCase)
            {
                completedProductSave = candidate;
            }
        }
        return new RealtimeR2SmokeResult(
            executedCaseCount,
            completedProductSave);
    }

    private static SmokeCase DefineCase(
        string name,
        Action<ICollection<string>> validate) =>
        new(name, failures =>
        {
            validate(failures);
            return null;
        });
}
#endif
