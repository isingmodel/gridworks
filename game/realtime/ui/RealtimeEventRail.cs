using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Gridworks.Game.Realtime.UI;

/// <summary>
/// A clipped, non-scrolling four-lane event horizon. Core/presenter order is
/// preserved as minute, priority, then stable ID. Lane placement never changes
/// keyboard or accessibility order.
/// </summary>
internal sealed partial class RealtimeEventRail : PanelContainer
{
    private const float BaseMarkerWidth = 300f;
    private const float BaseClusterWidth = 300f;
    private const float BaseLaneGap = 4f;
    private const int BaseMarkerFontSize = 14;

    private Label _nowLabel = null!;
    private Label _horizonLabel = null!;
    private VBoxContainer _laneLabels = null!;
    private Control _track = null!;
    private Button _previousEvent = null!;
    private Button _currentTime = null!;
    private Button _nextEvent = null!;
    private OptionButton _accessibleEventSelector = null!;
    private Button _shorterHorizon = null!;
    private Button _longerHorizon = null!;
    private Button _expandLanes = null!;
    private RealtimeEventRailPresentation? _presentation;
    private string _presentationSignature = string.Empty;
    private readonly List<MarkerEntry> _markers = [];
    private readonly ButtonGroup _markerGroup = new() { AllowUnpress = false };
    private float _markerWidth = BaseMarkerWidth;
    private float _clusterWidth = BaseClusterWidth;
    private float _markerHeight = 44f;
    private float _laneGap = BaseLaneGap;
    private RealtimeLayoutProfile _layoutProfile;
    private bool _expanded = true;
    private bool _focusSelectedOnNextPresentation;
    private string? _lastSemanticFocusItemId;
    private string? _pendingRestoreFocusItemId;
    private bool _markerLayoutScheduled;
    private bool _updatingAccessibleEventSelector;
    private readonly List<string> _accessibleItemIds = [];

    public event Action<int>? HorizonDeltaRequested;
    public event Action<IReadOnlyList<string>>? ItemsRequested;
    public event Action<RealtimeTimelineNavigation>? NavigationRequested;
    public event Action<int>? DesiredHeightChanged;
    public event Action<bool>? ExpansionRequested;

    public int VisibleLaneCount => _expanded ? 4 : 2;

    public int DesiredHeight => RealtimeUiMetrics.EventRailHeight(
        _layoutProfile.AccessibilityScale <= 0f ? 1f : _layoutProfile.AccessibilityScale,
        _layoutProfile.MinimumHitTarget <= 0 ? 44 : _layoutProfile.MinimumHitTarget,
        VisibleLaneCount);

    public IReadOnlyList<string> LinearItemIds =>
        Array.AsReadOnly(_accessibleItemIds.ToArray());

    public override void _Ready()
    {
        _nowLabel = GetNode<Label>("%NowLabel");
        _horizonLabel = GetNode<Label>("%HorizonLabel");
        _laneLabels = GetNode<VBoxContainer>("Margin/Row/LaneLabels");
        _track = GetNode<Control>("%Track");
        _previousEvent = GetNode<Button>("%PreviousEventButton");
        _currentTime = GetNode<Button>("%CurrentTimeButton");
        _nextEvent = GetNode<Button>("%NextEventButton");
        _accessibleEventSelector = GetNode<OptionButton>("%AccessibleEventSelector");
        _shorterHorizon = GetNode<Button>("%ShorterHorizonButton");
        _longerHorizon = GetNode<Button>("%LongerHorizonButton");
        _expandLanes = GetNode<Button>("%ExpandLanesButton");
        _previousEvent.Pressed += () => Navigate(RealtimeTimelineNavigation.PreviousEvent);
        _currentTime.Pressed += () => Navigate(RealtimeTimelineNavigation.Home);
        _nextEvent.Pressed += () => Navigate(RealtimeTimelineNavigation.NextEvent);
        _accessibleEventSelector.ItemSelected += HandleAccessibleEventSelected;
        _shorterHorizon.Pressed += () => HorizonDeltaRequested?.Invoke(-1);
        _longerHorizon.Pressed += () => HorizonDeltaRequested?.Invoke(1);
        _expandLanes.Pressed += ToggleExpansion;
        _previousEvent.AccessibilityName = "이전 사건";
        _previousEvent.AccessibilityDescription = "시간순으로 이전 사건을 지도와 상황 패널에서 엽니다.";
        _currentTime.AccessibilityName = "현재 시각";
        _currentTime.AccessibilityDescription = "사건 선택을 지우고 현재 시각으로 돌아갑니다.";
        _nextEvent.AccessibilityName = "다음 사건";
        _nextEvent.AccessibilityDescription = "시간순으로 다음 사건을 지도와 상황 패널에서 엽니다.";
        _accessibleEventSelector.AccessibilityName = "시간순 사건 목록";
        _accessibleEventSelector.AccessibilityDescription =
            "각 항목은 사건 한 건입니다. 선택하면 같은 사건을 지도와 상황 패널에서 엽니다.";
        _shorterHorizon.AccessibilityName = "사건 지평선을 더 짧게 보기";
        _longerHorizon.AccessibilityName = "사건 지평선을 더 길게 보기";
        _expandLanes.AccessibilityName = "사건 지평선 줄 수 바꾸기";
        _track.Resized += () => ScheduleMarkerLayout();
        RebuildLaneLabels();
    }

    public void SetPresentation(RealtimeEventRailPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (presentation.HorizonEndMinute <= presentation.HorizonStartMinute)
        {
            throw new ArgumentException(
                "Timeline horizon must have positive duration.",
                nameof(presentation));
        }
        string signature = Signature(presentation);
        if (string.Equals(signature, _presentationSignature, StringComparison.Ordinal))
        {
            return;
        }
        string? focusedItemId = FocusedItemId();
        // A passive realtime refresh must not move keyboard focus from the
        // marker the player is reading merely because another item is selected
        // on the map. Explicit rail navigation or marker selection is the sole
        // exception: its next presentation deliberately follows the
        // controller-selected stable ID.
        string? restoreFocusItemId = _focusSelectedOnNextPresentation
            ? presentation.SelectedItemId ?? focusedItemId
            : focusedItemId;
        if (_focusSelectedOnNextPresentation &&
            !string.IsNullOrWhiteSpace(presentation.SelectedItemId))
        {
            _lastSemanticFocusItemId = presentation.SelectedItemId;
        }
        _focusSelectedOnNextPresentation = false;
        _presentation = presentation;
        _presentationSignature = signature;
        bool wasExpanded = _expanded;
        if (_layoutProfile.AccessibilityScale > 0f)
        {
            _expanded = presentation.Expanded || _layoutProfile.AccessibilityScale < 1.25f;
        }
        if (wasExpanded != _expanded)
        {
            RebuildLaneLabels();
            ApplyLaneGeometry();
            UpdateExpansionButton();
            DesiredHeightChanged?.Invoke(DesiredHeight);
        }
        _nowLabel.Text = presentation.NowLabel;
        _horizonLabel.Text = presentation.HorizonLabel;
        _nowLabel.AccessibilityName = $"현재 시각 {presentation.NowLabel}";
        AccessibilityName = BuildAccessibilityName(presentation);
        AccessibilityDescription =
            "이전, 현재, 다음 버튼과 시간순 사건 목록으로 탐색합니다. " +
            "좌우 방향키도 시간순 사건을 이동하고 Home은 현재 시각으로 돌아갑니다.";
        UpdateAccessibleEventSelector(presentation);
        ScheduleMarkerLayout(restoreFocusItemId);
    }

    public void ApplyLayout(RealtimeLayoutProfile profile)
    {
        bool wasExpanded = _expanded;
        _layoutProfile = profile;
        _expanded = (_presentation?.Expanded ?? false) ||
            profile.AccessibilityScale < 1.25f;
        if (wasExpanded != _expanded)
        {
            RebuildLaneLabels();
        }
        CustomMinimumSize = new Vector2(0f, DesiredHeight);
        _markerWidth = BaseMarkerWidth * profile.AccessibilityScale;
        _clusterWidth = BaseClusterWidth * profile.AccessibilityScale;
        _markerHeight = profile.MinimumHitTarget;
        _laneGap = BaseLaneGap * profile.AccessibilityScale;
        ApplyLaneGeometry();
        _previousEvent.CustomMinimumSize = Vector2.One * profile.MinimumHitTarget;
        _currentTime.CustomMinimumSize = Vector2.One * profile.MinimumHitTarget;
        _nextEvent.CustomMinimumSize = Vector2.One * profile.MinimumHitTarget;
        _accessibleEventSelector.CustomMinimumSize = new Vector2(
            Math.Clamp(220f * profile.AccessibilityScale, 220f, 280f),
            profile.MinimumHitTarget);
        ApplyAccessiblePopupLayout(profile);
        _shorterHorizon.CustomMinimumSize = Vector2.One * profile.MinimumHitTarget;
        _longerHorizon.CustomMinimumSize = Vector2.One * profile.MinimumHitTarget;
        _expandLanes.CustomMinimumSize = new Vector2(
            profile.MinimumHitTarget * 2f,
            profile.MinimumHitTarget);
        _expandLanes.Visible = profile.AccessibilityScale >= 1.25f;
        UpdateExpansionButton();
        foreach (MarkerEntry entry in _markers)
        {
            ApplyMarkerVisualTokens(entry.Button);
        }
        ScheduleMarkerLayout();
    }

    private void ApplyAccessiblePopupLayout(RealtimeLayoutProfile profile)
    {
        PopupMenu popup = _accessibleEventSelector.GetPopup();
        int fontSize = Math.Max(
            BaseMarkerFontSize,
            Mathf.RoundToInt(BaseMarkerFontSize * profile.AccessibilityScale));
        popup.AddThemeFontSizeOverride("font_size", fontSize);
        Font font = popup.GetThemeFont("font");
        int verticalSeparation = Math.Max(
            0,
            Mathf.CeilToInt(profile.MinimumHitTarget - font.GetHeight(fontSize)));
        popup.AddThemeConstantOverride("v_separation", verticalSeparation);
        int horizontalPadding = Math.Max(
            12,
            Mathf.RoundToInt(12f * profile.AccessibilityScale));
        popup.AddThemeConstantOverride("item_start_padding", horizontalPadding);
        popup.AddThemeConstantOverride("item_end_padding", horizontalPadding);
    }

    private void ApplyLaneGeometry()
    {
        CustomMinimumSize = new Vector2(0f, DesiredHeight);
        float trackHeight = (_markerHeight * VisibleLaneCount) +
                            (_laneGap * (VisibleLaneCount - 1));
        _track.CustomMinimumSize = new Vector2(600f, trackHeight);
        _laneLabels.CustomMinimumSize = new Vector2(
            116f * Math.Max(1f, _layoutProfile.AccessibilityScale),
            trackHeight);
        _laneLabels.AddThemeConstantOverride(
            "separation",
            Mathf.RoundToInt(_laneGap));
        foreach (Node child in _laneLabels.GetChildren())
        {
            if (child is Control control)
            {
                control.CustomMinimumSize = new Vector2(0f, _markerHeight);
            }
        }
    }

    public bool Navigate(RealtimeTimelineNavigation navigation)
    {
        if (_presentation is null)
        {
            return false;
        }
        RealtimeTimelineItemPresentation[] items = VisibleItems(_presentation).ToArray();
        // Home is an absolute current-time anchor, not an item-relative move.
        // It must remain actionable even when filtering or the chosen horizon
        // leaves no visible events. Previous/next still require an item.
        if (navigation != RealtimeTimelineNavigation.Home && items.Length == 0)
        {
            return false;
        }
        // The controller is the sole navigation reducer. It owns authored,
        // construction, and thermal markers together and the refreshed
        // presentation restores focus to the selected stable ID.
        _focusSelectedOnNextPresentation = true;
        try
        {
            NavigationRequested?.Invoke(navigation);
        }
        finally
        {
            // Production presentation updates synchronously and consumes the
            // flag. If no owner (or a boundary no-op) publishes a new
            // presentation, do not let an unrelated refresh inherit it.
            _focusSelectedOnNextPresentation = false;
        }
        return true;
    }

    public override void _Draw()
    {
        if (_presentation is null || _track.Size.X <= 0f)
        {
            return;
        }
        Vector2 origin = GetGlobalTransform().AffineInverse() *
            _track.GetGlobalTransform().Origin;
        for (int lane = 0; lane < VisibleLaneCount; lane++)
        {
            float y = origin.Y + LaneY(lane) + (_markerHeight / 2f);
            DrawLine(
                new Vector2(origin.X, y),
                new Vector2(origin.X + _track.Size.X, y),
                Color.FromHtml("41575b"),
                1.5f);
        }
        float nowX = origin.X + TimeRatio(_presentation.NowMinute) * _track.Size.X;
        DrawLine(
            new Vector2(nowX, origin.Y),
            new Vector2(nowX, origin.Y + _track.Size.Y),
            Color.FromHtml("f0c469"),
            2.5f);

        foreach (RealtimeTimelineItemPresentation item in VisibleItems(_presentation))
        {
            if (!item.EndMinute.HasValue || item.EndMinute.Value <= item.StartMinute)
            {
                continue;
            }
            float start = origin.X + TimeRatio(item.StartMinute) * _track.Size.X;
            float end = origin.X + TimeRatio(item.EndMinute.Value) * _track.Size.X;
            float y = origin.Y + LaneY(DisplayLane(item.Lane)) + (_markerHeight / 2f);
            DrawLine(
                new Vector2(start, y),
                new Vector2(end, y),
                SeverityColor(item.Severity),
                item.IsCurrent ? 6f : 3f);
        }
    }

    private void RebuildLaneLabels()
    {
        foreach (Node child in _laneLabels.GetChildren())
        {
            _laneLabels.RemoveChild(child);
            child.QueueFree();
        }
        string[] labels = _expanded
            ? new[] { "수요·기한", "기상·정지", "공사", "열 보호" }
            : new[] { "수요·공사", "기상·열 보호" };
        foreach (string text in labels)
        {
            _laneLabels.AddChild(new Label
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
                AccessibilityName = $"사건 지평선 {text} 줄",
            });
        }
    }

    private void ScheduleMarkerLayout(string? restoreFocusItemId = null)
    {
        if (!IsInsideTree())
        {
            return;
        }

        // SetPresentation can be followed by one or more resize/layout
        // notifications before the deferred rebuild runs. A notification with
        // no focus payload must not erase the semantic sub-item that the first
        // request asked us to restore.
        if (!string.IsNullOrWhiteSpace(restoreFocusItemId))
        {
            _pendingRestoreFocusItemId = restoreFocusItemId;
        }
        else if (string.IsNullOrWhiteSpace(_pendingRestoreFocusItemId))
        {
            _pendingRestoreFocusItemId = FocusedItemId();
        }
        if (_markerLayoutScheduled)
        {
            return;
        }
        _markerLayoutScheduled = true;
        CallDeferred(nameof(RebuildMarkers));
    }

    private void RebuildMarkers()
    {
        _markerLayoutScheduled = false;
        string? restoreFocus = _pendingRestoreFocusItemId ?? FocusedItemId();
        _pendingRestoreFocusItemId = null;
        if (_presentation is null || _track.Size.X <= 1f)
        {
            if (!string.IsNullOrWhiteSpace(restoreFocus))
            {
                _pendingRestoreFocusItemId = restoreFocus;
            }
            QueueRedraw();
            return;
        }

        RealtimeTimelineItemPresentation[] visible = VisibleItems(_presentation).ToArray();
        IReadOnlyList<MarkerPlan> plans = BuildMarkerPlans(visible);
        var retained = new HashSet<MarkerEntry>();
        Dictionary<string, MarkerEntry> existing = _markers.ToDictionary(
            entry => entry.Key,
            StringComparer.Ordinal);
        var nextMarkers = new List<MarkerEntry>(plans.Count);
        foreach (MarkerPlan plan in plans)
        {
            if (!existing.TryGetValue(plan.Key, out MarkerEntry? entry))
            {
                Button button = CreateMarkerButton();
                _track.AddChild(button);
                entry = new MarkerEntry(
                    plan.Key,
                    button,
                    plan.DisplayLane,
                    plan.Items.Select(item => item.Id).ToArray(),
                    InitialSemanticItemId(plan.Items.Select(item => item.Id).ToArray()));
                button.FocusEntered += () => RememberMarkerFocus(entry);
                button.Pressed += () => RequestItems(entry.ItemIds, followMarkerFocus: true);
            }
            retained.Add(entry);
            entry.Update(
                plan.DisplayLane,
                plan.Items.Select(item => item.Id).ToArray());
            ConfigureMarker(entry, plan.Items);
            entry.Button.Position = new Vector2(plan.X, LaneY(plan.DisplayLane));
            entry.Button.Size = new Vector2(plan.Width, _markerHeight);
            nextMarkers.Add(entry);
        }

        for (int index = 0; index < nextMarkers.Count; index++)
        {
            _track.MoveChild(nextMarkers[index].Button, index);
        }

        _markers.Clear();
        _markers.AddRange(nextMarkers);
        WireChronologicalArrowFocus(_markers.Select(item => item.Button).ToArray());
        if (!string.IsNullOrWhiteSpace(restoreFocus))
        {
            GrabMarkerFor(restoreFocus);
        }
        foreach (MarkerEntry stale in existing.Values.Where(entry => !retained.Contains(entry)))
        {
            if (IsInstanceValid(stale.Button))
            {
                _track.RemoveChild(stale.Button);
                stale.Button.QueueFree();
            }
        }
#if DEBUG
        AssertNoSameLaneMarkerOverlap();
#endif
        QueueRedraw();
    }

    private Button CreateMarkerButton() => new()
    {
        ClipText = true,
        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        FocusMode = FocusModeEnum.All,
        ThemeTypeVariation = "TimelineMarkerButton",
        ToggleMode = true,
        ButtonGroup = _markerGroup,
    };

    private IReadOnlyList<MarkerPlan> BuildMarkerPlans(
        IReadOnlyList<RealtimeTimelineItemPresentation> visible)
    {
        float collisionWidth = Math.Min(
            _track.Size.X,
            Math.Max(_markerWidth, _clusterWidth));
        var plans = new List<MarkerPlan>();
        foreach (IGrouping<int, RealtimeTimelineItemPresentation> lane in visible
                     .GroupBy(item => DisplayLane(item.Lane))
                     .OrderBy(group => group.Key))
        {
            var cluster = new List<RealtimeTimelineItemPresentation>();
            float occupiedRight = float.NegativeInfinity;
            foreach (RealtimeTimelineItemPresentation item in lane
                         .OrderBy(candidate => candidate.StartMinute)
                         .ThenBy(candidate => candidate.Priority)
                         .ThenBy(candidate => candidate.Id, StringComparer.Ordinal))
            {
                float left = MarkerLeft(item.StartMinute, collisionWidth);
                float right = left + collisionWidth;
                if (cluster.Count > 0 && left >= occupiedRight)
                {
                    plans.Add(CreateMarkerPlan(lane.Key, cluster));
                    cluster.Clear();
                    occupiedRight = float.NegativeInfinity;
                }
                cluster.Add(item);
                occupiedRight = Math.Max(occupiedRight, right);
            }
            if (cluster.Count > 0)
            {
                plans.Add(CreateMarkerPlan(lane.Key, cluster));
            }
        }
        return Array.AsReadOnly(plans
            .OrderBy(plan => plan.Items.Min(item => item.StartMinute))
            .ThenBy(plan => plan.Items.Min(item => item.Priority))
            .ThenBy(plan => plan.Items.Min(item => item.Id), StringComparer.Ordinal)
            .ToArray());
    }

    private MarkerPlan CreateMarkerPlan(
        int displayLane,
        IReadOnlyList<RealtimeTimelineItemPresentation> items)
    {
        RealtimeTimelineItemPresentation[] ordered = items
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        float width = Math.Min(
            _track.Size.X,
            ordered.Length > 1 ? _clusterWidth : _markerWidth);
        float x = MarkerLeft(ordered[0].StartMinute, width);
        string key = $"{displayLane}\u001c" +
            string.Join("\u001d", ordered.Select(item => item.Id));
        return new MarkerPlan(
            key,
            displayLane,
            Array.AsReadOnly(ordered),
            x,
            width);
    }

    private float MarkerLeft(long minute, float width)
    {
        float desired = TimeRatio(minute) * _track.Size.X - (width / 2f);
        return Math.Clamp(desired, 0f, Math.Max(0f, _track.Size.X - width));
    }

    private void ConfigureMarker(
        MarkerEntry entry,
        IReadOnlyList<RealtimeTimelineItemPresentation> items)
    {
        RealtimeTimelineItemPresentation lead =
            items.FirstOrDefault(item => string.Equals(
                item.Id,
                _presentation?.SelectedItemId,
                StringComparison.Ordinal)) ?? items[0];
        bool containsCurrent = items.Any(item => item.IsCurrent);
        string statePrefix = lead.IsCurrent
            ? "진행 중 · "
            : lead.Visibility == RealtimeTimelineVisibility.Completed
                ? "완료 · "
                : string.Empty;
        int otherCurrentCount = items.Count(item => item.IsCurrent) -
            (lead.IsCurrent ? 1 : 0);
        string currentSiblingSuffix = otherCurrentCount > 0
            ? $" · 진행 {otherCurrentCount}건"
            : string.Empty;
        string text = items.Count == 1
            ? $"{statePrefix}{SeverityGlyph(lead.Severity)} {lead.KindIcon} {lead.TimeLabel} · {lead.ShortLabel}"
            : $"{statePrefix}{SeverityGlyph(lead.Severity)} {lead.KindIcon} {lead.TimeLabel} · " +
              $"{lead.ShortLabel} +{items.Count - 1}{currentSiblingSuffix}";
        Button marker = entry.Button;
        marker.Text = text;
        marker.TooltipText = items.Count == 1
            ? $"{lead.TimeLabel} · {lead.Description}"
            : string.Join("\n", items.Select(item => $"{item.TimeLabel} · {item.Title}"));
        marker.AccessibilityName = items.Count == 1
            ? TimelineAccessibility(lead)
            : $"가까운 시간 구간의 일정 {items.Count}건. " +
              string.Join(". ", items.Select(TimelineAccessibility));
        marker.AccessibilityDescription =
            "선택하면 같은 운영 예측을 지도와 상황 패널에서 엽니다.";
        marker.AddThemeColorOverride("font_color", SeverityColor(lead.Severity));
        bool selected = _presentation?.SelectedItemId is string selectedId &&
            items.Any(item => string.Equals(item.Id, selectedId, StringComparison.Ordinal));
        marker.ButtonPressed = selected;
        marker.RemoveThemeConstantOverride("outline_size");
        marker.RemoveThemeColorOverride("font_outline_color");
        if (containsCurrent)
        {
            marker.AddThemeConstantOverride(
                "outline_size",
                Math.Max(2, Mathf.RoundToInt(2f * AccessibilityScale)));
            marker.AddThemeColorOverride("font_outline_color", Color.FromHtml("f0c469"));
        }
        marker.SetMeta("item_ids", string.Join("\n", entry.ItemIds));
        ApplyMarkerVisualTokens(marker);
    }

    private void RequestItems(
        IReadOnlyList<string> itemIds,
        bool followMarkerFocus)
    {
        string[] requestedIds = itemIds.ToArray();
        // A marker press is an explicit rail selection just like keyboard
        // navigation. The synchronous controller presentation identifies the
        // precise semantic item selected within a clustered button.
        _focusSelectedOnNextPresentation = followMarkerFocus;
        try
        {
            ItemsRequested?.Invoke(Array.AsReadOnly(requestedIds));
        }
        finally
        {
            _focusSelectedOnNextPresentation = false;
        }
        string? selectedItemId = _presentation?.SelectedItemId;
        if (!string.IsNullOrWhiteSpace(selectedItemId) &&
            requestedIds.Contains(selectedItemId, StringComparer.Ordinal))
        {
            _lastSemanticFocusItemId = selectedItemId;
            MarkerEntry? owningEntry = _markers.FirstOrDefault(entry =>
                entry.ItemIds.Contains(selectedItemId, StringComparer.Ordinal));
            if (owningEntry is not null)
            {
                owningEntry.SemanticItemId = selectedItemId;
            }
        }
    }

    private float AccessibilityScale => Math.Max(1f, _layoutProfile.AccessibilityScale);

    private void ApplyMarkerVisualTokens(Button marker)
    {
        marker.AddThemeFontSizeOverride(
            "font_size",
            Math.Max(1, Mathf.RoundToInt(BaseMarkerFontSize * AccessibilityScale)));
        marker.RemoveThemeStyleboxOverride("focus");
        if (marker.GetThemeStylebox("focus") is not StyleBoxFlat sourceFocus)
        {
            return;
        }
        var focus = (StyleBoxFlat)sourceFocus.Duplicate();
        int border = Math.Max(3, Mathf.RoundToInt(3f * AccessibilityScale));
        float expand = Math.Max(2f, 2f * AccessibilityScale);
        focus.BorderWidthLeft = border;
        focus.BorderWidthTop = border;
        focus.BorderWidthRight = border;
        focus.BorderWidthBottom = border;
        focus.ExpandMarginLeft = expand;
        focus.ExpandMarginTop = expand;
        focus.ExpandMarginRight = expand;
        focus.ExpandMarginBottom = expand;
        marker.AddThemeStyleboxOverride("focus", focus);
    }

    private void UpdateAccessibleEventSelector(
        RealtimeEventRailPresentation presentation)
    {
        RealtimeTimelineItemPresentation[] items = VisibleItems(presentation).ToArray();
        string[] ids = items.Select(item => item.Id).ToArray();
        bool structureChanged = _accessibleEventSelector.ItemCount == 0 ||
            !_accessibleItemIds.SequenceEqual(ids, StringComparer.Ordinal);
        _updatingAccessibleEventSelector = true;
        try
        {
            if (structureChanged)
            {
                _accessibleEventSelector.Clear();
                _accessibleItemIds.Clear();
                _accessibleItemIds.AddRange(ids);
                _accessibleEventSelector.AddItem(
                    items.Length == 0 ? "표시할 사건 없음" : "사건 선택");
                _accessibleEventSelector.SetItemDisabled(0, true);
                for (int index = 0; index < items.Length; index++)
                {
                    _accessibleEventSelector.AddItem(AccessibleSelectorText(items[index]));
                    _accessibleEventSelector.SetItemTooltip(
                        index + 1,
                        TimelineAccessibility(items[index]));
                }
            }
            else
            {
                for (int index = 0; index < items.Length; index++)
                {
                    _accessibleEventSelector.SetItemText(
                        index + 1,
                        AccessibleSelectorText(items[index]));
                    _accessibleEventSelector.SetItemTooltip(
                        index + 1,
                        TimelineAccessibility(items[index]));
                }
            }

            int selected = presentation.SelectedItemId is string selectedId
                ? Array.FindIndex(ids, id => string.Equals(
                    id,
                    selectedId,
                    StringComparison.Ordinal)) + 1
                : 0;
            _accessibleEventSelector.Select(Math.Max(0, selected));
            _accessibleEventSelector.Disabled = items.Length == 0;
            // Keep the closed selector readable at FHD accessibility scales.
            // The popup retains one fully named row per event and the tooltip /
            // accessibility name retain the selected event's complete meaning.
            _accessibleEventSelector.Text = selected > 0
                ? $"{SeverityGlyph(items[selected - 1].Severity)} " +
                  $"{selected}/{items.Length} · {items[selected - 1].TimeLabel}"
                : items.Length == 0
                    ? "표시할 사건 없음"
                    : "사건 선택";
            _accessibleEventSelector.TooltipText = selected > 0
                ? TimelineAccessibility(items[selected - 1])
                : "시간순 사건을 한 건씩 선택합니다.";
            _accessibleEventSelector.AccessibilityName = selected > 0
                ? $"시간순 사건 {selected}/{items.Length}. " +
                  TimelineAccessibility(items[selected - 1])
                : "시간순 사건 목록";
        }
        finally
        {
            _updatingAccessibleEventSelector = false;
        }
    }

    private void HandleAccessibleEventSelected(long selectedIndex)
    {
        if (_updatingAccessibleEventSelector || selectedIndex <= 0 ||
            selectedIndex > _accessibleItemIds.Count)
        {
            return;
        }
        string itemId = _accessibleItemIds[checked((int)selectedIndex - 1)];
        RequestItems(new[] { itemId }, followMarkerFocus: false);
    }

    private static string AccessibleSelectorText(
        RealtimeTimelineItemPresentation item) =>
        $"{SeverityGlyph(item.Severity)} {item.TimeLabel} · {item.KindLabel} · {item.ShortLabel}";

#if DEBUG
    private void AssertNoSameLaneMarkerOverlap()
    {
        for (int leftIndex = 0; leftIndex < _markers.Count; leftIndex++)
        {
            MarkerEntry left = _markers[leftIndex];
            Rect2 leftRect = new(left.Button.Position, left.Button.Size);
            for (int rightIndex = leftIndex + 1; rightIndex < _markers.Count; rightIndex++)
            {
                MarkerEntry right = _markers[rightIndex];
                if (left.DisplayLane != right.DisplayLane)
                {
                    continue;
                }
                Rect2 rightRect = new(right.Button.Position, right.Button.Size);
                float horizontalOverlap = Math.Min(leftRect.End.X, rightRect.End.X) -
                    Math.Max(leftRect.Position.X, rightRect.Position.X);
                if (horizontalOverlap > 0f)
                {
                    throw new InvalidOperationException(
                        $"Timeline markers overlap in lane {left.DisplayLane}: " +
                        $"{left.Key} {leftRect} / {right.Key} {rightRect}.");
                }
            }
        }
    }
#endif

    private float LaneY(int lane) => lane * (_markerHeight + _laneGap);

    private int DisplayLane(RealtimeTimelineLane lane)
    {
        if (_expanded)
        {
            return (int)lane;
        }
        return lane is RealtimeTimelineLane.DemandAndDeadline or
            RealtimeTimelineLane.Construction
            ? 0
            : 1;
    }

    private void ToggleExpansion()
    {
        _expanded = !_expanded;
        int height = DesiredHeight;
        RebuildLaneLabels();
        ApplyLaneGeometry();
        UpdateExpansionButton();
        DesiredHeightChanged?.Invoke(height);
        ExpansionRequested?.Invoke(_expanded);
        ScheduleMarkerLayout(FocusedItemId());
    }

    private void UpdateExpansionButton()
    {
        _expandLanes.Text = _expanded ? "2줄" : "4줄";
        _expandLanes.TooltipText = _expanded
            ? "사건 지평선을 두 개의 요약 줄로 접습니다."
            : "사건 지평선을 네 종류의 줄로 펼칩니다.";
        _expandLanes.AccessibilityDescription = _expandLanes.TooltipText;
    }

    private float TimeRatio(long minute)
    {
        RealtimeEventRailPresentation presentation = _presentation!;
        double duration = presentation.HorizonEndMinute - presentation.HorizonStartMinute;
        return (float)Math.Clamp(
            (minute - presentation.HorizonStartMinute) / duration,
            0d,
            1d);
    }

    private string? FocusedItemId()
    {
        MarkerEntry? entry = FocusedMarkerEntry();
        return entry?.SemanticItemId;
    }

    private bool HasFocusedMarker() => FocusedItemId() is not null;

    private MarkerEntry? FocusedMarkerEntry()
    {
        Control? focus = GetViewport().GuiGetFocusOwner();
        return _markers.FirstOrDefault(item => ReferenceEquals(item.Button, focus));
    }

    private string InitialSemanticItemId(IReadOnlyList<string> itemIds)
    {
        if (!string.IsNullOrWhiteSpace(_lastSemanticFocusItemId) &&
            itemIds.Contains(_lastSemanticFocusItemId, StringComparer.Ordinal))
        {
            return _lastSemanticFocusItemId;
        }
        string? selectedItemId = _presentation?.SelectedItemId;
        if (!string.IsNullOrWhiteSpace(selectedItemId) &&
            itemIds.Contains(selectedItemId, StringComparer.Ordinal))
        {
            return selectedItemId;
        }
        return itemIds[0];
    }

    private void RememberMarkerFocus(MarkerEntry entry)
    {
        if (!entry.ItemIds.Contains(entry.SemanticItemId, StringComparer.Ordinal))
        {
            entry.SemanticItemId = InitialSemanticItemId(entry.ItemIds);
        }
        _lastSemanticFocusItemId = entry.SemanticItemId;
    }

    private void GrabMarkerFor(string itemId)
    {
        MarkerEntry? entry = _markers.FirstOrDefault(item =>
            item.ItemIds.Contains(itemId, StringComparer.Ordinal));
        if (entry is null)
        {
            return;
        }
        entry.SemanticItemId = itemId;
        _lastSemanticFocusItemId = itemId;
        if (!ReferenceEquals(GetViewport().GuiGetFocusOwner(), entry.Button))
        {
            entry.Button.GrabFocus();
        }
    }

    private static IEnumerable<RealtimeTimelineItemPresentation> VisibleItems(
        RealtimeEventRailPresentation presentation) => presentation.Items
        .Where(item => item.Visibility != RealtimeTimelineVisibility.Hidden)
        .Where(item => item.StartMinute <= presentation.HorizonEndMinute &&
                       (item.EndMinute ?? item.StartMinute) >= presentation.HorizonStartMinute)
        .OrderBy(item => item.StartMinute)
        .ThenBy(item => item.Priority)
        .ThenBy(item => item.Id, StringComparer.Ordinal);

    private static Color SeverityColor(RealtimeTimelineSeverity severity) => severity switch
    {
        RealtimeTimelineSeverity.Information => Color.FromHtml("83d0c8"),
        RealtimeTimelineSeverity.Advisory => Color.FromHtml("d6ba70"),
        RealtimeTimelineSeverity.Warning => Color.FromHtml("e28a55"),
        RealtimeTimelineSeverity.Critical => Color.FromHtml("ed756e"),
        _ => throw new ArgumentOutOfRangeException(nameof(severity)),
    };

    private static string SeverityGlyph(RealtimeTimelineSeverity severity) => severity switch
    {
        RealtimeTimelineSeverity.Information => "●",
        RealtimeTimelineSeverity.Advisory => "◆",
        RealtimeTimelineSeverity.Warning => "▲",
        RealtimeTimelineSeverity.Critical => "■",
        _ => throw new ArgumentOutOfRangeException(nameof(severity)),
    };

    private static string TimelineAccessibility(RealtimeTimelineItemPresentation item) =>
        $"{item.TimeLabel}. {item.KindLabel} {item.Title}. " +
        $"{(item.IsCurrent ? "진행 중. " : item.Visibility == RealtimeTimelineVisibility.Completed ? "완료됨. " : string.Empty)}" +
        $"{item.SeverityLabel}. {item.Description}.";

    private static string BuildAccessibilityName(RealtimeEventRailPresentation presentation)
    {
        string items = string.Join(". ", VisibleItems(presentation)
            .Select(TimelineAccessibility));
        return $"사건 지평선. 현재 {presentation.NowLabel}. 범위 {presentation.HorizonLabel}. {items}";
    }

    private static string Signature(RealtimeEventRailPresentation presentation) =>
        string.Join(
            "\u001f",
            presentation.NowMinute,
            presentation.HorizonStartMinute,
            presentation.HorizonEndMinute,
            presentation.NowLabel,
            presentation.HorizonLabel,
            presentation.HorizonPreset,
            presentation.SelectedItemId ?? string.Empty,
            presentation.Expanded,
            string.Join("\u001e", presentation.Items.Select(item => string.Join(
                "\u001d",
                item.Id,
                item.Kind,
                item.StartMinute,
                item.EndMinute?.ToString() ?? string.Empty,
                item.Priority,
                item.Lane,
                item.Visibility,
                item.IsCurrent,
                item.IsActionable,
                item.Title,
                item.ShortLabel,
                item.Description,
                item.Severity,
                item.KindIcon,
                item.KindLabel,
                item.TimeLabel,
                item.SeverityLabel))));

    private static void WireChronologicalArrowFocus(IReadOnlyList<Button> markers)
    {
        for (int index = 0; index < markers.Count; index++)
        {
            Button current = markers[index];
            Button previous = markers[(index - 1 + markers.Count) % markers.Count];
            Button next = markers[(index + 1) % markers.Count];
            current.FocusNeighborLeft = current.GetPathTo(previous);
            current.FocusNeighborRight = current.GetPathTo(next);
            // Do not set FocusNext/FocusPrevious: Tab must leave the rail normally.
        }
    }

    private sealed record MarkerPlan(
        string Key,
        int DisplayLane,
        IReadOnlyList<RealtimeTimelineItemPresentation> Items,
        float X,
        float Width);

    private sealed class MarkerEntry(
        string key,
        Button button,
        int displayLane,
        IReadOnlyList<string> itemIds,
        string semanticItemId)
    {
        public string Key { get; } = key;
        public Button Button { get; } = button;
        public int DisplayLane { get; private set; } = displayLane;
        public IReadOnlyList<string> ItemIds { get; private set; } =
            Array.AsReadOnly(itemIds.ToArray());
        public string SemanticItemId { get; set; } = semanticItemId;

        public void Update(int nextDisplayLane, IReadOnlyList<string> nextItemIds)
        {
            DisplayLane = nextDisplayLane;
            ItemIds = Array.AsReadOnly(nextItemIds.ToArray());
            if (!ItemIds.Contains(SemanticItemId, StringComparer.Ordinal))
            {
                SemanticItemId = ItemIds[0];
            }
        }
    }
}
