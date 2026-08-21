# G.3 target formative judge 기록

`pair-normal-reference-first-r1.json`은 judge transport와 rubric을 검증한 첫 단일 호출이다.

- judge: `gpt-5.6-sol`
- reasoning effort: `ultra`
- Codex CLI: `0.147.0`
- order/replicate: `REFERENCE_FIRST / 1`
- 입력: `assets/01-grid-construction.png` 대 `normal-first-light-v1-source.png`
- 결과 label: camera `CLOSE`, density `RELATED`, river `RELATED`, scale `CLOSE`,
  material `CLOSE`, grid `CLOSE`, HUD `RELATED`, timeline `WEAK`
- 해당 pair 가중 산술: `73.33`

이 값은 2 order × 2 replicate가 없는 **비공식 formative probe**이며 `ReferenceParity`가 아니다.
다만 target v1 자체가 기준에 충분히 가깝지 않다는 명확한 수정 신호로 사용한다. target v2와 runtime은
강을 더 좁고 사선으로 굽히고, 지구 사이의 음영 여백을 회복하고, inspector와 timeline chrome의
비율을 reference 하단 strip에 더 가깝게 줄여야 한다.
