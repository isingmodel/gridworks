using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Gridworks.Game.Realtime.R2;

internal sealed partial class RealtimePlaceholderMap
{
#if DEBUG
    private enum VisualLayoutHandleKind
    {
        District,
        Source,
        RoadPoint,
    }

    private readonly record struct VisualLayoutHandle(
        VisualLayoutHandleKind Kind,
        string Id,
        int PointIndex,
        RealtimeVisualLayoutPoint Point,
        int WorldMaxSide)
    {
        internal string Label => Kind switch
        {
            VisualLayoutHandleKind.District => $"건물 · {Id}",
            VisualLayoutHandleKind.Source => $"발전원 · {Id}",
            VisualLayoutHandleKind.RoadPoint => $"도로 · {Id} · 점 {PointIndex + 1}",
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private bool _visualLayoutEditorEnabled;
    private VisualLayoutHandle? _selectedVisualLayoutHandle;
    private bool _visualLayoutDragging;
    private string _visualLayoutEditorStatus = "대상을 클릭해 이동하세요";
#endif

    private void ConfigureVisualLayoutEditor()
    {
#if DEBUG
        _visualLayoutEditorEnabled = string.Equals(
            OS.GetEnvironment("GRIDWORKS_VISUAL_LAYOUT_EDITOR"),
            "1",
            StringComparison.Ordinal);
        if (_visualLayoutEditorEnabled)
        {
            AccessibilityDescription =
                "시각 배치 편집 모드. 건물, 발전원과 도로점을 드래그하고 S로 저장합니다.";
        }
#endif
    }

    private bool HandleVisualLayoutEditorInput(InputEvent inputEvent)
    {
#if DEBUG
        if (!_visualLayoutEditorEnabled || _transform is null)
        {
            return false;
        }
        switch (inputEvent)
        {
            case InputEventMouseButton mouse when
                mouse.ButtonIndex == MouseButton.Left && mouse.Pressed:
                _selectedVisualLayoutHandle = NearestVisualLayoutHandle(mouse.Position);
                _visualLayoutDragging = _selectedVisualLayoutHandle is not null;
                _visualLayoutEditorStatus = _selectedVisualLayoutHandle is VisualLayoutHandle h
                    ? $"선택: {h.Label}"
                    : "선택 가능한 handle이 없습니다";
                GrabFocus();
                QueueRedraw();
                AcceptEvent();
                return true;
            case InputEventMouseButton mouse when
                mouse.ButtonIndex == MouseButton.Left && !mouse.Pressed:
                _visualLayoutDragging = false;
                AcceptEvent();
                return true;
            case InputEventMouseMotion motion when _visualLayoutDragging &&
                _selectedVisualLayoutHandle is not null:
                MoveSelectedVisualLayoutHandle(motion.Position);
                QueueRedraw();
                AcceptEvent();
                return true;
            case InputEventMouseButton mouse when
                mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown &&
                mouse.Pressed && _selectedVisualLayoutHandle is VisualLayoutHandle selected &&
                selected.Kind is VisualLayoutHandleKind.District or
                    VisualLayoutHandleKind.Source:
                ResizeSelectedVisualLayoutHandle(
                    mouse.ButtonIndex == MouseButton.WheelUp ? 20 : -20);
                QueueRedraw();
                AcceptEvent();
                return true;
            case InputEventKey key when key.Pressed && !key.Echo && key.Keycode == Key.S:
                RealtimeVisualLayoutStore.SaveCanonical(_visualLayout);
                _visualLayout = RealtimeVisualLayoutStore.LoadCanonical();
                RefreshSelectedVisualLayoutHandle();
                _visualLayoutEditorStatus = "저장 완료 · 다음 실행에도 적용됩니다";
                QueueRedraw();
                AcceptEvent();
                return true;
            case InputEventKey key when key.Pressed && !key.Echo && key.Keycode == Key.R:
                _visualLayout = RealtimeVisualLayoutStore.LoadCanonical();
                _selectedVisualLayoutHandle = null;
                _visualLayoutDragging = false;
                _visualLayoutEditorStatus = "저장된 배치로 되돌렸습니다";
                QueueRedraw();
                AcceptEvent();
                return true;
        }
        return true;
#else
        return false;
#endif
    }

    private void DrawVisualLayoutEditorOverlay()
    {
#if DEBUG
        if (!_visualLayoutEditorEnabled || _transform is null)
        {
            return;
        }
        foreach (VisualLayoutHandle handle in VisualLayoutHandles())
        {
            Vector2 canvas = _transform.WorldToCanvas(handle.Point.X, handle.Point.Y);
            bool selected = _selectedVisualLayoutHandle is VisualLayoutHandle active &&
                active.Kind == handle.Kind &&
                string.Equals(active.Id, handle.Id, StringComparison.Ordinal) &&
                active.PointIndex == handle.PointIndex;
            Color color = handle.Kind switch
            {
                VisualLayoutHandleKind.District => Color.FromHtml("4ed7cf"),
                VisualLayoutHandleKind.Source => Color.FromHtml("f2b95f"),
                VisualLayoutHandleKind.RoadPoint => Color.FromHtml("d6dde0"),
                _ => Colors.White,
            };
            float radius = selected ? 10f : handle.Kind == VisualLayoutHandleKind.RoadPoint
                ? 5f
                : 7f;
            DrawCircle(canvas, radius * _accessibilityScale,
                new Color(Color.FromHtml("0b1111"), 0.92f));
            DrawArc(canvas, radius * _accessibilityScale, 0f, Mathf.Tau, 24,
                color with { A = selected ? 1f : 0.82f },
                (selected ? 3f : 2f) * _accessibilityScale,
                true);
            if (selected)
            {
                DrawString(
                    ThemeDB.FallbackFont,
                    canvas + new Vector2(14f, -12f),
                    $"{handle.Label} · ({handle.Point.X}, {handle.Point.Y})" +
                    (handle.WorldMaxSide > 0 ? $" · 크기 {handle.WorldMaxSide}" : string.Empty),
                    HorizontalAlignment.Left,
                    -1,
                    LabelFontSize,
                    color);
            }
        }

        Rect2 banner = new(
            new Vector2(16f, 14f) * _accessibilityScale,
            new Vector2(Math.Min(760f, Size.X - 32f), 58f) * _accessibilityScale);
        DrawRect(banner, new Color(Color.FromHtml("0a1010"), 0.94f), true);
        DrawRect(banner, new Color(Color.FromHtml("f2b95f"), 0.72f), false,
            1.5f * _accessibilityScale);
        DrawString(
            ThemeDB.FallbackFont,
            banner.Position + new Vector2(14f, 22f) * _accessibilityScale,
            "VISUAL LAYOUT · 드래그 이동 · 휠 크기 · S 저장 · R 되돌리기",
            HorizontalAlignment.Left,
            -1,
            LabelFontSize,
            Color.FromHtml("f3dfb3"));
        DrawString(
            ThemeDB.FallbackFont,
            banner.Position + new Vector2(14f, 44f) * _accessibilityScale,
            _visualLayoutEditorStatus,
            HorizontalAlignment.Left,
            -1,
            LabelFontSize,
            Color.FromHtml("9ddbd5"));
#endif
    }

#if DEBUG
    private IEnumerable<VisualLayoutHandle> VisualLayoutHandles()
    {
        foreach (RealtimeVisualDistrictLayout district in _visualLayout.Districts)
        {
            yield return new VisualLayoutHandle(
                VisualLayoutHandleKind.District,
                district.NodeId,
                -1,
                district.SpriteGround,
                district.WorldMaxSide);
        }
        foreach (RealtimeVisualSourceLayout source in _visualLayout.Sources)
        {
            yield return new VisualLayoutHandle(
                VisualLayoutHandleKind.Source,
                source.NodeId,
                -1,
                source.SpriteGround,
                source.WorldMaxSide);
        }
        foreach (RealtimeVisualRoadLayout road in _visualLayout.Roads)
        {
            foreach ((RealtimeVisualLayoutPoint point, int index) in road.Points.Select(
                         (point, index) => (point, index)))
            {
                yield return new VisualLayoutHandle(
                    VisualLayoutHandleKind.RoadPoint,
                    road.RoadId,
                    index,
                    point,
                    0);
            }
        }
    }

    private VisualLayoutHandle? NearestVisualLayoutHandle(Vector2 canvasPoint)
    {
        return VisualLayoutHandles()
            .Select(handle => (Handle: handle, Distance: canvasPoint.DistanceTo(
                _transform!.WorldToCanvas(handle.Point.X, handle.Point.Y))))
            .Where(item => item.Distance <= 18f * _accessibilityScale)
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Handle.Kind)
            .ThenBy(item => item.Handle.Id, StringComparer.Ordinal)
            .ThenBy(item => item.Handle.PointIndex)
            .Select(item => (VisualLayoutHandle?)item.Handle)
            .FirstOrDefault();
    }

    private void MoveSelectedVisualLayoutHandle(Vector2 canvasPoint)
    {
        if (_selectedVisualLayoutHandle is not VisualLayoutHandle selected)
        {
            return;
        }
        MapWorldPosition world = _transform!.CanvasToWorld(canvasPoint);
        int previousX = selected.Point.X;
        int previousY = selected.Point.Y;
        selected.Point.X = Math.Clamp(Mathf.RoundToInt((float)world.X), -200, 3400);
        selected.Point.Y = Math.Clamp(Mathf.RoundToInt((float)world.Y), -200, 2300);
        if (selected.Kind == VisualLayoutHandleKind.District)
        {
            RealtimeVisualDistrictLayout district = _visualLayout.Districts.Single(item =>
                string.Equals(item.NodeId, selected.Id, StringComparison.Ordinal));
            district.Center.X = Math.Clamp(
                district.Center.X + selected.Point.X - previousX,
                -200,
                3400);
            district.Center.Y = Math.Clamp(
                district.Center.Y + selected.Point.Y - previousY,
                -200,
                2300);
        }
        _selectedVisualLayoutHandle = selected with { Point = selected.Point };
        _visualLayoutEditorStatus = $"미저장 변경 · {selected.Label}";
    }

    private void ResizeSelectedVisualLayoutHandle(int delta)
    {
        if (_selectedVisualLayoutHandle is not VisualLayoutHandle selected)
        {
            return;
        }
        int size;
        if (selected.Kind == VisualLayoutHandleKind.District)
        {
            RealtimeVisualDistrictLayout district = _visualLayout.Districts.Single(item =>
                string.Equals(item.NodeId, selected.Id, StringComparison.Ordinal));
            district.WorldMaxSide = Math.Clamp(district.WorldMaxSide + delta, 200, 1400);
            size = district.WorldMaxSide;
        }
        else
        {
            RealtimeVisualSourceLayout source = _visualLayout.Sources.Single(item =>
                string.Equals(item.NodeId, selected.Id, StringComparison.Ordinal));
            source.WorldMaxSide = Math.Clamp(source.WorldMaxSide + delta, 200, 1400);
            size = source.WorldMaxSide;
        }
        _selectedVisualLayoutHandle = selected with { WorldMaxSide = size };
        _visualLayoutEditorStatus = $"미저장 변경 · {selected.Label} · 크기 {size}";
    }

    private void RefreshSelectedVisualLayoutHandle()
    {
        if (_selectedVisualLayoutHandle is not VisualLayoutHandle selected)
        {
            return;
        }
        _selectedVisualLayoutHandle = VisualLayoutHandles().Single(handle =>
            handle.Kind == selected.Kind &&
            string.Equals(handle.Id, selected.Id, StringComparison.Ordinal) &&
            handle.PointIndex == selected.PointIndex);
    }
#endif
}
