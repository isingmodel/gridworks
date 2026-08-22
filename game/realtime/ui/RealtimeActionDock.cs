using System;
using Godot;

namespace Gridworks.Game.Realtime.UI;

internal sealed partial class RealtimeActionDock : PanelContainer
{
    private Label _context = null!;
    private Label _detail = null!;
    private Button _primary = null!;
    private string? _actionId;

    public event Action<string>? PrimaryActionRequested;

    public override void _Ready()
    {
        _context = GetNode<Label>("%ContextLabel");
        _detail = GetNode<Label>("%DetailLabel");
        _primary = GetNode<Button>("%PrimaryButton");
        _primary.Pressed += () =>
        {
            if (_actionId is string actionId)
            {
                PrimaryActionRequested?.Invoke(actionId);
            }
        };
    }

    public void SetPresentation(RealtimeActionDockPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        Visible = presentation.Visible && presentation.PrimaryAction?.Visible == true;
        _actionId = Visible ? presentation.PrimaryAction!.Id : null;
        if (!Visible)
        {
            return;
        }
        _context.Text = presentation.Context;
        _detail.Text = presentation.Detail;
        RealtimeActionPresentation action = presentation.PrimaryAction!;
        _primary.Text = action.Label;
        _primary.Disabled = !action.Enabled;
        _primary.TooltipText = action.Description;
        _primary.AccessibilityName = action.Label;
        _primary.AccessibilityDescription = action.Description;
        _primary.ThemeTypeVariation = action.Tone == RealtimeActionTone.Destructive
            ? "DestructiveButton"
            : "PrimaryButton";
        AccessibilityName =
            $"현재 작업. {presentation.Context}. {presentation.Detail}. {action.Label}.";
    }

    public void ApplyLayout(RealtimeLayoutProfile profile)
    {
        _primary.CustomMinimumSize = new Vector2(
            Math.Max(280, profile.ContextDockWidth - 48),
            profile.PrimaryHitTarget);
    }
}
