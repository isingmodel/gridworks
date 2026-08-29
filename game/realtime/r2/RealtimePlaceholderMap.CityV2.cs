using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Gridworks.Core.Release.V2;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

internal sealed partial class RealtimePlaceholderMap
{
    private const string CityV2Root = "res://art/realtime/city-v2/";
    private const string CityResidentialBlock = CityV2Root + "residential-block-a.png";
    private const string CityIndustrialCampus = CityV2Root + "industrial-campus-a.png";

    private enum CityDistrictKind
    {
        Waterworks,
        NorthResidential,
        EastResidential,
        Hospital,
        Industrial,
    }

    private readonly record struct CityDistrict(
        string NodeId,
        string DisplayId,
        CityDistrictKind Kind,
        CoreMapPoint Center,
        CoreMapPoint SpriteGround,
        Vector2 WorldFootprint,
        float WorldMaxSide,
        string? CampusAssetPath = null);

    private static readonly CityDistrict[] CityDistricts =
    [
        new(
            "WATER_TERMINAL",
            "waterworks",
            CityDistrictKind.Waterworks,
            new CoreMapPoint(2550, 220),
            new CoreMapPoint(2550, 360),
            new Vector2(500f, 360f),
            0f),
        new(
            "NORTH_RESIDENTIAL_TERMINAL",
            "north_residential",
            CityDistrictKind.NorthResidential,
            new CoreMapPoint(3000, 225),
            new CoreMapPoint(3000, 380),
            new Vector2(330f, 330f),
            0f),
        new(
            "EAST_RESIDENTIAL_TERMINAL",
            "east_residential",
            CityDistrictKind.EastResidential,
            new CoreMapPoint(2910, 725),
            new CoreMapPoint(2910, 960),
            new Vector2(560f, 450f),
            720f,
            CityResidentialBlock),
        new(
            "HOSPITAL_TERMINAL",
            "hospital",
            CityDistrictKind.Hospital,
            new CoreMapPoint(2875, 1350),
            new CoreMapPoint(2875, 1530),
            new Vector2(550f, 400f),
            0f),
        new(
            "FACTORY_TERMINAL",
            "industrial",
            CityDistrictKind.Industrial,
            new CoreMapPoint(2875, 1800),
            new CoreMapPoint(2875, 1980),
            new Vector2(560f, 330f),
            740f,
            CityIndustrialCampus),
    ];

    private static readonly CoreMapPoint[][] CityRoadPaths =
    [
        [new(2015, 150), new(2060, 480), new(2040, 820), new(2090, 1180),
            new(2070, 1480), new(2150, 1850)],
        [new(2050, 330), new(2200, 320), new(2320, 270)],
        [new(2070, 480), new(2320, 420), new(2550, 330), new(2780, 245)],
        [new(2045, 820), new(2260, 785), new(2460, 745), new(2605, 725)],
        [new(2080, 1180), new(2260, 1210), new(2440, 1280), new(2590, 1340)],
        [new(2100, 1650), new(2280, 1690), new(2440, 1760), new(2585, 1800)],
    ];

    private CityDistrict? DistrictForNode(string nodeId)
    {
        foreach (CityDistrict district in CityDistricts)
        {
            if (string.Equals(district.NodeId, nodeId, StringComparison.Ordinal))
            {
                return district;
            }
        }
        return null;
    }

    private void DrawCityRoadNetwork()
    {
        DrawCityGroundPlane();
        Color shoulder = new(Color.FromHtml("202725"), 0.96f);
        Color asphalt = new(Color.FromHtml("303634"), 0.98f);
        Color lane = new(Color.FromHtml("a88b55"), 0.42f);
        foreach (CoreMapPoint[] path in CityRoadPaths)
        {
            Vector2[] points = SmoothRoadPath(path.Select(Point).ToArray());
            DrawPolyline(points, shoulder, WorldPixels(58f), true);
            DrawPolyline(points, asphalt, WorldPixels(42f), true);
            DrawPolyline(points, lane, Math.Max(1f, 1.2f * _accessibilityScale), true);
#if DEBUG
            _drawnCityRoadPathCount++;
#endif
        }
    }

    private void DrawCityGroundPlane()
    {
        CoreMapPoint[] boundary =
        [
            new(1870, 40), new(2520, 10), new(3180, 95), new(3235, 560),
            new(3165, 1050), new(3240, 1540), new(3130, 2050), new(2380, 2090),
            new(1885, 1930), new(1915, 1320), new(1825, 680),
        ];
        Vector2[] polygon = boundary.Select(Point).ToArray();
        DrawColoredPolygon(polygon, new Color(Color.FromHtml("1b2421"), 0.32f));
        DrawPolyline(
            [.. polygon, polygon[0]],
            new Color(Color.FromHtml("718078"), 0.07f),
            1.4f * _accessibilityScale,
            true);

        // Two subdued green belts break the corridor into neighborhoods without
        // reintroducing a repeated tile grid behind the authored campuses.
        foreach ((CoreMapPoint centerPoint, float width, float depth) in new[]
                 {
                     (new CoreMapPoint(2440, 545), 440f, 135f),
                     (new CoreMapPoint(2350, 1510), 390f, 120f),
                 })
        {
            Vector2 center = Point(centerPoint);
            Vector2[] belt = DistrictDiamond(
                center,
                WorldPixels(width) * 0.5f,
                WorldPixels(depth) * 0.28f);
            DrawColoredPolygon(belt, new Color(Color.FromHtml("26332b"), 0.62f));
        }
    }

    private static Vector2[] SmoothRoadPath(Vector2[] controlPoints)
    {
        if (controlPoints.Length < 3)
        {
            return controlPoints;
        }
        const int subdivisions = 8;
        var result = new List<Vector2>((controlPoints.Length - 1) * subdivisions + 1);
        for (int segment = 0; segment < controlPoints.Length - 1; segment++)
        {
            Vector2 p0 = controlPoints[Math.Max(0, segment - 1)];
            Vector2 p1 = controlPoints[segment];
            Vector2 p2 = controlPoints[segment + 1];
            Vector2 p3 = controlPoints[Math.Min(controlPoints.Length - 1, segment + 2)];
            for (int step = 0; step < subdivisions; step++)
            {
                float t = step / (float)subdivisions;
                float t2 = t * t;
                float t3 = t2 * t;
                result.Add(0.5f * ((2f * p1) + ((-p0 + p2) * t) +
                    ((2f * p0 - 5f * p1 + 4f * p2 - p3) * t2) +
                    ((-p0 + 3f * p1 - 3f * p2 + p3) * t3)));
            }
        }
        result.Add(controlPoints[^1]);
        return result.ToArray();
    }

    private void DrawCityDistricts(RealtimeWorldPresentation presentation)
    {
        foreach (CityDistrict district in CityDistricts
            .OrderBy(item => Point(item.Center).Y)
            .ThenBy(item => Point(item.Center).X)
            .ThenBy(item => item.NodeId, StringComparer.Ordinal))
        {
            RealtimeWorldAssetStatus? status = Status(presentation, district.NodeId);
            Color stateModulate = G3NodeModulate(status?.State);
            DrawDistrictFoundation(district, stateModulate);
            switch (district.Kind)
            {
                case CityDistrictKind.Waterworks:
                    DrawWaterworksDistrict(district, stateModulate);
                    break;
                case CityDistrictKind.NorthResidential:
                    DrawNorthResidentialDistrict(district, stateModulate);
                    break;
                case CityDistrictKind.EastResidential:
                case CityDistrictKind.Industrial:
                    DrawDistrictCampusSprite(district, stateModulate);
                    break;
                case CityDistrictKind.Hospital:
                    DrawHospitalDistrict(district, stateModulate);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(district));
            }
#if DEBUG
            _drawnCityDistrictIds.Add(district.DisplayId);
#endif
        }
    }

    private void DrawDistrictFoundation(CityDistrict district, Color stateModulate)
    {
        Vector2 center = Point(district.Center);
        float halfWidth = WorldPixels(district.WorldFootprint.X) * 0.5f;
        float halfDepth = WorldPixels(district.WorldFootprint.Y) * 0.28f;
        Vector2[] diamond = DistrictDiamond(center, halfWidth, halfDepth);
        Color baseColor = district.Kind switch
        {
            CityDistrictKind.Waterworks => Color.FromHtml("253334"),
            CityDistrictKind.NorthResidential or CityDistrictKind.EastResidential =>
                Color.FromHtml("34312a"),
            CityDistrictKind.Hospital => Color.FromHtml("343737"),
            CityDistrictKind.Industrial => Color.FromHtml("2f302e"),
            _ => Color.FromHtml("303330"),
        };
        DrawColoredPolygon(
            diamond.Select(point => point + new Vector2(0f, 5f * _accessibilityScale))
                .ToArray(),
            new Color(0f, 0f, 0f, 0.38f));
        DrawColoredPolygon(diamond, new Color(baseColor, 0.88f));
        DrawPolyline(
            [.. diamond, diamond[0]],
            stateModulate with { A = 0.46f },
            2f * _accessibilityScale,
            true);

        Vector2 leftMid = diamond[0].Lerp(diamond[3], 0.5f);
        Vector2 rightMid = diamond[1].Lerp(diamond[2], 0.5f);
        DrawLine(leftMid, rightMid, new Color(Color.FromHtml("74746b"), 0.18f),
            _accessibilityScale, true);
    }

    private static Vector2[] DistrictDiamond(Vector2 center, float halfWidth, float halfDepth) =>
    [
        center + new Vector2(-halfWidth, 0f),
        center + new Vector2(0f, -halfDepth),
        center + new Vector2(halfWidth, 0f),
        center + new Vector2(0f, halfDepth),
    ];

    private void DrawDistrictCampusSprite(CityDistrict district, Color modulate)
    {
        if (district.CampusAssetPath is not string assetPath ||
            G3Texture(assetPath) is not Texture2D texture)
        {
            return;
        }
        Vector2 spriteSize = FitG3SpriteSize(texture, WorldPixels(district.WorldMaxSide));
        Vector2 ground = Point(district.SpriteGround);
        DrawTextureRect(
            texture,
            new Rect2(
                ground - new Vector2(spriteSize.X * 0.5f, spriteSize.Y * 0.93f),
                spriteSize),
            false,
            modulate with { A = 0.97f });
        RecordG3Asset(assetPath);
#if DEBUG
        _drawnG3SpriteCount++;
#endif
    }

    private void DrawNorthResidentialDistrict(CityDistrict district, Color modulate)
    {
        DrawG3Sprite(
            G3WorkerHouseB,
            Point(new CoreMapPoint(district.Center.XUnit - 92, district.Center.YUnit + 32)),
            WorldPixels(170f),
            modulate);
        DrawG3Sprite(
            G3WorkerHouseA,
            Point(new CoreMapPoint(district.Center.XUnit + 70, district.Center.YUnit - 32)),
            WorldPixels(150f),
            modulate);
        DrawG3Sprite(
            G3RowShop,
            Point(new CoreMapPoint(district.Center.XUnit + 78, district.Center.YUnit + 92)),
            WorldPixels(165f),
            modulate);
        DrawDistrictPracticalLights(district, 3);
    }

    private void DrawHospitalDistrict(CityDistrict district, Color modulate)
    {
        DrawG3Sprite(
            G3HospitalService,
            Point(new CoreMapPoint(district.Center.XUnit + 150, district.Center.YUnit + 75)),
            WorldPixels(235f),
            modulate with { A = 0.94f });
        DrawG3Sprite(
            G3HospitalMain,
            Point(new CoreMapPoint(district.Center.XUnit - 25, district.Center.YUnit + 92)),
            WorldPixels(430f),
            modulate);
        Vector2 medicalMark = Point(new CoreMapPoint(
            district.Center.XUnit - 185,
            district.Center.YUnit - 35));
        Color medical = new(Color.FromHtml("8bcac5"), 0.86f);
        DrawCircle(medicalMark, 13f * _accessibilityScale,
            new Color(Color.FromHtml("172624"), 0.94f));
        DrawLine(
            medicalMark + new Vector2(-7f, 0f) * _accessibilityScale,
            medicalMark + new Vector2(7f, 0f) * _accessibilityScale,
            medical,
            3f * _accessibilityScale,
            true);
        DrawLine(
            medicalMark + new Vector2(0f, -7f) * _accessibilityScale,
            medicalMark + new Vector2(0f, 7f) * _accessibilityScale,
            medical,
            3f * _accessibilityScale,
            true);
        DrawDistrictPracticalLights(district, 4);
    }

    private void DrawWaterworksDistrict(CityDistrict district, Color modulate)
    {
        DrawG3Sprite(
            G3WaterTank,
            Point(new CoreMapPoint(district.Center.XUnit + 145, district.Center.YUnit - 30)),
            WorldPixels(235f),
            modulate with { A = 0.94f });
        DrawG3Sprite(
            G3PumpHouse,
            Point(new CoreMapPoint(district.Center.XUnit - 72, district.Center.YUnit + 72)),
            WorldPixels(315f),
            modulate);
        DrawLine(
            Point(new CoreMapPoint(district.Center.XUnit + 20, district.Center.YUnit + 55)),
            Point(new CoreMapPoint(district.Center.XUnit + 145, district.Center.YUnit + 10)),
            new Color(Color.FromHtml("559a9c"), 0.74f),
            Math.Max(2f, 4f * _accessibilityScale),
            true);
        DrawDistrictPracticalLights(district, 3);
    }

    private void DrawDistrictPracticalLights(CityDistrict district, int count)
    {
        Vector2 center = Point(district.Center);
        float halfWidth = WorldPixels(district.WorldFootprint.X) * 0.34f;
        float halfDepth = WorldPixels(district.WorldFootprint.Y) * 0.17f;
        for (int index = 0; index < count; index++)
        {
            float t = count == 1 ? 0.5f : index / (float)(count - 1);
            Vector2 point = center + new Vector2(
                Mathf.Lerp(-halfWidth, halfWidth, t),
                halfDepth + MathF.Sin(index * 1.7f) * 3f * _accessibilityScale);
            DrawCircle(point, 3.6f * _accessibilityScale,
                new Color(Color.FromHtml("e5b866"), 0.15f));
            DrawCircle(point, 1.4f * _accessibilityScale,
                new Color(Color.FromHtml("f0ca7a"), 0.74f));
        }
    }

    private bool TryGetDistrictVisual(
        SpatialNodeDefinition node,
        out CityDistrict district,
        out Vector2 center,
        out Vector2 halfExtents)
    {
        CityDistrict? match = DistrictForNode(node.NodeId);
        if (!match.HasValue)
        {
            district = default;
            center = default;
            halfExtents = default;
            return false;
        }
        district = match.Value;
        center = Point(district.Center);
        halfExtents = new Vector2(
            WorldPixels(district.WorldFootprint.X) * 0.48f,
            WorldPixels(district.WorldFootprint.Y) * 0.30f);
        return true;
    }

    private bool DistrictContainsPoint(
        SpatialNodeDefinition node,
        Vector2 canvasPoint,
        out double distanceSquared)
    {
        if (!TryGetDistrictVisual(node, out _, out Vector2 center, out Vector2 halfExtents))
        {
            distanceSquared = double.PositiveInfinity;
            return false;
        }
        Vector2 delta = canvasPoint - center;
        float normalized = Math.Abs(delta.X) / Math.Max(1f, halfExtents.X) +
            Math.Abs(delta.Y) / Math.Max(1f, halfExtents.Y);
        distanceSquared = center.DistanceSquaredTo(canvasPoint);
        return normalized <= 1f;
    }

    private bool DrawDistrictNodeOverlay(
        RealtimeWorldPresentation presentation,
        SpatialNodeDefinition node,
        RealtimeWorldAssetStatus? status,
        bool selected,
        bool routeHighlighted)
    {
        if (!TryGetDistrictVisual(
                node,
                out _,
                out Vector2 center,
                out Vector2 halfExtents))
        {
            return false;
        }

        Color color = node.Commissioned ? StateColor(status?.State) : Planned;
        Vector2 terminal = Point(node.Position);
        DrawLine(
            terminal,
            center,
            color with { A = selected || routeHighlighted ? 0.74f : 0.34f },
            (selected || routeHighlighted ? 2.4f : 1.3f) * _accessibilityScale,
            true);
        DrawCircle(terminal, 5f * _accessibilityScale, Ground with { A = 0.92f });
        DrawCircle(
            terminal,
            5f * _accessibilityScale,
            color,
            false,
            1.8f * _accessibilityScale,
            true);

        Vector2[] footprint = DistrictDiamond(center, halfExtents.X, halfExtents.Y);
        if (selected || routeHighlighted)
        {
            Color outline = selected || routeHighlighted ? Selected : color;
            DrawColoredPolygon(footprint, outline with { A = 0.08f });
            DrawPolyline(
                [.. footprint, footprint[0]],
                outline,
                3f * _accessibilityScale,
                true);
        }
        DrawNodeStateCue(
            node.NodeId,
            center,
            Math.Max(9f * _accessibilityScale, halfExtents.Y * 0.38f),
            status?.State);
        if (selected)
        {
            DrawActiveCandidateBadge(
                center + new Vector2(-halfExtents.X * 0.72f, -halfExtents.Y - 52f),
                $"{node.DisplayName} · {StatusLabel(status)}");
        }
        return true;
    }

    private bool DrawDistrictCandidateOutline(
        SpatialNodeDefinition node,
        Color color,
        out Vector2 badgeAnchor)
    {
        if (!TryGetDistrictVisual(
                node,
                out _,
                out Vector2 center,
                out Vector2 halfExtents))
        {
            badgeAnchor = default;
            return false;
        }
        Vector2[] footprint = DistrictDiamond(center, halfExtents.X, halfExtents.Y);
        DrawPolyline(
            [.. footprint, footprint[0]],
            color,
            3f * _accessibilityScale,
            true);
        badgeAnchor = center + new Vector2(halfExtents.X * 0.55f, -halfExtents.Y);
        return true;
    }
}
