# Gridworks 문서 지도

이 문서는 질문마다 **어느 문서가 답을 소유하는지** 정한다. 현재 상태, 작업 절차, 코드 구조,
구현 backlog, 외부 release gate, 제품 기준과 완료 이력을 한 문서에 섞지 않는다.

## 처음 읽는 순서

1. [루트 README](../README.md) — 게임과 현재 실행 상태
2. [현재 작업 범위](ACTIVE_SCOPE.md) — 지금 허용된 변경
3. [Agent 작업 안내](AGENT_GUIDE.md) — 시작·검증·commit·review·handoff 절차
4. 이 문서 — 현재 질문의 단일 소유자 선택
5. 코드를 변경할 때만 [개발 구조](ARCHITECTURE.md) — current R2 ownership과 변경 경로
6. 필요한 backlog·gate·제품 문서
7. 과거 맥락이 필요할 때만 [완료 이력](archive/COMPLETED_HISTORY.md)

## 질문별 문서

| 질문 | 문서 |
|---|---|
| 지금 구현해도 되는가? | [ACTIVE_SCOPE.md](ACTIVE_SCOPE.md) |
| 작업을 어떤 순서로 시작하고 검증·종료하는가? | [AGENT_GUIDE.md](AGENT_GUIDE.md) |
| current R2를 어디서 이해하고 변경하는가? | [ARCHITECTURE.md](ARCHITECTURE.md) |
| 저장소 안에 구현할 일이 남았는가? | [NEXT_TASKS.md](NEXT_TASKS.md) |
| 물리 장치·사람·평가·출시에 무엇이 남았는가? | [RELEASE_GATES.md](RELEASE_GATES.md) |
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

`NEXT_TASKS.md`와 완료 이력은 구현 권한을 만들지 않는다. 현재 사용자 지시가 없거나
`ACTIVE_SCOPE.md`에 해당 결과물이 열려 있지 않으면 준비된 후보나 과거 계획을 실행하지 않는다.
위 순서는 변경 권한의 우선순위다. 개발 구조는 `ARCHITECTURE.md`, 제품 기준은 해당 제품 문서,
repository backlog는 `NEXT_TASKS.md`, 외부 증거·승인은 `RELEASE_GATES.md`처럼 질문별 표가
지정한 문서가 세부 사실을 소유한다. 증거가 무엇을 주장할 수
있는지는 질문 소유 문서의 hard gate를 우회하지 않는다. 사용자 지시와 active scope가 평가 작업을
열어도 score-bearing execution authority가 없으면 공식 점수는 만들 수 없다.

## 문서별 소유 범위

| 문서 | 소유하는 사실 | 갱신할 때 | 두지 않는 내용 |
|---|---|---|---|
| 루트 `README.md` | 현재 제품·실행·검증의 짧은 사실 | 플레이 가능한 경로, 기본 장면, 현재 capability가 바뀜 | task 로그, 미래 계획 |
| `ACTIVE_SCOPE.md` | 지금 허용된 결과물과 완료 검사 | 변경 작업을 열거나 닫음 | 여러 독립 목표, 장기 backlog |
| `AGENT_GUIDE.md` | 반복 가능한 작업 절차 | 시작·검증·handoff 규칙이 바뀜 | 제품 상태, 코드 세부 구조 |
| `ARCHITECTURE.md` | current code authority와 변경 경로 | ownership·dependency·entry가 바뀜 | 작업 권한, 과거 task 로그 |
| `NEXT_TASKS.md` | repository-controlled 구현 backlog | 확인된 제품 결함이나 새 목표가 구현 후보가 됨 | 외부 증거·승인, 자동 실행 권한 |
| `RELEASE_GATES.md` | device·사람·평가·서명·공개 승인 상태 | exact 외부 gate를 실행하거나 상태가 바뀜 | repository 구현 backlog |
| `product/*.md` | 장기 경험·화면·오브젝트·평가 기준 | 제품 기준 자체가 바뀜 | 현재 chapter 수, active scope |
| `scopes/*.md` | 과거 링크를 보존하는 tombstone | 오래된 외부 링크의 목적지가 필요함 | 현재 scope, 실행 지시 |
| `archive/COMPLETED_HISTORY.md` | 완료·중단 task의 짧은 결과와 한계 | scope를 닫고 장기 맥락이 필요함 | 현재 권한, 상세 실행 로그 |

한 사실은 위 소유 문서 한 곳에서 설명하고 다른 문서에는 짧은 요약과 링크만 둔다. 어떤 문서를
갱신할지 애매하면 파일 이름이 아니라 위 표의 질문으로 결정한다.

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
  app-owned root의 bytes를 읽는 2B1과 engine input Apply→fresh Restore·headless UI/audio runtime projection
  2B2는 완료됐지만 실제 display나 사람 접근성 증거를 뜻하지 않음
- `current R2 basic audio`: source-tree generated ambient와 live Breaker/Energize/Outage cue wiring;
  exact package의 generated stream/bus·ambient one-start·quiet SFX wiring은 2B2 완료, 실제 playback·speaker와
  상태 전반의 audio coverage·청감 품질 증거는 아님
- `current R2 package identity candidate`: clean source와 exact internal ZIP/tree·runtime·PCK·G3·legal
  closure, ad-hoc signature와 임시 설치 위치의 headless title marker를 strict manifest로 결속한 후보; 빈
  user-data, 전체 캠페인, packaged settings/audio, 사람 UX·공증·출시 증거는 아님
- `current R2 2B qualification`: exact package의 별도 app-owned root에서 2B1 data 분류와 2B2 bounded
  default-scene lifecycle InputEvent·settings restore·generated-audio wiring을 한 canonical record v2로 결속;
  engine `user://` 전체, full production input, hardware/speaker 또는 evaluation readiness를 뜻하지 않음
- `current R2 internal candidate complete`: 위 current graph의 8장 product·settings·audio wiring·package·
  combined 2B까지 repository-controlled 구현 backlog가 닫힘; 외부 release gate 통과나 공개 1.0을
  뜻하지 않음
- `deterministic PASS`: 규칙·상태·wiring 검사 통과; 미감·재미·출시 품질을 뜻하지 않음
- `historical baseline`: 회귀 참고용 과거 제품; 현재 제품 entry가 아님
- `사건 지평선`: 한 줄 future-event bar의 플레이어용 이름; 코드명은 `RealtimeEventRail`
- `사건 시간축`: 사건 지평선의 시간 관계를 설명하는 일반 표현이며 별도 UI가 아님
- `current R2 evaluation candidate`: 2B-qualified exact package를 첫 capture 전 versioned
  evaluation-session gate에 등록하고 candidate identity로 결속한 후보; full native journey
  capture·evidence는 이 후보에서 나오는 평가 산출물이며 bounded 2B만으로는 해당하지 않음
- `score-bearing`: 실제 후보·evidence·hard gate·model receipt에 결속되어 공식 점수에 들어갈 수 있음

완료 task는 이 폴더 곳곳에 복제하지 않는다. 작업 시작·종료 checklist는
[Agent 작업 안내](AGENT_GUIDE.md)를 따르고, 실제로 바뀐 사실을 소유한 current 문서만 함께 갱신한다.
세부 실행 로그와 과거 scope 원문은 Git 이력에 둔다. 제품 문서는 장기 목표와 통과 기준만 소유하며
현재 chapter 수나 미완료 목록을 복제하지 않는다. 평가 프로토콜의 `현재 판정`은 score/evidence 상한을
명시하는 질문 소유 기록이다.
