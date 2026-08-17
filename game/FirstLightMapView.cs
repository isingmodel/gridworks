using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Gridworks.Game;

internal readonly record struct FirstLightGridPoint(int X, int Y);

internal readonly record struct FirstLightGridBounds(int MinX, int MaxX, int MinY, int MaxY);

internal enum FirstLightProjectVisualState
{
    NotOrdered,
    Building,
    Commissioned,
    Unavailable,
}

internal enum FirstLightLineKind
{
    Town,
    HospitalPrimary,
    HospitalBackup,
}

internal enum FirstLightPointerMode
{
    Substation,
    LineSupport,
}

internal sealed record FirstLightPointerPreview(
    FirstLightPointerMode Mode,
    FirstLightGridPoint Point,
    FirstLightGridPoint? From,
    bool Accepted,
    string Description);

internal sealed record FirstLightTargetPreview(
    FirstLightGridPoint From,
    FirstLightGridPoint Target,
    bool Accepted);

internal sealed record FirstLightRiskRect(
    FirstLightGridPoint Minimum,
    FirstLightGridPoint Maximum,
    bool IncidentActive);

internal sealed record FirstLightLineVisual(
    FirstLightLineKind Kind,
    string Label,
    FirstLightGridPoint Target,
    IReadOnlyList<FirstLightGridPoint> Supports,
    FirstLightProjectVisualState State,
    bool IsActiveProject);

internal sealed record FirstLightMapModel(
    FirstLightGridBounds Bounds,
    IReadOnlyList<FirstLightGridPoint> BlockedCells,
    FirstLightGridPoint Source,
    FirstLightGridPoint Town,
    long TownDeliveredKw,
    FirstLightGridPoint Hospital,
    long HospitalUtilityKw,
    long HospitalP0DeliveredKw,
    FirstLightRiskRect RiskRect,
    FirstLightGridPoint? Substation,
    int ServiceRadius,
    FirstLightProjectVisualState SubstationState,
    IReadOnlyList<FirstLightLineVisual> Lines,
    FirstLightPointerPreview? PointerPreview,
    FirstLightTargetPreview? TargetPreview,
    string PhaseDescription,
    string SupplyDescription);

internal sealed partial class FirstLightMapView : Container
{
    private static readonly Color MapBackground = Color.FromHtml("0a151d");
    private static readonly Color GridColor = Color.FromHtml("20313b");
    private static readonly Color CoordinateColor = Color.FromHtml("778b96");
    private static readonly Color TextColor = Color.FromHtml("e6eef2");
    private static readonly Color MutedColor = Color.FromHtml("9babb4");
    private static readonly Color PlannedColor = Color.FromHtml("d89a4a");
    private static readonly Color BuildingColor = Color.FromHtml("b97936");
    private static readonly Color EnergizedColor = Color.FromHtml("78c8cf");
    private static readonly Color InvalidColor = Color.FromHtml("e66d66");
    private static readonly Color SourceColor = Color.FromHtml("4d98a4");
    private static readonly Color ServiceStroke = Color.FromHtml("7897a8");
    private static readonly Color ServiceFill = new(0.35f, 0.58f, 0.72f, 0.12f);
    private static readonly Color BlockedFill = Color.FromHtml("25313a");
    private static readonly Color FocusColor = Color.FromHtml("f0c66d");
    private static readonly Color PrimaryColor = Color.FromHtml("d6a7e8");
    private static readonly Color BackupColor = Color.FromHtml("91d58b");
    private static readonly Color RiskColor = Color.FromHtml("d85d62");

    private FirstLightMapModel? _model;
    private FirstLightGridPoint? _keyboardPoint;

    public event Action<FirstLightGridPoint?>? PointerChanged;

    public event Action<FirstLightGridPoint>? PointRequested;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        AccessibilityRegion = true;
        MouseExited += OnMouseExited;
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
        AccessibilityDescription =
            "전력망 지도. 마우스 왼쪽 클릭으로 격자점을 선택합니다. 키보드로는 화살표로 커서를 움직이고 Enter 또는 Space로 선택합니다.";
    }

    public void SetModel(FirstLightMapModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _keyboardPoint ??= new FirstLightGridPoint(model.Bounds.MinX, model.Bounds.MinY);
        _keyboardPoint = Clamp(_keyboardPoint.Value, model.Bounds);
        AccessibilityName = BuildAccessibilitySummary(model, _keyboardPoint);
        QueueRedraw();
    }

    public Vector2 ViewportPointForGridPoint(FirstLightGridPoint point)
    {
        FirstLightMapModel model = _model
            ?? throw new InvalidOperationException("Map model is not ready.");
        Vector2 localPoint = ToCanvas(point, model.Bounds, PlotRect(model.Bounds));
        return GetGlobalTransformWithCanvas() * localPoint;
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (_model is null)
        {
            return;
        }

        switch (inputEvent)
        {
            case InputEventMouseMotion motion:
                PointerChanged?.Invoke(
                    TrySnap(motion.Position, _model.Bounds, out FirstLightGridPoint hover)
                        ? hover
                        : null);
                AcceptEvent();
                return;

            case InputEventMouseButton button when
                button.ButtonIndex == MouseButton.Left &&
                button.Pressed &&
                TrySnap(button.Position, _model.Bounds, out FirstLightGridPoint requested):
                GrabFocus();
                _keyboardPoint = requested;
                PointerChanged?.Invoke(requested);
                PointRequested?.Invoke(requested);
                AcceptEvent();
                return;

            case InputEventKey key when key.Pressed && !key.Echo:
                HandleKeyboardInput(key, _model.Bounds);
                return;
        }
    }

    public override void _Draw()
    {
        if (_model is null)
        {
            return;
        }

        Rect2 plot = PlotRect(_model.Bounds);
        DrawRect(new Rect2(Vector2.Zero, Size), MapBackground);
        DrawGrid(_model.Bounds, plot);
        DrawBlockedCells(_model, plot);
        DrawRiskRect(_model, plot);
        DrawServiceArea(_model, plot);
        DrawLineProjects(_model, plot);
        DrawSource(_model, plot);
        DrawTown(_model, plot);
        DrawHospital(_model, plot);
        DrawSubstation(_model, plot);
        DrawSupports(_model, plot);
        DrawPointerPreview(_model, plot);
        DrawKeyboardCursor(_model, plot);
        DrawLegend(_model);

        if (HasFocus())
        {
            DrawRect(new Rect2(Vector2.One * 2f, Size - (Vector2.One * 4f)), FocusColor, false, 2f);
        }
    }

    private void HandleKeyboardInput(InputEventKey key, FirstLightGridBounds bounds)
    {
        FirstLightGridPoint current = _keyboardPoint
            ?? new FirstLightGridPoint(bounds.MinX, bounds.MinY);
        FirstLightGridPoint next = key.Keycode switch
        {
            Key.Left => current with { X = current.X - 1 },
            Key.Right => current with { X = current.X + 1 },
            Key.Up => current with { Y = current.Y - 1 },
            Key.Down => current with { Y = current.Y + 1 },
            _ => current,
        };

        if (key.Keycode is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            _keyboardPoint = Clamp(next, bounds);
            PointerChanged?.Invoke(_keyboardPoint);
            if (_model is not null)
            {
                AccessibilityName = BuildAccessibilitySummary(_model, _keyboardPoint);
            }
            QueueRedraw();
            AcceptEvent();
            return;
        }

        if (key.Keycode is Key.Enter or Key.KpEnter or Key.Space)
        {
            _keyboardPoint = Clamp(current, bounds);
            PointerChanged?.Invoke(_keyboardPoint);
            PointRequested?.Invoke(_keyboardPoint.Value);
            AcceptEvent();
        }
    }

    private void DrawGrid(FirstLightGridBounds bounds, Rect2 plot)
    {
        for (int x = bounds.MinX; x <= bounds.MaxX; x++)
        {
            Vector2 top = ToCanvas(new FirstLightGridPoint(x, bounds.MinY), bounds, plot);
            Vector2 bottom = ToCanvas(new FirstLightGridPoint(x, bounds.MaxY), bounds, plot);
            DrawLine(top, bottom, GridColor, 1f);
            DrawString(
                ThemeDB.FallbackFont,
                bottom + new Vector2(-5f, 19f),
                x.ToString(CultureInfo.InvariantCulture),
                HorizontalAlignment.Left,
                -1f,
                11,
                CoordinateColor);
        }

        for (int y = bounds.MinY; y <= bounds.MaxY; y++)
        {
            Vector2 left = ToCanvas(new FirstLightGridPoint(bounds.MinX, y), bounds, plot);
            Vector2 right = ToCanvas(new FirstLightGridPoint(bounds.MaxX, y), bounds, plot);
            DrawLine(left, right, GridColor, 1f);
            DrawString(
                ThemeDB.FallbackFont,
                left + new Vector2(-26f, 4f),
                y.ToString(CultureInfo.InvariantCulture),
                HorizontalAlignment.Right,
                20f,
                11,
                CoordinateColor);
        }
    }

    private void DrawBlockedCells(FirstLightMapModel model, Rect2 plot)
    {
        float scale = GridScale(model.Bounds, plot);
        float size = Math.Clamp(scale * 0.62f, 12f, 28f);
        foreach (FirstLightGridPoint blocked in model.BlockedCells)
        {
            Vector2 center = ToCanvas(blocked, model.Bounds, plot);
            var rect = new Rect2(center - new Vector2(size / 2f, size / 2f), new Vector2(size, size));
            DrawRect(rect, BlockedFill);
            DrawLine(rect.Position, rect.End, MutedColor, 2f);
            DrawLine(
                new Vector2(rect.Position.X, rect.End.Y),
                new Vector2(rect.End.X, rect.Position.Y),
                MutedColor,
                2f);
        }
    }

    private void DrawServiceArea(FirstLightMapModel model, Rect2 plot)
    {
        FirstLightGridPoint? centerPoint = model.Substation;
        if (model.PointerPreview?.Mode == FirstLightPointerMode.Substation)
        {
            centerPoint = model.PointerPreview.Point;
        }
        if (centerPoint is null)
        {
            return;
        }

        Vector2 center = ToCanvas(centerPoint.Value, model.Bounds, plot);
        float radius = model.ServiceRadius * GridScale(model.Bounds, plot);
        DrawCircle(center, radius, ServiceFill);
        DrawDashedLine(
            center + new Vector2(-radius, 0f),
            center + new Vector2(radius, 0f),
            new Color(ServiceStroke, 0.45f),
            1f,
            8f,
            true,
            true);
        DrawArc(center, radius, 0f, Mathf.Tau, 96, ServiceStroke, 2f, true);
        DrawString(
            ThemeDB.FallbackFont,
            center + new Vector2(-radius, -radius - 8f),
            "서비스 권역 · 접속 가능 범위",
            HorizontalAlignment.Left,
            -1f,
            12,
            ServiceStroke);
    }

    private void DrawRiskRect(FirstLightMapModel model, Rect2 plot)
    {
        Vector2 minimum = ToCanvas(model.RiskRect.Minimum, model.Bounds, plot);
        Vector2 maximum = ToCanvas(model.RiskRect.Maximum, model.Bounds, plot);
        var rect = new Rect2(minimum, maximum - minimum);
        Color fill = model.RiskRect.IncidentActive
            ? new Color(RiskColor, 0.2f)
            : new Color(RiskColor, 0.08f);
        DrawRect(rect, fill);
        DrawDashedLine(rect.Position, new Vector2(rect.End.X, rect.Position.Y), RiskColor, 2f, 8f);
        DrawDashedLine(new Vector2(rect.End.X, rect.Position.Y), rect.End, RiskColor, 2f, 8f);
        DrawDashedLine(rect.End, new Vector2(rect.Position.X, rect.End.Y), RiskColor, 2f, 8f);
        DrawDashedLine(new Vector2(rect.Position.X, rect.End.Y), rect.Position, RiskColor, 2f, 8f);
        if (model.RiskRect.IncidentActive)
        {
            for (float x = rect.Position.X - rect.Size.Y; x < rect.End.X; x += 18f)
            {
                Vector2 from = new(Math.Max(rect.Position.X, x), rect.End.Y);
                Vector2 to = new(Math.Min(rect.End.X, x + rect.Size.Y), rect.Position.Y);
                DrawLine(from, to, new Color(RiskColor, 0.35f), 1.5f);
            }
        }
        DrawString(
            ThemeDB.FallbackFont,
            rect.Position + new Vector2(6f, -7f),
            model.RiskRect.IncidentActive
                ? "공간사건 활성 · 닿는 회선 사용불가"
                : "공간 위험구역 · 경계 포함",
            HorizontalAlignment.Left,
            -1f,
            12,
            RiskColor);
    }

    private void DrawLineProjects(FirstLightMapModel model, Rect2 plot)
    {
        foreach (FirstLightLineVisual line in model.Lines)
        {
            var points = new List<FirstLightGridPoint> { model.Source };
            points.AddRange(line.Supports);
            if (line.State != FirstLightProjectVisualState.NotOrdered)
            {
                points.Add(line.Target);
            }

            for (int index = 0; index < points.Count - 1; index++)
            {
                Vector2 from = ToCanvas(points[index], model.Bounds, plot);
                Vector2 to = ToCanvas(points[index + 1], model.Bounds, plot);
                DrawProjectSpan(from, to, line.State, line.Kind);
            }

            if (points.Count > 1)
            {
                Vector2 labelPoint = ToCanvas(points[1], model.Bounds, plot);
                Vector2 offset = line.Kind switch
                {
                    FirstLightLineKind.Town => new Vector2(8f, 19f),
                    FirstLightLineKind.HospitalPrimary => new Vector2(8f, -13f),
                    FirstLightLineKind.HospitalBackup => new Vector2(8f, 20f),
                    _ => Vector2.Zero,
                };
                DrawString(
                    ThemeDB.FallbackFont,
                    labelPoint + offset,
                    line.Label,
                    HorizontalAlignment.Left,
                    -1f,
                    11,
                    line.State == FirstLightProjectVisualState.Unavailable
                        ? InvalidColor
                        : LineColor(line.Kind));
            }

            if (line.IsActiveProject &&
                line.State == FirstLightProjectVisualState.NotOrdered &&
                model.TargetPreview is not null)
            {
                Vector2 from = ToCanvas(model.TargetPreview.From, model.Bounds, plot);
                Vector2 to = ToCanvas(model.TargetPreview.Target, model.Bounds, plot);
                Color color = model.TargetPreview.Accepted ? PlannedColor : InvalidColor;
                DrawDashedLine(from, to, color, 2.5f, 8f, true, true);
            }
        }
    }

    private void DrawProjectSpan(
        Vector2 from,
        Vector2 to,
        FirstLightProjectVisualState state,
        FirstLightLineKind kind)
    {
        switch (state)
        {
            case FirstLightProjectVisualState.NotOrdered:
                DrawDashedLine(from, to, PlannedColor, 3f, 8f, true, true);
                break;
            case FirstLightProjectVisualState.Building:
                DrawDashedLine(from, to, BuildingColor, 5f, 10f, true, true);
                DrawConstructionHatching(from, to);
                break;
            case FirstLightProjectVisualState.Commissioned:
                DrawLine(from, to, Color.FromHtml("102b31"), 9f, true);
                DrawLine(from, to, LineColor(kind), 5f, true);
                break;
            case FirstLightProjectVisualState.Unavailable:
                DrawLine(from, to, Color.FromHtml("2b171a"), 9f, true);
                DrawDashedLine(from, to, InvalidColor, 5f, 12f, true, true);
                DrawCross((from + to) / 2f, InvalidColor, 8f);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state));
        }
    }

    private void DrawSource(FirstLightMapModel model, Rect2 plot)
    {
        Vector2 center = ToCanvas(model.Source, model.Bounds, plot);
        DrawCircle(center, 14f, SourceColor);
        DrawCircle(center, 14f, TextColor, false, 2f, true);
        DrawString(
            ThemeDB.FallbackFont,
            center + new Vector2(18f, -17f),
            "기존 발전원 · 온라인",
            HorizontalAlignment.Left,
            -1f,
            13,
            TextColor);
    }

    private void DrawTown(FirstLightMapModel model, Rect2 plot)
    {
        Vector2 center = ToCanvas(model.Town, model.Bounds, plot);
        Color color = model.TownDeliveredKw > 0 ? EnergizedColor : MutedColor;
        var building = new Rect2(center - new Vector2(13f, 11f), new Vector2(26f, 22f));
        DrawRect(building, model.TownDeliveredKw > 0 ? new Color(color, 0.62f) : MapBackground);
        DrawRect(building, color, false, 3f);
        if (model.TownDeliveredKw == 0)
        {
            DrawCross(center, color, 7f);
        }
        DrawString(
            ThemeDB.FallbackFont,
            center + new Vector2(-132f, -18f),
            model.TownDeliveredKw > 0 ? "마을 · 공급 중" : "마을 · 미공급",
            HorizontalAlignment.Right,
            114f,
            13,
            color);
    }

    private void DrawHospital(FirstLightMapModel model, Rect2 plot)
    {
        Vector2 center = ToCanvas(model.Hospital, model.Bounds, plot);
        Color utilityColor = model.HospitalUtilityKw > 0 ? EnergizedColor : MutedColor;
        var building = new Rect2(center - new Vector2(16f, 14f), new Vector2(32f, 28f));
        DrawRect(building, new Color(utilityColor, 0.22f));
        DrawRect(building, utilityColor, false, 3f);
        DrawLine(center + new Vector2(-8f, 0f), center + new Vector2(8f, 0f), TextColor, 4f);
        DrawLine(center + new Vector2(0f, -8f), center + new Vector2(0f, 8f), TextColor, 4f);
        string utility = model.HospitalUtilityKw > 0 ? "utility 공급" : "utility 미공급";
        string p0 = model.HospitalP0DeliveredKw > 0 ? "P0 유지" : "P0 미공급";
        DrawString(
            ThemeDB.FallbackFont,
            center + new Vector2(-170f, -21f),
            $"병원 · {utility} · {p0}",
            HorizontalAlignment.Right,
            148f,
            12,
            utilityColor);
    }

    private void DrawSubstation(FirstLightMapModel model, Rect2 plot)
    {
        if (!model.Substation.HasValue)
        {
            return;
        }

        Vector2 center = ToCanvas(model.Substation.Value, model.Bounds, plot);
        Color color = model.SubstationState switch
        {
            FirstLightProjectVisualState.NotOrdered => PlannedColor,
            FirstLightProjectVisualState.Building => BuildingColor,
            FirstLightProjectVisualState.Commissioned => EnergizedColor,
            _ => throw new ArgumentOutOfRangeException(nameof(model.SubstationState)),
        };
        Vector2[] diamond =
        [
            center + new Vector2(0f, -15f),
            center + new Vector2(15f, 0f),
            center + new Vector2(0f, 15f),
            center + new Vector2(-15f, 0f),
        ];
        DrawColoredPolygon(diamond, new Color(color, 0.42f));
        for (int index = 0; index < diamond.Length; index++)
        {
            DrawLine(diamond[index], diamond[(index + 1) % diamond.Length], color, 3f, true);
        }
        if (model.SubstationState == FirstLightProjectVisualState.Building)
        {
            DrawConstructionHatching(center + new Vector2(-12f, 0f), center + new Vector2(12f, 0f));
        }
        DrawString(
            ThemeDB.FallbackFont,
            center + new Vector2(18f, 19f),
            SubstationStateText(model.SubstationState),
            HorizontalAlignment.Left,
            -1f,
            12,
            color);
    }

    private void DrawSupports(FirstLightMapModel model, Rect2 plot)
    {
        foreach (FirstLightLineVisual line in model.Lines)
        {
            Color color = line.State switch
            {
                FirstLightProjectVisualState.NotOrdered => PlannedColor,
                FirstLightProjectVisualState.Building => BuildingColor,
                FirstLightProjectVisualState.Commissioned => LineColor(line.Kind),
                FirstLightProjectVisualState.Unavailable => InvalidColor,
                _ => throw new ArgumentOutOfRangeException(nameof(line.State)),
            };
            foreach (FirstLightGridPoint support in line.Supports)
            {
                Vector2 point = ToCanvas(support, model.Bounds, plot);
                DrawLine(point + new Vector2(0f, -12f), point + new Vector2(0f, 12f), color, 4f, true);
                DrawLine(point + new Vector2(-8f, -7f), point + new Vector2(8f, -7f), color, 3f, true);
                DrawCircle(point, 5f, MapBackground);
                DrawCircle(point, 5f, color, false, 2f, true);
            }
        }
    }

    private void DrawPointerPreview(FirstLightMapModel model, Rect2 plot)
    {
        FirstLightPointerPreview? preview = model.PointerPreview;
        if (preview is null)
        {
            return;
        }

        Vector2 point = ToCanvas(preview.Point, model.Bounds, plot);
        Color color = preview.Accepted ? PlannedColor : InvalidColor;
        if (preview.Mode == FirstLightPointerMode.Substation)
        {
            DrawCircle(point, 11f, new Color(color, 0.35f));
            DrawCircle(point, 11f, color, false, 2f, true);
            if (!preview.Accepted)
            {
                DrawCross(point, color, 7f);
            }
            return;
        }

        if (preview.From.HasValue)
        {
            Vector2 from = ToCanvas(preview.From.Value, model.Bounds, plot);
            if (preview.Accepted)
            {
                DrawLine(from, point, PlannedColor, 3f, true);
                DrawCircle(point, 7f, PlannedColor, false, 2f, true);
            }
            else
            {
                DrawDashedLine(from, point, InvalidColor, 3f, 8f, true, true);
                DrawCross(point, InvalidColor, 7f);
            }
        }
    }

    private void DrawKeyboardCursor(FirstLightMapModel model, Rect2 plot)
    {
        if (!HasFocus() || !_keyboardPoint.HasValue)
        {
            return;
        }
        Vector2 center = ToCanvas(_keyboardPoint.Value, model.Bounds, plot);
        DrawRect(new Rect2(center - new Vector2(10f, 10f), new Vector2(20f, 20f)), FocusColor, false, 2f);
    }

    private void DrawLegend(FirstLightMapModel model)
    {
        string text =
            $"{model.PhaseDescription}  ·  ┄ 계획  ·  ╱╱ 공사  ·  ━ 통전  ·  --× 사용불가  ·  ◌ 위험구역";
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(18f, Size.Y - 14f),
            text,
            HorizontalAlignment.Left,
            Math.Max(0f, Size.X - 36f),
            12,
            TextColor);
    }

    private bool TrySnap(
        Vector2 localPosition,
        FirstLightGridBounds bounds,
        out FirstLightGridPoint point)
    {
        Rect2 plot = PlotRect(bounds);
        Vector2 end = plot.Position + plot.Size;
        if (localPosition.X < plot.Position.X || localPosition.X > end.X ||
            localPosition.Y < plot.Position.Y || localPosition.Y > end.Y)
        {
            point = default;
            return false;
        }

        float scale = GridScale(bounds, plot);
        float gridX = bounds.MinX + ((localPosition.X - plot.Position.X) / scale);
        float gridY = bounds.MinY + ((localPosition.Y - plot.Position.Y) / scale);
        int snappedX = checked((int)MathF.Floor(gridX + 0.5f));
        int snappedY = checked((int)MathF.Floor(gridY + 0.5f));
        point = Clamp(new FirstLightGridPoint(snappedX, snappedY), bounds);
        return true;
    }

    private static Vector2 ToCanvas(
        FirstLightGridPoint point,
        FirstLightGridBounds bounds,
        Rect2 plot)
    {
        float scale = GridScale(bounds, plot);
        return new Vector2(
            plot.Position.X + ((point.X - bounds.MinX) * scale),
            plot.Position.Y + ((point.Y - bounds.MinY) * scale));
    }

    private Rect2 PlotRect(FirstLightGridBounds bounds)
    {
        float spanX = bounds.MaxX - bounds.MinX;
        float spanY = bounds.MaxY - bounds.MinY;
        float availableWidth = Math.Max(1f, Size.X - 92f);
        float availableHeight = Math.Max(1f, Size.Y - 100f);
        float scale = Math.Min(availableWidth / spanX, availableHeight / spanY);
        var plotSize = new Vector2(spanX * scale, spanY * scale);
        return new Rect2((Size - plotSize) / 2f + new Vector2(0f, -4f), plotSize);
    }

    private static float GridScale(FirstLightGridBounds bounds, Rect2 plot)
    {
        float spanX = bounds.MaxX - bounds.MinX;
        return plot.Size.X / spanX;
    }

    private static FirstLightGridPoint Clamp(FirstLightGridPoint point, FirstLightGridBounds bounds) => new(
        Math.Clamp(point.X, bounds.MinX, bounds.MaxX),
        Math.Clamp(point.Y, bounds.MinY, bounds.MaxY));

    private static string BuildAccessibilitySummary(
        FirstLightMapModel model,
        FirstLightGridPoint? keyboardPoint)
    {
        string substation = model.Substation.HasValue
            ? $"{SubstationStateText(model.SubstationState)}."
            : "변전소 초안 없음.";
        string line = string.Join(
            " ",
            model.Lines.Select(item =>
                $"{item.Label} {LineStateText(item.State)}, 지지물 {item.Supports.Count}개."));
        string cursor = keyboardPoint.HasValue
            ? $" 키보드 커서 {keyboardPoint.Value.X}, {keyboardPoint.Value.Y}."
            : string.Empty;
        string incident = model.RiskRect.IncidentActive
            ? " 공간사건 활성."
            : " 공간 위험구역 표시됨.";
        return $"전력망 지도. {model.PhaseDescription}. {substation} {line} {model.SupplyDescription}.{incident}{cursor}";
    }

    private static string LineStateText(FirstLightProjectVisualState state) => state switch
    {
        FirstLightProjectVisualState.NotOrdered => "계획",
        FirstLightProjectVisualState.Building => "공사 중",
        FirstLightProjectVisualState.Commissioned => "완공",
        FirstLightProjectVisualState.Unavailable => "사건으로 사용불가",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static Color LineColor(FirstLightLineKind kind) => kind switch
    {
        FirstLightLineKind.Town => EnergizedColor,
        FirstLightLineKind.HospitalPrimary => PrimaryColor,
        FirstLightLineKind.HospitalBackup => BackupColor,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string SubstationStateText(FirstLightProjectVisualState state) => state switch
    {
        FirstLightProjectVisualState.NotOrdered => "변전소 · 계획",
        FirstLightProjectVisualState.Building => "변전소 · 공사 중",
        FirstLightProjectVisualState.Commissioned => "변전소 · 완공",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private void OnMouseExited() => PointerChanged?.Invoke(null);

    private void DrawConstructionHatching(Vector2 from, Vector2 to)
    {
        Vector2 segment = to - from;
        float length = segment.Length();
        if (length <= 0f)
        {
            return;
        }
        Vector2 direction = segment / length;
        Vector2 normal = new(-direction.Y, direction.X);
        Vector2 slash = (direction + normal).Normalized() * 6f;
        float spacing = 16f;
        float start = Math.Min(spacing / 2f, length / 2f);
        for (float distance = start; distance < length; distance += spacing)
        {
            Vector2 center = from + (direction * distance);
            DrawLine(center - slash, center + slash, BuildingColor, 3f, true);
        }
    }

    private void DrawCross(Vector2 center, Color color, float radius)
    {
        DrawLine(center - new Vector2(radius, radius), center + new Vector2(radius, radius), color, 2f, true);
        DrawLine(center + new Vector2(-radius, radius), center + new Vector2(radius, -radius), color, 2f, true);
    }
}
