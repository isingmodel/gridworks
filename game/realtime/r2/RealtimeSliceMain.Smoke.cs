#if DEBUG
using System;
using System.Linq;
using Godot;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

internal sealed partial class RealtimeSliceMain
{
    /// <summary>
    /// Owns an off-tree smoke host. GodotObject.Dispose only releases the
    /// managed binding; Node.Free is required to release its native RID/object.
    /// </summary>
    internal IDisposable FreeAfterSmoke() => new SmokeLifetime(this);

    internal RealtimeUiRoot UiForSmoke => _ui ??
        throw new InvalidOperationException("Scene UI is not ready.");

    internal RealtimePlaceholderMap MapForSmoke => _worldView as RealtimePlaceholderMap ??
        throw new InvalidOperationException("The smoke scene is not using PlaceholderMap.");

    internal Rect2 MapInteractionRectForSmoke => _worldView is null
        ? throw new InvalidOperationException("Scene map is not ready.")
        : _worldView.InteractionRect;

    internal void ApplyMapInteractionRectForSmoke(Rect2 rect)
    {
        EnsureBootstrapped();
        ApplyMapInteractionRect(rect);
    }

    internal RealtimeComparisonDraftForecast ComparisonDraftForecastForSmoke =>
        _run?.GetComparisonDraftForecast() ??
        throw new InvalidOperationException("Slice Core is not ready.");

    internal RealtimeForecastSnapshot ForecastForHorizonForSmoke(
        long horizonMinutes) =>
        _run?.GetForecast(horizonMinutes) ??
        throw new InvalidOperationException("Slice Core is not ready.");

    internal RealtimeSlicePresentation PresentSnapshotForSmoke(
        RealtimeCampaignSnapshot snapshot,
        RealtimeInteractionState? interaction = null)
    {
        EnsureBootstrapped();
        return RealtimeSlicePresenter.Present(
            _data!.BaseWorld,
            _data.World,
            snapshot,
            _run!.GetComparisonDraftForecast(),
            interaction ?? _interaction!,
            _presentationRevision,
            nodeOrderQuote: snapshot.Construction.NodeDraft is not null
                ? _run.PreviewNodeOrder()
                : null,
            lineOrderQuote: snapshot.Construction.LineDraft is { EndNodeId: not null }
                ? _run.PreviewLineOrder()
                : null);
    }

    internal RealtimeProjectQuote PreviewNodeOrderForSmoke()
    {
        EnsureBootstrapped();
        return _run!.PreviewNodeOrder();
    }

    internal RealtimeProjectQuote PreviewLineOrderForSmoke()
    {
        EnsureBootstrapped();
        return _run!.PreviewLineOrder();
    }

    internal (string ToolId, CoreMapPoint Position) AcceptedNodeDraftForSmoke()
    {
        EnsureBootstrapped();
        RealtimeCampaignSnapshot snapshot = _run!.GetSnapshot();
        string nodeClassId = snapshot.Chapter.Content.AvailableNodeClassIds
            .OrderBy(item => item, StringComparer.Ordinal)
            .First();
        SpatialNodeClassDefinition nodeClass = snapshot.Construction.World.NodeClasses
            .Single(item => string.Equals(
                item.ClassId,
                nodeClassId,
                StringComparison.Ordinal));
        MapBounds bounds = snapshot.Construction.World.Bounds;
        int inset = Math.Max(1, nodeClass.FootprintRadiusUnit);
        int step = Math.Max(1, inset);
        for (int y = bounds.MinYUnit + inset;
             y <= bounds.MaxYUnit - inset;
             y = checked(y + step))
        {
            for (int x = bounds.MinXUnit + inset;
                 x <= bounds.MaxXUnit - inset;
                 x = checked(x + step))
            {
                var point = new CoreMapPoint(x, y);
                if (_run.PreviewNodePlacement(nodeClassId, point).Accepted)
                {
                    return ($"NODE:{nodeClassId}", point);
                }
            }
        }
        throw new InvalidOperationException(
            $"The embedded R1 fixture has no accepted {nodeClassId} smoke placement.");
    }

    internal CoreMapPoint RejectedNodeDraftForSmoke(string toolId)
    {
        EnsureBootstrapped();
        const string prefix = "NODE:";
        if (!toolId.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("A node build-tool ID is required.", nameof(toolId));
        }
        string nodeClassId = toolId[prefix.Length..];
        MapBounds bounds = _run!.GetSnapshot().Construction.World.Bounds;
        CoreMapPoint[] boundaryPoints =
        [
            new(bounds.MinXUnit, bounds.MinYUnit),
            new(bounds.MinXUnit, bounds.MaxYUnit),
            new(bounds.MaxXUnit, bounds.MinYUnit),
            new(bounds.MaxXUnit, bounds.MaxYUnit),
        ];
        foreach (CoreMapPoint point in boundaryPoints)
        {
            if (!_run.PreviewNodePlacement(nodeClassId, point).Accepted)
            {
                return point;
            }
        }
        throw new InvalidOperationException(
            $"The embedded R1 fixture has no rejected {nodeClassId} boundary placement.");
    }

    /// <summary>
    /// Binds the live debug UI signal to the same production action handler used
    /// by <see cref="WireNodes"/>. The harness always detaches after one press.
    /// </summary>
    internal void AttachActionUiForSmoke(RealtimeUiRoot ui) =>
        ui.ActionRequested += HandleAction;

    internal void DetachActionUiForSmoke(RealtimeUiRoot ui) =>
        ui.ActionRequested -= HandleAction;

    internal void AttachInputUiForSmoke(RealtimeUiRoot ui) =>
        ui.InputRequested += HandleInputRequest;

    internal void DetachInputUiForSmoke(RealtimeUiRoot ui) =>
        ui.InputRequested -= HandleInputRequest;

    internal void AttachTimelineUiForSmoke(RealtimeUiRoot ui)
    {
        ui.TimelineItemsRequested += HandleTimelineItems;
        ui.TimelineHorizonDeltaRequested += HandleTimelineHorizonDelta;
        ui.TimelineNavigationRequested += HandleTimelineNavigation;
    }

    internal void DetachTimelineUiForSmoke(RealtimeUiRoot ui)
    {
        ui.TimelineItemsRequested -= HandleTimelineItems;
        ui.TimelineHorizonDeltaRequested -= HandleTimelineHorizonDelta;
        ui.TimelineNavigationRequested -= HandleTimelineNavigation;
    }

    internal void EnterCampaignEndedForSmoke()
    {
        EnsureBootstrapped();
        _interaction = RealtimeInteractionReducer.AutoPause(
            _interaction!,
            RealtimePauseReason.CampaignResult);
        _frame!.Pause();
        Present();
    }

    internal void RequestBuildToolForSmoke(string toolId) =>
        HandleBuildTool(toolId);

    internal void RequestShortcutForSmoke(RealtimeInputCommand command) =>
        HandleShortcut(command);

    internal void FreezeAutonomousClockForSmoke()
    {
        EnsureBootstrapped();
        SetProcess(false);
    }

    internal bool AutonomousClockEnabledForSmoke => IsProcessing();

    private sealed class SmokeLifetime : IDisposable
    {
        private RealtimeSliceMain? _slice;

        internal SmokeLifetime(RealtimeSliceMain slice) => _slice = slice;

        public void Dispose()
        {
            RealtimeSliceMain? slice = _slice;
            _slice = null;
            if (slice is not null && GodotObject.IsInstanceValid(slice))
            {
                slice.Free();
            }
        }
    }
}
#endif
