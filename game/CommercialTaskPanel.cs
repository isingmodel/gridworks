using System;
using System.Collections.Generic;
using Godot;

namespace Gridworks.Game;

internal enum CommercialPanelAction
{
    PlaceSubstation,
    StartLine,
    UndoPoint,
    CancelDraft,
    Commission,
}

internal sealed record CommercialActionPresentation(
    bool Enabled,
    string Text,
    string Description);

internal sealed record CommercialProjectionPresentation(
    string Label,
    bool PreviousEnabled,
    bool NextEnabled);

internal sealed record CommercialTaskPanelModel(
    string Heading,
    string Instruction,
    string Selection,
    string Quote,
    string Status,
    string Error,
    CommercialActionPresentation PlaceSubstation,
    CommercialActionPresentation StartLine,
    CommercialActionPresentation UndoPoint,
    CommercialActionPresentation CancelDraft,
    CommercialActionPresentation Commission,
    CommercialProjectionPresentation? Projection = null);

internal sealed partial class CommercialTaskPanel : PanelContainer
{
    private Label _headingLabel = null!;
    private Label _instructionLabel = null!;
    private Label _selectionLabel = null!;
    private Label _quoteLabel = null!;
    private Label _statusLabel = null!;
    private Label _errorLabel = null!;
    private Control _toolRow = null!;
    private Control _editRow = null!;
    private Control _thermalControls = null!;
    private Label _projectionLabel = null!;
    private Button _previousProjectionButton = null!;
    private Button _nextProjectionButton = null!;
    private IReadOnlyDictionary<CommercialPanelAction, Button> _buttons = null!;

    public event Action<CommercialPanelAction>? ActionRequested;

    public event Action<int>? ProjectionDeltaRequested;

    public override void _Ready()
    {
        _headingLabel = GetNode<Label>("%HeadingLabel");
        _instructionLabel = GetNode<Label>("%InstructionLabel");
        _selectionLabel = GetNode<Label>("%SelectionLabel");
        _quoteLabel = GetNode<Label>("%QuoteLabel");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _errorLabel = GetNode<Label>("%ErrorLabel");
        _toolRow = GetNode<Control>("%ToolRow");
        _editRow = GetNode<Control>("%EditRow");
        _thermalControls = GetNode<Control>("%ThermalControls");
        _projectionLabel = GetNode<Label>("%ProjectionLabel");
        _previousProjectionButton = GetNode<Button>("%PreviousProjectionButton");
        _nextProjectionButton = GetNode<Button>("%NextProjectionButton");
        _buttons = new Dictionary<CommercialPanelAction, Button>
        {
            [CommercialPanelAction.PlaceSubstation] = GetNode<Button>("%PlaceSubstationButton"),
            [CommercialPanelAction.StartLine] = GetNode<Button>("%StartLineButton"),
            [CommercialPanelAction.UndoPoint] = GetNode<Button>("%UndoPointButton"),
            [CommercialPanelAction.CancelDraft] = GetNode<Button>("%CancelDraftButton"),
            [CommercialPanelAction.Commission] = GetNode<Button>("%CommissionButton"),
        };

        foreach ((CommercialPanelAction action, Button button) in _buttons)
        {
            button.Pressed += () => ActionRequested?.Invoke(action);
        }
        _previousProjectionButton.Pressed += () => ProjectionDeltaRequested?.Invoke(-1);
        _nextProjectionButton.Pressed += () => ProjectionDeltaRequested?.Invoke(1);
        _statusLabel.AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Polite;
        _errorLabel.AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Assertive;
    }

    public void SetModel(CommercialTaskPanelModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _headingLabel.Text = model.Heading;
        _instructionLabel.Text = model.Instruction;
        _selectionLabel.Text = model.Selection;
        _quoteLabel.Text = model.Quote;
        _statusLabel.Text = model.Status;
        _errorLabel.Text = model.Error;
        SetButton(CommercialPanelAction.PlaceSubstation, model.PlaceSubstation);
        SetButton(CommercialPanelAction.StartLine, model.StartLine);
        SetButton(CommercialPanelAction.UndoPoint, model.UndoPoint);
        SetButton(CommercialPanelAction.CancelDraft, model.CancelDraft);
        SetButton(CommercialPanelAction.Commission, model.Commission);
        bool thermal = model.Projection is not null;
        _toolRow.Visible = !thermal;
        _editRow.Visible = !thermal;
        _buttons[CommercialPanelAction.Commission].Visible = !thermal;
        _thermalControls.Visible = thermal;
        if (model.Projection is CommercialProjectionPresentation projection)
        {
            _projectionLabel.Text = projection.Label;
            _previousProjectionButton.Disabled = !projection.PreviousEnabled;
            _nextProjectionButton.Disabled = !projection.NextEnabled;
            _previousProjectionButton.AccessibilityDescription =
                "이전 열 운전 국면의 계산 결과를 표시합니다.";
            _nextProjectionButton.AccessibilityDescription =
                "다음 열 운전 국면의 계산 결과를 표시합니다.";
        }
        AccessibilityName = thermal
            ? $"열 운전 확인 패널. {model.Heading}. {model.Status}"
            : $"공사 작업 패널. {model.Heading}. {model.Status}";
    }

    public BaseButton GetActionButton(CommercialPanelAction action) => _buttons[action];

    public BaseButton GetProjectionButton(int direction) => direction < 0
        ? _previousProjectionButton
        : _nextProjectionButton;

    public string SelectionText => _selectionLabel.Text;

    public string LimitsText => _quoteLabel.Text;

    public string StatusText => _statusLabel.Text;

    public string ProjectionText => _projectionLabel.Text;

    private void SetButton(
        CommercialPanelAction action,
        CommercialActionPresentation presentation)
    {
        Button button = _buttons[action];
        button.Disabled = !presentation.Enabled;
        button.Text = presentation.Text;
        button.AccessibilityName = presentation.Text;
        button.AccessibilityDescription = presentation.Description;
    }
}
