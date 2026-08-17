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

public sealed record ProductTown(string Id, ProductPoint Position, long DemandKw);

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
    ProductLineProjectDefinition LineProject);

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
    ProductMissionOutcome Outcome);

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
    ProductSupplyFailure? ProjectedSupplyFailure);

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
