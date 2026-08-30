# 현재 작업 범위

## 상태

**Godot 직접 시각 배치 편집 scope가 활성 상태다.**

## 단일 결과물

개발자가 실제 Godot 게임 화면에서 건물·발전원과 도로 제어점을 마우스로 옮기고 크기를 조절한 뒤,
그 결과를 visual-only layout 데이터에 저장해 다음 실행과 제품 화면에 그대로 재현한다.

## 단일 권위

- 시각 배치 좌표·크기·도로 제어점: strict `realtime-visual-layout.v1` project data
- 실제 편집·미리보기·저장: DEBUG 전용 `RealtimePlaceholderMap` visual-layout mode

## 범위 안

- 현재 C# 하드코딩 district/source/road 좌표를 별도 strict layout 데이터로 옮긴다.
- 실제 렌더러 위에 선택 handle, drag, 크기 조절, 저장·되돌리기와 사용 안내를 제공한다.
- `./dev play layout`으로 canonical `FIRST_LIGHT` 화면을 편집 모드로 연다.
- Godot UI를 직접 조작해 배치를 저장하고 재실행·스크린샷으로 재현성을 확인한다.

## 범위 밖

- Core node·전력망 기하·반경·열·경제·story·save schema 변경
- 일반 플레이어용 건설/맵 에디터, PNG 재생성, package/release gate, push·PR·merge·배포

## 완료 검사

- strict loader의 누락·중복·범위·결정론 검사와 실제 save/reload round trip
- 편집 모드 밖 canonical 화면 불변, Godot UI 직접 drag/save 증거, `./dev check`
- 사용 문서와 scope 종료
