# Commercial map discrete art v1

- 생성일: 2026-08-21
- 생성 도구: OpenAI built-in ImageGen
- 사용 방식: 새 tile/object 생성 후 object별 background-extraction edit. atlas나 whole-map 합성은 사용하지 않음.
- style reference: `assets/01-grid-construction.png`, `assets/02-heatwave-outage.png`,
  `assets/03-route-comparison.png`, `assets/04-plant-siting.png`를 모든 생성 호출에 직접 넣고 낮은 3/4
  사선 시점, 약 30도 하향 orthographic camera, 흑연·그을린 철·낡은 콘크리트, 촘촘한 산업 디테일,
  제한된 황동 작업등을 최대한 가깝게 고정했다. 원본 이미지는 runtime에 포함하지 않음.
- 공통 금지요소: UI, 문구, 숫자, logo, 완성 지도, 전력선, gameplay 상태, 사람, 차량, 흰색/검은색
  matte. object는 실제 transparent alpha, tile은 semantic object와 보이는 격자선을 금지.

## Tile prompts

모든 tile은 정사각형 seamless production texture로 생성했다. 지면 네 종은 각각 `낡은 짙은 아스팔트`,
`다져진 흙과 성긴 풀`, `낡은 산업 콘크리트`, `어두운 쇄석`만 담고 건물·강·도로 표식·시설을 넣지
않았다. `river-water`는 강둑 없는 어두운 청록 수면, `residential-block`은 낮은 사선 시점의 조밀한
한국형 주거 지붕, `hospital-block`은 의료 상징 없는 질서정연한 공공시설 지붕·중정 texture로
생성했다. runtime은 이 파일을 v2 terrain polygon이나 400단위 지면 셀에 개별 반복한다.

| runtime 파일 | SHA-256 |
|---|---|
| `tiles/ground-asphalt-v1.png` | `d4f3b205aeb2e51d785421288b4a1d891ecd040939e48744defc2625d03d4b7d` |
| `tiles/ground-scrub-v1.png` | `07428690c956ef95b23e2528185dc95d3a67b9ebe648fd0fe426cc36c700b72f` |
| `tiles/ground-concrete-v1.png` | `146ee721c0195c0a907c24ae44cc2871e252da9cc9e1766008ca7745ae2ee385` |
| `tiles/ground-gravel-v1.png` | `4df081e67b48ba61007a5db2e0599064fc7799b6a7866df97cd137bcfac64c1a` |
| `tiles/river-water-v1.png` | `cde04fee2c5833d134fe1c4991299a6d9c7fdc185ad6dae886fed74ccf29f941` |
| `tiles/residential-block-v1.png` | `1a2e9e5676bf7c586c931118dc4820867082a7730df41ebfac12fda22f610840` |
| `tiles/hospital-block-v1.png` | `42b8d9f486ade2048e99abdba5ade5e6daf4db8bb8c240bcec52e8edc264911d` |

## Object prompts

각 object는 하나의 완전한 top-down 3/4 orthographic production sprite로 따로 생성했다. 공통 prompt는
`한 오브젝트만 중앙에 배치`, `청류시 산업 전략 게임의 낡은 철·콘크리트·도자기 재료`, `작은 크기에서도
구분되는 silhouette`, `충분한 투명 여백`, `네 모서리 alpha 0`을 요구한다. 종류별 주대상은 발전
접속 설비, 일반 목주, 보강 철주, 교량 보호기초, 소형 배전 변전소, 주거 묶음, 의료원, 정수장,
산업시설이다. background-extraction prompt는 원 오브젝트의 geometry·색·조명·비율은 보존하고 baked
checkerboard와 모든 배경 픽셀만 제거하도록 고정했다. 정수장은 배경분리가 두 번 실패해 폐기하고 같은
종류를 transparent-alpha 조건으로 새로 생성했다. reference 정렬 재생성에서도 baked checkerboard를
그대로 채택하지 않고 각 object를 다시 background-extraction했으며, 정수장과 의료원은 실패 결과를
폐기하고 RGBA 후보가 나올 때까지 별도 재시도했다.

| runtime 파일 | SHA-256 |
|---|---|
| `objects/source-plant-v1.png` | `e79805f2a707b993b2b9df5f3648f2af5337184b85b1ec756464c47cd53152d7` |
| `objects/pole-standard-v1.png` | `4f2c33b12822194ed7487cfc81e5ee106e46ddcbcf2c5eeac1458cedfd04ff34` |
| `objects/pole-reinforced-v1.png` | `83b55e555d7667db70ba05b597d2dd965ba96b59cbf975d87a041be621d808c9` |
| `objects/bridge-foundation-v1.png` | `2bcfa9ee118a546c61f7b4c28c2dd0c260f50894914431e8fbc7f874f7d390b5` |
| `objects/substation-v1.png` | `cf1a98d21edca128aef96e3c8476d5f09e8a3af6cf7e315d68fed97827b7eb81` |
| `objects/facility-residential-v1.png` | `9c1921a9d1621216b5b690715e5385eeea64d0538e05a74e20890d281c402dfc` |
| `objects/facility-hospital-v1.png` | `28d158008e9e2d509c02fe20ed626627cb110184df223a0ff54e50212160911d` |
| `objects/facility-water-v1.png` | `6f3dc13b7c95f11f8e4d9c01786cf3ea1a1a1ac40f8504c128033f44f4e7c013` |
| `objects/facility-industry-v1.png` | `f3371150454cbc630ca33c086c91c4887a887774c94f679b71f91008f74f80a7` |

object 최종 검수는 PNG color type RGBA, 네 모서리 alpha 0/1 이하, 완전 투명 pixel 비율 35% 이상을
요구한다. reference 정렬 재생성 9종은 모두 이 조건을 통과했으며
`commercial-map-discrete-art-contract`가 같은 조건과 위 hash, scene binding, whole-map plate 부재를
반복 검사한다.
