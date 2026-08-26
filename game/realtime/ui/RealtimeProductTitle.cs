using System;
using Godot;

namespace Gridworks.Game.Realtime.UI;

internal enum RealtimeProductNewGameAction
{
    Unavailable,
    Immediate,
    Reset,
    ConfirmReset,
}

internal sealed record RealtimeProductTitlePresentation(
    string Status,
    string Detail,
    bool CanContinue,
    RealtimeProductNewGameAction NewGameAction)
{
    internal bool CanStartNewGame =>
        NewGameAction != RealtimeProductNewGameAction.Unavailable;
}

/// <summary>
/// Current-R2 product entry surface. Campaign bootstrap and persistence remain
/// outside this view; it only presents title state and emits start intents.
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
    public event Action? ContinueRequested;

    internal Button NewGameButton => _newGame;

    internal Button ContinueButton => _continue;

    internal string DetailText => _continueReason.Text;

    internal string ContinueReasonText => DetailText;

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
        _continue.Pressed += () => ContinueRequested?.Invoke();
        AccessibilityName = "Gridworks 제품 시작 화면";
        Dismiss();
    }

    public void Present(RealtimeProductTitlePresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (string.IsNullOrWhiteSpace(presentation.Detail))
        {
            throw new ArgumentException(
                "The product title requires visible detail.",
                nameof(presentation));
        }

        _status.Text = presentation.Status;
        _status.AccessibilityName = string.IsNullOrWhiteSpace(presentation.Status)
            ? "시작 상태 알림 없음"
            : presentation.Status;
        _status.AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Polite;

        _continue.Disabled = !presentation.CanContinue;
        _newGame.Disabled = !presentation.CanStartNewGame;
        _newGame.Text = presentation.NewGameAction ==
            RealtimeProductNewGameAction.ConfirmReset
                ? "새 게임 시작 확인"
                : "새 게임";
        _continue.AccessibilityDescription = presentation.CanContinue
            ? "저장된 진행을 이어갑니다."
            : presentation.Detail;
        _newGame.AccessibilityDescription = presentation.NewGameAction switch
        {
            RealtimeProductNewGameAction.Immediate =>
                "첫 임무 안내를 열고 새 게임을 시작합니다.",
            RealtimeProductNewGameAction.Reset =>
                "저장 원본을 백업하기 위한 새 게임 확인 단계를 엽니다.",
            RealtimeProductNewGameAction.ConfirmReset =>
                "저장 원본을 백업하고 새 게임 시작을 확인합니다.",
            _ => presentation.Detail,
        };
        _continueReason.Text = presentation.Detail;
        _continueReason.AccessibilityName = presentation.Detail;

        AccessibilityDescription =
            $"{presentation.Status} {presentation.Detail}".Trim();
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;
        Control? preferredFocus = presentation.NewGameAction ==
            RealtimeProductNewGameAction.ConfirmReset
                ? _newGame
                : presentation.CanContinue
                    ? _continue
                    : presentation.CanStartNewGame
                        ? _newGame
                        : null;
        _focusScope.Activate(preferredFocus);
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
