#!/usr/bin/env ruby
# frozen_string_literal: true

require "json"
require "open3"
require "pathname"
require "tmpdir"

ROOT = Pathname.new(__dir__).join("../..").expand_path
GODOT = ROOT.join(".tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot").to_s
GAME = ROOT.join("game").to_s
BASELINE = "be1b3c275a1212c89fab47b87bbe3d5e1e591724"
EXPECTED_BUILD = "f8af82bf9e6ecc824f811b6a1b7309ee2d78a29eda2718e06075269602dc6ab2"
EXPECTED_FIXTURE = "8c1cd63efe1e6a6d3745db96c4071fd3a264ace07e715883581163f1c98e6a2b"
EXPECTED_INITIAL = "928a92efde792d1c40a6452424785f181a060bbce6a12cf02010a47c754ab34d"
EXPECTED_FINAL = "f088b365ec59ec127a2215cf6f65bd09550598303d2b2d33c2b5bb6a00989555"
EXPECTED_SCOPE0B_FINAL = "d8d6ac9edf2dd05e45be72dd1d0f2d01d849a8e051d2d59115b9ba9a7880792d"

def fail!(message)
  warn "FAIL #{message}"
  exit 1
end

def check(condition, message)
  fail!(message) unless condition
end

def run!(*command, chdir: ROOT.to_s)
  output, status = Open3.capture2e(*command, chdir: chdir)
  fail!("command #{command.join(' ')}\n#{output}") unless status.success?
  output
end

def read_rows(path)
  Pathname.new(path).read.lines.map { |line| JSON.parse(line) }
end

check(File.executable?(GODOT), "Godot 4.7.1 binary missing")
run!("ruby", "playtests/scope-1/verify_contract.rb")
run!("dotnet", "build", "src/Gridworks.Core/Gridworks.Core.csproj", "-c", "Release", "--no-restore")
run!("dotnet", "build", "game/Gridworks.Game.csproj", "-c", "Debug", "--no-restore", "-t:Rebuild")
scope1_checks = run!("dotnet", "run", "--project", "tools/Gridworks.Scope1Checks/Gridworks.Scope1Checks.csproj", "--no-restore")
check(scope1_checks.include?("10/10 suites, 274 assertions"), "Scope 1 checker result drift")
scope0b_checks = run!("dotnet", "run", "--project", "tools/Gridworks.Checks/Gridworks.Checks.csproj", "--no-restore")
check(scope0b_checks.include?("7 suites, 3098 assertions"), "Scope 0B regression result drift")
run!("ruby", "playtests/scope-0b/verify_contract.rb")
run!("ruby", "playtests/scope-0a-r2/verify_scope0a_r2.rb")

historical_paths = %w[
  src/Gridworks.Core/Definitions.cs
  src/Gridworks.Core/FixtureLoader.cs
  src/Gridworks.Core/FixtureValidator.cs
  src/Gridworks.Core/GridworksSession.cs
  src/Gridworks.Core/RawFixtureModels.cs
  src/Gridworks.Core/SnapshotJson.cs
  game/DiagnosticLog.cs
  game/GridMapView.cs
  game/LaunchOptions.cs
  game/Main.cs
  game/Main.tscn
  game/TimelineView.cs
  game/VisualModels.cs
]
historical_paths.each do |path|
  baseline = run!("git", "show", "#{BASELINE}:#{path}").b
  current = ROOT.join(path).binread
  check(current == baseline, "historical Scope 0B source changed: #{path}")
end

scope1_game = ROOT.glob("game/Scope1*.cs").map(&:read).join("\n")
check(!scope1_game.include?("new Scope1Point(5, 4)"), "checker witness leaked into Game source")
check(!scope1_game.include?("new Scope1Point(9, 4)"), "checker witness leaked into Game source")
check(ROOT.join("game/project.godot").read.include?('run/main_scene="res://Scope1Main.tscn"'),
      "Scope 1 is not the default scene")

Dir.mktmpdir("gridworks-s1-verify-") do |directory|
  import_log = File.join(directory, "import.log")
  scope1_engine = File.join(directory, "scope1-engine.log")
  scope1_app = File.join(directory, "scope1-app.jsonl")
  scope0b_engine = File.join(directory, "scope0b-engine.log")
  scope0b_app = File.join(directory, "scope0b-app.jsonl")

  run!(GODOT, "--headless", "--editor", "--path", GAME, "--quit", "--log-file", import_log)
  scope1_output = run!(
    GODOT, "--headless", "--path", GAME, "--log-file", scope1_engine, "--",
    "--session-id", "S1-VERIFY-SMOKE", "--diagnostic-log", scope1_app, "--smoke",
    "--smoke-support", "5,4", "--smoke-support", "9,4")
  check(scope1_output.include?("SCOPE1_SMOKE_PASS"), "Scope 1 smoke marker missing")

  rows = read_rows(scope1_app)
  check(rows.length == 6, "Scope 1 diagnostic row count")
  check(rows.map { |row| row.fetch("event") } ==
        %w[READY SUPPORT_ADDED SUPPORT_ADDED ORDERED COMPLETED FINAL],
        "Scope 1 diagnostic event order")
  check(rows.map { |row| row.fetch("sequence") } == (1..6).to_a, "Scope 1 diagnostic sequence")
  check(rows.all? { |row| row.fetch("accepted") == true }, "Scope 1 diagnostic rejection")
  check(rows.all? { |row| row.fetch("sessionId") == "S1-VERIFY-SMOKE" }, "Scope 1 session identity")
  ready = rows[0]
  check(ready.fetch("snapshotHash") == EXPECTED_INITIAL, "Scope 1 initial snapshot drift")
  check(ready.dig("payload", "buildHash") == EXPECTED_BUILD, "Scope 1 build hash drift")
  check(ready.dig("payload", "fixtureHash") == EXPECTED_FIXTURE, "Scope 1 fixture hash drift")
  check(rows[1].fetch("payload") == { "errorCode" => nil, "supportCount" => 1 },
        "first support payload")
  check(rows[2].fetch("payload") == { "errorCode" => nil, "supportCount" => 2 },
        "second support payload")
  check(rows[3].dig("payload", "targetEnergized") == false, "Building target must be off")
  check(rows[3].dig("payload", "completionMinute") == 60, "completion minute drift")
  check(rows[4].dig("payload", "targetEnergized") == true, "completion target must be on")
  check(rows[4].fetch("snapshotHash") == EXPECTED_FINAL, "completion snapshot drift")
  check(rows[5].fetch("snapshotHash") == EXPECTED_FINAL, "final snapshot drift")
  check(rows[5].fetch("payload") == { "targetEnergized" => true }, "final payload")

  scope0b_output = run!(
    GODOT, "--headless", "--path", GAME, "res://Main.tscn", "--log-file", scope0b_engine, "--",
    "--session-id", "S0B-S1-REGRESSION", "--variant", "ba", "--diagnostic-log", scope0b_app, "--smoke")
  check(scope0b_output.include?("SCOPE0B_SMOKE_PASS"), "Scope 0B smoke marker missing")
  scope0b_rows = read_rows(scope0b_app)
  check(scope0b_rows.length == 10, "Scope 0B diagnostic row count")
  check(scope0b_rows.last.fetch("snapshotHash") == EXPECTED_SCOPE0B_FINAL,
        "Scope 0B final snapshot drift")

  [import_log, scope1_engine, scope0b_engine].each do |path|
    text = Pathname.new(path).read
    check(!text.include?("SCRIPT ERROR"), "Godot script error in #{File.basename(path)}")
  end
end

puts "PASS source isolation: completed Scope 0B implementation unchanged"
puts "PASS managed checks: Scope 1 274 assertions; Scope 0B 3098 assertions"
puts "PASS Godot smoke: map input → Building off → Commissioned on; Scope 0B regression"
puts "Scope 1 implementation preflight: PASS"
