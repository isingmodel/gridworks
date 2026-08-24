using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;

namespace Gridworks.Game.Realtime.R2;

internal enum RealtimeNativeRouteKind
{
    StandaloneChapter,
    CumulativePrefix,
}

internal sealed record RealtimeNativeRoute(
    string LaunchArgument,
    string EndChapterId,
    int SelectedChapterCount,
    RealtimeNativeRouteKind Kind,
    string? FullFlowPassToken)
{
    internal bool UsesChapterStoryFlow =>
        Kind == RealtimeNativeRouteKind.CumulativePrefix;

    internal bool IsStandaloneChapter =>
        Kind == RealtimeNativeRouteKind.StandaloneChapter;
}

/// <summary>
/// The single launch and native-coverage authority for current R2 release content.
/// Authored chapters after NativeThroughChapterId remain unavailable until this
/// capability is explicitly advanced.
/// </summary>
internal static class RealtimeNativeRouteCatalog
{
    private const string ReleaseChapterArgumentPrefix = "--release-chapter=";
    private const string ReleaseThroughArgumentPrefix = "--release-through=";

    internal const string NativeThroughChapterId = "NORTH_BANK_PROMISE";

    internal static RealtimeNativeRoute FirstLight { get; } = Standalone(
        RealtimeCampaignOverlayLoader.FirstReleaseChapterId);

    internal static RealtimeNativeRoute TutorialThroughSecondSource { get; } = Prefix(
        "SECOND_SOURCE",
        "FULL_FLOW_E2E_PASS:TUTORIAL_THROUGH_SECOND_SOURCE");

    internal static RealtimeNativeRoute ThroughNativeCoverage { get; } = Prefix(
        NativeThroughChapterId,
        "FULL_FLOW_E2E_PASS:RELEASE_PREFIX_THROUGH_NORTH_BANK_PROMISE");

    private static readonly RealtimeNativeRoute[] SupportedRoutes = Validate(
    [
        FirstLight,
        TutorialThroughSecondSource,
        ThroughNativeCoverage,
    ]);

    internal static IReadOnlyList<RealtimeNativeRoute> All { get; } =
        Array.AsReadOnly(SupportedRoutes);

    internal static RealtimeNativeRoute RequireSupported(RealtimeNativeRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (!SupportedRoutes.Any(candidate => ReferenceEquals(candidate, route)))
        {
            throw new ArgumentException(
                "Native R2 resources require a canonical catalog route.",
                nameof(route));
        }
        return route;
    }

    internal static RealtimeNativeRoute? Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Length == 0)
        {
            return null;
        }
#if DEBUG
        if (arguments.Length == 1 &&
            arguments[0].StartsWith("--checkpoint=", StringComparison.Ordinal) &&
            RealtimeSliceCheckpointIds.IsKnown(arguments[0]["--checkpoint=".Length..]))
        {
            return null;
        }
#endif
        if (arguments.Length != 1)
        {
            throw new ArgumentException(
                "Exactly one supported release route user argument is required.");
        }

        RealtimeNativeRoute? route = SupportedRoutes.SingleOrDefault(item =>
            string.Equals(item.LaunchArgument, arguments[0], StringComparison.Ordinal));
        if (route is not null)
        {
            return route;
        }

        if (arguments[0].StartsWith(ReleaseChapterArgumentPrefix, StringComparison.Ordinal))
        {
            string chapterId = arguments[0][ReleaseChapterArgumentPrefix.Length..];
            throw new ArgumentException(
                $"Unknown release chapter '{chapterId}'. This gate exposes only " +
                $"{FirstLight.EndChapterId}.");
        }
        if (arguments[0].StartsWith(ReleaseThroughArgumentPrefix, StringComparison.Ordinal))
        {
            string chapterId = arguments[0][ReleaseThroughArgumentPrefix.Length..];
            string supported = string.Join(
                " or ",
                SupportedRoutes
                    .Where(item => item.Kind == RealtimeNativeRouteKind.CumulativePrefix)
                    .Select(item => item.EndChapterId));
            throw new ArgumentException(
                $"Unknown release prefix end '{chapterId}'. This gate exposes only " +
                $"{supported}.");
        }
        throw new ArgumentException("Unknown realtime release route user argument.");
    }

    private static RealtimeNativeRoute Standalone(string chapterId) => new(
        $"{ReleaseChapterArgumentPrefix}{chapterId}",
        chapterId,
        SelectedChapterCountThrough(chapterId),
        RealtimeNativeRouteKind.StandaloneChapter,
        null);

    private static RealtimeNativeRoute Prefix(string chapterId, string passToken) => new(
        $"{ReleaseThroughArgumentPrefix}{chapterId}",
        chapterId,
        SelectedChapterCountThrough(chapterId),
        RealtimeNativeRouteKind.CumulativePrefix,
        passToken);

    private static RealtimeNativeRoute[] Validate(RealtimeNativeRoute[] routes)
    {
        if (routes.Length == 0 ||
            routes.Select(route => route.LaunchArgument)
                .Distinct(StringComparer.Ordinal)
                .Count() != routes.Length)
        {
            throw new InvalidOperationException(
                "Native R2 launch arguments must be non-empty and unique.");
        }
        return routes;
    }

    private static int SelectedChapterCountThrough(string chapterId)
    {
        int chapterIndex = CommercialCampaignLoader.CanonicalChapterIds
            .ToList()
            .FindIndex(item => string.Equals(item, chapterId, StringComparison.Ordinal));
        int nativeIndex = CommercialCampaignLoader.CanonicalChapterIds
            .ToList()
            .FindIndex(item => string.Equals(
                item,
                NativeThroughChapterId,
                StringComparison.Ordinal));
        if (nativeIndex < 0)
        {
            throw new InvalidOperationException(
                $"Native R2 cap '{NativeThroughChapterId}' is not a canonical chapter.");
        }
        if (chapterIndex < 0 || chapterIndex > nativeIndex)
        {
            throw new InvalidOperationException(
                $"Native R2 route endpoint '{chapterId}' exceeds the explicit " +
                $"'{NativeThroughChapterId}' capability.");
        }
        return checked(chapterIndex + 1);
    }
}
