using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gridworks.Core.Release;
using Godot;

namespace Gridworks.Game;

internal sealed record ReleaseMapPresentation(
    ReleaseConstructionSnapshot Snapshot,
    ReleasePoint? PointerPoint,
    bool PointerAccepted,
    string? SelectedNodeId,
    string? SelectedEdgeId,
    string ToolDescription);

internal sealed partial class ReleaseMapView : Control
{
    private static readonly Color Background = Color.FromHtml("071319");
    private static readonly Color Land = Color.FromHtml("0d2022");
    private static readonly Color LandLight = Color.FromHtml("14292a");
    private static readonly Color MinorGrid = Color.FromHtml("1b3436");
    private static readonly Color MajorGrid = Color.FromHtml("355255");
    private static readonly Color Text = Color.FromHtml("e6eff0");
    private static readonly Color Muted = Color.FromHtml("82969a");
    private static readonly Color Idle = Color.FromHtml("9aadb0");
    private static readonly Color Energized = Color.FromHtml("65d3ce");
    private static readonly Color EnergizedGlow = Color.FromHtml("b8f4e8");
    private static readonly Color Planned = Color.FromHtml("efb75d");
    private static readonly Color Invalid = Color.FromHtml("ed756e");
    private static readonly Color Focus = Color.FromHtml("f4d27c");
    private static readonly Color Risk = Color.FromHtml("d77778");
    private static readonly Color River = Color.FromHtml("123743");
    private static readonly Color RiverLine = Color.FromHtml("27535d");
    private static readonly Color Road = Color.FromHtml("333d3a");
    private static readonly Color RoadLine = Color.FromHtml("59605a");
    private static readonly Color Building = Color.FromHtml("5e7372");
    private static readonly Color Facility = Color.FromHtml("91aaa7");
    private const float FlowSpeed = 0.11f;

    private ReleaseMapPresentation? _presentation;
    private ReleasePoint _keyboardPoint = new(0, 0);
    private string? _hoverNodeId;
    private string? _hoverEdgeId;
    private float _flowPhase;
    private double _redrawAccumulator;
    private readonly List<Rect2> _mapLabelBounds = [];

    public event Action<ReleasePoint?>? PointerChanged;
    public event Action<ReleasePoint>? PointRequested;
    public event Action<string?, string?>? AssetUnderPointerChanged;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        AccessibilityDescription =
            "청류시 전력망 지도. 마우스로 격자점을 가리키고 클릭합니다. 방향키는 한 칸, Shift와 방향키는 네 칸 이동하며 Enter 또는 Space로 선택합니다.";
        MouseExited += () =>
        {
            PointerChanged?.Invoke(null);
            UpdateHoveredAsset(null, null);
            QueueRedraw();
        };
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
    }

    public override void _Process(double delta)
    {
        if (_presentation is null || !IsVisibleInTree() ||
            !_presentation.Snapshot.Evaluation.Edges.Any(edge => edge.UsedKw > 0 && edge.Available))
        {
            return;
        }
        _flowPhase = (_flowPhase + ((float)delta * FlowSpeed)) % 1f;
        _redrawAccumulator += delta;
        if (_redrawAccumulator >= (1d / 24d))
        {
            _redrawAccumulator = 0d;
            QueueRedraw();
        }
    }

    public void SetPresentation(ReleaseMapPresentation presentation)
    {
        _presentation = presentation;
        _keyboardPoint = Clamp(_keyboardPoint, presentation.Snapshot.World.Grid);
        AccessibilityName = BuildAccessibilitySummary(presentation);
        QueueRedraw();
    }

    public Vector2 ViewportPointForGridPoint(ReleasePoint point)
    {
        ReleaseGridDefinition grid = _presentation?.Snapshot.World.Grid
            ?? throw new InvalidOperationException("지도가 아직 준비되지 않았습니다.");
        return GetGlobalTransformWithCanvas() * ToCanvas(point, grid, PlotRect(grid));
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (_presentation is null)
        {
            return;
        }
        ReleaseGridDefinition grid = _presentation.Snapshot.World.Grid;
        switch (inputEvent)
        {
            case InputEventMouseMotion motion:
                PointerChanged?.Invoke(TrySnap(motion.Position, grid, out ReleasePoint point)
                    ? point
                    : null);
                FindAsset(motion.Position, out string? nodeId, out string? edgeId);
                UpdateHoveredAsset(nodeId, edgeId);
                AcceptEvent();
                break;
            case InputEventMouseButton button when
                button.ButtonIndex == MouseButton.Left && button.Pressed &&
                TrySnap(button.Position, grid, out ReleasePoint clickedPoint):
                GrabFocus();
                _keyboardPoint = clickedPoint;
                PointerChanged?.Invoke(clickedPoint);
                FindAsset(button.Position, out string? clickedNode, out string? clickedEdge);
                UpdateHoveredAsset(clickedNode, clickedEdge);
                PointRequested?.Invoke(clickedPoint);
                AcceptEvent();
                break;
            case InputEventKey key when key.Pressed && !key.Echo:
                HandleKey(key, grid);
                break;
        }
    }

    public override void _Draw()
    {
        if (_presentation is null)
        {
            return;
        }
        ReleaseConstructionSnapshot snapshot = _presentation.Snapshot;
        ReleaseGridDefinition grid = snapshot.World.Grid;
        Rect2 plot = PlotRect(grid);
        DrawRect(new Rect2(Vector2.Zero, Size), Background);
        DrawTerrain(snapshot.World, grid, plot);
        DrawGrid(grid, plot);
        DrawFacilityFootprints(snapshot.World, grid, plot);
        DrawRiskAreas(snapshot.World, grid, plot);
        DrawWaterSupports(snapshot.World, grid, plot);
        DrawEdges(snapshot, grid, plot);
        DrawNonJunctionCrossings(snapshot, grid, plot);
        DrawLineDraft(snapshot, grid, plot);
        _mapLabelBounds.Clear();
        DrawLoads(snapshot, grid, plot);
        DrawNodes(snapshot, grid, plot);
        DrawNodeDraft(snapshot, grid, plot);
        DrawPointer(grid, plot);
        DrawLegend();
        if (HasFocus())
        {
            DrawRect(new Rect2(Vector2.One * 2f, Size - Vector2.One * 4f), Focus, false, 2f);
        }
    }

    private void HandleKey(InputEventKey key, ReleaseGridDefinition grid)
    {
        int step = key.ShiftPressed ? grid.MajorStep : 1;
        ReleasePoint next = key.Keycode switch
        {
            Key.Left => _keyboardPoint with { X = _keyboardPoint.X - step },
            Key.Right => _keyboardPoint with { X = _keyboardPoint.X + step },
            Key.Up => _keyboardPoint with { Y = _keyboardPoint.Y - step },
            Key.Down => _keyboardPoint with { Y = _keyboardPoint.Y + step },
            _ => _keyboardPoint,
        };
        if (key.Keycode is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            _keyboardPoint = Clamp(next, grid);
            PointerChanged?.Invoke(_keyboardPoint);
            UpdateHoveredAssetAt(_keyboardPoint, grid);
            QueueRedraw();
            AcceptEvent();
            return;
        }
        if (key.Keycode is Key.Enter or Key.KpEnter or Key.Space)
        {
            PointerChanged?.Invoke(_keyboardPoint);
            UpdateHoveredAssetAt(_keyboardPoint, grid);
            PointRequested?.Invoke(_keyboardPoint);
            AcceptEvent();
        }
    }

    private void UpdateHoveredAssetAt(ReleasePoint point, ReleaseGridDefinition grid)
    {
        FindAsset(ToCanvas(point, grid, PlotRect(grid)), out string? nodeId, out string? edgeId);
        UpdateHoveredAsset(nodeId, edgeId);
    }

    private void DrawTerrain(ReleaseWorldDefinition world, ReleaseGridDefinition grid, Rect2 plot)
    {
        DrawRect(plot, Land);
        DrawDistrict(grid, plot, new ReleasePoint(11, 1), new ReleasePoint(20, 7), "북안 생활권");
        DrawDistrict(grid, plot, new ReleasePoint(18, 7), new ReleasePoint(28, 14), "동부 생활권");
        DrawDistrict(grid, plot, new ReleasePoint(12, 13), new ReleasePoint(25, 20), "강변 산업권");

        DrawTerrainContour(grid, plot,
            new ReleasePoint(0, 3), new ReleasePoint(5, 2), new ReleasePoint(10, 4));
        DrawTerrainContour(grid, plot,
            new ReleasePoint(22, 2), new ReleasePoint(27, 4), new ReleasePoint(32, 3));
        DrawTerrainContour(grid, plot,
            new ReleasePoint(22, 18), new ReleasePoint(27, 17), new ReleasePoint(32, 19));

        if (world.WaterPolygon.Count >= 3)
        {
            Vector2[] river = world.WaterPolygon
                .Select(point => ToCanvas(point, grid, plot))
                .ToArray();
            DrawColoredPolygon(river, River);
            DrawPolyline(river.Append(river[0]).ToArray(), RiverLine, 1.5f, true);
            DrawWaterPattern(world.WaterPolygon, grid, plot);
        }

        DrawRoad(grid, plot,
            new ReleasePoint(grid.MinX, 17), new ReleasePoint(8, 14),
            new ReleasePoint(20, 8), new ReleasePoint(grid.MaxX, 5));
        DrawRoad(grid, plot,
            new ReleasePoint(2, 5), new ReleasePoint(9, 5),
            new ReleasePoint(18, 6), new ReleasePoint(27, 10));
        DrawRoad(grid, plot,
            new ReleasePoint(5, 19), new ReleasePoint(16, 16),
            new ReleasePoint(25, 15), new ReleasePoint(32, 16));

        foreach (ReleaseSourceDefinition source in world.Sources)
        {
            ReleaseNodeDefinition node = world.Nodes.Single(item => item.NodeId == source.NodeId);
            DrawPowerYard(ToCanvas(node.Position, grid, plot));
        }
        DrawRect(plot, MajorGrid, false, 1.25f);
    }

    private void DrawWaterPattern(
        IReadOnlyList<ReleasePoint> waterPolygon,
        ReleaseGridDefinition grid,
        Rect2 plot)
    {
        int minX = Math.Max(grid.MinX, waterPolygon.Min(point => point.X));
        int maxX = Math.Min(grid.MaxX, waterPolygon.Max(point => point.X));
        int minY = Math.Max(grid.MinY, waterPolygon.Min(point => point.Y));
        int maxY = Math.Min(grid.MaxY, waterPolygon.Max(point => point.Y));
        Vector2[] polygon = waterPolygon
            .Select(point => new Vector2(point.X, point.Y))
            .ToArray();
        for (int x = minX; x < maxX; x++)
        {
            for (int y = minY; y < maxY; y++)
            {
                Vector2 center = new(x + 0.5f, y + 0.5f);
                if ((x + (y * 2)) % 3 != 0 || !PointInsidePolygon(center, polygon))
                {
                    continue;
                }
                Vector2 from = ToCanvas(center + new Vector2(-0.3f, 0.12f), grid, plot);
                Vector2 to = ToCanvas(center + new Vector2(0.3f, -0.12f), grid, plot);
                DrawLine(from, to, new Color(RiverLine, 0.72f), 1.2f, true);
            }
        }
    }

    private void DrawDistrict(
        ReleaseGridDefinition grid,
        Rect2 plot,
        ReleasePoint from,
        ReleasePoint to,
        string label)
    {
        Vector2 topLeft = ToCanvas(from, grid, plot);
        Vector2 bottomRight = ToCanvas(to, grid, plot);
        Rect2 district = new(topLeft, bottomRight - topLeft);
        DrawRect(district, new Color(LandLight, 0.58f));
        DrawRect(district, new Color(Facility, 0.18f), false, 1.1f);
        DrawMapText(topLeft + new Vector2(7f, 17f), label, 11, new Color(Muted, 0.68f));
    }

    private void DrawTerrainContour(
        ReleaseGridDefinition grid,
        Rect2 plot,
        params ReleasePoint[] points) =>
        DrawPolyline(points.Select(point => ToCanvas(point, grid, plot)).ToArray(),
            new Color(MajorGrid, 0.36f), 1f, true);

    private void DrawRoad(ReleaseGridDefinition grid, Rect2 plot, params ReleasePoint[] points)
    {
        Vector2[] canvasPoints = points.Select(point => ToCanvas(point, grid, plot)).ToArray();
        DrawPolyline(canvasPoints, Road, 7f, true);
        DrawPolyline(canvasPoints, new Color(RoadLine, 0.72f), 1.2f, true);
    }

    private void DrawPowerYard(Vector2 center)
    {
        Color fill = new(Building, 0.28f);
        DrawRect(new Rect2(center + new Vector2(-24f, -18f), new Vector2(48f, 36f)), fill);
        DrawRect(new Rect2(center + new Vector2(-24f, -18f), new Vector2(48f, 36f)),
            new Color(Facility, 0.32f), false, 1f);
        for (int x = -16; x <= 16; x += 16)
        {
            DrawLine(center + new Vector2(x, -10f), center + new Vector2(x, 10f),
                new Color(Facility, 0.42f), 1.5f);
            DrawLine(center + new Vector2(x - 5f, -5f), center + new Vector2(x + 5f, -5f),
                new Color(Facility, 0.42f), 1.5f);
        }
    }

    private void DrawGrid(ReleaseGridDefinition grid, Rect2 plot)
    {
        for (int x = grid.MinX; x <= grid.MaxX; x++)
        {
            bool major = (x - grid.MinX) % grid.MajorStep == 0;
            Vector2 top = ToCanvas(new ReleasePoint(x, grid.MinY), grid, plot);
            Vector2 bottom = ToCanvas(new ReleasePoint(x, grid.MaxY), grid, plot);
            DrawLine(top, bottom, major ? MajorGrid : MinorGrid, major ? 1.15f : 0.55f);
            if (major)
            {
                DrawMapText(bottom + new Vector2(-7f, 18f),
                    x.ToString(CultureInfo.InvariantCulture), 11, Muted);
            }
        }
        for (int y = grid.MinY; y <= grid.MaxY; y++)
        {
            bool major = (y - grid.MinY) % grid.MajorStep == 0;
            Vector2 left = ToCanvas(new ReleasePoint(grid.MinX, y), grid, plot);
            Vector2 right = ToCanvas(new ReleasePoint(grid.MaxX, y), grid, plot);
            DrawLine(left, right, major ? MajorGrid : MinorGrid, major ? 1.15f : 0.55f);
            if (major)
            {
                DrawString(GetThemeDefaultFont(), left + new Vector2(-25f, 4f),
                    y.ToString(CultureInfo.InvariantCulture), HorizontalAlignment.Right, 20f, 11, Muted);
            }
        }
    }

    private void DrawFacilityFootprints(
        ReleaseWorldDefinition world,
        ReleaseGridDefinition grid,
        Rect2 plot)
    {
        foreach (ReleaseLoadDefinition load in world.Loads)
        {
            Vector2 center = ToCanvas(load.Position, grid, plot);
            switch (load.LoadId)
            {
                case "HOSPITAL_LIFE_SAFETY":
                    DrawHospital(center);
                    break;
                case "WATER_ESSENTIAL":
                    DrawWaterworks(center);
                    break;
                case "RIVER_FACTORY":
                    DrawFactory(center);
                    break;
                case "NORTH_RESIDENTIAL":
                case "EAST_RESIDENTIAL":
                    DrawHomes(center);
                    break;
                default:
                    DrawRect(new Rect2(center - new Vector2(13f, 9f), new Vector2(26f, 18f)),
                        new Color(Building, 0.3f));
                    break;
            }
        }
    }

    private void DrawHospital(Vector2 center)
    {
        Color fill = new(Building, 0.48f);
        DrawRect(new Rect2(center + new Vector2(-25f, -20f), new Vector2(50f, 40f)), fill);
        DrawRect(new Rect2(center + new Vector2(-25f, -20f), new Vector2(50f, 40f)),
            new Color(Facility, 0.56f), false, 1.3f);
        DrawRect(new Rect2(center + new Vector2(-3f, -12f), new Vector2(6f, 24f)),
            new Color(Facility, 0.68f));
        DrawRect(new Rect2(center + new Vector2(-12f, -3f), new Vector2(24f, 6f)),
            new Color(Facility, 0.68f));
    }

    private void DrawWaterworks(Vector2 center)
    {
        DrawRect(new Rect2(center + new Vector2(-26f, -16f), new Vector2(52f, 32f)),
            new Color(Building, 0.42f));
        foreach (float offset in new[] { -12f, 12f })
        {
            DrawCircle(center + new Vector2(offset, 0f), 9f, new Color(Facility, 0.34f));
            DrawCircle(center + new Vector2(offset, 0f), 9f, new Color(Facility, 0.58f), false, 1.3f);
        }
        DrawLine(center + new Vector2(-3f, 0f), center + new Vector2(3f, 0f),
            new Color(Facility, 0.62f), 2f);
    }

    private void DrawFactory(Vector2 center)
    {
        Vector2[] shed =
        [
            center + new Vector2(-28f, 18f), center + new Vector2(-28f, -4f),
            center + new Vector2(-16f, -14f), center + new Vector2(-4f, -4f),
            center + new Vector2(8f, -14f), center + new Vector2(20f, -4f),
            center + new Vector2(28f, -4f), center + new Vector2(28f, 18f),
        ];
        DrawColoredPolygon(shed, new Color(Building, 0.5f));
        DrawPolyline(shed.Append(shed[0]).ToArray(), new Color(Facility, 0.56f), 1.2f, true);
        DrawRect(new Rect2(center + new Vector2(18f, -24f), new Vector2(7f, 21f)),
            new Color(Building, 0.62f));
        DrawLine(center + new Vector2(18f, -24f), center + new Vector2(25f, -24f),
            new Color(Facility, 0.56f), 1.2f);
    }

    private void DrawHomes(Vector2 center)
    {
        for (int index = 0; index < 5; index++)
        {
            int column = index % 3;
            int row = index / 3;
            Vector2 house = center + new Vector2((column - 1) * 17f, (row * 18f) - 10f);
            DrawRect(new Rect2(house + new Vector2(-7f, -1f), new Vector2(14f, 10f)),
                new Color(Building, 0.46f));
            Vector2[] roof =
            [
                house + new Vector2(-9f, -1f), house + new Vector2(0f, -9f),
                house + new Vector2(9f, -1f),
            ];
            DrawColoredPolygon(roof, new Color(Facility, 0.42f));
        }
    }

    private void DrawRiskAreas(ReleaseWorldDefinition world, ReleaseGridDefinition grid, Rect2 plot)
    {
        foreach (ReleaseRiskAreaDefinition area in world.RiskAreas)
        {
            Vector2[] polygon = area.Polygon.Select(point => ToCanvas(point, grid, plot)).ToArray();
            DrawColoredPolygon(polygon, new Color(Risk, 0.11f));
            for (int index = 0; index < polygon.Length; index++)
            {
                DrawDashedLine(polygon[index], polygon[(index + 1) % polygon.Length], Risk, 2f, 7f);
            }
            DrawRiskHatch(area, grid, plot);
            DrawMapText(polygon[0] + new Vector2(4f, -7f), area.DisplayName, 12, new Color(Risk, 0.96f));
        }
    }

    private void DrawRiskHatch(ReleaseRiskAreaDefinition area, ReleaseGridDefinition grid, Rect2 plot)
    {
        int minX = area.Polygon.Min(point => point.X);
        int maxX = area.Polygon.Max(point => point.X);
        int minY = area.Polygon.Min(point => point.Y);
        int maxY = area.Polygon.Max(point => point.Y);
        Vector2[] polygon = area.Polygon.Select(point => new Vector2(point.X, point.Y)).ToArray();
        for (int x = minX; x < maxX; x++)
        {
            for (int y = minY; y < maxY; y++)
            {
                Vector2 cell = new(x + 0.5f, y + 0.5f);
                if ((x + y) % 2 == 0 && PointInsidePolygon(cell, polygon))
                {
                    Vector2 center = ToCanvas(cell, grid, plot);
                    DrawLine(center + new Vector2(-4f, 4f), center + new Vector2(4f, -4f),
                        new Color(Risk, 0.48f), 1.2f, true);
                }
            }
        }
    }

    private void DrawWaterSupports(
        ReleaseWorldDefinition world,
        ReleaseGridDefinition grid,
        Rect2 plot)
    {
        if (world.WaterPolygon.Count < 3)
        {
            return;
        }

        Vector2[] water = world.WaterPolygon
            .Select(point => new Vector2(point.X, point.Y))
            .ToArray();
        Dictionary<string, ReleaseNodeClassDefinition> classes =
            world.NodeClasses.ToDictionary(item => item.ClassId);
        foreach (ReleaseNodeDefinition node in world.Nodes)
        {
            Vector2 position = new(node.Position.X, node.Position.Y);
            if (!PointInsidePolygon(position, water))
            {
                continue;
            }

            ReleaseNodeKind kind = classes[node.ClassId].Kind;
            Vector2 center = ToCanvas(node.Position, grid, plot);
            Vector2 halfSize = kind is ReleaseNodeKind.Substation or ReleaseNodeKind.SourceTerminal
                ? new Vector2(15f, 11f)
                : new Vector2(9f, 8f);
            Rect2 platform = new(center - halfSize, halfSize * 2f);
            DrawRect(platform, new Color(Road, 0.96f));
            DrawRect(platform, new Color(Facility, 0.72f), false, 1.4f);
            DrawLine(
                center + new Vector2(-halfSize.X + 3f, -halfSize.Y),
                center + new Vector2(-halfSize.X + 3f, halfSize.Y),
                new Color(Facility, 0.55f),
                1.2f,
                true);
            DrawLine(
                center + new Vector2(halfSize.X - 3f, -halfSize.Y),
                center + new Vector2(halfSize.X - 3f, halfSize.Y),
                new Color(Facility, 0.55f),
                1.2f,
                true);
        }
    }

    private void DrawEdges(ReleaseConstructionSnapshot snapshot, ReleaseGridDefinition grid, Rect2 plot)
    {
        Dictionary<string, ReleaseNodeDefinition> nodes = snapshot.World.Nodes.ToDictionary(item => item.NodeId);
        Dictionary<string, ReleaseEdgeUsage> usages = snapshot.Evaluation.Edges.ToDictionary(item => item.EdgeId);
        HashSet<string> buildingEdges = snapshot.ActiveConstruction?.EdgeIds.ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        foreach (ReleaseEdgeDefinition edge in snapshot.World.Edges)
        {
            Vector2 from = ToCanvas(nodes[edge.FromNodeId].Position, grid, plot);
            Vector2 to = ToCanvas(nodes[edge.ToNodeId].Position, grid, plot);
            ReleaseEdgeUsage usage = usages[edge.EdgeId];
            float width = EdgeWidth(snapshot.World, edge);
            bool selected = string.Equals(_presentation!.SelectedEdgeId, edge.EdgeId, StringComparison.Ordinal);
            if (selected)
            {
                DrawLine(from, to, new Color(Focus, 0.9f), width + 5f, true);
                DrawLine(from, to, Background, width + 2f, true);
            }

            if (!edge.Commissioned)
            {
                DrawDashedLine(from, to, Planned, width, buildingEdges.Contains(edge.EdgeId) ? 6f : 10f,
                    true, true);
                if (buildingEdges.Contains(edge.EdgeId))
                {
                    DrawConstructionTicks(from, to, Planned);
                }
            }
            else if (!usage.Available)
            {
                DrawDashedLine(from, to, Invalid, width, 10f, true, true);
                DrawUnavailableMark(from.Lerp(to, 0.5f), Invalid);
            }
            else if (usage.UsedKw > 0)
            {
                DrawLine(from, to, new Color(Background, 0.9f), width + 2.5f, true);
                DrawLine(from, to, Energized, width, true);
                DrawPowerFlow(from, to, width);
            }
            else
            {
                DrawLine(from, to, new Color(Background, 0.84f), width + 2f, true);
                DrawLine(from, to, Idle, Math.Max(1.8f, width - 1f), true);
            }
        }
    }

    private void DrawPowerFlow(Vector2 from, Vector2 to, float width)
    {
        float length = from.DistanceTo(to);
        int markerCount = Math.Max(1, (int)MathF.Floor(length / 48f));
        for (int index = 0; index <= markerCount; index++)
        {
            float fraction = (_flowPhase + (index / (float)(markerCount + 1))) % 1f;
            DrawCircle(from.Lerp(to, fraction), Math.Max(1.7f, width * 0.42f), EnergizedGlow);
        }
    }

    private void DrawNonJunctionCrossings(
        ReleaseConstructionSnapshot snapshot,
        ReleaseGridDefinition grid,
        Rect2 plot)
    {
        IReadOnlyList<ReleaseEdgeDefinition> edges = snapshot.World.Edges;
        Dictionary<string, ReleaseNodeDefinition> nodes = snapshot.World.Nodes.ToDictionary(item => item.NodeId);
        Dictionary<string, ReleaseEdgeUsage> usages = snapshot.Evaluation.Edges.ToDictionary(item => item.EdgeId);
        for (int firstIndex = 0; firstIndex < edges.Count; firstIndex++)
        {
            ReleaseEdgeDefinition first = edges[firstIndex];
            for (int secondIndex = firstIndex + 1; secondIndex < edges.Count; secondIndex++)
            {
                ReleaseEdgeDefinition second = edges[secondIndex];
                if (SharesNode(first, second))
                {
                    continue;
                }
                Vector2 secondFrom = ToCanvas(nodes[second.FromNodeId].Position, grid, plot);
                Vector2 secondTo = ToCanvas(nodes[second.ToNodeId].Position, grid, plot);
                if (!TrySegmentIntersection(
                        ToCanvas(nodes[first.FromNodeId].Position, grid, plot),
                        ToCanvas(nodes[first.ToNodeId].Position, grid, plot),
                        secondFrom,
                        secondTo,
                        out Vector2 crossing))
                {
                    continue;
                }
                float width = EdgeWidth(snapshot.World, second);
                Vector2 direction = (secondTo - secondFrom).Normalized();
                DrawCircle(crossing, width + 3.2f, Background);
                DrawLine(crossing - (direction * 7f), crossing + (direction * 7f),
                    EdgeColor(second, usages[second.EdgeId]), width, true);
            }
        }
    }

    private void DrawLineDraft(ReleaseConstructionSnapshot snapshot, ReleaseGridDefinition grid, Rect2 plot)
    {
        ReleaseLineDraftSnapshot? draft = snapshot.LineDraft;
        if (draft is null)
        {
            return;
        }
        ReleaseNodeDefinition start = snapshot.World.Nodes.Single(item => item.NodeId == draft.StartNodeId);
        var points = new List<ReleasePoint> { start.Position };
        points.AddRange(draft.IntermediatePoints);
        if (draft.EndNodeId is not null)
        {
            points.Add(snapshot.World.Nodes.Single(item => item.NodeId == draft.EndNodeId).Position);
        }
        for (int index = 0; index < points.Count - 1; index++)
        {
            DrawDashedLine(ToCanvas(points[index], grid, plot), ToCanvas(points[index + 1], grid, plot),
                Planned, 3f, 7f, true, true);
        }
        if (draft.EndNodeId is null && _presentation!.PointerPoint is ReleasePoint pointer)
        {
            Vector2 from = ToCanvas(points[^1], grid, plot);
            Vector2 to = ToCanvas(pointer, grid, plot);
            Color color = _presentation.PointerAccepted ? Planned : Invalid;
            DrawDashedLine(from, to, color, 2.5f, 7f, true, true);
            if (!_presentation.PointerAccepted)
            {
                DrawUnavailableMark(to, Invalid);
            }
        }
        foreach (ReleasePoint point in draft.IntermediatePoints)
        {
            Vector2 center = ToCanvas(point, grid, plot);
            DrawCircle(center, 6f, Planned);
            DrawCircle(center, 10f, Planned, false, 2f);
        }
    }

    private void DrawLoads(ReleaseConstructionSnapshot snapshot, ReleaseGridDefinition grid, Rect2 plot)
    {
        Dictionary<string, ReleaseLoadSupply> supplies =
            snapshot.Evaluation.Loads.ToDictionary(item => item.LoadId);
        foreach (ReleaseLoadDefinition load in snapshot.World.Loads)
        {
            Vector2 center = ToCanvas(load.Position, grid, plot);
            ReleaseLoadSupply supply = supplies[load.LoadId];
            bool supplied = supply.DeliveredKw == supply.DemandKw && supply.DemandKw > 0;
            Color color = supplied ? Energized : Invalid;
            DrawCircle(center, 7f, new Color(Background, 0.88f));
            DrawCircle(center, 5f, color);
            if (supplied)
            {
                DrawCircle(center, 9f, color, false, 1.5f);
            }
            else
            {
                DrawUnavailableMark(center, color, 5f);
            }
            DrawMapLabel(center, MapLoadName(load), plot);
        }
    }

    private void DrawNodes(ReleaseConstructionSnapshot snapshot, ReleaseGridDefinition grid, Rect2 plot)
    {
        Dictionary<string, ReleaseNodeClassDefinition> classes =
            snapshot.World.NodeClasses.ToDictionary(item => item.ClassId);
        Dictionary<string, ReleaseNodeUsage> usages =
            snapshot.Evaluation.Nodes.ToDictionary(item => item.NodeId);
        HashSet<string> energizedNodes = snapshot.Evaluation.Loads
            .Where(load => load.DeliveredKw > 0)
            .SelectMany(load => load.PathNodeIds)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> buildingNodes = snapshot.ActiveConstruction?.NodeIds.ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);

        foreach (ReleaseNodeDefinition node in snapshot.World.Nodes)
        {
            ReleaseNodeClassDefinition nodeClass = classes[node.ClassId];
            ReleaseNodeUsage usage = usages[node.NodeId];
            Vector2 center = ToCanvas(node.Position, grid, plot);
            bool energized = node.Commissioned && usage.Available && energizedNodes.Contains(node.NodeId);
            Color color = !node.Commissioned ? Planned : !usage.Available ? Invalid : energized ? Energized : Idle;
            float size = nodeClass.Kind switch
            {
                ReleaseNodeKind.SourceTerminal => 10f,
                ReleaseNodeKind.Substation => 9f,
                ReleaseNodeKind.DedicatedLoadTerminal => 7f,
                _ => 5.5f,
            };
            DrawNodeSymbol(center, size, color, nodeClass, energized);
            if (!node.Commissioned && buildingNodes.Contains(node.NodeId))
            {
                DrawConstructionTicks(center - Vector2.One * 9f, center + Vector2.One * 9f, Planned);
            }
            if (!usage.Available && node.Commissioned)
            {
                DrawUnavailableMark(center, Invalid, size + 2f);
            }
            if (string.Equals(_presentation!.SelectedNodeId, node.NodeId, StringComparison.Ordinal))
            {
                DrawCircle(center, size + 8f, Focus, false, 2.5f);
            }
            if (nodeClass.Kind is ReleaseNodeKind.SourceTerminal or ReleaseNodeKind.Substation)
            {
                DrawMapLabel(center, MapNodeName(node.DisplayName), plot);
            }
        }
    }

    private void DrawNodeSymbol(
        Vector2 center,
        float size,
        Color color,
        ReleaseNodeClassDefinition nodeClass,
        bool energized)
    {
        DrawCircle(center, size + 2f, new Color(Background, 0.92f));
        switch (nodeClass.Kind)
        {
            case ReleaseNodeKind.Substation:
                DrawCircle(center + new Vector2(-4f, 0f), size * 0.64f,
                    new Color(color, energized ? 0.66f : 0.3f));
                DrawCircle(center + new Vector2(4f, 0f), size * 0.64f,
                    new Color(color, energized ? 0.66f : 0.3f));
                DrawCircle(center + new Vector2(-4f, 0f), size * 0.64f, color, false, 1.8f);
                DrawCircle(center + new Vector2(4f, 0f), size * 0.64f, color, false, 1.8f);
                DrawLine(center + new Vector2(-size, -size), center + new Vector2(size, -size), color, 2f);
                DrawLine(center + new Vector2(-size, size), center + new Vector2(size, size), color, 2f);
                break;
            case ReleaseNodeKind.SourceTerminal:
                DrawCircle(center, size, new Color(color, energized ? 0.66f : 0.3f));
                DrawCircle(center, size, color, false, 2.2f);
                DrawPolyline(
                    new[]
                    {
                        center + new Vector2(-6f, 1f), center + new Vector2(-2f, -3f),
                        center + new Vector2(2f, 3f), center + new Vector2(6f, -1f),
                    },
                    color,
                    1.7f,
                    true);
                break;
            case ReleaseNodeKind.DedicatedLoadTerminal:
                DrawRect(new Rect2(center - Vector2.One * size, Vector2.One * size * 2f),
                    new Color(color, energized ? 0.6f : 0.25f));
                DrawRect(new Rect2(center - Vector2.One * size, Vector2.One * size * 2f),
                    color, false, 1.8f);
                DrawCircle(center, 2.2f, color);
                break;
            default:
                DrawLine(center + new Vector2(0f, -size - 4f), center + new Vector2(0f, size + 4f),
                    color, 2f);
                DrawLine(center + new Vector2(-size, -2f), center + new Vector2(size, -2f), color, 1.7f);
                DrawCircle(center, size, new Color(color, energized ? 0.78f : 0.38f));
                DrawCircle(center, size, color, false, 1.5f);
                if (nodeClass.MaxConnections >= 4)
                {
                    DrawCircle(center, size + 3f, color, false, 1.3f);
                }
                break;
        }
    }

    private void DrawNodeDraft(ReleaseConstructionSnapshot snapshot, ReleaseGridDefinition grid, Rect2 plot)
    {
        if (snapshot.NodeDraft is not ReleaseNodeDraftSnapshot draft)
        {
            return;
        }
        Vector2 center = ToCanvas(draft.Position, grid, plot);
        Rect2 box = new(center - Vector2.One * 10f, Vector2.One * 20f);
        DrawRect(box, new Color(Planned, 0.18f));
        DrawDashedLine(box.Position, new Vector2(box.End.X, box.Position.Y), Planned, 2f, 5f);
        DrawDashedLine(new Vector2(box.End.X, box.Position.Y), box.End, Planned, 2f, 5f);
        DrawDashedLine(box.End, new Vector2(box.Position.X, box.End.Y), Planned, 2f, 5f);
        DrawDashedLine(new Vector2(box.Position.X, box.End.Y), box.Position, Planned, 2f, 5f);
    }

    private void DrawPointer(ReleaseGridDefinition grid, Rect2 plot)
    {
        if (_presentation!.PointerPoint is null && !HasFocus())
        {
            return;
        }
        ReleasePoint point = _presentation.PointerPoint ?? _keyboardPoint;
        Vector2 center = ToCanvas(point, grid, plot);
        Color color = _presentation.PointerPoint is null || _presentation.PointerAccepted ? Focus : Invalid;
        DrawCircle(center, 11f, color, false, 2f);
        DrawLine(center + new Vector2(-15f, 0f), center + new Vector2(-9f, 0f), color, 1.5f);
        DrawLine(center + new Vector2(9f, 0f), center + new Vector2(15f, 0f), color, 1.5f);
        DrawLine(center + new Vector2(0f, -15f), center + new Vector2(0f, -9f), color, 1.5f);
        DrawLine(center + new Vector2(0f, 9f), center + new Vector2(0f, 15f), color, 1.5f);
        if (!_presentation.PointerAccepted)
        {
            DrawUnavailableMark(center, Invalid, 6f);
        }
    }

    private void DrawLegend()
    {
        float legendWidth = Math.Min(Math.Max(0f, Size.X - 16f), 610f);
        DrawRect(new Rect2(8f, 6f, legendWidth, 29f), new Color(Background, 0.94f));
        DrawRect(new Rect2(8f, 6f, legendWidth, 29f), new Color(MajorGrid, 0.8f), false, 1f);
        DrawLegendLine(18f, "통전", Energized, false, true);
        DrawLegendLine(92f, "완공·현재 미사용", Idle, false, false);
        DrawLegendLine(252f, "계획·공사", Planned, true, false);
        DrawLegendLine(363f, "사용 불가", Invalid, true, false, true);
        DrawRiskLegend(480f);
    }

    private void DrawLegendLine(
        float x,
        string label,
        Color color,
        bool dashed,
        bool flow,
        bool unavailable = false)
    {
        const float y = 20f;
        if (dashed)
        {
            DrawDashedLine(new Vector2(x, y), new Vector2(x + 24f, y), color, 3f, 6f);
        }
        else
        {
            DrawLine(new Vector2(x, y), new Vector2(x + 24f, y), color, flow ? 4f : 2.2f);
        }
        if (flow)
        {
            DrawCircle(new Vector2(x + 14f, y), 2f, EnergizedGlow);
        }
        if (unavailable)
        {
            DrawUnavailableMark(new Vector2(x + 12f, y), color, 3.5f);
        }
        DrawMapText(new Vector2(x + 30f, y + 4f), label, 11, Text, outline: false);
    }

    private void DrawRiskLegend(float x)
    {
        Rect2 box = new(x, 14f, 24f, 12f);
        DrawRect(box, new Color(Risk, 0.13f));
        DrawRect(box, Risk, false, 1f);
        for (int offset = 4; offset < 24; offset += 7)
        {
            DrawLine(new Vector2(x + offset - 3f, 25f), new Vector2(x + offset + 3f, 15f),
                new Color(Risk, 0.72f), 1f);
        }
        DrawMapText(new Vector2(x + 30f, 24f), "위험구역", 11, Text, outline: false);
    }

    private void DrawConstructionTicks(Vector2 from, Vector2 to, Color color)
    {
        Vector2 delta = to - from;
        if (delta.LengthSquared() < 1f)
        {
            return;
        }
        Vector2 normal = new Vector2(-delta.Y, delta.X).Normalized() * 5f;
        for (float fraction = 0.2f; fraction < 1f; fraction += 0.22f)
        {
            Vector2 point = from.Lerp(to, fraction);
            DrawLine(point - normal, point + normal, color, 1.3f);
        }
    }

    private void DrawUnavailableMark(Vector2 center, Color color, float radius = 6f)
    {
        DrawLine(center + new Vector2(-radius, -radius), center + new Vector2(radius, radius), color, 2f, true);
        DrawLine(center + new Vector2(radius, -radius), center + new Vector2(-radius, radius), color, 2f, true);
    }

    private void DrawMapText(
        Vector2 position,
        string value,
        int size,
        Color color,
        bool outline = true)
    {
        Font font = GetThemeDefaultFont();
        if (outline)
        {
            DrawStringOutline(font, position, value, HorizontalAlignment.Left, -1f, size, 3,
                new Color(Background, 0.92f));
        }
        DrawString(font, position, value, HorizontalAlignment.Left, -1f, size, color);
    }

    private void DrawMapLabel(Vector2 center, string value, Rect2 plot)
    {
        const int fontSize = 11;
        Font font = GetThemeDefaultFont();
        Vector2 textSize = font.GetStringSize(
            value,
            HorizontalAlignment.Left,
            -1f,
            fontSize);
        Vector2[] candidates =
        [
            center + new Vector2(11f, -9f),
            center + new Vector2(11f, textSize.Y + 13f),
            center + new Vector2(-textSize.X - 11f, -9f),
            center + new Vector2(-textSize.X - 11f, textSize.Y + 13f),
            center + new Vector2(-textSize.X / 2f, -18f),
            center + new Vector2(-textSize.X / 2f, textSize.Y + 20f),
        ];

        Rect2 chosen = default;
        float bestOverlap = float.PositiveInfinity;
        foreach (Vector2 baseline in candidates)
        {
            float x = Math.Clamp(
                baseline.X,
                plot.Position.X + 4f,
                plot.End.X - textSize.X - 4f);
            float y = Math.Clamp(
                baseline.Y,
                plot.Position.Y + textSize.Y + 4f,
                plot.End.Y - 4f);
            Rect2 bounds = new(
                new Vector2(x - 2f, y - textSize.Y - 1f),
                textSize + new Vector2(4f, 3f));
            float overlap = _mapLabelBounds.Sum(existing => OverlapArea(bounds, existing));
            if (overlap <= 0f)
            {
                chosen = bounds;
                bestOverlap = 0f;
                break;
            }
            if (overlap < bestOverlap)
            {
                chosen = bounds;
                bestOverlap = overlap;
            }
        }

        _mapLabelBounds.Add(chosen);
        DrawMapText(new Vector2(chosen.Position.X + 2f, chosen.End.Y - 2f), value, fontSize, Text);
    }

    private static float OverlapArea(Rect2 first, Rect2 second)
    {
        float width = Math.Max(0f, Math.Min(first.End.X, second.End.X) - Math.Max(first.Position.X, second.Position.X));
        float height = Math.Max(0f, Math.Min(first.End.Y, second.End.Y) - Math.Max(first.Position.Y, second.Position.Y));
        return width * height;
    }

    private void FindAsset(Vector2 localPoint, out string? nodeId, out string? edgeId)
    {
        nodeId = null;
        edgeId = null;
        if (_presentation is null)
        {
            return;
        }
        ReleaseWorldDefinition world = _presentation.Snapshot.World;
        Rect2 plot = PlotRect(world.Grid);
        foreach (ReleaseNodeDefinition node in world.Nodes)
        {
            if (localPoint.DistanceTo(ToCanvas(node.Position, world.Grid, plot)) <= 11f)
            {
                nodeId = node.NodeId;
                return;
            }
        }
        Dictionary<string, ReleaseNodeDefinition> nodes = world.Nodes.ToDictionary(item => item.NodeId);
        float best = 7f;
        foreach (ReleaseEdgeDefinition edge in world.Edges)
        {
            float distance = DistanceToSegment(
                localPoint,
                ToCanvas(nodes[edge.FromNodeId].Position, world.Grid, plot),
                ToCanvas(nodes[edge.ToNodeId].Position, world.Grid, plot));
            if (distance < best)
            {
                best = distance;
                edgeId = edge.EdgeId;
            }
        }
    }

    private void UpdateHoveredAsset(string? nodeId, string? edgeId)
    {
        if (string.Equals(_hoverNodeId, nodeId, StringComparison.Ordinal) &&
            string.Equals(_hoverEdgeId, edgeId, StringComparison.Ordinal))
        {
            return;
        }
        _hoverNodeId = nodeId;
        _hoverEdgeId = edgeId;
        AssetUnderPointerChanged?.Invoke(nodeId, edgeId);
    }

    private bool TrySnap(Vector2 position, ReleaseGridDefinition grid, out ReleasePoint point)
    {
        Rect2 plot = PlotRect(grid);
        if (!plot.HasPoint(position))
        {
            point = default;
            return false;
        }
        float scale = plot.Size.X / (grid.MaxX - grid.MinX);
        int x = checked((int)MathF.Floor(grid.MinX + ((position.X - plot.Position.X) / scale) + 0.5f));
        int y = checked((int)MathF.Floor(grid.MinY + ((position.Y - plot.Position.Y) / scale) + 0.5f));
        point = Clamp(new ReleasePoint(x, y), grid);
        return true;
    }

    private static Vector2 ToCanvas(ReleasePoint point, ReleaseGridDefinition grid, Rect2 plot) =>
        ToCanvas(new Vector2(point.X, point.Y), grid, plot);

    private static Vector2 ToCanvas(Vector2 point, ReleaseGridDefinition grid, Rect2 plot)
    {
        float scale = plot.Size.X / (grid.MaxX - grid.MinX);
        return new Vector2(
            plot.Position.X + ((point.X - grid.MinX) * scale),
            plot.Position.Y + ((point.Y - grid.MinY) * scale));
    }

    private Rect2 PlotRect(ReleaseGridDefinition grid)
    {
        float spanX = grid.MaxX - grid.MinX;
        float spanY = grid.MaxY - grid.MinY;
        float scale = Math.Min(Math.Max(1f, Size.X - 72f) / spanX, Math.Max(1f, Size.Y - 68f) / spanY);
        Vector2 plotSize = new(spanX * scale, spanY * scale);
        return new Rect2((Size - plotSize) / 2f + new Vector2(0f, 4f), plotSize);
    }

    private static float EdgeWidth(ReleaseWorldDefinition world, ReleaseEdgeDefinition edge)
    {
        long rating = world.LineClasses.Single(item => item.ClassId == edge.LineClassId).RatingKw;
        long minimum = world.LineClasses.Min(item => item.RatingKw);
        return rating > minimum ? 4.5f : 3f;
    }

    private static Color EdgeColor(ReleaseEdgeDefinition edge, ReleaseEdgeUsage usage) =>
        !edge.Commissioned ? Planned : !usage.Available ? Invalid : usage.UsedKw > 0 ? Energized : Idle;

    private static string MapLoadName(ReleaseLoadDefinition load) => load.DisplayName
        .Replace(" 생명 유지 전력", string.Empty, StringComparison.Ordinal)
        .Replace(" 필수 전력", string.Empty, StringComparison.Ordinal);

    private static string MapNodeName(string name) => name
        .Replace(" 배전 변전소", " 변전소", StringComparison.Ordinal)
        .Replace(" 발전 접속점", " 발전원", StringComparison.Ordinal);

    private static bool SharesNode(ReleaseEdgeDefinition first, ReleaseEdgeDefinition second) =>
        string.Equals(first.FromNodeId, second.FromNodeId, StringComparison.Ordinal) ||
        string.Equals(first.FromNodeId, second.ToNodeId, StringComparison.Ordinal) ||
        string.Equals(first.ToNodeId, second.FromNodeId, StringComparison.Ordinal) ||
        string.Equals(first.ToNodeId, second.ToNodeId, StringComparison.Ordinal);

    private static bool TrySegmentIntersection(
        Vector2 firstFrom,
        Vector2 firstTo,
        Vector2 secondFrom,
        Vector2 secondTo,
        out Vector2 crossing)
    {
        Vector2 first = firstTo - firstFrom;
        Vector2 second = secondTo - secondFrom;
        float determinant = Cross(first, second);
        if (MathF.Abs(determinant) < 0.001f)
        {
            crossing = default;
            return false;
        }
        Vector2 offset = secondFrom - firstFrom;
        float firstFraction = Cross(offset, second) / determinant;
        float secondFraction = Cross(offset, first) / determinant;
        if (firstFraction <= 0.02f || firstFraction >= 0.98f ||
            secondFraction <= 0.02f || secondFraction >= 0.98f)
        {
            crossing = default;
            return false;
        }
        crossing = firstFrom + (first * firstFraction);
        return true;
    }

    private static float Cross(Vector2 first, Vector2 second) =>
        (first.X * second.Y) - (first.Y * second.X);

    private static bool PointInsidePolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
    {
        bool inside = false;
        for (int current = 0, previous = polygon.Count - 1; current < polygon.Count; previous = current++)
        {
            Vector2 a = polygon[current];
            Vector2 b = polygon[previous];
            bool crosses = ((a.Y > point.Y) != (b.Y > point.Y)) &&
                point.X < (((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X);
            if (crosses)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static ReleasePoint Clamp(ReleasePoint point, ReleaseGridDefinition grid) => new(
        Math.Clamp(point.X, grid.MinX, grid.MaxX),
        Math.Clamp(point.Y, grid.MinY, grid.MaxY));

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        if (segment.LengthSquared() < 0.001f)
        {
            return point.DistanceTo(start);
        }
        float t = Math.Clamp((point - start).Dot(segment) / segment.LengthSquared(), 0f, 1f);
        return point.DistanceTo(start + (segment * t));
    }

    private static string BuildAccessibilitySummary(ReleaseMapPresentation presentation)
    {
        ReleaseConstructionSnapshot snapshot = presentation.Snapshot;
        int commissionedNodes = snapshot.World.Nodes.Count(item => item.Commissioned);
        int commissionedEdges = snapshot.World.Edges.Count(item => item.Commissioned);
        int energizedEdges = snapshot.Evaluation.Edges.Count(item => item.Available && item.UsedKw > 0);
        string pointer = presentation.PointerPoint is ReleasePoint point
            ? $"현재 격자 동쪽 {point.X}, 남쪽 {point.Y}. " +
              $"현재 도구로 {(presentation.PointerAccepted ? "선택할 수 있습니다" : "선택할 수 없습니다")}. "
            : string.Empty;
        return $"청류시 전력망. {ReleaseKoreanText.Phase(snapshot.Phase)}. " +
               $"완공 설비 {commissionedNodes}곳, 완공 선로 {commissionedEdges}구간, 통전 선로 {energizedEdges}구간. " +
               $"현재 공급 {ReleaseKoreanText.FormatPower(snapshot.Evaluation.TotalDeliveredKw)}. " +
               pointer +
               presentation.ToolDescription;
    }
}
