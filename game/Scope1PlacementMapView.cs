using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gridworks.Core;
using Godot;

namespace Gridworks.Game;

internal sealed partial class Scope1PlacementMapView : Control
{
    private static readonly Color MapBackground = Color.FromHtml("0a151d");
    private static readonly Color GridColor = Color.FromHtml("20313b");
    private static readonly Color CoordinateColor = Color.FromHtml("778b96");
    private static readonly Color TextColor = Color.FromHtml("e6eef2");
    private static readonly Color MutedColor = Color.FromHtml("9babb4");
    private static readonly Color DraftColor = Color.FromHtml("93a3ab");
    private static readonly Color BuildingColor = Color.FromHtml("e0a458");
    private static readonly Color EnergizedColor = Color.FromHtml("5bc0be");
    private static readonly Color InvalidColor = Color.FromHtml("e66d66");
    private static readonly Color RangeColor = Color.FromHtml("668ba0");
    private static readonly Color SourceColor = Color.FromHtml("4d98a4");

    private Scope1Scenario? _scenario;
    private Scope1View? _view;
    private Scope1PreviewResult? _pointerPreview;

    public event Action<Scope1Point?>? PointerChanged;

    public event Action<Scope1Point>? SupportRequested;

    public override void _Ready()
    {
        MouseExited += OnMouseExited;
    }

    public void SetModel(
        Scope1Scenario scenario,
        Scope1View view,
        Scope1PreviewResult? pointerPreview)
    {
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _pointerPreview = pointerPreview;
        AccessibilityName = BuildAccessibilitySummary(view, pointerPreview);
        AccessibilityDescription =
            "수동 선로 지도. 마우스로 격자 교차점을 선택합니다. 범위 원과 선 모양, 문장으로 거리 제한과 공사 상태를 함께 표시합니다.";
        QueueRedraw();
    }

    public Vector2 CanvasPointForGridPoint(Scope1Point point)
    {
        Scope1Scenario scenario = _scenario
            ?? throw new InvalidOperationException("Map model is not ready.");
        return ToCanvas(point, scenario, PlotRect(scenario));
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (_scenario is null)
        {
            return;
        }

        switch (inputEvent)
        {
            case InputEventMouseMotion motion when TrySnap(motion.Position, _scenario, out Scope1Point hover):
                PointerChanged?.Invoke(hover);
                AcceptEvent();
                break;
            case InputEventMouseButton button when
                button.ButtonIndex == MouseButton.Left &&
                button.Pressed &&
                TrySnap(button.Position, _scenario, out Scope1Point requested):
                PointerChanged?.Invoke(requested);
                SupportRequested?.Invoke(requested);
                AcceptEvent();
                break;
        }
    }

    public override void _Draw()
    {
        if (_scenario is null || _view is null)
        {
            return;
        }

        Rect2 plot = PlotRect(_scenario);
        DrawRect(new Rect2(Vector2.Zero, Size), MapBackground);
        DrawGrid(_scenario, plot);
        DrawRange(_scenario, _view, plot);
        DrawPath(_scenario, _view, plot);
        DrawGhost(_scenario, _view, plot);
        DrawEndpoints(_scenario, _view, plot);
        DrawSupports(_scenario, _view, plot);
        DrawLegend(_view);
    }

    private void DrawGrid(Scope1Scenario scenario, Rect2 plot)
    {
        for (int x = scenario.MapBounds.MinX; x <= scenario.MapBounds.MaxX; x++)
        {
            Vector2 top = ToCanvas(new Scope1Point(x, scenario.MapBounds.MinY), scenario, plot);
            Vector2 bottom = ToCanvas(new Scope1Point(x, scenario.MapBounds.MaxY), scenario, plot);
            DrawLine(top, bottom, GridColor, 1f);
            DrawString(
                ThemeDB.FallbackFont,
                bottom + new Vector2(-4f, 22f),
                x.ToString(CultureInfo.InvariantCulture),
                HorizontalAlignment.Left,
                -1f,
                12,
                CoordinateColor);
        }

        for (int y = scenario.MapBounds.MinY; y <= scenario.MapBounds.MaxY; y++)
        {
            Vector2 left = ToCanvas(new Scope1Point(scenario.MapBounds.MinX, y), scenario, plot);
            Vector2 right = ToCanvas(new Scope1Point(scenario.MapBounds.MaxX, y), scenario, plot);
            DrawLine(left, right, GridColor, 1f);
            DrawString(
                ThemeDB.FallbackFont,
                left + new Vector2(-28f, 5f),
                y.ToString(CultureInfo.InvariantCulture),
                HorizontalAlignment.Right,
                20f,
                12,
                CoordinateColor);
        }
    }

    private void DrawRange(Scope1Scenario scenario, Scope1View view, Rect2 plot)
    {
        if (view.Phase != Scope1Phase.Drafting)
        {
            return;
        }

        Scope1Point last = LastEndpoint(scenario, view);
        Vector2 center = ToCanvas(last, scenario, plot);
        float radius = scenario.MaxSpan * GridScale(scenario, plot);
        DrawArc(center, radius, 0f, Mathf.Tau, 96, RangeColor, 2f, true);
        DrawString(
            ThemeDB.FallbackFont,
            center + new Vector2(-radius, -radius - 9f),
            $"MaxSpan {scenario.MaxSpan} {scenario.PositionUnit}",
            HorizontalAlignment.Left,
            -1f,
            13,
            RangeColor);
    }

    private void DrawPath(Scope1Scenario scenario, Scope1View view, Rect2 plot)
    {
        var points = new List<Scope1Point> { scenario.Source };
        points.AddRange(view.SupportPositions);
        if (view.Phase != Scope1Phase.Drafting)
        {
            points.Add(scenario.Target);
        }

        for (int index = 0; index < points.Count - 1; index++)
        {
            Vector2 from = ToCanvas(points[index], scenario, plot);
            Vector2 to = ToCanvas(points[index + 1], scenario, plot);
            switch (view.Phase)
            {
                case Scope1Phase.Drafting:
                    DrawDashedLine(from, to, DraftColor, 3f, 7f, true, true);
                    break;
                case Scope1Phase.Building:
                    DrawDashedLine(from, to, BuildingColor, 5f, 10f, true, true);
                    DrawConstructionTick(from, to);
                    break;
                case Scope1Phase.Commissioned:
                    DrawLine(from, to, Color.FromHtml("102b31"), 9f, true);
                    DrawLine(from, to, EnergizedColor, 5f, true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(view.Phase));
            }
        }
    }

    private void DrawGhost(Scope1Scenario scenario, Scope1View view, Rect2 plot)
    {
        if (view.Phase != Scope1Phase.Drafting || _pointerPreview is null)
        {
            return;
        }

        Vector2 from = ToCanvas(_pointerPreview.From, scenario, plot);
        Vector2 to = ToCanvas(_pointerPreview.To, scenario, plot);
        if (_pointerPreview.Accepted)
        {
            DrawLine(from, to, EnergizedColor, 3f, true);
            DrawCircle(to, 7f, EnergizedColor, false, 2f, true);
        }
        else
        {
            DrawDashedLine(from, to, InvalidColor, 3f, 8f, true, true);
            DrawCross(to, InvalidColor, 7f);
        }
    }

    private void DrawEndpoints(Scope1Scenario scenario, Scope1View view, Rect2 plot)
    {
        Vector2 source = ToCanvas(scenario.Source, scenario, plot);
        DrawCircle(source, 14f, SourceColor);
        DrawCircle(source, 14f, TextColor, false, 2f, true);
        DrawString(
            ThemeDB.FallbackFont,
            source + new Vector2(18f, -16f),
            "SOURCE · 통전",
            HorizontalAlignment.Left,
            -1f,
            14,
            TextColor);

        Vector2 target = ToCanvas(scenario.Target, scenario, plot);
        if (view.TargetEnergized)
        {
            DrawCircle(target, 14f, EnergizedColor);
            DrawCircle(target, 14f, TextColor, false, 2f, true);
        }
        else
        {
            DrawCircle(target, 14f, MapBackground);
            DrawCircle(target, 14f, MutedColor, false, 3f, true);
            DrawCross(target, MutedColor, 7f);
        }

        DrawString(
            ThemeDB.FallbackFont,
            target + new Vector2(-142f, -16f),
            view.TargetEnergized ? "TARGET · 통전" : "TARGET · 무전압",
            HorizontalAlignment.Right,
            124f,
            14,
            view.TargetEnergized ? EnergizedColor : MutedColor);
    }

    private void DrawSupports(Scope1Scenario scenario, Scope1View view, Rect2 plot)
    {
        foreach (Scope1Point support in view.SupportPositions)
        {
            Vector2 point = ToCanvas(support, scenario, plot);
            Color color = view.Phase switch
            {
                Scope1Phase.Drafting => DraftColor,
                Scope1Phase.Building => BuildingColor,
                Scope1Phase.Commissioned => EnergizedColor,
                _ => throw new ArgumentOutOfRangeException(nameof(view.Phase)),
            };
            DrawLine(point + new Vector2(0f, -12f), point + new Vector2(0f, 12f), color, 4f, true);
            DrawLine(point + new Vector2(-8f, -7f), point + new Vector2(8f, -7f), color, 3f, true);
            DrawCircle(point, 5f, MapBackground);
            DrawCircle(point, 5f, color, false, 2f, true);
        }
    }

    private void DrawLegend(Scope1View view)
    {
        string phase = view.Phase switch
        {
            Scope1Phase.Drafting => "DRAFTING · ┄ 계획선 · 목표 무전압",
            Scope1Phase.Building => "BUILDING · ┅| 공사 중 · 목표 무전압",
            Scope1Phase.Commissioned => "COMMISSIONED · ━ 통전선 · 목표 통전",
            _ => throw new ArgumentOutOfRangeException(nameof(view.Phase)),
        };
        Color color = view.Phase switch
        {
            Scope1Phase.Drafting => DraftColor,
            Scope1Phase.Building => BuildingColor,
            Scope1Phase.Commissioned => EnergizedColor,
            _ => throw new ArgumentOutOfRangeException(nameof(view.Phase)),
        };
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(20f, Size.Y - 15f),
            phase,
            HorizontalAlignment.Left,
            -1f,
            14,
            color);
    }

    private bool TrySnap(Vector2 localPosition, Scope1Scenario scenario, out Scope1Point point)
    {
        Rect2 plot = PlotRect(scenario);
        Vector2 end = plot.Position + plot.Size;
        if (localPosition.X < plot.Position.X || localPosition.X > end.X ||
            localPosition.Y < plot.Position.Y || localPosition.Y > end.Y)
        {
            point = default;
            return false;
        }

        float scale = GridScale(scenario, plot);
        float gridX = scenario.MapBounds.MinX + ((localPosition.X - plot.Position.X) / scale);
        float gridY = scenario.MapBounds.MinY + ((localPosition.Y - plot.Position.Y) / scale);
        int snappedX = checked((int)MathF.Floor(gridX + 0.5f));
        int snappedY = checked((int)MathF.Floor(gridY + 0.5f));
        point = new Scope1Point(snappedX, snappedY);
        return true;
    }

    private static Vector2 ToCanvas(Scope1Point point, Scope1Scenario scenario, Rect2 plot)
    {
        float scale = GridScale(scenario, plot);
        return new Vector2(
            plot.Position.X + ((point.X - scenario.MapBounds.MinX) * scale),
            plot.Position.Y + ((point.Y - scenario.MapBounds.MinY) * scale));
    }

    private Rect2 PlotRect(Scope1Scenario scenario)
    {
        float spanX = scenario.MapBounds.MaxX - scenario.MapBounds.MinX;
        float spanY = scenario.MapBounds.MaxY - scenario.MapBounds.MinY;
        float availableWidth = Math.Max(1f, Size.X - 116f);
        float availableHeight = Math.Max(1f, Size.Y - 126f);
        float scale = Math.Min(availableWidth / spanX, availableHeight / spanY);
        var plotSize = new Vector2(spanX * scale, spanY * scale);
        return new Rect2((Size - plotSize) / 2f + new Vector2(0f, -5f), plotSize);
    }

    private static float GridScale(Scope1Scenario scenario, Rect2 plot)
    {
        float spanX = scenario.MapBounds.MaxX - scenario.MapBounds.MinX;
        return plot.Size.X / spanX;
    }

    private static Scope1Point LastEndpoint(Scope1Scenario scenario, Scope1View view) =>
        view.SupportPositions.Count == 0 ? scenario.Source : view.SupportPositions[^1];

    private static string BuildAccessibilitySummary(
        Scope1View view,
        Scope1PreviewResult? preview)
    {
        string phase = view.Phase switch
        {
            Scope1Phase.Drafting => "선로 계획 중",
            Scope1Phase.Building => "선로 공사 중",
            Scope1Phase.Commissioned => "선로 완공",
            _ => throw new ArgumentOutOfRangeException(nameof(view.Phase)),
        };
        string target = view.TargetEnergized ? "목표 통전" : "목표 무전압";
        string pointer = preview is null
            ? string.Empty
            : preview.Accepted
                ? " 현재 위치 배치 가능."
                : " 현재 위치 배치 불가.";
        return $"수동 선로 지도. {phase}. 전신주 {view.SupportPositions.Count}개. {target}.{pointer}";
    }

    private void OnMouseExited() => PointerChanged?.Invoke(null);

    private void DrawConstructionTick(Vector2 from, Vector2 to)
    {
        Vector2 center = (from + to) / 2f;
        Vector2 direction = (to - from).Normalized();
        Vector2 normal = new(-direction.Y, direction.X);
        DrawLine(center - (normal * 7f), center + (normal * 7f), BuildingColor, 3f, true);
    }

    private void DrawCross(Vector2 center, Color color, float radius)
    {
        DrawLine(center - new Vector2(radius, radius), center + new Vector2(radius, radius), color, 2f, true);
        DrawLine(center + new Vector2(-radius, radius), center + new Vector2(radius, -radius), color, 2f, true);
    }
}
