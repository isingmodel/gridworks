# Gridworks 개발 실행 안내

## 현재 경계

현재 저장소의 기본 장면은 live R2 `RealtimeSliceMain`이고 G3 자산 57개가 연결돼 있다. 인자 없는
실행은 제품 title을 연다. 저장 파일이 없으면 `새 게임`이 authored `FIRST_LIGHT` briefing부터 누적
8장을 시작하고, exact current `ProductCampaign | FIRST_LIGHT` source의 journal-restorable v1/v2 save가
있으면 `이어하기`만 활성화된다. 모든 장의 stable 상태, story-idle active event·duty,
exact-minute active `EventStory | DecisionWindowStory`와 bounded non-final result→next briefing
save/Continue는 구현됐다. undelivered Core transition·general queued story·initial briefing·완료 저장과
설치 패키지는 아직 없다. 과거 V2의 저장 파일과 내부 macOS 후보를 current R2 기능으로 간주하지 않는다.

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
`새 게임`이 `FIRST_LIGHT`→`LONGEST_NIGHT` 누적 8장을 시작한다. 유효한 journal-restorable 진행 저장이
있으면 새 게임을 차단하고 `이어하기`에서 같은 Core 상태를 paused로 복원한다.

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

`./dev check`는 current root solution, RealtimeChecks의 누적 8장 stable replay와 pending fail-closed,
CommercialChecks, 세 Python 회귀, no-arg 제품 title과 명시적 fixture entry smoke, 같은 save path의 active
`FLOOD_ISOLATION_TEST` create→same-story Continue→`SECOND_HEART` result write→fresh Continue→
`SECOND_SOURCE` briefing write, 직전 exact `FIRST_LIGHT` v1 Continue→current v2 write,
invalid/unsupported-schema/I/O 실패 title 상태와 두 named checkpoint를 묶은 기본 회귀다. 한 화면 상태나
story part를 조사할 때는 더 작은 `checkpoint` 또는 `story` 명령부터 시작한다. 전체 명령 형태는
`./dev help`에서 확인한다.

저장소에 포함된 Godot 대신 다른 Mono executable을 사용할 때의 예:

```sh
GRIDWORKS_GODOT_BIN=/absolute/path/to/Godot ./dev check
```

## 저장과 user-data

current 파일은 `user://gridworks-r2-campaign-save-v1.json` 하나다. 파일명은 유지하지만 current write wire
schema는 v2이며 deterministic story candidate prefix 중 닫은 개수인 required nonnegative 32-bit
`closedStoryCount`를 기록한다. prior
v1은 read-only이고 cursor가 없으므로 projected candidate를 모두 닫은 story-idle로 해석한다. v1 probe와
restore 자체는 원본 bytes를 rewrite하지 않으며, Continue 뒤 정상 종료는 같은 파일의 current v2 write
정책을 따른다.

제품 title의 New Game 또는 Continue로 시작한 product-owned session만 이 파일을 쓴다. 공통 Core 경계는
command 하나 이상, incomplete campaign, pending transition·draft 없음이며 epilogue와 frame debt도 없어야
한다. application 경계는 다음 세 종류뿐이다.

- story-idle: active modal 없음, story flow idle, simulation이 Running 또는 PlayerPaused
- in-chapter active story: pending story queue 없음, `EventStory | DecisionWindowStory`, trigger minute == saved minute,
  같은 blocking story modal과 AutoPaused interaction, `ModalRestore.Simulation`이 Running 또는 PlayerPaused,
  story pause reason 일치
- bounded handoff: exact-minute non-final `ChapterResult`와 이어지는 next `ChapterBriefing`, optional
  same-chapter `DecisionWindowStory` suffix. result는 마지막 completed chapter와 일치하고, next chapter가
  이미 시작됐거나 미래 `ChapterStartMinute`가 남은 typed between-chapter 상태여야 함

이 조건에서 active construction과 active event·duty도 허용한다. save는 current route와 base/realtime source
hash, saved minute, ordered accepted journal, final canonical hash와 v2 cursor를 기록하며 snapshot, modal body,
seed나 raw transition queue를 복제하지 않는다. 정상 종료 때 같은 디렉터리의 private temp를 거쳐
atomic하게 교체한다.

title의 파일 상태 정책은 다음과 같다.

- 저장 파일 없음: `이어하기` disabled, `새 게임` enabled
- exact current `ProductCampaign | FIRST_LIGHT` source의 journal-restorable v1/v2 save:
  `이어하기` enabled, `새 게임` disabled
- 다른 개발 route·형식 손상·지원하지 않는 schema/version·source/hash/replay 불일치·I/O 실패:
  두 action disabled, 원본 bytes 보존

`이어하기`는 exact clock·cash·world·construction·journal/hash를 되살린다. story-idle과 prior v1은
PlayerPaused·Normal·no-modal로 열고, active story v2는 같은 authored modal을 AutoPaused로 연다. handoff의
result를 닫으면 queued next briefing(+decision)을 FIFO로 열고, 긴 장 간격이면 exact next-chapter minute로
한 번 전진한 뒤 연다. pointer, camera, selection과 fractional frame remainder는 복원하지 않는다.

위 경계를 만족하면 누적 8장의 어느 장이든 stable 상태, story-idle active event·duty, exact-minute active
in-chapter story 또는 bounded non-final result→next briefing handoff를 저장할 수 있다. undelivered pending
transition, general queued story suffix, initial briefing, 완료 run/finale/epilogue와 crash journal은 아직
저장하지 않는다. 직전 exact `FIRST_LIGHT` v1 save는 probe/restore 때 원본을 보존하고, Continue 뒤 정상
종료 때 원 route의 current v2로 쓴다. 이후 그 v2도 같은 continuation source로 읽는다. 유효 save의 새
게임 덮어쓰기 확인, 삭제, migration/recovery UI도 없다. 새 게임이 필요하면 앱을 종료한 상태에서
current save를 별도 위치로 백업·이동한다. 명시적 `chapter`, `through`, `fixture` 개발 명령은 product
save를 읽거나 갱신하지 않는다.

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
