using Gridworks.Core.Release.V3;

namespace Gridworks.Game.Realtime.R2;

/// <summary>
/// Stable semantic IDs shared by the R2 application session, presentation, and UI adapters.
/// Keeping their construction here prevents each layer from inventing its own string protocol.
/// </summary>
internal static class RealtimeR2Ids
{
    internal const string OrderNodeAction = "ORDER_NODE";
    internal const string OrderLineAction = "ORDER_LINE";
    internal const string PromiseKeepAction = "PROMISE_KEEP";
    internal const string PromiseDeferAction = "PROMISE_DEFER";

    internal const string NoticeCloseAction = "NOTICE_CLOSE";
    internal const string BriefingContinueAction = "BRIEFING_CONTINUE";
    internal const string EventStoryContinueAction = "EVENT_STORY_CONTINUE";
    internal const string DecisionWindowContinueAction = "DECISION_WINDOW_CONTINUE";
    internal const string ResultCloseAction = "RESULT_CLOSE";

    internal const string ChapterBriefingModal = "CHAPTER_BRIEFING";
    internal const string CampaignResultModal = "CAMPAIGN_RESULT";
    internal const string TutorialResultModalPrefix = "TUTORIAL_RESULT:";
    internal const string TutorialBriefingModalPrefix = "TUTORIAL_BRIEFING:";
    internal const string TutorialDecisionWindowModalPrefix =
        "TUTORIAL_DECISION_WINDOW:";
    internal const string TutorialEventStoryModalPrefix = "TUTORIAL_EVENT_STORY:";

    internal const string InspectTool = "TOOL:INSPECT";
    internal const string AnalysisTool = "TOOL:ANALYSIS";
    internal const string NodeToolPrefix = "NODE:";
    internal const string LineToolPrefix = "LINE:";

    internal const string ActiveConstructionMarker = "ACTIVE_CONSTRUCTION";
    internal const string DraftConstructionMarker = "DRAFT_CONSTRUCTION";
    internal const string CompletedConstructionMarkerPrefix =
        "COMPLETED_CONSTRUCTION:";
    internal const string PromiseDecisionMarkerPrefix = "PROMISE_DEADLINE:";
    internal const string ThermalMarkerPrefix = "THERMAL:";
    internal const string ComparisonEventMarkerPrefix = "DRAFT_FORECAST:";
    internal const string ComparisonThermalMarkerPrefix = "DRAFT_THERMAL:";

    internal static string TutorialResultModal(string chapterId) =>
        $"{TutorialResultModalPrefix}{chapterId}";

    internal static string TutorialBriefingModal(string chapterId) =>
        $"{TutorialBriefingModalPrefix}{chapterId}";

    internal static string TutorialDecisionWindowModal(
        string chapterId,
        string windowId) =>
        $"{TutorialDecisionWindowModalPrefix}{chapterId}:{windowId}";

    internal static string TutorialEventStoryModal(string chapterId, string eventId) =>
        $"{TutorialEventStoryModalPrefix}{chapterId}:{eventId}";

    internal static string NodeTool(string classId) => $"{NodeToolPrefix}{classId}";

    internal static string LineTool(string lineClassId, string poleClassId) =>
        $"{LineToolPrefix}{lineClassId}:{poleClassId}";

    internal static string PromiseDecisionMarker(string promiseId) =>
        $"{PromiseDecisionMarkerPrefix}{promiseId}";

    internal static string ThermalMarker(
        string eventId,
        RealtimeThermalTransition transition) =>
        $"{ThermalMarkerPrefix}{eventId}:{transition.Minute}:" +
        $"{transition.Kind}:{transition.AssetId}";

    internal static string ComparisonEventMarker(string eventId) =>
        $"{ComparisonEventMarkerPrefix}{eventId}";

    internal static string ComparisonThermalMarker(
        string eventId,
        RealtimeThermalTransition transition) =>
        $"{ComparisonThermalMarkerPrefix}{eventId}:{transition.Minute}:" +
        $"{transition.Kind}:{transition.AssetId}";

    internal static string CompletedConstructionMarker(
        RealtimeConstructionCompletion completion) =>
        $"{CompletedConstructionMarkerPrefix}{completion.CompletionMinute}:" +
        $"{completion.Kind}:{string.Join('+', completion.NodeIds)}:" +
        string.Join('+', completion.EdgeIds);

    internal static bool IsSupportedModalCloseAction(string actionId) =>
        actionId is NoticeCloseAction or
            BriefingContinueAction or
            EventStoryContinueAction or
            DecisionWindowContinueAction or
            ResultCloseAction;

    internal static bool IsComparisonMarker(string? id) => id is not null &&
        (id.StartsWith(ComparisonEventMarkerPrefix, System.StringComparison.Ordinal) ||
         id.StartsWith(ComparisonThermalMarkerPrefix, System.StringComparison.Ordinal));
}
