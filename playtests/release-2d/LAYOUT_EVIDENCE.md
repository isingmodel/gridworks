# 출시 후보 화면·입력 증거

이 묶음의 첫 두 절은 구현 커밋 `1fd78bb`에서 만든 직전 macOS 내부 후보와 같은 소스·데이터를
사용한다.

- 패키지: `Gridworks-macOS-0.1.0.zip`
- 패키지 SHA-256: `218bcd436b34d417c19f553b2e748fbcf68f0823eba7bd1fb3862e9e06aa0e2d`
- world SHA-256: `d1b22271be87ac598d9e7b86123e6a5cc67dde43ef7884fa635bc73a311288e8`
- campaign SHA-256: `32e9f3285b7547c6aa2f1895e294e618106aae02c685cf13337b5eb3da2d65b8`
- 실행 환경: macOS 26.6.1, Apple M1, Godot 4.7.1 .NET

## 화면 배치

| 배치 | 대표 화면 | PNG SHA-256 | 실행 기록 |
|---|---|---|---|
| 1280×720 · UI 100% | [화면](layout-evidence/1280x720-ui100.png) | `c52fe8948c580f0f0b50c022f4471414813af259306436db3b00e699e550364f` | [로그](layout-evidence/1280x720-ui100.log) |
| 1280×720 · UI 125% | [화면](layout-evidence/1280x720-ui125.png) | `cb42256c1359101938d9ccfa5cf3b144d8572767e585aa25c00b3f6b545c9035` | [로그](layout-evidence/1280x720-ui125.log) |
| 1920×1080 · UI 100% | [화면](layout-evidence/1920x1080-ui100.png) | `4d26353191d09191dfcd29185d26d7c543847f6a5a6b7bda5380741d6f16a287` | [로그](layout-evidence/1920x1080-ui100.log) |
| 1920×1080 · UI 125% | [화면](layout-evidence/1920x1080-ui125.png) | `c183baee4943c31cd1c7dc78dc68b28bf1973e901f2b14481c2d013b949de31b` | [로그](layout-evidence/1920x1080-ui125.log) |

1280×720 두 화면은 최종 ZIP을 저장소 밖에 풀어 직접 기록했다. 1920×1080 두 화면은 같은 커밋의
Game 어셈블리와 같은 embedded world·campaign을 사용하고, 캡처 동안에만 프로젝트 viewport를
1920×1080으로 바꿔 기록한 뒤 1280×720으로 되돌렸다. 네 실행은 모두 본편 2장 저장 경계에
도달했다.

대표 화면에서 지도, 상태 범례, 헤더, 작업 패널과 핵심 조작이 겹치거나 잘리지 않았다. UI 125%는
100%와 다른 실제 글자 크기를 사용하며, 1280×720에서 작업 패널의 긴 내용은 세로 스크롤로 끝까지
접근할 수 있다.

## 키보드·focus

[패키지 입력 기록](layout-evidence/keyboard-focus.log)은 최종 ZIP의 arm64 실행 파일을 저장소 밖에서
실행해 다음 경로를 통과했다.

- 실제 viewport 마우스 입력으로 변전소와 분기·합류 선로를 계획·취소·발주·완공
- 선로 끝 되돌리기
- 지도 focus에서 `오른쪽 방향키`와 `Enter` 입력
- 이전 마우스 hover 대상이 키보드 선택으로 잘못 재선택되지 않음을 확인

최종 표지는 `RELEASE_CONSTRUCTION_SMOKE_PASS`이며 로그 SHA-256은
`d0bc7b450d52fee15e07125f9d47e77a6e86d6ba0ba968952eb57ed090e00e37`이다.

즉시 자동 종료 뒤의 ObjectDB 정리 경고는 패키지 전체 흐름에도 기록된 제한된 minor다. 실행은
모두 종료 코드 0이며 화면·입력 완료 표지보다 뒤에 발생한다.

## 공식 관찰 후속 후보의 영향 구간 확인

직전 후보의 공식 LLM 관찰 뒤 구현 커밋 `47f6095`와 현재 장 수요 문구 수정 커밋 `0b5bf37`을
반영한 후속 후보는 다음 identity를 사용한다.

- 패키지 SHA-256: `90c3257925c0e5224a9b910be9d6f9f510a4a4f81cbc8c5e759831eb0696f9db`
- world SHA-256: `5633d9e0de53eefec8de50e8d1d8eef095da7be9488497628db925e3e1ca0681`
- campaign SHA-256: `32e9f3285b7547c6aa2f1895e294e618106aae02c685cf13337b5eb3da2d65b8`

[후속 native 공사 기록](official-fix-construction-smoke.log)은 1280×720·UI 125%에서 실제 viewport
입력으로 다음 영향 구간을 한 번 확인했다.

- 고정 작업 영역의 공사 도구가 작업 패널 안에 완전히 보임
- 강물 위 변전소 preview와 실제 선택이 같은 typed 원인으로 거부되고 상태가 바뀌지 않음
- 연결 한도가 찬 접속점이 클릭 전에 `연결 회선 4/4`와 불가 preview를 표시함
- 선로 완공 뒤 도구가 `현황 보기`로 돌아옴

기록의 SHA-256은 `95cf0a7c705d467070284ccd2d07d804a2fb0692f189d3d3b03b44ef2069de2b`다.
후속 ZIP의 두 fresh process 캠페인 흐름도 저장·이어하기·현재 임무 재시작, 매 공사 뒤 지도 focus와
8개 임무 완료 표지를 남겼다. 이 절은 영향이 큰 한 배치의 bounded native 판정이며, 직전 후보의 네
PNG를 후속 후보의 새 시각 증거로 바꾸어 부르지 않는다.
