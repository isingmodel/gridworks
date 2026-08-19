using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

public sealed record ThermalProtectionDefinition(
    long ContinuousKw,
    long EmergencyKw,
    int EmergencyExposureLimitMinutes,
    int EmergencyExposureRecoveryPerMinute,
    int ProtectiveOutageMinutes);

public sealed record RealtimeThermalClassDefinition(
    ThermalAssetKind AssetKind,
    string ClassId,
    ThermalProtectionDefinition Protection);

public sealed record RealtimeWorldDefinition(
    string SchemaVersion,
    string WorldId,
    CommercialWorldDefinition Network,
    IReadOnlyList<RealtimeThermalClassDefinition> ThermalClasses)
{
    private IReadOnlyList<RealtimeThermalClassDefinition> _thermalClasses =
        Freeze(ThermalClasses);

    public IReadOnlyList<RealtimeThermalClassDefinition> ThermalClasses
    {
        get => _thermalClasses;
        init => _thermalClasses = Freeze(value);
    }

    public ThermalProtectionDefinition ProtectionFor(
        ThermalAssetKind assetKind,
        string classId) => ThermalClasses.Single(item =>
            item.AssetKind == assetKind &&
            string.Equals(item.ClassId, classId, StringComparison.Ordinal)).Protection;

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed class RealtimeWorldValidationException : Exception
{
    public RealtimeWorldValidationException(string message)
        : base(message)
    {
    }

    public RealtimeWorldValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
