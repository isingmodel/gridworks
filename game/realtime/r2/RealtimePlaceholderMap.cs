using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;
using Godot;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

internal enum RealtimePlaceholderAssetState
{
    Normal,
    Planned,
    Building,
    AuthoredUnavailable,
    Emergency,
    ProtectiveOutage,
    OverLimit,
}

internal enum RealtimePlaceholderWeather
{
    Clear,
    Heat,
    Rain,
    Storm,
}

internal enum RealtimePlaceholderStateCue
{
    None,
    AuthoredUnavailableBars,
    EmergencyTriangle,
    ProtectiveOutageCross,
    OverLimitDiamond,
}

internal sealed record RealtimePlaceholderAssetStatus(
    string AssetId,
    RealtimePlaceholderAssetState State,
    long UsedKw,
    long ContinuousLimitKw,
    long EmergencyLimitKw,
    int EmergencyExposureMinutes,
    int EmergencyExposureLimitMinutes,
    bool AuthoredUnavailable = false,
    bool ProtectiveOutage = false);

internal sealed record RealtimePlaceholderHighlight(
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<string> EdgeIds,
    string? LimitingAssetId,
    string AccessibilitySummary)
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

    private static IReadOnlyList<string> Freeze(IReadOnlyList<string> values) =>
        Array.AsReadOnly(values.ToArray());
}

internal sealed record RealtimePlaceholderMapPresentation(
    SpatialWorldDefinition World,
    IReadOnlyList<RealtimePlaceholderAssetStatus> AssetStatuses,
    CoreMapPoint? PointerPoint,
    bool PointerAccepted,
    string PointerMessage,
    bool PlacementMode,
    string? SelectedAssetId,
    bool AnalysisVisible,
    RealtimePlaceholderWeather Weather,
    long Minute,
    IReadOnlyList<string> ActiveRiskAreaIds,
    RealtimePlaceholderHighlight? Highlight,
    bool ReduceMotion)
{
    private IReadOnlyList<RealtimePlaceholderAssetStatus> _assetStatuses =
        Array.AsReadOnly(AssetStatuses.ToArray());
    private IReadOnlyList<string> _activeRiskAreaIds =
        Array.AsReadOnly(ActiveRiskAreaIds.ToArray());

    public IReadOnlyList<RealtimePlaceholderAssetStatus> AssetStatuses
    {
        get => _assetStatuses;
        init => _assetStatuses = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> ActiveRiskAreaIds
    {
        get => _activeRiskAreaIds;
        init => _activeRiskAreaIds = Array.AsReadOnly(value.ToArray());
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
    private IReadOnlyList<RealtimeMapCandidate> _candidates =
        Array.AsReadOnly(Candidates.ToArray());

    public IReadOnlyList<RealtimeMapCandidate> Candidates
    {
        get => _candidates;
        init => _candidates = Array.AsReadOnly(value.ToArray());
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

internal readonly record struct RealtimeMapCameraSnapshot(
    Vector2 Center,
    int ZoomIndex);

internal static class RealtimePointerOwnerResolver
{
    internal static RealtimePointerResolution Resolve(RealtimePointerProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        RealtimeMapCandidate[] ordered = probe.Candidates
            .Where(item => item.Kind != RealtimeMapCandidateKind.EmptyTerrain)
            .OrderByDescending(item => Priority(item.Owner))
            .ThenBy(item => item.DistanceSquared)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        string[] worldIds = ordered
            .Where(item => item.Owner == RealtimePointerOwner.WorldCandidate)
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

/// <summary>
/// R2-only code-native map. It deliberately renders geometry and typed state without using
/// production artwork; the R1 simulation snapshot remains its sole world authority.
/// </summary>
internal sealed partial class RealtimePlaceholderMap : Control
{
    private static readonly Color Ground = Color.FromHtml("26342e");
    private static readonly Color Grid = Color.FromHtml("34463d");
    private static readonly Color Normal = Color.FromHtml("78c7b9");
    private static readonly Color Planned = Color.FromHtml("d5b45c");
    private static readonly Color Emergency = Color.FromHtml("ed964d");
    private static readonly Color Outage = Color.FromHtml("b9bfbc");
    private static readonly Color Danger = Color.FromHtml("ec6f68");
    private static readonly Color Selected = Color.FromHtml("90e2d4");
    private static readonly Color Candidate = Color.FromHtml("f4d58a");
    private static readonly Color Text = Color.FromHtml("eef5f0");

    private RealtimeSlicePresentation? _presentation;
    private CommercialMapTransform? _transform;
    private CoreMapPoint? _pointer;
    private IReadOnlyList<string> _candidateCycle = Array.Empty<string>();
    private int _candidateIndex;
    private string? _preferredCandidateId;
    private bool _panning;
    private Vector2 _lastCanvasPointer;
    private bool _hasCanvasPointer;
    private string? _lastFollowSelectionId;
    private float _accessibilityScale = 1f;
    private float _minimumPointerHitRadius = 22f;
#if DEBUG
    private readonly Dictionary<string, RealtimePlaceholderStateCue> _drawnStateCues =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _drawnAnalysisRiskAreaIds =
        new(StringComparer.Ordinal);
    private string? _drawnActiveCandidateId;
    private bool _drawnAnalysisOverlay;
#endif

    internal event Action<RealtimePointerResolution, CoreMapPoint>? PrimaryRequested;
    internal event Action<RealtimePointerResolution, CoreMapPoint>? PointerMoved;
    internal event Action? CancelRequested;

    internal string ZoomLabel => _transform?.ZoomLabel ?? "지역 보기";
    internal Vector2 CameraCenter => _transform?.Center ?? Vector2.Zero;
    internal bool IsPanning => _panning;

    internal int LabelFontSize => Math.Max(1, Mathf.RoundToInt(12f * _accessibilityScale));

    private string? ActiveCandidateId =>
        _candidateCycle.Count > 0 &&
        _candidateIndex >= 0 &&
        _candidateIndex < _candidateCycle.Count
            ? _candidateCycle[_candidateIndex]
            : null;

    private string ActiveCandidateVisibleLabel =>
        ActiveCandidateId is string candidateId && _presentation is not null
            ? $"후보 {_candidateIndex + 1}/{_candidateCycle.Count} · " +
              CandidateDisplayName(_presentation.World, candidateId)
            : string.Empty;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        ClipContents = true;
        AccessibilityName = "청류시 실시간 전력망";
        AccessibilityDescription =
            "설비와 선로 후보를 거리와 안정된 순서로 정렬하며 장식과 날씨는 클릭을 받지 않습니다.";
        Resized += ConfigureTransform;
    }

    internal void SetPresentation(RealtimeSlicePresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        _presentation = presentation;
        ConfigureTransform();
        EnsureKeyboardCursor();
        FollowSelection();
        _ = RefreshPointerResolution("MAP_PRESENTATION_REFRESH");
        UpdateAccessibility();
        QueueRedraw();
    }

    internal void ApplyLayout(RealtimeLayoutProfile profile)
    {
        _accessibilityScale = Math.Max(1f, profile.AccessibilityScale);
        _minimumPointerHitRadius = Math.Max(20f, profile.MinimumHitTarget / 2f);
        _ = RefreshPointerResolution("MAP_LAYOUT_REFRESH");
        QueueRedraw();
    }

    internal void CycleCandidate(int delta)
    {
        if (delta == 0 || !_hasCanvasPointer || _transform is null)
        {
            return;
        }
        RealtimePointerResolution? resolution = RefreshPointerResolution(
            "MAP_KEYBOARD_CHOOSER");
        // Selection actions and draft handles own the point above any world
        // candidates underneath them. Q/E must not announce or cycle an
        // obscured candidate that Enter cannot actually activate.
        if (resolution is null ||
            resolution.Owner != RealtimePointerOwner.WorldCandidate ||
            _candidateCycle.Count == 0)
        {
            return;
        }
        _candidateIndex = ((_candidateIndex + delta) % _candidateCycle.Count +
            _candidateCycle.Count) % _candidateCycle.Count;
        _preferredCandidateId = ActiveCandidateId;
        UpdateAccessibility();
        QueueRedraw();
    }

    internal void BeginPan()
    {
        _panning = true;
        MouseDefaultCursorShape = CursorShape.Drag;
    }

    internal void EndPan()
    {
        _panning = false;
        MouseDefaultCursorShape = CursorShape.Arrow;
    }

    internal void ConfirmCurrentCandidate()
    {
        if (_presentation is null || _transform is null || !_hasCanvasPointer)
        {
            return;
        }
        RealtimePointerResolution? resolution = RefreshPointerResolution(
            "MAP_KEYBOARD_CONFIRM");
        if (resolution is not null && _pointer is CoreMapPoint point)
        {
            PrimaryRequested?.Invoke(resolution, point);
        }
    }

    internal RealtimeMapCameraSnapshot CaptureCamera() => new(
        _transform?.Center ?? Vector2.Zero,
        _transform?.ZoomIndex ?? 0);

    internal void RestoreCamera(RealtimeMapCameraSnapshot camera)
    {
        if (_transform is null)
        {
            return;
        }
        _transform.Home();
        _transform.SetZoomAt(camera.ZoomIndex, _transform.PlotRect.GetCenter());
        Vector2 current = _transform.Center;
        _transform.PanByCanvasDelta(
            new Vector2(current.X - camera.Center.X, current.Y - camera.Center.Y) *
            (float)_transform.Scale);
        _ = RefreshPointerResolution("MAP_CAMERA_RESTORE");
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (_presentation is null || _transform is null)
        {
            return;
        }
        switch (inputEvent)
        {
            case InputEventMouseMotion motion when _panning:
                if (_hasCanvasPointer)
                {
                    _transform.PanByCanvasDelta(motion.Position - _lastCanvasPointer);
                }
                _hasCanvasPointer = true;
                _lastCanvasPointer = motion.Position;
                _pointer = ToWorld(motion.Position);
                RealtimePointerResolution panResolution = ResolveCanvasPoint(
                    "MAP_HOVER",
                    motion.Position,
                    _pointer.Value);
                PointerMoved?.Invoke(panResolution, _pointer.Value);
                UpdateAccessibility();
                QueueRedraw();
                AcceptEvent();
                break;
            case InputEventMouseMotion motion:
                _hasCanvasPointer = true;
                _lastCanvasPointer = motion.Position;
                _pointer = ToWorld(motion.Position);
                RealtimePointerResolution hoverResolution = ResolveCanvasPoint(
                    "MAP_HOVER",
                    motion.Position,
                    _pointer.Value);
                PointerMoved?.Invoke(hoverResolution, _pointer.Value);
                UpdateAccessibility();
                QueueRedraw();
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.Left &&
                mouse.Pressed:
                CoreMapPoint worldPoint = ToWorld(mouse.Position);
                _pointer = worldPoint;
                _lastCanvasPointer = mouse.Position;
                _hasCanvasPointer = true;
                RealtimePointerResolution resolution = ResolveCanvasPoint(
                    "MAP_PRIMARY",
                    mouse.Position,
                    worldPoint);
                PrimaryRequested?.Invoke(resolution, worldPoint);
                AcceptEvent();
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.Right &&
                mouse.Pressed:
                CancelRequested?.Invoke();
                AcceptEvent();
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.WheelUp &&
                mouse.Pressed:
                _transform.SetZoomAt(_transform.ZoomIndex + 1, mouse.Position);
                _ = RefreshPointerResolution("MAP_ZOOM_REFRESH");
                QueueRedraw();
                AcceptEvent();
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.WheelDown &&
                mouse.Pressed:
                _transform.SetZoomAt(_transform.ZoomIndex - 1, mouse.Position);
                _ = RefreshPointerResolution("MAP_ZOOM_REFRESH");
                QueueRedraw();
                AcceptEvent();
                break;
            // Keyboard commands deliberately remain unhandled here. The
            // priority-aware RealtimeInputRouter is their sole owner, so a
            // focused map cannot bypass a blocking modal/HUD context or reduce
            // one physical key twice. The controller invokes CycleCandidate,
            // ConfirmCurrentCandidate, and the analysis intent after routing.
        }
    }

    public override void _Draw()
    {
#if DEBUG
        _drawnStateCues.Clear();
        _drawnAnalysisRiskAreaIds.Clear();
        _drawnActiveCandidateId = null;
        _drawnAnalysisOverlay = false;
#endif
        DrawRect(new Rect2(Vector2.Zero, Size), Ground);
        DrawGrid();
        if (_presentation is null || _transform is null)
        {
            return;
        }
        if (_presentation.World.AnalysisVisible)
        {
#if DEBUG
            _drawnAnalysisOverlay = true;
#endif
            DrawRiskAreas(_presentation.World);
        }
        DrawEdges(_presentation.World);
        DrawNodes(_presentation.World);
        DrawActiveCandidate(_presentation.World);
        DrawSelectionAction(_presentation.World);
        DrawDraft(_presentation);
        DrawPointer(_presentation.World);
    }

    internal RealtimePointerResolution ResolveWorldProbe(RealtimePointerProbe probe) =>
        RealtimePointerOwnerResolver.Resolve(probe);

    private RealtimePointerResolution ResolveCanvasPoint(
        string probeId,
        Vector2 canvasPoint,
        CoreMapPoint worldPoint)
    {
        string? previousCandidateId = ActiveCandidateId ?? _preferredCandidateId;
        RealtimeMapCandidate[] candidates = Candidates(canvasPoint).ToArray();
        RealtimePointerResolution resolution = RealtimePointerOwnerResolver.Resolve(
            new RealtimePointerProbe(
                probeId,
                worldPoint,
                Array.AsReadOnly(candidates),
                BlockingModalHit: _presentation!.Interaction.Surface ==
                    RealtimeSurface.BlockingModal,
                OverlayVisible: _presentation.World.AnalysisVisible,
                WeatherVisible: _presentation.World.Weather != RealtimePlaceholderWeather.Clear));
        bool candidateIdIsConfirmable = _presentation!.Interaction.Tool is
            RealtimeTool.Inspect or RealtimeTool.Analysis or RealtimeTool.BuildLine;
        if (resolution.OrderedWorldCandidateIds.Count == 0 ||
            resolution.Owner != RealtimePointerOwner.WorldCandidate ||
            !candidateIdIsConfirmable)
        {
            _candidateCycle = Array.Empty<string>();
            _candidateIndex = 0;
            if (resolution.Owner != RealtimePointerOwner.BlockingModal)
            {
                _preferredCandidateId = null;
            }
            UpdateAccessibility();
            return resolution;
        }
        string[] candidateIds = resolution.OrderedWorldCandidateIds.ToArray();
        _candidateCycle = Array.AsReadOnly(candidateIds);
        int retainedIndex = previousCandidateId is null
            ? -1
            : Array.FindIndex(candidateIds, id => string.Equals(
                id,
                previousCandidateId,
                StringComparison.Ordinal));
        _candidateIndex = retainedIndex >= 0 ? retainedIndex : 0;
        string selected = _candidateCycle[_candidateIndex];
        _preferredCandidateId = selected;
        RealtimeMapCandidate chosen = resolution.OrderedCandidates.Single(item =>
            string.Equals(item.Id, selected, StringComparison.Ordinal));
        UpdateAccessibility();
        return resolution with
        {
            Owner = chosen.Owner,
            ResolvedId = chosen.Id,
        };
    }

    /// <summary>
    /// The keyboard/mouse target is stored in world coordinates. Whenever a
    /// responsive surface, modal, zoom, or camera change rebuilds the canvas
    /// transform, reproject that same world point and recompute the exact hit
    /// owner. This keeps the visible candidate badge and Enter on one authority.
    /// </summary>
    private RealtimePointerResolution? RefreshPointerResolution(string probeId)
    {
        if (!_hasCanvasPointer || _pointer is not CoreMapPoint worldPoint ||
            _presentation is null || _transform is null)
        {
            return null;
        }
        _lastCanvasPointer = Point(worldPoint);
        RealtimePointerResolution resolution = ResolveCanvasPoint(
            probeId,
            _lastCanvasPointer,
            worldPoint);
        QueueRedraw();
        return resolution;
    }

    private IEnumerable<RealtimeMapCandidate> Candidates(Vector2 canvasPoint)
    {
        RealtimeCampaignSnapshot snapshot = _presentation!.CoreSnapshot;
        if (!_presentation.World.PlacementMode &&
            SelectionActionPoint(_presentation.World) is
            (string selectedAssetId, Vector2 actionPoint))
        {
            double actionDistance = actionPoint.DistanceSquaredTo(canvasPoint);
            double actionRadius = Math.Max(
                18f * _accessibilityScale,
                _minimumPointerHitRadius);
            if (actionDistance <= actionRadius * actionRadius)
            {
                yield return new RealtimeMapCandidate(
                    $"ACTION:INSPECT:{selectedAssetId}",
                    RealtimeMapCandidateKind.SelectionAction,
                    RealtimePointerOwner.SelectionAction,
                    actionDistance);
            }
        }
        foreach ((CoreMapPoint point, string id) in DraftHandles(snapshot))
        {
            double distance = Point(point).DistanceSquaredTo(canvasPoint);
            double hitRadius = Math.Max(
                24f * _accessibilityScale,
                _minimumPointerHitRadius);
            if (distance <= hitRadius * hitRadius)
            {
                yield return new RealtimeMapCandidate(
                    id,
                    RealtimeMapCandidateKind.DraftHandle,
                    RealtimePointerOwner.DraftHandle,
                    distance);
            }
        }
        foreach (SpatialNodeDefinition node in snapshot.Construction.World.Nodes)
        {
            double distance = Point(node.Position).DistanceSquaredTo(canvasPoint);
            double hitRadius = Math.Max(
                36f * _accessibilityScale,
                _minimumPointerHitRadius);
            if (distance <= hitRadius * hitRadius)
            {
                yield return new RealtimeMapCandidate(
                    node.NodeId,
                    RealtimeMapCandidateKind.Node,
                    RealtimePointerOwner.WorldCandidate,
                    distance);
            }
        }
        foreach (SpatialEdgeDefinition edge in snapshot.Construction.World.Edges)
        {
            SpatialNodeDefinition from = snapshot.Construction.World.Nodes.Single(item =>
                string.Equals(item.NodeId, edge.FromNodeId, StringComparison.Ordinal));
            SpatialNodeDefinition to = snapshot.Construction.World.Nodes.Single(item =>
                string.Equals(item.NodeId, edge.ToNodeId, StringComparison.Ordinal));
            double distance = SegmentDistanceSquared(
                canvasPoint,
                Point(from.Position),
                Point(to.Position));
            double hitRadius = Math.Max(
                12f * _accessibilityScale,
                _minimumPointerHitRadius);
            if (distance <= hitRadius * hitRadius)
            {
                yield return new RealtimeMapCandidate(
                    edge.EdgeId,
                    RealtimeMapCandidateKind.Edge,
                    RealtimePointerOwner.WorldCandidate,
                    distance);
            }
        }
    }

    private void DrawGrid()
    {
        const int step = 52;
        for (int x = 0; x < Size.X; x += step)
        {
            DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), Grid with { A = 0.28f });
        }
        for (int y = 0; y < Size.Y; y += step)
        {
            DrawLine(new Vector2(0, y), new Vector2(Size.X, y), Grid with { A = 0.28f });
        }
    }

    private void DrawRiskAreas(RealtimePlaceholderMapPresentation presentation)
    {
        HashSet<string> active = presentation.ActiveRiskAreaIds.ToHashSet(StringComparer.Ordinal);
        foreach (SpatialRiskAreaDefinition risk in presentation.World.RiskAreas.Where(item =>
                     active.Contains(item.RiskAreaId)))
        {
            Vector2[] polygon = risk.Polygon.Select(Point).ToArray();
            DrawColoredPolygon(polygon, Danger with { A = 0.14f });
            DrawPolyline(
                [.. polygon, polygon[0]],
                Danger,
                2f * _accessibilityScale,
                true);
#if DEBUG
            _drawnAnalysisRiskAreaIds.Add(risk.RiskAreaId);
#endif
        }
    }

    private void DrawEdges(RealtimePlaceholderMapPresentation presentation)
    {
        HashSet<string> highlighted =
            presentation.Highlight?.EdgeIds.ToHashSet(StringComparer.Ordinal) ?? [];
        foreach (SpatialEdgeDefinition edge in presentation.World.Edges.OrderBy(item =>
                     item.EdgeId,
                     StringComparer.Ordinal))
        {
            SpatialNodeDefinition from = presentation.World.Nodes.Single(item =>
                string.Equals(item.NodeId, edge.FromNodeId, StringComparison.Ordinal));
            SpatialNodeDefinition to = presentation.World.Nodes.Single(item =>
                string.Equals(item.NodeId, edge.ToNodeId, StringComparison.Ordinal));
            RealtimePlaceholderAssetStatus? status = Status(presentation, edge.EdgeId);
            bool selected = string.Equals(
                presentation.SelectedAssetId,
                edge.EdgeId,
                StringComparison.Ordinal);
            Color color = selected
                ? Selected
                : edge.Commissioned ? StateColor(status?.State) : Planned;
            float width = (selected || highlighted.Contains(edge.EdgeId) ? 5f : 2.5f) *
                _accessibilityScale;
            DrawLine(Point(from.Position), Point(to.Position), color, width, true);
            if (!edge.Commissioned)
            {
                DrawDashedLine(Point(from.Position), Point(to.Position), Planned);
            }
            else
            {
                DrawEdgeStateCue(
                    edge.EdgeId,
                    Point(from.Position),
                    Point(to.Position),
                    status?.State);
            }
        }
    }

    private void DrawNodes(RealtimePlaceholderMapPresentation presentation)
    {
        HashSet<string> highlighted =
            presentation.Highlight?.NodeIds.ToHashSet(StringComparer.Ordinal) ?? [];
        foreach (SpatialNodeDefinition node in presentation.World.Nodes.OrderBy(item =>
                     item.NodeId,
                     StringComparer.Ordinal))
        {
            RealtimePlaceholderAssetStatus? status = Status(presentation, node.NodeId);
            bool selected = string.Equals(
                presentation.SelectedAssetId,
                node.NodeId,
                StringComparison.Ordinal);
            bool routeHighlighted = highlighted.Contains(node.NodeId);
            float radius = NodeRadius(presentation.World, node);
            Color color = node.Commissioned ? StateColor(status?.State) : Planned;
            Vector2 center = Point(node.Position);
            DrawCircle(center, radius, color);
            DrawCircle(
                center,
                radius + (selected || routeHighlighted ? 7 : 2) * _accessibilityScale,
                selected || routeHighlighted ? Selected : Ground,
                false,
                (selected || routeHighlighted ? 3 : 1) * _accessibilityScale,
                true);
            DrawNodeStateCue(node.NodeId, center, radius, status?.State);
            if (selected || _transform!.ZoomIndex > 0)
            {
                string statusText = StatusLabel(status);
                DrawString(
                    ThemeDB.FallbackFont,
                    center + new Vector2(
                        radius + (5f * _accessibilityScale),
                        4f * _accessibilityScale),
                    $"{node.DisplayName} · {statusText}",
                    HorizontalAlignment.Left,
                    -1,
                    LabelFontSize,
                    Text);
            }
        }
    }

    private void DrawActiveCandidate(RealtimePlaceholderMapPresentation presentation)
    {
        if (ActiveCandidateId is not string candidateId)
        {
            return;
        }
#if DEBUG
        _drawnActiveCandidateId = candidateId;
#endif
        Vector2 anchor;
        SpatialNodeDefinition? node = presentation.World.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, candidateId, StringComparison.Ordinal));
        if (node is not null)
        {
            anchor = Point(node.Position);
            float radius = NodeRadius(presentation.World, node) +
                11f * _accessibilityScale;
            DrawCircle(
                anchor,
                radius,
                Candidate,
                false,
                3f * _accessibilityScale,
                true);
        }
        else
        {
            SpatialEdgeDefinition? edge = presentation.World.Edges.FirstOrDefault(item =>
                string.Equals(item.EdgeId, candidateId, StringComparison.Ordinal));
            if (edge is null)
            {
                return;
            }
            SpatialNodeDefinition from = presentation.World.Nodes.Single(item =>
                string.Equals(item.NodeId, edge.FromNodeId, StringComparison.Ordinal));
            SpatialNodeDefinition to = presentation.World.Nodes.Single(item =>
                string.Equals(item.NodeId, edge.ToNodeId, StringComparison.Ordinal));
            Vector2 fromPoint = Point(from.Position);
            Vector2 toPoint = Point(to.Position);
            Vector2 axis = toPoint - fromPoint;
            Vector2 normal = axis.LengthSquared() > 0.001f
                ? new Vector2(axis.Y, -axis.X).Normalized()
                : Vector2.Up;
            float offset = 4f * _accessibilityScale;
            DrawLine(
                fromPoint + normal * offset,
                toPoint + normal * offset,
                Candidate,
                2f * _accessibilityScale,
                true);
            DrawLine(
                fromPoint - normal * offset,
                toPoint - normal * offset,
                Candidate,
                2f * _accessibilityScale,
                true);
            anchor = (fromPoint + toPoint) / 2f + normal * (10f * _accessibilityScale);
        }
        DrawActiveCandidateBadge(anchor, ActiveCandidateVisibleLabel);
    }

    private void DrawActiveCandidateBadge(Vector2 anchor, string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }
        int fontSize = Math.Max(LabelFontSize, Mathf.RoundToInt(13f * _accessibilityScale));
        Vector2 textSize = ThemeDB.FallbackFont.GetStringSize(
            label,
            HorizontalAlignment.Left,
            -1,
            fontSize);
        Vector2 padding = new(9f * _accessibilityScale, 6f * _accessibilityScale);
        Vector2 badgeSize = textSize + padding * 2f;
        Vector2 desired = anchor + new Vector2(12f, 12f) * _accessibilityScale;
        Vector2 position = new(
            Math.Clamp(
                desired.X,
                4f,
                Math.Max(4f, Size.X - badgeSize.X - 4f)),
            Math.Clamp(
                desired.Y,
                4f,
                Math.Max(4f, Size.Y - badgeSize.Y - 4f)));
        var badge = new Rect2(position, badgeSize);
        DrawRect(badge, Ground with { A = 0.96f });
        DrawRect(badge, Candidate, false, 2f * _accessibilityScale);
        DrawString(
            ThemeDB.FallbackFont,
            position + new Vector2(padding.X, padding.Y + textSize.Y * 0.78f),
            label,
            HorizontalAlignment.Left,
            -1,
            fontSize,
            Text);
    }

    private void DrawSelectionAction(RealtimePlaceholderMapPresentation presentation)
    {
        if (presentation.PlacementMode ||
            SelectionActionPoint(presentation) is not (_, Vector2 point))
        {
            return;
        }
        float radius = 11f * _accessibilityScale;
        DrawCircle(point, radius, Ground);
        DrawCircle(point, radius, Selected, false, 2f * _accessibilityScale, true);
        DrawString(
            ThemeDB.FallbackFont,
            point + new Vector2(-3.5f, 4.5f) * _accessibilityScale,
            "i",
            HorizontalAlignment.Left,
            -1,
            Math.Max(1, Mathf.RoundToInt(13f * _accessibilityScale)),
            Selected);
    }

    private (string AssetId, Vector2 Point)? SelectionActionPoint(
        RealtimePlaceholderMapPresentation presentation)
    {
        if (presentation.PlacementMode ||
            presentation.SelectedAssetId is not string selectedId ||
            _transform is null)
        {
            return null;
        }
        SpatialNodeDefinition? node = presentation.World.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, selectedId, StringComparison.Ordinal));
        if (node is not null)
        {
            float radius = NodeRadius(presentation.World, node);
            Vector2 direction = new Vector2(1f, -1f).Normalized();
            Vector2 raw = Point(node.Position) +
                direction * (radius + (24f * _accessibilityScale));
            return (selectedId, ClampSelectionAction(raw));
        }
        SpatialEdgeDefinition? edge = presentation.World.Edges.FirstOrDefault(item =>
            string.Equals(item.EdgeId, selectedId, StringComparison.Ordinal));
        if (edge is null)
        {
            return null;
        }
        SpatialNodeDefinition from = presentation.World.Nodes.Single(item =>
            string.Equals(item.NodeId, edge.FromNodeId, StringComparison.Ordinal));
        SpatialNodeDefinition to = presentation.World.Nodes.Single(item =>
            string.Equals(item.NodeId, edge.ToNodeId, StringComparison.Ordinal));
        Vector2 fromPoint = Point(from.Position);
        Vector2 toPoint = Point(to.Position);
        Vector2 axis = toPoint - fromPoint;
        Vector2 normal = axis.LengthSquared() > 0.001f
            ? new Vector2(axis.Y, -axis.X).Normalized()
            : new Vector2(1f, -1f).Normalized();
        Vector2 rawPoint = (fromPoint + toPoint) / 2f +
            normal * (24f * _accessibilityScale);
        return (selectedId, ClampSelectionAction(rawPoint));
    }

    private Vector2 ClampSelectionAction(Vector2 point)
    {
        float margin = 22f * _accessibilityScale;
        return new Vector2(
            Math.Clamp(point.X, margin, Math.Max(margin, Size.X - margin)),
            Math.Clamp(point.Y, margin, Math.Max(margin, Size.Y - margin)));
    }

    private void DrawDraft(RealtimeSlicePresentation presentation)
    {
        LineDraftSnapshot? draft = presentation.CoreSnapshot.Construction.LineDraft;
        if (draft is null)
        {
            return;
        }
        var points = new List<Vector2>
        {
            Point(presentation.World.World.Nodes.Single(item =>
                string.Equals(item.NodeId, draft.StartNodeId, StringComparison.Ordinal)).Position),
        };
        points.AddRange(draft.IntermediatePoints.Select(Point));
        if (draft.EndNodeId is not null)
        {
            points.Add(Point(presentation.World.World.Nodes.Single(item =>
                string.Equals(item.NodeId, draft.EndNodeId, StringComparison.Ordinal)).Position));
        }
        else if (_pointer is CoreMapPoint pointer)
        {
            points.Add(Point(pointer));
        }
        if (points.Count > 1)
        {
            DrawPolyline(points.ToArray(), Planned, 4f * _accessibilityScale, true);
        }
        foreach (Vector2 point in points)
        {
            DrawCircle(point, 7f * _accessibilityScale, Planned);
            DrawCircle(
                point,
                11f * _accessibilityScale,
                Selected,
                false,
                2f * _accessibilityScale,
                true);
        }
    }

    private void DrawPointer(RealtimePlaceholderMapPresentation presentation)
    {
        CoreMapPoint? pointer = presentation.PointerPoint ?? _pointer;
        if (pointer is not CoreMapPoint value ||
            !presentation.PlacementMode && !HasFocus())
        {
            return;
        }
        Vector2 center = Point(value);
        Color color = presentation.PlacementMode && !presentation.PointerAccepted
            ? Danger
            : Selected;
        float radius = (presentation.PlacementMode ? 12f : 8f) * _accessibilityScale;
        DrawCircle(center, radius, color, false, 2f * _accessibilityScale, true);
        float arm = 16f * _accessibilityScale;
        DrawLine(
            center + Vector2.Left * arm,
            center + Vector2.Right * arm,
            color,
            _accessibilityScale);
        DrawLine(
            center + Vector2.Up * arm,
            center + Vector2.Down * arm,
            color,
            _accessibilityScale);
    }

    private void DrawDashedLine(Vector2 from, Vector2 to, Color color)
    {
        const int segments = 12;
        for (int index = 0; index < segments; index += 2)
        {
            DrawLine(from.Lerp(to, index / (float)segments),
                from.Lerp(to, (index + 1) / (float)segments),
                color,
                2f * _accessibilityScale,
                true);
        }
    }

    private void DrawEdgeStateCue(
        string assetId,
        Vector2 from,
        Vector2 to,
        RealtimePlaceholderAssetState? state)
    {
        Vector2 axis = to - from;
        if (axis.LengthSquared() <= 0.001f)
        {
            return;
        }
        Vector2 normal = new Vector2(axis.Y, -axis.X).Normalized();
        Vector2 middle = (from + to) / 2f;
        float scale = _accessibilityScale;
        RealtimePlaceholderStateCue cue = StateCue(state);
#if DEBUG
        _drawnStateCues[assetId] = cue;
#endif
        switch (cue)
        {
            case RealtimePlaceholderStateCue.AuthoredUnavailableBars:
                DrawDashedLine(from, to, Text);
                DrawLine(
                    middle - normal * 6f * scale,
                    middle + normal * 6f * scale,
                    Planned,
                    3f * scale,
                    true);
                break;
            case RealtimePlaceholderStateCue.EmergencyTriangle:
                DrawLine(from + normal * 3f * scale, to + normal * 3f * scale,
                    Emergency, 1.5f * scale, true);
                DrawTriangle(middle, 6f * scale, Emergency);
                break;
            case RealtimePlaceholderStateCue.ProtectiveOutageCross:
                DrawDashedLine(from, to, Text);
                DrawX(middle, 7f * scale, Outage);
                break;
            case RealtimePlaceholderStateCue.OverLimitDiamond:
                DrawDiamond(middle, 7f * scale, Danger);
                break;
        }
    }

    private void DrawNodeStateCue(
        string assetId,
        Vector2 center,
        float radius,
        RealtimePlaceholderAssetState? state)
    {
        float scale = _accessibilityScale;
        Vector2 cueCenter = center + Vector2.Up * (radius + 7f * scale);
        RealtimePlaceholderStateCue cue = StateCue(state);
#if DEBUG
        _drawnStateCues[assetId] = cue;
#endif
        switch (cue)
        {
            case RealtimePlaceholderStateCue.AuthoredUnavailableBars:
                DrawLine(
                    center + new Vector2(-radius, radius) * 0.55f,
                    center + new Vector2(radius, -radius) * 0.55f,
                    Text,
                    2.5f * scale,
                    true);
                break;
            case RealtimePlaceholderStateCue.EmergencyTriangle:
                DrawTriangle(cueCenter, 6f * scale, Emergency);
                break;
            case RealtimePlaceholderStateCue.ProtectiveOutageCross:
                DrawX(center, Math.Max(radius * 0.72f, 5f * scale), Text);
                break;
            case RealtimePlaceholderStateCue.OverLimitDiamond:
                DrawDiamond(cueCenter, 6f * scale, Danger);
                break;
        }
    }

    private void DrawTriangle(Vector2 center, float radius, Color color)
    {
        Vector2[] points =
        [
            center + Vector2.Up * radius,
            center + new Vector2(0.866f, 0.5f) * radius,
            center + new Vector2(-0.866f, 0.5f) * radius,
        ];
        DrawPolyline([.. points, points[0]], color, 2f * _accessibilityScale, true);
    }

    private void DrawDiamond(Vector2 center, float radius, Color color)
    {
        Vector2[] points =
        [
            center + Vector2.Up * radius,
            center + Vector2.Right * radius,
            center + Vector2.Down * radius,
            center + Vector2.Left * radius,
        ];
        DrawPolyline([.. points, points[0]], color, 2f * _accessibilityScale, true);
    }

    private void DrawX(Vector2 center, float radius, Color color)
    {
        Vector2 diagonal = new(radius, radius);
        DrawLine(center - diagonal, center + diagonal, color,
            2f * _accessibilityScale, true);
        Vector2 cross = new(radius, -radius);
        DrawLine(center - cross, center + cross, color,
            2f * _accessibilityScale, true);
    }

    private IEnumerable<(CoreMapPoint Point, string Id)> DraftHandles(
        RealtimeCampaignSnapshot snapshot)
    {
        if (snapshot.Construction.NodeDraft is NodeDraftSnapshot node)
        {
            yield return (node.Position, "DRAFT_NODE");
        }
        if (snapshot.Construction.LineDraft is not LineDraftSnapshot line)
        {
            yield break;
        }
        for (int index = 0; index < line.IntermediatePoints.Count; index++)
        {
            yield return (line.IntermediatePoints[index], $"DRAFT_POINT:{index}");
        }
    }

    private void ConfigureTransform()
    {
        if (_presentation is null || Size.X <= 0 || Size.Y <= 0)
        {
            return;
        }
        MapBounds bounds = _presentation.World.World.Bounds;
        var mapBounds = new CommercialMapBounds(
            bounds.MinXUnit,
            bounds.MaxXUnit,
            bounds.MinYUnit,
            bounds.MaxYUnit);
        if (_transform is null)
        {
            _transform = new CommercialMapTransform(mapBounds, Size);
        }
        else
        {
            _transform.Configure(mapBounds, Size);
        }
        _ = RefreshPointerResolution("MAP_TRANSFORM_REFRESH");
        QueueRedraw();
    }

    private void EnsureKeyboardCursor()
    {
        if (_hasCanvasPointer || _presentation is null || _transform is null)
        {
            return;
        }
        CoreMapPoint? target = SelectionTarget(_presentation.World) ??
            _presentation.World.World.Nodes
                .Where(item => item.Commissioned)
                .OrderBy(item => item.NodeId, StringComparer.Ordinal)
                .Select(item => (CoreMapPoint?)item.Position)
                .FirstOrDefault();
        if (!target.HasValue)
        {
            return;
        }
        _pointer = target.Value;
        _lastCanvasPointer = Point(target.Value);
        _hasCanvasPointer = true;
        _ = ResolveCanvasPoint(
            "MAP_KEYBOARD_DEFAULT",
            _lastCanvasPointer,
            target.Value);
    }

    private void FollowSelection()
    {
        if (_presentation is null || _transform is null || string.Equals(
                _lastFollowSelectionId,
                _presentation.World.SelectedAssetId,
                StringComparison.Ordinal))
        {
            return;
        }
        _lastFollowSelectionId = _presentation.World.SelectedAssetId;
        CoreMapPoint? target = SelectionTarget(_presentation.World);
        if (target.HasValue)
        {
            _transform.Follow(target.Value.XUnit, target.Value.YUnit, 80f);
            _pointer = target;
            _lastCanvasPointer = Point(target.Value);
            _hasCanvasPointer = true;
            _ = ResolveCanvasPoint(
                "MAP_SELECTION_TARGET",
                _lastCanvasPointer,
                target.Value);
        }
    }

    private static CoreMapPoint? SelectionTarget(
        RealtimePlaceholderMapPresentation presentation)
    {
        string? selectedId = presentation.SelectedAssetId;
        SpatialNodeDefinition? node = presentation.World.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, selectedId, StringComparison.Ordinal));
        if (node is not null)
        {
            return node.Position;
        }
        SpatialEdgeDefinition? edge = presentation.World.Edges.FirstOrDefault(item =>
            string.Equals(item.EdgeId, selectedId, StringComparison.Ordinal));
        if (edge is not null)
        {
            CoreMapPoint from = presentation.World.Nodes.Single(item => string.Equals(
                item.NodeId,
                edge.FromNodeId,
                StringComparison.Ordinal)).Position;
            CoreMapPoint to = presentation.World.Nodes.Single(item => string.Equals(
                item.NodeId,
                edge.ToNodeId,
                StringComparison.Ordinal)).Position;
            return new CoreMapPoint(
                checked((int)(((long)from.XUnit + to.XUnit) / 2)),
                checked((int)(((long)from.YUnit + to.YUnit) / 2)));
        }
        string? highlightedNode = presentation.Highlight?.NodeIds.FirstOrDefault();
        return highlightedNode is null
            ? null
            : presentation.World.Nodes.FirstOrDefault(item => string.Equals(
                item.NodeId,
                highlightedNode,
                StringComparison.Ordinal))?.Position;
    }

    private void UpdateAccessibility()
    {
        if (_presentation is null)
        {
            return;
        }
        string selection = _presentation.World.SelectedAssetId is string selected
            ? $"선택 {CandidateDisplayName(_presentation.World, selected)}"
            : "선택 없음";
        string candidate = _candidateCycle.Count == 0
            ? "후보 없음"
            : $"후보 {_candidateIndex + 1}/{_candidateCycle.Count} " +
              CandidateDisplayName(
                  _presentation.World,
                  _candidateCycle[_candidateIndex]);
        string feedback = string.IsNullOrWhiteSpace(_presentation.World.PointerMessage)
            ? "배치 결과 없음"
            : (_presentation.World.PointerAccepted ? "승인" : "거절") + " " +
              _presentation.World.PointerMessage;
        AccessibilityName = Accessibility(_presentation);
        AccessibilityDescription =
            $"{selection}. {candidate}. {feedback}. " +
            "Q와 E로 겹친 후보를 바꾸고 Enter로 현재 후보를 선택합니다.";
    }

    private Vector2 Point(CoreMapPoint point) =>
        _transform!.WorldToCanvas(point.XUnit, point.YUnit);

    private CoreMapPoint ToWorld(Vector2 point)
    {
        CommercialWorldPosition world = _transform!.CanvasToWorld(point);
        return new CoreMapPoint(
            (int)Math.Round(world.X, MidpointRounding.AwayFromZero),
            (int)Math.Round(world.Y, MidpointRounding.AwayFromZero));
    }

    private float NodeRadius(
        SpatialWorldDefinition world,
        SpatialNodeDefinition node) =>
        (world.NodeClasses.Single(item =>
            string.Equals(item.ClassId, node.ClassId, StringComparison.Ordinal)).Kind switch
        {
            SpatialNodeKind.Substation => 13f,
            SpatialNodeKind.Pole => 7f,
            _ => 9f,
        }) * _accessibilityScale;

    private static RealtimePlaceholderAssetStatus? Status(
        RealtimePlaceholderMapPresentation presentation,
        string id) => presentation.AssetStatuses.FirstOrDefault(item =>
        string.Equals(item.AssetId, id, StringComparison.Ordinal));

    private static Color StateColor(RealtimePlaceholderAssetState? state) => state switch
    {
        RealtimePlaceholderAssetState.Planned or
            RealtimePlaceholderAssetState.Building or
            RealtimePlaceholderAssetState.AuthoredUnavailable => Planned,
        RealtimePlaceholderAssetState.Emergency => Emergency,
        RealtimePlaceholderAssetState.ProtectiveOutage => Outage,
        RealtimePlaceholderAssetState.OverLimit => Danger,
        _ => Normal,
    };

    private static double SegmentDistanceSquared(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 axis = end - start;
        if (axis.LengthSquared() <= 0.001f)
        {
            return point.DistanceSquaredTo(start);
        }
        float t = Math.Clamp((point - start).Dot(axis) / axis.LengthSquared(), 0f, 1f);
        return point.DistanceSquaredTo(start + axis * t);
    }

    private static string Accessibility(RealtimeSlicePresentation presentation)
    {
        int emergency = presentation.World.AssetStatuses.Count(item =>
            item.State == RealtimePlaceholderAssetState.Emergency);
        int outage = presentation.World.AssetStatuses.Count(item =>
            item.State == RealtimePlaceholderAssetState.ProtectiveOutage);
        int authoredUnavailable = presentation.World.AssetStatuses.Count(item =>
            item.AuthoredUnavailable);
        int building = presentation.World.AssetStatuses.Count(item =>
            item.State == RealtimePlaceholderAssetState.Building);
        return $"청류시 실시간 전력망 · 후보는 거리와 안정된 순서로 정렬 · " +
               $"공사 중 {building}곳 · 계획 사용불가 {authoredUnavailable}곳 · " +
               $"비상 {emergency}곳 · 보호정지 {outage}곳";
    }

    private static string CandidateDisplayName(
        RealtimePlaceholderMapPresentation presentation,
        string id)
    {
        if (id.StartsWith("DRAFT_POINT:", StringComparison.Ordinal))
        {
            return "초안 경로점";
        }
        SpatialNodeDefinition? node = presentation.World.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, id, StringComparison.Ordinal));
        if (node is not null)
        {
            return $"{node.DisplayName} · {StatusLabel(Status(presentation, id))}";
        }
        SpatialEdgeDefinition? edge = presentation.World.Edges.FirstOrDefault(item =>
            string.Equals(item.EdgeId, id, StringComparison.Ordinal));
        if (edge is not null)
        {
            string lineName = presentation.World.LineClasses.FirstOrDefault(item =>
                string.Equals(item.ClassId, edge.LineClassId, StringComparison.Ordinal))
                ?.DisplayName ?? "배전선";
            return $"{lineName} 구간 · {StatusLabel(Status(presentation, id))}";
        }
        return "지도 후보";
    }

    private static string StatusLabel(RealtimePlaceholderAssetStatus? status)
    {
        if (status is null)
        {
            return "정상";
        }
        if (status.ProtectiveOutage && status.AuthoredUnavailable)
        {
            return "보호정지 · 계획 사용불가 겹침";
        }
        return status.State switch
        {
            RealtimePlaceholderAssetState.Planned => "계획",
            RealtimePlaceholderAssetState.Building => "공사 중",
            RealtimePlaceholderAssetState.AuthoredUnavailable => "계획 사용불가",
            RealtimePlaceholderAssetState.Emergency => "비상 운전",
            RealtimePlaceholderAssetState.ProtectiveOutage => "보호정지",
            RealtimePlaceholderAssetState.OverLimit => "한계 초과",
            _ => "정상",
        };
    }

    private static string StatusLabel(RealtimePlaceholderAssetState? state) =>
        StatusLabel(state is null
            ? null
            : new RealtimePlaceholderAssetStatus(
                "STATUS_PREVIEW",
                state.Value,
                0,
                0,
                0,
                0,
                0,
                AuthoredUnavailable:
                    state == RealtimePlaceholderAssetState.AuthoredUnavailable,
                ProtectiveOutage:
                    state == RealtimePlaceholderAssetState.ProtectiveOutage));

    private static RealtimePlaceholderStateCue StateCue(
        RealtimePlaceholderAssetState? state) => state switch
    {
        RealtimePlaceholderAssetState.AuthoredUnavailable =>
            RealtimePlaceholderStateCue.AuthoredUnavailableBars,
        RealtimePlaceholderAssetState.Emergency =>
            RealtimePlaceholderStateCue.EmergencyTriangle,
        RealtimePlaceholderAssetState.ProtectiveOutage =>
            RealtimePlaceholderStateCue.ProtectiveOutageCross,
        RealtimePlaceholderAssetState.OverLimit =>
            RealtimePlaceholderStateCue.OverLimitDiamond,
        _ => RealtimePlaceholderStateCue.None,
    };
}
