#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

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

internal sealed partial class RealtimeSliceMain
{
    private RealtimeSmokeLinePlan? _smokeLinePlan;
    private RealtimeSmokeBoundaryFacts? _smokeBoundaryFacts;
    private RealtimeInputRequest? _lastInputRequest;
    private bool _suppressFormativeDirectPlayOutputForSmoke;
    private RealtimeLaunchSelection? _launchOverrideForSmoke;

    /// <summary>
    /// Owns an off-tree smoke host. GodotObject.Dispose only releases the
    /// managed binding; Node.Free is required to release its native RID/object.
    /// </summary>
    internal IDisposable FreeAfterSmoke() => new SmokeLifetime(this);

    internal RealtimeUiRoot UiForSmoke => _ui ??
        throw new InvalidOperationException("Scene UI is not ready.");

    internal RealtimePlaceholderMap MapForSmoke => _worldView as RealtimePlaceholderMap ??
        throw new InvalidOperationException("The smoke scene is not using PlaceholderMap.");

    internal Rect2 MapInteractionRectForSmoke => _worldView is null
        ? throw new InvalidOperationException("Scene map is not ready.")
        : _worldView.InteractionRect;

    internal void ApplyMapInteractionRectForSmoke(Rect2 rect)
    {
        EnsureBootstrapped();
        ApplyMapInteractionRect(rect);
    }

    internal RealtimeComparisonDraftForecast ComparisonDraftForecastForSmoke =>
        Session.GetComparisonDraftForecast();

    internal IReadOnlyList<string> FormativeTutorialResultChapterIdsForSmoke =>
        Session.FormativeTutorialResultChapterIds;

    internal bool FormativeTutorialFullFlowRecordedForSmoke =>
        Session.FormativeTutorialFullFlowRecorded;

    internal RealtimeChapterStoryModalRequest? ActiveChapterStoryModalForSmoke =>
        Session.ActiveChapterStoryModal;

    internal RealtimeEpilogueModalRequest? ActiveEpilogueModalForSmoke =>
        Session.ActiveEpilogueModal;

    internal bool EpilogueCompletedForSmoke => Session.EpilogueCompleted;

    internal void RequestActionForSmoke(string actionId) => HandleAction(actionId);

    internal RealtimeModalPresentation? ClosePresentedStoryModalForSmoke()
    {
        RealtimeModalPresentation modal = Session.LatestPresentation.Modal ??
            throw new InvalidOperationException(
                "No story modal is available for the production close handler.");
        _suppressFormativeDirectPlayOutputForSmoke = true;
        try
        {
            HandleModalAction(modal.Id, modal.PrimaryAction.Id);
            return Session.LatestPresentation.Modal;
        }
        finally
        {
            _suppressFormativeDirectPlayOutputForSmoke = false;
        }
    }

    internal RealtimeForecastSnapshot ForecastForHorizonForSmoke(
        long horizonMinutes) =>
        Session.GetForecast(horizonMinutes);

    internal RealtimeSlicePresentation PresentSnapshotForSmoke(
        RealtimeCampaignSnapshot snapshot,
        RealtimeInteractionState? interaction = null,
        IReadOnlyList<RealtimeTransition>? transitionHistory = null)
    {
        EnsureBootstrapped();
        return RealtimeSlicePresenter.Present(new RealtimePresentationSource(
            Session.Data,
            snapshot,
            snapshot.Forecast,
            Session.GetComparisonDraftForecast(),
            interaction ?? Session.InteractionState,
            Session.PresentationRevision,
            new RealtimeWorldPointerFeedback(null, true, string.Empty),
            ReduceMotion: false,
            NodeOrderQuote: snapshot.Construction.NodeDraft is not null
                ? Session.PreviewNodeOrder()
                : null,
            LineOrderQuote: snapshot.Construction.LineDraft is { EndNodeId: not null }
                ? Session.PreviewLineOrder()
                : null,
            transitionHistory ?? Array.Empty<RealtimeTransition>(),
            ActiveStoryRequest: null,
            StoryResultAdvancesCalendar: false,
            SuccessfulStandaloneCompletion: false,
            ActiveEpilogueRequest: null));
    }

    internal RealtimeProjectQuote PreviewNodeOrderForSmoke()
    {
        EnsureBootstrapped();
        return Session.PreviewNodeOrder();
    }

    internal RealtimeProjectQuote PreviewLineOrderForSmoke()
    {
        EnsureBootstrapped();
        return Session.PreviewLineOrder();
    }

    internal (string ToolId, CoreMapPoint Position) AcceptedNodeDraftForSmoke()
    {
        EnsureBootstrapped();
        RealtimeCampaignSnapshot snapshot = Session.CoreSnapshot;
        string nodeClassId = snapshot.Chapter.Content.AvailableNodeClassIds
            .OrderBy(item => item, StringComparer.Ordinal)
            .First();
        SpatialNodeClassDefinition nodeClass = snapshot.Construction.World.NodeClasses
            .Single(item => string.Equals(
                item.ClassId,
                nodeClassId,
                StringComparison.Ordinal));
        MapBounds bounds = snapshot.Construction.World.Bounds;
        int inset = Math.Max(1, nodeClass.FootprintRadiusUnit);
        int step = Math.Max(1, inset);
        for (int y = bounds.MinYUnit + inset;
             y <= bounds.MaxYUnit - inset;
             y = checked(y + step))
        {
            for (int x = bounds.MinXUnit + inset;
                 x <= bounds.MaxXUnit - inset;
                 x = checked(x + step))
            {
                var point = new CoreMapPoint(x, y);
                if (Session.PreviewNodePlacement(nodeClassId, point).Accepted)
                {
                    return (RealtimeR2Ids.NodeTool(nodeClassId), point);
                }
            }
        }
        throw new InvalidOperationException(
            $"The embedded R1 fixture has no accepted {nodeClassId} smoke placement.");
    }

    internal CoreMapPoint RejectedNodeDraftForSmoke(string toolId)
    {
        EnsureBootstrapped();
        const string prefix = RealtimeR2Ids.NodeToolPrefix;
        if (!toolId.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("A node build-tool ID is required.", nameof(toolId));
        }
        string nodeClassId = toolId[prefix.Length..];
        MapBounds bounds = Session.CoreSnapshot.Construction.World.Bounds;
        CoreMapPoint[] boundaryPoints =
        [
            new(bounds.MinXUnit, bounds.MinYUnit),
            new(bounds.MinXUnit, bounds.MaxYUnit),
            new(bounds.MaxXUnit, bounds.MinYUnit),
            new(bounds.MaxXUnit, bounds.MaxYUnit),
        ];
        foreach (CoreMapPoint point in boundaryPoints)
        {
            if (!Session.PreviewNodePlacement(nodeClassId, point).Accepted)
            {
                return point;
            }
        }
        throw new InvalidOperationException(
            $"The embedded R1 fixture has no rejected {nodeClassId} boundary placement.");
    }

    /// <summary>
    /// Binds the live debug UI signal to the same production action handler used
    /// by <see cref="WireNodes"/>. The harness always detaches after one press.
    /// </summary>
    internal void AttachActionUiForSmoke(RealtimeUiRoot ui) =>
        ui.ActionRequested += HandleAction;

    internal void DetachActionUiForSmoke(RealtimeUiRoot ui) =>
        ui.ActionRequested -= HandleAction;

    internal void AttachInputUiForSmoke(RealtimeUiRoot ui) =>
        ui.InputRequested += HandleInputRequest;

    internal void DetachInputUiForSmoke(RealtimeUiRoot ui) =>
        ui.InputRequested -= HandleInputRequest;

    internal void AttachTimelineUiForSmoke(RealtimeUiRoot ui)
    {
        ui.TimelineItemsRequested += HandleTimelineItems;
        ui.TimelineHorizonDeltaRequested += HandleTimelineHorizonDelta;
        ui.TimelineNavigationRequested += HandleTimelineNavigation;
    }

    internal void DetachTimelineUiForSmoke(RealtimeUiRoot ui)
    {
        ui.TimelineItemsRequested -= HandleTimelineItems;
        ui.TimelineHorizonDeltaRequested -= HandleTimelineHorizonDelta;
        ui.TimelineNavigationRequested -= HandleTimelineNavigation;
    }

    internal void EnterCampaignEndedForSmoke()
    {
        EnsureBootstrapped();
        Session.EnterCampaignEndedForSmoke();
    }

    internal void RequestBuildToolForSmoke(string toolId) =>
        HandleBuildTool(toolId);

    internal void RequestShortcutForSmoke(RealtimeInputCommand command) =>
        HandleShortcut(command);

    internal void RequestInputForSmoke(RealtimeInputRequest request) =>
        HandleInputRequest(request);

    internal void FreezeAutonomousClockForSmoke()
    {
        EnsureBootstrapped();
        SetProcess(false);
    }

    internal bool AutonomousClockEnabledForSmoke => IsProcessing();

    internal void BootstrapForSmoke()
    {
        _launch = RealtimeLaunchSelection.TechnicalFixture;
        Bootstrap();
    }

    internal void UseTechnicalFixtureLaunchForSmoke()
    {
        if (IsInsideTree())
        {
            throw new InvalidOperationException(
                "A smoke launch override must be selected before entering the tree.");
        }
        _launchOverrideForSmoke = RealtimeLaunchSelection.TechnicalFixture;
    }

    internal void BootstrapNativeReleaseForSmoke(RealtimeNativeRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        _launch = RealtimeLaunchSelection.Native(route);
        Bootstrap();
    }

    internal bool ClosePresentedPrimaryModalForSmoke()
    {
        RealtimeModalPresentation modal = Session.LatestPresentation.Modal ??
            throw new InvalidOperationException(
                "No presented modal is available for the production close handler.");
        _suppressFormativeDirectPlayOutputForSmoke = true;
        try
        {
            Session.HandleModalAction(modal.Id, modal.PrimaryAction.Id);
            if (Session.LatestPresentation.Modal is not null)
            {
                throw new InvalidOperationException(
                    "Production modal close handler did not close the presented modal.");
            }
            return Session.FormativeDirectPlayRecorded;
        }
        finally
        {
            _suppressFormativeDirectPlayOutputForSmoke = false;
        }
    }

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
        int framesPerSecond) => InjectExactFrames(frameCount, framesPerSecond);

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

    internal RealtimeR2AdvanceResult AdvanceToForSmoke(long targetMinute) =>
        Session.AdvanceTo(targetMinute);

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
        string worldId = Session.CoreSnapshot.Construction.World.Nodes
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
        int beforeCommands = Session.AcceptedCommandCount;
        long beforeRevision = Session.PresentationRevision;
        RealtimePointerResolution resolution = RealtimePointerOwnerResolver.Resolve(probe);
        _clickCounters[resolution.Owner]++;
        if (resolution.Owner == RealtimePointerOwner.WorldCandidate)
        {
            _ = ApplyIntent(RealtimeR2Intent.Select(resolution.ResolvedId));
        }
        return new RealtimePointerProbeResult(
            resolution,
            beforeCommands,
            Session.AcceptedCommandCount,
            beforeRevision,
            Session.PresentationRevision,
            Session.InteractionState.SelectionId,
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
            _worldView?.CameraCenter ?? Vector2.Zero);
    }

    internal RealtimeMapCameraSnapshot CaptureCameraForSmoke() =>
        _worldView?.CaptureCamera() ?? new RealtimeMapCameraSnapshot(Vector2.Zero, 0);

    internal void RestoreCameraForSmoke(RealtimeMapCameraSnapshot camera) =>
        _worldView?.RestoreCamera(camera);

    internal RealtimeCampaignSnapshot CoreSnapshot => Session.CoreSnapshot;
    internal bool HasSessionForSmoke => _session is not null;
    internal RealtimeLaunchSelection LaunchForSmoke => _launch;
    internal bool WorldVisibleForSmoke => _worldControl?.Visible == true;
    internal CommercialWorldDefinition DisplayWorldForSmoke => Session.Data.BaseWorld;
    internal RealtimeWorldDefinition RealtimeWorldForSmoke => Session.Data.World;
    internal RealtimeSliceData SliceDataForSmoke => Session.Data;
    internal string CanonicalStateSha256 => Session.CanonicalStateSha256;
    internal RealtimeFrameAccumulatorSnapshot AccumulatorSnapshot =>
        Session.AccumulatorSnapshot;
    internal RealtimeInteractionState InteractionState => Session.InteractionState;
    internal int AcceptedCommandCount => _session?.AcceptedCommandCount ?? 0;
    internal long CommandSequence => _session?.CommandSequence ?? 1;
    internal long CurrentMinute => CoreSnapshot.Minute;
    internal long PresentationRevision => Session.PresentationRevision;
    internal RealtimeSlicePresentation LatestPresentation =>
        Session.LatestPresentation;
    internal IReadOnlyList<RealtimeTransition> EmittedTransitions =>
        Session.EmittedTransitions;
    internal IReadOnlyDictionary<RealtimePointerOwner, int> PointerClickCounters =>
        FrozenClickCounters();
    internal IReadOnlyList<RealtimeR2PendingFrameDebt> RetainedFrameDebt =>
        Session.RetainedFrameDebt;
    internal Vector2 CameraCenter => _worldView?.CameraCenter ?? Vector2.Zero;
    internal RealtimeR2InputOwnershipFacts InputOwnershipFacts => new(
        _lastInputRequest,
        _worldView?.IsPanning ?? false,
        FrozenClickCounters());
    internal RealtimeR2TimelineChooserFacts TimelineChooserFacts =>
        Session.TimelineChooserFacts;
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

    private sealed class SmokeLifetime : IDisposable
    {
        private RealtimeSliceMain? _slice;

        internal SmokeLifetime(RealtimeSliceMain slice) => _slice = slice;

        public void Dispose()
        {
            RealtimeSliceMain? slice = _slice;
            _slice = null;
            if (slice is not null && GodotObject.IsInstanceValid(slice))
            {
                slice.Free();
            }
        }
    }
}
#endif
