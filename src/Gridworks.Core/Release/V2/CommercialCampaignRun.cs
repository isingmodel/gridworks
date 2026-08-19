namespace Gridworks.Core.Release.V2;

public enum CommercialCampaignRunError
{
    WrongState,
    InvalidCommandShape,
    ToolUnavailable,
    ConstructionRejected,
    InsufficientCash,
    DeadlineExceeded,
    ConnectionRequirementUnmet,
    PromiseDecisionRequired,
    SafetyDutyUnserved,
    KeptPromiseUnserved,
    FutureSafetyAtRisk,
    ArithmeticOverflow,
    CommandLimit,
}

public sealed record CommercialCampaignSnapshot(
    int ChapterIndex,
    CommercialCampaignChapterDefinition Chapter,
    CommercialDecisionWindowDefinition? CurrentWindow,
    ConstructionSnapshot Construction,
    long CashUnit,
    long Minute,
    CommercialPromiseDecision PromiseDecision,
    ThermalState ThermalState,
    IReadOnlyList<CommercialPhaseProjection> Projections,
    IReadOnlyList<CommercialCommittedPhaseResult> CommittedPhases,
    IReadOnlyList<string> AvailableNodeClassIds,
    IReadOnlyList<CommercialCampaignLinePlanDefinition> AvailableLinePlans,
    IReadOnlyList<CommercialCampaignConnectionFailure> ConnectionFailures,
    IReadOnlyList<CommercialCampaignChapterOutcome> CompletedChapterOutcomes,
    IReadOnlyList<CommercialCampaignChapterReplayOption> ChapterReplayOptions,
    CommercialCampaignEpiloguePresentation? Epilogue,
    bool ProjectionIncludesCurrentConstruction,
    bool CanApprove,
    bool CanRollbackRecentProject,
    bool CanRestartWindow,
    bool CanRestartChapter,
    bool CanRewindPreviousChapter,
    bool CampaignComplete,
    int CommandCount,
    int ChapterStartCommandCount,
    int WindowStartCommandCount,
    ThermalSupplyFailure? FirstBlockingFailure,
    CommercialCampaignChapterOutcome? LastOutcome)
{
    private IReadOnlyList<CommercialPhaseProjection> _projections = Freeze(Projections);
    private IReadOnlyList<CommercialCommittedPhaseResult> _committedPhases =
        Freeze(CommittedPhases);
    private IReadOnlyList<string> _availableNodeClassIds = Freeze(AvailableNodeClassIds);
    private IReadOnlyList<CommercialCampaignLinePlanDefinition> _availableLinePlans =
        Freeze(AvailableLinePlans);
    private IReadOnlyList<CommercialCampaignConnectionFailure> _connectionFailures =
        Freeze(ConnectionFailures);
    private IReadOnlyList<CommercialCampaignChapterOutcome> _completedChapterOutcomes =
        Freeze(CompletedChapterOutcomes);
    private IReadOnlyList<CommercialCampaignChapterReplayOption> _chapterReplayOptions =
        Freeze(ChapterReplayOptions);
    private IReadOnlyList<CommercialPhaseComparisonRow> _phaseComparisonRows =
        Array.Empty<CommercialPhaseComparisonRow>();

    public IReadOnlyList<CommercialPhaseProjection> Projections
    {
        get => _projections;
        init => _projections = Freeze(value);
    }

    public IReadOnlyList<CommercialCommittedPhaseResult> CommittedPhases
    {
        get => _committedPhases;
        init => _committedPhases = Freeze(value);
    }

    public IReadOnlyList<string> AvailableNodeClassIds
    {
        get => _availableNodeClassIds;
        init => _availableNodeClassIds = Freeze(value);
    }

    public IReadOnlyList<CommercialCampaignLinePlanDefinition> AvailableLinePlans
    {
        get => _availableLinePlans;
        init => _availableLinePlans = Freeze(value);
    }

    public IReadOnlyList<CommercialCampaignConnectionFailure> ConnectionFailures
    {
        get => _connectionFailures;
        init => _connectionFailures = Freeze(value);
    }

    public IReadOnlyList<CommercialCampaignChapterOutcome> CompletedChapterOutcomes
    {
        get => _completedChapterOutcomes;
        init => _completedChapterOutcomes = Freeze(value);
    }

    public IReadOnlyList<CommercialCampaignChapterReplayOption> ChapterReplayOptions
    {
        get => _chapterReplayOptions;
        init => _chapterReplayOptions = Freeze(value);
    }

    public CommercialApprovalChecklist ApprovalChecklist { get; init; } =
        CommercialApprovalChecklist.Empty;

    public CommercialSupplyDiagnostic? FirstBlockingDiagnostic { get; init; }

    public IReadOnlyList<CommercialPhaseComparisonRow> PhaseComparisonRows
    {
        get => _phaseComparisonRows;
        init => _phaseComparisonRows = Freeze(value);
    }

    public CommercialConstructionWindowForecast ConstructionWindowForecast { get; init; } =
        CommercialConstructionWindowForecast.Empty;

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record CommercialCampaignConnectionFailure(
    string NodeId,
    int CurrentConnections,
    int RequiredConnections);

public sealed record CommercialCampaignCommandResult(
    bool Accepted,
    CommercialCampaignRunError? Error,
    ConstructionError? ConstructionError,
    CommercialCampaignConnectionFailure? ConnectionFailure,
    CommercialCampaignSnapshot Snapshot);

public sealed record CommercialCampaignProjectQuote(
    bool Accepted,
    CommercialCampaignRunError? Error,
    ConstructionError? ConstructionError,
    long? CostCashUnit,
    long? BuildMinutes,
    long? CompletionMinute,
    IReadOnlyList<string> RiskAreaIds)
{
    private IReadOnlyList<string> _riskAreaIds = Freeze(RiskAreaIds);

    public IReadOnlyList<string> RiskAreaIds
    {
        get => _riskAreaIds;
        init => _riskAreaIds = Freeze(value);
    }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed class CommercialCampaignRun
{
    public const int MaximumAcceptedCommands = 20_000;

    private readonly CommercialCampaignDefinition _definition;
    private readonly CommercialWorldDefinition _baseWorld;
    private readonly List<CommercialCoreCommand> _commands = [];
    private readonly List<int> _chapterStartCommandCounts = [];
    private readonly List<CommercialCampaignChapterOutcome> _completedOutcomes = [];
    private readonly List<CommercialCommittedPhaseResult> _committedPhases = [];
    private ConstructionSession _construction = null!;
    private CommercialWorldDefinition _chapterStartWorld = null!;
    private CommercialCampaignChapterDefinition _chapter = null!;
    private int _chapterIndex;
    private int _windowIndex;
    private int _chapterStartCommandCount;
    private int _windowStartCommandCount;
    private int? _pendingProjectStartCommandCount;
    private int? _recentProjectStartCommandCount;
    private long _cashUnit;
    private long _chapterStartMinute;
    private long _windowStartMinute;
    private CommercialPromiseDecision _promiseDecision;
    private ThermalState _thermalState = ThermalState.Empty;
    private bool _campaignComplete;
    private CommercialCampaignChapterOutcome? _lastOutcome;
    private long _journalGeneration;
    private long _recoveryPreviewGeneration = -1;
    private IReadOnlyList<CommercialRecoveryPreview>? _recoveryPreviewCache;

    public CommercialCampaignRun(
        CommercialCampaignDefinition definition,
        CommercialWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(world);
        CommercialCampaignLoader.Validate(definition, world);
        _definition = definition;
        _baseWorld = world with { };
        ResetToStart();
    }

    public IReadOnlyList<CommercialCoreCommand> Commands =>
        Array.AsReadOnly(_commands.ToArray());

    public int CurrentChapterIndex => _chapterIndex;

    public string CurrentChapterId => _chapter.ChapterId;

    public int CommandCount => _commands.Count;

    public int ChapterStartCommandCount => _chapterStartCommandCount;

    public int WindowStartCommandCount => _windowStartCommandCount;

    public CommercialCampaignSnapshot GetSnapshot()
    {
        ApprovalEvaluation approval = EvaluateApproval();
        CommercialCampaignSnapshot snapshot = new(
            _chapterIndex,
            _chapter,
            CurrentWindowOrNull(),
            _construction.GetSnapshot(),
            _cashUnit,
            CurrentMinute,
            _promiseDecision,
            _thermalState,
            approval.Projections,
            _committedPhases.ToArray(),
            _chapter.AvailableNodeClassIds,
            _chapter.AvailableLinePlans,
            approval.ConnectionFailures,
            _completedOutcomes.ToArray(),
            GetCompletedCampaignReplayOptions(),
            GetEpiloguePresentation(),
            approval.ProjectionIncludesCurrentConstruction,
            approval.Checklist.CanApprove,
            CanRollbackRecentProject,
            !_campaignComplete && _commands.Count > _windowStartCommandCount,
            !_campaignComplete && _commands.Count > _chapterStartCommandCount,
            CanRewindPreviousChapter,
            _campaignComplete,
            _commands.Count,
            _chapterStartCommandCount,
            _windowStartCommandCount,
            approval.FirstBlocking?.Supply.Failure,
            _lastOutcome);
        return snapshot with
        {
            ApprovalChecklist = approval.Checklist,
            FirstBlockingDiagnostic = approval.FirstBlocking is null
                ? null
                : BuildSupplyDiagnostic(approval.FirstBlocking),
            PhaseComparisonRows = BuildPhaseComparisonRows(approval.Projections),
            ConstructionWindowForecast = BuildConstructionWindowForecast(null),
        };
    }

    public NodePlacementPreview PreviewNodePlacement(string nodeClassId, MapPoint position)
    {
        if (_campaignComplete ||
            !_chapter.AvailableNodeClassIds.Contains(nodeClassId, StringComparer.Ordinal))
        {
            return new NodePlacementPreview(
                false,
                _campaignComplete
                    ? ConstructionError.WrongPhase
                    : ConstructionError.InvalidNodeClass,
                nodeClassId,
                position);
        }
        return _construction.PreviewNodePlacement(nodeClassId, position);
    }

    public LineStartPreview PreviewLineStart(
        string startNodeId,
        string lineClassId,
        string poleClassId)
    {
        if (_campaignComplete || !LinePlanAvailable(lineClassId, poleClassId))
        {
            return new LineStartPreview(
                false,
                _campaignComplete
                    ? ConstructionError.WrongPhase
                    : ConstructionError.UnknownLineClass,
                startNodeId,
                lineClassId,
                poleClassId);
        }
        return _construction.PreviewLineStart(startNodeId, lineClassId, poleClassId);
    }

    public LinePointPreview PreviewLinePoint(MapPoint position) =>
        _construction.PreviewLinePoint(position);

    public LinePointMovePreview PreviewMoveLinePoint(int pointIndex, MapPoint position) =>
        _construction.PreviewMoveLinePoint(pointIndex, position);

    public LineFinishPreview PreviewLineFinish(string endNodeId) =>
        _construction.PreviewLineFinish(endNodeId);

    public CommercialCampaignProjectQuote PreviewNodeOrder() =>
        ProjectQuote(_construction.PreviewNodeOrder());

    public CommercialCampaignProjectQuote PreviewLineOrder() =>
        ProjectQuote(_construction.PreviewLineOrder());

    public CommercialConstructionWindowForecast PreviewConstructionWindowForecast(
        CommercialNextProjectPlan? nextProject = null) =>
        BuildConstructionWindowForecast(nextProject);

    public IReadOnlyList<CommercialRecoveryPreview> GetRecoveryPreviews()
    {
        if (_recoveryPreviewGeneration == _journalGeneration &&
            _recoveryPreviewCache is not null)
        {
            return _recoveryPreviewCache;
        }

        CommercialCampaignSnapshot current = GetSnapshot();
        CommercialRecoveryKind[] kinds = Enum.GetValues<CommercialRecoveryKind>();
        var targets = new Dictionary<int, CommercialCampaignSnapshot>();
        var previews = new CommercialRecoveryPreview[kinds.Length];
        for (int index = 0; index < kinds.Length; index++)
        {
            CommercialRecoveryKind kind = kinds[index];
            int? targetCount = RecoveryTargetCommandCount(kind);
            if (!targetCount.HasValue)
            {
                previews[index] = DisabledRecovery(kind);
                continue;
            }
            if (!targets.TryGetValue(targetCount.Value, out CommercialCampaignSnapshot? target))
            {
                CommercialCampaignRun restored = Restore(
                    _definition,
                    _baseWorld,
                    _commands.Take(targetCount.Value).ToArray());
                target = restored.GetSnapshot();
                targets.Add(targetCount.Value, target);
            }
            previews[index] = BuildRecoveryPreview(kind, targetCount.Value, current, target);
        }
        _recoveryPreviewCache = Array.AsReadOnly(previews);
        _recoveryPreviewGeneration = _journalGeneration;
        return _recoveryPreviewCache;
    }

    public CommercialRecoveryPreview PreviewRecovery(CommercialRecoveryKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        return GetRecoveryPreviews().Single(item => item.Kind == kind);
    }

    private CommercialRecoveryPreview BuildRecoveryPreview(
        CommercialRecoveryKind kind,
        int targetCount,
        CommercialCampaignSnapshot current,
        CommercialCampaignSnapshot target)
    {
        HashSet<string> targetNodes = target.Construction.World.Nodes
            .Select(item => item.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> targetEdges = target.Construction.World.Edges
            .Select(item => item.EdgeId)
            .ToHashSet(StringComparer.Ordinal);
        string[] removedNodes = current.Construction.World.Nodes
            .Select(item => item.NodeId)
            .Where(id => !targetNodes.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        string[] removedEdges = current.Construction.World.Edges
            .Select(item => item.EdgeId)
            .Where(id => !targetEdges.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        RemovedWorkSummary removed = AnalyzeRemovedWork(targetCount, current.Construction);
        ConstructionKind[] removedKinds =
        [
            .. removed.CompletedNodeProjectCount > 0
                ? new[] { ConstructionKind.Node }
                : Array.Empty<ConstructionKind>(),
            .. removed.CompletedLineProjectCount > 0
                ? new[] { ConstructionKind.Line }
                : Array.Empty<ConstructionKind>(),
            .. removed.DiscardedDraftKind.HasValue
                ? new[] { removed.DiscardedDraftKind.Value }
                : Array.Empty<ConstructionKind>(),
            .. removed.DiscardedActiveConstructionKind.HasValue
                ? new[] { removed.DiscardedActiveConstructionKind.Value }
                : Array.Empty<ConstructionKind>(),
        ];
        ConstructionKind[] distinctRemovedKinds = removedKinds.Distinct().ToArray();
        ConstructionKind? removedKind = distinctRemovedKinds.Length == 1
            ? distinctRemovedKinds[0]
            : null;
        int removedRoutePointCount = checked(
            removed.CompletedLineRoutePointCount +
            (removed.DiscardedDraftKind == ConstructionKind.Line
                ? removed.DiscardedDraftRoutePointCount
                : 0) +
            (removed.DiscardedActiveConstructionKind == ConstructionKind.Line
                ? removed.DiscardedActiveConstructionRoutePointCount
                : 0));
        (string? phaseId, string? phaseDisplay, int? phaseNumber, int? phaseCount) =
            RecoveryPhase(target);
        return new CommercialRecoveryPreview(
            kind,
            true,
            targetCount,
            removedKind,
            removedNodes,
            removedEdges,
            removedRoutePointCount,
            target.CashUnit,
            target.Minute,
            target.ChapterIndex,
            target.Chapter.DisplayName,
            phaseId,
            phaseDisplay,
            phaseNumber,
            phaseCount,
            target.PromiseDecision,
            target.Chapter.CityPromise?.DisplayName,
            target.ThermalState.CoolingAssetIds)
        {
            RemovedCompletedNodeProjectCount = removed.CompletedNodeProjectCount,
            RemovedCompletedLineProjectCount = removed.CompletedLineProjectCount,
            RemovedCompletedLineRoutePointCount = removed.CompletedLineRoutePointCount,
            DiscardedDraftKind = removed.DiscardedDraftKind,
            DiscardedDraftRoutePointCount = removed.DiscardedDraftRoutePointCount,
            DiscardedActiveConstructionKind = removed.DiscardedActiveConstructionKind,
            DiscardedActiveLineRoutePointCount =
                removed.DiscardedActiveConstructionRoutePointCount,
        };
    }

    private RemovedWorkSummary AnalyzeRemovedWork(
        int targetCount,
        ConstructionSnapshot currentConstruction)
    {
        ConstructionKind? activeKind = null;
        int activeLineIntermediatePointCount = 0;
        bool activeLineEndSelected = false;
        int completedNodeProjectCount = 0;
        int completedLineProjectCount = 0;
        int completedLineRoutePointCount = 0;

        for (int index = targetCount; index < _commands.Count; index++)
        {
            CommercialCoreCommand command = _commands[index];
            switch (command.Kind)
            {
                case CommercialCoreCommandKind.SetNodeDraft:
                    activeKind = ConstructionKind.Node;
                    activeLineIntermediatePointCount = 0;
                    activeLineEndSelected = false;
                    break;
                case CommercialCoreCommandKind.StartLineDraft:
                    activeKind = ConstructionKind.Line;
                    activeLineIntermediatePointCount = 0;
                    activeLineEndSelected = false;
                    break;
                case CommercialCoreCommandKind.AddLinePoint
                    when activeKind == ConstructionKind.Line:
                    activeLineIntermediatePointCount = checked(
                        activeLineIntermediatePointCount + 1);
                    break;
                case CommercialCoreCommandKind.FinishLineDraft
                    when activeKind == ConstructionKind.Line:
                    activeLineEndSelected = true;
                    break;
                case CommercialCoreCommandKind.UndoLinePoint
                    when activeKind == ConstructionKind.Line && activeLineEndSelected:
                    activeLineEndSelected = false;
                    break;
                case CommercialCoreCommandKind.UndoLinePoint
                    when activeKind == ConstructionKind.Line:
                    activeLineIntermediatePointCount = Math.Max(
                        0,
                        activeLineIntermediatePointCount - 1);
                    break;
                case CommercialCoreCommandKind.CancelNodeDraft
                    when activeKind == ConstructionKind.Node:
                case CommercialCoreCommandKind.CancelLineDraft
                    when activeKind == ConstructionKind.Line:
                    activeKind = null;
                    activeLineIntermediatePointCount = 0;
                    activeLineEndSelected = false;
                    break;
                case CommercialCoreCommandKind.AdvanceConstruction:
                    if (activeKind == ConstructionKind.Node)
                    {
                        completedNodeProjectCount = checked(completedNodeProjectCount + 1);
                    }
                    else if (activeKind == ConstructionKind.Line)
                    {
                        completedLineProjectCount = checked(completedLineProjectCount + 1);
                        completedLineRoutePointCount = checked(
                            completedLineRoutePointCount +
                            activeLineIntermediatePointCount +
                            2);
                    }
                    activeKind = null;
                    activeLineIntermediatePointCount = 0;
                    activeLineEndSelected = false;
                    break;
            }
        }

        ConstructionKind? discardedDraftKind = currentConstruction.Phase switch
        {
            ConstructionPhase.NodeDrafting => ConstructionKind.Node,
            ConstructionPhase.LineDrafting => ConstructionKind.Line,
            _ => null,
        };
        int discardedDraftRoutePointCount = currentConstruction.Phase ==
            ConstructionPhase.LineDrafting
                ? checked(
                    1 +
                    currentConstruction.LineDraft!.IntermediatePoints.Count +
                    (currentConstruction.LineDraft.EndNodeId is null ? 0 : 1))
                : 0;
        ConstructionKind? discardedActiveConstructionKind = currentConstruction.Phase switch
        {
            ConstructionPhase.NodeBuilding => ConstructionKind.Node,
            ConstructionPhase.LineBuilding => ConstructionKind.Line,
            _ => null,
        };
        int discardedActiveConstructionRoutePointCount =
            discardedActiveConstructionKind == ConstructionKind.Line
                ? checked(currentConstruction.ActiveConstruction!.EdgeIds.Count + 1)
                : 0;
        return new RemovedWorkSummary(
            completedNodeProjectCount,
            completedLineProjectCount,
            completedLineRoutePointCount,
            discardedDraftKind,
            discardedDraftRoutePointCount,
            discardedActiveConstructionKind,
            discardedActiveConstructionRoutePointCount);
    }

    public CommercialCampaignCommandResult Execute(CommercialCoreCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplyResult result = TryApply(command);
        if (!result.Accepted)
        {
            return Rejected(
                result.Error!.Value,
                result.ConstructionError,
                result.ConnectionFailure);
        }
        RecordAccepted(command, result);
        return new CommercialCampaignCommandResult(true, null, null, null, GetSnapshot());
    }

    private ApplyResult TryApply(CommercialCoreCommand command)
    {
        if (_commands.Count >= MaximumAcceptedCommands)
        {
            return ApplyResult.Failure(CommercialCampaignRunError.CommandLimit);
        }
        if (!ValidShape(command))
        {
            return ApplyResult.Failure(CommercialCampaignRunError.InvalidCommandShape);
        }
        if (!ToolAvailable(command))
        {
            return ApplyResult.Failure(CommercialCampaignRunError.ToolUnavailable);
        }
        return Apply(command);
    }

    private void RecordAccepted(CommercialCoreCommand command, ApplyResult result)
    {
        _commands.Add(command);
        result.AfterRecorded?.Invoke();
        MarkJournalChanged();
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
        if (_campaignComplete || _commands.Count == _chapterStartCommandCount)
        {
            return false;
        }
        ReplayPrefix(_chapterStartCommandCount);
        return true;
    }

    public bool RewindToPreviousChapter() =>
        RewindToChapter(_campaignComplete ? _chapterIndex : _chapterIndex - 1);

    public bool RewindToChapter(int chapterIndex)
    {
        int exclusiveUpperBound = _campaignComplete ? _definition.Chapters.Count : _chapterIndex;
        if (chapterIndex < 0 || chapterIndex >= exclusiveUpperBound ||
            chapterIndex >= _chapterStartCommandCounts.Count)
        {
            return false;
        }
        ReplayPrefix(_chapterStartCommandCounts[chapterIndex]);
        return true;
    }

    public IReadOnlyList<CommercialCampaignChapterReplayOption>
        GetCompletedCampaignReplayOptions()
    {
        if (!_campaignComplete)
        {
            return Array.Empty<CommercialCampaignChapterReplayOption>();
        }
        if (_chapterStartCommandCounts.Count != _definition.Chapters.Count)
        {
            throw new InvalidOperationException(
                "Completed campaign chapter checkpoints are incomplete.");
        }
        return _definition.Chapters.Select((chapter, index) =>
            new CommercialCampaignChapterReplayOption(
                index,
                chapter.ChapterId,
                chapter.DisplayName,
                _chapterStartCommandCounts[index])).ToArray();
    }

    public bool ReplayCompletedChapterStart(string chapterId)
    {
        if (!_campaignComplete || string.IsNullOrWhiteSpace(chapterId))
        {
            return false;
        }
        int chapterIndex = _definition.Chapters.ToList().FindIndex(chapter =>
            string.Equals(chapter.ChapterId, chapterId, StringComparison.Ordinal));
        if (chapterIndex < 0)
        {
            return false;
        }
        ReplayPrefix(_chapterStartCommandCounts[chapterIndex]);
        return true;
    }

    public CommercialCampaignEpiloguePresentation? GetEpiloguePresentation()
    {
        if (!_campaignComplete)
        {
            return null;
        }
        if (_completedOutcomes.Count != _definition.Chapters.Count)
        {
            throw new InvalidOperationException(
                "Completed campaign outcomes are incomplete.");
        }

        CommercialWorldDefinition finalWorld = WorldFromSnapshot(
            _construction.GetSnapshot().World);
        CommercialCampaignEpilogueChapterFact[] chapterFacts =
            _completedOutcomes.Select((outcome, index) =>
            {
                CommercialCampaignChapterDefinition chapter = _definition.Chapters[index];
                CommercialCampaignThermalFact[] emergencyAssets = outcome.Facts.ThermalAssets
                    .Where(asset => asset.State == ThermalOperatingState.Emergency)
                    .ToArray();
                CommercialCampaignThermalFact[] protectiveOutageAssets =
                    outcome.Facts.ThermalAssets
                        .Where(asset => asset.State == ThermalOperatingState.ProtectiveOutage)
                        .ToArray();
                return new CommercialCampaignEpilogueChapterFact(
                    outcome.ChapterId,
                    chapter.DisplayName,
                    outcome.Facts.Loads,
                    emergencyAssets,
                    protectiveOutageAssets,
                    BuildEpilogueSummaryLines(
                        chapter,
                        outcome,
                        emergencyAssets,
                        protectiveOutageAssets,
                        finalWorld),
                    outcome.PromiseDecision,
                    outcome.EndingCashUnit);
            }).ToArray();

        Dictionary<string, CommercialCampaignChapterOutcome> outcomes = _completedOutcomes
            .ToDictionary(outcome => outcome.ChapterId, StringComparer.Ordinal);
        Dictionary<string, CommercialCampaignChapterDefinition> chapters = _definition.Chapters
            .ToDictionary(chapter => chapter.ChapterId, StringComparer.Ordinal);
        CommercialCampaignEpiloguePromiseFact[] promiseFacts = _definition.Epilogue.PromiseLines
            .Select(line =>
            {
                CommercialCampaignChapterDefinition chapter = chapters[line.ChapterId];
                CommercialCampaignChapterOutcome outcome = outcomes[line.ChapterId];
                string rendered = outcome.PromiseDecision switch
                {
                    CommercialPromiseDecision.Keep => line.Kept,
                    CommercialPromiseDecision.Defer => line.Deferred,
                    _ => throw new InvalidOperationException(
                        "Completed promise outcome has no decision."),
                };
                return new CommercialCampaignEpiloguePromiseFact(
                    line.ChapterId,
                    line.PromiseId,
                    chapter.CityPromise!.DisplayName,
                    outcome.PromiseDecision,
                    rendered);
            }).ToArray();
        CommercialCampaignEpilogueDefinition epilogue = _definition.Epilogue;
        return new CommercialCampaignEpiloguePresentation(
            epilogue.DisplayName,
            epilogue.CityReport,
            epilogue.MedicalWitness,
            epilogue.Closing,
            chapterFacts,
            promiseFacts,
            _cashUnit);
    }

    public static CommercialCampaignRun Restore(
        CommercialCampaignDefinition definition,
        CommercialWorldDefinition world,
        IReadOnlyList<CommercialCoreCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (commands.Count > MaximumAcceptedCommands)
        {
            throw new ArgumentException("Commercial campaign command journal exceeds its limit.",
                nameof(commands));
        }
        var run = new CommercialCampaignRun(definition, world);
        foreach (CommercialCoreCommand command in commands)
        {
            ApplyResult result = run.TryApply(command);
            if (!result.Accepted)
            {
                throw new ArgumentException(
                    $"Commercial campaign command replay rejected {command.Kind}.",
                    nameof(commands));
            }
            run.RecordAccepted(command, result);
        }
        return run;
    }

    private long CurrentMinute => checked(
        _chapterStartMinute + _construction.GetSnapshot().Minute);

    private bool CanRollbackRecentProject => !_campaignComplete &&
        _recentProjectStartCommandCount.HasValue &&
        _construction.GetSnapshot().Phase == ConstructionPhase.Ready;

    private bool CanRewindPreviousChapter => _campaignComplete || _chapterIndex > 0;

    private CommercialDecisionWindowDefinition? CurrentWindowOrNull() =>
        _campaignComplete ? null : _chapter.DecisionWindows[_windowIndex];

    private bool LinePlanAvailable(string lineClassId, string poleClassId) =>
        _chapter.AvailableLinePlans.Any(plan =>
            string.Equals(plan.LineClassId, lineClassId, StringComparison.Ordinal) &&
            string.Equals(plan.PoleClassId, poleClassId, StringComparison.Ordinal));

    private bool ToolAvailable(CommercialCoreCommand command) => command.Kind switch
    {
        CommercialCoreCommandKind.SetNodeDraft =>
            _chapter.AvailableNodeClassIds.Contains(command.FirstId!, StringComparer.Ordinal),
        CommercialCoreCommandKind.StartLineDraft =>
            LinePlanAvailable(command.SecondId!, command.ThirdId!),
        _ => true,
    };

    private CommercialCampaignProjectQuote ProjectQuote(ConstructionQuote quote)
    {
        if (!quote.Accepted)
        {
            return new CommercialCampaignProjectQuote(
                false,
                CommercialCampaignRunError.ConstructionRejected,
                quote.Error,
                null,
                null,
                null,
                quote.RiskAreaIds);
        }
        CommercialCampaignRunError? error = null;
        long? absoluteCompletionMinute = CheckedAbsoluteCompletionMinute(
            quote.CompletionMinute!.Value);
        int? allowance = _chapter.DecisionWindows[_windowIndex].BuildMinutesAvailable;
        long? deadline = allowance.HasValue
            ? CheckedWindowDeadline(allowance.Value)
            : null;
        if (!HasCommandSlots(2))
        {
            error = CommercialCampaignRunError.CommandLimit;
        }
        else if (!absoluteCompletionMinute.HasValue)
        {
            error = CommercialCampaignRunError.ArithmeticOverflow;
        }
        else if (quote.CostCashUnit!.Value > _cashUnit)
        {
            error = CommercialCampaignRunError.InsufficientCash;
        }
        else if (allowance.HasValue && !deadline.HasValue)
        {
            error = CommercialCampaignRunError.ArithmeticOverflow;
        }
        else if (deadline.HasValue && absoluteCompletionMinute.Value > deadline.Value)
        {
            error = CommercialCampaignRunError.DeadlineExceeded;
        }
        return new CommercialCampaignProjectQuote(
            error is null,
            error,
            null,
            quote.CostCashUnit,
            quote.BuildMinutes,
            absoluteCompletionMinute,
            quote.RiskAreaIds);
    }

    private CommercialConstructionWindowForecast BuildConstructionWindowForecast(
        CommercialNextProjectPlan? nextProject)
    {
        CommercialDecisionWindowDefinition? window = CurrentWindowOrNull();
        long currentMinute = CurrentMinute;
        long windowStartMinute = _campaignComplete ? currentMinute : _windowStartMinute;
        int? allowance = window?.BuildMinutesAvailable;
        long? deadline = allowance.HasValue
            ? CheckedWindowDeadline(allowance.Value)
            : null;
        long alreadySpent = checked(currentMinute - windowStartMinute);
        long? remainingNow = deadline.HasValue
            ? checked(deadline.Value - currentMinute)
            : null;
        var steps = new List<CommercialConstructionForecastStep>();

        (ConstructionKind Kind, CommercialCampaignProjectQuote Quote)? current =
            CurrentProjectForecastQuote();
        if (current.HasValue)
        {
            steps.Add(ForecastStep(
                1,
                CommercialConstructionForecastStepRole.CurrentDraft,
                current.Value.Kind,
                current.Value.Quote,
                deadline));
        }

        if (nextProject is not null)
        {
            int sequence = steps.Count + 1;
            ConstructionKind nextKind = NextProjectKind(nextProject);
            CommercialCampaignRun clone = Restore(_definition, _baseWorld, _commands.ToArray());
            CommercialCampaignProjectQuote nextQuote;
            if (current.HasValue && !current.Value.Quote.Accepted)
            {
                nextQuote = BlockedNextProjectQuote();
            }
            else if (!clone.CompleteCurrentProjectForForecast(
                         out CommercialCampaignProjectQuote? _))
            {
                nextQuote = BlockedNextProjectQuote();
            }
            else
            {
                nextQuote = clone.QuoteNextProject(nextProject);
            }
            steps.Add(ForecastStep(
                sequence,
                CommercialConstructionForecastStepRole.ExplicitNextPlan,
                nextKind,
                nextQuote,
                deadline));
        }

        return new CommercialConstructionWindowForecast(
            window?.WindowId,
            windowStartMinute,
            currentMinute,
            alreadySpent,
            allowance,
            deadline,
            remainingNow,
            steps);
    }

    private (ConstructionKind Kind, CommercialCampaignProjectQuote Quote)?
        CurrentProjectForecastQuote()
    {
        ConstructionSnapshot snapshot = _construction.GetSnapshot();
        return snapshot.Phase switch
        {
            ConstructionPhase.NodeDrafting =>
                (ConstructionKind.Node, PreviewNodeOrder()),
            ConstructionPhase.LineDrafting =>
                (ConstructionKind.Line, PreviewLineOrder()),
            ConstructionPhase.NodeBuilding =>
                (ConstructionKind.Node, ActiveConstructionQuote(snapshot)),
            ConstructionPhase.LineBuilding =>
                (ConstructionKind.Line, ActiveConstructionQuote(snapshot)),
            _ => null,
        };
    }

    private CommercialCampaignProjectQuote ActiveConstructionQuote(
        ConstructionSnapshot snapshot)
    {
        ActiveConstructionSnapshot active = snapshot.ActiveConstruction ??
            throw new InvalidOperationException("A building phase must have active construction.");
        long? completion = CheckedAbsoluteCompletionMinute(active.CompletionMinute);
        long? remaining = null;
        CommercialCampaignRunError? error = null;
        try
        {
            remaining = checked(active.CompletionMinute - snapshot.Minute);
        }
        catch (OverflowException)
        {
            error = CommercialCampaignRunError.ArithmeticOverflow;
        }
        if (!completion.HasValue)
        {
            error = CommercialCampaignRunError.ArithmeticOverflow;
        }
        else if (!HasCommandSlots(1))
        {
            error = CommercialCampaignRunError.CommandLimit;
        }
        return new CommercialCampaignProjectQuote(
            error is null,
            error,
            null,
            active.CostCashUnit,
            remaining,
            completion,
            active.RiskAreaIds);
    }

    private static CommercialConstructionForecastStep ForecastStep(
        int sequence,
        CommercialConstructionForecastStepRole stepRole,
        ConstructionKind kind,
        CommercialCampaignProjectQuote quote,
        long? deadline)
    {
        long? remaining = deadline.HasValue && quote.CompletionMinute.HasValue
            ? checked(deadline.Value - quote.CompletionMinute.Value)
            : null;
        return new CommercialConstructionForecastStep(
            sequence,
            kind,
            quote.Accepted,
            quote.Error,
            quote.ConstructionError,
            quote.BuildMinutes,
            quote.CompletionMinute,
            remaining)
        {
            StepRole = stepRole,
        };
    }

    private static CommercialCampaignProjectQuote BlockedNextProjectQuote() => new(
        false,
        CommercialCampaignRunError.WrongState,
        null,
        null,
        null,
        null,
        Array.Empty<string>());

    private bool CompleteCurrentProjectForForecast(
        out CommercialCampaignProjectQuote? blockingQuote)
    {
        blockingQuote = null;
        ConstructionPhase phase = _construction.GetSnapshot().Phase;
        if (phase == ConstructionPhase.Ready)
        {
            return true;
        }
        if (phase is ConstructionPhase.NodeDrafting or ConstructionPhase.LineDrafting)
        {
            CommercialCampaignProjectQuote quote = phase == ConstructionPhase.NodeDrafting
                ? PreviewNodeOrder()
                : PreviewLineOrder();
            if (!quote.Accepted)
            {
                blockingQuote = quote;
                return false;
            }
            CommercialCoreCommand order = phase == ConstructionPhase.NodeDrafting
                ? CommercialCoreCommand.OrderNode()
                : CommercialCoreCommand.OrderLine();
            CommercialCampaignCommandResult orderResult = Execute(order);
            if (!orderResult.Accepted)
            {
                blockingQuote = new CommercialCampaignProjectQuote(
                    false,
                    orderResult.Error,
                    orderResult.ConstructionError,
                    quote.CostCashUnit,
                    quote.BuildMinutes,
                    quote.CompletionMinute,
                    quote.RiskAreaIds);
                return false;
            }
        }
        else if (phase is ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding)
        {
            CommercialCampaignProjectQuote quote = ActiveConstructionQuote(
                _construction.GetSnapshot());
            if (!quote.Accepted)
            {
                blockingQuote = quote;
                return false;
            }
        }
        CommercialCampaignCommandResult advance = Execute(
            CommercialCoreCommand.AdvanceConstruction());
        if (!advance.Accepted)
        {
            blockingQuote = new CommercialCampaignProjectQuote(
                false,
                advance.Error,
                advance.ConstructionError,
                null,
                null,
                null,
                Array.Empty<string>());
            return false;
        }
        return true;
    }

    private CommercialCampaignProjectQuote QuoteNextProject(CommercialNextProjectPlan plan)
    {
        switch (plan)
        {
            case CommercialNextNodeProjectPlan node:
            {
                CommercialCampaignCommandResult result = Execute(
                    CommercialCoreCommand.SetNodeDraft(node.NodeClassId, node.Position));
                if (!result.Accepted)
                {
                    return QuoteFromRejection(result);
                }
                CommercialCampaignProjectQuote quote = PreviewNodeOrder();
                if (!quote.Accepted)
                {
                    return quote;
                }
                result = Execute(CommercialCoreCommand.OrderNode());
                if (!result.Accepted)
                {
                    return QuoteFromRejection(result, quote);
                }
                result = Execute(CommercialCoreCommand.AdvanceConstruction());
                return result.Accepted
                    ? quote
                    : QuoteFromRejection(result, quote);
            }
            case CommercialNextLineProjectPlan line:
            {
                CommercialCampaignCommandResult result = Execute(
                    CommercialCoreCommand.StartLineDraft(
                        line.StartNodeId,
                        line.LineClassId,
                        line.PoleClassId));
                if (!result.Accepted)
                {
                    return QuoteFromRejection(result);
                }
                foreach (MapPoint point in line.IntermediatePoints)
                {
                    result = Execute(CommercialCoreCommand.AddLinePoint(point));
                    if (!result.Accepted)
                    {
                        return QuoteFromRejection(result);
                    }
                }
                result = Execute(CommercialCoreCommand.FinishLineDraft(line.EndNodeId));
                if (!result.Accepted)
                {
                    return QuoteFromRejection(result);
                }
                CommercialCampaignProjectQuote quote = PreviewLineOrder();
                if (!quote.Accepted)
                {
                    return quote;
                }
                result = Execute(CommercialCoreCommand.OrderLine());
                if (!result.Accepted)
                {
                    return QuoteFromRejection(result, quote);
                }
                result = Execute(CommercialCoreCommand.AdvanceConstruction());
                return result.Accepted
                    ? quote
                    : QuoteFromRejection(result, quote);
            }
            default:
                throw new ArgumentException("The next-project plan kind is unsupported.", nameof(plan));
        }
    }

    private static CommercialCampaignProjectQuote QuoteFromRejection(
        CommercialCampaignCommandResult result,
        CommercialCampaignProjectQuote? quote = null) => new(
        false,
        result.Error,
        result.ConstructionError,
        quote?.CostCashUnit,
        quote?.BuildMinutes,
        quote?.CompletionMinute,
        quote?.RiskAreaIds ?? Array.Empty<string>());

    private static ConstructionKind NextProjectKind(CommercialNextProjectPlan plan) => plan switch
    {
        CommercialNextNodeProjectPlan => ConstructionKind.Node,
        CommercialNextLineProjectPlan => ConstructionKind.Line,
        _ => throw new ArgumentException("The next-project plan kind is unsupported.", nameof(plan)),
    };

    private ApplyResult Apply(CommercialCoreCommand command)
    {
        if (_campaignComplete)
        {
            return ApplyResult.Failure(CommercialCampaignRunError.WrongState);
        }
        return command.Kind switch
        {
            CommercialCoreCommandKind.SetNodeDraft => ApplyConstruction(
                () => _construction.SetNodeDraft(command.FirstId!, command.Position!.Value),
                startsProject: _construction.GetSnapshot().Phase == ConstructionPhase.Ready),
            CommercialCoreCommandKind.CancelNodeDraft => ApplyConstruction(
                _construction.CancelNodeDraft,
                cancelsProject: true),
            CommercialCoreCommandKind.OrderNode => ApplyOrder(
                _construction.PreviewNodeOrder,
                _construction.OrderNode),
            CommercialCoreCommandKind.StartLineDraft => ApplyConstruction(
                () => _construction.StartLineDraft(
                    command.FirstId!,
                    command.SecondId!,
                    command.ThirdId!),
                startsProject: true),
            CommercialCoreCommandKind.AddLinePoint => ApplyConstruction(
                () => _construction.AddLinePoint(command.Position!.Value)),
            CommercialCoreCommandKind.MoveLinePoint => ApplyConstruction(
                () => _construction.MoveLinePoint(
                    command.PointIndex!.Value,
                    command.Position!.Value)),
            CommercialCoreCommandKind.UndoLinePoint => ApplyConstruction(
                _construction.UndoLinePoint),
            CommercialCoreCommandKind.FinishLineDraft => ApplyConstruction(
                () => _construction.FinishLineDraft(command.FirstId!)),
            CommercialCoreCommandKind.CancelLineDraft => ApplyConstruction(
                _construction.CancelLineDraft,
                cancelsProject: true),
            CommercialCoreCommandKind.OrderLine => ApplyOrder(
                _construction.PreviewLineOrder,
                _construction.OrderLine),
            CommercialCoreCommandKind.AdvanceConstruction => ApplyAdvance(),
            CommercialCoreCommandKind.SetPromiseDecision => ApplyPromise(
                command.PromiseDecision!.Value),
            CommercialCoreCommandKind.ApproveDecisionWindow => ApplyApproval(),
            _ => ApplyResult.Failure(CommercialCampaignRunError.InvalidCommandShape),
        };
    }

    private ApplyResult ApplyConstruction(
        Func<ConstructionCommandResult> execute,
        bool startsProject = false,
        bool cancelsProject = false)
    {
        int commandIndex = _commands.Count;
        ConstructionCommandResult result = execute();
        if (!result.Accepted)
        {
            return ApplyResult.Failure(
                CommercialCampaignRunError.ConstructionRejected,
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
                CommercialCampaignRunError.ConstructionRejected,
                quote.Error);
        }
        if (!HasCommandSlots(2))
        {
            return ApplyResult.Failure(CommercialCampaignRunError.CommandLimit);
        }
        long? absoluteCompletionMinute = CheckedAbsoluteCompletionMinute(
            quote.CompletionMinute!.Value);
        if (!absoluteCompletionMinute.HasValue)
        {
            return ApplyResult.Failure(CommercialCampaignRunError.ArithmeticOverflow);
        }
        if (quote.CostCashUnit!.Value > _cashUnit)
        {
            return ApplyResult.Failure(CommercialCampaignRunError.InsufficientCash);
        }
        int? allowance = _chapter.DecisionWindows[_windowIndex].BuildMinutesAvailable;
        long? deadline = allowance.HasValue
            ? CheckedWindowDeadline(allowance.Value)
            : null;
        if (allowance.HasValue && !deadline.HasValue)
        {
            return ApplyResult.Failure(CommercialCampaignRunError.ArithmeticOverflow);
        }
        if (deadline.HasValue && absoluteCompletionMinute.Value > deadline.Value)
        {
            return ApplyResult.Failure(CommercialCampaignRunError.DeadlineExceeded);
        }
        ConstructionCommandResult result = execute();
        if (!result.Accepted)
        {
            return ApplyResult.Failure(
                CommercialCampaignRunError.ConstructionRejected,
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
                CommercialCampaignRunError.ConstructionRejected,
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
        if (_chapter.CityPromise is null ||
            decision is not (CommercialPromiseDecision.Keep or CommercialPromiseDecision.Defer) ||
            _committedPhases.Count > 0 ||
            _construction.GetSnapshot().Phase != ConstructionPhase.Ready)
        {
            return ApplyResult.Failure(CommercialCampaignRunError.WrongState);
        }
        return ApplyResult.Success(() => _promiseDecision = decision);
    }

    private ApplyResult ApplyApproval()
    {
        ApprovalEvaluation evaluation = EvaluateApproval();
        if (evaluation.Error.HasValue)
        {
            return ApplyResult.Failure(
                evaluation.Error.Value,
                connectionFailure: evaluation.ConnectionFailure);
        }
        IReadOnlyList<CommercialPhaseProjection> committed = evaluation.Committed;
        return ApplyResult.Success(() =>
        {
            foreach (CommercialPhaseProjection projection in committed)
            {
                _committedPhases.Add(new CommercialCommittedPhaseResult(
                    projection.Phase.PhaseId,
                    projection.Evaluation));
            }
            if (committed.Count > 0)
            {
                _thermalState = committed[^1].Evaluation.NextThermalState;
            }
            _recentProjectStartCommandCount = null;
            _pendingProjectStartCommandCount = null;

            if (_windowIndex + 1 < _chapter.DecisionWindows.Count)
            {
                _windowIndex++;
                _windowStartCommandCount = _commands.Count;
                _windowStartMinute = CurrentMinute;
                return;
            }

            CommercialCampaignChapterOutcome outcome = BuildOutcome();
            _completedOutcomes.Add(outcome);
            _lastOutcome = outcome;
            if (_chapterIndex + 1 < _definition.Chapters.Count)
            {
                EnterChapter(_chapterIndex + 1, _commands.Count, firstChapter: false);
            }
            else
            {
                _campaignComplete = true;
            }
        });
    }

    private ApprovalEvaluation EvaluateApproval()
    {
        (IReadOnlyList<CommercialPhaseProjection> projections, bool includesConstruction) =
            BuildProjections();
        IReadOnlyDictionary<string, bool> counterfactualFutureSafety =
            BuildCounterfactualFutureSafety();
        IReadOnlyList<CommercialCampaignConnectionFailure> connectionFailures =
            BuildConnectionFailures();
        BlockingSupply? firstBlocking = FirstBlockingSupply(
            projections,
            counterfactualFutureSafety);

        CommercialCampaignRunError? error = null;
        CommercialCampaignConnectionFailure? connectionFailure = null;
        IReadOnlyList<CommercialPhaseProjection> committed =
            Array.Empty<CommercialPhaseProjection>();
        if (_commands.Count >= MaximumAcceptedCommands)
        {
            error = CommercialCampaignRunError.CommandLimit;
        }
        else if (_campaignComplete ||
                 _construction.GetSnapshot().Phase != ConstructionPhase.Ready)
        {
            error = CommercialCampaignRunError.WrongState;
        }
        else if (!PromiseReady())
        {
            error = CommercialCampaignRunError.PromiseDecisionRequired;
        }
        else if (connectionFailures.Count > 0)
        {
            error = CommercialCampaignRunError.ConnectionRequirementUnmet;
            connectionFailure = connectionFailures[0];
        }
        else
        {
            int endIndex = CurrentWindowEndPhaseIndex();
            int startIndex = CurrentWindowStartPhaseIndex();
            for (int offset = 0; offset < projections.Count && !error.HasValue; offset++)
            {
                int index = startIndex + offset;
                CommercialPhaseProjection projection = projections[offset];
                if (!projection.SafetySatisfied)
                {
                    if (index < endIndex)
                    {
                        error = CommercialCampaignRunError.SafetyDutyUnserved;
                    }
                    else if (counterfactualFutureSafety[projection.Phase.PhaseId])
                    {
                        error = CommercialCampaignRunError.FutureSafetyAtRisk;
                    }
                }
                if (!error.HasValue && index < endIndex && !projection.PromiseSatisfied)
                {
                    error = CommercialCampaignRunError.KeptPromiseUnserved;
                }
            }

            committed = projections.Take(endIndex - startIndex).ToArray();
            bool completesChapter = _windowIndex + 1 >= _chapter.DecisionWindows.Count;
            if (!error.HasValue && completesChapter &&
                _chapterIndex + 1 < _definition.Chapters.Count &&
                !CanEnterNextChapter(_definition.Chapters[_chapterIndex + 1]))
            {
                error = CommercialCampaignRunError.ArithmeticOverflow;
            }
        }

        CommercialApprovalChecklist checklist = BuildApprovalChecklist(
            projections,
            counterfactualFutureSafety,
            connectionFailures,
            error is null);
        return new ApprovalEvaluation(
            projections,
            includesConstruction,
            connectionFailures,
            firstBlocking,
            checklist,
            error,
            connectionFailure,
            committed);
    }

    private IReadOnlyList<CommercialCampaignConnectionFailure> BuildConnectionFailures()
    {
        if (_campaignComplete || _chapter.ConnectionRequirements.Count == 0)
        {
            return Array.Empty<CommercialCampaignConnectionFailure>();
        }
        (CommercialWorldDefinition world, _) = ProjectedWorld();
        Dictionary<string, int> incident = world.Nodes.ToDictionary(
            item => item.NodeId,
            _ => 0,
            StringComparer.Ordinal);
        foreach (SpatialEdgeDefinition edge in world.Edges.Where(item => item.Commissioned))
        {
            incident[edge.FromNodeId]++;
            incident[edge.ToNodeId]++;
        }
        return _chapter.ConnectionRequirements
            .Select(requirement => new CommercialCampaignConnectionFailure(
                requirement.NodeId,
                incident[requirement.NodeId],
                requirement.MinimumConnections))
            .Where(failure => failure.CurrentConnections < failure.RequiredConnections)
            .ToArray();
    }

    private CommercialApprovalChecklist BuildApprovalChecklist(
        IReadOnlyList<CommercialPhaseProjection> projections,
        IReadOnlyDictionary<string, bool> counterfactualFutureSafety,
        IReadOnlyList<CommercialCampaignConnectionFailure> connectionFailures,
        bool canApprove)
    {
        if (_campaignComplete)
        {
            return CommercialApprovalChecklist.Empty;
        }
        int phaseCount = _chapter.OperatingPhases.Count;
        int firstPhaseNumber = checked(CurrentWindowStartPhaseIndex() + 1);
        var items = new List<CommercialApprovalChecklistItem>();
        long remainingCommandSlots = checked(MaximumAcceptedCommands - _commands.Count);
        bool commandCapacity = remainingCommandSlots > 0;
        items.Add(new CommercialApprovalChecklistItem(
            "command-capacity",
            commandCapacity
                ? $"명령 용량 · 승인 가능 · 남은 슬롯 {remainingCommandSlots}"
                : "명령 용량 · 승인 불가 · 남은 슬롯 없음",
            CommercialApprovalGateKind.CommandCapacity,
            commandCapacity,
            null,
            null,
            null,
            phaseCount,
            null,
            null,
            null,
            null,
            1,
            remainingCommandSlots,
            Math.Max(0, 1 - remainingCommandSlots),
            Array.Empty<string>(),
            Array.Empty<string>(),
            null));
        ConstructionSnapshot construction = _construction.GetSnapshot();
        bool constructionReady = construction.Phase == ConstructionPhase.Ready;
        items.Add(new CommercialApprovalChecklistItem(
            "construction-ready",
            constructionReady
                ? "공사 상태 · 발주 또는 공사 중인 설비 없음"
                : "공사 상태 · 현재 공사를 먼저 완료하거나 취소해야 함",
            CommercialApprovalGateKind.ConstructionReady,
            constructionReady,
            null,
            null,
            null,
            phaseCount,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null));

        if (_chapter.CityPromise is CommercialCityPromiseDefinition promise)
        {
            bool selected = PromiseReady();
            items.Add(new CommercialApprovalChecklistItem(
                "promise-decision",
                selected
                    ? $"도시 약속 · {promise.DisplayName} · 선택 완료"
                    : $"도시 약속 · {promise.DisplayName} · 선택 필요",
                CommercialApprovalGateKind.PromiseDecision,
                selected,
                null,
                null,
                null,
                phaseCount,
                CommercialObligationKind.CityPromise,
                promise.LoadId,
                null,
                null,
                null,
                null,
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                null));
        }

        Dictionary<string, CommercialCampaignConnectionFailure> failedConnections =
            connectionFailures.ToDictionary(item => item.NodeId, StringComparer.Ordinal);
        (CommercialWorldDefinition projectedWorld, _) = ProjectedWorld();
        Dictionary<string, int> connectionCounts = ConnectionCounts(projectedWorld);
        Dictionary<string, string> nodeNames = projectedWorld.Nodes.ToDictionary(
            item => item.NodeId,
            item => item.DisplayName,
            StringComparer.Ordinal);
        foreach (CommercialCampaignConnectionRequirement requirement in
                 _chapter.ConnectionRequirements)
        {
            int current = failedConnections.TryGetValue(
                requirement.NodeId,
                out CommercialCampaignConnectionFailure? failure)
                ? failure.CurrentConnections
                : connectionCounts[requirement.NodeId];
            bool passed = current >= requirement.MinimumConnections;
            items.Add(new CommercialApprovalChecklistItem(
                $"connection:{requirement.NodeId}",
                $"접속 조건 · {nodeNames[requirement.NodeId]} · {current}/{requirement.MinimumConnections}",
                CommercialApprovalGateKind.ConnectionRequirement,
                passed,
                null,
                null,
                null,
                phaseCount,
                null,
                null,
                requirement.NodeId,
                requirement.NodeId,
                requirement.MinimumConnections,
                current,
                Math.Max(0, requirement.MinimumConnections - current),
                Array.AsReadOnly(new[] { requirement.NodeId }),
                Array.Empty<string>(),
                null));
        }

        foreach (CommercialPhaseProjection projection in projections)
        {
            bool futureSafetyGate = !projection.IsInCurrentWindow &&
                counterfactualFutureSafety[projection.Phase.PhaseId];
            foreach (CommercialLoadBundleDefinition load in projection.Phase.Loads)
            {
                CommercialApprovalGateKind? kind = load.Obligation switch
                {
                    CommercialObligationKind.SafetyDuty when projection.IsInCurrentWindow =>
                        CommercialApprovalGateKind.SafetyDemand,
                    CommercialObligationKind.CityPromise
                        when projection.IsInCurrentWindow &&
                             _promiseDecision == CommercialPromiseDecision.Keep =>
                        CommercialApprovalGateKind.KeptPromiseDemand,
                    CommercialObligationKind.SafetyDuty when futureSafetyGate =>
                        CommercialApprovalGateKind.FutureSafety,
                    _ => null,
                };
                if (!kind.HasValue)
                {
                    continue;
                }
                ThermalLoadSupply supply = projection.Evaluation.Loads.Single(item =>
                    string.Equals(item.LoadId, load.LoadId, StringComparison.Ordinal));
                bool passed = supply.DeliveredKw == supply.DemandKw;
                CommercialSupplyDiagnostic? diagnostic = supply.Failure is null
                    ? null
                    : BuildSupplyDiagnostic(new BlockingSupply(projection, load, supply));
                string loadName = LoadDisplayName(load.LoadId);
                string gateLabel = kind == CommercialApprovalGateKind.FutureSafety
                    ? "후속 열 상태"
                    : ObligationDisplayName(load.Obligation);
                items.Add(new CommercialApprovalChecklistItem(
                    $"{GateId(kind.Value)}:{projection.Phase.PhaseId}:{load.LoadId}",
                    $"{projection.Phase.DisplayName} · {loadName} · {gateLabel} · {supply.DeliveredKw}/{supply.DemandKw} kW",
                    kind.Value,
                    passed,
                    projection.Phase.PhaseId,
                    projection.Phase.DisplayName,
                    PhaseNumber(projection.Phase.PhaseId),
                    phaseCount,
                    load.Obligation,
                    load.LoadId,
                    null,
                    supply.Failure?.AssetId,
                    supply.DemandKw,
                    supply.DeliveredKw,
                    checked(supply.DemandKw - supply.DeliveredKw),
                    supply.PathNodeIds,
                    supply.PathEdgeIds,
                    diagnostic));
            }
        }

        int blockers = items.Count(item => !item.Passed);
        return new CommercialApprovalChecklist(
            CurrentWindowOrNull()?.WindowId,
            checked(_windowIndex + 1),
            _chapter.DecisionWindows.Count,
            firstPhaseNumber,
            phaseCount,
            canApprove,
            blockers,
            items);
    }

    private IReadOnlyList<CommercialPhaseComparisonRow> BuildPhaseComparisonRows(
        IReadOnlyList<CommercialPhaseProjection> projections)
    {
        int phaseCount = _chapter.OperatingPhases.Count;
        var rows = new List<CommercialPhaseComparisonRow>();
        foreach (CommercialPhaseProjection projection in projections)
        {
            foreach (CommercialLoadBundleDefinition load in projection.Phase.Loads)
            {
                ThermalLoadSupply? supply = projection.Evaluation.Loads.FirstOrDefault(item =>
                    string.Equals(item.LoadId, load.LoadId, StringComparison.Ordinal));
                CommercialPhaseComparisonApplicability applicability = supply is not null
                    ? CommercialPhaseComparisonApplicability.Evaluated
                    : _promiseDecision == CommercialPromiseDecision.Defer
                        ? CommercialPhaseComparisonApplicability.DeferredByPromise
                        : CommercialPhaseComparisonApplicability.AwaitingPromiseDecision;
                (ThermalOperatingState? currentState, ThermalOperatingState? nextState) =
                    supply is null
                        ? (null, null)
                        : PathStateSummary(projection.Evaluation, supply);
                CommercialSupplyDiagnostic? diagnostic = supply?.Failure is null
                    ? null
                    : BuildSupplyDiagnostic(new BlockingSupply(projection, load, supply));
                rows.Add(new CommercialPhaseComparisonRow(
                    projection.Phase.PhaseId,
                    projection.Phase.DisplayName,
                    PhaseNumber(projection.Phase.PhaseId),
                    phaseCount,
                    projection.IsInCurrentWindow,
                    projection.Phase.Story?.Title,
                    projection.Phase.Story?.Body,
                    load.LoadId,
                    LoadDisplayName(load.LoadId),
                    load.Obligation,
                    ObligationDisplayName(load.Obligation),
                    applicability,
                    load.DemandKw,
                    supply?.DeliveredKw ?? 0,
                    supply?.SourceId,
                    supply?.SourceId is null ? null : SourceDisplayName(supply.SourceId),
                    supply?.MinimumRemainingKw,
                    currentState,
                    nextState,
                    supply?.PathNodeIds ?? Array.Empty<string>(),
                    supply?.PathEdgeIds ?? Array.Empty<string>(),
                    diagnostic));
            }
        }
        return Array.AsReadOnly(rows.ToArray());
    }

    private static (ThermalOperatingState? Current, ThermalOperatingState? Next)
        PathStateSummary(
            ThermalIntervalEvaluation evaluation,
            ThermalLoadSupply supply)
    {
        HashSet<string> pathAssets = supply.PathNodeIds
            .Concat(supply.PathEdgeIds)
            .ToHashSet(StringComparer.Ordinal);
        ThermalAssetUsage[] assets = evaluation.Assets
            .Where(item => pathAssets.Contains(item.AssetId))
            .ToArray();
        if (assets.Length == 0)
        {
            return (null, null);
        }
        return (
            assets.Select(item => item.State).MaxBy(StateRank),
            assets.Select(item => item.NextState).MaxBy(StateRank));
    }

    private static int StateRank(ThermalOperatingState state) => state switch
    {
        ThermalOperatingState.Continuous => 0,
        ThermalOperatingState.Emergency => 1,
        ThermalOperatingState.ProtectiveOutage => 2,
        ThermalOperatingState.OverLimit => 3,
        _ => throw new InvalidOperationException("Unknown thermal operating state."),
    };

    private static Dictionary<string, int> ConnectionCounts(CommercialWorldDefinition world)
    {
        Dictionary<string, int> result = world.Nodes.ToDictionary(
            item => item.NodeId,
            _ => 0,
            StringComparer.Ordinal);
        foreach (SpatialEdgeDefinition edge in world.Edges.Where(item => item.Commissioned))
        {
            result[edge.FromNodeId]++;
            result[edge.ToNodeId]++;
        }
        return result;
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
        for (int index = startIndex; index < _chapter.OperatingPhases.Count; index++)
        {
            CommercialOperatingPhaseDefinition phase = _chapter.OperatingPhases[index];
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
        else if (cloneSnapshot.Phase is ConstructionPhase.NodeBuilding or
                 ConstructionPhase.LineBuilding)
        {
            completed = clone.AdvanceToConstructionCompletion().Accepted;
        }
        return (WorldFromSnapshot(completed ? clone.GetSnapshot().World : snapshot.World), completed);
    }

    private bool WithinProjectionBudgetAndDeadline(ConstructionQuote quote)
    {
        if (!quote.Accepted || !HasCommandSlots(2) || quote.CostCashUnit!.Value > _cashUnit)
        {
            return false;
        }
        long? absoluteCompletionMinute = CheckedAbsoluteCompletionMinute(
            quote.CompletionMinute!.Value);
        if (!absoluteCompletionMinute.HasValue)
        {
            return false;
        }
        int? allowance = _chapter.DecisionWindows[_windowIndex].BuildMinutesAvailable;
        if (allowance is null)
        {
            return true;
        }
        long? deadline = CheckedWindowDeadline(allowance.Value);
        return deadline.HasValue && absoluteCompletionMinute.Value <= deadline.Value;
    }

    private ConstructionSession RebuildCurrentConstruction()
    {
        var clone = new ConstructionSession(_chapterStartWorld.ToSpatialWorld());
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
                throw new InvalidOperationException(
                    "Accepted campaign construction journal failed to replay.");
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

    private bool PromiseReady() => _chapter.CityPromise is null ||
        _promiseDecision is CommercialPromiseDecision.Keep or CommercialPromiseDecision.Defer;

    private bool HasCommandSlots(int required) => required >= 0 &&
        _commands.Count <= MaximumAcceptedCommands - required;

    private bool CurrentWindowPromiseSatisfied(
        IReadOnlyList<CommercialPhaseProjection> projections) =>
        projections.Where(item => item.IsInCurrentWindow).All(item => item.PromiseSatisfied);

    private BlockingSupply? FirstBlockingSupply(
        IReadOnlyList<CommercialPhaseProjection> projections,
        IReadOnlyDictionary<string, bool> counterfactualFutureSafety)
    {
        foreach (CommercialPhaseProjection projection in projections)
        {
            foreach (CommercialLoadBundleDefinition load in projection.Phase.Loads)
            {
                bool futureThermalCarryBlock = !projection.IsInCurrentWindow &&
                    !projection.SafetySatisfied &&
                    counterfactualFutureSafety[projection.Phase.PhaseId];
                bool required =
                    (load.Obligation == CommercialObligationKind.SafetyDuty &&
                     (projection.IsInCurrentWindow || futureThermalCarryBlock)) ||
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
                    return supply is null
                        ? null
                        : new BlockingSupply(projection, load, supply);
                }
            }
        }
        return null;
    }

    private CommercialSupplyDiagnostic BuildSupplyDiagnostic(BlockingSupply blocking)
    {
        ThermalSupplyFailure failure = blocking.Supply.Failure ??
            throw new InvalidOperationException("A blocking supply must have a typed failure.");
        (CommercialWorldDefinition world, _) = ProjectedWorld();
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        Dictionary<string, SpatialEdgeDefinition> edges = world.Edges.ToDictionary(
            item => item.EdgeId,
            StringComparer.Ordinal);
        ThermalAssetKind? assetKind = failure.AssetId switch
        {
            string id when nodes.ContainsKey(id) => ThermalAssetKind.Node,
            string id when edges.ContainsKey(id) => ThermalAssetKind.Edge,
            _ => null,
        };
        string? assetName = failure.AssetId switch
        {
            string id when nodes.TryGetValue(id, out SpatialNodeDefinition? node) =>
                node.DisplayName,
            string id when edges.TryGetValue(id, out SpatialEdgeDefinition? edge) =>
                EdgeDisplayName(world, edge),
            _ => null,
        };
        return new CommercialSupplyDiagnostic(
            blocking.Projection.Phase.PhaseId,
            blocking.Projection.Phase.DisplayName,
            PhaseNumber(blocking.Projection.Phase.PhaseId),
            _chapter.OperatingPhases.Count,
            blocking.Load.LoadId,
            LoadDisplayName(blocking.Load.LoadId),
            blocking.Load.Obligation,
            ObligationDisplayName(blocking.Load.Obligation),
            failure.Kind,
            failure.AttemptedSourceId,
            failure.AttemptedSourceId is null
                ? null
                : SourceDisplayName(failure.AttemptedSourceId),
            assetKind,
            failure.AssetId,
            assetName,
            failure.RequiredKw,
            failure.AvailableKw,
            checked(failure.RequiredKw - failure.AvailableKw),
            blocking.Supply.PathNodeIds,
            blocking.Supply.PathNodeIds.Select(id => nodes[id].DisplayName).ToArray(),
            blocking.Supply.PathEdgeIds,
            blocking.Supply.PathEdgeIds.Select(id => EdgeDisplayName(world, edges[id])).ToArray());
    }

    private int PhaseNumber(string phaseId) => checked(
        _chapter.OperatingPhases.ToList().FindIndex(item =>
            string.Equals(item.PhaseId, phaseId, StringComparison.Ordinal)) + 1);

    private string LoadDisplayName(string loadId) => _baseWorld.Loads.First(item =>
        string.Equals(item.LoadId, loadId, StringComparison.Ordinal)).DisplayName;

    private string SourceDisplayName(string sourceId) => _baseWorld.Sources.First(item =>
        string.Equals(item.SourceId, sourceId, StringComparison.Ordinal)).DisplayName;

    private static string ObligationDisplayName(CommercialObligationKind obligation) =>
        obligation switch
        {
            CommercialObligationKind.SafetyDuty => "안전 의무",
            CommercialObligationKind.CityPromise => "도시 약속",
            CommercialObligationKind.OperatingRecord => "운영 기록",
            _ => throw new InvalidOperationException("Unknown commercial obligation kind."),
        };

    private static string GateId(CommercialApprovalGateKind kind) => kind switch
    {
        CommercialApprovalGateKind.SafetyDemand => "safety",
        CommercialApprovalGateKind.KeptPromiseDemand => "promise-load",
        CommercialApprovalGateKind.FutureSafety => "future-safety",
        _ => kind.ToString(),
    };

    private static string EdgeDisplayName(
        CommercialWorldDefinition world,
        SpatialEdgeDefinition edge)
    {
        string lineName = world.LineClasses.First(item =>
            string.Equals(item.ClassId, edge.LineClassId, StringComparison.Ordinal)).DisplayName;
        Dictionary<string, string> nodeNames = world.Nodes.ToDictionary(
            item => item.NodeId,
            item => item.DisplayName,
            StringComparer.Ordinal);
        return $"{lineName} · {nodeNames[edge.FromNodeId]}–{nodeNames[edge.ToNodeId]}";
    }

    private IReadOnlyDictionary<string, bool> BuildCounterfactualFutureSafety()
    {
        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (_campaignComplete)
        {
            return result;
        }
        (CommercialWorldDefinition world, _) = ProjectedWorld();
        ThermalState state = ThermalState.Empty;
        int endIndex = CurrentWindowEndPhaseIndex();
        for (int index = endIndex; index < _chapter.OperatingPhases.Count; index++)
        {
            CommercialOperatingPhaseDefinition phase = _chapter.OperatingPhases[index];
            ThermalIntervalEvaluation evaluation = ThermalNetworkEvaluator.EvaluateInterval(
                world,
                BuildIntervalRequest(phase, world),
                state);
            result.Add(
                phase.PhaseId,
                RequiredLoadsDelivered(
                    phase,
                    evaluation,
                    CommercialObligationKind.SafetyDuty));
            state = evaluation.NextThermalState;
        }
        return result;
    }

    private int CurrentWindowStartPhaseIndex()
    {
        string id = _chapter.DecisionWindows[_windowIndex].BeforePhaseId;
        return _chapter.OperatingPhases.ToList().FindIndex(item => item.PhaseId == id);
    }

    private int CurrentWindowEndPhaseIndex()
    {
        if (_windowIndex + 1 >= _chapter.DecisionWindows.Count)
        {
            return _chapter.OperatingPhases.Count;
        }
        string next = _chapter.DecisionWindows[_windowIndex + 1].BeforePhaseId;
        return _chapter.OperatingPhases.ToList().FindIndex(item => item.PhaseId == next);
    }

    private void ResetToStart()
    {
        _commands.Clear();
        MarkJournalChanged();
        _chapterStartCommandCounts.Clear();
        _completedOutcomes.Clear();
        _committedPhases.Clear();
        _lastOutcome = null;
        _cashUnit = _definition.InitialSeed.InitialCashUnit;
        _chapterStartMinute = _definition.InitialSeed.StartMinute;
        _thermalState = new ThermalState(_definition.InitialSeed.CoolingAssetIds);
        _chapterStartWorld = CommercialCampaignLoader.BuildInitialWorld(
            _baseWorld,
            _definition.InitialSeed);
        _construction = new ConstructionSession(_chapterStartWorld.ToSpatialWorld());
        EnterChapter(0, 0, firstChapter: true);
    }

    private void EnterChapter(int chapterIndex, int commandCount, bool firstChapter)
    {
        long carriedMinute = firstChapter ? _chapterStartMinute : CurrentMinute;
        ThermalState carriedThermalState = _thermalState;
        CommercialWorldDefinition carriedWorld = firstChapter
            ? _chapterStartWorld
            : WorldFromSnapshot(_construction.GetSnapshot().World);

        _chapterIndex = chapterIndex;
        _chapter = _definition.Chapters[chapterIndex];
        _chapterStartMinute = checked(
            carriedMinute + _chapter.TimeAdvanceBeforeChapterMinutes);
        _thermalState = _chapter.ResetThermalStateBeforeChapter
            ? ThermalState.Empty
            : carriedThermalState;
        _cashUnit = checked(_cashUnit + _chapter.BudgetGrantCashUnit);
        _chapterStartWorld = carriedWorld with { InitialCashUnit = _cashUnit };
        _construction = new ConstructionSession(_chapterStartWorld.ToSpatialWorld());
        _promiseDecision = CommercialPromiseDecision.Unset;
        _committedPhases.Clear();
        _windowIndex = 0;
        _chapterStartCommandCount = commandCount;
        _windowStartCommandCount = commandCount;
        _windowStartMinute = _chapterStartMinute;
        _pendingProjectStartCommandCount = null;
        _recentProjectStartCommandCount = null;
        _campaignComplete = false;
        if (_chapterStartCommandCounts.Count == chapterIndex)
        {
            _chapterStartCommandCounts.Add(commandCount);
        }
        else if (_chapterStartCommandCounts.Count > chapterIndex)
        {
            _chapterStartCommandCounts[chapterIndex] = commandCount;
            _chapterStartCommandCounts.RemoveRange(
                chapterIndex + 1,
                _chapterStartCommandCounts.Count - chapterIndex - 1);
        }
        else
        {
            throw new InvalidOperationException("Campaign chapter checkpoints are discontinuous.");
        }
    }

    private CommercialCampaignChapterOutcome BuildOutcome()
    {
        CommercialStoryCard card = _chapter.CityPromise is null
            ? _chapter.ResultCards.Standard!
            : _promiseDecision == CommercialPromiseDecision.Keep
                ? _chapter.ResultCards.Kept!
                : _chapter.ResultCards.Deferred!;
        CommercialCampaignOutcomeFacts facts = BuildOutcomeFacts();
        return new CommercialCampaignChapterOutcome(
            _chapter.ChapterId,
            card,
            _promiseDecision,
            _committedPhases.ToArray(),
            facts,
            RenderFacts(facts),
            _cashUnit,
            CurrentMinute);
    }

    private CommercialCampaignOutcomeFacts BuildOutcomeFacts()
    {
        var loads = new List<CommercialCampaignLoadFact>();
        var thermal = new List<CommercialCampaignThermalFact>();
        Dictionary<string, CommercialOperatingPhaseDefinition> phases = _chapter.OperatingPhases
            .ToDictionary(item => item.PhaseId, StringComparer.Ordinal);
        foreach (CommercialCommittedPhaseResult committed in _committedPhases)
        {
            CommercialOperatingPhaseDefinition phase = phases[committed.PhaseId];
            Dictionary<string, CommercialLoadBundleDefinition> definitions = phase.Loads
                .ToDictionary(item => item.LoadId, StringComparer.Ordinal);
            foreach (ThermalLoadSupply supply in committed.Evaluation.Loads)
            {
                CommercialLoadBundleDefinition definition = definitions[supply.LoadId];
                loads.Add(new CommercialCampaignLoadFact(
                    phase.PhaseId,
                    definition.Obligation,
                    supply.LoadId,
                    supply.DemandKw,
                    supply.DeliveredKw,
                    supply.SourceId,
                    supply.MinimumRemainingKw,
                    supply.Failure));
            }
            foreach (ThermalAssetUsage asset in committed.Evaluation.Assets)
            {
                thermal.Add(new CommercialCampaignThermalFact(
                    phase.PhaseId,
                    asset.AssetId,
                    asset.AssetKind,
                    asset.UsedKw,
                    asset.ContinuousKw,
                    asset.EmergencyKw,
                    asset.State,
                    asset.NextState));
            }
        }
        return new CommercialCampaignOutcomeFacts(loads, thermal);
    }

    private IReadOnlyList<string> RenderFacts(CommercialCampaignOutcomeFacts facts)
    {
        Dictionary<string, string> phaseNames = _chapter.OperatingPhases.ToDictionary(
            item => item.PhaseId,
            item => item.DisplayName,
            StringComparer.Ordinal);
        Dictionary<string, string> loadNames = _baseWorld.Loads.ToDictionary(
            item => item.LoadId,
            item => item.DisplayName,
            StringComparer.Ordinal);
        Dictionary<string, string> sourceNames = _baseWorld.Sources.ToDictionary(
            item => item.SourceId,
            item => item.DisplayName,
            StringComparer.Ordinal);
        CommercialCampaignResultFactTemplatesDefinition templates = _chapter.ResultFactTemplates;
        var result = new List<string>();
        foreach (CommercialCampaignLoadFact fact in facts.Loads)
        {
            bool supplied = fact.DeliveredKw == fact.DemandKw && fact.SourceId is not null;
            string template = supplied ? templates.SuppliedLoad : templates.UnservedLoad;
            string rendered = template
                .Replace("{phase}", phaseNames[fact.PhaseId], StringComparison.Ordinal)
                .Replace("{load}", loadNames[fact.LoadId], StringComparison.Ordinal)
                .Replace("{demandKw}", Invariant(fact.DemandKw), StringComparison.Ordinal);
            if (supplied)
            {
                rendered = rendered
                    .Replace("{source}", sourceNames[fact.SourceId!], StringComparison.Ordinal)
                    .Replace(
                        "{minimumRemainingKw}",
                        Invariant(fact.MinimumRemainingKw!.Value),
                        StringComparison.Ordinal);
            }
            else
            {
                rendered = rendered.Replace(
                    "{deliveredKw}",
                    Invariant(fact.DeliveredKw),
                    StringComparison.Ordinal);
            }
            result.Add(rendered);
        }
        if (_chapter.CityPromise is CommercialCityPromiseDefinition promise)
        {
            string template = _promiseDecision == CommercialPromiseDecision.Keep
                ? templates.KeptPromise!
                : templates.DeferredPromise!;
            result.Add(template.Replace(
                "{promise}",
                promise.DisplayName,
                StringComparison.Ordinal));
        }
        result.Add(templates.RemainingCash.Replace(
            "{cashUnit}",
            Invariant(_cashUnit),
            StringComparison.Ordinal));
        return result;
    }

    private static IReadOnlyList<string> BuildEpilogueSummaryLines(
        CommercialCampaignChapterDefinition chapter,
        CommercialCampaignChapterOutcome outcome,
        IReadOnlyList<CommercialCampaignThermalFact> emergencyAssets,
        IReadOnlyList<CommercialCampaignThermalFact> protectiveOutageAssets,
        CommercialWorldDefinition world)
    {
        Dictionary<string, string> loadNames = world.Loads.ToDictionary(
            load => load.LoadId,
            load => load.DisplayName,
            StringComparer.Ordinal);
        string[] suppliedSafetyLoads = outcome.Facts.Loads
            .Where(fact => fact.Obligation == CommercialObligationKind.SafetyDuty &&
                fact.DeliveredKw == fact.DemandKw)
            .Select(fact => loadNames[fact.LoadId])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] unservedSafetyLoads = outcome.Facts.Loads
            .Where(fact => fact.Obligation == CommercialObligationKind.SafetyDuty &&
                fact.DeliveredKw != fact.DemandKw)
            .Select(fact => loadNames[fact.LoadId])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var result = new List<string>
        {
            unservedSafetyLoads.Length == 0
                ? $"{chapter.DisplayName}: 안전 의무 공급 · {SummarizeNames(suppliedSafetyLoads)}"
                : $"{chapter.DisplayName}: 안전 의무 미공급 · {SummarizeNames(unservedSafetyLoads)}",
        };

        CommercialCampaignLoadFact[] operatingRecords = outcome.Facts.Loads
            .Where(fact => fact.Obligation == CommercialObligationKind.OperatingRecord)
            .ToArray();
        if (operatingRecords.Length > 0)
        {
            IGrouping<string, CommercialCampaignLoadFact>[] recordsByLoad = operatingRecords
                .GroupBy(fact => fact.LoadId, StringComparer.Ordinal)
                .ToArray();
            string[] supplied = recordsByLoad
                .Where(group => group.All(fact => fact.DeliveredKw == fact.DemandKw))
                .Select(group => loadNames[group.Key])
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] unserved = recordsByLoad
                .Where(group => group.Any(fact => fact.DeliveredKw != fact.DemandKw))
                .Select(group => loadNames[group.Key])
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            result.Add(unserved.Length == 0
                ? $"{chapter.DisplayName}: 운영 기록 공급 · {SummarizeNames(supplied)}"
                : $"{chapter.DisplayName}: 운영 기록 유지 · {SummarizeNames(supplied)} · 중단 기록 · {SummarizeNames(unserved)}");
        }

        string[] assetNames = emergencyAssets
            .Select(asset => EpilogueAssetDisplayName(asset, world))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] protectiveNames = protectiveOutageAssets
            .Select(asset => EpilogueAssetDisplayName(asset, world))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string finalPhaseId = chapter.OperatingPhases[^1].PhaseId;
        string[] nextProtectiveNames = emergencyAssets
            .Where(asset =>
                asset.PhaseId == finalPhaseId &&
                asset.NextState == ThermalOperatingState.ProtectiveOutage)
            .Select(asset => EpilogueAssetDisplayName(asset, world))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        result.Add(
            $"{chapter.DisplayName}: 비상 운전 · {SummarizeNames(assetNames)} · 보호정지 · {SummarizeNames(protectiveNames)} · 다음 보호정지 · {SummarizeNames(nextProtectiveNames)} · 종료 자금 {Invariant(outcome.EndingCashUnit)}원");
        return result;
    }

    private static string SummarizeNames(IReadOnlyList<string> names) => names.Count switch
    {
        0 => "없음",
        1 => names[0],
        2 => $"{names[0]}, {names[1]}",
        _ => $"{names[0]}, {names[1]} 외 {Invariant(names.Count - 2)}개",
    };

    private static string EpilogueAssetDisplayName(
        CommercialCampaignThermalFact asset,
        CommercialWorldDefinition world)
    {
        if (asset.AssetKind == ThermalAssetKind.Node)
        {
            return world.Nodes.FirstOrDefault(node =>
                    string.Equals(node.NodeId, asset.AssetId, StringComparison.Ordinal))
                ?.DisplayName ?? "접속 설비";
        }

        SpatialEdgeDefinition? edge = world.Edges.FirstOrDefault(item =>
            string.Equals(item.EdgeId, asset.AssetId, StringComparison.Ordinal));
        if (edge is null)
        {
            return "선로 설비";
        }
        string lineName = world.LineClasses.First(item =>
            string.Equals(item.ClassId, edge.LineClassId, StringComparison.Ordinal)).DisplayName;
        Dictionary<string, string> nodeNames = world.Nodes.ToDictionary(
            node => node.NodeId,
            node => node.DisplayName,
            StringComparer.Ordinal);
        return $"{lineName} · {nodeNames[edge.FromNodeId]}–{nodeNames[edge.ToNodeId]}";
    }

    private static string Invariant(long value) => value.ToString(
        "#,0",
        System.Globalization.CultureInfo.InvariantCulture);

    private long? CheckedAbsoluteCompletionMinute(long constructionMinute)
    {
        try
        {
            return checked(_chapterStartMinute + constructionMinute);
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private long? CheckedWindowDeadline(int allowance)
    {
        try
        {
            return checked(_windowStartMinute + allowance);
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private bool CanEnterNextChapter(CommercialCampaignChapterDefinition next)
    {
        try
        {
            _ = checked(_cashUnit + next.BudgetGrantCashUnit);
            _ = checked(CurrentMinute + next.TimeAdvanceBeforeChapterMinutes);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private int? RecoveryTargetCommandCount(CommercialRecoveryKind kind) => kind switch
    {
        CommercialRecoveryKind.RecentProject when CanRollbackRecentProject =>
            _recentProjectStartCommandCount,
        CommercialRecoveryKind.DecisionWindow
            when !_campaignComplete && _commands.Count > _windowStartCommandCount =>
            _windowStartCommandCount,
        CommercialRecoveryKind.Chapter
            when !_campaignComplete && _commands.Count > _chapterStartCommandCount =>
            _chapterStartCommandCount,
        CommercialRecoveryKind.PreviousChapter when CanRewindPreviousChapter =>
            _chapterStartCommandCounts[_campaignComplete ? _chapterIndex : _chapterIndex - 1],
        _ => null,
    };

    private static CommercialRecoveryPreview DisabledRecovery(CommercialRecoveryKind kind) =>
        new(
            kind,
            false,
            null,
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            0,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Array.Empty<string>());

    private static (string? PhaseId, string? PhaseDisplay, int? PhaseNumber, int? PhaseCount)
        RecoveryPhase(CommercialCampaignSnapshot snapshot)
    {
        if (snapshot.CampaignComplete || snapshot.CurrentWindow is null)
        {
            return (null, null, null, null);
        }
        int index = snapshot.Chapter.OperatingPhases.ToList().FindIndex(item =>
            string.Equals(
                item.PhaseId,
                snapshot.CurrentWindow.BeforePhaseId,
                StringComparison.Ordinal));
        CommercialOperatingPhaseDefinition phase = snapshot.Chapter.OperatingPhases[index];
        return (
            phase.PhaseId,
            phase.DisplayName,
            checked(index + 1),
            snapshot.Chapter.OperatingPhases.Count);
    }

    private void ReplayPrefix(int count)
    {
        CommercialCoreCommand[] prefix = _commands.Take(count).ToArray();
        ResetToStart();
        foreach (CommercialCoreCommand command in prefix)
        {
            ApplyResult result = TryApply(command);
            if (!result.Accepted)
            {
                throw new InvalidOperationException(
                    "Accepted commercial campaign journal failed to replay.");
            }
            RecordAccepted(command, result);
        }
    }

    private void MarkJournalChanged()
    {
        _journalGeneration = checked(_journalGeneration + 1);
        _recoveryPreviewGeneration = -1;
        _recoveryPreviewCache = null;
    }

    private CommercialCampaignCommandResult Rejected(
        CommercialCampaignRunError error,
        ConstructionError? constructionError = null,
        CommercialCampaignConnectionFailure? connectionFailure = null) =>
        new(false, error, constructionError, connectionFailure, GetSnapshot());

    private static bool ValidShape(CommercialCoreCommand command)
    {
        bool noIds = command.FirstId is null &&
            command.SecondId is null &&
            command.ThirdId is null;
        bool noPoint = command.Position is null && command.PointIndex is null;
        return command.Kind switch
        {
            CommercialCoreCommandKind.SetNodeDraft =>
                Text(command.FirstId) && command.SecondId is null && command.ThirdId is null &&
                command.Position.HasValue && command.PointIndex is null &&
                command.PromiseDecision is null,
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

    private sealed record BlockingSupply(
        CommercialPhaseProjection Projection,
        CommercialLoadBundleDefinition Load,
        ThermalLoadSupply Supply);

    private sealed record ApprovalEvaluation(
        IReadOnlyList<CommercialPhaseProjection> Projections,
        bool ProjectionIncludesCurrentConstruction,
        IReadOnlyList<CommercialCampaignConnectionFailure> ConnectionFailures,
        BlockingSupply? FirstBlocking,
        CommercialApprovalChecklist Checklist,
        CommercialCampaignRunError? Error,
        CommercialCampaignConnectionFailure? ConnectionFailure,
        IReadOnlyList<CommercialPhaseProjection> Committed);

    private sealed record RemovedWorkSummary(
        int CompletedNodeProjectCount,
        int CompletedLineProjectCount,
        int CompletedLineRoutePointCount,
        ConstructionKind? DiscardedDraftKind,
        int DiscardedDraftRoutePointCount,
        ConstructionKind? DiscardedActiveConstructionKind,
        int DiscardedActiveConstructionRoutePointCount);

    private sealed record ApplyResult(
        bool Accepted,
        CommercialCampaignRunError? Error,
        ConstructionError? ConstructionError,
        CommercialCampaignConnectionFailure? ConnectionFailure,
        Action? AfterRecorded)
    {
        public static ApplyResult Success(Action? afterRecorded = null) =>
            new(true, null, null, null, afterRecorded);

        public static ApplyResult Failure(
            CommercialCampaignRunError error,
            ConstructionError? constructionError = null,
            CommercialCampaignConnectionFailure? connectionFailure = null) =>
            new(false, error, constructionError, connectionFailure, null);
    }
}
