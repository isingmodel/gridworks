# 현재 작업 범위

## 상태

**도시 건물·배경·UI 조화 개선 scope가 활성 상태다.**

## 단일 결과물

플레이어가 기본·선택·건설 화면에서 같은 원근과 광원의 연속된 도시 구역을 읽고, 고유 시설과 전력 설비,
상태와 조작 UI를 서로 혼동하지 않는 전문적인 current R2 화면을 얻는다.

## 단일 권위

- 월드 시각 구성·건물 footprint/anchor/depth·상태 overlay: `game/realtime/r2/RealtimePlaceholderMap*.cs`
- 반응형 surface 크기와 선택 패널 내용: `game/realtime/ui/*`, `game/realtime/r2/RealtimeContextPresenter.cs`
- 채택 runtime raster와 UI skin: `game/art/commercial/g3/**`, `game/RealtimeTheme.tres`, `ASSET_MANIFEST.md`

## 범위 안

- 이전 native UI review에서 확인한 건물 삼중 중복, 반복 격자, 혼합 원근·비율, 과도한 축소, 약한 부지와
  도로 연결, 시설 의미 혼동, 고정 대형 inspector·선택 후 clipping, 과한 metal chrome, 낮은 정보 대비를
  빠짐없이 목록화하고 수정한다.
- 동일 카메라·광원·최종 표시 크기의 건물/구역 자산만 채택하고 provenance·사용 경계를 기록한다.
- normal, selected asset, construction 상태와 FHD/UI scale의 결정론 smoke 및 fresh native 직접 관찰을 갱신한다.

## 범위 밖

- Core gameplay 규칙·경제·chapter/story·save schema 변경
- 새 설비 class, 자유 회전 camera, 신규 gameplay 또는 오디오 제작
- 사람 미감·사용성 승인, 공식 평가 점수, package·외부 release gate
- push, PR, merge, 공개 배포

## 완료 검사

- G3 asset/load/layer/anchor와 responsive layout/context presentation의 가장 가까운 smoke가 새 불변조건을 증명한다.
- `./dev check`가 통과하고 normal·selected·construction native 화면을 fresh process에서 직접 비교한다.
- 채택 자산 provenance와 current 시각 사실의 소유 문서만 갱신하고 major unit·scope closure를 commit한다.
- 사람 시각 검토는 수집하지 않았음을 명시한다.
