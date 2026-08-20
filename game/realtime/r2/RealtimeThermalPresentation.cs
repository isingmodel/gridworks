using System;
using System.Globalization;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;

namespace Gridworks.Game.Realtime.R2;

internal sealed record RealtimeThermalProtectionCopy(
    string Summary,
    string Detail,
    long EmergencyMinutesRemaining,
    long OutageMinutesRemaining,
    int ProtectiveOutageMinutes);

/// <summary>
/// Converts authoritative V3 protection state into player-facing countdown copy.
/// This adapter never estimates protection curves: it reports the exact integer
/// allowance and cooldown owned by the loaded realtime world definition.
/// </summary>
internal static class RealtimeThermalPresentation
{
    internal static RealtimeThermalProtectionCopy For(
        RealtimeWorldDefinition world,
        RealtimeThermalSnapshot snapshot,
        RealtimeThermalAssetSnapshot asset)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(asset);

        ThermalProtectionDefinition protection = world.ProtectionFor(
            asset.AssetKind,
            asset.ClassId);
        long emergencyRemaining = Math.Max(
            0L,
            (long)protection.EmergencyExposureLimitMinutes -
            asset.EmergencyExposureMinutes);
        long outageRemaining = RemainingMinutes(
            snapshot.Minute,
            asset.ProtectiveOutageUntilMinute);

        string summary = asset.State switch
        {
            ThermalOperatingState.Continuous =>
                $"비상 운전 여유 {emergencyRemaining}분 · " +
                $"이후 {protection.ProtectiveOutageMinutes}분 보호정지",
            ThermalOperatingState.Emergency =>
                $"보호정지까지 {emergencyRemaining}분 · " +
                $"이후 {protection.ProtectiveOutageMinutes}분 보호정지",
            ThermalOperatingState.ProtectiveOutage =>
                $"복귀까지 {outageRemaining}분 · " +
                $"보호정지 기준 {protection.ProtectiveOutageMinutes}분",
            ThermalOperatingState.OverLimit =>
                $"비상 한계 초과 · 현재 경로 사용 불가 · " +
                $"보호정지 기준 {protection.ProtectiveOutageMinutes}분",
            _ => throw new ArgumentOutOfRangeException(nameof(asset)),
        };
        string detail = string.Format(
            CultureInfo.InvariantCulture,
            "사용 {0:N0} kW\n연속 한계 {1:N0} kW\n비상 한계 {2:N0} kW\n" +
            "비상 노출 {3}/{4}분\n{5}",
            asset.UsedKw,
            asset.ContinuousKw,
            asset.EmergencyKw,
            asset.EmergencyExposureMinutes,
            protection.EmergencyExposureLimitMinutes,
            summary);
        return new RealtimeThermalProtectionCopy(
            summary,
            detail,
            emergencyRemaining,
            outageRemaining,
            protection.ProtectiveOutageMinutes);
    }

    private static long RemainingMinutes(long currentMinute, long? untilMinute)
    {
        if (!untilMinute.HasValue || untilMinute.Value <= currentMinute)
        {
            return 0L;
        }
        return untilMinute.Value - currentMinute;
    }
}
