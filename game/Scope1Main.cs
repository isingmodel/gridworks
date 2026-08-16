using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gridworks.Core;
using Godot;

namespace Gridworks.Game;

public sealed partial class Scope1Main : Control
{
    private static readonly Color BackgroundColor = Color.FromHtml("071019");
    private static readonly Color PanelColor = Color.FromHtml("101d27");
    private static readonly Color PanelBorderColor = Color.FromHtml("385166");
    private static readonly Color TextColor = Color.FromHtml("e6eef2");
    private static readonly Color MutedColor = Color.FromHtml("9fb0b9");
    private static readonly Color AccentColor = Color.FromHtml("5bc0be");
    private static readonly Color WarningColor = Color.FromHtml("e0a458");
    private static readonly Color ErrorColor = Color.FromHtml("e66d66");

    private Scope1LaunchOptions _options = null!;
    private Scope1DiagnosticWriter? _diagnostic;
    private Scope1Fixture _fixture = null!;
    private Scope1PlacementSession _session = null!;
    private Scope1View _view = null!;
    private Scope1PreviewResult? _pointerPreview;
    private string _fixtureHash = string.Empty;
    private string _buildHash = string.Empty;
    private bool _finalLogged;

    private Label _header = null!;
    private Label _phaseLabel = null!;
    private Label _instructionLabel = null!;
    private Label _pathStatusLabel = null!;
    private Label _pointerStatusLabel = null!;
    private Label _errorLabel = null!;
    private Button _undoButton = null!;
    private Button _orderButton = null!;
    private Button _advanceButton = null!;
    private Scope1PlacementMapView _mapView = null!;

    public override void _Ready()
    {
        try
        {
            GetWindow().Title = "Gridworks — 수동 선로 건설";
            _options = Scope1LaunchOptions.Parse(OS.GetCmdlineUserArgs());

            string fixturePath = Path.GetFullPath(Path.Combine(
                ProjectSettings.GlobalizePath("res://"), "..", "data", "scope-1-v1.json"));
            byte[] fixtureBytes = File.ReadAllBytes(fixturePath);
            _fixtureHash = LowerHex(SHA256.HashData(fixtureBytes));
            _buildHash = ComputeBuildHash();

            _fixture = Scope1FixtureLoader.Load(fixtureBytes);
            _session = new Scope1PlacementSession(_fixture);
            _view = _session.GetView();
            _diagnostic = new Scope1DiagnosticWriter(_options.DiagnosticPath, _options.SessionId);

            BuildChrome();
            Render();
            _diagnostic.Write(
                "READY",
                true,
                SnapshotHash(_view),
                new ReadyPayload(_buildHash, _fixtureHash));

            if (_options.Smoke)
            {
                CallDeferred(nameof(RunSmoke));
            }
        }
        catch (Exception exception)
        {
            GD.PushError($"Scope 1 startup failed: {exception}");
            ShowFatalError(exception.Message);
            if (_options?.Smoke == true || OS.GetCmdlineUserArgs().Contains("--smoke"))
            {
                GetTree().Quit(1);
            }
        }
    }

    public override void _ExitTree()
    {
        _diagnostic?.Dispose();
        _diagnostic = null;
    }

    private void BuildChrome()
    {
        Theme = new Theme { DefaultFontSize = 15 };

        AddChild(new ColorRect
        {
            Color = BackgroundColor,
            Position = Vector2.Zero,
            Size = new Vector2(1280f, 720f),
            MouseFilter = MouseFilterEnum.Ignore,
        });

        _header = NewLabel(string.Empty, 22, 38f, TextColor);
        _header.Position = new Vector2(18f, 10f);
        _header.Size = new Vector2(1244f, 38f);
        AddChild(_header);

        Panel mapPanel = NewPanel(new Rect2(16f, 58f, 838f, 646f));
        AddChild(mapPanel);
        _mapView = new Scope1PlacementMapView
        {
            Position = new Vector2(8f, 8f),
            Size = new Vector2(822f, 630f),
            MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.Cross,
        };
        _mapView.PointerChanged += OnPointerChanged;
        _mapView.SupportRequested += OnSupportRequested;
        mapPanel.AddChild(_mapView);

        Panel sidePanel = NewPanel(new Rect2(870f, 58f, 394f, 646f));
        AddChild(sidePanel);
        var body = new VBoxContainer
        {
            Position = new Vector2(16f, 14f),
            Size = new Vector2(362f, 618f),
        };
        body.AddThemeConstantOverride("separation", 7);
        sidePanel.AddChild(body);

        body.AddChild(NewLabel("MANUAL LINE / 단일 구간", 13, 24f, MutedColor));
        _phaseLabel = NewLabel(string.Empty, 22, 44f, TextColor);
        body.AddChild(_phaseLabel);
        _instructionLabel = NewLabel(string.Empty, 14, 94f, TextColor);
        body.AddChild(_instructionLabel);
        _pathStatusLabel = NewLabel(string.Empty, 14, 62f, MutedColor);
        body.AddChild(_pathStatusLabel);
        _pointerStatusLabel = NewLabel(string.Empty, 13, 62f, MutedColor);
        _pointerStatusLabel.AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Polite;
        body.AddChild(_pointerStatusLabel);

        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddChild(spacer);

        _undoButton = NewButton(
            "UNDO · 마지막 전신주",
            "마지막으로 배치한 전신주 하나를 되돌립니다.",
            OnUndoPressed);
        body.AddChild(_undoButton);
        _orderButton = NewButton(
            "선로 발주",
            "현재 계획선의 마지막 구간이 허용 거리 안이면 전체 선로 공사를 발주합니다.",
            OnOrderPressed);
        body.AddChild(_orderButton);
        _advanceButton = NewButton(
            "완공까지 진행",
            "공사 완료 시각까지 진행합니다. 완공된 뒤에만 목표가 통전됩니다.",
            OnAdvancePressed);
        body.AddChild(_advanceButton);

        _errorLabel = NewLabel(string.Empty, 13, 50f, ErrorColor);
        _errorLabel.AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Assertive;
        body.AddChild(_errorLabel);

        WireFocusOrder(new Control[] { _mapView, _undoButton, _orderButton, _advanceButton });
    }

    private void Render()
    {
        string phase = PhaseLabel(_view.Phase);
        _header.Text = $"GRIDWORKS  |  수동 선로 건설  |  {phase}  |  {_view.Minute} GameMinute";
        _header.AccessibilityName = _header.Text;
        _phaseLabel.Text = phase;
        _phaseLabel.AccessibilityName = $"현재 상태 {phase}";
        _instructionLabel.Text = InstructionText(_view.Phase);
        _instructionLabel.AccessibilityName = _instructionLabel.Text;

        Scope1PreviewResult targetPreview = _session.PreviewTarget();
        _pathStatusLabel.Text = BuildPathStatus(targetPreview);
        _pathStatusLabel.AccessibilityName = _pathStatusLabel.Text;
        _pointerStatusLabel.Text = BuildPointerStatus(_pointerPreview);
        _pointerStatusLabel.AccessibilityName = _pointerStatusLabel.Text;

        _undoButton.Disabled = _view.Phase != Scope1Phase.Drafting || _view.SupportPositions.Count == 0;
        _undoButton.AccessibilityDescription = _undoButton.Disabled
            ? "되돌릴 수 있는 계획 전신주가 없습니다."
            : "마지막으로 배치한 전신주 하나를 되돌립니다.";
        _orderButton.Disabled = !targetPreview.Accepted;
        _orderButton.AccessibilityDescription = targetPreview.Accepted
            ? "마지막 전신주에서 목표까지의 구간이 허용 거리 안입니다. 전체 선로 공사를 발주합니다."
            : "목표까지의 마지막 구간이 허용 거리 밖이거나 현재 단계에서는 발주할 수 없습니다.";
        _advanceButton.Disabled = _view.Phase != Scope1Phase.Building;
        _advanceButton.AccessibilityDescription = _advanceButton.Disabled
            ? "선로를 발주해 공사 중일 때만 사용할 수 있습니다."
            : $"{_view.CompletionMinute} GameMinute까지 진행하고 선로를 완공합니다.";

        _mapView.SetModel(_fixture, _view, _pointerPreview);

        if (_view.Phase == Scope1Phase.Commissioned && !_finalLogged)
        {
            _diagnostic?.Write("FINAL", true, SnapshotHash(_view), new FinalPayload(_view.TargetEnergized));
            _finalLogged = true;
        }
    }

    private void OnPointerChanged(Scope1Point? point)
    {
        _pointerPreview = point is null ? null : _session.PreviewSpan(point);
        Render();
    }

    private void OnSupportRequested(Scope1Point point)
    {
        Scope1CommandResult result = _session.AddSupport(point);
        _view = result.View;
        _diagnostic?.Write(
            "SUPPORT_ADDED",
            result.Accepted,
            SnapshotHash(_view),
            new SupportPayload(ErrorMachineValue(result.ErrorCode), _view.SupportPositions.Count));

        if (result.Accepted)
        {
            _pointerPreview = null;
            SetError(string.Empty);
        }
        else
        {
            SetError(ErrorText(result.ErrorCode));
        }
        Render();
    }

    private void OnUndoPressed()
    {
        Scope1CommandResult result = _session.UndoSupport();
        _view = result.View;
        _pointerPreview = null;
        SetError(result.Accepted ? string.Empty : ErrorText(result.ErrorCode));
        Render();
    }

    private void OnOrderPressed()
    {
        Scope1CommandResult result = _session.OrderLine();
        _view = result.View;
        _pointerPreview = null;
        _diagnostic?.Write(
            "ORDERED",
            result.Accepted,
            SnapshotHash(_view),
            new OrderPayload(
                ErrorMachineValue(result.ErrorCode),
                _view.CompletionMinute,
                _view.TargetEnergized));
        SetError(result.Accepted ? string.Empty : ErrorText(result.ErrorCode));
        Render();
    }

    private void OnAdvancePressed()
    {
        Scope1CommandResult result = _session.AdvanceToCompletion();
        _view = result.View;
        _pointerPreview = null;
        _diagnostic?.Write(
            "COMPLETED",
            result.Accepted,
            SnapshotHash(_view),
            new CompletionPayload(ErrorMachineValue(result.ErrorCode), _view.TargetEnergized));
        SetError(result.Accepted ? string.Empty : ErrorText(result.ErrorCode));
        Render();
    }

    private async void RunSmoke()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            for (int index = 0; index < _options.SmokeSupports.Count; index++)
            {
                Scope1Point point = _options.SmokeSupports[index];
                Vector2 viewportPoint = _mapView.ViewportPointForGridPoint(point);
                GetViewport().PushInput(new InputEventMouseMotion
                {
                    Position = viewportPoint,
                    GlobalPosition = viewportPoint,
                }, true);
                GetViewport().PushInput(new InputEventMouseButton
                {
                    Position = viewportPoint,
                    GlobalPosition = viewportPoint,
                    ButtonIndex = MouseButton.Left,
                    Pressed = true,
                }, true);
                GetViewport().PushInput(new InputEventMouseButton
                {
                    Position = viewportPoint,
                    GlobalPosition = viewportPoint,
                    ButtonIndex = MouseButton.Left,
                    Pressed = false,
                }, true);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                if (_view.SupportPositions.Count != index + 1 || _view.SupportPositions[^1] != point)
                {
                    throw new InvalidOperationException(
                        $"Smoke map click {index + 1} did not round-trip through inverse snapping.");
                }
            }

            RequireButton(_orderButton, "line order").EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_view.Phase != Scope1Phase.Building || _view.TargetEnergized)
            {
                throw new InvalidOperationException("Building must keep the target de-energized.");
            }

            RequireButton(_advanceButton, "advance to completion").EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_view.Phase != Scope1Phase.Commissioned || !_view.TargetEnergized || !_finalLogged)
            {
                throw new InvalidOperationException("Smoke flow did not commission and energize the target.");
            }

            GD.Print(
                $"SCOPE1_SMOKE_PASS session={_options.SessionId} finalSnapshotHash={SnapshotHash(_view)}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"SCOPE1_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static BaseButton RequireButton(BaseButton? button, string description)
    {
        if (button is null || button.Disabled)
        {
            throw new InvalidOperationException($"Missing enabled UI action for {description}.");
        }
        return button;
    }

    private string BuildPathStatus(Scope1PreviewResult targetPreview)
    {
        string target = _view.TargetEnergized ? "통전" : "무전압";
        string supports = $"전신주 {_view.SupportPositions.Count}개 · TARGET {target}";
        if (_view.Phase == Scope1Phase.Building)
        {
            return $"{supports}\n완공 예정 {_view.CompletionMinute} GameMinute · 공사 중에는 무전압";
        }
        if (_view.Phase == Scope1Phase.Commissioned)
        {
            return $"{supports}\n전체 선로 완공 · 통전 경로 편입 완료";
        }

        string distance = DistanceText(targetPreview);
        return targetPreview.Accepted
            ? $"{supports}\n목표까지 {distance} · 발주 가능"
            : $"{supports}\n목표까지 {distance} · 중간 전신주가 필요합니다";
    }

    private static string BuildPointerStatus(Scope1PreviewResult? preview)
    {
        if (preview is null)
        {
            return "지도 격자점을 가리키면 다음 span의 실제 거리와 허용 거리를 확인할 수 있습니다.";
        }

        string distance = DistanceText(preview);
        if (preview.Accepted)
        {
            return $"{distance} · 배치 가능 · 실선 ghost";
        }
        return preview.ErrorCode switch
        {
            Scope1ErrorCode.SpanTooLong => $"{distance} · 중간 전신주가 필요합니다 · 점선 ghost",
            Scope1ErrorCode.InvalidPosition => "이미 사용 중이거나 지도 밖인 위치입니다 · 점선 ghost",
            Scope1ErrorCode.WrongPhase => "현재 단계에서는 전신주를 추가할 수 없습니다.",
            _ => "현재 위치에 전신주를 배치할 수 없습니다.",
        };
    }

    private static string DistanceText(Scope1PreviewResult preview)
    {
        string actual = Math.Sqrt(preview.DistanceSquared).ToString("0.##", CultureInfo.InvariantCulture);
        string allowed = Math.Sqrt(preview.MaxSpanSquared).ToString("0.##", CultureInfo.InvariantCulture);
        return $"거리 {actual} / 허용 {allowed} GridUnit";
    }

    private static string InstructionText(Scope1Phase phase) => phase switch
    {
        Scope1Phase.Drafting =>
            "SOURCE에서 시작합니다. 범위 원 안의 격자점을 직접 클릭해 전신주를 잇고, TARGET까지 마지막 span이 유효할 때 발주하세요.",
        Scope1Phase.Building =>
            "전체 계획선이 공사 중입니다. 공사 중인 선로와 TARGET은 아직 무전압입니다. 완공까지 진행하세요.",
        Scope1Phase.Commissioned =>
            "전체 선로가 한 번에 완공되어 TARGET이 통전됐습니다. 이 gate는 수동 배치 한 흐름만 검증합니다.",
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };

    private static string PhaseLabel(Scope1Phase phase) => phase switch
    {
        Scope1Phase.Drafting => "DRAFTING · 계획",
        Scope1Phase.Building => "BUILDING · 공사 중",
        Scope1Phase.Commissioned => "COMMISSIONED · 완공",
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };

    private static string? ErrorMachineValue(Scope1ErrorCode? code) => code switch
    {
        null => null,
        Scope1ErrorCode.WrongPhase => "WRONG_PHASE",
        Scope1ErrorCode.InvalidPosition => "INVALID_POSITION",
        Scope1ErrorCode.SpanTooLong => "SPAN_TOO_LONG",
        Scope1ErrorCode.NothingToUndo => "NOTHING_TO_UNDO",
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };

    private static string ErrorText(Scope1ErrorCode? code) => code switch
    {
        Scope1ErrorCode.WrongPhase => "WRONG_PHASE · 현재 단계에서는 이 명령을 실행할 수 없습니다.",
        Scope1ErrorCode.InvalidPosition => "INVALID_POSITION · 그 위치에는 전신주를 놓을 수 없습니다.",
        Scope1ErrorCode.SpanTooLong => "SPAN_TOO_LONG · 중간 전신주가 필요합니다.",
        Scope1ErrorCode.NothingToUndo => "NOTHING_TO_UNDO · 되돌릴 전신주가 없습니다.",
        null => string.Empty,
        _ => "알 수 없는 명령 오류입니다.",
    };

    private void SetError(string text)
    {
        _errorLabel.Text = text;
        _errorLabel.AccessibilityName = text;
    }

    private static string SnapshotHash(Scope1View view) => Scope1ViewJson.Sha256Hex(view);

    private static string ComputeBuildHash()
    {
        string gameDirectory = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        string repositoryRoot = new DirectoryInfo(gameDirectory).Parent?.FullName
            ?? throw new InvalidOperationException("Game directory has no repository parent.");
        string coreDirectory = Path.Combine(repositoryRoot, "src", "Gridworks.Core");
        var components = new List<string>
        {
            Path.Combine(repositoryRoot, "Directory.Build.props"),
            Path.Combine(repositoryRoot, "global.json"),
            Path.Combine(coreDirectory, "Gridworks.Core.csproj"),
            Path.Combine(gameDirectory, "Gridworks.Game.csproj"),
            Path.Combine(gameDirectory, "project.godot"),
        };
        components.AddRange(Directory.EnumerateFiles(coreDirectory, "*.cs"));
        components.AddRange(Directory.EnumerateFiles(gameDirectory, "*.cs"));
        components.AddRange(Directory.EnumerateFiles(gameDirectory, "*.tscn"));

        var manifest = new StringBuilder();
        foreach (string path in components
                     .Select(Path.GetFullPath)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(path => Path.GetRelativePath(repositoryRoot, path), StringComparer.Ordinal))
        {
            string label = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Build-hash component '{label}' was not found.", path);
            }
            manifest.Append(label)
                .Append(':')
                .Append(LowerHex(SHA256.HashData(File.ReadAllBytes(path))))
                .Append('\n');
        }
        return LowerHex(SHA256.HashData(Encoding.UTF8.GetBytes(manifest.ToString())));
    }

    private static string LowerHex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    private static Panel NewPanel(Rect2 rect)
    {
        var style = new StyleBoxFlat { BgColor = PanelColor, BorderColor = PanelBorderColor };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(3);
        var panel = new Panel
        {
            Position = rect.Position,
            Size = rect.Size,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static Label NewLabel(string text, int fontSize, float minimumHeight, Color color)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(0f, minimumHeight),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            AccessibilityName = text,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button NewButton(string text, string description, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            AccessibilityName = text,
            AccessibilityDescription = description,
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(0f, 42f),
        };
        button.Pressed += pressed;
        return button;
    }

    private static void WireFocusOrder(IReadOnlyList<Control> controls)
    {
        for (int index = 0; index < controls.Count; index++)
        {
            Control current = controls[index];
            Control previous = controls[(index + controls.Count - 1) % controls.Count];
            Control next = controls[(index + 1) % controls.Count];
            NodePath previousPath = current.GetPathTo(previous);
            NodePath nextPath = current.GetPathTo(next);
            current.FocusPrevious = previousPath;
            current.FocusNext = nextPath;
            current.FocusNeighborTop = previousPath;
            current.FocusNeighborLeft = previousPath;
            current.FocusNeighborBottom = nextPath;
            current.FocusNeighborRight = nextPath;
        }
    }

    private void ShowFatalError(string message)
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var background = new ColorRect
        {
            Color = BackgroundColor,
            Position = Vector2.Zero,
            Size = new Vector2(1280f, 720f),
        };
        AddChild(background);
        Label label = NewLabel($"FIXTURE_INVALID\n\n{message}", 22, 300f, ErrorColor);
        label.Position = new Vector2(140f, 180f);
        label.Size = new Vector2(1000f, 300f);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        background.AddChild(label);
    }

    private sealed class Scope1DiagnosticWriter : IDisposable
    {
        private const string SchemaVersion = "gridworks.scope1.diagnostic.v1";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = null,
            WriteIndented = false,
        };

        private readonly StreamWriter _writer;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly string _sessionId;
        private long _sequence;

        public Scope1DiagnosticWriter(string path, string sessionId)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _writer = new StreamWriter(
                new FileStream(path, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read));
            _sessionId = sessionId;
        }

        public void Write(string eventName, bool accepted, string snapshotHash, object payload)
        {
            var row = new DiagnosticRow(
                SchemaVersion,
                checked(++_sequence),
                _clock.ElapsedMilliseconds,
                _sessionId,
                eventName,
                accepted,
                snapshotHash,
                payload);
            _writer.WriteLine(JsonSerializer.Serialize(row, JsonOptions));
            _writer.Flush();
        }

        public void Dispose()
        {
            _writer.Dispose();
            _clock.Stop();
        }

        private sealed record DiagnosticRow(
            [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
            [property: JsonPropertyName("sequence")] long Sequence,
            [property: JsonPropertyName("elapsedMs")] long ElapsedMs,
            [property: JsonPropertyName("sessionId")] string SessionId,
            [property: JsonPropertyName("event")] string Event,
            [property: JsonPropertyName("accepted")] bool Accepted,
            [property: JsonPropertyName("snapshotHash")] string SnapshotHash,
            [property: JsonPropertyName("payload")] object Payload);
    }

    private sealed record Scope1LaunchOptions(
        string SessionId,
        string DiagnosticPath,
        bool Smoke,
        IReadOnlyList<Scope1Point> SmokeSupports)
    {
        public static Scope1LaunchOptions Parse(IReadOnlyList<string> arguments)
        {
            string? sessionId = null;
            string? diagnosticPath = null;
            bool smoke = false;
            var smokeSupports = new List<Scope1Point>();

            for (int index = 0; index < arguments.Count; index++)
            {
                switch (arguments[index])
                {
                    case "--session-id":
                        sessionId = RequiredValue(arguments, ref index, "--session-id");
                        break;
                    case "--diagnostic-log":
                        diagnosticPath = RequiredValue(arguments, ref index, "--diagnostic-log");
                        break;
                    case "--smoke":
                        smoke = true;
                        break;
                    case "--smoke-support":
                        smokeSupports.Add(ParsePoint(RequiredValue(arguments, ref index, "--smoke-support")));
                        break;
                    default:
                        throw new ArgumentException($"Unknown Scope 1 game argument: {arguments[index]}");
                }
            }

            sessionId ??= "LOCAL-S1";
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("--session-id cannot be empty.");
            }
            if (smoke && smokeSupports.Count != 2)
            {
                throw new ArgumentException("--smoke requires exactly two --smoke-support x,y values.");
            }
            if (!smoke && smokeSupports.Count != 0)
            {
                throw new ArgumentException("--smoke-support is valid only with --smoke.");
            }

            diagnosticPath ??= ProjectSettings.GlobalizePath(
                $"user://scope-1-local-{System.Environment.ProcessId}.jsonl");
            return new Scope1LaunchOptions(
                sessionId,
                Path.GetFullPath(diagnosticPath),
                smoke,
                smokeSupports.AsReadOnly());
        }

        private static Scope1Point ParsePoint(string value)
        {
            string[] fields = value.Split(',', StringSplitOptions.None);
            if (fields.Length != 2 ||
                !int.TryParse(fields[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int x) ||
                !int.TryParse(fields[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int y))
            {
                throw new ArgumentException("--smoke-support must be exactly x,y using two integers.");
            }
            return new Scope1Point(x, y);
        }

        private static string RequiredValue(IReadOnlyList<string> arguments, ref int index, string option)
        {
            if (index + 1 >= arguments.Count ||
                arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"{option} requires a value.");
            }
            index++;
            return arguments[index];
        }
    }

    private sealed record ReadyPayload(
        [property: JsonPropertyName("buildHash")] string BuildHash,
        [property: JsonPropertyName("fixtureHash")] string FixtureHash);

    private sealed record SupportPayload(
        [property: JsonPropertyName("errorCode")] string? ErrorCode,
        [property: JsonPropertyName("supportCount")] int SupportCount);

    private sealed record OrderPayload(
        [property: JsonPropertyName("errorCode")] string? ErrorCode,
        [property: JsonPropertyName("completionMinute")] int? CompletionMinute,
        [property: JsonPropertyName("targetEnergized")] bool TargetEnergized);

    private sealed record CompletionPayload(
        [property: JsonPropertyName("errorCode")] string? ErrorCode,
        [property: JsonPropertyName("targetEnergized")] bool TargetEnergized);

    private sealed record FinalPayload(
        [property: JsonPropertyName("targetEnergized")] bool TargetEnergized);
}
