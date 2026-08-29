# 현재 작업 범위

## 상태

**활성 scope가 없다.**

발전원 캠퍼스 시각 교체 scope는 완료됐다. 서부 발전원은 낮은 보일러·터빈동, 건물에 결합된 중형
굴뚝, 변압기·개폐장을 가진 하나의 열발전 RGBA campus를 사용한다. 남부 발전원은 두 저층 가스터빈동,
세 짧은 배기 stack, heat-recovery 배관, 변압기·개폐장을 가진 별도 RGBA campus를 사용한다. 기존
main hall·turbine hall·smokestack·switchyard 조각 중첩 draw는 제거했다.

두 campus의 gate 끝은 native service road 시작점과 일치해 한 번만 연결된다. 실제 1229×768 native
캡처에서 두 발전원의 역할·스케일·실루엣이 서로 구분되고 주변 도로·도시 campus와 같은 3/4 camera,
좌상단 광원과 charcoal/warm/cyan 재질을 사용함을 확인했다. 내장 ImageGen의 도로 제거 edit와 후속
background extraction은 실제 alpha가 없어 폐기했고 최초 생성의 genuine RGBA 후보만 채택했다.

`./dev check`의 Realtime 27 suites/1,132 assertions, Commercial 31 suites/7,085 assertions, 57-file G3
identity, 39-file live map palette, save/settings failure matrix, FHD/QHD/UHD control-tree harness와 두 named
checkpoint가 PASS했다. 발전량·열·반경 공급·경제·story·save schema는 바꾸지 않았다. 사람 미감 승인,
package/release gate, push·PR·merge·배포는 수행하지 않았다.

## 다음 변경을 여는 조건

- 읽기·설명·진단은 관련 질문 소유 문서를 read-only로 확인한다.
- 파일 변경은 사용자가 명시한 결과물 하나를 이 문서에 먼저 연다.
- 외부 gate 실행, push, PR, merge와 공개 배포는 각각 사용자의 명시적 권한이 있어야 한다.
- 시작 형식, 최소 검증과 종료 checklist는 [Agent 작업 안내](AGENT_GUIDE.md)를 따른다.

`NEXT_TASKS.md`, [외부 출시 gate](RELEASE_GATES.md), 준비된 코드와 과거 PASS는 자동으로
구현·평가·배포 권한을 만들지 않는다.
