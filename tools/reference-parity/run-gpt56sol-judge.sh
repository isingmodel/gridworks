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

for required in "$reference_image" "$candidate_image" "$prompt_template" "$schema"; do
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

codex exec \
  --ephemeral \
  --ignore-rules \
  --sandbox read-only \
  --model gpt-5.6-sol \
  --config 'model_reasoning_effort="ultra"' \
  --image "$first_image" "$second_image" \
  --output-schema "$schema" \
  --output-last-message "$output_json" \
  "$prompt"
