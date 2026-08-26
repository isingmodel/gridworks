# 현재 작업 범위

## 상태

**safe-point reset scope가 활성화됐다.**

exact completed save의 title은 기존 `이어하기`와 `새 게임`을 함께 제공한다. `새 게임`은 별도 replay
구조 없이 canonical `ProductCampaign`을 `FIRST_LIGHT` initial briefing에서 시작하며, 선택만으로 terminal
bytes를 바꾸지 않는다. 저장 가능한 지점의 정상 종료가 same slot을 current-v3 진행으로 교체하고 fresh
`이어하기`가 이를 복원한다.

남은 제품 seam은 두 가지지만 한 reset lifecycle로 닫을 수 있다. transient story/finale 구간은 새 cursor를
만들지 않고 직전 safe save를 보존해야 한다. valid in-progress 또는 읽을 수 있는 blocked save에서 처음부터
시작하려면 원본을 보존하는 명시적 확인 경로가 필요하다.

## 결과물

- non-saveable transient 구간의 정상 tree exit는 기존 valid save bytes를 그대로 둔다. 이후 saveable
  지점의 정상 exit만 primary slot을 current v3로 갱신한다.
- valid in-progress와 읽을 수 있는 invalid/unsupported/source/replay save의 title은 기존 `새 게임` action을
  reset action으로 재사용한다.
- 첫 activation은 확인 상태만 표시하고 session/save를 바꾸지 않는다. 두 번째 activation은 원본을 unique
  sibling backup으로 복사한 뒤 canonical 새 게임을 시작한다.
- backup 실패 또는 I/O-failure save는 fail-closed하며 primary bytes와 기존 title 상태를 보존한다.

## 구현 범위

- `RealtimeSliceMain`의 작은 typed reset eligibility/confirmation state와 기존 New Game routing
- `RealtimeCampaignSaveStore`의 fail-closed sibling backup 한 동작
- 기존 `RealtimeProductTitle` presentation/button 재사용; 새 UI component 없음
- 기존 save/session/product-entry smoke의 대표 safe-point와 reset path 최소 확장
- 실제 변경 사실을 소유하는 current Markdown 문서

## 범위 밖

- transient pending/general queued/finale/epilogue cursor 또는 새 save schema
- save migration, backup browser/restore/delete UI
- completed save의 즉시 New Game 정책 변경
- explicit chapter/through/fixture의 product persistence
- settings/audio, package, 공식 평가와 사람 UX 판정
- push, PR, merge

## 완료 검사

- 대표 non-saveable exit가 이전 valid bytes를 byte-exact 보존하고 다음 safe-point exit가 이를 갱신한다.
- valid in-progress reset은 첫 activation에서 bytes/session 불변, 두 번째 activation에서 byte-exact sibling
  backup 생성→canonical initial briefing 시작→safe exit primary current-v3 교체 순서를 지킨다.
- readable invalid/unsupported/source/replay는 같은 확인·backup seam을 사용하고 I/O failure는 두 action을
  계속 차단한다.
- completed title의 immediate New Game, terminal/in-progress Continue와 missing-save New Game 회귀를 유지한다.
- 새 schema, transient cursor, UI component와 별도 test suite를 만들지 않는다.
- focused product-entry smoke, `./dev check`와 두 독립 review를 통과한다.
