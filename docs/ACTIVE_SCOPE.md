# 현재 작업 범위

## 상태

**current R2 macOS package identity vertical slice가 활성 scope다.**

[남은 작업](NEXT_TASKS.md)의 큰 fresh-install/E2E gate를 가장 구현하기 쉬운 두 단위로 나눈다. 이번 2A는
current R2 package bytes와 identity, 설치 디렉터리의 제품 title boot까지만 소유한다. fresh user-data의
전체 8장 qualification은 후속 2B가 소유한다.

## 결과물

- Godot이 사용하는 `ExportRelease`에서 기존 `GridworksLegacyV2Export=true`와 새
  `GridworksCurrentR2Export=true` 중 정확히 하나만 허용한다. current 선택은 strict V2 base+V3 Core와
  `realtime/r2`·`realtime/ui`만 compile하고 DEBUG fixture resource는 제외한다.
- 새 `Current R2 macOS Internal Candidate` preset은 product main scene, default audio bus와 G3 57개만
  selected-resource closure로 export한다. historical V2 scene/theme/portrait/audio는 포함하지 않는다.
- 단일 `tools/r2_candidate.py`가 `build | verify`를 소유하고 `./dev candidate build | verify`가 이를
  전달한다. 별도 schema/policy/session 계층을 만들지 않는다.
- adjacent strict `gridworks.r2-package-manifest.v1`은 clean source commit, producer/preset, Godot·.NET,
  archive bytes와 안전하게 펼친 tree identity, bundle/platform/runtime, entry scene, G3, save/settings logical
  path·schema·defaults와 고정 claim ceiling을 결속한다.
- build는 새 임시 설치 디렉터리에서 packaged executable을 user argument 없이 headless boot해 current R2
  product-title ready marker와 error-free exit를 확인한다.

## 범위 밖

- fresh user-data가 비어 있다는 주장, packaged New Game→8장→finale→epilogue production-input E2E
- packaged save/reset/settings restore와 speaker audio·청감·접근성 qualification
- 지원 OS/하드웨어 일반화, Developer ID 서명, 공증, 공개 배포와 업데이트/제거 정책
- evaluation session/capture/evidence/oracle/judge, `CommercialUXProxy`와 score-bearing 주장
- historical V2 packager·`tools/commercial-ux/native` authority의 승격 또는 재사용
- push, PR, merge

## 완료 검사

1. 두 export selector의 missing/both를 fail-closed하고 current/legacy compile graph가 섞이지 않는지 확인한다.
2. clean committed HEAD에서 package+manifest를 만들고 별도 extraction의 strict verifier가 archive/tree,
   plist·architecture·ad-hoc signature, assemblies/PCK/G3/legal files와 claim ceiling을 재구성하는지 확인한다.
3. archive 변조, manifest mismatch, duplicate/path traversal/symlink escape가 verifier에서 거부되는지 작은
   고정 mutation으로 확인한다.
4. packaged no-arg title boot가 current R2 marker로 종료하고 PDB·checkout absolute path·historical runtime
   marker가 없음을 확인한다. 이는 fresh user-data나 사람/native UX 증거가 아니다.
5. `./dev check`와 두 bounded independent review 뒤 scope-valid finding을 수정하고 current 문서·완료 이력과
   2A/2B backlog 경계를 갱신한다.
