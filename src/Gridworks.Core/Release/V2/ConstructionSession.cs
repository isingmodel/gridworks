namespace Gridworks.Core.Release.V2;

public sealed class ConstructionSession
{
    private SpatialWorldDefinition _world;
    private long _minute;
    private ConstructionPhase _phase = ConstructionPhase.Ready;
    private NodeDraftSnapshot? _nodeDraft;
    private LineDraftSnapshot? _lineDraft;
    private ActiveConstructionSnapshot? _activeConstruction;

    public ConstructionSession(SpatialWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(world);
        SpatialWorldLoader.Validate(world);
        _world = world with { };
    }

    public ConstructionSnapshot GetSnapshot() => new(
        _minute,
        _phase,
        _world,
        _nodeDraft,
        _lineDraft,
        _activeConstruction);

    public NodePlacementPreview PreviewNodePlacement(
        string nodeClassId,
        MapPoint position)
    {
        if (_phase is not (ConstructionPhase.Ready or ConstructionPhase.NodeDrafting))
        {
            return new NodePlacementPreview(
                false,
                ConstructionError.WrongPhase,
                nodeClassId,
                position);
        }
        return PlacementValidator.PreviewNodePlacement(_world, nodeClassId, position);
    }

    public ConstructionCommandResult SetNodeDraft(
        string nodeClassId,
        MapPoint position)
    {
        NodePlacementPreview preview = PreviewNodePlacement(nodeClassId, position);
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }
        _nodeDraft = new NodeDraftSnapshot(nodeClassId, position)
        {
            RiskAreaIds = preview.RiskAreaIds,
        };
        _phase = ConstructionPhase.NodeDrafting;
        return Accepted();
    }

    public ConstructionCommandResult CancelNodeDraft()
    {
        if (_phase != ConstructionPhase.NodeDrafting)
        {
            return Rejected(ConstructionError.WrongPhase);
        }
        _nodeDraft = null;
        _phase = ConstructionPhase.Ready;
        return Accepted();
    }

    public ConstructionQuote PreviewNodeOrder()
    {
        if (_phase != ConstructionPhase.NodeDrafting || _nodeDraft is null)
        {
            return QuoteRejected(ConstructionError.WrongPhase);
        }
        NodePlacementPreview preview = PlacementValidator.PreviewNodePlacement(
            _world,
            _nodeDraft.NodeClassId,
            _nodeDraft.Position);
        if (!preview.Accepted)
        {
            return QuoteRejected(preview.Error!.Value);
        }
        SpatialNodeClassDefinition nodeClass = NodeClass(_nodeDraft.NodeClassId);
        try
        {
            long completion = checked(_minute + nodeClass.BuildMinutes);
            return new ConstructionQuote(
                true,
                null,
                nodeClass.CostCashUnit,
                nodeClass.BuildMinutes,
                completion)
            {
                RiskAreaIds = preview.RiskAreaIds,
            };
        }
        catch (OverflowException)
        {
            return QuoteRejected(ConstructionError.ArithmeticOverflow);
        }
    }

    public ConstructionCommandResult OrderNode()
    {
        ConstructionQuote quote = PreviewNodeOrder();
        if (!quote.Accepted)
        {
            return Rejected(quote.Error!.Value);
        }

        NodeDraftSnapshot draft = _nodeDraft!;
        SpatialNodeClassDefinition nodeClass = NodeClass(draft.NodeClassId);
        (string nodeId, int ordinal) = NextAvailableId(
            "PLAYER_SUBSTATION",
            _world.Nodes.Select(item => item.NodeId));
        var node = new SpatialNodeDefinition(
            nodeId,
            draft.NodeClassId,
            $"{nodeClass.DisplayName} {ordinal}",
            draft.Position,
            false,
            false);
        SpatialWorldDefinition candidate = _world with
        {
            Nodes = _world.Nodes.Append(node).ToArray(),
        };

        _world = candidate;
        _nodeDraft = null;
        _activeConstruction = new ActiveConstructionSnapshot(
            ConstructionKind.Node,
            quote.CostCashUnit!.Value,
            quote.CompletionMinute!.Value,
            [nodeId],
            Array.Empty<string>())
        {
            RiskAreaIds = quote.RiskAreaIds,
        };
        _phase = ConstructionPhase.NodeBuilding;
        return Accepted();
    }

    public LineStartPreview PreviewLineStart(
        string startNodeId,
        string lineClassId,
        string poleClassId)
    {
        if (_phase != ConstructionPhase.Ready)
        {
            return new LineStartPreview(
                false,
                ConstructionError.WrongPhase,
                startNodeId,
                lineClassId,
                poleClassId);
        }
        return PlacementValidator.PreviewLineStart(
            _world,
            startNodeId,
            lineClassId,
            poleClassId);
    }

    public ConstructionCommandResult StartLineDraft(
        string startNodeId,
        string lineClassId,
        string poleClassId)
    {
        LineStartPreview preview = PreviewLineStart(startNodeId, lineClassId, poleClassId);
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }
        _lineDraft = new LineDraftSnapshot(
            startNodeId,
            lineClassId,
            poleClassId,
            Array.Empty<MapPoint>(),
            null)
        {
            RiskAreaIds = Array.Empty<string>(),
        };
        _phase = ConstructionPhase.LineDrafting;
        return Accepted();
    }

    public LinePointPreview PreviewLinePoint(MapPoint position)
    {
        if (_phase != ConstructionPhase.LineDrafting || _lineDraft is null)
        {
            return new LinePointPreview(
                false,
                ConstructionError.WrongPhase,
                position,
                null,
                null);
        }
        return PlacementValidator.PreviewLinePoint(_world, _lineDraft, position);
    }

    public ConstructionCommandResult AddLinePoint(MapPoint position)
    {
        LinePointPreview preview = PreviewLinePoint(position);
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }
        _lineDraft = _lineDraft! with
        {
            IntermediatePoints = _lineDraft.IntermediatePoints.Append(position).ToArray(),
        };
        RefreshLineDraftRiskAreas();
        return Accepted();
    }

    public LinePointMovePreview PreviewMoveLinePoint(int pointIndex, MapPoint position)
    {
        if (_phase != ConstructionPhase.LineDrafting || _lineDraft is null)
        {
            return new LinePointMovePreview(
                false,
                ConstructionError.WrongPhase,
                pointIndex,
                position,
                null,
                null,
                null);
        }
        return PlacementValidator.PreviewMoveLinePoint(
            _world,
            _lineDraft,
            pointIndex,
            position);
    }

    public ConstructionCommandResult MoveLinePoint(int pointIndex, MapPoint position)
    {
        LinePointMovePreview preview = PreviewMoveLinePoint(pointIndex, position);
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }

        MapPoint[] points = _lineDraft!.IntermediatePoints.ToArray();
        points[pointIndex] = position;
        _lineDraft = _lineDraft with { IntermediatePoints = points };
        RefreshLineDraftRiskAreas();
        return Accepted();
    }

    public ConstructionCommandResult UndoLinePoint()
    {
        if (_phase != ConstructionPhase.LineDrafting || _lineDraft is null)
        {
            return Rejected(ConstructionError.WrongPhase);
        }
        if (_lineDraft.EndNodeId is not null)
        {
            _lineDraft = _lineDraft with { EndNodeId = null };
            RefreshLineDraftRiskAreas();
            return Accepted();
        }
        if (_lineDraft.IntermediatePoints.Count == 0)
        {
            return Rejected(ConstructionError.NothingToUndo);
        }
        _lineDraft = _lineDraft with
        {
            IntermediatePoints = _lineDraft.IntermediatePoints.SkipLast(1).ToArray(),
        };
        RefreshLineDraftRiskAreas();
        return Accepted();
    }

    public LineFinishPreview PreviewLineFinish(string endNodeId)
    {
        if (_phase != ConstructionPhase.LineDrafting || _lineDraft is null)
        {
            return new LineFinishPreview(
                false,
                ConstructionError.WrongPhase,
                endNodeId,
                null,
                null);
        }
        return PlacementValidator.PreviewLineFinish(_world, _lineDraft, endNodeId);
    }

    public ConstructionCommandResult FinishLineDraft(string endNodeId)
    {
        LineFinishPreview preview = PreviewLineFinish(endNodeId);
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }
        _lineDraft = _lineDraft! with { EndNodeId = endNodeId };
        RefreshLineDraftRiskAreas();
        return Accepted();
    }

    public ConstructionCommandResult CancelLineDraft()
    {
        if (_phase != ConstructionPhase.LineDrafting)
        {
            return Rejected(ConstructionError.WrongPhase);
        }
        _lineDraft = null;
        _phase = ConstructionPhase.Ready;
        return Accepted();
    }

    public ConstructionQuote PreviewLineOrder()
    {
        if (_phase != ConstructionPhase.LineDrafting || _lineDraft is null)
        {
            return QuoteRejected(ConstructionError.WrongPhase);
        }
        ConstructionError? validation = PlacementValidator.ValidateCompleteLineDraft(
            _world,
            _lineDraft);
        if (validation is not null)
        {
            return QuoteRejected(validation.Value);
        }

        try
        {
            SpatialLineClassDefinition lineClass = LineClass(_lineDraft.LineClassId);
            SpatialNodeClassDefinition poleClass = NodeClass(_lineDraft.PoleClassId);
            SpatialNodeDefinition start = Node(_lineDraft.StartNodeId);
            SpatialNodeDefinition end = Node(_lineDraft.EndNodeId!);
            MapPoint[] points = [
                start.Position,
                .. _lineDraft.IntermediatePoints,
                end.Position,
            ];
            long pathLengthUnit = 0;
            for (int index = 0; index + 1 < points.Length; index++)
            {
                pathLengthUnit = checked(
                    pathLengthUnit + PlacementValidator.DistanceCeilingUnit(
                        points[index],
                        points[index + 1]));
            }
            long designLength = pathLengthUnit == 0
                ? 0
                : checked(
                    (pathLengthUnit + _world.UnitsPerDesignUnit - 1L) /
                    _world.UnitsPerDesignUnit);
            long cost = checked(
                checked(designLength * lineClass.CostCashUnitPerDesignUnit) +
                checked((long)_lineDraft.IntermediatePoints.Count * poleClass.CostCashUnit));
            long minutes = checked(
                checked(designLength * lineClass.BuildMinutesPerDesignUnit) +
                checked((long)_lineDraft.IntermediatePoints.Count * poleClass.BuildMinutes));
            return new ConstructionQuote(
                true,
                null,
                cost,
                minutes,
                checked(_minute + minutes))
            {
                RiskAreaIds = PlacementValidator.RiskAreasForLineDraft(_world, _lineDraft),
            };
        }
        catch (OverflowException)
        {
            return QuoteRejected(ConstructionError.ArithmeticOverflow);
        }
    }

    public ConstructionCommandResult OrderLine()
    {
        ConstructionQuote quote = PreviewLineOrder();
        if (!quote.Accepted)
        {
            return Rejected(quote.Error!.Value);
        }

        LineDraftSnapshot draft = _lineDraft!;
        SpatialNodeClassDefinition poleClass = NodeClass(draft.PoleClassId);
        List<SpatialNodeDefinition> nodes = _world.Nodes.ToList();
        List<string> createdNodeIds = [];
        foreach (MapPoint point in draft.IntermediatePoints)
        {
            (string nodeId, int ordinal) = NextAvailableId(
                "PLAYER_POLE",
                nodes.Select(item => item.NodeId));
            nodes.Add(new SpatialNodeDefinition(
                nodeId,
                draft.PoleClassId,
                $"{poleClass.DisplayName} {ordinal}",
                point,
                false,
                false));
            createdNodeIds.Add(nodeId);
        }

        string[] pathNodeIds = [draft.StartNodeId, .. createdNodeIds, draft.EndNodeId!];
        List<SpatialEdgeDefinition> edges = _world.Edges.ToList();
        List<string> createdEdgeIds = [];
        for (int index = 0; index + 1 < pathNodeIds.Length; index++)
        {
            (string edgeId, _) = NextAvailableId(
                "PLAYER_EDGE",
                edges.Select(item => item.EdgeId));
            edges.Add(new SpatialEdgeDefinition(
                edgeId,
                draft.LineClassId,
                pathNodeIds[index],
                pathNodeIds[index + 1],
                false));
            createdEdgeIds.Add(edgeId);
        }

        _world = _world with
        {
            Nodes = nodes.ToArray(),
            Edges = edges.ToArray(),
        };
        _lineDraft = null;
        _activeConstruction = new ActiveConstructionSnapshot(
            ConstructionKind.Line,
            quote.CostCashUnit!.Value,
            quote.CompletionMinute!.Value,
            createdNodeIds,
            createdEdgeIds)
        {
            RiskAreaIds = quote.RiskAreaIds,
        };
        _phase = ConstructionPhase.LineBuilding;
        return Accepted();
    }

    public ConstructionCommandResult AdvanceToConstructionCompletion()
    {
        if (_phase is not (ConstructionPhase.NodeBuilding or ConstructionPhase.LineBuilding) ||
            _activeConstruction is null)
        {
            return Rejected(ConstructionError.WrongPhase);
        }

        HashSet<string> nodeIds = _activeConstruction.NodeIds.ToHashSet(StringComparer.Ordinal);
        HashSet<string> edgeIds = _activeConstruction.EdgeIds.ToHashSet(StringComparer.Ordinal);
        SpatialWorldDefinition candidate = _world with
        {
            Nodes = _world.Nodes.Select(node =>
                nodeIds.Contains(node.NodeId) ? node with { Commissioned = true } : node).ToArray(),
            Edges = _world.Edges.Select(edge =>
                edgeIds.Contains(edge.EdgeId) ? edge with { Commissioned = true } : edge).ToArray(),
        };
        try
        {
            SpatialWorldLoader.Validate(candidate);
        }
        catch (SpatialWorldValidationException)
        {
            return Rejected(ConstructionError.InvalidCompletion);
        }

        _world = candidate;
        _minute = _activeConstruction.CompletionMinute;
        _activeConstruction = null;
        _phase = ConstructionPhase.Ready;
        return Accepted();
    }

    private ConstructionCommandResult Accepted() => new(true, null, GetSnapshot());

    private ConstructionCommandResult Rejected(ConstructionError error) =>
        new(false, error, GetSnapshot());

    private static ConstructionQuote QuoteRejected(ConstructionError error) =>
        new(false, error, null, null, null);

    private void RefreshLineDraftRiskAreas()
    {
        _lineDraft = _lineDraft! with
        {
            RiskAreaIds = PlacementValidator.RiskAreasForLineDraft(_world, _lineDraft),
        };
    }

    private SpatialNodeDefinition Node(string nodeId) =>
        _world.Nodes.Single(item => string.Equals(item.NodeId, nodeId, StringComparison.Ordinal));

    private SpatialNodeClassDefinition NodeClass(string classId) =>
        _world.NodeClasses.Single(item => string.Equals(item.ClassId, classId, StringComparison.Ordinal));

    private SpatialLineClassDefinition LineClass(string classId) =>
        _world.LineClasses.Single(item => string.Equals(item.ClassId, classId, StringComparison.Ordinal));

    private static (string Id, int Ordinal) NextAvailableId(
        string prefix,
        IEnumerable<string> existingIds)
    {
        HashSet<string> existing = existingIds.ToHashSet(StringComparer.Ordinal);
        for (int ordinal = 1; ordinal < int.MaxValue; ordinal++)
        {
            string candidate = $"{prefix}_{ordinal}";
            if (!existing.Contains(candidate))
            {
                return (candidate, ordinal);
            }
        }
        throw new OverflowException("Player construction identifier space is exhausted.");
    }
}
