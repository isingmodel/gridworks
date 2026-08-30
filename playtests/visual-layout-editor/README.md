# Godot Editor-native visual-layout 직접 조작 증거

- `godot-editor-saved.png`: `./dev play layout`으로 연 실제 Godot Editor의 2D 뷰에서 campus `Sprite2D`를
  선택해 병원 Position `(2505,1390)`과 두 source의 최종 Position/Scale을 scene에 저장한 상태. 서부는
  `(225,720)`/`0.492`, 남부는 `(215,1725)`/`0.459`다.
- `godot-normal-reproduced.png`: Editor를 종료하고 `./dev play chapter FIRST_LIGHT`를 새로 열어 같은 위치를
  재현한 normal 게임 화면.

이 캡처는 2026-08-30 macOS의 실제 Godot Editor와 별도 native DEBUG game process에서 얻은 직접 조작
증거다. 사람 참가자의 미감·사용성 승인, packaged build 또는 공식 평가 점수를 뜻하지 않는다.
