using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Gridworks.Game.Realtime.UI;

internal sealed partial class RealtimeFocusScope : Control
{
    private Control? _returnFocus;

    public void Activate(Control? preferredFocus = null, Control? returnFocus = null)
    {
        _returnFocus = returnFocus ?? GetViewport().GuiGetFocusOwner();
        IReadOnlyList<Control> controls = FocusableControls();
        WireCycle(controls);
        Control? target = preferredFocus is not null && IsFocusable(preferredFocus)
            ? preferredFocus
            : controls.FirstOrDefault();
        target?.CallDeferred(Control.MethodName.GrabFocus);
    }

    public void Refresh()
    {
        WireCycle(FocusableControls());
    }

    public void Deactivate(bool restoreFocus = true)
    {
        Control? target = restoreFocus && IsFocusable(_returnFocus) ? _returnFocus : null;
        _returnFocus = null;
        target?.CallDeferred(Control.MethodName.GrabFocus);
    }

    private IReadOnlyList<Control> FocusableControls() => GetChildrenRecursive(this)
        .OfType<Control>()
        .Where(IsFocusable)
        .ToArray();

    private static IEnumerable<Node> GetChildrenRecursive(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            yield return child;
            foreach (Node descendant in GetChildrenRecursive(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool IsFocusable(Control? control) => control is not null &&
        control.IsInsideTree() &&
        control.IsVisibleInTree() &&
        control.FocusMode != FocusModeEnum.None &&
        (control is not BaseButton button || !button.Disabled);

    private static void WireCycle(IReadOnlyList<Control> controls)
    {
        if (controls.Count == 0)
        {
            return;
        }
        for (int index = 0; index < controls.Count; index++)
        {
            Control current = controls[index];
            Control previous = controls[(index - 1 + controls.Count) % controls.Count];
            Control next = controls[(index + 1) % controls.Count];
            current.FocusPrevious = current.GetPathTo(previous);
            current.FocusNext = current.GetPathTo(next);
            current.FocusNeighborTop = current.GetPathTo(previous);
            current.FocusNeighborBottom = current.GetPathTo(next);
            current.FocusNeighborLeft = current.GetPathTo(previous);
            current.FocusNeighborRight = current.GetPathTo(next);
        }
    }
}
