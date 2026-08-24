# 과거 문서 안내

현재 문서 트리에는 완료 task의 짧은 요약만 둔다.

- 완료·중단된 단계: [COMPLETED_HISTORY.md](COMPLETED_HISTORY.md)
- 당시의 상세 scope와 체크리스트: Git 이력
- 실행·관찰 artifact: 저장소 `playtests/`

과거 문서를 현재 `docs/`에 복원하거나 현재 계획과 섞지 않는다. 필요하면 `git log --all -- <path>`와
`git show <revision>:<path>`로 읽는다. 과거 scope가 존재하거나 PASS였다는 사실은 현재 구현 권한이나
현재 제품 품질 증거가 아니다.

완료 단계가 추가될 때는 assertion 수와 긴 실행 로그를 복제하지 않고 플레이어 결과, 증거의
종류와 명확한 한계만 [완료 이력](COMPLETED_HISTORY.md)에 기록한다.
