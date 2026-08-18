using System;
using System.Collections.Generic;

namespace Gridworks.Game;

internal sealed record CommercialLaunchOptions(bool PlacementSmoke)
{
    public static CommercialLaunchOptions Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        bool placementSmoke = false;
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
#endif
                default:
                    throw new ArgumentException($"지원하지 않는 상용 게임 실행 인자입니다: {argument}");
            }
        }
        return new CommercialLaunchOptions(placementSmoke);
    }
}
