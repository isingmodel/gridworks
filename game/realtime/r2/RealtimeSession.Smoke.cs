#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;

namespace Gridworks.Game.Realtime.R2;

internal sealed record RealtimeR2AdvanceResult(
    RealtimeAdvanceResult Advance,
    long PresentationRevisionDelta);

internal sealed record RealtimeR2TimelineChooserFacts(
    IReadOnlyList<string> VisibleOrderedItemIds,
    IReadOnlyList<string> ClusterItemIds,
    int ClusterIndex,
    string? SelectedMarkerId,
    string? SelectedSubjectId)
{
    private IReadOnlyList<string> _visibleOrderedItemIds =
        Array.AsReadOnly(VisibleOrderedItemIds.ToArray());
    private IReadOnlyList<string> _clusterItemIds =
        Array.AsReadOnly(ClusterItemIds.ToArray());

    public IReadOnlyList<string> VisibleOrderedItemIds
    {
        get => _visibleOrderedItemIds;
        init => _visibleOrderedItemIds = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string> ClusterItemIds
    {
        get => _clusterItemIds;
        init => _clusterItemIds = Array.AsReadOnly(value.ToArray());
    }
}

internal sealed partial class RealtimeSession
{
    internal RealtimeR2AdvanceResult AdvanceTo(long targetMinute)
    {
        long beforeRevision = _presentationRevision;
        RealtimeAdvanceResult result = _run.AdvanceTo(targetMinute);
        CollectTransitions(result.Transitions);
        Present();
        return new RealtimeR2AdvanceResult(
            result,
            _presentationRevision - beforeRevision);
    }

    internal void EnterCampaignEndedForSmoke()
    {
        _interaction = RealtimeInteractionReducer.AutoPause(
            _interaction,
            RealtimePauseReason.CampaignResult);
        _frame.Pause();
        Present();
    }

    internal RealtimeR2TimelineChooserFacts TimelineChooserFacts => new(
        Array.AsReadOnly(VisibleTimelineItems().Select(item => item.Id).ToArray()),
        _timelineClusterIds,
        _timelineClusterIndex,
        _interaction.TimelineSelectedItemId,
        _interaction.SelectionId);
}
#endif
