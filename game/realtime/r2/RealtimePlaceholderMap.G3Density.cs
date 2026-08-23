using System;
using System.Collections.Generic;
using Godot;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

/// <summary>
/// Full-density presentation recipe recovered from the accepted G3 source tree.
/// These placements are deliberately draw-only: the Release.V3 world remains the
/// sole authority for terrain, construction, hit testing, and simulation state.
/// </summary>
internal sealed partial class RealtimePlaceholderMap
{
    private const string G3GroundRubbleMixB = G3Root + "tiles/ground-rubble-mix-b.png";
    private const string G3IndustrialRoadBridgeA =
        G3Root + "objects/industrial-road-bridge-a.png";
    private const string G3IndustrialRoadBridgeB =
        G3Root + "objects/industrial-road-bridge-b.png";
    private const string G3RiverBankRockSegment =
        G3Root + "objects/river-bank-rock-segment-a.png";
    private const string G3RiverBankInnerBend =
        G3Root + "objects/river-bank-inner-bend-a.png";
    private const string G3RiverBankOuterBend =
        G3Root + "objects/river-bank-outer-bend-a.png";
    private const string G3RiverCurrentReflection =
        G3Root + "objects/river-current-reflection-a.png";
    private const string G3RiverBankLeftStraight =
        G3Root + "river/river-bank-left-straight-a.png";
    private const string G3RiverBankRightStraight =
        G3Root + "river/river-bank-right-straight-a.png";
    private const string G3RiverBankLeftInner =
        G3Root + "river/river-bank-left-inner-a.png";
    private const string G3RiverBankLeftOuter =
        G3Root + "river/river-bank-left-outer-a.png";
    private const string G3RiverBankRightInner =
        G3Root + "river/river-bank-right-inner-a.png";
    private const string G3RiverBankRightOuter =
        G3Root + "river/river-bank-right-outer-a.png";
    private const string G3RiverFloodRipple =
        G3Root + "river/river-flood-ripple-a.png";
    private const string G3RiverRockSoilTransition =
        G3Root + "river/river-rock-soil-transition-a.png";

    private static string[] G3ExtendedMapAssetPaths =>
    [
        G3GroundRubbleMixB,
        G3IndustrialRoadBridgeA, G3IndustrialRoadBridgeB,
        G3RiverBankRockSegment, G3RiverBankInnerBend, G3RiverBankOuterBend,
        G3RiverCurrentReflection,
        G3RiverBankLeftStraight, G3RiverBankRightStraight,
        G3RiverBankLeftInner, G3RiverBankLeftOuter,
        G3RiverBankRightInner, G3RiverBankRightOuter,
        G3RiverFloodRipple, G3RiverRockSoilTransition,
    ];

    private static readonly G3Placement[] G3DenseRoadPlacements = BuildDenseRoadPlacements();
    private static readonly G3Placement[] G3DenseCityPlacements = BuildDenseCityPlacements();
    private static readonly G3Placement[] G3FullRiverPlacements = BuildFullRiverPlacements();

    private void DrawG3GroundVariation()
    {
        if (G3Texture(G3GroundRubbleMixB) is not Texture2D rubble)
        {
            return;
        }
        DrawTextureRectRegion(
            rubble,
            new Rect2(Vector2.Zero, Size),
            new Rect2(new Vector2(168f, 91f), Size * 1.30f),
            new Color(0.72f, 0.71f, 0.65f, 0.18f));
        RecordG3Asset(G3GroundRubbleMixB);
    }

    private void DrawG3FullRiverDetails(RealtimeWorldPresentation presentation)
    {
        DrawG3Placements(
            G3FullRiverPlacements,
            new Color(0.82f, 0.78f, 0.69f, 1f));
        DrawG3Sprite(
            G3RiverCurrentReflection,
            Point(new CoreMapPoint(1390, 1030)),
            WorldPixels(780f),
            new Color(0.72f, 0.86f, 0.88f, presentation.Weather == RealtimeWorldWeather.Heat
                ? 0.30f
                : 0.46f));
        if (presentation.Weather is RealtimeWorldWeather.Rain or RealtimeWorldWeather.Storm)
        {
            float alpha = presentation.Weather == RealtimeWorldWeather.Storm ? 0.72f : 0.50f;
            DrawG3Sprite(
                G3RiverFloodRipple,
                Point(new CoreMapPoint(1390, 1020)),
                WorldPixels(690f),
                new Color(0.70f, 0.83f, 0.90f, alpha));
            DrawG3Sprite(
                G3RiverFloodRipple,
                Point(new CoreMapPoint(1510, 1510)),
                WorldPixels(410f),
                new Color(0.70f, 0.83f, 0.90f, alpha * 0.72f));
        }
    }

    private void DrawG3DenseRoads() => DrawG3Placements(
        G3DenseRoadPlacements,
        new Color(0.87f, 0.82f, 0.72f, 1f));

    private void DrawG3DenseCity() => DrawG3Placements(
        G3DenseCityPlacements,
        new Color(0.92f, 0.88f, 0.78f, 1f));

    private static G3Placement[] BuildDenseRoadPlacements()
    {
        var placements = new List<G3Placement>();

        for (int x = 2700; x <= 3100; x += 80)
        {
            placements.Add(new G3Placement(
                G3RoadNorthWestSouthEast, new CoreMapPoint(x, 650), 138f, 0.82f));
            placements.Add(new G3Placement(
                G3RoadNorthWestSouthEast, new CoreMapPoint(x, 850), 138f, 0.82f));
        }
        for (int x = 2740; x <= 3060; x += 80)
        {
            placements.Add(new G3Placement(
                G3RoadNorthEastSouthWest, new CoreMapPoint(x, 750), 138f, 0.82f));
        }
        placements.Add(new G3Placement(G3RoadCrossJunction, new CoreMapPoint(2860, 750), 144f));
        placements.Add(new G3Placement(G3RoadCrossJunction, new CoreMapPoint(3020, 750), 144f));
        placements.Add(new G3Placement(G3RoadCornerNorthEast, new CoreMapPoint(2700, 1000), 142f));
        placements.Add(new G3Placement(G3RoadCornerNorthEast, new CoreMapPoint(3100, 1000), 142f));

        for (int x = 2440; x <= 3040; x += 150)
        {
            placements.Add(new G3Placement(
                G3RoadNorthWestSouthEast, new CoreMapPoint(x, 1500), 152f, 0.78f));
        }
        placements.Add(new G3Placement(G3RoadTJunction, new CoreMapPoint(2540, 1360), 156f));
        placements.Add(new G3Placement(G3RoadTJunction, new CoreMapPoint(2860, 1640), 156f));
        placements.Add(new G3Placement(G3ServiceYard, new CoreMapPoint(1960, 1800), 182f));
        placements.Add(new G3Placement(G3ServiceYard, new CoreMapPoint(1590, 1900), 182f));
        return placements.ToArray();
    }

    private static G3Placement[] BuildDenseCityPlacements()
    {
        var placements = new List<G3Placement>();
        string[] homes = [G3WorkerHouseA, G3WorkerHouseB, G3WorkerHouseC, G3RowShop];
        for (int row = 0; row < 5; row++)
        {
            for (int column = 0; column < 6; column++)
            {
                string home = homes[(row * 3 + column) % homes.Length];
                placements.Add(new G3Placement(
                    home,
                    new CoreMapPoint(2700 + column * 80, 600 + row * 90),
                    68f + ((row + column) % 3) * 3f,
                    0.96f));
            }
        }

        placements.Add(new G3Placement(G3HospitalMain, new CoreMapPoint(2540, 1390), 190f));
        placements.Add(new G3Placement(G3HospitalService, new CoreMapPoint(2750, 1380), 100f));
        placements.Add(new G3Placement(G3HospitalService, new CoreMapPoint(2910, 1420), 96f));
        placements.Add(new G3Placement(G3HospitalService, new CoreMapPoint(2750, 1580), 98f));
        placements.Add(new G3Placement(G3WaterTank, new CoreMapPoint(3030, 1530), 130f));
        placements.Add(new G3Placement(G3PumpHouse, new CoreMapPoint(2450, 1600), 96f));

        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                string asset = (row + column) % 2 == 0 ? G3Workshop : G3SmallWarehouse;
                placements.Add(new G3Placement(
                    asset,
                    new CoreMapPoint(150 + column * 145, 1390 + row * 145),
                    (row + column) % 2 == 0 ? 62f : 68f,
                    0.91f));
            }
        }
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 6; column++)
            {
                string asset = (row + column) % 2 == 0 ? G3Workshop : G3SmallWarehouse;
                placements.Add(new G3Placement(
                    asset,
                    new CoreMapPoint(1160 + column * 165, 150 + row * 190),
                    (row + column) % 2 == 0 ? 72f : 78f,
                    0.90f));
            }
        }
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                string home = homes[(row + column * 2) % homes.Length];
                placements.Add(new G3Placement(
                    home,
                    new CoreMapPoint(2740 + column * 120, 150 + row * 120),
                    66f + ((row + column) % 2) * 4f,
                    0.94f));
            }
        }
        for (int x = 180; x <= 3060; x += 280)
        {
            placements.Add(new G3Placement(G3StreetLamp, new CoreMapPoint(x, 220), 52f, 0.78f));
            placements.Add(new G3Placement(G3StreetLamp, new CoreMapPoint(x, 1880), 52f, 0.72f));
        }
        return placements.ToArray();
    }

    private static G3Placement[] BuildFullRiverPlacements() =>
    [
        new(G3RiverBankRockSegment, new CoreMapPoint(1030, 220), 300f, 0.82f),
        new(G3RiverBankLeftStraight, new CoreMapPoint(1060, 560), 310f, 0.88f),
        new(G3RiverBankLeftInner, new CoreMapPoint(1110, 930), 300f, 0.88f),
        new(G3RiverBankLeftOuter, new CoreMapPoint(1190, 1310), 300f, 0.88f),
        new(G3RiverBankRockSegment, new CoreMapPoint(1260, 1710), 300f, 0.82f),
        new(G3RiverBankRightStraight, new CoreMapPoint(1450, 230), 310f, 0.88f),
        new(G3RiverBankRightInner, new CoreMapPoint(1520, 610), 300f, 0.88f),
        new(G3RiverBankRightOuter, new CoreMapPoint(1600, 1010), 300f, 0.88f),
        new(G3RiverBankOuterBend, new CoreMapPoint(1660, 1430), 330f, 0.88f),
        new(G3RiverBankInnerBend, new CoreMapPoint(1730, 1810), 300f, 0.86f),
        new(G3RiverRockSoilTransition, new CoreMapPoint(920, 820), 250f, 0.78f),
        new(G3RiverRockSoilTransition, new CoreMapPoint(1840, 1180), 250f, 0.78f),
        new(G3IndustrialRoadBridgeA, new CoreMapPoint(1300, 500), 390f, 0.86f),
        new(G3IndustrialRoadBridgeB, new CoreMapPoint(1450, 1500), 430f, 0.96f),
    ];
}
