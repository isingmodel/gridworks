using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gridworks.Core.Release.V2;

namespace Gridworks.CommercialChecks;

/// <summary>
/// Replays the exact journal bytes supplied by the Commercial UX gold validator and
/// proves that every supplied snapshot is the canonical snapshot produced by Core.
/// The batch input carries base64 bytes rather than paths so a caller cannot swap a
/// file between Python's raw-byte validation and Core replay.
/// </summary>
internal static class CommercialGoldReplayVerifier
{
    private const string InputSchema = "gridworks.commercial-ux.gold-replay-batch-input.v1";
    private const string OutputSchema = "gridworks.commercial-ux.gold-replay-batch-report.v1";

    private static readonly JsonSerializerOptions StrictInputOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly JsonSerializerOptions CanonicalSnapshotOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
        },
    };

    private static readonly JsonSerializerOptions ReportOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
        },
    };

    public static void VerifyBatch(string absoluteInputPath, Stream output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteInputPath);
        ArgumentNullException.ThrowIfNull(output);
        if (!Path.IsPathFullyQualified(absoluteInputPath))
        {
            throw new ArgumentException("gold replay batch input path must be absolute");
        }

        byte[] inputBytes = File.ReadAllBytes(absoluteInputPath);
        RejectDuplicateKeys(inputBytes);
        GoldReplayBatchInput input = JsonSerializer.Deserialize<GoldReplayBatchInput>(
            inputBytes,
            StrictInputOptions) ?? throw new InvalidDataException("gold replay batch is empty");
        if (input.SchemaVersion != InputSchema)
        {
            throw new InvalidDataException("gold replay batch schemaVersion mismatch");
        }
        if (input.Entries is null || input.Entries.Count == 0)
        {
            throw new InvalidDataException("gold replay batch entries must be non-empty");
        }
        if (input.Entries.Select(entry => entry.Owner).Distinct(StringComparer.Ordinal).Count()
            != input.Entries.Count)
        {
            throw new InvalidDataException("gold replay batch owner values must be unique");
        }

        byte[] worldBytes = DecodeBase64(input.WorldBytesBase64, "worldBytesBase64");
        byte[] campaignBytes = DecodeBase64(input.CampaignBytesBase64, "campaignBytesBase64");
        CommercialWorldDefinition world = CommercialWorldLoader.Load(worldBytes);
        CommercialCampaignDefinition campaign = CommercialCampaignLoader.Load(
            campaignBytes,
            world);

        var results = new List<GoldReplayEntryReport>(input.Entries.Count);
        foreach (GoldReplayBatchEntry entry in input.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Owner))
            {
                throw new InvalidDataException("gold replay entry owner must be non-empty");
            }
            byte[] journalBytes = DecodeBase64(
                entry.JournalBytesBase64,
                $"{entry.Owner}.journalBytesBase64");
            byte[] snapshotBytes = DecodeBase64(
                entry.SnapshotBytesBase64,
                $"{entry.Owner}.snapshotBytesBase64");

            CommercialCampaignSaveV3 save = CommercialCampaignSaveCodec.Deserialize(journalBytes);
            byte[] canonicalJournalBytes = CommercialCampaignSaveCodec.Serialize(save);
            if (!journalBytes.AsSpan().SequenceEqual(canonicalJournalBytes))
            {
                throw new InvalidDataException(
                    $"{entry.Owner}: journal bytes are not canonical CommercialCampaignSaveCodec output");
            }
            CommercialCoreRun run = CommercialCampaignSaveCodec.Restore(
                save,
                world,
                worldBytes,
                campaign,
                campaignBytes);
            CommercialCoreSnapshot snapshot = run.GetSnapshot();
            byte[] expectedSnapshotBytes = JsonSerializer.SerializeToUtf8Bytes(
                snapshot,
                CanonicalSnapshotOptions);
            if (!snapshotBytes.AsSpan().SequenceEqual(expectedSnapshotBytes))
            {
                throw new InvalidDataException(
                    $"{entry.Owner}: snapshot bytes are not the canonical replay result");
            }

            LineDraftSnapshot? lineDraft = snapshot.Construction.LineDraft;
            ConstructionQuote lineOrderProjection = run.PreviewLineOrder();
            results.Add(new GoldReplayEntryReport(
                entry.Owner,
                Sha256(journalBytes),
                save.Commands.Count,
                Sha256(snapshotBytes),
                new GoldReplayStateSummary(
                    snapshot.Chapter.ChapterId,
                    snapshot.DecisionWindow?.WindowId,
                    snapshot.DecisionWindowIndex,
                    snapshot.ChapterResults.Count,
                    snapshot.CampaignComplete,
                    snapshot.Construction.Phase,
                    snapshot.Construction.NodeDraft is not null,
                    lineDraft is not null,
                    snapshot.PromiseDecision,
                    snapshot.ThermalMemory.Count(item => item.ProtectiveOutage),
                    snapshot.CommandCount),
                lineDraft,
                lineOrderProjection));
        }

        var report = new GoldReplayBatchReport(OutputSchema, results);
        JsonSerializer.Serialize(output, report, ReportOptions);
        output.WriteByte((byte)'\n');
        output.Flush();
    }

    public static void EmitSnapshot(string absoluteInputPath, Stream output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteInputPath);
        ArgumentNullException.ThrowIfNull(output);
        if (!Path.IsPathFullyQualified(absoluteInputPath))
        {
            throw new ArgumentException("gold snapshot input path must be absolute");
        }
        byte[] inputBytes = File.ReadAllBytes(absoluteInputPath);
        RejectDuplicateKeys(inputBytes);
        GoldSnapshotInput input = JsonSerializer.Deserialize<GoldSnapshotInput>(
            inputBytes,
            StrictInputOptions) ?? throw new InvalidDataException("gold snapshot input is empty");
        if (input.SchemaVersion != "gridworks.commercial-ux.gold-snapshot-input.v1")
        {
            throw new InvalidDataException("gold snapshot input schemaVersion mismatch");
        }
        byte[] worldBytes = DecodeBase64(input.WorldBytesBase64, "worldBytesBase64");
        byte[] campaignBytes = DecodeBase64(input.CampaignBytesBase64, "campaignBytesBase64");
        byte[] journalBytes = DecodeBase64(input.JournalBytesBase64, "journalBytesBase64");
        CommercialWorldDefinition world = CommercialWorldLoader.Load(worldBytes);
        CommercialCampaignDefinition campaign = CommercialCampaignLoader.Load(
            campaignBytes,
            world);
        CommercialCampaignSaveV3 save = CommercialCampaignSaveCodec.Deserialize(journalBytes);
        byte[] canonicalJournalBytes = CommercialCampaignSaveCodec.Serialize(save);
        if (!journalBytes.AsSpan().SequenceEqual(canonicalJournalBytes))
        {
            throw new InvalidDataException(
                "journal bytes are not canonical CommercialCampaignSaveCodec output");
        }
        CommercialCoreRun run = CommercialCampaignSaveCodec.Restore(
            save,
            world,
            worldBytes,
            campaign,
            campaignBytes);
        JsonSerializer.Serialize(output, run.GetSnapshot(), CanonicalSnapshotOptions);
        output.Flush();
    }

    private static byte[] DecodeBase64(string? encoded, string label)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            throw new InvalidDataException($"{label} must be non-empty base64");
        }
        try
        {
            return Convert.FromBase64String(encoded);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException($"{label} is not canonical base64", exception);
        }
    }

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void RejectDuplicateKeys(byte[] bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        });
        RejectDuplicateKeys(document.RootElement, "$");
    }

    private static void RejectDuplicateKeys(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException($"duplicate JSON key: {path}.{property.Name}");
                }
                RejectDuplicateKeys(property.Value, $"{path}.{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicateKeys(item, $"{path}[{index++}]");
            }
        }
    }

    private sealed record GoldReplayBatchInput(
        string SchemaVersion,
        string WorldBytesBase64,
        string CampaignBytesBase64,
        IReadOnlyList<GoldReplayBatchEntry> Entries);

    private sealed record GoldSnapshotInput(
        string SchemaVersion,
        string WorldBytesBase64,
        string CampaignBytesBase64,
        string JournalBytesBase64);

    private sealed record GoldReplayBatchEntry(
        string Owner,
        string JournalBytesBase64,
        string SnapshotBytesBase64);

    private sealed record GoldReplayBatchReport(
        string SchemaVersion,
        IReadOnlyList<GoldReplayEntryReport> Entries);

    private sealed record GoldReplayEntryReport(
        string Owner,
        string JournalRawSha256,
        int CommandCount,
        string SnapshotRawSha256,
        GoldReplayStateSummary State,
        LineDraftSnapshot? DraftGeometry,
        ConstructionQuote DraftProjection);

    private sealed record GoldReplayStateSummary(
        string ChapterId,
        string? DecisionWindowId,
        int DecisionWindowIndex,
        int ChapterResultsCount,
        bool CampaignComplete,
        ConstructionPhase ConstructionPhase,
        bool NodeDraftPresent,
        bool LineDraftPresent,
        PromiseDecision? PromiseDecision,
        int ThermalMemoryProtectiveOutageCount,
        int CommandCount);
}
