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
            Require(_shell.Page == ReleaseShellPage.Help,
                "새 게임의 조작 도움말이 같은 shell overlay에 열리지 않았습니다.");
            await PressShellAsync(ReleaseShellAction.HelpBack, "조작 도움말 닫기");
            await NextFrame();
            Require(_shell.Page == ReleaseShellPage.Hidden && _map.HasFocus(),
                "조작 도움말 뒤 지도로 keyboard focus가 돌아오지 않았습니다.");

            await BuildCampaignSmokeSubstation(
                new CoreMapPoint(2200, 750),
                "표현 확인 변전소");
            await BuildCampaignSmokeLine(
                "WEST_SOURCE",
                "PLAYER_SUBSTATION_1",
                [
                    new CoreMapPoint(650, 700),
                    new CoreMapPoint(950, 500),
                    new CoreMapPoint(1545, 450),
                    new CoreMapPoint(1900, 600),
                ],
                "표현 확인 간선");
            await BuildCampaignSmokeLine(
                "PLAYER_SUBSTATION_1",
                "EAST_RESIDENTIAL_TERMINAL",
                Array.Empty<CoreMapPoint>(),
                "표현 확인 인입선");
            ThermalIntervalResult active = _thermalSequence.Intervals[_thermalProjectionIndex];
            ThermalDemandResult selected = SelectedDemand(active)
                ?? throw new InvalidOperationException("선택할 수요가 없습니다.");
            Require(
                selected.Supplied &&
                selected.PathEdgeIds.Count > 0 &&
                _map.AccessibilityName.Contains("선택 수요 경로", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("최소 열여유", StringComparison.Ordinal) &&
                _summaryLabel.Text.Contains("필수 공급 1/1 ✓", StringComparison.Ordinal),
                "선택 수요의 발전원·전체 경로·열여유·시설 강조가 함께 갱신되지 않았습니다.");

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
            Require(
                GetWindow().Size == new Vector2I(1920, 1080) &&
                ControlInside(_panel, _panel.GetActionButton(CommercialPanelAction.Commission)) &&
                ControlInside(_panel, _panel.GetActionButton(CommercialPanelAction.NextDemand)) &&
                ControlInside(_panel, _panel.GetActionButton(CommercialPanelAction.NextThermalPhase)) &&
                _map.AccessibilityName.Contains("선택 수요 경로", StringComparison.Ordinal),
                "1920×1080·UI 125%에서 고정 행동이나 선택 경로가 화면 밖으로 잘렸습니다.");
            SaveEvidencePng(Path.Combine(evidenceDirectory, "1920x1080-ui125-path-reduce-motion.png"));

            await PressKey(Key.Escape);
            await PressShellAsync(ReleaseShellAction.SaveAndQuit, "저장하고 제목으로");
            Require(
                _shell.Page == ReleaseShellPage.Title &&
                _hasContinuation &&
                File.Exists(_savePath) &&
                !File.Exists(_savePath + ".tmp") &&
                !File.Exists(_settingsPath + ".tmp"),
                "Save & Quit이 성공한 원자적 저장 뒤에만 제목으로 이동하지 않았습니다.");
            GD.Print(
                "COMMERCIAL_STAGE_G_PRESENTATION_SMOKE_PASS " +
                "screens=title-ui100|path-ui125 input=focus-keyboard reduce-motion=on " +
                "save-and-quit=atomic resolution=1920x1080 " +
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
            await MovePointer(new CoreMapPoint(300, 950));
            Require(
                _map.CandidateNodeIds.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(
                    new[] { "WEST_AUXILIARY", "WEST_SOURCE" },
                    StringComparer.Ordinal),
                "화면 거리와 node ID 순서의 접속 후보가 안정적으로 만들어지지 않았습니다: " +
                string.Join(",", _map.CandidateNodeIds) +
                $" pointer={_pointerPoint} sources=" +
                string.Join(";", _snapshot.World.Nodes.Where(item =>
                    item.NodeId is "WEST_SOURCE" or "WEST_AUXILIARY").Select(item =>
                    $"{item.NodeId}:{item.Position}")));
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
                new(2400, 1150),
            ];
            await SelectAndClickCandidate(new CoreMapPoint(300, 950), "WEST_SOURCE");
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
            Require(_snapshot.LineDraft?.IntermediatePoints.SequenceEqual(exactPath[..4]) == true,
                $"네 번째 전신주 계획을 추가하지 못했습니다: {_lastError}");
            await ClickMap(exactPath[4]);
            Require(_snapshot.LineDraft?.IntermediatePoints.SequenceEqual(exactPath) == true,
                $"마지막 전신주 계획을 추가하지 못했습니다: {_lastError}");
            await SelectAndClickCandidate(
                new CoreMapPoint(2600, 800),
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
                _audio.GetChildCount() == 4 &&
                !_panel.GetActionButton(CommercialPanelAction.KeepPromise).Visible &&
                _panel.AccessibilityName.Contains("현재 의무", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("조작 ·", StringComparison.Ordinal) &&
                _map.AccessibilityName.Contains("예정 시설 4곳", StringComparison.Ordinal) &&
                ControlInside(_panel, _panel.GetActionButton(CommercialPanelAction.ApproveWindow)) &&
                ControlInside(_panel, _panel.GetActionButton(CommercialPanelAction.RollbackProject)) &&
                !_panel.GetActionButton(CommercialPanelAction.RestartChapter).Visible &&
                !_panel.GetActionButton(CommercialPanelAction.NewGame).Visible,
                "1920×1080·UI 125%에서 상용 캠페인·오디오와 고정 행동 영역을 열지 못했습니다.");

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

            await BuildCampaignSmokeSubstation(
                new CoreMapPoint(2200, 750),
                "첫 불빛 변전소");
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
            await BuildCampaignSmokeLine(
                "PLAYER_SUBSTATION_1",
                "EAST_RESIDENTIAL_TERMINAL",
                Array.Empty<CoreMapPoint>(),
                "첫 불빛 인입선");
            Require(_thermalSequence.Intervals.SelectMany(item => item.Assets).Any(item =>
                    item.AssetId == "PLAYER_EDGE_6") &&
                _panel.AccessibilityName.Contains("공급할 수 있습니다", StringComparison.Ordinal),
                "예약 시설이 남은 임무에서 완공 선로의 열·경로 projection을 즉시 갱신하지 못했습니다.");
            CommercialDecisionPreview firstPreview = coreRun.PreviewDecisionWindow();
            Require(firstPreview.Accepted && firstPreview.ProjectedMinute <= 800,
                "첫 불빛 운영안이 안전 의무와 기한을 만족하지 못했습니다.");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "첫 불빛 운영 승인");
            await NextFrame();
            Require(
                coreRun.GetSnapshot().Chapter.ChapterId == "SECOND_HEART" &&
                coreRun.GetSnapshot().ChapterResults.Count == 1 &&
                _panel.AccessibilityName.Contains("동부 생활권 첫 점등", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("실제 경로", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("서부 발전 접속점", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("동부 생활권", StringComparison.Ordinal),
                "첫 불빛 결과와 실제 공급 사실을 제시한 뒤 두 번째 심장으로 전환하지 못했습니다.");

            await BuildCampaignSmokeLine(
                "PLAYER_SUBSTATION_1",
                "HOSPITAL_TERMINAL",
                [new CoreMapPoint(2200, 1100)],
                "의료원 북안 회랑");
            await BuildCampaignSmokeSubstation(
                new CoreMapPoint(2100, 1450),
                "의료원 남안 변전소");
            await BuildCampaignSmokeLine(
                "WEST_SOURCE",
                "PLAYER_SUBSTATION_2",
                [
                    new CoreMapPoint(650, 1150),
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
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "의료원 차단시험 승인");
            await NextFrame();
            Require(
                coreRun.GetSnapshot().Chapter.ChapterId == "SECOND_SOURCE" &&
                !_panel.GetActionButton(CommercialPanelAction.KeepPromise).Visible &&
                !_panel.AccessibilityName.Contains("조작 ·", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("수술실 전환시험 완료", StringComparison.Ordinal),
                "두 번째 심장 결과와 다음 임무 전환을 제시하지 못했습니다.");

            await BuildCampaignSmokeLine(
                "WEST_AUXILIARY",
                "PLAYER_POLE_1",
                Array.Empty<CoreMapPoint>(),
                "남부 전원 생활권 연계");
            await BuildCampaignSmokeLine(
                "WEST_AUXILIARY",
                "PLAYER_POLE_6",
                Array.Empty<CoreMapPoint>(),
                "남부 전원 의료원 연계");
            CommercialDecisionPreview sourcePreview = coreRun.PreviewDecisionWindow();
            Require(sourcePreview.Accepted &&
                sourcePreview.PhaseResults[0].Demands.All(item =>
                    item.Supplied && item.SourceNodeId == "WEST_AUXILIARY"),
                "서부 전원 인수시험이 남부 발전 접속점의 실제 경로를 사용하지 못했습니다.");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "서부 주간선 인수시험 승인");
            await NextFrame();
            Require(
                coreRun.GetSnapshot().Chapter.ChapterId == "NORTH_BANK_PROMISE" &&
                _panel.GetActionButton(CommercialPanelAction.KeepPromise).Visible &&
                _panel.AccessibilityName.Contains("선택 필요", StringComparison.Ordinal) &&
                _map.AccessibilityName.Contains("예정 시설 1곳", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("운영 인수 완료", StringComparison.Ordinal),
                "두 번째 전원 결과와 본편 전환을 제시하지 못했습니다.");

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
                Require(_savePath is not null &&
                    CommercialCampaignPersistenceStore.Load(_savePath).Status ==
                        CommercialCoreDocumentLoadStatus.Loaded,
                    "네 번째 임무 뒤 fresh-process 재개용 저장을 남기지 못했습니다.");
                GD.Print(
                    "COMMERCIAL_CAMPAIGN_STAGE_F_CHECKPOINT_SMOKE_PASS " +
                    $"missions={checkpoint.ChapterResults.Count} next={checkpoint.Chapter.ChapterId} " +
                    $"edges={checkpoint.Construction.World.Edges.Count} input=focus-keyboard " +
                    "save=mission4-to-5 resolution=1920x1080");
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
                _panel.AccessibilityName.Contains("더운 저녁의 여유", StringComparison.Ordinal),
                "별도 process가 네 번째 임무 저장에서 다섯 번째 임무로 이어지지 못했습니다.");

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
                    new CoreMapPoint(650, 900),
                    new CoreMapPoint(1050, 1050),
                    new CoreMapPoint(1650, 1050),
                    new CoreMapPoint(2100, 1050),
                    new CoreMapPoint(2600, 1100),
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
                _panel.AccessibilityName.Contains("보호정지 뒤에도 아침은 옵니다", StringComparison.Ordinal),
                "다섯 번째 임무의 보호정지 결과 이야기와 다음 결정 경계를 표시하지 못했습니다.");
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
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "범람 통제 운영 승인");
            await NextFrame();
            Require(coreRun.GetSnapshot().Chapter.ChapterId == "SHUT_DOWN_TO_KEEP" &&
                coreRun.GetSnapshot().ChapterResults[^1].EmergencyAssetIds.Count > 0 &&
                coreRun.GetSnapshot().ThermalMemory.All(item => !item.ProtectiveOutage) &&
                coreRun.GetSnapshot().Chapter.ResetThermalMemoryAtStart &&
                coreRun.GetSnapshot().Chapter.Briefing.Title.Contains("3주 뒤", StringComparison.Ordinal),
                "작성된 장간 시간경과가 일곱 번째 임무의 열 상태를 복귀시키지 못했습니다.");

            await PressPanelAsync(
                CommercialPanelAction.CycleLineClass,
                "계획정지 보강 선종 선택");
            Require(_lineClassId == ReinforcedLineClassId &&
                _poleClassId == ReinforcedPoleClassId,
                "계획정지 임무의 보강 선로와 보강 전신주를 선택하지 못했습니다.");

            await BuildCampaignSmokeLine(
                "PLAYER_SUBSTATION_2",
                "PLAYER_POLE_14",
                Array.Empty<CoreMapPoint>(),
                "계획정지 변전소 연계");
            Require(_panel.AccessibilityName.Contains("3주 뒤", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("모든 설비가 연속 운전 가능 상태로 복귀", StringComparison.Ordinal),
                "일곱 번째 임무 화면이 작성된 장간 열 상태 복귀를 알리지 못했습니다.");
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
                "PLAYER_POLE_14",
                Array.Empty<CoreMapPoint>(),
                "가장 긴 밤 의료원 연계");
            await PressPanelAsync(CommercialPanelAction.DeferPromise, "마지막 야간 증산 약속 미룸");
            Require(coreRun.PreviewDecisionWindow().Accepted,
                "여덟 번째 임무의 폭염 운영안을 승인할 수 없습니다.");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "마지막 폭염 운영 승인");
            await NextFrame();
            Require(coreRun.GetSnapshot().DecisionWindowIndex == 1 &&
                _panel.AccessibilityName.Contains("강변 통제와 서부 전원 정지", StringComparison.Ordinal),
                "여덟 번째 임무의 복합재난 전환 이야기를 표시하지 못했습니다.");
            Require(coreRun.PreviewDecisionWindow().Accepted,
                "여덟 번째 임무의 복합재난 운영안을 승인할 수 없습니다.");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "마지막 복합재난 운영 승인");
            await NextFrame();

            CommercialCoreSnapshot complete = coreRun.GetSnapshot();
            Require(complete.CampaignComplete && complete.ChapterResults.Count == 8 &&
                complete.ChapterStartCommandCounts.Count == 8 &&
                _panel.AccessibilityName.Contains("여덟 임무 실제 기록", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("약속 미룸", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("잔액", StringComparison.Ordinal),
                "여덟 임무를 실제 사실 비교 기록으로 완료하지 못했습니다.");
            await PressPanelAsync(CommercialPanelAction.ApproveWindow, "완주 에필로그 열기");
            await NextFrame();
            Require(_showEpilogue &&
                _panel.AccessibilityName.Contains("청류시 전력망 운영 인계", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("원하는 장의 시작 상태", StringComparison.Ordinal),
                "완주 뒤 작성된 에필로그와 장 선택 안내를 표시하지 못했습니다.");
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
                _showEpilogue &&
                _panel.AccessibilityName.Contains("청류시 전력망 운영 인계", StringComparison.Ordinal) &&
                _panel.AccessibilityName.Contains("첫 불빛 · 안전 의무", StringComparison.Ordinal),
                "새 process가 완료 저장의 에필로그와 여덟 사실 기록을 복원하지 못했습니다.");
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
        string label)
    {
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
        }
        await SelectAndClickCandidate(end.Position, endNodeId);
        Require(
            _snapshot.LineDraft?.EndNodeId == endNodeId,
            $"{label} 경로를 완성하지 못했습니다: {_lastError}");
        await PressPanelAsync(CommercialPanelAction.Commission, $"{label} 공사 발주");
        await NextFrame();
        await PressPanelAsync(CommercialPanelAction.Commission, $"{label} 공사 완공");
        await NextFrame();
    }

    private async Task BuildCampaignSmokeSubstation(CoreMapPoint position, string label)
    {
        await PressPanelAsync(CommercialPanelAction.PlaceSubstation, $"{label} 도구");
        await NextFrame();
        await ClickMap(position);
        Require(
            _snapshot.NodeDraft?.Position == position,
            $"{label} 위치를 계획하지 못했습니다: {_lastError}");
        await PressPanelAsync(CommercialPanelAction.Commission, $"{label} 공사 발주");
        await NextFrame();
        await PressPanelAsync(CommercialPanelAction.Commission, $"{label} 공사 완공");
        await NextFrame();
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
        await MovePointer(point);
        for (int index = 0;
             index < _map.CandidateNodeIds.Count && _map.SelectedCandidateId != nodeId;
             index++)
        {
            await PressMapKey(Key.E, physical: Key.E);
        }
        Require(_map.SelectedCandidateId == nodeId,
            $"요청한 접속 후보를 선택할 수 없습니다: {nodeId}");
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
