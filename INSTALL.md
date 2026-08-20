# Gridworks 실행·내부 후보 설치

## 현재 실행 경계

현재 활성 revision gate는 없고 저장소 기본 장면은 `CommercialMain`이다. 실시간 전면 개편 R2
구현은 `4c27f65`에 보존됐지만 마지막 exact-tree 전체 harness가 사용자 지시로 중단됐으므로 설치·
출시 후보나 완료 gate가 아니다. R3~R7은 `USER_STOPPED_AFTER_R2`다. 물리 UHD, 사람·전문 검토,
Developer ID 서명·공증과 공개 출시는 계속 미수집·미승인이다.
[HTML 목표 이미지](docs/mockups/realtime-target/README.md)는 non-runtime 참고 자료이며 실행 파일,
native capture 또는 지원 환경 증거가 아니다.

## 저장과 설정 파일

현재 상용 v2 단계 G 완료 게임은 다음 Godot 사용자 폴더의 v3 파일을 사용한다.

```text
~/Library/Application Support/Godot/app_userdata/Gridworks/
  release-campaign-save-v3.json
  settings.json
```

`release-campaign-save-v3.json`은 여덟 장 캠페인 진행과 같은 망의 상태를 보존한다. 이전 개발판의
`release-campaign-save-v2.json`은 자동 변환하지 않으며 원본을 덮어쓰지 않는다. `settings.json`의
현재 권위는 strict `gridworks.settings.v3`다. 정상 settings v2는 화면·UI 배율·세 음량을 보존하고
`움직임 줄이기`를 끈 상태로 한 번 승격한다. 손상되었거나 알 수 없는 설정 문서는 자동으로
덮어쓰지 않고 기본값으로 실행하며 화면에 오류를 알린다.

완전히 새 상태로 확인하려면 게임을 종료한 뒤 위 파일을 삭제하지 말고 별도 폴더로 옮겨 백업한다.

## 저장소에서 실행

현재 기본 장면은 단계 G 완료본의 `CommercialMain`이다. R2 참고 scene가 이를 대체하지 않는다.
저장소 루트에서 다음과 같이 실행한다.

```sh
./.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot --path game
```

처음 준비한 checkout은 [README의 도구 버전과 hash](README.md#개발-도구)에 맞는 Godot 4.7.1
Mono를 `.tools/godot-4.7.1/`에 준비하고 빌드한다.

```sh
dotnet restore game/Gridworks.Game.csproj
dotnet build game/Gridworks.Game.csproj -c Debug
```

이 실행은 설치·서명된 후보가 아니며 개발용 Debug witness를 포함할 수 있다.

## macOS 1.0.0 내부 후보

단계 G의 패키징 스크립트는 clean commit에서만
`dist/Gridworks-macOS-1.0.0-internal/` 후보 세트를 만든다.
이 후보는 내부 검증용이며 로컬 ad-hoc 서명만 사용한다. Developer ID 서명과 Apple 공증은 하지
않았고 공개 배포는 승인되지 않았다.

```sh
tools/package_commercial_macos_internal.sh
```

스크립트는 ExportRelease build identity, arm64·x86_64 Game DLL의 PE Machine과 개별 hash,
독립 `Gridworks.pck`의 Godot 4.7 PCK v4 자원표, v2 데이터 hash, 법적 문서와 ad-hoc 서명을
검사한다. ZIP을 다시 푼 뒤 같은 감사를 반복하고, ZIP·manifest·SHA-256을 `dist/` 안의
same-volume 임시 디렉터리에서 모두 검증한 뒤 디렉터리 rename 한 번으로 다음 세트를 공개한다.

```text
dist/Gridworks-macOS-1.0.0-internal/
  Gridworks-macOS-1.0.0-internal.zip
  Gridworks-macOS-1.0.0-internal.manifest.txt
  Gridworks-macOS-1.0.0-internal.sha256
```

내부 후보를 실행할 때는 먼저 `.sha256`과 ZIP의 SHA-256을 비교하고, ZIP 안의
`PACKAGE_MANIFEST.txt`가 별도 manifest와 같은지 확인한다. 앱을 쓰기 가능한 로컬 폴더로 옮긴 뒤
Finder에서 control-click하고 **열기**를 선택한다. macOS가 막으면 hash와 출처를 다시 확인한
신뢰할 수 있는 내부 후보에만 **시스템 설정 → 개인정보 보호 및 보안 → 확인 없이 열기**를
사용한다. 보안 기능을 전역으로 끄지 않는다.

패키저는 새 설치 전체 캠페인 실행을 대신하지 않는다. manifest의
`new_install_full_campaign=NOT_RUN_BY_PACKAGER`는 같은 ZIP bytes를 별도의 빈 user-data에서
처음부터 끝까지 실행하는 별도 gate라는 뜻이다. 단계 G 완료 후보는 이 외부 UI gate에서 새 게임→
저장→fresh continue→전체 캠페인·에필로그→완료 저장 재개→장 재설계를 통과했다. 정확한 실행 범위와
후보 identity는 [단계 G 완료 증거](docs/scopes/COMMERCIAL_2D_IMPLEMENTATION.md#8-전체-완료-증거--단계-g-완료)가
소유한다.

## 0.1.0 역사 기록

`Gridworks-macOS-0.1.0.zip`은 33×21 `ReleaseMain` 기술 기준선의 과거 내부 ZIP이다. 이 파일도
ad-hoc·비공증 상태이며 상용 v2 설치 후보가 아니다. 당시 save v2와 설정 파일은 현재 v3 파일을
덮어쓰거나 삭제하지 않고 역사 회귀에만 보존한다.

## 공개 배포 경계

현재 저장소에는 공개 배포가 승인된 package가 없다. 단계 H의 사람 검증·전문 교정, Developer ID
서명·공증, 지원 OS 확인과 별도 소유자 결정 전에는 자동 업데이트·외부 지원·공개 출시를 주장하지
않는다.
