# Gridworks 문서 안내

이 파일은 문서의 역할과 질문별 소유권을 정한다. 프로젝트의 현재 상태, 활성 단계와 구현 권한은
루트 [README](../README.md)가 소유한다. 로드맵에 적힌 기능은 자동 승인된 backlog가 아니다.

## 문서 구조

```text
docs/
├── README.md                         문서 지도와 질문별 소유권
├── DEVELOPMENT_HISTORY.md            압축된 과거 결정·증거·교훈
├── ROADMAP_2D.md                     2D 1.0까지의 단계와 범위
├── ROADMAP_2D_CHECKLIST.md           단계 상태와 최소 종료 증거 장부
├── product/
│   ├── GAME_DESIGN_KO.md             제품 비전과 안정된 게임 원칙
│   ├── COMMERCIAL_2D_GAME_DESIGN_PLAN_KO.md 상용 2D 게임 재기획 제안
│   ├── COMMERCIAL_2D_REFERENCE_PARITY_PLAN_KO.md 완료된 G.3 재구축 계획·이력
│   ├── REFERENCE_PARITY_EVALUATION_PROTOCOL_KO.md 동결된 화면·reference 평가 프로토콜
│   ├── OBJECT_CATALOG.md             오브젝트 정의와 기능 상태
│   └── VISUAL_PRODUCTION_SPEC.md      규칙을 표현하는 시각 기준
├── scopes/
│   ├── SCOPE_0B_PLAYABLE.md           완료된 Scope 0B 구현 기준
│   ├── SCOPE_1_INTERACTION.md         완료된 Scope 1 구현 기준
│   ├── FIRST_LIGHT.md                 완료된 첫 제품 단계 기준
│   ├── SECOND_HEART.md                완료된 병원 신뢰도·경제 구현 기준
│   ├── FACTORY_CAPACITY.md             완료된 공장 수요·발전소 용량 기준
│   ├── HEATWAVE_MAINTENANCE.md         완료된 폭염·예방정비 구현 기준
│   ├── CAMPAIGN_SAVE_SETTINGS.md        완료된 캠페인·저장·설정 구현 기준
│   ├── CAMPAIGN_CONTENT.md              완료된 세 장 콘텐츠 구현 기준
│   ├── RELEASE_2D.md                    완료된 내부 후보의 2D 표현·사운드·패키징 기준
│   ├── RELEASE_REBUILD.md               완료된 기술 기준선 재구축 계약
│   └── COMMERCIAL_2D_IMPLEMENTATION.md  완료된 상용 2D 게임 구현 계약
├── development/
│   └── BALANCING_STATIC_SIM.md        조건부 정적 분석 도구
└── future/
    └── POST_1_0.md                    1.0 이후 격리 후보
```

동결된 카드, 진행자료와 로컬 원본 위치는 [`playtests/`](../playtests/)에 있다. 그 자료는 과거
실행 입력과 증거이며 현재 구현 권한이나 제품 숫자를 새로 만들지 않는다. Git 제외 `private/`
원본을 재귀 삭제하지 않는다.

## 질문별 소유 문서

| 질문 | 소유 문서 | 경계 |
|---|---|---|
| 지금 무엇을 구현할 수 있는가? | [루트 README](../README.md) | 로드맵·후보가 구현을 승인하지 않음 |
| 무엇을 어떤 순서로 완성하려는가? | [2D 완성 로드맵](ROADMAP_2D.md) | 현재 단계의 정확한 계약을 대신하지 않음 |
| 진행과 종료 증거는 어디에 기록하는가? | [로드맵 체크리스트](ROADMAP_2D_CHECKLIST.md) | 기능 명세·숫자를 복제하지 않음 |
| 과거에 무엇을 확인하고 왜 바꿨는가? | [개발 이력](DEVELOPMENT_HISTORY.md) | 현재 권한이나 규칙을 소유하지 않음 |
| 게임의 경험과 장기 원칙은 무엇인가? | [게임 기획서](product/GAME_DESIGN_KO.md) | 단계별 숫자·절차를 만들지 않음 |
| 자유 배치·열 한계를 포함한 상용 2D 게임의 제품 기준은 무엇인가? | [상용 2D 게임 재기획서](product/COMMERCIAL_2D_GAME_DESIGN_PLAN_KO.md) | 경험·콘텐츠·표현 원칙을 소유하고 구현 경계·종료 증거는 완료 계약이 소유 |
| 오브젝트와 가능한 동작은 무엇인가? | [오브젝트 카탈로그](product/OBJECT_CATALOG.md) | 미래 기능을 현재 가능으로 표시하지 않음 |
| 규칙을 어떻게 보이게 하는가? | [비주얼 제작 명세](product/VISUAL_PRODUCTION_SPEC.md) | 게임 규칙을 새로 계산하지 않음 |
| `assets/` reference와 화면을 어떻게 다시 맞췄는가? | [레퍼런스 정렬 계획](product/COMMERCIAL_2D_REFERENCE_PARITY_PLAN_KO.md) | 완료된 G.3 구현·종료 기록이며 새 구현 권한이 아님 |
| reference와 개발 화면의 유사도·차이를 어떻게 판정했는가? | [평가 프로토콜](product/REFERENCE_PARITY_EVALUATION_PROTOCOL_KO.md) | 고정 `gpt-5.6-sol` ultra judge의 동결 절차이며 사람 증거가 아님 |
| 현재 고정 시나리오는 어떻게 동작하는가? | [Scope 0B 기준](scopes/SCOPE_0B_PLAYABLE.md) | 제품 전체 모델로 일반화하지 않음 |
| 현재 수동 선로 slice는 어떻게 동작하는가? | [Scope 1 기준](scopes/SCOPE_1_INTERACTION.md) | 고정 endpoint 밖의 기능을 열지 않음 |
| 완료된 첫 제품 구현은 무엇을 만들 수 있는가? | [첫 점등 통합](scopes/FIRST_LIGHT.md) | 완료 범위를 현재 권한으로 오해하지 않음 |
| 완료된 병원 제품 흐름은 무엇인가? | [두 번째 심장](scopes/SECOND_HEART.md) | 완료 범위를 다음 단계 권한으로 오해하지 않음 |
| 완료된 공장·발전소 흐름은 무엇인가? | [공장 수요와 발전소 용량](scopes/FACTORY_CAPACITY.md) | 완료 범위를 폭염·정비 권한으로 오해하지 않음 |
| 완료된 폭염·정비 흐름은 무엇인가? | [예고된 폭염과 예방정비](scopes/HEATWAVE_MAINTENANCE.md) | 완료 범위를 다음 단계 권한으로 오해하지 않음 |
| 완료된 캠페인·저장 흐름은 무엇인가? | [캠페인 골격·저장·기본 설정](scopes/CAMPAIGN_SAVE_SETTINGS.md) | 완료 범위를 콘텐츠·패키징 권한으로 오해하지 않음 |
| 완료된 세 장 콘텐츠는 무엇을 보장하는가? | [세 장 캠페인 콘텐츠 고정](scopes/CAMPAIGN_CONTENT.md) | 완료 범위를 아트·사운드·패키징 권한으로 오해하지 않음 |
| 완료된 2D 표현·사운드·package는 어디까지 보장하는가? | [2D 출시 마감](scopes/RELEASE_2D.md) | 외부 테스트·공개 배포를 포함하지 않음 |
| 완료된 기술 기준선은 무엇을 보장하는가? | [출시판 재구축](scopes/RELEASE_REBUILD.md) | 새 상용 제품의 구현 권한으로 사용하지 않음 |
| 완료된 상용 2D 게임은 무엇을 보장하는가? | [상용 2D 게임 구현](scopes/COMMERCIAL_2D_IMPLEMENTATION.md) | v2 권위·완료한 단계 B~G.3와 전체 종료조건을 고정 |
| 정적 분석 도구는 언제 쓰는가? | [Static Balance Lab](development/BALANCING_STATIC_SIM.md) | 자동 튜닝이나 사람 선택 대체에 쓰지 않음 |
| 1.0 이후 냉각수·원전 방향은 무엇인가? | [1.0 이후 방향](future/POST_1_0.md) | 1.0 schema·UI를 선결하지 않음 |

충돌하면 `현재 사용자 지시 → 루트 README가 지목한 활성 단계 → 질문별 소유 문서` 순서로
판단한다. 콘셉트 이미지, 과거 실행자료와 미래 후보는 현재 규칙의 권위가 아니다.

## 읽는 순서

### 개발 작업

1. [루트 README](../README.md)에서 활성 단계와 금지 범위를 확인한다.
2. 활성 단계가 있으면 그 단계의 별도 계약을 처음부터 끝까지 읽는다.
3. 질문별 소유 문서만 추가로 읽는다.
4. 작업 뒤 체크리스트, README와 영향을 받은 문서를 같은 변경에서 갱신한다.

활성 단계가 없으면 현재 사용자 요청이 문서·검토 작업인지, 새 구현 단계를 명시적으로 여는지
구분한다. 로드맵의 다음 줄을 임의로 구현하지 않는다.

### 다음 작업자 최소 읽기

1. [루트 README의 현재 상태와 인계](../README.md#현재-상태)
2. [체크리스트의 단계 상태](ROADMAP_2D_CHECKLIST.md)
3. 유지보수라면 [상용 구현 계약](scopes/COMMERCIAL_2D_IMPLEMENTATION.md)과 해당 코드·fixture
4. G.3 증거 재현이라면 [v27 종료 요약](../playtests/commercial-2d/g3-final-candidate/FORMATIVE_V27_SUMMARY.md)
5. H를 명시적으로 승인받은 경우에만 [로드맵의 H 게이트](ROADMAP_2D.md#9-단계-h--외부-검증과-공개-후보--미승인)를 읽고 외부 권한·입력을 확인

완료 로그 전체를 처음부터 다시 읽을 필요는 없다. 현재 사실은 README·체크리스트, 이유와 실패
경로는 개발 이력, 세부 재현은 Git과 승인된 `playtests/` 증거에서 찾는다.

### 프로젝트 이해

1. [게임 기획서](product/GAME_DESIGN_KO.md)
2. [현재 상태](../README.md#현재-상태)
3. [2D 완성 로드맵](ROADMAP_2D.md)
4. 필요한 경우 [개발 이력](DEVELOPMENT_HISTORY.md)과 완료 구현 기준

## 유지 규칙

- 상태·권한은 루트 README 한 곳에만 선언한다.
- 로드맵 설명과 체크리스트 상태를 분리한다.
- machine-readable fixture의 숫자를 문서나 장면에 별도 권위로 복제하지 않는다.
- 오브젝트 능력이 바뀌면 오브젝트 카탈로그를 같은 변경에서 갱신한다.
- 파일을 이동·삭제하면 문서 링크와 검증기의 경로를 같은 변경에서 고친다.
- 완료된 단계의 상세 로그는 압축 이력이나 Git 이력으로 보내고 현재 문서에 반복하지 않는다.
- 다음 단계 시작 전 이전 단계의 코드·증거·문서 최신성을 독립 검토한다.
