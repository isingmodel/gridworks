# 현재 작업 범위

## 상태

**문서 온보딩 정리 scope가 활성 상태다.**

새 agent가 저장소에 들어왔을 때 제품 상태, 변경 권한, 질문별 권위와 작업 종료 조건을 추측하지 않고
짧은 경로로 찾게 만드는 것이 이 scope의 단일 결과물이다. 제품 코드와 current R2의 완료선은 바꾸지
않는다.

## 단일 권위

- 작업 절차와 handoff 규칙: 새 `AGENT_GUIDE.md`
- 문서별 질문 소유권과 전체 지도: `README.md`
- 제품·실행의 현재 사실: 루트 `README.md`
- current R2 코드 ownership: `ARCHITECTURE.md`

같은 설명을 여러 문서에 복제하지 않고, 각 문서는 위 소유 문서로 연결한다.

## 결과물

1. `AGENTS.md`, 루트 `README.md`, `README.md`가 같은 첫 읽기 순서와 권위 규칙을 가리킨다.
2. 새 `AGENT_GUIDE.md`가 작업 시작 판단, active scope 작성, 최소 검증, commit·review·handoff와 금지된
   추론을 한 곳에서 설명한다.
3. 문서 지도는 각 질문의 단일 소유자와 해당 문서를 언제 갱신하는지 명확히 구분한다.
4. 현재 상태와 과거 이력을 복제하지 않고 링크로 연결한다.

## 범위 밖

- gameplay, UI, save/settings/audio, data, build와 package 도구 변경
- 외부 release gate 실행, 공식 점수, 사람·device 검수
- current R2 목표나 완료 주장 변경
- push, PR, merge

## 완료 검사

- 첫 읽기 순서와 권위 순서가 `AGENTS.md`, 루트 `README.md`, `README.md`에서 충돌하지 않는다.
- 모든 Markdown 상대 링크가 존재하며 `git diff --check`가 통과한다.
- 문서별 소유권 표와 작업 시작/종료 checklist를 따라 새 agent가 추가 추론 없이 다음 행동을 고를 수 있다.
- docs-only 변경 뒤 exact source identity가 어긋나지 않도록 final clean commit에서 candidate와 combined 2B를
  다시 만들고 fresh verify한다.
- current-state 문서를 갱신하고 이 scope를 닫는다.
