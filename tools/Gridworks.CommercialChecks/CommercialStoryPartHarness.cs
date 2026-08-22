using System.Text.Encodings.Web;
using System.Text.Json;
using Gridworks.Core.Release.V2;

namespace Gridworks.CommercialChecks;

internal enum CommercialStoryPartKind
{
    Briefing,
    Window,
    Result,
    Epilogue,
}

internal enum CommercialStoryPartErrorCode
{
    InvalidSelector,
    UnknownChapter,
    UnreachableStoryPart,
}

internal sealed record CommercialStoryPart(
    string CampaignId,
    string Selector,
    CommercialStoryPartKind Kind,
    string? ChapterId,
    string? WindowId,
    bool Reachable,
    PromiseDecision? RequiredPromiseBranch,
    CommercialStoryCard Story);

internal sealed class CommercialStoryPartSelectionException : Exception
{
    public CommercialStoryPartSelectionException(
        string selector,
        CommercialStoryPartErrorCode errorCode,
        string message)
        : base(message)
    {
        Selector = selector;
        ErrorCode = errorCode;
    }

    public string Selector { get; }

    public CommercialStoryPartErrorCode ErrorCode { get; }
}

internal sealed class CommercialStoryPartHarness
{
    public const string OutputSchemaVersion =
        "gridworks.commercial.story-part-output.v1";
    public const string ManifestSchemaVersion =
        "gridworks.commercial.story-manifest.v1";
    public const string ErrorSchemaVersion =
        "gridworks.commercial.story-part-error.v1";

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = true,
        SkipValidation = false,
    };

    private readonly CommercialCampaignDefinition _campaign;
    private readonly IReadOnlyList<CommercialStoryPart> _parts;
    private readonly IReadOnlyDictionary<string, CommercialStoryPart> _partsBySelector;
    private readonly IReadOnlyDictionary<string, CommercialCoreChapter> _chaptersById;

    public CommercialStoryPartHarness(CommercialCampaignDefinition campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        _campaign = campaign;
        _parts = Array.AsReadOnly(BuildParts(campaign).ToArray());
        _partsBySelector = _parts.ToDictionary(
            part => part.Selector,
            StringComparer.Ordinal);
        _chaptersById = campaign.Chapters.ToDictionary(
            chapter => chapter.ChapterId,
            StringComparer.Ordinal);
    }

    public IReadOnlyList<CommercialStoryPart> Parts => _parts;

    public CommercialStoryPart Select(string selector)
    {
        selector ??= string.Empty;
        if (selector == "campaign/epilogue")
        {
            return _partsBySelector[selector];
        }

        string[] segments = selector.Split('/', StringSplitOptions.None);
        if (!HasChapterSelectorGrammar(segments))
        {
            throw SelectionError(
                selector,
                CommercialStoryPartErrorCode.InvalidSelector,
                "Selector must match the exact canonical story-part grammar.");
        }

        string chapterId = segments[0];
        if (!_chaptersById.ContainsKey(chapterId))
        {
            throw SelectionError(
                selector,
                CommercialStoryPartErrorCode.UnknownChapter,
                $"Story selector references unknown chapter '{chapterId}'.");
        }

        if (!_partsBySelector.TryGetValue(selector, out CommercialStoryPart? part))
        {
            throw SelectionError(
                selector,
                CommercialStoryPartErrorCode.UnreachableStoryPart,
                $"Story selector '{selector}' is not reachable in chapter '{chapterId}'.");
        }
        return part;
    }

    public byte[] Serialize(CommercialStoryPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return WriteJson(writer => WritePart(writer, part));
    }

    public byte[] SerializeManifest() => WriteJson(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("schemaVersion", ManifestSchemaVersion);
        writer.WriteString("campaignId", _campaign.CampaignId);
        writer.WriteNumber("count", _parts.Count);
        writer.WriteStartArray("parts");
        foreach (CommercialStoryPart part in _parts)
        {
            WritePart(writer, part);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    public static byte[] SerializeError(CommercialStoryPartSelectionException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", ErrorSchemaVersion);
            writer.WriteString("selector", exception.Selector);
            writer.WriteString("errorCode", ErrorCodeText(exception.ErrorCode));
            writer.WriteString("message", exception.Message);
            writer.WriteEndObject();
        });
    }

    private static IEnumerable<CommercialStoryPart> BuildParts(
        CommercialCampaignDefinition campaign)
    {
        foreach (CommercialCoreChapter chapter in campaign.Chapters)
        {
            yield return Part(
                campaign.CampaignId,
                $"{chapter.ChapterId}/briefing",
                CommercialStoryPartKind.Briefing,
                chapter.ChapterId,
                windowId: null,
                requiredPromiseBranch: null,
                story: chapter.Briefing);

            foreach (CommercialCoreDecisionWindow window in chapter.DecisionWindows)
            {
                if (window.Story is not null)
                {
                    yield return Part(
                        campaign.CampaignId,
                        $"{chapter.ChapterId}/window/{window.WindowId}",
                        CommercialStoryPartKind.Window,
                        chapter.ChapterId,
                        window.WindowId,
                        requiredPromiseBranch: null,
                        story: window.Story);
                }
            }

            if (chapter.Promise is null)
            {
                yield return Part(
                    campaign.CampaignId,
                    $"{chapter.ChapterId}/result/standard",
                    CommercialStoryPartKind.Result,
                    chapter.ChapterId,
                    windowId: null,
                    requiredPromiseBranch: null,
                    story: chapter.StandardResult);
            }
            else
            {
                yield return Part(
                    campaign.CampaignId,
                    $"{chapter.ChapterId}/result/keep",
                    CommercialStoryPartKind.Result,
                    chapter.ChapterId,
                    windowId: null,
                    requiredPromiseBranch: PromiseDecision.Keep,
                    story: chapter.KeptResult!);
                yield return Part(
                    campaign.CampaignId,
                    $"{chapter.ChapterId}/result/defer",
                    CommercialStoryPartKind.Result,
                    chapter.ChapterId,
                    windowId: null,
                    requiredPromiseBranch: PromiseDecision.Defer,
                    story: chapter.DeferredResult!);
            }
        }

        yield return Part(
            campaign.CampaignId,
            "campaign/epilogue",
            CommercialStoryPartKind.Epilogue,
            chapterId: null,
            windowId: null,
            requiredPromiseBranch: null,
            story: campaign.Epilogue);
    }

    private static CommercialStoryPart Part(
        string campaignId,
        string selector,
        CommercialStoryPartKind kind,
        string? chapterId,
        string? windowId,
        PromiseDecision? requiredPromiseBranch,
        CommercialStoryCard story) =>
        new(
            campaignId,
            selector,
            kind,
            chapterId,
            windowId,
            Reachable: true,
            requiredPromiseBranch,
            story);

    private static bool HasChapterSelectorGrammar(IReadOnlyList<string> segments)
    {
        if (segments.Count == 2)
        {
            return segments[0].Length > 0 && segments[1] == "briefing";
        }
        if (segments.Count != 3 || segments[0].Length == 0 || segments[2].Length == 0)
        {
            return false;
        }
        if (segments[1] == "window")
        {
            return true;
        }
        return segments[1] == "result" &&
            segments[2] is "standard" or "keep" or "defer";
    }

    private static CommercialStoryPartSelectionException SelectionError(
        string selector,
        CommercialStoryPartErrorCode errorCode,
        string message) => new(selector, errorCode, message);

    private static byte[] WriteJson(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            write(writer);
        }
        return stream.ToArray();
    }

    private static void WritePart(Utf8JsonWriter writer, CommercialStoryPart part)
    {
        writer.WriteStartObject();
        writer.WriteString("schemaVersion", OutputSchemaVersion);
        writer.WriteString("campaignId", part.CampaignId);
        writer.WriteString("selector", part.Selector);
        writer.WriteString("kind", KindText(part.Kind));
        WriteNullableString(writer, "chapterId", part.ChapterId);
        WriteNullableString(writer, "windowId", part.WindowId);
        writer.WriteBoolean("reachable", part.Reachable);
        WriteNullableString(
            writer,
            "requiredPromiseBranch",
            PromiseBranchText(part.RequiredPromiseBranch));
        writer.WriteStartObject("story");
        writer.WriteString("speaker", part.Story.Speaker);
        writer.WriteString("title", part.Story.Title);
        writer.WriteString("body", part.Story.Body);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteNullableString(
        Utf8JsonWriter writer,
        string propertyName,
        string? value)
    {
        if (value is null)
        {
            writer.WriteNull(propertyName);
        }
        else
        {
            writer.WriteString(propertyName, value);
        }
    }

    private static string KindText(CommercialStoryPartKind kind) => kind switch
    {
        CommercialStoryPartKind.Briefing => "briefing",
        CommercialStoryPartKind.Window => "window",
        CommercialStoryPartKind.Result => "result",
        CommercialStoryPartKind.Epilogue => "epilogue",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string? PromiseBranchText(PromiseDecision? branch) => branch switch
    {
        null => null,
        PromiseDecision.Keep => "keep",
        PromiseDecision.Defer => "defer",
        _ => throw new ArgumentOutOfRangeException(nameof(branch)),
    };

    private static string ErrorCodeText(CommercialStoryPartErrorCode errorCode) =>
        errorCode switch
        {
            CommercialStoryPartErrorCode.InvalidSelector => "INVALID_SELECTOR",
            CommercialStoryPartErrorCode.UnknownChapter => "UNKNOWN_CHAPTER",
            CommercialStoryPartErrorCode.UnreachableStoryPart => "UNREACHABLE_STORY_PART",
            _ => throw new ArgumentOutOfRangeException(nameof(errorCode)),
        };
}
