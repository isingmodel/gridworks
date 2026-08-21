using System;
using System.Collections.Generic;

namespace Gridworks.Game;

internal sealed record CommercialLaunchOptions(
    bool PlacementSmoke,
    bool ThermalSmoke,
    bool CoreSmoke)
{
    public static CommercialLaunchOptions Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        bool placementSmoke = false;
        bool thermalSmoke = false;
        bool coreSmoke = false;
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
                case "--commercial-core-smoke" when !coreSmoke:
                    coreSmoke = true;
                    break;
                case "--commercial-core-smoke":
                    throw new ArgumentException(
                        "상용 핵심 흐름 확인 인자는 한 번만 사용할 수 있습니다.");
#endif
                default:
                    throw new ArgumentException($"지원하지 않는 상용 게임 실행 인자입니다: {argument}");
            }
        }
        if ((placementSmoke ? 1 : 0) + (thermalSmoke ? 1 : 0) + (coreSmoke ? 1 : 0) > 1)
        {
            throw new ArgumentException("상용 smoke 인자는 한 번에 하나만 사용하세요.");
        }
        return new CommercialLaunchOptions(placementSmoke, thermalSmoke, coreSmoke);
    }
}
