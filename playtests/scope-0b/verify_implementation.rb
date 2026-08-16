#!/usr/bin/env ruby
# frozen_string_literal: true

require "digest"
require "pathname"

ROOT = Pathname(__dir__).join("../..").expand_path
CONTRACT = ROOT.join("docs/scopes/SCOPE_0B_PLAYABLE.md")
BUILD_CHECKPOINT = ROOT.join("playtests/scope-0b/CHECKPOINT_1_IMPLEMENTATION_FREEZE.md")
RUN_CHECKPOINT = ROOT.join("playtests/scope-0b/CHECKPOINT_1F_RUN_PROTOCOL_V6.md")
SHEET = ROOT.join("playtests/scope-0b/FACILITATOR_SHEET.md")
FIXTURE = ROOT.join("data/scope-0b-v1.json")

def check(condition, message)
  raise "FAIL: #{message}" unless condition
end

def sha(path)
  Digest::SHA256.file(path).hexdigest
end

def fenced_section(markdown, heading)
  markdown[/^## #{Regexp.escape(heading)}\s*$.*?^```text[ \t]*\r?\n(.*?)\r?\n```[ \t]*$/m, 1]
end

def session_assignments
  {
    "S0B-V6-L01" => "ab",
    "S0B-V6-L02" => "ba",
    "S0B-V6-L03" => "ab",
    "S0B-V6-L04" => "ba",
    "S0B-V6-L05" => "ab"
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
canonical_prompt = fenced_section(sheet, "3. Exact participant prompt")
check(!canonical_prompt.nil?, "facilitator §3 participant prompt missing")
check(canonical_prompt.scan("<SESSION_ID>").length == 1, "participant prompt must contain one <SESSION_ID>")
check(canonical_prompt.valid_encoding?, "participant prompt is not valid UTF-8")

case ARGV.first
when "--render-prompt"
  check(ARGV.length == 2, "usage: verify_implementation.rb --render-prompt SESSION_ID")
  session_id = ARGV.fetch(1)
  check(session_assignments.key?(session_id), "unknown v6 session #{session_id}")
  STDOUT.write(canonical_prompt.sub("<SESSION_ID>", session_id))
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
      "active contract must reference, not duplicate, the canonical prompt")
prompt_hash = Digest::SHA256.hexdigest(canonical_prompt)
expected_prompt_hash = run_checkpoint[/task-message template SHA-256: `([0-9a-f]{64})`/, 1]
check(prompt_hash == expected_prompt_hash, "prompt-template hash drift")
%w[S0B-CONTRACT-v6 S0B-PROXY-v6 S0B-RUN-v6].each do |version|
  check(run_checkpoint.include?(version), "run checkpoint version #{version} missing")
end

assignments = session_assignments
assignments.each do |session_id, variant|
  row = run_checkpoint.lines.find { |line| line.start_with?("| `#{session_id}` |") }
  check(!row.nil? && row.include?("| `#{variant}` |"), "missing assignment #{session_id}/#{variant}")
  expected = expected_session_hash(run_checkpoint, session_id)
  actual = Digest::SHA256.hexdigest(canonical_prompt.sub("<SESSION_ID>", session_id))
  check(actual == expected, "participant message hash drift for #{session_id}")
end
puts "PASS prompt-and-assignments: #{prompt_hash}, 5 messages"

expected_sheet_hash = run_checkpoint[/facilitator-sheet SHA-256: `([0-9a-f]{64})`/, 1]
check(sha(SHEET) == expected_sheet_hash, "facilitator sheet hash drift")

check(sheet.include?("--accessibility always"), "launch command lacks forced accessibility")
check(sheet.include?("--resolution 1280x720"), "launch command lacks frozen resolution")
check(sheet.include?("--diagnostic-log"), "launch command lacks separate diagnostic path")
check(sheet.include?("tools.mcp__node_repl__js") && sheet.include?("org.godotengine.godot"),
      "v6 direct transport target missing")
check(sheet.include?("platform-owned session JSONL") &&
      sheet.include?("app diagnostic JSONL") &&
      sheet.include?("those two original artifacts"), "v6 two-original-artifact boundary missing")
check(sheet.include?("Record its path and SHA-256 after completion; do not copy") &&
      sheet.include?("Record its SHA-256."), "v6 original-artifact retention rule missing")
check(sheet.include?("no Godot process remains and both new log paths do not exist") &&
      sheet.include?("exactly one Godot process") &&
      sheet.include?("starts with one `READY` row") &&
      sheet.include?("exact title is readable") &&
      sheet.include?("If setup fails, close the whole round") &&
      sheet.include?("do not dispatch or replace a participant") &&
      sheet.include?("PROXY-RUN-BLOCKED"), "v6 participant preflight boundary missing")
check(sheet.include?("slot is never replaced") &&
      sheet.include?("InteractionCompletionPass = false") &&
      sheet.include?("Do not replace the participant"), "v6 no-replacement/scored-failure boundary missing")
check(sheet.include?("Do not send a post-measurement export, create a runner manifest or reconstruct a transcript"),
      "v6 removed-evidence prohibition missing")

game_text = ROOT.glob("game/*.{cs,tscn,godot}").map(&:read).join("\n")
check(!game_text.include?("verificationOnly"), "Game reads hidden oracle")
check(!game_text.match?(/\b(?:TODO|FIXME|HACK)\b/), "runtime contains placeholder marker")

tracked_private = `git -C #{ROOT} ls-files playtests/scope-0b/private`.lines.reject do |line|
  line.strip.end_with?("/.gitignore")
end
check(tracked_private.empty?, "private proxy evidence is tracked")
puts "PASS facilitator-evidence-boundary: two originals, preflight, no replacement, no private evidence"

checkpoint_reviewed = run_checkpoint.match?(
  /\A# Scope 0B v5 protocol result and v6 reset checkpoint\n\n> Status: \*\*REVIEWED — official v6 sessions authorized\*\*/
)
sheet_reviewed = sheet.match?(
  /\A# Scope 0B LLM UI-proxy facilitator sheet\n\n> Status: \*\*S0B-RUN-v6 REVIEWED — official sessions authorized\*\*/
)
draft_closed = run_checkpoint.match?(
  /\A# Scope 0B v5 protocol result and v6 reset checkpoint\n\n> Status: \*\*DRAFT — official v6 sessions closed\*\*/
) || sheet.match?(
  /\A# Scope 0B LLM UI-proxy facilitator sheet\n\n> Status: \*\*S0B-RUN-v6 DRAFT — official sessions closed\*\*/
)
check(!draft_closed, "v6 protocol remains DRAFT; official sessions are closed")

review_recorded = run_checkpoint.match?(/- initial v6 protocol commit: `[0-9a-f]{40}`/) &&
                  run_checkpoint.match?(/- bounded independent v6 reviewers?: `(?!PENDING`)[^`]+`/) &&
                  run_checkpoint.include?("final review: `P0=0, P1=0, P2=0`") &&
                  run_checkpoint.match?(/- reviewed v6 protocol commit: `[0-9a-f]{40}`/)
check(checkpoint_reviewed && sheet_reviewed && review_recorded,
      "v6 checkpoint is not independently reviewed/authorized")

initial_commit = run_checkpoint[/- initial v6 protocol commit: `([0-9a-f]{40})`/, 1]
reviewed_commit = run_checkpoint[/- reviewed v6 protocol commit: `([0-9a-f]{40})`/, 1]
check(system("git", "-C", ROOT.to_s, "cat-file", "-e", "#{initial_commit}^{commit}", out: File::NULL, err: File::NULL),
      "initial v6 protocol commit does not exist")
check(system("git", "-C", ROOT.to_s, "cat-file", "-e", "#{reviewed_commit}^{commit}", out: File::NULL, err: File::NULL),
      "reviewed v6 protocol commit does not exist")
check(initial_commit != reviewed_commit, "reviewed v6 commit must differ from initial commit")
check(system("git", "-C", ROOT.to_s, "merge-base", "--is-ancestor", initial_commit, reviewed_commit,
             out: File::NULL, err: File::NULL), "reviewed v6 commit does not descend from initial commit")
check(system("git", "-C", ROOT.to_s, "merge-base", "--is-ancestor", reviewed_commit, "HEAD",
             out: File::NULL, err: File::NULL), "reviewed v6 commit is not contained in current HEAD")

puts "Scope 0B implementation freeze: PASS"
