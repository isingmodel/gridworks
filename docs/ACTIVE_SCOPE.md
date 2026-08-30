# 현재 작업 범위

## 상태

**활성 scope가 없다.**

Godot 직접 시각 배치 편집 scope는 완료됐다. district·발전원 campus의 sprite 위치와 크기, road 제어점은
이제 strict `realtime-visual-layout.v1` project data 한 곳에 있으며 normal renderer와 DEBUG 편집 모드가
같은 데이터를 읽는다. `./dev play layout`에서 청록 건물, 황색 발전원, 흰색 도로점 handle을 드래그하고
건물·발전원 크기를 휠로 조절한 뒤 `S`로 저장하거나 `R`로 되돌릴 수 있다.

실제 Godot UI에서 병원 campus를 `(2480,1485)`에서 `(2537,1412)`로 옮겨 남동 공장과의 실루엣 중첩을
줄였고, 편집 모드를 끈 fresh `FIRST_LIGHT`에서도 저장 배치가 재현되는 것을 확인했다. strict loader의
unknown·누락/중복 ID 거부와 deterministic round trip, Debug/Release build, 전체 `./dev check`가 PASS했다.
Core node·전력망 기하·반경·열·경제·story·save schema는 바꾸지 않았다. 이는 한 native 화면의 직접 조작
증거이며 사람 미감 승인이나 package/release gate는 아니다. push·PR·merge·배포는 수행하지 않았다.

## 다음 변경을 여는 조건

- 읽기·설명·진단은 관련 질문 소유 문서를 read-only로 확인한다.
- 파일 변경은 사용자가 명시한 결과물 하나를 이 문서에 먼저 연다.
- 외부 gate 실행, push, PR, merge와 공개 배포는 각각 사용자의 명시적 권한이 있어야 한다.
- 시작 형식, 최소 검증과 종료 checklist는 [Agent 작업 안내](AGENT_GUIDE.md)를 따른다.

`NEXT_TASKS.md`, [외부 출시 gate](RELEASE_GATES.md), 준비된 코드와 과거 PASS는 자동으로
구현·평가·배포 권한을 만들지 않는다.
