namespace Gridworks.Core.Release.V3;

public enum RealtimeFramePauseReason
{
    None,
    Manual,
    CatchUpCeiling,
}

/// <summary>
/// Immutable timing facts exposed to a host without introducing a renderer dependency.
/// FractionalMinuteUnits uses <see cref="RealtimeFrameAccumulator.UnitsPerMinute"/>
/// as its denominator; pending whole minutes are never silently discarded.
/// </summary>
public sealed record RealtimeFrameAccumulatorSnapshot(
    RealtimeFramePauseReason PauseReason,
    long CatchUpCeilingMinutes,
    long PendingWholeMinutes,
    int FractionalMinuteUnits,
    int FractionalMinuteUnitsPerMinute,
    long AppliedSimulationMinutes)
{
    public bool Paused => PauseReason != RealtimeFramePauseReason.None;

    /// <summary>
    /// Whole simulation minutes retained by a bounded catch-up. This is the termination
    /// predicate for a Resume/DrainPending loop; a fractional remainder cannot be drained
    /// until additional frames complete its minute.
    /// </summary>
    public bool HasCatchUpDebt => PendingWholeMinutes > 0;

    /// <summary>
    /// Any exact simulation time retained by the accumulator, including a fractional
    /// remainder. Do not use this as a DrainPending loop predicate.
    /// </summary>
    public bool HasPendingTime =>
        HasCatchUpDebt || FractionalMinuteUnits > 0;
}

/// <summary>
/// One atomic host-frame result. Campaign is present only when AppliedMinutes is
/// positive; a null Campaign with AppliedMinutes equal to zero explicitly means that
/// no campaign snapshot was requested or campaign advance was performed. AccruedWholeMinutes
/// can exceed AppliedMinutes when bounded catch-up pauses the host; the difference
/// remains in Accumulator.PendingWholeMinutes.
/// </summary>
public sealed record RealtimeFrameAdvanceResult(
    RealtimeAdvanceResult? Campaign,
    RealtimeFrameAccumulatorSnapshot Accumulator,
    long AccruedWholeMinutes,
    long AppliedMinutes,
    bool CatchUpCeilingReached);

/// <summary>
/// Stateful, integer-only wall-clock adapter for the realtime campaign.
///
/// The least common multiple of every supported refresh rate is 720. A frame can
/// therefore be represented exactly as an integer number of 1/720 simulation-minute
/// units at every supported speed. Irregular frame chunks, per-frame calls, refresh
/// rate changes, and speed changes retain the same rational remainder.
/// </summary>
public sealed class RealtimeFrameAccumulator
{
    public const int UnitsPerMinute = 720;

    private static readonly int[] SupportedFramesPerSecond = [30, 60, 120, 144];
    private static readonly int[] SupportedSpeedMultipliers = [1, 2, 4];

    private readonly long _catchUpCeilingMinutes;
    private long _pendingWholeMinutes;
    private int _fractionalMinuteUnits;
    private long _appliedSimulationMinutes;
    private RealtimeFramePauseReason _pauseReason;
    private RealtimeCampaignRun? _boundRun;

    public RealtimeFrameAccumulator(long catchUpCeilingMinutes)
    {
        if (catchUpCeilingMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(catchUpCeilingMinutes));
        }
        _catchUpCeilingMinutes = catchUpCeilingMinutes;
    }

    public RealtimeFrameAccumulatorSnapshot GetSnapshot() => new(
        _pauseReason,
        _catchUpCeilingMinutes,
        _pendingWholeMinutes,
        _fractionalMinuteUnits,
        UnitsPerMinute,
        _appliedSimulationMinutes);

    /// <summary>
    /// Pauses simulation time immediately. A catch-up ceiling pause with retained debt
    /// has priority over a manual pause so its safety stop reason cannot be hidden.
    /// Frames observed while paused are intentional wall-clock time outside the
    /// simulation and are not accrued.
    /// </summary>
    public void Pause()
    {
        if (_pauseReason == RealtimeFramePauseReason.CatchUpCeiling &&
            _pendingWholeMinutes > 0)
        {
            return;
        }
        _pauseReason = RealtimeFramePauseReason.Manual;
    }

    /// <summary>
    /// Explicitly acknowledges and clears either a manual pause or a catch-up safety
    /// pause. Resume never clears retained debt: the next AdvanceFrames or DrainPending
    /// call applies one bounded batch and re-enters CatchUpCeiling when debt remains.
    /// </summary>
    public void Resume() => _pauseReason = RealtimeFramePauseReason.None;

    /// <summary>
    /// Accrues a possibly irregular frame chunk and advances by at most the configured
    /// catch-up ceiling. If more whole minutes are ready, the applied prefix is bounded,
    /// the remainder is retained, and the accumulator enters CatchUpCeiling pause. A
    /// paused call or a call that has not accrued a whole minute returns Campaign null
    /// without reading or advancing the campaign run.
    /// </summary>
    public RealtimeFrameAdvanceResult AdvanceFrames(
        RealtimeCampaignRun run,
        long frameCount,
        int framesPerSecond,
        int speedMultiplier)
    {
        ArgumentNullException.ThrowIfNull(run);
        ValidateFrameInput(frameCount, framesPerSecond, speedMultiplier);
        EnsureBoundRun(run);

        if (_pauseReason != RealtimeFramePauseReason.None)
        {
            return NoCampaignAdvance(0);
        }

        int unitsPerFrame = checked(
            UnitsPerMinute / framesPerSecond * speedMultiplier);
        Int128 accruedUnits = checked(
            (Int128)frameCount * unitsPerFrame + _fractionalMinuteUnits);
        Int128 newlyAccruedWhole = accruedUnits / UnitsPerMinute;
        int nextFractionalUnits = checked((int)(accruedUnits % UnitsPerMinute));
        if (newlyAccruedWhole > long.MaxValue)
        {
            throw new OverflowException("The frame interval exceeds the realtime clock range.");
        }

        long accruedWholeMinutes = (long)newlyAccruedWhole;
        long availableWholeMinutes = checked(
            _pendingWholeMinutes + accruedWholeMinutes);
        long appliedMinutes = Math.Min(
            availableWholeMinutes,
            _catchUpCeilingMinutes);
        bool ceilingReached = availableWholeMinutes > _catchUpCeilingMinutes;

        if (appliedMinutes == 0)
        {
            // No whole campaign minute exists yet. Commit only the exact rational
            // remainder; do not construct a potentially expensive or immediately
            // stale campaign snapshot.
            _fractionalMinuteUnits = nextFractionalUnits;
            return NoCampaignAdvance(accruedWholeMinutes);
        }

        long currentMinute = run.GetSnapshot().Minute;
        long targetMinute = checked(currentMinute + appliedMinutes);
        RealtimeAdvanceResult campaign = run.AdvanceTo(targetMinute);

        // Commit timing state only after the campaign accepts the target minute.
        _pendingWholeMinutes = availableWholeMinutes - appliedMinutes;
        _fractionalMinuteUnits = nextFractionalUnits;
        _appliedSimulationMinutes = checked(
            _appliedSimulationMinutes + appliedMinutes);
        if (ceilingReached)
        {
            _pauseReason = RealtimeFramePauseReason.CatchUpCeiling;
        }

        return new RealtimeFrameAdvanceResult(
            campaign,
            GetSnapshot(),
            accruedWholeMinutes,
            appliedMinutes,
            ceilingReached);
    }

    /// <summary>
    /// Drains retained whole-minute debt after an explicit Resume without adding wall
    /// time. It uses the same bounded path and can pause again if another batch remains.
    /// </summary>
    public RealtimeFrameAdvanceResult DrainPending(RealtimeCampaignRun run) =>
        AdvanceFrames(run, 0, 30, 1);

    private RealtimeFrameAdvanceResult NoCampaignAdvance(long accruedWholeMinutes) =>
        new(
            null,
            GetSnapshot(),
            accruedWholeMinutes,
            0,
            false);

    private void EnsureBoundRun(RealtimeCampaignRun run)
    {
        if (_boundRun is null)
        {
            _boundRun = run;
            return;
        }
        if (!ReferenceEquals(_boundRun, run))
        {
            throw new InvalidOperationException(
                "A realtime frame accumulator cannot carry time debt across campaign runs.");
        }
    }

    internal static void ValidateFrameInput(
        long frameCount,
        int framesPerSecond,
        int speedMultiplier)
    {
        if (frameCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        }
        if (!SupportedFramesPerSecond.Contains(framesPerSecond))
        {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        }
        if (!SupportedSpeedMultipliers.Contains(speedMultiplier))
        {
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
        }
        if (UnitsPerMinute % framesPerSecond != 0)
        {
            throw new InvalidOperationException(
                "The supported frame rate must divide the clock resolution exactly.");
        }
    }
}

/// <summary>
/// Compatibility facade for callers that already own an exact whole-minute frame
/// interval. Irregular frame delivery should use <see cref="RealtimeFrameAccumulator"/>.
/// </summary>
public static class RealtimeFrameAdapter
{
    public static RealtimeAdvanceResult AdvanceExactFrames(
        RealtimeCampaignRun run,
        long frameCount,
        int framesPerSecond,
        int speedMultiplier)
    {
        ArgumentNullException.ThrowIfNull(run);
        RealtimeFrameAccumulator.ValidateFrameInput(
            frameCount,
            framesPerSecond,
            speedMultiplier);

        int unitsPerFrame = checked(
            RealtimeFrameAccumulator.UnitsPerMinute / framesPerSecond *
            speedMultiplier);
        Int128 scaledUnits = checked((Int128)frameCount * unitsPerFrame);
        if (scaledUnits % RealtimeFrameAccumulator.UnitsPerMinute != 0)
        {
            throw new ArgumentException(
                "The exact frame interval must resolve to a whole simulation minute.",
                nameof(frameCount));
        }
        Int128 delta = scaledUnits / RealtimeFrameAccumulator.UnitsPerMinute;
        if (delta > long.MaxValue)
        {
            throw new OverflowException("The frame interval exceeds the realtime clock range.");
        }
        return run.AdvanceTo(checked(run.GetSnapshot().Minute + (long)delta));
    }
}
