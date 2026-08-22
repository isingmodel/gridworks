# Gridworks

`Gridworks`는 이미 마을·병원·공장이 자리 잡은 지역에서 발전소, 변전소와 선로를 건설하고
운영하는 싱글 플레이 2D 전력망 전략 게임이다. 플레이어는 필수 안전 기준을 지키면서도 비용과
공사시간을 감당할 수 있는 망을 만든다.

배전 변전소의 서비스 권역은 접속 가능 범위일 뿐 전기를 만들지 않는다. 발전소에서 수요처까지
실제로 이어진 경로와 충분한 용량이 있어야 공급이 성립한다. 값싼 공유 경로는 사고에 취약하고,
완전한 이중화는 다음 공사를 할 돈을 줄인다. 예고된 위기는 이전 선택을 시험하고 결과 보고서는
어떤 설비가 멈췄고 어떤 경로가 사람들의 전력을 지켰는지 설명한다.

## 현재 상태

| 항목 | 현재 권위 |
|---|---|
| 기본 실행 | `game/project.godot`의 `CommercialMain.tscn` |
| 규칙·공간 데이터 | `data/release-world-v2.json` |
| 캠페인 데이터 | `data/release-campaign-v2.json` |
| 구현 상태 | 단계 B~G.3 완료, **상용 UX 87 개선 단계 활성** |
| 활성 계약 | `gpt-5.6-sol` ultra의 실제 게임 UX 프록시 87점 이상을 목표로 하는 [상용 UX 87](docs/scopes/COMMERCIAL_UX_87.md) |
| 다음 외부 단계 | H는 미승인. 별도 사용자 승인과 외부 증거가 필요함 |
| 지원 화면 | **1920×1080, UI 100%·125%만 지원**. 1280×720/720p 미지원 |
| 사람 증거 | `ReleaseOwnerPlayReviewStatus = NOT_COLLECTED`, `ExternalHumanValidationStatus = NOT_COLLECTED` |

[상용 2D 게임 구현 계약](docs/scopes/COMMERCIAL_2D_IMPLEMENTATION.md)의 B~G.3은 자유 배치, 분기·합류,
연속·비상 열 한계, 여덟 임무·에필로그, 저장·복구, 접근성, 시청각 표현과 macOS 내부 package
경계를 완성했다. G.1의 whole-map plate와 G.2의 낮은 reference 정렬 결과는 소유자 검토에서
거부됐고, G.3은 도시·도로·강·제방·전력 시설을 **각각의 tile/object 이미지로 만들고 코드가
개별 배치**하는 구조로 다시 구현했다. 합성 도시 배경 한 장을 뒤에 까는 방식은 허용하지 않는다.

사용자는 2026-08-22에 별도 [상용 UX 87 개선 계약](docs/scopes/COMMERCIAL_UX_87.md)을 열었다. 현재
작업은 튜토리얼·여덟 장·결과·에필로그·저장 재개의 완결성을 텍스트 계획부터 실제 end-to-end
플레이까지 평가하고, 선택한 story part만 단독 실행하는 검사 경로를 만들며, 고정
`gpt-5.6-sol` ultra의 공식 `CommercialUXProxy >=87`까지 확정된 UX 결함을 반복 개선한다.
이는 H의 사람 검토·전문 교정·공개 배포를 승인하지 않는다.

현재 Gate C 후보는 briefing·window story·operations·result·epilogue·resume orientation을 분리하고,
첫 장의 목표·단계별 다음 행동, 승인 직전 상태를 동결한 결산, 직전 결과와 현재 목표를 함께 복원하는
재개 카드를 연결했다. story selector 26개와 17개 typed failure, CommercialChecks 24/2,910 및 작성
중 선로 초안을 다음 프로세스에서 편집하는 native 회귀는 통과했지만 공식 cold actor·blind judge는
아직 실행 전이므로 87점이나 종료를 선언하지 않는다. Gate C exact fix의 독립 재검토는
`P0 0 / P1 0 / P2 0`이다.

텍스트 후보 2의 fresh replacement panel은 `TextPlanProxy = 94.6625`, `SCORED_FORMATIVE`였다.
다만 blind evidence verifier가 177개 관찰을 `SUPPORTED 155 / PARTIAL 22 / UNSUPPORTED 0`으로
판정해 전체 결론은 `BLOCKED_EVIDENCE_VERIFICATION`이다. 이 수치는 공식
`CommercialUXProxy`가 아니고, 제품 수정을 정당화하는 근거로도 사용하지 않는다.

Gate D는 [native evaluator v1.1 실행 부록](docs/product/COMMERCIAL_UX_NATIVE_EVALUATOR_ADDENDUM_KO.md)과
provenance·집계 계약을 첫 score-bearing capture 전에 동결했다. 고정
`gpt-5.6-sol` ultra, qualification 20 anchors, 3 cold actors+coverage, 동일 evidence set을 보는
3 blind judges, 별도 verifier/oracle, `FORMATIVE-01`+8 holdout을 계약으로 묶었고 native 집계
schema 21개·contract 23개·gold-state 11개·aggregate 44개 결정론적 검사와
CommercialChecks 24/2,910, Debug·Release 0 warning/error는 통과했다. 현재 gold native replay
owner 52개가 pending이고 E09 witness 4개가 미바인딩이며,
10개 deterministic producer stage의 구현·raw hash binding이 남아 `BLOCKED_PRE_CAPTURE`이다.
따라서 공식 cold/native 점수는 아직 없으며, 기존 Gate C smoke는 score evidence에 포함하지 않는다.

G.3 최종 v27은 55개 개별 runtime art, 338개 원자 도시 배치와 641개 전체 world 배치, 굽은 강과
정상·고온·범람 상태, 발전소·변전소·철탑 부품 조립, full-bleed HUD와 독립 event timeline을
연결했다. `CommercialChecks` 22 suites/2,331 assertions, Debug·Release build, native 1920×1080
UI 100%·125% presentation, checkpoint→completion→completed-resume 실제 입력이 통과했다.

고정 `gpt-5.6-sol` ultra의 10-pair 단일-call formative proxy는 **74.375**였다. 이는 공식 40-call
`ReferenceParity` 점수가 아니므로 `referenceParity = null`이다. 원래 `>80` gate를 달성했다고
소급 표기하지 않는다. 사용자가 2026-08-22에 “v27까지 진행하고 점수와 무관하게 이번 step을 성공
종료”하도록 최종 지시해 G.3을 닫았다. 정확한 구성과 남은 시각 차이는
[v27 종료 증거](playtests/commercial-2d/g3-final-candidate/FORMATIVE_V27_SUMMARY.md), 단계별 종료
상태는 [체크리스트](docs/ROADMAP_2D_CHECKLIST.md)가 소유한다.

기존 `ReleaseMain`과 33×21 후보, 과거 1280×720 smoke는 동결 회귀 기준선일 뿐 현재 제품의 실행
권위나 지원 해상도가 아니다.

## 다음 작업자 인계

1. 이 README와 [문서 안내](docs/README.md)를 먼저 읽고, 현재 사용자 요청이 새 구현 범위를
   명시적으로 여는지 확인한다.
2. 활성 범위는 [상용 UX 87 계약](docs/scopes/COMMERCIAL_UX_87.md)뿐이다. G.3 reference 목표를 다시
   열거나 로드맵 H를 선행 구현하지 않는다.
3. v2 world·campaign과 `CommercialMain`을 단일 권위로 삼는다. 화면에서 규칙을 다시
   계산하지 말고 Core의 typed 결과를 표현한다.
4. 시각 변경을 다시 승인받으면 개별 object/tile 구성, 2:1 사선 시점, 굽은 강·제방 접합,
   reference-scale 도시 밀도, 전력망 상태와 event timeline을 함께 보존한다.
5. 변경 전후에 `CommercialChecks`, Debug·Release rebuild와 관련 native actual-input smoke를
   실행한다. 큰 단위가 끝나면 독립 검토와 현재 문서 갱신까지 같은 단위로 닫는다.
6. Gate D score-bearing capture는 `BLOCKED_PRE_CAPTURE`의 gold replay·E09 witness·10 producer binding을
   모두 닫고 native 계약을 commit·독립 검토한 뒤에만 시작한다. 기존 developer smoke를 재사용하지 않는다.

## 다음 승인 지점

상용 UX 87 범위가 종료된 뒤에도 단계 H는 다음 조건을 충족할 때만 별도 사용자 지시로 연다.

- 소유자의 전체 캠페인 플레이와 한국어 전문 교정 범위 확정
- 실제 지원 macOS 환경과 기기 목록 확정 및 실행 증거 수집
- 공개 배포를 원할 경우 Developer ID, 공증 권한과 배포 결정 제공
- 공개 후보 source commit·package SHA 고정, clean package와 새 user-data에서 저장→재개→완주 검증

LLM reference jury나 자동검사는 사람 사용성·재미·전문 교정·실제 OS 호환성·서명 권한을 대신하지
않는다. H가 아니라 새 기능을 원하면 먼저 별도 scope와 데이터 권위를 승인해야 한다.

## 1.0 결과

보이는 격자 없는 한 도시 지도와 전력망이 이어지는 여덟 임무를 구현했다.

1. 프롤로그 `첫 불빛 · 두 번째 심장 · 두 번째 전원`: 자유 배치, 회랑 독립성과 연속 한계를 익힌다.
2. `북안의 약속`: 서비스권역과 미래 분기 공간을 선택한다.
3. `누구의 여유인가`: 도시 약속과 비상 열여유·다음 국면 보호정지를 비교한다.
4. `물이 닿기 전에`: 공사 기한 안에 범람 밖 회랑을 만든다.
5. `꺼야 지킬 수 있다`: 계획정지 중 남은 경로의 열여유로 필수시설을 지킨다.
6. `가장 긴 밤`: 최대수요, 보호정지와 범람을 앞선 망으로 통과한다.

현재 제품에는 22.9 kV급 계획 모델, 분기·합류, 고정소수점 자유 배치, 점유영역·지형,
연속·비상 열 한계와 국면 상태, 결정론적 공급·우회, 한국어 UX, 실제 망을 기억하는 이야기,
저장·설정과 설치 가능한 2D 내부 빌드가 포함된다. 현실성의 상한과 전역 제외는
[완료 구현 계약](docs/scopes/COMMERCIAL_2D_IMPLEMENTATION.md)이 소유한다.

## 저장소 구조

```text
assets/                 비권위 콘셉트 이미지
data/                   prototype과 완료 제품의 machine-readable fixture
docs/                   문서 지도, 로드맵, 완료 계약과 압축 이력
game/                   Godot .NET 장면과 화면 adapter
src/Gridworks.Core/     Godot을 참조하지 않는 C# 규칙
tools/                  독립 자동검사와 package 도구
playtests/              동결된 실행 입력과 승인된 증거
```

문서의 읽는 순서와 질문별 소유자는 [문서 안내](docs/README.md), 오브젝트의 현재 기능 상태는
[오브젝트 카탈로그](docs/product/OBJECT_CATALOG.md)가 관리한다. 과거 결정은
[개발 이력 요약](docs/DEVELOPMENT_HISTORY.md)에 압축돼 있다.

## 개발 원칙

- 한 번에 승인된 단계 하나만 연다. 다음 단계의 interface, schema field와 placeholder UI를 미리
  만들지 않는다.
- 실행 숫자는 해당 단계의 machine-readable fixture 한 곳만 소유한다.
- 권위 규칙은 순수 C# Core에 두고, Godot은 명령을 보내고 반환 상태를 그린다.
- 제품의 권위 상태·기본 실행·검증 진입점은 하나씩 유지한다.
- schema, 보존식, 상태전이, build, crash와 save 손상처럼 기계적인 사실만 강한 PASS/FAIL로
  판정한다.
- 이해·혼란·재미는 관찰로 기록한다. LLM 증거를 사람 또는 출시 증거로 세지 않는다.
- 큰 개발단위가 끝나면 자동증거·native smoke·문서·독립 검토를 함께 닫는다.

파라미터 분류와 정적 분석 도구의 개방 조건은
[Static Balance Lab](docs/development/BALANCING_STATIC_SIM.md)이 소유한다. 목표 점수에 맞춘 무제한
자동 튜닝은 사용하지 않는다.

## 개발 도구

[`global.json`](global.json)은 .NET SDK `8.0.129`를 고정하고 roll-forward를 끈다. Godot binary는
Git에 포함하지 않는다. 현재 검증한 도구는 `Godot 4.7.1.stable.mono.official.a13da4feb`이다.

- archive: `Godot_v4.7.1-stable_mono_macos.universal.zip`
- SHA-256: `92cac516baa8ddc7756eeaa38a6d007778a968bfbf188db7c5d6e6ec21c5d52c`
- 로컬 binary: `.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot`

`.tools/`는 로컬 설치 경로이며 새 checkout에서는 별도로 준비해야 한다.

## 현재 확인 명령

현재 제품의 최소 deterministic gate는 다음과 같다.

```sh
dotnet restore tools/Gridworks.CommercialChecks/Gridworks.CommercialChecks.csproj
dotnet restore game/Gridworks.Game.csproj
dotnet run --project tools/Gridworks.CommercialChecks/Gridworks.CommercialChecks.csproj -c Release
dotnet build game/Gridworks.Game.csproj -c Debug -t:Rebuild
dotnet build game/Gridworks.Game.csproj -c Release -t:Rebuild
python3 tools/reference-parity/test-aggregate-jury.py
```

기능을 건드리면 관련 동결 회귀와 native actual-input smoke도 실행한다. package와 배포 검사는 해당
범위를 사용자가 승인했을 때만 연다. 완료된 prototype·옛 출시 후보의 전체 명령과 증거 위치는 각
scope 문서와 [체크리스트](docs/ROADMAP_2D_CHECKLIST.md)에 남아 있다.

## 콘셉트 이미지

이미지는 분위기와 공간 구도를 공유하는 참고 자료이며 게임 숫자 권위가 아니다.

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
