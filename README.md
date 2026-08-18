# Gridworks

`Gridworks`는 이미 마을·병원·공장이 자리 잡은 지역에서 발전소, 변전소와 선로를 건설하고
운영하는 싱글 플레이 2D 전력망 전략 게임이다. 플레이어는 필수 안전 기준을 지키면서도 비용과
공사시간을 감당할 수 있는 망을 만든다.

배전 변전소의 서비스 권역은 접속 가능 범위일 뿐 전기를 만들지 않는다. 발전소에서 수요처까지
실제로 이어진 경로와 충분한 용량이 있어야 공급이 성립한다. 값싼 공유 경로는 사고에 취약하고,
완전한 이중화는 다음 공사를 할 돈을 줄인다. 예고된 위기는 이전 선택을 시험하고 결과 보고서는
어떤 설비가 멈췄고 어떤 경로가 사람들의 전력을 지켰는지 설명한다.

## 현재 상태

현재 활성 구현 단계는 [상용 2D 게임 구현](docs/scopes/COMMERCIAL_2D_IMPLEMENTATION.md)의
**단계 E — 첫 네 임무와 공통 UX**다. 사용자는 보이는 격자를 없앤 자유 배치, 선로 도체·변전소
주기기·전신주 접속부의 연속·비상 열 한계와 상용 재기획서 전체 구현을 승인했다. 새 제품은 별도
v2 world·campaign·Core와 기본 장면에서 단계 B부터 G까지 순서대로 만든다. 기존 `ReleaseMain`과
33×21 후보는 기술 회귀 기준선이며 새 규칙의 실행 권위가 아니다.

단계 B의 명시 실행 장면은 고정소수점 자유 좌표, 원형 점유영역, 수면·건물·위험구역, 교차 비접속
선로, 초안 전신주 이동과 세 단계 카메라를 구현했다. CommercialChecks 7개 묶음 238 assertions,
Game Debug·Release build와 1280×720·UI 125% native 자유 배치 흐름을 통과했고 독립 검토에서
P0/P1이 없었다. 이 기반은 단계 D 제품 장면에 통합됐다.

단계 C는 같은 v2 지도에 선로 도체·변전소 주기기·전신주 접속부의 연속·비상 한계, 공유 사용량,
모든 발전원·경로의 결정론적 선택과 `비상 운전 → 보호정지 → 한 국면 냉각 뒤 복귀`를 연결했다.
CommercialChecks 13개 묶음 350 assertions와 Debug 전용 native 열 화면을 통과했고, Release 제품
assembly에는 검사 국면과 대표 해법을 넣지 않았다.

단계 D는 짧은 `첫 불빛` prelude와 4장 완료 상태의 `누구의 여유인가`를 실제 제품 장면에 연결했다.
안전 의무·도시 약속·운영 기록, 두 선종, 작성 기한, 공개 국면 preview, 최근 공사 복구, 실제 결과
사실과 save v3를 같은 command journal로 처리한다. CommercialChecks 19개 묶음 1,312 assertions와
서로 다른 두 Godot 프로세스의 저장·복원·완료 흐름을 통과했고, 독립 검토의 P0/P1을 모두 닫았다.
새 `CommercialMain`이 이제 기본 장면이며 단계 E는 같은 runner를 여덟 임무용 final campaign v2로
확장한다.

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
기억하는 여덟 임무를 새 기반으로 삼는다. 정확한 구현 경계와 순서는 활성 계약이 소유한다.

동결 기준선 `ReleaseMain`은 33×21 지도에서 변전소와 선로를 직접 건설하고,
분기·합류 설비의 사용량·정격·접속 여유를 보면서 같은 청류시 망을 8개 임무 동안 이어 쓴다.
예고 상황을 지도에 미리 적용해 사용 불가 설비와 우회 경로를 비교할 수 있고, 과거 설계가 뒤의
임무를 막으면 이전 임무 시작부터 다시 설계할 수 있다. 두 fresh process 대표 흐름에서 본편 중간
저장·이어하기·장 재시작과 마지막 임무 완료까지 확인했다.
[화면·입력 증거](playtests/release-2d/LAYOUT_EVIDENCE.md)는 직전 후보의 네 화면 배치와 저장소 밖
키보드 focus 흐름, 후속 후보의 1280×720·UI 125% 작업 영역·접속 한도·수면 거부 확인을 구분해
보존한다.
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
[활성 계약](docs/scopes/COMMERCIAL_2D_IMPLEMENTATION.md)이 소유한다.

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

Godot 기본 실행은 `game/project.godot`의 `CommercialMain.tscn`이다. 단계 D 종료에서 제품 장면을
전환했으며 동결된 회귀 장면은
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
