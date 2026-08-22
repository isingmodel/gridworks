#!/usr/bin/env python3
"""Self-tests for the frozen commercial UX text-plan evaluation infrastructure."""

from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
import subprocess
import sys
import tempfile
from pathlib import Path
from types import ModuleType
from typing import Any


ROOT = Path(__file__).resolve().parent
REPOSITORY_ROOT = ROOT.parents[1]
RUBRIC_PATH = ROOT / "rubric.json"
SCHEMA_PATH = ROOT / "text-plan-judge.schema.json"
PROMPT_PATH = ROOT / "text-plan-judge-prompt.template.txt"
AGGREGATOR_PATH = ROOT / "aggregate-text-plan.py"
BUILDER_PATH = ROOT / "build-text-plan-input.py"
CONTEXT_PATH = ROOT / "text-plan-context.json"
CAMPAIGN_PATH = REPOSITORY_ROOT / "data/release-campaign-v2.json"
COMMERCIAL_CHECKS = REPOSITORY_ROOT / "tools/Gridworks.CommercialChecks/Gridworks.CommercialChecks.csproj"
PROMPT_TEMPLATE_SHA256 = "sha256:" + hashlib.sha256(PROMPT_PATH.read_bytes()).hexdigest()
JUDGMENT_SCHEMA_SHA256 = "sha256:" + hashlib.sha256(SCHEMA_PATH.read_bytes()).hexdigest()
EXPECTED_SELECTORS = [
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
]


def load_module(path: Path, name: str) -> ModuleType:
    specification = importlib.util.spec_from_file_location(name, path)
    assert specification is not None and specification.loader is not None
    module = importlib.util.module_from_spec(specification)
    sys.modules[name] = module
    specification.loader.exec_module(module)
    return module


def load_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    assert isinstance(value, dict)
    return value


_REAL_STORY_MANIFEST: dict[str, Any] | None = None


def real_story_manifest() -> dict[str, Any]:
    global _REAL_STORY_MANIFEST
    if _REAL_STORY_MANIFEST is None:
        subprocess.run(
            ["dotnet", "build", str(COMMERCIAL_CHECKS), "-c", "Release", "--nologo"],
            cwd=REPOSITORY_ROOT,
            check=True,
            capture_output=True,
            text=True,
        )
        completed = subprocess.run(
            [
                "dotnet",
                "run",
                "--project",
                str(COMMERCIAL_CHECKS),
                "-c",
                "Release",
                "--no-build",
                "--",
                "--story-manifest",
            ],
            cwd=REPOSITORY_ROOT,
            check=True,
            capture_output=True,
            text=True,
        )
        value = json.loads(completed.stdout)
        assert isinstance(value, dict)
        _REAL_STORY_MANIFEST = value
    return copy.deepcopy(_REAL_STORY_MANIFEST)


def all_text_cell_ids(rubric: dict[str, Any]) -> list[str]:
    return [
        cell["id"]
        for category in rubric["textPlan"]["categories"]
        for cell in category["cells"]
    ]


def test_canonical_rubric_and_schema() -> None:
    rubric = load_json(RUBRIC_PATH)
    labels = {
        row["id"]: (row["ordinal"], row["score"])
        for row in rubric["labels"]
    }
    assert labels == {
        "EXCELLENT": (4, 100),
        "STRONG": (3, 85),
        "SERVICEABLE": (2, 70),
        "WEAK": (1, 40),
        "BROKEN": (0, 0),
    }

    expected_native = {
        "journey": (12, 85, {
            "J1": (20, ("COLD-JOURNEY",)),
            "J2": (30, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "J3": (20, ("COLD-JOURNEY",)),
            "J4": (30, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
        }),
        "tutorial": (13, 85, {
            "T1": (40, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "T2": (35, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "T3": (25, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
        }),
        "hierarchy": (13, 85, {
            "H1": (25, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "H2": (25, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "H3": (25, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "H4": (25, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
        }),
        "feedback": (12, 85, {
            "I1": (35, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "I2": (30, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "I3": (35, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
        }),
        "causality": (13, 85, {
            "C1": (20, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "C2": (20, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "C3": (20, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "C4": (20, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "C5": (20, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
        }),
        "agency": (10, 70, {
            "A1": (30, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "A2": (25, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "A3": (25, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "A4": (20, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
        }),
        "pacing": (8, 70, {
            "P1": (25, ("COLD-JOURNEY",)),
            "P2": (35, ("COLD-JOURNEY",)),
            "P3": (40, ("COLD-JOURNEY",)),
        }),
        "audiovisual": (6, 70, {
            "V1": (25, ("COVERAGE-JOURNEY",)),
            "V2": (25, ("COVERAGE-JOURNEY",)),
            "V3": (25, ("COVERAGE-JOURNEY",)),
            "V4": (25, ("COVERAGE-JOURNEY",)),
        }),
        "recovery": (5, 85, {
            "R1": (30, ("COVERAGE-JOURNEY",)),
            "R2": (45, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "R3": (25, ("COVERAGE-JOURNEY",)),
        }),
        "accessibility": (4, 85, {
            "L1": (40, ("COVERAGE-JOURNEY",)),
            "L2": (40, ("COVERAGE-JOURNEY",)),
            "L3": (20, ("COVERAGE-JOURNEY",)),
        }),
        "korean": (4, 85, {
            "K1": (35, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "K2": (35, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
            "K3": (30, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
        }),
    }
    actual_native: dict[str, Any] = {}
    for category in rubric["native"]["categories"]:
        actual_native[category["id"]] = (
            category["weight"],
            category["minimumScore"],
            {
                cell["id"]: (cell["weight"], tuple(cell["laneOwnership"]))
                for cell in category["cells"]
            },
        )
        assert sum(cell["weight"] for cell in category["cells"]) == 100
    assert actual_native == expected_native
    assert sum(value[0] for value in expected_native.values()) == 100
    assert rubric["native"]["overallTarget"] == 87.0
    assert rubric["native"]["requiredCellMinimum"] == 70

    expected_text = {
        "journey": (12, {"TP-J1": 50, "TP-J2": 50}),
        "tutorial": (13, {"TP-T1": 40, "TP-T2": 35, "TP-T3": 25}),
        "causality": (13, {
            "TP-C1": 20,
            "TP-C2": 20,
            "TP-C3": 20,
            "TP-C4": 20,
            "TP-C5": 20,
        }),
        "agency": (10, {"TP-A1": 30, "TP-A2": 25, "TP-A3": 25, "TP-A4": 20}),
        "pacing": (8, {"TP-P1": 25, "TP-P2": 35, "TP-P3": 40}),
        "korean": (4, {"TP-K1": 35, "TP-K2": 35, "TP-K3": 30}),
    }
    actual_text = {
        category["id"]: (
            category["weight"],
            {cell["id"]: cell["weight"] for cell in category["cells"]},
        )
        for category in rubric["textPlan"]["categories"]
    }
    assert actual_text == expected_text
    assert sum(weight for weight, _ in expected_text.values()) == 60
    assert all(
        cell["laneOwnership"] == ["TEXT-PLAN"]
        for category in rubric["textPlan"]["categories"]
        for cell in category["cells"]
    )

    schema = load_json(SCHEMA_PATH)
    schema_cells = schema["$defs"]["cell"]["properties"]["cellId"]["enum"]
    assert schema_cells == all_text_cell_ids(rubric)
    assert schema["properties"]["cells"]["minItems"] == 20
    assert schema["properties"]["cells"]["maxItems"] == 20
    assert [
        rule["contains"]["properties"]["cellId"]["const"]
        for rule in schema["properties"]["cells"]["allOf"]
    ] == schema_cells
    assert all(
        rule["minContains"] == 1 and rule["maxContains"] == 1
        for rule in schema["properties"]["cells"]["allOf"]
    )
    assert schema["$defs"]["cell"]["properties"]["confidence"]["enum"] == ["HIGH", "MEDIUM"]
    assert "promptTemplateSha256" in schema["required"]
    assert "judgmentSchemaSha256" in schema["required"]
    assert schema["properties"]["promptTemplateSha256"]["pattern"] == "^sha256:[0-9a-f]{64}$"
    assert schema["properties"]["judgmentSchemaSha256"]["pattern"] == "^sha256:[0-9a-f]{64}$"
    assert schema["additionalProperties"] is False
    assert schema["$defs"]["cell"]["additionalProperties"] is False

    prompt = PROMPT_PATH.read_text(encoding="utf-8")
    lower_prompt = prompt.casefold()
    for hidden_term in ("87", "target", "previous", "history", "score"):
        assert hidden_term not in lower_prompt
    assert "recommendedchange" not in lower_prompt
    assert "__TEXT_PLAN_ARTIFACT__" in prompt
    assert "__PROMPT_TEMPLATE_SHA256__" in prompt
    assert "__JUDGMENT_SCHEMA_SHA256__" in prompt
    assert "Do not return a numeric rating" in prompt


def judgment(
    rubric: dict[str, Any],
    run_number: int,
    artifact_sha: str,
    default_label: str = "STRONG",
    overrides: dict[str, str] | None = None,
) -> dict[str, Any]:
    overrides = overrides or {}
    cells = []
    for cell_id in all_text_cell_ids(rubric):
        label = overrides.get(cell_id, default_label)
        strength = [] if label == "BROKEN" else [{
            "sourceRef": "context:premise",
            "observation": f"fixed self-test support for {cell_id}",
        }]
        gap = [{
            "sourceRef": "context:premise",
            "observation": f"fixed self-test gap for {cell_id}",
        }] if label == "BROKEN" else []
        cells.append({
            "cellId": cell_id,
            "label": label,
            "confidence": "HIGH",
            "strengthEvidence": strength,
            "gapEvidence": gap,
        })
    return {
        "protocol": "GRIDWORKS-COMMERCIAL-UX-TEXT-PLAN-JUDGMENT-v1",
        "judgeRunId": f"fresh-run-{run_number}",
        "judgeSlot": "SOL-ULTRA",
        "model": "gpt-5.6-sol",
        "reasoningEffort": "ultra",
        "textPlanSha256": artifact_sha,
        "promptTemplateSha256": PROMPT_TEMPLATE_SHA256,
        "judgmentSchemaSha256": JUDGMENT_SCHEMA_SHA256,
        "cells": cells,
    }


def run_aggregate(
    directory: Path,
    name: str,
    judgments: list[dict[str, Any]],
    text_plan: dict[str, Any],
    expect_success: bool = True,
    replacement_for: dict[str, Any] | None = None,
    rubric_payload: dict[str, Any] | None = None,
) -> dict[str, Any] | subprocess.CompletedProcess[str]:
    paths = []
    for index, payload in enumerate(judgments, start=1):
        path = directory / f"{name}-judge-{index}.json"
        path.write_text(json.dumps(payload, ensure_ascii=False) + "\n", encoding="utf-8")
        paths.append(path)
    output = directory / f"{name}-aggregate.json"
    text_plan_path = directory / f"{name}-text-plan.json"
    text_plan_path.write_text(
        json.dumps(text_plan, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    command = [
        "python3",
        str(AGGREGATOR_PATH),
        *(str(path) for path in paths),
        "--text-plan",
        str(text_plan_path),
        "--output",
        str(output),
    ]
    if replacement_for is not None:
        initial_path = directory / f"{name}-initial-aggregate.json"
        initial_path.write_text(
            json.dumps(replacement_for, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        command.extend(["--replacement-for", str(initial_path)])
    if rubric_payload is not None:
        rubric_path = directory / f"{name}-rubric.json"
        rubric_path.write_text(
            json.dumps(rubric_payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        command.extend(["--rubric", str(rubric_path)])
    completed = subprocess.run(
        command,
        capture_output=True,
        text=True,
    )
    if not expect_success:
        assert completed.returncode != 0, completed.stdout
        return completed
    assert completed.returncode == 0, completed.stderr
    return load_json(output)


def test_aggregation() -> None:
    rubric = load_json(RUBRIC_PATH)
    builder = load_module(BUILDER_PATH, "gridworks_commercial_ux_aggregate_builder_test")
    context = load_json(CONTEXT_PATH)
    campaign = load_json(CAMPAIGN_PATH)
    manifest = real_story_manifest()
    builder.validate_context(context)
    builder.validate_manifest_against_campaign(manifest, campaign, context)
    text_plan = builder.build_artifact(context, manifest)
    artifact_sha = text_plan["artifactSha256"]
    with tempfile.TemporaryDirectory(prefix="gridworks-commercial-ux-aggregate-test-") as temp:
        directory = Path(temp)

        all_strong = run_aggregate(
            directory,
            "all-strong",
            [judgment(rubric, index, artifact_sha) for index in range(1, 4)],
            text_plan,
        )
        assert isinstance(all_strong, dict)
        assert all_strong["status"] == "SCORED_FORMATIVE"
        assert all_strong["textRaw"] == 85.0
        assert all_strong["textRawSpread"] == 0.0
        assert all_strong["disagreementPenalty"] == 0.0
        assert all_strong["textPlanProxy"] == 85.0
        assert all_strong["commercialUXProxy"] is None
        assert all_strong["panelKind"] == "INITIAL"
        assert all_strong["replacementForPanelInputSha256"] is None
        assert all_strong["promptTemplateSha256"] == PROMPT_TEMPLATE_SHA256
        assert all_strong["judgmentSchemaSha256"] == JUDGMENT_SCHEMA_SHA256

        journey_excellent = {"TP-J1": "EXCELLENT", "TP-J2": "EXCELLENT"}
        above_threshold_example = run_aggregate(
            directory,
            "some-excellent",
            [
                judgment(rubric, index, artifact_sha, overrides=journey_excellent)
                for index in range(1, 4)
            ],
            text_plan,
        )
        assert isinstance(above_threshold_example, dict)
        assert above_threshold_example["textPlanProxy"] == 88.0
        assert above_threshold_example["textPlanProxy"] >= 87.0

        spread_panel = [
            judgment(rubric, 1, artifact_sha, default_label="STRONG"),
            judgment(rubric, 2, artifact_sha, default_label="EXCELLENT"),
            judgment(rubric, 3, artifact_sha, default_label="STRONG"),
        ]
        spread = run_aggregate(directory, "stable-spread", spread_panel, text_plan)
        assert isinstance(spread, dict)
        assert spread["cellScores"]["TP-J1"]["medianScore"] == 85
        assert spread["cellScores"]["TP-J1"]["spread"] == 15
        assert spread["textRawSpread"] == 15.0
        assert spread["disagreementPenalty"] == 3.0
        assert spread["textPlanProxy"] == 82.0

        unstable_panel = [
            judgment(rubric, 1, artifact_sha, overrides={"TP-J1": "BROKEN"}),
            judgment(rubric, 2, artifact_sha, overrides={"TP-J1": "SERVICEABLE"}),
            judgment(rubric, 3, artifact_sha, overrides={"TP-J1": "BROKEN"}),
        ]
        unstable = run_aggregate(directory, "unstable", unstable_panel, text_plan)
        assert isinstance(unstable, dict)
        assert unstable["status"] == "RERUN_REQUIRED_JUDGE_INSTABILITY"
        assert unstable["textPlanProxy"] is None
        assert unstable["commercialUXProxy"] is None
        assert unstable["unstableCells"] == ["TP-J1"]
        assert unstable["cellScores"]["TP-J1"]["ordinalRange"] == 2
        assert unstable["panelKind"] == "INITIAL"
        assert unstable["replacementForPanelInputSha256"] is None
        assert unstable["rerunRequired"] is True

        replacement_unstable_panel = [
            judgment(rubric, 4, artifact_sha, overrides={"TP-J1": "BROKEN"}),
            judgment(rubric, 5, artifact_sha, overrides={"TP-J1": "SERVICEABLE"}),
            judgment(rubric, 6, artifact_sha, overrides={"TP-J1": "BROKEN"}),
        ]
        replacement_unstable = run_aggregate(
            directory,
            "replacement-unstable",
            replacement_unstable_panel,
            text_plan,
            replacement_for=unstable,
        )
        assert isinstance(replacement_unstable, dict)
        assert replacement_unstable["status"] == "BLOCKED_JUDGE_INSTABILITY"
        assert replacement_unstable["textPlanProxy"] is None
        assert replacement_unstable["panelKind"] == "REPLACEMENT"
        assert (
            replacement_unstable["replacementForPanelInputSha256"]
            == unstable["panelInputSha256"]
        )
        assert replacement_unstable["rerunRequired"] is False

        stable_replacement = run_aggregate(
            directory,
            "replacement-stable",
            [judgment(rubric, index, artifact_sha) for index in range(4, 7)],
            text_plan,
            replacement_for=unstable,
        )
        assert isinstance(stable_replacement, dict)
        assert stable_replacement["status"] == "SCORED_FORMATIVE"
        assert stable_replacement["textPlanProxy"] == 85.0
        assert stable_replacement["panelKind"] == "REPLACEMENT"
        assert (
            stable_replacement["replacementForPanelInputSha256"]
            == unstable["panelInputSha256"]
        )
        assert stable_replacement["rerunRequired"] is False

        exact_ninety_cells = {
            "TP-J1": "EXCELLENT",
            "TP-J2": "EXCELLENT",
            "TP-A1": "EXCELLENT",
            "TP-A2": "EXCELLENT",
            "TP-A3": "EXCELLENT",
        }
        exact_ninety = run_aggregate(
            directory,
            "exact-ninety",
            [
                judgment(rubric, index, artifact_sha, overrides=exact_ninety_cells)
                for index in range(1, 4)
            ],
            text_plan,
        )
        assert isinstance(exact_ninety, dict)
        assert exact_ninety["textPlanProxy"] == 90.0
        assert exact_ninety["commercialUXProxy"] is None
        assert exact_ninety["officialCommercialUX"] is False

        duplicate_runs = [judgment(rubric, index, artifact_sha) for index in range(1, 4)]
        duplicate_runs[2]["judgeRunId"] = duplicate_runs[1]["judgeRunId"]
        rejected_duplicate = run_aggregate(
            directory,
            "duplicate-run",
            duplicate_runs,
            text_plan,
            expect_success=False,
        )
        assert isinstance(rejected_duplicate, subprocess.CompletedProcess)
        assert "distinct judgeRunId" in rejected_duplicate.stderr

        low_confidence = [judgment(rubric, index, artifact_sha) for index in range(1, 4)]
        low_confidence[0]["cells"][0]["confidence"] = "LOW"
        rejected_low = run_aggregate(
            directory,
            "low-confidence",
            low_confidence,
            text_plan,
            expect_success=False,
        )
        assert isinstance(rejected_low, subprocess.CompletedProcess)
        assert "confidence must be HIGH or MEDIUM" in rejected_low.stderr

        missing_evidence = [judgment(rubric, index, artifact_sha) for index in range(1, 4)]
        missing_evidence[0]["cells"][0]["strengthEvidence"] = []
        rejected_evidence = run_aggregate(
            directory,
            "missing-evidence",
            missing_evidence,
            text_plan,
            expect_success=False,
        )
        assert isinstance(rejected_evidence, subprocess.CompletedProcess)
        assert "at least one grounded evidence row" in rejected_evidence.stderr

        bad_ref = [judgment(rubric, index, artifact_sha) for index in range(1, 4)]
        bad_ref[0]["cells"][0]["strengthEvidence"][0]["sourceRef"] = "selector:not-present"
        rejected_ref = run_aggregate(
            directory,
            "bad-source-ref",
            bad_ref,
            text_plan,
            expect_success=False,
        )
        assert isinstance(rejected_ref, subprocess.CompletedProcess)
        assert "does not exist in the text-plan artifact" in rejected_ref.stderr

        tampered_text_plan = copy.deepcopy(text_plan)
        tampered_text_plan["artifact"]["premise"] += " tampered"
        rejected_tamper = run_aggregate(
            directory,
            "tampered-artifact",
            [judgment(rubric, index, artifact_sha) for index in range(1, 4)],
            tampered_text_plan,
            expect_success=False,
        )
        assert isinstance(rejected_tamper, subprocess.CompletedProcess)
        assert "artifact hash mismatch" in rejected_tamper.stderr

        wrong_declared_hash = copy.deepcopy(text_plan)
        wrong_declared_hash["artifactSha256"] = "sha256:" + "f" * 64
        rejected_hash = run_aggregate(
            directory,
            "wrong-envelope-hash",
            [judgment(rubric, index, artifact_sha) for index in range(1, 4)],
            wrong_declared_hash,
            expect_success=False,
        )
        assert isinstance(rejected_hash, subprocess.CompletedProcess)
        assert "artifact hash mismatch" in rejected_hash.stderr

        wrong_judgment_hash = "sha256:" + "e" * 64
        rejected_judgment_hash = run_aggregate(
            directory,
            "wrong-judgment-hash",
            [judgment(rubric, index, wrong_judgment_hash) for index in range(1, 4)],
            text_plan,
            expect_success=False,
        )
        assert isinstance(rejected_judgment_hash, subprocess.CompletedProcess)
        assert "does not match the supplied text-plan envelope" in rejected_judgment_hash.stderr

        wrong_prompt_contract = [
            judgment(rubric, index, artifact_sha) for index in range(1, 4)
        ]
        wrong_prompt_contract[0]["promptTemplateSha256"] = "sha256:" + "d" * 64
        rejected_prompt_contract = run_aggregate(
            directory,
            "wrong-prompt-contract",
            wrong_prompt_contract,
            text_plan,
            expect_success=False,
        )
        assert isinstance(rejected_prompt_contract, subprocess.CompletedProcess)
        assert "promptTemplateSha256 must be" in rejected_prompt_contract.stderr

        wrong_schema_contract = [
            judgment(rubric, index, artifact_sha) for index in range(1, 4)
        ]
        wrong_schema_contract[0]["judgmentSchemaSha256"] = "sha256:" + "c" * 64
        rejected_schema_contract = run_aggregate(
            directory,
            "wrong-schema-contract",
            wrong_schema_contract,
            text_plan,
            expect_success=False,
        )
        assert isinstance(rejected_schema_contract, subprocess.CompletedProcess)
        assert "judgmentSchemaSha256 must be" in rejected_schema_contract.stderr

        overlap_replacement = run_aggregate(
            directory,
            "overlap-replacement-runs",
            [judgment(rubric, index, artifact_sha) for index in range(1, 4)],
            text_plan,
            expect_success=False,
            replacement_for=unstable,
        )
        assert isinstance(overlap_replacement, subprocess.CompletedProcess)
        assert "must be fresh and disjoint" in overlap_replacement.stderr

        replacement_for_stable = run_aggregate(
            directory,
            "replacement-for-stable",
            [judgment(rubric, index, artifact_sha) for index in range(4, 7)],
            text_plan,
            expect_success=False,
            replacement_for=all_strong,
        )
        assert isinstance(replacement_for_stable, subprocess.CompletedProcess)
        assert "initial aggregate status mismatch" in replacement_for_stable.stderr

        marked_replacement_initial = copy.deepcopy(unstable)
        marked_replacement_initial["panelKind"] = "REPLACEMENT"
        marked_replacement_initial["replacementForPanelInputSha256"] = "sha256:" + "a" * 64
        rejected_marked_initial = run_aggregate(
            directory,
            "replacement-for-marked-replacement",
            [judgment(rubric, index, artifact_sha) for index in range(4, 7)],
            text_plan,
            expect_success=False,
            replacement_for=marked_replacement_initial,
        )
        assert isinstance(rejected_marked_initial, subprocess.CompletedProcess)
        assert "initial aggregate panelKind mismatch" in rejected_marked_initial.stderr

        initial_hash_fields = {
            "textPlanSha256": "sha256:" + "1" * 64,
            "rubricSha256": "sha256:" + "2" * 64,
            "promptTemplateSha256": "sha256:" + "3" * 64,
            "judgmentSchemaSha256": "sha256:" + "4" * 64,
        }
        for hash_field, wrong_hash in initial_hash_fields.items():
            mismatched_initial = copy.deepcopy(unstable)
            mismatched_initial[hash_field] = wrong_hash
            rejected_initial_hash = run_aggregate(
                directory,
                f"replacement-{hash_field}-mismatch",
                [judgment(rubric, index, artifact_sha) for index in range(4, 7)],
                text_plan,
                expect_success=False,
                replacement_for=mismatched_initial,
            )
            assert isinstance(rejected_initial_hash, subprocess.CompletedProcess)
            assert f"initial aggregate {hash_field} mismatch" in rejected_initial_hash.stderr

        drifted_rubric = copy.deepcopy(rubric)
        drifted_rubric["textPlan"]["categories"][0]["cells"][0]["weight"] = 51
        drifted_rubric["textPlan"]["categories"][0]["cells"][1]["weight"] = 49
        rejected_rubric = run_aggregate(
            directory,
            "drifted-rubric",
            [judgment(rubric, index, artifact_sha) for index in range(1, 4)],
            text_plan,
            expect_success=False,
            rubric_payload=drifted_rubric,
        )
        assert isinstance(rejected_rubric, subprocess.CompletedProcess)
        assert "rubric canonical JSON hash drift" in rejected_rubric.stderr

        bad_part_order = copy.deepcopy(text_plan)
        bad_part_order["artifact"]["storyParts"][0], bad_part_order["artifact"]["storyParts"][1] = (
            bad_part_order["artifact"]["storyParts"][1],
            bad_part_order["artifact"]["storyParts"][0],
        )
        bad_part_order["artifactSha256"] = "sha256:" + hashlib.sha256(
            json.dumps(
                bad_part_order["artifact"],
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
        ).hexdigest()
        rejected_part_order = run_aggregate(
            directory,
            "bad-artifact-part-order",
            [
                judgment(rubric, index, bad_part_order["artifactSha256"])
                for index in range(1, 4)
            ],
            bad_part_order,
            expect_success=False,
        )
        assert isinstance(rejected_part_order, subprocess.CompletedProcess)
        assert ".selector mismatch" in rejected_part_order.stderr

        bad_reachable = copy.deepcopy(text_plan)
        bad_reachable["artifact"]["storyParts"][0]["reachable"] = False
        bad_reachable["artifactSha256"] = "sha256:" + hashlib.sha256(
            json.dumps(
                bad_reachable["artifact"],
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
        ).hexdigest()
        rejected_reachable = run_aggregate(
            directory,
            "bad-artifact-reachable",
            [
                judgment(rubric, index, bad_reachable["artifactSha256"])
                for index in range(1, 4)
            ],
            bad_reachable,
            expect_success=False,
        )
        assert isinstance(rejected_reachable, subprocess.CompletedProcess)
        assert ".reachable mismatch" in rejected_reachable.stderr

        bad_story_shape = copy.deepcopy(text_plan)
        bad_story_shape["artifact"]["storyParts"][0]["story"]["extra"] = "invalid"
        bad_story_shape["artifactSha256"] = "sha256:" + hashlib.sha256(
            json.dumps(
                bad_story_shape["artifact"],
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
        ).hexdigest()
        rejected_story_shape = run_aggregate(
            directory,
            "bad-artifact-story-shape",
            [
                judgment(rubric, index, bad_story_shape["artifactSha256"])
                for index in range(1, 4)
            ],
            bad_story_shape,
            expect_success=False,
        )
        assert isinstance(rejected_story_shape, subprocess.CompletedProcess)
        assert ".story keys mismatch" in rejected_story_shape.stderr


def valid_story_manifest(builder: ModuleType) -> dict[str, Any]:
    assert builder.MANIFEST_SCHEMA == "gridworks.commercial.story-manifest.v1"
    return real_story_manifest()


def run_builder(
    directory: Path,
    name: str,
    manifest: dict[str, Any],
    expect_success: bool = True,
) -> tuple[subprocess.CompletedProcess[str], Path]:
    manifest_path = directory / f"{name}-manifest.json"
    output_path = directory / f"{name}-artifact.json"
    manifest_path.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    completed = subprocess.run(
        [
            "python3",
            str(BUILDER_PATH),
            "--story-manifest",
            str(manifest_path),
            "--campaign",
            str(CAMPAIGN_PATH),
            "--context",
            str(CONTEXT_PATH),
            "--output",
            str(output_path),
        ],
        capture_output=True,
        text=True,
    )
    if expect_success:
        assert completed.returncode == 0, completed.stderr
        assert output_path.is_file()
    else:
        assert completed.returncode != 0, completed.stdout
    return completed, output_path


def test_text_plan_builder() -> None:
    builder = load_module(BUILDER_PATH, "gridworks_commercial_ux_text_plan_builder_test")
    assert [part.selector for part in builder.EXPECTED_PARTS] == EXPECTED_SELECTORS
    manifest = valid_story_manifest(builder)
    with tempfile.TemporaryDirectory(prefix="gridworks-commercial-ux-builder-test-") as temp:
        directory = Path(temp)
        _, first_path = run_builder(directory, "valid-first", manifest)
        _, second_path = run_builder(directory, "valid-second", manifest)
        assert first_path.read_bytes() == second_path.read_bytes()
        envelope = load_json(first_path)
        artifact = envelope["artifact"]
        expected_digest = hashlib.sha256(
            json.dumps(
                artifact,
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
        ).hexdigest()
        assert envelope["artifactSha256"] == f"sha256:{expected_digest}"
        assert envelope["schemaVersion"] == builder.ENVELOPE_SCHEMA
        assert artifact["schemaVersion"] == builder.ARTIFACT_SCHEMA
        assert len(artifact["chapters"]) == 8
        assert len(artifact["storyParts"]) == 26
        assert [part["selector"] for part in artifact["storyParts"]] == EXPECTED_SELECTORS

        bad_count = copy.deepcopy(manifest)
        bad_count["count"] = 25
        rejected_count, _ = run_builder(directory, "bad-count", bad_count, expect_success=False)
        assert "count must be exactly 26" in rejected_count.stderr

        bad_order = copy.deepcopy(manifest)
        bad_order["parts"][0], bad_order["parts"][1] = (
            bad_order["parts"][1],
            bad_order["parts"][0],
        )
        rejected_order, _ = run_builder(directory, "bad-order", bad_order, expect_success=False)
        assert "selector mismatch" in rejected_order.stderr

        bad_shape = copy.deepcopy(manifest)
        bad_shape["parts"][0]["nativeExposure"] = True
        rejected_shape, _ = run_builder(directory, "bad-shape", bad_shape, expect_success=False)
        assert "keys mismatch" in rejected_shape.stderr

        altered_story = copy.deepcopy(manifest)
        altered_story["parts"][0]["story"]["body"] = "nonempty but not campaign authority"
        rejected_story, _ = run_builder(
            directory,
            "altered-story",
            altered_story,
            expect_success=False,
        )
        assert "story does not match campaign authority" in rejected_story.stderr


def main() -> None:
    test_canonical_rubric_and_schema()
    test_aggregation()
    test_text_plan_builder()
    print(
        "commercial UX text-plan self-test: PASS "
        "(rubric/schema/prompt, provenance aggregation scenarios, 5 builder scenarios)"
    )


if __name__ == "__main__":
    main()
