namespace Gridworks.Core;

internal static class FixtureValidator
{
    public static void Validate(
        ScenarioDefinition scenario,
        PresentationDefinition presentation,
        FixtureOracle oracle)
    {
        EnsureNonBlank(scenario.DisplayName, "displayName");
        EnsureNonBlank(scenario.Calendar.OriginLabel, "calendar.originLabel");
        Require(scenario.Calendar.MinutesPerDay > 0, "calendar.minutesPerDay must be positive.");
        ValidateUnits(scenario.Units);
        ValidateEconomy(scenario.Economy);

        Dictionary<string, NodeDefinition> nodes = Index(
            scenario.Nodes,
            node => node.Id,
            "node");
        Dictionary<string, EdgeDefinition> edges = Index(
            scenario.Edges,
            edge => edge.Id,
            "edge");
        Dictionary<string, ProjectDefinition> projects = Index(
            scenario.Projects,
            project => project.Id,
            "project");
        Dictionary<string, LoadDefinition> loads = Index(
            scenario.Loads,
            load => load.NodeId,
            "load declaration");
        Dictionary<string, RequirementDefinition> requirements = Index(
            scenario.Requirements,
            requirement => requirement.Id,
            "requirement");
        Dictionary<string, SupplyPathDefinition> paths = Index(
            scenario.PermittedSupplyPaths,
            path => path.Id,
            "permitted path");
        Dictionary<string, EvaluationCaseDefinition> cases = Index(
            scenario.EvaluationCases,
            evaluationCase => evaluationCase.Id,
            "evaluation case");
        Dictionary<string, EventDefinition> events = Index(
            scenario.Events,
            gameEvent => gameEvent.Id,
            "event");

        Require(nodes.Count > 0, "nodes cannot be empty.");
        Require(edges.Count > 0, "edges cannot be empty.");
        Require(projects.Count > 0, "projects cannot be empty.");
        Require(loads.Count > 0, "loads cannot be empty.");
        Require(paths.Count > 0, "permittedSupplyPaths cannot be empty.");
        Require(cases.Count > 0, "evaluationCases cannot be empty.");
        Require(events.Count > 0, "events cannot be empty.");
        _ = requirements;

        ValidateNodes(nodes);
        ValidateEdges(edges, nodes);
        ValidateProjects(projects, edges);
        ValidateLoads(loads, nodes, scenario.Economy);
        ValidateRequirements(scenario.Requirements, edges);
        ValidatePaths(scenario.PermittedSupplyPaths, paths, loads, nodes, edges);
        ValidateCasesAndEvents(cases, events, scenario.Milestones);
        ValidateInternalPower(scenario.HospitalInternalPower, nodes, loads);
        ValidateMilestones(scenario);
        ValidatePresentation(presentation, nodes, edges, projects);
        ValidateOracle(oracle, scenario, edges, loads, paths, cases);
    }

    private static void ValidateUnits(UnitsDefinition units)
    {
        EnsureNonBlank(units.Position, "units.position");
        EnsureNonBlank(units.Power, "units.power");
        EnsureNonBlank(units.Energy, "units.energy");
        EnsureNonBlank(units.Time, "units.time");
        EnsureNonBlank(units.Cash, "units.cash");
        EnsureNonBlank(units.Rate, "units.rate");
    }

    private static void ValidateEconomy(EconomyDefinition economy)
    {
        Require(economy.InitialCash >= 0, "economy.initialCash cannot be negative.");
        Require(economy.SaleRate >= 0, "economy.saleRate cannot be negative.");
        Require(economy.GasVariableRate >= 0, "economy.gasVariableRate cannot be negative.");
        Require(economy.TownOutageRate >= 0, "economy.townOutageRate cannot be negative.");
        Require(economy.HospitalOutageRate >= 0, "economy.hospitalOutageRate cannot be negative.");
    }

    private static void ValidateNodes(IReadOnlyDictionary<string, NodeDefinition> nodes)
    {
        foreach (NodeDefinition node in nodes.Values)
        {
            switch (node.Kind)
            {
                case NodeKind.Generator:
                    Require(node.MaxOutputKw > 0, $"Generator '{node.Id}' requires positive maxOutputKw.");
                    Require(node.InitialOnline.HasValue, $"Generator '{node.Id}' requires initialOnline.");
                    RequireNoLoadFields(node);
                    Require(node.InitialCommissioned is null,
                        $"Generator '{node.Id}' cannot declare initialCommissioned.");
                    break;
                case NodeKind.Bus:
                    RequireNoElectricalNodeFields(node);
                    break;
                case NodeKind.Substation:
                    Require(node.InitialCommissioned.HasValue,
                        $"Substation '{node.Id}' requires initialCommissioned.");
                    Require(node.MaxOutputKw is null && node.InitialOnline is null,
                        $"Substation '{node.Id}' contains generator-only fields.");
                    RequireNoLoadFields(node);
                    break;
                case NodeKind.Load:
                    Require(node.DemandKw > 0, $"Load '{node.Id}' requires positive demandKw.");
                    Require(node.Priority.HasValue, $"Load '{node.Id}' requires priority.");
                    EnsureNonBlank(node.ServiceSubstationId, $"load '{node.Id}' serviceSubstationId");
                    Require(node.MaxOutputKw is null && node.InitialOnline is null &&
                        node.InitialCommissioned is null,
                        $"Load '{node.Id}' contains non-load fields.");
                    break;
                default:
                    throw new FixtureValidationException($"Unsupported node kind for '{node.Id}'.");
            }
        }

        foreach (NodeDefinition load in nodes.Values.Where(node => node.Kind == NodeKind.Load))
        {
            Require(nodes.TryGetValue(load.ServiceSubstationId!, out NodeDefinition? substation),
                $"Load '{load.Id}' references missing service substation '{load.ServiceSubstationId}'.");
            if (substation is null)
            {
                throw new FixtureValidationException(
                    $"Load '{load.Id}' references a null service substation.");
            }
            Require(substation.Kind == NodeKind.Substation,
                $"Load '{load.Id}' serviceSubstationId is not a substation.");
        }
    }

    private static void RequireNoElectricalNodeFields(NodeDefinition node)
    {
        Require(node.MaxOutputKw is null && node.InitialOnline is null &&
            node.InitialCommissioned is null && node.DemandKw is null &&
            node.Priority is null && node.ServiceSubstationId is null,
            $"Bus '{node.Id}' contains kind-specific fields.");
    }

    private static void RequireNoLoadFields(NodeDefinition node)
    {
        Require(node.DemandKw is null && node.Priority is null && node.ServiceSubstationId is null,
            $"Node '{node.Id}' contains load-only fields.");
    }

    private static void ValidateEdges(
        IReadOnlyDictionary<string, EdgeDefinition> edges,
        IReadOnlyDictionary<string, NodeDefinition> nodes)
    {
        foreach (EdgeDefinition edge in edges.Values)
        {
            Require(nodes.ContainsKey(edge.FromNodeId),
                $"Edge '{edge.Id}' references missing fromNodeId '{edge.FromNodeId}'.");
            Require(nodes.ContainsKey(edge.ToNodeId),
                $"Edge '{edge.Id}' references missing toNodeId '{edge.ToNodeId}'.");
            Require(!string.Equals(edge.FromNodeId, edge.ToNodeId, StringComparison.Ordinal),
                $"Edge '{edge.Id}' cannot be a self-loop.");
            Require(edge.RatingKw > 0, $"Edge '{edge.Id}' ratingKw must be positive.");
            EnsureNonBlank(edge.ElectricalContingencyId,
                $"edge '{edge.Id}' electricalContingencyId");
            EnsureNonBlank(edge.SpatialRiskGroup, $"edge '{edge.Id}' spatialRiskGroup");
        }
    }

    private static void ValidateProjects(
        IReadOnlyDictionary<string, ProjectDefinition> projects,
        IReadOnlyDictionary<string, EdgeDefinition> edges)
    {
        HashSet<string> projectEdges = new(StringComparer.Ordinal);
        foreach (ProjectDefinition project in projects.Values)
        {
            Require(edges.TryGetValue(project.EdgeId, out EdgeDefinition? edge),
                $"Project '{project.Id}' references missing edge '{project.EdgeId}'.");
            if (edge is null)
            {
                throw new FixtureValidationException(
                    $"Project '{project.Id}' references a null edge.");
            }
            Require(projectEdges.Add(project.EdgeId),
                $"Multiple projects target edge '{project.EdgeId}'.");
            Require(edge.InitialConstructionState == ConstructionState.NotOrdered,
                $"Project '{project.Id}' targets an initially commissioned edge.");
            Require(project.CostCashUnit >= 0,
                $"Project '{project.Id}' costCashUnit cannot be negative.");
            Require(project.AllowedOrderMinute >= 0,
                $"Project '{project.Id}' allowedOrderMinute cannot be negative.");
            Require(project.BuildMinutes > 0,
                $"Project '{project.Id}' buildMinutes must be positive.");
            _ = checked(project.AllowedOrderMinute + project.BuildMinutes);
        }

        foreach (EdgeDefinition edge in edges.Values.Where(edge =>
                     edge.InitialConstructionState == ConstructionState.NotOrdered))
        {
            Require(projectEdges.Contains(edge.Id),
                $"Initially not-ordered edge '{edge.Id}' has no project.");
        }
    }

    private static void ValidateLoads(
        IReadOnlyDictionary<string, LoadDefinition> loads,
        IReadOnlyDictionary<string, NodeDefinition> nodes,
        EconomyDefinition economy)
    {
        foreach (LoadDefinition load in loads.Values)
        {
            Require(nodes.TryGetValue(load.NodeId, out NodeDefinition? node),
                $"Load declaration references missing node '{load.NodeId}'.");
            if (node is null)
            {
                throw new FixtureValidationException(
                    $"Load declaration '{load.NodeId}' references a null node.");
            }
            Require(node.Kind == NodeKind.Load,
                $"Load declaration '{load.NodeId}' does not reference a load node.");
            Require(load.ActiveMinute >= 0,
                $"Load '{load.NodeId}' activeMinute cannot be negative.");
            if (load.NoticeMinute.HasValue)
            {
                Require(load.NoticeMinute.Value >= 0 && load.NoticeMinute.Value <= load.ActiveMinute,
                    $"Load '{load.NodeId}' has invalid noticeMinute.");
            }

            _ = economy.GetOutageRate(load.OutageRateKey);
        }

        foreach (NodeDefinition node in nodes.Values.Where(node => node.Kind == NodeKind.Load))
        {
            Require(loads.ContainsKey(node.Id),
                $"Load node '{node.Id}' has no load declaration.");
        }
    }

    private static void ValidateRequirements(
        IEnumerable<RequirementDefinition> requirements,
        IReadOnlyDictionary<string, EdgeDefinition> edges)
    {
        foreach (RequirementDefinition requirement in requirements)
        {
            Require(requirement.DeadlineMinute >= 0,
                $"Requirement '{requirement.Id}' deadline cannot be negative.");
            Require(requirement.SatisfiedByAnyCommissionedEdgeId.Count > 0,
                $"Requirement '{requirement.Id}' has no satisfying edge.");
            EnsureUnique(requirement.SatisfiedByAnyCommissionedEdgeId,
                $"requirement '{requirement.Id}' edge");
            foreach (string edgeId in requirement.SatisfiedByAnyCommissionedEdgeId)
            {
                Require(edges.ContainsKey(edgeId),
                    $"Requirement '{requirement.Id}' references missing edge '{edgeId}'.");
            }
        }
    }

    private static void ValidatePaths(
        IEnumerable<SupplyPathDefinition> pathDefinitions,
        IReadOnlyDictionary<string, SupplyPathDefinition> paths,
        IReadOnlyDictionary<string, LoadDefinition> loads,
        IReadOnlyDictionary<string, NodeDefinition> nodes,
        IReadOnlyDictionary<string, EdgeDefinition> edges)
    {
        foreach (SupplyPathDefinition path in pathDefinitions)
        {
            Require(loads.ContainsKey(path.LoadNodeId),
                $"Path '{path.Id}' references undeclared load '{path.LoadNodeId}'.");
            Require(path.EdgeIds.Count > 0, $"Path '{path.Id}' has no edges.");
            EnsureUnique(path.EdgeIds, $"path '{path.Id}' edge");

            string? previousTo = null;
            for (int index = 0; index < path.EdgeIds.Count; index++)
            {
                string edgeId = path.EdgeIds[index];
                Require(edges.TryGetValue(edgeId, out EdgeDefinition? edge),
                    $"Path '{path.Id}' references missing edge '{edgeId}'.");
                if (edge is null)
                {
                    throw new FixtureValidationException(
                        $"Path '{path.Id}' references a null edge.");
                }
                if (index == 0)
                {
                    NodeDefinition source = nodes[edge.FromNodeId];
                    Require(source.Kind == NodeKind.Generator && source.InitialOnline == true,
                        $"Path '{path.Id}' does not start at an online generator.");
                }
                else
                {
                    Require(string.Equals(previousTo, edge.FromNodeId, StringComparison.Ordinal),
                        $"Path '{path.Id}' is discontinuous before edge '{edge.Id}'.");
                }

                previousTo = edge.ToNodeId;
            }

            Require(string.Equals(previousTo, path.LoadNodeId, StringComparison.Ordinal),
                $"Path '{path.Id}' does not end at load '{path.LoadNodeId}'.");

            if (path.Role == PathRole.Primary)
            {
                Require(path.RequiredCommissionedEdgeId is null,
                    $"Primary path '{path.Id}' cannot require a selected edge.");
            }
            else
            {
                EnsureNonBlank(path.RequiredCommissionedEdgeId,
                    $"backup path '{path.Id}' requiredCommissionedEdgeId");
                Require(edges.ContainsKey(path.RequiredCommissionedEdgeId!),
                    $"Backup path '{path.Id}' requires missing edge '{path.RequiredCommissionedEdgeId}'.");
                Require(path.EdgeIds.Contains(path.RequiredCommissionedEdgeId!, StringComparer.Ordinal),
                    $"Backup path '{path.Id}' does not contain its required edge.");
            }
        }

        foreach (string loadId in loads.Keys)
        {
            int primaryCount = paths.Values.Count(path =>
                path.LoadNodeId == loadId && path.Role == PathRole.Primary);
            Require(primaryCount == 1,
                $"Load '{loadId}' must have exactly one primary path.");
        }
    }

    private static void ValidateCasesAndEvents(
        IReadOnlyDictionary<string, EvaluationCaseDefinition> cases,
        IReadOnlyDictionary<string, EventDefinition> events,
        IReadOnlyList<MilestoneDefinition> milestones)
    {
        foreach (EvaluationCaseDefinition evaluationCase in cases.Values)
        {
            EnsureNonBlank(evaluationCase.SelectorValue,
                $"evaluation case '{evaluationCase.Id}' selectorValue");
        }

        HashSet<int> milestoneMinutes = milestones.Select(milestone => milestone.Minute).ToHashSet();
        foreach (EventDefinition gameEvent in events.Values)
        {
            Require(cases.ContainsKey(gameEvent.EvaluationCaseId),
                $"Event '{gameEvent.Id}' references missing case '{gameEvent.EvaluationCaseId}'.");
            Require(gameEvent.StartMinute >= 0 && gameEvent.EndMinute > gameEvent.StartMinute,
                $"Event '{gameEvent.Id}' has an invalid interval.");
            Require(milestoneMinutes.Contains(gameEvent.StartMinute) &&
                milestoneMinutes.Contains(gameEvent.EndMinute),
                $"Event '{gameEvent.Id}' boundaries must be public milestones.");
        }
    }

    private static void ValidateInternalPower(
        HospitalInternalPowerDefinition internalPower,
        IReadOnlyDictionary<string, NodeDefinition> nodes,
        IReadOnlyDictionary<string, LoadDefinition> loads)
    {
        Require(nodes.TryGetValue(internalPower.LoadNodeId, out NodeDefinition? loadNode),
            $"hospitalInternalPower references missing load '{internalPower.LoadNodeId}'.");
        if (loadNode is null)
        {
            throw new FixtureValidationException(
                "hospitalInternalPower references a null load node.");
        }
        Require(loads.ContainsKey(internalPower.LoadNodeId) && loadNode.Kind == NodeKind.Load &&
            loadNode.Priority == LoadPriority.P0,
            "hospitalInternalPower must reference the declared P0 load.");
        Require(internalPower.RatedPowerKw > 0 &&
            internalPower.RatedPowerKw == loadNode.DemandKw,
            "hospitalInternalPower ratedPowerKw must equal P0 demand.");
        Require(internalPower.Stages.Count == 2,
            "hospitalInternalPower must contain exactly UPS and DIESEL stages.");
        Require(internalPower.Stages[0].Id == "UPS" && internalPower.Stages[1].Id == "DIESEL",
            "hospitalInternalPower stages must be ordered UPS then DIESEL.");
        EnsureUnique(internalPower.Stages.Select(stage => stage.Id), "internal-power stage");
        foreach (InternalPowerStageDefinition stage in internalPower.Stages)
        {
            Require(stage.EnergyKwMinute > 0,
                $"Internal-power stage '{stage.Id}' energy must be positive.");
            Require(stage.EnergyKwMinute % internalPower.RatedPowerKw == 0,
                $"Internal-power stage '{stage.Id}' must end on an integer minute.");
        }
    }

    private static void ValidateMilestones(ScenarioDefinition scenario)
    {
        Require(scenario.Milestones.Count > 1, "At least two milestones are required.");
        Require(scenario.Milestones[0].Minute == 0, "First milestone must be minute 0.");
        int previous = -1;
        HashSet<int> minutes = new();
        foreach (MilestoneDefinition milestone in scenario.Milestones)
        {
            Require(milestone.Minute > previous, "Milestones must be strictly increasing.");
            Require(minutes.Add(milestone.Minute), "Milestone minutes must be unique.");
            EnsureNonBlank(milestone.Label, $"milestone {milestone.Minute} label");
            previous = milestone.Minute;
        }

        foreach (ProjectDefinition project in scenario.Projects)
        {
            Require(minutes.Contains(project.AllowedOrderMinute),
                $"Project '{project.Id}' order minute is not a milestone.");
            int completion = checked(project.AllowedOrderMinute + project.BuildMinutes);
            Require(minutes.Contains(completion),
                $"Project '{project.Id}' completion minute is not a milestone.");
        }

        foreach (LoadDefinition load in scenario.Loads)
        {
            Require(minutes.Contains(load.ActiveMinute),
                $"Load '{load.NodeId}' activation is not a milestone.");
        }

        foreach (RequirementDefinition requirement in scenario.Requirements)
        {
            Require(minutes.Contains(requirement.DeadlineMinute),
                $"Requirement '{requirement.Id}' deadline is not a milestone.");
        }
    }

    private static void ValidatePresentation(
        PresentationDefinition presentation,
        IReadOnlyDictionary<string, NodeDefinition> nodes,
        IReadOnlyDictionary<string, EdgeDefinition> edges,
        IReadOnlyDictionary<string, ProjectDefinition> projects)
    {
        Require(presentation.MapBounds.Width > 0 && presentation.MapBounds.Height > 0,
            "presentation.mapBounds must be positive.");
        foreach (NodeDefinition node in nodes.Values)
        {
            Require(IsInside(node.Position, presentation.MapBounds),
                $"Node '{node.Id}' lies outside mapBounds.");
        }

        EnsureUnique(presentation.ServiceAreas.Select(area => area.SubstationId),
            "presentation service-area substation");
        foreach (ServiceAreaDefinition area in presentation.ServiceAreas)
        {
            Require(nodes.TryGetValue(area.SubstationId, out NodeDefinition? node) &&
                node.Kind == NodeKind.Substation,
                $"Service area references missing/non-substation '{area.SubstationId}'.");
            Require(area.Shape == "ellipse", "Only the authored ellipse service shape is supported.");
            Require(area.RadiusX > 0 && area.RadiusY > 0,
                $"Service area '{area.SubstationId}' radii must be positive.");
            Require(IsInside(area.Center, presentation.MapBounds),
                $"Service area '{area.SubstationId}' center lies outside mapBounds.");
        }

        Dictionary<string, RiskAreaDefinition> riskAreas = Index(
            presentation.RiskAreas,
            area => area.Id,
            "presentation risk area");
        _ = riskAreas;
        HashSet<string> spatialGroups = edges.Values
            .Select(edge => edge.SpatialRiskGroup)
            .ToHashSet(StringComparer.Ordinal);
        foreach (RiskAreaDefinition area in presentation.RiskAreas)
        {
            Require(spatialGroups.Contains(area.SpatialRiskGroup),
                $"Risk area '{area.Id}' references unknown spatial group '{area.SpatialRiskGroup}'.");
            Require(area.Polygon.Count >= 3, $"Risk area '{area.Id}' polygon needs at least 3 points.");
            Require(area.Polygon.All(point => IsInside(point, presentation.MapBounds)),
                $"Risk area '{area.Id}' polygon lies outside mapBounds.");
        }

        Dictionary<string, EdgePolylineDefinition> polylines = Index(
            presentation.EdgePolylines,
            polyline => polyline.EdgeId,
            "presentation edge polyline");
        Require(polylines.Count == edges.Count,
            "presentation.edgePolylines must cover every edge exactly once.");
        foreach (EdgeDefinition edge in edges.Values)
        {
            Require(polylines.TryGetValue(edge.Id, out EdgePolylineDefinition? polyline),
                $"Missing presentation polyline for edge '{edge.Id}'.");
            if (polyline is null)
            {
                throw new FixtureValidationException(
                    $"Presentation polyline for edge '{edge.Id}' is null.");
            }
            Require(polyline.Points.Count >= 2,
                $"Polyline '{edge.Id}' needs at least two points.");
            Require(polyline.Points[0] == nodes[edge.FromNodeId].Position &&
                polyline.Points[^1] == nodes[edge.ToNodeId].Position,
                $"Polyline '{edge.Id}' endpoints do not match edge endpoints.");
            Require(polyline.Points.All(point => IsInside(point, presentation.MapBounds)),
                $"Polyline '{edge.Id}' lies outside mapBounds.");
        }

        Dictionary<string, LayoutVariantDefinition> variants = Index(
            presentation.LayoutVariants,
            variant => variant.Id,
            "presentation layout variant");
        Require(variants.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(new[] { "ab", "ba" }),
            "presentation.layoutVariants must contain exactly ab and ba.");
        foreach (LayoutVariantDefinition variant in variants.Values)
        {
            EnsureUnique(variant.CorridorProjectOrder,
                $"layout variant '{variant.Id}' project");
            Require(variant.CorridorProjectOrder.Count == 2,
                $"Layout variant '{variant.Id}' must list two corridor projects.");
            Require(variant.CorridorProjectOrder.All(projects.ContainsKey),
                $"Layout variant '{variant.Id}' references a missing project.");
        }
        Require(variants["ab"].CorridorProjectOrder.SequenceEqual(
                    variants["ba"].CorridorProjectOrder.Reverse(), StringComparer.Ordinal),
            "ab and ba corridor orders must be exact reversals.");
    }

    private static void ValidateOracle(
        FixtureOracle oracle,
        ScenarioDefinition scenario,
        IReadOnlyDictionary<string, EdgeDefinition> edges,
        IReadOnlyDictionary<string, LoadDefinition> loads,
        IReadOnlyDictionary<string, SupplyPathDefinition> paths,
        IReadOnlyDictionary<string, EvaluationCaseDefinition> cases)
    {
        Require(oracle.Topology.NodeCount == scenario.Nodes.Count,
            "Oracle topology nodeCount does not match scenario.");
        Require(oracle.Topology.EdgeCount == scenario.Edges.Count,
            "Oracle topology edgeCount does not match scenario.");

        HashSet<string> outcomePairs = new(StringComparer.Ordinal);
        foreach (EvaluationOutcomeOracle outcome in oracle.EvaluationOutcomes)
        {
            Require(cases.ContainsKey(outcome.CaseId),
                $"Oracle outcome references missing case '{outcome.CaseId}'.");
            Require(outcomePairs.Add($"{outcome.Design}:{outcome.CaseId}"),
                $"Duplicate oracle outcome '{outcome.Design}/{outcome.CaseId}'.");
            EnsureUnique(outcome.RemovedEdgeIds, "oracle outcome removed edge");
            Require(outcome.RemovedEdgeIds.All(edges.ContainsKey),
                "Oracle outcome references a missing removed edge.");
            ValidateOptionalPath(outcome.TownPathId, paths, "oracle townPathId");
            ValidateOptionalPath(outcome.HospitalPathId, paths, "oracle hospitalPathId");
        }
        Require(outcomePairs.Count == Enum.GetValues<EvaluationDesign>().Length * cases.Count,
            "Oracle evaluation outcomes do not cover every design/case pair.");

        EnsureUnique(oracle.CommonBoundaryStates.Select(state => state.Id),
            "common boundary oracle");
        foreach (BoundaryOracle boundary in oracle.CommonBoundaryStates)
        {
            ValidateOracleSnapshot(boundary.Snapshot, edges, loads, paths, scenario);
        }

        EnsureUnique(oracle.RouteBoundaryStates.Select(route => route.Design.ToString()),
            "route boundary design");
        Require(oracle.RouteBoundaryStates.Count == Enum.GetValues<CorridorDesign>().Length,
            "Route boundary oracle must cover both corridor designs.");
        foreach (RouteBoundaryOracle route in oracle.RouteBoundaryStates)
        {
            EnsureUnique(route.States.Select(state => state.Id),
                $"route '{route.Design}' boundary");
            foreach (BoundaryOracle boundary in route.States)
            {
                Require(boundary.Snapshot.SelectedCorridor == route.Design,
                    $"Boundary '{boundary.Id}' selected corridor does not match route.");
                ValidateOracleSnapshot(boundary.Snapshot, edges, loads, paths, scenario);
            }
        }
    }

    private static void ValidateOracleSnapshot(
        PublicSnapshot snapshot,
        IReadOnlyDictionary<string, EdgeDefinition> edges,
        IReadOnlyDictionary<string, LoadDefinition> loads,
        IReadOnlyDictionary<string, SupplyPathDefinition> paths,
        ScenarioDefinition scenario)
    {
        Require(snapshot.Minute >= 0, "Oracle snapshot minute cannot be negative.");
        EnsureUnique(snapshot.CommissionedEdgeIds, "oracle commissioned edge");
        EnsureUnique(snapshot.EventRemovedEdgeIds, "oracle event-removed edge");
        EnsureUnique(snapshot.ActiveLoadIds, "oracle active load");
        Require(snapshot.CommissionedEdgeIds.All(edges.ContainsKey),
            "Oracle snapshot references missing commissioned edge.");
        Require(snapshot.EventRemovedEdgeIds.All(snapshot.CommissionedEdgeIds.Contains),
            "Oracle eventRemovedEdgeIds must be a commissioned-edge subset.");
        Require(snapshot.ActiveLoadIds.All(loads.ContainsKey),
            "Oracle snapshot references missing active load.");
        Require(snapshot.UtilityPathByLoad.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(snapshot.ActiveLoadIds),
            "Oracle utilityPathByLoad keys must equal activeLoadIds.");
        foreach ((string loadId, string? pathId) in snapshot.UtilityPathByLoad)
        {
            if (pathId is null)
            {
                continue;
            }
            Require(paths.TryGetValue(pathId, out SupplyPathDefinition? path) &&
                path.LoadNodeId == loadId,
                $"Oracle load '{loadId}' references invalid path '{pathId}'.");
        }
        Require(snapshot.HospitalInternalRemainingKwMinute >= 0,
            "Oracle hospital internal energy cannot be negative.");
        ValidateSettlement(snapshot.Interval, scenario.Loads);
        ValidateSettlement(snapshot.Cumulative, scenario.Loads);
    }

    private static void ValidateSettlement(
        Settlement settlement,
        IReadOnlyList<LoadDefinition> loads)
    {
        HashSet<string> loadIds = loads.Select(load => load.NodeId).ToHashSet(StringComparer.Ordinal);
        Require(settlement.UtilityDeliveredKwMinuteByLoad.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(loadIds),
            "Settlement delivered-energy keys must cover every load.");
        Require(settlement.UtilityUnservedKwMinuteByLoad.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(loadIds),
            "Settlement unserved-energy keys must cover every load.");
        Require(settlement.RevenueCashUnit >= 0 && settlement.GasCostCashUnit >= 0 &&
            settlement.CompensationCashUnit >= 0 && settlement.LostSalesCashUnit >= 0 &&
            settlement.GasInjectionKwMinute >= 0 &&
            settlement.HospitalInternalUsedKwMinute >= 0 &&
            settlement.HospitalP0UnservedKwMinute >= 0 &&
            settlement.UtilityDeliveredKwMinuteByLoad.Values.All(value => value >= 0) &&
            settlement.UtilityUnservedKwMinuteByLoad.Values.All(value => value >= 0),
            "Settlement values cannot be negative.");
    }

    private static void ValidateOptionalPath(
        string? pathId,
        IReadOnlyDictionary<string, SupplyPathDefinition> paths,
        string label)
    {
        if (pathId is not null)
        {
            Require(paths.ContainsKey(pathId), $"{label} references missing path '{pathId}'.");
        }
    }

    private static bool IsInside(Position position, MapBoundsDefinition bounds) =>
        position.X >= 0 && position.X <= bounds.Width &&
        position.Y >= 0 && position.Y <= bounds.Height;

    private static Dictionary<string, T> Index<T>(
        IEnumerable<T> values,
        Func<T, string> keySelector,
        string label)
    {
        Dictionary<string, T> result = new(StringComparer.Ordinal);
        foreach (T value in values)
        {
            string key = keySelector(value);
            EnsureNonBlank(key, $"{label} id");
            if (!result.TryAdd(key, value))
            {
                throw new FixtureValidationException($"Duplicate {label} id '{key}'.");
            }
        }

        return result;
    }

    private static void EnsureUnique(IEnumerable<string> values, string label)
    {
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (string value in values)
        {
            EnsureNonBlank(value, label);
            Require(unique.Add(value), $"Duplicate {label} '{value}'.");
        }
    }

    private static void EnsureNonBlank(string? value, string label)
    {
        Require(!string.IsNullOrWhiteSpace(value), $"{label} cannot be blank.");
        Require(string.Equals(value, value!.Trim(), StringComparison.Ordinal),
            $"{label} cannot have surrounding whitespace.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new FixtureValidationException(message);
        }
    }
}
