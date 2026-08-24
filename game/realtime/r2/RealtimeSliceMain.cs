using System;
using System.Collections.Generic;
using System.Reflection;
using Gridworks.Core.Release.V2;
using Gridworks.Game.Realtime.UI;
using Godot;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

/// <summary>
/// Godot scene adapter for the R2 realtime session. This class owns route/resource bootstrap,
/// node lifecycle, input routing, focus, canvas, camera, and UI publication only.
/// </summary>
internal sealed partial class RealtimeSliceMain : Control
{
    private static readonly Vector2I RequiredLogicalCanvas = new(1920, 1080);

    private readonly Dictionary<RealtimePointerOwner, int> _clickCounters = [];
    private RealtimeSession? _session;
    private IRealtimeWorldView? _worldView;
    private RealtimeUiRoot? _ui;
    private string? _presentedModalId;
    private Vector2I _priorContentScaleSize;
    private Window.ContentScaleModeEnum _priorContentScaleMode;
    private Window.ContentScaleAspectEnum _priorContentScaleAspect;
    private bool _logicalCanvasApplied;
    private RealtimeNativeRoute? _nativeRoute;

    private RealtimeSession Session => RequireSession();

    private RealtimeSession RequireSession() => _session ??
        throw new InvalidOperationException("Realtime R2 slice is not bootstrapped.");

    public override void _Ready()
    {
        _nativeRoute = ParseSourceRoute(OS.GetCmdlineUserArgs());
        Control worldNode = GetNode<Control>("%WorldView");
        _worldView = worldNode as IRealtimeWorldView ??
            throw new InvalidOperationException(
                "The WorldView scene node must implement IRealtimeWorldView.");
        _ui = GetNode<RealtimeUiRoot>("%UiRoot");
        WireNodes();
        ApplyLogicalCanvas();
        Bootstrap();
        SetProcess(true);
    }

    public override void _ExitTree()
    {
        DetachSession();
        if (_logicalCanvasApplied && GetWindow() is Window window)
        {
            window.ContentScaleSize = _priorContentScaleSize;
            window.ContentScaleMode = _priorContentScaleMode;
            window.ContentScaleAspect = _priorContentScaleAspect;
        }
        _logicalCanvasApplied = false;
    }

    public override void _Process(double delta)
    {
        if (_session is null || delta <= 0)
        {
            return;
        }
        _ = InjectElapsedSeconds(delta);
#if DEBUG
        StopInteractiveTargetAtBoundaryForDebug();
#endif
    }

    private void Bootstrap()
    {
        Assembly assembly = typeof(RealtimeSliceMain).Assembly;
        RealtimeSliceData data = _nativeRoute is null
            ? RealtimeSliceResources.LoadTechnicalFixture(assembly)
            : RealtimeSliceResources.LoadNativeRelease(assembly, _nativeRoute);
        if (!ReferenceEquals(data.NativeRoute, _nativeRoute))
        {
            throw new InvalidOperationException(
                "Realtime slice resource route does not match its launch route.");
        }

        DetachSession();
        _session = new RealtimeSession(data);
        Session.PresentationPublished += PublishPresentation;
        Session.PointerPresentationPublished += PublishPointerPresentation;
        Session.EvidenceRecorded += RecordEvidence;

        _clickCounters.Clear();
        foreach (RealtimePointerOwner owner in Enum.GetValues<RealtimePointerOwner>())
        {
            _clickCounters.Add(owner, 0);
        }
        _presentedModalId = null;
#if DEBUG
        if (data.NativeRoute is null)
        {
            _smokeLinePlan = BuildSmokeLinePlan(data);
            _smokeBoundaryFacts = BuildSmokeBoundaryFacts(
                Session.CoreSnapshot,
                _smokeLinePlan);
        }
        else
        {
            _smokeLinePlan = null;
            _smokeBoundaryFacts = null;
        }
        _lastInputRequest = null;
        _suppressFormativeDirectPlayOutputForSmoke = false;
#endif
        PublishPresentation(Session.LatestPresentation);
    }

    private void DetachSession()
    {
        if (_session is null)
        {
            return;
        }
        _session.PresentationPublished -= PublishPresentation;
        _session.PointerPresentationPublished -= PublishPointerPresentation;
        _session.EvidenceRecorded -= RecordEvidence;
    }

    private void WireNodes()
    {
        _worldView!.PrimaryRequested += HandleMapPrimary;
        _worldView.PointerMoved += HandleMapPointerMoved;
        _worldView.CancelRequested += HandleUndoDraftStep;
        _ui!.SpeedRequested += HandleSpeedRequested;
        _ui.MenuRequested += () => HandleShortcut(RealtimeInputCommand.ToggleBuildShelf);
        _ui.TimelineItemsRequested += HandleTimelineItems;
        _ui.TimelineHorizonDeltaRequested += HandleTimelineHorizonDelta;
        _ui.TimelineNavigationRequested += HandleTimelineNavigation;
        _ui.TimelineExpansionRequested += expanded => _ = ApplyIntent(
            new RealtimeR2Intent(
                expanded
                    ? RealtimeR2IntentKind.OpenSurface
                    : RealtimeR2IntentKind.CloseSurface,
                Surface: RealtimeSurface.Timeline));
        _ui.ContextCloseRequested += () => _ = ApplyIntent(new RealtimeR2Intent(
            RealtimeR2IntentKind.CloseSurface,
            Surface: RealtimeSurface.Inspector));
        _ui.ActionRequested += HandleAction;
        _ui.BuildToolRequested += HandleBuildTool;
        _ui.ModalActionRequested += HandleModalAction;
        _ui.ModalDismissRequested += HandleModalDismiss;
        _ui.InputRequested += HandleInputRequest;
        _ui.MapInteractionRectChanged += ApplyMapInteractionRect;
    }

    private void PublishPresentation(RealtimeSlicePresentation presentation)
    {
        if (_worldView is null || _ui is null || !IsInsideTree())
        {
            return;
        }
        _worldView.SetPresentation(presentation.World);
        _worldView.SetPointerFeedback(presentation.Pointer);
        _ui.SetTopHud(presentation.Hud);
        _ui.SetEventRail(presentation.Rail);
        _ui.SetContextDock(presentation.Context);
        _ui.SetBuildShelf(presentation.BuildShelf);
        _ui.SetActionDock(presentation.ActionDock);
        PresentModal(presentation.Modal);
    }

    private void PublishPointerPresentation(RealtimeSlicePresentation presentation)
    {
        if (_worldView is null || _ui is null || !IsInsideTree())
        {
            return;
        }
        _worldView.SetPointerFeedback(presentation.Pointer);
        _ui.SetBuildShelf(presentation.BuildShelf);
        _ui.SetActionDock(presentation.ActionDock);
    }

    private void RecordEvidence(string evidence)
    {
#if DEBUG
        if (_suppressFormativeDirectPlayOutputForSmoke)
        {
            return;
        }
#endif
        GD.Print(evidence);
    }

    private void PresentModal(RealtimeModalPresentation? modal)
    {
        if (_worldView is null || _ui is null || !IsInsideTree())
        {
            return;
        }
        if (modal is null)
        {
            if (_presentedModalId is not null)
            {
                _ui.PopModal();
                _presentedModalId = null;
            }
            return;
        }
        if (string.Equals(_presentedModalId, modal.Id, StringComparison.Ordinal))
        {
            return;
        }
        if (_presentedModalId is not null)
        {
            _ui.PopModal();
        }
        _ui.InputRouter.CancelPanCapture();
        _worldView.EndPan();
        Control? opener = GetViewport().GuiGetFocusOwner();
        if (!IsValidReturnFocus(opener))
        {
            _worldView.RequestFocus();
        }
        if (!_ui.PushModal(modal))
        {
            throw new InvalidOperationException("R2 modal host rejected the single modal.");
        }
        _presentedModalId = modal.Id;
    }

    private static bool IsValidReturnFocus(Control? control) => control is not null &&
        control.IsInsideTree() &&
        control.IsVisibleInTree() &&
        control.FocusMode != Control.FocusModeEnum.None &&
        (control is not BaseButton button || !button.Disabled);

    private void HandleMapPrimary(
        RealtimePointerResolution resolution,
        CoreMapPoint worldPoint)
    {
        EnsureBootstrapped();
        _clickCounters[resolution.Owner]++;
        Session.HandleMapPrimary(resolution, worldPoint);
    }

    private void HandleMapPointerMoved(
        RealtimePointerResolution resolution,
        CoreMapPoint worldPoint)
    {
        EnsureBootstrapped();
        Session.HandleMapPointerMoved(
            resolution,
            worldPoint,
            _worldView?.IsPanning == true);
    }

    private void HandleAction(string id) => Session.HandleAction(id);

    private void HandleModalAction(string modalId, string actionId) =>
        Session.HandleModalAction(modalId, actionId);

    private void HandleModalDismiss(string modalId) =>
        Session.HandleModalDismiss(modalId);

    private void HandleBuildTool(string id) => Session.HandleBuildTool(id);

    private void HandleSpeedRequested(RealtimeSimulationSpeed speed) =>
        Session.HandleSpeedRequested(speed);

    private void HandleTimelineHorizonDelta(int delta) =>
        Session.HandleTimelineHorizonDelta(delta);

    private void HandleTimelineItems(IReadOnlyList<string> ids) =>
        Session.HandleTimelineItems(ids);

    private void HandleTimelineNavigation(RealtimeTimelineNavigation navigation) =>
        Session.HandleTimelineNavigation(navigation);

    private void HandleUndoDraftStep() => Session.HandleUndoDraftStep();

    private RealtimeR2IntentResult ApplyIntent(RealtimeR2Intent intent) =>
        Session.ApplyIntent(intent);

    private RealtimeR2FrameResult InjectElapsedNanoseconds(long elapsedNanoseconds) =>
        Session.InjectElapsedNanoseconds(
            elapsedNanoseconds,
            MaximumInteractiveVirtualFrames());

    private RealtimeR2FrameResult InjectElapsedSeconds(double elapsedSeconds) =>
        Session.InjectElapsedSeconds(
            elapsedSeconds,
            MaximumInteractiveVirtualFrames());

    private RealtimeR2FrameResult InjectExactFrames(
        long frameCount,
        int framesPerSecond) => Session.InjectExactFrames(frameCount, framesPerSecond);

    private long? MaximumInteractiveVirtualFrames()
    {
#if DEBUG
        return _interactiveCheckpoint is null
            ? null
            : ClampInteractiveVirtualFramesAtBoundaryForDebug(long.MaxValue);
#else
        return null;
#endif
    }

    private void ApplyMapInteractionRect(Rect2 rect)
    {
        if (_worldView is null || _ui is null || rect.Size.X <= 0 || rect.Size.Y <= 0)
        {
            return;
        }
        _worldView.SetInteractionRect(rect, _ui.LayoutProfile);
    }

    private void HandleInputRequest(RealtimeInputRequest request)
    {
#if DEBUG
        _lastInputRequest = request;
#endif
        HandleShortcut(request.Command);
    }

    private void HandleShortcut(RealtimeInputCommand command)
    {
        if (command is not RealtimeInputCommand.CancelOrBack and
            not RealtimeInputCommand.ToggleBuildShelf)
        {
            Session.DisarmDraftCancellation();
        }
        switch (command)
        {
            case RealtimeInputCommand.TogglePause:
                Session.HandleTogglePause();
                break;
            case RealtimeInputCommand.SetNormalSpeed:
                _ = ApplyIntent(RealtimeR2Intent.SetSpeed(RealtimeSimulationSpeed.Normal));
                break;
            case RealtimeInputCommand.SetFastSpeed:
                _ = ApplyIntent(RealtimeR2Intent.SetSpeed(RealtimeSimulationSpeed.Fast));
                break;
            case RealtimeInputCommand.SetVeryFastSpeed:
                _ = ApplyIntent(RealtimeR2Intent.SetSpeed(RealtimeSimulationSpeed.VeryFast));
                break;
            case RealtimeInputCommand.ToggleAnalysis:
                _ = ApplyIntent(new RealtimeR2Intent(RealtimeR2IntentKind.ToggleAnalysis));
                break;
            case RealtimeInputCommand.ToggleBuildShelf:
                Session.HandleToggleBuildShelf();
                break;
            case RealtimeInputCommand.CancelOrBack:
                Session.HandleCancel();
                break;
            case RealtimeInputCommand.UndoDraftStep:
                Session.HandleUndoDraftStep();
                break;
            case RealtimeInputCommand.CycleCandidatePrevious:
                _worldView?.CycleCandidate(-1);
                break;
            case RealtimeInputCommand.CycleCandidateNext:
                _worldView?.CycleCandidate(1);
                break;
            case RealtimeInputCommand.BeginPan:
                _worldView?.BeginPan();
                break;
            case RealtimeInputCommand.EndPan:
                _worldView?.EndPan();
                break;
            case RealtimeInputCommand.ConfirmOrSelect:
                _worldView?.ConfirmCurrentCandidate();
                break;
            case RealtimeInputCommand.TimelineHome:
                RouteTimelineNavigation(RealtimeTimelineNavigation.Home);
                break;
            case RealtimeInputCommand.TimelinePrevious:
                RouteTimelineNavigation(RealtimeTimelineNavigation.PreviousEvent);
                break;
            case RealtimeInputCommand.TimelineNext:
                RouteTimelineNavigation(RealtimeTimelineNavigation.NextEvent);
                break;
            case RealtimeInputCommand.SelectInspectTool:
                Session.HandleBuildTool("TOOL:INSPECT");
                break;
            case RealtimeInputCommand.SelectFirstNodeTool:
                Session.SelectBuildToolByPrefix("NODE:");
                break;
            case RealtimeInputCommand.SelectFirstLineTool:
                Session.SelectBuildToolByPrefix("LINE:");
                break;
        }
    }

    private void RouteTimelineNavigation(RealtimeTimelineNavigation navigation)
    {
        if (_ui?.NavigateTimeline(navigation) != true)
        {
            Session.HandleTimelineNavigation(navigation);
        }
    }

    private void ApplyLogicalCanvas()
    {
        Window window = GetWindow();
        _priorContentScaleSize = window.ContentScaleSize;
        _priorContentScaleMode = window.ContentScaleMode;
        _priorContentScaleAspect = window.ContentScaleAspect;
        window.ContentScaleSize = RequiredLogicalCanvas;
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
        _logicalCanvasApplied = true;
    }

    internal static RealtimeNativeRoute? ParseSourceRoute(string[] arguments) =>
        RealtimeNativeRouteCatalog.Parse(arguments);

    private void EnsureBootstrapped()
    {
        _ = Session;
    }

}
