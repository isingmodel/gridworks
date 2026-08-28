# 현재 작업 범위

## 상태

**native world compositing polish scope가 활성 상태다.**

## 단일 결과물

청류시 native world에서 강·교량·전신주 도체·건물 필지가 하나의 일관된 oblique 장면으로 보이며,
직사각형 tile seam이나 떠 있는 교량, 지면 중심에 꽂힌 도체가 플레이 화면의 완성도를 해치지 않는다.

## 단일 권위

- gameplay terrain·placement·hit geometry: 기존 Release V3 world와 Core를 유지한다.
- world 합성·sprite placement·conductor attachment: `RealtimePlaceholderMap`이 한 번 소유한다.
- 장기 시각 기준: `docs/product/VISUAL_PRODUCTION_SPEC.md`를 유지한다.

## 범위 안

- authoritative water polygon 위에 비직선 bank contour와 물 흐름을 합성한다.
- 기존 tracked G3 bridge를 양쪽 강둑에 실제로 닿는 일관된 span으로 배치한다.
- pole class별 authored visual attachment에서 conductor 세 가닥을 시작·종료한다.
- building terrain plate의 강한 직사각형 fill을 parcel·road·terrain과 섞이는 낮은 대비 표현으로 바꾼다.
- 가장 작은 renderer regression과 FHD native before/after 화면을 확인한다.

## 범위 밖

- Core terrain polygon·건설 가능 영역·hit test·campaign 규칙·save schema 변경
- 새 gameplay, UI 정보 구조, 장·사건·밸런스 변경
- 새 third-party/generated bitmap 자산 채택, package·외부 gate·push·PR·merge·배포
- 사람 미감 승인이나 공식 상용 UX 점수 주장

## 완료 검사

- bank contour·bridge span·pole attachment·building plate의 renderer 불변조건을 결정론적으로 고정한다.
- Debug build, relevant live renderer/layout harness와 `./dev check`를 통과한다.
- native FHD에서 normal/construction world와 selected crop을 다시 캡처해 네 요청 항목을 직접 대조한다.
- 실제로 바뀐 current 사실의 소유 문서만 갱신하고 scope를 닫는다.

## 다음 변경을 여는 조건

- 읽기·설명·진단은 관련 질문 소유 문서를 read-only로 확인한다.
- 파일 변경은 사용자가 명시한 결과물 하나를 이 문서에 먼저 연다.
- 외부 gate 실행, push, PR, merge와 공개 배포는 각각 사용자의 명시적 권한이 있어야 한다.
- 시작 형식, 최소 검증과 종료 checklist는 [Agent 작업 안내](AGENT_GUIDE.md)를 따른다.

`NEXT_TASKS.md`, [외부 출시 gate](RELEASE_GATES.md), 준비된 코드와 과거 PASS는 자동으로
구현·평가·배포 권한을 만들지 않는다.
