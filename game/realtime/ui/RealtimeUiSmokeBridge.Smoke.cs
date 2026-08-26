#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Gridworks.Game.Realtime.UI;

internal sealed record RealtimeUiSmokeSurfaceFact(
    string Id,
    Rect2 Rect,
    Rect2 GlobalRect,
    Vector2 CombinedMinimumSize,
    bool Visible);

internal sealed record RealtimeUiSmokeControlFact(
    string Path,
    string OwnerSurfaceId,
    Rect2 Rect,
    Rect2 EffectiveVisibleRect,
    Vector2 CombinedMinimumSize,
    bool Visible,
    bool HasClipAncestor,
    IReadOnlyList<string> ClipAncestorPaths);

internal sealed record RealtimeUiSmokeButtonFact(
    string Path,
    Vector2 Size,
    bool Enabled,
    bool Primary,
    Control.FocusModeEnum FocusMode);

internal sealed record RealtimeUiSmokeTextFact(
    string Path,
    string Text,
    bool Button,
    bool Wrapped,
    float RequiredWidth,
    float AvailableWidth,
    int LineCount,
    int VisibleLineCount,
    bool FullyVisible);

internal sealed record RealtimeUiSmokeScrollFact(
    string Path,
    bool Visible,
    ScrollContainer.ScrollMode HorizontalMode,
    ScrollContainer.ScrollMode VerticalMode,
    Rect2 Rect);

internal sealed record RealtimeUiSmokeMarkerFact(
    IReadOnlyList<string> ItemIds,
    RealtimeTimelineLane AuthoredLane,
    int DisplayLane,
    Rect2 Rect,
    IReadOnlyList<string> LeftNeighborItemIds,
    IReadOnlyList<string> RightNeighborItemIds,
    string VisibleText,
    string AccessibilityName,
    string AccessibilityDescription,
    bool Selected,
    int OutlineSize,
    string SemanticItemId,
    ulong ButtonInstanceId,
    int FontSize,
    int FocusBorderWidth,
    float FocusExpandMargin);

internal sealed record RealtimeUiSmokeAccessibleTimelineItemFact(
    int OptionIndex,
    string ItemId,
    string Text,
    string Tooltip,
    bool Disabled,
    bool Selected);

internal sealed record RealtimeUiSmokeAccessibleTimelineClosedFact(
    Rect2 Rect,
    string Text,
    string AccessibilityName,
    string AccessibilityDescription,
    float RequiredWidth,
    float AvailableWidth,
    float RequiredHeight,
    float AvailableHeight,
    bool ClipText);

internal sealed record RealtimeUiSmokeAccessibleTimelinePopupFact(
    int ItemCount,
    int EnabledItemCount,
    int FontSize,
    float FontHeight,
    int VerticalSeparation,
    float EffectiveRowHeight,
    int StartPadding,
    int EndPadding);

internal sealed record RealtimeUiSmokeTimelineNavigationFact(
    RealtimeTimelineNavigation Navigation,
    Rect2 Rect,
    bool Enabled,
    string Text,
    string AccessibilityName,
    string AccessibilityDescription);

internal sealed record RealtimeUiSmokeSpeedFact(
    RealtimeSimulationSpeed Speed,
    bool Enabled,
    bool Pressed,
    string Text,
    string Tooltip,
    string AccessibilityName,
    string AccessibilityDescription,
    Rect2 Rect);

internal sealed record RealtimeUiSmokeFocusLinkFact(
    string Path,
    string NextPath,
    string PreviousPath,
    bool NextInsideModal,
    bool PreviousInsideModal);

internal sealed record RealtimeUiSmokeLayoutSnapshot(
    RealtimeLayoutProfile Profile,
    RealtimeSurfaceLayout ExpectedLayout,
    IReadOnlyList<RealtimeUiSmokeSurfaceFact> Surfaces,
    RealtimeUiSmokeSurfaceFact ModalPanel,
    IReadOnlyList<RealtimeUiSmokeControlFact> Controls,
    IReadOnlyList<RealtimeUiSmokeButtonFact> Buttons,
    IReadOnlyList<RealtimeUiSmokeTextFact> Text,
    IReadOnlyList<RealtimeUiSmokeScrollFact> Scrolls,
    IReadOnlyList<RealtimeUiSmokeMarkerFact> Markers,
    IReadOnlyList<string> LinearTimelineItemIds,
    IReadOnlyList<RealtimeUiSmokeTimelineNavigationFact> TimelineNavigation,
    IReadOnlyList<RealtimeUiSmokeAccessibleTimelineItemFact> AccessibleTimelineItems,
    RealtimeUiSmokeAccessibleTimelinePopupFact AccessibleTimelinePopup,
    int VisibleTimelineLanes,
    int TimelineLaneLabels,
    string? FocusedTimelineItemId,
    string? FocusOwnerPath);

internal sealed partial class RealtimeUiRoot
{
    /// <summary>
    /// Applies the same metrics, surface layout, and typography authorities as
    /// runtime while injecting the physical output size independently from the
    /// fixed 1920x1080 logical canvas.
    /// </summary>
    internal void ApplyLayoutForSmoke(
        Vector2I physicalSize,
        Vector2 logicalSize,
        int uiScalePercent)
    {
        if (!IsInsideTree())
        {
            throw new InvalidOperationException("UI root must be live for layout smoke.");
        }
        if (logicalSize.X <= 0 || logicalSize.Y <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalSize));
        }

        _uiScalePercent = uiScalePercent;
        _layoutProfile = RealtimeUiMetrics.ForWindow(physicalSize, uiScalePercent);
        _topHud.ApplyLayout(_layoutProfile);
        _eventRail.ApplyLayout(_layoutProfile);
        _contextDock.ApplyLayout(_layoutProfile);
        _buildShelf.ApplyLayout(_layoutProfile);
        _actionDock.ApplyLayout(_layoutProfile);
        _modalHost.ApplyLayout(_layoutProfile);
        _productTitle.ApplyLayout(_layoutProfile);
        _settingsSurface.ApplyLayout(_layoutProfile);
        ApplySyntheticSurfaceRectsForSmoke(logicalSize);
        ApplyTypography();
    }

    internal RealtimeUiSmokeLayoutSnapshot CaptureLayoutForSmoke(Vector2 logicalSize)
    {
        RealtimeLayoutProfile surfaceProfile = _layoutProfile with
        {
            EventRailHeight = _eventRail.DesiredHeight,
        };
        RealtimeSurfaceLayout expected = RealtimeUiMetrics.CalculateSurfaceLayout(
            logicalSize,
            surfaceProfile,
            _contextDock.Visible,
            _buildShelf.Visible,
            _actionDock.Visible);
        RealtimeUiSmokeSurfaceFact[] surfaces =
        [
            Surface("TopHud", _topHud),
            Surface("EventRail", _eventRail),
            Surface("ContextDock", _contextDock),
            Surface("BuildShelf", _buildShelf),
            Surface("ActionDock", _actionDock),
        ];
        RealtimeUiSmokeControlFact[] controls = surfaces
            .Where(surface => surface.Visible)
            .SelectMany(surface => DescendantFactsForSmoke(
                surface.Id,
                SurfaceControlForSmoke(surface.Id)))
            .ToArray();
        BaseButton[] visibleButtons = AllControlsForSmoke()
            .OfType<BaseButton>()
            .Where(item => item.IsVisibleInTree())
            .ToArray();
        RealtimeUiSmokeButtonFact[] buttons = visibleButtons
            .Select(item => new RealtimeUiSmokeButtonFact(
                PathForSmoke(item),
                item.Size,
                !item.Disabled,
                item.ThemeTypeVariation == "PrimaryButton",
                item.FocusMode))
            .ToArray();
        RealtimeUiSmokeTextFact[] text = AllControlsForSmoke()
            .Where(item => item.IsVisibleInTree())
            .Where(item => item is Label or BaseButton)
            .Select(TextFactForSmoke)
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .ToArray();
        RealtimeUiSmokeScrollFact[] scrolls = FindChildren(
                "*",
                "ScrollContainer",
                true,
                false)
            .OfType<ScrollContainer>()
            .Where(item => item.Owner is not null &&
                !_settingsSurface.IsAncestorOf(item))
            .Select(item => new RealtimeUiSmokeScrollFact(
                PathForSmoke(item),
                item.IsVisibleInTree(),
                item.HorizontalScrollMode,
                item.VerticalScrollMode,
                item.GetGlobalRect()))
            .ToArray();
        Control? focus = GetViewport().GuiGetFocusOwner();
        return new RealtimeUiSmokeLayoutSnapshot(
            _layoutProfile,
            expected,
            Array.AsReadOnly(surfaces),
            _modalHost.ModalPanelFactForSmoke(),
            Array.AsReadOnly(controls),
            Array.AsReadOnly(buttons),
            Array.AsReadOnly(text),
            Array.AsReadOnly(scrolls),
            _eventRail.MarkerFactsForSmoke(),
            _eventRail.LinearItemIds,
            _eventRail.NavigationFactsForSmoke,
            _eventRail.AccessibleTimelineItemsForSmoke,
            _eventRail.AccessibleTimelinePopupForSmoke,
            _eventRail.VisibleLaneCount,
            _eventRail.LaneLabelCountForSmoke,
            _eventRail.FocusedItemIdForSmoke,
            focus is null ? null : PathForSmoke(focus));
    }

    internal RealtimeEventRail EventRailForSmoke => _eventRail;
    internal RealtimeContextDock ContextDockForSmoke => _contextDock;
    internal RealtimeTopHud TopHudForSmoke => _topHud;
    internal RealtimeBuildShelf BuildShelfForSmoke => _buildShelf;
    internal RealtimeActionDock ActionDockForSmoke => _actionDock;
    internal RealtimeModalHost ModalHostForSmoke => _modalHost;
    internal RealtimeProductTitle ProductTitleForSmoke => _productTitle;
    internal RealtimeSettingsSurface SettingsSurfaceForSmoke => _settingsSurface;
    internal RealtimeInputRouter InputRouterForSmoke => _inputRouter;
    internal bool HudSurfaceVisibleForSmoke => _hudSurface.Visible;
    internal Theme ThemeForSmoke => _hudSurface.Theme ??
        throw new InvalidOperationException("Realtime HUD surface has no assigned theme.");
    internal Control? FocusOwnerForSmoke => GetViewport().GuiGetFocusOwner();

    internal void SetLayersVisibleForSmoke(bool visible)
    {
        _hudSurface.Visible = visible;
        if (!visible)
        {
            _modalHost.Visible = false;
        }
    }

    internal BaseButton PrimaryActionForSmoke() => AllControlsForSmoke()
        .OfType<BaseButton>()
        .Single(item => item.IsVisibleInTree() &&
            item.ThemeTypeVariation == "PrimaryButton");

    internal Vector2 PrimaryActionCenterForSmoke() =>
        PrimaryActionForSmoke().GetGlobalRect().GetCenter();

    internal IReadOnlyList<BaseButton> FocusableButtonsForSmoke() =>
        Array.AsReadOnly(AllControlsForSmoke()
            .OfType<BaseButton>()
            .Where(item => item.IsVisibleInTree() && !item.Disabled &&
                item.FocusMode != Control.FocusModeEnum.None)
            .OrderBy(item => item.GetPath().ToString(), StringComparer.Ordinal)
            .ToArray());

    internal void ApplyRuntimeThenSyntheticParityForSmoke(
        Vector2I physicalSize,
        Vector2 logicalSize,
        int uiScalePercent)
    {
        _uiScalePercent = uiScalePercent;
        ApplyResponsiveLayout();
        RealtimeUiSmokeSurfaceFact[] runtime =
        [
            Surface("TopHud", _topHud),
            Surface("EventRail", _eventRail),
            Surface("ContextDock", _contextDock),
            Surface("BuildShelf", _buildShelf),
            Surface("ActionDock", _actionDock),
        ];
        ApplyLayoutForSmoke(physicalSize, logicalSize, uiScalePercent);
        RealtimeUiSmokeSurfaceFact[] injected =
        [
            Surface("TopHud", _topHud),
            Surface("EventRail", _eventRail),
            Surface("ContextDock", _contextDock),
            Surface("BuildShelf", _buildShelf),
            Surface("ActionDock", _actionDock),
        ];
        for (int index = 0; index < runtime.Length; index++)
        {
            if (runtime[index].Visible != injected[index].Visible ||
                runtime[index].Visible && !RectApproximatelyEqual(
                    runtime[index].Rect,
                    injected[index].Rect))
            {
                throw new InvalidOperationException(
                    $"Synthetic layout diverged from runtime for {runtime[index].Id}.");
            }
        }
    }

    private void ApplySyntheticSurfaceRectsForSmoke(Vector2 logicalSize)
    {
        RealtimeLayoutProfile surfaceProfile = _layoutProfile with
        {
            EventRailHeight = _eventRail.DesiredHeight,
        };
        RealtimeSurfaceLayout layout = RealtimeUiMetrics.CalculateSurfaceLayout(
            logicalSize,
            surfaceProfile,
            _contextDock.Visible,
            _buildShelf.Visible,
            _actionDock.Visible);
        ApplyRect(_topHud, layout.TopHud);
        ApplyRect(_eventRail, layout.EventRail);
        ApplyRect(_contextDock, layout.ContextDock);
        ApplyRect(_buildShelf, layout.BuildShelf);
        ApplyRect(_actionDock, layout.ActionDock);
    }

    private IEnumerable<Control> AllControlsForSmoke()
    {
        foreach (Control control in GetChildrenForSmoke(_hudSurface))
        {
            yield return control;
        }
        foreach (Control control in GetChildrenForSmoke(_modalHost))
        {
            yield return control;
        }
    }

    private Control SurfaceControlForSmoke(string id) => id switch
    {
        "TopHud" => _topHud,
        "EventRail" => _eventRail,
        "ContextDock" => _contextDock,
        "BuildShelf" => _buildShelf,
        "ActionDock" => _actionDock,
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };

    private static IEnumerable<RealtimeUiSmokeControlFact> DescendantFactsForSmoke(
        string ownerSurfaceId,
        Control surface)
    {
        foreach (Control control in GetChildrenForSmoke(surface))
        {
            if (ReferenceEquals(control, surface))
            {
                continue;
            }
            bool visibleInTree = control.IsVisibleInTree();
            Rect2 rect = control.GetGlobalRect();
            Rect2 visible = rect;
            var clipAncestorPaths = new List<string>();
            Node? ancestor = control.GetParent();
            while (ancestor is Control parent)
            {
                if (parent.ClipContents || parent is ScrollContainer)
                {
                    visible = visible.Intersection(parent.GetGlobalRect());
                    clipAncestorPaths.Add(PathForSmoke(parent));
                }
                if (ReferenceEquals(parent, surface))
                {
                    break;
                }
                ancestor = parent.GetParent();
            }
            yield return new RealtimeUiSmokeControlFact(
                PathForSmoke(control),
                ownerSurfaceId,
                rect,
                visible,
                control.GetCombinedMinimumSize(),
                visibleInTree,
                clipAncestorPaths.Count > 0,
                Array.AsReadOnly(clipAncestorPaths.ToArray()));
        }
    }

    private static IEnumerable<Control> GetChildrenForSmoke(Node root)
    {
        if (root is Control control)
        {
            yield return control;
        }
        foreach (Node child in root.GetChildren())
        {
            foreach (Control descendant in GetChildrenForSmoke(child))
            {
                yield return descendant;
            }
        }
    }

    private static RealtimeUiSmokeTextFact TextFactForSmoke(Control control)
    {
        string text;
        bool button;
        bool wrapped;
        int lineCount;
        int visibleLineCount;
        float availableWidth = control.Size.X;
        Font font = control.GetThemeFont("font");
        int fontSize = control.GetThemeFontSize("font_size");
        bool fullyVisible;
        if (control is Label label)
        {
            text = label.Text;
            button = false;
            wrapped = label.AutowrapMode != TextServer.AutowrapMode.Off;
            lineCount = label.GetLineCount();
            visibleLineCount = label.GetVisibleLineCount();
            float requiredWidth = font.GetStringSize(
                text,
                HorizontalAlignment.Left,
                -1,
                fontSize).X;
            fullyVisible = wrapped
                ? visibleLineCount >= lineCount
                : requiredWidth <= availableWidth + 1f &&
                  font.GetHeight(fontSize) <= control.Size.Y + 1f;
            return new RealtimeUiSmokeTextFact(
                PathForSmoke(control),
                text,
                button,
                wrapped,
                requiredWidth,
                availableWidth,
                lineCount,
                visibleLineCount,
                fullyVisible);
        }

        var baseButton = (BaseButton)control;
        text = control is Button textButton
            ? textButton.Text
            : baseButton.AccessibilityName;
        button = true;
        wrapped = false;
        lineCount = 1;
        visibleLineCount = 1;
        StyleBox style = control.GetThemeStylebox("normal");
        availableWidth = Math.Max(
            0f,
            control.Size.X - style.GetMargin(Side.Left) - style.GetMargin(Side.Right));
        float buttonWidth = font.GetStringSize(
            text,
            HorizontalAlignment.Left,
            -1,
            fontSize).X;
        fullyVisible = buttonWidth <= availableWidth + 1f &&
                       font.GetHeight(fontSize) <= control.Size.Y + 1f;
        return new RealtimeUiSmokeTextFact(
            PathForSmoke(control),
            text,
            button,
            wrapped,
            buttonWidth,
            availableWidth,
            lineCount,
            visibleLineCount,
            fullyVisible);
    }

    private static RealtimeUiSmokeSurfaceFact Surface(string id, Control control) =>
        new(
            id,
            new Rect2(control.Position, control.Size),
            control.GetGlobalRect(),
            control.GetCombinedMinimumSize(),
            control.IsVisibleInTree());

    private static string PathForSmoke(Node node) => node.GetPath().ToString();

    private static bool RectApproximatelyEqual(Rect2 left, Rect2 right) =>
        left.Position.IsEqualApprox(right.Position) && left.Size.IsEqualApprox(right.Size);
}

internal sealed partial class RealtimeTopHud
{
    internal BaseButton MenuButtonForSmoke => _menu;

    internal string ObjectiveTextForSmoke => _objective.Text;
    internal string ObjectiveAccessibilityForSmoke => _objective.AccessibilityName;
    internal string PauseStatusTextForSmoke => _pauseStatus.Text;
    internal string PauseStatusAccessibilityForSmoke => _pauseStatus.AccessibilityName;
    internal string AccessibilitySummaryForSmoke => AccessibilityName;
    internal string MenuTextForSmoke => _menu.Text;
    internal string MenuTooltipForSmoke => _menu.TooltipText;

    internal IReadOnlyList<RealtimeUiSmokeSpeedFact> SpeedFactsForSmoke =>
        Array.AsReadOnly(_speedButtons
            .OrderBy(item => (int)item.Key)
            .Select(item => new RealtimeUiSmokeSpeedFact(
                item.Key,
                !item.Value.Disabled,
                item.Value.ButtonPressed,
                item.Value.Text,
                item.Value.TooltipText,
                item.Value.AccessibilityName,
                item.Value.AccessibilityDescription,
                item.Value.GetGlobalRect()))
            .ToArray());

    internal void PressMenuForSmoke() =>
        _menu.EmitSignal(BaseButton.SignalName.Pressed);

    internal void PressSpeedForSmoke(RealtimeSimulationSpeed speed) =>
        _speedButtons[speed].EmitSignal(BaseButton.SignalName.Pressed);

    internal Vector2 MenuCenterForSmoke => _menu.GetGlobalRect().GetCenter();

    internal Vector2 SpeedCenterForSmoke(RealtimeSimulationSpeed speed) =>
        _speedButtons[speed].GetGlobalRect().GetCenter();
}

internal sealed partial class RealtimeBuildShelf
{
    internal string GuidanceTextForSmoke => _guidance.Text;

    internal (Rect2 Rect, bool Visible, bool Enabled, bool Pressed)
        ToolHitFactForSmoke(string toolId)
    {
        if (!_buttons.TryGetValue(toolId, out Button? button))
        {
            throw new InvalidOperationException($"Build tool {toolId} is not live.");
        }
        return (
            button.GetGlobalRect(),
            button.IsVisibleInTree(),
            !button.Disabled,
            button.ButtonPressed);
    }

    internal Vector2 ToolCenterForSmoke(string toolId)
    {
        if (!_buttons.TryGetValue(toolId, out Button? button) ||
            !button.IsVisibleInTree() || button.Disabled)
        {
            throw new InvalidOperationException(
                $"Build tool {toolId} is not live and enabled.");
        }
        return button.GetGlobalRect().GetCenter();
    }

    internal void PressToolForSmoke(string toolId)
    {
        if (!_buttons.TryGetValue(toolId, out Button? button) ||
            !button.IsVisibleInTree() || button.Disabled)
        {
            throw new InvalidOperationException(
                $"Build tool {toolId} is not live and enabled.");
        }
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }
}

internal sealed partial class RealtimeActionDock
{
    internal string DetailTextForSmoke => _detail.Text;
}

internal sealed partial class RealtimeEventRail
{
    internal int LaneLabelCountForSmoke => _laneLabels.GetChildCount();
    internal string? FocusedItemIdForSmoke => FocusedItemId();

    internal IReadOnlyList<string> FocusedMarkerItemIdsForSmoke
    {
        get
        {
            Control? focus = GetViewport().GuiGetFocusOwner();
            MarkerEntry? entry = _markers.FirstOrDefault(item =>
                ReferenceEquals(item.Button, focus));
            return entry is null
                ? Array.Empty<string>()
                : Array.AsReadOnly(entry.ItemIds.ToArray());
        }
    }

    internal void RebuildMarkersForSmoke() => RebuildMarkers();

    internal void GrabMarkerFocusOnlyForSmoke(string itemId)
    {
        MarkerEntry? entry = _markers.FirstOrDefault(item =>
            item.ItemIds.Contains(itemId, StringComparer.Ordinal));
        if (entry is null)
        {
            throw new InvalidOperationException(
                $"No rendered timeline marker contains {itemId}.");
        }
        entry.Button.GrabFocus();
    }

    internal void FocusMarkerForSmoke(string itemId)
    {
        MarkerEntry? entry = _markers.FirstOrDefault(item =>
            item.ItemIds.Contains(itemId, StringComparer.Ordinal));
        if (entry is null)
        {
            throw new InvalidOperationException(
                $"No rendered timeline marker contains {itemId}.");
        }
        entry.Button.GrabFocus();
        entry.Button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    internal void PressShorterHorizonForSmoke() =>
        _shorterHorizon.EmitSignal(BaseButton.SignalName.Pressed);

    internal void PressLongerHorizonForSmoke() =>
        _longerHorizon.EmitSignal(BaseButton.SignalName.Pressed);

    internal void PressExpansionForSmoke() =>
        _expandLanes.EmitSignal(BaseButton.SignalName.Pressed);

    internal void PressPreviousEventForSmoke() =>
        _previousEvent.EmitSignal(BaseButton.SignalName.Pressed);

    internal Vector2 PreviousEventCenterForSmoke =>
        _previousEvent.GetGlobalRect().GetCenter();

    internal void PressCurrentTimeForSmoke() =>
        _currentTime.EmitSignal(BaseButton.SignalName.Pressed);

    internal Vector2 CurrentTimeCenterForSmoke =>
        _currentTime.GetGlobalRect().GetCenter();

    internal void PressNextEventForSmoke() =>
        _nextEvent.EmitSignal(BaseButton.SignalName.Pressed);

    internal Vector2 NextEventCenterForSmoke =>
        _nextEvent.GetGlobalRect().GetCenter();

    internal Vector2 MarkerCenterForSmoke(string itemId)
    {
        MarkerEntry? entry = _markers.FirstOrDefault(item =>
            item.ItemIds.Contains(itemId, StringComparer.Ordinal));
        if (entry is null)
        {
            throw new InvalidOperationException(
                $"No rendered timeline marker contains {itemId}.");
        }
        return entry.Button.GetGlobalRect().GetCenter();
    }

    internal IReadOnlyList<RealtimeUiSmokeTimelineNavigationFact>
        NavigationFactsForSmoke => Array.AsReadOnly(new[]
        {
            NavigationFact(RealtimeTimelineNavigation.PreviousEvent, _previousEvent),
            NavigationFact(RealtimeTimelineNavigation.Home, _currentTime),
            NavigationFact(RealtimeTimelineNavigation.NextEvent, _nextEvent),
        });

    internal IReadOnlyList<RealtimeUiSmokeAccessibleTimelineItemFact>
        AccessibleTimelineItemsForSmoke => Array.AsReadOnly(_accessibleItemIds
            .Select((itemId, index) =>
            {
                int optionIndex = index + 1;
                return new RealtimeUiSmokeAccessibleTimelineItemFact(
                    optionIndex,
                    itemId,
                    _accessibleEventSelector.GetItemText(optionIndex),
                    _accessibleEventSelector.GetItemTooltip(optionIndex),
                    _accessibleEventSelector.IsItemDisabled(optionIndex),
                    _accessibleEventSelector.Selected == optionIndex);
            })
            .ToArray());

    internal RealtimeUiSmokeAccessibleTimelineClosedFact
        AccessibleTimelineClosedForSmoke
    {
        get
        {
            Font font = _accessibleEventSelector.GetThemeFont("font");
            int fontSize = _accessibleEventSelector.GetThemeFontSize("font_size");
            StyleBox style = _accessibleEventSelector.GetThemeStylebox("normal");
            Texture2D? arrow = _accessibleEventSelector.GetThemeIcon("arrow");
            float arrowWidth = arrow is not null && GodotObject.IsInstanceValid(arrow)
                ? arrow.GetWidth()
                : 0f;
            float arrowMargin = _accessibleEventSelector.GetThemeConstant(
                "arrow_margin");
            float availableWidth = Math.Max(
                0f,
                _accessibleEventSelector.Size.X -
                style.GetMargin(Side.Left) -
                style.GetMargin(Side.Right) -
                arrowWidth -
                arrowMargin);
            float availableHeight = Math.Max(
                0f,
                _accessibleEventSelector.Size.Y -
                style.GetMargin(Side.Top) -
                style.GetMargin(Side.Bottom));
            Vector2 required = font.GetStringSize(
                _accessibleEventSelector.Text,
                HorizontalAlignment.Left,
                -1f,
                fontSize);
            return new RealtimeUiSmokeAccessibleTimelineClosedFact(
                _accessibleEventSelector.GetGlobalRect(),
                _accessibleEventSelector.Text,
                _accessibleEventSelector.AccessibilityName,
                _accessibleEventSelector.AccessibilityDescription,
                required.X,
                availableWidth,
                font.GetHeight(fontSize),
                availableHeight,
                _accessibleEventSelector.ClipText);
        }
    }

    internal RealtimeUiSmokeAccessibleTimelinePopupFact
        AccessibleTimelinePopupForSmoke
    {
        get
        {
            PopupMenu popup = _accessibleEventSelector.GetPopup();
            int fontSize = popup.GetThemeFontSize("font_size");
            Font font = popup.GetThemeFont("font");
            float fontHeight = font.GetHeight(fontSize);
            int verticalSeparation = popup.GetThemeConstant("v_separation");
            int itemCount = popup.ItemCount;
            int enabledItemCount = Enumerable.Range(0, itemCount)
                .Count(index => !popup.IsItemDisabled(index));
            return new RealtimeUiSmokeAccessibleTimelinePopupFact(
                itemCount,
                enabledItemCount,
                fontSize,
                fontHeight,
                verticalSeparation,
                fontHeight + verticalSeparation,
                popup.GetThemeConstant("item_start_padding"),
                popup.GetThemeConstant("item_end_padding"));
        }
    }

    internal void SelectAccessibleTimelineItemForSmoke(string itemId)
    {
        int itemIndex = _accessibleItemIds.FindIndex(candidate => string.Equals(
            candidate,
            itemId,
            StringComparison.Ordinal));
        if (itemIndex < 0)
        {
            throw new InvalidOperationException(
                $"No accessible timeline option maps to {itemId}.");
        }
        int optionIndex = itemIndex + 1;
        _accessibleEventSelector.Select(optionIndex);
        _accessibleEventSelector.EmitSignal(
            OptionButton.SignalName.ItemSelected,
            optionIndex);
    }

    internal (long BeforeMinute, long AfterMinute) LegacyBucketBoundaryPairForSmoke()
    {
        if (_presentation is null || _track.Size.X <= 1f)
        {
            throw new InvalidOperationException("Timeline track is not ready.");
        }
        int bucketCount = Math.Max(2, (int)MathF.Floor(_track.Size.X / _markerWidth));
        long duration = _presentation.HorizonEndMinute -
            _presentation.HorizonStartMinute;
        long boundary = _presentation.HorizonStartMinute +
            Math.Max(2L, duration / bucketCount);
        return (boundary - 1, boundary + 1);
    }

    internal IReadOnlyList<RealtimeUiSmokeMarkerFact> MarkerFactsForSmoke()
    {
        var facts = new List<RealtimeUiSmokeMarkerFact>(_markers.Count);
        foreach (MarkerEntry entry in _markers)
        {
            RealtimeTimelineItemPresentation? item = _presentation?.Items.FirstOrDefault(
                candidate => entry.ItemIds.Contains(candidate.Id, StringComparer.Ordinal));
            if (item is null)
            {
                continue;
            }
            MarkerEntry? left = NeighborEntryForSmoke(
                entry.Button,
                entry.Button.FocusNeighborLeft);
            MarkerEntry? right = NeighborEntryForSmoke(
                entry.Button,
                entry.Button.FocusNeighborRight);
            facts.Add(new RealtimeUiSmokeMarkerFact(
                Array.AsReadOnly(entry.ItemIds.ToArray()),
                item.Lane,
                DisplayLane(item.Lane),
                entry.Button.GetGlobalRect(),
                Array.AsReadOnly(left?.ItemIds.ToArray() ?? Array.Empty<string>()),
                Array.AsReadOnly(right?.ItemIds.ToArray() ?? Array.Empty<string>()),
                entry.Button.Text,
                entry.Button.AccessibilityName,
                entry.Button.AccessibilityDescription,
                entry.Button.ButtonPressed,
                entry.Button.HasThemeConstantOverride("outline_size")
                    ? entry.Button.GetThemeConstant("outline_size")
                    : 0,
                entry.SemanticItemId,
                entry.Button.GetInstanceId(),
                entry.Button.GetThemeFontSize("font_size"),
                FocusBorderWidthForSmoke(entry.Button),
                FocusExpandMarginForSmoke(entry.Button)));
        }
        return Array.AsReadOnly(facts.ToArray());
    }

    private static RealtimeUiSmokeTimelineNavigationFact NavigationFact(
        RealtimeTimelineNavigation navigation,
        Button button) => new(
        navigation,
        button.GetGlobalRect(),
        !button.Disabled,
        button.Text,
        button.AccessibilityName,
        button.AccessibilityDescription);

    private static int FocusBorderWidthForSmoke(Button button) =>
        button.GetThemeStylebox("focus") is StyleBoxFlat focus
            ? new[]
            {
                focus.BorderWidthLeft,
                focus.BorderWidthTop,
                focus.BorderWidthRight,
                focus.BorderWidthBottom,
            }.Min()
            : 0;

    private static float FocusExpandMarginForSmoke(Button button) =>
        button.GetThemeStylebox("focus") is StyleBoxFlat focus
            ? new[]
            {
                focus.ExpandMarginLeft,
                focus.ExpandMarginTop,
                focus.ExpandMarginRight,
                focus.ExpandMarginBottom,
            }.Min()
            : 0f;

    private MarkerEntry? NeighborEntryForSmoke(Button source, NodePath path)
    {
        if (path.IsEmpty)
        {
            return null;
        }
        Button? neighbor = source.GetNodeOrNull<Button>(path);
        return neighbor is null
            ? null
            : _markers.FirstOrDefault(entry => ReferenceEquals(entry.Button, neighbor));
    }
}

internal sealed partial class RealtimeContextDock
{
    internal bool CompactOverviewActiveForSmoke =>
        _compactDetails && _showingOverview && _detailPresentations.Count > 0;

    internal bool DetailTabActiveForSmoke =>
        _detailPresentations.Count > 0 && !_showingOverview;

    internal Vector2 CloseCenterForSmoke => _close.GetGlobalRect().GetCenter();
    internal string CloseAccessibilityNameForSmoke => _close.AccessibilityName;
    internal string CloseAccessibilityDescriptionForSmoke =>
        _close.AccessibilityDescription;
    internal string AccessibilitySummaryForSmoke => AccessibilityName;

    internal void PressFirstDetailTabForSmoke()
    {
        Button tab = _tabButtons
            .OrderBy(item => item.Key)
            .Select(item => item.Value)
            .FirstOrDefault() ?? throw new InvalidOperationException(
                "The active context presentation has no detail tab.");
        tab.EmitSignal(BaseButton.SignalName.Pressed);
    }
}

internal sealed partial class RealtimeModalHost
{
    internal Control? BackgroundReturnFocusForSmoke => _backgroundReturnFocus;
    internal string AccessibilitySummaryForSmoke => AccessibilityName;
    internal string PauseStatusTextForSmoke => _pauseStatus.Text;
    internal string PauseStatusAccessibilityForSmoke => _pauseStatus.AccessibilityName;
    internal void PressPrimaryForSmoke() =>
        _primary.EmitSignal(BaseButton.SignalName.Pressed);
    internal Vector2 PrimaryCenterForSmoke => _primary.GetGlobalRect().GetCenter();
    internal string PrimaryTextForSmoke => _primary.Text;
    internal bool PrimaryVisibleForSmoke => _primary.IsVisibleInTree();
    internal bool PrimaryEnabledForSmoke => !_primary.Disabled;
    internal string PrimaryThemeForSmoke => _primary.ThemeTypeVariation.ToString();
    internal string SecondaryTextForSmoke => _secondary.Text;
    internal bool SecondaryVisibleForSmoke => _secondary.IsVisibleInTree();
    internal string? ActivePrimaryActionIdForSmoke =>
        _active?.PrimaryAction.Id;
    internal string? ActiveSecondaryActionIdForSmoke =>
        _active?.SecondaryAction?.Id;
    internal RealtimeUiSmokeSurfaceFact ModalPanelFactForSmoke() => new(
        "ModalPanel",
        new Rect2(_panel.Position, _panel.Size),
        _panel.GetGlobalRect(),
        _panel.GetCombinedMinimumSize(),
        _panel.IsVisibleInTree());
    internal IReadOnlyList<RealtimeUiSmokeFocusLinkFact> FocusLinksForSmoke()
    {
        BaseButton[] buttons = { _secondary, _primary };
        return Array.AsReadOnly(buttons
            .Where(button => button.IsVisibleInTree() && !button.Disabled)
            .Select(button =>
            {
                Control? next = button.FocusNext.IsEmpty
                    ? null
                    : button.GetNodeOrNull<Control>(button.FocusNext);
                Control? previous = button.FocusPrevious.IsEmpty
                    ? null
                    : button.GetNodeOrNull<Control>(button.FocusPrevious);
                return new RealtimeUiSmokeFocusLinkFact(
                    PathForModalSmoke(button),
                    next is null ? string.Empty : PathForModalSmoke(next),
                    previous is null ? string.Empty : PathForModalSmoke(previous),
                    next is not null && (ReferenceEquals(next, this) || IsAncestorOf(next)),
                    previous is not null &&
                        (ReferenceEquals(previous, this) || IsAncestorOf(previous)));
            })
            .ToArray());
    }

    internal bool OwnsFocusForSmoke
    {
        get
        {
            Control? focus = GetViewport().GuiGetFocusOwner();
            return focus is not null && (ReferenceEquals(focus, this) || IsAncestorOf(focus));
        }
    }

    private static string PathForModalSmoke(Node node) => node.GetPath().ToString();
}
#endif
