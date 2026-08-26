using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Godot;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game;

internal sealed record CommercialMapPresentation(
    ConstructionSnapshot Snapshot,
    CoreMapPoint? PointerPoint,
    bool PointerAccepted,
    string PointerMessage,
    string ToolLabel,
    int? PointerFootprintRadiusUnit,
    bool NodeSnapEnabled,
    bool ConstructionInputEnabled,
    CommercialThermalMapPresentation? Thermal = null,
    int? PointerServiceRadiusUnit = null,
    int? DraftServiceRadiusUnit = null,
    CommercialServiceAreaPresentation? SelectedServiceArea = null,
    CommercialMapHighlightPresentation? Highlight = null,
    ConstructionError? PointerError = null,
    IReadOnlyList<string>? PointerRiskAreaIds = null,
    bool ReduceMotion = false,
    CommercialCityMapPresentation? City = null,
    SpatialWorldDefinition? ProjectionWorld = null);

internal sealed record CommercialServiceAreaPresentation(
    CoreMapPoint Center,
    int RadiusUnit,
    string Label);

internal sealed record CommercialThermalMapPresentation(
    string ProjectionLabel,
    IReadOnlyList<ThermalAssetUsage> Assets,
    string? SelectedAssetId,
    string AccessibilitySummary,
    bool ContinuousOnly = false,
    IReadOnlyList<string>? ActiveRiskAreaIds = null);

internal sealed record CommercialMapHighlightPresentation(
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<string> EdgeIds,
    string? LimitingAssetId,
    string AccessibilitySummary);

internal enum CommercialFacilityVisualKind
{
    Residential,
    Medical,
    Water,
    Industrial,
}

internal enum CommercialCityResponseState
{
    Normal,
    Stressed,
    Unserved,
}

internal sealed record CommercialCityFacilityPresentation(
    string NodeId,
    string DisplayName,
    CommercialFacilityVisualKind Kind,
    CommercialCityResponseState State,
    string StatusText);

internal sealed record CommercialCityMapPresentation(
    CommercialWeatherProfile Weather,
    long Minute,
    bool RaisedRiver,
    bool HeatStress,
    IReadOnlyList<string> UnavailableNodeIds,
    IReadOnlyList<string> UnavailableEdgeIds,
    IReadOnlyList<CommercialCityFacilityPresentation> Facilities,
    string AccessibilitySummary);

internal readonly record struct CommercialDraftPointDrag(int PointIndex, CoreMapPoint Position);

internal sealed partial class CommercialMapView : Control
{
    private static readonly Color Background = Color.FromHtml("071319");
    private static readonly Color Land = Color.FromHtml("142724");
    private static readonly Color LandEdge = Color.FromHtml("35534d");
    private static readonly Color Water = Color.FromHtml("123b4b");
    private static readonly Color WaterLine = Color.FromHtml("397389");
    private static readonly Color Building = Color.FromHtml("5b6663");
    private static readonly Color BuildingEdge = Color.FromHtml("879590");
    private static readonly Color Risk = Color.FromHtml("c36568");
    private static readonly Color IdleLine = Color.FromHtml("869895");
    private static readonly Color CommissionedLine = Color.FromHtml("68d1c5");
    private static readonly Color Planned = Color.FromHtml("efb75d");
    private static readonly Color Invalid = Color.FromHtml("ed756e");
    private static readonly Color Text = Color.FromHtml("e6eff0");
    private static readonly Color Muted = Color.FromHtml("91a3a1");
    private static readonly Color Focus = Color.FromHtml("f4d27c");
    private static readonly Color ThermalContinuous = Color.FromHtml("79d5c9");
    private static readonly Color ThermalEmergency = Color.FromHtml("f0b54f");
    private static readonly Color ThermalOutage = Color.FromHtml("a4adb2");
    private static readonly Color ThermalOverLimit = Color.FromHtml("ef706a");
    private const float CandidateRadiusPixel = 24f;
    private const float KeyboardFollowMarginPixel = 72f;
    private const int KeyboardSmallStepUnit = 100;
    private const int KeyboardLargeStepUnit = 500;

    private CommercialMapPresentation? _presentation;
    private MapViewportTransform? _transform;
    private CoreMapPoint _keyboardPoint;
    private CoreMapPoint? _pointerPoint;
    private readonly List<string> _candidateNodeIds = [];
    private int _candidateIndex;
    private bool _spaceHeld;
    private bool _panning;
    private MouseButton _panButton;
    private Vector2 _lastPanPosition;
    private bool _draggingDraftPoint;
    private int _draggedDraftPointIndex = -1;
    private double _weatherAnimationSeconds;

    public event Action<CoreMapPoint?, string?>? PointerChanged;

    public event Action<CoreMapPoint, string?>? PointRequested;

    public event Action? UndoRequested;

    public event Action<CommercialDraftPointDrag>? DraftPointMoveRequested;

    public event Action<CommercialDraftPointDrag?>? DraftPointDragPreviewChanged;

    public event Action<string>? ThermalAssetRequested;

    public event Action? CameraChanged;

#if DEBUG
    public int ZoomIndex => _transform?.ZoomIndex ?? 0;
#endif

    public string ZoomLabel => _transform?.ZoomLabel ?? "전체 보기";

#if DEBUG
    public Vector2 CameraCenter => _transform?.Center ?? Vector2.Zero;

    public CoreMapPoint KeyboardPoint => _keyboardPoint;
#endif

    public string? SelectedCandidateId => _candidateNodeIds.Count == 0
        ? null
        : _candidateNodeIds[_candidateIndex];

    public IReadOnlyList<string> CandidateNodeIds => _candidateNodeIds.AsReadOnly();

#if DEBUG
    public string? SelectedCandidateSummary => _presentation is null
        ? null
        : CandidateLabel(_presentation.Snapshot.World);

    public bool ReduceMotion => _presentation?.ReduceMotion ?? false;

    public CommercialWeatherProfile VisualWeather =>
        _presentation?.City?.Weather ?? CommercialWeatherProfile.Clear;

    public bool VisualHeatStress => _presentation?.City?.HeatStress ?? false;

    public double WeatherAnimationPhase => ReduceMotion ? 0d : _weatherAnimationSeconds;

    public int CityFacilityCount => _presentation?.City?.Facilities.Count ?? 0;

    public IReadOnlyList<string> UnavailableNodeIds =>
        _presentation?.City?.UnavailableNodeIds ?? Array.Empty<string>();

    public IReadOnlyList<string> UnavailableEdgeIds =>
        _presentation?.City?.UnavailableEdgeIds ?? Array.Empty<string>();

    public SpatialWorldDefinition? ProjectionWorld => _presentation?.ProjectionWorld;

    public string HighlightAccessibilitySummary =>
        _presentation?.Highlight?.AccessibilitySummary ?? string.Empty;

    public string? HighlightedLimitingAssetId =>
        _presentation?.Highlight?.LimitingAssetId;

    public IReadOnlyList<string> HighlightedNodeIds =>
        _presentation?.Highlight?.NodeIds ?? Array.Empty<string>();

    public IReadOnlyList<string> HighlightedEdgeIds =>
        _presentation?.Highlight?.EdgeIds ?? Array.Empty<string>();

    public bool IsDraggingDraftPoint => _draggingDraftPoint;

    public int DraggedDraftPointIndex => _draggedDraftPointIndex;

    public int? PointerServiceRadiusUnit => _presentation?.PointerServiceRadiusUnit;

    public int? DraftServiceRadiusUnit => _presentation?.DraftServiceRadiusUnit;

    public CommercialServiceAreaPresentation? SelectedServiceArea =>
        _presentation?.SelectedServiceArea;

    public IReadOnlyList<string> ActiveRiskAreaIds =>
        _presentation?.Thermal?.ActiveRiskAreaIds ?? Array.Empty<string>();

    public string? SelectedThermalAssetId => _presentation?.Thermal?.SelectedAssetId;

    public ThermalOperatingState? SelectedThermalState
    {
        get
        {
            CommercialThermalMapPresentation? thermal = _presentation?.Thermal;
            return thermal?.SelectedAssetId is string assetId
                ? thermal.Assets.FirstOrDefault(item => string.Equals(
                    item.AssetId,
                    assetId,
                    StringComparison.Ordinal))?.State
                : null;
        }
    }
#endif

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        AccessibilityDescription =
            "청류시 자유 배치 지도. 방향키로 커서를 움직이고 Enter로 선택합니다. Q와 E로 가까운 접속점을 바꿉니다.";
        MouseExited += () =>
        {
            if (!_panning)
            {
                SetPointer(null);
            }
        };
        FocusEntered += QueueRedraw;
        FocusExited += () =>
        {
            _spaceHeld = false;
            EndPan();
            QueueRedraw();
        };
        Resized += OnResized;
    }

    public void SetPresentation(CommercialMapPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        _presentation = presentation;
        MapBounds bounds = presentation.Snapshot.World.Bounds;
        var gameBounds = new MapViewportBounds(
            bounds.MinXUnit,
            bounds.MaxXUnit,
            bounds.MinYUnit,
            bounds.MaxYUnit);
        if (_transform is null)
        {
            _transform = new MapViewportTransform(gameBounds, Size);
            _keyboardPoint = InitialKeyboardPoint(presentation.Snapshot.World);
            _pointerPoint = _keyboardPoint;
        }
        else
        {
            _transform.Configure(gameBounds, Size);
        }

        _pointerPoint = presentation.PointerPoint;
        if (!presentation.ConstructionInputEnabled && _draggingDraftPoint)
        {
            _draggingDraftPoint = false;
            _draggedDraftPointIndex = -1;
            DraftPointDragPreviewChanged?.Invoke(null);
        }
        RefreshCandidates(notify: false);
        bool animateWeather = !presentation.ReduceMotion &&
            presentation.City?.Weather is CommercialWeatherProfile.Heat or
                CommercialWeatherProfile.Rain or CommercialWeatherProfile.Storm;
        SetProcess(animateWeather);
        if (presentation.ReduceMotion)
        {
            _weatherAnimationSeconds = 0d;
        }
        AccessibilityDescription = presentation.ConstructionInputEnabled
            ? "청류시 자유 배치 지도. 방향키로 커서를 움직이고 Enter로 선택합니다. Q와 E로 가까운 접속점을 바꿉니다."
            : "청류시 전력망 운영 지도. 마우스 또는 방향키와 Enter로 설비를 선택하고 오른쪽 패널에서 현재 사용과 한계를 확인합니다.";
        AccessibilityName = BuildAccessibilityName(presentation);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_presentation is null || _presentation.ReduceMotion ||
            _presentation.City?.Weather == CommercialWeatherProfile.Clear)
        {
            return;
        }
        _weatherAnimationSeconds = (_weatherAnimationSeconds + delta) % 10d;
        QueueRedraw();
    }

#if DEBUG
    public Vector2 ViewportPointForWorld(CoreMapPoint point)
    {
        MapViewportTransform transform = RequireTransform();
        Vector2 local = transform.WorldToCanvas(point.XUnit, point.YUnit);
        return GetGlobalTransformWithCanvas() * local;
    }

    public MapWorldPosition WorldAtViewportPoint(Vector2 viewportPoint)
    {
        Vector2 local = GetGlobalTransformWithCanvas().AffineInverse() * viewportPoint;
        return RequireTransform().CanvasToWorld(local);
    }
#endif

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (_presentation is null || _transform is null)
        {
            return;
        }

        switch (inputEvent)
        {
            case InputEventMouseButton button when
                button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown &&
                button.Pressed:
                int direction = button.ButtonIndex == MouseButton.WheelUp ? 1 : -1;
                ZoomBy(direction, button.Position);
                AcceptEvent();
                return;

            case InputEventMouseButton button when IsPanButton(button):
                if (button.Pressed)
                {
                    BeginPan(button.ButtonIndex, button.Position);
                }
                else if (_panning && button.ButtonIndex == _panButton)
                {
                    EndPan();
                }
                AcceptEvent();
                return;

            case InputEventMouseMotion motion when _panning:
                Vector2 delta = motion.Position - _lastPanPosition;
                _lastPanPosition = motion.Position;
                _transform.PanByCanvasDelta(delta);
                RefreshCandidates(notify: true);
                CameraChanged?.Invoke();
                QueueRedraw();
                AcceptEvent();
                return;

            case InputEventMouseMotion motion when _draggingDraftPoint:
                if (TryMapPoint(motion.Position, out CoreMapPoint draggedPoint))
                {
                    _pointerPoint = draggedPoint;
                    RefreshCandidates(notify: false);
                    DraftPointDragPreviewChanged?.Invoke(new CommercialDraftPointDrag(
                        _draggedDraftPointIndex,
                        draggedPoint));
                    QueueRedraw();
                }
                AcceptEvent();
                return;

            case InputEventMouseMotion motion:
                SetPointer(TryMapPoint(motion.Position, out CoreMapPoint point) ? point : null);
                AcceptEvent();
                return;

            case InputEventMouseButton button when
                button.ButtonIndex == MouseButton.Left &&
                button.Pressed &&
                _presentation.ConstructionInputEnabled &&
                TryBeginDraftPointDrag(button.Position):
                AcceptEvent();
                return;

            case InputEventMouseButton button when
                button.ButtonIndex == MouseButton.Left &&
                !button.Pressed &&
                _draggingDraftPoint:
                _draggingDraftPoint = false;
                if (TryMapPoint(button.Position, out CoreMapPoint movedPoint))
                {
                    DraftPointMoveRequested?.Invoke(new CommercialDraftPointDrag(
                        _draggedDraftPointIndex,
                        movedPoint));
                }
                _draggedDraftPointIndex = -1;
                DraftPointDragPreviewChanged?.Invoke(null);
                AcceptEvent();
                return;

            case InputEventMouseButton button when
                button.ButtonIndex == MouseButton.Right && button.Pressed:
                if (_presentation.ConstructionInputEnabled)
                {
                    UndoRequested?.Invoke();
                }
                AcceptEvent();
                return;

            case InputEventMouseButton button when
                button.ButtonIndex == MouseButton.Left && button.Pressed:
                if (TryMapPoint(button.Position, out CoreMapPoint clicked))
                {
                    GrabFocus();
                    _keyboardPoint = clicked;
                    SetPointer(clicked);
                    if (CanRequestConstructionPoint())
                    {
                        PointRequested?.Invoke(clicked, SelectedCandidateId);
                    }
                    else if (_presentation.Thermal is not null)
                    {
                        if (TryThermalAssetAt(button.Position, out string assetId))
                        {
                            ThermalAssetRequested?.Invoke(assetId);
                        }
                    }
                }
                AcceptEvent();
                return;

            case InputEventKey key:
                HandleKey(key);
                return;
        }
    }

    public override void _Draw()
    {
        if (_presentation is null || _transform is null)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), Background);
            return;
        }

        ConstructionSnapshot snapshot = _presentation.Snapshot;
        SpatialWorldDefinition projectionWorld =
            _presentation.ProjectionWorld ?? snapshot.World;
        DrawRect(new Rect2(Vector2.Zero, Size), Background);
        DrawMapGround();
        DrawTerrain(snapshot.World);
        if (_presentation.City is CommercialCityMapPresentation city)
        {
            DrawTimeAndWeather(city, _presentation.ReduceMotion);
            DrawCityFacilities(projectionWorld, city);
        }
        DrawRiskAreas(snapshot.World, _presentation.Thermal?.ActiveRiskAreaIds);
        DrawEdges(snapshot.World);
        DrawLineDraft(snapshot);
        DrawNodes(snapshot.World);
        DrawNodeDraft(snapshot);
        if (_presentation.SelectedServiceArea is CommercialServiceAreaPresentation serviceArea)
        {
            DrawServiceRadius(
                serviceArea.Center,
                serviceArea.RadiusUnit,
                serviceArea.Label,
                Focus,
                0.58f);
        }
        if (_presentation.Thermal is CommercialThermalMapPresentation thermal)
        {
            DrawThermalOverlays(projectionWorld, thermal);
        }
        if (_presentation.Highlight is CommercialMapHighlightPresentation highlight)
        {
            DrawOperationalHighlight(projectionWorld, highlight);
        }
        if (_presentation.City is CommercialCityMapPresentation cityMarkers)
        {
            DrawUnavailableMarkers(projectionWorld, cityMarkers);
        }
        DrawPointer(_presentation);
        DrawMapLegend();
        if (HasFocus())
        {
            DrawRect(new Rect2(Vector2.One * 2f, Size - (Vector2.One * 4f)), Focus, false, 2f);
        }
    }

    private void HandleKey(InputEventKey key)
    {
        if (key.Keycode == Key.Tab || key.PhysicalKeycode == Key.Tab)
        {
            return;
        }

        if (key.Keycode == Key.Space || key.PhysicalKeycode == Key.Space)
        {
            _spaceHeld = key.Pressed;
            AcceptEvent();
            return;
        }
        if (!key.Pressed || key.Echo)
        {
            return;
        }

        Key physical = key.PhysicalKeycode;
        if (_presentation!.ConstructionInputEnabled &&
            (physical == Key.Q || physical == Key.E))
        {
            CycleCandidate(physical == Key.Q ? -1 : 1);
            AcceptEvent();
            return;
        }
        if (key.Keycode is Key.Plus or Key.Equal or Key.KpAdd)
        {
            ZoomBy(1, KeyboardAnchor());
            AcceptEvent();
            return;
        }
        if (key.Keycode is Key.Minus or Key.KpSubtract)
        {
            ZoomBy(-1, KeyboardAnchor());
            AcceptEvent();
            return;
        }
        if (key.Keycode == Key.Home)
        {
            RequireTransform().Home();
            RefreshCandidates(notify: true);
            CameraChanged?.Invoke();
            QueueRedraw();
            AcceptEvent();
            return;
        }
        if (key.Keycode == Key.Backspace)
        {
            if (_presentation!.ConstructionInputEnabled)
            {
                UndoRequested?.Invoke();
            }
            AcceptEvent();
            return;
        }
        if (key.Keycode is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            MoveKeyboardCursor(key);
            AcceptEvent();
            return;
        }
        if (key.Keycode is Key.Enter or Key.KpEnter)
        {
            SetPointer(_keyboardPoint);
            if (CanRequestConstructionPoint())
            {
                PointRequested?.Invoke(_keyboardPoint, SelectedCandidateId);
            }
            else if (_presentation.Thermal is not null)
            {
                if (TryThermalAssetAt(KeyboardAnchor(), out string assetId))
                {
                    ThermalAssetRequested?.Invoke(assetId);
                }
            }
            AcceptEvent();
        }
    }

    private bool CanRequestConstructionPoint() =>
        _presentation?.ConstructionInputEnabled == true &&
        !(_presentation.Snapshot.Phase == ConstructionPhase.LineDrafting &&
          _presentation.Snapshot.LineDraft?.EndNodeId is not null);

    private void MoveKeyboardCursor(InputEventKey key)
    {
        int step = key.ShiftPressed ? KeyboardLargeStepUnit : KeyboardSmallStepUnit;
        MapBounds bounds = _presentation!.Snapshot.World.Bounds;
        int x = _keyboardPoint.XUnit;
        int y = _keyboardPoint.YUnit;
        switch (key.Keycode)
        {
            case Key.Left: x = SaturatingAdd(x, -step); break;
            case Key.Right: x = SaturatingAdd(x, step); break;
            case Key.Up: y = SaturatingAdd(y, -step); break;
            case Key.Down: y = SaturatingAdd(y, step); break;
        }
        _keyboardPoint = new CoreMapPoint(
            Math.Clamp(x, bounds.MinXUnit, bounds.MaxXUnit),
            Math.Clamp(y, bounds.MinYUnit, bounds.MaxYUnit));
        RequireTransform().Follow(
            _keyboardPoint.XUnit,
            _keyboardPoint.YUnit,
            KeyboardFollowMarginPixel);
        SetPointer(_keyboardPoint);
        CameraChanged?.Invoke();
    }

    private void ZoomBy(int direction, Vector2 anchor)
    {
        MapViewportTransform transform = RequireTransform();
        transform.SetZoomAt(transform.ZoomIndex + direction, anchor);
        RefreshCandidates(notify: true);
        CameraChanged?.Invoke();
        QueueRedraw();
    }

    private void SetPointer(CoreMapPoint? point)
    {
        _pointerPoint = point;
        RefreshCandidates(notify: false);
        PointerChanged?.Invoke(point, SelectedCandidateId);
        QueueRedraw();
    }

    private void RefreshCandidates(bool notify)
    {
        string? retainedId = SelectedCandidateId;
        _candidateNodeIds.Clear();
        if (_pointerPoint is CoreMapPoint pointer &&
            _presentation is { NodeSnapEnabled: true } &&
            !_draggingDraftPoint &&
            _transform is not null)
        {
            Vector2 pointerCanvas = _transform.WorldToCanvas(pointer.XUnit, pointer.YUnit);
            _candidateNodeIds.AddRange(_presentation.Snapshot.World.Nodes
                .Select(node => new
                {
                    node.NodeId,
                    Distance = pointerCanvas.DistanceSquaredTo(
                        _transform.WorldToCanvas(node.Position.XUnit, node.Position.YUnit)),
                })
                .Where(candidate => candidate.Distance <= CandidateRadiusPixel * CandidateRadiusPixel)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.NodeId, StringComparer.Ordinal)
                .Select(candidate => candidate.NodeId));
        }
        _candidateIndex = retainedId is null
            ? 0
            : Math.Max(0, _candidateNodeIds.IndexOf(retainedId));
        if (notify)
        {
            PointerChanged?.Invoke(_pointerPoint, SelectedCandidateId);
        }
    }

    private void CycleCandidate(int direction)
    {
        if (_candidateNodeIds.Count == 0)
        {
            return;
        }
        _candidateIndex = (_candidateIndex + direction + _candidateNodeIds.Count) %
            _candidateNodeIds.Count;
        PointerChanged?.Invoke(_pointerPoint, SelectedCandidateId);
        QueueRedraw();
    }

    private bool TryThermalAssetAt(Vector2 canvasPoint, out string assetId)
    {
        assetId = string.Empty;
        if (_presentation?.Thermal is not CommercialThermalMapPresentation thermal)
        {
            return false;
        }
        SpatialWorldDefinition world =
            _presentation.ProjectionWorld ?? _presentation.Snapshot.World;
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        Dictionary<string, SpatialEdgeDefinition> edges = world.Edges.ToDictionary(
            item => item.EdgeId,
            StringComparer.Ordinal);
        var hits = new List<(float Distance, int KindOrder, string AssetId)>();
        foreach (ThermalAssetUsage usage in thermal.Assets)
        {
            if (usage.AssetKind == ThermalAssetKind.Node &&
                nodes.TryGetValue(usage.AssetId, out SpatialNodeDefinition? node))
            {
                float distance = ToCanvas(node.Position).DistanceSquaredTo(canvasPoint);
                if (distance <= 18f * 18f)
                {
                    hits.Add((distance, 0, usage.AssetId));
                }
            }
            else if (usage.AssetKind == ThermalAssetKind.Edge &&
                     edges.TryGetValue(usage.AssetId, out SpatialEdgeDefinition? edge) &&
                     nodes.TryGetValue(edge.FromNodeId, out SpatialNodeDefinition? from) &&
                     nodes.TryGetValue(edge.ToNodeId, out SpatialNodeDefinition? to))
            {
                float distance = DistanceSquaredToSegment(
                    canvasPoint,
                    ToCanvas(from.Position),
                    ToCanvas(to.Position));
                if (distance <= 13f * 13f)
                {
                    hits.Add((distance, 1, usage.AssetId));
                }
            }
        }
        if (hits.Count == 0)
        {
            return false;
        }
        assetId = hits
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.KindOrder)
            .ThenBy(item => item.AssetId, StringComparer.Ordinal)
            .First()
            .AssetId;
        return true;
    }

    private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.001f)
        {
            return point.DistanceSquaredTo(start);
        }
        float ratio = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0f, 1f);
        return point.DistanceSquaredTo(start + segment * ratio);
    }

    private bool TryMapPoint(Vector2 canvasPoint, out CoreMapPoint point)
    {
        MapViewportTransform transform = RequireTransform();
        if (!transform.PlotRect.HasPoint(canvasPoint))
        {
            point = default;
            return false;
        }
        MapWorldPosition world = transform.CanvasToWorld(canvasPoint);
        MapBounds bounds = _presentation!.Snapshot.World.Bounds;
        point = new CoreMapPoint(
            Math.Clamp(RoundUnit(world.X), bounds.MinXUnit, bounds.MaxXUnit),
            Math.Clamp(RoundUnit(world.Y), bounds.MinYUnit, bounds.MaxYUnit));
        return true;
    }

    private void DrawMapGround()
    {
        MapViewportTransform transform = RequireTransform();
        MapBounds bounds = _presentation!.Snapshot.World.Bounds;
        Vector2 topLeft = transform.WorldToCanvas(bounds.MinXUnit, bounds.MinYUnit);
        Vector2 bottomRight = transform.WorldToCanvas(bounds.MaxXUnit, bounds.MaxYUnit);
        Rect2 mapRect = new(topLeft, bottomRight - topLeft);
        DrawRect(mapRect, Land);
        DrawRect(mapRect, LandEdge, false, 1.5f);

        DrawParcel(new Rect2(
            MapRatio(topLeft, bottomRight, 0.05f, 0.08f),
            (bottomRight - topLeft) * new Vector2(0.26f, 0.30f)));
        DrawParcel(new Rect2(
            MapRatio(topLeft, bottomRight, 0.65f, 0.08f),
            (bottomRight - topLeft) * new Vector2(0.29f, 0.34f)));
        DrawParcel(new Rect2(
            MapRatio(topLeft, bottomRight, 0.61f, 0.56f),
            (bottomRight - topLeft) * new Vector2(0.34f, 0.35f)));
        Color road = new(LandEdge, 0.34f);
        DrawLine(
            MapRatio(topLeft, bottomRight, 0.02f, 0.48f),
            MapRatio(topLeft, bottomRight, 0.98f, 0.48f),
            new Color(Background, 0.45f),
            9f,
            true);
        DrawLine(
            MapRatio(topLeft, bottomRight, 0.02f, 0.48f),
            MapRatio(topLeft, bottomRight, 0.98f, 0.48f),
            road,
            4f,
            true);
        DrawLine(
            MapRatio(topLeft, bottomRight, 0.73f, 0.05f),
            MapRatio(topLeft, bottomRight, 0.67f, 0.95f),
            road,
            3f,
            true);
    }

    private void DrawParcel(Rect2 rect)
    {
        DrawRect(rect, new Color(LandEdge, 0.07f));
        DrawRect(rect, new Color(LandEdge, 0.23f), false, 1f);
    }

    private static Vector2 MapRatio(
        Vector2 topLeft,
        Vector2 bottomRight,
        float xRatio,
        float yRatio) => new(
        Mathf.Lerp(topLeft.X, bottomRight.X, xRatio),
        Mathf.Lerp(topLeft.Y, bottomRight.Y, yRatio));

    private void DrawTerrain(SpatialWorldDefinition world)
    {
        foreach (TerrainPolygonDefinition area in world.Terrain)
        {
            Vector2[] polygon = area.Polygon.Select(ToCanvas).ToArray();
            Color fill = area.Kind == TerrainKind.Water ? Water : Building;
            Color edge = area.Kind == TerrainKind.Water ? WaterLine : BuildingEdge;
            DrawColoredPolygon(polygon, fill);
            DrawPolyline(polygon.Append(polygon[0]).ToArray(), edge, 1.6f, true);
            if (area.Kind == TerrainKind.Water)
            {
                DrawPolygonHatching(polygon, WaterLine, 24f, 0.32f);
                if (_presentation?.City?.RaisedRiver == true)
                {
                    DrawPolyline(
                        polygon.Append(polygon[0]).ToArray(),
                        new Color(WaterLine, 0.95f),
                        5f,
                        true);
                }
            }
            DrawAreaLabel(polygon, area.DisplayName, edge);
        }
    }

    private void DrawTimeAndWeather(
        CommercialCityMapPresentation city,
        bool reduceMotion)
    {
        long minuteOfDay = ((city.Minute % (24L * 60L)) + (24L * 60L)) % (24L * 60L);
        float nightAlpha = minuteOfDay switch
        {
            < 360 => 0.25f,
            < 480 => 0.12f,
            > 1200 => 0.22f,
            > 1080 => 0.10f,
            _ => 0f,
        };
        if (nightAlpha > 0f)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.02f, 0.04f, 0.12f, nightAlpha));
        }
        float phase = reduceMotion ? 0f : (float)(_weatherAnimationSeconds % 1d);
        if (city.HeatStress)
        {
            DrawHeatHaze(phase);
        }
        switch (city.Weather)
        {
            case CommercialWeatherProfile.Clear:
                return;
            case CommercialWeatherProfile.Heat:
                return;
            case CommercialWeatherProfile.Rain:
            case CommercialWeatherProfile.Storm:
                bool storm = city.Weather == CommercialWeatherProfile.Storm;
                DrawRect(
                    new Rect2(Vector2.Zero, Size),
                    new Color(0.04f, 0.12f, 0.18f, storm ? 0.17f : 0.09f));
                float spacing = storm ? 22f : 34f;
                float offsetX = phase * spacing;
                for (float x = -Size.Y + offsetX; x < Size.X; x += spacing)
                {
                    DrawLine(
                        new Vector2(x, 0f),
                        new Vector2(x + Size.Y * 0.34f, Size.Y),
                        new Color(WaterLine, storm ? 0.38f : 0.25f),
                        storm ? 1.7f : 1.1f,
                        true);
                }
                return;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void DrawHeatHaze(float phase)
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.42f, 0.18f, 0.04f, 0.07f));
        for (float y = 42f; y < Size.Y - 32f; y += 58f)
        {
            float offset = (phase * 18f) % 18f;
            DrawLine(
                new Vector2(12f + offset, y),
                new Vector2(Size.X - 12f, y + 4f),
                new Color(Planned, 0.16f),
                1.2f,
                true);
        }
    }

    private void DrawCityFacilities(
        SpatialWorldDefinition world,
        CommercialCityMapPresentation city)
    {
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        foreach (CommercialCityFacilityPresentation facility in city.Facilities)
        {
            if (!nodes.TryGetValue(facility.NodeId, out SpatialNodeDefinition? node))
            {
                continue;
            }
            Vector2 anchor = ToCanvas(node.Position) + new Vector2(0f, 31f);
            Color stateColor = facility.State switch
            {
                CommercialCityResponseState.Normal => CommissionedLine,
                CommercialCityResponseState.Stressed => Planned,
                CommercialCityResponseState.Unserved => Invalid,
                _ => throw new ArgumentOutOfRangeException(),
            };
            DrawFacilitySilhouette(anchor, facility.Kind, stateColor, facility.State);
        }
    }

    private void DrawFacilitySilhouette(
        Vector2 anchor,
        CommercialFacilityVisualKind kind,
        Color color,
        CommercialCityResponseState state)
    {
        Rect2 body = kind switch
        {
            CommercialFacilityVisualKind.Medical => new(anchor - new Vector2(14f, 10f), new Vector2(28f, 20f)),
            CommercialFacilityVisualKind.Water => new(anchor - new Vector2(13f, 8f), new Vector2(26f, 16f)),
            CommercialFacilityVisualKind.Industrial => new(anchor - new Vector2(17f, 9f), new Vector2(34f, 18f)),
            _ => new(anchor - new Vector2(12f, 8f), new Vector2(24f, 16f)),
        };
        DrawRect(body, new Color(Background, 0.92f));
        DrawRect(body, color, false, state == CommercialCityResponseState.Stressed ? 3f : 2f);
        if (kind == CommercialFacilityVisualKind.Medical)
        {
            DrawLine(anchor + new Vector2(-5f, 0f), anchor + new Vector2(5f, 0f), color, 2f);
            DrawLine(anchor + new Vector2(0f, -5f), anchor + new Vector2(0f, 5f), color, 2f);
        }
        else if (kind == CommercialFacilityVisualKind.Water)
        {
            DrawArc(anchor, 6f, 0f, Mathf.Tau, 20, color, 2f, true);
        }
        else if (kind == CommercialFacilityVisualKind.Industrial)
        {
            DrawLine(body.Position + new Vector2(6f, 0f), body.Position + new Vector2(6f, -8f), color, 3f);
            DrawLine(body.Position + new Vector2(13f, 0f), body.Position + new Vector2(13f, -5f), color, 3f);
        }
        else
        {
            DrawLine(anchor + new Vector2(-12f, -8f), anchor + new Vector2(0f, -16f), color, 2f);
            DrawLine(anchor + new Vector2(0f, -16f), anchor + new Vector2(12f, -8f), color, 2f);
        }
        DrawThermalIcon(
            anchor + new Vector2(17f, -13f),
            state switch
            {
                CommercialCityResponseState.Normal => "✓",
                CommercialCityResponseState.Stressed => "!",
                CommercialCityResponseState.Unserved => "×",
                _ => throw new ArgumentOutOfRangeException(),
            },
            color);
    }

    private void DrawUnavailableMarkers(
        SpatialWorldDefinition world,
        CommercialCityMapPresentation city)
    {
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        Dictionary<string, SpatialEdgeDefinition> edges = world.Edges.ToDictionary(
            item => item.EdgeId,
            StringComparer.Ordinal);
        foreach (string nodeId in city.UnavailableNodeIds)
        {
            if (nodes.TryGetValue(nodeId, out SpatialNodeDefinition? node))
            {
                DrawThermalIcon(ToCanvas(node.Position) + new Vector2(-18f, 18f), "불가", ThermalOutage);
            }
        }
        foreach (string edgeId in city.UnavailableEdgeIds)
        {
            if (edges.TryGetValue(edgeId, out SpatialEdgeDefinition? edge) &&
                nodes.TryGetValue(edge.FromNodeId, out SpatialNodeDefinition? from) &&
                nodes.TryGetValue(edge.ToNodeId, out SpatialNodeDefinition? to))
            {
                DrawThermalIcon(
                    (ToCanvas(from.Position) + ToCanvas(to.Position)) / 2f + new Vector2(0f, 16f),
                    "불가",
                    ThermalOutage);
            }
        }
    }

    private void DrawRiskAreas(
        SpatialWorldDefinition world,
        IReadOnlyList<string>? activeRiskAreaIds)
    {
        IReadOnlySet<string> active = activeRiskAreaIds is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : activeRiskAreaIds.ToHashSet(StringComparer.Ordinal);
        foreach (SpatialRiskAreaDefinition area in world.RiskAreas)
        {
            bool isActive = active.Contains(area.RiskAreaId);
            Vector2[] polygon = area.Polygon.Select(ToCanvas).ToArray();
            DrawColoredPolygon(polygon, new Color(Risk, isActive ? 0.20f : 0.10f));
            DrawPolyline(
                polygon.Append(polygon[0]).ToArray(),
                new Color(Risk, isActive ? 1f : 0.85f),
                isActive ? 2.5f : 1.5f,
                true);
            DrawPolygonHatching(
                polygon,
                Risk,
                isActive ? 12f : 18f,
                isActive ? 0.52f : 0.32f);
            DrawAreaLabel(
                polygon,
                isActive ? area.DisplayName + " · 현재 시험 적용" : area.DisplayName,
                Risk);
        }
    }

    private void DrawEdges(SpatialWorldDefinition world)
    {
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            node => node.NodeId,
            StringComparer.Ordinal);
        foreach (SpatialEdgeDefinition edge in world.Edges)
        {
            if (!nodes.TryGetValue(edge.FromNodeId, out SpatialNodeDefinition? from) ||
                !nodes.TryGetValue(edge.ToNodeId, out SpatialNodeDefinition? to))
            {
                continue;
            }
            Vector2 start = ToCanvas(from.Position);
            Vector2 end = ToCanvas(to.Position);
            Color color = edge.Commissioned ? CommissionedLine : Planned;
            DrawLine(start, end, new Color(Background, 0.9f), 6f, true);
            DrawLine(start, end, color, edge.Commissioned ? 3f : 2.5f, true);
        }
    }

    private void DrawLineDraft(ConstructionSnapshot snapshot)
    {
        if (snapshot.LineDraft is not LineDraftSnapshot draft)
        {
            return;
        }
        SpatialNodeDefinition? start = snapshot.World.Nodes.FirstOrDefault(
            node => string.Equals(node.NodeId, draft.StartNodeId, StringComparison.Ordinal));
        if (start is null)
        {
            return;
        }
        var points = new List<CoreMapPoint> { start.Position };
        points.AddRange(draft.IntermediatePoints);
        if (draft.EndNodeId is string endId)
        {
            SpatialNodeDefinition? end = snapshot.World.Nodes.FirstOrDefault(
                node => string.Equals(node.NodeId, endId, StringComparison.Ordinal));
            if (end is not null)
            {
                points.Add(end.Position);
            }
        }
        if (points.Count >= 2)
        {
            DrawPolyline(points.Select(ToCanvas).ToArray(), new Color(Background, 0.9f), 6f, true);
            DrawPolyline(points.Select(ToCanvas).ToArray(), Planned, 2.5f, true);
        }
        SpatialNodeClassDefinition poleClass = snapshot.World.NodeClasses.Single(
            nodeClass => string.Equals(nodeClass.ClassId, draft.PoleClassId, StringComparison.Ordinal));
        SpatialLineClassDefinition lineClass = snapshot.World.LineClasses.Single(
            item => string.Equals(item.ClassId, draft.LineClassId, StringComparison.Ordinal));
        CoreMapPoint currentSegmentStart = draft.IntermediatePoints.Count == 0
            ? start.Position
            : draft.IntermediatePoints[^1];
        float spanRadiusPixel = (float)(lineClass.MaxSpanUnit * RequireTransform().Scale);
        Color spanColor = _presentation?.PointerError == ConstructionError.SpanTooLong
            ? Invalid
            : Planned;
        DrawArc(
            ToCanvas(currentSegmentStart),
            spanRadiusPixel,
            0f,
            Mathf.Tau,
            72,
            new Color(spanColor, 0.38f),
            1.2f,
            true);
        foreach (CoreMapPoint point in draft.IntermediatePoints)
        {
            DrawFootprint(point, poleClass.FootprintRadiusUnit, Planned, 0.12f);
            DrawCircle(ToCanvas(point), 4.5f, Planned);
        }
    }

    private void DrawNodes(SpatialWorldDefinition world)
    {
        Dictionary<string, SpatialNodeClassDefinition> classes = world.NodeClasses.ToDictionary(
            item => item.ClassId,
            StringComparer.Ordinal);
        foreach (SpatialNodeDefinition node in world.Nodes)
        {
            if (!classes.TryGetValue(node.ClassId, out SpatialNodeClassDefinition? nodeClass))
            {
                continue;
            }
            Color color = node.Commissioned ? CommissionedLine : Planned;
            DrawFootprint(node.Position, nodeClass.FootprintRadiusUnit, color, 0.08f);
            Vector2 center = ToCanvas(node.Position);
            float radius = nodeClass.Kind switch
            {
                SpatialNodeKind.SourceTerminal => 9f,
                SpatialNodeKind.Substation => 8f,
                SpatialNodeKind.DedicatedLoadTerminal => 7f,
                _ => 5f,
            };
            DrawCircle(center, radius + 2f, Background);
            DrawCircle(center, radius, color);
            if (nodeClass.Kind != SpatialNodeKind.Pole)
            {
                DrawString(
                    GetThemeDefaultFont(),
                    center + new Vector2(radius + 7f, -radius - 2f),
                    node.DisplayName,
                    HorizontalAlignment.Left,
                    -1f,
                    12,
                    Text);
            }
        }
    }

    private void DrawNodeDraft(ConstructionSnapshot snapshot)
    {
        if (snapshot.NodeDraft is not NodeDraftSnapshot draft)
        {
            return;
        }
        SpatialNodeClassDefinition nodeClass = snapshot.World.NodeClasses.Single(
            item => string.Equals(item.ClassId, draft.NodeClassId, StringComparison.Ordinal));
        DrawFootprint(draft.Position, nodeClass.FootprintRadiusUnit, Planned, 0.16f);
        if (_presentation?.DraftServiceRadiusUnit is int serviceRadius)
        {
            DrawServiceRadius(
                draft.Position,
                serviceRadius,
                "서비스 권역",
                Planned,
                0.45f);
        }
        DrawCircle(ToCanvas(draft.Position), 6f, Planned);
    }

    private void DrawThermalOverlays(
        SpatialWorldDefinition world,
        CommercialThermalMapPresentation thermal)
    {
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        Dictionary<string, SpatialEdgeDefinition> edges = world.Edges.ToDictionary(
            item => item.EdgeId,
            StringComparer.Ordinal);
        foreach (ThermalAssetUsage usage in thermal.Assets.OrderBy(
                     item => item.AssetId,
                     StringComparer.Ordinal))
        {
            bool selected = string.Equals(
                usage.AssetId,
                thermal.SelectedAssetId,
                StringComparison.Ordinal);
            if (usage.AssetKind == ThermalAssetKind.Node &&
                nodes.TryGetValue(usage.AssetId, out SpatialNodeDefinition? node))
            {
                DrawThermalNode(ToCanvas(node.Position), usage.State, selected);
            }
            else if (usage.AssetKind == ThermalAssetKind.Edge &&
                     edges.TryGetValue(usage.AssetId, out SpatialEdgeDefinition? edge) &&
                     nodes.TryGetValue(edge.FromNodeId, out SpatialNodeDefinition? from) &&
                     nodes.TryGetValue(edge.ToNodeId, out SpatialNodeDefinition? to))
            {
                DrawThermalEdge(ToCanvas(from.Position), ToCanvas(to.Position), usage.State, selected);
            }
        }
    }

    private void DrawThermalEdge(
        Vector2 start,
        Vector2 end,
        ThermalOperatingState state,
        bool selected)
    {
        Color color = ThermalStateColor(state);
        Vector2 delta = end - start;
        Vector2 direction = delta.LengthSquared() <= 0.001f
            ? Vector2.Right
            : delta.Normalized();
        Vector2 normal = new(-direction.Y, direction.X);
        if (selected)
        {
            DrawLine(start, end, new Color(Focus, 0.9f), 11f, true);
        }

        switch (state)
        {
            case ThermalOperatingState.Continuous:
                DrawLine(start, end, color, 4f, true);
                break;
            case ThermalOperatingState.Emergency:
                DrawLine(start + normal * 3f, end + normal * 3f, color, 2.2f, true);
                DrawLine(start - normal * 3f, end - normal * 3f, color, 2.2f, true);
                DrawThermalTicks(start, end, color, crossed: false);
                break;
            case ThermalOperatingState.ProtectiveOutage:
                DrawDashedSegment(start, end, color, 3f, 11f, 8f);
                DrawThermalTicks(start, end, color, crossed: true);
                break;
            case ThermalOperatingState.OverLimit:
                DrawLine(start + normal * 3.5f, end + normal * 3.5f, color, 2.5f, true);
                DrawLine(start - normal * 3.5f, end - normal * 3.5f, color, 2.5f, true);
                DrawThermalTicks(start, end, color, crossed: true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state));
        }
        if (selected || state != ThermalOperatingState.Continuous)
        {
            DrawThermalIcon((start + end) / 2f, ThermalStateIcon(state), color);
        }
    }

    private void DrawThermalNode(
        Vector2 center,
        ThermalOperatingState state,
        bool selected)
    {
        Color color = ThermalStateColor(state);
        if (selected)
        {
            DrawArc(center, 18f, 0f, Mathf.Tau, 48, Focus, 3f, true);
        }
        switch (state)
        {
            case ThermalOperatingState.Continuous:
                DrawArc(center, 12f, 0f, Mathf.Tau, 40, color, 3f, true);
                break;
            case ThermalOperatingState.Emergency:
                DrawArc(center, 10f, 0f, Mathf.Tau, 40, color, 2f, true);
                DrawArc(center, 14f, 0f, Mathf.Tau, 40, color, 2f, true);
                DrawRadialTicks(center, 17f, color);
                break;
            case ThermalOperatingState.ProtectiveOutage:
                DrawDashedArc(center, 13f, color);
                DrawLine(center + new Vector2(-7f, -7f), center + new Vector2(7f, 7f), color, 2.5f);
                DrawLine(center + new Vector2(-7f, 7f), center + new Vector2(7f, -7f), color, 2.5f);
                break;
            case ThermalOperatingState.OverLimit:
                DrawArc(center, 10f, 0f, Mathf.Tau, 40, color, 2f, true);
                DrawArc(center, 15f, 0f, Mathf.Tau, 40, color, 2.5f, true);
                DrawLine(center + new Vector2(-9f, -9f), center + new Vector2(9f, 9f), color, 3f);
                DrawLine(center + new Vector2(-9f, 9f), center + new Vector2(9f, -9f), color, 3f);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state));
        }
        if (selected || state != ThermalOperatingState.Continuous)
        {
            DrawThermalIcon(center + new Vector2(15f, -15f), ThermalStateIcon(state), color);
        }
    }

    private void DrawThermalTicks(
        Vector2 start,
        Vector2 end,
        Color color,
        bool crossed)
    {
        Vector2 delta = end - start;
        float length = delta.Length();
        if (length < 8f)
        {
            return;
        }
        Vector2 direction = delta / length;
        Vector2 normal = new(-direction.Y, direction.X);
        for (float offset = 18f; offset < length - 8f; offset += 28f)
        {
            Vector2 center = start + direction * offset;
            DrawLine(
                center - normal * 6f - direction * 3f,
                center + normal * 6f + direction * 3f,
                color,
                1.5f,
                true);
            if (crossed)
            {
                DrawLine(
                    center - normal * 6f + direction * 3f,
                    center + normal * 6f - direction * 3f,
                    color,
                    1.5f,
                    true);
            }
        }
    }

    private void DrawDashedSegment(
        Vector2 start,
        Vector2 end,
        Color color,
        float width,
        float dashLength,
        float gapLength)
    {
        Vector2 delta = end - start;
        float length = delta.Length();
        if (length <= 0.001f)
        {
            return;
        }
        Vector2 direction = delta / length;
        for (float offset = 0f; offset < length; offset += dashLength + gapLength)
        {
            DrawLine(
                start + direction * offset,
                start + direction * Math.Min(length, offset + dashLength),
                color,
                width,
                true);
        }
    }

    private void DrawDashedArc(Vector2 center, float radius, Color color)
    {
        const int segmentCount = 12;
        for (int index = 0; index < segmentCount; index += 2)
        {
            float from = Mathf.Tau * index / segmentCount;
            float to = Mathf.Tau * (index + 1) / segmentCount;
            DrawArc(center, radius, from, to, 5, color, 2.5f, true);
        }
    }

    private void DrawRadialTicks(Vector2 center, float radius, Color color)
    {
        for (int index = 0; index < 8; index++)
        {
            float angle = Mathf.Tau * index / 8f;
            Vector2 direction = Vector2.FromAngle(angle);
            DrawLine(
                center + direction * (radius - 3f),
                center + direction * (radius + 3f),
                color,
                1.5f,
                true);
        }
    }

    private void DrawThermalIcon(Vector2 center, string icon, Color color)
    {
        Vector2 size = GetThemeDefaultFont().GetStringSize(
            icon,
            HorizontalAlignment.Center,
            -1f,
            12);
        DrawRect(
            new Rect2(center - size / 2f - new Vector2(3f, 2f), size + new Vector2(6f, 4f)),
            new Color(Background, 0.94f));
        DrawString(
            GetThemeDefaultFont(),
            center + new Vector2(-size.X / 2f, size.Y / 3f),
            icon,
            HorizontalAlignment.Left,
            -1f,
            12,
            color);
    }

    private static Color ThermalStateColor(ThermalOperatingState state) => state switch
    {
        ThermalOperatingState.Continuous => ThermalContinuous,
        ThermalOperatingState.Emergency => ThermalEmergency,
        ThermalOperatingState.ProtectiveOutage => ThermalOutage,
        ThermalOperatingState.OverLimit => ThermalOverLimit,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static string ThermalStateIcon(ThermalOperatingState state) => state switch
    {
        ThermalOperatingState.Continuous => "✓",
        ThermalOperatingState.Emergency => "!",
        ThermalOperatingState.ProtectiveOutage => "×",
        ThermalOperatingState.OverLimit => "!!",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private void DrawPointer(CommercialMapPresentation presentation)
    {
        if (_pointerPoint is not CoreMapPoint point)
        {
            return;
        }
        Color color = presentation.PointerAccepted ? Focus : Invalid;
        CoreMapPoint displayPoint = SelectedCandidateId is string candidateId
            ? presentation.Snapshot.World.Nodes.Single(node => node.NodeId == candidateId).Position
            : point;
        Vector2 center = ToCanvas(displayPoint);
        DrawPointerSegment(presentation, displayPoint, color);
        if (presentation.PointerFootprintRadiusUnit is int radius)
        {
            DrawFootprint(displayPoint, radius, color, 0.12f);
        }
        if (presentation.PointerServiceRadiusUnit is int serviceRadius)
        {
            DrawServiceRadius(
                displayPoint,
                serviceRadius,
                "서비스 권역",
                color,
                0.45f);
        }
        DrawArc(center, 10f, 0f, Mathf.Tau, 32, color, 2f, true);
        DrawLine(center + new Vector2(-14f, 0f), center + new Vector2(14f, 0f), color, 1f);
        DrawLine(center + new Vector2(0f, -14f), center + new Vector2(0f, 14f), color, 1f);
        string resultIcon = presentation.PointerAccepted ? "✓" : "×";
        DrawThermalIcon(center + new Vector2(-16f, -17f), resultIcon, color);
        if (presentation.PointerRiskAreaIds is { Count: > 0 })
        {
            DrawThermalIcon(center + new Vector2(17f, -17f), "!", Planned);
        }

        string label = string.IsNullOrWhiteSpace(presentation.PointerMessage)
            ? CandidateLabel(presentation.Snapshot.World) ?? string.Empty
            : presentation.PointerMessage;
        if (!string.IsNullOrWhiteSpace(label))
        {
            Vector2 labelPosition = center + new Vector2(14f, 27f);
            Vector2 labelSize = GetThemeDefaultFont().GetStringSize(
                label,
                HorizontalAlignment.Left,
                -1f,
                12);
            DrawRect(new Rect2(labelPosition - new Vector2(5f, 16f), labelSize + new Vector2(10f, 8f)),
                new Color(Background, 0.92f));
            DrawString(GetThemeDefaultFont(), labelPosition, label,
                HorizontalAlignment.Left, -1f, 12, color);
        }
    }

    private void DrawPointerSegment(
        CommercialMapPresentation presentation,
        CoreMapPoint displayPoint,
        Color color)
    {
        LineDraftSnapshot? draft = presentation.Snapshot.LineDraft;
        if (!presentation.ConstructionInputEnabled ||
            draft is null ||
            draft.EndNodeId is not null)
        {
            return;
        }
        SpatialNodeDefinition? startNode = presentation.Snapshot.World.Nodes.FirstOrDefault(
            node => string.Equals(node.NodeId, draft.StartNodeId, StringComparison.Ordinal));
        if (startNode is null)
        {
            return;
        }
        CoreMapPoint segmentStart = draft.IntermediatePoints.Count == 0
            ? startNode.Position
            : draft.IntermediatePoints[^1];
        Vector2 from = ToCanvas(segmentStart);
        Vector2 to = ToCanvas(displayPoint);
        DrawLine(from, to, new Color(Background, 0.92f), 7f, true);
        if (presentation.PointerAccepted)
        {
            DrawLine(from, to, color, 2.7f, true);
            DrawThermalTicks(from, to, color, crossed: false);
        }
        else
        {
            DrawDashedSegment(from, to, color, 2.7f, 10f, 7f);
            DrawThermalTicks(from, to, color, crossed: true);
        }
    }

    private void DrawOperationalHighlight(
        SpatialWorldDefinition world,
        CommercialMapHighlightPresentation highlight)
    {
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        Dictionary<string, SpatialEdgeDefinition> edges = world.Edges.ToDictionary(
            item => item.EdgeId,
            StringComparer.Ordinal);
        foreach (string edgeId in highlight.EdgeIds)
        {
            if (!edges.TryGetValue(edgeId, out SpatialEdgeDefinition? edge) ||
                !nodes.TryGetValue(edge.FromNodeId, out SpatialNodeDefinition? from) ||
                !nodes.TryGetValue(edge.ToNodeId, out SpatialNodeDefinition? to))
            {
                continue;
            }
            Vector2 start = ToCanvas(from.Position);
            Vector2 end = ToCanvas(to.Position);
            DrawLine(start, end, new Color(Background, 0.95f), 12f, true);
            DrawLine(start, end, Focus, 6f, true);
            DrawThermalTicks(start, end, Focus, crossed: false);
        }
        foreach (string nodeId in highlight.NodeIds)
        {
            if (nodes.TryGetValue(nodeId, out SpatialNodeDefinition? node))
            {
                DrawArc(ToCanvas(node.Position), 20f, 0f, Mathf.Tau, 48, Focus, 3f, true);
            }
        }
        if (highlight.LimitingAssetId is string limitingId)
        {
            if (nodes.TryGetValue(limitingId, out SpatialNodeDefinition? limitingNode))
            {
                Vector2 center = ToCanvas(limitingNode.Position);
                DrawArc(center, 26f, 0f, Mathf.Tau, 48, Invalid, 4f, true);
                DrawThermalIcon(center + new Vector2(22f, -22f), "병목", Invalid);
            }
            else if (edges.TryGetValue(limitingId, out SpatialEdgeDefinition? limitingEdge) &&
                     nodes.TryGetValue(limitingEdge.FromNodeId, out SpatialNodeDefinition? from) &&
                     nodes.TryGetValue(limitingEdge.ToNodeId, out SpatialNodeDefinition? to))
            {
                Vector2 start = ToCanvas(from.Position);
                Vector2 end = ToCanvas(to.Position);
                DrawLine(start, end, Invalid, 4f, true);
                DrawThermalTicks(start, end, Invalid, crossed: true);
                DrawThermalIcon((start + end) / 2f, "병목", Invalid);
            }
        }
    }

    private void DrawFootprint(CoreMapPoint point, int radiusUnit, Color color, float alpha)
    {
        float radiusPixel = Math.Max(2f, (float)(radiusUnit * RequireTransform().Scale));
        Vector2 center = ToCanvas(point);
        DrawCircle(center, radiusPixel, new Color(color, alpha));
        DrawArc(center, radiusPixel, 0f, Mathf.Tau, 48, new Color(color, 0.72f), 1.2f, true);
        DrawArc(center, radiusPixel + 6f, 0f, Mathf.Tau, 48, new Color(color, 0.22f), 1f, true);
    }

    private void DrawServiceRadius(
        CoreMapPoint point,
        int radiusUnit,
        string label,
        Color color,
        float alpha)
    {
        Vector2 center = ToCanvas(point);
        float radiusPixel = Math.Max(3f, (float)(radiusUnit * RequireTransform().Scale));
        DrawDashedArc(center, radiusPixel, new Color(color, alpha));
        float labelOffset = Math.Max(10f, Math.Min(radiusPixel - 12f, 70f));
        Vector2 labelPosition = center + new Vector2(10f, -labelOffset);
        DrawString(
            GetThemeDefaultFont(),
            labelPosition,
            label,
            HorizontalAlignment.Left,
            -1f,
            11,
            new Color(color, Math.Min(1f, alpha + 0.3f)));
    }

    private void DrawMapLegend()
    {
        if (_presentation?.Thermal is CommercialThermalMapPresentation thermal)
        {
            string instruction = _presentation.ConstructionInputEnabled
                ? "운영 상태 표시 유지 · 현재 선택한 공사 위치를 지도에서 확정합니다."
                : thermal.ContinuousOnly
                    ? "설비를 선택하면 현재 사용과 연속 한계를 확인합니다."
                    : "설비를 선택하면 현재 사용과 다음 상태를 확인합니다.";
            DrawString(
                GetThemeDefaultFont(),
                new Vector2(18f, Size.Y - 28f),
                $"{thermal.ProjectionLabel} · {instruction}",
                HorizontalAlignment.Left,
                -1f,
                11,
                Muted);
            DrawString(
                GetThemeDefaultFont(),
                new Vector2(18f, Size.Y - 12f),
                thermal.ContinuousOnly
                    ? "✓ 연속 운전 실선 · 서비스 권역 점선 · 색 없이도 오른쪽 상태 문장으로 확인"
                    : "✓ 연속 실선 · ! 비상 이중선/사선 · × 보호정지 점선 · !! 한계초과 교차선",
                HorizontalAlignment.Left,
                -1f,
                11,
                Muted);
            return;
        }
        string label = $"지도 {ZoomLabel}  ·  격자 맞춤 없음  ·  설계 거리 1 = 내부 100단위";
        DrawString(GetThemeDefaultFont(), new Vector2(18f, Size.Y - 13f), label,
            HorizontalAlignment.Left, -1f, 11, Muted);
    }

    private void DrawAreaLabel(Vector2[] polygon, string label, Color color)
    {
        float minX = polygon.Min(point => point.X);
        float minY = polygon.Min(point => point.Y);
        DrawString(GetThemeDefaultFont(), new Vector2(minX + 7f, minY + 17f), label,
            HorizontalAlignment.Left, -1f, 11, new Color(color, 0.9f));
    }

    private void DrawPolygonHatching(Vector2[] polygon, Color color, float spacing, float alpha)
    {
        float minX = polygon.Min(point => point.X);
        float maxX = polygon.Max(point => point.X);
        float minY = polygon.Min(point => point.Y);
        float maxY = polygon.Max(point => point.Y);
        for (float x = minX - (maxY - minY); x <= maxX; x += spacing)
        {
            var clipped = ClipLineToPolygon(
                new Vector2(x, maxY),
                new Vector2(x + (maxY - minY), minY),
                polygon);
            foreach ((Vector2 from, Vector2 to) in clipped)
            {
                DrawLine(from, to, new Color(color, alpha), 1f, true);
            }
        }
    }

    private static IReadOnlyList<(Vector2 From, Vector2 To)> ClipLineToPolygon(
        Vector2 from,
        Vector2 to,
        Vector2[] polygon)
    {
        var intersections = new List<Vector2>();
        if (Geometry2D.IsPointInPolygon(from, polygon))
        {
            intersections.Add(from);
        }
        for (int index = 0; index < polygon.Length; index++)
        {
            Variant hit = Geometry2D.SegmentIntersectsSegment(
                from,
                to,
                polygon[index],
                polygon[(index + 1) % polygon.Length]);
            if (hit.VariantType == Variant.Type.Vector2)
            {
                intersections.Add(hit.AsVector2());
            }
        }
        if (Geometry2D.IsPointInPolygon(to, polygon))
        {
            intersections.Add(to);
        }
        intersections = intersections
            .DistinctBy(point => (Mathf.RoundToInt(point.X * 10f), Mathf.RoundToInt(point.Y * 10f)))
            .OrderBy(point => point.DistanceSquaredTo(from))
            .ToList();
        var segments = new List<(Vector2 From, Vector2 To)>();
        for (int index = 0; index + 1 < intersections.Count; index += 2)
        {
            segments.Add((intersections[index], intersections[index + 1]));
        }
        return segments;
    }

    private string? CandidateLabel(SpatialWorldDefinition world)
    {
        if (SelectedCandidateId is not string nodeId)
        {
            return null;
        }
        SpatialNodeDefinition node = world.Nodes.Single(item => item.NodeId == nodeId);
        SpatialNodeClassDefinition nodeClass = world.NodeClasses.Single(
            item => item.ClassId == node.ClassId);
        string kind = nodeClass.Kind switch
        {
            SpatialNodeKind.SourceTerminal => "발전 접속점",
            SpatialNodeKind.Substation => "변전소",
            SpatialNodeKind.DedicatedLoadTerminal => "수요 접속점",
            SpatialNodeKind.Pole => "전신주",
            _ => "접속 설비",
        };
        string location = $"위치 {FormatMapCoordinate(node.Position.XUnit)}, " +
            FormatMapCoordinate(node.Position.YUnit);
        return $"후보 {_candidateIndex + 1}/{_candidateNodeIds.Count} · {kind} · " +
            $"{node.DisplayName} · {location}" +
            (_candidateNodeIds.Count > 1 ? " · Q/E 변경" : string.Empty) +
            " · Enter 확정";
    }

    private string BuildAccessibilityName(CommercialMapPresentation presentation)
    {
        if (presentation.ConstructionInputEnabled)
        {
            string pointer = "지도 밖";
            if (_pointerPoint is CoreMapPoint)
            {
                string? candidate = CandidateLabel(presentation.Snapshot.World);
                pointer = candidate is null
                    ? presentation.PointerMessage
                    : $"{candidate}. 현재 판정: {presentation.PointerMessage}";
            }
            string thermalContext = presentation.Thermal is CommercialThermalMapPresentation overlay
                ? overlay.ContinuousOnly
                    ? $" {overlay.ProjectionLabel} 연속 운전 상태 표시 중."
                    : $" {overlay.ProjectionLabel} 열 상태 표시 중."
                : string.Empty;
            string cityContext = presentation.City is CommercialCityMapPresentation city
                ? $" {city.AccessibilitySummary}"
                : string.Empty;
            string highlightContext = presentation.Highlight is CommercialMapHighlightPresentation highlight
                ? $" {highlight.AccessibilitySummary}"
                : string.Empty;
            return $"청류시 자유 배치 지도. {presentation.ToolLabel}. {pointer}." +
                $"{thermalContext}{cityContext}{highlightContext} 지도 {ZoomLabel}.";
        }
        if (presentation.Thermal is CommercialThermalMapPresentation thermal)
        {
            string mode = thermal.ContinuousOnly ? "연속 운전 지도" : "열 운전 지도";
            return $"청류시 전력망 {mode}. {thermal.ProjectionLabel}. " +
                   $"{thermal.AccessibilitySummary}. " +
                   $"{presentation.City?.AccessibilitySummary ?? string.Empty} " +
                   $"{presentation.Highlight?.AccessibilitySummary ?? string.Empty} 지도 {ZoomLabel}.";
        }
        return $"청류시 지도. 선택 가능한 설비가 없습니다. 지도 {ZoomLabel}.";
    }

    private static string FormatMapCoordinate(int unit) =>
        (unit / 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private Vector2 KeyboardAnchor() => RequireTransform().WorldToCanvas(
        _keyboardPoint.XUnit,
        _keyboardPoint.YUnit);

    private void OnResized()
    {
        if (_presentation is null || _transform is null)
        {
            return;
        }
        MapBounds bounds = _presentation.Snapshot.World.Bounds;
        _transform.Configure(
            new MapViewportBounds(
                bounds.MinXUnit,
                bounds.MaxXUnit,
                bounds.MinYUnit,
                bounds.MaxYUnit),
            Size);
        RefreshCandidates(notify: true);
        QueueRedraw();
    }

    private bool IsPanButton(InputEventMouseButton button) =>
        button.ButtonIndex == MouseButton.Middle ||
        (button.ButtonIndex == MouseButton.Left && _spaceHeld);

    private void BeginPan(MouseButton button, Vector2 position)
    {
        GrabFocus();
        _panning = true;
        _panButton = button;
        _lastPanPosition = position;
        MouseDefaultCursorShape = CursorShape.Drag;
    }

    private void EndPan()
    {
        _panning = false;
        _panButton = MouseButton.None;
        MouseDefaultCursorShape = CursorShape.Arrow;
    }

    private bool TryBeginDraftPointDrag(Vector2 canvasPoint)
    {
        LineDraftSnapshot? draft = _presentation?.Snapshot.LineDraft;
        if (draft is null || draft.IntermediatePoints.Count == 0)
        {
            return false;
        }
        (int Index, float Distance) nearest = draft.IntermediatePoints
            .Select((point, index) => (
                Index: index,
                Distance: ToCanvas(point).DistanceSquaredTo(canvasPoint)))
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Index)
            .First();
        if (nearest.Distance > 14f * 14f)
        {
            return false;
        }
        GrabFocus();
        _draggingDraftPoint = true;
        _draggedDraftPointIndex = nearest.Index;
        RefreshCandidates(notify: false);
        DraftPointDragPreviewChanged?.Invoke(new CommercialDraftPointDrag(
            nearest.Index,
            draft.IntermediatePoints[nearest.Index]));
        return true;
    }

    private Vector2 ToCanvas(CoreMapPoint point) => RequireTransform().WorldToCanvas(
        point.XUnit,
        point.YUnit);

    private MapViewportTransform RequireTransform() => _transform ??
        throw new InvalidOperationException("지도가 아직 준비되지 않았습니다.");

    private static CoreMapPoint InitialKeyboardPoint(SpatialWorldDefinition world)
    {
        SpatialNodeDefinition? source = world.Nodes.FirstOrDefault(node =>
            world.NodeClasses.Any(nodeClass =>
                nodeClass.ClassId == node.ClassId &&
                nodeClass.Kind == SpatialNodeKind.SourceTerminal));
        return source?.Position ?? new CoreMapPoint(
            (int)(((long)world.Bounds.MinXUnit + world.Bounds.MaxXUnit) / 2L),
            (int)(((long)world.Bounds.MinYUnit + world.Bounds.MaxYUnit) / 2L));
    }

    private static int RoundUnit(double value)
    {
        double rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded <= int.MinValue
            ? int.MinValue
            : rounded >= int.MaxValue
                ? int.MaxValue
                : (int)rounded;
    }

    private static int SaturatingAdd(int value, int delta)
    {
        long result = (long)value + delta;
        return result <= int.MinValue
            ? int.MinValue
            : result >= int.MaxValue
                ? int.MaxValue
                : (int)result;
    }
}
