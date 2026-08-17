using System;
using Godot;

namespace Gridworks.Game;

internal enum ProductShellPage
{
    Hidden,
    Title,
    Pause,
    Settings,
    Help,
    Confirm,
}

internal enum ProductShellConfirmation
{
    None,
    NewGame,
    RestartChapter,
}

internal enum ProductShellAction
{
    NewGame,
    Continue,
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

internal sealed partial class ProductShellOverlay : Control
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
    private Button _newGameButton = null!;
    private Button _continueButton = null!;
    private Button _resumeButton = null!;
    private Button _confirmButton = null!;
    private Button _cancelConfirmButton = null!;
    private OptionButton _windowModeOption = null!;
    private OptionButton _uiScaleOption = null!;
    private OptionButton _masterVolumeOption = null!;
    private OptionButton _ambientVolumeOption = null!;
    private OptionButton _sfxVolumeOption = null!;
    private CheckButton _controlHelpCheck = null!;
    private ProductShellPage _page = ProductShellPage.Hidden;
    private ProductShellPage _returnPage = ProductShellPage.Title;
    private ProductShellConfirmation _confirmation;
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

    public ProductShellPage Page => _page;

    public BaseButton GetActionButton(ProductShellAction action) => action switch
    {
        ProductShellAction.NewGame => _newGameButton,
        ProductShellAction.Continue => _continueButton,
        ProductShellAction.Resume => _resumeButton,
        ProductShellAction.SaveAndQuit => GetNode<Button>("%SaveAndQuitButton"),
        ProductShellAction.RestartChapter => GetNode<Button>("%RestartChapterButton"),
        ProductShellAction.TitleSettings => GetNode<Button>("%TitleSettingsButton"),
        ProductShellAction.PauseSettings => GetNode<Button>("%PauseSettingsButton"),
        ProductShellAction.TitleHelp => GetNode<Button>("%TitleHelpButton"),
        ProductShellAction.PauseHelp => GetNode<Button>("%PauseHelpButton"),
        ProductShellAction.SettingsBack => GetNode<Button>("%SettingsBackButton"),
        ProductShellAction.HelpBack => GetNode<Button>("%HelpBackButton"),
        ProductShellAction.Confirm => _confirmButton,
        ProductShellAction.CancelConfirm => _cancelConfirmButton,
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    public OptionButton GetUiScaleOption() => _uiScaleOption;

    public OptionButton GetSfxVolumeOption() => _sfxVolumeOption;

    public CheckButton GetControlHelpCheck() => _controlHelpCheck;

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
        _newGameButton = GetNode<Button>("%NewGameButton");
        _continueButton = GetNode<Button>("%ContinueButton");
        _resumeButton = GetNode<Button>("%ResumeButton");
        _confirmButton = GetNode<Button>("%ConfirmButton");
        _cancelConfirmButton = GetNode<Button>("%CancelConfirmButton");
        _windowModeOption = GetNode<OptionButton>("%WindowModeOption");
        _uiScaleOption = GetNode<OptionButton>("%UiScaleOption");
        _masterVolumeOption = GetNode<OptionButton>("%MasterVolumeOption");
        _ambientVolumeOption = GetNode<OptionButton>("%AmbientVolumeOption");
        _sfxVolumeOption = GetNode<OptionButton>("%SfxVolumeOption");
        _controlHelpCheck = GetNode<CheckButton>("%ControlHelpCheck");

        _windowModeOption.AddItem("창 모드", 0);
        _windowModeOption.AddItem("전체화면", 1);
        _uiScaleOption.AddItem("100%", 100);
        _uiScaleOption.AddItem("125%", 125);
        AddVolumeItems(_masterVolumeOption);
        AddVolumeItems(_ambientVolumeOption);
        AddVolumeItems(_sfxVolumeOption);

        _newGameButton.Pressed += OnNewGamePressed;
        _continueButton.Pressed += () => ContinueRequested?.Invoke();
        _resumeButton.Pressed += () => ResumeRequested?.Invoke();
        GetNode<Button>("%SaveAndQuitButton").Pressed += () => SaveAndQuitRequested?.Invoke();
        GetNode<Button>("%RestartChapterButton").Pressed += OnRestartChapterPressed;
        GetNode<Button>("%TitleSettingsButton").Pressed += () => ShowSettings(ProductShellPage.Title);
        GetNode<Button>("%PauseSettingsButton").Pressed += () => ShowSettings(ProductShellPage.Pause);
        GetNode<Button>("%TitleHelpButton").Pressed += () => ShowHelp(ProductShellPage.Title);
        GetNode<Button>("%PauseHelpButton").Pressed += () => ShowHelp(ProductShellPage.Pause);
        GetNode<Button>("%SettingsBackButton").Pressed += ReturnFromSubpage;
        GetNode<Button>("%HelpBackButton").Pressed += ReturnFromSubpage;
        _confirmButton.Pressed += OnConfirmPressed;
        _cancelConfirmButton.Pressed += ReturnFromSubpage;
        _windowModeOption.ItemSelected += index =>
        {
            if (!_settingControls)
            {
                FullscreenChanged?.Invoke(_windowModeOption.GetItemId((int)index) == 1);
            }
        };
        _uiScaleOption.ItemSelected += index =>
        {
            if (!_settingControls)
            {
                UiScalePercentChanged?.Invoke(_uiScaleOption.GetItemId((int)index));
            }
        };
        _masterVolumeOption.ItemSelected += index =>
        {
            if (!_settingControls)
            {
                MasterVolumePercentChanged?.Invoke(
                    _masterVolumeOption.GetItemId((int)index));
            }
        };
        _ambientVolumeOption.ItemSelected += index =>
        {
            if (!_settingControls)
            {
                AmbientVolumePercentChanged?.Invoke(
                    _ambientVolumeOption.GetItemId((int)index));
            }
        };
        _sfxVolumeOption.ItemSelected += index =>
        {
            if (!_settingControls)
            {
                SfxVolumePercentChanged?.Invoke(_sfxVolumeOption.GetItemId((int)index));
            }
        };
        _controlHelpCheck.Toggled += enabled =>
        {
            if (!_settingControls)
            {
                ControlHelpChanged?.Invoke(enabled);
            }
        };

        AccessibilityName = "Gridworks 타이틀과 일시정지 메뉴";
        SetPage(ProductShellPage.Hidden);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey { Pressed: true, Echo: false } key ||
            key.Keycode != Key.Escape)
        {
            return;
        }

        switch (_page)
        {
            case ProductShellPage.Hidden:
                PauseRequested?.Invoke();
                break;
            case ProductShellPage.Pause:
                ResumeRequested?.Invoke();
                break;
            case ProductShellPage.Settings:
            case ProductShellPage.Help:
            case ProductShellPage.Confirm:
                ReturnFromSubpage();
                break;
            case ProductShellPage.Title:
                return;
            default:
                throw new ArgumentOutOfRangeException();
        }
        GetViewport().SetInputAsHandled();
    }

    public void ShowTitle(bool hasSave, string message)
    {
        _hasSave = hasSave;
        _continueButton.Disabled = !hasSave;
        _continueButton.AccessibilityDescription = hasSave
            ? "저장된 한 개의 캠페인을 이어서 시작합니다."
            : "이어할 수 있는 유효한 저장이 없습니다.";
        _titleMessage.Text = message;
        _titleMessage.AccessibilityName = string.IsNullOrWhiteSpace(message)
            ? "저장 상태 이상 없음"
            : message;
        GetTree().Paused = true;
        SetPage(ProductShellPage.Title, hasSave ? _continueButton : _newGameButton);
    }

    public void ShowPause(string chapterName, string message = "")
    {
        _pauseChapter.Text = $"현재 장 · {chapterName}";
        _pauseMessage.Text = message;
        GetTree().Paused = true;
        SetPage(ProductShellPage.Pause, _resumeButton);
    }

    public void HideShell()
    {
        SetPage(ProductShellPage.Hidden);
        GetTree().Paused = false;
        GameplayFocusRequested?.Invoke();
    }

    public void ShowControlHelpBeforeGameplay()
    {
        _returnPage = ProductShellPage.Hidden;
        GetTree().Paused = true;
        SetPage(ProductShellPage.Help, GetNode<Button>("%HelpBackButton"));
    }

    public void SetSettings(
        bool fullscreen,
        int uiScalePercent,
        bool controlHelpEnabled,
        int masterVolumePercent,
        int ambientVolumePercent,
        int sfxVolumePercent)
    {
        if (uiScalePercent is not (100 or 125))
        {
            throw new ArgumentOutOfRangeException(
                nameof(uiScalePercent),
                "UI scale must be 100 or 125 percent.");
        }

        _settingControls = true;
        _windowModeOption.Select(fullscreen ? 1 : 0);
        _uiScaleOption.Select(uiScalePercent == 125 ? 1 : 0);
        _masterVolumeOption.Select(VolumeIndex(masterVolumePercent));
        _ambientVolumeOption.Select(VolumeIndex(ambientVolumePercent));
        _sfxVolumeOption.Select(VolumeIndex(sfxVolumePercent));
        _controlHelpCheck.ButtonPressed = controlHelpEnabled;
        _settingControls = false;
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

    public void ShowPauseError(string message)
    {
        _pauseMessage.Text = message;
        _pauseMessage.AccessibilityName = message;
        if (_page != ProductShellPage.Pause)
        {
            SetPage(ProductShellPage.Pause, _resumeButton);
        }
    }

    public void ShowPersistenceError(string message)
    {
        bool titleContext = _page == ProductShellPage.Title ||
            ((_page is ProductShellPage.Settings or ProductShellPage.Help or
                ProductShellPage.Confirm) && _returnPage == ProductShellPage.Title);
        if (titleContext)
        {
            ShowTitle(_hasSave, message);
            return;
        }
        ShowPauseError(message);
    }

    private void OnNewGamePressed()
    {
        if (!_hasSave)
        {
            NewGameRequested?.Invoke();
            return;
        }

        ShowConfirmation(
            ProductShellConfirmation.NewGame,
            "새 게임을 시작할까요?",
            "현재 단일 저장을 새 게임으로 덮어씁니다. 이 작업은 되돌릴 수 없습니다.",
            "저장을 덮어쓰고 새 게임");
    }

    private void OnRestartChapterPressed() =>
        ShowConfirmation(
            ProductShellConfirmation.RestartChapter,
            "현재 장을 다시 시작할까요?",
            "현재 장에서 진행한 내용은 사라지고 장 시작 checkpoint로 돌아갑니다.",
            "현재 장 다시 시작");

    private void ShowSettings(ProductShellPage returnPage)
    {
        _returnPage = returnPage;
        SetPage(ProductShellPage.Settings, _windowModeOption);
    }

    private void ShowHelp(ProductShellPage returnPage)
    {
        _returnPage = returnPage;
        SetPage(ProductShellPage.Help, GetNode<Button>("%HelpBackButton"));
    }

    private void ShowConfirmation(
        ProductShellConfirmation confirmation,
        string heading,
        string body,
        string action)
    {
        _confirmation = confirmation;
        _returnPage = confirmation == ProductShellConfirmation.NewGame
            ? ProductShellPage.Title
            : ProductShellPage.Pause;
        _confirmHeading.Text = heading;
        _confirmBody.Text = body;
        _confirmButton.Text = action;
        _confirmButton.AccessibilityName = action;
        SetPage(ProductShellPage.Confirm, _cancelConfirmButton);
    }

    private void OnConfirmPressed()
    {
        ProductShellConfirmation confirmation = _confirmation;
        _confirmation = ProductShellConfirmation.None;
        if (confirmation == ProductShellConfirmation.NewGame)
        {
            NewGameRequested?.Invoke();
        }
        else if (confirmation == ProductShellConfirmation.RestartChapter)
        {
            RestartChapterRequested?.Invoke();
        }
    }

    private void ReturnFromSubpage()
    {
        _confirmation = ProductShellConfirmation.None;
        if (_returnPage == ProductShellPage.Hidden)
        {
            HideShell();
            return;
        }
        SetPage(
            _returnPage,
            _returnPage == ProductShellPage.Title ? _newGameButton : _resumeButton);
    }

    private void SetPage(ProductShellPage page, Control? focus = null)
    {
        _page = page;
        Visible = page != ProductShellPage.Hidden;
        _titlePage.Visible = page == ProductShellPage.Title;
        _pausePage.Visible = page == ProductShellPage.Pause;
        _settingsPage.Visible = page == ProductShellPage.Settings;
        _helpPage.Visible = page == ProductShellPage.Help;
        _confirmPage.Visible = page == ProductShellPage.Confirm;
        AccessibilityName = page switch
        {
            ProductShellPage.Hidden => "Gridworks 게임 화면",
            ProductShellPage.Title => "Gridworks 타이틀 메뉴",
            ProductShellPage.Pause => "Gridworks 일시정지 메뉴",
            ProductShellPage.Settings => "Gridworks 화면과 소리 설정",
            ProductShellPage.Help => "Gridworks 조작 도움말",
            ProductShellPage.Confirm => "Gridworks 확인",
            _ => throw new ArgumentOutOfRangeException(nameof(page)),
        };
        if (focus is not null)
        {
            focus.CallDeferred(Control.MethodName.GrabFocus);
        }
    }
}
