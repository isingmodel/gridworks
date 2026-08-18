# Gridworks 실행·내부 후보 설치

## 현재 상용 v2 source 실행

현재 기본 게임은 상용 v2 단계 F의 `CommercialMain`이다. 오른쪽 패널 핫픽스 `36038a9`도 이
경로에 포함된다. 저장소 루트에서 다음 명령으로 실행한다.

```sh
./.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot --path game
```

처음 준비하는 checkout이라면 [README의 도구 버전과 hash](README.md#개발-도구)에 맞는 Godot
4.7.1 Mono를 `.tools/godot-4.7.1/`에 준비하고 .NET 의존성을 복원한다.

```sh
dotnet restore game/Gridworks.Game.csproj
dotnet build game/Gridworks.Game.csproj -c Debug
```

현재 source 실행은 설치·서명된 출시 후보가 아니다. 단계 G의 최종 자산·settings v3 migration,
패키징·서명·공증과 새 설치 전체 실행은 계획·미개방 상태다.

상용 v2 source 실행은 다음 사용자 폴더를 사용한다.

```text
~/Library/Application Support/Godot/app_userdata/Gridworks/
  release-campaign-save-v3.json
```

새 저장으로 시작하려면 게임을 완전히 종료한 뒤 파일을 삭제하지 말고 별도 폴더로 옮겨 백업한다.
과거 장면이 만든 `settings.json`과 v1의 `release-campaign-save-v2.json`이 같은 폴더에 남을 수 있지만
현재 `CommercialMain`의 설정 권위는 아니다. v1 저장은 v3로 자동 변환하지 않는다. strict settings
v3와 움직임 줄이기 저장은 단계 G의 계획·미개방 범위다.

## 동결된 0.1.0 내부 ZIP

아래 내용은 현재 상용 v2가 아니라 33×21 `ReleaseMain` 기술 기준선
`Gridworks 0.1.0`용 역사적 설치 기록이다. 이 ZIP은 로컬 ad-hoc 서명만 적용했고 Developer ID
서명과 Apple 공증을 하지 않았다. 공개 배포물이 아니며 상용 v2의 설치 후보로 취급하지 않는다.

### 확인된 환경

- 확인 환경: macOS 26.6.1 arm64
- 선언 deployment target: macOS 14.0, 별도 실행 미검증
- binary: Godot 공식 template의 Universal 2, x86_64 실행 미검증

### 설치

1. [변경 기록](CHANGELOG.md)의 0.1.0 artifact record와 ZIP의 SHA-256을 비교한다.
2. `shasum -a 256 Gridworks-macOS-0.1.0.zip` 결과가 record와 다르면 실행하지 않는다.
3. ZIP을 풀고 `Gridworks.app`을 쓰기 가능한 로컬 폴더로 옮긴다.
4. 신뢰할 수 있는 내부 후보임을 확인한 뒤 Finder에서 control-click하고 **열기**를 선택한다.

macOS가 앱을 막으면 hash와 출처를 다시 확인한다. 확인된 내부 후보에만 **시스템 설정 → 개인정보
보호 및 보안 → 확인 없이 열기**를 사용하고 보안 기능을 전역으로 끄지 않는다.

동결 v1은 `release-campaign-save-v2.json`과 `settings.json`을 같은 Godot 사용자 폴더에 둔다.
상용 v2 저장과 파일 이름은 다르지만 원본을 덮어쓰거나 삭제하지 않는다.

## 배포 경계

현재 저장소에는 공개 배포가 승인된 Gridworks package가 없다. Developer ID 자격증명, 공증,
지원 OS 확인, 최종 자산 license와 별도 소유자 배포 결정이 마련되기 전에는 자동 업데이트,
외부 사용자 지원이나 공개 출시를 주장하지 않는다.
