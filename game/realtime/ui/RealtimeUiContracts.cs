using System;
using System.Collections.Generic;
using System.Linq;

namespace Gridworks.Game.Realtime.UI;

internal enum RealtimeSimulationSpeed
{
    Paused = 0,
    Normal = 1,
    Fast = 2,
    VeryFast = 4,
}

internal enum RealtimeSimulationState
{
    Running,
    PlayerPaused,
    AutoPaused,
    Ended,
}

internal enum RealtimeTool
{
    Inspect,
    BuildNode,
    BuildLine,
    MoveDraft,
    Analysis,
}

internal enum RealtimeSurface
{
    World,
    Inspector,
    Timeline,
    Drawer,
    BlockingModal,
}

internal enum RealtimePauseReason
{
    None,
    PlayerRequest,
    ChapterBriefing,
    CriticalIncident,
    RecoveryConfirmation,
    CampaignResult,
    CatchUpCeiling,
    FatalError,
}

internal sealed record RealtimePausePresentation(
    RealtimePauseReason Reason,
    long CurrentMinute,
    string CurrentTimeLabel,
    string? NextEventId,
    long? NextEventMinute,
    string NextEventLabel)
{
    public static readonly RealtimePausePresentation None = new(
        RealtimePauseReason.None,
        0,
        string.Empty,
        null,
        null,
        string.Empty);
}

internal enum RealtimeReliabilityState
{
    Stable,
    Watch,
    Emergency,
    Outage,
}

internal enum RealtimeTimelineItemKind
{
    ExternalEvent,
    Construction,
    Decision,
    Weather,
    Demand,
    PlannedOutage,
    ThermalProtection,
}

internal enum RealtimeTimelineLane
{
    DemandAndDeadline = 0,
    WeatherAndOutage = 1,
    Construction = 2,
    ThermalProtection = 3,
}

internal enum RealtimeTimelineHorizonPreset
{
    SixHours,
    TwentyFourHours,
    SevenDays,
}

internal enum RealtimeTimelineNavigation
{
    Home,
    PreviousEvent,
    NextEvent,
}

internal enum RealtimeTimelineSeverity
{
    Information,
    Advisory,
    Warning,
    Critical,
}

internal enum RealtimeTimelineVisibility
{
    Hidden,
    Announced,
    Active,
    Completed,
}

internal enum RealtimeActionTone
{
    Primary,
    Secondary,
    Destructive,
}

internal enum RealtimeModalKind
{
    ChapterStory,
    NewGameConfirmation,
    RecoveryConfirmation,
    FatalError,
}

internal enum RealtimeInputPriority
{
    EmptyTerrain = 0,
    WorldCandidate = 100,
    SelectionAction = 200,
    DraftHandle = 300,
    PanCapture = 350,
    Hud = 400,
    BlockingModal = 500,
    Fatal = 600,
}

internal enum RealtimeInputCommand
{
    TogglePause,
    SetNormalSpeed,
    SetFastSpeed,
    SetVeryFastSpeed,
    ToggleAnalysis,
    ToggleBuildShelf,
    CancelOrBack,
    ConfirmOrSelect,
    UndoDraftStep,
    CycleCandidatePrevious,
    CycleCandidateNext,
    BeginPan,
    EndPan,
    TimelineHome,
    TimelinePrevious,
    TimelineNext,
    SelectInspectTool,
    SelectFirstNodeTool,
    SelectFirstLineTool,
}

internal readonly record struct RealtimeInputRequest(
    RealtimeInputCommand Command,
    RealtimeInputPriority SourcePriority);

internal sealed record RealtimeTopHudPresentation(
    string Chapter,
    string Objective,
    string Clock,
    string Cash,
    string Reliability,
    RealtimeReliabilityState ReliabilityState,
    RealtimeSimulationSpeed Speed,
    string? MajorWarning = null)
{
    public RealtimeSimulationState SimulationState { get; init; } =
        RealtimeSimulationState.Running;

    public RealtimePausePresentation Pause { get; init; } =
        RealtimePausePresentation.None;

    public bool ToolShelfVisible { get; init; }

    public bool BuildModeActive { get; init; }
}

internal sealed record RealtimeTimelineItemPresentation(
    string Id,
    RealtimeTimelineItemKind Kind,
    long StartMinute,
    long? EndMinute,
    string Title,
    string ShortLabel,
    string Description,
    RealtimeTimelineSeverity Severity,
    RealtimeTimelineVisibility Visibility,
    bool IsCurrent,
    bool IsActionable)
{
    public RealtimeTimelineLane Lane { get; init; } = LaneFor(Kind);

    public int Priority { get; init; }

    public string KindIcon { get; init; } = IconFor(Kind);

    public string KindLabel { get; init; } = LabelFor(Kind);

    public string TimeLabel { get; init; } = string.Empty;

    public string SeverityLabel { get; init; } = Severity.ToString();

    private static RealtimeTimelineLane LaneFor(RealtimeTimelineItemKind kind) => kind switch
    {
        RealtimeTimelineItemKind.Construction => RealtimeTimelineLane.Construction,
        RealtimeTimelineItemKind.Weather or RealtimeTimelineItemKind.PlannedOutage =>
            RealtimeTimelineLane.WeatherAndOutage,
        RealtimeTimelineItemKind.ThermalProtection =>
            RealtimeTimelineLane.ThermalProtection,
        _ => RealtimeTimelineLane.DemandAndDeadline,
    };

    private static string IconFor(RealtimeTimelineItemKind kind) => kind switch
    {
        RealtimeTimelineItemKind.ExternalEvent => "◆",
        RealtimeTimelineItemKind.Construction => "▰",
        RealtimeTimelineItemKind.Decision => "◇",
        RealtimeTimelineItemKind.Weather => "☂",
        RealtimeTimelineItemKind.Demand => "▲",
        RealtimeTimelineItemKind.PlannedOutage => "⊘",
        RealtimeTimelineItemKind.ThermalProtection => "!",
        _ => "•",
    };

    private static string LabelFor(RealtimeTimelineItemKind kind) => kind switch
    {
        RealtimeTimelineItemKind.ExternalEvent => "도시 사건",
        RealtimeTimelineItemKind.Construction => "공사",
        RealtimeTimelineItemKind.Decision => "운영 결정",
        RealtimeTimelineItemKind.Weather => "기상",
        RealtimeTimelineItemKind.Demand => "전력 수요",
        RealtimeTimelineItemKind.PlannedOutage => "계획 사용불가",
        RealtimeTimelineItemKind.ThermalProtection => "열 보호",
        _ => "일정",
    };
}

internal sealed record RealtimeEventRailPresentation(
    long NowMinute,
    long HorizonStartMinute,
    long HorizonEndMinute,
    string NowLabel,
    string HorizonLabel,
    IReadOnlyList<RealtimeTimelineItemPresentation> Items,
    string? SelectedItemId = null)
{
    private IReadOnlyList<RealtimeTimelineItemPresentation> _items = Freeze(Items);

    public IReadOnlyList<RealtimeTimelineItemPresentation> Items
    {
        get => _items;
        init => _items = Freeze(value);
    }

    public RealtimeTimelineHorizonPreset HorizonPreset { get; init; } =
        RealtimeTimelineHorizonPreset.TwentyFourHours;

    public bool Expanded { get; init; } = true;

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

internal sealed record RealtimeActionPresentation(
    string Id,
    string Label,
    string Description,
    bool Enabled,
    RealtimeActionTone Tone = RealtimeActionTone.Primary,
    bool Visible = true);

internal sealed record RealtimeContextSectionPresentation(
    string Heading,
    string Body,
    RealtimeTimelineSeverity Severity = RealtimeTimelineSeverity.Information);

internal enum RealtimeContextDetailTab
{
    Route,
    Thermal,
    Forecast,
    History,
}

internal sealed record RealtimeContextDetailPresentation(
    RealtimeContextDetailTab Tab,
    string Heading,
    string Body,
    RealtimeTimelineSeverity Severity = RealtimeTimelineSeverity.Information);

internal sealed record RealtimeContextDockPresentation(
    string SubjectId,
    bool Visible,
    string Eyebrow,
    string Heading,
    IReadOnlyList<RealtimeContextSectionPresentation> Sections,
    RealtimeActionPresentation? PrimaryAction = null,
    RealtimeActionPresentation? SecondaryAction = null)
{
    private IReadOnlyList<RealtimeContextSectionPresentation> _sections = Freeze(Sections);
    private IReadOnlyList<RealtimeContextDetailPresentation> _details =
        Array.Empty<RealtimeContextDetailPresentation>();

    public IReadOnlyList<RealtimeContextSectionPresentation> Sections
    {
        get => _sections;
        init => _sections = Freeze(value);
    }

    /// <summary>
    /// Optional long-form rows. They are never mixed into the fixed summary;
    /// only the selected detail table may scroll when it genuinely overflows.
    /// </summary>
    public IReadOnlyList<RealtimeContextDetailPresentation> Details
    {
        get => _details;
        init => _details = Freeze(value);
    }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

internal sealed record RealtimeBuildToolPresentation(
    string Id,
    string Label,
    string Shortcut,
    string Description,
    bool Enabled,
    bool Selected);

internal sealed record RealtimeBuildShelfPresentation(
    bool Visible,
    IReadOnlyList<RealtimeBuildToolPresentation> Tools)
{
    private IReadOnlyList<RealtimeBuildToolPresentation> _tools = Freeze(Tools);

    public IReadOnlyList<RealtimeBuildToolPresentation> Tools
    {
        get => _tools;
        init => _tools = Freeze(value);
    }

    public string Guidance { get; init; } = string.Empty;

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

internal sealed record RealtimeActionDockPresentation(
    bool Visible,
    string Context,
    string Detail,
    RealtimeActionPresentation? PrimaryAction);

internal sealed record RealtimeModalPresentation(
    string Id,
    RealtimeModalKind Kind,
    string Eyebrow,
    string Heading,
    string Body,
    RealtimeActionPresentation PrimaryAction,
    RealtimeActionPresentation? SecondaryAction,
    bool PausesSimulation,
    bool DismissOnCancel)
{
    public RealtimePausePresentation Pause { get; init; } =
        RealtimePausePresentation.None;
}

internal sealed record RealtimeInteractionPresentation(
    RealtimeSimulationState Simulation,
    RealtimeSimulationSpeed Speed,
    RealtimeTool Tool,
    RealtimeSurface Surface,
    string? SelectionId,
    RealtimePausePresentation Pause);
