# Gridworks 개발 실행 안내

## 현재 경계

현재 저장소의 기본 장면은 live R2 `RealtimeSliceMain`이고 G3 자산 57개가 연결돼 있다. 인자 없는
실행은 제품 title을 연다. 저장 파일이 없으면 `새 게임`이 authored `FIRST_LIGHT` briefing부터 누적
8장을 시작하고, 유효한 product stable 진행 저장이나 직전 exact standalone `FIRST_LIGHT` v1 save가
있으면 `이어하기`만 활성화된다. 모든 장의 stable save/Continue는 구현됐지만 사건·story·result
handoff·완료 저장과 설치 패키지는 아직 없다. 과거 V2의 저장 파일과 내부 macOS 후보를 current R2
기능으로 간주하지 않는다.

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

이 명령은 Godot user argument 없이 기본 장면을 열어 제품 title을 재현한다. 저장 파일이 없으면
`새 게임`이 `FIRST_LIGHT`→`LONGEST_NIGHT` 누적 8장을 시작한다. 유효한 stable 진행 저장이 있으면 새
게임을 차단하고 `이어하기`에서 같은 상태를 paused로 복원한다.

### 누적 8장 경로

```sh
./dev play through LONGEST_NIGHT
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

`through`, `chapter`, `fixture`는 모두 제품 title을 우회하는 명시적 개발 경로다. 같은 누적 8장 route를
쓰는 `./dev play through LONGEST_NIGHT`도 product save를 읽거나 쓰지 않는다. 제품용 새 게임이나 저장
lifecycle은 `./dev play product`에서 검증한다.

## 검증과 단위 진입점

```sh
./dev check
./dev checkpoint A1_NORMAL_READY
./dev checkpoint A1_CONSTRUCTION_DUE_1M
./dev story SWITCH_OFF_TO_PROTECT/result/standard
./dev story manifest
```

`./dev check`는 current root solution, 누적 8장 stable replay를 포함한 두 check project, 세 Python 회귀,
no-arg 제품 title과 명시적 fixture entry smoke, 별도 process의 누적 product mid-construction
save-create→Continue·직전 exact `FIRST_LIGHT` Continue·invalid/unsupported-schema/I/O 실패 title 상태와
두 named checkpoint를 묶은 기본 회귀다. 한 화면 상태나 story part를 조사할 때는 더 작은 `checkpoint`
또는 `story` 명령부터 시작한다. 전체 명령 형태는 `./dev help`에서 확인한다.

저장소에 포함된 Godot 대신 다른 Mono executable을 사용할 때의 예:

```sh
GRIDWORKS_GODOT_BIN=/absolute/path/to/Godot ./dev check
```

## 저장과 user-data

current 파일은 `user://gridworks-r2-campaign-save-v1.json` 하나다. 제품 title의 New Game 또는 Continue로
시작한 product-owned session만 이 파일을 쓴다. Core command가 하나 이상 수락되고 active incomplete
chapter이면서 사건·duty·pending transition·draft·queued/active story·epilogue·frame debt가 없는 stable
진행을 정상 종료하면 같은 디렉터리의 private temp를 거쳐 atomic하게 교체한다. active construction은
허용한다. save는 current route와 base/realtime source hash, saved minute, ordered accepted journal과 final
canonical hash를 기록하며 snapshot이나 seed를 복제하지 않는다.

title의 파일 상태 정책은 다음과 같다.

- 저장 파일 없음: `이어하기` disabled, `새 게임` enabled
- exact current `ProductCampaign` stable save 또는 직전 exact standalone `FIRST_LIGHT` v1 save:
  `이어하기` enabled, `새 게임` disabled
- 다른 개발 route·형식 손상·지원하지 않는 schema/version·source/hash/replay 불일치·I/O 실패:
  두 action disabled, 원본 bytes 보존

`이어하기`는 exact clock·cash·world·construction·journal/hash를 되살린 뒤 player-paused·normal speed·
no-modal 상태로 연다. pointer, camera, selection과 fractional frame remainder는 복원하지 않는다.

누적 8장의 어느 장이든 result와 다음 briefing을 닫아 active chapter·story-idle 상태가 되면 저장할 수
있다. 사건 중·active duty·pending transition, queued/active story·result handoff, 완료 run/finale/epilogue와
crash journal은 아직 저장하지 않는다. 직전 exact `FIRST_LIGHT` save는 migration 없이 원 route로
Continue한다. 유효 save의 새 게임 덮어쓰기 확인, 삭제, migration/recovery UI도 없다. 새 게임이 필요하면
앱을 종료한 상태에서 current save를 별도 위치로 백업·이동한다. 명시적 `chapter`, `through`, `fixture`
개발 명령은 product save를 읽거나 갱신하지 않는다.

Godot user-data에 과거 V2의 `release-campaign-save-v3.json`이나 `settings.json`이 남아 있어도 current R2
권위가 아니다. 빈 user-data를 검사할 때 기존 파일을 삭제하거나 덮어쓰지 말고 별도 폴더로 옮겨
백업한다. 남은 save 범위와 recovery 정책은 [남은 작업](docs/NEXT_TASKS.md)의 후속 단계다.

## 패키지와 공개 배포

저장소에는 과거 `CommercialMain` V2를 위한 내부 패키징 스크립트와 회귀 자료가 남아 있다. 그
스크립트의 성공은 current R2 package나 출시 후보를 만들지 않는다. current R2의 fresh-install 후보,
지원 OS 검증, Developer ID 서명·공증과 공개 배포 승인은 아직 없다.

일반 Debug/Release의 current R2 graph와 `ExportRelease`는 서로 다르다. `ExportRelease`는 동결된 V2
내부 export allowlist이며 current R2 후보를 만들지 않는다. current 개발 경계와 변경 순서는
[개발 구조](docs/ARCHITECTURE.md)가 소유한다.

과거 V2 내부 후보의 범위는 [완료 이력](docs/archive/COMPLETED_HISTORY.md), 앞으로 필요한 단계는
[남은 작업](docs/NEXT_TASKS.md)을 따른다.
