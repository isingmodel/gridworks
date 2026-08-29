# 현재 작업 범위

## 상태

**변전소 반경 공급과 도시 가독성 scope가 활성 상태다.**

## 단일 결과물

발전원에서 가동 중인 변전소까지 완공된 망이 이어지면 그 변전소가 등급별 반경 R 안의 수요를 직접
공급하고, 플레이어가 같은 반경·공급 관계와 도시 시설을 전문적인 native 화면에서 즉시 구분한다.

## 단일 권위

- 변전소 반경 공급 자격·선택 경로·설비 사용량: Release V3 Core의 `RealtimeSupplyAllocator`
- 위 Core 사실의 화면 의미: typed realtime presentation과 draw-only `RealtimePlaceholderMap`

## 범위 안

- 공급 경로를 발전 접속점→가동 변전소에서 끝내고, 반경 R 안 수요에는 별도 수요측 선로를 요구하지 않는다.
- 반경 경계·밖, 변전소 사용불가·보호정지, 여러 변전소, 용량·열 병목의 결정론적 선택과 진단을 고정한다.
- 선택 수요/변전소에서 공급 변전소, 반경 R, 서비스 구간과 실제 통전 선로를 혼동 없이 표시한다.
- 고정 native 장면의 변경 전 스크린샷을 회의적인 게임 디자인 리뷰에 제공하고, 건물·배경·설비·UI의
  scope-valid 지적을 반영한 뒤 같은 장면을 다시 확인한다.

## 범위 밖

- 경제·공사 시간·chapter/story·save wire schema·입력 체계·오디오 규칙 변경
- 새로운 배전선 모델, 범용 완공망 편집기, 새 chapter·시설 추가
- 사람 참가자 미감 승인, 공식 UX 점수, package/release gate, push·PR·merge·배포

## 완료 검사

- Release V3 Core의 반경 안·경계·밖, 유선 수요측 선로 없음, 다중 변전소·열/용량·사용불가 accepted/rejected 검사
- owning presenter/map 검사와 고정 native normal·selected·construction 스크린샷 비교
- 회의적인 독립 리뷰의 scope-valid finding 반영, named checkpoint와 `./dev check`
- 실제로 바뀐 제품·구조·자산 사실의 소유 문서 갱신과 scope 종료
