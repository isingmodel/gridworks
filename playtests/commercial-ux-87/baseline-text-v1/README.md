# 상용 UX 텍스트 기준선 v1

이 디렉터리는 `e6682e6fd3e7d00f263673338dc039faaf4103e1`에서 고정한 첫 텍스트 계획
후보와 두 패널의 원본 판정을 보존한다. 이 실행은 실제 게임 UX 점수가 아니며
`officialCommercialUX = false`다.

## 입력

- judge: `gpt-5.6-sol`, reasoning effort `ultra`
- story manifest SHA-256: `8b7eaa37d4c146508b8dd111f0786aec6ffe7d1aae2fe61603f00c6bddcf1b86`
- text-plan envelope file SHA-256: `8fe77e8c2ac3eaa04f9b640254f869f18da8859525400d2180f1cbe73c978218`
- canonical artifact ID: `sha256:7c09be293114a4fd6e8171b9d7c9d52d2eba324de0570297c52f669f6192c72c`
- prompt template: `sha256:d31481546619063fcba5193d7c9043c5bb7e620d258ad3a6bc726fead6ff3be9`
- judgment schema: `sha256:69eb5143bc4821b14b90aa479da21620ddfffb20b63070d580184dfe35e69c04`

## 판정

초기 패널 `j01`~`j03`은 `TP-K2`에서 `WEAK / WEAK / STRONG`으로 갈려
`RERUN_REQUIRED_JUDGE_INSTABILITY`가 됐다. 프로토콜이 허용한 한 번의 fresh replacement
패널 `j04`~`j06`은 이전 판정이나 불안정 셀을 보지 않고 같은 hash-pinned 입력을 평가했다.

replacement도 다음 두 셀에서 ordinal range 2가 남아 최종 상태가
`BLOCKED_JUDGE_INSTABILITY`다.

- `TP-A4`: `STRONG / SERVICEABLE / EXCELLENT` — 마지막 산업 약속을 결과 전에 언제, 어떤
  요구로 제시하는지가 authored text에서 충분히 고정되지 않았다.
- `TP-P1`: `SERVICEABLE / STRONG / EXCELLENT` — 세 번째 튜토리얼의 종료 선언은 있지만 어떤
  안내가 단계적으로 사라지고 무엇이 남는지가 고정되지 않았다.

따라서 v1에는 `textPlanProxy`가 없으며 숫자 점수를 만들지 않았다. scored aggregate가 아니므로
blind evidence verifier 입력도 만들지 않았다. 다음 후보는 위 두 전달 구조와 반복해서 지적된
한국어 문구(`draft`, `reset`, `장간 시간경과`, `작성된 정비 시간`)를 제품 권위에서 고친 뒤 새
artifact ID로 처음부터 평가해야 한다.

## 무결성 파일

- initial aggregate SHA-256: `00ba3f6192c7a54151ff7c9e99ec1ac95af50bf0c1bce22e0fd7b513f6a66069`
- replacement aggregate SHA-256: `e5e4d6a372d1e7758ec3be7fbb275dcc986f61cabcc5057fbda347a7eb41790d`
- single-use replacement receipt SHA-256: `275e7a6ccaf81e45557d67722acc7dd691a68ae3d01044e9460000c7d2431b39`
- initial judgment file SHA-256:
  - `j01`: `f4635204f14d4759c02a82632095b472fb34e6553b3e8c675cd1708c27f7a1c6`
  - `j02`: `82556c241780ff881b35d32c88ac09acf35a795f43a75394242c803c7d856638`
  - `j03`: `8f63b8619233b8c88711b8ee7fbe619864a86c6a3f47f8909abec9d9d1f17ce4`
- replacement judgment file SHA-256:
  - `j04`: `373068508a569df42c1524b578c67b121f2c793569b8e924bab0543d0a25037d`
  - `j05`: `19deb22c66b0305ccd6af682076da79947ccaf40853ea379e605ffc423c0ed92`
  - `j06`: `bf013f2c971b6305743085ea75568f99ca4ea62c5a931dfc3398944e584c66ca`

