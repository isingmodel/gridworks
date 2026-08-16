#!/usr/bin/env ruby

require "digest"
require "json"
require "pathname"
require "set"
require "uri"

ROOT = Pathname(__dir__).join("../..").expand_path
FIXTURE_PATH = ROOT.join("data/scope-0b-v1.json")
CONTRACT_PATH = ROOT.join("docs/scopes/SCOPE_0B_PLAYABLE.md")

def check(condition, message)
  raise message unless condition
end

def exact_keys(object, keys, label)
  check(object.keys.sort == keys.sort, "#{label} keys: #{object.keys.sort.inspect}")
end

fixture = JSON.parse(FIXTURE_PATH.read)
exact_keys(
  fixture,
  %w[
    schemaVersion fixtureId displayName units calendar economy nodes edges projects loads requirements
    permittedSupplyPaths evaluationCases events hospitalInternalPower milestones verificationOnly
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
  "PROJECT_TOWN_FEEDER" => ["TOWN_FEEDER", 6_000_000, 0, 1_440, "town_connection"],
  "PROJECT_RIVER_PARALLEL" => ["RIVER_PARALLEL", 8_000_000, 1_440, 2_880, "hospital_corridor"],
  "PROJECT_NORTH_DETOUR" => ["NORTH_DETOUR", 12_000_000, 1_440, 2_880, "hospital_corridor"]
}
projects.each { |project| exact_keys(project, %w[id edgeId costCashUnit allowedOrderMinute buildMinutes orderWindow], "project #{project.fetch('id')}") }
actual_projects = projects.to_h do |project|
  [project.fetch("id"), %w[edgeId costCashUnit allowedOrderMinute buildMinutes orderWindow].map { |key| project.fetch(key) }]
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
check(fixture.fetch("milestones").map { |milestone| milestone.fetch("minute") } == [0, 1_440, 4_320, 13_440, 13_680], "public milestones")

initial_commissioned = edges.select { |edge| edge.fetch("initialConstructionState") == "commissioned" }.map { |edge| edge.fetch("id") }.to_set
design_edge = { "NO_BUILD" => nil, "RIVER_PARALLEL" => "RIVER_PARALLEL", "NORTH_DETOUR" => "NORTH_DETOUR" }
backup_path = { "RIVER_PARALLEL" => "HOSPITAL_RIVER_BACKUP_PATH", "NORTH_DETOUR" => "HOSPITAL_NORTH_BACKUP_PATH" }

def removed_edges(edges, removal_case)
  key = removal_case.fetch("selectorType")
  value = removal_case.fetch("selectorValue")
  edges.select { |edge| edge.fetch(key) == value }.map { |edge| edge.fetch("id") }.to_set
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
expected_outcomes = verification.fetch("evaluationOutcomes")
expected_outcomes.each do |outcome|
  design = outcome.fetch("design")
  installed = initial_commissioned | Set["TOWN_FEEDER"]
  installed << design_edge.fetch(design) unless design_edge.fetch(design).nil?
  removed = removed_edges(edges, case_by_id.fetch(outcome.fetch("caseId")))
  selected = choose_paths(design, installed, removed, path_by_id, backup_path)
  actual = [selected.key?("TOWN_LOAD"), selected.key?("HOSPITAL_LOAD")]
  expected = [outcome.fetch("townUtilityDelivered"), outcome.fetch("hospitalUtilityDelivered")]
  check(actual == expected, "evaluation outcome #{design}/#{outcome.fetch('caseId')}: #{actual.inspect}")

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

def slug(text)
  text.downcase.gsub(/[`*_~]/, "").gsub(/[^\p{Alnum}\p{M}\s_-]/u, "").strip.gsub(/\s+/, "-")
end

missing_links = []
ROOT.glob("**/*.md").each do |markdown|
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
check(ROOT.glob("**/*.md").none? { |path| path.read.include?("SCOPE_0B_CANDIDATE.md") }, "stale candidate reference")
contract = CONTRACT_PATH.read
%w[S0B-FIXTURE-v1 S0B-BUILD-v1 S0B-PROXY-v1 S0B-GATE-v1].each { |version| check(contract.include?(version), "contract version #{version}") }
check(contract.include?("각각 `4/5` 이상") && contract.include?("`3/5` 이상"), "gate thresholds")
check(contract.include?("ActiveKnob = 0"), "parameter policy")
puts "PASS documents: 0 missing local links, 0 stale candidate references, gate and versions frozen"
puts "PASS fixture-hash: #{Digest::SHA256.file(FIXTURE_PATH).hexdigest}"
puts "Scope 0B contract preflight: PASS"
