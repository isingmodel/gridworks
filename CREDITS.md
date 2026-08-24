# Gridworks credits

## Gridworks

게임 설계, C# 코드, 한국어 문구, code-native 2D 표현과 app icon은 Gridworks 저장소의 작업물이다.
현재 R2의 G3 runtime 자산은 프로젝트를 위해 생성·가공한 개별 이미지이며, 제작 provenance는
`game/art/commercial/g3-assets.prompts.md`에 보존한다.

윤서진·강민호·박지현·이도윤의 고정 초상 네 장은
`OpenAI imagegen, user-directed project asset, 2026-08-19` provenance를 가진다. 이는 프로젝트가
지시해 생성한 자산이라는 기록이며 외부 재배포 라이선스 주장이나 공개 라이선스 부여가 아니다.
현재 사용 경계는 [ASSET_MANIFEST.md](ASSET_MANIFEST.md)에 있다.

동결 V2의 code-native 도시·날씨 ambient, 효과 cue와 두 motif는 저장된 외부 음원 없이 고정 PCM16
파형으로 생성한다. 네 인물 초상과 이 음향은 저장소에 보존되지만 현재 R2 runtime에는 연결돼 있지
않다. 현재 R2에는 루트 `assets/`의 reference 이미지, 외부 font·녹음·음악·voice-over를 포함하지
않는다.

## Engine과 runtime

- [Godot Engine 4.7.1 Mono](https://godotengine.org/), Godot Engine contributors
- [.NET 8](https://dotnet.microsoft.com/), .NET Foundation and contributors

각 구성요소의 저작권과 라이선스 고지는 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)에 있다.
Godot의 기본 font와 engine 내부 라이브러리도 Godot upstream 고지의 적용을 받는다.
