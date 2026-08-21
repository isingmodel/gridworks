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
    CycleLineClass,
    KeepPromise,
    DeferPromise,
    ApproveWindow,
    RollbackProject,
    RestartChapter,
    NextThermalPhase,
}

internal sealed record CommercialActionPresentation(
    bool Enabled,
    string Text,
    string Description);

internal sealed record CommercialTaskPanelModel(
    string Heading,
    string Instruction,
    string Selection,
    string Quote,
    string Thermal,
    string Status,
    string Error,
    CommercialActionPresentation PlaceSubstation,
    CommercialActionPresentation StartLine,
    CommercialActionPresentation UndoPoint,
    CommercialActionPresentation CancelDraft,
    CommercialActionPresentation Commission,
    CommercialActionPresentation CycleLineClass,
    CommercialActionPresentation KeepPromise,
    CommercialActionPresentation DeferPromise,
    CommercialActionPresentation ApproveWindow,
    CommercialActionPresentation RollbackProject,
    CommercialActionPresentation RestartChapter,
    CommercialActionPresentation NextThermalPhase);

internal sealed partial class CommercialTaskPanel : PanelContainer
{
    private Label _headingLabel = null!;
    private Label _instructionLabel = null!;
    private Label _selectionLabel = null!;
    private Label _quoteLabel = null!;
    private Label _thermalLabel = null!;
    private Label _statusLabel = null!;
    private Label _errorLabel = null!;
    private IReadOnlyDictionary<CommercialPanelAction, Button> _buttons = null!;

    public event Action<CommercialPanelAction>? ActionRequested;

    public override void _Ready()
    {
        _headingLabel = GetNode<Label>("%HeadingLabel");
        _instructionLabel = GetNode<Label>("%InstructionLabel");
        _selectionLabel = GetNode<Label>("%SelectionLabel");
        _quoteLabel = GetNode<Label>("%QuoteLabel");
        _thermalLabel = GetNode<Label>("%ThermalLabel");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _errorLabel = GetNode<Label>("%ErrorLabel");
        _buttons = new Dictionary<CommercialPanelAction, Button>
        {
            [CommercialPanelAction.PlaceSubstation] = GetNode<Button>("%PlaceSubstationButton"),
            [CommercialPanelAction.StartLine] = GetNode<Button>("%StartLineButton"),
            [CommercialPanelAction.UndoPoint] = GetNode<Button>("%UndoPointButton"),
            [CommercialPanelAction.CancelDraft] = GetNode<Button>("%CancelDraftButton"),
            [CommercialPanelAction.Commission] = GetNode<Button>("%CommissionButton"),
            [CommercialPanelAction.CycleLineClass] = GetNode<Button>("%CycleLineClassButton"),
            [CommercialPanelAction.KeepPromise] = GetNode<Button>("%KeepPromiseButton"),
            [CommercialPanelAction.DeferPromise] = GetNode<Button>("%DeferPromiseButton"),
            [CommercialPanelAction.ApproveWindow] = GetNode<Button>("%ApproveWindowButton"),
            [CommercialPanelAction.RollbackProject] = GetNode<Button>("%RollbackProjectButton"),
            [CommercialPanelAction.RestartChapter] = GetNode<Button>("%RestartChapterButton"),
            [CommercialPanelAction.NextThermalPhase] = GetNode<Button>("%NextThermalPhaseButton"),
        };

        foreach ((CommercialPanelAction action, Button button) in _buttons)
        {
            button.Pressed += () => ActionRequested?.Invoke(action);
        }
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
        _thermalLabel.Text = model.Thermal;
        _statusLabel.Text = model.Status;
        _errorLabel.Text = model.Error;
        SetButton(CommercialPanelAction.PlaceSubstation, model.PlaceSubstation);
        SetButton(CommercialPanelAction.StartLine, model.StartLine);
        SetButton(CommercialPanelAction.UndoPoint, model.UndoPoint);
        SetButton(CommercialPanelAction.CancelDraft, model.CancelDraft);
        SetButton(CommercialPanelAction.Commission, model.Commission);
        SetButton(CommercialPanelAction.CycleLineClass, model.CycleLineClass);
        SetButton(CommercialPanelAction.KeepPromise, model.KeepPromise);
        SetButton(CommercialPanelAction.DeferPromise, model.DeferPromise);
        SetButton(CommercialPanelAction.ApproveWindow, model.ApproveWindow);
        SetButton(CommercialPanelAction.RollbackProject, model.RollbackProject);
        SetButton(CommercialPanelAction.RestartChapter, model.RestartChapter);
        SetButton(CommercialPanelAction.NextThermalPhase, model.NextThermalPhase);
        AccessibilityName = $"공사와 열 작업 패널. {model.Heading}. {model.Thermal}. {model.Status}";
    }

    public BaseButton GetActionButton(CommercialPanelAction action) => _buttons[action];

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
