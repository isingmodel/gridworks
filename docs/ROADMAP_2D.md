# Gridworks — 상용 2D 게임 완성 로드맵

> 문서 상태: 현재 계획

이 로드맵은 기술 기준선을 보이는 격자 없는 상용 2D 전력망 게임으로 교체하는 제작 순서를 정한다.
현재 단계와 구현 권한은 루트 [README](../README.md), 정확한 규칙과 종료조건은
[상용 2D 게임 구현 계약](scopes/COMMERCIAL_2D_IMPLEMENTATION.md), 제품 경험은
[상용 재기획서](product/COMMERCIAL_2D_GAME_DESIGN_PLAN_KO.md), 증거 상태는
[체크리스트](ROADMAP_2D_CHECKLIST.md)가 소유한다.

## 1. 출발점

동결된 `release v1` 기준선은 분기·합류 그래프, 공유 용량, 사건 projection, 직접 건설, 저장·재개와
내부 macOS package를 증명했다. 그러나 셀 격자, 고정 정격, 한 번의 장 판정과 반복되는 콘텐츠 때문에
상용 제품의 공간 선택·운영 리듬·이야기 결과가 부족하다.

새 제품은 기준선을 제자리 확장하지 않는다. 별도 v2 world·campaign·Core·장면에서 다음 기반을
만들고, 기존 경로는 회귀로 보존한다.

- 고정소수점 자유 배치와 점유영역·수면·건물·구간 기하
- 선로·변전소·전신주 접속부의 연속·비상 열 한계
- 작성된 운영 국면, 보호정지·냉각과 모든 공개 국면 미리보기
- 안전 의무, 선택 가능한 도시 약속, 실제 기한과 최근 공사 복구
- 같은 망을 이어 쓰는 프롤로그 세 임무와 본편 다섯 장
- 실제 망 상태를 기억하는 결과·에필로그와 상용 수준의 2D 표현·사운드

## 2. 제작 원칙

- 단계 B→C→D→E/F→G 순서로 한 단계씩 연다.
- 한 단계의 코드·데이터·검사·native 증거·문서·독립 검토를 커밋한 뒤 다음 단계로 간다.
- v2 world와 campaign이 실행 숫자와 콘텐츠를 한 곳씩 소유한다.
- Core가 규칙을 계산하고 Game은 typed 결과만 입력·표현한다.
- 임무 5 이후 새 건설·열 규칙을 추가하지 않는다.
- 검증은 대표 성공·경계·반례로 제한하고 재미를 검사 수로 대신하지 않는다.
- 사람 관찰·전문 교정과 공개 서명·공증은 구현 완료 뒤 단계 H에서 별도로 수행한다. LLM 관찰은
  단계 H의 의무가 아니며 사용자가 별도로 요청할 때만 수행한다.

## 3. 단계 B — 자유 좌표 기반 — 완료

### 플레이어 결과

격자와 셀 좌표 없이 도시 지형 위에 전신주·변전소를 놓고 직선 선로를 직접 잇는다. 화면 확대나
해상도가 달라도 같은 세계 위치는 같은 비용·기간·배치 결과를 낸다.

### 포함

- 1 설계 단위 = 100 내부 단위인 정수 고정소수점 좌표
- 올림 정수제곱근 거리, 설비 원형 점유영역과 지도 bounds
- 수면·건물·설비 접촉 거부, 위험구역 노출
- 양의 길이, 최대 경간, 제3 설비 관통, 중복·공선 겹침과 교차 비접속
- 화면 거리 자석 맞춤과 `Q / E` 후보 순환
- 전체/1.5/2.25 세 줌, pan, Home, 키보드 cursor-follow
- 첫 불빛의 작은 무벌점 자유 배치 흐름

### 종료

strict spatial fixture와 geometry·construction 검사가 통과하고, 1280×720·UI 125% 실제 viewport에서
마우스·키보드 자유 배치와 카메라를 한 번 확인한다. 열 schema와 캠페인은 만들지 않는다.

## 4. 단계 C — 이산 열 국면 기반 — 완료

### 플레이어 결과

선택 설비와 수요 경로에서 `현재 사용 / 연속 한계 / 비상 한계 / 다음 경계 상태`를 읽고, 짧고
뜨거운 경로와 길지만 계속 쓸 수 있는 경로를 비교한다.

### 포함

- 선로 도체, 변전소 주기기와 전신주 접속부의 연속·비상 한계
- 연속 운전, 비상 운전, 다음 국면 보호정지, 한 국면 냉각 뒤 복귀
- 누적 예약을 반영한 모든 발전원·단순 경로의 결정론적 열 우선 순서
- 평상 안전·운영 기록, 도시 약속과 이름 붙은 비상 안전 의무의 서로 다른 비상 사용 권한
- 작성 국면의 수요·사고·한계 override와 전체 공개 국면 pure preview
- 수요별 공급원·경로·최소 열여유와 첫 병목 typed 결과

### 종료

연속·비상 경계, 공유 합산, 경로 순서, permission, 정지·냉각·복귀와 preview=commit을 자동검사하고
작은 native 열 UI 흐름을 한 번 확인한다. 실제 온도·연속 축열·수동 급전은 만들지 않는다.

## 5. 단계 D — 상용 핵심 흐름 — 완료

### 플레이어 결과

짧은 `첫 불빛` prelude 뒤 4장 완료 상태의 `누구의 여유인가`에서 산업 약속을 지키기 위해 비상
열여유를 쓸지, 약속을 미루고 다음 안전 국면의 망을 남길지 선택한다.

### 포함

- 최대 세 decision window와 작성 operating phases
- 안전 의무, 도시 약속, 운영 기록
- 공사기간과 authored deadline, active construction 하나
- 발주 전 가상 완공의 모든 공개 국면 비교
- 최근 완공 project bundle 직전으로 journal replay 복구
- 실제 발전원·경로·비상 운전·보호정지·약속을 말하는 결과
- 최종에 가까운 지도, 문체, 환경음과 상태 cue

### 종료

두 유효 설계 원형, 약속 두 선택, deadline 경계, 대표 실패, 복구와 실제 결과를 자동·native 흐름으로
고정한다. 사람 이해·재미는 `NOT_COLLECTED`로 남기되 사용자의 전체 구현 지시에 따라 제작을 계속한다.

17 suites / 682 assertions와 기본 장면의 1280×720·UI 125% 실제 입력 smoke로 핵심 흐름을 닫았다.
독립 검토의 P1 4건은 strict 수요 terminal, authored story·prelude 결과, 실제 결과 사실과 오디오
cue 경계를 보강해 모두 수정했으며 열린 P0/P1은 없다. 사람 상태는 `NOT_COLLECTED`다.

## 6. 단계 E — 첫 네 임무와 공통 UX — 완료

1. `첫 불빛`: 자유 배치, 자석 맞춤, 카메라, 수면과 완공 전 무전압
2. `두 번째 심장`: 두 회랑, 선 교차와 실제 접속, 범람 안전 차단시험
3. `두 번째 전원`: 남부 전원, 경로 전체 연속 한계와 공유 접속부
4. `북안의 약속`: 서비스권역, 미래 분기 공간과 첫 도시 약속

안내는 장마다 줄어든다. 3장까지 비상 열 사용을 열지 않고, 4장 briefing에서 다음 더운 저녁을
예고한다. 공통 저장·복구, 의무·약속 UI, story/result와 한국어 표현을 같은 경로에 연결한다.

## 7. 단계 F — 후반 네 임무와 에필로그 — 완료

5. `누구의 여유인가`: 더운 저녁, 산업 야간 증산과 다음 국면 보호정지
6. `물이 닿기 전에`: 기한, 범람 밖 회랑과 동부 생활권 약속
7. `꺼야 지킬 수 있다`: 서부 전원 계획정지와 남은 경로 열여유
8. `가장 긴 밤`: 최대수요→폭염 정점→계산형 보호정지와 범람

5장 뒤 새 rule enum이나 조작을 추가하지 않는다. 각 본편 장은 checker-owned 유효 설계 원형 두 개,
대표 실패와 현재 장 또는 최근 공사 복구를 가진다. 실제 장별 약속 결과와 마지막 망 상태를 에필로그에
최대 세 줄의 사실로 회수한다. 전체 저장·fresh resume·완료 저장 재개와 장 시작 재도전을 닫는다.

## 8. 단계 G — 시청각·접근성·패키징 마감 — 완료

### 표현

- 도시 지도와 장별 조명·날씨·강 수위·정비 표식
- 주거·의료원·정수장·산업단지의 두세 상태 반응
- 열 상태의 색 외 선 모양·pattern·icon·문장
- 네 인물의 작은 고정 초상과 일관된 card 색
- 환경·날씨, 발주·완공·통전·차단·경고·결과 cue와 두 짧은 motif

### 접근성·저장

- 최소 1920×1080 × UI 100/125; 1280×720은 지원하지 않음
- 마우스·키보드 동등성, focus 복원, 한국어 glyph, 움직임 줄이기
- Title, Pause, 도움말, 설정, atomic 저장·재개·최근 공사·장 재시작
- 설정 v3의 화면·세 음량·움직임 줄이기와 strict v2 one-step migration

### 패키지

clean committed checkout에서 v2 데이터와 새 제품 scene만 포함한 macOS 내부 ZIP을 만든다. 저장소 밖
새 user-data에서 저장→fresh continue→전체 캠페인을 실행하고 archive/data/build hash, license,
Universal binary와 서명 경계를 기록한다. Developer ID·공증·실제 지원 OS 증거가 없으면 공개 배포를
주장하지 않는다.

## 8.1 단계 G.1 — 소유자 시각 정렬 수정 — 완료

2026-08-21 소유자 실행 검토에서 `assets/` 콘셉트와 실제 화면 사이의 큰 거리, 사건 timeline bar
누락을 확인했다. Core 규칙·v2 campaign·save·열 계산은 동결하고 다음 표현만 다시 연다.

- 네 콘셉트의 산업 도시 질감, 청록 통전망, 호박색 계획선, 큰 시설 실루엣과 금속 frame 위계를
  직접 복제하지 않는 자체 runtime plate·code-native overlay로 번역한다.
- 상단 resource HUD, 넓은 도시 지도, 오른쪽 compact inspector, 하단 사건 흐름 bar의 위계를 만든다.
- 사건 bar는 `브리핑 → 작성된 결정 경계/운영 국면 → 결과`를 campaign 권위에서 만들고 현재·완료·
  다음 상태, 공사 경과/기한을 색 외 marker와 접근성 문장으로 표시한다. 배속·실시간 simulation은
  추가하지 않는다.
- 1920×1080 UI 100/125 actual-input evidence를 다시 만들고, 1280×720 지원은 열지 않는다.

## 8.2 단계 G.2 — 개별 tile·object 자산 교체 — 기술 완료·소유자 시각 거부

G.1의 whole-map plate 방식은 소유자 검토에서 거부됐다. 전체 배경 한 장을 제거하고 다음 개별
runtime art만 사용한다.

- seamless ground·water·building tile을 별도 PNG로 만들고 정확한 v2 terrain에 반복 배치한다.
- 발전·전신주·교량 기초·변전·주거·의료·정수·산업 object를 transparent PNG로 각각 만들고 실제
  node 좌표·class에 연결한다.
- 기존 typed 전력·열·선택 상태를 sprite tint·outline·pattern과 문장으로 유지한다.
- package에 개별 자산이 모두 있고 whole-map plate와 source concept이 없는지 검사한다.
- 1920×1080 UI 100/125 actual-input evidence, 회귀·package·독립 검토를 다시 닫는다.

완료 후보는 네 `assets/` reference를 직접 사용해 같은 낮은 3/4 사선 산업도시 언어로 tile 7종과
transparent object 9종을 개별 생성했다. v2 terrain·node·class와 live construction draft에 각각
연결했으며 whole-map plate는 제거했다. CommercialChecks 22 suites / 2,024 assertions, 동결 회귀,
Debug·Release build, 1920×1080 actual-input 화면 5장, clean macOS package와 exact-tree 독립 검토
P0/P1/open 0으로 종료했다. 1280×720은 실행·검수하지 않았다.

2026-08-21 소유자 실제 실행 screenshot은 자산 binding과 달리 camera, 도시 밀도, object scale,
강물·제방, 전력망과 UI가 `assets/01~04`와 크게 다르다고 확인했다. 따라서 이 단계의 package는 현재
시각 후보가 아니며 [G.3 계획](product/COMMERCIAL_2D_REFERENCE_PARITY_PLAN_KO.md)이 대체 후보를
정의한다. 이 기록 당시에는 구현 권한이 아니었고, 아래 G.3 활성 기록이 이를 대체한다.

## 8.3 단계 G.3 — 레퍼런스 정렬 시각 재구축 — 활성

- fixed 2:1 isometric transform과 inverse input
- 개별 diamond terrain·river bank·road·district·functional object
- 굽은 강, 평상·폭염·범람 수면과 foundation 접합
- full-bleed map, reference scale grid, HUD·inspector·128px event timeline
- 48개 개별 runtime raster의 provenance·asset-kit sheet와 whole-map 합성 우회 금지
- [평가 프로토콜](product/REFERENCE_PARITY_EVALUATION_PROTOCOL_KO.md)의 세 multimodal LLM jury
  checkpoint, 10개 고정 pair·bias calibration과 근거 좌표가 있는 차이 보고서

2026-08-21 사용자는 개별 asset과 게임 구현의 근본 수정, 고정 LLM jury `ReferenceParity >96`까지의
반복 개선을 승인했다. 720p는 열지 않고 1920×1080 UI 100%·125%만 검수한다.

## 9. 단계 H — 외부 검증과 공개 후보 — 구현 뒤 별도 게이트

- 상용 핵심 구간 사람 관찰
- 소유자 전체 캠페인 플레이와 한국어 전문 교정
- 실제 지원 환경 확인, Developer ID 서명·공증과 공개 배포 결정
- 새 설치에서 사람이 완주한 bytes와 동일한 공개 후보

이 단계의 사람 결과는 G.3 LLM visual jury나 자동 증거에 합산하지 않는다. 현재 활성 구현은 G.3이며
H 상태는 `NOT_COLLECTED` 또는 외부 자격증명 차단으로 남긴다.

## 10. 전역 제외

- 두 번째 도시, sandbox, 절차 생성, 사용자 제작 지도
- 실시간 수동급전, 확률 고장, AC 전력조류, 전압·무효전력·보호계전
- 여러 공사반, 공사 queue·배속, 자재·인력·연료·전력시장·기술 트리
- 실제 섭씨, 0~100 축열, 가변 냉각·열화·화재
- 곡선 선로, 자동 경로·전신주, line-body snap, 범용 완공망 편집기
- 연속 줌·회전·관성·미니맵
- 대화 선택지, 호감도, 분기 결말 엔진, 긴 컷신·음성
- 온라인, 업적, 순위표, 다국어·게임패드·모바일 동시 출시
- 반복 LLM 플레이와 자동 밸런스 튜닝
