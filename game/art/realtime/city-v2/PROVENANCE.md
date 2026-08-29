# City V2 runtime raster provenance

이 폴더의 두 PNG는 2026-08-29에 Codex 내장 ImageGen으로 이 scope를 위해 생성한 후보 가운데,
투명 배경·공통 3/4 isometric camera·좌상단 key light·따뜻한 practical light·읽히는 최종 silhouette
조건을 통과한 것만 채택한 runtime 자산이다. 외부 이미지나 합성 화면을 잘라 쓰지 않았다.

## 공통 reference와 생성 방식

- 참고 이미지: `assets/01-grid-construction.png`와 저장소의 기존 G3 건물군
- 생성 mode: Codex 내장 ImageGen의 reference-guided raster generation과 background extraction
- 공통 prompt 축: compact industrial-city district, elevated 3/4 isometric orthographic camera, dark
  slate/charcoal material, warm window/practical light, top-left key light, coherent ground footprint,
  no text/logo/UI/power line, isolated subject, transparent background
- 후보 탈락 기준: checkerboard가 pixel에 baked된 결과, vignette/opaque background, 서로 다른 camera,
  과도한 미세 묘사, 최종 표시 크기에서 약한 silhouette

## 채택 자산

| 파일 | 생성 prompt의 구역 요구 | 원본/최종 | alpha | runtime pivot·footprint |
|---|---|---|---|---|
| `residential-block-a.png` | 서로 다른 높이의 주택 4동과 코너 상점, 울타리·보행로·작은 안뜰·가로등이 하나의 생활권 필지를 이루는 구역 | ImageGen 1024급 후보 → 768×568 | RGBA | sprite 하단 93%를 ground anchor로 사용, 560×450 world footprint 안에 표시 |
| `industrial-campus-a.png` | 대형 생산동·보조건물·하역장·진입도로·울타리·설비 yard가 한 캠퍼스를 이루는 산업 구역 | ImageGen 1024급 후보 → 768×599 | RGBA | sprite 하단 93%를 ground anchor로 사용, 560×330 world footprint 안에 표시 |

두 자산은 current source-tree runtime에만 연결한다. package/export 채택, 제3자 권리 승인 또는 사람
미감 승인을 뜻하지 않는다. 병원·정수장은 기존 G3 atomic 자산을 같은 authored footprint와 광원 체계로
재조합하며, 별도 ImageGen 후보는 투명도 기준을 통과하지 못해 채택하지 않았다.
