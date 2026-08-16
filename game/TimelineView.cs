using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Gridworks.Game;

internal sealed partial class TimelineView : Control
{
    private static readonly Color LineColor = Color.FromHtml("526978");
    private static readonly Color CurrentColor = Color.FromHtml("f1f5f7");
    private static readonly Color ConstructionColor = Color.FromHtml("e0a458");
    private static readonly Color DeadlineColor = Color.FromHtml("d4c26a");
    private static readonly Color EventColor = Color.FromHtml("e66d66");
    private static readonly Color RecoveryColor = Color.FromHtml("5bc0be");

    private IReadOnlyList<TimelineMarker> _markers = Array.Empty<TimelineMarker>();
    private long _endMinute = 1;

    public void SetMarkers(IReadOnlyList<TimelineMarker> markers, long endMinute)
    {
        _markers = markers;
        _endMinute = Math.Max(1, endMinute);
        AccessibilityName = "선형 예고 타임라인. " + string.Join(". ", markers.Select(marker => marker.Text));
        AccessibilityDescription = "현재 시각, 공사 예상 완공, 병원 2회로 기한, 강변 사건 시작과 복구를 왼쪽에서 오른쪽 순서로 표시합니다.";
        QueueRedraw();
    }

    public override void _Draw()
    {
        var start = new Vector2(24, 20);
        var end = new Vector2(Math.Max(25, Size.X - 24), 20);
        DrawLine(start, end, LineColor, 3f, true);

        foreach (var marker in _markers)
        {
            var ratio = Math.Clamp(marker.Minute / (double)_endMinute, 0d, 1d);
            var point = new Vector2((float)(start.X + ((end.X - start.X) * ratio)), 20);
            DrawMarker(point, marker.Kind);
        }
    }

    private void DrawMarker(Vector2 point, TimelineMarkerKind kind)
    {
        switch (kind)
        {
            case TimelineMarkerKind.Current:
                DrawCircle(point, 7f, CurrentColor);
                DrawCircle(point, 10f, CurrentColor, false, 2f, true);
                break;
            case TimelineMarkerKind.Construction:
                DrawRect(new Rect2(point - new Vector2(6, 6), new Vector2(12, 12)), ConstructionColor);
                break;
            case TimelineMarkerKind.Deadline:
                var diamond = new[]
                {
                    point + new Vector2(0, -8), point + new Vector2(8, 0),
                    point + new Vector2(0, 8), point + new Vector2(-8, 0),
                };
                DrawColoredPolygon(diamond, DeadlineColor);
                break;
            case TimelineMarkerKind.EventStart:
                DrawColoredPolygon(new[]
                {
                    point + new Vector2(0, -9), point + new Vector2(9, 7), point + new Vector2(-9, 7),
                }, EventColor);
                break;
            case TimelineMarkerKind.Recovery:
                DrawRect(new Rect2(point - new Vector2(7, 7), new Vector2(14, 14)), RecoveryColor);
                DrawRect(new Rect2(point - new Vector2(4, 4), new Vector2(8, 8)), Color.FromHtml("10212a"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
}
