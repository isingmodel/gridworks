# 현재 작업 범위

## 상태

**Godot Editor 직접 campus polish scope가 활성 상태다.**

## 단일 결과물

실제 Godot Editor를 열어 서부·남부 발전원 campus의 과대한 시각 무게를 줄이고 service road 접점을 유지한
배치를 scene에 저장하며, 작업 후 Editor를 열린 상태로 남긴다.

## 단일 권위

- 발전원 시각 위치·크기: `RealtimeVisualLayoutAuthoring.tscn`의 source `Sprite2D` transform

## 범위 안

- Godot Editor Scene tree/Inspector에서 두 source Position과 uniform Scale을 직접 수정한다.
- normal `FIRST_LIGHT` 재현과 strict projection·회귀를 확인한다.
- 최종 authoring scene을 실제 Godot Editor에 열린 상태로 남긴다.

## 범위 밖

- Core source 좌표·출력·열·반경·경제·story·save schema, PNG 변경
- 다른 district/road 재설계, package/release gate, push·PR·merge·배포

## 완료 검사

- 두 source의 strict size/bounds와 service-road 접점 visual 확인
- fresh normal 재현, targeted UI/check, 문서·scope 종료
