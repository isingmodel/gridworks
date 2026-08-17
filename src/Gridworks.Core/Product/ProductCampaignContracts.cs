namespace Gridworks.Core.Product;

public sealed record ProductCampaignChapter(
    string ChapterId,
    string DisplayName);

public sealed record ProductCampaignDefinition(
    string SchemaVersion,
    string CampaignId,
    string DisplayName,
    string ScenarioFixture,
    IReadOnlyList<ProductCampaignChapter> Chapters);

public enum ProductCampaignCommandKind
{
    SetSubstationDraft,
    CancelSubstationDraft,
    AddLineSupport,
    UndoLineSupport,
    CancelLineDraft,
    OrderSubstation,
    OrderLine,
    AdvanceToConstructionCompletion,
    AdvanceToSettlement,
    AdvanceToIncident,
    AdvanceToRecoveryAndSettlement,
    SetPlantDraft,
    CancelPlantDraft,
    OrderPlant,
    AdvanceToFactorySettlement,
    OrderPreventiveMaintenance,
    SkipPreventiveMaintenance,
    AdvanceToHeatwave,
    AdvanceToHeatwaveSettlement,
}

public sealed record ProductCampaignCommand(
    ProductCampaignCommandKind Kind,
    ProductPoint? Position = null);

public sealed record ProductCampaignSave(
    string SchemaVersion,
    string CampaignId,
    string CampaignRootSha256,
    string FixtureId,
    string FixtureSha256,
    IReadOnlyList<ProductCampaignCommand> Commands)
{
    public const string SupportedSchemaVersion = "gridworks.campaign-save.v1";
}

public enum ProductWindowMode
{
    Windowed,
    Fullscreen,
}

public sealed record ProductSettings(
    string SchemaVersion,
    ProductWindowMode WindowMode,
    int UiScalePercent,
    bool ShowControlHelp)
{
    public const string SupportedSchemaVersion = "gridworks.settings.v1";

    public static ProductSettings Default { get; } = new(
        SupportedSchemaVersion,
        ProductWindowMode.Windowed,
        100,
        true);
}

public enum ProductDocumentLoadStatus
{
    Missing,
    Loaded,
    Invalid,
}

public sealed record ProductCampaignSaveLoadResult(
    ProductDocumentLoadStatus Status,
    ProductCampaignSave? Save,
    string? ErrorMessage);

public sealed record ProductSettingsLoadResult(
    ProductDocumentLoadStatus Status,
    ProductSettings Settings,
    string? ErrorMessage);

public sealed class ProductCampaignValidationException : Exception
{
    public ProductCampaignValidationException(string message)
        : base(message)
    {
    }

    public ProductCampaignValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class ProductPersistenceValidationException : Exception
{
    public ProductPersistenceValidationException(string message)
        : base(message)
    {
    }

    public ProductPersistenceValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
