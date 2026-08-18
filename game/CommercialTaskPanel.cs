using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Godot;

namespace Gridworks.Game;

internal enum CommercialPanelAction
{
    PlaceSubstation,
    PlaceLargeSubstation,
    StartStandardLine,
    StartLine,
    UndoPoint,
    CancelDraft,
    Commission,
}

internal enum CommercialProductAction
{
    ApproveWindow,
    RollbackRecentConstruction,
    RestartWindow,
    RestartChapter,
    RewindPreviousChapter,
}

internal sealed record CommercialActionPresentation(
    bool Enabled,
    string Text,
    string Description,
    bool Visible = true);

internal sealed record CommercialProjectionPresentation(
    string Label,
    bool PreviousEnabled,
    bool NextEnabled);

internal sealed record CommercialObligationPresentation(string Label, string Status);

internal sealed record CommercialPromisePresentation(
    string Heading,
    string Status,
    string KeepLabel,
    string DeferLabel,
    bool CanChoose);

internal sealed record CommercialProductPanelPresentation(
    string Objective,
    IReadOnlyList<CommercialObligationPresentation> Obligations,
    string Deadline,
    CommercialPromisePresentation? Promise,
    CommercialActionPresentation ApproveWindow,
    CommercialActionPresentation RollbackRecentConstruction,
    CommercialActionPresentation RestartWindow,
    CommercialActionPresentation RestartChapter,
    CommercialActionPresentation RewindPreviousChapter);

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
    CommercialProjectionPresentation? Projection = null,
    bool ShowConstructionActions = true,
    CommercialProductPanelPresentation? Product = null,
    CommercialActionPresentation? StandardLine = null,
    CommercialActionPresentation? LargeSubstation = null);

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
    private Control _productSections = null!;
    private Label _objectiveLabel = null!;
    private Label _obligationsLabel = null!;
    private Label _deadlineLabel = null!;
    private Control _promiseControls = null!;
    private Label _promiseHeading = null!;
    private Label _promiseStatus = null!;
    private Button _keepPromiseButton = null!;
    private Button _deferPromiseButton = null!;
    private Control _productActions = null!;
    private IReadOnlyDictionary<CommercialProductAction, Button> _productButtons = null!;
    private IReadOnlyDictionary<CommercialPanelAction, Button> _buttons = null!;

    public event Action<CommercialPanelAction>? ActionRequested;

    public event Action<int>? ProjectionDeltaRequested;
    public event Action<CommercialPromiseDecision>? PromiseRequested;
    public event Action<CommercialProductAction>? ProductActionRequested;

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
        _productSections = GetNode<Control>("%ProductSections");
        _objectiveLabel = GetNode<Label>("%ObjectiveLabel");
        _obligationsLabel = GetNode<Label>("%ObligationsLabel");
        _deadlineLabel = GetNode<Label>("%DeadlineLabel");
        _promiseControls = GetNode<Control>("%PromiseControls");
        _promiseHeading = GetNode<Label>("%PromiseHeading");
        _promiseStatus = GetNode<Label>("%PromiseStatus");
        _keepPromiseButton = GetNode<Button>("%KeepPromiseButton");
        _deferPromiseButton = GetNode<Button>("%DeferPromiseButton");
        _productActions = GetNode<Control>("%ProductActions");
        _buttons = new Dictionary<CommercialPanelAction, Button>
        {
            [CommercialPanelAction.PlaceSubstation] = GetNode<Button>("%PlaceSubstationButton"),
            [CommercialPanelAction.PlaceLargeSubstation] =
                GetNode<Button>("%PlaceLargeSubstationButton"),
            [CommercialPanelAction.StartStandardLine] =
                GetNode<Button>("%StartStandardLineButton"),
            [CommercialPanelAction.StartLine] = GetNode<Button>("%StartLineButton"),
            [CommercialPanelAction.UndoPoint] = GetNode<Button>("%UndoPointButton"),
            [CommercialPanelAction.CancelDraft] = GetNode<Button>("%CancelDraftButton"),
            [CommercialPanelAction.Commission] = GetNode<Button>("%CommissionButton"),
        };
        _productButtons = new Dictionary<CommercialProductAction, Button>
        {
            [CommercialProductAction.ApproveWindow] = GetNode<Button>("%ApproveWindowButton"),
            [CommercialProductAction.RollbackRecentConstruction] =
                GetNode<Button>("%RollbackRecentButton"),
            [CommercialProductAction.RestartWindow] = GetNode<Button>("%RestartWindowButton"),
            [CommercialProductAction.RestartChapter] = GetNode<Button>("%RestartChapterButton"),
            [CommercialProductAction.RewindPreviousChapter] =
                GetNode<Button>("%RewindPreviousChapterButton"),
        };

        foreach ((CommercialPanelAction action, Button button) in _buttons)
        {
            button.Pressed += () => ActionRequested?.Invoke(action);
        }
        _previousProjectionButton.Pressed += () => ProjectionDeltaRequested?.Invoke(-1);
        _nextProjectionButton.Pressed += () => ProjectionDeltaRequested?.Invoke(1);
        _keepPromiseButton.Pressed += () =>
            PromiseRequested?.Invoke(CommercialPromiseDecision.Keep);
        _deferPromiseButton.Pressed += () =>
            PromiseRequested?.Invoke(CommercialPromiseDecision.Defer);
        foreach ((CommercialProductAction action, Button button) in _productButtons)
        {
            button.Pressed += () => ProductActionRequested?.Invoke(action);
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
        _statusLabel.Text = model.Status;
        _errorLabel.Text = model.Error;
        SetButton(CommercialPanelAction.PlaceSubstation, model.PlaceSubstation);
        SetButton(
            CommercialPanelAction.PlaceLargeSubstation,
            model.LargeSubstation ?? new CommercialActionPresentation(
                false,
                string.Empty,
                string.Empty,
                false));
        SetButton(
            CommercialPanelAction.StartStandardLine,
            model.StandardLine ?? new CommercialActionPresentation(
                false,
                string.Empty,
                string.Empty,
                false));
        SetButton(CommercialPanelAction.StartLine, model.StartLine);
        SetButton(CommercialPanelAction.UndoPoint, model.UndoPoint);
        SetButton(CommercialPanelAction.CancelDraft, model.CancelDraft);
        SetButton(CommercialPanelAction.Commission, model.Commission);
        bool projectionVisible = model.Projection is not null;
        bool toolSelectionAvailable = model.PlaceSubstation.Enabled ||
            model.LargeSubstation?.Enabled == true ||
            model.StandardLine?.Enabled == true ||
            model.StartLine.Enabled;
        bool draftEditingAvailable = model.UndoPoint.Enabled || model.CancelDraft.Enabled;
        _toolRow.Visible = model.ShowConstructionActions && toolSelectionAvailable;
        _editRow.Visible = model.ShowConstructionActions && draftEditingAvailable;
        _buttons[CommercialPanelAction.Commission].Visible = model.ShowConstructionActions;
        _thermalControls.Visible = projectionVisible;
        if (model.Projection is CommercialProjectionPresentation projection)
        {
            _projectionLabel.Text = projection.Label;
            _previousProjectionButton.Disabled = !projection.PreviousEnabled;
            _nextProjectionButton.Disabled = !projection.NextEnabled;
            _previousProjectionButton.AccessibilityDescription =
                "이전 운영 국면의 계산 결과를 표시합니다.";
            _nextProjectionButton.AccessibilityDescription =
                "다음 운영 국면의 계산 결과를 표시합니다.";
        }
        _productSections.Visible = model.Product is not null;
        _promiseControls.Visible = model.Product?.Promise is not null;
        _productActions.Visible = model.Product is not null;
        if (model.Product is CommercialProductPanelPresentation product)
        {
            _objectiveLabel.Text = product.Objective;
            _obligationsLabel.Text = string.Join("\n", product.Obligations.Select(item =>
                $"{item.Status} · {item.Label}"));
            _deadlineLabel.Text = product.Deadline;
            if (product.Promise is CommercialPromisePresentation promise)
            {
                _promiseHeading.Text = promise.Heading;
                _promiseStatus.Text = promise.Status;
                _keepPromiseButton.Text = promise.KeepLabel;
                _deferPromiseButton.Text = promise.DeferLabel;
                _keepPromiseButton.Disabled = !promise.CanChoose;
                _deferPromiseButton.Disabled = !promise.CanChoose;
            }
            SetProductButton(CommercialProductAction.ApproveWindow, product.ApproveWindow);
            SetProductButton(
                CommercialProductAction.RollbackRecentConstruction,
                product.RollbackRecentConstruction);
            SetProductButton(CommercialProductAction.RestartWindow, product.RestartWindow);
            SetProductButton(CommercialProductAction.RestartChapter, product.RestartChapter);
            SetProductButton(
                CommercialProductAction.RewindPreviousChapter,
                product.RewindPreviousChapter);
        }
        AccessibilityName = model.Product is not null
            ? $"운영안 작업 패널. {model.Heading}. {model.Status}"
            : projectionVisible
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

    public string ObligationsText => _obligationsLabel.Text;

    public BaseButton GetProductActionButton(CommercialProductAction action) =>
        _productButtons[action];

    public BaseButton GetPromiseButton(CommercialPromiseDecision decision) => decision switch
    {
        CommercialPromiseDecision.Keep => _keepPromiseButton,
        CommercialPromiseDecision.Defer => _deferPromiseButton,
        _ => throw new ArgumentOutOfRangeException(nameof(decision)),
    };

    private void SetButton(
        CommercialPanelAction action,
        CommercialActionPresentation presentation)
    {
        Button button = _buttons[action];
        button.Visible = presentation.Visible;
        button.Disabled = !presentation.Enabled;
        button.Text = presentation.Text;
        button.AccessibilityName = presentation.Text;
        button.AccessibilityDescription = presentation.Description;
    }

    private void SetProductButton(
        CommercialProductAction action,
        CommercialActionPresentation presentation)
    {
        Button button = _productButtons[action];
        button.Visible = presentation.Visible;
        button.Disabled = !presentation.Enabled;
        button.Text = presentation.Text;
        button.AccessibilityName = presentation.Text;
        button.AccessibilityDescription = presentation.Description;
    }
}
