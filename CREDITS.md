# Gridworks credits

## Gridworks

게임 설계, C# 코드, 한국어 문구, code-native 2D 표현과 app icon은 Gridworks 저장소의 작업물이다.
runtime의 ambient와 상태 cue는 저장된 외부 음원 없이 코드가 고정된 PCM16 파형으로 생성한다.

청류시 runtime 지형 tile 7종과 망 설비·시설 object 9종은 `assets/`의 네 이미지를 각 생성 호출의
style·camera·material reference로 직접 사용해 OpenAI built-in ImageGen으로 각각 생성했다. 낮은 3/4
사선 시점과 흑철·낡은 콘크리트·황동 조명 언어를 공유하지만 whole-map 배경판이나 atlas로 합치지
않으며, object는 background-extraction을 거친 실제 transparent PNG로 분리했다. 원본 concept 자체,
UI·문구·전력망 상태는 생성
파일에 포함하지 않는다. 현재 제품 빌드에는 `assets/`의 콘셉트 원본, 외부 raster sprite, 외부 font,
음악과 voice-over를 포함하지 않는다.

최종 runtime별 출처와 권리 경계는 [ASSET_MANIFEST.md](ASSET_MANIFEST.md)에 고정했다.

## Engine과 runtime

- [Godot Engine 4.7.1 Mono](https://godotengine.org/), Godot Engine contributors
- [.NET 8](https://dotnet.microsoft.com/), .NET Foundation and contributors

각 구성요소의 저작권과 라이선스 고지는 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)에 있다.
Godot의 기본 font와 engine 내부 라이브러리도 Godot upstream 고지의 적용을 받는다.
