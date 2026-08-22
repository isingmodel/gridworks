#!/bin/zsh
set -euo pipefail

if [[ $# -ne 7 ]]; then
  print -u2 "usage: $0 PAIR_ID REFERENCE_IMAGE CANDIDATE_IMAGE ORDER REPLICATE CRITERIA OUTPUT_JSON"
  exit 2
fi

pair_id="$1"
reference_image="$2"
candidate_image="$3"
order="$4"
replicate="$5"
criteria="$6"
output_json="$7"
script_dir="${0:A:h}"
prompt_template="$script_dir/judge-prompt.template.txt"
schema="$script_dir/judge-output.schema.json"
validator="$script_dir/validate-judgment.py"
expected_codex_version="${GRIDWORKS_JUDGE_CODEX_VERSION:-codex-cli 0.149.0}"
actual_codex_version="$(codex --version)"

if [[ "$actual_codex_version" != "$expected_codex_version" ]]; then
  print -u2 "judge transport version mismatch: expected '$expected_codex_version', got '$actual_codex_version'"
  exit 3
fi

if [[ "$order" == "REFERENCE_FIRST" ]]; then
  first_image="$reference_image"
  second_image="$candidate_image"
  first_role="REFERENCE"
  second_role="CANDIDATE"
elif [[ "$order" == "CANDIDATE_FIRST" ]]; then
  first_image="$candidate_image"
  second_image="$reference_image"
  first_role="CANDIDATE"
  second_role="REFERENCE"
else
  print -u2 "ORDER must be REFERENCE_FIRST or CANDIDATE_FIRST"
  exit 2
fi

for required in "$reference_image" "$candidate_image" "$prompt_template" "$schema" "$validator"; do
  [[ -f "$required" ]] || { print -u2 "missing file: $required"; exit 2; }
done

mkdir -p "${output_json:h}"
prompt="$(<"$prompt_template")"
prompt="${prompt//__PAIR_ID__/$pair_id}"
prompt="${prompt//__ORDER__/$order}"
prompt="${prompt//__REPLICATE__/$replicate}"
prompt="${prompt//__ATTACHMENT_1_ROLE__/$first_role}"
prompt="${prompt//__ATTACHMENT_2_ROLE__/$second_role}"
prompt="${prompt//__CRITERIA__/$criteria}"

for attempt in 1 2 3; do
  attempt_json="${output_json}.attempt-${attempt}.tmp"
  if codex exec \
    --ephemeral \
    --ignore-rules \
    --sandbox read-only \
    --model gpt-5.6-sol \
    --config 'model_reasoning_effort="ultra"' \
    --image "$first_image" "$second_image" \
    --output-schema "$schema" \
    --output-last-message "$attempt_json" \
    "$prompt" && \
    python3 "$validator" \
      "$attempt_json" "$pair_id" "$order" "$replicate" "$criteria"; then
    mv -f "$attempt_json" "$output_json"
    exit 0
  fi
  if [[ -f "$attempt_json" ]]; then
    mv -f "$attempt_json" "${output_json}.rejected-${attempt}.json"
  fi
  print -u2 "judge response rejected; retry $attempt/3 for $pair_id $order r$replicate"
done

print -u2 "judge failed validation after three attempts: $pair_id $order r$replicate"
exit 4
