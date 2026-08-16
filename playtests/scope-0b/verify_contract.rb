#!/usr/bin/env ruby

require "digest"
require "json"
require "pathname"
require "set"
require "uri"

ROOT = Pathname(__dir__).join("../..").expand_path
FIXTURE_PATH = ROOT.join("data/scope-0b-v1.json")
CONTRACT_PATH = ROOT.join("docs/scopes/SCOPE_0B_PLAYABLE.md")
CHECKPOINT_PATH = ROOT.join("playtests/scope-0b/CHECKPOINT_0_CONTRACT_FREEZE.md")
RUN_CHECKPOINT_PATH = ROOT.join("playtests/scope-0b/CHECKPOINT_1D_RUN_PROTOCOL_V4.md")
SHEET_PATH = ROOT.join("playtests/scope-0b/FACILITATOR_SHEET.md")

def check(condition, message)
  raise message unless condition
end

def ascii_whitespace_fold(text)
  utf8 = text.dup.force_encoding(Encoding::UTF_8)
  check(utf8.valid_encoding?, "prompt is not valid UTF-8")
  utf8.gsub(/[ \t\r\n]+/, " ").sub(/\A +/, "").sub(/ +\z/, "")
end

def fenced_section(markdown, heading)
  markdown[/^## #{Regexp.escape(heading)}\s*$.*?^```text[ \t]*\r?\n(.*?)\r?\n```[ \t]*$/m, 1]
end

def exact_keys(object, keys, label)
  check(object.keys.sort == keys.sort, "#{label} keys: #{object.keys.sort.inspect}")
end

EPSILON = 1e-9

def cross(a, b, c)
  (b.fetch("x") - a.fetch("x")) * (c.fetch("y") - a.fetch("y")) -
    (b.fetch("y") - a.fetch("y")) * (c.fetch("x") - a.fetch("x"))
end

def point_on_segment?(point, left, right)
  cross(left, right, point).abs <= EPSILON &&
    point.fetch("x").between?([left.fetch("x"), right.fetch("x")].min - EPSILON, [left.fetch("x"), right.fetch("x")].max + EPSILON) &&
    point.fetch("y").between?([left.fetch("y"), right.fetch("y")].min - EPSILON, [left.fetch("y"), right.fetch("y")].max + EPSILON)
end

def segments_intersect?(a, b, c, d)
  values = [cross(a, b, c), cross(a, b, d), cross(c, d, a), cross(c, d, b)]
  return true if values[0] * values[1] < -EPSILON && values[2] * values[3] < -EPSILON

  (values[0].abs <= EPSILON && point_on_segment?(c, a, b)) ||
    (values[1].abs <= EPSILON && point_on_segment?(d, a, b)) ||
    (values[2].abs <= EPSILON && point_on_segment?(a, c, d)) ||
    (values[3].abs <= EPSILON && point_on_segment?(b, c, d))
end

def point_in_polygon?(point, polygon)
  return true if polygon.each_cons(2).any? { |left, right| point_on_segment?(point, left, right) } || point_on_segment?(point, polygon.last, polygon.first)

  inside = false
  previous = polygon.last
  polygon.each do |current|
    crosses = (current.fetch("y") > point.fetch("y")) != (previous.fetch("y") > point.fetch("y"))
    if crosses
      x_at_y = (previous.fetch("x") - current.fetch("x")) *
        (point.fetch("y") - current.fetch("y")) /
        (previous.fetch("y") - current.fetch("y")).to_f + current.fetch("x")
      inside = !inside if point.fetch("x") < x_at_y
    end
    previous = current
  end
  inside
end

def segment_sample(left, right, fraction)
  {
    "x" => left.fetch("x") + (right.fetch("x") - left.fetch("x")) * fraction,
    "y" => left.fetch("y") + (right.fetch("y") - left.fetch("y")) * fraction
  }
end

fixture = JSON.parse(FIXTURE_PATH.read)
exact_keys(
  fixture,
  %w[
    schemaVersion fixtureId displayName units calendar economy nodes edges projects loads requirements
    permittedSupplyPaths evaluationCases events hospitalInternalPower milestones presentation verificationOnly
  ],
  "root"
)
check(fixture.fetch("schemaVersion") == "gridworks.scope0b.fixture.v1", "schema version")
check(fixture.fetch("fixtureId") == "S0B-FIXTURE-v1", "fixture id")
check(
  fixture.fetch("units") == {
    "position" => "GridUnit",
    "power" => "kW",
    "energy" => "kWMinute",
    "time" => "GameMinute",
    "cash" => "CashUnit",
    "rate" => "CashUnitPerGWh"
  },
  "units"
)
check(fixture.fetch("calendar") == { "originLabel" => "DAY 0 08:00", "minutesPerDay" => 1_440 }, "calendar")
check(
  fixture.fetch("economy") == {
    "initialCash" => 20_000_000,
    "saleRate" => 180_000,
    "gasVariableRate" => 115_000,
    "townOutageRate" => 800_000,
    "hospitalOutageRate" => 20_000_000
  },
  "economy"
)

nodes = fixture.fetch("nodes")
edges = fixture.fetch("edges")
projects = fixture.fetch("projects")
loads = fixture.fetch("loads")
paths = fixture.fetch("permittedSupplyPaths")
cases = fixture.fetch("evaluationCases")

check(nodes.length == 7, "node count")
check(edges.length == 8, "edge count")
[nodes, edges, projects, paths, cases].each_with_index do |records, index|
  ids = records.map { |record| record.fetch("id") }
  check(ids.uniq.length == ids.length, "duplicate id collection #{index}")
end

node_keys = {
  "generator" => %w[id kind position maxOutputKw initialOnline],
  "bus" => %w[id kind position],
  "substation" => %w[id kind position initialCommissioned],
  "load" => %w[id kind position demandKw priority serviceSubstationId]
}
nodes.each do |node|
  exact_keys(node, node_keys.fetch(node.fetch("kind")), "node #{node.fetch('id')}")
  exact_keys(node.fetch("position"), %w[x y], "node position #{node.fetch('id')}")
end

expected_nodes = {
  "GEN_GAS" => ["generator", 1, 3],
  "SOURCE_BUS" => ["bus", 2, 3],
  "LOAD_HUB" => ["bus", 5, 3],
  "TOWN_SUB" => ["substation", 7, 1],
  "TOWN_LOAD" => ["load", 8, 1],
  "HOSPITAL_SUB" => ["substation", 7, 5],
  "HOSPITAL_LOAD" => ["load", 8, 5]
}
actual_nodes = nodes.to_h do |node|
  [node.fetch("id"), [node.fetch("kind"), node.fetch("position").fetch("x"), node.fetch("position").fetch("y")]]
end
check(actual_nodes == expected_nodes, "node handoff")
check(nodes.find { |node| node.fetch("id") == "GEN_GAS" }.slice("maxOutputKw", "initialOnline") == { "maxOutputKw" => 80_000, "initialOnline" => true }, "generator")
check(nodes.find { |node| node.fetch("id") == "TOWN_LOAD" }.slice("demandKw", "priority", "serviceSubstationId") == { "demandKw" => 24_000, "priority" => "P2", "serviceSubstationId" => "TOWN_SUB" }, "town load")
check(nodes.find { |node| node.fetch("id") == "HOSPITAL_LOAD" }.slice("demandKw", "priority", "serviceSubstationId") == { "demandKw" => 8_000, "priority" => "P0", "serviceSubstationId" => "HOSPITAL_SUB" }, "hospital load")

edges.each { |edge| exact_keys(edge, %w[id fromNodeId toNodeId ratingKw electricalContingencyId spatialRiskGroup initialConstructionState], "edge #{edge.fetch('id')}") }
expected_edges = {
  "GEN_EXPORT" => ["GEN_GAS", "SOURCE_BUS", 80_000, "GEN_EXPORT", "GEN_SITE", "commissioned"],
  "MAIN_TRUNK" => ["SOURCE_BUS", "LOAD_HUB", 40_000, "MAIN_TRUNK", "OLD_CORRIDOR", "commissioned"],
  "TOWN_FEEDER" => ["LOAD_HUB", "TOWN_SUB", 40_000, "TOWN", "TOWN_LOCAL", "not_ordered"],
  "TOWN_SERVICE" => ["TOWN_SUB", "TOWN_LOAD", 40_000, "TOWN", "TOWN_LOCAL", "commissioned"],
  "HOSPITAL_PRIMARY" => ["LOAD_HUB", "HOSPITAL_SUB", 40_000, "HOSPITAL_E1", "OLD_CORRIDOR", "commissioned"],
  "HOSPITAL_SERVICE" => ["HOSPITAL_SUB", "HOSPITAL_LOAD", 40_000, "HOSPITAL_SERVICE", "HOSPITAL_YARD", "commissioned"],
  "RIVER_PARALLEL" => ["SOURCE_BUS", "HOSPITAL_SUB", 40_000, "BACKUP_RIVER", "OLD_CORRIDOR", "not_ordered"],
  "NORTH_DETOUR" => ["SOURCE_BUS", "HOSPITAL_SUB", 40_000, "BACKUP_NORTH", "NORTH_CORRIDOR", "not_ordered"]
}
actual_edges = edges.to_h do |edge|
  [edge.fetch("id"), %w[fromNodeId toNodeId ratingKw electricalContingencyId spatialRiskGroup initialConstructionState].map { |key| edge.fetch(key) }]
end
check(actual_edges == expected_edges, "edge handoff")
node_ids = nodes.map { |node| node.fetch("id") }.to_set
edges.each do |edge|
  check(node_ids.include?(edge.fetch("fromNodeId")) && node_ids.include?(edge.fetch("toNodeId")), "edge endpoint #{edge.fetch('id')}")
end

expected_projects = {
  "PROJECT_TOWN_FEEDER" => ["TOWN_FEEDER", 6_000_000, 0, 1_440],
  "PROJECT_RIVER_PARALLEL" => ["RIVER_PARALLEL", 8_000_000, 1_440, 2_880],
  "PROJECT_NORTH_DETOUR" => ["NORTH_DETOUR", 12_000_000, 1_440, 2_880]
}
projects.each { |project| exact_keys(project, %w[id edgeId costCashUnit allowedOrderMinute buildMinutes], "project #{project.fetch('id')}") }
actual_projects = projects.to_h do |project|
  [project.fetch("id"), %w[edgeId costCashUnit allowedOrderMinute buildMinutes].map { |key| project.fetch(key) }]
end
check(actual_projects == expected_projects, "project handoff")

check(loads == [
  { "nodeId" => "HOSPITAL_LOAD", "activeMinute" => 0, "outageRateKey" => "hospitalOutageRate" },
  { "nodeId" => "TOWN_LOAD", "noticeMinute" => 0, "activeMinute" => 1_440, "outageRateKey" => "townOutageRate" }
], "load activation handoff")
check(fixture.fetch("requirements") == [{
  "id" => "HOSPITAL_SECOND_CIRCUIT",
  "deadlineMinute" => 4_320,
  "satisfiedByAnyCommissionedEdgeId" => %w[RIVER_PARALLEL NORTH_DETOUR]
}], "requirement handoff")

edge_by_id = edges.to_h { |edge| [edge.fetch("id"), edge] }
path_by_id = paths.to_h { |path| [path.fetch("id"), path] }
expected_path_edges = {
  "TOWN_PRIMARY_PATH" => %w[GEN_EXPORT MAIN_TRUNK TOWN_FEEDER TOWN_SERVICE],
  "HOSPITAL_PRIMARY_PATH" => %w[GEN_EXPORT MAIN_TRUNK HOSPITAL_PRIMARY HOSPITAL_SERVICE],
  "HOSPITAL_RIVER_BACKUP_PATH" => %w[GEN_EXPORT RIVER_PARALLEL HOSPITAL_SERVICE],
  "HOSPITAL_NORTH_BACKUP_PATH" => %w[GEN_EXPORT NORTH_DETOUR HOSPITAL_SERVICE]
}
check(path_by_id.transform_values { |path| path.fetch("edgeIds") } == expected_path_edges, "permitted path handoff")
expected_path_metadata = {
  "TOWN_PRIMARY_PATH" => ["TOWN_LOAD", "primary", nil],
  "HOSPITAL_PRIMARY_PATH" => ["HOSPITAL_LOAD", "primary", nil],
  "HOSPITAL_RIVER_BACKUP_PATH" => ["HOSPITAL_LOAD", "backup", "RIVER_PARALLEL"],
  "HOSPITAL_NORTH_BACKUP_PATH" => ["HOSPITAL_LOAD", "backup", "NORTH_DETOUR"]
}
actual_path_metadata = path_by_id.transform_values do |path|
  [path.fetch("loadNodeId"), path.fetch("role"), path["requiredCommissionedEdgeId"]]
end
check(actual_path_metadata == expected_path_metadata, "permitted path metadata handoff")
paths.each do |path|
  allowed = %w[id loadNodeId role edgeIds]
  allowed << "requiredCommissionedEdgeId" if path.fetch("role") == "backup"
  exact_keys(path, allowed, "path #{path.fetch('id')}")
  sequence = path.fetch("edgeIds").map { |edge_id| edge_by_id.fetch(edge_id) }
  check(sequence.first.fetch("fromNodeId") == "GEN_GAS", "path source #{path.fetch('id')}")
  sequence.each_cons(2) { |left, right| check(left.fetch("toNodeId") == right.fetch("fromNodeId"), "path continuity #{path.fetch('id')}") }
  check(sequence.last.fetch("toNodeId") == path.fetch("loadNodeId"), "path target #{path.fetch('id')}")
end

check(cases == [
  { "id" => "E1_REMOVAL", "selectorType" => "electricalContingencyId", "selectorValue" => "HOSPITAL_E1" },
  { "id" => "OLD_CORRIDOR_REMOVAL", "selectorType" => "spatialRiskGroup", "selectorValue" => "OLD_CORRIDOR" }
], "evaluation cases")
check(fixture.fetch("events") == [{
  "id" => "OLD_CORRIDOR_OUTAGE",
  "startMinute" => 13_440,
  "endMinute" => 13_680,
  "evaluationCaseId" => "OLD_CORRIDOR_REMOVAL"
}], "event handoff")
fixture.fetch("milestones").each { |milestone| exact_keys(milestone, %w[minute label], "milestone") }
check(fixture.fetch("milestones") == [
  { "minute" => 0, "label" => "DAY 0 08:00" },
  { "minute" => 1_440, "label" => "DAY 1 08:00" },
  { "minute" => 4_320, "label" => "DAY 3 08:00" },
  { "minute" => 13_440, "label" => "DAY 9 16:00" },
  { "minute" => 13_680, "label" => "DAY 9 20:00" }
], "public milestones")

presentation = fixture.fetch("presentation")
exact_keys(presentation, %w[mapBounds serviceAreas riskAreas edgePolylines layoutVariants], "presentation")
check(presentation.fetch("mapBounds") == { "width" => 10, "height" => 7 }, "map bounds")
service_areas = presentation.fetch("serviceAreas")
check(service_areas.length == 1 && service_areas.first.fetch("substationId") == "TOWN_SUB" && service_areas.first.fetch("shape") == "ellipse", "service area presentation")
exact_keys(service_areas.first, %w[substationId shape center radiusX radiusY], "service area")
exact_keys(service_areas.first.fetch("center"), %w[x y], "service area center")
service = service_areas.first
check(service.fetch("radiusX") > 0 && service.fetch("radiusY") > 0, "service ellipse radius")
%w[TOWN_SUB TOWN_LOAD].each do |node_id|
  position = nodes.find { |node| node.fetch("id") == node_id }.fetch("position")
  normalized = ((position.fetch("x") - service.fetch("center").fetch("x")) / service.fetch("radiusX").to_f)**2 +
    ((position.fetch("y") - service.fetch("center").fetch("y")) / service.fetch("radiusY").to_f)**2
  check(normalized <= 1.0 + EPSILON, "service ellipse excludes #{node_id}")
end
risk_areas = presentation.fetch("riskAreas")
check(risk_areas.length == 3, "risk band count")
check(risk_areas.map { |area| area.fetch("id") }.sort == %w[OLD_CORRIDOR_HOSPITAL_BAND OLD_CORRIDOR_MAIN_BAND OLD_CORRIDOR_RIVER_BAND], "risk band ids")
risk_areas.each do |area|
  exact_keys(area, %w[id spatialRiskGroup polygon], "risk area")
  check(area.fetch("spatialRiskGroup") == "OLD_CORRIDOR", "risk area group")
  polygon = area.fetch("polygon")
  check(polygon.length >= 3, "risk polygon")
  polygon.each do |point|
    exact_keys(point, %w[x y], "risk polygon point")
    check(point.fetch("x").between?(0, 10) && point.fetch("y").between?(0, 7), "risk polygon bounds")
  end
  polygon_segments = polygon.each_cons(2).to_a + [[polygon.last, polygon.first]]
  polygon_segments.each_with_index do |left_segment, left_index|
    polygon_segments.each_with_index do |right_segment, right_index|
      next if right_index <= left_index
      next if (left_index - right_index).abs == 1 || [left_index, right_index].sort == [0, polygon_segments.length - 1]

      check(!segments_intersect?(*left_segment, *right_segment), "self-intersecting risk polygon #{area.fetch('id')}")
    end
  end
end

polylines = presentation.fetch("edgePolylines")
check(polylines.length == edges.length, "polyline count")
check(polylines.map { |polyline| polyline.fetch("edgeId") }.sort == edges.map { |edge| edge.fetch("id") }.sort, "polyline ids")
node_by_id = nodes.to_h { |node| [node.fetch("id"), node] }
polylines.each do |polyline|
  exact_keys(polyline, %w[edgeId points], "polyline #{polyline.fetch('edgeId')}")
  points = polyline.fetch("points")
  check(points.length >= 2, "polyline point count #{polyline.fetch('edgeId')}")
  points.each do |point|
    exact_keys(point, %w[x y], "polyline point")
    check(point.fetch("x").between?(0, 10) && point.fetch("y").between?(0, 7), "polyline bounds #{polyline.fetch('edgeId')}")
  end
  edge = edge_by_id.fetch(polyline.fetch("edgeId"))
  from = node_by_id.fetch(edge.fetch("fromNodeId")).fetch("position")
  to = node_by_id.fetch(edge.fetch("toNodeId")).fetch("position")
  check(points.first == from && points.last == to, "polyline endpoint #{polyline.fetch('edgeId')}")
end
polyline_by_id = polylines.to_h { |polyline| [polyline.fetch("edgeId"), polyline.fetch("points")] }
check(polyline_by_id.fetch("RIVER_PARALLEL") != polyline_by_id.fetch("NORTH_DETOUR"), "corridor display separation")
risk_polygons = risk_areas.map { |area| area.fetch("polygon") }
polylines.each do |polyline|
  is_old = edge_by_id.fetch(polyline.fetch("edgeId")).fetch("spatialRiskGroup") == "OLD_CORRIDOR"
  samples = polyline.fetch("points").each_cons(2).flat_map do |left, right|
    (1..9).map { |step| segment_sample(left, right, step / 10.0) }
  end
  membership = samples.map { |point| risk_polygons.any? { |polygon| point_in_polygon?(point, polygon) } }
  check(is_old ? membership.all? : membership.none?, "risk-band mismatch #{polyline.fetch('edgeId')}: #{membership.inspect}")
end

polylines.reject { |polyline| edge_by_id.fetch(polyline.fetch("edgeId")).fetch("spatialRiskGroup") == "OLD_CORRIDOR" }.each do |polyline|
  polyline.fetch("points").each_cons(2) do |left, right|
    risk_polygons.each do |polygon|
      polygon_edges = polygon.each_cons(2).to_a + [[polygon.last, polygon.first]]
      polygon_edges.each do |risk_left, risk_right|
        next unless segments_intersect?(left, right, risk_left, risk_right)

        endpoint_touch = [left, right].any? { |point| point_on_segment?(point, risk_left, risk_right) }
        check(endpoint_touch, "safe edge crosses risk band #{polyline.fetch('edgeId')}")
      end
    end
  end
end

polylines.combination(2) do |left, right|
  left_edge = edge_by_id.fetch(left.fetch("edgeId"))
  right_edge = edge_by_id.fetch(right.fetch("edgeId"))
  shared_node_ids = [left_edge.fetch("fromNodeId"), left_edge.fetch("toNodeId")] & [right_edge.fetch("fromNodeId"), right_edge.fetch("toNodeId")]
  shared_points = shared_node_ids.map { |node_id| node_by_id.fetch(node_id).fetch("position") }
  left.fetch("points").each_cons(2) do |a, b|
    right.fetch("points").each_cons(2) do |c, d|
      next unless segments_intersect?(a, b, c, d)

      permitted_touch = shared_points.any? { |point| [a, b].include?(point) && [c, d].include?(point) }
      check(permitted_touch, "non-terminal polyline crossing #{left.fetch('edgeId')}/#{right.fetch('edgeId')}")
    end
  end
end
check(presentation.fetch("layoutVariants") == [
  { "id" => "ab", "corridorProjectOrder" => %w[PROJECT_RIVER_PARALLEL PROJECT_NORTH_DETOUR] },
  { "id" => "ba", "corridorProjectOrder" => %w[PROJECT_NORTH_DETOUR PROJECT_RIVER_PARALLEL] }
], "layout variants")
puts "PASS presentation: service ellipse, OLD-only risk bands, no non-terminal crossings, AB/BA order"

initial_commissioned = edges.select { |edge| edge.fetch("initialConstructionState") == "commissioned" }.map { |edge| edge.fetch("id") }.to_set
design_edge = { "NO_BUILD" => nil, "RIVER_PARALLEL" => "RIVER_PARALLEL", "NORTH_DETOUR" => "NORTH_DETOUR" }
backup_path = { "RIVER_PARALLEL" => "HOSPITAL_RIVER_BACKUP_PATH", "NORTH_DETOUR" => "HOSPITAL_NORTH_BACKUP_PATH" }

def removed_edges(edges, installed, removal_case)
  key = removal_case.fetch("selectorType")
  value = removal_case.fetch("selectorValue")
  edges.select { |edge| installed.include?(edge.fetch("id")) && edge.fetch(key) == value }.map { |edge| edge.fetch("id") }.to_set
end

def usable_path?(path, installed, removed)
  required = path["requiredCommissionedEdgeId"]
  (required.nil? || installed.include?(required)) && path.fetch("edgeIds").all? { |edge_id| installed.include?(edge_id) && !removed.include?(edge_id) }
end

def choose_paths(design, installed, removed, path_by_id, backup_path)
  chosen = {}
  town = path_by_id.fetch("TOWN_PRIMARY_PATH")
  chosen["TOWN_LOAD"] = town if usable_path?(town, installed, removed)
  hospital_primary = path_by_id.fetch("HOSPITAL_PRIMARY_PATH")
  if usable_path?(hospital_primary, installed, removed)
    chosen["HOSPITAL_LOAD"] = hospital_primary
  elsif backup_path.key?(design)
    backup = path_by_id.fetch(backup_path.fetch(design))
    chosen["HOSPITAL_LOAD"] = backup if usable_path?(backup, installed, removed)
  end
  chosen
end

case_by_id = cases.to_h { |entry| [entry.fetch("id"), entry] }
verification = fixture.fetch("verificationOnly")
exact_keys(verification, %w[topology evaluationOutcomes internalPower commonBoundaryStates routeBoundaryStates cash], "verificationOnly")
expected_outcomes = verification.fetch("evaluationOutcomes")
expected_outcomes.each { |outcome| exact_keys(outcome, %w[design caseId removedEdgeIds townUtilityDelivered hospitalUtilityDelivered townPathId hospitalPathId], "evaluation outcome") }
expected_pairs = ["NO_BUILD", "RIVER_PARALLEL", "NORTH_DETOUR"].product(["E1_REMOVAL", "OLD_CORRIDOR_REMOVAL"])
actual_pairs = expected_outcomes.map { |outcome| [outcome.fetch("design"), outcome.fetch("caseId")] }
check(actual_pairs.length == 6 && actual_pairs.uniq.sort == expected_pairs.sort, "evaluation outcome pair set")
expected_outcomes.each do |outcome|
  design = outcome.fetch("design")
  installed = initial_commissioned | Set["TOWN_FEEDER"]
  installed << design_edge.fetch(design) unless design_edge.fetch(design).nil?
  removed = removed_edges(edges, installed, case_by_id.fetch(outcome.fetch("caseId")))
  check(removed.subset?(installed), "removed edges must be commissioned")
  selected = choose_paths(design, installed, removed, path_by_id, backup_path)
  actual = [selected.key?("TOWN_LOAD"), selected.key?("HOSPITAL_LOAD")]
  expected = [outcome.fetch("townUtilityDelivered"), outcome.fetch("hospitalUtilityDelivered")]
  check(actual == expected, "evaluation outcome #{design}/#{outcome.fetch('caseId')}: #{actual.inspect}")
  check(outcome.fetch("removedEdgeIds") == removed.to_a.sort, "evaluation removed set #{design}/#{outcome.fetch('caseId')}")
  check(outcome.fetch("townPathId") == selected["TOWN_LOAD"]&.fetch("id"), "evaluation town path #{design}/#{outcome.fetch('caseId')}")
  check(outcome.fetch("hospitalPathId") == selected["HOSPITAL_LOAD"]&.fetch("id"), "evaluation hospital path #{design}/#{outcome.fetch('caseId')}")

  edge_load = Hash.new(0)
  selected.each do |load_id, path|
    demand = nodes.find { |node| node.fetch("id") == load_id }.fetch("demandKw")
    path.fetch("edgeIds").each { |edge_id| edge_load[edge_id] += demand }
  end
  edge_load.each { |edge_id, demand| check(demand <= edge_by_id.fetch(edge_id).fetch("ratingKw"), "capacity #{design}/#{edge_id}") }
  check(selected.keys.sum { |load_id| nodes.find { |node| node.fetch("id") == load_id }.fetch("demandKw") } <= 80_000, "generation capacity #{design}")
end

topology_oracle = verification.fetch("topology")
check(topology_oracle == {
  "nodeCount" => 7,
  "edgeCount" => 8,
  "normalDemandKw" => 32_000,
  "sharedTrunkRatingKw" => 40_000,
  "generatorRatingKw" => 80_000,
  "initialTownServiceEligible" => true,
  "initialTownUtilityPathAvailable" => false,
  "initialHospitalUtilityPathAvailable" => true
}, "topology oracle")
check(nodes.find { |node| node.fetch("id") == "TOWN_LOAD" }.fetch("serviceSubstationId") == "TOWN_SUB", "town service eligibility")
check(!usable_path?(path_by_id.fetch("TOWN_PRIMARY_PATH"), initial_commissioned, Set.new), "initial town path")
check(usable_path?(path_by_id.fetch("HOSPITAL_PRIMARY_PATH"), initial_commissioned, Set.new), "initial hospital path")
puts "PASS fixture: strict Scope 0B root, 7 nodes, 8 edges, authored service eligibility"
puts "PASS paths: 4 continuous permitted paths, 6 removal outcomes, no inferred reverse feed"

internal = fixture.fetch("hospitalInternalPower")
exact_keys(internal, %w[loadNodeId ratedPowerKw stages], "hospital internal power")
check(internal.fetch("loadNodeId") == "HOSPITAL_LOAD", "internal load")
internal.fetch("stages").each { |stage| exact_keys(stage, %w[id energyKwMinute], "internal stage") }
stage_ids = internal.fetch("stages").map { |stage| stage.fetch("id") }
check(stage_ids.length == 2 && stage_ids.uniq.sort == %w[DIESEL UPS], "internal stage id set")
check(internal.fetch("ratedPowerKw") == 8_000, "internal rated power")
stage_energy = internal.fetch("stages").to_h { |stage| [stage.fetch("id"), stage.fetch("energyKwMinute")] }
check(stage_energy == { "UPS" => 120_000, "DIESEL" => 2_280_000 }, "internal stages")
ups_minutes = stage_energy.fetch("UPS") / internal.fetch("ratedPowerKw")
diesel_minutes = stage_energy.fetch("DIESEL") / internal.fetch("ratedPowerKw")
check([ups_minutes, diesel_minutes, ups_minutes + diesel_minutes] == [15, 285, 300], "internal duration")
check(13_680 - 13_440 == 240, "event duration")
event_use = 8_000 * 240
remaining = stage_energy.values.sum - event_use
check([event_use, remaining, remaining / 8_000] == [1_920_000, 480_000, 60], "event internal use")
check(verification.fetch("internalPower") == {
  "upsDurationMinutes" => 15,
  "dieselDurationMinutes" => 285,
  "totalDurationMinutes" => 300,
  "riverEventUsedKwMinute" => 1_920_000,
  "riverEventRemainingKwMinute" => 480_000,
  "riverEventHospitalP0UnservedKwMinute" => 0,
  "northEventUsedKwMinute" => 0,
  "northEventRemainingKwMinute" => 2_400_000,
  "northEventHospitalP0UnservedKwMinute" => 0
}, "internal oracle")
puts "PASS energy: UPS=15m, diesel=285m, event=240m, remaining=60m"

def cash_for(power_kw, elapsed_minutes, rate)
  numerator = power_kw * elapsed_minutes * rate
  denominator = 60_000_000
  check((numerator % denominator).zero?, "non-exact cash #{power_kw}*#{elapsed_minutes}*#{rate}")
  numerator / denominator
end

economy = fixture.fetch("economy")
prechoice_revenue = cash_for(8_000, 1_440, economy.fetch("saleRate"))
prechoice_gas = cash_for(8_000, 1_440, economy.fetch("gasVariableRate"))
prechoice_cash = economy.fetch("initialCash") - 6_000_000 + prechoice_revenue - prechoice_gas
normal_revenue = cash_for(32_000, 12_000, economy.fetch("saleRate"))
normal_gas = cash_for(32_000, 12_000, economy.fetch("gasVariableRate"))
normal_margin = normal_revenue - normal_gas
river_town_unserved = 24_000 * 240
river_hospital_unserved = 8_000 * 240
river_lost_sales = cash_for(32_000, 240, economy.fetch("saleRate"))
river_compensation = cash_for(24_000, 240, economy.fetch("townOutageRate")) + cash_for(8_000, 240, economy.fetch("hospitalOutageRate"))
river_delta = -river_compensation
river_end = prechoice_cash - 8_000_000 + normal_margin + river_delta
north_delivered = 8_000 * 240
north_revenue = cash_for(8_000, 240, economy.fetch("saleRate"))
north_gas = cash_for(8_000, 240, economy.fetch("gasVariableRate"))
north_compensation = cash_for(24_000, 240, economy.fetch("townOutageRate"))
north_lost_sales = cash_for(24_000, 240, economy.fetch("saleRate"))
north_delta = north_revenue - north_gas - north_compensation
north_end = prechoice_cash - 12_000_000 + normal_margin + north_delta
normal_48h_revenue = cash_for(32_000, 2_880, economy.fetch("saleRate"))
normal_48h_gas = cash_for(32_000, 2_880, economy.fetch("gasVariableRate"))
normal_152h_revenue = cash_for(32_000, 9_120, economy.fetch("saleRate"))
normal_152h_gas = cash_for(32_000, 9_120, economy.fetch("gasVariableRate"))
river_15m_compensation = cash_for(24_000, 15, economy.fetch("townOutageRate")) + cash_for(8_000, 15, economy.fetch("hospitalOutageRate"))
river_225m_compensation = cash_for(24_000, 225, economy.fetch("townOutageRate")) + cash_for(8_000, 225, economy.fetch("hospitalOutageRate"))
north_15m_delta = cash_for(8_000, 15, economy.fetch("saleRate")) - cash_for(8_000, 15, economy.fetch("gasVariableRate")) - cash_for(24_000, 15, economy.fetch("townOutageRate"))
north_225m_delta = cash_for(8_000, 225, economy.fetch("saleRate")) - cash_for(8_000, 225, economy.fetch("gasVariableRate")) - cash_for(24_000, 225, economy.fetch("townOutageRate"))
check([normal_48h_revenue, normal_48h_gas, normal_152h_revenue, normal_152h_gas] == [276_480, 176_640, 875_520, 559_360], "normal interval cash")
check([river_15m_compensation, river_225m_compensation] == [44_800, 672_000], "river split cash")
check([north_15m_delta, north_225m_delta] == [-4_670, -70_050], "north split cash")
cash_oracle = verification.fetch("cash")
check(cash_oracle.fetch("preChoiceCash") == prechoice_cash && prechoice_cash == 14_012_480, "prechoice cash")
check(cash_oracle.fetch("normalPostChoiceNetCash") == normal_margin && normal_margin == 416_000, "normal margin")
check(cash_oracle.fetch("riverEvent") == {
  "utilityDeliveredKwMinute" => 0,
  "townUtilityUnservedKwMinute" => river_town_unserved,
  "hospitalUtilityUnservedKwMinute" => river_hospital_unserved,
  "revenueCashUnit" => 0,
  "lostSalesCashUnit" => river_lost_sales,
  "compensationCashUnit" => river_compensation,
  "gasCostCashUnit" => 0,
  "eventCashDelta" => river_delta,
  "endingCash" => river_end
}, "river cash oracle")
check(cash_oracle.fetch("northEvent") == {
  "utilityDeliveredKwMinute" => north_delivered,
  "townUtilityUnservedKwMinute" => 24_000 * 240,
  "hospitalUtilityUnservedKwMinute" => 0,
  "revenueCashUnit" => north_revenue,
  "lostSalesCashUnit" => north_lost_sales,
  "compensationCashUnit" => north_compensation,
  "gasCostCashUnit" => north_gas,
  "eventCashDelta" => north_delta,
  "endingCash" => north_end
}, "north cash oracle")
puts "PASS cash: prechoice=14012480, river_end=5711680, north_end=2353760"

SETTLEMENT_KEYS = %w[
  revenueCashUnit gasCostCashUnit compensationCashUnit lostSalesCashUnit
  utilityDeliveredKwMinuteByLoad utilityUnservedKwMinuteByLoad gasInjectionKwMinute
  hospitalInternalUsedKwMinute hospitalP0UnservedKwMinute
].freeze

def empty_settlement
  {
    "revenueCashUnit" => 0, "gasCostCashUnit" => 0, "compensationCashUnit" => 0,
    "lostSalesCashUnit" => 0,
    "utilityDeliveredKwMinuteByLoad" => { "HOSPITAL_LOAD" => 0, "TOWN_LOAD" => 0 },
    "utilityUnservedKwMinuteByLoad" => { "HOSPITAL_LOAD" => 0, "TOWN_LOAD" => 0 },
    "gasInjectionKwMinute" => 0, "hospitalInternalUsedKwMinute" => 0,
    "hospitalP0UnservedKwMinute" => 0
  }
end

def settlement_for(minutes, economy, town_active:, town_delivered:, hospital_delivered:, internal_used: 0)
  town_delivered_energy = town_active && town_delivered ? 24_000 * minutes : 0
  town_unserved_energy = town_active && !town_delivered ? 24_000 * minutes : 0
  hospital_delivered_energy = hospital_delivered ? 8_000 * minutes : 0
  hospital_unserved_energy = hospital_delivered ? 0 : 8_000 * minutes
  delivered_power = (town_delivered_energy + hospital_delivered_energy) / minutes
  lost_power = (town_unserved_energy + hospital_unserved_energy) / minutes
  compensation = cash_for(town_unserved_energy / minutes, minutes, economy.fetch("townOutageRate")) +
    cash_for(hospital_unserved_energy / minutes, minutes, economy.fetch("hospitalOutageRate"))
  {
    "revenueCashUnit" => cash_for(delivered_power, minutes, economy.fetch("saleRate")),
    "gasCostCashUnit" => cash_for(delivered_power, minutes, economy.fetch("gasVariableRate")),
    "compensationCashUnit" => compensation,
    "lostSalesCashUnit" => cash_for(lost_power, minutes, economy.fetch("saleRate")),
    "utilityDeliveredKwMinuteByLoad" => { "HOSPITAL_LOAD" => hospital_delivered_energy, "TOWN_LOAD" => town_delivered_energy },
    "utilityUnservedKwMinuteByLoad" => { "HOSPITAL_LOAD" => hospital_unserved_energy, "TOWN_LOAD" => town_unserved_energy },
    "gasInjectionKwMinute" => town_delivered_energy + hospital_delivered_energy,
    "hospitalInternalUsedKwMinute" => internal_used,
    "hospitalP0UnservedKwMinute" => [hospital_unserved_energy - internal_used, 0].max
  }
end

def add_settlement(left, right)
  result = {}
  %w[revenueCashUnit gasCostCashUnit compensationCashUnit lostSalesCashUnit gasInjectionKwMinute hospitalInternalUsedKwMinute hospitalP0UnservedKwMinute].each do |key|
    result[key] = left.fetch(key) + right.fetch(key)
  end
  %w[utilityDeliveredKwMinuteByLoad utilityUnservedKwMinuteByLoad].each do |key|
    result[key] = %w[HOSPITAL_LOAD TOWN_LOAD].to_h { |load_id| [load_id, left.fetch(key).fetch(load_id) + right.fetch(key).fetch(load_id)] }
  end
  result
end

def expected_state(id:, minute:, cash:, town_state:, corridor_state:, selected:, commissioned:, removed:, active:, paths:, internal_stage:, internal_remaining:, interval:, cumulative:, complete:)
  {
    "id" => id, "minute" => minute, "cash" => cash,
    "townProjectState" => town_state, "corridorProjectState" => corridor_state,
    "selectedCorridor" => selected, "commissionedEdgeIds" => commissioned,
    "eventRemovedEdgeIds" => removed, "activeLoadIds" => active,
    "utilityPathByLoad" => paths, "hospitalInternalStage" => internal_stage,
    "hospitalInternalRemainingKwMinute" => internal_remaining,
    "interval" => interval, "cumulative" => cumulative, "isComplete" => complete
  }
end

def validate_state_shape(state)
  exact_keys(state, %w[id minute cash townProjectState corridorProjectState selectedCorridor commissionedEdgeIds eventRemovedEdgeIds activeLoadIds utilityPathByLoad hospitalInternalStage hospitalInternalRemainingKwMinute interval cumulative isComplete], "boundary #{state.fetch('id')}")
  %w[interval cumulative].each do |ledger_key|
    ledger = state.fetch(ledger_key)
    exact_keys(ledger, SETTLEMENT_KEYS, "#{ledger_key} #{state.fetch('id')}")
    %w[utilityDeliveredKwMinuteByLoad utilityUnservedKwMinuteByLoad].each do |energy_key|
      exact_keys(ledger.fetch(energy_key), %w[HOSPITAL_LOAD TOWN_LOAD], "#{ledger_key} #{energy_key}")
    end
  end
  check(state.fetch("commissionedEdgeIds") == state.fetch("commissionedEdgeIds").sort, "commissioned order #{state.fetch('id')}")
  check(state.fetch("eventRemovedEdgeIds") == state.fetch("eventRemovedEdgeIds").sort, "removed order #{state.fetch('id')}")
  check(state.fetch("activeLoadIds") == state.fetch("activeLoadIds").sort, "active-load order #{state.fetch('id')}")
  check(state.fetch("eventRemovedEdgeIds").to_set.subset?(state.fetch("commissionedEdgeIds").to_set), "removed/commissioned subset #{state.fetch('id')}")
  check(state.fetch("utilityPathByLoad").keys.sort == state.fetch("activeLoadIds"), "path keys #{state.fetch('id')}")
end

zero = empty_settlement
pre_interval = settlement_for(1_440, economy, town_active: false, town_delivered: false, hospital_delivered: true)
normal_48h = settlement_for(2_880, economy, town_active: true, town_delivered: true, hospital_delivered: true)
normal_152h = settlement_for(9_120, economy, town_active: true, town_delivered: true, hospital_delivered: true)
river_15m = settlement_for(15, economy, town_active: true, town_delivered: false, hospital_delivered: false, internal_used: 120_000)
river_240m = settlement_for(240, economy, town_active: true, town_delivered: false, hospital_delivered: false, internal_used: 1_920_000)
north_240m = settlement_for(240, economy, town_active: true, town_delivered: false, hospital_delivered: true)
cumulative_4320 = add_settlement(pre_interval, normal_48h)
cumulative_13440 = add_settlement(cumulative_4320, normal_152h)
cumulative_river_15 = add_settlement(cumulative_13440, river_15m)
cumulative_river_final = add_settlement(cumulative_13440, river_240m)
cumulative_north_final = add_settlement(cumulative_13440, north_240m)

base_edges = %w[GEN_EXPORT HOSPITAL_PRIMARY HOSPITAL_SERVICE MAIN_TRUNK TOWN_SERVICE]
town_edges = %w[GEN_EXPORT HOSPITAL_PRIMARY HOSPITAL_SERVICE MAIN_TRUNK TOWN_FEEDER TOWN_SERVICE]
both_paths = { "HOSPITAL_LOAD" => "HOSPITAL_PRIMARY_PATH", "TOWN_LOAD" => "TOWN_PRIMARY_PATH" }
common_boundary_expected = [
  expected_state(id: "INITIAL", minute: 0, cash: 20_000_000, town_state: "not_ordered", corridor_state: "not_ordered", selected: nil, commissioned: base_edges, removed: [], active: %w[HOSPITAL_LOAD], paths: { "HOSPITAL_LOAD" => "HOSPITAL_PRIMARY_PATH" }, internal_stage: "none", internal_remaining: 2_400_000, interval: zero, cumulative: zero, complete: false),
  expected_state(id: "TOWN_ORDERED", minute: 0, cash: 14_000_000, town_state: "building", corridor_state: "not_ordered", selected: nil, commissioned: base_edges, removed: [], active: %w[HOSPITAL_LOAD], paths: { "HOSPITAL_LOAD" => "HOSPITAL_PRIMARY_PATH" }, internal_stage: "none", internal_remaining: 2_400_000, interval: zero, cumulative: zero, complete: false),
  expected_state(id: "PRE_CHOICE", minute: 1_440, cash: 14_012_480, town_state: "commissioned", corridor_state: "not_ordered", selected: nil, commissioned: town_edges, removed: [], active: %w[HOSPITAL_LOAD TOWN_LOAD], paths: both_paths, internal_stage: "none", internal_remaining: 2_400_000, interval: pre_interval, cumulative: pre_interval, complete: false)
]

river_edges = %w[GEN_EXPORT HOSPITAL_PRIMARY HOSPITAL_SERVICE MAIN_TRUNK RIVER_PARALLEL TOWN_FEEDER TOWN_SERVICE]
north_edges = %w[GEN_EXPORT HOSPITAL_PRIMARY HOSPITAL_SERVICE MAIN_TRUNK NORTH_DETOUR TOWN_FEEDER TOWN_SERVICE]
route_boundary_expected = {
  "RIVER_PARALLEL" => [
    expected_state(id: "CORRIDOR_ORDERED", minute: 1_440, cash: 6_012_480, town_state: "commissioned", corridor_state: "building", selected: "RIVER_PARALLEL", commissioned: town_edges, removed: [], active: %w[HOSPITAL_LOAD TOWN_LOAD], paths: both_paths, internal_stage: "none", internal_remaining: 2_400_000, interval: zero, cumulative: pre_interval, complete: false),
    expected_state(id: "CORRIDOR_COMMISSIONED", minute: 4_320, cash: 6_112_320, town_state: "commissioned", corridor_state: "commissioned", selected: "RIVER_PARALLEL", commissioned: river_edges, removed: [], active: %w[HOSPITAL_LOAD TOWN_LOAD], paths: both_paths, internal_stage: "none", internal_remaining: 2_400_000, interval: normal_48h, cumulative: cumulative_4320, complete: false),
    expected_state(id: "EVENT_STARTED", minute: 13_440, cash: 6_428_480, town_state: "commissioned", corridor_state: "commissioned", selected: "RIVER_PARALLEL", commissioned: river_edges, removed: %w[HOSPITAL_PRIMARY MAIN_TRUNK RIVER_PARALLEL], active: %w[HOSPITAL_LOAD TOWN_LOAD], paths: { "HOSPITAL_LOAD" => nil, "TOWN_LOAD" => nil }, internal_stage: "ups", internal_remaining: 2_400_000, interval: normal_152h, cumulative: cumulative_13440, complete: false),
    expected_state(id: "UPS_DEPLETED", minute: 13_455, cash: 6_383_680, town_state: "commissioned", corridor_state: "commissioned", selected: "RIVER_PARALLEL", commissioned: river_edges, removed: %w[HOSPITAL_PRIMARY MAIN_TRUNK RIVER_PARALLEL], active: %w[HOSPITAL_LOAD TOWN_LOAD], paths: { "HOSPITAL_LOAD" => nil, "TOWN_LOAD" => nil }, internal_stage: "diesel", internal_remaining: 2_280_000, interval: river_15m, cumulative: cumulative_river_15, complete: false),
    expected_state(id: "FINAL", minute: 13_680, cash: 5_711_680, town_state: "commissioned", corridor_state: "commissioned", selected: "RIVER_PARALLEL", commissioned: river_edges, removed: [], active: %w[HOSPITAL_LOAD TOWN_LOAD], paths: both_paths, internal_stage: "none", internal_remaining: 480_000, interval: river_240m, cumulative: cumulative_river_final, complete: true)
  ],
  "NORTH_DETOUR" => [
    expected_state(id: "CORRIDOR_ORDERED", minute: 1_440, cash: 2_012_480, town_state: "commissioned", corridor_state: "building", selected: "NORTH_DETOUR", commissioned: town_edges, removed: [], active: %w[HOSPITAL_LOAD TOWN_LOAD], paths: both_paths, internal_stage: "none", internal_remaining: 2_400_000, interval: zero, cumulative: pre_interval, complete: false),
    expected_state(id: "CORRIDOR_COMMISSIONED", minute: 4_320, cash: 2_112_320, town_state: "commissioned", corridor_state: "commissioned", selected: "NORTH_DETOUR", commissioned: north_edges, removed: [], active: %w[HOSPITAL_LOAD TOWN_LOAD], paths: both_paths, internal_stage: "none", internal_remaining: 2_400_000, interval: normal_48h, cumulative: cumulative_4320, complete: false),
    expected_state(id: "EVENT_STARTED", minute: 13_440, cash: 2_428_480, town_state: "commissioned", corridor_state: "commissioned", selected: "NORTH_DETOUR", commissioned: north_edges, removed: %w[HOSPITAL_PRIMARY MAIN_TRUNK], active: %w[HOSPITAL_LOAD TOWN_LOAD], paths: { "HOSPITAL_LOAD" => "HOSPITAL_NORTH_BACKUP_PATH", "TOWN_LOAD" => nil }, internal_stage: "none", internal_remaining: 2_400_000, interval: normal_152h, cumulative: cumulative_13440, complete: false),
    expected_state(id: "FINAL", minute: 13_680, cash: 2_353_760, town_state: "commissioned", corridor_state: "commissioned", selected: "NORTH_DETOUR", commissioned: north_edges, removed: [], active: %w[HOSPITAL_LOAD TOWN_LOAD], paths: both_paths, internal_stage: "none", internal_remaining: 2_400_000, interval: north_240m, cumulative: cumulative_north_final, complete: true)
  ]
}

common_boundary_actual = verification.fetch("commonBoundaryStates")
common_boundary_actual.each { |state| validate_state_shape(state) }
check(common_boundary_actual == common_boundary_expected, "common boundary oracle")
route_boundary_groups = verification.fetch("routeBoundaryStates")
route_designs = route_boundary_groups.map { |entry| entry.fetch("design") }
check(route_designs.length == 2 && route_designs.uniq.sort == %w[NORTH_DETOUR RIVER_PARALLEL], "route boundary design set")
route_boundary_actual = route_boundary_groups.to_h do |entry|
  exact_keys(entry, %w[design states], "route boundary group")
  entry.fetch("states").each { |state| validate_state_shape(state) }
  [entry.fetch("design"), entry.fetch("states")]
end
check(route_boundary_actual == route_boundary_expected, "route boundary oracle")
puts "PASS boundaries: full snapshots, actual River-only hidden trace, interval/cumulative conservation"

def slug(text)
  text.downcase.gsub(/[`*_~]/, "").gsub(/[^\p{Alnum}\p{M}\s_-]/u, "").strip.gsub(/\s+/, "-")
end

missing_links = []
markdown_paths = ROOT.glob("**/*.md").reject do |path|
  relative = path.relative_path_from(ROOT).to_s
  relative.start_with?(".tools/") || relative.split("/").include?("private")
end
markdown_paths.each do |markdown|
  content = markdown.read
  content.scan(/!?\[[^\]]*\]\(([^)]+)\)/).flatten.each do |raw_target|
    next if raw_target.match?(/\A(?:https?:|mailto:)/)

    target = raw_target.strip.sub(/\A</, "").sub(/>\z/, "")
    path_part, fragment = target.split("#", 2)
    resolved = path_part.nil? || path_part.empty? ? markdown : markdown.dirname.join(URI.decode_www_form_component(path_part)).cleanpath
    unless resolved.exist?
      missing_links << "#{markdown.relative_path_from(ROOT)} -> #{target}"
      next
    end
    next if fragment.nil? || fragment.empty? || resolved.directory?

    headings = resolved.readlines.map { |line| line[/\A[#]{1,6}\s+(.+?)\s*#*\s*\z/, 1] }.compact.map { |heading| slug(heading) }
    decoded_fragment = URI.decode_www_form_component(fragment)
    missing_links << "#{markdown.relative_path_from(ROOT)} -> ##{fragment}" unless headings.include?(decoded_fragment)
  end
end
check(missing_links.empty?, "missing links: #{missing_links.join(', ')}")
check(markdown_paths.none? { |path| path.read.include?("SCOPE_0B_CANDIDATE.md") }, "stale candidate reference")
contract = CONTRACT_PATH.read
%w[S0B-CONTRACT-v4 S0B-FIXTURE-v1 S0B-BUILD-v1 S0B-PROXY-v4 S0B-RUN-v4 S0B-GATE-v1].each { |version| check(contract.include?(version), "contract version #{version}") }
check(contract.include?("각각 `4/5` 이상") && contract.include?("`3/5` 이상"), "gate thresholds")
check(contract.include?("ActiveKnob = 0"), "parameter policy")
check(contract.include?("InteractionCompletionPass = false"), "technically valid incomplete session contract")
check(contract.include?("PROXY-RUN-BLOCKED") && contract.include?("catch-all"), "blocked/catch-all separation")
check(contract.include?("`final:none`") && contract.include?("`runner_error:runner`"), "runner manifest truth table")
fixture_hash = Digest::SHA256.file(FIXTURE_PATH).hexdigest
checkpoint_hash = CHECKPOINT_PATH.read[/frozen fixture SHA-256: `([0-9a-f]{64})`/, 1]
check(checkpoint_hash == fixture_hash, "checkpoint fixture hash #{checkpoint_hash.inspect} != #{fixture_hash}")
sheet = SHEET_PATH.read
prompt_template = fenced_section(sheet, "2. Exact participant prompt")
check(!prompt_template.nil?, "facilitator §2 participant prompt missing")
check(prompt_template.scan("<SESSION_ID>").length == 1, "participant prompt must contain one <SESSION_ID>")
check(contract.include?("FACILITATOR_SHEET.md") && !contract.include?(prompt_template),
      "active contract must reference, not duplicate, facilitator §2 prompt")
prompt_hash = Digest::SHA256.hexdigest(ascii_whitespace_fold(prompt_template))
checkpoint_prompt_hash = RUN_CHECKPOINT_PATH.read[/task-message template SHA-256: `([0-9a-f]{64})`/, 1]
check(checkpoint_prompt_hash == prompt_hash, "checkpoint prompt hash #{checkpoint_prompt_hash.inspect} != #{prompt_hash}")
check(sheet.include?("tools.mcp__node_repl__js") && sheet.include?("org.godotengine.godot"),
      "facilitator direct transport target")
check(contract.include?("v1·v2·v3·v4는 합산하지 않는다"), "run-version evidence separation")
check(contract.include?("`evidenceId`") && contract.include?("<sessionId>-launch1"), "replacement evidence identity")
check(contract.include?("literal wrapper 문법이나 첫 호출 성공 여부는") &&
      contract.include?("repository·source/data·diagnostic/log"), "v4 content-source validity boundary")
check(contract.include?("`TechnicalValid = false` launch는 원인과 무관하게 최대 두 번"), "v4 replacement boundary")
check(sheet.include?("ascii-whitespace-fold-v1"), "v4 prompt normalization policy")

def scope0b_decision(valid_sessions:, field_passes:, integrated_passes:, conclusion_passes:, one_family:, safe_fix:, budget_available:)
  check(field_passes.keys.sort == %i[coverage interaction risk utility], "gate field keys")
  check(conclusion_passes.keys.sort == %i[coverage risk utility], "gate conclusion keys")
  return "PROXY-RUN-BLOCKED" unless valid_sessions == 5
  return "GO" if field_passes.values.all? { |count| count >= 4 } && integrated_passes >= 3

  revisable = conclusion_passes.values.all? { |count| count >= 4 } && one_family && safe_fix && budget_available
  revisable ? "REVISE" : "NO-GO"
end

four = { interaction: 4, coverage: 4, risk: 4, utility: 4 }
five = { interaction: 5, coverage: 5, risk: 5, utility: 5 }
short = { interaction: 3, coverage: 5, risk: 5, utility: 5 }
conclusion_five = { coverage: 5, risk: 5, utility: 5 }
conclusion_short = { coverage: 3, risk: 5, utility: 5 }
check(scope0b_decision(valid_sessions: 4, field_passes: five, integrated_passes: 5, conclusion_passes: conclusion_five, one_family: true, safe_fix: true, budget_available: true) == "PROXY-RUN-BLOCKED", "gate blocked boundary")
check(scope0b_decision(valid_sessions: 5, field_passes: four, integrated_passes: 3, conclusion_passes: conclusion_five, one_family: false, safe_fix: false, budget_available: false) == "GO", "gate pass boundary")
check(scope0b_decision(valid_sessions: 5, field_passes: short, integrated_passes: 2, conclusion_passes: conclusion_five, one_family: true, safe_fix: true, budget_available: true) == "REVISE", "gate revise boundary")
check(scope0b_decision(valid_sessions: 5, field_passes: short, integrated_passes: 2, conclusion_passes: conclusion_short, one_family: true, safe_fix: true, budget_available: true) == "NO-GO", "gate repeated conclusion boundary")
check(scope0b_decision(valid_sessions: 5, field_passes: short, integrated_passes: 2, conclusion_passes: conclusion_five, one_family: false, safe_fix: true, budget_available: true) == "NO-GO", "gate multiple family boundary")
check(scope0b_decision(valid_sessions: 5, field_passes: short, integrated_passes: 2, conclusion_passes: conclusion_five, one_family: true, safe_fix: true, budget_available: false) == "NO-GO", "gate budget boundary")
puts "PASS documents: 0 missing local links, 0 stale candidate references, gate and versions frozen"
puts "PASS gate: blocked/GO/REVISE/NO-GO boundaries and incomplete-session scoring"
puts "PASS fixture-hash: #{fixture_hash}"
puts "Scope 0B contract preflight: PASS"
