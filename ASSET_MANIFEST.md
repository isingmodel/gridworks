# Gridworks runtime asset manifest

이 manifest는 `Gridworks 0.1.0` macOS 내부 후보의 최종 runtime 표현 자산 경계를 기록한다.

| 자산 | 형태와 출처 | 권리·라이선스 경계 |
|---|---|---|
| 청류시 도시 배경 plate | `game/art/commercial-city-plate-v1.png`; 저장소의 네 concept을 style·mood reference로 사용해 OpenAI built-in ImageGen으로 새로 생성하고, 권위 시설로 오인할 의료·정수 landmark를 제거. prompt와 금지요소는 인접 `.prompt.md`에 고정 | Gridworks 프로젝트용 생성 출력. 공개 라이선스 없음. 내부 후보에만 포함. SHA-256 `151a498dc4e6f6284c045a430f1cf3a90873b9db7ca944a9fcec4490a522846c` |
| 실제 망·시설·상태 pattern·사건 timeline·네 고정 인물 초상 | `CommercialMapView.cs`, `CommercialEventTimeline.cs`, `CommercialPortrait.cs`가 그리는 저장소 자체 code-native 2D 표현 | Gridworks 자체 저작물. 공개 라이선스 없음. 내부 후보에만 포함 |
| 도시 환경음·날씨 layer·발주·완공·통전·차단·경고·결과 cue·두 motif | `ReleaseAudio.cs`가 실행 중 생성하는 결정적 mono PCM16 파형. 외부 음원 sample 없음 | Gridworks 자체 저작물. 공개 라이선스 없음. 내부 후보에만 포함 |
| app icon | `game/icon.svg`, 저장소 자체 vector | Gridworks 자체 저작물. 공개 라이선스 없음. 내부 후보에만 포함 |
| UI 글꼴 | 별도 font 파일을 포함하지 않고 Godot/macOS system fallback 사용 | Godot와 OS의 각 라이선스 적용. Godot 고지는 동봉 |
| Godot Engine 4.7.1 Mono | 공식 Universal export template | MIT 및 upstream 제3자 고지. `licenses/GODOT-4.7.1-COPYRIGHT.txt` 동봉 |
| .NET runtime 8.0.29 | Godot Mono export가 포함하는 runtime | MIT 및 runtime 제3자 고지. 버전 고정 원문 두 파일 동봉 |

`assets/`의 네 콘셉트 PNG 자체, 외부 raster sprite, 외부 font, 음악, 음성, prototype scene과 v1
data fixture는 최종 runtime 후보에 포함하지 않는다. 실제 전력선·시설·위험·선택·timeline 상태는
생성 plate에 굽지 않고 v2 권위에서 그린다. 자세한 법적 경계는 `LICENSE.md`, `CREDITS.md`,
`THIRD_PARTY_NOTICES.md`가 소유한다.
