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
    PlantConnection,
}

internal enum FirstLightPointerMode
{
    Substation,
    LineSupport,
    GasPlant,
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
    bool IsActiveProject,
    FirstLightGridPoint? From = null);

internal sealed record FirstLightPlantSiteVisual(
    string SiteId,
    FirstLightGridPoint Position,
    bool Selected);

internal enum FirstLightFactoryFeederVisualState
{
    Normal,
    Maintenance,
    Maintained,
    Unavailable,
}

internal sealed record FirstLightFactoryVisual(
    FirstLightGridPoint Position,
    FirstLightGridPoint FeederFrom,
    long DeliveredKw,
    FirstLightFactoryFeederVisualState FeederState =
        FirstLightFactoryFeederVisualState.Normal,
    long EffectiveRatingKw = 0);

internal sealed record FirstLightGasPlantVisual(
    FirstLightGridPoint Position,
    FirstLightProjectVisualState State,
    bool Connected,
    long DispatchedKw);

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
    string SupplyDescription,
    IReadOnlyList<FirstLightPlantSiteVisual>? PlantSites = null,
    FirstLightFactoryVisual? Factory = null,
    FirstLightGasPlantVisual? GasPlant = null,
    bool FactoryStageActive = false);

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
    private static readonly Color SiteColor = Color.FromHtml("8e9ba3");
    private static readonly Color FactoryColor = Color.FromHtml("c5a36a");
    private static readonly Color RiverColor = Color.FromHtml("18303b");
    private static readonly Color RoadColor = Color.FromHtml("2a302f");
    private static readonly Color IndustryColor = Color.FromHtml("252d2e");

    private FirstLightMapModel? _model;
    private FirstLightGridPoint? _keyboardPoint;
    private double _flowSeconds;
    private double _redrawSeconds;

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

    public override void _Process(double delta)
    {
        if (_model is null)
        {
            return;
        }
        _flowSeconds = (_flowSeconds + delta) % 20d;
        _redrawSeconds += delta;
        if (_redrawSeconds >= (1d / 12d))
        {
            _redrawSeconds = 0d;
            QueueRedraw();
        }
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
        DrawTerrain(_model, plot);
        DrawGrid(_model.Bounds, plot);
        DrawBlockedCells(_model, plot);
        DrawRiskRect(_model, plot);
        DrawServiceArea(_model, plot);
        DrawPlantSites(_model, plot);
        DrawFactoryFeeder(_model, plot);
        DrawLineProjects(_model, plot);
        DrawSource(_model, plot);
        DrawTown(_model, plot);
        DrawHospital(_model, plot);
        DrawSubstation(_model, plot);
        DrawFactory(_model, plot);
        DrawGasPlant(_model, plot);
        DrawSupports(_model, plot);
        DrawPointerPreview(_model, plot);
        DrawKeyboardCursor(_model, plot);
        DrawLegend(_model);

        if (HasFocus())
        {
            DrawRect(new Rect2(Vector2.One * 2f, Size - (Vector2.One * 4f)), FocusColor, false, 2f);
        }
    }

    private void DrawTerrain(FirstLightMapModel model, Rect2 plot)
    {
        DrawRect(plot, Color.FromHtml("0d1b22"));

        float riverCenterX = ToCanvas(
            new FirstLightGridPoint(
                (model.RiskRect.Minimum.X + model.RiskRect.Maximum.X) / 2,
                model.Bounds.MinY),
            model.Bounds,
            plot).X;
        float riverHalfWidth = Math.Clamp(GridScale(model.Bounds, plot) * 1.15f, 18f, 42f);
        const int riverSegments = 8;
        var river = new Vector2[(riverSegments + 1) * 2];
        for (int index = 0; index <= riverSegments; index++)
        {
            float progress = index / (float)riverSegments;
            float y = plot.Position.Y + (plot.Size.Y * progress);
            float bend = MathF.Sin((progress * Mathf.Tau) + 0.8f) * riverHalfWidth * 0.28f;
            river[index] = new Vector2(riverCenterX + bend - riverHalfWidth, y);
            river[river.Length - 1 - index] =
                new Vector2(riverCenterX + bend + riverHalfWidth, y);
        }
        DrawColoredPolygon(river, RiverColor);
        DrawPolyline(river, new Color(ServiceStroke, 0.22f), 1.2f, true);

        float roadY = plot.Position.Y + (plot.Size.Y * 0.72f);
        Vector2 roadFrom = new(plot.Position.X, roadY - 10f);
        Vector2 roadTo = new(plot.End.X, roadY + 12f);
        DrawLine(roadFrom, roadTo, RoadColor, 10f, true);
        DrawLine(roadFrom, roadTo, new Color(MutedColor, 0.14f), 1f, true);

        var industry = new Rect2(
            plot.Position + new Vector2(plot.Size.X * 0.73f, plot.Size.Y * 0.69f),
            new Vector2(plot.Size.X * 0.23f, plot.Size.Y * 0.24f));
        DrawRect(industry, new Color(IndustryColor, 0.72f));
        for (int index = 1; index < 4; index++)
        {
            float x = industry.Position.X + ((industry.Size.X / 4f) * index);
            DrawLine(
                new Vector2(x, industry.Position.Y),
                new Vector2(x, industry.End.Y),
                new Color(MutedColor, 0.1f),
                1f);
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
            : new Color(RiskColor, model.FactoryStageActive ? 0.035f : 0.08f);
        Color stroke = model.FactoryStageActive
            ? new Color(RiskColor, 0.42f)
            : RiskColor;
        DrawRect(rect, fill);
        DrawDashedLine(rect.Position, new Vector2(rect.End.X, rect.Position.Y), stroke, 2f, 8f);
        DrawDashedLine(new Vector2(rect.End.X, rect.Position.Y), rect.End, stroke, 2f, 8f);
        DrawDashedLine(rect.End, new Vector2(rect.Position.X, rect.End.Y), stroke, 2f, 8f);
        DrawDashedLine(new Vector2(rect.Position.X, rect.End.Y), rect.Position, stroke, 2f, 8f);
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
                : model.FactoryStageActive
                    ? "해결된 공간 위험구역"
                    : "공간 위험구역 · 경계 포함",
            HorizontalAlignment.Left,
            -1f,
            12,
            stroke);
    }

    private void DrawPlantSites(FirstLightMapModel model, Rect2 plot)
    {
        foreach (FirstLightPlantSiteVisual site in
                 model.PlantSites ?? Array.Empty<FirstLightPlantSiteVisual>())
        {
            Vector2 center = ToCanvas(site.Position, model.Bounds, plot);
            float radius = site.Selected ? 18f : 16f;
            Color outline = site.Selected ? PlannedColor : SiteColor;
            var pad = new Rect2(
                center - new Vector2(radius, radius),
                new Vector2(radius * 2f, radius * 2f));
            DrawRect(pad, new Color(SiteColor, 0.12f));
            DrawDashedLine(pad.Position, new Vector2(pad.End.X, pad.Position.Y), outline, 2f, 6f);
            DrawDashedLine(new Vector2(pad.End.X, pad.Position.Y), pad.End, outline, 2f, 6f);
            DrawDashedLine(pad.End, new Vector2(pad.Position.X, pad.End.Y), outline, 2f, 6f);
            DrawDashedLine(new Vector2(pad.Position.X, pad.End.Y), pad.Position, outline, 2f, 6f);
            DrawString(
                ThemeDB.FallbackFont,
                center + new Vector2(-42f, 34f),
                $"허용 부지 · {site.SiteId}",
                HorizontalAlignment.Center,
                84f,
                11,
                outline);
        }
    }

    private void DrawFactoryFeeder(FirstLightMapModel model, Rect2 plot)
    {
        if (model.Factory is not FirstLightFactoryVisual factory)
        {
            return;
        }

        Vector2 from = ToCanvas(factory.FeederFrom, model.Bounds, plot);
        Vector2 to = ToCanvas(factory.Position, model.Bounds, plot);
        Color color = factory.FeederState switch
        {
            FirstLightFactoryFeederVisualState.Normal =>
                factory.DeliveredKw > 0 ? EnergizedColor : MutedColor,
            FirstLightFactoryFeederVisualState.Maintenance => BuildingColor,
            FirstLightFactoryFeederVisualState.Maintained => EnergizedColor,
            FirstLightFactoryFeederVisualState.Unavailable => InvalidColor,
            _ => throw new ArgumentOutOfRangeException(nameof(factory.FeederState)),
        };
        if (factory.FeederState == FirstLightFactoryFeederVisualState.Unavailable)
        {
            DrawLine(from, to, Color.FromHtml("2b171a"), 8f, true);
            DrawDashedLine(from, to, color, 4f, 12f, true, true);
            DrawCross((from + to) / 2f, color, 8f);
        }
        else if (factory.FeederState == FirstLightFactoryFeederVisualState.Maintenance)
        {
            DrawDashedLine(from, to, color, 5f, 10f, true, true);
            DrawConstructionHatching(from, to);
        }
        else
        {
            DrawLine(from, to, Color.FromHtml("102b31"), 7f, true);
            DrawLine(from, to, color, 3f, true);
            if (factory.DeliveredKw > 0)
            {
                DrawFlowMarkers(from, to, color);
            }
            if (factory.FeederState == FirstLightFactoryFeederVisualState.Maintained)
            {
                DrawCircle((from + to) / 2f, 6f, MapBackground);
                DrawCircle((from + to) / 2f, 6f, color, false, 2f, true);
            }
        }
        string state = factory.FeederState switch
        {
            FirstLightFactoryFeederVisualState.Normal => "정상",
            FirstLightFactoryFeederVisualState.Maintenance => "예방정비 중",
            FirstLightFactoryFeederVisualState.Maintained => "정비 완료",
            FirstLightFactoryFeederVisualState.Unavailable => "폭염 사용불가",
            _ => throw new ArgumentOutOfRangeException(nameof(factory.FeederState)),
        };
        string rating = factory.EffectiveRatingKw > 0
            ? $" · 유효정격 {PowerText(factory.EffectiveRatingKw)}"
            : string.Empty;
        DrawString(
            ThemeDB.FallbackFont,
            ((from + to) / 2f) + new Vector2(7f, -7f),
            $"공장 노후 feeder · {state}{rating}",
            HorizontalAlignment.Left,
            -1f,
            11,
            color);
    }

    private void DrawLineProjects(FirstLightMapModel model, Rect2 plot)
    {
        foreach (FirstLightLineVisual line in model.Lines)
        {
            var points = new List<FirstLightGridPoint> { line.From ?? model.Source };
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
                    FirstLightLineKind.PlantConnection => new Vector2(8f, -13f),
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
                DrawFlowMarkers(from, to, LineColor(kind));
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
        var baseRect = new Rect2(center - new Vector2(18f, 12f), new Vector2(36f, 24f));
        DrawRect(baseRect, new Color(SourceColor, 0.22f));
        DrawRect(baseRect, SourceColor, false, 2f);
        DrawLine(center + new Vector2(-11f, -12f), center + new Vector2(-11f, -27f), SourceColor, 5f);
        DrawLine(center + new Vector2(10f, -12f), center + new Vector2(10f, -22f), SourceColor, 4f);
        DrawCircle(center, 14f, SourceColor);
        DrawCircle(center, 7f, MapBackground);
        DrawLine(center + new Vector2(-7f, 0f), center + new Vector2(7f, 0f), TextColor, 2f);
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
        for (int index = -1; index <= 1; index++)
        {
            Vector2 home = center + new Vector2(index * 12f, Math.Abs(index) * 4f);
            var building = new Rect2(home - new Vector2(5f, 3f), new Vector2(10f, 11f));
            DrawRect(
                building,
                model.TownDeliveredKw > 0 ? new Color(color, 0.52f) : MapBackground);
            DrawRect(building, color, false, 1.7f);
            Vector2[] roof =
            [
                home + new Vector2(-7f, -3f),
                home + new Vector2(0f, -10f),
                home + new Vector2(7f, -3f),
            ];
            DrawPolyline(roof, color, 1.7f, true);
            if (model.TownDeliveredKw > 0)
            {
                DrawRect(new Rect2(home + new Vector2(-2f, 1f), new Vector2(4f, 4f)), TextColor);
            }
        }
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
        DrawRect(
            new Rect2(center - new Vector2(24f, 7f), new Vector2(48f, 18f)),
            new Color(utilityColor, 0.12f));
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
        else if (model.SubstationState == FirstLightProjectVisualState.Commissioned)
        {
            DrawCircle(center + new Vector2(-5f, 0f), 4f, MapBackground);
            DrawCircle(center + new Vector2(-5f, 0f), 4f, color, false, 1.5f);
            DrawCircle(center + new Vector2(5f, 0f), 4f, MapBackground);
            DrawCircle(center + new Vector2(5f, 0f), 4f, color, false, 1.5f);
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

    private void DrawFactory(FirstLightMapModel model, Rect2 plot)
    {
        if (model.Factory is not FirstLightFactoryVisual factory)
        {
            return;
        }

        Vector2 center = ToCanvas(factory.Position, model.Bounds, plot);
        Color color = factory.DeliveredKw > 0 ? EnergizedColor : MutedColor;
        var building = new Rect2(center - new Vector2(18f, 13f), new Vector2(36f, 26f));
        DrawRect(building, new Color(FactoryColor, 0.24f));
        DrawRect(building, color, false, 3f);
        DrawLine(center + new Vector2(-11f, -13f), center + new Vector2(-4f, -23f), color, 3f);
        DrawLine(center + new Vector2(-4f, -23f), center + new Vector2(3f, -13f), color, 3f);
        DrawLine(center + new Vector2(10f, -13f), center + new Vector2(10f, -27f), FactoryColor, 5f);
        if (factory.DeliveredKw > 0)
        {
            for (int index = -1; index <= 1; index++)
            {
                DrawRect(
                    new Rect2(center + new Vector2((index * 9f) - 3f, -2f), new Vector2(6f, 6f)),
                    TextColor);
            }
        }
        if (factory.DeliveredKw == 0)
        {
            DrawCross(center, color, 7f);
        }
        DrawString(
            ThemeDB.FallbackFont,
            center + new Vector2(-72f, 38f),
            factory.DeliveredKw > 0
                ? $"공장 · {PowerText(factory.DeliveredKw)} 공급"
                : "공장 · 용량 부족",
            HorizontalAlignment.Center,
            144f,
            12,
            color);
    }

    private void DrawGasPlant(FirstLightMapModel model, Rect2 plot)
    {
        if (model.GasPlant is not FirstLightGasPlantVisual plant)
        {
            return;
        }

        Vector2 center = ToCanvas(plant.Position, model.Bounds, plot);
        Color color = plant.State switch
        {
            FirstLightProjectVisualState.NotOrdered => PlannedColor,
            FirstLightProjectVisualState.Building => BuildingColor,
            FirstLightProjectVisualState.Commissioned when plant.Connected => EnergizedColor,
            FirstLightProjectVisualState.Commissioned => SiteColor,
            FirstLightProjectVisualState.Unavailable => InvalidColor,
            _ => throw new ArgumentOutOfRangeException(nameof(plant.State)),
        };
        var building = new Rect2(center - new Vector2(20f, 16f), new Vector2(40f, 32f));
        DrawRect(building, new Color(color, 0.2f));
        if (plant.State == FirstLightProjectVisualState.NotOrdered)
        {
            DrawDashedLine(building.Position, new Vector2(building.End.X, building.Position.Y), color, 3f, 6f);
            DrawDashedLine(new Vector2(building.End.X, building.Position.Y), building.End, color, 3f, 6f);
            DrawDashedLine(building.End, new Vector2(building.Position.X, building.End.Y), color, 3f, 6f);
            DrawDashedLine(new Vector2(building.Position.X, building.End.Y), building.Position, color, 3f, 6f);
        }
        else
        {
            DrawRect(building, color, false, 3f);
        }
        DrawLine(center + new Vector2(8f, -16f), center + new Vector2(8f, -30f), color, 5f);
        DrawLine(center + new Vector2(-7f, -16f), center + new Vector2(-7f, -25f), color, 4f);
        DrawLine(center + new Vector2(-14f, 7f), center + new Vector2(14f, 7f), color, 2f);
        if (plant.State == FirstLightProjectVisualState.Building)
        {
            DrawConstructionHatching(center + new Vector2(-17f, 0f), center + new Vector2(17f, 0f));
        }
        string status = plant.State switch
        {
            FirstLightProjectVisualState.NotOrdered => "가스발전소 · 초안",
            FirstLightProjectVisualState.Building => "가스발전소 · 공사 중 · 출력 0",
            FirstLightProjectVisualState.Commissioned when !plant.Connected =>
                "가스발전소 · 완공 · 계통 미접속 · 출력 0",
            FirstLightProjectVisualState.Commissioned =>
                $"가스발전소 · 계통접속 · 출력 {PowerText(plant.DispatchedKw)}",
            FirstLightProjectVisualState.Unavailable => "가스발전소 · 사용불가",
            _ => throw new ArgumentOutOfRangeException(nameof(plant.State)),
        };
        DrawString(
            ThemeDB.FallbackFont,
            center + new Vector2(-112f, -39f),
            status,
            HorizontalAlignment.Center,
            224f,
            11,
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

        if (preview.Mode == FirstLightPointerMode.GasPlant)
        {
            var draft = new Rect2(point - new Vector2(18f, 14f), new Vector2(36f, 28f));
            DrawRect(draft, new Color(color, 0.2f));
            DrawRect(draft, color, false, 2f);
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
                DrawDashedLine(from, point, PlannedColor, 3f, 8f, true, true);
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
        float phaseY = Size.Y - 39f;
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(18f, phaseY),
            model.FactoryStageActive
                ? $"{model.PhaseDescription} · □ 허용 발전소 부지"
                : model.PhaseDescription,
            HorizontalAlignment.Left,
            Math.Max(0f, Size.X - 36f),
            12,
            TextColor);

        float y = Size.Y - 15f;
        float x = 18f;
        DrawLegendLine(ref x, y, "계획", PlannedColor, FirstLightProjectVisualState.NotOrdered);
        DrawLegendLine(ref x, y, "공사", BuildingColor, FirstLightProjectVisualState.Building);
        DrawLegendLine(ref x, y, "통전", EnergizedColor, FirstLightProjectVisualState.Commissioned);
        DrawLegendLine(ref x, y, "사용불가", InvalidColor, FirstLightProjectVisualState.Unavailable);
        var risk = new Rect2(new Vector2(x, y - 11f), new Vector2(18f, 12f));
        DrawRect(risk, new Color(RiskColor, 0.12f));
        DrawRect(risk, RiskColor, false, 1.5f);
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(x + 24f, y),
            "위험구역",
            HorizontalAlignment.Left,
            -1f,
            11,
            TextColor);
    }

    private void DrawLegendLine(
        ref float x,
        float y,
        string label,
        Color color,
        FirstLightProjectVisualState state)
    {
        Vector2 from = new(x, y - 5f);
        Vector2 to = new(x + 25f, y - 5f);
        if (state == FirstLightProjectVisualState.NotOrdered)
        {
            DrawDashedLine(from, to, color, 2f, 5f, true, true);
        }
        else if (state == FirstLightProjectVisualState.Building)
        {
            DrawDashedLine(from, to, color, 3f, 6f, true, true);
            DrawConstructionHatching(from, to);
        }
        else if (state == FirstLightProjectVisualState.Commissioned)
        {
            DrawLine(from, to, color, 3f, true);
            DrawCircle((from + to) / 2f, 2.2f, TextColor);
        }
        else
        {
            DrawDashedLine(from, to, color, 3f, 7f, true, true);
            DrawCross((from + to) / 2f, color, 4f);
        }
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(x + 31f, y),
            label,
            HorizontalAlignment.Left,
            -1f,
            11,
            TextColor);
        x += label == "사용불가" ? 123f : 101f;
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
            : model.FactoryStageActive
                ? " 해결된 공간 위험구역이 흐리게 표시됨."
                : " 공간 위험구역 표시됨.";
        string factory = model.Factory is null
            ? string.Empty
            : $" 공장 {(model.Factory.DeliveredKw > 0 ? "공급 중" : "미공급")}, " +
              $"노후 feeder {FactoryFeederStateText(model.Factory.FeederState)}, " +
              $"유효정격 {PowerText(model.Factory.EffectiveRatingKw)}.";
        string plant = model.GasPlant is null
            ? string.Empty
            : $" 가스발전소 {(model.GasPlant.Connected ? "계통접속" : LineStateText(model.GasPlant.State))}, 출력 {PowerText(model.GasPlant.DispatchedKw)}.";
        return $"전력망 지도. {model.PhaseDescription}. {substation} {line} {model.SupplyDescription}.{incident}{factory}{plant}{cursor}";
    }

    private static string PowerText(long kw) =>
        $"{(kw / 1_000d).ToString("0.###", CultureInfo.InvariantCulture)} MW";

    private static string FactoryFeederStateText(FirstLightFactoryFeederVisualState state) =>
        state switch
        {
            FirstLightFactoryFeederVisualState.Normal => "정상",
            FirstLightFactoryFeederVisualState.Maintenance => "예방정비 중",
            FirstLightFactoryFeederVisualState.Maintained => "정비 완료",
            FirstLightFactoryFeederVisualState.Unavailable => "폭염 사용불가",
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };

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
        FirstLightLineKind.PlantConnection => EnergizedColor,
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

    private void DrawFlowMarkers(Vector2 from, Vector2 to, Color color)
    {
        Vector2 segment = to - from;
        float length = segment.Length();
        if (length < 12f)
        {
            return;
        }
        Vector2 direction = segment / length;
        const float spacing = 52f;
        float offset = (float)((_flowSeconds * 14d) % spacing);
        for (float distance = offset; distance < length; distance += spacing)
        {
            Vector2 center = from + (direction * distance);
            DrawCircle(center, 2.6f, new Color(color, 0.95f));
            DrawLine(center - (direction * 4f), center, TextColor, 1.5f, true);
        }
    }

    private void DrawCross(Vector2 center, Color color, float radius)
    {
        DrawLine(center - new Vector2(radius, radius), center + new Vector2(radius, radius), color, 2f, true);
        DrawLine(center + new Vector2(-radius, radius), center + new Vector2(radius, -radius), color, 2f, true);
    }
}
