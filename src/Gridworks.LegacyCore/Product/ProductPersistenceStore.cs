namespace Gridworks.Core.Product;

public static class ProductPersistenceStore
{
    public const string CampaignSaveFileName = "campaign-save.json";
    public const string SettingsFileName = "settings.json";

    public static ProductCampaignSaveLoadResult LoadCampaignSave(string absolutePath)
    {
        ValidateAbsolutePath(absolutePath);
        if (!File.Exists(absolutePath))
        {
            return new ProductCampaignSaveLoadResult(
                ProductDocumentLoadStatus.Missing,
                null,
                null);
        }
        try
        {
            ProductCampaignSave save = ProductCampaignSaveCodec.Deserialize(
                File.ReadAllBytes(absolutePath));
            return new ProductCampaignSaveLoadResult(
                ProductDocumentLoadStatus.Loaded,
                save,
                null);
        }
        catch (Exception exception) when (
            exception is ProductPersistenceValidationException or IOException or
            UnauthorizedAccessException)
        {
            return new ProductCampaignSaveLoadResult(
                ProductDocumentLoadStatus.Invalid,
                null,
                exception.Message);
        }
    }

    public static void SaveCampaign(string absolutePath, ProductCampaignSave save)
    {
        ValidateAbsolutePath(absolutePath);
        ProductAtomicFile.Write(
            absolutePath,
            ProductCampaignSaveCodec.Serialize(save));
    }

    public static ProductSettingsLoadResult LoadSettings(string absolutePath)
    {
        ValidateAbsolutePath(absolutePath);
        if (!File.Exists(absolutePath))
        {
            return new ProductSettingsLoadResult(
                ProductDocumentLoadStatus.Missing,
                ProductSettings.Default,
                null);
        }
        try
        {
            byte[] storedBytes = File.ReadAllBytes(absolutePath);
            ProductSettings settings = ProductSettingsCodec.Deserialize(
                storedBytes);
            byte[] canonicalBytes = ProductSettingsCodec.Serialize(settings);
            if (!storedBytes.AsSpan().SequenceEqual(canonicalBytes))
            {
                ProductAtomicFile.Write(absolutePath, canonicalBytes);
            }
            return new ProductSettingsLoadResult(
                ProductDocumentLoadStatus.Loaded,
                settings,
                null);
        }
        catch (Exception exception) when (
            exception is ProductPersistenceValidationException or IOException or
            UnauthorizedAccessException)
        {
            return new ProductSettingsLoadResult(
                ProductDocumentLoadStatus.Invalid,
                ProductSettings.Default,
                exception.Message);
        }
    }

    public static void SaveSettings(string absolutePath, ProductSettings settings)
    {
        ValidateAbsolutePath(absolutePath);
        ProductAtomicFile.Write(
            absolutePath,
            ProductSettingsCodec.Serialize(settings));
    }

    private static void ValidateAbsolutePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path) || Path.GetDirectoryName(path) is null)
        {
            throw new ArgumentException("Persistence path must be absolute.", nameof(path));
        }
    }
}

internal static class ProductAtomicFile
{
    internal static void Write(string absolutePath, ReadOnlySpan<byte> content)
    {
        string directory = Path.GetDirectoryName(absolutePath)
            ?? throw new ArgumentException(
                "Persistence path must include a directory.",
                nameof(absolutePath));
        Directory.CreateDirectory(directory);
        string temporaryPath = string.Concat(absolutePath, ".tmp");

        using (FileStream stream = new(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None))
        {
            stream.Write(content);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporaryPath, absolutePath, overwrite: true);
    }
}
