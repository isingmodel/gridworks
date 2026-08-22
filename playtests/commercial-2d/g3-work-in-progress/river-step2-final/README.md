# G.3 Step 2 — 원자 강물·제방 종료 증거

이 디렉터리는 G.3 Step 2의 고정 증거다. 강의 경로는 코드/authoritative world geometry가
소유하고, 이미지는 개별 수면 material tile 또는 짧은 제방·전이·교대·파문 object로만 배치한다.
전체 강, 지도, 구역, 도시를 한 장에 굽는 raster는 사용하지 않는다.

## 고정 구조 판정

- protocol: `G3-ATOMIC-RIVER-AUDIT-v1`
- judge: `gpt-5.6-sol`, `ultra`, `codex-cli 0.149.0`
- atomic board: `pair-kit-river-atomic-board.png`
- board SHA-256: `d7d6f21c6fca91f92c5fc402e355462deecfd15cd56bd3d3305cc75d6e52654a`
- result: **PASS** — R01~R12 모두 `singleCompositionUnit=true`
- `containsWholeRiverOrMap=false`, `containsAtlasOrAlternatives=false` 12/12
- runtime `largeBakedRiverRasterPresent=false`
- state facts: 정상 수면 visible, 고온은 더 좁고 건조, 홍수는 더 넓고 습윤 — 모두 `true`
- critical failure: 0

원문 판정은 `atomic-river-audit-sol-ultra.json`, 실행 고정값은
`atomic-river-audit-sol-ultra.execution.json`, board 조립 recipe와 입력 hash는
`pair-kit-river-atomic-board.recipe.json`에 있다.

## Native 증거

- `1920x1080-ui100-discrete-art-path.png`: 정상 수면·제방과 개별 도시/전력 object
- `1920x1080-ui100-river-heat.png`: 좁고 갈색인 저수위/고온 표현
- `1920x1080-ui100-river-flood.png`: 넓고 차가운 수면·파문·우천 표현
- 나머지 PNG: UI 100% 제목/공사 초안과 UI 125% ReduceMotion 회귀
- presentation smoke: **PASS**, `resolution=1920x1080`, `discrete-tiles=17`,
  `discrete-objects=32`, `planned-class-sprites`, `event-timeline`
- 720p 증거는 만들지 않았다.

## 결정론적 결과

- CommercialChecks: **22 suites / 2,265 assertions PASS**
- Game Debug·Release rebuild: **0 warnings / 0 errors**
- 1920×1080 placement smoke: **PASS**, `nodes=20`, `edges=21`, `zoom=전체 보기`
- 1920×1080 thermal smoke: **PASS**, `continuous|emergency|protective-outage`
- Step 2 runtime asset: 새 PNG 12개, 각각 한 번의 선택된 ImageGen call과 보존 source/hash 보유
- 강 control path는 authored corridor의 남단 `(1211, 2000)`에서 북단 `(1310, 500)`까지 이어지고,
  두 bridge deck은 authored foundation `(1330, 500)`, `(1480, 1500)`에 정렬된다.

이 구조 gate는 최종 ReferenceParity 점수를 대신하지 않는다. 전체 완료는 고정 jury의
`ReferenceParity > 85`이며, 85.0은 실패다.
