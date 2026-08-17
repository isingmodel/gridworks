# Gridworks — 2D 완성 진행 체크리스트

> 이 체크리스트는 계획이며 구현 권한이 아니다.

상세 목표·규칙·제외 범위는 [2D 완성 로드맵](ROADMAP_2D.md)이 소유한다. 이 문서는 그 내용을
복사하지 않고 단계 상태와 종료 증거만 기록한다. 현재 활성 단계는
[루트 README](../README.md)만 소유한다.

## 1. 사용 규칙

- 상태는 `미승인 → 활성 → 검토 중 → 완료`만 사용한다. 사용자가 단계를 자르면 `삭제`로 기록한다.
- 한 단계가 열릴 때만 그 단계 행을 갱신한다. 미래 행은 backlog나 구현 허가가 아니다.
- 각 증거 칸은 긴 설명 대신 해당 단계 계약이나 종료 증거 한 곳을 링크한다.
- exact 숫자, fixture 내용, 긴 로그와 테스트 목록은 단계 계약·데이터·증거 문서에 둔다.
- 기계 검증, native 확인, 외부 관찰과 독립 검토를 서로 대신 쓰지 않는다.
- LLM 플레이는 로드맵이 허용한 한 번의 임시 smoke일 뿐 외부 사람 관찰 칸을 채우지 않는다.

## 2. 공통 활성화 조건

새 단계를 시작하려면 다음을 모두 만족해야 한다.

- 이전 단계가 종료됐거나 사용자가 명시적으로 삭제했다.
- 루트 README가 이 단계 하나만 활성으로 지목한다.
- 플레이어 결과, 제외 범위, 단일 데이터 권위와 완료조건을 담은 단계 계약을 승인했다.
- 새 기능이 실제 어느 장에서 사용되는지 정했다.
- 다음 단계의 schema, interface와 빈 UI를 미리 만들지 않았다.

## 3. 진행 장부

| 단계 | 상태 | 단계 계약·데이터 | 기계 검증 | native 확인 | 외부 관찰 | 독립 검토·종료 |
|---|---|---|---|---|---|---|
| 완료된 두 검증용 구현 | 완료 | [Scope 0B](scopes/SCOPE_0B_PLAYABLE.md), [Scope 1](scopes/SCOPE_1_INTERACTION.md) | 각 구현 회귀 | 각 장면 실행 확인 | 사람 증거 없음 | [개발 이력](DEVELOPMENT_HISTORY.md) |
| 1. 첫 점등 통합 | 완료 | [구현 기준](scopes/FIRST_LIGHT.md), [데이터](../data/product-first-light-v1.json) | [종료 기록](scopes/FIRST_LIGHT.md#10-현재-검사와-종료-기록) | [종료 기록](scopes/FIRST_LIGHT.md#10-현재-검사와-종료-기록) | `NOT_COLLECTED` | [독립 검토 완료](scopes/FIRST_LIGHT.md#10-현재-검사와-종료-기록) |
| 2. 병원 신뢰도·경제 | 완료 | [구현 기준](scopes/SECOND_HEART.md), [데이터](../data/product-second-heart-v1.json) | [종료 기록](scopes/SECOND_HEART.md#8-현재-검사와-종료-기록) | [종료 기록](scopes/SECOND_HEART.md#8-현재-검사와-종료-기록) | `NOT_COLLECTED` | [독립 검토 완료](scopes/SECOND_HEART.md#8-현재-검사와-종료-기록) |
| 3. 공장 수요·발전소 용량 | 완료 | [구현 기준](scopes/FACTORY_CAPACITY.md), [데이터](../data/product-factory-v1.json) | [종료 기록](scopes/FACTORY_CAPACITY.md#8-현재-검사와-종료-기록) | [종료 기록](scopes/FACTORY_CAPACITY.md#8-현재-검사와-종료-기록) | `NOT_COLLECTED` | [독립 검토 완료](scopes/FACTORY_CAPACITY.md#8-현재-검사와-종료-기록) |
| 4. 예고된 폭염·정비 | 완료 | [구현 기준](scopes/HEATWAVE_MAINTENANCE.md), [데이터](../data/product-heatwave-v1.json) | [종료 기록](scopes/HEATWAVE_MAINTENANCE.md#8-현재-검사와-종료-기록) | [종료 기록](scopes/HEATWAVE_MAINTENANCE.md#8-현재-검사와-종료-기록) | `NOT_COLLECTED` | [독립 검토 완료](scopes/HEATWAVE_MAINTENANCE.md#8-현재-검사와-종료-기록) |
| 5. 캠페인 골격·저장·기본 설정 | 완료 | [구현 기준](scopes/CAMPAIGN_SAVE_SETTINGS.md), [캠페인 데이터](../data/product-campaign-v1.json) | [종료 기록](scopes/CAMPAIGN_SAVE_SETTINGS.md#9-현재-검사와-종료-기록) | [종료 기록](scopes/CAMPAIGN_SAVE_SETTINGS.md#9-현재-검사와-종료-기록) | `NOT_COLLECTED` | [독립 검토 완료](scopes/CAMPAIGN_SAVE_SETTINGS.md#9-현재-검사와-종료-기록) |
| 6. 세 장 콘텐츠 고정 | 완료 | [구현 기준](scopes/CAMPAIGN_CONTENT.md), [캠페인 데이터](../data/product-campaign-v1.json) | [종료 기록](scopes/CAMPAIGN_CONTENT.md#8-현재-검사와-종료-기록) | [종료 기록](scopes/CAMPAIGN_CONTENT.md#8-현재-검사와-종료-기록) | `NOT_COLLECTED` | [독립 검토 완료](scopes/CAMPAIGN_CONTENT.md#8-현재-검사와-종료-기록) |
| 7. 2D 표현·사운드·패키징 | 검토 중 | [구현 기준](scopes/RELEASE_2D.md) | [종료 검토 증거](scopes/RELEASE_2D.md#9-종료-상태) | [종료 검토 증거](scopes/RELEASE_2D.md#9-종료-상태) | `NOT_COLLECTED` | 독립 검토 중 |

`필요`는 전체 제품 테스트에서 외부 formative observation을 기록할 지점이라는 뜻이다. 현재의
테스트 전 개발 목표에서는 `NOT_COLLECTED`로 남기며, 이를 통과로 해석하지 않는다. `선택`은
자동검사와 native 확인 뒤에도 사람만 답할 질문이 남을 때만 테스트 계획에 넣는다는 뜻이다.
참가자 수나 통과율 목표는 두지 않는다.

## 4. 공통 종료조건

활성 단계의 종료 기록은 다음 공통 조건을 모두 확인한다.

- 단계 계약이 정한 기계 불변식이 모두 통과했다.
- 현재 단계의 핵심 회귀와 변경한 공용 규칙에 닿는 기존 회귀가 통과했다.
- 핵심 행동을 대표 native 입력 흐름 하나로 처음부터 끝까지 수행했다.
- canonical 창에서 눈에 띄는 clipping, 기본 keyboard focus와 색 이외 상태 표현을 확인했다.
- 미해결 critical과 다음 단계가 의존하는 core-flow major가 0이다.
- 남은 비핵심 major가 있다면 수용 이유와 다시 볼 조건을 기록했다.
- 이 단계에 필요한 외부 관찰을 완료했거나, 테스트 전 개발 목표에서는 `NOT_COLLECTED`로 기록했다.
- README와 영향을 받은 문서를 현재 구현에 맞췄다.
- 한 번의 독립 검토 후 발견사항을 수정하고 종료 증거를 장부에 연결했다.

두 해상도·접근성·전체 회귀 매트릭스는 중간 단계마다 반복하지 않고 마지막 통합 단계에서 한 번
수행한다. 출시 후보는 예외 없이 열린 critical, major, softlock과 data-loss가 모두 0이어야 한다.
