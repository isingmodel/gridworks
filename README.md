# Gridworks

`Gridworks`는 이미 마을·병원·공장이 자리 잡은 지역에서 발전소, 변전소와 선로를 건설하고
운영하는 싱글 플레이 2D 전력망 전략 게임이다. 플레이어는 필수 안전 기준을 지키면서도 비용과
공사시간을 감당할 수 있는 망을 만든다.

배전 변전소의 서비스 권역은 접속 가능 범위일 뿐 전기를 만들지 않는다. 발전소에서 수요처까지
실제로 이어진 경로와 충분한 용량이 있어야 공급이 성립한다. 값싼 공유 경로는 사고에 취약하고,
완전한 이중화는 다음 공사를 할 돈을 줄인다. 예고된 위기는 이전 선택을 시험하고 결과 보고서는
어떤 설비가 멈췄고 어떤 경로가 사람들의 전력을 지켰는지 설명한다.

## 현재 상태

실시간 물리 세계 전면 개편은 사용자 지시로 **R2 뒤 중단**됐으며 현재 활성 구현 단계는 없다.
R0 계약·동결 기준선은 `5a9e465`, R1 결정론적 실시간 Core vertical slice는 `3da1897`, R2 UX
foundation·수평 사건 지평선 구현은 `4c27f65`에 남아 있다. 다만 R2의 마지막 exact-tree 전체
harness는 사용자가 실행 중단을 지시해 끝나지 않았으므로 완료 gate가 아니다. 그 전까지 수집된
자동·native 증거는 그대로 보존하되 R2 종료, 전면 개편 완료나 출시 증거로 승격하지 않는다.
[전면 개편 계약](docs/scopes/REALTIME_PHYSICAL_TOTAL_REVISION.md)이 이 경계와 단계 기록을 소유한다.
R3~R7과 더 넓은 전면 개편은 `USER_STOPPED_AFTER_R2`이며 새 사용자 승인 전에는 구현하지 않는다.

단계 B~G와 관찰 기반 선행 보정 backlog를 끝낸 상용 v2는 회귀·저장 이관 기준선으로 동결한다.
과거 단계 H 사람 검증·전문 교정·공개 배포는 v2의 미개방 외부 gate였으며, 이번 개편을 공개
배포 승인으로 해석하지 않는다.

현재 저장소 기본 장면은 `CommercialMain`이다. R1의 별도 V3 Core·fixture와 R2의 비기본 scene은
이 경로를 대체하지 않는다. v2 world·campaign·Core에서 보이는 격자 없는 자유
배치, 수면·건물·설비 점유영역, 서비스 권역과 실제 발전원 경로의 분리, 선로 도체·변전소 주기기·
전신주 접속부의 연속·비상 열 한계와 보호정지, 안전 의무·도시 약속·최근 공사 복구, 같은 망을
이어 쓰는 여덟 임무와 에필로그·save v3가 연결돼 있다. 단계 G는 이름 붙은 병목 경로, 원자적
배치 피드백, 승인 체크리스트와 국면 표, 누적 공사 예측·복구 미리보기, settings v3·움직임 줄이기,
날씨·초상·사운드와 네 화면 조합의 접근성 표현을 더했다.

macOS 1.0.0 내부 ad-hoc 후보를 clean commit에서 만들고 저장소 밖의 빈 user-data로 새 게임→저장→
fresh process 이어하기→8장·에필로그→완료 저장 재개→장 재설계를 한 번 끝까지 확인했다. 이 후보는
Developer ID 서명·공증을 거치지 않았고 공개 배포가 승인되지 않았다. 자동검사·native marker·
archive identity와 독립 감사의 정확한 기록은
[단계 G 완료 증거](docs/scopes/COMMERCIAL_2D_IMPLEMENTATION.md#8-전체-완료-증거--단계-g-완료)가
한 곳에서 소유한다.

단계 F 뒤의 공식 cold LLM 관찰은 8장 도중 사용자가 중단했고, 뒤이어 같은 참가자에게 회고를
요청해 원래의 no-follow-up 완료 protocol이 무효화됐다. 관찰에서 나온 후속 task는 단계 G에서
재현·보정했지만 그 한 실행을 사람 사용성·재미·밸런스 증거로 승격하지 않는다. 이번 개편은
`temp/`의 두 시각 비평 package를 새 제품 입력으로 검토했으며, 그 이미지를 runtime 자산이나
독립된 두 사람의 합의로 취급하지 않는다. 자세한 과거 경과는
[개발 이력 요약](docs/DEVELOPMENT_HISTORY.md), 완료된 수용 기준은
[로드맵 backlog](docs/ROADMAP_2D.md#관찰-기반-선행-보정-backlog--완료)가 소유한다.

`FullCampaignHumanStatus = NOT_COLLECTED`, `ReleaseOwnerPlayReviewStatus = NOT_COLLECTED`,
`ExternalHumanValidationStatus = NOT_COLLECTED`,
`KoreanProfessionalProofStatus = NOT_COLLECTED`다. LLM 관찰은 특정 build에서 보인 행동과
혼란을 기록할 뿐 사람 사용성·재미·밸런스, 성공률이나 한국어 품질을 증명하지 않는다.
물리 UHD panel 확인도 `OPEN_EXTERNAL_HARDWARE_NOT_AVAILABLE`이며 공개 출시는 계속
`NOT_AUTHORIZED`다. [HTML 목표 이미지](docs/mockups/realtime-target/README.md)는 비실행 참고 시안일
뿐 runtime 화면·구현·native/사람/전문 검토 증거가 아니다.

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
이야기, 저장·설정과 설치 가능한 2D 빌드가 포함된다. 이 문단은 동결 v2 목표의 요약이며 과거
상세 범위는 [상용 구현 계약과 완료 기록](docs/scopes/COMMERCIAL_2D_IMPLEMENTATION.md), 현재
실시간·물리 세계 제품 규칙은 [전면 개편 계약](docs/scopes/REALTIME_PHYSICAL_TOTAL_REVISION.md)이
소유한다. 단계 G 종료 시점에는 v2 기술·제품 범위와 내부 후보 gate를 닫았다. 다만 사람 전체 플레이, 전문 한국어
교정, 실제 지원 환경, Developer ID 서명·공증과 공개 배포 결정은 단계 H의 미수집·미승인 항목이므로
상용 release-ready라고 부르지 않는다.

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
