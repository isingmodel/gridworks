# 현재 작업 범위

## 상태

**current R2 repository completion closure가 활성 scope다.**

사용자가 요청한 가장 단순한 구현 방향에 맞춰, 저장소가 소유하는 내부 R2 후보의
완료와 외부 release 승인을 분리한다. current R2와 연결되지 않은 historical non-score 평가
계층을 현행화하지 않고 제거하며, 실제 production 상태 표현의 작은 누락만 닫는다.

## 결과물

- `RealtimeWorldPresenter`가 active risk area를 폭우, thermal-limit override를 폭염으로 표현해
  기존 G3 flood/heat renderer가 실제 Core 상태를 사용하게 한다. risk area가 있는 복합
  상태는 폭우가 우선한다.
- `tools/commercial-ux/native/` 30개 파일의 detached historical evaluator를 제거한다. 이 계층은
  current `./dev`·`./dev check`와 연결되지 않고 old editor-native First Light non-score 정책에
  고정돼 있다. 기존 이력은 Git이 보존한다.
- [남은 작업](NEXT_TASKS.md)은 repository-controlled implementation backlog만 소유하고, 완료 후
  비운다. 실제 hardware·사람·score-bearing model receipt·Developer ID·공개 승인은
  새 `RELEASE_GATES.md`에 `NOT_RUN | PENDING | NOT_APPROVED`로 보존한다.
- README, 개발 구조, 문서 지도, 평가 프로토콜, 도구 안내와 완료 이력이 동일한
  내부-후보/외부-gate 경계를 설명한다.

## 범위 밖

- 전체 8장 packaged action-by-action input harness 추가
- 실제 FHD/UHD display, OS hardware mouse/keyboard, audio device·speaker와 frame-time 관찰
- 사람 미감·사용성·접근성, 한국어·전력설비 전문 검토
- score-bearing capture/judge platform, `CommercialUXProxy`, model/API raw receipt 생성
- 지원 OS 일반화, 법무·자산 권리 판정, Developer ID 서명·공증·공개 배포 승인
- 새 art/audio asset, gameplay mechanic, save/settings schema
- push, PR, merge

## 완료 검사

1. 작은 production smoke가 flood는 `Storm`, thermal override event는 `Heat`, 평상은 `Clear`로
   판정하며 기존 live campaign 회귀가 통과한다.
2. `git ls-files 'tools/commercial-ux/native/**'`가 비어 있고 current 문서·명령에 삭제한 권위
   참조가 없다.
3. 상대 Markdown 링크, `git diff --check`과 `./dev check`가 통과한다. 동일 규칙을 별도
   대형 test matrix로 복제하지 않는다.
4. 두 bounded independent review의 scope-valid finding을 수정한 뒤 최종 clean commit에서
   candidate/qualification을 한 번만 재생성·검증한다.
5. 문서는 internal package·combined 2B 완료만 주장하고, full packaged journey, hardware,
   speaker, 사람, 공식 점수, 서명·공증·공개 승인은 계속 미완료로 닫는다.
