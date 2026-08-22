#if DEBUG
using System;
using System.Linq;
using Gridworks.Game.Realtime.UI;

namespace Gridworks.Game.Realtime.R2
{
    internal sealed record RealtimeSliceCheckpointHudRenderFact(
        string ClockText,
        RealtimeSimulationSpeed PressedSpeed);
}

namespace Gridworks.Game.Realtime.UI
{
    internal sealed partial class RealtimeTopHud
    {
        internal global::Gridworks.Game.Realtime.R2.RealtimeSliceCheckpointHudRenderFact
            CaptureTargetedCheckpointHudFact()
        {
            RealtimeSimulationSpeed[] pressed = _speedButtons
                .Where(item => item.Value.ButtonPressed)
                .Select(item => item.Key)
                .ToArray();
            if (pressed.Length != 1)
            {
                throw new InvalidOperationException(
                    "The actual HUD does not expose exactly one pressed speed.");
            }
            return new global::Gridworks.Game.Realtime.R2
                .RealtimeSliceCheckpointHudRenderFact(_clock.Text, pressed[0]);
        }
    }
}
#endif
