# Gridworks 멀티모달 LLM 배심원 레퍼런스 정렬 평가 프로토콜

> 상태: **DRAFT COMPLETE — G.3 후보의 LLM-as-a-judge 전용 시각 평가 절차, 구현 권한 아님**
> 사람 평가: **사용하지 않음**
> 기본 캔버스: **native 1920×1080, UI 100%**; UI 125%는 접근성 보조 세트

이 프로토콜은 개발 화면과 `assets/01~04`가 어디에서 얼마나 닮고 다른지 서로 다른 멀티모달 LLM
배심원만으로 판정한다. 사람이 점수를 주거나 tie를 깨지 않는다. 자동 image metric도 reference
parity 점수에는 들어가지 않는다. 파일·alpha·world authority·input·build 같은 결정론적 검사는 별도
hard gate로만 남긴다.

reference는 서로 다른 장면의 제품 concept이므로 whole-frame pixel match를 요구하지 않는다. 같은
의미 영역을 camera, density, river, object scale, material, grid, HUD, state, timeline으로 나누고
reference와 candidate를 직접 pairwise 비교한다.

## 1. 설계 근거와 한계

- 멀티모달 judge 연구는 자유 점수·batch ranking보다 pair comparison의 판별력이 상대적으로 낫지만,
  hallucination·bias·불일치가 남는다고 보고한다.
- LLM judge에는 position, verbosity, self-enhancement bias가 있다. 같은 pair의 입력 순서를 반드시
  뒤집고 결과가 유지되는지 검사한다.
- 단일 대형 judge 대신 서로 다른 model family의 panel을 사용하면 intra-model bias와 prompt 변화에
  대한 분산을 줄일 수 있다.
- judge model의 snapshot과 prompt가 바뀌면 결과를 이어 붙일 수 없다. exact model ID, sampling,
  prompt SHA를 고정하고 전체 세트를 다시 실행한다.

이 프로토콜은 LLM 판정이 사람 취향의 객관적 진실이라고 주장하지 않는다. 목적은 사람 review 없이도
같은 입력·rubric·jury로 반복 가능하고 보수적인 내부 visual gate를 만드는 것이다.

## 2. 고정 비교 세트

| pair ID | reference | 개발 캡처 | 주 비교 대상 |
|---|---|---|---|
| `PAIR-NORMAL` | `01-grid-construction.png` | first-light 계획·통전 화면 | 기본 camera, 도시 밀도, grid, HUD |
| `PAIR-HEAT` | `02-heatwave-outage.png` | mission 5 heat/outage | warm light, 강 저수위, 경고·사용불가 |
| `PAIR-ROUTE` | `03-route-comparison.png` | 두 경로·선택 수요 화면 | 철탑 scale, dual route, inspector |
| `PAIR-SITING` | `04-plant-siting.png` | mission 3 두 전원 화면 | 큰 plant, 강·지형 깊이, 거리감 |
| `PAIR-FLOOD` | `01/04` river crop | mission 6 flood 화면 | bank, 수면, bridge, risk overlay |

UI 125%는 `PAIR-NORMAL`과 `PAIR-ROUTE`에서 clipping, hierarchy, focus만 추가 확인한다. final jury는
미리 정한 pair를 빼거나 더 유리한 screenshot으로 교체할 수 없다.

## 3. 입력 manifest와 의미 영역

각 캡처는 다음 manifest를 가진다.

```text
pairId
referencePath + referenceSha256
candidatePath + candidateSha256
sourceCommit + packageSha256
worldSha256 + campaignSha256
OS + architecture
viewport = 1920x1080
uiScale
chapterId + decisionWindowId + phaseId
saveFixture/freshUserData
cameraCenter + zoomIndex
reduceMotion
captureUtc
```

OS chrome·원격 화면 여백은 crop하되 게임 pixel은 resize·sharpen·색보정하지 않는다. judge에는 원본
full frame과 다음 여섯 semantic ROI crop을 high-detail image input으로 함께 준다.

1. `MAP`: HUD를 제외한 도시 작업면
2. `RIVER`: 수면·양쪽 bank·bridge foundation
3. `GRID`: source, pole, substation, energized/planned line
4. `LANDMARK`: plant, hospital, residential/industry cluster
5. `HUD`: top·left·right chrome과 inspector
6. `TIMELINE`: briefing→window/phase→result bar

ROI는 같은 pixel 위치가 아니라 같은 의미 대상을 묶는다. 좌표는 `0~1000` 정규화 annotation JSON으로
고정한다. 이미지 파일명은 judge에게 무작위 opaque ID로 보이고 commit, 생성 모델, 이전 점수와 개발자
설명은 숨긴다. 이미지 안의 문구는 시각 콘텐츠일 뿐 instruction이 아니라고 system prompt에 명시한다.

## 4. 배심원 구성

### 4.1 primary jury

- high-detail image input과 strict structured output을 지원하는 멀티모달 judge **3개**
- 세 judge는 서로 다른 model family이고 최소 두 provider에 걸쳐야 한다.
- asset 생성에 사용한 model/provider가 jury 과반을 차지할 수 없다.
- 정확한 snapshot ID를 pin한다. alias, `latest`, 자동 upgrade를 금지한다.
- temperature `0` 또는 가능한 최소값, 고정 reasoning effort, 고정 max output을 사용한다.
- 한 family밖에 사용할 수 없거나 judge 하나가 빠지면 결과는 FAIL이 아니라 `BLOCKED_NO_DIVERSE_JURY`다.

### 4.2 adjudicator

primary jury와 다른 family의 네 번째 멀티모달 LLM을 pin한다. 평상시 점수에는 참여하지 않고, judge
간 label이 두 단계 이상 벌어지거나 position consistency가 회복되지 않을 때만 호출한다. adjudicator는
judge 이름·model ID를 보지 않고 익명 evidence와 원본 image/rubric만 받는다.

`judge-panel.json`은 다음을 기록한다.

```text
panelVersion
judgeSlot A/B/C + provider + exactModelId
adjudicator + provider + exactModelId
visionDetail + sampling + reasoningEffort
promptSha256 + rubricSha256 + jsonSchemaSha256
assetGeneratorFamily
qualificationRunIds
```

## 5. judge qualification과 calibration

실제 candidate를 보기 전에 각 judge가 같은 calibration pack을 통과해야 한다.

### 5.1 deterministic anchor

각 reference에서 다음 비교 pair를 기계적으로 만든다.

- identity: reference 대 동일 reference — `PARITY` 예상
- mild: 제한된 10% luminance 변화와 약한 blur — `CLOSE` 이상 예상
- structural: 25° 회전, landmark 50% 축소 또는 river straightening 중 하나 — `RELATED` 이하 예상
- blank: 단색/checker image — `DIFFERENT` 예상

anchor는 candidate 작업마다 새로 만들지 않고 hash를 고정한다. judge는 anchor label의 95% 이상을
예상 band에 넣고, identity를 `PARITY`, blank를 `DIFFERENT`로 모두 판정해야 한다.

### 5.2 stability qualification

각 calibration pair는 image order를 `reference→candidate`, `candidate→reference`로 뒤집고 두 번씩
실행한다. 다음을 모두 만족해야 한다.

- order reversal 뒤 label 차이 최대 한 단계
- 네 결과의 median absolute deviation 최대 한 단계
- 증거 좌표가 실제 ROI 안에 있음
- 존재하지 않는 object·text·state를 근거로 쓰지 않음

실패한 judge는 한 번만 전체 calibration을 재실행한다. 다시 실패하면 panel에서 제외하며 다른 family의
대체 judge가 없으면 `BLOCKED_NO_QUALIFIED_JURY`다.

## 6. 판정 label과 rubric

judge는 자유로운 0~100 점수를 만들지 않고 criterion마다 다음 label 하나를 고른다.

| label | 고정 점수 | 의미 |
|---|---:|---|
| `PARITY` | 100 | 같은 시각 체계로 즉시 인식되고 material gap이 없음 |
| `CLOSE` | 85 | 같은 체계이며 작은 scale·light·detail 차이만 있음 |
| `RELATED` | 65 | 방향은 같지만 camera·density·geometry 같은 구조 차이가 남음 |
| `WEAK` | 35 | palette나 일부 motif만 비슷함 |
| `DIFFERENT` | 0 | 사실상 다른 제품 화면 |

범주형 label 뒤의 숫자 변환과 가중합은 코드가 수행한다. LLM이 임의 중간점수나 전체 pass/fail을
결정하지 않는다.

### 6.1 100점 criterion

| criterion | 가중치 | judge가 비교할 내용 |
|---|---:|---|
| camera·perspective | 15 | 높이, yaw/pitch, 평행 투영, 지면 diamond 축 |
| scene density·composition | 15 | 빈 공간, 도로·건물·산업 회랑, 주요 질량 배치 |
| river·bank·bridge | 15 | 굽은 수로, 제방 깊이, 반사, 수위 상태, foundation 접합 |
| object scale·silhouette | 10 | plant·pole·substation·facility의 상대 크기와 형태 |
| material·lighting | 10 | 흑철·콘크리트·토사, 중간톤, amber 광원 |
| power grid | 10 | cyan/amber 도체, 철탑 연결, 경로 가독성 |
| HUD·inspector | 10 | 산업 HUD, panel 비례, 지도 위 overlay와 정보 위계 |
| chapter state variants | 10 | 평상·폭염·범람·겨울이 같은 세계의 상태 변화인지 |
| event timeline | 5 | 독립적인 사건 단계 bar로 즉시 인식되는지 |

각 pair는 화면에 실제 존재하는 criterion만 평가하고 위 가중치를 pair 내부에서 100으로 재정규화한다.
전체 category 점수는 지정 pair의 median으로 만든다.

| pair | 평가 criterion |
|---|---|
| `PAIR-NORMAL` | camera, density, river, scale, material, grid, HUD, timeline |
| `PAIR-HEAT` | camera, density, river, scale, material, grid, HUD, state, timeline |
| `PAIR-ROUTE` | camera, density, river, scale, material, grid, HUD, timeline |
| `PAIR-SITING` | camera, density, river, scale, material, grid, HUD |
| `PAIR-FLOOD` | camera, river, material, grid, state |

이 mapping은 rubric SHA에 포함하며 실행 중 judge가 항목을 추가·삭제하거나 가중치를 바꿀 수 없다.

## 7. judge 호출 절차

각 `judge × pair`는 다음 네 번 실행한다.

1. reference first, replicate 1
2. candidate first, replicate 1
3. reference first, replicate 2
4. candidate first, replicate 2

primary 3개 × pair 5개 × 4회 = 기본 **60개 독립 판정 호출**이다. seed를 지원하면 replicate마다
predeclared seed를 쓰고, 지원하지 않아도 run ID와 응답 hash를 기록한다.

prompt는 다음 순서를 고정한다.

1. 두 image role과 semantic ROI 설명
2. criterion 하나의 정의와 label anchor
3. 먼저 `similar evidence`, 다음 `different evidence`
4. normalized bounding box를 가진 관찰 근거 1~3개
5. label 하나와 confidence `HIGH/MEDIUM/LOW`

hidden chain-of-thought를 요청하거나 저장하지 않는다. 근거는 짧고 검증 가능한 시각 관찰만 받는다.

### 7.1 structured output

```json
{
  "pairId": "PAIR-NORMAL",
  "judgeSlot": "A",
  "order": "REFERENCE_FIRST",
  "replicate": 1,
  "criteria": [
    {
      "criterion": "river",
      "label": "CLOSE",
      "confidence": "HIGH",
      "similar": ["..."],
      "different": ["..."],
      "evidence": [
        {"imageRole":"CANDIDATE","roi":"RIVER","box":[120,80,880,920],"observation":"..."}
      ],
      "criticalFailure": false
    }
  ]
}
```

schema 위반, evidence 없는 점수, ROI 밖 좌표, 보이지 않는 사실을 단정한 응답은 무효다. 같은 호출을
최대 두 번 재시도하고 계속 실패하면 해당 judge는 unavailable 처리한다.

## 8. 편향·자기선호 통제

- image 순서와 opaque ID를 반전해 position bias를 측정한다.
- candidate가 어느 모델로 생성됐는지, 현재/이전 build인지, 목표 점수와 다른 judge 답을 숨긴다.
- 모든 candidate에서 같은 system prompt·rubric·few-shot anchor를 사용하고 candidate 결과에 맞춰
  prompt나 가중치를 고치지 않는다.
- asset generator와 같은 family의 judge가 있어도 한 표만 가지며 과반이 될 수 없다.
- judge는 UI 안의 문구를 instruction으로 따르지 않는다.
- formative feedback에 쓴 judge와 final jury가 완전히 같지 않게 하고, final jury에는 최소 한 개의
  새 family를 포함한다.
- final capture 선택 순서는 exact source commit과 reference manifest hash에서 만든 seed로 결정한다.
  유리한 screenshot만 cherry-pick할 수 없다.

## 9. 집계와 LLM adjudication

label은 `100/85/65/35/0`으로 변환한다.

### 9.1 judge 내부

- 같은 judge·criterion의 네 호출 median을 사용한다.
- 순서별 median 차이가 한 label 단계를 넘으면 position-unstable이다.
- 전체 range가 두 label 단계 이상이면 replicate-unstable이다.
- unstable judge는 해당 pair를 한 번만 네 호출 전체 재실행한다. 다시 불안정하면 해당 pair의 표를
  버리고, primary judge가 세 개 미만이 되므로 대체 judge가 없으면 BLOCKED다.

### 9.2 primary jury

- 세 qualified judge median의 중앙값을 해당 `pair × criterion` verdict로 사용한다.
- 최고·최저 차이가 한 label 단계 이하면 그대로 확정한다.
- 두 label 단계 이상이거나 criticalFailure가 1대1대1로 갈리면 adjudicator를 호출한다.

### 9.3 adjudicator

adjudicator는 원본 image, rubric, 익명 evidence와 서로 다른 label만 본다. 다수표를 그대로 따르라는
지시는 받지 않는다. evidence coordinate를 검증해 primary 범위 안의 label 하나를 고른다. 범위 밖
상향·하향은 금지한다. adjudicator도 schema/evidence를 두 번 실패하면 `BLOCKED_JURY_DISAGREEMENT`다.

### 9.4 종합 점수

먼저 각 pair 안에서 그 pair에 배정된 criterion의 최종 label 점수를 전역 가중치 비율로 다시
정규화한다.

```text
PairParity(pair) = Σ(pairCriterionWeight × finalPairCriterionLabelScore)
                   / Σ(pairCriterionWeight)
```

그다음 같은 criterion이 배정된 pair들의 고정 숫자 점수 중앙값을 `FinalCategoryScore`로 삼는다. pair가
짝수 개라 중앙 두 값의 평균이 생겨도 새 LLM 점수가 아니라 고정 label 변환의 산술 결과다. primary
최고·최저 차이도 같은 순서로 pair별 차이를 구한 뒤 `CategorySpread` 중앙값으로 만든다.

```text
RawJuryParity = Σ(categoryWeight × FinalCategoryScore) / 100
RawSpread     = Σ(categoryWeight × CategorySpread) / 100
Penalty       = min(10, RawSpread × 0.25)
ReferenceParity = RawJuryParity - Penalty
```

자동 image metric과 사람 점수는 이 식에 들어가지 않는다. 모든 숫자는 LLM label을 결정론적으로
변환한 결과다.

| ReferenceParity | 해석 |
|---:|---|
| `90~100` | reference와 거의 같은 시각 체계 |
| `85~89.99` | G.3 최소 정렬선 충족 후보 |
| `75~84.99` | 관련성은 분명하지만 구조 차이로 실패 |
| `<75` | reference와 상당히 다른 제품 화면 |

이 band는 설명용이며 아래 개별 category·pair·spread 조건을 무시하는 override가 아니다.

### 9.5 점수 밖의 결정론적 hard gate

다음 검사는 취향·유사도를 판정하지 않으므로 LLM jury가 아니라 재현 가능한 코드와 native smoke가
소유한다. 결과는 `PASS/FAIL`뿐이며 `ReferenceParity`를 올리거나 내리지 않는다.

- capture manifest·hash·exact commit·1920×1080 viewport 일치
- asset manifest, RGBA/alpha, crop·atlas boundary, seam·누락 파일 검사
- 강·장애물·facility와 배치 가능 영역이 authoritative world data와 일치
- 선택·건설·통전·timeline의 actual-input round trip과 UI 100%·125% 접근성
- deterministic checks, clean Debug/Release build, 해당 campaign/native smoke

하나라도 실패하면 시각 점수와 무관하게 `FAIL_HARD_GATE`다. 반대로 전부 통과해도 LLM jury 기준을
충족하지 못하면 visual pass가 아니다.

## 10. 통과·차단 조건

G.3 visual pass는 다음을 모두 만족해야 한다.

- `ReferenceParity ≥85`
- camera, density, river의 `FinalCategoryScore`가 각각 `≥85`
- 개별 comparison pair 점수 `≥75`
- `Penalty ≤5`
- 두 개 이상 primary judge가 같은 criticalFailure를 지목한 항목 `0`
- 세 primary judge와 adjudicator가 모두 qualification 통과
- asset-level·world authority·input·accessibility·build hard gate 모두 PASS
- unresolved visual P0/P1 `0`

다음은 FAIL이 아니라 판정 불가 BLOCKED다.

- 서로 다른 family 3개를 확보하지 못함
- exact model snapshot이 사라짐
- calibration 실패
- adjudicator로도 두 단계 disagreement를 닫지 못함
- capture/manifest/ROI 누락

사람 review·owner 점수·사람 tie-break는 어느 단계에도 없다.

## 11. 차이 보고서

각 criterion의 최종 보고서는 LLM evidence를 합쳐 다음 형식으로 만든다.

```text
pairId / ROI / criterion
final label + fixed score
primary labels A/B/C + order consistency + replicate stability
adjudicator label if invoked
similar evidence with normalized boxes
different evidence with normalized boxes
critical failures
proposed single visual change
before path / after path
```

`P1`은 잘못된 camera, 직선·평면 강, landmark 절반 scale, 큰 도시 공백, timeline 비식별처럼 reference
체계를 깨는 차이다. `P2`는 한 asset의 light 방향·bank seam·UI 간격처럼 국소 차이다. severity는 별도
LLM 호출이 아니라 rubric의 고정 mapping으로 코드가 부여한다.

- `P1`: camera·density·river가 `RELATED` 이하, 어느 category든 `WEAK/DIFFERENT`, 또는 primary judge
  둘 이상이 같은 criticalFailure를 지목
- `P2`: 그 밖의 category가 `RELATED`, 한 judge만의 비다수 criticalFailure, 국소 seam·light·spacing 차이
- `P3`: `CLOSE` 안에서 남은 비차단 detail 차이

`P0`는 crash·data loss·실행 불가 같은 비시각 hard gate가 소유하고 LLM이 부여하지 않는다.

각 run은 다음을 보존한다.

- `reference-manifest.json`
- `capture-manifest.json`
- `annotations.json`
- `judge-panel.json`
- `prompt-and-rubric.sha256`
- `calibration/`
- `raw-judgments/`
- `adjudications/`
- `scorecard.json`
- `DIFFERENCE_REPORT.md`

## 12. formative와 final 분리

- formative jury는 target mockup, vertical slice, 전체 상태의 세 checkpoint에서 차이 보고서를 낸다.
- 개발자는 difference report로 자산·code를 바꿀 수 있지만 rubric·weight·pair는 바꿀 수 없다.
- final jury에는 formative에서 쓰지 않은 model family를 최소 하나 넣는다.
- final verdict는 exact clean commit에서 한 번만 낸다. 같은 commit의 결과가 낮다고 seed나 screenshot을
  바꿔 reroll하지 않는다. 변경 뒤 새 commit에서 전체 calibration과 60개 판정을 다시 실행한다.

720p 캡처나 검사는 만들지 않는다. 이 프로토콜의 유효 입력은 1920×1080 UI 100%·125%뿐이다.

## 13. 참고 근거

- [MLLM-as-a-Judge: Assessing Multimodal LLM-as-a-Judge with Vision-Language Benchmark](https://arxiv.org/abs/2402.04788)
- [Judging LLM-as-a-Judge with MT-Bench and Chatbot Arena](https://arxiv.org/abs/2306.05685)
- [Judging the Judges: A Systematic Study of Position Bias in LLM-as-a-Judge](https://arxiv.org/abs/2406.07791)
- [Replacing Judges with Juries: Evaluating LLM Generations with a Panel of Diverse Models](https://arxiv.org/abs/2404.18796)
- [G-Eval: NLG Evaluation using GPT-4 with Better Human Alignment](https://arxiv.org/abs/2303.16634)
- [OpenAI Graders API — image input, structured model graders and pinned sampling fields](https://developers.openai.com/api/reference/resources/graders)
- [OpenAI API versioning guidance — pin model versions and rerun evals](https://developers.openai.com/api/reference/overview#backwards-compatibility)
