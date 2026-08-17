# Gridworks macOS 내부 출시 후보 설치

이 문서는 `Gridworks 0.1.0` macOS 내부 출시 후보용이다. 이 앱은 로컬 ad-hoc 서명만 적용했고
Developer ID 서명과 Apple 공증은 하지 않았다. 따라서 공개 배포물이 아니며, 신뢰할 수 있는
프로젝트 내부 경로에서 받은 파일만 사용하고 외부에 재배포하지 않는다.

## 확인된 환경

- 지원 후보: macOS arm64
- 미검증 deployment target: macOS 14.0
- 현재 확인 환경: macOS 26.6.1 arm64

공식 Godot template 때문에 앱 binary는 Universal 2이지만 x86_64 실행과 다른 macOS 버전은 아직
검증하지 않았다.

## 설치와 실행

1. ZIP과 별도로 신뢰할 수 있는 경로로 제공된 release record를 확인한다. 현재 저장소에서는
   `CHANGELOG.md`가 이 기록이며 상태가 `INTERNAL_RC`여야 한다. ZIP 안에는 설치 안내, 라이선스,
   크레딧과 제3자 고지가 함께 들어 있다.
2. `shasum -a 256 Gridworks-macOS-0.1.0.zip`을 실행해 그 release record의 SHA-256과 비교한다.
3. ZIP을 풀고 `Gridworks.app`을 `Applications` 또는 쓰기 가능한 로컬 폴더로 옮긴다.
4. Finder에서 앱을 control-click한 뒤 **열기**를 선택한다.

macOS가 앱을 막으면 먼저 hash와 출처를 다시 확인한다. 신뢰할 수 있는 내부 후보가 맞을 때만
**시스템 설정 → 개인정보 보호 및 보안 → 확인 없이 열기**를 사용한다. 이 경고는 Developer ID로
서명하지 않고 공증하지 않은 내부 빌드의 알려진 제한이며, 보안 기능을 전역으로 끄지 않는다.

## 저장 위치

게임은 다음 폴더에 한 슬롯 campaign 저장과 설정을 둔다.

```text
~/Library/Application Support/Godot/app_userdata/Gridworks/
  release-campaign-save-v2.json
  settings.json
```

초기 상태를 다시 확인하려면 게임을 완전히 종료한 뒤 두 파일을 별도 폴더로 옮겨 백업한다. 앱을
교체해도 이 폴더는 자동으로 지워지지 않는다.

## 범위

이 설치 안내는 위의 한 환경에서 수행하는 내부 확인만 다룬다. 빌드 환경에는 유효한 코드 서명
identity가 없으므로 Developer ID 서명과 공증을 진행할 수 없었다. 해당 자격증명, 실제 지원 OS
확인과 별도 배포 승인이 마련되기 전에는 자동 업데이트, 외부 사용자 지원과 공개 출시를 주장하지
않는다.
