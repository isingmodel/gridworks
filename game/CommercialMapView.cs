using System;
using System.Collections.Generic;
using System.Linq;
using Gridworks.Core.Release.V2;
using Godot;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game;

internal enum CommercialFacilityState
{
    Waiting,
    Supplied,
    Deferred,
    Unavailable,
}

internal sealed record CommercialFacilityPresentation(
    string NodeId,
    CommercialFacilityState State,
    string StatusText);

internal sealed record CommercialMapPresentation(
    ConstructionSnapshot Snapshot,
    CoreMapPoint? PointerPoint,
    bool PointerAccepted,
    string PointerMessage,
    string ToolLabel,
    int? PointerFootprintRadiusUnit,
    bool NodeSnapEnabled,
    ThermalIntervalResult? ThermalInterval,
    string? SelectedThermalAssetId,
    string? SelectedDemandNodeId,
    IReadOnlyList<string> SelectedPathNodeIds,
    IReadOnlyList<string> SelectedPathEdgeIds,
    IReadOnlyList<string> ActiveRiskAreaIds,
    IReadOnlyList<CommercialFacilityPresentation> Facilities,
    int ChapterIndex,
    bool ReduceMotion);

internal readonly record struct CommercialDraftPointDrag(int PointIndex, CoreMapPoint Position);

internal sealed partial class CommercialMapView : Control
{
    private enum AtomicCitySpriteKind
    {
        WorkerHouseA,
        WorkerHouseB,
        WorkerHouseC,
        RowShopA,
        WorkshopA,
        SmallWarehouseA,
        HospitalMainA,
        HospitalServiceA,
        PumpHouseA,
        WaterTankA,
        RetainingWallA,
        StreetLampA,
    }

    private enum AtomicRoadSpriteKind
    {
        StraightNorthWestSouthEast,
        StraightNorthEastSouthWest,
        CornerNorthEast,
        TJunction,
        CrossJunction,
        ServiceYard,
    }

    private readonly record struct AtomicCityInstanceSpec(
        AtomicCitySpriteKind Kind,
        int XUnit,
        int YUnit,
        float MaxSide,
        float Alpha = 0.96f);

    private readonly record struct AtomicRoadInstanceSpec(
        AtomicRoadSpriteKind Kind,
        int XUnit,
        int YUnit,
        float MaxSide,
        float Alpha = 0.92f);

    private static readonly Color Background = Color.FromHtml("071319");
    private static readonly Color Land = Color.FromHtml("142724");
    private static readonly Color Water = Color.FromHtml("123b4b");
    private static readonly Color WaterLine = Color.FromHtml("397389");
    private static readonly Color Building = Color.FromHtml("5b6663");
    private static readonly Color BuildingEdge = Color.FromHtml("879590");
    private static readonly Color Risk = Color.FromHtml("c36568");
    private static readonly Color IdleLine = Color.FromHtml("869895");
    private static readonly Color CommissionedLine = Color.FromHtml("55b8d8");
    private static readonly Color EmergencyLine = Color.FromHtml("f0b75e");
    private static readonly Color OutageLine = Color.FromHtml("e56e73");
    private static readonly Color OverLimitLine = Color.FromHtml("ff845d");
    private static readonly Color Planned = Color.FromHtml("efb75d");
    private static readonly Color Invalid = Color.FromHtml("ed756e");
    private static readonly Color Text = Color.FromHtml("e6eff0");
    private static readonly Color Muted = Color.FromHtml("91a3a1");
    private static readonly Color Focus = Color.FromHtml("f4d27c");
    // Whole-map mode keeps close authored terminals independently selectable.
    // A 68 px radius lets their hover regions overlap for Q/E disambiguation
    // at supported UI 125%;
    // zoomed construction views naturally narrow the corresponding world radius.
    private const float CandidateRadiusPixel = 68f;
    private const float KeyboardFollowMarginPixel = 72f;
    private const int KeyboardSmallStepUnit = 100;
    private const int KeyboardLargeStepUnit = 500;
    private const int GroundTileWorldUnit = 400;
    private const float BuildRailLeft = 10f;
    private const float BuildRailTop = 300f;
    private const float BuildRailSlotWidth = 156f;
    private const float BuildRailSlotHeight = 124f;
    private const float BuildRailGap = 8f;
    private static readonly CoreMapPoint[] ReferenceRiverControlPoints =
    [
        new(1332, 1940),
        new(1296, 1797),
        new(1256, 1658),
        new(1227, 1509),
        new(1151, 1370),
        new(1104, 1283),
        new(1095, 1194),
        new(1091, 1127),
        new(1095, 1069),
        new(1127, 1020),
        new(1145, 948),
        new(1171, 903),
        new(1185, 854),
        new(1207, 814),
    ];

    // Step 1 city composition authority. Every record resolves to one PNG that
    // contains exactly one building or one prop. No raster owns a street network,
    // parcel, district, cluster, neighbourhood, or city silhouette.
    private static readonly AtomicCityInstanceSpec[] AtomicCityInstances =
    [
        // East residential block: 30 individually placed buildings.
        new(AtomicCitySpriteKind.WorkerHouseA, 2700, 600, 66f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2780, 600, 62f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2860, 600, 70f),
        new(AtomicCitySpriteKind.RowShopA, 2940, 600, 68f),
        new(AtomicCitySpriteKind.WorkerHouseA, 3020, 600, 64f),
        new(AtomicCitySpriteKind.WorkerHouseB, 3100, 600, 60f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2700, 690, 62f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2780, 690, 68f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2860, 690, 64f),
        new(AtomicCitySpriteKind.RowShopA, 2940, 690, 70f),
        new(AtomicCitySpriteKind.WorkerHouseB, 3020, 690, 62f),
        new(AtomicCitySpriteKind.WorkerHouseC, 3100, 690, 68f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2700, 780, 70f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2780, 780, 64f),
        new(AtomicCitySpriteKind.RowShopA, 2860, 780, 70f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2940, 780, 62f),
        new(AtomicCitySpriteKind.WorkerHouseC, 3020, 780, 68f),
        new(AtomicCitySpriteKind.WorkerHouseA, 3100, 780, 64f),
        new(AtomicCitySpriteKind.RowShopA, 2700, 870, 70f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2780, 870, 62f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2860, 870, 64f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2940, 870, 68f),
        new(AtomicCitySpriteKind.WorkerHouseB, 3020, 870, 62f),
        new(AtomicCitySpriteKind.RowShopA, 3100, 870, 70f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2700, 960, 64f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2780, 960, 68f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2860, 960, 62f),
        new(AtomicCitySpriteKind.RowShopA, 2940, 960, 70f),
        new(AtomicCitySpriteKind.WorkerHouseA, 3020, 960, 64f),
        new(AtomicCitySpriteKind.WorkerHouseC, 3100, 960, 68f),

        // Hospital campus: each building and utility object is independent.
        new(AtomicCitySpriteKind.HospitalMainA, 2540, 1390, 170f, 0.99f),
        new(AtomicCitySpriteKind.HospitalServiceA, 2750, 1380, 92f),
        new(AtomicCitySpriteKind.HospitalServiceA, 2900, 1410, 88f),
        new(AtomicCitySpriteKind.HospitalServiceA, 2750, 1580, 90f),
        new(AtomicCitySpriteKind.HospitalServiceA, 2920, 1600, 86f),
        new(AtomicCitySpriteKind.WaterTankA, 3030, 1530, 126f),
        new(AtomicCitySpriteKind.PumpHouseA, 2450, 1600, 88f),

        // West industrial block: nine separate low structures/props.
        new(AtomicCitySpriteKind.WorkshopA, 160, 1330, 92f),
        new(AtomicCitySpriteKind.WorkshopA, 320, 1340, 88f),
        new(AtomicCitySpriteKind.WorkshopA, 480, 1330, 90f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 160, 1490, 96f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 330, 1500, 98f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 500, 1490, 94f),
        new(AtomicCitySpriteKind.PumpHouseA, 180, 1650, 82f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 370, 1650, 94f),
        new(AtomicCitySpriteKind.RetainingWallA, 540, 1600, 90f),

        // East block street furniture and retaining edges.
        new(AtomicCitySpriteKind.StreetLampA, 2680, 620, 44f),
        new(AtomicCitySpriteKind.StreetLampA, 2760, 720, 44f),
        new(AtomicCitySpriteKind.StreetLampA, 2840, 620, 44f),
        new(AtomicCitySpriteKind.StreetLampA, 2920, 720, 44f),
        new(AtomicCitySpriteKind.StreetLampA, 3000, 620, 44f),
        new(AtomicCitySpriteKind.StreetLampA, 3080, 720, 44f),
        new(AtomicCitySpriteKind.StreetLampA, 2700, 880, 44f),
        new(AtomicCitySpriteKind.StreetLampA, 2800, 980, 44f),
        new(AtomicCitySpriteKind.StreetLampA, 2900, 880, 44f),
        new(AtomicCitySpriteKind.StreetLampA, 3000, 980, 44f),
        new(AtomicCitySpriteKind.StreetLampA, 3100, 880, 44f),
        new(AtomicCitySpriteKind.StreetLampA, 3050, 1010, 44f),
        new(AtomicCitySpriteKind.RetainingWallA, 2700, 1020, 94f),
        new(AtomicCitySpriteKind.RetainingWallA, 2820, 1020, 94f),
        new(AtomicCitySpriteKind.RetainingWallA, 2940, 1020, 94f),
        new(AtomicCitySpriteKind.RetainingWallA, 3060, 1020, 94f),
        new(AtomicCitySpriteKind.RetainingWallA, 2680, 760, 90f),
        new(AtomicCitySpriteKind.RetainingWallA, 3120, 840, 90f),

        // Hospital and west-block furniture complete the 80-instance gate.
        new(AtomicCitySpriteKind.StreetLampA, 2400, 1340, 48f),
        new(AtomicCitySpriteKind.StreetLampA, 2520, 1320, 48f),
        new(AtomicCitySpriteKind.StreetLampA, 2660, 1320, 48f),
        new(AtomicCitySpriteKind.StreetLampA, 2800, 1320, 48f),
        new(AtomicCitySpriteKind.StreetLampA, 2940, 1320, 48f),
        new(AtomicCitySpriteKind.StreetLampA, 3020, 1420, 48f),
        new(AtomicCitySpriteKind.StreetLampA, 3020, 1580, 48f),
        new(AtomicCitySpriteKind.StreetLampA, 2440, 1720, 48f),
        new(AtomicCitySpriteKind.RetainingWallA, 2500, 1720, 104f),
        new(AtomicCitySpriteKind.RetainingWallA, 2680, 1720, 104f),
        new(AtomicCitySpriteKind.RetainingWallA, 2860, 1720, 104f),
        new(AtomicCitySpriteKind.RetainingWallA, 3000, 1720, 104f),
        new(AtomicCitySpriteKind.StreetLampA, 120, 1280, 46f),
        new(AtomicCitySpriteKind.StreetLampA, 260, 1280, 46f),
        new(AtomicCitySpriteKind.StreetLampA, 420, 1280, 46f),
        new(AtomicCitySpriteKind.StreetLampA, 580, 1280, 46f),
    ];

    private static readonly AtomicRoadInstanceSpec[] AtomicRoadInstances =
    [
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2700, 650, 136f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2780, 650, 136f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2860, 650, 136f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2940, 650, 136f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 3020, 650, 136f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 3100, 650, 136f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2700, 850, 136f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2780, 850, 136f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2860, 850, 136f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2940, 850, 136f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 3020, 850, 136f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 3100, 850, 136f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 2740, 750, 136f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 2820, 750, 136f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 2980, 750, 136f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 3060, 750, 136f),
        new(AtomicRoadSpriteKind.CrossJunction, 2860, 750, 140f),
        new(AtomicRoadSpriteKind.CrossJunction, 3020, 750, 140f),
        new(AtomicRoadSpriteKind.CornerNorthEast, 2700, 1000, 140f),
        new(AtomicRoadSpriteKind.CornerNorthEast, 3100, 1000, 140f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2400, 1500, 152f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2550, 1500, 152f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2700, 1500, 152f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2850, 1500, 152f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 3000, 1500, 152f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 2600, 1360, 148f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 2600, 1640, 148f),
        new(AtomicRoadSpriteKind.TJunction, 2750, 1500, 152f),
        new(AtomicRoadSpriteKind.CrossJunction, 2900, 1500, 152f),
        new(AtomicRoadSpriteKind.ServiceYard, 2480, 1500, 164f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 180, 1420, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 340, 1420, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 500, 1420, 142f),
        new(AtomicRoadSpriteKind.TJunction, 330, 1550, 146f),
        new(AtomicRoadSpriteKind.ServiceYard, 180, 1580, 154f),
        new(AtomicRoadSpriteKind.CornerNorthEast, 520, 1580, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1600, 900, 164f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 1800, 1000, 164f),
        new(AtomicRoadSpriteKind.CrossJunction, 2000, 1100, 168f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2200, 1200, 164f),
    ];

    [Export]
    public Texture2D? GroundAsphaltTile { get; set; }

    [Export]
    public Texture2D? GroundScrubTile { get; set; }

    [Export]
    public Texture2D? GroundConcreteTile { get; set; }

    [Export]
    public Texture2D? GroundGravelTile { get; set; }

    [Export]
    public Texture2D? G3GroundRubbleMixBTile { get; set; }

    [Export]
    public Texture2D? G3GroundRubbleReliefCTile { get; set; }

    [Export]
    public Texture2D? RiverWaterTile { get; set; }

    [Export]
    public Texture2D? G3RiverWaterSurfaceTile { get; set; }

    [Export]
    public Texture2D? RoadStraightNorthWestSouthEastATile { get; set; }

    [Export]
    public Texture2D? RoadStraightNorthEastSouthWestATile { get; set; }

    [Export]
    public Texture2D? RoadCornerNorthEastATile { get; set; }

    [Export]
    public Texture2D? RoadTJunctionATile { get; set; }

    [Export]
    public Texture2D? RoadCrossJunctionATile { get; set; }

    [Export]
    public Texture2D? ServiceYardATile { get; set; }

    [Export]
    public Texture2D? StandardPoleSprite { get; set; }

    [Export]
    public Texture2D? ReinforcedPoleSprite { get; set; }

    [Export]
    public Texture2D? BridgeFoundationSprite { get; set; }

    [Export]
    public Texture2D? IndustrialRoadBridgeASprite { get; set; }

    [Export]
    public Texture2D? TallThermalPowerStationBSprite { get; set; }

    [Export]
    public Texture2D? SubstationSprite { get; set; }

    [Export]
    public Texture2D? ChunkySwitchingSubstationBSprite { get; set; }

    [Export]
    public Texture2D? AtomicWorkerHouseASprite { get; set; }

    [Export]
    public Texture2D? AtomicWorkerHouseBSprite { get; set; }

    [Export]
    public Texture2D? AtomicWorkerHouseCSprite { get; set; }

    [Export]
    public Texture2D? AtomicRowShopASprite { get; set; }

    [Export]
    public Texture2D? AtomicWorkshopASprite { get; set; }

    [Export]
    public Texture2D? AtomicSmallWarehouseASprite { get; set; }

    [Export]
    public Texture2D? AtomicHospitalMainASprite { get; set; }

    [Export]
    public Texture2D? AtomicHospitalServiceASprite { get; set; }

    [Export]
    public Texture2D? AtomicPumpHouseASprite { get; set; }

    [Export]
    public Texture2D? AtomicWaterTankASprite { get; set; }

    [Export]
    public Texture2D? AtomicRetainingWallASprite { get; set; }

    [Export]
    public Texture2D? AtomicStreetLampASprite { get; set; }

    [Export]
    public Texture2D? G3RiverBankRockSegmentASprite { get; set; }

    [Export]
    public Texture2D? G3RiverBankInnerBendASprite { get; set; }

    [Export]
    public Texture2D? G3RiverBankOuterBendASprite { get; set; }

    [Export]
    public Texture2D? RiverCurrentReflectionASprite { get; set; }

    [Export]
    public Texture2D? UiChromeFrameTexture { get; set; }

    private CommercialMapPresentation? _presentation;
    private CommercialMapTransform? _transform;
    private CoreMapPoint _keyboardPoint;
    private CoreMapPoint? _pointerPoint;
    private readonly List<string> _candidateNodeIds = [];
    private int _candidateIndex;
    private bool _spaceHeld;
    private bool _panning;
    private MouseButton _panButton;
    private Vector2 _lastPanPosition;
    private bool _draggingDraftPoint;
    private int _draggedDraftPointIndex = -1;
    private double _animationSeconds;
    private double _redrawAccumulator;

    public event Action<CoreMapPoint?, string?>? PointerChanged;

    public event Action<CoreMapPoint, string?>? PointRequested;

    public event Action? UndoRequested;

    public event Action<CommercialDraftPointDrag>? DraftPointMoveRequested;

    public event Action<CommercialDraftPointDrag?>? DraftPointDragPreviewChanged;

    public event Action? CameraChanged;

    public event Action<CommercialPanelAction>? BuildRailActionRequested;

    public int ZoomIndex => _transform?.ZoomIndex ?? 0;

    public string ZoomLabel => _transform?.ZoomLabel ?? "전체 보기";

    public Vector2 CameraCenter => _transform?.Center ?? Vector2.Zero;

    public CoreMapPoint KeyboardPoint => _keyboardPoint;

    public string? SelectedCandidateId => _candidateNodeIds.Count == 0
        ? null
        : _candidateNodeIds[_candidateIndex];

    public IReadOnlyList<string> CandidateNodeIds => _candidateNodeIds.AsReadOnly();

    public bool HasIndividualTileAssets =>
        GroundAsphaltTile is not null &&
        GroundScrubTile is not null &&
        GroundConcreteTile is not null &&
        GroundGravelTile is not null &&
        G3GroundRubbleMixBTile is not null &&
        G3GroundRubbleReliefCTile is not null &&
        RiverWaterTile is not null &&
        G3RiverWaterSurfaceTile is not null &&
        RoadStraightNorthWestSouthEastATile is not null &&
        RoadStraightNorthEastSouthWestATile is not null &&
        RoadCornerNorthEastATile is not null &&
        RoadTJunctionATile is not null &&
        RoadCrossJunctionATile is not null &&
        ServiceYardATile is not null;

    public bool HasIndividualObjectAssets =>
        StandardPoleSprite is not null &&
        ReinforcedPoleSprite is not null &&
        BridgeFoundationSprite is not null &&
        IndustrialRoadBridgeASprite is not null &&
        TallThermalPowerStationBSprite is not null &&
        SubstationSprite is not null &&
        ChunkySwitchingSubstationBSprite is not null &&
        G3RiverBankRockSegmentASprite is not null &&
        G3RiverBankInnerBendASprite is not null &&
        G3RiverBankOuterBendASprite is not null &&
        RiverCurrentReflectionASprite is not null &&
        HasAtomicCityAssets;

    public bool HasAtomicCityAssets =>
        AtomicWorkerHouseASprite is not null &&
        AtomicWorkerHouseBSprite is not null &&
        AtomicWorkerHouseCSprite is not null &&
        AtomicRowShopASprite is not null &&
        AtomicWorkshopASprite is not null &&
        AtomicSmallWarehouseASprite is not null &&
        AtomicHospitalMainASprite is not null &&
        AtomicHospitalServiceASprite is not null &&
        AtomicPumpHouseASprite is not null &&
        AtomicWaterTankASprite is not null &&
        AtomicRetainingWallASprite is not null &&
        AtomicStreetLampASprite is not null;

    public int AtomicCityAssetCount => AtomicCityTextures().Count(texture => texture is not null);

    public int AtomicRoadTileAssetCount => AtomicRoadTextures().Count(texture => texture is not null);

    public int AtomicCityInstanceCount => AtomicCityInstances.Length;

    public int AtomicRoadInstanceCount => AtomicRoadInstances.Length;

    public int AtomicWorldInstanceCount => AtomicCityInstanceCount + AtomicRoadInstanceCount;

    public int IndividualArtAssetCount =>
        IndividualTileAssetCount + IndividualObjectAssetCount;

    public string? CurrentDraftSpriteClassId
    {
        get
        {
            if (_presentation?.Snapshot.NodeDraft is NodeDraftSnapshot nodeDraft &&
                DraftNodeSprite(nodeDraft.NodeClassId).Texture is not null)
            {
                return nodeDraft.NodeClassId;
            }
            if (_presentation?.Snapshot.LineDraft is LineDraftSnapshot lineDraft &&
                lineDraft.IntermediatePoints.Count > 0 &&
                DraftPoleSprite(lineDraft.PoleClassId).Texture is not null)
            {
                return lineDraft.PoleClassId;
            }
            return null;
        }
    }

    public int IndividualTileAssetCount =>
        new Texture2D?[]
        {
            GroundAsphaltTile,
            GroundScrubTile,
            GroundConcreteTile,
            GroundGravelTile,
            G3GroundRubbleMixBTile,
            G3GroundRubbleReliefCTile,
            RiverWaterTile,
            G3RiverWaterSurfaceTile,
            RoadStraightNorthWestSouthEastATile,
            RoadStraightNorthEastSouthWestATile,
            RoadCornerNorthEastATile,
            RoadTJunctionATile,
            RoadCrossJunctionATile,
            ServiceYardATile,
        }.Count(texture => texture is not null);

    public int IndividualObjectAssetCount =>
        new Texture2D?[]
        {
            StandardPoleSprite,
            ReinforcedPoleSprite,
            BridgeFoundationSprite,
            IndustrialRoadBridgeASprite,
            TallThermalPowerStationBSprite,
            SubstationSprite,
            ChunkySwitchingSubstationBSprite,
            G3RiverBankRockSegmentASprite,
            G3RiverBankInnerBendASprite,
            G3RiverBankOuterBendASprite,
            RiverCurrentReflectionASprite,
            AtomicWorkerHouseASprite,
            AtomicWorkerHouseBSprite,
            AtomicWorkerHouseCSprite,
            AtomicRowShopASprite,
            AtomicWorkshopASprite,
            AtomicSmallWarehouseASprite,
            AtomicHospitalMainASprite,
            AtomicHospitalServiceASprite,
            AtomicPumpHouseASprite,
            AtomicWaterTankASprite,
            AtomicRetainingWallASprite,
            AtomicStreetLampASprite,
        }.Count(texture => texture is not null);

    public bool IsDraggingDraftPoint => _draggingDraftPoint;

    public int DraggedDraftPointIndex => _draggedDraftPointIndex;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        TextureRepeat = TextureRepeatEnum.Enabled;
        AccessibilityDescription =
            "청류시 자유 배치 지도. 건물 한 채와 도로 한 조각 단위의 원자 자산이 v2 좌표에 배치됩니다. " +
            "방향키로 커서를 움직이고 Enter로 선택합니다. Q와 E로 가까운 접속점을 바꿉니다.";
        MouseExited += () =>
        {
            if (!_panning)
            {
                SetPointer(null);
            }
        };
        FocusEntered += QueueRedraw;
        FocusExited += () =>
        {
            _spaceHeld = false;
            EndPan();
            QueueRedraw();
        };
        Resized += OnResized;
    }

    public override void _Process(double delta)
    {
        if (_presentation?.ReduceMotion != false)
        {
            return;
        }
        _animationSeconds += delta;
        _redrawAccumulator += delta;
        if (_redrawAccumulator >= 1d / 12d)
        {
            _redrawAccumulator = 0d;
            QueueRedraw();
        }
    }

    public void SetPresentation(CommercialMapPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        _presentation = presentation;
        SetProcess(!presentation.ReduceMotion);
        MapBounds bounds = presentation.Snapshot.World.Bounds;
        var gameBounds = new CommercialMapBounds(
            bounds.MinXUnit,
            bounds.MaxXUnit,
            bounds.MinYUnit,
            bounds.MaxYUnit);
        if (_transform is null)
        {
            _transform = new CommercialMapTransform(gameBounds, Size);
            _keyboardPoint = InitialKeyboardPoint(presentation.Snapshot.World);
            _pointerPoint = _keyboardPoint;
        }
        else
        {
            _transform.Configure(gameBounds, Size);
        }

        if (presentation.PointerPoint is CoreMapPoint presentedPointer)
        {
            _pointerPoint = presentedPointer;
        }
        RefreshCandidates(notify: false);
        AccessibilityName = BuildAccessibilityName(presentation);
        QueueRedraw();
    }

    public Vector2 ViewportPointForWorld(CoreMapPoint point)
    {
        CommercialMapTransform transform = RequireTransform();
        Vector2 local = transform.WorldToCanvas(point.XUnit, point.YUnit);
        return GetGlobalTransformWithCanvas() * local;
    }

    public CommercialWorldPosition WorldAtViewportPoint(Vector2 viewportPoint)
    {
        Vector2 local = GetGlobalTransformWithCanvas().AffineInverse() * viewportPoint;
        return RequireTransform().CanvasToWorld(local);
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (_presentation is null || _transform is null)
        {
            return;
        }

        switch (inputEvent)
        {
            case InputEventMouseButton button when
                button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown &&
                button.Pressed:
                int direction = button.ButtonIndex == MouseButton.WheelUp ? 1 : -1;
                ZoomBy(direction, button.Position);
                AcceptEvent();
                return;

            case InputEventMouseButton button when IsPanButton(button):
                if (button.Pressed)
                {
                    BeginPan(button.ButtonIndex, button.Position);
                }
                else if (_panning && button.ButtonIndex == _panButton)
                {
                    EndPan();
                }
                AcceptEvent();
                return;

            case InputEventMouseMotion motion when _panning:
                Vector2 delta = motion.Position - _lastPanPosition;
                _lastPanPosition = motion.Position;
                _transform.PanByCanvasDelta(delta);
                RefreshCandidates(notify: true);
                CameraChanged?.Invoke();
                QueueRedraw();
                AcceptEvent();
                return;

            case InputEventMouseMotion motion when _draggingDraftPoint:
                if (TryMapPoint(motion.Position, out CoreMapPoint draggedPoint))
                {
                    _pointerPoint = draggedPoint;
                    RefreshCandidates(notify: false);
                    DraftPointDragPreviewChanged?.Invoke(new CommercialDraftPointDrag(
                        _draggedDraftPointIndex,
                        draggedPoint));
                    QueueRedraw();
                }
                AcceptEvent();
                return;

            case InputEventMouseMotion motion:
                SetPointer(TryMapPoint(motion.Position, out CoreMapPoint point) ? point : null);
                AcceptEvent();
                return;

            case InputEventMouseButton button when
                button.ButtonIndex == MouseButton.Left &&
                button.Pressed &&
                TryHandleBuildRailClick(button.Position):
                AcceptEvent();
                return;

            case InputEventMouseButton button when
                button.ButtonIndex == MouseButton.Left &&
                button.Pressed &&
                TryBeginDraftPointDrag(button.Position):
                AcceptEvent();
                return;

            case InputEventMouseButton button when
                button.ButtonIndex == MouseButton.Left &&
                !button.Pressed &&
                _draggingDraftPoint:
                _draggingDraftPoint = false;
                if (TryMapPoint(button.Position, out CoreMapPoint movedPoint))
                {
                    DraftPointMoveRequested?.Invoke(new CommercialDraftPointDrag(
                        _draggedDraftPointIndex,
                        movedPoint));
                }
                _draggedDraftPointIndex = -1;
                DraftPointDragPreviewChanged?.Invoke(null);
                AcceptEvent();
                return;

            case InputEventMouseButton button when
                button.ButtonIndex == MouseButton.Right && button.Pressed:
                UndoRequested?.Invoke();
                AcceptEvent();
                return;

            case InputEventMouseButton button when
                button.ButtonIndex == MouseButton.Left && button.Pressed:
                if (TryMapPoint(button.Position, out CoreMapPoint clicked))
                {
                    GrabFocus();
                    _keyboardPoint = clicked;
                    SetPointer(clicked);
                    PointRequested?.Invoke(clicked, SelectedCandidateId);
                }
                AcceptEvent();
                return;

            case InputEventKey key:
                HandleKey(key);
                return;
        }
    }

    public override void _Draw()
    {
        if (_presentation is null || _transform is null)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), Background);
            return;
        }

        ConstructionSnapshot snapshot = _presentation.Snapshot;
        DrawRect(new Rect2(Vector2.Zero, Size), Background);
        DrawMapGround();
        DrawRubbleMaterialPatches();
        DrawRoadFabric();
        DrawAtomicRoadTiles();
        // Water belongs below the physical district/object layer. Drawing it after
        // the city made the channel read as a flat blue UI ribbon laid over roofs.
        DrawWaterTerrain(snapshot.World);
        DrawTerrain(snapshot.World);
        DrawAtomicCity();
        DrawRiskAreas(snapshot.World, _presentation.ActiveRiskAreaIds);
        DrawChapterAtmosphere(_presentation.ChapterIndex, _presentation.ReduceMotion);
        DrawReservedRouteCorridor(snapshot.World);
        DrawEdges(snapshot.World, _presentation.ThermalInterval);
        DrawSelectedDemandPath(snapshot.World, _presentation);
        DrawUnavailableMarks(snapshot.World, _presentation.ThermalInterval);
        DrawLineDraft(snapshot);
        DrawNodes(
            snapshot.World,
            _presentation.ThermalInterval,
            _presentation.SelectedThermalAssetId,
            _presentation.SelectedDemandNodeId,
            _presentation.Facilities);
        DrawNodeDraft(snapshot);
        DrawPointer(_presentation);
        DrawMapLegend();
        DrawBuildRail();
    }

    private void HandleKey(InputEventKey key)
    {
        if (key.Keycode == Key.Tab || key.PhysicalKeycode == Key.Tab)
        {
            return;
        }

        if (key.Keycode == Key.Space || key.PhysicalKeycode == Key.Space)
        {
            _spaceHeld = key.Pressed;
            AcceptEvent();
            return;
        }
        if (!key.Pressed || key.Echo)
        {
            return;
        }

        Key physical = key.PhysicalKeycode;
        if (physical == Key.Q || physical == Key.E)
        {
            CycleCandidate(physical == Key.Q ? -1 : 1);
            AcceptEvent();
            return;
        }
        if (key.Keycode is Key.Plus or Key.Equal or Key.KpAdd)
        {
            ZoomBy(1, KeyboardAnchor());
            AcceptEvent();
            return;
        }
        if (key.Keycode is Key.Minus or Key.KpSubtract)
        {
            ZoomBy(-1, KeyboardAnchor());
            AcceptEvent();
            return;
        }
        if (key.Keycode == Key.Home)
        {
            RequireTransform().Home();
            RefreshCandidates(notify: true);
            CameraChanged?.Invoke();
            QueueRedraw();
            AcceptEvent();
            return;
        }
        if (key.Keycode is Key.Key1 or Key.Kp1)
        {
            BuildRailActionRequested?.Invoke(CommercialPanelAction.StartLine);
            AcceptEvent();
            return;
        }
        if (key.Keycode is Key.Key2 or Key.Kp2)
        {
            BuildRailActionRequested?.Invoke(CommercialPanelAction.PlaceSubstation);
            AcceptEvent();
            return;
        }
        if (key.Keycode is Key.Key3 or Key.Kp3)
        {
            BuildRailActionRequested?.Invoke(CommercialPanelAction.CycleLineClass);
            AcceptEvent();
            return;
        }
        if (key.Keycode == Key.Backspace)
        {
            UndoRequested?.Invoke();
            AcceptEvent();
            return;
        }
        if (key.Keycode is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            MoveKeyboardCursor(key);
            AcceptEvent();
            return;
        }
        if (key.Keycode is Key.Enter or Key.KpEnter)
        {
            SetPointer(_keyboardPoint);
            PointRequested?.Invoke(_keyboardPoint, SelectedCandidateId);
            AcceptEvent();
        }
    }

    private void MoveKeyboardCursor(InputEventKey key)
    {
        int step = key.ShiftPressed ? KeyboardLargeStepUnit : KeyboardSmallStepUnit;
        MapBounds bounds = _presentation!.Snapshot.World.Bounds;
        int x = _keyboardPoint.XUnit;
        int y = _keyboardPoint.YUnit;
        switch (key.Keycode)
        {
            case Key.Left: x = SaturatingAdd(x, -step); break;
            case Key.Right: x = SaturatingAdd(x, step); break;
            case Key.Up: y = SaturatingAdd(y, -step); break;
            case Key.Down: y = SaturatingAdd(y, step); break;
        }
        _keyboardPoint = new CoreMapPoint(
            Math.Clamp(x, bounds.MinXUnit, bounds.MaxXUnit),
            Math.Clamp(y, bounds.MinYUnit, bounds.MaxYUnit));
        RequireTransform().Follow(
            _keyboardPoint.XUnit,
            _keyboardPoint.YUnit,
            KeyboardFollowMarginPixel);
        SetPointer(_keyboardPoint);
        CameraChanged?.Invoke();
    }

    private void ZoomBy(int direction, Vector2 anchor)
    {
        CommercialMapTransform transform = RequireTransform();
        transform.SetZoomAt(transform.ZoomIndex + direction, anchor);
        RefreshCandidates(notify: true);
        CameraChanged?.Invoke();
        QueueRedraw();
    }

    private void SetPointer(CoreMapPoint? point)
    {
        _pointerPoint = point;
        RefreshCandidates(notify: false);
        PointerChanged?.Invoke(point, SelectedCandidateId);
        QueueRedraw();
    }

    private void RefreshCandidates(bool notify)
    {
        string? retainedId = SelectedCandidateId;
        _candidateNodeIds.Clear();
        if (_pointerPoint is CoreMapPoint pointer &&
            _presentation is { NodeSnapEnabled: true } &&
            !_draggingDraftPoint &&
            _transform is not null)
        {
            Vector2 pointerCanvas = _transform.WorldToCanvas(pointer.XUnit, pointer.YUnit);
            _candidateNodeIds.AddRange(_presentation.Snapshot.World.Nodes
                .Where(node => node.Commissioned)
                .Select(node => new
                {
                    node.NodeId,
                    Distance = pointerCanvas.DistanceSquaredTo(
                        _transform.WorldToCanvas(node.Position.XUnit, node.Position.YUnit)),
                })
                .Where(candidate => candidate.Distance <= CandidateRadiusPixel * CandidateRadiusPixel)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.NodeId, StringComparer.Ordinal)
                .Select(candidate => candidate.NodeId));
        }
        _candidateIndex = retainedId is null
            ? 0
            : Math.Max(0, _candidateNodeIds.IndexOf(retainedId));
        if (notify)
        {
            PointerChanged?.Invoke(_pointerPoint, SelectedCandidateId);
        }
    }

    private void CycleCandidate(int direction)
    {
        if (_candidateNodeIds.Count == 0)
        {
            return;
        }
        _candidateIndex = (_candidateIndex + direction + _candidateNodeIds.Count) %
            _candidateNodeIds.Count;
        PointerChanged?.Invoke(_pointerPoint, SelectedCandidateId);
        QueueRedraw();
    }

    private bool TryMapPoint(Vector2 canvasPoint, out CoreMapPoint point)
    {
        CommercialMapTransform transform = RequireTransform();
        if (!transform.PlotRect.HasPoint(canvasPoint))
        {
            point = default;
            return false;
        }
        CommercialWorldPosition world = transform.CanvasToWorld(canvasPoint);
        MapBounds bounds = _presentation!.Snapshot.World.Bounds;
        point = new CoreMapPoint(
            Math.Clamp(RoundUnit(world.X), bounds.MinXUnit, bounds.MaxXUnit),
            Math.Clamp(RoundUnit(world.Y), bounds.MinYUnit, bounds.MaxYUnit));
        return true;
    }

    private void DrawMapGround()
    {
        Texture2D? ground = G3GroundRubbleReliefCTile ??
            G3GroundRubbleMixBTile ?? GroundAsphaltTile;
        if (ground is not null)
        {
            DrawTextureRectRegion(
                ground,
                new Rect2(Vector2.Zero, Size),
                new Rect2(Vector2.Zero, Size * 1.72f),
                new Color(0.76f, 0.72f, 0.66f, 1f));
            DrawRect(
                new Rect2(Vector2.Zero, Size),
                new Color(Color.FromHtml("10100f"), 0.08f));
        }
        else
        {
            DrawRect(new Rect2(Vector2.Zero, Size), Land);
        }
    }

    private void DrawRubbleMaterialPatches()
    {
        Texture2D? rubble = G3GroundRubbleReliefCTile ?? G3GroundRubbleMixBTile;
        if (rubble is null)
        {
            return;
        }
        (int MinX, int MinY, int MaxX, int MaxY, float Alpha)[] patches =
        [
            (820, 520, 1540, 1080, 0.17f),
            (1020, 1080, 1740, 1680, 0.14f),
            (420, 1120, 1040, 1780, 0.12f),
        ];
        foreach ((int minX, int minY, int maxX, int maxY, float alpha) in patches)
        {
            DrawWorldQuadTexture(
                rubble,
                minX,
                minY,
                maxX,
                maxY,
                new Color(0.84f, 0.76f, 0.62f, alpha));
        }
    }

    private void DrawRoadFabric()
    {
        if (GroundConcreteTile is null)
        {
            return;
        }

        (int MinX, int MinY, int MaxX, int MaxY)[] roads =
        [
            (2080, 470, 3160, 525),
            (2180, 790, 3200, 850),
            (2050, 1080, 3180, 1140),
            (2160, 1360, 3150, 1420),
            (2260, 1660, 3160, 1720),
            (2320, 390, 2380, 1820),
            (2660, 420, 2720, 1810),
            (3020, 500, 3080, 1780),
            (40, 1170, 980, 1230),
            (20, 1510, 1040, 1570),
            (240, 1030, 300, 1900),
            (610, 1030, 670, 1880),
            (920, 260, 980, 1880),
            (1660, 240, 1720, 1700),
            (1450, 1010, 2520, 1060),
            (-200, 2050, 1480, 2110),
            (140, 2360, 1460, 2420),
            (1160, 1740, 2380, 1800),
            (1420, 1420, 1480, 2020),
            (1810, 1430, 1870, 2020),
            (2160, 1460, 2220, 1980),
            (3340, -80, 3400, 1540),
            (3650, 80, 3710, 1740),
            // Dense east-city lattice under the individual parcel sprites.
            (1260, 720, 1310, 1580),
            (1490, 700, 1540, 1580),
            (1720, 700, 1770, 1540),
            (1950, 720, 2000, 1500),
            (1200, 780, 2070, 830),
            (1210, 1020, 2070, 1070),
            (1250, 1260, 2080, 1310),
            (1320, 1460, 2040, 1510),
            // West industrial service lattice.
            (930, 1320, 1430, 1370),
            (980, 1510, 1440, 1560),
            (1040, 1280, 1090, 1690),
            (1280, 1270, 1330, 1690),
        ];

        foreach ((int minX, int minY, int maxX, int maxY) in roads)
        {
            DrawWorldQuadTexture(
                GroundConcreteTile,
                minX,
                minY,
                maxX,
                maxY,
                new Color(0.70f, 0.66f, 0.59f, 0.34f));
            Vector2[] road = WorldQuad(minX, minY, maxX, maxY);
            DrawPolyline(
                road.Append(road[0]).ToArray(),
                new Color(Color.FromHtml("887865"), 0.08f),
                0.8f,
                true);
        }
    }

    private Vector2[] WorldQuad(int minX, int minY, int maxX, int maxY) =>
    [
        RequireTransform().WorldToCanvas(minX, minY),
        RequireTransform().WorldToCanvas(maxX, minY),
        RequireTransform().WorldToCanvas(maxX, maxY),
        RequireTransform().WorldToCanvas(minX, maxY),
    ];

    private void DrawWorldQuadTexture(
        Texture2D texture,
        int minX,
        int minY,
        int maxX,
        int maxY,
        Color modulate)
    {
        Vector2[] uvs =
        [
            Vector2.Zero,
            new Vector2(Math.Max(1, texture.GetWidth() - 1), 0f),
            new Vector2(
                Math.Max(1, texture.GetWidth() - 1),
                Math.Max(1, texture.GetHeight() - 1)),
            new Vector2(0f, Math.Max(1, texture.GetHeight() - 1)),
        ];
        DrawColoredPolygon(WorldQuad(minX, minY, maxX, maxY), modulate, uvs, texture);
    }

    private void DrawChapterAtmosphere(int chapterIndex, bool reduceMotion)
    {
        string[] labels =
        [
            "새벽 · 첫 입주",
            "맑음 · 북안 점검",
            "바람 · 전원 전환",
            "저녁 · 입주 약속",
            "폭염 · 열여유 경계",
            "폭우 · 강 수위 상승",
            "갬 · 3주 뒤",
            "겨울밤 · 마지막 우회",
        ];
        int index = Math.Clamp(chapterIndex, 0, labels.Length - 1);
        Color tint = index switch
        {
            0 => new Color(Color.FromHtml("304b59"), 0.12f),
            3 => new Color(Color.FromHtml("855f38"), 0.08f),
            4 => new Color(Color.FromHtml("ba6c3e"), 0.10f),
            5 => new Color(Color.FromHtml("264a62"), 0.16f),
            7 => new Color(Color.FromHtml("1b2851"), 0.20f),
            _ => new Color(Color.FromHtml("31564f"), 0.06f),
        };
        DrawRect(new Rect2(Vector2.Zero, Size), tint);

        if (index is 5 or 7)
        {
            float phase = reduceMotion ? 0f : (float)((_animationSeconds * 28d) % 34d);
            for (float x = -Size.Y; x < Size.X + Size.Y; x += 34f)
            {
                Vector2 from = new(x + phase, 0f);
                Vector2 to = new(x - Size.Y + phase, Size.Y);
                DrawLine(from, to, new Color(WaterLine, index == 5 ? 0.16f : 0.09f), 1f, true);
            }
        }
        else if (index == 4)
        {
            for (float y = 44f; y < Size.Y; y += 52f)
            {
                DrawDashedLine(
                    new Vector2(0f, y),
                    new Vector2(Size.X, y),
                    new Color(EmergencyLine, 0.08f),
                    1f,
                    18f);
            }
        }

        string motion = reduceMotion ? " · 움직임 줄임" : string.Empty;
        string label = $"장면 · {labels[index]}{motion}";
        Vector2 labelSize = GetThemeDefaultFont().GetStringSize(
            label,
            HorizontalAlignment.Left,
            -1f,
            12);
        Vector2 position = new(Size.X - labelSize.X - 18f, 24f);
        DrawRect(
            new Rect2(position - new Vector2(7f, 16f), labelSize + new Vector2(14f, 8f)),
            new Color(Background, 0.84f));
        DrawString(GetThemeDefaultFont(), position, label,
            HorizontalAlignment.Left, -1f, 12, Text);
    }

    private void DrawTerrain(SpatialWorldDefinition world)
    {
        foreach (TerrainPolygonDefinition area in world.Terrain)
        {
            Vector2[] polygon = area.Polygon.Select(ToCanvas).ToArray();
            Texture2D? texture = TerrainTexture(area.TerrainId);
            if (area.Kind == TerrainKind.Water)
            {
                continue;
            }
            Color fill = area.Kind == TerrainKind.Water
                ? new Color(Water, 0.62f)
                : new Color(Building, 0.055f);
            Color edge = area.Kind == TerrainKind.Water ? WaterLine : BuildingEdge;
            if (texture is null)
            {
                DrawColoredPolygon(polygon, fill);
            }
            else
            {
                int minX = area.Polygon.Min(point => point.XUnit);
                int maxX = area.Polygon.Max(point => point.XUnit);
                int minY = area.Polygon.Min(point => point.YUnit);
                int maxY = area.Polygon.Max(point => point.YUnit);
                Vector2[] uvs = area.Polygon
                    .Select(point => new Vector2(
                        (float)(point.XUnit - minX) / Math.Max(1, maxX - minX) *
                            Math.Max(1, texture.GetWidth() - 1),
                        (float)(point.YUnit - minY) / Math.Max(1, maxY - minY) *
                            Math.Max(1, texture.GetHeight() - 1)))
                    .ToArray();
                if (area.Kind == TerrainKind.Water)
                {
                    DrawColoredPolygon(
                        polygon,
                        new Color(0.82f, 0.92f, 0.94f, 0.98f),
                        uvs,
                        texture);
                    DrawColoredPolygon(polygon, new Color(fill, 0.10f));
                }
                else
                {
                    // Building polygons are simulation boundaries, not giant
                    // concrete plates. District identity is expressed by the
                    // individually placed parcel/object sprites below.
                    DrawColoredPolygon(polygon, fill);
                }
            }
            DrawPolyline(
                polygon.Append(polygon[0]).ToArray(),
                new Color(edge, area.Kind == TerrainKind.Water ? 1f : 0.16f),
                area.Kind == TerrainKind.Water ? 1.6f : 1f,
                true);
        }
    }

    private void DrawWaterTerrain(SpatialWorldDefinition world)
    {
        foreach (TerrainPolygonDefinition area in world.Terrain.Where(item =>
                     item.Kind == TerrainKind.Water))
        {
            Vector2[] polygon = area.Polygon.Select(ToCanvas).ToArray();
            DrawRiverTerrain(area, polygon, TerrainTexture(area.TerrainId));
        }
    }

    private Texture2D?[] AtomicCityTextures() =>
    [
        AtomicWorkerHouseASprite,
        AtomicWorkerHouseBSprite,
        AtomicWorkerHouseCSprite,
        AtomicRowShopASprite,
        AtomicWorkshopASprite,
        AtomicSmallWarehouseASprite,
        AtomicHospitalMainASprite,
        AtomicHospitalServiceASprite,
        AtomicPumpHouseASprite,
        AtomicWaterTankASprite,
        AtomicRetainingWallASprite,
        AtomicStreetLampASprite,
    ];

    private Texture2D?[] AtomicRoadTextures() =>
    [
        RoadStraightNorthWestSouthEastATile,
        RoadStraightNorthEastSouthWestATile,
        RoadCornerNorthEastATile,
        RoadTJunctionATile,
        RoadCrossJunctionATile,
        ServiceYardATile,
    ];

    private Texture2D? AtomicCityTexture(AtomicCitySpriteKind kind) => kind switch
    {
        AtomicCitySpriteKind.WorkerHouseA => AtomicWorkerHouseASprite,
        AtomicCitySpriteKind.WorkerHouseB => AtomicWorkerHouseBSprite,
        AtomicCitySpriteKind.WorkerHouseC => AtomicWorkerHouseCSprite,
        AtomicCitySpriteKind.RowShopA => AtomicRowShopASprite,
        AtomicCitySpriteKind.WorkshopA => AtomicWorkshopASprite,
        AtomicCitySpriteKind.SmallWarehouseA => AtomicSmallWarehouseASprite,
        AtomicCitySpriteKind.HospitalMainA => AtomicHospitalMainASprite,
        AtomicCitySpriteKind.HospitalServiceA => AtomicHospitalServiceASprite,
        AtomicCitySpriteKind.PumpHouseA => AtomicPumpHouseASprite,
        AtomicCitySpriteKind.WaterTankA => AtomicWaterTankASprite,
        AtomicCitySpriteKind.RetainingWallA => AtomicRetainingWallASprite,
        AtomicCitySpriteKind.StreetLampA => AtomicStreetLampASprite,
        _ => null,
    };

    private Texture2D? AtomicRoadTexture(AtomicRoadSpriteKind kind) => kind switch
    {
        AtomicRoadSpriteKind.StraightNorthWestSouthEast =>
            RoadStraightNorthWestSouthEastATile,
        AtomicRoadSpriteKind.StraightNorthEastSouthWest =>
            RoadStraightNorthEastSouthWestATile,
        AtomicRoadSpriteKind.CornerNorthEast => RoadCornerNorthEastATile,
        AtomicRoadSpriteKind.TJunction => RoadTJunctionATile,
        AtomicRoadSpriteKind.CrossJunction => RoadCrossJunctionATile,
        AtomicRoadSpriteKind.ServiceYard => ServiceYardATile,
        _ => null,
    };

    private void DrawAtomicRoadTiles()
    {
        foreach (AtomicRoadInstanceSpec instance in AtomicRoadInstances
            .OrderBy(item => ToCanvas(new CoreMapPoint(item.XUnit, item.YUnit)).Y)
            .ThenBy(item => ToCanvas(new CoreMapPoint(item.XUnit, item.YUnit)).X)
            .ThenBy(item => item.Kind))
        {
            Texture2D? texture = AtomicRoadTexture(instance.Kind);
            if (texture is null)
            {
                continue;
            }
            Vector2 center = ToCanvas(new CoreMapPoint(instance.XUnit, instance.YUnit));
            Vector2 size = FitSpriteSize(
                texture,
                instance.MaxSide * (1f + (ZoomIndex * 0.08f)));
            DrawTextureRect(
                texture,
                new Rect2(center - (size * 0.5f), size),
                false,
                new Color(0.74f, 0.72f, 0.68f, instance.Alpha));
        }
    }

    private void DrawAtomicCity()
    {
        foreach (AtomicCityInstanceSpec instance in AtomicCityInstances
            .OrderBy(item => ToCanvas(new CoreMapPoint(item.XUnit, item.YUnit)).Y)
            .ThenBy(item => ToCanvas(new CoreMapPoint(item.XUnit, item.YUnit)).X)
            .ThenBy(item => item.Kind))
        {
            Texture2D? texture = AtomicCityTexture(instance.Kind);
            if (texture is null)
            {
                continue;
            }
            Vector2 size = FitSpriteSize(
                texture,
                instance.MaxSide * (1f + (ZoomIndex * 0.10f)));
            Color modulate = instance.Kind switch
            {
                AtomicCitySpriteKind.HospitalMainA =>
                    new Color(0.90f, 0.90f, 0.86f, instance.Alpha),
                AtomicCitySpriteKind.WaterTankA =>
                    new Color(0.80f, 0.82f, 0.80f, instance.Alpha),
                AtomicCitySpriteKind.StreetLampA =>
                    new Color(0.86f, 0.82f, 0.72f, instance.Alpha),
                _ => new Color(0.80f, 0.78f, 0.73f, instance.Alpha),
            };
            DrawTextureRect(
                texture,
                SpriteRect(
                    ToCanvas(new CoreMapPoint(instance.XUnit, instance.YUnit)),
                    size),
                false,
                modulate);
        }
    }

    private void DrawRiverTerrain(
        TerrainPolygonDefinition area,
        Vector2[] authoredPolygon,
        Texture2D? texture)
    {
        int chapter = _presentation?.ChapterIndex ?? 0;
        int bankPointCount = authoredPolygon.Length / 2;
        if (bankPointCount < 3 || authoredPolygon.Length % 2 != 0)
        {
            DrawColoredPolygon(authoredPolygon, new Color(Water, 0.92f));
            return;
        }
        Vector2[] leftBank;
        Vector2[] rightBankAscending;
        if (area.TerrainId == "CHEONGRYU_RIVER")
        {
            // The polygon is the simulation's conservative crossing envelope. The
            // reference presentation uses one narrower exposed channel inside it,
            // partly hidden by rubble and bridges. Keep that visual centerline in
            // world space so zoom, pan, hit testing, and every chapter remain one map.
            Vector2[] centerline = SmoothOpenPolyline(
                ReferenceRiverControlPoints.Select(ToCanvas).ToArray(),
                8);
            centerline = RuggedOpenPolyline(centerline, 3.8f, 0.7f);
            (leftBank, rightBankAscending) = BuildSingleRiverBanks(centerline, chapter);
        }
        else
        {
            leftBank = SmoothOpenPolyline(authoredPolygon[..bankPointCount], 8);
            rightBankAscending = SmoothOpenPolyline(
                    authoredPolygon[bankPointCount..],
                    8)
                .Reverse()
                .ToArray();
            (leftBank, rightBankAscending) = RuggedRiverBanks(leftBank, rightBankAscending);
        }
        Color bank = chapter == 4
            ? Color.FromHtml("66513f")
            : Color.FromHtml("4b4940");

        float baseBankInset = chapter switch
        {
            4 => 0.28f,
            5 => 0.04f,
            7 => 0.20f,
            _ => 0.10f,
        };
        Vector2[] surfaceLeft = leftBank
            .Select((point, index) => point.Lerp(
                rightBankAscending[index],
                VariableBankInset(baseBankInset, index, leftBank.Length)))
            .ToArray();
        Vector2[] surfaceRightAscending = Enumerable.Range(0, rightBankAscending.Length)
            .Select(index => rightBankAscending[index].Lerp(
                leftBank[index],
                VariableBankInset(baseBankInset, index, leftBank.Length)))
            .ToArray();
        Vector2[] surfaceRightDescending = surfaceRightAscending.Reverse().ToArray();
        Vector2[] surface = surfaceLeft.Concat(surfaceRightDescending).ToArray();
        // The authored polygon is a gameplay bank envelope, not a literal second
        // riverbed. Filling and outlining that whole envelope made its two distant
        // edges read as tributaries. Render a narrow, terrain-owned strip around the
        // actual water surface instead, so the visible river is unambiguously one
        // continuous channel while still using individual bank objects.
        Vector2[] outerLeft = Enumerable.Range(0, surfaceLeft.Length)
            .Select(index => surfaceLeft[index].Lerp(leftBank[index], 0.82f))
            .ToArray();
        Vector2[] outerRight = Enumerable.Range(0, surfaceRightAscending.Length)
            .Select(index => surfaceRightAscending[index].Lerp(rightBankAscending[index], 0.82f))
            .ToArray();
        DrawRiverBankStrip(outerLeft, surfaceLeft, bank);
        DrawRiverBankStrip(surfaceRightAscending, outerRight, bank);
        DrawPolyline(
            outerLeft.Select(point => point + new Vector2(0f, 2f)).ToArray(),
            new Color(Color.FromHtml("090808"), 0.24f),
            1.4f,
            true);
        DrawPolyline(
            outerRight.Select(point => point + new Vector2(0f, 2f)).ToArray(),
            new Color(Color.FromHtml("090808"), 0.24f),
            1.4f,
            true);
        Color waterModulate = chapter switch
        {
            4 => new Color(0.58f, 0.55f, 0.46f, 0.94f),
            5 => new Color(0.72f, 0.85f, 0.90f, 0.98f),
            7 => new Color(0.61f, 0.69f, 0.74f, 0.94f),
            _ => new Color(0.51f, 0.58f, 0.59f, 0.92f),
        };
        DrawRiverSurfaceSegments(
            surfaceLeft,
            surfaceRightAscending,
            texture,
            waterModulate,
            new Color(Water, texture is null ? 0.96f : chapter == 4 ? 0.18f : 0.025f));
        DrawPolyline(surfaceLeft, new Color(Color.FromHtml("45666d"), 0.08f), 0.6f, true);
        DrawPolyline(surfaceRightAscending, new Color(Color.FromHtml("45666d"), 0.08f), 0.6f, true);
        for (int index = 8; index < surfaceLeft.Length - 8; index += 13)
        {
            DrawLine(
                surfaceLeft[index].Lerp(surfaceRightAscending[index], 0.18f),
                surfaceLeft[index].Lerp(surfaceRightAscending[index], 0.72f),
                new Color(Color.FromHtml("6b9298"), index % 26 == 8 ? 0.16f : 0.10f),
                1f,
                true);
        }
        DrawRiverBankObjects(outerLeft, leftSide: true);
        DrawRiverBankObjects(outerRight, leftSide: false);
        DrawRiverShoals(surfaceLeft, surfaceRightAscending);
        DrawRiverBridgeDeck(new CoreMapPoint(1139, 1338), 1.18f);
        DrawRiverBridgeDeck(new CoreMapPoint(1254, 1655), 1.18f);
        DrawRiverReflections(surface, chapter);
    }

    private static (Vector2[] Left, Vector2[] Right) BuildSingleRiverBanks(
        Vector2[] centerline,
        int chapter)
    {
        float baseHalfWidth = chapter switch
        {
            4 => 9f,
            5 => 16f,
            7 => 12f,
            _ => 13f,
        };
        Vector2[] left = new Vector2[centerline.Length];
        Vector2[] right = new Vector2[centerline.Length];
        for (int index = 0; index < centerline.Length; index++)
        {
            int previous = Math.Max(0, index - 2);
            int next = Math.Min(centerline.Length - 1, index + 2);
            Vector2 tangent = (centerline[next] - centerline[previous]).Normalized();
            Vector2 normal = new(-tangent.Y, tangent.X);
            float phase = centerline.Length <= 1 ? 0f : index / (float)(centerline.Length - 1);
            float width = baseHalfWidth +
                (Mathf.Sin((phase * Mathf.Tau * 3.1f) + 0.4f) * 2.8f) +
                (Mathf.Sin((phase * Mathf.Tau * 7.3f) + 1.2f) * 1.4f);
            left[index] = centerline[index] - (normal * width);
            right[index] = centerline[index] + (normal * width);
        }
        return (left, right);
    }

    private void DrawRiverBankStrip(Vector2[] outerEdge, Vector2[] innerEdge, Color bank)
    {
        DrawSegmentedStrip(
            outerEdge,
            innerEdge,
            GroundGravelTile,
            GroundGravelTile is null
                ? new Color(bank, 0.82f)
                : new Color(0.56f, 0.51f, 0.43f, 0.18f),
            GroundGravelTile is null ? Colors.Transparent : new Color(bank, 0.08f));
    }

    private void DrawRiverSurfaceSegments(
        Vector2[] leftEdge,
        Vector2[] rightEdge,
        Texture2D? texture,
        Color textureModulate,
        Color overlay)
    {
        DrawSegmentedStrip(
            leftEdge,
            rightEdge,
            texture,
            texture is null ? overlay : textureModulate,
            texture is null ? Colors.Transparent : overlay);
    }

    private void DrawSegmentedStrip(
        Vector2[] edgeA,
        Vector2[] edgeB,
        Texture2D? texture,
        Color fill,
        Color overlay)
    {
        int segmentCount = Math.Min(edgeA.Length, edgeB.Length) - 1;
        for (int index = 0; index < segmentCount; index++)
        {
            Vector2[] first = [edgeA[index], edgeA[index + 1], edgeB[index + 1]];
            Vector2[] second = [edgeA[index], edgeB[index + 1], edgeB[index]];
            DrawStripTriangle(first, texture, fill, overlay);
            DrawStripTriangle(second, texture, fill, overlay);
        }
    }

    private void DrawStripTriangle(
        Vector2[] triangle,
        Texture2D? texture,
        Color fill,
        Color overlay)
    {
        float twiceArea = Math.Abs(
            (triangle[1] - triangle[0]).Cross(triangle[2] - triangle[0]));
        if (twiceArea < 0.01f)
        {
            return;
        }
        if (texture is null)
        {
            DrawColoredPolygon(triangle, fill);
        }
        else
        {
            DrawColoredPolygon(triangle, fill, TextureUvs(triangle, texture), texture);
        }
        if (overlay.A > 0f)
        {
            DrawColoredPolygon(triangle, overlay);
        }
    }

    private static float VariableBankInset(float baseInset, int index, int count)
    {
        float phase = count <= 1 ? 0f : index / (float)(count - 1);
        float variation =
            (Mathf.Sin((phase * Mathf.Tau * 2.7f) + 0.35f) * 0.035f) +
            (Mathf.Sin((phase * Mathf.Tau * 5.1f) + 1.2f) * 0.018f);
        return Math.Clamp(baseInset + variation, 0.015f, 0.44f);
    }

    private static (Vector2[] Left, Vector2[] Right) RuggedRiverBanks(
        Vector2[] left,
        Vector2[] right)
    {
        if (left.Length != right.Length || left.Length < 3)
        {
            return (left, right);
        }

        Vector2[] ruggedLeft = left.ToArray();
        Vector2[] ruggedRight = right.ToArray();
        for (int index = 1; index < left.Length - 1; index++)
        {
            Vector2 center = (left[index] + right[index]) * 0.5f;
            Vector2 across = right[index] - left[index];
            float width = across.Length();
            if (width <= 0.01f)
            {
                continue;
            }
            Vector2 acrossDirection = across / width;
            float phase = index / (float)(left.Length - 1);
            float centerShift =
                (Mathf.Sin((phase * Mathf.Tau * 8.3f) + 0.4f) * 7.8f) +
                (Mathf.Sin((phase * Mathf.Tau * 17.7f) + 1.1f) * 3.6f);
            float widthScale = 0.82f +
                (Mathf.Sin((phase * Mathf.Tau * 11.1f) + 0.9f) * 0.17f) +
                (Mathf.Sin((phase * Mathf.Tau * 23.3f) + 0.2f) * 0.07f);
            float halfWidth = width * Math.Clamp(widthScale, 0.58f, 1.06f) * 0.5f;
            Vector2 shiftedCenter = center + (acrossDirection * centerShift);
            ruggedLeft[index] = shiftedCenter - (acrossDirection * halfWidth);
            ruggedRight[index] = shiftedCenter + (acrossDirection * halfWidth);
        }
        return (ruggedLeft, ruggedRight);
    }

    private void DrawRiverBankObjects(Vector2[] bank, bool leftSide)
    {
        Texture2D? straight = G3RiverBankRockSegmentASprite;
        if (straight is null || bank.Length < 7)
        {
            return;
        }

        for (int index = 7; index < bank.Length - 7; index += 12)
        {
            Vector2 tangent = bank[index + 2] - bank[index - 2];
            if (tangent.LengthSquared() < 0.01f)
            {
                continue;
            }
            Vector2 before = (bank[index] - bank[index - 3]).Normalized();
            Vector2 after = (bank[index + 3] - bank[index]).Normalized();
            float turn = before.Cross(after);
            Texture2D bankTexture = Math.Abs(turn) < 0.035f
                ? straight
                : (turn > 0f) == leftSide
                    ? G3RiverBankInnerBendASprite ?? straight
                    : G3RiverBankOuterBendASprite ?? straight;
            float rotation = tangent.Angle();
            float maxSide = Math.Abs(turn) >= 0.035f
                ? 38f
                : index % 24 == 7 ? 33f : 29f;
            Vector2 size = FitSpriteSize(bankTexture, maxSide);
            DrawSetTransform(bank[index], rotation, Vector2.One);
            DrawTextureRect(
                bankTexture,
                new Rect2(size * -0.5f, size),
                false,
                new Color(0.68f, 0.62f, 0.54f, 0.34f));
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void DrawRiverShoals(Vector2[] left, Vector2[] right)
    {
        Texture2D? shoalTexture = G3RiverBankRockSegmentASprite;
        if (shoalTexture is null || left.Length != right.Length || left.Length < 24)
        {
            return;
        }

        // A few individual rubble shoals interrupt the water without baking a
        // complete river image. This mirrors the reference's broken, terrain-owned
        // channel and keeps every obstruction a separately bound runtime object.
        float[] phases = [0.16f, 0.31f, 0.47f, 0.63f, 0.79f];
        foreach (float phase in phases)
        {
            int index = Math.Clamp(
                Mathf.RoundToInt((left.Length - 1) * phase),
                2,
                left.Length - 3);
            Vector2 tangent = ((left[index + 2] + right[index + 2]) -
                (left[index - 2] + right[index - 2])) * 0.5f;
            Vector2 center = left[index].Lerp(right[index], phase == 0.47f ? 0.62f : 0.48f);
            Vector2 size = FitSpriteSize(shoalTexture, phase == 0.47f ? 42f : 34f);
            DrawSetTransform(center, tangent.Angle(), Vector2.One);
            DrawTextureRect(
                shoalTexture,
                new Rect2(size * -0.5f, size),
                false,
                new Color(0.58f, 0.54f, 0.47f, 0.80f));
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void DrawRiverBridgeAbutment(CoreMapPoint position, float rotation)
    {
        Texture2D? abutmentTexture = BridgeFoundationSprite ??
            G3RiverBankRockSegmentASprite;
        if (abutmentTexture is null)
        {
            return;
        }
        Vector2 center = ToCanvas(position);
        Vector2 size = FitSpriteSize(abutmentTexture, 110f);
        DrawSetTransform(center, rotation, Vector2.One);
        DrawTextureRect(
            abutmentTexture,
            new Rect2(size * -0.5f, size),
            false,
            new Color(0.76f, 0.72f, 0.64f, 0.86f));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void DrawRiverBridgeDeck(CoreMapPoint position, float rotation)
    {
        if (IndustrialRoadBridgeASprite is null)
        {
            DrawRiverBridgeAbutment(position, rotation);
            return;
        }

        Vector2 center = ToCanvas(position);
        Vector2 size = FitSpriteSize(IndustrialRoadBridgeASprite, 54f);
        DrawSetTransform(center, rotation, Vector2.One);
        DrawTextureRect(
            IndustrialRoadBridgeASprite,
            new Rect2(size * -0.5f, size),
            false,
            new Color(0.78f, 0.75f, 0.69f, 0.58f));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private static Vector2[] SmoothOpenPolyline(Vector2[] points, int samplesPerSegment)
    {
        var result = new List<Vector2>((points.Length - 1) * samplesPerSegment + 1);
        for (int index = 0; index < points.Length - 1; index++)
        {
            Vector2 p0 = points[Math.Max(0, index - 1)];
            Vector2 p1 = points[index];
            Vector2 p2 = points[index + 1];
            Vector2 p3 = points[Math.Min(points.Length - 1, index + 2)];
            for (int sample = 0; sample < samplesPerSegment; sample++)
            {
                float t = sample / (float)samplesPerSegment;
                float t2 = t * t;
                float t3 = t2 * t;
                result.Add(0.5f * (
                    (2f * p1) +
                    ((-p0 + p2) * t) +
                    (((2f * p0) - (5f * p1) + (4f * p2) - p3) * t2) +
                    (((-p0) + (3f * p1) - (3f * p2) + p3) * t3)));
            }
        }
        result.Add(points[^1]);
        return result.ToArray();
    }

    private static Vector2[] RuggedOpenPolyline(
        Vector2[] points,
        float amplitude,
        float phase)
    {
        if (points.Length < 3)
        {
            return points;
        }
        Vector2[] result = new Vector2[points.Length];
        result[0] = points[0];
        result[^1] = points[^1];
        for (int index = 1; index < points.Length - 1; index++)
        {
            Vector2 tangent = (points[index + 1] - points[index - 1]).Normalized();
            Vector2 normal = new(-tangent.Y, tangent.X);
            float noise =
                (Mathf.Sin((index * 1.73f) + phase) * 0.62f) +
                (Mathf.Sin((index * 0.47f) + (phase * 1.9f)) * 0.38f);
            result[index] = points[index] + (normal * noise * amplitude);
        }
        return result;
    }

    private static Vector2[] TextureUvs(Vector2[] polygon, Texture2D texture)
    {
        float minX = polygon.Min(point => point.X);
        float maxX = polygon.Max(point => point.X);
        float minY = polygon.Min(point => point.Y);
        float maxY = polygon.Max(point => point.Y);
        float width = Math.Max(1f, maxX - minX);
        float height = Math.Max(1f, maxY - minY);
        return polygon.Select(point => new Vector2(
            ((point.X - minX) / width) * Math.Max(1, texture.GetWidth() - 1),
            ((point.Y - minY) / height) * Math.Max(1, texture.GetHeight() - 1))).ToArray();
    }

    private static Vector2[] RiverTextureUvs(Vector2[] polygon, Texture2D texture) =>
        TextureUvs(polygon, texture)
            .Select(uv => uv * 2.8f)
            .ToArray();

    private void DrawRiverReflections(Vector2[] polygon, int chapter)
    {
        if (RiverCurrentReflectionASprite is not null)
        {
            Vector2[] centerline = SmoothOpenPolyline(
                ReferenceRiverControlPoints.Select(ToCanvas).ToArray(),
                8);
            Color modulate = chapter == 4
                ? new Color(0.74f, 0.55f, 0.38f, 0.28f)
                : chapter == 5
                    ? new Color(0.82f, 0.94f, 1.00f, 0.52f)
                    : new Color(0.70f, 0.82f, 0.86f, 0.38f);
            for (int index = 12; index < centerline.Length - 12; index += 20)
            {
                Vector2 tangent = centerline[index + 3] - centerline[index - 3];
                Vector2 size = FitSpriteSize(
                    RiverCurrentReflectionASprite,
                    index % 40 == 12 ? 68f : 58f);
                DrawSetTransform(centerline[index], tangent.Angle(), Vector2.One);
                DrawTextureRect(
                    RiverCurrentReflectionASprite,
                    new Rect2(size * -0.5f, size),
                    false,
                    modulate);
            }
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            return;
        }

        float minX = polygon.Min(point => point.X);
        float maxX = polygon.Max(point => point.X);
        float minY = polygon.Min(point => point.Y);
        float maxY = polygon.Max(point => point.Y);
        Color reflection = chapter == 4 ? EmergencyLine : WaterLine;
        int row = 0;
        for (float y = minY + 11f; y < maxY; y += 29f, row++)
        {
            float drift = (row % 3) * 7f;
            foreach ((Vector2 from, Vector2 to) in ClipLineToPolygon(
                         new Vector2(minX - 4f + drift, y),
                         new Vector2(maxX + 4f, y - 5f),
                         polygon))
            {
                DrawLine(from, to, new Color(reflection, chapter == 5 ? 0.34f : 0.27f), 1f, true);
            }
        }
    }

    private Texture2D? TerrainTexture(string terrainId) => terrainId switch
    {
        "CHEONGRYU_RIVER" => G3RiverWaterSurfaceTile ?? RiverWaterTile,
        "EAST_RESIDENTIAL_BLOCK" => null,
        "HOSPITAL_BLOCK" => null,
        "WEST_INDUSTRIAL_BLOCK" => GroundConcreteTile,
        _ => null,
    };

    private void DrawRiskAreas(
        SpatialWorldDefinition world,
        IReadOnlyList<string> activeRiskAreaIds)
    {
        HashSet<string> active = activeRiskAreaIds.ToHashSet(StringComparer.Ordinal);
        foreach (SpatialRiskAreaDefinition area in world.RiskAreas)
        {
            Vector2[] polygon = area.Polygon.Select(ToCanvas).ToArray();
            bool isActive = active.Contains(area.RiskAreaId);
            DrawColoredPolygon(polygon, new Color(Risk, isActive ? 0.12f : 0.018f));
            DrawPolyline(
                polygon.Append(polygon[0]).ToArray(),
                new Color(isActive ? Risk : BrassRisk(), isActive ? 0.92f : 0.24f),
                isActive ? 2f : 1f,
                true);
            if (isActive)
            {
                DrawPolygonHatching(polygon, Risk, 20f, 0.28f);
                DrawAreaLabel(polygon, $"사건 구역 · {area.DisplayName}", Risk);
            }
        }
    }

    private void DrawEdges(
        SpatialWorldDefinition world,
        ThermalIntervalResult? thermalInterval)
    {
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            node => node.NodeId,
            StringComparer.Ordinal);
        Dictionary<string, SpatialNodeClassDefinition> classes = world.NodeClasses.ToDictionary(
            item => item.ClassId,
            StringComparer.Ordinal);
        foreach (SpatialEdgeDefinition edge in world.Edges)
        {
            if (!nodes.TryGetValue(edge.FromNodeId, out SpatialNodeDefinition? from) ||
                !nodes.TryGetValue(edge.ToNodeId, out SpatialNodeDefinition? to))
            {
                continue;
            }
            Vector2 start = ConductorAnchor(from, classes);
            Vector2 end = ConductorAnchor(to, classes);
            ThermalAssetResult? thermal = thermalInterval?.Assets.FirstOrDefault(item =>
                item.AssetId == edge.EdgeId);
            Color color = !edge.Commissioned
                ? Planned
                : ThermalColor(thermal?.CurrentState);
            if (thermal?.CurrentState == ThermalOperatingState.ProtectiveOutage)
            {
                DrawDashedLine(start, end, color, 3.2f, 9f, true, true);
                DrawCross((start + end) / 2f, color, 7f);
            }
            else if (thermal?.CurrentState == ThermalOperatingState.Emergency)
            {
                DrawPowerSpan(start, end, color, 2.8f);
                DrawDashedLine(start, end, Background, 1.3f, 7f, true, true);
            }
            else
            {
                DrawPowerSpan(start, end, color, edge.Commissioned ? 2.6f : 2.0f);
            }
        }
    }

    private void DrawReservedRouteCorridor(SpatialWorldDefinition world)
    {
        if (!world.Nodes.Any(node => node.Reserved || !node.Commissioned))
        {
            return;
        }
        // The future corridor is an explicit proposal overlay rather than a tint on
        // the already-commissioned southern route. Keeping its supports distinct
        // makes planned amber and energized cyan readable as two alternatives.
        CoreMapPoint[] proposal =
        [
            new(957, 1243),
            new(1100, 1279),
            new(1252, 1323),
            new(1421, 1350),
            new(1542, 1319),
            new(1529, 1171),
            new(1493, 1029),
        ];
        Vector2[] grounds = proposal.Select(ToCanvas).ToArray();
        Vector2[] anchors = grounds
            .Select(ground => ground - new Vector2(0f, 82f))
            .ToArray();
        for (int index = 0; index < anchors.Length - 1; index++)
        {
            DrawPlannedPowerSpan(anchors[index], anchors[index + 1]);
        }
        DrawPlannedSupportSprites(grounds);
    }

    private static Vector2[] PlannedSupportGrounds(Vector2 start, Vector2 end)
    {
        int count = Math.Clamp(Mathf.FloorToInt(start.DistanceTo(end) / 185f), 0, 4);
        return Enumerable.Range(1, count)
            .Select(index => start.Lerp(end, index / (float)(count + 1)))
            .ToArray();
    }

    private void DrawPlannedSupportSprites(Vector2[] supports)
    {
        if (ReinforcedPoleSprite is null)
        {
            return;
        }
        for (int index = 0; index < supports.Length; index++)
        {
            Vector2 ground = supports[index];
            Vector2 size = FitSpriteSize(ReinforcedPoleSprite, 96f);
            DrawCircle(ground, 11f, new Color(Planned, 0.10f));
            DrawTextureRect(
                ReinforcedPoleSprite,
                SpriteRect(ground, size),
                false,
                new Color(1.00f, 0.75f, 0.38f, 0.94f));
        }
    }

    private void DrawPlannedPowerSpan(Vector2 start, Vector2 end)
    {
        Vector2 direction = end - start;
        if (direction.LengthSquared() <= 0.01f)
        {
            return;
        }
        Vector2 normal = new Vector2(-direction.Y, direction.X).Normalized() * 2.4f;
        float sag = Math.Clamp(direction.Length() * 0.14f, 6f, 32f);
        Vector2[] center = Enumerable.Range(0, 25)
            .Select(index =>
            {
                float t = index / 24f;
                return start.Lerp(end, t) + new Vector2(0f, 4f * t * (1f - t) * sag);
            })
            .ToArray();
        DrawPolyline(center, new Color(Background, 0.80f), 4.4f, true);
        for (int index = 0; index < center.Length - 1; index += 2)
        {
            DrawLine(
                center[index] + normal,
                center[index + 1] + normal,
                new Color(Planned, 0.96f),
                1.9f,
                true);
            DrawLine(
                center[index] - normal,
                center[index + 1] - normal,
                new Color(Planned, 0.72f),
                1.5f,
                true);
        }
    }

    private void DrawPowerSpan(Vector2 start, Vector2 end, Color color, float width)
    {
        Vector2 direction = end - start;
        if (direction.LengthSquared() <= 0.01f)
        {
            return;
        }
        Vector2 normal = new Vector2(-direction.Y, direction.X).Normalized() * 3.0f;
        float sag = Math.Clamp(direction.Length() * 0.14f, 6f, 32f);
        Vector2[] center = Enumerable.Range(0, 17)
            .Select(index =>
            {
                float t = index / 16f;
                return start.Lerp(end, t) + new Vector2(0f, 4f * t * (1f - t) * sag);
            })
            .ToArray();
        Vector2[][] strands =
        [
            center.Select(point => point + normal).ToArray(),
            center,
            center.Select(point => point - normal).ToArray(),
        ];
        foreach (Vector2[] strand in strands)
        {
            DrawPolyline(strand, new Color(color, 0.17f), 4.0f, true);
        }
        DrawPolyline(strands[0], new Color(color, 0.98f), 1.55f, true);
        DrawPolyline(strands[1], new Color(color, 0.88f), 1.25f, true);
        DrawPolyline(strands[2], new Color(color, 0.96f), 1.45f, true);
    }

    private Vector2 ConductorAnchor(
        SpatialNodeDefinition node,
        IReadOnlyDictionary<string, SpatialNodeClassDefinition> classes)
    {
        if (!classes.TryGetValue(node.ClassId, out SpatialNodeClassDefinition? nodeClass))
        {
            return ToCanvas(node.Position);
        }
        float lift = nodeClass.Kind switch
        {
            SpatialNodeKind.SourceTerminal => 70f,
            SpatialNodeKind.Substation => 66f,
            SpatialNodeKind.Pole => node.AuthoredFoundation ? 52f : 90f,
            SpatialNodeKind.DedicatedLoadTerminal => 46f,
            _ => 0f,
        };
        return ToCanvas(node.Position) - new Vector2(0f, lift * (1f + (ZoomIndex * 0.12f)));
    }

    private void DrawSelectedDemandPath(
        SpatialWorldDefinition world,
        CommercialMapPresentation presentation)
    {
        if (presentation.SelectedPathEdgeIds.Count == 0)
        {
            return;
        }
        HashSet<string> selected = presentation.SelectedPathEdgeIds.ToHashSet(StringComparer.Ordinal);
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            node => node.NodeId,
            StringComparer.Ordinal);
        Dictionary<string, SpatialNodeClassDefinition> classes = world.NodeClasses.ToDictionary(
            item => item.ClassId,
            StringComparer.Ordinal);
        foreach (SpatialEdgeDefinition edge in world.Edges.Where(edge => selected.Contains(edge.EdgeId)))
        {
            if (!nodes.TryGetValue(edge.FromNodeId, out SpatialNodeDefinition? from) ||
                !nodes.TryGetValue(edge.ToNodeId, out SpatialNodeDefinition? to))
            {
                continue;
            }
            Vector2 start = ConductorAnchor(from, classes);
            Vector2 end = ConductorAnchor(to, classes);
            float distance = start.DistanceTo(end);
            if (distance <= 1f)
            {
                continue;
            }
            // Selected-path emphasis belongs on the actual attachment nodes and
            // conductor glow. Repeated travelling dots made the span read as a UI
            // rail rather than physical conductors between tower heads.
        }
        // The conductor glow itself is the selected-path indication. Extra node
        // rings made substations read as abstract diagram vertices instead of
        // physical equipment in the isometric world.
    }

    private void DrawUnavailableMarks(
        SpatialWorldDefinition world,
        ThermalIntervalResult? interval)
    {
        if (interval is null)
        {
            return;
        }
        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            item => item.NodeId,
            StringComparer.Ordinal);
        foreach (ThermalAssetResult asset in interval.Assets.Where(item => item.AuthoredUnavailable))
        {
            Vector2? center = null;
            if (nodes.TryGetValue(asset.AssetId, out SpatialNodeDefinition? node))
            {
                center = ToCanvas(node.Position);
            }
            else
            {
                SpatialEdgeDefinition? edge = world.Edges.FirstOrDefault(item => item.EdgeId == asset.AssetId);
                if (edge is not null &&
                    nodes.TryGetValue(edge.FromNodeId, out SpatialNodeDefinition? from) &&
                    nodes.TryGetValue(edge.ToNodeId, out SpatialNodeDefinition? to))
                {
                    center = (ToCanvas(from.Position) + ToCanvas(to.Position)) / 2f;
                }
            }
            if (center is Vector2 point)
            {
                DrawRect(new Rect2(point - new Vector2(10f, 8f), new Vector2(20f, 16f)),
                    new Color(Background, 0.88f));
                DrawCross(point, OutageLine, 7f);
                DrawString(GetThemeDefaultFont(), point + new Vector2(12f, -7f), "정비·잠금",
                    HorizontalAlignment.Left, -1f, 11, OutageLine);
            }
        }
    }

    private void DrawLineDraft(ConstructionSnapshot snapshot)
    {
        if (snapshot.LineDraft is not LineDraftSnapshot draft)
        {
            return;
        }
        SpatialNodeDefinition? start = snapshot.World.Nodes.FirstOrDefault(
            node => string.Equals(node.NodeId, draft.StartNodeId, StringComparison.Ordinal));
        if (start is null)
        {
            return;
        }
        var points = new List<CoreMapPoint> { start.Position };
        points.AddRange(draft.IntermediatePoints);
        if (draft.EndNodeId is string endId)
        {
            SpatialNodeDefinition? end = snapshot.World.Nodes.FirstOrDefault(
                node => string.Equals(node.NodeId, endId, StringComparison.Ordinal));
            if (end is not null)
            {
                points.Add(end.Position);
            }
        }
        if (points.Count >= 2)
        {
            DrawPolyline(points.Select(ToCanvas).ToArray(), new Color(Background, 0.9f), 6f, true);
            DrawPolyline(points.Select(ToCanvas).ToArray(), Planned, 2.5f, true);
        }
        SpatialNodeClassDefinition poleClass = snapshot.World.NodeClasses.Single(
            nodeClass => string.Equals(nodeClass.ClassId, draft.PoleClassId, StringComparison.Ordinal));
        SpatialLineClassDefinition lineClass = snapshot.World.LineClasses.Single(
            item => string.Equals(item.ClassId, draft.LineClassId, StringComparison.Ordinal));
        CoreMapPoint currentSegmentStart = draft.IntermediatePoints.Count == 0
            ? start.Position
            : draft.IntermediatePoints[^1];
        DrawProjectedWorldCircle(
            currentSegmentStart,
            lineClass.MaxSpanUnit,
            new Color(Planned, 0.24f),
            fillAlpha: 0f,
            width: 1.2f,
            pointCount: 72);
        foreach (CoreMapPoint point in draft.IntermediatePoints)
        {
            DrawFootprint(point, poleClass.FootprintRadiusUnit, Planned, 0.12f);
            (Texture2D? texture, float maxSide) = DraftPoleSprite(draft.PoleClassId);
            DrawDraftSprite(texture, point, maxSide);
            DrawCircle(ToCanvas(point), 4.5f, Planned);
        }
    }

    private void DrawNodes(
        SpatialWorldDefinition world,
        ThermalIntervalResult? thermalInterval,
        string? selectedThermalAssetId,
        string? selectedDemandNodeId,
        IReadOnlyList<CommercialFacilityPresentation> facilities)
    {
        Dictionary<string, SpatialNodeClassDefinition> classes = world.NodeClasses.ToDictionary(
            item => item.ClassId,
            StringComparer.Ordinal);
        foreach (SpatialNodeDefinition node in world.Nodes
            .OrderBy(node => ToCanvas(node.Position).Y)
            .ThenBy(node => ToCanvas(node.Position).X)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal))
        {
            if (!classes.TryGetValue(node.ClassId, out SpatialNodeClassDefinition? nodeClass))
            {
                continue;
            }
            ThermalAssetResult? thermal = thermalInterval?.Assets.FirstOrDefault(item =>
                item.AssetId == node.NodeId);
            Color color = node.Reserved
                ? Muted
                : node.Commissioned
                    ? ThermalColor(thermal?.CurrentState)
                    : Planned;
            if (node.Commissioned && node.NodeId == selectedThermalAssetId &&
                nodeClass.ServiceRadiusUnit > 0)
            {
                DrawServiceArea(node.Position, nodeClass.ServiceRadiusUnit);
            }
            bool emphasized = (!node.Reserved && !node.Commissioned) ||
                node.NodeId == selectedDemandNodeId ||
                node.NodeId == SelectedCandidateId;
            bool selectedPathNode =
                _presentation?.SelectedPathNodeIds.Contains(node.NodeId) ?? false;
            bool selectedPathSubstation =
                nodeClass.Kind == SpatialNodeKind.Substation && selectedPathNode;
            if (emphasized)
            {
                DrawFootprint(node.Position, nodeClass.FootprintRadiusUnit, color, 0.08f);
            }
            Vector2 center = ToCanvas(node.Position);
            float radius = nodeClass.Kind switch
            {
                SpatialNodeKind.SourceTerminal => 16f,
                SpatialNodeKind.Substation => 14f,
                SpatialNodeKind.DedicatedLoadTerminal => 13f,
                _ => 6f,
            };
            (Texture2D? texture, float maxSide) = NodeSprite(node, nodeClass);
            Vector2 spriteSize = texture is null
                ? Vector2.One * (radius * 2f)
                : FitSpriteSize(texture, maxSide * (1f + (ZoomIndex * 0.16f)));
            float objectRadius = Math.Max(radius, Math.Max(spriteSize.X, spriteSize.Y) * 0.42f);
            if (texture is not null)
            {
                Color spriteModulate = selectedPathNode &&
                    nodeClass.Kind == SpatialNodeKind.Pole
                        ? new Color(0.72f, 0.92f, 1.00f, 0.99f)
                        : NodeSpriteModulate(node, thermal);
                DrawTextureRect(
                    texture,
                    SpriteRect(center, spriteSize),
                    false,
                    spriteModulate);
            }
            if (selectedPathSubstation && node.NodeId != selectedThermalAssetId)
            {
                Vector2 half = spriteSize * 0.48f;
                DrawRect(
                    new Rect2(center - half, half * 2f),
                    new Color(CommissionedLine, 0.92f),
                    false,
                    2.2f);
            }
            if (node.Reserved && nodeClass.Kind == SpatialNodeKind.Pole)
            {
                DrawCircle(center, objectRadius * 0.72f, new Color(Planned, 0.10f));
                DrawArc(
                    center,
                    objectRadius + 3f,
                    0f,
                    Mathf.Tau,
                    28,
                    new Color(Planned, 0.92f),
                    2.4f,
                    true);
            }
            if ((!node.Commissioned && !node.Reserved) || thermal?.CurrentState is
                ThermalOperatingState.Emergency or ThermalOperatingState.ProtectiveOutage)
            {
                DrawArc(center, objectRadius, 0f, Mathf.Tau, 32, new Color(color, 0.88f), 2f, true);
            }
            if (texture is null)
            {
                DrawCircle(center, 4.2f, new Color(Background, 0.86f));
                DrawCircle(center, 2.7f, color);
            }
            if (nodeClass.Kind == SpatialNodeKind.DedicatedLoadTerminal)
            {
                CommercialFacilityPresentation facility = facilities.FirstOrDefault(item =>
                    item.NodeId == node.NodeId) ?? new CommercialFacilityPresentation(
                        node.NodeId,
                        CommercialFacilityState.Waiting,
                        "현재 국면 수요 없음");
                DrawFacilityStateMarker(center, objectRadius, facility);
            }
            if (thermal?.CurrentState == ThermalOperatingState.Emergency)
            {
                Vector2[] triangle =
                [
                    center + new Vector2(0f, -objectRadius - 8f),
                    center + new Vector2(-5f, -objectRadius + 1f),
                    center + new Vector2(5f, -objectRadius + 1f),
                ];
                DrawPolyline(triangle.Append(triangle[0]).ToArray(), color, 2f, true);
            }
            else if (thermal?.CurrentState == ThermalOperatingState.ProtectiveOutage)
            {
                DrawCross(center, color, objectRadius + 3f);
            }
            if (node.NodeId == selectedThermalAssetId)
            {
                Vector2 half = spriteSize * 0.53f;
                DrawRect(
                    new Rect2(center - half, half * 2f),
                    CommissionedLine,
                    false,
                    2.2f);
            }
            if (node.NodeId == selectedDemandNodeId)
            {
                DrawRect(new Rect2(
                        center - new Vector2(objectRadius + 8f, objectRadius + 8f),
                        Vector2.One * (objectRadius + 8f) * 2f),
                    Focus, false, 2f);
            }
            if (nodeClass.Kind != SpatialNodeKind.Pole && emphasized && !node.Reserved)
            {
                DrawMapLabel(
                    center + new Vector2(objectRadius + 8f, -objectRadius + 2f),
                    node.Reserved ? $"예정 · {node.DisplayName}" : node.DisplayName,
                    node.Reserved ? Muted : Text);
            }
        }
    }

    private (Texture2D? Texture, float MaxSide) NodeSprite(
        SpatialNodeDefinition node,
        SpatialNodeClassDefinition nodeClass)
    {
        if (node.Reserved && nodeClass.Kind == SpatialNodeKind.DedicatedLoadTerminal)
        {
            return (null, 24f);
        }
        if (node.AuthoredFoundation)
        {
            return (BridgeFoundationSprite, 58f);
        }
        return nodeClass.Kind switch
        {
            SpatialNodeKind.SourceTerminal => (TallThermalPowerStationBSprite, 222f),
            SpatialNodeKind.Substation =>
                (ChunkySwitchingSubstationBSprite ?? SubstationSprite, 156f),
            SpatialNodeKind.Pole when node.ClassId == "STANDARD_POLE" =>
                (StandardPoleSprite, 82f),
            SpatialNodeKind.Pole => (ReinforcedPoleSprite, 94f),
            SpatialNodeKind.DedicatedLoadTerminal when
                node.NodeId == "EAST_RESIDENTIAL_TERMINAL" =>
                (AtomicRowShopASprite, 116f),
            SpatialNodeKind.DedicatedLoadTerminal when
                node.NodeId == "HOSPITAL_TERMINAL" =>
                (AtomicHospitalMainASprite, 174f),
            SpatialNodeKind.DedicatedLoadTerminal when
                node.NodeId == "WATER_TERMINAL" =>
                (AtomicPumpHouseASprite, 118f),
            SpatialNodeKind.DedicatedLoadTerminal when
                node.NodeId == "INDUSTRY_TERMINAL" =>
                (AtomicSmallWarehouseASprite, 124f),
            _ => (null, 24f),
        };
    }

    private (Texture2D? Texture, float MaxSide) DraftPoleSprite(string poleClassId) =>
        poleClassId switch
        {
            "STANDARD_POLE" => (StandardPoleSprite, 82f),
            "REINFORCED_POLE" => (ReinforcedPoleSprite, 94f),
            _ => (null, 24f),
        };

    private (Texture2D? Texture, float MaxSide) DraftNodeSprite(string nodeClassId) =>
        nodeClassId switch
        {
            "SMALL_SUBSTATION" =>
                (ChunkySwitchingSubstationBSprite ?? SubstationSprite, 156f),
            _ => (null, 24f),
        };

    private void DrawDraftSprite(
        Texture2D? texture,
        CoreMapPoint point,
        float maxSide)
    {
        if (texture is null)
        {
            return;
        }
        Vector2 center = ToCanvas(point);
        Vector2 spriteSize = FitSpriteSize(texture, maxSide * (1f + (ZoomIndex * 0.16f)));
        float objectRadius = Math.Max(6f, Math.Max(spriteSize.X, spriteSize.Y) * 0.42f);
        DrawTextureRect(
            texture,
            SpriteRect(center, spriteSize),
            false,
            new Color(1f, 0.86f, 0.62f, 0.88f));
        DrawArc(center, objectRadius, 0f, Mathf.Tau, 32, new Color(Planned, 0.9f), 2f, true);
    }

    private static Vector2 FitSpriteSize(Texture2D texture, float maxSide)
    {
        float width = Math.Max(1, texture.GetWidth());
        float height = Math.Max(1, texture.GetHeight());
        float scale = maxSide / Math.Max(width, height);
        return new Vector2(width * scale, height * scale);
    }

    private static Rect2 SpriteRect(Vector2 groundAnchor, Vector2 spriteSize) => new(
        groundAnchor - new Vector2(spriteSize.X * 0.5f, spriteSize.Y * 0.78f),
        spriteSize);

    private static Color NodeSpriteModulate(
        SpatialNodeDefinition node,
        ThermalAssetResult? thermal)
    {
        if (thermal?.CurrentState == ThermalOperatingState.ProtectiveOutage)
        {
            return new Color(0.74f, 0.48f, 0.48f, 0.54f);
        }
        if (node.Reserved)
        {
            return new Color(0.84f, 0.70f, 0.48f, 0.72f);
        }
        if (!node.Commissioned)
        {
            return new Color(0.86f, 0.72f, 0.48f, 0.62f);
        }
        return new Color(0.94f, 0.96f, 0.92f, 0.98f);
    }

    private void DrawFacilityStateMarker(
        Vector2 center,
        float objectRadius,
        CommercialFacilityPresentation facility)
    {
        Color stateColor = facility.State switch
        {
            CommercialFacilityState.Supplied => CommissionedLine,
            CommercialFacilityState.Deferred => Muted,
            CommercialFacilityState.Unavailable => OutageLine,
            _ => IdleLine,
        };
        Vector2 badge = center + new Vector2(objectRadius * 0.72f, -objectRadius * 0.72f);
        DrawCircle(badge, 7f, new Color(Background, 0.88f));
        DrawArc(badge, 6f, 0f, Mathf.Tau, 20, stateColor, 2f, true);

        if (facility.State == CommercialFacilityState.Supplied)
        {
            DrawLine(badge + new Vector2(-3f, 0f), badge + new Vector2(-1f, 3f), stateColor, 2f);
            DrawLine(badge + new Vector2(-1f, 3f), badge + new Vector2(4f, -3f), stateColor, 2f);
        }
        else if (facility.State == CommercialFacilityState.Unavailable)
        {
            DrawCross(center, stateColor, objectRadius + 3f);
        }
        else if (facility.State == CommercialFacilityState.Deferred)
        {
            DrawDashedLine(badge + new Vector2(-4f, 0f), badge + new Vector2(4f, 0f),
                stateColor, 2f, 4f);
        }
    }

    private void DrawNodeDraft(ConstructionSnapshot snapshot)
    {
        if (snapshot.NodeDraft is not NodeDraftSnapshot draft)
        {
            return;
        }
        SpatialNodeClassDefinition nodeClass = snapshot.World.NodeClasses.Single(
            item => string.Equals(item.ClassId, draft.NodeClassId, StringComparison.Ordinal));
        if (nodeClass.ServiceRadiusUnit > 0)
        {
            DrawServiceArea(draft.Position, nodeClass.ServiceRadiusUnit);
        }
        DrawFootprint(draft.Position, nodeClass.FootprintRadiusUnit, Planned, 0.16f);
        (Texture2D? texture, float maxSide) = DraftNodeSprite(draft.NodeClassId);
        DrawDraftSprite(texture, draft.Position, maxSide);
        DrawCircle(ToCanvas(draft.Position), 6f, Planned);
    }

    private static Color ThermalColor(ThermalOperatingState? state) => state switch
    {
        ThermalOperatingState.Emergency => EmergencyLine,
        ThermalOperatingState.ProtectiveOutage => OutageLine,
        ThermalOperatingState.OverLimit => OverLimitLine,
        _ => CommissionedLine,
    };

    private void DrawCross(Vector2 center, Color color, float radius)
    {
        DrawLine(
            center + new Vector2(-radius, -radius),
            center + new Vector2(radius, radius),
            color,
            2f,
            true);
        DrawLine(
            center + new Vector2(-radius, radius),
            center + new Vector2(radius, -radius),
            color,
            2f,
            true);
    }

    private void DrawPointer(CommercialMapPresentation presentation)
    {
        if (_pointerPoint is not CoreMapPoint point)
        {
            return;
        }
        Color color = presentation.PointerAccepted ? Focus : Invalid;
        CoreMapPoint displayPoint = SelectedCandidateId is string candidateId
            ? presentation.Snapshot.World.Nodes.Single(node => node.NodeId == candidateId).Position
            : point;
        Vector2 center = ToCanvas(displayPoint);
        if (presentation.PointerFootprintRadiusUnit is int radius)
        {
            DrawFootprint(displayPoint, radius, color, 0.12f);
        }
        DrawArc(center, 10f, 0f, Mathf.Tau, 32, color, 2f, true);
        DrawLine(center + new Vector2(-14f, 0f), center + new Vector2(14f, 0f), color, 1f);
        DrawLine(center + new Vector2(0f, -14f), center + new Vector2(0f, 14f), color, 1f);

        string label = CandidateLabel(presentation.Snapshot.World) ?? presentation.PointerMessage;
        if (!string.IsNullOrWhiteSpace(label))
        {
            Vector2 labelPosition = center + new Vector2(14f, 27f);
            Vector2 labelSize = GetThemeDefaultFont().GetStringSize(
                label,
                HorizontalAlignment.Left,
                -1f,
                12);
            DrawRect(new Rect2(labelPosition - new Vector2(5f, 16f), labelSize + new Vector2(10f, 8f)),
                new Color(Background, 0.92f));
            DrawString(GetThemeDefaultFont(), labelPosition, label,
                HorizontalAlignment.Left, -1f, 12, color);
        }
    }

    private void DrawFootprint(CoreMapPoint point, int radiusUnit, Color color, float alpha)
    {
        DrawProjectedWorldCircle(point, radiusUnit, color, alpha, 1.2f, 48);
    }

    private void DrawServiceArea(CoreMapPoint point, int radiusUnit)
    {
        DrawProjectedWorldCircle(
            point,
            Math.Min(radiusUnit, 460),
            new Color(CommissionedLine, 0.42f),
            0.050f,
            1.25f,
            72,
            0.34f);
    }

    private void DrawProjectedWorldCircle(
        CoreMapPoint center,
        int radiusUnit,
        Color color,
        float fillAlpha,
        float width,
        int pointCount,
        float outlineAlpha = 0.72f)
    {
        int count = Math.Max(12, pointCount);
        Vector2[] polygon = Enumerable.Range(0, count)
            .Select(index =>
            {
                double angle = Mathf.Tau * index / count;
                return RequireTransform().WorldToCanvas(
                    center.XUnit + (Math.Cos(angle) * radiusUnit),
                    center.YUnit + (Math.Sin(angle) * radiusUnit));
            })
            .ToArray();
        if (fillAlpha > 0f)
        {
            DrawColoredPolygon(polygon, new Color(color, fillAlpha));
        }
        DrawPolyline(
            polygon.Append(polygon[0]).ToArray(),
            new Color(color, Math.Max(color.A, outlineAlpha)),
            width,
            true);
    }

    private void DrawMapLegend()
    {
        string label = $"{ZoomLabel}  ·  자유 배치  ·  1 = 100단위";
        DrawString(GetThemeDefaultFont(), new Vector2(18f, Size.Y - 13f), label,
            HorizontalAlignment.Left, -1f, 11, Muted);
    }

    private void DrawBuildRail()
    {
        (Texture2D? Texture, string Label, CommercialPanelAction? Action)[] slots =
        [
            (TallThermalPowerStationBSprite, "발전", null),
            (StandardPoleSprite, "1 선로", CommercialPanelAction.StartLine),
            (ChunkySwitchingSubstationBSprite ?? SubstationSprite,
                "2 변전", CommercialPanelAction.PlaceSubstation),
            (ReinforcedPoleSprite, "3 보강", CommercialPanelAction.CycleLineClass),
        ];
        float height = 18f + (slots.Length * BuildRailSlotHeight) +
            ((slots.Length - 1) * BuildRailGap) + 16f;
        Rect2 outer = new(
            new Vector2(BuildRailLeft, BuildRailTop - 18f),
            new Vector2(BuildRailSlotWidth + 16f, height));
        DrawChromeFrame(outer, Colors.White, 17f);

        for (int index = 0; index < slots.Length; index++)
        {
            float top = BuildRailTop + (index * (BuildRailSlotHeight + BuildRailGap));
            Rect2 slot = new(
                new Vector2(BuildRailLeft + 8f, top),
                new Vector2(BuildRailSlotWidth, BuildRailSlotHeight));
            bool active = slots[index].Action switch
            {
                CommercialPanelAction.StartLine =>
                    _presentation?.ToolLabel.Contains("선로", StringComparison.Ordinal) == true,
                CommercialPanelAction.PlaceSubstation =>
                    _presentation?.ToolLabel.Contains("변전", StringComparison.Ordinal) == true,
                CommercialPanelAction.CycleLineClass =>
                    _presentation?.ToolLabel.Contains("보강", StringComparison.Ordinal) == true,
                _ => false,
            };
            Color accent = active ? Focus : Color.FromHtml("685b48");
            DrawChromeFrame(
                slot,
                active ? new Color(0.96f, 0.78f, 0.43f, 1f) : new Color(0.76f, 0.72f, 0.65f, 1f),
                14f);
            DrawRect(slot, accent, false, active ? 2.4f : 1.3f);
            if (slots[index].Texture is Texture2D texture)
            {
                Vector2 iconSize = FitSpriteSize(texture, index == 0 ? 104f : 98f);
                Vector2 anchor = slot.GetCenter() + new Vector2(0f, 16f);
                DrawTextureRect(texture, SpriteRect(anchor, iconSize), false,
                    index == 0
                        ? new Color(0.60f, 0.61f, 0.58f, 0.74f)
                        : new Color(0.92f, 0.91f, 0.84f, 0.96f));
            }
            DrawString(
                GetThemeDefaultFont(),
                new Vector2(slot.Position.X, slot.End.Y - 7f),
                slots[index].Label,
                HorizontalAlignment.Center,
                slot.Size.X,
                11,
                active ? Focus : Text);
        }
    }

    private void DrawChromeFrame(Rect2 destination, Color modulate, float destinationSlice)
    {
        if (UiChromeFrameTexture is null)
        {
            DrawRect(destination, new Color(Color.FromHtml("090d0f"), 0.97f));
            DrawRect(destination, Color.FromHtml("755f3e"), false, 2f);
            return;
        }
        float sourceWidth = UiChromeFrameTexture.GetWidth();
        float sourceHeight = UiChromeFrameTexture.GetHeight();
        float sourceSlice = Math.Min(18f, Math.Min(sourceWidth, sourceHeight) * 0.25f);
        float drawSlice = Math.Min(
            destinationSlice,
            Math.Min(destination.Size.X, destination.Size.Y) * 0.34f);
        float[] sourceX = [0f, sourceSlice, sourceWidth - sourceSlice, sourceWidth];
        float[] sourceY = [0f, sourceSlice, sourceHeight - sourceSlice, sourceHeight];
        float[] drawX =
        [
            destination.Position.X,
            destination.Position.X + drawSlice,
            destination.End.X - drawSlice,
            destination.End.X,
        ];
        float[] drawY =
        [
            destination.Position.Y,
            destination.Position.Y + drawSlice,
            destination.End.Y - drawSlice,
            destination.End.Y,
        ];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                DrawTextureRectRegion(
                    UiChromeFrameTexture,
                    new Rect2(
                        drawX[column],
                        drawY[row],
                        drawX[column + 1] - drawX[column],
                        drawY[row + 1] - drawY[row]),
                    new Rect2(
                        sourceX[column],
                        sourceY[row],
                        sourceX[column + 1] - sourceX[column],
                        sourceY[row + 1] - sourceY[row]),
                    modulate);
            }
        }
    }

    private bool TryHandleBuildRailClick(Vector2 position)
    {
        float right = BuildRailLeft + BuildRailSlotWidth + 16f;
        if (position.X < BuildRailLeft || position.X > right || position.Y < BuildRailTop)
        {
            return false;
        }
        int slot = (int)((position.Y - BuildRailTop) / (BuildRailSlotHeight + BuildRailGap));
        if (slot < 0 || slot > 3)
        {
            return false;
        }
        float withinSlot = (position.Y - BuildRailTop) % (BuildRailSlotHeight + BuildRailGap);
        if (withinSlot > BuildRailSlotHeight)
        {
            return true;
        }
        CommercialPanelAction? action = slot switch
        {
            1 => CommercialPanelAction.StartLine,
            2 => CommercialPanelAction.PlaceSubstation,
            3 => CommercialPanelAction.CycleLineClass,
            _ => null,
        };
        if (action is CommercialPanelAction selected)
        {
            BuildRailActionRequested?.Invoke(selected);
        }
        return true;
    }

    private void DrawMapLabel(Vector2 position, string label, Color color)
    {
        Vector2 size = GetThemeDefaultFont().GetStringSize(
            label,
            HorizontalAlignment.Left,
            -1f,
            12);
        DrawRect(
            new Rect2(position - new Vector2(5f, 15f), size + new Vector2(10f, 7f)),
            new Color(Background, 0.82f));
        DrawString(GetThemeDefaultFont(), position, label,
            HorizontalAlignment.Left, -1f, 12, color);
    }

    private static Color BrassRisk() => Color.FromHtml("8c7047");

    private void DrawAreaLabel(Vector2[] polygon, string label, Color color)
    {
        float minX = polygon.Min(point => point.X);
        float minY = polygon.Min(point => point.Y);
        DrawString(GetThemeDefaultFont(), new Vector2(minX + 7f, minY + 17f), label,
            HorizontalAlignment.Left, -1f, 11, new Color(color, 0.9f));
    }

    private void DrawPolygonHatching(Vector2[] polygon, Color color, float spacing, float alpha)
    {
        float minX = polygon.Min(point => point.X);
        float maxX = polygon.Max(point => point.X);
        float minY = polygon.Min(point => point.Y);
        float maxY = polygon.Max(point => point.Y);
        for (float x = minX - (maxY - minY); x <= maxX; x += spacing)
        {
            var clipped = ClipLineToPolygon(
                new Vector2(x, maxY),
                new Vector2(x + (maxY - minY), minY),
                polygon);
            foreach ((Vector2 from, Vector2 to) in clipped)
            {
                DrawLine(from, to, new Color(color, alpha), 1f, true);
            }
        }
    }

    private static IReadOnlyList<(Vector2 From, Vector2 To)> ClipLineToPolygon(
        Vector2 from,
        Vector2 to,
        Vector2[] polygon)
    {
        var intersections = new List<Vector2>();
        if (Geometry2D.IsPointInPolygon(from, polygon))
        {
            intersections.Add(from);
        }
        for (int index = 0; index < polygon.Length; index++)
        {
            Variant hit = Geometry2D.SegmentIntersectsSegment(
                from,
                to,
                polygon[index],
                polygon[(index + 1) % polygon.Length]);
            if (hit.VariantType == Variant.Type.Vector2)
            {
                intersections.Add(hit.AsVector2());
            }
        }
        if (Geometry2D.IsPointInPolygon(to, polygon))
        {
            intersections.Add(to);
        }
        intersections = intersections
            .DistinctBy(point => (Mathf.RoundToInt(point.X * 10f), Mathf.RoundToInt(point.Y * 10f)))
            .OrderBy(point => point.DistanceSquaredTo(from))
            .ToList();
        var segments = new List<(Vector2 From, Vector2 To)>();
        for (int index = 0; index + 1 < intersections.Count; index += 2)
        {
            segments.Add((intersections[index], intersections[index + 1]));
        }
        return segments;
    }

    private string? CandidateLabel(SpatialWorldDefinition world)
    {
        if (SelectedCandidateId is not string nodeId)
        {
            return null;
        }
        SpatialNodeDefinition node = world.Nodes.Single(item => item.NodeId == nodeId);
        return _candidateNodeIds.Count == 1
            ? $"접속 · {node.DisplayName}"
            : $"접속 {_candidateIndex + 1}/{_candidateNodeIds.Count} · {node.DisplayName} · Q/E 변경";
    }

    private string BuildAccessibilityName(CommercialMapPresentation presentation)
    {
        string pointer = _pointerPoint is CoreMapPoint
            ? CandidateLabel(presentation.Snapshot.World) ?? presentation.PointerMessage
            : "지도 밖";
        string thermal = string.Empty;
        if (presentation.ThermalInterval is ThermalIntervalResult interval)
        {
            int emergency = interval.Assets.Count(item =>
                item.CurrentState == ThermalOperatingState.Emergency);
            int outage = interval.Assets.Count(item =>
                item.CurrentState == ThermalOperatingState.ProtectiveOutage);
            thermal = $" 열 상태: 비상 {emergency}곳, 보호정지 {outage}곳.";
        }
        string selectedPath = presentation.SelectedDemandNodeId is null
            ? " 선택 수요 경로가 없습니다."
            : $" 선택 수요 경로는 접속점 {presentation.SelectedPathNodeIds.Count}곳과 " +
              $"선로 {presentation.SelectedPathEdgeIds.Count}구간입니다.";
        Dictionary<string, string> nodeNames = presentation.Snapshot.World.Nodes.ToDictionary(
            item => item.NodeId,
            item => item.DisplayName,
            StringComparer.Ordinal);
        string facilities = presentation.Facilities.Count == 0
            ? string.Empty
            : " 시설 상태: " + string.Join(
                ", ",
                presentation.Facilities.Select(item =>
                    $"{(nodeNames.TryGetValue(item.NodeId, out string? name) ? name : "수요 시설")} " +
                    item.StatusText)) + ".";
        string motion = presentation.ReduceMotion ? " 지도 움직임 줄임 적용." : string.Empty;
        int reserved = presentation.Snapshot.World.Nodes.Count(item => item.Reserved);
        string reservedText = reserved == 0 ? string.Empty : $" 예정 시설 {reserved}곳.";
        return $"청류시 자유 배치 지도. {presentation.ToolLabel}. {pointer}. 지도 {ZoomLabel}." +
               $"{reservedText}{thermal}{selectedPath}{facilities}{motion}";
    }

    private Vector2 KeyboardAnchor() => RequireTransform().WorldToCanvas(
        _keyboardPoint.XUnit,
        _keyboardPoint.YUnit);

    private void OnResized()
    {
        if (_presentation is null || _transform is null)
        {
            return;
        }
        MapBounds bounds = _presentation.Snapshot.World.Bounds;
        _transform.Configure(
            new CommercialMapBounds(
                bounds.MinXUnit,
                bounds.MaxXUnit,
                bounds.MinYUnit,
                bounds.MaxYUnit),
            Size);
        RefreshCandidates(notify: true);
        QueueRedraw();
    }

    private bool IsPanButton(InputEventMouseButton button) =>
        button.ButtonIndex == MouseButton.Middle ||
        (button.ButtonIndex == MouseButton.Left && _spaceHeld);

    private void BeginPan(MouseButton button, Vector2 position)
    {
        GrabFocus();
        _panning = true;
        _panButton = button;
        _lastPanPosition = position;
        MouseDefaultCursorShape = CursorShape.Drag;
    }

    private void EndPan()
    {
        _panning = false;
        _panButton = MouseButton.None;
        MouseDefaultCursorShape = CursorShape.Arrow;
    }

    private bool TryBeginDraftPointDrag(Vector2 canvasPoint)
    {
        LineDraftSnapshot? draft = _presentation?.Snapshot.LineDraft;
        if (draft is null || draft.IntermediatePoints.Count == 0)
        {
            return false;
        }
        (int Index, float Distance) nearest = draft.IntermediatePoints
            .Select((point, index) => (
                Index: index,
                Distance: ToCanvas(point).DistanceSquaredTo(canvasPoint)))
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Index)
            .First();
        if (nearest.Distance > 14f * 14f)
        {
            return false;
        }
        GrabFocus();
        _draggingDraftPoint = true;
        _draggedDraftPointIndex = nearest.Index;
        RefreshCandidates(notify: false);
        DraftPointDragPreviewChanged?.Invoke(new CommercialDraftPointDrag(
            nearest.Index,
            draft.IntermediatePoints[nearest.Index]));
        return true;
    }

    private Vector2 ToCanvas(CoreMapPoint point) => RequireTransform().WorldToCanvas(
        point.XUnit,
        point.YUnit);

    private CommercialMapTransform RequireTransform() => _transform ??
        throw new InvalidOperationException("지도가 아직 준비되지 않았습니다.");

    private static CoreMapPoint InitialKeyboardPoint(SpatialWorldDefinition world)
    {
        SpatialNodeDefinition? source = world.Nodes.FirstOrDefault(node =>
            world.NodeClasses.Any(nodeClass =>
                nodeClass.ClassId == node.ClassId &&
                nodeClass.Kind == SpatialNodeKind.SourceTerminal));
        return source?.Position ?? new CoreMapPoint(
            (int)(((long)world.Bounds.MinXUnit + world.Bounds.MaxXUnit) / 2L),
            (int)(((long)world.Bounds.MinYUnit + world.Bounds.MaxYUnit) / 2L));
    }

    private static int RoundUnit(double value)
    {
        double rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded <= int.MinValue
            ? int.MinValue
            : rounded >= int.MaxValue
                ? int.MaxValue
                : (int)rounded;
    }

    private static int SaturatingAdd(int value, int delta)
    {
        long result = (long)value + delta;
        return result <= int.MinValue
            ? int.MinValue
            : result >= int.MaxValue
                ? int.MaxValue
                : (int)result;
    }
}
