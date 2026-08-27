# 현재 작업 범위

## 상태

**FIRST_LIGHT 초반 안내·피드백 개선 scope가 활성 상태다.**

## 단일 결과물

첫 플레이어가 `FIRST_LIGHT`에서 필요한 전력 경로와 다음 행동을 읽고, 시간 손해 없이
초안 오류를 고친 뒤, 완공·실패 원인을 구체적으로 확인할 수 있다.

## 단일 권위

- `FIRST_LIGHT` 상호작 안내와 안전 pause 정책: `game/realtime/r2/RealtimeSession.cs`
- 거부 경간·완공·결과의 player-facing 표시: typed presentation과 해당 `Realtime*Presenter.cs`

## 범위 안

- 변전소 → 발전 접속점 → 동부 생활권의 단계별 다음 행동을 현재 HUD/action 문장에 표시
- `FIRST_LIGHT` 건설 도구 선택과 초안 거부 시 안전하게 pause
- 경간 거부에 현재 거리와 허용 거리를 표시하고 초안 지지점을 더 분명하게 표시
- 공사 완공과 `FIRST_LIGHT` 실패 원인을 다음 행동과 함께 표시

## 범위 밖

- 자동 경로, 정답 전신주, 새 tutorial/save schema, 전체 장 UI 재설계
- Core 공급·열·건설 규칙 변경, 오디오·자산 추가, 패키지·외부 gate·배포
- 사람 UX 통과 주장이나 공식 상용 UX 점수

## 완료 검사

- 가장 가까운 presentation/session smoke에서 단계 안내, pause, 수치 있는 경간 거부,
  완공 안내와 실패 디브리프를 확인
- `./dev build` 및 `./dev play chapter FIRST_LIGHT`의 한 번 native 확인
- 변경한 현재 제품 사실의 소유 문서만 갱신하고 scope를 닫음
