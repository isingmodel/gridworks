# 현재 작업 범위

## 상태

**프로젝트 Godot Editor UI skill scope가 활성 상태다.**

## 단일 결과물

Agent가 Gridworks의 실제 Godot Editor 개발 UI를 열고 scene을 직접 편집·저장한 뒤 normal game에서
재현을 검증하는 프로젝트 로컬 skill을 제공한다.

## 단일 권위

- Godot Editor UI 작업 절차와 trigger: `.agents/skills/godot-editor-ui/SKILL.md`

## 범위 안

- 프로젝트 로컬 skill과 Codex UI metadata를 만든다.
- `./dev play layout`, actual Editor 판별, Scene tree·2D·Inspector 편집, `⌘S`, normal game 검증 절차를 고정한다.
- Gridworks visual-layout 불변조건, UI 자동화 안전 규칙과 Mac 절전 방지 요청 처리법을 기록한다.
- 프로젝트 작업 안내에서 skill의 canonical 위치만 연결한다.

## 범위 밖

- 현재 게임의 scene·art·배치·규칙·runtime 코드는 바꾸지 않는다.
- 전역 skill 설치, plugin 제작, Godot Editor plugin·툴 스크립트 추가는 하지 않는다.
- 사람 미감 승인, package/release gate, push·PR·merge·배포는 수행하지 않는다.

## 완료 검사

- `quick_validate.py`로 skill 구조와 frontmatter를 검증한다.
- placeholder·절대경로·trigger·실제 project command와 scene authority를 정적 점검한다.
- 상대 링크와 `git diff --check`를 확인하고 실제로 바뀐 workflow 소유 문서만 갱신한다.
