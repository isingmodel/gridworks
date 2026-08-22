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
    NewGame,
    NextThermalPhase,
    NextDemand,
}

internal sealed record CommercialActionPresentation(
    bool Enabled,
    string Text,
    string Description,
    bool Visible = true);

internal sealed record CommercialSpeakerPresentation(
    string PersonId,
    string NameAndRole,
    Color CardColor);

internal sealed record CommercialTaskPanelModel(
    string Heading,
    CommercialSpeakerPresentation Speaker,
    string Instruction,
    string Obligations,
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
    CommercialActionPresentation NewGame,
    CommercialActionPresentation NextThermalPhase,
    CommercialActionPresentation NextDemand);

internal sealed partial class CommercialTaskPanel : PanelContainer
{
    private Label _headingLabel = null!;
    private CommercialPortrait _portrait = null!;
    private Label _speakerLabel = null!;
    private Label _instructionLabel = null!;
    private Label _obligationsLabel = null!;
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
        _portrait = GetNode<CommercialPortrait>("%Portrait");
        _speakerLabel = GetNode<Label>("%SpeakerLabel");
        _instructionLabel = GetNode<Label>("%InstructionLabel");
        _obligationsLabel = GetNode<Label>("%ObligationsLabel");
        _selectionLabel = GetNode<Label>("%SelectionLabel");
        _quoteLabel = GetNode<Label>("%QuoteLabel");
        _thermalLabel = GetNode<Label>("%ThermalLabel");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _errorLabel = GetNode<Label>("%ErrorLabel");
        _speakerLabel.GetParent<Control>().Visible = false;
        _instructionLabel.Visible = false;
        _selectionLabel.Visible = false;
        _thermalLabel.Visible = false;
        _statusLabel.Visible = false;
        VBoxContainer infoColumn = GetNode<VBoxContainer>("Margin/Column/InfoScroll/InfoColumn");
        infoColumn.MoveChild(_obligationsLabel, 1);
        infoColumn.MoveChild(_thermalLabel, 2);
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
            [CommercialPanelAction.NewGame] = GetNode<Button>("%NewGameButton"),
            [CommercialPanelAction.NextThermalPhase] = GetNode<Button>("%NextThermalPhaseButton"),
            [CommercialPanelAction.NextDemand] = GetNode<Button>("%NextDemandButton"),
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
        _portrait.SetPerson(
            model.Speaker.PersonId,
            model.Speaker.NameAndRole,
            model.Speaker.CardColor);
        _speakerLabel.Text = model.Speaker.NameAndRole;
        _speakerLabel.AddThemeColorOverride("font_color", model.Speaker.CardColor);
        _instructionLabel.Text = model.Instruction;
        _obligationsLabel.Text = CompactVisual(model.Obligations, 38);
        _selectionLabel.Text = model.Selection;
        _quoteLabel.Visible = !string.IsNullOrWhiteSpace(model.Quote);
        _quoteLabel.Text = CompactVisual(model.Quote, 62);
        _thermalLabel.Text = CompactVisual(model.Thermal, 72);
        _statusLabel.Text = model.Status;
        _errorLabel.Visible = !string.IsNullOrWhiteSpace(model.Error);
        _errorLabel.Text = CompactVisual(model.Error, 72);
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
        SetButton(CommercialPanelAction.NewGame, model.NewGame);
        SetButton(CommercialPanelAction.NextThermalPhase, model.NextThermalPhase);
        SetButton(CommercialPanelAction.NextDemand, model.NextDemand);
        AccessibilityName =
            $"공사와 열 작업 패널. {model.Heading}. {model.Speaker.NameAndRole}. " +
            $"{model.Instruction}. {model.Obligations}. " +
            $"{model.Thermal}. {model.Status}";
    }

    public BaseButton GetActionButton(CommercialPanelAction action) => _buttons[action];

    private void SetButton(
        CommercialPanelAction action,
        CommercialActionPresentation presentation)
    {
        Button button = _buttons[action];
        // The inspector is contextual: controls that cannot act in the current
        // state collapse completely instead of forming a tall disabled form.
        // Their descriptions remain available when the action becomes valid.
        button.Visible = presentation.Visible && presentation.Enabled;
        button.Disabled = !presentation.Enabled;
        button.Text = action switch
        {
            CommercialPanelAction.ApproveWindow or CommercialPanelAction.NextThermalPhase =>
                $"⚡  {presentation.Text}",
            CommercialPanelAction.RollbackProject => $"↶  {presentation.Text}",
            _ => presentation.Text,
        };
        button.AccessibilityName = presentation.Text;
        button.AccessibilityDescription = presentation.Description;
    }

    private static string CompactVisual(string value, int maxCharacters)
    {
        string compact = string.Join(
            " · ",
            (value ?? string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return compact.Length <= maxCharacters
            ? compact
            : compact[..Math.Max(1, maxCharacters - 1)] + "…";
    }
}
