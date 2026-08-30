# 현재 작업 범위

## 상태

**Godot Editor-native 시각 배치 scope가 활성 상태다.**

## 단일 결과물

개발자가 게임 실행 창이 아니라 실제 Godot Editor의 2D 뷰와 Inspector에서 건물·발전원·도로를 선택하고
이동·크기 조절한 뒤 scene 저장만으로 normal 게임 배치를 바꾼다.

## 단일 권위

- 시각 배치 위치·크기·도로점: strict `RealtimeVisualLayoutAuthoring.tscn`
- normal renderer 입력: 위 scene을 읽어 만드는 immutable visual-layout definition

## 범위 안

- JSON/게임창 overlay 중심 편집을 Godot Editor-native scene authoring으로 교체한다.
- 실제 campus sprite, river/road context, district footprint와 이름을 2D editor에 표시한다.
- Godot Editor를 직접 열어 노드를 이동하고 scene 저장 후 normal 게임 재현을 캡처한다.
- strict ID/type/style/bounds 검사와 runtime/headless 회귀를 유지한다.

## 범위 밖

- Core node·전력망 기하·반경·열·경제·story·save schema 변경
- 일반 플레이어용 editor, PNG 재생성, package/release gate, push·PR·merge·배포

## 완료 검사

- scene loader의 누락·중복·타입·범위 검사와 deterministic projection
- Godot Editor 2D/Inspector 직접 조작·scene 저장, fresh normal 재현, `./dev check`
- 사용 문서와 scope 종료
