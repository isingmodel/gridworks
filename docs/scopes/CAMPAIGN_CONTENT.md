# 세 장 캠페인 콘텐츠 고정 — 활성 구현 계약

> 상태: `ACTIVE`
>
> 구현 권한: `GRANTED`
>
> 사람 검증: `NOT_COLLECTED`

이 단계는 이미 완주 가능한 누적 제품 흐름을 `첫 점등 → 두 번째 심장 → 열돔 아래`의 짧은
캠페인으로 읽히게 만든다. 새 전력 규칙, 새 건설물, 새 장면, 사운드·아트와 패키징은 추가하지
않는다.

## 1. 플레이어 결과

각 장은 현재 지도와 이전 장의 설비·현금을 그대로 이어받는다. 장 시작 상태에서 다음 내용을
한국어로 바로 확인할 수 있어야 한다.

- 지금까지 벌어진 일과 이번 장을 맡게 된 이유를 설명하는 짧은 업무 브리핑
- 이번 장에서 반드시 달성할 한 가지 목적
- 다음 장을 실제로 끝낼 수 있도록 남겨야 하는 최소 진입현금

단계별 조작 안내는 기존 작업 패널, 언제든 다시 보는 조작 도움말은 기존 shell을 사용한다.
별도 대화창, 인물 대화, cutscene과 tutorial framework를 만들지 않는다.

## 2. 세 장의 내용

### 첫 점등

강변의 새 마을이 아직 전력망 밖에 있다. 플레이어는 기존 발전원에서 출발해 배전 변전소와
마을 선로를 별도로 완공하고 첫 공급기간을 결산한다.

### 두 번째 심장

마을 점등 뒤 강변 병원의 단일 경로 의존이 드러난다. 플레이어는 공간적으로 다른 주·예비
회선을 직접 만들고 단일회선 제거와 고정 공간사건에서 병원 P0를 지킨다.

### 열돔 아래

병원 의무를 지킨 뒤 공장 증설로 발전용량이 부족해진다. 플레이어는 새 가스발전소를 건설·접속한
뒤 예고된 폭염 전에 노후 공장 feeder의 예방정비를 결정하고 최종 결과를 결산한다.

각 장의 결과 보고는 기존 결산·원인·현금 화면이 담당한다. 다음 장의 브리핑은 앞 장의 성공과
새 문제를 한 문장으로 연결한다. 실패하면 새 장으로 넘어가지 않고 현재 장 결과와
`Restart Chapter`를 제공한다.

## 3. 단일 데이터 권위

[`data/product-campaign-v1.json`](../../data/product-campaign-v1.json)이 장 순서와 다음 장별 필드의
유일한 machine authority다.

- `chapterId`
- `displayName`
- `briefing`
- `objective`
- `minimumStartingCashUnit`

campaign schema는 `gridworks.campaign.v2`로 올린다. scenario fixture와 save command schema는
바꾸지 않는다. campaign root hash가 바뀌므로 이전 개발 저장은 안전하게 `Continue`가
비활성화되며 migration은 만들지 않는다.

브리핑과 목표는 비어 있지 않은 한국어 문자열이어야 한다. 최소 진입현금은 첫 장에서 정확히
`0`, 둘째·셋째 장에서 양수여야 한다. runtime data에 reference 경로, support 위치, 정답 선택과
결과 문구를 넣지 않는다.

## 4. carry-over와 softlock 방지

`minimumStartingCashUnit`은 checker가 소유한 고정 reference suffix가 끝까지 현금부족 없이 가기
위한 authored threshold다. 그 reference의 peak cash deficit만 검산하며, 가능한 모든 경로의
수학적 최솟값이나 최적해라고 주장하지 않는다. 정확한 값은 campaign root 한 곳에만 저장한다.

`ProductCampaignRun`은 성공한 장 경계에서 다음 장의 최소 진입현금을 확인한다.

- 충분하면 현재 명령 수를 다음 장 checkpoint로 고정하고 장을 넘긴다.
- 부족하면 경계를 만든 settlement command는 승인된 명령 기록에 남기되 다음 장으로 넘기지 않고,
  wrapper가 현재 장과 checkpoint를 유지한 `Complete / Failure` 상태를 투영한다.
- 실패 상태에서는 다른 mutation을 받지 않고 `Restart Chapter`만 허용한다.
- 장 재시작은 현재 장 시작의 명령 prefix를 재생해 실패 표시와 그 장에서 쓴 비용을 함께 되돌린다.

실패 상태의 후속 `Execute`는 `WrongPhase`로 거부한다. save/restore는 승인된 settlement command를
재생해 같은 blocked 상태를 다시 만들며, `Restart Chapter`만 그 command를 checkpoint 뒤 suffix와
함께 제거한다.

이 검사는 ProductSession의 전력·경제 규칙을 바꾸지 않는다. 범용 해법 탐색, 동적 난이도,
자동 환불과 완공 자산 철거를 만들지 않는다. 실패 상태와 blocked target은 campaign wrapper가
소유하고 save replay에서도 동일하게 재현한다.

## 5. 화면 연결

기본 `ProductMain` 장면과 기존 작업 패널·shell만 사용한다.

- header는 현재 장 표시명을 보여준다.
- 각 장의 첫 planning phase는 현재 `chapters[index]`의 briefing과 objective를 phase 안내 앞에
  보여준다.
- 마지막 장 전까지 `chapters[index + 1].minimumStartingCashUnit`을 `다음 장 진입 조건`으로
  표시하고 마지막 장에는 준비금 문장을 표시하지 않는다.
- 준비금 부족 실패는 필요한 금액과 `Restart Chapter` 경로를 설명한다.
- 최종 장은 기존 폭염 결과·원인·현금 보고를 그대로 사용한다.

브리핑을 보기 위한 별도 scene, modal stack, narration system과 localization framework는 만들지 않는다.
현재 조작 도움말과 keyboard focus를 보존한다.

## 6. 느슨한 완료조건

- strict campaign v2 loader와 field·type·순서 검사
- 현재 reference full completion이 세 장을 순서대로 지나 최종 성공하는지 한 번 확인
- 두 장 경계에서 authored threshold의 고정 reference suffix는 성공하고 `threshold - 1`의 같은
  suffix는 명시적 실패가 되며, mutation이 막히고 `Restart Chapter`가 해당 장 시작 상태를
  복원하는 대표 검사
- campaign carry-over, checkpoint, save/restore snapshot equality와 중복결산 거부 회귀
- 누적 ProductChecks와 Game build
- 기존 1280×720 full native smoke 한 번에서 세 장 header·시작 briefing, 장 전환과 최종 결과 확인
- 눈에 띄는 clipping·focus 확인과 짧은 독립 검토 한 번

새 사람·LLM playtest, 여러 경로 조합 탐색, 두 번째 해상도, 새 golden screenshot과 balance tuning은
하지 않는다. 외부 캠페인 관찰은 전체 개발 뒤 테스트로 미루고 `HumanValidationStatus =
NOT_COLLECTED`를 유지한다.

## 7. 제외

- 새 전력·경제 mechanic, 별도 장 fixture와 chapter scene
- 대화 tree, 인물 연출, cutscene, voice-over
- challenge mode, achievement, 여러 save slot
- 일반 feasibility solver와 자동 경로 탐색
- Stage 7의 원본 아트·VFX·SFX·음량·package

## 8. 현재 검사와 종료 기록

구현과 검토가 끝날 때 campaign v2 identity, 누적 검사, full native 결과, blocked/restart 대표 증거와
독립 검토 결과를 기록한다. 이 단계 완료는 2D 표현·사운드·패키징을 승인하지 않는다.
