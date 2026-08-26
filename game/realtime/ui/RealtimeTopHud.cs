using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Gridworks.Game.Realtime.UI;

internal sealed partial class RealtimeTopHud : PanelContainer
{
    private Label _chapter = null!;
    private Label _objective = null!;
    private Label _clock = null!;
    private Label _pauseStatus = null!;
    private Label _cash = null!;
    private Label _reliability = null!;
    private Label _warning = null!;
    private Button _pause = null!;
    private Button _normal = null!;
    private Button _fast = null!;
    private Button _veryFast = null!;
    private Button _menu = null!;
    private Button _settings = null!;
    private HBoxContainer _statusRow = null!;
    private HBoxContainer _controlRow = null!;
    private HBoxContainer _speedControls = null!;
    private IReadOnlyDictionary<RealtimeSimulationSpeed, Button> _speedButtons = null!;
    private bool _splitControls;
    private RealtimeLayoutProfile _layoutProfile;
    private string _objectiveFullText = string.Empty;
    private string _warningFullText = string.Empty;

    public event Action<RealtimeSimulationSpeed>? SpeedRequested;
    public event Action? MenuRequested;
    public event Action? SettingsRequested;

    internal Button SettingsButton => _settings;

    public override void _Ready()
    {
        _chapter = GetNode<Label>("%ChapterLabel");
        _objective = GetNode<Label>("%ObjectiveLabel");
        _clock = GetNode<Label>("%ClockLabel");
        _pauseStatus = GetNode<Label>("%PauseStatusLabel");
        _cash = GetNode<Label>("%CashLabel");
        _reliability = GetNode<Label>("%ReliabilityLabel");
        _warning = GetNode<Label>("%WarningLabel");
        _pause = GetNode<Button>("%PauseButton");
        _normal = GetNode<Button>("%NormalSpeedButton");
        _fast = GetNode<Button>("%FastSpeedButton");
        _veryFast = GetNode<Button>("%VeryFastSpeedButton");
        _menu = GetNode<Button>("%MenuButton");
        _settings = GetNode<Button>("%SettingsButton");
        _statusRow = GetNode<HBoxContainer>("%StatusRow");
        _controlRow = GetNode<HBoxContainer>("%ControlRow");
        _speedControls = GetNode<HBoxContainer>("%SpeedControls");

        var speedGroup = new ButtonGroup { AllowUnpress = false };
        _speedButtons = new Dictionary<RealtimeSimulationSpeed, Button>
        {
            [RealtimeSimulationSpeed.Paused] = _pause,
            [RealtimeSimulationSpeed.Normal] = _normal,
            [RealtimeSimulationSpeed.Fast] = _fast,
            [RealtimeSimulationSpeed.VeryFast] = _veryFast,
        };
        foreach ((RealtimeSimulationSpeed speed, Button button) in _speedButtons)
        {
            button.ToggleMode = true;
            button.ButtonGroup = speedGroup;
            button.Pressed += () => SpeedRequested?.Invoke(speed);
            button.AccessibilityDescription = $"시뮬레이션 속도를 {SpeedAccessibility(speed)}로 바꿉니다.";
            button.AccessibilityName = SpeedAccessibility(speed);
            button.TooltipText = speed switch
            {
                RealtimeSimulationSpeed.Paused => "시뮬레이션 일시정지 (P)",
                RealtimeSimulationSpeed.Normal => "시뮬레이션 1배속 (1)",
                RealtimeSimulationSpeed.Fast => "시뮬레이션 2배속 (2)",
                RealtimeSimulationSpeed.VeryFast => "시뮬레이션 4배속 (4)",
                _ => throw new ArgumentOutOfRangeException(nameof(speed)),
            };
        }
        _menu.Pressed += () => MenuRequested?.Invoke();
        _menu.Text = "도구";
        _menu.TooltipText = "건설·분석 도구 열기 또는 닫기 (B)";
        _menu.AccessibilityName = "건설·분석 도구";
        _menu.AccessibilityDescription =
            "하단의 건설 및 망 분석 도구를 열거나 닫습니다. 단축키 B.";
        _settings.Pressed += () => SettingsRequested?.Invoke();
        _settings.TooltipText = "화면, UI 배율, 음량과 움직임 설정";
        _settings.AccessibilityName = "설정";
        _settings.AccessibilityDescription =
            "현재 여정을 유지한 채 화면, UI 배율, 음량과 움직임 설정을 엽니다.";
    }

    public void SetPresentation(RealtimeTopHudPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        _chapter.Text = presentation.Chapter;
        _objectiveFullText = presentation.Objective;
        _objective.Text = $"임무 · {CompactObjective(
            presentation.Objective,
            _layoutProfile.AccessibilityScale)}";
        _objective.TooltipText = presentation.Objective;
        _objective.AccessibilityName = $"현재 임무 목표. {presentation.Objective}";
        _clock.Text = presentation.Clock;
        _pauseStatus.Text = SimulationStatus(presentation);
        _pauseStatus.AccessibilityName = $"시뮬레이션 상태. {_pauseStatus.Text}";
        _cash.Text = presentation.Cash;
        _reliability.Text = presentation.Reliability;
        _warningFullText = presentation.MajorWarning ?? string.Empty;
        _warning.Text = CompactWarning(
            _warningFullText,
            _layoutProfile.AccessibilityScale);
        _warning.TooltipText = _warningFullText;
        _warning.Visible = !string.IsNullOrWhiteSpace(presentation.MajorWarning);
        _reliability.AddThemeColorOverride("font_color", ReliabilityColor(
            presentation.ReliabilityState));
        bool canChooseSpeed = presentation.SimulationState is
            RealtimeSimulationState.Running or RealtimeSimulationState.PlayerPaused;
        bool canResume = presentation.SimulationState is
                RealtimeSimulationState.Running or RealtimeSimulationState.PlayerPaused ||
            presentation.SimulationState == RealtimeSimulationState.AutoPaused &&
            presentation.Pause.Reason is RealtimePauseReason.CriticalIncident or
                RealtimePauseReason.CatchUpCeiling;
        _pause.Disabled = !canResume;
        _normal.Disabled = !canChooseSpeed;
        _fast.Disabled = !canChooseSpeed;
        _veryFast.Disabled = !canChooseSpeed;
        foreach (Button button in _speedButtons.Values)
        {
            button.ButtonPressed = false;
        }
        _speedButtons[presentation.Speed].ButtonPressed = true;
        _pause.Text = presentation.SimulationState switch
        {
            RealtimeSimulationState.Running => "Ⅱ",
            RealtimeSimulationState.Ended => "■",
            _ => "▶",
        };
        _pause.TooltipText = presentation.SimulationState switch
        {
            RealtimeSimulationState.Running => "시뮬레이션 일시정지 (P)",
            RealtimeSimulationState.Ended => "운영이 종료되어 시간 제어를 사용할 수 없습니다.",
            _ => $"시뮬레이션 재개 (P) · {PauseReason(presentation.Pause.Reason)}",
        };
        _pause.AccessibilityName = presentation.SimulationState switch
        {
            RealtimeSimulationState.Running => "시뮬레이션 일시정지",
            RealtimeSimulationState.Ended => "운영 종료",
            _ => "시뮬레이션 재개",
        };
        _pause.AccessibilityDescription = presentation.SimulationState switch
        {
            RealtimeSimulationState.Running => "현재 시뮬레이션을 일시정지합니다.",
            RealtimeSimulationState.Ended =>
                $"운영이 종료되었습니다. 현재 {presentation.Pause.CurrentTimeLabel}.",
            _ => $"{PauseReason(presentation.Pause.Reason)}. " +
                 $"현재 {presentation.Pause.CurrentTimeLabel}. " +
                 $"다음 사건 {presentation.Pause.NextEventLabel}.",
        };
        string speedLock = presentation.SimulationState == RealtimeSimulationState.Ended
            ? "운영이 종료되어 배속을 바꿀 수 없습니다."
            : $"{PauseReason(presentation.Pause.Reason)} 동안 배속을 바꿀 수 없습니다.";
        if (!canChooseSpeed)
        {
            foreach (Button button in new[] { _normal, _fast, _veryFast })
            {
                button.TooltipText = speedLock;
                button.AccessibilityDescription = speedLock;
            }
        }
        else
        {
            RestoreSpeedDescriptions();
        }
        _menu.Text = presentation.BuildModeActive
            ? "건설 취소"
            : presentation.ToolShelfVisible
                ? "도구 닫기"
                : "도구";
        _menu.TooltipText = presentation.BuildModeActive
            ? "초안을 취소하려면 B 또는 Esc를 누른 뒤 한 번 더 눌러 확인합니다."
            : presentation.ToolShelfVisible
                ? "건설·분석 도구를 닫습니다. (B)"
                : "건설·분석 도구를 엽니다. (B)";
        _menu.AccessibilityName = _menu.Text;
        _menu.AccessibilityDescription = _menu.TooltipText;
        _reliability.Text = $"{ReliabilityIcon(presentation.ReliabilityState)} {presentation.Reliability}";
        if (_layoutProfile.AccessibilityScale > 0f)
        {
            ApplyContentMinimums(_layoutProfile);
        }
        AccessibilityName =
            $"상단 운영 정보. {presentation.Chapter}. 임무 목표 {presentation.Objective}. " +
            $"{presentation.Clock}. {_pauseStatus.Text}. " +
            $"{presentation.Cash}. {presentation.Reliability}." +
            (presentation.MajorWarning is null ? string.Empty : $" 경고 {presentation.MajorWarning}.");
    }

    public void ApplyLayout(RealtimeLayoutProfile profile)
    {
        _layoutProfile = profile;
        if (!string.IsNullOrWhiteSpace(_objectiveFullText))
        {
            _objective.Text = $"임무 · {CompactObjective(
                _objectiveFullText,
                profile.AccessibilityScale)}";
        }
        _warning.Text = CompactWarning(_warningFullText, profile.AccessibilityScale);
        SetSplitControls(profile.AccessibilityScale >= 1.25f);
        _statusRow.AddThemeConstantOverride(
            "separation",
            _splitControls ? 14 : 8);
        _controlRow.AddThemeConstantOverride("separation", 6);
        CustomMinimumSize = new Vector2(0f, profile.TopHudHeight);
        foreach (Button button in _speedButtons.Values)
        {
            button.CustomMinimumSize = new Vector2(
                profile.MinimumHitTarget,
                profile.MinimumHitTarget);
        }
        _menu.CustomMinimumSize = new Vector2(
            Math.Max(88, profile.MinimumHitTarget * 2),
            profile.MinimumHitTarget);
        _settings.CustomMinimumSize = new Vector2(
            Math.Max(88, profile.MinimumHitTarget * 2),
            profile.MinimumHitTarget);
        ApplyContentMinimums(profile);
    }

    private void SetSplitControls(bool split)
    {
        if (split == _splitControls)
        {
            return;
        }
        _splitControls = split;
        if (split)
        {
            _pauseStatus.Reparent(_controlRow);
            _cash.Reparent(_controlRow);
            _reliability.Reparent(_controlRow);
            _warning.Reparent(_controlRow);
            _speedControls.Reparent(_controlRow);
            _menu.Reparent(_controlRow);
            _settings.Reparent(_controlRow);
            _controlRow.MoveChild(_pauseStatus, 0);
            _controlRow.MoveChild(_cash, 1);
            _controlRow.MoveChild(_reliability, 2);
            _controlRow.MoveChild(_warning, 3);
            _controlRow.MoveChild(_speedControls, _controlRow.GetChildCount() - 3);
            _controlRow.MoveChild(_menu, _controlRow.GetChildCount() - 2);
            _controlRow.MoveChild(_settings, _controlRow.GetChildCount() - 1);
            _controlRow.Visible = true;
        }
        else
        {
            _pauseStatus.Reparent(_statusRow);
            _cash.Reparent(_statusRow);
            _reliability.Reparent(_statusRow);
            _warning.Reparent(_statusRow);
            _speedControls.Reparent(_statusRow);
            _menu.Reparent(_statusRow);
            _settings.Reparent(_statusRow);
            _statusRow.MoveChild(_pauseStatus, 3);
            _statusRow.MoveChild(_cash, 4);
            _statusRow.MoveChild(_reliability, 5);
            _statusRow.MoveChild(_warning, 6);
            _statusRow.MoveChild(_speedControls, 7);
            _statusRow.MoveChild(_menu, 8);
            _statusRow.MoveChild(_settings, 9);
            _controlRow.Visible = false;
        }
    }

    private static Color ReliabilityColor(RealtimeReliabilityState state) => state switch
    {
        RealtimeReliabilityState.Stable => Color.FromHtml("78c9c1"),
        RealtimeReliabilityState.Watch => Color.FromHtml("e0b15a"),
        RealtimeReliabilityState.Emergency => Color.FromHtml("ed756e"),
        RealtimeReliabilityState.Outage => Color.FromHtml("aab2b5"),
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static string SpeedAccessibility(RealtimeSimulationSpeed speed) => speed switch
    {
        RealtimeSimulationSpeed.Paused => "일시정지",
        RealtimeSimulationSpeed.Normal => "1배속",
        RealtimeSimulationSpeed.Fast => "2배속",
        RealtimeSimulationSpeed.VeryFast => "4배속",
        _ => throw new ArgumentOutOfRangeException(nameof(speed)),
    };

    private void ApplyContentMinimums(RealtimeLayoutProfile profile)
    {
        ApplyLabelMinimum(_chapter, profile, 20, 160f);
        ApplyLabelMinimum(_objective, profile, 16, 160f);
        ApplyLabelMinimum(_clock, profile, 16, 120f);
        ApplyLabelMinimum(_pauseStatus, profile, 16, 120f);
        ApplyLabelMinimum(_cash, profile, 16, 120f);
        ApplyLabelMinimum(_reliability, profile, 16, 100f);
        _warning.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ApplyLabelMinimum(_warning, profile, 16, 90f);
    }

    private static void ApplyLabelMinimum(
        Label label,
        RealtimeLayoutProfile profile,
        int baseFontSize,
        float minimum)
    {
        int fontSize = Math.Max(
            1,
            Mathf.RoundToInt(baseFontSize * profile.AccessibilityScale));
        float measured = label.GetThemeFont("font").GetStringSize(
            label.Text,
            HorizontalAlignment.Left,
            -1f,
            fontSize).X + (10f * profile.AccessibilityScale);
        label.CustomMinimumSize = new Vector2(
            Math.Max(measured, minimum),
            0f);
    }

    private static string PauseReason(RealtimePauseReason reason) => reason switch
    {
        RealtimePauseReason.None => "정지 상태",
        RealtimePauseReason.PlayerRequest => "플레이어가 일시정지함",
        RealtimePauseReason.ChapterBriefing => "장 안내를 읽는 중",
        RealtimePauseReason.CriticalIncident => "중대 사건 확인 중",
        RealtimePauseReason.RecoveryConfirmation => "복구 확인 중",
        RealtimePauseReason.CampaignResult => "결과 확인 중",
        RealtimePauseReason.CatchUpCeiling => "성능 보호 정지",
        RealtimePauseReason.FatalError => "오류로 정지됨",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    private void RestoreSpeedDescriptions()
    {
        foreach ((RealtimeSimulationSpeed speed, Button button) in _speedButtons
                     .Where(item => item.Key != RealtimeSimulationSpeed.Paused))
        {
            button.TooltipText = speed switch
            {
                RealtimeSimulationSpeed.Paused => "시뮬레이션 일시정지 (P)",
                RealtimeSimulationSpeed.Normal => "시뮬레이션 1배속 (1)",
                RealtimeSimulationSpeed.Fast => "시뮬레이션 2배속 (2)",
                RealtimeSimulationSpeed.VeryFast => "시뮬레이션 4배속 (4)",
                _ => throw new ArgumentOutOfRangeException(nameof(speed)),
            };
            button.AccessibilityDescription =
                $"시뮬레이션 속도를 {SpeedAccessibility(speed)}로 바꿉니다.";
        }
    }

    private static string SimulationStatus(RealtimeTopHudPresentation presentation) =>
        presentation.SimulationState switch
        {
            RealtimeSimulationState.Running => $"▶ {(int)presentation.Speed}× 운행 중",
            RealtimeSimulationState.PlayerPaused => "Ⅱ 플레이어 일시정지",
            RealtimeSimulationState.AutoPaused => $"Ⅱ {PauseStatusReason(presentation.Pause.Reason)}",
            RealtimeSimulationState.Ended => "■ 운영 종료",
            _ => throw new ArgumentOutOfRangeException(nameof(presentation)),
        };

    private static string PauseStatusReason(RealtimePauseReason reason) => reason switch
    {
        RealtimePauseReason.ChapterBriefing => "장 안내 정지",
        RealtimePauseReason.CriticalIncident => "중대 사건 정지",
        RealtimePauseReason.RecoveryConfirmation => "복구 확인 정지",
        RealtimePauseReason.CampaignResult => "결과 확인 정지",
        RealtimePauseReason.CatchUpCeiling => "성능 보호 정지",
        RealtimePauseReason.FatalError => "오류 정지",
        RealtimePauseReason.PlayerRequest => "플레이어 일시정지",
        _ => "일시정지",
    };

    private static string CompactObjective(string objective, float accessibilityScale)
    {
        int maxCharacters = accessibilityScale switch
        {
            >= 2f => 24,
            >= 1.5f => 28,
            >= 1.25f => 31,
            _ => 34,
        };
        string compact = objective.Trim();
        int clause = compact.LastIndexOf("해 ", StringComparison.Ordinal);
        if (clause >= 0 && clause + 2 < compact.Length)
        {
            compact = compact[(clause + 2)..];
        }
        compact = compact
            .Replace("공급하세요.", "공급", StringComparison.Ordinal)
            .Replace("확인하세요.", "확인", StringComparison.Ordinal);
        return compact.Length <= maxCharacters
            ? compact
            : $"{compact[..(maxCharacters - 1)]}…";
    }

    private static string CompactWarning(string warning, float accessibilityScale)
    {
        if (accessibilityScale < 1.5f || string.IsNullOrWhiteSpace(warning))
        {
            return warning;
        }
        if (warning.Contains("보호정지", StringComparison.Ordinal))
        {
            return "! 보호정지 설비";
        }
        if (warning.Contains("비상 열운전", StringComparison.Ordinal))
        {
            return "! 비상 열운전";
        }
        if (warning.Contains("미공급", StringComparison.Ordinal))
        {
            return "! 필수 수요 미공급";
        }
        const int maxCharacters = 12;
        return warning.Length <= maxCharacters
            ? warning
            : $"! {warning[..(maxCharacters - 2)]}…";
    }

    private static string ReliabilityIcon(RealtimeReliabilityState state) => state switch
    {
        RealtimeReliabilityState.Stable => "●",
        RealtimeReliabilityState.Watch => "◆",
        RealtimeReliabilityState.Emergency => "▲",
        RealtimeReliabilityState.Outage => "■",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
}
