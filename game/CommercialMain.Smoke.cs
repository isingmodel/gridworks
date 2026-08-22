#if DEBUG || COMMERCIAL_INTERNAL
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Gridworks.Core.Release.V2;
using Godot;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game;

internal sealed partial class CommercialMain
{
    private async void RunPresentationSmoke()
    {
        try
        {
            await NextFrame();
            string evidenceDirectory = System.Environment.GetEnvironmentVariable(
                    "GRIDWORKS_STAGE_G_EVIDENCE_DIRECTORY")
                ?? throw new InvalidOperationException(
                    "Stage-G presentation smoke에는 evidence directory가 필요합니다.");
            if (!Path.IsPathFullyQualified(evidenceDirectory))
            {
                throw new InvalidOperationException(
                    "Stage-G evidence directory는 절대경로여야 합니다.");
            }
            Directory.CreateDirectory(evidenceDirectory);
            GetWindow().Size = new Vector2I(1920, 1080);
            await NextFrame();
            Require(
                _settings.UiScalePercent == 100 &&
                !_settings.ReduceMotion &&
                _shell.Page == ReleaseShellPage.Title &&
                _shell.GetActionButton(ReleaseShellAction.NewGame).HasFocus() &&
                ControlInside(_shell, _shell.GetActionButton(ReleaseShellAction.NewGame)),
                "1920×1080·UI 100% 제목 화면의 keyboard focus와 bounds가 올바르지 않습니다.");
            SaveEvidencePng(Path.Combine(evidenceDirectory, "1920x1080-ui100-title.png"));

            await PressShellAsync(ReleaseShellAction.NewGame, "새 게임");
            await NextFrame();
            Require(
                _shell.Page == ReleaseShellPage.Hidden &&
                _presentationMode == CommercialPresentationMode.Briefing &&
                _map.OperationsLocked &&
                _panel.GetActionButton(CommercialPanelAction.ApproveWindow)
                    .AccessibilityName == "임무 시작" &&
                !_panel.GetActionButton(CommercialPanelAction.StartLine).Visible,
                "새 게임이 generic 조작 도움말 대신 잠긴 첫 임무 브리핑을 열지 않았습니다.");
            SaveEvidencePng(Path.Combine(
                evidenceDirectory,
                "1920x1080-ui100-first-briefing.png"));
            await AdvancePresentationToOperations("첫 불빛 브리핑");
            Require(
                _panel.AccessibilityName.Contains("변전소 놓기(2)", StringComparison.Ordinal),
                "첫 운영 화면이 변전소 배치라는 단계별 다음 행동을 표시하지 않았습니다.");
            SaveEvidencePng(Path.Combine(
                evidenceDirectory,
                "1920x1080-ui100-first-operations.png"));

            // Construction input is exercised at the closest authored zoom. The
            // final parity captures return Home to the full-map composition so all
            // independently placed buildings and road pieces remain visible.
            Vector2 constructionZoomAnchor =
                _map.ViewportPointForWorld(new CoreMapPoint(1600, 1000));
            await WheelAt(constructionZoomAnchor, MouseButton.WheelUp);
            await WheelAt(constructionZoomAnchor, MouseButton.WheelUp);
            Require(_map.ZoomIndex == 2,
                "표현 smoke가 실제 입력으로 2.25배 공사 보기를 열지 못했습니다.");

            await BuildCampaignSmokeSubstation(
                new CoreMapPoint(2200, 750),
                "표현 확인 동부 변전소");
            await BuildCampaignSmokeSubstation(
                new CoreMapPoint(1050, 1050),
                "표현 확인 중앙 변전소",
                Path.Combine(evidenceDirectory, "1920x1080-ui100-substation-draft.png"));
            Vector2 constructionAnchor = _map.ViewportPointForWorld(new CoreMapPoint(1600, 1000));
            Vector2 cameraBeforeConstructionPan = _map.CameraCenter;
            await MiddleDrag(
                constructionAnchor,
                constructionAnchor + new Vector2(390f, 260f));
            Require(
                _map.CameraCenter.X < cameraBeforeConstructionPan.X &&
                _map.CameraCenter.Y < cameraBeforeConstructionPan.Y,
                "확대된 작업 보기에서 서부 발전원 접속을 위한 actual-input camera 이동에 실패했습니다.");
            await BuildCampaignSmokeLine(
                "WEST_SOURCE",
                "PLAYER_SUBSTATION_2",
                [
                    new CoreMapPoint(800, 1000),
                    new CoreMapPoint(900, 950),
                ],
                "표현 확인 간선",
                Path.Combine(evidenceDirectory, "1920x1080-ui100-pole-draft.png"));
            _map.GrabFocus();
            await NextFrame();
            await PressKey(Key.Home);
            await NextFrame();
            await BuildCampaignSmokeLine(
                "PLAYER_SUBSTATION_2",
                "PLAYER_SUBSTATION_1",
                [
                    new CoreMapPoint(1350, 900),
                    new CoreMapPoint(1650, 850),
                    new CoreMapPoint(1950, 800),
                ],
                "표현 확인 중앙 연계선");
            Vector2 eastConstructionAnchor =
                _map.ViewportPointForWorld(new CoreMapPoint(2200, 800));
            await WheelAt(eastConstructionAnchor, MouseButton.WheelUp);
            await WheelAt(eastConstructionAnchor, MouseButton.WheelUp);
            Require(_map.ZoomIndex == 2,
                "동부 인입선 actual-input 공사 보기가 2.25배로 열리지 않았습니다.");
            await BuildCampaignSmokeLine(
                "PLAYER_SUBSTATION_1",
                "EAST_RESIDENTIAL_TERMINAL",
                [],
                "표현 확인 인입선");
            _map.GrabFocus();
            await NextFrame();
            await PressKey(Key.Home);
            await NextFrame();
            Vector2 presentationAnchor =
                _map.ViewportPointForWorld(new CoreMapPoint(1600, 1000));
            for (int attempt = 0; attempt < 3 && _map.ZoomIndex != 1; attempt++)
            {
                // Keep this actual pointer input. Native windows can discard the
                // first wheel event immediately after Home/focus transfer; a
                // bounded retry proves the same action without calling renderer
                // methods or signals directly.
                await WheelAt(presentationAnchor, MouseButton.WheelUp);
            }
            // Keep the fixed parity checkpoint on the denser eastern half while
            // retaining the western source. This is a real middle-button camera
            // gesture, and its center is recorded by the smoke evidence.
            await MiddleDrag(
                presentationAnchor,
                presentationAnchor + new Vector2(-65f, 32f));
            Require(
                _map.ZoomIndex == 1 &&
                Math.Abs(_map.CameraCenter.X - 1600f) < 12f &&
                _map.CameraCenter.Y is > 870f and < 920f,
                "고정 시각 판정 캡처가 actual-input 1.50배 프레젠테이션 보기에 진입하지 않았습니다: " +
                $"zoom={_map.ZoomIndex}, center={_map.CameraCenter.X:F1},{_map.CameraCenter.Y:F1}");
            ThermalIntervalResult active = _thermalSequence.Intervals[_thermalProjectionIndex];
            ThermalDemandResult selected = SelectedDemand(active)
                ?? throw new InvalidOperationException("선택할 수요가 없습니다.");
            Require(
                selected.Supplied &&
                selected.PathEdgeIds.Count > 0 &&
                _map.AccessibilityName.Contains("선택 수요 경로", StringComparison.Ordinal) &&
                _map.HasIndividualTileAssets &&
                _map.HasIndividualObjectAssets &&
                _map.IndividualArtAssetCount == 55 &&
                _map.AtomicCityAssetCount == 12 &&
                _map.AtomicRoadTileAssetCount == 6 &&
                _map.AtomicWorldInstanceCount == 641 &&
                _panel.AccessibilityName.Contains("최소 열여유", StringComparison.Ordinal) &&
                _supplyLabel.Text.Contains("필수 공급 · 1/1 ✓", StringComparison.Ordinal) &&
                _timeline.AccessibilityName.Contains("사건 흐름", StringComparison.Ordinal) &&
                _timeline.CurrentStepLabel.Contains("첫 입주 점등", StringComparison.Ordinal),
                "선택 수요의 발전원·전체 경로·열여유·시설 강조가 함께 갱신되지 않았습니다. " +
                $"tiles={_map.HasIndividualTileAssets}, objects={_map.HasIndividualObjectAssets}, " +
                $"art={_map.IndividualArtAssetCount}, atomic={_map.AtomicCityAssetCount}/" +
                $"{_map.AtomicRoadTileAssetCount}/{_map.AtomicWorldInstanceCount}, " +
                $"selected={selected.Supplied}/{selected.PathEdgeIds.Count}, " +
                $"map-path={_map.AccessibilityName.Contains("선택 수요 경로", StringComparison.Ordinal)}, " +
                $"panel-margin={_panel.AccessibilityName.Contains("최소 열여유", StringComparison.Ordinal)}, " +
                $"timeline-a11y={_timeline.AccessibilityName.Contains("사건 흐름", StringComparison.Ordinal)}, " +
                $"timeline={_timeline.CurrentStepLabel}, " +
                $"supply={_supplyLabel.Text}");
            SaveEvidencePng(Path.Combine(
                evidenceDirectory,
                "1920x1080-ui100-discrete-art-path.png"));
            _map.SetChapterIndexForPresentationSmoke(4);
            await NextFrame();
            SaveEvidencePng(Path.Combine(
                evidenceDirectory,
                "1920x1080-ui100-river-heat.png"));
            _map.SetChapterIndexForPresentationSmoke(5);
            await NextFrame();
            SaveEvidencePng(Path.Combine(
                evidenceDirectory,
                "1920x1080-ui100-river-flood.png"));
            _map.SetChapterIndexForPresentationSmoke(0);
            await NextFrame();

            await PressKey(Key.Escape);
            Require(_shell.Page == ReleaseShellPage.Pause,
                "Esc가 단일 shell overlay의 일시정지를 열지 않았습니다.");
            await PressShellAsync(ReleaseShellAction.PauseSettings, "설정 열기");
            OptionButton uiScale = _shell.GetUiScaleOption();
            uiScale.GrabFocus();
            await NextFrame();
            await PressKey(Key.Enter);
            await PressKey(Key.Down);
            await PressKey(Key.Enter);
            CheckButton reduceMotion = _shell.GetReduceMotionCheck();
            reduceMotion.GrabFocus();
            await NextFrame();
            await PressKey(Key.Enter);
            Require(
                _settings.UiScalePercent == 125 &&
                _settings.ReduceMotion &&
                CommercialSettingsPersistenceStore.Load(_settingsPath).Settings == _settings,
                "UI 125%와 ReduceMotion을 actual keyboard input 또는 settings v3에 적용하지 못했습니다.");
            await PressShellAsync(ReleaseShellAction.SettingsBack, "설정 닫기");
            await PressShellAsync(ReleaseShellAction.Resume, "게임 계속하기");
            await NextFrame();
            Label objectiveLabel = _panel.GetNode<Label>("%ObjectiveLabel");
            Label nextActionLabel = _panel.GetNode<Label>("%NextActionLabel");
            ScrollContainer infoScroll = _panel.GetNode<ScrollContainer>(
                "Margin/Column/InfoScroll");
            BaseButton primaryAction = _panel.GetActionButton(
                CommercialPanelAction.ApproveWindow);
            BaseButton recoveryAction = _panel.GetActionButton(
                CommercialPanelAction.RollbackProject);
            primaryAction.GrabFocus();
            await NextFrame();
            Require(
                GetWindow().Size == new Vector2I(1920, 1080) &&
                objectiveLabel.Visible && ControlInside(_panel, objectiveLabel) &&
                nextActionLabel.Visible && ControlInside(_panel, nextActionLabel) &&
                infoScroll.Visible && infoScroll.Size.Y > 0f &&
                ControlInside(_panel, infoScroll) &&
                primaryAction.Visible && ControlInside(_panel, primaryAction) &&
                primaryAction.HasFocus() &&
                recoveryAction.Visible && ControlInside(_panel, recoveryAction) &&
                _map.AccessibilityName.Contains("선택 수요 경로", StringComparison.Ordinal),
                "1920×1080·UI 125%에서 목표·다음 행동·본문·주 행동·복구 행동 또는 focus가 " +
                "화면 밖으로 잘렸습니다.");
            SaveEvidencePng(Path.Combine(evidenceDirectory, "1920x1080-ui125-path-reduce-motion.png"));

            await PressPanelAsync(
                CommercialPanelAction.ApproveWindow,
                "첫 불빛 동결 결과 확인");
            await NextFrame();
            Require(
                _presentationMode == CommercialPresentationMode.Result &&
                _map.OperationsLocked &&
                HeadingText() == "첫 불빛 · 결과" &&
                _panel.GetActionButton(CommercialPanelAction.ApproveWindow)
                    .AccessibilityName == "다음 임무" &&
                !_panel.GetActionButton(CommercialPanelAction.RollbackProject).Visible,
                "UI 125% 표현 smoke가 다음 장 상태와 분리된 첫 불빛 동결 결과를 열지 못했습니다.");
            SaveEvidencePng(Path.Combine(
                evidenceDirectory,
                "1920x1080-ui125-first-result.png"));

            await PressKey(Key.Escape);
            await PressShellAsync(ReleaseShellAction.SaveAndQuit, "저장하고 제목으로");
            Require(
                _shell.Page == ReleaseShellPage.Title &&
                _hasContinuation &&
                File.Exists(_savePath) &&
                !File.Exists(_savePath + ".tmp") &&
                !File.Exists(_settingsPath + ".tmp"),
                "Save & Quit이 성공한 원자적 저장 뒤에만 제목으로 이동하지 않았습니다.");
            await PressShellAsync(ReleaseShellAction.Continue, "저장 진행 재개 안내");
            await NextFrame();
            Require(
                _presentationMode == CommercialPresentationMode.ResumeOrientation &&
                _map.OperationsLocked &&
                _panel.AccessibilityName.Contains("동부 생활권 첫 점등", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("두 번째 심장", StringComparison.Ordinal) &&
                _panel.GetActionButton(CommercialPanelAction.ApproveWindow)
                    .AccessibilityName == "진행 재개",
                "저장 뒤 Continue가 직전 결과와 현재 장을 구분한 재개 안내를 열지 못했습니다.");
            SaveEvidencePng(Path.Combine(
                evidenceDirectory,
                "1920x1080-ui125-resume-orientation.png"));
            await PressKey(Key.Escape);
            await PressShellAsync(ReleaseShellAction.SaveAndQuit, "재개 안내에서 저장하고 제목으로");
            Require(_shell.Page == ReleaseShellPage.Title,
                "재개 안내 캡처 뒤 제목 화면으로 돌아오지 못했습니다.");
            GD.Print(
                "COMMERCIAL_STAGE_G_PRESENTATION_SMOKE_PASS " +
                "screens=title-ui100|briefing-ui100|operations-ui100|substation-draft-ui100|" +
                "pole-draft-ui100|art-path-ui100|river-heat-ui100|river-flood-ui100|" +
                "art-path-ui125|result-ui125|resume-ui125 " +
                $"visual=discrete-tiles-{_map.IndividualTileAssetCount}|" +
                $"discrete-objects-{_map.IndividualObjectAssetCount}|" +
                "planned-class-sprites|event-timeline " +
                "input=focus-keyboard reduce-motion=on " +
                "save-and-quit=atomic resume=orientation resolution=1920x1080 " +
                $"camera={_map.CameraCenter.X:0.0},{_map.CameraCenter.Y:0.0} " +
                $"buildIdentity={CommercialCoreSaveCodec.ComputeSha256(_buildIdentityBytes)}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"Stage-G 시청각·접근성 smoke 실패: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task PressShellAsync(ReleaseShellAction action, string description)
    {
        BaseButton button = _shell.GetActionButton(action);
        if (!button.Visible || button.Disabled)
        {
            throw new InvalidOperationException($"필요한 shell 행동을 사용할 수 없습니다: {description}");
        }
        button.GrabFocus();
        await NextFrame();
        Require(button.HasFocus(), $"shell 행동이 keyboard focus를 받지 못했습니다: {description}");
        await PressKey(Key.Enter);
    }

    private void SaveEvidencePng(string path)
    {
        Image image = GetViewport().GetTexture().GetImage();
        Godot.Error result = image.SavePng(path);
        if (result != Godot.Error.Ok)
        {
            throw new IOException($"화면 증거 PNG를 저장하지 못했습니다: {result}");
        }
    }

    private async Task CaptureG3FinalPair(string fileName)
    {
        string? directory = System.Environment.GetEnvironmentVariable(
            G3FinalCaptureDirectoryEnvironment);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        if (!Path.IsPathFullyQualified(directory))
        {
            throw new InvalidOperationException(
                $"{G3FinalCaptureDirectoryEnvironment}는 절대경로여야 합니다.");
        }
        Directory.CreateDirectory(directory);
        Vector2 previousCenter = _map.CameraCenter;
        int previousZoomIndex = _map.ZoomIndex;
        GetWindow().Size = new Vector2I(1920, 1080);
        ApplyRuntimeUiScale(this, 1f);
        _timeline.SetUiScale(1f);
        await NextFrame();
        _map.GrabFocus();
        await NextFrame();
        await PressKey(Key.Home);
        bool regionalSitingView = string.Equals(
            fileName,
            "pair-siting.png",
            StringComparison.Ordinal);
        bool regionalFloodView = string.Equals(
            fileName,
            "pair-flood.png",
            StringComparison.Ordinal);
        bool floodBaselineView = string.Equals(
            fileName,
            "pair-flood-baseline.png",
            StringComparison.Ordinal);
        bool routeComparisonView = string.Equals(
            fileName,
            "pair-route.png",
            StringComparison.Ordinal);
        bool heatCityView = string.Equals(
            fileName,
            "pair-heat.png",
            StringComparison.Ordinal);
        bool easternRiverView = string.Equals(
            fileName,
            "pair-normal.png",
            StringComparison.Ordinal);
        bool fullRegionalView = routeComparisonView || regionalSitingView ||
            regionalFloodView || floodBaselineView;
        if (!fullRegionalView)
        {
            Vector2 anchor = _map.ViewportPointForWorld(new CoreMapPoint(1600, 1000));
            await WheelAt(anchor, MouseButton.WheelUp);
            await MiddleDrag(
                anchor,
                anchor + (routeComparisonView
                    ? new Vector2(-280f, -129f)
                    : heatCityView
                        ? new Vector2(-193f, -43f)
                    : easternRiverView
                        ? new Vector2(-133f, -13f)
                        : new Vector2(-65f, 32f)));
        }
        Require(
            GetWindow().Size == new Vector2I(1920, 1080) &&
            _map.ZoomIndex == (fullRegionalView ? 0 : 1) &&
            (routeComparisonView
                ? Math.Abs(_map.CameraCenter.X - 1600f) < 12f
                : heatCityView
                    ? _map.CameraCenter.X is > 1840f and < 1940f
                : easternRiverView
                    ? _map.CameraCenter.X is > 1720f and < 1780f
                    : Math.Abs(_map.CameraCenter.X - 1600f) < 12f) &&
            (fullRegionalView
                ? Math.Abs(_map.CameraCenter.Y - 1000f) < 12f
                : routeComparisonView
                    ? _map.CameraCenter.Y is > 960f and < 1040f
                : heatCityView
                    ? _map.CameraCenter.Y is > 880f and < 940f
                : _map.CameraCenter.Y is > 890f and < 920f),
            $"G.3 final pair {fileName}의 1920×1080·UI 100% fixed camera가 다릅니다. " +
            $"zoom={_map.ZoomIndex}/{_map.ZoomLabel}, center={_map.CameraCenter}");
        await NextFrame();
        SaveEvidencePng(Path.Combine(directory, fileName));

        // Restore the exact pre-capture navigation state so evidence capture has
        // no effect on the subsequent actual-input campaign regression.
        ApplyRuntimeUiScale(this, 1.25f);
        _timeline.SetUiScale(1.25f);
        await NextFrame();
        _map.SetCameraForSmoke(previousCenter, previousZoomIndex);
        await NextFrame();
    }

    private async void RunPlacementSmoke()
    {
        try
        {
            await NextFrame();
            int authoredNodeCount = _snapshot.World.Nodes.Count;
            int authoredEdgeCount = _snapshot.World.Edges.Count;
            GetWindow().Size = new Vector2I(1920, 1080);
            await NextFrame();
            CoreMapPoint resolutionPoint = new(913, 711);
            CommercialWorldPosition highResolutionRoundTrip = _map.WorldAtViewportPoint(
                _map.ViewportPointForWorld(resolutionPoint));
            Require(
                NearlyEqual(highResolutionRoundTrip.X, resolutionPoint.XUnit, 0.02d) &&
                NearlyEqual(highResolutionRoundTrip.Y, resolutionPoint.YUnit, 0.02d),
                "1920×1080 지도 변환이 같은 자유 좌표를 왕복하지 못했습니다.");
            GetWindow().Size = new Vector2I(1920, 1080);
            await NextFrame();
            ApplyUiScale(this, 1.25f);
            await NextFrame();
            Require(
                GetWindow().Size == new Vector2I(1920, 1080) &&
                ControlInside(_panel, _panel.GetActionButton(CommercialPanelAction.Commission)),
                "1920×1080·UI 125%에서 핵심 공사 행동이 패널 밖으로 잘렸습니다.");

            await PressPanelAsync(CommercialPanelAction.PlaceSubstation, "변전소 도구");
            await NextFrame();
            CoreMapPoint fractional = new(613, 327);
            await ClickMap(fractional);
            Require(_snapshot.NodeDraft?.Position == fractional,
                "설계 단위 사이의 자유 좌표가 지도 입력에서 그대로 유지되지 않았습니다.");
            await PressKey(Key.Escape);
            Require(_snapshot.Phase == ConstructionPhase.Ready && _snapshot.NodeDraft is null,
                "Esc가 작성 중인 변전소 계획을 먼저 취소하지 않았습니다.");

            await PressPanelAsync(CommercialPanelAction.PlaceSubstation, "변전소 도구 다시 선택");
            await NextFrame();
            await RequireRejectedPlacement(
                new CoreMapPoint(1300, 900),
                ConstructionError.WaterFootprint,
                "수면 배치");
            await RequireRejectedPlacement(
                new CoreMapPoint(2580, 600),
                ConstructionError.BuildingFootprint,
                "건물 경계 접촉 배치");

            CoreMapPoint riskPoint = new(1900, 1850);
            await MovePointer(riskPoint);
            Require(
                _pointerAccepted && _pointerRiskAreaIds.Contains("RIVER_FLOOD_ZONE", StringComparer.Ordinal) &&
                _pointerMessage.Contains("주의", StringComparison.Ordinal),
                "허용되는 위험구역 배치를 차단하지 않으면서 경고하지 못했습니다.");
            _tool = CommercialTool.None;
            RefreshPointerPreview();
            Render();

            CoreMapPoint mapCenter = new(1600, 1000);
            Vector2 anchor = _map.ViewportPointForWorld(mapCenter);
            CommercialWorldPosition beforeZoom = _map.WorldAtViewportPoint(anchor);
            await WheelAt(anchor, MouseButton.WheelUp);
            await WheelAt(anchor, MouseButton.WheelUp);
            CommercialWorldPosition afterZoom = _map.WorldAtViewportPoint(anchor);
            Require(
                _map.ZoomIndex == 2 &&
                NearlyEqual(beforeZoom.X, afterZoom.X, 0.02d) &&
                NearlyEqual(beforeZoom.Y, afterZoom.Y, 0.02d),
                "세 단계 확대가 포인터 아래 세계좌표를 유지하지 못했습니다.");

            ConstructionSnapshot beforePan = _legacySession!.GetSnapshot();
            Vector2 cameraBeforePan = _map.CameraCenter;
            await MiddleDrag(anchor, anchor + new Vector2(90f, 35f));
            Require(
                !_map.CameraCenter.IsEqualApprox(cameraBeforePan) &&
                Equals(_legacySession.GetSnapshot(), beforePan),
                "지도 이동이 카메라만 바꾸지 않았거나 Core 상태를 변경했습니다.");
            await PressMapKey(Key.Home);
            Require(
                _map.ZoomIndex == 0 &&
                _map.CameraCenter.IsEqualApprox(new Vector2(1600f, 1000f)),
                "Home이 전체 보기와 지도 중심을 복원하지 못했습니다.");

            await PressPanelAsync(CommercialPanelAction.StartLine, "접속 후보 확인용 선로 도구");
            await NextFrame();
            CoreMapPoint ambiguousSourcePointer = new(763, 1013);
            await MovePointer(ambiguousSourcePointer);
            Require(
                _map.CandidateNodeIds.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(
                    new[] { "WEST_AUXILIARY", "WEST_SOURCE" },
                    StringComparer.Ordinal),
                "화면 거리와 node ID 순서의 접속 후보가 안정적으로 만들어지지 않았습니다: " +
                string.Join(",", _map.CandidateNodeIds) +
                $" pointer={_pointerPoint} sources=" +
                string.Join(";", _snapshot.World.Nodes.Where(item =>
                    item.NodeId is "WEST_SOURCE" or "WEST_AUXILIARY").Select(item =>
                    $"{item.NodeId}:{item.Position}:screenDistance=" +
                    $"{_map.ViewportPointForWorld(item.Position).DistanceTo(_map.ViewportPointForWorld(ambiguousSourcePointer)):F2}")));
            string firstCandidate = _map.SelectedCandidateId!;
            await PressMapKey(Key.Q, physical: Key.Q);
            string cycledCandidate = _map.SelectedCandidateId!;
            await PressMapKey(Key.E, physical: Key.E);
            Require(
                firstCandidate != cycledCandidate &&
                _map.SelectedCandidateId == firstCandidate,
                "물리 Q/E 입력이 접속 후보 ID를 순환하지 못했습니다.");

            _map.GrabFocus();
            await PressMapKey(Key.Plus);
            await PressMapKey(Key.Plus);
            Vector2 keyboardCameraStart = _map.CameraCenter;
            for (int index = 0; index < 5; index++)
            {
                await PressMapKey(Key.Right, shift: true);
            }
            Require(
                _map.KeyboardPoint.XUnit == 3200 &&
                !_map.CameraCenter.IsEqualApprox(keyboardCameraStart),
                "Shift+방향키 자유 커서가 이동하거나 화면 가장자리에서 카메라를 따라오게 하지 못했습니다.");
            await PressMapKey(Key.Tab);
            Control? focusAfterTab = GetViewport().GuiGetFocusOwner();
            Require(focusAfterTab is not null && focusAfterTab != _map,
                "지도에서 Tab 표준 focus 이동을 가로챘습니다.");
            _map.GrabFocus();
            await PressMapKey(Key.Home);

            CoreMapPoint[] exactPath =
            [
                new(650, 900),
                new(1050, 850),
                new(1610, 850),
                new(2100, 1150),
            ];
            await SelectAndClickCandidate(new CoreMapPoint(675, 1100), "WEST_SOURCE");
            Require(_snapshot.LineDraft?.StartNodeId == "WEST_SOURCE",
                "선택한 서부 발전 접속점에서 선로 계획을 시작하지 못했습니다.");
            await ClickMap(exactPath[0]);
            Require(_snapshot.LineDraft?.IntermediatePoints.SequenceEqual(exactPath[..1]) == true,
                $"첫 전신주 계획을 추가하지 못했습니다: {_lastError}");
            await ClickMap(exactPath[1]);
            Require(_snapshot.LineDraft?.IntermediatePoints.SequenceEqual(exactPath[..2]) == true,
                "두 번째 전신주 계획을 추가하지 못했습니다: " +
                $"points={string.Join(";", _snapshot.LineDraft?.IntermediatePoints ?? Array.Empty<CoreMapPoint>())} " +
                $"pointer={_pointerPoint} candidate={_candidateNodeId ?? "없음"} " +
                $"drag={_map.IsDraggingDraftPoint} error={_lastError}");
            CoreMapPoint dragged = new(1047, 777);
            await DragMap(exactPath[1], dragged);
            Require(_snapshot.LineDraft?.IntermediatePoints[^1] == dragged,
                "마지막 작성 전신주를 실제 드래그 입력으로 옮기지 못했습니다.");
            await PressMapKey(Key.Backspace);
            Require(_snapshot.LineDraft?.IntermediatePoints.Count == 1,
                "Backspace가 마지막 작성 전신주를 되돌리지 못했습니다.");
            await ClickMap(exactPath[1]);
            Require(_snapshot.LineDraft?.IntermediatePoints.SequenceEqual(exactPath[..2]) == true,
                $"되돌린 두 번째 전신주를 다시 추가하지 못했습니다: {_lastError}");
            await ClickMap(exactPath[2]);
            Require(_snapshot.LineDraft?.IntermediatePoints.SequenceEqual(exactPath[..3]) == true,
                $"강 동쪽 전신주 계획을 추가하지 못했습니다: {_lastError}");
            await ClickMap(exactPath[3]);
            Require(_snapshot.LineDraft?.IntermediatePoints.SequenceEqual(exactPath) == true,
                $"마지막 전신주 계획을 추가하지 못했습니다: {_lastError}");
            await SelectAndClickCandidate(
                new CoreMapPoint(2350, 800),
                "EAST_RESIDENTIAL_TERMINAL");
            Require(
                _snapshot.LineDraft?.EndNodeId == "EAST_RESIDENTIAL_TERMINAL",
                "동부 생활권 접속점을 선로 끝점으로 확정하지 못했습니다. " +
                $"candidate={_map.SelectedCandidateId ?? "없음"} error={_lastError}");
            CoreMapPoint movedNonLast = new(1047, 777);
            await DragMap(exactPath[1], movedNonLast);
            Require(
                _snapshot.LineDraft?.IntermediatePoints[1] == movedNonLast &&
                _snapshot.LineDraft.EndNodeId == "EAST_RESIDENTIAL_TERMINAL",
                "끝 접속점을 정한 뒤 중간 전신주를 실제 드래그 입력으로 옮기지 못했습니다. " +
                $"points={string.Join(';', _snapshot.LineDraft?.IntermediatePoints ?? Array.Empty<CoreMapPoint>())} " +
                $"end={_snapshot.LineDraft?.EndNodeId ?? "없음"} error={_lastError}");
            await DragMap(movedNonLast, exactPath[1]);
            Require(
                _snapshot.LineDraft is not null &&
                _snapshot.LineDraft.IntermediatePoints.SequenceEqual(exactPath) &&
                _snapshot.LineDraft.EndNodeId == "EAST_RESIDENTIAL_TERMINAL",
                "강을 건너는 선로 계획의 정확한 자유 좌표가 유지되지 않았습니다. " +
                $"points={string.Join(';', _snapshot.LineDraft?.IntermediatePoints ?? Array.Empty<CoreMapPoint>())} " +
                $"end={_snapshot.LineDraft?.EndNodeId ?? "없음"} error={_lastError}");
            await PressPanelAsync(CommercialPanelAction.Commission, "선로 공사 발주");
            await NextFrame();
            await PressPanelAsync(CommercialPanelAction.Commission, "선로 공사 완공");
            await NextFrame();
            CoreMapPoint[] commissionedPositions = _snapshot.World.Nodes
                .Where(node => node.NodeId.StartsWith("PLAYER_POLE_", StringComparison.Ordinal))
                .OrderBy(node => node.NodeId, StringComparer.Ordinal)
                .Select(node => node.Position)
                .ToArray();
            Require(
                _snapshot.Phase == ConstructionPhase.Ready &&
                commissionedPositions.SequenceEqual(exactPath) &&
                _snapshot.World.Nodes.Count == authoredNodeCount + exactPath.Length &&
                _snapshot.World.Edges.Count(edge => edge.Commissioned) ==
                    authoredEdgeCount + exactPath.Length + 1,
                "강을 건너는 한 선로가 정확한 위치로 완공되지 않았습니다.");

            GD.Print(
                $"COMMERCIAL_PLACEMENT_SMOKE_PASS minute={_snapshot.Minute} " +
                $"nodes={_snapshot.World.Nodes.Count} edges={_snapshot.World.Edges.Count} " +
                $"zoom={_map.ZoomLabel}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"상용 자유 배치 smoke 실패: {exception}");
            GetTree().Quit(1);
        }
    }

    private async void RunThermalSmoke()
    {
        try
        {
            await NextFrame();
            GetWindow().Size = new Vector2I(1920, 1080);
            await NextFrame();
            ApplyUiScale(this, 1.25f);
            await NextFrame();
            Require(ControlInside(_panel, _panel.GetActionButton(CommercialPanelAction.NextThermalPhase)),
                "1920×1080·UI 125%에서 열 국면 전환 행동이 패널 밖으로 잘렸습니다.");

            ThermalIntervalResult hot = _thermalSequence.Intervals[0];
            ThermalAssetResult hotSubstation = hot.Assets.Single(item =>
                item.AssetId == "NORTH_SUBSTATION");
            Require(
                hotSubstation.UseKw == 3900 &&
                hotSubstation.ContinuousLimitKw == 4000 &&
                hotSubstation.EmergencyLimitKw == 5200,
                "선택 변전소의 사용·연속·비상 한계가 typed 결과와 다릅니다.");
            Require(
                hot.Assets.Any(item => item.CurrentState == ThermalOperatingState.Emergency) &&
                _panel.AccessibilityName.Contains("열", StringComparison.Ordinal),
                "비상 운전 상태를 색 외 문장으로 노출하지 못했습니다.");

            await PressPanelAsync(CommercialPanelAction.NextThermalPhase, "보호정지 국면 전환");
            await NextFrame();
            Require(
                _thermalProjectionIndex == 1 &&
                _thermalSequence.Intervals[1].Assets.Any(item =>
                    item.CurrentState == ThermalOperatingState.ProtectiveOutage) &&
                _map.AccessibilityName.Contains("보호정지", StringComparison.Ordinal),
                "다음 국면의 보호정지 overlay와 접근성 문장이 함께 바뀌지 않았습니다.");

            await PressPanelAsync(CommercialPanelAction.NextThermalPhase, "복귀 국면 전환");
            await NextFrame();
            Require(
                _thermalProjectionIndex == 2 &&
                _thermalSequence.Intervals[2].Assets.All(item =>
                    item.CurrentState != ThermalOperatingState.ProtectiveOutage),
                "한 국면 냉각 뒤 자동 복귀가 projection에 나타나지 않았습니다.");

            _tool = CommercialTool.None;
            await SelectAndClickCandidate(
                new CoreMapPoint(2100, 1350),
                "SOUTH_SUBSTATION");
            Require(
                _selectedThermalAssetId == "SOUTH_SUBSTATION" &&
                _panel.AccessibilityName.Contains("열", StringComparison.Ordinal),
                "지도 선택으로 열 설비 상세를 바꾸지 못했습니다.");

            GD.Print(
                "COMMERCIAL_THERMAL_SMOKE_PASS " +
                $"phases={_thermalSequence.Intervals.Count} selected={_selectedThermalAssetId} " +
                "patterns=continuous|emergency|protective-outage");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"상용 열 국면 smoke 실패: {exception}");
            GetTree().Quit(1);
        }
    }

    private async void RunCampaignSmoke()
    {
        try
        {
            await NextFrame();
            CommercialCoreRun coreRun = _coreRun
                ?? throw new InvalidOperationException("상용 핵심 흐름 runner가 없습니다.");
            GetWindow().Size = new Vector2I(1920, 1080);
            await NextFrame();
            ApplyUiScale(this, 1.25f);
            await NextFrame();
            Require(
                coreRun.GetSnapshot().Chapter.ChapterId == "FIRST_LIGHT" &&
                _presentationMode == CommercialPresentationMode.Briefing &&
                _map.OperationsLocked &&
                _audio.GetChildCount() == 4 &&
                !_panel.GetActionButton(CommercialPanelAction.KeepPromise).Visible &&
                !_panel.GetActionButton(CommercialPanelAction.StartLine).Visible &&
                _panel.AccessibilityName.Contains("현재 의무", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("조작 ·", StringComparison.Ordinal) &&
                _map.AccessibilityName.Contains("예정 시설 4곳", StringComparison.Ordinal) &&
                _map.HasIndividualTileAssets &&
                _map.HasIndividualObjectAssets &&
                _map.IndividualArtAssetCount == 55 &&
                _map.AtomicCityAssetCount == 12 &&
                _map.AtomicRoadTileAssetCount == 6 &&
                _map.AtomicWorldInstanceCount == 641 &&
                _timeline.StepCount == 4 &&
                _timeline.CurrentStepLabel == "브리핑" &&
                _timeline.AccessibilityName.Contains("시간을 진행하지 않습니다", StringComparison.Ordinal) &&
                _panel.GetActionButton(CommercialPanelAction.ApproveWindow)
                    .AccessibilityName == "임무 시작" &&
                ControlInside(_panel, _panel.GetActionButton(CommercialPanelAction.ApproveWindow)) &&
                ControlInside(_panel, _panel.GetActionButton(CommercialPanelAction.RollbackProject)) &&
                !_panel.GetActionButton(CommercialPanelAction.RestartChapter).Visible &&
                !_panel.GetActionButton(CommercialPanelAction.NewGame).Visible,
                "1920×1080·UI 125%에서 상용 캠페인·오디오와 고정 행동 영역을 열지 못했습니다. " +
                $"chapter={coreRun.GetSnapshot().Chapter.ChapterId}, audio={_audio.GetChildCount()}, " +
                $"art={_map.IndividualArtAssetCount}, atomic={_map.AtomicCityAssetCount}/" +
                $"{_map.AtomicRoadTileAssetCount}/{_map.AtomicWorldInstanceCount}, " +
                $"mode={_presentationMode}, timeline={_timeline.StepCount}/{_timeline.CurrentStepLabel}, " +
                $"approveInside={ControlInside(_panel, _panel.GetActionButton(CommercialPanelAction.ApproveWindow))}, " +
                $"rollbackInside={ControlInside(_panel, _panel.GetActionButton(CommercialPanelAction.RollbackProject))}");

            bool initialSaveWritable = _saveWritable;
            _saveWritable = false;
            Require(
                !PersistCoreRun() &&
                _panel.HasVisibleError &&
                _panel.AccessibilityName.Contains("저장할 수 없습니다", StringComparison.Ordinal),
                "잠긴 Briefing에서 자동저장 실패를 상단 시각 경고와 assertive 접근성으로 알리지 않았습니다.");
            _saveWritable = initialSaveWritable;
            _lastError = string.Empty;
            Render();

            if (_options.CampaignSmoke)
            {
                string recoveryDirectory = Path.Combine(
                    Path.GetTempPath(),
                    $"gridworks-stage-e-native-recovery-{Guid.NewGuid():N}");
                Directory.CreateDirectory(recoveryDirectory);
                string incompatiblePath = Path.Combine(
                    recoveryDirectory,
                    CommercialCampaignPersistenceStore.SaveFileName);
                byte[] incompatibleBytes = [0xff, 0x00, 0x7f];
                File.WriteAllBytes(incompatiblePath, incompatibleBytes);
                _savePath = incompatiblePath;
                _saveWritable = false;
                _incompatibleSavePending = true;
                _shell.ShowTitle(false, "비호환 저장 복구 확인");
                await PressShellAsync(ReleaseShellAction.NewGame, "비호환 저장 뒤 새 게임");
                if (_shell.Page == ReleaseShellPage.Help)
                {
                    await PressShellAsync(
                        ReleaseShellAction.HelpBack,
                        "비호환 저장 뒤 조작 도움말 닫기");
                }
                await NextFrame();
                coreRun = _coreRun!;
                string[] preserved = Directory.GetFiles(
                    recoveryDirectory,
                    "*.incompatible*.json");
                Require(
                    _saveWritable && !_incompatibleSavePending &&
                    CommercialCampaignPersistenceStore.Load(incompatiblePath).Status ==
                        CommercialCoreDocumentLoadStatus.Loaded &&
                    preserved.Length == 1 &&
                    File.ReadAllBytes(preserved[0]).SequenceEqual(incompatibleBytes),
                    "비호환 저장을 보존한 뒤 실제 새 게임 입력으로 쓰기 가능한 저장을 열지 못했습니다.");
                Directory.Delete(recoveryDirectory, recursive: true);
                _savePath = null;
            }

            await AdvancePresentationToOperations("첫 불빛 단계 안내");
            Require(
                _panel.AccessibilityName.Contains("변전소 놓기(2)", StringComparison.Ordinal),
                "첫 불빛 운영 시작이 변전소 배치라는 첫 행동을 고정 안내하지 않았습니다.");
            await BuildCampaignSmokeSubstation(
                new CoreMapPoint(2200, 750),
                "첫 불빛 변전소");
            Require(
                _panel.AccessibilityName.Contains(
                    "서부 발전 접속점에서 새 변전소",
                    StringComparison.Ordinal),
                "첫 불빛 변전소 완공 뒤 발전원 연결이라는 다음 행동으로 바뀌지 않았습니다.");
            await BuildCampaignSmokeLine(
                "WEST_SOURCE",
                "PLAYER_SUBSTATION_1",
                [
                    new CoreMapPoint(650, 700),
                    new CoreMapPoint(950, 500),
                    new CoreMapPoint(1545, 450),
                    new CoreMapPoint(1900, 600),
                ],
                "첫 불빛 간선");
            Require(
                _panel.AccessibilityName.Contains(
                    "동부 생활권 접속점까지 인입선",
                    StringComparison.Ordinal),
                "첫 불빛 발전원 연결 뒤 생활권 인입이라는 다음 행동으로 바뀌지 않았습니다.");
            await BuildCampaignSmokeLine(
                "PLAYER_SUBSTATION_1",
                "EAST_RESIDENTIAL_TERMINAL",
                Array.Empty<CoreMapPoint>(),
                "첫 불빛 인입선");
            Require(_thermalSequence.Intervals.SelectMany(item => item.Assets).Any(item =>
                    item.AssetId == "PLAYER_EDGE_6") &&
                _panel.AccessibilityName.Contains("공급할 수 있습니다", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("필수 공급 1/1 ✓", StringComparison.Ordinal),
                "예약 시설이 남은 임무에서 완공 선로의 열·경로 projection을 즉시 갱신하지 못했습니다.");
            CommercialDecisionPreview firstPreview = coreRun.PreviewDecisionWindow();
            Require(firstPreview.Accepted && firstPreview.ProjectedMinute <= 800,
                "첫 불빛 운영안이 안전 의무와 기한을 만족하지 못했습니다.");
            CommercialCoreSnapshot firstResultCore = coreRun.GetSnapshot();
            ConstructionSnapshot firstResultConstruction = _snapshot;
            ThermalSequenceResult firstResultThermal = _thermalSequence;
            int firstResultThermalIndex = _thermalProjectionIndex;
            string firstResultCash = _cashLabel.Text;
            string firstResultMap = _map.AccessibilityName;
            await CaptureG3FinalPair("pair-normal.png");
            // The flood comparison needs a genuine before-state of the same
            // persistent world at the same regional Home camera. Keep this as a
            // separate actual-input capture instead of reusing the closer normal
            // hero shot, which would make camera change look like state change.
            await CaptureG3FinalPair("pair-flood-baseline.png");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "첫 불빛 운영 승인");
            await NextFrame();
            Require(
                coreRun.GetSnapshot().Chapter.ChapterId == "SECOND_HEART" &&
                coreRun.GetSnapshot().ChapterResults.Count == 1 &&
                _presentationMode == CommercialPresentationMode.Result &&
                _map.OperationsLocked &&
                ReferenceEquals(_snapshot, firstResultConstruction) &&
                ReferenceEquals(_thermalSequence, firstResultThermal) &&
                _thermalProjectionIndex == firstResultThermalIndex &&
                _frozenResult?.CoreSnapshot.Chapter.ChapterId ==
                    firstResultCore.Chapter.ChapterId &&
                _frozenResult.CoreSnapshot.CommandCount == firstResultCore.CommandCount &&
                _frozenResult.CashUnit == firstResultCore.CashUnit &&
                HeadingText() == "첫 불빛 · 결과" &&
                _cashLabel.Text == firstResultCash &&
                _map.AccessibilityName.Contains("예정 시설 4곳", StringComparison.Ordinal) &&
                firstResultMap.Contains("예정 시설 4곳", StringComparison.Ordinal) &&
                _panel.GetActionButton(CommercialPanelAction.ApproveWindow)
                    .AccessibilityName == "다음 임무" &&
                !_panel.GetActionButton(CommercialPanelAction.StartLine).Visible &&
                !_panel.GetActionButton(CommercialPanelAction.RollbackProject).Visible &&
                !_panel.GetActionButton(CommercialPanelAction.NextThermalPhase).Visible &&
                !_panel.GetActionButton(CommercialPanelAction.NextDemand).Visible &&
                _timeline.CurrentStepLabel == "결과" &&
                _timeline.AccessibilityName.Contains("동부 생활권 첫 점등", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("동부 생활권 첫 점등", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("실제 경로", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("서부 발전 접속점", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("동부 생활권", StringComparison.Ordinal),
                "첫 불빛의 현금·열 projection·지도를 동결한 결과로 제시하지 못했습니다. " +
                $"mode={_presentationMode}, locked={_map.OperationsLocked}, " +
                $"snapshot={ReferenceEquals(_snapshot, firstResultConstruction)}, " +
                $"thermal={ReferenceEquals(_thermalSequence, firstResultThermal)}/" +
                $"{_thermalProjectionIndex}/{firstResultThermalIndex}, " +
                $"frozen={_frozenResult?.CoreSnapshot.Chapter.ChapterId}/" +
                $"{_frozenResult?.CoreSnapshot.CommandCount}/{firstResultCore.CommandCount}, " +
                $"cash={_cashLabel.Text}/{firstResultCash}, heading={HeadingText()}, " +
                $"map={_map.AccessibilityName}, action=" +
                $"{_panel.GetActionButton(CommercialPanelAction.ApproveWindow).AccessibilityName}, " +
                $"timeline={_timeline.CurrentStepLabel}, panel={_panel.AccessibilityName}");

            _helpButton.GrabFocus();
            await NextFrame();
            await PressKey(Key.Enter);
            Require(
                _shell.Page == ReleaseShellPage.Pause &&
                _shell.GetActionButton(ReleaseShellAction.RestartChapter).Disabled &&
                _shell.GetActionButton(ReleaseShellAction.RewindPreviousChapter).Disabled &&
                _presentationMode == CommercialPresentationMode.Result &&
                ReferenceEquals(_snapshot, firstResultConstruction),
                "동결된 Result의 메뉴가 다음 장을 현재 임무로 노출하거나 재시작 우회를 허용했습니다.");
            await PressShellAsync(ReleaseShellAction.Resume, "첫 결과 메뉴 닫기");

            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "두 번째 심장 브리핑 열기");
            await NextFrame();
            Require(
                _presentationMode == CommercialPresentationMode.Briefing &&
                _map.OperationsLocked &&
                HeadingText() == "두 번째 심장 · 브리핑" &&
                _panel.AccessibilityName.Contains("두 번째 심장", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains(
                    coreRun.GetSnapshot().Chapter.Briefing.Title,
                    StringComparison.Ordinal) &&
                _timeline.CurrentStepLabel == "브리핑" &&
                _panel.GetActionButton(CommercialPanelAction.ApproveWindow)
                    .AccessibilityName == "임무 시작",
                "첫 결과 뒤 다음 장 상태를 섞지 않은 명시적 두 번째 심장 브리핑을 열지 못했습니다.");

            await BuildCampaignSmokeLine(
                "PLAYER_SUBSTATION_1",
                "HOSPITAL_TERMINAL",
                [new CoreMapPoint(2000, 1100)],
                "의료원 북안 회랑");
            await BuildCampaignSmokeSubstation(
                new CoreMapPoint(2100, 1450),
                "의료원 남안 변전소");
            await BuildCampaignSmokeLine(
                "WEST_SOURCE",
                "PLAYER_SUBSTATION_2",
                [
                    new CoreMapPoint(550, 1150),
                    new CoreMapPoint(950, 1450),
                    new CoreMapPoint(1170, 1750),
                    new CoreMapPoint(1760, 1750),
                    new CoreMapPoint(2050, 1650),
                ],
                "의료원 강변 회랑");
            await BuildCampaignSmokeLine(
                "PLAYER_SUBSTATION_2",
                "HOSPITAL_TERMINAL",
                Array.Empty<CoreMapPoint>(),
                "의료원 남안 인입선");
            CommercialDecisionPreview heartPreview = coreRun.PreviewDecisionWindow();
            Require(
                heartPreview.Accepted && heartPreview.PhaseResults.Count == 2 &&
                heartPreview.PhaseResults.All(item => item.Demands[0].Supplied) &&
                !heartPreview.PhaseResults[0].Demands[0].PathEdgeIds.SequenceEqual(
                    heartPreview.PhaseResults[1].Demands[0].PathEdgeIds,
                    StringComparer.Ordinal),
                "두 차단시험이 서로 다른 생존 회랑을 사용하지 못했습니다.");
            await CaptureG3FinalPair("pair-route.png");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "의료원 차단시험 승인");
            await NextFrame();
            Require(
                coreRun.GetSnapshot().Chapter.ChapterId == "SECOND_SOURCE" &&
                _presentationMode == CommercialPresentationMode.Result &&
                !_panel.GetActionButton(CommercialPanelAction.KeepPromise).Visible &&
                !_panel.AccessibilityName.Contains("조작 ·", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("수술실 전환시험 완료", StringComparison.Ordinal),
                "두 번째 심장 결과와 다음 임무 전환을 제시하지 못했습니다.");

            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "두 번째 전원 브리핑 열기");
            await NextFrame();
            Require(
                _presentationMode == CommercialPresentationMode.Briefing &&
                _panel.AccessibilityName.Contains(
                    coreRun.GetSnapshot().Chapter.Briefing.Title,
                    StringComparison.Ordinal) &&
                !_panel.AccessibilityName.Contains(
                    coreRun.GetSnapshot().DecisionWindow!.Story!.Title,
                    StringComparison.Ordinal),
                "첫 경계 이야기가 두 번째 전원 브리핑을 다시 대체했습니다.");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "두 번째 전원 첫 운영 이야기");
            await NextFrame();
            Require(
                _presentationMode == CommercialPresentationMode.WindowStory &&
                _map.OperationsLocked &&
                _panel.AccessibilityName.Contains(
                    coreRun.GetSnapshot().DecisionWindow!.Story!.Title,
                    StringComparison.Ordinal) &&
                _panel.GetActionButton(CommercialPanelAction.ApproveWindow)
                    .AccessibilityName == "운영 시작" &&
                !_panel.GetActionButton(CommercialPanelAction.StartLine).Visible,
                "첫 경계의 authored 이야기를 별도 잠긴 WindowStory mode로 열지 못했습니다.");

            await BuildCampaignSmokeLine(
                "WEST_AUXILIARY",
                "PLAYER_POLE_1",
                Array.Empty<CoreMapPoint>(),
                "남부 전원 생활권 연계");
            await BuildCampaignSmokeLine(
                "WEST_AUXILIARY",
                "PLAYER_POLE_6",
                [new CoreMapPoint(900, 1200)],
                "남부 전원 의료원 연계");
            CommercialDecisionPreview sourcePreview = coreRun.PreviewDecisionWindow();
            Require(sourcePreview.Accepted &&
                sourcePreview.PhaseResults[0].Demands.All(item =>
                    item.Supplied && item.SourceNodeId == "WEST_AUXILIARY"),
                "서부 전원 인수시험이 남부 발전 접속점의 실제 경로를 사용하지 못했습니다.");
            await CaptureG3FinalPair("pair-siting.png");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "서부 주간선 인수시험 승인");
            await NextFrame();
            Require(
                coreRun.GetSnapshot().Chapter.ChapterId == "NORTH_BANK_PROMISE" &&
                _presentationMode == CommercialPresentationMode.Result &&
                !_panel.GetActionButton(CommercialPanelAction.KeepPromise).Visible &&
                _panel.AccessibilityName.Contains("운영 인수 완료", StringComparison.Ordinal),
                "두 번째 전원 결과와 본편 전환을 제시하지 못했습니다.");

            await AdvancePresentationToOperations("북안의 약속");
            Require(
                _panel.GetActionButton(CommercialPanelAction.KeepPromise).Visible &&
                _panel.AccessibilityName.Contains("선택 필요", StringComparison.Ordinal) &&
                _map.AccessibilityName.Contains("예정 시설 1곳", StringComparison.Ordinal),
                "북안의 약속 브리핑 뒤 운영 선택을 열지 못했습니다.");
            await PressPanelAsync(CommercialPanelAction.KeepPromise, "북안 입주 약속 지킴");
            await NextFrame();
            await BuildCampaignSmokeLine(
                "PLAYER_SUBSTATION_1",
                "WATER_TERMINAL",
                Array.Empty<CoreMapPoint>(),
                "정수장 분기");
            int completedEdgeCount = coreRun.GetSnapshot().Construction.World.Edges.Count;
            await PressPanelAsync(CommercialPanelAction.RollbackProject, "정수장 최근 공사 복구");
            await NextFrame();
            Require(
                coreRun.GetSnapshot().PromiseDecision == PromiseDecision.Keep &&
                coreRun.GetSnapshot().Construction.World.Edges.Count == completedEdgeCount - 1 &&
                coreRun.GetSnapshot().ChapterResults.Count == 3,
                "최근 공사 복구가 이전 임무 망·결과와 현재 약속을 보존하지 못했습니다.");
            await BuildCampaignSmokeLine(
                "PLAYER_SUBSTATION_1",
                "WATER_TERMINAL",
                Array.Empty<CoreMapPoint>(),
                "복구 뒤 정수장 분기");
            CommercialDecisionPreview finalPreview = coreRun.PreviewDecisionWindow();
            Require(finalPreview.Accepted && finalPreview.PhaseResults[0].Assets.All(item =>
                    item.CurrentState != ThermalOperatingState.Emergency),
                "북안 운영안이 연속 한계 안에서 의무와 약속을 공급하지 못했습니다.");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "북안 운영안 승인");
            await NextFrame();
            CommercialCoreSnapshot checkpoint = coreRun.GetSnapshot();
            CommercialChapterResultRecord result = checkpoint.ChapterResults[^1];
            CommercialResultDemandFact fact = result.DemandFacts.Single(item =>
                item.DemandId == "NORTH_BANK_PROMISE_LOAD");
            Require(
                !checkpoint.CampaignComplete &&
                checkpoint.Chapter.ChapterId == "WHOSE_MARGIN" &&
                checkpoint.ChapterResults.Count == 4 &&
                fact.Supplied && fact.SourceNodeId is not null &&
                checkpoint.Construction.World.Edges.Any(item => item.EdgeId == "PLAYER_EDGE_1") &&
                _timeline.CurrentStepLabel == "결과" &&
                _timeline.AccessibilityName.Contains(result.Story.Title, StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("실제 의무", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("정수장", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("도시 약속 · 지킴", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("실제 경로", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("발전 접속점", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("비상 운전", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("보호정지", StringComparison.Ordinal),
                "결과 카드가 실제 공급원·경로·의무·약속 사실로 첫 네 임무를 닫지 못했습니다.");

            if (_options.CampaignCheckpointSmoke)
            {
                await AdvancePresentationToOperations("다섯 번째 임무 draft checkpoint");
                await PressPanelAsync(
                    CommercialPanelAction.StartLine,
                    "fresh-process 재개용 선로 도구");
                SpatialNodeDefinition draftStart = _snapshot.World.Nodes.Single(item =>
                    item.NodeId == "INDUSTRY_TERMINAL");
                await SelectAndClickCandidate(draftStart.Position, draftStart.NodeId);
                checkpoint = coreRun.GetSnapshot();
                string checkpointSavePath = _savePath
                    ?? throw new InvalidOperationException(
                        "Stage-F checkpoint 저장 경로가 없습니다.");
                CommercialCampaignSaveLoadResult persisted =
                    CommercialCampaignPersistenceStore.Load(checkpointSavePath);
                Require(
                    checkpoint.Construction.Phase == ConstructionPhase.LineDrafting &&
                    checkpoint.Construction.LineDraft?.StartNodeId == "INDUSTRY_TERMINAL" &&
                    persisted.Status == CommercialCoreDocumentLoadStatus.Loaded,
                    "다섯 번째 임무의 작성 중 선로를 fresh-process 재개용 저장으로 남기지 못했습니다.");
                GD.Print(
                    "COMMERCIAL_CAMPAIGN_STAGE_F_CHECKPOINT_SMOKE_PASS " +
                    $"missions={checkpoint.ChapterResults.Count} next={checkpoint.Chapter.ChapterId} " +
                    $"edges={checkpoint.Construction.World.Edges.Count} input=focus-keyboard " +
                    "save=mission5-line-draft resolution=1920x1080");
            }
            else
            {
                GD.Print(
                    "COMMERCIAL_CAMPAIGN_STAGE_E_SMOKE_PASS " +
                    $"missions={checkpoint.ChapterResults.Count} choice={result.PromiseDecision} " +
                    $"edges={checkpoint.Construction.World.Edges.Count} path={fact.PathEdgeIds.Count} " +
                    "carry=yes rollback=recent preview=approval input=focus-keyboard " +
                    "recovery=incompatible-preserved projection=live checkpoint=mission5 resolution=1920x1080");
            }
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"상용 캠페인 smoke 실패: {exception}");
            GetTree().Quit(1);
        }
    }

    private async void RunCampaignCompletionSmoke()
    {
        try
        {
            await NextFrame();
            GetWindow().Size = new Vector2I(1920, 1080);
            await NextFrame();
            ApplyUiScale(this, 1.25f);
            await NextFrame();
            CommercialCoreRun coreRun = _coreRun
                ?? throw new InvalidOperationException("상용 핵심 흐름 runner가 없습니다.");
            CommercialCoreSnapshot resumed = coreRun.GetSnapshot();
            Require(
                resumed.Chapter.ChapterId == "WHOSE_MARGIN" &&
                resumed.ChapterResults.Count == 4 &&
                resumed.Construction.World.Edges.Any(item => item.EdgeId == "PLAYER_EDGE_1") &&
                resumed.Construction.Phase == ConstructionPhase.LineDrafting &&
                resumed.Construction.LineDraft?.StartNodeId == "INDUSTRY_TERMINAL" &&
                _presentationMode == CommercialPresentationMode.ResumeOrientation,
                "별도 process가 다섯 번째 임무의 작성 중 선로까지 이어서 복원하지 못했습니다.");

            _shell.ShowTitle(true, "저장 진행 재개 확인");
            await PressShellAsync(ReleaseShellAction.Continue, "네 번째 임무 저장 이어하기");
            await NextFrame();
            CommercialChapterResultRecord resumedPrevious = resumed.ChapterResults[^1];
            Require(
                _shell.Page == ReleaseShellPage.Hidden &&
                _presentationMode == CommercialPresentationMode.ResumeOrientation &&
                _map.OperationsLocked &&
                _panel.GetActionButton(CommercialPanelAction.ApproveWindow)
                    .AccessibilityName == "진행 재개" &&
                _panel.AccessibilityName.Contains(resumedPrevious.Story.Title, StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("도시 약속 지킴", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains(resumed.Chapter.DisplayName, StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains(resumed.Chapter.Objective, StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains(FormatWon(resumed.CashUnit), StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains($"공사 {resumed.Construction.Minute}분", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("다음 행동", StringComparison.Ordinal),
                "Continue가 generic 도움말 대신 직전 결과·현재 위치·목표·자금·시각·다음 행동의 ResumeOrientation을 열지 못했습니다.");
            int resumeDraftCommandCount = coreRun.GetSnapshot().CommandCount;
            Require(
                _presentationMode == CommercialPresentationMode.ResumeOrientation &&
                _snapshot.Phase == ConstructionPhase.LineDrafting &&
                coreRun.GetSnapshot().CommandCount == resumeDraftCommandCount,
                "fresh-process 작성 중 저장이 ResumeOrientation에서 원형대로 복원되지 않았습니다.");
            _helpButton.GrabFocus();
            await NextFrame();
            await PressKey(Key.Enter);
            Require(
                _shell.Page == ReleaseShellPage.Pause &&
                _snapshot.Phase == ConstructionPhase.LineDrafting &&
                coreRun.GetSnapshot().CommandCount == resumeDraftCommandCount,
                "잠긴 ResumeOrientation의 메뉴가 저장된 draft를 취소하거나 기록을 바꿨습니다.");
            await PressShellAsync(ReleaseShellAction.Resume, "작성 중 재개 화면으로 돌아가기");
            await PressKey(Key.Escape);
            Require(
                _shell.Page == ReleaseShellPage.Pause &&
                _snapshot.Phase == ConstructionPhase.LineDrafting &&
                coreRun.GetSnapshot().CommandCount == resumeDraftCommandCount,
                "잠긴 ResumeOrientation의 Escape가 저장된 draft를 취소하거나 기록을 바꿨습니다.");
            await PressShellAsync(ReleaseShellAction.Resume, "작성 중 재개 화면 닫기");
            await PressPanelAsync(
                CommercialPanelAction.ApproveWindow,
                "작성 중 진행 재개");
            await NextFrame();
            Require(
                _presentationMode == CommercialPresentationMode.Operations &&
                _snapshot.Phase == ConstructionPhase.LineDrafting &&
                _tool == CommercialTool.Line,
                "진행 재개 뒤 저장된 draft와 선로 도구가 Operations로 돌아오지 않았습니다.");
            int resumedDraftPointCount = _snapshot.LineDraft?.IntermediatePoints.Count ?? 0;
            await ClickMap(new CoreMapPoint(2825, 1150));
            Require(
                _snapshot.LineDraft?.IntermediatePoints.Count == resumedDraftPointCount + 1,
                "진행 재개 뒤 복원된 선로 draft에 실제 지도 입력으로 전신주를 이어 놓지 못했습니다.");
            await PressPanelAsync(
                CommercialPanelAction.UndoPoint,
                "재개한 draft의 추가 전신주 되돌리기");
            Require(
                _snapshot.LineDraft?.IntermediatePoints.Count == resumedDraftPointCount,
                "재개한 선로 draft에서 추가한 전신주를 되돌리지 못했습니다.");
            await PressPanelAsync(
                CommercialPanelAction.CancelDraft,
                "재개 잠금 확인용 draft 취소");
            await NextFrame();

            await PressPanelAsync(
                CommercialPanelAction.CycleLineClass,
                "산업단지 일반 선종 확인");
            await PressPanelAsync(
                CommercialPanelAction.CycleLineClass,
                "산업단지 보강 선종 선택");
            Require(_lineClassId == ReinforcedLineClassId &&
                _poleClassId == ReinforcedPoleClassId,
                "다섯 번째 임무에서 보강 선로와 보강 전신주를 화면 입력으로 선택하지 못했습니다.");

            await BuildCampaignSmokeSubstation(
                new CoreMapPoint(2700, 1150),
                "산업단지 서비스 변전소");
            await BuildCampaignSmokeLine(
                "WEST_AUXILIARY",
                "PLAYER_SUBSTATION_3",
                [
                    new CoreMapPoint(900, 700),
                    new CoreMapPoint(1050, 1050),
                    new CoreMapPoint(1650, 1050),
                    new CoreMapPoint(2100, 1050),
                    new CoreMapPoint(2500, 1050),
                ],
                "산업단지 보강 간선");
            await PressPanelAsync(
                CommercialPanelAction.CycleLineClass,
                "산업단지 일반 인입선 선택");
            Require(_lineClassId == StandardLineClassId && _poleClassId == StandardPoleClassId,
                "화면 선종 선택이 일반 선로와 일반 전신주를 함께 선택하지 못했습니다.");
            await BuildCampaignSmokeLine(
                "PLAYER_SUBSTATION_3",
                "INDUSTRY_TERMINAL",
                Array.Empty<CoreMapPoint>(),
                "산업단지 일반 인입선");
            await PressPanelAsync(CommercialPanelAction.KeepPromise, "폭염 증산 약속 지킴");
            CommercialDecisionPreview hot = coreRun.PreviewDecisionWindow();
            Require(hot.Accepted && hot.PhaseResults[0].Demands.Single(item =>
                    item.DemandId == "INDUSTRY_MARGIN_PROMISE").EmergencyAssetIds.Count > 0,
                "다섯 번째 임무가 공개된 비상 열여유로 증산 약속을 공급하지 못했습니다.");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "폭염 운영 승인");
            await NextFrame();
            Require(coreRun.GetSnapshot().DecisionWindowIndex == 1 &&
                _presentationMode == CommercialPresentationMode.WindowStory &&
                _map.OperationsLocked &&
                _panel.GetActionButton(CommercialPanelAction.ApproveWindow)
                    .AccessibilityName == "운영 시작" &&
                !_panel.GetActionButton(CommercialPanelAction.KeepPromise).Visible &&
                _timeline.CurrentStepLabel == "다음 아침 안전 경계" &&
                _timeline.AccessibilityName.Contains(
                    "더운 저녁의 여유는 한 번만 쓸 수 있습니다",
                    StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("보호정지 뒤에도 아침은 옵니다", StringComparison.Ordinal),
                "다섯 번째 임무의 보호정지 결과 이야기와 다음 결정 경계를 표시하지 못했습니다. " +
                $"window={coreRun.GetSnapshot().DecisionWindowIndex}, timeline={_timeline.CurrentStepLabel}, " +
                $"timelineA11y={_timeline.AccessibilityName}, panelA11y={_panel.AccessibilityName}");
            (int OutageIntervalIndex, ThermalAssetResult OutageAsset) outage =
                _thermalSequence.Intervals
                    .SelectMany((interval, index) => interval.Assets
                        .Where(asset => asset.CurrentState ==
                            ThermalOperatingState.ProtectiveOutage)
                        .Select(asset => (OutageIntervalIndex: index, OutageAsset: asset)))
                    .FirstOrDefault();
            Require(outage.OutageAsset is not null,
                "폭염 보호정지 캡처에 실제 보호정지 asset이 없습니다.");
            _thermalProjectionIndex = outage.OutageIntervalIndex;
            _selectedThermalAssetId = outage.OutageAsset!.AssetId;
            Render();
            await NextFrame();
            // The heat reference is explicitly an outage screen. Capture the
            // authored protective-outage boundary, not the preceding successful
            // emergency-use preview, so the visual state and factual panel agree.
            await CaptureG3FinalPair("pair-heat.png");
            await AdvancePresentationToOperations("보호정지 뒤 아침 경계");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "보호정지 뒤 아침 운영 승인");
            await NextFrame();
            Require(coreRun.GetSnapshot().Chapter.ChapterId == "BEFORE_WATER_REACHES" &&
                coreRun.GetSnapshot().ChapterResults.Count == 5,
                "다섯 번째 임무의 두 운영 경계를 사실 기록으로 닫지 못했습니다.");
            Require(_lineClassId == StandardLineClassId &&
                _poleClassId == StandardPoleClassId,
                "범람 우회의 일반 선로와 일반 전신주 선택이 유지되지 않았습니다.");

            await BuildCampaignSmokeLine(
                "WATER_TERMINAL",
                "PLAYER_SUBSTATION_3",
                Array.Empty<CoreMapPoint>(),
                "범람 고지대 우회선");
            CommercialDecisionPreview flood = coreRun.PreviewDecisionWindow();
            Require(flood.Accepted && flood.PhaseResults[0].Demands
                    .Where(item => item.DemandId is
                        "FLOOD_WATER_DUTY" or "FLOOD_HOSPITAL_DUTY")
                    .All(item => item.Supplied) &&
                flood.PhaseResults[0].Demands.Single(item =>
                    item.DemandId == "FLOOD_HOSPITAL_DUTY").EmergencyAssetIds.Count > 0,
                "여섯 번째 임무의 범람 우회선이 수술과 급수 의무를 공급하지 못했습니다: " +
                $"{flood.Accepted}/{flood.Error}/{flood.FailedDemandId}/{flood.SupplyFailure}; " +
                "hospital-emergency=" + string.Join(",", flood.PhaseResults[0].Demands.Single(item =>
                    item.DemandId == "FLOOD_HOSPITAL_DUTY").EmergencyAssetIds) + "; demands=" +
                string.Join(";", flood.PhaseResults[0].Demands.Select(item =>
                    $"{item.DemandId}:{item.Supplied}:{item.Deferred}:{item.Failure}")));
            await CaptureG3FinalPair("pair-flood.png");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "범람 통제 운영 승인");
            await NextFrame();
            Require(coreRun.GetSnapshot().Chapter.ChapterId == "SHUT_DOWN_TO_KEEP" &&
                coreRun.GetSnapshot().ChapterResults[^1].EmergencyAssetIds.Count > 0 &&
                coreRun.GetSnapshot().ThermalMemory.All(item => !item.ProtectiveOutage) &&
                coreRun.GetSnapshot().Chapter.ResetThermalMemoryAtStart &&
                coreRun.GetSnapshot().Chapter.Briefing.Title.Contains("3주 뒤", StringComparison.Ordinal),
                "장 시작 전 시간 경과가 일곱 번째 임무의 열 상태를 복귀시키지 못했습니다.");

            await AdvancePresentationToOperations("꺼야 지킬 수 있다");
            await PressPanelAsync(
                CommercialPanelAction.CycleLineClass,
                "계획정지 보강 선종 선택");
            Require(_lineClassId == ReinforcedLineClassId &&
                _poleClassId == ReinforcedPoleClassId,
                "계획정지 임무의 보강 선로와 보강 전신주를 선택하지 못했습니다.");

            await BuildCampaignSmokeLine(
                "PLAYER_SUBSTATION_2",
                "PLAYER_POLE_15",
                Array.Empty<CoreMapPoint>(),
                "계획정지 변전소 연계");
            Require(_panel.AccessibilityName.Contains("3주 뒤", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("모든 설비가 연속 운전 가능 상태로 복귀", StringComparison.Ordinal),
                "일곱 번째 임무 화면이 장 시작 전 열 상태 복귀를 알리지 못했습니다.");
            await PressPanelAsync(CommercialPanelAction.KeepPromise, "계획정지 복구 약속 지킴");
            Require(coreRun.PreviewDecisionWindow().Accepted,
                "일곱 번째 임무의 계획정지 우회 운영안을 승인할 수 없습니다.");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "계획정지 운영 승인");
            await NextFrame();
            Require(coreRun.GetSnapshot().Chapter.ChapterId == "LONGEST_NIGHT" &&
                coreRun.GetSnapshot().ChapterResults.Count == 7,
                "일곱 번째 임무 결과가 마지막 임무로 이어지지 못했습니다.");

            await BuildCampaignSmokeLine(
                "HOSPITAL_TERMINAL",
                "PLAYER_POLE_15",
                Array.Empty<CoreMapPoint>(),
                "가장 긴 밤 의료원 연계");
            await PressPanelAsync(CommercialPanelAction.DeferPromise, "마지막 야간 증산 약속 미룸");
            Require(coreRun.PreviewDecisionWindow().Accepted,
                "여덟 번째 임무의 폭염 운영안을 승인할 수 없습니다.");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "마지막 폭염 운영 승인");
            await NextFrame();
            Require(coreRun.GetSnapshot().DecisionWindowIndex == 1 &&
                _presentationMode == CommercialPresentationMode.WindowStory &&
                _map.OperationsLocked &&
                _panel.AccessibilityName.Contains("강변 통제와 서부 전원 정지", StringComparison.Ordinal),
                "여덟 번째 임무의 복합재난 전환 이야기를 표시하지 못했습니다.");
            await AdvancePresentationToOperations("마지막 복합재난 경계");
            Require(coreRun.PreviewDecisionWindow().Accepted,
                "여덟 번째 임무의 복합재난 운영안을 승인할 수 없습니다.");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "마지막 복합재난 운영 승인");
            await NextFrame();

            CommercialCoreSnapshot complete = coreRun.GetSnapshot();
            Require(complete.CampaignComplete && complete.ChapterResults.Count == 8 &&
                complete.ChapterStartCommandCounts.Count == 8 &&
                _presentationMode == CommercialPresentationMode.Result &&
                _map.OperationsLocked &&
                HeadingText() == "가장 긴 밤 · 결과" &&
                _panel.GetActionButton(CommercialPanelAction.ApproveWindow)
                    .AccessibilityName == "에필로그 보기" &&
                !_panel.GetActionButton(CommercialPanelAction.NextThermalPhase).Visible &&
                _panel.AccessibilityName.Contains("도시 약속 · 미룸", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("실제 경로", StringComparison.Ordinal),
                "여덟 번째 임무를 동결된 마지막 Result mode로 완료하지 못했습니다.");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "완주 에필로그 열기");
            await NextFrame();
            Require(_presentationMode == CommercialPresentationMode.Epilogue &&
                _map.OperationsLocked &&
                _panel.AccessibilityName.Contains("청류시 전력망 운영 인계", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("여덟 임무 실제 기록", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("원하는 장의 시작 상태", StringComparison.Ordinal) &&
                !_panel.GetActionButton(CommercialPanelAction.ApproveWindow).Visible,
                "마지막 Result의 에필로그 보기 행동이 작성된 Epilogue mode를 열지 못했습니다.");
            string completedSavePath = _savePath
                ?? throw new InvalidOperationException("완료 저장 경로가 없습니다.");
            CommercialCampaignSaveLoadResult completedLoad =
                CommercialCampaignPersistenceStore.Load(completedSavePath);
            Require(completedLoad.Status == CommercialCoreDocumentLoadStatus.Loaded,
                "여덟 번째 임무 뒤 완료 저장을 기록하지 못했습니다.");
            CommercialCoreRun freshComplete = CommercialCampaignSaveCodec.Restore(
                completedLoad.Save!,
                _commercialWorld,
                _worldBytes,
                _campaign,
                _campaignBytes);
            Require(freshComplete.GetSnapshot().CampaignComplete &&
                freshComplete.GetSnapshot().ChapterResults.Count == 8,
                "완료 저장을 fresh replay로 동일한 완주 상태에 복원하지 못했습니다.");

            GD.Print(
                "COMMERCIAL_CAMPAIGN_STAGE_F_COMPLETION_SMOKE_PASS " +
                $"missions={complete.ChapterResults.Count} results=factual epilogue=shown " +
                "resume=mission5-to-8 save=complete input=focus-keyboard resolution=1920x1080");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"상용 캠페인 Stage-F 완주 smoke 실패: {exception}");
            GetTree().Quit(1);
        }
    }

    private async void RunCampaignCompletedResumeSmoke()
    {
        try
        {
            await NextFrame();
            GetWindow().Size = new Vector2I(1920, 1080);
            await NextFrame();
            ApplyUiScale(this, 1.25f);
            await NextFrame();
            CommercialCoreRun coreRun = _coreRun
                ?? throw new InvalidOperationException("상용 핵심 흐름 runner가 없습니다.");
            CommercialCoreSnapshot restored = coreRun.GetSnapshot();
            Require(restored.CampaignComplete && restored.ChapterResults.Count == 8 &&
                _presentationMode == CommercialPresentationMode.ResumeOrientation &&
                _panel.AccessibilityName.Contains("첫 불빛 · 안전 의무", StringComparison.Ordinal),
                "새 process가 완료 저장의 ResumeOrientation과 여덟 사실 기록을 복원하지 못했습니다.");
            _shell.ShowTitle(true, "완료 저장 재개 확인");
            await PressShellAsync(ReleaseShellAction.Continue, "완료 저장 이어하기");
            await NextFrame();
            Require(
                _presentationMode == CommercialPresentationMode.ResumeOrientation &&
                _map.OperationsLocked &&
                _panel.GetActionButton(CommercialPanelAction.ApproveWindow)
                    .AccessibilityName == "진행 재개" &&
                _panel.AccessibilityName.Contains(
                    restored.ChapterResults[^1].Story.Title,
                    StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("캠페인 완료", StringComparison.Ordinal),
                "완료 저장 Continue가 마지막 결과와 완료 위치를 ResumeOrientation으로 복원하지 못했습니다.");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "완료 저장 에필로그 재개");
            await NextFrame();
            Require(
                _presentationMode == CommercialPresentationMode.Epilogue &&
                _panel.AccessibilityName.Contains("청류시 전력망 운영 인계", StringComparison.Ordinal),
                "완료 ResumeOrientation의 진행 재개가 에필로그를 열지 못했습니다.");
            for (int index = 1; index < _campaign.Chapters.Count; index++)
            {
                await PressPanelAsync(
                    CommercialPanelAction.NextThermalPhase,
                    $"완료 장 선택 {index + 1}");
            }
            Require(_completedChapterSelectionIndex == 7 &&
                _panel.AccessibilityName.Contains("8/8 가장 긴 밤", StringComparison.Ordinal),
                "실제 키보드 입력으로 여덟 번째 장 시작 상태를 선택하지 못했습니다.");
            _helpButton.GrabFocus();
            await NextFrame();
            await PressKey(Key.Enter);
            Require(_shell.Page == ReleaseShellPage.Pause,
                "완료 캠페인에서 메뉴가 선택 장 재시작 확인을 열지 못했습니다.");
            await PressShellAsync(ReleaseShellAction.RestartChapter, "선택한 마지막 장 재시작 확인 열기");
            await PressShellAsync(ReleaseShellAction.Confirm, "선택한 마지막 장부터 다시 시작");
            await NextFrame();
            CommercialCoreSnapshot selected = _coreRun!.GetSnapshot();
            Require(!selected.CampaignComplete && selected.Chapter.ChapterId == "LONGEST_NIGHT" &&
                selected.ChapterResults.Count == 7 && selected.DecisionWindowIndex == 0 &&
                _presentationMode == CommercialPresentationMode.Briefing &&
                _map.OperationsLocked &&
                _panel.GetActionButton(CommercialPanelAction.ApproveWindow)
                    .AccessibilityName == "임무 시작" &&
                _panel.AccessibilityName.Contains("앞서 만든 망과 약속", StringComparison.Ordinal),
                "선택한 여덟 번째 장의 정확한 시작 journal 상태로 돌아가지 못했습니다.");
            Require(_savePath is not null &&
                CommercialCampaignPersistenceStore.Load(_savePath).Status ==
                    CommercialCoreDocumentLoadStatus.Loaded,
                "선택 장 시작 상태를 새 진행으로 저장하지 못했습니다.");

            GD.Print(
                "COMMERCIAL_CAMPAIGN_STAGE_F_COMPLETED_RESUME_SMOKE_PASS " +
                $"restored=complete selected={selected.Chapter.ChapterId} prior-results=" +
                $"{selected.ChapterResults.Count} input=focus-keyboard resolution=1920x1080");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"상용 캠페인 Stage-F 완료 재개 smoke 실패: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task BuildCampaignSmokeLine(
        string startNodeId,
        string endNodeId,
        IReadOnlyList<CoreMapPoint> intermediatePoints,
        string label,
        string? draftEvidencePath = null)
    {
        await AdvancePresentationToOperations(label);
        await PressPanelAsync(CommercialPanelAction.StartLine, $"{label} 선로 도구");
        await NextFrame();
        SpatialNodeDefinition start = _snapshot.World.Nodes.Single(item =>
            item.NodeId == startNodeId);
        SpatialNodeDefinition end = _snapshot.World.Nodes.Single(item =>
            item.NodeId == endNodeId);
        await SelectAndClickCandidate(start.Position, startNodeId);
        foreach (CoreMapPoint point in intermediatePoints)
        {
            await ClickMap(point);
            Require(
                _map.CurrentDraftSpriteClassId == _snapshot.LineDraft?.PoleClassId,
                $"{label}의 건설 중 전주가 선택한 node class 스프라이트를 표시하지 않았습니다. " +
                $"point={point} candidate={_map.SelectedCandidateId ?? "none"} " +
                $"draft={string.Join(';', _snapshot.LineDraft?.IntermediatePoints ?? Array.Empty<CoreMapPoint>())} " +
                $"error={_lastError} zoom={_map.ZoomLabel}");
        }
        if (draftEvidencePath is not null)
        {
            SaveEvidencePng(draftEvidencePath);
        }
        await SelectAndClickCandidate(end.Position, endNodeId);
        Require(
            _snapshot.LineDraft?.EndNodeId == endNodeId,
            $"{label} 경로를 완성하지 못했습니다: {_lastError}; " +
            $"endCanvas={_map.ViewportPointForWorld(end.Position)}; " +
            $"candidate={_map.SelectedCandidateId ?? "none"}; " +
            $"draft={string.Join(';', _snapshot.LineDraft?.IntermediatePoints ?? Array.Empty<CoreMapPoint>())}");
        await PressPanelAsync(CommercialPanelAction.Commission, $"{label} 공사 발주");
        await NextFrame();
        await PressPanelAsync(CommercialPanelAction.Commission, $"{label} 공사 완공");
        await NextFrame();
    }

    private async Task BuildCampaignSmokeSubstation(
        CoreMapPoint position,
        string label,
        string? draftEvidencePath = null)
    {
        await AdvancePresentationToOperations(label);
        await PressPanelAsync(CommercialPanelAction.PlaceSubstation, $"{label} 도구");
        await NextFrame();
        await ClickMap(position);
        Require(
            _snapshot.NodeDraft?.Position == position &&
            _map.CurrentDraftSpriteClassId == _snapshot.NodeDraft.NodeClassId,
            $"{label} 위치를 계획하지 못했습니다: {_lastError}");
        if (draftEvidencePath is not null)
        {
            SaveEvidencePng(draftEvidencePath);
        }
        await PressPanelAsync(CommercialPanelAction.Commission, $"{label} 공사 발주");
        await NextFrame();
        await PressPanelAsync(CommercialPanelAction.Commission, $"{label} 공사 완공");
        await NextFrame();
    }

    private async Task AdvancePresentationToOperations(string description)
    {
        for (int transition = 0;
             transition < 4 && _presentationMode != CommercialPresentationMode.Operations;
             transition++)
        {
            if (_presentationMode == CommercialPresentationMode.Epilogue)
            {
                throw new InvalidOperationException(
                    $"에필로그에서는 공사를 시작할 수 없습니다: {description}");
            }
            BaseButton primary = _panel.GetActionButton(CommercialPanelAction.ApproveWindow);
            Require(
                primary.Visible && !primary.Disabled && _map.OperationsLocked,
                $"잠긴 presentation의 기본 진행 행동을 사용할 수 없습니다: {description}; " +
                $"mode={_presentationMode}, action={primary.AccessibilityName}");
            await PressPanelAsync(
                CommercialPanelAction.ApproveWindow,
                $"{description} presentation 진행");
            await NextFrame();
        }
        Require(
            _presentationMode == CommercialPresentationMode.Operations &&
            !_map.OperationsLocked,
            $"공사 전에 Operations mode에 진입하지 못했습니다: {description}; " +
            $"mode={_presentationMode}");
    }

    private async Task RequireRejectedPlacement(
        CoreMapPoint point,
        ConstructionError expected,
        string description)
    {
        int nodeCount = _snapshot.World.Nodes.Count;
        await MovePointer(point);
        Require(!_pointerAccepted && _pointerError == expected,
            $"{description}을 클릭 전에 막지 못했습니다.");
        await ClickMap(point);
        Require(
            _snapshot.World.Nodes.Count == nodeCount &&
            _snapshot.NodeDraft is null &&
            _lastError == ErrorText(expected),
            $"{description}을 클릭 뒤 typed 오류로 거부하지 못했습니다.");
    }

    private async Task ClickMap(CoreMapPoint point)
    {
        Vector2 viewportPoint = _map.ViewportPointForWorld(point);
        await PushMouseMove(viewportPoint);
        viewportPoint = _map.ViewportPointForWorld(point);
        await PushMouseMove(viewportPoint);
        viewportPoint = _map.ViewportPointForWorld(point);
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
        await NextFrame();
    }

    private async Task MovePointer(CoreMapPoint point)
    {
        await PushMouseMove(_map.ViewportPointForWorld(point));
        await PushMouseMove(_map.ViewportPointForWorld(point));
    }

    private async Task SelectAndClickCandidate(CoreMapPoint point, string nodeId)
    {
        // Native rendering can publish the pointer move one frame before the map
        // has rebuilt its proximity candidates after a camera/layout update.
        // Keep exercising the real mouse-input path, but bound the settle loop so
        // the smoke is deterministic without invoking renderer signals directly.
        for (int settle = 0;
             settle < 8 && !_map.CandidateNodeIds.Contains(nodeId, StringComparer.Ordinal);
             settle++)
        {
            await MovePointer(point);
            await NextFrame();
        }
        for (int index = 0;
             index < _map.CandidateNodeIds.Count && _map.SelectedCandidateId != nodeId;
             index++)
        {
            await PressMapKey(Key.E, physical: Key.E);
        }
        Require(_map.SelectedCandidateId == nodeId,
            $"요청한 접속 후보를 선택할 수 없습니다: {nodeId}; " +
            $"selected={_map.SelectedCandidateId ?? "none"}; " +
            $"candidates={string.Join(',', _map.CandidateNodeIds)}; " +
            $"pointer={_map.KeyboardPoint.XUnit},{_map.KeyboardPoint.YUnit}");
        await ClickMap(point);
    }

    private async Task PushMouseMove(Vector2 viewportPoint)
    {
        GetViewport().PushInput(new InputEventMouseMotion
        {
            Position = viewportPoint,
            GlobalPosition = viewportPoint,
        }, true);
        await NextFrame();
    }

    private async Task WheelAt(Vector2 viewportPoint, MouseButton wheel)
    {
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = viewportPoint,
            GlobalPosition = viewportPoint,
            ButtonIndex = wheel,
            Pressed = true,
        }, true);
        await NextFrame();
    }

    private async Task MiddleDrag(Vector2 from, Vector2 to)
    {
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = from,
            GlobalPosition = from,
            ButtonIndex = MouseButton.Middle,
            Pressed = true,
        }, true);
        GetViewport().PushInput(new InputEventMouseMotion
        {
            Position = to,
            GlobalPosition = to,
            Relative = to - from,
            ButtonMask = MouseButtonMask.Middle,
        }, true);
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = to,
            GlobalPosition = to,
            ButtonIndex = MouseButton.Middle,
            Pressed = false,
        }, true);
        await NextFrame();
    }

    private async Task DragMap(CoreMapPoint from, CoreMapPoint to)
    {
        Vector2 fromViewport = _map.ViewportPointForWorld(from);
        Vector2 toViewport = _map.ViewportPointForWorld(to);
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = fromViewport,
            GlobalPosition = fromViewport,
            ButtonIndex = MouseButton.Left,
            Pressed = true,
        }, true);
        GetViewport().PushInput(new InputEventMouseMotion
        {
            Position = toViewport,
            GlobalPosition = toViewport,
            Relative = toViewport - fromViewport,
            ButtonMask = MouseButtonMask.Left,
        }, true);
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = toViewport,
            GlobalPosition = toViewport,
            ButtonIndex = MouseButton.Left,
            Pressed = false,
        }, true);
        await NextFrame();
    }

    private async Task PressKey(
        Key key,
        Key physical = Key.None,
        bool shift = false)
    {
        GetViewport().PushInput(new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = physical == Key.None ? key : physical,
            ShiftPressed = shift,
            Pressed = true,
        }, true);
        GetViewport().PushInput(new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = physical == Key.None ? key : physical,
            ShiftPressed = shift,
            Pressed = false,
        }, true);
        await NextFrame();
    }

    private async Task PressMapKey(
        Key key,
        Key physical = Key.None,
        bool shift = false)
    {
        _map.GrabFocus();
        await PressKey(key, physical, shift);
    }

    private async Task PressPanelAsync(CommercialPanelAction action, string description)
    {
        Key toolKey = action switch
        {
            CommercialPanelAction.StartLine => Key.Key1,
            CommercialPanelAction.PlaceSubstation => Key.Key2,
            CommercialPanelAction.CycleLineClass => Key.Key3,
            _ => Key.None,
        };
        if (toolKey != Key.None)
        {
            await PressMapKey(toolKey);
            return;
        }
        BaseButton button = _panel.GetActionButton(action);
        if (!button.Visible || button.Disabled)
        {
            throw new InvalidOperationException($"필요한 화면 행동을 사용할 수 없습니다: {description}");
        }
        button.GrabFocus();
        await NextFrame();
        Require(button.HasFocus(), $"화면 행동이 키보드 focus를 받지 못했습니다: {description}");
        await PressKey(Key.Enter);
    }

    private static bool ControlInside(Control outer, Control inner)
    {
        Rect2 outerRect = outer.GetGlobalRect();
        Rect2 innerRect = inner.GetGlobalRect();
        return outerRect.Encloses(innerRect);
    }

    private static void ApplyUiScale(Node node, float scale)
    {
        if (node is Control control && control is Label or BaseButton)
        {
            int baseSize = control.GetThemeFontSize("font_size");
            if (baseSize > 0)
            {
                control.AddThemeFontSizeOverride(
                    "font_size",
                    Math.Max(1, (int)MathF.Round(baseSize * scale)));
            }
        }
        foreach (Node child in node.GetChildren())
        {
            ApplyUiScale(child, scale);
        }
    }

    private async Task NextFrame() =>
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private static bool NearlyEqual(double first, double second, double tolerance) =>
        Math.Abs(first - second) <= tolerance;

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
