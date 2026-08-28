# Gridworks

`Gridworks`는 계속 흐르는 시간 속에서 전력망을 건설하고 운영하는 싱글 플레이 2D 전략 게임이다.
플레이어는 청류시의 전신주·선로·변전소를 직접 연결하고, 예고된 수요 증가·기상·공사·설비 정지에
대비해 병원과 정수장 같은 필수 시설을 지킨다.

현재 저장소는 **실시간 R2 플레이 빌드와 G3 아이소메트릭 아트 기준선**을 제공한다. 작성된 8장과
finale→세 epilogue card는 누적 native 개발 경로에 연결됐지만 아직 판매 가능한 1.0 게임은 아니다.
제품 title의 `새 게임`은 누적 8장을 시작하고 모든 장의 stable 진행, story-idle active event·duty와
exact initial briefing, exact-minute active in-chapter story, non-final result→next briefing handoff
저장·재개와 full campaign의 exact terminal 완료 저장·재개를 지원한다. completed title에서는 기존
`새 게임`으로 첫 장부터 다시 시작할 수 있다. 진행 중이거나 읽을 수 있는 비호환 저장도 같은 버튼의
확인→원본 sibling backup 뒤 첫 장부터 다시 시작할 수 있다. title과 gameplay가 공유하는 current R2
제품 설정과 strict persistence, code-native 기본 audio는 구현됐고, 출시 패키지에서의 audio coverage·
청감 검증과 공식 UX 평가는 남아 있다. exact 내부 package는 별도 app-owned root의 save/settings 2B1과
default scene의 title·Continue·reset·settings·generated-audio lifecycle seam 2B2 qualification을 통과했다.
chapter briefing·계획 decision story를 닫거나 공사 도구에 진입하면 선택한 속도를 보존한 채 계획 정지하며,
플레이어가 speed·재생 입력으로 명시적으로 재개한다. 결과 화면은 접속 조건의 평가 시각과 실패 terminal의
읽기 전용 경계를 설명한다.
active flood/thermal-override event는 각각 storm/heat world로 표현되고 `Reduce Motion`은 비·폭우
weather phase를 고정한다. 여기까지가 저장소가 소유하는 **current R2 내부 후보 완료선**이다.
실제 hardware·사람·공식 평가·서명·공증·공개 승인은 별도 외부 gate며, 아직 판매 가능한
1.0 게임이라고 주장하지 않는다.

## 30초 현재 상태

| 구분 | 현재 사실 |
|---|---|
| 제품 방향 | turn 방식이 아닌 pause·1×·2×·4× 실시간 전력망 운영 |
| 기본 Godot 장면 | `res://realtime/r2/RealtimeSliceMain.tscn` |
| 인자 없는 실행 | 제품 title; 저장 파일 없음은 즉시 `새 게임`, in-progress는 `이어하기`와 확인형 `새 게임`, current-v3 terminal은 `이어하기`와 즉시 `새 게임` 활성 |
| R2 save/Continue | in-progress는 current v3 write/prior v1·v2 read, terminal은 current v3만 지원; stable/active story/handoff와 full-campaign `Ended` 복원, non-saveable exit의 prior bytes 보존과 readable-save backup/reset 지원 |
| 게임 아트 | G3 PNG 57개가 R2에 연결됨: 지도 50개, UI 7개 |
| 작성된 콘텐츠 | 8장, 16개 사건, 34개 story part |
| R2 native 구현 | `LONGEST_NIGHT`까지 누적 8장, exact finale 뒤 authored epilogue 3장 |
| `FIRST_LIGHT` 초반 안내 | HUD의 1/3→2/3→3/3 경로 안내, 완료 경로에서만 시험 진행 action, 건설 진입·거부·완공의 계획 정지, 수치 경간과 평가 시점·원인별 실패 디브리프 |
| 직접 플레이 관찰 | 고정 LLM sample로 누적 8장·모든 Defer·실패 finale까지 확인; 성공 전용 epilogue는 이 sample에서 관찰하지 않음 |
| 사건 지평선 | 한 줄 future-event bar(`RealtimeEventRail`)의 compact marker와 hover/선택 상세 정보 |
| product settings | title/gameplay 공용 surface, UI 100/125/150/200%, Master/Ambient/SFX 0/25/50/75/100%, windowed/fullscreen, Reduce Motion과 strict atomic persistence 구현 |
| product audio | 22,050Hz mono PCM16 ambient와 live `Breaker/Energize/Outage` cue 생성; Session 의미·bus·Resume 무재생, packaged ambient one-start/SFX-quiet wiring 2B2 완료 |
| macOS 내부 package와 2B | current R2 universal ad-hoc ZIP·strict manifest/verifier, app-owned save/settings 2B1과 packaged lifecycle InputEvent 2B2 완료; 전체 8장 packaged 입력·OS hardware·speaker·사람 UX·공증은 아직 아님 |
| 저장소 구현 backlog | 없음; 실제 device·사람·score-bearing 평가·출시 승인은 외부 gate로 분리 |
| 공식 상용 UX 점수 | 없음. 텍스트 계획 형성평가만 `83.4475` |
| 현재 구현 권한 | [현재 작업 범위](docs/ACTIVE_SCOPE.md)가 단일 권위 |

여기서 세 표현은 서로 다르다.

- **작성됨**: 데이터와 story selector가 존재한다.
- **native 구현됨**: R2 화면과 실제 Core 진행 경로가 연결됐다.
- **직접 관찰됨**: fresh process에서 production mouse/keyboard 입력으로 해당 경로를 끝까지 확인했다.

직접 관찰은 headless smoke와 다르지만, 사람 참가자의 사용성·미감·재미 증거는 아니다.

작성 사실과 native 도달성은 별도 증거다. 제품 `새 게임`과 명시적 8장 개발 route는 모두
`FIRST_LIGHT`→`LONGEST_NIGHT`와 마지막 result→세 epilogue card를 사용한다. product save는 initial,
stable/active story와 bounded 장 전환을 exact journal replay로 복원한다. full campaign terminal은 current
v3만 허용하고 `이어하기`에서 epilogue 재생 없이 같은 `Ended` world를 연다. completed title의 `새 게임`은
기존 canonical bootstrap을 재사용하며 saveable 지점의 정상 종료 전까지 terminal bytes를 보존한다. prior
v1/v2 terminal은 거부한다. in-progress와 읽을 수 있는 invalid/unsupported/source/replay 저장의 `새 게임`은
첫 activation에서 확인만 열고, 두 번째 activation에서 원본 raw bytes를 unique sibling backup으로 만든
뒤 같은 canonical bootstrap을 사용한다. 상세 wire·cursor·title 정책은 [실행 안내](INSTALL.md)가 소유한다.

undelivered pending transition, general queued story suffix와 active final result/epilogue cursor는 의도적인
non-saveable 구간으로 남긴다. 이 구간의 정상 종료는 직전 safe save bytes를 그대로 두며, 다음 saveable
정상 종료만 primary를 갱신한다. transient cursor/schema를 추가하지 않는다.

## 게임 경험

플레이어는 같은 도시와 전력망을 장마다 이어서 사용한다.

```text
도시와 한 줄 사건 지평선을 읽는다
→ 현재 공급 경로와 첫 병목을 찾는다
→ 전신주·선로·변전소 공사를 설계하고 견적을 확인한다
→ 사건 전에 완공될지, 어떤 시설을 지킬지 비교한다
→ 시간을 진행해 공사·과부하·보호정지·복귀를 관찰한다
→ 결과를 확인하고 다음 장에서 같은 망을 보강한다
```

전기는 발전량 총합만으로 공급되지 않는다. 발전 접속점에서 수요처까지 완공된 실제 경로가 있어야
하며, 공유 선로·전신주 접속부·변전소의 연속/비상 한도가 병목이 된다. 공사는 시간이 걸리고, 비상
운전은 허용시간 뒤 보호정지와 냉각·복귀로 이어진다.

장기 제품 경험은 [게임 기획서](docs/product/GAME_DESIGN_KO.md), 화면과 아트 기준은
[비주얼 제작 명세](docs/product/VISUAL_PRODUCTION_SPEC.md)가 설명한다.

## 지금 플레이할 수 있는 경로

먼저 .NET 8 SDK와 Godot 4.7.1 Mono가 필요하다. current R2 개발의 단일 진입점은 저장소 루트의
`./dev`다.

```sh
./dev build
```

| 목적 | 명령 |
|---|---|
| 제품 title에서 시작 | `./dev play product` |
| 개발용 기술 fixture | `./dev play fixture` |
| 첫 장만 플레이 | `./dev play chapter FIRST_LIGHT` |
| 튜토리얼 3장 누적 플레이 | `./dev play through SECOND_SOURCE` |
| 구현된 8장 누적 플레이 | `./dev play through LONGEST_NIGHT` |

예를 들어 현재 구현된 8장 경로는 다음과 같이 실행한다.

```sh
./dev play through LONGEST_NIGHT
```

`./dev play product`만 Godot user argument 없이 제품 title과 product save/settings lifecycle을 연다.
`fixture`, `chapter`, `through` 명령은 title을 우회하는 명시적 개발 경로이며 product save/settings를
읽거나 쓰지 않는다. 설정 surface는 같은 UI를 read-only로 보여 준다.
fixture를 새 게임이나 전체 캠페인으로 평가하지 않는다. 환경 준비, 전체 명령과 현재 저장/패키지
경계는 [실행 안내](INSTALL.md)에 있다.

## 개발 검증

current R2의 기본 자동 회귀 명령은 하나다.

```sh
./dev check
```

macOS에서 clean committed HEAD의 current R2 내부 package identity 후보를 만들고 재검증할 때는 다음 단일
진입점을 사용한다.

```sh
./dev candidate build
./dev candidate verify dist/Gridworks-current-r2-macOS-internal.manifest.json
./dev qualify run dist/Gridworks-current-r2-macOS-internal.manifest.json
./dev qualify verify dist/Gridworks-current-r2-macOS-internal.qualification.json
```

이 후보는 exact archive/tree, universal runtime, PCK의 G3 57개, legal files와 headless 제품 title marker를
검증한다. qualification v2는 그 exact package를 private copy로 고정한 뒤 2B1의 missing/settings/progress/
completed data stage와 2B2의 empty New Game, progress/completed Continue, completed/reset New Game,
settings apply→fresh restore를 default scene의 actual `Viewport.PushInput`으로 재구성한다. 각 exact input 수,
save/settings bytes, generated ambient PCM/Ambient bus one-start와 SFX player quiet wiring, 실제 account
home의 current 두 파일과 package app tree를
canonical record에 결속하며 invalid scenario/root도 title 전에 거부한다.

이 결과는 bounded product lifecycle seam의 2B 증거다. Godot engine `user://` 전체 격리, 전체 8장 packaged
production 입력, OS hardware input, 실제 audio device·speaker, 사람 UX, Developer ID·공증 또는 출시 승인을
주장하지 않는다.

`./dev check`는 current root solution, RealtimeChecks의 누적 8장 stable replay와 pending fail-closed,
CommercialChecks, 세 Python 회귀, no-arg 제품 title과 명시적 fixture entry smoke, 같은 save path의
initial briefing create→non-saveable draft exit의 byte-exact 보존→fresh Continue와 safe write→fresh Continue,
진행 저장의 확인→backup 실패 차단→byte-exact sibling backup→initial write→fresh Continue, 이어지는
`FLOOD_ISOLATION_TEST`→`SECOND_HEART` result→`SECOND_SOURCE` briefing write/Continue, 직전 exact
`FIRST_LIGHT` v1 Continue→current v3 write, 성공 8장 terminal create→fresh Continue→`Ended`·terminal write,
fresh completed title→`새 게임`→initial write→fresh Continue, invalid/unsupported 확인 상태와 I/O 실패
차단, 제품 설정 create→fresh restore와 invalid/unsupported/read/write failure 보존, explicit fixture의
read-only 설정, generated audio의 selector·scene shape/bus·ambient one-start·Continue history 무재생,
전체 Godot UI layout harness와 두 named checkpoint를 실행한다.

root `Gridworks.sln` 전체의 Release build는 이 기본 명령에 포함되지 않는다. Godot 검사는 headless라서
실제 물리 display나 사람 사용성은 주장하지 않으며, native window mode가 검증 대상이면 별도
non-headless 실행을 active scope의 완료 검사에 명시한다.

결함을 재현할 때는 전체 캠페인을 매번 처음부터 돌리지 않는다. 가장 가까운 단위나 named checkpoint를
사용하되, onboarding·누적 장 전환·save/resume·전체 캠페인처럼 시작 경로 자체가 검증 대상일 때만
처음부터 E2E를 실행한다.

작성된 story part 하나만 검사하는 예:

```sh
./dev story SWITCH_OFF_TO_PROTECT/result/standard
./dev story manifest
```

R2 controller·presentation·HUD를 실제로 거치는 두 개발 checkpoint:

```sh
./dev checkpoint A1_NORMAL_READY
./dev checkpoint A1_CONSTRUCTION_DUE_1M
```

selector와 checkpoint의 통과는 해당 콘텐츠의 native 도달성, 전체 캠페인 완결성 또는 사람 UX 품질을
대신하지 않는다. 세부 도구 사용법은 [상용 UX 도구 안내](tools/commercial-ux/README.md)에 있다.

## 문서 읽는 순서

처음 온 사람이나 LLM agent는 아래 순서만 따르면 된다.

1. 이 README — 제품과 현재 구현의 차이
2. [현재 작업 범위](docs/ACTIVE_SCOPE.md) — 지금 변경이 허용됐는지
3. [Agent 작업 안내](docs/AGENT_GUIDE.md) — 시작·검증·commit·review·handoff 절차
4. [문서 지도](docs/README.md) — 질문별 단일 소유 문서
5. 코드를 바꿀 때만 [개발 구조](docs/ARCHITECTURE.md) — current R2의 ownership과 변경 경로

새 작업의 scope 작성 형식, 변경별 최소 검사와 종료 checklist는 `Agent 작업 안내`가 소유한다.
[남은 구현 작업](docs/NEXT_TASKS.md), [외부 출시 gate](docs/RELEASE_GATES.md), 과거 scope 또는 준비된
코드는 그 자체로 구현·평가·배포 권한을 만들지 않는다.

## 출시와 자산 경계

루트 `assets/`의 네 이미지는 카메라·밀도·재질·실루엣·조명·상태 표현의 시각 참고 자료이며 runtime
배경이 아니다. 현재 R2에는 별도로 제작된 G3 자산 57개가 연결돼 있다. 출처와 사용 경계는
[자산 안내](ASSET_MANIFEST.md)에 기록한다.

현재 R2에는 외부 녹음 음원과 상태 전반의 packaged audio·사람 청감 검증, transient cursor/schema와
backup browser/restore/delete UI가 없다. universal macOS ad-hoc 내부 package identity와 app-owned data 2B1,
packaged lifecycle seam 2B2 qualification은 있지만 engine `user://` 전체 격리와 packaged production-input 전체 여정,
Developer ID 서명·공증, 지원 OS 검증,
사람 미감·사용성 검토, 한국어·전력설비 전문 검토 또는 공개 출시 승인이 없다. 저장소를
열람할 수 있다는 사실은 자산의 재사용·재배포 허가를 뜻하지 않는다.
이 미완료 증거·승인의 소유자와 통과 조건은 [외부 출시 gate](docs/RELEASE_GATES.md)에 있다.
