# 과거 문서 아카이브 안내

이 폴더는 현재 목표가 아닌 완료·중단 기록을 **짧게 보존**한다. 현재 규칙·권한·backlog를 찾는 곳이
아니다. 현재 문서는 상위 [문서 안내](../README.md)를 따른다.

## 보존 방식

- 현재 작업에 필요한 사실: [완료 이력](COMPLETED_HISTORY.md)
- 원래 상세 문서와 HTML/render: Git commit `9aceaf7`
- 원본 실행 입력: 저장소 `playtests/`
- release 변경 기록: 루트 `CHANGELOG.md`의 동결 항목

상세 문서를 읽어야 할 때 현재 `docs/`로 복원하지 않고 Git에서 직접 본다.

```sh
git show 9aceaf7:docs/scopes/COMMERCIAL_2D_IMPLEMENTATION.md
git show 9aceaf7:docs/scopes/REALTIME_PHYSICAL_TOTAL_REVISION.md
git show 9aceaf7:docs/DEVELOPMENT_HISTORY.md
```

파일을 실험적으로 복구해야 하면 현재 작업과 분리된 branch/worktree에서 한다. 과거 scope를 복구했다고
현재 구현 권한이 생기지 않는다.

## 압축 원칙

- 완료된 단계별 assertion 수와 긴 로그는 반복하지 않는다.
- 현재 기반을 식별하는 commit, 증거 상한, 실패·중단 상태와 유지할 교훈만 남긴다.
- 미완료 사람·전문·법적 gate는 완료로 바꾸지 않는다.
- 과거 HTML target과 concept는 구현·native·사람 evidence로 세지 않는다.
- Git 이력에 존재하는 상세 계약을 새 현재 문서에서 재서술하지 않는다.
