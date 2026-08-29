using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Gridworks.Game.Realtime.UI;
using Godot;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

internal enum RealtimePlaceholderStateCue
{
    None,
    AuthoredUnavailableBars,
    EmergencyTriangle,
    ProtectiveOutageCross,
    OverLimitDiamond,
}

/// <summary>
/// R2-only world owner. It renders the Release.V3 typed state with the canonical G3 presentation
/// layer while retaining the same renderer-neutral contract for geometry, hit testing, and input.
/// </summary>
internal sealed partial class RealtimePlaceholderMap : Control, IRealtimeWorldView
{
    private const string G3Root = "res://art/commercial/g3/";
    private const string G3GroundRubble = G3Root + "tiles/ground-rubble-relief-c.png";
    private const string G3RiverWaterSurface = G3Root + "tiles/river-water-surface-a.png";
    private const string G3RiverWaterNeutral = G3Root + "river/river-water-neutral-b.png";
    private const string G3RiverWaterHeat = G3Root + "river/river-water-heat-a.png";
    private const string G3RiverWaterFlood = G3Root + "river/river-water-flood-a.png";
    private const string G3RiverConifer = G3Root + "river/river-bank-conifer-a.png";
    private const string G3RiverScrub = G3Root + "river/river-bank-scrub-a.png";
    private const string G3RiverOutcrop = G3Root + "river/river-bank-outcrop-a.png";
    private const string G3RiverBridgeAbutment = G3Root + "river/river-bridge-abutment-a.png";
    private const string G3RoadNorthWestSouthEast =
        G3Root + "roads/road-straight-nw-se-a.png";
    private const string G3RoadNorthEastSouthWest =
        G3Root + "roads/road-straight-ne-sw-a.png";
    private const string G3RoadCornerNorthEast = G3Root + "roads/road-corner-n-e-a.png";
    private const string G3RoadTJunction = G3Root + "roads/road-t-junction-a.png";
    private const string G3RoadCrossJunction = G3Root + "roads/road-cross-junction-a.png";
    private const string G3ServiceYard = G3Root + "roads/service-yard-tile-a.png";
    private const string G3WorkerHouseA = G3Root + "atomic/worker-house-a.png";
    private const string G3WorkerHouseB = G3Root + "atomic/worker-house-b.png";
    private const string G3WorkerHouseC = G3Root + "atomic/worker-house-c.png";
    private const string G3RowShop = G3Root + "atomic/row-shop-a.png";
    private const string G3Workshop = G3Root + "atomic/workshop-a.png";
    private const string G3SmallWarehouse = G3Root + "atomic/small-warehouse-a.png";
    private const string G3HospitalMain = G3Root + "atomic/hospital-main-a.png";
    private const string G3HospitalService = G3Root + "atomic/hospital-service-a.png";
    private const string G3PumpHouse = G3Root + "atomic/pump-house-a.png";
    private const string G3WaterTank = G3Root + "atomic/water-tank-a.png";
    private const string G3RetainingWall = G3Root + "atomic/retaining-wall-a.png";
    private const string G3StreetLamp = G3Root + "atomic/street-lamp-a.png";
    private const string G3PlantMainHall = G3Root + "grid/plant-main-hall-a.png";
    private const string G3PlantSmokestack = G3Root + "grid/plant-smokestack-a.png";
    private const string G3PlantTurbineHall = G3Root + "grid/plant-turbine-hall-a.png";
    private const string G3SwitchyardBreakerBay = G3Root + "grid/switchyard-breaker-bay-a.png";
    private const string G3SubstationTransformer =
        G3Root + "grid/substation-transformer-a.png";
    private const string G3StandardPole = G3Root + "grid/pole-standard-a.png";
    private const string G3ReinforcedPole = G3Root + "grid/pole-reinforced-a.png";
    private const string G3BridgeFoundation = G3Root + "grid/bridge-foundation-a.png";

    private readonly record struct G3Placement(
        string AssetPath,
        CoreMapPoint Position,
        float WorldMaxSide,
        float Alpha = 1f);

    private static readonly string[] G3AssetPaths =
    [
        G3GroundRubble, G3RiverWaterSurface, G3RiverWaterNeutral, G3RiverWaterHeat,
        G3RiverWaterFlood, G3RiverConifer, G3RiverScrub, G3RiverOutcrop,
        G3RiverBridgeAbutment,
        G3PlantMainHall, G3PlantSmokestack,
        G3PlantTurbineHall, G3SwitchyardBreakerBay, G3SubstationTransformer,
        G3StandardPole, G3ReinforcedPole, G3BridgeFoundation,
        CityResidentialBlock, CityIndustrialCampus,
        CityHospitalCampus, CityWaterworksCampus,
        G3WorkerHouseA, G3RowShop, G3Workshop, G3SmallWarehouse, G3StreetLamp,
        .. G3ExtendedMapAssetPaths,
    ];

    // These placements decorate the same release-world coordinates used by the R2 Core.
    // They are individual G3 units, not a baked map plate or replacement world data.
    private static readonly G3Placement[] G3RoadPlacements =
    [
        new(G3RoadNorthWestSouthEast, new CoreMapPoint(2440, 710), 640f, 0.72f),
        new(G3RoadNorthEastSouthWest, new CoreMapPoint(2680, 1020), 620f, 0.72f),
        new(G3RoadCornerNorthEast, new CoreMapPoint(2320, 520), 470f, 0.78f),
        new(G3RoadTJunction, new CoreMapPoint(2560, 1320), 520f, 0.76f),
        new(G3RoadCrossJunction, new CoreMapPoint(2800, 1570), 500f, 0.74f),
        new(G3ServiceYard, new CoreMapPoint(2410, 1780), 500f, 0.82f),
    ];

    private static readonly G3Placement[] G3CityPlacements =
    [
        new(G3WorkerHouseA, new CoreMapPoint(2890, 570), 265f),
        new(G3WorkerHouseB, new CoreMapPoint(3040, 610), 245f),
        new(G3WorkerHouseC, new CoreMapPoint(2910, 780), 270f),
        new(G3RowShop, new CoreMapPoint(3060, 800), 275f),
        new(G3Workshop, new CoreMapPoint(2730, 930), 285f),
        new(G3SmallWarehouse, new CoreMapPoint(2800, 1800), 350f),
        new(G3HospitalMain, new CoreMapPoint(2780, 1300), 430f),
        new(G3HospitalService, new CoreMapPoint(2960, 1430), 320f),
        new(G3PumpHouse, new CoreMapPoint(2440, 250), 330f),
        new(G3WaterTank, new CoreMapPoint(2620, 230), 290f),
        new(G3RetainingWall, new CoreMapPoint(2690, 1040), 360f, 0.88f),
        new(G3StreetLamp, new CoreMapPoint(2860, 1010), 160f),
    ];

    private static readonly G3Placement[] G3RiverPlacements =
    [
        new(G3RiverConifer, new CoreMapPoint(930, 320), 250f, 0.82f),
        new(G3RiverScrub, new CoreMapPoint(1690, 400), 210f, 0.82f),
        new(G3RiverOutcrop, new CoreMapPoint(1080, 1120), 240f, 0.86f),
        new(G3RiverConifer, new CoreMapPoint(1720, 1320), 260f, 0.82f),
        new(G3RiverScrub, new CoreMapPoint(1170, 1730), 210f, 0.80f),
    ];

    private static readonly Color Ground = Color.FromHtml("26342e");
    private static readonly Color G3WaterEdge = Color.FromHtml("111817");
    private static readonly Color G3BuildingBase = Color.FromHtml("151b1c");
    private const float G3BuildingParcelAlpha = 0.08f;
    private static readonly Color Normal = Color.FromHtml("78c7b9");
    private static readonly Color Planned = Color.FromHtml("d5b45c");
    private static readonly Color Emergency = Color.FromHtml("ed964d");
    private static readonly Color Outage = Color.FromHtml("b9bfbc");
    private static readonly Color Danger = Color.FromHtml("ec6f68");
    private static readonly Color Selected = Color.FromHtml("90e2d4");
    private static readonly Color Candidate = Color.FromHtml("f4d58a");
    private static readonly Color Text = Color.FromHtml("eef5f0");

    private RealtimeWorldPresentation? _presentation;
    private RealtimeWorldPointerFeedback _pointerFeedback =
        RealtimeWorldPointerFeedback.Empty;
    private MapViewportTransform? _transform;
    private CoreMapPoint? _pointer;
    private IReadOnlyList<string> _candidateCycle = Array.Empty<string>();
    private int _candidateIndex;
    private string? _preferredCandidateId;
    private bool _candidateSuppressedUntilInput;
    private bool _panning;
    private Vector2 _lastCanvasPointer;
    private bool _hasCanvasPointer;
    private string? _lastFollowSelectionId;
    private float _accessibilityScale = 1f;
    private float _minimumPointerHitRadius = 22f;
    private readonly Dictionary<string, Texture2D> _g3Textures = new(StringComparer.Ordinal);
#if DEBUG
    private readonly Dictionary<string, RealtimePlaceholderStateCue> _drawnStateCues =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _drawnAnalysisRiskAreaIds =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _drawnForecastRiskAreaIds =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _drawnActiveRiskAreaIds =
        new(StringComparer.Ordinal);
    private string? _drawnActiveCandidateId;
    private string? _drawnGuidanceTargetNodeId;
    private bool _drawnAnalysisOverlay;
    private readonly HashSet<string> _drawnG3AssetPaths = new(StringComparer.Ordinal);
    private readonly HashSet<string> _drawnG3Layers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _drawnCityDistrictIds = new(StringComparer.Ordinal);
    private string? _drawnG3WaterMaterial;
    private int _drawnG3SpriteCount;
    private float _drawnRiverBankMaxDeviation;
    private float _drawnBuildingParcelAlpha;
    private int _drawnCityRoadPathCount;
    private readonly List<Vector2[]> _drawnBridgeSpans = [];
    private readonly Dictionary<string, Vector2[]> _drawnConductorAnchors =
        new(StringComparer.Ordinal);
    private int? _drawnServiceAreaRadiusUnit;
    private bool _drawnServiceLink;
    private bool _drawnSubstationDraftFootprint;
#endif

    public event Action<RealtimePointerResolution, CoreMapPoint>? PrimaryRequested;
    public event Action<RealtimePointerResolution, CoreMapPoint>? PointerMoved;
    public event Action? CancelRequested;

    internal string ZoomLabel => _transform?.ZoomLabel ?? "지역 보기";
    public Vector2 CameraCenter => _transform?.Center ?? Vector2.Zero;
    public bool IsPanning => _panning;
    public Rect2 InteractionRect => new(Position, Size);

    internal int LabelFontSize => Math.Max(1, Mathf.RoundToInt(12f * _accessibilityScale));

    private string? ActiveCandidateId =>
        _candidateCycle.Count > 0 &&
        _candidateIndex >= 0 &&
        _candidateIndex < _candidateCycle.Count
            ? _candidateCycle[_candidateIndex]
            : null;

    private string ActiveCandidateVisibleLabel =>
        ActiveCandidateId is string candidateId && _presentation is not null
            ? $"후보 {_candidateIndex + 1}/{_candidateCycle.Count}" +
              (_candidateCycle.Count > 1 ? " · Q/E 전환" : string.Empty) +
              " · " +
              CandidateDisplayName(_presentation, candidateId)
            : string.Empty;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        ClipContents = true;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        TextureRepeat = TextureRepeatEnum.Enabled;
        AccessibilityName = "청류시 실시간 전력망";
        AccessibilityDescription =
            "설비와 선로 후보를 거리와 안정된 순서로 정렬하며 장식과 날씨는 클릭을 받지 않습니다.";
        Resized += ConfigureTransform;
    }

    public void SetPresentation(RealtimeWorldPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        bool chapterChanged = _presentation is not null &&
            !string.Equals(
                _presentation.ChapterId,
                presentation.ChapterId,
                StringComparison.Ordinal);
        _presentation = presentation;
        ConfigureTransform();
        EnsureKeyboardCursor();
        FollowSelection();
        if (chapterChanged)
        {
            _candidateSuppressedUntilInput = true;
            _candidateCycle = Array.Empty<string>();
            _candidateIndex = 0;
            _preferredCandidateId = null;
            _pointerFeedback = RealtimeWorldPointerFeedback.Empty;
        }
        else if (!_candidateSuppressedUntilInput)
        {
            _ = RefreshPointerResolution(RealtimeWorldProbeIds.PresentationRefresh);
        }
        UpdateAccessibility();
        QueueRedraw();
    }

    public void SetPointerFeedback(RealtimeWorldPointerFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        _pointerFeedback = feedback;
        UpdateAccessibility();
        QueueRedraw();
    }

    internal void ApplyLayout(RealtimeLayoutProfile profile)
    {
        _accessibilityScale = Math.Max(1f, profile.AccessibilityScale);
        _minimumPointerHitRadius = Math.Max(20f, profile.MinimumHitTarget / 2f);
        if (!_candidateSuppressedUntilInput)
        {
            _ = RefreshPointerResolution(RealtimeWorldProbeIds.LayoutRefresh);
        }
        QueueRedraw();
    }

    public void SetInteractionRect(Rect2 rect, RealtimeLayoutProfile profile)
    {
        AnchorLeft = 0;
        AnchorTop = 0;
        AnchorRight = 0;
        AnchorBottom = 0;
        Position = rect.Position;
        Size = rect.Size;
        ApplyLayout(profile);
    }

    public void RequestFocus() => GrabFocus();

    public void CycleCandidate(int delta)
    {
        if (delta == 0 || !_hasCanvasPointer || _transform is null)
        {
            return;
        }
        _candidateSuppressedUntilInput = false;
        RealtimePointerResolution? resolution = RefreshPointerResolution(
            RealtimeWorldProbeIds.KeyboardChooser);
        // Selection actions and draft handles own the point above any world
        // candidates underneath them. Q/E must not announce or cycle an
        // obscured candidate that Enter cannot actually activate.
        if (resolution is null ||
            resolution.Owner != RealtimePointerOwner.WorldCandidate ||
            _candidateCycle.Count == 0)
        {
            return;
        }
        _candidateIndex = ((_candidateIndex + delta) % _candidateCycle.Count +
            _candidateCycle.Count) % _candidateCycle.Count;
        _preferredCandidateId = ActiveCandidateId;
        UpdateAccessibility();
        QueueRedraw();
    }

    public void BeginPan()
    {
        _panning = true;
        MouseDefaultCursorShape = CursorShape.Drag;
    }

    public void EndPan()
    {
        _panning = false;
        MouseDefaultCursorShape = CursorShape.Arrow;
    }

    public void ConfirmCurrentCandidate()
    {
        if (_presentation is null || _transform is null || !_hasCanvasPointer)
        {
            return;
        }
        _candidateSuppressedUntilInput = false;
        RealtimePointerResolution? resolution = RefreshPointerResolution(
            RealtimeWorldProbeIds.KeyboardConfirm);
        if (resolution is not null && _pointer is CoreMapPoint point)
        {
            PrimaryRequested?.Invoke(resolution, point);
        }
    }

    public RealtimeMapCameraSnapshot CaptureCamera() => new(
        _transform?.Center ?? Vector2.Zero,
        _transform?.ZoomIndex ?? 0);

    public void RestoreCamera(RealtimeMapCameraSnapshot camera)
    {
        if (_transform is null)
        {
            return;
        }
        _transform.Home();
        _transform.SetZoomAt(camera.ZoomIndex, _transform.PlotRect.GetCenter());
        Vector2 current = _transform.Center;
        _transform.PanByCanvasDelta(
            new Vector2(current.X - camera.Center.X, current.Y - camera.Center.Y) *
            (float)_transform.Scale);
        if (!_candidateSuppressedUntilInput)
        {
            _ = RefreshPointerResolution(RealtimeWorldProbeIds.CameraRestore);
        }
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (_presentation is null || _transform is null)
        {
            return;
        }
        switch (inputEvent)
        {
            case InputEventMouseMotion motion when _panning:
                _candidateSuppressedUntilInput = false;
                if (_hasCanvasPointer)
                {
                    _transform.PanByCanvasDelta(motion.Position - _lastCanvasPointer);
                }
                _hasCanvasPointer = true;
                _lastCanvasPointer = motion.Position;
                _pointer = ToWorld(motion.Position);
                RealtimePointerResolution panResolution = ResolveCanvasPoint(
                    RealtimeWorldProbeIds.Hover,
                    motion.Position,
                    _pointer.Value);
                PointerMoved?.Invoke(panResolution, _pointer.Value);
                UpdateAccessibility();
                QueueRedraw();
                AcceptEvent();
                break;
            case InputEventMouseMotion motion:
                _candidateSuppressedUntilInput = false;
                _hasCanvasPointer = true;
                _lastCanvasPointer = motion.Position;
                _pointer = ToWorld(motion.Position);
                RealtimePointerResolution hoverResolution = ResolveCanvasPoint(
                    RealtimeWorldProbeIds.Hover,
                    motion.Position,
                    _pointer.Value);
                PointerMoved?.Invoke(hoverResolution, _pointer.Value);
                UpdateAccessibility();
                QueueRedraw();
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.Left &&
                mouse.Pressed:
                _candidateSuppressedUntilInput = false;
                CoreMapPoint worldPoint = ToWorld(mouse.Position);
                _pointer = worldPoint;
                _lastCanvasPointer = mouse.Position;
                _hasCanvasPointer = true;
                RealtimePointerResolution resolution = ResolveCanvasPoint(
                    RealtimeWorldProbeIds.Primary,
                    mouse.Position,
                    worldPoint);
                PrimaryRequested?.Invoke(resolution, worldPoint);
                AcceptEvent();
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.Right &&
                mouse.Pressed:
                CancelRequested?.Invoke();
                AcceptEvent();
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.WheelUp &&
                mouse.Pressed:
                _candidateSuppressedUntilInput = false;
                _transform.SetZoomAt(_transform.ZoomIndex + 1, mouse.Position);
                _ = RefreshPointerResolution(RealtimeWorldProbeIds.ZoomRefresh);
                QueueRedraw();
                AcceptEvent();
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.WheelDown &&
                mouse.Pressed:
                _candidateSuppressedUntilInput = false;
                _transform.SetZoomAt(_transform.ZoomIndex - 1, mouse.Position);
                _ = RefreshPointerResolution(RealtimeWorldProbeIds.ZoomRefresh);
                QueueRedraw();
                AcceptEvent();
                break;
            // Keyboard commands deliberately remain unhandled here. The
            // priority-aware RealtimeInputRouter is their sole owner, so a
            // focused map cannot bypass a blocking modal/HUD context or reduce
            // one physical key twice. The controller invokes CycleCandidate,
            // ConfirmCurrentCandidate, and the analysis intent after routing.
        }
    }

    public override void _Draw()
    {
#if DEBUG
        _drawnStateCues.Clear();
        _drawnAnalysisRiskAreaIds.Clear();
        _drawnForecastRiskAreaIds.Clear();
        _drawnActiveRiskAreaIds.Clear();
        _drawnActiveCandidateId = null;
        _drawnGuidanceTargetNodeId = null;
        _drawnAnalysisOverlay = false;
        _drawnG3AssetPaths.Clear();
        _drawnG3Layers.Clear();
        _drawnCityDistrictIds.Clear();
        _drawnG3WaterMaterial = null;
        _drawnG3SpriteCount = 0;
        _drawnRiverBankMaxDeviation = 0f;
        _drawnBuildingParcelAlpha = 0f;
        _drawnCityRoadPathCount = 0;
        _drawnBridgeSpans.Clear();
        _drawnConductorAnchors.Clear();
        _drawnServiceAreaRadiusUnit = null;
        _drawnServiceLink = false;
        _drawnSubstationDraftFootprint = false;
#endif
        DrawG3Ground();
        if (_presentation is null || _transform is null)
        {
            return;
        }
        DrawG3Terrain(_presentation);
        DrawG3Roads();
        DrawG3City(_presentation);
        DrawG3Weather(_presentation);
        DrawServiceAreasAndLinks(_presentation);
        if (_presentation.AnalysisVisible)
        {
#if DEBUG
            _drawnAnalysisOverlay = true;
#endif
            DrawForecastRiskAreas(_presentation);
        }
        if (_presentation.ActiveRiskAreaIds.Count > 0)
        {
            DrawActiveRiskAreas(_presentation);
        }
        DrawNodeEquipmentLayer(_presentation);
        DrawEdges(_presentation);
        DrawNodeOverlayLayer(_presentation);
        DrawGuidanceTarget(_presentation);
        DrawActiveCandidate(_presentation);
        DrawSelectionAction(_presentation);
        DrawDraft(_presentation);
        DrawPointer(_presentation);
    }

    private void DrawServiceAreasAndLinks(RealtimeWorldPresentation presentation)
    {
        RealtimeWorldDraftHandle? nodeDraft = presentation.Draft.Handles.FirstOrDefault(
            item => string.Equals(item.Id, RealtimeWorldIds.DraftNode,
                StringComparison.Ordinal));
        if (nodeDraft is not null && presentation.PlacementClass is not null)
        {
            DrawServiceArea(
                presentation,
                nodeDraft.Point,
                presentation.PlacementClass.ServiceRadiusUnit,
                presentation.PlacementClass.FootprintRadiusUnit,
                Planned,
                presentation.PlacementClass.DisplayName,
                drawGhost: true);
        }
        else if (presentation.Tool == RealtimeTool.BuildNode &&
                 presentation.PlacementClass is not null &&
                 (_pointerFeedback.Point ?? _pointer) is CoreMapPoint pointer)
        {
            DrawServiceArea(
                presentation,
                pointer,
                presentation.PlacementClass.ServiceRadiusUnit,
                presentation.PlacementClass.FootprintRadiusUnit,
                _pointerFeedback.Accepted ? Planned : Danger,
                presentation.PlacementClass.DisplayName,
                drawGhost: true);
        }
        else
        {
            RealtimeWorldServiceArea? selectedArea = presentation.ServiceAreas.FirstOrDefault(
                item => string.Equals(item.NodeId, presentation.SelectedAssetId,
                    StringComparison.Ordinal));
            RealtimeWorldServiceLink? link = presentation.Highlight?.ServiceLink;
            RealtimeWorldServiceArea? linkedArea = link is null
                ? null
                : presentation.ServiceAreas.FirstOrDefault(item => string.Equals(
                    item.NodeId,
                    link.SubstationNodeId,
                    StringComparison.Ordinal));
            RealtimeWorldServiceArea? visibleArea = selectedArea ?? linkedArea;
            if (visibleArea is not null)
            {
                SpatialNodeDefinition node = presentation.World.Nodes.Single(item =>
                    string.Equals(item.NodeId, visibleArea.NodeId,
                        StringComparison.Ordinal));
                DrawServiceArea(
                    presentation,
                    node.Position,
                    visibleArea.RadiusUnit,
                    visibleArea.FootprintRadiusUnit,
                    Selected,
                    visibleArea.ClassDisplayName,
                    drawGhost: false);
            }
        }

        if (presentation.Highlight?.ServiceLink is not RealtimeWorldServiceLink service)
        {
            return;
        }
        SpatialNodeDefinition from = presentation.World.Nodes.Single(item => string.Equals(
            item.NodeId,
            service.SubstationNodeId,
            StringComparison.Ordinal));
        SpatialNodeDefinition to = presentation.World.Nodes.Single(item => string.Equals(
            item.NodeId,
            service.LoadNodeId,
            StringComparison.Ordinal));
#if DEBUG
        _drawnServiceLink = true;
#endif
        Color linkColor = service.Supplied ? Selected : Danger;
        Vector2 fromPoint = Point(from.Position);
        Vector2 toPoint = Point(to.Position);
        DrawDashedLine(fromPoint, toPoint, linkColor with { A = 0.92f });
        DrawCircle(toPoint, 9f * _accessibilityScale, linkColor, false,
            2.5f * _accessibilityScale, true);
        DrawLine(
            toPoint + new Vector2(-5f, 0f) * _accessibilityScale,
            toPoint + new Vector2(-1f, 5f) * _accessibilityScale,
            linkColor,
            2f * _accessibilityScale,
            true);
        DrawLine(
            toPoint + new Vector2(-1f, 5f) * _accessibilityScale,
            toPoint + new Vector2(6f, -5f) * _accessibilityScale,
            linkColor,
            2f * _accessibilityScale,
            true);
    }

    private void DrawServiceArea(
        RealtimeWorldPresentation presentation,
        CoreMapPoint centerWorld,
        int radiusUnit,
        int footprintRadiusUnit,
        Color color,
        string classDisplayName,
        bool drawGhost)
    {
#if DEBUG
        _drawnServiceAreaRadiusUnit = radiusUnit;
        _drawnSubstationDraftFootprint = drawGhost;
#endif
        Vector2 center = Point(centerWorld);
        float radius = Math.Max(1f, radiusUnit * (float)_transform!.Scale);
        DrawCircle(center, radius, color with { A = 0.035f });
        const int segmentCount = 64;
        for (int index = 0; index < segmentCount; index += 2)
        {
            float fromAngle = Mathf.Tau * index / segmentCount;
            float toAngle = Mathf.Tau * (index + 1) / segmentCount;
            DrawLine(
                center + Vector2.FromAngle(fromAngle) * radius,
                center + Vector2.FromAngle(toAngle) * radius,
                color with { A = 0.9f },
                1.8f * _accessibilityScale,
                true);
        }

        int covered = 0;
        foreach (SpatialNodeDefinition loadNode in presentation.World.Nodes.Where(item =>
                     presentation.World.NodeClasses.Single(nodeClass => string.Equals(
                         nodeClass.ClassId,
                         item.ClassId,
                         StringComparison.Ordinal)).Kind ==
                     SpatialNodeKind.DedicatedLoadTerminal))
        {
            int distance = checked((int)FixedGeometry.CeilDistance(
                centerWorld,
                loadNode.Position));
            Vector2 loadPoint = Point(loadNode.Position);
            float glyph = 10f * _accessibilityScale;
            if (distance > radiusUnit)
            {
                if (distance <= radiusUnit + Math.Max(160, radiusUnit / 3))
                {
                    Color outside = Outage with { A = 0.72f };
                    DrawLine(loadPoint + new Vector2(-glyph, -glyph),
                        loadPoint + new Vector2(glyph, glyph), outside,
                        2.4f * _accessibilityScale, true);
                    DrawLine(loadPoint + new Vector2(-glyph, glyph),
                        loadPoint + new Vector2(glyph, -glyph), outside,
                        2.4f * _accessibilityScale, true);
                }
                continue;
            }
            covered++;
            DrawCircle(loadPoint, glyph + (4f * _accessibilityScale),
                color with { A = 0.18f });
            DrawCircle(loadPoint, glyph + (4f * _accessibilityScale), color,
                false, 2.3f * _accessibilityScale, true);
            DrawLine(loadPoint + new Vector2(-glyph, -glyph),
                loadPoint + new Vector2(glyph, -glyph), color,
                2f * _accessibilityScale, true);
            DrawLine(loadPoint + new Vector2(-glyph, -glyph),
                loadPoint + new Vector2(-glyph, glyph), color,
                2f * _accessibilityScale, true);
            DrawLine(loadPoint + new Vector2(-glyph * 0.45f, 0f),
                loadPoint + new Vector2(-glyph * 0.08f, glyph * 0.42f), color,
                2.4f * _accessibilityScale, true);
            DrawLine(loadPoint + new Vector2(-glyph * 0.08f, glyph * 0.42f),
                loadPoint + new Vector2(glyph * 0.58f, -glyph * 0.5f), color,
                2.4f * _accessibilityScale, true);
        }

        string label = $"R {radiusUnit:N0} m · 포함 {covered}곳";
        int labelSize = Math.Max(LabelFontSize,
            Mathf.RoundToInt(15f * _accessibilityScale));
        Vector2 labelOrigin = center +
            new Vector2(12f, -radius - 12f) * _accessibilityScale;
        Vector2 labelTextSize = ThemeDB.FallbackFont.GetStringSize(
            label,
            HorizontalAlignment.Left,
            -1,
            labelSize);
        var labelBadge = new Rect2(
            labelOrigin - new Vector2(7f, labelTextSize.Y + 5f) * _accessibilityScale,
            labelTextSize + new Vector2(14f, 9f) * _accessibilityScale);
        var labelStyle = new StyleBoxFlat
        {
            BgColor = new Color(Color.FromHtml("101615"), 0.9f),
            BorderColor = color with { A = 0.72f },
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };
        DrawStyleBox(labelStyle, labelBadge);
        DrawString(
            ThemeDB.FallbackFont,
            labelOrigin,
            label,
            HorizontalAlignment.Left,
            -1,
            labelSize,
            color);

        if (!drawGhost)
        {
            return;
        }
        float footprint = Math.Max(
            18f * _accessibilityScale,
            footprintRadiusUnit * (float)_transform.Scale);
        Color footprintColor = color;
        Vector2[] footprintPoints =
        [
            center + new Vector2(0f, -footprint * 0.62f),
            center + new Vector2(footprint, 0f),
            center + new Vector2(0f, footprint * 0.62f),
            center + new Vector2(-footprint, 0f),
        ];
        DrawColoredPolygon(footprintPoints, footprintColor with { A = 0.13f });
        for (int index = 0; index < footprintPoints.Length; index++)
        {
            DrawDashedLine(
                footprintPoints[index],
                footprintPoints[(index + 1) % footprintPoints.Length],
                footprintColor);
        }
        float ghostSize = radiusUnit >= 800 ? 650f : 520f;
        DrawG3Sprite(
            G3SubstationTransformer,
            center,
            WorldPixels(ghostSize),
            new Color(footprintColor, 0.64f));
        Vector2 bayAnchor = center + new Vector2(footprint * 1.18f, 0f);
        DrawLine(center, bayAnchor, footprintColor with { A = 0.9f },
            3f * _accessibilityScale, true);
        DrawCircle(bayAnchor, 5f * _accessibilityScale, footprintColor);
    }

    internal RealtimePointerResolution ResolveWorldProbe(RealtimePointerProbe probe) =>
        RealtimePointerOwnerResolver.Resolve(
            probe,
            _presentation?.CompatibleLineNodeIds);

    private RealtimePointerResolution ResolveCanvasPoint(
        string probeId,
        Vector2 canvasPoint,
        CoreMapPoint worldPoint)
    {
        string? previousCandidateId = ActiveCandidateId ?? _preferredCandidateId;
        RealtimeMapCandidate[] candidates = Candidates(canvasPoint).ToArray();
        RealtimePointerResolution resolution = RealtimePointerOwnerResolver.Resolve(
            new RealtimePointerProbe(
                probeId,
                worldPoint,
                Array.AsReadOnly(candidates),
                BlockingModalHit: _presentation!.Surface ==
                    RealtimeSurface.BlockingModal,
                OverlayVisible: _presentation.AnalysisVisible,
                WeatherVisible: _presentation.Weather != RealtimeWorldWeather.Clear),
            _presentation.CompatibleLineNodeIds);
        bool candidateIdIsConfirmable = _presentation!.Tool is
            RealtimeTool.Inspect or RealtimeTool.Analysis or RealtimeTool.BuildLine;
        if (resolution.OrderedWorldCandidateIds.Count == 0 ||
            resolution.Owner != RealtimePointerOwner.WorldCandidate ||
            !candidateIdIsConfirmable)
        {
            _candidateCycle = Array.Empty<string>();
            _candidateIndex = 0;
            if (resolution.Owner != RealtimePointerOwner.BlockingModal)
            {
                _preferredCandidateId = null;
            }
            UpdateAccessibility();
            return resolution;
        }
        string[] candidateIds = resolution.OrderedWorldCandidateIds.ToArray();
        _candidateCycle = Array.AsReadOnly(candidateIds);
        bool compatibleNodeAvailable = candidateIds.Any(id =>
            _presentation.CompatibleLineNodeIds.Contains(id, StringComparer.Ordinal));
        bool mayRetainPrevious = previousCandidateId is not null &&
            (!compatibleNodeAvailable || _presentation.CompatibleLineNodeIds.Contains(
                previousCandidateId,
                StringComparer.Ordinal));
        int retainedIndex = !mayRetainPrevious
            ? -1
            : Array.FindIndex(candidateIds, id => string.Equals(
                id,
                previousCandidateId,
                StringComparison.Ordinal));
        _candidateIndex = retainedIndex >= 0 ? retainedIndex : 0;
        string selected = _candidateCycle[_candidateIndex];
        _preferredCandidateId = selected;
        RealtimeMapCandidate chosen = resolution.OrderedCandidates.Single(item =>
            string.Equals(item.Id, selected, StringComparison.Ordinal));
        UpdateAccessibility();
        return resolution with
        {
            Owner = chosen.Owner,
            ResolvedId = chosen.Id,
        };
    }

    /// <summary>
    /// The keyboard/mouse target is stored in world coordinates. Whenever a
    /// responsive surface, modal, zoom, or camera change rebuilds the canvas
    /// transform, reproject that same world point and recompute the exact hit
    /// owner. This keeps the visible candidate badge and Enter on one authority.
    /// </summary>
    private RealtimePointerResolution? RefreshPointerResolution(string probeId)
    {
        if (!_hasCanvasPointer || _pointer is not CoreMapPoint worldPoint ||
            _presentation is null || _transform is null)
        {
            return null;
        }
        _lastCanvasPointer = Point(worldPoint);
        RealtimePointerResolution resolution = ResolveCanvasPoint(
            probeId,
            _lastCanvasPointer,
            worldPoint);
        QueueRedraw();
        return resolution;
    }

    private IEnumerable<RealtimeMapCandidate> Candidates(Vector2 canvasPoint)
    {
        RealtimeWorldPresentation presentation = _presentation ??
            throw new InvalidOperationException("World presentation is not ready.");
        if (!presentation.PlacementMode &&
            SelectionActionPoint(presentation) is
            (string selectedAssetId, Vector2 actionPoint))
        {
            double actionDistance = actionPoint.DistanceSquaredTo(canvasPoint);
            double actionRadius = Math.Max(
                18f * _accessibilityScale,
                _minimumPointerHitRadius);
            if (actionDistance <= actionRadius * actionRadius)
            {
                yield return new RealtimeMapCandidate(
                    RealtimeWorldIds.SelectionAction(selectedAssetId),
                    RealtimeMapCandidateKind.SelectionAction,
                    RealtimePointerOwner.SelectionAction,
                    actionDistance);
            }
        }
        foreach (RealtimeWorldDraftHandle handle in presentation.Draft.Handles)
        {
            double distance = Point(handle.Point).DistanceSquaredTo(canvasPoint);
            double hitRadius = Math.Max(
                24f * _accessibilityScale,
                _minimumPointerHitRadius);
            if (distance <= hitRadius * hitRadius)
            {
                yield return new RealtimeMapCandidate(
                    handle.Id,
                    RealtimeMapCandidateKind.DraftHandle,
                    RealtimePointerOwner.DraftHandle,
                    distance);
            }
        }
        foreach (SpatialNodeDefinition node in presentation.World.Nodes)
        {
            double distance = Point(node.Position).DistanceSquaredTo(canvasPoint);
            double hitRadius = Math.Max(
                36f * _accessibilityScale,
                _minimumPointerHitRadius);
            bool districtHit = DistrictContainsPoint(
                node,
                canvasPoint,
                out double districtDistance);
            if (districtHit || distance <= hitRadius * hitRadius)
            {
                yield return new RealtimeMapCandidate(
                    node.NodeId,
                    RealtimeMapCandidateKind.Node,
                    RealtimePointerOwner.WorldCandidate,
                    districtHit ? districtDistance : distance);
            }
        }
        foreach (SpatialEdgeDefinition edge in presentation.World.Edges)
        {
            SpatialNodeDefinition from = presentation.World.Nodes.Single(item =>
                string.Equals(item.NodeId, edge.FromNodeId, StringComparison.Ordinal));
            SpatialNodeDefinition to = presentation.World.Nodes.Single(item =>
                string.Equals(item.NodeId, edge.ToNodeId, StringComparison.Ordinal));
            double distance = SegmentDistanceSquared(
                canvasPoint,
                Point(from.Position),
                Point(to.Position));
            double hitRadius = Math.Max(
                12f * _accessibilityScale,
                _minimumPointerHitRadius);
            if (distance <= hitRadius * hitRadius)
            {
                yield return new RealtimeMapCandidate(
                    edge.EdgeId,
                    RealtimeMapCandidateKind.Edge,
                    RealtimePointerOwner.WorldCandidate,
                    distance);
            }
        }
    }

    private void DrawG3Ground()
    {
        RecordG3Layer("ground");
        if (G3Texture(G3GroundRubble) is not Texture2D ground)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), Ground);
            return;
        }
        DrawTextureRectRegion(
            ground,
            new Rect2(Vector2.Zero, Size),
            new Rect2(Vector2.Zero, Size * 1.62f),
            new Color(0.34f, 0.35f, 0.32f, 1f));
        DrawG3GroundVariation();
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(Color.FromHtml("101716"), 0.48f));
        RecordG3Asset(G3GroundRubble);
    }

    private void DrawG3Terrain(RealtimeWorldPresentation presentation)
    {
        RecordG3Layer("terrain");
        string weatherMaterial = WeatherWaterMaterial(presentation.Weather);
        _ = G3Texture(G3RiverWaterSurface);
        Texture2D? weatherWater = G3Texture(weatherMaterial);
#if DEBUG
        _drawnG3WaterMaterial = weatherMaterial;
#endif
        foreach (TerrainPolygonDefinition terrain in presentation.World.Terrain.OrderBy(item =>
                     item.TerrainId,
                     StringComparer.Ordinal))
        {
            Vector2[] polygon = terrain.Polygon.Select(Point).ToArray();
            if (terrain.Kind == TerrainKind.Water)
            {
                (Vector2[] leftBank, Vector2[] rightBank) =
                    BuildG3NaturalRiverBanks(polygon);
                Vector2[] riverSurface =
                [
                    .. leftBank,
                    .. rightBank.Reverse(),
                ];
                if (G3Texture(G3RiverWaterSurface) is Texture2D baseWater)
                {
                    DrawG3TexturedPolygon(
                        riverSurface,
                        baseWater,
                        G3RiverWaterSurface,
                        new Color(0.54f, 0.68f, 0.72f, 0.86f));
                }
                if (weatherWater is not null)
                {
                    DrawG3TexturedPolygon(
                        riverSurface,
                        weatherWater,
                        weatherMaterial,
                        WeatherWaterModulate(presentation.Weather));
                }
                DrawG3NaturalRiverBanks(leftBank, rightBank);
                DrawG3RiverCurrent(leftBank, rightBank, presentation.Weather);
                DrawG3MeasuredBridges(leftBank, rightBank);
                continue;
            }
            if (terrain.Kind == TerrainKind.Building)
            {
                Vector2 centroid = polygon.Aggregate(Vector2.Zero, (sum, point) =>
                    sum + point) / polygon.Length;
                Vector2[] inset = polygon.Select(point => centroid.Lerp(point, 0.94f))
                    .ToArray();
                DrawColoredPolygon(polygon, G3BuildingBase with
                {
                    A = G3BuildingParcelAlpha * 0.45f,
                });
                DrawColoredPolygon(inset, new Color(Color.FromHtml("26302e"),
                    G3BuildingParcelAlpha));
                DrawPolyline(
                    [.. inset, inset[0]],
                    new Color(Color.FromHtml("81877d"), 0.10f),
                    0.9f * _accessibilityScale,
                    true);
#if DEBUG
                _drawnBuildingParcelAlpha = Math.Max(
                    _drawnBuildingParcelAlpha,
                    G3BuildingParcelAlpha);
#endif
            }
        }
        DrawG3Placements(
            G3RiverPlacements,
            new Color(0.84f, 0.80f, 0.72f, 1f));
        DrawG3FullRiverDetails(presentation);
    }

    private void DrawG3Roads()
    {
        RecordG3Layer("roads");
        DrawCityRoadNetwork();
    }

    private void DrawG3City(RealtimeWorldPresentation presentation)
    {
        RecordG3Layer("city");
        DrawCityDistricts(presentation);
    }

    private void DrawG3Placements(
        IEnumerable<G3Placement> placements,
        Color modulate)
    {
        foreach (G3Placement placement in placements
            .OrderBy(item => Point(item.Position).Y)
            .ThenBy(item => Point(item.Position).X)
            .ThenBy(item => item.AssetPath, StringComparer.Ordinal))
        {
            DrawG3Sprite(
                placement.AssetPath,
                Point(placement.Position),
                WorldPixels(placement.WorldMaxSide),
                modulate with { A = modulate.A * placement.Alpha });
        }
    }

    private void DrawG3Weather(RealtimeWorldPresentation presentation)
    {
        RecordG3Layer("weather");
        switch (presentation.Weather)
        {
            case RealtimeWorldWeather.Heat:
                DrawRect(
                    new Rect2(Vector2.Zero, Size),
                    new Color(Color.FromHtml("a55c2d"), 0.12f));
                for (int index = 0; index < 11; index++)
                {
                    float x = ((index * 97) % Math.Max(1f, Size.X - 24f)) + 12f;
                    float y = ((index * 131) % Math.Max(1f, Size.Y - 20f)) + 10f;
                    Vector2 start = new(x, y);
                    Vector2 end = start + new Vector2(9f + (index % 3) * 3f, 4f);
                    DrawLine(start, end, new Color(Color.FromHtml("261710"), 0.38f), 1.2f, true);
                }
                break;
            case RealtimeWorldWeather.Rain:
            case RealtimeWorldWeather.Storm:
                bool storm = presentation.Weather == RealtimeWorldWeather.Storm;
                DrawRect(
                    new Rect2(Vector2.Zero, Size),
                    new Color(Color.FromHtml(storm ? "18313b" : "315963"),
                        storm ? 0.18f : 0.10f));
                long minutePhase = WeatherMinutePhase(presentation);
                int streakCount = storm ? 34 : 20;
                for (int index = 0; index < streakCount; index++)
                {
                    float x = (float)((minutePhase * 31L + index * 97L) %
                        Math.Max(1, (int)Math.Ceiling(Size.X)));
                    float y = (float)((minutePhase * 17L + index * 53L) %
                        Math.Max(1, (int)Math.Ceiling(Size.Y)));
                    Vector2 start = new(x, y);
                    DrawLine(
                        start,
                        start + new Vector2(-7f, 15f),
                        new Color(Color.FromHtml("a9c7cf"), storm ? 0.28f : 0.20f),
                        storm ? 1.15f : 0.85f,
                        true);
                }
                break;
        }
    }

    private static long WeatherMinutePhase(RealtimeWorldPresentation presentation) =>
        presentation.ReduceMotion ? 0L : Math.Abs(presentation.Minute % 997L);

    private void DrawG3TexturedPolygon(
        Vector2[] polygon,
        Texture2D texture,
        string assetPath,
        Color modulate)
    {
        DrawColoredPolygon(polygon, modulate, TiledG3TextureUvs(polygon, texture), texture);
        RecordG3Asset(assetPath);
    }

    private void DrawG3Sprite(
        string assetPath,
        Vector2 center,
        float maxSide,
        Color modulate)
    {
        if (G3Texture(assetPath) is not Texture2D texture || maxSide <= 0f)
        {
            return;
        }
        Vector2 spriteSize = FitG3SpriteSize(texture, maxSide);
        DrawG3SpriteShadow(center, spriteSize);
        DrawTextureRect(
            texture,
            G3SpriteRect(center, spriteSize),
            false,
            modulate);
        RecordG3Asset(assetPath);
#if DEBUG
        _drawnG3SpriteCount++;
#endif
    }

    private void DrawG3SpriteShadow(Vector2 center, Vector2 spriteSize)
    {
        float halfWidth = Math.Clamp(spriteSize.X * 0.30f, 4f, 54f);
        float halfDepth = Math.Clamp(spriteSize.Y * 0.08f, 2f, 13f);
        Vector2[] shadow =
        [
            center + new Vector2(-halfWidth, 0f),
            center + new Vector2(0f, -halfDepth),
            center + new Vector2(halfWidth, 0f),
            center + new Vector2(0f, halfDepth),
        ];
        DrawColoredPolygon(shadow, new Color(0f, 0f, 0f, 0.30f));
    }

    private Texture2D? G3Texture(string assetPath)
    {
        if (_g3Textures.TryGetValue(assetPath, out Texture2D? cached))
        {
            return cached;
        }
        Texture2D? loaded = GD.Load<Texture2D>(assetPath);
        if (loaded is not null)
        {
            _g3Textures.Add(assetPath, loaded);
        }
        return loaded;
    }

    private float WorldPixels(float worldSize) =>
        Math.Max(8f, worldSize * (float)_transform!.Scale);

    private static Vector2 FitG3SpriteSize(Texture2D texture, float maxSide)
    {
        float longest = Math.Max(texture.GetWidth(), texture.GetHeight());
        return new Vector2(
            texture.GetWidth() / longest,
            texture.GetHeight() / longest) * maxSide;
    }

    private static Rect2 G3SpriteRect(Vector2 groundAnchor, Vector2 spriteSize) => new(
        groundAnchor - new Vector2(spriteSize.X * 0.5f, spriteSize.Y * 0.78f),
        spriteSize);

    private static Vector2[] TiledG3TextureUvs(Vector2[] polygon, Texture2D texture)
    {
        const float screenRepeatPeriod = 96f;
        return polygon.Select(point => new Vector2(
            (point.X / screenRepeatPeriod) * Math.Max(1, texture.GetWidth() - 1),
            (point.Y / screenRepeatPeriod) * Math.Max(1, texture.GetHeight() - 1))).ToArray();
    }

    private static string WeatherWaterMaterial(RealtimeWorldWeather weather) => weather switch
    {
        RealtimeWorldWeather.Heat => G3RiverWaterHeat,
        RealtimeWorldWeather.Rain or RealtimeWorldWeather.Storm => G3RiverWaterFlood,
        _ => G3RiverWaterNeutral,
    };

    private static Color WeatherWaterModulate(RealtimeWorldWeather weather) => weather switch
    {
        RealtimeWorldWeather.Heat => new Color(0.72f, 0.60f, 0.44f, 0.92f),
        RealtimeWorldWeather.Rain => new Color(0.68f, 0.82f, 0.88f, 0.92f),
        RealtimeWorldWeather.Storm => new Color(0.56f, 0.72f, 0.80f, 0.96f),
        _ => new Color(0.70f, 0.80f, 0.82f, 0.94f),
    };

    private void RecordG3Layer(string layer)
    {
#if DEBUG
        _drawnG3Layers.Add(layer);
#endif
    }

    private void RecordG3Asset(string assetPath)
    {
#if DEBUG
        _drawnG3AssetPaths.Add(assetPath);
#endif
    }

    private void DrawForecastRiskAreas(RealtimeWorldPresentation presentation)
    {
        HashSet<string> forecast = presentation.ForecastRiskAreaIds.ToHashSet(
            StringComparer.Ordinal);
        foreach (SpatialRiskAreaDefinition risk in presentation.World.RiskAreas.Where(item =>
                     forecast.Contains(item.RiskAreaId)))
        {
            Vector2[] polygon = risk.Polygon.Select(Point).ToArray();
            for (int index = 0; index < polygon.Length; index++)
            {
                DrawDashedLine(
                    polygon[index],
                    polygon[(index + 1) % polygon.Length],
                    Planned);
            }
#if DEBUG
            _drawnAnalysisRiskAreaIds.Add(risk.RiskAreaId);
            _drawnForecastRiskAreaIds.Add(risk.RiskAreaId);
#endif
        }
    }

    private void DrawActiveRiskAreas(RealtimeWorldPresentation presentation)
    {
        HashSet<string> active = presentation.ActiveRiskAreaIds.ToHashSet(StringComparer.Ordinal);
        foreach (SpatialRiskAreaDefinition risk in presentation.World.RiskAreas.Where(item =>
                     active.Contains(item.RiskAreaId)))
        {
            Vector2[] polygon = risk.Polygon.Select(Point).ToArray();
            DrawColoredPolygon(polygon, Danger with { A = 0.14f });
            DrawPolyline(
                [.. polygon, polygon[0]],
                Danger,
                2f * _accessibilityScale,
                true);
#if DEBUG
            _drawnAnalysisRiskAreaIds.Add(risk.RiskAreaId);
            _drawnActiveRiskAreaIds.Add(risk.RiskAreaId);
#endif
        }
    }

    private void DrawEdges(RealtimeWorldPresentation presentation)
    {
        RecordG3Layer("conductors");
        HashSet<string> highlighted =
            presentation.Highlight?.EdgeIds.ToHashSet(StringComparer.Ordinal) ?? [];
        foreach (SpatialEdgeDefinition edge in presentation.World.Edges.OrderBy(item =>
                     item.EdgeId,
                     StringComparer.Ordinal))
        {
            SpatialNodeDefinition from = presentation.World.Nodes.Single(item =>
                string.Equals(item.NodeId, edge.FromNodeId, StringComparison.Ordinal));
            SpatialNodeDefinition to = presentation.World.Nodes.Single(item =>
                string.Equals(item.NodeId, edge.ToNodeId, StringComparison.Ordinal));
            RealtimeWorldAssetStatus? status = Status(presentation, edge.EdgeId);
            bool selected = string.Equals(
                presentation.SelectedAssetId,
                edge.EdgeId,
                StringComparison.Ordinal);
            Color color = selected
                ? Selected
                : edge.Commissioned ? StateColor(status?.State) : Planned;
            float width = (selected || highlighted.Contains(edge.EdgeId) ? 5f : 2.5f) *
                _accessibilityScale;
            // The existing map resolver continues to own edge hover/selection at
            // ground level. Rendering alone lifts pole endpoints to their visual
            // crossarms, while every non-pole endpoint remains on that geometry.
            Vector2 fromPoint = G3ConductorAnchor(presentation, from);
            Vector2 toPoint = G3ConductorAnchor(presentation, to);
#if DEBUG
            _drawnConductorAnchors[edge.EdgeId] =
            [
                Point(from.Position), fromPoint,
                Point(to.Position), toPoint,
            ];
#endif
            DrawG3ConductorSpan(fromPoint, toPoint, color, width);
            if (!edge.Commissioned)
            {
                DrawDashedLine(fromPoint, toPoint, Planned);
            }
            else
            {
                DrawEdgeStateCue(
                    edge.EdgeId,
                    fromPoint,
                    toPoint,
                    status?.State);
            }
        }
    }

    private void DrawNodeEquipmentLayer(RealtimeWorldPresentation presentation)
    {
        RecordG3Layer("grid");
        foreach (SpatialNodeDefinition node in presentation.World.Nodes.OrderBy(item =>
                     item.NodeId,
                     StringComparer.Ordinal))
        {
            DrawG3NodeEquipment(presentation, node, Status(presentation, node.NodeId));
        }
    }

    private void DrawNodeOverlayLayer(RealtimeWorldPresentation presentation)
    {
        HashSet<string> highlighted =
            presentation.Highlight?.NodeIds.ToHashSet(StringComparer.Ordinal) ?? [];
        foreach (SpatialNodeDefinition node in presentation.World.Nodes.OrderBy(item =>
                     item.NodeId,
                     StringComparer.Ordinal))
        {
            RealtimeWorldAssetStatus? status = Status(presentation, node.NodeId);
            bool selected = string.Equals(
                presentation.SelectedAssetId,
                node.NodeId,
                StringComparison.Ordinal);
            bool routeHighlighted = highlighted.Contains(node.NodeId);
            if (DrawDistrictNodeOverlay(
                    presentation,
                    node,
                    status,
                    selected,
                    routeHighlighted))
            {
                continue;
            }
            float radius = NodeRadius(presentation.World, node);
            Color color = node.Commissioned ? StateColor(status?.State) : Planned;
            Vector2 center = Point(node.Position);
            DrawCircle(center, radius, color with { A = 0.26f });
            DrawCircle(
                center,
                radius + (selected || routeHighlighted ? 7 : 2) * _accessibilityScale,
                selected || routeHighlighted ? Selected : Ground,
                false,
                (selected || routeHighlighted ? 3 : 1) * _accessibilityScale,
                true);
            DrawNodeStateCue(node.NodeId, center, radius, status?.State);
            if (selected || _transform!.ZoomIndex > 0)
            {
                string statusText = StatusLabel(status);
                DrawString(
                    ThemeDB.FallbackFont,
                    center + new Vector2(
                        radius + (5f * _accessibilityScale),
                        4f * _accessibilityScale),
                    $"{node.DisplayName} · {statusText}",
                    HorizontalAlignment.Left,
                    -1,
                    LabelFontSize,
                    Text);
            }
        }
    }

    private Vector2 G3ConductorAnchor(
        RealtimeWorldPresentation presentation,
        SpatialNodeDefinition node)
    {
        Vector2 ground = Point(node.Position);
        SpatialNodeKind kind = presentation.World.NodeClasses.Single(item =>
            string.Equals(item.ClassId, node.ClassId, StringComparison.Ordinal)).Kind;
        if (kind != SpatialNodeKind.Pole || node.AuthoredFoundation)
        {
            return ground;
        }
        string assetPath = node.ClassId == "STANDARD_POLE"
            ? G3StandardPole
            : G3ReinforcedPole;
        float maxSide = WorldPixels(node.ClassId == "STANDARD_POLE" ? 280f : 320f);
        if (G3Texture(assetPath) is not Texture2D texture)
        {
            return ground;
        }
        Vector2 spriteSize = FitG3SpriteSize(texture, maxSide);
        return ground + new Vector2(0f, -spriteSize.Y * 0.68f);
    }

    private void DrawG3ConductorSpan(Vector2 from, Vector2 to, Color color, float width)
    {
        Vector2 axis = to - from;
        if (axis.LengthSquared() <= 0.001f)
        {
            return;
        }
        Vector2 normal = new Vector2(-axis.Y, axis.X).Normalized() *
            (3.8f * _accessibilityScale);
        float sag = Math.Clamp(axis.Length() * 0.13f, 5f, 28f) * _accessibilityScale;
        Vector2[] center = Enumerable.Range(0, 17)
            .Select(index =>
            {
                float t = index / 16f;
                return from.Lerp(to, t) + new Vector2(0f, 4f * t * (1f - t) * sag);
            })
            .ToArray();
        Vector2[][] strands =
        [
            center.Select(point => point + normal).ToArray(),
            center,
            center.Select(point => point - normal).ToArray(),
        ];
        float conductorWidth = Math.Max(1.15f, width * 0.48f);
        foreach (Vector2[] strand in strands)
        {
            DrawPolyline(
                strand,
                new Color(Color.FromHtml("071012"), 0.82f),
                conductorWidth + 1.35f,
                true);
            DrawPolyline(strand, color with { A = 0.96f }, conductorWidth, true);
        }
    }

    private void DrawG3NodeEquipment(
        RealtimeWorldPresentation presentation,
        SpatialNodeDefinition node,
        RealtimeWorldAssetStatus? status)
    {
        Color modulate = G3NodeModulate(node.Commissioned ? status?.State :
            RealtimeWorldAssetState.Planned);
        SpatialNodeKind kind = presentation.World.NodeClasses.Single(item => string.Equals(
            item.ClassId,
            node.ClassId,
            StringComparison.Ordinal)).Kind;
        if (kind == SpatialNodeKind.SourceTerminal)
        {
            DrawG3SourceCampusFoundation(node);
            DrawG3SourcePlant(node, modulate);
            return;
        }
        if (node.AuthoredFoundation)
        {
            DrawG3Sprite(G3BridgeFoundation, Point(node.Position), WorldPixels(330f), modulate);
            return;
        }
        switch (kind)
        {
            case SpatialNodeKind.Pole:
                DrawG3Sprite(
                    node.ClassId == "STANDARD_POLE" ? G3StandardPole : G3ReinforcedPole,
                    Point(node.Position),
                    WorldPixels(node.ClassId == "STANDARD_POLE" ? 280f : 320f),
                    modulate);
                return;
            case SpatialNodeKind.Substation:
                DrawG3Sprite(
                    G3SubstationTransformer,
                    Point(node.Position),
                    WorldPixels(350f),
                    modulate);
                return;
            case SpatialNodeKind.DedicatedLoadTerminal:
                // City districts own the visible facility mass and its authored footprint.
                // The electrical terminal remains at the world node so conductors and Core
                // geometry keep one authority without drawing the building a second time.
                return;
        }
    }

    private void DrawG3SourcePlant(SpatialNodeDefinition node, Color modulate)
    {
        CoreMapPoint origin = node.Position;
        if (string.Equals(node.NodeId, "SOUTH_SOURCE_NODE", StringComparison.Ordinal))
        {
            DrawG3Sprite(
                G3PlantTurbineHall,
                Point(new CoreMapPoint(origin.XUnit - 95, origin.YUnit + 35)),
                WorldPixels(500f),
                modulate);
            DrawG3Sprite(
                G3PlantMainHall,
                Point(new CoreMapPoint(origin.XUnit + 45, origin.YUnit + 85)),
                WorldPixels(440f),
                modulate with { A = 0.96f });
            DrawG3Sprite(
                G3SwitchyardBreakerBay,
                Point(new CoreMapPoint(origin.XUnit + 205, origin.YUnit + 105)),
                WorldPixels(340f),
                modulate);
            return;
        }
        DrawG3Sprite(
            G3PlantSmokestack,
            Point(new CoreMapPoint(origin.XUnit + 60, origin.YUnit - 150)),
            WorldPixels(480f),
            modulate);
        DrawG3Sprite(
            G3PlantTurbineHall,
            Point(new CoreMapPoint(origin.XUnit - 170, origin.YUnit + 70)),
            WorldPixels(520f),
            modulate);
        DrawG3Sprite(G3PlantMainHall, Point(origin), WorldPixels(590f), modulate);
        DrawG3Sprite(
            G3SwitchyardBreakerBay,
            Point(new CoreMapPoint(origin.XUnit + 190, origin.YUnit + 110)),
            WorldPixels(360f),
            modulate);
    }

    private void DrawG3SourceCampusFoundation(SpatialNodeDefinition node)
    {
        Vector2 center = Point(new CoreMapPoint(
            node.Position.XUnit,
            node.Position.YUnit + 65));
        float halfWidth = WorldPixels(
            string.Equals(node.NodeId, "SOUTH_SOURCE_NODE", StringComparison.Ordinal)
                ? 390f
                : 470f) * 0.5f;
        float halfDepth = WorldPixels(330f) * 0.27f;
        Vector2[] footprint =
        [
            center + new Vector2(-halfWidth, 0f),
            center + new Vector2(0f, -halfDepth),
            center + new Vector2(halfWidth, 0f),
            center + new Vector2(0f, halfDepth),
        ];
        DrawColoredPolygon(
            footprint.Select(point => point + new Vector2(0f, 5f * _accessibilityScale))
                .ToArray(),
            new Color(0f, 0f, 0f, 0.34f));
        DrawColoredPolygon(footprint, new Color(Color.FromHtml("30332f"), 0.82f));
        DrawPolyline(
            [.. footprint, footprint[0]],
            new Color(Color.FromHtml("6c7068"), 0.26f),
            1.2f * _accessibilityScale,
            true);
    }

    private void DrawG3LoadTerminal(SpatialNodeDefinition node, Color modulate)
    {
        Vector2 center = Point(node.Position);
        switch (node.NodeId)
        {
            case "WATER_TERMINAL":
                DrawG3Sprite(G3PumpHouse, center, WorldPixels(340f), modulate);
                break;
            case "HOSPITAL_TERMINAL":
                DrawG3Sprite(G3HospitalMain, center, WorldPixels(440f), modulate);
                break;
            case "FACTORY_TERMINAL":
                DrawG3Sprite(G3SmallWarehouse, center, WorldPixels(390f), modulate);
                break;
            case "NORTH_RESIDENTIAL_TERMINAL":
                DrawG3Sprite(G3WorkerHouseA, center, WorldPixels(300f), modulate);
                break;
            case "EAST_RESIDENTIAL_TERMINAL":
                DrawG3Sprite(G3RowShop, center, WorldPixels(320f), modulate);
                break;
            default:
                DrawG3Sprite(G3Workshop, center, WorldPixels(300f), modulate);
                break;
        }
    }

    private static Color G3NodeModulate(RealtimeWorldAssetState? state) => state switch
    {
        RealtimeWorldAssetState.Planned or RealtimeWorldAssetState.Building or
            RealtimeWorldAssetState.AuthoredUnavailable => new Color(1f, 0.74f, 0.38f, 0.94f),
        RealtimeWorldAssetState.Emergency => new Color(1f, 0.64f, 0.34f, 0.96f),
        RealtimeWorldAssetState.ProtectiveOutage => new Color(0.68f, 0.70f, 0.70f, 0.88f),
        RealtimeWorldAssetState.OverLimit => new Color(1f, 0.42f, 0.37f, 0.96f),
        _ => new Color(0.92f, 0.88f, 0.78f, 0.98f),
    };

    private void DrawActiveCandidate(RealtimeWorldPresentation presentation)
    {
        if (ActiveCandidateId is not string candidateId)
        {
            return;
        }
#if DEBUG
        _drawnActiveCandidateId = candidateId;
#endif
        Vector2 anchor;
        SpatialNodeDefinition? node = presentation.World.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, candidateId, StringComparison.Ordinal));
        if (node is not null)
        {
            if (!DrawDistrictCandidateOutline(node, Candidate, out anchor))
            {
                anchor = Point(node.Position);
                float radius = NodeRadius(presentation.World, node) +
                    11f * _accessibilityScale;
                DrawCircle(
                    anchor,
                    radius,
                    Candidate,
                    false,
                    3f * _accessibilityScale,
                    true);
            }
        }
        else
        {
            SpatialEdgeDefinition? edge = presentation.World.Edges.FirstOrDefault(item =>
                string.Equals(item.EdgeId, candidateId, StringComparison.Ordinal));
            if (edge is null)
            {
                return;
            }
            SpatialNodeDefinition from = presentation.World.Nodes.Single(item =>
                string.Equals(item.NodeId, edge.FromNodeId, StringComparison.Ordinal));
            SpatialNodeDefinition to = presentation.World.Nodes.Single(item =>
                string.Equals(item.NodeId, edge.ToNodeId, StringComparison.Ordinal));
            Vector2 fromPoint = Point(from.Position);
            Vector2 toPoint = Point(to.Position);
            Vector2 axis = toPoint - fromPoint;
            Vector2 normal = axis.LengthSquared() > 0.001f
                ? new Vector2(axis.Y, -axis.X).Normalized()
                : Vector2.Up;
            float offset = 4f * _accessibilityScale;
            DrawLine(
                fromPoint + normal * offset,
                toPoint + normal * offset,
                Candidate,
                2f * _accessibilityScale,
                true);
            DrawLine(
                fromPoint - normal * offset,
                toPoint - normal * offset,
                Candidate,
                2f * _accessibilityScale,
                true);
            anchor = (fromPoint + toPoint) / 2f + normal * (10f * _accessibilityScale);
        }
        DrawActiveCandidateBadge(anchor, ActiveCandidateVisibleLabel);
    }

    private void DrawGuidanceTarget(RealtimeWorldPresentation presentation)
    {
        if (presentation.GuidanceTarget is not RealtimeWorldGuidanceTarget target)
        {
            return;
        }
        SpatialNodeDefinition? node = presentation.World.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, target.NodeId, StringComparison.Ordinal));
        if (node is null)
        {
            return;
        }
#if DEBUG
        _drawnGuidanceTargetNodeId = node.NodeId;
#endif
        if (DrawDistrictCandidateOutline(node, Selected, out Vector2 districtAnchor))
        {
            DrawActiveCandidateBadge(districtAnchor, target.Label);
            return;
        }
        Vector2 center = Point(node.Position);
        float radius = NodeRadius(presentation.World, node) +
            16f * _accessibilityScale;
        DrawCircle(
            center,
            radius,
            Selected with { A = 0.18f });
        DrawCircle(
            center,
            radius,
            Selected,
            false,
            3f * _accessibilityScale,
            true);
        Vector2 elbow = center + new Vector2(
            radius + 12f * _accessibilityScale,
            -radius - 8f * _accessibilityScale);
        DrawLine(
            center + new Vector2(radius * 0.7f, -radius * 0.7f),
            elbow,
            Selected,
            2f * _accessibilityScale,
            true);
        DrawActiveCandidateBadge(elbow, target.Label);
    }

    private void DrawActiveCandidateBadge(Vector2 anchor, string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }
        int fontSize = Math.Max(LabelFontSize, Mathf.RoundToInt(15f * _accessibilityScale));
        Vector2 textSize = ThemeDB.FallbackFont.GetStringSize(
            label,
            HorizontalAlignment.Left,
            -1,
            fontSize);
        Vector2 padding = new(9f * _accessibilityScale, 6f * _accessibilityScale);
        Vector2 badgeSize = textSize + padding * 2f;
        Vector2 desired = anchor + new Vector2(12f, 12f) * _accessibilityScale;
        Vector2 position = new(
            Math.Clamp(
                desired.X,
                4f,
                Math.Max(4f, Size.X - badgeSize.X - 4f)),
            Math.Clamp(
                desired.Y,
                4f,
                Math.Max(4f, Size.Y - badgeSize.Y - 4f)));
        var badge = new Rect2(position, badgeSize);
        DrawRect(badge, Ground with { A = 0.96f });
        DrawRect(badge, Candidate, false, 2f * _accessibilityScale);
        DrawString(
            ThemeDB.FallbackFont,
            position + new Vector2(padding.X, padding.Y + textSize.Y * 0.78f),
            label,
            HorizontalAlignment.Left,
            -1,
            fontSize,
            Text);
    }

    private void DrawSelectionAction(RealtimeWorldPresentation presentation)
    {
        if (presentation.PlacementMode ||
            SelectionActionPoint(presentation) is not (_, Vector2 point))
        {
            return;
        }
        float radius = 11f * _accessibilityScale;
        DrawCircle(point, radius, Ground);
        DrawCircle(point, radius, Selected, false, 2f * _accessibilityScale, true);
        DrawString(
            ThemeDB.FallbackFont,
            point + new Vector2(-3.5f, 4.5f) * _accessibilityScale,
            "i",
            HorizontalAlignment.Left,
            -1,
            Math.Max(1, Mathf.RoundToInt(13f * _accessibilityScale)),
            Selected);
    }

    private (string AssetId, Vector2 Point)? SelectionActionPoint(
        RealtimeWorldPresentation presentation)
    {
        if (presentation.PlacementMode ||
            presentation.SelectedAssetId is not string selectedId ||
            _transform is null)
        {
            return null;
        }
        SpatialNodeDefinition? node = presentation.World.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, selectedId, StringComparison.Ordinal));
        if (node is not null)
        {
            if (TryGetDistrictVisual(
                    node,
                    out _,
                    out Vector2 districtCenter,
                    out Vector2 halfExtents))
            {
                Vector2 districtAction = districtCenter + new Vector2(
                    halfExtents.X * 0.72f,
                    -halfExtents.Y - (18f * _accessibilityScale));
                return (selectedId, ClampSelectionAction(districtAction));
            }
            float radius = NodeRadius(presentation.World, node);
            Vector2 direction = new Vector2(1f, -1f).Normalized();
            Vector2 raw = Point(node.Position) +
                direction * (radius + (24f * _accessibilityScale));
            return (selectedId, ClampSelectionAction(raw));
        }
        SpatialEdgeDefinition? edge = presentation.World.Edges.FirstOrDefault(item =>
            string.Equals(item.EdgeId, selectedId, StringComparison.Ordinal));
        if (edge is null)
        {
            return null;
        }
        SpatialNodeDefinition from = presentation.World.Nodes.Single(item =>
            string.Equals(item.NodeId, edge.FromNodeId, StringComparison.Ordinal));
        SpatialNodeDefinition to = presentation.World.Nodes.Single(item =>
            string.Equals(item.NodeId, edge.ToNodeId, StringComparison.Ordinal));
        Vector2 fromPoint = Point(from.Position);
        Vector2 toPoint = Point(to.Position);
        Vector2 axis = toPoint - fromPoint;
        Vector2 normal = axis.LengthSquared() > 0.001f
            ? new Vector2(axis.Y, -axis.X).Normalized()
            : new Vector2(1f, -1f).Normalized();
        Vector2 rawPoint = (fromPoint + toPoint) / 2f +
            normal * (24f * _accessibilityScale);
        return (selectedId, ClampSelectionAction(rawPoint));
    }

    private Vector2 ClampSelectionAction(Vector2 point)
    {
        float margin = 22f * _accessibilityScale;
        return new Vector2(
            Math.Clamp(point.X, margin, Math.Max(margin, Size.X - margin)),
            Math.Clamp(point.Y, margin, Math.Max(margin, Size.Y - margin)));
    }

    private void DrawDraft(RealtimeWorldPresentation presentation)
    {
        if (presentation.Draft.LinePath.Count == 0)
        {
            return;
        }
        var points = presentation.Draft.LinePath.Select(Point).ToList();
        Vector2? ghost = null;
        if (presentation.Draft.ExtendLineToPointer && _pointer is CoreMapPoint pointer)
        {
            ghost = Point(pointer);
        }
        if (points.Count > 1)
        {
            DrawPolyline(points.ToArray(), Planned, 4f * _accessibilityScale, true);
        }
        foreach (Vector2 point in points)
        {
            DrawCircle(point, 7f * _accessibilityScale, Planned);
            DrawCircle(
                point,
                11f * _accessibilityScale,
                Selected,
                false,
                2f * _accessibilityScale,
                true);
        }
        if (ghost.HasValue)
        {
            Color ghostColor = _pointerFeedback.Accepted ? Planned : Danger;
            DrawLine(
                points[^1],
                ghost.Value,
                ghostColor,
                (_pointerFeedback.Accepted ? 4f : 6f) * _accessibilityScale,
                true);
            DrawCircle(
                ghost.Value,
                8f * _accessibilityScale,
                ghostColor,
                false,
                3f * _accessibilityScale,
                true);
        }
    }

    private void DrawPointer(RealtimeWorldPresentation presentation)
    {
        CoreMapPoint? pointer = _pointerFeedback.Point ?? _pointer;
        if (pointer is not CoreMapPoint value ||
            !presentation.PlacementMode && !HasFocus())
        {
            return;
        }
        Vector2 center = Point(value);
        Color color = presentation.PlacementMode && !_pointerFeedback.Accepted
            ? Danger
            : Selected;
        float radius = (presentation.PlacementMode ? 12f : 8f) * _accessibilityScale;
        DrawCircle(center, radius, color, false, 2f * _accessibilityScale, true);
        float arm = 16f * _accessibilityScale;
        DrawLine(
            center + Vector2.Left * arm,
            center + Vector2.Right * arm,
            color,
            _accessibilityScale);
        DrawLine(
            center + Vector2.Up * arm,
            center + Vector2.Down * arm,
            color,
            _accessibilityScale);
    }

    private void DrawDashedLine(Vector2 from, Vector2 to, Color color)
    {
        const int segments = 12;
        for (int index = 0; index < segments; index += 2)
        {
            DrawLine(from.Lerp(to, index / (float)segments),
                from.Lerp(to, (index + 1) / (float)segments),
                color,
                2f * _accessibilityScale,
                true);
        }
    }

    private void DrawEdgeStateCue(
        string assetId,
        Vector2 from,
        Vector2 to,
        RealtimeWorldAssetState? state)
    {
        Vector2 axis = to - from;
        if (axis.LengthSquared() <= 0.001f)
        {
            return;
        }
        Vector2 normal = new Vector2(axis.Y, -axis.X).Normalized();
        Vector2 middle = (from + to) / 2f;
        float scale = _accessibilityScale;
        RealtimePlaceholderStateCue cue = StateCue(state);
#if DEBUG
        _drawnStateCues[assetId] = cue;
#endif
        switch (cue)
        {
            case RealtimePlaceholderStateCue.AuthoredUnavailableBars:
                DrawDashedLine(from, to, Text);
                DrawLine(
                    middle - normal * 6f * scale,
                    middle + normal * 6f * scale,
                    Planned,
                    3f * scale,
                    true);
                break;
            case RealtimePlaceholderStateCue.EmergencyTriangle:
                DrawLine(from + normal * 3f * scale, to + normal * 3f * scale,
                    Emergency, 1.5f * scale, true);
                DrawTriangle(middle, 6f * scale, Emergency);
                break;
            case RealtimePlaceholderStateCue.ProtectiveOutageCross:
                DrawDashedLine(from, to, Text);
                DrawX(middle, 7f * scale, Outage);
                break;
            case RealtimePlaceholderStateCue.OverLimitDiamond:
                DrawDiamond(middle, 7f * scale, Danger);
                break;
        }
    }

    private void DrawNodeStateCue(
        string assetId,
        Vector2 center,
        float radius,
        RealtimeWorldAssetState? state)
    {
        float scale = _accessibilityScale;
        Vector2 cueCenter = center + Vector2.Up * (radius + 7f * scale);
        RealtimePlaceholderStateCue cue = StateCue(state);
#if DEBUG
        _drawnStateCues[assetId] = cue;
#endif
        switch (cue)
        {
            case RealtimePlaceholderStateCue.AuthoredUnavailableBars:
                DrawLine(
                    center + new Vector2(-radius, radius) * 0.55f,
                    center + new Vector2(radius, -radius) * 0.55f,
                    Text,
                    2.5f * scale,
                    true);
                break;
            case RealtimePlaceholderStateCue.EmergencyTriangle:
                DrawTriangle(cueCenter, 6f * scale, Emergency);
                break;
            case RealtimePlaceholderStateCue.ProtectiveOutageCross:
                DrawX(center, Math.Max(radius * 0.72f, 5f * scale), Text);
                break;
            case RealtimePlaceholderStateCue.OverLimitDiamond:
                DrawDiamond(cueCenter, 6f * scale, Danger);
                break;
        }
    }

    private void DrawTriangle(Vector2 center, float radius, Color color)
    {
        Vector2[] points =
        [
            center + Vector2.Up * radius,
            center + new Vector2(0.866f, 0.5f) * radius,
            center + new Vector2(-0.866f, 0.5f) * radius,
        ];
        DrawPolyline([.. points, points[0]], color, 2f * _accessibilityScale, true);
    }

    private void DrawDiamond(Vector2 center, float radius, Color color)
    {
        Vector2[] points =
        [
            center + Vector2.Up * radius,
            center + Vector2.Right * radius,
            center + Vector2.Down * radius,
            center + Vector2.Left * radius,
        ];
        DrawPolyline([.. points, points[0]], color, 2f * _accessibilityScale, true);
    }

    private void DrawX(Vector2 center, float radius, Color color)
    {
        Vector2 diagonal = new(radius, radius);
        DrawLine(center - diagonal, center + diagonal, color,
            2f * _accessibilityScale, true);
        Vector2 cross = new(radius, -radius);
        DrawLine(center - cross, center + cross, color,
            2f * _accessibilityScale, true);
    }

    private void ConfigureTransform()
    {
        if (_presentation is null || Size.X <= 0 || Size.Y <= 0)
        {
            return;
        }
        MapBounds bounds = _presentation.World.Bounds;
        var mapBounds = new MapViewportBounds(
            bounds.MinXUnit,
            bounds.MaxXUnit,
            bounds.MinYUnit,
            bounds.MaxYUnit);
        if (_transform is null)
        {
            _transform = new MapViewportTransform(mapBounds, Size);
        }
        else
        {
            _transform.Configure(mapBounds, Size);
        }
        FollowSelection(force: true);
        if (!_candidateSuppressedUntilInput)
        {
            _ = RefreshPointerResolution(RealtimeWorldProbeIds.TransformRefresh);
        }
        QueueRedraw();
    }

    private void EnsureKeyboardCursor()
    {
        if (_hasCanvasPointer || _presentation is null || _transform is null)
        {
            return;
        }
        CoreMapPoint? target = SelectionTarget(_presentation) ??
            _presentation.World.Nodes
                .Where(item => item.Commissioned)
                .OrderBy(item => item.NodeId, StringComparer.Ordinal)
                .Select(item => (CoreMapPoint?)item.Position)
                .FirstOrDefault();
        if (!target.HasValue)
        {
            return;
        }
        _pointer = target.Value;
        _lastCanvasPointer = Point(target.Value);
        _hasCanvasPointer = true;
        _ = ResolveCanvasPoint(
            RealtimeWorldProbeIds.KeyboardDefault,
            _lastCanvasPointer,
            target.Value);
    }

    private void FollowSelection(bool force = false)
    {
        if (_presentation is null || _transform is null || !force && string.Equals(
                _lastFollowSelectionId,
                _presentation.SelectedAssetId,
                StringComparison.Ordinal))
        {
            return;
        }
        _lastFollowSelectionId = _presentation.SelectedAssetId;
        CoreMapPoint? target = SelectionTarget(_presentation);
        if (target.HasValue)
        {
            float edgeMargin = 80f;
            SpatialNodeDefinition? selectedNode = _presentation.World.Nodes.FirstOrDefault(
                item => string.Equals(
                    item.NodeId,
                    _presentation.SelectedAssetId,
                    StringComparison.Ordinal));
            if (selectedNode is not null && TryGetDistrictVisual(
                    selectedNode,
                    out _,
                    out _,
                    out Vector2 districtHalfExtents))
            {
                edgeMargin = Math.Max(
                    edgeMargin,
                    Math.Max(districtHalfExtents.X, districtHalfExtents.Y) +
                    (30f * _accessibilityScale));
            }
            _transform.Follow(target.Value.XUnit, target.Value.YUnit, edgeMargin);
            _pointer = target;
            _lastCanvasPointer = Point(target.Value);
            _hasCanvasPointer = true;
            _ = ResolveCanvasPoint(
                RealtimeWorldProbeIds.SelectionTarget,
                _lastCanvasPointer,
                target.Value);
        }
    }

    private CoreMapPoint? SelectionTarget(
        RealtimeWorldPresentation presentation)
    {
        string? selectedId = presentation.SelectedAssetId;
        SpatialNodeDefinition? node = presentation.World.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, selectedId, StringComparison.Ordinal));
        if (node is not null)
        {
            CityDistrict? district = DistrictForNode(node.NodeId);
            if (district.HasValue)
            {
                return district.Value.Center;
            }
            return node.Position;
        }
        SpatialEdgeDefinition? edge = presentation.World.Edges.FirstOrDefault(item =>
            string.Equals(item.EdgeId, selectedId, StringComparison.Ordinal));
        if (edge is not null)
        {
            CoreMapPoint from = presentation.World.Nodes.Single(item => string.Equals(
                item.NodeId,
                edge.FromNodeId,
                StringComparison.Ordinal)).Position;
            CoreMapPoint to = presentation.World.Nodes.Single(item => string.Equals(
                item.NodeId,
                edge.ToNodeId,
                StringComparison.Ordinal)).Position;
            return new CoreMapPoint(
                checked((int)(((long)from.XUnit + to.XUnit) / 2)),
                checked((int)(((long)from.YUnit + to.YUnit) / 2)));
        }
        string? highlightedNode = presentation.Highlight?.NodeIds.FirstOrDefault();
        return highlightedNode is null
            ? null
            : presentation.World.Nodes.FirstOrDefault(item => string.Equals(
                item.NodeId,
                highlightedNode,
                StringComparison.Ordinal))?.Position;
    }

    private void UpdateAccessibility()
    {
        if (_presentation is null)
        {
            return;
        }
        string selection = _presentation.SelectedAssetId is string selected
            ? $"선택 {CandidateDisplayName(_presentation, selected)}"
            : "선택 없음";
        string candidate = _candidateCycle.Count == 0
            ? "후보 없음"
            : $"후보 {_candidateIndex + 1}/{_candidateCycle.Count} " +
              CandidateDisplayName(
                  _presentation,
                  _candidateCycle[_candidateIndex]);
        string feedback = string.IsNullOrWhiteSpace(_pointerFeedback.Message)
            ? "배치 결과 없음"
            : (_pointerFeedback.Accepted ? "승인" : "거절") + " " +
              _pointerFeedback.Message;
        AccessibilityName = Accessibility(_presentation);
        string guidance = _presentation.GuidanceTarget is { } target
            ? $"안내 대상 {target.Label}"
            : "안내 대상 없음";
        AccessibilityDescription =
            $"{selection}. {candidate}. {guidance}. {feedback}. " +
            "Q와 E로 겹친 후보를 바꾸고 Enter로 현재 후보를 선택합니다.";
    }

    private Vector2 Point(CoreMapPoint point) =>
        _transform!.WorldToCanvas(point.XUnit, point.YUnit);

    private CoreMapPoint ToWorld(Vector2 point)
    {
        MapWorldPosition world = _transform!.CanvasToWorld(point);
        return new CoreMapPoint(
            (int)Math.Round(world.X, MidpointRounding.AwayFromZero),
            (int)Math.Round(world.Y, MidpointRounding.AwayFromZero));
    }

    private float NodeRadius(
        SpatialWorldDefinition world,
        SpatialNodeDefinition node) =>
        (world.NodeClasses.Single(item =>
            string.Equals(item.ClassId, node.ClassId, StringComparison.Ordinal)).Kind switch
        {
            SpatialNodeKind.Substation => 13f,
            SpatialNodeKind.Pole => 7f,
            _ => 9f,
        }) * _accessibilityScale;

    private static RealtimeWorldAssetStatus? Status(
        RealtimeWorldPresentation presentation,
        string id) => presentation.AssetStatuses.FirstOrDefault(item =>
        string.Equals(item.AssetId, id, StringComparison.Ordinal));

    private static Color StateColor(RealtimeWorldAssetState? state) => state switch
    {
        RealtimeWorldAssetState.Planned or
            RealtimeWorldAssetState.Building or
            RealtimeWorldAssetState.AuthoredUnavailable => Planned,
        RealtimeWorldAssetState.Emergency => Emergency,
        RealtimeWorldAssetState.ProtectiveOutage => Outage,
        RealtimeWorldAssetState.OverLimit => Danger,
        _ => Normal,
    };

    private static double SegmentDistanceSquared(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 axis = end - start;
        if (axis.LengthSquared() <= 0.001f)
        {
            return point.DistanceSquaredTo(start);
        }
        float t = Math.Clamp((point - start).Dot(axis) / axis.LengthSquared(), 0f, 1f);
        return point.DistanceSquaredTo(start + axis * t);
    }

    private static string Accessibility(RealtimeWorldPresentation presentation)
    {
        int emergency = presentation.AssetStatuses.Count(item =>
            item.State == RealtimeWorldAssetState.Emergency);
        int outage = presentation.AssetStatuses.Count(item =>
            item.State == RealtimeWorldAssetState.ProtectiveOutage);
        int authoredUnavailable = presentation.AssetStatuses.Count(item =>
            item.AuthoredUnavailable);
        int building = presentation.AssetStatuses.Count(item =>
            item.State == RealtimeWorldAssetState.Building);
        string forecastRisk = presentation.ForecastRiskAreaIds.Count == 0
            ? "범람 예고 선택 없음"
            : "범람 예고 점선 윤곽 " + string.Join(
                ", ",
                presentation.World.RiskAreas
                    .Where(item => presentation.ForecastRiskAreaIds.Contains(
                        item.RiskAreaId,
                        StringComparer.Ordinal))
                    .Select(item => item.DisplayName));
        string activeRisk = presentation.ActiveRiskAreaIds.Count == 0
            ? "활성 범람 없음"
            : "활성 범람 실선 채움 " + string.Join(
                ", ",
                presentation.World.RiskAreas
                    .Where(item => presentation.ActiveRiskAreaIds.Contains(
                        item.RiskAreaId,
                        StringComparer.Ordinal))
                    .Select(item => item.DisplayName));
        return $"청류시 실시간 전력망 · 후보는 거리와 안정된 순서로 정렬 · " +
               $"공사 중 {building}곳 · 계획 사용불가 {authoredUnavailable}곳 · " +
               $"비상 {emergency}곳 · 보호정지 {outage}곳 · " +
               $"{forecastRisk} · {activeRisk}";
    }

    private static string CandidateDisplayName(
        RealtimeWorldPresentation presentation,
        string id)
    {
        if (RealtimeWorldIds.IsDraftPoint(id))
        {
            return "초안 경로점";
        }
        SpatialNodeDefinition? node = presentation.World.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, id, StringComparison.Ordinal));
        if (node is not null)
        {
            return $"{node.DisplayName} · {StatusLabel(Status(presentation, id))}";
        }
        SpatialEdgeDefinition? edge = presentation.World.Edges.FirstOrDefault(item =>
            string.Equals(item.EdgeId, id, StringComparison.Ordinal));
        if (edge is not null)
        {
            string lineName = presentation.World.LineClasses.FirstOrDefault(item =>
                string.Equals(item.ClassId, edge.LineClassId, StringComparison.Ordinal))
                ?.DisplayName ?? "배전선";
            return $"{lineName} 구간 · {StatusLabel(Status(presentation, id))}";
        }
        return "지도 후보";
    }

    private static string StatusLabel(RealtimeWorldAssetStatus? status)
    {
        if (status is null)
        {
            return "정상";
        }
        if (status.ProtectiveOutage && status.AuthoredUnavailable)
        {
            return "보호정지 · 계획 사용불가 겹침";
        }
        return status.State switch
        {
            RealtimeWorldAssetState.Planned => "계획",
            RealtimeWorldAssetState.Building => "공사 중",
            RealtimeWorldAssetState.AuthoredUnavailable => "계획 사용불가",
            RealtimeWorldAssetState.Emergency => "비상 운전",
            RealtimeWorldAssetState.ProtectiveOutage => "보호정지",
            RealtimeWorldAssetState.OverLimit => "한계 초과",
            _ => "정상",
        };
    }

    private static string StatusLabel(RealtimeWorldAssetState? state) =>
        StatusLabel(state is null
            ? null
            : new RealtimeWorldAssetStatus(
                "STATUS_PREVIEW",
                state.Value,
                0,
                0,
                0,
                0,
                0,
                AuthoredUnavailable:
                    state == RealtimeWorldAssetState.AuthoredUnavailable,
                ProtectiveOutage:
                    state == RealtimeWorldAssetState.ProtectiveOutage));

    private static RealtimePlaceholderStateCue StateCue(
        RealtimeWorldAssetState? state) => state switch
    {
        RealtimeWorldAssetState.AuthoredUnavailable =>
            RealtimePlaceholderStateCue.AuthoredUnavailableBars,
        RealtimeWorldAssetState.Emergency =>
            RealtimePlaceholderStateCue.EmergencyTriangle,
        RealtimeWorldAssetState.ProtectiveOutage =>
            RealtimePlaceholderStateCue.ProtectiveOutageCross,
        RealtimeWorldAssetState.OverLimit =>
            RealtimePlaceholderStateCue.OverLimitDiamond,
        _ => RealtimePlaceholderStateCue.None,
    };
}
