using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;
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

internal enum RealtimeBuildToolFamily
{
    Node,
    Line,
}

/// <summary>
/// Godot-free application session for one current R2 run. Core state, interaction state,
/// exact time, chapter flow, and immutable presentation publication have one owner here.
/// </summary>
internal sealed partial class RealtimeSession
{
    private const int VirtualFramesPerSecond = 60;
    private const long NanosecondsPerSecond = 1_000_000_000;
    private const long CatchUpCeilingMinutes = 30;

    private readonly List<RealtimeTransition> _emittedTransitions = [];
    private readonly HashSet<string> _autoPausedIncidentKeys = new(StringComparer.Ordinal);
    private readonly Queue<PendingFrameBatch> _retainedFrameDebt = [];
    private readonly RealtimeChapterStoryFlow _chapterStoryFlow = new();
    private readonly RealtimeEpilogueFlow _epilogueFlow = new();
    private readonly HashSet<string> _formativeTutorialResultChapterIds =
        new(StringComparer.Ordinal);
    private readonly RealtimeSliceData _data;
    private readonly RealtimeCampaignRun _run;
    private readonly RealtimeFrameAccumulator _frame;
    private RealtimeInteractionState _interaction;
    private RealtimeSlicePresentation _latestPresentation = null!;
    private CoreMapPoint? _pointerPoint;
    private bool _pointerAccepted = true;
    private string _pointerMessage = string.Empty;
    private RealtimeProjectQuote? _nodeOrderQuote;
    private RealtimeProjectQuote? _lineOrderQuote;
    private long _presentationRevision;
    private long _wallClockRemainderUnits;
    private double _wallClockVirtualFrameRemainder;
    private IReadOnlyList<string> _timelineClusterIds = Array.Empty<string>();
    private int _timelineClusterIndex;
    private bool _draftCancelArmed;
    private bool _formativeDirectPlayRecorded;
    private bool _formativeTutorialFullFlowRecorded;

    internal event Action<RealtimeSlicePresentation>? PresentationPublished;
    internal event Action<RealtimeSlicePresentation>? PointerPresentationPublished;
    internal event Action<string>? EvidenceRecorded;

    internal RealtimeSession(RealtimeSliceData data)
        : this(
            data,
            new RealtimeCampaignRun(
                data?.Campaign ?? throw new ArgumentNullException(nameof(data)),
                data.World),
            Array.Empty<RealtimeTransition>(),
            resumed: false)
    {
    }

    private RealtimeSession(
        RealtimeSliceData data,
        RealtimeCampaignRun run,
        IReadOnlyList<RealtimeTransition> transitionHistory,
        bool resumed)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _run = run ?? throw new ArgumentNullException(nameof(run));
        ArgumentNullException.ThrowIfNull(transitionHistory);
        _frame = new RealtimeFrameAccumulator(CatchUpCeilingMinutes);
        if (resumed)
        {
            RealtimeCampaignSnapshot snapshot = _run.GetSnapshot();
            if (!IsJournalRestorableProgressSnapshot(snapshot) ||
                _run.AcceptedCommands.Count == 0)
            {
                throw new InvalidOperationException(
                    "A resumed R2 session requires accepted journal-restorable " +
                    "incomplete progress.");
            }
            _emittedTransitions.AddRange(transitionHistory);
            foreach (RealtimeTransition transition in transitionHistory.Where(item =>
                         item.Kind == RealtimeTransitionKind.ThermalProtectiveTrip))
            {
                _autoPausedIncidentKeys.Add(
                    $"{transition.Minute}:{transition.AssetKind}:{transition.AssetId}");
            }
            RealtimeInteractionState running =
                RealtimeInteractionReducer.Initial(chapterBriefing: false);
            RealtimeInteractionReduction paused = RealtimeInteractionReducer.Reduce(
                running,
                RealtimeR2Intent.SetPlayerPaused(true),
                snapshot.Construction);
            if (!paused.Accepted)
            {
                throw new InvalidOperationException(
                    "A restored R2 session could not enter its paused resume policy.");
            }
            _interaction = RealtimeInteractionReducer.AlignWithAuthoritativeDraft(
                paused.State,
                snapshot.Construction);
        }
        else
        {
            _interaction = RealtimeInteractionReducer.Initial(chapterBriefing: true);
        }
        _frame.Pause();
        Present();
    }

    internal static RealtimeSession Resume(
        RealtimeSliceData data,
        RealtimeCampaignRestoreResult restore)
    {
        ArgumentNullException.ThrowIfNull(restore);
        return new RealtimeSession(
            data,
            restore.Run,
            restore.Transitions,
            resumed: true);
    }

    internal RealtimeR2IntentResult ApplyIntent(RealtimeR2Intent intent)
    {
        EnsureBootstrapped();
        ArgumentNullException.ThrowIfNull(intent);
        if (!RealtimeInteractionReducer.Supports(intent.Kind))
        {
            RealtimeCampaignSnapshot unsupportedSnapshot = _run.GetSnapshot();
            return IntentResult(
                false,
                RealtimeInteractionReducer.UnsupportedIntentReason,
                null,
                _run.GetCanonicalStateSha256(),
                unsupportedSnapshot.Minute,
                NextCommandSequence,
                _run.AcceptedCommands.Count,
                _presentationRevision);
        }
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
                _run.Minute,
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
                    RealtimePresentationText.RealtimeRunErrorText(commandResult.Error),
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
                (RealtimeR2Ids.IsComparisonMarker(_interaction!.SelectionId) ||
                 RealtimeR2Ids.IsComparisonMarker(
                     _interaction.TimelineSelectedItemId)))
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

    internal RealtimeR2FrameResult InjectElapsedNanoseconds(
        long elapsedNanoseconds,
        long? maximumVirtualFrames = null)
    {
        EnsureBootstrapped();
        if (elapsedNanoseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedNanoseconds));
        }
        return InjectElapsedSeconds(
            elapsedNanoseconds / (double)NanosecondsPerSecond,
            maximumVirtualFrames);
    }

    internal RealtimeR2FrameResult InjectElapsedSeconds(
        double elapsedSeconds,
        long? maximumVirtualFrames = null)
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
        if (maximumVirtualFrames is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumVirtualFrames));
        }
        long boundedVirtualFrames = maximumVirtualFrames.HasValue
            ? Math.Min(virtualFrames, maximumVirtualFrames.Value)
            : virtualFrames;
        if (boundedVirtualFrames != virtualFrames)
        {
            // The interactive observation ends at the frozen canonical minute.
            // Discard callback overrun instead of carrying time beyond the
            // player-visible boundary that the host is about to verify.
            virtualFrames = boundedVirtualFrames;
            nextRemainder = 0;
        }
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

    internal RealtimeR2FrameResult InjectExactFrames(
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
            if (_data?.NativeRoute?.UsesChapterStoryFlow == true)
            {
                _chapterStoryFlow.Observe(
                    transition,
                    _run!.GetSnapshot(),
                    _data!.Campaign);
            }
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
            else if (transition.Kind == RealtimeTransitionKind.CampaignCompleted &&
                     _data?.NativeRoute?.UsesChapterStoryFlow != true)
            {
                _interaction = RealtimeInteractionReducer.AutoPause(
                    _interaction!,
                    RealtimePauseReason.CampaignResult);
                if (_interaction!.ActiveModalId is null)
                {
                    RealtimeInteractionReduction modal = RealtimeInteractionReducer.Reduce(
                        _interaction,
                        RealtimeR2Intent.OpenModal(
                            RealtimeR2Ids.CampaignResultModal,
                            RealtimeModalKind.Story,
                            RealtimePauseReason.CampaignResult,
                            "WORLD"));
                    _interaction = modal.State;
                }
                _frame!.Pause();
            }
        }
        TryOpenChapterStoryModal();
    }

    private void Present()
    {
        EnsureBootstrapped(requirePresentation: false);
        RealtimeCampaignSnapshot snapshot = _run!.GetSnapshot();
        long forecastHorizonMinutes =
            RealtimeTimelinePolicy.RequiredForecastHorizonMinutes(
                snapshot.Minute,
                _interaction!.TimelineAnchorMinute,
                _interaction.TimelineHorizon);
        RealtimeForecastSnapshot baseForecast =
            _run.GetForecast(forecastHorizonMinutes);
        RealtimeComparisonDraftForecast comparisonDraftForecast =
            _run.GetComparisonDraftForecast(forecastHorizonMinutes);
        _nodeOrderQuote =
            snapshot.Construction.NodeDraft is not null
                ? _run.PreviewNodeOrder()
                : null;
        _lineOrderQuote =
            snapshot.Construction.LineDraft is { EndNodeId: not null }
                ? _run.PreviewLineOrder()
                : null;
        RealtimeChapterStoryModalRequest? activeStoryRequest =
            _data!.NativeRoute?.UsesChapterStoryFlow == true
                ? string.Equals(
                    _interaction.ActiveModalId,
                    RealtimeR2Ids.ChapterBriefingModal,
                    StringComparison.Ordinal)
                    ? RealtimeChapterStoryFlow.InitialBriefing(
                        snapshot.Chapter.Content.ChapterId)
                    : _chapterStoryFlow.Active
                : null;
        bool storyResultAdvancesCalendar = activeStoryRequest is not null &&
            string.Equals(
                activeStoryRequest.ModalId,
                _interaction.ActiveModalId,
                StringComparison.Ordinal) &&
            _chapterStoryFlow.CalendarAdvanceTarget(
                activeStoryRequest,
                snapshot).HasValue;
        bool successfulStandaloneCompletion =
            _data.NativeRoute?.IsStandaloneChapter == true &&
            string.Equals(
                _interaction.ActiveModalId,
                RealtimeR2Ids.CampaignResultModal,
                StringComparison.Ordinal) &&
            IsSuccessfulStandaloneCompletion(
                snapshot,
                _data.Campaign.Chapters.Single());
        _presentationRevision = checked(_presentationRevision + 1);
        _latestPresentation = RealtimeSlicePresenter.Present(new RealtimePresentationSource(
            _data,
            snapshot,
            baseForecast,
            comparisonDraftForecast,
            _interaction,
            _presentationRevision,
            new RealtimeWorldPointerFeedback(
                _pointerPoint,
                _pointerAccepted,
                _pointerMessage),
            ReduceMotion: false,
            _nodeOrderQuote,
            _lineOrderQuote,
            _emittedTransitions,
            activeStoryRequest,
            storyResultAdvancesCalendar,
            successfulStandaloneCompletion,
            _epilogueFlow.Active));
        PresentationPublished?.Invoke(_latestPresentation);
    }

    private void PresentPointerFeedback()
    {
        EnsureBootstrapped();
        _presentationRevision = checked(_presentationRevision + 1);
        var pointer = new RealtimeWorldPointerFeedback(
            _pointerPoint,
            _pointerAccepted,
            _pointerMessage);
        _latestPresentation = RealtimeSlicePresenter.PresentPointerFeedback(
            _latestPresentation!,
            _interaction!,
            _presentationRevision,
            pointer,
            _nodeOrderQuote,
            _lineOrderQuote);
        PointerPresentationPublished?.Invoke(_latestPresentation);
    }

    internal void HandleMapPrimary(
        RealtimePointerResolution resolution,
        CoreMapPoint worldPoint)
    {
        _draftCancelArmed = false;
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
            RealtimeWorldIds.TryParseSelectionAction(
                resolution.ResolvedId,
                out string selectedId))
        {
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

    internal void HandleMapPointerMoved(
        RealtimePointerResolution resolution,
        CoreMapPoint worldPoint,
        bool isPanning)
    {
        // Pointer feedback replaces the inline Esc confirmation. Disarm it at
        // the same boundary so a later Esc can never cancel a draft using a
        // warning that is no longer visible.
        _draftCancelArmed = false;
        _pointerPoint = worldPoint;
        if (isPanning)
        {
            PresentPointerFeedback();
            return;
        }

        ConstructionSnapshot construction = _latestPresentation!.CoreSnapshot.Construction;
        if (IsCampaignReadOnlyShell &&
            (resolution.Owner == RealtimePointerOwner.DraftHandle ||
             _interaction!.Tool is RealtimeTool.BuildNode or
                 RealtimeTool.BuildLine or RealtimeTool.MoveDraft))
        {
            SetPointerFeedback(
                false,
                RealtimeInteractionReducer.CampaignEndedReadOnlyReason);
            PresentPointerFeedback();
            return;
        }
        if (resolution.Owner == RealtimePointerOwner.SelectionAction &&
            RealtimeWorldIds.TryParseSelectionAction(
                resolution.ResolvedId,
                out string selectedId))
        {
            SetPointerFeedback(
                true,
                $"{DisplayAssetName(selectedId)} 상황 패널 열기 · 클릭 또는 Enter로 엽니다.");
            PresentPointerFeedback();
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
                NodePlacementPreview nodePreview = _run!.PreviewNodePlacement(
                    nodeClassId,
                    worldPoint);
                SetPointerFeedback(
                    nodePreview.Accepted,
                    nodePreview.Accepted
                        ? nodePreview.RiskAreaIds.Count == 0
                            ? "배치 가능 · 클릭 또는 Enter로 초안을 놓습니다."
                            : $"배치 가능 · 위험구역 {nodePreview.RiskAreaIds.Count}곳과 겹칩니다."
                        : $"배치 불가 · {RealtimePresentationText.ConstructionErrorText(nodePreview.Error)}");
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
        PresentPointerFeedback();
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
                    : $"시작 불가 · {RealtimePresentationText.ConstructionErrorText(preview.Error)}");
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
                    : $"접속 불가 · {RealtimePresentationText.ConstructionErrorText(preview.Error)}");
            return;
        }
        LinePointPreview pointPreview = _run!.PreviewLinePoint(worldPoint);
        SetPointerFeedback(
            pointPreview.Accepted,
            pointPreview.Accepted
                ? pointPreview.RiskAreaIds.Count == 0
                    ? "경로점 추가 가능 · 클릭 또는 Enter로 확정합니다."
                    : $"경로점 추가 가능 · 위험구역 {pointPreview.RiskAreaIds.Count}곳을 지납니다."
                : $"경로점 불가 · {RealtimePresentationText.ConstructionErrorText(pointPreview.Error)}");
    }

    internal void HandleAction(string id)
    {
        if ((id is RealtimeR2Ids.PromiseKeepAction or
                RealtimeR2Ids.PromiseDeferAction) &&
            !CanRequestPromiseAction(id))
        {
            return;
        }
        _ = id switch
        {
            RealtimeR2Ids.OrderNodeAction => ApplyIntent(new RealtimeR2Intent(
                RealtimeR2IntentKind.OrderNode)),
            RealtimeR2Ids.OrderLineAction => ApplyIntent(RealtimeR2Intent.OrderLine()),
            RealtimeR2Ids.PromiseKeepAction => ApplyIntent(new RealtimeR2Intent(
                RealtimeR2IntentKind.SetPromiseDecision,
                PromiseDecision: CommercialPromiseDecision.Keep)),
            RealtimeR2Ids.PromiseDeferAction => ApplyIntent(new RealtimeR2Intent(
                RealtimeR2IntentKind.SetPromiseDecision,
                PromiseDecision: CommercialPromiseDecision.Defer)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(id),
                id,
                "Unsupported realtime action."),
        };
    }

    private bool CanRequestPromiseAction(string actionId)
    {
        RealtimeCampaignSnapshot? snapshot = _run?.GetSnapshot();
        RealtimeContextDockPresentation? context = _latestPresentation?.Context;
        if (snapshot?.Chapter.Content.CityPromise is not
                CommercialCityPromiseDefinition promise ||
            context is not { Visible: true } ||
            !string.Equals(
                context.SubjectId,
                RealtimeR2Ids.PromiseDecisionMarker(promise.PromiseId),
                StringComparison.Ordinal))
        {
            return false;
        }
        RealtimeActionPresentation? action = string.Equals(
                actionId,
                RealtimeR2Ids.PromiseKeepAction,
                StringComparison.Ordinal)
            ? context.PrimaryAction
            : context.SecondaryAction;
        return action is { Visible: true, Enabled: true } &&
            string.Equals(action.Id, actionId, StringComparison.Ordinal);
    }

    internal void HandleModalAction(string modalId, string actionId)
    {
        RealtimeModalPresentation? modal = _latestPresentation?.Modal;
        RealtimeChapterStoryModalRequest? storyRequest = _chapterStoryFlow.Active;
        if (modal is null ||
            !string.Equals(modal.Id, modalId, StringComparison.Ordinal))
        {
            return;
        }
        bool primaryMatch = string.Equals(
            modal.PrimaryAction.Id,
            actionId,
            StringComparison.Ordinal);
        RealtimeActionPresentation? matchedAction = primaryMatch
            ? modal.PrimaryAction
            : modal.SecondaryAction is { } secondary && string.Equals(
                secondary.Id,
                actionId,
                StringComparison.Ordinal)
                ? secondary
                : null;
        if (matchedAction is null || !matchedAction.Enabled || !matchedAction.Visible)
        {
            return;
        }
        if (!primaryMatch || !RealtimeR2Ids.IsSupportedModalCloseAction(actionId))
        {
            throw new ArgumentOutOfRangeException(
                nameof(actionId),
                actionId,
                "Unsupported realtime modal action.");
        }
        // Every production R2 modal action is deliberately a close/continue
        // operation. Destructive recovery/new-game/title actions are never
        // presented because no production handler implements those mutations.
        RealtimeR2IntentResult result =
            ApplyIntent(RealtimeR2Intent.CloseModal(modalId));
        if (result.Accepted)
        {
            AfterModalClosed(modal, storyRequest);
        }
    }

    internal void HandleModalDismiss(string modalId)
    {
        RealtimeModalPresentation? modal = _latestPresentation?.Modal;
        RealtimeChapterStoryModalRequest? storyRequest = _chapterStoryFlow.Active;
        if (modal is null ||
            !string.Equals(modal.Id, modalId, StringComparison.Ordinal) ||
            !modal.DismissOnCancel)
        {
            return;
        }
        RealtimeR2IntentResult result =
            ApplyIntent(RealtimeR2Intent.CloseModal(modalId));
        if (result.Accepted)
        {
            AfterModalClosed(modal, storyRequest);
        }
    }

    private void AfterModalClosed(
        RealtimeModalPresentation modal,
        RealtimeChapterStoryModalRequest? storyRequest)
    {
        TryRecordFormativeDirectPlay(modal);
        if (_epilogueFlow.Close(modal.Id))
        {
            TryOpenEpilogueModal();
            return;
        }
        if (_data?.NativeRoute?.UsesChapterStoryFlow != true ||
            !_chapterStoryFlow.Close(modal.Id))
        {
            return;
        }
        if (TryAdvanceToNextChapter(storyRequest))
        {
            return;
        }
        if (storyRequest is
            {
                Purpose: RealtimeChapterStoryModalPurpose.ChapterResult,
                FinalResult: true,
            } && TryStartEpilogue())
        {
            return;
        }
        TryOpenChapterStoryModal();
    }

    private bool TryAdvanceToNextChapter(
        RealtimeChapterStoryModalRequest? closedRequest)
    {
        if (_data?.NativeRoute?.UsesChapterStoryFlow != true)
        {
            return false;
        }

        RealtimeCampaignSnapshot before = _run!.GetSnapshot();
        long? targetMinute = _chapterStoryFlow.CalendarAdvanceTarget(
            closedRequest,
            before);
        if (!targetMinute.HasValue)
        {
            return false;
        }

        RealtimeAdvanceResult advance = _run.AdvanceTo(targetMinute.Value);
        CollectTransitions(advance.Transitions);
        if (_latestPresentation?.CoreSnapshot.Minute != advance.Snapshot.Minute)
        {
            Present();
        }
        return true;
    }

    private void TryOpenChapterStoryModal()
    {
        if (_data?.NativeRoute?.UsesChapterStoryFlow != true ||
            _interaction?.ActiveModalId is not null)
        {
            return;
        }
        RealtimeChapterStoryModalRequest? request = _chapterStoryFlow.ActivateNext();
        if (request is null)
        {
            return;
        }

        RealtimeInteractionState before = _interaction!;
        if (request.FinalResult)
        {
            _interaction = RealtimeInteractionReducer.AutoPause(
                _interaction!,
                RealtimePauseReason.CampaignResult);
        }
        RealtimeInteractionReduction reduction = RealtimeInteractionReducer.Reduce(
            _interaction!,
            RealtimeR2Intent.OpenModal(
                request.ModalId,
                RealtimeModalKind.Story,
                request.PauseReason,
                "WORLD"));
        if (!reduction.Accepted)
        {
            throw new InvalidOperationException(
                $"Tutorial modal '{request.ModalId}' could not be opened: " +
                reduction.Error);
        }
        _interaction = reduction.State;
        SynchronizeFramePause(before, _interaction);
        Present();
    }

    private bool TryStartEpilogue()
    {
        RealtimeCampaignSnapshot snapshot = _run!.GetSnapshot();
        if (!snapshot.CampaignComplete || _epilogueFlow.Started)
        {
            return false;
        }
        if (!_epilogueFlow.TryStart(_data!.BaseCampaign, _data.Campaign, snapshot))
        {
            return false;
        }
        TryOpenEpilogueModal();
        return true;
    }

    private void TryOpenEpilogueModal()
    {
        if (_interaction?.ActiveModalId is not null)
        {
            return;
        }
        RealtimeEpilogueModalRequest? request = _epilogueFlow.ActivateNext();
        if (request is null)
        {
            return;
        }
        RealtimeInteractionState before = _interaction!;
        RealtimeInteractionReduction reduction = RealtimeInteractionReducer.Reduce(
            _interaction!,
            RealtimeR2Intent.OpenModal(
                request.ModalId,
                RealtimeModalKind.Story,
                RealtimePauseReason.CampaignResult,
                "WORLD"));
        if (!reduction.Accepted)
        {
            throw new InvalidOperationException(
                $"Epilogue modal '{request.ModalId}' could not be opened: " +
                reduction.Error);
        }
        _interaction = reduction.State;
        SynchronizeFramePause(before, _interaction);
        Present();
    }

    private void TryRecordFormativeDirectPlay(RealtimeModalPresentation closedModal)
    {
        if (_data?.NativeRoute?.UsesChapterStoryFlow == true)
        {
            TryRecordCumulativeDirectPlay(closedModal);
            return;
        }
        if (_formativeDirectPlayRecorded ||
            _data?.NativeRoute?.IsStandaloneChapter != true ||
            !string.Equals(
                closedModal.Id,
                RealtimeR2Ids.CampaignResultModal,
                StringComparison.Ordinal) ||
            _run?.GetSnapshot() is not { CampaignComplete: true } snapshot)
        {
            return;
        }
        RealtimeChapterDefinition realtimeChapter = _data.Campaign.Chapters.Single();
        if (!IsSuccessfulStandaloneCompletion(snapshot, realtimeChapter))
        {
            return;
        }
        CommercialCampaignChapterDefinition chapter = realtimeChapter.Content;
        CommercialStoryCard authored = chapter.ResultCards.Standard ??
            throw new InvalidOperationException(
                "FIRST_LIGHT release route has no authored standard result.");
        if (!string.Equals(closedModal.Eyebrow, authored.Speaker, StringComparison.Ordinal) ||
            !string.Equals(closedModal.Heading, authored.Title, StringComparison.Ordinal) ||
            !string.Equals(closedModal.Body, authored.Body, StringComparison.Ordinal) ||
            snapshot.CompletedChapters.Count != 1 ||
            !string.Equals(
                snapshot.CompletedChapters[0].ChapterId,
                chapter.ChapterId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "FORMATIVE direct-play close did not carry the exact authored FIRST_LIGHT result.");
        }
        _formativeDirectPlayRecorded = true;
        EvidenceRecorded?.Invoke($"FORMATIVE_DIRECT_PLAY_PASS:{chapter.ChapterId}");
    }

    private void TryRecordCumulativeDirectPlay(RealtimeModalPresentation closedModal)
    {
        RealtimeChapterStoryModalRequest? request = _chapterStoryFlow.Active;
        if (request is not
            {
                Purpose: RealtimeChapterStoryModalPurpose.ChapterResult,
            } ||
            !string.Equals(request.ModalId, closedModal.Id, StringComparison.Ordinal))
        {
            return;
        }
        RealtimeCampaignSnapshot snapshot = _run!.GetSnapshot();
        RealtimeChapterOutcome outcome = snapshot.CompletedChapters.Single(item =>
            string.Equals(item.ChapterId, request.ChapterId, StringComparison.Ordinal));
        if (!outcome.ObjectiveSatisfied)
        {
            return;
        }
        CommercialCampaignChapterDefinition chapter = _data!.Campaign.Chapters.Single(
            item => string.Equals(
                item.Content.ChapterId,
                request.ChapterId,
                StringComparison.Ordinal)).Content;
        bool promiseChapter = chapter.CityPromise is not null;
        bool autoDefaulted = _emittedTransitions.Any(item =>
            item.Kind == RealtimeTransitionKind.PromiseDefaulted &&
            string.Equals(
                item.ChapterId,
                request.ChapterId,
                StringComparison.Ordinal));
        if (promiseChapter &&
            (autoDefaulted || outcome.PromiseDecision != CommercialPromiseDecision.Keep))
        {
            // The only native formative branch authorized for this gate is an
            // explicit Keep. Defer/default/failure remain deterministic smoke
            // evidence and cannot mint a production-input PASS token.
            return;
        }
        CommercialStoryCard authored = promiseChapter
            ? chapter.ResultCards.Kept ?? throw new InvalidOperationException(
                $"Promise chapter '{request.ChapterId}' has no kept result.")
            : chapter.ResultCards.Standard ?? throw new InvalidOperationException(
                $"Tutorial chapter '{request.ChapterId}' has no standard result.");
        if (!string.Equals(closedModal.Eyebrow, authored.Speaker, StringComparison.Ordinal) ||
            !string.Equals(closedModal.Heading, authored.Title, StringComparison.Ordinal) ||
            !string.Equals(closedModal.Body, authored.Body, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Tutorial result '{request.ChapterId}' did not carry its exact authored card.");
        }
        int chapterIndex = _data.Campaign.Chapters.ToList().FindIndex(chapter =>
            string.Equals(
                chapter.Content.ChapterId,
                request.ChapterId,
                StringComparison.Ordinal));
        if (chapterIndex < 0 ||
            _formativeTutorialResultChapterIds.Contains(request.ChapterId))
        {
            throw new InvalidOperationException(
                "Tutorial formative result chapter is unknown or was closed more than once.");
        }
        if (chapterIndex != _formativeTutorialResultChapterIds.Count)
        {
            // A prior failed chapter deliberately breaks the positive evidence
            // chain. Later successful chapters remain playable, but cannot mint
            // a partial sequence or the full-flow record.
            return;
        }
        if (!_formativeTutorialResultChapterIds.Add(request.ChapterId))
        {
            throw new InvalidOperationException(
                "Tutorial formative result close was repeated.");
        }
        EvidenceRecorded?.Invoke(promiseChapter
            ? $"FORMATIVE_DIRECT_PLAY_PASS:{request.ChapterId}:KEEP"
            : $"FORMATIVE_DIRECT_PLAY_PASS:{request.ChapterId}");
        if (_formativeTutorialResultChapterIds.Count != _data.Campaign.Chapters.Count ||
            !request.FinalResult ||
            !snapshot.CampaignComplete ||
            _formativeTutorialFullFlowRecorded)
        {
            return;
        }
        _formativeTutorialFullFlowRecorded = true;
        EvidenceRecorded?.Invoke(_data.NativeRoute?.FullFlowPassToken ??
            throw new InvalidOperationException(
                "A cumulative native route has no full-flow evidence token."));
    }

    private static bool IsSuccessfulStandaloneCompletion(
        RealtimeCampaignSnapshot snapshot,
        RealtimeChapterDefinition selectedChapter)
    {
        if (!snapshot.CampaignComplete || snapshot.CompletedChapters.Count != 1)
        {
            return false;
        }
        RealtimeChapterOutcome chapter = snapshot.CompletedChapters[0];
        if (!string.Equals(
                chapter.ChapterId,
                selectedChapter.Content.ChapterId,
                StringComparison.Ordinal) ||
            chapter.Events.Count != 1)
        {
            return false;
        }
        RealtimeEventOutcome outcome = chapter.Events[0];
        return string.Equals(
                   outcome.EventId,
                   selectedChapter.ScheduledEvents.Single().EventId,
                   StringComparison.Ordinal) &&
               outcome.SafetySatisfied &&
               outcome.SafetyUnservedMinutes == 0 &&
               outcome.PromiseSatisfied;
    }

    internal void HandleBuildTool(string id)
    {
        if (!_latestPresentation.BuildShelf.Tools.Any(item => string.Equals(
                item.Id,
                id,
                StringComparison.Ordinal)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(id),
                id,
                "The build tool is not in the current presentation.");
        }
        if (string.Equals(id, RealtimeR2Ids.AnalysisTool, StringComparison.Ordinal))
        {
            _ = ApplyIntent(new RealtimeR2Intent(RealtimeR2IntentKind.ToggleAnalysis));
            return;
        }
        RealtimeTool? tool = id switch
        {
            RealtimeR2Ids.InspectTool => RealtimeTool.Inspect,
            _ when id.StartsWith(RealtimeR2Ids.NodeToolPrefix, StringComparison.Ordinal) =>
                RealtimeTool.BuildNode,
            _ when id.StartsWith(RealtimeR2Ids.LineToolPrefix, StringComparison.Ordinal) =>
                RealtimeTool.BuildLine,
            _ => null,
        };
        if (!tool.HasValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(id),
                id,
                "Unsupported realtime build tool.");
        }
        _ = ApplyIntent(tool is RealtimeTool.BuildNode or RealtimeTool.BuildLine
            ? RealtimeR2Intent.SelectBuildTool(tool.Value, id)
            : RealtimeR2Intent.SelectTool(tool.Value));
    }

    private bool TrySelectedNodeClass(out string nodeClassId)
    {
        const string prefix = RealtimeR2Ids.NodeToolPrefix;
        string? toolId = _interaction?.SelectedBuildToolId;
        nodeClassId = toolId is not null && toolId.StartsWith(prefix, StringComparison.Ordinal)
            ? toolId[prefix.Length..]
            : string.Empty;
        return nodeClassId.Length > 0 && PresentedCoreSnapshot.Chapter.Content
            .AvailableNodeClassIds.Contains(nodeClassId, StringComparer.Ordinal);
    }

    private bool TrySelectedLinePlan(
        out CommercialCampaignLinePlanDefinition? selectedPlan)
    {
        string? toolId = _interaction?.SelectedBuildToolId;
        selectedPlan = PresentedCoreSnapshot.Chapter.Content.AvailableLinePlans
            .SingleOrDefault(plan => string.Equals(
                toolId,
                RealtimeR2Ids.LineTool(plan.LineClassId, plan.PoleClassId),
                StringComparison.Ordinal));
        return selectedPlan is not null;
    }

    internal void HandleSpeedRequested(RealtimeSimulationSpeed speed)
    {
        if (speed == RealtimeSimulationSpeed.Paused)
        {
            HandleTogglePause();
            return;
        }
        _ = ApplyIntent(RealtimeR2Intent.SetSpeed(speed));
    }

    internal void HandleTogglePause()
    {
        if (_interaction.Simulation == RealtimeSimulationState.AutoPaused &&
            _interaction.ActiveModalId is null)
        {
            _ = ApplyIntent(RealtimeR2Intent.AcknowledgeAutoPause());
        }
        else
        {
            _ = ApplyIntent(RealtimeR2Intent.SetPlayerPaused(
                _interaction.Simulation == RealtimeSimulationState.Running));
        }
    }

    internal void HandleToggleBuildShelf()
    {
        ConstructionSnapshot construction = PresentedCoreSnapshot.Construction;
        bool menuCancelsAuthoritativeDraft = !IsCampaignReadOnlyShell &&
            (construction.NodeDraft is not null || construction.LineDraft is not null);
        if (menuCancelsAuthoritativeDraft)
        {
            // The top HUD says "건설 취소" while a Core-authoritative draft exists.
            HandleCancel();
        }
        else if (_interaction.Tool is RealtimeTool.BuildNode or
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
    }

    internal void SelectBuildToolFamily(RealtimeBuildToolFamily family)
    {
        string prefix = family switch
        {
            RealtimeBuildToolFamily.Node => RealtimeR2Ids.NodeToolPrefix,
            RealtimeBuildToolFamily.Line => RealtimeR2Ids.LineToolPrefix,
            _ => throw new ArgumentOutOfRangeException(
                nameof(family),
                family,
                "Unsupported realtime build-tool family."),
        };
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

    internal void HandleTimelineHorizonDelta(int delta)
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

    internal void HandleTimelineItems(IReadOnlyList<string> ids)
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

    internal void HandleTimelineNavigation(RealtimeTimelineNavigation navigation)
    {
        if (!RealtimeUiCapabilities.Supports(navigation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(navigation),
                navigation,
                "Unsupported realtime timeline navigation.");
        }
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
        RealtimeTimelineTarget target = RealtimeTimelineTargetResolver.Resolve(
            _data!.BaseWorld,
            _run!.GetSnapshot(),
            _latestPresentation!.BaseForecast,
            _latestPresentation!.ComparisonDraftForecast,
            _latestPresentation.TransitionHistory,
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

    internal void HandleCancel()
    {
        ConstructionSnapshot construction = _run!.GetSnapshot().Construction;
        if (_interaction!.ActiveModalId is string modalId)
        {
            HandleModalDismiss(modalId);
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

    internal void HandleUndoDraftStep()
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
                RealtimeR2IntentKind.SetSpeed or
                    RealtimeR2IntentKind.SetPlayerPaused or
                    RealtimeR2IntentKind.SelectTool or
                    RealtimeR2IntentKind.OpenSurface or
                    RealtimeR2IntentKind.CloseSurface or
                    RealtimeR2IntentKind.SelectId or
                    RealtimeR2IntentKind.ClearSelection or
                    RealtimeR2IntentKind.OpenModal or
                    RealtimeR2IntentKind.CloseModal or
                    RealtimeR2IntentKind.AcknowledgeAutoPause or
                    RealtimeR2IntentKind.ToggleAnalysis or
                    RealtimeR2IntentKind.SetTimelineView => (null, null),
                _ => (null, RealtimeInteractionReducer.UnsupportedIntentReason),
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

    private void SetPointerFeedback(bool accepted, string message)
    {
        _pointerAccepted = accepted;
        _pointerMessage = message;
    }

    private string DisplayAssetName(string assetId)
    {
        EnsureBootstrapped(requirePresentation: false);
        return RealtimePresentationText.AssetDisplayName(
            _data!.BaseWorld,
            PresentedCoreSnapshot,
            assetId);
    }

    private static string ConstructionFeedback(
        RealtimeR2IntentKind kind,
        RealtimeCommandResult result)
    {
        if (!result.Accepted)
        {
            string reason = result.ConstructionError.HasValue
                ? RealtimePresentationText.ConstructionErrorText(result.ConstructionError)
                : RealtimePresentationText.RealtimeRunErrorText(result.Error);
            return $"공사 입력을 처리하지 못했습니다. {reason}";
        }
        return kind switch
        {
            RealtimeR2IntentKind.SetNodeDraft => "변전소 초안을 배치했습니다.",
            RealtimeR2IntentKind.CancelNodeDraft => "변전소 초안을 취소했습니다.",
            RealtimeR2IntentKind.OrderNode or RealtimeR2IntentKind.OrderLine
                when result.Snapshot.Construction.ActiveConstruction is
                    ActiveConstructionSnapshot project =>
                $"공사를 승인했습니다. {RealtimePresentationText.Time(project.CompletionMinute)}에 완공됩니다.",
            RealtimeR2IntentKind.StartLineDraft => "선로 시작점을 선택했습니다.",
            RealtimeR2IntentKind.AddLinePoint => "선로 경로점을 추가했습니다.",
            RealtimeR2IntentKind.MoveLinePoint => "선로 경로점을 이동했습니다.",
            RealtimeR2IntentKind.UndoLinePoint => "마지막 선로 단계를 되돌렸습니다.",
            RealtimeR2IntentKind.FinishLineDraft => "선로 끝점을 연결했습니다.",
            RealtimeR2IntentKind.CancelLineDraft => "선로 초안을 취소했습니다.",
            _ => "공사 입력을 승인했습니다.",
        };
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
        _run.Minute,
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

    private long NextCommandSequence => _run.AcceptedCommands.Count + 1L;

    private bool IsCampaignReadOnlyShell =>
        _interaction.Simulation == RealtimeSimulationState.Ended ||
        _latestPresentation.CoreSnapshot.CampaignComplete;

    private RealtimeCampaignSnapshot PresentedCoreSnapshot =>
        _latestPresentation?.CoreSnapshot ?? _run.GetSnapshot();

    private bool CanCaptureProgress =>
        _run.AcceptedCommands.Count > 0 &&
        IsJournalRestorableProgressSnapshot(_run.GetSnapshot()) &&
        _interaction.ActiveModalId is null &&
        _chapterStoryFlow.IsIdle &&
        !_epilogueFlow.Started &&
        _retainedFrameDebt.Count == 0 &&
        _interaction.Simulation is
            RealtimeSimulationState.Running or
            RealtimeSimulationState.PlayerPaused;

    internal static bool IsJournalRestorableProgressSnapshot(
        RealtimeCampaignSnapshot snapshot) =>
        snapshot.ChapterStarted &&
        !snapshot.CampaignComplete &&
        snapshot.PendingTransitions.Count == 0 &&
        snapshot.Construction.NodeDraft is null &&
        snapshot.Construction.LineDraft is null;

    private void EnsureBootstrapped(bool requirePresentation = true)
    {
        if (requirePresentation && _latestPresentation is null)
        {
            throw new InvalidOperationException("Realtime R2 session is not presented.");
        }
    }

    internal void DisarmDraftCancellation() => _draftCancelArmed = false;

    internal RealtimeSliceData Data => _data;

    internal RealtimeCampaignSnapshot CoreSnapshot => _run.GetSnapshot();

    internal string CanonicalStateSha256 => _run.GetCanonicalStateSha256();

    internal RealtimeFrameAccumulatorSnapshot AccumulatorSnapshot =>
        _frame.GetSnapshot();

    internal RealtimeInteractionState InteractionState => _interaction;

    internal IReadOnlyList<TimedRealtimeCommand> AcceptedCommands =>
        _run.AcceptedCommands;

    internal bool TryCaptureProgress(
        RealtimeCampaignSourceIdentity source,
        out RealtimeCampaignSave? save)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!CanCaptureProgress)
        {
            save = null;
            return false;
        }
        save = RealtimeCampaignSaveCodec.Capture(
            source,
            _data.Campaign,
            _data.World,
            _run);
        return true;
    }

    internal int AcceptedCommandCount => _run.AcceptedCommands.Count;

    internal long CommandSequence => NextCommandSequence;

    internal long CurrentMinute => _run.Minute;

    internal long PresentationRevision => _presentationRevision;

    internal RealtimeSlicePresentation LatestPresentation => _latestPresentation;

    internal IReadOnlyList<RealtimeTransition> EmittedTransitions =>
        Array.AsReadOnly(_emittedTransitions.ToArray());

    internal IReadOnlyList<RealtimeR2PendingFrameDebt> RetainedFrameDebt =>
        FrozenFrameDebt();

    internal RealtimeComparisonDraftForecast GetComparisonDraftForecast(
        long? horizonMinutes = null) => horizonMinutes.HasValue
            ? _run.GetComparisonDraftForecast(horizonMinutes.Value)
            : _run.GetComparisonDraftForecast();

    internal RealtimeForecastSnapshot GetForecast(long horizonMinutes) =>
        _run.GetForecast(horizonMinutes);

    internal RealtimeProjectQuote PreviewNodeOrder() => _run.PreviewNodeOrder();

    internal RealtimeProjectQuote PreviewLineOrder() => _run.PreviewLineOrder();

    internal NodePlacementPreview PreviewNodePlacement(
        string nodeClassId,
        CoreMapPoint position) => _run.PreviewNodePlacement(nodeClassId, position);

    internal RealtimeChapterStoryModalRequest? ActiveChapterStoryModal =>
        _chapterStoryFlow.Active;

    internal RealtimeEpilogueModalRequest? ActiveEpilogueModal =>
        _epilogueFlow.Active;

    internal bool EpilogueCompleted => _epilogueFlow.Completed;

    internal IReadOnlyList<string> FormativeTutorialResultChapterIds =>
        Array.AsReadOnly(_formativeTutorialResultChapterIds
            .OrderBy(id => _data.Campaign.Chapters.ToList().FindIndex(chapter =>
                string.Equals(
                    chapter.Content.ChapterId,
                    id,
                    StringComparison.Ordinal)))
            .ToArray());

    internal bool FormativeDirectPlayRecorded => _formativeDirectPlayRecorded;

    internal bool FormativeTutorialFullFlowRecorded =>
        _formativeTutorialFullFlowRecorded;

    private sealed class PendingFrameBatch(
        long frameCount,
        int framesPerSecond,
        int speedMultiplier)
    {
        internal long FrameCount { get; set; } = frameCount;
        internal int FramesPerSecond { get; } = framesPerSecond;
        internal int SpeedMultiplier { get; } = speedMultiplier;
    }
}
