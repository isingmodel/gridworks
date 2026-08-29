# 현재 작업 범위

## 상태

**활성 scope가 없다.**

변전소 반경 공급과 도시 가독성 scope는 완료됐다. Release V3 Core는 발전원에서 가동 변전소까지의
완공된 망만 전기 경로로 사용하고, 변전소가 등급별 반경 R 안의 전용 수요를 별도 수요측 선로 없이 직접
공급한다. 경계 포함, 다중 후보 선택, 용량·열·사용불가·보호정지와 실패 진단은 deterministic allocator가
소유한다. 화면은 exact R, 포함/밖 수요, 공급 변전소, 실제 통전 선로와 service 관계를 서로 다른 표기로
보여 준다.

native 도시는 강한 S자 강, 양안에 착지하는 두 교량, 도로 hierarchy, 통합 district·campus·저대비 infill과
발전소 service road를 사용한다. 전신주 도체는 class별 상단 anchor에 연결되고, 변전소 draft는 빈 parcel의
footprint·bay·반경·포함 수요·견적을 함께 표시한다. GPT-5.6-sol xhigh의 두 차례 skeptical LLM 형성평가는
최종 건물·배경 조화와 전문적 조직성 범위에 남은 P0/P1이 없다고 판정했다. 이는 사람 미감·사용성 승인이나
공식 점수가 아니다.

`./dev check`의 Realtime 27 suites/1,132 assertions, Commercial 31 suites/7,085 assertions, 57-file G3
identity, 41-file live map palette, save/settings failure matrix, FHD/QHD/UHD control-tree harness와 두 named
checkpoint가 PASS했다. headless 자동검사는 물리 display, 실제 하드웨어 입력이나 사람 평가를 주장하지
않는다. push, PR, merge, package·배포는 수행하지 않았다.

## 다음 변경을 여는 조건

- 읽기·설명·진단은 관련 질문 소유 문서를 read-only로 확인한다.
- 파일 변경은 사용자가 명시한 결과물 하나를 이 문서에 먼저 연다.
- 외부 gate 실행, push, PR, merge와 공개 배포는 각각 사용자의 명시적 권한이 있어야 한다.
- 시작 형식, 최소 검증과 종료 checklist는 [Agent 작업 안내](AGENT_GUIDE.md)를 따른다.

`NEXT_TASKS.md`, [외부 출시 gate](RELEASE_GATES.md), 준비된 코드와 과거 PASS는 자동으로
구현·평가·배포 권한을 만들지 않는다.
