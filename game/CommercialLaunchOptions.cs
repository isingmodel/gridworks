using System;
using System.Collections.Generic;

namespace Gridworks.Game;

internal enum CommercialCampaignSmokeLeg
{
    None,
    First,
    Second,
}

internal sealed record CommercialLaunchOptions(
    bool PlacementSmoke,
    bool ThermalSmoke,
    CommercialCampaignSmokeLeg CampaignSmokeLeg,
    string? SmokeSavePath)
{
    public static CommercialLaunchOptions Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        bool placementSmoke = false;
        bool thermalSmoke = false;
        CommercialCampaignSmokeLeg campaignSmokeLeg = CommercialCampaignSmokeLeg.None;
        string? smokeSavePath = null;
        foreach (string argument in arguments)
        {
            switch (argument)
            {
#if DEBUG
                case "--commercial-placement-smoke" when !placementSmoke:
                    placementSmoke = true;
                    break;
                case "--commercial-placement-smoke":
                    throw new ArgumentException(
                        "자유 배치 확인 인자는 한 번만 사용할 수 있습니다.");
                case "--commercial-thermal-smoke" when !thermalSmoke:
                    thermalSmoke = true;
                    break;
                case "--commercial-thermal-smoke":
                    throw new ArgumentException(
                        "열 운전 확인 인자는 한 번만 사용할 수 있습니다.");
                case "--commercial-campaign-smoke=first" when
                    campaignSmokeLeg == CommercialCampaignSmokeLeg.None:
                    campaignSmokeLeg = CommercialCampaignSmokeLeg.First;
                    break;
                case "--commercial-campaign-smoke=second" when
                    campaignSmokeLeg == CommercialCampaignSmokeLeg.None:
                    campaignSmokeLeg = CommercialCampaignSmokeLeg.Second;
                    break;
                case "--commercial-campaign-smoke=first" or
                    "--commercial-campaign-smoke=second":
                    throw new ArgumentException(
                        "상용 캠페인 확인 단계는 한 번만 지정할 수 있습니다.");
                case string value when value.StartsWith(
                    "--commercial-smoke-save-path=",
                    StringComparison.Ordinal):
                    if (smokeSavePath is not null)
                    {
                        throw new ArgumentException(
                            "상용 캠페인 확인 저장 경로는 한 번만 지정할 수 있습니다.");
                    }
                    smokeSavePath = value["--commercial-smoke-save-path=".Length..];
                    if (!System.IO.Path.IsPathFullyQualified(smokeSavePath))
                    {
                        throw new ArgumentException(
                            "상용 캠페인 확인 저장 경로는 절대경로여야 합니다.");
                    }
                    break;
#endif
                default:
                    throw new ArgumentException($"지원하지 않는 상용 게임 실행 인자입니다: {argument}");
            }
        }
        int smokeCount = (placementSmoke ? 1 : 0) + (thermalSmoke ? 1 : 0) +
            (campaignSmokeLeg == CommercialCampaignSmokeLeg.None ? 0 : 1);
        if (smokeCount > 1)
        {
            throw new ArgumentException("상용 게임 확인 흐름은 한 번에 하나만 실행할 수 있습니다.");
        }
        if ((campaignSmokeLeg == CommercialCampaignSmokeLeg.None) != (smokeSavePath is null))
        {
            throw new ArgumentException(
                "상용 캠페인 확인에는 전용 절대 저장 경로와 실행 단계가 함께 필요합니다.");
        }
        return new CommercialLaunchOptions(
            placementSmoke,
            thermalSmoke,
            campaignSmokeLeg,
            smokeSavePath);
    }
}
