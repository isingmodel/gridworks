# 현재 작업 범위

## 상태

**활성 scope가 없다.**

고정 `gpt-5.6-sol` xhigh LLM sample의 누적 8장 native review에서 재현된 current R2 품질 결함을
보완했다. story·build 진입의 계획 정지와 명시적 재개, `FIRST_LIGHT` action gating·평가 시각·실패 원인,
약속 context 가독성, critical pause 안내, 겹친 후보의 `Q/E` 안내, 실패 finale의 읽기 전용 경계를
명확히 했다. 장 전환은 이전 tool·quote·pointer feedback·후보 badge를 지우고 새 map 입력 뒤에만 후보를
다시 계산한다. Core 규칙, save schema, 새 콘텐츠·mechanic·art는 바꾸지 않았다.

Debug build, Realtime 1,205 assertions, Commercial 7,084 assertions, product save/settings failure matrix,
FHD/QHD/UHD 100–200% offscreen UI harness와 두 targeted live checkpoint가 PASS했다. 같은 reviewer의 최종
native 확인에서 `SECOND_HEART` 22:00 계획 정지와 chapter-local cleanup, mouse 뒤 후보 재표시와 `E` cycling,
정상 process 종료를 관찰했다. 전체 sample은 모든 약속 Defer와 실패 finale를 택했으므로 성공 전용
epilogue를 직접 관찰하지 않았다. 이 결과는 사람 재미·사용성·미감 증거나 공식 UX 점수가 아니다.

저장소가 소유하는 제품 목표는 실시간 8장·finale/epilogue, product save/settings/audio wiring,
internal macOS package identity와 combined 2B를 포함하는 **current R2 내부 후보**다.
[남은 구현 작업](NEXT_TASKS.md)은 현재 비어 있다.

## 다음 변경을 여는 조건

- 읽기·설명·진단은 관련 질문 소유 문서를 read-only로 확인한다.
- 파일 변경은 사용자가 명시한 결과물 하나를 이 문서에 먼저 연다.
- 외부 gate 실행, push, PR, merge와 공개 배포는 각각 사용자의 명시적 권한이 있어야 한다.
- 시작 형식, 최소 검증과 종료 checklist는 [Agent 작업 안내](AGENT_GUIDE.md)를 따른다.

`NEXT_TASKS.md`, [외부 출시 gate](RELEASE_GATES.md), 준비된 코드와 과거 PASS는 자동으로
구현·평가·배포 권한을 만들지 않는다.
