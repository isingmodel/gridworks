# 현재 작업 범위

## 상태

**활성 scope가 없다.**

Godot Editor 직접 campus polish scope는 완료됐다. 실제 Scene tree와 Inspector에서 서부 source Position을
`(205,720)→(225,720)`, uniform Scale을 `0.518→0.492`로, 남부 source Position을
`(195,1725)→(215,1725)`, Scale을 `0.484→0.459`로 수정하고 `⌘S` 저장했다. 두 campus의 visual max side는
각각 약 `800→760`, `780→740` world-unit이 되어 주변 district보다 과도하던 시각 무게가 줄었다. 오른쪽으로
20 unit 보정해 축소 후에도 service-road gate 접점을 유지한다.

별도 fresh normal `FIRST_LIGHT` process에서 두 road 연결과 전체 구도를 확인했다. strict scene projection,
Debug/Release build와 전체 `./dev check`가 PASS했다. Core source 좌표·출력·열·반경·경제·story·save schema와
PNG는 바꾸지 않았다. 이는 actual Godot Editor 직접 조작과 한 native 화면의 근거이며 사람 미감 승인이나
package/release gate는 아니다. push·PR·merge·배포는 수행하지 않았다. 최종 authoring scene은 Godot Editor에
열린 상태로 남긴다.

## 다음 변경을 여는 조건

- 읽기·설명·진단은 관련 질문 소유 문서를 read-only로 확인한다.
- 파일 변경은 사용자가 명시한 결과물 하나를 이 문서에 먼저 연다.
- 외부 gate 실행, push, PR, merge와 공개 배포는 각각 사용자의 명시적 권한이 있어야 한다.
- 시작 형식, 최소 검증과 종료 checklist는 [Agent 작업 안내](AGENT_GUIDE.md)를 따른다.

`NEXT_TASKS.md`, [외부 출시 gate](RELEASE_GATES.md), 준비된 코드와 과거 PASS는 자동으로
구현·평가·배포 권한을 만들지 않는다.
