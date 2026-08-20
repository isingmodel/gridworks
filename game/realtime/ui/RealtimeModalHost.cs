using System;
using Godot;

namespace Gridworks.Game.Realtime.UI;

internal sealed partial class RealtimeModalHost : Control
{
    private PanelContainer _panel = null!;
    private RealtimeFocusScope _focusScope = null!;
    private Label _eyebrow = null!;
    private Label _heading = null!;
    private Label _body = null!;
    private Label _pauseStatus = null!;
    private Button _secondary = null!;
    private Button _primary = null!;
    private RealtimeModalPresentation? _active;
    private Control? _backgroundReturnFocus;

    public event Action<string, string>? ActionRequested;
    public event Action<string>? DismissRequested;
    public event Action<bool>? SimulationPauseChanged;
    public event Action<RealtimePausePresentation>? PauseChanged;
    public event Action<int>? DepthChanged;
    public event Action<string>? NestedModalRejected;

    public int Depth => _active is null ? 0 : 1;

    public RealtimeModalPresentation? ActiveModal => _active;

    public RealtimePausePresentation ActivePause =>
        _active?.Pause ?? RealtimePausePresentation.None;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _panel = GetNode<PanelContainer>("%ModalPanel");
        _focusScope = GetNode<RealtimeFocusScope>("%FocusScope");
        _eyebrow = GetNode<Label>("%EyebrowLabel");
        _heading = GetNode<Label>("%HeadingLabel");
        _body = GetNode<Label>("%BodyLabel");
        _pauseStatus = GetNode<Label>("%PauseStatusLabel");
        _secondary = GetNode<Button>("%SecondaryButton");
        _primary = GetNode<Button>("%PrimaryButton");
        _secondary.Pressed += OnSecondaryPressed;
        _primary.Pressed += OnPrimaryPressed;
        Visible = false;
    }

    public bool PushModal(RealtimeModalPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (string.IsNullOrWhiteSpace(presentation.Id))
        {
            throw new ArgumentException("Modal id cannot be empty.", nameof(presentation));
        }
        if (_active is not null)
        {
            NestedModalRejected?.Invoke(presentation.Id);
            return false;
        }
        if (!presentation.PausesSimulation ||
            presentation.Pause.Reason == RealtimePauseReason.None ||
            string.IsNullOrWhiteSpace(presentation.Pause.CurrentTimeLabel) ||
            string.IsNullOrWhiteSpace(presentation.Pause.NextEventLabel))
        {
            throw new ArgumentException(
                "A blocking modal requires a typed pause reason, current time, and next event.",
                nameof(presentation));
        }
        _backgroundReturnFocus = GetViewport().GuiGetFocusOwner();
        _active = presentation;
        RenderActive();
        SimulationPauseChanged?.Invoke(true);
        PauseChanged?.Invoke(presentation.Pause);
        DepthChanged?.Invoke(1);
        return true;
    }

    public bool PopModal(bool dismissed = false)
    {
        if (_active is null)
        {
            return false;
        }
        RealtimeModalPresentation removed = _active;
        _active = null;
        if (dismissed)
        {
            DismissRequested?.Invoke(removed.Id);
        }
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _focusScope.Deactivate(restoreFocus: false);
        Control? returnFocus = IsValidFocus(_backgroundReturnFocus)
            ? _backgroundReturnFocus
            : null;
        _backgroundReturnFocus = null;
        returnFocus?.CallDeferred(Control.MethodName.GrabFocus);
        SimulationPauseChanged?.Invoke(false);
        PauseChanged?.Invoke(RealtimePausePresentation.None);
        DepthChanged?.Invoke(0);
        return true;
    }

    public bool HandleCancel()
    {
        if (ActiveModal is not { DismissOnCancel: true })
        {
            return false;
        }
        return PopModal(dismissed: true);
    }

    public void Clear(bool restoreFocus = true)
    {
        bool wasActive = _active is not null;
        _active = null;
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _focusScope.Deactivate(restoreFocus: false);
        Control? returnFocus = restoreFocus && IsValidFocus(_backgroundReturnFocus)
            ? _backgroundReturnFocus
            : null;
        _backgroundReturnFocus = null;
        returnFocus?.CallDeferred(Control.MethodName.GrabFocus);
        if (wasActive)
        {
            SimulationPauseChanged?.Invoke(false);
            PauseChanged?.Invoke(RealtimePausePresentation.None);
        }
        DepthChanged?.Invoke(0);
    }

    public void ApplyLayout(RealtimeLayoutProfile profile)
    {
        float width = Math.Clamp(720f * profile.AccessibilityScale, 680f, 920f);
        float height = Math.Clamp(460f * profile.AccessibilityScale, 440f, 680f);
        _panel.CustomMinimumSize = new Vector2(width, height);
        _secondary.CustomMinimumSize = new Vector2(
            Math.Max(150, profile.MinimumHitTarget * 3),
            profile.PrimaryHitTarget);
        _primary.CustomMinimumSize = new Vector2(
            Math.Max(190, profile.MinimumHitTarget * 4),
            profile.PrimaryHitTarget);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (Visible && inputEvent.IsActionPressed("ui_cancel") && HandleCancel())
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private void RenderActive()
    {
        RealtimeModalPresentation presentation = ActiveModal!;
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;
        _eyebrow.Text = presentation.Eyebrow;
        _heading.Text = presentation.Heading;
        _body.Text = presentation.Body;
        _pauseStatus.Text =
            $"Ⅱ {PauseReasonAccessibility(presentation.Pause.Reason)} · " +
            $"현재 {presentation.Pause.CurrentTimeLabel} · " +
            $"다음 {presentation.Pause.NextEventLabel}";
        _pauseStatus.AccessibilityName = _pauseStatus.Text;
        SetButton(_primary, presentation.PrimaryAction);
        SetButton(_secondary, presentation.SecondaryAction);
        _secondary.Visible = presentation.SecondaryAction?.Visible == true;
        AccessibilityName =
            $"{ModalKindAccessibility(presentation.Kind)}. {presentation.Heading}. " +
            $"{presentation.Body}. 멈춘 이유 {PauseReasonAccessibility(presentation.Pause.Reason)}. " +
            $"현재 시각 {presentation.Pause.CurrentTimeLabel}. " +
            $"재개 후 다음 사건 {presentation.Pause.NextEventLabel}.";

        Control preferred = presentation.PrimaryAction.Tone == RealtimeActionTone.Destructive &&
                            _secondary.Visible && !_secondary.Disabled
            ? _secondary
            : !_primary.Disabled
                ? _primary
                : _secondary;
        _focusScope.Activate(preferred, _backgroundReturnFocus);
    }

    private void OnPrimaryPressed()
    {
        if (ActiveModal is RealtimeModalPresentation modal && !modal.PrimaryAction.Enabled)
        {
            return;
        }
        if (ActiveModal is RealtimeModalPresentation active)
        {
            ActionRequested?.Invoke(active.Id, active.PrimaryAction.Id);
        }
    }

    private void OnSecondaryPressed()
    {
        if (ActiveModal is not RealtimeModalPresentation active ||
            active.SecondaryAction is not RealtimeActionPresentation secondary ||
            !secondary.Enabled)
        {
            return;
        }
        ActionRequested?.Invoke(active.Id, secondary.Id);
    }

    private static void SetButton(Button button, RealtimeActionPresentation? presentation)
    {
        if (presentation is null)
        {
            button.Visible = false;
            return;
        }
        button.Visible = presentation.Visible;
        button.Text = presentation.Label;
        button.Disabled = !presentation.Enabled;
        button.TooltipText = presentation.Description;
        button.AccessibilityName = presentation.Label;
        button.AccessibilityDescription = presentation.Description;
        button.ThemeTypeVariation = presentation.Tone switch
        {
            RealtimeActionTone.Primary => "PrimaryButton",
            RealtimeActionTone.Secondary => "Button",
            RealtimeActionTone.Destructive => "DestructiveButton",
            _ => throw new ArgumentOutOfRangeException(nameof(presentation)),
        };
    }

    private static bool IsValidFocus(Control? control)
    {
        if (control is null)
        {
            return false;
        }
        try
        {
            // Godot's managed wrapper can outlive its native Control between a
            // responsive rebuild and modal close. Never dereference a stale
            // focus return target; focus restoration is optional, modal state
            // teardown is not.
            return GodotObject.IsInstanceValid(control) &&
                   !control.IsQueuedForDeletion() &&
                   control.IsInsideTree() &&
                   control.IsVisibleInTree() &&
                   control.FocusMode != FocusModeEnum.None &&
                   (control is not BaseButton button || !button.Disabled);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static string ModalKindAccessibility(RealtimeModalKind kind) => kind switch
    {
        RealtimeModalKind.ChapterStory => "장 시작 또는 결과 이야기 화면",
        RealtimeModalKind.NewGameConfirmation => "새 게임 확인 대화상자",
        RealtimeModalKind.RecoveryConfirmation => "복구 확인 대화상자",
        RealtimeModalKind.FatalError => "치명적 오류 화면",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string PauseReasonAccessibility(RealtimePauseReason reason) => reason switch
    {
        RealtimePauseReason.PlayerRequest => "플레이어 요청",
        RealtimePauseReason.ChapterBriefing => "장 시작 안내",
        RealtimePauseReason.CriticalIncident => "처음 발생한 중대 사건",
        RealtimePauseReason.RecoveryConfirmation => "되돌릴 수 없는 복구 확인",
        RealtimePauseReason.CampaignResult => "장 또는 캠페인 결과",
        RealtimePauseReason.CatchUpCeiling => "성능 안전 정지",
        RealtimePauseReason.FatalError => "치명적 오류",
        RealtimePauseReason.None => "없음",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };
}
