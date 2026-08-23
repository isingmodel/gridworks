# Gridworks 자산 manifest와 현재 시각 기준 경계

이 manifest는 상용 v2 내부 후보가 직접 사용하는 시각·음향 자산의 출처와 경계를 기록하고, 현재
`ASSET_STYLE_REALTIME_GAME` 목표의 reference와 runtime 자산을 구분한다.
재배포 권한이나 공개 라이선스를 새로 부여하지 않으며, Gridworks 자체 저작물의 법적 상태는
[LICENSE.md](LICENSE.md)가 소유한다. 엔진과 runtime 고지는
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)가 소유한다.

## 현재 visual reference — runtime 미포함

루트 `assets/` 네 PNG는 새 목표의 카메라·밀도·재질·실루엣·조명·상태 언어를 고정하는
**visual reference authority**다. 합성 화면 전체이므로 runtime texture·sprite·UI source가 아니며,
개별 요소의 재사용 권리나 production provenance를 제공하지 않는다.

| 경로 | 역할 | SHA-256 |
|---|---|---|
| `assets/01-grid-construction.png` | 정상 운전·건설·통전 reference | `23c9acec1b8026ebcb8eebf329eb6b94201179f8952451aa27a71ab38b7ebedc` |
| `assets/02-heatwave-outage.png` | 폭염·위험·보호정지 reference | `47d4e53b9d9bad74b6afce6311acce023dc3f9642ffd0ea16609d67b6960a630` |
| `assets/03-route-comparison.png` | 경로 비교·정보 위계 reference | `f471ac24cbab24d9b1aff89953595d70fc3ccaf4e8c08c442651125ab3c65828` |
| `assets/04-plant-siting.png` | 지형·도시·산업 scale reference | `10908370f3a2d8403e62ce2a97e39b7ba5c43d8eb0e32073f03e5b3521c01092` |

## A1-G3 bounded realtime runtime art — 완료·비기본 R2 한정

현재의 runtime art authority는 local `main`의 `cf5da56` G3 tree
`75b27b02580de2c60c50666497d9807ed0ff8b27`에서 provenance와 함께 고정한 **35 PNG**다.
정확한 파일별 SHA-256은 `game/art/commercial/g3/a1-g3-allowlist.sha256`가 소유하고,
`tools/commercial-ux/test-g3-a1-asset-allowlist.py`가 source bytes, ledger blob, 대응 `.import`,
unlisted G3 PNG 0개를 결정론적으로 확인한다. `g3-assets.prompts.md`는 역사적 provenance 메모다.

이 자산은 `RealtimePlaceholderMap`의 비기본 live R2 draw layer에만 적용했다. 일반 runtime art 채택,
V2/default/package authority, 재배포 권한, native 사람 미감 검토 또는 공식 UX 점수를 주장하지 않는다.
`game/assets/realtime/`처럼 별도 로컬 후보는 위 allowlist와 무관하며 채택되지 않는다.

## 인물 초상

네 파일의 provenance는 모두 `OpenAI imagegen, user-directed project asset, 2026-08-19`다.
프로젝트가 지시한 고정 인물 초상으로 생성했으며 외부에서 가져온 재배포 라이선스 자산이라고
주장하지 않는다. 원본 PNG는 384×384 RGBA이고 runtime에서 작은 이야기 카드 초상으로만 사용한다.
아래 SHA-256은 Godot import 전 저장소 source PNG bytes를 가리킨다.

| runtime 경로 | 인물·역할 | SHA-256 |
|---|---|---|
| `game/assets/commercial/portraits/yoon_seojin.png` | 운영센터장 윤서진 | `1d44019497a55c606bd6e2bfde1f4651ff791296293aa5e1e743e9ac232cf230` |
| `game/assets/commercial/portraits/kang_minho.png` | 계통운영관 강민호 | `9001df0fad2161b224e3f16064d99e71efab0212dc8e87337c1ae43e12095e4b` |
| `game/assets/commercial/portraits/park_jihyeon.png` | 의료원 시설책임자 박지현 | `bb98b78da14205783b1ce0064de25e007816df57e2050d438a2fb2d328552190` |
| `game/assets/commercial/portraits/lee_doyoon.png` | 재난대응관 이도윤 | `131bb439074446c2cfdf2de5ae6ef2fc0852f38d72098bb99b4e6b871926621f` |

## 자체 제작 code-native 표현

| source | runtime 결과 | provenance | source SHA-256 |
|---|---|---|---|
| `game/CommercialAudioLibrary.cs` | 도시 환경음, 맑음·폭염·비·폭풍 날씨 층, 발주·완공·통전·보호정지·경고·결과 cue, 첫 점등·마지막 우회 motif | Gridworks repository-authored deterministic PCM16 synthesis, 2026-08-19 | `cb1f55c3203aae97af0c19b382b5d780f0417ac6facb11216cc7ae343e045538` |
| `game/CommercialMapView.cs` | 도시 지형·건물·강·전력망·날씨·상태 pattern | Gridworks repository-authored Godot drawing code | build manifest가 exact assembly hash를 기록 |
| `game/CommercialTheme.tres` | 한국어 system font fallback, 패널·버튼·focus·scroll 시각 체계 | Gridworks repository-authored Godot theme; 동결 v1 theme와 시각 회귀가 없도록 byte-identical하게 분리 | `186f65de4455f8a6bdf6edc5b1c1a49d3de66964387fad82f5e429b5fac9a3e4` |
| `game/icon.svg` | macOS app icon | Gridworks repository-authored SVG | `59eef19061f84aa7e3ba897bd3319a35c067f0e74b49ffd55861bbe11297fb90` |
| `game/default_bus_layout.tres` | Master·Ambient·SFX bus | Gridworks repository-authored Godot resource | `529670dc7786d5343d1d4e6199d472cad4d98fa776802bbeefe9687cecff60f1` |

생성 음향은 실행할 때 코드가 고정된 파형을 만들며 저장된 외부 녹음·음악 파일을 포함하지 않는다.
필수 상태는 화면 문장·pattern과 함께 전달하고 소리만으로 규칙을 전달하지 않는다.

## 포함하지 않는 자료

- 루트 `assets/`의 네 이미지는 현재 visual reference authority지만 runtime package에는 포함하지 않는다.
- 프로젝트가 별도로 추가한 외부 font·녹음·음악·음성·stock sprite를 포함하지 않는다. Godot의
  기본 font와 engine 내부 자산은 upstream 고지를 따른다.
- prototype·v1 fixture와 scene은 상용 v2 runtime package에 포함하지 않는다.
