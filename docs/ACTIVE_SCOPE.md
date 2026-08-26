# 현재 작업 범위

## 상태

**completed-save new-game scope가 활성화됐다.**

직전 terminal completed save·Continue scope는 완료됐다. canonical full `ProductCampaign`의 current-v3
terminal은 성공이면 세 epilogue card까지 닫힌 상태, 실패이면 epilogue가 시작되지 않은 상태로 저장된다.
제품 title의 `이어하기`는 같은 Core/world/outcome을 epilogue 재생 없이 `Ended`·World·no-modal로 복원한다.

빠른 완성을 위해 완료 journal에서 장별 checkpoint를 재구성하는 chapter replay는 만들지 않는다. 완료한
플레이어가 다시 시작할 수 있게, 검증된 기존 title `새 게임` 경로를 completed save에만 여는 것이 이번
scope의 한 가지 제품 동작이다.

## 결과물

- exact completed save가 있으면 title에서 `이어하기`와 `새 게임`을 함께 활성화한다.
- `이어하기`는 기존 exact terminal read-only 복원을 그대로 유지한다.
- `새 게임`은 기존 canonical `ProductCampaign` bootstrap을 그대로 사용해 `FIRST_LIGHT` initial briefing에서
  시작한다. 별도 replay planner, chapter selector, Session rewind API 또는 save field를 만들지 않는다.
- action 선택만으로 기존 completed bytes를 즉시 바꾸지 않는다. 새 product-owned Session이 정상 종료될 때
  기존 atomic current-v3 write lifecycle이 새 진행으로 같은 slot을 교체한다.

## 구현 범위

- `RealtimeSliceMain`의 completed-title action availability 한 곳
- 기존 `RealtimeProductTitle`의 두 action과 기존 `StartNewGame` routing 재사용
- 기존 product-entry fresh-process smoke의 completed-title/new-game case 최소 확장
- 실제 변경 사실을 소유하는 current Markdown 문서

## 범위 밖

- 장별 선택, 완료 journal checkpoint 재구성, chapter rewind/replay
- 새 completion summary 또는 gameplay UI
- in-progress save 위 `새 게임`
- 손상·지원 밖 schema·I/O 실패 save의 recovery·폐기
- undelivered Core pending transition, general queued story와 active finale/epilogue cursor
- settings/audio, package, 공식 평가와 사람 UX 판정
- push, PR, merge

## 완료 검사

- exact terminal의 fresh title에서 `이어하기`와 `새 게임`이 모두 활성화된다. 성공·실패 terminal의 유효성
  구분은 직전 terminal scope의 기존 회귀가 소유한다.
- 기존 `이어하기` 회귀를 유지하면서, 한 product fresh-process 경로가 `새 게임`→zero-command
  `FIRST_LIGHT` initial briefing→정상 종료→same-slot current-v3 교체→fresh `이어하기` 복원을 검증한다.
- in-progress, invalid, unsupported와 I/O-failure title 정책은 바뀌지 않는다.
- 새 schema/field, 새 UI component와 별도 test suite를 만들지 않는다.
- focused product-entry smoke, `./dev check`와 두 독립 review를 통과한다. touched seam이 요구하지 않는 별도
  Release/UI matrix는 추가하지 않는다.
