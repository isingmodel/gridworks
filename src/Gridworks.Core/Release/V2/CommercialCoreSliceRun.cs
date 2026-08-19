namespace Gridworks.Core.Release.V2;

public enum CommercialCoreCommandKind
{
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
    AdvanceConstruction,
    SetPromiseDecision,
    ApproveDecisionWindow,
}

public enum CommercialCoreRunError
{
    WrongState,
    InvalidCommandShape,
    ConstructionRejected,
    InsufficientCash,
    DeadlineExceeded,
    PromiseDecisionRequired,
    SafetyDutyUnserved,
    KeptPromiseUnserved,
    FutureSafetyAtRisk,
    NothingToRollback,
    NothingToRestart,
    CommandLimit,
}

public sealed record CommercialCoreCommand(
    CommercialCoreCommandKind Kind,
    string? FirstId = null,
    string? SecondId = null,
    string? ThirdId = null,
    MapPoint? Position = null,
    int? PointIndex = null,
    CommercialPromiseDecision? PromiseDecision = null)
{
    public static CommercialCoreCommand SetNodeDraft(string nodeClassId, MapPoint position) =>
        new(CommercialCoreCommandKind.SetNodeDraft, nodeClassId, Position: position);

    public static CommercialCoreCommand CancelNodeDraft() =>
        new(CommercialCoreCommandKind.CancelNodeDraft);

    public static CommercialCoreCommand OrderNode() =>
        new(CommercialCoreCommandKind.OrderNode);

    public static CommercialCoreCommand StartLineDraft(
        string startNodeId,
        string lineClassId,
        string poleClassId) =>
        new(CommercialCoreCommandKind.StartLineDraft, startNodeId, lineClassId, poleClassId);

    public static CommercialCoreCommand AddLinePoint(MapPoint position) =>
        new(CommercialCoreCommandKind.AddLinePoint, Position: position);

    public static CommercialCoreCommand MoveLinePoint(int pointIndex, MapPoint position) =>
        new(CommercialCoreCommandKind.MoveLinePoint, Position: position, PointIndex: pointIndex);

    public static CommercialCoreCommand UndoLinePoint() =>
        new(CommercialCoreCommandKind.UndoLinePoint);

    public static CommercialCoreCommand FinishLineDraft(string endNodeId) =>
        new(CommercialCoreCommandKind.FinishLineDraft, endNodeId);

    public static CommercialCoreCommand CancelLineDraft() =>
        new(CommercialCoreCommandKind.CancelLineDraft);

    public static CommercialCoreCommand OrderLine() =>
        new(CommercialCoreCommandKind.OrderLine);

    public static CommercialCoreCommand AdvanceConstruction() =>
        new(CommercialCoreCommandKind.AdvanceConstruction);

    public static CommercialCoreCommand SetPromiseDecision(CommercialPromiseDecision decision) =>
        new(CommercialCoreCommandKind.SetPromiseDecision, PromiseDecision: decision);

    public static CommercialCoreCommand ApproveDecisionWindow() =>
        new(CommercialCoreCommandKind.ApproveDecisionWindow);
}

public sealed record CommercialPhaseProjection(
    CommercialOperatingPhaseDefinition Phase,
    ThermalIntervalEvaluation Evaluation,
    bool IsInCurrentWindow,
    bool SafetySatisfied,
    bool PromiseSatisfied)
{
    private IReadOnlyList<string> _effectiveUnavailableNodeIds = Array.Empty<string>();
    private IReadOnlyList<string> _effectiveUnavailableEdgeIds = Array.Empty<string>();

    public SpatialWorldDefinition? ProjectedWorld { get; init; }

    public IReadOnlyList<string> EffectiveUnavailableNodeIds
    {
        get => _effectiveUnavailableNodeIds;
        init => _effectiveUnavailableNodeIds = FreezeSorted(value);
    }

    public IReadOnlyList<string> EffectiveUnavailableEdgeIds
    {
        get => _effectiveUnavailableEdgeIds;
        init => _effectiveUnavailableEdgeIds = FreezeSorted(value);
    }

    public bool Equals(CommercialPhaseProjection? other) => other is not null &&
        Equals(Phase, other.Phase) &&
        Equals(Evaluation, other.Evaluation) &&
        IsInCurrentWindow == other.IsInCurrentWindow &&
        SafetySatisfied == other.SafetySatisfied &&
        PromiseSatisfied == other.PromiseSatisfied &&
        ProjectedWorldEquals(ProjectedWorld, other.ProjectedWorld) &&
        EffectiveUnavailableNodeIds.SequenceEqual(
            other.EffectiveUnavailableNodeIds,
            StringComparer.Ordinal) &&
        EffectiveUnavailableEdgeIds.SequenceEqual(
            other.EffectiveUnavailableEdgeIds,
            StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Phase);
        hash.Add(Evaluation);
        hash.Add(IsInCurrentWindow);
        hash.Add(SafetySatisfied);
        hash.Add(PromiseSatisfied);
        AddProjectedWorld(ref hash, ProjectedWorld);
        AddSequence(ref hash, EffectiveUnavailableNodeIds);
        AddSequence(ref hash, EffectiveUnavailableEdgeIds);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<string> FreezeSorted(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());
    }

    private static void AddSequence(ref HashCode hash, IEnumerable<string> values)
    {
        foreach (string value in values)
        {
            hash.Add(value, StringComparer.Ordinal);
        }
    }

    private static bool ProjectedWorldEquals(
        SpatialWorldDefinition? first,
        SpatialWorldDefinition? second) =>
        ReferenceEquals(first, second) ||
        first is not null &&
        second is not null &&
        string.Equals(first.SchemaVersion, second.SchemaVersion, StringComparison.Ordinal) &&
        string.Equals(first.WorldId, second.WorldId, StringComparison.Ordinal) &&
        string.Equals(first.DisplayName, second.DisplayName, StringComparison.Ordinal) &&
        first.UnitsPerDesignUnit == second.UnitsPerDesignUnit &&
        first.Bounds == second.Bounds &&
        first.InitialCashUnit == second.InitialCashUnit &&
        first.NodeClasses.SequenceEqual(second.NodeClasses) &&
        first.LineClasses.SequenceEqual(second.LineClasses) &&
        TerrainEquals(first.Terrain, second.Terrain) &&
        RiskAreasEqual(first.RiskAreas, second.RiskAreas) &&
        first.Nodes.SequenceEqual(second.Nodes) &&
        first.Edges.SequenceEqual(second.Edges);

    private static bool TerrainEquals(
        IReadOnlyList<TerrainPolygonDefinition> first,
        IReadOnlyList<TerrainPolygonDefinition> second) =>
        first.Count == second.Count && first.Zip(second).All(pair =>
            string.Equals(
                pair.First.TerrainId,
                pair.Second.TerrainId,
                StringComparison.Ordinal) &&
            string.Equals(
                pair.First.DisplayName,
                pair.Second.DisplayName,
                StringComparison.Ordinal) &&
            pair.First.Kind == pair.Second.Kind &&
            pair.First.Polygon.SequenceEqual(pair.Second.Polygon));

    private static bool RiskAreasEqual(
        IReadOnlyList<SpatialRiskAreaDefinition> first,
        IReadOnlyList<SpatialRiskAreaDefinition> second) =>
        first.Count == second.Count && first.Zip(second).All(pair =>
            string.Equals(
                pair.First.RiskAreaId,
                pair.Second.RiskAreaId,
                StringComparison.Ordinal) &&
            string.Equals(
                pair.First.DisplayName,
                pair.Second.DisplayName,
                StringComparison.Ordinal) &&
            pair.First.Polygon.SequenceEqual(pair.Second.Polygon));

    private static void AddProjectedWorld(
        ref HashCode hash,
        SpatialWorldDefinition? world)
    {
        if (world is null)
        {
            hash.Add(false);
            return;
        }
        hash.Add(true);
        hash.Add(world.SchemaVersion, StringComparer.Ordinal);
        hash.Add(world.WorldId, StringComparer.Ordinal);
        hash.Add(world.DisplayName, StringComparer.Ordinal);
        hash.Add(world.UnitsPerDesignUnit);
        hash.Add(world.Bounds);
        hash.Add(world.InitialCashUnit);
        foreach (SpatialNodeClassDefinition nodeClass in world.NodeClasses)
        {
            hash.Add(nodeClass);
        }
        foreach (SpatialLineClassDefinition lineClass in world.LineClasses)
        {
            hash.Add(lineClass);
        }
        foreach (TerrainPolygonDefinition terrain in world.Terrain)
        {
            hash.Add(terrain.TerrainId, StringComparer.Ordinal);
            hash.Add(terrain.DisplayName, StringComparer.Ordinal);
            hash.Add(terrain.Kind);
            foreach (MapPoint point in terrain.Polygon)
            {
                hash.Add(point);
            }
        }
        foreach (SpatialRiskAreaDefinition riskArea in world.RiskAreas)
        {
            hash.Add(riskArea.RiskAreaId, StringComparer.Ordinal);
            hash.Add(riskArea.DisplayName, StringComparer.Ordinal);
            foreach (MapPoint point in riskArea.Polygon)
            {
                hash.Add(point);
            }
        }
        foreach (SpatialNodeDefinition node in world.Nodes)
        {
            hash.Add(node);
        }
        foreach (SpatialEdgeDefinition edge in world.Edges)
        {
            hash.Add(edge);
        }
    }
}

public sealed record CommercialCommittedPhaseResult(
    string PhaseId,
    ThermalIntervalEvaluation Evaluation);

public sealed record CommercialChapterOutcome(
    string ChapterId,
    CommercialStoryCard ResultCard,
    CommercialPromiseDecision PromiseDecision,
    IReadOnlyList<CommercialCommittedPhaseResult> Phases,
    long EndingCashUnit,
    long EndingMinute)
{
    private IReadOnlyList<CommercialCommittedPhaseResult> _phases =
        Array.AsReadOnly(Phases.ToArray());

    public IReadOnlyList<CommercialCommittedPhaseResult> Phases
    {
        get => _phases;
        init => _phases = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record CommercialCoreSnapshot(
    string SegmentId,
    CommercialCoreSeedDefinition Seed,
    CommercialCoreChapterDefinition Chapter,
    CommercialDecisionWindowDefinition? CurrentWindow,
    ConstructionSnapshot Construction,
    long CashUnit,
    long Minute,
    CommercialPromiseDecision PromiseDecision,
    ThermalState ThermalState,
    IReadOnlyList<CommercialPhaseProjection> Projections,
    IReadOnlyList<CommercialCommittedPhaseResult> CommittedPhases,
    bool ProjectionIncludesCurrentConstruction,
    bool CanApprove,
    bool CanRollbackRecentProject,
    bool CanRestartWindow,
    bool CampaignComplete,
    int CommandCount,
    int ChapterStartCommandCount,
    int WindowStartCommandCount,
    ThermalSupplyFailure? FirstBlockingFailure,
    CommercialChapterOutcome? LastOutcome)
{
    private IReadOnlyList<CommercialPhaseProjection> _projections =
        Array.AsReadOnly(Projections.ToArray());
    private IReadOnlyList<CommercialCommittedPhaseResult> _committedPhases =
        Array.AsReadOnly(CommittedPhases.ToArray());

    public IReadOnlyList<CommercialPhaseProjection> Projections
    {
        get => _projections;
        init => _projections = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<CommercialCommittedPhaseResult> CommittedPhases
    {
        get => _committedPhases;
        init => _committedPhases = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record CommercialCoreCommandResult(
    bool Accepted,
    CommercialCoreRunError? Error,
    ConstructionError? ConstructionError,
    CommercialCoreSnapshot Snapshot);

public sealed record CommercialCoreProjectQuote(
    bool Accepted,
    CommercialCoreRunError? Error,
    ConstructionError? ConstructionError,
    long? CostCashUnit,
    long? BuildMinutes,
    long? CompletionMinute,
    IReadOnlyList<string> RiskAreaIds)
{
    private IReadOnlyList<string> _riskAreaIds = Array.AsReadOnly(RiskAreaIds.ToArray());

    public IReadOnlyList<string> RiskAreaIds
    {
        get => _riskAreaIds;
        init => _riskAreaIds = Array.AsReadOnly(value.ToArray());
    }
}

public sealed class CommercialCoreSliceRun
{
    public const int MaximumAcceptedCommands = 20_000;

    private readonly CommercialCoreSliceDefinition _definition;
    private readonly CommercialWorldDefinition _baseWorld;
    private readonly List<CommercialCoreCommand> _commands = [];
    private ConstructionSession _construction = null!;
    private CommercialCoreSegmentDefinition _segment = null!;
    private CommercialPromiseDecision _promiseDecision;
    private ThermalState _thermalState = ThermalState.Empty;
    private readonly List<CommercialCommittedPhaseResult> _committedPhases = [];
    private long _cashUnit;
    private int _windowIndex;
    private int _chapterStartCommandCount;
    private int _windowStartCommandCount;
    private long _windowStartMinute;
    private int? _pendingProjectStartCommandCount;
    private int? _recentProjectStartCommandCount;
    private bool _campaignComplete;
    private CommercialChapterOutcome? _lastOutcome;

    public CommercialCoreSliceRun(
        CommercialCoreSliceDefinition definition,
        CommercialWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(world);
        CommercialCoreSliceLoader.Validate(definition, world);
        _definition = definition;
        _baseWorld = world with { };
        ResetToPrelude();
    }

    public IReadOnlyList<CommercialCoreCommand> Commands =>
        Array.AsReadOnly(_commands.ToArray());

    public string CurrentSegmentId => _segment.SegmentId;

    public int CommandCount => _commands.Count;

    public int ChapterStartCommandCount => _chapterStartCommandCount;

    public int WindowStartCommandCount => _windowStartCommandCount;

    public CommercialCoreSnapshot GetSnapshot()
    {
        (IReadOnlyList<CommercialPhaseProjection> projections, bool includesConstruction) =
            BuildProjections();
        ThermalSupplyFailure? firstFailure = FirstBlockingFailure(projections);
        bool canApprove = !_campaignComplete &&
            _construction.GetSnapshot().Phase == ConstructionPhase.Ready &&
            PromiseReady() &&
            firstFailure is null &&
            CurrentWindowPromiseSatisfied(projections);
        return new CommercialCoreSnapshot(
            _segment.SegmentId,
            _segment.Seed,
            _segment.Chapter,
            CurrentWindowOrNull(),
            _construction.GetSnapshot(),
            _cashUnit,
            CurrentMinute,
            _promiseDecision,
            _thermalState,
            projections,
            _committedPhases.ToArray(),
            includesConstruction,
            canApprove,
            CanRollbackRecentProject,
            !_campaignComplete && _commands.Count > _windowStartCommandCount,
            _campaignComplete,
            _commands.Count,
            _chapterStartCommandCount,
            _windowStartCommandCount,
            firstFailure,
            _lastOutcome);
    }

    public NodePlacementPreview PreviewNodePlacement(string nodeClassId, MapPoint position) =>
        _campaignComplete
            ? new NodePlacementPreview(
                false,
                ConstructionError.WrongPhase,
                nodeClassId,
                position)
            : _construction.PreviewNodePlacement(nodeClassId, position);

    public LineStartPreview PreviewLineStart(
        string startNodeId,
        string lineClassId,
        string poleClassId) =>
        _campaignComplete
            ? new LineStartPreview(
                false,
                ConstructionError.WrongPhase,
                startNodeId,
                lineClassId,
                poleClassId)
            : _construction.PreviewLineStart(startNodeId, lineClassId, poleClassId);

    public LinePointPreview PreviewLinePoint(MapPoint position) =>
        _construction.PreviewLinePoint(position);

    public LinePointMovePreview PreviewMoveLinePoint(int pointIndex, MapPoint position) =>
        _construction.PreviewMoveLinePoint(pointIndex, position);

    public LineFinishPreview PreviewLineFinish(string endNodeId) =>
        _construction.PreviewLineFinish(endNodeId);

    public CommercialCoreProjectQuote PreviewNodeOrder() =>
        ProjectQuote(_construction.PreviewNodeOrder());

    public CommercialCoreProjectQuote PreviewLineOrder() =>
        ProjectQuote(_construction.PreviewLineOrder());

    public CommercialCoreCommandResult Execute(CommercialCoreCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (_commands.Count >= MaximumAcceptedCommands)
        {
            return Rejected(CommercialCoreRunError.CommandLimit);
        }
        if (!ValidShape(command))
        {
            return Rejected(CommercialCoreRunError.InvalidCommandShape);
        }
        ApplyResult result = Apply(command);
        if (!result.Accepted)
        {
            return Rejected(result.Error!.Value, result.ConstructionError);
        }
        _commands.Add(command);
        result.AfterRecorded?.Invoke();
        return new CommercialCoreCommandResult(true, null, null, GetSnapshot());
    }

    public bool UndoRecentConstruction()
    {
        if (!CanRollbackRecentProject)
        {
            return false;
        }
        ReplayPrefix(_recentProjectStartCommandCount!.Value);
        return true;
    }

    public bool RestartDecisionWindow()
    {
        if (_campaignComplete || _commands.Count == _windowStartCommandCount)
        {
            return false;
        }
        ReplayPrefix(_windowStartCommandCount);
        return true;
    }

    public bool RestartChapter()
    {
        if (_commands.Count == _chapterStartCommandCount)
        {
            return false;
        }
        ReplayPrefix(_chapterStartCommandCount);
        return true;
    }

    public static CommercialCoreSliceRun Restore(
        CommercialCoreSliceDefinition definition,
        CommercialWorldDefinition world,
        IReadOnlyList<CommercialCoreCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (commands.Count > MaximumAcceptedCommands)
        {
            throw new ArgumentException("Commercial command journal exceeds its limit.", nameof(commands));
        }
        var run = new CommercialCoreSliceRun(definition, world);
        foreach (CommercialCoreCommand command in commands)
        {
            CommercialCoreCommandResult result = run.Execute(command);
            if (!result.Accepted)
            {
                throw new ArgumentException(
                    $"Commercial command replay rejected {command.Kind}.",
                    nameof(commands));
            }
        }
        return run;
    }

    private long CurrentMinute => checked(
        (long)_segment.Seed.StartMinute + _construction.GetSnapshot().Minute);

    private CommercialCoreProjectQuote ProjectQuote(ConstructionQuote quote)
    {
        if (!quote.Accepted)
        {
            return new CommercialCoreProjectQuote(
                false,
                CommercialCoreRunError.ConstructionRejected,
                quote.Error,
                null,
                null,
                null,
                quote.RiskAreaIds);
        }
        CommercialCoreRunError? error = null;
        if (quote.CostCashUnit!.Value > _cashUnit)
        {
            error = CommercialCoreRunError.InsufficientCash;
        }
        else
        {
            int? allowance = _segment.Chapter.DecisionWindows[_windowIndex]
                .BuildMinutesAvailable;
            if (allowance is not null &&
                checked(_segment.Seed.StartMinute + quote.CompletionMinute!.Value) >
                checked(_windowStartMinute + allowance.Value))
            {
                error = CommercialCoreRunError.DeadlineExceeded;
            }
        }
        return new CommercialCoreProjectQuote(
            error is null,
            error,
            null,
            quote.CostCashUnit,
            quote.BuildMinutes,
            checked(_segment.Seed.StartMinute + quote.CompletionMinute!.Value),
            quote.RiskAreaIds);
    }

    private bool CanRollbackRecentProject =>
        !_campaignComplete &&
        _recentProjectStartCommandCount.HasValue &&
        _construction.GetSnapshot().Phase == ConstructionPhase.Ready;

    private CommercialDecisionWindowDefinition? CurrentWindowOrNull() =>
        _campaignComplete
            ? null
            : _segment.Chapter.DecisionWindows[_windowIndex];

    private ApplyResult Apply(CommercialCoreCommand command)
    {
        if (_campaignComplete)
        {
            return ApplyResult.Failure(CommercialCoreRunError.WrongState);
        }
        return command.Kind switch
        {
            CommercialCoreCommandKind.SetNodeDraft => ApplyConstruction(
                command,
                () => _construction.SetNodeDraft(command.FirstId!, command.Position!.Value),
                startsProject: _construction.GetSnapshot().Phase == ConstructionPhase.Ready),
            CommercialCoreCommandKind.CancelNodeDraft => ApplyConstruction(
                command,
                _construction.CancelNodeDraft,
                cancelsProject: true),
            CommercialCoreCommandKind.OrderNode => ApplyOrder(
                _construction.PreviewNodeOrder,
                _construction.OrderNode),
            CommercialCoreCommandKind.StartLineDraft => ApplyConstruction(
                command,
                () => _construction.StartLineDraft(
                    command.FirstId!,
                    command.SecondId!,
                    command.ThirdId!),
                startsProject: true),
            CommercialCoreCommandKind.AddLinePoint => ApplyConstruction(
                command,
                () => _construction.AddLinePoint(command.Position!.Value)),
            CommercialCoreCommandKind.MoveLinePoint => ApplyConstruction(
                command,
                () => _construction.MoveLinePoint(
                    command.PointIndex!.Value,
                    command.Position!.Value)),
            CommercialCoreCommandKind.UndoLinePoint => ApplyConstruction(
                command,
                _construction.UndoLinePoint),
            CommercialCoreCommandKind.FinishLineDraft => ApplyConstruction(
                command,
                () => _construction.FinishLineDraft(command.FirstId!)),
            CommercialCoreCommandKind.CancelLineDraft => ApplyConstruction(
                command,
                _construction.CancelLineDraft,
                cancelsProject: true),
            CommercialCoreCommandKind.OrderLine => ApplyOrder(
                _construction.PreviewLineOrder,
                _construction.OrderLine),
            CommercialCoreCommandKind.AdvanceConstruction => ApplyAdvance(),
            CommercialCoreCommandKind.SetPromiseDecision => ApplyPromise(
                command.PromiseDecision!.Value),
            CommercialCoreCommandKind.ApproveDecisionWindow => ApplyApproval(),
            _ => ApplyResult.Failure(CommercialCoreRunError.InvalidCommandShape),
        };
    }

    private ApplyResult ApplyConstruction(
        CommercialCoreCommand command,
        Func<ConstructionCommandResult> execute,
        bool startsProject = false,
        bool cancelsProject = false)
    {
        _ = command;
        int commandIndex = _commands.Count;
        ConstructionCommandResult result = execute();
        if (!result.Accepted)
        {
            return ApplyResult.Failure(
                CommercialCoreRunError.ConstructionRejected,
                result.Error);
        }
        int? previousPending = _pendingProjectStartCommandCount;
        return ApplyResult.Success(() =>
        {
            if (startsProject && !previousPending.HasValue)
            {
                _pendingProjectStartCommandCount = commandIndex;
            }
            if (cancelsProject)
            {
                _pendingProjectStartCommandCount = null;
            }
        });
    }

    private ApplyResult ApplyOrder(
        Func<ConstructionQuote> preview,
        Func<ConstructionCommandResult> execute)
    {
        ConstructionQuote quote = preview();
        if (!quote.Accepted)
        {
            return ApplyResult.Failure(
                CommercialCoreRunError.ConstructionRejected,
                quote.Error);
        }
        if (quote.CostCashUnit!.Value > _cashUnit)
        {
            return ApplyResult.Failure(CommercialCoreRunError.InsufficientCash);
        }
        CommercialDecisionWindowDefinition window =
            _segment.Chapter.DecisionWindows[_windowIndex];
        if (window.BuildMinutesAvailable is int allowance &&
            checked(_segment.Seed.StartMinute + quote.CompletionMinute!.Value) >
            checked(_windowStartMinute + allowance))
        {
            return ApplyResult.Failure(CommercialCoreRunError.DeadlineExceeded);
        }
        ConstructionCommandResult result = execute();
        if (!result.Accepted)
        {
            return ApplyResult.Failure(
                CommercialCoreRunError.ConstructionRejected,
                result.Error);
        }
        long cost = quote.CostCashUnit.Value;
        return ApplyResult.Success(() => _cashUnit = checked(_cashUnit - cost));
    }

    private ApplyResult ApplyAdvance()
    {
        ConstructionCommandResult result = _construction.AdvanceToConstructionCompletion();
        if (!result.Accepted)
        {
            return ApplyResult.Failure(
                CommercialCoreRunError.ConstructionRejected,
                result.Error);
        }
        int projectStart = _pendingProjectStartCommandCount ?? _commands.Count;
        return ApplyResult.Success(() =>
        {
            _recentProjectStartCommandCount = projectStart;
            _pendingProjectStartCommandCount = null;
        });
    }

    private ApplyResult ApplyPromise(CommercialPromiseDecision decision)
    {
        if (_segment.Chapter.CityPromise is null ||
            decision is not (CommercialPromiseDecision.Keep or CommercialPromiseDecision.Defer) ||
            _committedPhases.Count > 0 ||
            _construction.GetSnapshot().Phase != ConstructionPhase.Ready)
        {
            return ApplyResult.Failure(CommercialCoreRunError.WrongState);
        }
        return ApplyResult.Success(() => _promiseDecision = decision);
    }

    private ApplyResult ApplyApproval()
    {
        if (_construction.GetSnapshot().Phase != ConstructionPhase.Ready)
        {
            return ApplyResult.Failure(CommercialCoreRunError.WrongState);
        }
        if (!PromiseReady())
        {
            return ApplyResult.Failure(CommercialCoreRunError.PromiseDecisionRequired);
        }

        (IReadOnlyList<CommercialPhaseProjection> projections, _) = BuildProjections();
        int endIndex = CurrentWindowEndPhaseIndex();
        int startIndex = CurrentWindowStartPhaseIndex();
        for (int offset = 0; offset < projections.Count; offset++)
        {
            int index = startIndex + offset;
            CommercialPhaseProjection projection = projections[offset];
            if (!projection.SafetySatisfied)
            {
                return ApplyResult.Failure(
                    index < endIndex
                        ? CommercialCoreRunError.SafetyDutyUnserved
                        : CommercialCoreRunError.FutureSafetyAtRisk);
            }
            if (index < endIndex && !projection.PromiseSatisfied)
            {
                return ApplyResult.Failure(CommercialCoreRunError.KeptPromiseUnserved);
            }
        }

        CommercialPhaseProjection[] committed = projections
            .Take(endIndex - startIndex)
            .ToArray();
        return ApplyResult.Success(() =>
        {
            foreach (CommercialPhaseProjection projection in committed)
            {
                _committedPhases.Add(new CommercialCommittedPhaseResult(
                    projection.Phase.PhaseId,
                    projection.Evaluation));
            }
            if (committed.Length > 0)
            {
                _thermalState = committed[^1].Evaluation.NextThermalState;
            }
            _recentProjectStartCommandCount = null;
            _pendingProjectStartCommandCount = null;

            if (_windowIndex + 1 < _segment.Chapter.DecisionWindows.Count)
            {
                _windowIndex++;
                _windowStartCommandCount = _commands.Count;
                _windowStartMinute = CurrentMinute;
                return;
            }
            if (ReferenceEquals(_segment, _definition.Prelude))
            {
                _lastOutcome = BuildOutcome();
                EnterSegment(_definition.Main, _commands.Count);
            }
            else
            {
                _lastOutcome = BuildOutcome();
                _campaignComplete = true;
            }
        });
    }

    private (IReadOnlyList<CommercialPhaseProjection> Projections, bool IncludesConstruction)
        BuildProjections()
    {
        if (_campaignComplete)
        {
            return (Array.Empty<CommercialPhaseProjection>(), false);
        }
        (CommercialWorldDefinition world, bool includesConstruction) = ProjectedWorld();
        SpatialWorldDefinition projectedSpatialWorld = world.ToSpatialWorld();
        int startIndex = CurrentWindowStartPhaseIndex();
        int currentEndIndex = CurrentWindowEndPhaseIndex();
        ThermalState state = _thermalState;
        var result = new List<CommercialPhaseProjection>();
        for (int index = startIndex; index < _segment.Chapter.OperatingPhases.Count; index++)
        {
            CommercialOperatingPhaseDefinition phase =
                _segment.Chapter.OperatingPhases[index];
            ThermalIntervalRequest request = BuildIntervalRequest(phase, world);
            ThermalIntervalEvaluation evaluation = ThermalNetworkEvaluator.EvaluateInterval(
                world,
                request,
                state);
            state = evaluation.NextThermalState;
            result.Add(new CommercialPhaseProjection(
                phase,
                evaluation,
                index < currentEndIndex,
                RequiredLoadsDelivered(phase, evaluation, CommercialObligationKind.SafetyDuty),
                _promiseDecision != CommercialPromiseDecision.Keep ||
                RequiredLoadsDelivered(phase, evaluation, CommercialObligationKind.CityPromise))
            {
                ProjectedWorld = projectedSpatialWorld,
                EffectiveUnavailableNodeIds = request.UnavailableNodeIds,
                EffectiveUnavailableEdgeIds = request.UnavailableEdgeIds,
            });
        }
        return (result, includesConstruction);
    }

    private ThermalIntervalRequest BuildIntervalRequest(
        CommercialOperatingPhaseDefinition phase,
        CommercialWorldDefinition world)
    {
        ThermalLoadRequest[] loads = phase.Loads
            .Where(load => load.Obligation != CommercialObligationKind.CityPromise ||
                _promiseDecision == CommercialPromiseDecision.Keep)
            .Select(load => new ThermalLoadRequest(
                load.LoadId,
                load.DemandKw,
                load.Obligation switch
                {
                    CommercialObligationKind.OperatingRecord => ThermalPermission.ContinuousOnly,
                    CommercialObligationKind.CityPromise => ThermalPermission.EmergencyAllowed,
                    CommercialObligationKind.SafetyDuty
                        when phase.ThermalPolicy ==
                            CommercialPhaseThermalPolicy.SafetyEmergencyAllowed =>
                        ThermalPermission.EmergencyAllowed,
                    _ => ThermalPermission.ContinuousOnly,
                }))
            .ToArray();
        (IReadOnlyList<string> unavailableNodes, IReadOnlyList<string> unavailableEdges) =
            UnavailableAssets(phase, world);
        return new ThermalIntervalRequest(
            phase.PhaseId,
            loads,
            unavailableNodes,
            unavailableEdges,
            phase.ThermalLimitOverrides);
    }

    private static (IReadOnlyList<string> Nodes, IReadOnlyList<string> Edges)
        UnavailableAssets(
            CommercialOperatingPhaseDefinition phase,
            CommercialWorldDefinition world)
    {
        var nodes = new SortedSet<string>(phase.UnavailableNodeIds, StringComparer.Ordinal);
        var edges = new SortedSet<string>(phase.UnavailableEdgeIds, StringComparer.Ordinal);
        if (phase.ActiveRiskAreaIds.Count == 0)
        {
            return (nodes.ToArray(), edges.ToArray());
        }

        Dictionary<string, SpatialRiskAreaDefinition> riskAreas = world.RiskAreas
            .ToDictionary(item => item.RiskAreaId, StringComparer.Ordinal);
        IReadOnlyList<SpatialRiskAreaDefinition> activeRiskAreas = phase.ActiveRiskAreaIds
            .Select(id => riskAreas[id])
            .ToArray();
        Dictionary<string, CommercialNodeClassDefinition> nodeClasses = world.NodeClasses
            .ToDictionary(item => item.ClassId, StringComparer.Ordinal);
        Dictionary<string, SpatialNodeDefinition> worldNodes = world.Nodes
            .ToDictionary(item => item.NodeId, StringComparer.Ordinal);

        foreach (SpatialNodeDefinition node in world.Nodes)
        {
            int radius = nodeClasses[node.ClassId].FootprintRadiusUnit;
            if (activeRiskAreas.Any(area => FixedGeometry.CircleIntersectsPolygon(
                    node.Position,
                    radius,
                    area.Polygon)))
            {
                nodes.Add(node.NodeId);
            }
        }
        foreach (SpatialEdgeDefinition edge in world.Edges)
        {
            MapPoint from = worldNodes[edge.FromNodeId].Position;
            MapPoint to = worldNodes[edge.ToNodeId].Position;
            if (activeRiskAreas.Any(area => FixedGeometry.SegmentIntersectsPolygon(
                    from,
                    to,
                    area.Polygon)))
            {
                edges.Add(edge.EdgeId);
            }
        }
        return (nodes.ToArray(), edges.ToArray());
    }

    private static bool RequiredLoadsDelivered(
        CommercialOperatingPhaseDefinition phase,
        ThermalIntervalEvaluation evaluation,
        CommercialObligationKind obligation)
    {
        string[] required = phase.Loads
            .Where(load => load.Obligation == obligation)
            .Select(load => load.LoadId)
            .ToArray();
        return required.All(loadId => evaluation.Loads.Any(result =>
            result.LoadId == loadId && result.DeliveredKw == result.DemandKw));
    }

    private (CommercialWorldDefinition World, bool IncludesConstruction) ProjectedWorld()
    {
        ConstructionSnapshot snapshot = _construction.GetSnapshot();
        if (snapshot.Phase == ConstructionPhase.Ready)
        {
            return (WorldFromSnapshot(snapshot.World), false);
        }

        ConstructionSession clone = RebuildCurrentConstruction();
        ConstructionSnapshot cloneSnapshot = clone.GetSnapshot();
        bool completed = false;
        if (cloneSnapshot.Phase == ConstructionPhase.NodeDrafting)
        {
            ConstructionQuote quote = clone.PreviewNodeOrder();
            completed = quote.Accepted && WithinProjectionBudgetAndDeadline(quote) &&
                clone.OrderNode().Accepted && clone.AdvanceToConstructionCompletion().Accepted;
        }
        else if (cloneSnapshot.Phase == ConstructionPhase.LineDrafting &&
                 cloneSnapshot.LineDraft?.EndNodeId is not null)
        {
            ConstructionQuote quote = clone.PreviewLineOrder();
            completed = quote.Accepted && WithinProjectionBudgetAndDeadline(quote) &&
                clone.OrderLine().Accepted && clone.AdvanceToConstructionCompletion().Accepted;
        }
        else if (cloneSnapshot.Phase is ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding)
        {
            completed = clone.AdvanceToConstructionCompletion().Accepted;
        }
        return (WorldFromSnapshot(completed ? clone.GetSnapshot().World : snapshot.World), completed);
    }

    private bool WithinProjectionBudgetAndDeadline(ConstructionQuote quote)
    {
        if (!quote.Accepted || quote.CostCashUnit!.Value > _cashUnit)
        {
            return false;
        }
        int? allowance = _segment.Chapter.DecisionWindows[_windowIndex].BuildMinutesAvailable;
        return allowance is null ||
            checked(_segment.Seed.StartMinute + quote.CompletionMinute!.Value) <=
            checked(_windowStartMinute + allowance.Value);
    }

    private ConstructionSession RebuildCurrentConstruction()
    {
        var clone = new ConstructionSession(
            CommercialCoreSliceLoader.BuildSeedWorld(_baseWorld, _segment.Seed).ToSpatialWorld());
        for (int index = _chapterStartCommandCount; index < _commands.Count; index++)
        {
            CommercialCoreCommand command = _commands[index];
            ConstructionCommandResult? result = command.Kind switch
            {
                CommercialCoreCommandKind.SetNodeDraft => clone.SetNodeDraft(
                    command.FirstId!, command.Position!.Value),
                CommercialCoreCommandKind.CancelNodeDraft => clone.CancelNodeDraft(),
                CommercialCoreCommandKind.OrderNode => clone.OrderNode(),
                CommercialCoreCommandKind.StartLineDraft => clone.StartLineDraft(
                    command.FirstId!, command.SecondId!, command.ThirdId!),
                CommercialCoreCommandKind.AddLinePoint => clone.AddLinePoint(command.Position!.Value),
                CommercialCoreCommandKind.MoveLinePoint => clone.MoveLinePoint(
                    command.PointIndex!.Value, command.Position!.Value),
                CommercialCoreCommandKind.UndoLinePoint => clone.UndoLinePoint(),
                CommercialCoreCommandKind.FinishLineDraft => clone.FinishLineDraft(command.FirstId!),
                CommercialCoreCommandKind.CancelLineDraft => clone.CancelLineDraft(),
                CommercialCoreCommandKind.OrderLine => clone.OrderLine(),
                CommercialCoreCommandKind.AdvanceConstruction =>
                    clone.AdvanceToConstructionCompletion(),
                _ => null,
            };
            if (result is { Accepted: false })
            {
                throw new InvalidOperationException("Accepted construction journal failed to replay.");
            }
        }
        return clone;
    }

    private CommercialWorldDefinition WorldFromSnapshot(SpatialWorldDefinition spatial) =>
        _baseWorld with
        {
            InitialCashUnit = _cashUnit,
            Nodes = spatial.Nodes.Where(item => item.Commissioned).ToArray(),
            Edges = spatial.Edges.Where(item => item.Commissioned).ToArray(),
        };

    private bool PromiseReady() =>
        _segment.Chapter.CityPromise is null ||
        _promiseDecision is CommercialPromiseDecision.Keep or CommercialPromiseDecision.Defer;

    private bool CurrentWindowPromiseSatisfied(
        IReadOnlyList<CommercialPhaseProjection> projections) =>
        projections.Where(item => item.IsInCurrentWindow).All(item => item.PromiseSatisfied);

    private ThermalSupplyFailure? FirstBlockingFailure(
        IReadOnlyList<CommercialPhaseProjection> projections)
    {
        foreach (CommercialPhaseProjection projection in projections)
        {
            foreach (CommercialLoadBundleDefinition load in projection.Phase.Loads)
            {
                bool required = load.Obligation == CommercialObligationKind.SafetyDuty ||
                    (load.Obligation == CommercialObligationKind.CityPromise &&
                     _promiseDecision == CommercialPromiseDecision.Keep &&
                     projection.IsInCurrentWindow);
                if (!required)
                {
                    continue;
                }
                ThermalLoadSupply? supply = projection.Evaluation.Loads.FirstOrDefault(item =>
                    item.LoadId == load.LoadId);
                if (supply is null || supply.DeliveredKw != supply.DemandKw)
                {
                    return supply?.Failure;
                }
            }
        }
        return null;
    }

    private int CurrentWindowStartPhaseIndex()
    {
        string id = _segment.Chapter.DecisionWindows[_windowIndex].BeforePhaseId;
        return _segment.Chapter.OperatingPhases.ToList().FindIndex(item => item.PhaseId == id);
    }

    private int CurrentWindowEndPhaseIndex()
    {
        if (_windowIndex + 1 >= _segment.Chapter.DecisionWindows.Count)
        {
            return _segment.Chapter.OperatingPhases.Count;
        }
        string next = _segment.Chapter.DecisionWindows[_windowIndex + 1].BeforePhaseId;
        return _segment.Chapter.OperatingPhases.ToList().FindIndex(item => item.PhaseId == next);
    }

    private void ResetToPrelude()
    {
        _commands.Clear();
        _lastOutcome = null;
        _chapterStartCommandCount = 0;
        EnterSegment(_definition.Prelude, 0);
    }

    private void EnterSegment(CommercialCoreSegmentDefinition segment, int commandCount)
    {
        _segment = segment;
        CommercialWorldDefinition seedWorld = CommercialCoreSliceLoader.BuildSeedWorld(
            _baseWorld,
            segment.Seed);
        _construction = new ConstructionSession(seedWorld.ToSpatialWorld());
        _cashUnit = checked(segment.Seed.InitialCashUnit + segment.Chapter.BudgetGrantCashUnit);
        _promiseDecision = CommercialPromiseDecision.Unset;
        _thermalState = new ThermalState(segment.Seed.CoolingAssetIds);
        _committedPhases.Clear();
        _windowIndex = 0;
        _chapterStartCommandCount = commandCount;
        _windowStartCommandCount = commandCount;
        _windowStartMinute = segment.Seed.StartMinute;
        _pendingProjectStartCommandCount = null;
        _recentProjectStartCommandCount = null;
        _campaignComplete = false;
    }

    private CommercialChapterOutcome BuildOutcome()
    {
        CommercialStoryCard card = _segment.Chapter.CityPromise is null
            ? _segment.Chapter.ResultCards.Standard!
            : _promiseDecision == CommercialPromiseDecision.Keep
                ? _segment.Chapter.ResultCards.Kept!
                : _segment.Chapter.ResultCards.Deferred!;
        return new CommercialChapterOutcome(
            _segment.Chapter.ChapterId,
            card,
            _promiseDecision,
            _committedPhases.ToArray(),
            _cashUnit,
            CurrentMinute);
    }

    private void ReplayPrefix(int count)
    {
        CommercialCoreCommand[] prefix = _commands.Take(count).ToArray();
        ResetToPrelude();
        foreach (CommercialCoreCommand command in prefix)
        {
            CommercialCoreCommandResult result = Execute(command);
            if (!result.Accepted)
            {
                throw new InvalidOperationException("Accepted commercial journal failed to replay.");
            }
        }
    }

    private CommercialCoreCommandResult Rejected(
        CommercialCoreRunError error,
        ConstructionError? constructionError = null) =>
        new(false, error, constructionError, GetSnapshot());

    private static bool ValidShape(CommercialCoreCommand command)
    {
        bool noIds = command.FirstId is null && command.SecondId is null && command.ThirdId is null;
        bool noPoint = command.Position is null && command.PointIndex is null;
        return command.Kind switch
        {
            CommercialCoreCommandKind.SetNodeDraft =>
                Text(command.FirstId) && command.SecondId is null && command.ThirdId is null &&
                command.Position.HasValue && command.PointIndex is null && command.PromiseDecision is null,
            CommercialCoreCommandKind.CancelNodeDraft or
            CommercialCoreCommandKind.OrderNode or
            CommercialCoreCommandKind.UndoLinePoint or
            CommercialCoreCommandKind.CancelLineDraft or
            CommercialCoreCommandKind.OrderLine or
            CommercialCoreCommandKind.AdvanceConstruction or
            CommercialCoreCommandKind.ApproveDecisionWindow =>
                noIds && noPoint && command.PromiseDecision is null,
            CommercialCoreCommandKind.StartLineDraft =>
                Text(command.FirstId) && Text(command.SecondId) && Text(command.ThirdId) &&
                noPoint && command.PromiseDecision is null,
            CommercialCoreCommandKind.AddLinePoint =>
                noIds && command.Position.HasValue && command.PointIndex is null &&
                command.PromiseDecision is null,
            CommercialCoreCommandKind.MoveLinePoint =>
                noIds && command.Position.HasValue && command.PointIndex is >= 0 &&
                command.PromiseDecision is null,
            CommercialCoreCommandKind.FinishLineDraft =>
                Text(command.FirstId) && command.SecondId is null && command.ThirdId is null &&
                noPoint && command.PromiseDecision is null,
            CommercialCoreCommandKind.SetPromiseDecision =>
                noIds && noPoint && command.PromiseDecision is
                    CommercialPromiseDecision.Keep or CommercialPromiseDecision.Defer,
            _ => false,
        };
    }

    private static bool Text(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value == value.Trim();

    private sealed record ApplyResult(
        bool Accepted,
        CommercialCoreRunError? Error,
        ConstructionError? ConstructionError,
        Action? AfterRecorded)
    {
        public static ApplyResult Success(Action? afterRecorded = null) =>
            new(true, null, null, afterRecorded);

        public static ApplyResult Failure(
            CommercialCoreRunError error,
            ConstructionError? constructionError = null) =>
            new(false, error, constructionError, null);
    }
}
