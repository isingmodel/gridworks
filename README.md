# Gridworks

`Gridworks`는 이미 마을·병원·공장이 자리 잡은 지역에 발전소, 송전선과 변전소를 건설하고
운영하는 싱글 플레이 전력망 전략 게임이다. 배전 변전소의 위치와 형식은 기하학적 서비스
권역을 정하지만, 그 권역이 발전소까지 실제 경로로 이어져 통전돼야 전기가 공급된다.

플레이어는 공급한 전력으로 돈을 벌고, 공급하지 못한 전력에는 판매 손실과 정전 보상을
부담한다. 가장 싼 망만 만들면 사고에 취약하고, 모든 설비를 이중화하면 돈이 모자란다.
따라서 목표는 **필수 안전 기준을 만족하면서도 설명 가능하고 경제적인 전력망**을 만드는
것이다.

## 게임의 핵심

- 마을·병원·공장은 플레이어가 짓는 도시가 아니라 지켜야 할 수요처다.
- 배전 변전소의 서비스 권역은 접속 가능 범위이지 전기의 원천이 아니다.
- 발전소에서 수요처까지 이어지는 모든 선로와 변전소가 충분한 용량을 가져야 한다.
- 완성 제품에서는 발전소·변전소와 전신주/철탑을 직접 배치하고, 인접 오브젝트 사이 길이
  한계 안에서 전선을 연결한다.
- 건설에는 돈과 시간이 들며 완공 전에는 사용할 수 없다. 완공 설비의 철거도 추가 비용·시간과
  무전압 조건을 요구한다.
- 실제로 전달한 전력만 매출이 되고, 미공급 전력에는 고객별 보상이 붙는다.
- 폭염과 예고된 설비 사용불가는 미리 선택한 경로, 여유와 정비의 가치를 드러낸다.
- 화면 구석의 선형 예고 타임라인은 폭염, 공장 증설과 계획된 사용불가를 대비 가능한 시점에
  보여주며, 중요한 경보에서 시간을 멈춘다.
- 병원 같은 중요시설은 단일 전기고장과 공간 공통원인을 구분해 대비해야 한다.

완성 제품의 정서적 방향은 혹독한 산업 생존 전략이다. 풍화된 설비, 제한된 자원과 도시의
어둠을 통해 기반시설의 무게를 전달하되, 특정 작품의 세계관·도시 구조·UI를 복제하지 않는다.

## 현재 개발 상태

현재 활성 개발단위는 [**Scope 0B authored 2D playable**](docs/scopes/SCOPE_0B_PLAYABLE.md)다.
Scope 0A R2는 coverage·위험 인과·내부전원 경계·trade-off와 통합을 모두 `5/5`로 통과했고
[결과 checkpoint](playtests/scope-0a-r2/CHECKPOINT_2_R2_DECISION.md)도 완료했다. 종료된
[R1](docs/scopes/SCOPE_0A_CARD_TEST.md)은 불변 실패 증거로 남고 R2와 합산하지 않는다.

1. 서비스 권역과 실제 전력 공급은 다르다.
2. 전기적으로 다른 두 회로도 같은 공간 회랑을 쓰면 함께 끊길 수 있다.
3. 병원 내부전원이 환자를 지키는 것과 전력회사가 전기를 공급·판매한 것은 다르다.

Scope 0B의 계약·machine fixture, Core·검사도구·단일 Godot scene, 자동검사, native smoke와
독립 코드 review는 완료됐다. [L00 네이티브 조작](playtests/scope-0b/L00_RESULT.md)도 실제 화면에서
`FINAL`까지 통과했다. 공식 LLM 조작 proxy v1~v5는 각 실행 증거 규칙 문제로 게임 판정 없이
`PROXY-RUN-BLOCKED`이며 합산하지 않는다. 상세 이력과 현재 승인은
[evidence package](playtests/scope-0b/README.md)와
[checkpoint 1F](playtests/scope-0b/CHECKPOINT_1F_RUN_PROTOCOL_V6.md)가 소유한다.
`HumanValidationStatus = NOT_COLLECTED`도 유지한다.

v6는 build·fixture·UI·rubric·수치 gate를 바꾸지 않고 별도 runner manifest, custom timestamp와 participant
provenance export를 없앤다. 한 coordinator가 global preflight 뒤 교체 없는 다섯 cold row를 실행하며,
setup·participant·evidence 실패도 분모에서 지우지 않고 보수적인 `false`로 남긴다. platform은 spawn
본문 평문을 보존하지 않으므로 prompt hash는 실행 절차의 동결값이지 사후 평문 증거라고 주장하지 않는다.
v6의 실행 승인 상태는 [checkpoint 1F](playtests/scope-0b/CHECKPOINT_1F_RUN_PROTOCOL_V6.md)만 소유한다.
Scope 0의 순서는 [Scope 0 TODO](docs/scopes/SCOPE_0_TODO.md)가 관리하며, Scope 1은 Scope 0B의 실제
`GO`와 별도 적응형 점검 전까지 열리지 않는다.

## 문서 구조

세부 문서의 관계, 질문별 권위와 읽는 순서는 [문서 안내](docs/README.md)가 관리한다.
오브젝트별 건설·운영·철거 가능성은 [오브젝트 카탈로그](docs/product/OBJECT_CATALOG.md)에 있다.

| 경로 | 역할 |
|---|---|
| [README.md](README.md) | 프로젝트 진입점과 현재 개발 상태 |
| [AGENTS.md](AGENTS.md) | 저장소 작업 규칙 |
| [`docs/product/`](docs/product/) | 제품 비전, 오브젝트와 표현 규칙 |
| [`docs/scopes/`](docs/scopes/) | gate 실행 계약, 진행 TODO와 출시 후보 상한 |
| [`docs/development/`](docs/development/) | 조건부 개발·검증 도구 |
| [`docs/future/`](docs/future/) | 1.0 이후 장기 후보 |

문서 간 충돌이 있으면 현재 개발단위의 scope가 우선한다. 완료된 Scope 0A R2는
[R2 계약](docs/scopes/SCOPE_0A_R2_CARD_TEST.md)에 따라 [R1 카드 테스트](docs/scopes/SCOPE_0A_CARD_TEST.md)의
동결 fixture와 oracle을 값 변경 없이 사용했다. 현재 Scope 0B의 기계 권위 전환 조건과 정확한
fixture는 [활성 계약](docs/scopes/SCOPE_0B_PLAYABLE.md)만 정한다.

## 개발 방식

계획은 완성된 기능 목록을 순서대로 실행하는 로드맵이 아니다. 한 번에 하나의 가설만 열고,
큰 개발단위가 끝날 때마다 다음을 다시 판단한다.

- 무엇을 만들고 어떤 자동검사·사람 증거를 얻었는가?
- 어떤 가정이 지지되거나 반박되었는가?
- 다음 위험은 상호작용, 선택 구조, 시간, 물리, 경제, 표현 중 무엇인가?
- 다음 단위에서 무엇을 빼고 무엇만 상세화할 것인가?

미래 기능의 빈 인터페이스나 저장 필드를 미리 만들지 않는다. 파라미터도 처음부터 많이
열지 않으며, LLM 에이전트가 목표 점수를 향해 무제한 튜닝하는 방식을 사용하지 않는다.

### 활성 gate와 전환

`README.md`의 **현재 개발 상태**가 활성 gate를 가리키고, 연결된 scope 문서가 그 gate의
가설·포함·제외·숫자 권위·검증·완료조건을 정의한다. 후보 문서에 기능이 적혀 있다는 사실은
구현 승인이 아니다. 문서가 충돌하면 `현재 사용자 지시 → 활성 scope → docs/README.md가
지정한 질문별 소유 문서` 순으로 판단한다. 콘셉트 이미지는 규칙 권위가 아니다.

gate 통과는 다음 작업을 자동 승인하지 않는다. 큰 개발단위 뒤에는 결과와 남은 위험을 검토하고,
다음 단위가 명시적으로 승인되었을 때 같은 변경에서 다음을 함께 갱신한다.

1. 이 문서의 현재 개발 상태
2. 완료한 scope의 결과 또는 종료 상태
3. 새 활성 scope의 가설·범위·권위·검증·완료조건
4. 이전 gate를 아직 `현재`라고 부르는 문장과 교차 링크

활성 scope 밖의 기능, 범용 framework, 미래 interface, schema field와 placeholder UI를 미리
만들지 않는다. 숫자는 활성 scope가 지정한 한 곳만 기계 권위로 사용하고 코드·scene·문서에
독립적으로 복제하지 않는다.

### 검증과 파라미터

자동검사는 사람의 이해·재미·공정성을 대신하지 않는다. 사람 테스트 한 라운드에서는 정보
규칙, 구조 규칙 또는 parameter family 중 하나만 바꾸며, 물질적으로 바뀐 prototype의 결과를
이전 참가자와 합산하지 않는다. 전역 parameter 분류와 상한은
[Static Balance Lab](docs/development/BALANCING_STATIC_SIM.md)의 계약만 소유하며, 각 활성 scope는
그보다 엄격한 제한만 둔다.

작업 완료 전에는 활성 scope가 요구하는 formatting, link, unit, oracle, build와 smoke 검사를
실행한다. 열리지 않은 시스템의 테스트를 현재 완료조건에 추가하지 않는다.

## 기술 방향

조건부 첫 구현의 엔진 방향은 Godot .NET과 C#이다. 권위 게임 규칙은 Godot을 참조하지 않는
순수 .NET 코드에 두고, Godot은 명령을 보내고 결과를 그린다. exact toolchain과 설치·build 증거는
현재 활성 scope와 그 구현 checkpoint만 소유한다.

프로젝트 금융은 1.0과 현재 장기 확장 후보에서 제외한다. 원전·데이터센터·공유 냉각수도
전력망 핵심이 사람 플레이에서 검증되기 전에는 구현하지 않는다.

## 콘셉트 이미지

이미지는 분위기와 공간 구도를 공유하는 보조 자료이며 규칙이나 숫자의 권위가 아니다.

- [핵심 전력망 건설](assets/01-grid-construction.png)
- [폭염과 노후 송전선 사용불가](assets/02-heatwave-outage.png)
- [송전 경로 비교](assets/03-route-comparison.png)
- [기존 발전소 입지 구도](assets/04-plant-siting.png) — 레이아웃 참고 전용

## 단위와 법적 상태

화폐는 `M`으로 표시하며 `1 M = 1,000,000 CashUnit`이다. 전력은 `MW`, 에너지는
`MWh/GWh`, 절대시각은 `DAY 9 16:00`처럼 의미를 붙여 쓴다.

현재 저장소에는 라이선스가 없다. 공개 열람은 코드·문서·이미지의 재사용 허가를 뜻하지
않는다. 외부 기여를 받거나 재사용을 허용하기 전에 각 자산 범위의 라이선스와 기여 조건을
별도로 정해야 한다.
