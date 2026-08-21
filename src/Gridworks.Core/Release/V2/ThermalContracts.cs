namespace Gridworks.Core.Release.V2;

public enum ThermalOperatingState
{
    Continuous,
    Emergency,
    ProtectiveOutage,
    OverLimit,
}

public enum ThermalObligationKind
{
    SafetyDuty,
    CityPromise,
    OperatingRecord,
}

public enum ThermalIntervalPolicy
{
    ContinuousOnly,
    SafetyEmergencyAllowed,
}

public enum ThermalSupplyFailure
{
    None,
    Deferred,
    NoPath,
    UnavailableAsset,
    SourceCapacity,
    ContinuousPermission,
    EmergencyLimit,
    FutureSafetyDuty,
}

public sealed record ThermalDemandDefinition(
    string DemandId,
    string DisplayName,
    string NodeId,
    long DemandKw,
    ThermalObligationKind ObligationKind,
    bool Included,
    bool EmergencyUseApproved,
    bool NamedEmergencyDuty,
    bool RequireSubstationPath = false);

public sealed record ThermalLimitOverride(
    string AssetId,
    long ContinuousLimitKw,
    long EmergencyLimitKw);

public sealed record ThermalIntervalDefinition(
    string IntervalId,
    string DisplayName,
    ThermalIntervalPolicy Policy,
    IReadOnlyList<ThermalDemandDefinition> Demands,
    IReadOnlyList<string> UnavailableAssetIds,
    IReadOnlyList<ThermalLimitOverride> LimitOverrides)
{
    private IReadOnlyList<ThermalDemandDefinition> _demands =
        Array.AsReadOnly(Demands.ToArray());
    private IReadOnlyList<string> _unavailableAssetIds =
        Array.AsReadOnly(UnavailableAssetIds.ToArray());
    private IReadOnlyList<ThermalLimitOverride> _limitOverrides =
        Array.AsReadOnly(LimitOverrides.ToArray());

    public IReadOnlyList<ThermalDemandDefinition> Demands
    {
        get => _demands;
        init => _demands = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> UnavailableAssetIds
    {
        get => _unavailableAssetIds;
        init => _unavailableAssetIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ThermalLimitOverride> LimitOverrides
    {
        get => _limitOverrides;
        init => _limitOverrides = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record ThermalAssetMemory(
    string AssetId,
    bool ProtectiveOutage);

public sealed record ThermalSequenceRequest(
    IReadOnlyList<ThermalIntervalDefinition> Intervals,
    IReadOnlyList<ThermalAssetMemory> InitialAssetMemory)
{
    private IReadOnlyList<ThermalIntervalDefinition> _intervals =
        Array.AsReadOnly(Intervals.ToArray());
    private IReadOnlyList<ThermalAssetMemory> _initialAssetMemory =
        Array.AsReadOnly(InitialAssetMemory.ToArray());

    public IReadOnlyList<ThermalIntervalDefinition> Intervals
    {
        get => _intervals;
        init => _intervals = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ThermalAssetMemory> InitialAssetMemory
    {
        get => _initialAssetMemory;
        init => _initialAssetMemory = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record ThermalDemandResult(
    string DemandId,
    bool Supplied,
    bool Deferred,
    long DemandKw,
    string? SourceNodeId,
    IReadOnlyList<string> PathNodeIds,
    IReadOnlyList<string> PathEdgeIds,
    IReadOnlyList<string> EmergencyAssetIds,
    long? MinimumRemainingLimitKw,
    ThermalSupplyFailure Failure,
    string? FirstBottleneckAssetId)
{
    private IReadOnlyList<string> _pathNodeIds = Array.AsReadOnly(PathNodeIds.ToArray());
    private IReadOnlyList<string> _pathEdgeIds = Array.AsReadOnly(PathEdgeIds.ToArray());
    private IReadOnlyList<string> _emergencyAssetIds =
        Array.AsReadOnly(EmergencyAssetIds.ToArray());

    public IReadOnlyList<string> PathNodeIds
    {
        get => _pathNodeIds;
        init => _pathNodeIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> PathEdgeIds
    {
        get => _pathEdgeIds;
        init => _pathEdgeIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> EmergencyAssetIds
    {
        get => _emergencyAssetIds;
        init => _emergencyAssetIds = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record ThermalAssetResult(
    string AssetId,
    long UseKw,
    long ContinuousLimitKw,
    long EmergencyLimitKw,
    ThermalOperatingState CurrentState,
    ThermalOperatingState NextState,
    bool AuthoredUnavailable);

public sealed record ThermalIntervalResult(
    string IntervalId,
    IReadOnlyList<ThermalDemandResult> Demands,
    IReadOnlyList<ThermalAssetResult> Assets,
    IReadOnlyList<ThermalAssetMemory> NextAssetMemory)
{
    private IReadOnlyList<ThermalDemandResult> _demands = Array.AsReadOnly(Demands.ToArray());
    private IReadOnlyList<ThermalAssetResult> _assets = Array.AsReadOnly(Assets.ToArray());
    private IReadOnlyList<ThermalAssetMemory> _nextAssetMemory =
        Array.AsReadOnly(NextAssetMemory.ToArray());

    public IReadOnlyList<ThermalDemandResult> Demands
    {
        get => _demands;
        init => _demands = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ThermalAssetResult> Assets
    {
        get => _assets;
        init => _assets = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ThermalAssetMemory> NextAssetMemory
    {
        get => _nextAssetMemory;
        init => _nextAssetMemory = Array.AsReadOnly(value.ToArray());
    }
}

public sealed record ThermalSequenceResult(
    IReadOnlyList<ThermalIntervalResult> Intervals,
    IReadOnlyList<ThermalAssetMemory> FinalAssetMemory)
{
    private IReadOnlyList<ThermalIntervalResult> _intervals =
        Array.AsReadOnly(Intervals.ToArray());
    private IReadOnlyList<ThermalAssetMemory> _finalAssetMemory =
        Array.AsReadOnly(FinalAssetMemory.ToArray());

    public IReadOnlyList<ThermalIntervalResult> Intervals
    {
        get => _intervals;
        init => _intervals = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ThermalAssetMemory> FinalAssetMemory
    {
        get => _finalAssetMemory;
        init => _finalAssetMemory = Array.AsReadOnly(value.ToArray());
    }
}

public sealed class ThermalEvaluationException : Exception
{
    public ThermalEvaluationException(string message)
        : base(message)
    {
    }
}
