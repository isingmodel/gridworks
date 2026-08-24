# Gridworks 남은 작업

이 문서는 **미완료 작업의 유일한 우선순위 목록**이다. 현재 기준선에서 무엇이 부족한지 설명하지만,
어떤 항목도 자동으로 활성 scope가 되지 않는다. 시작 전 [현재 작업 범위](ACTIVE_SCOPE.md)에 선택한
한 단계의 파일 범위와 완료 검사를 적어야 한다.

## 현재 기준선

- Godot 기본 장면은 live R2 `RealtimeSliceMain`이다.
- 인자 없는 실행은 제품용 새 게임이 아니라 기술 fixture다.
- G3 아트 57개(지도 50/UI 7)가 R2 world와 UI에 연결돼 있다.
- 한 줄 사건 지평선이 사건·공사·결정 기한·열 경계를 compact marker로 표시하고 상세 정보를
  hover 또는 선택으로 연다.
- 8장/16개 사건/34개 story part가 작성돼 있다.
- R2에는 `FIRST_LIGHT`부터 `NORTH_BANK_PROMISE`까지 누적 4장이 구현·자동검증돼 있다.
- 실제 mouse/keyboard 직접 플레이는 첫 3장까지만 관찰했다.
- R2 save/resume, 남은 4장, finale/epilogue, product audio·settings, current R2 패키지와 공식 UX 점수는
  없다.

다음 scope는 [current R2 개발 구조](ARCHITECTURE.md)의 단일 권위를 따라야 한다. 기존 규칙으로 장을
연결할 때는 content/schedule과 native route endpoint를 전진시키고 generic loader/story flow를 유지한다.
새 mechanic은 Core→intent/capability→Session→typed presentation→Godot adapter 순서로 연결한다. 화면
표현만 바꾸는 작업은 presenter와 owning UI에서 시작하며 Core hash를 바꾸지 않는다.

## 권장 순서

### 1. 제품 title과 새 게임 경로 확정

플레이어가 인자 없이 실행했을 때 개발 fixture가 아니라 명확한 새 게임 진입점을 만나도록 한다.
제품 title, 새 user-data, 첫 브리핑까지의 소유권을 정한다. 실제 이어하기는 4번의 R2 save/resume이
완성되기 전까지 title에 이유가 보이는 비활성 상태로 두며, 과거 V2 저장을 연결하지 않는다.

완료 기준:

- default boot와 명시적 개발 fixture가 분리된다.
- 새 설치에서 title→`새 게임`→첫 briefing→`FIRST_LIGHT`가 실제 입력으로 재현된다.
- 빈 user-data에서는 `이어하기`가 보이지만 비활성이고 이유가 명확하며 dead-end나 가짜 복구 경로가
  없다.
- story/modal의 계속 동작을 title의 `이어하기`로 세지 않는다.
- checkpoint와 테스트 route는 계속 명시적 인자로만 접근한다.

### 2. 누적 4장 직접 플레이 검증

`./dev play through NORTH_BANK_PROMISE`를 fresh process에서 플레이해 첫 3장의 실제 망·현금·시계가
4장으로 이어지는지 확인한다. 6개월 달력 전환, 약속 기한 marker와 Keep/Defer 결과를 모두 다룬다.

완료 기준:

- Keep과 Defer의 production input 경로가 각각 결과까지 도달한다.
- 장 전환 뒤 망·공사·자금이 위조되거나 초기화되지 않는다.
- 한 줄 사건 지평선에서 결정 기한과 주변 사건을 실제로 찾고 상세 정보를 열 수 있다.

### 3. 남은 4장 native 구현

작성된 다음 장을 R2 Core/controller/presentation에 순서대로 연결한다.

1. `WHOSE_MARGIN`
2. `BEFORE_WATER_RISE`
3. `SWITCH_OFF_TO_PROTECT`
4. `LONGEST_NIGHT`

각 장은 별도 작은 scope로 연다. briefing, 준비시간, forecast, 사건 시작/종료, 플레이어 결정, authored
result와 다음 장 전환을 같은 누적 상태에서 닫아야 한다. story part selector 단독 실행은 계속
유지하되, 그 통과를 native 도달성으로 세지 않는다.

완료 기준:

- 8장이 동일한 망·자금·시계에서 순서대로 진행된다.
- 각 장의 story/result가 작성 데이터와 일치한다.
- 사건·공사·결정·열 경계가 한 줄 사건 지평선에 시간순으로 나타난다.
- 장별 unit/checkpoint와 8장 누적 경로가 모두 통과한다.

### 4. 캠페인 완결성과 저장

R2 save/resume, finale, 세 epilogue card, 누적 약속 결과와 완료 후 재개를 구현한다. 동결 V2 저장을
그대로 현재 R2 저장이라고 주장하지 말고, Release.V3 상태에 맞는 권위를 정의한다.

완료 기준:

- 진행 중 저장→프로세스 종료→fresh process→title의 `이어하기`→정확한 시각·망·결정 상태 복구
- 공사 중, 사건 중과 장 전환 직전의 시간·망·결정 상태 복구
- 8장 완료→finale→epilogue→완료 저장→fresh process의 `이어하기`→결과와 chapter/replay 선택 복구
- 유효한 저장이 있을 때 `새 게임`의 확인·덮어쓰기 정책
- 빈·손상·구버전 저장의 `이어하기` 비활성, 명시적 거부 또는 migration 정책과 원본 보존

### 5. 시청각·설정·조작성·접근성 마감

현재 G3 적용을 출발점으로 정상·공사·폭염·범람·비상·보호정지·냉각·복귀 화면을 실제 플레이에서
검수한다. 아직 없는 current R2 product audio와 player settings surface를 먼저 구현하고, 과거 V2
audio/settings를 현재 권위로 재사용하지 않는다. 아트·audio 파일의 존재가 아니라 화면 밀도, 설비
실루엣, 상태 인과, sound cue, hit target과 프레임 성능을 판정한다.

완료 기준:

- FHD와 UHD, UI 100/125/150/200%의 가독성
- 정상 조작·공사·폭염·범람·보호정지·냉각·복귀·finale의 화면과 audio가 같은 Core 상태를 말함
- title과 pause에서 settings를 열고 원래 여정·focus로 돌아갈 수 있음
- window mode, master/ambient/SFX volume·mute, UI scale과 Reduce Motion을 제품 UI에서 바꿀 수 있고
  fresh process에서 복원
- mouse와 keyboard 동등 조작, focus 복구와 색 외 cue
- audio를 꺼도 필수 상태·경고·행동을 시각·icon·문장 cue로 동일하게 읽을 수 있음
- hover와 선택 상세 overlay가 world를 과도하게 가리지 않음
- 사람 미감·사용성, 한국어와 전력설비 검토의 이슈 처리

1.0의 audio는 non-voice ambient와 interaction/state cue 범위다. 음성 연기는 이 단계에 포함하지 않는다.

### 6. current R2 fresh-install 후보와 전체 E2E

과거 V2 내부 패키지가 아니라 current R2 전체 여정이 들어 있는 평가용 fresh-install 후보를 만든다.
서명·공증·공개 배포는 아직 하지 않지만, 빈 user-data의 별도 설치 위치에서 production 입력만으로 전체
여정을 재생해 공식 capture에 사용할 수 있는 후보임을 먼저 증명한다. 이 단계가 current R2 candidate
packager, finalized manifest와 manifest verifier의 권위를 함께 소유한다.

완료 기준:

- clean build의 current R2 package와 빈 user-data 설치 검사
- title→8장→finale→epilogue→완료 저장 재개의 production E2E
- 진행 중 저장→프로세스 종료→동일 후보의 fresh process 재개
- settings 변경→동일 후보의 fresh process 재실행→UI scale·Reduce Motion·volume/mute 값 복원
- 개발 fixture와 checkpoint가 명시적 인자 없이는 평가 여정에 섞이지 않음
- source commit, package bytes/tree, target OS/runtime, 기본 settings와 save/settings 경로를 finalized candidate
  manifest에 결속
- candidate packager와 manifest verifier가 누락·변조·다른 source/package에서 fail-closed
- capture 중 package contents가 바뀌면 기존 session을 폐기하고 새 candidate/session을 만듦

### 7. score-bearing 평가 권위와 공식 LLM-as-a-judge

현재 `tools/commercial-ux/native/`는 구조·거부 경로를 위한 non-score 기준선이므로 그대로 공식 점수를
내지 않는다. 6번의 finalized candidate를 소비하는 versioned evaluation-session authority, native
capture, judge input, evidence verifier, deterministic hard-gate oracle과 score aggregator를 current
R2용으로 구현·검증한다. 그 다음 새 설치 cold journey와 고정 coverage journey를 수집한다.

judge는 `gpt-5.6-sol`, reasoning effort `ultra`를 사용한다. [평가 프로토콜](product/COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md)의
rubric·hard gate를 모두 만족한 `CommercialUXProxy >= 87`이 될 때까지 원인 단위로 개선한다.

완료 기준:

- rubric, hard gate, candidate, source, evidence, model identity·platform/API raw receipt와 aggregate가 한
  versioned session에 결속되고 누락·불일치에서 fail-closed
- UX-R0 파일을 덮어쓰지 않고 새 text/native context와 전체 current coverage를 별도 version으로 생성
- fresh actor/judge와 검증 가능한 동일 evidence set
- 필수 category와 hard gate를 포함한 공식 점수 87 이상
- 점수와 별개로 사람 사용성·미감 검토의 출시 차단 이슈 0

검증 가능한 score-bearing execution authority가 없으면 capture와 judgment는 non-score로만 보존하고
`CommercialUXProxy = null`로 닫는다. 나중에 영수증을 붙여 공식 session으로 승격하지 않는다.

### 8. 출시 준비와 배포 승인

평가에 사용한 current R2 후보의 source와 product payload를 유지한 배포 후보를 준비한다. 서명·공증이
추가하는 wrapper·metadata 차이는 별도 allowlist와 deterministic 검사로 한정한다. 내부 평가 package의
통과를 서명·공증이나 공개 출시 승인으로 해석하지 않는다.

완료 기준:

- 지원 OS/하드웨어, 설정·save 경로와 제거/업데이트 정책
- 라이선스·자산 권리·고지 검토
- 평가 후보와 배포 후보의 차이가 허용된 signing/notarization metadata뿐임을 재검증
- signed/notarized artifact를 빈 user-data에 fresh-install해 title boot와 기본 입력 smoke 재검증
- product payload나 gameplay-affecting contents가 달라지면 6번 candidate와 7번 평가 session을 다시 생성
- Developer ID 서명·공증과 공개 출시 여부의 명시적 소유자 승인

## 테스트 선택 원칙

| 질문 | 가장 작은 올바른 시작점 |
|---|---|
| 문장·결과 하나가 맞는가? | `./dev story <selector>` |
| 특정 시각의 UI/Core 상태가 맞는가? | `./dev checkpoint <CHECKPOINT_ID>` |
| 한 장의 누적 전환이 맞는가? | catalog가 명시적으로 지원하는 `./dev play through <CHAPTER_ID>` |
| 첫 경험·저장·누적 선택·전체 완결성이 맞는가? | fresh-process E2E |
| 미감·이해·재미가 충분한가? | 실제 화면의 사람/LLM 관찰 |

unit 테스트가 가능하도록 새 story 구간도 독립 selector를 먼저 제공한다. 다만 unit fixture가 production
controller나 앞선 누적 상태를 흉내 내서 E2E 증거를 대체하게 만들지 않는다.
