using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Game.Realtime.UI;
using Godot;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

/// <summary>
/// Authoritative, renderer-neutral world state. A world renderer consumes this DTO instead of
/// reaching into the Core campaign snapshot or the rest of the slice presentation.
/// </summary>
internal sealed record RealtimeWorldPresentation(
    SpatialWorldDefinition World,
    IReadOnlyList<RealtimeWorldAssetStatus> AssetStatuses,
    IReadOnlyList<RealtimeWorldServiceArea> ServiceAreas,
    RealtimeWorldDraftPresentation Draft,
    bool PlacementMode,
    string? SelectedAssetId,
    bool AnalysisVisible,
    RealtimeWorldWeather Weather,
    long Minute,
    IReadOnlyList<string> ForecastRiskAreaIds,
    IReadOnlyList<string> ActiveRiskAreaIds,
    RealtimeWorldHighlight? Highlight,
    bool ReduceMotion,
    RealtimeTool Tool,
    RealtimeSurface Surface,
    string ChapterId,
    IReadOnlyList<string> CompatibleLineNodeIds,
    RealtimeWorldGuidanceTarget? GuidanceTarget,
    RealtimeWorldPlacementClass? PlacementClass)
{
    private IReadOnlyList<RealtimeWorldAssetStatus> _assetStatuses =
        Freeze(AssetStatuses);
    private IReadOnlyList<RealtimeWorldServiceArea> _serviceAreas = Freeze(ServiceAreas);
    private IReadOnlyList<string> _forecastRiskAreaIds = Freeze(ForecastRiskAreaIds);
    private IReadOnlyList<string> _activeRiskAreaIds = Freeze(ActiveRiskAreaIds);
    private IReadOnlyList<string> _compatibleLineNodeIds =
        Freeze(CompatibleLineNodeIds);

    public IReadOnlyList<RealtimeWorldAssetStatus> AssetStatuses
    {
        get => _assetStatuses;
        init => _assetStatuses = Freeze(value);
    }

    public IReadOnlyList<RealtimeWorldServiceArea> ServiceAreas
    {
        get => _serviceAreas;
        init => _serviceAreas = Freeze(value);
    }

    public IReadOnlyList<string> ActiveRiskAreaIds
    {
        get => _activeRiskAreaIds;
        init => _activeRiskAreaIds = Freeze(value);
    }

    public IReadOnlyList<string> ForecastRiskAreaIds
    {
        get => _forecastRiskAreaIds;
        init => _forecastRiskAreaIds = Freeze(value);
    }

    public IReadOnlyList<string> CompatibleLineNodeIds
    {
        get => _compatibleLineNodeIds;
        init => _compatibleLineNodeIds = Freeze(value);
    }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

internal sealed record RealtimeWorldGuidanceTarget(string NodeId, string Label);

internal sealed record RealtimeWorldServiceArea(
    string NodeId,
    int RadiusUnit,
    int FootprintRadiusUnit,
    string ClassDisplayName);

internal sealed record RealtimeWorldPlacementClass(
    string ClassId,
    string DisplayName,
    int FootprintRadiusUnit,
    int ServiceRadiusUnit);

/// <summary>
/// Pointer-only feedback. Updating it must not require a Core snapshot, forecast, or a complete
/// UI/world presentation rebuild.
/// </summary>
internal sealed record RealtimeWorldPointerFeedback(
    CoreMapPoint? Point,
    bool Accepted,
    string Message)
{
    internal static RealtimeWorldPointerFeedback Empty { get; } = new(null, true, string.Empty);
}

internal enum RealtimeWorldAssetState
{
    Normal,
    Planned,
    Building,
    AuthoredUnavailable,
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
    int EmergencyExposureLimitMinutes,
    bool AuthoredUnavailable = false,
    bool ProtectiveOutage = false);

internal sealed record RealtimeWorldHighlight(
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<string> EdgeIds,
    string? LimitingAssetId,
    string AccessibilitySummary,
    RealtimeWorldServiceLink? ServiceLink = null)
{
    private IReadOnlyList<string> _nodeIds = Freeze(NodeIds);
    private IReadOnlyList<string> _edgeIds = Freeze(EdgeIds);

    public IReadOnlyList<string> NodeIds
    {
        get => _nodeIds;
        init => _nodeIds = Freeze(value);
    }

    public IReadOnlyList<string> EdgeIds
    {
        get => _edgeIds;
        init => _edgeIds = Freeze(value);
    }

    private static IReadOnlyList<string> Freeze(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

internal sealed record RealtimeWorldServiceLink(
    string SubstationNodeId,
    string LoadNodeId,
    int RadiusUnit,
    int DistanceUnit,
    bool Supplied);

internal sealed record RealtimeWorldDraftHandle(string Id, CoreMapPoint Point);

internal sealed record RealtimeWorldDraftPresentation(
    IReadOnlyList<RealtimeWorldDraftHandle> Handles,
    IReadOnlyList<CoreMapPoint> LinePath,
    bool ExtendLineToPointer,
    string? NodeClassId)
{
    private IReadOnlyList<RealtimeWorldDraftHandle> _handles = Freeze(Handles);
    private IReadOnlyList<CoreMapPoint> _linePath = Freeze(LinePath);

    public IReadOnlyList<RealtimeWorldDraftHandle> Handles
    {
        get => _handles;
        init => _handles = Freeze(value);
    }

    public IReadOnlyList<CoreMapPoint> LinePath
    {
        get => _linePath;
        init => _linePath = Freeze(value);
    }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

internal enum RealtimePointerOwner
{
    EmptyTerrain,
    WorldCandidate,
    SelectionAction,
    DraftHandle,
    Hud,
    BlockingModal,
    Fatal,
}

internal enum RealtimeMapCandidateKind
{
    Node,
    Edge,
    DraftHandle,
    SelectionAction,
    EmptyTerrain,
}

internal sealed record RealtimeMapCandidate(
    string Id,
    RealtimeMapCandidateKind Kind,
    RealtimePointerOwner Owner,
    double DistanceSquared);

internal sealed record RealtimePointerProbe(
    string Id,
    CoreMapPoint WorldPoint,
    IReadOnlyList<RealtimeMapCandidate> Candidates,
    bool HudHit = false,
    bool BlockingModalHit = false,
    bool FatalHit = false,
    bool OverlayVisible = false,
    bool WeatherVisible = false)
{
    private IReadOnlyList<RealtimeMapCandidate> _candidates = Freeze(Candidates);

    public IReadOnlyList<RealtimeMapCandidate> Candidates
    {
        get => _candidates;
        init => _candidates = Freeze(value);
    }

    private static IReadOnlyList<RealtimeMapCandidate> Freeze(
        IReadOnlyList<RealtimeMapCandidate> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

internal sealed record RealtimePointerResolution(
    string ProbeId,
    RealtimePointerOwner Owner,
    string? ResolvedId,
    IReadOnlyList<RealtimeMapCandidate> OrderedCandidates,
    IReadOnlyList<string> OrderedWorldCandidateIds)
{
    private IReadOnlyList<RealtimeMapCandidate> _orderedCandidates =
        Array.AsReadOnly(OrderedCandidates.ToArray());
    private IReadOnlyList<string> _orderedWorldCandidateIds =
        Array.AsReadOnly(OrderedWorldCandidateIds.ToArray());

    public IReadOnlyList<RealtimeMapCandidate> OrderedCandidates
    {
        get => _orderedCandidates;
        init => _orderedCandidates = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> OrderedWorldCandidateIds
    {
        get => _orderedWorldCandidateIds;
        init => _orderedWorldCandidateIds = Array.AsReadOnly(value.ToArray());
    }

    internal static RealtimePointerResolution Empty(string probeId) => new(
        probeId,
        RealtimePointerOwner.EmptyTerrain,
        null,
        Array.Empty<RealtimeMapCandidate>(),
        Array.Empty<string>());
}

internal readonly record struct RealtimeMapCameraSnapshot(Vector2 Center, int ZoomIndex);

internal interface IRealtimeWorldPresentationView
{
    void SetPresentation(RealtimeWorldPresentation presentation);
    void SetPointerFeedback(RealtimeWorldPointerFeedback feedback);
}

internal interface IRealtimeWorldInteractionView
{
    event Action<RealtimePointerResolution, CoreMapPoint>? PrimaryRequested;
    event Action<RealtimePointerResolution, CoreMapPoint>? PointerMoved;
    event Action? CancelRequested;

    bool IsPanning { get; }
    Rect2 InteractionRect { get; }

    void SetInteractionRect(Rect2 rect, RealtimeLayoutProfile profile);
    void CycleCandidate(int delta);
    void BeginPan();
    void EndPan();
    void ConfirmCurrentCandidate();
    void RequestFocus();
}

internal interface IRealtimeWorldCameraView
{
    Vector2 CameraCenter { get; }
    RealtimeMapCameraSnapshot CaptureCamera();
    void RestoreCamera(RealtimeMapCameraSnapshot camera);
}

internal interface IRealtimeWorldView :
    IRealtimeWorldPresentationView,
    IRealtimeWorldInteractionView,
    IRealtimeWorldCameraView;

internal static class RealtimeWorldIds
{
    private const string SelectionActionPrefix = "ACTION:INSPECT:";
    private const string DraftPointPrefix = "DRAFT_POINT:";

    internal const string DraftNode = "DRAFT_NODE";

    internal static string SelectionAction(string assetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        return SelectionActionPrefix + assetId;
    }

    internal static bool TryParseSelectionAction(string? value, out string assetId)
    {
        if (value is not null && value.StartsWith(
                SelectionActionPrefix,
                StringComparison.Ordinal))
        {
            assetId = value[SelectionActionPrefix.Length..];
            return assetId.Length > 0;
        }
        assetId = string.Empty;
        return false;
    }

    internal static string DraftPoint(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return $"{DraftPointPrefix}{index}";
    }

    internal static bool IsDraftPoint(string? value) => value?.StartsWith(
        DraftPointPrefix,
        StringComparison.Ordinal) == true;
}

internal static class RealtimeWorldProbeIds
{
    internal const string Hover = "MAP_HOVER";
    internal const string Primary = "MAP_PRIMARY";
    internal const string PresentationRefresh = "MAP_PRESENTATION_REFRESH";
    internal const string LayoutRefresh = "MAP_LAYOUT_REFRESH";
    internal const string KeyboardChooser = "MAP_KEYBOARD_CHOOSER";
    internal const string KeyboardConfirm = "MAP_KEYBOARD_CONFIRM";
    internal const string CameraRestore = "MAP_CAMERA_RESTORE";
    internal const string ZoomRefresh = "MAP_ZOOM_REFRESH";
    internal const string TransformRefresh = "MAP_TRANSFORM_REFRESH";
    internal const string KeyboardDefault = "MAP_KEYBOARD_DEFAULT";
    internal const string SelectionTarget = "MAP_SELECTION_TARGET";
}

internal static class RealtimePointerOwnerResolver
{
    internal static RealtimePointerResolution Resolve(
        RealtimePointerProbe probe,
        IReadOnlyList<string>? preferredWorldNodeIds = null)
    {
        ArgumentNullException.ThrowIfNull(probe);
        RealtimeMapCandidate[] ordered = probe.Candidates
            .Where(item => item.Kind != RealtimeMapCandidateKind.EmptyTerrain)
            .OrderByDescending(item => Priority(item.Owner))
            .ThenByDescending(item => item.Kind == RealtimeMapCandidateKind.Node &&
                preferredWorldNodeIds?.Contains(item.Id, StringComparer.Ordinal) == true)
            .ThenBy(item => item.DistanceSquared)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        RealtimeMapCandidate[] worldCandidates = ordered
            .Where(item => item.Owner == RealtimePointerOwner.WorldCandidate)
            .ToArray();
        RealtimeMapCandidate[] compatibleNodes = worldCandidates
            .Where(item => item.Kind == RealtimeMapCandidateKind.Node &&
                preferredWorldNodeIds?.Contains(item.Id, StringComparer.Ordinal) == true)
            .ToArray();
        string[] worldIds = (compatibleNodes.Length > 0
                ? compatibleNodes
                : worldCandidates)
            .Select(item => item.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (probe.FatalHit)
        {
            return Forced(probe, RealtimePointerOwner.Fatal, "FATAL", ordered, worldIds);
        }
        if (probe.BlockingModalHit)
        {
            return Forced(
                probe,
                RealtimePointerOwner.BlockingModal,
                "BLOCKING_MODAL",
                ordered,
                worldIds);
        }
        if (probe.HudHit)
        {
            return Forced(probe, RealtimePointerOwner.Hud, "HUD", ordered, worldIds);
        }
        RealtimeMapCandidate? resolved = ordered.FirstOrDefault();
        return resolved is null
            ? RealtimePointerResolution.Empty(probe.Id)
            : new RealtimePointerResolution(
                probe.Id,
                resolved.Owner,
                resolved.Id,
                Array.AsReadOnly(ordered),
                Array.AsReadOnly(worldIds));
    }

    private static RealtimePointerResolution Forced(
        RealtimePointerProbe probe,
        RealtimePointerOwner owner,
        string id,
        IReadOnlyList<RealtimeMapCandidate> ordered,
        IReadOnlyList<string> worldIds) => new(
        probe.Id,
        owner,
        id,
        ordered,
        worldIds);

    private static int Priority(RealtimePointerOwner owner) => owner switch
    {
        RealtimePointerOwner.Fatal => (int)RealtimeInputPriority.Fatal,
        RealtimePointerOwner.BlockingModal => (int)RealtimeInputPriority.BlockingModal,
        RealtimePointerOwner.Hud => (int)RealtimeInputPriority.Hud,
        RealtimePointerOwner.DraftHandle => (int)RealtimeInputPriority.DraftHandle,
        RealtimePointerOwner.SelectionAction => (int)RealtimeInputPriority.SelectionAction,
        RealtimePointerOwner.WorldCandidate => (int)RealtimeInputPriority.WorldCandidate,
        RealtimePointerOwner.EmptyTerrain => (int)RealtimeInputPriority.EmptyTerrain,
        _ => throw new ArgumentOutOfRangeException(nameof(owner)),
    };
}
