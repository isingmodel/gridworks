namespace Gridworks.Core.Release;

internal static class ReleaseGridMath
{
    public static long DistanceSquared(ReleasePoint from, ReleasePoint to)
    {
        long dx = (long)from.X - to.X;
        long dy = (long)from.Y - to.Y;
        return checked((dx * dx) + (dy * dy));
    }

    public static long EdgeLengthMilliCells(ReleasePoint from, ReleasePoint to)
    {
        long scaledSquared = checked(DistanceSquared(from, to) * 1_000_000L);
        long root = (long)Math.Sqrt(scaledSquared);
        while (root * root < scaledSquared)
        {
            root++;
        }
        while (root > 0 && (root - 1) * (root - 1) >= scaledSquared)
        {
            root--;
        }
        return root;
    }

    public static bool PointInPolygon(
        ReleasePoint point,
        IReadOnlyList<ReleasePoint> polygon)
    {
        bool inside = false;
        for (int index = 0, previous = polygon.Count - 1; index < polygon.Count; previous = index++)
        {
            ReleasePoint a = polygon[previous];
            ReleasePoint b = polygon[index];
            if (Orientation(a, b, point) == 0 && OnSegment(a, point, b))
            {
                return true;
            }
            if ((a.Y > point.Y) != (b.Y > point.Y))
            {
                double crossingX = a.X + ((double)(b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y));
                if (point.X < crossingX)
                {
                    inside = !inside;
                }
            }
        }
        return inside;
    }

    private static long Orientation(ReleasePoint a, ReleasePoint b, ReleasePoint c) =>
        checked(((long)b.X - a.X) * ((long)c.Y - a.Y) -
                ((long)b.Y - a.Y) * ((long)c.X - a.X));

    private static bool OnSegment(ReleasePoint a, ReleasePoint point, ReleasePoint b) =>
        point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X) &&
        point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y);
}
