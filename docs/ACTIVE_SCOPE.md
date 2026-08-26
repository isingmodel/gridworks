# 현재 작업 범위

## 상태

**활성 구현 scope가 없다.**

current R2 repository completion closure를 완료했다. 저장소가 소유하는 목표는 실시간
8장·finale/epilogue, product save/settings/audio wiring, internal macOS package identity와 combined 2B를
포함하는 **current R2 내부 후보**다. [남은 구현 작업](NEXT_TASKS.md)은 현재 비어 있다.

## 완료한 구조 정리

- actual campaign의 active risk area는 `Storm`, thermal-limit override는 `Heat`, 그 외는
  `Clear`로 world에 표현된다. 복합 flood가 우선한다.
- `Reduce Motion`이 켜지면 비·폭우의 minute-driven weather phase가 고정된다.
- current `./dev`·package·combined 2B와 연결되지 않은 historical editor-native non-score
  evaluator 30개 파일 26,732줄을 제거했다. Git 이력이 과거 기준선을 보존한다.
- 실제 device·사람·score-bearing model receipt·Developer ID·공개 승인은
  [외부 출시 gate](RELEASE_GATES.md)에 `NOT_RUN | PENDING | NOT_APPROVED`로 분리했다.
- Debug build, `./dev check`, Markdown 링크 감사와 두 bounded independent review가 통과했고
  actionable finding을 모두 수정했다. final closure commit의 candidate/qualification을 재생성·
  fresh verify하는 것이 마지막 evidence gate다.

## 현재 경계

- current R2 내부 후보 완료는 authored 8장의 packaged action-by-action E2E, engine `user://`
  전체 격리, OS hardware input, 실제 display·audio device·speaker를 뜻하지 않는다.
- 사람 UX·미감·접근성, 한국어·전력설비 전문 검토, `CommercialUXProxy`,
  Developer ID·공증·공개 배포는 완료하지 않았다.
- push, PR, merge는 수행하지 않았다.

새 구현은 외부 gate에서 재현 가능한 결함이 발견되거나 사용자가 새 제품 목표를
명시한 뒤, 이 문서에 하나의 결과물·범위 밖·완료 검사를 적어야만 다시 연다.
`NEXT_TASKS.md`와 `RELEASE_GATES.md`는 자동으로 구현·평가·배포 권한을 만들지 않는다.
