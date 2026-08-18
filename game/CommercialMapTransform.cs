using System;
using Godot;

namespace Gridworks.Game;

internal readonly record struct CommercialMapBounds(int MinX, int MaxX, int MinY, int MaxY)
{
    public double Width => checked((long)MaxX - MinX);

    public double Height => checked((long)MaxY - MinY);

    public Vector2 Center => new(
        (float)(((double)MinX + MaxX) / 2d),
        (float)(((double)MinY + MaxY) / 2d));

    public void Validate()
    {
        if (MinX >= MaxX || MinY >= MaxY)
        {
            throw new ArgumentException("지도 범위는 양의 너비와 높이를 가져야 합니다.");
        }
    }
}

internal readonly record struct CommercialWorldPosition(double X, double Y);

/// <summary>
/// Pure world/canvas camera math for the commercial map. It deliberately owns no
/// Godot node, Core session, hover state, or command state.
/// </summary>
internal sealed class CommercialMapTransform
{
    private static readonly double[] ZoomMultipliers = [1d, 1.5d, 2.25d];
    private const float PlotPadding = 28f;

    private CommercialMapBounds _bounds;
    private Vector2 _viewportSize;
    private Vector2 _center;
    private int _zoomIndex;

    public CommercialMapTransform(CommercialMapBounds bounds, Vector2 viewportSize)
    {
        bounds.Validate();
        _bounds = bounds;
        _viewportSize = ValidViewport(viewportSize);
        _center = bounds.Center;
    }

    public CommercialMapBounds Bounds => _bounds;

    public Vector2 Center => _center;

    public int ZoomIndex => _zoomIndex;

    public string ZoomLabel => _zoomIndex switch
    {
        0 => "전체 보기",
        1 => "1.5배",
        2 => "2.25배",
        _ => throw new InvalidOperationException("지원하지 않는 지도 확대 단계입니다."),
    };

    public double Scale
    {
        get
        {
            Rect2 plot = PlotRect;
            double fit = Math.Min(plot.Size.X / _bounds.Width, plot.Size.Y / _bounds.Height);
            return fit * ZoomMultipliers[_zoomIndex];
        }
    }

    public Rect2 PlotRect
    {
        get
        {
            float horizontalPadding = Math.Min(PlotPadding, _viewportSize.X * 0.08f);
            float verticalPadding = Math.Min(PlotPadding, _viewportSize.Y * 0.08f);
            return new Rect2(
                new Vector2(horizontalPadding, verticalPadding),
                new Vector2(
                    Math.Max(1f, _viewportSize.X - (horizontalPadding * 2f)),
                    Math.Max(1f, _viewportSize.Y - (verticalPadding * 2f))));
        }
    }

    public void Configure(CommercialMapBounds bounds, Vector2 viewportSize)
    {
        bounds.Validate();
        CommercialWorldPosition oldCenter = new(_center.X, _center.Y);
        _bounds = bounds;
        _viewportSize = ValidViewport(viewportSize);
        _center = ClampCenter(new Vector2((float)oldCenter.X, (float)oldCenter.Y));
    }

    public Vector2 WorldToCanvas(double worldX, double worldY)
    {
        Rect2 plot = PlotRect;
        double scale = Scale;
        return plot.GetCenter() + new Vector2(
            (float)((worldX - _center.X) * scale),
            (float)((worldY - _center.Y) * scale));
    }

    public CommercialWorldPosition CanvasToWorld(Vector2 canvasPoint)
    {
        Rect2 plot = PlotRect;
        double scale = Scale;
        Vector2 offset = canvasPoint - plot.GetCenter();
        return new CommercialWorldPosition(
            _center.X + (offset.X / scale),
            _center.Y + (offset.Y / scale));
    }

    public void SetZoomAt(int zoomIndex, Vector2 canvasAnchor)
    {
        int clampedIndex = Math.Clamp(zoomIndex, 0, ZoomMultipliers.Length - 1);
        if (clampedIndex == _zoomIndex)
        {
            return;
        }

        CommercialWorldPosition anchoredWorld = CanvasToWorld(canvasAnchor);
        _zoomIndex = clampedIndex;
        CommercialWorldPosition worldAfterZoom = CanvasToWorld(canvasAnchor);
        _center = ClampCenter(_center + new Vector2(
            (float)(anchoredWorld.X - worldAfterZoom.X),
            (float)(anchoredWorld.Y - worldAfterZoom.Y)));
    }

    public void PanByCanvasDelta(Vector2 canvasDelta)
    {
        double scale = Scale;
        _center = ClampCenter(_center - new Vector2(
            (float)(canvasDelta.X / scale),
            (float)(canvasDelta.Y / scale)));
    }

    public void Home()
    {
        _zoomIndex = 0;
        _center = _bounds.Center;
    }

    public void Follow(double worldX, double worldY, float edgeMargin)
    {
        Rect2 safe = PlotRect.Grow(-Math.Max(0f, edgeMargin));
        if (safe.Size.X <= 0f || safe.Size.Y <= 0f)
        {
            return;
        }

        Vector2 current = WorldToCanvas(worldX, worldY);
        Vector2 desired = new(
            Math.Clamp(current.X, safe.Position.X, safe.End.X),
            Math.Clamp(current.Y, safe.Position.Y, safe.End.Y));
        if (current.IsEqualApprox(desired))
        {
            return;
        }

        double scale = Scale;
        _center = ClampCenter(_center + new Vector2(
            (float)((current.X - desired.X) / scale),
            (float)((current.Y - desired.Y) / scale)));
    }

    private Vector2 ClampCenter(Vector2 requested)
    {
        Rect2 plot = PlotRect;
        double halfVisibleWidth = plot.Size.X / (Scale * 2d);
        double halfVisibleHeight = plot.Size.Y / (Scale * 2d);
        return new Vector2(
            ClampAxis(requested.X, _bounds.MinX, _bounds.MaxX, halfVisibleWidth),
            ClampAxis(requested.Y, _bounds.MinY, _bounds.MaxY, halfVisibleHeight));
    }

    private static float ClampAxis(float requested, int minimum, int maximum, double halfVisible)
    {
        if ((halfVisible * 2d) >= ((double)maximum - minimum))
        {
            return (float)(((double)minimum + maximum) / 2d);
        }
        return (float)Math.Clamp(requested, minimum + halfVisible, maximum - halfVisible);
    }

    private static Vector2 ValidViewport(Vector2 viewportSize) => new(
        Math.Max(1f, viewportSize.X),
        Math.Max(1f, viewportSize.Y));
}
