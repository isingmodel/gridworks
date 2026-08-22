#!/usr/bin/env python3
"""Mutation self-tests for the candidate-independent native evaluator contract."""

from __future__ import annotations

import copy
import importlib.util
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from types import ModuleType
from typing import Any, Callable


ROOT = Path(__file__).resolve().parent
RUBRIC = ROOT.parent / "rubric.json"
VALIDATOR = ROOT / "validate-contract.py"


def load_module() -> ModuleType:
    spec = importlib.util.spec_from_file_location("gridworks_native_contract_validator", VALIDATOR)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def load_path_module(name: str, path: Path) -> ModuleType:
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    assert isinstance(value, dict)
    return value


def write_json(path: Path, value: Any) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def mutated_errors(
    module: ModuleType,
    mutate: Callable[[Path], None],
) -> list[str]:
    with tempfile.TemporaryDirectory(prefix="gridworks-native-contract-test-") as temporary:
        native = Path(temporary) / "native"
        shutil.copytree(ROOT, native)
        shutil.copy2(ROOT.parent / "aggregate-native.py", native.parent / "aggregate-native.py")
        mutate(native)
        errors, _ = module.validate_contract(native, RUBRIC)
        return errors


def assert_rejected(
    module: ModuleType,
    mutate: Callable[[Path], None],
    expected_fragment: str,
) -> None:
    errors = mutated_errors(module, mutate)
    assert errors, "mutated contract unexpectedly passed"
    assert any(expected_fragment in error for error in errors), (expected_fragment, errors)


def main() -> int:
    module = load_module()
    checks = 0

    completed = subprocess.run(
        ["python3", str(VALIDATOR), "--json"],
        cwd=ROOT.parents[3],
        capture_output=True,
        text=True,
    )
    assert completed.returncode == 0, completed.stderr or completed.stdout
    report = json.loads(completed.stdout)
    assert report["status"] == "PASS"
    assert report["nativeCellCount"] == 39
    assert report["coldAssignedCellCount"] == 30
    assert report["coverageAssignedCellCount"] == 34
    assert report["probeCount"] == 21
    assert report["episodeCount"] == 12
    assert report["holdoutCount"] == 8
    assert report["qualificationAnchorCount"] == 20
    assert report["scoreBearingReady"] is False
    checks += 1

    def add_cold_only_coverage_cell(native: Path) -> None:
        path = native / "coverage-recipe.json"
        value = read_json(path)
        value["episodes"][0]["cells"].append("J1")
        write_json(path, value)

    assert_rejected(module, add_cold_only_coverage_cell, "score-bearing cells outside lane")
    checks += 1

    def duplicate_cross_episode_action_occurrence(native: Path) -> None:
        path = native / "coverage-recipe.json"
        value = read_json(path)
        value["episodes"][1]["actions"][0] = value["episodes"][0]["actions"][0]
        write_json(path, value)

    assert_rejected(
        module,
        duplicate_cross_episode_action_occurrence,
        "action occurrence ID is not globally unique",
    )
    checks += 1

    def break_probe_checkpoint(native: Path) -> None:
        path = native / "concept-exposure-manifest.json"
        value = read_json(path)
        value["probes"][0]["firstCheckpoint"] = "not-a-real-checkpoint"
        write_json(path, value)

    assert_rejected(module, break_probe_checkpoint, "first checkpoint is not in coverage recipe")
    checks += 1

    def drift_holdout_bits(native: Path) -> None:
        path = native / "holdout-recipes.json"
        value = read_json(path)
        value["holdouts"][2]["missionPrototypeBits"] = "111"
        write_json(path, value)

    assert_rejected(module, drift_holdout_bits, "bits drift")
    checks += 1

    def leak_qualification_band_in_id(native: Path) -> None:
        path = native / "qualification-transport-map.json"
        value = read_json(path)
        value["entries"][0]["transportId"] = "Q-EXCELLENT-01"
        write_json(path, value)

    assert_rejected(module, leak_qualification_band_in_id, "transport IDs/order drift")
    checks += 1

    def drift_judge_lane_cells(native: Path) -> None:
        path = native / "native-judge.schema.json"
        value = read_json(path)
        value["$defs"]["coverageCellId"]["enum"].remove("K3")
        write_json(path, value)

    assert_rejected(module, drift_judge_lane_cells, "judge schema coverage cells drift")
    checks += 1

    def remove_media_reader_allowlist(native: Path) -> None:
        path = native / "native-evidence-verifier-prompt.template.txt"
        text = path.read_text(encoding="utf-8")
        path.write_text(text.replace("harness-provided media reader", "unavailable reader"), encoding="utf-8")

    assert_rejected(module, remove_media_reader_allowlist, "does not allow supplied media")
    checks += 1

    def drift_bound_prompt_raw_bytes(native: Path) -> None:
        path = native / "cold-actor-prompt.template.txt"
        path.write_text(path.read_text(encoding="utf-8") + "\n", encoding="utf-8")

    assert_rejected(module, drift_bound_prompt_raw_bytes, "contract binding raw SHA mismatch")
    checks += 1

    def drift_score_producer_raw_bytes(native: Path) -> None:
        path = native.parent / "aggregate-native.py"
        path.write_text(path.read_text(encoding="utf-8") + "\n", encoding="utf-8")

    assert_rejected(
        module,
        drift_score_producer_raw_bytes,
        "contract binding raw SHA mismatch: ../aggregate-native.py",
    )
    checks += 1

    def remove_required_packager_stage(native: Path) -> None:
        path = native / "contract-bindings.json"
        value = read_json(path)
        value["stageBindings"] = [
            row
            for row in value["stageBindings"]
            if row["stageId"] != "EVIDENCE-SET-PACKAGER"
        ]
        write_json(path, value)

    assert_rejected(module, remove_required_packager_stage, "stage DAG/order drift")
    checks += 1

    def make_run_provenance_post_aggregate(native: Path) -> None:
        path = native / "contract-bindings.json"
        value = read_json(path)
        stages = value["stageBindings"]
        stages[-2], stages[-1] = stages[-1], stages[-2]
        write_json(path, value)

    assert_rejected(
        module,
        make_run_provenance_post_aggregate,
        "stage DAG/order drift",
    )
    checks += 1

    def drop_evaluation_run_from_aggregate(native: Path) -> None:
        path = native / "contract-bindings.json"
        value = read_json(path)
        aggregate = next(
            row for row in value["stageBindings"]
            if row["stageId"] == "NATIVE-AGGREGATE"
        )
        aggregate["inputSchemas"].remove("evaluation-run-manifest.schema.json")
        write_json(path, value)

    assert_rejected(
        module,
        drop_evaluation_run_from_aggregate,
        "does not bind every pre-aggregate authority",
    )
    checks += 1

    anchors = read_json(ROOT / "qualification-anchors.json")
    transport_map = read_json(ROOT / "qualification-transport-map.json")
    qualification_schema = read_json(ROOT / "qualification-input.schema.json")
    projected = module.qualification_projection(
        anchors,
        transport_map,
        module.raw_sha256(ROOT / "qualification-anchors.json"),
        module.raw_sha256(RUBRIC),
    )
    assert not module.instance_errors(projected, qualification_schema)
    serialized = json.dumps(projected, ensure_ascii=False)
    assert "expectedLabel" not in serialized
    assert "authorityAnchorId" not in serialized
    assert all(label not in serialized for label in module.LABELS)
    poisoned = copy.deepcopy(projected)
    poisoned["anchors"][0]["expectedLabel"] = "EXCELLENT"
    assert any("additional property expectedLabel" in error for error in module.instance_errors(poisoned, qualification_schema))
    checks += 1

    with tempfile.TemporaryDirectory(prefix="gridworks-native-raw-hash-") as temporary:
        path_a = Path(temporary) / "a.json"
        path_b = Path(temporary) / "b.json"
        path_a.write_bytes(b'{"a":1}\n')
        path_b.write_bytes(b'{ "a": 1 }\n')
        assert json.loads(path_a.read_text()) == json.loads(path_b.read_text())
        assert module.raw_sha256(path_a) != module.raw_sha256(path_b)
    checks += 1

    envelope = {
        "schemaVersion": "self-test",
        "candidateManifestSha256": "sha256:" + "0" * 64,
        "value": "same semantic payload",
    }
    first_hash = module.self_hash(envelope, "candidateManifestSha256")
    envelope["candidateManifestSha256"] = "sha256:" + "f" * 64
    assert module.self_hash(envelope, "candidateManifestSha256") == first_hash
    envelope["value"] = "changed payload"
    assert module.self_hash(envelope, "candidateManifestSha256") != first_hash
    checks += 1

    cold_recipe = read_json(ROOT / "cold-journey-recipe.json")
    cold_schema = read_json(ROOT / "cold-journey-recipe.schema.json")
    assert not module.instance_errors(cold_recipe, cold_schema)
    bad_cold = copy.deepcopy(cold_recipe)
    bad_cold["routePolicy"]["hiddenHint"] = True
    assert module.instance_errors(bad_cold, cold_schema)
    checks += 1

    scorecard_schema = read_json(ROOT / "native-scorecard.schema.json")
    statuses = scorecard_schema["$defs"]["status"]["enum"]
    final_verdicts = scorecard_schema["$defs"]["finalVerdict"]["enum"]
    assert "SCORED_FORMATIVE" in statuses and "PASS" in final_verdicts
    formative_pair = scorecard_schema["oneOf"][0]["properties"]
    assert formative_pair["status"]["const"] == "SCORED_FORMATIVE"
    assert formative_pair["verdict"]["type"] == "null"
    phase_rule = scorecard_schema["allOf"][0]
    assert phase_rule["then"]["properties"]["officialCommercialUX"]["const"] is False
    assert phase_rule["if"]["properties"]["recipeId"]["const"] == "FORMATIVE-01"
    checks += 1

    candidate_schema = read_json(ROOT / "candidate-manifest.schema.json")
    top_required = set(candidate_schema["required"])
    assert "artifacts" not in top_required
    assert "retryLedger" not in top_required
    assert "terminalStates" not in top_required
    run_schema = read_json(ROOT / "evaluation-run-manifest.schema.json")
    assert {
        "candidateManifestSha256",
        "qualificationReceiptSha256",
        "judgePanelSha256",
        "artifacts",
        "retryLedger",
        "terminalStates",
    }.issubset(run_schema["required"])
    run_artifacts = set(run_schema["$defs"]["artifacts"]["required"])
    assert "scorecardSha256" not in run_artifacts
    assert "aggregationInputSha256" not in run_artifacts
    scorecard_provenance = set(scorecard_schema["$defs"]["provenance"]["required"])
    assert {
        "nativeAggregatorSha256",
        "qualificationReceiptSha256",
        "evaluationRunManifestSha256",
        "aggregationInputRawSha256",
    }.issubset(scorecard_provenance)
    assert "nativeAggregatorSha256" in set(
        candidate_schema["$defs"]["contractHashes"]["required"]
    )
    aggregation_schema = read_json(ROOT / "native-aggregation-input.schema.json")
    assert "nativeAggregatorSha256" in set(
        aggregation_schema["$defs"]["provenance"]["required"]
    )
    checks += 1

    qualification_receipt_schema = read_json(ROOT / "qualification-receipt.schema.json")
    fixture_hash = "sha256:" + "a" * 64

    def qualification_slot(slot_id: str, run_id: str, hash_digit: str) -> dict[str, Any]:
        return {
            "slotId": slot_id,
            "judgeRunId": run_id,
            "judgmentRawSha256": "sha256:" + hash_digit * 64,
            "exactCount": 19,
            "excellentAndBrokenAllExact": True,
            "schemaValidCount": 20,
            "status": "PASS",
        }

    qualification_receipt = {
        "schemaVersion": "gridworks.commercial-ux.native-qualification-receipt.v1",
        "protocol": module.PROTOCOL,
        "qualificationReceiptSha256": fixture_hash,
        "candidateIndependent": True,
        "model": "gpt-5.6-sol",
        "reasoningEffort": "ultra",
        "transportVersion": "fixture-v1",
        "promptTemplateSha256": fixture_hash,
        "judgmentSchemaSha256": fixture_hash,
        "qualificationInputSchemaSha256": fixture_hash,
        "qualificationInputSha256": fixture_hash,
        "qualificationAnchorsAuthoritySha256": fixture_hash,
        "qualificationTransportMapSha256": fixture_hash,
        "rubricSha256": fixture_hash,
        "passRule": {
            "minimumExactPerJudge": 19,
            "anchorCount": 20,
            "excellentAndBrokenAllExact": True,
            "schemaValidCount": 20,
            "fullPanelReplacementMaximum": 1,
        },
        "attempts": [{
            "attempt": 1,
            "slots": [
                qualification_slot("JUDGE-01", "qualification-01", "1"),
                qualification_slot("JUDGE-02", "qualification-02", "2"),
                qualification_slot("JUDGE-03", "qualification-03", "3"),
            ],
            "status": "PASS",
        }],
        "status": "PASS",
    }
    qualification_receipt["qualificationReceiptSha256"] = module.self_hash(
        qualification_receipt,
        "qualificationReceiptSha256",
    )
    assert not module.instance_errors(qualification_receipt, qualification_receipt_schema)
    qualification_semantic_errors: list[str] = []
    module.validate_qualification_receipt_semantics(
        qualification_receipt,
        qualification_semantic_errors,
    )
    assert not qualification_semantic_errors
    contradictory = copy.deepcopy(qualification_receipt)
    contradictory["status"] = "BLOCKED_JUDGE_QUALIFICATION"
    assert module.instance_errors(contradictory, qualification_receipt_schema)
    bad_replacement = copy.deepcopy(qualification_receipt)
    bad_replacement["attempts"].append(copy.deepcopy(bad_replacement["attempts"][0]))
    bad_replacement["attempts"][1]["attempt"] = 2
    assert module.instance_errors(bad_replacement, qualification_receipt_schema)
    replacement_pass = copy.deepcopy(qualification_receipt)
    replacement_pass["attempts"][0]["status"] = "INVALIDATED"
    replacement_pass["attempts"][0]["slots"][0].update({
        "exactCount": 18,
        "status": "FAIL_BAND",
    })
    replacement_pass["attempts"].append({
        "attempt": 2,
        "slots": [
            qualification_slot("JUDGE-01", "qualification-04", "4"),
            qualification_slot("JUDGE-02", "qualification-05", "5"),
            qualification_slot("JUDGE-03", "qualification-06", "6"),
        ],
        "status": "PASS",
    })
    assert not module.instance_errors(replacement_pass, qualification_receipt_schema)
    qualification_semantic_errors = []
    module.validate_qualification_receipt_semantics(
        replacement_pass,
        qualification_semantic_errors,
    )
    assert not qualification_semantic_errors
    duplicate_replacement_transport = copy.deepcopy(replacement_pass)
    duplicate_replacement_transport["attempts"][1]["slots"][0]["judgmentRawSha256"] = (
        duplicate_replacement_transport["attempts"][0]["slots"][0]["judgmentRawSha256"]
    )
    qualification_semantic_errors = []
    module.validate_qualification_receipt_semantics(
        duplicate_replacement_transport,
        qualification_semantic_errors,
    )
    assert any("judgmentRawSha256 values must be disjoint" in error for error in qualification_semantic_errors)
    blocked = copy.deepcopy(replacement_pass)
    blocked["status"] = "BLOCKED_JUDGE_QUALIFICATION"
    blocked["attempts"][1]["status"] = "FAIL"
    blocked["attempts"][1]["slots"][0].update({"exactCount": 18, "status": "FAIL_BAND"})
    assert not module.instance_errors(blocked, qualification_receipt_schema)
    qualification_semantic_errors = []
    module.validate_qualification_receipt_semantics(blocked, qualification_semantic_errors)
    assert not qualification_semantic_errors
    checks += 1

    actor_schema = read_json(ROOT / "actor-observation.schema.json")
    incident_schema = actor_schema["$defs"]["incident"]
    incident_hash = "sha256:" + "7" * 64
    artifact_ref = {"artifactId": "frame-1", "kind": "FRAME", "locator": "frame://1"}
    stall_incident = {
        "incidentKey": "FIRST_LIGHT/WINDOW/OPERATIONS/UX_STALL",
        "episode": "E01-FIRST-LIGHT",
        "checkpointOrdinals": [1],
        "incidentType": "UX_STALL",
        "confusionBoundary": None,
        "severity": "SEVERE",
        "description": "Twelve distinct rational actions left one checkpoint unchanged.",
        "actionIndexes": list(range(1, 13)),
        "artifactRefs": [artifact_ref],
    }
    assert not module.instance_errors(stall_incident, incident_schema, actor_schema)
    actor_semantic_fixture = {
        "actionLedger": [{"actionIndex": index} for index in range(1, 13)],
        "checkpoints": [{"ordinal": 1}],
        "incidents": [stall_incident],
        "terminalState": "PLAYER_STALLED",
        "terminalIncidentKey": stall_incident["incidentKey"],
    }
    actor_semantic_errors: list[str] = []
    module.validate_actor_observation_semantics(
        actor_semantic_fixture,
        actor_semantic_errors,
    )
    assert not actor_semantic_errors
    duplicate_action_index = copy.deepcopy(actor_semantic_fixture)
    duplicate_action_index["actionLedger"][11]["actionIndex"] = 11
    actor_semantic_errors = []
    module.validate_actor_observation_semantics(
        duplicate_action_index,
        actor_semantic_errors,
    )
    assert any("exact, unique, and strictly increasing" in error for error in actor_semantic_errors)
    short_stall = copy.deepcopy(stall_incident)
    short_stall["actionIndexes"].pop()
    assert module.instance_errors(short_stall, incident_schema, actor_schema)
    harness_incident = copy.deepcopy(stall_incident)
    harness_incident.update({
        "incidentKey": "FIRST_LIGHT/WINDOW/OPERATIONS/HARNESS_FAILURE",
        "incidentType": "HARNESS_FAILURE",
        "severity": "SEVERE",
        "actionIndexes": [],
    })
    assert module.instance_errors(harness_incident, incident_schema, actor_schema)

    terminal_schema = run_schema["$defs"]["terminalState"]
    terminal = {
        "actorArtifactId": incident_hash,
        "actorObservationRawSha256": fixture_hash,
        "state": "COMPLETED",
        "severeSingleRun": False,
        "incidentKeys": [],
        "terminalIncidentKey": None,
    }
    assert not module.instance_errors(terminal, terminal_schema, run_schema)
    bad_terminal = copy.deepcopy(terminal)
    bad_terminal["terminalIncidentKey"] = stall_incident["incidentKey"]
    bad_terminal["incidentKeys"] = [stall_incident["incidentKey"]]
    assert module.instance_errors(bad_terminal, terminal_schema, run_schema)
    stalled_terminal = copy.deepcopy(bad_terminal)
    stalled_terminal["state"] = "PLAYER_STALLED"
    assert not module.instance_errors(stalled_terminal, terminal_schema, run_schema)
    duplicate_terminal_rows = [terminal, copy.deepcopy(terminal), copy.deepcopy(terminal)]
    assert module.instance_errors(
        duplicate_terminal_rows,
        run_schema["properties"]["terminalStates"],
        run_schema,
    )
    checks += 1

    replacement_receipt_schema = read_json(ROOT / "native-replacement-receipt.schema.json")
    duplicate_transport_attempts = [
        {
            "slotId": f"JUDGE-0{index}",
            "path": f"/tmp/judge-{index}.json",
            "readStatus": "READ",
            "rawSha256": fixture_hash,
            "attemptOutcome": "TRANSPORT_FAILURE",
            "failureCode": "DUPLICATE_TRANSPORT_BODY",
        }
        for index in range(1, 4)
    ]
    identical_transport_receipt = {
        "schemaVersion": "gridworks.commercial-ux.native-replacement-receipt.v1",
        "protocol": module.PROTOCOL,
        "claimPolicy": (
            "O_EXCL_AFTER_AUTHORITY_PREFLIGHT_BEFORE_ATTEMPT_READ_"
            "THEN_FINALIZE_SAME_DESCRIPTOR"
        ),
        "authorityPreflightStatus": "EXACT_BEFORE_CLAIM",
        "replacementReceiptPath": "/tmp/replacement.receipt.json",
        "initialAggregatePath": "/tmp/initial-scorecard.json",
        "initialAggregateRawSha256": fixture_hash,
        "initialPanelSha256": fixture_hash,
        "initialEvaluationRunManifestSha256": fixture_hash,
        "replacementRequiredLanes": ["COLD-JOURNEY"],
        "candidateManifestSha256": fixture_hash,
        "qualificationReceiptSha256": fixture_hash,
        "evaluationRunManifestSha256": fixture_hash,
        "recipeId": "HOLDOUT-01",
        "rubricSha256": fixture_hash,
        "promptTemplateSha256": fixture_hash,
        "judgmentSchemaSha256": fixture_hash,
        "rawAggregationInputSha256": fixture_hash,
        "rawCandidateManifestSha256": fixture_hash,
        "rawQualificationReceiptSha256": fixture_hash,
        "rawEvaluationRunManifestSha256": fixture_hash,
        "panelAttempt": {
            "slotId": "PANEL",
            "path": "/tmp/panel.json",
            "readStatus": "READ",
            "rawSha256": fixture_hash,
            "attemptOutcome": "VALID",
            "failureCode": None,
        },
        "judgmentAttempts": duplicate_transport_attempts,
        "attemptOutcome": "TRANSPORT_FAILURE",
        "failureCode": "DUPLICATE_TRANSPORT_BODY",
        "parsedReplacementPanelSha256": fixture_hash,
        "parsedJudgeRunIds": [],
        "slotConsumed": True,
    }
    assert not module.instance_errors(
        identical_transport_receipt,
        replacement_receipt_schema,
    )
    unreadable_receipt = copy.deepcopy(identical_transport_receipt)
    unreadable_receipt["attemptOutcome"] = "INPUT_UNREADABLE"
    unreadable_receipt["failureCode"] = "JUDGE-02_INPUT_UNREADABLE"
    unreadable_receipt["judgmentAttempts"][1].update({
        "readStatus": "INPUT_UNREADABLE",
        "rawSha256": None,
        "attemptOutcome": "INPUT_UNREADABLE",
        "failureCode": "PATH_NOT_READABLE",
    })
    assert not module.instance_errors(unreadable_receipt, replacement_receipt_schema)
    wrong_slot_receipt = copy.deepcopy(unreadable_receipt)
    wrong_slot_receipt["judgmentAttempts"][1]["slotId"] = "JUDGE-01"
    assert module.instance_errors(wrong_slot_receipt, replacement_receipt_schema)
    unreadable_with_hash = copy.deepcopy(unreadable_receipt)
    unreadable_with_hash["judgmentAttempts"][1]["rawSha256"] = fixture_hash
    assert module.instance_errors(unreadable_with_hash, replacement_receipt_schema)
    wrong_top_precedence = copy.deepcopy(unreadable_receipt)
    wrong_top_precedence["attemptOutcome"] = "SCHEMA_FAILURE"
    assert module.instance_errors(wrong_top_precedence, replacement_receipt_schema)
    missing_unreadable_slot = copy.deepcopy(identical_transport_receipt)
    missing_unreadable_slot["attemptOutcome"] = "INPUT_UNREADABLE"
    assert module.instance_errors(missing_unreadable_slot, replacement_receipt_schema)

    retry_schema = run_schema["$defs"]["retry"]
    unreadable_retry = {
        "runSlot": "JUDGE-02",
        "role": "REPLACEMENT",
        "reason": "INPUT_UNREADABLE",
        "attempt": 2,
        "outcome": "BLOCKED",
        "readStatus": "INPUT_UNREADABLE",
        "rawArtifactSha256": None,
    }
    assert not module.instance_errors(unreadable_retry, retry_schema, run_schema)
    wrong_retry_slot = copy.deepcopy(unreadable_retry)
    wrong_retry_slot["runSlot"] = "replacement-panel"
    assert module.instance_errors(wrong_retry_slot, retry_schema, run_schema)
    read_retry_without_hash = copy.deepcopy(unreadable_retry)
    read_retry_without_hash.update({
        "reason": "TRANSPORT",
        "readStatus": "READ",
    })
    assert module.instance_errors(read_retry_without_hash, retry_schema, run_schema)
    checks += 1

    aggregate_fixtures = load_path_module(
        "gridworks_native_aggregate_contract_fixtures",
        ROOT.parent / "test-native-aggregate.py",
    )
    aggregation_input_schema = read_json(ROOT / "native-aggregation-input.schema.json")
    receipt_schema = replacement_receipt_schema
    with tempfile.TemporaryDirectory(prefix="gridworks-native-aggregate-contract-") as temporary:
        aggregate_root = Path(temporary)
        official_fixture = aggregate_fixtures.make_fixture(
            aggregate_root / "official",
            labeler=aggregate_fixtures.constant_label("EXCELLENT"),
        )
        assert not module.instance_errors(official_fixture["candidate"], aggregation_input_schema)
        official = aggregate_fixtures.aggregate_fixture(official_fixture)
        assert official["status"] == "PASS"
        assert not module.instance_errors(official, scorecard_schema)

        formative_fixture = aggregate_fixtures.make_fixture(
            aggregate_root / "formative",
            recipe_id="FORMATIVE-01",
            labeler=aggregate_fixtures.constant_label("EXCELLENT"),
        )
        formative = aggregate_fixtures.aggregate_fixture(formative_fixture)
        assert formative["status"] == "SCORED_FORMATIVE"
        assert formative["verdict"] is None
        assert formative["commercialUXProxy"] == 100.0
        assert not formative["officialCommercialUX"]
        assert not module.instance_errors(formative, scorecard_schema)

        def unstable_labeler(judge: int, kind: str, artifact: str, cell: str) -> str:
            if kind == "COLD_ACTOR" and artifact == "ARTIFACT-A" and cell == "J1":
                return ("EXCELLENT", "SERVICEABLE", "STRONG")[judge]
            return "STRONG"

        initial_fixture = aggregate_fixtures.make_fixture(
            aggregate_root / "initial",
            labeler=unstable_labeler,
            cold_suffix="initial",
            coverage_suffix="shared",
            panel_suffix="initial",
        )
        initial = aggregate_fixtures.aggregate_fixture(initial_fixture)
        assert initial["status"] == "RERUN_REQUIRED_COLD_INSTABILITY"
        assert not module.instance_errors(initial, scorecard_schema)
        replacement_fixture = aggregate_fixtures.make_fixture(
            aggregate_root / "replacement",
            labeler=aggregate_fixtures.constant_label("STRONG"),
            cold_suffix="replacement",
            coverage_suffix="shared",
            panel_suffix="replacement",
        )
        replacement = aggregate_fixtures.aggregate_fixture(
            replacement_fixture,
            replacement_for=initial_fixture["directory"] / "scorecard.json",
        )
        assert replacement["panelKind"] == "REPLACEMENT"
        assert not module.instance_errors(replacement, scorecard_schema)
        receipt = read_json(Path(replacement["replacementReceiptPath"]))
        assert not module.instance_errors(receipt, receipt_schema)
    checks += 1

    print(f"PASS native evaluator contract self-tests: {checks} scenarios")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
