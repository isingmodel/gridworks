using System.Text.Encodings.Web;
using System.Text.Json;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;

namespace Gridworks.CommercialChecks;

internal enum CommercialStoryPartKind
{
    Briefing,
    Window,
    Result,
    EpilogueCard,
    EpiloguePromiseLine,
}

internal enum CommercialStoryPartErrorCode
{
    InvalidSelector,
    UnknownChapter,
    UnreachableStoryPart,
}

internal abstract record CommercialStoryContent;

internal sealed record CommercialStoryCardContent(CommercialStoryCard Card)
    : CommercialStoryContent;

internal sealed record CommercialPromiseLineContent(string Text)
    : CommercialStoryContent;

internal sealed record CommercialRealtimeScheduleBinding(
    string ChapterId,
    int PreparationMinutes,
    int? PromiseDecisionDeadlineOffsetMinutes,
    IReadOnlyList<string> ScheduledEventIds)
{
    private IReadOnlyList<string> _scheduledEventIds =
        Array.AsReadOnly(ScheduledEventIds.ToArray());

    public IReadOnlyList<string> ScheduledEventIds
    {
        get => _scheduledEventIds;
        init => _scheduledEventIds = Array.AsReadOnly(value.ToArray());
    }
}

internal sealed record CommercialStoryPart(
    string CampaignId,
    string Selector,
    CommercialStoryPartKind Kind,
    string? ChapterId,
    string? WindowId,
    string? PromiseId,
    CommercialPromiseDecision? RequiredPromiseBranch,
    CommercialStoryContent Content,
    CommercialRealtimeScheduleBinding? RealtimeSchedule);

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
        "gridworks.commercial.story-part-output.v2";
    public const string ManifestSchemaVersion =
        "gridworks.commercial.story-manifest.v2";
    public const string ErrorSchemaVersion =
        "gridworks.commercial.story-part-error.v2";
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = true,
        SkipValidation = false,
    };

    private readonly CommercialCampaignDefinition _campaign;
    private readonly RealtimeCampaignDefinition _realtimeCampaign;
    private readonly IReadOnlyList<CommercialStoryPart> _parts;
    private readonly IReadOnlyDictionary<string, CommercialStoryPart> _partsBySelector;
    private readonly IReadOnlyDictionary<string, CommercialCampaignChapterDefinition>
        _chaptersById;

    public CommercialStoryPartHarness(
        CommercialCampaignDefinition campaign,
        RealtimeCampaignDefinition realtimeCampaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(realtimeCampaign);
        if (realtimeCampaign.CampaignId != campaign.CampaignId ||
            !ReferenceEquals(realtimeCampaign.Content, campaign))
        {
            throw new ArgumentException(
                "Realtime campaign must retain the supplied authored campaign content.",
                nameof(realtimeCampaign));
        }
        if (realtimeCampaign.Chapters.Count != campaign.Chapters.Count ||
            realtimeCampaign.Chapters.Where((chapter, index) =>
                chapter.Content.ChapterId != campaign.Chapters[index].ChapterId).Any())
        {
            throw new ArgumentException(
                "Realtime schedule must cover every authored campaign chapter in order.",
                nameof(realtimeCampaign));
        }

        _campaign = campaign;
        _realtimeCampaign = realtimeCampaign;
        _parts = Array.AsReadOnly(BuildParts(campaign, realtimeCampaign).ToArray());
        _partsBySelector = _parts.ToDictionary(
            part => part.Selector,
            StringComparer.Ordinal);
        _chaptersById = campaign.Chapters.ToDictionary(
            chapter => chapter.ChapterId,
            StringComparer.Ordinal);
    }

    public CommercialCampaignDefinition Campaign => _campaign;

    public RealtimeCampaignDefinition RealtimeCampaign => _realtimeCampaign;

    public IReadOnlyList<CommercialStoryPart> Parts => _parts;

    public CommercialStoryPart Select(string selector)
    {
        selector ??= string.Empty;
        if (_partsBySelector.TryGetValue(selector, out CommercialStoryPart? part))
        {
            return part;
        }

        string[] segments = selector.Split('/', StringSplitOptions.None);
        if (TryChapterSelector(segments, out string chapterId))
        {
            if (!_chaptersById.ContainsKey(chapterId))
            {
                throw SelectionError(
                    selector,
                    CommercialStoryPartErrorCode.UnknownChapter,
                    $"Story selector references unknown chapter '{chapterId}'.");
            }
            throw SelectionError(
                selector,
                CommercialStoryPartErrorCode.UnreachableStoryPart,
                $"Story selector '{selector}' has no authored content in chapter '{chapterId}'.");
        }

        if (TryEpiloguePromiseSelector(segments, out chapterId))
        {
            if (!_chaptersById.ContainsKey(chapterId))
            {
                throw SelectionError(
                    selector,
                    CommercialStoryPartErrorCode.UnknownChapter,
                    $"Epilogue promise selector references unknown chapter '{chapterId}'.");
            }
            throw SelectionError(
                selector,
                CommercialStoryPartErrorCode.UnreachableStoryPart,
                $"Story selector '{selector}' has no authored epilogue promise line.");
        }

        if (segments.Length == 4 &&
            segments[0] == "campaign" &&
            segments[1] == "epilogue" &&
            segments[2] == "card" &&
            segments[3].Length > 0)
        {
            throw SelectionError(
                selector,
                CommercialStoryPartErrorCode.UnreachableStoryPart,
                $"Story selector '{selector}' has no authored epilogue card.");
        }

        throw SelectionError(
            selector,
            CommercialStoryPartErrorCode.InvalidSelector,
            "Selector must match the exact canonical story-part grammar.");
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
        writer.WriteString("baseCampaignSchemaVersion", _campaign.SchemaVersion);
        writer.WriteString(
            "realtimeCampaignSchemaVersion",
            _realtimeCampaign.SchemaVersion);
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
        CommercialCampaignDefinition campaign,
        RealtimeCampaignDefinition realtimeCampaign)
    {
        IReadOnlyDictionary<string, CommercialRealtimeScheduleBinding> schedules =
            realtimeCampaign.Chapters.ToDictionary(
                chapter => chapter.Content.ChapterId,
                Schedule,
                StringComparer.Ordinal);

        foreach (CommercialCampaignChapterDefinition chapter in campaign.Chapters)
        {
            CommercialRealtimeScheduleBinding schedule = schedules[chapter.ChapterId];
            yield return StoryCardPart(
                campaign.CampaignId,
                $"{chapter.ChapterId}/briefing",
                CommercialStoryPartKind.Briefing,
                chapter.ChapterId,
                windowId: null,
                requiredPromiseBranch: null,
                chapter.Briefing,
                schedule);

            foreach (CommercialDecisionWindowDefinition window in chapter.DecisionWindows)
            {
                if (window.Story is not null)
                {
                    yield return StoryCardPart(
                        campaign.CampaignId,
                        $"{chapter.ChapterId}/window/{window.WindowId}",
                        CommercialStoryPartKind.Window,
                        chapter.ChapterId,
                        window.WindowId,
                        requiredPromiseBranch: null,
                        window.Story,
                        schedule);
                }
            }

            if (chapter.ResultCards.Standard is not null)
            {
                yield return StoryCardPart(
                    campaign.CampaignId,
                    $"{chapter.ChapterId}/result/standard",
                    CommercialStoryPartKind.Result,
                    chapter.ChapterId,
                    windowId: null,
                    requiredPromiseBranch: null,
                    chapter.ResultCards.Standard,
                    schedule);
            }
            if (chapter.ResultCards.Kept is not null)
            {
                yield return StoryCardPart(
                    campaign.CampaignId,
                    $"{chapter.ChapterId}/result/keep",
                    CommercialStoryPartKind.Result,
                    chapter.ChapterId,
                    windowId: null,
                    CommercialPromiseDecision.Keep,
                    chapter.ResultCards.Kept,
                    schedule);
            }
            if (chapter.ResultCards.Deferred is not null)
            {
                yield return StoryCardPart(
                    campaign.CampaignId,
                    $"{chapter.ChapterId}/result/defer",
                    CommercialStoryPartKind.Result,
                    chapter.ChapterId,
                    windowId: null,
                    CommercialPromiseDecision.Defer,
                    chapter.ResultCards.Deferred,
                    schedule);
            }
        }

        yield return StoryCardPart(
            campaign.CampaignId,
            "campaign/epilogue/card/city-report",
            CommercialStoryPartKind.EpilogueCard,
            chapterId: null,
            windowId: null,
            requiredPromiseBranch: null,
            campaign.Epilogue.CityReport,
            schedule: null);
        yield return StoryCardPart(
            campaign.CampaignId,
            "campaign/epilogue/card/medical-witness",
            CommercialStoryPartKind.EpilogueCard,
            chapterId: null,
            windowId: null,
            requiredPromiseBranch: null,
            campaign.Epilogue.MedicalWitness,
            schedule: null);
        yield return StoryCardPart(
            campaign.CampaignId,
            "campaign/epilogue/card/closing",
            CommercialStoryPartKind.EpilogueCard,
            chapterId: null,
            windowId: null,
            requiredPromiseBranch: null,
            campaign.Epilogue.Closing,
            schedule: null);

        foreach (CommercialCampaignEpiloguePromiseLineDefinition promiseLine in
                 campaign.Epilogue.PromiseLines)
        {
            CommercialRealtimeScheduleBinding schedule = schedules[promiseLine.ChapterId];
            yield return PromiseLinePart(
                campaign.CampaignId,
                $"campaign/epilogue/promise/{promiseLine.ChapterId}/keep",
                promiseLine,
                CommercialPromiseDecision.Keep,
                promiseLine.Kept,
                schedule);
            yield return PromiseLinePart(
                campaign.CampaignId,
                $"campaign/epilogue/promise/{promiseLine.ChapterId}/defer",
                promiseLine,
                CommercialPromiseDecision.Defer,
                promiseLine.Deferred,
                schedule);
        }
    }

    private static CommercialStoryPart StoryCardPart(
        string campaignId,
        string selector,
        CommercialStoryPartKind kind,
        string? chapterId,
        string? windowId,
        CommercialPromiseDecision? requiredPromiseBranch,
        CommercialStoryCard story,
        CommercialRealtimeScheduleBinding? schedule) =>
        new(
            campaignId,
            selector,
            kind,
            chapterId,
            windowId,
            PromiseId: null,
            requiredPromiseBranch,
            new CommercialStoryCardContent(story),
            schedule);

    private static CommercialStoryPart PromiseLinePart(
        string campaignId,
        string selector,
        CommercialCampaignEpiloguePromiseLineDefinition promiseLine,
        CommercialPromiseDecision branch,
        string text,
        CommercialRealtimeScheduleBinding schedule) =>
        new(
            campaignId,
            selector,
            CommercialStoryPartKind.EpiloguePromiseLine,
            promiseLine.ChapterId,
            WindowId: null,
            promiseLine.PromiseId,
            branch,
            new CommercialPromiseLineContent(text),
            schedule);

    private static CommercialRealtimeScheduleBinding Schedule(
        RealtimeChapterDefinition chapter) =>
        new(
            chapter.Content.ChapterId,
            chapter.PreparationMinutes,
            chapter.PromiseDecisionDeadlineOffsetMinutes,
            chapter.ScheduledEvents.Select(item => item.EventId).ToArray());

    private static bool TryChapterSelector(
        IReadOnlyList<string> segments,
        out string chapterId)
    {
        chapterId = string.Empty;
        if (segments.Count == 2 &&
            segments[0].Length > 0 &&
            segments[1] == "briefing")
        {
            chapterId = segments[0];
            return true;
        }
        if (segments.Count != 3 ||
            segments[0].Length == 0 ||
            segments[2].Length == 0)
        {
            return false;
        }
        if (segments[1] == "window" ||
            segments[1] == "result" &&
            segments[2] is "standard" or "keep" or "defer")
        {
            chapterId = segments[0];
            return true;
        }
        return false;
    }

    private static bool TryEpiloguePromiseSelector(
        IReadOnlyList<string> segments,
        out string chapterId)
    {
        chapterId = string.Empty;
        if (segments.Count != 5 ||
            segments[0] != "campaign" ||
            segments[1] != "epilogue" ||
            segments[2] != "promise" ||
            segments[3].Length == 0 ||
            segments[4] is not ("keep" or "defer"))
        {
            return false;
        }
        chapterId = segments[3];
        return true;
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
        writer.WriteBoolean("authoredReachable", true);
        WriteNullableString(
            writer,
            "requiredPromiseBranch",
            PromiseBranchText(part.RequiredPromiseBranch));
        if (part.RealtimeSchedule is null)
        {
            writer.WriteNull("realtimeSchedule");
        }
        else
        {
            writer.WriteStartObject("realtimeSchedule");
            writer.WriteString("chapterId", part.RealtimeSchedule.ChapterId);
            writer.WriteNumber(
                "preparationMinutes",
                part.RealtimeSchedule.PreparationMinutes);
            if (part.RealtimeSchedule.PromiseDecisionDeadlineOffsetMinutes is int deadline)
            {
                writer.WriteNumber("promiseDecisionDeadlineOffsetMinutes", deadline);
            }
            else
            {
                writer.WriteNull("promiseDecisionDeadlineOffsetMinutes");
            }
            writer.WriteStartArray("scheduledEventIds");
            foreach (string eventId in part.RealtimeSchedule.ScheduledEventIds)
            {
                writer.WriteStringValue(eventId);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteStartObject("content");
        switch (part.Content)
        {
            case CommercialStoryCardContent story:
                writer.WriteString("contentType", "story-card");
                writer.WriteString("speaker", story.Card.Speaker);
                writer.WriteString("title", story.Card.Title);
                writer.WriteString("body", story.Card.Body);
                break;
            case CommercialPromiseLineContent promiseLine:
                writer.WriteString("contentType", "promise-line");
                writer.WriteString("promiseId", part.PromiseId);
                writer.WriteString(
                    "branch",
                    PromiseBranchText(part.RequiredPromiseBranch));
                writer.WriteString("text", promiseLine.Text);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(part));
        }
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
        CommercialStoryPartKind.EpilogueCard => "epilogue-card",
        CommercialStoryPartKind.EpiloguePromiseLine => "epilogue-promise-line",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string? PromiseBranchText(CommercialPromiseDecision? branch) => branch switch
    {
        null => null,
        CommercialPromiseDecision.Keep => "keep",
        CommercialPromiseDecision.Defer => "defer",
        _ => throw new ArgumentOutOfRangeException(nameof(branch)),
    };

    private static string ErrorCodeText(CommercialStoryPartErrorCode errorCode) =>
        errorCode switch
        {
            CommercialStoryPartErrorCode.InvalidSelector => "INVALID_SELECTOR",
            CommercialStoryPartErrorCode.UnknownChapter => "UNKNOWN_CHAPTER",
            CommercialStoryPartErrorCode.UnreachableStoryPart =>
                "UNREACHABLE_STORY_PART",
            _ => throw new ArgumentOutOfRangeException(nameof(errorCode)),
        };
}
