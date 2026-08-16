#!/usr/bin/env ruby
# frozen_string_literal: true

require "digest"
require "json"
require "open3"
require "pathname"

ROOT = Pathname(__dir__).join("../..").expand_path
CONTRACT = ROOT.join("docs/scopes/SCOPE_0B_PLAYABLE.md")
BUILD_CHECKPOINT = ROOT.join("playtests/scope-0b/CHECKPOINT_1_IMPLEMENTATION_FREEZE.md")
RUN_CHECKPOINT = ROOT.join("playtests/scope-0b/CHECKPOINT_1F_RUN_PROTOCOL_V6.md")
SHEET = ROOT.join("playtests/scope-0b/FACILITATOR_SHEET.md")
CONTRACT_VERIFIER = ROOT.join("playtests/scope-0b/verify_contract.rb")
FIXTURE = ROOT.join("data/scope-0b-v1.json")
SKILL = Pathname("/Users/fred/.codex/plugins/cache/openai-bundled/computer-use/1.0.1000717/skills/computer-use/SKILL.md")
INITIAL_V6_COMMIT = "0e6e0ed35c5053104ae7e5889c8dd9a91b9869d3"
AUTHORITY_PATHS = [CONTRACT, SHEET, Pathname(__FILE__).expand_path, CONTRACT_VERIFIER].freeze

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

def child_task_names
  session_assignments.keys.to_h { |session_id| [session_id, session_id.downcase.tr("-", "_")] }
end

def expected_session_hash(checkpoint, session_id)
  row = checkpoint.lines.find { |line| line.start_with?("| `#{session_id}` |") }
  check(!row.nil?, "checkpoint assignment missing for #{session_id}")
  hash = row[/`([0-9a-f]{64})`/, 1]
  check(!hash.nil?, "checkpoint prompt hash missing for #{session_id}")
  hash
end

def jsonl(path)
  path.readlines(chomp: true).map { |line| JSON.parse(line) }
rescue JSON::ParserError => e
  check(false, "invalid JSONL #{path}: #{e.message}")
end

def git_output(*args)
  stdout, stderr, status = Open3.capture3("git", "-C", ROOT.to_s, *args)
  check(status.success?, "git #{args.join(' ')} failed: #{stderr.strip}")
  stdout
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
] + (ROOT.glob("src/Gridworks.Core/**/*.cs") + ROOT.glob("game/**/*.cs")).reject do |path|
  (path.each_filename.to_a & %w[bin obj .godot]).any?
end

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

session_assignments.each do |session_id, variant|
  checkpoint_row = run_checkpoint.lines.find { |line| line.start_with?("| `#{session_id}` |") }
  sheet_row = sheet.lines.find { |line| line.start_with?("| `#{session_id}` |") }
  check(!checkpoint_row.nil? && checkpoint_row.include?("| `#{variant}` |"),
        "checkpoint assignment #{session_id}/#{variant} missing")
  check(!sheet_row.nil? && sheet_row.include?("| `#{variant}` |") &&
        sheet_row.include?("| `#{child_task_names.fetch(session_id)}` |"),
        "facilitator assignment #{session_id}/#{variant} missing")
  expected = expected_session_hash(run_checkpoint, session_id)
  actual = Digest::SHA256.hexdigest(canonical_prompt.sub("<SESSION_ID>", session_id))
  check(actual == expected, "participant message hash drift for #{session_id}")
end
puts "PASS prompt-and-assignments: #{prompt_hash}, 5 messages"

expected_sheet_hash = run_checkpoint[/facilitator-sheet SHA-256: `([0-9a-f]{64})`/, 1]
check(sha(SHEET) == expected_sheet_hash, "facilitator sheet hash drift")
expected_skill_hash = run_checkpoint[/computer-use skill SHA-256: `([0-9a-f]{64})`/, 1]
check(SKILL.file?, "designated computer-use skill missing")
check(sha(SKILL) == expected_skill_hash, "computer-use skill hash drift")
check(sheet.include?(SKILL.to_s) && sheet.include?(expected_skill_hash), "facilitator skill identity missing")
puts "PASS execution-identities: facilitator and computer-use skill"

check(sheet.start_with?("# Scope 0B LLM UI-proxy facilitator sheet\n\n" \
                       "> Status: **S0B-RUN-v6 FROZEN EXECUTION COPY — use only when checkpoint 1F is AUTHORIZED**"),
      "facilitator must remain a status-neutral frozen execution copy")
check(sheet.include?("--accessibility always") &&
      sheet.include?("--resolution 1280x720") &&
      sheet.include?("--diagnostic-log") &&
      sheet.include?("tools.mcp__node_repl__js") &&
      sheet.include?("org.godotengine.godot"),
      "frozen native launch/transport identity missing")
check(sheet.include?("new cold `gpt-5.6-sol`, reasoning\n  `medium`, `fork_turns=none`"),
      "frozen model/reasoning/fork identity missing")
check(sheet.include?("`SESSION_ID=S0B-V6-PREFLIGHT` and `VARIANT=ab`") &&
      sheet.include?("dotnet build game/Gridworks.Game.csproj -c Debug --no-restore -t:Rebuild") &&
      sheet.include?("Failure here closes the round as `PROXY-RUN-BLOCKED`") &&
      sheet.include?("commit all\nfive rows immediately") &&
      sheet.include?("irrevocable before `L01` setup"),
      "single global-preflight boundary missing")
check(sheet.include?("SlotStatus = SETUP_FAILURE") &&
      sheet.include?("SlotStatus = PARTICIPANT_FAILURE") &&
      sheet.include?("SlotStatus = EVIDENCE_FAILURE") &&
      sheet.include?("Apply the common PID cleanup invariant") &&
      sheet.include?("Never erase an earlier row"),
      "fixed five-row failure mapping missing")
check(sheet.include?("coordinator platform JSONL") &&
      sheet.include?("participant platform JSONL") &&
      sheet.include?("app diagnostic JSONL"),
      "three-original evidence boundary missing")
check(sheet.include?("encrypt the spawn-message body") &&
      sheet.include?("cannot prove the frozen prompt's plaintext bytes") &&
      sheet.include?("not a post-run evidence predicate"),
      "prompt-plaintext claim ceiling missing")
check(sheet.include?("Send no follow-up or help") &&
      sheet.include?("interrupt that exact child once") &&
      sheet.include?("confirm its task is no longer\n  running. Only then terminate the captured app PID") &&
      sheet.include?("only then start the next row"),
      "serial no-help PID lifecycle missing")
check(sheet.include?("source restriction applies to participants") &&
      sheet.include?("must not relay their contents or judgments") &&
      sheet.include?("outer controller sends no message") &&
      sheet.include?("separate evidence auditor") &&
      sheet.include?("post-round auditor first stops any exact recorded child and app PID"),
      "participant/coordinator/post-round evidence boundary missing")
check(sheet.include?("For a dispatched row, the three originals are required") &&
      sheet.include?("SETUP_FAILURE` row intentionally has no participant"),
      "setup-failure evidence exception missing")
check(sheet.include?("every launch that captured a PID must stop only that PID") &&
      sheet.include?("even when setup validation fails") &&
      sheet.include?("partial setup log does not become a new required original"),
      "captured-PID cleanup invariant missing")
check(sheet.include?("Do not send a post-measurement export, create a runner manifest or reconstruct a transcript"),
      "removed evidence machinery prohibition missing")

coordinator_path_text = run_checkpoint[/- coordinator original:\n\s+`([^`]+)`/, 1]
child_path_text = run_checkpoint[/- child original:\n\s+`([^`]+)`/, 1]
coordinator_hash = run_checkpoint[/- coordinator SHA-256: `([0-9a-f]{64})`/, 1]
child_hash = run_checkpoint[/- child SHA-256: `([0-9a-f]{64})`/, 1]
coordinator_id = run_checkpoint[/- coordinator session ID: `([^`]+)`/, 1]
child_id = run_checkpoint[/- child session ID: `([^`]+)`/, 1]
coordinator_path = Pathname(coordinator_path_text.to_s)
child_path = Pathname(child_path_text.to_s)
check(coordinator_path.file? && child_path.file?, "platform-evidence rehearsal originals missing")
check(sha(coordinator_path) == coordinator_hash && sha(child_path) == child_hash,
      "platform-evidence rehearsal hash drift")

coordinator_rows = jsonl(coordinator_path)
child_rows = jsonl(child_path)
coordinator_meta = coordinator_rows.find { |row| row["type"] == "session_meta" }.fetch("payload")
child_meta = child_rows.find { |row| row["type"] == "session_meta" }.fetch("payload")
check(coordinator_meta.fetch("id") == coordinator_id, "rehearsal coordinator ID mismatch")
check(child_meta.fetch("id") == child_id && child_meta.fetch("parent_thread_id") == coordinator_id,
      "rehearsal child parent/ID mismatch")

function_calls = coordinator_rows.select do |row|
  row["type"] == "response_item" && row.dig("payload", "type") == "function_call"
end
spawn_calls = function_calls.select { |row| row.dig("payload", "name") == "spawn_agent" }
check(spawn_calls.length == 1, "rehearsal must have one child spawn")
spawn_args = JSON.parse(spawn_calls.first.dig("payload", "arguments"))
check(spawn_args.values_at("task_name", "fork_turns", "model", "reasoning_effort") ==
      ["v6_evidence_rehearsal_child", "none", "gpt-5.6-sol", "medium"],
      "rehearsal spawn identity mismatch")
check((function_calls.map { |row| row.dig("payload", "name") } & %w[followup_task send_message interrupt_agent]).empty?,
      "rehearsal contains follow-up/help call")

child_turn = child_rows.find { |row| row["type"] == "turn_context" }.fetch("payload")
check(child_turn.values_at("model", "effort") == ["gpt-5.6-sol", "medium"],
      "rehearsal child model/effort mismatch")
child_task = child_rows.find do |row|
  row["type"] == "response_item" && row.dig("payload", "type") == "agent_message"
end
child_cipher = child_task.fetch("payload").fetch("content").find do |item|
  item["type"] == "encrypted_content"
end.fetch("encrypted_content")
check(spawn_args.fetch("message") == child_cipher, "rehearsal dispatch/receipt ciphertext mismatch")
child_final = child_rows.reverse.find { |row| row.dig("payload", "type") == "task_complete" }
check(child_final.dig("payload", "last_agent_message") == "EVIDENCE_CHILD_OK",
      "rehearsal child final missing")
puts "PASS platform-evidence-rehearsal: linked ciphertext, model identity, no follow-up"

game_text = ROOT.glob("game/*.{cs,tscn,godot}").map(&:read).join("\n")
check(!game_text.include?("verificationOnly"), "Game reads hidden oracle")
check(!game_text.match?(/\b(?:TODO|FIXME|HACK)\b/), "runtime contains placeholder marker")

tracked_private = git_output("ls-files", "playtests/scope-0b/private").lines.reject do |line|
  line.strip.end_with?("/.gitignore")
end
check(tracked_private.empty?, "private proxy evidence is tracked")
puts "PASS v6-boundary: fixed rows, three originals, no replacement, no private evidence"

authorized = run_checkpoint.start_with?("# Scope 0B v5 protocol result and v6 reset checkpoint\n\n" \
                                        "> Status: **AUTHORIZED — official v6 sessions may start**")
unless authorized
  check(run_checkpoint.start_with?("# Scope 0B v5 protocol result and v6 reset checkpoint\n\n" \
                                   "> Status: **DRAFT — official v6 sessions closed**"),
        "unknown v6 checkpoint status")
  check(run_checkpoint.include?("- initial v6 protocol commit: `#{INITIAL_V6_COMMIT}`"),
        "initial v6 commit is not pinned while draft")
  check(false, "v6 checkpoint remains DRAFT; official sessions are closed")
end

initial_commit = run_checkpoint[/- initial v6 protocol commit: `([0-9a-f]{40})`/, 1]
content_commit = run_checkpoint[/- reviewed v6 content commit: `([0-9a-f]{40})`/, 1]
check(initial_commit == INITIAL_V6_COMMIT, "initial v6 commit is not the frozen initial commit")
check(!content_commit.nil? && content_commit != initial_commit, "reviewed v6 content commit missing")
check(run_checkpoint.match?(/- bounded independent v6 reviewers?: `(?!PENDING`)[^`]+`/),
      "bounded independent v6 reviewer missing")
check(run_checkpoint.include?("- final review: `P0=0, P1=0, P2=0`"), "clean final review missing")

git_output("cat-file", "-e", "#{initial_commit}^{commit}")
git_output("cat-file", "-e", "#{content_commit}^{commit}")
git_output("merge-base", "--is-ancestor", initial_commit, content_commit)

head = git_output("rev-parse", "HEAD").strip
parents = git_output("rev-list", "--parents", "-n", "1", head).split
check(parents.length == 2 && parents.fetch(1) == content_commit,
      "authorization commit must be HEAD with reviewed content as its sole parent")
changed_paths = git_output("diff-tree", "--no-commit-id", "--name-only", "-r", head).lines.map(&:strip).reject(&:empty?)
check(changed_paths == ["playtests/scope-0b/CHECKPOINT_1F_RUN_PROTOCOL_V6.md"],
      "authorization commit may change only checkpoint 1F")

reviewed_checkpoint = git_output("show", "#{content_commit}:playtests/scope-0b/CHECKPOINT_1F_RUN_PROTOCOL_V6.md")
normalized_checkpoint = run_checkpoint.dup
normalizations = [
  ["> Status: **AUTHORIZED — official v6 sessions may start**",
   "> Status: **DRAFT — official v6 sessions closed**"],
  [/^- bounded independent v6 reviewers?: `[^\r\n`]+`$/, "- bounded independent v6 reviewers: `PENDING`"],
  [/^- final review: `P0=0, P1=0, P2=0`$/, "- final review: `PENDING`"],
  [/^- reviewed v6 content commit: `[0-9a-f]{40}`$/, "- reviewed v6 content commit: `PENDING`"]
]
normalizations.each do |from, to|
  check(!normalized_checkpoint.sub!(from, to).nil?, "authorization field normalization failed")
end
check(normalized_checkpoint.b == reviewed_checkpoint.b,
      "authorization commit changed checkpoint content outside four authorization fields")

AUTHORITY_PATHS.each do |path|
  relative = path.relative_path_from(ROOT).to_s
  reviewed_bytes = git_output("show", "#{content_commit}:#{relative}")
  check(reviewed_bytes.b == path.binread, "current #{relative} differs from reviewed content commit")
end
dirty = git_output("status", "--porcelain", "--untracked-files=all")
check(dirty.empty?, "authorization requires a completely clean worktree")

puts "Scope 0B implementation freeze: PASS"
