using System;
using Gridworks.Core.Release.V2;
using Godot;

namespace Gridworks.Game;

internal enum CommercialShellSurface
{
    Hidden,
    Title,
    Pause,
    Settings,
    Help,
    Confirm,
    Story,
    Result,
}

internal enum CommercialShellAction
{
    NewGame,
    Continue,
    SaveAndQuit,
    ReturnToTitle,
}

internal sealed record CommercialTitlePresentation(
    string Subtitle,
    bool HasSave,
    string Status);

internal sealed record CommercialPausePresentation(
    string Context,
    string Status,
    bool SaveAndQuitEnabled,
    bool ReturnToTitleEnabled);

internal sealed record CommercialSettingsPresentation(
    bool Fullscreen,
    int UiScalePercent,
    int MasterVolumePercent,
    int AmbientVolumePercent,
    int SfxVolumePercent,
    bool ReduceMotion);

internal sealed record CommercialStoryPresentation(
    CommercialStoryCard Card,
    bool IsResult,
    string ContinueLabel,
    bool IsSystemWarning = false,
    string? KindLabel = null,
    CommercialStoryPortraitPresentation? Portrait = null);

internal sealed record CommercialStoryPortraitPresentation(
    string ResourcePath,
    string AccessibilityDescription);

internal sealed partial class CommercialShell : Control
{
    private Control _title = null!;
    private Control _pause = null!;
    private Control _settings = null!;
    private Control _help = null!;
    private Control _confirm = null!;
    private Control _story = null!;
    private Label _titleSubtitle = null!;
    private Label _titleStatus = null!;
    private Button _newGame = null!;
    private Button _continue = null!;
    private Label _pauseContext = null!;
    private Label _pauseStatus = null!;
    private Button _saveAndQuit = null!;
    private Button _returnToTitle = null!;
    private Label _helpBody = null!;
    private Label _confirmHeading = null!;
    private Label _confirmBody = null!;
    private Button _confirmAction = null!;
    private Label _storyKind = null!;
    private TextureRect _storyPortrait = null!;
    private Label _storySpeaker = null!;
    private Label _storyTitle = null!;
    private ScrollContainer _storyBodyScroll = null!;
    private Label _storyBody = null!;
    private Button _storyContinue = null!;
    private OptionButton _fullscreen = null!;
    private OptionButton _uiScale = null!;
    private SpinBox _master = null!;
    private SpinBox _ambient = null!;
    private SpinBox _sfx = null!;
    private CheckButton _reduceMotion = null!;
    private Label _settingsStatus = null!;
    private CommercialShellSurface _surface;
    private CommercialShellSurface _returnSurface;
    private CommercialShellAction? _pendingConfirmation;
    private string? _pendingConfirmationId;
    private Control? _returnFocus;
    private bool _settingControls;

    public event Action<CommercialShellAction>? ActionRequested;
    public event Action<CommercialSettingsPresentation>? SettingsChanged;
    public event Action? StoryAcknowledged;
    public event Action? GameplayFocusRequested;
    public event Action<string>? ConfirmationAccepted;

    public CommercialShellSurface Surface => _surface;
#if DEBUG
    public string HelpText => _helpBody.Text;
    public string StoryKindText => _storyKind.Text;
    public string StoryBodyText => _storyBody.Text;
    public string TitleStatusText => _titleStatus.Text;
    public ScrollContainer StoryBodyScroll => _storyBodyScroll;
    public BaseButton StoryContinueButton => _storyContinue;

    public BaseButton GetActionButton(CommercialShellAction action) => action switch
    {
        CommercialShellAction.NewGame => _newGame,
        CommercialShellAction.Continue => _continue,
        CommercialShellAction.SaveAndQuit => _saveAndQuit,
        CommercialShellAction.ReturnToTitle => _returnToTitle,
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };
#endif

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _title = GetNode<Control>("%TitlePage");
        _pause = GetNode<Control>("%PausePage");
        _settings = GetNode<Control>("%SettingsPage");
        _help = GetNode<Control>("%HelpPage");
        _confirm = GetNode<Control>("%ConfirmPage");
        _story = GetNode<Control>("%StoryPage");
        _titleSubtitle = GetNode<Label>("%TitleSubtitle");
        _titleStatus = GetNode<Label>("%TitleStatus");
        _newGame = GetNode<Button>("%NewGameButton");
        _continue = GetNode<Button>("%ContinueButton");
        _pauseContext = GetNode<Label>("%PauseContext");
        _pauseStatus = GetNode<Label>("%PauseStatus");
        _saveAndQuit = GetNode<Button>("%SaveAndQuitButton");
        _returnToTitle = GetNode<Button>("%ReturnToTitleButton");
        _helpBody = GetNode<Label>("%HelpBody");
        _confirmHeading = GetNode<Label>("%ConfirmHeading");
        _confirmBody = GetNode<Label>("%ConfirmBody");
        _confirmAction = GetNode<Button>("%ConfirmActionButton");
        _storyKind = GetNode<Label>("%StoryKindLabel");
        _storyPortrait = GetNode<TextureRect>("%StoryPortrait");
        _storySpeaker = GetNode<Label>("%StorySpeakerLabel");
        _storyTitle = GetNode<Label>("%StoryTitleLabel");
        _storyBodyScroll = GetNode<ScrollContainer>("%StoryBodyScroll");
        _storyBody = GetNode<Label>("%StoryBodyLabel");
        _storyContinue = GetNode<Button>("%StoryContinueButton");
        _fullscreen = GetNode<OptionButton>("%FullscreenOption");
        _uiScale = GetNode<OptionButton>("%UiScaleOption");
        _master = GetNode<SpinBox>("%MasterVolumeOption");
        _ambient = GetNode<SpinBox>("%AmbientVolumeOption");
        _sfx = GetNode<SpinBox>("%SfxVolumeOption");
        _reduceMotion = GetNode<CheckButton>("%ReduceMotionCheck");
        _settingsStatus = GetNode<Label>("%SettingsStatus");

        AddChoice(_fullscreen, "창 모드", 0, "전체화면", 1);
        AddChoice(_uiScale, "100%", 100, "125%", 125);
        _master.AccessibilityName = "전체 음량 백분율";
        _ambient.AccessibilityName = "환경음 백분율";
        _sfx.AccessibilityName = "효과음 백분율";

        _newGame.Pressed += () => ActionRequested?.Invoke(CommercialShellAction.NewGame);
        _continue.Pressed += () => ActionRequested?.Invoke(CommercialShellAction.Continue);
        GetNode<Button>("%TitleSettingsButton").Pressed += () => ShowSettings(CommercialShellSurface.Title);
        GetNode<Button>("%TitleHelpButton").Pressed += () => ShowHelp(CommercialShellSurface.Title);
        GetNode<Button>("%ResumeButton").Pressed += HideShell;
        _saveAndQuit.Pressed += () => ActionRequested?.Invoke(CommercialShellAction.SaveAndQuit);
        _returnToTitle.Pressed += () => ShowConfirmation(
            CommercialShellAction.ReturnToTitle,
            "제목 화면으로 돌아가기",
            "현재 장을 떠나기 전에 저장 상태를 확인하세요.",
            "제목 화면으로");
        GetNode<Button>("%PauseSettingsButton").Pressed += () => ShowSettings(CommercialShellSurface.Pause);
        GetNode<Button>("%PauseHelpButton").Pressed += () => ShowHelp(CommercialShellSurface.Pause);
        GetNode<Button>("%SettingsBackButton").Pressed += ReturnFromSubpage;
        GetNode<Button>("%HelpBackButton").Pressed += ReturnFromSubpage;
        GetNode<Button>("%CancelConfirmButton").Pressed += ReturnFromSubpage;
        _confirmAction.Pressed += Confirm;
        _storyContinue.Pressed += () => StoryAcknowledged?.Invoke();
        _storyBodyScroll.GuiInput += OnStoryBodyScrollGuiInput;
        _fullscreen.ItemSelected += _ => EmitSettings();
        _uiScale.ItemSelected += _ => EmitSettings();
        _master.ValueChanged += _ => EmitSettings();
        _ambient.ValueChanged += _ => EmitSettings();
        _sfx.ValueChanged += _ => EmitSettings();
        _reduceMotion.Toggled += _ => EmitSettings();
        SetSurface(CommercialShellSurface.Hidden);
    }

    public void ShowTitle(CommercialTitlePresentation model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _titleSubtitle.Text = model.Subtitle;
        _titleStatus.Text = model.Status;
        _continue.Disabled = !model.HasSave;
        _continue.AccessibilityDescription = model.HasSave
            ? "저장된 게임을 이어서 시작합니다."
            : "이어할 수 있는 저장이 없습니다.";
        SetSurface(CommercialShellSurface.Title, model.HasSave ? _continue : _newGame);
    }

    public void ShowPause(CommercialPausePresentation model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _pauseContext.Text = model.Context;
        _pauseStatus.Text = model.Status;
        _saveAndQuit.Disabled = !model.SaveAndQuitEnabled;
        _returnToTitle.Disabled = !model.ReturnToTitleEnabled;
        SetSurface(CommercialShellSurface.Pause, GetNode<Button>("%ResumeButton"));
    }

    public void SetHelpText(string text)
    {
        _helpBody.Text = text;
        _helpBody.AccessibilityName = text.Replace('\n', ' ');
    }

    public void ShowHelp() => ShowHelp(
        _surface is CommercialShellSurface.Title or CommercialShellSurface.Pause
            ? _surface
            : CommercialShellSurface.Hidden);

    public void ShowStory(CommercialStoryPresentation model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _storyKind.Text = model.KindLabel ?? (model.IsSystemWarning
            ? "저장 경고"
            : model.IsResult
                ? "결과"
                : "이야기");
        if (model.Portrait is CommercialStoryPortraitPresentation portrait)
        {
            _storyPortrait.Texture = ResourceLoader.Load<Texture2D>(portrait.ResourcePath);
            _storyPortrait.Visible = _storyPortrait.Texture is not null;
            _storyPortrait.AccessibilityName = portrait.AccessibilityDescription;
            _storyPortrait.AccessibilityDescription =
                "말하는 사람을 구분하는 고정 인물 초상입니다.";
        }
        else
        {
            _storyPortrait.Texture = null;
            _storyPortrait.Visible = false;
            _storyPortrait.AccessibilityName = "인물 초상 없음";
            _storyPortrait.AccessibilityDescription = string.Empty;
        }
        _storySpeaker.Text = model.Card.Speaker;
        _storyTitle.Text = model.Card.Title;
        _storyBody.Text = model.Card.Body;
        _storyBodyScroll.ScrollVertical = 0;
        _storyBodyScroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, 0);
        _storyContinue.Text = model.ContinueLabel;
        _story.AccessibilityDescription = model.Portrait is null
            ? $"{_storyKind.Text}. {model.Card.Speaker}. {model.Card.Title}."
            : $"{_storyKind.Text}. {model.Portrait.AccessibilityDescription}. {model.Card.Title}.";
        SetSurface(model.IsResult ? CommercialShellSurface.Result : CommercialShellSurface.Story,
            _storyContinue);
    }

    public void ShowConfirmation(
        CommercialShellAction action,
        string heading,
        string body,
        string actionLabel)
    {
        _pendingConfirmation = action;
        _pendingConfirmationId = null;
        _returnSurface = _surface;
        _returnFocus = GetViewport().GuiGetFocusOwner();
        _confirmHeading.Text = heading;
        _confirmBody.Text = body;
        _confirmAction.Text = actionLabel;
        SetSurface(CommercialShellSurface.Confirm, GetNode<Button>("%CancelConfirmButton"));
    }

    public void ShowConfirmation(
        string confirmationId,
        string heading,
        string body,
        string actionLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationId);
        _pendingConfirmation = null;
        _pendingConfirmationId = confirmationId;
        _returnSurface = _surface;
        _returnFocus = GetViewport().GuiGetFocusOwner();
        _confirmHeading.Text = heading;
        _confirmBody.Text = body;
        _confirmAction.Text = actionLabel;
        _confirmBody.AccessibilityName = body.Replace('\n', ' ');
        SetSurface(CommercialShellSurface.Confirm, GetNode<Button>("%CancelConfirmButton"));
    }

    public void SetSettings(CommercialSettingsPresentation settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settingControls = true;
        SelectById(_fullscreen, settings.Fullscreen ? 1 : 0);
        SelectById(_uiScale, settings.UiScalePercent);
        _master.Value = settings.MasterVolumePercent;
        _ambient.Value = settings.AmbientVolumePercent;
        _sfx.Value = settings.SfxVolumePercent;
        _reduceMotion.ButtonPressed = settings.ReduceMotion;
        _settingControls = false;
    }

    public void SetSettingsStatus(string status, bool isError = false)
    {
        _settingsStatus.Text = status;
        _settingsStatus.AccessibilityName = string.IsNullOrWhiteSpace(status)
            ? "설정 상태 알림 없음"
            : status;
        _settingsStatus.AccessibilityLive = isError
            ? AccessibilityServer.AccessibilityLiveMode.Assertive
            : AccessibilityServer.AccessibilityLiveMode.Polite;
        _settingsStatus.AddThemeColorOverride(
            "font_color",
            isError ? Color.FromHtml("ed756e") : Color.FromHtml("78c9c1"));
    }

    public bool HandleEscape()
    {
        switch (_surface)
        {
            case CommercialShellSurface.Hidden:
                return false;
            case CommercialShellSurface.Pause:
                HideShell();
                return true;
            case CommercialShellSurface.Settings:
            case CommercialShellSurface.Help:
            case CommercialShellSurface.Confirm:
                ReturnFromSubpage();
                return true;
            case CommercialShellSurface.Title:
            case CommercialShellSurface.Story:
            case CommercialShellSurface.Result:
                return true;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void HideShell()
    {
        SetSurface(CommercialShellSurface.Hidden);
        GameplayFocusRequested?.Invoke();
    }

    private void ShowSettings(CommercialShellSurface returnSurface)
    {
        _returnSurface = returnSurface;
        _returnFocus = GetViewport().GuiGetFocusOwner();
        SetSurface(CommercialShellSurface.Settings, _fullscreen);
    }

    private void ShowHelp(CommercialShellSurface returnSurface)
    {
        _returnSurface = returnSurface;
        _returnFocus = GetViewport().GuiGetFocusOwner();
        SetSurface(CommercialShellSurface.Help, GetNode<Button>("%HelpBackButton"));
    }

    private void ReturnFromSubpage()
    {
        _pendingConfirmation = null;
        _pendingConfirmationId = null;
        if (_returnSurface == CommercialShellSurface.Hidden)
        {
            Control? hiddenFocus = ReturnFocusTarget(CommercialShellSurface.Hidden);
            _returnFocus = null;
            SetSurface(CommercialShellSurface.Hidden, hiddenFocus);
            if (hiddenFocus is null)
            {
                GameplayFocusRequested?.Invoke();
            }
            return;
        }
        CommercialShellSurface returnSurface = _returnSurface;
        Control? focus = ReturnFocusTarget(returnSurface);
        _returnFocus = null;
        SetSurface(returnSurface, focus);
    }

    private Control? ReturnFocusTarget(CommercialShellSurface surface)
    {
        if (_returnFocus is { } opener &&
            opener.IsInsideTree() &&
            opener.FocusMode != FocusModeEnum.None &&
            (opener is not BaseButton button || !button.Disabled))
        {
            return opener;
        }
        return surface switch
        {
            CommercialShellSurface.Title => _continue.Disabled ? _newGame : _continue,
            CommercialShellSurface.Pause => GetNode<Button>("%ResumeButton"),
            _ => null,
        };
    }

    private void Confirm()
    {
        if (_pendingConfirmation is CommercialShellAction action)
        {
            _pendingConfirmation = null;
            ActionRequested?.Invoke(action);
            return;
        }
        if (_pendingConfirmationId is string confirmationId)
        {
            _pendingConfirmationId = null;
            ConfirmationAccepted?.Invoke(confirmationId);
        }
    }

    private void EmitSettings()
    {
        if (_settingControls)
        {
            return;
        }
        SettingsChanged?.Invoke(new CommercialSettingsPresentation(
            SelectedId(_fullscreen) == 1,
            SelectedId(_uiScale),
            (int)_master.Value,
            (int)_ambient.Value,
            (int)_sfx.Value,
            _reduceMotion.ButtonPressed));
    }

    private void OnStoryBodyScrollGuiInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        int page = Math.Max(32, (int)MathF.Round(_storyBodyScroll.Size.Y * 0.8f));
        int next = key.Keycode switch
        {
            Key.Up => _storyBodyScroll.ScrollVertical - 32,
            Key.Down => _storyBodyScroll.ScrollVertical + 32,
            Key.Pageup => _storyBodyScroll.ScrollVertical - page,
            Key.Pagedown => _storyBodyScroll.ScrollVertical + page,
            Key.Home => 0,
            Key.End => int.MaxValue,
            _ => _storyBodyScroll.ScrollVertical,
        };
        if (next == _storyBodyScroll.ScrollVertical &&
            key.Keycode is not Key.Home and not Key.End)
        {
            return;
        }
        _storyBodyScroll.ScrollVertical = next;
        _storyBodyScroll.AcceptEvent();
    }

    private void SetSurface(CommercialShellSurface surface, Control? focus = null)
    {
        _surface = surface;
        Visible = surface != CommercialShellSurface.Hidden;
        _title.Visible = surface == CommercialShellSurface.Title;
        _pause.Visible = surface == CommercialShellSurface.Pause;
        _settings.Visible = surface == CommercialShellSurface.Settings;
        _help.Visible = surface == CommercialShellSurface.Help;
        _confirm.Visible = surface == CommercialShellSurface.Confirm;
        _story.Visible = surface is CommercialShellSurface.Story or CommercialShellSurface.Result;
        AccessibilityName = surface switch
        {
            CommercialShellSurface.Hidden => "Gridworks 게임 화면",
            CommercialShellSurface.Title => "Gridworks 제목 화면",
            CommercialShellSurface.Pause => "Gridworks 일시정지 메뉴",
            CommercialShellSurface.Settings => "Gridworks 설정",
            CommercialShellSurface.Help => "Gridworks 조작 안내",
            CommercialShellSurface.Confirm => "Gridworks 확인",
            CommercialShellSurface.Story => "Gridworks 이야기 카드",
            CommercialShellSurface.Result => "Gridworks 결과 카드",
            _ => throw new ArgumentOutOfRangeException(nameof(surface)),
        };
        focus?.CallDeferred(Control.MethodName.GrabFocus);
    }

    private static void AddChoice(
        OptionButton option,
        string firstText,
        int firstId,
        string secondText,
        int secondId)
    {
        option.AddItem(firstText, firstId);
        option.AddItem(secondText, secondId);
    }

    private static void SelectById(OptionButton option, int id)
    {
        for (int index = 0; index < option.ItemCount; index++)
        {
            if (option.GetItemId(index) == id)
            {
                option.Select(index);
                return;
            }
        }
        throw new ArgumentOutOfRangeException(nameof(id));
    }

    private static int SelectedId(OptionButton option) =>
        option.GetItemId(option.Selected);
}
