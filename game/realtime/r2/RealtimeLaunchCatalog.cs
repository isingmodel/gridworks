using System;

namespace Gridworks.Game.Realtime.R2;

internal enum RealtimeLaunchKind
{
    ProductTitle,
    TechnicalFixture,
    NativeRelease,
}

/// <summary>
/// One validated choice for starting the current R2 scene. Product boot,
/// development fixtures, and native release routes are deliberately distinct.
/// </summary>
internal sealed record RealtimeLaunchSelection
{
    private RealtimeLaunchSelection(
        RealtimeLaunchKind kind,
        RealtimeNativeRoute? nativeRoute)
    {
        if ((kind == RealtimeLaunchKind.NativeRelease) != (nativeRoute is not null))
        {
            throw new ArgumentException(
                "Only a native release launch may carry a native route.");
        }
        Kind = kind;
        NativeRoute = nativeRoute;
    }

    internal RealtimeLaunchKind Kind { get; }

    internal RealtimeNativeRoute? NativeRoute { get; }

    internal static RealtimeLaunchSelection ProductTitle { get; } =
        new(RealtimeLaunchKind.ProductTitle, null);

    internal static RealtimeLaunchSelection TechnicalFixture { get; } =
        new(RealtimeLaunchKind.TechnicalFixture, null);

    internal static RealtimeLaunchSelection Native(RealtimeNativeRoute route) =>
        new(
            RealtimeLaunchKind.NativeRelease,
            RealtimeNativeRouteCatalog.RequireSupported(route));
}

/// <summary>
/// The single authority that separates product boot from explicit development
/// and native-content launches.
/// </summary>
internal static class RealtimeLaunchCatalog
{
    internal const string TechnicalFixtureArgument = "--technical-fixture";

    internal static RealtimeLaunchSelection Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Length == 0)
        {
            return RealtimeLaunchSelection.ProductTitle;
        }
#if DEBUG
        if (arguments.Length == 1 && string.Equals(
                arguments[0],
                TechnicalFixtureArgument,
                StringComparison.Ordinal))
        {
            return RealtimeLaunchSelection.TechnicalFixture;
        }
        if (arguments.Length == 1 &&
            arguments[0].StartsWith("--checkpoint=", StringComparison.Ordinal) &&
            RealtimeSliceCheckpointIds.IsKnown(arguments[0]["--checkpoint=".Length..]))
        {
            return RealtimeLaunchSelection.TechnicalFixture;
        }
#endif
        return RealtimeLaunchSelection.Native(
            RealtimeNativeRouteCatalog.Parse(arguments));
    }
}
