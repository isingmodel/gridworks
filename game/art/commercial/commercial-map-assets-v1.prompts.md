# Commercial map discrete art v1

- 생성일: 2026-08-21
- 생성 도구: OpenAI built-in ImageGen
- 사용 방식: 새 tile/object 생성 후 object별 background-extraction edit. atlas나 whole-map 합성은 사용하지 않음.
- style reference: `assets/01-grid-construction.png`, `assets/02-heatwave-outage.png`,
  `assets/03-route-comparison.png`, `assets/04-plant-siting.png`의 낮은 사선 시점, 낡은 산업 재료,
  청록 통전색과 황동 작업등만 참고. 원본 이미지는 runtime에 포함하지 않음.
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
| `tiles/ground-asphalt-v1.png` | `a066839b7e220ba86019098f95b1db3be416313fdbb97a4a86cb2f535b0b8ca1` |
| `tiles/ground-scrub-v1.png` | `22ab0a3c8ba9420ba05caa30f90c0dc1bb647acef0b6b639ceee44045f7c31a4` |
| `tiles/ground-concrete-v1.png` | `2dc5c1cbea8d82e5e66163e700e80ad448e3624f594c01167be629ee3a588f74` |
| `tiles/ground-gravel-v1.png` | `7e55a542a244725d68b5b9ecd0bf1ef6bcf65d87360f12db00637625e9e83efb` |
| `tiles/river-water-v1.png` | `7016d7130d517e40ecf68fb9e3cf9a8adb15ba6478e26c79d24f8aa3787de0ee` |
| `tiles/residential-block-v1.png` | `17a017f71ff510a8d763621538b1079633928489aaffc6b98a9e982ac5421f27` |
| `tiles/hospital-block-v1.png` | `51104b40dcf8209d8d16b005cf470852dc7ef908485d81cce53dd5c49c9a6730` |

## Object prompts

각 object는 하나의 완전한 top-down 3/4 orthographic production sprite로 따로 생성했다. 공통 prompt는
`한 오브젝트만 중앙에 배치`, `청류시 산업 전략 게임의 낡은 철·콘크리트·도자기 재료`, `작은 크기에서도
구분되는 silhouette`, `충분한 투명 여백`, `네 모서리 alpha 0`을 요구한다. 종류별 주대상은 발전
접속 설비, 일반 목주, 보강 철주, 교량 보호기초, 소형 배전 변전소, 주거 묶음, 의료원, 정수장,
산업시설이다. background-extraction prompt는 원 오브젝트의 geometry·색·조명·비율은 보존하고 baked
checkerboard와 모든 배경 픽셀만 제거하도록 고정했다. 정수장은 배경분리가 두 번 실패해 폐기하고 같은
종류를 transparent-alpha 조건으로 새로 생성했다.

| runtime 파일 | SHA-256 |
|---|---|
| `objects/source-plant-v1.png` | `59b672e990bf5e5d58759a243cd54b1d5718fa6b72d93aa34b6931a77dc44432` |
| `objects/pole-standard-v1.png` | `ac30be64f40f082c50c2723ea52f3e145de9b32976da33ee18fe1c201124a377` |
| `objects/pole-reinforced-v1.png` | `dc5593c7cdea88d3bccce1b9603d4e7754e7892ea43c05f3d2273ba97a986520` |
| `objects/bridge-foundation-v1.png` | `802c249f3bec6060d515f1bce8d4bf24df8dceda618e6aeb4387e7262b243ce8` |
| `objects/substation-v1.png` | `82d8568adb1a3b9308ca6e548d90504e1fb16f758d5ba62d032972f0ea35f853` |
| `objects/facility-residential-v1.png` | `e3fd6efb2f10637b1e2c28f0b2dd3dadd7deaa0cd3bdfeb61a1b543e87781465` |
| `objects/facility-hospital-v1.png` | `84820bdedc749335cd17c6491c4628e0c279c64f9c8ac22a563bf823ed1bd4bb` |
| `objects/facility-water-v1.png` | `ff772d28a19a9eeac8b42c4d923a7a47963c9012b43bec8b58d79046e51955f4` |
| `objects/facility-industry-v1.png` | `d13eab68d94e1878dbd778c767aa421c24d313e92483a63a21551350565ca96e` |

object 최종 검수는 PNG color type RGBA, 네 모서리 alpha 0/1 이하, 완전 투명 pixel 비율 35% 이상을
요구한다. 실제 결과는 43.3%~81.3%이며 `commercial-map-discrete-art-contract`가 같은 조건과 위 hash,
scene binding, whole-map plate 부재를 반복 검사한다.
