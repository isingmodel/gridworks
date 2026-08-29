# 현재 작업 범위

## 상태

**활성 scope가 없다.**

도시 건물·배경·UI 조화 scope는 완료됐다. 반복 placement와 설비 건물의 중복 draw를 제거하고, 정수장·
북부/동부 생활권·병원·산업단지를 authored footprint, 공통 ground plane, 완만한 road spine/branch와 한
depth 순서로 묶었다. 서부/남부 발전원도 서로 다른 campus 실루엣과 바닥 접점을 사용한다. 내장 ImageGen
후보 중 공통 3/4 isometric camera·좌상단 광원·RGBA를 통과한 주거/산업 구역 2개만 source-tree runtime에
채택했으며 provenance와 package 밖 사용 경계를 기록했다.

선택 dock은 376px 기준 폭의 compact overlay가 되어 지도 아래 dead strip을 만들지 않고, 역할·운영 상태·
연결 정보와 상세 탭을 분리한다. top HUD, event rail, 일반/primary/tool control과 modal은 G3 metal texture
대신 기능별 flat style hierarchy를 사용한다. `./dev check`의 Core/Commercial, G3 identity, save/settings
failure matrix, FHD/QHD/UHD 100–200% UI harness와 normal/construction checkpoint가 PASS했다. fresh native
normal·selected·construction 화면은 한 LLM이 직접 비교했으며 사람 미감·사용성 승인이나 공식 점수는
수집하지 않았다.

이 완료는 Core gameplay·경제·story·save schema, package identity나 외부 release gate를 바꾸지 않는다.
push, PR, merge, 공개 배포는 수행하지 않았다.

## 다음 변경을 여는 조건

- 읽기·설명·진단은 관련 질문 소유 문서를 read-only로 확인한다.
- 파일 변경은 사용자가 명시한 결과물 하나를 이 문서에 먼저 연다.
- 외부 gate 실행, push, PR, merge와 공개 배포는 각각 사용자의 명시적 권한이 있어야 한다.
- 시작 형식, 최소 검증과 종료 checklist는 [Agent 작업 안내](AGENT_GUIDE.md)를 따른다.

`NEXT_TASKS.md`, [외부 출시 gate](RELEASE_GATES.md), 준비된 코드와 과거 PASS는 자동으로
구현·평가·배포 권한을 만들지 않는다.
