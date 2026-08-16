#!/usr/bin/env ruby
# frozen_string_literal: true

require "json"
require "pathname"

ROOT = Pathname.new(__dir__).join("../..").expand_path
FIXTURE_PATH = ROOT.join("data/scope-1-v1.json")

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
  dx = integer(b.fetch("x"), "point.x") - integer(a.fetch("x"), "point.x")
  dy = integer(b.fetch("y"), "point.y") - integer(a.fetch("y"), "point.y")
  dx * dx + dy * dy
end

begin
  fixture = JSON.parse(FIXTURE_PATH.binread, object_class: DuplicateCheckingHash)
rescue StandardError => e
  fail!("fixture JSON: #{e.message}")
end

exact_keys(fixture, %w[
  schemaVersion fixtureId units mapBounds source target maxSpan initialMinute buildMinutes
], "fixture root")
check(fixture.fetch("schemaVersion") == "1", "schemaVersion")
check(fixture.fetch("fixtureId") == "scope-1-v1", "fixtureId")

units = fixture.fetch("units")
exact_keys(units, %w[position time], "units")
check(units == { "position" => "GridUnit", "time" => "GameMinute" }, "units values")

bounds = fixture.fetch("mapBounds")
exact_keys(bounds, %w[minX maxX minY maxY], "mapBounds")
%w[minX maxX minY maxY].each { |key| integer(bounds.fetch(key), "mapBounds.#{key}") }
check(bounds == { "minX" => 0, "maxX" => 11, "minY" => 0, "maxY" => 7 }, "mapBounds values")

source = fixture.fetch("source")
target = fixture.fetch("target")
[[source, "source"], [target, "target"]].each do |point, label|
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

witness = [{ "x" => 5, "y" => 4 }, { "x" => 9, "y" => 4 }].freeze
limit_squared = max_span * max_span

check(squared(source, target) == 100 && squared(source, target) > limit_squared,
      "direct span oracle")
check(squared(source, witness[0]) == limit_squared, "source to witness[0] boundary")
check(squared(witness[0], target) == 36 && squared(witness[0], target) > limit_squared,
      "single-support failure oracle")
check(squared(witness[0], witness[1]) == limit_squared, "witness internal boundary")
check(squared(witness[1], target) == 4 && squared(witness[1], target) < limit_squared,
      "witness final span oracle")
check(squared(source, { "x" => 6, "y" => 4 }) == 25, "known invalid support oracle")
check(squared(source, { "x" => 3, "y" => 7 }) == 13, "two-axis valid oracle")
check(squared(source, { "x" => 4, "y" => 7 }) == 18, "two-axis invalid oracle")
check(initial_minute + build_minutes == 60, "completion minute oracle")

forbidden_fields = %w[verificationOnly presentation witness witnessSupportPositions]
check((fixture.keys & forbidden_fields).empty?, "product fixture contains checker/presentation fields")

puts "PASS fixture: exact nine-field root, nested shapes and integer values"
puts "PASS oracle: direct fail, boundary spans, witness success and completion minute"
puts "Scope 1 fixture contract: PASS"
