using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

public sealed record RealtimeConstructionCompletion(
    ConstructionKind Kind,
    long CompletionMinute,
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<string> EdgeIds)
{
    private IReadOnlyList<string> _nodeIds = RealtimeStructural.Freeze(NodeIds);
    private IReadOnlyList<string> _edgeIds = RealtimeStructural.Freeze(EdgeIds);

    public IReadOnlyList<string> NodeIds
    {
        get => _nodeIds;
        init => _nodeIds = RealtimeStructural.Freeze(value);
    }

    public IReadOnlyList<string> EdgeIds
    {
        get => _edgeIds;
        init => _edgeIds = RealtimeStructural.Freeze(value);
    }
}

public sealed record RealtimeConstructionAdvanceResult(
    ConstructionSnapshot Snapshot,
    RealtimeConstructionCompletion? Completion);

/// <summary>
/// One-crew construction state whose clock is the campaign's absolute integer minute.
/// Drafting and ordering preserve the V2 placement rules; completion is driven only by AdvanceTo.
/// </summary>
public sealed class RealtimeConstructionSession
{
    private SpatialWorldDefinition _world;
    private long _minute;
    private NodeDraftSnapshot? _nodeDraft;
    private LineDraftSnapshot? _lineDraft;
    private ActiveConstructionSnapshot? _activeConstruction;

    public RealtimeConstructionSession(SpatialWorldDefinition world, long startMinute)
    {
        ArgumentNullException.ThrowIfNull(world);
        SpatialWorldLoader.Validate(world);
        if (startMinute < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startMinute));
        }
        _world = world with { };
        _minute = startMinute;
    }

    public ConstructionSnapshot GetSnapshot() => new(
        _minute,
        Phase,
        _world,
        _nodeDraft,
        _lineDraft,
        _activeConstruction);

    public RealtimeConstructionSession Fork() =>
        (RealtimeConstructionSession)MemberwiseClone();

    public RealtimeConstructionSession? ForkWithComparisonDraftCommissioned()
    {
        if (_nodeDraft is null &&
            (_lineDraft is null || _lineDraft.EndNodeId is null))
        {
            return null;
        }
        var result = (RealtimeConstructionSession)MemberwiseClone();
        SpatialWorldDefinition candidate;
        if (_nodeDraft is not null)
        {
            NodePlacementPreview preview = PlacementValidator.PreviewNodePlacement(
                _world,
                _nodeDraft.NodeClassId,
                _nodeDraft.Position);
            if (!preview.Accepted)
            {
                return null;
            }
            SpatialNodeClassDefinition nodeClass = NodeClass(_nodeDraft.NodeClassId);
            (string nodeId, int ordinal) = NextAvailableId(
                "PLAYER_SUBSTATION",
                _world.Nodes.Select(item => item.NodeId));
            candidate = _world with
            {
                Nodes = _world.Nodes.Append(new SpatialNodeDefinition(
                    nodeId,
                    _nodeDraft.NodeClassId,
                    $"{nodeClass.DisplayName} {ordinal}",
                    _nodeDraft.Position,
                    true,
                    false)).ToArray(),
            };
        }
        else
        {
            ConstructionError? validation = PlacementValidator.ValidateCompleteLineDraft(
                _world,
                _lineDraft!);
            if (validation is not null)
            {
                return null;
            }
            SpatialNodeClassDefinition poleClass = NodeClass(_lineDraft!.PoleClassId);
            List<SpatialNodeDefinition> nodes = _world.Nodes.ToList();
            var createdNodeIds = new List<string>();
            foreach (MapPoint point in _lineDraft.IntermediatePoints)
            {
                (string id, int ordinal) = NextAvailableId(
                    "PLAYER_POLE",
                    nodes.Select(item => item.NodeId));
                nodes.Add(new SpatialNodeDefinition(
                    id,
                    _lineDraft.PoleClassId,
                    $"{poleClass.DisplayName} {ordinal}",
                    point,
                    true,
                    false));
                createdNodeIds.Add(id);
            }
            string[] pathNodeIds =
                [_lineDraft.StartNodeId, .. createdNodeIds, _lineDraft.EndNodeId!];
            List<SpatialEdgeDefinition> edges = _world.Edges.ToList();
            for (int index = 0; index + 1 < pathNodeIds.Length; index++)
            {
                (string id, _) = NextAvailableId(
                    "PLAYER_EDGE",
                    edges.Select(item => item.EdgeId));
                edges.Add(new SpatialEdgeDefinition(
                    id,
                    _lineDraft.LineClassId,
                    pathNodeIds[index],
                    pathNodeIds[index + 1],
                    true));
            }
            candidate = _world with { Nodes = nodes.ToArray(), Edges = edges.ToArray() };
        }
        result._world = candidate;
        result._nodeDraft = null;
        result._lineDraft = null;
        return result;
    }

    public NodePlacementPreview PreviewNodePlacement(string nodeClassId, MapPoint position)
    {
        if (_lineDraft is not null)
        {
            return new NodePlacementPreview(false, ConstructionError.WrongPhase, nodeClassId, position);
        }
        return PlacementValidator.PreviewNodePlacement(_world, nodeClassId, position);
    }

    public ConstructionCommandResult SetNodeDraft(string nodeClassId, MapPoint position)
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
        return Accepted();
    }

    public ConstructionCommandResult CancelNodeDraft()
    {
        if (_nodeDraft is null)
        {
            return Rejected(ConstructionError.WrongPhase);
        }
        _nodeDraft = null;
        return Accepted();
    }

    public ConstructionQuote PreviewNodeOrder()
    {
        if (_nodeDraft is null)
        {
            return QuoteRejected(ConstructionError.WrongPhase);
        }
        if (_activeConstruction is not null)
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
            return new ConstructionQuote(
                true,
                null,
                nodeClass.CostCashUnit,
                nodeClass.BuildMinutes,
                checked(_minute + nodeClass.BuildMinutes))
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
        _world = _world with { Nodes = _world.Nodes.Append(node).ToArray() };
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
        return Accepted();
    }

    public LineStartPreview PreviewLineStart(
        string startNodeId,
        string lineClassId,
        string poleClassId)
    {
        if (_nodeDraft is not null || _lineDraft is not null)
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
            null);
        return Accepted();
    }

    public LinePointPreview PreviewLinePoint(MapPoint position) =>
        _lineDraft is not null
            ? PlacementValidator.PreviewLinePoint(_world, _lineDraft, position)
            : new LinePointPreview(false, ConstructionError.WrongPhase, position, null, null);

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
        RefreshLineRisk();
        return Accepted();
    }

    public LinePointMovePreview PreviewMoveLinePoint(int pointIndex, MapPoint position) =>
        _lineDraft is not null
            ? PlacementValidator.PreviewMoveLinePoint(_world, _lineDraft, pointIndex, position)
            : new LinePointMovePreview(
                false,
                ConstructionError.WrongPhase,
                pointIndex,
                position,
                null,
                null,
                null);

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
        RefreshLineRisk();
        return Accepted();
    }

    public ConstructionCommandResult UndoLinePoint()
    {
        if (_lineDraft is null)
        {
            return Rejected(ConstructionError.WrongPhase);
        }
        if (_lineDraft.EndNodeId is not null)
        {
            _lineDraft = _lineDraft with { EndNodeId = null };
            RefreshLineRisk();
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
        RefreshLineRisk();
        return Accepted();
    }

    public LineFinishPreview PreviewLineFinish(string endNodeId) =>
        _lineDraft is not null
            ? PlacementValidator.PreviewLineFinish(_world, _lineDraft, endNodeId)
            : new LineFinishPreview(false, ConstructionError.WrongPhase, endNodeId, null, null);

    public ConstructionCommandResult FinishLineDraft(string endNodeId)
    {
        LineFinishPreview preview = PreviewLineFinish(endNodeId);
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }
        _lineDraft = _lineDraft! with { EndNodeId = endNodeId };
        RefreshLineRisk();
        return Accepted();
    }

    public ConstructionCommandResult CancelLineDraft()
    {
        if (_lineDraft is null)
        {
            return Rejected(ConstructionError.WrongPhase);
        }
        _lineDraft = null;
        return Accepted();
    }

    public ConstructionQuote PreviewLineOrder()
    {
        if (_lineDraft is null)
        {
            return QuoteRejected(ConstructionError.WrongPhase);
        }
        if (_activeConstruction is not null)
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
            MapPoint[] points =
            [
                Node(_lineDraft.StartNodeId).Position,
                .. _lineDraft.IntermediatePoints,
                Node(_lineDraft.EndNodeId!).Position,
            ];
            long length = 0;
            for (int index = 0; index + 1 < points.Length; index++)
            {
                length = checked(length + PlacementValidator.DistanceCeilingUnit(
                    points[index], points[index + 1]));
            }
            long designLength = length == 0
                ? 0
                : checked((length + _world.UnitsPerDesignUnit - 1L) /
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
        var createdNodeIds = new List<string>();
        foreach (MapPoint point in draft.IntermediatePoints)
        {
            (string id, int ordinal) = NextAvailableId(
                "PLAYER_POLE", nodes.Select(item => item.NodeId));
            nodes.Add(new SpatialNodeDefinition(
                id,
                draft.PoleClassId,
                $"{poleClass.DisplayName} {ordinal}",
                point,
                false,
                false));
            createdNodeIds.Add(id);
        }
        string[] pathNodeIds = [draft.StartNodeId, .. createdNodeIds, draft.EndNodeId!];
        List<SpatialEdgeDefinition> edges = _world.Edges.ToList();
        var createdEdgeIds = new List<string>();
        for (int index = 0; index + 1 < pathNodeIds.Length; index++)
        {
            (string id, _) = NextAvailableId(
                "PLAYER_EDGE", edges.Select(item => item.EdgeId));
            edges.Add(new SpatialEdgeDefinition(
                id,
                draft.LineClassId,
                pathNodeIds[index],
                pathNodeIds[index + 1],
                false));
            createdEdgeIds.Add(id);
        }
        _world = _world with { Nodes = nodes.ToArray(), Edges = edges.ToArray() };
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
        return Accepted();
    }

    public RealtimeConstructionAdvanceResult AdvanceTo(long targetMinute)
    {
        if (targetMinute < _minute)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetMinute),
                "Construction time cannot move backward.");
        }
        RealtimeConstructionCompletion? completion = null;
        if (_activeConstruction is { } active && active.CompletionMinute <= targetMinute)
        {
            HashSet<string> nodeIds = active.NodeIds.ToHashSet(StringComparer.Ordinal);
            HashSet<string> edgeIds = active.EdgeIds.ToHashSet(StringComparer.Ordinal);
            SpatialWorldDefinition candidate = _world with
            {
                Nodes = _world.Nodes.Select(node => nodeIds.Contains(node.NodeId)
                    ? node with { Commissioned = true }
                    : node).ToArray(),
                Edges = _world.Edges.Select(edge => edgeIds.Contains(edge.EdgeId)
                    ? edge with { Commissioned = true }
                    : edge).ToArray(),
            };
            SpatialWorldLoader.Validate(candidate);
            _world = candidate;
            completion = new RealtimeConstructionCompletion(
                active.Kind,
                active.CompletionMinute,
                active.NodeIds,
                active.EdgeIds);
            _activeConstruction = null;
        }
        _minute = targetMinute;
        return new RealtimeConstructionAdvanceResult(GetSnapshot(), completion);
    }

    private ConstructionCommandResult Accepted() => new(true, null, GetSnapshot());

    private ConstructionPhase Phase => _nodeDraft is not null
        ? ConstructionPhase.NodeDrafting
        : _lineDraft is not null
            ? ConstructionPhase.LineDrafting
            : _activeConstruction?.Kind switch
            {
                ConstructionKind.Node => ConstructionPhase.NodeBuilding,
                ConstructionKind.Line => ConstructionPhase.LineBuilding,
                _ => ConstructionPhase.Ready,
            };

    private ConstructionCommandResult Rejected(ConstructionError error) =>
        new(false, error, GetSnapshot());

    private static ConstructionQuote QuoteRejected(ConstructionError error) =>
        new(false, error, null, null, null);

    private void RefreshLineRisk()
    {
        _lineDraft = _lineDraft! with
        {
            RiskAreaIds = PlacementValidator.RiskAreasForLineDraft(_world, _lineDraft),
        };
    }

    private SpatialNodeDefinition Node(string id) => _world.Nodes.Single(item =>
        string.Equals(item.NodeId, id, StringComparison.Ordinal));

    private SpatialNodeClassDefinition NodeClass(string id) => _world.NodeClasses.Single(item =>
        string.Equals(item.ClassId, id, StringComparison.Ordinal));

    private SpatialLineClassDefinition LineClass(string id) => _world.LineClasses.Single(item =>
        string.Equals(item.ClassId, id, StringComparison.Ordinal));

    private static (string Id, int Ordinal) NextAvailableId(
        string prefix,
        IEnumerable<string> existingIds)
    {
        HashSet<string> existing = existingIds.ToHashSet(StringComparer.Ordinal);
        for (int ordinal = 1; ordinal < int.MaxValue; ordinal++)
        {
            string id = $"{prefix}_{ordinal}";
            if (!existing.Contains(id))
            {
                return (id, ordinal);
            }
        }
        throw new OverflowException("Construction identifier space is exhausted.");
    }
}
