# Agent 작업 안내

이 문서는 Gridworks에서 **어떤 순서로 판단하고 작업을 끝내는지**만 소유한다. 제품의 현재 사실은
[루트 README](../README.md), 지금 허용된 변경은 [현재 작업 범위](ACTIVE_SCOPE.md), 코드 ownership은
[개발 구조](ARCHITECTURE.md), 질문별 문서 소유자는 [문서 지도](README.md)를 따른다.

## 1분 온보딩

다음 순서를 바꾸지 않는다.

1. [루트 README](../README.md)의 `30초 현재 상태`와 실행·검증 경계를 읽는다.
2. [현재 작업 범위](ACTIVE_SCOPE.md)에서 활성 scope와 범위 밖 항목을 확인한다.
3. 이 문서에서 작업 절차와 종료 조건을 확인한다.
4. [문서 지도](README.md)에서 질문의 단일 소유 문서를 고른다.
5. 코드를 바꿀 때만 [개발 구조](ARCHITECTURE.md)의 ownership 표와 해당 변경 경로를 읽는다.

읽기·설명·진단 요청은 관련 문서를 확인하고 read-only 증거로 답한다. 파일을 변경하는 요청은 현재 사용자
지시가 허용한 결과물 하나를 `ACTIVE_SCOPE.md`에 먼저 연다. 외부 평가·device 검수·배포와 원격 저장소
write는 각각 명시적 사용자 권한이 있어야 한다.

## 권위와 질문 소유권

문장이 충돌하면 다음 순서로 판단한다.

```text
현재 사용자 지시
→ ACTIVE_SCOPE.md의 결과물·범위 밖·완료 검사
→ 루트 README.md의 현재 제품·실행 사실
→ docs/README.md가 질문별로 지정한 소유 문서
→ archive/COMPLETED_HISTORY.md
→ Git의 과거 문서
```

상위 문서는 변경 권한을 정하고, 질문 소유 문서는 세부 사실을 정한다. 예를 들어 사용자와 active scope가
평가 작업을 허용해도 평가 프로토콜의 hard gate가 없으면 공식 점수는 만들 수 없다. 반대로
`NEXT_TASKS.md`에 항목이 있거나 과거 구현이 PASS였다는 사실만으로 변경 권한이 생기지 않는다.

## 작업 시작

변경 전에 다음 네 가지를 확인한다.

- `git status --short`로 기존 사용자 변경을 식별하고 보존한다.
- 결과물을 한 문장으로 말할 수 있는지 확인한다. 서로 독립인 결과는 같은 scope에 묶지 않는다.
- 상태·규칙·콘텐츠·표현·입력·저장·설정·오디오·패키지 중 단일 data authority를 정한다.
- 자동 검사가 관찰할 사실과 사람이/device가 관찰해야 할 사실을 구분한다.

활성 scope가 없다면 다음 최소 형태로 `ACTIVE_SCOPE.md`를 열고 별도 commit으로 고정한다.

```markdown
# 현재 작업 범위

## 상태
**<결과물 이름> scope가 활성 상태다.**

## 단일 결과물
<플레이어 또는 개발자가 얻는 결과 한 문장>

## 단일 권위
- <바뀌는 사실>: `<authority>`

## 범위 안
- <필요한 변경>

## 범위 밖
- <이번에 하지 않을 인접 작업>

## 완료 검사
- <가장 작은 결정론적 검사>
- <필요한 통합 검사와 문서 갱신>
```

기존 active scope와 새 요청이 다르면 조용히 확장하지 않는다. 기존 scope를 완료하거나 사용자에게 충돌을
알린 뒤 하나의 결과물로 다시 연다.

## 구현 순서

1. 질문 소유 문서와 코드 authority에서 현재 사실을 재현한다.
2. 가장 가까운 unit/story/checkpoint 검사로 실패 또는 불변조건을 먼저 고정한다.
3. authority 한 곳을 바꾸고 adapter·presentation은 typed fact만 전달하게 둔다.
4. 범위에 맞는 가장 작은 검사부터 실행하고 필요한 통합 검사까지 넓힌다.
5. 주요 단위를 commit한다. unrelated 변경을 함께 stage하지 않는다.
6. 가능한 경우 bounded independent review를 한 번 받고, scope 안의 actionable finding만 수정한다.
7. 실제로 바뀐 사실의 소유 문서만 갱신한다. 실행 로그나 commit SHA를 current 문서에 복제하지 않는다.
8. `ACTIVE_SCOPE.md`를 닫고 완료 이력에는 장기적으로 필요한 결과와 한계만 요약한다.

새 계층이나 추상화를 먼저 추가하지 않는다. 기존 변경 경로가 여러 authority에 같은 결정을 반복할 때만
ownership을 옮기거나 합친다. 파일 수보다 “변경 이유 하나가 authority 하나로 이어지는가”를 기준으로
판단한다.

## 변경별 최소 검증

| 변경 | 먼저 실행 | 완료 전 확인 |
|---|---|---|
| 문서만 변경 | 상대 링크, 소유권·용어 충돌, `git diff --check` | current 사실을 바꿨다면 해당 소유 문서; exact candidate가 HEAD를 주장하면 재생성·verify |
| Core 규칙·data | 가장 가까운 Core suite와 accepted/rejected case | `./dev check` |
| story·chapter 연결 | selector와 해당 chapter/누적 route | `./dev check` |
| presentation·UI·input | owning presenter/router와 named checkpoint | `./dev check`; 물리 표시 주장은 별도 non-headless/device gate |
| save·settings·audio | 해당 codec/store/session 또는 audio wiring smoke | `./dev check`; package 주장은 exact candidate/qualification |
| package identity | `./dev candidate build` | candidate verify, qualification run/verify와 clean exact source commit |
| 사람·device·공식 평가·출시 | [외부 출시 gate](RELEASE_GATES.md)의 선행 조건 | 해당 owner의 증거와 승인; 자동 PASS로 대체 금지 |

전체 명령과 경계는 [실행 안내](../INSTALL.md)가 소유한다. `./dev check`는 기본 통합 회귀지만 모든 작업의
첫 진단 명령은 아니다. 가장 가까운 실패를 먼저 보고, 시작 경로 자체가 검증 대상일 때만 긴 E2E를 쓴다.

## 문서 갱신 규칙

- 같은 사실을 여러 문서에 설명하지 않고 [문서 지도](README.md)의 소유 문서 한 곳을 고친다.
- 다른 문서에는 한 문장 요약과 상대 링크만 둔다.
- current 문서에는 현재 사실과 재현 방법만 남긴다. 과거 scope, 긴 실행 로그와 commit 영수증은 Git에 둔다.
- 완료 task는 [완료 이력](archive/COMPLETED_HISTORY.md)에 짧게 압축하고, backlog나 current 상태처럼 쓰지
  않는다.
- 증거의 상한을 함께 적는다. `authored`, `native implemented`, `direct-play observed`, package, 사람 QA는
  서로 대체되지 않는다.

## 금지된 추론

- 준비된 코드, backlog, release gate 또는 과거 scope가 존재한다 → 지금 구현해도 된다.
- headless·결정론적 검사가 통과했다 → 실제 display, input, speaker 또는 사람 UX가 통과했다.
- package identity나 bounded qualification이 통과했다 → 전체 8장 production-input 여정이 관찰됐다.
- 콘텐츠가 authored다 → native route에서 도달하거나 직접 플레이로 확인됐다.
- 사용자가 로컬 변경을 요청했다 → push, PR, merge나 공개 배포도 허용됐다.
- 구조를 단순화한다 → 새 facade, service, schema 또는 미래용 platform을 미리 만들어도 된다.

세부 용어의 정확한 뜻은 [문서 지도](README.md)의 `오해를 막는 용어`가 소유한다.

## 종료와 handoff

작업을 넘기기 전에 다음 상태를 한 번에 확인할 수 있어야 한다.

- `git status --short`: 남은 변경과 그 소유자가 분명하다.
- 결과물: active scope의 단일 결과물이 실제로 동작하거나 문서화됐다.
- 검증: 실행한 명령, 결과와 실행하지 않은 물리·사람 gate가 구분된다.
- 문서: current 사실은 질문 소유 문서에 있고, 다른 문서는 그곳을 링크한다.
- 이력: 주요 단위와 scope closure가 commit돼 있으며 unrelated 파일이 섞이지 않았다.
- 원격 작업: push·PR·merge 여부가 사용자 권한과 일치한다.

최종 handoff에는 결과, 검증, 남은 외부 gate와 원격 write 여부만 보고한다. 긴 작업 일지는 반복하지 않는다.
