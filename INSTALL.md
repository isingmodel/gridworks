# Gridworks 개발 실행 안내

## 현재 경계

현재 저장소의 기본 장면은 live R2 `RealtimeSliceMain`이고 G3 자산 57개가 연결돼 있다. 그러나 인자
없는 실행은 개발용 기술 fixture이며, current R2에는 제품용 title, save/resume 또는 설치 패키지가
없다. 과거 V2의 저장 파일과 내부 macOS 후보를 현재 R2 기능으로 간주하지 않는다.

## 요구 환경

- .NET SDK 8.0.129 (`global.json`이 고정)
- Godot 4.7.1 Mono
- macOS 저장소 환경에서는 `.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot` 사용 가능

다른 위치의 Godot을 사용할 때는 아래 명령의 실행 파일 경로만 바꾼다.

## 빌드

저장소 루트에서 실행한다.

```sh
dotnet restore game/Gridworks.Game.csproj
dotnet build game/Gridworks.Game.csproj -c Debug
```

## 플레이 경로

### 구현된 누적 4장

```sh
./.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot \
  --path game -- --release-through=NORTH_BANK_PROMISE
```

### 직접 플레이가 끝난 튜토리얼 3장

```sh
./.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot \
  --path game -- --release-through=SECOND_SOURCE
```

### 첫 장만

```sh
./.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot \
  --path game -- --release-chapter=FIRST_LIGHT
```

### 기술 fixture

```sh
./.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot --path game
```

마지막 명령은 기본 scene wiring을 사용하지만 제품용 새 게임은 아니다. 전체 8장이나 저장 기능을
검증할 때 사용하지 않는다.

## 저장과 user-data

current R2 진행 상태는 프로세스를 종료한 뒤 이어 할 수 없다. 따라서 fresh-process 검증은 매번 새
경로로 시작한다. Godot user-data에 과거 V2의 `release-campaign-save-v3.json`이나 `settings.json`이
남아 있을 수 있지만 current R2의 저장 권위가 아니다.

빈 user-data를 검사할 때 기존 파일을 삭제하거나 덮어쓰지 말고 별도 폴더로 옮겨 백업한다. R2
save/resume과 migration 정책은 [남은 작업](docs/NEXT_TASKS.md)의 별도 단계다.

## 패키지와 공개 배포

저장소에는 과거 `CommercialMain` V2를 위한 내부 패키징 스크립트와 회귀 자료가 남아 있다. 그
스크립트의 성공은 current R2 package나 출시 후보를 만들지 않는다. current R2의 fresh-install 후보,
지원 OS 검증, Developer ID 서명·공증과 공개 배포 승인은 아직 없다.

과거 V2 내부 후보의 범위는 [완료 이력](docs/archive/COMPLETED_HISTORY.md), 앞으로 필요한 단계는
[남은 작업](docs/NEXT_TASKS.md)을 따른다.
