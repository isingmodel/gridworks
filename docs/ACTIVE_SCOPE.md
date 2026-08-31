# 현재 작업 범위

## 상태

**관리형 최소 검사 진입점 scope가 활성 상태다.**

## 단일 결과물

개발자는 전체 `./dev check`를 실행하지 않고도 `./dev check realtime [SUITE]` 또는
`./dev check commercial [SUITE]`로 해당 관리형 검사 전체나 exact suite 하나를 즉시 실행할 수 있다.

## 단일 권위

- 개발 검사 명령 형태와 routing: `dev`
- 각 executable이 허용하는 exact suite 목록: 해당 `Gridworks.*Checks` suite registry

## 범위 안

- existing Realtime exact-suite selector를 `./dev`에 노출한다.
- CommercialChecks에 같은 fail-closed exact-suite selector를 추가한다.
- 인자 없는 `./dev check`의 전체 회귀 계약을 유지한다.
- 새 최소 검사 명령과 경계를 실행 안내에 기록한다.

## 범위 밖

- Core·게임 규칙, 콘텐츠, presentation, UI, input, save, settings와 audio 변경
- Godot UI phase·product lifecycle scenario의 선택 실행과 검사 삭제·병렬화
- build configuration, package, qualification, 외부 device·사람 gate 변경
- push, PR, merge와 배포

## 완료 검사

- Realtime와 Commercial의 exact suite 하나가 각각 1 suite만 실행하고 PASS한다.
- 알 수 없는 suite와 잘못된 명령 형태가 상태 변경 없이 fail-closed하고 허용 목록을 보여 준다.
- 인자 없는 `./dev check`가 기존 전체 관리형·Python·Godot 회귀를 모두 통과한다.
- 관련 실행 문서와 `git diff --check`가 현재 명령 형태에 일치한다.
