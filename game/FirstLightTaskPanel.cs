using System;
using System.Collections.Generic;
using Godot;

namespace Gridworks.Game;

internal enum FirstLightPanelAction
{
    CancelDraft,
    Undo,
    Order,
    Advance,
    Settle,
    Restart,
}

internal sealed record FirstLightActionPresentation(
    bool Visible,
    bool Enabled,
    string Text,
    string AccessibilityDescription);

internal sealed record FirstLightTaskPanelModel(
    string Step,
    string Instruction,
    string Preview,
    string Status,
    string Error,
    FirstLightActionPresentation CancelDraft,
    FirstLightActionPresentation Undo,
    FirstLightActionPresentation Order,
    FirstLightActionPresentation Advance,
    FirstLightActionPresentation Settle,
    FirstLightActionPresentation Restart);

internal sealed partial class FirstLightTaskPanel : PanelContainer
{
    private Label _stepLabel = null!;
    private Label _instructionLabel = null!;
    private Label _previewLabel = null!;
    private Label _statusLabel = null!;
    private Label _errorLabel = null!;
    private Button _cancelDraftButton = null!;
    private Button _undoButton = null!;
    private Button _orderButton = null!;
    private Button _advanceButton = null!;
    private Button _settleButton = null!;
    private Button _restartButton = null!;
    private IReadOnlyDictionary<FirstLightPanelAction, Button> _buttons = null!;

    public event Action? CancelDraftRequested;

    public event Action? UndoRequested;

    public event Action? OrderRequested;

    public event Action? AdvanceRequested;

    public event Action? SettleRequested;

    public event Action? RestartRequested;

    public override void _Ready()
    {
        _stepLabel = GetNode<Label>("%StepLabel");
        _instructionLabel = GetNode<Label>("%InstructionLabel");
        _previewLabel = GetNode<Label>("%PreviewLabel");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _errorLabel = GetNode<Label>("%ErrorLabel");
        _cancelDraftButton = GetNode<Button>("%CancelDraftButton");
        _undoButton = GetNode<Button>("%UndoButton");
        _orderButton = GetNode<Button>("%OrderButton");
        _advanceButton = GetNode<Button>("%AdvanceButton");
        _settleButton = GetNode<Button>("%SettleButton");
        _restartButton = GetNode<Button>("%RestartButton");

        _buttons = new Dictionary<FirstLightPanelAction, Button>
        {
            [FirstLightPanelAction.CancelDraft] = _cancelDraftButton,
            [FirstLightPanelAction.Undo] = _undoButton,
            [FirstLightPanelAction.Order] = _orderButton,
            [FirstLightPanelAction.Advance] = _advanceButton,
            [FirstLightPanelAction.Settle] = _settleButton,
            [FirstLightPanelAction.Restart] = _restartButton,
        };

        _previewLabel.AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Polite;
        _errorLabel.AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Assertive;
        _cancelDraftButton.Pressed += () => CancelDraftRequested?.Invoke();
        _undoButton.Pressed += () => UndoRequested?.Invoke();
        _orderButton.Pressed += () => OrderRequested?.Invoke();
        _advanceButton.Pressed += () => AdvanceRequested?.Invoke();
        _settleButton.Pressed += () => SettleRequested?.Invoke();
        _restartButton.Pressed += () => RestartRequested?.Invoke();
    }

    public void SetModel(FirstLightTaskPanelModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _stepLabel.Text = model.Step;
        _stepLabel.AccessibilityName = $"현재 단계 {model.Step}";
        _instructionLabel.Text = model.Instruction;
        _instructionLabel.AccessibilityName = model.Instruction;
        _previewLabel.Text = model.Preview;
        _previewLabel.AccessibilityName = model.Preview;
        _statusLabel.Text = model.Status;
        _statusLabel.AccessibilityName = model.Status;
        _errorLabel.Text = model.Error;
        _errorLabel.AccessibilityName = string.IsNullOrEmpty(model.Error) ? "명령 오류 없음" : model.Error;

        SetButton(_cancelDraftButton, model.CancelDraft);
        SetButton(_undoButton, model.Undo);
        SetButton(_orderButton, model.Order);
        SetButton(_advanceButton, model.Advance);
        SetButton(_settleButton, model.Settle);
        SetButton(_restartButton, model.Restart);
        AccessibilityName = $"첫 점등 작업 패널. {model.Step}. {model.Status}";
    }

    public BaseButton GetActionButton(FirstLightPanelAction action)
    {
        if (!_buttons.TryGetValue(action, out Button? button))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }
        return button;
    }

    public IEnumerable<BaseButton> AllActionButtons => _buttons.Values;

    private static void SetButton(Button button, FirstLightActionPresentation presentation)
    {
        button.Visible = presentation.Visible;
        button.Disabled = !presentation.Enabled;
        button.Text = presentation.Text;
        button.AccessibilityName = presentation.Text;
        button.AccessibilityDescription = presentation.AccessibilityDescription;
    }
}
