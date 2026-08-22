using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Gridworks.Game.Realtime.UI;

internal sealed partial class RealtimeContextDock : PanelContainer
{
    private MarginContainer _margin = null!;
    private VBoxContainer _column = null!;
    private Label _eyebrow = null!;
    private Label _heading = null!;
    private Button _close = null!;
    private GridContainer _summarySections = null!;
    private VBoxContainer _details = null!;
    private HBoxContainer _detailTabs = null!;
    private ScrollContainer _detailScroll = null!;
    private VBoxContainer _detailRows = null!;
    private Button _overviewTab = null!;
    private Control _footer = null!;
    private Button _secondary = null!;
    private Button _primary = null!;
    private string? _subjectId;
    private string? _primaryActionId;
    private string? _secondaryActionId;
    private RealtimeContextDetailTab? _selectedTab;
    private IReadOnlyList<RealtimeContextDetailPresentation> _detailPresentations =
        Array.Empty<RealtimeContextDetailPresentation>();
    private readonly Dictionary<RealtimeContextDetailTab, Button> _tabButtons = [];
    private readonly ButtonGroup _tabGroup = new() { AllowUnpress = false };
    private readonly List<SectionCard> _summaryCards = [];
    private readonly List<SectionCard> _detailCards = [];
    private bool _compactDetails;
    private bool _showingOverview = true;

    public event Action? CloseRequested;
    public event Action<string>? ActionRequested;

    public ScrollContainer DetailScroll => _detailScroll;

    public override void _Ready()
    {
        _margin = GetNode<MarginContainer>("Margin");
        _column = GetNode<VBoxContainer>("Margin/Column");
        _eyebrow = GetNode<Label>("%EyebrowLabel");
        _heading = GetNode<Label>("%HeadingLabel");
        _close = GetNode<Button>("%CloseButton");
        _summarySections = GetNode<GridContainer>("%SummarySections");
        _details = GetNode<VBoxContainer>("%Details");
        _detailTabs = GetNode<HBoxContainer>("%DetailTabs");
        _detailScroll = GetNode<ScrollContainer>("%DetailScroll");
        _detailRows = GetNode<VBoxContainer>("%DetailRows");
        _footer = GetNode<Control>("%Footer");
        _secondary = GetNode<Button>("%SecondaryButton");
        _primary = GetNode<Button>("%PrimaryButton");
        _overviewTab = new Button
        {
            Text = "요약",
            ToggleMode = true,
            ButtonGroup = _tabGroup,
            ThemeTypeVariation = "ToolButton",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AccessibilityName = "상황 요약 탭",
        };
        _overviewTab.Pressed += SelectOverview;
        _detailTabs.AddChild(_overviewTab);
        _close.Pressed += () => CloseRequested?.Invoke();
        _close.AccessibilityName = "상황 패널 닫기";
        _close.AccessibilityDescription =
            "현재 선택은 유지하고 상황 패널을 닫아 지도에 집중합니다.";
        _primary.Pressed += () => RequestAction(_primaryActionId);
        _secondary.Pressed += () => RequestAction(_secondaryActionId);
    }

    internal void ReflowToAssignedSize()
    {
        if (!IsInsideTree() || Size.X <= 0f || Size.Y <= 0f)
        {
            return;
        }
        // PanelContainer may retain the child's former expanded allocation
        // when native fullscreen or UI density shrinks this top-level panel.
        // Its combined minimum can already be below the new budget while the
        // stale Margin/VBox rect still protrudes. Fit the direct child to the
        // authoritative panel rect, then explicitly resort each nested
        // container that shares the vertical budget.
        FitChildInRect(_margin, new Rect2(Vector2.Zero, Size));
        _margin.QueueSort();
        _column.QueueSort();
        _summarySections.QueueSort();
        _details.QueueSort();
        _detailTabs.QueueSort();
        _detailScroll.QueueSort();
        _detailRows.QueueSort();
        if (_footer is Container footerContainer)
        {
            footerContainer.QueueSort();
        }
    }

    public void SetPresentation(RealtimeContextDockPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        Visible = presentation.Visible;
        if (!presentation.Visible)
        {
            return;
        }

        bool changedSubject = !string.Equals(
            _subjectId,
            presentation.SubjectId,
            StringComparison.Ordinal);
        _subjectId = presentation.SubjectId;
        if (presentation.Sections.Count is < 1 or > 4)
        {
            throw new ArgumentException(
                "The fixed inspector summary requires one to four sections.",
                nameof(presentation));
        }
        _eyebrow.Text = presentation.Eyebrow;
        _heading.Text = presentation.Heading;
        RenderSectionCards(
            _summarySections,
            _summaryCards,
            presentation.Sections.Select(section =>
                (section.Heading, section.Body, section.Severity)));
        RebuildDetails(presentation.Details, changedSubject);
        SetAction(_primary, presentation.PrimaryAction, out _primaryActionId);
        SetAction(_secondary, presentation.SecondaryAction, out _secondaryActionId);
        _footer.Visible = _primary.Visible || _secondary.Visible;
        AccessibilityName =
            $"상황 패널. {presentation.Eyebrow}. {presentation.Heading}. " +
            string.Join(". ", presentation.Sections.Select(section =>
                $"{section.Heading}. {section.Body}")) +
            (presentation.Details.Count == 0
                ? string.Empty
                : ". 상세 정보는 경로, 열, 예측, 이력 탭으로 분리되어 있습니다.");
    }

    public void ApplyLayout(RealtimeLayoutProfile profile)
    {
        CustomMinimumSize = new Vector2(profile.ContextDockWidth, 0f);
        _close.CustomMinimumSize = Vector2.One * profile.MinimumHitTarget;
        _secondary.CustomMinimumSize = new Vector2(0f, profile.MinimumHitTarget);
        _primary.CustomMinimumSize = new Vector2(0f, profile.PrimaryHitTarget);
        _summarySections.Columns = profile.ContextDockWidth >= 800
            ? 3
            : profile.ContextDockWidth >= 600
                ? 2
                : 1;
        bool compactDetails = profile.AccessibilityScale >= 2f;
        if (compactDetails != _compactDetails)
        {
            _compactDetails = compactDetails;
            _showingOverview = true;
        }
        _overviewTab.Visible = _compactDetails && _detailPresentations.Count > 0;
        _overviewTab.CustomMinimumSize = new Vector2(0f, profile.MinimumHitTarget);
        foreach (Button tab in _tabButtons.Values)
        {
            tab.CustomMinimumSize = new Vector2(0f, profile.MinimumHitTarget);
        }
        ApplyDetailModeVisibility();
    }

    private void RebuildDetails(
        IReadOnlyList<RealtimeContextDetailPresentation> details,
        bool changedSubject)
    {
        _detailPresentations = details;
        _details.Visible = details.Count > 0;
        if (details.Count == 0)
        {
            _selectedTab = null;
            foreach (Button button in _tabButtons.Values)
            {
                _detailTabs.RemoveChild(button);
                button.QueueFree();
            }
            _tabButtons.Clear();
            ClearCards(_detailRows, _detailCards);
            ApplyDetailModeVisibility();
            return;
        }

        RealtimeContextDetailTab[] available = details
            .Select(item => item.Tab)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();
        if (changedSubject || !_selectedTab.HasValue || !available.Contains(_selectedTab.Value))
        {
            _selectedTab = available[0];
            _showingOverview = true;
        }
        foreach (RealtimeContextDetailTab obsolete in _tabButtons.Keys
                     .Where(tab => !available.Contains(tab))
                     .ToArray())
        {
            Button button = _tabButtons[obsolete];
            _detailTabs.RemoveChild(button);
            button.QueueFree();
            _tabButtons.Remove(obsolete);
        }
        for (int index = 0; index < available.Length; index++)
        {
            RealtimeContextDetailTab tab = available[index];
            if (!_tabButtons.TryGetValue(tab, out Button? button))
            {
                button = new Button
                {
                    Text = TabLabel(tab),
                    ToggleMode = true,
                    ButtonGroup = _tabGroup,
                    ThemeTypeVariation = "ToolButton",
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    AccessibilityName = $"상황 상세 {TabLabel(tab)} 탭",
                };
                RealtimeContextDetailTab captured = tab;
                button.Pressed += () => SelectTab(captured);
                _tabButtons.Add(tab, button);
                _detailTabs.AddChild(button);
            }
            button.ButtonPressed = tab == _selectedTab;
            button.CustomMinimumSize = new Vector2(
                0f,
                Math.Max(44f, _close.CustomMinimumSize.Y));
            _detailTabs.MoveChild(button, index + 1);
        }
        RenderSelectedDetails();
        ApplyDetailModeVisibility();
    }

    private void SelectOverview()
    {
        if (!_compactDetails || _detailPresentations.Count == 0)
        {
            return;
        }
        _showingOverview = true;
        ApplyDetailModeVisibility();
    }

    private void SelectTab(RealtimeContextDetailTab tab)
    {
        if (!_tabButtons.ContainsKey(tab))
        {
            return;
        }
        _selectedTab = tab;
        _showingOverview = false;
        foreach ((RealtimeContextDetailTab key, Button value) in _tabButtons)
        {
            value.ButtonPressed = key == tab;
        }
        RenderSelectedDetails();
        ApplyDetailModeVisibility();
        _detailScroll.ScrollVertical = 0;
        _detailScroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, 0);
    }

    private void RenderSelectedDetails()
    {
        if (!_selectedTab.HasValue)
        {
            ClearCards(_detailRows, _detailCards);
            return;
        }
        RenderSectionCards(
            _detailRows,
            _detailCards,
            _detailPresentations
                .Where(item => item.Tab == _selectedTab.Value)
                .Select(item => (item.Heading, item.Body, item.Severity)));
    }

    private void ApplyDetailModeVisibility()
    {
        bool hasDetails = _detailPresentations.Count > 0;
        bool overviewActive = _compactDetails && _showingOverview;
        _details.Visible = hasDetails;
        _overviewTab.Visible = _compactDetails && hasDetails;
        _overviewTab.ButtonPressed = overviewActive;
        _summarySections.Visible = !_compactDetails || overviewActive || !hasDetails;
        _detailScroll.Visible = hasDetails && !overviewActive;
        foreach ((RealtimeContextDetailTab tab, Button button) in _tabButtons)
        {
            button.ButtonPressed = !overviewActive && tab == _selectedTab;
        }
    }

    private static void RenderSectionCards(
        Container target,
        List<SectionCard> cards,
        IEnumerable<(string Heading, string Body, RealtimeTimelineSeverity Severity)> sections)
    {
        (string Heading, string Body, RealtimeTimelineSeverity Severity)[] rows =
            sections.ToArray();
        while (cards.Count < rows.Length)
        {
            var panel = new PanelContainer
            {
                ThemeTypeVariation = "ElevatedPanel",
                MouseFilter = MouseFilterEnum.Ignore,
            };
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 14);
            margin.AddThemeConstantOverride("margin_top", 12);
            margin.AddThemeConstantOverride("margin_right", 14);
            margin.AddThemeConstantOverride("margin_bottom", 12);
            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", 7);
            var heading = new Label
            {
                MouseFilter = MouseFilterEnum.Ignore,
            };
            var body = new Label
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            column.AddChild(heading);
            column.AddChild(body);
            margin.AddChild(column);
            panel.AddChild(margin);
            target.AddChild(panel);
            cards.Add(new SectionCard(panel, heading, body));
        }
        while (cards.Count > rows.Length)
        {
            SectionCard obsolete = cards[^1];
            cards.RemoveAt(cards.Count - 1);
            target.RemoveChild(obsolete.Panel);
            obsolete.Panel.QueueFree();
        }
        for (int index = 0; index < rows.Length; index++)
        {
            (string sectionHeading, string sectionBody,
                RealtimeTimelineSeverity sectionSeverity) = rows[index];
            SectionCard card = cards[index];
            card.Heading.Text =
                $"{SeverityIcon(sectionSeverity)} {sectionHeading} · " +
                SeverityLabel(sectionSeverity);
            card.Heading.AddThemeColorOverride(
                "font_color",
                SeverityColor(sectionSeverity));
            card.Body.Text = sectionBody;
            card.Body.AccessibilityName =
                $"{SeverityLabel(sectionSeverity)}. {sectionHeading}. {sectionBody}";
            target.MoveChild(card.Panel, index);
        }
    }

    private static void ClearCards(Container parent, List<SectionCard> cards)
    {
        foreach (SectionCard card in cards)
        {
            parent.RemoveChild(card.Panel);
            card.Panel.QueueFree();
        }
        cards.Clear();
    }

    private static string TabLabel(RealtimeContextDetailTab tab) => tab switch
    {
        RealtimeContextDetailTab.Route => "경로",
        RealtimeContextDetailTab.Thermal => "열",
        RealtimeContextDetailTab.Forecast => "예측",
        RealtimeContextDetailTab.History => "이력",
        _ => throw new ArgumentOutOfRangeException(nameof(tab)),
    };

    private static void SetAction(
        Button button,
        RealtimeActionPresentation? presentation,
        out string? actionId)
    {
        actionId = presentation?.Id;
        button.Visible = presentation?.Visible == true;
        if (presentation is null)
        {
            return;
        }
        button.Text = presentation.Label;
        button.Disabled = !presentation.Enabled;
        button.TooltipText = presentation.Description;
        button.AccessibilityName = presentation.Label;
        button.AccessibilityDescription = presentation.Description;
        button.ThemeTypeVariation = ActionTheme(presentation.Tone);
    }

    private void RequestAction(string? actionId)
    {
        if (actionId is not null)
        {
            ActionRequested?.Invoke(actionId);
        }
    }

    private static StringName ActionTheme(RealtimeActionTone tone) => tone switch
    {
        RealtimeActionTone.Primary => "PrimaryButton",
        RealtimeActionTone.Secondary => "Button",
        RealtimeActionTone.Destructive => "DestructiveButton",
        _ => throw new ArgumentOutOfRangeException(nameof(tone)),
    };

    private static Color SeverityColor(RealtimeTimelineSeverity severity) => severity switch
    {
        RealtimeTimelineSeverity.Information => Color.FromHtml("83d0c8"),
        RealtimeTimelineSeverity.Advisory => Color.FromHtml("d6ba70"),
        RealtimeTimelineSeverity.Warning => Color.FromHtml("e28a55"),
        RealtimeTimelineSeverity.Critical => Color.FromHtml("ed756e"),
        _ => throw new ArgumentOutOfRangeException(nameof(severity)),
    };

    private static string SeverityIcon(RealtimeTimelineSeverity severity) => severity switch
    {
        RealtimeTimelineSeverity.Information => "●",
        RealtimeTimelineSeverity.Advisory => "◆",
        RealtimeTimelineSeverity.Warning => "▲",
        RealtimeTimelineSeverity.Critical => "■",
        _ => throw new ArgumentOutOfRangeException(nameof(severity)),
    };

    private static string SeverityLabel(RealtimeTimelineSeverity severity) => severity switch
    {
        RealtimeTimelineSeverity.Information => "정보",
        RealtimeTimelineSeverity.Advisory => "주의",
        RealtimeTimelineSeverity.Warning => "경고",
        RealtimeTimelineSeverity.Critical => "위험",
        _ => throw new ArgumentOutOfRangeException(nameof(severity)),
    };

    private sealed record SectionCard(
        PanelContainer Panel,
        Label Heading,
        Label Body);
}
