# Gridworks — 에셋 스타일 실시간 게임 체크리스트

> 현재 전체 목표: `ASSET_STYLE_REALTIME_GAME`
> 현재 상태: **A0 완료 · 활성 구현 gate 없음**
> 다음 후보: **A1 미개방**

이 문서는 단계 상태와 증거 상한만 기록한다. 기능과 시각 규격은
[현재 계약](scopes/ASSET_STYLE_REALTIME_GAME.md)과 [로드맵](ROADMAP_2D.md)이 소유한다.

## 진행 장부

| 단계 | 상태 | 핵심 결과 | 자동/native | 사람·전문 | 종료 |
|---|---|---|---|---|---|
| A0 목표·문서 기준선 | **완료** | 네 reference hash, 스타일 DNA, R1/R2 보존, 문서 압축 | 문서·링크·경계 검사 | 해당 없음 | 이 문서 변경 커밋 |
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
- [x] A1을 자동으로 열지 않음

## A1 개방 전 체크

- [ ] 사용자 A1 구현 승인
- [ ] exact source asset allowlist
- [ ] source별 provenance·hash·사용 경계
- [ ] 공통 camera·light·scale sheet
- [ ] 수정 가능한 Game/Core/data 경계
- [ ] 한 `FIRST_LIGHT` scene와 player outcome
- [ ] FHD reference contact sheet 절차
- [ ] frame budget와 지원 hardware
- [ ] fallback·placeholder 금지 목록
- [ ] 독립 리뷰 질문과 종료 gate

하나라도 비어 있으면 A1은 `미개방`이다.

## 단계별 증거 구분

| 증거 | 증명할 수 있음 | 증명할 수 없음 |
|---|---|---|
| Core/자동검사 | 규칙·상태전이·동등성·save | 미감·도시 밀도·재미 |
| scene smoke | wiring·입력 owner·crash·clipping | 장시간 가독성·사람 이해 |
| native capture | 실제 render·texture·depth·layout | 실제 사람의 선호·피로 |
| reference review | 카메라·밀도·재질·실루엣·조명 방향 | 규칙 정확성·법적 권리 |
| 사람 관찰 | 이해·조작·미감 경험 | 코드 보존식·다른 사람의 성공률 |
| 전문 검토 | 한국어·전력설비 표현 | 재미·공개 출시 승인 |

## 현재 상태 블록

```text
CurrentGoal = ASSET_STYLE_REALTIME_GAME
DocumentationBaseline = A0_COMPLETE
ActiveImplementationGate = NONE
NextCandidate = A1_NORMAL_OPERATION_ART_SLICE_NOT_OPENED
VisualReferenceAuthority = ROOT_ASSETS_FOUR_IMAGES
RuntimeArtAuthority = NOT_ESTABLISHED
DefaultMainScene = CommercialMain
R1RealtimeCore = PRESERVED
R2Implementation = PRESERVED
R2ExitGate = NOT_COMPLETED
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
reference capture IDs
human/professional status
independent P0/P1 verdict
explicit exclusions and next gate status
```

긴 로그와 과거 단계 수치를 이 문서에 복제하지 않는다.
