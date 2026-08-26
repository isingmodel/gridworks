# 현재 작업 범위

## 상태

**current R2 packaged app-owned persistence qualification 2B1이 활성 scope다.**

[남은 작업](NEXT_TASKS.md)의 큰 2B를 가장 구현하기 쉬운 두 단위로 나눈다. 이번 2B1은 exact package가
별도 빈 Gridworks-owned data root에서 save/settings를 찾고 fresh process마다 같은 bytes를 읽는지만
소유한다. default Godot engine `user://` 전체나 물리 입력을 격리했다고 주장하지 않는다.

## 결과물

- `RealtimeSliceMain`의 한 release-safe environment seam이
  `GRIDWORKS_R2_QUALIFICATION_USER_DATA_DIR`의 기존 absolute non-symlink directory 아래에 current save와
  settings 두 fixed filename을 함께 둔다. env가 없으면 기존 `user://` 동작은 byte-for-byte 유지한다.
- product title은 qualification env가 유효할 때만 app user argument 수, settings load와 continuation
  분류를 machine-readable marker 하나로 출력한다.
- 단일 `tools/r2_qualification.py`가 `run MANIFEST | verify RECORD`를 소유하고 `./dev qualify`가 전달한다.
  기존 `r2_candidate.py`의 manifest/archive verifier와 extractor를 재사용하며 packager를 복제하지 않는다.
- strict canonical qualification record 하나가 source/package/tool identity와 exact-empty root, no-user-arg
  packaged boot, settings loaded, initial progress loaded, terminal completion loaded stage를 결속한다.

## 범위 밖

- Godot engine `user://` 전체 격리, 새 macOS account, `HOME` 치환, `_sc_`/override.cfg
- packaged New Game/Continue/settings를 누르는 InputEvent, OS hardware keyboard/mouse와 전체 8장 journey
- non-saveable/reset/backup packaged 입력, CoreAudio device·speaker 청감과 상태 전반 audio coverage
- 사람 UX, evaluation session/score, Developer ID·공증·공개 배포
- push, PR, merge

## 완료 검사

1. invalid/relative/missing/symlink qualification root가 app에서 fail-closed하고 env 없음은 기존 `user://`
   logical path와 source-tree 회귀를 유지한다.
2. clean exact candidate를 먼저 strict verify하고 별도 extraction의 app tree identity를 실행 전후 대조한다.
3. exact-empty root에서 packaged default scene을 app user argument 없이 실행해 missing/missing marker를 얻고,
   기존 actual-scene product smoke가 만든 settings·initial save·terminal save를 fresh packaged process가 각각
   loaded/restorable/completed로 분류하는지 확인한다.
4. qualification root에는 단계별 fixed save/settings 외 파일·symlink·temp가 없고 실제 default current R2
   save/settings bytes는 실행 전후 바뀌지 않았음을 확인한다.
5. record type/key/canonical bytes와 source/package/tool/stage identity mismatch를 verifier가 거부하고,
   targeted build·qualification run/verify, `./dev check`와 두 bounded independent review를 통과한다.
