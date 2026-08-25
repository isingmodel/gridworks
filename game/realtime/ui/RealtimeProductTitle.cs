using System;
using Godot;

namespace Gridworks.Game.Realtime.UI;

internal sealed record RealtimeProductTitlePresentation(
    string Status,
    string ContinueUnavailableReason);

/// <summary>
/// Current-R2 product entry surface. Campaign bootstrap and persistence remain
/// outside this view; it only presents title state and emits the new-game intent.
/// </summary>
internal sealed partial class RealtimeProductTitle : Control
{
    private PanelContainer _panel = null!;
    private RealtimeFocusScope _focusScope = null!;
    private Label _status = null!;
    private Label _continueReason = null!;
    private Button _newGame = null!;
    private Button _continue = null!;

    public event Action? NewGameRequested;

    internal Button NewGameButton => _newGame;

    internal Button ContinueButton => _continue;

    internal string ContinueReasonText => _continueReason.Text;

    internal string StatusText => _status.Text;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _panel = GetNode<PanelContainer>("%TitlePanel");
        _focusScope = GetNode<RealtimeFocusScope>("%FocusScope");
        _status = GetNode<Label>("%StatusLabel");
        _continueReason = GetNode<Label>("%ContinueReasonLabel");
        _newGame = GetNode<Button>("%NewGameButton");
        _continue = GetNode<Button>("%ContinueButton");

        _newGame.Pressed += () => NewGameRequested?.Invoke();
        AccessibilityName = "Gridworks 제품 시작 화면";
        Dismiss();
    }

    public void Present(RealtimeProductTitlePresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (string.IsNullOrWhiteSpace(presentation.ContinueUnavailableReason))
        {
            throw new ArgumentException(
                "A disabled Continue action requires a visible reason.",
                nameof(presentation));
        }

        _status.Text = presentation.Status;
        _status.AccessibilityName = string.IsNullOrWhiteSpace(presentation.Status)
            ? "시작 상태 알림 없음"
            : presentation.Status;
        _status.AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Polite;

        _continue.Disabled = true;
        _continue.AccessibilityDescription = presentation.ContinueUnavailableReason;
        _continueReason.Text = presentation.ContinueUnavailableReason;
        _continueReason.AccessibilityName = presentation.ContinueUnavailableReason;
        _newGame.AccessibilityDescription =
            "첫 임무 안내를 열고 새 게임을 시작합니다.";

        AccessibilityDescription =
            $"{presentation.Status} {presentation.ContinueUnavailableReason}".Trim();
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;
        _focusScope.Activate(_newGame);
    }

    public void Dismiss()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        if (_focusScope is not null)
        {
            _focusScope.Deactivate(restoreFocus: false);
        }
    }

    public void ApplyLayout(RealtimeLayoutProfile profile)
    {
        float width = Math.Clamp(720f * profile.AccessibilityScale, 680f, 960f);
        float height = Math.Clamp(520f * profile.AccessibilityScale, 500f, 760f);
        _panel.CustomMinimumSize = new Vector2(width, height);
        _newGame.CustomMinimumSize = new Vector2(0, profile.PrimaryHitTarget);
        _continue.CustomMinimumSize = new Vector2(0, profile.PrimaryHitTarget);
        _status.CustomMinimumSize = new Vector2(0, profile.MinimumHitTarget);
        _continueReason.CustomMinimumSize = new Vector2(0, profile.MinimumHitTarget);
    }
}
