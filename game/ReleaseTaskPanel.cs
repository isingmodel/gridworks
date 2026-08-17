using System;
using System.Collections.Generic;
using Godot;

namespace Gridworks.Game;

internal enum ReleasePanelAction
{
    Inspect,
    SmallSubstation,
    LargeSubstation,
    StandardLine,
    ReinforcedLine,
    Cancel,
    Undo,
    Order,
    Advance,
}

internal sealed record ReleaseButtonPresentation(
    bool Visible,
    bool Enabled,
    string Text,
    string Description);

internal sealed record ReleaseTaskPanelModel(
    string Heading,
    string Instruction,
    string Selection,
    string Network,
    string Quote,
    string Error,
    ReleaseButtonPresentation Inspect,
    ReleaseButtonPresentation SmallSubstation,
    ReleaseButtonPresentation LargeSubstation,
    ReleaseButtonPresentation StandardLine,
    ReleaseButtonPresentation ReinforcedLine,
    ReleaseButtonPresentation Cancel,
    ReleaseButtonPresentation Undo,
    ReleaseButtonPresentation Order,
    ReleaseButtonPresentation Advance);

internal sealed partial class ReleaseTaskPanel : PanelContainer
{
    private Label _heading = null!;
    private Label _instruction = null!;
    private Label _selection = null!;
    private Label _network = null!;
    private Label _quote = null!;
    private Label _error = null!;
    private IReadOnlyDictionary<ReleasePanelAction, Button> _buttons = null!;

    public event Action<ReleasePanelAction>? ActionRequested;

    public override void _Ready()
    {
        _heading = GetNode<Label>("%HeadingLabel");
        _instruction = GetNode<Label>("%InstructionLabel");
        _selection = GetNode<Label>("%SelectionLabel");
        _network = GetNode<Label>("%NetworkLabel");
        _quote = GetNode<Label>("%QuoteLabel");
        _error = GetNode<Label>("%ErrorLabel");
        _error.AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Assertive;
        _quote.AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Polite;

        _buttons = new Dictionary<ReleasePanelAction, Button>
        {
            [ReleasePanelAction.Inspect] = GetNode<Button>("%InspectButton"),
            [ReleasePanelAction.SmallSubstation] = GetNode<Button>("%SmallSubstationButton"),
            [ReleasePanelAction.LargeSubstation] = GetNode<Button>("%LargeSubstationButton"),
            [ReleasePanelAction.StandardLine] = GetNode<Button>("%StandardLineButton"),
            [ReleasePanelAction.ReinforcedLine] = GetNode<Button>("%ReinforcedLineButton"),
            [ReleasePanelAction.Cancel] = GetNode<Button>("%CancelButton"),
            [ReleasePanelAction.Undo] = GetNode<Button>("%UndoButton"),
            [ReleasePanelAction.Order] = GetNode<Button>("%OrderButton"),
            [ReleasePanelAction.Advance] = GetNode<Button>("%AdvanceButton"),
        };
        foreach ((ReleasePanelAction action, Button button) in _buttons)
        {
            button.Pressed += () => ActionRequested?.Invoke(action);
        }
    }

    public void SetModel(ReleaseTaskPanelModel model)
    {
        _heading.Text = model.Heading;
        _instruction.Text = model.Instruction;
        _selection.Text = model.Selection;
        _network.Text = model.Network;
        _quote.Text = model.Quote;
        _error.Text = model.Error;
        _heading.AccessibilityName = model.Heading;
        _instruction.AccessibilityName = model.Instruction;
        _selection.AccessibilityName = model.Selection;
        _network.AccessibilityName = model.Network;
        _quote.AccessibilityName = model.Quote;
        _error.AccessibilityName = string.IsNullOrWhiteSpace(model.Error)
            ? "작업 오류 없음"
            : model.Error;

        Set(ReleasePanelAction.Inspect, model.Inspect);
        Set(ReleasePanelAction.SmallSubstation, model.SmallSubstation);
        Set(ReleasePanelAction.LargeSubstation, model.LargeSubstation);
        Set(ReleasePanelAction.StandardLine, model.StandardLine);
        Set(ReleasePanelAction.ReinforcedLine, model.ReinforcedLine);
        Set(ReleasePanelAction.Cancel, model.Cancel);
        Set(ReleasePanelAction.Undo, model.Undo);
        Set(ReleasePanelAction.Order, model.Order);
        Set(ReleasePanelAction.Advance, model.Advance);
        AccessibilityName = $"전력망 작업 패널. {model.Heading}. {model.Network}";
    }

    public BaseButton GetActionButton(ReleasePanelAction action) => _buttons[action];

    private void Set(ReleasePanelAction action, ReleaseButtonPresentation presentation)
    {
        Button button = _buttons[action];
        button.Visible = presentation.Visible;
        button.Disabled = !presentation.Enabled;
        button.Text = presentation.Text;
        button.AccessibilityName = presentation.Text;
        button.AccessibilityDescription = presentation.Description;
    }
}
