#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;

namespace Gridworks.Game.Realtime.R2;

internal static class RealtimeSliceCheckpointIds
{
    internal const string NormalReady = "A1_NORMAL_READY";
    internal const string ConstructionDueOneMinute = "A1_CONSTRUCTION_DUE_1M";

    internal static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        NormalReady,
        ConstructionDueOneMinute,
    });

    internal static bool IsKnown(string checkpointId) => All.Contains(
        checkpointId,
        StringComparer.Ordinal);
}

internal sealed record RealtimeSliceCheckpointFixtureFact(
    string BaseWorldResourceId,
    string BaseWorldSchemaVersion,
    string BaseWorldId,
    string BaseWorldSourceSha256,
    string BaseCampaignResourceId,
    string BaseCampaignSchemaVersion,
    string BaseCampaignId,
    string BaseCampaignSourceSha256,
    string RealtimeWorldResourceId,
    string RealtimeWorldSchemaVersion,
    string RealtimeWorldId,
    string RealtimeWorldSourceSha256,
    string RealtimeWorldDefinitionSha256,
    string RealtimeCampaignResourceId,
    string RealtimeCampaignSchemaVersion,
    string RealtimeCampaignId,
    string RealtimeCampaignSourceSha256,
    string RealtimeCampaignDefinitionSha256);

internal sealed record RealtimeSliceCheckpointConstructionFact(
    ConstructionKind Kind,
    long CostCashUnit,
    long CompletionMinute,
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<string> EdgeIds)
{
    private IReadOnlyList<string> _nodeIds = Freeze(NodeIds);
    private IReadOnlyList<string> _edgeIds = Freeze(EdgeIds);

    public IReadOnlyList<string> NodeIds
    {
        get => _nodeIds;
        init => _nodeIds = Freeze(value);
    }

    public IReadOnlyList<string> EdgeIds
    {
        get => _edgeIds;
        init => _edgeIds = Freeze(value);
    }

    private static IReadOnlyList<string> Freeze(IReadOnlyList<string> values) =>
        Array.AsReadOnly(values.OrderBy(value => value, StringComparer.Ordinal).ToArray());
}

internal sealed record RealtimeSliceCheckpointEventFact(
    string EventId,
    long StartMinute,
    long EndMinute,
    int Priority);

internal sealed record RealtimeSliceCheckpointDutyFact(
    string ChapterId,
    string EventId,
    long SegmentStartMinute,
    int ClosedSegmentCount,
    int IncidentCount);

internal sealed record RealtimeSliceCheckpointThermalFact(
    string AssetId,
    ThermalAssetKind AssetKind,
    ThermalOperatingState State,
    long UsedKw,
    long EmergencyExposureMinutes,
    bool AuthoredUnavailable,
    bool ProtectiveOutage,
    long? ProtectiveOutageUntilMinute);

internal sealed record RealtimeSliceCheckpointFact(
    string CheckpointId,
    RealtimeSliceCheckpointFixtureFact Fixture,
    string StateCreationMethod,
    string CommandReplaySchemaId,
    string CommandReplaySha256,
    int CommandCount,
    long StartMinute,
    string StartCanonicalStateSha256,
    string ExpectedEndCanonicalStateSha256,
    RealtimeSliceCheckpointConstructionFact? ActiveConstruction,
    IReadOnlyList<RealtimeSliceCheckpointEventFact> ActiveEvents,
    RealtimeSliceCheckpointDutyFact? ActiveDuty,
    IReadOnlyList<RealtimeSliceCheckpointThermalFact> Thermal,
    string? ExpectedSelectionId,
    long? ExpectedTimelineAnchorMinute,
    long ExpectedWorldPresentationMinute,
    RealtimeSurface ExpectedSurface,
    RealtimeTool ExpectedTool,
    RealtimeSimulationState ExpectedSimulation,
    RealtimePauseReason ExpectedPauseReason,
    string AllowedNextInput,
    long AllowedFrameCount,
    int AllowedFramesPerSecond,
    long AllowedAdvanceMinutes,
    int StartWorldNodeCount,
    int StartWorldEdgeCount,
    string EndAssertion,
    string EvidenceLabel)
{
    private IReadOnlyList<RealtimeSliceCheckpointEventFact> _activeEvents =
        Freeze(ActiveEvents);
    private IReadOnlyList<RealtimeSliceCheckpointThermalFact> _thermal =
        Freeze(Thermal);

    public IReadOnlyList<RealtimeSliceCheckpointEventFact> ActiveEvents
    {
        get => _activeEvents;
        init => _activeEvents = Freeze(value);
    }

    public IReadOnlyList<RealtimeSliceCheckpointThermalFact> Thermal
    {
        get => _thermal;
        init => _thermal = Freeze(value);
    }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values) =>
        Array.AsReadOnly(values.ToArray());
}

internal sealed record RealtimeSliceCheckpointSegmentResult(
    RealtimeSliceCheckpointFact Checkpoint,
    long EndMinute,
    string EndCanonicalStateSha256,
    long StartPresentationRevision,
    long EndPresentationRevision,
    IReadOnlyList<RealtimeTransition> Transitions,
    IReadOnlyList<string> ExpectedCommissionedNodeIds,
    IReadOnlyList<string> ExpectedCommissionedEdgeIds)
{
    private IReadOnlyList<RealtimeTransition> _transitions =
        Array.AsReadOnly(Transitions.ToArray());
    private IReadOnlyList<string> _expectedCommissionedNodeIds =
        Freeze(ExpectedCommissionedNodeIds);
    private IReadOnlyList<string> _expectedCommissionedEdgeIds =
        Freeze(ExpectedCommissionedEdgeIds);

    public IReadOnlyList<RealtimeTransition> Transitions
    {
        get => _transitions;
        init => _transitions = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> ExpectedCommissionedNodeIds
    {
        get => _expectedCommissionedNodeIds;
        init => _expectedCommissionedNodeIds = Freeze(value);
    }

    public IReadOnlyList<string> ExpectedCommissionedEdgeIds
    {
        get => _expectedCommissionedEdgeIds;
        init => _expectedCommissionedEdgeIds = Freeze(value);
    }

    private static IReadOnlyList<string> Freeze(IReadOnlyList<string> values) =>
        Array.AsReadOnly(values.OrderBy(value => value, StringComparer.Ordinal).ToArray());
}

internal sealed record RealtimeSliceCheckpointEvidence(
    string EvidenceLabel,
    string CheckpointId,
    long StartMinute,
    string StartCanonicalStateSha256,
    string CommandReplaySha256,
    long EndMinute,
    string EndCanonicalStateSha256,
    long EndPresentationRevision,
    int RenderedAssetCount,
    string HudClockText);

internal sealed record RealtimeSliceCheckpointWorldRenderFact(
    long Minute,
    string WorldSchemaVersion,
    string WorldId,
    string? SelectedAssetId,
    RealtimeTool Tool,
    RealtimeSurface Surface,
    IReadOnlyList<string> CommissionedNodeIds,
    IReadOnlyList<string> CommissionedEdgeIds,
    IReadOnlyList<string> DrawnStateCueAssetIds)
{
    private IReadOnlyList<string> _commissionedNodeIds = Freeze(CommissionedNodeIds);
    private IReadOnlyList<string> _commissionedEdgeIds = Freeze(CommissionedEdgeIds);
    private IReadOnlyList<string> _drawnStateCueAssetIds = Freeze(DrawnStateCueAssetIds);

    public IReadOnlyList<string> CommissionedNodeIds
    {
        get => _commissionedNodeIds;
        init => _commissionedNodeIds = Freeze(value);
    }

    public IReadOnlyList<string> CommissionedEdgeIds
    {
        get => _commissionedEdgeIds;
        init => _commissionedEdgeIds = Freeze(value);
    }

    public IReadOnlyList<string> DrawnStateCueAssetIds
    {
        get => _drawnStateCueAssetIds;
        init => _drawnStateCueAssetIds = Freeze(value);
    }

    private static IReadOnlyList<string> Freeze(IReadOnlyList<string> values) =>
        Array.AsReadOnly(values.OrderBy(value => value, StringComparer.Ordinal).ToArray());
}

/// <summary>
/// Debug-only evidence seam shared by every renderer that can host an A1 checkpoint.
/// It keeps the checkpoint controller independent of the current G3-backed R2 renderer.
/// </summary>
internal interface IRealtimeWorldCheckpointEvidenceView
{
    RealtimeSliceCheckpointWorldRenderFact CaptureTargetedCheckpointRenderFact();
}

internal sealed partial class RealtimeSliceMain
{
    private const string CheckpointReplaySchema =
        "gridworks.targeted-live-command-replay.v1";
    private const string AllowedCheckpointInput = "HUD_SPEED_NORMAL";
    private const long CheckpointFrameCount = 60;
    private const int CheckpointFramesPerSecond = 60;
    private const string ExpectedBaseWorldSourceSha256 =
        "c4923f752205c193efa78ddb4ca9e5431801731e6087be3ba3796abf9117ac14";
    private const string ExpectedBaseCampaignSourceSha256 =
        "19d042ca18cebec4d3164aba5932319ec123e1520283094fc3473eea548afd8c";
    private const string ExpectedRealtimeWorldSourceSha256 =
        "0d047c66063a9e925f1c0d0d6a19940956cbd3fb7e6a7d7be0df6035ff3d4ed0";
    private const string ExpectedRealtimeCampaignSourceSha256 =
        "e33510b49b32c127bca66ce14e755e48f75067cddc8b38d7d94b92b64d2c530a";
    private const string ExpectedRealtimeWorldDefinitionSha256 =
        "7bc7061a5564dbbbf0d98217c60e977ed20287f6b5da71f8153b6893a0923b60";
    private const string ExpectedRealtimeCampaignDefinitionSha256 =
        "4dc4dee6a9740e6b3babf1f9b2ccf9b8d107e541c9918e33a822ef4006163519";

    private RealtimeSliceCheckpointFact? _enteredTargetedCheckpoint;
    private InteractiveCheckpointState? _interactiveCheckpoint;
    private int _checkpointLiveAudioCueCountAtEntry;

    internal RealtimeSliceCheckpointFact EnterTargetedLiveCheckpoint(
        string checkpointId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);
        if (!RealtimeSliceCheckpointIds.IsKnown(checkpointId))
        {
            throw new ArgumentOutOfRangeException(
                nameof(checkpointId),
                checkpointId,
                $"Unknown targeted checkpoint. Expected one of: {string.Join(", ", RealtimeSliceCheckpointIds.All)}.");
        }
        EnsureBootstrapped();
        RealtimeSession session = RequireSession();
        if (!IsInsideTree() || _worldView is null || _ui is null)
        {
            throw new InvalidOperationException(
                "A targeted live checkpoint requires the actual RealtimeSliceMain scene tree.");
        }
        if (_enteredTargetedCheckpoint is not null)
        {
            throw new InvalidOperationException(
                "A RealtimeSliceMain instance may enter exactly one targeted checkpoint.");
        }

        SetProcess(false);
        Require(
            session.AcceptedCommandCount == 0 &&
            session.CurrentMinute == session.Data.Campaign.InitialSeed.StartMinute,
            "checkpoint entry did not begin from the exact embedded fixture baseline");
        Require(
            session.InteractionState.ActiveModalId == RealtimeR2Ids.ChapterBriefingModal &&
            session.InteractionState.Surface == RealtimeSurface.BlockingModal,
            "checkpoint entry did not begin at the authored chapter briefing boundary");

        RequireAccepted(
            ApplyIntent(RealtimeR2Intent.CloseModal(RealtimeR2Ids.ChapterBriefingModal)),
            "close chapter briefing");
        RealtimeAudio audio = AudioForSmoke;
        int? cueCountBeforeOrder = null;

        string stateCreationMethod;
        if (checkpointId == RealtimeSliceCheckpointIds.ConstructionDueOneMinute)
        {
            RealtimeSmokeLinePlan plan = _smokeLinePlan ??
                throw new InvalidOperationException("The exact fixture line plan is unavailable.");
            Require(plan.ExpectedCompletionMinute > plan.OrderMinute,
                "fixture line plan has no positive construction interval");
            Require(plan.Intents.Count == 3,
                "fixture line plan is not the bounded start/finish/order replay");
            _ = AdvanceToForSmoke(plan.OrderMinute);
            cueCountBeforeOrder = audio.LiveCuePlayCountForSmoke;
            foreach ((RealtimeR2Intent intent, int index) in
                     plan.Intents.Select((intent, index) => (intent, index)))
            {
                RequireAccepted(ApplyIntent(intent), $"construction replay intent {index}");
            }
            long dueMinute = checked(plan.ExpectedCompletionMinute - 1);
            _ = AdvanceToForSmoke(dueMinute);
            stateCreationMethod = string.Create(
                CultureInfo.InvariantCulture,
                $"EXACT_FIXTURE+CORE_ADVANCE_TO:{plan.OrderMinute}+REAL_CONTROLLER_COMMANDS:3+CORE_ADVANCE_TO:{dueMinute}");
        }
        else
        {
            stateCreationMethod = "EXACT_FIXTURE+NO_CORE_COMMANDS";
        }

        NormalizeCheckpointInteraction();
        Require(audio.AmbientStartCountForSmoke == 1,
            "checkpoint entry did not preserve the one current-R2 ambient start");
        if (checkpointId == RealtimeSliceCheckpointIds.ConstructionDueOneMinute)
        {
            Require(cueCountBeforeOrder.HasValue &&
                    audio.LiveCuePlayCountForSmoke == cueCountBeforeOrder.Value + 1 &&
                    audio.LastLiveCueForSmoke == RealtimeLiveAudioCue.Breaker &&
                    ReferenceEquals(
                        audio.SfxPlayerForSmoke.Stream,
                        audio.StreamForSmoke(RealtimeLiveAudioCue.Breaker)),
                "accepted construction order did not request exactly one Breaker SFX; " +
                $"before={cueCountBeforeOrder}; " +
                $"count={audio.LiveCuePlayCountForSmoke}; " +
                $"last={audio.LastLiveCueForSmoke}; " +
                $"actualStream={audio.SfxPlayerForSmoke.Stream?.GetInstanceId()}; " +
                $"expectedStream={audio.StreamForSmoke(RealtimeLiveAudioCue.Breaker).GetInstanceId()}");
        }
        else
        {
            Require(audio.LiveCuePlayCountForSmoke == 0,
                "normal checkpoint entry emitted an unrelated live SFX cue");
        }
        _checkpointLiveAudioCueCountAtEntry = audio.LiveCuePlayCountForSmoke;
        RealtimeCampaignSnapshot snapshot = session.CoreSnapshot;
        RealtimeSliceCheckpointFact fact = BuildCheckpointFact(
            checkpointId,
            stateCreationMethod,
            snapshot);
        VerifyFrozenCheckpointIdentity(fact);
        VerifyCheckpointEntry(fact);
        _enteredTargetedCheckpoint = fact;
        return fact;
    }

    internal RealtimeSliceCheckpointSegmentResult RunTargetedLiveCheckpointSegment(
        RealtimeSliceCheckpointFact checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        RealtimeSession session = RequireSession();
        Require(ReferenceEquals(checkpoint, _enteredTargetedCheckpoint),
            "checkpoint segment did not use the fact returned by this scene entry");
        VerifyCheckpointEntry(checkpoint);

        long startRevision = session.PresentationRevision;
        int startCommandCount = session.AcceptedCommandCount;
        IReadOnlyDictionary<RealtimePointerOwner, int> startClicks =
            FrozenClickCounters();

        _ui!.TopHudForSmoke.PressSpeedForSmoke(RealtimeSimulationSpeed.Normal);
        Require(
            session.InteractionState.Simulation == RealtimeSimulationState.Running &&
            session.InteractionState.RunningSpeed == RealtimeSimulationSpeed.Normal &&
            session.InteractionState.PauseReason == RealtimePauseReason.None,
            "the actual HUD speed signal did not resume the production controller");
        Require(session.PresentationRevision == startRevision + 1,
            "the resume input did not publish exactly one presentation");

        RealtimeR2FrameResult frame = InjectExactFrames(
            checkpoint.AllowedFrameCount,
            checkpoint.AllowedFramesPerSecond);
        Require(frame.RequestedFrameCount == CheckpointFrameCount &&
                frame.ConsumedFrameCount == CheckpointFrameCount &&
                frame.Frame is { AppliedMinutes: 1, CatchUpCeilingReached: false } &&
                frame.RetainedFrameDebt.Count == 0,
            "the bounded frame segment was not exactly one simulation minute");
        Require(session.CurrentMinute ==
                checkpoint.StartMinute + checkpoint.AllowedAdvanceMinutes,
            "the bounded frame segment ended at the wrong minute");
        Require(session.AcceptedCommandCount == startCommandCount,
            "no-click time flow appended a Core command");
        Require(startClicks.All(item => item.Value == 0) &&
                FrozenClickCounters().All(item => item.Value == 0),
            "the no-click segment routed a pointer click");
        Require(session.PresentationRevision == startRevision + 2 &&
                frame.PresentationRevisionDelta == 1,
            "resume plus one-minute frame did not publish exactly two presentations");

        RealtimeCampaignSnapshot end = session.CoreSnapshot;
        Require(string.Equals(session.CanonicalStateSha256,
                    checkpoint.ExpectedEndCanonicalStateSha256,
                    StringComparison.Ordinal),
            "checkpoint end canonical identity drifted from its frozen contract");
        VerifyCommonEnd(checkpoint, end);
        IReadOnlyList<string> expectedNodes =
            checkpoint.ActiveConstruction?.NodeIds ?? Array.Empty<string>();
        IReadOnlyList<string> expectedEdges =
            checkpoint.ActiveConstruction?.EdgeIds ?? Array.Empty<string>();
        if (checkpoint.CheckpointId == RealtimeSliceCheckpointIds.NormalReady)
        {
            VerifyNormalEnd(checkpoint, end, frame.Transitions);
        }
        else
        {
            VerifyConstructionEnd(
                checkpoint,
                end,
                frame.Transitions,
                expectedNodes,
                expectedEdges);
        }

        return new RealtimeSliceCheckpointSegmentResult(
            checkpoint,
            end.Minute,
            session.CanonicalStateSha256,
            startRevision,
            session.PresentationRevision,
            frame.Transitions,
            expectedNodes,
            expectedEdges);
    }

    internal void ArmInteractiveTargetedLiveCheckpoint(
        RealtimeSliceCheckpointFact checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        RealtimeSession session = RequireSession();
        Require(ReferenceEquals(checkpoint, _enteredTargetedCheckpoint),
            "interactive checkpoint did not use the fact returned by scene entry");
        Require(_interactiveCheckpoint is null,
            "interactive checkpoint was already armed");
        VerifyCheckpointEntry(checkpoint);
        _interactiveCheckpoint = new InteractiveCheckpointState(
            checkpoint,
            session.PresentationRevision,
            session.AcceptedCommandCount,
            FrozenClickCounters(),
            session.EmittedTransitions.Count);
        // Entry deliberately freezes the autonomous clock. The interactive host
        // re-enables the real production callback, but the paused accumulator
        // remains stable until the player chooses 1x through the production UI.
        SetProcess(true);
    }

    internal bool TryCompleteInteractiveTargetedLiveCheckpoint(
        out RealtimeSliceCheckpointEvidence? evidence)
    {
        evidence = null;
        InteractiveCheckpointState state = _interactiveCheckpoint ??
            throw new InvalidOperationException(
                "Interactive checkpoint completion was requested before arming.");
        RealtimeSession session = RequireSession();
        RealtimeSliceCheckpointFact checkpoint = state.Checkpoint;
        long expectedMinute = checked(
            checkpoint.StartMinute + checkpoint.AllowedAdvanceMinutes);
        if (session.CurrentMinute < expectedMinute)
        {
            return false;
        }

        SetProcess(false);
        Require(session.CurrentMinute == expectedMinute,
            "interactive production clock crossed more than the allowed minute");
        Require(session.AcceptedCommandCount == state.StartCommandCount,
            "interactive no-map-click time flow appended a Core command");
        Require(state.StartClicks.All(item => item.Value == 0) &&
                FrozenClickCounters().All(item => item.Value == 0),
            "interactive checkpoint routed a map pointer click");
        Require(session.InteractionState.Simulation == RealtimeSimulationState.Running &&
                session.InteractionState.RunningSpeed == RealtimeSimulationSpeed.Normal &&
                session.InteractionState.PauseReason == RealtimePauseReason.None,
            "interactive checkpoint did not advance from the production 1x state");
        Require(session.PresentationRevision == state.StartPresentationRevision + 2,
            "interactive 1x input plus one-minute boundary did not publish exactly two presentations");
        Require(session.RetainedFrameDebt.Count == 0 &&
                session.AccumulatorSnapshot is
                {
                    PendingWholeMinutes: 0,
                    FractionalMinuteUnits: 0,
                },
            "interactive checkpoint did not stop on the exact minute boundary");

        RealtimeCampaignSnapshot end = session.CoreSnapshot;
        string endHash = session.CanonicalStateSha256;
        Require(string.Equals(
                endHash,
                checkpoint.ExpectedEndCanonicalStateSha256,
                StringComparison.Ordinal),
            "interactive checkpoint end canonical identity drifted from its frozen contract");
        VerifyCommonEnd(checkpoint, end);
        RealtimeTransition[] transitions = session.EmittedTransitions
            .Skip(state.StartTransitionCount)
            .ToArray();
        IReadOnlyList<string> expectedNodes =
            checkpoint.ActiveConstruction?.NodeIds ?? Array.Empty<string>();
        IReadOnlyList<string> expectedEdges =
            checkpoint.ActiveConstruction?.EdgeIds ?? Array.Empty<string>();
        if (checkpoint.CheckpointId == RealtimeSliceCheckpointIds.NormalReady)
        {
            VerifyNormalEnd(checkpoint, end, transitions);
        }
        else
        {
            VerifyConstructionEnd(
                checkpoint,
                end,
                transitions,
                expectedNodes,
                expectedEdges);
        }

        var segment = new RealtimeSliceCheckpointSegmentResult(
            checkpoint,
            end.Minute,
            endHash,
            state.StartPresentationRevision,
            session.PresentationRevision,
            transitions,
            expectedNodes,
            expectedEdges);
        evidence = CompleteTargetedLiveCheckpoint(segment);
        _interactiveCheckpoint = null;
        return true;
    }

    private void StopInteractiveTargetAtBoundaryForDebug()
    {
        InteractiveCheckpointState? state = _interactiveCheckpoint;
        if (state is null || _session is null)
        {
            return;
        }
        RealtimeSession session = RequireSession();
        long expectedMinute = checked(
            state.Checkpoint.StartMinute + state.Checkpoint.AllowedAdvanceMinutes);
        if (session.CurrentMinute >= expectedMinute)
        {
            SetProcess(false);
        }
    }

    private long ClampInteractiveVirtualFramesAtBoundaryForDebug(
        long requestedFrames)
    {
        InteractiveCheckpointState? state = _interactiveCheckpoint;
        if (state is null || requestedFrames <= 0 || _session is null)
        {
            return requestedFrames;
        }
        RealtimeSession session = RequireSession();
        if (session.InteractionState.Simulation != RealtimeSimulationState.Running)
        {
            return requestedFrames;
        }
        long expectedMinute = checked(
            state.Checkpoint.StartMinute + state.Checkpoint.AllowedAdvanceMinutes);
        if (session.CurrentMinute >= expectedMinute)
        {
            return 0;
        }
        RealtimeFrameAccumulatorSnapshot timing = session.AccumulatorSnapshot;
        int unitsPerFrame = checked(
            RealtimeFrameAccumulator.UnitsPerMinute /
            CheckpointFramesPerSecond * (int)session.InteractionState.RunningSpeed);
        int unitsUntilBoundary = checked(
            RealtimeFrameAccumulator.UnitsPerMinute -
            timing.FractionalMinuteUnits);
        long framesUntilBoundary = Math.Max(
            1,
            (unitsUntilBoundary + unitsPerFrame - 1L) / unitsPerFrame);
        return Math.Min(requestedFrames, framesUntilBoundary);
    }

    internal RealtimeSliceCheckpointEvidence CompleteTargetedLiveCheckpoint(
        RealtimeSliceCheckpointSegmentResult segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        RealtimeSession session = RequireSession();
        Require(ReferenceEquals(segment.Checkpoint, _enteredTargetedCheckpoint),
            "checkpoint completion belongs to a different scene entry");
        Require(session.CurrentMinute == segment.EndMinute &&
                string.Equals(session.CanonicalStateSha256,
                    segment.EndCanonicalStateSha256,
                    StringComparison.Ordinal),
            "Core state changed while the renderer settled");
        Require(session.PresentationRevision == segment.EndPresentationRevision,
            "presentation changed while the renderer settled");

        IRealtimeWorldCheckpointEvidenceView evidenceView =
            _worldView as IRealtimeWorldCheckpointEvidenceView ??
            throw new InvalidOperationException(
                "The active world renderer does not implement targeted checkpoint evidence.");
        RealtimeSliceCheckpointWorldRenderFact render =
            evidenceView.CaptureTargetedCheckpointRenderFact();
        RealtimeSliceCheckpointHudRenderFact hud =
            _ui!.TopHudForSmoke.CaptureTargetedCheckpointHudFact();
        RealtimeSliceCheckpointFact checkpoint = segment.Checkpoint;
        Require(render.Minute == segment.EndMinute &&
                render.WorldSchemaVersion ==
                    session.CoreSnapshot.Construction.World.SchemaVersion &&
                render.WorldId == session.CoreSnapshot.Construction.World.WorldId,
            "the actual world renderer did not consume the final authoritative world");
        Require(render.SelectedAssetId == checkpoint.ExpectedSelectionId &&
                render.Tool == checkpoint.ExpectedTool &&
                render.Surface == checkpoint.ExpectedSurface,
            "the actual world renderer diverged from selection/tool/surface truth");
        Require(render.DrawnStateCueAssetIds.Count > 0,
            "the actual world draw path did not produce state-cue draw facts");
        Require(segment.ExpectedCommissionedNodeIds.All(id =>
                    render.CommissionedNodeIds.Contains(id, StringComparer.Ordinal) &&
                    render.DrawnStateCueAssetIds.Contains(id, StringComparer.Ordinal)) &&
                segment.ExpectedCommissionedEdgeIds.All(id =>
                    render.CommissionedEdgeIds.Contains(id, StringComparer.Ordinal) &&
                    render.DrawnStateCueAssetIds.Contains(id, StringComparer.Ordinal)),
            "the actual world draw path omitted a newly commissioned construction asset");
        Require(string.Equals(hud.ClockText, session.LatestPresentation.Hud.Clock,
                    StringComparison.Ordinal) &&
                hud.PressedSpeed == RealtimeSimulationSpeed.Normal,
            "the actual HUD did not consume the final clock/speed presentation");
        Require(session.LatestPresentation.CoreSnapshot.Minute == segment.EndMinute &&
                string.Equals(
                    RealtimeStateCanonicalizer.Sha256(
                        session.LatestPresentation.CoreSnapshot),
                    segment.EndCanonicalStateSha256,
                    StringComparison.Ordinal),
            "the final presentation does not carry the authoritative Core identity");
        RealtimeAudio audio = AudioForSmoke;
        if (checkpoint.CheckpointId == RealtimeSliceCheckpointIds.ConstructionDueOneMinute)
        {
            Require(audio.AmbientStartCountForSmoke == 1 &&
                    audio.LiveCuePlayCountForSmoke ==
                        _checkpointLiveAudioCueCountAtEntry + 1 &&
                    audio.LastLiveCueForSmoke == RealtimeLiveAudioCue.Outage &&
                    ReferenceEquals(
                        audio.SfxPlayerForSmoke.Stream,
                        audio.StreamForSmoke(RealtimeLiveAudioCue.Outage)),
                "mixed construction/emergency completion did not replace Breaker " +
                "with one highest-priority Outage SFX; " +
                $"entry={_checkpointLiveAudioCueCountAtEntry}; " +
                $"count={audio.LiveCuePlayCountForSmoke}; " +
                $"last={audio.LastLiveCueForSmoke}; " +
                $"actualStream={audio.SfxPlayerForSmoke.Stream?.GetInstanceId()}; " +
                $"expectedStream={audio.StreamForSmoke(RealtimeLiveAudioCue.Outage).GetInstanceId()}");
        }
        else
        {
            Require(audio.AmbientStartCountForSmoke == 1 &&
                    audio.LiveCuePlayCountForSmoke ==
                        _checkpointLiveAudioCueCountAtEntry,
                "normal checkpoint segment restarted ambience or emitted an SFX cue");
        }

        return new RealtimeSliceCheckpointEvidence(
            checkpoint.EvidenceLabel,
            checkpoint.CheckpointId,
            checkpoint.StartMinute,
            checkpoint.StartCanonicalStateSha256,
            checkpoint.CommandReplaySha256,
            segment.EndMinute,
            segment.EndCanonicalStateSha256,
            segment.EndPresentationRevision,
            render.DrawnStateCueAssetIds.Count,
            hud.ClockText);
    }

    private void NormalizeCheckpointInteraction()
    {
        RealtimeSession session = RequireSession();
        RequireAccepted(ApplyIntent(RealtimeR2Intent.Select(null)), "clear selection");
        RequireAccepted(ApplyIntent(RealtimeR2Intent.RestoreInspectTool()),
            "restore inspect tool");
        if (session.InteractionState.Surface != RealtimeSurface.World)
        {
            RequireAccepted(
                ApplyIntent(new RealtimeR2Intent(
                    RealtimeR2IntentKind.CloseSurface,
                    Surface: session.InteractionState.Surface)),
                "close auxiliary surface");
        }
        RequireAccepted(ApplyIntent(RealtimeR2Intent.SetPlayerPaused(true)),
            "stabilize checkpoint pause");
    }

    private RealtimeSliceCheckpointFact BuildCheckpointFact(
        string checkpointId,
        string stateCreationMethod,
        RealtimeCampaignSnapshot snapshot)
    {
        RealtimeSession session = RequireSession();
        RealtimeStateAuthority authority = snapshot.Authority;
        var fixture = new RealtimeSliceCheckpointFixtureFact(
            RealtimeSliceResources.BaseWorldResource,
            session.Data.BaseWorld.SchemaVersion,
            session.Data.BaseWorld.WorldId,
            session.Data.BaseWorldSha256,
            RealtimeSliceResources.BaseCampaignResource,
            session.Data.BaseCampaign.SchemaVersion,
            session.Data.BaseCampaign.CampaignId,
            session.Data.BaseCampaignSha256,
            RealtimeSliceResources.WorldResource,
            session.Data.World.SchemaVersion,
            session.Data.World.WorldId,
            session.Data.WorldSha256,
            authority.WorldDefinitionSha256,
            RealtimeSliceResources.CampaignResource,
            session.Data.Campaign.SchemaVersion,
            session.Data.Campaign.CampaignId,
            session.Data.CampaignSha256,
            authority.CampaignDefinitionSha256);
        RealtimeSliceCheckpointConstructionFact? construction =
            snapshot.Construction.ActiveConstruction is ActiveConstructionSnapshot active
                ? new RealtimeSliceCheckpointConstructionFact(
                    active.Kind,
                    active.CostCashUnit,
                    active.CompletionMinute,
                    active.NodeIds,
                    active.EdgeIds)
                : null;
        RealtimeSliceCheckpointEventFact[] events = snapshot.ActiveEventStates
            .Select(item => new RealtimeSliceCheckpointEventFact(
                item.EventId,
                checked(snapshot.ChapterStartMinute + item.Event.StartOffsetMinutes),
                checked(snapshot.ChapterStartMinute + item.Event.EndOffsetMinutes),
                item.Event.Priority))
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .ToArray();
        RealtimeSliceCheckpointDutyFact? duty = snapshot.ActiveDuty is null
            ? null
            : new RealtimeSliceCheckpointDutyFact(
                snapshot.ActiveDuty.ChapterId,
                snapshot.ActiveDuty.EventId,
                snapshot.ActiveDuty.SegmentStartMinute,
                snapshot.ActiveDuty.ClosedSegments.Count,
                snapshot.ActiveDuty.Incidents.Count);
        RealtimeSliceCheckpointThermalFact[] thermal = snapshot.Thermal.Assets
            .OrderBy(item => item.AssetId, StringComparer.Ordinal)
            .Select(item => new RealtimeSliceCheckpointThermalFact(
                item.AssetId,
                item.AssetKind,
                item.State,
                item.UsedKw,
                item.EmergencyExposureMinutes,
                item.AuthoredUnavailable,
                item.ProtectiveOutage,
                item.ProtectiveOutageUntilMinute))
            .ToArray();
        string endAssertion = checkpointId == RealtimeSliceCheckpointIds.NormalReady
            ? "HUD resume + 60/60 exact frames => minute+1; canonical/presentation identity aligned; no selection, draft, construction, or pointer click"
            : "HUD resume + 60/60 exact frames => one ConstructionCompleted transition before same-minute EventStarted; exact project nodes/edges commissioned in Core, presentation, and draw path";
        FrozenCheckpointIdentity expected = FrozenIdentity(checkpointId);
        return new RealtimeSliceCheckpointFact(
            checkpointId,
            fixture,
            stateCreationMethod,
            CheckpointReplaySchema,
            CommandReplaySha256(session.AcceptedCommands),
            session.AcceptedCommandCount,
            snapshot.Minute,
            session.CanonicalStateSha256,
            expected.EndCanonicalStateSha256,
            construction,
            Array.AsReadOnly(events),
            duty,
            Array.AsReadOnly(thermal),
            ExpectedSelectionId: null,
            ExpectedTimelineAnchorMinute: null,
            ExpectedWorldPresentationMinute: snapshot.Minute,
            ExpectedSurface: RealtimeSurface.World,
            ExpectedTool: RealtimeTool.Inspect,
            ExpectedSimulation: RealtimeSimulationState.PlayerPaused,
            ExpectedPauseReason: RealtimePauseReason.PlayerRequest,
            AllowedNextInput: AllowedCheckpointInput,
            AllowedFrameCount: CheckpointFrameCount,
            AllowedFramesPerSecond: CheckpointFramesPerSecond,
            AllowedAdvanceMinutes: 1,
            StartWorldNodeCount: snapshot.Construction.World.Nodes.Count,
            StartWorldEdgeCount: snapshot.Construction.World.Edges.Count,
            EndAssertion: endAssertion,
            EvidenceLabel: $"TARGETED_LIVE_CHECKPOINT_PASS:{checkpointId}");
    }

    private static FrozenCheckpointIdentity FrozenIdentity(string checkpointId) =>
        checkpointId switch
        {
            RealtimeSliceCheckpointIds.NormalReady => new FrozenCheckpointIdentity(
                StartMinute: 1020,
                StartCanonicalStateSha256:
                    "7094f631c89fe072800858a205d08358be07a6e0e7341b83026ff619fc03f9a3",
                CommandReplaySha256:
                    "4f4d3748681585f49eeb4291262db3c99676baba10913450c94d5e1eda9e1611",
                CommandCount: 0,
                EndCanonicalStateSha256:
                    "d61217a830053e59f9c75a69eef110da2604892baf9b52ea74cb04d406ad6fec"),
            RealtimeSliceCheckpointIds.ConstructionDueOneMinute =>
                new FrozenCheckpointIdentity(
                    StartMinute: 1259,
                    StartCanonicalStateSha256:
                        "3a00c6c937d130cc7574e3971403445cb036a26aecba6671e300e1398d4b9989",
                    CommandReplaySha256:
                        "9bd7c3226fd36396d9d9f7a8d81da25379cedb8e0e54441601bb7c89e947c65c",
                    CommandCount: 3,
                    EndCanonicalStateSha256:
                        "304b96410d7652db9928613fe77443d8d50e29efcb273ff8061c064f876f37f9"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(checkpointId), checkpointId, "Unknown checkpoint identity."),
        };

    private static void VerifyFrozenCheckpointIdentity(
        RealtimeSliceCheckpointFact checkpoint)
    {
        FrozenCheckpointIdentity expected = FrozenIdentity(checkpoint.CheckpointId);
        RealtimeSliceCheckpointFixtureFact fixture = checkpoint.Fixture;
        Require(
            fixture.BaseWorldSchemaVersion == "gridworks.commercial.world.v2" &&
            fixture.BaseWorldId == "CHEONGRYU_COMMERCIAL_WORLD" &&
            fixture.BaseWorldSourceSha256 == ExpectedBaseWorldSourceSha256 &&
            fixture.BaseCampaignSchemaVersion == "gridworks.release.campaign.v2" &&
            fixture.BaseCampaignId == "CHEONGRYU_RELEASE_CAMPAIGN" &&
            fixture.BaseCampaignSourceSha256 == ExpectedBaseCampaignSourceSha256 &&
            fixture.RealtimeWorldSchemaVersion == "gridworks.realtime.world.v3" &&
            fixture.RealtimeWorldId == "CHEONGRYU_COMMERCIAL_WORLD" &&
            fixture.RealtimeWorldSourceSha256 == ExpectedRealtimeWorldSourceSha256 &&
            fixture.RealtimeWorldDefinitionSha256 ==
                ExpectedRealtimeWorldDefinitionSha256 &&
            fixture.RealtimeCampaignSchemaVersion ==
                "gridworks.realtime.campaign.v3" &&
            fixture.RealtimeCampaignId == "CHEONGRYU_RELEASE_CAMPAIGN" &&
            fixture.RealtimeCampaignSourceSha256 ==
                ExpectedRealtimeCampaignSourceSha256 &&
            fixture.RealtimeCampaignDefinitionSha256 ==
                ExpectedRealtimeCampaignDefinitionSha256,
            "checkpoint fixture/schema/ID/source/definition authority drifted");
        Require(checkpoint.StartMinute == expected.StartMinute &&
                checkpoint.StartCanonicalStateSha256 ==
                    expected.StartCanonicalStateSha256 &&
                checkpoint.CommandReplaySchemaId == CheckpointReplaySchema &&
                checkpoint.CommandReplaySha256 == expected.CommandReplaySha256 &&
                checkpoint.CommandCount == expected.CommandCount &&
                checkpoint.ExpectedEndCanonicalStateSha256 ==
                    expected.EndCanonicalStateSha256,
            "checkpoint minute/start hash/replay hash/end hash drifted from its frozen contract");
    }

    private sealed record FrozenCheckpointIdentity(
        long StartMinute,
        string StartCanonicalStateSha256,
        string CommandReplaySha256,
        int CommandCount,
        string EndCanonicalStateSha256);

    private void VerifyCheckpointEntry(RealtimeSliceCheckpointFact checkpoint)
    {
        RealtimeSession session = RequireSession();
        RealtimeCampaignSnapshot snapshot = session.CoreSnapshot;
        Require(!IsProcessing(), "checkpoint setup left the autonomous clock enabled");
        Require(session.AccumulatorSnapshot is
            {
                Paused: true,
                PendingWholeMinutes: 0,
                FractionalMinuteUnits: 0,
            }, "checkpoint setup did not leave a stable exact frame accumulator");
        Require(snapshot.Minute == checkpoint.StartMinute &&
                string.Equals(session.CanonicalStateSha256,
                    checkpoint.StartCanonicalStateSha256,
                    StringComparison.Ordinal),
            "checkpoint start Core identity changed");
        Require(session.AcceptedCommandCount == checkpoint.CommandCount &&
                string.Equals(CommandReplaySha256(session.AcceptedCommands),
                    checkpoint.CommandReplaySha256,
                    StringComparison.Ordinal),
            "checkpoint command replay identity changed");
        Require(session.InteractionState.Simulation == checkpoint.ExpectedSimulation &&
                session.InteractionState.PauseReason == checkpoint.ExpectedPauseReason &&
                session.InteractionState.SelectionId == checkpoint.ExpectedSelectionId &&
                session.InteractionState.TimelineAnchorMinute ==
                    checkpoint.ExpectedTimelineAnchorMinute &&
                session.InteractionState.Surface == checkpoint.ExpectedSurface &&
                session.InteractionState.Tool == checkpoint.ExpectedTool &&
                session.InteractionState.ActiveModalId is null,
            "checkpoint interaction state is not paused, unselected, and world-stable");
        Require(snapshot.Construction.NodeDraft is null &&
                snapshot.Construction.LineDraft is null,
            "checkpoint setup retained a construction draft");
        Require(snapshot.ActiveEventStates.Count == checkpoint.ActiveEvents.Count &&
                snapshot.Thermal.Assets.Count == checkpoint.Thermal.Count,
            "checkpoint active event/thermal facts drifted after capture");
        Require(session.LatestPresentation.CoreSnapshot.Minute == checkpoint.StartMinute &&
                session.LatestPresentation.World.Minute ==
                    checkpoint.ExpectedWorldPresentationMinute &&
                session.LatestPresentation.World.SelectedAssetId ==
                    checkpoint.ExpectedSelectionId &&
                session.LatestPresentation.World.Surface == checkpoint.ExpectedSurface &&
                session.LatestPresentation.World.Tool == checkpoint.ExpectedTool,
            "checkpoint presentation is not anchored to its start identity");
        Require(FrozenClickCounters().All(item => item.Value == 0),
            "checkpoint setup routed a pointer click");

        if (checkpoint.CheckpointId == RealtimeSliceCheckpointIds.NormalReady)
        {
            Require(checkpoint.CommandCount == 0 &&
                    checkpoint.ActiveConstruction is null &&
                    snapshot.Construction.ActiveConstruction is null,
                "normal-ready checkpoint is not the command-free normal baseline");
        }
        else
        {
            RealtimeSliceCheckpointConstructionFact active =
                checkpoint.ActiveConstruction ?? throw new InvalidOperationException(
                    "construction-due checkpoint has no active project fact");
            Require(active.CompletionMinute == checkpoint.StartMinute + 1 &&
                    snapshot.Construction.ActiveConstruction is not null &&
                    snapshot.Construction.ActiveConstruction.CompletionMinute ==
                        active.CompletionMinute,
                "construction-due checkpoint is not exactly one minute before completion");
            Require(active.NodeIds.All(id => !snapshot.Construction.World.Nodes.Single(
                        item => string.Equals(item.NodeId, id, StringComparison.Ordinal))
                    .Commissioned) &&
                    active.EdgeIds.All(id => !snapshot.Construction.World.Edges.Single(
                        item => string.Equals(item.EdgeId, id, StringComparison.Ordinal))
                    .Commissioned),
                "construction-due checkpoint contains an already commissioned project asset");
        }
    }

    private void VerifyCommonEnd(
        RealtimeSliceCheckpointFact checkpoint,
        RealtimeCampaignSnapshot end)
    {
        RealtimeSession session = RequireSession();
        Require(session.InteractionState.Simulation == RealtimeSimulationState.Running &&
                session.InteractionState.RunningSpeed == RealtimeSimulationSpeed.Normal &&
                session.InteractionState.PauseReason == RealtimePauseReason.None &&
                session.InteractionState.SelectionId == checkpoint.ExpectedSelectionId &&
                session.InteractionState.TimelineAnchorMinute ==
                    checkpoint.ExpectedTimelineAnchorMinute &&
                session.InteractionState.Surface == checkpoint.ExpectedSurface &&
                session.InteractionState.Tool == checkpoint.ExpectedTool,
            "bounded segment changed selection/anchor/surface/tool unexpectedly");
        Require(end.Construction.NodeDraft is null &&
                end.Construction.LineDraft is null &&
                end.Construction.World.Nodes.Count == checkpoint.StartWorldNodeCount &&
                end.Construction.World.Edges.Count == checkpoint.StartWorldEdgeCount,
            "bounded segment changed draft or world object cardinality");
        Require(session.LatestPresentation.Revision == session.PresentationRevision &&
                session.LatestPresentation.CoreSnapshot.Minute == end.Minute &&
                session.LatestPresentation.World.Minute == end.Minute &&
                session.LatestPresentation.World.SelectedAssetId ==
                    checkpoint.ExpectedSelectionId &&
                session.LatestPresentation.World.Surface == checkpoint.ExpectedSurface &&
                session.LatestPresentation.World.Tool == checkpoint.ExpectedTool,
            "bounded segment did not publish the exact final world presentation");
        Require(string.Equals(
                RealtimeStateCanonicalizer.Sha256(
                    session.LatestPresentation.CoreSnapshot),
                session.CanonicalStateSha256,
                StringComparison.Ordinal),
            "bounded segment Core and presentation canonical hashes diverged");
    }

    private static void VerifyNormalEnd(
        RealtimeSliceCheckpointFact checkpoint,
        RealtimeCampaignSnapshot end,
        IReadOnlyList<RealtimeTransition> transitions)
    {
        Require(checkpoint.ActiveConstruction is null &&
                end.Construction.ActiveConstruction is null &&
                !transitions.Any(item =>
                    item.Kind == RealtimeTransitionKind.ConstructionCompleted),
            "normal-ready segment created or completed construction");
    }

    private void VerifyConstructionEnd(
        RealtimeSliceCheckpointFact checkpoint,
        RealtimeCampaignSnapshot end,
        IReadOnlyList<RealtimeTransition> transitions,
        IReadOnlyList<string> expectedNodes,
        IReadOnlyList<string> expectedEdges)
    {
        RealtimeSession session = RequireSession();
        RealtimeSliceCheckpointConstructionFact project =
            checkpoint.ActiveConstruction ?? throw new InvalidOperationException(
                "construction-due checkpoint lost its active construction fact");
        RealtimeTransition[] completions = transitions.Where(item =>
            item.Kind == RealtimeTransitionKind.ConstructionCompleted &&
            item.Minute == end.Minute).ToArray();
        Require(completions.Length == 1 &&
                completions[0].Construction is RealtimeConstructionCompletion completion &&
                completion.Kind == project.Kind &&
                completion.CompletionMinute == project.CompletionMinute &&
                completion.NodeIds.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(
                    expectedNodes.OrderBy(id => id, StringComparer.Ordinal),
                    StringComparer.Ordinal) &&
                completion.EdgeIds.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(
                    expectedEdges.OrderBy(id => id, StringComparer.Ordinal),
                    StringComparer.Ordinal),
            "construction-due segment did not emit the exact atomic completion");
        int completionIndex = transitions.ToList().FindIndex(item =>
            item.Kind == RealtimeTransitionKind.ConstructionCompleted &&
            item.Minute == end.Minute);
        int eventIndex = transitions.ToList().FindIndex(item =>
            item.Kind == RealtimeTransitionKind.EventStarted &&
            item.Minute == end.Minute);
        Require(completionIndex >= 0 && (eventIndex < 0 || completionIndex < eventIndex),
            "same-minute event started before construction commissioning");
        Require(end.Construction.ActiveConstruction is null &&
                expectedNodes.All(id => end.Construction.World.Nodes.Single(item =>
                    string.Equals(item.NodeId, id, StringComparison.Ordinal)).Commissioned) &&
                expectedEdges.All(id => end.Construction.World.Edges.Single(item =>
                    string.Equals(item.EdgeId, id, StringComparison.Ordinal)).Commissioned),
            "completed construction is not commissioned in authoritative Core world");
        Require(expectedNodes.All(id =>
                    session.LatestPresentation.World.World.Nodes.Single(item =>
                    string.Equals(item.NodeId, id, StringComparison.Ordinal)).Commissioned) &&
                expectedEdges.All(id =>
                    session.LatestPresentation.World.World.Edges.Single(item =>
                    string.Equals(item.EdgeId, id, StringComparison.Ordinal)).Commissioned),
            "completed construction is not commissioned in world presentation");
        Require(expectedEdges.All(id =>
                session.LatestPresentation.World.AssetStatuses.Single(item =>
                    string.Equals(item.AssetId, id, StringComparison.Ordinal)) is
                {
                    UsedKw: > 0,
                    State: not (RealtimeWorldAssetState.Planned or
                        RealtimeWorldAssetState.Building),
                }),
            "completed construction is not energized in the final world presentation");
    }

    private sealed record InteractiveCheckpointState(
        RealtimeSliceCheckpointFact Checkpoint,
        long StartPresentationRevision,
        int StartCommandCount,
        IReadOnlyDictionary<RealtimePointerOwner, int> StartClicks,
        int StartTransitionCount);

    private static string CommandReplaySha256(
        IReadOnlyList<TimedRealtimeCommand> commands)
    {
        var canonical = new StringBuilder();
        AppendField(canonical, "schema", CheckpointReplaySchema);
        AppendField(canonical, "count", commands.Count.ToString(CultureInfo.InvariantCulture));
        foreach (TimedRealtimeCommand timed in commands.OrderBy(item => item.Sequence))
        {
            RealtimeCommand command = timed.Command;
            AppendField(canonical, "sequence", timed.Sequence.ToString(CultureInfo.InvariantCulture));
            AppendField(canonical, "minute", timed.Minute.ToString(CultureInfo.InvariantCulture));
            AppendField(canonical, "kind", command.Kind.ToString());
            AppendField(canonical, "firstId", command.FirstId);
            AppendField(canonical, "secondId", command.SecondId);
            AppendField(canonical, "thirdId", command.ThirdId);
            AppendField(canonical, "xUnit", command.Position?.XUnit.ToString(
                CultureInfo.InvariantCulture));
            AppendField(canonical, "yUnit", command.Position?.YUnit.ToString(
                CultureInfo.InvariantCulture));
            AppendField(canonical, "pointIndex", command.PointIndex?.ToString(
                CultureInfo.InvariantCulture));
            AppendField(canonical, "promiseDecision", command.PromiseDecision?.ToString());
        }
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static void AppendField(StringBuilder target, string name, string? value)
    {
        string encoded = value ?? "";
        target.Append(name)
            .Append('=')
            .Append(encoded.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(encoded)
            .Append('\n');
    }

    private static void RequireAccepted(
        RealtimeR2IntentResult result,
        string operation)
    {
        Require(result.Accepted,
            $"{operation} was rejected: {result.Error ?? result.CoreCommandResult?.Error.ToString() ?? "unknown"}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

internal sealed partial class RealtimePlaceholderMap :
    IRealtimeWorldCheckpointEvidenceView
{
    RealtimeSliceCheckpointWorldRenderFact IRealtimeWorldCheckpointEvidenceView
        .CaptureTargetedCheckpointRenderFact()
    {
        RealtimeWorldPresentation presentation = _presentation ??
            throw new InvalidOperationException(
                "The actual world renderer has no checkpoint presentation.");
        return new RealtimeSliceCheckpointWorldRenderFact(
            presentation.Minute,
            presentation.World.SchemaVersion,
            presentation.World.WorldId,
            presentation.SelectedAssetId,
            presentation.Tool,
            presentation.Surface,
            presentation.World.Nodes
                .Where(item => item.Commissioned)
                .Select(item => item.NodeId)
                .ToArray(),
            presentation.World.Edges
                .Where(item => item.Commissioned)
                .Select(item => item.EdgeId)
                .ToArray(),
            _drawnStateCues.Keys.ToArray());
    }
}
#endif
