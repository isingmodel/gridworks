# Gridworks — 출시판 진행 체크리스트

> 이 문서는 단계 상태와 최소 종료 증거만 기록한다. 상세 규칙은
> [로드맵](ROADMAP_2D.md)과 [완료 구현 계약](scopes/COMMERCIAL_2D_IMPLEMENTATION.md)이 소유한다.

현재 활성 단계는 루트 [README](../README.md)만 선언한다.

## 사용 규칙

- 승인된 구현 단계는 `승인·대기 → 활성 → 검토 중 → 완료`, 별도 권한이 필요한 외부 단계는
  `미승인`을 사용한다.
- 한 번에 한 단계만 활성으로 둔다.
- 단계가 완료될 때 증거 링크를 채운다. 수치와 긴 로그를 이 표에 복사하지 않는다.
- 기계 검증, native 확인, 소유자 플레이와 독립 검토는 서로 대신하지 않는다.
- 옛 내부 후보의 완료는 새 출시판 단계의 완료 증거가 아니다.

## 진행 장부

| 단계 | 상태 | 계약·데이터 | 자동검사 | native 확인 | 사람 검토 | 독립 검토·종료 |
|---|---|---|---|---|---|---|
| 과거 내부 후보 | 보존 | [개발 이력](DEVELOPMENT_HISTORY.md)과 완료 scope | 동결 회귀 | 기존 package 실행 | 소유자 전체 플레이에서 출시 차단 문제 확인 | 출시판으로는 superseded |
| 동결 release v1 기술 기준선 | **완료** | [과거 계약](scopes/RELEASE_REBUILD.md)·[world](../data/release-world-v1.json)·[campaign](../data/release-campaign-v1.json) | 15 suites / 481 assertions | 건설·두 프로세스 8임무·내부 package 통과 | 공식 cold LLM은 마지막 장 `BLOCKED`; v2 증거로 합산하지 않음 | 후속 기술 수정 P0/P1 0 |
| B. 자유 좌표 기반 | **완료** | [완료 계약](scopes/COMMERCIAL_2D_IMPLEMENTATION.md)·Stage-B spatial fixture | CommercialChecks 7 suites / 238 assertions | 1280×720·UI 125% 자유 배치 흐름 통과 | 해당 없음 | exact-tree P0/P1 0 |
| C. 이산 열 국면 기반 | **완료** | [완료 계약](scopes/COMMERCIAL_2D_IMPLEMENTATION.md#45-단계-c-종료-기록-2026-08-21)·[world v2](../data/release-world-v2.json) | CommercialChecks 12 suites / 283 assertions | 자유 배치 회귀·1280×720 UI 125% 열 projection 통과 | 해당 없음 | candidate P0 0 / P1 3; 전부 수정·재검증, open 0 |
| D. 상용 핵심 흐름 | **완료** | [단계 D 종료 기록](scopes/COMMERCIAL_2D_IMPLEMENTATION.md#51-단계-d-종료-기록-2026-08-21)·[world v2](../data/release-world-v2.json)·[core slice](../data/commercial-core-slice-v1.json) | CommercialChecks 17 suites / 682 assertions, 동결 회귀 통과 | 기본 장면 1280×720·UI 125% 핵심 흐름과 자유 배치·열 회귀 통과 | `CommercialSliceHumanStatus = NOT_COLLECTED` | candidate P0 0 / P1 4; 전부 수정·재검증, open 0 |
| E. 첫 네 임무·공통 UX | **완료** | [단계 E 종료 기록](scopes/COMMERCIAL_2D_IMPLEMENTATION.md#64-단계-e-종료-기록-2026-08-21)·[world v2](../data/release-world-v2.json)·[campaign v2](../data/release-campaign-v2.json) | CommercialChecks 19 suites / 1,330 assertions, 동결 회귀 통과 | 1920×1080·UI 125% actual-input 4임무·저장복구, 자유 배치·열 회귀 통과 | `FullCampaignHumanStatus = NOT_COLLECTED` | candidate P0 0 / P1 4; 전부 수정·exact-fix 재검증, open 0 |
| F. 후반 네 임무·에필로그 | **완료** | [단계 F 종료 기록](scopes/COMMERCIAL_2D_IMPLEMENTATION.md#65-단계-f-종료-기록-2026-08-21)·[world v2](../data/release-world-v2.json)·[campaign v2](../data/release-campaign-v2.json) | CommercialChecks 20 suites / 1,805 assertions, 동결 회귀 통과 | 세 fresh 1920×1080 actual-input process로 4장 저장→8장·에필로그→완료 저장·장 선택, 자유 배치·열·E 회귀 통과 | `FullCampaignHumanStatus = NOT_COLLECTED` | candidate P0 0 / P1 3; 전부 수정·exact-fix 재검증, open 0 |
| G. 시청각·접근성·package | **완료** | [단계 G 종료 기록](scopes/COMMERCIAL_2D_IMPLEMENTATION.md#74-단계-g-종료-기록-2026-08-21)·[world v2](../data/release-world-v2.json)·[campaign v2](../data/release-campaign-v2.json) | CommercialChecks 21 suites / 1,828 assertions, 동결 회귀 통과 | 1920×1080 UI 100/125 actual-input presentation, 자유 배치·열·세 fresh campaign process와 clean macOS ZIP 통과 | 전체 사람 검토 `NOT_COLLECTED` | candidate P0 0 / P1 5; 전부 수정·exact-fix 재검증, open 0 |
| G.1 소유자 시각 정렬 | **완료** | [G.1 종료 기록](scopes/COMMERCIAL_2D_IMPLEMENTATION.md#76-단계-g1-종료-기록-2026-08-21)·동결 v2 data·[plate prompt](../game/art/commercial-city-plate-v1.prompt.md) | CommercialChecks 21 suites / 1,828 assertions, 동결 회귀·promise 결과 timeline 검사 통과 | 1920×1080 UI 100/125 actual-input 화면, 자유 배치·열·세 campaign process와 clean macOS ZIP 통과 | 최초 소유자 피드백 반영; 수정 화면 재확인 대기, 전체 검토 `NOT_COLLECTED` | candidate P0 0 / P1 2; 전부 수정, exact tree `d1e7f9a` P0/P1/open 0 |
| G.2 개별 tile·object art | **활성** | [G.2 계약](scopes/COMMERCIAL_2D_IMPLEMENTATION.md#77-단계-g2-개별-tileobject-자산-교체)·동결 v2 data | mapping·whole-map plate 부재 검사 예정 | 1920×1080 UI 100/125 actual-input 화면 재검수 예정 | G.1 plate 방식 소유자 거부 | 구현 뒤 bounded exact-tree 검토 예정 |
| H. 외부 검증·공개 후보 | **미승인** | 별도 사용자·자격증명 gate | 자동증거로 대체하지 않음 | 공개 bytes 확정 전 미실행 | 소유자·외부·전문 교정 필요 | Developer ID·공증·배포 결정 필요 |

## 단계 활성화 조건

- 이전 단계가 완료됐거나 사용자가 명시적으로 삭제했다.
- README가 현재 단계 하나만 가리킨다.
- 해당 단계가 사용할 데이터·코드 경계와 플레이어 결과가 활성 계약에 적혀 있다.
- 다음 단계의 schema, interface와 빈 UI를 미리 만들지 않는다.

단계 B부터 G까지의 전체 구현 목표는 2026-08-18 사용자 요청으로 승인됐다. 따라서 각 단계는 이전
단계 종료와 문서 전환 뒤 같은 목표 안에서 이어갈 수 있지만, 동시에 둘을 구현하지 않는다. 단계 H의
사람 관찰·전문 교정·공개 배포 자격은 별도 외부 gate다.

현재 제품의 최소 지원 해상도는 **1920×1080**이며 UI 100%·125%를 검수한다. 완료된 B~D 행의
1280×720 기록은 당시 단계의 역사 증거일 뿐 현재 지원 범위가 아니다.

## 공통 종료조건

- 활성 단계의 대표 성공·경계·반례 자동검사가 통과한다.
- 변경이 닿는 과거 회귀와 Game build가 통과한다.
- Game을 바꾼 단계는 실제 입력 핵심 흐름을 한 번 확인한다.
- 화면 단계는 대표 해상도에서 clipping, keyboard focus와 색 외 상태 표현을 확인한다.
- 미해결 crash, data loss, softlock, critical과 다음 단계가 의존하는 core-flow major가 0이다.
- 사용자 문구에 기계 ID, enum, 오류 코드, 원시 예외가 노출되지 않는다.
- README와 영향받은 제품 문서를 현재 사실에 맞춘다.
- 한 번의 독립 P0/P1 검토를 닫고 종료 증거를 연결한다.

최종 출시 후보는 비핵심을 포함한 열린 major도 0이어야 한다. LLM 플레이는 기본 종료조건이
아니며 자동검사가 답할 수 없는 좁은 질문에 사용자가 별도 요청할 때만 수행한다.
