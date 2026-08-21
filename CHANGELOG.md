# Gridworks 변경 기록

## 0.1.0 — macOS 내부 출시 후보

보이는 격자 없이 자유 배치하는 청류시 지도에서 프롤로그 세 임무와 본편 다섯 장을 하나의
전력망으로 이어 플레이하는 상용 2D 게임의 macOS 내부 출시 후보다. 발전소에서 수요처까지
분기·합류망을 직접 건설하고, 작성된 운영 국면의 연속·비상 열여유와 다음 보호정지를 비교한다.

- 장별 현금·망·열 상태·결과가 여덟 임무에 이어지며 완료 저장에서 원하는 장 시작으로 돌아갈 수
  있다. 현재 장 재시작, 이전 장 되감기와 새 게임은 파괴 범위를 알리는 확인 화면을 거친다.
- 선택 수요의 발전원→전체 경로→최소 열여유→수요처와 시설 상태를 지도·inspector·접근성 문장에
  함께 표시한다. 연속·비상·보호정지는 색 외 pattern과 icon으로도 구분한다.
- 네 concept의 사선 산업도시 방향을 개별 지형 tile 7종, transparent 설비·시설 object 9종,
  흑철·황동 frame으로 반영한다. 지면은 400단위 셀에, 강·주거·의료 tile은 정확한 v2 polygon에,
  설비·시설은 class/ID별 node 좌표에 배치한다. 전체 map plate와 concept 원본은 package에 넣지 않고
  실제 선로·위험·선택 상태는 v2 권위 layer가 소유한다. 하단 사건 timeline은
  briefing→정확한 decision window·phase→선택한 promise 결과와 공사분/기한을 읽기 전용으로 표시한다.
- 장별 조명·날씨와 네 인물의 고정 code-native 초상을 제공한다. 외부 음원 없이 도시 환경음, 날씨
  layer, 발주·완공·통전·차단·경고·결과 cue와 두 motif를 생성한다.
- strict settings v3는 v2의 화면·UI·Master/Ambient/SFX 값을 한 번 보존 승격하고 ReduceMotion을
  추가한다. campaign·settings 저장은 같은 폴더의 temp→flush→replace이며 Save & Quit은 두 저장이
  성공한 뒤에만 제목으로 이동한다.
- 최소 지원·검수 해상도는 **1920×1080**이며 UI 100%·125%와 마우스·키보드를 확인했다.
  1280×720은 지원하거나 검수하지 않는다.
- 패키지는 `CommercialMain`과 실행 의존성, 개별 tile/object 16종, v2 world·campaign·build identity,
  기본 오디오 bus, 설치 안내와 라이선스·크레딧·고지만 포함한다. source concept·prototype·v1
  fixture·PDB·로컬 빌드 경로와 이 별도 release record는 ZIP에서 제외했다.

### Artifact record

- 파일: `Gridworks-macOS-0.1.0.zip`
- 상태: `INTERNAL_RC`
- 크기: `126,993,251 bytes`
- SHA-256: `9e3c6869847fcf6c763f85438d8babeb8d9aca0a09e222f282c392e058cd06a9`
- world SHA-256: `304eb051a564a0ceda7912d717268d3011f5f3482a5ab9d1c68dd9330e0e165c`
- campaign SHA-256: `f94617c74de7bab0c97499fbaa8fd542aa64ee3c2fc60a6c7f090de203239200`
- build identity SHA-256: `98db63fb41da601c2620fabd55aaca58218387c6d22eed5208d599e2ab737a17`
- 소스 기준 커밋: `997f67551d07c4f4e5405bb0e202467be3ad53df`
- 앱: `Gridworks 0.1.0`, bundle identifier `com.gridworks.game`
- 실행 파일: Universal 2 (`x86_64 arm64`), architecture별 선언 하한 macOS `14.0`
- 실제 확인 환경: macOS `26.6.1` arm64
- 서명: 로컬 ad-hoc; Developer ID 서명과 Apple 공증 없음

clean committed checkout에서 만든 ZIP을 저장소 밖에 풀었다. 첫 arm64 프로세스는 네 임무를
완료해 저장했고, 두 번째 fresh 프로세스는 그 저장을 읽어 여덟 결과와 에필로그까지 완료했으며,
세 번째 fresh 프로세스는 완료 저장과 마지막 장 선택·재시작 확인을 통과했다. 세 프로세스 모두
실제 keyboard focus 입력, 1920×1080 표식과 종료 코드 0을 남겼다. 즉시 종료하는 headless
확인에서는 Godot이 코드 생성 오디오 객체의 종료 정리 경고를 남겼지만 실행 중 오류·저장 손상·
미완료는 관찰되지 않았다.

[화면·입력 증거](playtests/commercial-2d/LAYOUT_EVIDENCE.md)는 native 1920×1080 UI 100% 제목
화면과 UI 125%의 concept-aligned 도시, 선택 경로, 사건 timeline·ReduceMotion 화면을 보존한다.
같은 실제 keyboard 흐름에서 새 게임→도움말→공사→설정→Save & Quit을 통과했다.

v2 hash나 build identity가 맞지 않는 개발 저장은 이 후보에서 이어갈 수 없다. 앱은 호환되지 않는
저장을 덮어쓰지 않고 별도 파일로 보존한 뒤 쓰기 가능한 새 게임을 시작할 수 있다.

이 기록은 위 ZIP bytes와 확인 환경에만 적용된다. x86_64 실행과 다른 macOS 버전은 검증하지
않았으며 지원한다고 주장하지 않는다. 빌드 환경에는 유효한 코드 서명 identity가 없으므로
Developer ID 서명·공증과 공개 배포는 차단된 상태다. 해당 자격증명, 실제 지원 OS 확인과 별도
배포 승인이 마련되기 전까지 이 파일은 프로젝트 내부 후보로만 사용한다.
