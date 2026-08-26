namespace Gridworks.Game.Realtime.UI;

/// <summary>
/// Exhaustive values supported by the current R2 input and interaction contracts.
/// Adding an enum member must extend this capability before it can reach a reducer.
/// </summary>
internal static class RealtimeUiCapabilities
{
    internal static bool Supports(RealtimeTool value) => value is
        RealtimeTool.Inspect or
        RealtimeTool.BuildNode or
        RealtimeTool.BuildLine or
        RealtimeTool.MoveDraft or
        RealtimeTool.Analysis;

    internal static bool Supports(RealtimeSurface value) => value is
        RealtimeSurface.World or
        RealtimeSurface.Inspector or
        RealtimeSurface.Timeline or
        RealtimeSurface.Drawer or
        RealtimeSurface.BlockingModal;

    internal static bool Supports(RealtimePauseReason value) => value is
        RealtimePauseReason.None or
        RealtimePauseReason.PlayerRequest or
        RealtimePauseReason.ChapterBriefing or
        RealtimePauseReason.CriticalIncident or
        RealtimePauseReason.RecoveryConfirmation or
        RealtimePauseReason.CampaignResult or
        RealtimePauseReason.CatchUpCeiling or
        RealtimePauseReason.FatalError;

    internal static bool Supports(RealtimeTimelineHorizonPreset value) => value is
        RealtimeTimelineHorizonPreset.SixHours or
        RealtimeTimelineHorizonPreset.TwentyFourHours or
        RealtimeTimelineHorizonPreset.SevenDays;

    internal static bool Supports(RealtimeTimelineNavigation value) => value is
        RealtimeTimelineNavigation.Home or
        RealtimeTimelineNavigation.PreviousEvent or
        RealtimeTimelineNavigation.NextEvent;

    internal static bool Supports(RealtimeModalKind value) => value is
        RealtimeModalKind.Story or
        RealtimeModalKind.NewGameConfirmation or
        RealtimeModalKind.RecoveryConfirmation or
        RealtimeModalKind.FatalError;

    internal static bool Supports(RealtimeInputPriority value) => value is
        RealtimeInputPriority.EmptyTerrain or
        RealtimeInputPriority.WorldCandidate or
        RealtimeInputPriority.SelectionAction or
        RealtimeInputPriority.DraftHandle or
        RealtimeInputPriority.PanCapture or
        RealtimeInputPriority.Hud or
        RealtimeInputPriority.BlockingModal or
        RealtimeInputPriority.Fatal;

    internal static bool Supports(RealtimeInputCommand value) => value is
        RealtimeInputCommand.TogglePause or
        RealtimeInputCommand.SetNormalSpeed or
        RealtimeInputCommand.SetFastSpeed or
        RealtimeInputCommand.SetVeryFastSpeed or
        RealtimeInputCommand.ToggleAnalysis or
        RealtimeInputCommand.ToggleBuildShelf or
        RealtimeInputCommand.CancelOrBack or
        RealtimeInputCommand.ConfirmOrSelect or
        RealtimeInputCommand.UndoDraftStep or
        RealtimeInputCommand.CycleCandidatePrevious or
        RealtimeInputCommand.CycleCandidateNext or
        RealtimeInputCommand.BeginPan or
        RealtimeInputCommand.EndPan or
        RealtimeInputCommand.TimelineHome or
        RealtimeInputCommand.TimelinePrevious or
        RealtimeInputCommand.TimelineNext or
        RealtimeInputCommand.SelectInspectTool or
        RealtimeInputCommand.SelectFirstNodeTool or
        RealtimeInputCommand.SelectFirstLineTool;
}
