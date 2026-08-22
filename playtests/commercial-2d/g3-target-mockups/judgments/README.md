# G.3 target formative judge 기록

normal v1~v4 JSON은 judge transport와 rubric, target 수정을 검증한 단일 formative 호출이다.

- judge: `gpt-5.6-sol`
- reasoning effort: `ultra`
- Codex CLI: v1 `0.147.0`; v2~v4 `0.149.0`
- order/replicate: `REFERENCE_FIRST / 1`
- 입력: `assets/01-grid-construction.png` 대 `normal-first-light-v1-source.png`
- v1: camera/scale/material/grid `CLOSE`, density/river/HUD `RELATED`, timeline `WEAK` → `73.33`
- v2: camera/scale/material/grid/HUD `CLOSE`, density/river `RELATED`, timeline `WEAK` → `75.56`
- v3: camera/scale `PARITY`, 나머지 여섯 criterion `CLOSE` → `89.17`
- v4: camera/density/river/scale/material/grid `PARITY`, HUD/timeline `CLOSE` → `97.50`

이 값들은 2 order × 2 replicate가 없는 **비공식 formative probe**이며 최종 `ReferenceParity`가 아니다.
v4는 처음으로 사용자 목표선 `>96`을 넘는 normal-state target이므로 runtime의 명시적 시각 계약으로
사용한다. final에서는 같은 exact candidate를 두 순서·두 replicate로 다시 평가해야 한다.
