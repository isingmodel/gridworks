#!/usr/bin/env python3
"""Isolated self-test for the blinded text-plan evidence verification lane."""

from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
import subprocess
import tempfile
from pathlib import Path
from types import ModuleType
from typing import Any


ROOT = Path(__file__).resolve().parent
PREPARE_PATH = ROOT / "prepare-text-plan-evidence.py"
AGGREGATE_PATH = ROOT / "aggregate-text-plan-evidence.py"
JUDGE_AGGREGATOR_PATH = ROOT / "aggregate-text-plan.py"
RUBRIC_PATH = ROOT / "rubric.json"
PROMPT_PATH = ROOT / "text-plan-evidence-verifier-prompt.template.txt"
SCHEMA_PATH = ROOT / "text-plan-evidence-verifier.schema.json"
CONTEXT_PATH = ROOT / "text-plan-context.json"


def load_module(path: Path, name: str) -> ModuleType:
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def sha256_identifier(value: Any) -> str:
    return "sha256:" + hashlib.sha256(canonical_json_bytes(value)).hexdigest()


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    assert isinstance(value, dict)
    return value


def write_json(path: Path, value: Any) -> None:
    path.write_text(
        json.dumps(value, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def story_part(selector: str) -> dict[str, Any]:
    if selector == "campaign/epilogue":
        kind = "epilogue"
        chapter_id = None
        window_id = None
        promise_branch = None
    else:
        chapter_id, part_kind, *tail = selector.split("/")
        kind = part_kind
        window_id = tail[0] if kind == "window" else None
        promise_branch = tail[0] if kind == "result" and tail[0] in {"keep", "defer"} else None
    return {
        "schemaVersion": "gridworks.commercial.story-part-output.v1",
        "campaignId": "CHEONGRYU_COMMERCIAL_CAMPAIGN_V2",
        "selector": selector,
        "kind": kind,
        "chapterId": chapter_id,
        "windowId": window_id,
        "reachable": True,
        "requiredPromiseBranch": promise_branch,
        "story": {
            "speaker": "self-test speaker",
            "title": f"self-test {selector}",
            "body": f"The artifact contains the authored part for {selector}.",
        },
    }


def valid_text_plan(prepare: ModuleType) -> dict[str, Any]:
    context = read_json(CONTEXT_PATH)
    artifact = {
        "schemaVersion": "gridworks.commercial-ux.text-plan-input.v1",
        "campaignId": context["campaignId"],
        "premise": context["premise"],
        "playerRole": context["playerRole"],
        "chapters": context["chapters"],
        "storyParts": [story_part(selector) for selector in prepare.EXPECTED_SELECTORS],
    }
    return {
        "schemaVersion": "gridworks.commercial-ux.text-plan-envelope.v1",
        "artifactSha256": sha256_identifier(artifact),
        "artifact": artifact,
    }


def valid_judgment(
    prepare: ModuleType,
    run_number: int,
    text_plan_sha256: str,
    default_label: str = "STRONG",
    overrides: dict[str, str] | None = None,
) -> dict[str, Any]:
    overrides = overrides or {}
    cells = []
    for cell_id in prepare.EXPECTED_CELL_IDS:
        judgment_label = overrides.get(cell_id, default_label)
        cells.append(
            {
                "cellId": cell_id,
                "label": judgment_label,
                "confidence": "HIGH",
                "strengthEvidence": [] if judgment_label == "BROKEN" else [
                    {
                        "sourceRef": "context:premise",
                        "observation": f"The premise explicitly identifies the game setting for {cell_id}.",
                    }
                ],
                "gapEvidence": [
                    {
                        "sourceRef": "context:premise",
                        "observation": f"The premise omits the self-test detail for {cell_id}.",
                    }
                ] if judgment_label == "BROKEN" else [],
            }
        )
    return {
        "protocol": "GRIDWORKS-COMMERCIAL-UX-TEXT-PLAN-JUDGMENT-v1",
        "judgeRunId": f"private-judge-{run_number}",
        "judgeSlot": "SOL-ULTRA",
        "model": "gpt-5.6-sol",
        "reasoningEffort": "ultra",
        "promptTemplateSha256": "sha256:" + hashlib.sha256(
            prepare.JUDGE_PROMPT_PATH.read_bytes()
        ).hexdigest(),
        "judgmentSchemaSha256": "sha256:" + hashlib.sha256(
            prepare.JUDGMENT_SCHEMA_PATH.read_bytes()
        ).hexdigest(),
        "textPlanSha256": text_plan_sha256,
        "cells": cells,
    }


def build_judge_aggregate(
    aggregate_module: ModuleType,
    directory: Path,
    name: str,
    text_plan: dict[str, Any],
    judgments: list[dict[str, Any]],
    replacement_for: dict[str, Any] | None = None,
) -> dict[str, Any]:
    text_plan_path = directory / f"{name}-panel-text-plan.json"
    write_json(text_plan_path, text_plan)
    judgment_paths = []
    for index, judgment in enumerate(judgments, start=1):
        path = directory / f"{name}-panel-judgment-{index}.json"
        write_json(path, judgment)
        judgment_paths.append(path)
    replacement_path = None
    if replacement_for is not None:
        replacement_path = directory / f"{name}-initial-aggregate.json"
        write_json(replacement_path, replacement_for)
    result = aggregate_module.aggregate(
        judgment_paths,
        RUBRIC_PATH,
        text_plan_path,
        replacement_path,
        directory / f"{name}-panel-aggregate-output.json",
    )
    assert isinstance(result, dict)
    return result


def run_prepare(
    directory: Path,
    name: str,
    text_plan: dict[str, Any],
    judgments: list[dict[str, Any]],
    judge_aggregate: dict[str, Any],
    expect_success: bool = True,
) -> tuple[subprocess.CompletedProcess[str], Path]:
    text_plan_path = directory / f"{name}-text-plan.json"
    write_json(text_plan_path, text_plan)
    judgment_paths = []
    for index, judgment in enumerate(judgments, start=1):
        path = directory / f"{name}-judgment-{index}.json"
        write_json(path, judgment)
        judgment_paths.append(path)
    aggregate_path = directory / f"{name}-aggregate.json"
    write_json(aggregate_path, judge_aggregate)
    output_path = directory / f"{name}-evidence-input.json"
    completed = subprocess.run(
        [
            "python3",
            str(PREPARE_PATH),
            *(str(path) for path in judgment_paths),
            "--text-plan",
            str(text_plan_path),
            "--aggregate",
            str(aggregate_path),
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


def valid_verification(evidence_envelope: dict[str, Any]) -> dict[str, Any]:
    verification_input = evidence_envelope["verificationInput"]
    return {
        "protocol": "GRIDWORKS-COMMERCIAL-UX-TEXT-PLAN-EVIDENCE-VERIFICATION-v1",
        "verifierRunId": "fresh-private-verifier-1",
        "verifierSlot": "SOL-ULTRA",
        "model": "gpt-5.6-sol",
        "reasoningEffort": "ultra",
        "promptTemplateSha256": verification_input["promptTemplateSha256"],
        "verifierSchemaSha256": verification_input["verifierSchemaSha256"],
        "textPlanSha256": verification_input["textPlanSha256"],
        "judgePanelInputSha256": verification_input["judgePanelInputSha256"],
        "verificationInputSha256": evidence_envelope["verificationInputSha256"],
        "observations": [
            {
                "observationId": row["observationId"],
                "verdict": "SUPPORTED",
                "sourceRef": row["sourceRef"],
                "rationale": "The cited premise directly contains the claimed game setting.",
            }
            for row in verification_input["observations"]
        ],
    }


def run_aggregate(
    directory: Path,
    name: str,
    evidence_envelope: dict[str, Any],
    verification: Any,
    expected_code: int,
    raw_verification: str | None = None,
) -> dict[str, Any]:
    input_path = directory / f"{name}-input.json"
    verification_path = directory / f"{name}-verification.json"
    output_path = directory / f"{name}-result.json"
    write_json(input_path, evidence_envelope)
    if raw_verification is None:
        write_json(verification_path, verification)
    else:
        verification_path.write_text(raw_verification, encoding="utf-8")
    completed = subprocess.run(
        [
            "python3",
            str(AGGREGATE_PATH),
            "--input",
            str(input_path),
            "--verification",
            str(verification_path),
            "--output",
            str(output_path),
        ],
        capture_output=True,
        text=True,
    )
    assert completed.returncode == expected_code, completed.stderr
    return read_json(output_path)


def test_prompt_and_schema() -> None:
    prompt = PROMPT_PATH.read_text(encoding="utf-8")
    for placeholder in (
        "__VERIFIER_RUN_ID__",
        "__TEXT_PLAN_SHA256__",
        "__VERIFICATION_INPUT_SHA256__",
        "__PROMPT_TEMPLATE_SHA256__",
        "__VERIFIER_SCHEMA_SHA256__",
        "__JUDGE_PANEL_INPUT_SHA256__",
        "__VERIFICATION_INPUT__",
    ):
        assert placeholder in prompt
    assert 'model "gpt-5.6-sol"' in prompt
    assert 'reasoningEffort "ultra"' in prompt
    assert "SUPPORTED" in prompt and "PARTIAL" in prompt and "UNSUPPORTED" in prompt
    for hidden_fragment in (
        "87",
        "EXCELLENT",
        "STRONG",
        "SERVICEABLE",
        "WEAK",
        "BROKEN",
        "recommendedChange",
        "TextPlanProxy",
        "CommercialUXProxy",
    ):
        assert hidden_fragment not in prompt

    schema = read_json(SCHEMA_PATH)
    assert schema["additionalProperties"] is False
    assert schema["properties"]["model"]["const"] == "gpt-5.6-sol"
    assert schema["properties"]["reasoningEffort"]["const"] == "ultra"
    assert schema["properties"]["promptTemplateSha256"]["pattern"] == (
        "^sha256:[0-9a-f]{64}$"
    )
    assert schema["properties"]["verifierSchemaSha256"]["pattern"] == (
        "^sha256:[0-9a-f]{64}$"
    )
    assert schema["properties"]["judgePanelInputSha256"]["pattern"] == (
        "^sha256:[0-9a-f]{64}$"
    )
    assert schema["properties"]["observations"]["minItems"] == 1
    assert schema["properties"]["observations"]["maxItems"] == 480
    observation = schema["$defs"]["observation"]
    assert observation["additionalProperties"] is False
    assert observation["properties"]["verdict"]["enum"] == [
        "SUPPORTED",
        "PARTIAL",
        "UNSUPPORTED",
    ]


def test_prepare_and_aggregate() -> None:
    prepare = load_module(PREPARE_PATH, "gridworks_text_plan_evidence_prepare_test")
    judge_aggregator = load_module(
        JUDGE_AGGREGATOR_PATH,
        "gridworks_text_plan_judge_aggregate_binding_test",
    )
    text_plan = valid_text_plan(prepare)
    judgments = [
        valid_judgment(prepare, index, text_plan["artifactSha256"])
        for index in range(1, 4)
    ]
    with tempfile.TemporaryDirectory(prefix="gridworks-text-plan-evidence-test-") as temp:
        directory = Path(temp)
        scored_aggregate = build_judge_aggregate(
            judge_aggregator,
            directory,
            "valid",
            text_plan,
            judgments,
        )
        assert scored_aggregate["status"] == "SCORED_FORMATIVE"
        _, first_path = run_prepare(
            directory,
            "valid",
            text_plan,
            judgments,
            scored_aggregate,
        )
        evidence_envelope = read_json(first_path)
        verification_input = evidence_envelope["verificationInput"]
        assert evidence_envelope["schemaVersion"] == prepare.EVIDENCE_ENVELOPE_SCHEMA
        assert verification_input["schemaVersion"] == prepare.EVIDENCE_INPUT_SCHEMA
        assert evidence_envelope["verificationInputSha256"] == sha256_identifier(
            verification_input
        )
        assert verification_input["textPlanSha256"] == text_plan["artifactSha256"]
        assert verification_input["judgePanelInputSha256"] == (
            scored_aggregate["panelInputSha256"]
        )
        assert verification_input["promptTemplateSha256"] == (
            "sha256:" + hashlib.sha256(PROMPT_PATH.read_bytes()).hexdigest()
        )
        assert verification_input["verifierSchemaSha256"] == (
            "sha256:" + hashlib.sha256(SCHEMA_PATH.read_bytes()).hexdigest()
        )
        assert verification_input["artifact"] == text_plan["artifact"]
        assert len(verification_input["observations"]) == 20
        assert [row["observationId"] for row in verification_input["observations"]] == [
            f"OBS-{index:04d}" for index in range(1, 21)
        ]

        swapped_judgments = [judgments[2], judgments[0], judgments[1]]
        swapped_aggregate = build_judge_aggregate(
            judge_aggregator,
            directory,
            "swapped",
            text_plan,
            swapped_judgments,
        )
        _, swapped_path = run_prepare(
            directory,
            "swapped",
            text_plan,
            swapped_judgments,
            swapped_aggregate,
        )
        assert first_path.read_bytes() == swapped_path.read_bytes()

        forged_panel_hash = copy.deepcopy(scored_aggregate)
        forged_panel_hash["panelInputSha256"] = "sha256:" + "f" * 64
        forged_panel_completed, _ = run_prepare(
            directory,
            "forged-panel-hash",
            text_plan,
            judgments,
            forged_panel_hash,
            expect_success=False,
        )
        assert "panelInputSha256 mismatch" in forged_panel_completed.stderr

        forged_run_ids = copy.deepcopy(scored_aggregate)
        forged_run_ids["judgeRunIds"] = ["forged-1", "forged-2", "forged-3"]
        forged_runs_completed, _ = run_prepare(
            directory,
            "forged-run-ids",
            text_plan,
            judgments,
            forged_run_ids,
            expect_success=False,
        )
        assert "judgeRunIds do not exactly match" in forged_runs_completed.stderr

        unstable_judgments = [
            valid_judgment(
                prepare,
                11,
                text_plan["artifactSha256"],
                overrides={"TP-J1": "BROKEN"},
            ),
            valid_judgment(
                prepare,
                12,
                text_plan["artifactSha256"],
                overrides={"TP-J1": "SERVICEABLE"},
            ),
            valid_judgment(
                prepare,
                13,
                text_plan["artifactSha256"],
                overrides={"TP-J1": "BROKEN"},
            ),
        ]
        unstable_aggregate = build_judge_aggregate(
            judge_aggregator,
            directory,
            "unstable-initial",
            text_plan,
            unstable_judgments,
        )
        assert unstable_aggregate["status"] == "RERUN_REQUIRED_JUDGE_INSTABILITY"
        unscored_completed, _ = run_prepare(
            directory,
            "unscored-initial",
            text_plan,
            unstable_judgments,
            unstable_aggregate,
            expect_success=False,
        )
        assert "status mismatch" in unscored_completed.stderr

        replacement_judgments = [
            valid_judgment(prepare, index, text_plan["artifactSha256"])
            for index in range(14, 17)
        ]
        replacement_aggregate = build_judge_aggregate(
            judge_aggregator,
            directory,
            "stable-replacement",
            text_plan,
            replacement_judgments,
            replacement_for=unstable_aggregate,
        )
        assert replacement_aggregate["status"] == "SCORED_FORMATIVE"
        assert replacement_aggregate["panelKind"] == "REPLACEMENT"
        assert replacement_aggregate["replacementReceiptSha256"].startswith("sha256:")
        _, replacement_path = run_prepare(
            directory,
            "valid-replacement",
            text_plan,
            replacement_judgments,
            replacement_aggregate,
        )
        replacement_input = read_json(replacement_path)["verificationInput"]
        assert replacement_input["judgePanelInputSha256"] == (
            replacement_aggregate["panelInputSha256"]
        )

        replacement_as_initial_completed, _ = run_prepare(
            directory,
            "replacement-as-initial",
            text_plan,
            replacement_judgments,
            scored_aggregate,
            expect_success=False,
        )
        assert "judgeRunIds do not exactly match" in replacement_as_initial_completed.stderr
        initial_as_replacement_completed, _ = run_prepare(
            directory,
            "initial-as-replacement",
            text_plan,
            judgments,
            replacement_aggregate,
            expect_success=False,
        )
        assert "judgeRunIds do not exactly match" in initial_as_replacement_completed.stderr

        forged_initial_as_replacement = copy.deepcopy(scored_aggregate)
        forged_initial_as_replacement["panelKind"] = "REPLACEMENT"
        forged_initial_as_replacement["replacementForPanelInputSha256"] = (
            unstable_aggregate["panelInputSha256"]
        )
        forged_initial_as_replacement["replacementReceiptSha256"] = (
            replacement_aggregate["replacementReceiptSha256"]
        )
        forged_initial_kind_completed, _ = run_prepare(
            directory,
            "forged-initial-as-replacement",
            text_plan,
            judgments,
            forged_initial_as_replacement,
            expect_success=False,
        )
        assert "panelInputSha256 mismatch" in forged_initial_kind_completed.stderr

        forged_replacement_as_initial = copy.deepcopy(replacement_aggregate)
        forged_replacement_as_initial["panelKind"] = "INITIAL"
        forged_replacement_as_initial["replacementForPanelInputSha256"] = None
        forged_replacement_as_initial["replacementReceiptSha256"] = None
        forged_replacement_kind_completed, _ = run_prepare(
            directory,
            "forged-replacement-as-initial",
            text_plan,
            replacement_judgments,
            forged_replacement_as_initial,
            expect_success=False,
        )
        assert "panelInputSha256 mismatch" in forged_replacement_kind_completed.stderr

        missing_replacement_receipt = copy.deepcopy(replacement_aggregate)
        missing_replacement_receipt["replacementReceiptSha256"] = None
        missing_receipt_completed, _ = run_prepare(
            directory,
            "missing-replacement-receipt",
            text_plan,
            replacement_judgments,
            missing_replacement_receipt,
            expect_success=False,
        )
        assert "replacementReceiptSha256" in missing_receipt_completed.stderr

        serialized_inputs = (
            first_path.read_text(encoding="utf-8"),
            replacement_path.read_text(encoding="utf-8"),
        )
        for serialized in serialized_inputs:
            for hidden_fragment in (
                "private-judge-",
                '"cellId"',
                '"label"',
                '"confidence"',
                '"strengthEvidence"',
                '"gapEvidence"',
                '"recommendedChange"',
                '"score"',
                '"panelKind"',
                '"replacementForPanelInputSha256"',
                '"replacementReceiptSha256"',
                '"judgeRunIds"',
            ):
                assert hidden_fragment not in serialized

        verification = valid_verification(evidence_envelope)
        verified = run_aggregate(
            directory,
            "all-supported",
            evidence_envelope,
            verification,
            0,
        )
        assert verified["status"] == "VERIFIED_SUPPORTED_ONLY"
        assert verified["formativeConclusionsAllowed"] is True
        assert verified["promptTemplateSha256"] == verification_input["promptTemplateSha256"]
        assert verified["verifierSchemaSha256"] == verification_input["verifierSchemaSha256"]
        assert verified["judgePanelInputSha256"] == scored_aggregate["panelInputSha256"]
        assert verified["supportedObservationCount"] == 20
        assert verified["partialObservationCount"] == 0
        assert verified["unsupportedObservationCount"] == 0
        assert verified["missingObservationCount"] == 0
        assert verified["invalidObservationCount"] == 0
        assert verified["blockedObservationIds"] == []
        assert verified["blockers"] == []

        partial = copy.deepcopy(verification)
        partial["observations"][0]["verdict"] = "PARTIAL"
        partial_result = run_aggregate(directory, "partial", evidence_envelope, partial, 2)
        assert partial_result["status"] == "BLOCKED_EVIDENCE_VERIFICATION"
        assert partial_result["formativeConclusionsAllowed"] is False
        assert partial_result["partialObservationCount"] == 1
        assert partial_result["blockedObservationIds"] == ["OBS-0001"]
        assert partial_result["blockers"][0]["code"] == "PARTIAL_OBSERVATION"

        unsupported = copy.deepcopy(verification)
        unsupported["observations"][1]["verdict"] = "UNSUPPORTED"
        unsupported_result = run_aggregate(
            directory,
            "unsupported",
            evidence_envelope,
            unsupported,
            2,
        )
        assert unsupported_result["status"] == "BLOCKED_EVIDENCE_VERIFICATION"
        assert unsupported_result["unsupportedObservationCount"] == 1
        assert unsupported_result["blockedObservationIds"] == ["OBS-0002"]

        missing = copy.deepcopy(verification)
        missing["observations"].pop(2)
        missing_result = run_aggregate(directory, "missing", evidence_envelope, missing, 2)
        assert missing_result["missingObservationCount"] == 1
        assert missing_result["blockedObservationIds"] == ["OBS-0003"]
        assert any(row["code"] == "MISSING_OBSERVATION" for row in missing_result["blockers"])

        duplicate = copy.deepcopy(verification)
        duplicate["observations"].append(copy.deepcopy(duplicate["observations"][0]))
        duplicate_result = run_aggregate(
            directory,
            "duplicate",
            evidence_envelope,
            duplicate,
            2,
        )
        assert duplicate_result["invalidObservationCount"] == 1
        assert "OBS-0001" in duplicate_result["blockedObservationIds"]
        assert any(row["code"] == "DUPLICATE_OBSERVATION" for row in duplicate_result["blockers"])

        wrong_source = copy.deepcopy(verification)
        wrong_source["observations"][3]["sourceRef"] = "context:playerRole"
        wrong_source_result = run_aggregate(
            directory,
            "wrong-source",
            evidence_envelope,
            wrong_source,
            2,
        )
        assert wrong_source_result["invalidObservationCount"] == 1
        assert "OBS-0004" in wrong_source_result["blockedObservationIds"]
        assert any(row["code"] == "SOURCE_REF_MISMATCH" for row in wrong_source_result["blockers"])

        unknown = copy.deepcopy(verification)
        unknown["observations"].append(
            {
                "observationId": "OBS-9999",
                "verdict": "SUPPORTED",
                "sourceRef": "context:premise",
                "rationale": "irrelevant extra row",
            }
        )
        unknown_result = run_aggregate(directory, "unknown", evidence_envelope, unknown, 2)
        assert unknown_result["invalidObservationCount"] == 1
        assert any(row["code"] == "UNKNOWN_OBSERVATION" for row in unknown_result["blockers"])

        bad_identity = copy.deepcopy(verification)
        bad_identity["model"] = "different-model"
        identity_result = run_aggregate(
            directory,
            "identity",
            evidence_envelope,
            bad_identity,
            2,
        )
        assert identity_result["status"] == "BLOCKED_EVIDENCE_VERIFICATION"
        assert identity_result["invalidObservationCount"] == 1
        assert identity_result["blockers"][0]["code"] == "INVALID_VERIFIER_IDENTITY"

        wrong_hash = copy.deepcopy(verification)
        wrong_hash["verificationInputSha256"] = "sha256:" + "f" * 64
        hash_result = run_aggregate(
            directory,
            "wrong-hash",
            evidence_envelope,
            wrong_hash,
            2,
        )
        assert hash_result["blockers"][0]["code"] == "HASH_MISMATCH"

        wrong_prompt_hash = copy.deepcopy(verification)
        wrong_prompt_hash["promptTemplateSha256"] = "sha256:" + "e" * 64
        prompt_hash_result = run_aggregate(
            directory,
            "wrong-prompt-hash",
            evidence_envelope,
            wrong_prompt_hash,
            2,
        )
        assert prompt_hash_result["blockers"][0]["code"] == "HASH_MISMATCH"

        wrong_panel_binding = copy.deepcopy(verification)
        wrong_panel_binding["judgePanelInputSha256"] = "sha256:" + "d" * 64
        panel_binding_result = run_aggregate(
            directory,
            "wrong-panel-binding",
            evidence_envelope,
            wrong_panel_binding,
            2,
        )
        assert panel_binding_result["status"] == "BLOCKED_EVIDENCE_VERIFICATION"
        assert panel_binding_result["blockers"][0]["code"] == "HASH_MISMATCH"

        malformed_result = run_aggregate(
            directory,
            "malformed",
            evidence_envelope,
            {},
            2,
            raw_verification="{not-json",
        )
        assert malformed_result["blockers"][0]["code"] == "INVALID_VERIFICATION_OUTPUT"

        tampered_input = copy.deepcopy(evidence_envelope)
        tampered_input["verificationInput"]["artifact"]["premise"] += " tampered"
        tampered_result = run_aggregate(
            directory,
            "tampered-input",
            tampered_input,
            verification,
            2,
        )
        assert tampered_result["status"] == "BLOCKED_EVIDENCE_VERIFICATION"
        assert tampered_result["textPlanSha256"] is None
        assert tampered_result["blockers"][0]["code"] == "INVALID_VERIFICATION_INPUT"

        bad_ref_judgments = copy.deepcopy(judgments)
        bad_ref_judgments[0]["cells"][0]["strengthEvidence"][0]["sourceRef"] = "missing:ref"
        bad_ref_completed, _ = run_prepare(
            directory,
            "bad-ref",
            text_plan,
            bad_ref_judgments,
            scored_aggregate,
            expect_success=False,
        )
        assert "does not exist in the text-plan artifact" in bad_ref_completed.stderr

        leaking_judgments = copy.deepcopy(judgments)
        leaking_judgments[0]["cells"][0]["strengthEvidence"][0]["observation"] = (
            "This repeats the STRONG judgment metadata."
        )
        leaking_completed, _ = run_prepare(
            directory,
            "metadata-leak",
            text_plan,
            leaking_judgments,
            scored_aggregate,
            expect_success=False,
        )
        assert "leaks hidden judgment metadata" in leaking_completed.stderr

        duplicate_run_judgments = copy.deepcopy(judgments)
        duplicate_run_judgments[2]["judgeRunId"] = duplicate_run_judgments[1]["judgeRunId"]
        duplicate_run_completed, _ = run_prepare(
            directory,
            "duplicate-run",
            text_plan,
            duplicate_run_judgments,
            scored_aggregate,
            expect_success=False,
        )
        assert "distinct judgeRunId" in duplicate_run_completed.stderr


def main() -> None:
    test_prompt_and_schema()
    test_prepare_and_aggregate()
    print(
        "commercial UX text-plan evidence verifier self-test: PASS "
        "(prompt/schema, scored-panel binding, blinded input, verifier gate outcomes)"
    )


if __name__ == "__main__":
    main()
