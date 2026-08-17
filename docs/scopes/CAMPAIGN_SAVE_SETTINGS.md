# 캠페인 골격·저장·기본 설정 — 완료된 구현 기준

> 역사적 내부 후보 기록이다. 현재 출시판 권위와 구현은 [출시판 재구축](RELEASE_REBUILD.md)이
> 소유하며, 이 문서는 새 구현을 승인하지 않는다.

> 상태: `COMPLETED`
>
> 완료 당시 후속 단계 자동 승인: `NOT_GRANTED`
>
> 사람 검증: `NOT_COLLECTED`

이 단계는 완료된 누적 제품 흐름을 세 장의 캠페인으로 감싸고 저장·재개·장 재시작과 최소 화면
설정만 추가한다. 새 전력 규칙, 새 임무 숫자, 장별 콘텐츠 다듬기, 사운드와 패키징은 열지 않는다.

## 1. 플레이어 결과

기본 실행은 다음 흐름을 제공한다.

```text
Title
├─ New Game → 첫 점등부터 시작
└─ Continue → 마지막 승인 명령 뒤의 안전 경계로 복원

플레이 중 Pause
├─ Resume
├─ Save & Quit
├─ Restart Chapter
└─ Settings / Control Help
```

현재 누적 임무의 첫 마을 결산 뒤가 `두 번째 심장`, 병원 복구·결산 뒤가 `열돔 아래`의 시작이다.
장 재시작은 현재 장의 첫 상태로 돌아가며 이전 장에서 만든 설비·현금은 그대로 이어받는다.

## 2. 지원 환경과 파일 위치

1.0의 지원 플랫폼은 `macOS arm64` 하나로 한정한다. 현재 확인 환경은 macOS `26.6.1` arm64이며,
최소 지원 OS version은 설치 package를 만드는 마지막 단계에서 실제 package로 고정한다. 그 전에는
다른 운영체제나 다른 CPU를 지원한다고 광고하지 않는다.

저장과 설정은 Godot `user://` 아래 한 슬롯만 사용한다.

- 캠페인 저장: `user://campaign-save.json`
- 화면·도움말 설정: `user://settings.json`

macOS에서는 Godot의 앱 데이터 디렉터리 아래에 놓인다. repository, fixture, 진단 로그와 저장 파일을
섞지 않는다.

## 3. 캠페인 권위

[`data/product-campaign-v1.json`](../../data/product-campaign-v1.json)이 캠페인 ID, 장 순서·표시명과
현재 scenario fixture 참조의 유일한 권위다. 정확히 다음 세 장만 선언한다.

```text
FIRST_LIGHT → SECOND_HEART → HEAT_DOME
```

캠페인 root는 완료된 [`product-heatwave-v1.json`](../../data/product-heatwave-v1.json)을 참조한다.
전력 숫자를 복제하거나 정답 경로·추천 선택을 넣지 않는다. 장별 briefing, tutorial과 별도 시작
fixture는 다음 콘텐츠 고정 단계가 소유한다.

Loader는 exact field와 type, schema·campaign ID, 상대 fixture filename, 세 chapter ID의 고유성과
고정 순서를 검사한다. 절대경로, 상위 directory 참조와 알 수 없는 field를 거부한다.

## 4. 실행 상태와 장 경계

`ProductCampaignRun` 하나가 외부에 노출하지 않은 `ProductSession` 하나와 승인된 명령 기록을
소유한다. Game의 모든 상태변경은 `ProductCampaignRun.Execute` 한 경로만 통한다. pointer hover와
preview, 거부된 명령은 기록하지 않는다. 상태를 바꾸는 승인 명령만 순서대로 기록하고, 동일
fixture에서 처음부터 재생하면 같은 `ProductSnapshot`이 나와야 한다.

- `FIRST_LIGHT`: 새 게임부터 첫 마을 결산 전까지
- `SECOND_HEART`: 첫 마을 결산 성공 뒤부터 병원 복구·결산 전까지
- `HEAT_DOME`: 병원 결산 성공 뒤부터 공장·폭염 최종 결과까지

장 경계는 승인 명령 직후의 Core 상태로만 판단한다. 첫 결산이 실패하면 첫 장, 병원 hard condition이
실패하면 둘째 장에 머문다. 성공 경계에서 현재 명령 수를 장 시작 checkpoint로 고정한다.
`RestartChapter`는 checkpoint 뒤 명령만 버리고 같은 기록 prefix를 재생한다. 별도 snapshot mutation,
범용 undo와 완공 자산 철거를 만들지 않는다.

## 5. 저장 형식과 복원

저장 JSON은 다음 현재 사실만 가진다.

- save schema version
- campaign ID와 campaign root SHA-256
- scenario fixture ID와 fixture SHA-256
- 승인된 campaign command의 순서와 command에 필요한 위치 한 점

현재 chapter와 장 시작 checkpoint는 명령을 재생해 다시 계산하므로 JSON에 중복 저장하지 않는다.
명령 enum은 `RestartMission`을 제외하고 현재 제품 흐름에 필요한 닫힌 저장 명령 집합만 포함한다.
장 재시작은 별도 command를 기록하지 않고 checkpoint prefix로 복원한다. 미래 명령 field,
diagnostic, 화면 상태와 preview를 저장하지 않는다.

복원은 campaign·fixture identity와 두 원본의 hash를 먼저 확인하고, 빈 session에서 모든 명령을
순서대로 재생한다. 하나라도 알 수 없거나 거부되면 전체 저장을 유효하지 않은 것으로 처리하며
부분 상태를 열지 않는다. 잘린 JSON, 알 수 없는 version·field, 잘못된 point와 다른 campaign 또는
fixture hash도 같은 방식으로 거부한다. 유효하지 않은 저장은 `Continue`만 비활성화하고 오류를
짧게 보여주며 `New Game`은 항상 가능하다.

## 6. 안전 경계와 파일 쓰기

모든 상태변경 명령은 동기적으로 끝난다. 승인 명령과 장 재시작 직후 한 슬롯을 autosave하므로 그
직후가 저장 가능한 안전 경계다. preview·hover·거부 명령은 저장하지 않는다. `Save & Quit`은 현재
안전 경계를 한 번 더 저장하고 성공을 확인한 뒤 종료한다.

파일은 같은 directory의 임시 파일에 UTF-8 JSON을 쓰고 flush한 뒤 원본 경로로 원자 교체한다.
교체 전에 종료되거나 임시 파일만 남으면 마지막 완성 저장을 계속 사용한다. cloud, backup 회전,
여러 profile, replay 제품과 범용 migration framework는 만들지 않는다.

## 7. 기본 설정과 화면

기존 `ProductMain` 기본 진입점과 제품 장면을 유지한다. 작은 shell child panel 하나가 Title, Pause와
Settings overlay를 맡고, 전력망 규칙이나 campaign state를 계산하지 않는다.

설정은 다음 세 값만 가진다.

- 창 모드: windowed / fullscreen
- UI scale: 100% / 125%
- 조작 도움말 표시: on / off

설정은 변경 즉시 별도 JSON에 원자 저장하고 다음 실행에서 적용한다. 손상되거나 version이 다르면
기본값 `windowed / 100% / on`으로 시작하며 새 게임·이어하기를 막지 않는다. 음량, 언어 선택,
key rebinding과 여러 display profile은 열지 않는다. 게임 언어는 한국어 하나다.

## 8. 느슨한 기술 완료조건

- strict campaign·save·settings codec과 command replay의 대표 Core 검사
- 현재 전체 성공 명령의 모든 prefix를 저장·복원해 snapshot 값이 같은지 한 loop로 확인
- 세 장 경계와 chapter restart, fixture hash mismatch, 잘린·손상·unknown-version 저장의 안전 거부
- 원자 교체 성공과 남은 임시 파일이 기존 저장을 훼손하지 않는 작은 filesystem 검사
- 누적 ProductChecks와 Game build
- 표준 button으로 `Title → New Game → 첫 장 결산 → UI scale 또는 도움말 변경 → Save & Quit`, 새
  process에서 `설정 유지 → Continue → 둘째 장 동일 상태 → Restart Chapter`를 확인하는 1280×720
  native 시나리오 한 번
- 눈에 띄는 clipping·focus·상태문장 확인, 미해결 critical·core-flow major 0, 짧은 독립 검토 한 번

모든 phase별 별도 native 실행, 두 번째 해상도, 강제종료 자동화, 전체 접근성, LLM·사람 플레이,
장별 narrative polish와 다음 단계 placeholder는 만들지 않는다. 사람 관찰은 전체 개발 뒤 테스트로
미루고 `HumanValidationStatus = NOT_COLLECTED`를 유지한다.

## 9. 현재 검사와 종료 기록

현재 구현은 다음 경계에서 종료됐다.

- 누적 ProductChecks: 첫 점등 `10 suites / 664 assertions`, 병원 `5 / 124`, 공장 `5 / 378`,
  폭염 `5 / 243`, 캠페인 저장 `5 / 421` 통과
- Core Release와 Game Debug rebuild: warning/error `0/0`
- 두 개의 fresh Godot process로 1280×720 shell 시나리오 통과: 첫 process에서 새 게임·도움말·설정
  `125% / help off`·첫 장 결산·Save & Quit, 둘째 process에서 설정 유지·Continue·장 재시작 확인
- 재개 경계: `SECOND_HEART`, minute `360`, cash `14,700,000`, command count와 chapter checkpoint `9`
- 도움말 닫기, Continue와 Restart 뒤 지도 keyboard focus 복구 확인
- 기존 전체 제품 native 회귀: 정비 선택 경로가 minute `1845`, cash `4,660,000`으로 종료
- campaign root SHA-256:
  `4db2f72775c4740ffbe5134c410d800945791301fbbd654e82cf18f56c161085`
- scenario fixture SHA-256:
  `b00b7fc9d657fd355b8741e4326d9a5297ae749de629c1763334bcca4df83f9c`
- 해당 shell native build SHA-256:
  `8939759bc182bfb239fd317d28353e47f94abedfba2b874c425741ec4cb56619`
- 독립 검토 최종 결과: `P0=0, P1=0`
- 사람·LLM 관찰: `NOT_COLLECTED`

두 번째 해상도, 강제종료, 전체 접근성, 사람·LLM 플레이는 이 단계에서 반복하지 않았다. 이 완료는
세 장 콘텐츠 고정이나 출시 표현·패키징을 승인하지 않는다.
