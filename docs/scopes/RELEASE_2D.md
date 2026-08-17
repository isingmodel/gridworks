# 2D 표현·사운드·패키징 구현 기준

> 상태: **COMPLETED**
>
> 완료 경계: 이 단계의 구현·대표 실행·종료 검토를 마쳤다. 현재 상태와 권한은 루트 README가
> 소유한다.
>
> 이 완료가 자동 승인하지 않는 것: 외부 사용자 관찰, 배포 서명·공증과 공개 출시.

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
- `tools/package_macos_internal.sh`가 Godot export를 만든 뒤 현재 macOS에서 실행 가능한 local ad-hoc
  signature를 다시 적용하고 설치·법적 문서를 ZIP root에 넣는다. 최종 artifact는 이 한 경로로만
  만든다.

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

픽셀 golden, 여러 OS·Mac version matrix, 반복 성능 benchmark, LLM play와 외부 사람 관찰은 이 단계의
완료 기준에 포함하지 않는다. 한 번의 package 실행에서 crash가 없고 peak memory가 512 MiB 이하이며
전체 대표 smoke가 60초 안에 끝나는지만 내부 test budget으로 기록한다. 단계 완료 뒤 사용자 승인으로
실행한 cold LLM 관찰 1회는 §9에 별도 후속 증거로 기록하며 aggregate gate나 사람 증거로 바꾸지 않는다.

## 9. 종료 상태

현재 종료 증거는 다음과 같다.

- 내부 ZIP: `dist/Gridworks-macOS-0.1.0.zip`
- SHA-256: `045ed65f3be85d05417cfb838acaacd08aebccd516cad89aba0a9ca3bddd5771`
- 앱: `Gridworks 0.1.0`, bundle `com.gridworks.game`, Universal 2 (`x86_64 arm64`), architecture별
  deployment target `14.0`, local ad-hoc signature
- canonical data: package `READY`의 campaign
  `9e9ec5ea0ee1d8ab5780799f308e5ebd287ccd5da2c0916aa1ec4828a0ccdedb`, fixture
  `b00b7fc9d657fd355b8741e4326d9a5297ae749de629c1763334bcca4df83f9c`가 저장소 bytes와 일치
- 누적 검사: First Light `10/664`, Second Heart `5/124`, Factory `5/378`, Heatwave `5/243`,
  Campaign Save·Settings `5/581`; Game Release rebuild `0 warning / 0 error`
- editor 대표 흐름: `PRODUCT_HEATWAVE_MAINTENANCE_SMOKE_PASS`, 기말현금 `4.660 M`, minute `1845`
- package 대표 흐름: `PRODUCT_HEATWAVE_MAINTENANCE_SMOKE_PASS`, `2.72초`, peak RSS
  `238,567,424 bytes`; package build hash
  `5dee2c8ac3bcba8652c2820a1ebecca32fcb55093f55a481627f8c8e3d6cba39`
- fresh process 저장·재개: `PRODUCT_CAMPAIGN_SAVE_LEG_PASS` 뒤
  `PRODUCT_CAMPAIGN_CONTINUE_LEG_PASS`; SFX `50%` 설정이 저장되고 fresh process audio bus에 적용됨
- 화면 확인: 1280×720 logical canvas에서 title·도움말·settings·초기·중간·최종 gameplay를 확인했다.
  1920×1080 native window 요청과 UI `125%` 조합은 같은 1280×720 logical canvas로 stretch되며,
  settings·gameplay text, focus, legend와 의도된 panel scroll을 확인했다.
- 후속 [공식 cold LLM 관찰 1회](../../playtests/release-2d/OFFICIAL_LLM_OBSERVATION_01.md): 처음 보는
  `gpt-5.6-sol` 참가자가 follow-up·도움·재시작 없이 native 캠페인을 `SUCCESS`로 끝내고 핵심
  건설·신뢰도·발전소 부지·예방정비 인과를 설명했다. 단일 관찰이므로 aggregate 판정은 없고
  `HumanValidationStatus = NOT_COLLECTED`를 유지한다.

현재 알려진 minor는 두 가지다.

1. Developer ID 서명·공증이 없어 외부 다운로드 실행은 Gatekeeper에 막힐 수 있다. 이 ZIP은 내부
   후보이며 공개 배포물이 아니다.
2. editor/movie가 자동으로 즉시 종료될 때 Godot이 code-generated audio resource `2~4`개의
   `ObjectDB` 종료 경고를 간헐적으로 남긴다. 정상 package 전체 흐름에서는 재현되지 않았고 실행
   중 누적·crash·저장 손상은 관찰되지 않았다. 외부 테스트에서 반복 누적이 보일 때 다시 연다.

독립 검토는 `P0=0, P1=2`를 보고했다. v1 설정이 매 실행마다 다시 변환되던 문제는 첫 읽기 때
canonical v2를 원자 저장하도록 고쳤고, 설치 안내는 ZIP 자기참조 없이 별도 신뢰 경로의 release
record와 hash를 대조하도록 고쳤다. 수정 뒤 누적 검사, Game build와 최종 package 흐름을 다시
확인했으며 열린 P0/P1은 없다.

최종 목표 감사에서는 PID만 쓰던 기본 진단 파일명이 아주 드물게 과거 파일과 충돌해 시작을 막을
수 있음을 추가로 발견했다. 기본 이름에 매 실행 고유값을 더하되 명시한 증거 경로의 `CreateNew`
보호는 유지했고, 재생성한 package의 전체 캠페인 대표 흐름을 다시 통과했다. 완료된 단계의 역사적
권한 문구와 이 로드맵의 내부 후보 검증 경계도 현재 상태에 맞췄다.

따라서 7단계 2D 개발과 내부 테스트 후보 제작은 완료했다. 이 결론은 위 한 환경과 대표 흐름에만
한정하며, `HumanValidationStatus = NOT_COLLECTED`다. 사람 사용성·재미·밸런스, macOS 버전 범위,
외부 설치와 공개 배포는 다음 사용자 승인과 별도 증거가 필요하다.
