# Gridworks 남은 구현 작업

## 현재 상태

**repository-controlled implementation backlog는 없다.**

현재 저장소가 소유하는 목표는 **current R2 내부 후보**다. 다음 경로가 동일한
current graph에 결속돼 있다.

- 실시간 8장, 16개 사건, 34개 story part와 finale→epilogue 3장
- 제품 title·New Game·Continue·reset, current-v3 save, title/gameplay 공용 settings
- generated non-voice ambient와 typed live operation cue, flood/heat 상태 표현,
  `Reduce Motion` weather 동작
- universal macOS ad-hoc internal ZIP의 strict package identity와 app-owned data/lifecycle combined 2B
  qualification

이 완료선은 실제 hardware·사람 검수, score-bearing 평가, Developer ID·공증·공개
배포 승인을 포함하지 않는다. 그 항목은 미완료 주장을 삭제하지 않고
[외부 출시 gate](RELEASE_GATES.md)에 별도로 보존한다.

## 다시 여는 규칙

새 구현은 다음 중 하나가 있을 때만 작은 [현재 작업 범위](ACTIVE_SCOPE.md)로 연다.

1. 외부 gate의 exact package 관찰에서 재현 가능한 제품 결함이 발견됨
2. 사용자가 internal R2 후보를 넘는 새 제품 목표를 명시함

이때도 결함/목표 하나의 단일 권위, 범위 밖 항목과 가장 작은 완료 검사를 먼저
적는다. 물리 장치나 사람 관찰을 흉내 내는 대형 headless harness, 실행 권위 없는
score platform, 승인 전 release automation은 미리 만들지 않는다.
