# G.3 Step 3 — 원자 grid/facility·전력망 종료 증거

이 디렉터리는 G.3 Step 3의 고정 증거다. 발전소는 본관·굴뚝·터빈동·breaker bay를 개별 PNG와
명시 placement record로 조립하며, 변압기·표준/보강 철탑·교량 foundation도 각각 한 기능 object다.
발전소 전체, 변전소 전체, 송전 경로 또는 도시를 한 장에 구운 raster는 runtime에 없다.

## 고정 구조 판정

- protocol: `G3-ATOMIC-GRID-AUDIT-v1`
- judge: `gpt-5.6-sol`, `ultra`, `codex-cli 0.149.0`
- atomic board: `pair-kit-grid-atomic-board.png`
- board SHA-256: `0758b6f016fe59443e4bc30727f2746d5419a957317e3dee58581448cd49cda4`
- result: **PASS** — G01~G08 모두 `singleCompositionUnit=true`
- `containsWholeFacilityOrRoute=false`, `containsAtlasOrAlternatives=false` 8/8
- runtime `largeBakedGridFacilityRasterPresent=false`
- runtime state facts: 원자 발전소 조립, 개별 철탑, amber 계획/cyan 통전 구분 — 모두 `true`
- critical failure: 0

원문 판정은 `atomic-grid-audit-sol-ultra.json`, 실행 고정값은
`atomic-grid-audit-sol-ultra.execution.json`, board 조립 recipe와 입력 hash는
`pair-kit-grid-atomic-board.recipe.json`에 있다.

## Native 증거

- `1920x1080-ui100-discrete-art-path.png`: 네 발전소 부품 조립, 변압기, 개별 철탑과 cyan 통전 경로
- `1920x1080-ui100-pole-draft.png`: 실제 입력으로 배치한 amber 계획 철탑·dual conductor
- `1920x1080-ui100-substation-draft.png`: 실제 입력 변전소 초안과 개별 변압기 sprite
- 나머지 PNG: 제목, 정상 camera의 고온/홍수 강 상태와 UI 125% ReduceMotion 회귀
- presentation smoke: **PASS**, `resolution=1920x1080`, `discrete-tiles=17`,
  `discrete-objects=34`, `planned-class-sprites`, `event-timeline`
- 720p mode나 증거는 만들지 않았다.

## 결정론적 결과

- CommercialChecks: **22 suites / 2,286 assertions PASS**
- Game Debug·Release rebuild: **0 warnings / 0 errors**
- 1920×1080 placement smoke: **PASS**, `nodes=20`, `edges=21`, `zoom=전체 보기`
- 1920×1080 thermal smoke: **PASS**, `continuous|emergency|protective-outage`
- Step 3 runtime asset: 새 PNG 8개, 각각 한 번의 선택된 ImageGen call과 보존 source/hash 보유
- main plant는 4개 독립 placement record를 projected-Y로 정렬하며, player draft도 선택한 pole class의
  개별 sprite를 사용한다.

이 구조 gate는 최종 ReferenceParity 점수를 대신하지 않는다. 전체 완료는 고정 jury의
`ReferenceParity > 90`이며, 90.0은 실패다.
