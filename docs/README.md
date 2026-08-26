# Gridworks 문서 지도

이 폴더는 문서를 네 종류로 나눈다. 현재 상태, 남은 일, 제품 기준, 완료 이력을 한 문서에 섞지
않는다.

## 처음 읽는 순서

1. [루트 README](../README.md) — 게임과 현재 실행 상태
2. [현재 작업 범위](ACTIVE_SCOPE.md) — 지금 허용된 변경
3. [개발 구조](ARCHITECTURE.md) — current R2의 권위와 변경 경로
4. [남은 작업](NEXT_TASKS.md) — 다음 후보와 완료 조건
5. 필요한 제품 문서 — 게임·비주얼·오브젝트·평가 기준
6. [완료 이력](archive/COMPLETED_HISTORY.md) — 과거 task 요약

## 질문별 문서

| 질문 | 문서 |
|---|---|
| 지금 구현해도 되는가? | [ACTIVE_SCOPE.md](ACTIVE_SCOPE.md) |
| current R2를 어디서 이해하고 변경하는가? | [ARCHITECTURE.md](ARCHITECTURE.md) |
| 무엇이 남았고 어떤 순서인가? | [NEXT_TASKS.md](NEXT_TASKS.md) |
| 최종 게임은 어떤 경험인가? | [GAME_DESIGN_KO.md](product/GAME_DESIGN_KO.md) |
| 화면과 아트의 통과 기준은? | [VISUAL_PRODUCTION_SPEC.md](product/VISUAL_PRODUCTION_SPEC.md) |
| 설비·시설·표현 coverage는? | [OBJECT_CATALOG.md](product/OBJECT_CATALOG.md) |
| 공식 LLM 점수·evidence·실행 권위의 gate는? | [COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md](product/COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md) |
| 평가 도구가 현재 무엇을 주장할 수 있는가? | [상용 UX 평가 도구](../tools/commercial-ux/README.md) |
| 완료·중단된 task는? | [COMPLETED_HISTORY.md](archive/COMPLETED_HISTORY.md) |
| 개발 실행과 설치 경계는? | [INSTALL.md](../INSTALL.md) |
| runtime 자산과 reference의 차이는? | [ASSET_MANIFEST.md](../ASSET_MANIFEST.md) |

## 권위 순서

문서가 충돌하면 다음 순서를 따른다.

```text
현재 사용자 지시
→ ACTIVE_SCOPE.md
→ README.md의 현재 실행 사실
→ 위 질문별 표가 지정한 current 문서
→ COMPLETED_HISTORY.md
→ Git의 과거 문서
```

`NEXT_TASKS.md`와 완료 이력은 구현 권한을 만들지 않는다. 현재 사용자 지시와
`ACTIVE_SCOPE.md`가 없으면 준비된 후보나 과거 계획을 실행하지 않는다.
위 순서는 변경 권한의 우선순위다. 개발 구조는 `ARCHITECTURE.md`, 제품 기준은 해당 제품 문서,
backlog는 `NEXT_TASKS.md`처럼 질문별 표가 지정한 문서가 세부 사실을 소유한다. 증거가 무엇을 주장할 수
있는지는 질문 소유 문서의 hard gate를 우회하지 않는다. 사용자 지시와 active scope가 평가 작업을
열어도 score-bearing execution authority가 없으면 공식 점수는 만들 수 없다.

## 오해를 막는 용어

- `authored`: 데이터와 story part가 존재함
- `native implemented`: R2 Core/controller/presentation 경로가 연결됨
- `direct-play observed`: fresh process의 production mouse/keyboard 입력으로 실제 경로를 확인함;
  사람 참가자의 사용성·미감·재미 증거는 아님
- `default scene`: Godot이 처음 여는 scene; 완성된 새 게임 여정을 뜻하지 않음
- `product title`: default boot의 `새 게임`·`이어하기` 시작 화면; debug launcher나 story modal이 아님
- `title Continue` (`이어하기`): 유효한 current R2 save를 복구하는 동작; story/modal continue와 다름
- `completed Continue`: current-v3 full `ProductCampaign` terminal을 epilogue 재생 없이 `Ended` world로
  복구하는 title Continue; prior v1/v2 completion은 포함하지 않음
- `completed New Game`: 같은 terminal title에서 기존 canonical 8장을 첫 장부터 다시 시작하는 action;
  chapter rewind/replay가 아님
- `reset New Game`: in-progress 또는 raw bytes를 읽을 수 있는 blocked save에서 첫 activation은 확인만
  표시하고, 두 번째 activation은 원본 sibling backup 성공 뒤 canonical 8장을 시작하는 action; completed
  New Game은 이 확인 없이 즉시 시작함
- `current R2 product settings`: title/gameplay가 공유하는 설정 authority와 strict persistence; exact package가
  app-owned root의 bytes를 fresh process에서 읽는 2B1 분류는 완료됐지만 packaged UI 조작이나 사람 접근성
  증거를 뜻하지 않음
- `current R2 basic audio`: source-tree generated ambient와 live Breaker/Energize/Outage cue wiring;
  packaged speaker 출력, 상태 전반의 audio coverage·청감 품질 증거를 뜻하지 않음
- `current R2 package identity candidate`: clean source와 exact internal ZIP/tree·runtime·PCK·G3·legal
  closure, ad-hoc signature와 임시 설치 위치의 headless title marker를 strict manifest로 결속한 후보; 빈
  user-data, 전체 캠페인, packaged settings/audio, 사람 UX·공증·출시 증거는 아님
- `current R2 app-owned persistence qualification`: exact package가 별도 빈 root의 current save/settings
  bytes를 user-argument 없는 fresh process에서 missing/loaded/restorable/completed로 분류한 2B1 record;
  engine `user://` 전체 격리, packaged production 입력/audio 또는 evaluation candidate를 뜻하지 않음
- `deterministic PASS`: 규칙·상태·wiring 검사 통과; 미감·재미·출시 품질을 뜻하지 않음
- `historical baseline`: 회귀 참고용 과거 제품; 현재 제품 entry가 아님
- `사건 지평선`: 한 줄 future-event bar의 플레이어용 이름; 코드명은 `RealtimeEventRail`
- `사건 시간축`: 사건 지평선의 시간 관계를 설명하는 일반 표현이며 별도 UI가 아님
- `current R2 evaluation candidate`: package identity와 app-owned persistence 2B1에 이어 packaged
  default-scene 전체 E2E 2B2까지 통과한 exact package; editor tree, V2 candidate 또는 title-only smoke가 아님
- `score-bearing`: 실제 후보·evidence·hard gate·model receipt에 결속되어 공식 점수에 들어갈 수 있음

완료 task는 이 폴더 곳곳에 복제하지 않는다. 새 단계가 끝나면
`archive/COMPLETED_HISTORY.md`에 한두 문단으로 추가하고 `ACTIVE_SCOPE.md`를 닫는다. `README.md`,
`ARCHITECTURE.md`, `NEXT_TASKS.md`와 질문별 표의 다른 current 문서 중 실제로 바뀐 사실을 소유한
문서만 함께 갱신한다. 세부 실행 로그와 과거 scope 원문은 Git 이력에 둔다.
게임·비주얼 제품 문서는 장기 목표와 통과 기준을 소유하며 현재 chapter 수나 미완료 목록을 복제하지
않는다. 평가 프로토콜의 `현재 판정`은 score/evidence 상한을 명시하는 질문 소유 기록이다.
