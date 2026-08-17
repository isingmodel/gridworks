# 2D 표현·사운드·패키징 구현 기준

> 상태: **ACTIVE**
>
> 현재 권한: 기존 세 장 캠페인을 외부 테스트 직전의 macOS 내부 테스트 빌드로 마감한다.
>
> 다음 권한: 외부 사용자 관찰, 배포 서명·공증과 공개 출시는 포함하지 않는다.

## 1. 플레이어에게 보이는 결과

기존 `첫 점등 → 두 번째 심장 → 열돔 아래` 캠페인의 규칙과 수치는 바꾸지 않는다. 대신 한 화면의
산업 지도, 설비와 상태가 완성된 하나의 2D 시각 언어로 보이고, 발주·점등·사용불가를 짧은 소리로
확인할 수 있어야 한다. 타이틀에서 새 게임을 시작하고 저장·재개해 세 장을 끝내는 동일한 흐름이
설치 가능한 macOS 앱에서도 동작해야 한다.

이 단계가 끝나면 제품은 **외부 테스트 준비 완료**다. 사람 사용성·재미·밸런스와 지원 OS 범위는
아직 검증된 것이 아니며 `HumanValidationStatus = NOT_COLLECTED`를 유지한다.

## 2. 바꾸지 않는 권위

- 캠페인 순서·브리핑·진입 조건은 [`product-campaign-v1.json`](../../data/product-campaign-v1.json)이
  계속 단독 소유한다.
- 전력망 규칙·경제·사건 수치는 [`product-heatwave-v1.json`](../../data/product-heatwave-v1.json)과
  `Gridworks.Core.Product`가 계속 소유한다.
- campaign save schema와 accepted-command journal은 변경하지 않는다.
- `ProductMain.tscn` 하나가 기본 실행 진입점이다. 현재 지도 좌표, 클릭 hitbox와 키보드 입력을
  바꾸지 않는다.
- 완료된 두 prototype의 장면·fixture·검사는 수정하지 않는다.

패키지에서도 같은 JSON bytes를 쓰기 위해 두 파일을 Game assembly의 embedded resource로 포함한다.
repository checkout과 export app은 그 한 복사본을 각각 build 입력으로 사용할 뿐, 별도 runtime
fixture를 만들지 않는다. startup hash와 load는 embedded bytes를 사용하고 repository source tree를
runtime에 요구하지 않는다.

## 3. 시각 마감

기존 code-native vector 지도를 현재 제품의 원본 2D 표현으로 마감한다.

- 공통 theme 한 개로 panel, button, focus, spacing과 한국어 typography를 통일한다.
- 지도 배경에 강·도로·산업 구역의 낮은 대비 silhouette를 더하되 규칙이나 클릭 영역으로 쓰지
  않는다.
- 발전원, 변전소, 마을, 병원, 공장과 선로는 기존 상태값만 그린다.
- 계획은 점선, 공사는 사선 pattern, 통전은 실선과 느린 흐름 표시, 사용불가는 끊긴 선과 X표로
  구분한다. 모든 상태는 색뿐 아니라 pattern과 label로도 전달한다.
- 애니메이션은 느린 통전 흐름과 짧은 상태 강조만 허용한다. 카메라, 날씨 입자, cinematic과 새
  renderer는 만들지 않는다.
- 대표 조합인 1280×720·UI 100%와 1920×1080·UI 125%에서 핵심 문장, 버튼과 지도 legend가 잘리지
  않아야 한다. 더 작은 화면이나 다른 scale 조합은 외부 테스트 범위다.

기존 `assets/`의 콘셉트 이미지는 권리와 실제 규칙 경계가 다른 참고 자료이므로 runtime에 넣지
않는다. 새 raster sprite나 외부 font·asset도 도입하지 않는다.

## 4. 사운드와 설정

사운드는 새 파일 자산 없이 고정 seed의 PCM16 파형을 코드로 생성한다.

- 낮은 볼륨의 산업 ambient loop 한 개
- 발주/차단기 cue 한 개
- 완공·점등 cue 한 개
- 폭염 사용불가 cue 한 개

소리는 화면 상태를 보조할 뿐 오디오만으로 전달되는 규칙은 없다. 별도 music, voice-over, mixer
framework와 공간 음향은 만들지 않는다.

설정 문서는 `gridworks.settings.v2`로 한 번 올리고 다음 값만 추가한다.

- master volume percent
- ambient volume percent
- SFX volume percent

세 값은 `0 / 25 / 50 / 75 / 100` 중 하나다. 기존 창 모드, UI scale과 도움말 설정을 유지한다.
기본값과 v1에서 보충하는 값은 master·ambient·SFX 모두 `100`이다. 기존 v1 설정은 정확히 한 번
읽어 이 값을 보충하고, 새 저장은 v2만 쓴다. 손상 설정은 기존처럼 기본값으로 안전하게 돌아가며
campaign save에는 영향을 주지 않는다.

## 5. macOS 내부 테스트 패키지

- 지원 후보는 `macOS arm64` 하나다. 공식 Godot 4.7.1 Mono template이 Universal 2이므로 산출물은
  universal `.app`을 담은 ZIP이지만, 이 단계의 실제 실행 증거와 지원 주장은 arm64에만 한정한다.
- package deployment target은 `.NET 8`의 현재 지원 범위에 맞춘 `macOS 14.0`으로 설정한다. 이는
  아직 검증하지 않은 하한 후보이지 지원 증거가 아니다. 현재 내부 지원·확인 환경은
  `macOS 26.6.1 arm64` 하나이며, 더 낮은 버전 호환성은 외부 테스트 전에는 주장하지 않는다.
- 앱 이름은 `Gridworks`, 내부 버전은 `0.1.0`, bundle identifier는 `com.gridworks.game`으로 고정한다.
- `export_presets.cfg`와 원본 vector app icon은 추적한다. `/dist/`의 build artifact와 인증정보는
  추적하지 않는다.
- local internal build는 ad-hoc signing만 허용한다. Developer ID 서명·공증이 없는 ZIP은 공개
  배포물이 아니며 외부 전송 전 별도 권한과 credential이 필요하다.

공식 export template은 다음 파일만 사용한다.

- URL: `https://github.com/godotengine/godot/releases/download/4.7.1-stable/Godot_v4.7.1-stable_mono_export_templates.tpz`
- SHA-256: `ef9a708be51ecd974cd7dccdcafd7a1870da3d3e1c24c072bdbb9818c7a7db63`

## 6. 법적·배포 문서

runtime에 넣는 게임 표현과 사운드는 이 저장소에서 생성한 것만 사용한다. 다음 문서를 짧게
남긴다.

- 설치·실행·저장 위치와 unsigned internal build 경고
- 게임/엔진 credits와 third-party notices
- 현재 저작물에 재사용 허가를 부여하지 않는 저장소 법적 상태
- 이 내부 테스트 빌드의 변경 요약과 artifact hash

공개 라이선스 부여, 외부 자산 반입과 Contributor License Agreement는 이 단계에 포함하지 않는다.

## 7. 허용 파일과 제외

허용한다.

- `game/`의 제품 theme, vector map, 제품 audio, shell 설정, package preset/icon과 startup resource 경계
- `src/Gridworks.Core/Product/`의 settings contract·codec만
- `tools/Gridworks.ProductChecks/`의 settings/resource/package 관련 작은 검사
- `.gitignore`, README, roadmap/checklist, 제품 시각 문서와 설치·법적·종료 문서

제외한다.

- 전력망·경제·사건 규칙, fixture 값과 campaign save schema 변경
- 새 장, 임무, 대화, tutorial mechanic과 콘텐츠 재작성
- raster asset pipeline, camera, 3D, shader framework, localization과 audio middleware
- Windows/Linux package, cloud, telemetry, updater와 installer framework
- Developer ID credential, notarization, storefront upload와 외부 사용자 테스트

## 8. 느슨한 완료 기준

중간 단계의 증거 체계를 늘리지 않는다. 다음 한 묶음이면 충분하다.

1. 누적 `Gridworks.ProductChecks` 한 번과 Game Release rebuild 한 번이 통과한다.
2. settings v1→v2, 세 음량 경계, embedded campaign/fixture byte identity를 작은 자동검사로 확인한다.
3. editor build에서 1280×720 전체 캠페인 대표 native smoke 한 번을 통과한다.
4. 1280×720·100%와 1920×1080·125%에서 title/settings/gameplay의 clipping, focus, 한국어 text와
   색 이외 상태 표현을 한 번씩 눈으로 확인한다.
5. clean export로 만든 ZIP의 구조, arm64 slice, ad-hoc signature와 declared minimum을 검사한다.
6. package를 격리된 user data에서 실행해 새 게임→음량 하나 변경→저장 종료와 fresh process
   이어하기를 확인한다. 재실행 뒤 그 음량이 실제 audio bus에 적용됐는지 확인하고, 대표 package
   smoke가 FINAL에 도달한다.
7. 한 번의 짧은 독립 검토에서 열린 P0/P1을 닫고 문서·artifact hash를 현재 bytes와 맞춘다.

픽셀 golden, 여러 OS·Mac version matrix, 반복 성능 benchmark, LLM play와 외부 사람 관찰은 하지
않는다. 한 번의 package 실행에서 crash가 없고 peak memory가 512 MiB 이하이며 전체 대표 smoke가
60초 안에 끝나는지만 내부 test budget으로 기록한다.

## 9. 종료 상태

종료 전에는 이 절을 `IN_PROGRESS`로 둔다. 완료 뒤 다음만 기록한다.

- package 경로·SHA-256·앱 내부 버전과 minimum OS
- 누적 검사, 두 화면 확인, package save/resume·FINAL 결과
- 남은 known issue와 서명·공증·외부 테스트 경계
- `HumanValidationStatus = NOT_COLLECTED`
