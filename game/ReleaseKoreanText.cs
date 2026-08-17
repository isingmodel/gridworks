using System;
using System.Collections.Generic;
using Gridworks.Core.Release;

namespace Gridworks.Game;

internal static class ReleaseKoreanText
{
    public static string Phase(ReleaseConstructionPhase phase) => phase switch
    {
        ReleaseConstructionPhase.Ready => "전력망 살펴보기",
        ReleaseConstructionPhase.NodeDrafting => "변전소 계획하기",
        ReleaseConstructionPhase.NodeBuilding => "변전소 공사 중",
        ReleaseConstructionPhase.LineDrafting => "선로 계획하기",
        ReleaseConstructionPhase.LineBuilding => "선로 공사 중",
        _ => "현재 작업",
    };

    public static string ConstructionError(ReleaseConstructionError? error) => error switch
    {
        null => string.Empty,
        ReleaseConstructionError.WrongPhase =>
            "지금은 이 작업을 할 수 없습니다. 진행 중인 계획이나 공사를 먼저 마치세요.",
        ReleaseConstructionError.UnknownClass =>
            "선택한 설비 종류를 찾을 수 없습니다. 다른 설비를 선택하세요.",
        ReleaseConstructionError.InvalidNodeClass =>
            "이 도구로는 해당 설비를 배치할 수 없습니다. 변전소 종류를 선택하세요.",
        ReleaseConstructionError.InvalidLineClass =>
            "선택한 선로 종류를 사용할 수 없습니다. 다른 선로를 선택하세요.",
        ReleaseConstructionError.InvalidPoleClass =>
            "선택한 선로를 사용할 수 없습니다. 다른 선로를 선택하세요.",
        ReleaseConstructionError.OutsideGrid =>
            "공사 구역 밖입니다. 지도 안의 격자를 선택하세요.",
        ReleaseConstructionError.PositionOccupied =>
            "이미 설비가 있거나 이번 계획에서 사용한 자리입니다. 빈 격자를 선택하세요.",
        ReleaseConstructionError.EndpointNotFound =>
            "선로를 시작할 접속점을 찾을 수 없습니다. 완공된 설비를 선택하세요.",
        ReleaseConstructionError.EndpointNotCommissioned =>
            "아직 완공되지 않은 설비에는 선로를 연결할 수 없습니다.",
        ReleaseConstructionError.SameEndpoint =>
            "선로를 시작한 설비로 되돌아갈 수 없습니다. 다른 완공 설비를 선택하세요.",
        ReleaseConstructionError.ConnectionLimit =>
            "이 설비에는 새 선로를 연결할 여유가 없습니다. 남은 연결이 있는 설비를 선택하세요.",
        ReleaseConstructionError.SpanTooLong =>
            "선택한 두 지점 사이가 너무 멉니다. 사이에 전신주를 하나 더 배치하세요.",
        ReleaseConstructionError.DuplicateSegment =>
            "두 설비 사이는 이미 직접 연결돼 있습니다. 다른 경로를 선택하세요.",
        ReleaseConstructionError.DraftIncomplete =>
            "선로가 아직 다른 완공 설비에 닿지 않았습니다. 선로를 연결할 끝 설비를 선택하세요.",
        ReleaseConstructionError.NothingToUndo =>
            "되돌릴 선택이 없습니다.",
        ReleaseConstructionError.ArithmeticOverflow =>
            "이 계획의 공사비와 기간을 계산할 수 없습니다. 선로를 여러 짧은 구간으로 나눠 계획하세요.",
        _ => "작업을 마칠 수 없습니다. 계획을 다시 확인하세요.",
    };

    public static string SupplyFailure(
        string loadName,
        ReleaseSupplyFailure failure,
        string? assetName,
        bool duringMissionSituation = false)
    {
        string circumstance = duringMissionSituation ? "임무 상황에서는 " : string.Empty;
        string impact = loadName.EndsWith("전력", StringComparison.Ordinal)
            ? $"{circumstance}{loadName} 공급이 끊겼습니다."
            : $"{circumstance}{loadName}에 전력이 공급되지 않습니다.";
        string success = loadName.EndsWith("전력", StringComparison.Ordinal)
            ? $"{loadName}이 정상적으로 공급되고 있습니다."
            : $"{loadName}에 필요한 전력이 공급되고 있습니다.";
        string asset = string.IsNullOrWhiteSpace(assetName) ? "해당 설비" : assetName;
        return failure.Kind switch
        {
            ReleaseSupplyFailureKind.None =>
                success,
            ReleaseSupplyFailureKind.NoEligibleSubstation =>
                $"{impact} 연결 범위 안에 가동 중인 변전소가 없습니다. " +
                "가까운 변전소를 전력망에 연결하세요.",
            ReleaseSupplyFailureKind.Disconnected =>
                $"{impact} 발전 설비부터 이곳까지 이어진 선로가 없습니다. " +
                "끊긴 구간을 연결하거나 우회선을 추가하세요.",
            ReleaseSupplyFailureKind.SourceCapacity =>
                $"{impact} {asset}의 여유 용량이 {FormatPower(failure.ShortfallKw)} 부족합니다. " +
                "다른 발전 경로를 연결해 전력을 나누세요.",
            ReleaseSupplyFailureKind.EdgeCapacity =>
                $"{impact} {asset}의 정격 용량을 {FormatPower(failure.ShortfallKw)} 초과했습니다. " +
                "다른 선로를 추가해 전력 흐름을 나누세요.",
            ReleaseSupplyFailureKind.NodeCapacity =>
                $"{impact} {asset}의 정격 용량을 {FormatPower(failure.ShortfallKw)} 초과했습니다. " +
                "다른 분기 경로를 연결해 전력 흐름을 나누세요.",
            ReleaseSupplyFailureKind.TransformerCapacity =>
                $"{impact} {asset}의 변압기 여유 용량이 {FormatPower(failure.ShortfallKw)} 부족합니다. " +
                "다른 변전소로 수요를 나누거나 연결을 보강하세요.",
            _ =>
                $"{impact} 공급 경로와 설비 정격 용량을 확인한 뒤 전력망을 보강하세요.",
        };
    }

    public static string Capacity(long usedKw, long ratingKw)
    {
        long headroomKw = ratingKw - usedKw;
        string headroom = headroomKw >= 0
            ? $"여유 용량 {FormatPower(headroomKw)}"
            : $"정격 용량 초과 {FormatPower(-headroomKw)}";
        return $"부하 {FormatPower(usedKw)} · 정격 용량 {FormatPower(ratingKw)} · {headroom}";
    }

    public static string Connections(int count, int maximum) =>
        $"접속 {count}/{maximum}회선 · {maximum - count}회선 여유";

    public static string ConnectionRequirement(
        string nodeName,
        int actualConnections,
        int requiredConnections) =>
        $"수요처에 안정적으로 전력을 보내려면 접속 회선을 더 확보해야 합니다. {nodeName}에는 현재 " +
        $"{actualConnections}회선이 연결돼 있으며 {requiredConnections}회선이 필요합니다. " +
        "다른 완공 설비로 이어지는 선로를 추가하세요.";

    public static string FormatPower(long kilowatts) => kilowatts % 1_000 == 0
        ? $"{kilowatts / 1_000} MW"
        : $"{kilowatts / 1_000d:0.0} MW";

    public static string FormatCash(long cashUnit)
    {
        decimal tenThousands = cashUnit / 10_000m;
        return tenThousands == decimal.Truncate(tenThousands)
            ? $"{tenThousands:0}만 원"
            : $"{tenThousands:0.#}만 원";
    }

    public static string FormatDuration(long minutes)
    {
        long days = minutes / 1_440;
        long hours = (minutes % 1_440) / 60;
        long remainder = minutes % 60;
        var parts = new List<string>(3);
        if (days > 0)
        {
            parts.Add($"{days}일");
        }
        if (hours > 0)
        {
            parts.Add($"{hours}시간");
        }
        if (remainder > 0 || parts.Count == 0)
        {
            parts.Add($"{remainder}분");
        }
        return string.Join(" ", parts);
    }

    public static string FormatClock(long minute)
    {
        long day = (minute / 1_440) + 1;
        long minuteOfDay = minute % 1_440;
        return $"{day}일차 {minuteOfDay / 60:00}:{minuteOfDay % 60:00}";
    }

    public static string NodeKind(ReleaseNodeKind kind) => kind switch
    {
        ReleaseNodeKind.SourceTerminal => "발전 접속점",
        ReleaseNodeKind.Pole => "전신주",
        ReleaseNodeKind.Substation => "배전 변전소",
        ReleaseNodeKind.DedicatedLoadTerminal => "전용 수요 접속점",
        _ => "전력 설비",
    };
}
