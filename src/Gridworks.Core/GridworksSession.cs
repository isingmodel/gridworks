using System.Collections.ObjectModel;

namespace Gridworks.Core;

public sealed class GridworksSession
{
    private readonly ScenarioDefinition _scenario;
    private readonly IReadOnlyDictionary<string, NodeDefinition> _nodes;
    private readonly IReadOnlyDictionary<string, EdgeDefinition> _edges;
    private readonly IReadOnlyDictionary<string, ProjectDefinition> _projects;
    private readonly IReadOnlyDictionary<string, LoadDefinition> _loads;
    private readonly IReadOnlyDictionary<string, EvaluationCaseDefinition> _evaluationCases;
    private readonly IReadOnlyDictionary<string, EventDefinition> _events;
    private readonly RuntimeProject _townProject;
    private readonly IReadOnlyDictionary<CorridorDesign, RuntimeProject> _corridorProjects;
    private readonly string _hospitalLoadId;
    private readonly string _townLoadId;
    private readonly long _dieselCapacityKwMinute;
    private readonly long _totalInternalCapacityKwMinute;
    private readonly HashSet<string> _commissionedEdgeIds;
    private readonly HashSet<string> _eventRemovedEdgeIds = new(StringComparer.Ordinal);
    private readonly SettlementAccumulator _interval;
    private readonly SettlementAccumulator _cumulative;

    private int _currentMinute;
    private long _cash;
    private CorridorDesign? _selectedCorridor;
    private InternalPowerStage _hospitalInternalStage;
    private long _hospitalInternalRemainingKwMinute;
    private bool _isComplete;

    public GridworksSession(ScenarioDefinition scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        _scenario = scenario;

        try
        {
            _nodes = ToReadOnlyIndex(scenario.Nodes, node => node.Id, "node");
            _edges = ToReadOnlyIndex(scenario.Edges, edge => edge.Id, "edge");
            _projects = ToReadOnlyIndex(scenario.Projects, project => project.Id, "project");
            _loads = ToReadOnlyIndex(scenario.Loads, load => load.NodeId, "load");
            _evaluationCases = ToReadOnlyIndex(
                scenario.EvaluationCases,
                evaluationCase => evaluationCase.Id,
                "evaluation case");
            _events = ToReadOnlyIndex(scenario.Events, gameEvent => gameEvent.Id, "event");

            _hospitalLoadId = scenario.HospitalInternalPower.LoadNodeId;
            string[] otherLoads = scenario.Loads
                .Select(load => load.NodeId)
                .Where(loadId => !string.Equals(loadId, _hospitalLoadId, StringComparison.Ordinal))
                .OrderBy(loadId => loadId, StringComparer.Ordinal)
                .ToArray();
            if (otherLoads.Length != 1)
            {
                throw new FixtureValidationException(
                    "Scope 0B requires exactly one hospital load and one town load.");
            }
            _townLoadId = otherLoads[0];

            SupplyPathDefinition townPrimary = GetSinglePrimaryPath(_townLoadId);
            ProjectDefinition[] townCandidates = scenario.Projects
                .Where(project => townPrimary.EdgeIds.Contains(project.EdgeId, StringComparer.Ordinal))
                .Where(project => project.AllowedOrderMinute == scenario.Milestones[0].Minute)
                .ToArray();
            if (townCandidates.Length != 1)
            {
                throw new FixtureValidationException(
                    "Scope 0B town feeder project is not uniquely identifiable.");
            }

            _townProject = new RuntimeProject(townCandidates[0]);
            Dictionary<CorridorDesign, RuntimeProject> corridorProjects = new();
            foreach (CorridorDesign design in Enum.GetValues<CorridorDesign>())
            {
                string edgeId = ToFixtureId(design);
                ProjectDefinition[] matches = scenario.Projects
                    .Where(project => string.Equals(project.EdgeId, edgeId, StringComparison.Ordinal))
                    .ToArray();
                if (matches.Length != 1)
                {
                    throw new FixtureValidationException(
                        $"Corridor project for '{edgeId}' is not uniquely identifiable.");
                }
                corridorProjects.Add(design, new RuntimeProject(matches[0]));
            }
            _corridorProjects = new ReadOnlyDictionary<CorridorDesign, RuntimeProject>(corridorProjects);

            if (scenario.HospitalInternalPower.Stages.Count != 2)
            {
                throw new FixtureValidationException("Scope 0B requires UPS and DIESEL stages.");
            }
            _dieselCapacityKwMinute = scenario.HospitalInternalPower.Stages[1].EnergyKwMinute;
            _totalInternalCapacityKwMinute = checked(
                scenario.HospitalInternalPower.Stages[0].EnergyKwMinute +
                _dieselCapacityKwMinute);
            _hospitalInternalRemainingKwMinute = _totalInternalCapacityKwMinute;

            _commissionedEdgeIds = scenario.Edges
                .Where(edge => edge.InitialConstructionState == ConstructionState.Commissioned)
                .Select(edge => edge.Id)
                .ToHashSet(StringComparer.Ordinal);
            _currentMinute = scenario.Milestones[0].Minute;
            _cash = scenario.Economy.InitialCash;
            _interval = new SettlementAccumulator(scenario.Loads.Select(load => load.NodeId));
            _cumulative = new SettlementAccumulator(scenario.Loads.Select(load => load.NodeId));

            ValidateRuntimeContract();
            _ = ResolveUtilityPaths(
                _commissionedEdgeIds,
                _eventRemovedEdgeIds,
                GetActiveLoadIds(_currentMinute),
                _selectedCorridor);
        }
        catch (FixtureValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is OverflowException or InvalidOperationException)
        {
            throw new FixtureValidationException(
                "Scenario cannot initialize a Scope 0B session.", exception);
        }
    }

    public PublicSnapshot GetSnapshot()
    {
        SortedSet<string> activeLoads = GetActiveLoadIds(_currentMinute);
        IReadOnlyDictionary<string, string?> paths = ResolveUtilityPaths(
            _commissionedEdgeIds,
            _eventRemovedEdgeIds,
            activeLoads,
            _selectedCorridor);

        return new PublicSnapshot(
            _currentMinute,
            _cash,
            _townProject.State,
            GetCorridorProjectState(),
            _selectedCorridor,
            FreezeSorted(_commissionedEdgeIds),
            FreezeSorted(_eventRemovedEdgeIds),
            FreezeSorted(activeLoads),
            paths,
            _hospitalInternalStage,
            _hospitalInternalRemainingKwMinute,
            _interval.Snapshot(),
            _cumulative.Snapshot(),
            _isComplete);
    }

    public CommandResult OrderTownFeeder()
    {
        if (_currentMinute != _townProject.Definition.AllowedOrderMinute)
        {
            return Rejected(CommandErrorCode.WrongTime);
        }
        if (_townProject.State != ProjectState.NotOrdered)
        {
            return Rejected(CommandErrorCode.AlreadyOrdered);
        }

        try
        {
            _cash = checked(_cash - _townProject.Definition.CostCashUnit);
            _townProject.Order();
            _interval.Reset();
            return Accepted();
        }
        catch (OverflowException exception)
        {
            throw new FixtureValidationException("Town order cash calculation overflowed.", exception);
        }
    }

    public CommandResult OrderCorridor(CorridorDesign design)
    {
        if (!Enum.IsDefined(design))
        {
            throw new ArgumentOutOfRangeException(nameof(design), design, "Unknown corridor design.");
        }

        RuntimeProject project = _corridorProjects[design];
        if (_currentMinute != project.Definition.AllowedOrderMinute)
        {
            return Rejected(CommandErrorCode.WrongTime);
        }
        if (_townProject.State != ProjectState.Commissioned)
        {
            return Rejected(CommandErrorCode.RequiredActionPending);
        }
        if (_selectedCorridor.HasValue)
        {
            return Rejected(CommandErrorCode.AlreadyOrdered);
        }

        try
        {
            _cash = checked(_cash - project.Definition.CostCashUnit);
            _selectedCorridor = design;
            project.Order();
            _interval.Reset();
            return Accepted();
        }
        catch (OverflowException exception)
        {
            throw new FixtureValidationException("Corridor order cash calculation overflowed.", exception);
        }
    }

    public CommandResult AdvanceToNextMilestone()
    {
        if (_isComplete)
        {
            return Rejected(CommandErrorCode.NoNextMilestone);
        }
        if (_currentMinute == _townProject.Definition.AllowedOrderMinute &&
            _townProject.State == ProjectState.NotOrdered)
        {
            return Rejected(CommandErrorCode.RequiredActionPending);
        }
        if (_currentMinute == GetCorridorOrderMinute() && !_selectedCorridor.HasValue)
        {
            return Rejected(CommandErrorCode.RequiredActionPending);
        }

        MilestoneDefinition? target = _scenario.Milestones
            .Where(milestone => milestone.Minute > _currentMinute)
            .OrderBy(milestone => milestone.Minute)
            .FirstOrDefault();
        if (target is null)
        {
            return Rejected(CommandErrorCode.NoNextMilestone);
        }

        try
        {
            _interval.Reset();
            List<BoundaryTrace> trace = new();
            while (_currentMinute < target.Minute)
            {
                int boundary = FindNextBoundary(target.Minute);
                SettleUntil(boundary);
                _currentMinute = boundary;
                ApplyBoundary();

                if (boundary < target.Minute && IsUpsDepletionBoundary())
                {
                    trace.Add(new BoundaryTrace("UPS_DEPLETED", GetSnapshot()));
                }
            }

            if (_currentMinute == _scenario.Milestones[^1].Minute)
            {
                _isComplete = true;
            }

            return new CommandResult(true, null, GetSnapshot(), Array.AsReadOnly(trace.ToArray()));
        }
        catch (FixtureValidationException)
        {
            throw;
        }
        catch (OverflowException exception)
        {
            throw new FixtureValidationException("Advance calculation overflowed.", exception);
        }
    }

    public RemovalEvaluation EvaluateRemoval(EvaluationDesign design, string caseId)
    {
        if (!Enum.IsDefined(design))
        {
            throw new ArgumentOutOfRangeException(nameof(design), design, "Unknown evaluation design.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        if (!_evaluationCases.TryGetValue(caseId, out EvaluationCaseDefinition? evaluationCase))
        {
            throw new ArgumentException($"Unknown evaluation case '{caseId}'.", nameof(caseId));
        }

        HashSet<string> commissioned = _scenario.Edges
            .Where(edge => edge.InitialConstructionState == ConstructionState.Commissioned)
            .Select(edge => edge.Id)
            .ToHashSet(StringComparer.Ordinal);
        commissioned.Add(_townProject.Definition.EdgeId);

        CorridorDesign? selected = design switch
        {
            EvaluationDesign.NoBuild => null,
            EvaluationDesign.RiverParallel => CorridorDesign.RiverParallel,
            EvaluationDesign.NorthDetour => CorridorDesign.NorthDetour,
            _ => throw new ArgumentOutOfRangeException(nameof(design), design, null),
        };
        if (selected.HasValue)
        {
            commissioned.Add(_corridorProjects[selected.Value].Definition.EdgeId);
        }

        HashSet<string> removed = commissioned
            .Where(edgeId => MatchesSelector(_edges[edgeId], evaluationCase))
            .ToHashSet(StringComparer.Ordinal);
        SortedSet<string> activeLoads = new(_loads.Keys, StringComparer.Ordinal);
        IReadOnlyDictionary<string, string?> paths = ResolveUtilityPaths(
            commissioned,
            removed,
            activeLoads,
            selected);

        string? townPath = paths[_townLoadId];
        string? hospitalPath = paths[_hospitalLoadId];
        return new RemovalEvaluation(
            design,
            caseId,
            FreezeSorted(removed),
            townPath is not null,
            hospitalPath is not null,
            townPath,
            hospitalPath);
    }

    private void ValidateRuntimeContract()
    {
        if (_scenario.Milestones.Count == 0 || _scenario.Milestones[0].Minute != 0)
        {
            throw new FixtureValidationException("Scope 0B session must start at minute 0.");
        }
        if (_loads.Count != 2)
        {
            throw new FixtureValidationException("Scope 0B session requires exactly two loads.");
        }
        if (_events.Count != 1)
        {
            throw new FixtureValidationException("Scope 0B session requires exactly one event.");
        }
        if (_townProject.Definition.CostCashUnit > _scenario.Economy.InitialCash)
        {
            throw new FixtureValidationException("Town project is not affordable at session start.");
        }

        long cashAfterTown = checked(
            _scenario.Economy.InitialCash - _townProject.Definition.CostCashUnit);
        foreach (RuntimeProject corridor in _corridorProjects.Values)
        {
            if (corridor.Definition.CostCashUnit > cashAfterTown)
            {
                throw new FixtureValidationException(
                    $"Corridor project '{corridor.Definition.Id}' is not deterministically affordable.");
            }
        }

        foreach (CorridorDesign design in Enum.GetValues<CorridorDesign>())
        {
            string requiredEdgeId = _corridorProjects[design].Definition.EdgeId;
            SupplyPathDefinition[] backupPaths = _scenario.PermittedSupplyPaths
                .Where(path => path.LoadNodeId == _hospitalLoadId && path.Role == PathRole.Backup)
                .Where(path => string.Equals(
                    path.RequiredCommissionedEdgeId,
                    requiredEdgeId,
                    StringComparison.Ordinal))
                .ToArray();
            if (backupPaths.Length != 1)
            {
                throw new FixtureValidationException(
                    $"Hospital backup for '{requiredEdgeId}' is not uniquely authored.");
            }
        }

        if (_scenario.PermittedSupplyPaths.Any(path =>
            path.LoadNodeId == _townLoadId && path.Role == PathRole.Backup))
        {
            throw new FixtureValidationException("The town cannot have a backup path in Scope 0B.");
        }
    }

    private int FindNextBoundary(int publicTarget)
    {
        int next = publicTarget;
        foreach (RuntimeProject project in AllRuntimeProjects())
        {
            if (project.State == ProjectState.Building &&
                project.CompletionMinute > _currentMinute &&
                project.CompletionMinute < next)
            {
                next = project.CompletionMinute.Value;
            }
        }
        foreach (LoadDefinition load in _loads.Values)
        {
            if (load.ActiveMinute > _currentMinute && load.ActiveMinute < next)
            {
                next = load.ActiveMinute;
            }
        }
        foreach (RequirementDefinition requirement in _scenario.Requirements)
        {
            if (requirement.DeadlineMinute > _currentMinute && requirement.DeadlineMinute < next)
            {
                next = requirement.DeadlineMinute;
            }
        }
        foreach (EventDefinition gameEvent in _events.Values)
        {
            if (gameEvent.StartMinute > _currentMinute && gameEvent.StartMinute < next)
            {
                next = gameEvent.StartMinute;
            }
            if (gameEvent.EndMinute > _currentMinute && gameEvent.EndMinute < next)
            {
                next = gameEvent.EndMinute;
            }
        }

        int? internalBoundary = GetNextInternalBoundary();
        if (internalBoundary > _currentMinute && internalBoundary < next)
        {
            next = internalBoundary.Value;
        }
        return next;
    }

    private int? GetNextInternalBoundary()
    {
        if (_hospitalInternalStage == InternalPowerStage.None ||
            _hospitalInternalRemainingKwMinute <= 0)
        {
            return null;
        }

        long stageEnergy = _hospitalInternalStage switch
        {
            InternalPowerStage.Ups =>
                _hospitalInternalRemainingKwMinute - _dieselCapacityKwMinute,
            InternalPowerStage.Diesel => _hospitalInternalRemainingKwMinute,
            _ => 0,
        };
        if (stageEnergy <= 0)
        {
            return null;
        }

        long ratedPower = _scenario.HospitalInternalPower.RatedPowerKw;
        if (stageEnergy % ratedPower != 0)
        {
            throw new FixtureValidationException(
                "Internal-power stage does not end on an integer minute.");
        }
        long duration = stageEnergy / ratedPower;
        long boundary = checked((long)_currentMinute + duration);
        if (boundary > int.MaxValue)
        {
            throw new FixtureValidationException("Internal-power boundary exceeds minute range.");
        }
        return (int)boundary;
    }

    private void SettleUntil(int targetMinute)
    {
        int elapsedMinutes = checked(targetMinute - _currentMinute);
        if (elapsedMinutes <= 0)
        {
            throw new FixtureValidationException("Settlement interval must be positive.");
        }

        SortedSet<string> activeLoads = GetActiveLoadIds(_currentMinute);
        IReadOnlyDictionary<string, string?> paths = ResolveUtilityPaths(
            _commissionedEdgeIds,
            _eventRemovedEdgeIds,
            activeLoads,
            _selectedCorridor);
        SettlementAccumulator delta = new(_loads.Keys);

        foreach (string loadId in activeLoads)
        {
            NodeDefinition loadNode = _nodes[loadId];
            long demandKw = loadNode.DemandKw
                ?? throw new FixtureValidationException($"Load '{loadId}' has no demand.");
            long energy = checked(demandKw * elapsedMinutes);
            if (paths[loadId] is not null)
            {
                delta.UtilityDeliveredKwMinuteByLoad[loadId] = energy;
                delta.GasInjectionKwMinute = checked(delta.GasInjectionKwMinute + energy);
            }
            else
            {
                delta.UtilityUnservedKwMinuteByLoad[loadId] = energy;
                if (string.Equals(loadId, _hospitalLoadId, StringComparison.Ordinal))
                {
                    long internalUsed = Math.Min(energy, _hospitalInternalRemainingKwMinute);
                    _hospitalInternalRemainingKwMinute = checked(
                        _hospitalInternalRemainingKwMinute - internalUsed);
                    delta.HospitalInternalUsedKwMinute = checked(
                        delta.HospitalInternalUsedKwMinute + internalUsed);
                    delta.HospitalP0UnservedKwMinute = checked(
                        delta.HospitalP0UnservedKwMinute + energy - internalUsed);
                }
            }
        }

        long deliveredEnergy = delta.UtilityDeliveredKwMinuteByLoad.Values.SumChecked();
        long unservedEnergy = delta.UtilityUnservedKwMinuteByLoad.Values.SumChecked();
        delta.RevenueCashUnit = CashForEnergy(deliveredEnergy, _scenario.Economy.SaleRate);
        delta.GasCostCashUnit = CashForEnergy(
            delta.GasInjectionKwMinute,
            _scenario.Economy.GasVariableRate);
        delta.LostSalesCashUnit = CashForEnergy(unservedEnergy, _scenario.Economy.SaleRate);

        foreach (string loadId in activeLoads)
        {
            long unserved = delta.UtilityUnservedKwMinuteByLoad[loadId];
            long compensation = CashForEnergy(unserved, _scenario.Economy.GetOutageRate(
                _loads[loadId].OutageRateKey));
            delta.CompensationCashUnit = checked(delta.CompensationCashUnit + compensation);
        }

        long cashDelta = checked(
            delta.RevenueCashUnit - delta.GasCostCashUnit - delta.CompensationCashUnit);
        _cash = checked(_cash + cashDelta);
        _interval.Add(delta);
        _cumulative.Add(delta);
    }

    private void ApplyBoundary()
    {
        foreach (RuntimeProject project in AllRuntimeProjects())
        {
            if (project.State == ProjectState.Building &&
                project.CompletionMinute == _currentMinute)
            {
                project.Commission();
                _commissionedEdgeIds.Add(project.Definition.EdgeId);
            }
        }

        foreach (RequirementDefinition requirement in _scenario.Requirements.Where(
                     requirement => requirement.DeadlineMinute == _currentMinute))
        {
            bool satisfied = requirement.SatisfiedByAnyCommissionedEdgeId.Any(
                _commissionedEdgeIds.Contains);
            if (!satisfied)
            {
                throw new FixtureValidationException(
                    $"Requirement '{requirement.Id}' is unsatisfied at its deadline.");
            }
        }

        RebuildEventRemovedEdges();
        SortedSet<string> activeLoads = GetActiveLoadIds(_currentMinute);
        IReadOnlyDictionary<string, string?> paths = ResolveUtilityPaths(
            _commissionedEdgeIds,
            _eventRemovedEdgeIds,
            activeLoads,
            _selectedCorridor);
        if (activeLoads.Contains(_hospitalLoadId) && paths[_hospitalLoadId] is null)
        {
            _hospitalInternalStage = _hospitalInternalRemainingKwMinute switch
            {
                > 0 when _hospitalInternalRemainingKwMinute > _dieselCapacityKwMinute =>
                    InternalPowerStage.Ups,
                > 0 => InternalPowerStage.Diesel,
                _ => InternalPowerStage.None,
            };
        }
        else
        {
            _hospitalInternalStage = InternalPowerStage.None;
        }
    }

    private void RebuildEventRemovedEdges()
    {
        _eventRemovedEdgeIds.Clear();
        foreach (EventDefinition gameEvent in _events.Values.Where(gameEvent =>
                     gameEvent.StartMinute <= _currentMinute && _currentMinute < gameEvent.EndMinute))
        {
            EvaluationCaseDefinition evaluationCase = _evaluationCases[gameEvent.EvaluationCaseId];
            foreach (string edgeId in _commissionedEdgeIds)
            {
                if (MatchesSelector(_edges[edgeId], evaluationCase))
                {
                    _eventRemovedEdgeIds.Add(edgeId);
                }
            }
        }
    }

    private bool IsUpsDepletionBoundary() =>
        _hospitalInternalStage == InternalPowerStage.Diesel &&
        _hospitalInternalRemainingKwMinute == _dieselCapacityKwMinute;

    private IReadOnlyDictionary<string, string?> ResolveUtilityPaths(
        IReadOnlySet<string> commissioned,
        IReadOnlySet<string> removed,
        IReadOnlySet<string> activeLoads,
        CorridorDesign? selectedCorridor)
    {
        SortedDictionary<string, string?> selectedPaths = new(StringComparer.Ordinal);
        foreach (string loadId in activeLoads.OrderBy(loadId => loadId, StringComparer.Ordinal))
        {
            SupplyPathDefinition primary = GetSinglePrimaryPath(loadId);
            SupplyPathDefinition? chosen = IsUsable(primary, commissioned, removed, selectedCorridor)
                ? primary
                : null;

            if (chosen is null && string.Equals(loadId, _hospitalLoadId, StringComparison.Ordinal) &&
                selectedCorridor.HasValue)
            {
                string requiredEdgeId = _corridorProjects[selectedCorridor.Value].Definition.EdgeId;
                SupplyPathDefinition[] candidates = _scenario.PermittedSupplyPaths
                    .Where(path => path.LoadNodeId == loadId && path.Role == PathRole.Backup)
                    .Where(path => string.Equals(
                        path.RequiredCommissionedEdgeId,
                        requiredEdgeId,
                        StringComparison.Ordinal))
                    .ToArray();
                if (candidates.Length != 1)
                {
                    throw new FixtureValidationException(
                        $"Selected backup path for '{requiredEdgeId}' is not unique.");
                }
                if (IsUsable(candidates[0], commissioned, removed, selectedCorridor))
                {
                    chosen = candidates[0];
                }
            }

            selectedPaths.Add(loadId, chosen?.Id);
        }

        ValidateCapacity(selectedPaths, commissioned, removed);
        return new ReadOnlyDictionary<string, string?>(selectedPaths);
    }

    private bool IsUsable(
        SupplyPathDefinition path,
        IReadOnlySet<string> commissioned,
        IReadOnlySet<string> removed,
        CorridorDesign? selectedCorridor)
    {
        if (path.Role == PathRole.Backup)
        {
            if (!selectedCorridor.HasValue)
            {
                return false;
            }
            string selectedEdgeId = _corridorProjects[selectedCorridor.Value].Definition.EdgeId;
            if (!string.Equals(
                path.RequiredCommissionedEdgeId,
                selectedEdgeId,
                StringComparison.Ordinal))
            {
                return false;
            }
        }

        return path.EdgeIds.All(edgeId =>
            commissioned.Contains(edgeId) && !removed.Contains(edgeId));
    }

    private void ValidateCapacity(
        IReadOnlyDictionary<string, string?> selectedPaths,
        IReadOnlySet<string> commissioned,
        IReadOnlySet<string> removed)
    {
        Dictionary<string, long> edgeDemand = new(StringComparer.Ordinal);
        Dictionary<string, long> generatorDemand = new(StringComparer.Ordinal);
        foreach ((string loadId, string? pathId) in selectedPaths)
        {
            if (pathId is null)
            {
                continue;
            }
            SupplyPathDefinition path = _scenario.PermittedSupplyPaths.Single(candidate =>
                string.Equals(candidate.Id, pathId, StringComparison.Ordinal));
            long demand = _nodes[loadId].DemandKw
                ?? throw new FixtureValidationException($"Load '{loadId}' has no demand.");
            foreach (string edgeId in path.EdgeIds)
            {
                if (!commissioned.Contains(edgeId) || removed.Contains(edgeId))
                {
                    throw new FixtureValidationException(
                        $"Selected path '{path.Id}' contains unusable edge '{edgeId}'.");
                }
                edgeDemand[edgeId] = checked(edgeDemand.GetValueOrDefault(edgeId) + demand);
            }

            EdgeDefinition firstEdge = _edges[path.EdgeIds[0]];
            generatorDemand[firstEdge.FromNodeId] = checked(
                generatorDemand.GetValueOrDefault(firstEdge.FromNodeId) + demand);
        }

        foreach ((string edgeId, long demand) in edgeDemand)
        {
            if (demand > _edges[edgeId].RatingKw)
            {
                throw new FixtureValidationException(
                    $"Edge '{edgeId}' allocation exceeds its rating.");
            }
        }
        foreach ((string generatorId, long demand) in generatorDemand)
        {
            NodeDefinition generator = _nodes[generatorId];
            if (generator.Kind != NodeKind.Generator || generator.InitialOnline != true ||
                !generator.MaxOutputKw.HasValue || demand > generator.MaxOutputKw.Value)
            {
                throw new FixtureValidationException(
                    $"Generator '{generatorId}' cannot supply authored path allocation.");
            }
        }
    }

    private SupplyPathDefinition GetSinglePrimaryPath(string loadId)
    {
        SupplyPathDefinition[] matches = _scenario.PermittedSupplyPaths
            .Where(path => path.LoadNodeId == loadId && path.Role == PathRole.Primary)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new FixtureValidationException(
                $"Load '{loadId}' does not have exactly one primary path.");
        }
        return matches[0];
    }

    private SortedSet<string> GetActiveLoadIds(int minute) => new(
        _loads.Values
            .Where(load => load.ActiveMinute <= minute)
            .Select(load => load.NodeId),
        StringComparer.Ordinal);

    private bool MatchesSelector(
        EdgeDefinition edge,
        EvaluationCaseDefinition evaluationCase) => evaluationCase.SelectorType switch
    {
        SelectorType.ElectricalContingencyId => string.Equals(
            edge.ElectricalContingencyId,
            evaluationCase.SelectorValue,
            StringComparison.Ordinal),
        SelectorType.SpatialRiskGroup => string.Equals(
            edge.SpatialRiskGroup,
            evaluationCase.SelectorValue,
            StringComparison.Ordinal),
        _ => throw new FixtureValidationException(
            $"Unsupported selector type in case '{evaluationCase.Id}'."),
    };

    private int GetCorridorOrderMinute()
    {
        int[] minutes = _corridorProjects.Values
            .Select(project => project.Definition.AllowedOrderMinute)
            .Distinct()
            .ToArray();
        if (minutes.Length != 1)
        {
            throw new FixtureValidationException("Corridor projects must share one order minute.");
        }
        return minutes[0];
    }

    private ProjectState GetCorridorProjectState() => _selectedCorridor.HasValue
        ? _corridorProjects[_selectedCorridor.Value].State
        : ProjectState.NotOrdered;

    private IEnumerable<RuntimeProject> AllRuntimeProjects()
    {
        yield return _townProject;
        foreach (RuntimeProject project in _corridorProjects
                     .OrderBy(pair => pair.Key)
                     .Select(pair => pair.Value))
        {
            yield return project;
        }
    }

    private CommandResult Accepted() =>
        new(true, null, GetSnapshot(), Array.Empty<BoundaryTrace>());

    private CommandResult Rejected(CommandErrorCode errorCode) =>
        new(false, errorCode, GetSnapshot(), Array.Empty<BoundaryTrace>());

    private static long CashForEnergy(long energyKwMinute, long rateCashUnitPerGWh)
    {
        long numerator = checked(energyKwMinute * rateCashUnitPerGWh);
        const long denominator = 60_000_000;
        if (numerator % denominator != 0)
        {
            throw new FixtureValidationException(
                $"Cash numerator {numerator} is not exactly divisible by {denominator}.");
        }
        return numerator / denominator;
    }

    private static string ToFixtureId(CorridorDesign design) => design switch
    {
        CorridorDesign.RiverParallel => "RIVER_PARALLEL",
        CorridorDesign.NorthDetour => "NORTH_DETOUR",
        _ => throw new ArgumentOutOfRangeException(nameof(design), design, null),
    };

    private static IReadOnlyDictionary<string, T> ToReadOnlyIndex<T>(
        IEnumerable<T> values,
        Func<T, string> keySelector,
        string label)
    {
        Dictionary<string, T> dictionary = new(StringComparer.Ordinal);
        foreach (T value in values)
        {
            string key = keySelector(value);
            if (!dictionary.TryAdd(key, value))
            {
                throw new FixtureValidationException($"Duplicate {label} '{key}'.");
            }
        }
        return new ReadOnlyDictionary<string, T>(dictionary);
    }

    private static IReadOnlyList<string> FreezeSorted(IEnumerable<string> values) =>
        Array.AsReadOnly(values.OrderBy(value => value, StringComparer.Ordinal).ToArray());

    private sealed class RuntimeProject
    {
        public RuntimeProject(ProjectDefinition definition)
        {
            Definition = definition;
        }

        public ProjectDefinition Definition { get; }
        public ProjectState State { get; private set; } = ProjectState.NotOrdered;
        public int? CompletionMinute { get; private set; }

        public void Order()
        {
            if (State != ProjectState.NotOrdered)
            {
                throw new InvalidOperationException("Project can only be ordered once.");
            }
            State = ProjectState.Building;
            CompletionMinute = checked(
                Definition.AllowedOrderMinute + Definition.BuildMinutes);
        }

        public void Commission()
        {
            if (State != ProjectState.Building)
            {
                throw new InvalidOperationException("Only a building project can commission.");
            }
            State = ProjectState.Commissioned;
        }
    }
}

internal sealed class SettlementAccumulator
{
    public SettlementAccumulator(IEnumerable<string> loadIds)
    {
        UtilityDeliveredKwMinuteByLoad = loadIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(loadId => loadId, StringComparer.Ordinal)
            .ToDictionary(loadId => loadId, _ => 0L, StringComparer.Ordinal);
        UtilityUnservedKwMinuteByLoad = loadIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(loadId => loadId, StringComparer.Ordinal)
            .ToDictionary(loadId => loadId, _ => 0L, StringComparer.Ordinal);
    }

    public long RevenueCashUnit { get; set; }
    public long GasCostCashUnit { get; set; }
    public long CompensationCashUnit { get; set; }
    public long LostSalesCashUnit { get; set; }
    public Dictionary<string, long> UtilityDeliveredKwMinuteByLoad { get; }
    public Dictionary<string, long> UtilityUnservedKwMinuteByLoad { get; }
    public long GasInjectionKwMinute { get; set; }
    public long HospitalInternalUsedKwMinute { get; set; }
    public long HospitalP0UnservedKwMinute { get; set; }

    public void Reset()
    {
        RevenueCashUnit = 0;
        GasCostCashUnit = 0;
        CompensationCashUnit = 0;
        LostSalesCashUnit = 0;
        GasInjectionKwMinute = 0;
        HospitalInternalUsedKwMinute = 0;
        HospitalP0UnservedKwMinute = 0;
        foreach (string loadId in UtilityDeliveredKwMinuteByLoad.Keys.ToArray())
        {
            UtilityDeliveredKwMinuteByLoad[loadId] = 0;
            UtilityUnservedKwMinuteByLoad[loadId] = 0;
        }
    }

    public void Add(SettlementAccumulator other)
    {
        RevenueCashUnit = checked(RevenueCashUnit + other.RevenueCashUnit);
        GasCostCashUnit = checked(GasCostCashUnit + other.GasCostCashUnit);
        CompensationCashUnit = checked(CompensationCashUnit + other.CompensationCashUnit);
        LostSalesCashUnit = checked(LostSalesCashUnit + other.LostSalesCashUnit);
        GasInjectionKwMinute = checked(GasInjectionKwMinute + other.GasInjectionKwMinute);
        HospitalInternalUsedKwMinute = checked(
            HospitalInternalUsedKwMinute + other.HospitalInternalUsedKwMinute);
        HospitalP0UnservedKwMinute = checked(
            HospitalP0UnservedKwMinute + other.HospitalP0UnservedKwMinute);
        foreach (string loadId in UtilityDeliveredKwMinuteByLoad.Keys)
        {
            UtilityDeliveredKwMinuteByLoad[loadId] = checked(
                UtilityDeliveredKwMinuteByLoad[loadId] +
                other.UtilityDeliveredKwMinuteByLoad[loadId]);
            UtilityUnservedKwMinuteByLoad[loadId] = checked(
                UtilityUnservedKwMinuteByLoad[loadId] +
                other.UtilityUnservedKwMinuteByLoad[loadId]);
        }
    }

    public Settlement Snapshot()
    {
        SortedDictionary<string, long> delivered = new(
            UtilityDeliveredKwMinuteByLoad,
            StringComparer.Ordinal);
        SortedDictionary<string, long> unserved = new(
            UtilityUnservedKwMinuteByLoad,
            StringComparer.Ordinal);
        return new Settlement(
            RevenueCashUnit,
            GasCostCashUnit,
            CompensationCashUnit,
            LostSalesCashUnit,
            new ReadOnlyDictionary<string, long>(delivered),
            new ReadOnlyDictionary<string, long>(unserved),
            GasInjectionKwMinute,
            HospitalInternalUsedKwMinute,
            HospitalP0UnservedKwMinute);
    }
}

internal static class CheckedEnumerableExtensions
{
    public static long SumChecked(this IEnumerable<long> values)
    {
        long sum = 0;
        foreach (long value in values)
        {
            sum = checked(sum + value);
        }
        return sum;
    }
}
