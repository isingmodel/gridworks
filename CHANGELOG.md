# Gridworks 변경 기록

## 0.1.0 — macOS 내부 테스트 후보

세 장 campaign의 건설·사건·결산 흐름, 한 슬롯 저장·재개, 장 재시작과 기본 화면 설정을 하나의
제품 실행 장면으로 묶었다. 이번 후보는 기존 규칙과 수치를 바꾸지 않고 다음 release 경계를 더한다.

- code-native 2D theme, 지도 상태 표현과 vector app icon
- 코드가 생성하는 최소 ambient와 상태 cue
- master, ambient, SFX 음량 설정과 기존 설정 읽기
- canonical campaign·heatwave JSON의 byte-identical embedded resource
- macOS 14.0 minimum, Universal 2, ad-hoc internal export preset

### Artifact record

- 예상 파일: `Gridworks-macOS-0.1.0.zip`
- 상태: `FINAL`
- SHA-256: `bfe684b19b0930a6252bf68c2d7bee2dbd88eac6a97772baea96c34abcd94c08`
- 실제 확인 환경: macOS 26.6.1 arm64, package 전체 흐름·fresh process 저장 재개 완료

이 release record는 저장소와 별도로 전달되는 위 ZIP bytes에 한정한다. 이 후보는 Developer ID
서명·공증, 외부 사용자 테스트와 공개 배포를 포함하지 않으며
`HumanValidationStatus = NOT_COLLECTED`를 유지한다.
