# Gridworks 문서 안내

이 파일은 문서의 **내용이 아니라 소유권과 관계**를 관리한다. 프로젝트의 현재 상태와 활성
scope 지목은 루트 [README](../README.md), 실행 계약은 그 README가 지목한 scope가 맡는다.

## 디렉터리 구조

```text
docs/
├── README.md                         문서 지도와 질문별 소유권
├── product/
│   ├── GAME_DESIGN_KO.md             제품 비전과 안정된 게임 원칙
│   ├── OBJECT_CATALOG.md             오브젝트 정의와 기능 상태
│   └── VISUAL_PRODUCTION_SPEC.md      규칙을 표현하는 시각 기준
├── scopes/
│   ├── SCOPE_0_TODO.md               Scope 0 통합 진행 체크리스트
│   ├── SCOPE_0A_CARD_TEST.md          카드 테스트 실행 계약
│   ├── SCOPE_0B_CANDIDATE.md          다음 playable 후보의 경계
│   ├── SCOPE_1_INTERACTION_TODO.md    전신주·거리제한 미개방 후보
│   └── RELEASE_1_0_BOUNDARY.md        1.0 범위의 미개방 상한
├── development/
│   └── BALANCING_STATIC_SIM.md        조건부 정적 검증 도구
└── future/
    └── POST_1_0.md                    1.0 이후 격리된 후보
```

폴더는 문서의 권위 수준이 아니라 역할을 나타낸다. `scopes/` 안에서도 현재 scope 하나만 실행
권한을 가지며, 후보 scope는 backlog가 아니다.

## 질문별 소유 문서

| 질문 | 소유 문서 | 다른 문서가 해서는 안 되는 일 |
|---|---|---|
| 지금 무엇을 만들고 어떤 값으로 검증하는가? | 루트 README가 지목한 활성 scope | 후보 문서가 구현을 승인하거나 숫자를 덮어쓰지 않음 |
| 이 게임은 어떤 경험과 장기 원칙을 지향하는가? | [게임 기획서](product/GAME_DESIGN_KO.md) | 현재 scope의 절차·fixture를 복제하지 않음 |
| 무엇이 오브젝트이며 무엇을 할 수 있는가? | [오브젝트 카탈로그](product/OBJECT_CATALOG.md) | 미개방 기능을 현재 가능으로 표시하지 않음 |
| 규칙을 어떻게 보이게 하는가? | [비주얼 제작 명세](product/VISUAL_PRODUCTION_SPEC.md) | 게임 규칙이나 수치를 새로 만들지 않음 |
| Scope 0A/0B의 전환 checkpoint는 무엇인가? | [Scope 0 TODO](scopes/SCOPE_0_TODO.md) | 활성 실행 계약의 절차·숫자를 다시 정의하지 않음 |
| 수동 선로 Interaction 후보의 경계는 무엇인가? | [Scope 1 후보 TODO](scopes/SCOPE_1_INTERACTION_TODO.md) | 선행 결과 없이 구현하거나 실행 수치를 확정하지 않음 |
| 1.0의 최대 후보 범위는 무엇인가? | [Release 1.0 후보 범위](scopes/RELEASE_1_0_BOUNDARY.md) | 확정 roadmap이나 backlog로 사용하지 않음 |
| 정적 밸런스 도구는 언제·어디까지 쓰는가? | [Static Balance Lab](development/BALANCING_STATIC_SIM.md) | 게임을 대신 플레이하거나 수치를 자동 확정하지 않음 |
| 1.0 뒤의 냉각수·원전 방향은 무엇인가? | [1.0 이후 방향](future/POST_1_0.md) | 현재 interface·schema·UI를 선결하지 않음 |

충돌 시 우선순위는 `현재 사용자 지시 → 활성 scope → 해당 질문의 소유 문서`다. 이미지와 미래
후보는 현재 규칙의 권위가 아니다.

## 읽는 순서

### 현재 작업을 수행할 때

1. 루트 [README](../README.md)에서 활성 gate 확인
2. 활성 scope 전체 읽기
3. 작업 질문에 해당하는 소유 문서만 추가로 읽기
4. 미개방 후보는 범위 경계를 확인할 때만 읽기

### 제품 전체를 이해할 때

1. [게임 기획서](product/GAME_DESIGN_KO.md)
2. [오브젝트 카탈로그](product/OBJECT_CATALOG.md)
3. [Release 1.0 후보 범위](scopes/RELEASE_1_0_BOUNDARY.md)
4. 필요할 때만 비주얼·개발 도구·장기 후보 문서

## 유지 규칙

- 활성 gate와 실행 권한은 이 파일에 복제하지 않고 루트 README에서만 지목한다.
- 활성 gate가 바뀌면 루트 README, 완료 scope와 새 scope를 함께 갱신한다. 문서 역할이나 경로도
  바뀐 경우에만 이 안내를 갱신한다.
- 오브젝트의 건설·운영·철거 가능성이 바뀌면 오브젝트 카탈로그를 같은 변경에서 갱신한다.
- 파일을 이동하거나 역할을 나누면 이 트리와 모든 상대 링크를 같은 변경에서 고친다.
- 비주얼 문서와 후보 문서에 활성 fixture 숫자를 복제하지 않는다.
- 대체된 scope, critic 입력과 임시 결정 메모는 현재 문서 트리에 남기지 않는다.
