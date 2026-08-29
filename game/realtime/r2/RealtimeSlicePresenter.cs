using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;

namespace Gridworks.Game.Realtime.R2;

internal sealed record RealtimeSlicePresentation(
    long Revision,
    RealtimeCampaignSnapshot CoreSnapshot,
    RealtimeForecastSnapshot BaseForecast,
    RealtimeComparisonDraftForecast ComparisonDraftForecast,
    IReadOnlyList<RealtimeTransition> TransitionHistory,
    RealtimeInteractionPresentation Interaction,
    RealtimeWorldPresentation World,
    RealtimeWorldPointerFeedback Pointer,
    RealtimeTopHudPresentation Hud,
    RealtimeEventRailPresentation Rail,
    RealtimeContextDockPresentation Context,
    RealtimeBuildShelfPresentation BuildShelf,
    RealtimeActionDockPresentation ActionDock,
    RealtimeModalPresentation? Modal)
{
    private IReadOnlyList<RealtimeTransition> _transitionHistory =
        Array.AsReadOnly(TransitionHistory.ToArray());

    public IReadOnlyList<RealtimeTransition> TransitionHistory
    {
        get => _transitionHistory;
        init => _transitionHistory = Array.AsReadOnly(value.ToArray());
    }
}

internal static class RealtimeSlicePresenter
{
    internal static RealtimeSlicePresentation Present(RealtimePresentationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.Data);
        ArgumentNullException.ThrowIfNull(source.Data.BaseWorld);
        ArgumentNullException.ThrowIfNull(source.Data.World);
        ArgumentNullException.ThrowIfNull(source.Snapshot);
        ArgumentNullException.ThrowIfNull(source.BaseForecast);
        ArgumentNullException.ThrowIfNull(source.ComparisonDraftForecast);
        ArgumentNullException.ThrowIfNull(source.Interaction);
        ArgumentNullException.ThrowIfNull(source.Pointer);

        CommercialWorldDefinition displayWorld = source.Data.BaseWorld;
        RealtimeWorldDefinition realtimeWorld = source.Data.World;
        RealtimeCampaignSnapshot snapshot = source.Snapshot;
        RealtimeForecastSnapshot baseForecast = source.BaseForecast;
        RealtimeComparisonDraftForecast comparisonDraftForecast =
            source.ComparisonDraftForecast;
        RealtimeInteractionState interaction = source.Interaction;
        IReadOnlyList<RealtimeTransition> history = source.TransitionHistory;

        RealtimePausePresentation pause = RealtimeShellPresenter.PresentPause(
            displayWorld,
            snapshot,
            baseForecast,
            interaction.PauseReason);
        RealtimeInteractionPresentation interactionPresentation =
            interaction.ToPresentation(pause);
        RealtimeEventRailPresentation rail = RealtimeTimelinePresenter.Present(
            displayWorld,
            snapshot,
            baseForecast,
            comparisonDraftForecast,
            interaction,
            source.NodeOrderQuote,
            source.LineOrderQuote,
            history);
        RealtimeWorldPresentation world = RealtimeWorldPresenter.Present(
            displayWorld,
            snapshot,
            baseForecast,
            comparisonDraftForecast,
            interaction,
            source.ReduceMotion,
            source.CompatibleLineNodeIds,
            history);
        return new RealtimeSlicePresentation(
            source.Revision,
            snapshot,
            baseForecast,
            comparisonDraftForecast,
            history,
            interactionPresentation,
            world,
            source.Pointer,
            RealtimeShellPresenter.PresentHud(displayWorld, snapshot, interaction, pause),
            rail,
            RealtimeContextPresenter.Present(
                displayWorld,
                realtimeWorld,
                snapshot,
                baseForecast,
                comparisonDraftForecast,
                interaction.Surface == RealtimeSurface.Inspector
                    ? interaction.SelectionId
                    : null,
                source.NodeOrderQuote,
                source.LineOrderQuote,
                history),
            RealtimeConstructionPresenter.PresentBuildShelf(
                realtimeWorld,
                snapshot,
                interaction,
                source.Pointer.Accepted,
                source.Pointer.Message),
            RealtimeConstructionPresenter.PresentActionDock(
                snapshot,
                interaction,
                source.Pointer.Accepted,
                source.Pointer.Message,
                source.NodeOrderQuote,
                source.LineOrderQuote,
                source.Data.NativeRoute is not null &&
                string.Equals(
                    snapshot.Chapter.Content.ChapterId,
                    "FIRST_LIGHT",
                    StringComparison.Ordinal),
                world.PlacementClass),
            RealtimeModalPresenter.Present(source, pause));
    }

    /// <summary>
    /// Projects pointer feedback from the last authoritative presentation. This path performs no
    /// snapshot fetch or forecast calculation and only changes the world pointer, build guidance,
    /// and action-dock DTOs that can visibly depend on hover feedback.
    /// </summary>
    internal static RealtimeSlicePresentation PresentPointerFeedback(
        RealtimeSlicePresentation current,
        RealtimeInteractionState interaction,
        long revision,
        RealtimeWorldPointerFeedback pointer,
        RealtimeProjectQuote? nodeOrderQuote,
        RealtimeProjectQuote? lineOrderQuote,
        bool firstLightAdvanceEnabled)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(interaction);
        ArgumentNullException.ThrowIfNull(pointer);
        return current with
        {
            Revision = revision,
            Pointer = pointer,
            BuildShelf = current.BuildShelf with
            {
                Guidance = RealtimePresentationText.BuildGuidance(
                    current.CoreSnapshot,
                    interaction,
                    pointer.Accepted,
                    pointer.Message),
            },
            ActionDock = RealtimeConstructionPresenter.PresentActionDock(
                current.CoreSnapshot,
                interaction,
                pointer.Accepted,
                pointer.Message,
                nodeOrderQuote,
                lineOrderQuote,
                firstLightAdvanceEnabled,
                current.World.PlacementClass),
        };
    }
}
