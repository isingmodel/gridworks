# Commercial UX candidate text v2

> 후보 source commit: `9ff6571a5809cfdc8dc845ca7b8f896a43a24d6e`
>
> judge/verifier: `gpt-5.6-sol`, reasoning effort `ultra`
>
> 공식 `CommercialUXProxy`: **없음** — 이 묶음은 TEXT-PLAN 형성 평가만 소유함

이 묶음은 Gate C 후보의 캠페인 권위에서 정상 도달 가능한 26개 story part를 다시 추출한
TEXT-PLAN 평가다. native 앱을 플레이하거나 사람 사용성을 증명한 결과가 아니며, 공식 87점 gate에
합산하지 않는다.

## 결과

- 초기 3인 panel은 `TP-K2`가 `WEAK / STRONG / STRONG`으로 두 ordinal 단계 벌어져
  `RERUN_REQUIRED_JUDGE_INSTABILITY`로 닫혔다. 초기 결과를 점수로 선택하지 않았다.
- 프로토콜이 허용한 fresh replacement panel 전체를 한 번 실행했다. 교체 panel은 unstable cell 0,
  `TextRaw=95.3`, disagreement penalty `0.6375`, **`TextPlanProxy=94.6625`**로
  `SCORED_FORMATIVE`였다.
- 초기 `j01`, `j02`는 판정 내용이 끝난 뒤 field name transport가 strict schema와 달라 같은 agent가
  label·근거를 바꾸지 않는 schema-only transport correction을 한 번 수행했다. 이는 의미상 재판정이
  아니다.
- 별도 fresh verifier는 label, cell, polarity, score와 threshold를 보지 않고 177개 익명 관찰을
  sourceRef별로 검사했다. 155개 `SUPPORTED`, 22개 `PARTIAL`, 0개 `UNSUPPORTED`였으므로 전체 결과는
  **`BLOCKED_EVIDENCE_VERIFICATION`**이다.
- valid `PARTIAL`을 다시 뽑지 않았다. `formativeConclusionsAllowed=false`이므로 panel의 강점·약점·
  변경 제안은 제품 수정 근거로 사용하지 않는다. 숫자는 형성 panel의 산술 기록일 뿐 검증된 결론이나
  공식 native UX 점수가 아니다.

## 권위 hash

| artifact | SHA-256 |
|---|---|
| `story-manifest.json` | `4c7b9fcebee78087116d98d8a4a744fe977535c04f00788a31fcc77aa1735648` |
| `text-plan/input.json` file | `0b3c160371f5f55a49f7e61d9e9fa7af01f2793257a952a9d6a85d0e2b7395bd` |
| canonical text-plan artifact | `8e63340c123862d827e5c85926cbd6ac868eac93308a40f5825fe4b9ff37315c` |
| raw judge prompt template | `d31481546619063fcba5193d7c9043c5bb7e620d258ad3a6bc726fead6ff3be9` |
| raw judge schema | `69eb5143bc4821b14b90aa479da21620ddfffb20b63070d580184dfe35e69c04` |
| initial aggregate | `8d749f0c716d7897351f2b5257dbe40cb8b8873dc19b0516501d50cbdffbe74a` |
| replacement panel input | `cfb1c0fd55f4795e75b23372fe49ba536254fa0b1cdc7ba9754367ee129e38c3` |
| replacement receipt | `21303f802032cd88fb20901a7e9ff8c22a9f9084951a0da594efa47aa8bc770a` |
| replacement aggregate | `d9c8704acb004907847e72f8176972f5418289f085d12a85ff1aece6c362b54a` |
| raw verifier prompt template | `2481284cf9f1b359a42ec5ccdaad0ffb5107d35e643b62832af742f798f1b63c` |
| raw verifier schema | `d80429e50556e3482d3aafff4ac3cdd9dea8bef894efb6e8b40ebd4278875e56` |
| evidence input file | `ff7fb74392c08d836fec718e403375b28bf5ddc7ed46ee25f1814386f2d60092` |
| canonical verification input | `8b9781f17db940c93d076e29d58b4c3b8b292fd69d6ee55f8e8d2c56728db7b4` |
| verifier output | `f21fde05bf0221156f224ce9bd014870bd77faf58eca532c406dce354ad78ffc` |
| evidence result | `1ae05edcbee068dc44715139ef3f23ef06bd058478c60f6e225d20fc0551933e` |

Replacement judgment file SHA-256는 다음과 같다.

- `r01`: `f7100f23f03ffea2d469dea8ecf73f6cbaf4ac37140eda48184fc3f6a6aeec18`
- `r02`: `e31bee965e1c5dd8755647edee7ce922e9281f42042b1a37e4a3f1fcebe07ce4`
- `r03`: `44f00158cd3b54029c015f61efc028837f89e5857deec03ece0602273da0282f`

## 재현 검사

```sh
python3 tools/commercial-ux/test-text-plan-tools.py
python3 tools/commercial-ux/test-text-plan-evidence-verifier.py
```

두 self-test는 이 묶음을 보존하기 직전에 모두 PASS했다. replacement receipt는 single-use이므로 같은
aggregate 명령을 다시 실행하지 않는다. `prepare-text-plan-evidence.py`와 evidence aggregate는
side-effect-free 검증 경로로만 다시 확인할 수 있다.
