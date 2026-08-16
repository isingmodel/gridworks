#!/usr/bin/env ruby
# frozen_string_literal: true

require "csv"
require "digest"
require "pathname"

ROOT = Pathname(__dir__).join("../..").expand_path
CONTRACT = ROOT.join("docs/scopes/SCOPE_0B_PLAYABLE.md")
BUILD_CHECKPOINT = ROOT.join("playtests/scope-0b/CHECKPOINT_1_IMPLEMENTATION_FREEZE.md")
RUN_CHECKPOINT = ROOT.join("playtests/scope-0b/CHECKPOINT_1E_RUN_PROTOCOL_V5.md")
SHEET = ROOT.join("playtests/scope-0b/FACILITATOR_SHEET.md")
RECORD = ROOT.join("playtests/scope-0b/record-template.csv")
FIXTURE = ROOT.join("data/scope-0b-v1.json")

def check(condition, message)
  raise "FAIL: #{message}" unless condition
end

def sha(path)
  Digest::SHA256.file(path).hexdigest
end

def ascii_whitespace_fold(text)
  utf8 = text.dup.force_encoding(Encoding::UTF_8)
  check(utf8.valid_encoding?, "prompt is not valid UTF-8")
  utf8.gsub(/[ \t\r\n]+/, " ").sub(/\A +/, "").sub(/ +\z/, "")
end

def fenced_section(markdown, heading)
  markdown[/^## #{Regexp.escape(heading)}\s*$.*?^```text[ \t]*\r?\n(.*?)\r?\n```[ \t]*$/m, 1]
end

def session_assignments
  {
    "S0B-V5-L01" => "ab",
    "S0B-V5-L02" => "ba",
    "S0B-V5-L03" => "ab",
    "S0B-V5-L04" => "ba",
    "S0B-V5-L05" => "ab"
  }
end

def expected_session_hash(checkpoint, session_id)
  row = checkpoint.lines.find { |line| line.start_with?("| `#{session_id}` |") }
  check(!row.nil?, "checkpoint assignment missing for #{session_id}")
  hash = row[/`([0-9a-f]{64})`/, 1]
  check(!hash.nil?, "checkpoint prompt hash missing for #{session_id}")
  hash
end

sheet = SHEET.read
canonical_prompt = fenced_section(sheet, "2. Exact participant prompt")
check(!canonical_prompt.nil?, "facilitator §2 participant prompt missing")
check(canonical_prompt.scan("<SESSION_ID>").length == 1, "participant prompt must contain one <SESSION_ID>")

case ARGV.first
when "--render-prompt"
  check(ARGV.length == 2, "usage: verify_implementation.rb --render-prompt SESSION_ID")
  session_id = ARGV.fetch(1)
  check(session_assignments.key?(session_id), "unknown v5 session #{session_id}")
  STDOUT.write(canonical_prompt.sub("<SESSION_ID>", session_id))
  exit 0
when "--check-transcript"
  check(ARGV.length == 3, "usage: verify_implementation.rb --check-transcript SESSION_ID PATH")
  session_id = ARGV.fetch(1)
  check(session_assignments.key?(session_id), "unknown v5 session #{session_id}")
  transcript_path = Pathname(ARGV.fetch(2)).expand_path
  check(transcript_path.file?, "transcript not found: #{transcript_path}")
  transcript_prompt = fenced_section(transcript_path.read, "Exact task message")
  check(!transcript_prompt.nil?, "transcript Exact task message fenced block missing")
  actual = Digest::SHA256.hexdigest(ascii_whitespace_fold(transcript_prompt))
  run_checkpoint = RUN_CHECKPOINT.read
  expected = expected_session_hash(run_checkpoint, session_id)
  check(actual == expected, "transcript prompt hash #{actual} != checkpoint #{expected}")
  puts "PASS transcript-prompt: #{session_id} #{actual}"
  exit 0
when nil
  # Run the complete freeze verification below.
else
  check(false, "unknown mode #{ARGV.first.inspect}")
end

contract = CONTRACT.read
build_checkpoint = BUILD_CHECKPOINT.read
run_checkpoint = RUN_CHECKPOINT.read

source_files = [
  ROOT.join("Directory.Build.props"),
  ROOT.join("global.json"),
  ROOT.join("src/Gridworks.Core/Gridworks.Core.csproj"),
  ROOT.join("game/Gridworks.Game.csproj"),
  ROOT.join("game/Main.tscn"),
  ROOT.join("game/project.godot")
] + ROOT.glob("src/Gridworks.Core/*.cs") + ROOT.glob("game/*.cs")

source_manifest = source_files.uniq.sort_by { |path| path.relative_path_from(ROOT).to_s }.map do |path|
  check(path.file?, "missing build input #{path}")
  "#{path.relative_path_from(ROOT)}:#{sha(path)}\n"
end.join
build_hash = Digest::SHA256.hexdigest(source_manifest)
expected_build_hash = build_checkpoint[/source-manifest build SHA-256:\n\s+`([0-9a-f]{64})`/, 1]
check(build_hash == expected_build_hash, "build hash #{build_hash} != checkpoint #{expected_build_hash}")
run_build_hash = run_checkpoint[/source-manifest build SHA-256:\s*`([0-9a-f]{64})`/m, 1]
check(build_hash == run_build_hash, "build hash #{build_hash} != run checkpoint #{run_build_hash}")
puts "PASS build-source-hash: #{build_hash}"

fixture_hash = sha(FIXTURE)
expected_fixture_hash = build_checkpoint[/fixture SHA-256: `([0-9a-f]{64})`/, 1]
check(fixture_hash == expected_fixture_hash, "fixture hash drift")
run_fixture_hash = run_checkpoint[/fixture SHA-256: `([0-9a-f]{64})`/, 1]
check(fixture_hash == run_fixture_hash, "fixture hash #{fixture_hash} != run checkpoint #{run_fixture_hash}")
puts "PASS fixture-hash: #{fixture_hash}"

check(contract.include?("FACILITATOR_SHEET.md") && !contract.include?(canonical_prompt),
      "active contract must reference, not duplicate, facilitator §2 prompt")
prompt_hash = Digest::SHA256.hexdigest(ascii_whitespace_fold(canonical_prompt))
expected_prompt_hash = run_checkpoint[/task-message template SHA-256: `([0-9a-f]{64})`/, 1]
check(prompt_hash == expected_prompt_hash, "prompt-template hash drift")
%w[S0B-CONTRACT-v5 S0B-PROXY-v5 S0B-RUN-v5].each do |version|
  check(run_checkpoint.include?(version), "run checkpoint version #{version} missing")
end

assignments = session_assignments
assignments.each do |session_id, variant|
  row = run_checkpoint.lines.find { |line| line.start_with?("| `#{session_id}` |") }
  check(!row.nil? && row.include?("| `#{variant}` |"), "missing assignment #{session_id}/#{variant}")
  expected = expected_session_hash(run_checkpoint, session_id)
  actual = Digest::SHA256.hexdigest(ascii_whitespace_fold(canonical_prompt.sub("<SESSION_ID>", session_id)))
  check(actual == expected, "participant message hash drift for #{session_id}")
end
puts "PASS prompt-and-assignments: #{prompt_hash}, 5 messages"

expected_sheet_hash = run_checkpoint[/facilitator-sheet SHA-256: `([0-9a-f]{64})`/, 1]
expected_record_hash = run_checkpoint[/record-template SHA-256: `([0-9a-f]{64})`/, 1]
check(sha(SHEET) == expected_sheet_hash, "facilitator sheet hash drift")
check(sha(RECORD) == expected_record_hash, "record template hash drift")

header = CSV.read(RECORD, headers: true).headers
required = %w[SessionId Variant BuildHash FixtureHash PromptHash TechnicalValid EndReason
              FaultAttribution FacilitatorHelp InteractionCompletionPass CoverageActionPass
              RiskPredictionPass UtilityBoundaryPass IntegratedInteractionPass CoverageConclusionPass
              RiskConclusionPass UtilityConclusionPass LockedRiverE1 LockedRiverOld LockedNorthE1
              LockedNorthOld SelectedCorridor FinalReached TranscriptSha256 DiagnosticSha256
              RunnerManifestSha256]
check((required - header).empty?, "record template is missing required columns")

check(sheet.include?("--accessibility always"), "launch command lacks forced accessibility")
check(sheet.include?("--resolution 1280x720"), "launch command lacks frozen resolution")
check(sheet.include?("--diagnostic-log"), "launch command lacks separate diagnostic path")
check(sheet.include?("S0B-L00") && sheet.include?("L00Status = PASS"), "L00 pass is not explicit")
check(sheet.include?("tools.mcp__node_repl__js") && sheet.include?("org.godotengine.godot"), "v5 direct transport target missing")
check(sheet.include?("Generic environment-owned tool name/signature metadata") &&
      sheet.include?("other-app contents are forbidden"), "v5 content-source boundary missing")
check(sheet.include?("Literal wrapper spelling and first-call success are not") &&
      sheet.include?("Any `TechnicalValid=false` launch"), "v5 semantic validity/replacement boundary missing")
check(sheet.include?("ascii-whitespace-fold-v1"), "v5 prompt normalization policy missing")
check(sheet.include?("participant stop/timeout is a TechnicalValid scored failure") &&
      sheet.include?("before scoring or") && sheet.include?("gameplay answer quality must not affect"),
      "v5 no-state/anti-selection boundary missing")
check(sheet.include?("<EVIDENCE_ID>") && sheet.include?("<SESSION_ID>-launch1"), "replacement evidence ID boundary missing")
ledger_policy = %w[
  EvidenceLedger\ =\ S0B-CALL-SOURCE-v1
  Coverage\ =\ ALL_CALLS_TASK_START_TO_FINAL_REPORT
  EntryFields\ =\ ORDER|TOOL|PURPOSE|CONTENT_SOURCE|REQUEST|RESULT|FRESH_STATE_FOR
  MetadataOrErrorRequest\ =\ EXACT
  MetadataResult\ =\ RETURNED_TOOL_NAMES_AND_USED_SIGNATURE_ONLY
  ErrorResult\ =\ EXACT_TEXT
  UiSequence\ =\ ACTION_THEN_FRESH_STATE
  UnusedToolDescriptions\ =\ OMIT
  FullAxOrScreenshotBody\ =\ OMIT
]
ledger_policy.each { |line| check(sheet.include?(line.tr("\\", "")), "v5 evidence policy missing: #{line}") }
check(sheet.include?("## 5. Exact post-measurement provenance export") &&
      sheet.include?("전체 tool-catalog 응답") &&
      sheet.include?("생략해도 증거 누락이 아닙니다") &&
      sheet.include?("도구를 다시 호출하지 마세요"), "v5 provenance export template missing")

game_text = ROOT.glob("game/*.{cs,tscn,godot}").map(&:read).join("\n")
check(!game_text.include?("verificationOnly"), "Game reads hidden oracle")
check(!game_text.match?(/\b(?:TODO|FIXME|HACK)\b/), "runtime contains placeholder marker")

tracked_private = `git -C #{ROOT} ls-files playtests/scope-0b/private`.lines.reject do |line|
  line.strip.end_with?("/.gitignore")
end
check(tracked_private.empty?, "private proxy evidence is tracked")
puts "PASS facilitator-record-boundary: exact files, launch, no private evidence"

checkpoint_reviewed = run_checkpoint.match?(
  /\A# Scope 0B v4 protocol result and v5 reset checkpoint\n\n> Status: \*\*REVIEWED — official v5 sessions authorized\*\*/
)
sheet_reviewed = sheet.match?(
  /\A# Scope 0B LLM UI-proxy facilitator sheet\n\n> Status: \*\*S0B-RUN-v5 REVIEWED — official sessions authorized\*\*/
)
review_recorded = run_checkpoint.match?(/- initial v5 protocol commit: `[0-9a-f]{40}`/) &&
                  run_checkpoint.match?(/- bounded independent v5 reviewers?: `(?!PENDING`)[^`]+`/) &&
                  run_checkpoint.include?("final review: `P0=0, P1=0, P2=0`") &&
                  run_checkpoint.match?(/- reviewed v5 protocol commit: `[0-9a-f]{40}`/)
check(checkpoint_reviewed && sheet_reviewed && review_recorded,
      "v5 checkpoint is not independently reviewed/authorized")

puts "Scope 0B implementation freeze: PASS"
