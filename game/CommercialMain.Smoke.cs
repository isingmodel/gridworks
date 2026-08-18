#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gridworks.Core.Release.V2;
using Godot;
using CoreMapPoint = Gridworks.Core.Release.V2.MapPoint;

namespace Gridworks.Game;

internal sealed partial class CommercialMain
{
    private const string ThermalWorldResource =
        "Gridworks.Game.EmbeddedData.release-world-v2.json";

    private void InitializeThermalSmokeMode()
    {
        _thermalWorld = CommercialWorldLoader.Load(
            ReadEmbeddedResourceBytes(ThermalWorldResource));
        var sequence = new ThermalSequenceRequest(
        [
            new ThermalIntervalRequest(
                "THERMAL_WITNESS_EMERGENCY",
                [new ThermalLoadRequest("WATERWORKS", 2800, ThermalPermission.EmergencyAllowed)],
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<ThermalLimitOverride>()),
            new ThermalIntervalRequest(
                "THERMAL_WITNESS_COOLING",
                [new ThermalLoadRequest("WATERWORKS", 2800, ThermalPermission.EmergencyAllowed)],
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<ThermalLimitOverride>()),
            new ThermalIntervalRequest(
                "THERMAL_WITNESS_RECOVERED",
                [new ThermalLoadRequest("WATERWORKS", 2000, ThermalPermission.ContinuousOnly)],
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<ThermalLimitOverride>()),
        ]);
        _thermalEvaluation = ThermalNetworkEvaluator.EvaluateSequence(
            _thermalWorld,
            sequence,
            ThermalState.Empty);
        _thermalProjectionIndex = 0;
        _selectedThermalAssetId = null;
        _session = new ConstructionSession(_thermalWorld.ToSpatialWorld());
        _snapshot = _session.GetSnapshot();
    }

    private async void RunThermalSmoke()
    {
        try
        {
            await NextFrame();
            GetWindow().Size = new Vector2I(1280, 720);
            await NextFrame();
            ApplyUiScale(this, 1.25f);
            await NextFrame();
            Require(
                GetWindow().Size == new Vector2I(1280, 720) &&
                ControlInside(_panel, _panel.GetProjectionButton(1)),
                "1280×720·UI 125%에서 열 국면 선택이 패널 밖으로 잘렸습니다.");
            Require(
                _thermalEvaluation?.Intervals.Count == 3 &&
                _panel.ProjectionText == "국면 1 / 3" &&
                _panel.GetProjectionButton(-1).Disabled &&
                !_panel.GetProjectionButton(1).Disabled,
                "첫 열 국면의 중립 표기와 이동 경계를 표시하지 못했습니다.");

            CoreMapPoint edgeSelectionPoint = new(2200, 525);
            await ClickMap(edgeSelectionPoint);
            ThermalAssetUsage emergency = CurrentThermalInterval().Assets.Single(item =>
                item.AssetId == "EDGE_WATER");
            Require(
                _selectedThermalAssetId == "EDGE_WATER" &&
                _map.SelectedThermalAssetId == "EDGE_WATER" &&
                _map.SelectedThermalState == ThermalOperatingState.Emergency,
                "실제 지도 클릭으로 열 선로를 선택하지 못했습니다.");
            Require(
                emergency.UsedKw == 2800 &&
                emergency.ContinuousKw == 2500 &&
                emergency.EmergencyKw == 3200 &&
                emergency.State == ThermalOperatingState.Emergency &&
                emergency.NextState == ThermalOperatingState.ProtectiveOutage,
                "첫 국면의 typed 사용·연속·비상·다음 상태가 예상과 다릅니다.");
            Require(
                _panel.SelectionText.Contains("도체", StringComparison.Ordinal) &&
                _panel.LimitsText.Contains("현재 사용 2,800 kW", StringComparison.Ordinal) &&
                _panel.LimitsText.Contains("연속 한계 2,500 kW", StringComparison.Ordinal) &&
                _panel.LimitsText.Contains("비상 한계 3,200 kW", StringComparison.Ordinal) &&
                _panel.StatusText.Contains("현재 상태 · 비상 운전", StringComparison.Ordinal) &&
                _panel.StatusText.Contains("다음 상태 · 보호정지", StringComparison.Ordinal),
                "선택 설비의 typed 열 결과를 전문 용어로 표시하지 못했습니다.");
            Require(
                _shell.HelpText.Contains("이중선과 사선", StringComparison.Ordinal) &&
                _shell.HelpText.Contains("점선", StringComparison.Ordinal) &&
                _shell.HelpText.Contains("교차선", StringComparison.Ordinal) &&
                _map.AccessibilityName.Contains("비상 운전", StringComparison.Ordinal),
                "열 overlay의 색 독립 패턴·아이콘·문장 안내가 빠졌습니다.");

            EmitProjection(1);
            await NextFrame();
            ThermalAssetUsage cooling = CurrentThermalInterval().Assets.Single(item =>
                item.AssetId == "EDGE_WATER");
            Require(
                _thermalProjectionIndex == 1 &&
                _panel.ProjectionText == "국면 2 / 3" &&
                _selectedThermalAssetId == "EDGE_WATER" &&
                cooling.UsedKw == 0 &&
                cooling.State == ThermalOperatingState.ProtectiveOutage &&
                cooling.NextState == ThermalOperatingState.Continuous &&
                _map.SelectedThermalState == ThermalOperatingState.ProtectiveOutage &&
                _panel.StatusText.Contains("현재 상태 · 보호정지", StringComparison.Ordinal),
                "국면 변경 뒤 보호정지와 다음 자동 복귀를 그대로 표시하지 못했습니다.");

            EmitProjection(1);
            await NextFrame();
            ThermalAssetUsage recovered = CurrentThermalInterval().Assets.Single(item =>
                item.AssetId == "EDGE_WATER");
            Require(
                _thermalProjectionIndex == 2 &&
                _panel.ProjectionText == "국면 3 / 3" &&
                recovered.UsedKw == 2000 &&
                recovered.State == ThermalOperatingState.Continuous &&
                recovered.NextState == ThermalOperatingState.Continuous &&
                _map.SelectedThermalState == ThermalOperatingState.Continuous &&
                _panel.GetProjectionButton(1).Disabled,
                "냉각 다음 국면의 연속 운전 복귀와 마지막 이동 경계를 표시하지 못했습니다.");

            GD.Print(
                "COMMERCIAL_THERMAL_SMOKE_PASS projections=3 " +
                $"asset={_selectedThermalAssetId} states=Emergency>ProtectiveOutage>Continuous");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"상용 열 운전 smoke 실패: {exception}");
            GetTree().Quit(1);
        }
    }

    private async void RunPlacementSmoke()
    {
        try
        {
            await NextFrame();
            GetWindow().Size = new Vector2I(1920, 1080);
            await NextFrame();
            CoreMapPoint resolutionPoint = new(913, 711);
            CommercialWorldPosition highResolutionRoundTrip = _map.WorldAtViewportPoint(
                _map.ViewportPointForWorld(resolutionPoint));
            Require(
                NearlyEqual(highResolutionRoundTrip.X, resolutionPoint.XUnit, 0.02d) &&
                NearlyEqual(highResolutionRoundTrip.Y, resolutionPoint.YUnit, 0.02d),
                "1920×1080 지도 변환이 같은 자유 좌표를 왕복하지 못했습니다.");
            GetWindow().Size = new Vector2I(1280, 720);
            await NextFrame();
            ApplyUiScale(this, 1.25f);
            await NextFrame();
            Require(
                GetWindow().Size == new Vector2I(1280, 720) &&
                ControlInside(_panel, _panel.GetActionButton(CommercialPanelAction.Commission)),
                "1280×720·UI 125%에서 핵심 공사 행동이 패널 밖으로 잘렸습니다.");

            EmitPanel(CommercialPanelAction.PlaceSubstation, "변전소 도구");
            await NextFrame();
            CoreMapPoint fractional = new(600, 627);
            await ClickMap(fractional);
            Require(_snapshot.NodeDraft?.Position == fractional,
                "설계 단위 사이의 자유 좌표가 지도 입력에서 그대로 유지되지 않았습니다.");
            await PressKey(Key.Escape);
            Require(_snapshot.Phase == ConstructionPhase.Ready && _snapshot.NodeDraft is null,
                "Esc가 작성 중인 변전소 계획을 먼저 취소하지 않았습니다.");

            EmitPanel(CommercialPanelAction.PlaceSubstation, "변전소 도구 다시 선택");
            await NextFrame();
            await RequireRejectedPlacement(
                new CoreMapPoint(1300, 900),
                ConstructionError.WaterFootprint,
                "수면 배치");
            await RequireRejectedPlacement(
                new CoreMapPoint(2580, 600),
                ConstructionError.BuildingFootprint,
                "건물 경계 접촉 배치");

            CoreMapPoint riskPoint = new(1900, 1500);
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

            ConstructionSnapshot beforePan = _session.GetSnapshot();
            Vector2 cameraBeforePan = _map.CameraCenter;
            await MiddleDrag(anchor, anchor + new Vector2(90f, 35f));
            Require(
                !_map.CameraCenter.IsEqualApprox(cameraBeforePan) &&
                Equals(_session.GetSnapshot(), beforePan),
                "지도 이동이 카메라만 바꾸지 않았거나 Core 상태를 변경했습니다.");
            await PressMapKey(Key.Home);
            Require(
                _map.ZoomIndex == 0 &&
                _map.CameraCenter.IsEqualApprox(new Vector2(1600f, 1000f)),
                "Home이 전체 보기와 지도 중심을 복원하지 못했습니다.");

            EmitPanel(CommercialPanelAction.StartLine, "접속 후보 확인용 선로 도구");
            await NextFrame();
            await MovePointer(new CoreMapPoint(300, 1000));
            Require(
                _map.CandidateNodeIds.SequenceEqual(
                    new[] { "WEST_AUXILIARY", "WEST_SOURCE" },
                    StringComparer.Ordinal),
                "화면 거리와 node ID 순서의 접속 후보가 안정적으로 만들어지지 않았습니다.");
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
                new(650, 850),
                new(1050, 800),
                new(1605, 800),
                new(2100, 800),
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
            Require(_snapshot.LineDraft?.IntermediatePoints.SequenceEqual(exactPath) == true,
                $"마지막 전신주 계획을 추가하지 못했습니다: {_lastError}");
            await SelectAndClickCandidate(
                new CoreMapPoint(2600, 800),
                "EAST_RESIDENTIAL_TERMINAL");
            CoreMapPoint movedNonLast = new(1047, 777);
            await DragMap(exactPath[1], movedNonLast);
            Require(
                _snapshot.LineDraft?.IntermediatePoints[1] == movedNonLast &&
                _snapshot.LineDraft.EndNodeId == "EAST_RESIDENTIAL_TERMINAL",
                "끝 접속점을 정한 뒤 중간 전신주를 실제 드래그 입력으로 옮기지 못했습니다.");
            await DragMap(movedNonLast, exactPath[1]);
            Require(
                _snapshot.LineDraft is not null &&
                _snapshot.LineDraft.IntermediatePoints.SequenceEqual(exactPath) &&
                _snapshot.LineDraft.EndNodeId == "EAST_RESIDENTIAL_TERMINAL",
                "강을 건너는 선로 계획의 정확한 자유 좌표가 유지되지 않았습니다. " +
                $"points={string.Join(';', _snapshot.LineDraft?.IntermediatePoints ?? Array.Empty<CoreMapPoint>())} " +
                $"end={_snapshot.LineDraft?.EndNodeId ?? "없음"} error={_lastError}");
            EmitPanel(CommercialPanelAction.Commission, "선로 공사 발주");
            await NextFrame();
            EmitPanel(CommercialPanelAction.Commission, "선로 공사 완공");
            await NextFrame();
            CoreMapPoint[] commissionedPositions = _snapshot.World.Nodes
                .Where(node => node.NodeId.StartsWith("PLAYER_POLE_", StringComparison.Ordinal))
                .OrderBy(node => node.NodeId, StringComparer.Ordinal)
                .Select(node => node.Position)
                .ToArray();
            Require(
                _snapshot.Phase == ConstructionPhase.Ready &&
                commissionedPositions.SequenceEqual(exactPath) &&
                _snapshot.World.Edges.Count(edge => edge.Commissioned) == exactPath.Length + 1,
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

    private async Task<IReadOnlyList<string>> DismissStorySequence()
    {
        var frames = new List<string>();
        int guard = 0;
        while (_shell.Surface is CommercialShellSurface.Story or CommercialShellSurface.Result)
        {
            if (++guard > 24)
            {
                throw new InvalidOperationException("이야기 카드 흐름이 종료되지 않습니다.");
            }
            frames.Add($"{_shell.StoryKindText}\n{_shell.StoryBodyText}");
            _shell.StoryContinueButton.EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
        }
        return frames;
    }

    private async void RunCommercialCampaignSmoke()
    {
        try
        {
            await NextFrame();
            GetWindow().Size = new Vector2I(1280, 720);
            await NextFrame();
            ApplyUiScale(this, 1.25f);
            await NextFrame();
            if (_options.CampaignSmokeLeg == CommercialCampaignSmokeLeg.First)
            {
                await RunCommercialCampaignSmokeFirstLeg();
            }
            else if (_options.CampaignSmokeLeg == CommercialCampaignSmokeLeg.Second)
            {
                await RunCommercialCampaignSmokeSecondLeg();
            }
            else
            {
                throw new InvalidOperationException("상용 캠페인 확인 단계가 없습니다.");
            }
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"상용 캠페인 smoke 실패: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task RunCommercialCampaignSmokeFirstLeg()
    {
        Require(
            _shell.Surface == CommercialShellSurface.Title &&
            _shell.GetActionButton(CommercialShellAction.Continue).Disabled,
            "첫 캠페인 프로세스가 빈 저장 제목 화면으로 시작하지 않았습니다.");
        EmitShell(CommercialShellAction.NewGame, "새 캠페인");
        await NextFrame();
        await DismissStorySequence();
        Require(
            _coreSnapshot!.Chapter.ChapterId == "FIRST_LIGHT" &&
            _coreSnapshot.CommandCount == 0,
            "새 캠페인이 첫 불빛의 빈 명령 기록으로 시작하지 않았습니다.");

        string eastSubstationId = await BuildCampaignNodeThroughUi(
            "SMALL_SUBSTATION",
            new CoreMapPoint(2250, 700),
            550,
            "첫 불빛 동부 변전소");
        await BuildCampaignLineThroughUi(
            "WEST_SOURCE_NODE",
            "STANDARD_LINE",
            [
                new CoreMapPoint(750, 650),
                new CoreMapPoint(1050, 650),
                new CoreMapPoint(1600, 650),
                new CoreMapPoint(2050, 650),
            ],
            eastSubstationId,
            "첫 불빛 동부 간선");
        await BuildCampaignLineThroughUi(
            eastSubstationId,
            "STANDARD_LINE",
            Array.Empty<CoreMapPoint>(),
            "EAST_RESIDENTIAL_TERMINAL",
            "첫 불빛 생활권 인입선");
        Require(
            _coreSnapshot.Projections.Single().Evaluation.Loads.Single(
                item => item.LoadId == "EAST_RESIDENTIAL").DeliveredKw == 800,
            "첫 불빛 화면 projection이 동부 생활권 공급을 표시하지 못했습니다.");
        EmitProduct(CommercialProductAction.ApproveWindow, "첫 불빛 운영안 승인");
        await NextFrame();
        Require(
            _coreSnapshot.Chapter.ChapterId == "SECOND_HEART" &&
            _titleLabel.Text.Contains(
                _coreSnapshot.Chapter.DisplayName,
                StringComparison.Ordinal) &&
            _shell.Surface == CommercialShellSurface.Result,
            "첫 불빛 결과와 두 번째 심장 전환을 같은 제품 흐름으로 열지 못했습니다.");
        await DismissStorySequence();

        string highSubstationId = await BuildCampaignNodeThroughUi(
            "SMALL_SUBSTATION",
            new CoreMapPoint(2200, 1250),
            550,
            "병원 고지대 변전소");
        string riverSubstationId = await BuildCampaignNodeThroughUi(
            "SMALL_SUBSTATION",
            new CoreMapPoint(2300, 1550),
            550,
            "병원 강변 변전소");
        await BuildCampaignLineThroughUi(
            "WEST_SOURCE_NODE",
            "STANDARD_LINE",
            [
                new CoreMapPoint(650, 900),
                new CoreMapPoint(1050, 900),
                new CoreMapPoint(1650, 900),
                new CoreMapPoint(2050, 900),
            ],
            highSubstationId,
            "병원 고지대 회랑");
        await BuildCampaignLineThroughUi(
            "WEST_SOURCE_NODE",
            "STANDARD_LINE",
            [
                new CoreMapPoint(650, 1080),
                new CoreMapPoint(1050, 1200),
                new CoreMapPoint(1150, 1500),
                new CoreMapPoint(1725, 1500),
                new CoreMapPoint(2050, 1550),
            ],
            riverSubstationId,
            "병원 강변 회랑");
        await BuildCampaignLineThroughUi(
            highSubstationId,
            "STANDARD_LINE",
            Array.Empty<CoreMapPoint>(),
            "HOSPITAL_TERMINAL",
            "병원 첫 접속 회선");
        Require(
            _coreSnapshot.ConnectionFailures.Single().CurrentConnections == 1 &&
            _panel.ObligationsText.Contains("접속 회선 1/2", StringComparison.Ordinal),
            "병원 첫 접속 뒤 typed 1/2 상태를 작업 패널에 표시하지 못했습니다.");
        await BuildCampaignLineThroughUi(
            riverSubstationId,
            "STANDARD_LINE",
            Array.Empty<CoreMapPoint>(),
            "HOSPITAL_TERMINAL",
            "병원 두 번째 접속 회선");
        Require(
            _coreSnapshot.ConnectionFailures.Count == 0 &&
            _panel.ObligationsText.Contains("접속 회선 2/2", StringComparison.Ordinal),
            "병원 두 접속 회선 완성 뒤 2/2 상태를 작업 패널에 표시하지 못했습니다.");
        EmitProjection(1);
        await NextFrame();
        CommercialPhaseProjection flood = _coreSnapshot.Projections[_thermalProjectionIndex];
        Require(
            flood.Phase.ActiveRiskAreaIds.Contains(
                "RIVER_FLOOD_ZONE",
                StringComparer.Ordinal) &&
            _map.ActiveRiskAreaIds.Contains(
                "RIVER_FLOOD_ZONE",
                StringComparer.Ordinal) &&
            flood.Evaluation.Loads.Single(item => item.LoadId == "HOSPITAL").DeliveredKw == 900 &&
            _panel.ProjectionText.Contains(flood.Phase.DisplayName, StringComparison.Ordinal),
            "범람 안전 차단시험 projection과 병원 공급 유지 결과를 같은 화면에 표시하지 못했습니다.");
        EmitProduct(CommercialProductAction.ApproveWindow, "두 번째 심장 운영안 승인");
        await NextFrame();
        Require(
            _coreSnapshot.Chapter.ChapterId == "SECOND_SOURCE" &&
            _coreSnapshot.CommandCount == _coreSnapshot.ChapterStartCommandCount,
            "두 번째 심장 승인 뒤 세 번째 장 시작 checkpoint로 전환하지 못했습니다.");
        await DismissStorySequence();
        await CompleteCampaignChaptersThreeAndFourThroughUi();
        Require(
            _coreSnapshot.Chapter.ChapterId == "WHOSE_MARGIN" &&
            _coreSnapshot.CompletedChapterOutcomes.Count == 4 &&
            _coreSnapshot.CommandCount == _coreSnapshot.ChapterStartCommandCount,
            "첫 프로세스가 네 장 결과를 거쳐 다섯 번째 장 시작 checkpoint에 도달하지 못했습니다.");
        RequireCampaignPersistedSnapshot(
            expectedChapterId: "WHOSE_MARGIN",
            campaignComplete: false);
        await PressKey(Key.Escape);
        EmitShell(CommercialShellAction.SaveAndQuit, "네 장 완료 저장");
        await NextFrame();
        Require(_shell.Surface == CommercialShellSurface.Title,
            "첫 캠페인 프로세스가 저장 뒤 제목 화면으로 돌아오지 못했습니다.");
        GD.Print(
            "COMMERCIAL_CAMPAIGN_SMOKE_LEG1_PASS " +
            $"chapter={_coreSnapshot.Chapter.ChapterId} commands={_coreSnapshot.CommandCount} " +
            $"save={_options.SmokeSavePath}");
    }

    private async Task CompleteCampaignChaptersThreeAndFourThroughUi()
    {
        Require(
            _coreSnapshot!.Chapter.ChapterId == "SECOND_SOURCE" &&
            _panel.GetActionButton(CommercialPanelAction.StartStandardLine).Visible &&
            !_panel.GetActionButton(CommercialPanelAction.StartStandardLine).Disabled &&
            _panel.GetActionButton(CommercialPanelAction.StartLine).Visible &&
            !_panel.GetActionButton(CommercialPanelAction.StartLine).Disabled,
            "두 번째 전원에서 일반선과 보강선의 typed 도구 노출을 함께 표시하지 못했습니다.");

        string eastSubstationId = NodeIdAt(new CoreMapPoint(2250, 700), "SMALL_SUBSTATION");
        string highSubstationId = NodeIdAt(new CoreMapPoint(2200, 1250), "SMALL_SUBSTATION");
        await BuildCampaignLineThroughUi(
            "SOUTH_SOURCE_NODE",
            "REINFORCED_LINE",
            [
                new CoreMapPoint(700, 1650),
                new CoreMapPoint(1150, 1650),
                new CoreMapPoint(1750, 1650),
                new CoreMapPoint(2050, 1450),
            ],
            highSubstationId,
            "남부 전원 주회랑");
        await BuildCampaignLineThroughUi(
            "HOSPITAL_TERMINAL",
            "REINFORCED_LINE",
            [
                new CoreMapPoint(2550, 1050),
                new CoreMapPoint(2550, 800),
            ],
            eastSubstationId,
            "의료원-동부 연계선",
            zoomPointIndex: 1);
        EmitProduct(CommercialProductAction.ApproveWindow, "두 번째 전원 운영안 승인");
        await NextFrame();
        Require(
            _coreSnapshot.Chapter.ChapterId == "NORTH_BANK_PROMISE" &&
            _coreSnapshot.LastOutcome?.ChapterId == "SECOND_SOURCE",
            "두 번째 전원 결과와 북안의 약속 전환을 표시하지 못했습니다.");
        _shell.StoryContinueButton.EmitSignal(BaseButton.SignalName.Pressed);
        await NextFrame();
        Require(
            _shell.Surface == CommercialShellSurface.Story &&
            _shell.StoryBodyText.Contains(
                FormatElapsedDuration(
                    _coreSnapshot.Chapter.TimeAdvanceBeforeChapterMinutes),
                StringComparison.Ordinal) &&
            _shell.StoryBodyText.Contains("이전 열 상태가 모두 해제", StringComparison.Ordinal),
            "장간 시간 경과와 열 상태 초기화 사실을 네 번째 장 시작 전에 알리지 못했습니다.");
        await DismissStorySequence();
        ScrollContainer infoScroll = _panel.GetNode<ScrollContainer>("%InfoScroll");
        Control instructionLabel = _panel.GetNode<Control>("%InstructionLabel");
        Control objectiveLabel = _panel.GetNode<Control>("%ObjectiveLabel");
        BaseButton startLineButton =
            _panel.GetActionButton(CommercialPanelAction.StartLine);
        infoScroll.ScrollVertical = 0;
        await NextFrame();
        Require(
            ControlInside(this, _panel) &&
            infoScroll.Size.Y >= 200f &&
            ControlInside(_panel, infoScroll) &&
            ControlInside(this, infoScroll) &&
            ControlInside(infoScroll, instructionLabel) &&
            ControlInside(infoScroll, objectiveLabel) &&
            ControlInside(
                _panel,
                _panel.GetProductActionButton(CommercialProductAction.ApproveWindow)) &&
            ControlInside(
                this,
                _panel.GetProductActionButton(CommercialProductAction.ApproveWindow)) &&
            ControlInside(
                _panel,
                _panel.GetActionButton(CommercialPanelAction.Commission)) &&
            ControlInside(
                this,
                _panel.GetActionButton(CommercialPanelAction.Commission)),
            "1280×720·UI 125%에서 네 번째 장의 안내 영역이나 고정 승인·발주 행동이 패널 밖으로 잘렸습니다.");
        startLineButton.GrabFocus();
        await NextFrame();
        Require(
            infoScroll.ScrollVertical > 0 &&
            ControlInside(infoScroll, startLineButton) &&
            ControlInside(_panel, startLineButton) &&
            ControlInside(this, startLineButton),
            "1280×720·UI 125%에서 스크롤된 공사 도구를 키보드 focus로 표시하지 못했습니다.");
        infoScroll.ScrollVertical = 0;
        _map.GrabFocus();
        await NextFrame();

        EmitPromise(CommercialPromiseDecision.Keep, "북안 입주 약속 지키기");
        await NextFrame();
        string northSubstationId = await BuildCampaignNodeThroughUi(
            "LARGE_SUBSTATION",
            new CoreMapPoint(2050, 400),
            850,
            "북안 대형 변전소");
        await BuildCampaignLineThroughUi(
            eastSubstationId,
            "REINFORCED_LINE",
            Array.Empty<CoreMapPoint>(),
            northSubstationId,
            "북안 보강 인입선");
        await BuildCampaignLineThroughUi(
            northSubstationId,
            "REINFORCED_LINE",
            [
                new CoreMapPoint(2100, 600),
                new CoreMapPoint(2500, 460),
            ],
            "NORTH_RESIDENTIAL_TERMINAL",
            "북안 생활권 인입선",
            zoomPointIndex: 0);
        Require(
            _snapshot.World.Edges.Count(edge =>
                edge.Commissioned &&
                (edge.FromNodeId == "NORTH_RESIDENTIAL_TERMINAL" ||
                 edge.ToNodeId == "NORTH_RESIDENTIAL_TERMINAL")) == 1 &&
            _coreSnapshot.Projections[0].Evaluation.Loads.Single(item =>
                item.LoadId == "NORTH_RESIDENTIAL").DeliveredKw == 900,
            "북안 생활권 접속선이 단말에 완공되거나 현재 운영안에 공급으로 반영되지 않았습니다.");
        await BuildCampaignLineThroughUi(
            northSubstationId,
            "STANDARD_LINE",
            Array.Empty<CoreMapPoint>(),
            "WATER_TERMINAL",
            "정수장 접속선");
        await ClickMap(new CoreMapPoint(2050, 400));
        Require(
            _selectedThermalAssetId == northSubstationId &&
            _map.SelectedServiceArea?.RadiusUnit == 850 &&
            _map.SelectedServiceArea.Label.Contains("연결 회선 3/", StringComparison.Ordinal),
            "선택한 북안 변전소의 서비스 권역·점유영역·향후 접속 여유 표현을 열지 못했습니다.");
        EmitProjection(1);
        await NextFrame();
        CommercialPhaseProjection forecast = _coreSnapshot.Projections[_thermalProjectionIndex];
        Require(
            forecast.Evaluation.Loads.Single(
                item => item.LoadId == "WATERWORKS").DeliveredKw == 900 &&
            forecast.Evaluation.Loads.Single(
                item => item.LoadId == "NORTH_RESIDENTIAL").DeliveredKw == 1100,
            "북안의 약속 예고 국면에서 정수장과 북안 생활권 공급을 표시하지 못했습니다.");
        EmitProduct(CommercialProductAction.ApproveWindow, "북안의 약속 운영안 승인");
        await NextFrame();
        Require(
            !_coreSnapshot.CampaignComplete &&
            _coreSnapshot.Chapter.ChapterId == "WHOSE_MARGIN" &&
            _coreSnapshot.CompletedChapterOutcomes.Count == 4 &&
            _coreSnapshot.LastOutcome?.PromiseDecision == CommercialPromiseDecision.Keep &&
            _shell.Surface == CommercialShellSurface.Result &&
            _coreSnapshot.LastOutcome.RenderedFacts.Count > 0 &&
            _coreSnapshot.LastOutcome.RenderedFacts.All(fact =>
                _shell.StoryBodyText.Contains(fact, StringComparison.Ordinal)),
            "네 번째 장 결과가 실제 약속·공급 facts를 회수하거나 다섯 번째 장으로 전환하지 못했습니다.");
        await DismissStorySequence();
    }

    private async Task RunCommercialCampaignSmokeSecondLeg()
    {
        Require(
            _shell.Surface == CommercialShellSurface.Title &&
            !_shell.GetActionButton(CommercialShellAction.Continue).Disabled,
            "두 번째 캠페인 프로세스가 첫 프로세스의 저장을 찾지 못했습니다.");
        EmitShell(CommercialShellAction.Continue, "캠페인 이어하기");
        await NextFrame();
        await DismissStorySequence();
        Require(
            _coreSnapshot!.Chapter.ChapterId == "WHOSE_MARGIN" &&
            _titleLabel.Text.Contains(
                _coreSnapshot.Chapter.DisplayName,
                StringComparison.Ordinal) &&
            _coreSnapshot.CompletedChapterOutcomes.Count == 4 &&
            _coreSnapshot.CommandCount == _coreSnapshot.ChapterStartCommandCount,
            "fresh process restore가 다섯 번째 장 시작 checkpoint를 보존하지 못했습니다.");
        RequireCampaignPersistedSnapshot(
            expectedChapterId: "WHOSE_MARGIN",
            campaignComplete: false);
        Require(
            _panel.GetActionButton(CommercialPanelAction.StartStandardLine).Visible &&
            !_panel.GetActionButton(CommercialPanelAction.StartStandardLine).Disabled &&
            _panel.GetActionButton(CommercialPanelAction.StartLine).Visible &&
            !_panel.GetActionButton(CommercialPanelAction.StartLine).Disabled,
            "다섯 번째 장에서 일반선과 보강선의 typed 도구를 함께 표시하지 못했습니다.");

        string hospitalSecondSubstationId = NodeIdAt(
            new CoreMapPoint(2300, 1550),
            "SMALL_SUBSTATION");
        string hospitalHighSubstationId = NodeIdAt(
            new CoreMapPoint(2200, 1250),
            "SMALL_SUBSTATION");
        string northLargeSubstationId = NodeIdAt(
            new CoreMapPoint(2050, 400),
            "LARGE_SUBSTATION");

        EmitPromise(CommercialPromiseDecision.Keep, "산업 야간 증산 약속 지키기");
        await NextFrame();
        await BuildCampaignLineThroughUi(
            hospitalSecondSubstationId,
            "STANDARD_LINE",
            Array.Empty<CoreMapPoint>(),
            "FACTORY_TERMINAL",
            "산업단지 일반 회랑");
        await SelectCampaignProjectionThroughUi("NIGHT_SHIFT");
        CommercialPhaseProjection nightShift = _coreSnapshot.Projections.Single(item =>
            item.Phase.PhaseId == "NIGHT_SHIFT");
        Require(
            nightShift.Evaluation.Loads.Single(item =>
                item.LoadId == "RIVER_FACTORY").DeliveredKw == 2700 &&
            nightShift.Evaluation.Assets.Any(item =>
                item.State == ThermalOperatingState.Emergency &&
                item.NextState == ThermalOperatingState.ProtectiveOutage),
            "야간 증산 projection이 실제 2,700 kW 공급과 다음 보호정지 설비를 표시하지 못했습니다.");
        EmitProduct(CommercialProductAction.ApproveWindow, "더운 저녁 운영안 승인");
        await NextFrame();
        Require(
            _coreSnapshot.Chapter.ChapterId == "WHOSE_MARGIN" &&
            _coreSnapshot.CurrentWindow?.WindowId == "LATE_NIGHT_RECOVERY_WINDOW",
            "야간 증산 승인 뒤 늦은 밤 복구 운영 단계로 전환하지 못했습니다.");
        await DismissStorySequence();
        CommercialPhaseProjection lateNight = _coreSnapshot.Projections.Single(item =>
            item.Phase.PhaseId == "LATE_NIGHT");
        Require(
            lateNight.Evaluation.Loads.Single(item =>
                item.LoadId == "HOSPITAL").DeliveredKw == 900 &&
            lateNight.Evaluation.Loads.Single(item =>
                item.LoadId == "WATERWORKS").DeliveredKw == 900,
            "보호정지 이후 늦은 밤의 의료원·정수장 공급을 표시하지 못했습니다.");
        EmitProduct(CommercialProductAction.ApproveWindow, "늦은 밤 복구 운영안 승인");
        await NextFrame();
        Require(
            _coreSnapshot.Chapter.ChapterId == "BEFORE_WATER_RISE" &&
            _coreSnapshot.LastOutcome?.ChapterId == "WHOSE_MARGIN" &&
            _coreSnapshot.LastOutcome.PromiseDecision == CommercialPromiseDecision.Keep,
            "다섯 번째 장의 약속·열 결과를 회수하고 여섯 번째 장으로 전환하지 못했습니다.");
        await DismissStorySequence();

        EmitPromise(CommercialPromiseDecision.Defer, "동부 연속공급 약속 미루기");
        await NextFrame();
        await BuildCampaignLineThroughUi(
            "SOUTH_SOURCE_NODE",
            "STANDARD_LINE",
            [
                new CoreMapPoint(450, 1200),
                new CoreMapPoint(650, 750),
                new CoreMapPoint(1040, 750),
                new CoreMapPoint(1620, 750),
                new CoreMapPoint(1900, 800),
            ],
            hospitalHighSubstationId,
            "남부-고지대 일반 회랑",
            zoomEveryPoint: true);
        await BuildCampaignLineThroughUi(
            northLargeSubstationId,
            "STANDARD_LINE",
            [
                new CoreMapPoint(1950, 500),
                new CoreMapPoint(2500, 600),
            ],
            "EAST_RESIDENTIAL_TERMINAL",
            "동부 두 번째 접속 회선",
            zoomEveryPoint: true);
        await SelectCampaignProjectionThroughUi("FLOOD_ARRIVAL");
        CommercialPhaseProjection flood = _coreSnapshot.Projections.Single(item =>
            item.Phase.PhaseId == "FLOOD_ARRIVAL");
        Require(
            _snapshot.World.Edges.Count(edge => edge.Commissioned &&
                (edge.FromNodeId == "EAST_RESIDENTIAL_TERMINAL" ||
                 edge.ToNodeId == "EAST_RESIDENTIAL_TERMINAL")) == 2 &&
            _panel.ObligationsText.Contains("접속 회선 2/2", StringComparison.Ordinal) &&
            flood.Phase.ActiveRiskAreaIds.Contains(
                "RIVER_FLOOD_ZONE",
                StringComparer.Ordinal) &&
            _map.ActiveRiskAreaIds.Contains(
                "RIVER_FLOOD_ZONE",
                StringComparer.Ordinal) &&
            flood.Evaluation.Loads.Single(item =>
                item.LoadId == "HOSPITAL").DeliveredKw == 900 &&
            flood.Evaluation.Loads.Single(item =>
                item.LoadId == "WATERWORKS").DeliveredKw == 900,
            "범람 도달 projection에서 두 접속 회선·위험구역·필수 공급을 함께 표시하지 못했습니다.");
        EmitProduct(CommercialProductAction.ApproveWindow, "물이 닿기 전 운영안 승인");
        await NextFrame();
        Require(
            _coreSnapshot.Chapter.ChapterId == "SWITCH_OFF_TO_PROTECT" &&
            _coreSnapshot.LastOutcome?.ChapterId == "BEFORE_WATER_RISE" &&
            _coreSnapshot.LastOutcome.PromiseDecision == CommercialPromiseDecision.Defer,
            "여섯 번째 장의 미룬 약속과 범람 결과를 회수하지 못했습니다.");
        await DismissStorySequence();

        Require(
            !_coreSnapshot.CanApprove &&
            _coreSnapshot.ConnectionFailures.Count == 1 &&
            _coreSnapshot.ConnectionFailures[0].NodeId == "WATER_TERMINAL" &&
            _coreSnapshot.ConnectionFailures[0].CurrentConnections == 1 &&
            _coreSnapshot.ConnectionFailures[0].RequiredConnections == 2 &&
            _panel.ObligationsText.Contains("접속 회선 1/2", StringComparison.Ordinal),
            "일곱 번째 장 시작에서 정수장 두 번째 접속 회선 의무를 표시하고 승인을 막지 못했습니다.");
        await BuildCampaignLineThroughUi(
            hospitalSecondSubstationId,
            "STANDARD_LINE",
            [
                new CoreMapPoint(1900, 1250),
                new CoreMapPoint(1800, 1050),
                new CoreMapPoint(1800, 700),
            ],
            "WATER_TERMINAL",
            "계획정지 대비 정수장 공유 회선");
        Require(
            _coreSnapshot.ConnectionFailures.Count == 0 &&
            _coreSnapshot.CanApprove &&
            _panel.ObligationsText.Contains("접속 회선 2/2", StringComparison.Ordinal),
            "정수장 두 번째 회선 완공 뒤 2/2 의무와 승인 가능 상태를 표시하지 못했습니다.");
        await SelectCampaignProjectionThroughUi("WEST_SOURCE_PLANNED_OUTAGE");
        CommercialPhaseProjection plannedOutage = _coreSnapshot.Projections.Single(item =>
            item.Phase.PhaseId == "WEST_SOURCE_PLANNED_OUTAGE");
        Require(
            plannedOutage.Evaluation.Loads.Single(item =>
                item.LoadId == "HOSPITAL").DeliveredKw == 1800 &&
            plannedOutage.Evaluation.Loads.Single(item =>
                item.LoadId == "WATERWORKS").DeliveredKw == 1400 &&
            plannedOutage.Evaluation.Assets.Any(item =>
                item.State == ThermalOperatingState.Emergency),
            "서부 전원 계획정지에서 필수 공급과 공유 회랑의 비상 운전을 표시하지 못했습니다.");
        await SelectCampaignProjectionThroughUi("WEST_SOURCE_RETURN_SERVICE");
        CommercialPhaseProjection returnedService = _coreSnapshot.Projections.Single(item =>
            item.Phase.PhaseId == "WEST_SOURCE_RETURN_SERVICE");
        Require(
            returnedService.Evaluation.Loads.Single(item =>
                item.LoadId == "HOSPITAL").DeliveredKw == 900 &&
            returnedService.Evaluation.Loads.Single(item =>
                item.LoadId == "WATERWORKS").DeliveredKw == 900,
            "서부 전원 복귀 국면의 의료원·정수장 900 kW를 표시하지 못했습니다.");
        EmitProduct(CommercialProductAction.ApproveWindow, "계획정지 운영안 승인");
        await NextFrame();
        Require(
            _coreSnapshot.Chapter.ChapterId == "LONGEST_NIGHT" &&
            _coreSnapshot.LastOutcome?.ChapterId == "SWITCH_OFF_TO_PROTECT",
            "일곱 번째 장 결과를 회수하고 마지막 장으로 전환하지 못했습니다.");
        await DismissStorySequence();

        string refugeSubstationId = await BuildCampaignNodeThroughUi(
            "SMALL_SUBSTATION",
            new CoreMapPoint(2400, 900),
            550,
            "야간 피난 소형 변전소");
        await BuildCampaignLineThroughUi(
            "WEST_SOURCE_NODE",
            "STANDARD_LINE",
            [
                new CoreMapPoint(650, 450),
                new CoreMapPoint(990, 400),
                new CoreMapPoint(1570, 400),
                new CoreMapPoint(1700, 850),
                new CoreMapPoint(1950, 1000),
            ],
            refugeSubstationId,
            "야간 피난 전원 회랑",
            zoomEveryPoint: true);
        await BuildCampaignLineThroughUi(
            refugeSubstationId,
            "STANDARD_LINE",
            Array.Empty<CoreMapPoint>(),
            "HOSPITAL_TERMINAL",
            "야간 피난 의료원 회선");
        await BuildCampaignLineThroughUi(
            refugeSubstationId,
            "STANDARD_LINE",
            [new CoreMapPoint(2350, 450)],
            "WATER_TERMINAL",
            "야간 피난 정수장 회선");
        await ClickMap(new CoreMapPoint(2400, 900));
        Require(
            _selectedThermalAssetId == refugeSubstationId &&
            _map.SelectedServiceArea?.RadiusUnit == 550 &&
            _map.SelectedServiceArea.Label.Contains("연결 회선 3/", StringComparison.Ordinal),
            "마지막 장의 소형 변전소 서비스 권역과 세 접속 회선을 표시하지 못했습니다.");

        await SelectCampaignProjectionThroughUi("MAX_DEMAND");
        CommercialPhaseProjection maxDemand = _coreSnapshot.Projections.Single(item =>
            item.Phase.PhaseId == "MAX_DEMAND");
        Require(
            maxDemand.Evaluation.Loads.Single(item =>
                item.LoadId == "HOSPITAL").DeliveredKw == 900 &&
            maxDemand.Evaluation.Loads.Single(item =>
                item.LoadId == "WATERWORKS").DeliveredKw == 900,
            "마지막 장 최대수요에서 의료원·정수장 공급을 표시하지 못했습니다.");
        await SelectCampaignProjectionThroughUi("HEATWAVE_PEAK");
        CommercialPhaseProjection heatwave = _coreSnapshot.Projections.Single(item =>
            item.Phase.PhaseId == "HEATWAVE_PEAK");
        Require(
            heatwave.Evaluation.Loads.Single(item =>
                item.LoadId == "HOSPITAL").DeliveredKw == 1600 &&
            heatwave.Evaluation.Loads.Single(item =>
                item.LoadId == "WATERWORKS").DeliveredKw == 1400 &&
            heatwave.Evaluation.Assets.Any(item =>
                item.State == ThermalOperatingState.Emergency &&
                item.NextState == ThermalOperatingState.ProtectiveOutage),
            "폭염 정점의 실제 필수 공급과 비상→보호정지 설비를 표시하지 못했습니다.");
        await SelectCampaignProjectionThroughUi("PROTECTIVE_STOP_FLOOD");
        CommercialPhaseProjection protectiveFlood = _coreSnapshot.Projections.Single(item =>
            item.Phase.PhaseId == "PROTECTIVE_STOP_FLOOD");
        Require(
            protectiveFlood.Phase.ActiveRiskAreaIds.Contains(
                "RIVER_FLOOD_ZONE",
                StringComparer.Ordinal) &&
            protectiveFlood.Evaluation.Loads.Single(item =>
                item.LoadId == "HOSPITAL").DeliveredKw == 900 &&
            protectiveFlood.Evaluation.Loads.Single(item =>
                item.LoadId == "WATERWORKS").DeliveredKw == 900 &&
            protectiveFlood.Evaluation.Assets.Any(item =>
                item.State == ThermalOperatingState.ProtectiveOutage),
            "보호정지와 범람 국면의 위험·필수 공급·보호정지 설비를 표시하지 못했습니다.");
        EmitProduct(CommercialProductAction.ApproveWindow, "마지막 운영안 승인");
        await NextFrame();
        Require(
            _coreSnapshot.CampaignComplete &&
            _coreSnapshot.CompletedChapterOutcomes.Count == 8 &&
            _coreSnapshot.LastOutcome?.ChapterId == "LONGEST_NIGHT" &&
            _coreSnapshot.Epilogue is not null &&
            _coreSnapshot.ChapterReplayOptions.Count == 8 &&
            _shell.Surface == CommercialShellSurface.Result &&
            _coreSnapshot.LastOutcome.RenderedFacts.Count > 0 &&
            _coreSnapshot.LastOutcome.RenderedFacts.All(fact =>
                _shell.StoryBodyText.Contains(fact, StringComparison.Ordinal)),
            "여덟 장 완료 결과와 typed 에필로그·재시작 지점을 함께 열지 못했습니다.");
        CommercialCampaignEpiloguePresentation epilogue = _coreSnapshot.Epilogue ??
            throw new InvalidOperationException("완료 smoke의 typed 에필로그가 없습니다.");
        int completedCommandCount = _coreSnapshot.CommandCount;
        IReadOnlyList<string> completionFrames = await DismissStorySequence();
        Require(
            completionFrames.Any(frame => frame.StartsWith(
                "에필로그\n",
                StringComparison.Ordinal)) &&
            completionFrames.Any(frame => frame.Contains(
                epilogue.CityReport.Body,
                StringComparison.Ordinal)) &&
            completionFrames.Any(frame => frame.Contains(
                epilogue.MedicalWitness.Body,
                StringComparison.Ordinal)) &&
            completionFrames.Any(frame => frame.Contains(
                epilogue.Closing.Body,
                StringComparison.Ordinal)) &&
            epilogue.ChapterFacts.SelectMany(fact => fact.SummaryLines).All(line =>
                completionFrames.Any(frame => frame.Contains(line, StringComparison.Ordinal))) &&
            epilogue.PromiseFacts.All(fact => completionFrames.Any(frame =>
                frame.Contains(fact.Line, StringComparison.Ordinal))),
            "에필로그가 Core의 장별 실제 facts·약속 문장과 고정 시작·현장·마감 카드를 모두 표시하지 못했습니다.");
        Require(
            _panel.ChapterReplayOptionCount == 8 &&
            _panel.ChapterReplayButton.Visible &&
            !_panel.ChapterReplayButton.Disabled,
            "완료 화면에서 여덟 장 시작 선택을 사용할 수 없습니다.");
        ScrollContainer completedInfoScroll =
            _panel.GetNode<ScrollContainer>("%InfoScroll");
        completedInfoScroll.ScrollVertical = 0;
        await NextFrame();
        _panel.ChapterReplayButton.GrabFocus();
        await NextFrame();
        Require(
            completedInfoScroll.ScrollVertical > 0 &&
            ControlInside(this, _panel) &&
            ControlInside(completedInfoScroll, _panel.ChapterReplayButton) &&
            ControlInside(_panel, _panel.ChapterReplayButton) &&
            ControlInside(this, _panel.ChapterReplayButton),
            "완료 화면의 장 시작 선택을 패널 스크롤과 키보드 focus로 표시하지 못했습니다.");
        RequireCampaignPersistedSnapshot(
            expectedChapterId: "LONGEST_NIGHT",
            campaignComplete: true);
        await PressKey(Key.Escape);
        EmitShell(CommercialShellAction.SaveAndQuit, "여덟 장 완료 저장");
        await NextFrame();
        Require(_shell.Surface == CommercialShellSurface.Title,
            "완료 저장 뒤 제목 화면으로 돌아오지 못했습니다.");
        RequireCampaignPersistedSnapshot(
            expectedChapterId: "LONGEST_NIGHT",
            campaignComplete: true);

        EmitShell(CommercialShellAction.Continue, "완료 저장 이어하기");
        await NextFrame();
        Require(
            _coreSnapshot.CampaignComplete &&
            _coreSnapshot.ChapterReplayOptions.Count == 8 &&
            _shell.Surface == CommercialShellSurface.Story &&
            _shell.StoryKindText == "에필로그",
            "fresh 완료 저장 이어하기가 에필로그 첫 카드와 장 재시작 권한을 복원하지 못했습니다.");
        await DismissStorySequence();
        Require(
            _panel.SelectChapterReplayOption("WHOSE_MARGIN") &&
            _panel.SelectedChapterReplayId == "WHOSE_MARGIN",
            "완료 후 대표 다섯 번째 장 시작 지점을 선택하지 못했습니다.");
        _panel.ChapterReplayButton.EmitSignal(BaseButton.SignalName.Pressed);
        await NextFrame();
        Require(
            !_coreSnapshot.CampaignComplete &&
            _coreSnapshot.Chapter.ChapterId == "WHOSE_MARGIN" &&
            _coreSnapshot.CompletedChapterOutcomes.Count == 4 &&
            _coreSnapshot.CommandCount == _coreSnapshot.ChapterStartCommandCount &&
            _coreSnapshot.ChapterReplayOptions.Count == 0 &&
            _shell.Surface == CommercialShellSurface.Story &&
            _shell.StoryKindText != "에필로그",
            "완료한 장 선택이 다섯 번째 장 시작 checkpoint로만 되돌아가지 못했습니다.");
        await DismissStorySequence();
        RequireCampaignPersistedSnapshot(
            expectedChapterId: "WHOSE_MARGIN",
            campaignComplete: false);
        GD.Print(
            "COMMERCIAL_CAMPAIGN_SMOKE_LEG2_PASS " +
            $"completedResume=True replay=WHOSE_MARGIN " +
            $"completedCommands={completedCommandCount} replayCommands={_coreSnapshot.CommandCount}");
    }

    private async Task SelectCampaignProjectionThroughUi(string phaseId)
    {
        int targetIndex = _coreSnapshot!.Projections.ToList().FindIndex(item =>
            string.Equals(item.Phase.PhaseId, phaseId, StringComparison.Ordinal));
        Require(targetIndex >= 0, $"요청한 운영 국면을 찾을 수 없습니다: {phaseId}");
        while (_thermalProjectionIndex < targetIndex)
        {
            EmitProjection(1);
            await NextFrame();
        }
        while (_thermalProjectionIndex > targetIndex)
        {
            EmitProjection(-1);
            await NextFrame();
        }
        Require(
            _coreSnapshot.Projections[_thermalProjectionIndex].Phase.PhaseId == phaseId &&
            _panel.ProjectionText.Contains(
                _coreSnapshot.Projections[targetIndex].Phase.DisplayName,
                StringComparison.Ordinal),
            $"요청한 운영 국면을 UI에 표시하지 못했습니다: {phaseId}");
    }

    private async Task<string> BuildCampaignNodeThroughUi(
        string nodeClassId,
        CoreMapPoint position,
        int expectedServiceRadiusUnit,
        string description)
    {
        HashSet<string> before = _snapshot.World.Nodes
            .Select(item => item.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        CommercialPanelAction action = nodeClassId switch
        {
            "SMALL_SUBSTATION" => CommercialPanelAction.PlaceSubstation,
            "LARGE_SUBSTATION" => CommercialPanelAction.PlaceLargeSubstation,
            _ => throw new InvalidOperationException(
                $"지원하지 않는 smoke 변전소 등급입니다: {nodeClassId}"),
        };
        EmitPanel(action, description);
        await NextFrame();
        await MovePointer(position);
        Require(
            _map.PointerServiceRadiusUnit == expectedServiceRadiusUnit,
            $"{description}: 포인터 서비스 권역 반경이 typed class 값과 다릅니다.");
        await ClickMap(position);
        Require(
            _snapshot.NodeDraft?.NodeClassId == nodeClassId &&
            _map.DraftServiceRadiusUnit == expectedServiceRadiusUnit,
            $"{description}: 변전소 초안과 서비스 권역을 같은 위치에 표시하지 못했습니다.");
        EmitPanel(CommercialPanelAction.Commission, $"{description} 발주");
        await NextFrame();
        EmitPanel(CommercialPanelAction.Commission, $"{description} 완공");
        await NextFrame();
        SpatialNodeDefinition built = _snapshot.World.Nodes.Single(item =>
            !before.Contains(item.NodeId));
        Require(
            built.ClassId == nodeClassId && built.Position == position && built.Commissioned,
            $"{description}: 실제 UI 공사가 요청한 등급·좌표로 완공되지 않았습니다.");
        return built.NodeId;
    }

    private async Task BuildCampaignLineThroughUi(
        string startNodeId,
        string lineClassId,
        IReadOnlyList<CoreMapPoint> points,
        string endNodeId,
        string description,
        int zoomPointIndex = -1,
        bool zoomEveryPoint = false)
    {
        CommercialPanelAction action = lineClassId switch
        {
            "STANDARD_LINE" => CommercialPanelAction.StartStandardLine,
            "REINFORCED_LINE" => CommercialPanelAction.StartLine,
            _ => throw new InvalidOperationException(
                $"지원하지 않는 smoke 선로 등급입니다: {lineClassId}"),
        };
        EmitPanel(action, description);
        await NextFrame();
        await SelectAndClickCandidate(NodePosition(startNodeId), startNodeId);
        Require(
            _snapshot.LineDraft?.StartNodeId == startNodeId,
            $"{description}: 시작 접속점을 초안에 반영하지 못했습니다. " +
            CampaignLineDraftDiagnostics());
        for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
        {
            CoreMapPoint point = points[pointIndex];
            bool zoomPoint = zoomEveryPoint || pointIndex == zoomPointIndex;
            if (zoomPoint)
            {
                Vector2 anchor = _map.ViewportPointForWorld(point);
                await WheelAt(anchor, MouseButton.WheelUp);
                await WheelAt(anchor, MouseButton.WheelUp);
            }
            int expectedPointCount =
                (_snapshot.LineDraft?.IntermediatePoints.Count ?? 0) + 1;
            await ClickMap(point);
            Require(
                _snapshot.Phase == ConstructionPhase.LineDrafting &&
                _snapshot.LineDraft is LineDraftSnapshot pointDraft &&
                pointDraft.EndNodeId is null &&
                pointDraft.IntermediatePoints.Count == expectedPointCount &&
                pointDraft.IntermediatePoints[^1] == point,
                $"{description}: 전신주 위치 ({point.XUnit},{point.YUnit})를 초안에 더하지 못했습니다. " +
                CampaignLineDraftDiagnostics());
            if (zoomPoint)
            {
                await PressMapKey(Key.Home);
            }
        }
        await SelectAndClickCandidate(NodePosition(endNodeId), endNodeId);
        Require(
            _snapshot.LineDraft is LineDraftSnapshot completedDraft &&
            completedDraft.LineClassId == lineClassId &&
            completedDraft.EndNodeId == endNodeId &&
            _coreRun!.PreviewLineOrder().Accepted,
            $"{description}: 실제 지도 입력으로 만든 선로 초안의 발주 견적이 거부됐습니다. " +
            CampaignLineDraftDiagnostics());
        EmitPanel(CommercialPanelAction.Commission, $"{description} 발주");
        await NextFrame();
        EmitPanel(CommercialPanelAction.Commission, $"{description} 완공");
        await NextFrame();
        Require(
            _snapshot.Phase == ConstructionPhase.Ready &&
            _snapshot.World.Edges.Any(edge =>
                edge.Commissioned &&
                ((edge.FromNodeId == startNodeId && edge.ToNodeId != startNodeId) ||
                 (edge.ToNodeId == startNodeId && edge.FromNodeId != startNodeId))),
            $"{description}: 선로 공사를 완공하지 못했습니다. " +
            CampaignLineDraftDiagnostics());
    }

    private string CampaignLineDraftDiagnostics() =>
        $"pointerError={_pointerError?.ToString() ?? "없음"} · " +
        $"lastError={(_lastError.Length == 0 ? "없음" : _lastError)} · " +
        $"phase={_snapshot.Phase} · " +
        $"end={_snapshot.LineDraft?.EndNodeId ?? "없음"} · " +
        $"points={_snapshot.LineDraft?.IntermediatePoints.Count ?? 0}";

    private CoreMapPoint NodePosition(string nodeId) => _snapshot.World.Nodes.Single(
        item => item.NodeId == nodeId).Position;

    private string NodeIdAt(CoreMapPoint position, string classId) =>
        _snapshot.World.Nodes.Single(item =>
            item.Position == position && item.ClassId == classId).NodeId;

    private void RequireCampaignPersistedSnapshot(
        string expectedChapterId,
        bool campaignComplete)
    {
        CommercialCampaignSaveLoadResult load = CommercialCampaignSaveStore.Load(
            _options.SmokeSavePath!);
        Require(
            load.Status == CommercialCampaignSaveLoadStatus.Loaded && load.Save is not null,
            "상용 캠페인 저장을 원자 쓰기 뒤 다시 읽지 못했습니다.");
        CommercialCampaignRun restored = CommercialCampaignSaveCodec.Restore(
            _productData!.Campaign,
            _productData.World,
            _productData.CampaignSha256,
            _productData.WorldSha256,
            load.Save!);
        CommercialCampaignSnapshot restoredSnapshot = restored.GetSnapshot();
        Require(
            restored.Commands.SequenceEqual(_coreRun!.Commands) &&
            restoredSnapshot.Chapter.ChapterId == expectedChapterId &&
            restoredSnapshot.CashUnit == _coreSnapshot!.CashUnit &&
            restoredSnapshot.Minute == _coreSnapshot.Minute &&
            restoredSnapshot.PromiseDecision == _coreSnapshot.PromiseDecision &&
            restoredSnapshot.CampaignComplete == campaignComplete &&
            restoredSnapshot.Construction.World.Nodes.SequenceEqual(
                _coreSnapshot.Construction.World.Nodes) &&
            restoredSnapshot.Construction.World.Edges.SequenceEqual(
                _coreSnapshot.Construction.World.Edges),
            "fresh restore가 명령·좌표·자금·시각·약속·완료 상태를 보존하지 못했습니다.");
    }

    private void EmitShell(CommercialShellAction action, string description)
    {
        BaseButton button = _shell.GetActionButton(action);
        if (!button.Visible || button.Disabled)
        {
            throw new InvalidOperationException($"필요한 shell 행동을 사용할 수 없습니다: {description}");
        }
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private void EmitPromise(CommercialPromiseDecision decision, string description)
    {
        BaseButton button = _panel.GetPromiseButton(decision);
        if (!button.Visible || button.Disabled)
        {
            throw new InvalidOperationException($"필요한 약속 선택을 사용할 수 없습니다: {description}");
        }
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private void EmitProduct(CommercialProductAction action, string description)
    {
        BaseButton button = _panel.GetProductActionButton(action);
        if (!button.Visible || button.Disabled)
        {
            throw new InvalidOperationException($"필요한 제품 행동을 사용할 수 없습니다: {description}");
        }
        button.EmitSignal(BaseButton.SignalName.Pressed);
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

    private void EmitPanel(CommercialPanelAction action, string description)
    {
        BaseButton button = _panel.GetActionButton(action);
        if (!button.Visible || button.Disabled)
        {
            throw new InvalidOperationException($"필요한 화면 행동을 사용할 수 없습니다: {description}");
        }
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private void EmitProjection(int direction)
    {
        BaseButton button = _panel.GetProjectionButton(direction);
        if (!button.Visible || button.Disabled)
        {
            throw new InvalidOperationException("요청한 열 국면으로 이동할 수 없습니다.");
        }
        button.EmitSignal(BaseButton.SignalName.Pressed);
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
