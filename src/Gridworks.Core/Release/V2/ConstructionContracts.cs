namespace Gridworks.Core.Release.V2;

public enum ConstructionPhase
{
    Ready,
    NodeDrafting,
    NodeBuilding,
    LineDrafting,
    LineBuilding,
}

public enum ConstructionKind
{
    Node,
    Line,
}

public enum ConstructionError
{
    WrongPhase,
    UnknownNodeClass,
    InvalidNodeClass,
    UnknownLineClass,
    UnknownPoleClass,
    InvalidPoleClass,
    OutsideBounds,
    WaterFootprint,
    BuildingFootprint,
    PositionOccupied,
    ExistingLineTouch,
    EndpointNotFound,
    EndpointNotCommissioned,
    SameEndpoint,
    ConnectionLimit,
    SpanTooLong,
    ZeroLengthSegment,
    ThirdNodeTouch,
    DuplicateSegment,
    CollinearOverlap,
    BuildingCrossing,
    DraftIncomplete,
    NothingToUndo,
    InvalidPointIndex,
    ArithmeticOverflow,
    InvalidCompletion,
}

public sealed record NodeDraftSnapshot(
    string NodeClassId,
    MapPoint Position)
{
    private IReadOnlyList<string> _riskAreaIds = Array.Empty<string>();

    public IReadOnlyList<string> RiskAreaIds
    {
        get => _riskAreaIds;
        init => _riskAreaIds = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record LineDraftSnapshot(
    string StartNodeId,
    string LineClassId,
    string PoleClassId,
    IReadOnlyList<MapPoint> IntermediatePoints,
    string? EndNodeId)
{
    private IReadOnlyList<MapPoint> _intermediatePoints =
        Array.AsReadOnly(IntermediatePoints.ToArray());
    private IReadOnlyList<string> _riskAreaIds = Array.Empty<string>();

    public IReadOnlyList<MapPoint> IntermediatePoints
    {
        get => _intermediatePoints;
        init => _intermediatePoints = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> RiskAreaIds
    {
        get => _riskAreaIds;
        init => _riskAreaIds = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record ActiveConstructionSnapshot(
    ConstructionKind Kind,
    long CostCashUnit,
    long CompletionMinute,
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<string> EdgeIds)
{
    private IReadOnlyList<string> _nodeIds = Array.AsReadOnly(NodeIds.ToArray());
    private IReadOnlyList<string> _edgeIds = Array.AsReadOnly(EdgeIds.ToArray());
    private IReadOnlyList<string> _riskAreaIds = Array.Empty<string>();

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

    public IReadOnlyList<string> RiskAreaIds
    {
        get => _riskAreaIds;
        init => _riskAreaIds = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record ConstructionSnapshot(
    long Minute,
    ConstructionPhase Phase,
    SpatialWorldDefinition World,
    NodeDraftSnapshot? NodeDraft,
    LineDraftSnapshot? LineDraft,
    ActiveConstructionSnapshot? ActiveConstruction);

public sealed record ConstructionCommandResult(
    bool Accepted,
    ConstructionError? Error,
    ConstructionSnapshot Snapshot);

public sealed record NodePlacementPreview(
    bool Accepted,
    ConstructionError? Error,
    string NodeClassId,
    MapPoint Position)
{
    private IReadOnlyList<string> _riskAreaIds = Array.Empty<string>();

    public IReadOnlyList<string> RiskAreaIds
    {
        get => _riskAreaIds;
        init => _riskAreaIds = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record LineStartPreview(
    bool Accepted,
    ConstructionError? Error,
    string StartNodeId,
    string LineClassId,
    string PoleClassId);

public sealed record LinePointPreview(
    bool Accepted,
    ConstructionError? Error,
    MapPoint Position,
    long? SegmentLengthUnit,
    int? MaxSpanUnit)
{
    private IReadOnlyList<string> _riskAreaIds = Array.Empty<string>();

    public IReadOnlyList<string> RiskAreaIds
    {
        get => _riskAreaIds;
        init => _riskAreaIds = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record LineFinishPreview(
    bool Accepted,
    ConstructionError? Error,
    string EndNodeId,
    long? SegmentLengthUnit,
    int? MaxSpanUnit)
{
    private IReadOnlyList<string> _riskAreaIds = Array.Empty<string>();

    public IReadOnlyList<string> RiskAreaIds
    {
        get => _riskAreaIds;
        init => _riskAreaIds = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record LinePointMovePreview(
    bool Accepted,
    ConstructionError? Error,
    int PointIndex,
    MapPoint Position,
    long? PreviousSegmentLengthUnit,
    long? NextSegmentLengthUnit,
    int? MaxSpanUnit)
{
    private IReadOnlyList<string> _riskAreaIds = Array.Empty<string>();

    public IReadOnlyList<string> RiskAreaIds
    {
        get => _riskAreaIds;
        init => _riskAreaIds = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record ConstructionQuote(
    bool Accepted,
    ConstructionError? Error,
    long? CostCashUnit,
    long? BuildMinutes,
    long? CompletionMinute)
{
    private IReadOnlyList<string> _riskAreaIds = Array.Empty<string>();

    public IReadOnlyList<string> RiskAreaIds
    {
        get => _riskAreaIds;
        init => _riskAreaIds = Array.AsReadOnly(value.ToArray());
    }
}
