# Gridworks 레퍼런스 정렬 평가 프로토콜

> 상태: **DRAFT — G.3 후보의 제작·검수 절차, 구현 권한 아님**
> 목적: 개발 화면과 `assets/01~04`가 어디에서 얼마나 닮고 다른지 반복 가능한 방식으로 기록
> 기본 캔버스: **native 1920×1080, UI 100%**; UI 125%는 접근성 보조 세트

이 프로토콜은 전체 화면 pixel match를 요구하지 않는다. reference는 서로 다른 장면을 묘사하고 게임은
실제 v2 규칙·한국어 UI를 표현해야 하기 때문이다. 대신 같은 의미 영역을 camera, geometry, scale,
density, material, light, river, grid, UI로 나눠 측정하고 마지막에 사람이 나란히 판정한다.

## 1. 비교 원칙

- reference와 개발 화면의 원본을 보존하고 SHA-256을 manifest에 기록한다.
- OS chrome, 원격 화면 여백과 촬영기 bezel은 crop하되 게임 pixel은 resize·sharpen·색보정하지 않는다.
- 게임은 같은 commit/package, fresh user data, authored chapter/state, home camera에서 캡처한다.
- asset sheet와 실제 runtime 화면을 둘 다 본다. 단품 sprite가 닮아도 배치 화면이 다르면 통과가 아니다.
- whole-frame SSIM/PSNR, ImageGen 또는 LLM의 단독 자기평가, 자동 embedding 한 값만으로 pass하지 않는다.
- visual score는 gameplay authority, input, accessibility, build 검사를 대신하지 않는다.

## 2. 고정 비교 세트

| pair ID | reference | 개발 캡처 | 주 비교 대상 |
|---|---|---|---|
| `PAIR-NORMAL` | `01-grid-construction.png` | first-light 계획·통전 화면 | 기본 camera, 도시 밀도, grid, HUD |
| `PAIR-HEAT` | `02-heatwave-outage.png` | mission 5 heat/outage | warm light, 강 저수위, 경고·사용불가 |
| `PAIR-ROUTE` | `03-route-comparison.png` | 두 경로·선택 수요 화면 | 철탑 scale, dual route, inspector |
| `PAIR-SITING` | `04-plant-siting.png` | mission 3 두 전원 화면 | 큰 plant, 강·지형 깊이, 거리감 |
| `PAIR-FLOOD` | `01/04` river crop | mission 6 flood 화면 | bank, 수면, bridge, risk overlay |

UI 125%는 같은 `PAIR-NORMAL`과 `PAIR-ROUTE`에서 clipping, hierarchy, focus만 추가 확인한다.

## 3. capture manifest

각 캡처는 다음 필드를 가진다.

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

누락된 manifest, OS scaling이 적용된 screenshot, 임의 pan/zoom 캡처는 점수 대상에서 제외한다.

## 4. 의미 영역 ROI

각 pair에는 좌표가 고정된 여섯 ROI와 전체 frame이 있다.

1. `MAP`: HUD를 제외한 도시 작업면
2. `RIVER`: 수면·양쪽 bank·bridge foundation
3. `GRID`: source, pole, substation, energized/planned line
4. `LANDMARK`: plant, hospital, residential/industry cluster
5. `HUD`: top·left·right chrome과 inspector
6. `TIMELINE`: briefing→window/phase→result bar

reference와 candidate의 ROI는 같은 pixel 위치가 아니라 같은 의미 대상을 묶는다. landmark bounding box,
river bank mask와 grid centerline을 별도 annotation JSON으로 저장한다.

## 5. asset-level 검사

모든 tile/object는 중립 회색과 checker background 두 장에서 fixed scale로 contact sheet를 만든다.

- 파일명·family·reference role·prompt·SHA-256 존재
- object는 RGBA, corner alpha `≤1`, transparent pixel `≥35%`, black/white matte·halo 없음
- tile은 3×3 반복에서 seam·밝기 jump·눈에 띄는 동일 focal feature 없음
- 주요 ground edge는 2:1 isometric 축 `±26.565°`에서 `±3°` 이내
- object face와 shadow 방향은 style bible과 일치
- 홈 zoom pixel size가 plan range 안에 있음
- bridge·bank처럼 맞닿는 asset은 접합 오차 `≤2px`

하나라도 실패하면 scene similarity 점수를 내지 않고 asset을 되돌린다.

## 6. 자동 측정

자동 측정은 차이를 찾는 도구이며 사람 판정을 대체하지 않는다.

### 6.1 camera·geometry

- MAP/GRID ROI의 긴 구조 edge를 추출해 두 isometric 축의 중앙각을 구한다.
- reference와 candidate 축 차이의 절댓값 평균 `≤3°`, 95 percentile `≤5°`를 목표로 한다.
- world→screen→world round trip은 `≤1 unit`, fixed node 중심은 reference annotation이 아니라 runtime
  expected coordinate에서 `≤2px`다.

### 6.2 scale·density

- plant, substation, pole, hospital의 bounding-box height를 MAP 높이 비율로 비교한다.
- candidate/reference 비율은 주요 landmark `0.85~1.15`, pole `0.80~1.20`을 목표로 한다.
- edge/structure occupancy와 local entropy로 도시 밀도를 비교한다. candidate는 대응 reference의
  `80~120%`, 준검정 `Y<0.05` 공백은 MAP의 `10%` 이하여야 한다.

### 6.3 palette·light

- ROI를 sRGB→CIELAB으로 바꾸고 3D histogram Earth Mover Distance와 luminance percentile을 기록한다.
- median luminance 차이는 정규화 `0.12` 이하, `Y<0.05` shadow clipping은 reference보다 `5%p` 넘게
  많지 않아야 한다.
- cyan energized, amber planned, orange/red outage swatch는 reference semantic mask와 `ΔE00 ≤12`를
  목표로 하되 pattern·contrast gate도 함께 통과해야 한다.

### 6.4 grid readability

- active/planned line과 바로 옆 terrain의 non-text contrast는 `3:1` 이상이다.
- dual conductor는 home zoom에서 분리되고 tower attachment 중심 오차는 `≤3px`다.
- 선택하지 않은 footprint·label이 landmark silhouette의 `10%` 이상을 가리지 않는다.

### 6.5 강 전용 측정

- visible channel의 좌·우 bank edge continuity `≥95%`
- centerline에 화면상 유효한 방향 변화가 최소 3개, 폭 변동계수 `0.10~0.25`
- 인접 segment 폭 jump `≤20%`, bridge-bank 접합 공백/중첩 `≤2px`
- water와 인접 ground의 `ΔE00`는 `12~35`: 구분되지만 neon strip처럼 뜨지 않아야 한다.
- water ROI luminance `P90-P10`은 8-bit 기준 `20~70`으로 표면 반사가 읽혀야 한다.
- 반복 offset autocorrelation peak `<0.90`; 같은 물결·돌 무늬가 연속 세 번 보이면 수치와 무관하게 실패
- authored water/risk mask 밖의 물 표현 `0px`; flood 표현은 active flood authority 안에서만 확장
- neutral/heat/flood water ROI의 pairwise median `ΔE00 ≥8`, ReduceMotion에서도 pattern 차이가 남음

### 6.6 자동점수 환산

| 자동 항목 | 가중치 | 100점 조건 | 0점 조건 |
|---|---:|---|---|
| camera·geometry | 20 | 평균 `≤3°`, P95 `≤5°`, round trip 통과 | 평균 `≥12°` 또는 역변환 실패 |
| object scale | 10 | 모든 family가 목표 비율 안 | 어느 주요 landmark라도 `0.5~1.5` 밖 |
| density·empty space | 15 | density `80~120%`, 준검정 `≤10%` | density `50~150%` 밖 또는 준검정 `≥30%` |
| palette·luminance | 15 | luminance·clipping·semantic `ΔE00` 모두 목표 안 | 차이 `≥0.30`, clipping `≥20%p` 또는 `ΔE00 ≥30` |
| river | 20 | 6.5의 모든 목표 통과 | authority 누출, bank 단절 또는 bridge 접합 실패 |
| grid readability | 10 | contrast·attachment·occlusion 모두 통과 | contrast `<1.5:1` 또는 attachment `>12px` |
| HUD·timeline geometry | 10 | 승인 mockup panel rect `±3%`, 최소 font·marker 통과 | rect `±12%` 밖 또는 timeline 식별 실패 |

100점과 0점 사이 값은 각 metric의 허용 경계에서 선형 보간하고 항목 안에서는 산술평균한다. hard
authority·input failure는 종합점수와 무관하게 FAIL이다. 측정 script는 raw metric과 환산식을 함께
출력해 수동으로 같은 결과를 재계산할 수 있어야 한다.

## 7. 사람 점수표 — 100점

동일 모니터에서 reference와 candidate를 100% 크기로 나란히 보고, 필요하면 ROI blink overlay를 쓴다.

| 항목 | 배점 | 질문 |
|---|---:|---|
| camera·perspective | 15 | 같은 높이·각도·평행 투영의 도시로 즉시 읽히는가? |
| scene density·composition | 15 | 빈 작업면이 아니라 같은 밀도의 도시·산업 회랑인가? |
| river·bank·bridge | 15 | 굽은 수로, 깊이, 반사, 상태와 접합 품질이 같은 수준인가? |
| object scale·silhouette | 10 | plant·pole·substation·facility의 비중과 형태가 비슷한가? |
| material·lighting | 10 | 흑철·콘크리트·토사와 amber 광원의 명암이 같은가? |
| power grid | 10 | cyan/amber 도체, 철탑 연결과 경로가 같은 강도로 읽히는가? |
| HUD·inspector | 10 | 두꺼운 산업 HUD, 정보 위계와 지도 위 overlay 비례가 비슷한가? |
| chapter state variants | 10 | 평상·폭염·범람·겨울이 같은 세계의 상태 변화인가? |
| event timeline | 5 | 독립적인 사건 단계 bar로 즉시 인식되는가? |

각 항목은 `0~4` anchor로 먼저 평가하고 배점으로 환산한다.

- `4`: reference와 같은 시각 체계로 즉시 인식, 작은 차이만 존재
- `3`: 분명히 같은 방향이나 한두 요소의 scale/material 차이가 보임
- `2`: 관련성은 있으나 현재 G.2처럼 구조적 차이가 큼
- `1`: palette나 소재 일부만 비슷함
- `0`: 사실상 다른 제품 화면

평가자는 점수마다 한 줄의 `similar`, `different`, `next action`을 반드시 쓴다.

## 8. 종합 유사도와 판정

```text
MeasuredSimilarity = 자동 camera/scale/density/palette/river/grid 점수의 가중 합
ReviewSimilarity   = 100점 사람 점수표
ReferenceParity    = 0.35 × MeasuredSimilarity + 0.65 × ReviewSimilarity
```

개념 reference는 pixel ground truth가 아니므로 사람 점수 비중을 더 크게 둔다.

| ReferenceParity | 해석 |
|---:|---|
| 90~100 | reference와 거의 같은 시각 체계 |
| 85~89 | 목표에 충분히 근접, 작은 차이만 남음 |
| 75~84 | 관련성은 분명하지만 출시 후보로 부족 |
| 60~74 | 상당히 다른 화면 |
| 0~59 | 레퍼런스와 별개인 화면 |

G.3 visual pass는 다음을 모두 만족해야 한다.

- `ReferenceParity ≥85`
- camera, density, river 각 항목 환산점수 `≥80`
- 개별 comparison pair `≥75`
- asset-level·authority·input·accessibility·build gate 모두 PASS
- unresolved visual P0/P1 `0`
- 세 owner checkpoint 모두 명시 승인

## 9. 차이 보고서

모든 차이는 다음 형식으로 기록한다.

```text
pairId / ROI / criterion
reference expectation
candidate observation
measured delta
review score 0~4
severity: P0/P1/P2/P3
proposed single change
before path / after path
owner status
```

P1 예시는 잘못된 camera, 직선·평면 강, landmark가 절반 크기, 도시의 큰 공백, timeline이 bar로
인식되지 않는 경우다. P2는 한 asset의 light 방향·bank seam·UI 간격처럼 전체 체계를 깨지 않는 차이다.

각 checkpoint는 `reference-manifest.json`, `capture-manifest.json`, `annotations.json`,
`scorecard.json`, `DIFFERENCE_REPORT.md`와 원본 PNG를 같은 증거 폴더에 보존한다.

## 10. 실행 순서

1. G.2 사용자 screenshot을 baseline으로 채점해 현재 차이를 수치화
2. target mockup 3종을 같은 protocol로 비교하고 owner checkpoint 1
3. first-light vertical slice의 asset sheet·runtime frame을 비교하고 checkpoint 2
4. 평상·폭염·범람·겨울 전체 pair를 비교하고 checkpoint 3
5. exact committed tree에서 독립 visual review, full regression, clean package 재검수

1920×1080 이외 화면은 이 프로토콜의 입력이 아니다. 특히 720p 캡처나 검사를 만들지 않는다.
