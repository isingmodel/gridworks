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
    StoreNextProjectComparison,
    ClearNextProjectComparison,
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

internal sealed record CommercialApprovalChecklistPresentation(
    string Id,
    bool Passed,
    string Label,
    string Description,
    bool CanInspect);

internal sealed record CommercialPhaseComparisonPresentation(
    string Id,
    string Cells,
    string Description,
    bool CanInspect);

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
    string ApprovalHeading,
    IReadOnlyList<CommercialApprovalChecklistPresentation> ApprovalChecklist,
    IReadOnlyList<CommercialPhaseComparisonPresentation> PhaseComparisons,
    string RecoveryPreview,
    CommercialActionPresentation StoreNextProjectComparison,
    CommercialActionPresentation ClearNextProjectComparison,
    CommercialPromisePresentation? Promise,
    CommercialActionPresentation ApproveWindow,
    CommercialActionPresentation RollbackRecentConstruction,
    CommercialActionPresentation RestartWindow,
    CommercialActionPresentation RestartChapter,
    CommercialActionPresentation RewindPreviousChapter,
    IReadOnlyList<CommercialCampaignChapterReplayOption> ChapterReplayOptions);

internal sealed record CommercialTaskPanelModel(
    string Heading,
    string Instruction,
    string Selection,
    string Quote,
    string Status,
    string Error,
    string ToolStatus,
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
    private Label _toolStatusLabel = null!;
    private Button _toolPaletteButton = null!;
    private ScrollContainer _infoScroll = null!;
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
    private Control _phaseComparisonSection = null!;
    private Control _phaseComparisonRows = null!;
    private Button _legendToggleButton = null!;
    private Label _legendLabel = null!;
    private Label _recoveryPreviewLabel = null!;
    private Control _promiseControls = null!;
    private Label _promiseHeading = null!;
    private Label _promiseStatus = null!;
    private Button _keepPromiseButton = null!;
    private Button _deferPromiseButton = null!;
    private Control _productActions = null!;
    private Control _approvalChecklistSection = null!;
    private Label _approvalChecklistHeading = null!;
    private Control _approvalChecklistRows = null!;
    private Control _chapterReplaySection = null!;
    private OptionButton _chapterReplayOption = null!;
    private Button _chapterReplayButton = null!;
    private IReadOnlyList<CommercialCampaignChapterReplayOption> _chapterReplayOptions =
        Array.Empty<CommercialCampaignChapterReplayOption>();
    private IReadOnlyDictionary<CommercialProductAction, Button> _productButtons = null!;
    private IReadOnlyDictionary<CommercialPanelAction, Button> _buttons = null!;
    private readonly Dictionary<string, Button> _approvalRowButtons =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _phaseRowButtons =
        new(StringComparer.Ordinal);

    public event Action<CommercialPanelAction>? ActionRequested;

    public event Action<int>? ProjectionDeltaRequested;
    public event Action<CommercialPromiseDecision>? PromiseRequested;
    public event Action<CommercialProductAction>? ProductActionRequested;
    public event Action<string>? ChapterReplayRequested;
    public event Action<string>? ApprovalChecklistRequested;
    public event Action<string>? PhaseComparisonRequested;

    public override void _Ready()
    {
        _headingLabel = GetNode<Label>("%HeadingLabel");
        _toolStatusLabel = GetNode<Label>("%ToolStatusLabel");
        _toolPaletteButton = GetNode<Button>("%ToolPaletteButton");
        _infoScroll = GetNode<ScrollContainer>("%InfoScroll");
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
        _phaseComparisonSection = GetNode<Control>("%PhaseComparisonSection");
        _phaseComparisonRows = GetNode<Control>("%PhaseComparisonRows");
        _legendToggleButton = GetNode<Button>("%LegendToggleButton");
        _legendLabel = GetNode<Label>("%LegendLabel");
        _recoveryPreviewLabel = GetNode<Label>("%RecoveryPreviewLabel");
        _promiseControls = GetNode<Control>("%PromiseControls");
        _promiseHeading = GetNode<Label>("%PromiseHeading");
        _promiseStatus = GetNode<Label>("%PromiseStatus");
        _keepPromiseButton = GetNode<Button>("%KeepPromiseButton");
        _deferPromiseButton = GetNode<Button>("%DeferPromiseButton");
        _productActions = GetNode<Control>("%ProductActions");
        _approvalChecklistSection = GetNode<Control>("%ApprovalChecklistSection");
        _approvalChecklistHeading = GetNode<Label>("%ApprovalChecklistHeading");
        _approvalChecklistRows = GetNode<Control>("%ApprovalChecklistRows");
        _chapterReplaySection = GetNode<Control>("%ChapterReplaySection");
        _chapterReplayOption = GetNode<OptionButton>("%ChapterReplayOption");
        _chapterReplayButton = GetNode<Button>("%ChapterReplayButton");
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
            [CommercialProductAction.StoreNextProjectComparison] =
                GetNode<Button>("%StoreNextComparisonButton"),
            [CommercialProductAction.ClearNextProjectComparison] =
                GetNode<Button>("%ClearNextComparisonButton"),
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
        _chapterReplayButton.Pressed += RequestSelectedChapterReplay;
        _toolPaletteButton.Pressed += FocusToolPalette;
        _legendToggleButton.Pressed += ToggleLegend;
        _statusLabel.AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Polite;
        _errorLabel.AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Assertive;
        _toolStatusLabel.AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Polite;
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
        _toolStatusLabel.Text = model.ToolStatus;
        _toolStatusLabel.AccessibilityName = $"현재 도구와 상태. {model.ToolStatus}";
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
        _approvalChecklistSection.Visible = model.Product?.ApprovalChecklist.Count > 0;
        if (model.Product is CommercialProductPanelPresentation product)
        {
            _objectiveLabel.Text = product.Objective;
            _obligationsLabel.Text = string.Join("\n", product.Obligations.Select(item =>
                $"{item.Status} · {item.Label}"));
            _deadlineLabel.Text = product.Deadline;
            _approvalChecklistHeading.Text = product.ApprovalHeading;
            SetApprovalChecklist(product.ApprovalChecklist);
            SetPhaseComparisons(product.PhaseComparisons);
            _phaseComparisonSection.Visible = product.PhaseComparisons.Count > 0;
            _recoveryPreviewLabel.Text = product.RecoveryPreview;
            _recoveryPreviewLabel.AccessibilityName = product.RecoveryPreview;
            SetProductButton(
                CommercialProductAction.StoreNextProjectComparison,
                product.StoreNextProjectComparison);
            SetProductButton(
                CommercialProductAction.ClearNextProjectComparison,
                product.ClearNextProjectComparison);
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
            SetChapterReplayOptions(product.ChapterReplayOptions);
        }
        else
        {
            SetChapterReplayOptions(Array.Empty<CommercialCampaignChapterReplayOption>());
            SetApprovalChecklist(Array.Empty<CommercialApprovalChecklistPresentation>());
            SetPhaseComparisons(Array.Empty<CommercialPhaseComparisonPresentation>());
        }
        AccessibilityName = model.Product is not null
            ? $"운영안 작업 패널. {model.Heading}. {model.Status}"
            : projectionVisible
                ? $"열 운전 확인 패널. {model.Heading}. {model.Status}"
                : $"공사 작업 패널. {model.Heading}. {model.Status}";
    }

#if DEBUG
    public BaseButton GetActionButton(CommercialPanelAction action) => _buttons[action];

    public BaseButton GetProjectionButton(int direction) => direction < 0
        ? _previousProjectionButton
        : _nextProjectionButton;

    public string SelectionText => _selectionLabel.Text;

    public string LimitsText => _quoteLabel.Text;

    public string StatusText => _statusLabel.Text;

    public string ProjectionText => _projectionLabel.Text;

    public string ObligationsText => _obligationsLabel.Text;

    public string ToolStatusText => _toolStatusLabel.Text;

    public string ApprovalChecklistHeadingText => _approvalChecklistHeading.Text;

    public string ApprovalChecklistText => string.Join("\n", _approvalChecklistRows
        .GetChildren()
        .OfType<Button>()
        .Select(button => button.Text));

    public string PhaseComparisonText => string.Join("\n", _phaseComparisonRows
        .GetChildren()
        .OfType<Button>()
        .Select(button => button.Text));

    public float InfoViewportMinimumHeight => _infoScroll.CustomMinimumSize.Y;

    public BaseButton GetApprovalChecklistButton(string itemId) =>
        _approvalRowButtons[itemId];

    public BaseButton GetPhaseComparisonButton(string rowId) =>
        _phaseRowButtons[rowId];

    public BaseButton GetProductActionButton(CommercialProductAction action) =>
        _productButtons[action];

    public BaseButton GetPromiseButton(CommercialPromiseDecision decision) => decision switch
    {
        CommercialPromiseDecision.Keep => _keepPromiseButton,
        CommercialPromiseDecision.Defer => _deferPromiseButton,
        _ => throw new ArgumentOutOfRangeException(nameof(decision)),
    };

    public BaseButton ChapterReplayButton => _chapterReplayButton;

    public int ChapterReplayOptionCount => _chapterReplayOptions.Count;

    public string? SelectedChapterReplayId => CurrentSelectedChapterReplayId();

    public bool SelectChapterReplayOption(string chapterId)
    {
        int index = _chapterReplayOptions.ToList().FindIndex(option =>
            string.Equals(option.ChapterId, chapterId, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }
        _chapterReplayOption.Select(index);
        return true;
    }
#endif

    private string? CurrentSelectedChapterReplayId() =>
        _chapterReplayOption.Selected >= 0 &&
        _chapterReplayOption.Selected < _chapterReplayOptions.Count
            ? _chapterReplayOptions[_chapterReplayOption.Selected].ChapterId
            : null;

    public void FocusPromiseDecision()
    {
        Button target = !_keepPromiseButton.Disabled
            ? _keepPromiseButton
            : _deferPromiseButton;
        target.CallDeferred(Control.MethodName.GrabFocus);
    }

    public void FocusConstructionResolution()
    {
        Button target = !_buttons[CommercialPanelAction.Commission].Disabled
            ? _buttons[CommercialPanelAction.Commission]
            : !_buttons[CommercialPanelAction.CancelDraft].Disabled
                ? _buttons[CommercialPanelAction.CancelDraft]
                : _toolPaletteButton;
        target.CallDeferred(Control.MethodName.GrabFocus);
    }

    public void FocusRecoveryResolution()
    {
        Button target = !_productButtons[CommercialProductAction.RollbackRecentConstruction].Disabled
            ? _productButtons[CommercialProductAction.RollbackRecentConstruction]
            : !_productButtons[CommercialProductAction.RestartWindow].Disabled
                ? _productButtons[CommercialProductAction.RestartWindow]
                : !_productButtons[CommercialProductAction.RestartChapter].Disabled
                    ? _productButtons[CommercialProductAction.RestartChapter]
                    : _toolPaletteButton;
        target.CallDeferred(Control.MethodName.GrabFocus);
    }

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

    private void SetChapterReplayOptions(
        IReadOnlyList<CommercialCampaignChapterReplayOption> options)
    {
        string? previouslySelected = CurrentSelectedChapterReplayId();
        CommercialCampaignChapterReplayOption[] incoming = options.ToArray();
        bool sameOptions = _chapterReplayOptions.SequenceEqual(incoming);
        _chapterReplayOptions = incoming;
        if (!sameOptions)
        {
            _chapterReplayOption.Clear();
            foreach (CommercialCampaignChapterReplayOption option in _chapterReplayOptions)
            {
                _chapterReplayOption.AddItem($"{option.ChapterIndex + 1}장 · {option.DisplayName}");
            }
        }
        int selectedIndex = previouslySelected is null
            ? 0
            : _chapterReplayOptions.ToList().FindIndex(option => string.Equals(
                option.ChapterId,
                previouslySelected,
                StringComparison.Ordinal));
        if (!sameOptions && _chapterReplayOptions.Count > 0)
        {
            _chapterReplayOption.Select(Math.Max(0, selectedIndex));
        }
        bool visible = _chapterReplayOptions.Count > 0;
        _chapterReplaySection.Visible = visible;
        _chapterReplayButton.Disabled = !visible;
        _chapterReplayOption.AccessibilityName = "다시 시작할 완료 장";
        _chapterReplayButton.AccessibilityDescription =
            "선택한 장의 시작 상태로 돌아가 이후 도시망을 다시 설계합니다.";
    }

    private void SetApprovalChecklist(
        IReadOnlyList<CommercialApprovalChecklistPresentation> items)
    {
        RemoveStaleRows(
            _approvalChecklistRows,
            _approvalRowButtons,
            items.Select(item => item.Id));
        for (int index = 0; index < items.Count; index++)
        {
            CommercialApprovalChecklistPresentation item = items[index];
            if (!_approvalRowButtons.TryGetValue(item.Id, out Button? button))
            {
                button = new Button
                {
                    Flat = true,
                    Alignment = HorizontalAlignment.Left,
                    FocusMode = FocusModeEnum.All,
                    ClipText = true,
                    TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                };
                string id = item.Id;
                button.Pressed += () => ApprovalChecklistRequested?.Invoke(id);
                _approvalRowButtons.Add(id, button);
                _approvalChecklistRows.AddChild(button);
            }
            button.Text = $"{(item.Passed ? "✓" : "×")} {item.Label}";
            button.Disabled = !item.CanInspect;
            button.AccessibilityName =
                $"{(item.Passed ? "통과" : "미통과")}. {item.Label}";
            button.AccessibilityDescription = item.Description;
            button.TooltipText = item.Description;
            button.CustomMinimumSize = new Vector2(0f, 30f);
            _approvalChecklistRows.MoveChild(button, index);
        }
        _approvalChecklistSection.AccessibilityName =
            _approvalChecklistHeading.Text + ". " +
            string.Join(". ", items.Select(item =>
                $"{(item.Passed ? "통과" : "미통과")} {item.Label}"));
    }

    private void SetPhaseComparisons(
        IReadOnlyList<CommercialPhaseComparisonPresentation> rows)
    {
        RemoveStaleRows(
            _phaseComparisonRows,
            _phaseRowButtons,
            rows.Select(row => row.Id));
        for (int index = 0; index < rows.Count; index++)
        {
            CommercialPhaseComparisonPresentation row = rows[index];
            if (!_phaseRowButtons.TryGetValue(row.Id, out Button? button))
            {
                button = new Button
                {
                    Flat = true,
                    Alignment = HorizontalAlignment.Left,
                    FocusMode = FocusModeEnum.All,
                    ClipText = true,
                    TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                };
                string id = row.Id;
                button.Pressed += () => PhaseComparisonRequested?.Invoke(id);
                _phaseRowButtons.Add(id, button);
                _phaseComparisonRows.AddChild(button);
            }
            button.Text = row.Cells;
            button.Disabled = !row.CanInspect;
            button.AccessibilityName = row.Description;
            button.AccessibilityDescription =
                "이 행을 선택하면 같은 경로와 제한 설비를 지도에서 확인합니다.";
            button.TooltipText = row.Description;
            button.CustomMinimumSize = new Vector2(0f, 36f);
            _phaseComparisonRows.MoveChild(button, index);
        }
        _phaseComparisonSection.AccessibilityName =
            "수요와 운영 국면 비교 표. 열 제목: 수요, 국면, 공급원, 공급, 최소 여유, 현재와 다음 상태. " +
            string.Join(". ", rows.Select(row => row.Description));
    }

    private static void RemoveStaleRows(
        Control parent,
        IDictionary<string, Button> buttons,
        IEnumerable<string> retainedIds)
    {
        HashSet<string> retained = retainedIds.ToHashSet(StringComparer.Ordinal);
        foreach (string id in buttons.Keys.Where(id => !retained.Contains(id)).ToArray())
        {
            Button button = buttons[id];
            parent.RemoveChild(button);
            button.QueueFree();
            buttons.Remove(id);
        }
    }

    private void FocusToolPalette()
    {
        Button? firstAvailable = _buttons
            .Where(pair => pair.Key is CommercialPanelAction.PlaceSubstation or
                CommercialPanelAction.PlaceLargeSubstation or
                CommercialPanelAction.StartStandardLine or CommercialPanelAction.StartLine)
            .Select(pair => pair.Value)
            .FirstOrDefault(button => button.Visible && !button.Disabled);
        firstAvailable?.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void ToggleLegend()
    {
        _legendLabel.Visible = !_legendLabel.Visible;
        _legendToggleButton.Text = _legendLabel.Visible
            ? "운전 상태 범례 닫기"
            : "운전 상태 범례 열기";
        _legendToggleButton.AccessibilityDescription = _legendLabel.Visible
            ? "일반어 운전 상태 범례가 아래에 열려 있습니다."
            : "연속 운전, 비상 운전, 보호정지, 현재 미사용과 단절의 뜻을 엽니다.";
    }

    private void RequestSelectedChapterReplay()
    {
        if (CurrentSelectedChapterReplayId() is string chapterId)
        {
            ChapterReplayRequested?.Invoke(chapterId);
        }
    }
}
