# 출시판 재구축 후보 — 공식 cold LLM 관찰 01

> 상태: `COMPLETED`
>
> 결과: `BLOCKED`
>
> 권한 근거: 2026-08-18 사용자 요청 “LLM agent로 하여금 정식 테스트르류시켜봐.”
>
> 해석 상한: 현재 출시판 후보에 대한 cold LLM 1회다. 사람 검증, 성공률, 사용성·재미·밸런스
> 판정이나 aggregate gate가 아니다.

## 1. 목적과 고정 조건

현재 `ReleaseMain` 출시판 후보를 처음 보는 LLM 한 명이 실제 native 창만 사용해 타이틀부터
캠페인 종단까지 자력으로 진행할 수 있는지 관찰한다. 자동검사가 이미 증명한 전력망 계산을 다시
채점하지 않고, 참가자가 화면에서 이해한 규칙·선택과 실제로 막힌 지점을 기록한다.

- 관찰 ID: `RELEASE-REBUILD-L01`
- 실행 횟수: 정확히 1회
- 참가자: `gpt-5.6-sol`, reasoning `ultra`, fresh subagent, fork `none`
- 시작 화면: 이미 열린 `Gridworks` 타이틀. 이전 버전의 호환되지 않는 저장 때문에 `이어하기`는
  비활성이었으며 참가자는 `새 게임`으로 시작했다.
- 조작 경계: Computer Use로 이미 열린 native 앱만 사용
- 허용: 현재 선로 초안의 `취소`·`되돌리기`
- 금지: 임무 재시작, 이전 임무 되감기, reload, 두 번째 시도, facilitator help, follow-up
- 금지된 정보: 저장소, 일반 filesystem, shell, web, source, data, 로그, 이전 대화와 검사 자료

## 2. 고정 실행물

- Git commit: `e8f464adfc7aed381c201090da27dffbf67b9d40`
- ZIP: `dist/Gridworks-macOS-0.1.0.zip`
- ZIP 크기: `124,447,595 bytes`
- ZIP SHA-256: `218bcd436b34d417c19f553b2e748fbcf68f0823eba7bd1fb3862e9e06aa0e2d`
- world SHA-256: `d1b22271be87ac598d9e7b86123e6a5cc67dde43ef7884fa635bc73a311288e8`
- campaign SHA-256: `32e9f3285b7547c6aa2f1895e294e618106aae02c685cf13337b5eb3da2d65b8`
- 실행 앱: `/private/tmp/gridworks-owner-rc.Hc92BX/Gridworks.app`

위 ZIP 경로는 관찰 당시 위치다. 같은 파일명에 후속 후보가 만들어져도 이 관찰의 실행물은 이 절의
크기·SHA-256과 아래 byte 비교 기록으로 식별한다.

실행 뒤 앱 executable, PCK, arm64·x86_64의 `Gridworks.Core.dll`과 `Gridworks.Game.dll` 여섯 파일을
ZIP 안의 대응 파일과 byte 단위로 비교했고 모두 일치했다. 따라서 이 관찰은 위 ZIP과 같은 실행
바이트를 사용했다.

## 3. 참가자에게 전달한 단일 prompt

아래 메시지 한 건만 전달했고 follow-up은 보내지 않았다.

```text
You are participant RELEASE-REBUILD-L01 in one fixed official cold LLM observation.

Use only the already-open Gridworks native window at /private/tmp/gridworks-owner-rc.Hc92BX/Gridworks.app through Computer Use. You may read only the Computer Use skill instructions needed to operate that window. Do not inspect the repository, filesystem, shell, web, logs, source, data, prior conversation, or any test material. Do not ask for help and do not accept follow-up instructions.

Starting from the title screen, play one uninterrupted attempt until the campaign reaches a clear terminal result or you are genuinely blocked. Choose every placement and decision yourself from information visible in the game. Normal in-game Undo or Cancel is allowed, but do not restart a mission, rewind to an earlier mission, reload, or begin a second attempt.

When you stop, report only:
1. whether you reached a terminal result and what it visibly said;
2. the main rules, state changes, and tradeoffs you inferred from the game;
3. the decisions you made and why;
4. any point where the interface or wording confused or blocked you.
```

## 4. 판정

- `TerminalResult = BLOCKED`
- `NativeCompletion = false`
- `CandidateIdentityValid = true`
- `SaveEvidenceValid = true`
- `FacilitatorHelp = false`
- `FollowUpCount = 0`
- `RestartOrRewind = false`
- `OfficialObservationsExecutedForThisRelease = 1`
- `AggregateDecision = NOT_APPLICABLE`
- `HumanValidationStatus = NOT_COLLECTED`
- `ReleaseOwnerPlayReviewStatus = NOT_COLLECTED`

참가자는 프롤로그 3개와 본편 4개를 통과했다. 본편 다섯 번째이자 전체 여덟 번째 임무
`청류 비상전력`에서 정상 공급 `5 MW / 8 MW`, 남은 현금 `16.4만원` 상태로 더 진행할 수 없다고
판단했다. 화면의 마지막 실패 설명은 다음과 같았다.

> 강변 산업단지에 전력이 공급되지 않습니다. 서부 발전소의 공급 여력이 3 MW 부족합니다.
> 다른 발전원을 연결해 공급을 분담하세요.

참가자는 남부 발전소에서 산업 망으로 여러 경로를 만들었지만 해당 경로가 회색으로 남고 급전에
사용되지 않는 이유를 화면에서 판별하지 못했다고 보고했다. 남은 현금으로는 유의미한 추가 공사를
할 수 없었고, 금지된 재시작·이전 임무 되감기를 사용하지 않은 채 관찰을 끝냈다.

## 5. 원본과 실행 증거

### 5.1 platform original

- platform session ID: `01a011c3-689b-7e80-a8e7-b6a7515b6a0d`
- platform original:
  `/Users/fred/.codex/sessions/2026/08/18/rollout-2026-08-18T07-06-54-01a011c3-689b-7e80-a8e7-b6a7515b6a0d.jsonl`
- platform original SHA-256:
  `6c4b79558eca0bf687b717a7fa37d3bd568e4f3a9b6c2f2171f4eb9649292455`
- session 시작: `2026-08-17T22:06:54.164Z`
- 최종 보고: `2026-08-17T23:26:14.711Z`
- 실제 설정: `gpt-5.6-sol`, reasoning `ultra`, fresh subagent, fork `none`

원본에는 user task 1건, `task_started` 1건, `task_complete` 1건과 follow-up 0건이 있다. 참가자는
Computer Use skill 설명만 두 번 읽었고, 나머지 관찰·조작은 도구 발견 2회와 Computer Use
`node_repl` 호출 268회로 이루어졌다. 다른 shell 명령, 저장소·source·data·로그·web 열람은 없다.
전달 task 본문은 참가자와 coordinator의 platform original에서 암호화된다. 이 문서 §3은 실행 뒤
coordinator가 실제 전달 내용을 보존한 사후 전사본이며, 암호화된 원본만으로 plaintext의 byte 단위
동일성을 독립적으로 다시 확인할 수는 없다. platform original은 단일 task, follow-up 0건과 실행
provenance를 보존한다.

### 5.2 공식 저장

- 공식 save:
  `playtests/release-2d/private/official-release-rebuild-llm-01/official-userdata/release-campaign-save-v2.json`
- save SHA-256:
  `72b84d305e0516a84bf7c0f4e3b04252c97119fd84f8c6d5c6b8e59204b209ea`
- settings:
  `playtests/release-2d/private/official-release-rebuild-llm-01/official-userdata/settings.json`
- settings SHA-256:
  `437895e5581cf52d6ca55348c834554fcecb7bd990468470481490dfed7c44cb`

save는 현재 campaign·world ID와 SHA-256을 그대로 기록하고 JSON 파싱에 성공했다. accepted command는
183개이며 그중 `evaluateChapter`는 7개다. 실패한 마지막 평가 명령은 accepted journal에 들어가지
않으므로, 이 수는 첫 일곱 임무 완료 후 여덟 번째 임무에 남았다는 참가자 보고와 일치한다.

| accepted command | 수 |
|---|---:|
| 선로 시작 | 35 |
| 선로 점 추가 | 81 |
| 점 되돌리기 | 2 |
| 초안 취소 | 12 |
| 선로 발주 | 23 |
| 공사 완료 | 23 |
| 임무 평가 통과 | 7 |

일반 패키지 창을 기존 타이틀에서 사용했으므로 별도 app JSONL이나 engine log를 만들지 않았다.
후보 동일성은 ZIP과 실행 앱의 핵심 파일 byte 비교로, 진행 상태는 콘텐츠 hash를 포함한 공식 save와
platform original로 확인한다. 실행 전의 호환되지 않는 과거 save는 앱이 `.bak`으로 보존했으며 이
관찰의 공식 데이터로 복제하지 않았다.

## 6. 참가자가 이해한 규칙과 선택

참가자는 화면만으로 다음을 설명했다.

- 선로는 기존 설비나 전신주를 endpoint로 직접 골라야 접속되며, 시각적으로 교차하는 것만으로는
  합류하지 않는다.
- 한 span은 격자 4칸까지이고 endpoint마다 회선 수 한도가 있다.
- 공사는 현금과 시간이 들며 완공 전 선로는 전기를 전달하지 않는다.
- 일반 선로는 싸고 빠르지만 이번 폭염에서 `2.5 MW`로 감액되고, 보강 선로는 비싸지만 더 큰 용량을
  전달한다.
- 임무 평가는 평상시와 예고 사고 상태를 함께 확인한다. 어떤 장에서는 필수 수요를 지키면서 일반
  수요 제한을 허용한다.
- 이중화는 선이 다르게 보이는 것만으로 부족하며 공통 병목과 사용 불가 설비를 함께 피해야 한다.

실제 플레이에서는 범람구역을 피한 첫 서부–동부 선로, 명시적으로 합류시킨 북쪽 분기, 공간적으로
나눈 의료원 두 경로, 남부 발전소와 남부 분기, 정수장 접속을 만들었다. 공유 구간과 범람 예보는
앞서 만든 이중화를 재사용해 추가 공사 없이 통과했다. 계획 정지에서는 일반 병렬 경로가 병목을
풀지 못하자 보강 남부–동부 우회로를 만들었다. 마지막 임무에서는 보강 남부–의료원 비상선과 별도
정수장 접속으로 예고 상황 목표 `2.1 / 2.1 MW`를 먼저 만족한 뒤 평상시 부족을 해소하려고 서부·북안
우회와 남부 전원 접속을 추가했다.

## 7. 관찰된 혼란과 개선 질문

다음은 참가자 1명의 보고다. 알려진 기술 결함이나 사람 사용성 결론으로 확정하지 않는다.

1. 아래쪽 작업 버튼이 화면 밖으로 잘려 마우스로 발견하지 못했고 `Tab` 초점 이동으로 조작했다.
   기존 네 배치 증거와 다른 실제 창 상태에서 나온 보고이므로, 다음 확인에서는 창 크기와 패널
   scroll affordance를 함께 본다.
2. 공사 완료 뒤에도 선로 계획 상태가 현황 화면과 비슷하게 남아, 숨은 조작 버튼이 예상 밖 평가를
   실행하는 것처럼 느껴졌다.
3. endpoint의 회선 슬롯이 가득 찼다는 사실을 endpoint를 실제로 고른 뒤에야 알 수 있었다.
4. 남부 발전소에 여섯 연결이 보이는데도 여러 경로가 회색으로 남았다. 화면은 “다른 발전원을
   연결”하라고 했지만 어떤 구간·설비·우선순위 때문에 그 발전원이 부하까지 도달하지 못했는지
   설명하지 않았다.
5. 마지막 임무에서 원인을 알아내기 전에 현금을 거의 소진했다. 이것이 밸런스 문제인지, 경로 설명
   문제인지, 참가자의 선택 문제인지는 이 한 번으로 구분할 수 없다.

가장 직접적인 다음 질문은 **이미 연결돼 보이는 발전원이 왜 선택한 부하에 급전되지 않는지 화면이
첫 차단 설비와 남은 용량으로 설명하는가**이다. 작업 영역의 스크롤·초점 가시성과 현재 조작 상태도
같이 확인할 가치가 있다. 이 관찰만으로 수치, 비용, 망 규칙이나 UI를 자동 변경하지 않는다.

## 8. 해석 상한

이 한 번은 다음 사실만 지지한다.

- 이 LLM은 도움·재시작·되감기 없이 현재 후보의 첫 일곱 임무를 통과했다.
- 분기·합류, span, 접속 한도, 공사 상태, 일반·보강 선로, 평상시·사고 평가와 공통 병목을 설명했다.
- 마지막 임무에서는 연결돼 보이는 남부 발전원이 실제 급전에 쓰이지 않는 이유를 이해하지 못해
  캠페인을 끝내지 못했다.

사람의 이해도·접근성·재미·밸런스, 일반적인 성공 가능성, 평균 플레이 시간, 다른 전략의 실행
가능성은 증명하지 않는다. `ReleaseOwnerPlayReviewStatus`와 `HumanValidationStatus`는 계속
`NOT_COLLECTED`이며, 전체 목표나 4단계를 완료로 바꾸지 않는다.
