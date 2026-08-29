# 변전소 반경 공급·도시 시각 형성평가

## 증거 성격

- 검토자: GPT-5.6-sol xhigh 하위 에이전트의 2회 skeptical game-design review
- 입력: `before-first-light.png`, `before-selected.png`, `before-substation-draft.png`
- 수정 후 캡처: `after-first-light.png`, `after-substation-draft.png`
- 보조 진단 캡처: `after-pole-top-route.png`는 확대 전 경로 확인본이며 최종 미감 판정 입력이 아니다.
- 이 기록은 LLM 형성평가다. 사람 참가자 UX·미감 증거나 공식 출시 승인이 아니다.

## 지적 사항과 처리

| 우선 | 지적 사항 | 처리 결과 | 검증 |
|---|---|---|---|
| P0 | 변전소 반경 R이 보이지 않아 포함/경계 밖 수요와 거리/R을 판단할 수 없음 | 배치·선택 때 exact R 점선 원, 저알파 면, `R n · 수요 n곳`, 포함 수요 bracket을 표시 | DEBUG draw fact + UI harness |
| P0 | 발전소→수요처 실선과 도로가 충돌하고 서비스 의미가 불명확함 | 발전소→변전소만 3상 실선, 변전소→수요는 점선 service link와 check glyph로 분리 | allocator 9단언 + UI harness |
| P0 | 변전소 초안에 ghost·footprint·bay가 없음 | 실제 footprint diamond, 점선 경계, 반투명 transformer ghost, 수용 반경을 한 번에 표시 | DEBUG draft fact + 캡처 |
| P0 | 전신주 사이 선이 꼭대기 attachment에 닿지 않는 것처럼 보임 | pole class별 sprite 높이로 상단 anchor를 계산하고 세 가닥 sag conductor를 그리며 ground→anchor 차이를 자동검사 | `PoleConductorsUseRaisedAttachmentsForSmoke` |
| P1 | 건물들이 세로로 고립되고 서로 도시 블록처럼 묶이지 않음 | 5개 구역을 zigzag block으로 재배치하고 동·서안 9개 curve-sampled spine/branch, 분할 ground ward, 구역 foundation·진입로로 묶음 | district/road draw facts + 캡처 |
| P1 | ward/source reserve 외곽선이 타일 경계처럼 보이고 발전소가 별도 섬처럼 남음 | 외곽선을 제거하고 알파를 낮췄으며 발전소 service road와 도시 ward의 재질·명도를 같은 municipal landscape 안에 정리 | 캡처 |
| P1 | 대형 campus 사이가 비어 있어 landmark sticker처럼 보임 | 저대비 상가·주택·작업장·창고 7동, 주차/service court 6곳과 가로등을 빈 블록에 배치 | 41-file draw palette + 캡처 |
| P1 | 발전소 캠퍼스가 반복적으로 보임 | 서부는 굴뚝 중심 발전소, 남부는 낮은 turbine/switchyard 구성을 유지하고 서로 다른 massing으로 표시 | 캡처 |
| P1 | 동부 생활권이 산업시설처럼 보이고 시설 silhouette가 서로 구분되지 않음 | 주거 통합 block, 병원 통합 campus, 정수장 tank/filtration campus, 산업단지 yard campus를 구분; 구역 glyph·이름 badge 추가 | 4개 city-v2 asset/label draw |
| P1 | 병원·정수장 건물이 배경과 재질·광원·검은 바닥에서 어긋남 | 동일 reference palette/camera/light로 병원·정수장 투명 campus를 생성하고 공통 저대비 parcel·city plane에 합성 | alpha PNG + provenance + 캡처 |
| P1 | 강이 직선이고 교량이 단순한 판처럼 보임 | 다중 주파수 bank contour의 주 굴곡을 확대하고 두 교량을 양안 길이에 맞춘 deck·abutment·rail·차선으로 렌더링 | bank deviation + measured bridge facts |
| P1 | 도로와 교량·시설 진입로가 끊겨 보임 | 두 발전소 service road→북·남 교량 landing→도시 spine→각 campus driveway까지 이어지는 9개 연속 곡선 경로로 재구성 | road path count + 캡처 |
| P1 | 모든 도로 폭·포장·차선이 비슷해 hierarchy가 약함 | 발전소 concrete service road, 주 city spine, 주거 branch, 산업 access의 폭·shoulder·surface·lane 강도를 분리 | 캡처 |
| P1 | 변전소 ghost가 주거 건물과 겹쳐 배치 가능 위치가 불명확함 | 비어 있는 parcel에서만 최종 draft를 촬영하고 footprint·bay·반경·포함 수요·발주 견적을 한 상태로 표시 | `after-substation-draft.png` |
| P1 | context가 세계를 막고 하단 회색 빈 띠가 큼 | map interaction을 workspace 전체 높이로 확장해 context/action/build panel 뒤까지 도시가 이어지게 함 | responsive layout harness + 캡처 |
| P1 | 변전소 선택 context가 과밀해 실제 선택 시 4개 제한을 넘고 예외가 남음 | 핵심 R/포함/공급 요약은 고정하고 시설 목록을 route 상세로 이동; 모든 열 상태에서 요약 1–4개를 회귀검사 | realtime smoke + UI harness |
| P1 | HUD 위계가 약하고 목표가 기존 3단계 부하선 건설을 요구함 | `1/2 반경 R 변전소`→`2/2 발전소→변전소`로 축약하고 ready/debrief 문구를 새 규칙에 맞춤 | native smoke |
| P2 | 작은 상태 marker·반복 green diamond·silhouette와 안 맞는 outline | 반복 장식 대신 구역별 identity badge, 시설 terminal glyph, 실제 footprint와 상태 border로 정리 | 캡처 |
| P2 | 전신주가 작고 상태 marker가 하드웨어를 가림 | standard/reinforced pole 크기와 top anchor를 높이고 overlay 순서를 구조물·도체 뒤로 고정 | renderer smoke; 보조 캡처 |
| P2 | 산업단지의 밝기와 ground noise가 경쟁함 | ground texture modulation·variation을 낮추고 city plane을 강화해 building mass와 도로를 우선시함 | 캡처 |

## 재감사 결론

- 첫 감사는 기능은 GO, 시각은 NO-GO였다. 남은 이유는 ward seam, campus 고립, 약한 도로 위계,
  주거와 겹친 ghost, 작은 전신주와 marker 간섭이었다.
- 같은 검토자의 두 번째 고정 캡처 감사는 건물·배경 조화, 배치, 시설 구분, 도로 위계, 남부 접근로,
  강·교량, 발전소 campus, 변전소 ghost, UI를 모두 해결된 것으로 판정했다.
- 요청된 조화·전문적 조직성 범위에는 남은 P0/P1이 없으며 최종 판정은 **GO**다.
- 최종 두 캡처에는 완공된 전신주 경로가 크게 나오지 않아 pole-top 접속의 최종 미감은 재판정하지 않았다.
  이 항목은 작은 보조 캡처와 deterministic raised-attachment renderer 검사만 근거로 둔다.

## 보존한 경계

- fixed isometric camera, charcoal/warm/cyan palette, 한 줄 사건 rail, 한 개 primary CTA는 유지했다.
- 경제·공사시간·story 순서·save schema·입력·audio는 변경하지 않았다.
- 변전소→수요처를 다시 유선으로 연결하는 모델은 도입하지 않았다.
