# Gridworks 오브젝트와 표현 coverage

이 문서는 제품에 필요한 오브젝트, 현재 R2 규칙 연결과 G3 시각 기준선의 범위를 구분한다. 현재 G3
자산이 적용됐다는 사실은 모든 상태의 최종 production art와 사람 미감 검토가 끝났다는 뜻이 아니다.

## 1. 상태 용어

| 용어 | 의미 |
|---|---|
| 규칙 연결 | Release.V3 Core가 배치·공급·공사·열 상태를 계산함 |
| native 연결 | R2 controller와 presentation에서 실제로 사용함 |
| 시각 기준선 | current world raster와 code-native UI가 연결되고 자동검사됨 |
| production gap | 상태별 아트, 가독성·성능 또는 사람 검토가 남음 |

## 2. 전력 설비

| 오브젝트 | gameplay 역할 | 현재 구현·표현 | 남은 품질 항목 |
|---|---|---|---|
| 발전 접속점 | 공급 시작점, 출력 한도와 순서 | 서부 열발전·남부 가스터빈의 통합 RGBA campus, 각 campus 내부 switchyard·gate와 native service road 접속 | 출력·선택·사건 상태와 두 campus 구분의 실제 화면 검수 |
| 일반 전신주 | 저비용 경유·분기, 제한된 접속 | 규칙/native 연결, G3 standard pole | 작은 zoom의 attachment·hit target 검수 |
| 보강 전신주 | 큰 분기·합류와 접속부 한도 | 규칙/native 연결, G3 reinforced pole | 일반형과 색 없이 구분되는지 사람 검수 |
| 일반/보강 선로 | 실제 공급 경로와 열 병목 | 규칙/native 연결, code-drawn conductor와 G3 지지 구조 | 3상 cue, 교차/접속, 비상·정지 상태 polish |
| 소형/대형 배전 변전소 | 등급별 반경 R 안의 수요 직접 공급·접속 수·주기기 한도 | 규칙/native 연결, exact R/포함 수요/점선 service link와 G3 transformer/bay | 색 없이 구분되는 등급별 실루엣과 열 상태의 사람 검수 |
| 공사 초안·공사 중 | 비용·공기·완공 전 무전압 | actual/draft construction과 rail marker 연결 | scaffold·미완성 구조와 overlay 혼잡 검수 |

전신주의 열 한계는 기둥 온도가 아니라 단자·퓨즈·개폐기·분기 접속부를 추상화한 게임용 계획
한계다. 선로·접속부·변전소 주기기는 각각 현재 사용, 연속, 비상, 보호정지 상태를 가진다.

## 3. 수요와 시설

| 오브젝트 | 제품 의미 | 현재 G3 기준선 | 남은 품질 항목 |
|---|---|---|---|
| 주거지 | 생활·상업 수요와 폭염 결과 | 통합 주거 block과 소형 worker-house 구역, authored 필지·진입로 | 공급/미공급을 조명 외 cue로 사람 검수 |
| 청류의료원 | 생명 유지, 두 접속과 범람 시험 | hospital main/service campus, 의료 cross, authored footprint | 두 회선·범람·결과의 사람 검수 |
| 정수장 | 필수 서비스와 범람·정지 결과 | pump house·water tank·배관 campus와 authored footprint | 공급 상태의 사람 검수 |
| 산업단지 | 큰 수요와 공유 병목 | 통합 생산동·하역장·yard campus와 진입로 | 선택 경로와 배경 대비의 사람 검수 |

warm window light는 도시가 살아 있음을 보여 주지만 공급 판정이 아니다. Core state, world cue,
사건 지평선과 context 문장이 함께 같은 결과를 말해야 한다.

## 4. 공간과 환경

| 레이어 | gameplay 역할 | 현재 G3 기준선 | 남은 품질 항목 |
|---|---|---|---|
| 지형·필지 | footprint 합법성과 도시 밀도 | 저대비 terrain, 비반복 도시 ground plane과 구역별 authored footprint | 사람 미감·세 zoom 검수 |
| 강·제방 | 지지 설비 거부, 가공선 횡단 | water, bank, rock, bridge 조각 | clear/heat/flood 전환의 경계 가독성 |
| 도로·yard | 도시 정체성과 건설 회랑 | curve-sampled spine/branch와 구역 내장 진입로·yard, measured bridge | 사람 미감·세 zoom 검수 |
| service area | 급전된 변전소가 직접 공급할 수 있는 반경 R | 선택·배치 때 exact R, 포함 수요 bracket, 점선 service link | 건물과 도체를 가리지 않는지 사람 검수 |
| 위험구역 | 사건 사용불가와 배치 경고 | forecast pattern과 active fill 유지 | 날씨·선택·공사와 겹칠 때 구분 |
| 날씨·시간 | 사건 분위기와 상태 강조 | clear·heat·rain·storm draw 경로 | 실제 플레이 성능·대비·motion 검수 |

service area 안의 수요는 그 변전소의 직접 공급 후보다. 실제 공급에는 발전 접속점에서 변전소까지
완공·사용 가능한 급전 경로와 충분한 선로·접속부·주기기 한도가 필요하다. 변전소에서 수요처까지
별도 선로는 요구하지 않는다.

## 5. UI surface

live R2는 code-native flat stylebox를 사용한다. top HUD는 cyan 운영 chrome, 사건 지평선은 얇은 amber
rail, context/action/modal은 elevated surface, 일반·primary·tool button은 서로 다른 fill·border 위계를
가진다. G3 UI 7개는 package identity에 보존하지만 current theme mapping에는 사용하지 않는다.

context dock은 376px 기준 폭과 최대 460px 높이의 우상단 overlay이며, 요약·운영 상태·연결 수를 먼저
보이고 상세만 내부 scroll한다. 열고 닫을 때 지도는 full workspace를 유지하고 선택 구역을 다시 framing한다.

한 줄 사건 지평선은 수요·기상·공사·결정 기한·열 보호 경계를 별도 상시 패널로 나누지 않는다.
시간순 compact marker를 한 rail에 놓고 hover 또는 선택한 항목의 상세만 overlay로 연다.

남은 UI 검수는 다음과 같다.

- 실제 mouse hover popup 출현과 pointer 이탈/겹침 동작
- marker cluster의 keyboard 탐색과 screen-reader 이름
- FHD/UHD, UI 100/125/150/200%에서 world 면적과 한국어 줄바꿈
- focus 복구, Reduce Motion, 색각·grayscale 구분

## 6. 공통 생명주기

```text
초안
→ 위치·거리·접속·충돌 검토
→ 비용·공기·완공 시각·forecast 확인
→ 발주와 자금 지급
→ 공사 중(무전압)
→ 원자 시운전
→ 정상/비상 운전
→ 필요 시 보호정지·냉각·복귀
```

- 초안은 Core 상태를 바꾸지 않는다.
- 한 선로 공사의 지지점과 구간은 함께 완공된다.
- authored 사용불가와 열 보호정지는 다른 원인이며 동시에 존재할 수 있다.
- 완공 설비의 부분 철거·범용 재배선은 현재 1.0 범위가 아니다.

## 7. 필수 상태 coverage

| 상태 | world에서 필요한 표현 | UI에서 필요한 설명 |
|---|---|---|
| 정상 | class 실루엣, 안정된 통전 경로 | 현재/연속 사용량 |
| 선택 | outline, 전체 경로와 대상 | 발전원→수요, 첫 병목 |
| 초안 | footprint, 점선, 합법/거부 cue | 비용·공기·거부 원인 |
| 공사 | 미완성 구조와 무전압 | 완공 시각과 rail marker |
| 비상 | 색 외 notch/pattern | 노출 남은 시간 |
| 계획 사용불가 | 차단 pattern | 사건명·기간·대상 |
| 보호정지 | 끊긴 선/X·잠금 | 정지 원인·복귀 시각 |
| 냉각·복귀 | 점감/재연결 cue | 남은 시간과 transition |

새 시각 작업은 [비주얼 제작 명세](VISUAL_PRODUCTION_SPEC.md)의 카메라·밀도·재질·실루엣·조명·상태
기준과 [자산 안내](../../ASSET_MANIFEST.md)의 provenance 경계를 함께 만족해야 한다.

## 8. 현재 제외

원전·석탄·LNG·태양광·풍력, 배터리, 데이터센터, 전력시장, 다중 공사반, 사용자 switch, 완공망
부분 편집·철거와 자유 회전 camera는 현재 카탈로그에 넣지 않는다. reference 이미지에 보인다는
이유만으로 제품 기능이 되지 않는다.
