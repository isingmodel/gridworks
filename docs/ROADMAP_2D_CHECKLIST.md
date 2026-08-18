# Gridworks — 출시판 진행 체크리스트

> 이 문서는 단계 상태와 최소 종료 증거만 기록한다. 상세 규칙은
> [로드맵](ROADMAP_2D.md)과 [상용 구현 계약·완료 기록](scopes/COMMERCIAL_2D_IMPLEMENTATION.md)이
> 소유한다.

현재 활성 단계는 단계 G다. 루트 [README](../README.md)가 단계 F 동결 기준과 단계 G 구현 권한,
단계 H 미개방 경계를 선언한다.

## 사용 규칙

- 승인된 구현 단계는 `계획·미개방 → 활성 → 검토 중 → 완료`, 별도 권한이 필요한 외부 단계는
  `미승인`을 사용한다. 계획은 현재 구현 권한을 뜻하지 않는다.
- 한 번에 한 단계만 활성으로 둔다.
- 단계가 완료될 때 증거 링크를 채운다. 수치와 긴 로그를 이 표에 복사하지 않는다.
- 기계 검증, native 확인, 소유자 플레이와 독립 검토는 서로 대신하지 않는다.
- 옛 내부 후보의 완료는 새 출시판 단계의 완료 증거가 아니다.

## 진행 장부

| 단계 | 상태 | 계약·데이터 | 자동검사 | native 확인 | 사람 검토 | 독립 검토·종료 |
|---|---|---|---|---|---|---|
| 과거 내부 후보 | 보존 | [개발 이력](DEVELOPMENT_HISTORY.md)과 완료 scope | 동결 회귀 | 기존 package 실행 | 소유자 전체 플레이에서 출시 차단 문제 확인 | 출시판으로는 superseded |
| 동결 release v1 기술 기준선 | **완료** | [과거 계약](scopes/RELEASE_REBUILD.md)·[world](../data/release-world-v1.json)·[campaign](../data/release-campaign-v1.json) | 15 suites / 481 assertions | 건설·두 프로세스 8임무·내부 package 통과 | 공식 cold LLM은 마지막 장 `BLOCKED`; v2 증거로 합산하지 않음 | 후속 기술 수정 P0/P1 0 |
| B. 자유 좌표 기반 | **완료** | [상용 구현 기록](scopes/COMMERCIAL_2D_IMPLEMENTATION.md)·Stage-B spatial fixture | CommercialChecks 7 suites / 238 assertions | 1280×720·UI 125% 자유 배치 흐름 통과 | 해당 없음 | exact-tree P0/P1 0 |
| C. 이산 열 국면 기반 | **완료** | final world v2와 같은 상용 구현 기록 | CommercialChecks 13 suites / 350 assertions | 열 projection·설비 선택·비상→정지→복귀 통과 | 해당 없음 | P1 2건 수정 뒤 exact-tree P0/P1 0 |
| D. 상용 핵심 흐름 | **완료** | core slice SHA `8d09a0…0842`와 같은 상용 구현 기록 | CommercialChecks 19 suites / 1,312 assertions | 두 fresh process로 첫 불빛 저장→본편 복원→5장 완료 | `CommercialSliceHumanStatus = NOT_COLLECTED` | 입력·저장 P0/P1 수정 뒤 exact-tree P0/P1 0 |
| E. 첫 네 임무·공통 UX | **완료** | final world/campaign v2와 [상용 구현 기록](scopes/COMMERCIAL_2D_IMPLEMENTATION.md) | CommercialChecks 26 suites / 2,402 assertions | Game 3구성 0/0, 두 fresh process로 4장 완료 | `NOT_COLLECTED` | exact-tree P0/P1 0 |
| F. 후반 네 임무·에필로그 | **완료** | world SHA `c4923f752205c193efa78ddb4ca9e5431801731e6087be3ba3796abf9117ac14` / campaign SHA `078df95f9f0c833be7e1a299088b4ab6e0de4ddf13426ce5b96a1abbeee70b7a` | CommercialChecks 29 suites / 4,486 assertions | Game 3구성 0/0, 1280×720·UI 125% 두 process 전체 흐름 통과 | 사람 전체 플레이·전문 교정 `NOT_COLLECTED`; 공식 cold LLM은 8장 폭염 정점 2/3에서 `USER_STOPPED`, 중단 후 리뷰만 수집 | Stage F 계약 exact-tree P0/P1 0; 관찰 backlog는 종료 당시 독립 미검증·미승인이었음 |
| G. 시청각·접근성·package | **활성** | 최종 자산·settings v3·네 layout/keyboard evidence·package gate와 [관찰 기반 backlog](ROADMAP_2D.md#관찰-기반-선행-보정-backlog--단계-g-활성) | 구현 전 기준선 PASS, G 증거 대기 | 구현 전 기준선 PASS, G 증거 대기 | 전체 사람 검토 `NOT_COLLECTED` | P0 3 / P1 5 / P2 3은 관찰 기반·구현 승인·재현 대기; Stage H는 미승인 |
| H. 외부 검증·공개 후보 | **미승인** | 별도 사용자·자격증명 gate | 자동증거로 대체하지 않음 | 공개 bytes 확정 전 미실행 | 소유자·외부·전문 교정 필요 | Developer ID·공증·배포 결정 필요 |

`USER_STOPPED`는 native 완료, 실패 또는 막힘 판정이 아니다. 참가자는 `가장 긴 밤`의 `폭염 정점
2/3`에서 500 kW 비상 열여유 부족을 보고 있었고 완료·에필로그는 확인하지 못했다. 사용자 중단 뒤
같은 참가자에게 리뷰를 요청해 원래 no-follow-up 완주 관찰 protocol은 `INVALIDATED`됐다. 별도 중단
후 리뷰의 강점과 과제는 정성 입력일 뿐 사람 검토나 Stage F 자동·native 증거에 합산하지 않는다.

## 단계 활성화 조건

- 이전 단계가 완료됐거나 사용자가 명시적으로 삭제했다.
- README가 현재 단계 하나만 가리킨다.
- 해당 단계가 사용할 데이터·코드 경계와 플레이어 결과가 구현 계약에 적혀 있다.
- 다음 단계의 schema, interface와 빈 UI를 미리 만들지 않는다.

단계 B부터 F까지는 2026-08-18 시작된 구현 목표 안에서 완료했다. 사용자 종료 조건은 F 전체 감사
뒤 목표 추구를 멈추도록 했으므로 G는 자동 활성화하지 않는다. README가 새 사용자 지시에 따라 G를
명시적으로 열기 전에는 최종 아트·날씨·초상·audio cue, settings v3·움직임 줄이기, 네 화면과
keyboard evidence, packaging·signing·legal·새 설치 전체 실행을 시작하지 않는다. 단계 H의 사람
관찰·전문 교정·공개 배포 자격도 별도 외부 gate다.

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
