using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Gridworks.Game.Realtime.UI;

internal sealed partial class RealtimeBuildShelf : PanelContainer
{
    private HBoxContainer _tools = null!;
    private Label _guidance = null!;
    private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);
    private readonly ButtonGroup _toolGroup = new() { AllowUnpress = false };
    private RealtimeLayoutProfile _layoutProfile;

    public event Action<string>? ToolRequested;

    public override void _Ready()
    {
        _tools = GetNode<HBoxContainer>("%Tools");
        _guidance = GetNode<Label>("%GuidanceLabel");
    }

    public void SetPresentation(RealtimeBuildShelfPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        Visible = presentation.Visible;
        if (!presentation.Visible)
        {
            return;
        }

        _guidance.Text = string.IsNullOrWhiteSpace(presentation.Guidance)
            ? "도구 하나만 선택됩니다"
            : presentation.Guidance;
        _guidance.TooltipText = _guidance.Text;
        _guidance.AccessibilityName = $"공사 도구 안내. {_guidance.Text}";

        var retained = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < presentation.Tools.Count; index++)
        {
            RealtimeBuildToolPresentation tool = presentation.Tools[index];
            retained.Add(tool.Id);
            if (!_buttons.TryGetValue(tool.Id, out Button? button))
            {
                string id = tool.Id;
                button = new Button
                {
                    ToggleMode = true,
                    ButtonGroup = _toolGroup,
                    FocusMode = FocusModeEnum.All,
                    ThemeTypeVariation = "ToolButton",
                    CustomMinimumSize = new Vector2(108f, 44f),
                };
                button.Pressed += () => ToolRequested?.Invoke(id);
                _buttons.Add(id, button);
                _tools.AddChild(button);
                ApplyButtonLayout(button);
            }
            button.Text = string.IsNullOrWhiteSpace(tool.Shortcut)
                ? tool.Label
                : $"{tool.Label}  {tool.Shortcut}";
            button.Disabled = !tool.Enabled;
            button.ButtonPressed = tool.Selected;
            button.TooltipText = tool.Description;
            button.AccessibilityName = tool.Label;
            button.AccessibilityDescription =
                $"{tool.Description} 단축키 {tool.Shortcut}. " +
                (tool.Selected ? "현재 선택됨." : string.Empty);
            _tools.MoveChild(button, index);
        }

        foreach (string id in new List<string>(_buttons.Keys))
        {
            if (retained.Contains(id))
            {
                continue;
            }
            Button button = _buttons[id];
            _tools.RemoveChild(button);
            button.QueueFree();
            _buttons.Remove(id);
        }
        AccessibilityName = "공사 도구. " +
            string.Join(". ", presentation.Tools.Select(tool =>
                $"{tool.Label}. {tool.Description}"));
    }

    public void ApplyLayout(RealtimeLayoutProfile profile)
    {
        _layoutProfile = profile;
        CustomMinimumSize = new Vector2(0f, profile.BuildShelfHeight);
        _guidance.CustomMinimumSize = new Vector2(
            Math.Min(420f, 300f * profile.AccessibilityScale),
            profile.MinimumHitTarget);
        foreach (Button button in _buttons.Values)
        {
            ApplyButtonLayout(button);
        }
    }

    private void ApplyButtonLayout(Button button)
    {
        int minimumHitTarget = _layoutProfile.MinimumHitTarget <= 0
            ? 44
            : _layoutProfile.MinimumHitTarget;
        button.CustomMinimumSize = new Vector2(
            Math.Max(114, minimumHitTarget * 2),
            minimumHitTarget);
    }
}
