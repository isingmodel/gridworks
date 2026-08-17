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
}
