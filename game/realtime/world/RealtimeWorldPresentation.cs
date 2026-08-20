using System;
using System.Collections.Generic;
using Gridworks.Core.Release.V2;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime;

internal enum RealtimeWorldAssetState
{
    Normal,
    Planned,
    Building,
    Emergency,
    ProtectiveOutage,
    OverLimit,
}

internal enum RealtimeWorldWeather
{
    Clear,
    Heat,
    Rain,
    Storm,
}

internal sealed record RealtimeWorldAssetStatus(
    string AssetId,
    RealtimeWorldAssetState State,
    long UsedKw,
    long ContinuousLimitKw,
    long EmergencyLimitKw,
    int EmergencyExposureMinutes,
    int EmergencyExposureLimitMinutes);

internal sealed record RealtimeWorldHighlight(
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<string> EdgeIds,
    string? LimitingAssetId,
    string AccessibilitySummary);

internal sealed record RealtimeWorldPresentation(
    SpatialWorldDefinition World,
    IReadOnlyList<RealtimeWorldAssetStatus> AssetStatuses,
    CoreMapPoint? PointerPoint,
    bool PointerAccepted,
    string PointerMessage,
    bool PlacementMode,
    string? SelectedAssetId,
    bool AnalysisVisible,
    RealtimeWorldWeather Weather,
    long Minute,
    IReadOnlyList<string> ActiveRiskAreaIds,
    RealtimeWorldHighlight? Highlight,
    bool ReduceMotion)
{
    public static RealtimeWorldPresentation Empty(SpatialWorldDefinition world) => new(
        world,
        Array.Empty<RealtimeWorldAssetStatus>(),
        null,
        true,
        string.Empty,
        false,
        null,
        false,
        RealtimeWorldWeather.Clear,
        0,
        Array.Empty<string>(),
        null,
        false);
}
