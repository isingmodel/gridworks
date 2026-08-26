using System;
using Godot;

namespace Gridworks.Game.Realtime.UI;

internal enum RealtimeSettingsJourney
{
    ProductTitle,
    Gameplay,
}

internal sealed record RealtimeSettingsValues(
    bool Fullscreen,
    int UiScalePercent,
    int MasterVolumePercent,
    int AmbientVolumePercent,
    int SfxVolumePercent,
    bool ReduceMotion);

internal sealed record RealtimeSettingsPresentation(
    RealtimeSettingsValues Values,
    string Status,
    bool CanApply = true,
    bool IsError = false);

/// <summary>
/// Shared current-R2 settings editor. Persistence and runtime projection remain
/// outside this view; it renders committed values and emits one typed candidate.
/// </summary>
internal sealed partial class RealtimeSettingsSurface : Control
{
    private static readonly int[] UiScaleValues = [100, 125, 150, 200];
    private static readonly int[] VolumeValues = [0, 25, 50, 75, 100];

    private PanelContainer _panel = null!;
    private RealtimeFocusScope _focusScope = null!;
    private OptionButton _windowMode = null!;
    private OptionButton _uiScale = null!;
    private OptionButton _masterVolume = null!;
    private OptionButton _ambientVolume = null!;
    private OptionButton _sfxVolume = null!;
    private CheckButton _reduceMotion = null!;
    private Label _status = null!;
    private Button _close = null!;
    private Button _apply = null!;
    private bool _closePending;

    public event Action<RealtimeSettingsValues>? CandidateRequested;
    public event Action? CloseRequested;

    internal Button ApplyButton => _apply;

    internal Button CloseButton => _close;

    internal OptionButton WindowModeOption => _windowMode;

    internal OptionButton UiScaleOption => _uiScale;

    internal OptionButton MasterVolumeOption => _masterVolume;

    internal OptionButton AmbientVolumeOption => _ambientVolume;

    internal OptionButton SfxVolumeOption => _sfxVolume;

    internal CheckButton ReduceMotionCheck => _reduceMotion;

    internal string StatusText => _status.Text;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _panel = GetNode<PanelContainer>("%SettingsPanel");
        _focusScope = GetNode<RealtimeFocusScope>("%FocusScope");
        _windowMode = GetNode<OptionButton>("%WindowModeOption");
        _uiScale = GetNode<OptionButton>("%UiScaleOption");
        _masterVolume = GetNode<OptionButton>("%MasterVolumeOption");
        _ambientVolume = GetNode<OptionButton>("%AmbientVolumeOption");
        _sfxVolume = GetNode<OptionButton>("%SfxVolumeOption");
        _reduceMotion = GetNode<CheckButton>("%ReduceMotionCheck");
        _status = GetNode<Label>("%SettingsStatusLabel");
        _close = GetNode<Button>("%SettingsCloseButton");
        _apply = GetNode<Button>("%SettingsApplyButton");

        AddChoice(_windowMode, "창 모드", 0);
        AddChoice(_windowMode, "전체 화면", 1);
        AddChoices(_uiScale, UiScaleValues, value => $"{value}%");
        AddChoices(_masterVolume, VolumeValues, VolumeLabel);
        AddChoices(_ambientVolume, VolumeValues, VolumeLabel);
        AddChoices(_sfxVolume, VolumeValues, VolumeLabel);

        _windowMode.AccessibilityName = "화면 모드";
        _uiScale.AccessibilityName = "UI 배율";
        _masterVolume.AccessibilityName = "전체 음량";
        _ambientVolume.AccessibilityName = "환경음 음량";
        _sfxVolume.AccessibilityName = "효과음 음량";
        _reduceMotion.AccessibilityName = "움직임 줄이기";
        _reduceMotion.AccessibilityDescription =
            "장식 움직임을 줄여 화면 변화를 더 차분하게 표시합니다.";
        _close.AccessibilityDescription =
            "변경 후보를 적용하지 않고 설정을 연 화면으로 돌아갑니다.";
        _apply.AccessibilityDescription =
            "선택한 설정을 저장하고 적용하도록 요청합니다.";
        _close.Pressed += RequestClose;
        _apply.Pressed += () => CandidateRequested?.Invoke(ReadCandidate());
        AccessibilityName = "Gridworks 설정";
        Dismiss(restoreFocus: false);
    }

    public void Present(
        RealtimeSettingsPresentation presentation,
        Control? returnFocus)
    {
        SetPresentation(presentation);
        _closePending = false;
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;
        _focusScope.Activate(_windowMode, returnFocus);
    }

    public void SetPresentation(RealtimeSettingsPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(presentation.Values);
        Validate(presentation.Values);
        if (string.IsNullOrWhiteSpace(presentation.Status))
        {
            throw new ArgumentException(
                "Settings presentation requires a visible status.",
                nameof(presentation));
        }

        SelectById(_windowMode, presentation.Values.Fullscreen ? 1 : 0);
        SelectById(_uiScale, presentation.Values.UiScalePercent);
        SelectById(_masterVolume, presentation.Values.MasterVolumePercent);
        SelectById(_ambientVolume, presentation.Values.AmbientVolumePercent);
        SelectById(_sfxVolume, presentation.Values.SfxVolumePercent);
        _reduceMotion.ButtonPressed = presentation.Values.ReduceMotion;
        _status.Text = presentation.Status;
        _status.AccessibilityName = presentation.Status;
        _status.AccessibilityLive = presentation.IsError
            ? AccessibilityServer.AccessibilityLiveMode.Assertive
            : AccessibilityServer.AccessibilityLiveMode.Polite;
        _status.AddThemeColorOverride(
            "font_color",
            presentation.IsError
                ? Color.FromHtml("ed756e")
                : Color.FromHtml("efc469"));
        foreach (OptionButton option in new[]
                 {
                     _windowMode,
                     _uiScale,
                     _masterVolume,
                     _ambientVolume,
                     _sfxVolume,
                 })
        {
            option.Disabled = !presentation.CanApply;
        }
        _reduceMotion.Disabled = !presentation.CanApply;
        _apply.Disabled = !presentation.CanApply;
        _apply.TooltipText = presentation.CanApply
            ? "선택한 설정을 저장한 뒤 적용합니다."
            : presentation.Status;
        _apply.AccessibilityDescription = presentation.CanApply
            ? "선택한 설정을 저장하고 적용하도록 요청합니다."
            : presentation.Status;
        AccessibilityDescription = $"현재 설정. {presentation.Status}";
        if (Visible)
        {
            _focusScope.Refresh();
        }
    }

    public void Dismiss(bool restoreFocus = true)
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _closePending = false;
        if (_focusScope is not null)
        {
            _focusScope.Deactivate(restoreFocus);
        }
    }

    public bool HandleCancel()
    {
        if (!Visible)
        {
            return false;
        }
        RequestClose();
        return true;
    }

    public void ApplyLayout(RealtimeLayoutProfile profile)
    {
        float width = Math.Clamp(
            760f * profile.AccessibilityScale,
            720f,
            1120f);
        float height = Math.Clamp(
            760f * profile.AccessibilityScale,
            720f,
            980f);
        _panel.CustomMinimumSize = new Vector2(width, height);
        foreach (OptionButton option in new[]
                 {
                     _windowMode,
                     _uiScale,
                     _masterVolume,
                     _ambientVolume,
                     _sfxVolume,
                 })
        {
            option.CustomMinimumSize = new Vector2(
                Math.Max(240f, profile.MinimumHitTarget * 4.5f),
                profile.MinimumHitTarget);
        }
        _reduceMotion.CustomMinimumSize = new Vector2(
            0f,
            profile.MinimumHitTarget);
        _close.CustomMinimumSize = new Vector2(
            Math.Max(180f, profile.MinimumHitTarget * 3.5f),
            profile.PrimaryHitTarget);
        _apply.CustomMinimumSize = new Vector2(
            Math.Max(220f, profile.MinimumHitTarget * 4f),
            profile.PrimaryHitTarget);
        _status.CustomMinimumSize = new Vector2(
            0f,
            profile.MinimumHitTarget);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (Visible && inputEvent.IsActionPressed("ui_cancel"))
        {
            HandleCancel();
            GetViewport().SetInputAsHandled();
        }
    }

    private void RequestClose()
    {
        if (_closePending)
        {
            return;
        }
        _closePending = true;
        CloseRequested?.Invoke();
    }

    private RealtimeSettingsValues ReadCandidate() => new(
        Fullscreen: SelectedId(_windowMode) == 1,
        UiScalePercent: SelectedId(_uiScale),
        MasterVolumePercent: SelectedId(_masterVolume),
        AmbientVolumePercent: SelectedId(_ambientVolume),
        SfxVolumePercent: SelectedId(_sfxVolume),
        ReduceMotion: _reduceMotion.ButtonPressed);

    private static void Validate(RealtimeSettingsValues values)
    {
        if (Array.IndexOf(UiScaleValues, values.UiScalePercent) < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(values),
                values.UiScalePercent,
                "Unsupported UI scale.");
        }
        foreach (int volume in new[]
                 {
                     values.MasterVolumePercent,
                     values.AmbientVolumePercent,
                     values.SfxVolumePercent,
                 })
        {
            if (Array.IndexOf(VolumeValues, volume) < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(values),
                    volume,
                    "Unsupported volume step.");
            }
        }
    }

    private static void AddChoices(
        OptionButton option,
        int[] values,
        Func<int, string> label)
    {
        foreach (int value in values)
        {
            AddChoice(option, label(value), value);
        }
    }

    private static void AddChoice(OptionButton option, string label, int id) =>
        option.AddItem(label, id);

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

    private static string VolumeLabel(int value) => value == 0
        ? "0% · 음소거"
        : $"{value}%";
}
