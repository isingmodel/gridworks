# 현재 작업 범위

## 상태

**활성 scope가 없다.**

청류시 native world 합성을 보완했다. 물 surface 자체가 굽이치는 양안 contour를 따르고, 흐름선과
저대비 반사가 같은 형태를 공유한다. 두 교량은 시각 강둑을 직접 재서 양안 너머까지 착지하며 차도,
교대, 측면 두께와 보강재를 가진다. pole 도체는 지면 중심이 아니라 pole sprite의 상단 attachment를
사용하고, building terrain plate는 주변 parcel·road·ground와 섞이는 저대비 표현으로 바뀌었다. Core
terrain·hit geometry·게임 규칙·save와 자산 목록은 바꾸지 않았다.

renderer 불변조건, Debug build와 전체 `./dev check`가 PASS했다. exact final commit의 native full-screen에서
normal world, 실제 line-construction mode와 전신주 망 fixture를 관찰해 강·교량·건물 조화와 pole 상단 간
도체 연결을 다시 확인했다. 이는 한 LLM의 formative native 관찰이며 사람 미감 승인이나 공식 UX 점수가
아니다. package·외부 gate·push·PR·merge·배포는 수행하지 않았다.

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
