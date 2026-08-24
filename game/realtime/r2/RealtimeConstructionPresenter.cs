using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;

namespace Gridworks.Game.Realtime.R2;

internal static class RealtimeConstructionPresenter
{
    internal static RealtimeBuildShelfPresentation PresentBuildShelf(
        RealtimeWorldDefinition realtimeWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeInteractionState interaction,
        bool pointerAccepted,
        string pointerMessage)
    {
        bool ended = snapshot.CampaignComplete ||
            interaction.Simulation == RealtimeSimulationState.Ended;
        RealtimeDraftToolLock? draftToolLock = ended
            ? null
            : RealtimeInteractionReducer.ResolveDraftToolLock(snapshot.Construction);
        bool inspectEnabled = draftToolLock is null;
        var tools = new List<RealtimeBuildToolPresentation>
        {
            new(
                RealtimeR2Ids.InspectTool,
                "선택·검사",
                "I",
                inspectEnabled
                    ? "설비와 사건을 선택해 현재 상태를 확인합니다."
                    : draftToolLock!.RejectionReason,
                inspectEnabled,
                draftToolLock is null && interaction.Tool == RealtimeTool.Inspect),
        };
        foreach (string classId in snapshot.Chapter.Content.AvailableNodeClassIds)
        {
            SpatialNodeClassDefinition nodeClass = snapshot.Construction.World.NodeClasses
                .Single(item => string.Equals(
                    item.ClassId,
                    classId,
                    StringComparison.Ordinal));
            string toolId = RealtimeR2Ids.NodeTool(classId);
            bool enabled = !ended && (draftToolLock is null || string.Equals(
                draftToolLock.RequiredBuildToolId,
                toolId,
                StringComparison.Ordinal));
            string nodeDescription = ended
                ? RealtimeInteractionReducer.CampaignEndedReadOnlyReason
                : !enabled
                ? draftToolLock!.RejectionReason
                : snapshot.Construction.ActiveConstruction is
                ActiveConstructionSnapshot active
                ? $"비교 초안은 만들 수 있습니다. {RealtimePresentationText.Time(active.CompletionMinute)}까지 두 번째 발주는 대기합니다."
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{nodeClass.DisplayName} · 비용 {RealtimePresentationText.Cash(nodeClass.CostCashUnit)} · 공기 {nodeClass.BuildMinutes}분");
            tools.Add(new RealtimeBuildToolPresentation(
                toolId,
                $"{nodeClass.DisplayName} 배치",
                "N",
                nodeDescription,
                enabled,
                !ended && (draftToolLock is not null
                    ? string.Equals(
                        draftToolLock.RequiredBuildToolId,
                        toolId,
                        StringComparison.Ordinal)
                    : interaction.Tool == RealtimeTool.BuildNode &&
                      string.Equals(
                          interaction.SelectedBuildToolId,
                          toolId,
                          StringComparison.Ordinal))));
        }
        foreach (CommercialCampaignLinePlanDefinition plan in
                 snapshot.Chapter.Content.AvailableLinePlans)
        {
            SpatialLineClassDefinition lineClass = snapshot.Construction.World.LineClasses
                .Single(item => string.Equals(
                    item.ClassId,
                    plan.LineClassId,
                    StringComparison.Ordinal));
            SpatialNodeClassDefinition poleClass = snapshot.Construction.World.NodeClasses
                .Single(item => string.Equals(
                    item.ClassId,
                    plan.PoleClassId,
                    StringComparison.Ordinal));
            ThermalProtectionDefinition lineProtection = realtimeWorld.ProtectionFor(
                ThermalAssetKind.Edge,
                plan.LineClassId);
            string toolId = RealtimeR2Ids.LineTool(
                plan.LineClassId,
                plan.PoleClassId);
            bool enabled = !ended && (draftToolLock is null || string.Equals(
                draftToolLock.RequiredBuildToolId,
                toolId,
                StringComparison.Ordinal));
            string lineDescription = ended
                ? RealtimeInteractionReducer.CampaignEndedReadOnlyReason
                : !enabled
                ? draftToolLock!.RejectionReason
                : snapshot.Construction.ActiveConstruction is
                ActiveConstructionSnapshot active
                ? $"비교 경로는 그릴 수 있습니다. {RealtimePresentationText.Time(active.CompletionMinute)}까지 두 번째 발주는 대기합니다."
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{lineClass.DisplayName} · 비용 {RealtimePresentationText.Cash(lineClass.CostCashUnitPerDesignUnit)}/설계단위 · " +
                    $"공기 {lineClass.BuildMinutesPerDesignUnit}분/설계단위 · " +
                    $"연속 {lineProtection.ContinuousKw:N0} kW · {poleClass.DisplayName} 접속부");
            tools.Add(new RealtimeBuildToolPresentation(
                toolId,
                $"{lineClass.DisplayName} 건설",
                "L",
                lineDescription,
                enabled,
                !ended && (draftToolLock is not null
                    ? string.Equals(
                        draftToolLock.RequiredBuildToolId,
                        toolId,
                        StringComparison.Ordinal)
                    : interaction.Tool == RealtimeTool.BuildLine &&
                      string.Equals(
                          interaction.SelectedBuildToolId,
                          toolId,
                          StringComparison.Ordinal))));
        }
        bool analysisEnabled = draftToolLock is null;
        tools.Add(new RealtimeBuildToolPresentation(
            RealtimeR2Ids.AnalysisTool,
            interaction.Tool == RealtimeTool.Analysis
                ? "망 분석 켜짐"
                : "망 분석",
            "A",
            analysisEnabled
                ? interaction.Tool == RealtimeTool.Analysis
                    ? "망 분석 켜짐 · 공급 경로와 첫 병목을 지도 위에 겹쳐 보고 있습니다."
                    : "공급 경로와 첫 병목을 지도 위에 겹쳐 봅니다."
                : draftToolLock!.RejectionReason,
            analysisEnabled,
            draftToolLock is null && interaction.Tool == RealtimeTool.Analysis));
        return new RealtimeBuildShelfPresentation(
            interaction.Surface == RealtimeSurface.Drawer,
            Array.AsReadOnly(tools.ToArray()))
        {
            Guidance = RealtimePresentationText.BuildGuidance(
                snapshot,
                interaction,
                pointerAccepted,
                pointerMessage),
        };
    }

    internal static RealtimeActionDockPresentation PresentActionDock(
        RealtimeCampaignSnapshot snapshot,
        RealtimeInteractionState interaction,
        bool pointerAccepted,
        string pointerMessage,
        RealtimeProjectQuote? nodeOrderQuote,
        RealtimeProjectQuote? lineOrderQuote)
    {
        ConstructionSnapshot construction = snapshot.Construction;
        bool ended = snapshot.CampaignComplete ||
            interaction.Simulation == RealtimeSimulationState.Ended;
        if (ended && (construction.NodeDraft is not null ||
                      construction.LineDraft is not null))
        {
            return new RealtimeActionDockPresentation(
                true,
                "운영 완료 · 읽기 전용 초안",
                RealtimeInteractionReducer.CampaignEndedReadOnlyReason,
                new RealtimeActionPresentation(
                    construction.NodeDraft is not null
                        ? RealtimeR2Ids.OrderNodeAction
                        : RealtimeR2Ids.OrderLineAction,
                    "운영 완료 · 공사 시작 불가",
                    RealtimeInteractionReducer.CampaignEndedReadOnlyReason,
                    false));
        }
        if (construction.NodeDraft is not null)
        {
            bool enabled = OrderQuoteEnabled(snapshot, nodeOrderQuote);
            string quoteDetail = RealtimePresentationText.OrderQuoteDetail(
                snapshot,
                nodeOrderQuote,
                construction.ActiveConstruction);
            return new RealtimeActionDockPresentation(
                true,
                "변전소 초안",
                RealtimePresentationText.FeedbackDetail(pointerAccepted, pointerMessage, quoteDetail),
                new RealtimeActionPresentation(
                    RealtimeR2Ids.OrderNodeAction,
                    "변전소 공사 시작",
                    quoteDetail,
                    enabled));
        }
        if (construction.LineDraft is { EndNodeId: not null })
        {
            bool enabled = OrderQuoteEnabled(snapshot, lineOrderQuote);
            string quoteDetail = RealtimePresentationText.OrderQuoteDetail(
                snapshot,
                lineOrderQuote,
                construction.ActiveConstruction);
            return new RealtimeActionDockPresentation(
                true,
                "선로 초안",
                RealtimePresentationText.FeedbackDetail(pointerAccepted, pointerMessage, quoteDetail),
                new RealtimeActionPresentation(
                    RealtimeR2Ids.OrderLineAction,
                    "선로 공사 시작",
                    quoteDetail,
                    enabled));
        }
        if (construction.ActiveConstruction is ActiveConstructionSnapshot active)
        {
            return new RealtimeActionDockPresentation(
                true,
                "공사 진행",
                $"{RealtimePresentationText.Time(active.CompletionMinute)} 자동 완공 · 두 번째 발주 불가",
                null);
        }
        return new RealtimeActionDockPresentation(false, string.Empty, string.Empty, null);
    }

    private static bool OrderQuoteEnabled(
        RealtimeCampaignSnapshot snapshot,
        RealtimeProjectQuote? quote) => quote is
        {
            Accepted: true,
            CostCashUnit: not null,
            BuildMinutes: not null,
            CompletionMinute: not null,
        } && quote.CostCashUnit.Value <= snapshot.CashUnit;

}

