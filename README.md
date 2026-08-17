# Gridworks

`Gridworks`는 이미 마을·병원·공장이 자리 잡은 지역에서 발전소, 변전소와 선로를 건설하고
운영하는 싱글 플레이 2D 전력망 전략 게임이다. 플레이어는 필수 안전 기준을 지키면서도 비용과
공사시간을 감당할 수 있는 망을 만든다.

배전 변전소의 서비스 권역은 접속 가능 범위일 뿐 전기를 만들지 않는다. 발전소에서 수요처까지
실제로 이어진 경로와 충분한 용량이 있어야 공급이 성립한다. 값싼 공유 경로는 사고에 취약하고,
완전한 이중화는 다음 공사를 할 돈을 줄인다. 예고된 위기는 이전 선택을 시험하고 결과 보고서는
어떤 설비가 누구를 끊었으며 얼마를 벌고 잃었는지 설명한다.

## 현재 상태

현재 활성 구현 단계는 없다. [세 장 캠페인 콘텐츠 고정](docs/scopes/CAMPAIGN_CONTENT.md)까지
완료했으며, 다음 2D 표현·사운드·패키징 단계는 아직 열지 않았다.

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
  Title·Pause, 안전 경계 저장·재개, 장 재시작과 최소 화면 설정을 제공한다. 현재 기본 실행 장면이다.
- [세 장 캠페인 콘텐츠 고정](docs/scopes/CAMPAIGN_CONTENT.md): 세 장의 브리핑·목표를 현재 화면에
  연결하고, 다음 장 reference 흐름에 필요한 진입현금을 검사해 복구 불가능한 진행을 막는다.

- [강변 병원 회랑](docs/scopes/SCOPE_0B_PLAYABLE.md): 고정된 마을·병원 시나리오에서 서비스 권역,
  경로, 전기 사고와 공간 공통원인, 병원 내부전원과 현금 정산을 검증한다.
- [수동 선로 건설](docs/scopes/SCOPE_1_INTERACTION.md): 고정 source·target 사이에 지지물을 직접
  놓고 거리 제한, 발주, 공사 중 무전압과 원자 완공을 검증한다. 별도 장면으로 실행한다.

사운드와 최종 아트·패키징은 별도 단계가 열릴 때까지 구현하지 않는다.
[2D 완성 로드맵](docs/ROADMAP_2D.md)은 전체 순서, [체크리스트](docs/ROADMAP_2D_CHECKLIST.md)는
단계 상태와 종료 증거를 소유한다.

과거 카드 실험, 화면 검증과 시행착오는 [개발 이력 요약](docs/DEVELOPMENT_HISTORY.md)에 압축했다.
현재까지 사람 플레이 검증은 수집하지 않았으며 `HumanValidationStatus = NOT_COLLECTED`다. 기존 LLM
관찰은 특정 화면에서 특정 과제를 수행했다는 증거일 뿐 사람 사용성·재미·밸런스나 성공률을
증명하지 않는다.

## 1.0 목표

한 지도에서 이어지는 세 장의 짧은 캠페인을 목표로 한다.

1. `첫 점등`: 변전소와 선로를 직접 지어 마을을 켠다.
2. `두 번째 심장`: 병원 주·예비 경로를 만들고 비용과 공간 독립성을 판단한다.
3. `열돔 아래`: 공장 증설, 예방정비와 예고된 폭염·노후 회선 사용불가에 대비한다.

최소 제품에는 한 종류의 가스발전소, 변전소, 지지물·선로, 마을·병원·공장, 단순 연결과 병목,
병원 내부전원, 판매·발전비·미공급 보상, 예고 타임라인, 예방정비, 원인 설명, 저장·설정과 설치
가능한 2D 빌드가 포함된다. 완공 자산 철거와 위기 중 유상 수요감축은 1.0에서 제외하며, 실제
관찰에서 없으면 진행이 막힐 때만 로드맵을 고쳐 재검토한다. 상세 범위는 로드맵이 소유한다.

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
- LLM 플레이는 자동검사로 답할 수 없는 한 문장 상호작용 질문이 남을 때만 최대 한 번 사용하며,
  사람 또는 출시 증거로 세지 않는다.
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

ruby playtests/scope-0a/verify_scope0a.rb
ruby playtests/scope-0a-r2/verify_scope0a_r2.rb
ruby playtests/scope-0b/verify_contract.rb
ruby playtests/scope-1/verify_contract.rb
dotnet run --project tools/Gridworks.Checks/Gridworks.Checks.csproj -c Release
dotnet run --project tools/Gridworks.Scope1Checks/Gridworks.Scope1Checks.csproj -c Release -- data/scope-1-v1.json
dotnet run --project tools/Gridworks.ProductChecks/Gridworks.ProductChecks.csproj -c Release -- data/product-campaign-v1.json
dotnet build game/Gridworks.Game.csproj -c Debug -t:Rebuild
```

Godot 기본 실행은 `game/project.godot`의 `ProductMain.tscn`을 열어 Title에서 새 게임 또는 이어하기를
선택하고 첫 점등부터 예방정비·폭염 결산까지 진행한다. 완료된 회귀 장면은
`--scene res://Main.tscn` 또는 `--scene res://Scope1Main.tscn`을 명시해 실행한다. 현재 대표 shell
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

화폐는 `M`으로 표시하며 `1 M = 1,000,000 CashUnit`이다. 전력은 `MW`, 에너지는 `MWh/GWh`,
절대시각은 `DAY 9 16:00`처럼 의미를 붙여 쓴다.

현재 저장소에는 라이선스가 없다. 공개 열람은 코드·문서·이미지의 재사용 허가를 뜻하지 않는다.
외부 기여를 받거나 재사용을 허용하기 전에 자산별 라이선스와 기여 조건을 정해야 한다.
