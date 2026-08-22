#!/usr/bin/env python3
"""Build a hash-pinned, label-blind evidence bundle for the text-plan verifier."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
from pathlib import Path
from typing import Any


TOOL_DIRECTORY = Path(__file__).resolve().parent
JUDGE_PROMPT_PATH = TOOL_DIRECTORY / "text-plan-judge-prompt.template.txt"
JUDGMENT_SCHEMA_PATH = TOOL_DIRECTORY / "text-plan-judge.schema.json"
VERIFIER_PROMPT_PATH = TOOL_DIRECTORY / "text-plan-evidence-verifier-prompt.template.txt"
VERIFIER_SCHEMA_PATH = TOOL_DIRECTORY / "text-plan-evidence-verifier.schema.json"
RUBRIC_PATH = TOOL_DIRECTORY / "rubric.json"
TEXT_PLAN_ENVELOPE_SCHEMA = "gridworks.commercial-ux.text-plan-envelope.v1"
TEXT_PLAN_ARTIFACT_SCHEMA = "gridworks.commercial-ux.text-plan-input.v1"
STORY_PART_SCHEMA = "gridworks.commercial.story-part-output.v1"
JUDGMENT_PROTOCOL = "GRIDWORKS-COMMERCIAL-UX-TEXT-PLAN-JUDGMENT-v1"
EVIDENCE_INPUT_SCHEMA = "gridworks.commercial-ux.text-plan-evidence-input.v1"
EVIDENCE_ENVELOPE_SCHEMA = "gridworks.commercial-ux.text-plan-evidence-envelope.v1"
EVIDENCE_INPUT_PROTOCOL = "GRIDWORKS-COMMERCIAL-UX-TEXT-PLAN-EVIDENCE-INPUT-v1"
AGGREGATE_PROTOCOL = "GRIDWORKS-COMMERCIAL-UX-TEXT-PLAN-AGGREGATE-v1"
CAMPAIGN_ID = "CHEONGRYU_COMMERCIAL_CAMPAIGN_V2"
SHA256_PATTERN = re.compile(r"^sha256:[0-9a-f]{64}$")
FORBIDDEN_OBSERVATION_PATTERN = re.compile(
    r"(?:\b(?:EXCELLENT|STRONG|SERVICEABLE|WEAK|BROKEN)\b"
    r"|\b(?:TextPlanProxy|CommercialUXProxy|recommendedChange)\b"
    r"|(?<![0-9])87(?:\.0+)?(?![0-9]))"
)
EXPECTED_LABELS = {"EXCELLENT", "STRONG", "SERVICEABLE", "WEAK", "BROKEN"}
EXPECTED_CELL_IDS = (
    "TP-J1",
    "TP-J2",
    "TP-T1",
    "TP-T2",
    "TP-T3",
    "TP-C1",
    "TP-C2",
    "TP-C3",
    "TP-C4",
    "TP-C5",
    "TP-A1",
    "TP-A2",
    "TP-A3",
    "TP-A4",
    "TP-P1",
    "TP-P2",
    "TP-P3",
    "TP-K1",
    "TP-K2",
    "TP-K3",
)
EXPECTED_SELECTORS = (
    "FIRST_LIGHT/briefing",
    "FIRST_LIGHT/result/standard",
    "SECOND_HEART/briefing",
    "SECOND_HEART/result/standard",
    "SECOND_SOURCE/briefing",
    "SECOND_SOURCE/window/SECOND_SOURCE_BUILD",
    "SECOND_SOURCE/result/standard",
    "NORTH_BANK_PROMISE/briefing",
    "NORTH_BANK_PROMISE/result/keep",
    "NORTH_BANK_PROMISE/result/defer",
    "WHOSE_MARGIN/briefing",
    "WHOSE_MARGIN/window/AFTER_HEAT_SAFETY",
    "WHOSE_MARGIN/result/keep",
    "WHOSE_MARGIN/result/defer",
    "BEFORE_WATER_REACHES/briefing",
    "BEFORE_WATER_REACHES/window/FLOOD_BYPASS_BUILD",
    "BEFORE_WATER_REACHES/result/standard",
    "SHUT_DOWN_TO_KEEP/briefing",
    "SHUT_DOWN_TO_KEEP/window/MAINTENANCE_BYPASS_BUILD",
    "SHUT_DOWN_TO_KEEP/result/keep",
    "SHUT_DOWN_TO_KEEP/result/defer",
    "LONGEST_NIGHT/briefing",
    "LONGEST_NIGHT/window/LAST_STORM_APPROVAL",
    "LONGEST_NIGHT/result/keep",
    "LONGEST_NIGHT/result/defer",
    "campaign/epilogue",
)
AGGREGATE_KEYS = {
    "protocol",
    "status",
    "textPlanProxy",
    "commercialUXProxy",
    "officialCommercialUX",
    "panelKind",
    "rerunRequired",
    "panelInputSha256",
    "replacementForPanelInputSha256",
    "replacementReceiptSha256",
    "rubricSha256",
    "promptTemplateSha256",
    "judgmentSchemaSha256",
    "textRaw",
    "textRawSpread",
    "disagreementPenalty",
    "textPlanSha256",
    "judgeRunIds",
    "cellScores",
    "categoryScores",
    "unstableCells",
    "blockers",
}


def fail(message: str) -> None:
    raise SystemExit(message)


def read_object(path: Path, label: str) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exception:
        fail(f"{label} is unreadable: {exception}")
    if not isinstance(value, dict):
        fail(f"{label} must be a JSON object")
    return value


def exact_keys(value: dict[str, Any], expected: set[str], label: str) -> None:
    actual = set(value)
    if actual != expected:
        fail(
            f"{label} keys mismatch: missing={sorted(expected - actual)}, "
            f"extra={sorted(actual - expected)}"
        )


def nonempty_string(value: Any, label: str, maximum: int | None = None) -> str:
    if not isinstance(value, str) or not value.strip():
        fail(f"{label} must be a nonempty string")
    if maximum is not None and len(value) > maximum:
        fail(f"{label} must contain at most {maximum} characters")
    return value


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def sha256_identifier(value: Any) -> str:
    return "sha256:" + hashlib.sha256(canonical_json_bytes(value)).hexdigest()


def file_sha256_identifier(path: Path, label: str) -> str:
    try:
        content = path.read_bytes()
    except OSError as exception:
        fail(f"{label} is unreadable: {exception}")
    return "sha256:" + hashlib.sha256(content).hexdigest()


def finite_number(value: Any, label: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        fail(f"{label} must be a finite number")
    number = float(value)
    if not math.isfinite(number):
        fail(f"{label} must be a finite number")
    return number


def canonical_file_object_sha256(path: Path, label: str) -> str:
    payload = read_object(path, label)
    return sha256_identifier(payload)


def validate_story_part(part: Any, index: int) -> str:
    label = f"text-plan artifact storyParts[{index}]"
    if not isinstance(part, dict):
        fail(f"{label} must be an object")
    exact_keys(
        part,
        {
            "schemaVersion",
            "campaignId",
            "selector",
            "kind",
            "chapterId",
            "windowId",
            "reachable",
            "requiredPromiseBranch",
            "story",
        },
        label,
    )
    if part["schemaVersion"] != STORY_PART_SCHEMA:
        fail(f"{label}.schemaVersion must be {STORY_PART_SCHEMA}")
    if part["campaignId"] != CAMPAIGN_ID:
        fail(f"{label}.campaignId must be {CAMPAIGN_ID}")
    selector = nonempty_string(part["selector"], f"{label}.selector")
    if part["reachable"] is not True:
        fail(f"{label}.reachable must be true")
    story = part["story"]
    if not isinstance(story, dict):
        fail(f"{label}.story must be an object")
    exact_keys(story, {"speaker", "title", "body"}, f"{label}.story")
    for field in ("speaker", "title", "body"):
        nonempty_string(story[field], f"{label}.story.{field}")
    return selector


def load_text_plan(path: Path) -> tuple[dict[str, Any], str, set[str]]:
    envelope = read_object(path, "text-plan envelope")
    exact_keys(
        envelope,
        {"schemaVersion", "artifactSha256", "artifact"},
        "text-plan envelope",
    )
    if envelope["schemaVersion"] != TEXT_PLAN_ENVELOPE_SCHEMA:
        fail(f"text-plan envelope schemaVersion must be {TEXT_PLAN_ENVELOPE_SCHEMA}")
    declared_sha = envelope["artifactSha256"]
    if not isinstance(declared_sha, str) or SHA256_PATTERN.fullmatch(declared_sha) is None:
        fail("text-plan envelope artifactSha256 must be a lowercase sha256 identifier")
    artifact = envelope["artifact"]
    if not isinstance(artifact, dict):
        fail("text-plan envelope artifact must be an object")
    exact_keys(
        artifact,
        {
            "schemaVersion",
            "campaignId",
            "premise",
            "playerRole",
            "chapters",
            "storyParts",
        },
        "text-plan artifact",
    )
    if artifact["schemaVersion"] != TEXT_PLAN_ARTIFACT_SCHEMA:
        fail(f"text-plan artifact schemaVersion must be {TEXT_PLAN_ARTIFACT_SCHEMA}")
    if artifact["campaignId"] != CAMPAIGN_ID:
        fail(f"text-plan artifact campaignId must be {CAMPAIGN_ID}")
    nonempty_string(artifact["premise"], "text-plan artifact premise")
    nonempty_string(artifact["playerRole"], "text-plan artifact playerRole")
    computed_sha = sha256_identifier(artifact)
    if declared_sha != computed_sha:
        fail(
            "text-plan artifact hash mismatch: "
            f"declared {declared_sha}, computed {computed_sha}"
        )

    chapters = artifact["chapters"]
    if not isinstance(chapters, list) or len(chapters) != 8:
        fail("text-plan artifact chapters must contain exactly eight rows")
    allowed_source_refs = {"context:premise", "context:playerRole"}
    chapter_ids: set[str] = set()
    chapter_fields = {
        "order",
        "chapterId",
        "displayName",
        "phase",
        "learningIntent",
        "crisisIntent",
        "choiceIntent",
    }
    for index, chapter in enumerate(chapters, start=1):
        label = f"text-plan artifact chapters[{index - 1}]"
        if not isinstance(chapter, dict):
            fail(f"{label} must be an object")
        exact_keys(chapter, chapter_fields, label)
        if type(chapter["order"]) is not int or chapter["order"] != index:
            fail("text-plan artifact chapter order must be exactly 1..8")
        chapter_id = nonempty_string(chapter["chapterId"], f"{label}.chapterId")
        if chapter_id in chapter_ids:
            fail(f"text-plan artifact duplicate chapterId {chapter_id!r}")
        chapter_ids.add(chapter_id)
        nonempty_string(chapter["displayName"], f"{label}.displayName")
        if chapter["phase"] not in {"tutorial", "main"}:
            fail(f"{label}.phase must be tutorial or main")
        for field in ("learningIntent", "crisisIntent", "choiceIntent"):
            nonempty_string(chapter[field], f"{label}.{field}")
            allowed_source_refs.add(f"chapter:{chapter_id}:{field}")

    story_parts = artifact["storyParts"]
    if not isinstance(story_parts, list) or len(story_parts) != len(EXPECTED_SELECTORS):
        fail("text-plan artifact storyParts must contain exactly 26 rows")
    actual_selectors = [
        validate_story_part(part, index)
        for index, part in enumerate(story_parts)
    ]
    if tuple(actual_selectors) != EXPECTED_SELECTORS:
        fail("text-plan artifact story selector order or coverage drifted")
    allowed_source_refs.update(actual_selectors)
    return artifact, declared_sha, allowed_source_refs


def validate_evidence_rows(
    value: Any,
    label: str,
    allowed_source_refs: set[str],
) -> list[tuple[str, str]]:
    if not isinstance(value, list) or len(value) > 4:
        fail(f"{label} must be an array with at most four rows")
    observations: list[tuple[str, str]] = []
    seen: set[tuple[str, str]] = set()
    for index, row in enumerate(value):
        row_label = f"{label}[{index}]"
        if not isinstance(row, dict):
            fail(f"{row_label} must be an object")
        exact_keys(row, {"sourceRef", "observation"}, row_label)
        source_ref = nonempty_string(row["sourceRef"], f"{row_label}.sourceRef", 240)
        observation = nonempty_string(row["observation"], f"{row_label}.observation", 1200)
        if source_ref not in allowed_source_refs:
            fail(f"{row_label}.sourceRef does not exist in the text-plan artifact: {source_ref!r}")
        if FORBIDDEN_OBSERVATION_PATTERN.search(observation):
            fail(f"{row_label}.observation leaks hidden judgment metadata")
        key = (source_ref, observation)
        if key in seen:
            fail(f"{label} contains duplicate evidence")
        seen.add(key)
        observations.append(key)
    return observations


def extract_judgment_observations(
    payload: dict[str, Any],
    path: Path,
    text_plan_sha256: str,
    allowed_source_refs: set[str],
) -> tuple[str, list[tuple[str, str]]]:
    prefix = str(path)
    exact_keys(
        payload,
        {
            "protocol",
            "judgeRunId",
            "judgeSlot",
            "model",
            "reasoningEffort",
            "promptTemplateSha256",
            "judgmentSchemaSha256",
            "textPlanSha256",
            "cells",
        },
        prefix,
    )
    expected_identity = {
        "protocol": JUDGMENT_PROTOCOL,
        "judgeSlot": "SOL-ULTRA",
        "model": "gpt-5.6-sol",
        "reasoningEffort": "ultra",
        "promptTemplateSha256": file_sha256_identifier(
            JUDGE_PROMPT_PATH,
            "text-plan judge prompt template",
        ),
        "judgmentSchemaSha256": file_sha256_identifier(
            JUDGMENT_SCHEMA_PATH,
            "text-plan judgment schema",
        ),
        "textPlanSha256": text_plan_sha256,
    }
    for field, expected in expected_identity.items():
        if payload[field] != expected:
            fail(f"{prefix}: {field} must be {expected!r}")
    run_id = nonempty_string(payload["judgeRunId"], f"{prefix}.judgeRunId")
    cells = payload["cells"]
    if not isinstance(cells, list) or len(cells) != len(EXPECTED_CELL_IDS):
        fail(f"{prefix}: cells must contain exactly {len(EXPECTED_CELL_IDS)} rows")
    seen_cells: set[str] = set()
    observations: list[tuple[str, str]] = []
    for index, cell in enumerate(cells):
        cell_label = f"{prefix}.cells[{index}]"
        if not isinstance(cell, dict):
            fail(f"{cell_label} must be an object")
        exact_keys(
            cell,
            {"cellId", "label", "confidence", "strengthEvidence", "gapEvidence"},
            cell_label,
        )
        cell_id = cell["cellId"]
        if cell_id not in EXPECTED_CELL_IDS:
            fail(f"{cell_label}: unknown cellId {cell_id!r}")
        if cell_id in seen_cells:
            fail(f"{prefix}: duplicate cellId {cell_id}")
        seen_cells.add(cell_id)
        if cell["label"] not in EXPECTED_LABELS:
            fail(f"{cell_label}.label is invalid")
        if cell["confidence"] not in {"HIGH", "MEDIUM"}:
            fail(f"{cell_label}.confidence must be HIGH or MEDIUM")
        strength = validate_evidence_rows(
            cell["strengthEvidence"],
            f"{cell_label}.strengthEvidence",
            allowed_source_refs,
        )
        gap = validate_evidence_rows(
            cell["gapEvidence"],
            f"{cell_label}.gapEvidence",
            allowed_source_refs,
        )
        if not strength and not gap:
            fail(f"{cell_label}: at least one evidence row is required")
        if cell["label"] == "EXCELLENT" and not strength:
            fail(f"{cell_label}: EXCELLENT requires strength evidence")
        observations.extend(strength)
        observations.extend(gap)
    if seen_cells != set(EXPECTED_CELL_IDS):
        fail(f"{prefix}: missing text-plan cells {sorted(set(EXPECTED_CELL_IDS) - seen_cells)}")
    return run_id, observations


def validate_scored_aggregate(
    path: Path,
    payloads: list[dict[str, Any]],
    text_plan_sha256: str,
) -> str:
    aggregate = read_object(path, "text-plan aggregate")
    exact_keys(aggregate, AGGREGATE_KEYS, "text-plan aggregate")
    prompt_template_sha256 = file_sha256_identifier(
        JUDGE_PROMPT_PATH,
        "text-plan judge prompt template",
    )
    judgment_schema_sha256 = file_sha256_identifier(
        JUDGMENT_SCHEMA_PATH,
        "text-plan judgment schema",
    )
    rubric_sha256 = canonical_file_object_sha256(RUBRIC_PATH, "text-plan rubric")
    expected_values = {
        "protocol": AGGREGATE_PROTOCOL,
        "status": "SCORED_FORMATIVE",
        "commercialUXProxy": None,
        "officialCommercialUX": False,
        "rerunRequired": False,
        "textPlanSha256": text_plan_sha256,
        "rubricSha256": rubric_sha256,
        "promptTemplateSha256": prompt_template_sha256,
        "judgmentSchemaSha256": judgment_schema_sha256,
        "unstableCells": [],
        "blockers": [],
    }
    for field, expected in expected_values.items():
        if aggregate[field] != expected:
            fail(
                f"text-plan aggregate {field} mismatch: "
                f"expected {expected!r}, got {aggregate[field]!r}"
            )
    text_plan_proxy = finite_number(
        aggregate["textPlanProxy"],
        "text-plan aggregate textPlanProxy",
    )
    text_raw = finite_number(aggregate["textRaw"], "text-plan aggregate textRaw")
    text_raw_spread = finite_number(
        aggregate["textRawSpread"],
        "text-plan aggregate textRawSpread",
    )
    disagreement_penalty = finite_number(
        aggregate["disagreementPenalty"],
        "text-plan aggregate disagreementPenalty",
    )
    if not 0.0 <= text_raw <= 100.0:
        fail("text-plan aggregate textRaw must be within 0..100")
    if not 0.0 <= text_raw_spread <= 100.0:
        fail("text-plan aggregate textRawSpread must be within 0..100")
    expected_penalty = round(min(8.0, text_raw_spread * 0.20), 4)
    if disagreement_penalty != expected_penalty:
        fail(
            "text-plan aggregate disagreementPenalty mismatch: "
            f"expected {expected_penalty}, got {disagreement_penalty}"
        )
    expected_proxy = round(text_raw - disagreement_penalty, 4)
    if text_plan_proxy != expected_proxy:
        fail(
            "text-plan aggregate textPlanProxy mismatch: "
            f"expected {expected_proxy}, got {text_plan_proxy}"
        )
    if not isinstance(aggregate["cellScores"], dict) or not aggregate["cellScores"]:
        fail("text-plan aggregate cellScores must be a nonempty object")
    if not isinstance(aggregate["categoryScores"], dict) or not aggregate["categoryScores"]:
        fail("text-plan aggregate categoryScores must be a nonempty object")

    run_ids = [payload["judgeRunId"] for payload in payloads]
    if aggregate["judgeRunIds"] != run_ids:
        fail(
            "text-plan aggregate judgeRunIds do not exactly match the supplied judgments: "
            f"expected {run_ids!r}, got {aggregate['judgeRunIds']!r}"
        )
    panel_kind = aggregate["panelKind"]
    replacement_for_panel_sha256 = aggregate["replacementForPanelInputSha256"]
    replacement_receipt_sha256 = aggregate["replacementReceiptSha256"]
    if panel_kind == "INITIAL":
        if replacement_for_panel_sha256 is not None:
            fail("INITIAL text-plan aggregate replacementForPanelInputSha256 must be null")
        if replacement_receipt_sha256 is not None:
            fail("INITIAL text-plan aggregate replacementReceiptSha256 must be null")
    elif panel_kind == "REPLACEMENT":
        for value, label in (
            (replacement_for_panel_sha256, "replacementForPanelInputSha256"),
            (replacement_receipt_sha256, "replacementReceiptSha256"),
        ):
            if not isinstance(value, str) or SHA256_PATTERN.fullmatch(value) is None:
                fail(f"REPLACEMENT text-plan aggregate {label} must be a sha256 identifier")
    else:
        fail("text-plan aggregate panelKind must be INITIAL or REPLACEMENT")

    panel_payload = {
        "textPlanSha256": text_plan_sha256,
        "rubricSha256": rubric_sha256,
        "promptTemplateSha256": prompt_template_sha256,
        "judgmentSchemaSha256": judgment_schema_sha256,
        "panelKind": panel_kind,
        "replacementForPanelInputSha256": replacement_for_panel_sha256,
        "judgments": sorted(payloads, key=lambda payload: payload["judgeRunId"]),
    }
    computed_panel_sha256 = sha256_identifier(panel_payload)
    declared_panel_sha256 = aggregate["panelInputSha256"]
    if (
        not isinstance(declared_panel_sha256, str)
        or SHA256_PATTERN.fullmatch(declared_panel_sha256) is None
    ):
        fail("text-plan aggregate panelInputSha256 must be a sha256 identifier")
    if declared_panel_sha256 != computed_panel_sha256:
        fail(
            "text-plan aggregate panelInputSha256 mismatch: "
            f"declared {declared_panel_sha256}, computed {computed_panel_sha256}"
        )
    return declared_panel_sha256


def build_evidence_envelope(
    artifact: dict[str, Any],
    text_plan_sha256: str,
    judge_panel_input_sha256: str,
    judgment_observations: list[list[tuple[str, str]]],
) -> dict[str, Any]:
    unique_observations = sorted(
        {
            observation
            for observations in judgment_observations
            for observation in observations
        }
    )
    if not unique_observations:
        fail("at least one unique evidence observation is required")
    observations = [
        {
            "observationId": f"OBS-{index:04d}",
            "sourceRef": source_ref,
            "observation": observation,
        }
        for index, (source_ref, observation) in enumerate(unique_observations, start=1)
    ]
    verification_input = {
        "schemaVersion": EVIDENCE_INPUT_SCHEMA,
        "protocol": EVIDENCE_INPUT_PROTOCOL,
        "textPlanSha256": text_plan_sha256,
        "judgePanelInputSha256": judge_panel_input_sha256,
        "promptTemplateSha256": file_sha256_identifier(
            VERIFIER_PROMPT_PATH,
            "text-plan evidence verifier prompt template",
        ),
        "verifierSchemaSha256": file_sha256_identifier(
            VERIFIER_SCHEMA_PATH,
            "text-plan evidence verifier schema",
        ),
        "artifact": artifact,
        "observations": observations,
    }
    return {
        "schemaVersion": EVIDENCE_ENVELOPE_SCHEMA,
        "verificationInputSha256": sha256_identifier(verification_input),
        "verificationInput": verification_input,
    }


def prepare(
    judgment_paths: list[Path],
    text_plan_path: Path,
    aggregate_path: Path,
) -> dict[str, Any]:
    if len(judgment_paths) != 3:
        fail("exactly three judgment paths are required")
    artifact, text_plan_sha256, allowed_source_refs = load_text_plan(text_plan_path)
    payloads = [read_object(path, f"judgment {path}") for path in judgment_paths]
    extracted = [
        extract_judgment_observations(
            payload,
            path,
            text_plan_sha256,
            allowed_source_refs,
        )
        for payload, path in zip(payloads, judgment_paths)
    ]
    run_ids = [row[0] for row in extracted]
    if len(set(run_ids)) != 3:
        fail("the three judgments must have distinct judgeRunId values")
    judge_panel_input_sha256 = validate_scored_aggregate(
        aggregate_path,
        payloads,
        text_plan_sha256,
    )
    return build_evidence_envelope(
        artifact,
        text_plan_sha256,
        judge_panel_input_sha256,
        [row[1] for row in extracted],
    )


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Prepare one blinded text-plan evidence verifier input from three judgments."
    )
    parser.add_argument("judgments", nargs=3, type=Path)
    parser.add_argument("--text-plan", required=True, type=Path)
    parser.add_argument("--aggregate", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    envelope = prepare(args.judgments, args.text_plan, args.aggregate)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(envelope, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
