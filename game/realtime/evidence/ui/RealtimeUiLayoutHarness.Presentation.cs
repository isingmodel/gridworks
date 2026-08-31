#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.R2;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game.Realtime.UI;

internal sealed partial class RealtimeUiLayoutHarness : Control
{
    private async Task ValidatePresentationStates(
        RealtimeR2LayoutPresentationSet presentations,
        ICollection<string> failures)
    {
        (string Name, RealtimeSlicePresentation Presentation)[] states =
        [
            ("world", presentations.World),
            ("build-shelf", presentations.BuildShelf),
            ("inspector", presentations.Inspector),
            ("action", presentations.Action),
            ("expanded-timeline", presentations.Timeline),
            ("modal", presentations.Modal),
        ];
        (Vector2I Physical, int Scale)[] profiles =
        [
            (new Vector2I(1920, 1080), 100),
            (new Vector2I(1920, 1080), 125),
            (new Vector2I(1920, 1080), 150),
            (new Vector2I(1920, 1080), 200),
            (new Vector2I(3840, 2160), 100),
            (new Vector2I(3840, 2160), 125),
            (new Vector2I(3840, 2160), 150),
            (new Vector2I(3840, 2160), 200),
            (new Vector2I(2560, 1440), 100),
            (new Vector2I(2560, 1440), 200),
        ];
        foreach ((string stateName, RealtimeSlicePresentation presentation) in states)
        foreach ((Vector2I physical, int scale) in profiles)
        {
            string label =
                $"state={stateName}/{physical.X}x{physical.Y}@{scale}%";
            (SubViewport viewport, RealtimeUiRoot stateRoot) = await CreateOffscreenUi(
                physical,
                RealtimeUiMetrics.ReferenceResolution,
                scale,
                presentation);
            try
            {
                RealtimeUiSmokeLayoutSnapshot snapshot = stateRoot.CaptureLayoutForSmoke(
                    RealtimeUiMetrics.ReferenceResolution);
                ValidateSurfaceGeometry(
                    snapshot,
                    RealtimeUiMetrics.ReferenceResolution,
                    presentation,
                    label,
                    failures);
                ValidateButtons(
                    snapshot,
                    ExpectedPrimaryCtaCount(presentation),
                    label,
                    failures);
                ValidateText(snapshot, label, failures);
                ValidateScroll(stateRoot, snapshot, presentation, label, failures);
                RealtimeTimelineItemPresentation[] visibleItems = presentation.Rail.Items
                    .Where(item => item.Visibility != RealtimeTimelineVisibility.Hidden)
                    .Where(item => item.StartMinute <= presentation.Rail.HorizonEndMinute &&
                                   (item.EndMinute ?? item.StartMinute) >=
                                       presentation.Rail.HorizonStartMinute)
                    .OrderBy(item => item.StartMinute)
                    .ThenBy(item => item.Priority)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .ToArray();
                ValidateTimeline(
                    stateRoot.EventRailForSmoke,
                    snapshot,
                    visibleItems,
                    scale,
                    label,
                    failures);
                if (presentation.Modal is null)
                {
                    await ValidateNonModalFocusTraversal(
                        viewport,
                        stateRoot,
                        label,
                        failures);
                }
                if (scale == 200 && presentation.Context.Visible &&
                    presentation.Context.Details.Count > 0)
                {
                    stateRoot.ContextDockForSmoke.PressFirstDetailTabForSmoke();
                    await SettleLayout();
                    RealtimeUiSmokeLayoutSnapshot detailSnapshot =
                        stateRoot.CaptureLayoutForSmoke(
                            RealtimeUiMetrics.ReferenceResolution);
                    ValidateSurfaceGeometry(
                        detailSnapshot,
                        RealtimeUiMetrics.ReferenceResolution,
                        presentation,
                        $"{label}/detail-tab",
                        failures);
                    ValidateText(detailSnapshot, $"{label}/detail-tab", failures);
                    ValidateScroll(
                        stateRoot,
                        detailSnapshot,
                        presentation,
                        $"{label}/detail-tab",
                        failures);
                    await ValidateNonModalFocusTraversal(
                        viewport,
                        stateRoot,
                        $"{label}/detail-tab",
                        failures);
                }
            }
            finally
            {
                RemoveAndFree(viewport);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
    }

    private async Task ValidateG3VisualRenderer(
        RealtimeSlicePresentation baseline,
        ICollection<string> failures)
    {
        var viewport = new SubViewport
        {
            Name = "G3VisualRendererSmokeViewport",
            Size = new Vector2I(1280, 720),
            Size2DOverride = new Vector2I(1280, 720),
            Size2DOverrideStretch = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            HandleInputLocally = true,
        };
        AddChild(viewport);
        var map = new RealtimeWorldMap
        {
            Name = "G3VisualRendererSmokeMap",
            Size = new Vector2(1280, 720),
        };
        viewport.AddChild(map);
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            SpatialRiskAreaDefinition risk = baseline.World.World.RiskAreas.First();
            (RealtimeWorldWeather Weather, string WaterMaterial, bool Forecast, bool Active)[]
                states =
                [
                    (RealtimeWorldWeather.Clear,
                        "res://art/commercial/g3/river/river-water-neutral-b.png", true, false),
                    (RealtimeWorldWeather.Heat,
                        "res://art/commercial/g3/river/river-water-heat-a.png", false, false),
                    (RealtimeWorldWeather.Rain,
                        "res://art/commercial/g3/river/river-water-flood-a.png", false, true),
                    (RealtimeWorldWeather.Storm,
                        "res://art/commercial/g3/river/river-water-flood-a.png", false, true),
                ];
            var drawnUnion = new HashSet<string>(StringComparer.Ordinal);
            foreach ((RealtimeWorldWeather weather, string waterMaterial, bool forecast, bool active)
                     in states)
            {
                map.SetPresentation(baseline.World with
                {
                    Weather = weather,
                    AnalysisVisible = forecast,
                    ForecastRiskAreaIds = forecast
                        ? new[] { risk.RiskAreaId }
                        : Array.Empty<string>(),
                    ActiveRiskAreaIds = active
                        ? new[] { risk.RiskAreaId }
                        : Array.Empty<string>(),
                });
                await ForceActualMapDraw(viewport, map);
                drawnUnion.UnionWith(map.DrawnG3AssetPathsForSmoke);
                Require(map.AllG3AssetsLoadableForSmoke &&
                        map.DrawnG3LayersForSmoke.SequenceEqual(
                            new[]
                            {
                                "city", "conductors", "grid", "ground", "roads", "terrain",
                                "weather",
                            },
                            StringComparer.Ordinal) &&
                        string.Equals(
                            map.DrawnG3WaterMaterialForSmoke,
                            waterMaterial,
                            StringComparison.Ordinal) &&
                        map.DrawnG3SpriteCountForSmoke >= 45 &&
                        map.DrawnRiverBankMaxDeviationForSmoke >= 50f &&
                        map.DrawnMeasuredBridgeCountForSmoke == 2 &&
                        map.MeasuredBridgesLandOnBothBanksForSmoke &&
                        map.DrawnBuildingParcelAlphaForSmoke is > 0f and <= 0.10f &&
                        map.DrawnCityDistrictIdsForSmoke.SequenceEqual(
                            new[]
                            {
                                "east_residential", "hospital", "industrial",
                                "north_residential", "waterworks",
                            },
                            StringComparer.Ordinal) &&
                        map.DrawnCityRoadPathCountForSmoke == 9 &&
                        map.PoleConductorsUseRaisedAttachmentsForSmoke,
                    $"G3 {weather} map draw omitted a required asset/layer/material " +
                    $"(layers=[{string.Join(',', map.DrawnG3LayersForSmoke)}], " +
                    $"water={map.DrawnG3WaterMaterialForSmoke}, " +
                    $"sprites={map.DrawnG3SpriteCountForSmoke}, " +
                    $"bankDeviation={map.DrawnRiverBankMaxDeviationForSmoke:0.##}, " +
                    $"bridges={map.DrawnMeasuredBridgeCountForSmoke}/" +
                    $"{map.MeasuredBridgesLandOnBothBanksForSmoke}, " +
                    $"parcelAlpha={map.DrawnBuildingParcelAlphaForSmoke:0.##}, " +
                    $"poleAttachments=" +
                    $"{map.PoleConductorsUseRaisedAttachmentsForSmoke})",
                    failures);
                if (forecast)
                {
                    Require(map.DrawnForecastRiskAreaIdsForSmoke.SequenceEqual(
                                new[] { risk.RiskAreaId }, StringComparer.Ordinal) &&
                            map.ForecastRiskUsesPatternWithoutFillForSmoke,
                        "G3 clear draw hid the existing forecast risk pattern",
                        failures);
                }
                if (active)
                {
                    Require(map.DrawnActiveRiskAreaIdsForSmoke.SequenceEqual(
                                new[] { risk.RiskAreaId }, StringComparer.Ordinal) &&
                            map.ActiveRiskUsesSolidFillForSmoke,
                        "G3 rain draw hid the existing active risk fill",
                        failures);
                }
            }
            string missingG3Assets = string.Join(
                ',',
                map.G3AssetPathsForSmoke.Where(path => !drawnUnion.Contains(path)));
            Require(drawnUnion.SetEquals(map.G3AssetPathsForSmoke) &&
                    map.G3AssetPathsForSmoke.Count == 39,
                "Realtime clear/heat/rain/storm draw union did not exactly match the 39-file " +
                "adopted map palette " +
                $"(drawn={drawnUnion.Count}, allowed={map.G3AssetPathsForSmoke.Count}, " +
                $"missing=[{missingG3Assets}])",
                failures);
            RealtimeWorldServiceArea serviceArea = baseline.World.ServiceAreas.First();
            SpatialNodeDefinition serviceNode = baseline.World.World.Nodes.Single(item =>
                string.Equals(item.NodeId, serviceArea.NodeId, StringComparison.Ordinal));
            SpatialNodeDefinition coveredLoad = baseline.World.World.Nodes
                .Where(item => baseline.World.World.NodeClasses.Single(nodeClass =>
                        string.Equals(nodeClass.ClassId, item.ClassId,
                            StringComparison.Ordinal)).Kind ==
                    SpatialNodeKind.DedicatedLoadTerminal)
                .First(item => FixedGeometry.CeilDistance(
                    serviceNode.Position,
                    item.Position) <= serviceArea.RadiusUnit);
            int serviceDistance = checked((int)FixedGeometry.CeilDistance(
                serviceNode.Position,
                coveredLoad.Position));
            map.SetPresentation(baseline.World with
            {
                SelectedAssetId = serviceArea.NodeId,
                Highlight = new RealtimeWorldHighlight(
                    [serviceArea.NodeId, coveredLoad.NodeId],
                    Array.Empty<string>(),
                    null,
                    "service-radius-check",
                    new RealtimeWorldServiceLink(
                        serviceArea.NodeId,
                        coveredLoad.NodeId,
                        serviceArea.RadiusUnit,
                        serviceDistance,
                        true)),
            });
            await ForceActualMapDraw(viewport, map);
            Require(map.DrawnServiceAreaRadiusUnitForSmoke == serviceArea.RadiusUnit &&
                    map.DrawnServiceLinkForSmoke,
                "selected substation did not draw its exact R service area and load link",
                failures);

            map.SetPresentation(baseline.World with
            {
                Draft = new RealtimeWorldDraftPresentation(
                    [new RealtimeWorldDraftHandle(
                        RealtimeWorldIds.DraftNode,
                        serviceNode.Position)],
                    Array.Empty<CoreMapPoint>(),
                    false,
                    "SMALL_SUBSTATION"),
                PlacementClass = new RealtimeWorldPlacementClass(
                    "SMALL_SUBSTATION",
                    "소형 배전 변전소",
                    serviceArea.FootprintRadiusUnit,
                    serviceArea.RadiusUnit),
                Tool = RealtimeTool.BuildNode,
            });
            await ForceActualMapDraw(viewport, map);
            Require(map.DrawnSubstationDraftFootprintForSmoke &&
                    map.DrawnServiceAreaRadiusUnitForSmoke == serviceArea.RadiusUnit,
                "substation placement draft omitted its footprint or exact R preview",
                failures);
            RealtimeWorldPresentation movingWeather = baseline.World with
            {
                Weather = RealtimeWorldWeather.Storm,
                Minute = 41,
                ReduceMotion = false,
            };
            RealtimeWorldPresentation reducedWeather = movingWeather with
            {
                ReduceMotion = true,
            };
            Require(
                RealtimeWorldMap.WeatherMinutePhaseForSmoke(movingWeather) !=
                    RealtimeWorldMap.WeatherMinutePhaseForSmoke(
                        movingWeather with { Minute = 42 }) &&
                RealtimeWorldMap.WeatherMinutePhaseForSmoke(reducedWeather) == 0 &&
                RealtimeWorldMap.WeatherMinutePhaseForSmoke(
                    reducedWeather with { Minute = 42 }) == 0,
                "G3 Reduce Motion did not freeze the minute-driven weather phase",
                failures);
        }
        finally
        {
            RemoveAndFree(viewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task ValidateG3UiChrome(
        RealtimeSlicePresentation baseline,
        ICollection<string> failures)
    {
        (SubViewport viewport, RealtimeUiRoot root) = await CreateOffscreenUi(
            new Vector2I(1920, 1080),
            RealtimeUiMetrics.ReferenceResolution,
            uiScalePercent: 100,
            baseline);
        try
        {
            Theme theme = root.ThemeForSmoke;
            var styles = new Dictionary<string, StyleBox>
            {
                ["generic panel"] = theme.GetStylebox("panel", "PanelContainer"),
                ["default button"] = theme.GetStylebox("normal", "Button"),
                ["hover button"] = theme.GetStylebox("hover", "Button"),
                ["pressed button"] = theme.GetStylebox("pressed", "Button"),
                ["tool button"] = theme.GetStylebox("normal", "ToolButton"),
                ["top HUD"] = root.TopHudForSmoke.GetThemeStylebox("panel"),
                ["event rail"] = root.EventRailForSmoke.GetThemeStylebox("panel"),
                ["context dock"] = root.ContextDockForSmoke.GetThemeStylebox("panel"),
                ["build shelf"] = root.BuildShelfForSmoke.GetThemeStylebox("panel"),
                ["action dock"] = root.ActionDockForSmoke.GetThemeStylebox("panel"),
                ["modal"] = root.ModalHostForSmoke
                    .GetNode<PanelContainer>("Center/ModalPanel")
                    .GetThemeStylebox("panel"),
            };
            StyleBoxFlat? top = styles["top HUD"] as StyleBoxFlat;
            StyleBoxFlat? rail = styles["event rail"] as StyleBoxFlat;
            StyleBoxFlat? primary = theme.GetStylebox("normal", "PrimaryButton") as StyleBoxFlat;
            bool hierarchyIsDistinct = top is not null && rail is not null && primary is not null &&
                !top.BgColor.IsEqualApprox(rail.BgColor) &&
                primary.BgColor.Luminance > top.BgColor.Luminance &&
                top.BorderWidthBottom >= 2 &&
                rail.BorderWidthBottom == 1;
            Require(styles.Values.All(style => style is StyleBoxFlat) && hierarchyIsDistinct,
                "Realtime UI chrome did not use the simplified flat hierarchy " +
                $"(styles=[{string.Join(", ", styles.Select(item =>
                    $"{item.Key}={item.Value.GetType().Name}"))}], " +
                $"top={top?.BgColor}, rail={rail?.BgColor}, primary={primary?.BgColor})",
                failures);
        }
        finally
        {
            RemoveAndFree(viewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task ValidateAuditPresentationSemantics(
        RealtimeSlicePresentation baseline,
        ICollection<string> failures)
    {
        await ValidateLockedSpeedPresentation(
            baseline,
            RealtimeSimulationState.AutoPaused,
            RealtimePauseReason.ChapterBriefing,
            "장 안내 정지",
            failures);
        await ValidateLockedSpeedPresentation(
            baseline,
            RealtimeSimulationState.Ended,
            RealtimePauseReason.CampaignResult,
            "운영 종료",
            failures);

        var typedSlice = CreateRunningAuditSlice();
        using var typedLifetime = typedSlice.FreeAfterSmoke();
        RealtimeTimelineItemPresentation thermalKindItem =
            typedSlice.LatestPresentation.Rail.Items.First(item =>
                item.Kind == RealtimeTimelineItemKind.ThermalProtection);
        RealtimeCampaignSnapshot typedBase = typedSlice.CoreSnapshot;
        RealtimeForecastEvent sourceForecast = typedBase.Forecast.Events[0];
        RealtimeScheduledEventDefinition sourceScheduled =
            typedBase.Chapter.ScheduledEvents.Single(item => string.Equals(
                item.EventId,
                sourceForecast.EventId,
                StringComparison.Ordinal));
        CommercialOperatingPhaseDefinition weatherProfile =
            sourceForecast.OperatingProfile with
            {
                PhaseId = "SMOKE_WEATHER_PROFILE",
                DisplayName = "동부 생활권 폭우",
                ActiveRiskAreaIds = new[] { "SMOKE_RISK_AREA" },
                UnavailableNodeIds = Array.Empty<string>(),
                UnavailableEdgeIds = Array.Empty<string>(),
            };
        string authoredUnavailableNodeId = typedBase.Construction.World.Nodes
            .OrderBy(item => item.NodeId, StringComparer.Ordinal)
            .First().NodeId;
        CommercialOperatingPhaseDefinition outageProfile =
            sourceForecast.OperatingProfile with
            {
                PhaseId = "SMOKE_OUTAGE_PROFILE",
                DisplayName = "배전 설비 계획 사용불가",
                ActiveRiskAreaIds = Array.Empty<string>(),
                UnavailableNodeIds = new[] { authoredUnavailableNodeId },
                UnavailableEdgeIds = Array.Empty<string>(),
            };
        RealtimeForecastEvent weatherForecast = sourceForecast with
        {
            EventId = "SMOKE_WEATHER_EVENT",
            DisplayName = weatherProfile.DisplayName,
            OperatingProfile = weatherProfile,
        };
        RealtimeForecastEvent outageForecast = sourceForecast with
        {
            EventId = "SMOKE_OUTAGE_EVENT",
            DisplayName = outageProfile.DisplayName,
            StartMinute = sourceForecast.StartMinute + 90,
            EndMinute = sourceForecast.EndMinute + 90,
            OperatingProfile = outageProfile,
        };
        RealtimeCampaignSnapshot typedSnapshot = typedBase with
        {
            Chapter = typedBase.Chapter with
            {
                ScheduledEvents = new[]
                {
                    sourceScheduled with
                    {
                        EventId = weatherForecast.EventId,
                        OperatingProfile = weatherProfile,
                    },
                    sourceScheduled with
                    {
                        EventId = outageForecast.EventId,
                        StartOffsetMinutes = sourceScheduled.StartOffsetMinutes + 90,
                        OperatingProfile = outageProfile,
                    },
                },
            },
            Forecast = typedBase.Forecast with
            {
                Events = new[] { weatherForecast, outageForecast },
            },
        };
        RealtimeSlicePresentation typedPresentation =
            typedSlice.PresentSnapshotForSmoke(typedSnapshot);
        RealtimeTimelineItemPresentation weatherItem =
            typedPresentation.Rail.Items.Single(item => string.Equals(
                item.Id, weatherForecast.EventId, StringComparison.Ordinal));
        RealtimeTimelineItemPresentation outageItem =
            typedPresentation.Rail.Items.Single(item => string.Equals(
                item.Id, outageForecast.EventId, StringComparison.Ordinal));
        RealtimeInteractionState typedSelection = typedSlice.InteractionState with
        {
            Tool = RealtimeTool.Inspect,
            Surface = RealtimeSurface.Inspector,
            SelectionId = outageItem.Id,
            TimelineSelectedItemId = outageItem.Id,
        };
        RealtimeSlicePresentation selectedOutagePresentation =
            typedSlice.PresentSnapshotForSmoke(typedSnapshot, typedSelection);
        RealtimeTimelineTarget outageTarget = RealtimeTimelineTargetResolver.Resolve(
            typedSlice.DisplayWorldForSmoke,
            typedSnapshot,
            outageItem.Id);
        RealtimeWorldHighlight? outageHighlight =
            selectedOutagePresentation.World.Highlight;
        Require(weatherItem.Kind == RealtimeTimelineItemKind.Weather &&
                weatherItem.Lane == RealtimeTimelineLane.WeatherAndOutage &&
                outageItem.Kind == RealtimeTimelineItemKind.PlannedOutage &&
                outageItem.Lane == RealtimeTimelineLane.WeatherAndOutage &&
                thermalKindItem.Lane == RealtimeTimelineLane.ThermalProtection &&
                !string.Equals(outageItem.KindIcon, thermalKindItem.KindIcon,
                    StringComparison.Ordinal) &&
                !string.Equals(outageItem.KindLabel, thermalKindItem.KindLabel,
                    StringComparison.Ordinal),
            "authored risk/unavailability profiles were flattened into demand markers",
            failures);
        (SubViewport typedViewport, RealtimeUiRoot typedRoot) = await CreateOffscreenUi(
            RealtimeUiMetrics.ReferenceResolution,
            RealtimeUiMetrics.ReferenceResolution,
            100,
            typedPresentation);
        try
        {
            RealtimeUiSmokeMarkerFact weatherMarker = typedRoot.EventRailForSmoke
                .MarkerFactsForSmoke().Single(marker => marker.ItemIds.Contains(
                    weatherItem.Id, StringComparer.Ordinal));
            RealtimeUiSmokeMarkerFact outageMarker = typedRoot.EventRailForSmoke
                .MarkerFactsForSmoke().Single(marker => marker.ItemIds.Contains(
                    outageItem.Id, StringComparer.Ordinal));
            Require(weatherMarker.AccessibilityName.Contains(
                        weatherItem.KindLabel, StringComparison.Ordinal) &&
                    outageMarker.AccessibilityName.Contains(
                        outageItem.KindLabel, StringComparison.Ordinal) &&
                    !string.Equals(weatherItem.KindLabel, weatherItem.KindIcon,
                        StringComparison.Ordinal) &&
                    !string.Equals(outageItem.KindLabel, outageItem.KindIcon,
                        StringComparison.Ordinal) &&
                    outageMarker.AccessibilityName.Contains(
                        "계획 사용불가", StringComparison.Ordinal) &&
                    !outageMarker.AccessibilityName.Contains(
                        "열 보호", StringComparison.Ordinal),
                "weather/outage markers exposed glyphs instead of typed AX kind labels",
                failures);
        Require(string.Equals(outageTarget.MapSubjectId, authoredUnavailableNodeId,
                    StringComparison.Ordinal) &&
                string.Equals(selectedOutagePresentation.World.SelectedAssetId,
                    authoredUnavailableNodeId, StringComparison.Ordinal) &&
                string.Equals(selectedOutagePresentation.Context.SubjectId,
                    outageItem.Id, StringComparison.Ordinal) &&
                outageHighlight is not null &&
                outageHighlight.NodeIds.Contains(
                    authoredUnavailableNodeId, StringComparer.Ordinal),
            "planned-outage target did not preserve its independently authored " +
            $"unavailable node {authoredUnavailableNodeId}",
            failures);

            SpatialRiskAreaDefinition authoredRisk = typedPresentation.World.World
                .RiskAreas.First();
            var riskMap = new RealtimeWorldMap
            {
                Size = new Vector2(960, 540),
            };
            typedViewport.AddChild(riskMap);
            riskMap.SetPresentation(typedPresentation.World with
            {
                AnalysisVisible = true,
                ForecastRiskAreaIds = new[] { authoredRisk.RiskAreaId },
                ActiveRiskAreaIds = Array.Empty<string>(),
            });
            await ForceActualMapDraw(typedViewport, riskMap);
            Require(riskMap.DrawnForecastRiskAreaIdsForSmoke.SequenceEqual(
                        new[] { authoredRisk.RiskAreaId },
                        StringComparer.Ordinal) &&
                    riskMap.DrawnActiveRiskAreaIdsForSmoke.Count == 0 &&
                    riskMap.ForecastRiskUsesPatternWithoutFillForSmoke &&
                    riskMap.AccessibilityName.Contains(
                        "범람 예고 점선 윤곽",
                        StringComparison.Ordinal),
                "forecast risk did not render as the named pattern-only outline",
                failures);
            riskMap.SetPresentation(typedPresentation.World with
            {
                AnalysisVisible = false,
                ForecastRiskAreaIds = Array.Empty<string>(),
                ActiveRiskAreaIds = new[] { authoredRisk.RiskAreaId },
            });
            await ForceActualMapDraw(typedViewport, riskMap);
            Require(riskMap.DrawnForecastRiskAreaIdsForSmoke.Count == 0 &&
                    riskMap.DrawnActiveRiskAreaIdsForSmoke.SequenceEqual(
                        new[] { authoredRisk.RiskAreaId },
                        StringComparer.Ordinal) &&
                    riskMap.ActiveRiskUsesSolidFillForSmoke &&
                    riskMap.AccessibilityName.Contains(
                        "활성 범람 실선 채움",
                        StringComparison.Ordinal),
                "active risk did not replace forecast with the named solid fill",
                failures);
        }
        finally
        {
            RemoveAndFree(typedViewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var activeSlice = CreateRunningAuditSlice();
        using var activeLifetime = activeSlice.FreeAfterSmoke();
        long activeMinute = activeSlice.CoreSnapshot.Forecast.Events
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .First().StartMinute;
        activeSlice.AdvanceToForSmoke(activeMinute);
        RealtimeTimelineItemPresentation activeItem = activeSlice.LatestPresentation.Rail.Items
            .First(item => item.IsCurrent &&
                item.Visibility == RealtimeTimelineVisibility.Active &&
                RealtimeTimelineTargetResolver.Resolve(
                    activeSlice.DisplayWorldForSmoke,
                    activeSlice.CoreSnapshot,
                    item.Id).Kind == RealtimeTimelineTargetKind.Event);
        RealtimeTimelineItemPresentation selectedItem = activeSlice.LatestPresentation.Rail.Items
            .Where(item => !item.IsCurrent &&
                item.Lane != activeItem.Lane)
            .OrderByDescending(item => item.StartMinute)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .First();
        activeSlice.ChooseTimelineClusterForSmoke(new[] { selectedItem.Id });
        (SubViewport activeViewport, RealtimeUiRoot activeRoot) = await CreateOffscreenUi(
            RealtimeUiMetrics.ReferenceResolution,
            RealtimeUiMetrics.ReferenceResolution,
            100,
            activeSlice.LatestPresentation);
        try
        {
            IReadOnlyList<RealtimeUiSmokeMarkerFact> markerFacts =
                activeRoot.EventRailForSmoke.MarkerFactsForSmoke();
            RealtimeUiSmokeMarkerFact activeMarker = markerFacts.Single(marker =>
                marker.ItemIds.Contains(activeItem.Id, StringComparer.Ordinal));
            RealtimeUiSmokeMarkerFact selectedMarker = markerFacts.Single(marker =>
                marker.ItemIds.Contains(selectedItem.Id, StringComparer.Ordinal));
            RealtimeTimelineTooltipOverlayFact activeDetail =
                activeRoot.EventRailForSmoke.TooltipOverlayFactForSmoke(activeItem.Id);
            Require(!activeMarker.ItemIds.Contains(selectedItem.Id,
                        StringComparer.Ordinal) &&
                    !activeMarker.Selected &&
                    activeMarker.OutlineSize >= 2 &&
                    activeMarker.VisibleText.StartsWith("▶",
                        StringComparison.Ordinal) &&
                    selectedMarker.Selected &&
                    !selectedMarker.VisibleText.StartsWith("▶",
                        StringComparison.Ordinal) &&
                    activeDetail.CustomOverlay &&
                    activeDetail.Text.Contains("진행 중", StringComparison.Ordinal) &&
                    activeDetail.Text.Contains(activeItem.Title, StringComparison.Ordinal),
                "timeline conflated active-now outline/text with selected pressed state " +
                $"(activeText={activeMarker.VisibleText}, " +
                $"activeSelected={activeMarker.Selected}, " +
                $"activeOutline={activeMarker.OutlineSize}, " +
                $"selectedText={selectedMarker.VisibleText}, " +
                $"selectedPressed={selectedMarker.Selected}, " +
                $"selectedOutline={selectedMarker.OutlineSize})",
                failures);
        }
        finally
        {
            RemoveAndFree(activeViewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        RealtimeTimelineItemPresentation mixedCurrent = activeItem with
        {
            Id = "SMOKE_MIXED_CURRENT",
            StartMinute = activeMinute,
            EndMinute = activeMinute + 30,
            IsCurrent = true,
            Visibility = RealtimeTimelineVisibility.Active,
            Lane = RealtimeTimelineLane.DemandAndDeadline,
            Priority = 10,
            TimeLabel = "현재",
        };
        RealtimeTimelineItemPresentation mixedSelected = selectedItem with
        {
            Id = "SMOKE_MIXED_SELECTED",
            StartMinute = activeMinute,
            EndMinute = null,
            IsCurrent = false,
            Visibility = RealtimeTimelineVisibility.Announced,
            Lane = RealtimeTimelineLane.WeatherAndOutage,
            Priority = 20,
            TimeLabel = "곧",
        };
        RealtimeSlicePresentation mixedPresentation = activeSlice.LatestPresentation with
        {
            Rail = activeSlice.LatestPresentation.Rail with
            {
                Items = new[] { mixedCurrent, mixedSelected },
                SelectedItemId = mixedSelected.Id,
            },
        };
        (SubViewport mixedViewport, RealtimeUiRoot mixedRoot) = await CreateOffscreenUi(
            RealtimeUiMetrics.ReferenceResolution,
            RealtimeUiMetrics.ReferenceResolution,
            100,
            mixedPresentation);
        try
        {
            RealtimeUiSmokeMarkerFact mixedMarker = mixedRoot.EventRailForSmoke
                .MarkerFactsForSmoke().Single();
            RealtimeTimelineTooltipOverlayFact mixedDetail = mixedRoot.EventRailForSmoke
                .TooltipOverlayFactForSmoke(mixedSelected.Id);
            Require(mixedMarker.ItemIds.Count == 2 &&
                    mixedMarker.ItemIds.Contains(mixedCurrent.Id, StringComparer.Ordinal) &&
                    mixedMarker.ItemIds.Contains(mixedSelected.Id, StringComparer.Ordinal) &&
                    mixedMarker.Selected &&
                    mixedMarker.OutlineSize >= 2 &&
                    string.Equals(mixedMarker.SemanticItemId, mixedSelected.Id,
                        StringComparison.Ordinal) &&
                    mixedMarker.VisibleText.StartsWith("▶",
                        StringComparison.Ordinal) &&
                    mixedMarker.VisibleText.Contains("+1", StringComparison.Ordinal) &&
                    mixedDetail.CustomOverlay &&
                    mixedDetail.Text.Contains(mixedCurrent.Title,
                        StringComparison.Ordinal) &&
                    mixedDetail.Text.Contains(mixedSelected.Title,
                        StringComparison.Ordinal) &&
                    mixedMarker.AccessibilityName.Contains("진행 중.",
                        StringComparison.Ordinal),
                "mixed current-sibling/non-current-selected cluster lost pressed, " +
                "outline, semantic-ID, visible-current, or AX-current semantics",
                failures);

            (long beforeBoundary, long afterBoundary) = mixedRoot.EventRailForSmoke
                .LegacyBucketBoundaryPairForSmoke();
            RealtimeTimelineItemPresentation boundaryBefore = mixedSelected with
            {
                Id = "SMOKE_BUCKET_BOUNDARY_BEFORE",
                StartMinute = beforeBoundary,
                Priority = 1,
                Lane = RealtimeTimelineLane.DemandAndDeadline,
                TimeLabel = "경계 직전",
            };
            RealtimeTimelineItemPresentation boundaryAfter = mixedSelected with
            {
                Id = "SMOKE_BUCKET_BOUNDARY_AFTER",
                StartMinute = afterBoundary,
                Priority = 2,
                Lane = RealtimeTimelineLane.ThermalProtection,
                TimeLabel = "경계 직후",
            };
            RealtimeSlicePresentation boundaryPresentation = mixedPresentation with
            {
                Rail = mixedPresentation.Rail with
                {
                    Items = new[] { boundaryBefore, boundaryAfter },
                    SelectedItemId = null,
                },
            };
            Present(mixedRoot, boundaryPresentation);
            await SettleLayout();
            RealtimeUiSmokeMarkerFact[] boundaryMarkers = mixedRoot.EventRailForSmoke
                .MarkerFactsForSmoke().ToArray();
            Require(boundaryMarkers.Length == 1 &&
                    boundaryMarkers[0].ItemIds.SequenceEqual(
                        new[] { boundaryBefore.Id, boundaryAfter.Id },
                        StringComparer.Ordinal),
                "cross-authored-lane markers straddling a legacy bucket edge were " +
                "not clustered before their single-track rectangles could intersect",
                failures);
        }
        finally
        {
            RemoveAndFree(mixedViewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var completedSlice = CreateRunningAuditSlice();
        using var completedLifetime = completedSlice.FreeAfterSmoke();
        long completedMinute = completedSlice.SmokeBoundaryFacts.Events
            .OrderBy(item => item.EndMinute)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .First().EndMinute;
        completedSlice.AdvanceToForSmoke(completedMinute);
        RealtimeTimelineItemPresentation completedItem =
            completedSlice.LatestPresentation.Rail.Items.First(item =>
                item.Visibility == RealtimeTimelineVisibility.Completed);
        (SubViewport completedViewport, RealtimeUiRoot completedRoot) =
            await CreateOffscreenUi(
                RealtimeUiMetrics.ReferenceResolution,
                RealtimeUiMetrics.ReferenceResolution,
                100,
                completedSlice.LatestPresentation);
        try
        {
            RealtimeUiSmokeMarkerFact completedMarker = completedRoot.EventRailForSmoke
                .MarkerFactsForSmoke().Single(marker => marker.ItemIds.Contains(
                    completedItem.Id, StringComparer.Ordinal));
            RealtimeTimelineTooltipOverlayFact completedDetail =
                completedRoot.EventRailForSmoke.TooltipOverlayFactForSmoke(
                    completedItem.Id);
            Require(completedDetail.CustomOverlay &&
                    completedDetail.Text.Contains("완료", StringComparison.Ordinal) &&
                    completedMarker.AccessibilityName.Contains("완료됨.",
                        StringComparison.Ordinal),
                "recent completed event lacked distinct hover-detail and AX completion state",
                failures);
        }
        finally
        {
            RemoveAndFree(completedViewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        RealtimeLayoutProfile label100 = RealtimeUiMetrics.ForWindow(
            new Vector2I(1920, 1080), 100);
        RealtimeLayoutProfile label200 = RealtimeUiMetrics.ForWindow(
            new Vector2I(1920, 1080), 200);
        var map = new RealtimeWorldMap();
        try
        {
            map.ApplyLayoutForSmoke(label100);
            int font100 = map.LabelFontSizeForSmoke;
            map.ApplyLayoutForSmoke(label200);
            int font200 = map.LabelFontSizeForSmoke;
            Require(font100 == Mathf.RoundToInt(12f * label100.AccessibilityScale) &&
                    font200 == Mathf.RoundToInt(12f * label200.AccessibilityScale) &&
                    font200 == font100 * 2,
                "world map labels ignored the 100/200% UI accessibility scale",
                failures);
            Require(map.StatusLabelForSmoke(
                        RealtimeWorldAssetState.Emergency) == "비상 운전" &&
                    map.StatusLabelForSmoke(
                        RealtimeWorldAssetState.ProtectiveOutage) == "보호정지" &&
                    map.StatusLabelForSmoke(
                        RealtimeWorldAssetState.OverLimit) == "한계 초과",
                "map thermal states lost their non-color text/AX labels",
                failures);
            Require(map.StateCueForSmoke(
                        RealtimeWorldAssetState.Emergency) ==
                        RealtimeWorldStateCue.EmergencyTriangle &&
                    map.StateCueForSmoke(
                        RealtimeWorldAssetState.ProtectiveOutage) ==
                        RealtimeWorldStateCue.ProtectiveOutageCross &&
                    map.StateCueForSmoke(
                        RealtimeWorldAssetState.OverLimit) ==
                        RealtimeWorldStateCue.OverLimitDiamond,
                "map thermal states lost their non-color geometric cues",
                failures);
        }
        finally
        {
            map.Free();
        }
    }

    private async Task ValidateLockedSpeedPresentation(
        RealtimeSlicePresentation baseline,
        RealtimeSimulationState simulation,
        RealtimePauseReason reason,
        string expectedVisibleStatus,
        ICollection<string> failures)
    {
        RealtimeTimelineItemPresentation next = baseline.Rail.Items
            .Where(item => item.StartMinute > baseline.Rail.NowMinute)
            .OrderBy(item => item.StartMinute)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .First();
        var pause = new RealtimePausePresentation(
            reason,
            baseline.Rail.NowMinute,
            baseline.Rail.NowLabel,
            next.Id,
            next.StartMinute,
            $"{next.TimeLabel} · {next.ShortLabel}");
        RealtimeSlicePresentation locked = baseline with
        {
            Hud = baseline.Hud with
            {
                SimulationState = simulation,
                Speed = RealtimeSimulationSpeed.Paused,
                Pause = pause,
            },
            Modal = null,
        };
        (SubViewport viewport, RealtimeUiRoot root) = await CreateOffscreenUi(
            RealtimeUiMetrics.ReferenceResolution,
            RealtimeUiMetrics.ReferenceResolution,
            100,
            locked);
        int requests = 0;
        void Observe(RealtimeSimulationSpeed _) => requests++;
        root.SpeedRequested += Observe;
        try
        {
            RealtimeUiSmokeSpeedFact[] before = root.TopHudForSmoke.SpeedFactsForSmoke
                .ToArray();
            Require(before.All(item => !item.Enabled) &&
                    before.Count(item => item.Pressed) == 1 &&
                    before.Single(item => item.Pressed).Speed ==
                        RealtimeSimulationSpeed.Paused &&
                    root.TopHudForSmoke.PauseStatusTextForSmoke.Contains(
                        expectedVisibleStatus, StringComparison.Ordinal) &&
                    before.Where(item => item.Speed != RealtimeSimulationSpeed.Paused)
                        .All(item => item.Tooltip.Contains(
                            "바꿀 수 없습니다", StringComparison.Ordinal)),
                $"{simulation} HUD did not visibly lock every speed at paused state",
                failures);
            if (simulation == RealtimeSimulationState.Ended)
            {
                RealtimeUiSmokeSpeedFact ended = before.Single(item =>
                    item.Speed == RealtimeSimulationSpeed.Paused);
                Require(!ended.Tooltip.Contains("재개", StringComparison.Ordinal) &&
                        !ended.Tooltip.Contains("(P)", StringComparison.Ordinal) &&
                        ended.AccessibilityName == "운영 종료" &&
                        ended.AccessibilityDescription.Contains(
                            "운영이 종료", StringComparison.Ordinal),
                    "ended pause control advertised a nonexistent resume shortcut",
                    failures);
            }
            PushViewportPrimary(
                viewport,
                before.Single(item => item.Speed == RealtimeSimulationSpeed.Normal)
                    .Rect.GetCenter());
            await SettleLayout();
            RealtimeUiSmokeSpeedFact[] after = root.TopHudForSmoke.SpeedFactsForSmoke
                .ToArray();
            Require(requests == 0 &&
                    after.Select(item => (item.Speed, item.Enabled, item.Pressed))
                        .SequenceEqual(before.Select(item =>
                            (item.Speed, item.Enabled, item.Pressed))),
                $"disabled {simulation} speed control emitted or visually desynchronized",
                failures);
        }
        finally
        {
            root.SpeedRequested -= Observe;
            RemoveAndFree(viewport);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static RealtimeSliceMain CreateRunningAuditSlice()
    {
        var slice = new RealtimeSliceMain();
        try
        {
            slice.BootstrapForSmoke();
            string modalId = slice.InteractionState.ActiveModalId ??
                throw new InvalidOperationException(
                    "Audit slice did not expose its chapter briefing.");
            RealtimeR2IntentResult close = slice.ApplyIntentForSmoke(
                RealtimeR2Intent.CloseModal(modalId));
            if (!close.Accepted ||
                slice.InteractionState.Simulation != RealtimeSimulationState.Running)
            {
                throw new InvalidOperationException(
                    $"Audit slice could not close its briefing: {close.Error}");
            }
            return slice;
        }
        catch
        {
            slice.Free();
            throw;
        }
    }

    private static void ValidateScaledCustomMapHitTargets(
        RealtimeWorldMap map,
        string selectedAssetId,
        RealtimeLayoutProfile restoreProfile,
        ICollection<string> failures)
    {
        (Vector2I Physical, int Scale)[] profiles =
        [
            (new Vector2I(1920, 1080), 100),
            (new Vector2I(1920, 1080), 125),
            (new Vector2I(1920, 1080), 150),
            (new Vector2I(1920, 1080), 200),
            (new Vector2I(3840, 2160), 100),
            (new Vector2I(3840, 2160), 125),
            (new Vector2I(3840, 2160), 150),
            (new Vector2I(3840, 2160), 200),
            (new Vector2I(2560, 1440), 100),
            (new Vector2I(2560, 1440), 200),
        ];
        try
        {
            foreach ((Vector2I physical, int scale) in profiles)
            {
                string label = $"{physical.X}x{physical.Y}@{scale}%";
                RealtimeLayoutProfile profile = RealtimeUiMetrics.ForWindow(
                    physical,
                    scale);
                map.ApplyLayoutForSmoke(profile);
                float insideOffset = profile.MinimumHitTarget / 2f - 0.25f;
                float outsideOffset = profile.MinimumHitTarget / 2f + 0.75f;

                (string AssetId, Vector2 CanvasPoint)? action =
                    map.SelectionActionCanvasPointForSmoke;
                Require(action is not null && string.Equals(
                            action.Value.AssetId,
                            selectedAssetId,
                            StringComparison.Ordinal),
                    $"{label} did not expose the selected action hit probe",
                    failures);
                if (action is not null)
                {
                    string actionId = $"ACTION:INSPECT:{selectedAssetId}";
                    RealtimePointerResolution actionInside =
                        map.ResolveCanvasPointForSmoke(
                            action.Value.CanvasPoint + Vector2.Right * insideOffset);
                    RealtimePointerResolution actionOutside =
                        map.ResolveCanvasPointForSmoke(
                            action.Value.CanvasPoint + Vector2.Right * outsideOffset);
                    Require(actionInside.Owner == RealtimePointerOwner.SelectionAction &&
                            string.Equals(actionInside.ResolvedId, actionId,
                                StringComparison.Ordinal) &&
                            actionInside.OrderedCandidates.Any(item => string.Equals(
                                item.Id, actionId, StringComparison.Ordinal)) &&
                            actionOutside.OrderedCandidates.All(item => !string.Equals(
                                item.Id, actionId, StringComparison.Ordinal)),
                        $"{label} actual selection-action resolver missed its scaled " +
                        "minimum boundary or leaked beyond it",
                        failures);
                }

                (string edgeId, Vector2 edgePoint, Vector2 edgeNormal) =
                    map.EdgeHitProbeForSmoke();
                RealtimePointerResolution edgeInside = map.ResolveCanvasPointForSmoke(
                    edgePoint + edgeNormal * insideOffset);
                RealtimePointerResolution edgeOutside = map.ResolveCanvasPointForSmoke(
                    edgePoint + edgeNormal * outsideOffset);
                Require(edgeInside.OrderedCandidates.Any(item => string.Equals(
                            item.Id, edgeId, StringComparison.Ordinal)) &&
                        edgeOutside.OrderedCandidates.All(item => !string.Equals(
                            item.Id, edgeId, StringComparison.Ordinal)),
                    $"{label} actual edge resolver missed its scaled minimum boundary " +
                    $"or leaked beyond it ({edgeId})",
                    failures);
            }
        }
        finally
        {
            map.ApplyLayoutForSmoke(restoreProfile);
        }
    }

    private async Task ValidateNonModalFocusTraversal(
        SubViewport viewport,
        RealtimeUiRoot uiRoot,
        string label,
        ICollection<string> failures)
    {
        BaseButton[] targets = uiRoot.FocusableButtonsForSmoke().ToArray();
        Require(targets.Length > 0,
            $"{label} has no enabled nonmodal keyboard target", failures);
        if (targets.Length == 0)
        {
            return;
        }

        string[] expectedPaths = targets
            .Select(item => item.GetPath().ToString())
            .ToArray();
        await ValidateFocusDirection(
            viewport,
            uiRoot,
            targets[0],
            expectedPaths,
            backwards: false,
            label,
            failures);
        await ValidateFocusDirection(
            viewport,
            uiRoot,
            targets[^1],
            expectedPaths,
            backwards: true,
            label,
            failures);
        uiRoot.FocusOwnerForSmoke?.ReleaseFocus();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task ValidateFocusDirection(
        SubViewport viewport,
        RealtimeUiRoot uiRoot,
        BaseButton start,
        IReadOnlyList<string> expectedPaths,
        bool backwards,
        string label,
        ICollection<string> failures)
    {
        var expected = expectedPaths.ToHashSet(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        start.GrabFocus();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (int step = 0; step < expectedPaths.Count; step++)
        {
            Control? focus = uiRoot.FocusOwnerForSmoke;
            if (focus is null)
            {
                break;
            }
            visited.Add(focus.GetPath().ToString());
            PushViewportKey(
                viewport,
                Key.Tab,
                pressed: true,
                shiftPressed: backwards);
            PushViewportKey(
                viewport,
                Key.Tab,
                pressed: false,
                shiftPressed: backwards);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        string? finalPath = uiRoot.FocusOwnerForSmoke?.GetPath().ToString();
        string startPath = start.GetPath().ToString();
        Require(visited.SetEquals(expected) &&
                string.Equals(finalPath, startPath, StringComparison.Ordinal),
            $"{label} actual {(backwards ? "Shift+Tab" : "Tab")} traversal " +
            $"did not reach every enabled target exactly once and wrap " +
            $"(expected=[{string.Join(",", expected.OrderBy(item => item, StringComparer.Ordinal))}], " +
            $"visited=[{string.Join(",", visited.OrderBy(item => item, StringComparer.Ordinal))}], " +
            $"final={finalPath ?? "<none>"}, start={startPath})",
            failures);
    }

    private static IReadOnlySet<string> ExpectedVisibleSurfaces(
        RealtimeSlicePresentation presentation)
    {
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "TopHud",
            "EventRail",
        };
        bool timelineOwnsWorkspace = presentation.Rail.Expanded;
        bool contextVisible = presentation.Context.Visible && !timelineOwnsWorkspace;
        bool contextOwnsPrimary = contextVisible &&
            presentation.Context.PrimaryAction is { Visible: true };
        bool actionVisible = !contextOwnsPrimary && !timelineOwnsWorkspace &&
            presentation.ActionDock is
            {
                Visible: true,
                PrimaryAction.Visible: true,
            };
        bool buildVisible = !contextOwnsPrimary && !actionVisible &&
            !timelineOwnsWorkspace && presentation.BuildShelf.Visible;
        if (contextVisible)
        {
            expected.Add("ContextDock");
        }
        if (actionVisible)
        {
            expected.Add("ActionDock");
        }
        if (buildVisible)
        {
            expected.Add("BuildShelf");
        }
        return expected;
    }

    private static int ExpectedPrimaryCtaCount(RealtimeSlicePresentation presentation)
    {
        IReadOnlySet<string> visible = ExpectedVisibleSurfaces(presentation);
        int count = 0;
        if (visible.Contains("ContextDock") &&
            presentation.Context.PrimaryAction is
            {
                Visible: true,
                Tone: RealtimeActionTone.Primary,
            })
        {
            count++;
        }
        if (visible.Contains("ActionDock") &&
            presentation.ActionDock.PrimaryAction is
            {
                Visible: true,
                Tone: not RealtimeActionTone.Destructive,
            })
        {
            count++;
        }
        if (presentation.Modal?.PrimaryAction is
            {
                Visible: true,
                Tone: RealtimeActionTone.Primary,
            })
        {
            count++;
        }
        return count;
    }
}
#endif
