using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;
using Godot;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

internal sealed record RealtimeR2IntentResult(
    bool Accepted,
    string? Error,
    RealtimeCommandResult? CoreCommandResult,
    string BeforeCanonicalStateSha256,
    string AfterCanonicalStateSha256,
    long BeforeMinute,
    long AfterMinute,
    long BeforeCommandSequence,
    long AfterCommandSequence,
    int JournalDelta,
    long PresentationRevisionDelta);

internal sealed record RealtimeR2FrameResult(
    RealtimeFrameAdvanceResult? Frame,
    RealtimeCampaignSnapshot CoreSnapshot,
    IReadOnlyList<RealtimeTransition> Transitions,
    long RequestedFrameCount,
    long ConsumedFrameCount,
    int FramesPerSecond,
    long WallClockRemainderUnits,
    long PresentationRevisionDelta,
    IReadOnlyList<RealtimeR2PendingFrameDebt> RetainedFrameDebt)
{
    private IReadOnlyList<RealtimeTransition> _transitions =
        Array.AsReadOnly(Transitions.ToArray());
    private IReadOnlyList<RealtimeR2PendingFrameDebt> _retainedFrameDebt =
        Array.AsReadOnly(RetainedFrameDebt.ToArray());

    public IReadOnlyList<RealtimeTransition> Transitions
    {
        get => _transitions;
        init => _transitions = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<RealtimeR2PendingFrameDebt> RetainedFrameDebt
    {
        get => _retainedFrameDebt;
        init => _retainedFrameDebt = Array.AsReadOnly(value.ToArray());
    }
}

internal sealed record RealtimeR2PendingFrameDebt(
    long FrameCount,
    int FramesPerSecond,
    int SpeedMultiplier);

internal sealed record RealtimeR2AdvanceResult(
    RealtimeAdvanceResult Advance,
    long PresentationRevisionDelta);

#if DEBUG
internal sealed record RealtimeSmokeLinePlan(
    string StartNodeId,
    string EndNodeId,
    string LineClassId,
    string PoleClassId,
    IReadOnlyList<CoreMapPoint> IntermediatePoints,
    long OrderMinute,
    long BuildMinutes,
    long ExpectedCompletionMinute,
    IReadOnlyList<string> ExpectedNodeIds,
    IReadOnlyList<string> ExpectedEdgeIds,
    RealtimeRunError ExpectedSecondOrderError,
    ConstructionError? ExpectedSecondOrderConstructionError,
    IReadOnlyList<RealtimeR2Intent> Intents);

internal sealed record RealtimeSmokeEventBoundary(
    string EventId,
    long StartMinute,
    long EndMinute,
    int Priority);

internal sealed record RealtimeSmokeThermalBoundary(
    string AssetId,
    ThermalAssetKind AssetKind,
    long? EmergencyStartMinute,
    long? TripMinute,
    long? RecoveryMinute);

internal sealed record RealtimeSmokePointerPoint(string Id, CoreMapPoint WorldPoint);

internal sealed record RealtimeSmokeBoundaryFacts(
    IReadOnlyList<RealtimeSmokeEventBoundary> Events,
    long ConstructionCompletionMinute,
    IReadOnlyList<RealtimeSmokeThermalBoundary> Thermal,
    IReadOnlyList<RealtimeSmokePointerPoint> PointerPoints);

internal enum RealtimeSmokePointerProbeKind
{
    Modal,
    Hud,
    Draft,
    SelectionAction,
    World,
    Empty,
    Overlay,
    Weather,
    Fatal,
}

internal sealed record RealtimePointerProbeResult(
    RealtimePointerResolution Resolution,
    int BeforeCommandCount,
    int AfterCommandCount,
    long BeforePresentationRevision,
    long AfterPresentationRevision,
    string? SelectionId,
    IReadOnlyDictionary<RealtimePointerOwner, int> ClickCounters);

internal sealed record RealtimeR2LayoutFacts(
    Vector2I PhysicalWindowSize,
    Vector2I PriorContentScaleSize,
    Vector2I CurrentContentScaleSize,
    Vector2I RequiredContentScaleSize,
    Window.ContentScaleModeEnum PriorContentScaleMode,
    Window.ContentScaleModeEnum CurrentContentScaleMode,
    Window.ContentScaleAspectEnum PriorContentScaleAspect,
    Window.ContentScaleAspectEnum CurrentContentScaleAspect,
    Vector2 CameraCenter);

internal sealed record RealtimeR2InputOwnershipFacts(
    RealtimeInputRequest? LastRequest,
    bool Panning,
    IReadOnlyDictionary<RealtimePointerOwner, int> PointerClickCounters);

internal sealed record RealtimeR2TimelineChooserFacts(
    IReadOnlyList<string> VisibleOrderedItemIds,
    IReadOnlyList<string> ClusterItemIds,
    int ClusterIndex,
    string? SelectedMarkerId,
    string? SelectedSubjectId)
{
    private IReadOnlyList<string> _visibleOrderedItemIds =
        Array.AsReadOnly(VisibleOrderedItemIds.ToArray());
    private IReadOnlyList<string> _clusterItemIds =
        Array.AsReadOnly(ClusterItemIds.ToArray());

    public IReadOnlyList<string> VisibleOrderedItemIds
    {
        get => _visibleOrderedItemIds;
        init => _visibleOrderedItemIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> ClusterItemIds
    {
        get => _clusterItemIds;
        init => _clusterItemIds = Array.AsReadOnly(value.ToArray());
    }
}
#endif

/// <summary>
/// Non-default R2 vertical-slice host. It owns one Core run, one exact frame accumulator,
/// the pure interaction reducer, and immutable presentations. No product save/default-scene
/// authority is attached here.
/// </summary>
internal sealed partial class RealtimeSliceMain : Control
{
    private static readonly Vector2I RequiredLogicalCanvas = new(1920, 1080);
    private const int VirtualFramesPerSecond = 60;
    private const long NanosecondsPerSecond = 1_000_000_000;
    private const long CatchUpCeilingMinutes = 30;

    private readonly List<RealtimeTransition> _emittedTransitions = [];
    private readonly HashSet<string> _autoPausedIncidentKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<RealtimePointerOwner, int> _clickCounters = [];
    private readonly Queue<PendingFrameBatch> _retainedFrameDebt = [];
    private RealtimeSliceData? _data;
    private RealtimeCampaignRun? _run;
    private RealtimeFrameAccumulator? _frame;
    private RealtimeInteractionState? _interaction;
    private RealtimeSlicePresentation? _latestPresentation;
    private RealtimePlaceholderMap? _map;
    private RealtimeUiRoot? _ui;
    private CoreMapPoint? _pointerPoint;
    private bool _pointerAccepted = true;
    private string _pointerMessage = string.Empty;
    private long _presentationRevision;
    private long _wallClockRemainderUnits;
    private double _wallClockVirtualFrameRemainder;
    private string? _presentedModalId;
    private Vector2I _priorContentScaleSize;
    private Window.ContentScaleModeEnum _priorContentScaleMode;
    private Window.ContentScaleAspectEnum _priorContentScaleAspect;
    private bool _logicalCanvasApplied;
    private IReadOnlyList<string> _timelineClusterIds = Array.Empty<string>();
    private int _timelineClusterIndex;
    private bool _draftCancelArmed;

#if DEBUG
    private RealtimeSmokeLinePlan? _smokeLinePlan;
    private RealtimeSmokeBoundaryFacts? _smokeBoundaryFacts;
    private RealtimeInputRequest? _lastInputRequest;
#endif

    public override void _Ready()
    {
        _map = GetNode<RealtimePlaceholderMap>("%PlaceholderMap");
        _ui = GetNode<RealtimeUiRoot>("%UiRoot");
        WireNodes();
        ApplyLogicalCanvas();
        Bootstrap();
        SetProcess(true);
    }

    public override void _ExitTree()
    {
        if (_logicalCanvasApplied && GetWindow() is Window window)
        {
            window.ContentScaleSize = _priorContentScaleSize;
            window.ContentScaleMode = _priorContentScaleMode;
            window.ContentScaleAspect = _priorContentScaleAspect;
        }
        _logicalCanvasApplied = false;
    }

    public override void _Process(double delta)
    {
        if (_run is null || delta <= 0)
        {
            return;
        }
        _ = InjectElapsedSeconds(delta);
    }

    private void Bootstrap()
    {
        _data = RealtimeSliceResources.Load(typeof(RealtimeSliceMain).Assembly);
        _run = new RealtimeCampaignRun(_data.Campaign, _data.World);
        _frame = new RealtimeFrameAccumulator(CatchUpCeilingMinutes);
        _interaction = RealtimeInteractionReducer.Initial(chapterBriefing: true);
        _frame.Pause();
        _emittedTransitions.Clear();
        _autoPausedIncidentKeys.Clear();
        _clickCounters.Clear();
        foreach (RealtimePointerOwner owner in Enum.GetValues<RealtimePointerOwner>())
        {
            _clickCounters.Add(owner, 0);
        }
        _pointerPoint = null;
        _pointerAccepted = true;
        _pointerMessage = string.Empty;
        _presentationRevision = 0;
        _wallClockRemainderUnits = 0;
        _wallClockVirtualFrameRemainder = 0;
        _retainedFrameDebt.Clear();
        _presentedModalId = null;
        _timelineClusterIds = Array.Empty<string>();
        _timelineClusterIndex = 0;
        _draftCancelArmed = false;
#if DEBUG
        _smokeLinePlan = BuildSmokeLinePlan(_data);
        _smokeBoundaryFacts = BuildSmokeBoundaryFacts(
            _run.GetSnapshot(),
            _smokeLinePlan);
        _lastInputRequest = null;
#endif
        Present();
    }

    private void WireNodes()
    {
        _map!.PrimaryRequested += HandleMapPrimary;
        _map.PointerMoved += HandleMapPointerMoved;
        _map.CancelRequested += HandleUndoDraftStep;
        _ui!.SpeedRequested += HandleSpeedRequested;
        _ui.MenuRequested += () => HandleShortcut(RealtimeInputCommand.ToggleBuildShelf);
        _ui.TimelineItemsRequested += HandleTimelineItems;
        _ui.TimelineHorizonDeltaRequested += HandleTimelineHorizonDelta;
        _ui.TimelineNavigationRequested += HandleTimelineNavigation;
        _ui.TimelineExpansionRequested += expanded => _ = ApplyIntent(
            new RealtimeR2Intent(
                expanded
                    ? RealtimeR2IntentKind.OpenSurface
                    : RealtimeR2IntentKind.CloseSurface,
                Surface: RealtimeSurface.Timeline));
        _ui.ContextCloseRequested += () => _ = ApplyIntent(new RealtimeR2Intent(
            RealtimeR2IntentKind.CloseSurface,
            Surface: RealtimeSurface.Inspector));
        _ui.ActionRequested += HandleAction;
        _ui.BuildToolRequested += HandleBuildTool;
        _ui.ModalActionRequested += HandleModalAction;
        _ui.ModalDismissRequested += modalId =>
            _ = ApplyIntent(RealtimeR2Intent.CloseModal(modalId));
        // The priority-bearing request is the single keyboard owner. The map
        // handles pointer/wheel input only; every physical key reaches this
        // route at most once after modal/HUD priority arbitration.
        _ui.InputRequested += HandleInputRequest;
        _ui.MapInteractionRectChanged += ApplyMapInteractionRect;
    }

    private RealtimeR2IntentResult ApplyIntent(RealtimeR2Intent intent)
    {
        EnsureBootstrapped();
        _draftCancelArmed = false;
        string beforeHash = _run!.GetCanonicalStateSha256();
        RealtimeCampaignSnapshot beforeSnapshot = _run.GetSnapshot();
        long beforeMinute = beforeSnapshot.Minute;
        long beforeSequence = NextCommandSequence;
        int beforeCount = _run.AcceptedCommands.Count;
        long beforeRevision = _presentationRevision;
        bool constructionIntent = IsConstructionIntent(intent.Kind);
        ConstructionSnapshot authoritativeConstruction =
            beforeSnapshot.Construction;
        bool endedWriteIntent =
            (_interaction!.Simulation == RealtimeSimulationState.Ended ||
             beforeSnapshot.CampaignComplete) &&
            RealtimeInteractionReducer.IsEndedWriteIntent(intent);
        if (endedWriteIntent &&
            _interaction.Simulation != RealtimeSimulationState.Ended)
        {
            SetPointerFeedback(
                false,
                RealtimeInteractionReducer.CampaignEndedReadOnlyReason);
            Present();
            return IntentResult(
                false,
                RealtimeInteractionReducer.CampaignEndedReadOnlyReason,
                null,
                beforeHash,
                beforeMinute,
                beforeSequence,
                beforeCount,
                beforeRevision);
        }
        string? draftToolBlock =
            RealtimeInteractionReducer.DraftToolChangeBlockReason(
                authoritativeConstruction,
                intent);

        RealtimeInteractionReduction reduction = RealtimeInteractionReducer.Reduce(
            _interaction!,
            intent,
            authoritativeConstruction);
        if (!reduction.Accepted)
        {
            if (constructionIntent || draftToolBlock is not null ||
                endedWriteIntent)
            {
                SetPointerFeedback(false, reduction.Error ?? "입력을 처리할 수 없습니다.");
                Present();
            }
            return IntentResult(
                false,
                reduction.Error,
                null,
                beforeHash,
                beforeMinute,
                beforeSequence,
                beforeCount,
                beforeRevision);
        }

        bool interactionChanged = reduction.State != _interaction;
        if (interactionChanged)
        {
            RealtimeInteractionState prior = _interaction!;
            _interaction = reduction.State;
            SynchronizeFramePause(prior, _interaction);
        }

        RealtimeCommand? coreCommand;
        string? shapeError;
        (coreCommand, shapeError) = CoreCommand(intent);
        RealtimeCommandResult? commandResult = null;
        if (shapeError is not null)
        {
            if (constructionIntent)
            {
                SetPointerFeedback(false, shapeError);
                Present();
            }
            return IntentResult(
                false,
                shapeError,
                null,
                beforeHash,
                beforeMinute,
                beforeSequence,
                beforeCount,
                beforeRevision);
        }
        if (coreCommand is not null)
        {
            commandResult = _run.ApplyCommand(
                _run.GetSnapshot().Minute,
                NextCommandSequence,
                coreCommand);
            CollectTransitions(commandResult.Transitions);
            if (constructionIntent)
            {
                SetPointerFeedback(
                    commandResult.Accepted,
                    ConstructionFeedback(intent.Kind, commandResult));
            }
            if (!commandResult.Accepted)
            {
                if (interactionChanged || constructionIntent)
                {
                    Present();
                }
                return IntentResult(
                    false,
                    RealtimeRunErrorText(commandResult.Error),
                    commandResult,
                    beforeHash,
                    beforeMinute,
                    beforeSequence,
                    beforeCount,
                    beforeRevision);
            }
            RealtimeInteractionState aligned =
                RealtimeInteractionReducer.AlignWithAuthoritativeDraft(
                    _interaction!,
                    commandResult.Snapshot.Construction);
            if (aligned != _interaction)
            {
                _interaction = aligned;
                interactionChanged = true;
            }
            if ((intent.Kind is RealtimeR2IntentKind.CancelNodeDraft or
                    RealtimeR2IntentKind.CancelLineDraft) &&
                (IsComparisonMarker(_interaction!.SelectionId) ||
                 IsComparisonMarker(_interaction.TimelineSelectedItemId)))
            {
                RealtimeInteractionReduction clearVanishedComparison =
                    RealtimeInteractionReducer.Reduce(
                        _interaction,
                        RealtimeR2Intent.Select(null),
                        commandResult.Snapshot.Construction);
                if (!clearVanishedComparison.Accepted)
                {
                    throw new InvalidOperationException(
                        "Accepted draft cancellation could not clear its vanished " +
                        "comparison selection.");
                }
                _interaction = clearVanishedComparison.State;
                _timelineClusterIds = Array.Empty<string>();
                _timelineClusterIndex = 0;
                interactionChanged = true;
            }
        }

        if (interactionChanged || coreCommand is not null)
        {
            Present();
        }
        return IntentResult(
            true,
            null,
            commandResult,
            beforeHash,
            beforeMinute,
            beforeSequence,
            beforeCount,
            beforeRevision);
    }

    private RealtimeR2FrameResult InjectElapsedNanoseconds(long elapsedNanoseconds)
    {
        EnsureBootstrapped();
        if (elapsedNanoseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedNanoseconds));
        }
        return InjectElapsedSeconds(elapsedNanoseconds / (double)NanosecondsPerSecond);
    }

    private RealtimeR2FrameResult InjectElapsedSeconds(double elapsedSeconds)
    {
        EnsureBootstrapped();
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }
        long beforeRevision = _presentationRevision;
        if (_interaction!.Simulation != RealtimeSimulationState.Running)
        {
            return FrameResult(
                null,
                Array.Empty<RealtimeTransition>(),
                0,
                0,
                VirtualFramesPerSecond,
                beforeRevision);
        }

        double scaledFrames = checked(
            elapsedSeconds * VirtualFramesPerSecond +
            _wallClockVirtualFrameRemainder);
        double nearestInteger = Math.Round(scaledFrames);
        if (Math.Abs(scaledFrames - nearestInteger) <= 1e-10)
        {
            scaledFrames = nearestInteger;
        }
        if (!double.IsFinite(scaledFrames) || scaledFrames > long.MaxValue)
        {
            throw new OverflowException("Realtime wall interval exceeds frame range.");
        }
        long virtualFrames = checked((long)Math.Floor(scaledFrames));
        double nextRemainder = scaledFrames - virtualFrames;
        if (nextRemainder < 0 || nextRemainder >= 1)
        {
            throw new InvalidOperationException("Realtime wall remainder escaped one frame.");
        }
        _wallClockVirtualFrameRemainder = nextRemainder;
        _wallClockRemainderUnits = Math.Clamp(
            checked((long)Math.Round(
                nextRemainder * NanosecondsPerSecond,
                MidpointRounding.AwayFromZero)),
            0,
            NanosecondsPerSecond - 1);
        if (virtualFrames == 0)
        {
            return FrameResult(
                null,
                Array.Empty<RealtimeTransition>(),
                0,
                0,
                VirtualFramesPerSecond,
                beforeRevision);
        }
        return InjectExactFrames(
            virtualFrames,
            VirtualFramesPerSecond,
            beforeRevision);
    }

    private RealtimeR2FrameResult InjectExactFrames(
        long frameCount,
        int framesPerSecond,
        long? knownBeforeRevision = null)
    {
        EnsureBootstrapped();
        if (frameCount < 0 || framesPerSecond is not (30 or 60 or 120 or 144))
        {
            throw new ArgumentOutOfRangeException(
                frameCount < 0 ? nameof(frameCount) : nameof(framesPerSecond));
        }
        long beforeRevision = knownBeforeRevision ?? _presentationRevision;
        if (_interaction!.Simulation != RealtimeSimulationState.Running)
        {
            return FrameResult(
                null,
                Array.Empty<RealtimeTransition>(),
                frameCount,
                0,
                framesPerSecond,
                beforeRevision);
        }

        if (frameCount > 0)
        {
            _retainedFrameDebt.Enqueue(new PendingFrameBatch(
                frameCount,
                framesPerSecond,
                (int)_interaction.RunningSpeed));
        }

        var transitions = new List<RealtimeTransition>();
        RealtimeFrameAdvanceResult? lastFrame = null;
        long consumedFrames = 0;
        long accruedWholeMinutes = 0;
        long appliedMinutes = 0;
        bool catchUpCeilingReached = false;
        while (_retainedFrameDebt.TryPeek(out PendingFrameBatch? batch) &&
               _interaction.Simulation == RealtimeSimulationState.Running)
        {
            if (appliedMinutes >= CatchUpCeilingMinutes)
            {
                _interaction = RealtimeInteractionReducer.AutoPause(
                    _interaction,
                    RealtimePauseReason.CatchUpCeiling);
                _frame!.Pause();
                catchUpCeilingReached = true;
                break;
            }

            RealtimeFrameAccumulatorSnapshot timing = _frame!.GetSnapshot();
            int unitsPerFrame = checked(
                RealtimeFrameAccumulator.UnitsPerMinute /
                batch.FramesPerSecond * batch.SpeedMultiplier);
            int unitsUntilBoundary = RealtimeFrameAccumulator.UnitsPerMinute -
                timing.FractionalMinuteUnits;
            long framesUntilBoundary = Math.Max(
                1,
                (unitsUntilBoundary + unitsPerFrame - 1L) / unitsPerFrame);
            long chunk = Math.Min(batch.FrameCount, framesUntilBoundary);
            lastFrame = _frame.AdvanceFrames(
                _run!,
                chunk,
                batch.FramesPerSecond,
                batch.SpeedMultiplier);
            batch.FrameCount -= chunk;
            consumedFrames = checked(consumedFrames + chunk);
            accruedWholeMinutes = checked(
                accruedWholeMinutes + lastFrame.AccruedWholeMinutes);
            appliedMinutes = checked(appliedMinutes + lastFrame.AppliedMinutes);
            if (batch.FrameCount == 0)
            {
                _retainedFrameDebt.Dequeue();
            }

            if (lastFrame.Campaign is not null)
            {
                transitions.AddRange(lastFrame.Campaign.Transitions);
                CollectTransitions(lastFrame.Campaign.Transitions);
            }
            if (lastFrame.CatchUpCeilingReached)
            {
                catchUpCeilingReached = true;
                _interaction = RealtimeInteractionReducer.AutoPause(
                    _interaction,
                    RealtimePauseReason.CatchUpCeiling);
                break;
            }
        }
        if (appliedMinutes > 0 ||
            _interaction.PauseReason is RealtimePauseReason.CriticalIncident or
                RealtimePauseReason.CampaignResult or RealtimePauseReason.CatchUpCeiling)
        {
            Present();
        }
        if (lastFrame is not null)
        {
            // One host callback may cross a minute and then end with a
            // fractional chunk. Expose the aggregate campaign result instead
            // of letting that trailing zero-minute chunk hide the state change.
            RealtimeAdvanceResult? aggregateCampaign = appliedMinutes > 0
                ? new RealtimeAdvanceResult(
                    _run!.GetSnapshot(),
                    Array.AsReadOnly(transitions.ToArray()))
                : null;
            lastFrame = new RealtimeFrameAdvanceResult(
                aggregateCampaign,
                _frame!.GetSnapshot(),
                accruedWholeMinutes,
                appliedMinutes,
                catchUpCeilingReached);
        }
        return FrameResult(
            lastFrame,
            transitions,
            frameCount,
            consumedFrames,
            framesPerSecond,
            beforeRevision);
    }

    private void CollectTransitions(IReadOnlyList<RealtimeTransition> transitions)
    {
        foreach (RealtimeTransition transition in transitions)
        {
            _emittedTransitions.Add(transition);
            if (transition.Kind == RealtimeTransitionKind.ThermalProtectiveTrip)
            {
                string key = $"{transition.Minute}:{transition.AssetKind}:{transition.AssetId}";
                if (_autoPausedIncidentKeys.Add(key) &&
                    _interaction!.Simulation == RealtimeSimulationState.Running)
                {
                    _interaction = RealtimeInteractionReducer.AutoPause(
                        _interaction,
                        RealtimePauseReason.CriticalIncident);
                    _frame!.Pause();
                }
            }
            else if (transition.Kind == RealtimeTransitionKind.CampaignCompleted)
            {
                _interaction = RealtimeInteractionReducer.AutoPause(
                    _interaction!,
                    RealtimePauseReason.CampaignResult);
                if (_interaction!.ActiveModalId is null)
                {
                    RealtimeInteractionReduction modal = RealtimeInteractionReducer.Reduce(
                        _interaction,
                        RealtimeR2Intent.OpenModal(
                            "CAMPAIGN_RESULT",
                            RealtimeModalKind.ChapterStory,
                            RealtimePauseReason.CampaignResult,
                            "WORLD"));
                    _interaction = modal.State;
                }
                _frame!.Pause();
            }
        }
    }

    private void Present()
    {
        EnsureBootstrapped(requirePresentation: false);
        RealtimeCampaignSnapshot snapshot = _run!.GetSnapshot();
        long forecastHorizonMinutes =
            RealtimeSlicePresenter.RequiredForecastHorizonMinutes(
                snapshot.Minute,
                _interaction!.TimelineAnchorMinute,
                _interaction.TimelineHorizon);
        RealtimeForecastSnapshot baseForecast =
            _run.GetForecast(forecastHorizonMinutes);
        RealtimeComparisonDraftForecast comparisonDraftForecast =
            _run.GetComparisonDraftForecast(forecastHorizonMinutes);
        RealtimeProjectQuote? nodeOrderQuote =
            snapshot.Construction.NodeDraft is not null
                ? _run.PreviewNodeOrder()
                : null;
        RealtimeProjectQuote? lineOrderQuote =
            snapshot.Construction.LineDraft is { EndNodeId: not null }
                ? _run.PreviewLineOrder()
                : null;
        _presentationRevision = checked(_presentationRevision + 1);
        _latestPresentation = RealtimeSlicePresenter.Present(
            _data!.BaseWorld,
            _data.World,
            snapshot,
            baseForecast,
            comparisonDraftForecast,
            _interaction!,
            _presentationRevision,
            _pointerPoint,
            _pointerAccepted,
            _pointerMessage,
            reduceMotion: false,
            nodeOrderQuote: nodeOrderQuote,
            lineOrderQuote: lineOrderQuote);
        if (_map is null || _ui is null || !IsInsideTree())
        {
            return;
        }
        _map.SetPresentation(_latestPresentation);
        _ui.SetTopHud(_latestPresentation.Hud);
        _ui.SetEventRail(_latestPresentation.Rail);
        _ui.SetContextDock(_latestPresentation.Context);
        _ui.SetBuildShelf(_latestPresentation.BuildShelf);
        _ui.SetActionDock(_latestPresentation.ActionDock);
        PresentModal(_latestPresentation.Modal);
    }

    private void PresentModal(RealtimeModalPresentation? modal)
    {
        if (modal is null)
        {
            if (_presentedModalId is not null)
            {
                _ui!.PopModal();
                _presentedModalId = null;
            }
            return;
        }
        if (string.Equals(_presentedModalId, modal.Id, StringComparison.Ordinal))
        {
            return;
        }
        if (_presentedModalId is not null)
        {
            _ui!.PopModal();
        }
        _ui!.InputRouter.CancelPanCapture();
        _map?.EndPan();
        // Preserve the real control that opened the modal. The map is only a
        // fallback for bootstrap/automatic incidents where no usable opener
        // currently owns focus; forcing it here would make every button-opened
        // modal return to the world instead of its originating control.
        Control? opener = GetViewport().GuiGetFocusOwner();
        if (!IsValidReturnFocus(opener))
        {
            _map?.GrabFocus();
        }
        if (!_ui!.PushModal(modal))
        {
            throw new InvalidOperationException("R2 modal host rejected the single modal.");
        }
        _presentedModalId = modal.Id;
    }

    private static bool IsValidReturnFocus(Control? control) => control is not null &&
        control.IsInsideTree() &&
        control.IsVisibleInTree() &&
        control.FocusMode != Control.FocusModeEnum.None &&
        (control is not BaseButton button || !button.Disabled);

    private void HandleMapPrimary(
        RealtimePointerResolution resolution,
        CoreMapPoint worldPoint)
    {
        _draftCancelArmed = false;
        _clickCounters[resolution.Owner]++;
        if (resolution.Owner is RealtimePointerOwner.BlockingModal or
            RealtimePointerOwner.Hud or RealtimePointerOwner.Fatal)
        {
            return;
        }
        ConstructionSnapshot construction = _run!.GetSnapshot().Construction;
        if (IsCampaignReadOnlyShell &&
            (resolution.Owner == RealtimePointerOwner.DraftHandle ||
             _interaction!.Tool is RealtimeTool.BuildNode or
                 RealtimeTool.BuildLine or RealtimeTool.MoveDraft))
        {
            SetPointerFeedback(
                false,
                RealtimeInteractionReducer.CampaignEndedReadOnlyReason);
            Present();
            return;
        }
        if (resolution.Owner == RealtimePointerOwner.DraftHandle)
        {
            // A handle wins hit arbitration but never masquerades as empty
            // terrain or a terminal. R2 keeps editing bounded to explicit
            // point commands; selecting the handle therefore reports the next
            // safe action without appending/replacing a draft point.
            SetPointerFeedback(
                true,
                construction.NodeDraft is not null
                    ? "현재 변전소 초안입니다. 우클릭으로 취소하거나 공사 시작을 선택하세요."
                    : "현재 선로 경로점입니다. Backspace로 되돌리거나 빈 지형에 다음 점을 추가하세요.");
            Present();
            return;
        }
        if (resolution.Owner == RealtimePointerOwner.SelectionAction &&
            resolution.ResolvedId is string actionId &&
            actionId.StartsWith("ACTION:INSPECT:", StringComparison.Ordinal))
        {
            string selectedId = actionId["ACTION:INSPECT:".Length..];
            SetPointerFeedback(true, $"{DisplayAssetName(selectedId)} 상황 패널을 열었습니다.");
            _ = ApplyIntent(RealtimeR2Intent.Select(selectedId));
            return;
        }
        switch (_interaction!.Tool)
        {
            case RealtimeTool.BuildNode:
                if (!TrySelectedNodeClass(out string nodeClassId))
                {
                    SetPointerFeedback(false, "먼저 보이는 변전소 등급을 선택하세요.");
                    Present();
                    break;
                }
                _ = ApplyIntent(new RealtimeR2Intent(
                    RealtimeR2IntentKind.SetNodeDraft,
                    FirstId: nodeClassId,
                    Position: worldPoint));
                break;
            case RealtimeTool.BuildLine when construction.LineDraft is null &&
                resolution.ResolvedId is not null:
                if (!TrySelectedLinePlan(out CommercialCampaignLinePlanDefinition? plan))
                {
                    SetPointerFeedback(false, "먼저 보이는 선로 등급을 선택하세요.");
                    Present();
                    break;
                }
                _ = ApplyIntent(RealtimeR2Intent.StartLineDraft(
                    resolution.ResolvedId,
                    plan!.LineClassId,
                    plan.PoleClassId));
                break;
            case RealtimeTool.BuildLine when construction.LineDraft is { EndNodeId: null } &&
                resolution.Owner == RealtimePointerOwner.WorldCandidate &&
                resolution.ResolvedId is not null:
                _ = ApplyIntent(RealtimeR2Intent.FinishLineDraft(resolution.ResolvedId));
                break;
            case RealtimeTool.BuildLine when construction.LineDraft is { EndNodeId: null }:
                _ = ApplyIntent(RealtimeR2Intent.AddLinePoint(worldPoint));
                break;
            default:
                _ = ApplyIntent(RealtimeR2Intent.Select(
                    resolution.Owner == RealtimePointerOwner.WorldCandidate
                        ? resolution.ResolvedId
                        : null));
                break;
        }
    }

    private void HandleMapPointerMoved(
        RealtimePointerResolution resolution,
        CoreMapPoint worldPoint)
    {
        // Pointer feedback replaces the inline Esc confirmation. Disarm it at
        // the same boundary so a later Esc can never cancel a draft using a
        // warning that is no longer visible.
        _draftCancelArmed = false;
        _pointerPoint = worldPoint;
        if (_map?.IsPanning == true)
        {
            Present();
            return;
        }

        ConstructionSnapshot construction = _run!.GetSnapshot().Construction;
        if (IsCampaignReadOnlyShell &&
            (resolution.Owner == RealtimePointerOwner.DraftHandle ||
             _interaction!.Tool is RealtimeTool.BuildNode or
                 RealtimeTool.BuildLine or RealtimeTool.MoveDraft))
        {
            SetPointerFeedback(
                false,
                RealtimeInteractionReducer.CampaignEndedReadOnlyReason);
            Present();
            return;
        }
        if (resolution.Owner == RealtimePointerOwner.SelectionAction &&
            resolution.ResolvedId is string selectionActionId &&
            selectionActionId.StartsWith("ACTION:INSPECT:", StringComparison.Ordinal))
        {
            string selectedId = selectionActionId["ACTION:INSPECT:".Length..];
            SetPointerFeedback(
                true,
                $"{DisplayAssetName(selectedId)} 상황 패널 열기 · 클릭 또는 Enter로 엽니다.");
            Present();
            return;
        }
        switch (_interaction!.Tool)
        {
            case RealtimeTool.BuildNode:
                if (!TrySelectedNodeClass(out string nodeClassId))
                {
                    SetPointerFeedback(false, "먼저 하단에서 변전소 등급을 선택하세요.");
                    break;
                }
                NodePlacementPreview nodePreview = _run.PreviewNodePlacement(
                    nodeClassId,
                    worldPoint);
                SetPointerFeedback(
                    nodePreview.Accepted,
                    nodePreview.Accepted
                        ? nodePreview.RiskAreaIds.Count == 0
                            ? "배치 가능 · 클릭 또는 Enter로 초안을 놓습니다."
                            : $"배치 가능 · 위험구역 {nodePreview.RiskAreaIds.Count}곳과 겹칩니다."
                        : $"배치 불가 · {ConstructionErrorText(nodePreview.Error)}");
                break;
            case RealtimeTool.BuildLine:
                PreviewLinePointer(resolution, worldPoint, construction);
                break;
            case RealtimeTool.Analysis:
                SetPointerFeedback(
                    true,
                    resolution.ResolvedId is string analysisId
                        ? $"분석 후보 {DisplayAssetName(analysisId)} · " +
                          "클릭 또는 Enter로 엽니다."
                        : "빈 지형 · 공급 경로와 첫 병목을 겹쳐 보고 있습니다.");
                break;
            default:
                SetPointerFeedback(
                    true,
                    resolution.ResolvedId is string candidateId
                        ? $"선택 후보 {DisplayAssetName(candidateId)} · " +
                          "클릭 또는 Enter로 엽니다."
                        : "빈 지형 · 클릭하면 현재 선택을 해제합니다.");
                break;
        }
        Present();
    }

    private void PreviewLinePointer(
        RealtimePointerResolution resolution,
        CoreMapPoint worldPoint,
        ConstructionSnapshot construction)
    {
        if (!TrySelectedLinePlan(out CommercialCampaignLinePlanDefinition? plan))
        {
            SetPointerFeedback(false, "먼저 하단에서 선로 등급을 선택하세요.");
            return;
        }
        if (construction.LineDraft is null)
        {
            if (resolution.ResolvedId is not string startId ||
                resolution.Owner != RealtimePointerOwner.WorldCandidate)
            {
                SetPointerFeedback(false, "완공된 접속 설비에서 선로를 시작하세요.");
                return;
            }
            LineStartPreview preview = _run!.PreviewLineStart(
                startId,
                plan!.LineClassId,
                plan.PoleClassId);
            SetPointerFeedback(
                preview.Accepted,
                preview.Accepted
                    ? $"{DisplayAssetName(startId)}에서 선로 시작 가능 · " +
                      "클릭 또는 Enter로 확정합니다."
                    : $"시작 불가 · {ConstructionErrorText(preview.Error)}");
            return;
        }
        if (construction.LineDraft.EndNodeId is not null)
        {
            SetPointerFeedback(true, "선로 초안이 닫혔습니다. 하단에서 공사 시작을 확인하세요.");
            return;
        }
        if (resolution.Owner == RealtimePointerOwner.DraftHandle)
        {
            SetPointerFeedback(true, "현재 경로점 · Backspace로 마지막 단계를 되돌릴 수 있습니다.");
            return;
        }
        if (resolution.Owner == RealtimePointerOwner.WorldCandidate &&
            resolution.ResolvedId is string endId)
        {
            LineFinishPreview preview = _run!.PreviewLineFinish(endId);
            SetPointerFeedback(
                preview.Accepted,
                preview.Accepted
                    ? $"{DisplayAssetName(endId)}에 접속 가능 · " +
                      "클릭 또는 Enter로 경로를 닫습니다."
                    : $"접속 불가 · {ConstructionErrorText(preview.Error)}");
            return;
        }
        LinePointPreview pointPreview = _run!.PreviewLinePoint(worldPoint);
        SetPointerFeedback(
            pointPreview.Accepted,
            pointPreview.Accepted
                ? pointPreview.RiskAreaIds.Count == 0
                    ? "경로점 추가 가능 · 클릭 또는 Enter로 확정합니다."
                    : $"경로점 추가 가능 · 위험구역 {pointPreview.RiskAreaIds.Count}곳을 지납니다."
                : $"경로점 불가 · {ConstructionErrorText(pointPreview.Error)}");
    }

    private void HandleAction(string id)
    {
        _ = id switch
        {
            "ORDER_NODE" => ApplyIntent(new RealtimeR2Intent(RealtimeR2IntentKind.OrderNode)),
            "ORDER_LINE" => ApplyIntent(RealtimeR2Intent.OrderLine()),
            _ => ApplyIntent(RealtimeR2Intent.Select(id)),
        };
    }

    private void HandleModalAction(string modalId, string actionId)
    {
        RealtimeModalPresentation? modal = _latestPresentation?.Modal;
        if (modal is null ||
            !string.Equals(modal.Id, modalId, StringComparison.Ordinal) ||
            !modal.PrimaryAction.Enabled ||
            !modal.PrimaryAction.Visible ||
            !string.Equals(modal.PrimaryAction.Id, actionId, StringComparison.Ordinal))
        {
            return;
        }
        // Every production R2 modal action is deliberately a close/continue
        // operation. Destructive recovery/new-game/title actions are never
        // presented because no production handler implements those mutations.
        _ = ApplyIntent(RealtimeR2Intent.CloseModal(modalId));
    }

    private void HandleBuildTool(string id)
    {
        if (string.Equals(id, "TOOL:ANALYSIS", StringComparison.Ordinal))
        {
            _ = ApplyIntent(new RealtimeR2Intent(RealtimeR2IntentKind.ToggleAnalysis));
            return;
        }
        RealtimeTool? tool = id switch
        {
            "TOOL:INSPECT" => RealtimeTool.Inspect,
            _ when id.StartsWith("NODE:", StringComparison.Ordinal) => RealtimeTool.BuildNode,
            _ when id.StartsWith("LINE:", StringComparison.Ordinal) => RealtimeTool.BuildLine,
            _ => null,
        };
        if (tool.HasValue)
        {
            _ = ApplyIntent(tool is RealtimeTool.BuildNode or RealtimeTool.BuildLine
                ? RealtimeR2Intent.SelectBuildTool(tool.Value, id)
                : RealtimeR2Intent.SelectTool(tool.Value));
        }
    }

    private bool TrySelectedNodeClass(out string nodeClassId)
    {
        const string prefix = "NODE:";
        string? toolId = _interaction?.SelectedBuildToolId;
        nodeClassId = toolId is not null && toolId.StartsWith(prefix, StringComparison.Ordinal)
            ? toolId[prefix.Length..]
            : string.Empty;
        return nodeClassId.Length > 0 && _run!.GetSnapshot().Chapter.Content
            .AvailableNodeClassIds.Contains(nodeClassId, StringComparer.Ordinal);
    }

    private bool TrySelectedLinePlan(
        out CommercialCampaignLinePlanDefinition? selectedPlan)
    {
        string? toolId = _interaction?.SelectedBuildToolId;
        selectedPlan = _run!.GetSnapshot().Chapter.Content.AvailableLinePlans
            .SingleOrDefault(plan => string.Equals(
                toolId,
                $"LINE:{plan.LineClassId}:{plan.PoleClassId}",
                StringComparison.Ordinal));
        return selectedPlan is not null;
    }

    private void HandleSpeedRequested(RealtimeSimulationSpeed speed)
    {
        if (speed == RealtimeSimulationSpeed.Paused)
        {
            HandleShortcut(RealtimeInputCommand.TogglePause);
            return;
        }
        _ = ApplyIntent(RealtimeR2Intent.SetSpeed(speed));
    }

    private void ApplyMapInteractionRect(Rect2 rect)
    {
        if (_map is null || rect.Size.X <= 0 || rect.Size.Y <= 0)
        {
            return;
        }
        _map.AnchorLeft = 0;
        _map.AnchorTop = 0;
        _map.AnchorRight = 0;
        _map.AnchorBottom = 0;
        _map.Position = rect.Position;
        _map.Size = rect.Size;
        _map.ApplyLayout(_ui!.LayoutProfile);
    }

    private void HandleInputRequest(RealtimeInputRequest request)
    {
#if DEBUG
        _lastInputRequest = request;
#endif
        HandleShortcut(request.Command);
    }

    private void HandleShortcut(RealtimeInputCommand command)
    {
        bool menuCancelsAuthoritativeDraft =
            command == RealtimeInputCommand.ToggleBuildShelf &&
            !IsCampaignReadOnlyShell &&
            (_run!.GetSnapshot().Construction.NodeDraft is not null ||
             _run.GetSnapshot().Construction.LineDraft is not null);
        if (command != RealtimeInputCommand.CancelOrBack &&
            !menuCancelsAuthoritativeDraft)
        {
            _draftCancelArmed = false;
        }
        switch (command)
        {
            case RealtimeInputCommand.TogglePause:
                if (_interaction!.Simulation == RealtimeSimulationState.AutoPaused &&
                    _interaction.ActiveModalId is null)
                {
                    _ = ApplyIntent(RealtimeR2Intent.AcknowledgeAutoPause());
                }
                else
                {
                    _ = ApplyIntent(RealtimeR2Intent.SetPlayerPaused(
                        _interaction.Simulation == RealtimeSimulationState.Running));
                }
                break;
            case RealtimeInputCommand.SetNormalSpeed:
                _ = ApplyIntent(RealtimeR2Intent.SetSpeed(RealtimeSimulationSpeed.Normal));
                break;
            case RealtimeInputCommand.SetFastSpeed:
                _ = ApplyIntent(RealtimeR2Intent.SetSpeed(RealtimeSimulationSpeed.Fast));
                break;
            case RealtimeInputCommand.SetVeryFastSpeed:
                _ = ApplyIntent(RealtimeR2Intent.SetSpeed(RealtimeSimulationSpeed.VeryFast));
                break;
            case RealtimeInputCommand.ToggleAnalysis:
                _ = ApplyIntent(new RealtimeR2Intent(RealtimeR2IntentKind.ToggleAnalysis));
                break;
            case RealtimeInputCommand.ToggleBuildShelf:
                if (menuCancelsAuthoritativeDraft)
                {
                    // The top HUD says "건설 취소" while a Core-authoritative
                    // draft exists. Route it into the same explicit two-stage
                    // cancel flow as Esc; an impossible Inspect transition would
                    // be rejected by the draft lock and make the control lie.
                    HandleCancel();
                }
                else if (_interaction!.Tool is RealtimeTool.BuildNode or
                         RealtimeTool.BuildLine or RealtimeTool.MoveDraft)
                {
                    _ = ApplyIntent(RealtimeR2Intent.SelectTool(RealtimeTool.Inspect));
                }
                else
                {
                    _ = ApplyIntent(new RealtimeR2Intent(
                        _interaction.Surface == RealtimeSurface.Drawer
                            ? RealtimeR2IntentKind.CloseSurface
                            : RealtimeR2IntentKind.OpenSurface,
                        Surface: RealtimeSurface.Drawer));
                }
                break;
            case RealtimeInputCommand.CancelOrBack:
                HandleCancel();
                break;
            case RealtimeInputCommand.UndoDraftStep:
                HandleUndoDraftStep();
                break;
            case RealtimeInputCommand.CycleCandidatePrevious:
                _map?.CycleCandidate(-1);
                break;
            case RealtimeInputCommand.CycleCandidateNext:
                _map?.CycleCandidate(1);
                break;
            case RealtimeInputCommand.BeginPan:
                _map?.BeginPan();
                break;
            case RealtimeInputCommand.EndPan:
                _map?.EndPan();
                break;
            case RealtimeInputCommand.ConfirmOrSelect:
                _map?.ConfirmCurrentCandidate();
                break;
            case RealtimeInputCommand.TimelineHome:
                RouteTimelineNavigation(RealtimeTimelineNavigation.Home);
                break;
            case RealtimeInputCommand.TimelinePrevious:
                RouteTimelineNavigation(RealtimeTimelineNavigation.PreviousEvent);
                break;
            case RealtimeInputCommand.TimelineNext:
                RouteTimelineNavigation(RealtimeTimelineNavigation.NextEvent);
                break;
            case RealtimeInputCommand.SelectInspectTool:
                HandleBuildTool("TOOL:INSPECT");
                break;
            case RealtimeInputCommand.SelectFirstNodeTool:
                SelectBuildToolByPrefix("NODE:");
                break;
            case RealtimeInputCommand.SelectFirstLineTool:
                SelectBuildToolByPrefix("LINE:");
                break;
        }
    }

    private void SelectBuildToolByPrefix(string prefix)
    {
        RealtimeBuildToolPresentation[] choices = _latestPresentation!.BuildShelf.Tools
            .Where(item => item.Enabled && item.Id.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        if (choices.Length == 0)
        {
            RealtimeDraftToolLock? draftToolLock =
                RealtimeInteractionReducer.ResolveDraftToolLock(
                    _run!.GetSnapshot().Construction);
            if (draftToolLock is not null)
            {
                SetPointerFeedback(false, draftToolLock.RejectionReason);
                Present();
            }
            return;
        }
        int current = Array.FindIndex(choices, item => string.Equals(
            item.Id,
            _interaction!.SelectedBuildToolId,
            StringComparison.Ordinal));
        HandleBuildTool(choices[(current + 1) % choices.Length].Id);
    }

    private void RouteTimelineNavigation(RealtimeTimelineNavigation navigation)
    {
        // The priority-aware input router owns the physical key. A live rail
        // then owns semantic focus movement and emits exactly one controller
        // navigation request. Off-tree deterministic checks have no UI tree,
        // so they reduce the same typed navigation directly.
        if (_ui?.NavigateTimeline(navigation) != true)
        {
            HandleTimelineNavigation(navigation);
        }
    }

    private void HandleTimelineHorizonDelta(int delta)
    {
        RealtimeTimelineHorizonPreset[] presets =
        [
            RealtimeTimelineHorizonPreset.SixHours,
            RealtimeTimelineHorizonPreset.TwentyFourHours,
            RealtimeTimelineHorizonPreset.SevenDays,
        ];
        int current = Array.IndexOf(presets, _interaction!.TimelineHorizon);
        int next = Math.Clamp(current + Math.Sign(delta), 0, presets.Length - 1);
        ApplyTimelineState(
            _interaction.TimelineSelectedItemId,
            _interaction.SelectionId,
            null,
            presets[next]);
    }

    private void HandleTimelineItems(IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        string[] ordered = VisibleTimelineItems()
            .Where(item => ids.Contains(item.Id, StringComparer.Ordinal))
            .Select(item => item.Id)
            .ToArray();
        if (ordered.Length == 0)
        {
            return;
        }
        if (_timelineClusterIds.SequenceEqual(ordered, StringComparer.Ordinal))
        {
            _timelineClusterIndex = (_timelineClusterIndex + 1) % ordered.Length;
        }
        else
        {
            _timelineClusterIds = Array.AsReadOnly(ordered);
            _timelineClusterIndex = 0;
        }
        RealtimeTimelineItemPresentation item = VisibleTimelineItems().Single(candidate =>
            string.Equals(
                candidate.Id,
                _timelineClusterIds[_timelineClusterIndex],
                StringComparison.Ordinal));
        SelectTimelineItem(item);
    }

    private void HandleTimelineNavigation(RealtimeTimelineNavigation navigation)
    {
        RealtimeCampaignSnapshot snapshot = _run!.GetSnapshot();
        if (navigation == RealtimeTimelineNavigation.Home)
        {
            _timelineClusterIds = Array.Empty<string>();
            _timelineClusterIndex = 0;
            ApplyTimelineState(
                null,
                null,
                null,
                _interaction!.TimelineHorizon);
            return;
        }
        RealtimeTimelineItemPresentation[] items = VisibleTimelineItems();
        if (items.Length == 0)
        {
            ApplyTimelineState(
                null,
                null,
                null,
                _interaction!.TimelineHorizon);
            return;
        }
        int selectedIndex = Array.FindIndex(items, item => string.Equals(
            item.Id,
            _interaction!.TimelineSelectedItemId,
            StringComparison.Ordinal));
        int targetIndex;
        if (selectedIndex >= 0)
        {
            targetIndex = Math.Clamp(
                selectedIndex + (navigation == RealtimeTimelineNavigation.NextEvent ? 1 : -1),
                0,
                items.Length - 1);
            if (targetIndex == selectedIndex)
            {
                return;
            }
        }
        else if (navigation == RealtimeTimelineNavigation.NextEvent)
        {
            targetIndex = Array.FindIndex(items, item => item.StartMinute > snapshot.Minute);
            if (targetIndex < 0)
            {
                // "다음 사건" is a strict future jump when no marker is
                // selected. Completed/history markers remain available through
                // previous navigation and direct selection, but must never be
                // mislabeled as the next occurrence.
                return;
            }
        }
        else
        {
            targetIndex = Array.FindLastIndex(items, item => item.StartMinute <= snapshot.Minute);
            if (targetIndex < 0)
            {
                targetIndex = 0;
            }
        }
        _timelineClusterIds = Array.Empty<string>();
        _timelineClusterIndex = 0;
        SelectTimelineItem(items[targetIndex]);
    }

    private RealtimeTimelineItemPresentation[] VisibleTimelineItems()
    {
        RealtimeEventRailPresentation rail = _latestPresentation?.Rail ??
            throw new InvalidOperationException("Timeline presentation is unavailable.");
        return rail.Items
            .Where(item => item.Visibility != RealtimeTimelineVisibility.Hidden)
            .Where(item => item.StartMinute <= rail.HorizonEndMinute &&
                (item.EndMinute ?? item.StartMinute) >= rail.HorizonStartMinute)
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private void SelectTimelineItem(RealtimeTimelineItemPresentation item)
    {
        RealtimeTimelineTarget target = RealtimeSlicePresenter.ResolveTimelineTarget(
            _data!.BaseWorld,
            _run!.GetSnapshot(),
            _latestPresentation!.BaseForecast,
            _latestPresentation!.ComparisonDraftForecast,
            item.Id);
        ApplyTimelineState(
            item.Id,
            target.SubjectId,
            null,
            _interaction!.TimelineHorizon);
    }

    private RealtimeR2IntentResult ApplyTimelineState(
        string? markerId,
        string? subjectId,
        long? anchorMinute,
        RealtimeTimelineHorizonPreset horizon)
    {
        RealtimeR2IntentResult result = ApplyIntent(
            RealtimeR2Intent.SetTimelineMarker(
                markerId,
                subjectId,
                anchorMinute,
                horizon));
        if (result.Accepted && !string.Equals(
                result.BeforeCanonicalStateSha256,
                result.AfterCanonicalStateSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Timeline navigation changed authoritative Core state.");
        }
        return result;
    }

    private void HandleCancel()
    {
        ConstructionSnapshot construction = _run!.GetSnapshot().Construction;
        if (_interaction!.ActiveModalId is string modalId)
        {
            _ = ApplyIntent(RealtimeR2Intent.CloseModal(modalId));
        }
        else if (!IsCampaignReadOnlyShell &&
                 (construction.NodeDraft is not null || construction.LineDraft is not null))
        {
            if (!_draftCancelArmed)
            {
                _draftCancelArmed = true;
                RealtimeInteractionReduction revealGuidance =
                    RealtimeInteractionReducer.Reduce(
                        _interaction,
                        new RealtimeR2Intent(
                            RealtimeR2IntentKind.OpenSurface,
                            Surface: RealtimeSurface.Drawer),
                        construction);
                if (revealGuidance.Accepted)
                {
                    _interaction = revealGuidance.State;
                }
                SetPointerFeedback(
                    false,
                    "초안을 모두 취소하려면 B 또는 Esc를 한 번 더 누르세요. " +
                    "Backspace는 마지막 단계만 되돌립니다.");
                Present();
            }
            else
            {
                _draftCancelArmed = false;
                RealtimeR2IntentResult cancellation = ApplyIntent(new RealtimeR2Intent(
                    construction.NodeDraft is not null
                        ? RealtimeR2IntentKind.CancelNodeDraft
                        : RealtimeR2IntentKind.CancelLineDraft));
                if (cancellation.Accepted)
                {
                    // The HUD action is named "건설 취소". Once Core accepts
                    // that cancellation, complete the promised transition by
                    // leaving the construction tool and returning to the world.
                    _ = ApplyIntent(RealtimeR2Intent.SelectTool(RealtimeTool.Inspect));
                }
            }
        }
        else if (_interaction.Tool != RealtimeTool.Inspect)
        {
            _ = ApplyIntent(RealtimeR2Intent.RestoreInspectTool());
        }
        else if (_interaction.Surface != RealtimeSurface.World)
        {
            _ = ApplyIntent(new RealtimeR2Intent(
                RealtimeR2IntentKind.CloseSurface,
                Surface: _interaction.Surface));
        }
        else if (_interaction.SelectionId is not null)
        {
            _ = ApplyIntent(RealtimeR2Intent.Select(null));
        }
        else if (_interaction.Simulation == RealtimeSimulationState.Running)
        {
            _ = ApplyIntent(RealtimeR2Intent.SetPlayerPaused(true));
        }
        else
        {
            // PlayerPaused/AutoPaused/Ended already expose their typed reason
            // in the HUD. Esc must not silently resume or override it.
        }
    }

    private void HandleUndoDraftStep()
    {
        ConstructionSnapshot construction = _run!.GetSnapshot().Construction;
        _draftCancelArmed = false;
        if (construction.NodeDraft is not null)
        {
            _ = ApplyIntent(new RealtimeR2Intent(RealtimeR2IntentKind.CancelNodeDraft));
        }
        else if (construction.LineDraft is { EndNodeId: not null } ||
                 construction.LineDraft?.IntermediatePoints.Count > 0)
        {
            _ = ApplyIntent(new RealtimeR2Intent(RealtimeR2IntentKind.UndoLinePoint));
        }
        else if (construction.LineDraft is not null)
        {
            _ = ApplyIntent(new RealtimeR2Intent(RealtimeR2IntentKind.CancelLineDraft));
        }
    }

    private static (RealtimeCommand? Command, string? Error) CoreCommand(
        RealtimeR2Intent intent)
    {
        try
        {
            return intent.Kind switch
            {
                RealtimeR2IntentKind.SetNodeDraft when intent.FirstId is not null &&
                    intent.Position.HasValue =>
                    (RealtimeCommand.SetNodeDraft(intent.FirstId, intent.Position.Value), null),
                RealtimeR2IntentKind.CancelNodeDraft =>
                    (RealtimeCommand.CancelNodeDraft(), null),
                RealtimeR2IntentKind.OrderNode => (RealtimeCommand.OrderNode(), null),
                RealtimeR2IntentKind.StartLineDraft when intent.FirstId is not null &&
                    intent.SecondId is not null && intent.ThirdId is not null =>
                    (RealtimeCommand.StartLineDraft(
                        intent.FirstId,
                        intent.SecondId,
                        intent.ThirdId), null),
                RealtimeR2IntentKind.AddLinePoint when intent.Position.HasValue =>
                    (RealtimeCommand.AddLinePoint(intent.Position.Value), null),
                RealtimeR2IntentKind.MoveLinePoint when intent.Position.HasValue &&
                    intent.PointIndex.HasValue =>
                    (RealtimeCommand.MoveLinePoint(
                        intent.PointIndex.Value,
                        intent.Position.Value), null),
                RealtimeR2IntentKind.UndoLinePoint =>
                    (RealtimeCommand.UndoLinePoint(), null),
                RealtimeR2IntentKind.FinishLineDraft when intent.FirstId is not null =>
                    (RealtimeCommand.FinishLineDraft(intent.FirstId), null),
                RealtimeR2IntentKind.CancelLineDraft =>
                    (RealtimeCommand.CancelLineDraft(), null),
                RealtimeR2IntentKind.OrderLine => (RealtimeCommand.OrderLine(), null),
                RealtimeR2IntentKind.SetPromiseDecision when intent.PromiseDecision.HasValue =>
                    (RealtimeCommand.SetPromiseDecision(intent.PromiseDecision.Value), null),
                RealtimeR2IntentKind.SetNodeDraft or
                    RealtimeR2IntentKind.StartLineDraft or
                    RealtimeR2IntentKind.AddLinePoint or
                    RealtimeR2IntentKind.MoveLinePoint or
                    RealtimeR2IntentKind.FinishLineDraft or
                    RealtimeR2IntentKind.SetPromiseDecision =>
                    (null, "공사 입력에 필요한 정보가 부족합니다."),
                _ => (null, null),
            };
        }
        catch (ArgumentException)
        {
            return (null, "공사 입력 형식을 확인할 수 없습니다.");
        }
    }

    private static bool IsConstructionIntent(RealtimeR2IntentKind kind) => kind is
        RealtimeR2IntentKind.SetNodeDraft or
        RealtimeR2IntentKind.CancelNodeDraft or
        RealtimeR2IntentKind.OrderNode or
        RealtimeR2IntentKind.StartLineDraft or
        RealtimeR2IntentKind.AddLinePoint or
        RealtimeR2IntentKind.MoveLinePoint or
        RealtimeR2IntentKind.UndoLinePoint or
        RealtimeR2IntentKind.FinishLineDraft or
        RealtimeR2IntentKind.CancelLineDraft or
        RealtimeR2IntentKind.OrderLine;

    private static bool IsComparisonMarker(string? id) => id is not null &&
        (id.StartsWith("DRAFT_FORECAST:", StringComparison.Ordinal) ||
         id.StartsWith("DRAFT_THERMAL:", StringComparison.Ordinal));

    private void SetPointerFeedback(bool accepted, string message)
    {
        _pointerAccepted = accepted;
        _pointerMessage = message;
    }

    private string DisplayAssetName(string assetId)
    {
        EnsureBootstrapped(requirePresentation: false);
        return RealtimeSlicePresenter.AssetDisplayName(
            _data!.BaseWorld,
            _run!.GetSnapshot(),
            assetId);
    }

    private static string ConstructionFeedback(
        RealtimeR2IntentKind kind,
        RealtimeCommandResult result)
    {
        if (!result.Accepted)
        {
            string reason = result.ConstructionError.HasValue
                ? ConstructionErrorText(result.ConstructionError)
                : RealtimeRunErrorText(result.Error);
            return $"공사 입력을 처리하지 못했습니다. {reason}";
        }
        return kind switch
        {
            RealtimeR2IntentKind.SetNodeDraft => "변전소 초안을 배치했습니다.",
            RealtimeR2IntentKind.CancelNodeDraft => "변전소 초안을 취소했습니다.",
            RealtimeR2IntentKind.OrderNode or RealtimeR2IntentKind.OrderLine
                when result.Snapshot.Construction.ActiveConstruction is
                    ActiveConstructionSnapshot project =>
                $"공사를 승인했습니다. {TimeText(project.CompletionMinute)}에 완공됩니다.",
            RealtimeR2IntentKind.StartLineDraft => "선로 시작점을 선택했습니다.",
            RealtimeR2IntentKind.AddLinePoint => "선로 경로점을 추가했습니다.",
            RealtimeR2IntentKind.MoveLinePoint => "선로 경로점을 이동했습니다.",
            RealtimeR2IntentKind.UndoLinePoint => "마지막 선로 단계를 되돌렸습니다.",
            RealtimeR2IntentKind.FinishLineDraft => "선로 끝점을 연결했습니다.",
            RealtimeR2IntentKind.CancelLineDraft => "선로 초안을 취소했습니다.",
            _ => "공사 입력을 승인했습니다.",
        };
    }

    internal static string ConstructionErrorText(ConstructionError? error) => error switch
    {
        null => "알 수 없는 공간 규칙 오류입니다.",
        ConstructionError.WrongPhase => "지금은 이 공사 단계를 실행할 수 없습니다.",
        ConstructionError.UnknownNodeClass or
        ConstructionError.InvalidNodeClass or
        ConstructionError.UnknownLineClass or
        ConstructionError.UnknownPoleClass or
        ConstructionError.InvalidPoleClass => "선택한 공사 등급을 사용할 수 없습니다.",
        ConstructionError.OutsideBounds => "설비 전체가 지도 안에 들어오도록 옮기세요.",
        ConstructionError.WaterFootprint => "물 위에는 설비를 놓을 수 없습니다.",
        ConstructionError.BuildingFootprint => "건물 점유영역을 피하세요.",
        ConstructionError.PositionOccupied => "다른 설비와 겹치지 않도록 간격을 두세요.",
        ConstructionError.ExistingLineTouch => "기존 선로와 닿지 않는 위치를 고르세요.",
        ConstructionError.EndpointNotFound => "연결할 접속 설비가 없습니다.",
        ConstructionError.EndpointNotCommissioned => "완공된 접속 설비만 연결할 수 있습니다.",
        ConstructionError.SameEndpoint => "시작점과 다른 접속 설비를 선택하세요.",
        ConstructionError.ConnectionLimit => "이 설비의 접속 회선 한도를 넘습니다.",
        ConstructionError.SpanTooLong => "허용 경간을 넘습니다. 중간 경로점을 추가하세요.",
        ConstructionError.ZeroLengthSegment => "같은 위치에 경로점을 연속으로 둘 수 없습니다.",
        ConstructionError.ThirdNodeTouch => "연결 대상이 아닌 다른 설비와 닿습니다.",
        ConstructionError.DuplicateSegment => "같은 접속점을 잇는 선로가 이미 있습니다.",
        ConstructionError.CollinearOverlap => "기존 선로와 같은 방향으로 포개집니다.",
        ConstructionError.BuildingCrossing => "선로가 건물을 가로지릅니다.",
        ConstructionError.DraftIncomplete => "다른 접속 설비까지 경로를 이어야 합니다.",
        ConstructionError.NothingToUndo => "되돌릴 경로점이 없습니다.",
        ConstructionError.InvalidPointIndex => "옮길 경로점을 찾지 못했습니다.",
        ConstructionError.ArithmeticOverflow => "좌표나 견적이 계산 범위를 벗어났습니다.",
        ConstructionError.InvalidCompletion => "완공 결과가 공간 규칙을 만족하지 않습니다.",
        _ => throw new ArgumentOutOfRangeException(nameof(error)),
    };

    internal static string RealtimeRunErrorText(RealtimeRunError? error) => error switch
    {
        null => "입력 원인을 확인하지 못했습니다.",
        RealtimeRunError.WrongState => "현재 운영 상태에서는 실행할 수 없습니다.",
        RealtimeRunError.InvalidCommandShape => "입력 순서가 올바르지 않습니다.",
        RealtimeRunError.ToolUnavailable => "이 임무에서 사용할 수 없는 공사입니다.",
        RealtimeRunError.ConstructionRejected => "공간·공사 규칙을 만족하지 않습니다.",
        RealtimeRunError.InsufficientCash => "운영 자금이 부족합니다.",
        RealtimeRunError.PromiseUnavailable => "지금은 운영 약속을 선택할 수 없습니다.",
        RealtimeRunError.PromiseDeadlinePassed => "운영 약속 선택 시간이 지났습니다.",
        RealtimeRunError.ClockMismatch => "시각이 바뀌었습니다. 현재 상태를 다시 확인하세요.",
        RealtimeRunError.SequenceMismatch => "다른 입력이 먼저 처리되었습니다. 다시 시도하세요.",
        RealtimeRunError.TimeInPast => "지난 시각에는 공사 입력을 적용할 수 없습니다.",
        RealtimeRunError.CommandLimit => "이 운영 기록에 더 많은 입력을 저장할 수 없습니다.",
        RealtimeRunError.ArithmeticOverflow => "비용이나 시각이 계산 범위를 벗어났습니다.",
        _ => throw new ArgumentOutOfRangeException(nameof(error)),
    };

    private static string TimeText(long minute)
    {
        long day = checked(minute / (24 * 60) + 1);
        long minuteOfDay = minute % (24 * 60);
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{day}일 {minuteOfDay / 60:00}:{minuteOfDay % 60:00}");
    }

    private void SynchronizeFramePause(
        RealtimeInteractionState before,
        RealtimeInteractionState after)
    {
        if (before.Simulation == after.Simulation)
        {
            return;
        }
        if (after.Simulation == RealtimeSimulationState.Running)
        {
            _frame!.Resume();
        }
        else
        {
            _frame!.Pause();
        }
    }

    private RealtimeR2IntentResult IntentResult(
        bool accepted,
        string? error,
        RealtimeCommandResult? commandResult,
        string beforeHash,
        long beforeMinute,
        long beforeSequence,
        int beforeCount,
        long beforeRevision) => new(
        accepted,
        error,
        commandResult,
        beforeHash,
        _run!.GetCanonicalStateSha256(),
        beforeMinute,
        _run.GetSnapshot().Minute,
        beforeSequence,
        NextCommandSequence,
        _run.AcceptedCommands.Count - beforeCount,
        _presentationRevision - beforeRevision);

    private RealtimeR2FrameResult FrameResult(
        RealtimeFrameAdvanceResult? frame,
        IReadOnlyList<RealtimeTransition> transitions,
        long requestedFrames,
        long consumedFrames,
        int framesPerSecond,
        long beforeRevision) => new(
        frame,
        _run!.GetSnapshot(),
        Array.AsReadOnly(transitions.ToArray()),
        requestedFrames,
        consumedFrames,
        framesPerSecond,
        _wallClockRemainderUnits,
        _presentationRevision - beforeRevision,
        FrozenFrameDebt());

    private IReadOnlyList<RealtimeR2PendingFrameDebt> FrozenFrameDebt() =>
        Array.AsReadOnly(_retainedFrameDebt.Select(item =>
            new RealtimeR2PendingFrameDebt(
                item.FrameCount,
                item.FramesPerSecond,
                item.SpeedMultiplier)).ToArray());

    private void ApplyLogicalCanvas()
    {
        Window window = GetWindow();
        _priorContentScaleSize = window.ContentScaleSize;
        _priorContentScaleMode = window.ContentScaleMode;
        _priorContentScaleAspect = window.ContentScaleAspect;
        window.ContentScaleSize = RequiredLogicalCanvas;
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
        _logicalCanvasApplied = true;
    }

    private long NextCommandSequence => _run!.AcceptedCommands.Count + 1L;

    private bool IsCampaignReadOnlyShell =>
        _interaction?.Simulation == RealtimeSimulationState.Ended ||
        _run?.GetSnapshot().CampaignComplete == true;

    private void EnsureBootstrapped(bool requirePresentation = true)
    {
        if (_run is null || _frame is null || _interaction is null ||
            requirePresentation && _latestPresentation is null)
        {
            throw new InvalidOperationException("Realtime R2 slice is not bootstrapped.");
        }
    }

    private sealed class PendingFrameBatch(
        long frameCount,
        int framesPerSecond,
        int speedMultiplier)
    {
        internal long FrameCount { get; set; } = frameCount;
        internal int FramesPerSecond { get; } = framesPerSecond;
        internal int SpeedMultiplier { get; } = speedMultiplier;
    }

#if DEBUG
    internal void BootstrapForSmoke() => Bootstrap();

    internal void SetSpeedForSmoke(RealtimeSimulationSpeed speed)
    {
        RealtimeR2IntentResult result = ApplyIntent(RealtimeR2Intent.SetSpeed(speed));
        if (!result.Accepted)
        {
            throw new InvalidOperationException(result.Error);
        }
    }

    internal void SetPlayerPausedForSmoke(bool paused)
    {
        RealtimeR2IntentResult result = ApplyIntent(
            RealtimeR2Intent.SetPlayerPaused(paused));
        if (!result.Accepted)
        {
            throw new InvalidOperationException(result.Error);
        }
    }

    internal RealtimeR2FrameResult InjectFramesForSmoke(
        long frameCount,
        int framesPerSecond)
    {
        return InjectExactFrames(frameCount, framesPerSecond);
    }

    internal RealtimeR2FrameResult InjectElapsedNanosecondsForSmoke(
        long elapsedNanoseconds) => InjectElapsedNanoseconds(elapsedNanoseconds);

    internal RealtimeR2FrameResult InjectElapsedSecondsForSmoke(double elapsedSeconds) =>
        InjectElapsedSeconds(elapsedSeconds);

    internal void RequestHudSpeedForSmoke(RealtimeSimulationSpeed speed) =>
        HandleSpeedRequested(speed);

    internal void NavigateTimelineForSmoke(RealtimeTimelineNavigation navigation) =>
        HandleTimelineNavigation(navigation);

    internal void AdjustTimelineHorizonForSmoke(int delta) =>
        HandleTimelineHorizonDelta(delta);

    internal void ChooseTimelineClusterForSmoke(IReadOnlyList<string> itemIds) =>
        HandleTimelineItems(itemIds);

    internal RealtimeR2AdvanceResult AdvanceToForSmoke(long targetMinute)
    {
        EnsureBootstrapped();
        long beforeRevision = _presentationRevision;
        RealtimeAdvanceResult result = _run!.AdvanceTo(targetMinute);
        CollectTransitions(result.Transitions);
        Present();
        return new RealtimeR2AdvanceResult(
            result,
            _presentationRevision - beforeRevision);
    }

    internal RealtimeR2IntentResult ApplyIntentForSmoke(RealtimeR2Intent intent) =>
        ApplyIntent(intent);

    internal RealtimePointerProbe CreatePointerProbeForSmoke(
        RealtimeSmokePointerProbeKind kind)
    {
        EnsureBootstrapped();
        RealtimeSmokePointerPoint worldPoint = _smokeBoundaryFacts!.PointerPoints.Single(item =>
            string.Equals(
                item.Id,
                kind == RealtimeSmokePointerProbeKind.Empty ? "EMPTY" : "WORLD",
                StringComparison.Ordinal));
        string worldId = _run!.GetSnapshot().Construction.World.Nodes
            .OrderBy(item => item.NodeId, StringComparer.Ordinal)
            .First().NodeId;
        RealtimeMapCandidate[] candidates = kind switch
        {
            RealtimeSmokePointerProbeKind.Empty => Array.Empty<RealtimeMapCandidate>(),
            RealtimeSmokePointerProbeKind.Draft =>
            [new RealtimeMapCandidate(
                "DRAFT_HANDLE",
                RealtimeMapCandidateKind.DraftHandle,
                RealtimePointerOwner.DraftHandle,
                0)],
            RealtimeSmokePointerProbeKind.SelectionAction =>
            [new RealtimeMapCandidate(
                "SELECTION_ACTION",
                RealtimeMapCandidateKind.SelectionAction,
                RealtimePointerOwner.SelectionAction,
                0)],
            _ =>
            [new RealtimeMapCandidate(
                worldId,
                RealtimeMapCandidateKind.Node,
                RealtimePointerOwner.WorldCandidate,
                0)],
        };
        return new RealtimePointerProbe(
            kind.ToString().ToUpperInvariant(),
            worldPoint.WorldPoint,
            Array.AsReadOnly(candidates),
            HudHit: kind == RealtimeSmokePointerProbeKind.Hud,
            BlockingModalHit: kind == RealtimeSmokePointerProbeKind.Modal,
            FatalHit: kind == RealtimeSmokePointerProbeKind.Fatal,
            OverlayVisible: kind == RealtimeSmokePointerProbeKind.Overlay,
            WeatherVisible: kind == RealtimeSmokePointerProbeKind.Weather);
    }

    internal RealtimePointerProbeResult RoutePointerForSmoke(RealtimePointerProbe probe)
    {
        EnsureBootstrapped();
        int beforeCommands = _run!.AcceptedCommands.Count;
        long beforeRevision = _presentationRevision;
        RealtimePointerResolution resolution = RealtimePointerOwnerResolver.Resolve(probe);
        _clickCounters[resolution.Owner]++;
        if (resolution.Owner == RealtimePointerOwner.WorldCandidate)
        {
            _ = ApplyIntent(RealtimeR2Intent.Select(resolution.ResolvedId));
        }
        return new RealtimePointerProbeResult(
            resolution,
            beforeCommands,
            _run.AcceptedCommands.Count,
            beforeRevision,
            _presentationRevision,
            _interaction!.SelectionId,
            FrozenClickCounters());
    }

    internal void ResetPointerClickCountersForSmoke()
    {
        foreach (RealtimePointerOwner owner in _clickCounters.Keys.ToArray())
        {
            _clickCounters[owner] = 0;
        }
    }

    internal RealtimeR2LayoutFacts LayoutFactsForSmoke()
    {
        Vector2I physical = IsInsideTree()
            ? DisplayServer.WindowGetSize()
            : Vector2I.Zero;
        Vector2I current = IsInsideTree()
            ? GetWindow().ContentScaleSize
            : RequiredLogicalCanvas;
        return new RealtimeR2LayoutFacts(
            physical,
            _priorContentScaleSize,
            current,
            RequiredLogicalCanvas,
            _priorContentScaleMode,
            IsInsideTree()
                ? GetWindow().ContentScaleMode
                : Window.ContentScaleModeEnum.CanvasItems,
            _priorContentScaleAspect,
            IsInsideTree()
                ? GetWindow().ContentScaleAspect
                : Window.ContentScaleAspectEnum.Expand,
            _map?.CameraCenter ?? Vector2.Zero);
    }

    internal RealtimeMapCameraSnapshot CaptureCameraForSmoke() =>
        _map?.CaptureCamera() ?? new RealtimeMapCameraSnapshot(Vector2.Zero, 0);

    internal void RestoreCameraForSmoke(RealtimeMapCameraSnapshot camera) =>
        _map?.RestoreCamera(camera);

    internal RealtimeCampaignSnapshot CoreSnapshot =>
        _run?.GetSnapshot() ?? throw new InvalidOperationException("Not bootstrapped.");
    internal CommercialWorldDefinition DisplayWorldForSmoke =>
        _data?.BaseWorld ?? throw new InvalidOperationException("Not bootstrapped.");
    internal RealtimeWorldDefinition RealtimeWorldForSmoke =>
        _data?.World ?? throw new InvalidOperationException("Not bootstrapped.");
    internal string CanonicalStateSha256 =>
        _run?.GetCanonicalStateSha256() ?? throw new InvalidOperationException("Not bootstrapped.");
    internal RealtimeFrameAccumulatorSnapshot AccumulatorSnapshot =>
        _frame?.GetSnapshot() ?? throw new InvalidOperationException("Not bootstrapped.");
    internal RealtimeInteractionState InteractionState =>
        _interaction ?? throw new InvalidOperationException("Not bootstrapped.");
    internal int AcceptedCommandCount => _run?.AcceptedCommands.Count ?? 0;
    internal long CommandSequence => _run is null ? 1 : NextCommandSequence;
    internal long CurrentMinute => CoreSnapshot.Minute;
    internal long PresentationRevision => _presentationRevision;
    internal RealtimeSlicePresentation LatestPresentation =>
        _latestPresentation ?? throw new InvalidOperationException("Not presented.");
    internal IReadOnlyList<RealtimeTransition> EmittedTransitions =>
        Array.AsReadOnly(_emittedTransitions.ToArray());
    internal IReadOnlyDictionary<RealtimePointerOwner, int> PointerClickCounters =>
        FrozenClickCounters();
    internal IReadOnlyList<RealtimeR2PendingFrameDebt> RetainedFrameDebt =>
        FrozenFrameDebt();
    internal Vector2 CameraCenter => _map?.CameraCenter ?? Vector2.Zero;
    internal RealtimeR2InputOwnershipFacts InputOwnershipFacts => new(
        _lastInputRequest,
        _map?.IsPanning ?? false,
        FrozenClickCounters());
    internal RealtimeR2TimelineChooserFacts TimelineChooserFacts => new(
        Array.AsReadOnly(VisibleTimelineItems().Select(item => item.Id).ToArray()),
        _timelineClusterIds,
        _timelineClusterIndex,
        _interaction?.TimelineSelectedItemId,
        _interaction?.SelectionId);
    internal RealtimeSmokeLinePlan SmokeLinePlan =>
        _smokeLinePlan ?? throw new InvalidOperationException("Not bootstrapped.");
    internal RealtimeSmokeBoundaryFacts SmokeBoundaryFacts =>
        _smokeBoundaryFacts ?? throw new InvalidOperationException("Not bootstrapped.");

    private IReadOnlyDictionary<RealtimePointerOwner, int> FrozenClickCounters() =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<RealtimePointerOwner, int>(
            new Dictionary<RealtimePointerOwner, int>(_clickCounters));

    private static RealtimeSmokeLinePlan BuildSmokeLinePlan(RealtimeSliceData data)
    {
        CommercialWorldDefinition initialWorld = CommercialCampaignLoader.BuildInitialWorld(
            data.World.Network,
            data.Campaign.InitialSeed);
        RealtimeScheduledEventDefinition target = data.Campaign.Chapters[0].ScheduledEvents
            .OrderBy(item => item.StartOffsetMinutes)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .First();
        long targetMinute = checked(
            data.Campaign.InitialSeed.StartMinute +
            data.Campaign.Chapters[0].Content.TimeAdvanceBeforeChapterMinutes +
            target.StartOffsetMinutes);
        CommercialCampaignLinePlanDefinition linePlan = data.Campaign.Chapters[0]
            .Content.AvailableLinePlans.Single();
        string targetLoadId = target.OperatingProfile.Loads[0].LoadId;
        string targetLoadNodeId = initialWorld.Loads.Single(item =>
            string.Equals(item.LoadId, targetLoadId, StringComparison.Ordinal)).NodeId;
        HashSet<string> loadComponent = Component(initialWorld, targetLoadNodeId);
        HashSet<string> sourceComponent = initialWorld.Sources
            .SelectMany(item => Component(initialWorld, item.NodeId))
            .ToHashSet(StringComparer.Ordinal);

        (string Start, string End, ConstructionQuote Quote)? selected = null;
        foreach (SpatialNodeDefinition start in initialWorld.Nodes
                     .Where(item => item.Commissioned && sourceComponent.Contains(item.NodeId))
                     .OrderBy(item => item.NodeId, StringComparer.Ordinal))
        {
            foreach (SpatialNodeDefinition end in initialWorld.Nodes
                         .Where(item => item.Commissioned &&
                             loadComponent.Contains(item.NodeId) &&
                             !string.Equals(item.NodeId, start.NodeId,
                                 StringComparison.Ordinal))
                         .OrderBy(item => item.NodeId, StringComparer.Ordinal))
            {
                var probe = new RealtimeConstructionSession(initialWorld.ToSpatialWorld(), 0);
                if (!probe.StartLineDraft(
                        start.NodeId,
                        linePlan.LineClassId,
                        linePlan.PoleClassId).Accepted ||
                    !probe.FinishLineDraft(end.NodeId).Accepted)
                {
                    continue;
                }
                ConstructionQuote quote = probe.PreviewLineOrder();
                if (!quote.Accepted || quote.BuildMinutes is not > 0 ||
                    quote.BuildMinutes.Value > targetMinute -
                        data.Campaign.InitialSeed.StartMinute)
                {
                    continue;
                }
                selected = (start.NodeId, end.NodeId, quote);
                break;
            }
            if (selected.HasValue)
            {
                break;
            }
        }
        if (!selected.HasValue)
        {
            throw new InvalidOperationException(
                "The embedded R1 fixture has no direct just-in-time smoke line plan.");
        }

        long buildMinutes = selected.Value.Quote.BuildMinutes!.Value;
        long orderMinute = checked(targetMinute - buildMinutes);
        var construction = new RealtimeConstructionSession(
            initialWorld.ToSpatialWorld(),
            orderMinute);
        RequireAccepted(construction.StartLineDraft(
            selected.Value.Start,
            linePlan.LineClassId,
            linePlan.PoleClassId));
        RequireAccepted(construction.FinishLineDraft(selected.Value.End));
        RequireAccepted(construction.OrderLine());
        ActiveConstructionSnapshot active = construction.GetSnapshot().ActiveConstruction ??
            throw new InvalidOperationException("Smoke line order created no active project.");
        ConstructionCommandResult second = construction.OrderLine();
        return new RealtimeSmokeLinePlan(
            selected.Value.Start,
            selected.Value.End,
            linePlan.LineClassId,
            linePlan.PoleClassId,
            Array.Empty<CoreMapPoint>(),
            orderMinute,
            buildMinutes,
            active.CompletionMinute,
            active.NodeIds,
            active.EdgeIds,
            RealtimeRunError.ConstructionRejected,
            second.Error,
            Array.AsReadOnly(new[]
            {
                RealtimeR2Intent.StartLineDraft(
                    selected.Value.Start,
                    linePlan.LineClassId,
                    linePlan.PoleClassId),
                RealtimeR2Intent.FinishLineDraft(selected.Value.End),
                RealtimeR2Intent.OrderLine(),
            }));
    }

    private static RealtimeSmokeBoundaryFacts BuildSmokeBoundaryFacts(
        RealtimeCampaignSnapshot snapshot,
        RealtimeSmokeLinePlan linePlan)
    {
        RealtimeSmokeEventBoundary[] events = snapshot.Forecast.Events
            .Select(item => new RealtimeSmokeEventBoundary(
                item.EventId,
                item.StartMinute,
                item.EndMinute,
                snapshot.Chapter.ScheduledEvents.Single(scheduled => string.Equals(
                    scheduled.EventId,
                    item.EventId,
                    StringComparison.Ordinal)).Priority))
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .ToArray();
        RealtimeThermalTransition[] transitions = snapshot.Forecast.Events
            .SelectMany(item => item.TemporalProjection.Transitions)
            .Distinct()
            .ToArray();
        RealtimeSmokeThermalBoundary[] thermal = transitions
            .GroupBy(item => (item.AssetId, item.AssetKind))
            .Select(group => new RealtimeSmokeThermalBoundary(
                group.Key.AssetId,
                group.Key.AssetKind,
                FirstMinute(group, RealtimeThermalTransitionKind.EmergencyEntered),
                FirstMinute(group, RealtimeThermalTransitionKind.ProtectiveTrip),
                FirstMinute(group, RealtimeThermalTransitionKind.Recovered)))
            .Where(item => item.EmergencyStartMinute.HasValue || item.TripMinute.HasValue ||
                item.RecoveryMinute.HasValue)
            .OrderBy(item => item.AssetId, StringComparer.Ordinal)
            .ToArray();
        SpatialNodeDefinition worldNode = snapshot.Construction.World.Nodes
            .OrderBy(item => item.NodeId, StringComparer.Ordinal)
            .First();
        MapBounds bounds = snapshot.Construction.World.Bounds;
        RealtimeSmokePointerPoint[] points =
        [
            new("WORLD", worldNode.Position),
            new("EMPTY", new CoreMapPoint(bounds.MinXUnit + 1, bounds.MinYUnit + 1)),
        ];
        return new RealtimeSmokeBoundaryFacts(
            Array.AsReadOnly(events),
            linePlan.ExpectedCompletionMinute,
            Array.AsReadOnly(thermal),
            Array.AsReadOnly(points));
    }

    private static HashSet<string> Component(
        CommercialWorldDefinition world,
        string startNodeId)
    {
        var adjacency = world.Nodes.ToDictionary(
            item => item.NodeId,
            _ => new List<string>(),
            StringComparer.Ordinal);
        foreach (SpatialEdgeDefinition edge in world.Edges.Where(item => item.Commissioned))
        {
            adjacency[edge.FromNodeId].Add(edge.ToNodeId);
            adjacency[edge.ToNodeId].Add(edge.FromNodeId);
        }
        var seen = new HashSet<string>(StringComparer.Ordinal) { startNodeId };
        var queue = new Queue<string>();
        queue.Enqueue(startNodeId);
        while (queue.TryDequeue(out string? nodeId))
        {
            foreach (string neighbor in adjacency[nodeId])
            {
                if (seen.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }
        return seen;
    }

    private static long? FirstMinute(
        IEnumerable<RealtimeThermalTransition> transitions,
        RealtimeThermalTransitionKind kind) => transitions
        .Where(item => item.Kind == kind)
        .Select(item => (long?)item.Minute)
        .OrderBy(item => item)
        .FirstOrDefault();

    private static void RequireAccepted(ConstructionCommandResult result)
    {
        if (!result.Accepted)
        {
            throw new InvalidOperationException(
                $"Fixture-derived smoke construction was rejected: {result.Error}");
        }
    }
#endif
}
