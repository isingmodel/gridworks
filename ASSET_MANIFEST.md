# Gridworks runtime asset manifest

이 manifest는 `Gridworks 0.1.0` macOS 내부 후보의 최종 runtime 표현 자산 경계를 기록한다.

| 자산 | 형태와 출처 | 권리·라이선스 경계 |
|---|---|---|
| 청류시 지형 tile 7종 | `game/art/commercial/tiles/`의 지면 4종·강·주거 block·의료원 block PNG. 네 concept을 모든 생성 호출의 직접 style/camera/material reference로 사용해 낮은 3/4 사선·흑철·황동 산업도시 언어를 맞췄고, 각 파일 prompt·SHA-256은 [`commercial-map-assets-v1.prompts.md`](game/art/commercial/commercial-map-assets-v1.prompts.md)에 고정 | OpenAI built-in ImageGen의 Gridworks 프로젝트용 생성 출력. 공개 라이선스 없음. 내부 후보에만 포함 |
| 망 설비·시설 object 9종 | `game/art/commercial/objects/`의 발전 접속 설비·일반/보강 격자 철탑·교량 기초·변전소·주거/의료/정수/산업 transparent PNG. 같은 reference angle로 각 object를 별도 생성·background-extraction했고 파일별 prompt·SHA-256과 실제 alpha 검수는 같은 [생성 기록](game/art/commercial/commercial-map-assets-v1.prompts.md)에 고정 | OpenAI built-in ImageGen의 Gridworks 프로젝트용 생성 출력. 공개 라이선스 없음. 내부 후보에만 포함 |
| 실제 망·상태 pattern·사건 timeline·네 고정 인물 초상 | `CommercialMapView.cs`, `CommercialEventTimeline.cs`, `CommercialPortrait.cs`가 v2 좌표와 typed state에서 그리는 저장소 자체 code-native 2D 표현 | Gridworks 자체 저작물. 공개 라이선스 없음. 내부 후보에만 포함 |
| 도시 환경음·날씨 layer·발주·완공·통전·차단·경고·결과 cue·두 motif | `ReleaseAudio.cs`가 실행 중 생성하는 결정적 mono PCM16 파형. 외부 음원 sample 없음 | Gridworks 자체 저작물. 공개 라이선스 없음. 내부 후보에만 포함 |
| app icon | `game/icon.svg`, 저장소 자체 vector | Gridworks 자체 저작물. 공개 라이선스 없음. 내부 후보에만 포함 |
| UI 글꼴 | 별도 font 파일을 포함하지 않고 Godot/macOS system fallback 사용 | Godot와 OS의 각 라이선스 적용. Godot 고지는 동봉 |
| Godot Engine 4.7.1 Mono | 공식 Universal export template | MIT 및 upstream 제3자 고지. `licenses/GODOT-4.7.1-COPYRIGHT.txt` 동봉 |
| .NET runtime 8.0.29 | Godot Mono export가 포함하는 runtime | MIT 및 runtime 제3자 고지. 버전 고정 원문 두 파일 동봉 |

`assets/`의 네 콘셉트 PNG 자체, 외부 raster sprite, 외부 font, 음악, 음성, prototype scene과 v1
data fixture는 최종 runtime 후보에 포함하지 않는다. whole-map plate도 포함하지 않는다. 개별 tile과
object의 배치, 실제 전력선·위험·선택·timeline 상태는 v2 권위에서 그린다. 자세한 법적 경계는 `LICENSE.md`, `CREDITS.md`,
`THIRD_PARTY_NOTICES.md`가 소유한다.
