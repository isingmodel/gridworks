using System;
using System.Collections.Generic;
using Gridworks.Core.Release;

namespace Gridworks.Game;

internal static class ReleaseKoreanText
{
    public static string Phase(ReleaseConstructionPhase phase) => phase switch
    {
        ReleaseConstructionPhase.Ready => "망 살펴보기",
        ReleaseConstructionPhase.NodeDrafting => "변전소 계획",
        ReleaseConstructionPhase.NodeBuilding => "변전소 공사",
        ReleaseConstructionPhase.LineDrafting => "선로 계획",
        ReleaseConstructionPhase.LineBuilding => "선로 공사",
        _ => "현재 작업",
    };

    public static string ConstructionError(ReleaseConstructionError? error) => error switch
    {
        null => string.Empty,
        ReleaseConstructionError.WrongPhase =>
            "지금은 이 작업을 할 수 없습니다. 진행 중인 계획이나 공사를 먼저 마치세요.",
        ReleaseConstructionError.UnknownClass =>
            "선택한 설비 형식을 찾을 수 없습니다. 다른 설비를 선택하세요.",
        ReleaseConstructionError.InvalidNodeClass =>
            "이 도구로는 해당 설비를 놓을 수 없습니다. 변전소 형식을 선택하세요.",
        ReleaseConstructionError.InvalidLineClass =>
            "선택한 선로 형식을 사용할 수 없습니다. 다른 선로를 선택하세요.",
        ReleaseConstructionError.InvalidPoleClass =>
            "이 선로에 맞는 전신주 형식이 아닙니다. 다른 선로를 선택하세요.",
        ReleaseConstructionError.OutsideGrid =>
            "공사 구역 밖입니다. 지도 안의 격자점을 선택하세요.",
        ReleaseConstructionError.PositionOccupied =>
            "이미 설비가 있거나 이번 계획에서 사용한 자리입니다. 빈 격자점을 선택하세요.",
        ReleaseConstructionError.EndpointNotFound =>
            "선로를 시작할 접속점을 찾을 수 없습니다. 완공된 설비를 선택하세요.",
        ReleaseConstructionError.EndpointNotCommissioned =>
            "아직 완공되지 않은 설비에는 선로를 연결할 수 없습니다.",
        ReleaseConstructionError.SameEndpoint =>
            "선로를 시작한 설비로 되돌아갈 수 없습니다. 다른 완공 설비를 선택하세요.",
        ReleaseConstructionError.ConnectionLimit =>
            "이 설비에는 새 선로를 연결할 여유가 없습니다. 접속 여유가 있는 설비를 선택하세요.",
        ReleaseConstructionError.SpanTooLong =>
            "두 접속점 사이가 너무 멉니다. 사이에 전신주를 하나 더 놓으세요.",
        ReleaseConstructionError.DuplicateSegment =>
            "두 설비 사이는 이미 직접 연결돼 있습니다. 다른 경로를 선택하세요.",
        ReleaseConstructionError.DraftIncomplete =>
            "선로가 아직 다른 완공 설비에 닿지 않았습니다. 끝 접속점을 선택하세요.",
        ReleaseConstructionError.NothingToUndo =>
            "되돌릴 전신주가 없습니다.",
        ReleaseConstructionError.ArithmeticOverflow =>
            "이 계획의 견적을 계산할 수 없습니다. 더 짧은 계획으로 나누세요.",
        _ => "작업을 마칠 수 없습니다. 계획을 다시 확인하세요.",
    };

    public static string SupplyFailure(ReleaseSupplyFailure failure, string? assetName) =>
        failure.Kind switch
        {
            ReleaseSupplyFailureKind.None => "공급 경로가 정상입니다.",
            ReleaseSupplyFailureKind.NoEligibleSubstation =>
                "서비스 구역 안에 사용 가능한 변전소가 없습니다.",
            ReleaseSupplyFailureKind.Disconnected =>
                "발전 접속점에서 수요처까지 이어진 경로가 없습니다.",
            ReleaseSupplyFailureKind.SourceCapacity =>
                $"{NameOrAsset(assetName)}의 공급 여유가 {FormatPower(failure.ShortfallKw)} 부족합니다.",
            ReleaseSupplyFailureKind.EdgeCapacity =>
                $"{NameOrAsset(assetName)}의 선로 여유가 {FormatPower(failure.ShortfallKw)} 부족합니다.",
            ReleaseSupplyFailureKind.NodeCapacity =>
                $"{NameOrAsset(assetName)}의 통과 여유가 {FormatPower(failure.ShortfallKw)} 부족합니다.",
            ReleaseSupplyFailureKind.TransformerCapacity =>
                $"{NameOrAsset(assetName)}의 변압 여유가 {FormatPower(failure.ShortfallKw)} 부족합니다.",
            _ => "공급할 수 없는 이유를 확인하세요.",
        };

    public static string FormatPower(long kilowatts) => kilowatts % 1_000 == 0
        ? $"{kilowatts / 1_000} MW"
        : $"{kilowatts / 1_000d:0.0} MW";

    public static string FormatCash(long cashUnit)
    {
        decimal tenThousands = cashUnit / 10_000m;
        return tenThousands == decimal.Truncate(tenThousands)
            ? $"{tenThousands:0}만"
            : $"{tenThousands:0.#}만";
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

    private static string NameOrAsset(string? assetName) =>
        string.IsNullOrWhiteSpace(assetName) ? "선택한 설비" : assetName;
}
