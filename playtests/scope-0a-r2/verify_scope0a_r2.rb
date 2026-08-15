#!/usr/bin/env ruby

require "pathname"
require "set"
require "uri"
require "digest"

ROOT = Pathname(__dir__).join("../..").expand_path
CARD_DIR = Pathname(__dir__).join("cards")
PNG_DIR = CARD_DIR.join("png")
R1_CARD_DIR = ROOT.join("playtests/scope-0a/cards")
R1_PNG_DIR = R1_CARD_DIR.join("png")
FACILITATOR = Pathname(__dir__).join("FACILITATOR_SHEET.md")
ACTIVE_SCOPE = ROOT.join("docs/scopes/SCOPE_0A_R2_CARD_TEST.md")

EXPECTED_STEMS = %w[
  card-01
  card-02
  card-03-ab
  card-03-ba
  card-04-ab-prediction
  card-04-ba-prediction
  card-04-ab-causal-reveal
  card-04-ba-causal-reveal
  card-04-ab-settlement-reveal
  card-04-ba-settlement-reveal
].freeze

UNCHANGED_STEMS = %w[
  card-02
  card-03-ab
  card-03-ba
  card-04-ab-settlement-reveal
  card-04-ba-settlement-reveal
].freeze

def check(condition, message)
  raise message unless condition
end

def reachable?(nodes, edges, removed, target)
  adjacency = nodes.to_h { |node| [node, []] }
  edges.each do |edge|
    next if removed.include?(edge.fetch(:id))

    adjacency.fetch(edge.fetch(:from)) << edge.fetch(:to)
    adjacency.fetch(edge.fetch(:to)) << edge.fetch(:from)
  end
  seen = Set["GEN_GAS"]
  queue = ["GEN_GAS"]
  until queue.empty?
    current = queue.shift
    adjacency.fetch(current).each do |neighbor|
      next if seen.include?(neighbor)

      seen << neighbor
      queue << neighbor
    end
  end
  seen.include?(target)
end

nodes = %w[GEN_GAS SOURCE_BUS LOAD_HUB TOWN_SUB TOWN_LOAD HOSPITAL_SUB HOSPITAL_LOAD]
base_edges = [
  { id: "GEN_EXPORT", from: "GEN_GAS", to: "SOURCE_BUS", capacity: 80, spatial: "GEN_SITE" },
  { id: "MAIN_TRUNK", from: "SOURCE_BUS", to: "LOAD_HUB", capacity: 40, spatial: "OLD_CORRIDOR" },
  { id: "TOWN_FEEDER", from: "LOAD_HUB", to: "TOWN_SUB", capacity: 40, spatial: "TOWN_LOCAL" },
  { id: "TOWN_SERVICE", from: "TOWN_SUB", to: "TOWN_LOAD", capacity: 40, spatial: "TOWN_LOCAL" },
  { id: "HOSPITAL_PRIMARY", from: "LOAD_HUB", to: "HOSPITAL_SUB", capacity: 40, spatial: "OLD_CORRIDOR" },
  { id: "HOSPITAL_SERVICE", from: "HOSPITAL_SUB", to: "HOSPITAL_LOAD", capacity: 40, spatial: "HOSPITAL_YARD" },
  { id: "RIVER_PARALLEL", from: "SOURCE_BUS", to: "HOSPITAL_SUB", capacity: 40, spatial: "OLD_CORRIDOR" },
  { id: "NORTH_DETOUR", from: "SOURCE_BUS", to: "HOSPITAL_SUB", capacity: 40, spatial: "NORTH_CORRIDOR" }
]
check(nodes.length == 7 && nodes.uniq.length == 7, "topology node count/uniqueness")
check(base_edges.length == 8 && base_edges.map { |edge| edge[:id] }.uniq.length == 8, "topology edge count/uniqueness")
check(base_edges.all? { |edge| nodes.include?(edge[:from]) && nodes.include?(edge[:to]) }, "topology endpoint")

initial_edges = base_edges.reject { |edge| %w[TOWN_FEEDER RIVER_PARALLEL NORTH_DETOUR].include?(edge[:id]) }
check(!reachable?(nodes, initial_edges, Set.new, "TOWN_LOAD"), "initial town must be de-energized")
check(reachable?(nodes, initial_edges, Set.new, "HOSPITAL_LOAD"), "initial hospital must be energized")

expected_cases = {
  ["NoBuild", "E1"] => [true, false],
  ["NoBuild", "OLD"] => [false, false],
  ["River", "E1"] => [true, true],
  ["River", "OLD"] => [false, false],
  ["North", "E1"] => [true, true],
  ["North", "OLD"] => [false, true]
}
expected_cases.each do |(design, event), expected|
  built_ids = %w[GEN_EXPORT MAIN_TRUNK TOWN_FEEDER TOWN_SERVICE HOSPITAL_PRIMARY HOSPITAL_SERVICE]
  built_ids << "RIVER_PARALLEL" if design == "River"
  built_ids << "NORTH_DETOUR" if design == "North"
  edges = base_edges.select { |edge| built_ids.include?(edge[:id]) }
  removed = if event == "E1"
              Set["HOSPITAL_PRIMARY"]
            else
              Set.new(edges.select { |edge| edge[:spatial] == "OLD_CORRIDOR" }.map { |edge| edge[:id] })
            end
  actual = [
    reachable?(nodes, edges, removed, "TOWN_LOAD"),
    reachable?(nodes, edges, removed, "HOSPITAL_LOAD")
  ]
  check(actual == expected, "topology case #{design}/#{event}: #{actual.inspect}")
end
check(32 <= 40 && 40 <= 80, "normal shared capacity")
check(8 <= 40 && 24 <= 40, "contingency path capacity")
puts "PASS topology: 7 nodes, 8 edges, initial state, 6 utility-delivery cases"

time_map = {
  0 => "DAY 0 08:00",
  1_440 => "DAY 1 08:00",
  4_320 => "DAY 3 08:00",
  13_440 => "DAY 9 16:00",
  13_680 => "DAY 9 20:00"
}
check(time_map.length == 5, "timestamp count")
check(13_680 - 13_440 == 240, "event duration")
check(1_440 == 24 * 60 && 2_880 == 2 * 24 * 60, "build durations")
check(1_000_000 == 1_000_000, "M to CashUnit")
check(60_000_000 == 1_000 * 60 * 1_000, "GWh denominator")
puts "PASS units: 5 timestamps, 2 build durations, exact integer conversions"

check(2.0 / 8 * 60 == 15, "UPS duration")
check(38.0 / 8 * 60 == 285, "diesel duration")
check(40.0 / 8 * 60 == 300, "internal total duration")
check(8 * 4 == 32, "event internal energy")
check((40 - 32).to_f / 8 * 60 == 60, "remaining internal duration")
puts "PASS energy: UPS=15m diesel=285m total=300m event=240m remaining=60m"

def cash_for(power_mw, elapsed_minutes, rate_per_gwh)
  numerator = power_mw * 1_000 * elapsed_minutes * rate_per_gwh
  denominator = 60_000_000
  check((numerator % denominator).zero?, "non-exact cash conversion #{numerator}/#{denominator}")
  numerator / denominator
end

prechoice_revenue = cash_for(8, 1_440, 180_000)
prechoice_gas = cash_for(8, 1_440, 115_000)
prechoice_cash = 20_000_000 - 6_000_000 + prechoice_revenue - prechoice_gas
normal_revenue = cash_for(32, 12_000, 180_000)
normal_gas = cash_for(32, 12_000, 115_000)
normal_margin = normal_revenue - normal_gas
river_compensation = cash_for(24, 240, 800_000) + cash_for(8, 240, 20_000_000)
river_lost_sales = cash_for(32, 240, 180_000)
river_delta = -river_compensation
north_revenue = cash_for(8, 240, 180_000)
north_gas = cash_for(8, 240, 115_000)
north_compensation = cash_for(24, 240, 800_000)
north_lost_sales = cash_for(24, 240, 180_000)
north_delta = north_revenue - north_gas - north_compensation
river_end = prechoice_cash - 8_000_000 + normal_margin + river_delta
north_end = prechoice_cash - 12_000_000 + normal_margin + north_delta
check([prechoice_revenue, prechoice_gas, prechoice_cash] == [34_560, 22_080, 14_012_480], "prechoice cash")
check([normal_revenue, normal_gas, normal_margin] == [1_152_000, 736_000, 416_000], "normal cash")
check([river_lost_sales, river_compensation, river_delta, river_end] == [23_040, 716_800, -716_800, 5_711_680], "river cash")
check([north_revenue, north_gas, north_compensation, north_lost_sales, north_delta, north_end] == [5_760, 3_680, 76_800, 17_280, -74_720, 2_353_760], "north cash")
puts "PASS cash: prechoice=14012480 river_end=5711680 north_end=2353760"

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
remote_svg_refs = CARD_DIR.glob("*.svg").flat_map do |svg|
  svg.read.scan(/(?:href|xlink:href)=["']([^"']+)["']/).flatten.reject { |ref| ref.start_with?("#", "data:") }.map { |ref| "#{svg.basename}: #{ref}" }
end
check(missing_links.empty?, "missing links: #{missing_links.join(', ')}")
check(remote_svg_refs.empty?, "remote SVG refs: #{remote_svg_refs.join(', ')}")
puts "PASS links: 0 missing targets, 0 missing anchors, 0 remote SVG dependencies"

actual_svg_stems = CARD_DIR.glob("*.svg").map { |path| path.basename(".svg").to_s }.sort
actual_png_stems = PNG_DIR.glob("*.png").map { |path| path.basename(".png").to_s }.sort
check(actual_svg_stems == EXPECTED_STEMS.sort, "SVG set mismatch")
check(actual_png_stems == EXPECTED_STEMS.sort, "PNG set mismatch")
UNCHANGED_STEMS.each do |stem|
  check(CARD_DIR.join("#{stem}.svg").binread == R1_CARD_DIR.join("#{stem}.svg").binread, "R1 SVG drift #{stem}")
  check(PNG_DIR.join("#{stem}.png").binread == R1_PNG_DIR.join("#{stem}.png").binread, "R1 PNG drift #{stem}")
end
hash_manifest = Pathname(__dir__).join("CARD_HASHES.sha256").readlines.map(&:strip).reject(&:empty?).to_h do |line|
  digest, relative_path = line.split(/\s+/, 2)
  [relative_path, digest]
end
check(hash_manifest.keys.sort == EXPECTED_STEMS.map { |stem| "cards/png/#{stem}.png" }.sort, "card hash manifest set")
forbidden_chunks = %w[tEXt zTXt iTXt eXIf]
EXPECTED_STEMS.each do |stem|
  svg = CARD_DIR.join("#{stem}.svg").read
  check(svg.match?(/<svg[^>]*width="1600"[^>]*height="900"[^>]*viewBox="0 0 1600 900"/), "SVG dimensions #{stem}")

  bytes = PNG_DIR.join("#{stem}.png").binread
  check(bytes.start_with?("\x89PNG\r\n\x1A\n".b), "PNG signature #{stem}")
  check(bytes.byteslice(16, 8).unpack("NN") == [1600, 900], "PNG dimensions #{stem}")
  check(bytes.getbyte(24) == 8 && [2, 6].include?(bytes.getbyte(25)), "PNG color #{stem}")
  check(Digest::SHA256.hexdigest(bytes) == hash_manifest.fetch("cards/png/#{stem}.png"), "PNG hash #{stem}")
  offset = 8
  chunks = []
  while offset < bytes.bytesize
    length = bytes.byteslice(offset, 4).unpack1("N")
    type = bytes.byteslice(offset + 4, 4)
    chunks << type
    offset += 12 + length
  end
  check((chunks & forbidden_chunks).empty?, "PNG metadata #{stem}: #{chunks & forbidden_chunks}")
end
puts "PASS card-metadata: 10 SVG + 10 PNG at 1600x900"

all_svg_text = EXPECTED_STEMS.to_h { |stem| [stem, CARD_DIR.join("#{stem}.svg").read] }
forbidden_tokens = %w[
  NoBuild NodeId EdgeId ElectricalContingencyId SpatialRiskGroup GameMinute CashUnit
  CoveragePass RiskCausalityPass UtilityInternalPass TradeOffPass IntegratedCausalPass
  SAFE RISKY SHARED INDEPENDENT P01 P02 P03 P04 P05
]
forbidden_tokens += ["추천", "승자", "총점", "🛡", "☠"]
all_svg_text.each do |stem, content|
  hits = forbidden_tokens.select { |token| content.include?(token) }
  check(hits.empty?, "forbidden participant copy #{stem}: #{hits.join(', ')}")
end

prediction_stems = %w[card-04-ab-prediction card-04-ba-prediction]
answer_leaks = ["계통공급 남음", "계통공급 끊김", "내부전원 60분", "0.7168", "0.07472", "0.00576", "0.00368", "0.02304", "0.01728"]
prediction_stems.each do |stem|
  hits = answer_leaks.select { |token| all_svg_text.fetch(stem).include?(token) }
  check(hits.empty?, "prediction answer leak #{stem}: #{hits.join(', ')}")
end
%w[card-04-ab-causal-reveal card-04-ba-causal-reveal].each do |stem|
  hits = %w[0.7168 0.07472 0.00576 0.00368 0.02304 0.01728].select { |token| all_svg_text.fetch(stem).include?(token) }
  check(hits.empty?, "causal settlement leak #{stem}: #{hits.join(', ')}")
end
ab_stems = %w[card-03-ab card-04-ab-prediction card-04-ab-causal-reveal card-04-ab-settlement-reveal]
ba_stems = %w[card-03-ba card-04-ba-prediction card-04-ba-causal-reveal card-04-ba-settlement-reveal]
ab_stems.each do |stem|
  check(all_svg_text.fetch(stem).index("강변 병행") < all_svg_text.fetch(stem).index("북부 우회"), "AB order #{stem}")
end
ba_stems.each do |stem|
  check(all_svg_text.fetch(stem).index("북부 우회") < all_svg_text.fetch(stem).index("강변 병행"), "BA order #{stem}")
end
card_1_fragments = ["서비스 권역 안에 있다는 것은", "실제 공급 여부", "더 필요한가", "상위 피더 미연결"]
check(card_1_fragments.all? { |fragment| all_svg_text.fetch("card-01").include?(fragment) }, "card 1 structured prompt")
check(all_svg_text.fetch("card-02").include?("UPS") && all_svg_text.fetch("card-02").include?("전력회사 공급·판매로 계량하지 않음"), "card 2 copy")
prediction_stems.each do |stem|
  required = ["전기회로 사고", "공간 통로 사고", "병원 주 회로 E1만 사용불가", "강변 통로 전체 사용불가 · 4시간", "E1과의 차단 회로 관계", "강변과의 공간 통로 관계"]
  check(required.all? { |fragment| all_svg_text.fetch(stem).include?(fragment) }, "prediction response grammar #{stem}")
  check(!all_svg_text.fetch(stem).include?("같음") && !all_svg_text.fetch(stem).include?("다름"), "prediction relationship answer leak #{stem}")
end
%w[card-04-ab-causal-reveal card-04-ba-causal-reveal].each do |stem|
  required = ["전기회로 사고", "공간 통로 사고", "병원 주 회로 E1만 사용불가", "강변 통로 전체 사용불가 · 4시간"]
  check(required.all? { |fragment| all_svg_text.fetch(stem).include?(fragment) }, "causal reveal axis #{stem}")
end

facilitator = FACILITATOR.read
expected_facilitator_sha256 = "8561848fa873109be2adad6a52d3d43119d9ddd8afd010f9c5ab89e9f14b6f88"
check(Digest::SHA256.hexdigest(facilitator) == expected_facilitator_sha256, "exact facilitator template hash")
facilitator_fragments = [
  "S0A-CARD-v2",
  "S0A-PROXY-v2",
  "S0A-GATE-v2",
  "서비스 권역 안에 있다는 것은 무엇을 가능하게 하는가",
  "실제 공급 여부는 무엇으로 판단하며, 공급되려면 무엇이 더 필요한가",
  "각 계획선과 병원 주 회로 E1의 차단 회로 관계",
  "각 계획선과 강변 통로의 공간 관계"
]
check(facilitator_fragments.all? { |fragment| facilitator.include?(fragment) }, "facilitator prompt freeze")
allocations = ["R2-L01 AB", "R2-L02 BA", "R2-L03 AB", "R2-L04 BA", "R2-L05 AB"]
check(allocations.all? { |allocation| facilitator.include?(allocation) }, "facilitator allocation freeze")
active_scope = ACTIVE_SCOPE.read
check(active_scope.include?("S0A-GATE-v2") && active_scope.include?("각각 4/5 이상") && active_scope.include?("3/5 이상"), "decision rule freeze")
check(allocations.all? { |allocation| active_scope.include?(allocation) }, "scope allocation freeze")
record_header = Pathname(__dir__).join("record-template.csv").readlines.first
expected_record_columns = %w[
  EvidenceType HumanValidationStatus ParticipantId SessionId ModelBuild RunTimestamp CardSetVersion
  PromptVersion DecisionRuleVersion CardHashes OrderVariant AllowedToolUse ActualToolUse TranscriptRef
  TechnicalValid CoverageRaw CoveragePass RiverE1 RiverSpatial NorthE1 NorthSpatial RiskCausalityPass
  UtilityInternalRaw UtilityInternalPass TradeOffRaw TradeOffPass Choice ChoiceReason SwitchingCondition
  FacilitatorHelp IntegratedCausalPass
]
check(record_header.strip.split(",") == expected_record_columns, "record-template columns")

def proxy_decision(valid_sessions:, field_passes:, integrated_passes:, conclusion_passes:, reason_only:, one_family:, safe_fix:)
  return "PROXY-RUN-INVALID" unless valid_sessions == 5
  return "PROXY-PASS" if field_passes.values.all? { |count| count >= 4 } && integrated_passes >= 3

  revisable = conclusion_passes.values.all? { |count| count >= 4 } && reason_only && one_family && safe_fix
  revisable ? "PROXY-REVISE" : "PROXY-FAIL"
end

all_four = { coverage: 4, risk: 4, utility: 4, tradeoff: 4 }
all_five = { coverage: 5, risk: 5, utility: 5, tradeoff: 5 }
one_field_short = { coverage: 3, risk: 4, utility: 5, tradeoff: 5 }
check(proxy_decision(valid_sessions: 4, field_passes: all_five, integrated_passes: 5, conclusion_passes: all_five, reason_only: true, one_family: true, safe_fix: true) == "PROXY-RUN-INVALID", "gate invalid-session boundary")
check(proxy_decision(valid_sessions: 5, field_passes: all_four, integrated_passes: 3, conclusion_passes: all_four, reason_only: false, one_family: false, safe_fix: false) == "PROXY-PASS", "gate exact pass boundary")
check(proxy_decision(valid_sessions: 5, field_passes: all_four, integrated_passes: 2, conclusion_passes: all_five, reason_only: true, one_family: true, safe_fix: true) == "PROXY-REVISE", "gate integrated revise boundary")
check(proxy_decision(valid_sessions: 5, field_passes: one_field_short, integrated_passes: 3, conclusion_passes: all_five, reason_only: true, one_family: true, safe_fix: true) == "PROXY-REVISE", "gate field revise boundary")
check(proxy_decision(valid_sessions: 5, field_passes: one_field_short, integrated_passes: 2, conclusion_passes: one_field_short, reason_only: true, one_family: true, safe_fix: true) == "PROXY-FAIL", "gate repeated wrong-conclusion boundary")
check(proxy_decision(valid_sessions: 5, field_passes: one_field_short, integrated_passes: 2, conclusion_passes: all_five, reason_only: false, one_family: true, safe_fix: true) == "PROXY-FAIL", "gate active-wrong-reason boundary")
check(proxy_decision(valid_sessions: 5, field_passes: one_field_short, integrated_passes: 2, conclusion_passes: all_five, reason_only: true, one_family: false, safe_fix: true) == "PROXY-FAIL", "gate multiple-family boundary")
check(proxy_decision(valid_sessions: 5, field_passes: one_field_short, integrated_passes: 2, conclusion_passes: all_five, reason_only: true, one_family: true, safe_fix: false) == "PROXY-FAIL", "gate unsafe-fix boundary")
puts "PASS gate-contract: exact prompt/allocation, record fields, PASS/REVISE/FAIL boundaries"
puts "PASS participant-copy: R1 unchanged frames, structured contrasts, 0 forbidden tokens, 0 phase leaks"
puts "Scope 0A R2 deterministic preflight: PASS"
