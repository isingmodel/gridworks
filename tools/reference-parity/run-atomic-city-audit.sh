#!/bin/zsh
set -euo pipefail

if [[ $# -ne 4 ]]; then
  print -u2 "usage: $0 CANDIDATE_BOARD RUNTIME_MAP BOARD_RECIPE OUTPUT_JSON"
  exit 2
fi

board="$1"
runtime_map="$2"
recipe="$3"
output_json="$4"
script_dir="${0:A:h}"
prompt_template="$script_dir/atomic-city-audit-prompt.template.txt"
schema="$script_dir/atomic-city-audit.schema.json"
expected_codex_version="${GRIDWORKS_JUDGE_CODEX_VERSION:-codex-cli 0.149.0}"
actual_codex_version="$(codex --version)"

if [[ "$actual_codex_version" != "$expected_codex_version" ]]; then
  print -u2 "judge transport version mismatch: expected '$expected_codex_version', got '$actual_codex_version'"
  exit 3
fi

for required in "$board" "$runtime_map" "$recipe" "$prompt_template" "$schema"; do
  [[ -f "$required" ]] || { print -u2 "missing file: $required"; exit 2; }
done

board_sha="$(shasum -a 256 "$board" | awk '{print $1}')"
map_sha="$(shasum -a 256 "$runtime_map" | awk '{print $1}')"
recipe_sha="$(shasum -a 256 "$recipe" | awk '{print $1}')"
[[ "$(jq -r '.boardSha256' "$recipe")" == "$board_sha" ]] || {
  print -u2 "board hash does not match recipe"; exit 4;
}
[[ "$(jq -r '.mapCaptureSha256' "$recipe")" == "$map_sha" ]] || {
  print -u2 "map hash does not match recipe"; exit 4;
}

mkdir -p "${output_json:h}"
prompt="$(<"$prompt_template")"
prompt="${prompt//__BOARD_SHA256__/$board_sha}"
prompt="${prompt//__MAP_SHA256__/$map_sha}"
prompt="${prompt//__RECIPE_SHA256__/$recipe_sha}"

codex exec \
  --ephemeral \
  --ignore-rules \
  --sandbox read-only \
  --model gpt-5.6-sol \
  --config 'model_reasoning_effort="ultra"' \
  --image "$board" "$runtime_map" \
  --output-schema "$schema" \
  --output-last-message "$output_json" \
  "$prompt"

expected_ids='["C01","C02","C03","C04","C05","C06","C07","C08","C09","C10","C11","C12"]'
jq -e --argjson expected "$expected_ids" '
  ([.cells[].cellId] | sort) == $expected and
  ([.cells[].cellId] | unique | length) == 12 and
  all(.cells[]; .singleCompositionUnit == true and .visibleSolidCount == 1) and
  .map.largeBakedCityRasterPresent == false and
  (.criticalFailures | length) == 0 and
  .verdict == "PASS"
' "$output_json" >/dev/null || {
  print -u2 "atomic city audit hard gate failed: $output_json"
  exit 10
}

jq -n \
  --arg protocol "G3-ATOMIC-CITY-AUDIT-v1" \
  --arg model "gpt-5.6-sol" \
  --arg mode "ultra" \
  --arg codexVersion "$actual_codex_version" \
  --arg boardSha256 "$board_sha" \
  --arg mapSha256 "$map_sha" \
  --arg recipeSha256 "$recipe_sha" \
  '{protocol:$protocol, model:$model, codexMode:$mode, codexVersion:$codexVersion,
    boardSha256:$boardSha256, mapSha256:$mapSha256, recipeSha256:$recipeSha256}' \
  > "${output_json:r}.execution.json"

print "ATOMIC_CITY_AUDIT_PASS model=gpt-5.6-sol mode=ultra cells=12 baked-map=false"
