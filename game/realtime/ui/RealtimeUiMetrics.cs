using System;
using Godot;

namespace Gridworks.Game.Realtime.UI;

internal enum RealtimeResolutionTier
{
    BelowTarget,
    FullHd,
    UltraHd,
    Wide,
}

internal readonly record struct RealtimeLayoutProfile(
    Vector2I PhysicalSize,
    RealtimeResolutionTier Tier,
    float PhysicalRenderScale,
    float AccessibilityScale,
    int SafeMargin,
    int TopHudHeight,
    int EventRailHeight,
    int ContextDockWidth,
    int BuildShelfHeight,
    int MinimumHitTarget,
    int PrimaryHitTarget,
    int BodyFontSize);

internal readonly record struct RealtimeSurfaceLayout(
    Rect2 TopHud,
    Rect2 EventRail,
    Rect2 ContextDock,
    Rect2 BuildShelf,
    Rect2 ActionDock,
    Rect2 MapInteraction);

internal static class RealtimeUiMetrics
{
    private const float SurfaceGap = 12f;
    private const float BuildShelfWidth = 1040f;

    public static readonly Vector2I ReferenceResolution = new(1920, 1080);
    public static readonly Vector2I UltraHdResolution = new(3840, 2160);

    public const int MinimumSupportedWidth = 1920;
    public const int MinimumSupportedHeight = 1080;

    public static RealtimeLayoutProfile ForWindow(
        Vector2I physicalSize,
        int uiScalePercent = 100)
    {
        if (physicalSize.X <= 0 || physicalSize.Y <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalSize));
        }
        if (uiScalePercent is not (100 or 125 or 150 or 200))
        {
            throw new ArgumentOutOfRangeException(nameof(uiScalePercent));
        }

        float renderScale = MathF.Min(
            physicalSize.X / (float)ReferenceResolution.X,
            physicalSize.Y / (float)ReferenceResolution.Y);
        float accessibilityScale = uiScalePercent / 100f;
        bool wide = physicalSize.X / (float)physicalSize.Y > 16f / 9f + 0.05f;
        RealtimeResolutionTier tier = physicalSize.X < MinimumSupportedWidth ||
                                      physicalSize.Y < MinimumSupportedHeight
            ? RealtimeResolutionTier.BelowTarget
            : wide
                ? RealtimeResolutionTier.Wide
                : physicalSize.X >= UltraHdResolution.X &&
                  physicalSize.Y >= UltraHdResolution.Y
                    ? RealtimeResolutionTier.UltraHd
                    : RealtimeResolutionTier.FullHd;

        int minimumHitTarget = Scaled(44, accessibilityScale);
        // At 125% and above the status labels and simulation controls occupy
        // two rows. Budget from the scaled hit target itself so typography can
        // never force the live HUD beyond the rectangle handed to it.
        int topHudHeight = uiScalePercent < 125
            ? minimumHitTarget + 24
            : checked((minimumHitTarget * 2) + 24);
        // From 125% onward, preserve the inspector's readable first screen by
        // folding the four semantic event kinds into the rail's two visual
        // rows. The typed lane identity remains in every marker and in the
        // linear accessibility order; only the responsive spatial grouping is
        // reduced. Keeping four physical rows at 125% leaves less than 610px
        // for the inspector on Full HD and forces its content off-canvas.
        int defaultLaneCount = uiScalePercent >= 125 ? 2 : 4;
        return new RealtimeLayoutProfile(
            physicalSize,
            tier,
            Math.Max(1f, renderScale),
            accessibilityScale,
            Scaled(24, accessibilityScale),
            topHudHeight,
            EventRailHeight(accessibilityScale, minimumHitTarget, defaultLaneCount),
            Math.Clamp(Scaled(448, accessibilityScale), 400, 840),
            minimumHitTarget + Scaled(20, accessibilityScale),
            minimumHitTarget,
            Scaled(54, accessibilityScale),
            Scaled(16, accessibilityScale));
    }

    public static bool MeetsPrimaryTarget(Vector2 size, RealtimeLayoutProfile profile) =>
        size.X >= profile.MinimumHitTarget && size.Y >= profile.PrimaryHitTarget;

    public static RealtimeSurfaceLayout CalculateSurfaceLayout(
        Vector2 logicalSize,
        RealtimeLayoutProfile profile,
        bool contextVisible,
        bool buildShelfVisible,
        bool actionDockVisible = false)
    {
        if (logicalSize.X <= 0f || logicalSize.Y <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalSize));
        }
        if (buildShelfVisible && actionDockVisible)
        {
            throw new InvalidOperationException(
                "A responsive surface may expose either build tools or a primary action, not both.");
        }

        float margin = profile.SafeMargin;
        float gap = SurfaceGap * profile.AccessibilityScale;
        float usableWidth = Math.Max(1f, logicalSize.X - (margin * 2f));
        var topHud = new Rect2(
            margin,
            margin,
            usableWidth,
            profile.TopHudHeight);
        var eventRail = new Rect2(
            margin,
            topHud.End.Y + gap,
            usableWidth,
            profile.EventRailHeight);
        float workspaceTop = eventRail.End.Y + gap;
        float workspaceBottom = logicalSize.Y - margin;
        float workspaceHeight = Math.Max(1f, workspaceBottom - workspaceTop);
        var contextDock = new Rect2(
            logicalSize.X - margin - profile.ContextDockWidth,
            workspaceTop,
            profile.ContextDockWidth,
            workspaceHeight);
        float mapRight = contextVisible ? contextDock.Position.X - gap : logicalSize.X - margin;
        float mapWidth = Math.Max(1f, mapRight - margin);
        float shelfWidth = Math.Min(BuildShelfWidth * profile.AccessibilityScale, mapWidth);
        var buildShelf = new Rect2(
            margin + Math.Max(0f, (mapWidth - shelfWidth) / 2f),
            logicalSize.Y - margin - profile.BuildShelfHeight,
            shelfWidth,
            profile.BuildShelfHeight);
        float actionWidth = Math.Min(440f * profile.AccessibilityScale, mapWidth);
        float actionHeight = Math.Min(
            workspaceHeight,
            // The action dock owns two single-line labels, their separation,
            // panel margins, and the primary hit target. The measured 100%
            // combined minimum is 158 logical pixels; 104 keeps that exact
            // content inside the assigned rect without relying on clipping.
            profile.PrimaryHitTarget + (104f * profile.AccessibilityScale));
        var actionDock = new Rect2(
            mapRight - actionWidth,
            logicalSize.Y - margin - actionHeight,
            actionWidth,
            actionHeight);
        float commandOcclusion = buildShelfVisible
            ? profile.BuildShelfHeight + gap
            : actionDockVisible
                ? actionHeight + gap
                : 0f;
        var mapInteraction = new Rect2(
            margin,
            workspaceTop,
            mapWidth,
            Math.Max(1f, workspaceHeight - commandOcclusion));
        return new RealtimeSurfaceLayout(
            topHud,
            eventRail,
            contextDock,
            buildShelf,
            actionDock,
            mapInteraction);
    }

    private static int Scaled(int value, float scale) =>
        Math.Max(1, (int)MathF.Round(value * scale));

    public static int EventRailHeight(
        float accessibilityScale,
        int minimumHitTarget,
        int laneCount)
    {
        if (laneCount is not (2 or 4))
        {
            throw new ArgumentOutOfRangeException(nameof(laneCount));
        }
        int laneGap = Scaled(4, accessibilityScale);
        // Scene rail margins are deliberately physical-logical constants;
        // marker and gap sizes already carry the accessibility scale.
        const int verticalChrome = 14;
        return checked((minimumHitTarget * laneCount) +
                       (laneGap * (laneCount - 1)) + verticalChrome);
    }
}
