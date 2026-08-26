# Gridworks 개발 실행 안내

## 현재 경계

현재 저장소의 기본 장면은 live R2 `RealtimeSliceMain`이고 G3 자산 57개가 연결돼 있다. 인자 없는
실행은 제품 title을 열며, `새 게임`은 authored `FIRST_LIGHT` briefing으로 진입한다. current R2 저장
권위가 없으므로 `이어하기`는 이유와 함께 비활성이고, save/resume과 설치 패키지도 아직 없다. 과거
V2의 저장 파일과 내부 macOS 후보를 현재 R2 기능으로 간주하지 않는다.

## 요구 환경

- .NET SDK 8.0.129 (`global.json`이 고정)
- Godot 4.7.1 Mono
- macOS 저장소 환경에서는 `.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot` 사용 가능

다른 위치의 Godot을 사용할 때는 `GRIDWORKS_GODOT_BIN`에 실행 파일의 절대 경로를 지정한다.

## 빌드

저장소 루트에서 실행한다.

```sh
./dev build
```

`./dev build`는 current R2의 Core, Game, RealtimeChecks와 CommercialChecks만 포함한 루트
`Gridworks.sln`을 Debug로 빌드한다. restore는 `dotnet build`가 필요할 때 함께 수행한다.

## 플레이 경로

### 제품 title

```sh
./dev play product
```

이 명령은 Godot user argument 없이 기본 장면을 열어 제품 title과 `새 게임` 경로를 재현한다. 현재
`새 게임`은 standalone `FIRST_LIGHT` 한 장만 시작하며, 저장 기반 `이어하기`는 제공하지 않는다.

### 누적 4장 경로

```sh
./dev play through NORTH_BANK_PROMISE
```

### 튜토리얼 3장 누적 경로

```sh
./dev play through SECOND_SOURCE
```

### 첫 장만

```sh
./dev play chapter FIRST_LIGHT
```

### 기술 fixture

```sh
./dev play fixture
```

마지막 명령은 명시적 `--technical-fixture` 인자로 제품 title을 우회하는 DEBUG 개발 경로다. 제품용
새 게임, 전체 8장이나 저장 기능을 검증할 때 사용하지 않는다.

## 검증과 단위 진입점

```sh
./dev check
./dev checkpoint A1_NORMAL_READY
./dev checkpoint A1_CONSTRUCTION_DUE_1M
./dev story NORTH_BANK_PROMISE/result/keep
./dev story manifest
```

`./dev check`는 current root solution, 두 check project, 세 Python 회귀, no-arg 제품 title과 명시적
fixture entry smoke, 두 named checkpoint를 묶은 기본 회귀다. 한 화면 상태나 story part를 조사할
때는 더 작은 `checkpoint` 또는 `story` 명령부터 시작한다. 전체 명령 형태는 `./dev help`에서 확인한다.

저장소에 포함된 Godot 대신 다른 Mono executable을 사용할 때의 예:

```sh
GRIDWORKS_GODOT_BIN=/absolute/path/to/Godot ./dev check
```

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

일반 Debug/Release의 current R2 graph와 `ExportRelease`는 서로 다르다. `ExportRelease`는 동결된 V2
내부 export allowlist이며 current R2 후보를 만들지 않는다. current 개발 경계와 변경 순서는
[개발 구조](docs/ARCHITECTURE.md)가 소유한다.

과거 V2 내부 후보의 범위는 [완료 이력](docs/archive/COMPLETED_HISTORY.md), 앞으로 필요한 단계는
[남은 작업](docs/NEXT_TASKS.md)을 따른다.
