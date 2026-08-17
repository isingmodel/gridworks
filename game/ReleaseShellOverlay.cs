using System;
using Godot;

namespace Gridworks.Game;

internal enum ReleaseShellPage
{
    Hidden,
    Title,
    Pause,
    Settings,
    Help,
    Confirm,
}

internal enum ReleaseShellAction
{
    NewGame,
    Continue,
    Quit,
    Resume,
    SaveAndQuit,
    RestartChapter,
    TitleSettings,
    PauseSettings,
    TitleHelp,
    PauseHelp,
    SettingsBack,
    HelpBack,
    Confirm,
    CancelConfirm,
}

internal enum ReleaseShellConfirmation
{
    None,
    NewGame,
    RestartChapter,
}

internal sealed partial class ReleaseShellOverlay : Control
{
    private Control _titlePage = null!;
    private Control _pausePage = null!;
    private Control _settingsPage = null!;
    private Control _helpPage = null!;
    private Control _confirmPage = null!;
    private Label _titleMessage = null!;
    private Label _pauseChapter = null!;
    private Label _pauseMessage = null!;
    private Label _confirmHeading = null!;
    private Label _confirmBody = null!;
    private Button _newGame = null!;
    private Button _continue = null!;
    private Button _quit = null!;
    private Button _resume = null!;
    private Button _confirm = null!;
    private Button _cancelConfirm = null!;
    private OptionButton _windowMode = null!;
    private OptionButton _uiScale = null!;
    private OptionButton _masterVolume = null!;
    private OptionButton _ambientVolume = null!;
    private OptionButton _sfxVolume = null!;
    private CheckButton _controlHelp = null!;
    private ReleaseShellPage _page = ReleaseShellPage.Hidden;
    private ReleaseShellPage _returnPage = ReleaseShellPage.Title;
    private ReleaseShellConfirmation _confirmation;
    private bool _hasSave;
    private bool _settingControls;

    public event Action? PauseRequested;
    public event Action? NewGameRequested;
    public event Action? ContinueRequested;
    public event Action? ResumeRequested;
    public event Action? SaveAndQuitRequested;
    public event Action? RestartChapterRequested;
    public event Action<bool>? FullscreenChanged;
    public event Action<int>? UiScalePercentChanged;
    public event Action<int>? MasterVolumePercentChanged;
    public event Action<int>? AmbientVolumePercentChanged;
    public event Action<int>? SfxVolumePercentChanged;
    public event Action<bool>? ControlHelpChanged;
    public event Action? GameplayFocusRequested;

    public ReleaseShellPage Page => _page;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _titlePage = GetNode<Control>("%TitlePage");
        _pausePage = GetNode<Control>("%PausePage");
        _settingsPage = GetNode<Control>("%SettingsPage");
        _helpPage = GetNode<Control>("%HelpPage");
        _confirmPage = GetNode<Control>("%ConfirmPage");
        _titleMessage = GetNode<Label>("%TitleMessage");
        _pauseChapter = GetNode<Label>("%PauseChapter");
        _pauseMessage = GetNode<Label>("%PauseMessage");
        _confirmHeading = GetNode<Label>("%ConfirmHeading");
        _confirmBody = GetNode<Label>("%ConfirmBody");
        _newGame = GetNode<Button>("%NewGameButton");
        _continue = GetNode<Button>("%ContinueButton");
        _quit = GetNode<Button>("%TitleQuitButton");
        _resume = GetNode<Button>("%ResumeButton");
        _confirm = GetNode<Button>("%ConfirmButton");
        _cancelConfirm = GetNode<Button>("%CancelConfirmButton");
        _windowMode = GetNode<OptionButton>("%WindowModeOption");
        _uiScale = GetNode<OptionButton>("%UiScaleOption");
        _masterVolume = GetNode<OptionButton>("%MasterVolumeOption");
        _ambientVolume = GetNode<OptionButton>("%AmbientVolumeOption");
        _sfxVolume = GetNode<OptionButton>("%SfxVolumeOption");
        _controlHelp = GetNode<CheckButton>("%ControlHelpCheck");

        _windowMode.AddItem("창 모드", 0);
        _windowMode.AddItem("전체화면", 1);
        _uiScale.AddItem("100%", 100);
        _uiScale.AddItem("125%", 125);
        AddVolumeItems(_masterVolume);
        AddVolumeItems(_ambientVolume);
        AddVolumeItems(_sfxVolume);

        _newGame.Pressed += OnNewGamePressed;
        _continue.Pressed += () => ContinueRequested?.Invoke();
        _quit.Pressed += () => GetTree().Quit(0);
        _quit.AccessibilityDescription = "게임을 종료하고 바탕 화면으로 돌아갑니다.";
        _resume.Pressed += () => ResumeRequested?.Invoke();
        GetNode<Button>("%SaveAndQuitButton").Pressed += () => SaveAndQuitRequested?.Invoke();
        GetNode<Button>("%RestartChapterButton").Pressed += OnRestartChapterPressed;
        GetNode<Button>("%TitleSettingsButton").Pressed += () => ShowSettings(ReleaseShellPage.Title);
        GetNode<Button>("%PauseSettingsButton").Pressed += () => ShowSettings(ReleaseShellPage.Pause);
        GetNode<Button>("%TitleHelpButton").Pressed += () => ShowHelp(ReleaseShellPage.Title);
        GetNode<Button>("%PauseHelpButton").Pressed += () => ShowHelp(ReleaseShellPage.Pause);
        GetNode<Button>("%SettingsBackButton").Pressed += ReturnFromSubpage;
        GetNode<Button>("%HelpBackButton").Pressed += ReturnFromSubpage;
        _confirm.Pressed += OnConfirmPressed;
        _cancelConfirm.Pressed += ReturnFromSubpage;

        _windowMode.ItemSelected += index =>
        {
            if (!_settingControls)
            {
                FullscreenChanged?.Invoke(_windowMode.GetItemId((int)index) == 1);
            }
        };
        _uiScale.ItemSelected += index =>
        {
            if (!_settingControls)
            {
                UiScalePercentChanged?.Invoke(_uiScale.GetItemId((int)index));
            }
        };
        _masterVolume.ItemSelected += index => EmitVolume(_masterVolume, index, MasterVolumePercentChanged);
        _ambientVolume.ItemSelected += index => EmitVolume(_ambientVolume, index, AmbientVolumePercentChanged);
        _sfxVolume.ItemSelected += index => EmitVolume(_sfxVolume, index, SfxVolumePercentChanged);
        _controlHelp.Toggled += enabled =>
        {
            if (!_settingControls)
            {
                ControlHelpChanged?.Invoke(enabled);
            }
        };

        AccessibilityName = "Gridworks 제목 화면 및 일시정지 메뉴";
        SetPage(ReleaseShellPage.Hidden);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey { Pressed: true, Echo: false } key || key.Keycode != Key.Escape)
        {
            return;
        }

        switch (_page)
        {
            case ReleaseShellPage.Hidden:
                PauseRequested?.Invoke();
                break;
            case ReleaseShellPage.Pause:
                ResumeRequested?.Invoke();
                break;
            case ReleaseShellPage.Settings:
            case ReleaseShellPage.Help:
            case ReleaseShellPage.Confirm:
                ReturnFromSubpage();
                break;
            case ReleaseShellPage.Title:
                return;
            default:
                throw new ArgumentOutOfRangeException();
        }
        GetViewport().SetInputAsHandled();
    }

    public void ShowTitle(bool hasSave, string message)
    {
        _hasSave = hasSave;
        _continue.Disabled = !hasSave;
        _continue.AccessibilityDescription = hasSave
            ? "마지막으로 저장한 게임을 이어서 시작합니다."
            : "이어할 저장 파일이 없습니다.";
        _titleMessage.Text = message;
        _titleMessage.AccessibilityName = string.IsNullOrWhiteSpace(message)
            ? "알림 없음"
            : message;
        GetTree().Paused = true;
        SetPage(ReleaseShellPage.Title, hasSave ? _continue : _newGame);
    }

    public void ShowPause(string chapterName, string message = "")
    {
        _pauseChapter.Text = $"현재 임무 · {chapterName}";
        _pauseMessage.Text = message;
        GetTree().Paused = true;
        SetPage(ReleaseShellPage.Pause, _resume);
    }

    public void HideShell()
    {
        SetPage(ReleaseShellPage.Hidden);
        GetTree().Paused = false;
        GameplayFocusRequested?.Invoke();
    }

    public void ShowControlHelpBeforeGameplay()
    {
        _returnPage = ReleaseShellPage.Hidden;
        GetTree().Paused = true;
        SetPage(ReleaseShellPage.Help, GetNode<Button>("%HelpBackButton"));
    }

    public void SetSettings(
        bool fullscreen,
        int uiScalePercent,
        bool controlHelpEnabled,
        int masterVolumePercent,
        int ambientVolumePercent,
        int sfxVolumePercent)
    {
        _settingControls = true;
        _windowMode.Select(fullscreen ? 1 : 0);
        _uiScale.Select(uiScalePercent == 125 ? 1 : 0);
        _masterVolume.Select(VolumeIndex(masterVolumePercent));
        _ambientVolume.Select(VolumeIndex(ambientVolumePercent));
        _sfxVolume.Select(VolumeIndex(sfxVolumePercent));
        _controlHelp.ButtonPressed = controlHelpEnabled;
        _settingControls = false;
    }

    public void ShowPersistenceError(string message)
    {
        bool titleContext = _page == ReleaseShellPage.Title ||
            ((_page is ReleaseShellPage.Settings or ReleaseShellPage.Help or ReleaseShellPage.Confirm) &&
             _returnPage == ReleaseShellPage.Title);
        if (titleContext)
        {
            ShowTitle(_hasSave, message);
            return;
        }
        _pauseMessage.Text = message;
        _pauseMessage.AccessibilityName = message;
        SetPage(ReleaseShellPage.Pause, _resume);
    }

    public BaseButton GetActionButton(ReleaseShellAction action) => action switch
    {
        ReleaseShellAction.NewGame => _newGame,
        ReleaseShellAction.Continue => _continue,
        ReleaseShellAction.Quit => _quit,
        ReleaseShellAction.Resume => _resume,
        ReleaseShellAction.SaveAndQuit => GetNode<Button>("%SaveAndQuitButton"),
        ReleaseShellAction.RestartChapter => GetNode<Button>("%RestartChapterButton"),
        ReleaseShellAction.TitleSettings => GetNode<Button>("%TitleSettingsButton"),
        ReleaseShellAction.PauseSettings => GetNode<Button>("%PauseSettingsButton"),
        ReleaseShellAction.TitleHelp => GetNode<Button>("%TitleHelpButton"),
        ReleaseShellAction.PauseHelp => GetNode<Button>("%PauseHelpButton"),
        ReleaseShellAction.SettingsBack => GetNode<Button>("%SettingsBackButton"),
        ReleaseShellAction.HelpBack => GetNode<Button>("%HelpBackButton"),
        ReleaseShellAction.Confirm => _confirm,
        ReleaseShellAction.CancelConfirm => _cancelConfirm,
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    public OptionButton GetUiScaleOption() => _uiScale;

    public OptionButton GetSfxVolumeOption() => _sfxVolume;

    public CheckButton GetControlHelpCheck() => _controlHelp;

    private void OnNewGamePressed()
    {
        if (!_hasSave)
        {
            NewGameRequested?.Invoke();
            return;
        }
        ShowConfirmation(
            ReleaseShellConfirmation.NewGame,
            "새 게임을 시작할까요?",
            "지금까지 저장한 진행 상황이 사라집니다. 이 작업은 되돌릴 수 없습니다.",
            "저장 삭제 후 새 게임 시작하기");
    }

    private void OnRestartChapterPressed() => ShowConfirmation(
        ReleaseShellConfirmation.RestartChapter,
        "현재 임무를 다시 시작할까요?",
        "이번 임무에서 진행한 공사와 사용한 자금이 사라지고 임무 시작 시점으로 돌아갑니다.",
        "현재 임무 다시 시작하기");

    private void ShowSettings(ReleaseShellPage returnPage)
    {
        _returnPage = returnPage;
        SetPage(ReleaseShellPage.Settings, _windowMode);
    }

    private void ShowHelp(ReleaseShellPage returnPage)
    {
        _returnPage = returnPage;
        SetPage(ReleaseShellPage.Help, GetNode<Button>("%HelpBackButton"));
    }

    private void ShowConfirmation(
        ReleaseShellConfirmation confirmation,
        string heading,
        string body,
        string action)
    {
        _confirmation = confirmation;
        _returnPage = confirmation == ReleaseShellConfirmation.NewGame
            ? ReleaseShellPage.Title
            : ReleaseShellPage.Pause;
        _confirmHeading.Text = heading;
        _confirmBody.Text = body;
        _confirm.Text = action;
        _confirm.AccessibilityName = action;
        SetPage(ReleaseShellPage.Confirm, _cancelConfirm);
    }

    private void OnConfirmPressed()
    {
        ReleaseShellConfirmation confirmation = _confirmation;
        _confirmation = ReleaseShellConfirmation.None;
        if (confirmation == ReleaseShellConfirmation.NewGame)
        {
            NewGameRequested?.Invoke();
        }
        else if (confirmation == ReleaseShellConfirmation.RestartChapter)
        {
            RestartChapterRequested?.Invoke();
        }
    }

    private void ReturnFromSubpage()
    {
        _confirmation = ReleaseShellConfirmation.None;
        if (_returnPage == ReleaseShellPage.Hidden)
        {
            HideShell();
            return;
        }
        SetPage(
            _returnPage,
            _returnPage == ReleaseShellPage.Title ? _newGame : _resume);
    }

    private void SetPage(ReleaseShellPage page, Control? focus = null)
    {
        _page = page;
        Visible = page != ReleaseShellPage.Hidden;
        _titlePage.Visible = page == ReleaseShellPage.Title;
        _pausePage.Visible = page == ReleaseShellPage.Pause;
        _settingsPage.Visible = page == ReleaseShellPage.Settings;
        _helpPage.Visible = page == ReleaseShellPage.Help;
        _confirmPage.Visible = page == ReleaseShellPage.Confirm;
        if (focus is not null)
        {
            focus.CallDeferred(Control.MethodName.GrabFocus);
        }
    }

    private void EmitVolume(OptionButton option, long index, Action<int>? handler)
    {
        if (!_settingControls)
        {
            handler?.Invoke(option.GetItemId((int)index));
        }
    }

    private static void AddVolumeItems(OptionButton option)
    {
        foreach (int percent in new[] { 0, 25, 50, 75, 100 })
        {
            option.AddItem($"{percent}%", percent);
        }
    }

    private static int VolumeIndex(int percent) => percent switch
    {
        0 => 0,
        25 => 1,
        50 => 2,
        75 => 3,
        100 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(percent)),
    };
}
