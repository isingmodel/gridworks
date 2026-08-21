namespace Gridworks.Core.Release.V2;

public sealed record CommercialCampaignDefinition(
    string SchemaVersion,
    string CampaignId,
    string DisplayName,
    string WorldId,
    IReadOnlyList<CommercialCoreChapter> Chapters,
    CommercialStoryCard Epilogue)
{
    public const string SupportedSchemaVersion = "gridworks.commercial.campaign.v2";

    private IReadOnlyList<CommercialCoreChapter> _chapters =
        Array.AsReadOnly(Chapters.ToArray());

    public IReadOnlyList<CommercialCoreChapter> Chapters
    {
        get => _chapters;
        init => _chapters = Array.AsReadOnly(value.ToArray());
    }
}

public sealed class CommercialCampaignValidationException : Exception
{
    public CommercialCampaignValidationException(string message)
        : base(message)
    {
    }

    public CommercialCampaignValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
