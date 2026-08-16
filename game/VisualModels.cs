using System;
using System.Collections.Generic;

namespace Gridworks.Game;

internal readonly record struct MapPoint(float X, float Y);

internal sealed record MapNodeDefinition(string Id, MapNodeKind Kind, MapPoint Position);

internal enum MapNodeKind
{
    Generator,
    Bus,
    Substation,
    Town,
    Hospital,
}

internal sealed record MapEdgeDefinition(string Id, IReadOnlyList<MapPoint> Points, string DisplayName);

internal sealed record MapRiskArea(IReadOnlyList<MapPoint> Points);

internal sealed record MapServiceArea(MapPoint Center, float RadiusX, float RadiusY);

internal sealed record MapDefinition(
    float Width,
    float Height,
    IReadOnlyList<MapNodeDefinition> Nodes,
    IReadOnlyList<MapEdgeDefinition> Edges,
    IReadOnlyList<MapRiskArea> RiskAreas,
    IReadOnlyList<MapServiceArea> ServiceAreas,
    IReadOnlyDictionary<string, IReadOnlyList<string>> PathEdgeIds);

internal sealed record MapState(
    IReadOnlySet<string> CommissionedEdgeIds,
    IReadOnlySet<string> BuildingEdgeIds,
    IReadOnlySet<string> RemovedEdgeIds,
    IReadOnlySet<string> EnergizedEdgeIds,
    string? SelectedCorridorEdgeId,
    bool TownUtilityDelivered,
    bool HospitalUtilityDelivered,
    string HospitalP0Source);

internal sealed record TimelineMarker(long Minute, TimelineMarkerKind Kind, string Text);

internal enum TimelineMarkerKind
{
    Current,
    Construction,
    Deadline,
    EventStart,
    Recovery,
}
