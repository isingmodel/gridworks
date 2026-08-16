#!/usr/bin/env ruby
# frozen_string_literal: true

require "digest"
require "json"
require "pathname"

ROOT = Pathname.new(__dir__).join("../..").expand_path
FIXTURE_PATH = ROOT.join("data/scope-1-v1.json")
SCOPE_PATH = ROOT.join("docs/scopes/SCOPE_1_INTERACTION.md")
CHECKPOINT_PATH = ROOT.join("playtests/scope-1/CHECKPOINT_1_CONTRACT_FREEZE.md")

class DuplicateCheckingHash < Hash
  def []=(key, value)
    raise JSON::ParserError, "duplicate property #{key.inspect}" if key?(key)

    super
  end
end

def fail!(message)
  warn "FAIL #{message}"
  exit 1
end

def check(condition, message)
  fail!(message) unless condition
end

def exact_keys(object, keys, label)
  check(object.is_a?(Hash), "#{label} must be an object")
  check(object.keys.sort == keys.sort, "#{label} fields differ: #{object.keys.sort.inspect}")
end

def integer(value, label)
  check(value.is_a?(Integer), "#{label} must be an integer")
  value
end

def squared(a, b)
  dx = Integer(b.fetch("x")) - Integer(a.fetch("x"))
  dy = Integer(b.fetch("y")) - Integer(a.fetch("y"))
  dx * dx + dy * dy
end

begin
  fixture_bytes = FIXTURE_PATH.binread
  fixture = JSON.parse(fixture_bytes, object_class: DuplicateCheckingHash)
rescue StandardError => e
  fail!("fixture JSON: #{e.message}")
end

exact_keys(fixture, %w[
  schemaVersion fixtureId units mapBounds source target maxSpan initialMinute buildMinutes verificationOnly
], "fixture root")
check(fixture.fetch("schemaVersion") == "gridworks.scope1.fixture.v1", "schemaVersion")
check(fixture.fetch("fixtureId") == "S1-FIXTURE-v1", "fixtureId")

units = fixture.fetch("units")
exact_keys(units, %w[position time], "units")
check(units == { "position" => "GridUnit", "time" => "GameMinute" }, "units values")

bounds = fixture.fetch("mapBounds")
exact_keys(bounds, %w[minX maxX minY maxY], "mapBounds")
%w[minX maxX minY maxY].each { |key| integer(bounds.fetch(key), "mapBounds.#{key}") }
check(bounds == { "minX" => 0, "maxX" => 11, "minY" => 0, "maxY" => 7 }, "mapBounds values")

source = fixture.fetch("source")
target = fixture.fetch("target")
[ [source, "source"], [target, "target"] ].each do |point, label|
  exact_keys(point, %w[x y], label)
  integer(point.fetch("x"), "#{label}.x")
  integer(point.fetch("y"), "#{label}.y")
end
check(source == { "x" => 1, "y" => 4 }, "source values")
check(target == { "x" => 11, "y" => 4 }, "target values")

max_span = integer(fixture.fetch("maxSpan"), "maxSpan")
initial_minute = integer(fixture.fetch("initialMinute"), "initialMinute")
build_minutes = integer(fixture.fetch("buildMinutes"), "buildMinutes")
check(max_span == 4 && initial_minute.zero? && build_minutes == 60, "time/span values")

verification = fixture.fetch("verificationOnly")
exact_keys(verification, ["witnessSupportPositions"], "verificationOnly")
witness = verification.fetch("witnessSupportPositions")
check(witness.is_a?(Array) && witness.length == 2, "witness must contain exactly two supports")
witness.each_with_index do |point, index|
  exact_keys(point, %w[x y], "witness[#{index}]")
  integer(point.fetch("x"), "witness[#{index}].x")
  integer(point.fetch("y"), "witness[#{index}].y")
end
check(witness == [{ "x" => 5, "y" => 4 }, { "x" => 9, "y" => 4 }], "witness values")

limit_squared = max_span * max_span
check(squared(source, target) == 100 && squared(source, target) > limit_squared, "direct span oracle")
check(squared(source, witness[0]) == limit_squared, "source to witness[0] boundary")
check(squared(witness[0], target) == 36 && squared(witness[0], target) > limit_squared,
      "single-support failure oracle")
check(squared(witness[0], witness[1]) == limit_squared, "witness internal boundary")
check(squared(witness[1], target) == 4 && squared(witness[1], target) < limit_squared,
      "witness final span oracle")
check(squared(source, { "x" => 6, "y" => 4 }) == 25, "known invalid support oracle")
check(initial_minute + build_minutes == 60, "completion minute oracle")
puts "PASS fixture: exact 10-field root and integer values"
puts "PASS oracle: direct fail, boundary spans, witness success, completion minute"

scope = SCOPE_PATH.read
readme = ROOT.join("README.md").read
docs_map = ROOT.join("docs/README.md").read
scope0_todo = ROOT.join("docs/scopes/SCOPE_0_TODO.md").read
visual = ROOT.join("docs/product/VISUAL_PRODUCTION_SPEC.md").read
check(readme.include?("현재 활성 구현 gate는 [**Scope 1 수동 선로 건설**]"), "README active Scope 1")
check(scope.include?("상태: **ACTIVE"), "Scope 1 ACTIVE header")
check(scope.include?("data/scope-1-v1.json"), "Scope 1 fixture link")
check(docs_map.include?("활성 수동 선로 구현 계약"), "docs map active role")
check(scope0_todo.include?("[x] 사용자가 Scope 1 구현과 Coverage·Integrated 통과"), "Scope 0 approval history")
check(visual.include?("Scope 1 Interaction 화면 — 현재 활성 gate"), "visual current Scope 1 section")
check(!readme.include?("현재 활성 구현 gate는 없다"), "stale no-active-gate wording")
check(!scope.include?("IMPLEMENTATION-READY CANDIDATE"), "stale candidate header")
puts "PASS authority: README, active scope, docs map and product status"

link_files = [
  ROOT.join("README.md"), ROOT.join("docs/README.md"), SCOPE_PATH,
  ROOT.join("docs/scopes/SCOPE_0_TODO.md"), ROOT.join("docs/scopes/RELEASE_1_0_BOUNDARY.md"),
  ROOT.join("docs/product/OBJECT_CATALOG.md"), ROOT.join("docs/product/VISUAL_PRODUCTION_SPEC.md"),
  ROOT.join("playtests/scope-0b/README.md"), ROOT.join("playtests/scope-1/README.md"), CHECKPOINT_PATH
]
missing = []
link_files.each do |file|
  file.read.scan(/\[[^\]]*\]\(([^)]+)\)/).flatten.each do |target|
    next if target.start_with?("http://", "https://", "#")

    clean = target.split("#", 2).first
    next if clean.empty?

    resolved = file.dirname.join(clean).cleanpath
    missing << "#{file.relative_path_from(ROOT)} -> #{target}" unless resolved.exist?
  end
end
check(missing.empty?, "missing local links: #{missing.join(', ')}")
puts "PASS links: #{link_files.length} authority files"

fixture_hash = Digest::SHA256.hexdigest(fixture_bytes)
checkpoint = CHECKPOINT_PATH.read
if ARGV == ["--content"]
  puts "PASS fixture-hash: #{fixture_hash} (content review mode)"
  puts "Scope 1 activation contract content: PASS; authorization remains review-gated"
  exit 0
end
check(ARGV.empty?, "usage: verify_contract.rb [--content]")
check(checkpoint.include?("CheckpointStatus = REVIEWED"), "activation checkpoint is not REVIEWED")
check(checkpoint.include?("FixtureAuthorityStatus = REVIEWED_MACHINE_AUTHORITY"),
      "fixture authority handoff is not reviewed")
check(checkpoint.include?("Fixture SHA-256: `#{fixture_hash}`"), "fixture hash drift")
check(checkpoint.match?(/initial activation content commit: `[0-9a-f]{40}`/), "initial commit missing")
check(checkpoint.match?(/reviewed activation content commit: `[0-9a-f]{40}`/), "reviewed commit missing")
puts "PASS fixture-hash: #{fixture_hash}"
puts "Scope 1 activation contract: PASS"
