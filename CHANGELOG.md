# Gridworks 변경 기록

## Unreleased — 실시간 전면 개편 R2 중단 기록

- R0 기준선 `5a9e465`, R1 실시간 Core `3da1897` 뒤 R2 UX foundation·수평 사건 지평선 구현을
  `4c27f65`에 보존했다.
- 마지막 exact-tree 전체 harness는 사용자 지시로 중단돼 완료 gate가 아니다. 앞서 수집된 증거는
  보존하지만 R2 PASS, 전면 개편 완료 또는 출시 증거로 승격하지 않는다.
- R3~R7과 더 넓은 전면 개편은 `USER_STOPPED_AFTER_R2`이며 활성 revision gate는 없다. 기본 장면은
  계속 `CommercialMain`이다.
- 물리 UHD, 사람 사용성·미감, 한국어·전력설비 전문 검토와 공개 출시는 미수집·미승인이다.
  [HTML 목표 이미지](docs/mockups/realtime-target/README.md)는 non-runtime 참고 시안이며 게임 구현이나
  검증 증거가 아니다.

## 상용 v2 단계 G 내부 후보 — 동결 기록

현재 기본 `CommercialMain`은 별도 v2 world·campaign·Core에서 보이는 격자 없는 자유 배치,
수면·건물·설비 점유영역, 선로 도체·변전소 주기기·전신주 접속부의 연속·비상 열 한계,
보호정지·냉각, 안전 의무·도시 약속·최근 공사 복구와 여덟 임무·에필로그를 연결한다.
단계 G의 정확한 해시·검사·native·package·새 설치 증거는
[상용 구현 계약](docs/scopes/COMMERCIAL_2D_IMPLEMENTATION.md#8-전체-완료-증거--단계-g-완료)이 소유한다.

- 이름 붙은 발전원→수요 병목 경로, 원자적 배치 feedback, 고정 승인 checklist, 국면 비교 표,
  누적 공사 forecast와 실행 전 복구 결과를 추가했다.
- 정보 viewport 200px, focus-follow 보조 조작, 고정 승인·발주 CTA를 유지하면서 조밀한 후보 선택과
  hover 합법성, 접근성 이름·키보드 focus를 보강했다.
- strict settings v3·v2 one-step import, UI 배율·세 음량·움직임 줄이기, 장별 날씨·도시 반응,
  네 인물 초상, event cue·motif와 긴 결과·에필로그 카드의 scroll/keyboard 처리를 추가했다.
- ExportRelease는 final v2 world·campaign·build identity만 포함하고 prototype·v1·PDB·DEBUG witness·
  로컬 경로를 제외한다. Universal macOS 내부 ZIP과 audit·asset/legal manifest를 추가했다.

내부 후보는 `Gridworks-macOS-1.0.0-internal.zip`, source commit
`78ff78889ed2c21aad43d1d285ea1a5e8d01442a`, 상태 `INTERNAL_ADHOC / NOT_AUTHORIZED`다. 저장소 밖
새 설치 전체 캠페인 gate는 통과했지만, 사람 전체 플레이·한국어 전문 교정·Developer ID 서명·공증과
공개 배포는 미수집·미승인이다.

## 0.1.0 — 동결된 macOS 내부 출시 후보

33×21 청류시 지도에서 프롤로그 세 임무와 본편 다섯 장을 하나의 전력망으로 이어 플레이하는
내부 출시 후보다. 발전소에서 수요처까지 선로를 직접 건설하고, 세 갈래 이상의 분기·합류와
설비별 사용량·정격·여유를 확인하며 예고된 상황에 대비한다.

- 장마다 사람이 겪는 상황, 전력망에 생긴 영향과 다음 행동을 자연스러운 한국어로 전달한다.
- 본편은 정답 접속점을 지시하지 않고 평상시·예고 상황의 공급 결과를 목표로 삼는다. 예고 상황을
  지도에 미리 적용해 사용 불가 설비, 우회 경로와 설비별 여유 용량을 비교할 수 있다.
- 건설비와 장별 예산, 공사 시간, 정상 공급과 고정 사고 목표가 여덟 임무 동안 이어진다.
- 한 슬롯 자동 저장, 새 프로세스 이어하기, 현재 임무 재시작과 이전 임무부터 다시 설계를 제공한다.
- 도시 지형, 생활권, 설비, 통전·현재 미사용·공사·사용 불가를 색과 선 무늬로 함께 표현한다.
- 긴 설명만 스크롤하고 현재 작업·도구·점검 버튼은 하단 고정 영역에 둔다. 공사가 끝나면 현황
  보기와 지도 입력으로 돌아간다.
- 선로 시작점을 고르기 전에 접속 여유를 보여주며, 공급 실패는 실제 발전원 경로의 첫 병목,
  남은 용량과 수요 전량을 설명한다.
- 강물 위 신규 변전소·중간 전신주는 막고, 고정 교량·보호기초 접속점과 강을 가로지르는 선로는
  허용한다.
- 외부 음원 없이 생성하는 낮은 환경음과 차단·통전·정전 알림을 제공하며 Master·Ambient·SFX
  음량을 저장한다.
- 패키지에는 `ReleaseMain`과 그 실행 의존성, 기본 오디오 bus layout, 설치 안내, 라이선스,
  크레딧, Godot 4.7.1과 .NET 8.0.29의 버전 고정 라이선스·제3자 고지 원문만 포함한다. 디버그
  심볼, 로컬 빌드 경로와 이전 제품 fixture는 제외했다.

### Artifact record

- 파일: `Gridworks-macOS-0.1.0.zip`
- 상태: `INTERNAL_RC`
- 크기: `124,456,235 bytes`
- SHA-256: `90c3257925c0e5224a9b910be9d6f9f510a4a4f81cbc8c5e759831eb0696f9db`
- world SHA-256: `5633d9e0de53eefec8de50e8d1d8eef095da7be9488497628db925e3e1ca0681`
- campaign SHA-256: `32e9f3285b7547c6aa2f1895e294e618106aae02c685cf13337b5eb3da2d65b8`
- 소스 기준 커밋: `0b5bf37`
- 앱: `Gridworks 0.1.0`, bundle identifier `com.gridworks.game`
- 실행 파일: Universal 2 (`x86_64 arm64`), architecture별 선언 하한 macOS `14.0`
- 실제 확인 환경: macOS `26.6.1` arm64
- 서명: 로컬 ad-hoc; Developer ID 서명과 Apple 공증 없음

저장소 밖 임시 폴더에서 첫 프로세스가 프롤로그 세 임무와 본편 첫 장을 마치고 저장했다. 두 번째
프로세스는 그 저장을 읽고, 현재 임무 재시작을 확인한 뒤 남은 본편 네 장과 마지막 결과까지
완료했다. 최종 상태는 운영 자금 `17,050,250`, 완공 설비 `17곳`, 선로 `21구간`이었다. 두
프로세스 모두 종료 코드 0과 완료 표식을 남겼다. 즉시 종료하는 headless 확인에서는
Godot이 코드 생성 오디오 객체의 종료 정리 경고를 남겼지만, 실행 중 오류·저장 손상·미완료는
관찰되지 않았다.

[화면·입력 증거](playtests/release-2d/LAYOUT_EVIDENCE.md)는 직전 후보의 네 대표 화면과 후속
후보의 1280×720·UI 125% 작업 영역·접속 한도·수면 거부, 저장소 밖 패키지의 키보드 focus를
구분해 보존한다.

world hash가 바뀌었으므로 이전 개발 저장은 이 후보에서 이어갈 수 없다. 앱은 호환되지 않는 저장을
덮어쓰지 않고 `.bak`으로 보존하며 새 게임은 항상 시작할 수 있다.

이 기록은 위 ZIP bytes와 확인 환경에만 적용된다. x86_64 실행과 다른 macOS 버전은 검증하지
않았으며 지원한다고 주장하지 않는다. 빌드 환경에는 유효한 코드 서명 identity가 없으므로
Developer ID 서명·공증과 공개 배포는 차단된 상태다. 해당 자격증명, 실제 지원 OS 확인과 별도
배포 승인이 마련되기 전까지 이 파일은 프로젝트 내부 후보로만 사용한다.
