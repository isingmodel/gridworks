# Gridworks 문서 안내

이 디렉터리는 **현재 `./assets` 스타일 실시간 게임 목표에 필요한 문서만 전면에 둔다.** 완료·중단된
과거 상세 계약은 현재 권한과 혼동되지 않도록 Git 이력으로 돌리고, 핵심 사실만 `archive/`에
압축했다.

## 현재 문서 구조

```text
docs/
├── README.md
├── ROADMAP_2D.md
├── ROADMAP_2D_CHECKLIST.md
├── product/
│   ├── GAME_DESIGN_KO.md
│   ├── OBJECT_CATALOG.md
│   └── VISUAL_PRODUCTION_SPEC.md
├── scopes/
│   └── ASSET_STYLE_REALTIME_GAME.md
└── archive/
    ├── README.md
    └── COMPLETED_HISTORY.md
```

## 질문별 소유권

| 질문 | 소유 문서 | 경계 |
|---|---|---|
| 지금 무엇을 할 수 있는가? | [루트 README](../README.md) | 목표와 코드 구현 권한을 구분 |
| 최종적으로 어떤 게임을 만드는가? | [게임 기획서](product/GAME_DESIGN_KO.md) | 단계별 파일·절차는 만들지 않음 |
| `./assets` 스타일을 게임에서 어떻게 재현하는가? | [비주얼 제작 명세](product/VISUAL_PRODUCTION_SPEC.md) | 규칙·수치를 새로 계산하지 않음 |
| 현재 전체 목표와 금지 범위는 무엇인가? | [에셋 스타일 실시간 게임 계약](scopes/ASSET_STYLE_REALTIME_GAME.md) | 활성 코드 gate가 없으면 구현하지 않음 |
| 어떤 순서로 전환하는가? | [로드맵](ROADMAP_2D.md) | 다음 단계 후보는 자동 승인 아님 |
| 현재 단계와 증거 상태는 무엇인가? | [체크리스트](ROADMAP_2D_CHECKLIST.md) | 긴 로그·규칙을 복제하지 않음 |
| 설비와 상태는 무엇인가? | [오브젝트 카탈로그](product/OBJECT_CATALOG.md) | 현재 규칙과 시각 준비 상태를 분리 |
| 과거에 무엇을 완료·중단했는가? | [완료 이력](archive/COMPLETED_HISTORY.md) | 현재 구현 권한이나 현재 증거가 아님 |

충돌하면 다음 순서를 따른다.

```text
현재 사용자 지시
→ 루트 README의 활성 구현 gate
→ 현재 목표 계약
→ 질문별 소유 문서
→ 압축 과거 이력
```

## 현재 경계

- `CurrentGoal = ASSET_STYLE_REALTIME_GAME`
- `DocumentationBaseline = A0_COMPLETE`
- `ActiveImplementationGate = NONE`
- `NextCandidate = A1_NORMAL_OPERATION_ART_SLICE_NOT_OPENED`
- 기본 장면은 `CommercialMain`이다.
- R1/R2는 기반으로 보존하지만 R2 종료 gate를 새로 PASS로 만들지 않는다.
- `./assets` 네 이미지는 visual reference authority이며 runtime·규칙·숫자 authority가 아니다.
- 이전 HTML/CSS 목표 화면은 현재 스타일 목표에서 폐기했다. 파일은 Git commit `9aceaf7`로 복구할 수
  있고 현재 증거로 사용하지 않는다.

## 작업 읽기 순서

1. 루트 [README](../README.md)를 읽는다.
2. [현재 목표 계약](scopes/ASSET_STYLE_REALTIME_GAME.md)을 처음부터 끝까지 읽는다.
3. 작업 질문의 소유 문서 하나만 추가로 읽는다.
4. 승인된 gate 밖 코드·data·asset·scene은 만들지 않는다.
5. 작업이 끝나면 체크리스트와 현재 상태를 같은 변경에서 갱신한다.

과거 상세 내용이 필요하면 새 현재 문서로 되살리지 말고
[아카이브 안내](archive/README.md)의 Git 조회 방법을 사용한다.
