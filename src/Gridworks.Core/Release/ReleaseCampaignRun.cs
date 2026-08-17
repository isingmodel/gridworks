namespace Gridworks.Core.Release;

public sealed class ReleaseCampaignRun
{
    private const int MaximumCommandCount = 20_000;

    private readonly ReleaseCampaignDefinition _definition;
    private readonly ReleaseWorldDefinition _baseWorld;
    private readonly string _campaignSha256;
    private readonly string _worldSha256;
    private readonly List<ReleaseCampaignCommand> _commands = [];
    private ReleaseConstructionSession _session;
    private int _chapterIndex;
    private int _chapterStartCommandCount;
    private long _cashUnit;
    private bool _campaignComplete;

    public ReleaseCampaignRun(
        ReleaseCampaignDefinition definition,
        ReleaseWorldDefinition world,
        string campaignSha256,
        string worldSha256)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(world);
        ReleaseCampaignLoader.Validate(definition, world);
        ValidateSha256(campaignSha256, nameof(campaignSha256));
        ValidateSha256(worldSha256, nameof(worldSha256));

        _definition = definition with { };
        _baseWorld = world with { };
        _campaignSha256 = campaignSha256;
        _worldSha256 = worldSha256;
        _session = new ReleaseConstructionSession(BuildInitialWorld());
        _cashUnit = checked(definition.InitialCashUnit + definition.Chapters[0].BudgetGrantCashUnit);
    }

    public ReleaseCampaignChapter CurrentChapter => _definition.Chapters[_chapterIndex];

    public ReleaseCampaignSnapshot GetSnapshot() => BuildSnapshot();

    public ReleaseNodePlacementPreview PreviewNodePlacement(
        string nodeClassId,
        ReleasePoint position) =>
        _session.PreviewNodePlacement(nodeClassId, position);

    public ReleaseLinePointPreview PreviewLinePoint(ReleasePoint position) =>
        _session.PreviewLinePoint(position);

    public ReleaseConstructionQuote PreviewNodeOrder() =>
        _session.PreviewNodeOrder();

    public ReleaseConstructionQuote PreviewLineOrder() =>
        _session.PreviewLineOrder();

    public ReleaseCampaignCommandResult Execute(ReleaseCampaignCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommandShape(command);
        if (_campaignComplete)
        {
            return Rejected(ReleaseCampaignError.CampaignComplete);
        }

        if (command.Kind == ReleaseCampaignCommandKind.EvaluateChapter)
        {
            return EvaluateChapter(command);
        }

        long orderCost = 0;
        if (command.Kind == ReleaseCampaignCommandKind.OrderNode)
        {
            ReleaseConstructionQuote quote = _session.PreviewNodeOrder();
            if (!quote.Accepted)
            {
                return Rejected(
                    ReleaseCampaignError.ConstructionRejected,
                    quote.Error);
            }
            orderCost = quote.CostCashUnit!.Value;
        }
        else if (command.Kind == ReleaseCampaignCommandKind.OrderLine)
        {
            ReleaseConstructionQuote quote = _session.PreviewLineOrder();
            if (!quote.Accepted)
            {
                return Rejected(
                    ReleaseCampaignError.ConstructionRejected,
                    quote.Error);
            }
            orderCost = quote.CostCashUnit!.Value;
        }

        if (orderCost > _cashUnit)
        {
            return Rejected(ReleaseCampaignError.InsufficientCash);
        }

        ReleaseConstructionCommandResult construction = Dispatch(command);
        if (!construction.Accepted)
        {
            return Rejected(
                ReleaseCampaignError.ConstructionRejected,
                construction.Error);
        }

        _cashUnit = checked(_cashUnit - orderCost);
        _commands.Add(command);
        return Accepted();
    }

    public ReleaseCampaignSnapshot RestartChapter()
    {
        ReleaseCampaignCommand[] prefix = _commands
            .Take(_chapterStartCommandCount)
            .ToArray();
        Replay(prefix);
        return BuildSnapshot();
    }

    public ReleaseCampaignSave CaptureSave() => new(
        ReleaseCampaignSave.SupportedSchemaVersion,
        _definition.CampaignId,
        _campaignSha256,
        _baseWorld.WorldId,
        _worldSha256,
        _commands);

    public static ReleaseCampaignRun Restore(
        ReleaseCampaignDefinition definition,
        ReleaseWorldDefinition world,
        string campaignSha256,
        string worldSha256,
        ReleaseCampaignSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (!string.Equals(
                save.SchemaVersion,
                ReleaseCampaignSave.SupportedSchemaVersion,
                StringComparison.Ordinal) ||
            !string.Equals(save.CampaignId, definition.CampaignId, StringComparison.Ordinal) ||
            !string.Equals(save.CampaignSha256, campaignSha256, StringComparison.Ordinal) ||
            !string.Equals(save.WorldId, world.WorldId, StringComparison.Ordinal) ||
            !string.Equals(save.WorldSha256, worldSha256, StringComparison.Ordinal))
        {
            throw new ReleasePersistenceValidationException(
                "저장 기록이 현재 캠페인 또는 지도와 맞지 않습니다.");
        }
        if (save.Commands.Count > MaximumCommandCount)
        {
            throw new ReleasePersistenceValidationException(
                $"저장 명령은 {MaximumCommandCount}개를 넘을 수 없습니다.");
        }

        var run = new ReleaseCampaignRun(
            definition,
            world,
            campaignSha256,
            worldSha256);
        run.Replay(save.Commands);
        return run;
    }

    private ReleaseCampaignCommandResult EvaluateChapter(ReleaseCampaignCommand command)
    {
        if (_session.GetSnapshot().Phase != ReleaseConstructionPhase.Ready)
        {
            return Rejected(ReleaseCampaignError.WrongPhase);
        }

        ReleaseChapterAssessment assessment = AssessCurrentChapter();
        if (!assessment.Passed)
        {
            return new ReleaseCampaignCommandResult(
                false,
                ReleaseCampaignError.ObjectiveNotMet,
                null,
                BuildSnapshot(),
                assessment,
                null);
        }

        ReleaseCampaignChapter completedChapter = CurrentChapter;
        _commands.Add(command);
        if (_chapterIndex == _definition.Chapters.Count - 1)
        {
            _campaignComplete = true;
        }
        else
        {
            _chapterIndex++;
            _cashUnit = checked(
                _cashUnit + _definition.Chapters[_chapterIndex].BudgetGrantCashUnit);
            _chapterStartCommandCount = _commands.Count;
        }

        return new ReleaseCampaignCommandResult(
            true,
            null,
            null,
            BuildSnapshot(),
            assessment,
            completedChapter);
    }

    private ReleaseChapterAssessment AssessCurrentChapter()
    {
        ReleaseCampaignSnapshot snapshot = BuildSnapshot();
        foreach (string loadId in CurrentChapter.RequiredNormalLoadIds)
        {
            ReleaseLoadSupply load = snapshot.NormalEvaluation.Loads.Single(item =>
                string.Equals(item.LoadId, loadId, StringComparison.Ordinal));
            if (load.DeliveredKw != load.DemandKw)
            {
                return new ReleaseChapterAssessment(
                    false,
                    loadId,
                    load.Failure,
                    false,
                    null,
                    null,
                    null);
            }
        }
        foreach (string loadId in CurrentChapter.RequiredEventLoadIds)
        {
            ReleaseLoadSupply load = snapshot.EventEvaluation.Loads.Single(item =>
                string.Equals(item.LoadId, loadId, StringComparison.Ordinal));
            if (load.DeliveredKw != load.DemandKw)
            {
                return new ReleaseChapterAssessment(
                    false,
                    loadId,
                    load.Failure,
                    true,
                    null,
                    null,
                    null);
            }
        }
        foreach (ReleaseConnectionRequirement requirement in CurrentChapter.ConnectionRequirements)
        {
            ReleaseNodeUsage usage = snapshot.NormalEvaluation.Nodes.Single(item =>
                string.Equals(item.NodeId, requirement.NodeId, StringComparison.Ordinal));
            if (usage.ConnectionCount < requirement.MinimumConnections)
            {
                return new ReleaseChapterAssessment(
                    false,
                    null,
                    null,
                    false,
                    requirement.NodeId,
                    requirement.MinimumConnections,
                    usage.ConnectionCount);
            }
        }
        return new ReleaseChapterAssessment(true, null, null, false, null, null, null);
    }

    private ReleaseCampaignSnapshot BuildSnapshot()
    {
        ReleaseConstructionSnapshot construction = _session.GetSnapshot();
        ReleaseWorldDefinition effectiveWorld = EffectiveWorld(construction.World, CurrentChapter);
        ReleaseNetworkEvaluation normal = ReleaseNetworkEvaluator.Evaluate(effectiveWorld);
        ReleaseNetworkEvaluation incident = CurrentChapter.Event is null
            ? normal
            : ReleaseNetworkEvaluator.Evaluate(
                effectiveWorld,
                new ReleaseContingency(
                    CurrentChapter.Event.UnavailableNodeIds.ToHashSet(StringComparer.Ordinal),
                    CurrentChapter.Event.UnavailableEdgeIds.ToHashSet(StringComparer.Ordinal),
                    CurrentChapter.Event.ActiveRiskAreaIds.ToHashSet(StringComparer.Ordinal)));
        ReleaseConstructionSnapshot projected = construction with
        {
            World = effectiveWorld,
            Evaluation = normal,
        };
        return new ReleaseCampaignSnapshot(
            projected,
            CurrentChapter,
            _chapterIndex,
            _definition.Chapters.Count,
            _cashUnit,
            normal,
            incident,
            _campaignComplete,
            _commands.Count,
            _chapterStartCommandCount);
    }

    private ReleaseConstructionCommandResult Dispatch(ReleaseCampaignCommand command) =>
        command.Kind switch
        {
            ReleaseCampaignCommandKind.SetNodeDraft =>
                _session.SetNodeDraft(command.NodeClassId!, command.Position!.Value),
            ReleaseCampaignCommandKind.CancelNodeDraft =>
                _session.CancelNodeDraft(),
            ReleaseCampaignCommandKind.StartLineDraft =>
                _session.StartLineDraft(
                    command.StartNodeId!,
                    command.LineClassId!,
                    command.PoleClassId!),
            ReleaseCampaignCommandKind.AddLinePoint =>
                _session.AddLinePoint(command.Position!.Value),
            ReleaseCampaignCommandKind.UndoLinePoint =>
                _session.UndoLinePoint(),
            ReleaseCampaignCommandKind.CancelLineDraft =>
                _session.CancelLineDraft(),
            ReleaseCampaignCommandKind.OrderNode =>
                _session.OrderNode(),
            ReleaseCampaignCommandKind.OrderLine =>
                _session.OrderLine(),
            ReleaseCampaignCommandKind.AdvanceConstruction =>
                _session.AdvanceToConstructionCompletion(),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

    private void Replay(IReadOnlyList<ReleaseCampaignCommand> commands)
    {
        _session = new ReleaseConstructionSession(BuildInitialWorld());
        _commands.Clear();
        _chapterIndex = 0;
        _chapterStartCommandCount = 0;
        _cashUnit = checked(
            _definition.InitialCashUnit + _definition.Chapters[0].BudgetGrantCashUnit);
        _campaignComplete = false;

        for (int index = 0; index < commands.Count; index++)
        {
            ReleaseCampaignCommand command = commands[index]
                ?? throw new ReleasePersistenceValidationException(
                    $"저장 명령 {index}이 비어 있습니다.");
            ReleaseCampaignCommandResult result;
            try
            {
                result = Execute(command);
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidOperationException or OverflowException)
            {
                throw new ReleasePersistenceValidationException(
                    $"저장 명령 {index}을 복원하지 못했습니다.",
                    exception);
            }
            if (!result.Accepted)
            {
                throw new ReleasePersistenceValidationException(
                    $"저장 명령 {index}이 현재 규칙에서 거부됐습니다.");
            }
        }
    }

    private ReleaseWorldDefinition BuildInitialWorld()
    {
        HashSet<string> initialEdges = _definition.InitialEdgeIds
            .ToHashSet(StringComparer.Ordinal);
        return _baseWorld with
        {
            Edges = _baseWorld.Edges
                .Where(item => initialEdges.Contains(item.EdgeId))
                .ToArray(),
        };
    }

    private static ReleaseWorldDefinition EffectiveWorld(
        ReleaseWorldDefinition world,
        ReleaseCampaignChapter chapter)
    {
        Dictionary<string, ReleaseLoadDefinition> definitions = world.Loads
            .ToDictionary(item => item.LoadId, StringComparer.Ordinal);
        ReleaseLoadDefinition[] activeLoads = chapter.ActiveLoads
            .Select(item => definitions[item.LoadId] with { DemandKw = item.DemandKw })
            .ToArray();
        return world with { Loads = activeLoads };
    }

    private ReleaseCampaignCommandResult Accepted() => new(
        true,
        null,
        null,
        BuildSnapshot(),
        null,
        null);

    private ReleaseCampaignCommandResult Rejected(
        ReleaseCampaignError error,
        ReleaseConstructionError? constructionError = null) => new(
            false,
            error,
            constructionError,
            BuildSnapshot(),
            null,
            null);

    private static void ValidateCommandShape(ReleaseCampaignCommand command)
    {
        if (!Enum.IsDefined(command.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(command));
        }
        bool hasPosition = command.Position is not null;
        bool hasNodeClass = command.NodeClassId is not null;
        bool hasStart = command.StartNodeId is not null;
        bool hasLineClass = command.LineClassId is not null;
        bool hasPoleClass = command.PoleClassId is not null;
        bool valid = command.Kind switch
        {
            ReleaseCampaignCommandKind.SetNodeDraft =>
                hasPosition && hasNodeClass && !hasStart && !hasLineClass && !hasPoleClass,
            ReleaseCampaignCommandKind.AddLinePoint =>
                hasPosition && !hasNodeClass && !hasStart && !hasLineClass && !hasPoleClass,
            ReleaseCampaignCommandKind.StartLineDraft =>
                !hasPosition && !hasNodeClass && hasStart && hasLineClass && hasPoleClass,
            _ => !hasPosition && !hasNodeClass && !hasStart && !hasLineClass && !hasPoleClass,
        };
        if (!valid)
        {
            throw new ArgumentException("Campaign command shape is invalid.", nameof(command));
        }
    }

    private static void ValidateSha256(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length != 64 || value.Any(character =>
                character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "SHA-256 must use 64 lowercase hexadecimal characters.",
                parameterName);
        }
    }
}
