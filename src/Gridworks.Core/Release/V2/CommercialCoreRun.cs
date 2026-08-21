namespace Gridworks.Core.Release.V2;

public sealed class CommercialCoreRun
{
    private readonly CommercialWorldDefinition _world;
    private readonly CommercialCoreSliceDefinition? _slice;
    private readonly CommercialCampaignDefinition? _campaign;
    private readonly IReadOnlyList<CommercialCoreChapter> _chapters;
    private readonly bool _carryWorldAcrossChapters;
    private readonly bool _gatePromiseEmergencyByPhase;
    private readonly List<CommercialCoreCommand> _commands = [];
    private readonly List<ThermalIntervalResult> _committedPhaseResults = [];
    private readonly List<CommercialChapterResultRecord> _chapterResults = [];
    private readonly List<int> _chapterStartCommandCounts = [];

    private ConstructionSession _construction = null!;
    private int _chapterIndex;
    private int _windowIndex;
    private long _cashUnit;
    private PromiseDecision? _promiseDecision;
    private IReadOnlyList<ThermalAssetMemory> _thermalMemory = Array.Empty<ThermalAssetMemory>();
    private bool _campaignComplete;
    private int _chapterStartCommandCount;
    private int? _pendingProjectStartCommandCount;
    private int? _recentProjectCheckpointCommandCount;

    public CommercialCoreRun(
        CommercialWorldDefinition world,
        CommercialCoreSliceDefinition slice)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(slice);
        CommercialCoreLoader.Validate(slice, world);
        _world = world;
        _slice = slice;
        _chapters = slice.Chapters;
        _carryWorldAcrossChapters = false;
        _gatePromiseEmergencyByPhase = false;
        StartChapter(0, chapterStartCommandCount: 0);
    }

    public CommercialCoreRun(
        CommercialWorldDefinition world,
        CommercialCampaignDefinition campaign)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(campaign);
        CommercialCampaignLoader.Validate(campaign, world);
        _world = world;
        _campaign = campaign;
        _chapters = campaign.Chapters;
        _carryWorldAcrossChapters = true;
        _gatePromiseEmergencyByPhase = true;
        StartChapter(0, chapterStartCommandCount: 0);
    }

    public CommercialCoreSnapshot GetSnapshot()
    {
        CommercialCoreChapter chapter = CurrentChapter;
        return new CommercialCoreSnapshot(
            chapter,
            _chapterIndex,
            _chapters.Count,
            _campaignComplete ? null : chapter.DecisionWindows[_windowIndex],
            _windowIndex,
            _construction.GetSnapshot(),
            _cashUnit,
            _promiseDecision,
            _thermalMemory,
            _committedPhaseResults,
            _chapterResults,
            _campaignComplete,
            _commands.Count,
            _chapterStartCommandCount,
            _recentProjectCheckpointCommandCount,
            _chapterStartCommandCounts);
    }

    public IReadOnlyList<CommercialCoreCommand> GetCommands() =>
        Array.AsReadOnly(_commands.ToArray());

    public NodePlacementPreview PreviewNodePlacement(string nodeClassId, MapPoint position) =>
        _construction.PreviewNodePlacement(nodeClassId, position);

    public LineStartPreview PreviewLineStart(
        string startNodeId,
        string lineClassId,
        string poleClassId) =>
        _construction.PreviewLineStart(startNodeId, lineClassId, poleClassId);

    public LinePointPreview PreviewLinePoint(MapPoint position) =>
        _construction.PreviewLinePoint(position);

    public LinePointMovePreview PreviewMoveLinePoint(int pointIndex, MapPoint position) =>
        _construction.PreviewMoveLinePoint(pointIndex, position);

    public LineFinishPreview PreviewLineFinish(string endNodeId) =>
        _construction.PreviewLineFinish(endNodeId);

    public ConstructionQuote PreviewNodeOrder() => _construction.PreviewNodeOrder();

    public ConstructionQuote PreviewLineOrder() => _construction.PreviewLineOrder();

    public CommercialCoreCommandResult Apply(CommercialCoreCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Execute(command, record: true);
    }

    public CommercialDecisionPreview PreviewDecisionWindow(bool includeCompleteDraft = true)
    {
        if (_campaignComplete)
        {
            return PreviewRejected(CommercialCoreError.CampaignComplete);
        }
        if (_construction.GetSnapshot().Phase is
            ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding)
        {
            return PreviewRejected(CommercialCoreError.WrongPhase);
        }
        if (CurrentChapter.Promise is not null && _promiseDecision is null)
        {
            return PreviewRejected(CommercialCoreError.PromiseDecisionRequired);
        }

        (CommercialWorldDefinition candidateWorld, long candidateMinute, long candidateCash) =
            PreviewWorld(includeCompleteDraft);
        long deadline = CurrentWindowDeadline();
        if (candidateMinute > deadline)
        {
            return new CommercialDecisionPreview(
                false,
                CommercialCoreError.DeadlineExceeded,
                candidateMinute,
                candidateCash,
                Array.Empty<ThermalIntervalResult>(),
                null,
                null,
                null);
        }

        IReadOnlyList<CommercialCoreOperatingPhase> phases = PhasesForCurrentWindow();
        IReadOnlyList<CommercialCoreOperatingPhase> evaluationPhases =
            EvaluationPhasesForCurrentWindow();
        ThermalSequenceRequest request = BuildThermalRequest(candidateWorld, evaluationPhases);
        ThermalSequenceResult evaluation = ThermalEvaluator.Preview(candidateWorld, request);
        for (int phaseIndex = 0; phaseIndex < evaluation.Intervals.Count; phaseIndex++)
        {
            ThermalIntervalResult phaseResult = evaluation.Intervals[phaseIndex];
            CommercialCoreOperatingPhase phase = evaluationPhases[phaseIndex];
            for (int index = 0; index < phase.Loads.Count; index++)
            {
                CommercialCoreLoadBundle load = phase.Loads[index];
                ThermalDemandResult demand = phaseResult.Demands[index];
                if (load.ObligationKind == CommercialCoreObligationKind.MustSupply && !demand.Supplied)
                {
                    return PreviewFailure(
                        CommercialCoreError.SafetyDutyFailed,
                        candidateMinute,
                        candidateCash,
                        evaluation.Intervals.Take(phases.Count).ToArray(),
                        demand);
                }
                if (load.ObligationKind == CommercialCoreObligationKind.CityPromise &&
                    _promiseDecision == PromiseDecision.Keep &&
                    !demand.Supplied)
                {
                    return PreviewFailure(
                        CommercialCoreError.KeptPromiseFailed,
                        candidateMinute,
                        candidateCash,
                        evaluation.Intervals.Take(phases.Count).ToArray(),
                        demand);
                }
            }
        }
        return new CommercialDecisionPreview(
            true,
            null,
            candidateMinute,
            candidateCash,
            evaluation.Intervals.Take(phases.Count).ToArray(),
            null,
            null,
            null);
    }

    public CommercialCoreCommandResult RollbackRecentProject()
    {
        if (_recentProjectCheckpointCommandCount is not int checkpoint ||
            checkpoint < _chapterStartCommandCount)
        {
            return Rejected(CommercialCoreError.NothingToRollback);
        }
        RebuildFromPrefix(checkpoint);
        return Accepted();
    }

    public CommercialCoreCommandResult RestartChapter()
    {
        RebuildFromPrefix(_chapterStartCommandCount);
        return Accepted();
    }

    public static CommercialCoreRun Restore(
        CommercialWorldDefinition world,
        CommercialCoreSliceDefinition slice,
        IReadOnlyList<CommercialCoreCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var run = new CommercialCoreRun(world, slice);
        for (int index = 0; index < commands.Count; index++)
        {
            CommercialCoreCommandResult result = run.Execute(commands[index], record: true);
            if (!result.Accepted)
            {
                throw new CommercialCoreReplayException(
                    $"Command {index} was rejected during fresh replay: {result.Error}/{result.ConstructionError}.");
            }
        }
        return run;
    }

    public static CommercialCoreRun Restore(
        CommercialWorldDefinition world,
        CommercialCampaignDefinition campaign,
        IReadOnlyList<CommercialCoreCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var run = new CommercialCoreRun(world, campaign);
        for (int index = 0; index < commands.Count; index++)
        {
            CommercialCoreCommandResult result = run.Execute(commands[index], record: true);
            if (!result.Accepted)
            {
                throw new CommercialCoreReplayException(
                    $"Command {index} was rejected during fresh replay: {result.Error}/{result.ConstructionError}.");
            }
        }
        return run;
    }

    private CommercialCoreCommandResult Execute(CommercialCoreCommand command, bool record)
    {
        if (_campaignComplete)
        {
            return Rejected(CommercialCoreError.CampaignComplete);
        }
        if (!HasExactShape(command))
        {
            return Rejected(CommercialCoreError.InvalidCommand);
        }

        CommercialCoreCommandResult result = command.Kind switch
        {
            CommercialCoreCommandKind.SetNodeDraft => SetNodeDraft(command),
            CommercialCoreCommandKind.CancelNodeDraft => Construction(
                _construction.CancelNodeDraft(),
                onAccepted: () => _pendingProjectStartCommandCount = null),
            CommercialCoreCommandKind.StartLineDraft => StartLineDraft(command),
            CommercialCoreCommandKind.AddLinePoint => Construction(
                command.Position is MapPoint point
                    ? _construction.AddLinePoint(point)
                    : InvalidConstructionResult()),
            CommercialCoreCommandKind.MoveLinePoint => Construction(
                command.Position is MapPoint point && command.PointIndex is int index
                    ? _construction.MoveLinePoint(index, point)
                    : InvalidConstructionResult()),
            CommercialCoreCommandKind.FinishLineDraft => Construction(
                command.EndNodeId is string endNodeId
                    ? _construction.FinishLineDraft(endNodeId)
                    : InvalidConstructionResult()),
            CommercialCoreCommandKind.UndoLinePoint => Construction(_construction.UndoLinePoint()),
            CommercialCoreCommandKind.CancelLineDraft => Construction(
                _construction.CancelLineDraft(),
                onAccepted: () => _pendingProjectStartCommandCount = null),
            CommercialCoreCommandKind.OrderNode => OrderNode(),
            CommercialCoreCommandKind.OrderLine => OrderLine(),
            CommercialCoreCommandKind.AdvanceConstruction => AdvanceConstruction(),
            CommercialCoreCommandKind.SetPromiseDecision => SetPromiseDecision(command),
            CommercialCoreCommandKind.ApproveDecisionWindow => ApproveDecisionWindow(),
            _ => Rejected(CommercialCoreError.InvalidCommand),
        };

        if (result.Accepted && record)
        {
            _commands.Add(command);
            result = result with { Snapshot = GetSnapshot() };
        }
        return result;
    }

    private CommercialCoreCommandResult SetNodeDraft(CommercialCoreCommand command)
    {
        if (command.Position is not MapPoint position || command.NodeClassId is null)
        {
            return Rejected(CommercialCoreError.InvalidCommand);
        }
        bool beginsProject = _construction.GetSnapshot().Phase == ConstructionPhase.Ready;
        CommercialCoreCommandResult result = Construction(
            _construction.SetNodeDraft(command.NodeClassId, position));
        if (result.Accepted && beginsProject)
        {
            _pendingProjectStartCommandCount = _commands.Count;
        }
        return result;
    }

    private CommercialCoreCommandResult StartLineDraft(CommercialCoreCommand command)
    {
        if (command.StartNodeId is null || command.LineClassId is null || command.PoleClassId is null)
        {
            return Rejected(CommercialCoreError.InvalidCommand);
        }
        CommercialCoreCommandResult result = Construction(_construction.StartLineDraft(
            command.StartNodeId,
            command.LineClassId,
            command.PoleClassId));
        if (result.Accepted)
        {
            _pendingProjectStartCommandCount = _commands.Count;
        }
        return result;
    }

    private CommercialCoreCommandResult OrderNode()
    {
        ConstructionQuote quote = _construction.PreviewNodeOrder();
        return Order(quote, _construction.OrderNode);
    }

    private CommercialCoreCommandResult OrderLine()
    {
        ConstructionQuote quote = _construction.PreviewLineOrder();
        return Order(quote, _construction.OrderLine);
    }

    private CommercialCoreCommandResult Order(
        ConstructionQuote quote,
        Func<ConstructionCommandResult> order)
    {
        if (!quote.Accepted)
        {
            return Rejected(CommercialCoreError.ConstructionRejected, quote.Error);
        }
        if (_cashUnit < quote.CostCashUnit!.Value)
        {
            return Rejected(CommercialCoreError.InsufficientCash);
        }
        ConstructionCommandResult constructionResult = order();
        if (!constructionResult.Accepted)
        {
            return Rejected(CommercialCoreError.ConstructionRejected, constructionResult.Error);
        }
        _cashUnit = checked(_cashUnit - quote.CostCashUnit.Value);
        return Accepted();
    }

    private CommercialCoreCommandResult AdvanceConstruction()
    {
        ConstructionPhase before = _construction.GetSnapshot().Phase;
        ConstructionCommandResult constructionResult = _construction.AdvanceToConstructionCompletion();
        if (!constructionResult.Accepted)
        {
            return Rejected(CommercialCoreError.ConstructionRejected, constructionResult.Error);
        }
        if (before is ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding)
        {
            _recentProjectCheckpointCommandCount =
                _pendingProjectStartCommandCount ?? _commands.Count;
            _pendingProjectStartCommandCount = null;
        }
        return Accepted();
    }

    private CommercialCoreCommandResult SetPromiseDecision(CommercialCoreCommand command)
    {
        if (CurrentChapter.Promise is null || command.PromiseDecision is null)
        {
            return Rejected(CommercialCoreError.InvalidCommand);
        }
        if (_construction.GetSnapshot().Phase is ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding)
        {
            return Rejected(CommercialCoreError.WrongPhase);
        }
        if (_windowIndex != 0)
        {
            return Rejected(CommercialCoreError.WrongPhase);
        }
        _promiseDecision = command.PromiseDecision;
        return Accepted();
    }

    private CommercialCoreCommandResult ApproveDecisionWindow()
    {
        if (_construction.GetSnapshot().Phase != ConstructionPhase.Ready)
        {
            return Rejected(CommercialCoreError.WrongPhase);
        }
        CommercialDecisionPreview preview = PreviewDecisionWindow(includeCompleteDraft: false);
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value, decisionPreview: preview);
        }

        _committedPhaseResults.AddRange(preview.PhaseResults);
        _thermalMemory = preview.PhaseResults[^1].NextAssetMemory;
        _windowIndex++;
        CommercialChapterResultRecord? completed = null;
        if (_windowIndex == CurrentChapter.DecisionWindows.Count)
        {
            completed = CompleteChapter();
        }
        return Accepted(preview, completed);
    }

    private CommercialChapterResultRecord CompleteChapter()
    {
        CommercialCoreChapter chapter = CurrentChapter;
        CommercialStoryCard story = _promiseDecision switch
        {
            PromiseDecision.Keep => chapter.KeptResult!,
            PromiseDecision.Defer => chapter.DeferredResult!,
            _ => chapter.StandardResult,
        };
        ThermalIntervalResult[] chapterPhases = _committedPhaseResults
            .Where(result => chapter.OperatingPhases.Any(phase => phase.PhaseId == result.IntervalId))
            .ToArray();
        CommercialResultDemandFact[] demandFacts = chapterPhases
            .SelectMany(result =>
            {
                CommercialCoreOperatingPhase phase = chapter.OperatingPhases.Single(item =>
                    item.PhaseId == result.IntervalId);
                return result.Demands.Select((demand, index) =>
                {
                    CommercialCoreLoadBundle load = phase.Loads[index];
                    return new CommercialResultDemandFact(
                        result.IntervalId,
                        demand.DemandId,
                        load.NodeId,
                        load.ObligationKind,
                        demand.Supplied,
                        demand.Deferred,
                        demand.SourceNodeId,
                        demand.PathNodeIds,
                        demand.PathEdgeIds,
                        demand.EmergencyAssetIds,
                        demand.Failure,
                        demand.FirstBottleneckAssetId);
                });
            })
            .ToArray();
        var record = new CommercialChapterResultRecord(
            chapter.ChapterId,
            chapter.DisplayName,
            story,
            _promiseDecision,
            demandFacts,
            chapterPhases.SelectMany(item => item.Demands)
                .Where(item => item.Supplied)
                .Select(item => item.DemandId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            chapterPhases.SelectMany(item => item.Demands)
                .Where(item => item.Supplied && item.SourceNodeId is not null)
                .Select(item => item.SourceNodeId!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            chapterPhases.SelectMany(item => item.Assets)
                .Where(item => item.CurrentState == ThermalOperatingState.Emergency)
                .Select(item => item.AssetId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            chapterPhases.SelectMany(item => item.Assets)
                .Where(item => item.CurrentState == ThermalOperatingState.ProtectiveOutage)
                .Select(item => item.AssetId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            _cashUnit);
        _chapterResults.Add(record);

        if (_chapterIndex + 1 == _chapters.Count)
        {
            _campaignComplete = true;
        }
        else
        {
            StartChapter(_chapterIndex + 1, _commands.Count + 1);
        }
        return record;
    }

    private void StartChapter(int chapterIndex, int chapterStartCommandCount)
    {
        if (_chapterStartCommandCounts.Count == chapterIndex)
        {
            _chapterStartCommandCounts.Add(chapterStartCommandCount);
        }
        SpatialWorldDefinition? carriedWorld = _carryWorldAcrossChapters && chapterIndex > 0
            ? _construction.GetSnapshot().World
            : null;
        _chapterIndex = chapterIndex;
        _windowIndex = 0;
        _promiseDecision = null;
        if (carriedWorld is null)
        {
            _thermalMemory = Array.Empty<ThermalAssetMemory>();
        }
        _recentProjectCheckpointCommandCount = null;
        _pendingProjectStartCommandCount = null;
        _chapterStartCommandCount = chapterStartCommandCount;
        CommercialCoreChapter chapter = CurrentChapter;
        if (chapter.ResetThermalMemoryAtStart)
        {
            _thermalMemory = Array.Empty<ThermalAssetMemory>();
        }
        if (carriedWorld is null)
        {
            CommercialWorldDefinition chapterWorld = _campaign is null
                ? CommercialCoreLoader.CreateChapterWorld(_world, chapter)
                : CommercialCampaignLoader.CreateInitialWorld(_world, _campaign);
            _cashUnit = checked(chapter.SeedCashUnit + chapter.GrantCashUnit);
            _construction = new ConstructionSession(chapterWorld.Spatial with
            {
                InitialCashUnit = _cashUnit,
            });
        }
        else
        {
            _cashUnit = checked(_cashUnit + chapter.GrantCashUnit);
            SpatialWorldDefinition activated = CommercialCampaignLoader.ActivateChapterAssets(
                _world,
                carriedWorld,
                chapter);
            _construction = new ConstructionSession(activated with
            {
                InitialCashUnit = _cashUnit,
            });
        }
    }

    private (CommercialWorldDefinition World, long Minute, long Cash) PreviewWorld(bool includeDraft)
    {
        ConstructionSnapshot snapshot = _construction.GetSnapshot();
        if (!includeDraft || snapshot.Phase == ConstructionPhase.Ready ||
            snapshot.Phase is ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding)
        {
            return (CurrentCommercialWorld(), snapshot.Minute, _cashUnit);
        }

        var previewSession = new ConstructionSession(snapshot.World);
        ConstructionQuote quote;
        ConstructionCommandResult result;
        if (snapshot.NodeDraft is NodeDraftSnapshot nodeDraft)
        {
            result = previewSession.SetNodeDraft(nodeDraft.NodeClassId, nodeDraft.Position);
            if (!result.Accepted) return (CurrentCommercialWorld(), snapshot.Minute, _cashUnit);
            quote = previewSession.PreviewNodeOrder();
            if (!quote.Accepted || _cashUnit < quote.CostCashUnit!.Value)
            {
                return (CurrentCommercialWorld(), snapshot.Minute, _cashUnit);
            }
            result = previewSession.OrderNode();
        }
        else if (snapshot.LineDraft is LineDraftSnapshot lineDraft && lineDraft.EndNodeId is not null)
        {
            result = previewSession.StartLineDraft(
                lineDraft.StartNodeId,
                lineDraft.LineClassId,
                lineDraft.PoleClassId);
            if (!result.Accepted) return (CurrentCommercialWorld(), snapshot.Minute, _cashUnit);
            foreach (MapPoint point in lineDraft.IntermediatePoints)
            {
                result = previewSession.AddLinePoint(point);
                if (!result.Accepted) return (CurrentCommercialWorld(), snapshot.Minute, _cashUnit);
            }
            result = previewSession.FinishLineDraft(lineDraft.EndNodeId);
            if (!result.Accepted) return (CurrentCommercialWorld(), snapshot.Minute, _cashUnit);
            quote = previewSession.PreviewLineOrder();
            if (!quote.Accepted || _cashUnit < quote.CostCashUnit!.Value)
            {
                return (CurrentCommercialWorld(), snapshot.Minute, _cashUnit);
            }
            result = previewSession.OrderLine();
        }
        else
        {
            return (CurrentCommercialWorld(), snapshot.Minute, _cashUnit);
        }
        if (!result.Accepted || !previewSession.AdvanceToConstructionCompletion().Accepted)
        {
            return (CurrentCommercialWorld(), snapshot.Minute, _cashUnit);
        }
        ConstructionSnapshot projected = previewSession.GetSnapshot();
        CommercialWorldDefinition world = CurrentCommercialWorld() with { Spatial = projected.World };
        return (
            world,
            checked(snapshot.Minute + projected.Minute),
            checked(_cashUnit - quote.CostCashUnit!.Value));
    }

    private ThermalSequenceRequest BuildThermalRequest(
        CommercialWorldDefinition candidateWorld,
        IReadOnlyList<CommercialCoreOperatingPhase> phases)
    {
        List<ThermalIntervalDefinition> intervals = [];
        foreach (CommercialCoreOperatingPhase phase in phases)
        {
            HashSet<string> unavailable = phase.UnavailableAssetIds.ToHashSet(StringComparer.Ordinal);
            foreach (string riskAreaId in phase.ActiveRiskAreaIds)
            {
                AddRiskAssets(candidateWorld.Spatial, riskAreaId, unavailable);
            }
            intervals.Add(new ThermalIntervalDefinition(
                phase.PhaseId,
                phase.DisplayName,
                phase.Policy,
                phase.Loads.Select(load => new ThermalDemandDefinition(
                    load.LoadId,
                    load.DisplayName,
                    load.NodeId,
                    load.DemandKw,
                    load.ObligationKind switch
                    {
                        CommercialCoreObligationKind.MustSupply => ThermalObligationKind.SafetyDuty,
                        CommercialCoreObligationKind.CityPromise => ThermalObligationKind.CityPromise,
                        _ => ThermalObligationKind.OperatingRecord,
                    },
                    load.ObligationKind != CommercialCoreObligationKind.CityPromise ||
                        _promiseDecision == PromiseDecision.Keep,
                    load.ObligationKind == CommercialCoreObligationKind.CityPromise &&
                        _promiseDecision == PromiseDecision.Keep &&
                        (!_gatePromiseEmergencyByPhase ||
                            phase.Policy == ThermalIntervalPolicy.SafetyEmergencyAllowed),
                    load.NamedEmergencyDuty,
                    load.RequireSubstationPath)).ToArray(),
                unavailable.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                phase.LimitOverrides));
        }
        return new ThermalSequenceRequest(intervals, _thermalMemory);
    }

    private static void AddRiskAssets(
        SpatialWorldDefinition world,
        string riskAreaId,
        ISet<string> unavailable)
    {
        SpatialRiskAreaDefinition risk = world.RiskAreas.Single(item => item.RiskAreaId == riskAreaId);
        Dictionary<string, SpatialNodeClassDefinition> classes = world.NodeClasses.ToDictionary(
            item => item.ClassId,
            StringComparer.Ordinal);
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        foreach (SpatialNodeDefinition node in world.Nodes.Where(item => item.Commissioned))
        {
            if (FixedGeometry.CircleIntersectsPolygon(
                    node.Position,
                    classes[node.ClassId].FootprintRadiusUnit,
                    risk.Polygon))
            {
                unavailable.Add(node.NodeId);
            }
        }
        foreach (SpatialEdgeDefinition edge in world.Edges.Where(item => item.Commissioned))
        {
            if (FixedGeometry.SegmentIntersectsPolygon(
                    nodes[edge.FromNodeId].Position,
                    nodes[edge.ToNodeId].Position,
                    risk.Polygon))
            {
                unavailable.Add(edge.EdgeId);
            }
        }
    }

    private IReadOnlyList<CommercialCoreOperatingPhase> PhasesForCurrentWindow()
    {
        CommercialCoreChapter chapter = CurrentChapter;
        string startId = chapter.DecisionWindows[_windowIndex].NextPhaseId;
        int start = chapter.OperatingPhases.ToList().FindIndex(item => item.PhaseId == startId);
        int end = chapter.OperatingPhases.Count;
        if (_windowIndex + 1 < chapter.DecisionWindows.Count)
        {
            string endId = chapter.DecisionWindows[_windowIndex + 1].NextPhaseId;
            end = chapter.OperatingPhases.ToList().FindIndex(item => item.PhaseId == endId);
        }
        return chapter.OperatingPhases.Skip(start).Take(end - start).ToArray();
    }

    private IReadOnlyList<CommercialCoreOperatingPhase> EvaluationPhasesForCurrentWindow()
    {
        IReadOnlyList<CommercialCoreOperatingPhase> committed = PhasesForCurrentWindow();
        CommercialCoreChapter chapter = CurrentChapter;
        int start = chapter.OperatingPhases.ToList().FindIndex(item =>
            item.PhaseId == committed[0].PhaseId);
        int count = committed.Count;
        if (start + count < chapter.OperatingPhases.Count)
        {
            count++;
        }
        return chapter.OperatingPhases.Skip(start).Take(count).ToArray();
    }

    private long CurrentWindowDeadline()
    {
        long allowance = 0;
        bool hasAllowance = false;
        for (int index = 0; index <= _windowIndex; index++)
        {
            if (CurrentChapter.DecisionWindows[index].BuildMinutesAllowance is int value)
            {
                allowance = checked(allowance + value);
                hasAllowance = true;
            }
        }
        return hasAllowance ? Math.Min(CurrentChapter.DeadlineMinute, allowance) : CurrentChapter.DeadlineMinute;
    }

    private CommercialWorldDefinition CurrentCommercialWorld() =>
        CommercialCampaignLoader.WorldForSpatial(_world, _construction.GetSnapshot().World);

    private CommercialCoreCommandResult Construction(
        ConstructionCommandResult result,
        Action? onAccepted = null)
    {
        if (!result.Accepted)
        {
            return Rejected(CommercialCoreError.ConstructionRejected, result.Error);
        }
        onAccepted?.Invoke();
        return Accepted();
    }

    private ConstructionCommandResult InvalidConstructionResult() => new(
        false,
        ConstructionError.WrongPhase,
        _construction.GetSnapshot());

    private static bool HasExactShape(CommercialCoreCommand command)
    {
        bool position = command.Position is not null;
        bool nodeClass = command.NodeClassId is not null;
        bool startNode = command.StartNodeId is not null;
        bool lineClass = command.LineClassId is not null;
        bool poleClass = command.PoleClassId is not null;
        bool endNode = command.EndNodeId is not null;
        bool pointIndex = command.PointIndex is not null;
        bool promisePresent = command.PromiseDecision is not null;
        bool promise = promisePresent && Enum.IsDefined(command.PromiseDecision!.Value);
        return command.Kind switch
        {
            CommercialCoreCommandKind.SetNodeDraft =>
                position && nodeClass && !startNode && !lineClass && !poleClass && !endNode &&
                !pointIndex && !promisePresent,
            CommercialCoreCommandKind.StartLineDraft =>
                !position && !nodeClass && startNode && lineClass && poleClass && !endNode &&
                !pointIndex && !promisePresent,
            CommercialCoreCommandKind.AddLinePoint =>
                position && !nodeClass && !startNode && !lineClass && !poleClass && !endNode &&
                !pointIndex && !promisePresent,
            CommercialCoreCommandKind.MoveLinePoint =>
                position && !nodeClass && !startNode && !lineClass && !poleClass && !endNode &&
                pointIndex && !promisePresent,
            CommercialCoreCommandKind.FinishLineDraft =>
                !position && !nodeClass && !startNode && !lineClass && !poleClass && endNode &&
                !pointIndex && !promisePresent,
            CommercialCoreCommandKind.SetPromiseDecision =>
                !position && !nodeClass && !startNode && !lineClass && !poleClass && !endNode &&
                !pointIndex && promise,
            CommercialCoreCommandKind.CancelNodeDraft or
            CommercialCoreCommandKind.UndoLinePoint or
            CommercialCoreCommandKind.CancelLineDraft or
            CommercialCoreCommandKind.OrderNode or
            CommercialCoreCommandKind.OrderLine or
            CommercialCoreCommandKind.AdvanceConstruction or
            CommercialCoreCommandKind.ApproveDecisionWindow =>
                !position && !nodeClass && !startNode && !lineClass && !poleClass && !endNode &&
                !pointIndex && !promisePresent,
            _ => false,
        };
    }

    private CommercialDecisionPreview PreviewFailure(
        CommercialCoreError error,
        long minute,
        long cash,
        IReadOnlyList<ThermalIntervalResult> phases,
        ThermalDemandResult demand) => new(
            false,
            error,
            minute,
            cash,
            phases,
            demand.DemandId,
            demand.Failure,
            demand.FirstBottleneckAssetId);

    private CommercialDecisionPreview PreviewRejected(CommercialCoreError error) => new(
        false,
        error,
        _construction.GetSnapshot().Minute,
        _cashUnit,
        Array.Empty<ThermalIntervalResult>(),
        null,
        null,
        null);

    private CommercialCoreCommandResult Accepted(
        CommercialDecisionPreview? preview = null,
        CommercialChapterResultRecord? completed = null) => new(
            true,
            null,
            null,
            GetSnapshot(),
            preview,
            completed);

    private CommercialCoreCommandResult Rejected(
        CommercialCoreError error,
        ConstructionError? constructionError = null,
        CommercialDecisionPreview? decisionPreview = null) => new(
            false,
            error,
            constructionError,
            GetSnapshot(),
            decisionPreview,
            null);

    private void RebuildFromPrefix(int commandCount)
    {
        CommercialCoreRun rebuilt = _campaign is null
            ? Restore(
                _world,
                _slice!,
                _commands.Take(commandCount).ToArray())
            : Restore(
                _world,
                _campaign,
                _commands.Take(commandCount).ToArray());
        _commands.Clear();
        _commands.AddRange(rebuilt._commands);
        _committedPhaseResults.Clear();
        _committedPhaseResults.AddRange(rebuilt._committedPhaseResults);
        _chapterResults.Clear();
        _chapterResults.AddRange(rebuilt._chapterResults);
        _chapterStartCommandCounts.Clear();
        _chapterStartCommandCounts.AddRange(rebuilt._chapterStartCommandCounts);
        _construction = rebuilt._construction;
        _chapterIndex = rebuilt._chapterIndex;
        _windowIndex = rebuilt._windowIndex;
        _cashUnit = rebuilt._cashUnit;
        _promiseDecision = rebuilt._promiseDecision;
        _thermalMemory = rebuilt._thermalMemory;
        _campaignComplete = rebuilt._campaignComplete;
        _chapterStartCommandCount = rebuilt._chapterStartCommandCount;
        _pendingProjectStartCommandCount = rebuilt._pendingProjectStartCommandCount;
        _recentProjectCheckpointCommandCount = rebuilt._recentProjectCheckpointCommandCount;
    }

    private CommercialCoreChapter CurrentChapter => _chapters[_chapterIndex];
}
