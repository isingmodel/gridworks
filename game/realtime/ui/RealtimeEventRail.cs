using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Gridworks.Game.Realtime.UI;

/// <summary>
/// A non-scrolling chronological event horizon. Typed lane identity
/// remains in the presentation and accessibility copy, while every visible
/// item shares one compact time track. Full detail is disclosed by the marker's
/// custom hover detail panel instead of being repeated in permanent lane cards.
/// </summary>
internal sealed partial class RealtimeEventRail : PanelContainer
{
    private const float BaseMarkerWidth = 72f;
    private const float BaseClusterWidth = 112f;
    private const float BaseRailChromeHeight = 18f;
    private const float BaseTooltipWidth = 420f;
    private const int TooltipItemLimit = 6;
    private const int BaseMarkerFontSize = 14;

    private Label _nowHeading = null!;
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
    private RealtimeLayoutProfile _layoutProfile;
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

    public int VisibleLaneCount => 1;

    public int DesiredHeight => checked(
        (2 * Mathf.CeilToInt(_markerHeight)) +
        Mathf.CeilToInt(BaseRailChromeHeight));

    public IReadOnlyList<string> LinearItemIds =>
        Array.AsReadOnly(_accessibleItemIds.ToArray());

    public override void _Ready()
    {
        _nowHeading = GetNode<Label>("%NowHeading");
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
            "한 줄 시간축의 각 항목을 전체 문장으로 고릅니다. 선택하면 같은 사건을 지도와 상황 패널에서 엽니다.";
        _shorterHorizon.AccessibilityName = "사건 지평선을 더 짧게 보기";
        _longerHorizon.AccessibilityName = "사건 지평선을 더 길게 보기";
        _expandLanes.AccessibilityName = "한 줄 사건 지평선";
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
        UpdateStatusLabels(presentation);
        AccessibilityName = BuildAccessibilityName(presentation);
        AccessibilityDescription =
            "한 줄 시간축입니다. 짧은 기호에 마우스를 올리면 상세 정보창이 열립니다. " +
            "이전, 현재, 다음 버튼과 시간순 사건 목록으로 탐색하며, 좌우 방향키도 시간순 사건을 이동하고 Home은 현재 시각으로 돌아갑니다.";
        UpdateAccessibleEventSelector(presentation);
        ScheduleMarkerLayout(restoreFocusItemId);
    }

    public void ApplyLayout(RealtimeLayoutProfile profile)
    {
        _layoutProfile = profile;
        _markerWidth = BaseMarkerWidth * profile.AccessibilityScale;
        _clusterWidth = BaseClusterWidth * profile.AccessibilityScale;
        _markerHeight = profile.MinimumHitTarget;
        ApplyLaneGeometry();
        _previousEvent.CustomMinimumSize = Vector2.One * profile.MinimumHitTarget;
        _currentTime.CustomMinimumSize = Vector2.One * profile.MinimumHitTarget;
        _nextEvent.CustomMinimumSize = Vector2.One * profile.MinimumHitTarget;
        _accessibleEventSelector.CustomMinimumSize = new Vector2(
            Math.Clamp(190f * profile.AccessibilityScale, 190f, 250f),
            profile.MinimumHitTarget);
        ApplyAccessiblePopupLayout(profile);
        _shorterHorizon.CustomMinimumSize = Vector2.One * profile.MinimumHitTarget;
        _longerHorizon.CustomMinimumSize = Vector2.One * profile.MinimumHitTarget;
        _expandLanes.CustomMinimumSize = Vector2.Zero;
        _expandLanes.Visible = false;
        UpdateExpansionButton();
        if (_presentation is not null)
        {
            UpdateStatusLabels(_presentation);
        }
        foreach (MarkerEntry entry in _markers)
        {
            ApplyMarkerVisualTokens(entry.Button);
        }
        ScheduleMarkerLayout();
    }

    private void UpdateStatusLabels(RealtimeEventRailPresentation presentation)
    {
        _nowLabel.Text = $"● {presentation.NowLabel}";
        _nowHeading.Text = presentation.NextEvent is null
            ? "다음 없음"
            : $"다음 {presentation.NextEvent.CountdownLabel}";
        _nowHeading.TooltipText = presentation.NextEvent is null
            ? "예정된 다음 사건이 없습니다."
            : $"{presentation.NextEvent.EventLabel} · " +
              $"{presentation.NextEvent.CountdownLabel} · " +
              presentation.NextEvent.WindowLabel;

        _horizonLabel.Text = presentation.NextEvent is null
            ? CompactHorizonLabel(presentation.HorizonPreset)
            : $"{CompactHorizonLabel(presentation.HorizonPreset)} · " +
              presentation.NextEvent.CompactWindowLabel;
        _horizonLabel.TooltipText = presentation.NextEvent is null
            ? presentation.HorizonLabel
            : $"{presentation.HorizonLabel} · {presentation.NextEvent.EventLabel} · " +
              presentation.NextEvent.WindowLabel;
        _nowLabel.AccessibilityName = $"현재 시각 {presentation.NowLabel}";
        _nowHeading.AccessibilityName = presentation.NextEvent is null
            ? "다음 사건 예정 없음"
            : $"다음 사건 {presentation.NextEvent.EventLabel}. " +
              presentation.NextEvent.CountdownLabel;
        _horizonLabel.AccessibilityName = presentation.NextEvent is null
            ? $"사건 지평선 범위 {presentation.HorizonLabel}"
            : $"사건 지평선 범위 {presentation.HorizonLabel}. " +
              presentation.NextEvent.WindowLabel;
    }

    private static string CompactHorizonLabel(
        RealtimeTimelineHorizonPreset preset) => preset switch
    {
        RealtimeTimelineHorizonPreset.SixHours => "+6h · -6h",
        RealtimeTimelineHorizonPreset.TwentyFourHours => "+24h · -6h",
        RealtimeTimelineHorizonPreset.SevenDays => "+7d · -6h",
        _ => throw new ArgumentOutOfRangeException(nameof(preset)),
    };

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
        _track.CustomMinimumSize = new Vector2(0f, _markerHeight);
        _laneLabels.CustomMinimumSize = Vector2.Zero;
        _laneLabels.Visible = false;
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
        string signatureBefore = _presentationSignature;
        string? selectedBefore = _presentation.SelectedItemId;
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
        if (navigation != RealtimeTimelineNavigation.Home &&
            !string.IsNullOrWhiteSpace(selectedBefore) &&
            string.Equals(
                signatureBefore,
                _presentationSignature,
                StringComparison.Ordinal))
        {
            // A mouse press moves focus to the navigation button before the
            // callback runs. At a first/last-item boundary the controller is a
            // deliberate no-op, so no refreshed presentation exists to return
            // focus to the selected semantic marker. Restore it explicitly.
            GrabMarkerFor(selectedBefore);
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
        float trackY = origin.Y + LaneY(0) + (_markerHeight / 2f);
        DrawLine(
            new Vector2(origin.X, trackY),
            new Vector2(origin.X + _track.Size.X, trackY),
            Color.FromHtml("41575b"),
            1.5f);
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
            DrawLine(
                new Vector2(start, trackY),
                new Vector2(end, trackY),
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
        _laneLabels.Visible = false;
        _laneLabels.CustomMinimumSize = Vector2.Zero;
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

    private Button CreateMarkerButton() => new RealtimeTimelineMarkerButton()
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
        float trackWidth = _track.Size.X;
        float collisionWidth = Math.Min(
            trackWidth,
            Math.Max(_markerWidth, _clusterWidth));
        var plans = new List<MarkerPlan>();
        var cluster = new List<RealtimeTimelineItemPresentation>();
        float occupiedRight = float.NegativeInfinity;
        foreach (RealtimeTimelineItemPresentation item in visible
                     .OrderBy(candidate => candidate.StartMinute)
                     .ThenBy(candidate => candidate.Priority)
                     .ThenBy(candidate => candidate.Id, StringComparer.Ordinal))
        {
            float left = MarkerLeft(item.StartMinute, collisionWidth);
            float right = left + collisionWidth;
            if (cluster.Count > 0 &&
                (left >= occupiedRight || cluster.Count >= TooltipItemLimit))
            {
                plans.Add(CreateMarkerPlan(cluster));
                cluster.Clear();
                occupiedRight = float.NegativeInfinity;
            }
            cluster.Add(item);
            occupiedRight = Math.Max(occupiedRight, right);
        }
        if (cluster.Count > 0)
        {
            plans.Add(CreateMarkerPlan(cluster));
        }

        plans = plans
            .OrderBy(plan => plan.Items.Min(item => item.StartMinute))
            .ThenBy(plan => plan.Items.Min(item => item.Priority))
            .ThenBy(plan => plan.Items.Min(item => item.Id), StringComparer.Ordinal)
            .ToList();
        while (plans.Count > 1 && plans.Sum(plan => plan.Width) > trackWidth)
        {
            int mergeIndex = Enumerable.Range(0, plans.Count - 1)
                .MinBy(index =>
                    plans[index + 1].Items.Min(item => item.StartMinute) -
                    plans[index].Items.Max(item => item.StartMinute));
            RealtimeTimelineItemPresentation[] merged = plans[mergeIndex].Items
                .Concat(plans[mergeIndex + 1].Items)
                .OrderBy(item => item.StartMinute)
                .ThenBy(item => item.Priority)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();
            plans[mergeIndex] = CreateMarkerPlan(merged);
            plans.RemoveAt(mergeIndex + 1);
        }
        ResolveMarkerPositions(plans, trackWidth);
        return Array.AsReadOnly(plans.ToArray());
    }

    private MarkerPlan CreateMarkerPlan(
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
        string key = string.Join("\u001d", ordered.Select(item => item.Id));
        return new MarkerPlan(
            key,
            0,
            Array.AsReadOnly(ordered),
            x,
            width);
    }

    private static void ResolveMarkerPositions(
        IList<MarkerPlan> plans,
        float trackWidth)
    {
        float cursor = 0f;
        for (int index = 0; index < plans.Count; index++)
        {
            MarkerPlan plan = plans[index];
            float x = Math.Max(plan.X, cursor);
            plans[index] = plan with { X = x };
            cursor = x + plan.Width;
        }
        cursor = trackWidth;
        for (int index = plans.Count - 1; index >= 0; index--)
        {
            MarkerPlan plan = plans[index];
            float x = Math.Min(plan.X, cursor - plan.Width);
            plans[index] = plan with { X = x };
            cursor = x;
        }
        if (plans.Count == 0 || plans[0].X >= 0f)
        {
            return;
        }
        cursor = 0f;
        for (int index = 0; index < plans.Count; index++)
        {
            MarkerPlan plan = plans[index];
            float x = Math.Max(plan.X, cursor);
            plans[index] = plan with { X = x };
            cursor = x + plan.Width;
        }
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
        RealtimeTimelineSeverity displaySeverity = items.Max(item => item.Severity);
        string sourceGlyph = ClusterSourceGlyph(items);
        string text = $"{ClusterStateGlyph(items)}" +
                      $"{SeverityGlyph(displaySeverity)}{sourceGlyph}" +
                      $"{lead.KindIcon}" +
                      (items.Count > 1 ? $"+{items.Count - 1}" : string.Empty);
        Button marker = entry.Button;
        marker.Text = text;
        marker.TooltipText = BuildHoverOverlayText(items, lead);
        marker.AccessibilityName = items.Count == 1
            ? TimelineAccessibility(lead)
            : $"가까운 시간 구간의 일정 {items.Count}건. " +
              string.Join(". ", items.Select(TimelineAccessibility));
        marker.AccessibilityDescription =
            "선택하면 같은 실제 운영 기록 또는 초안 운영 예측을 지도와 상황 패널에서 엽니다.";
        marker.AddThemeColorOverride("font_color", SeverityColor(displaySeverity));
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
        int fontSize = Math.Max(
            1,
            Mathf.RoundToInt(BaseMarkerFontSize * AccessibilityScale));
        marker.AddThemeFontSizeOverride("font_size", fontSize);
        if (marker is RealtimeTimelineMarkerButton timelineMarker)
        {
            timelineMarker.TooltipWidth = Math.Clamp(
                BaseTooltipWidth * AccessibilityScale,
                320f,
                620f);
            timelineMarker.TooltipFontSize = Math.Max(
                14,
                Mathf.RoundToInt(15f * AccessibilityScale));
        }
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
        $"{SeverityGlyph(item.Severity)} {item.TimingLabel} · " +
        $"{item.SourceLabel} {item.KindLabel} · {item.ShortLabel}";

#if DEBUG
    internal RealtimeTimelineTooltipOverlayFact TooltipOverlayFactForSmoke(
        string itemId)
    {
        MarkerEntry entry = _markers.FirstOrDefault(item =>
            item.ItemIds.Contains(itemId, StringComparer.Ordinal)) ??
            throw new InvalidOperationException(
                $"No rendered timeline marker contains {itemId}.");
        if (entry.Button is not RealtimeTimelineMarkerButton marker)
        {
            return new RealtimeTimelineTooltipOverlayFact(
                false,
                entry.Button.TooltipText,
                0f,
                0,
                Control.MouseFilterEnum.Stop);
        }
        Control overlay = marker._MakeCustomTooltip(marker.TooltipText);
        try
        {
            Label? detail = overlay.GetChildren().OfType<Label>().SingleOrDefault();
            return new RealtimeTimelineTooltipOverlayFact(
                overlay is MarginContainer && detail is not null,
                detail?.Text ?? string.Empty,
                overlay.CustomMinimumSize.X,
                detail?.GetThemeFontSize("font_size") ?? 0,
                overlay.MouseFilter);
        }
        finally
        {
            overlay.Free();
        }
    }
#endif

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

    private float LaneY(int lane) => Math.Max(0f, (_track.Size.Y - _markerHeight) / 2f);

    private int DisplayLane(RealtimeTimelineLane lane) => 0;

    private void ToggleExpansion()
    {
        UpdateExpansionButton();
        DesiredHeightChanged?.Invoke(DesiredHeight);
        ExpansionRequested?.Invoke(false);
    }

    private void UpdateExpansionButton()
    {
        _expandLanes.Text = "한 줄";
        _expandLanes.TooltipText =
            "모든 일정은 시간순 한 줄에 표시되고 마우스를 올리면 상세 정보창이 열립니다.";
        _expandLanes.AccessibilityDescription = _expandLanes.TooltipText;
        _expandLanes.Visible = false;
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

    private static string ClusterSourceGlyph(
        IReadOnlyList<RealtimeTimelineItemPresentation> items)
    {
        bool hasActual = items.Any(item =>
            item.SourceKind == RealtimeTimelineSourceKind.Actual);
        bool hasDraft = items.Any(item =>
            item.SourceKind == RealtimeTimelineSourceKind.Draft);
        if (hasActual && hasDraft)
        {
            return "■◇";
        }
        if (hasDraft)
        {
            return "◇";
        }
        return hasActual ? "■" : string.Empty;
    }

    private static string ClusterStateGlyph(
        IReadOnlyList<RealtimeTimelineItemPresentation> items) =>
        items.Any(item => item.IsCurrent)
            ? "▶"
            : items.All(item =>
                item.Visibility == RealtimeTimelineVisibility.Completed)
                ? "✓"
                : "○";

    private static string ItemStateLabel(RealtimeTimelineItemPresentation item) =>
        item.IsCurrent
            ? "진행 중"
            : item.Visibility == RealtimeTimelineVisibility.Completed
                ? "완료"
                : "예정";

    private static string BuildHoverOverlayText(
        IReadOnlyList<RealtimeTimelineItemPresentation> items,
        RealtimeTimelineItemPresentation lead)
    {
        string heading = items.Count == 1
            ? "시간축 상세"
            : $"같은 시간대 일정 {items.Count}건";
        RealtimeTimelineItemPresentation[] visibleDetails = items.Count <= TooltipItemLimit
            ? items.ToArray()
            : new[] { lead }
                .Concat(items.Where(item => !string.Equals(
                    item.Id,
                    lead.Id,
                    StringComparison.Ordinal)))
                .Take(TooltipItemLimit)
                .ToArray();
        string body = string.Join("\n\n", visibleDetails.Select(item =>
            $"{ItemStateLabel(item)} · {item.SourceLabel} · " +
            $"{item.KindLabel} · {item.SeverityLabel}\n" +
            $"{item.TimingLabel} · {item.Title}\n" +
            item.Description));
        string overflow = items.Count > visibleDetails.Length
            ? $"\n\n외 {items.Count - visibleDetails.Length}건 · 시간순 사건 목록에서 전체 보기"
            : string.Empty;
        return heading + "\n\n" + body + overflow;
    }

    private static string TimelineAccessibility(RealtimeTimelineItemPresentation item) =>
        $"{item.TimingLabel}. {item.SourceLabel} {item.KindLabel} {item.Title}. " +
        $"{(item.IsCurrent ? "진행 중. " : item.Visibility == RealtimeTimelineVisibility.Completed ? "완료됨. " : string.Empty)}" +
        $"{item.SeverityLabel}. {item.Description}.";

    private static string BuildAccessibilityName(RealtimeEventRailPresentation presentation)
    {
        string items = string.Join(". ", VisibleItems(presentation)
            .Select(TimelineAccessibility));
        string next = presentation.NextEvent is null
            ? "다음 사건 예정 없음"
            : $"다음 사건 {presentation.NextEvent.EventLabel}, " +
              $"{presentation.NextEvent.CountdownLabel}, {presentation.NextEvent.WindowLabel}";
        return $"사건 지평선. 현재 {presentation.NowLabel}. {next}. " +
               $"범위 {presentation.HorizonLabel}. {items}";
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
            presentation.NextEvent?.EventId ?? string.Empty,
            presentation.NextEvent?.StartMinute.ToString() ?? string.Empty,
            presentation.NextEvent?.EndMinute.ToString() ?? string.Empty,
            presentation.NextEvent?.MinutesUntilStart.ToString() ?? string.Empty,
            presentation.NextEvent?.EventLabel ?? string.Empty,
            presentation.NextEvent?.CountdownLabel ?? string.Empty,
            presentation.NextEvent?.WindowLabel ?? string.Empty,
            presentation.NextEvent?.CompactWindowLabel ?? string.Empty,
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
                item.EndTimeLabel ?? string.Empty,
                item.TimingLabel,
                item.SourceKind,
                item.SourceLabel,
                item.SourceGlyph,
                item.SeverityLabel))));

#if DEBUG
    internal string NextEventCountdownTextForSmoke => _nowHeading.Text;
    internal string NextEventWindowTextForSmoke => _horizonLabel.Text;
#endif

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

#if DEBUG
internal sealed record RealtimeTimelineTooltipOverlayFact(
    bool CustomOverlay,
    string Text,
    float MinimumWidth,
    int FontSize,
    Control.MouseFilterEnum MouseFilter);
#endif

/// <summary>
/// Compact marker button whose TooltipText is rendered as a wrapped, padded
/// popup panel by Godot. The popup is hover-only and mouse-transparent;
/// keyboard and screen-reader users retain the full chronological selector and
/// marker accessibility name.
/// </summary>
internal sealed partial class RealtimeTimelineMarkerButton : Button
{
    internal float TooltipWidth { get; set; } = 420f;

    internal int TooltipFontSize { get; set; } = 15;

    public override Control _MakeCustomTooltip(string forText)
    {
        var margin = new MarginContainer
        {
            CustomMinimumSize = new Vector2(TooltipWidth, 0f),
            MouseFilter = MouseFilterEnum.Ignore,
            AccessibilityName = "시간축 상세 정보창",
        };
        int padding = Math.Max(12, Mathf.RoundToInt(TooltipFontSize * 0.9f));
        margin.AddThemeConstantOverride("margin_left", padding);
        margin.AddThemeConstantOverride("margin_top", padding);
        margin.AddThemeConstantOverride("margin_right", padding);
        margin.AddThemeConstantOverride("margin_bottom", padding);
        var detail = new Label
        {
            Text = forText,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            AccessibilityName = forText,
        };
        detail.AddThemeFontSizeOverride("font_size", TooltipFontSize);
        detail.AddThemeConstantOverride(
            "line_spacing",
            Math.Max(4, Mathf.RoundToInt(TooltipFontSize * 0.3f)));
        margin.AddChild(detail);
        return margin;
    }
}
