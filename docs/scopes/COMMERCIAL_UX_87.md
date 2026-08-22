# Gridworks — 상용 UX 87 개선 계약

> 상태: **활성 — 2026-08-22 사용자 승인**  
> 목표: 고정 `gpt-5.6-sol` + reasoning effort `ultra`의 `CommercialUXProxy >= 87`  
> 선행 권위: [완료된 상용 2D 구현 계약](COMMERCIAL_2D_IMPLEMENTATION.md)

이 계약은 완성된 B~G.3 제품을 실제 판매 가능한 게임 경험의 관점에서 다시 관찰하고, 튜토리얼부터
에필로그까지의 전달·진행·결과·재개 UX를 개선하는 현재 구현 범위다. 사용자는 README 기반 텍스트
계획 평가를 출발점으로 삼고, 선택한 스토리 파트만 단독 실행하는 개발 진입점과 실제 end-to-end
LLM 플레이까지 포함하도록 승인했다.

이 작업은 로드맵 H를 열지 않는다. LLM judge는 반복 가능한 내부 제품 프록시이며 소유자 플레이,
외부 사람 사용성, 전문 한국어 교정, 실제 기기 호환성, 공개 배포·서명 증거를 대신하지 않는다.

## 1. 플레이어 결과

최종 후보는 처음 접한 플레이어가 저장소나 캠페인 데이터를 보지 않고 다음 흐름을 이해하고 끝낼 수
있어야 한다.

1. 타이틀에서 새 게임을 시작하고 첫 장의 목표·조작·완료 조건을 화면 안에서 찾는다.
2. `첫 불빛 → 두 번째 심장 → 두 번째 전원`에서 자유 배치, 실제 접속, 회랑 독립성, 용량을
   행동과 피드백으로 익힌다.
3. 본편 다섯 장에서 폭염·범람·보호정지·도시 약속이 이전에 만든 망을 어떻게 시험하는지 예측하고
   선택한다.
4. 각 장의 결과를 다음 장의 현재 상태와 혼동하지 않고, 자신의 실제 경로·병목·약속 결과를 읽는다.
5. 중간 저장과 프로세스 재시작 뒤 현재 목표와 맥락을 되찾고, 여덟 장·에필로그·장 선택까지
   완결된 캠페인으로 경험한다.

## 2. 포함 범위

- [상용 UX 평가 프로토콜](../product/COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md)과 결정론적 집계기
- `release-campaign-v2.json`의 정상 도달 가능한 26개 briefing·window story·result·epilogue 목록과
  텍스트 계획 평가 입력
- 하나의 안정된 selector로 특정 스토리 파트만 로드·검증·출력하는 개발용 단독 실행 경로
- 첫 세 장의 점진적 튜토리얼 전달, 목표·실패·회복 설명과 필요 최소 authored text 보강
- 임무 결과와 다음 임무 상태의 명확한 경계, 저장·재개 reorientation
- 지원 화면 1920×1080 UI 100%·125%의 정보 위계·clipping·focus
- 실제 전체 캠페인 cold journey와 누락 분기 full-coverage 관찰
- judge가 반복해 확인한 범위 안의 국소 UI·문구·피드백 개선

## 3. 제외 범위

- 새 발전 규칙, 새 설비, 새 지도, 새 장, 실시간 급전 또는 v3 world/campaign schema
- G.3 reference parity 목표의 재개나 대규모 자산 재생성
- 1280×720/720p 지원, Windows/Linux 지원 확대
- 공개 package, Developer ID, 공증, 업로드, 마케팅·가격·상점 페이지
- 사람 검토를 LLM 점수로 대체하거나 `NOT_COLLECTED` 상태를 변경하는 일
- 목표 점수에 맞춘 규칙·경제 숫자의 무제한 자동 튜닝

결함 수정에 필요한 UI adapter와 authored campaign text는 바꿀 수 있지만, world·campaign 숫자와 Core
규칙은 기존 단일 권위를 유지한다. 규칙이 잘못됐다는 별도 결정론적 증거 없이 judge의 취향만으로
전력 규칙이나 경제를 바꾸지 않는다.

## 4. 단일 권위와 구현 경계

| 대상 | 권위 |
|---|---|
| 기본 실행 | `game/project.godot` → `CommercialMain.tscn` |
| 공간·규칙 숫자 | `data/release-world-v2.json` |
| 장 순서·authored story | `data/release-campaign-v2.json` |
| 상태전이·결과 사실 | `src/Gridworks.Core/Release/V2` |
| 화면 표현·입력 | `game/Commercial*.cs`, `game/Commercial*.tscn` |
| 결정론적 검사 | `tools/Gridworks.CommercialChecks`와 상용 UX 전용 도구 |
| LLM 평가 절차 | `docs/product/COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md` |
| 실행 증거 | `playtests/commercial-ux-87/` |

Game은 story와 결과 사실을 다시 만들지 않고 Core/campaign의 typed 값을 표현한다. 평가용 selector는
개발 도구에만 존재하며 제품 저장을 조작하거나 잠긴 장을 건너뛰는 runtime cheat를 추가하지 않는다.

## 5. 스토리 파트 단독 실행 계약

개발자는 전체 캠페인을 재생하지 않고 다음 selector 한 개를 명시해 해당 파트를 실행할 수 있어야
한다.

```text
<chapterId>/briefing
<chapterId>/window/<windowId>
<chapterId>/result/standard
<chapterId>/result/keep
<chapterId>/result/defer
campaign/epilogue
```

- selector는 대소문자와 구분자를 포함해 canonical ID 하나만 허용한다.
- 출력은 selector, chapter/window ID, story의 speaker/title/body, branch 도달 가능성, 요구되는 promise branch를
  가진 stable JSON이다.
- 존재하지 않거나 그 장에서 불가능한 branch는 성공처럼 fallback하지 않고 명시적으로 실패한다.
- 단독 실행 검사는 campaign JSON에서 직접 읽으며 같은 문구를 test source에 복사하지 않는다.
- 전체 coverage 검사는 모든 정상 도달 story가 정확히 한 canonical selector에 대응하고, promise가
  없는 장과 있는 장의 result branch가 섞이지 않음을 증명한다.
- 현재 세 장에서 first-window story가 briefing을 화면상 대체하는 현상은 catalog 도달성과 구분해
  native wiring 결함으로 검사하고 Gate C에서 해결한다.
- 실패·회복 화면은 authored story와 별개인 실제 상태 UX coverage로 보존하고 native episode에서
  확인한다.

## 6. 작업 순서와 게이트

### Gate A — 프로토콜·텍스트 기준선

- 평가 rubric, label, 집계, cap, judge model/effort와 입력 격리를 후보 수정 전에 고정한다.
- 캠페인 권위에서 도달 가능한 story manifest를 만들고 text-plan proxy를 세 fresh judge로 평가한다.
- text 점수는 개선 우선순위만 정하며 실제 플레이 점수나 최종 PASS가 아니다.

### Gate B — 단독 실행·결정론적 coverage

- story selector CLI와 전체 coverage 검사를 구현한다.
- 튜토리얼·여덟 장·결과 분기·에필로그의 누락/중복/불가능 selector를 자동검사한다.
- text-plan judge가 반복해 지적하고 verifier가 확인한 전달 결함만 authored text에서 보강한다.

### Gate C — 실제 UX 결함 수정

- 결과와 다음 장 상태가 한 화면에서 충돌하지 않게 명시적 전환을 둔다.
- 새 게임 도움과 재개의 reorientation을 구분한다.
- 지원 화면에서 briefing·목표·다음 행동이 가려지지 않게 한다.
- 변경마다 CommercialChecks, Debug·Release rebuild와 관련 native actual-input smoke를 실행한다.

### Gate D — end-to-end judge와 반복

- clean user-data의 cold journey와 고정 full-coverage episode를 실행한다.
- 고정 judge/프롬프트/집계기로 평가하고, 확정된 P0/P1만 다음 후보에 반영한다.
- 후보를 바꾸면 새 commit·새 process에서 전체 final 평가를 다시 실행한다. 낮은 결과를 버리고 같은
  후보를 재추첨하거나 더 유리한 캡처를 고르지 않는다.

## 7. 종료조건

다음을 모두 만족할 때만 이 범위를 완료한다.

- 공식 final `CommercialUXProxy >= 87.0`
- 평가 프로토콜의 category floor, cap, hard gate와 안정성 조건 모두 통과
- text-plan 평가와 실제 native 평가를 분리해 보존하고 실제 평가만 최종 점수를 소유
- 모든 도달 가능 story selector coverage PASS와 대표 단독 실행 PASS
- `CommercialChecks`, Debug·Release rebuild, 변경 영향 회귀 PASS
- fresh 1920×1080 UI 100%·125%에서 새 게임, 결과 전환, 저장→프로세스 재시작→재개,
  마지막 장→에필로그→장 선택 actual-input PASS
- crash, data loss, softlock, 필수 외부 정보 의존 0
- 독립 exact-diff 검토의 열린 P0/P1 0
- README, 문서 안내, 체크리스트와 `playtests/commercial-ux-87/` 최종 증거가 같은 commit을 가리킴

judge를 실행할 수 없거나 판정이 프로토콜 허용 범위를 넘어 불안정하면 낮은 점수나 PASS를 만들지
않고 `BLOCKED_*`로 기록한다. 87점은 사람 출시 승인이나 외부 판매 가능성의 보증이 아니라, 사용자가
지정한 고정 LLM 내부 프록시 gate다.

## 8. 진행 기록 — 2026-08-22 Gate A·B 첫 기준선

- campaign 권위에서 정상 도달 가능한 story selector 26개를 만들고, 존재하지 않거나 불가능한
  selector 17개가 typed failure로 끝남을 검사했다. 누적 `CommercialChecks`는 23 suites,
  2,885 assertions를 통과했다.
- `--story-part`, `--story-manifest`는 campaign JSON을 직접 읽는 stable JSON 개발 경로이며 제품
  저장이나 runtime 진행을 건너뛰지 않는다.
- 첫 `gpt-5.6-sol` ultra 텍스트 후보는 초기 3인 패널과 허용된 한 번의 fresh replacement 뒤에도
  `TP-A4`, `TP-P1`의 ordinal range 2가 남아 `BLOCKED_JUDGE_INSTABILITY`로 닫혔다. 점수나 PASS를
  만들지 않았고 상세 원본은
  [`baseline-text-v1`](../../playtests/commercial-ux-87/baseline-text-v1/README.md)이 소유한다.
- fresh storage의 개발자 직접 플레이는 첫 불빛 결산이 다음 장 현금·경계·실패 projection과
  섞이는 결함을 재현했다. 이는 공식 cold actor 증거가 아니며
  [`developer-pilot-v1`](../../playtests/commercial-ux-87/developer-pilot-v1/README.md)에 관찰만 남겼다.
