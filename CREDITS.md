# Gridworks credits

## Gridworks

게임 설계, C# 코드, 한국어 문구, code-native 2D 표현과 app icon은 Gridworks 저장소의 작업물이다.
runtime의 ambient와 상태 cue는 저장된 외부 음원 없이 코드가 고정된 PCM16 파형으로 생성한다.

청류시 runtime 배경 plate는 `assets/`의 네 이미지를 style·mood reference로 삼아 OpenAI built-in
ImageGen으로 새로 생성했다. 원본 concept 자체, UI·문구·전력망 상태는 plate에 포함하지 않는다.
현재 제품 빌드에는 `assets/`의 콘셉트 원본, 외부 raster sprite, 외부 font, 음악과 voice-over를
포함하지 않는다.

최종 runtime별 출처와 권리 경계는 [ASSET_MANIFEST.md](ASSET_MANIFEST.md)에 고정했다.

## Engine과 runtime

- [Godot Engine 4.7.1 Mono](https://godotengine.org/), Godot Engine contributors
- [.NET 8](https://dotnet.microsoft.com/), .NET Foundation and contributors

각 구성요소의 저작권과 라이선스 고지는 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)에 있다.
Godot의 기본 font와 engine 내부 라이브러리도 Godot upstream 고지의 적용을 받는다.
