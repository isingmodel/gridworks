# 현재 작업 범위

## 상태

**활성 구현 scope가 없다.**

직전 scope에서 저장 파일이 없는 제품 title의 `새 게임`을 canonical `FIRST_LIGHT`→`LONGEST_NIGHT`
`ProductCampaign`에 연결하고, 누적 8장 모든 장의 stable in-progress save/Continue를 닫았다.

## 현재 완료 사실

- session 없는 product title/Main만 v1 accepted-journal save를 probe·strict restore하고, title의
  New Game/Continue가 만든 product-owned session만 정상 종료 때 쓴다.
- 명시적 `play chapter`/`play through`/fixture 개발 실행은 같은 native route여도 product save를 읽거나
  쓰지 않는다.
- Core command가 하나 이상 수락됐고 active chapter가 미완료이며 사건·duty·pending transition·draft·
  queued/active story·epilogue·frame debt가 없는 stable 상태를 모든 장에서 exact replay한다.
- Continue는 exact clock·cash·world·construction·journal/hash와 chapter transition history를 복원한 뒤
  player-paused·normal speed·no-modal로 시작한다.
- 직전 standalone `FIRST_LIGHT` v1 save는 exact-current source인 경우 원 route 그대로 Continue할 수 있다.
  누적 save로 migration하지 않는다.
- 다른 개발 route, 형식 손상, 지원하지 않는 schema/version, source/hash/replay 불일치와 I/O 실패는
  원본을 바꾸지 않고 두 title action을 차단한다.

## 완료 검증

- focused save/session 회귀: 8장 stable exact replay와 `SECOND_HEART` handoff 뒤 story 재개
- `./dev check`: Realtime 26 suites/1,166 assertions, Commercial 31 suites/7,084 assertions와 product·legacy·
  invalid/unsupported/I/O fresh-process smoke
- `dotnet build Gridworks.sln -c Release`: warning/error 0
- 전체 Godot UI harness: `REALTIME_R2_OFFSCREEN_CONTROL_TREE_PASS`
- 독립 코드 review 2건: stale/programmatic title action finding을 수정하고 재검토 통과

## 아직 증명하지 않은 경계

- 사건 중·active duty·pending transition, queued/active story, chapter result·handoff 저장
- 완료 run, finale/epilogue cursor와 완료 후 result/chapter/replay 선택
- snapshot/application cursor 신규 schema, 유효 save 덮어쓰기 확인, 삭제·migration/recovery UI
- 전체 8장 production-input 직접 여정, settings/audio, package, 공식 평가와 사람 UX 판정
- 비정상 종료 journal, 다중 slot, cloud save, push/PR/merge

다음 구현은 [남은 작업](NEXT_TASKS.md)에서 한 단계만 골라 이 문서에 수정 범위·단일 권위·완료 검사를
먼저 적은 뒤 시작한다.
