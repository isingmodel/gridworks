# G.3 v27 사용자 승인 종료 기록

이 기록은 G.3의 마지막 구현·평가 반복 v27을 고정한다. 사용자는 2026-08-22 v27까지 진행한 뒤
80점을 넘지 않더라도 성공으로 기록하고 단계를 닫도록 기존 `ReferenceParity >80` 종료선을
대체했다. 따라서 아래 점수를 숨기거나 공식 PASS로 재분류하지 않는다.

## 최종 구현

- 지원·검수 화면은 native `1920×1080`, UI 100%·125%뿐이다. 720p mode를 만들거나 실행하지 않았다.
- whole-map/city/river plate 없이 55개 개별 runtime art, 338개 원자 도시 배치와 641개 world instance를
  유지했다. 도시 질량은 장별로 같은 개별 배치의 scale을 조정해 normal/route의 밀도와 siting의 열린
  중앙 계곡을 분리했다.
- 폭염의 전역 주황 veil을 줄이고, 범람 수면과 제방 윤곽을 완화했다. 두 authored bridge foundation은
  모두 유지하되 북쪽 교량은 작은 보조 교량, 남쪽 교량은 주 landmark가 되도록 상대 scale을 낮췄다.
- `river-bank-rock-segment-a.png`는 built-in ImageGen run
  `exec-e9f4465a-4bfc-45ef-ae58-a0fed72d3822`의 단일 젖은 암석 제방 오브젝트로 교체했다. source와
  runtime SHA-256은 `68e47a2a142f150b90c4d91474b08c3d430f98fd11c1d401ed82375e49feef9a`다.
  RGBA alpha 범위 0–255, 완전 투명 pixel 76.10%, 네 모서리 alpha 0을 확인했다. 전체 강·배경·도시를
  생성하거나 뒤에 배치하지 않았다.

## 결정론적·native 증거

- `Gridworks Commercial checks: PASS (22 suites, 2331 assertions)`
- Game Debug·Release build: 각각 0 warnings, 0 errors
- 1920×1080 actual-input checkpoint: PASS, missions 4, edges 19, `input=focus-keyboard`
- 1920×1080 actual-input completion: PASS, missions 8, factual results와 epilogue
- 1920×1080 completed resume: PASS, complete state와 prior results 7개 복원
- 1920×1080 UI 100%·125% presentation: PASS, title/draft/path/heat/flood/timeline,
  `ReduceMotion=on`, keyboard focus와 bounds
- runtime 6장과 5개 deterministic pair board는 `runtime/`, `boards/`에 있으며
  `final-jury/manifest.json`이 최종 image SHA와 pair mapping을 고정한다.

## GPT-5.6-sol ultra formative

최종 캡처를 본 뒤 새 독립 process로 고정 pair 10개를 각각 한 번 실행했다. model은 전부
`gpt-5.6-sol`, reasoning effort는 `ultra`, order는 `REFERENCE_FIRST`, replicate는 `1`이다.
10개 accepted JSON은 `formative-v27/`에 있고 모두 strict schema와 evidence 좌표 검증을 통과했다.
`PAIR-KIT-GRID`의 첫 transport process는 7분 55초 동안 출력이 없어 종료했고, 같은 wrapper가 새
process로 재시도해 유효 JSON을 반환했다. 미완성 첫 응답은 점수에 포함하지 않았다.

| pair | 단일-call pair proxy |
|---|---:|
| PAIR-NORMAL | 79.1667 |
| PAIR-HEAT | 78.5000 |
| PAIR-ROUTE | 76.9444 |
| PAIR-SITING | 69.7059 |
| PAIR-FLOOD | 68.3333 |
| PAIR-KIT-GROUND | 65.0000 |
| PAIR-KIT-RIVER | 71.0000 |
| PAIR-KIT-GRID | 71.6667 |
| PAIR-KIT-CITY | 73.5714 |
| PAIR-KIT-UI | 77.0000 |

고정 label 환산과 category weight로 계산한 **formative single-call proxy는 `74.375`**다.

| category | median |
|---|---:|
| camera | 85 |
| density | 65 |
| river | 65 |
| scale | 75 |
| material | 85 |
| grid | 65 |
| HUD | 85 |
| state | 65 |
| timeline | 92.5 |

이 값은 order reversal, 두 번째 replicate, spread penalty, qualification과 blind evidence verifier가 없는
formative이므로 공식 `ReferenceParity`가 아니다. 공식 값은 `null`이며 자동 `>80` gate를 통과했다고
주장하지 않는다. 남은 구조 차이는 density·river·grid·state의 `RELATED(65)`다.

## 종료 판정

- protocol verdict: **산출 안 함** (`officialReferenceParity=null`)
- G.3 단계 상태: **COMPLETE — USER_APPROVED_V27_CLOSE**
- 종료 근거: 사용자가 v27 구현·평가 완료를 점수와 무관한 성공 조건으로 명시함
- 사람 review: 사용하지 않음
- 다음 단계: 자동으로 열지 않음; H 외부 검증·공개 후보는 계속 미승인
