#!/usr/bin/env ruby
# frozen_string_literal: true

require "csv"
require "digest"
require "pathname"

ROOT = Pathname(__dir__).join("../..").expand_path
CONTRACT = ROOT.join("docs/scopes/SCOPE_0B_PLAYABLE.md")
BUILD_CHECKPOINT = ROOT.join("playtests/scope-0b/CHECKPOINT_1_IMPLEMENTATION_FREEZE.md")
RUN_CHECKPOINT = ROOT.join("playtests/scope-0b/CHECKPOINT_1B_RUN_PROTOCOL_V2.md")
SHEET = ROOT.join("playtests/scope-0b/FACILITATOR_SHEET.md")
RECORD = ROOT.join("playtests/scope-0b/record-template.csv")
FIXTURE = ROOT.join("data/scope-0b-v1.json")

def check(condition, message)
  raise "FAIL: #{message}" unless condition
end

def sha(path)
  Digest::SHA256.file(path).hexdigest
end

contract = CONTRACT.read
build_checkpoint = BUILD_CHECKPOINT.read
run_checkpoint = RUN_CHECKPOINT.read
sheet = SHEET.read

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
puts "PASS build-source-hash: #{build_hash}"

fixture_hash = sha(FIXTURE)
expected_fixture_hash = build_checkpoint[/fixture SHA-256: `([0-9a-f]{64})`/, 1]
check(fixture_hash == expected_fixture_hash, "fixture hash drift")
puts "PASS fixture-hash: #{fixture_hash}"

contract_prompt = contract[/아래 code block.*?```text\n(.*?)\n```/m, 1]
sheet_prompt = sheet[/## 2\. Exact participant prompt.*?```text\n(.*?)\n```/m, 1]
check(!contract_prompt.nil? && contract_prompt == sheet_prompt, "facilitator prompt differs from contract")
prompt_hash = Digest::SHA256.hexdigest(contract_prompt)
expected_prompt_hash = run_checkpoint[/task-message template SHA-256: `([0-9a-f]{64})`/, 1]
check(prompt_hash == expected_prompt_hash, "prompt-template hash drift")

assignments = { "S0B-V2-L01" => "ab", "S0B-V2-L02" => "ba", "S0B-V2-L03" => "ab",
                "S0B-V2-L04" => "ba", "S0B-V2-L05" => "ab" }
assignments.each do |session_id, variant|
  row = run_checkpoint.lines.find { |line| line.start_with?("| `#{session_id}` |") }
  check(!row.nil? && row.include?("| `#{variant}` |"), "missing assignment #{session_id}/#{variant}")
  expected = row[/`([0-9a-f]{64})`/, 1]
  actual = Digest::SHA256.hexdigest(contract_prompt.sub("<SESSION_ID>", session_id))
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
check(sheet.include?("tools.mcp__node_repl__js") && sheet.include?("org.godotengine.godot"), "v2 direct transport target missing")
check(sheet.include?("ALL_TOOLS") && sheet.include?("forbidden"), "v2 catalog lookup prohibition missing")

game_text = ROOT.glob("game/*.{cs,tscn,godot}").map(&:read).join("\n")
check(!game_text.include?("verificationOnly"), "Game reads hidden oracle")
check(!game_text.match?(/\b(?:TODO|FIXME|HACK)\b/), "runtime contains placeholder marker")

tracked_private = `git -C #{ROOT} ls-files playtests/scope-0b/private`.lines.reject do |line|
  line.strip.end_with?("/.gitignore")
end
check(tracked_private.empty?, "private proxy evidence is tracked")
puts "PASS facilitator-record-boundary: exact files, launch, no private evidence"

puts "Scope 0B implementation freeze: PASS"
