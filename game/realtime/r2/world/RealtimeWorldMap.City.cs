using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Gridworks.Core.Release.V2;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.R2;

internal sealed partial class RealtimeWorldMap
{
    private const string CityV2Root = "res://art/realtime/city-v2/";
    private const string CityResidentialBlock = CityV2Root + "residential-block-a.png";
    private const string CityIndustrialCampus = CityV2Root + "industrial-campus-a.png";
    private const string CityHospitalCampus = CityV2Root + "hospital-campus-a.png";
    private const string CityWaterworksCampus = CityV2Root + "waterworks-campus-a.png";
    private const string CityWestPowerCampus =
        CityV2Root + "power-plant-west-campus-b.png";
    private const string CitySouthPowerCampus =
        CityV2Root + "power-plant-south-campus-b.png";

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
        string Label,
        CityDistrictKind Kind,
        CoreMapPoint Center,
        CoreMapPoint SpriteGround,
        Vector2 WorldFootprint,
        float WorldMaxSide,
        string? CampusAssetPath = null);

    private readonly record struct CityDistrictTemplate(
        string NodeId,
        string DisplayId,
        string Label,
        CityDistrictKind Kind,
        string CampusAssetPath);

    private static readonly CityDistrictTemplate[] CityDistrictTemplates =
    [
        new(
            "WATER_TERMINAL",
            "waterworks",
            "정수장",
            CityDistrictKind.Waterworks,
            CityWaterworksCampus),
        new(
            "NORTH_RESIDENTIAL_TERMINAL",
            "north_residential",
            "북안 생활권",
            CityDistrictKind.NorthResidential,
            CityResidentialBlock),
        new(
            "EAST_RESIDENTIAL_TERMINAL",
            "east_residential",
            "동부 생활권",
            CityDistrictKind.EastResidential,
            CityResidentialBlock),
        new(
            "HOSPITAL_TERMINAL",
            "hospital",
            "청류의료원",
            CityDistrictKind.Hospital,
            CityHospitalCampus),
        new(
            "FACTORY_TERMINAL",
            "industrial",
            "강변 산업단지",
            CityDistrictKind.Industrial,
            CityIndustrialCampus),
    ];

    private IEnumerable<CityDistrict> CityDistricts => CityDistrictTemplates.Select(
        template =>
        {
            RealtimeVisualDistrictLayout layout = _visualLayout.Districts.Single(item =>
                string.Equals(item.NodeId, template.NodeId, StringComparison.Ordinal));
            return new CityDistrict(
                template.NodeId,
                template.DisplayId,
                template.Label,
                template.Kind,
                new CoreMapPoint(layout.Center.X, layout.Center.Y),
                new CoreMapPoint(layout.SpriteGround.X, layout.SpriteGround.Y),
                new Vector2(layout.Footprint.X, layout.Footprint.Y),
                layout.WorldMaxSide,
                template.CampusAssetPath);
        });

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
        foreach (RealtimeVisualRoadLayout road in _visualLayout.Roads)
        {
            CoreMapPoint[] path = road.Points
                .Select(point => new CoreMapPoint(point.X, point.Y))
                .ToArray();
            Vector2[] points = SmoothRoadPath(path.Select(Point).ToArray());
            bool sourceService = road.Style == "source_service";
            bool citySpine = road.Style.StartsWith("city_spine_",
                StringComparison.Ordinal);
            bool primarySpine = road.Style == "city_spine_primary";
            bool industrialAccess = road.Style == "industrial_access";
            Color shoulder = sourceService
                ? new Color(Color.FromHtml("252723"), 0.90f)
                : citySpine
                    ? new Color(Color.FromHtml("171e1d"), 0.98f)
                    : new Color(Color.FromHtml("202624"), 0.88f);
            Color surface = sourceService
                ? new Color(Color.FromHtml("46443b"), 0.90f)
                : industrialAccess
                    ? new Color(Color.FromHtml("514d41"), 0.92f)
                    : citySpine
                        ? new Color(Color.FromHtml("424a48"), 0.98f)
                        : new Color(Color.FromHtml("323937"), 0.92f);
            Color lane = industrialAccess
                ? new Color(Color.FromHtml("d3af64"), 0.62f)
                : citySpine
                    ? new Color(Color.FromHtml("d1ad69"), 0.68f)
                    : new Color(Color.FromHtml("9a8d68"), 0.24f);
            float shoulderWidth = road.Style switch
            {
                "source_service" => 42f,
                "city_spine_primary" => 68f,
                "city_spine_secondary" => 58f,
                "industrial_access" => 42f,
                _ => 34f,
            };
            float roadWidth = road.Style switch
            {
                "source_service" => 30f,
                "city_spine_primary" => 50f,
                "city_spine_secondary" => 42f,
                "industrial_access" => 32f,
                _ => 22f,
            };
            DrawPolyline(points, shoulder, WorldPixels(shoulderWidth), true);
            DrawPolyline(points, surface, WorldPixels(roadWidth), true);
            if (!sourceService)
            {
                DrawPolyline(points, lane,
                    Math.Max(0.8f, (primarySpine ? 1.45f : 0.9f) *
                        _accessibilityScale),
                    true);
            }
#if DEBUG
            _drawnCityRoadPathCount++;
#endif
        }
        DrawCityCivicInfill();
        DrawCityLowContrastInfill();
    }

    private void DrawCityCivicInfill()
    {
        // Small parking/service courts bridge the visual gap between the large
        // authored campuses without competing with their silhouettes.
        foreach ((CoreMapPoint centerPoint, float width, float depth) in new[]
                 {
                     (new CoreMapPoint(2310, 760), 250f, 125f),
                     (new CoreMapPoint(2710, 575), 230f, 112f),
                     (new CoreMapPoint(2510, 1080), 240f, 110f),
                     (new CoreMapPoint(2680, 1210), 300f, 135f),
                     (new CoreMapPoint(2550, 1570), 260f, 120f),
                     (new CoreMapPoint(2820, 1510), 220f, 105f),
                 })
        {
            Vector2 center = Point(centerPoint);
            float halfWidth = WorldPixels(width) * 0.5f;
            float halfDepth = WorldPixels(depth) * 0.28f;
            Vector2[] court = DistrictDiamond(center, halfWidth, halfDepth);
            DrawColoredPolygon(court, new Color(Color.FromHtml("303735"), 0.66f));
            DrawPolyline([.. court, court[0]],
                new Color(Color.FromHtml("777c73"), 0.16f),
                _accessibilityScale,
                true);
            for (int stall = -2; stall <= 2; stall++)
            {
                float t = 0.5f + stall * 0.11f;
                Vector2 near = court[0].Lerp(court[3], t);
                Vector2 far = court[1].Lerp(court[2], t);
                DrawLine(near.Lerp(far, 0.18f), near.Lerp(far, 0.34f),
                    new Color(Color.FromHtml("b5a777"), 0.28f),
                    Math.Max(0.8f, _accessibilityScale),
                    true);
            }
        }
    }

    private void DrawCityLowContrastInfill()
    {
        // These small masses sit between the authored gameplay campuses. They
        // establish block grain without impersonating selectable facilities.
        foreach ((string asset, CoreMapPoint ground, float maxSide, float alpha) in new[]
                 {
                     (G3RowShop, new CoreMapPoint(2650, 610), 210f, 0.52f),
                     (G3WorkerHouseA, new CoreMapPoint(2810, 620), 180f, 0.48f),
                     (G3RowShop, new CoreMapPoint(2500, 1050), 200f, 0.50f),
                     (G3Workshop, new CoreMapPoint(2910, 1120), 185f, 0.48f),
                     (G3WorkerHouseA, new CoreMapPoint(2660, 1110), 170f, 0.44f),
                     (G3SmallWarehouse, new CoreMapPoint(2700, 1540), 220f, 0.48f),
                     (G3Workshop, new CoreMapPoint(2940, 1510), 190f, 0.46f),
                 })
        {
            DrawG3Sprite(
                asset,
                Point(ground),
                WorldPixels(maxSide),
                new Color(0.72f, 0.70f, 0.63f, alpha));
        }

        foreach (CoreMapPoint light in new[]
                 {
                     new CoreMapPoint(2570, 660), new CoreMapPoint(2760, 650),
                     new CoreMapPoint(2450, 1110), new CoreMapPoint(2810, 1130),
                     new CoreMapPoint(2600, 1510), new CoreMapPoint(2860, 1540),
                 })
        {
            DrawG3Sprite(
                G3StreetLamp,
                Point(light),
                WorldPixels(54f),
                new Color(0.78f, 0.73f, 0.58f, 0.48f));
        }
    }

    private void DrawCityGroundPlane()
    {
        CoreMapPoint[][] wards =
        [
            [new(1880, 80), new(2510, 20), new(3180, 100), new(3170, 620),
                new(2630, 710), new(2040, 620)],
            [new(2040, 650), new(3170, 610), new(3150, 1180), new(2670, 1390),
                new(2110, 1250)],
            [new(2060, 1270), new(2710, 1330), new(3170, 1450), new(3110, 2020),
                new(2380, 2050), new(1900, 1880)],
        ];
        foreach (CoreMapPoint[] ward in wards)
        {
            Vector2[] polygon = ward.Select(Point).ToArray();
            DrawColoredPolygon(polygon, new Color(Color.FromHtml("1b2421"), 0.20f));
        }

        // The two generation campuses belong to the same municipal landscape as
        // the east-bank districts. Low-contrast reserve parcels and service roads
        // prevent their authored foundations from reading as loose stickers on an
        // otherwise empty ground texture.
        CoreMapPoint[][] sourceReserves =
        [
            [new(0, 410), new(430, 340), new(910, 540), new(780, 900),
                new(260, 980), new(0, 820)],
            [new(0, 1390), new(430, 1330), new(930, 1530), new(790, 1910),
                new(280, 2030), new(0, 1860)],
        ];
        foreach (CoreMapPoint[] reserve in sourceReserves)
        {
            Vector2[] polygon = reserve.Select(Point).ToArray();
            DrawColoredPolygon(polygon, new Color(Color.FromHtml("272b28"), 0.18f));
        }

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
                case CityDistrictKind.NorthResidential:
                case CityDistrictKind.EastResidential:
                case CityDistrictKind.Waterworks:
                case CityDistrictKind.Hospital:
                case CityDistrictKind.Industrial:
                    DrawDistrictCampusSprite(district, stateModulate);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(district));
            }
            DrawDistrictIdentity(district, stateModulate);
#if DEBUG
            _drawnCityDistrictIds.Add(district.DisplayId);
#endif
        }
    }

    private void DrawDistrictIdentity(CityDistrict district, Color stateModulate)
    {
        Vector2 center = Point(district.Center);
        float halfWidth = WorldPixels(district.WorldFootprint.X) * 0.5f;
        Vector2 labelPoint = center + new Vector2(
            -halfWidth + (14f * _accessibilityScale),
            WorldPixels(district.WorldFootprint.Y) * 0.22f);
        string glyph = district.Kind switch
        {
            CityDistrictKind.Waterworks => "≋",
            CityDistrictKind.Hospital => "+",
            CityDistrictKind.Industrial => "▥",
            CityDistrictKind.NorthResidential or CityDistrictKind.EastResidential => "⌂",
            _ => "•",
        };
        string text = $"{glyph}  {district.Label}";
        Vector2 textSize = ThemeDB.FallbackFont.GetStringSize(
            text,
            HorizontalAlignment.Left,
            -1,
            LabelFontSize);
        var badge = new Rect2(
            labelPoint - new Vector2(6f, textSize.Y + 5f) * _accessibilityScale,
            textSize + new Vector2(12f, 8f) * _accessibilityScale);
        var style = new StyleBoxFlat
        {
            BgColor = new Color(Color.FromHtml("111817"), 0.88f),
            BorderColor = stateModulate with { A = 0.52f },
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        };
        DrawStyleBox(style, badge);
        DrawString(
            ThemeDB.FallbackFont,
            labelPoint,
            text,
            HorizontalAlignment.Left,
            -1,
            LabelFontSize,
            Text with { A = 0.88f });
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
