#!/bin/zsh
set -euo pipefail

if [[ $# -ne 6 ]]; then
  print -u2 "usage: $0 CANDIDATE_BOARD RUNTIME_NORMAL RUNTIME_HEAT RUNTIME_FLOOD BOARD_RECIPE OUTPUT_JSON"
  exit 2
fi

board="$1"
runtime_normal="$2"
runtime_heat="$3"
runtime_flood="$4"
recipe="$5"
output_json="$6"
script_dir="${0:A:h}"
prompt_template="$script_dir/atomic-river-audit-prompt.template.txt"
schema="$script_dir/atomic-river-audit.schema.json"
expected_codex_version="${GRIDWORKS_JUDGE_CODEX_VERSION:-codex-cli 0.149.0}"
actual_codex_version="$(codex --version)"

if [[ "$actual_codex_version" != "$expected_codex_version" ]]; then
  print -u2 "judge transport version mismatch: expected '$expected_codex_version', got '$actual_codex_version'"
  exit 3
fi

for required in "$board" "$runtime_normal" "$runtime_heat" "$runtime_flood" "$recipe" "$prompt_template" "$schema"; do
  [[ -f "$required" ]] || { print -u2 "missing file: $required"; exit 2; }
done

board_sha="$(shasum -a 256 "$board" | awk '{print $1}')"
normal_sha="$(shasum -a 256 "$runtime_normal" | awk '{print $1}')"
heat_sha="$(shasum -a 256 "$runtime_heat" | awk '{print $1}')"
flood_sha="$(shasum -a 256 "$runtime_flood" | awk '{print $1}')"
recipe_sha="$(shasum -a 256 "$recipe" | awk '{print $1}')"
[[ "$(jq -r '.boardSha256' "$recipe")" == "$board_sha" ]] || {
  print -u2 "board hash does not match recipe"; exit 4;
}
[[ "$(jq -r '.captureSha256.normal' "$recipe")" == "$normal_sha" ]] || {
  print -u2 "normal hash does not match recipe"; exit 4;
}
[[ "$(jq -r '.captureSha256.heat' "$recipe")" == "$heat_sha" ]] || {
  print -u2 "heat hash does not match recipe"; exit 4;
}
[[ "$(jq -r '.captureSha256.flood' "$recipe")" == "$flood_sha" ]] || {
  print -u2 "flood hash does not match recipe"; exit 4;
}

mkdir -p "${output_json:h}"
prompt="$(<"$prompt_template")"
prompt="${prompt//__BOARD_SHA256__/$board_sha}"
prompt="${prompt//__NORMAL_SHA256__/$normal_sha}"
prompt="${prompt//__HEAT_SHA256__/$heat_sha}"
prompt="${prompt//__FLOOD_SHA256__/$flood_sha}"
prompt="${prompt//__RECIPE_SHA256__/$recipe_sha}"

codex exec \
  --ephemeral \
  --ignore-rules \
  --sandbox read-only \
  --model gpt-5.6-sol \
  --config 'model_reasoning_effort="ultra"' \
  --image "$board" "$runtime_normal" "$runtime_heat" "$runtime_flood" \
  --output-schema "$schema" \
  --output-last-message "$output_json" \
  "$prompt"

expected_ids='["R01","R02","R03","R04","R05","R06","R07","R08","R09","R10","R11","R12"]'
jq -e --argjson expected "$expected_ids" '
  ([.cells[].cellId] | sort) == $expected and
  ([.cells[].cellId] | unique | length) == 12 and
  all(.cells[];
    .singleCompositionUnit == true and
    .containsWholeRiverOrMap == false and
    .containsAtlasOrAlternatives == false) and
  .map.largeBakedRiverRasterPresent == false and
  .states.normalWaterVisible == true and
  .states.heatIsNarrowerAndDrier == true and
  .states.floodIsWiderAndWetter == true and
  (.criticalFailures | length) == 0 and
  .verdict == "PASS"
' "$output_json" >/dev/null || {
  print -u2 "atomic river audit hard gate failed: $output_json"
  exit 10
}

jq -n \
  --arg protocol "G3-ATOMIC-RIVER-AUDIT-v1" \
  --arg model "gpt-5.6-sol" \
  --arg mode "ultra" \
  --arg codexVersion "$actual_codex_version" \
  --arg boardSha256 "$board_sha" \
  --arg normalSha256 "$normal_sha" \
  --arg heatSha256 "$heat_sha" \
  --arg floodSha256 "$flood_sha" \
  --arg recipeSha256 "$recipe_sha" \
  '{protocol:$protocol, model:$model, codexMode:$mode, codexVersion:$codexVersion,
    boardSha256:$boardSha256, normalSha256:$normalSha256, heatSha256:$heatSha256,
    floodSha256:$floodSha256, recipeSha256:$recipeSha256}' \
  > "${output_json:r}.execution.json"

print "ATOMIC_RIVER_AUDIT_PASS model=gpt-5.6-sol mode=ultra cells=12 baked-river=false states=3"
