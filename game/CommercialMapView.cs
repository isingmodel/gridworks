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
    ThermalIntervalResult? ThermalInterval,
    string? SelectedThermalAssetId);

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
    private static readonly Color EmergencyLine = Color.FromHtml("f0b75e");
    private static readonly Color OutageLine = Color.FromHtml("e56e73");
    private static readonly Color OverLimitLine = Color.FromHtml("ff845d");
    private static readonly Color Planned = Color.FromHtml("efb75d");
    private static readonly Color Invalid = Color.FromHtml("ed756e");
    private static readonly Color Text = Color.FromHtml("e6eff0");
    private static readonly Color Muted = Color.FromHtml("91a3a1");
    private static readonly Color Focus = Color.FromHtml("f4d27c");
    private const float CandidateRadiusPixel = 24f;
    private const float KeyboardFollowMarginPixel = 72f;
    private const int KeyboardSmallStepUnit = 100;
    private const int KeyboardLargeStepUnit = 500;

    private CommercialMapPresentation? _presentation;
    private CommercialMapTransform? _transform;
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

    public event Action<CoreMapPoint?, string?>? PointerChanged;

    public event Action<CoreMapPoint, string?>? PointRequested;

    public event Action? UndoRequested;

    public event Action<CommercialDraftPointDrag>? DraftPointMoveRequested;

    public event Action<CommercialDraftPointDrag?>? DraftPointDragPreviewChanged;

    public event Action? CameraChanged;

    public int ZoomIndex => _transform?.ZoomIndex ?? 0;

    public string ZoomLabel => _transform?.ZoomLabel ?? "전체 보기";

    public Vector2 CameraCenter => _transform?.Center ?? Vector2.Zero;

    public CoreMapPoint KeyboardPoint => _keyboardPoint;

    public string? SelectedCandidateId => _candidateNodeIds.Count == 0
        ? null
        : _candidateNodeIds[_candidateIndex];

    public IReadOnlyList<string> CandidateNodeIds => _candidateNodeIds.AsReadOnly();

    public bool IsDraggingDraftPoint => _draggingDraftPoint;

    public int DraggedDraftPointIndex => _draggedDraftPointIndex;

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
        var gameBounds = new CommercialMapBounds(
            bounds.MinXUnit,
            bounds.MaxXUnit,
            bounds.MinYUnit,
            bounds.MaxYUnit);
        if (_transform is null)
        {
            _transform = new CommercialMapTransform(gameBounds, Size);
            _keyboardPoint = InitialKeyboardPoint(presentation.Snapshot.World);
            _pointerPoint = _keyboardPoint;
        }
        else
        {
            _transform.Configure(gameBounds, Size);
        }

        if (presentation.PointerPoint is CoreMapPoint presentedPointer)
        {
            _pointerPoint = presentedPointer;
        }
        RefreshCandidates(notify: false);
        AccessibilityName = BuildAccessibilityName(presentation);
        QueueRedraw();
    }

    public Vector2 ViewportPointForWorld(CoreMapPoint point)
    {
        CommercialMapTransform transform = RequireTransform();
        Vector2 local = transform.WorldToCanvas(point.XUnit, point.YUnit);
        return GetGlobalTransformWithCanvas() * local;
    }

    public CommercialWorldPosition WorldAtViewportPoint(Vector2 viewportPoint)
    {
        Vector2 local = GetGlobalTransformWithCanvas().AffineInverse() * viewportPoint;
        return RequireTransform().CanvasToWorld(local);
    }

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
                UndoRequested?.Invoke();
                AcceptEvent();
                return;

            case InputEventMouseButton button when
                button.ButtonIndex == MouseButton.Left && button.Pressed:
                if (TryMapPoint(button.Position, out CoreMapPoint clicked))
                {
                    GrabFocus();
                    _keyboardPoint = clicked;
                    SetPointer(clicked);
                    PointRequested?.Invoke(clicked, SelectedCandidateId);
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
        DrawRect(new Rect2(Vector2.Zero, Size), Background);
        DrawMapGround();
        DrawTerrain(snapshot.World);
        DrawRiskAreas(snapshot.World);
        DrawEdges(snapshot.World, _presentation.ThermalInterval);
        DrawLineDraft(snapshot);
        DrawNodes(
            snapshot.World,
            _presentation.ThermalInterval,
            _presentation.SelectedThermalAssetId);
        DrawNodeDraft(snapshot);
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
        if (physical == Key.Q || physical == Key.E)
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
            UndoRequested?.Invoke();
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
            PointRequested?.Invoke(_keyboardPoint, SelectedCandidateId);
            AcceptEvent();
        }
    }

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
        CommercialMapTransform transform = RequireTransform();
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

    private bool TryMapPoint(Vector2 canvasPoint, out CoreMapPoint point)
    {
        CommercialMapTransform transform = RequireTransform();
        if (!transform.PlotRect.HasPoint(canvasPoint))
        {
            point = default;
            return false;
        }
        CommercialWorldPosition world = transform.CanvasToWorld(canvasPoint);
        MapBounds bounds = _presentation!.Snapshot.World.Bounds;
        point = new CoreMapPoint(
            Math.Clamp(RoundUnit(world.X), bounds.MinXUnit, bounds.MaxXUnit),
            Math.Clamp(RoundUnit(world.Y), bounds.MinYUnit, bounds.MaxYUnit));
        return true;
    }

    private void DrawMapGround()
    {
        CommercialMapTransform transform = RequireTransform();
        MapBounds bounds = _presentation!.Snapshot.World.Bounds;
        Vector2 topLeft = transform.WorldToCanvas(bounds.MinXUnit, bounds.MinYUnit);
        Vector2 bottomRight = transform.WorldToCanvas(bounds.MaxXUnit, bounds.MaxYUnit);
        Rect2 mapRect = new(topLeft, bottomRight - topLeft);
        DrawRect(mapRect, Land);
        DrawRect(mapRect, LandEdge, false, 1.5f);

        for (int index = 1; index <= 4; index++)
        {
            float ratio = index / 5f;
            Vector2 from = new(
                Mathf.Lerp(topLeft.X, bottomRight.X, ratio),
                Mathf.Lerp(topLeft.Y, bottomRight.Y, 0.12f));
            Vector2 to = new(
                Mathf.Lerp(topLeft.X, bottomRight.X, ratio - 0.12f),
                Mathf.Lerp(topLeft.Y, bottomRight.Y, 0.88f));
            DrawLine(from, to, new Color(LandEdge, 0.17f), 1f, true);
        }
    }

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
            }
            DrawAreaLabel(polygon, area.DisplayName, edge);
        }
    }

    private void DrawRiskAreas(SpatialWorldDefinition world)
    {
        foreach (SpatialRiskAreaDefinition area in world.RiskAreas)
        {
            Vector2[] polygon = area.Polygon.Select(ToCanvas).ToArray();
            DrawColoredPolygon(polygon, new Color(Risk, 0.10f));
            DrawPolyline(polygon.Append(polygon[0]).ToArray(), new Color(Risk, 0.85f), 1.5f, true);
            DrawPolygonHatching(polygon, Risk, 18f, 0.32f);
            DrawAreaLabel(polygon, area.DisplayName, Risk);
        }
    }

    private void DrawEdges(
        SpatialWorldDefinition world,
        ThermalIntervalResult? thermalInterval)
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
            ThermalAssetResult? thermal = thermalInterval?.Assets.FirstOrDefault(item =>
                item.AssetId == edge.EdgeId);
            Color color = !edge.Commissioned
                ? Planned
                : ThermalColor(thermal?.CurrentState);
            DrawLine(start, end, new Color(Background, 0.9f), 6f, true);
            if (thermal?.CurrentState == ThermalOperatingState.ProtectiveOutage)
            {
                DrawDashedLine(start, end, color, 3f, 9f, true, true);
                DrawCross((start + end) / 2f, color, 7f);
            }
            else if (thermal?.CurrentState == ThermalOperatingState.Emergency)
            {
                DrawLine(start, end, color, 4.5f, true);
                DrawDashedLine(start, end, Background, 1.3f, 7f, true, true);
            }
            else
            {
                DrawLine(start, end, color, edge.Commissioned ? 3f : 2.5f, true);
            }
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
        DrawArc(
            ToCanvas(currentSegmentStart),
            spanRadiusPixel,
            0f,
            Mathf.Tau,
            72,
            new Color(Planned, 0.24f),
            1.2f,
            true);
        foreach (CoreMapPoint point in draft.IntermediatePoints)
        {
            DrawFootprint(point, poleClass.FootprintRadiusUnit, Planned, 0.12f);
            DrawCircle(ToCanvas(point), 4.5f, Planned);
        }
    }

    private void DrawNodes(
        SpatialWorldDefinition world,
        ThermalIntervalResult? thermalInterval,
        string? selectedThermalAssetId)
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
            ThermalAssetResult? thermal = thermalInterval?.Assets.FirstOrDefault(item =>
                item.AssetId == node.NodeId);
            Color color = node.Commissioned ? ThermalColor(thermal?.CurrentState) : Planned;
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
            if (thermal?.CurrentState == ThermalOperatingState.Emergency)
            {
                Vector2[] triangle =
                [
                    center + new Vector2(0f, -radius - 7f),
                    center + new Vector2(-5f, -radius + 2f),
                    center + new Vector2(5f, -radius + 2f),
                ];
                DrawPolyline(triangle.Append(triangle[0]).ToArray(), color, 2f, true);
            }
            else if (thermal?.CurrentState == ThermalOperatingState.ProtectiveOutage)
            {
                DrawCross(center, color, radius + 5f);
            }
            if (node.NodeId == selectedThermalAssetId)
            {
                DrawArc(center, radius + 7f, 0f, Mathf.Tau, 32, Focus, 2f, true);
            }
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
        DrawCircle(ToCanvas(draft.Position), 6f, Planned);
    }

    private static Color ThermalColor(ThermalOperatingState? state) => state switch
    {
        ThermalOperatingState.Emergency => EmergencyLine,
        ThermalOperatingState.ProtectiveOutage => OutageLine,
        ThermalOperatingState.OverLimit => OverLimitLine,
        _ => CommissionedLine,
    };

    private void DrawCross(Vector2 center, Color color, float radius)
    {
        DrawLine(
            center + new Vector2(-radius, -radius),
            center + new Vector2(radius, radius),
            color,
            2f,
            true);
        DrawLine(
            center + new Vector2(-radius, radius),
            center + new Vector2(radius, -radius),
            color,
            2f,
            true);
    }

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
        if (presentation.PointerFootprintRadiusUnit is int radius)
        {
            DrawFootprint(displayPoint, radius, color, 0.12f);
        }
        DrawArc(center, 10f, 0f, Mathf.Tau, 32, color, 2f, true);
        DrawLine(center + new Vector2(-14f, 0f), center + new Vector2(14f, 0f), color, 1f);
        DrawLine(center + new Vector2(0f, -14f), center + new Vector2(0f, 14f), color, 1f);

        string label = CandidateLabel(presentation.Snapshot.World) ?? presentation.PointerMessage;
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

    private void DrawFootprint(CoreMapPoint point, int radiusUnit, Color color, float alpha)
    {
        float radiusPixel = Math.Max(2f, (float)(radiusUnit * RequireTransform().Scale));
        Vector2 center = ToCanvas(point);
        DrawCircle(center, radiusPixel, new Color(color, alpha));
        DrawArc(center, radiusPixel, 0f, Mathf.Tau, 48, new Color(color, 0.72f), 1.2f, true);
        DrawArc(center, radiusPixel + 6f, 0f, Mathf.Tau, 48, new Color(color, 0.22f), 1f, true);
    }

    private void DrawMapLegend()
    {
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
        return _candidateNodeIds.Count == 1
            ? $"접속 · {node.DisplayName}"
            : $"접속 {_candidateIndex + 1}/{_candidateNodeIds.Count} · {node.DisplayName} · Q/E 변경";
    }

    private string BuildAccessibilityName(CommercialMapPresentation presentation)
    {
        string pointer = _pointerPoint is CoreMapPoint
            ? CandidateLabel(presentation.Snapshot.World) ?? presentation.PointerMessage
            : "지도 밖";
        string thermal = string.Empty;
        if (presentation.ThermalInterval is ThermalIntervalResult interval)
        {
            int emergency = interval.Assets.Count(item =>
                item.CurrentState == ThermalOperatingState.Emergency);
            int outage = interval.Assets.Count(item =>
                item.CurrentState == ThermalOperatingState.ProtectiveOutage);
            thermal = $" 열 상태: 비상 {emergency}곳, 보호정지 {outage}곳.";
        }
        return $"청류시 자유 배치 지도. {presentation.ToolLabel}. {pointer}. 지도 {ZoomLabel}.{thermal}";
    }

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
            new CommercialMapBounds(
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

    private CommercialMapTransform RequireTransform() => _transform ??
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
