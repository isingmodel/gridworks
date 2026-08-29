using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;

namespace Gridworks.Game.Realtime.R2;

/// <summary>
/// Pure player-facing naming and formatting for the R2 presentation and session.
/// </summary>
internal static class RealtimePresentationText
{
    internal static string ConstructionErrorText(ConstructionError? error) => error switch
    {
        null => "알 수 없는 공간 규칙 오류입니다.",
        ConstructionError.WrongPhase => "지금은 이 공사 단계를 실행할 수 없습니다.",
        ConstructionError.UnknownNodeClass or
        ConstructionError.InvalidNodeClass or
        ConstructionError.UnknownLineClass or
        ConstructionError.UnknownPoleClass or
        ConstructionError.InvalidPoleClass => "선택한 공사 등급을 사용할 수 없습니다.",
        ConstructionError.OutsideBounds => "설비 전체가 지도 안에 들어오도록 옮기세요.",
        ConstructionError.WaterFootprint => "물 위에는 설비를 놓을 수 없습니다.",
        ConstructionError.BuildingFootprint => "건물 점유영역을 피하세요.",
        ConstructionError.PositionOccupied => "다른 설비와 겹치지 않도록 간격을 두세요.",
        ConstructionError.ExistingLineTouch => "기존 선로와 닿지 않는 위치를 고르세요.",
        ConstructionError.EndpointNotFound => "연결할 접속 설비가 없습니다.",
        ConstructionError.EndpointNotCommissioned => "완공된 접속 설비만 연결할 수 있습니다.",
        ConstructionError.SameEndpoint => "시작점과 다른 접속 설비를 선택하세요.",
        ConstructionError.ConnectionLimit => "이 설비의 접속 회선 한도를 넘습니다.",
        ConstructionError.SpanTooLong => "허용 경간을 넘습니다. 중간 경로점을 추가하세요.",
        ConstructionError.ZeroLengthSegment => "같은 위치에 경로점을 연속으로 둘 수 없습니다.",
        ConstructionError.ThirdNodeTouch => "연결 대상이 아닌 다른 설비와 닿습니다.",
        ConstructionError.DuplicateSegment => "같은 접속점을 잇는 선로가 이미 있습니다.",
        ConstructionError.CollinearOverlap => "기존 선로와 같은 방향으로 포개집니다.",
        ConstructionError.BuildingCrossing => "선로가 건물을 가로지릅니다.",
        ConstructionError.DraftIncomplete => "다른 접속 설비까지 경로를 이어야 합니다.",
        ConstructionError.NothingToUndo => "되돌릴 경로점이 없습니다.",
        ConstructionError.InvalidPointIndex => "옮길 경로점을 찾지 못했습니다.",
        ConstructionError.ArithmeticOverflow => "좌표나 견적이 계산 범위를 벗어났습니다.",
        ConstructionError.InvalidCompletion => "완공 결과가 공간 규칙을 만족하지 않습니다.",
        _ => throw new ArgumentOutOfRangeException(nameof(error)),
    };

    internal static string RealtimeRunErrorText(RealtimeRunError? error) => error switch
    {
        null => "입력 원인을 확인하지 못했습니다.",
        RealtimeRunError.WrongState => "현재 운영 상태에서는 실행할 수 없습니다.",
        RealtimeRunError.InvalidCommandShape => "입력 순서가 올바르지 않습니다.",
        RealtimeRunError.ToolUnavailable => "이 임무에서 사용할 수 없는 공사입니다.",
        RealtimeRunError.ConstructionRejected => "공간·공사 규칙을 만족하지 않습니다.",
        RealtimeRunError.InsufficientCash => "운영 자금이 부족합니다.",
        RealtimeRunError.PromiseUnavailable => "지금은 운영 약속을 선택할 수 없습니다.",
        RealtimeRunError.PromiseDeadlinePassed => "운영 약속 선택 시간이 지났습니다.",
        RealtimeRunError.ClockMismatch => "시각이 바뀌었습니다. 현재 상태를 다시 확인하세요.",
        RealtimeRunError.SequenceMismatch => "다른 입력이 먼저 처리되었습니다. 다시 시도하세요.",
        RealtimeRunError.TimeInPast => "지난 시각에는 공사 입력을 적용할 수 없습니다.",
        RealtimeRunError.CommandLimit => "이 운영 기록에 더 많은 입력을 저장할 수 없습니다.",
        RealtimeRunError.ArithmeticOverflow => "비용이나 시각이 계산 범위를 벗어났습니다.",
        _ => throw new ArgumentOutOfRangeException(nameof(error)),
    };

    internal static string Time(long minute)
    {
        long day = checked(minute / (24 * 60) + 1);
        long minuteOfDay = minute % (24 * 60);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{day}일 {minuteOfDay / 60:00}:{minuteOfDay % 60:00}");
    }

    internal static string Clock(long minute)
    {
        long minuteOfDay = minute % (24 * 60);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}");
    }

    internal static string Countdown(long minutes)
    {
        if (minutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes));
        }
        long days = minutes / (24 * 60);
        long hours = minutes % (24 * 60) / 60;
        long remainderMinutes = minutes % 60;
        if (days > 0)
        {
            return hours > 0
                ? $"{days}일 {hours}시간 뒤"
                : $"{days}일 뒤";
        }
        if (hours > 0)
        {
            return remainderMinutes > 0
                ? $"{hours}시간 {remainderMinutes}분 뒤"
                : $"{hours}시간 뒤";
        }
        return $"{remainderMinutes}분 뒤";
    }

    internal static string Elapsed(long minutes)
    {
        if (minutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes));
        }
        long days = minutes / (24 * 60);
        long hours = minutes % (24 * 60) / 60;
        long remainderMinutes = minutes % 60;
        if (days > 0)
        {
            return hours > 0
                ? $"{days}일 {hours}시간"
                : $"{days}일";
        }
        if (hours > 0)
        {
            return remainderMinutes > 0
                ? $"{hours}시간 {remainderMinutes}분"
                : $"{hours}시간";
        }
        return $"{remainderMinutes}분";
    }

    internal static string Cash(long cashUnit)
    {
        long manWon = cashUnit / 10_000;
        long won = cashUnit % 10_000;
        if (manWon == 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{cashUnit:N0}원");
        }
        return won == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{manWon:N0}만 원")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{manWon:N0}만 {won:N0}원");
    }

    internal static string AssetDisplayName(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        string? assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return "공급 경로";
        }
        SpatialNodeDefinition? node = snapshot.Construction.World.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, assetId, StringComparison.Ordinal));
        if (node is not null)
        {
            return node.DisplayName;
        }
        SpatialEdgeDefinition? edge = snapshot.Construction.World.Edges.FirstOrDefault(item =>
            string.Equals(item.EdgeId, assetId, StringComparison.Ordinal));
        if (edge is not null)
        {
            string line = LineClassDisplayName(snapshot, edge.LineClassId);
            string from = snapshot.Construction.World.Nodes.FirstOrDefault(item =>
                string.Equals(item.NodeId, edge.FromNodeId, StringComparison.Ordinal))
                ?.DisplayName ?? "시작 설비";
            string to = snapshot.Construction.World.Nodes.FirstOrDefault(item =>
                string.Equals(item.NodeId, edge.ToNodeId, StringComparison.Ordinal))
                ?.DisplayName ?? "도착 설비";
            return $"{line} · {from}–{to}";
        }
        CommercialLoadDefinition? load = displayWorld.Loads.FirstOrDefault(item =>
            string.Equals(item.LoadId, assetId, StringComparison.Ordinal));
        if (load is not null)
        {
            return load.DisplayName;
        }
        CommercialSourceDefinition? source = displayWorld.Sources.FirstOrDefault(item =>
            string.Equals(item.SourceId, assetId, StringComparison.Ordinal));
        return source?.DisplayName ?? "미확인 설비";
    }

    internal static string LoadDisplayName(
        CommercialWorldDefinition displayWorld,
        string loadId) => displayWorld.Loads.FirstOrDefault(item =>
            string.Equals(item.LoadId, loadId, StringComparison.Ordinal))?.DisplayName ??
            "수요 시설";

    internal static string NodeClassDisplayName(
        RealtimeCampaignSnapshot snapshot,
        string classId) => snapshot.Construction.World.NodeClasses.FirstOrDefault(item =>
            string.Equals(item.ClassId, classId, StringComparison.Ordinal))?.DisplayName ??
            "접속 설비";

    internal static string LineClassDisplayName(
        RealtimeCampaignSnapshot snapshot,
        string classId) => snapshot.Construction.World.LineClasses.FirstOrDefault(item =>
            string.Equals(item.ClassId, classId, StringComparison.Ordinal))?.DisplayName ??
            "배전선";

    internal static string FailureSubjectName(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        ThermalSupplyFailure failure) => failure.AssetId is not null
        ? AssetDisplayName(displayWorld, snapshot, failure.AssetId)
        : failure.AttemptedSourceId is not null
            ? AssetDisplayName(displayWorld, snapshot, failure.AttemptedSourceId)
            : failure.Kind == ThermalFailureKind.NoEligibleSubstation
                ? "변전소 접속"
                : "공급 경로";

    internal static string FailureKindText(ThermalFailureKind kind) => kind switch
    {
        ThermalFailureKind.NoTopologyPath => "연결 경로 없음",
        ThermalFailureKind.NoEligibleSubstation => "적합한 변전소 없음",
        ThermalFailureKind.SourceCapacity => "발전 여력 부족",
        ThermalFailureKind.AssetUnavailable => "설비 사용 불가",
        ThermalFailureKind.ContinuousLimit => "연속 한계 초과",
        ThermalFailureKind.EmergencyLimit => "비상 한계 초과",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    internal static string TransitionKindText(RealtimeTransitionKind kind) => kind switch
    {
        RealtimeTransitionKind.ChapterStarted => "새 장 시작",
        RealtimeTransitionKind.ForecastRevealed => "사건 예보 공개",
        RealtimeTransitionKind.EventStarted => "사건 시작",
        RealtimeTransitionKind.EventCompleted => "사건 종료",
        RealtimeTransitionKind.ConstructionCompleted => "공사 완료",
        RealtimeTransitionKind.ThermalEmergencyEntered => "비상 운전 시작",
        RealtimeTransitionKind.ThermalEmergencyCleared => "연속 운전 복귀",
        RealtimeTransitionKind.ThermalProtectiveTrip => "보호정지",
        RealtimeTransitionKind.ThermalRecovered => "냉각 복귀",
        RealtimeTransitionKind.PromiseDefaulted => "운영 약속 자동 선택",
        RealtimeTransitionKind.ChapterCompleted => "장 운영 완료",
        RealtimeTransitionKind.CampaignCompleted => "캠페인 운영 완료",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    internal static string PlayerEventName(
        CommercialWorldDefinition displayWorld,
        CommercialOperatingPhaseDefinition profile)
    {
        if (!profile.DisplayName.Contains("병목 시험", StringComparison.Ordinal))
        {
            return profile.DisplayName;
        }
        string loadName = profile.Loads.Count == 0
            ? "도시"
            : LoadDisplayName(displayWorld, profile.Loads[0].LoadId);
        return $"{loadName} 전력 수요 증가";
    }

    internal static string ThermalTitle(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeThermalTransition transition)
    {
        string assetName = AssetDisplayName(displayWorld, snapshot, transition.AssetId);
        return transition.Kind switch
        {
            RealtimeThermalTransitionKind.EmergencyEntered =>
                $"{assetName} 비상 운전 시작",
            RealtimeThermalTransitionKind.EmergencyCleared =>
                $"{assetName} 연속 운전 복귀",
            RealtimeThermalTransitionKind.ProtectiveTrip =>
                $"{assetName} 보호정지",
            RealtimeThermalTransitionKind.Recovered =>
                $"{assetName} 냉각 복귀",
            _ => throw new ArgumentOutOfRangeException(nameof(transition)),
        };
    }

    internal static string JoinAssetNames(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        IReadOnlyList<string> ids) => ids.Count == 0
        ? "없음"
        : string.Join(", ", ids.Select(id =>
            AssetDisplayName(displayWorld, snapshot, id)));

    internal static string FirstBottleneck(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        ThermalIntervalEvaluation evaluation)
    {
        ThermalSupplyFailure? failure = evaluation.Loads
            .Select(item => item.Failure)
            .FirstOrDefault(item => item is not null);
        return failure is null
            ? "예상 병목 없음"
            : $"{FailureKindText(failure.Kind)} · " +
              $"{FailureSubjectName(displayWorld, snapshot, failure)} · " +
              $"필요 {failure.RequiredKw:N0} / 가능 {failure.AvailableKw:N0} kW";
    }

    internal static string ForecastRoutes(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        ThermalIntervalEvaluation evaluation)
    {
        if (evaluation.Loads.Count == 0)
        {
            return "예상된 수요 경로가 없습니다.";
        }
        return string.Join("\n", evaluation.Loads
            .OrderBy(item => item.LoadId, StringComparer.Ordinal)
            .Select(item =>
            {
                string path = item.PathNodeIds.Count == 0
                    ? "경로 없음"
                    : string.Join(" → ", item.PathNodeIds.Select(id =>
                        AssetDisplayName(displayWorld, snapshot, id))) +
                      $" ⇢ 반경 R 서비스 ⇢ {LoadDisplayName(displayWorld, item.LoadId)}";
                string supply = string.Create(CultureInfo.InvariantCulture,
                    $"{item.DeliveredKw:N0}/{item.DemandKw:N0} kW");
                string failure = item.Failure is null
                    ? ""
                    : $" · {FailureKindText(item.Failure.Kind)}" +
                      $" ({FailureSubjectName(displayWorld, snapshot, item.Failure)})";
                return $"{LoadDisplayName(displayWorld, item.LoadId)}: " +
                       $"{path} · {supply}{failure}";
            }));
    }

    internal static string ForecastDetail(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeForecastEvent forecast)
    {
        string intervals = string.Join(" · ", forecast.TemporalProjection.Intervals
            .Select(item => $"{Time(item.StartMinute)}–{Time(item.EndMinute)}"));
        string transitions = forecast.TemporalProjection.Transitions.Count == 0
            ? "열 전환 없음"
            : string.Join(" · ", forecast.TemporalProjection.Transitions.Select(item =>
                $"{Time(item.Minute)} {ThermalTitle(displayWorld, snapshot, item)}"));
        return $"예상 구간 {intervals}\n{transitions}";
    }

    internal static string ThermalAssetDisplayKind(
        RealtimeCampaignSnapshot snapshot,
        RealtimeThermalAssetSnapshot asset)
    {
        if (asset.AssetKind == ThermalAssetKind.Edge)
        {
            return "선로 도체";
        }
        SpatialNodeDefinition? node = snapshot.Construction.World.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, asset.AssetId, StringComparison.Ordinal));
        SpatialNodeClassDefinition? nodeClass = node is null
            ? null
            : snapshot.Construction.World.NodeClasses.FirstOrDefault(item =>
                string.Equals(item.ClassId, node.ClassId, StringComparison.Ordinal));
        return nodeClass?.Kind switch
        {
            SpatialNodeKind.Pole => "전신주 접속부",
            SpatialNodeKind.Substation => "변전소 주기기",
            _ => "접속 설비",
        };
    }

    internal static string TransitionHistory(
        IReadOnlyList<RealtimeTransition> transitionHistory,
        RealtimeThermalAssetSnapshot asset)
    {
        RealtimeTransition[] transitions = transitionHistory
            .Where(RealtimeThermalPresentation.IsThermalTransition)
            .Where(item => string.Equals(item.AssetId, asset.AssetId, StringComparison.Ordinal))
            .OrderBy(item => item.Minute)
            .ThenBy(item => item.Kind)
            .TakeLast(RealtimeTimelinePolicy.HistoryLimit)
            .ToArray();
        return transitions.Length == 0
            ? "현재 운영 기록에 설비 전환이 없습니다."
            : string.Join("\n", transitions.Select(item =>
                $"{Time(item.Minute)} · {TransitionKindText(item.Kind)}"));
    }

    internal static string BuildGuidance(
        RealtimeCampaignSnapshot snapshot,
        RealtimeInteractionState interaction,
        bool pointerAccepted,
        string pointerMessage)
    {
        if (snapshot.CampaignComplete ||
            interaction.Simulation == RealtimeSimulationState.Ended)
        {
            return RealtimeInteractionReducer.CampaignEndedReadOnlyReason;
        }
        if (interaction.Simulation == RealtimeSimulationState.AutoPaused &&
            interaction.PauseReason == RealtimePauseReason.CriticalIncident)
        {
            return "중대 사건 정지 · 설비와 공급 경로를 확인한 뒤 P 또는 상단 ▶로 계속하세요.";
        }
        RealtimeDraftToolLock? draftToolLock =
            RealtimeInteractionReducer.ResolveDraftToolLock(snapshot.Construction);
        if (draftToolLock is not null)
        {
            string feedback = string.IsNullOrWhiteSpace(pointerMessage)
                ? string.Empty
                : $"{(pointerAccepted ? "✓" : "!")} {pointerMessage} · ";
            return $"{feedback}초안 도구 잠금 · {draftToolLock.RejectionReason}";
        }
        if (!string.IsNullOrWhiteSpace(pointerMessage))
        {
            return $"{(pointerAccepted ? "✓" : "!")} {pointerMessage}";
        }
        string planningPause = IsFirstLight(snapshot) &&
            interaction.Simulation == RealtimeSimulationState.PlayerPaused
                ? "계획 정지 · "
                : string.Empty;
        ConstructionSnapshot construction = snapshot.Construction;
        if (construction.ActiveConstruction is ActiveConstructionSnapshot active)
        {
            return $"{planningPause}공사 완료 {Time(active.CompletionMinute)} · HUD 단계 확인";
        }
        if (construction.NodeDraft is not null)
        {
            return $"{planningPause}변전소 초안입니다. 발주를 확인하거나 우클릭으로 취소하세요.";
        }
        if (construction.LineDraft is LineDraftSnapshot line)
        {
            return planningPause + (line.EndNodeId is null
                ? "선로 경로를 이어 주세요. Backspace는 마지막 점, 우클릭은 현재 단계를 되돌립니다."
                : "선로 경로가 닫혔습니다. 발주하거나 Backspace로 끝점을 되돌리세요.");
        }
        if (interaction.Tool == RealtimeTool.Analysis)
        {
            return "망 분석 켜짐 · 예상 공급 경로와 첫 병목을 지도에서 확인합니다.";
        }
        if (IsFirstLight(snapshot))
        {
            return planningPause + FirstLightNextStep(snapshot);
        }
        return interaction.Tool switch
        {
            RealtimeTool.BuildNode => "지도에서 변전소 위치를 선택하세요.",
            RealtimeTool.BuildLine => "기존 설비에서 선로 시작점을 선택하세요.",
            _ => "설비나 사건을 선택해 상태와 다음 행동을 확인하세요.",
        };
    }

    internal static string FirstLightObjective(RealtimeCampaignSnapshot snapshot)
    {
        string next = FirstLightNextStep(snapshot);
        return IsFirstLight(snapshot)
            ? next.Replace("다음 행동 · ", string.Empty, StringComparison.Ordinal)
            : snapshot.Chapter.Content.Objective;
    }

    internal static string FirstLightNextStep(RealtimeCampaignSnapshot snapshot)
    {
        if (!IsFirstLight(snapshot))
        {
            return string.Empty;
        }
        SpatialNodeDefinition? substation = FirstLightSubstation(snapshot);
        if (substation is null)
        {
            return "다음 행동 · 1/2 N · 변전소 R550 안에 동부 생활권 포함";
        }
        if (!Connected(
                snapshot.Construction.World,
                "WEST_SOURCE_NODE",
                substation.NodeId))
        {
            return "다음 행동 · 2/2 L · 서부 발전소→변전소 급전선 연결";
        }
        return "경로 준비 완료 · 21:00 동부 첫 공급까지 유지하세요.";
    }

    internal static bool FirstLightRouteReady(RealtimeCampaignSnapshot snapshot)
    {
        SpatialNodeDefinition? substation = FirstLightSubstation(snapshot);
        return IsFirstLight(snapshot) &&
            substation is not null &&
            Connected(snapshot.Construction.World, "WEST_SOURCE_NODE", substation.NodeId);
    }

    internal static string SpanDetail(long? length, int? maximum) =>
        length.HasValue && maximum.HasValue
            ? $"경간 {length.Value:N0} / 허용 {maximum.Value:N0}"
            : string.Empty;

    internal static string FirstLightFailureDebrief(
        CommercialWorldDefinition displayWorld,
        RealtimeCampaignSnapshot snapshot,
        RealtimeChapterOutcome outcome)
    {
        RealtimeEventOutcome[] failed = outcome.Events
            .Where(item => !item.SafetySatisfied)
            .ToArray();
        int safeEvents = outcome.Events.Count(item => item.SafetySatisfied);
        long unservedMinutes = failed.Sum(item => item.SafetyUnservedMinutes);
        ThermalSupplyFailure? failure = failed
            .SelectMany(item => item.FinalEvaluation.Loads)
            .Select(item => item.Failure)
            .FirstOrDefault(item => item is not null);
        SpatialNodeDefinition? substation = FirstLightSubstation(snapshot);
        bool nodeReady = substation is not null;
        bool sourceReady = nodeReady && Connected(
            snapshot.Construction.World,
            "WEST_SOURCE_NODE",
            substation!.NodeId);
        int progress = (nodeReady ? 1 : 0) + (sourceReady ? 1 : 0);
        bool completedLate = nodeReady && sourceReady &&
            failed.Any(item => item.SafetyUnservedMinutes > 0);
        long firstTestMinute = failed
            .Select(item => item.StartMinute)
            .DefaultIfEmpty(outcome.StartMinute)
            .Min();
        string progressFacts =
            $"진행 {progress}/2 · 변전소 {(nodeReady ? "완공" : "미완료")} · " +
            $"상류 연결 {(sourceReady ? "완료" : "미완료")}";
        string cause = completedLate
            ? $"원인 · {Clock(firstTestMinute)} 시험 시작 뒤 경로 완공 · 준비 지연"
            : failure is null || failure.Kind == ThermalFailureKind.NoTopologyPath
                ? "원인 · 시험 시작 시점에 완공된 공급 경로 없음"
            : $"원인 · {FailureKindText(failure.Kind)} · " +
              FailureSubjectName(displayWorld, snapshot, failure);
        string retry = completedLate
            ? $"{Clock(firstTestMinute)} 시험 시작 전에 2/2 상류 공급 경로를 완공하세요."
            : !nodeReady
            ? "동부 생활권이 반경 R 안에 들도록 변전소를 완공하세요."
            : "서부 발전 접속점에서 완공한 변전소까지만 선로를 이으세요.";
        return $"안전 의무 {safeEvents}/{outcome.Events.Count} 충족 · " +
               $"동부 생활권 {unservedMinutes}분 미공급\n{cause}\n" +
               $"{progressFacts}\n다음 시도 · {retry}\n" +
               $"최종 운영 자금 {Cash(outcome.EndingCashUnit)}";
    }

    internal static string FirstLightSuccessDebrief(
        RealtimeChapterOutcome outcome,
        string authoredBody)
    {
        int safeEvents = outcome.Events.Count(item => item.SafetySatisfied);
        long unservedMinutes = outcome.Events.Sum(item => item.SafetyUnservedMinutes);
        return authoredBody +
            $"\n\n✓ 첫 공급 성공 · 안전 의무 {safeEvents}/{outcome.Events.Count} · " +
            $"미공급 {unservedMinutes}분\n" +
            "완공 경로 · 서부 발전 접속점 → 변전소 · 반경 R 서비스 → 동부 생활권\n" +
            $"남은 운영 자금 {Cash(outcome.EndingCashUnit)}";
    }

    private static bool IsFirstLight(RealtimeCampaignSnapshot snapshot) =>
        string.Equals(
            snapshot.Chapter.Content.ChapterId,
            "FIRST_LIGHT",
            StringComparison.Ordinal);

    internal static SpatialNodeDefinition? FirstLightSubstation(
        RealtimeCampaignSnapshot snapshot) => snapshot.Construction.World.Nodes
        .Where(item => item.Commissioned)
        .FirstOrDefault(item => snapshot.Construction.World.NodeClasses.Any(nodeClass =>
            string.Equals(nodeClass.ClassId, item.ClassId, StringComparison.Ordinal) &&
            nodeClass.Kind == SpatialNodeKind.Substation));

    internal static bool Connected(
        SpatialWorldDefinition world,
        string startNodeId,
        string endNodeId)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { startNodeId };
        var queue = new Queue<string>();
        queue.Enqueue(startNodeId);
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            if (string.Equals(current, endNodeId, StringComparison.Ordinal))
            {
                return true;
            }
            foreach (SpatialEdgeDefinition edge in world.Edges.Where(item =>
                         item.Commissioned &&
                         (string.Equals(item.FromNodeId, current, StringComparison.Ordinal) ||
                          string.Equals(item.ToNodeId, current, StringComparison.Ordinal))))
            {
                string next = string.Equals(
                        edge.FromNodeId,
                        current,
                        StringComparison.Ordinal)
                    ? edge.ToNodeId
                    : edge.FromNodeId;
                if (visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }
        return false;
    }

    internal static string DisabledConstructionReason(ActiveConstructionSnapshot active) =>
        $"현재 공사가 {Time(active.CompletionMinute)}에 끝난 뒤 발주할 수 있습니다.";

    internal static string OrderQuoteDetail(
        RealtimeCampaignSnapshot snapshot,
        RealtimeProjectQuote? quote,
        ActiveConstructionSnapshot? active)
    {
        if (quote is null)
        {
            return "발주 견적을 확인하지 못해 공사를 시작할 수 없습니다.";
        }
        if (!quote.Accepted)
        {
            string rejection = active is not null
                ? DisabledConstructionReason(active)
                : quote.ConstructionError.HasValue
                    ? ConstructionErrorText(quote.ConstructionError)
                    : RealtimeRunErrorText(quote.Error);
            return $"발주 불가 · {rejection}";
        }
        if (quote is not
            {
                CostCashUnit: long cost,
                BuildMinutes: long buildMinutes,
                CompletionMinute: long completionMinute,
            })
        {
            return "발주 견적 값이 완전하지 않아 공사를 시작할 수 없습니다.";
        }
        string exactQuote =
            $"발주 견적 · 비용 {Cash(cost)} · 공기 {buildMinutes}분 · " +
            $"{Time(completionMinute)} 완공";
        RealtimeScheduledEventDefinition? nextEvent = snapshot.Chapter.ScheduledEvents
            .Where(item => checked(
                snapshot.ChapterStartMinute + item.StartOffsetMinutes) > snapshot.Minute)
            .OrderBy(item => item.StartOffsetMinutes)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (nextEvent is not null)
        {
            long eventStart = checked(
                snapshot.ChapterStartMinute + nextEvent.StartOffsetMinutes);
            if (completionMinute > eventStart)
            {
                exactQuote +=
                    $"\n시험 일정 경고 · {nextEvent.OperatingProfile.DisplayName} 시작 " +
                    $"{Time(eventStart)} 이후 완공 · " +
                    $"{Elapsed(completionMinute - eventStart)} 늦음";
            }
        }
        return cost <= snapshot.CashUnit
            ? exactQuote
            : $"{exactQuote}\n발주 불가 · 운영 자금이 {Cash(cost - snapshot.CashUnit)} 부족합니다.";
    }

    internal static string FeedbackDetail(
        bool accepted,
        string message,
        string fallback) => string.IsNullOrWhiteSpace(message)
        ? fallback
        : $"{(accepted ? "✓" : "!")} {message}\n{fallback}";
}
