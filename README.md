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
**단계 G — 시청각·접근성·패키징 마감**이다. 사용자가 2026-08-19 단계 G와 관찰 기반 선행 보정
backlog 구현을 명시적으로 승인했다. 단계 F까지의 규칙·캠페인은 동결하고, 관찰 task를 먼저
재현·보정한 뒤 최종 표현·settings v3·화면 증거·내부 package gate를 닫는다. 단계 H 사람 검증과
공개 배포는 열지 않는다.

현재 기본 장면은 `CommercialMain`이다. 별도 v2 world·campaign·Core에서 보이는 격자 없는 자유
배치, 수면·건물·설비 점유영역, 서비스 권역과 실제 발전원 경로의 분리, 선로 도체·변전소 주기기·
전신주 접속부의 연속·비상 열 한계와 보호정지, 안전 의무·도시 약속·최근 공사 복구, 같은 망을
이어 쓰는 여덟 임무와 에필로그·save v3가 연결돼 있다.

단계 F 기준은 final world SHA-256
`c4923f752205c193efa78ddb4ca9e5431801731e6087be3ba3796abf9117ac14`, campaign SHA-256
`078df95f9f0c833be7e1a299088b4ab6e0de4ddf13426ce5b96a1abbeee70b7a`를 사용한다.
CommercialChecks **29 suites / 4,486 assertions**, Game Debug·Release·ExportRelease
**0 warnings / 0 errors**, 1280×720·UI 125%의 두 fresh process 전체 캠페인 smoke와 독립
exact-tree 기술 감사 **P0 0 / P1 0**을 통과했다. 이 수치는 규칙·저장·wiring 증거이지 재미나
상용 출시 준비 증거가 아니다.

오른쪽 패널에 동적 버튼이 늘며 상단 정보가 보이지 않던 문제는 커밋 `36038a9`에서 바로잡았다.
정보 영역은 최소 200px을 유지하고, 보조 조작은 keyboard focus를 따라가는 스크롤 영역에 두며,
`운영안 승인`과 `공사 발주`는 하단에 고정했다.

같은 후보로 수행한 공식 cold LLM 관찰은 한 번의 새 게임으로 앞선 임무를 통과한 뒤 8장
`가장 긴 밤`의 폭염 국면 2/3에서 사용자가 중단했다. 화면에는 비상 운전 500 kW 부족이
표시됐고, 참가자는 초안 취소 두 번과 약속 변경을 사용했으며 reload·두 번째 새 게임·장 되감기는
사용하지 않았다. 게임플레이 종료 상태는 `USER_STOPPED`이며 성공·실패·`BLOCKED`가 아니다.
에필로그에 도달하지 않았으므로 전체 완주 가능성을 판정하지 않는다. 사용자 중단 뒤 같은 참가자에게
follow-up 회고를 요청했으므로 원래의 `follow-up 없음` cold completion protocol은
`INVALIDATED_BY_USER_FOLLOWUP`이다. 그 회고에서 이름 붙은 병목 경로, 배치 단위의 즉시 성공·거부 피드백,
국면별 승인 조건 체크리스트를 우선 후속 과제로 남겼다. 정확한 관찰 상한은
[상용 구현 계약·완료 기록](docs/scopes/COMMERCIAL_2D_IMPLEMENTATION.md), task와 수용 기준은
[로드맵의 관찰 기반 backlog](docs/ROADMAP_2D.md#관찰-기반-선행-보정-backlog--단계-g-활성)가 소유한다.

`FullCampaignHumanStatus = NOT_COLLECTED`, `ReleaseOwnerPlayReviewStatus = NOT_COLLECTED`,
`ExternalHumanValidationStatus = NOT_COLLECTED`,
`KoreanProfessionalProofStatus = NOT_COLLECTED`다. LLM 관찰은 특정 build에서 보인 행동과
혼란을 기록할 뿐 사람 사용성·재미·밸런스, 성공률이나 한국어 품질을 증명하지 않는다.

단계 G에서 최종 도시 아트·날씨·인물 초상·audio cue, settings v3와 움직임 줄이기, 두 해상도·
두 UI 배율의 화면·키보드 동등성, 패키징·서명·법적 정리와 새 설치 전체 실행을 구현·검증한다.
관찰에서 나온 후속 과제는 관찰 한 건의 결함률 주장이 아니라, 독립 재현 뒤 닫는 선행 작업 목록이다.

기존 `ReleaseMain`·`ProductMain`과 33×21 격자 후보, Scope 0/1 실험은 회귀·역사 자료로만
보존한다. 과거 카드 실험, prototype, 소유자 전체 플레이와 이전 LLM 관찰의 핵심 결과는
[개발 이력 요약](docs/DEVELOPMENT_HISTORY.md)에 압축했다. 과거 후보의 증거를 현재 v2 제품의
검증으로 합산하지 않는다.

## 1.0 목표

보이는 격자 없는 한 도시 지도와 전력망이 이어지는 여덟 임무를 목표로 한다.

1. 프롤로그 `첫 불빛 · 두 번째 심장 · 두 번째 전원`: 자유 배치, 두 접속 회선·범람 시험과 연속
   한계를 익힌다.
2. `북안의 약속`: 서비스권역과 미래 분기 공간을 선택한다.
3. `누구의 여유인가`: 도시 약속과 비상 열여유·다음 국면 보호정지를 비교한다.
4. `물이 닿기 전에`: 공사 기한 안에 범람 밖 회랑을 만든다.
5. `꺼야 지킬 수 있다`: 계획정지 중 남은 경로의 열여유로 필수시설을 지킨다.
6. `가장 긴 밤`: 최대수요, 보호정지와 범람을 앞선 망으로 통과한다.

최소 출시판에는 22.9 kV급 계획 모델, 분기·합류, 고정소수점 자유 배치, 점유영역·지형,
연속·비상 열 한계와 국면 상태, 결정론적 공급·우회, 전문적인 한국어 UX, 실제 망을 기억하는
이야기, 저장·설정과 설치 가능한 2D 빌드가 포함된다. 상세 범위와 현실성의 상한은
[상용 구현 계약과 완료 기록](docs/scopes/COMMERCIAL_2D_IMPLEMENTATION.md)이 소유한다. 단계 F
종료 시점에는 설정·최종 자산·패키징이 남아 있어 이 최소 출시판 목표 전체를 달성했다고 보지 않는다.

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

현재 상용 v2 경로는 다음 최소 명령으로 확인한다.

```sh
dotnet restore game/Gridworks.Game.csproj
dotnet restore tools/Gridworks.CommercialChecks/Gridworks.CommercialChecks.csproj

dotnet run --project tools/Gridworks.CommercialChecks/Gridworks.CommercialChecks.csproj -c Release
dotnet build game/Gridworks.Game.csproj -c Debug -t:Rebuild
dotnet build game/Gridworks.Game.csproj -c Release -t:Rebuild
dotnet build game/Gridworks.Game.csproj -c ExportRelease -t:Rebuild
```

Godot 기본 실행은 `game/project.godot`의 `CommercialMain.tscn`이다. 단계 D 종료에서 제품 장면을
전환했으며 실행 방법은 [설치·실행 안내](INSTALL.md)에 정리했다. 동결된 회귀 장면은
`--scene res://ProductMain.tscn`, `--scene res://Main.tscn` 또는 `--scene res://Scope1Main.tscn`을
명시해 실행한다. 이전 runner는 영향을 받는 회귀 변경에서만 각 동결 scope의 종료 기록에 따라
실행한다. 과거 실행 승인만 확인하던 `verify_implementation.rb`는 제거했으며 현재 검사로 사용하지
않는다.

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
