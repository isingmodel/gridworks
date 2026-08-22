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
    private static readonly double[] ZoomMultipliers = [1.00d, 1.50d, 2.25d];
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
        _center = ReferenceHomeCenter(bounds);
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

    /// <summary>
    /// Horizontal pixels per world unit in the fixed 2:1 isometric projection.
    /// Kept as Scale for callers which need a conservative scalar size.
    /// </summary>
    public double Scale => ScaleX;

    public double ScaleX
    {
        get
        {
            Rect2 plot = PlotRect;
            double projectedSpan = _bounds.Width + _bounds.Height;
            double fit = Math.Min(
                plot.Size.X / projectedSpan,
                plot.Size.Y / (projectedSpan * 0.5d));
            return fit * ZoomMultipliers[_zoomIndex];
        }
    }

    public double ScaleY => ScaleX * 0.5d;

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
        double deltaX = worldX - _center.X;
        double deltaY = worldY - _center.Y;
        return plot.GetCenter() + new Vector2(
            (float)((deltaX - deltaY) * ScaleX),
            (float)((deltaX + deltaY) * ScaleY));
    }

    public CommercialWorldPosition CanvasToWorld(Vector2 canvasPoint)
    {
        Rect2 plot = PlotRect;
        Vector2 offset = canvasPoint - plot.GetCenter();
        Vector2 worldOffset = CanvasDeltaToWorld(offset);
        return new CommercialWorldPosition(
            _center.X + worldOffset.X,
            _center.Y + worldOffset.Y);
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
        _center = ClampCenter(_center - CanvasDeltaToWorld(canvasDelta));
    }

    public void Home()
    {
        _zoomIndex = 0;
        _center = ReferenceHomeCenter(_bounds);
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

        _center = ClampCenter(_center + CanvasDeltaToWorld(current - desired));
    }

    private Vector2 ClampCenter(Vector2 requested)
    {
        double zoom = ZoomMultipliers[_zoomIndex];
        double halfVisibleWidth = _bounds.Width / (zoom * 2d);
        double halfVisibleHeight = _bounds.Height / (zoom * 2d);
        return new Vector2(
            ClampAxis(requested.X, _bounds.MinX, _bounds.MaxX, halfVisibleWidth),
            ClampAxis(requested.Y, _bounds.MinY, _bounds.MaxY, halfVisibleHeight));
    }

    private Vector2 CanvasDeltaToWorld(Vector2 canvasDelta)
    {
        double projectedX = canvasDelta.X / ScaleX;
        double projectedY = canvasDelta.Y / ScaleY;
        return new Vector2(
            (float)((projectedX + projectedY) * 0.5d),
            (float)((projectedY - projectedX) * 0.5d));
    }

    private static float ClampAxis(float requested, int minimum, int maximum, double halfVisible)
    {
        if ((halfVisible * 2d) >= ((double)maximum - minimum))
        {
            return (float)(((double)minimum + maximum) / 2d);
        }
        return (float)Math.Clamp(requested, minimum + halfVisible, maximum - halfVisible);
    }

    private static Vector2 ReferenceHomeCenter(CommercialMapBounds bounds) => new(
        (float)(bounds.MinX + (bounds.Width * 0.421875d)),
        (float)(bounds.MinY + (bounds.Height * 0.55d)));

    private static Vector2 ValidViewport(Vector2 viewportSize) => new(
        Math.Max(1f, viewportSize.X),
        Math.Max(1f, viewportSize.Y));
}
