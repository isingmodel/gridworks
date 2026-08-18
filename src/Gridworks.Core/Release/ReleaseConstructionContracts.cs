namespace Gridworks.Core.Release;

public enum ReleaseConstructionPhase
{
    Ready,
    NodeDrafting,
    NodeBuilding,
    LineDrafting,
    LineBuilding,
}

public enum ReleaseConstructionKind
{
    Node,
    Line,
}

public enum ReleaseConstructionError
{
    WrongPhase,
    UnknownClass,
    InvalidNodeClass,
    InvalidLineClass,
    InvalidPoleClass,
    OutsideGrid,
    WaterSurface,
    PositionOccupied,
    EndpointNotFound,
    EndpointNotCommissioned,
    SameEndpoint,
    ConnectionLimit,
    SpanTooLong,
    DuplicateSegment,
    DraftIncomplete,
    NothingToUndo,
    ArithmeticOverflow,
}

public sealed record ReleaseNodeDraftSnapshot(
    string NodeClassId,
    ReleasePoint Position);

public sealed record ReleaseLineDraftSnapshot(
    string StartNodeId,
    string LineClassId,
    string PoleClassId,
    IReadOnlyList<ReleasePoint> IntermediatePoints,
    string? EndNodeId)
{
    private IReadOnlyList<ReleasePoint> _intermediatePoints = Array.AsReadOnly(IntermediatePoints.ToArray());

    public IReadOnlyList<ReleasePoint> IntermediatePoints
    {
        get => _intermediatePoints;
        init => _intermediatePoints = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record ReleaseActiveConstructionSnapshot(
    ReleaseConstructionKind Kind,
    long CostCashUnit,
    long CompletionMinute,
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<string> EdgeIds)
{
    private IReadOnlyList<string> _nodeIds = Array.AsReadOnly(NodeIds.ToArray());
    private IReadOnlyList<string> _edgeIds = Array.AsReadOnly(EdgeIds.ToArray());

    public IReadOnlyList<string> NodeIds
    {
        get => _nodeIds;
        init => _nodeIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> EdgeIds
    {
        get => _edgeIds;
        init => _edgeIds = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record ReleaseConstructionSnapshot(
    long Minute,
    ReleaseConstructionPhase Phase,
    ReleaseWorldDefinition World,
    ReleaseNetworkEvaluation Evaluation,
    ReleaseNodeDraftSnapshot? NodeDraft,
    ReleaseLineDraftSnapshot? LineDraft,
    ReleaseActiveConstructionSnapshot? ActiveConstruction);

public sealed record ReleaseConstructionCommandResult(
    bool Accepted,
    ReleaseConstructionError? Error,
    ReleaseConstructionSnapshot Snapshot);

public sealed record ReleaseNodePlacementPreview(
    bool Accepted,
    ReleaseConstructionError? Error,
    string NodeClassId,
    ReleasePoint Position);

public sealed record ReleaseLinePointPreview(
    bool Accepted,
    ReleaseConstructionError? Error,
    ReleasePoint Position,
    bool EndsAtExistingNode,
    string? EndpointNodeId,
    long? DistanceSquared,
    long? MaxSpanSquared);

public sealed record ReleaseLineStartPreview(
    bool Accepted,
    ReleaseConstructionError? Error,
    string StartNodeId,
    string LineClassId,
    string PoleClassId);

public sealed record ReleaseConstructionQuote(
    bool Accepted,
    ReleaseConstructionError? Error,
    long? CostCashUnit,
    long? BuildMinutes,
    long? CompletionMinute);
