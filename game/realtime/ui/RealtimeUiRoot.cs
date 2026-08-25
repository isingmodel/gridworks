using System;
using System.Collections.Generic;
using Godot;

namespace Gridworks.Game.Realtime.UI;

internal sealed partial class RealtimeUiRoot : Node
{
    private Control _hudSurface = null!;
    private RealtimeTopHud _topHud = null!;
    private RealtimeEventRail _eventRail = null!;
    private RealtimeContextDock _contextDock = null!;
    private RealtimeBuildShelf _buildShelf = null!;
    private RealtimeActionDock _actionDock = null!;
    private RealtimeModalHost _modalHost = null!;
    private RealtimeProductTitle _productTitle = null!;
    private RealtimeInputRouter _inputRouter = null!;
    private RealtimeActionDockPresentation? _actionDockPresentation;
    private RealtimeBuildShelfPresentation? _buildShelfPresentation;
    private bool _contextOwnsPrimaryAction;
    private bool _timelineOwnsWorkspace;
    private bool _surfaceStabilizationQueued;
    private bool _typographyQueued;
    private int _surfaceStabilizationPassesRemaining;
    private long? _modalInputToken;
    private long? _titleInputToken;
    private RealtimeLayoutProfile _layoutProfile;
    private readonly Dictionary<ulong, int> _baseFontSizes = [];
    private int _uiScalePercent = 100;

    [Export(PropertyHint.Enum, "100%:100,125%:125,150%:150,200%:200")]
    public int UiScalePercent
    {
        get => _uiScalePercent;
        set
        {
            if (value is not (100 or 125 or 150 or 200))
            {
                GD.PushWarning($"Unsupported realtime UI scale {value}; keeping {_uiScalePercent}%.");
                return;
            }
            _uiScalePercent = value;
            if (IsInsideTree())
            {
                ApplyResponsiveLayout();
            }
        }
    }

    public RealtimeLayoutProfile LayoutProfile => _layoutProfile;

    public bool CanWorldReceiveInput =>
        _inputRouter.CanReceive(RealtimeInputPriority.EmptyTerrain);

    public RealtimeInputRouter InputRouter => _inputRouter;

    public RealtimeModalHost ModalHost => _modalHost;

    public event Action<RealtimeSimulationSpeed>? SpeedRequested;
    public event Action? MenuRequested;
    public event Action<int>? TimelineHorizonDeltaRequested;
    public event Action<IReadOnlyList<string>>? TimelineItemsRequested;
    public event Action<RealtimeTimelineNavigation>? TimelineNavigationRequested;
    public event Action<bool>? TimelineExpansionRequested;
    public event Action? ContextCloseRequested;
    public event Action<string>? ActionRequested;
    public event Action<string>? BuildToolRequested;
    public event Action<string, string>? ModalActionRequested;
    public event Action<string>? ModalDismissRequested;
    public event Action<RealtimeInputRequest>? InputRequested;
    public event Action<Rect2>? MapInteractionRectChanged;
    public event Action? NewGameRequested;

    public override void _Ready()
    {
        SetProcess(false);
        _hudSurface = GetNode<Control>("%HudSurface");
        _topHud = GetNode<RealtimeTopHud>("%TopHud");
        _eventRail = GetNode<RealtimeEventRail>("%EventRail");
        _contextDock = GetNode<RealtimeContextDock>("%ContextDock");
        _buildShelf = GetNode<RealtimeBuildShelf>("%BuildShelf");
        _actionDock = GetNode<RealtimeActionDock>("%ActionDock");
        _modalHost = GetNode<RealtimeModalHost>("%ModalHost");
        _productTitle = GetNode<RealtimeProductTitle>("%ProductTitle");
        _inputRouter = GetNode<RealtimeInputRouter>("%InputRouter");

        _topHud.SpeedRequested += speed => SpeedRequested?.Invoke(speed);
        _topHud.MenuRequested += () => MenuRequested?.Invoke();
        _eventRail.HorizonDeltaRequested += delta =>
            TimelineHorizonDeltaRequested?.Invoke(delta);
        _eventRail.ItemsRequested += ids => TimelineItemsRequested?.Invoke(ids);
        _eventRail.NavigationRequested += navigation =>
            TimelineNavigationRequested?.Invoke(navigation);
        _eventRail.DesiredHeightChanged += _ => ApplySurfaceRects();
        _eventRail.ExpansionRequested += expanded =>
            TimelineExpansionRequested?.Invoke(expanded);
        _contextDock.CloseRequested += () => ContextCloseRequested?.Invoke();
        _contextDock.ActionRequested += id => ActionRequested?.Invoke(id);
        _buildShelf.ToolRequested += id => BuildToolRequested?.Invoke(id);
        _actionDock.PrimaryActionRequested += id => ActionRequested?.Invoke(id);
        _modalHost.ActionRequested += (modalId, actionId) =>
            ModalActionRequested?.Invoke(modalId, actionId);
        _modalHost.DismissRequested += id => ModalDismissRequested?.Invoke(id);
        _modalHost.DepthChanged += OnModalDepthChanged;
        _productTitle.NewGameRequested += () => NewGameRequested?.Invoke();
        _inputRouter.InputRequested += RouteShortcut;
        GetViewport().SizeChanged += ApplyResponsiveLayout;
        CallDeferred(nameof(ApplyResponsiveLayout));
    }

    public override void _Process(double delta)
    {
        if (_surfaceStabilizationPassesRemaining <= 0)
        {
            SetProcess(false);
            return;
        }
        ApplySurfaceRectsCore();
        _surfaceStabilizationPassesRemaining--;
        if (_surfaceStabilizationPassesRemaining == 0)
        {
            SetProcess(false);
        }
    }

    public override void _ExitTree()
    {
        if (GetViewport() is Viewport viewport)
        {
            viewport.SizeChanged -= ApplyResponsiveLayout;
        }
    }

    public void SetTopHud(RealtimeTopHudPresentation presentation) =>
        Present(() => _topHud.SetPresentation(presentation));

    public void SetEventRail(RealtimeEventRailPresentation presentation)
    {
        _timelineOwnsWorkspace = presentation.Expanded;
        _eventRail.SetPresentation(presentation);
        RenderCommandDocks();
        ApplySurfaceRects();
        ScheduleTypography();
    }

    public void SetContextDock(RealtimeContextDockPresentation presentation)
    {
        _contextDock.SetPresentation(presentation);
        _contextDock.Visible = presentation.Visible && !_timelineOwnsWorkspace;
        _contextOwnsPrimaryAction = _contextDock.Visible &&
            presentation.PrimaryAction is { Visible: true };
        RenderCommandDocks();
        ApplySurfaceRects();
        ScheduleTypography();
    }

    public void SetBuildShelf(RealtimeBuildShelfPresentation presentation)
    {
        _buildShelfPresentation = presentation;
        RenderCommandDocks();
        ApplySurfaceRects();
        ScheduleTypography();
    }

    public void SetActionDock(RealtimeActionDockPresentation presentation)
    {
        _actionDockPresentation = presentation;
        RenderCommandDocks();
        ApplySurfaceRects();
        ScheduleTypography();
    }

    public bool PushModal(RealtimeModalPresentation presentation) =>
        _modalHost.PushModal(presentation);

    public bool PopModal(bool dismissed = false) => _modalHost.PopModal(dismissed);

    public void ShowProductTitle(RealtimeProductTitlePresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        _hudSurface.Visible = false;
        if (!_titleInputToken.HasValue)
        {
            _inputRouter.CancelPanCapture();
            _titleInputToken = _inputRouter.PushContext(
                "product_title",
                RealtimeInputPriority.BlockingModal);
        }
        _productTitle.Present(presentation);
        ScheduleTypography();
    }

    public void HideProductTitle()
    {
        _productTitle.Dismiss();
        if (_titleInputToken.HasValue)
        {
            _inputRouter.PopContext(_titleInputToken.Value);
            _titleInputToken = null;
        }
        _hudSurface.Visible = true;
    }

    /// <summary>
    /// Routes a timeline shortcut through the rail's single navigation owner
    /// so controller selection and semantic marker focus advance together.
    /// </summary>
    public bool NavigateTimeline(RealtimeTimelineNavigation navigation) =>
        _eventRail.Navigate(navigation);

    public long ClaimInput(string owner, RealtimeInputPriority priority) =>
        _inputRouter.PushContext(owner, priority);

    public bool ReleaseInput(long token) => _inputRouter.PopContext(token);

    public void ApplyResponsiveLayout()
    {
        Vector2I physicalSize = DisplayServer.WindowGetSize();
        if (physicalSize.X <= 0 || physicalSize.Y <= 0)
        {
            Vector2 logical = GetViewport().GetVisibleRect().Size;
            physicalSize = new Vector2I(
                Math.Max(1, Mathf.RoundToInt(logical.X)),
                Math.Max(1, Mathf.RoundToInt(logical.Y)));
        }
        _layoutProfile = RealtimeUiMetrics.ForWindow(physicalSize, UiScalePercent);
        _topHud.ApplyLayout(_layoutProfile);
        _eventRail.ApplyLayout(_layoutProfile);
        _contextDock.ApplyLayout(_layoutProfile);
        _buildShelf.ApplyLayout(_layoutProfile);
        _actionDock.ApplyLayout(_layoutProfile);
        _modalHost.ApplyLayout(_layoutProfile);
        _productTitle.ApplyLayout(_layoutProfile);
        // Font, container minimum, and native window-mode notifications do
        // not settle in one Godot layout turn. Reassert the single layout
        // authority over a bounded sequence of deferred turns so a panel that
        // was previously taller cannot keep its obsolete expanded allocation.
        ApplySurfaceRects(stabilizationPasses: 8);
        ApplyTypography();
    }

    private void ApplySurfaceRects() => ApplySurfaceRects(stabilizationPasses: 1);

    private void ApplySurfaceRects(int stabilizationPasses)
    {
        if (!IsInsideTree())
        {
            return;
        }

        if (stabilizationPasses > 1)
        {
            _surfaceStabilizationPassesRemaining = Math.Max(
                _surfaceStabilizationPassesRemaining,
                stabilizationPasses);
            SetProcess(true);
        }
        ApplySurfaceRectsCore();
        if (!_surfaceStabilizationQueued)
        {
            _surfaceStabilizationQueued = true;
            CallDeferred(nameof(StabilizeSurfaceRects));
        }
    }

    private void StabilizeSurfaceRects()
    {
        _surfaceStabilizationQueued = false;
        if (IsInsideTree())
        {
            ApplySurfaceRectsCore();
        }
    }

    private void ApplySurfaceRectsCore()
    {

        RealtimeLayoutProfile surfaceProfile = _layoutProfile with
        {
            EventRailHeight = _eventRail.DesiredHeight,
        };
        RealtimeSurfaceLayout layout = RealtimeUiMetrics.CalculateSurfaceLayout(
            GetViewport().GetVisibleRect().Size,
            surfaceProfile,
            _contextDock.Visible,
            _buildShelf.Visible,
            _actionDock.Visible);
        ApplyRect(_topHud, layout.TopHud);
        ApplyRect(_eventRail, layout.EventRail);
        ApplyRect(_contextDock, layout.ContextDock);
        _contextDock.ReflowToAssignedSize();
        ApplyRect(_buildShelf, layout.BuildShelf);
        ApplyRect(_actionDock, layout.ActionDock);
        MapInteractionRectChanged?.Invoke(layout.MapInteraction);
    }

    private void RenderCommandDocks()
    {
        bool actionOwnsDock = !_contextOwnsPrimaryAction &&
            _actionDockPresentation is
            {
                Visible: true,
                PrimaryAction.Visible: true,
            };
        if (_actionDockPresentation is RealtimeActionDockPresentation action)
        {
            _actionDock.SetPresentation((_contextOwnsPrimaryAction || _timelineOwnsWorkspace)
                ? action with { Visible = false }
                : action);
        }
        if (_buildShelfPresentation is RealtimeBuildShelfPresentation shelf)
        {
            _buildShelf.SetPresentation((_contextOwnsPrimaryAction || actionOwnsDock ||
                                         _timelineOwnsWorkspace)
                ? shelf with { Visible = false }
                : shelf);
        }
    }

    private void OnModalDepthChanged(int depth)
    {
        if (depth > 0 && !_modalInputToken.HasValue)
        {
            _inputRouter.CancelPanCapture();
            _modalInputToken = _inputRouter.PushContext(
                "modal_host",
                RealtimeInputPriority.BlockingModal);
        }
        else if (depth == 0 && _modalInputToken.HasValue)
        {
            _inputRouter.PopContext(_modalInputToken.Value);
            _modalInputToken = null;
        }
    }

    private void RouteShortcut(RealtimeInputRequest request)
    {
        if (_productTitle.Visible)
        {
            return;
        }
        if (_modalHost.Depth > 0)
        {
            if (request.Command == RealtimeInputCommand.CancelOrBack)
            {
                _modalHost.HandleCancel();
            }
            return;
        }
        InputRequested?.Invoke(request);
    }

    private void Present(Action presenter)
    {
        presenter();
        ScheduleTypography();
    }

    private void ScheduleTypography()
    {
        if (IsInsideTree() && !_typographyQueued)
        {
            _typographyQueued = true;
            CallDeferred(nameof(ApplyTypography));
        }
    }

    private void ApplyTypography()
    {
        _typographyQueued = false;
        ApplyTypographyRecursive(_hudSurface);
        ApplyTypographyRecursive(_modalHost);
        ApplyTypographyRecursive(_productTitle);
        // Font changes invalidate combined minimum sizes. Reapply the single
        // surface authority on the following idle turn so a 200%→100%
        // round-trip cannot retain an enlarged control rectangle.
        if (IsInsideTree())
        {
            CallDeferred(nameof(ApplySurfaceRects));
        }
    }

    private void ApplyTypographyRecursive(Node node)
    {
        if (node is Control control && (control is Label || control is BaseButton))
        {
            ulong id = control.GetInstanceId();
            if (!_baseFontSizes.TryGetValue(id, out int baseSize))
            {
                baseSize = control.GetThemeFontSize("font_size");
                _baseFontSizes.Add(id, baseSize);
            }
            control.AddThemeFontSizeOverride(
                "font_size",
                Math.Max(1, Mathf.RoundToInt(baseSize * _layoutProfile.AccessibilityScale)));
            if (control is BaseButton button &&
                button.GetThemeStylebox("focus") is StyleBoxFlat sourceFocus)
            {
                var focus = (StyleBoxFlat)sourceFocus.Duplicate();
                int border = Math.Max(
                    3,
                    Mathf.RoundToInt(3f * _layoutProfile.AccessibilityScale));
                float expand = Math.Max(2f, 2f * _layoutProfile.AccessibilityScale);
                focus.BorderWidthLeft = border;
                focus.BorderWidthTop = border;
                focus.BorderWidthRight = border;
                focus.BorderWidthBottom = border;
                focus.ExpandMarginLeft = expand;
                focus.ExpandMarginTop = expand;
                focus.ExpandMarginRight = expand;
                focus.ExpandMarginBottom = expand;
                button.AddThemeStyleboxOverride("focus", focus);
            }
        }
        foreach (Node child in node.GetChildren())
        {
            ApplyTypographyRecursive(child);
        }
    }

    private static void ApplyRect(Control control, Rect2 rect)
    {
        // Godot may retain a previously larger manually-positioned Control size
        // when a density/UI-scale transition reduces the surface budget. Reset
        // managed descendant containers bottom-up, then the surface itself,
        // before applying the sole responsive rectangle. A surface-only reset
        // is insufficient because an old VBox/Grid child rect can push its
        // parent back to the previous scale on the next layout notification.
        if (control.Size.X > rect.Size.X + 0.5f ||
            control.Size.Y > rect.Size.Y + 0.5f)
        {
            ResetContainerSizes(control);
        }
        control.ResetSize();
        control.Position = rect.Position;
        control.Size = rect.Size;
        if (control is Container container)
        {
            // Native window-mode and UI-scale changes can leave a VBox child
            // with its former expanded allocation even after the surface has
            // a smaller valid minimum. Queue the container sort after writing
            // the authoritative rect so children are redistributed inside the
            // new budget instead of pushing the panel back to its old height.
            container.QueueSort();
        }
    }

    private static void ResetContainerSizes(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            ResetContainerSizes(child);
        }
        if (node is Container container)
        {
            container.ResetSize();
            container.QueueSort();
        }
    }
}
