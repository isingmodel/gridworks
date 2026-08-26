using System;
using System.IO;
using Gridworks.Core.Release.V3;

namespace Gridworks.Game.Realtime.R2;

internal enum RealtimeCampaignSaveLoadStatus
{
    Missing,
    Loaded,
    Invalid,
    Unsupported,
    IoFailure,
}

internal sealed record RealtimeCampaignSaveLoadResult(
    RealtimeCampaignSaveLoadStatus Status,
    RealtimeCampaignSave? Save,
    string? Message);

/// <summary>
/// Owns the one current-R2 campaign-save file. Route/source reconciliation stays
/// in the product adapter; this store only probes bytes and writes atomically.
/// </summary>
internal static class RealtimeCampaignSaveStore
{
    internal const string FileName = "gridworks-r2-campaign-save-v1.json";

    internal static RealtimeCampaignSaveLoadResult Load(string absolutePath)
    {
        ValidatePath(absolutePath);
        try
        {
            RealtimeCampaignSave save = RealtimeCampaignSaveCodec.Deserialize(
                File.ReadAllBytes(absolutePath));
            return new RealtimeCampaignSaveLoadResult(
                RealtimeCampaignSaveLoadStatus.Loaded,
                save,
                null);
        }
        catch (RealtimeCampaignPersistenceException exception)
        {
            return new RealtimeCampaignSaveLoadResult(
                exception.Kind == RealtimeCampaignPersistenceFailureKind.Unsupported
                    ? RealtimeCampaignSaveLoadStatus.Unsupported
                    : RealtimeCampaignSaveLoadStatus.Invalid,
                null,
                exception.Message);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new RealtimeCampaignSaveLoadResult(
                RealtimeCampaignSaveLoadStatus.Missing,
                null,
                null);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return new RealtimeCampaignSaveLoadResult(
                RealtimeCampaignSaveLoadStatus.IoFailure,
                null,
                exception.Message);
        }
    }

    internal static void Save(string absolutePath, RealtimeCampaignSave save)
    {
        ValidatePath(absolutePath);
        ArgumentNullException.ThrowIfNull(save);
        byte[] bytes = RealtimeCampaignSaveCodec.Serialize(save);
        string directory = Path.GetDirectoryName(absolutePath)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = $"{absolutePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (FileStream stream = new(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, absolutePath, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // The target outcome is already decided. A private temp cleanup
                // failure must not turn a successful atomic replacement into loss.
            }
        }
    }

    private static void ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path) || Path.GetDirectoryName(path) is null)
        {
            throw new ArgumentException(
                "The realtime save path must be an absolute file path.",
                nameof(path));
        }
    }
}
