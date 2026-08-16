using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Gridworks.Game;

internal sealed partial class GridMapView : Control
{
    private static readonly Color MapBackground = Color.FromHtml("0b1720");
    private static readonly Color GridColor = Color.FromHtml("1a2a35");
    private static readonly Color TextColor = Color.FromHtml("e6eef2");
    private static readonly Color MutedText = Color.FromHtml("98a9b3");
    private static readonly Color PlannedColor = Color.FromHtml("77858e");
    private static readonly Color BuildingColor = Color.FromHtml("e0a458");
    private static readonly Color AvailableColor = Color.FromHtml("8cb6c8");
    private static readonly Color EnergizedColor = Color.FromHtml("5bc0be");
    private static readonly Color RemovedColor = Color.FromHtml("e66d66");
    private static readonly Color RiskFill = new(0.52f, 0.23f, 0.18f, 0.22f);
    private static readonly Color RiskStroke = Color.FromHtml("a55c4b");
    private static readonly Color RiskText = Color.FromHtml("dc8b78");
    private static readonly Color ServiceFill = new(0.35f, 0.58f, 0.72f, 0.12f);
    private static readonly Color ServiceStroke = Color.FromHtml("81aeca");

    private MapDefinition? _definition;
    private MapState? _state;

    public void SetModel(MapDefinition definition, MapState state)
    {
        _definition = definition;
        _state = state;
        AccessibilityName = BuildAccessibilitySummary(state);
        AccessibilityDescription = "전력망 지도. 선 모양과 문장으로 공사, 통전, 무전압, 사용불가 상태를 함께 표시합니다.";
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_definition is null || _state is null)
        {
            return;
        }

        DrawRect(new Rect2(Vector2.Zero, Size), MapBackground);
        DrawGrid(_definition);
        DrawServiceAreas(_definition);
        DrawRiskAreas(_definition);
        DrawEdges(_definition, _state);
        DrawNodes(_definition, _state);
        DrawLegend();
    }

    private void DrawGrid(MapDefinition definition)
    {
        for (var x = 0; x <= (int)definition.Width; x++)
        {
            var top = ToCanvas(new MapPoint(x, 0), definition);
            var bottom = ToCanvas(new MapPoint(x, definition.Height), definition);
            DrawLine(top, bottom, GridColor, 1f);
        }

        for (var y = 0; y <= (int)definition.Height; y++)
        {
            var left = ToCanvas(new MapPoint(0, y), definition);
            var right = ToCanvas(new MapPoint(definition.Width, y), definition);
            DrawLine(left, right, GridColor, 1f);
        }
    }

    private void DrawServiceAreas(MapDefinition definition)
    {
        foreach (var area in definition.ServiceAreas)
        {
            var center = ToCanvas(area.Center, definition);
            var major = area.RadiusX * UsableWidth / definition.Width;
            var minor = area.RadiusY * UsableHeight / definition.Height;
            DrawEllipse(center, major, minor, ServiceFill, true);
            DrawEllipse(center, major, minor, ServiceStroke, false, 2f, true);
            DrawString(ThemeDB.FallbackFont, center + new Vector2(-major, -minor - 7),
                "접속 가능 권역 ≠ 전력 공급", HorizontalAlignment.Left, -1f, 13, ServiceStroke);
        }
    }

    private void DrawRiskAreas(MapDefinition definition)
    {
        foreach (var area in definition.RiskAreas)
        {
            var points = area.Points.Select(point => ToCanvas(point, definition)).ToArray();
            DrawColoredPolygon(points, RiskFill);
            DrawPolyline(points.Append(points[0]).ToArray(), RiskStroke, 2f, true);

            for (var index = 0; index < points.Length; index += 2)
            {
                var next = points[(index + 1) % points.Length];
                DrawDashedLine(points[index], next, RiskStroke, 1f, 5f, true, true);
            }
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(245, Size.Y - 22),
            "//// 강변 기존 통로: 공간 공통위험", HorizontalAlignment.Left, -1f, 13, RiskText);
    }

    private void DrawEdges(MapDefinition definition, MapState state)
    {
        foreach (var edge in definition.Edges)
        {
            var points = edge.Points.Select(point => ToCanvas(point, definition)).ToArray();
            var edgeState = ResolveEdgeState(edge.Id, state);
            DrawEdgeSegments(points, edgeState);

            if (!string.IsNullOrEmpty(edge.DisplayName))
            {
                var labelPoint = points[points.Length / 2] + new Vector2(7, -7);
                var prefix = state.SelectedCorridorEdgeId == edge.Id ? "▶ " : string.Empty;
                DrawString(ThemeDB.FallbackFont, labelPoint, prefix + edge.DisplayName,
                    HorizontalAlignment.Left, -1f, 13, EdgeColor(edgeState));
            }
        }
    }

    private void DrawEdgeSegments(IReadOnlyList<Vector2> points, EdgeVisualState state)
    {
        var color = EdgeColor(state);
        for (var index = 0; index < points.Count - 1; index++)
        {
            var from = points[index];
            var to = points[index + 1];
            switch (state)
            {
                case EdgeVisualState.Planned:
                    DrawDashedLine(from, to, color, 2f, 3f, true, true);
                    break;
                case EdgeVisualState.Building:
                    DrawDashedLine(from, to, color, 4f, 8f, true, true);
                    DrawConstructionTicks(from, to, color);
                    break;
                case EdgeVisualState.Available:
                    DrawLine(from, to, color, 2f, true);
                    DrawLine(from + new Vector2(0, 3), to + new Vector2(0, 3), color, 1f, true);
                    break;
                case EdgeVisualState.Energized:
                    DrawLine(from, to, Color.FromHtml("10252c"), 7f, true);
                    DrawLine(from, to, color, 4f, true);
                    break;
                case EdgeVisualState.Removed:
                    DrawDashedLine(from, to, color, 5f, 6f, true, true);
                    DrawRemovalCross((from + to) / 2f, color);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state));
            }
        }
    }

    private void DrawConstructionTicks(Vector2 from, Vector2 to, Color color)
    {
        var midpoint = (from + to) / 2f;
        var direction = (to - from).Normalized();
        var normal = new Vector2(-direction.Y, direction.X) * 5f;
        DrawLine(midpoint - normal, midpoint + normal, color, 2f, true);
    }

    private void DrawRemovalCross(Vector2 center, Color color)
    {
        DrawLine(center - new Vector2(5, 5), center + new Vector2(5, 5), color, 3f, true);
        DrawLine(center + new Vector2(-5, 5), center + new Vector2(5, -5), color, 3f, true);
    }

    private void DrawNodes(MapDefinition definition, MapState state)
    {
        foreach (var node in definition.Nodes)
        {
            var point = ToCanvas(node.Position, definition);
            switch (node.Kind)
            {
                case MapNodeKind.Generator:
                    DrawRect(new Rect2(point - new Vector2(11, 9), new Vector2(22, 18)), Color.FromHtml("315467"));
                    DrawRect(new Rect2(point - new Vector2(11, 9), new Vector2(22, 18)), TextColor, false, 2f);
                    DrawNodeLabel(point, "가스발전 · 온라인");
                    break;
                case MapNodeKind.Bus:
                    DrawCircle(point, 7f, Color.FromHtml("607987"));
                    DrawCircle(point, 7f, TextColor, false, 2f, true);
                    break;
                case MapNodeKind.Substation:
                    var diamond = new[]
                    {
                        point + new Vector2(0, -10),
                        point + new Vector2(10, 0),
                        point + new Vector2(0, 10),
                        point + new Vector2(-10, 0),
                    };
                    DrawColoredPolygon(diamond, Color.FromHtml("315467"));
                    DrawPolyline(diamond.Append(diamond[0]).ToArray(), TextColor, 2f, true);
                    break;
                case MapNodeKind.Town:
                    DrawRect(new Rect2(point - new Vector2(10, 10), new Vector2(20, 20)), Color.FromHtml("795f47"));
                    DrawRect(new Rect2(point - new Vector2(10, 10), new Vector2(20, 20)), TextColor, false, 2f);
                    DrawNodeLabel(point, state.TownUtilityDelivered ? "마을 · utility 공급" : "마을 · utility 끊김");
                    break;
                case MapNodeKind.Hospital:
                    DrawRect(new Rect2(point - new Vector2(11, 11), new Vector2(22, 22)), Color.FromHtml("4b6575"));
                    DrawLine(point - new Vector2(0, 7), point + new Vector2(0, 7), TextColor, 3f);
                    DrawLine(point - new Vector2(7, 0), point + new Vector2(7, 0), TextColor, 3f);
                    DrawNodeLabel(point,
                        $"병원 · utility {(state.HospitalUtilityDelivered ? "공급" : "끊김")} · P0 {state.HospitalP0Source}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void DrawNodeLabel(Vector2 point, string text)
    {
        DrawString(ThemeDB.FallbackFont, point + new Vector2(13, 22), text,
            HorizontalAlignment.Left, -1f, 13, TextColor);
    }

    private void DrawLegend()
    {
        const string legend = "━ 통전   ═ 대기/무전압   ┄ 계획   ┅| 공사 중   ┅× 사용불가";
        DrawString(ThemeDB.FallbackFont, new Vector2(18, Size.Y - 8), legend,
            HorizontalAlignment.Left, -1f, 13, MutedText);
    }

    private Vector2 ToCanvas(MapPoint point, MapDefinition definition)
    {
        return new Vector2(
            24f + (point.X / definition.Width * UsableWidth),
            20f + (point.Y / definition.Height * UsableHeight));
    }

    private float UsableWidth => Math.Max(1f, Size.X - 48f);

    private float UsableHeight => Math.Max(1f, Size.Y - 76f);

    private static EdgeVisualState ResolveEdgeState(string edgeId, MapState state)
    {
        if (state.RemovedEdgeIds.Contains(edgeId))
        {
            return EdgeVisualState.Removed;
        }

        if (state.EnergizedEdgeIds.Contains(edgeId))
        {
            return EdgeVisualState.Energized;
        }

        if (state.BuildingEdgeIds.Contains(edgeId))
        {
            return EdgeVisualState.Building;
        }

        if (state.CommissionedEdgeIds.Contains(edgeId))
        {
            return EdgeVisualState.Available;
        }

        return EdgeVisualState.Planned;
    }

    private static Color EdgeColor(EdgeVisualState state) => state switch
    {
        EdgeVisualState.Planned => PlannedColor,
        EdgeVisualState.Building => BuildingColor,
        EdgeVisualState.Available => AvailableColor,
        EdgeVisualState.Energized => EnergizedColor,
        EdgeVisualState.Removed => RemovedColor,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static string BuildAccessibilitySummary(MapState state)
    {
        var construction = state.BuildingEdgeIds.Count == 0 ? "공사 중 선로 없음" : "선로 공사 중";
        var removal = state.RemovedEdgeIds.Count == 0 ? "사용불가 선로 없음" : "사건으로 사용불가 선로 있음";
        return $"전력망 지도. 마을 utility {(state.TownUtilityDelivered ? "공급" : "끊김")}. " +
               $"병원 utility {(state.HospitalUtilityDelivered ? "공급" : "끊김")}. " +
               $"병원 P0 {state.HospitalP0Source}. {construction}. {removal}.";
    }

    private enum EdgeVisualState
    {
        Planned,
        Building,
        Available,
        Energized,
        Removed,
    }
}
