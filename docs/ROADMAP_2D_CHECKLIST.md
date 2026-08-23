# Gridworks — 에셋 스타일 실시간 게임 체크리스트

> 현재 전체 목표: `ASSET_STYLE_REALTIME_GAME`
> 현재 상태: **UX-R2.3 source/fix review 완료·native 관찰 보류 · full G3 live-R2 기본 진입점 및 local main 통합 완료**
> 제품 아트: **full G3 57개(지도 50/UI 7) 적용 완료; 일반 A1/A2–A5 미개방**

이 문서는 단계 상태와 증거 상한만 기록한다. 제품 기능과 시각 규격은
[제품 방향 계약](scopes/ASSET_STYLE_REALTIME_GAME.md)과 [로드맵](ROADMAP_2D.md), 평가 계약은
[실시간 상용 UX 87 scope](scopes/COMMERCIAL_UX_87.md)가 소유한다.

## 진행 장부

| 단계 | 상태 | 핵심 결과 | 자동/native | 사람·전문 | 종료 |
|---|---|---|---|---|---|
| A0 목표·문서 기준선 | **완료** | 네 reference hash, 스타일 DNA, R1/R2 보존, 문서 압축 | 문서·링크·경계 검사 | 해당 없음 | 이 문서 변경 커밋 |
| A0.1 A1 전 구조 준비 | **완료** | build/package 격리, world seam, 두 targeted checkpoint | build·exact suites·두 구간 headless PASS | 해당 없음 | A1은 계속 미개방 |
| UX-R0 실시간 텍스트 기준선 | **완료** | V2 authored content+V3 전체 event timing, 34 story part, future-event bar 계약 | build·31 Commercial suites·22 Realtime suites·16 text mutation PASS | `TextPlanProxy 83.4475` 형성평가 | `746c0aa`, 독립 P0 0/P1 0 |
| UX-R1 native evaluator authority | **완료** | candidate·route·session/attempt·non-score chain parent·blocked artifact·local controlled transcript | candidate 16/16 + session 12/12 + chain 14/14 + artifact 11/11 + transcript 13/13·strict schema | capture 금지 | `2b0b6ee`, 전체 review P0 0/P1 0 |
| UX-R2.1 FIRST_LIGHT release tutorial/rail | **완료** | 실제 release 1장 briefing→live→authored result, 단일 chronological rail, interactive checkpoint host | source `e385707`, build·회귀·독립 P0/P1 0 | FIRST_LIGHT+두 checkpoint actual-input PASS | 이 문서 종료 commit |
| UX-R2.2 tutorial prefix | **완료** | FIRST_LIGHT→SECOND_HEART→SECOND_SOURCE 누적 진행, 2회선 조건, result/briefing, forecast flood | source `659709d`+fix `40ed3fa`, build·회귀·UI PASS | fresh-process 세 장+full-flow record | source/fix와 closure `cf6398a` 독립 P0/P1 0 |
| UX-R2.3 NORTH_BANK promise | **구현·review 완료 / native 보류** | 누적 4장, 명시적 6개월 전환, deadline rail, Keep/Defer branch | source `aee4932` + fix `d85bb3f`, P0 0/P1 0 | user-requested native 보류 | score/evaluation 작업 중단 |
| A1-G3 기존 visual layer | **완료 / 역사 기록** | initial G3 city/grid/terrain 35개 부분 port | build·R2/UI regressions·source review PASS | native/score 미수집 | full port 이전의 완료 기록 |
| Realtime G3 canonicalization | **완료 / local main 단일 branch** | full G3 57개(지도 50/UI 7), R2 default entry | build·provenance·R2 UI matrix·Realtime/Commercial·checkpoint/default boot PASS | native/score 미수집 | local main 통합 완료, origin 미변경 |
| A1 일반 운전 아트 slice | **A1-G3 외 미개방** | dense normal world, actual clock·건설·통전 | 미실행 | 미수집 | 별도 승인 필요 |
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
- [x] 당시 A0 기준으로 기본 장면 `CommercialMain` 유지
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
관찰은 UX-R2.1 source commit·build PASS와 interactive host가 준비된 뒤 비점수 개발 관찰로 수행한다.

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
- [x] 동등한 local controlled transcript authority 결속 — `2b0b6ee`; platform attestation은 주장하지 않음
- [x] session snapshot·route·attempt의 누락·교체·path traversal·stale producer mutation 거부
- [x] chain snapshot·selected terminal·future path·root 이동/추가·재시도 누락 mutation 거부
- [x] artifact 누락·교체·cross-chain swap·downstream rehash·late inventory mutation 거부
- [x] candidate·route 단위에서 score-bearing capture를 fail-closed로 유지
- [x] chain claim 14/14·AJV strict와 독립 P0 0/P1 0 — `74ba725`
- [x] artifact chain source-bound 11/11·7 schema AJV strict·독립 P0 0/P1 0 — `a270339`
- [x] receipt/transcript authority 13/13·3 schema AJV strict·독립 subunit P0 0/P1 0 — `2b0b6ee`
- [x] UX-R1 전체 gate review P0 0/P1 0 — source boundary `2b0b6ee`

이 gate는 `tools/commercial-ux/native/`와 관련 문서만 수정한다. `game/`, `src/`, `data/`와 제품 art는
수정하지 않으며 실제 Mac 조작·capture도 실행하지 않는다.

## UX-R2.1 FIRST_LIGHT release tutorial/rail — 완료

- [x] 사용자 “87점 이상까지 계속 개선”·직접 플레이 지시를 순차 runtime 구현 권한으로 기록
- [x] UX-R1 전체 gate P0 0/P1 0 종료 뒤 개방
- [x] exact Core/Game/UI/test/doc allowlist와 A1–A4 미개방 경계 고정
- [x] gate-opening 독립 review `PASS_FOR_UX_R2_1_GATE_OPENING`, P0 0/P1 0
- [x] shared strict V2+V3 overlay loader와 actual `FIRST_LIGHT` 장(`FIRST_LIGHT_SUPPLY` phase/event) prefix
- [x] briefing→production input/reducer→clock·construction·event→authored standard result native wiring
- [x] FIRST_LIGHT briefing/result가 기존 exact story-part unit bytes와 동일
- [x] future-event bar 현재 시각·persistent countdown·event start/end·actual/draft construction completion
- [x] 수요·기한·기상 정지·공사·보호 사건을 lane별 장문 행이 아닌 한 줄 chronological track의 compact
  state/severity/source/kind marker로 통합하고 custom hover 상세 정보와 전체 AX selector 유지
- [x] actual/draft 형태·문장 구분과 completed construction history 유지
- [x] Debug interactive checkpoint host가 exact start에서 paused 대기하고 real input만 수용
- [x] 기존 A1 두 checkpoint hash/headless oracle·34 story unit·speed chunk invariance 무변경
- [x] FHD/UI scale 행렬, keyboard/focus와 unknown marker target 0
- [x] source commit 뒤 직접 플레이한 비점수 FIRST_LIGHT/checkpoint 세 actual-input record
- [x] first-light source와 single-rail major-unit 독립 review P0/P1 0
- [x] 현재 상태 문서와 종료 commit

현재 source checkpoint는 `e385707071e4ccfb34d5200e3401897db7f164ad`다. shared loader identity는
V2 `078df95f9f0c833be7e1a299088b4ab6e0de4ddf13426ce5b96a1abbeee70b7a`, V3 overlay
`ef962a272683bfd6761fbf10a0ca14cb6c8bf90cdfde810b468ad451088f2258`, full composed
`7bd151399040934cfcb9f7c96d2879aef6354cda79ced2af184641eb33a02f09`, FIRST_LIGHT prefix
`94379c0e8e4dae54b760a55df8c1143c975eaa12f11079e675b2e67ba57df88e`, world V3
`a0a837717bbd6d35f655d8094dfa6daac182d47b2d03f24b18c4883c04feecdf`다. package manifest는
`N/A — non-package carve-out`이며 source revision과 이 identity를 권위로 쓴다. 첫 독립 review의 P1
무공사 가짜 positive result를 수정했고 first-light 재검토는 `PASS_FOR_UX_R2_1_SOURCE_MAJOR_UNIT`, 단일
chronological rail 재검토는 `PASS_FOR_SINGLE_RAIL_MAJOR_UNIT`이며 모두 P0 0/P1 0이다. 실제 입력은
`FORMATIVE_DIRECT_PLAY_PASS:FIRST_LIGHT`, `TARGETED_LIVE_CHECKPOINT_PASS:A1_NORMAL_READY`,
`TARGETED_LIVE_CHECKPOINT_PASS:A1_CONSTRUCTION_DUE_1M`을 남겼다.

이 단위는 `data/**`, runtime art/world, 2–8장, promise·thermal presentation, persistence, default scene,
export/package와 score-bearing capture를 열지 않는다.

## UX-R2.2 tutorial prefix through SECOND_SOURCE — 완료

- [x] UX-R2.1 종료 문서·actual-input·closure review P0 0/P1 0 뒤 개방
- [x] cumulative 3장 prefix와 단독 2·3장 runtime 금지 경계 고정
- [x] exact Core/Game/test/doc allowlist와 4–8장·A1–A5 미개방 경계 고정
- [x] gate-opening 독립 review P0 0/P1 0 · `PASS_FOR_UX_R2_2_GATE_OPENING`
- [x] exact `--release-through=SECOND_SOURCE`, 기존 FIRST_LIGHT/checkpoint route 보존
- [x] Core-owned first-event connection requirement와 current/comparison `n/2`
- [x] one-line rail에서 5 event 순서·hover·AX·construction 회귀
- [x] announced flood forecast outline와 active flood fill의 색 외 구분·지도 선택 연동
- [x] FIRST_LIGHT result→SECOND_HEART briefing→event story→result→SECOND_SOURCE briefing→event story→final
- [x] positive authored result를 `ObjectiveSatisfied`와 결속하고 1회선/공유 범람 failure 위조 거부
- [x] 네 canonical story-part selector hash 단독 실행과 34-part/text-plan source 불변
- [x] 기존 A1 두 checkpoint hash, full Realtime/Commercial/text tooling와 UI scale matrix PASS
- [x] source commit·독립 source review 뒤 production input tutorial 3장 fresh-process PASS
- [x] 현재 상태 문서와 종료 commit

source/fix bounded review는 각각 `PASS_FOR_UX_R2_2_SOURCE_COMMIT`,
`PASS_FOR_UX_R2_2_SOURCE_FIX_COMMIT`, P0 0/P1 0이다. actual-input에서는 한 줄 rail marker의 실제
클릭·선택 연동을 확인했지만 custom hover-only popup의 네이티브 출현은 관찰하지 않았다. 해당 hover
계약은 full UI scale matrix 근거로만 PASS를 보존한다.

이 단위는 `data/**`, rail source, runtime art/world, 4–8장, promise/finale/epilogue, persistence,
default/export/package와 score-bearing capture를 열지 않는다.

## UX-R2.3 NORTH_BANK_PROMISE branch/deadline — 구현·review 완료, native 관찰 보류

- [x] UX-R2.2 source·actual-input·closure review P0 0/P1 0 뒤 개방
- [x] exact cumulative 4장 route와 단독 NORTH_BANK route 금지 경계 고정
- [x] exact Game/test/doc allowlist와 Core/data/loader·5–8장·save·epilogue 금지 경계 고정
- [x] gate-opening 독립 review `PASS_FOR_UX_R2_3_GATE_OPENING`, P0 0/P1 0 (`b0383d6`)
- [x] 2460→265260 명시적 calendar transition과 실제 망·현금·공사·thermal reset 보존
- [x] promise deadline 265680 한 줄 Decision marker, next summary, hover·AX·keyboard·ContextDock 두 action
- [x] unset Keep 가정, explicit Keep/Defer, auto-Defer와 deadline 전/후 Core truth를 UI에 구분
- [x] NORTH_BANK briefing→planning window→hot-evening story→branch/generic result FIFO
- [x] Keep·explicit Defer·auto-Defer·safety/promise failure controller/Core smoke
- [x] 네 canonical story-part hash 단독 실행과 34-part/text-plan source 불변
- [x] 기존 3장 route·FIRST_LIGHT·두 checkpoint hash·full Realtime/Commercial/UI 회귀
- [ ] production input cumulative 4장 KEEP path — user-requested stop으로 보류
- [ ] closure review — native observation을 요구하는 UX-R2.3 종료는 보류

이 단위는 `data/**`, `RealtimeCampaignOverlayLoader`, 다른 Core source, event-rail source/scene,
5–8장, save/persistence, promise ledger/epilogue, default/export/package와 score-bearing capture를 열지
않는다. 예외인 A1-G3 visual-only artifact는 제품 방향 계약의 별도 exact allowlist만 따른다.

## A1-G3 existing visual application — historical partial port

- [x] 사용자 승인: existing G3 design만 적용 후 중단
- [x] source tree `main:cf5da56`·provenance ledger·R2-only boundary 고정
- [x] Core/data/V2 gameplay/default/package/evaluator 비수정 경계 고정
- [x] G3 terrain/river/road/city/grid asset import와 35-asset SHA manifest 결속
- [x] `RealtimePlaceholderMap` draw layer 적용; input/hit/focus/AX owner 보존
- [x] neutral/heat/rain draw presence와 existing state/risk cue smoke
- [x] build·full Realtime/Commercial/text/UI regression 및 independent source review
- [x] independent review P0/P1 0과 current-state commit
- [x] 다음 gate 금지 / user-requested stop 기록

## Realtime G3 canonicalization과 local main 통합

- [x] scope가 V2 gameplay/data merge 없이 R2 renderer/UI만 visual authority로 고정
- [x] full source tree 57 PNG의 SHA-256·pinned bytes·`.import` provenance 결속
- [x] 지도 50개가 clear/heat/rain/storm draw union에 실제로 나타남
- [x] UI 7개가 TopHud, one-line EventRail, Context/Build/Action dock와 modal의 live style resource에 연결됨
- [x] `RealtimeSliceMain`이 default entry이며 headless default boot가 성공함
- [x] build, full R2 UI matrix, Realtime 25/1077, Commercial 31/7084, text-plan 34/16, 두 targeted checkpoint PASS
- [x] independent review의 current-state documentation P1을 수정함
- [x] local `main`에 history-only merge, same-main verification, working branch 안전 삭제

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
ActiveScope = NONE_USER_STOP_AFTER_REALTIME_G3_MAIN_CONSOLIDATION
ActiveEvaluationGate = SUSPENDED_AT_USER_REQUEST_AFTER_UXR23_SOURCE_REVIEW
NextEvaluationGate = NONE_USER_REQUESTED_STOP_AFTER_G3_APPLICATION
UserAuthorization = EXPLICIT_RESOLVE_REALTIME_G3_SPLIT_AND_CONSOLIDATE_LOCAL_MAIN
DocumentationBaseline = A0_COMPLETE
ArchitecturePreparation = COMPLETE
ProductArtImplementationGate = FULL_G3_R2_DEFAULT_ENTRY_AND_LOCAL_MAIN_CONSOLIDATION_COMPLETE
NextCandidate = NONE_USER_REQUESTED_STOP
VisualReferenceAuthority = ROOT_ASSETS_FOUR_IMAGES
RuntimeArtAuthority = LOCAL_MAIN_CF5DA56_G3_TREE_57_PNG_MAP50_UI7_APPLIED_TO_LIVE_R2
A1G3ProductSource = COMMIT_1AF2B33_FULL_G3_R2_CANONICALIZATION
A1G3SourceReview = CURRENT_SCOPE_REVIEW_PASS_P0_0_P1_0
LocalBranchState = MAIN_ONLY_LOCAL_BRANCH
RealtimeRuleAuthority = RELEASE_V3
RealtimeUxAuthority = R2_TUTORIAL_PREFIX_THROUGH_SECOND_SOURCE_PLUS_REVIEWED_UX_R2_3
FutureEventStatusBar = PASS_DETERMINISTIC_SINGLE_CHRONOLOGICAL_TRACK_COMPACT_MARKERS_CUSTOM_HOVER_DETAIL
LiveTestDefault = TARGETED_DETERMINISTIC_CHECKPOINT
TargetedCheckpointRuntime = A1_NORMAL_READY_AND_A1_CONSTRUCTION_DUE_1M_READY
FullFlowE2EPolicy = EXCEPTION_ONLY
FullCampaignNativeE2E = NOT_IMPLEMENTED_THREE_CHAPTER_PREFIX_ONLY
TextPlanProxy = 83.4475_FORMATIVE
TextJudgeExecutionReceipt = NOT_EXPORTED_FORMATIVE_ONLY
CommercialUXProxy = null
ScoreBearingCaptureAllowed = false
UXR0ClosureReview = PASS_P0_0_P1_0_COMMIT_746C0AA
NativeCandidateAuthority = PASS_SOURCE_REVISION_379E980_SHA256_373785E4
EvaluatorProducerAuthority = FOUR_GIT_BLOBS_MATCH_CLT_GIT_REPLACE_AND_LAZY_FETCH_DISABLED
TargetedCheckpointAuthority = TWO_POSITIVE_THREE_REJECTION_INDEPENDENT_REPLAY_PASS
UXR1CandidateRouteReview = PASS_P0_0_P1_0_SOURCE_379E980
SessionAttemptAuthority = PASS_SOURCE_REVISION_5A31FF3_PRODUCER_SHA256_FAEA99AC
UXR1SessionAttemptReview = PASS_P0_0_P1_0_SOURCE_5A31FF3
EvaluationChainParentAuthority = PASS_SOURCE_REVISION_74BA725_PRODUCER_SHA256_D87E6054
UXR1ChainParentReview = PASS_P0_0_P1_0_SOURCE_74BA725
CurrentRouteArtifactAuthority = PASS_SOURCE_REVISION_A270339_PRODUCER_SHA256_225696AD
UXR1CurrentRouteArtifactReview = PASS_P0_0_P1_0_SOURCE_A270339
ControlledCodexTranscriptAuthority = PASS_LOCAL_NON_PLATFORM_SOURCE_2B0B6EE_RECEIPT_SHA256_F7C17C4A
UXR1ControlledTranscriptReview = PASS_SUBUNIT_P0_0_P1_0_SOURCE_2B0B6EE
UXR1ClosureReview = PASS_P0_0_P1_0_SOURCE_2B0B6EE
NativeEvaluatorAuthority = COMPLETE_CANDIDATE_ROUTE_SESSION_CHAIN_PARENT_BLOCKED_ARTIFACT_AND_CONTROLLED_TRANSCRIPT
UXR21GateOpeningReview = PASS_P0_0_P1_0
DefaultMainScene = RealtimeSliceMain
R1RealtimeCore = PRESERVED
R2Implementation = PRESERVED_PLUS_REVIEWED_UX_R2_3_PLUS_FULL_G3_MAP50_UI7_DEFAULT_ENTRY
R2ExitGate = LOCAL_MAIN_HISTORY_CONSOLIDATION_COMPLETE
NativeCapturePolicy = NOT_REQUESTED_USER_STOP_AFTER_G3_APPLICATION
NativeCaptureEnvironment = MAC_CONSOLE_UNLOCKED_NOT_AUTHORIZATION
UXR21GateStatus = COMPLETE_NON_SCORE
UXR21ProductSourceAuthority = PASS_SOURCE_REVISION_E385707071E4CCFB34D5200E3401897DB7F164AD
UXR21SourceReview = PASS_FOR_UX_R2_1_SOURCE_MAJOR_UNIT_P0_0_P1_0_SOURCE_EC265999
UXR21SingleRailReview = PASS_FOR_SINGLE_RAIL_MAJOR_UNIT_P0_0_P1_0_SOURCE_E385707
UXR21ClosureReview = PASS_FOR_UX_R2_1_CLOSURE_MAJOR_UNIT_P0_0_P1_0_SOURCE_F2839D1
UXR21ActualInputObservation = PASS_THREE_NON_SCORE_RECORDS
InteractiveCheckpointHost = ACTUAL_INPUT_PASS_A1_NORMAL_READY_AND_A1_CONSTRUCTION_DUE_1M
FirstLightNativeStoryReachability = FORMATIVE_DIRECT_PLAY_PASS_AUTHORED_STANDARD_RESULT
UXR21DeterministicEvidence = BUILD_0_WARNINGS_REALTIME_23_673_COMMERCIAL_31_7084_UI_MATRIX_PASS_STORY_34
UXR22GateStatus = COMPLETE_NON_SCORE
UXR22GateOpeningReview = PASS_FOR_UX_R2_2_GATE_OPENING_P0_0_P1_0
UXR22ProductSourceAuthority = PASS_SOURCE_REVISION_40ED3FAB92A7054D6BC40D609AB6C5D1E1F801CC
UXR22MajorUnitSource = 659709DE2F654908DEE3E5FBC72D4106DF61E6CA
UXR22SourceReview = PASS_FOR_UX_R2_2_SOURCE_COMMIT_P0_0_P1_0_SOURCE_659709D
UXR22SourceFixReview = PASS_FOR_UX_R2_2_SOURCE_FIX_COMMIT_P0_0_P1_0_SOURCE_40ED3FA
UXR22DeterministicEvidence = BUILD_0_WARNINGS_REALTIME_24_778_COMMERCIAL_31_7084_TEXT_TOOLS_34_PARTS_16_MUTATIONS_STORY_34_UI_MATRIX_PASS_CHECKPOINT_HASHES_PRESERVED
UXR22ActualInputObservation = PASS_THREE_CHAPTER_RESULTS_PLUS_FULL_FLOW_NON_SCORE
UXR22MarkerNativeObservation = PASS_CLICK_SELECTION_CUSTOM_HOVER_ONLY_POPUP_NOT_OBSERVED
UXR22KeyboardCandidateObservation = PASS
UXR22ActiveFloodSolidFillObservation = PASS
UXR22ClosureReview = PASS_FOR_UX_R2_2_CLOSURE_MAJOR_UNIT_P0_0_P1_0_SOURCE_CF6398A
TutorialThreeChapterReachability = FORMATIVE_DIRECT_PLAY_PASS_THROUGH_SECOND_SOURCE
UXR23GateStatus = IMPLEMENTED_REVIEWED_NATIVE_OBSERVATION_DEFERRED
UXR23GateOpeningReview = PASS_FOR_UX_R2_3_GATE_OPENING_P0_0_P1_0_SOURCE_B0383D6
UXR23ProductSourceAuthority = PASS_SOURCE_FIX_D85BB3F
UXR23SourceReview = PASS_FOR_UX_R2_3_SOURCE_FIX_COMMIT_P0_0_P1_0
NorthBankPromiseNativeReachability = NOT_OBSERVED_USER_STOP
NorthBankPromiseDeadlineRail = IMPLEMENTED_REVIEWED
InterchapterCalendarTransition = IMPLEMENTED_REVIEWED
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
