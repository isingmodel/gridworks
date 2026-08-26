# Gridworks 외부 출시 gate

이 문서는 current R2 내부 후보 완료 후에도 저장소 안에서 자동으로 만들 수 없는
증거·자격 증명·승인을 소유한다. **구현 backlog가 아니며 실행 권한을 만들지 않는다.**

상태의 뜻은 다음과 같다.

- `NOT_RUN`: 해당 exact package에서 필요한 관찰을 시작하지 않음
- `PENDING`: 실행 주체, 자격 증명, 정책 또는 선행 증거가 준비되지 않음
- `NOT_APPROVED`: 증거와 별개로 명시적 소유자 승인이 없음

## 현재 gate

| gate | 상태 | 필요 실행 주체 | 최소 증거 | 통과 후에만 허용되는 주장 |
|---|---|---|---|---|
| full native cold/coverage journey | `NOT_RUN` | 평가 operator | combined 2B record가 고정한 exact package, 빈 data의 title→8장→finale→epilogue, safe save/resume·settings의 동일-session capture | 전체 packaged journey가 관찰됨 |
| 실제 display·input·performance | `NOT_RUN` | device QA owner | 지정 FHD/UHD·OS/DPI, UI 100/125/150/200%, mouse/keyboard delivery, focus·window round-trip, 상태별 frame-time 로그 | 검수한 hardware/OS matrix에서 표시·조작·성능이 통과함 |
| 실제 audio device·speaker | `NOT_RUN` | audio QA owner | Master/Ambient/SFX mute·volume, loop seam, Breaker/Energize/Outage 구분, resume 무재생과 필수 시각 cue 동등성 관찰 | 검수한 device의 playback·청감이 통과함 |
| 사람·전문가 검토 | `NOT_RUN` | human QA, 한국어·전력설비 reviewer | 미감·사용성·접근성·이해·용어 issue log, severity/owner/fix/retest | 검토 범위의 사람 UX·용어 gate가 통과함 |
| 공식 score-bearing UX 평가 | `PENDING` | 검증 가능한 model/API execution owner | versioned candidate/session/evidence/oracle, fresh actor/judge, `gpt-5.6-sol` `ultra` raw platform receipt, [평가 프로토콜](product/COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md) 통과 | `CommercialUXProxy >= 87`; 현재는 `null` |
| 지원·권리·서명·공증 | `PENDING` | release owner, 법무/자산 owner, Apple Developer account owner | 지원 OS/hardware·install/update/remove 정책, license/asset 권리, Developer ID signed/notarized artifact와 fresh-install smoke | 해당 artifact가 배포 기술·권리 gate를 통과함 |
| 공개 배포 | `NOT_APPROVED` | product/release owner | 위 gate의 필수 범위 통과와 exact release artifact/hash에 대한 명시적 승인 | 공개 출시가 승인됨 |

## 실행 규칙

1. gate는 실행 전 clean source commit, candidate manifest, archive와 combined 2B record를 exact
   hash로 고정한다.
2. 관찰이 없는 항목을 자동 검사 통과로 승격하지 않고, 이전 package의 증거를 새
   package에 소급 적용하지 않는다.
3. 관찰에서 재현 가능한 제품 결함이 발견되면 [남은 구현 작업](NEXT_TASKS.md)의 규칙으로
   한 결함만 소유하는 active scope를 연다.
4. product payload나 gameplay-affecting content가 바뀌면 package/combined 2B와 해당 외부
   session을 새로 만든다. signing/notarization wrapper 차이는 별도 allowlist로 고정한다.
5. 각 주장은 통과한 exact matrix에만 한정한다. 하나의 Mac, display, speaker 또는 관찰자를
   전체 지원 범위로 일반화하지 않는다.
