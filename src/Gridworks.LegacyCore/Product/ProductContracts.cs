namespace Gridworks.Core.Product;

public sealed record ProductPoint(int X, int Y);

public sealed record ProductUnits(
    string Position,
    string Power,
    string Energy,
    string Time,
    string Cash,
    string Rate);

public sealed record ProductMapBounds(int MinX, int MaxX, int MinY, int MaxY);

public sealed record ProductEconomy(long InitialCash, long SaleRateCashUnitPerGWh);

public sealed record ProductExistingSource(
    string AssetId,
    string TerminalId,
    ProductPoint Position,
    long CapacityKw);

public sealed record ProductTown(
    string Id,
    ProductPoint Position,
    long DemandKw,
    int Priority = 0);

public sealed record ProductSubstationProjectDefinition(
    string ProjectId,
    string AssetId,
    string TerminalId,
    long CapacityKw,
    int ServiceRadiusGridUnit,
    long CostCashUnit,
    int BuildMinutes);

public sealed record ProductLineProjectDefinition(
    string ProjectId,
    string FromTerminalId,
    string ToTerminalId,
    long RatingKw,
    int MaxSpanGridUnit,
    long SupportCostCashUnit,
    long SpanCostCashUnit,
    int SupportBuildMinutes,
    int SpanBuildMinutes);

public sealed record ProductHospital(
    string Id,
    ProductPoint Position,
    long DemandKw,
    int Priority,
    string PrimaryTerminalId,
    string BackupTerminalId,
    int UpsMinutes,
    int DieselMinutes);

public sealed record ProductHospitalLineProjectDefinition(
    string ProjectId,
    string FromTerminalId,
    string ToTerminalId,
    int RoutePriority,
    long RatingKw,
    int MaxSpanGridUnit,
    long SupportCostCashUnit,
    long SpanCostCashUnit,
    int SupportBuildMinutes,
    int SpanBuildMinutes);

public sealed record ProductRiskRect(int MinX, int MaxX, int MinY, int MaxY);

public sealed record ProductSpatialIncident(
    string Id,
    ProductRiskRect RiskRect,
    int LeadMinutes,
    int DurationMinutes);

public sealed record ProductHospitalEconomy(
    long VariableGenerationCostCashUnitPerGWh,
    long UnservedCompensationCashUnitPerGWh,
    long LostSalesCashUnitPerGWh);

public sealed record ProductFactory(
    string Id,
    string TerminalId,
    ProductPoint Position,
    long DemandKw,
    int Priority,
    long FeederRatingKw);

public sealed record ProductGasPlantProjectDefinition(
    string ProjectId,
    string AssetId,
    string TerminalId,
    long CapacityKw,
    long BaseCostCashUnit,
    int BuildMinutes,
    long VariableGenerationCostCashUnitPerGWh);

public sealed record ProductGasPlantSite(
    string SiteId,
    ProductPoint Position,
    long SiteCostCashUnit);

public sealed record ProductHeatwaveDefinition(
    string Id,
    int LeadMinutes,
    int DurationMinutes,
    long TownDemandKw,
    string AgedFactoryFeederId,
    long AgedFactoryFeederHeatwaveRatingKw);

public sealed record ProductPreventiveMaintenanceDefinition(
    string ProjectId,
    string TargetAssetId,
    long CostCashUnit,
    int BuildMinutes);

public sealed record ProductFixture(
    string SchemaVersion,
    string FixtureId,
    string DisplayName,
    ProductUnits Units,
    ProductMapBounds MapBounds,
    IReadOnlyList<ProductPoint> BlockedCells,
    int InitialMinute,
    int SettlementMinutes,
    ProductEconomy Economy,
    ProductExistingSource ExistingSource,
    ProductTown Town,
    ProductSubstationProjectDefinition SubstationProject,
    ProductLineProjectDefinition LineProject,
    ProductHospital? Hospital = null,
    IReadOnlyList<ProductHospitalLineProjectDefinition>? HospitalLineProjects = null,
    ProductSpatialIncident? SpatialIncident = null,
    ProductHospitalEconomy? HospitalEconomy = null,
    int? FactorySettlementMinutes = null,
    ProductFactory? Factory = null,
    ProductGasPlantProjectDefinition? GasPlantProject = null,
    IReadOnlyList<ProductGasPlantSite>? GasPlantSites = null,
    ProductLineProjectDefinition? PlantConnectionLineProject = null,
    ProductHeatwaveDefinition? Heatwave = null,
    ProductPreventiveMaintenanceDefinition? PreventiveMaintenance = null)
{
    public bool HasHospitalStage => Hospital is not null;
    public bool HasFactoryStage => Factory is not null;
    public bool HasHeatwaveStage => Heatwave is not null;
}

public enum ProductProjectState
{
    NotOrdered,
    Building,
    Commissioned,
}

public enum ProductPhase
{
    SubstationPlanning,
    SubstationBuilding,
    LinePlanning,
    LineBuilding,
    SettlementReady,
    Complete,
    PrimaryPlanning,
    PrimaryBuilding,
    BackupPlanning,
    BackupBuilding,
    IncidentReady,
    IncidentActive,
    PlantPlanning,
    PlantBuilding,
    PlantConnectionPlanning,
    PlantConnectionBuilding,
    FactorySettlementReady,
    MaintenanceDecision,
    MaintenanceBuilding,
    HeatwaveReady,
    HeatwaveActive,
}

public enum ProductMaintenanceChoice
{
    Undecided,
    Ordered,
    Skipped,
}

public enum ProductCommandError
{
    WrongPhase,
    NoDraft,
    OutOfBounds,
    NotBuildable,
    PositionOccupied,
    SpanTooLong,
    NothingToUndo,
    InsufficientCash,
}

public enum ProductSupplyFailure
{
    SubstationNotCommissioned,
    LineNotCommissioned,
    OutsideServiceArea,
    SourceCapacityInsufficient,
    LineCapacityInsufficient,
    SubstationCapacityInsufficient,
    None,
}

public enum ProductMissionOutcome
{
    Pending,
    Success,
    Failure,
}

public sealed record ProductSubstationSnapshot(
    ProductPoint? Position,
    ProductProjectState ProjectState,
    long? CompletionMinute);

public sealed record ProductLineSnapshot(
    IReadOnlyList<ProductPoint> SupportPositions,
    ProductProjectState ProjectState,
    long? CompletionMinute)
{
    public bool Equals(ProductLineSnapshot? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        ProjectState == other.ProjectState &&
        CompletionMinute == other.CompletionMinute &&
        SupportPositions.SequenceEqual(other.SupportPositions);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(ProjectState);
        hash.Add(CompletionMinute);
        foreach (ProductPoint support in SupportPositions)
        {
            hash.Add(support);
        }
        return hash.ToHashCode();
    }
}

public sealed record ProductSettlementSnapshot(
    bool Completed,
    long DeliveredEnergyKwMinute,
    long RevenueCashUnit);

public sealed record ProductHospitalLineSnapshot(
    string ProjectId,
    IReadOnlyList<ProductPoint> SupportPositions,
    ProductProjectState ProjectState,
    long? CompletionMinute,
    bool SpatialIncidentExposed)
{
    public bool Equals(ProductHospitalLineSnapshot? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        string.Equals(ProjectId, other.ProjectId, StringComparison.Ordinal) &&
        ProjectState == other.ProjectState &&
        CompletionMinute == other.CompletionMinute &&
        SpatialIncidentExposed == other.SpatialIncidentExposed &&
        SupportPositions.SequenceEqual(other.SupportPositions);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(ProjectId, StringComparer.Ordinal);
        hash.Add(ProjectState);
        hash.Add(CompletionMinute);
        hash.Add(SpatialIncidentExposed);
        foreach (ProductPoint support in SupportPositions)
        {
            hash.Add(support);
        }
        return hash.ToHashCode();
    }
}

public sealed record ProductReliabilitySnapshot(
    bool Evaluated,
    bool PrimaryRemovalKeepsHospitalUtility,
    bool BackupRemovalKeepsHospitalUtility)
{
    public bool AllSingleLineRemovalsKeepHospitalUtility =>
        Evaluated &&
        PrimaryRemovalKeepsHospitalUtility &&
        BackupRemovalKeepsHospitalUtility;
}

public sealed record ProductIncidentSnapshot(
    bool Started,
    bool Active,
    long? StartMinute,
    long? RecoveryMinute,
    IReadOnlyList<string> UnavailableProjectIds,
    long HospitalUtilityKw,
    long TownUtilityKw)
{
    public bool Equals(ProductIncidentSnapshot? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        Started == other.Started &&
        Active == other.Active &&
        StartMinute == other.StartMinute &&
        RecoveryMinute == other.RecoveryMinute &&
        HospitalUtilityKw == other.HospitalUtilityKw &&
        TownUtilityKw == other.TownUtilityKw &&
        UnavailableProjectIds.SequenceEqual(other.UnavailableProjectIds);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Started);
        hash.Add(Active);
        hash.Add(StartMinute);
        hash.Add(RecoveryMinute);
        hash.Add(HospitalUtilityKw);
        hash.Add(TownUtilityKw);
        foreach (string projectId in UnavailableProjectIds)
        {
            hash.Add(projectId, StringComparer.Ordinal);
        }
        return hash.ToHashCode();
    }
}

public sealed record ProductHospitalSettlementSnapshot(
    bool Completed,
    long HospitalUtilityEnergyKwMinute,
    long TownUtilityEnergyKwMinute,
    long UtilityGenerationEnergyKwMinute,
    long UtilityUnservedEnergyKwMinute,
    long UpsEnergyKwMinute,
    long DieselEnergyKwMinute,
    long HospitalP0UnservedEnergyKwMinute,
    long UtilityRevenueCashUnit,
    long GenerationCostCashUnit,
    long UnservedCompensationCashUnit,
    long LostSalesCashUnit,
    long CashChangeCashUnit,
    bool SingleLineRemovalConditionMet,
    bool SpatialIncidentUtilityConditionMet,
    bool HospitalP0ConditionMet);

public sealed record ProductHospitalSnapshot(
    string Id,
    string? ActiveProjectId,
    ProductHospitalLineSnapshot PrimaryLine,
    ProductHospitalLineSnapshot BackupLine,
    string? SelectedHospitalProjectId,
    long HospitalUtilityKw,
    long HospitalP0DeliveredKw,
    long TownUtilityKw,
    ProductReliabilitySnapshot Reliability,
    ProductIncidentSnapshot Incident,
    ProductHospitalSettlementSnapshot Settlement);

public sealed record ProductFactorySettlementSnapshot(
    bool Completed,
    long HospitalDeliveredEnergyKwMinute,
    long TownDeliveredEnergyKwMinute,
    long FactoryDeliveredEnergyKwMinute,
    long ExistingSourceGenerationEnergyKwMinute,
    long GasPlantGenerationEnergyKwMinute,
    long UtilityUnservedEnergyKwMinute,
    long UtilityRevenueCashUnit,
    long ExistingSourceGenerationCostCashUnit,
    long GasPlantGenerationCostCashUnit,
    long UnservedCompensationCashUnit,
    long LostSalesCashUnit,
    long CashChangeCashUnit,
    bool AllLoadsFullySupplied);

public sealed record ProductFactorySnapshot(
    string Id,
    string? SelectedSiteId,
    ProductPoint? PlantPosition,
    ProductProjectState PlantProjectState,
    long? PlantCompletionMinute,
    ProductLineSnapshot ConnectionLine,
    bool PlantGridConnected,
    long HospitalDeliveredKw,
    string? HospitalSourceAssetId,
    long TownDeliveredKw,
    string? TownSourceAssetId,
    long FactoryDeliveredKw,
    string? FactorySourceAssetId,
    long ExistingSourceDispatchKw,
    long GasPlantDispatchKw,
    ProductFactorySettlementSnapshot Settlement);

public sealed record ProductHeatwaveSettlementSnapshot(
    bool Completed,
    long HospitalDeliveredEnergyKwMinute,
    long TownDeliveredEnergyKwMinute,
    long FactoryDeliveredEnergyKwMinute,
    long ExistingSourceGenerationEnergyKwMinute,
    long GasPlantGenerationEnergyKwMinute,
    long UtilityUnservedEnergyKwMinute,
    long UtilityRevenueCashUnit,
    long ExistingSourceGenerationCostCashUnit,
    long GasPlantGenerationCostCashUnit,
    long UnservedCompensationCashUnit,
    long LostSalesCashUnit,
    long CashChangeCashUnit,
    bool AllLoadsFullySupplied);

public sealed record ProductHeatwaveSnapshot(
    string Id,
    long? StartMinute,
    long? RecoveryMinute,
    ProductMaintenanceChoice MaintenanceChoice,
    ProductProjectState MaintenanceProjectState,
    long? MaintenanceCompletionMinute,
    bool Active,
    bool AgedFactoryFeederCurrentlyUnavailable,
    bool AgedFactoryFeederUnavailableDuringEvent,
    long ForecastTownDemandKw,
    long ForecastFactoryFeederRatingKw,
    long CurrentTownDemandKw,
    long CurrentFactoryFeederRatingKw,
    long HospitalDeliveredKw,
    string? HospitalSourceAssetId,
    long TownDeliveredKw,
    string? TownSourceAssetId,
    long FactoryDeliveredKw,
    string? FactorySourceAssetId,
    long ExistingSourceDispatchKw,
    long GasPlantDispatchKw,
    ProductHeatwaveSettlementSnapshot Settlement);

public sealed record ProductSnapshot(
    long Minute,
    long Cash,
    ProductPhase Phase,
    ProductSubstationSnapshot Substation,
    ProductLineSnapshot Line,
    bool TownInServiceArea,
    ProductSupplyFailure SupplyFailure,
    long TownDeliveredKw,
    ProductSettlementSnapshot Settlement,
    ProductMissionOutcome Outcome,
    ProductHospitalSnapshot? Hospital = null,
    ProductFactorySnapshot? Factory = null,
    ProductHeatwaveSnapshot? Heatwave = null);

public sealed record ProductCommandResult(
    bool Accepted,
    ProductCommandError? Error,
    ProductSnapshot Snapshot);

public sealed record ProductSubstationPlacementPreview(
    bool Accepted,
    ProductCommandError? Error,
    ProductPoint Position,
    bool TownInServiceArea,
    ProductSupplyFailure? ProjectedSupplyFailure);

public sealed record ProductPlantPlacementPreview(
    bool Accepted,
    ProductCommandError? Error,
    ProductPoint Position,
    string? SiteId,
    long? SiteCostCashUnit);

public sealed record ProductLineSupportPreview(
    bool Accepted,
    ProductCommandError? Error,
    ProductPoint From,
    ProductPoint To,
    long DistanceSquared,
    long MaxSpanSquared);

public sealed record ProductOrderPreview(
    bool Accepted,
    ProductCommandError? Error,
    long? CostCashUnit,
    long? BuildMinutes,
    long? CompletionMinute,
    ProductSupplyFailure? ProjectedSupplyFailure,
    string? ActiveProjectId = null,
    bool? SpatialIncidentExposed = null);

public sealed class ProductFixtureValidationException : Exception
{
    public ProductFixtureValidationException(string message)
        : base(message)
    {
    }

    public ProductFixtureValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
