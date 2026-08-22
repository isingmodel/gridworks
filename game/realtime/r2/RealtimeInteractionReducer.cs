using System;
using Gridworks.Core.Release.V2;
using Gridworks.Game.Realtime.UI;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

internal enum RealtimeR2IntentKind
{
    SetSpeed,
    SetPlayerPaused,
    SelectTool,
    OpenSurface,
    CloseSurface,
    SelectId,
    ClearSelection,
    OpenModal,
    CloseModal,
    AcknowledgeAutoPause,
    ToggleAnalysis,
    SetTimelineView,
    SetNodeDraft,
    CancelNodeDraft,
    OrderNode,
    StartLineDraft,
    AddLinePoint,
    MoveLinePoint,
    UndoLinePoint,
    FinishLineDraft,
    CancelLineDraft,
    OrderLine,
    SetPromiseDecision,
}

internal sealed record RealtimeR2Intent(
    RealtimeR2IntentKind Kind,
    RealtimeSimulationSpeed? Speed = null,
    bool? Paused = null,
    RealtimeTool? Tool = null,
    RealtimeSurface? Surface = null,
    string? FirstId = null,
    string? SecondId = null,
    string? ThirdId = null,
    CoreMapPoint? Position = null,
    int? PointIndex = null,
    CommercialPromiseDecision? PromiseDecision = null,
    RealtimeModalKind? ModalKind = null,
    RealtimePauseReason? PauseReason = null,
    string? ReturnFocusId = null,
    RealtimeTimelineHorizonPreset? TimelineHorizon = null,
    long? TimelineAnchorMinute = null)
{
    internal static RealtimeR2Intent SetSpeed(RealtimeSimulationSpeed speed) =>
        new(RealtimeR2IntentKind.SetSpeed, Speed: speed);

    internal static RealtimeR2Intent SetPlayerPaused(bool paused) =>
        new(RealtimeR2IntentKind.SetPlayerPaused, Paused: paused);

    internal static RealtimeR2Intent SelectTool(RealtimeTool tool) =>
        new(
            RealtimeR2IntentKind.SelectTool,
            Tool: tool,
            Surface: RealtimeSurface.World);

    internal static RealtimeR2Intent SelectBuildTool(
        RealtimeTool tool,
        string buildToolId) => new(
        RealtimeR2IntentKind.SelectTool,
        Tool: tool,
        Surface: RealtimeSurface.Drawer,
        FirstId: buildToolId);

    internal static RealtimeR2Intent RestoreInspectTool() => new(
        RealtimeR2IntentKind.SelectTool,
        Tool: RealtimeTool.Inspect);

    internal static RealtimeR2Intent Select(string? id) => id is null
        ? new(RealtimeR2IntentKind.ClearSelection)
        : new(RealtimeR2IntentKind.SelectId, FirstId: id);

    internal static RealtimeR2Intent OpenModal(
        string modalId,
        RealtimeModalKind kind,
        RealtimePauseReason pauseReason,
        string? returnFocusId = null) => new(
        RealtimeR2IntentKind.OpenModal,
        FirstId: modalId,
        ModalKind: kind,
        PauseReason: pauseReason,
        ReturnFocusId: returnFocusId);

    internal static RealtimeR2Intent CloseModal(string modalId) =>
        new(RealtimeR2IntentKind.CloseModal, FirstId: modalId);

    internal static RealtimeR2Intent AcknowledgeAutoPause() =>
        new(RealtimeR2IntentKind.AcknowledgeAutoPause);

    internal static RealtimeR2Intent SetTimelineView(
        string? selectedId,
        long? anchorMinute,
        RealtimeTimelineHorizonPreset horizon) => new(
        RealtimeR2IntentKind.SetTimelineView,
        FirstId: selectedId,
        TimelineHorizon: horizon,
        TimelineAnchorMinute: anchorMinute);

    internal static RealtimeR2Intent SetTimelineMarker(
        string? markerId,
        string? subjectId,
        long? anchorMinute,
        RealtimeTimelineHorizonPreset horizon) => new(
        RealtimeR2IntentKind.SetTimelineView,
        FirstId: markerId,
        SecondId: subjectId,
        TimelineHorizon: horizon,
        TimelineAnchorMinute: anchorMinute);

    internal static RealtimeR2Intent StartLineDraft(
        string startNodeId,
        string lineClassId,
        string poleClassId) => new(
        RealtimeR2IntentKind.StartLineDraft,
        FirstId: startNodeId,
        SecondId: lineClassId,
        ThirdId: poleClassId);

    internal static RealtimeR2Intent AddLinePoint(CoreMapPoint position) =>
        new(RealtimeR2IntentKind.AddLinePoint, Position: position);

    internal static RealtimeR2Intent FinishLineDraft(string endNodeId) =>
        new(RealtimeR2IntentKind.FinishLineDraft, FirstId: endNodeId);

    internal static RealtimeR2Intent OrderLine() => new(RealtimeR2IntentKind.OrderLine);
}

internal sealed record RealtimeInteractionRestorePoint(
    RealtimeSimulationState Simulation,
    RealtimeSimulationSpeed RunningSpeed,
    RealtimeTool Tool,
    RealtimeSurface Surface,
    RealtimePauseReason PauseReason,
    string? ReturnFocusId);

internal sealed record RealtimeInteractionState(
    RealtimeSimulationState Simulation,
    RealtimeSimulationSpeed RunningSpeed,
    RealtimeTool Tool,
    RealtimeSurface Surface,
    string? SelectionId,
    RealtimePauseReason PauseReason,
    string? ActiveModalId,
    RealtimeModalKind? ActiveModalKind,
    string? ReturnFocusId,
    RealtimeInteractionRestorePoint? ModalRestore,
    RealtimeTimelineHorizonPreset TimelineHorizon,
    long? TimelineAnchorMinute,
    string? TimelineSelectedItemId)
{
    public string? SelectedBuildToolId { get; init; }

    internal RealtimeSimulationSpeed PresentedSpeed =>
        Simulation == RealtimeSimulationState.Running
            ? RunningSpeed
            : RealtimeSimulationSpeed.Paused;

    internal RealtimeInteractionPresentation ToPresentation(
        RealtimePausePresentation pause) => new(
        Simulation,
        PresentedSpeed,
        Tool,
        Surface,
        SelectionId,
        pause);
}

internal sealed record RealtimeInteractionReduction(
    bool Accepted,
    string? Error,
    RealtimeInteractionState State);

internal enum RealtimeDraftToolLockKind
{
    NodeDraft,
    OpenLineDraft,
    ClosedLineDraft,
}

/// <summary>
/// The Core construction draft is authoritative. While one exists, the shell
/// must keep showing the exact tool/class that owns it instead of allowing a
/// second, contradictory mode to replace the visible tool state.
/// </summary>
internal sealed record RealtimeDraftToolLock(
    RealtimeDraftToolLockKind Kind,
    RealtimeTool RequiredTool,
    string RequiredBuildToolId,
    string RejectionReason)
{
    internal bool Allows(RealtimeTool? tool, string? buildToolId) =>
        tool == RequiredTool && string.Equals(
            buildToolId,
            RequiredBuildToolId,
            StringComparison.Ordinal);
}

internal static class RealtimeInteractionReducer
{
    internal const string CampaignEndedReadOnlyReason =
        "운영이 완료되어 공사를 시작하거나 초안·운영 약속을 바꿀 수 없습니다.";

    internal static RealtimeInteractionState Initial(bool chapterBriefing = true) =>
        new(
            chapterBriefing
                ? RealtimeSimulationState.AutoPaused
                : RealtimeSimulationState.Running,
            RealtimeSimulationSpeed.Normal,
            RealtimeTool.Inspect,
            chapterBriefing
                ? RealtimeSurface.BlockingModal
                : RealtimeSurface.Drawer,
            null,
            chapterBriefing
                ? RealtimePauseReason.ChapterBriefing
                : RealtimePauseReason.None,
            chapterBriefing ? "CHAPTER_BRIEFING" : null,
            chapterBriefing ? RealtimeModalKind.ChapterStory : null,
            chapterBriefing ? "WORLD" : null,
            chapterBriefing
                ? new RealtimeInteractionRestorePoint(
                    RealtimeSimulationState.Running,
                    RealtimeSimulationSpeed.Normal,
                    RealtimeTool.Inspect,
                    RealtimeSurface.Drawer,
                    RealtimePauseReason.None,
                    "WORLD")
                : null,
            RealtimeTimelineHorizonPreset.TwentyFourHours,
            null,
            null);

    internal static RealtimeInteractionReduction Reduce(
        RealtimeInteractionState state,
        RealtimeR2Intent intent,
        ConstructionSnapshot? authoritativeConstruction = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(intent);
        if (state.Surface == RealtimeSurface.BlockingModal &&
            intent.Kind != RealtimeR2IntentKind.CloseModal)
        {
            return Rejected(state, "A blocking modal owns interaction.");
        }
        if (state.Simulation == RealtimeSimulationState.Ended &&
            IsEndedWriteIntent(intent))
        {
            return Rejected(state, CampaignEndedReadOnlyReason);
        }
        string? draftToolBlock = authoritativeConstruction is null ||
            state.Simulation == RealtimeSimulationState.Ended
            ? null
            : DraftToolChangeBlockReason(authoritativeConstruction, intent);
        if (draftToolBlock is not null)
        {
            return Rejected(state, draftToolBlock);
        }
        return intent.Kind switch
        {
            RealtimeR2IntentKind.SetSpeed => SetSpeed(state, intent.Speed),
            RealtimeR2IntentKind.SetPlayerPaused => SetPlayerPaused(state, intent.Paused),
            RealtimeR2IntentKind.SelectTool => SelectTool(
                state,
                intent.Tool,
                intent.Surface,
                intent.FirstId),
            RealtimeR2IntentKind.OpenSurface => OpenSurface(state, intent.Surface),
            RealtimeR2IntentKind.CloseSurface => CloseSurface(state, intent.Surface),
            RealtimeR2IntentKind.SelectId => Select(state, intent.FirstId),
            RealtimeR2IntentKind.ClearSelection => Accepted(state with
            {
                SelectionId = null,
                TimelineSelectedItemId = null,
                Surface = state.Surface == RealtimeSurface.Inspector
                    ? RealtimeSurface.World
                    : state.Surface,
            }),
            RealtimeR2IntentKind.OpenModal => OpenModal(state, intent),
            RealtimeR2IntentKind.CloseModal => CloseModal(state, intent.FirstId),
            RealtimeR2IntentKind.AcknowledgeAutoPause => AcknowledgeAutoPause(state),
            RealtimeR2IntentKind.ToggleAnalysis => ToggleAnalysis(state),
            RealtimeR2IntentKind.SetTimelineView => SetTimelineView(state, intent),
            _ => Accepted(state),
        };
    }

    internal static RealtimeDraftToolLock? ResolveDraftToolLock(
        ConstructionSnapshot construction)
    {
        ArgumentNullException.ThrowIfNull(construction);
        if (construction.NodeDraft is NodeDraftSnapshot nodeDraft)
        {
            return new RealtimeDraftToolLock(
                RealtimeDraftToolLockKind.NodeDraft,
                RealtimeTool.BuildNode,
                $"NODE:{nodeDraft.NodeClassId}",
                "변전소 초안을 먼저 발주하거나 Esc를 두 번 눌러 취소한 뒤 도구를 바꾸세요.");
        }
        if (construction.LineDraft is not LineDraftSnapshot lineDraft)
        {
            return null;
        }
        bool closed = lineDraft.EndNodeId is not null;
        return new RealtimeDraftToolLock(
            closed
                ? RealtimeDraftToolLockKind.ClosedLineDraft
                : RealtimeDraftToolLockKind.OpenLineDraft,
            RealtimeTool.BuildLine,
            $"LINE:{lineDraft.LineClassId}:{lineDraft.PoleClassId}",
            closed
                ? "닫힌 선로 초안을 먼저 발주하거나 Esc를 두 번 눌러 취소한 뒤 도구를 바꾸세요."
                : "작성 중인 선로 초안을 먼저 완성하거나 Esc를 두 번 눌러 취소한 뒤 도구를 바꾸세요.");
    }

    internal static string? DraftToolChangeBlockReason(
        ConstructionSnapshot construction,
        RealtimeR2Intent intent)
    {
        ArgumentNullException.ThrowIfNull(construction);
        ArgumentNullException.ThrowIfNull(intent);
        RealtimeDraftToolLock? toolLock = ResolveDraftToolLock(construction);
        if (toolLock is null)
        {
            return null;
        }
        return intent.Kind switch
        {
            RealtimeR2IntentKind.SelectTool when !toolLock.Allows(
                intent.Tool,
                intent.FirstId) => toolLock.RejectionReason,
            RealtimeR2IntentKind.ToggleAnalysis => toolLock.RejectionReason,
            _ => null,
        };
    }

    internal static RealtimeInteractionState AlignWithAuthoritativeDraft(
        RealtimeInteractionState state,
        ConstructionSnapshot construction)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(construction);
        if (state.Simulation == RealtimeSimulationState.Ended)
        {
            return EnterEndedReadOnly(state);
        }
        RealtimeDraftToolLock? toolLock = ResolveDraftToolLock(construction);
        if (toolLock is null ||
            toolLock.Allows(state.Tool, state.SelectedBuildToolId))
        {
            return state;
        }
        return state with
        {
            Tool = toolLock.RequiredTool,
            Surface = RealtimeSurface.Drawer,
            SelectedBuildToolId = toolLock.RequiredBuildToolId,
        };
    }

    private static RealtimeInteractionReduction SetTimelineView(
        RealtimeInteractionState state,
        RealtimeR2Intent intent)
    {
        if (!intent.TimelineHorizon.HasValue ||
            !Enum.IsDefined(intent.TimelineHorizon.Value) ||
            intent.TimelineAnchorMinute is < 0)
        {
            return Rejected(state, "사건 지평선의 시간 범위를 확인할 수 없습니다.");
        }
        if (state.Surface == RealtimeSurface.BlockingModal)
        {
            return Rejected(state, "A blocking modal owns interaction.");
        }
        return Accepted(EnforceAnalysisVisibility(state with
        {
            TimelineSelectedItemId = intent.FirstId,
            SelectionId = intent.SecondId ?? intent.FirstId,
            Surface = (intent.SecondId ?? intent.FirstId) is null
                ? state.Surface == RealtimeSurface.Inspector
                    ? RealtimeSurface.World
                    : state.Surface
                : RealtimeSurface.Inspector,
            TimelineHorizon = intent.TimelineHorizon.Value,
            TimelineAnchorMinute = intent.TimelineAnchorMinute,
        }));
    }

    internal static RealtimeInteractionState AutoPause(
        RealtimeInteractionState state,
        RealtimePauseReason reason)
    {
        if (reason is RealtimePauseReason.None or RealtimePauseReason.PlayerRequest)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }
        return reason == RealtimePauseReason.CampaignResult
            ? EnterEndedReadOnly(state)
            : state with
        {
            Simulation = RealtimeSimulationState.AutoPaused,
            PauseReason = reason,
        };
    }

    internal static bool IsEndedWriteIntent(RealtimeR2Intent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return intent.Kind switch
        {
            RealtimeR2IntentKind.SelectTool => intent.Tool is
                RealtimeTool.BuildNode or RealtimeTool.BuildLine or
                RealtimeTool.MoveDraft,
            RealtimeR2IntentKind.SetNodeDraft or
                RealtimeR2IntentKind.CancelNodeDraft or
                RealtimeR2IntentKind.OrderNode or
                RealtimeR2IntentKind.StartLineDraft or
                RealtimeR2IntentKind.AddLinePoint or
                RealtimeR2IntentKind.MoveLinePoint or
                RealtimeR2IntentKind.UndoLinePoint or
                RealtimeR2IntentKind.FinishLineDraft or
                RealtimeR2IntentKind.CancelLineDraft or
                RealtimeR2IntentKind.OrderLine or
                RealtimeR2IntentKind.SetPromiseDecision => true,
            _ => false,
        };
    }

    private static RealtimeInteractionState EnterEndedReadOnly(
        RealtimeInteractionState state) => state with
    {
        Simulation = RealtimeSimulationState.Ended,
        Tool = RealtimeTool.Inspect,
        Surface = state.Surface == RealtimeSurface.BlockingModal
            ? RealtimeSurface.BlockingModal
            : RealtimeSurface.World,
        PauseReason = RealtimePauseReason.CampaignResult,
        SelectedBuildToolId = null,
    };

    private static RealtimeInteractionReduction ToggleAnalysis(
        RealtimeInteractionState state)
    {
        bool enable = state.Tool != RealtimeTool.Analysis;
        return Accepted(state with
        {
            Tool = enable ? RealtimeTool.Analysis : RealtimeTool.Inspect,
            // Mouse and keyboard share this one surface policy. Keeping the
            // drawer present makes the active analysis state visible instead of
            // turning it into an overlay with no persistent control-state cue.
            Surface = RealtimeSurface.Drawer,
            SelectedBuildToolId = null,
        });
    }

    private static RealtimeInteractionState EnforceAnalysisVisibility(
        RealtimeInteractionState state) =>
        state.Tool == RealtimeTool.Analysis &&
        state.Surface != RealtimeSurface.Drawer
            ? state with
            {
                Tool = RealtimeTool.Inspect,
                SelectedBuildToolId = null,
            }
            : state;

    private static RealtimeInteractionReduction SetSpeed(
        RealtimeInteractionState state,
        RealtimeSimulationSpeed? speed)
    {
        if (speed is null || speed is not (
                RealtimeSimulationSpeed.Paused or
                RealtimeSimulationSpeed.Normal or
                RealtimeSimulationSpeed.Fast or
                RealtimeSimulationSpeed.VeryFast))
        {
            return Rejected(state, "Unsupported simulation speed.");
        }
        if (state.Simulation is RealtimeSimulationState.Ended or
                RealtimeSimulationState.AutoPaused ||
            state.Surface == RealtimeSurface.BlockingModal)
        {
            return Rejected(state, "Simulation speed is locked by the active state.");
        }
        return speed == RealtimeSimulationSpeed.Paused
            ? SetPlayerPaused(state, true)
            : Accepted(state with
            {
                Simulation = RealtimeSimulationState.Running,
                RunningSpeed = speed.Value,
                PauseReason = RealtimePauseReason.None,
            });
    }

    private static RealtimeInteractionReduction SetPlayerPaused(
        RealtimeInteractionState state,
        bool? paused)
    {
        if (!paused.HasValue)
        {
            return Rejected(state, "Pause intent is missing its state.");
        }
        if (state.Simulation == RealtimeSimulationState.Ended ||
            state.Surface == RealtimeSurface.BlockingModal ||
            state.Simulation == RealtimeSimulationState.AutoPaused)
        {
            return Rejected(state, "Player pause cannot override the active pause reason.");
        }
        return Accepted(state with
        {
            Simulation = paused.Value
                ? RealtimeSimulationState.PlayerPaused
                : RealtimeSimulationState.Running,
            PauseReason = paused.Value
                ? RealtimePauseReason.PlayerRequest
                : RealtimePauseReason.None,
        });
    }

    private static RealtimeInteractionReduction SelectTool(
        RealtimeInteractionState state,
        RealtimeTool? tool,
        RealtimeSurface? targetSurface,
        string? buildToolId)
    {
        if (!tool.HasValue || !Enum.IsDefined(tool.Value))
        {
            return Rejected(state, "알 수 없는 상호작용 도구입니다.");
        }
        if (state.Surface == RealtimeSurface.BlockingModal)
        {
            return Rejected(state, "A blocking modal owns interaction.");
        }
        if (targetSurface.HasValue &&
            (targetSurface == RealtimeSurface.BlockingModal ||
             !Enum.IsDefined(targetSurface.Value)))
        {
            return Rejected(state, "A tool cannot open an invalid surface.");
        }
        bool buildTool = tool is RealtimeTool.BuildNode or RealtimeTool.BuildLine;
        if (buildTool && (string.IsNullOrWhiteSpace(buildToolId) ||
                          buildToolId != buildToolId.Trim()))
        {
            return Rejected(state, "화면에 표시된 공사 도구를 다시 선택하세요.");
        }
        if (tool == RealtimeTool.Analysis)
        {
            return Accepted(state with
            {
                Tool = RealtimeTool.Analysis,
                Surface = RealtimeSurface.Drawer,
                SelectedBuildToolId = null,
            });
        }
        return Accepted(state with
        {
            Tool = tool.Value,
            Surface = targetSurface ?? state.Surface,
            SelectedBuildToolId = buildTool ? buildToolId : null,
        });
    }

    private static RealtimeInteractionReduction OpenSurface(
        RealtimeInteractionState state,
        RealtimeSurface? surface)
    {
        if (!surface.HasValue || surface == RealtimeSurface.BlockingModal ||
            !Enum.IsDefined(surface.Value))
        {
            return Rejected(state, "알 수 없는 보조 화면입니다.");
        }
        if (state.Surface == RealtimeSurface.BlockingModal)
        {
            return Rejected(state, "A blocking modal owns interaction.");
        }
        return Accepted(EnforceAnalysisVisibility(state with
        {
            Surface = surface.Value,
        }));
    }

    private static RealtimeInteractionReduction CloseSurface(
        RealtimeInteractionState state,
        RealtimeSurface? surface)
    {
        if (state.Surface == RealtimeSurface.BlockingModal ||
            surface.HasValue && surface.Value != state.Surface)
        {
            return Rejected(state, "The requested surface is not closable.");
        }
        return Accepted(EnforceAnalysisVisibility(state with
        {
            Surface = RealtimeSurface.World,
        }));
    }

    private static RealtimeInteractionReduction Select(
        RealtimeInteractionState state,
        string? selectionId)
    {
        if (string.IsNullOrWhiteSpace(selectionId) || selectionId != selectionId.Trim())
        {
            return Rejected(state, "선택한 대상을 찾을 수 없습니다.");
        }
        if (state.Surface == RealtimeSurface.BlockingModal)
        {
            return Rejected(state, "A blocking modal owns interaction.");
        }
        return Accepted(EnforceAnalysisVisibility(state with
        {
            SelectionId = selectionId,
            TimelineSelectedItemId = null,
            Surface = RealtimeSurface.Inspector,
        }));
    }

    private static RealtimeInteractionReduction OpenModal(
        RealtimeInteractionState state,
        RealtimeR2Intent intent)
    {
        if (state.ActiveModalId is not null || string.IsNullOrWhiteSpace(intent.FirstId) ||
            !intent.ModalKind.HasValue || !intent.PauseReason.HasValue ||
            intent.PauseReason == RealtimePauseReason.None)
        {
            return Rejected(state, "A single valid blocking modal is required.");
        }
        var restore = new RealtimeInteractionRestorePoint(
            state.Simulation,
            state.RunningSpeed,
            state.Tool,
            state.Surface,
            state.PauseReason,
            intent.ReturnFocusId);
        return Accepted(state with
        {
            Simulation = state.Simulation == RealtimeSimulationState.Ended
                ? RealtimeSimulationState.Ended
                : RealtimeSimulationState.AutoPaused,
            Surface = RealtimeSurface.BlockingModal,
            PauseReason = intent.PauseReason.Value,
            ActiveModalId = intent.FirstId,
            ActiveModalKind = intent.ModalKind,
            ReturnFocusId = intent.ReturnFocusId,
            ModalRestore = restore,
        });
    }

    private static RealtimeInteractionReduction CloseModal(
        RealtimeInteractionState state,
        string? modalId)
    {
        if (state.ActiveModalId is null || state.ModalRestore is null ||
            !string.Equals(state.ActiveModalId, modalId, StringComparison.Ordinal))
        {
            return Rejected(state, "The requested modal is not active.");
        }
        RealtimeInteractionRestorePoint restore = state.ModalRestore;
        RealtimeInteractionState restored = state with
        {
            Simulation = restore.Simulation,
            RunningSpeed = restore.RunningSpeed,
            Tool = restore.Tool,
            Surface = restore.Surface,
            PauseReason = restore.PauseReason,
            ActiveModalId = null,
            ActiveModalKind = null,
            ReturnFocusId = restore.ReturnFocusId,
            ModalRestore = null,
        };
        return Accepted(restore.Simulation == RealtimeSimulationState.Ended
            ? EnterEndedReadOnly(restored)
            : restored);
    }

    private static RealtimeInteractionReduction AcknowledgeAutoPause(
        RealtimeInteractionState state)
    {
        if (state.Simulation != RealtimeSimulationState.AutoPaused ||
            state.ActiveModalId is not null)
        {
            return Rejected(state, "No nonmodal automatic pause is active.");
        }
        return Accepted(state with
        {
            Simulation = RealtimeSimulationState.Running,
            PauseReason = RealtimePauseReason.None,
        });
    }

    private static RealtimeInteractionReduction Accepted(RealtimeInteractionState state) =>
        new(true, null, state);

    private static RealtimeInteractionReduction Rejected(
        RealtimeInteractionState state,
        string error) => new(false, error, state);
}
