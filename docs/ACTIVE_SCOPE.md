# 현재 작업 범위

## 상태

**safe-point reset scope는 완료되어 닫혔다. 현재 활성 구현 scope는 없다.**

non-saveable product 상태의 정상 tree exit는 직전 valid save bytes를 그대로 보존하고, 다음 saveable 정상
exit만 primary를 current v3로 갱신한다. valid in-progress와 raw bytes를 읽을 수 있는
invalid/unsupported/source/replay save는 기존 `새 게임`의 확인→unique raw-byte sibling backup→canonical
`ProductCampaign` 시작 경로를 공유한다. I/O failure는 두 action을 차단하고, backup failure는 confirm
action·Continue 가능성/continuation·ownership·primary를 보존한 채 실패 이유를 표시한다.

focused fresh-process product-entry 연쇄, `./dev check`와 두 독립 review를 통과했다. 세부 완료 이력은
[UX-R2.19](archive/COMPLETED_HISTORY.md)가 소유한다.

## 구현 권한

새 구현은 아직 열리지 않았다. 최신 사용자 지시의 “가장 구현하기 쉬운 방향으로 전체 목표를 빠르게
구현”은 다음 scope의 우선순위를 정하지만, [남은 작업](NEXT_TASKS.md)의 항목 자체가 구현 권한을 만들지는
않는다. 다음 작업은 먼저 이 문서에 결과물·범위 밖 항목·완료 검사를 적은 뒤 시작한다.

## 유지할 경계

- transient pending/general queued/finale/epilogue cursor 또는 새 save schema를 추가하지 않는다.
- backup browser/restore/delete UI와 save migration은 현재 구현 사실이 아니다.
- deterministic PASS를 packaged 전체 캠페인 E2E, 사람 UX 품질 또는 출시 승인으로 확대하지 않는다.
- push, PR, merge는 현재 범위 밖이다.
