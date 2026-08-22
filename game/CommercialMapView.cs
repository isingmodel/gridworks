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
    bool OperationsLocked,
    ThermalIntervalResult? ThermalInterval,
    string? SelectedThermalAssetId,
    string? SelectedDemandNodeId,
    IReadOnlyList<string> SelectedPathNodeIds,
    IReadOnlyList<string> SelectedPathEdgeIds,
    IReadOnlyList<IReadOnlyList<string>> ComparisonPathEdgeIds,
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

    private enum AtomicScenerySpriteKind
    {
        RubbleBankA,
        RockSoilTransitionA,
        ConiferA,
        ScrubA,
        OutcropA,
    }

    private enum AtomicSourcePartKind
    {
        MainHall,
        Smokestack,
        TurbineHall,
        BreakerBay,
    }

    private enum AtomicRiverEnvironmentKind
    {
        Conifer,
        Scrub,
        Outcrop,
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

    private readonly record struct AtomicSceneryInstanceSpec(
        AtomicScenerySpriteKind Kind,
        int XUnit,
        int YUnit,
        float MaxSide,
        float Alpha = 0.72f);

    private readonly record struct AtomicSourcePartSpec(
        AtomicSourcePartKind Kind,
        int OffsetXUnit,
        int OffsetYUnit,
        float MaxSide);

    private readonly record struct AtomicIndustrialPartInstanceSpec(
        AtomicSourcePartKind Kind,
        int XUnit,
        int YUnit,
        float MaxSide,
        float Alpha = 0.96f);

    private readonly record struct AtomicRiverEnvironmentInstanceSpec(
        AtomicRiverEnvironmentKind Kind,
        float Phase,
        bool LeftSide,
        float MaxSide,
        float OutwardOffset,
        float Alpha = 0.96f);

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
        // Continue beyond the playable rectangle so every supported camera sees
        // the same persistent river enter and leave the frame instead of a short
        // isolated pool. These are still authored centerline points; banks, water,
        // scenery, reflections, and bridges remain separately drawn runtime parts.
        new(1500, 2900),
        new(1350, 2400),
        new(1211, 2000),
        new(1250, 1900),
        new(1291, 1833),
        new(1402, 1666),
        new(1435, 1580),
        new(1461, 1500),
        new(1452, 1333),
        new(1371, 1166),
        new(1231, 1000),
        new(1175, 900),
        new(1151, 833),
        new(1160, 740),
        new(1192, 666),
        new(1240, 625),
        new(1300, 580),
        new(1290, 540),
        new(1310, 500),
    ];

    // Each record places one generated tree, scrub bush, or rock outcrop beside
    // one bank. The river itself remains authored geometry; no vegetation strip,
    // shoreline scene, or district raster is baked into these objects.
    private static readonly AtomicRiverEnvironmentInstanceSpec[]
        AtomicRiverEnvironmentInstances =
    [
        new(AtomicRiverEnvironmentKind.Outcrop, 0.05f, true, 54f, 10f),
        new(AtomicRiverEnvironmentKind.Scrub, 0.08f, false, 48f, 18f),
        new(AtomicRiverEnvironmentKind.Conifer, 0.11f, true, 76f, 24f),
        new(AtomicRiverEnvironmentKind.Scrub, 0.15f, true, 44f, 14f),
        new(AtomicRiverEnvironmentKind.Outcrop, 0.18f, false, 58f, 11f),
        new(AtomicRiverEnvironmentKind.Conifer, 0.22f, false, 70f, 25f),
        new(AtomicRiverEnvironmentKind.Scrub, 0.26f, true, 50f, 17f),
        new(AtomicRiverEnvironmentKind.Outcrop, 0.30f, true, 56f, 10f),
        new(AtomicRiverEnvironmentKind.Conifer, 0.34f, false, 82f, 26f),
        new(AtomicRiverEnvironmentKind.Scrub, 0.38f, false, 42f, 15f),
        new(AtomicRiverEnvironmentKind.Outcrop, 0.42f, true, 62f, 11f),
        new(AtomicRiverEnvironmentKind.Conifer, 0.46f, true, 72f, 24f),
        new(AtomicRiverEnvironmentKind.Scrub, 0.50f, false, 52f, 18f),
        new(AtomicRiverEnvironmentKind.Outcrop, 0.54f, false, 54f, 10f),
        new(AtomicRiverEnvironmentKind.Conifer, 0.58f, true, 78f, 27f),
        new(AtomicRiverEnvironmentKind.Scrub, 0.62f, true, 46f, 16f),
        new(AtomicRiverEnvironmentKind.Outcrop, 0.66f, false, 60f, 12f),
        new(AtomicRiverEnvironmentKind.Conifer, 0.70f, false, 74f, 24f),
        new(AtomicRiverEnvironmentKind.Scrub, 0.74f, true, 50f, 18f),
        new(AtomicRiverEnvironmentKind.Outcrop, 0.78f, true, 56f, 10f),
        new(AtomicRiverEnvironmentKind.Conifer, 0.82f, false, 80f, 26f),
        new(AtomicRiverEnvironmentKind.Scrub, 0.86f, false, 44f, 15f),
        new(AtomicRiverEnvironmentKind.Outcrop, 0.90f, true, 58f, 11f),
        new(AtomicRiverEnvironmentKind.Conifer, 0.94f, true, 72f, 24f),
    ];

    // A source terminal is assembled from four independently generated objects.
    // The authored source point remains the gameplay authority; these offsets are
    // presentation-only placement records and never replace the facility footprint.
    private static readonly AtomicSourcePartSpec[] AtomicSourcePartInstances =
    [
        new(AtomicSourcePartKind.Smokestack, 60, -150, 145f),
        new(AtomicSourcePartKind.TurbineHall, -170, 70, 164f),
        new(AtomicSourcePartKind.MainHall, 0, 0, 182f),
        new(AtomicSourcePartKind.BreakerBay, 190, 110, 102f),
    ];

    // One southern works compound assembled at runtime from five independent
    // functional objects. It remains inside SOUTH_CENTRAL_YARD_BLOCK; there is no
    // plant/district raster and the authored obstacle remains authoritative.
    private static readonly AtomicIndustrialPartInstanceSpec[] AtomicIndustrialPartInstances =
    [
        new(AtomicSourcePartKind.Smokestack, 1540, 1840, 150f),
        new(AtomicSourcePartKind.Smokestack, 1650, 1840, 142f),
        new(AtomicSourcePartKind.TurbineHall, 1570, 1950, 164f),
        new(AtomicSourcePartKind.MainHall, 1740, 1900, 188f),
        new(AtomicSourcePartKind.BreakerBay, 1810, 1950, 108f),
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
        new(AtomicCitySpriteKind.StreetLampA, 260, 320, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 560, 340, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 860, 320, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 1760, 320, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 2060, 340, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 2360, 320, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 2660, 340, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 2960, 320, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 460, 760, 56f),
        new(AtomicCitySpriteKind.StreetLampA, 760, 780, 56f),
        new(AtomicCitySpriteKind.StreetLampA, 2160, 760, 56f),
        new(AtomicCitySpriteKind.StreetLampA, 2460, 780, 56f),
        new(AtomicCitySpriteKind.RetainingWallA, 2700, 1020, 94f),
        new(AtomicCitySpriteKind.RetainingWallA, 2820, 1020, 94f),
        new(AtomicCitySpriteKind.RetainingWallA, 2940, 1020, 94f),
        new(AtomicCitySpriteKind.RetainingWallA, 3060, 1020, 94f),
        new(AtomicCitySpriteKind.RetainingWallA, 2680, 760, 90f),
        new(AtomicCitySpriteKind.RetainingWallA, 3120, 840, 90f),

        // Hospital and west-block furniture complete the original 80-instance gate.
        new(AtomicCitySpriteKind.StreetLampA, 720, 1160, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 980, 1160, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 1740, 1160, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 2020, 1160, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 2260, 1180, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 720, 1820, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 980, 1820, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 2060, 1820, 58f),
        new(AtomicCitySpriteKind.RetainingWallA, 2500, 1720, 104f),
        new(AtomicCitySpriteKind.RetainingWallA, 2680, 1720, 104f),
        new(AtomicCitySpriteKind.RetainingWallA, 2860, 1720, 104f),
        new(AtomicCitySpriteKind.RetainingWallA, 3000, 1720, 104f),
        new(AtomicCitySpriteKind.StreetLampA, 120, 1280, 56f),
        new(AtomicCitySpriteKind.StreetLampA, 260, 1280, 56f),
        new(AtomicCitySpriteKind.StreetLampA, 420, 1280, 56f),
        new(AtomicCitySpriteKind.StreetLampA, 580, 1280, 56f),

        // North works: ten individual structures inside NORTH_WORKS_BLOCK.
        new(AtomicCitySpriteKind.WorkshopA, 1160, 150, 86f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1360, 150, 92f),
        new(AtomicCitySpriteKind.WorkshopA, 1560, 150, 84f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1760, 150, 92f),
        new(AtomicCitySpriteKind.WorkshopA, 1960, 150, 86f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1160, 340, 94f),
        new(AtomicCitySpriteKind.WorkshopA, 1360, 340, 84f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1560, 340, 92f),
        new(AtomicCitySpriteKind.WorkshopA, 1760, 340, 86f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1960, 340, 94f),
        new(AtomicCitySpriteKind.WorkshopA, 1260, 245, 82f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1460, 245, 88f),
        new(AtomicCitySpriteKind.WorkshopA, 1660, 245, 82f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1860, 245, 88f),

        // North-east fringe: eight individually placed homes and shops.
        new(AtomicCitySpriteKind.WorkerHouseA, 2740, 150, 68f),
        new(AtomicCitySpriteKind.RowShopA, 2860, 150, 72f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2980, 150, 66f),
        new(AtomicCitySpriteKind.WorkerHouseC, 3100, 150, 72f),
        new(AtomicCitySpriteKind.RowShopA, 2740, 350, 72f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2860, 350, 70f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2980, 350, 68f),
        new(AtomicCitySpriteKind.WorkerHouseB, 3100, 350, 66f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2740, 250, 66f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2860, 250, 68f),
        new(AtomicCitySpriteKind.RowShopA, 2980, 250, 72f),
        new(AtomicCitySpriteKind.WorkerHouseC, 3100, 250, 70f),

        // Two compact authored obstacle islands fill the middle distance while
        // preserving every checker-owned construction corridor around them.
        new(AtomicCitySpriteKind.WorkshopA, 1450, 720, 82f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1530, 720, 86f),
        new(AtomicCitySpriteKind.WorkshopA, 1490, 780, 80f),
        new(AtomicCitySpriteKind.WorkshopA, 120, 800, 84f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 300, 800, 92f),
        new(AtomicCitySpriteKind.WorkshopA, 480, 800, 84f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 120, 980, 90f),
        new(AtomicCitySpriteKind.WorkshopA, 300, 980, 84f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 480, 980, 92f),

        // South freight edge: eight individual low industrial structures.
        new(AtomicCitySpriteKind.SmallWarehouseA, 1940, 1820, 92f),
        new(AtomicCitySpriteKind.WorkshopA, 2140, 1820, 84f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 2340, 1820, 94f),
        new(AtomicCitySpriteKind.WorkshopA, 2540, 1820, 86f),
        new(AtomicCitySpriteKind.WorkshopA, 1940, 1950, 82f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 2140, 1950, 92f),
        new(AtomicCitySpriteKind.WorkshopA, 2340, 1950, 84f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 2540, 1950, 94f),

        // Roadside furniture is still a list of independent object instances.
        // It stitches the playable districts into the same occupied industrial
        // landscape as the references without inventing impassable buildings.
        new(AtomicCitySpriteKind.StreetLampA, 180, 220, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 420, 220, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 660, 220, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 900, 220, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 1140, 220, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 1380, 220, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 1620, 220, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 1860, 220, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 2100, 220, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 2340, 220, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 2580, 220, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 2820, 220, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 3060, 220, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 180, 1900, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 460, 1900, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 740, 1900, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 1020, 1900, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 1860, 1900, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 2140, 1900, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 2420, 1900, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 2700, 1900, 58f),
        new(AtomicCitySpriteKind.StreetLampA, 2980, 1900, 58f),
        new(AtomicCitySpriteKind.RetainingWallA, 1040, 260, 90f),
        new(AtomicCitySpriteKind.RetainingWallA, 1960, 260, 90f),

        // Hospital neighbourhood: individual homes occupy the authored campus
        // perimeter, leaving the large medical objects readable at its core.
        new(AtomicCitySpriteKind.WorkerHouseA, 2390, 1320, 76f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2670, 1320, 72f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2840, 1320, 78f),
        new(AtomicCitySpriteKind.RowShopA, 3010, 1320, 80f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2380, 1450, 78f),
        new(AtomicCitySpriteKind.RowShopA, 2670, 1470, 80f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2850, 1480, 76f),
        new(AtomicCitySpriteKind.WorkerHouseB, 3010, 1460, 72f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2360, 1690, 72f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2540, 1690, 76f),
        new(AtomicCitySpriteKind.RowShopA, 2720, 1690, 80f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2900, 1690, 78f),
        new(AtomicCitySpriteKind.WorkerHouseA, 3020, 1690, 76f),

        // Extra west-industry sheds close the large gaps between its existing
        // nine objects while retaining each structure as a separate sprite.
        new(AtomicCitySpriteKind.WorkshopA, 120, 1260, 82f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 260, 1260, 90f),
        new(AtomicCitySpriteKind.WorkshopA, 420, 1260, 82f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 580, 1260, 90f),
        new(AtomicCitySpriteKind.WorkshopA, 100, 1410, 82f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 590, 1420, 90f),
        new(AtomicCitySpriteKind.WorkshopA, 100, 1570, 82f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 590, 1580, 90f),
        new(AtomicCitySpriteKind.WorkshopA, 120, 1710, 82f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 270, 1710, 90f),
        new(AtomicCitySpriteKind.WorkshopA, 440, 1710, 82f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 590, 1710, 90f),

        // Three additional authoritative obstacle islands fill the visible
        // middle distance. Every entry remains one generated object placement.
        new(AtomicCitySpriteKind.WorkshopA, 140, 340, 78f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 270, 340, 84f),
        new(AtomicCitySpriteKind.WorkshopA, 400, 340, 78f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 530, 340, 84f),
        new(AtomicCitySpriteKind.WorkshopA, 660, 340, 78f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 140, 460, 84f),
        new(AtomicCitySpriteKind.WorkshopA, 300, 460, 78f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 460, 460, 84f),
        new(AtomicCitySpriteKind.WorkshopA, 620, 460, 78f),
        new(AtomicCitySpriteKind.RowShopA, 380, 400, 74f),

        new(AtomicCitySpriteKind.SmallWarehouseA, 2110, 150, 92f),
        new(AtomicCitySpriteKind.WorkshopA, 2260, 150, 84f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 2420, 150, 92f),
        new(AtomicCitySpriteKind.WorkshopA, 2110, 310, 84f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 2260, 310, 92f),
        new(AtomicCitySpriteKind.WorkshopA, 2420, 310, 84f),
        new(AtomicCitySpriteKind.RowShopA, 2180, 230, 78f),
        new(AtomicCitySpriteKind.RowShopA, 2350, 230, 78f),

        new(AtomicCitySpriteKind.SmallWarehouseA, 1550, 1860, 90f),
        new(AtomicCitySpriteKind.WorkshopA, 1680, 1860, 82f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1810, 1860, 90f),
        new(AtomicCitySpriteKind.WorkshopA, 1550, 1960, 82f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1680, 1960, 90f),
        new(AtomicCitySpriteKind.WorkshopA, 1810, 1960, 82f),

        // A compact central-market obstacle block reduces the corridor void
        // without hiding or moving any authored network node.
        new(AtomicCitySpriteKind.RowShopA, 1610, 1140, 72f),
        new(AtomicCitySpriteKind.WorkshopA, 1680, 1140, 74f),
        new(AtomicCitySpriteKind.RowShopA, 1750, 1140, 72f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1610, 1210, 78f),
        new(AtomicCitySpriteKind.WorkshopA, 1680, 1210, 72f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1750, 1210, 78f),
        new(AtomicCitySpriteKind.WorkshopA, 1820, 1110, 74f),
        new(AtomicCitySpriteKind.RowShopA, 1870, 1110, 72f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1920, 1110, 78f),
        new(AtomicCitySpriteKind.RowShopA, 1820, 1170, 72f),
        new(AtomicCitySpriteKind.WorkshopA, 1870, 1170, 74f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1920, 1170, 78f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2460, 720, 74f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2530, 720, 72f),
        new(AtomicCitySpriteKind.RowShopA, 2600, 720, 76f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2460, 790, 76f),
        new(AtomicCitySpriteKind.RowShopA, 2530, 790, 76f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2600, 790, 74f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2460, 860, 72f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2530, 860, 76f),
        new(AtomicCitySpriteKind.RowShopA, 2600, 860, 76f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2490, 900, 72f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2560, 900, 70f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2630, 900, 74f),

        // Infill stays inside existing authoritative obstacle polygons. Each
        // record is still one generated building object; no cluster raster or
        // non-blocking fake building is introduced to increase apparent density.
        new(AtomicCitySpriteKind.WorkshopA, 1435, 685, 58f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1490, 685, 62f),
        new(AtomicCitySpriteKind.WorkshopA, 1550, 685, 58f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1435, 750, 60f),
        new(AtomicCitySpriteKind.WorkshopA, 1490, 750, 56f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1550, 750, 60f),
        new(AtomicCitySpriteKind.WorkshopA, 1435, 790, 54f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1520, 790, 58f),
        new(AtomicCitySpriteKind.WorkshopA, 1570, 790, 54f),

        new(AtomicCitySpriteKind.WorkshopA, 1120, 95, 68f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1290, 95, 74f),
        new(AtomicCitySpriteKind.WorkshopA, 1460, 95, 68f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1630, 95, 74f),
        new(AtomicCitySpriteKind.WorkshopA, 1800, 95, 68f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1970, 95, 74f),
        new(AtomicCitySpriteKind.WorkshopA, 1200, 285, 68f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1540, 285, 74f),
        new(AtomicCitySpriteKind.WorkshopA, 1880, 285, 68f),

        new(AtomicCitySpriteKind.WorkerHouseA, 1595, 1115, 54f),
        new(AtomicCitySpriteKind.WorkerHouseB, 1650, 1115, 52f),
        new(AtomicCitySpriteKind.RowShopA, 1710, 1115, 56f),
        new(AtomicCitySpriteKind.WorkerHouseC, 1595, 1195, 56f),
        new(AtomicCitySpriteKind.RowShopA, 1650, 1195, 56f),
        new(AtomicCitySpriteKind.WorkerHouseA, 1710, 1195, 54f),
        new(AtomicCitySpriteKind.WorkerHouseB, 1810, 1090, 52f),
        new(AtomicCitySpriteKind.RowShopA, 1850, 1090, 54f),
        new(AtomicCitySpriteKind.WorkerHouseC, 1900, 1090, 54f),
        new(AtomicCitySpriteKind.WorkerHouseA, 1810, 1175, 52f),
        new(AtomicCitySpriteKind.WorkerHouseB, 1860, 1175, 52f),
        new(AtomicCitySpriteKind.RowShopA, 1910, 1175, 54f),

        // Fine-grain infill: every record is one separately rendered building
        // inside an existing authoritative obstacle polygon. These smaller units
        // replace the coarse district read with the reference's many-building
        // urban grain; no record owns a parcel or combined silhouette.
        new(AtomicCitySpriteKind.WorkerHouseA, 2740, 645, 48f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2820, 645, 46f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2900, 645, 50f),
        new(AtomicCitySpriteKind.RowShopA, 2980, 645, 50f),
        new(AtomicCitySpriteKind.WorkerHouseA, 3060, 645, 48f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2740, 735, 46f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2820, 735, 50f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2900, 735, 48f),
        new(AtomicCitySpriteKind.RowShopA, 2980, 735, 50f),
        new(AtomicCitySpriteKind.WorkerHouseB, 3060, 735, 46f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2740, 825, 50f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2820, 825, 48f),
        new(AtomicCitySpriteKind.RowShopA, 2900, 825, 50f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2980, 825, 46f),
        new(AtomicCitySpriteKind.WorkerHouseC, 3060, 825, 50f),
        new(AtomicCitySpriteKind.RowShopA, 2740, 915, 50f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2820, 915, 46f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2900, 915, 48f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2980, 915, 50f),
        new(AtomicCitySpriteKind.WorkerHouseB, 3060, 915, 46f),

        new(AtomicCitySpriteKind.WorkerHouseA, 2400, 1340, 48f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2400, 1440, 46f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2400, 1540, 50f),
        new(AtomicCitySpriteKind.RowShopA, 2400, 1640, 50f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2400, 1720, 48f),
        new(AtomicCitySpriteKind.WorkerHouseB, 3000, 1340, 46f),
        new(AtomicCitySpriteKind.WorkerHouseC, 3000, 1440, 50f),
        new(AtomicCitySpriteKind.WorkerHouseA, 3000, 1540, 48f),
        new(AtomicCitySpriteKind.RowShopA, 3000, 1640, 50f),
        new(AtomicCitySpriteKind.WorkerHouseB, 3000, 1720, 46f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2520, 1710, 48f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2640, 1710, 46f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2760, 1710, 50f),
        new(AtomicCitySpriteKind.RowShopA, 2880, 1710, 50f),
        new(AtomicCitySpriteKind.WorkerHouseA, 2700, 1330, 48f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2860, 1330, 50f),

        new(AtomicCitySpriteKind.WorkshopA, 150, 1390, 50f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 290, 1390, 54f),
        new(AtomicCitySpriteKind.WorkshopA, 430, 1390, 50f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 570, 1390, 54f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 150, 1540, 54f),
        new(AtomicCitySpriteKind.WorkshopA, 290, 1540, 50f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 430, 1540, 54f),
        new(AtomicCitySpriteKind.WorkshopA, 570, 1540, 50f),
        new(AtomicCitySpriteKind.WorkshopA, 150, 1680, 50f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 290, 1680, 54f),
        new(AtomicCitySpriteKind.WorkshopA, 430, 1680, 50f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 570, 1680, 54f),

        new(AtomicCitySpriteKind.WorkshopA, 1150, 130, 48f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1320, 130, 52f),
        new(AtomicCitySpriteKind.WorkshopA, 1490, 130, 48f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1660, 130, 52f),
        new(AtomicCitySpriteKind.WorkshopA, 1830, 130, 48f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 2000, 130, 52f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1150, 310, 52f),
        new(AtomicCitySpriteKind.WorkshopA, 1320, 310, 48f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1490, 310, 52f),
        new(AtomicCitySpriteKind.WorkshopA, 1660, 310, 48f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 1830, 310, 52f),
        new(AtomicCitySpriteKind.WorkshopA, 2000, 310, 48f),

        new(AtomicCitySpriteKind.WorkerHouseA, 2760, 110, 48f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2880, 110, 46f),
        new(AtomicCitySpriteKind.RowShopA, 3000, 110, 50f),
        new(AtomicCitySpriteKind.WorkerHouseC, 3120, 110, 50f),
        new(AtomicCitySpriteKind.WorkerHouseB, 2760, 235, 46f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2880, 235, 50f),
        new(AtomicCitySpriteKind.WorkerHouseA, 3000, 235, 48f),
        new(AtomicCitySpriteKind.RowShopA, 3120, 235, 50f),
        new(AtomicCitySpriteKind.WorkerHouseC, 2760, 380, 50f),
        new(AtomicCitySpriteKind.RowShopA, 2880, 380, 50f),
        new(AtomicCitySpriteKind.WorkerHouseB, 3000, 380, 46f),
        new(AtomicCitySpriteKind.WorkerHouseA, 3120, 380, 48f),

        new(AtomicCitySpriteKind.SmallWarehouseA, 1960, 1840, 52f),
        new(AtomicCitySpriteKind.WorkshopA, 2160, 1840, 48f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 2360, 1840, 52f),
        new(AtomicCitySpriteKind.WorkshopA, 2560, 1840, 48f),
        new(AtomicCitySpriteKind.WorkshopA, 1960, 1950, 48f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 2160, 1950, 52f),
        new(AtomicCitySpriteKind.WorkshopA, 2360, 1950, 48f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 2560, 1950, 52f),

        new(AtomicCitySpriteKind.WorkshopA, 2100, 140, 48f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 2200, 140, 52f),
        new(AtomicCitySpriteKind.WorkshopA, 2300, 140, 48f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 2400, 140, 52f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 2100, 300, 52f),
        new(AtomicCitySpriteKind.WorkshopA, 2200, 300, 48f),
        new(AtomicCitySpriteKind.SmallWarehouseA, 2300, 300, 52f),
        new(AtomicCitySpriteKind.WorkshopA, 2400, 300, 48f),

    ];

    // Loose rubble is non-blocking scenery. Each record binds one short generated
    // rock/soil object; no entry owns a parcel, district, or map-sized raster.
    private static readonly AtomicSceneryInstanceSpec[] AtomicSceneryInstances =
    [
        new(AtomicScenerySpriteKind.RubbleBankA, 620, 650, 34f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 780, 700, 38f),
        new(AtomicScenerySpriteKind.RubbleBankA, 980, 750, 32f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 520, 900, 36f),
        new(AtomicScenerySpriteKind.RubbleBankA, 700, 980, 30f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 900, 1080, 38f),
        new(AtomicScenerySpriteKind.RubbleBankA, 520, 1160, 34f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 720, 1200, 36f),
        new(AtomicScenerySpriteKind.RubbleBankA, 960, 1180, 32f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 1050, 300, 40f),
        new(AtomicScenerySpriteKind.RubbleBankA, 1260, 430, 34f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 1500, 460, 38f),
        new(AtomicScenerySpriteKind.RubbleBankA, 1800, 430, 32f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 2050, 400, 38f),
        new(AtomicScenerySpriteKind.RubbleBankA, 1550, 900, 34f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 1750, 900, 40f),
        new(AtomicScenerySpriteKind.RubbleBankA, 1950, 900, 32f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 1550, 1040, 38f),
        new(AtomicScenerySpriteKind.RubbleBankA, 1850, 1050, 34f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 2050, 1080, 40f),
        new(AtomicScenerySpriteKind.RubbleBankA, 1500, 1300, 34f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 1680, 1320, 38f),
        new(AtomicScenerySpriteKind.RubbleBankA, 1850, 1370, 32f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 2050, 1400, 40f),
        new(AtomicScenerySpriteKind.RubbleBankA, 650, 1750, 34f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 820, 1820, 38f),
        new(AtomicScenerySpriteKind.RubbleBankA, 1000, 1880, 34f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 1550, 1750, 40f),
        new(AtomicScenerySpriteKind.RubbleBankA, 1720, 1780, 32f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 1880, 1720, 38f),
        new(AtomicScenerySpriteKind.RubbleBankA, 2050, 1750, 34f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 2200, 1800, 40f),
        new(AtomicScenerySpriteKind.RubbleBankA, 2500, 1100, 32f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 2750, 1150, 38f),
        new(AtomicScenerySpriteKind.RubbleBankA, 3000, 1200, 34f),
        new(AtomicScenerySpriteKind.RockSoilTransitionA, 2300, 1000, 40f),
        // Outer terrain relief uses separate generated environment objects. They
        // occupy the sparse regional margins without becoming collision geometry.
        new(AtomicScenerySpriteKind.OutcropA, 180, 180, 66f, 0.92f),
        new(AtomicScenerySpriteKind.ScrubA, 420, 120, 46f, 0.88f),
        new(AtomicScenerySpriteKind.ConiferA, 720, 110, 70f, 0.88f),
        new(AtomicScenerySpriteKind.OutcropA, 980, 90, 62f, 0.90f),
        new(AtomicScenerySpriteKind.ScrubA, 2220, 100, 48f, 0.88f),
        new(AtomicScenerySpriteKind.ConiferA, 2480, 90, 74f, 0.88f),
        new(AtomicScenerySpriteKind.OutcropA, 2780, 110, 68f, 0.92f),
        new(AtomicScenerySpriteKind.ScrubA, 3100, 180, 46f, 0.88f),
        new(AtomicScenerySpriteKind.ConiferA, 70, 460, 72f, 0.88f),
        new(AtomicScenerySpriteKind.OutcropA, 100, 720, 64f, 0.92f),
        new(AtomicScenerySpriteKind.ScrubA, 80, 980, 48f, 0.88f),
        new(AtomicScenerySpriteKind.ConiferA, 70, 1880, 76f, 0.88f),
        new(AtomicScenerySpriteKind.OutcropA, 330, 1940, 70f, 0.92f),
        new(AtomicScenerySpriteKind.ScrubA, 650, 1960, 48f, 0.88f),
        new(AtomicScenerySpriteKind.ConiferA, 920, 1940, 72f, 0.88f),
        new(AtomicScenerySpriteKind.OutcropA, 1220, 1970, 64f, 0.92f),
        new(AtomicScenerySpriteKind.ScrubA, 2080, 1960, 48f, 0.88f),
        new(AtomicScenerySpriteKind.ConiferA, 2380, 1940, 74f, 0.88f),
        new(AtomicScenerySpriteKind.OutcropA, 2680, 1960, 68f, 0.92f),
        new(AtomicScenerySpriteKind.ScrubA, 3020, 1940, 46f, 0.88f),
        new(AtomicScenerySpriteKind.ConiferA, 3160, 460, 72f, 0.88f),
        new(AtomicScenerySpriteKind.OutcropA, 3140, 760, 66f, 0.92f),
        new(AtomicScenerySpriteKind.ScrubA, 3160, 1060, 48f, 0.88f),
        new(AtomicScenerySpriteKind.ConiferA, 3150, 1840, 74f, 0.88f),
        new(AtomicScenerySpriteKind.OutcropA, 740, 430, 58f, 0.90f),
        new(AtomicScenerySpriteKind.ScrubA, 930, 520, 44f, 0.86f),
        new(AtomicScenerySpriteKind.ConiferA, 2120, 520, 66f, 0.86f),
        new(AtomicScenerySpriteKind.OutcropA, 2280, 660, 60f, 0.90f),
        new(AtomicScenerySpriteKind.ScrubA, 720, 1460, 46f, 0.86f),
        new(AtomicScenerySpriteKind.OutcropA, 1020, 1600, 62f, 0.90f),
        new(AtomicScenerySpriteKind.ConiferA, 2030, 1510, 68f, 0.86f),
        new(AtomicScenerySpriteKind.ScrubA, 2260, 1640, 46f, 0.86f),
        new(AtomicScenerySpriteKind.OutcropA, 1160, 620, 58f, 0.90f),
        new(AtomicScenerySpriteKind.ScrubA, 1380, 610, 44f, 0.86f),
        new(AtomicScenerySpriteKind.OutcropA, 1880, 620, 60f, 0.90f),
        new(AtomicScenerySpriteKind.ScrubA, 2080, 700, 46f, 0.86f),
        .. BuildReferenceRubbleField(),
    ];

    private static AtomicSceneryInstanceSpec[] BuildReferenceRubbleField()
    {
        // Ninety explicit runtime placements made from two short, individually
        // generated terrain objects. The deterministic field supplies the dense
        // broken-soil detail visible between reference districts without creating
        // a baked corridor, parcel, district, or collision-bearing fake building.
        var instances = new List<AtomicSceneryInstanceSpec>(90);
        for (int row = 0; row < 9; row++)
        {
            for (int column = 0; column < 10; column++)
            {
                int x = 500 + (column * 180) + ((row & 1) * 70);
                int y = 360 + (row * 170) + (((column * 37) + (row * 19)) % 54);
                AtomicScenerySpriteKind kind = ((column + (row * 2)) & 1) == 0
                    ? AtomicScenerySpriteKind.RubbleBankA
                    : AtomicScenerySpriteKind.RockSoilTransitionA;
                float maxSide = 40f + (((column * 5) + (row * 3)) % 5 * 4f);
                float alpha = 0.76f + (((column + row) % 3) * 0.05f);
                instances.Add(new AtomicSceneryInstanceSpec(kind, x, y, maxSide, alpha));
            }
        }
        return instances.ToArray();
    }

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

        // Continuous arterial fabric: every entry is one separately bound tile.
        // Water is rendered above these tiles and the two road crossings are then
        // restored by individual bridge objects at the authored foundations.
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 160, 260, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 380, 260, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 600, 260, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 820, 260, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1040, 260, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1260, 260, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1480, 260, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1700, 260, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1920, 260, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2140, 260, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2360, 260, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2580, 260, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2800, 260, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 3020, 260, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 160, 520, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 380, 520, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 600, 520, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 820, 520, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1040, 520, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1260, 520, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1480, 520, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1700, 520, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1920, 520, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2140, 520, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2360, 520, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2580, 520, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 2800, 520, 146f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 3020, 520, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 420, 760, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 420, 980, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 420, 1200, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 420, 1420, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 420, 1640, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 420, 1860, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 920, 780, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 920, 1000, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 920, 1220, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 920, 1440, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 920, 1660, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 2200, 360, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 2200, 580, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 2200, 800, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 2200, 1020, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 2200, 1240, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 2200, 1680, 146f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 2200, 1900, 146f),
        new(AtomicRoadSpriteKind.CrossJunction, 420, 520, 150f),
        new(AtomicRoadSpriteKind.CrossJunction, 920, 520, 150f),
        new(AtomicRoadSpriteKind.CrossJunction, 2200, 520, 150f),

        // Central service fabric is assembled from individual road tiles. Water
        // masks the pieces that pass below the channel, and the separate bridge
        // objects restore only the authored crossings above it.
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1100, 780, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1320, 780, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1540, 780, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1760, 780, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1980, 780, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1100, 1040, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1320, 1040, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1540, 1040, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1760, 1040, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1980, 1040, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1050, 1320, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1270, 1320, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1490, 1320, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1710, 1320, 142f),
        new(AtomicRoadSpriteKind.StraightNorthWestSouthEast, 1930, 1320, 142f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 1160, 700, 142f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 1160, 920, 142f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 1160, 1140, 142f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 1160, 1360, 142f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 1850, 700, 142f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 1850, 920, 142f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 1850, 1140, 142f),
        new(AtomicRoadSpriteKind.StraightNorthEastSouthWest, 1850, 1360, 142f),
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
    public Texture2D? RiverWaterNeutralBTile { get; set; }

    [Export]
    public Texture2D? RiverWaterHeatATile { get; set; }

    [Export]
    public Texture2D? RiverWaterFloodATile { get; set; }

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
    public Texture2D? AtomicStandardPoleASprite { get; set; }

    [Export]
    public Texture2D? AtomicReinforcedPoleASprite { get; set; }

    [Export]
    public Texture2D? AtomicBridgeFoundationASprite { get; set; }

    [Export]
    public Texture2D? IndustrialRoadBridgeASprite { get; set; }

    [Export]
    public Texture2D? IndustrialRoadBridgeBSprite { get; set; }

    [Export]
    public Texture2D? AtomicPlantMainHallASprite { get; set; }

    [Export]
    public Texture2D? AtomicPlantSmokestackASprite { get; set; }

    [Export]
    public Texture2D? AtomicPlantTurbineHallASprite { get; set; }

    [Export]
    public Texture2D? AtomicSwitchyardBreakerBayASprite { get; set; }

    [Export]
    public Texture2D? AtomicSubstationTransformerASprite { get; set; }

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
    public Texture2D? RiverBankLeftStraightASprite { get; set; }

    [Export]
    public Texture2D? RiverBankRightStraightASprite { get; set; }

    [Export]
    public Texture2D? RiverBankLeftInnerASprite { get; set; }

    [Export]
    public Texture2D? RiverBankLeftOuterASprite { get; set; }

    [Export]
    public Texture2D? RiverBankRightInnerASprite { get; set; }

    [Export]
    public Texture2D? RiverBankRightOuterASprite { get; set; }

    [Export]
    public Texture2D? RiverBridgeAbutmentASprite { get; set; }

    [Export]
    public Texture2D? RiverRockSoilTransitionASprite { get; set; }

    [Export]
    public Texture2D? RiverCurrentReflectionASprite { get; set; }

    [Export]
    public Texture2D? RiverFloodRippleASprite { get; set; }

    [Export]
    public Texture2D? RiverBankConiferASprite { get; set; }

    [Export]
    public Texture2D? RiverBankScrubASprite { get; set; }

    [Export]
    public Texture2D? RiverBankOutcropASprite { get; set; }

    [Export]
    public Texture2D? UiChromeFrameTexture { get; set; }

    [Export]
    public Texture2D? UiToolSlotTexture { get; set; }

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

    public bool OperationsLocked => _presentation?.OperationsLocked == true;

    public bool HasIndividualTileAssets =>
        GroundAsphaltTile is not null &&
        GroundScrubTile is not null &&
        GroundConcreteTile is not null &&
        GroundGravelTile is not null &&
        G3GroundRubbleMixBTile is not null &&
        G3GroundRubbleReliefCTile is not null &&
        RiverWaterTile is not null &&
        G3RiverWaterSurfaceTile is not null &&
        RiverWaterNeutralBTile is not null &&
        RiverWaterHeatATile is not null &&
        RiverWaterFloodATile is not null &&
        RoadStraightNorthWestSouthEastATile is not null &&
        RoadStraightNorthEastSouthWestATile is not null &&
        RoadCornerNorthEastATile is not null &&
        RoadTJunctionATile is not null &&
        RoadCrossJunctionATile is not null &&
        ServiceYardATile is not null;

    public bool HasIndividualObjectAssets =>
        HasAtomicGridAssets &&
        IndustrialRoadBridgeASprite is not null &&
        IndustrialRoadBridgeBSprite is not null &&
        G3RiverBankRockSegmentASprite is not null &&
        G3RiverBankInnerBendASprite is not null &&
        G3RiverBankOuterBendASprite is not null &&
        RiverBankLeftStraightASprite is not null &&
        RiverBankRightStraightASprite is not null &&
        RiverBankLeftInnerASprite is not null &&
        RiverBankLeftOuterASprite is not null &&
        RiverBankRightInnerASprite is not null &&
        RiverBankRightOuterASprite is not null &&
        RiverBridgeAbutmentASprite is not null &&
        RiverRockSoilTransitionASprite is not null &&
        RiverCurrentReflectionASprite is not null &&
        RiverFloodRippleASprite is not null &&
        RiverBankConiferASprite is not null &&
        RiverBankScrubASprite is not null &&
        RiverBankOutcropASprite is not null &&
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

    public bool HasAtomicGridAssets =>
        AtomicPlantMainHallASprite is not null &&
        AtomicPlantSmokestackASprite is not null &&
        AtomicPlantTurbineHallASprite is not null &&
        AtomicSwitchyardBreakerBayASprite is not null &&
        AtomicSubstationTransformerASprite is not null &&
        AtomicStandardPoleASprite is not null &&
        AtomicReinforcedPoleASprite is not null &&
        AtomicBridgeFoundationASprite is not null;

    private bool HasAtomicSourcePlantAssets =>
        AtomicPlantMainHallASprite is not null &&
        AtomicPlantSmokestackASprite is not null &&
        AtomicPlantTurbineHallASprite is not null &&
        AtomicSwitchyardBreakerBayASprite is not null;

    public int AtomicCityAssetCount => AtomicCityTextures().Count(texture => texture is not null);

    public int AtomicRoadTileAssetCount => AtomicRoadTextures().Count(texture => texture is not null);

    public int AtomicGridAssetCount => AtomicGridTextures().Count(texture => texture is not null);

    public int AtomicSourcePartInstanceCount => AtomicSourcePartInstances.Length;

    public int AtomicIndustrialPartInstanceCount => AtomicIndustrialPartInstances.Length;

    public int AtomicCityInstanceCount => AtomicCityInstances.Length;

    public int AtomicRoadInstanceCount => AtomicRoadInstances.Length;

    public int AtomicSceneryInstanceCount => AtomicSceneryInstances.Length;

    public int AtomicRiverEnvironmentInstanceCount =>
        AtomicRiverEnvironmentInstances.Length;

    public int AtomicWorldInstanceCount =>
        AtomicCityInstanceCount + AtomicRoadInstanceCount + AtomicSceneryInstanceCount +
        AtomicIndustrialPartInstanceCount + AtomicRiverEnvironmentInstanceCount;

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
            RiverWaterNeutralBTile,
            RiverWaterHeatATile,
            RiverWaterFloodATile,
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
            AtomicStandardPoleASprite,
            AtomicReinforcedPoleASprite,
            AtomicBridgeFoundationASprite,
            IndustrialRoadBridgeASprite,
            IndustrialRoadBridgeBSprite,
            AtomicPlantMainHallASprite,
            AtomicPlantSmokestackASprite,
            AtomicPlantTurbineHallASprite,
            AtomicSwitchyardBreakerBayASprite,
            AtomicSubstationTransformerASprite,
            G3RiverBankRockSegmentASprite,
            G3RiverBankInnerBendASprite,
            G3RiverBankOuterBendASprite,
            RiverBankLeftStraightASprite,
            RiverBankRightStraightASprite,
            RiverBankLeftInnerASprite,
            RiverBankLeftOuterASprite,
            RiverBankRightInnerASprite,
            RiverBankRightOuterASprite,
            RiverBridgeAbutmentASprite,
            RiverRockSoilTransitionASprite,
            RiverCurrentReflectionASprite,
            RiverFloodRippleASprite,
            RiverBankConiferASprite,
            RiverBankScrubASprite,
            RiverBankOutcropASprite,
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
        if (presentation.OperationsLocked && _draggingDraftPoint)
        {
            _draggingDraftPoint = false;
            _draggedDraftPointIndex = -1;
            DraftPointDragPreviewChanged?.Invoke(null);
        }
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
        AccessibilityDescription = presentation.OperationsLocked
            ? "읽기 전용 지도입니다. 공사 도구, 접속점 선택, 되돌리기는 잠겨 있습니다. " +
              "마우스 가운데 버튼 또는 Space+드래그로 이동하고 휠이나 +/-로 확대하며 Home으로 전체 보기를 복원합니다."
            : "청류시 자유 배치 지도. 건물 한 채와 도로 한 조각 단위의 원자 자산이 v2 좌표에 배치됩니다. " +
              "방향키로 커서를 움직이고 Enter로 선택합니다. Q와 E로 가까운 접속점을 바꿉니다.";
        QueueRedraw();
    }

#if DEBUG || COMMERCIAL_INTERNAL
    internal void SetChapterIndexForPresentationSmoke(int chapterIndex)
    {
        if (_presentation is null)
        {
            throw new InvalidOperationException(
                "표현 smoke chapter override에는 현재 map presentation이 필요합니다.");
        }
        _presentation = _presentation with { ChapterIndex = chapterIndex };
        QueueRedraw();
    }

    internal void SetCameraForSmoke(Vector2 center, int zoomIndex)
    {
        RequireTransform().SetViewForSmoke(center, zoomIndex);
        RefreshCandidates(notify: true);
        CameraChanged?.Invoke();
        QueueRedraw();
    }
#endif

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
                if (!OperationsLocked)
                {
                    UndoRequested?.Invoke();
                }
                AcceptEvent();
                return;

            case InputEventMouseButton button when
                button.ButtonIndex == MouseButton.Left && button.Pressed:
                GrabFocus();
                if (!OperationsLocked &&
                    TryMapPoint(button.Position, out CoreMapPoint clicked))
                {
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
        DrawAtomicScenery();
        DrawChapterGroundState(_presentation.ChapterIndex);
        // Water belongs below the physical district/object layer. Drawing it after
        // the city made the channel read as a flat blue UI ribbon laid over roofs.
        DrawWaterTerrain(snapshot.World);
        DrawTerrain(snapshot.World);
        DrawAtomicCity();
        DrawAtomicIndustrialParts();
        // Mission 2 is an explicit two-route comparison. Keep the authored cut
        // facts in the inspector, but do not lay the large diagnostic risk mask
        // over both alternatives; it destroys the reference's clean A/B read.
        if (_presentation.ChapterIndex is not 1 and not 5)
        {
            DrawRiskAreas(snapshot.World, _presentation.ActiveRiskAreaIds);
        }
        DrawChapterAtmosphere(_presentation.ChapterIndex, _presentation.ReduceMotion);
        DrawReservedRouteCorridor(snapshot.World);
        DrawEdges(snapshot.World, _presentation.ThermalInterval);
        DrawComparisonPaths(snapshot.World, _presentation);
        DrawSelectedDemandPath(snapshot.World, _presentation);
        if (_presentation.ChapterIndex is not 1 and not 5)
        {
            DrawUnavailableMarks(snapshot.World, _presentation.ThermalInterval);
        }
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
        if (OperationsLocked && IsConstructionKey(key))
        {
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
            !_presentation.OperationsLocked &&
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
        if (OperationsLocked || _candidateNodeIds.Count == 0)
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
            bool heat = _presentation?.ChapterIndex == 4;
            DrawTextureRectRegion(
                ground,
                new Rect2(Vector2.Zero, Size),
                new Rect2(Vector2.Zero, Size * 1.72f),
                heat
                    ? new Color(0.98f, 0.77f, 0.55f, 1f)
                    : new Color(0.88f, 0.83f, 0.75f, 1f));
            DrawRect(
                new Rect2(Vector2.Zero, Size),
                new Color(Color.FromHtml("10100f"), heat ? 0.04f : 0.08f));
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
            (180, 180, 1040, 720, 0.20f),
            (820, 260, 1780, 1080, 0.22f),
            (1540, 180, 2500, 900, 0.18f),
            (1020, 1080, 1740, 1680, 0.19f),
            (420, 1120, 1040, 1780, 0.17f),
            (1880, 980, 2920, 1860, 0.16f),
        ];
        bool heat = _presentation?.ChapterIndex == 4;
        foreach ((int minX, int minY, int maxX, int maxY, float alpha) in patches)
        {
            DrawWorldQuadTexture(
                rubble,
                minX,
                minY,
                maxX,
                maxY,
                new Color(
                    heat ? 0.96f : 0.84f,
                    heat ? 0.68f : 0.76f,
                    heat ? 0.42f : 0.62f,
                    heat ? alpha * 1.55f : alpha));
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
                new Color(0.94f, 0.88f, 0.78f, 0.68f));
            Vector2[] road = WorldQuad(minX, minY, maxX, maxY);
            DrawPolyline(
                road.Append(road[0]).ToArray(),
                new Color(Color.FromHtml("c0a783"), 0.26f),
                1.35f,
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
            // Preserve the localized hot sun and material response below. A
            // stronger full-map veil flattened every object into one orange
            // sheet and erased the reference's charcoal/amber separation.
            4 => new Color(Color.FromHtml("ba6c3e"), 0.14f),
            5 => new Color(Color.FromHtml("264a62"), 0.16f),
            7 => new Color(Color.FromHtml("1b2851"), 0.20f),
            _ => new Color(Color.FromHtml("31564f"), 0.06f),
        };
        DrawRect(new Rect2(Vector2.Zero, Size), tint);

        if (index == 4)
        {
            // Heat is a world state, not only a header label. A fixed upper-right
            // glare keeps the same authored map readable while matching the hot,
            // low-angle light language of the reference. These translucent discs
            // are presentation lighting; every building, river segment, and route
            // remains an independently drawn runtime object underneath.
            Vector2 sun = new(Size.X * 0.58f, Math.Max(104f, Size.Y * 0.14f));
            DrawCircle(sun, 290f, new Color(Color.FromHtml("e8a35a"), 0.040f));
            DrawCircle(sun, 195f, new Color(Color.FromHtml("f0b768"), 0.060f));
            DrawCircle(sun, 112f, new Color(Color.FromHtml("ffd38a"), 0.092f));
            DrawCircle(sun, 32f, new Color(Color.FromHtml("fff1c2"), 0.78f));
        }

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

    private Texture2D?[] AtomicGridTextures() =>
    [
        AtomicPlantMainHallASprite,
        AtomicPlantSmokestackASprite,
        AtomicPlantTurbineHallASprite,
        AtomicSwitchyardBreakerBayASprite,
        AtomicSubstationTransformerASprite,
        AtomicStandardPoleASprite,
        AtomicReinforcedPoleASprite,
        AtomicBridgeFoundationASprite,
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

    private Texture2D? AtomicSourcePartTexture(AtomicSourcePartKind kind) => kind switch
    {
        AtomicSourcePartKind.MainHall => AtomicPlantMainHallASprite,
        AtomicSourcePartKind.Smokestack => AtomicPlantSmokestackASprite,
        AtomicSourcePartKind.TurbineHall => AtomicPlantTurbineHallASprite,
        AtomicSourcePartKind.BreakerBay => AtomicSwitchyardBreakerBayASprite,
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
                instance.MaxSide * 0.82f * (1f + (ZoomIndex * 0.08f)));
            DrawTextureRect(
                texture,
                new Rect2(center - (size * 0.5f), size),
                false,
                new Color(0.94f, 0.88f, 0.78f, instance.Alpha * 0.78f));
        }
    }

    private Texture2D? AtomicSceneryTexture(AtomicScenerySpriteKind kind) => kind switch
    {
        AtomicScenerySpriteKind.RubbleBankA => G3RiverBankRockSegmentASprite,
        AtomicScenerySpriteKind.RockSoilTransitionA => RiverRockSoilTransitionASprite,
        AtomicScenerySpriteKind.ConiferA => RiverBankConiferASprite,
        AtomicScenerySpriteKind.ScrubA => RiverBankScrubASprite,
        AtomicScenerySpriteKind.OutcropA => RiverBankOutcropASprite,
        _ => null,
    };

    private void DrawAtomicScenery()
    {
        foreach (AtomicSceneryInstanceSpec instance in AtomicSceneryInstances
            .OrderBy(item => ToCanvas(new CoreMapPoint(item.XUnit, item.YUnit)).Y)
            .ThenBy(item => ToCanvas(new CoreMapPoint(item.XUnit, item.YUnit)).X)
            .ThenBy(item => item.Kind))
        {
            Texture2D? texture = AtomicSceneryTexture(instance.Kind);
            if (texture is null)
            {
                continue;
            }
            Vector2 center = ToCanvas(new CoreMapPoint(instance.XUnit, instance.YUnit));
            Vector2 size = FitSpriteSize(texture, instance.MaxSide * 1.65f);
            DrawTextureRect(
                texture,
                SpriteRect(center, size),
                false,
                new Color(0.84f, 0.78f, 0.67f, instance.Alpha));
        }
    }

    private void DrawChapterGroundState(int chapterIndex)
    {
        Texture2D? rubble = G3GroundRubbleReliefCTile ?? G3GroundRubbleMixBTile;
        if (chapterIndex == 2)
        {
            // Siting exposes a surveyed field between persistent city masses.
            // This is a runtime ground treatment; all separately placed solid
            // buildings are drawn afterward and remain intact.
            if (rubble is not null)
            {
                DrawWorldQuadTexture(
                    rubble,
                    1180,
                    580,
                    2240,
                    1420,
                    new Color(0.82f, 0.72f, 0.55f, 0.72f));
            }
            Vector2[] reserve = WorldQuad(1180, 580, 2240, 1420);
            DrawPolyline(
                reserve.Append(reserve[0]).ToArray(),
                new Color(Planned, 0.26f),
                1.2f,
                true);
            return;
        }
        if (chapterIndex != 4)
        {
            return;
        }

        if (rubble is not null)
        {
            // Heat deposits a dusty, granular basin over the central service
            // fabric while leaving authoritative solid objects visible.
            DrawWorldQuadTexture(
                rubble,
                1020,
                600,
                2120,
                1480,
                new Color(0.96f, 0.65f, 0.37f, 0.58f));
        }

        // Heat changes the ground material itself. These deterministic crack
        // branches are drawn below water and buildings, so the same assembled
        // world reads as desiccated rather than as a uniformly tinted screenshot.
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 12; column++)
            {
                int x = 180 + (column * 265) + ((row & 1) * 75);
                int y = 180 + (row * 235) + (((column * 41) + (row * 23)) % 86);
                Vector2 center = ToCanvas(new CoreMapPoint(x, y));
                float length = 9f + (((column * 3) + row) % 5 * 2.2f);
                float angle = -0.92f + (((column + (row * 2)) % 7) * 0.23f);
                Vector2 trunk = Vector2.FromAngle(angle) * length;
                DrawLine(
                    center - trunk,
                    center + trunk,
                    new Color(Color.FromHtml("17130f"), 0.56f),
                    2.2f,
                    true);
                DrawLine(
                    center - trunk,
                    center + trunk,
                    new Color(Color.FromHtml("b47a43"), 0.30f),
                    0.8f,
                    true);
                Vector2 branchStart = center + (trunk * 0.14f);
                for (int branch = -1; branch <= 1; branch += 2)
                {
                    Vector2 branchVector = Vector2.FromAngle(angle + (branch * 0.82f)) *
                        (length * 0.62f);
                    DrawLine(
                        branchStart,
                        branchStart + branchVector,
                        new Color(Color.FromHtml("1a1510"), 0.48f),
                        1.5f,
                        true);
                }
            }
        }
    }

    private void DrawAtomicCity()
    {
        int chapter = _presentation?.ChapterIndex ?? 0;
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
            bool heavyIndustrialDistrict =
                (instance.XUnit <= 650 && instance.YUnit is >= 1230 and <= 1750) ||
                (instance.XUnit is >= 1500 and <= 1840 && instance.YUnit >= 1810);
            bool denseEasternDistrict =
                instance.XUnit >= 2340 && instance.YUnit is >= 560 and <= 1760;
            bool centralInfillDistrict =
                instance.XUnit is >= 1400 and <= 1980 &&
                instance.YUnit is >= 650 and <= 1250;
            float districtMassScale = heavyIndustrialDistrict
                ? 1.48f
                : denseEasternDistrict ? 1.10f
                : centralInfillDistrict ? 1.14f : 1.06f;
            if (chapter == 1 && centralInfillDistrict)
            {
                // Keep both route corridors readable without hollowing out the
                // city mass. Each house remains a separate placed object.
                districtMassScale = 1.08f;
            }
            if (chapter == 2)
            {
                // The siting reference deliberately separates an eastern town
                // and a lower industrial mass with an open river valley. Express
                // that distribution by scaling the same atomic placements, not by
                // covering them with a precomposed valley/city plate.
                bool openSitingValley =
                    instance.XUnit is >= 700 and <= 2200 && instance.YUnit <= 1580;
                districtMassScale = heavyIndustrialDistrict
                    ? 1.62f
                    : denseEasternDistrict ? 1.20f
                    : openSitingValley ? 0.58f : 0.88f;
            }
            Vector2 size = FitSpriteSize(
                texture,
                instance.MaxSide * 0.98f * districtMassScale *
                    (1f + (ZoomIndex * 0.10f)));
            Color modulate = instance.Kind switch
            {
                AtomicCitySpriteKind.HospitalMainA =>
                    new Color(0.98f, 0.96f, 0.90f, instance.Alpha),
                AtomicCitySpriteKind.WaterTankA =>
                    new Color(0.90f, 0.89f, 0.84f, instance.Alpha),
                AtomicCitySpriteKind.StreetLampA =>
                    new Color(1.00f, 0.94f, 0.76f, instance.Alpha),
                _ => new Color(0.92f, 0.87f, 0.79f, instance.Alpha),
            };
            if (chapter == 4 &&
                instance.Kind != AtomicCitySpriteKind.StreetLampA)
            {
                modulate = new Color(
                    modulate.R * 0.84f,
                    modulate.G * 0.76f,
                    modulate.B * 0.66f,
                    modulate.A);
            }
            Vector2 center = ToCanvas(new CoreMapPoint(instance.XUnit, instance.YUnit));
            DrawSpriteGroundShadow(center, size);
            DrawTextureRect(
                texture,
                SpriteRect(
                    center,
                    size),
                false,
                modulate);
        }
    }

    private void DrawAtomicIndustrialParts()
    {
        foreach (AtomicIndustrialPartInstanceSpec instance in AtomicIndustrialPartInstances
            .OrderBy(item => ToCanvas(new CoreMapPoint(item.XUnit, item.YUnit)).Y)
            .ThenBy(item => ToCanvas(new CoreMapPoint(item.XUnit, item.YUnit)).X)
            .ThenBy(item => item.Kind))
        {
            Texture2D? texture = AtomicSourcePartTexture(instance.Kind);
            if (texture is null)
            {
                continue;
            }
            Vector2 center = ToCanvas(new CoreMapPoint(instance.XUnit, instance.YUnit));
            float industrialScale = _presentation?.ChapterIndex == 2 ? 1.24f : 0.92f;
            Vector2 size = FitSpriteSize(
                texture,
                instance.MaxSide * industrialScale * (1f + (ZoomIndex * 0.08f)));
            DrawSpriteGroundShadow(center, size);
            DrawTextureRect(
                texture,
                SpriteRect(center, size),
                false,
                new Color(0.90f, 0.86f, 0.78f, instance.Alpha));
        }
    }

    private void DrawSpriteGroundShadow(Vector2 groundAnchor, Vector2 spriteSize)
    {
        float halfWidth = Math.Clamp(spriteSize.X * 0.36f, 7f, 54f);
        float halfDepth = Math.Clamp(spriteSize.Y * 0.10f, 3f, 14f);
        Vector2[] shadow =
        [
            groundAnchor + new Vector2(-halfWidth, 0f),
            groundAnchor + new Vector2(0f, -halfDepth),
            groundAnchor + new Vector2(halfWidth, 0f),
            groundAnchor + new Vector2(0f, halfDepth),
        ];
        DrawColoredPolygon(shadow, new Color(0f, 0f, 0f, 0.34f));
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
                12);
            centerline = RuggedOpenPolyline(centerline, 1.65f, 0.7f);
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
            1 => 0.44f,
            2 => 0.44f,
            4 => 0.42f,
            // Flooding darkens and roughens the same persistent channel. It does
            // not replace the valley with a hard polygon sheet; wet-bank cues and
            // reflections carry the state while bridge attachment stays fixed.
            5 => 0.18f,
            // The siting reference is the one intentionally broad rural valley;
            // route and heat views keep the same channel much narrower.
            7 => 0.25f,
            _ => 0.44f,
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
        float visibleBankFraction = chapter switch
        {
            2 => 0.42f,
            4 => 0.48f,
            5 => 0.56f,
            7 => 0.55f,
            _ => 0.36f,
        };
        Vector2[] outerLeft = Enumerable.Range(0, surfaceLeft.Length)
            .Select(index => surfaceLeft[index].Lerp(leftBank[index], visibleBankFraction))
            .ToArray();
        Vector2[] outerRight = Enumerable.Range(0, surfaceRightAscending.Length)
            .Select(index => surfaceRightAscending[index].Lerp(
                rightBankAscending[index],
                visibleBankFraction))
            .ToArray();
        DrawRiverBankStrip(outerLeft, surfaceLeft, bank);
        DrawRiverBankStrip(surfaceRightAscending, outerRight, bank);
        // Recess the river with layered low-alpha soil shadows. A single opaque
        // contour made the otherwise smooth spline read as a vector cut-out.
        DrawPolyline(
            outerLeft.Select(point => point + new Vector2(0f, 3f)).ToArray(),
            new Color(Color.FromHtml("111513"), 0.18f),
            10.0f,
            true);
        DrawPolyline(
            outerRight.Select(point => point + new Vector2(0f, 3f)).ToArray(),
            new Color(Color.FromHtml("111513"), 0.18f),
            10.0f,
            true);
        DrawPolyline(outerLeft, new Color(Color.FromHtml("262823"), 0.28f), 3.0f, true);
        DrawPolyline(outerRight, new Color(Color.FromHtml("262823"), 0.28f), 3.0f, true);
        DrawPolyline(outerLeft, new Color(Color.FromHtml("8b795e"), 0.19f), 1.0f, true);
        DrawPolyline(outerRight, new Color(Color.FromHtml("8b795e"), 0.19f), 1.0f, true);
        // Two irregular contour ledges make the channel visibly recessed. They
        // are derived from the same runtime bank geometry rather than baked into
        // a river or district image.
        Vector2[] leftLedge = Enumerable.Range(0, outerLeft.Length)
            .Select(index => outerLeft[index].Lerp(surfaceLeft[index], 0.34f))
            .ToArray();
        Vector2[] rightLedge = Enumerable.Range(0, outerRight.Length)
            .Select(index => outerRight[index].Lerp(surfaceRightAscending[index], 0.34f))
            .ToArray();
        DrawPolyline(leftLedge, new Color(Color.FromHtml("1b1b18"), 0.34f), 4.2f, true);
        DrawPolyline(rightLedge, new Color(Color.FromHtml("1b1b18"), 0.34f), 4.2f, true);
        DrawPolyline(leftLedge, new Color(Color.FromHtml("9b805d"), 0.14f), 1.0f, true);
        DrawPolyline(rightLedge, new Color(Color.FromHtml("9b805d"), 0.14f), 1.0f, true);
        if (chapter == 5)
        {
            DrawPolyline(outerLeft, new Color(Color.FromHtml("4b8192"), 0.10f), 4.0f, true);
            DrawPolyline(outerRight, new Color(Color.FromHtml("4b8192"), 0.10f), 4.0f, true);
        }
        // Bank sprites are terrain objects, so composite them before the water.
        // The water then masks each sprite's irregular inner edge while its outer
        // rock/soil silhouette remains visible on land. Drawing these afterward
        // made the dark sprite matte intrude into the channel like floating debris.
        DrawRiverBankObjects(outerLeft, leftSide: true);
        DrawRiverBankObjects(outerRight, leftSide: false);
        DrawAtomicRiverEnvironment(outerLeft, outerRight);
        Color waterModulate = chapter switch
        {
            4 => new Color(0.62f, 0.63f, 0.58f, 1.00f),
            5 => new Color(0.72f, 0.82f, 0.86f, 1.00f),
            7 => new Color(0.76f, 0.83f, 0.88f, 0.98f),
            _ => new Color(0.70f, 0.78f, 0.79f, 1.00f),
        };
        Texture2D? primaryWaterTexture = chapter switch
        {
            4 => RiverWaterHeatATile ?? texture,
            5 => RiverWaterFloodATile ?? texture,
            _ => texture,
        };
        Texture2D? secondaryWaterTexture = chapter switch
        {
            4 => RiverWaterHeatATile ?? primaryWaterTexture,
            5 => RiverWaterFloodATile ?? primaryWaterTexture,
            _ => RiverWaterNeutralBTile ?? primaryWaterTexture,
        };
        Color waterLift = chapter switch
        {
            4 => new Color(Color.FromHtml("243234"), 0.18f),
            5 => new Color(Color.FromHtml("31515c"), 0.10f),
            _ => new Color(Color.FromHtml("1d3035"), 0.06f),
        };
        DrawRiverSurfaceSegments(
            surfaceLeft,
            surfaceRightAscending,
            primaryWaterTexture,
            secondaryWaterTexture,
            waterModulate,
            primaryWaterTexture is null ? new Color(Water, 0.96f) : waterLift);
        DrawPolyline(surfaceLeft, new Color(Color.FromHtml("111817"), 0.16f), 7.0f, true);
        DrawPolyline(surfaceRightAscending, new Color(Color.FromHtml("111817"), 0.16f), 7.0f, true);
        DrawPolyline(surfaceLeft, new Color(Color.FromHtml("111817"), 0.25f), 2.0f, true);
        DrawPolyline(surfaceRightAscending, new Color(Color.FromHtml("111817"), 0.25f), 2.0f, true);
        DrawPolyline(surfaceLeft, new Color(Color.FromHtml("748588"), 0.14f), 0.9f, true);
        DrawPolyline(surfaceRightAscending, new Color(Color.FromHtml("748588"), 0.14f), 0.9f, true);
        for (int index = 8; index < surfaceLeft.Length - 8; index += 13)
        {
            DrawLine(
                surfaceLeft[index].Lerp(surfaceRightAscending[index], 0.18f),
                surfaceLeft[index].Lerp(surfaceRightAscending[index], 0.72f),
                new Color(Color.FromHtml("718789"), index % 26 == 8 ? 0.15f : 0.09f),
                1.4f,
                true);
        }
        DrawRiverReflections(surface, chapter);
        DrawRiverShoals(surfaceLeft, surfaceRightAscending);
        // Both authored foundations stay visibly attached to the river. The
        // northern crossing is subordinate so the lower crossing remains the
        // persistent landmark instead of reading as a repeated bridge showcase.
        DrawRiverBridgeDeck(new CoreMapPoint(1330, 500), 1.18f, chapter, 0.58f);
        DrawRiverBridgeDeck(new CoreMapPoint(1480, 1500), 1.18f, chapter);
    }

    private static (Vector2[] Left, Vector2[] Right) BuildSingleRiverBanks(
        Vector2[] centerline,
        int chapter)
    {
        // The carved valley is persistent across chapters. Only the exposed
        // water surface changes through the inset above; this keeps bank objects,
        // bridges, and simulation crossings spatially registered in every state.
        const float baseHalfWidth = 64f;
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
                : new Color(0.76f, 0.67f, 0.53f, 0.78f),
            GroundGravelTile is null ? Colors.Transparent : new Color(bank, 0.25f));
    }

    private void DrawRiverSurfaceSegments(
        Vector2[] leftEdge,
        Vector2[] rightEdge,
        Texture2D? primaryTexture,
        Texture2D? secondaryTexture,
        Color textureModulate,
        Color overlay)
    {
        int segmentCount = Math.Min(leftEdge.Length, rightEdge.Length) - 1;
        for (int index = 0; index < segmentCount; index++)
        {
            Texture2D? texture = index % 2 == 0
                ? primaryTexture
                : secondaryTexture ?? primaryTexture;
            Color fill = texture is null ? overlay : textureModulate;
            Color segmentOverlay = texture is null ? Colors.Transparent : overlay;
            Vector2[] first = [leftEdge[index], leftEdge[index + 1], rightEdge[index + 1]];
            Vector2[] second = [leftEdge[index], rightEdge[index + 1], rightEdge[index]];
            DrawStripTriangle(first, texture, fill, segmentOverlay);
            DrawStripTriangle(second, texture, fill, segmentOverlay);
        }
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
        if (triangle.Any(point =>
                !float.IsFinite(point.X) || !float.IsFinite(point.Y)))
        {
            return;
        }
        float signedTwiceArea =
            (triangle[1] - triangle[0]).Cross(triangle[2] - triangle[0]);
        if (!float.IsFinite(signedTwiceArea) || Math.Abs(signedTwiceArea) < 0.5f)
        {
            return;
        }
        if (signedTwiceArea < 0f)
        {
            (triangle[1], triangle[2]) = (triangle[2], triangle[1]);
        }
        if (texture is null)
        {
            DrawColoredPolygon(triangle, fill);
        }
        else
        {
            DrawColoredPolygon(triangle, fill, TiledTextureUvs(triangle, texture), texture);
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
        Texture2D? straight = G3RiverBankRockSegmentASprite ?? (leftSide
            ? RiverBankLeftStraightASprite
            : RiverBankRightStraightASprite);
        if (straight is null || bank.Length < 7)
        {
            return;
        }
        Texture2D? inner = G3RiverBankInnerBendASprite ?? (leftSide
            ? RiverBankLeftInnerASprite
            : RiverBankRightInnerASprite);
        Texture2D? outer = G3RiverBankOuterBendASprite ?? (leftSide
            ? RiverBankLeftOuterASprite
            : RiverBankRightOuterASprite);

        for (int index = 7; index < bank.Length - 7; index += 11)
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
                    ? inner ?? straight
                    : outer ?? straight;
            if (Math.Abs(turn) < 0.035f && index % 33 == 7 &&
                RiverRockSoilTransitionASprite is not null)
            {
                bankTexture = RiverRockSoilTransitionASprite;
            }
            float rotation = tangent.Angle();
            Vector2 bankNormal = new Vector2(-tangent.Y, tangent.X).Normalized();
            Vector2 objectCenter = bank[index] +
                (bankNormal * (leftSide ? -8f : 8f));
            float maxSide = Math.Abs(turn) >= 0.035f
                ? 48f
                : bankTexture == RiverRockSoilTransitionASprite
                    ? 54f
                    : index % 22 == 7 ? 38f : 32f;
            Vector2 size = FitSpriteSize(bankTexture, maxSide * 0.94f);
            DrawSetTransform(objectCenter, rotation, Vector2.One);
            DrawTextureRect(
                bankTexture,
                new Rect2(size * -0.5f, size),
                false,
                new Color(0.74f, 0.69f, 0.60f, 0.72f));
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private Texture2D? AtomicRiverEnvironmentTexture(AtomicRiverEnvironmentKind kind) =>
        kind switch
        {
            AtomicRiverEnvironmentKind.Conifer => RiverBankConiferASprite,
            AtomicRiverEnvironmentKind.Scrub => RiverBankScrubASprite,
            AtomicRiverEnvironmentKind.Outcrop => RiverBankOutcropASprite,
            _ => null,
        };

    private void DrawAtomicRiverEnvironment(Vector2[] leftBank, Vector2[] rightBank)
    {
        if (leftBank.Length < 7 || leftBank.Length != rightBank.Length)
        {
            return;
        }

        foreach ((AtomicRiverEnvironmentInstanceSpec Spec, Vector2 Anchor) placement in
                 AtomicRiverEnvironmentInstances
                     .Select(spec =>
                     {
                         int index = Math.Clamp(
                             Mathf.RoundToInt(spec.Phase * (leftBank.Length - 1)),
                             3,
                             leftBank.Length - 4);
                         Vector2[] bank = spec.LeftSide ? leftBank : rightBank;
                         Vector2 tangent = (bank[index + 2] - bank[index - 2]).Normalized();
                         Vector2 normal = new(-tangent.Y, tangent.X);
                         Vector2 outward = spec.LeftSide ? -normal : normal;
                         return (Spec: spec, Anchor: bank[index] +
                             (outward * spec.OutwardOffset));
                     })
                     .OrderBy(item => item.Anchor.Y)
                     .ThenBy(item => item.Anchor.X)
                     .ThenBy(item => item.Spec.Kind))
        {
            Texture2D? texture = AtomicRiverEnvironmentTexture(placement.Spec.Kind);
            if (texture is null)
            {
                continue;
            }
            Vector2 size = FitSpriteSize(
                texture,
                placement.Spec.MaxSide * 1.08f * (1f + (ZoomIndex * 0.10f)));
            DrawSpriteGroundShadow(placement.Anchor, size);
            DrawTextureRect(
                texture,
                SpriteRect(placement.Anchor, size),
                false,
                new Color(0.92f, 0.88f, 0.78f, placement.Spec.Alpha * 0.82f));
        }
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
        float[] phases = [0.19f, 0.37f, 0.61f, 0.79f];
        foreach (float phase in phases)
        {
            int index = Math.Clamp(
                Mathf.RoundToInt((left.Length - 1) * phase),
                2,
                left.Length - 3);
            Vector2 tangent = ((left[index + 2] + right[index + 2]) -
                (left[index - 2] + right[index - 2])) * 0.5f;
            Vector2 center = left[index].Lerp(right[index], phase == 0.47f ? 0.62f : 0.48f);
            Texture2D phaseTexture = shoalTexture;
            Vector2 size = FitSpriteSize(
                phaseTexture,
                phase is > 0.30f and < 0.70f ? 17f : 13f);
            DrawSetTransform(center, tangent.Angle(), Vector2.One);
            DrawTextureRect(
                phaseTexture,
                new Rect2(size * -0.5f, size),
                false,
                new Color(0.58f, 0.54f, 0.47f, 0.80f));
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void DrawRiverBridgeAbutment(Vector2 center, float rotation, float maxSide)
    {
        Texture2D? abutmentTexture = RiverBridgeAbutmentASprite ?? AtomicBridgeFoundationASprite ??
            G3RiverBankRockSegmentASprite;
        if (abutmentTexture is null)
        {
            return;
        }
        Vector2 size = FitSpriteSize(abutmentTexture, maxSide);
        DrawSetTransform(center, 0f, Vector2.One);
        DrawTextureRect(
            abutmentTexture,
            new Rect2(size * -0.5f, size),
            false,
            new Color(0.72f, 0.68f, 0.60f, 0.94f));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void DrawRiverBridgeDeck(
        CoreMapPoint position,
        float rotation,
        int chapter,
        float visualScale = 1f)
    {
        Vector2 center = ToCanvas(position);
        Vector2 bridgeAxis = new(Mathf.Cos(rotation), Mathf.Sin(rotation));
        float bridgeMaxSide = chapter switch
        {
            1 => 78f,
            2 => 108f,
            4 => 82f,
            5 => 114f,
            7 => 104f,
            _ => 86f,
        };
        float abutmentMaxSide = chapter switch
        {
            1 => 30f,
            2 => 42f,
            4 => 32f,
            5 => 44f,
            7 => 40f,
            _ => 34f,
        };
        float anchorOffset = chapter switch
        {
            1 => 21f,
            2 => 30f,
            4 => 22f,
            5 => 31f,
            7 => 29f,
            _ => 23f,
        };
        DrawRiverBridgeAbutment(
            center - (bridgeAxis * anchorOffset * visualScale),
            rotation,
            abutmentMaxSide * visualScale);
        DrawRiverBridgeAbutment(
            center + (bridgeAxis * anchorOffset * visualScale),
            rotation + Mathf.Pi,
            abutmentMaxSide * visualScale);
        if (IndustrialRoadBridgeBSprite is null)
        {
            return;
        }

        Vector2 size = FitSpriteSize(
            IndustrialRoadBridgeBSprite,
            bridgeMaxSide * visualScale);
        // B is generated as one transparent, steep NW-SE isometric bridge object.
        // Its baked long axis matches this crossing's authored bridge axis, so it
        // is placed directly rather than rotating a shallow deck into a wall.
        DrawSetTransform(center, 0f, Vector2.One);
        DrawTextureRect(
            IndustrialRoadBridgeBSprite,
            new Rect2(size * -0.5f, size),
            false,
            new Color(0.76f, 0.72f, 0.64f, 0.94f));
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

    private static Vector2[] TiledTextureUvs(Vector2[] polygon, Texture2D texture)
    {
        const float ScreenRepeatPeriod = 96f;
        return polygon.Select(point => new Vector2(
            (point.X / ScreenRepeatPeriod) * Math.Max(1, texture.GetWidth() - 1),
            (point.Y / ScreenRepeatPeriod) * Math.Max(1, texture.GetHeight() - 1))).ToArray();
    }

    private static Vector2[] RiverTextureUvs(Vector2[] polygon, Texture2D texture) =>
        TextureUvs(polygon, texture)
            .Select(uv => uv * 2.8f)
            .ToArray();

    private void DrawRiverReflections(Vector2[] polygon, int chapter)
    {
        Texture2D? effectTexture = chapter == 5
            ? RiverFloodRippleASprite ?? RiverCurrentReflectionASprite
            : RiverCurrentReflectionASprite;
        if (effectTexture is not null)
        {
            Vector2[] centerline = SmoothOpenPolyline(
                ReferenceRiverControlPoints.Select(ToCanvas).ToArray(),
                8);
            Color modulate = chapter == 4
                ? new Color(0.78f, 0.63f, 0.46f, 0.42f)
                : chapter == 5
                    ? new Color(0.82f, 0.92f, 0.95f, 0.60f)
                    : new Color(0.76f, 0.88f, 0.92f, 0.44f);
            int step = chapter == 5 ? 10 : 16;
            for (int index = 12; index < centerline.Length - 12; index += step)
            {
                Vector2 tangent = centerline[index + 3] - centerline[index - 3];
                Vector2 size = FitSpriteSize(
                    effectTexture,
                    chapter == 5
                        ? index % 20 == 12 ? 118f : 92f
                        : index % 32 == 12 ? 78f : 62f);
                DrawSetTransform(centerline[index], tangent.Angle(), Vector2.One);
                DrawTextureRect(
                    effectTexture,
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
        "NORTH_WORKS_BLOCK" => null,
        "NORTH_EAST_FRINGE_BLOCK" => null,
        "SOUTH_FREIGHT_BLOCK" => null,
        "CENTRAL_MAINTENANCE_BLOCK" => null,
        "WEST_MID_BLOCK" => null,
        "WEST_NORTH_YARD_BLOCK" => null,
        "CENTRAL_NORTH_YARD_BLOCK" => null,
        "SOUTH_CENTRAL_YARD_BLOCK" => null,
        "CENTRAL_MARKET_BLOCK" => null,
        "CENTRAL_EAST_MARKET_BLOCK" => null,
        "TERMINAL_EAST_BLOCK" => null,
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
            DrawColoredPolygon(polygon, new Color(Risk, isActive ? 0.06f : 0.018f));
            DrawPolyline(
                polygon.Append(polygon[0]).ToArray(),
                new Color(isActive ? Risk : BrassRisk(), isActive ? 0.76f : 0.24f),
                isActive ? 2f : 1f,
                true);
            if (isActive)
            {
                DrawPolygonHatching(polygon, Risk, 26f, 0.13f);
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
            bool comparisonEdge = _presentation?.ChapterIndex == 1 &&
                _presentation.ComparisonPathEdgeIds.Any(path =>
                    path.Contains(edge.EdgeId, StringComparer.Ordinal));
            if (comparisonEdge)
            {
                // The two authoritative comparison paths are redrawn below as
                // distinct A/B overlays. Do not first paint the same edges as one
                // thick cyan bundle, which visually merges the alternatives.
                continue;
            }
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
            new(500, 1480),
            new(760, 1560),
            new(1040, 1660),
            new(1320, 1760),
            new(1620, 1720),
            new(1940, 1580),
            new(2260, 1360),
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
        if (AtomicReinforcedPoleASprite is null)
        {
            return;
        }
        for (int index = 0; index < supports.Length; index++)
        {
            Vector2 ground = supports[index];
            int chapterIndex = _presentation?.ChapterIndex ?? 0;
            float maxSide = chapterIndex == 2 ? 72f : chapterIndex == 4 ? 78f : 96f;
            Vector2 size = FitSpriteSize(AtomicReinforcedPoleASprite, maxSide);
            DrawCircle(ground, 11f, new Color(Planned, 0.10f));
            DrawTextureRect(
                AtomicReinforcedPoleASprite,
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
        bool sparseHeatSpan = _presentation?.ChapterIndex == 4;
        Vector2[][] strands = sparseHeatSpan
            ?
            [
                center.Select(point => point + (normal * 0.72f)).ToArray(),
                center.Select(point => point - (normal * 0.72f)).ToArray(),
            ]
            :
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
        if (strands.Length == 3)
        {
            DrawPolyline(strands[2], new Color(color, 0.96f), 1.45f, true);
        }
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
            SpatialNodeKind.Pole => node.AuthoredFoundation
                ? 66f
                : node.ClassId == "STANDARD_POLE" ? 100f : 104f,
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
            // conductor glow. A restrained fixed dash makes the currently
            // inspected source-to-facility route traceable through a dense network
            // without replacing the physical three-strand conductor underneath.
            DrawDashedLine(
                start,
                end,
                new Color(Focus, 0.90f),
                1.35f,
                11f,
                true,
                true);
        }
    }

    private void DrawComparisonPaths(
        SpatialWorldDefinition world,
        CommercialMapPresentation presentation)
    {
        if (presentation.ChapterIndex != 1 ||
            presentation.ComparisonPathEdgeIds.Count < 2)
        {
            return;
        }

        Dictionary<string, SpatialNodeDefinition> nodes = world.Nodes.ToDictionary(
            node => node.NodeId,
            StringComparer.Ordinal);
        Dictionary<string, SpatialNodeClassDefinition> classes = world.NodeClasses.ToDictionary(
            item => item.ClassId,
            StringComparer.Ordinal);
        Color[] routeColors = [Planned, Color.FromHtml("86dce7")];
        for (int routeIndex = 0;
             routeIndex < Math.Min(routeColors.Length, presentation.ComparisonPathEdgeIds.Count);
             routeIndex++)
        {
            Color routeColor = routeColors[routeIndex];
            HashSet<string> route = presentation.ComparisonPathEdgeIds[routeIndex]
                .ToHashSet(StringComparer.Ordinal);
            bool routeLabelDrawn = false;
            foreach (SpatialEdgeDefinition edge in world.Edges.Where(item => route.Contains(item.EdgeId)))
            {
                if (!nodes.TryGetValue(edge.FromNodeId, out SpatialNodeDefinition? from) ||
                    !nodes.TryGetValue(edge.ToNodeId, out SpatialNodeDefinition? to))
                {
                    continue;
                }
                Vector2 start = ConductorAnchor(from, classes);
                Vector2 end = ConductorAnchor(to, classes);
                DrawDashedLine(start, end, new Color(Background, 0.88f), 5.2f, 14f, true, true);
                DrawDashedLine(start, end, new Color(routeColor, 0.98f), 2.6f, 14f, true, true);
                DrawCircle(start, 6.2f, new Color(routeColor, 0.94f));
                DrawCircle(start, 2.4f, Background);
                DrawCircle(end, 6.2f, new Color(routeColor, 0.94f));
                DrawCircle(end, 2.4f, Background);
                if (!routeLabelDrawn)
                {
                    Vector2 badge = start.Lerp(end, 0.42f);
                    DrawCircle(badge, 13f, new Color(Background, 0.92f));
                    DrawArc(badge, 12f, 0f, Mathf.Tau, 28, routeColor, 2f, true);
                    DrawString(
                        GetThemeDefaultFont(),
                        badge + new Vector2(-4f, 5f),
                        routeIndex == 0 ? "A" : "B",
                        HorizontalAlignment.Left,
                        -1f,
                        13,
                        routeColor);
                    routeLabelDrawn = true;
                }
            }
        }
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

    private void DrawAtomicSourcePlant(CoreMapPoint sourcePoint, Color modulate)
    {
        foreach ((AtomicSourcePartSpec Part, CoreMapPoint Point) placement in
                 AtomicSourcePartInstances
                     .Select(part => (
                         Part: part,
                         Point: new CoreMapPoint(
                             sourcePoint.XUnit + part.OffsetXUnit,
                             sourcePoint.YUnit + part.OffsetYUnit)))
                     .OrderBy(item => ToCanvas(item.Point).Y)
                     .ThenBy(item => ToCanvas(item.Point).X)
                     .ThenBy(item => item.Part.Kind))
        {
            Texture2D? texture = AtomicSourcePartTexture(placement.Part.Kind);
            if (texture is null)
            {
                continue;
            }
            Vector2 center = ToCanvas(placement.Point);
            float sourceScale = _presentation?.ChapterIndex == 2 ? 1.12f : 0.78f;
            Vector2 size = FitSpriteSize(
                texture,
                placement.Part.MaxSide * sourceScale * (1f + (ZoomIndex * 0.12f)));
            DrawTextureRect(texture, SpriteRect(center, size), false, modulate);
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
            bool sourceEnsemble = nodeClass.Kind == SpatialNodeKind.SourceTerminal &&
                HasAtomicSourcePlantAssets;
            int chapterIndex = _presentation?.ChapterIndex ?? 0;
            Vector2 spriteSize = sourceEnsemble
                ? new Vector2(260f, 215f) *
                    (chapterIndex == 2 ? 1.28f : 1f) *
                    (1f + (ZoomIndex * 0.12f))
                : texture is null
                ? Vector2.One * (radius * 2f)
                : FitSpriteSize(texture, maxSide * (1f + (ZoomIndex * 0.16f)));
            if (nodeClass.Kind == SpatialNodeKind.Pole && chapterIndex is 1 or 2 or 4)
            {
                spriteSize *= chapterIndex switch
                {
                    1 => 0.78f,
                    2 => 0.76f,
                    _ => 0.82f,
                };
            }
            float objectRadius = Math.Max(radius, Math.Max(spriteSize.X, spriteSize.Y) * 0.42f);
            if (sourceEnsemble)
            {
                DrawAtomicSourcePlant(node.Position, NodeSpriteModulate(node, thermal));
            }
            else if (texture is not null)
            {
                Color spriteModulate = selectedPathNode &&
                    nodeClass.Kind == SpatialNodeKind.Pole
                        ? new Color(0.68f, 0.82f, 0.84f, 0.99f)
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
            return (AtomicBridgeFoundationASprite, 62f);
        }
        return nodeClass.Kind switch
        {
            SpatialNodeKind.SourceTerminal => (AtomicPlantMainHallASprite, 158f),
            SpatialNodeKind.Substation =>
                (AtomicSubstationTransformerASprite, 110f),
            SpatialNodeKind.Pole when node.ClassId == "STANDARD_POLE" =>
                (AtomicStandardPoleASprite, 82f),
            SpatialNodeKind.Pole => (AtomicReinforcedPoleASprite, 92f),
            SpatialNodeKind.DedicatedLoadTerminal when
                node.NodeId == "EAST_RESIDENTIAL_TERMINAL" =>
                (AtomicRowShopASprite, 104f),
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
            "STANDARD_POLE" => (AtomicStandardPoleASprite, 94f),
            "REINFORCED_POLE" => (AtomicReinforcedPoleASprite, 102f),
            _ => (null, 24f),
        };

    private (Texture2D? Texture, float MaxSide) DraftNodeSprite(string nodeClassId) =>
        nodeClassId switch
        {
            "SMALL_SUBSTATION" =>
                (AtomicSubstationTransformerASprite, 128f),
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
        return new Color(0.78f, 0.82f, 0.78f, 0.98f);
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
        if (presentation.OperationsLocked)
        {
            return;
        }
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
        string label = OperationsLocked
            ? $"{ZoomLabel}  ·  읽기 전용  ·  이동/확대 가능"
            : $"{ZoomLabel}  ·  자유 배치  ·  1 = 100단위";
        DrawString(GetThemeDefaultFont(), new Vector2(18f, Size.Y - 13f), label,
            HorizontalAlignment.Left, -1f, 11, Muted);
    }

    private void DrawBuildRail()
    {
        bool locked = OperationsLocked;
        (Texture2D? Texture, string Label, CommercialPanelAction? Action)[] slots =
        [
            (AtomicPlantMainHallASprite, "발전", null),
            (AtomicStandardPoleASprite, "1 선로", CommercialPanelAction.StartLine),
            (AtomicSubstationTransformerASprite,
                "2 변전", CommercialPanelAction.PlaceSubstation),
            (AtomicReinforcedPoleASprite, "3 보강", CommercialPanelAction.CycleLineClass),
        ];
        float height = 18f + (slots.Length * BuildRailSlotHeight) +
            ((slots.Length - 1) * BuildRailGap) + 16f;
        Rect2 outer = new(
            new Vector2(BuildRailLeft, BuildRailTop - 18f),
            new Vector2(BuildRailSlotWidth + 16f, height));
        DrawChromeFrame(
            outer,
            locked ? new Color(0.48f, 0.49f, 0.46f, 0.82f) : Colors.White,
            17f);
        DrawString(
            GetThemeDefaultFont(),
            new Vector2(outer.Position.X + 8f, BuildRailTop - 4f),
            locked ? "공사 잠금 · 읽기 전용" : "건설 도구",
            HorizontalAlignment.Left,
            outer.Size.X - 16f,
            11,
            locked ? Color.FromHtml("a59f91") : Focus);

        for (int index = 0; index < slots.Length; index++)
        {
            float top = BuildRailTop + (index * (BuildRailSlotHeight + BuildRailGap));
            Rect2 slot = new(
                new Vector2(BuildRailLeft + 8f, top),
                new Vector2(BuildRailSlotWidth, BuildRailSlotHeight));
            bool active = !locked && (slots[index].Action switch
            {
                CommercialPanelAction.StartLine =>
                    _presentation?.ToolLabel.Contains("선로", StringComparison.Ordinal) == true,
                CommercialPanelAction.PlaceSubstation =>
                    _presentation?.ToolLabel.Contains("변전", StringComparison.Ordinal) == true,
                CommercialPanelAction.CycleLineClass =>
                    _presentation?.ToolLabel.Contains("보강", StringComparison.Ordinal) == true,
                _ => false,
            });
            Color accent = locked
                ? Color.FromHtml("403f3a")
                : active
                    ? Focus
                    : Color.FromHtml("685b48");
            DrawChromeFrame(
                slot,
                locked
                    ? new Color(0.42f, 0.43f, 0.40f, 0.72f)
                    : active
                        ? new Color(0.96f, 0.78f, 0.43f, 1f)
                        : new Color(0.76f, 0.72f, 0.65f, 1f),
                14f,
                UiToolSlotTexture);
            DrawRect(slot, accent, false, active ? 2.4f : 1.3f);
            if (slots[index].Texture is Texture2D texture)
            {
                Vector2 iconSize = FitSpriteSize(texture, index == 0 ? 104f : 98f);
                Vector2 anchor = slot.GetCenter() + new Vector2(0f, 16f);
                DrawTextureRect(texture, SpriteRect(anchor, iconSize), false,
                    locked
                        ? new Color(0.36f, 0.37f, 0.35f, 0.55f)
                        : index == 0
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
                locked ? Muted : active ? Focus : Text);
        }
    }

    private void DrawChromeFrame(
        Rect2 destination,
        Color modulate,
        float destinationSlice,
        Texture2D? textureOverride = null)
    {
        Texture2D? texture = textureOverride ?? UiChromeFrameTexture;
        if (texture is null)
        {
            DrawRect(destination, new Color(Color.FromHtml("090d0f"), 0.97f));
            DrawRect(destination, Color.FromHtml("755f3e"), false, 2f);
            return;
        }
        float sourceWidth = texture.GetWidth();
        float sourceHeight = texture.GetHeight();
        float sourceSlice = Math.Min(sourceWidth, sourceHeight) * 0.16f;
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
                    texture,
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
        if (!OperationsLocked && action is CommercialPanelAction selected)
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
        string pointer = presentation.OperationsLocked
            ? "공사 포인터 비활성"
            : _pointerPoint is CoreMapPoint
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
        string operations = presentation.OperationsLocked
            ? " 공사 조작 잠금. 지도는 읽기 전용이며 이동, 확대, 전체 보기만 사용할 수 있습니다."
            : " 공사 조작 가능.";
        int reserved = presentation.Snapshot.World.Nodes.Count(item => item.Reserved);
        string reservedText = reserved == 0 ? string.Empty : $" 예정 시설 {reserved}곳.";
        return $"청류시 자유 배치 지도. {presentation.ToolLabel}. {pointer}. 지도 {ZoomLabel}." +
               $"{operations}{reservedText}{thermal}{selectedPath}{facilities}{motion}";
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
        if (OperationsLocked)
        {
            return false;
        }
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

    private static bool IsConstructionKey(InputEventKey key)
    {
        Key physical = key.PhysicalKeycode;
        return physical is Key.Q or Key.E ||
            key.Keycode is Key.Key1 or Key.Kp1 or
                Key.Key2 or Key.Kp2 or
                Key.Key3 or Key.Kp3 or
                Key.Backspace or
                Key.Left or Key.Right or Key.Up or Key.Down or
                Key.Enter or Key.KpEnter;
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
