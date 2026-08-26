# Gridworks 개발 실행 안내

## 현재 경계

현재 저장소의 기본 장면은 live R2 `RealtimeSliceMain`이고 G3 자산 57개가 연결돼 있다. 인자 없는
실행은 제품 title을 연다. 저장 파일이 없으면 `새 게임`이 authored `FIRST_LIGHT` briefing부터 누적
8장을 시작한다. exact current `ProductCampaign | FIRST_LIGHT` source의 지원되는 in-progress v1/v2/v3 또는
current-v3 terminal save가 있으면 `이어하기`가 활성화된다. terminal title은 `새 게임`도 함께 제공한다.
모든 장의 stable 상태, story-idle active event·duty,
zero-command exact initial briefing, exact-minute active `EventStory | DecisionWindowStory`와 bounded
non-final result→next briefing, full `ProductCampaign`의 exact terminal 완료 save/Continue는 구현됐다.
non-saveable 정상 종료의 prior-save 보존과 readable save의 확인·backup·reset도 구현됐다. current R2
전용 universal macOS ad-hoc 내부 package identity 후보와 strict verifier는 있다. exact package의
app-owned save/settings 2B1과 default-scene lifecycle InputEvent 2B2 qualification도 완료됐다. 다만 engine
user-data 전체 격리, 전체 8장 packaged production 입력, 실제 audio device·speaker, Developer ID·공증 또는
출시 후보 qualification은 아직 없다. 이들은 [외부 출시 gate](docs/RELEASE_GATES.md)로 분리한다.
제품 title과
gameplay는 current R2 설정 surface를 공유하고, window mode, UI 배율,
Master/Ambient/SFX volume·mute와 Reduce Motion을 별도 strict 파일에 저장한다. 과거 V2의 저장·설정
파일과 내부 macOS 후보를 current R2 기능으로 간주하지 않는다. `RealtimeAudio`는 별도 음원 파일 없이
22,050Hz mono PCM16 ambient와 `Breaker/Energize/Outage` SFX를 생성해 기존 Ambient/SFX bus를 사용한다.

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
있으면 `이어하기`에서 같은 Core 상태를 paused로 복원하거나, 확인형 `새 게임`으로 원본을 백업한 뒤
처음부터 시작할 수 있다. current v3의 exact
terminal 완료 저장이면 `이어하기`가 같은 완료 world/outcome을 `Ended` read-only 화면으로 복원하고,
`새 게임`은 canonical 8장을 첫 briefing부터 다시 시작한다. 선택만으로 terminal bytes를 바꾸지 않으며
saveable 지점의 정상 종료가 same slot을 새 진행으로 교체한다.

title의 `설정`은 session을 만들지 않고 열리며, gameplay의 `설정`은 running을 임시 pause한 뒤 닫을 때
정확한 이전 상태와 opener focus를 복구한다. 이미 player-paused이면 그대로 유지한다. 설정은 저장이
성공한 뒤에만 window, UI, audio bus와 Reduce Motion runtime에 적용된다.

ambient는 title boot에서 한 번 시작해 New Game·Continue·settings 왕복에서 다시 시작하지 않는다.
live operation의 cue는 `RealtimeSession`이 `Outage > Energize > Breaker` 우선순위로 최대 하나만 고르고,
bootstrap과 save replay history는 SFX를 요청하지 않는다.

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
쓰는 `./dev play through LONGEST_NIGHT`도 product save/settings를 읽거나 쓰지 않는다. 이 경로의 공용
설정 surface는 read-only다. 제품용 새 게임, 저장과 설정 lifecycle은 `./dev play product`에서 검증한다.

## 검증과 단위 진입점

```sh
./dev check
./dev checkpoint A1_NORMAL_READY
./dev checkpoint A1_CONSTRUCTION_DUE_1M
./dev story SWITCH_OFF_TO_PROTECT/result/standard
./dev story manifest
```

`./dev check`는 current root solution, RealtimeChecks의 누적 8장 stable replay와 pending fail-closed,
CommercialChecks, 세 Python 회귀, no-arg 제품 title과 명시적 fixture entry smoke, 같은 save path의
initial briefing create→non-saveable draft exit의 byte-exact 보존→fresh Continue와 safe write,
진행 저장의 확인→backup 실패 차단→byte-exact sibling backup→initial write→fresh Continue,
`FLOOD_ISOLATION_TEST`→`SECOND_HEART` result→`SECOND_SOURCE` briefing write/Continue, 직전 exact
`FIRST_LIGHT` v1 Continue→current v3 write, 성공 8장 terminal create→fresh Continue→`Ended`·terminal write,
fresh completed title→`새 게임`→initial write→fresh Continue, invalid/unsupported reset 확인과 I/O 실패
차단, 제품 설정 create→fresh restore, invalid/unsupported/read/write failure의 원본·runtime 보존,
explicit fixture의 read-only 설정, 전체 Godot UI layout harness와 두 named checkpoint를 묶은 기본
회귀다. 이 연쇄는 generated PCM shape와 bus, ambient one-start, accepted order와 복합 completion/emergency
cue, fresh Continue의 history 무재생도 확인한다. 한 화면
상태나 story part를 조사할 때는 더 작은 `checkpoint` 또는 `story` 명령부터 시작한다. 전체 명령 형태는
`./dev help`에서 확인한다.

기본 Godot 회귀는 headless라서 audio stream·routing·play request만 확인하며 실제 speaker 출력, 음질,
loop seam이나 사람 사용성을 주장하지 않는다. window
mode 자체가 scope의 완료 조건이면 격리 settings path를 쓴 별도 non-headless smoke로 확인한다.

저장소에 포함된 Godot 대신 다른 Mono executable을 사용할 때의 예:

```sh
GRIDWORKS_GODOT_BIN=/absolute/path/to/Godot ./dev check
```

## 저장과 user-data

current R2의 서로 독립된 primary file authority는 두 개다.

- campaign 진행: `user://gridworks-r2-campaign-save-v1.json`
- 제품 설정: `user://realtime-settings-v1.json`

campaign 파일명은 유지하지만 current write wire schema는 v3이며 first briefing을 포함한 deterministic
story candidate prefix 중 닫은 개수인 required
nonnegative 32-bit `closedStoryCount`를 기록한다. prior v2는 read-only이며 raw cursor를 보존하고 Restore
결과만 checked `+1`로 initial-inclusive 의미에 맞춘다. prior v1도 read-only이고 cursor가 없으므로 projected
candidate를 모두 닫은 story-idle로 해석한다. prior probe와 restore 자체는 원본 bytes를 rewrite하지 않으며,
Continue 뒤 정상 종료는 같은 파일의 current v3 write 정책을 따른다.

제품 title의 New Game 또는 Continue로 시작한 product-owned session만 이 파일을 쓴다. 진행 경계는
incomplete campaign, pending transition·draft 없음이며 epilogue와 retained frame debt도 없어야 한다.
accepted command 하나 이상이 원칙이고, 예외는 first chapter의 exact `ChapterStartMinute`에서
event/duty/completion/active construction 없이 initial batch를 drain한 zero-command 상태뿐이다.
application 경계는 다음 다섯 종류뿐이다.

- exact initial: authored first briefing active `c0` 또는 그 modal을 닫은 story-idle `c1`; 다른 cursor,
  queued suffix, initial minute 이후 zero-command는 거부
- story-idle: active modal 없음, story flow idle, simulation이 Running 또는 PlayerPaused
- in-chapter active story: pending story queue 없음, `EventStory | DecisionWindowStory`, trigger minute == saved minute,
  같은 blocking story modal과 AutoPaused interaction, `ModalRestore.Simulation`이 Running 또는 PlayerPaused,
  story pause reason 일치
- bounded handoff: exact-minute non-final `ChapterResult`와 이어지는 next `ChapterBriefing`, optional
  same-chapter `DecisionWindowStory` suffix. result는 마지막 completed chapter와 일치하고, next chapter가
  이미 시작됐거나 미래 `ChapterStartMinute`가 남은 typed between-chapter 상태여야 함
- terminal completion: canonical full `ProductCampaign`, current v3, pending/draft/frame debt 없음, 모든
  chapter story가 닫힌 `Ended`·World·no-modal. 성공 final은 세 epilogue card가 모두 닫혀야 하고 실패
  final은 epilogue가 시작되지 않아야 함

command-bearing 경계에서는 active construction과 active event·duty도 허용하되 exact initial에는 허용하지
않는다. save는 current route와 base/realtime source hash, saved minute, ordered accepted journal, final
canonical hash와 v3 cursor를 기록하며 snapshot, modal body, seed나 raw transition queue를 복제하지 않는다.
정상 종료 때 같은 디렉터리의 private temp를 거쳐 atomic하게 교체한다.

title의 파일 상태 정책은 다음과 같다.

- 저장 파일 없음: `이어하기` disabled, `새 게임` enabled
- exact current `ProductCampaign | FIRST_LIGHT` source의 journal-restorable in-progress v1/v2/v3 save:
  `이어하기` enabled, 확인형 `새 게임` enabled
- current v3 full `ProductCampaign` exact terminal completion: 완료 문구와 함께 `이어하기` enabled,
  `새 게임` enabled; crafted prior v1/v2 completion은 invalid로 차단
- 다른 개발 route·형식 손상·지원하지 않는 schema/version·source/hash/replay 불일치처럼 raw bytes를
  읽을 수 있는 저장: `이어하기` disabled, 확인형 `새 게임` enabled
- I/O 실패: 두 action disabled, 원본 보존

확인형 `새 게임`의 첫 activation은 title 문구와 focus만 확인 상태로 바꾸며 session, write ownership,
primary bytes와 유효한 `이어하기`를 그대로 둔다. 두 번째 activation은
`<save>.reset-<32자리 GUID>.bak` sibling에 원본 raw bytes를 temp+flush+non-overwrite move로 보존한 뒤에만
canonical `ProductCampaign`을 시작한다. backup을 읽거나 쓸 수 없으면 확인 title에 남고 primary와 기존
continuation을 바꾸지 않는다. completed save와 저장 파일 없음의 `새 게임`은 확인 없이 즉시 시작한다.
backup browser/restore/delete UI는 제공하지 않는다.

`이어하기`는 exact clock·cash·world·construction·journal/hash를 되살린다. story-idle과 prior v1은
PlayerPaused·Normal·no-modal로 열고, supported active v2/v3 story는 같은 authored modal을 AutoPaused로
연다. active initial도 같은 authored `FIRST_LIGHT` briefing으로 복원된다. handoff의 result를 닫으면 queued
next briefing(+decision)을 FIFO로 열고, 긴 장 간격이면 exact next-chapter minute로 한 번 전진한 뒤 연다.
terminal completion은 epilogue/story transition을 재생하지 않고 `Ended`·no-modal·no-frame-debt world를
연다. terminal title의 `새 게임`은 completed journal을 분기하지 않고 fresh `ProductCampaign`을 만들며,
저장 가능한 지점에서 정상 종료할 때만 기존 slot을 current v3 진행으로 교체한다. pointer, camera,
selection과 fractional frame remainder는 복원하지 않는다.

위 경계를 만족하면 exact initial briefing과 누적 8장의 stable 상태, story-idle active event·duty,
exact-minute active in-chapter story, bounded non-final result→next briefing handoff 또는 full campaign의
exact terminal 완료를 저장할 수 있다. terminal 진행 중인 final result와 city report/medical witness/
closing은 저장하지 않는다. undelivered pending transition, general queued story suffix와 crash journal도
저장하지 않는다. 이 non-saveable 상태의 정상 tree exit는 직전 primary bytes를 건드리지 않고, 이후
saveable 상태의 정상 exit만 current v3로 갱신한다. prior v1/v2 save는 probe/restore 때 원본을 보존하고,
Continue 뒤 정상 종료 때 원 route의 current v3로 쓴다. 이후 그 v3도 같은 continuation source로 읽는다.
유효 save의 새 게임 덮어쓰기 확인과 raw backup은 지원하지만 migration, backup restore/delete/browser
UI는 없다. 명시적 `chapter`, `through`, `fixture` 개발 명령은 product save를 읽거나 갱신하지 않는다.

제품 설정의 exact schema는 `gridworks.realtime-settings.v1`이며 `windowMode`, `uiScalePercent`,
`masterVolumePercent`, `ambientVolumePercent`, `sfxVolumePercent`, `reduceMotion`을 strict하게 요구한다. unknown,
duplicate, missing field와 지원하지 않는 값은 거부한다. 파일이 없으면 기본값
`windowed/100/100/100/100/Reduce Motion off`를 사용하고 파일을 만들지 않는다. malformed·unsupported·read
failure는 기본값과 보이는 오류를 사용하되 원본 bytes를 덮어쓰지 않는다. Apply는 같은 디렉터리의
고정 private temp에 쓰고 flush한 뒤 atomic replace하며, write failure면 이전 파일·runtime·control 값을
유지한다. 인자 없는 product boot만 이 파일을 load/write하고 explicit 개발 경로는 override 경로가
있어도 읽거나 쓰지 않는다.

Godot user-data에 과거 V2의 `release-campaign-save-v3.json`이나 `settings.json`이 남아 있어도 current R2
권위가 아니다. 빈 user-data를 검사할 때 기존 파일을 삭제하거나 덮어쓰지 말고 별도 폴더로 옮겨
백업한다. 남은 transient cursor와 backup 관리 UI는 현재 제품 범위 밖이다.

## current R2 macOS 내부 package identity와 2B qualification

macOS의 clean committed HEAD에서 다음 명령을 사용한다. build는 Godot 4.7.1 Mono, .NET SDK 8.0.129와
.NET runtime 8.0.29를 고정 확인하고 candidate를 만든 뒤 같은 strict verifier까지 실행한다.

```sh
./dev candidate build
./dev candidate verify dist/Gridworks-current-r2-macOS-internal.manifest.json
./dev qualify run dist/Gridworks-current-r2-macOS-internal.manifest.json
./dev qualify verify dist/Gridworks-current-r2-macOS-internal.qualification.json
```

출력은 `.gitignore` 대상인 다음 sibling set이다.

- `dist/Gridworks-current-r2-macOS-internal.zip`
- `dist/Gridworks-current-r2-macOS-internal.manifest.json`
- `dist/Gridworks-current-r2-macOS-internal.qualification.json`

단일 권위 `tools/r2_candidate.py`는 archive hash/size와 안전한 ZIP/tree closure, universal arm64+x86_64
launcher와 각 runtime architecture, plist의 `com.gridworks.game`·`0.2.0`·macOS 14.0, ad-hoc signature,
managed assemblies, current R2 PCK의 G3 `.png.import` 57개와 exact `.ctex` backing 57개, license files를
재구성한다. 별도 임시 설치 위치에서 user argument 없이 headless 실행해
`REALTIME_R2_PRODUCT_TITLE_READY`도 확인한다.

`./dev qualify run`은 manifest/archive를 private read-only copy로 고정·재검증하고 exact-empty 임시
Gridworks-owned root를 사용한다. source actual-scene smoke가 만든 settings, initial save와 terminal save를
동일 package의 user-argument 없는 fresh process가 각각 loaded, restorable, completed로 분류해야 한다.
이어 7개 fixed scenario가 exact default scene의 disabled Continue/New Game, progress/completed Continue,
completed/reset New Game, settings apply→fresh restore를 실제 `Viewport.PushInput`으로 조작한다. record v2는
시나리오별 exact pointer/key input 수, before/after save/settings bytes, reset의 한 normalized raw backup과
generated ambient PCM/Ambient bus one-start와 SFX player quiet/null/no-live-cue wiring을 기존 2B1
identity에 결속한다. invalid scenario/root는 title marker 전에
exit 1로 닫히고, qualification env가 없을 때는 lifecycle marker·입력 없이 기존 `user://`를 유지한다.
실제 account home의 current save/settings와 추출한 app tree도 실행 전후 동일해야 한다. `verify`는 새
임시 root에서 모든 stage를 재실행해 canonical record와 byte-level로 비교한다.

2A title smoke는 앱 설치 위치만 임시화하고 2B도 app-owned 두 fixed file만 별도 root로 보낸다. Godot
engine `user://` 전체를 비우거나 격리하지 않는다. 따라서 결과는 authored 8장의 action-by-action packaged
production 입력, OS hardware input, 실제 window/display, audio device·speaker·청감, 사람/native UX,
evaluation readiness, 지원 OS 일반화, Developer ID·공증 또는 공개 배포 승인이 아니다.

일반 Debug/Release graph와 `ExportRelease`는 서로 다르다. `ExportRelease`는
`GridworksCurrentR2Export=true`와 `GridworksLegacyV2Export=true` 중 정확히 하나를 요구하며 missing/both는
fail-closed한다. current candidate 명령은 current selector를 직접 설정한다. frozen V2 내부 경로는 legacy
selector만 쓰며 current R2 후보로 해석하지 않는다. 상세 graph와 claim 경계는
[개발 구조](docs/ARCHITECTURE.md), 남은 제품 마감·평가·배포 gate는
[외부 출시 gate](docs/RELEASE_GATES.md)를 따른다. 현재 [repository 구현 backlog](docs/NEXT_TASKS.md)는
비어 있다.
