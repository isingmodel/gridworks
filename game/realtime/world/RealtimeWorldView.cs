using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Godot;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime;

internal sealed partial class RealtimeWorldView : Control
{
    private static readonly Color Ground = Color.FromHtml("28362f");
    private static readonly Color GroundLight = Color.FromHtml("34473b");
    private static readonly Color Water = Color.FromHtml("174854");
    private static readonly Color WaterEdge = Color.FromHtml("5d8b91");
    private static readonly Color Road = Color.FromHtml("3b4140");
    private static readonly Color RoadEdge = Color.FromHtml("68716e");
    private static readonly Color Parcel = Color.FromHtml("435047");
    private static readonly Color Conductor = Color.FromHtml("171d1f");
    private static readonly Color Energized = Color.FromHtml("72beb5");
    private static readonly Color Planned = Color.FromHtml("d6a746");
    private static readonly Color Emergency = Color.FromHtml("e0903f");
    private static readonly Color Outage = Color.FromHtml("aeb4b1");
    private static readonly Color Danger = Color.FromHtml("db5c56");
    private static readonly Color Selection = Color.FromHtml("79d5ca");
    private static readonly Color Text = Color.FromHtml("eff4f1");
    private static readonly Color Muted = Color.FromHtml("b1bbb5");
    private static readonly CoreMapPoint[][] Roads =
    [
        [new(100, 520), new(900, 520), new(1500, 650), new(2350, 700), new(3180, 700)],
        [new(100, 1540), new(900, 1500), new(1700, 1450), new(3180, 1450)],
        [new(2050, 80), new(2100, 700), new(2200, 1450), new(2250, 1980)],
        [new(2800, 80), new(2750, 700), new(2750, 1450), new(2800, 1980)],
    ];

    private readonly Dictionary<string, Texture2D> _textures =
        new(StringComparer.Ordinal);
    private RealtimeWorldPresentation? _presentation;
    private CommercialMapTransform? _transform;
    private bool _panning;
    private Vector2 _lastPointer;
    private double _weatherPhase;

    public event Action<CoreMapPoint>? WorldPointRequested;
    public event Action<string?>? AssetSelected;
    public event Action<CoreMapPoint?>? PointerMoved;
    public event Action? CancelRequested;
    public event Action? AnalysisToggleRequested;
    public event Action? CameraChanged;

    public string ZoomLabel => _transform?.ZoomLabel ?? "지역 보기";

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        AccessibilityName = "청류시 실시간 물리 전력망";
        AccessibilityDescription =
            "실제 전신주, 변전소, 도체와 도시 시설을 보여 줍니다. 마우스 가운데 끌기로 이동하고 휠로 세 단계 확대합니다.";
        Resized += ConfigureTransform;
        SetProcess(true);
    }

    public void SetPresentation(RealtimeWorldPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        _presentation = presentation;
        ConfigureTransform();
        AccessibilityName = BuildAccessibilityName(presentation);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_presentation is null || _presentation.ReduceMotion ||
            _presentation.Weather == RealtimeWorldWeather.Clear)
        {
            return;
        }
        _weatherPhase = (_weatherPhase + delta) % 12d;
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
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.WheelUp &&
                mouse.Pressed:
                _transform.SetZoomAt(_transform.ZoomIndex + 1, mouse.Position);
                CameraChanged?.Invoke();
                QueueRedraw();
                AcceptEvent();
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.WheelDown &&
                mouse.Pressed:
                _transform.SetZoomAt(_transform.ZoomIndex - 1, mouse.Position);
                CameraChanged?.Invoke();
                QueueRedraw();
                AcceptEvent();
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.Middle:
                _panning = mouse.Pressed;
                _lastPointer = mouse.Position;
                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _panning:
                _transform.PanByCanvasDelta(motion.Position - _lastPointer);
                _lastPointer = motion.Position;
                CameraChanged?.Invoke();
                QueueRedraw();
                AcceptEvent();
                break;
            case InputEventMouseMotion motion:
                PointerMoved?.Invoke(ToWorld(motion.Position));
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.Left &&
                mouse.Pressed:
                HandlePrimary(mouse.Position);
                AcceptEvent();
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.Right &&
                mouse.Pressed:
                CancelRequested?.Invoke();
                AcceptEvent();
                break;
            case InputEventKey key when key.Pressed && !key.Echo && key.Keycode == Key.A:
                AnalysisToggleRequested?.Invoke();
                AcceptEvent();
                break;
            case InputEventKey key when key.Pressed && !key.Echo && key.Keycode == Key.Home:
                _transform.Home();
                CameraChanged?.Invoke();
                QueueRedraw();
                AcceptEvent();
                break;
        }
    }

    public override void _Draw()
    {
        if (_presentation is null || _transform is null)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), Ground);
            return;
        }

        DrawRect(new Rect2(Vector2.Zero, Size), Ground);
        DrawGroundTexture();
        DrawTerrain(_presentation.World);
        DrawRoads();
        DrawFacilities(_presentation.World);
        DrawConductors(_presentation);
        DrawEquipment(_presentation);
        DrawRiskAndAnalysis(_presentation);
        DrawPointer(_presentation);
        DrawWeather(_presentation);
    }

    private void DrawGroundTexture()
    {
        const int spacing = 52;
        for (int y = -spacing; y < Size.Y + spacing; y += spacing)
        {
            float phase = (y / spacing) % 2 == 0 ? 0 : spacing * 0.5f;
            for (float x = -spacing + phase; x < Size.X + spacing; x += spacing)
            {
                DrawCircle(new Vector2(x, y), 1.2f, GroundLight with { A = 0.34f });
            }
        }
    }

    private void DrawTerrain(SpatialWorldDefinition world)
    {
        foreach (TerrainPolygonDefinition terrain in world.Terrain)
        {
            Vector2[] polygon = terrain.Polygon.Select(Point).ToArray();
            if (terrain.Kind == TerrainKind.Water)
            {
                DrawColoredPolygon(polygon, Water);
                DrawPolyline([.. polygon, polygon[0]], WaterEdge, 2.2f, true);
                continue;
            }
            DrawColoredPolygon(polygon, Parcel with { A = 0.82f });
            DrawPolyline([.. polygon, polygon[0]], GroundLight, 1.2f, true);
        }
    }

    private void DrawRoads()
    {
        foreach (CoreMapPoint[] road in Roads)
        {
            Vector2[] points = road.Select(Point).ToArray();
            DrawPolyline(points, RoadEdge, 13f, true);
            DrawPolyline(points, Road, 9f, true);
            DrawDashedPolyline(points, Color.FromHtml("9ca39b") with { A = 0.55f });
        }
    }

    private void DrawDashedPolyline(Vector2[] points, Color color)
    {
        for (int index = 0; index + 1 < points.Length; index++)
        {
            Vector2 start = points[index];
            Vector2 end = points[index + 1];
            float length = start.DistanceTo(end);
            int count = Math.Max(1, (int)(length / 18f));
            for (int dash = 0; dash < count; dash += 2)
            {
                float from = dash / (float)count;
                float to = Math.Min(1f, (dash + 1) / (float)count);
                DrawLine(start.Lerp(end, from), start.Lerp(end, to), color, 1f, true);
            }
        }
    }

    private void DrawFacilities(SpatialWorldDefinition world)
    {
        foreach (SpatialNodeDefinition node in world.Nodes
                     .Where(item => item.ClassId == "LOAD_TERMINAL"))
        {
            RealtimeVisualKind kind = RealtimeVisualCatalog.FacilityFor(node.DisplayName);
            Texture2D texture = Texture(RealtimeVisualCatalog.FacilityResource(kind));
            Vector2 center = FacilityCenter(node, kind);
            Vector2 size = FacilitySize(kind);
            DrawTextureRect(texture, new Rect2(center - size * 0.5f, size), false,
                node.Commissioned ? Colors.White : Colors.White with { A = 0.45f });
        }
    }

    private Vector2 FacilityCenter(SpatialNodeDefinition node, RealtimeVisualKind kind)
    {
        Vector2 terminal = Point(node.Position);
        return kind switch
        {
            RealtimeVisualKind.Hospital => terminal + new Vector2(82, -42),
            RealtimeVisualKind.Waterworks => terminal + new Vector2(46, -62),
            RealtimeVisualKind.Factory => terminal + new Vector2(84, 46),
            _ => terminal + new Vector2(72, -40),
        };
    }

    private Vector2 FacilitySize(RealtimeVisualKind kind)
    {
        float scale = _transform!.ZoomIndex switch { 0 => 0.72f, 1 => 0.92f, _ => 1.12f };
        Vector2 basis = kind switch
        {
            RealtimeVisualKind.Hospital => new Vector2(150, 150),
            RealtimeVisualKind.Waterworks => new Vector2(150, 150),
            RealtimeVisualKind.Factory => new Vector2(162, 162),
            _ => new Vector2(138, 138),
        };
        return basis * scale;
    }

    private void DrawConductors(RealtimeWorldPresentation presentation)
    {
        SpatialWorldDefinition world = presentation.World;
        foreach (SpatialEdgeDefinition edge in world.Edges)
        {
            SpatialNodeDefinition start = world.Nodes.Single(node => node.NodeId == edge.FromNodeId);
            SpatialNodeDefinition end = world.Nodes.Single(node => node.NodeId == edge.ToNodeId);
            RealtimeWorldAssetStatus? status = Status(presentation, edge.EdgeId);
            RealtimeWorldAssetState state = !edge.Commissioned
                ? RealtimeWorldAssetState.Building
                : status?.State ?? RealtimeWorldAssetState.Normal;
            DrawConductorSpan(Point(start.Position), Point(end.Position), state,
                presentation.AnalysisVisible, edge.EdgeId == presentation.SelectedAssetId);
        }
    }

    private void DrawConductorSpan(
        Vector2 start,
        Vector2 end,
        RealtimeWorldAssetState state,
        bool analysis,
        bool selected)
    {
        Color baseColor = StateColor(state);
        int phases = _transform!.ZoomIndex == 0 ? 1 : 3;
        Vector2 axis = end - start;
        Vector2 normal = axis.LengthSquared() < 0.01f
            ? Vector2.Zero
            : new Vector2(-axis.Y, axis.X).Normalized();
        for (int phase = 0; phase < phases; phase++)
        {
            float offset = phases == 1 ? 0f : (phase - 1) * 2.4f;
            Vector2[] curve = SampleSag(start + normal * offset, end + normal * offset);
            DrawPolyline(curve, Conductor, selected ? 4.8f : 3.2f, true);
            DrawPolyline(curve, baseColor, selected ? 2.4f : 1.25f, true);
            if (state == RealtimeWorldAssetState.ProtectiveOutage)
            {
                DrawOutageGaps(curve);
            }
        }
        if (analysis || selected)
        {
            DrawPolyline(SampleSag(start, end), Selection with { A = 0.48f }, 7f, true);
        }
    }

    private static Vector2[] SampleSag(Vector2 start, Vector2 end)
    {
        const int segments = 16;
        var result = new Vector2[segments + 1];
        float sag = Math.Clamp(start.DistanceTo(end) * 0.055f, 2f, 15f);
        Vector2 control = (start + end) * 0.5f + Vector2.Down * sag;
        for (int index = 0; index <= segments; index++)
        {
            float t = index / (float)segments;
            result[index] = ((1 - t) * (1 - t) * start) +
                (2 * (1 - t) * t * control) + (t * t * end);
        }
        return result;
    }

    private void DrawOutageGaps(Vector2[] curve)
    {
        int middle = curve.Length / 2;
        DrawCircle(curve[middle], 5.5f, Ground);
        DrawLine(curve[middle] + new Vector2(-5, -5),
            curve[middle] + new Vector2(5, 5), Danger, 2.2f, true);
        DrawLine(curve[middle] + new Vector2(-5, 5),
            curve[middle] + new Vector2(5, -5), Danger, 2.2f, true);
    }

    private void DrawEquipment(RealtimeWorldPresentation presentation)
    {
        foreach (SpatialNodeDefinition node in presentation.World.Nodes
                     .OrderBy(item => item.Position.YUnit)
                     .ThenBy(item => item.NodeId, StringComparer.Ordinal))
        {
            RealtimeVisualSpec spec = RealtimeVisualCatalog.Resolve(node.ClassId);
            RealtimeWorldAssetStatus? status = Status(presentation, node.NodeId);
            RealtimeWorldAssetState state = !node.Commissioned
                ? RealtimeWorldAssetState.Building
                : status?.State ?? RealtimeWorldAssetState.Normal;
            Vector2 anchor = Point(node.Position);
            bool selected = node.NodeId == presentation.SelectedAssetId;
            if (spec.ResourcePath.Length == 0)
            {
                DrawTerminal(anchor, state, selected);
            }
            else if (_transform!.ZoomIndex == 0 && spec.Kind is
                     RealtimeVisualKind.StandardPole or RealtimeVisualKind.ReinforcedPole)
            {
                DrawRegionalPole(anchor, spec.Kind, state, selected);
            }
            else
            {
                DrawSprite(anchor, spec, state, selected);
            }
            if (status is not null && state is
                RealtimeWorldAssetState.Emergency or RealtimeWorldAssetState.OverLimit)
            {
                DrawOverloadBadge(anchor, status);
            }
        }
    }

    private void DrawSprite(
        Vector2 anchor,
        RealtimeVisualSpec spec,
        RealtimeWorldAssetState state,
        bool selected)
    {
        Vector2 size = _transform!.ZoomIndex == 2
            ? spec.ConstructionSize
            : spec.OperatingSize;
        Vector2 topLeft = anchor - new Vector2(size.X * spec.GroundAnchor.X,
            size.Y * spec.GroundAnchor.Y);
        Color modulate = state switch
        {
            RealtimeWorldAssetState.Planned => Planned with { A = 0.55f },
            RealtimeWorldAssetState.Building => Colors.White with { A = 0.58f },
            RealtimeWorldAssetState.ProtectiveOutage => Outage with { A = 0.72f },
            RealtimeWorldAssetState.OverLimit => Danger,
            _ => Colors.White,
        };
        DrawTextureRect(Texture(spec.ResourcePath), new Rect2(topLeft, size), false, modulate);
        if (state == RealtimeWorldAssetState.Building)
        {
            DrawDashedCircle(anchor, spec.SelectionRadius, Planned);
        }
        if (selected)
        {
            DrawSelectionBrackets(anchor, spec.SelectionRadius);
        }
        if (state == RealtimeWorldAssetState.ProtectiveOutage)
        {
            DrawCross(anchor, spec.SelectionRadius * 0.42f, Danger);
        }
    }

    private void DrawRegionalPole(
        Vector2 anchor,
        RealtimeVisualKind kind,
        RealtimeWorldAssetState state,
        bool selected)
    {
        float radius = kind == RealtimeVisualKind.ReinforcedPole ? 6f : 4.5f;
        DrawCircle(anchor + new Vector2(2, 3), radius + 2, Colors.Black with { A = 0.5f });
        DrawCircle(anchor, radius, StateColor(state));
        DrawLine(anchor + new Vector2(-radius * 1.3f, -2),
            anchor + new Vector2(radius * 1.3f, -2), Conductor, 2f, true);
        if (selected)
        {
            DrawCircle(anchor, radius + 6f, Selection, false, 2f, true);
        }
    }

    private void DrawTerminal(Vector2 anchor, RealtimeWorldAssetState state, bool selected)
    {
        DrawRect(new Rect2(anchor + new Vector2(-14, -6), new Vector2(28, 17)),
            Color.FromHtml("525d58"));
        DrawLine(anchor + new Vector2(-10, -7), anchor + new Vector2(-10, -17),
            StateColor(state), 3f, true);
        DrawLine(anchor + new Vector2(0, -7), anchor + new Vector2(0, -19),
            StateColor(state), 3f, true);
        DrawLine(anchor + new Vector2(10, -7), anchor + new Vector2(10, -17),
            StateColor(state), 3f, true);
        if (selected)
        {
            DrawSelectionBrackets(anchor, 24);
        }
    }

    private void DrawRiskAndAnalysis(RealtimeWorldPresentation presentation)
    {
        if (presentation.ActiveRiskAreaIds.Count > 0)
        {
            HashSet<string> active = presentation.ActiveRiskAreaIds.ToHashSet(StringComparer.Ordinal);
            foreach (SpatialRiskAreaDefinition risk in presentation.World.RiskAreas
                         .Where(item => active.Contains(item.RiskAreaId)))
            {
                Vector2[] polygon = risk.Polygon.Select(Point).ToArray();
                DrawColoredPolygon(polygon, Danger with { A = 0.13f });
                DrawPolyline([.. polygon, polygon[0]], Danger, 2.2f, true);
                DrawHatch(polygon, Danger with { A = 0.5f });
            }
        }
        if (!presentation.AnalysisVisible || presentation.Highlight is null)
        {
            return;
        }
        HashSet<string> edgeIds = presentation.Highlight.EdgeIds.ToHashSet(StringComparer.Ordinal);
        foreach (SpatialEdgeDefinition edge in presentation.World.Edges
                     .Where(item => edgeIds.Contains(item.EdgeId)))
        {
            SpatialNodeDefinition start = presentation.World.Nodes.Single(n => n.NodeId == edge.FromNodeId);
            SpatialNodeDefinition end = presentation.World.Nodes.Single(n => n.NodeId == edge.ToNodeId);
            DrawPolyline(SampleSag(Point(start.Position), Point(end.Position)),
                edge.EdgeId == presentation.Highlight.LimitingAssetId ? Danger : Selection,
                edge.EdgeId == presentation.Highlight.LimitingAssetId ? 5.5f : 4f,
                true);
        }
    }

    private void DrawHatch(Vector2[] polygon, Color color)
    {
        Rect2 bounds = Bounds(polygon);
        for (float x = bounds.Position.X - bounds.Size.Y; x < bounds.End.X; x += 18f)
        {
            Vector2 from = new(x, bounds.End.Y);
            Vector2 to = new(x + bounds.Size.Y, bounds.Position.Y);
            DrawLine(from, to, color, 1f, true);
        }
    }

    private void DrawPointer(RealtimeWorldPresentation presentation)
    {
        if (presentation.PointerPoint is not CoreMapPoint pointer)
        {
            return;
        }
        Vector2 point = Point(pointer);
        Color color = presentation.PointerAccepted ? Selection : Danger;
        DrawCircle(point, 11f, color with { A = 0.18f });
        DrawCircle(point, 11f, color, false, 2f, true);
        DrawLine(point + Vector2.Left * 16, point + Vector2.Right * 16, color, 1f, true);
        DrawLine(point + Vector2.Up * 16, point + Vector2.Down * 16, color, 1f, true);
    }

    private void DrawWeather(RealtimeWorldPresentation presentation)
    {
        if (presentation.Weather == RealtimeWorldWeather.Clear)
        {
            return;
        }
        Color veil = presentation.Weather switch
        {
            RealtimeWorldWeather.Heat => Color.FromHtml("9f5f32") with { A = 0.10f },
            RealtimeWorldWeather.Rain => Color.FromHtml("27475d") with { A = 0.16f },
            _ => Color.FromHtml("172738") with { A = 0.25f },
        };
        DrawRect(new Rect2(Vector2.Zero, Size), veil);
        if (presentation.ReduceMotion || presentation.Weather == RealtimeWorldWeather.Heat)
        {
            return;
        }
        int count = presentation.Weather == RealtimeWorldWeather.Storm ? 90 : 55;
        float phase = (float)(_weatherPhase * 95d);
        for (int index = 0; index < count; index++)
        {
            float x = ((index * 137f) + phase) % Math.Max(1f, Size.X + 80f) - 40f;
            float y = ((index * 83f) + (phase * 1.7f)) % Math.Max(1f, Size.Y + 60f) - 30f;
            DrawLine(new Vector2(x, y), new Vector2(x - 7, y + 16),
                Color.FromHtml("a8c4d1") with { A = 0.36f }, 1.2f, true);
        }
    }

    private void DrawOverloadBadge(Vector2 anchor, RealtimeWorldAssetStatus status)
    {
        float ratio = status.EmergencyExposureLimitMinutes <= 0
            ? 1f
            : Math.Clamp(status.EmergencyExposureMinutes /
                (float)status.EmergencyExposureLimitMinutes, 0f, 1f);
        Vector2 center = anchor + new Vector2(15, -22);
        DrawCircle(center, 9f, Color.FromHtml("1c2525"));
        DrawArc(center, 7f, -Mathf.Pi / 2f,
            -Mathf.Pi / 2f + (Mathf.Tau * ratio), 18,
            ratio >= 0.8f ? Danger : Emergency, 3f, true);
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-3.5f, 4f), "!",
            HorizontalAlignment.Left, -1, 12, Text);
    }

    private void DrawSelectionBrackets(Vector2 center, float radius)
    {
        const float length = 8f;
        foreach (Vector2 corner in new[]
                 {
                     new Vector2(-1, -1), new Vector2(1, -1),
                     new Vector2(-1, 1), new Vector2(1, 1),
                 })
        {
            Vector2 point = center + corner * radius;
            DrawLine(point, point - new Vector2(corner.X * length, 0), Selection, 2.4f, true);
            DrawLine(point, point - new Vector2(0, corner.Y * length), Selection, 2.4f, true);
        }
    }

    private void DrawDashedCircle(Vector2 center, float radius, Color color)
    {
        const int pieces = 16;
        for (int index = 0; index < pieces; index += 2)
        {
            DrawArc(center, radius, index * Mathf.Tau / pieces,
                (index + 1) * Mathf.Tau / pieces, 4, color, 2f, true);
        }
    }

    private void DrawCross(Vector2 center, float radius, Color color)
    {
        DrawLine(center + new Vector2(-radius, -radius),
            center + new Vector2(radius, radius), color, 3f, true);
        DrawLine(center + new Vector2(-radius, radius),
            center + new Vector2(radius, -radius), color, 3f, true);
    }

    private void HandlePrimary(Vector2 canvasPoint)
    {
        if (_presentation!.PlacementMode)
        {
            WorldPointRequested?.Invoke(ToWorld(canvasPoint));
            return;
        }
        AssetSelected?.Invoke(NearestAsset(canvasPoint));
    }

    private string? NearestAsset(Vector2 canvasPoint)
    {
        return _presentation!.World.Nodes
            .Select(node => (node.NodeId, Distance: Point(node.Position).DistanceTo(canvasPoint)))
            .Where(item => item.Distance <= 34f)
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.NodeId, StringComparer.Ordinal)
            .Select(item => item.NodeId)
            .FirstOrDefault();
    }

    private CoreMapPoint ToWorld(Vector2 canvasPoint)
    {
        CommercialWorldPosition world = _transform!.CanvasToWorld(canvasPoint);
        return new CoreMapPoint(
            (int)Math.Round(world.X, MidpointRounding.AwayFromZero),
            (int)Math.Round(world.Y, MidpointRounding.AwayFromZero));
    }

    private Vector2 Point(CoreMapPoint point) =>
        _transform!.WorldToCanvas(point.XUnit, point.YUnit);

    private Texture2D Texture(string path)
    {
        if (!_textures.TryGetValue(path, out Texture2D? texture))
        {
            texture = GD.Load<Texture2D>(path) ??
                throw new InvalidOperationException($"Unable to load realtime art asset {path}.");
            _textures.Add(path, texture);
        }
        return texture;
    }

    private static RealtimeWorldAssetStatus? Status(
        RealtimeWorldPresentation presentation,
        string assetId) => presentation.AssetStatuses.FirstOrDefault(item =>
            string.Equals(item.AssetId, assetId, StringComparison.Ordinal));

    private static Color StateColor(RealtimeWorldAssetState state) => state switch
    {
        RealtimeWorldAssetState.Planned => Planned,
        RealtimeWorldAssetState.Building => Planned,
        RealtimeWorldAssetState.Emergency => Emergency,
        RealtimeWorldAssetState.ProtectiveOutage => Outage,
        RealtimeWorldAssetState.OverLimit => Danger,
        _ => Energized,
    };

    private void ConfigureTransform()
    {
        if (_presentation is null)
        {
            return;
        }
        MapBounds bounds = _presentation.World.Bounds;
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
        QueueRedraw();
    }

    private static Rect2 Bounds(Vector2[] points)
    {
        float minX = points.Min(point => point.X);
        float minY = points.Min(point => point.Y);
        float maxX = points.Max(point => point.X);
        float maxY = points.Max(point => point.Y);
        return new Rect2(minX, minY, maxX - minX, maxY - minY);
    }

    private static string BuildAccessibilityName(RealtimeWorldPresentation presentation)
    {
        int emergency = presentation.AssetStatuses.Count(item =>
            item.State == RealtimeWorldAssetState.Emergency);
        int outage = presentation.AssetStatuses.Count(item =>
            item.State == RealtimeWorldAssetState.ProtectiveOutage);
        string analysis = presentation.AnalysisVisible ? "분석 켜짐" : "물리 세계 보기";
        return $"청류시 실시간 전력망 · {analysis} · 비상 {emergency}곳 · 보호정지 {outage}곳";
    }
}
