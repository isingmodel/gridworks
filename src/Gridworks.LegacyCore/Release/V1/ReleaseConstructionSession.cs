namespace Gridworks.Core.Release;

public sealed class ReleaseConstructionSession
{
    private ReleaseWorldDefinition _world;
    private long _minute;
    private ReleaseConstructionPhase _phase = ReleaseConstructionPhase.Ready;
    private ReleaseNodeDraftSnapshot? _nodeDraft;
    private LineDraftState? _lineDraft;
    private ReleaseActiveConstructionSnapshot? _activeConstruction;
    private int _nextSubstationOrdinal = 1;
    private int _nextPoleOrdinal = 1;
    private int _nextEdgeOrdinal = 1;

    public ReleaseConstructionSession(ReleaseWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(world);
        ReleaseWorldLoader.Validate(world);
        _world = world with { };
    }

    public ReleaseConstructionSnapshot GetSnapshot() => new(
        _minute,
        _phase,
        _world,
        ReleaseNetworkEvaluator.Evaluate(_world),
        _nodeDraft,
        _lineDraft?.ToSnapshot(),
        _activeConstruction);

    public ReleaseNodePlacementPreview PreviewNodePlacement(
        string nodeClassId,
        ReleasePoint position)
    {
        if (_phase is not (ReleaseConstructionPhase.Ready or ReleaseConstructionPhase.NodeDrafting))
        {
            return NodePreview(false, ReleaseConstructionError.WrongPhase, nodeClassId, position);
        }

        if (!TryNodeClass(nodeClassId, out ReleaseNodeClassDefinition? nodeClass) || nodeClass is null)
        {
            return NodePreview(false, ReleaseConstructionError.UnknownClass, nodeClassId, position);
        }

        if (nodeClass.Kind != ReleaseNodeKind.Substation)
        {
            return NodePreview(false, ReleaseConstructionError.InvalidNodeClass, nodeClassId, position);
        }

        if (!InGrid(position))
        {
            return NodePreview(false, ReleaseConstructionError.OutsideGrid, nodeClassId, position);
        }

        if (NodeAt(position) is not null)
        {
            return NodePreview(false, ReleaseConstructionError.PositionOccupied, nodeClassId, position);
        }

        if (OnWater(position))
        {
            return NodePreview(false, ReleaseConstructionError.WaterSurface, nodeClassId, position);
        }

        return NodePreview(true, null, nodeClassId, position);
    }

    public ReleaseConstructionCommandResult SetNodeDraft(
        string nodeClassId,
        ReleasePoint position)
    {
        ReleaseNodePlacementPreview preview = PreviewNodePlacement(nodeClassId, position);
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }

        _nodeDraft = new ReleaseNodeDraftSnapshot(nodeClassId, position);
        _phase = ReleaseConstructionPhase.NodeDrafting;
        return Accepted();
    }

    public ReleaseConstructionCommandResult CancelNodeDraft()
    {
        if (_phase != ReleaseConstructionPhase.NodeDrafting)
        {
            return Rejected(ReleaseConstructionError.WrongPhase);
        }

        _nodeDraft = null;
        _phase = ReleaseConstructionPhase.Ready;
        return Accepted();
    }

    public ReleaseConstructionQuote PreviewNodeOrder()
    {
        if (_phase != ReleaseConstructionPhase.NodeDrafting || _nodeDraft is null)
        {
            return QuoteRejected(ReleaseConstructionError.WrongPhase);
        }

        ReleaseNodePlacementPreview placement = PreviewNodePlacement(
            _nodeDraft.NodeClassId,
            _nodeDraft.Position);
        if (!placement.Accepted)
        {
            return QuoteRejected(placement.Error!.Value);
        }

        ReleaseNodeClassDefinition nodeClass = NodeClass(_nodeDraft.NodeClassId);
        try
        {
            long completionMinute = checked(_minute + nodeClass.BuildMinutes);
            return new ReleaseConstructionQuote(
                true,
                null,
                nodeClass.CostCashUnit,
                nodeClass.BuildMinutes,
                completionMinute);
        }
        catch (OverflowException)
        {
            return QuoteRejected(ReleaseConstructionError.ArithmeticOverflow);
        }
    }

    public ReleaseConstructionCommandResult OrderNode()
    {
        ReleaseConstructionQuote quote = PreviewNodeOrder();
        if (!quote.Accepted)
        {
            return Rejected(quote.Error!.Value);
        }

        ReleaseNodeDraftSnapshot draft = _nodeDraft!;
        ReleaseNodeClassDefinition nodeClass = NodeClass(draft.NodeClassId);
        int nextOrdinal = _nextSubstationOrdinal;
        string nodeId = NextAvailableId(
            "PLAYER_SUBSTATION",
            _world.Nodes.Select(item => item.NodeId),
            ref nextOrdinal);
        var node = new ReleaseNodeDefinition(
            nodeId,
            draft.NodeClassId,
            $"{nodeClass.DisplayName} {nextOrdinal - 1}",
            draft.Position,
            false);
        ReleaseWorldDefinition candidate = _world with
        {
            Nodes = _world.Nodes.Append(node).ToArray(),
        };
        ReleaseWorldLoader.Validate(candidate);

        _nextSubstationOrdinal = nextOrdinal;
        _world = candidate;
        _nodeDraft = null;
        _activeConstruction = new ReleaseActiveConstructionSnapshot(
            ReleaseConstructionKind.Node,
            quote.CostCashUnit!.Value,
            quote.CompletionMinute!.Value,
            [nodeId],
            []);
        _phase = ReleaseConstructionPhase.NodeBuilding;
        return Accepted();
    }

    public ReleaseLineStartPreview PreviewLineStart(
        string startNodeId,
        string lineClassId,
        string poleClassId)
    {
        if (_phase != ReleaseConstructionPhase.Ready)
        {
            return LineStartPreview(
                false,
                ReleaseConstructionError.WrongPhase,
                startNodeId,
                lineClassId,
                poleClassId);
        }

        ReleaseNodeDefinition? start = NodeById(startNodeId);
        if (start is null)
        {
            return LineStartPreview(
                false,
                ReleaseConstructionError.EndpointNotFound,
                startNodeId,
                lineClassId,
                poleClassId);
        }
        if (!start.Commissioned)
        {
            return LineStartPreview(
                false,
                ReleaseConstructionError.EndpointNotCommissioned,
                startNodeId,
                lineClassId,
                poleClassId);
        }
        if (!TryLineClass(lineClassId, out _))
        {
            return LineStartPreview(
                false,
                ReleaseConstructionError.InvalidLineClass,
                startNodeId,
                lineClassId,
                poleClassId);
        }
        if (!TryNodeClass(poleClassId, out ReleaseNodeClassDefinition? poleClass) ||
            poleClass is null ||
            poleClass.Kind != ReleaseNodeKind.Pole ||
            poleClass.MaxConnections < 2)
        {
            return LineStartPreview(
                false,
                ReleaseConstructionError.InvalidPoleClass,
                startNodeId,
                lineClassId,
                poleClassId);
        }
        if (ConnectionCount(startNodeId) >= NodeClass(start.ClassId).MaxConnections)
        {
            return LineStartPreview(
                false,
                ReleaseConstructionError.ConnectionLimit,
                startNodeId,
                lineClassId,
                poleClassId);
        }

        return LineStartPreview(true, null, startNodeId, lineClassId, poleClassId);
    }

    public ReleaseConstructionCommandResult StartLineDraft(
        string startNodeId,
        string lineClassId,
        string poleClassId)
    {
        ReleaseLineStartPreview preview = PreviewLineStart(startNodeId, lineClassId, poleClassId);
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }

        _lineDraft = new LineDraftState(startNodeId, lineClassId, poleClassId);
        _phase = ReleaseConstructionPhase.LineDrafting;
        return Accepted();
    }

    public ReleaseLinePointPreview PreviewLinePoint(ReleasePoint position)
    {
        if (_phase != ReleaseConstructionPhase.LineDrafting || _lineDraft is null ||
            _lineDraft.EndNodeId is not null)
        {
            return LinePreview(false, ReleaseConstructionError.WrongPhase, position);
        }

        if (!InGrid(position))
        {
            return LinePreview(false, ReleaseConstructionError.OutsideGrid, position);
        }

        ReleaseNodeDefinition start = NodeById(_lineDraft.StartNodeId)!;
        ReleasePoint from = _lineDraft.IntermediatePoints.Count == 0
            ? start.Position
            : _lineDraft.IntermediatePoints[^1];
        if (position == from || _lineDraft.IntermediatePoints.Contains(position))
        {
            return LinePreview(false, ReleaseConstructionError.PositionOccupied, position);
        }

        ReleaseNodeDefinition? endpoint = NodeAt(position);
        if (endpoint is not null)
        {
            if (string.Equals(endpoint.NodeId, start.NodeId, StringComparison.Ordinal))
            {
                return LinePreview(false, ReleaseConstructionError.SameEndpoint, position);
            }
            if (!endpoint.Commissioned)
            {
                return LinePreview(false, ReleaseConstructionError.EndpointNotCommissioned, position);
            }
            if (ConnectionCount(endpoint.NodeId) >= NodeClass(endpoint.ClassId).MaxConnections ||
                ConnectionCount(start.NodeId) >= NodeClass(start.ClassId).MaxConnections)
            {
                return LinePreview(false, ReleaseConstructionError.ConnectionLimit, position);
            }

            string fromNodeId = _lineDraft.IntermediatePoints.Count == 0
                ? start.NodeId
                : string.Empty;
            if (fromNodeId.Length != 0 && EdgePairExists(fromNodeId, endpoint.NodeId))
            {
                return LinePreview(false, ReleaseConstructionError.DuplicateSegment, position);
            }
        }
        else if (OnWater(position))
        {
            return LinePreview(false, ReleaseConstructionError.WaterSurface, position);
        }

        ReleaseLineClassDefinition lineClass = LineClass(_lineDraft.LineClassId);
        long distanceSquared = ReleaseGridMath.DistanceSquared(from, position);
        long maxSpanSquared = checked((long)lineClass.MaxSpanCells * lineClass.MaxSpanCells);
        if (distanceSquared > maxSpanSquared)
        {
            return new ReleaseLinePointPreview(
                false,
                ReleaseConstructionError.SpanTooLong,
                position,
                endpoint is not null,
                endpoint?.NodeId,
                distanceSquared,
                maxSpanSquared);
        }

        return new ReleaseLinePointPreview(
            true,
            null,
            position,
            endpoint is not null,
            endpoint?.NodeId,
            distanceSquared,
            maxSpanSquared);
    }

    public ReleaseConstructionCommandResult AddLinePoint(ReleasePoint position)
    {
        ReleaseLinePointPreview preview = PreviewLinePoint(position);
        if (!preview.Accepted)
        {
            return Rejected(preview.Error!.Value);
        }

        if (preview.EndsAtExistingNode)
        {
            _lineDraft!.EndNodeId = preview.EndpointNodeId;
        }
        else
        {
            _lineDraft!.IntermediatePoints.Add(position);
        }
        return Accepted();
    }

    public ReleaseConstructionCommandResult UndoLinePoint()
    {
        if (_phase != ReleaseConstructionPhase.LineDrafting || _lineDraft is null)
        {
            return Rejected(ReleaseConstructionError.WrongPhase);
        }
        if (_lineDraft.EndNodeId is not null)
        {
            _lineDraft.EndNodeId = null;
            return Accepted();
        }
        if (_lineDraft.IntermediatePoints.Count == 0)
        {
            return Rejected(ReleaseConstructionError.NothingToUndo);
        }

        _lineDraft.IntermediatePoints.RemoveAt(_lineDraft.IntermediatePoints.Count - 1);
        return Accepted();
    }

    public ReleaseConstructionCommandResult CancelLineDraft()
    {
        if (_phase != ReleaseConstructionPhase.LineDrafting)
        {
            return Rejected(ReleaseConstructionError.WrongPhase);
        }

        _lineDraft = null;
        _phase = ReleaseConstructionPhase.Ready;
        return Accepted();
    }

    public ReleaseConstructionQuote PreviewLineOrder()
    {
        if (_phase != ReleaseConstructionPhase.LineDrafting ||
            _lineDraft?.EndNodeId is null)
        {
            return QuoteRejected(
                _phase == ReleaseConstructionPhase.LineDrafting
                    ? ReleaseConstructionError.DraftIncomplete
                    : ReleaseConstructionError.WrongPhase);
        }

        ReleaseNodeDefinition start = NodeById(_lineDraft.StartNodeId)!;
        ReleaseNodeDefinition end = NodeById(_lineDraft.EndNodeId)!;
        if (!start.Commissioned || !end.Commissioned)
        {
            return QuoteRejected(ReleaseConstructionError.EndpointNotCommissioned);
        }
        if (ConnectionCount(start.NodeId) >= NodeClass(start.ClassId).MaxConnections ||
            ConnectionCount(end.NodeId) >= NodeClass(end.ClassId).MaxConnections)
        {
            return QuoteRejected(ReleaseConstructionError.ConnectionLimit);
        }

        try
        {
            ReleaseLineClassDefinition lineClass = LineClass(_lineDraft.LineClassId);
            ReleaseNodeClassDefinition poleClass = NodeClass(_lineDraft.PoleClassId);
            var points = new List<ReleasePoint> { start.Position };
            points.AddRange(_lineDraft.IntermediatePoints);
            points.Add(end.Position);

            long lengthMilliCells = 0;
            for (int index = 0; index < points.Count - 1; index++)
            {
                long distanceSquared = ReleaseGridMath.DistanceSquared(points[index], points[index + 1]);
                long maxSpanSquared = checked((long)lineClass.MaxSpanCells * lineClass.MaxSpanCells);
                if (distanceSquared > maxSpanSquared)
                {
                    return QuoteRejected(ReleaseConstructionError.SpanTooLong);
                }
                lengthMilliCells = checked(
                    lengthMilliCells + ReleaseGridMath.EdgeLengthMilliCells(points[index], points[index + 1]));
            }

            long cost = checked(
                checked(lengthMilliCells * lineClass.CostCashUnitPerMilliCell) +
                checked(_lineDraft.IntermediatePoints.Count * poleClass.CostCashUnit));
            long buildMinutes = checked(
                checked(lengthMilliCells * lineClass.BuildMinutesPerMilliCell) +
                checked((long)_lineDraft.IntermediatePoints.Count * poleClass.BuildMinutes));
            long completionMinute = checked(_minute + buildMinutes);
            return new ReleaseConstructionQuote(true, null, cost, buildMinutes, completionMinute);
        }
        catch (OverflowException)
        {
            return QuoteRejected(ReleaseConstructionError.ArithmeticOverflow);
        }
    }

    public ReleaseConstructionCommandResult OrderLine()
    {
        ReleaseConstructionQuote quote = PreviewLineOrder();
        if (!quote.Accepted)
        {
            return Rejected(quote.Error!.Value);
        }

        LineDraftState draft = _lineDraft!;
        ReleaseNodeClassDefinition poleClass = NodeClass(draft.PoleClassId);
        int nextPole = _nextPoleOrdinal;
        int nextEdge = _nextEdgeOrdinal;
        var existingNodeIds = new HashSet<string>(
            _world.Nodes.Select(item => item.NodeId),
            StringComparer.Ordinal);
        var existingEdgeIds = new HashSet<string>(
            _world.Edges.Select(item => item.EdgeId),
            StringComparer.Ordinal);
        var addedNodes = new List<ReleaseNodeDefinition>();
        foreach (ReleasePoint point in draft.IntermediatePoints)
        {
            string id = NextAvailableId("PLAYER_POLE", existingNodeIds, ref nextPole);
            existingNodeIds.Add(id);
            addedNodes.Add(new ReleaseNodeDefinition(
                id,
                draft.PoleClassId,
                $"{poleClass.DisplayName} {nextPole - 1}",
                point,
                false));
        }

        var sequence = new List<string> { draft.StartNodeId };
        sequence.AddRange(addedNodes.Select(item => item.NodeId));
        sequence.Add(draft.EndNodeId!);
        var addedEdges = new List<ReleaseEdgeDefinition>();
        for (int index = 0; index < sequence.Count - 1; index++)
        {
            string edgeId = NextAvailableId("PLAYER_EDGE", existingEdgeIds, ref nextEdge);
            existingEdgeIds.Add(edgeId);
            addedEdges.Add(new ReleaseEdgeDefinition(
                edgeId,
                draft.LineClassId,
                sequence[index],
                sequence[index + 1],
                false));
        }

        ReleaseWorldDefinition candidate = _world with
        {
            Nodes = _world.Nodes.Concat(addedNodes).ToArray(),
            Edges = _world.Edges.Concat(addedEdges).ToArray(),
        };
        ReleaseWorldLoader.Validate(candidate);

        _nextPoleOrdinal = nextPole;
        _nextEdgeOrdinal = nextEdge;
        _world = candidate;
        _lineDraft = null;
        _activeConstruction = new ReleaseActiveConstructionSnapshot(
            ReleaseConstructionKind.Line,
            quote.CostCashUnit!.Value,
            quote.CompletionMinute!.Value,
            addedNodes.Select(item => item.NodeId).ToArray(),
            addedEdges.Select(item => item.EdgeId).ToArray());
        _phase = ReleaseConstructionPhase.LineBuilding;
        return Accepted();
    }

    public ReleaseConstructionCommandResult AdvanceToConstructionCompletion()
    {
        if (_phase is not (ReleaseConstructionPhase.NodeBuilding or ReleaseConstructionPhase.LineBuilding) ||
            _activeConstruction is null)
        {
            return Rejected(ReleaseConstructionError.WrongPhase);
        }

        HashSet<string> nodeIds = new(_activeConstruction.NodeIds, StringComparer.Ordinal);
        HashSet<string> edgeIds = new(_activeConstruction.EdgeIds, StringComparer.Ordinal);
        _world = _world with
        {
            Nodes = _world.Nodes.Select(item => nodeIds.Contains(item.NodeId)
                ? item with { Commissioned = true }
                : item).ToArray(),
            Edges = _world.Edges.Select(item => edgeIds.Contains(item.EdgeId)
                ? item with { Commissioned = true }
                : item).ToArray(),
        };
        ReleaseWorldLoader.Validate(_world);
        _minute = _activeConstruction.CompletionMinute;
        _activeConstruction = null;
        _phase = ReleaseConstructionPhase.Ready;
        return Accepted();
    }

    private ReleaseConstructionCommandResult Accepted() => new(true, null, GetSnapshot());

    private ReleaseConstructionCommandResult Rejected(ReleaseConstructionError error) =>
        new(false, error, GetSnapshot());

    private static ReleaseNodePlacementPreview NodePreview(
        bool accepted,
        ReleaseConstructionError? error,
        string classId,
        ReleasePoint position) => new(accepted, error, classId, position);

    private static ReleaseLinePointPreview LinePreview(
        bool accepted,
        ReleaseConstructionError error,
        ReleasePoint position) => new(false, error, position, false, null, null, null);

    private static ReleaseLineStartPreview LineStartPreview(
        bool accepted,
        ReleaseConstructionError? error,
        string startNodeId,
        string lineClassId,
        string poleClassId) =>
        new(accepted, error, startNodeId, lineClassId, poleClassId);

    private static ReleaseConstructionQuote QuoteRejected(ReleaseConstructionError error) =>
        new(false, error, null, null, null);

    private bool TryNodeClass(string classId, out ReleaseNodeClassDefinition? nodeClass)
    {
        nodeClass = _world.NodeClasses.SingleOrDefault(item =>
            string.Equals(item.ClassId, classId, StringComparison.Ordinal));
        return nodeClass is not null;
    }

    private ReleaseNodeClassDefinition NodeClass(string classId) =>
        _world.NodeClasses.Single(item => string.Equals(item.ClassId, classId, StringComparison.Ordinal));

    private bool TryLineClass(string classId, out ReleaseLineClassDefinition? lineClass)
    {
        lineClass = _world.LineClasses.SingleOrDefault(item =>
            string.Equals(item.ClassId, classId, StringComparison.Ordinal));
        return lineClass is not null;
    }

    private ReleaseLineClassDefinition LineClass(string classId) =>
        _world.LineClasses.Single(item => string.Equals(item.ClassId, classId, StringComparison.Ordinal));

    private ReleaseNodeDefinition? NodeById(string nodeId) =>
        _world.Nodes.SingleOrDefault(item => string.Equals(item.NodeId, nodeId, StringComparison.Ordinal));

    private ReleaseNodeDefinition? NodeAt(ReleasePoint position) =>
        _world.Nodes.SingleOrDefault(item => item.Position == position);

    private int ConnectionCount(string nodeId) => _world.Edges.Count(item =>
        string.Equals(item.FromNodeId, nodeId, StringComparison.Ordinal) ||
        string.Equals(item.ToNodeId, nodeId, StringComparison.Ordinal));

    private bool EdgePairExists(string first, string second) => _world.Edges.Any(item =>
        string.Equals(item.FromNodeId, first, StringComparison.Ordinal) &&
        string.Equals(item.ToNodeId, second, StringComparison.Ordinal) ||
        string.Equals(item.FromNodeId, second, StringComparison.Ordinal) &&
        string.Equals(item.ToNodeId, first, StringComparison.Ordinal));

    private bool InGrid(ReleasePoint point) =>
        point.X >= _world.Grid.MinX && point.X <= _world.Grid.MaxX &&
        point.Y >= _world.Grid.MinY && point.Y <= _world.Grid.MaxY;

    private bool OnWater(ReleasePoint point) =>
        _world.WaterPolygon.Count != 0 &&
        ReleaseGridMath.PointInPolygon(point, _world.WaterPolygon);

    private static string NextAvailableId(
        string prefix,
        IEnumerable<string> existingIds,
        ref int ordinal)
    {
        var existing = existingIds as IReadOnlySet<string> ??
            new HashSet<string>(existingIds, StringComparer.Ordinal);
        string candidate;
        do
        {
            candidate = $"{prefix}_{ordinal:0000}";
            ordinal++;
        }
        while (existing.Contains(candidate));
        return candidate;
    }

    private sealed class LineDraftState
    {
        public LineDraftState(string startNodeId, string lineClassId, string poleClassId)
        {
            StartNodeId = startNodeId;
            LineClassId = lineClassId;
            PoleClassId = poleClassId;
        }

        public string StartNodeId { get; }
        public string LineClassId { get; }
        public string PoleClassId { get; }
        public List<ReleasePoint> IntermediatePoints { get; } = [];
        public string? EndNodeId { get; set; }

        public ReleaseLineDraftSnapshot ToSnapshot() => new(
            StartNodeId,
            LineClassId,
            PoleClassId,
            IntermediatePoints,
            EndNodeId);
    }
}
