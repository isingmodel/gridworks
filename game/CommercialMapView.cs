using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Godot;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game;

internal enum CommercialFacilityState
{
    Waiting,
    Supplied,
    Deferred,
    Unavailable,
}

internal sealed record CommercialFacilityPresentation(
    string NodeId,
    CommercialFacilityState State,
    string StatusText);

internal sealed record CommercialMapPresentation(
    ConstructionSnapshot Snapshot,
    CoreMapPoint? PointerPoint,
    bool PointerAccepted,
    string PointerMessage,
    string ToolLabel,
    int? PointerFootprintRadiusUnit,
    bool NodeSnapEnabled,
    ThermalIntervalResult? ThermalInterval,
    string? SelectedThermalAssetId,
    string? SelectedDemandNodeId,
    IReadOnlyList<string> SelectedPathNodeIds,
    IReadOnlyList<string> SelectedPathEdgeIds,
    IReadOnlyList<string> ActiveRiskAreaIds,
    IReadOnlyList<CommercialFacilityPresentation> Facilities,
    int ChapterIndex,
    bool ReduceMotion);

internal readonly record struct CommercialDraftPointDrag(int PointIndex, CoreMapPoint Position);

internal sealed partial class CommercialMapView : Control
{
    private static readonly Color Background = Color.FromHtml("071319");
    private static readonly Color Land = Color.FromHtml("142724");
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
    private const float CandidateRadiusPixel = 36f;
    private const float KeyboardFollowMarginPixel = 72f;
    private const int KeyboardSmallStepUnit = 100;
    private const int KeyboardLargeStepUnit = 500;
    private const int GroundTileWorldUnit = 400;

    [Export]
    public Texture2D? GroundAsphaltTile { get; set; }

    [Export]
    public Texture2D? GroundScrubTile { get; set; }

    [Export]
    public Texture2D? GroundConcreteTile { get; set; }

    [Export]
    public Texture2D? GroundGravelTile { get; set; }

    [Export]
    public Texture2D? RiverWaterTile { get; set; }

    [Export]
    public Texture2D? ResidentialBlockTile { get; set; }

    [Export]
    public Texture2D? HospitalBlockTile { get; set; }

    [Export]
    public Texture2D? SourcePlantSprite { get; set; }

    [Export]
    public Texture2D? StandardPoleSprite { get; set; }

    [Export]
    public Texture2D? ReinforcedPoleSprite { get; set; }

    [Export]
    public Texture2D? BridgeFoundationSprite { get; set; }

    [Export]
    public Texture2D? SubstationSprite { get; set; }

    [Export]
    public Texture2D? ResidentialFacilitySprite { get; set; }

    [Export]
    public Texture2D? HospitalFacilitySprite { get; set; }

    [Export]
    public Texture2D? WaterFacilitySprite { get; set; }

    [Export]
    public Texture2D? IndustryFacilitySprite { get; set; }

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
    private double _animationSeconds;
    private double _redrawAccumulator;

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

    public bool HasIndividualTileAssets =>
        GroundAsphaltTile is not null &&
        GroundScrubTile is not null &&
        GroundConcreteTile is not null &&
        GroundGravelTile is not null &&
        RiverWaterTile is not null &&
        ResidentialBlockTile is not null &&
        HospitalBlockTile is not null;

    public bool HasIndividualObjectAssets =>
        SourcePlantSprite is not null &&
        StandardPoleSprite is not null &&
        ReinforcedPoleSprite is not null &&
        BridgeFoundationSprite is not null &&
        SubstationSprite is not null &&
        ResidentialFacilitySprite is not null &&
        HospitalFacilitySprite is not null &&
        WaterFacilitySprite is not null &&
        IndustryFacilitySprite is not null;

    public int IndividualArtAssetCount =>
        IndividualTileAssetCount + IndividualObjectAssetCount;

    private int IndividualTileAssetCount =>
        new Texture2D?[]
        {
            GroundAsphaltTile,
            GroundScrubTile,
            GroundConcreteTile,
            GroundGravelTile,
            RiverWaterTile,
            ResidentialBlockTile,
            HospitalBlockTile,
        }.Count(texture => texture is not null);

    private int IndividualObjectAssetCount =>
        new Texture2D?[]
        {
            SourcePlantSprite,
            StandardPoleSprite,
            ReinforcedPoleSprite,
            BridgeFoundationSprite,
            SubstationSprite,
            ResidentialFacilitySprite,
            HospitalFacilitySprite,
            WaterFacilitySprite,
            IndustryFacilitySprite,
        }.Count(texture => texture is not null);

    public bool IsDraggingDraftPoint => _draggingDraftPoint;

    public int DraggedDraftPointIndex => _draggedDraftPointIndex;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        TextureRepeat = TextureRepeatEnum.Enabled;
        AccessibilityDescription =
            "청류시 자유 배치 지도. 개별 지형 타일과 시설 오브젝트가 v2 좌표에 배치됩니다. " +
            "방향키로 커서를 움직이고 Enter로 선택합니다. Q와 E로 가까운 접속점을 바꿉니다.";
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

    public override void _Process(double delta)
    {
        if (_presentation?.ReduceMotion != false)
        {
            return;
        }
        _animationSeconds += delta;
        _redrawAccumulator += delta;
        if (_redrawAccumulator >= 1d / 12d)
        {
            _redrawAccumulator = 0d;
            QueueRedraw();
        }
    }

    public void SetPresentation(CommercialMapPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        _presentation = presentation;
        SetProcess(!presentation.ReduceMotion);
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
        DrawRiskAreas(snapshot.World, _presentation.ActiveRiskAreaIds);
        DrawChapterAtmosphere(_presentation.ChapterIndex, _presentation.ReduceMotion);
        DrawEdges(snapshot.World, _presentation.ThermalInterval);
        DrawSelectedDemandPath(snapshot.World, _presentation);
        DrawUnavailableMarks(snapshot.World, _presentation.ThermalInterval);
        DrawLineDraft(snapshot);
        DrawNodes(
            snapshot.World,
            _presentation.ThermalInterval,
            _presentation.SelectedThermalAssetId,
            _presentation.SelectedDemandNodeId,
            _presentation.Facilities);
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
                .Where(node => node.Commissioned)
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

        Texture2D?[] variants =
        {
            GroundScrubTile,
            GroundConcreteTile,
            GroundGravelTile,
        };
        int row = 0;
        for (int y = bounds.MinYUnit; y < bounds.MaxYUnit; y += GroundTileWorldUnit, row++)
        {
            int column = 0;
            int nextY = Math.Min(bounds.MaxYUnit, y + GroundTileWorldUnit);
            for (int x = bounds.MinXUnit; x < bounds.MaxXUnit; x += GroundTileWorldUnit, column++)
            {
                int nextX = Math.Min(bounds.MaxXUnit, x + GroundTileWorldUnit);
                Vector2 cellTopLeft = transform.WorldToCanvas(x, y);
                Vector2 cellBottomRight = transform.WorldToCanvas(nextX, nextY);
                Rect2 cell = new(cellTopLeft, cellBottomRight - cellTopLeft);
                if (GroundAsphaltTile is not null)
                {
                    DrawTextureRect(
                        GroundAsphaltTile,
                        cell.Grow(0.65f),
                        false,
                        new Color(0.66f, 0.71f, 0.66f, 0.94f));
                }
                Texture2D? variant = variants[((column * 7) + (row * 11)) % variants.Length];
                if (variant is not null)
                {
                    DrawTextureRect(
                        variant,
                        cell.Grow(0.8f),
                        false,
                        new Color(0.66f, 0.70f, 0.64f, 0.18f));
                }
            }
        }
        DrawRect(mapRect, new Color(Land, 0.24f));
        DrawRect(mapRect, Color.FromHtml("9a7b4f"), false, 2f);
    }

    private void DrawChapterAtmosphere(int chapterIndex, bool reduceMotion)
    {
        string[] labels =
        [
            "새벽 · 첫 입주",
            "맑음 · 북안 점검",
            "바람 · 전원 전환",
            "저녁 · 입주 약속",
            "폭염 · 열여유 경계",
            "폭우 · 강 수위 상승",
            "갬 · 3주 뒤",
            "겨울밤 · 마지막 우회",
        ];
        int index = Math.Clamp(chapterIndex, 0, labels.Length - 1);
        Color tint = index switch
        {
            0 => new Color(Color.FromHtml("304b59"), 0.12f),
            3 => new Color(Color.FromHtml("855f38"), 0.08f),
            4 => new Color(Color.FromHtml("ba6c3e"), 0.10f),
            5 => new Color(Color.FromHtml("264a62"), 0.16f),
            7 => new Color(Color.FromHtml("1b2851"), 0.20f),
            _ => new Color(Color.FromHtml("31564f"), 0.06f),
        };
        DrawRect(new Rect2(Vector2.Zero, Size), tint);

        if (index is 5 or 7)
        {
            float phase = reduceMotion ? 0f : (float)((_animationSeconds * 28d) % 34d);
            for (float x = -Size.Y; x < Size.X + Size.Y; x += 34f)
            {
                Vector2 from = new(x + phase, 0f);
                Vector2 to = new(x - Size.Y + phase, Size.Y);
                DrawLine(from, to, new Color(WaterLine, index == 5 ? 0.16f : 0.09f), 1f, true);
            }
        }
        else if (index == 4)
        {
            for (float y = 44f; y < Size.Y; y += 52f)
            {
                DrawDashedLine(
                    new Vector2(0f, y),
                    new Vector2(Size.X, y),
                    new Color(EmergencyLine, 0.08f),
                    1f,
                    18f);
            }
        }

        string motion = reduceMotion ? " · 움직임 줄임" : string.Empty;
        string label = $"장면 · {labels[index]}{motion}";
        Vector2 labelSize = GetThemeDefaultFont().GetStringSize(
            label,
            HorizontalAlignment.Left,
            -1f,
            12);
        Vector2 position = new(Size.X - labelSize.X - 18f, 24f);
        DrawRect(
            new Rect2(position - new Vector2(7f, 16f), labelSize + new Vector2(14f, 8f)),
            new Color(Background, 0.84f));
        DrawString(GetThemeDefaultFont(), position, label,
            HorizontalAlignment.Left, -1f, 12, Text);
    }

    private void DrawTerrain(SpatialWorldDefinition world)
    {
        foreach (TerrainPolygonDefinition area in world.Terrain)
        {
            Vector2[] polygon = area.Polygon.Select(ToCanvas).ToArray();
            Texture2D? texture = TerrainTexture(area.TerrainId);
            Color fill = area.Kind == TerrainKind.Water
                ? new Color(Water, 0.62f)
                : new Color(Building, 0.18f);
            Color edge = area.Kind == TerrainKind.Water ? WaterLine : BuildingEdge;
            if (texture is null)
            {
                DrawColoredPolygon(polygon, fill);
            }
            else
            {
                int minX = area.Polygon.Min(point => point.XUnit);
                int maxX = area.Polygon.Max(point => point.XUnit);
                int minY = area.Polygon.Min(point => point.YUnit);
                int maxY = area.Polygon.Max(point => point.YUnit);
                Vector2[] uvs = area.Polygon
                    .Select(point => new Vector2(
                        (float)(point.XUnit - minX) / Math.Max(1, maxX - minX) *
                            Math.Max(1, texture.GetWidth() - 1),
                        (float)(point.YUnit - minY) / Math.Max(1, maxY - minY) *
                            Math.Max(1, texture.GetHeight() - 1)))
                    .ToArray();
                Color textureModulate = area.Kind == TerrainKind.Water
                    ? new Color(0.64f, 0.84f, 0.88f, 0.96f)
                    : new Color(0.88f, 0.88f, 0.82f, 0.96f);
                if (area.Kind == TerrainKind.Building &&
                    area.Polygon.All(point =>
                        (point.XUnit == minX || point.XUnit == maxX) &&
                        (point.YUnit == minY || point.YUnit == maxY)))
                {
                    Vector2 textureTopLeft = ToCanvas(new CoreMapPoint(minX, minY));
                    Vector2 textureBottomRight = ToCanvas(new CoreMapPoint(maxX, maxY));
                    DrawTextureRect(
                        texture,
                        new Rect2(textureTopLeft, textureBottomRight - textureTopLeft),
                        false,
                        textureModulate);
                }
                else
                {
                    DrawColoredPolygon(polygon, textureModulate, uvs, texture);
                }
                DrawColoredPolygon(polygon, new Color(fill, 0.10f));
            }
            DrawPolyline(polygon.Append(polygon[0]).ToArray(), edge, 1.6f, true);
            if (area.Kind == TerrainKind.Water)
            {
                DrawPolygonHatching(polygon, WaterLine, 24f, 0.18f);
            }
            DrawAreaLabel(polygon, area.DisplayName, edge);
        }
    }

    private Texture2D? TerrainTexture(string terrainId) => terrainId switch
    {
        "CHEONGRYU_RIVER" => RiverWaterTile,
        "EAST_RESIDENTIAL_BLOCK" => ResidentialBlockTile,
        "HOSPITAL_BLOCK" => HospitalBlockTile,
        _ => null,
    };

    private void DrawRiskAreas(
        SpatialWorldDefinition world,
        IReadOnlyList<string> activeRiskAreaIds)
    {
        HashSet<string> active = activeRiskAreaIds.ToHashSet(StringComparer.Ordinal);
        foreach (SpatialRiskAreaDefinition area in world.RiskAreas)
        {
            Vector2[] polygon = area.Polygon.Select(ToCanvas).ToArray();
            bool isActive = active.Contains(area.RiskAreaId);
            DrawColoredPolygon(polygon, new Color(Risk, isActive ? 0.12f : 0.018f));
            DrawPolyline(
                polygon.Append(polygon[0]).ToArray(),
                new Color(isActive ? Risk : BrassRisk(), isActive ? 0.92f : 0.24f),
                isActive ? 2f : 1f,
                true);
            if (isActive)
            {
                DrawPolygonHatching(polygon, Risk, 20f, 0.28f);
                DrawAreaLabel(polygon, $"사건 구역 · {area.DisplayName}", Risk);
            }
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

    private void DrawSelectedDemandPath(
        SpatialWorldDefinition world,
        CommercialMapPresentation presentation)
    {
        if (presentation.SelectedPathEdgeIds.Count == 0)
        {
            return;
        }
        HashSet<string> selected = presentation.SelectedPathEdgeIds.ToHashSet(StringComparer.Ordinal);
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            node => node.NodeId,
            StringComparer.Ordinal);
        foreach (SpatialEdgeDefinition edge in world.Edges.Where(edge => selected.Contains(edge.EdgeId)))
        {
            if (!nodes.TryGetValue(edge.FromNodeId, out SpatialNodeDefinition? from) ||
                !nodes.TryGetValue(edge.ToNodeId, out SpatialNodeDefinition? to))
            {
                continue;
            }
            Vector2 start = ToCanvas(from.Position);
            Vector2 end = ToCanvas(to.Position);
            DrawLine(start, end, new Color(Focus, 0.45f), 9f, true);
            DrawLine(start, end, CommissionedLine, 4f, true);
            float distance = start.DistanceTo(end);
            if (distance <= 1f)
            {
                continue;
            }
            float phase = presentation.ReduceMotion
                ? 0.5f
                : (float)((_animationSeconds * 0.18d) % 1d);
            int count = Math.Max(1, Mathf.FloorToInt(distance / 72f));
            for (int dot = 0; dot < count; dot++)
            {
                float ratio = (phase + (dot / (float)count)) % 1f;
                DrawCircle(start.Lerp(end, ratio), 3.2f, Focus);
            }
        }
        foreach (string nodeId in presentation.SelectedPathNodeIds)
        {
            if (nodes.TryGetValue(nodeId, out SpatialNodeDefinition? node))
            {
                DrawArc(ToCanvas(node.Position), 13f, 0f, Mathf.Tau, 28, Focus, 2f, true);
            }
        }
    }

    private void DrawUnavailableMarks(
        SpatialWorldDefinition world,
        ThermalIntervalResult? interval)
    {
        if (interval is null)
        {
            return;
        }
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        foreach (ThermalAssetResult asset in interval.Assets.Where(item => item.AuthoredUnavailable))
        {
            Vector2? center = null;
            if (nodes.TryGetValue(asset.AssetId, out SpatialNodeDefinition? node))
            {
                center = ToCanvas(node.Position);
            }
            else
            {
                SpatialEdgeDefinition? edge = world.Edges.FirstOrDefault(item => item.EdgeId == asset.AssetId);
                if (edge is not null &&
                    nodes.TryGetValue(edge.FromNodeId, out SpatialNodeDefinition? from) &&
                    nodes.TryGetValue(edge.ToNodeId, out SpatialNodeDefinition? to))
                {
                    center = (ToCanvas(from.Position) + ToCanvas(to.Position)) / 2f;
                }
            }
            if (center is Vector2 point)
            {
                DrawRect(new Rect2(point - new Vector2(10f, 8f), new Vector2(20f, 16f)),
                    new Color(Background, 0.88f));
                DrawCross(point, OutageLine, 7f);
                DrawString(GetThemeDefaultFont(), point + new Vector2(12f, -7f), "정비·잠금",
                    HorizontalAlignment.Left, -1f, 11, OutageLine);
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
        string? selectedThermalAssetId,
        string? selectedDemandNodeId,
        IReadOnlyList<CommercialFacilityPresentation> facilities)
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
            Color color = node.Reserved
                ? Muted
                : node.Commissioned
                    ? ThermalColor(thermal?.CurrentState)
                    : Planned;
            if (node.Commissioned && node.NodeId == selectedThermalAssetId &&
                nodeClass.ServiceRadiusUnit > 0)
            {
                DrawServiceArea(node.Position, nodeClass.ServiceRadiusUnit);
            }
            DrawFootprint(node.Position, nodeClass.FootprintRadiusUnit, color, 0.08f);
            Vector2 center = ToCanvas(node.Position);
            float radius = nodeClass.Kind switch
            {
                SpatialNodeKind.SourceTerminal => 16f,
                SpatialNodeKind.Substation => 14f,
                SpatialNodeKind.DedicatedLoadTerminal => 13f,
                _ => 6f,
            };
            (Texture2D? texture, float maxSide) = NodeSprite(node, nodeClass);
            Vector2 spriteSize = texture is null
                ? Vector2.One * (radius * 2f)
                : FitSpriteSize(texture, maxSide * (1f + (ZoomIndex * 0.16f)));
            float objectRadius = Math.Max(radius, Math.Max(spriteSize.X, spriteSize.Y) * 0.42f);
            if (texture is not null)
            {
                DrawTextureRect(
                    texture,
                    new Rect2(center - (spriteSize / 2f), spriteSize),
                    false,
                    NodeSpriteModulate(node, thermal));
            }
            DrawArc(center, objectRadius, 0f, Mathf.Tau, 32, new Color(color, 0.88f), 2f, true);
            DrawCircle(center, 4.2f, new Color(Background, 0.86f));
            DrawCircle(center, 2.7f, color);
            if (nodeClass.Kind == SpatialNodeKind.DedicatedLoadTerminal)
            {
                CommercialFacilityPresentation facility = facilities.FirstOrDefault(item =>
                    item.NodeId == node.NodeId) ?? new CommercialFacilityPresentation(
                        node.NodeId,
                        CommercialFacilityState.Waiting,
                        "현재 국면 수요 없음");
                DrawFacilityStateMarker(center, objectRadius, facility);
            }
            if (thermal?.CurrentState == ThermalOperatingState.Emergency)
            {
                Vector2[] triangle =
                [
                    center + new Vector2(0f, -objectRadius - 8f),
                    center + new Vector2(-5f, -objectRadius + 1f),
                    center + new Vector2(5f, -objectRadius + 1f),
                ];
                DrawPolyline(triangle.Append(triangle[0]).ToArray(), color, 2f, true);
            }
            else if (thermal?.CurrentState == ThermalOperatingState.ProtectiveOutage)
            {
                DrawCross(center, color, objectRadius + 3f);
            }
            if (node.NodeId == selectedThermalAssetId)
            {
                DrawArc(center, objectRadius + 6f, 0f, Mathf.Tau, 36, Focus, 2f, true);
            }
            if (node.NodeId == selectedDemandNodeId)
            {
                DrawRect(new Rect2(
                        center - new Vector2(objectRadius + 8f, objectRadius + 8f),
                        Vector2.One * (objectRadius + 8f) * 2f),
                    Focus, false, 2f);
            }
            if (nodeClass.Kind != SpatialNodeKind.Pole)
            {
                DrawMapLabel(
                    center + new Vector2(objectRadius + 8f, -objectRadius + 2f),
                    node.Reserved ? $"예정 · {node.DisplayName}" : node.DisplayName,
                    node.Reserved ? Muted : Text);
            }
        }
    }

    private (Texture2D? Texture, float MaxSide) NodeSprite(
        SpatialNodeDefinition node,
        SpatialNodeClassDefinition nodeClass)
    {
        if (node.AuthoredFoundation)
        {
            return (BridgeFoundationSprite, 64f);
        }
        return nodeClass.Kind switch
        {
            SpatialNodeKind.SourceTerminal => (SourcePlantSprite, 72f),
            SpatialNodeKind.Substation => (SubstationSprite, 70f),
            SpatialNodeKind.Pole when node.ClassId == "STANDARD_POLE" =>
                (StandardPoleSprite, 52f),
            SpatialNodeKind.Pole => (ReinforcedPoleSprite, 58f),
            SpatialNodeKind.DedicatedLoadTerminal when
                node.NodeId == "EAST_RESIDENTIAL_TERMINAL" =>
                (ResidentialFacilitySprite, 76f),
            SpatialNodeKind.DedicatedLoadTerminal when
                node.NodeId == "HOSPITAL_TERMINAL" =>
                (HospitalFacilitySprite, 82f),
            SpatialNodeKind.DedicatedLoadTerminal when
                node.NodeId == "WATER_TERMINAL" =>
                (WaterFacilitySprite, 82f),
            SpatialNodeKind.DedicatedLoadTerminal when
                node.NodeId == "INDUSTRY_TERMINAL" =>
                (IndustryFacilitySprite, 80f),
            _ => (null, 24f),
        };
    }

    private static Vector2 FitSpriteSize(Texture2D texture, float maxSide)
    {
        float width = Math.Max(1, texture.GetWidth());
        float height = Math.Max(1, texture.GetHeight());
        float scale = maxSide / Math.Max(width, height);
        return new Vector2(width * scale, height * scale);
    }

    private static Color NodeSpriteModulate(
        SpatialNodeDefinition node,
        ThermalAssetResult? thermal)
    {
        if (thermal?.CurrentState == ThermalOperatingState.ProtectiveOutage)
        {
            return new Color(0.74f, 0.48f, 0.48f, 0.54f);
        }
        if (node.Reserved)
        {
            return new Color(0.56f, 0.61f, 0.59f, 0.42f);
        }
        if (!node.Commissioned)
        {
            return new Color(0.86f, 0.72f, 0.48f, 0.62f);
        }
        return new Color(0.94f, 0.96f, 0.92f, 0.98f);
    }

    private void DrawFacilityStateMarker(
        Vector2 center,
        float objectRadius,
        CommercialFacilityPresentation facility)
    {
        Color stateColor = facility.State switch
        {
            CommercialFacilityState.Supplied => CommissionedLine,
            CommercialFacilityState.Deferred => Muted,
            CommercialFacilityState.Unavailable => OutageLine,
            _ => IdleLine,
        };
        Vector2 badge = center + new Vector2(objectRadius * 0.72f, -objectRadius * 0.72f);
        DrawCircle(badge, 7f, new Color(Background, 0.88f));
        DrawArc(badge, 6f, 0f, Mathf.Tau, 20, stateColor, 2f, true);

        if (facility.State == CommercialFacilityState.Supplied)
        {
            DrawLine(badge + new Vector2(-3f, 0f), badge + new Vector2(-1f, 3f), stateColor, 2f);
            DrawLine(badge + new Vector2(-1f, 3f), badge + new Vector2(4f, -3f), stateColor, 2f);
        }
        else if (facility.State == CommercialFacilityState.Unavailable)
        {
            DrawCross(center, stateColor, objectRadius + 3f);
        }
        else if (facility.State == CommercialFacilityState.Deferred)
        {
            DrawDashedLine(badge + new Vector2(-4f, 0f), badge + new Vector2(4f, 0f),
                stateColor, 2f, 4f);
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
        if (nodeClass.ServiceRadiusUnit > 0)
        {
            DrawServiceArea(draft.Position, nodeClass.ServiceRadiusUnit);
        }
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

    private void DrawServiceArea(CoreMapPoint point, int radiusUnit)
    {
        float radiusPixel = Math.Max(2f, (float)(radiusUnit * RequireTransform().Scale));
        Vector2 center = ToCanvas(point);
        DrawCircle(center, radiusPixel, new Color(Focus, 0.035f));
        DrawArc(center, radiusPixel, 0f, Mathf.Tau, 72, new Color(Focus, 0.62f), 1.5f, true);
    }

    private void DrawMapLegend()
    {
        string label = $"지도 {ZoomLabel}  ·  격자 맞춤 없음  ·  설계 거리 1 = 내부 100단위";
        DrawString(GetThemeDefaultFont(), new Vector2(18f, Size.Y - 13f), label,
            HorizontalAlignment.Left, -1f, 11, Muted);
    }

    private void DrawMapLabel(Vector2 position, string label, Color color)
    {
        Vector2 size = GetThemeDefaultFont().GetStringSize(
            label,
            HorizontalAlignment.Left,
            -1f,
            12);
        DrawRect(
            new Rect2(position - new Vector2(5f, 15f), size + new Vector2(10f, 7f)),
            new Color(Background, 0.82f));
        DrawString(GetThemeDefaultFont(), position, label,
            HorizontalAlignment.Left, -1f, 12, color);
    }

    private static Color BrassRisk() => Color.FromHtml("8c7047");

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
        string selectedPath = presentation.SelectedDemandNodeId is null
            ? " 선택 수요 경로가 없습니다."
            : $" 선택 수요 경로는 접속점 {presentation.SelectedPathNodeIds.Count}곳과 " +
              $"선로 {presentation.SelectedPathEdgeIds.Count}구간입니다.";
        Dictionary<string, string> nodeNames = presentation.Snapshot.World.Nodes.ToDictionary(
            item => item.NodeId,
            item => item.DisplayName,
            StringComparer.Ordinal);
        string facilities = presentation.Facilities.Count == 0
            ? string.Empty
            : " 시설 상태: " + string.Join(
                ", ",
                presentation.Facilities.Select(item =>
                    $"{(nodeNames.TryGetValue(item.NodeId, out string? name) ? name : "수요 시설")} " +
                    item.StatusText)) + ".";
        string motion = presentation.ReduceMotion ? " 지도 움직임 줄임 적용." : string.Empty;
        int reserved = presentation.Snapshot.World.Nodes.Count(item => item.Reserved);
        string reservedText = reserved == 0 ? string.Empty : $" 예정 시설 {reserved}곳.";
        return $"청류시 자유 배치 지도. {presentation.ToolLabel}. {pointer}. 지도 {ZoomLabel}." +
               $"{reservedText}{thermal}{selectedPath}{facilities}{motion}";
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
