# Gridworks

`Gridworks`는 계속 흐르는 시간 속에서 전력망을 건설하고 운영하는 싱글 플레이 2D 전략 게임이다.
플레이어는 청류시의 전신주·선로·변전소를 직접 연결하고, 예고된 수요 증가·기상·공사·설비 정지에
대비해 병원과 정수장 같은 필수 시설을 지킨다.

현재 저장소는 **실시간 R2 플레이 빌드와 G3 아이소메트릭 아트 기준선**을 제공한다. 작성된 8장과
finale→세 epilogue card는 누적 native 개발 경로에 연결됐지만 아직 판매 가능한 1.0 게임은 아니다.
제품 title에서 시작하는 전체 여정, R2 저장/재개와 완료 후 선택, 제품용 audio·settings, 출시 패키지와
공식 UX 평가가 남아 있다.

## 30초 현재 상태

| 구분 | 현재 사실 |
|---|---|
| 제품 방향 | turn 방식이 아닌 pause·1×·2×·4× 실시간 전력망 운영 |
| 기본 Godot 장면 | `res://realtime/r2/RealtimeSliceMain.tscn` |
| 인자 없는 실행 | 제품 title; `새 게임`은 `FIRST_LIGHT`로 진입하고 `이어하기`는 저장 부재 이유와 함께 비활성 |
| 게임 아트 | G3 PNG 57개가 R2에 연결됨: 지도 50개, UI 7개 |
| 작성된 콘텐츠 | 8장, 16개 사건, 34개 story part |
| R2 native 구현 | `LONGEST_NIGHT`까지 누적 8장, exact finale 뒤 authored epilogue 3장 |
| 직접 플레이 관찰 | `NORTH_BANK_PROMISE`까지 누적 4장; Keep·명시적 Defer를 각각 fresh process에서 확인 |
| 사건 지평선 | 한 줄 future-event bar(`RealtimeEventRail`)의 compact marker와 hover/선택 상세 정보 |
| product audio·settings | R2 audio layer, 설정 UI와 설정 저장은 아직 없음 |
| 공식 상용 UX 점수 | 없음. 텍스트 계획 형성평가만 `83.4475` |
| 현재 구현 권한 | [현재 작업 범위](docs/ACTIVE_SCOPE.md)가 단일 권위 |

여기서 세 표현은 서로 다르다.

- **작성됨**: 데이터와 story selector가 존재한다.
- **native 구현됨**: R2 화면과 실제 Core 진행 경로가 연결됐다.
- **직접 관찰됨**: fresh process에서 production mouse/keyboard 입력으로 해당 경로를 끝까지 확인했다.

직접 관찰은 headless smoke와 다르지만, 사람 참가자의 사용성·미감·재미 증거는 아니다.

작성 사실과 native 도달성은 별도 증거이며, 현재 명시적 개발 route에서는 8장 누적 경로와 마지막
authored result→city report→medical witness→closing을 플레이할 수 있다. 다만 인자 없는 제품 title의
`새 게임`은 여전히 standalone `FIRST_LIGHT` 한 장 경로다. 이 completion presentation에는 저장 기반
`이어하기`와 완료 후 result/chapter/replay 선택이 없으므로 전체 제품 여정의 완성을 뜻하지 않는다.

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

`./dev play product`는 Godot user argument 없이 제품 title을 열고, `./dev play fixture`는 명시적
`--technical-fixture` 인자로 title을 우회하는 DEBUG 개발 경로다. fixture를 새 게임이나 전체
캠페인으로 평가하지 않는다. 환경 준비, 전체 명령과 현재 저장/패키지 경계는
[실행 안내](INSTALL.md)에 있다.

## 개발 검증

current R2의 기본 자동 회귀 명령은 하나다.

```sh
./dev check
```

이 명령은 current root solution, RealtimeChecks, CommercialChecks, 세 Python 회귀, no-arg 제품 title과
명시적 fixture entry smoke, 두 named checkpoint를 실행한다.

root `Gridworks.sln` 전체의 Release build와 전체 Godot UI harness는 이 기본 명령에 포함되지 않는다.
해당 검사가 필요한 변경은 active scope의 완료 검사에 별도로 명시한다.

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
3. [개발 구조](docs/ARCHITECTURE.md) — current R2의 권위와 변경 경로
4. [남은 작업](docs/NEXT_TASKS.md) — 우선순위가 있는 backlog; 자동 실행 권한은 아님
5. [문서 지도](docs/README.md) — 질문별 상세 문서
6. [완료 이력](docs/archive/COMPLETED_HISTORY.md) — 과거 task의 유일한 요약

새 작업을 시작할 때는 먼저 `docs/ACTIVE_SCOPE.md`에 결과물, 범위 밖 항목과 완료 검사를 적는다.
`docs/NEXT_TASKS.md`의 항목, 과거 scope 또는 준비된 코드가 있다는 사실만으로 구현을 시작하지 않는다.

## 출시와 자산 경계

루트 `assets/`의 네 이미지는 카메라·밀도·재질·실루엣·조명·상태 표현의 시각 참고 자료이며 runtime
배경이 아니다. 현재 R2에는 별도로 제작된 G3 자산 57개가 연결돼 있다. 출처와 사용 경계는
[자산 안내](ASSET_MANIFEST.md)에 기록한다.

현재 R2에는 제품용 audio/settings, save/resume, 서명·공증된 패키지, 지원 OS 검증, 사람 미감·사용성
검토, 한국어·전력설비 전문 검토 또는 공개 출시 승인이 없다. 저장소를 열람할 수 있다는 사실은
자산의 재사용·재배포 허가를 뜻하지 않는다.
