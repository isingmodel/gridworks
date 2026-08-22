#!/usr/bin/env python3
"""Deterministically aggregate exactly three blinded SOL-ULTRA text-plan judgments."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
from pathlib import Path
from typing import Any


RUBRIC_SCHEMA = "gridworks.commercial-ux.rubric.v1"
JUDGMENT_PROTOCOL = "GRIDWORKS-COMMERCIAL-UX-TEXT-PLAN-JUDGMENT-v1"
AGGREGATE_PROTOCOL = "GRIDWORKS-COMMERCIAL-UX-TEXT-PLAN-AGGREGATE-v1"
REPLACEMENT_RECEIPT_SCHEMA = "gridworks.commercial-ux.text-plan-replacement-receipt.v1"
TEXT_PLAN_ENVELOPE_SCHEMA = "gridworks.commercial-ux.text-plan-envelope.v1"
TEXT_PLAN_ARTIFACT_SCHEMA = "gridworks.commercial-ux.text-plan-input.v1"
STORY_PART_SCHEMA = "gridworks.commercial.story-part-output.v1"
CAMPAIGN_ID = "CHEONGRYU_COMMERCIAL_CAMPAIGN_V2"
FROZEN_RUBRIC_SHA256 = "sha256:2e50903e40255c8141513cb36407223a68b18824adc5d61a8af864ba24359b0b"
FROZEN_PROMPT_TEMPLATE_SHA256 = "sha256:d31481546619063fcba5193d7c9043c5bb7e620d258ad3a6bc726fead6ff3be9"
FROZEN_JUDGMENT_SCHEMA_SHA256 = "sha256:69eb5143bc4821b14b90aa479da21620ddfffb20b63070d580184dfe35e69c04"
EXPECTED_LABELS = {
    "EXCELLENT": (4, 100),
    "STRONG": (3, 85),
    "SERVICEABLE": (2, 70),
    "WEAK": (1, 40),
    "BROKEN": (0, 0),
}
SHA256_PATTERN = re.compile(r"^sha256:[0-9a-f]{64}$")
EXPECTED_CHAPTERS = (
    ("FIRST_LIGHT", "첫 불빛", "tutorial"),
    ("SECOND_HEART", "두 번째 심장", "tutorial"),
    ("SECOND_SOURCE", "두 번째 전원", "tutorial"),
    ("NORTH_BANK_PROMISE", "북안의 약속", "main"),
    ("WHOSE_MARGIN", "누구의 여유인가", "main"),
    ("BEFORE_WATER_REACHES", "물이 닿기 전에", "main"),
    ("SHUT_DOWN_TO_KEEP", "꺼야 지킬 수 있다", "main"),
    ("LONGEST_NIGHT", "가장 긴 밤", "main"),
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


def nonempty_string(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value.strip():
        fail(f"{label} must be a nonempty string")
    return value


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def file_sha256(path: Path, label: str) -> str:
    try:
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
    except OSError as exception:
        fail(f"{label} is unreadable: {exception}")
    return f"sha256:{digest}"


def load_judge_contract() -> tuple[str, str]:
    prompt_path = Path(__file__).with_name("text-plan-judge-prompt.template.txt")
    schema_path = Path(__file__).with_name("text-plan-judge.schema.json")
    prompt_sha256 = file_sha256(prompt_path, "judge prompt template")
    schema_sha256 = file_sha256(schema_path, "judge schema")
    if prompt_sha256 != FROZEN_PROMPT_TEMPLATE_SHA256:
        fail(
            "judge prompt template hash drift: "
            f"expected {FROZEN_PROMPT_TEMPLATE_SHA256}, got {prompt_sha256}"
        )
    if schema_sha256 != FROZEN_JUDGMENT_SCHEMA_SHA256:
        fail(
            "judge schema hash drift: "
            f"expected {FROZEN_JUDGMENT_SCHEMA_SHA256}, got {schema_sha256}"
        )
    return prompt_sha256, schema_sha256


def expected_part_metadata(selector: str) -> tuple[str, str | None, str | None, str | None]:
    if selector == "campaign/epilogue":
        return "epilogue", None, None, None
    segments = selector.split("/")
    chapter_id = segments[0]
    if segments[1] == "briefing":
        return "briefing", chapter_id, None, None
    if segments[1] == "window":
        return "window", chapter_id, segments[2], None
    branch = segments[2]
    return "result", chapter_id, None, branch if branch in {"keep", "defer"} else None


def load_text_plan(path: Path) -> tuple[str, set[str]]:
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
    computed_sha = "sha256:" + hashlib.sha256(canonical_json_bytes(artifact)).hexdigest()
    if declared_sha != computed_sha:
        fail(
            "text-plan artifact hash mismatch: "
            f"declared {declared_sha}, computed {computed_sha}"
        )

    chapters = artifact["chapters"]
    if not isinstance(chapters, list) or len(chapters) != len(EXPECTED_CHAPTERS):
        fail("text-plan artifact chapters must contain exactly eight rows")
    allowed_refs = {"context:premise", "context:playerRole"}
    seen_chapters: set[str] = set()
    chapter_fields = {
        "order",
        "chapterId",
        "displayName",
        "phase",
        "learningIntent",
        "crisisIntent",
        "choiceIntent",
    }
    for index, (chapter, expected_chapter) in enumerate(
        zip(chapters, EXPECTED_CHAPTERS),
        start=1,
    ):
        label = f"text-plan artifact chapter {index}"
        if not isinstance(chapter, dict):
            fail(f"{label} must be an object")
        exact_keys(chapter, chapter_fields, label)
        if type(chapter["order"]) is not int or chapter["order"] != index:
            fail("text-plan artifact chapter order must be exactly 1..8")
        chapter_id = nonempty_string(chapter["chapterId"], f"{label}.chapterId")
        expected_chapter_id, expected_display_name, expected_phase = expected_chapter
        if chapter_id != expected_chapter_id:
            fail(
                f"{label}.chapterId mismatch: expected {expected_chapter_id!r}, "
                f"got {chapter_id!r}"
            )
        if chapter_id in seen_chapters:
            fail(f"text-plan artifact duplicate chapterId {chapter_id!r}")
        seen_chapters.add(chapter_id)
        if chapter["displayName"] != expected_display_name:
            fail(
                f"{label}.displayName mismatch: expected {expected_display_name!r}, "
                f"got {chapter['displayName']!r}"
            )
        if chapter["phase"] != expected_phase:
            fail(f"{label}.phase must be {expected_phase}")
        for field in ("learningIntent", "crisisIntent", "choiceIntent"):
            nonempty_string(chapter[field], f"{label}.{field}")
            allowed_refs.add(f"chapter:{chapter_id}:{field}")

    story_parts = artifact["storyParts"]
    if not isinstance(story_parts, list) or len(story_parts) != len(EXPECTED_SELECTORS):
        fail("text-plan artifact storyParts must contain exactly 26 rows")
    seen_selectors: set[str] = set()
    part_fields = {
        "schemaVersion",
        "campaignId",
        "selector",
        "kind",
        "chapterId",
        "windowId",
        "reachable",
        "requiredPromiseBranch",
        "story",
    }
    for index, (part, expected_selector) in enumerate(
        zip(story_parts, EXPECTED_SELECTORS),
        start=1,
    ):
        label = f"text-plan artifact story part {index}"
        if not isinstance(part, dict):
            fail(f"{label} must be an object")
        exact_keys(part, part_fields, label)
        selector = nonempty_string(part["selector"], f"{label}.selector")
        if selector != expected_selector:
            fail(
                f"{label}.selector mismatch: expected {expected_selector!r}, "
                f"got {selector!r}"
            )
        if selector in seen_selectors:
            fail(f"text-plan artifact duplicate selector {selector!r}")
        seen_selectors.add(selector)
        expected_kind, expected_chapter_id, expected_window_id, expected_branch = (
            expected_part_metadata(expected_selector)
        )
        expected_values = {
            "schemaVersion": STORY_PART_SCHEMA,
            "campaignId": CAMPAIGN_ID,
            "kind": expected_kind,
            "chapterId": expected_chapter_id,
            "windowId": expected_window_id,
            "reachable": True,
            "requiredPromiseBranch": expected_branch,
        }
        for field, expected_value in expected_values.items():
            if part[field] != expected_value or (
                field == "reachable" and part[field] is not True
            ):
                fail(
                    f"{label}.{field} mismatch: expected {expected_value!r}, "
                    f"got {part[field]!r}"
                )
        story = part["story"]
        if not isinstance(story, dict):
            fail(f"{label}.story must be an object")
        exact_keys(story, {"speaker", "title", "body"}, f"{label}.story")
        for field in ("speaker", "title", "body"):
            nonempty_string(story[field], f"{label}.story.{field}")
        allowed_refs.add(selector)
    return declared_sha, allowed_refs


def load_text_rubric(
    path: Path,
) -> tuple[dict[str, tuple[int, int]], list[dict[str, Any]], dict[str, Any], str]:
    rubric = read_object(path, "rubric")
    rubric_sha256 = "sha256:" + hashlib.sha256(canonical_json_bytes(rubric)).hexdigest()
    if rubric_sha256 != FROZEN_RUBRIC_SHA256:
        fail(
            "rubric canonical JSON hash drift: "
            f"expected {FROZEN_RUBRIC_SHA256}, got {rubric_sha256}"
        )
    if rubric.get("schemaVersion") != RUBRIC_SCHEMA:
        fail(f"rubric schemaVersion must be {RUBRIC_SCHEMA}")
    judge = rubric.get("judge")
    if judge != {
        "model": "gpt-5.6-sol",
        "reasoningEffort": "ultra",
        "slot": "SOL-ULTRA",
    }:
        fail("rubric judge identity drift")

    labels = rubric.get("labels")
    if not isinstance(labels, list) or len(labels) != len(EXPECTED_LABELS):
        fail("rubric labels must contain the five canonical labels")
    label_data: dict[str, tuple[int, int]] = {}
    for row in labels:
        if not isinstance(row, dict):
            fail("rubric label rows must be objects")
        label_id = row.get("id")
        actual = (row.get("ordinal"), row.get("score"))
        if label_id not in EXPECTED_LABELS or actual != EXPECTED_LABELS[label_id]:
            fail(f"rubric label drift: {label_id!r} -> {actual!r}")
        label_data[label_id] = actual
    if label_data != EXPECTED_LABELS:
        fail("rubric labels are missing or duplicated")

    text_plan = rubric.get("textPlan")
    if not isinstance(text_plan, dict):
        fail("rubric textPlan must be an object")
    if text_plan.get("metric") != "TextPlanProxy" or text_plan.get("officialCommercialUX") is not False:
        fail("rubric must keep TextPlanProxy non-official")
    if text_plan.get("categoryWeightTotal") != 60:
        fail("text-plan category weight total must be 60")
    aggregation = text_plan.get("aggregation")
    expected_aggregation = {
        "judgeCount": 3,
        "labelReduction": "NUMERIC_MEDIAN",
        "spreadReduction": "MAX_MINUS_MIN",
        "spreadPenaltyMultiplier": 0.2,
        "spreadPenaltyMaximum": 8.0,
        "instabilityOrdinalRangeMinimum": 2,
    }
    if aggregation != expected_aggregation:
        fail("text-plan aggregation constants drifted from the frozen protocol")

    categories = text_plan.get("categories")
    if not isinstance(categories, list) or not categories:
        fail("rubric text-plan categories must be a nonempty list")
    category_ids: set[str] = set()
    cell_ids: set[str] = set()
    category_weight_sum = 0
    for category in categories:
        if not isinstance(category, dict):
            fail("rubric text-plan category rows must be objects")
        category_id = nonempty_string(category.get("id"), "rubric category id")
        if category_id in category_ids:
            fail(f"duplicate rubric category: {category_id}")
        category_ids.add(category_id)
        weight = category.get("weight")
        if type(weight) is not int or weight <= 0:
            fail(f"{category_id}: category weight must be a positive integer")
        category_weight_sum += weight
        cells = category.get("cells")
        if not isinstance(cells, list) or not cells:
            fail(f"{category_id}: cells must be a nonempty list")
        cell_weight_sum = 0
        for cell in cells:
            if not isinstance(cell, dict):
                fail(f"{category_id}: cell rows must be objects")
            cell_id = nonempty_string(cell.get("id"), f"{category_id} cell id")
            if cell_id in cell_ids:
                fail(f"duplicate rubric text-plan cell: {cell_id}")
            cell_ids.add(cell_id)
            if cell.get("laneOwnership") != ["TEXT-PLAN"]:
                fail(f"{cell_id}: text-plan lane ownership drift")
            cell_weight = cell.get("weight")
            if type(cell_weight) is not int or cell_weight <= 0:
                fail(f"{cell_id}: cell weight must be a positive integer")
            cell_weight_sum += cell_weight
        if cell_weight_sum != 100:
            fail(f"{category_id}: cell weights total {cell_weight_sum}, expected 100")
    if category_weight_sum != text_plan["categoryWeightTotal"]:
        fail(
            f"text-plan category weights total {category_weight_sum}, "
            f"expected {text_plan['categoryWeightTotal']}"
        )
    if len(cell_ids) != 20:
        fail(f"text-plan rubric must contain exactly 20 cells, got {len(cell_ids)}")
    return label_data, categories, aggregation, rubric_sha256


def validate_evidence(value: Any, label: str, allowed_source_refs: set[str]) -> None:
    if not isinstance(value, list) or len(value) > 4:
        fail(f"{label} must be an array with at most four rows")
    seen: set[tuple[str, str]] = set()
    for index, row in enumerate(value):
        row_label = f"{label}[{index}]"
        if not isinstance(row, dict):
            fail(f"{row_label} must be an object")
        exact_keys(row, {"sourceRef", "observation"}, row_label)
        source = nonempty_string(row["sourceRef"], f"{row_label}.sourceRef")
        observation = nonempty_string(row["observation"], f"{row_label}.observation")
        if source not in allowed_source_refs:
            fail(f"{row_label}.sourceRef does not exist in the text-plan artifact: {source!r}")
        key = (source, observation)
        if key in seen:
            fail(f"{label} contains duplicate evidence")
        seen.add(key)


def validate_judgment(
    payload: dict[str, Any],
    path: Path,
    expected_cell_ids: set[str],
    labels: set[str],
    allowed_source_refs: set[str],
    prompt_template_sha256: str,
    judgment_schema_sha256: str,
) -> dict[str, dict[str, Any]]:
    prefix = str(path)
    exact_keys(
        payload,
        {
            "protocol",
            "judgeRunId",
            "judgeSlot",
            "model",
            "reasoningEffort",
            "textPlanSha256",
            "promptTemplateSha256",
            "judgmentSchemaSha256",
            "cells",
        },
        prefix,
    )
    expected_identity = {
        "protocol": JUDGMENT_PROTOCOL,
        "judgeSlot": "SOL-ULTRA",
        "model": "gpt-5.6-sol",
        "reasoningEffort": "ultra",
        "promptTemplateSha256": prompt_template_sha256,
        "judgmentSchemaSha256": judgment_schema_sha256,
    }
    for field, expected in expected_identity.items():
        if payload[field] != expected:
            fail(f"{prefix}: {field} must be {expected!r}")
    nonempty_string(payload["judgeRunId"], f"{prefix}.judgeRunId")
    sha256 = payload["textPlanSha256"]
    if not isinstance(sha256, str) or SHA256_PATTERN.fullmatch(sha256) is None:
        fail(f"{prefix}.textPlanSha256 must be a lowercase sha256 identifier")
    cells = payload["cells"]
    if not isinstance(cells, list) or len(cells) != len(expected_cell_ids):
        fail(f"{prefix}: cells must contain exactly {len(expected_cell_ids)} rows")
    by_id: dict[str, dict[str, Any]] = {}
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
        if cell_id not in expected_cell_ids:
            fail(f"{cell_label}: unknown cellId {cell_id!r}")
        if cell_id in by_id:
            fail(f"{prefix}: duplicate cellId {cell_id}")
        if cell["label"] not in labels:
            fail(f"{cell_id}: invalid label {cell['label']!r}")
        if cell["confidence"] not in {"HIGH", "MEDIUM"}:
            fail(f"{cell_id}: confidence must be HIGH or MEDIUM")
        validate_evidence(
            cell["strengthEvidence"],
            f"{cell_id}.strengthEvidence",
            allowed_source_refs,
        )
        validate_evidence(
            cell["gapEvidence"],
            f"{cell_id}.gapEvidence",
            allowed_source_refs,
        )
        if not cell["strengthEvidence"] and not cell["gapEvidence"]:
            fail(f"{cell_id}: at least one grounded evidence row is required")
        if cell["label"] == "EXCELLENT" and not cell["strengthEvidence"]:
            fail(f"{cell_id}: EXCELLENT requires concrete strength evidence")
        by_id[cell_id] = cell
    if set(by_id) != expected_cell_ids:
        fail(f"{prefix}: missing text-plan cells {sorted(expected_cell_ids - set(by_id))}")
    return by_id


def rounded(value: float) -> float:
    return round(value, 4)


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
    "replacementReceiptPath",
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

REPLACEMENT_RECEIPT_KEYS = {
    "schemaVersion",
    "replacementReceiptPath",
    "initialAggregatePath",
    "initialPanelInputSha256",
    "replacementPanelInputSha256",
    "replacementJudgeRunIds",
    "textPlanSha256",
    "rubricSha256",
    "promptTemplateSha256",
    "judgmentSchemaSha256",
}
REPLACEMENT_RECEIPT_FILENAME_PREFIX = ".gridworks-commercial-ux-replacement-"
REPLACEMENT_RECEIPT_FILENAME_SUFFIX = ".receipt.json"


def canonical_path(path: Path, label: str) -> Path:
    try:
        return path.resolve(strict=False)
    except (OSError, RuntimeError) as exception:
        fail(f"{label} cannot be resolved: {exception}")


def replacement_receipt_path_for_output(
    output_path: Path,
    initial_panel_input_sha256: str,
) -> Path:
    if SHA256_PATTERN.fullmatch(initial_panel_input_sha256) is None:
        fail("initial panelInputSha256 must be a lowercase sha256 identifier")
    digest = initial_panel_input_sha256.removeprefix("sha256:")
    output_parent = canonical_path(output_path, "aggregate output path").parent
    return output_parent / (
        REPLACEMENT_RECEIPT_FILENAME_PREFIX
        + digest
        + REPLACEMENT_RECEIPT_FILENAME_SUFFIX
    )


def validate_embedded_receipt_path(
    value: Any,
    initial_panel_input_sha256: str,
    label: str,
) -> Path:
    if not isinstance(value, str) or not value:
        fail(f"{label} must be a nonempty absolute path string")
    path = Path(value)
    if not path.is_absolute():
        fail(f"{label} must be an absolute path")
    resolved = canonical_path(path, label)
    if path != resolved:
        fail(f"{label} must be canonical: expected {resolved}, got {path}")
    digest = initial_panel_input_sha256.removeprefix("sha256:")
    expected_name = (
        REPLACEMENT_RECEIPT_FILENAME_PREFIX
        + digest
        + REPLACEMENT_RECEIPT_FILENAME_SUFFIX
    )
    if path.name != expected_name:
        fail(
            f"{label} is not content-addressed by the initial panelInputSha256: "
            f"expected filename {expected_name!r}, got {path.name!r}"
        )
    return path


def validate_replacement_initial(
    path: Path,
    text_plan_sha256: str,
    rubric_sha256: str,
    prompt_template_sha256: str,
    judgment_schema_sha256: str,
    replacement_run_ids: list[str],
) -> tuple[str, Path, Path]:
    try:
        resolved_path = path.resolve(strict=True)
    except OSError as exception:
        fail(f"initial aggregate path cannot be resolved: {exception}")
    initial = read_object(resolved_path, "initial aggregate")
    exact_keys(initial, AGGREGATE_KEYS, "initial aggregate")
    expected = {
        "protocol": AGGREGATE_PROTOCOL,
        "status": "RERUN_REQUIRED_JUDGE_INSTABILITY",
        "textPlanProxy": None,
        "commercialUXProxy": None,
        "officialCommercialUX": False,
        "panelKind": "INITIAL",
        "rerunRequired": True,
        "replacementForPanelInputSha256": None,
        "replacementReceiptSha256": None,
        "textPlanSha256": text_plan_sha256,
        "rubricSha256": rubric_sha256,
        "promptTemplateSha256": prompt_template_sha256,
        "judgmentSchemaSha256": judgment_schema_sha256,
    }
    for field, expected_value in expected.items():
        if initial[field] != expected_value:
            fail(
                f"initial aggregate {field} mismatch: expected {expected_value!r}, "
                f"got {initial[field]!r}"
            )
    panel_sha256 = initial["panelInputSha256"]
    if not isinstance(panel_sha256, str) or SHA256_PATTERN.fullmatch(panel_sha256) is None:
        fail("initial aggregate panelInputSha256 must be a lowercase sha256 identifier")
    receipt_path = validate_embedded_receipt_path(
        initial["replacementReceiptPath"],
        panel_sha256,
        "initial aggregate replacementReceiptPath",
    )
    initial_run_ids = initial["judgeRunIds"]
    if (
        not isinstance(initial_run_ids, list)
        or len(initial_run_ids) != 3
        or len(set(initial_run_ids)) != 3
        or any(not isinstance(run_id, str) or not run_id.strip() for run_id in initial_run_ids)
    ):
        fail("initial aggregate judgeRunIds must contain three distinct nonempty strings")
    overlap = sorted(set(initial_run_ids) & set(replacement_run_ids))
    if overlap:
        fail(f"replacement judgeRunIds must be fresh and disjoint; overlap={overlap}")
    unstable_cells = initial["unstableCells"]
    if not isinstance(unstable_cells, list) or not unstable_cells:
        fail("initial aggregate must preserve at least one unstable cell")
    return panel_sha256, resolved_path, receipt_path


def validate_replacement_output_path(
    output_path: Path,
    initial_aggregate_path: Path,
    receipt_path: Path,
) -> None:
    resolved_output_path = canonical_path(output_path, "replacement output path")
    if resolved_output_path in {initial_aggregate_path, receipt_path}:
        fail(
            "replacement output must not overwrite the initial aggregate or its receipt: "
            f"{resolved_output_path}"
        )
    try:
        output_path.lstat()
    except FileNotFoundError:
        return
    except OSError as exception:
        fail(f"replacement output path cannot be inspected: {exception}")

    try:
        aliases_initial = os.path.samefile(output_path, initial_aggregate_path)
    except OSError:
        aliases_initial = False
    if aliases_initial:
        fail(
            "replacement output aliases the initial aggregate inode and would modify it: "
            f"{output_path}"
        )
    fail(
        "replacement output path must not already exist; a fresh path is required for "
        f"exclusive output creation: {output_path}"
    )


def replacement_receipt_bytes(
    receipt_path: Path,
    initial_aggregate_path: Path,
    initial_panel_input_sha256: str,
    replacement_panel_input_sha256: str,
    replacement_run_ids: list[str],
    text_plan_sha256: str,
    rubric_sha256: str,
    prompt_template_sha256: str,
    judgment_schema_sha256: str,
) -> tuple[bytes, str]:
    receipt = {
        "schemaVersion": REPLACEMENT_RECEIPT_SCHEMA,
        "replacementReceiptPath": str(receipt_path),
        "initialAggregatePath": str(initial_aggregate_path),
        "initialPanelInputSha256": initial_panel_input_sha256,
        "replacementPanelInputSha256": replacement_panel_input_sha256,
        "replacementJudgeRunIds": sorted(replacement_run_ids),
        "textPlanSha256": text_plan_sha256,
        "rubricSha256": rubric_sha256,
        "promptTemplateSha256": prompt_template_sha256,
        "judgmentSchemaSha256": judgment_schema_sha256,
    }
    receipt_bytes = canonical_json_bytes(receipt) + b"\n"
    receipt_sha256 = "sha256:" + hashlib.sha256(receipt_bytes).hexdigest()
    return receipt_bytes, receipt_sha256


def create_replacement_receipt(
    path: Path,
    receipt_bytes: bytes,
    receipt_sha256: str,
) -> None:
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    try:
        descriptor = os.open(path, flags, 0o600)
    except FileExistsError:
        fail(
            "initial aggregate replacement was already consumed; "
            f"receipt exists: {path}"
        )
    except OSError as exception:
        fail(f"could not atomically claim replacement receipt {path}: {exception}")
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(receipt_bytes)
            stream.flush()
            os.fsync(stream.fileno())
    except OSError as exception:
        fail(
            "replacement receipt claim was created but could not be completed; "
            f"it remains consumed at {path}: {exception}"
        )
    try:
        persisted = path.read_bytes()
    except OSError as exception:
        fail(f"replacement receipt could not be verified at {path}: {exception}")
    if persisted != receipt_bytes:
        fail(f"replacement receipt bytes changed before verification: {path}")
    if "sha256:" + hashlib.sha256(persisted).hexdigest() != receipt_sha256:
        fail(f"replacement receipt hash verification failed: {path}")


def load_panel_inputs(
    judgment_paths: list[Path],
    rubric_path: Path,
    text_plan_path: Path,
) -> dict[str, Any]:
    if len(judgment_paths) != 3:
        fail("exactly three judgment paths are required")
    label_data, categories, aggregation_config, rubric_sha256 = load_text_rubric(rubric_path)
    prompt_template_sha256, judgment_schema_sha256 = load_judge_contract()
    text_plan_sha256, allowed_source_refs = load_text_plan(text_plan_path)
    expected_cell_ids = {
        cell["id"]
        for category in categories
        for cell in category["cells"]
    }
    payloads = [read_object(path, f"judgment {path}") for path in judgment_paths]
    judgments = [
        validate_judgment(
            payload,
            path,
            expected_cell_ids,
            set(label_data),
            allowed_source_refs,
            prompt_template_sha256,
            judgment_schema_sha256,
        )
        for payload, path in zip(payloads, judgment_paths)
    ]
    run_ids = [payload["judgeRunId"] for payload in payloads]
    if len(set(run_ids)) != 3:
        fail("the three judgments must have distinct judgeRunId values")
    artifact_ids = {payload["textPlanSha256"] for payload in payloads}
    if len(artifact_ids) != 1:
        fail("the three judgments must evaluate the same textPlanSha256")
    if artifact_ids != {text_plan_sha256}:
        fail(
            "judgment textPlanSha256 does not match the supplied text-plan envelope: "
            f"expected {text_plan_sha256}, got {sorted(artifact_ids)}"
        )
    return {
        "labelData": label_data,
        "categories": categories,
        "aggregationConfig": aggregation_config,
        "rubricSha256": rubric_sha256,
        "promptTemplateSha256": prompt_template_sha256,
        "judgmentSchemaSha256": judgment_schema_sha256,
        "textPlanSha256": text_plan_sha256,
        "payloads": payloads,
        "judgments": judgments,
        "runIds": run_ids,
    }


def panel_input_sha256(
    panel: dict[str, Any],
    panel_kind: str,
    replacement_for_panel_sha256: str | None,
) -> str:
    panel_payload = {
        "textPlanSha256": panel["textPlanSha256"],
        "rubricSha256": panel["rubricSha256"],
        "promptTemplateSha256": panel["promptTemplateSha256"],
        "judgmentSchemaSha256": panel["judgmentSchemaSha256"],
        "panelKind": panel_kind,
        "replacementForPanelInputSha256": replacement_for_panel_sha256,
        "judgments": sorted(
            panel["payloads"],
            key=lambda payload: payload["judgeRunId"],
        ),
    }
    return "sha256:" + hashlib.sha256(canonical_json_bytes(panel_payload)).hexdigest()


def compute_aggregate_result(
    panel: dict[str, Any],
    panel_kind: str,
    replacement_for_panel_sha256: str | None,
    replacement_receipt_path: Path,
    replacement_receipt_sha256: str | None,
) -> dict[str, Any]:
    if panel_kind not in {"INITIAL", "REPLACEMENT"}:
        fail(f"panelKind must be INITIAL or REPLACEMENT, got {panel_kind!r}")
    if panel_kind == "INITIAL":
        if replacement_for_panel_sha256 is not None:
            fail("INITIAL panel replacementForPanelInputSha256 must be null")
        if replacement_receipt_sha256 is not None:
            fail("INITIAL panel replacementReceiptSha256 must be null")
    else:
        for value, label in (
            (replacement_for_panel_sha256, "replacementForPanelInputSha256"),
            (replacement_receipt_sha256, "replacementReceiptSha256"),
        ):
            if not isinstance(value, str) or SHA256_PATTERN.fullmatch(value) is None:
                fail(f"REPLACEMENT panel {label} must be a lowercase sha256 identifier")

    label_data = panel["labelData"]
    categories = panel["categories"]
    aggregation_config = panel["aggregationConfig"]
    judgments = panel["judgments"]
    computed_panel_input_sha256 = panel_input_sha256(
        panel,
        panel_kind,
        replacement_for_panel_sha256,
    )

    score_for = {label: data[1] for label, data in label_data.items()}
    ordinal_for = {label: data[0] for label, data in label_data.items()}
    label_for_score = {score: label for label, score in score_for.items()}
    instability_minimum = aggregation_config["instabilityOrdinalRangeMinimum"]
    cell_results: dict[str, dict[str, Any]] = {}
    unstable_cells: list[str] = []
    for category in categories:
        for cell in category["cells"]:
            cell_id = cell["id"]
            cell_labels = [judgment[cell_id]["label"] for judgment in judgments]
            scores = [score_for[label] for label in cell_labels]
            ordinals = [ordinal_for[label] for label in cell_labels]
            median_score = sorted(scores)[1]
            ordinal_range = max(ordinals) - min(ordinals)
            unstable = ordinal_range >= instability_minimum
            if unstable:
                unstable_cells.append(cell_id)
            cell_results[cell_id] = {
                "categoryId": category["id"],
                "weightWithinCategory": cell["weight"],
                "labels": cell_labels,
                "medianLabel": label_for_score[median_score],
                "medianScore": median_score,
                "spread": max(scores) - min(scores),
                "ordinalRange": ordinal_range,
                "unstable": unstable,
            }

    category_results: dict[str, dict[str, Any]] = {}
    for category in categories:
        category_id = category["id"]
        category_score = sum(
            cell["weight"] * cell_results[cell["id"]]["medianScore"]
            for cell in category["cells"]
        ) / 100
        category_spread = sum(
            cell["weight"] * cell_results[cell["id"]]["spread"]
            for cell in category["cells"]
        ) / 100
        category_results[category_id] = {
            "weight": category["weight"],
            "score": rounded(category_score),
            "spread": rounded(category_spread),
        }

    category_weight_total = sum(category["weight"] for category in categories)
    text_raw = sum(
        category["weight"] * category_results[category["id"]]["score"]
        for category in categories
    ) / category_weight_total
    text_raw_spread = sum(
        category["weight"] * category_results[category["id"]]["spread"]
        for category in categories
    ) / category_weight_total
    penalty = min(
        aggregation_config["spreadPenaltyMaximum"],
        text_raw_spread * aggregation_config["spreadPenaltyMultiplier"],
    )
    blockers = (
        ["text-plan-panel:ordinal-range-at-least-two"]
        if unstable_cells
        else []
    )
    if unstable_cells and panel_kind == "REPLACEMENT":
        status = "BLOCKED_JUDGE_INSTABILITY"
    elif unstable_cells:
        status = "RERUN_REQUIRED_JUDGE_INSTABILITY"
    else:
        status = "SCORED_FORMATIVE"
    text_plan_proxy = None if unstable_cells else rounded(text_raw - penalty)
    return {
        "protocol": AGGREGATE_PROTOCOL,
        "status": status,
        "textPlanProxy": text_plan_proxy,
        "commercialUXProxy": None,
        "officialCommercialUX": False,
        "panelKind": panel_kind,
        "rerunRequired": bool(unstable_cells and panel_kind == "INITIAL"),
        "panelInputSha256": computed_panel_input_sha256,
        "replacementForPanelInputSha256": replacement_for_panel_sha256,
        "replacementReceiptPath": str(replacement_receipt_path),
        "replacementReceiptSha256": replacement_receipt_sha256,
        "rubricSha256": panel["rubricSha256"],
        "promptTemplateSha256": panel["promptTemplateSha256"],
        "judgmentSchemaSha256": panel["judgmentSchemaSha256"],
        "textRaw": rounded(text_raw),
        "textRawSpread": rounded(text_raw_spread),
        "disagreementPenalty": rounded(penalty),
        "textPlanSha256": panel["textPlanSha256"],
        "judgeRunIds": panel["runIds"],
        "cellScores": cell_results,
        "categoryScores": category_results,
        "unstableCells": unstable_cells,
        "blockers": blockers,
    }


def canonical_absolute_path_value(value: Any, label: str) -> Path:
    if not isinstance(value, str) or not value:
        fail(f"{label} must be a nonempty absolute path string")
    path = Path(value)
    if not path.is_absolute():
        fail(f"{label} must be an absolute path")
    resolved = canonical_path(path, label)
    if path != resolved:
        fail(f"{label} must be canonical: expected {resolved}, got {path}")
    return path


def verify_replacement_receipt(
    receipt_path: Path,
    declared_receipt_sha256: str,
    replacement_panel_input_sha256: str,
    panel: dict[str, Any],
    initial_panel_input_sha256: str,
) -> str:
    if (
        not isinstance(declared_receipt_sha256, str)
        or SHA256_PATTERN.fullmatch(declared_receipt_sha256) is None
    ):
        fail("replacement aggregate replacementReceiptSha256 must be a sha256 identifier")
    try:
        persisted = receipt_path.read_bytes()
    except OSError as exception:
        fail(f"replacement receipt is unreadable at {receipt_path}: {exception}")
    persisted_sha256 = "sha256:" + hashlib.sha256(persisted).hexdigest()
    if persisted_sha256 != declared_receipt_sha256:
        fail(
            "replacement receipt hash mismatch: "
            f"declared {declared_receipt_sha256}, computed {persisted_sha256}"
        )
    try:
        receipt = json.loads(persisted.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exception:
        fail(f"replacement receipt is not canonical UTF-8 JSON: {exception}")
    if not isinstance(receipt, dict):
        fail("replacement receipt must be a JSON object")
    exact_keys(receipt, REPLACEMENT_RECEIPT_KEYS, "replacement receipt")
    initial_aggregate_path = canonical_absolute_path_value(
        receipt["initialAggregatePath"],
        "replacement receipt initialAggregatePath",
    )
    expected_bytes, expected_sha256 = replacement_receipt_bytes(
        receipt_path,
        initial_aggregate_path,
        initial_panel_input_sha256,
        replacement_panel_input_sha256,
        panel["runIds"],
        panel["textPlanSha256"],
        panel["rubricSha256"],
        panel["promptTemplateSha256"],
        panel["judgmentSchemaSha256"],
    )
    if persisted != expected_bytes or persisted_sha256 != expected_sha256:
        fail("replacement receipt content does not match the scored panel provenance")
    return persisted_sha256


def verify_existing_scored_aggregate(
    aggregate_path: Path,
    judgment_paths: list[Path],
    rubric_path: Path,
    text_plan_path: Path,
) -> dict[str, Any]:
    """Read-only exact verification using the production aggregation calculation."""
    existing = read_object(aggregate_path, "existing scored aggregate")
    exact_keys(existing, AGGREGATE_KEYS, "existing scored aggregate")
    panel = load_panel_inputs(judgment_paths, rubric_path, text_plan_path)
    panel_kind = existing["panelKind"]
    replacement_for_panel_sha256 = existing["replacementForPanelInputSha256"]
    if panel_kind == "INITIAL":
        if replacement_for_panel_sha256 is not None:
            fail("INITIAL scored aggregate replacementForPanelInputSha256 must be null")
        expected_panel_input_sha256 = panel_input_sha256(panel, "INITIAL", None)
        receipt_path = validate_embedded_receipt_path(
            existing["replacementReceiptPath"],
            expected_panel_input_sha256,
            "existing scored aggregate replacementReceiptPath",
        )
        if existing["replacementReceiptSha256"] is not None:
            fail("INITIAL scored aggregate replacementReceiptSha256 must be null")
        replacement_receipt_sha256 = None
    elif panel_kind == "REPLACEMENT":
        if (
            not isinstance(replacement_for_panel_sha256, str)
            or SHA256_PATTERN.fullmatch(replacement_for_panel_sha256) is None
        ):
            fail(
                "REPLACEMENT scored aggregate replacementForPanelInputSha256 must be "
                "a sha256 identifier"
            )
        expected_panel_input_sha256 = panel_input_sha256(
            panel,
            "REPLACEMENT",
            replacement_for_panel_sha256,
        )
        receipt_path = validate_embedded_receipt_path(
            existing["replacementReceiptPath"],
            replacement_for_panel_sha256,
            "existing scored aggregate replacementReceiptPath",
        )
        replacement_receipt_sha256 = verify_replacement_receipt(
            receipt_path,
            existing["replacementReceiptSha256"],
            expected_panel_input_sha256,
            panel,
            replacement_for_panel_sha256,
        )
    else:
        fail("existing scored aggregate panelKind must be INITIAL or REPLACEMENT")

    expected = compute_aggregate_result(
        panel,
        panel_kind,
        replacement_for_panel_sha256,
        receipt_path,
        replacement_receipt_sha256,
    )
    if expected["status"] != "SCORED_FORMATIVE":
        fail(
            "supplied judgments do not produce a scored aggregate: "
            f"recomputed status is {expected['status']}"
        )
    if existing != expected:
        mismatched_fields = sorted(
            field for field in AGGREGATE_KEYS if existing[field] != expected[field]
        )
        fail(
            "existing scored aggregate does not exactly match deterministic "
            f"recomputation; mismatched fields={mismatched_fields}"
        )
    return existing


def aggregate(
    judgment_paths: list[Path],
    rubric_path: Path,
    text_plan_path: Path,
    replacement_for: Path | None,
    output_path: Path,
) -> dict[str, Any]:
    panel = load_panel_inputs(judgment_paths, rubric_path, text_plan_path)
    replacement_for_panel_sha256 = None
    replacement_initial_path = None
    receipt_path = None
    if replacement_for is not None:
        (
            replacement_for_panel_sha256,
            replacement_initial_path,
            receipt_path,
        ) = validate_replacement_initial(
            replacement_for,
            panel["textPlanSha256"],
            panel["rubricSha256"],
            panel["promptTemplateSha256"],
            panel["judgmentSchemaSha256"],
            panel["runIds"],
        )
    panel_kind = "REPLACEMENT" if replacement_for is not None else "INITIAL"
    computed_panel_input_sha256 = panel_input_sha256(
        panel,
        panel_kind,
        replacement_for_panel_sha256,
    )
    if receipt_path is None:
        receipt_path = replacement_receipt_path_for_output(
            output_path,
            computed_panel_input_sha256,
        )
        if canonical_path(output_path, "initial aggregate output path") == receipt_path:
            fail("initial aggregate output path collides with its replacement receipt path")
        receipt_sha256 = None
        receipt_bytes = None
    else:
        assert replacement_initial_path is not None
        assert replacement_for_panel_sha256 is not None
        validate_replacement_output_path(
            output_path,
            replacement_initial_path,
            receipt_path,
        )
        receipt_bytes, receipt_sha256 = replacement_receipt_bytes(
            receipt_path,
            replacement_initial_path,
            replacement_for_panel_sha256,
            computed_panel_input_sha256,
            panel["runIds"],
            panel["textPlanSha256"],
            panel["rubricSha256"],
            panel["promptTemplateSha256"],
            panel["judgmentSchemaSha256"],
        )

    result = compute_aggregate_result(
        panel,
        panel_kind,
        replacement_for_panel_sha256,
        receipt_path,
        receipt_sha256,
    )
    if receipt_bytes is not None:
        create_replacement_receipt(receipt_path, receipt_bytes, receipt_sha256)
    return result


def write_output(path: Path, output: str, replacement_receipt_claimed: bool) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    try:
        descriptor = os.open(path, flags, 0o600)
    except FileExistsError:
        if replacement_receipt_claimed:
            fail(
                "aggregate output path already exists after the replacement receipt claim; "
                f"the replacement remains consumed: {path}"
            )
        fail(f"aggregate output path already exists and will not be overwritten: {path}")
    except OSError as exception:
        if replacement_receipt_claimed:
            fail(
                "replacement receipt was claimed but output could not be exclusively "
                f"created; the replacement remains consumed at {path}: {exception}"
            )
        fail(f"aggregate output could not be exclusively created at {path}: {exception}")
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="") as stream:
            stream.write(output)
            stream.flush()
            os.fsync(stream.fileno())
    except OSError as exception:
        fail(
            "replacement output was created but could not be completed; "
            f"the replacement remains consumed at {path}: {exception}"
        )


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Aggregate exactly three strict commercial UX text-plan judgments."
    )
    parser.add_argument("judgments", nargs=3, type=Path)
    parser.add_argument(
        "--text-plan",
        type=Path,
        required=True,
        help="Hash-pinned envelope emitted by build-text-plan-input.py",
    )
    parser.add_argument(
        "--rubric",
        type=Path,
        default=Path(__file__).with_name("rubric.json"),
    )
    parser.add_argument(
        "--replacement-for",
        type=Path,
        help="Initial RERUN_REQUIRED aggregate replaced by this fresh panel",
    )
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = aggregate(
        args.judgments,
        args.rubric,
        args.text_plan,
        args.replacement_for,
        args.output,
    )
    output = json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    write_output(
        args.output,
        output,
        replacement_receipt_claimed=args.replacement_for is not None,
    )
    print(output, end="")


if __name__ == "__main__":
    main()
