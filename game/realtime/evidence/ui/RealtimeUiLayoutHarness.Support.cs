#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.R2;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.UI;

internal sealed partial class RealtimeUiLayoutHarness : Control
{
    private async Task SettleLayout()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task<bool> SettleUntil(
        Func<bool> condition,
        int maximumFrames = 30)
    {
        ArgumentNullException.ThrowIfNull(condition);
        for (int frame = 0; frame < maximumFrames; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (condition())
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                return true;
            }
        }
        return condition();
    }

    private async Task<Rect2> SettleStableRect(
        Func<Rect2> capture,
        int maximumFrames = 30)
    {
        ArgumentNullException.ThrowIfNull(capture);
        Rect2 previous = capture();
        int stableFrames = 0;
        for (int frame = 0; frame < maximumFrames; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Rect2 current = capture();
            if (current.Position.IsEqualApprox(previous.Position) &&
                current.Size.IsEqualApprox(previous.Size))
            {
                stableFrames++;
                if (stableFrames >= 3)
                {
                    return current;
                }
            }
            else
            {
                stableFrames = 0;
            }
            previous = current;
        }
        return capture();
    }

    private void FinishAndQuit(int exitCode)
    {
        SceneTree tree = GetTree();
        ScheduleQuitAfterCleanup(tree, exitCode);
        if (ReferenceEquals(tree.CurrentScene, this))
        {
            tree.CurrentScene = null;
        }
        if (GetParent() is Node parent)
        {
            parent.RemoveChild(this);
        }
        Free();
    }

    private static void ScheduleQuitAfterCleanup(SceneTree tree, int exitCode)
    {
        int remainingFrames = 3;
        void DrainAndQuit()
        {
            remainingFrames--;
            if (remainingFrames > 0)
            {
                return;
            }
            tree.ProcessFrame -= DrainAndQuit;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            tree.Quit(exitCode);
        }
        tree.ProcessFrame += DrainAndQuit;
    }

    private static void RemoveAndFree(Node node)
    {
        if (!GodotObject.IsInstanceValid(node))
        {
            return;
        }
        node.GetParent()?.RemoveChild(node);
        node.Free();
    }

    private static bool RectApproximatelyEqual(Rect2 left, Rect2 right) =>
        left.Position.DistanceTo(right.Position) <= 1f &&
        left.Size.DistanceTo(right.Size) <= 1f;

    private static void Require(
        bool condition,
        string message,
        ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }
}
#endif
