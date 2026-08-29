# 현재 작업 범위

## 상태

**발전원 캠퍼스 시각 교체 scope가 활성 상태다.**

## 단일 결과물

서부·남부 발전원이 조립식 창고와 분리된 굴뚝처럼 보이지 않고, 주변 지형·도로·도시 시설과 같은
아이소메트릭 카메라·광원·재질·스케일을 가진 전문적인 단일 발전 캠퍼스로 보인다.

## 단일 권위

- 발전원 가시 자산·배치·anchor: `RealtimePlaceholderMap`과 project-local RGBA campus PNG

## 범위 안

- 현재 스크린샷과 원본 발전소 조각 자산의 시점·스케일·halo·조립 문제를 제거한다.
- 서부와 남부 발전원의 역할 차이가 보이는 통합 캠퍼스 자산을 만들고 기존 source node에 연결한다.
- 같은 native 장면을 다시 캡처해 발전원과 배경·도로의 조화를 확인한다.

## 범위 밖

- 발전량·열·반경 공급·경제·story·save schema·입력·오디오 규칙 변경
- 도시의 다른 시설 재설계, package/release gate, 사람 참가자 미감 승인, push·PR·merge·배포

## 완료 검사

- RGBA·pivot·footprint와 정식 asset allowlist/provenance 검사
- source campus draw fact, `./dev check`, 고정 native 스크린샷 비교와 scope 종료
