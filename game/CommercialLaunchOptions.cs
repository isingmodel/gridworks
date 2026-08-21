using System;
using System.Collections.Generic;

namespace Gridworks.Game;

internal sealed record CommercialLaunchOptions(
    bool PlacementSmoke,
    bool ThermalSmoke,
    bool CampaignSmoke,
    bool CampaignCheckpointSmoke,
    bool CampaignCompletionSmoke,
    bool CampaignCompletedResumeSmoke)
{
    public bool AnyCampaignSmoke => CampaignSmoke || CampaignCheckpointSmoke ||
        CampaignCompletionSmoke || CampaignCompletedResumeSmoke;

    public bool AnySmoke => PlacementSmoke || ThermalSmoke || AnyCampaignSmoke;

    public bool StartsFreshCampaign => CampaignSmoke || CampaignCheckpointSmoke;

    public static CommercialLaunchOptions Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        bool placementSmoke = false;
        bool thermalSmoke = false;
        bool campaignSmoke = false;
        bool campaignCheckpointSmoke = false;
        bool campaignCompletionSmoke = false;
        bool campaignCompletedResumeSmoke = false;
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
                        "열 국면 확인 인자는 한 번만 사용할 수 있습니다.");
                case "--commercial-campaign-smoke" when !campaignSmoke:
                    campaignSmoke = true;
                    break;
                case "--commercial-campaign-smoke":
                    throw new ArgumentException(
                        "상용 캠페인 확인 인자는 한 번만 사용할 수 있습니다.");
                case "--commercial-campaign-stage-f-checkpoint-smoke" when !campaignCheckpointSmoke:
                    campaignCheckpointSmoke = true;
                    break;
                case "--commercial-campaign-stage-f-checkpoint-smoke":
                    throw new ArgumentException(
                        "상용 캠페인 체크포인트 확인 인자는 한 번만 사용할 수 있습니다.");
                case "--commercial-campaign-stage-f-completion-smoke" when !campaignCompletionSmoke:
                    campaignCompletionSmoke = true;
                    break;
                case "--commercial-campaign-stage-f-completion-smoke":
                    throw new ArgumentException(
                        "상용 캠페인 완주 확인 인자는 한 번만 사용할 수 있습니다.");
                case "--commercial-campaign-stage-f-completed-resume-smoke" when !campaignCompletedResumeSmoke:
                    campaignCompletedResumeSmoke = true;
                    break;
                case "--commercial-campaign-stage-f-completed-resume-smoke":
                    throw new ArgumentException(
                        "완료 저장 재개 확인 인자는 한 번만 사용할 수 있습니다.");
#endif
                default:
                    throw new ArgumentException($"지원하지 않는 상용 게임 실행 인자입니다: {argument}");
            }
        }
        if ((placementSmoke ? 1 : 0) + (thermalSmoke ? 1 : 0) +
            (campaignSmoke ? 1 : 0) + (campaignCheckpointSmoke ? 1 : 0) +
            (campaignCompletionSmoke ? 1 : 0) +
            (campaignCompletedResumeSmoke ? 1 : 0) > 1)
        {
            throw new ArgumentException("상용 smoke 인자는 한 번에 하나만 사용하세요.");
        }
        return new CommercialLaunchOptions(
            placementSmoke,
            thermalSmoke,
            campaignSmoke,
            campaignCheckpointSmoke,
            campaignCompletionSmoke,
            campaignCompletedResumeSmoke);
    }
}
