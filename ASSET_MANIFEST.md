# Gridworks 자산 출처와 사용 경계

이 문서는 현재 R2 runtime 자산, 시각 reference와 과거 V2 자산을 구분한다. 재배포 권한을 새로
부여하지 않으며, 프로젝트 저작물과 제3자 고지는 [LICENSE.md](LICENSE.md)와
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)가 소유한다.

## 1. 시각 reference — runtime 미포함

루트 `assets/`의 네 합성 이미지는 사용자가 제공한 프로젝트 시각 reference다. 카메라·밀도·재질·
실루엣·조명·상태 표현을 정하지만 runtime 배경이나 sprite source가 아니다.

| 파일 | 참고 역할 |
|---|---|
| `assets/01-grid-construction.png` | 정상 운전, 건설, cyan 통전과 산업도시 밀도 |
| `assets/02-heatwave-outage.png` | 폭염 대기, 위험과 보호정지 |
| `assets/03-route-comparison.png` | 경로 비교와 정보 위계 |
| `assets/04-plant-siting.png` | 지형·도시·산업의 큰 scale과 깊이 |

이미지의 UI 배치, 거대 송전설비, 발전원 종류와 수치를 그대로 복제하지 않는다.

## 2. 현재 R2 G3 runtime 자산

`game/art/commercial/g3/` 아래 PNG 57개가 live R2에 연결돼 있다. 제작 과정과 prompt provenance는
`game/art/commercial/g3-assets.prompts.md`에 byte 고정된 역사 ledger로 보존한다. 그 ledger 도입부의
“current package 29개”는 당시 G3 생산 checkpoint의 문구이며 지금의 runtime 수량이나 package 상태가
아니다. 현재 수량과 사용 경계는 이 문서가 소유한다.

| 그룹 | 수량 | 현재 사용 |
|---|---:|---|
| `atomic/` | 12 | 병원, 주거·상점, 정수 시설, 산업 소품과 가로등 |
| `grid/` | 8 | 발전소, switchyard, 일반/보강 전신주와 변전소 구조 |
| `objects/` | 6 | 교량, 강둑, 반사와 지형 전환 |
| `river/` | 15 | 물, 제방, 암반, 식생, 범람·폭염 상태 |
| `roads/` | 6 | 도로 방향·교차로·yard |
| `tiles/` | 3 | 지형·수면 base |
| `ui/`, `ui-v2/` | 7 | panel, HUD metric, inspector, tool slot과 버튼 chrome |

지도 50개는 clear·heat·rain·storm의 world draw에, UI 7개는 live R2 theme에 사용된다. 저장소의
결정론 검사는 tracked source set, Godot import와 실제 draw/theme wiring을 확인한다. 이 검사는 사람의
미감·가독성, 자산 권리 판단이나 모든 상태의 production 완성도를 대신하지 않는다.

## 3. code-native 현재 표현

R2는 G3 texture와 함께 다음 code-native 표현을 사용한다.

- 실제 전력 경로와 도체, 선택·초안·공사 overlay
- service area와 forecast/active 위험 pattern
- 한 줄 future-event bar의 marker, cluster와 상세 overlay
- focus, hit target, 접근성 이름과 한국어 text layout

필수 상태는 glow나 색 하나에 의존하지 않고 형태·pattern·icon·문장을 함께 사용해야 한다.

## 4. 현재 R2에서 사용하지 않는 과거 자산

다음은 저장소에 남아 있지만 동결 V2 `CommercialMain`의 역사·회귀 자산이다.

- `game/assets/commercial/portraits/`의 네 인물 초상
- `CommercialAudioLibrary`, `CommercialMapView`, `CommercialTheme.tres`
- V2 package 전용 icon, audio bus와 release manifest 자료

이 파일이 tracked 상태라는 이유로 current R2 runtime이나 앞으로의 package에 자동 포함하지 않는다.
현재 R2에는 승인된 product audio layer가 없다.

## 5. 새 자산 채택 조건

새 runtime 자산은 별도 활성 scope 안에서 다음을 기록하고 검증한다.

- source, 제작 방법, 날짜, 사용 권리와 사용 경계
- 공통 camera·광원·scale
- 투명 edge, pivot, footprint, selection bounds와 conductor anchor
- 필요한 상태 variant 또는 typed overlay 조합
- 실제 scene의 세 zoom, UI scale, hit target와 성능
- [비주얼 제작 명세](docs/product/VISUAL_PRODUCTION_SPEC.md)의 다섯 축과 사람 검토

untracked 후보, 합성 화면에서 잘라낸 sprite, 출처가 불명확한 파일은 runtime 권위가 아니다.
