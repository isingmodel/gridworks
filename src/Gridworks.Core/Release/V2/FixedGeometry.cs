using System.Numerics;

namespace Gridworks.Core.Release.V2;

public static class FixedGeometry
{
    public static long CeilDistance(MapPoint first, MapPoint second)
    {
        long dx = (long)second.XUnit - first.XUnit;
        long dy = (long)second.YUnit - first.YUnit;
        Int128 squared = (Int128)dx * dx + (Int128)dy * dy;
        return CeilSquareRoot(squared);
    }

    public static long CeilSquareRoot(Int128 value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (value <= 1)
        {
            return (long)value;
        }

        long low = 0;
        long high = 1;
        while ((Int128)high * high < value)
        {
            high = checked(high * 2);
        }

        while (low + 1 < high)
        {
            long middle = low + ((high - low) / 2);
            if ((Int128)middle * middle >= value)
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        return high;
    }

    public static bool PointWithinBounds(MapPoint point, MapBounds bounds) =>
        point.XUnit >= bounds.MinXUnit &&
        point.XUnit <= bounds.MaxXUnit &&
        point.YUnit >= bounds.MinYUnit &&
        point.YUnit <= bounds.MaxYUnit;

    public static bool CircleWithinBounds(MapPoint center, int radiusUnit, MapBounds bounds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radiusUnit);
        return (long)center.XUnit - radiusUnit >= bounds.MinXUnit &&
            (long)center.XUnit + radiusUnit <= bounds.MaxXUnit &&
            (long)center.YUnit - radiusUnit >= bounds.MinYUnit &&
            (long)center.YUnit + radiusUnit <= bounds.MaxYUnit;
    }

    public static bool ContainsPointInclusive(
        MapPoint point,
        IReadOnlyList<MapPoint> polygon)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        if (polygon.Count < 3)
        {
            return false;
        }

        int windingNumber = 0;
        for (int index = 0; index < polygon.Count; index++)
        {
            MapPoint first = polygon[index];
            MapPoint second = polygon[(index + 1) % polygon.Count];
            if (PointOnSegment(point, first, second))
            {
                return true;
            }

            Int128 cross = Cross(first, second, point);
            if (first.YUnit <= point.YUnit)
            {
                if (second.YUnit > point.YUnit && cross > 0)
                {
                    windingNumber++;
                }
            }
            else if (second.YUnit <= point.YUnit && cross < 0)
            {
                windingNumber--;
            }
        }

        return windingNumber != 0;
    }

    public static bool CirclesTouchOrOverlap(
        MapPoint firstCenter,
        int firstRadiusUnit,
        MapPoint secondCenter,
        int secondRadiusUnit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firstRadiusUnit);
        ArgumentOutOfRangeException.ThrowIfNegative(secondRadiusUnit);
        long dx = (long)secondCenter.XUnit - firstCenter.XUnit;
        long dy = (long)secondCenter.YUnit - firstCenter.YUnit;
        long combinedRadius = checked((long)firstRadiusUnit + secondRadiusUnit);
        return (Int128)dx * dx + (Int128)dy * dy <=
            (Int128)combinedRadius * combinedRadius;
    }

    public static bool SegmentTouchesCircle(
        MapPoint segmentStart,
        MapPoint segmentEnd,
        MapPoint center,
        int radiusUnit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radiusUnit);

        long dx = (long)segmentEnd.XUnit - segmentStart.XUnit;
        long dy = (long)segmentEnd.YUnit - segmentStart.YUnit;
        long cx = (long)center.XUnit - segmentStart.XUnit;
        long cy = (long)center.YUnit - segmentStart.YUnit;
        Int128 lengthSquared = (Int128)dx * dx + (Int128)dy * dy;
        Int128 radiusSquared = (Int128)radiusUnit * radiusUnit;

        if (lengthSquared == 0)
        {
            return (Int128)cx * cx + (Int128)cy * cy <= radiusSquared;
        }

        Int128 projection = (Int128)cx * dx + (Int128)cy * dy;
        if (projection <= 0)
        {
            return (Int128)cx * cx + (Int128)cy * cy <= radiusSquared;
        }

        if (projection >= lengthSquared)
        {
            long ex = (long)center.XUnit - segmentEnd.XUnit;
            long ey = (long)center.YUnit - segmentEnd.YUnit;
            return (Int128)ex * ex + (Int128)ey * ey <= radiusSquared;
        }

        Int128 cross = (Int128)dx * cy - (Int128)dy * cx;
        BigInteger absoluteCross = BigInteger.CreateChecked(Abs(cross));
        return absoluteCross * absoluteCross <=
            BigInteger.CreateChecked(radiusSquared) * BigInteger.CreateChecked(lengthSquared);
    }

    public static bool CircleIntersectsPolygon(
        MapPoint center,
        int radiusUnit,
        IReadOnlyList<MapPoint> polygon)
    {
        if (ContainsPointInclusive(center, polygon))
        {
            return true;
        }

        for (int index = 0; index < polygon.Count; index++)
        {
            if (SegmentTouchesCircle(
                    polygon[index],
                    polygon[(index + 1) % polygon.Count],
                    center,
                    radiusUnit))
            {
                return true;
            }
        }

        return false;
    }

    public static bool SegmentsIntersectInclusive(
        MapPoint firstStart,
        MapPoint firstEnd,
        MapPoint secondStart,
        MapPoint secondEnd)
    {
        Int128 firstOrientation = Cross(firstStart, firstEnd, secondStart);
        Int128 secondOrientation = Cross(firstStart, firstEnd, secondEnd);
        Int128 thirdOrientation = Cross(secondStart, secondEnd, firstStart);
        Int128 fourthOrientation = Cross(secondStart, secondEnd, firstEnd);

        if (firstOrientation == 0 && PointOnSegment(secondStart, firstStart, firstEnd))
        {
            return true;
        }

        if (secondOrientation == 0 && PointOnSegment(secondEnd, firstStart, firstEnd))
        {
            return true;
        }

        if (thirdOrientation == 0 && PointOnSegment(firstStart, secondStart, secondEnd))
        {
            return true;
        }

        if (fourthOrientation == 0 && PointOnSegment(firstEnd, secondStart, secondEnd))
        {
            return true;
        }

        return HasOppositeSigns(firstOrientation, secondOrientation) &&
            HasOppositeSigns(thirdOrientation, fourthOrientation);
    }

    public static bool CollinearPositiveOverlap(
        MapPoint firstStart,
        MapPoint firstEnd,
        MapPoint secondStart,
        MapPoint secondEnd)
    {
        if (Cross(firstStart, firstEnd, secondStart) != 0 ||
            Cross(firstStart, firstEnd, secondEnd) != 0)
        {
            return false;
        }

        long firstDx = (long)firstEnd.XUnit - firstStart.XUnit;
        long firstDy = (long)firstEnd.YUnit - firstStart.YUnit;
        bool useX = Abs(firstDx) >= Abs(firstDy);
        long firstMinimum = Math.Min(
            useX ? firstStart.XUnit : firstStart.YUnit,
            useX ? firstEnd.XUnit : firstEnd.YUnit);
        long firstMaximum = Math.Max(
            useX ? firstStart.XUnit : firstStart.YUnit,
            useX ? firstEnd.XUnit : firstEnd.YUnit);
        long secondMinimum = Math.Min(
            useX ? secondStart.XUnit : secondStart.YUnit,
            useX ? secondEnd.XUnit : secondEnd.YUnit);
        long secondMaximum = Math.Max(
            useX ? secondStart.XUnit : secondStart.YUnit,
            useX ? secondEnd.XUnit : secondEnd.YUnit);
        return Math.Min(firstMaximum, secondMaximum) >
            Math.Max(firstMinimum, secondMinimum);
    }

    public static bool SegmentIntersectsPolygon(
        MapPoint start,
        MapPoint end,
        IReadOnlyList<MapPoint> polygon)
    {
        if (ContainsPointInclusive(start, polygon) || ContainsPointInclusive(end, polygon))
        {
            return true;
        }

        for (int index = 0; index < polygon.Count; index++)
        {
            if (SegmentsIntersectInclusive(
                    start,
                    end,
                    polygon[index],
                    polygon[(index + 1) % polygon.Count]))
            {
                return true;
            }
        }

        return false;
    }

    public static Int128 SignedAreaTwice(IReadOnlyList<MapPoint> polygon)
    {
        Int128 area = 0;
        for (int index = 0; index < polygon.Count; index++)
        {
            MapPoint first = polygon[index];
            MapPoint second = polygon[(index + 1) % polygon.Count];
            area += (Int128)first.XUnit * second.YUnit -
                (Int128)second.XUnit * first.YUnit;
        }

        return area;
    }

    private static bool PointOnSegment(MapPoint point, MapPoint start, MapPoint end) =>
        Cross(start, end, point) == 0 &&
        point.XUnit >= Math.Min(start.XUnit, end.XUnit) &&
        point.XUnit <= Math.Max(start.XUnit, end.XUnit) &&
        point.YUnit >= Math.Min(start.YUnit, end.YUnit) &&
        point.YUnit <= Math.Max(start.YUnit, end.YUnit);

    private static Int128 Cross(MapPoint start, MapPoint end, MapPoint point) =>
        (Int128)((long)end.XUnit - start.XUnit) * ((long)point.YUnit - start.YUnit) -
        (Int128)((long)end.YUnit - start.YUnit) * ((long)point.XUnit - start.XUnit);

    private static bool HasOppositeSigns(Int128 first, Int128 second) =>
        (first < 0 && second > 0) || (first > 0 && second < 0);

    private static Int128 Abs(Int128 value) => value < 0 ? -value : value;

    private static long Abs(long value) => value < 0 ? -value : value;
}
