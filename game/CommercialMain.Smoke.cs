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

    private async void RunStageGLayoutSmoke()
    {
        try
        {
            await NextFrame();
            _productSavePath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"commercial-stage-g-layout-smoke-{Guid.NewGuid():N}.json");
            ReplaceProductRun(new CommercialCampaignRun(
                _productData!.Campaign,
                _productData.World));
            ShowProductTitle("단계 G modal focus 확인");
            await NextFrame();
            Button titleSettings = _shell.GetNode<Button>("%TitleSettingsButton");
            titleSettings.GrabFocus();
            titleSettings.EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
            Require(_shell.Surface == CommercialShellSurface.Settings,
                "제목 화면 설정 modal을 열지 못했습니다.");
            _shell.GetNode<Button>("%SettingsBackButton")
                .EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
            Require(
                _shell.Surface == CommercialShellSurface.Title &&
                GetViewport().GuiGetFocusOwner() == titleSettings,
                "설정 취소가 제목 화면의 opener focus를 복원하지 못했습니다.");
            titleSettings.EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
            titleSettings.Disabled = true;
            _shell.GetNode<Button>("%SettingsBackButton")
                .EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
            BaseButton stableTitleFocus = _shell.GetActionButton(
                CommercialShellAction.Continue);
            if (stableTitleFocus.Disabled)
            {
                stableTitleFocus = _shell.GetActionButton(CommercialShellAction.NewGame);
            }
            Require(
                _shell.Surface == CommercialShellSurface.Title &&
                GetViewport().GuiGetFocusOwner() == stableTitleFocus,
                "설정 opener가 비활성화되면 제목 화면의 stable focus를 복원하지 못했습니다.");
            titleSettings.Disabled = false;

            _shell.HideShell();
            await NextFrame();
            _shell.ShowPause(new CommercialPausePresentation(
                _coreSnapshot!.Chapter.DisplayName,
                "단계 G modal focus 확인",
                true,
                true));
            await NextFrame();
            Button pauseHelp = _shell.GetNode<Button>("%PauseHelpButton");
            pauseHelp.GrabFocus();
            pauseHelp.EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
            _shell.GetNode<Button>("%HelpBackButton")
                .EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
            Require(
                _shell.Surface == CommercialShellSurface.Pause &&
                GetViewport().GuiGetFocusOwner() == pauseHelp,
                "도움말 취소가 일시정지 화면의 opener focus를 복원하지 못했습니다.");
            pauseHelp.EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
            pauseHelp.Disabled = true;
            _shell.GetNode<Button>("%HelpBackButton")
                .EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
            Button stablePauseFocus = _shell.GetNode<Button>("%ResumeButton");
            Require(
                _shell.Surface == CommercialShellSurface.Pause &&
                GetViewport().GuiGetFocusOwner() == stablePauseFocus,
                "도움말 opener가 비활성화되면 일시정지 화면의 stable focus를 복원하지 못했습니다.");
            pauseHelp.Disabled = false;
            BaseButton returnToTitle = _shell.GetActionButton(
                CommercialShellAction.ReturnToTitle);
            returnToTitle.GrabFocus();
            returnToTitle.EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
            _shell.GetNode<Button>("%CancelConfirmButton")
                .EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
            Require(
                _shell.Surface == CommercialShellSurface.Pause &&
                GetViewport().GuiGetFocusOwner() == returnToTitle,
                "확인 취소가 일시정지 화면의 opener focus를 복원하지 못했습니다.");
            _shell.HideShell();
            await NextFrame();

            _helpButton.GrabFocus();
            _helpButton.EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
            _shell.GetNode<Button>("%HelpBackButton")
                .EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
            Require(
                _shell.Surface == CommercialShellSurface.Hidden &&
                GetViewport().GuiGetFocusOwner() == _helpButton,
                "게임 화면 도움말 취소가 바깥 opener focus를 복원하지 못했습니다.");
            _helpButton.EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
            _helpButton.Disabled = true;
            _shell.GetNode<Button>("%HelpBackButton")
                .EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
            Require(
                _shell.Surface == CommercialShellSurface.Hidden &&
                GetViewport().GuiGetFocusOwner() == _map,
                "게임 화면 opener가 비활성화되면 지도에 안정적으로 focus를 돌려주지 못했습니다.");
            _helpButton.Disabled = false;

            _settings = CommercialSettings.Default with
            {
                Fullscreen = false,
                ReduceMotion = true,
            };
            _shell.SetSettings(SettingsPresentation(_settings));
            ApplyProductSettings(_settings);
            Render();
            await NextFrame();

            CommercialCampaignCommandResult rejectedApproval = _coreRun!.Execute(
                CommercialCoreCommand.ApproveDecisionWindow());
            CommercialSupplyDiagnostic rejectedDiagnostic =
                rejectedApproval.Snapshot.FirstBlockingDiagnostic ??
                throw new InvalidOperationException("초기 승인 blocker 진단이 없습니다.");
            string rejectedApprovalDetail = FormatSupplyDiagnostic(rejectedDiagnostic);
            ApplyCore(
                rejectedApproval,
                "현재 운영안을 승인했습니다.",
                ApprovalRejectionDiagnostic(rejectedApproval));
            string terminalNodeId = _productData.World.Loads.Single(load =>
                load.LoadId == rejectedDiagnostic.LoadId).NodeId;
            Require(
                !rejectedApproval.Accepted &&
                (rejectedApproval.Error is CommercialCampaignRunError.SafetyDutyUnserved or
                    CommercialCampaignRunError.KeptPromiseUnserved or
                    CommercialCampaignRunError.FutureSafetyAtRisk) &&
                rejectedDiagnostic.FailureKind == ThermalFailureKind.NoTopologyPath &&
                _lastError == rejectedApprovalDetail &&
                !_lastError.Contains("배치 입력", StringComparison.Ordinal) &&
                _map.HighlightedLimitingAssetId is null &&
                _map.HighlightedNodeIds.Contains(terminalNodeId, StringComparer.Ordinal) &&
                _map.HighlightAccessibilitySummary.Contains(
                    rejectedDiagnostic.LoadDisplayName,
                    StringComparison.Ordinal),
                "승인 거부가 typed blocker 상세·NoTopology 수요 terminal 강조로 직접 이어지지 않았습니다.");
            CommercialPhaseComparisonRow noTopologyRow =
                _coreSnapshot.PhaseComparisonRows.Single(row =>
                    row.PhaseId == rejectedDiagnostic.PhaseId &&
                    row.LoadId == rejectedDiagnostic.LoadId);
            BaseButton noTopologyRowButton = _panel.GetPhaseComparisonButton(
                $"{noTopologyRow.PhaseId}:{noTopologyRow.LoadId}");
            noTopologyRowButton.GrabFocus();
            await PressKey(Key.Enter);
            Require(
                _selectedPhaseComparisonId ==
                    $"{noTopologyRow.PhaseId}:{noTopologyRow.LoadId}" &&
                noTopologyRow.FailureDiagnostic?.FailureKind ==
                    ThermalFailureKind.NoTopologyPath &&
                _map.HighlightedLimitingAssetId is null &&
                _map.HighlightedNodeIds.Contains(terminalNodeId, StringComparer.Ordinal) &&
                GetViewport().GuiGetFocusOwner() == noTopologyRowButton,
                "NoTopology 국면 행이 LoadId의 authoritative 수요 terminal을 강조하거나 행 focus를 유지하지 못했습니다.");

            CommercialPanelAction lineAction =
                _panel.GetActionButton(CommercialPanelAction.StartStandardLine).Visible
                    ? CommercialPanelAction.StartStandardLine
                    : CommercialPanelAction.StartLine;
            EmitPanel(lineAction, "단계 G 접속 후보 확인용 선로 도구");
            await NextFrame();
            CoreMapPoint sourcePoint = NodePosition("WEST_SOURCE_NODE");
            await MovePointer(sourcePoint);
            string candidateSummary = _map.SelectedCandidateSummary ?? string.Empty;
            Require(
                candidateSummary.Contains("후보", StringComparison.Ordinal) &&
                candidateSummary.Contains("발전 접속점", StringComparison.Ordinal) &&
                candidateSummary.Contains("위치", StringComparison.Ordinal) &&
                candidateSummary.Contains("Enter", StringComparison.Ordinal) &&
                _panel.SelectionText.Contains("후보", StringComparison.Ordinal),
                "단계 G 접속 후보가 종류·이름·위치·순서·Q/E/Enter 안내를 함께 표시하지 못했습니다.");

            long acceptedInputId = _placementInputSequence + 1;
            int acceptedOutcomesBefore = _placementOutcomePresentationCount;
            await SelectAndClickCandidate(sourcePoint, "WEST_SOURCE_NODE");
            Require(
                _snapshot.LineDraft?.StartNodeId == "WEST_SOURCE_NODE" &&
                _placementInputSequence == acceptedInputId &&
                _placementOutcomePresentationCount == acceptedOutcomesBefore + 1 &&
                _lastStatus.Contains($"배치 입력 #{acceptedInputId}", StringComparison.Ordinal) &&
                _lastStatus.Contains("적용", StringComparison.Ordinal) &&
                _lastStatus.Contains("현재 경로점 1곳", StringComparison.Ordinal) &&
                string.IsNullOrEmpty(_lastError) &&
                _map.SelectedCandidateSummary is null,
                "단계 G의 수락된 배치 입력 하나가 경로점 수를 포함한 결과 하나로 끝나지 않았습니다.");

            ConstructionSnapshot beforeRejectedInput = _snapshot;
            long rejectedInputId = _placementInputSequence + 1;
            int rejectedOutcomesBefore = _placementOutcomePresentationCount;
            await MovePointer(sourcePoint);
            Require(
                !_pointerAccepted &&
                _pointerError is ConstructionError &&
                _panel.SelectionText.Contains(_pointerMessage, StringComparison.Ordinal) &&
                _map.AccessibilityName.Contains(_pointerMessage, StringComparison.Ordinal),
                "이미 선택한 시작점의 typed 거부 판정을 클릭 전에 표시하지 못했습니다.");
            ConstructionError rejectedPlacementCause = _pointerError!.Value;
            await ClickMap(sourcePoint);
            Label errorLabel = _panel.GetNode<Label>("%ErrorLabel");
            Require(
                Equals(_snapshot, beforeRejectedInput) &&
                _placementInputSequence == rejectedInputId &&
                _placementOutcomePresentationCount == rejectedOutcomesBefore + 1 &&
                _lastError.Contains($"배치 입력 #{rejectedInputId}", StringComparison.Ordinal) &&
                _lastError.Contains("거부", StringComparison.Ordinal) &&
                _lastError.Contains(ErrorText(rejectedPlacementCause), StringComparison.Ordinal) &&
                _lastError.Contains("도시망·경로·초안은 바뀌지 않았습니다", StringComparison.Ordinal) &&
                !_lastError.Contains($"배치 입력 #{acceptedInputId}", StringComparison.Ordinal) &&
                !_lastStatus.Contains("적용", StringComparison.Ordinal) &&
                errorLabel.Text == _lastError,
                "거부된 배치 입력이 이전 성공과 섞였거나 UI 결과를 한 번보다 많이 만들었습니다.");

            CommercialApprovalChecklistItem constructionGate =
                _coreSnapshot!.ApprovalChecklist.Items.Single(item =>
                    item.Kind == CommercialApprovalGateKind.ConstructionReady);
            Require(!constructionGate.Passed,
                "작성 중 초안을 승인 체크리스트의 공사 blocker로 표시하지 못했습니다.");
            BaseButton constructionGateButton =
                _panel.GetApprovalChecklistButton(constructionGate.ItemId);
            constructionGateButton.GrabFocus();
            await PressKey(Key.Enter);
            Require(
                GetViewport().GuiGetFocusOwner() ==
                    _panel.GetActionButton(CommercialPanelAction.CancelDraft) &&
                _selectedApprovalChecklistId == constructionGate.ItemId &&
                _map.HighlightedNodeIds.Count == 0 &&
                _map.HighlightedEdgeIds.Count == 0 &&
                _map.HighlightedLimitingAssetId is null &&
                string.IsNullOrEmpty(_map.HighlightAccessibilitySummary),
                "비경로 공사 blocker를 눌렀을 때 발주·취소 행동으로 focus가 이어지지 않았습니다.");

            CommercialApprovalChecklistItem diagnosticGate =
                _coreSnapshot.ApprovalChecklist.Items.First(item =>
                    item.FailureDiagnostic is not null);
            CommercialSupplyDiagnostic diagnostic = diagnosticGate.FailureDiagnostic!;
            string? expectedDiagnosticAsset = diagnostic.LimitingAssetId;
            string? expectedDiagnosticTerminal =
                diagnostic.FailureKind == ThermalFailureKind.NoTopologyPath
                    ? _productData.World.Loads.Single(load =>
                        load.LoadId == diagnostic.LoadId).NodeId
                    : null;
            BaseButton diagnosticButton =
                _panel.GetApprovalChecklistButton(diagnosticGate.ItemId);
            diagnosticButton.GrabFocus();
            await PressKey(Key.Enter);
            int diagnosticProjection = ProjectionIndexForPhase(
                _coreSnapshot,
                diagnostic.PhaseId);
            Require(
                diagnosticProjection >= 0 &&
                _thermalProjectionIndex == diagnosticProjection &&
                GetViewport().GuiGetFocusOwner() == diagnosticButton &&
                _map.HighlightAccessibilitySummary.Contains(
                    diagnostic.LoadDisplayName,
                    StringComparison.Ordinal) &&
                _map.HighlightedLimitingAssetId == expectedDiagnosticAsset &&
                (expectedDiagnosticTerminal is null ||
                 _map.HighlightedNodeIds.Contains(
                     expectedDiagnosticTerminal,
                     StringComparer.Ordinal)),
                "승인 blocker를 눌렀을 때 정확한 국면·수요·첫 제한 설비를 표시하거나 행 focus를 유지하지 못했습니다.");

            CoreMapPoint[] linePoints =
            [
                new(750, 650),
                new(1050, 650),
                new(1600, 650),
                new(2050, 650),
            ];
            foreach (CoreMapPoint point in linePoints)
            {
                await ClickMap(point);
            }
            await SelectAndClickCandidate(
                NodePosition("EAST_RESIDENTIAL_TERMINAL"),
                "EAST_RESIDENTIAL_TERMINAL");

            const string projectedDraftEdgeId = "PLAYER_EDGE_2";
            CommercialPhaseComparisonRow projectedDraftRow =
                _coreSnapshot!.PhaseComparisonRows.FirstOrDefault(row =>
                    row.PathEdgeIds.Contains(
                        projectedDraftEdgeId,
                        StringComparer.Ordinal))
                ?? throw new InvalidOperationException(
                    "완성된 현재 선로 초안이 실제 공급 경로에 들어간 국면 행이 없습니다.");
            CommercialPhaseProjection projectedDraftPhase =
                _coreSnapshot.Projections.Single(item =>
                    item.Phase.PhaseId == projectedDraftRow.PhaseId);
            SpatialWorldDefinition projectedDraftWorld =
                projectedDraftPhase.ProjectedWorld ??
                throw new InvalidOperationException(
                    "현재 선로 초안을 포함한 projected spatial world가 없습니다.");
            SpatialEdgeDefinition projectedDraftEdge =
                projectedDraftWorld.Edges.Single(edge =>
                    edge.EdgeId == projectedDraftEdgeId);
            Require(
                !_snapshot.World.Edges.Any(edge =>
                    edge.EdgeId == projectedDraftEdgeId) &&
                projectedDraftPhase.Evaluation.Assets.Any(asset =>
                    asset.AssetId == projectedDraftEdgeId),
                "완성된 현재 초안의 PLAYER 선로가 live 편집 world와 분리된 projected 열 계산에 없습니다.");

            BaseButton projectedDraftRowButton = _panel.GetPhaseComparisonButton(
                $"{projectedDraftRow.PhaseId}:{projectedDraftRow.LoadId}");
            projectedDraftRowButton.GrabFocus();
            await PressKey(Key.Enter);
            Require(
                _map.ProjectionWorld?.Edges.Any(edge =>
                    edge.EdgeId == projectedDraftEdgeId) == true &&
                _map.HighlightedEdgeIds.Contains(
                    projectedDraftEdgeId,
                    StringComparer.Ordinal) &&
                _map.HighlightAccessibilitySummary.Contains(
                    projectedDraftRow.LoadDisplayName,
                    StringComparison.Ordinal) &&
                _map.AccessibilityName.Contains(
                    projectedDraftRow.LoadDisplayName,
                    StringComparison.Ordinal) &&
                (!projectedDraftPhase.EffectiveUnavailableEdgeIds.Contains(
                    projectedDraftEdgeId,
                    StringComparer.Ordinal) ||
                 _map.UnavailableEdgeIds.Contains(
                     projectedDraftEdgeId,
                     StringComparer.Ordinal)),
                "완성된 초안의 projected PLAYER 선로를 선택 국면의 지도 경로·사용불가 overlay 권위로 쓰지 못했습니다.");

            SpatialNodeDefinition projectedFrom = projectedDraftWorld.Nodes.Single(node =>
                node.NodeId == projectedDraftEdge.FromNodeId);
            SpatialNodeDefinition projectedTo = projectedDraftWorld.Nodes.Single(node =>
                node.NodeId == projectedDraftEdge.ToNodeId);
            CoreMapPoint projectedEdgeMidpoint = new(
                (projectedFrom.Position.XUnit + projectedTo.Position.XUnit) / 2,
                (projectedFrom.Position.YUnit + projectedTo.Position.YUnit) / 2);
            ConstructionSnapshot completedDraftBeforeThermalSelection = _snapshot;
            long placementSequenceBeforeThermalSelection = _placementInputSequence;
            await ClickMap(projectedEdgeMidpoint);
            Require(
                Equals(_snapshot, completedDraftBeforeThermalSelection) &&
                _placementInputSequence == placementSequenceBeforeThermalSelection &&
                _selectedThermalAssetId == projectedDraftEdgeId &&
                _map.SelectedThermalAssetId == projectedDraftEdgeId,
                "completed draft 편집 상태를 바꾸지 않고 projected PLAYER 선로를 지도에서 열 설비로 선택하지 못했습니다.");

            BaseButton restartWindow = _panel.GetProductActionButton(
                CommercialProductAction.RestartWindow);
            restartWindow.GrabFocus();
            EmitProduct(CommercialProductAction.RestartWindow, "복구 확인 미리보기");
            await NextFrame();
            Label confirmationBody = _shell.GetNode<Label>("%ConfirmBody");
            Require(
                _shell.Surface == CommercialShellSurface.Confirm &&
                confirmationBody.Text.Contains("작성 중 선로 초안", StringComparison.Ordinal) &&
                confirmationBody.Text.Contains("경로점", StringComparison.Ordinal) &&
                confirmationBody.Text.Contains("운영 자금", StringComparison.Ordinal) &&
                confirmationBody.Text.Contains("열 상태", StringComparison.Ordinal),
                "복구 확인이 폐기할 초안과 복원될 자금·시각·열 상태를 먼저 공개하지 못했습니다.");
            await PressKey(Key.Escape);
            Require(
                _shell.Surface == CommercialShellSurface.Hidden &&
                _snapshot.LineDraft?.EndNodeId == "EAST_RESIDENTIAL_TERMINAL" &&
                GetViewport().GuiGetFocusOwner() == restartWindow,
                "복구 확인 Esc가 배경 초안을 유지하고 게임 화면 opener focus로 돌아가지 못했습니다.");

            BaseButton storeNext = _panel.GetProductActionButton(
                CommercialProductAction.StoreNextProjectComparison);
            Require(!storeNext.Disabled,
                "끝 접속점까지 완성한 초안을 다음 계획 비교에 보관할 수 없습니다.");
            EmitProduct(
                CommercialProductAction.StoreNextProjectComparison,
                "명시적 다음 계획 비교 보관");
            await NextFrame();
            EmitPanel(CommercialPanelAction.CancelDraft, "보관한 다음 계획과 현재 초안 분리");
            await NextFrame();
            EmitPanel(CommercialPanelAction.PlaceSubstation, "현재 계획 비교용 변전소 초안");
            await NextFrame();
            await ClickMap(new CoreMapPoint(2250, 700));
            CommercialConstructionWindowForecast storedForecast =
                _coreRun!.PreviewConstructionWindowForecast(_nextProjectComparison);
            Label deadlineLabel = _panel.GetNode<Label>("%DeadlineLabel");
            Require(
                _nextProjectComparison is CommercialNextLineProjectPlan &&
                storedForecast.Steps.Count == 2 &&
                storedForecast.Steps[0].SequenceNumber == 1 &&
                storedForecast.Steps[1].SequenceNumber == 2 &&
                storedForecast.Steps[0].StepRole ==
                    CommercialConstructionForecastStepRole.CurrentDraft &&
                storedForecast.Steps[1].StepRole ==
                    CommercialConstructionForecastStepRole.ExplicitNextPlan &&
                storedForecast.Steps[0].Kind == ConstructionKind.Node &&
                storedForecast.Steps[1].Kind == ConstructionKind.Line &&
                deadlineLabel.Text.Contains("현재 공사", StringComparison.Ordinal) &&
                deadlineLabel.Text.Contains("다음 계획", StringComparison.Ordinal),
                "현재 공사와 명시적으로 보관한 다음 한 건의 누적 완료 경계를 표시하지 못했습니다.");
            EmitPanel(
                CommercialPanelAction.CancelDraft,
                "현재 초안을 취소해 보관한 다음 계획만 비교");
            await NextFrame();
            CommercialConstructionWindowForecast nextOnlyForecast =
                _coreRun.PreviewConstructionWindowForecast(_nextProjectComparison);
            Require(
                nextOnlyForecast.Steps.Count == 1 &&
                nextOnlyForecast.Steps[0].SequenceNumber == 1 &&
                nextOnlyForecast.Steps[0].StepRole ==
                    CommercialConstructionForecastStepRole.ExplicitNextPlan &&
                deadlineLabel.Text.Contains("1. 다음 계획", StringComparison.Ordinal) &&
                deadlineLabel.Text.Contains(
                    _nextProjectComparisonLabel,
                    StringComparison.Ordinal),
                "현재 공사가 없어 sequence 1인 명시적 다음 계획을 현재 공사로 오표시했습니다.");
            EmitProduct(
                CommercialProductAction.ClearNextProjectComparison,
                "다음 계획 비교 비우기");
            await NextFrame();
            Require(
                _nextProjectComparison is null &&
                _coreRun.PreviewConstructionWindowForecast().Steps.Count == 0 &&
                deadlineLabel.Text.Contains("다음 계획 · 비어 있음", StringComparison.Ordinal),
                "휘발성 다음 계획 비교 칸을 도시망과 분리해 명시적으로 비우지 못했습니다.");

            EmitPanel(lineAction, "단계 G 레이아웃 접속 후보 재확인");
            await NextFrame();
            await MovePointer(sourcePoint);

            (Vector2I Size, int UiScalePercent)[] layouts =
            [
                (new Vector2I(1280, 720), 100),
                (new Vector2I(1280, 720), 125),
                (new Vector2I(1920, 1080), 100),
                (new Vector2I(1920, 1080), 125),
            ];
            foreach ((Vector2I size, int uiScalePercent) in layouts)
            {
                GetWindow().Size = size;
                _settings = _settings with
                {
                    Fullscreen = false,
                    UiScalePercent = uiScalePercent,
                    ReduceMotion = true,
                };
                _shell.SetSettings(SettingsPresentation(_settings));
                ApplyProductSettings(_settings);
                Render();
                await NextFrame();
                await NextFrame();

                ScrollContainer infoScroll =
                    _panel.GetNode<ScrollContainer>("%InfoScroll");
                Control approvalSection =
                    _panel.GetNode<Control>("%ApprovalChecklistSection");
                BaseButton approve = _panel.GetProductActionButton(
                    CommercialProductAction.ApproveWindow);
                BaseButton commission = _panel.GetActionButton(
                    CommercialPanelAction.Commission);
                BaseButton focusTarget = _panel.GetActionButton(lineAction);
                approve.GrabFocus();
                await NextFrame();
                infoScroll.ScrollVertical = 0;
                await NextFrame();
                focusTarget.GrabFocus();
                await NextFrame();

                CommercialPhaseProjection projection =
                    _coreSnapshot!.Projections[_thermalProjectionIndex];
                double staticWeatherPhase = _map.WeatherAnimationPhase;
                await NextFrame();
                Require(
                    GetWindow().Size == size &&
                    ControlInside(this, _panel) &&
                    _panel.InfoViewportMinimumHeight >= 200f &&
                    infoScroll.Size.Y >= 200f &&
                    ControlInside(_panel, infoScroll) &&
                    ControlInside(_panel, approvalSection) &&
                    !infoScroll.IsAncestorOf(approvalSection) &&
                    ControlInside(_panel, approve) &&
                    ControlInside(_panel, commission),
                    $"{size.X}×{size.Y}·UI {uiScalePercent}%에서 200px 정보창 또는 고정 승인·발주 영역이 잘렸습니다.");
                Require(
                    GetViewport().GuiGetFocusOwner() == focusTarget,
                    $"{size.X}×{size.Y}·UI {uiScalePercent}%에서 도구 focus를 유지하지 못했습니다.");
                Require(
                    ControlInside(infoScroll, focusTarget),
                    $"{size.X}×{size.Y}·UI {uiScalePercent}%에서 focus-follow가 도구를 스크롤 viewport에 표시하지 못했습니다. " +
                    $"scroll={infoScroll.ScrollVertical} viewport={infoScroll.GetGlobalRect()} " +
                    $"target={focusTarget.GetGlobalRect()} visible={focusTarget.IsVisibleInTree()}");
                Require(
                    _panel.ToolStatusText.Contains("현재 도구", StringComparison.Ordinal),
                    $"{size.X}×{size.Y}·UI {uiScalePercent}%에서 sticky 현재 도구 문구가 사라졌습니다.");
                Require(
                    _panel.ApprovalChecklistText.Length > 0,
                    $"{size.X}×{size.Y}·UI {uiScalePercent}%에서 승인 체크리스트가 사라졌습니다.");
                Require(
                    _panel.PhaseComparisonText.Contains("|", StringComparison.Ordinal),
                    $"{size.X}×{size.Y}·UI {uiScalePercent}%에서 수요×국면 비교 표가 사라졌습니다.");
                Require(
                    _map.SelectedCandidateSummary?.Contains("후보", StringComparison.Ordinal) ?? false,
                    $"{size.X}×{size.Y}·UI {uiScalePercent}%에서 지도 후보 피드백이 사라졌습니다.");
                Require(
                    _map.ReduceMotion &&
                    _map.VisualWeather == WeatherProfile(projection) &&
                    _map.VisualHeatStress == HasThermalHeatStress(projection) &&
                    _map.UnavailableNodeIds.SequenceEqual(
                        projection.EffectiveUnavailableNodeIds,
                        StringComparer.Ordinal) &&
                    _map.UnavailableEdgeIds.SequenceEqual(
                        projection.EffectiveUnavailableEdgeIds,
                        StringComparer.Ordinal) &&
                    _map.CityFacilityCount == _productData.World.Loads.Count &&
                    NearlyEqual(staticWeatherPhase, 0d, 0.000001d) &&
                    NearlyEqual(_map.WeatherAnimationPhase, staticWeatherPhase, 0.000001d) &&
                    _map.AccessibilityName.Contains("도시 표현", StringComparison.Ordinal) &&
                    _map.AccessibilityName.Contains(
                        "사용불가 설비",
                        StringComparison.Ordinal) &&
                    !_map.AccessibilityName.Contains("정비", StringComparison.Ordinal),
                    $"{size.X}×{size.Y}·UI {uiScalePercent}%에서 typed 도시·날씨 또는 움직임 줄이기 정적 상태가 어긋났습니다.");

                bool resultPortrait = size.X == 1920;
                PresentStories(
                [
                    new CommercialStoryPresentation(
                        _coreSnapshot.Chapter.Briefing,
                        resultPortrait,
                        "계속"),
                ]);
                await NextFrame();
                TextureRect portrait = _shell.GetNode<TextureRect>("%StoryPortrait");
                Control storyPage = _shell.GetNode<Control>("%StoryPage");
                Require(
                    _shell.Surface == (resultPortrait
                        ? CommercialShellSurface.Result
                        : CommercialShellSurface.Story) &&
                    portrait.Visible &&
                    portrait.Texture is not null &&
                    portrait.AccessibilityName == "운영센터장 윤서진 초상" &&
                    ControlInside(storyPage, portrait) &&
                    ControlInside(_shell, portrait),
                    $"{size.X}×{size.Y}·UI {uiScalePercent}% 이야기·결과 초상과 접근성 설명이 modal 안에 유지되지 않았습니다.");
                _shell.StoryContinueButton.EmitSignal(BaseButton.SignalName.Pressed);
                await NextFrame();

                GD.Print(
                    $"COMMERCIAL_STAGE_G_LAYOUT_PASS size={size.X}x{size.Y} " +
                    $"ui={uiScalePercent} info={infoScroll.Size.Y:0} " +
                    $"weather={_map.VisualWeather} reduced_motion=static");
            }

            PresentStories(
            [
                new CommercialStoryPresentation(
                    new CommercialStoryCard(
                        "시스템",
                        "고정 fallback 확인",
                        "등록되지 않은 화자의 시스템 안내입니다."),
                    false,
                    "계속",
                    IsSystemWarning: true),
            ]);
            await NextFrame();
            TextureRect fallbackPortrait = _shell.GetNode<TextureRect>("%StoryPortrait");
            Require(
                !fallbackPortrait.Visible &&
                fallbackPortrait.Texture is null &&
                fallbackPortrait.AccessibilityName == "인물 초상 없음",
                "등록되지 않은 화자·시스템 경고의 초상 fallback을 유지하지 못했습니다.");
            _shell.StoryContinueButton.EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();

            GD.Print(
                "COMMERCIAL_STAGE_G_LAYOUT_SMOKE_PASS layouts=4 " +
                "placement_outcome=authoritative checklist=fixed phase_table=accessible " +
                "portrait=bounded reduce_motion=static");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"단계 G 화면·접근성 smoke 실패: {exception}");
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
            MapWorldPosition highResolutionRoundTrip = _map.WorldAtViewportPoint(
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
            MapWorldPosition beforeZoom = _map.WorldAtViewportPoint(anchor);
            await WheelAt(anchor, MouseButton.WheelUp);
            await WheelAt(anchor, MouseButton.WheelUp);
            MapWorldPosition afterZoom = _map.WorldAtViewportPoint(anchor);
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
            CoreMapPoint overlappingCandidates = new(300, 1000);
            await SelectCandidateAt(overlappingCandidates, "WEST_SOURCE");
            Require(
                _map.CandidateNodeIds.Count == 2 &&
                !_pointerAccepted &&
                _pointerError == ConstructionError.SameEndpoint &&
                _panel.SelectionText.Contains("서부 발전 접속점", StringComparison.Ordinal) &&
                _panel.SelectionText.Contains(_pointerMessage, StringComparison.Ordinal) &&
                _map.AccessibilityName.Contains("서부 발전 접속점", StringComparison.Ordinal) &&
                _map.AccessibilityName.Contains(_pointerMessage, StringComparison.Ordinal),
                "Q/E 다중 후보에서 시작점 후보의 invalid typed 판정을 panel과 지도 접근성에 표시하지 못했습니다.");
            await SelectCandidateAt(overlappingCandidates, "WEST_AUXILIARY");
            Require(
                _pointerAccepted &&
                _panel.SelectionText.Contains("서부 예비 접속점", StringComparison.Ordinal) &&
                _panel.SelectionText.Contains("접속 가능", StringComparison.Ordinal) &&
                _map.AccessibilityName.Contains("서부 예비 접속점", StringComparison.Ordinal) &&
                _map.AccessibilityName.Contains("접속 가능", StringComparison.Ordinal),
                "Q/E로 유효 후보를 바꾼 뒤 후보명과 authoritative 판정을 함께 갱신하지 못했습니다.");
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

    private async Task<IReadOnlyList<string>>
        DismissCompletedStorySequenceWithLayoutAssertions()
    {
        Require(
            GetWindow().Size == new Vector2I(1280, 720),
            "완료 이야기 레이아웃 검사는 1280×720 창에서 실행해야 합니다.");

        var frames = new List<string>();
        Control shellPanel = _shell.GetNode<Control>("Center/Panel");
        Control storyPage = _shell.GetNode<Control>("%StoryPage");
        Control storyKind = _shell.GetNode<Control>("%StoryKindLabel");
        Control storyHeader = storyPage.GetNode<Control>("StoryHeader");
        Label storyBody = _shell.GetNode<Label>("%StoryBodyLabel");
        ScrollContainer storyScroll = _shell.StoryBodyScroll;
        BaseButton storyContinue = _shell.StoryContinueButton;
        bool sawScrollableResult = false;
        bool sawScrollableEpilogue = false;
        bool sawKeyboardBoundariesResult = false;
        bool sawKeyboardBoundariesEpilogue = false;
        float longestBodyHeight = 0f;
        double longestBodyScrollRange = 0d;
        bool longestBodyBottomReachable = false;
        int guard = 0;

        while (_shell.Surface is CommercialShellSurface.Story or CommercialShellSurface.Result)
        {
            if (++guard > 24)
            {
                throw new InvalidOperationException("완료 이야기 카드 흐름이 종료되지 않습니다.");
            }

            await NextFrame();
            await NextFrame();

            Rect2 scrollRect = storyScroll.GetGlobalRect();
            Rect2 bodyTopRect = storyBody.GetGlobalRect();
            Rect2 continueRectBeforeScroll = storyContinue.GetGlobalRect();
            bool bodyTopVisible =
                bodyTopRect.Position.Y >= scrollRect.Position.Y - 1f &&
                bodyTopRect.Position.Y < scrollRect.End.Y;
            bool panelInsideShell = ControlInside(_shell, shellPanel);
            bool pageInsidePanel = ControlInside(shellPanel, storyPage);
            bool kindInsidePage = ControlInside(storyPage, storyKind);
            bool headerInsidePage = ControlInside(storyPage, storyHeader);
            bool scrollInsidePage = ControlInside(storyPage, storyScroll);
            bool continueInsidePage = ControlInside(storyPage, storyContinue);
            bool fixedSiblings =
                !storyScroll.IsAncestorOf(storyKind) &&
                !storyScroll.IsAncestorOf(storyHeader) &&
                !storyScroll.IsAncestorOf(storyContinue);
            Require(
                panelInsideShell &&
                pageInsidePanel &&
                kindInsidePage &&
                headerInsidePage &&
                scrollInsidePage &&
                continueInsidePage &&
                storyScroll.Size.Y > 0f &&
                fixedSiblings &&
                storyScroll.ScrollVertical == 0 &&
                storyBody.GetThemeFontSize("font_size") == 21 &&
                bodyTopVisible,
                "1280×720·UI 125% 완료 카드의 고정 머리말·본문 viewport·진행 버튼이 shell 안에 유지되지 않았습니다. " +
                $"panel={panelInsideShell} page={pageInsidePanel} kind={kindInsidePage} " +
                $"header={headerInsidePage} scroll={scrollInsidePage} " +
                $"continue={continueInsidePage} scroll_height={storyScroll.Size.Y:0.##} " +
                $"siblings={fixedSiblings} scroll_top={storyScroll.ScrollVertical} " +
                $"font={storyBody.GetThemeFontSize("font_size")} body_top={bodyTopVisible} " +
                $"shell_rect={_shell.GetGlobalRect()} panel_rect={shellPanel.GetGlobalRect()} " +
                $"page_rect={storyPage.GetGlobalRect()} kind_rect={storyKind.GetGlobalRect()} " +
                $"header_rect={storyHeader.GetGlobalRect()} scroll_rect={scrollRect} " +
                $"body_rect={bodyTopRect} continue_rect={continueRectBeforeScroll}");
            Require(
                !string.IsNullOrWhiteSpace(storyScroll.AccessibilityName) &&
                storyScroll.AccessibilityName.Contains("본문", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(storyScroll.AccessibilityDescription) &&
                storyScroll.AccessibilityDescription.Contains("방향키", StringComparison.Ordinal) &&
                storyScroll.AccessibilityDescription.Contains("Page Down", StringComparison.Ordinal) &&
                storyScroll.AccessibilityDescription.Contains("Home", StringComparison.Ordinal) &&
                storyScroll.AccessibilityDescription.Contains("End", StringComparison.Ordinal),
                "완료 카드 본문 스크롤의 한국어 접근성 이름·키 안내가 비어 있습니다.");
            Require(
                storyContinue.GetNodeOrNull<Control>(storyContinue.FocusNext) == storyScroll &&
                storyContinue.GetNodeOrNull<Control>(storyContinue.FocusPrevious) == storyScroll &&
                storyScroll.GetNodeOrNull<Control>(storyScroll.FocusNext) == storyContinue &&
                storyScroll.GetNodeOrNull<Control>(storyScroll.FocusPrevious) == storyContinue &&
                GetViewport().GuiGetFocusOwner() == storyContinue,
                "완료 카드의 본문 스크롤과 고정 진행 버튼 사이에서 keyboard focus를 순환하지 못했습니다.");
            await PressKey(Key.Tab);
            Require(
                GetViewport().GuiGetFocusOwner() == storyScroll,
                "완료 카드에서 Tab이 배경 UI 대신 본문 스크롤로 이동하지 않았습니다.");
            await PressKey(Key.Tab);
            Require(
                GetViewport().GuiGetFocusOwner() == storyContinue,
                "완료 카드에서 Tab이 본문과 고정 진행 버튼 안에서 순환하지 않았습니다.");

            VScrollBar verticalScrollBar = storyScroll.GetVScrollBar();
            double maximumScroll = Math.Max(
                0d,
                verticalScrollBar.MaxValue - verticalScrollBar.Page);
            bool scrollable = maximumScroll > 0.5d;
            bool isResult =
                _shell.Surface == CommercialShellSurface.Result &&
                string.Equals(_shell.StoryKindText, "결과", StringComparison.Ordinal);
            bool isEpilogue = string.Equals(
                _shell.StoryKindText,
                "에필로그",
                StringComparison.Ordinal);
            sawScrollableResult |= isResult && scrollable;
            sawScrollableEpilogue |= isEpilogue && scrollable;

            storyScroll.GrabFocus();
            await NextFrame();
            if (scrollable && (isResult || isEpilogue))
            {
                await PressKey(Key.Down);
                double afterDown = verticalScrollBar.Value;
                await PressKey(Key.Home);
                double afterHome = verticalScrollBar.Value;
                await PressKey(Key.Pagedown);
                double afterPageDown = verticalScrollBar.Value;
                await PressKey(Key.Up);
                double afterUp = verticalScrollBar.Value;
                await PressKey(Key.End);
                double afterEnd = verticalScrollBar.Value;
                await PressKey(Key.Pageup);
                double afterPageUp = verticalScrollBar.Value;
                bool keyboardBoundaries =
                    afterDown > 0d &&
                    Math.Abs(afterHome) <= 0.5d &&
                    afterPageDown > 0d &&
                    afterUp < afterPageDown &&
                    Math.Abs(afterEnd - maximumScroll) <= 2d &&
                    afterPageUp < afterEnd;
                Require(
                    keyboardBoundaries,
                    "완료 카드 본문이 방향키·Page Up/Down·Home/End 경계를 따르지 않았습니다. " +
                    $"down={afterDown:0.##} home={afterHome:0.##} " +
                    $"page_down={afterPageDown:0.##} up={afterUp:0.##} " +
                    $"end={afterEnd:0.##} page_up={afterPageUp:0.##} " +
                    $"maximum={maximumScroll:0.##}");
                sawKeyboardBoundariesResult |= isResult;
                sawKeyboardBoundariesEpilogue |= isEpilogue;
            }
            await PressKey(Key.End);
            await NextFrame();
            Rect2 bodyBottomRect = storyBody.GetGlobalRect();
            Rect2 continueRectAfterScroll = storyContinue.GetGlobalRect();
            bool bodyBottomReachable =
                bodyBottomRect.End.Y <= storyScroll.GetGlobalRect().End.Y + 2f &&
                (!scrollable || storyScroll.ScrollVertical > 0) &&
                Math.Abs(verticalScrollBar.Value - maximumScroll) <= 2d &&
                continueRectBeforeScroll.Position.DistanceSquaredTo(
                    continueRectAfterScroll.Position) <= 0.01f &&
                continueRectBeforeScroll.Size.DistanceSquaredTo(
                    continueRectAfterScroll.Size) <= 0.01f;
            Require(
                bodyBottomReachable &&
                ControlInside(storyPage, storyContinue),
                "완료 카드 본문의 마지막 줄까지 스크롤하거나 고정 진행 버튼을 유지하지 못했습니다.");

            if (storyBody.Size.Y > longestBodyHeight)
            {
                longestBodyHeight = storyBody.Size.Y;
                longestBodyScrollRange = maximumScroll;
                longestBodyBottomReachable = bodyBottomReachable;
            }

            frames.Add($"{_shell.StoryKindText}\n{_shell.StoryBodyText}");
            storyContinue.EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
        }

        Require(
            sawScrollableResult &&
            sawScrollableEpilogue &&
            sawKeyboardBoundariesResult &&
            sawKeyboardBoundariesEpilogue &&
            longestBodyHeight > 0f &&
            longestBodyScrollRange > 0.5d &&
            longestBodyBottomReachable,
            "실제 최종 결과와 에필로그의 긴 본문을 scrollable completion 카드로 검증하지 못했습니다.");
        GD.Print(
            "COMMERCIAL_COMPLETION_STORY_LAYOUT_PASS size=1280x720 ui=125 " +
            $"longest_body={longestBodyHeight:0} max_scroll={longestBodyScrollRange:0} " +
            "header=fixed continue=fixed body=top-to-bottom focus=contained");
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

        Require(
            _coreSnapshot.Projections.Count > 1 &&
            _thermalProjectionIndex == 0 &&
            _coreSnapshot.FirstBlockingDiagnostic is not null,
            "국면별 경로 전환을 확인할 두 국면 blocker 상태가 준비되지 않았습니다.");
        CommercialSupplyDiagnostic phaseABlocker =
            _coreSnapshot.FirstBlockingDiagnostic!;
        CommercialPhaseComparisonRow phaseARow =
            _coreSnapshot.PhaseComparisonRows.Single(row =>
                row.PhaseId == phaseABlocker.PhaseId &&
                row.LoadId == phaseABlocker.LoadId);
        BaseButton phaseARowButton = _panel.GetPhaseComparisonButton(
            $"{phaseARow.PhaseId}:{phaseARow.LoadId}");
        phaseARowButton.GrabFocus();
        await PressKey(Key.Enter);
        Require(
            _selectedPhaseComparisonId ==
                $"{phaseARow.PhaseId}:{phaseARow.LoadId}" &&
            _map.HighlightAccessibilitySummary.Contains(
                phaseABlocker.LoadDisplayName,
                StringComparison.Ordinal),
            "국면 A 행이 A의 typed blocker 경로를 지도에 표시하지 못했습니다.");
        EmitProjection(1);
        await NextFrame();
        CommercialPhaseProjection phaseB =
            _coreSnapshot.Projections[_thermalProjectionIndex];
        Require(
            phaseB.Phase.PhaseId != phaseARow.PhaseId &&
            _selectedApprovalChecklistId is null &&
            _selectedPhaseComparisonId is null &&
            _map.HighlightedNodeIds.Count == 0 &&
            _map.HighlightedEdgeIds.Count == 0 &&
            _map.HighlightedLimitingAssetId is null &&
            string.IsNullOrEmpty(_map.HighlightAccessibilitySummary) &&
            _lastStatus == $"{phaseB.Phase.DisplayName} 운영 국면을 표시했습니다.",
            "국면 A 행 선택 뒤 국면 B로 바꿨을 때 A 경로·기본 blocker·상태 문구가 남았습니다.");
        EmitProjection(-1);
        await NextFrame();
        Require(
            _thermalProjectionIndex == 0 &&
            _map.HighlightAccessibilitySummary.Contains(
                phaseABlocker.LoadDisplayName,
                StringComparison.Ordinal),
            "첫 blocker가 일치하는 국면으로 돌아왔을 때만 기본 경로를 복원하지 못했습니다.");

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
            flood.EffectiveUnavailableEdgeIds.Count >
                flood.Phase.UnavailableEdgeIds.Count &&
            _map.UnavailableNodeIds.SequenceEqual(
                flood.EffectiveUnavailableNodeIds,
                StringComparer.Ordinal) &&
            _map.UnavailableEdgeIds.SequenceEqual(
                flood.EffectiveUnavailableEdgeIds,
                StringComparer.Ordinal) &&
            _map.ActiveRiskAreaIds.Contains(
                "RIVER_FLOOD_ZONE",
                StringComparer.Ordinal) &&
            _map.VisualWeather == CommercialWeatherProfile.Storm &&
            _map.AccessibilityName.Contains(
                "사용불가 설비",
                StringComparison.Ordinal) &&
            !_map.AccessibilityName.Contains("정비", StringComparison.Ordinal) &&
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
            ControlInside(infoScroll, startLineButton),
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
        IReadOnlyList<CommercialAudioCue> scheduledStopCues = SelectApprovalCues(
            [nightShift.Evaluation],
            chapterCompleted: false,
            campaignComplete: false,
            completedChapterCount: _coreSnapshot.CompletedChapterOutcomes.Count);
        Require(
            nightShift.Evaluation.Loads.Single(item =>
                item.LoadId == "RIVER_FACTORY").DeliveredKw == 2700 &&
            nightShift.Evaluation.Assets.Any(item =>
                item.State == ThermalOperatingState.Emergency &&
                item.NextState == ThermalOperatingState.ProtectiveOutage) &&
            scheduledStopCues.Contains(CommercialAudioCue.Warning) &&
            !scheduledStopCues.Contains(CommercialAudioCue.ProtectiveStop),
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
        CommercialPhaseComparisonRow lateNightRow = _coreSnapshot.PhaseComparisonRows.First(row =>
            row.PhaseId == "LATE_NIGHT" && row.LoadId == "HOSPITAL");
        BaseButton lateNightRowButton = _panel.GetPhaseComparisonButton(
            $"{lateNightRow.PhaseId}:{lateNightRow.LoadId}");
        lateNightRowButton.GrabFocus();
        await PressKey(Key.Enter);
        IReadOnlyList<CommercialAudioCue> actualStopAndResultCues = SelectApprovalCues(
            [lateNight.Evaluation],
            chapterCompleted: true,
            campaignComplete: false,
            completedChapterCount: _coreSnapshot.CompletedChapterOutcomes.Count + 1);
        Require(
            lateNightRow.PhaseNumber > 1 &&
            _panel.ApprovalChecklistHeadingText.Contains(
                $"국면 {lateNightRow.PhaseNumber}/{lateNightRow.PhaseCount}",
                StringComparison.Ordinal) &&
            _thermalProjectionIndex == ProjectionIndexForPhase(
                _coreSnapshot,
                lateNightRow.PhaseId) &&
            GetViewport().GuiGetFocusOwner() == lateNightRowButton &&
            lateNightRow.PathNodeIds.Count > 0 &&
            _map.HighlightAccessibilitySummary.Contains("늦은 밤", StringComparison.Ordinal) &&
            lateNight.Evaluation.Loads.Single(item =>
                item.LoadId == "HOSPITAL").DeliveredKw == 900 &&
            lateNight.Evaluation.Loads.Single(item =>
                item.LoadId == "WATERWORKS").DeliveredKw == 900 &&
            lateNight.Evaluation.Assets.Any(item =>
                item.State == ThermalOperatingState.ProtectiveOutage) &&
            actualStopAndResultCues.Contains(CommercialAudioCue.ProtectiveStop) &&
            actualStopAndResultCues.Contains(CommercialAudioCue.Result),
            "두 번째 운영 단계의 절대 국면·행 focus·경로 또는 실제 보호정지+결과 cue를 표시하지 못했습니다.");
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
        IReadOnlyList<CommercialAudioCue> finalApprovalCues = SelectApprovalCues(
            [protectiveFlood.Evaluation],
            chapterCompleted: true,
            campaignComplete: true,
            completedChapterCount: 8);
        Require(
            protectiveFlood.Phase.ActiveRiskAreaIds.Contains(
                "RIVER_FLOOD_ZONE",
                StringComparer.Ordinal) &&
            protectiveFlood.Evaluation.Loads.Single(item =>
                item.LoadId == "HOSPITAL").DeliveredKw == 900 &&
            protectiveFlood.Evaluation.Loads.Single(item =>
                item.LoadId == "WATERWORKS").DeliveredKw == 900 &&
            protectiveFlood.Evaluation.Assets.Any(item =>
                item.State == ThermalOperatingState.ProtectiveOutage) &&
            finalApprovalCues.SequenceEqual(
            [
                CommercialAudioCue.ProtectiveStop,
                CommercialAudioCue.Result,
                CommercialAudioCue.FinalRerouteMotif,
            ]) &&
            _audio.SfxVoiceCount >= 3,
            "보호정지·결과·마지막 motif 세 cue와 이를 보존할 세 재생 voice를 함께 보장하지 못했습니다.");
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
        _settings = _settings with
        {
            Fullscreen = false,
            UiScalePercent = 125,
        };
        _shell.SetSettings(SettingsPresentation(_settings));
        ApplyProductSettings(_settings);
        GetWindow().Size = new Vector2I(1280, 720);
        await NextFrame();
        await NextFrame();
        IReadOnlyList<string> completionFrames =
            await DismissCompletedStorySequenceWithLayoutAssertions();
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
            ControlInside(completedInfoScroll, _panel.ChapterReplayButton),
            "완료 화면의 장 시작 선택을 패널 스크롤과 키보드 focus로 표시하지 못했습니다.");
        string? replaySelectionBeforeRender = _panel.SelectedChapterReplayId;
        Render();
        await NextFrame();
        Require(
            GetViewport().GuiGetFocusOwner() == _panel.ChapterReplayButton &&
            _panel.SelectedChapterReplayId == replaySelectionBeforeRender &&
            _panel.ChapterReplayOptionCount == 8,
            "동일한 완료 화면 Render가 장 재시작 선택 목록·선택·focus를 재생성했습니다.");
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
        CommercialRecoveryPreview activeLineRecovery =
            _coreRun!.PreviewRecovery(CommercialRecoveryKind.DecisionWindow);
        string activeLineRecoveryText = RecoveryConfirmationText(activeLineRecovery);
        Require(
            activeLineRecovery.Enabled &&
            activeLineRecovery.DiscardedActiveConstructionKind == ConstructionKind.Line &&
            activeLineRecovery.DiscardedActiveLineRoutePointCount == points.Count + 2 &&
            activeLineRecoveryText.Contains(
                $"진행 중 선로 공사(경로점 {points.Count + 2}곳)",
                StringComparison.Ordinal) &&
            (activeLineRecovery.RestoredCoolingAssetIds.Count != 0 ||
             activeLineRecoveryText.Contains(
                 "복원 후 냉각 상태 설비 없음",
                 StringComparison.Ordinal)),
            $"{description}: 복구 미리보기가 진행 중 선로의 typed 경로점 수 또는 빈 냉각 상태를 명확히 표시하지 못했습니다.");
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
        ConstructionSnapshot before = _snapshot;
        long inputId = _placementInputSequence + 1;
        int outcomeCount = _placementOutcomePresentationCount;
        await MovePointer(point);
        Require(!_pointerAccepted && _pointerError == expected,
            $"{description}을 클릭 전에 막지 못했습니다.");
        await ClickMap(point);
        Require(
            Equals(_snapshot, before) &&
            _snapshot.NodeDraft is null &&
            _placementInputSequence == inputId &&
            _placementOutcomePresentationCount == outcomeCount + 1 &&
            _lastError.Contains($"배치 입력 #{inputId}", StringComparison.Ordinal) &&
            _lastError.Contains(ErrorText(expected), StringComparison.Ordinal) &&
            _lastError.Contains("도시망·경로·초안은 바뀌지 않았습니다", StringComparison.Ordinal) &&
            !_lastStatus.Contains("적용", StringComparison.Ordinal),
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
        await SelectCandidateAt(point, nodeId);
        await ClickMap(point);
    }

    private async Task SelectCandidateAt(CoreMapPoint point, string nodeId)
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
