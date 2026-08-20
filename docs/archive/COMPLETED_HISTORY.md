# Gridworks — 완료·중단 이력 압축본

> 상태: **아카이브 · 현재 구현 권한 아님**
> 상세 snapshot: Git commit `9aceaf7`

## 1. 초기 prototype

카드와 작은 Godot slice는 세 가지 인과를 분리했다.

- 변전소 service area는 연결 자격일 뿐 발전원이 아니다.
- 전기적으로 다른 회로도 같은 공간 회랑 사고에 함께 끊길 수 있다.
- 공사 중 설비는 무전압이며 한 공사는 원자적으로 완공된다.

초기 비인간 관찰은 표현 개선의 입력이었을 뿐 사람 이해·재미·성공률 증거가 아니다. 원본 입력은
`playtests/scope-0a*`, `playtests/scope-0b`와 `playtests/scope-1`에 남아 있다.

## 2. release v1 기술 기준선

33×21 격자 기반 `ReleaseMain`은 분기·합류 그래프, 공유 정격, 사건 projection, 여덟 임무,
저장·재개와 ad-hoc macOS 0.1.0 내부 ZIP을 연결했다. 공식 cold LLM은 마지막 임무에서 병목을 찾지
못해 중단했고, 후속 기술 수정은 병목 진단·작업 버튼·접속 한도·수면 배치를 바로잡았다.

이 경로는 현재 제품이 아니라 동결 회귀 기준선이다. Developer ID 서명·공증과 공개 배포 승인은
없다.

## 3. 상용 v2 단계 B–G

별도 v2 world·campaign·Core와 `CommercialMain`에서 다음을 완료했다.

- B: 고정소수점 자유 배치, 점유영역·수면·건물·선로 기하, 제한된 camera
- C: 선로·전신주 접속부·변전소 주기기의 연속·비상 한계와 보호정지·냉각
- D: 안전 의무·도시 약속·기한·국면 preview·최근 공사 복구와 save v3
- E: 첫 네 임무와 같은 망·자금·저장
- F: 후반 네 임무, 결과·에필로그와 완료 저장 재개
- G: 병목 경로·원자 배치 feedback·승인 checklist·국면 표·설정/접근성·날씨·초상·sound·내부 package

clean commit 기반 내부 후보는 저장소 밖 빈 user-data에서 새 게임→저장→fresh continue→전체 캠페인·
에필로그→완료 저장 재개→장 재설계를 확인했다. 이 증거는 규칙·wiring·내부 package에 한정된다.
사람 전체 플레이, 한국어 전문 교정, 실제 지원 환경과 공개 release는 수집·승인되지 않았다.
최종 내부 후보의 source commit은 `78ff78889ed2c21aad43d1d285ea1a5e8d01442a`, 상태는
`INTERNAL_ADHOC / NOT_AUTHORIZED`였다.

Stage F cold LLM 실행은 마지막 장 폭염 국면 중 사용자가 중단했다. 같은 참가자에게 뒤이어 회고를
요청해 원래 no-follow-up protocol은 무효가 됐다. 관찰에서 나온 병목 진단, 입력 결과 단일화,
승인 checklist, 후보 선택, 공사 기한, hover, 접근성, modal과 복구 preview는 G에서 기계적으로
닫았지만 사람 증거로 승격하지 않았다.

## 4. 실시간 개편 R0–R2

### R0

- commit `5a9e465`
- turn/approval 중심 진행을 fixed-tick realtime으로 바꾸는 계약과 동결 기준선

### R1

- commit `3da1897`
- 별도 V3 Core 14개 파일, realtime checks와 두 `FIRST_LIGHT` fixture
- pause·1×·2×·4×, 공사·사건 시간, 세 설비군 과부하→정지→복귀, forecast=actual과 canonical state
- 당시 고정 증거: Debug/Release 22 suites / 639 assertions, V2 30 suites / 5,739 assertions,
  독립 P0/P1 0
- persistence, production V3 data, Game/UI/art는 범위 밖

### R2

- commit `4c27f65`
- 비기본 realtime shell, reducer, 상단 HUD, 수평 사건 지평선, 조건부 inspector/build/action과
  code-native placeholder world
- 마지막 exact-tree 전체 harness는 사용자 지시로 중단돼 `NOT_COMPLETED`
- 앞선 자동·native 증거는 보존하지만 R2 종료·전체 개편 완료·출시 증거로 승격하지 않음
- 기본 장면은 계속 `CommercialMain`

R3–R7은 실행하지 않았고 이전 전면 개편은 `USER_STOPPED_AFTER_R2`로 닫았다. 현재 새
`ASSET_STYLE_REALTIME_GAME` 목표는 R1/R2를 기반으로 사용하지만 그 중단 계획을 그대로 재개하지 않는다.

## 5. 미개방·조건부 옛 후보

`docs/development/BALANCING_STATIC_SIM.md`의 정적 분석 lab은 수치 조정이 별도 검증 질문을 가질 때만
여는 조건부 도구였고 자동 튜닝 권한이 아니었다. `docs/future/POST_1_0.md`의 냉각수·원전 등은 1.0
이후 아이디어였으며 구현·schema·UI가 열린 적이 없다. 두 문서는 현재 `./assets` 목표의 backlog가
아니며 원문은 Git 이력에만 보존한다.

## 6. 폐기한 HTML 목표 화면

commit `9aceaf7`의 `docs/mockups/realtime-target/`은 HTML/CSS로 FHD/UHD 정상·공사·비상 화면을
그린 non-runtime 참고 시안이었다. 실제 `./assets`의 회화적 밀도·재질·아이소메트릭 설비와 크게
달랐으므로 현재 목표·비주얼 evidence에서 폐기했다.

이 파일들은 runtime, Godot capture, production art, 사람 미감 검토 또는 지원 해상도 증거가 아니다.
역사 확인이 필요할 때만 다음처럼 본다.

```sh
git show 9aceaf7:docs/mockups/realtime-target/gridworks-realtime-target.html
```

## 7. 보존할 교훈

- 단계 하나는 위험 하나와 machine-readable authority 하나를 가진다.
- Core가 계산하고 Game은 typed result를 표현한다.
- service area, actual path, spatial risk와 thermal limit를 서로 대신하지 않는다.
- 거부된 command는 상태를 바꾸지 않고 visible result를 남긴다.
- 공사 완공과 사건 경계는 원자적이고 결정론적이어야 한다.
- 과거 후보의 PASS를 현재 후보의 품질·사람·출시 증거로 합산하지 않는다.
- 미감 목표는 색 palette가 아니라 카메라·밀도·재질·실루엣·조명·상태를 함께 고정해야 한다.

## 8. 삭제한 상세 문서의 원래 경로

아래 파일은 내용 소실이 아니라 **현재 문서 트리 정리**를 위해 제거했다. 모두 commit `9aceaf7`에서
읽을 수 있다.

```text
docs/DEVELOPMENT_HISTORY.md
docs/development/BALANCING_STATIC_SIM.md
docs/future/POST_1_0.md
docs/product/COMMERCIAL_2D_GAME_DESIGN_PLAN_KO.md
docs/scopes/SCOPE_0B_PLAYABLE.md
docs/scopes/SCOPE_1_INTERACTION.md
docs/scopes/FIRST_LIGHT.md
docs/scopes/SECOND_HEART.md
docs/scopes/FACTORY_CAPACITY.md
docs/scopes/HEATWAVE_MAINTENANCE.md
docs/scopes/CAMPAIGN_SAVE_SETTINGS.md
docs/scopes/CAMPAIGN_CONTENT.md
docs/scopes/RELEASE_2D.md
docs/scopes/RELEASE_REBUILD.md
docs/scopes/COMMERCIAL_2D_IMPLEMENTATION.md
docs/scopes/REALTIME_PHYSICAL_TOTAL_REVISION.md
docs/mockups/realtime-target/**
```

## 9. 증거 상한

```text
FrozenCommercialV2 = COMPLETE_INTERNAL_BASELINE
R1RealtimeCore = COMPLETE_BOUNDED_VERTICAL_SLICE
R2Implementation = PRESERVED
R2ExitGate = NOT_COMPLETED
OldRealtimeRevision = USER_STOPPED_AFTER_R2
OldHtmlTarget = SUPERSEDED_NON_RUNTIME_REFERENCE
HumanFullCampaign = NOT_COLLECTED
HumanVisualValidation = NOT_COLLECTED
ElectricalProfessionalReview = NOT_COLLECTED
KoreanProfessionalReview = NOT_COLLECTED
PhysicalUhdPanelEvidence = OPEN_EXTERNAL_HARDWARE_NOT_AVAILABLE
PublicReleaseStatus = NOT_AUTHORIZED
```
