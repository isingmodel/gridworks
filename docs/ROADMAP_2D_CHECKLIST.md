# Gridworks — 에셋 스타일 실시간 게임 체크리스트

> 현재 전체 목표: `ASSET_STYLE_REALTIME_GAME`
> 현재 상태: **A0+A0.1+UX-R0 완료 · UX-R1 native evaluator authority port 활성**
> 제품 아트: **A1 미개방**

이 문서는 단계 상태와 증거 상한만 기록한다. 제품 기능과 시각 규격은
[제품 방향 계약](scopes/ASSET_STYLE_REALTIME_GAME.md)과 [로드맵](ROADMAP_2D.md), 현재 평가 작업은
[실시간 상용 UX 87 scope](scopes/COMMERCIAL_UX_87.md)가 소유한다.

## 진행 장부

| 단계 | 상태 | 핵심 결과 | 자동/native | 사람·전문 | 종료 |
|---|---|---|---|---|---|
| A0 목표·문서 기준선 | **완료** | 네 reference hash, 스타일 DNA, R1/R2 보존, 문서 압축 | 문서·링크·경계 검사 | 해당 없음 | 이 문서 변경 커밋 |
| A0.1 A1 전 구조 준비 | **완료** | build/package 격리, world seam, 두 targeted checkpoint | build·exact suites·두 구간 headless PASS | 해당 없음 | A1은 계속 미개방 |
| UX-R0 실시간 텍스트 기준선 | **완료** | V2 authored content+V3 전체 event timing, 34 story part, future-event bar 계약 | build·31 Commercial suites·22 Realtime suites·16 text mutation PASS | `TextPlanProxy 83.4475` 형성평가 | `746c0aa`, 독립 P0 0/P1 0 |
| UX-R1 native evaluator authority | **활성** | candidate·route·session/attempt·non-score chain parent·blocked artifact chain 완료, receipt provenance 진행 | candidate 16/16 + session 12/12 + chain 14/14 + artifact 11/11·7 schema AJV strict | capture 금지 | 남은 receipt/transcript authority+전체 gate review 필요 |
| A1 일반 운전 아트 slice | **미개방** | dense normal world, actual clock·건설·통전 | 미실행 | 미수집 | 사용자 승인 필요 |
| A2 사건·열·복귀 | **미개방** | heatwave, emergency, trip, cooling, recovery | 미실행 | 미수집 | A1 뒤 별도 승인 |
| A3 production catalog | **미개방** | 전체 설비·시설·도시·LOD·manifest | 미실행 | 미수집 | A2 뒤 별도 승인 |
| A4 campaign·save | **미개방** | 전체 실시간 campaign, save v4, default 후보 | 미실행 | 미수집 | A3 뒤 별도 승인 |
| A5 native·release | **미개방** | FHD/UHD, package, fresh install | 미실행 | 미수집 | 별도 내부·외부 gate |

## A0 완료 확인

- [x] 루트 `./assets` 네 파일과 SHA-256을 visual reference authority로 고정
- [x] 카메라·밀도·재질·실루엣·조명·상태 언어 정의
- [x] reference에서 채택하지 않는 영어·송전급 설비·발전 입지·고정 panel 명시
- [x] R1 deterministic Core와 R2 UX 기반 보존
- [x] R2 마지막 전체 harness를 PASS로 승격하지 않음
- [x] 기본 장면 `CommercialMain` 유지
- [x] runtime art authority 미수립과 로컬/untracked 후보 미채택 명시
- [x] 현재 목표 문서만 전면에 보존
- [x] 완료·중단 과거를 압축 아카이브와 Git 이력으로 이동
- [x] 이전 HTML/vector 목표를 current target/evidence에서 제거
- [x] 구간 직접 진입형 live checkpoint를 기본 테스트 정책으로 고정
- [x] onboarding·save/migration·누적 상태·package·전체 campaign만 full-flow 예외로 제한
- [x] A1을 자동으로 열지 않음

## A0.1 구조 준비 확인

- [x] Core `ExportRelease`는 동결 V2 explicit allowlist, Game `ExportRelease`는 R2/UI 제외
- [x] 미승인 `RealtimeCampaignPersistence.cs`는 모든 Core 구성에서 compile 제외
- [x] package audit가 R2/UI namespace 유입을 거부
- [x] 동일 state/horizon forecast cache와 forecast 없는 `Minute` query 사용
- [x] caller 0 frame facade·command alias·UI event·compatibility alias 제거
- [x] world renderer가 raw campaign snapshot·forecast 대신 `IRealtimeWorldView` DTO를 사용
- [x] pointer 이동은 full snapshot·forecast·전체 presentation 갱신을 호출하지 않음
- [x] RealtimeChecks가 실행 위치와 무관하게 fixture를 찾고 `--suite <exact-name>`을 지원
- [x] `A1_NORMAL_READY` exact start/replay/end identity와 bounded live segment PASS
- [x] `A1_CONSTRUCTION_DUE_1M` exact start/replay/end identity와 원자 완공·통전 PASS
- [x] full R2 harness와 전체 campaign을 다시 실행하지 않음

두 구간의 고정 identity는 [현재 목표 계약 §8.3](scopes/ASSET_STYLE_REALTIME_GAME.md#83-표준-checkpoint-후보)가
소유한다. 이 결과는 A1 art·native capture·사람 검토나 R2 종료 PASS로 확대하지 않는다.

## UX-R0 실시간 텍스트 기준선

- [x] `origin/main`의 Release.V3/R2 실시간 방향을 제품 권위로 사용
- [x] judge identity를 `gpt-5.6-sol` / `ultra` / `SOL-ULTRA`로 고정
- [x] 8장·16 event와 34 authored narrative atom을 exact manifest로 결속
- [x] 16 event의 priority·start·duration·forecast lead를 part/context/artifact에 결속
- [x] `--story-part <selector>` 단독 실행과 invalid/unreachable typed failure
- [x] authored reachability와 native reachability를 분리
- [x] future-event status bar의 현재 시각·countdown·event interval·공사·결정·열 signal 계약
- [x] hash-bound text artifact와 mutation 검사
- [x] source binding+artifact 통합 hash와 원본 4종 deterministic aggregate rebuild
- [x] 불안정 첫 INITIAL panel 보존 뒤 별도 세 fresh blinded judge로 `TextPlanProxy = 83.4475`
- [x] 전체 UX-R0 변경의 독립 P0/P1 review와 종료 커밋 `746c0aa`

현재 R2 `RealtimeEventRail`의 코드 존재와 deterministic UI 검사는 native 이해·가독성 증거가 아니다.
source revision `379e980`의 exact candidate는 두 checkpoint scene-load wiring까지만 추가로 증명한다. 실제 화면
관찰은 native evaluator port와 Mac 잠금 해제 뒤 수행한다.

## UX-R1 native evaluator authority port

- [x] UX-R0 종료 증거와 독립 P0/P1 review 뒤 gate 개방
- [x] V3/R2 candidate source·project·runtime byte allowlist와 manifest — `379e980`
- [x] targeted checkpoint replay와 full-flow exception의 typed 분리 — `379e980`
- [x] candidate/story snapshot과 claim-last를 사용하는 session/attempt authority — `5a31ff3`
- [x] 두 checkpoint·34 story unit·full-flow zero-execution route가 retry에서 바뀌지 않음
- [x] 세 attempt predecessor chain, terminal-before-output, caller outcome 금지와 exact root inventory
- [x] finalized retry prefix 전체를 deterministic sibling에 snapshot하는 non-score chain parent — `74ba725`
- [x] chain parent가 future-event 6 signal/headless PASS/native `NOT_OBSERVED`와 full-flow 0-attempt를 유지
- [x] evidence·actor·judge·verifier·oracle·aggregate의 exact 7-artifact finalized blocked non-score chain — `a270339`
- [ ] platform/API receipt 또는 동등한 transcript authority 결속
- [x] session snapshot·route·attempt의 누락·교체·path traversal·stale producer mutation 거부
- [x] chain snapshot·selected terminal·future path·root 이동/추가·재시도 누락 mutation 거부
- [x] artifact 누락·교체·cross-chain swap·downstream rehash·late inventory mutation 거부
- [x] candidate·route 단위에서 score-bearing capture를 fail-closed로 유지
- [x] chain claim 14/14·AJV strict와 독립 P0 0/P1 0 — `74ba725`
- [x] artifact chain source-bound 11/11·7 schema AJV strict·독립 P0 0/P1 0 — `a270339`
- [ ] receipt/transcript authority exact test와 UX-R1 전체 gate review

이 gate는 `tools/commercial-ux/native/`와 관련 문서만 수정한다. `game/`, `src/`, `data/`와 제품 art는
수정하지 않으며 실제 Mac 조작·capture도 실행하지 않는다.

## A1 개방 전 체크

- [ ] 사용자 A1 구현 승인
- [ ] exact source asset allowlist
- [ ] source별 provenance·hash·사용 경계
- [ ] 공통 camera·light·scale sheet
- [x] 수정 가능한 Game/Core/data build authority 경계
- [ ] 한 `FIRST_LIGHT` scene와 player outcome
- [x] `A1_NORMAL_READY`·`A1_CONSTRUCTION_DUE_1M` 생성 계약과 시작 hash
- [x] targeted live 종료 assertion·evidence label
- [ ] FHD reference contact sheet 절차
- [ ] frame budget와 지원 hardware
- [ ] fallback·placeholder 금지 목록
- [ ] 독립 리뷰 질문과 종료 gate

하나라도 비어 있으면 A1은 `미개방`이다.

## 단계별 증거 구분

| 증거 | 증명할 수 있음 | 증명할 수 없음 |
|---|---|---|
| Core/자동검사 | 규칙·상태전이·동등성·save | 미감·도시 밀도·재미 |
| targeted live checkpoint | 특정 중간 상태의 실제 입력·렌더·transition | onboarding·전체 campaign·package |
| full-flow E2E | 시작부터 이어진 wiring·누적 상태·fresh process | 모든 중간 결함의 최소 재현 |
| scene smoke | wiring·입력 owner·crash·clipping | 장시간 가독성·사람 이해 |
| native capture | 실제 render·texture·depth·layout | 실제 사람의 선호·피로 |
| reference review | 카메라·밀도·재질·실루엣·조명 방향 | 규칙 정확성·법적 권리 |
| 사람 관찰 | 이해·조작·미감 경험 | 코드 보존식·다른 사람의 성공률 |
| 전문 검토 | 한국어·전력설비 표현 | 재미·공개 출시 승인 |

## 현재 상태 블록

```text
CurrentGoal = ASSET_STYLE_REALTIME_GAME
ActiveScope = COMMERCIAL_UX_87_REALTIME
ActiveEvaluationGate = UX_R1_NATIVE_EVALUATOR_AUTHORITY_PORT
DocumentationBaseline = A0_COMPLETE
ArchitecturePreparation = COMPLETE
ProductArtImplementationGate = NONE
NextCandidate = A1_NORMAL_OPERATION_ART_SLICE_NOT_OPENED
VisualReferenceAuthority = ROOT_ASSETS_FOUR_IMAGES
RuntimeArtAuthority = NOT_ESTABLISHED
RealtimeRuleAuthority = RELEASE_V3
RealtimeUxAuthority = R2_FIRST_LIGHT_TARGETED_SLICE
FutureEventStatusBar = REQUIRED_R2_EVENT_RAIL_HEADLESS_WIRING_PASS_NATIVE_QUALITY_NOT_OBSERVED
LiveTestDefault = TARGETED_DETERMINISTIC_CHECKPOINT
TargetedCheckpointRuntime = A1_NORMAL_READY_AND_A1_CONSTRUCTION_DUE_1M_READY
FullFlowE2EPolicy = EXCEPTION_ONLY
FullCampaignNativeE2E = NOT_IMPLEMENTED
TextPlanProxy = 83.4475_FORMATIVE
TextJudgeExecutionReceipt = NOT_EXPORTED_FORMATIVE_ONLY
CommercialUXProxy = null
ScoreBearingCaptureAllowed = false
UXR0ClosureReview = PASS_P0_0_P1_0_COMMIT_746C0AA
NativeCandidateAuthority = PASS_SOURCE_REVISION_379E980_SHA256_373785E4
EvaluatorProducerAuthority = FOUR_GIT_BLOBS_MATCH_CLT_GIT_REPLACE_AND_LAZY_FETCH_DISABLED
TargetedCheckpointAuthority = TWO_POSITIVE_THREE_REJECTION_INDEPENDENT_REPLAY_PASS
UXR1CandidateRouteReview = PASS_P0_0_P1_0_SOURCE_379E980
EvaluationChainParentAuthority = PASS_SOURCE_REVISION_74BA725_PRODUCER_SHA256_D87E6054
UXR1ChainParentReview = PASS_P0_0_P1_0_SOURCE_74BA725
CurrentRouteArtifactAuthority = PASS_SOURCE_REVISION_A270339_PRODUCER_SHA256_225696AD
UXR1CurrentRouteArtifactReview = PASS_P0_0_P1_0_SOURCE_A270339
NativeEvaluatorAuthority = CANDIDATE_ROUTE_SESSION_CHAIN_PARENT_AND_BLOCKED_ARTIFACT_CHAIN_COMPLETE_RECEIPT_AUTHORITY_PENDING
DefaultMainScene = CommercialMain
R1RealtimeCore = PRESERVED
R2Implementation = PRESERVED
R2ExitGate = NOT_COMPLETED
NativeCapture = BLOCKED_MAC_LOCKED
PhysicalUhdPanelEvidence = OPEN_EXTERNAL_HARDWARE_NOT_AVAILABLE
HumanVisualValidation = NOT_COLLECTED
ElectricalProfessionalReview = NOT_COLLECTED
KoreanProfessionalReview = NOT_COLLECTED
PublicReleaseStatus = NOT_AUTHORIZED
```

## 공통 종료 기록 형식

각 구현 gate를 끝낼 때 다음만 추가한다.

```text
source commit
exact file/asset allowlist
manifest aggregate hash
bounded automatic/native commands and result
checkpoint ID/start hash/end assertion or full-flow exception reason
reference capture IDs
human/professional status
independent P0/P1 verdict
explicit exclusions and next gate status
```

긴 로그와 과거 단계 수치를 이 문서에 복제하지 않는다.
