using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;

namespace Gridworks.Game.Realtime.R2;

internal static class RealtimeShellPresenter
{
    internal static RealtimeTopHudPresentation PresentHud(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeInteractionState interaction,
        RealtimePausePresentation pause)
    {
        RealtimeReliabilityState reliability = Reliability(snapshot);
        string reliabilityLabel = reliability switch
        {
            RealtimeReliabilityState.Stable => "안정",
            RealtimeReliabilityState.Watch => "공급 주의",
            RealtimeReliabilityState.Emergency => "비상 열운전",
            RealtimeReliabilityState.Outage => "보호정지",
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
        };
        string? warning = snapshot.Thermal.Assets.Any(item => item.ProtectiveOutage)
            ? "보호정지 설비 있음 · 공급 경로 제외"
            : snapshot.Thermal.Assets.Any(item => item.State == ThermalOperatingState.Emergency)
                ? "비상 열운전 · 허용시간 감소"
                : snapshot.Thermal.Evaluation.Loads.Any(item => item.DeliveredKw < item.DemandKw)
                    ? "필수 수요 미공급 · 경로 확인"
                    : null;
        RealtimeConnectionRequirementAssessment? connection =
            snapshot.Forecast.ConnectionRequirementAssessment;
        string objective = connection is null
            ? snapshot.Chapter.Content.Objective
            : snapshot.Chapter.Content.Objective + " · 접속 조건 " +
              string.Join(
                  ", ",
                  connection.Facts.Select(item =>
                      $"{RealtimePresentationText.AssetDisplayName(displayWorld, snapshot, item.NodeId)} " +
                      $"{item.CurrentConnections}/{item.RequiredConnections}")) +
              (connection.FrozenForChapter ? " · 시험 시작 시점 고정" : " · 현재 망");
        return new RealtimeTopHudPresentation(
            snapshot.Chapter.Content.DisplayName,
            objective,
            RealtimePresentationText.Time(snapshot.Minute),
            $"운영 자금 {RealtimePresentationText.Cash(snapshot.CashUnit)}",
            reliabilityLabel,
            reliability,
            interaction.PresentedSpeed,
            warning)
        {
            SimulationState = interaction.Simulation,
            Pause = pause,
            ToolShelfVisible = interaction.Surface == RealtimeSurface.Drawer,
            BuildModeActive = !snapshot.CampaignComplete &&
                interaction.Simulation != RealtimeSimulationState.Ended &&
                (snapshot.Construction.NodeDraft is not null ||
                 snapshot.Construction.LineDraft is not null),
        };
    }

    internal static RealtimePausePresentation PresentPause(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastSnapshot baseForecast,
        RealtimePauseReason reason)
    {
        // An event already in progress is current operational context, not the
        // next event. Auto-pause and modal copy therefore advance strictly to
        // the first authoritative forecast whose start lies in the future.
        RealtimeForecastEvent? next = baseForecast.Events
            .Where(item => item.StartMinute > snapshot.Minute)
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => RealtimeTimelinePolicy.ForecastPriority(snapshot, item))
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .FirstOrDefault();
        return new RealtimePausePresentation(
            reason,
            snapshot.Minute,
            RealtimePresentationText.Time(snapshot.Minute),
            next?.EventId,
            next?.StartMinute,
            next is null
                ? "예정된 사건 없음"
                : $"{RealtimePresentationText.PlayerEventName(displayWorld, next.OperatingProfile)} · " +
                  RealtimePresentationText.Time(next.StartMinute));
    }

    private static RealtimeReliabilityState Reliability(RealtimeCampaignSnapshot snapshot)
    {
        if (snapshot.Thermal.Assets.Any(item => item.ProtectiveOutage))
        {
            return RealtimeReliabilityState.Outage;
        }
        if (snapshot.Thermal.Assets.Any(item => item.State is
                ThermalOperatingState.Emergency or ThermalOperatingState.OverLimit))
        {
            return RealtimeReliabilityState.Emergency;
        }
        return snapshot.Thermal.Evaluation.Loads.Any(item =>
                item.DeliveredKw < item.DemandKw)
            ? RealtimeReliabilityState.Watch
            : RealtimeReliabilityState.Stable;
    }

}

