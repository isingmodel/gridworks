#!/bin/zsh
set -euo pipefail

if [[ $# -ne 5 ]]; then
  print -u2 "usage: $0 CANDIDATE_BOARD RUNTIME_MAP RUNTIME_DRAFT BOARD_RECIPE OUTPUT_JSON"
  exit 2
fi

board="$1"
runtime_map="$2"
runtime_draft="$3"
recipe="$4"
output_json="$5"
script_dir="${0:A:h}"
prompt_template="$script_dir/atomic-grid-audit-prompt.template.txt"
schema="$script_dir/atomic-grid-audit.schema.json"
expected_codex_version="${GRIDWORKS_JUDGE_CODEX_VERSION:-codex-cli 0.149.0}"
actual_codex_version="$(codex --version)"
[[ "$actual_codex_version" == "$expected_codex_version" ]] || {
  print -u2 "judge transport version mismatch: expected '$expected_codex_version', got '$actual_codex_version'"; exit 3;
}
for required in "$board" "$runtime_map" "$runtime_draft" "$recipe" "$prompt_template" "$schema"; do
  [[ -f "$required" ]] || { print -u2 "missing file: $required"; exit 2; }
done

board_sha="$(shasum -a 256 "$board" | awk '{print $1}')"
map_sha="$(shasum -a 256 "$runtime_map" | awk '{print $1}')"
draft_sha="$(shasum -a 256 "$runtime_draft" | awk '{print $1}')"
recipe_sha="$(shasum -a 256 "$recipe" | awk '{print $1}')"
[[ "$(jq -r '.boardSha256' "$recipe")" == "$board_sha" ]] || { print -u2 "board hash mismatch"; exit 4; }
[[ "$(jq -r '.mapSha256' "$recipe")" == "$map_sha" ]] || { print -u2 "map hash mismatch"; exit 4; }
[[ "$(jq -r '.draftSha256' "$recipe")" == "$draft_sha" ]] || { print -u2 "draft hash mismatch"; exit 4; }

mkdir -p "${output_json:h}"
prompt="$(<"$prompt_template")"
prompt="${prompt//__BOARD_SHA256__/$board_sha}"
prompt="${prompt//__MAP_SHA256__/$map_sha}"
prompt="${prompt//__DRAFT_SHA256__/$draft_sha}"
prompt="${prompt//__RECIPE_SHA256__/$recipe_sha}"

codex exec \
  --ephemeral --ignore-rules --sandbox read-only \
  --model gpt-5.6-sol --config 'model_reasoning_effort="ultra"' \
  --image "$board" "$runtime_map" "$runtime_draft" \
  --output-schema "$schema" --output-last-message "$output_json" "$prompt"

expected_ids='["G01","G02","G03","G04","G05","G06","G07","G08"]'
jq -e --argjson expected "$expected_ids" '
  ([.cells[].cellId] | sort) == $expected and
  ([.cells[].cellId] | unique | length) == 8 and
  all(.cells[];
    .singleCompositionUnit == true and
    .containsWholeFacilityOrRoute == false and
    .containsAtlasOrAlternatives == false) and
  .map.largeBakedGridFacilityRasterPresent == false and
  .map.atomicPlantAssemblyVisible == true and
  .map.individualPoleSpritesVisible == true and
  .map.plannedAmberAndEnergizedCyanDistinct == true and
  (.criticalFailures | length) == 0 and .verdict == "PASS"
' "$output_json" >/dev/null || { print -u2 "atomic grid audit hard gate failed: $output_json"; exit 10; }

jq -n \
  --arg protocol "G3-ATOMIC-GRID-AUDIT-v1" --arg model "gpt-5.6-sol" --arg mode "ultra" \
  --arg codexVersion "$actual_codex_version" --arg boardSha256 "$board_sha" \
  --arg mapSha256 "$map_sha" --arg draftSha256 "$draft_sha" --arg recipeSha256 "$recipe_sha" \
  '{protocol:$protocol, model:$model, codexMode:$mode, codexVersion:$codexVersion,
    boardSha256:$boardSha256, mapSha256:$mapSha256, draftSha256:$draftSha256,
    recipeSha256:$recipeSha256}' > "${output_json:r}.execution.json"

print "ATOMIC_GRID_AUDIT_PASS model=gpt-5.6-sol mode=ultra cells=8 baked-grid=false"
