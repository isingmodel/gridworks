using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;

namespace Gridworks.Game.Realtime.R2;

internal enum RealtimeEpiloguePurpose
{
    CityReport,
    MedicalWitness,
    Closing,
}

internal sealed record RealtimeEpilogueModalRequest(
    RealtimeEpiloguePurpose Purpose,
    CommercialStoryCard Card,
    IReadOnlyList<string> PromiseLines,
    long RemainingCashUnit)
{
    private IReadOnlyList<string> _promiseLines = Freeze(PromiseLines);

    public IReadOnlyList<string> PromiseLines
    {
        get => _promiseLines;
        init => _promiseLines = Freeze(value);
    }

    internal string ModalId => RealtimeR2Ids.EpilogueModal(Purpose);

    internal bool FinalCard => Purpose == RealtimeEpiloguePurpose.Closing;

    private static IReadOnlyList<string> Freeze(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

/// <summary>
/// Owns the one-shot, three-card epilogue sequence after the caller has closed
/// a validated full-campaign finale. Authored content and completed Core outcomes
/// remain the only authorities for the cards and promise lines.
/// </summary>
internal sealed class RealtimeEpilogueFlow
{
    private readonly Queue<RealtimeEpilogueModalRequest> _pending = new();

    internal RealtimeEpilogueModalRequest? Active { get; private set; }

    internal bool Started { get; private set; }

    internal bool Completed => Started && Active is null && _pending.Count == 0;

    internal bool TryStart(
        CommercialCampaignDefinition fullCampaign,
        RealtimeCampaignDefinition selectedCampaign,
        RealtimeCampaignSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(fullCampaign);
        ArgumentNullException.ThrowIfNull(selectedCampaign);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (Started)
        {
            throw new InvalidOperationException(
                "The realtime epilogue flow has already started.");
        }

        RealtimeEpilogueModalRequest[]? requests = BuildRequests(
            fullCampaign,
            selectedCampaign,
            snapshot);
        if (requests is null)
        {
            return false;
        }

        foreach (RealtimeEpilogueModalRequest request in requests)
        {
            _pending.Enqueue(request);
        }
        Started = true;
        return true;
    }

    internal bool RestoreCompleted(
        CommercialCampaignDefinition fullCampaign,
        RealtimeCampaignDefinition selectedCampaign,
        RealtimeCampaignSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(fullCampaign);
        ArgumentNullException.ThrowIfNull(selectedCampaign);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (Started)
        {
            throw new InvalidOperationException(
                "The realtime epilogue flow has already started.");
        }

        RealtimeEpilogueModalRequest[]? requests = BuildRequests(
            fullCampaign,
            selectedCampaign,
            snapshot);
        if (requests is null)
        {
            return false;
        }

        // Terminal restore validates the same authored sequence as live start,
        // then consumes every card without reopening a modal or adding a cursor.
        foreach (RealtimeEpilogueModalRequest request in requests)
        {
            _pending.Enqueue(request);
        }
        while (_pending.TryDequeue(out _))
        {
        }
        Started = true;
        return true;
    }

    internal static bool IsFullCampaign(
        CommercialCampaignDefinition fullCampaign,
        RealtimeCampaignDefinition selectedCampaign)
    {
        ArgumentNullException.ThrowIfNull(fullCampaign);
        ArgumentNullException.ThrowIfNull(selectedCampaign);
        string[] fullChapterIds = fullCampaign.Chapters
            .Select(chapter => chapter.ChapterId)
            .ToArray();
        if (fullChapterIds.Length == 0)
        {
            throw new InvalidOperationException(
                "The realtime epilogue requires a non-empty full campaign.");
        }
        return selectedCampaign.Chapters
            .Select(chapter => chapter.Content.ChapterId)
            .SequenceEqual(fullChapterIds, StringComparer.Ordinal);
    }

    internal RealtimeEpilogueModalRequest? ActivateNext()
    {
        if (Active is null &&
            _pending.TryDequeue(out RealtimeEpilogueModalRequest? next))
        {
            Active = next;
        }
        return Active;
    }

    internal bool Close(string modalId)
    {
        if (Active is null || !string.Equals(
                Active.ModalId,
                modalId,
                StringComparison.Ordinal))
        {
            return false;
        }
        Active = null;
        return true;
    }

    private static RealtimeEpilogueModalRequest[]? BuildRequests(
        CommercialCampaignDefinition fullCampaign,
        RealtimeCampaignDefinition selectedCampaign,
        RealtimeCampaignSnapshot snapshot)
    {
        if (!IsFullCampaign(fullCampaign, selectedCampaign))
        {
            return null;
        }

        string[] fullChapterIds = fullCampaign.Chapters
            .Select(chapter => chapter.ChapterId)
            .ToArray();
        string[] completedChapterIds = snapshot.CompletedChapters
            .Select(outcome => outcome.ChapterId)
            .ToArray();
        if (!snapshot.CampaignComplete ||
            !completedChapterIds.SequenceEqual(fullChapterIds, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The realtime epilogue requires every full-campaign chapter outcome in authored order.");
        }
        if (!snapshot.CompletedChapters[^1].ObjectiveSatisfied)
        {
            return null;
        }

        IReadOnlyList<string> promiseLines = PromiseLines(fullCampaign, snapshot);
        CommercialCampaignEpilogueDefinition epilogue = fullCampaign.Epilogue;
        return
        [
            new RealtimeEpilogueModalRequest(
                RealtimeEpiloguePurpose.CityReport,
                epilogue.CityReport,
                promiseLines,
                snapshot.CashUnit),
            new RealtimeEpilogueModalRequest(
                RealtimeEpiloguePurpose.MedicalWitness,
                epilogue.MedicalWitness,
                Array.Empty<string>(),
                snapshot.CashUnit),
            new RealtimeEpilogueModalRequest(
                RealtimeEpiloguePurpose.Closing,
                epilogue.Closing,
                Array.Empty<string>(),
                snapshot.CashUnit),
        ];
    }

    private static string[] PromiseLines(
        CommercialCampaignDefinition fullCampaign,
        RealtimeCampaignSnapshot snapshot)
    {
        var chapters = new Dictionary<string, CommercialCampaignChapterDefinition>(
            StringComparer.Ordinal);
        foreach (CommercialCampaignChapterDefinition chapter in fullCampaign.Chapters)
        {
            if (!chapters.TryAdd(chapter.ChapterId, chapter))
            {
                throw new InvalidOperationException(
                    "The supplied full campaign contains duplicate chapter IDs.");
            }
        }

        var outcomes = new Dictionary<string, RealtimeChapterOutcome>(
            StringComparer.Ordinal);
        foreach (RealtimeChapterOutcome outcome in snapshot.CompletedChapters)
        {
            if (!outcomes.TryAdd(outcome.ChapterId, outcome))
            {
                throw new InvalidOperationException(
                    "The completed campaign contains duplicate chapter outcomes.");
            }
        }

        return fullCampaign.Epilogue.PromiseLines.Select(line =>
        {
            if (!chapters.TryGetValue(
                    line.ChapterId,
                    out CommercialCampaignChapterDefinition? chapter) ||
                chapter.CityPromise is not CommercialCityPromiseDefinition promise ||
                !string.Equals(
                    promise.PromiseId,
                    line.PromiseId,
                    StringComparison.Ordinal) ||
                !outcomes.TryGetValue(
                    line.ChapterId,
                    out RealtimeChapterOutcome? outcome))
            {
                throw new InvalidOperationException(
                    "An authored epilogue promise line does not join to its completed chapter outcome.");
            }
            return outcome.PromiseDecision switch
            {
                CommercialPromiseDecision.Keep => line.Kept,
                CommercialPromiseDecision.Defer => line.Deferred,
                _ => throw new InvalidOperationException(
                    "A completed epilogue promise outcome has no Keep/Defer decision."),
            };
        }).ToArray();
    }
}
