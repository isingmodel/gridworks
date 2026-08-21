# Gridworks

`Gridworks`는 이미 마을·병원·공장이 자리 잡은 지역에서 발전소, 변전소와 선로를 건설하고
운영하는 싱글 플레이 2D 전력망 전략 게임이다. 플레이어는 필수 안전 기준을 지키면서도 비용과
공사시간을 감당할 수 있는 망을 만든다.

배전 변전소의 서비스 권역은 접속 가능 범위일 뿐 전기를 만들지 않는다. 발전소에서 수요처까지
실제로 이어진 경로와 충분한 용량이 있어야 공급이 성립한다. 값싼 공유 경로는 사고에 취약하고,
완전한 이중화는 다음 공사를 할 돈을 줄인다. 예고된 위기는 이전 선택을 시험하고 결과 보고서는
어떤 설비가 멈췄고 어떤 경로가 사람들의 전력을 지켰는지 설명한다.

## 현재 상태

[상용 2D 게임 구현](docs/scopes/COMMERCIAL_2D_IMPLEMENTATION.md)의 단계 B부터 G.1까지는 완료됐다.
현재 활성 구현 단계는 없다. 2026-08-21 소유자 실행 검토에서 실제 화면이 `assets/`의 산업 도시·
설비 규모·청록 통전망·호박색 계획선 방향과 멀고 사건 timeline bar가 없다는 출시 차단 피드백을
받았으며, 규칙·campaign·save를 바꾸지 않고 표현 구조와 사건 흐름을 바로잡았다.
사용자는 보이는 격자를 없앤 자유 배치, 선로 도체·변전소 주기기·전신주
접속부의 연속·비상 열 한계와 상용 재기획서 전체 구현을 승인했다. 새 제품은 별도 v2
world·campaign·Core와 기본 장면에서 그 범위를 순서대로 완성했다. 단계 H의 외부 검증·공개 후보는
미승인이며 Developer ID·공증·소유자 배포 결정 없이 열지 않는다. 기존 `ReleaseMain`과 33×21
후보는 기술 회귀 기준선이며 새 규칙의 실행 권위가 아니다.

단계 B의 명시 실행 장면은 고정소수점 자유 좌표, 원형 점유영역, 수면·건물·위험구역, 교차 비접속
선로, 초안 전신주 이동과 세 단계 카메라를 구현했다. CommercialChecks 7개 묶음 238 assertions,
Game Debug·Release build와 1280×720·UI 125% native 자유 배치 흐름을 통과했고 독립 검토에서
P0/P1이 없었다. 이 수치는 완료 당시의 역사 증거이며 1280×720을 현재 지원한다는 뜻은 아니다.

완료된 단계 C는 최종 `release-world-v2`의 초기 분기·합류망과 선로·변전소·전신주 접속부의
연속·비상 한계를 열었다. 모든 발전원·단순 경로를 누적 사용량과 고정 총순서로 비교하고, 의무별
비상 권한, 다음 국면 보호정지, 한 국면 냉각·복귀와 첫 병목을 typed 결과로 반환한다.
CommercialChecks 12개 묶음 283 assertions, Game Debug·Release build, 자유 배치 회귀와 1280×720·UI
125% 열 projection native smoke가 통과했다. 독립 검토의 P1 3건은 의무 임의 제외, 비열 endpoint
사용불가, 임의 경로 수 상한을 고쳤고 열린 P0/P1은 없다. 단계 D는 `첫 불빛` prelude와 4장 완료
seed의 `누구의 여유인가`를 한 상용 핵심 경로에 통합했다.

완료된 단계 D는 최대 세 결정 경계, 안전 의무·도시 약속·운영 기록, authored deadline, 최근 완공
공사 fresh-replay 복구와 save v3를 같은 Core runner에 연결했다. 결과 화면은 실제 시설·발전원·경로·
비상 운전·보호정지·약속을 말하고, authored prelude 결과와 사건 story, 환경음·발주·완공·통전·
보호정지 cue를 기본 `CommercialMain`에서 표현한다. CommercialChecks 17개 묶음 682 assertions,
Game Debug·Release build, 핵심·자유 배치·열 native smoke와 동결 회귀가 통과했다. 독립 검토의 P1
4건을 모두 수정해 열린 P0/P1은 없고 `CommercialSliceHumanStatus = NOT_COLLECTED`다.

완료된 단계 E는 최종 `release-campaign-v2.json`에 `첫 불빛`, `두 번째 심장`, `두 번째 전원`,
`북안의 약속`을 열고 같은 지도·현금·망·결과를 장 사이에 유지한다. 변전소 서비스 권역, 두 위험
회랑, 두 번째 발전원, 첫 도시 약속을 실제 공사로 검증하며 4장까지는 연속 한계만 허용한다. 예약된
후속 시설은 점유영역만 보이고 장 경계에서 통전되며, save v3 fresh replay·최근 공사·장 재시작과
비호환 저장 보존 뒤 쓰기 가능한 새 게임을 공통 UI에서 제공한다. CommercialChecks 19개 묶음 1,330
assertions, Game Debug·Release build, 1920×1080·UI 125% actual-input 캠페인과 자유 배치·열 native
smoke 및 동결 회귀가 통과했다. 독립 검토의 P1 4건을 모두 수정해 열린 P0/P1은 없다. 단계 F는 같은
campaign 권위와 runner를 정확히 여덟 임무·에필로그로 확장한다.

완료된 단계 F는 같은 campaign 권위와 runner를 `누구의 여유인가`, `물이 닿기 전에`, `꺼야 지킬 수
있다`, `가장 긴 밤`까지 확장했다. 임무 5에서 연속·비상 열여유와 다음 보호정지를 열고 6~8장은 같은
규칙을 범람·기한·계획정지·장간 열 reset과 재조합한다. 각 본편 장의 두 유효 원형, failure→장 재시작,
대체 원형의 미래 완주, 여덟 사실 결과와 에필로그, 완료 저장 재개와 장 시작 선택을 고정했다.
CommercialChecks 20개 묶음 1,805 assertions, 동결 회귀, Game Debug·Release build, 세 fresh
1920×1080 actual-input process와 자유 배치·열·단계 E 회귀가 통과했다. 독립 검토의 P1 3건을 모두
수정해 열린 P0/P1은 없고 `FullCampaignHumanStatus = NOT_COLLECTED`다. 단계 G는 이 완성된 핵심 흐름의
최종 시청각·접근성·설정·macOS 내부 package 경계만 마감한다.

단계 G 구현 후보는 1920×1080에서 장·경계·현금·필수 공급 header, 선택 수요의 전체 경로·최소
열여유·시설 상태, 색 외 pattern/icon, 네 고정 인물 초상과 장별 도시 표현을 통합했다. strict
settings v3 migration, ReduceMotion, 원자 campaign/settings 저장, 확인 shell, 생성형 환경음·날씨·
상태 cue·두 motif를 기본 장면에 연결했다. CommercialChecks 21개 묶음 1,828 assertions, Game
Debug·Release build, 자유 배치·열·세 fresh 캠페인 process와 1920×1080 UI 100%·125% actual-input
presentation smoke가 통과했다. clean source `997f675`의 macOS 내부 ZIP은 Universal 2·ad-hoc
서명, 도시 plate 포함·concept 원본 제외와 archive 격리 검사를 통과했으며 release record는
[변경 기록](CHANGELOG.md)이 소유한다.
candidate 독립 검토의 P1 5건은 파괴 행동 확인, release record, 도움말, audio cue 순서와 네 초상을
수정했다. exact tree `f38a337` 재검토는 **P0 0 / P1 0 / open 0**이며 전체 동결·상용 검사와
Debug·Release rebuild도 최종 통과했다. 완료된 G.1은 concept-aligned 도시 plate, 산업형
frame·inspector, 더 큰 시설 실루엣과 권위 chapter window/phase/실제 promise result에서 만든 읽기
전용 사건 timeline을 추가했다. 첫 exact 검토의 공간 landmark·promise 결과 P1 2건을 수정했고
exact tree `d1e7f9a` 재검토는 **P0 0 / P1 0 / open 0**이다. 수정 화면의 소유자 재확인은 대기 중이며
전체 소유자 플레이 상태를 대신하지 않는다. 1280×720은 지원하지 않는다.

직전 기준선의 `ReleaseMain`은 프롤로그 세 임무와 본편 다섯 장, 한국어 화면, 2D 표현·사운드,
접근성·종료 UX와 macOS 내부 후보까지 구현했다. 사용자 요청으로 수행한
[공식 cold LLM 관찰 1회](playtests/release-2d/OFFICIAL_RELEASE_REBUILD_LLM_OBSERVATION_01.md)는
첫 일곱 임무 뒤 마지막 장에서 막혔고, 후속 후보는 실제 경로 병목 오진과 숨은 작업 버튼·도구·접속
한도 문제를 고쳤다. 이 증거는 v2 상용 게임의 검증으로 합산하지 않는다.
사용자는 2026-08-17 전체 내부 후보를 직접 플레이한 뒤 표현, 망 구조, 이야기, 실제 콘텐츠와 지도
밀도의 근본적인 재작업을 요청했고, 출시 가능한 게임을 끝까지 완성하는 목표를 승인했다.
후속 내부 후보도 상용 2D 게임으로는 선택·리듬·이야기 회수가 부족하다는 판단에 따라
[상용 2D 게임 재기획서](docs/product/COMMERCIAL_2D_GAME_DESIGN_PLAN_KO.md)를 채택했다. 이 제품
기준은 보이는 격자 없는 자유 배치와 선로·변압기·전신주 접속부의 국면별 열 한계, 실제 상태를
기억하는 여덟 임무를 새 기반으로 삼는다. 정확한 구현 경계와 종료 증거는 완료 구현 계약이 소유한다.

현재 기본 실행 장면은 새 `CommercialMain`이다. 보이는 격자 없는 지도에서 자유 좌표로 전신주·
변전소·선로를 건설하고 여덟 임무에서 작성된 국면의 사용량·연속·비상 한계와 보호정지를 비교한다. 현재
제품의 최소 지원 해상도는 **1920×1080**이며 UI 100%·125%를 목표로 한다. 1280×720은 지원하지
않는다. `ReleaseMain`의 33×21 여덟 임무 후보는 명시 scene 동결 회귀로만 남는다.
[화면·입력 증거](playtests/release-2d/LAYOUT_EVIDENCE.md)는 직전 후보의 네 화면 배치와 저장소 밖
키보드 focus 흐름, 후속 후보의 1280×720·UI 125% 작업 영역·접속 한도·수면 거부 확인을 구분해
보존한다. 이 기록 역시 현재 상용 제품의 해상도 지원 근거가 아니다.
이전 세 장 내부 후보는 규칙 회귀용으로만 보존한다.

현재 저장소에는 예방정비와 고정 폭염 결산까지 이어지는 제품 흐름, Title·Pause, 한 슬롯
저장·재개, 장 재시작과 기본 화면 설정이 있으며 두 개의 완료된 검증용 2D 구현도 보존한다.

- [첫 점등 통합](docs/scopes/FIRST_LIGHT.md): 변전소 초안을 직접 놓고 별도로 완공한 뒤 지지물과
  선로를 건설해 마을을 켜고 첫 매출을 결산한다.
- [두 번째 심장](docs/scopes/SECOND_HEART.md): 같은 기본 실행 흐름에서 병원 주·예비 회선을 직접
  건설하고 전기 단일회선 제거, 공간사건, 내부전원과 현금을 결산한다.
- [공장 수요와 발전소 용량](docs/scopes/FACTORY_CAPACITY.md): 공장 증설 뒤 두 부지 중 하나에
  가스발전소를 직접 건설·접속하고 고정 급전과 세 수요처의 공급을 결산한다.
- [예고된 폭염과 예방정비](docs/scopes/HEATWAVE_MAINTENANCE.md): 같은 기본 실행 흐름에 읽기 전용
  예고, 예방정비 선택과 고정 폭염 사건을 추가한다.
- [캠페인 골격·저장·기본 설정](docs/scopes/CAMPAIGN_SAVE_SETTINGS.md): 세 장 경계와 carry-over,
  Title·Pause, 안전 경계 저장·재개, 장 재시작과 최소 화면 설정을 제공한 내부 후보 기록이다.
- [세 장 캠페인 콘텐츠 고정](docs/scopes/CAMPAIGN_CONTENT.md): 세 장의 브리핑·목표를 현재 화면에
  연결하고, 다음 장 reference 흐름에 필요한 진입현금을 검사해 복구 불가능한 진행을 막는다.
- [2D 표현·사운드·패키징](docs/scopes/RELEASE_2D.md): 공통 2D 화면 언어, 최소 사운드, 음량 설정과
  저장소 밖에서도 실행되는 macOS 내부 테스트 ZIP을 제공한다.

- [강변 병원 회랑](docs/scopes/SCOPE_0B_PLAYABLE.md): 고정된 마을·병원 시나리오에서 서비스 권역,
  경로, 전기 사고와 공간 공통원인, 병원 내부전원과 현금 정산을 검증한다.
- [수동 선로 건설](docs/scopes/SCOPE_1_INTERACTION.md): 고정 source·target 사이에 지지물을 직접
  놓고 거리 제한, 발주, 공사 중 무전압과 원자 완공을 검증한다. 별도 장면으로 실행한다.

[2D 완성 로드맵](docs/ROADMAP_2D.md)은 현재 재구축 순서,
[체크리스트](docs/ROADMAP_2D_CHECKLIST.md)는 단계 상태와 종료 증거를 소유한다. 과거 완료 범위는
새 출시판 기능을 구현했다는 뜻이 아니며, 활성 상용 2D 계약에 없는 구현을 승인하지 않는다.

과거 카드 실험, 화면 검증과 시행착오는 [개발 이력 요약](docs/DEVELOPMENT_HISTORY.md)에 압축했다.
옛 내부 후보에 대한 소유자 전체 플레이 피드백은 2026-08-17 수집했다
(`LegacyOwnerPlayReviewStatus = COLLECTED`). 이 검토에서 출시 차단 수준의 표현·망 모델·콘텐츠
문제가 확인됐다. 새 출시판 후보의 소유자 전체 플레이는 아직 수집하지 않았고
(`ReleaseOwnerPlayReviewStatus = NOT_COLLECTED`), 외부 formative test도 수집하지 않았다
(`ExternalHumanValidationStatus = NOT_COLLECTED`). LLM 관찰은 특정 build에서 특정 과제를 수행한
증거일 뿐 사람 사용성·재미·밸런스나 성공률을 증명하지 않는다. 옛 내부 후보에서는 사용자 승인으로
동일 prompt의
[공식 cold LLM 관찰 2회](playtests/release-2d/OFFICIAL_LLM_OBSERVATIONS_SUMMARY.md)를 수행했고, 두
참가자 모두 도움 없이 당시 native 캠페인 성공 종료와 핵심 규칙 설명을 남겼다. 재구축한 직전
후보에서는 별도 [공식 관찰 1회](playtests/release-2d/OFFICIAL_RELEASE_REBUILD_LLM_OBSERVATION_01.md)를
수행했고 참가자는 도움·재시작 없이 첫 일곱 임무를 통과했지만 마지막 임무에서 막혔다. 세 기록은
서로 다른 후보를 합산하지 않는 작은 비인간 관찰이며 사람 검증, 성공률이나 aggregate gate가 아니다.

## 1.0 목표

보이는 격자 없는 한 도시 지도와 전력망이 이어지는 여덟 임무를 목표로 한다.

1. 프롤로그 `첫 불빛 · 두 번째 심장 · 두 번째 전원`: 자유 배치, 회랑 독립성과 연속 한계를 익힌다.
2. `북안의 약속`: 서비스권역과 미래 분기 공간을 선택한다.
3. `누구의 여유인가`: 도시 약속과 비상 열여유·다음 국면 보호정지를 비교한다.
4. `물이 닿기 전에`: 공사 기한 안에 범람 밖 회랑을 만든다.
5. `꺼야 지킬 수 있다`: 계획정지 중 남은 경로의 열여유로 필수시설을 지킨다.
6. `가장 긴 밤`: 최대수요, 보호정지와 범람을 앞선 망으로 통과한다.

최소 출시판에는 22.9 kV급 계획 모델, 분기·합류, 고정소수점 자유 배치, 점유영역·지형,
연속·비상 열 한계와 국면 상태, 결정론적 공급·우회, 전문적인 한국어 UX, 실제 망을 기억하는
이야기, 저장·설정과 설치 가능한 2D 빌드가 포함된다. 상세 범위와 현실성의 상한은
[완료 구현 계약](docs/scopes/COMMERCIAL_2D_IMPLEMENTATION.md)이 소유한다.

## 저장소 구조

```text
assets/                 비권위 콘셉트 이미지
data/                   두 prototype과 완료된 제품 단계의 machine-readable fixture
docs/
  product/              제품 비전, 오브젝트와 시각 언어
  scopes/               완료 prototype과 제품 단계의 구현 기준
  development/          조건부 검증 도구
  future/               1.0 이후 격리 후보
game/                   Godot .NET 장면과 화면 adapter
src/Gridworks.Core/     Godot을 참조하지 않는 C# 규칙
tools/                  독립 자동검사 executable
playtests/              동결된 실행 입력과 로컬 증거 위치
```

문서의 읽는 순서와 질문별 소유자는 [문서 안내](docs/README.md)가 관리한다. 오브젝트가 현재 가능한지,
제품 목표인지, 조건부 후보인지는 [오브젝트 카탈로그](docs/product/OBJECT_CATALOG.md)에서 확인한다.

## 개발 원칙

- 한 번에 로드맵 단계 하나만 연다. 다음 단계의 interface, schema field와 placeholder UI를 미리
  만들지 않는다.
- 실행 숫자는 해당 단계의 machine-readable fixture 한 곳만 소유한다.
- 권위 규칙은 Godot을 참조하지 않는 순수 C#에 두고, Godot은 명령을 보내고 반환 상태를 그린다.
- 현재 두 prototype의 특수 가정을 범용 제품 모델로 확장하지 않는다.
- 제품의 권위 상태·기본 실행·검증 진입점은 하나씩 유지하되, 책임이 다른 규칙·화면·검사 파일은
  작게 나눈다.
- schema, 보존식, 상태전이, build, crash와 save 손상처럼 기계적인 사실만 강한 PASS/FAIL로
  판정한다.
- 이해·혼란·재미는 관찰로 기록한다. 숫자식 사람 통과율이나 목표 선택률을 만들지 않는다.
- LLM 플레이는 자동검사로 답할 수 없는 상호작용 질문에 한 번만 사용하고, 추가 반복은 사용자 별도
  지시에만 허용한다. 어느 경우도 사람 또는 출시 증거로 세지 않는다.
- 큰 개발단위가 끝나면 자동증거와 문서를 검토하고, 다음 단계를 열기 전에 독립 검토를 마친다.

파라미터 분류와 정적 분석 도구의 개방 조건은
[Static Balance Lab](docs/development/BALANCING_STATIC_SIM.md)이 소유한다. LLM이나 정책 agent가 목표
점수에 맞춰 숫자를 무제한 조정하는 방식은 사용하지 않는다.

## 개발 도구

[`global.json`](global.json)은 .NET SDK `8.0.129`를 고정하고 roll-forward를 끈다. Godot binary는 Git에
포함하지 않는다. 현재 검증한 도구는 `Godot 4.7.1.stable.mono.official.a13da4feb`이며 다음 공식
archive를 사용했다.

- URL: `https://github.com/godotengine/godot/releases/download/4.7.1-stable/Godot_v4.7.1-stable_mono_macos.universal.zip`
- SHA-256: `92cac516baa8ddc7756eeaa38a6d007778a968bfbf188db7c5d6e6ec21c5d52c`
- 로컬 binary 경로: `.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot`

`.tools/`는 로컬 설치 경로이며 새 checkout에서는 별도로 준비해야 한다.

## 현재 확인 명령

먼저 .NET 의존성을 복원한 뒤 다음을 실행한다.

```sh
dotnet restore src/Gridworks.Core/Gridworks.Core.csproj
dotnet restore game/Gridworks.Game.csproj
dotnet restore tools/Gridworks.Checks/Gridworks.Checks.csproj
dotnet restore tools/Gridworks.Scope1Checks/Gridworks.Scope1Checks.csproj
dotnet restore tools/Gridworks.ProductChecks/Gridworks.ProductChecks.csproj
dotnet restore tools/Gridworks.ReleaseChecks/Gridworks.ReleaseChecks.csproj
dotnet restore tools/Gridworks.CommercialChecks/Gridworks.CommercialChecks.csproj

ruby playtests/scope-0a/verify_scope0a.rb
ruby playtests/scope-0a-r2/verify_scope0a_r2.rb
ruby playtests/scope-0b/verify_contract.rb
ruby playtests/scope-1/verify_contract.rb
dotnet run --project tools/Gridworks.Checks/Gridworks.Checks.csproj -c Release
dotnet run --project tools/Gridworks.Scope1Checks/Gridworks.Scope1Checks.csproj -c Release -- data/scope-1-v1.json
dotnet run --project tools/Gridworks.ProductChecks/Gridworks.ProductChecks.csproj -c Release -- data/product-campaign-v1.json
dotnet run --project tools/Gridworks.ReleaseChecks/Gridworks.ReleaseChecks.csproj -c Release -- data/release-world-v1.json
dotnet run --project tools/Gridworks.CommercialChecks/Gridworks.CommercialChecks.csproj -c Release
dotnet build game/Gridworks.Game.csproj -c Debug -t:Rebuild
```

Godot 기본 실행은 `game/project.godot`의 `CommercialMain.tscn`이며 기본 viewport는
1920×1080이다. 완료된 회귀 장면은 `--scene res://ReleaseMain.tscn`,
`--scene res://ProductMain.tscn`, `--scene res://Main.tscn` 또는 `--scene res://Scope1Main.tscn`을
명시해 실행한다. 이전 내부 후보의 대표 shell
smoke는 [캠페인·저장 종료 기록](docs/scopes/CAMPAIGN_SAVE_SETTINGS.md#9-현재-검사와-종료-기록), 전체
제품 흐름은 [폭염·정비 종료 기록](docs/scopes/HEATWAVE_MAINTENANCE.md#8-현재-검사와-종료-기록),
첫 점등 동결 smoke는 [첫 점등 구현 기준](docs/scopes/FIRST_LIGHT.md#10-현재-검사와-종료-기록)이 설명한다. 과거 실행 승인만 검증하던
`verify_implementation.rb`는 제거했으며, 현재 회귀검사로 사용하지 않는다.

## 콘셉트 이미지

이미지는 분위기와 공간 구도를 공유하는 참고 자료이며 현재 게임 화면이나 숫자 권위가 아니다.

- [핵심 전력망 건설](assets/01-grid-construction.png)
- [폭염과 노후 송전선 사용불가](assets/02-heatwave-outage.png)
- [송전 경로 비교](assets/03-route-comparison.png)
- [기존 발전소 입지 구도](assets/04-plant-siting.png)

## 단위와 법적 상태

출시판 화면의 운영 자금은 `370만 원`, 전력은 `2.5 MW`, 시각은 `2일차 06:45`처럼 단위와 의미를
붙여 표시한다. `M`, `CashUnit`, `DAY 9 16:00`은 과거 내부 후보와 역사 문서에서만 사용한다.

현재 법적 상태는 [LICENSE.md](LICENSE.md)에 기록했다. Gridworks 자체 저작물에는 공개 라이선스를
부여하지 않았으며, 공개 열람이나 내부 테스트 빌드 접근은 재사용·재배포 허가를 뜻하지 않는다.
외부 기여를 받거나 재사용을 허용하기 전에 자산별 라이선스와 기여 조건을 정해야 한다.
