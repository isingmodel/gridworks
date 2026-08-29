# City V2 runtime raster provenance

이 폴더의 여섯 PNG는 2026-08-29에 Codex 내장 ImageGen으로 각 scope를 위해 생성한 후보 가운데,
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
| `hospital-campus-a.png` | 본동·응급동·ambulance 진입·주차/service yard·cyan 의료 표식이 한 필지를 이루는 의료 구역 | ImageGen reference-guided 생성의 checkerboard 후보 → background extraction → 768×513 | RGBA | sprite 하단 93%를 ground anchor로 사용, 620×410 world footprint 안에 표시 |
| `waterworks-campus-a.png` | pump house·두 물탱크·여과동·배관·배수로·service yard와 진입로가 한 필지를 이루는 정수장 | ImageGen reference-guided 생성 → 768×512 | RGBA | sprite 하단 93%를 ground anchor로 사용, 610×390 world footprint 안에 표시 |
| `power-plant-west-campus-b.png` | 낮은 보일러·터빈동, 건물에 결합된 중형 굴뚝, 변압기·개폐장·우측 진입로가 한 필지를 이루는 서부 열발전 캠퍼스 | ImageGen built-in 생성 → 1544×1019 | RGBA | sprite 하단 78% ground anchor, 800 world max side로 표시 |
| `power-plant-south-campus-b.png` | 두 저층 가스터빈동, 짧은 배기 stack·열회수 배관, 변압기·개폐장·우측 진입로가 한 필지를 이루는 남부 발전 캠퍼스 | ImageGen built-in 생성 → 1612×976 | RGBA | sprite 하단 78% ground anchor, 780 world max side로 표시 |

여섯 자산은 current source-tree runtime에만 연결한다. package/export 채택, 제3자 권리 승인 또는 사람
미감 승인을 뜻하지 않는다. 병원 최초 생성본은 checkerboard가 pixel에 baked돼 직접 채택하지 않고 같은
구도의 background-extraction 결과만 사용했다. 최종 두 추가 자산은 기존 주거·산업 두 자산을 style
reference로 사용했고 실제 RGBA alpha를 별도 확인했다.

두 발전 캠퍼스는 현재 native screenshot에서 기존 main hall·turbine hall·smokestack·switchyard 조각을
겹친 결과가 분리된 창고와 과도한 굴뚝처럼 보인다는 관찰을 입력으로 삼았다. 서부 prompt는 하나의
compact thermal campus, 결합된 중형 굴뚝, 우측 개폐장과 lower-right road를 요구했다. 남부 prompt는
서부와 다른 low-profile gas-turbine campus, 세 짧은 배기 stack, heat-recovery 배관과 우측 개폐장을
요구했다. 두 prompt 모두 투명 RGBA, 35도 3/4 isometric, 좌상단 key light, charcoal/warm/cyan palette,
white halo·checkerboard·검은 판·분리 부품 금지를 포함했다.
서부 후보에서 돌출 도로만 제거한 precise-object-edit와 후속 background extraction은 checkerboard를
pixel에 포함하고 실제 alpha가 없어 모두 폐기했다. runtime은 최초 생성에서 실제 RGBA를 통과한 `-b`
후보를 유지하고, native service road 시작점을 PNG gate 끝에 맞춰 단일 접속으로 만들었다.
