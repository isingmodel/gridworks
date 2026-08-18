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

    private async void RunCommercialCoreSmoke()
    {
        try
        {
            await NextFrame();
            GetWindow().Size = new Vector2I(1280, 720);
            await NextFrame();
            ApplyUiScale(this, 1.25f);
            await NextFrame();
            if (_options.CoreSmokeLeg == CommercialCoreSmokeLeg.First)
            {
                await RunCommercialCoreSmokeFirstLeg();
            }
            else if (_options.CoreSmokeLeg == CommercialCoreSmokeLeg.Second)
            {
                await RunCommercialCoreSmokeSecondLeg();
            }
            else
            {
                throw new InvalidOperationException("상용 핵심 흐름 확인 단계가 없습니다.");
            }
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"상용 핵심 흐름 smoke 실패: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task RunCommercialCoreSmokeFirstLeg()
    {
        Require(
            _shell.Surface == CommercialShellSurface.Title &&
            _shell.GetActionButton(CommercialShellAction.Continue).Disabled,
            "첫 실행의 제목 화면에서 빈 저장 상태를 표시하지 못했습니다.");
        EmitShell(CommercialShellAction.NewGame, "새 게임");
        await NextFrame();
        await DismissStorySequence();
        Require(
            _coreSnapshot!.SegmentId == "FIRST_LIGHT_PRELUDE_SEGMENT" &&
            _coreSnapshot.CommandCount == 0,
            "새 게임이 첫 불빛 시작 상태를 열지 못했습니다.");

        EmitPanel(CommercialPanelAction.StartStandardLine, "일반 선로 선택");
        await NextFrame();
        CoreMapPoint[] preludePath =
        [
            new(800, 650),
            new(1050, 650),
            new(1600, 650),
            new(2100, 650),
        ];
        await SelectAndClickCandidate(new CoreMapPoint(250, 650), "WEST_SOURCE_NODE");
        foreach (CoreMapPoint point in preludePath)
        {
            await ClickMap(point);
        }
        await SelectAndClickCandidate(
            new CoreMapPoint(2580, 725),
            "EAST_RESIDENTIAL_TERMINAL");
        Require(
            _snapshot.LineDraft?.LineClassId == "STANDARD_LINE" &&
            _snapshot.LineDraft.IntermediatePoints.SequenceEqual(preludePath) &&
            _coreRun!.PreviewLineOrder().Accepted,
            "첫 불빛의 일반 선로 자유 좌표와 견적을 화면 흐름으로 확정하지 못했습니다.");
        EmitPanel(CommercialPanelAction.Commission, "첫 불빛 선로 발주");
        await NextFrame();
        EmitPanel(CommercialPanelAction.Commission, "첫 불빛 선로 완공");
        await NextFrame();
        EmitProduct(CommercialProductAction.ApproveWindow, "첫 공급 운영안 승인");
        await NextFrame();
        Require(
            _coreSnapshot.SegmentId == "CHAPTER_FIVE_SEGMENT" &&
            _coreSnapshot.LastOutcome?.ChapterId == "FIRST_LIGHT_PRELUDE" &&
            _shell.Surface == CommercialShellSurface.Result,
            "첫 불빛 결과와 본편 시작 상태를 같은 제품 흐름으로 열지 못했습니다.");
        await DismissStorySequence();
        await PressKey(Key.Escape);
        Require(_shell.Surface == CommercialShellSurface.Pause,
            "첫 실행에서 Esc로 일시정지 메뉴를 열지 못했습니다.");
        EmitShell(CommercialShellAction.SaveAndQuit, "저장하고 제목 화면으로");
        await NextFrame();
        Require(
            _shell.Surface == CommercialShellSurface.Title &&
            !_shell.GetActionButton(CommercialShellAction.Continue).Disabled,
            "첫 실행을 저장한 뒤 이어하기가 활성화되지 않았습니다.");
        RequirePersistedSnapshot(campaignComplete: false);
        GD.Print(
            "COMMERCIAL_CORE_SMOKE_LEG1_PASS " +
            $"segment={_coreSnapshot.SegmentId} commands={_coreSnapshot.CommandCount} " +
            $"save={_options.SmokeSavePath}");
    }

    private async Task RunCommercialCoreSmokeSecondLeg()
    {
        Require(
            _shell.Surface == CommercialShellSurface.Title &&
            !_shell.GetActionButton(CommercialShellAction.Continue).Disabled,
            "새 프로세스의 제목 화면에서 유효한 저장을 찾지 못했습니다.");
        EmitShell(CommercialShellAction.Continue, "이어하기");
        await NextFrame();
        await DismissStorySequence();
        Require(
            _coreSnapshot!.SegmentId == "CHAPTER_FIVE_SEGMENT" &&
            _coreSnapshot.PromiseDecision == CommercialPromiseDecision.Unset,
            "이어하기가 본편 결정 상태를 정확히 복원하지 못했습니다.");
        Require(
            _panel.GetActionButton(CommercialPanelAction.PlaceSubstation).Disabled &&
            !_panel.GetActionButton(CommercialPanelAction.StartStandardLine).Disabled &&
            !_panel.GetActionButton(CommercialPanelAction.StartLine).Disabled &&
            ControlInside(
                _panel,
                _panel.GetProductActionButton(CommercialProductAction.ApproveWindow)),
            "상용 핵심 구간의 기존 변전소·일반 선로·보강 선로 선택이 화면에 맞게 열리지 않았습니다.");

        EmitPromise(CommercialPromiseDecision.Keep, "도시 약속 지키기");
        await NextFrame();
        EmitPanel(CommercialPanelAction.StartStandardLine, "짧은 일반 회랑 선택");
        await NextFrame();
        await SelectAndClickCandidate(new CoreMapPoint(1450, 1500), "BRIDGE_SOUTH");
        await ClickMap(new CoreMapPoint(1950, 1750));
        await SelectAndClickCandidate(new CoreMapPoint(2500, 1800), "FACTORY_TERMINAL");
        Require(
            _snapshot.LineDraft?.LineClassId == "STANDARD_LINE" &&
            _coreRun!.PreviewLineOrder().Accepted,
            "이어온 본편에서 짧은 일반 회랑을 실제 지도 입력으로 계획하지 못했습니다.");
        EmitPanel(CommercialPanelAction.Commission, "본편 일반 선로 발주");
        await NextFrame();
        EmitPanel(CommercialPanelAction.Commission, "본편 일반 선로 완공");
        await NextFrame();
        EmitProduct(CommercialProductAction.ApproveWindow, "더운 저녁 운영안 승인");
        await NextFrame();
        Require(
            _coreSnapshot.CommittedPhases.Count == 1 &&
            _coreSnapshot.CurrentWindow?.WindowId == "BEFORE_NIGHT_RECOVERY" &&
            _thermalProjectionIndex == 0,
            "첫 운영 승인 뒤 다음 결정 경계와 projection을 갱신하지 못했습니다.");
        await DismissStorySequence();
        EmitProduct(CommercialProductAction.ApproveWindow, "야간 필수 공급 승인");
        await NextFrame();
        Require(
            _coreSnapshot.CampaignComplete &&
            _coreSnapshot.LastOutcome?.PromiseDecision == CommercialPromiseDecision.Keep &&
            _shell.Surface == CommercialShellSurface.Result &&
            _shell.StoryBodyText.Contains("안전 의무", StringComparison.Ordinal) &&
            _shell.StoryBodyText.Contains("보호정지", StringComparison.Ordinal),
            "완료 결과 카드가 실제 의무·비상 운전·다음 보호정지 사실을 회수하지 못했습니다.");
        RequirePersistedSnapshot(campaignComplete: true);
        await DismissStorySequence();
        await PressKey(Key.Escape);
        EmitShell(CommercialShellAction.SaveAndQuit, "완료 저장하고 제목 화면으로");
        await NextFrame();
        Require(_shell.Surface == CommercialShellSurface.Title,
            "완료 저장 뒤 제목 화면으로 돌아오지 못했습니다.");
        GD.Print(
            "COMMERCIAL_CORE_SMOKE_LEG2_PASS " +
            $"complete={_coreSnapshot.CampaignComplete} commands={_coreSnapshot.CommandCount} " +
            $"outcome={_coreSnapshot.LastOutcome!.ChapterId}");
    }

    private async Task DismissStorySequence()
    {
        int guard = 0;
        while (_shell.Surface is CommercialShellSurface.Story or CommercialShellSurface.Result)
        {
            if (++guard > 8)
            {
                throw new InvalidOperationException("이야기 카드 흐름이 종료되지 않습니다.");
            }
            _shell.StoryContinueButton.EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
        }
    }

    private void RequirePersistedSnapshot(bool campaignComplete)
    {
        CommercialCoreSaveLoadResult load = CommercialCoreSaveStore.Load(
            _options.SmokeSavePath!);
        Require(
            load.Status == CommercialCoreSaveLoadStatus.Loaded && load.Save is not null,
            "상용 저장 파일을 원자 저장 뒤 다시 읽지 못했습니다.");
        CommercialCoreSliceRun restored = CommercialCoreSaveCodec.Restore(
            _productData!.Slice,
            _productData.World,
            _productData.SliceSha256,
            _productData.WorldSha256,
            load.Save!);
        CommercialCoreSnapshot restoredSnapshot = restored.GetSnapshot();
        Require(
            restored.Commands.SequenceEqual(_coreRun!.Commands) &&
            restoredSnapshot.SegmentId == _coreSnapshot!.SegmentId &&
            restoredSnapshot.CashUnit == _coreSnapshot.CashUnit &&
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
