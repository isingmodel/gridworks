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
    string Objective,
    string NextAction,
    bool FullInstruction,
    bool ShowOperationalCards,
    string Obligations,
    string Selection,
    string Quote,
    string Thermal,
    string Status,
    string Error,
    string FacilityType,
    string FacilityCapacity,
    string FacilityState,
    bool FacilityUnavailable,
    bool ShowComparisonCards,
    string ComparisonATitle,
    string ComparisonAMetric,
    string ComparisonADetail,
    string ComparisonBTitle,
    string ComparisonBMetric,
    string ComparisonBDetail,
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
    private Label _objectiveLabel = null!;
    private Label _nextActionLabel = null!;
    private CommercialPortrait _portrait = null!;
    private Label _speakerLabel = null!;
    private Label _instructionLabel = null!;
    private Label _obligationsLabel = null!;
    private Label _selectionLabel = null!;
    private Label _quoteLabel = null!;
    private Label _thermalLabel = null!;
    private Label _statusLabel = null!;
    private Label _errorLabel = null!;
    private TextureRect _facilityImage = null!;
    private Label _facilityType = null!;
    private Label _facilityCapacity = null!;
    private Label _facilityState = null!;
    private Control _facilityCard = null!;
    private Control _comparisonCards = null!;
    private ScrollContainer _infoScroll = null!;
    private Label _comparisonATitle = null!;
    private Label _comparisonAMetric = null!;
    private Label _comparisonADetail = null!;
    private Label _comparisonBTitle = null!;
    private Label _comparisonBMetric = null!;
    private Label _comparisonBDetail = null!;
    private IReadOnlyDictionary<CommercialPanelAction, Button> _buttons = null!;
    private string _narrativeIdentity = string.Empty;

    public event Action<CommercialPanelAction>? ActionRequested;

    public override void _Ready()
    {
        _headingLabel = GetNode<Label>("%HeadingLabel");
        _objectiveLabel = GetNode<Label>("%ObjectiveLabel");
        _nextActionLabel = GetNode<Label>("%NextActionLabel");
        _portrait = GetNode<CommercialPortrait>("%Portrait");
        _speakerLabel = GetNode<Label>("%SpeakerLabel");
        _instructionLabel = GetNode<Label>("%InstructionLabel");
        _obligationsLabel = GetNode<Label>("%ObligationsLabel");
        _selectionLabel = GetNode<Label>("%SelectionLabel");
        _quoteLabel = GetNode<Label>("%QuoteLabel");
        _thermalLabel = GetNode<Label>("%ThermalLabel");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _errorLabel = GetNode<Label>("%ErrorLabel");
        _facilityImage = GetNode<TextureRect>("%FacilityImage");
        _facilityType = GetNode<Label>("%FacilityType");
        _facilityCapacity = GetNode<Label>("%FacilityCapacity");
        _facilityState = GetNode<Label>("%FacilityState");
        _facilityCard = GetNode<Control>("%FacilityCard");
        _comparisonCards = GetNode<Control>("%ComparisonCards");
        _infoScroll = GetNode<ScrollContainer>("Margin/Column/InfoScroll");
        _comparisonATitle = GetNode<Label>("%ComparisonATitle");
        _comparisonAMetric = GetNode<Label>("%ComparisonAMetric");
        _comparisonADetail = GetNode<Label>("%ComparisonADetail");
        _comparisonBTitle = GetNode<Label>("%ComparisonBTitle");
        _comparisonBMetric = GetNode<Label>("%ComparisonBMetric");
        _comparisonBDetail = GetNode<Label>("%ComparisonBDetail");
        // The right inspector mirrors the reference's dense, factual hierarchy.
        // All authored facts stay visible in the scroll region instead of collapsing
        // into a mostly empty frame; only contextually unavailable actions collapse.
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
        string narrativeIdentity = string.Join(
            '\u001f',
            model.Heading,
            model.Speaker.PersonId,
            model.Instruction,
            model.FullInstruction ? "full" : "compact");
        if (!string.Equals(_narrativeIdentity, narrativeIdentity, StringComparison.Ordinal))
        {
            _narrativeIdentity = narrativeIdentity;
            // ScrollContainer keeps its offset when the same inspector node is
            // reused. Reset only when the authored card identity changes so a
            // new briefing/result starts at its first paragraph without making
            // an actively read card impossible to scroll.
            _infoScroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, 0);
        }
        _headingLabel.Text = model.Heading;
        _objectiveLabel.Text = model.Objective;
        _nextActionLabel.Text = model.NextAction;
        _portrait.SetPerson(
            model.Speaker.PersonId,
            model.Speaker.NameAndRole,
            model.Speaker.CardColor);
        _speakerLabel.Text = model.Speaker.NameAndRole;
        _speakerLabel.AddThemeColorOverride("font_color", model.Speaker.CardColor);
        _instructionLabel.Text = model.FullInstruction
            ? model.Instruction
            : CompactVisual(model.Instruction, 92);
        _obligationsLabel.Text = model.FullInstruction
            ? model.Obligations
            : CompactVisual(model.Obligations, 38);
        _obligationsLabel.Visible = !string.IsNullOrWhiteSpace(model.Obligations);
        bool operationalDetails = model.ShowOperationalCards;
        _selectionLabel.Visible = operationalDetails;
        _selectionLabel.Text = CompactVisual(model.Selection, 72);
        _quoteLabel.Visible = operationalDetails && !string.IsNullOrWhiteSpace(model.Quote);
        _quoteLabel.Text = CompactVisual(model.Quote, 62);
        _thermalLabel.Visible = operationalDetails;
        _thermalLabel.Text = CompactVisual(model.Thermal, 72);
        _statusLabel.Visible = operationalDetails;
        _statusLabel.Text = CompactVisual(model.Status, 78);
        _errorLabel.Visible = !string.IsNullOrWhiteSpace(model.Error);
        _errorLabel.Text = CompactVisual(model.Error, 72);
        _facilityType.Text = model.FacilityType;
        _facilityCapacity.Text = model.FacilityCapacity;
        _facilityState.Text = model.FacilityState;
        Color facilityStateColor = model.FacilityUnavailable
            ? Color.FromHtml("e56e73")
            : model.FacilityState.Contains("비상", StringComparison.Ordinal)
                ? Color.FromHtml("f0b75e")
                : Color.FromHtml("71c9cf");
        _facilityCapacity.AddThemeColorOverride("font_color", facilityStateColor);
        _facilityState.AddThemeColorOverride("font_color", facilityStateColor);
        _facilityImage.Modulate = model.FacilityUnavailable
            ? new Color(0.82f, 0.40f, 0.38f, 0.86f)
            : Colors.White;
        _facilityCard.Visible = model.ShowOperationalCards && !model.ShowComparisonCards;
        _comparisonCards.Visible = model.ShowOperationalCards && model.ShowComparisonCards;
        // Full briefings and non-operational states keep their authored narrative.
        // Operational A/B comparisons may dedicate the inspector body to their
        // cards, while the pinned objective and next action remain visible above.
        _infoScroll.Visible = model.FullInstruction ||
            !model.ShowOperationalCards ||
            !model.ShowComparisonCards;
        _comparisonATitle.Text = model.ComparisonATitle;
        _comparisonAMetric.Text = model.ComparisonAMetric;
        _comparisonADetail.Text = model.ComparisonADetail;
        _comparisonBTitle.Text = model.ComparisonBTitle;
        _comparisonBMetric.Text = model.ComparisonBMetric;
        _comparisonBDetail.Text = model.ComparisonBDetail;
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
        string operationalAccessibility = operationalDetails
            ? model.ShowComparisonCards
                ? $"{model.Thermal}. {model.ComparisonATitle} {model.ComparisonAMetric}. " +
                  $"{model.ComparisonBTitle} {model.ComparisonBMetric}. "
                : $"{model.Thermal}. {model.FacilityType} {model.FacilityCapacity}. " +
                  $"{model.FacilityState}. "
            : string.Empty;
        string operationalStatus = operationalDetails ? model.Status : string.Empty;
        AccessibilityName =
            $"공사와 열 작업 패널. {model.Heading}. {model.Speaker.NameAndRole}. " +
            $"{model.Objective}. {model.NextAction}. " +
            $"{model.Instruction}. {model.Obligations}. " +
            $"{operationalAccessibility}{operationalStatus}. {model.Error}";
    }

    public BaseButton GetActionButton(CommercialPanelAction action) => _buttons[action];

    public bool HasVisibleError =>
        _errorLabel.Visible && !string.IsNullOrWhiteSpace(_errorLabel.Text);

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
