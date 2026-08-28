using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;

namespace Gridworks.Game.Realtime.R2;

/// <summary>
/// Immutable input for one full projection of the current R2 screen.
/// Application-only story and completion decisions are calculated before this boundary.
/// </summary>
internal sealed record RealtimePresentationSource(
    RealtimeSliceData Data,
    RealtimeCampaignSnapshot Snapshot,
    RealtimeForecastSnapshot BaseForecast,
    RealtimeComparisonDraftForecast ComparisonDraftForecast,
    RealtimeInteractionState Interaction,
    long Revision,
    RealtimeWorldPointerFeedback Pointer,
    bool ReduceMotion,
    RealtimeProjectQuote? NodeOrderQuote,
    RealtimeProjectQuote? LineOrderQuote,
    IReadOnlyList<string> CompatibleLineNodeIds,
    IReadOnlyList<RealtimeTransition> TransitionHistory,
    RealtimeChapterStoryModalRequest? ActiveStoryRequest,
    bool StoryResultAdvancesCalendar,
    bool SuccessfulStandaloneCompletion,
    RealtimeEpilogueModalRequest? ActiveEpilogueRequest)
{
    private IReadOnlyList<string> _compatibleLineNodeIds =
        FreezeIds(CompatibleLineNodeIds);
    private IReadOnlyList<RealtimeTransition> _transitionHistory =
        Freeze(TransitionHistory);

    public IReadOnlyList<string> CompatibleLineNodeIds
    {
        get => _compatibleLineNodeIds;
        init => _compatibleLineNodeIds = FreezeIds(value);
    }

    public IReadOnlyList<RealtimeTransition> TransitionHistory
    {
        get => _transitionHistory;
        init => _transitionHistory = Freeze(value);
    }

    private static IReadOnlyList<RealtimeTransition> Freeze(
        IReadOnlyList<RealtimeTransition> transitions)
    {
        ArgumentNullException.ThrowIfNull(transitions);
        return Array.AsReadOnly(transitions.ToArray());
    }

    private static IReadOnlyList<string> FreezeIds(IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return Array.AsReadOnly(ids.ToArray());
    }
}
