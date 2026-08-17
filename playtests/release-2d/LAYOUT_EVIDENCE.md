# 출시 후보 화면·입력 증거

이 묶음은 구현 커밋 `1fd78bb`에서 만든 macOS 내부 후보와 같은 소스·데이터를 사용한다.

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
