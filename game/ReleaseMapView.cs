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
    private static readonly Color Background = Color.FromHtml("07141b");
    private static readonly Color MinorGrid = Color.FromHtml("162c35");
    private static readonly Color MajorGrid = Color.FromHtml("31505b");
    private static readonly Color Text = Color.FromHtml("dce8eb");
    private static readonly Color Muted = Color.FromHtml("738991");
    private static readonly Color Energized = Color.FromHtml("64c5cc");
    private static readonly Color Planned = Color.FromHtml("e0a44f");
    private static readonly Color Invalid = Color.FromHtml("e46c62");
    private static readonly Color Focus = Color.FromHtml("f0ce78");
    private static readonly Color Risk = Color.FromHtml("c55d63");
    private static readonly Color River = Color.FromHtml("102d3a");
    private static readonly Color Road = Color.FromHtml("34352f");
    private ReleaseMapPresentation? _presentation;
    private ReleasePoint _keyboardPoint = new(0, 0);
    private string? _hoverNodeId;
    private string? _hoverEdgeId;

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
        };
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
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
        DrawTerrain(grid, plot);
        DrawGrid(grid, plot);
        DrawRiskAreas(snapshot.World, grid, plot);
        DrawEdges(snapshot, grid, plot);
        DrawLineDraft(snapshot, grid, plot);
        DrawLoads(snapshot.World, grid, plot);
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
        FindAsset(
            ToCanvas(point, grid, PlotRect(grid)),
            out string? nodeId,
            out string? edgeId);
        UpdateHoveredAsset(nodeId, edgeId);
    }

    private void DrawTerrain(ReleaseGridDefinition grid, Rect2 plot)
    {
        Vector2[] river =
        [
            ToCanvas(new ReleasePoint(11, grid.MinY), grid, plot),
            ToCanvas(new ReleasePoint(15, grid.MinY), grid, plot),
            ToCanvas(new ReleasePoint(16, grid.MaxY), grid, plot),
            ToCanvas(new ReleasePoint(12, grid.MaxY), grid, plot),
        ];
        DrawColoredPolygon(river, River);
        DrawLine(
            ToCanvas(new ReleasePoint(grid.MinX, 17), grid, plot),
            ToCanvas(new ReleasePoint(grid.MaxX, 5), grid, plot),
            Road,
            7f);
    }

    private void DrawGrid(ReleaseGridDefinition grid, Rect2 plot)
    {
        for (int x = grid.MinX; x <= grid.MaxX; x++)
        {
            bool major = (x - grid.MinX) % grid.MajorStep == 0;
            Vector2 top = ToCanvas(new ReleasePoint(x, grid.MinY), grid, plot);
            Vector2 bottom = ToCanvas(new ReleasePoint(x, grid.MaxY), grid, plot);
            DrawLine(top, bottom, major ? MajorGrid : MinorGrid, major ? 1.4f : 0.65f);
            if (major)
            {
                DrawString(ThemeDB.FallbackFont, bottom + new Vector2(-7f, 18f),
                    x.ToString(CultureInfo.InvariantCulture), HorizontalAlignment.Left, -1f, 11, Muted);
            }
        }
        for (int y = grid.MinY; y <= grid.MaxY; y++)
        {
            bool major = (y - grid.MinY) % grid.MajorStep == 0;
            Vector2 left = ToCanvas(new ReleasePoint(grid.MinX, y), grid, plot);
            Vector2 right = ToCanvas(new ReleasePoint(grid.MaxX, y), grid, plot);
            DrawLine(left, right, major ? MajorGrid : MinorGrid, major ? 1.4f : 0.65f);
            if (major)
            {
                DrawString(ThemeDB.FallbackFont, left + new Vector2(-25f, 4f),
                    y.ToString(CultureInfo.InvariantCulture), HorizontalAlignment.Right, 20f, 11, Muted);
            }
        }
    }

    private void DrawRiskAreas(ReleaseWorldDefinition world, ReleaseGridDefinition grid, Rect2 plot)
    {
        foreach (ReleaseRiskAreaDefinition area in world.RiskAreas)
        {
            Vector2[] polygon = area.Polygon.Select(point => ToCanvas(point, grid, plot)).ToArray();
            DrawColoredPolygon(polygon, new Color(Risk, 0.13f));
            for (int index = 0; index < polygon.Length; index++)
            {
                DrawDashedLine(polygon[index], polygon[(index + 1) % polygon.Length], Risk, 2f, 7f);
            }
            DrawString(ThemeDB.FallbackFont, polygon[0] + new Vector2(4f, -7f), area.DisplayName,
                HorizontalAlignment.Left, -1f, 12, new Color(Risk, 0.9f));
        }
    }

    private void DrawEdges(ReleaseConstructionSnapshot snapshot, ReleaseGridDefinition grid, Rect2 plot)
    {
        Dictionary<string, ReleaseNodeDefinition> nodes = snapshot.World.Nodes.ToDictionary(item => item.NodeId);
        Dictionary<string, ReleaseEdgeUsage> usages = snapshot.Evaluation.Edges.ToDictionary(item => item.EdgeId);
        foreach (ReleaseEdgeDefinition edge in snapshot.World.Edges)
        {
            Vector2 from = ToCanvas(nodes[edge.FromNodeId].Position, grid, plot);
            Vector2 to = ToCanvas(nodes[edge.ToNodeId].Position, grid, plot);
            ReleaseEdgeUsage usage = usages[edge.EdgeId];
            bool selected = string.Equals(_presentation!.SelectedEdgeId, edge.EdgeId, StringComparison.Ordinal);
            Color color = edge.Commissioned
                ? usage.UsedKw > 0 ? Energized : Muted
                : Planned;
            float width = selected ? 6f : 3.5f;
            if (edge.Commissioned)
            {
                DrawLine(from, to, color, width, true);
            }
            else
            {
                DrawDashedLine(from, to, color, width, 8f, true, true);
                DrawConstructionTicks(from, to, color);
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
            DrawDashedLine(from, to, _presentation.PointerAccepted ? Planned : Invalid, 2.5f, 7f, true, true);
        }
        foreach (ReleasePoint point in draft.IntermediatePoints)
        {
            DrawCircle(ToCanvas(point, grid, plot), 6f, Planned);
            DrawCircle(ToCanvas(point, grid, plot), 10f, Planned, false, 2f);
        }
    }

    private void DrawLoads(ReleaseWorldDefinition world, ReleaseGridDefinition grid, Rect2 plot)
    {
        foreach (ReleaseLoadDefinition load in world.Loads)
        {
            Vector2 center = ToCanvas(load.Position, grid, plot);
            DrawCircle(center, 5f, Color.FromHtml("d7b97a"));
            DrawString(ThemeDB.FallbackFont, center + new Vector2(8f, -7f), load.DisplayName,
                HorizontalAlignment.Left, -1f, 11, Text);
        }
    }

    private void DrawNodes(ReleaseConstructionSnapshot snapshot, ReleaseGridDefinition grid, Rect2 plot)
    {
        Dictionary<string, ReleaseNodeClassDefinition> classes =
            snapshot.World.NodeClasses.ToDictionary(item => item.ClassId);
        foreach (ReleaseNodeDefinition node in snapshot.World.Nodes)
        {
            ReleaseNodeClassDefinition nodeClass = classes[node.ClassId];
            Vector2 center = ToCanvas(node.Position, grid, plot);
            Color color = node.Commissioned ? Energized : Planned;
            float size = nodeClass.Kind switch
            {
                ReleaseNodeKind.SourceTerminal => 10f,
                ReleaseNodeKind.Substation => 9f,
                ReleaseNodeKind.DedicatedLoadTerminal => 7f,
                _ => 5.5f,
            };
            switch (nodeClass.Kind)
            {
                case ReleaseNodeKind.Substation:
                    DrawRect(new Rect2(center - Vector2.One * size, Vector2.One * size * 2f),
                        new Color(color, node.Commissioned ? 0.72f : 0.28f));
                    DrawRect(new Rect2(center - Vector2.One * size, Vector2.One * size * 2f),
                        color, false, 2f);
                    break;
                case ReleaseNodeKind.SourceTerminal:
                    Vector2[] diamond =
                    [
                        center + new Vector2(0f, -size), center + new Vector2(size, 0f),
                        center + new Vector2(0f, size), center + new Vector2(-size, 0f),
                    ];
                    DrawColoredPolygon(diamond, new Color(color, 0.7f));
                    DrawPolyline(diamond.Append(diamond[0]).ToArray(), color, 2f, true);
                    break;
                default:
                    DrawCircle(center, size, color);
                    DrawCircle(center, size + 3f, color, false, 1.5f);
                    break;
            }
            if (!node.Commissioned)
            {
                DrawConstructionTicks(center - Vector2.One * 8f, center + Vector2.One * 8f, Planned);
            }
            if (string.Equals(_presentation!.SelectedNodeId, node.NodeId, StringComparison.Ordinal))
            {
                DrawCircle(center, size + 7f, Focus, false, 2.5f);
            }
            if (nodeClass.Kind is ReleaseNodeKind.SourceTerminal or ReleaseNodeKind.Substation)
            {
                DrawString(ThemeDB.FallbackFont, center + new Vector2(10f, 16f), node.DisplayName,
                    HorizontalAlignment.Left, -1f, 11, Text);
            }
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
        ReleasePoint point = _presentation!.PointerPoint ?? _keyboardPoint;
        Vector2 center = ToCanvas(point, grid, plot);
        Color color = _presentation.PointerPoint is null || _presentation.PointerAccepted ? Focus : Invalid;
        DrawCircle(center, 10f, color, false, 2f);
    }

    private void DrawLegend()
    {
        float y = 18f;
        DrawLine(new Vector2(16f, y), new Vector2(42f, y), Energized, 4f);
        DrawString(ThemeDB.FallbackFont, new Vector2(48f, y + 4f), "공급 중", HorizontalAlignment.Left, -1f, 11, Text);
        DrawDashedLine(new Vector2(118f, y), new Vector2(144f, y), Planned, 3f, 6f);
        DrawString(ThemeDB.FallbackFont, new Vector2(150f, y + 4f), "계획·공사", HorizontalAlignment.Left, -1f, 11, Text);
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

    private static Vector2 ToCanvas(ReleasePoint point, ReleaseGridDefinition grid, Rect2 plot)
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
        return $"청류시 전력망. {ReleaseKoreanText.Phase(snapshot.Phase)}. " +
               $"완공 설비 {commissionedNodes}곳, 완공 선로 {commissionedEdges}구간. " +
               $"현재 공급 {ReleaseKoreanText.FormatPower(snapshot.Evaluation.TotalDeliveredKw)}. " +
               presentation.ToolDescription;
    }
}
