#!/usr/bin/env python3
"""Deterministically validate the frozen Commercial UX native evaluator contract.

This tool validates candidate-independent authorities and schema/prompt bindings.  It
does not capture native evidence, call an LLM, or aggregate a candidate score.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import re
import subprocess
import sys
from collections import Counter
from pathlib import Path
from typing import Any


PROTOCOL = "GRIDWORKS-COMMERCIAL-UX-v1.1"
SHA_PATTERN = re.compile(r"sha256:[0-9a-f]{64}\Z")
LABELS = ["EXCELLENT", "STRONG", "SERVICEABLE", "WEAK", "BROKEN"]
EPISODES = [
    "E00-TITLE",
    "E01-FIRST-LIGHT",
    "E02-SECOND-HEART",
    "E03-SECOND-SOURCE",
    "E04-NORTH-BANK",
    "E05-WHOSE-MARGIN",
    "E06-FLOOD",
    "E07-MAINTENANCE",
    "E08-FINALE",
    "E09-MID-RESUME",
    "E10-COMPLETE-RESUME",
    "E11-AUTHORED-TEXT",
]
EXPECTED_PROBE_IDS = [
    "PX01-ROLE-START",
    "PX02-GOAL-NEXT-ACTION",
    "PX03-PLACEMENT-RECOVERY",
    "PX03-COLD-PLACEMENT-COMMIT",
    "PX04-LINE-DRAFT",
    "PX04-COLD-LINE-COMMIT",
    "PX05-SUPPLY-ENERGIZATION",
    "PX06-CONTINUOUS-LIMIT",
    "PX07-CORRIDOR-INDEPENDENCE",
    "PX08-SOURCE-BOTTLENECK",
    "PX09-TUTORIAL-WITHDRAWAL",
    "PX10-MUST-PROMISE",
    "PX11-RESULT-BOUNDARY",
    "PX12-EMERGENCY-SHUTDOWN",
    "PX13-FLOOD-DEADLINE-RECOVERY",
    "PX13-COLD-FLOOD-OUTCOME",
    "PX14-RESET-MAINTENANCE",
    "PX15-FINALE-PROMISE",
    "PX16-FINALE-PAYOFF",
    "PX17-MID-RESUME",
    "PX18-COMPLETE-RESUME",
    "PX19-ACCESSIBILITY",
    "PX20-AUDIOVISUAL",
    "PX21-COLD-KOREAN-STORY",
    "PX22-AUTHORED-KOREAN-STORY",
]
EXPECTED_COLD_PROBE_ORDER = [
    "PX01-ROLE-START",
    "PX21-COLD-KOREAN-STORY",
    "PX02-GOAL-NEXT-ACTION",
    "PX03-COLD-PLACEMENT-COMMIT",
    "PX04-COLD-LINE-COMMIT",
    "PX05-SUPPLY-ENERGIZATION",
    "PX06-CONTINUOUS-LIMIT",
    "PX07-CORRIDOR-INDEPENDENCE",
    "PX08-SOURCE-BOTTLENECK",
    "PX09-TUTORIAL-WITHDRAWAL",
    "PX10-MUST-PROMISE",
    "PX17-MID-RESUME",
    "PX11-RESULT-BOUNDARY",
    "PX12-EMERGENCY-SHUTDOWN",
    "PX13-COLD-FLOOD-OUTCOME",
    "PX14-RESET-MAINTENANCE",
    "PX15-FINALE-PROMISE",
    "PX16-FINALE-PAYOFF",
]
HARD_GATES = [
    "HG01-AUTHORITY",
    "HG02-STORY",
    "HG03-REACHABILITY",
    "HG04-BUILD",
    "HG05-TYPED-DISPLAY",
    "HG06-VIEWPORT",
    "HG07-KEYBOARD",
    "HG08-NONCOLOR",
    "HG09-AUDIO",
    "HG10-REPLAY",
    "HG11-LIVENESS",
    "HG12-RAW-ID",
    "HG13-PROVENANCE",
]

# category: (weight, minimum, ordered {cell: (weight, lane tuple)})
EXPECTED_CATEGORIES: dict[str, tuple[int, int, dict[str, tuple[int, tuple[str, ...]]]]] = {
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
        cell: (25, ("COLD-JOURNEY", "COVERAGE-JOURNEY"))
        for cell in ("H1", "H2", "H3", "H4")
    }),
    "feedback": (12, 85, {
        "I1": (35, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
        "I2": (30, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
        "I3": (35, ("COLD-JOURNEY", "COVERAGE-JOURNEY")),
    }),
    "causality": (13, 85, {
        cell: (20, ("COLD-JOURNEY", "COVERAGE-JOURNEY"))
        for cell in ("C1", "C2", "C3", "C4", "C5")
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
        cell: (25, ("COVERAGE-JOURNEY",)) for cell in ("V1", "V2", "V3", "V4")
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
ALL_CELLS = [cell for _, _, cells in EXPECTED_CATEGORIES.values() for cell in cells]
COLD_CELLS = [
    cell
    for _, _, cells in EXPECTED_CATEGORIES.values()
    for cell, (_, lanes) in cells.items()
    if "COLD-JOURNEY" in lanes
]
COVERAGE_CELLS = [
    cell
    for _, _, cells in EXPECTED_CATEGORIES.values()
    for cell, (_, lanes) in cells.items()
    if "COVERAGE-JOURNEY" in lanes
]


class ContractError(ValueError):
    pass


def duplicate_rejector(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ContractError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def read_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(
            path.read_text(encoding="utf-8"),
            object_pairs_hook=duplicate_rejector,
        )
    except (OSError, UnicodeError, json.JSONDecodeError, ContractError) as error:
        raise ContractError(f"cannot read strict JSON {path}: {error}") from error
    if not isinstance(value, dict):
        raise ContractError(f"{path} must contain a JSON object")
    return value


def read_json_bytes(data: bytes, label: str) -> dict[str, Any]:
    """Parse one already-opened runtime artifact without reopening its path."""
    try:
        value = json.loads(
            data,
            object_pairs_hook=duplicate_rejector,
        )
    except (UnicodeError, json.JSONDecodeError, ContractError) as error:
        raise ContractError(f"cannot read strict JSON {label}: {error}") from error
    if not isinstance(value, dict):
        raise ContractError(f"{label} must contain a JSON object")
    return value


def bytes_sha256(data: bytes) -> str:
    return "sha256:" + hashlib.sha256(data).hexdigest()


def raw_sha256(path: Path) -> str:
    return bytes_sha256(path.read_bytes())


def canonical_json_bytes(value: Any) -> bytes:
    """JCS-compatible encoding for the contract's integer/string/bool/null payloads."""
    return json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def self_hash(value: dict[str, Any], field: str) -> str:
    payload = copy.deepcopy(value)
    if field not in payload:
        raise ContractError(f"self-hash field absent: {field}")
    payload[field] = None
    return "sha256:" + hashlib.sha256(canonical_json_bytes(payload)).hexdigest()


def validate_qualification_receipt_semantics(
    receipt: dict[str, Any],
    errors: list[str],
) -> None:
    """Validate cross-item qualification invariants JSON Schema cannot express."""
    attempts = receipt.get("attempts")
    if not isinstance(attempts, list) or not 1 <= len(attempts) <= 2:
        errors.append("qualification receipt must contain one or two attempts")
        return
    run_ids: list[str] = []
    raw_hashes: list[str] = []
    for attempt_index, attempt in enumerate(attempts, start=1):
        if not isinstance(attempt, dict):
            errors.append(f"qualification attempt {attempt_index} must be an object")
            continue
        slots = attempt.get("slots")
        if not isinstance(slots, list) or len(slots) != 3:
            errors.append(f"qualification attempt {attempt_index} must contain three slots")
            continue
        slot_passes: list[bool] = []
        for slot_index, slot in enumerate(slots, start=1):
            if not isinstance(slot, dict):
                errors.append(
                    f"qualification attempt {attempt_index} slot {slot_index} must be an object"
                )
                continue
            run_id = slot.get("judgeRunId")
            raw_hash = slot.get("judgmentRawSha256")
            if isinstance(run_id, str):
                run_ids.append(run_id)
            if isinstance(raw_hash, str):
                raw_hashes.append(raw_hash)
            band_passes = (
                isinstance(slot.get("exactCount"), int)
                and slot["exactCount"] >= 19
                and slot.get("excellentAndBrokenAllExact") is True
                and slot.get("schemaValidCount") == 20
            )
            status = slot.get("status")
            if status == "PASS" and not band_passes:
                errors.append("qualification PASS slot violates the frozen pass predicate")
            if status == "FAIL_SCHEMA" and not (
                isinstance(slot.get("schemaValidCount"), int)
                and slot["schemaValidCount"] < 20
            ):
                errors.append("qualification FAIL_SCHEMA slot has a valid schema count")
            if status == "FAIL_BAND" and (
                slot.get("schemaValidCount") != 20 or band_passes
            ):
                errors.append("qualification FAIL_BAND slot violates its predicate")
            slot_passes.append(status == "PASS")
        expected_status = "PASS" if len(slot_passes) == 3 and all(slot_passes) else (
            "INVALIDATED" if attempt_index == 1 and len(attempts) == 2 else "FAIL"
        )
        if attempt.get("status") != expected_status:
            errors.append(
                f"qualification attempt {attempt_index} status must be {expected_status}"
            )
    if len(run_ids) != len(set(run_ids)):
        errors.append("qualification judgeRunId values must be disjoint across all attempts")
    if len(raw_hashes) != len(set(raw_hashes)):
        errors.append(
            "qualification judgmentRawSha256 values must be disjoint across all attempts"
        )
    final_attempt_status = attempts[-1].get("status") if isinstance(attempts[-1], dict) else None
    expected_receipt_status = (
        "PASS" if final_attempt_status == "PASS" else "BLOCKED_JUDGE_QUALIFICATION"
    )
    if receipt.get("status") != expected_receipt_status:
        errors.append(
            f"qualification receipt status must be {expected_receipt_status}"
        )


def validate_actor_observation_semantics(
    observation: dict[str, Any],
    errors: list[str],
) -> None:
    """Validate actor ledger ordering and references that JSON Schema cannot compare."""
    actions = observation.get("actionLedger")
    if not isinstance(actions, list):
        errors.append("actor actionLedger must be an array")
        return
    action_indexes = [
        row.get("actionIndex")
        for row in actions
        if isinstance(row, dict)
    ]
    expected_indexes = list(range(1, len(actions) + 1))
    if action_indexes != expected_indexes:
        errors.append(
            "actor actionIndex values must be exact, unique, and strictly increasing from 1"
        )
    checkpoints = observation.get("checkpoints")
    checkpoint_rows = checkpoints if isinstance(checkpoints, list) else []
    checkpoint_ordinals = [
        row.get("ordinal")
        for row in checkpoint_rows
        if isinstance(row, dict)
    ]
    if checkpoint_ordinals != list(range(1, len(checkpoint_rows) + 1)):
        errors.append(
            "actor checkpoint ordinals must be exact, unique, and strictly increasing from 1"
        )
    recipe_checkpoint_ordinals = [
        row.get("recipeCheckpointSequenceOrdinal")
        for row in checkpoint_rows
        if isinstance(row, dict)
    ]
    if not all(
        isinstance(value, int) and not isinstance(value, bool)
        for value in recipe_checkpoint_ordinals
    ) or any(
        right <= left
        for left, right in zip(recipe_checkpoint_ordinals, recipe_checkpoint_ordinals[1:])
    ):
        errors.append("actor recipe checkpoint sequence ordinals must be strictly increasing")
    checkpoint_by_ordinal = {
        row.get("ordinal"): row
        for row in checkpoint_rows
        if isinstance(row, dict) and isinstance(row.get("ordinal"), int)
    }
    action_index_set = {
        index for index in action_indexes if isinstance(index, int) and not isinstance(index, bool)
    }
    first_use_rows = observation.get("firstUseRecords")
    if isinstance(first_use_rows, list):
        first_use_ordinals = [
            row.get("firstUseOrdinal") for row in first_use_rows if isinstance(row, dict)
        ]
        if first_use_ordinals != list(range(1, len(first_use_rows) + 1)):
            errors.append("actor firstUseOrdinal values must be contiguous from 1")
    approval_rows = observation.get("approvalRecords")
    if isinstance(approval_rows, list):
        approval_ordinals = [
            row.get("approvalOrdinal") for row in approval_rows if isinstance(row, dict)
        ]
        if approval_ordinals != list(range(1, len(approval_rows) + 1)):
            errors.append("actor approvalOrdinal values must be contiguous from 1")
    incidents = observation.get("incidents")
    incident_rows = incidents if isinstance(incidents, list) else []
    incident_keys = [
        row.get("incidentKey")
        for row in incident_rows
        if isinstance(row, dict)
    ]
    if len(incident_keys) != len(set(incident_keys)):
        errors.append("actor incidentKey values must be unique")
    incident_ordinals = [
        row.get("incidentOrdinal") for row in incident_rows if isinstance(row, dict)
    ]
    if incident_ordinals != list(range(1, len(incident_rows) + 1)):
        errors.append("actor incidentOrdinal values must be contiguous from 1")
    for index, incident in enumerate(incident_rows):
        if not isinstance(incident, dict):
            continue
        cited_actions = incident.get("actionIndexes")
        if isinstance(cited_actions, list):
            if len(cited_actions) != len(set(cited_actions)):
                errors.append(f"actor incident {index} actionIndexes contain duplicates")
            if not set(cited_actions).issubset(action_index_set):
                errors.append(f"actor incident {index} cites an unknown actionIndex")
        cited_checkpoints = incident.get("checkpointOrdinals")
        if isinstance(cited_checkpoints, list):
            if len(cited_checkpoints) != len(set(cited_checkpoints)):
                errors.append(f"actor incident {index} checkpointOrdinals contain duplicates")
            if not set(cited_checkpoints).issubset(checkpoint_by_ordinal):
                errors.append(f"actor incident {index} cites an unknown checkpoint ordinal")
    terminal_key = observation.get("terminalIncidentKey")
    terminal_ordinal = observation.get("terminalIncidentOrdinal")
    if terminal_key is not None and terminal_key not in incident_keys:
        errors.append("actor terminalIncidentKey must name one of its incidents")
    if terminal_ordinal is not None:
        terminal_rows = [
            row for row in incident_rows
            if isinstance(row, dict) and row.get("incidentOrdinal") == terminal_ordinal
        ]
        if len(terminal_rows) != 1 or terminal_rows[0].get("incidentKey") != terminal_key:
            errors.append("actor terminal incident ordinal/key linkage mismatch")
    terminal_state = observation.get("terminalState")
    if terminal_state == "COMPLETED" and (terminal_key is not None or terminal_ordinal is not None):
        errors.append("actor COMPLETED terminal state requires null terminal incident linkage")
    if terminal_state in {"PLAYER_STALLED", "HARNESS_BLOCKED"} and (
        terminal_key is None or terminal_ordinal is None
    ):
        errors.append(f"actor {terminal_state} terminal state requires an incident key and ordinal")


def validate_candidate_manifest_semantics(
    candidate: dict[str, Any],
    native: Path,
    rubric_path: Path,
    errors: list[str],
) -> None:
    """Recompute every candidate-independent hash bound by the pre-capture identity."""
    contract_paths = {
        "contractBindingsSha256": native / "contract-bindings.json",
        "canonicalHashPolicySha256": native / "canonical-hash-policy.json",
        "nativeAggregatorSha256": native.parent / "aggregate-native.py",
        "rubricSha256": rubric_path,
        "coldActorPromptSha256": native / "cold-actor-prompt.template.txt",
        "coldActorResponseSchemaSha256": native / "cold-actor-response.schema.json",
        "actorActionLedgerSchemaSha256": native / "actor-action-ledger.schema.json",
        "actorObservationSchemaSha256": native / "actor-observation.schema.json",
        "actorTraceSchemaSha256": native / "actor-trace.schema.json",
        "coverageTraceSchemaSha256": native / "coverage-trace.schema.json",
        "evidenceSetSchemaSha256": native / "evidence-set.schema.json",
        "nativeJudgePromptSha256": native / "native-judge-prompt.template.txt",
        "nativeJudgeSchemaSha256": native / "native-judge.schema.json",
        "judgePanelSchemaSha256": native / "judge-panel.schema.json",
        "qualificationInputSchemaSha256": native / "qualification-input.schema.json",
        "qualificationReceiptSchemaSha256": native / "qualification-receipt.schema.json",
        "nativeVerifierPromptSha256": native / "native-evidence-verifier-prompt.template.txt",
        "nativeVerifierInputSchemaSha256": native / "native-evidence-verification-input.schema.json",
        "nativeVerifierSchemaSha256": native / "native-evidence-verifier.schema.json",
        "oracleHardGateSchemaSha256": native / "oracle-hard-gate-ledger.schema.json",
        "nativeAggregationInputSchemaSha256": native / "native-aggregation-input.schema.json",
        "nativeScorecardSchemaSha256": native / "native-scorecard.schema.json",
        "evaluationRunManifestSchemaSha256": native / "evaluation-run-manifest.schema.json",
        "nativeReplacementReceiptSchemaSha256": native / "native-replacement-receipt.schema.json",
    }
    contract_hashes = candidate.get("contractHashes")
    if not isinstance(contract_hashes, dict):
        errors.append("candidate contractHashes must be an object")
    else:
        for field, path in contract_paths.items():
            if not path.is_file():
                errors.append(f"candidate contract authority is missing: {path}")
            elif contract_hashes.get(field) != raw_sha256(path):
                errors.append(f"candidate contractHashes.{field} raw SHA mismatch")

    recipe_paths = {
        "coldJourneySha256": native / "cold-journey-recipe.json",
        "coverageSha256": native / "coverage-recipe.json",
        "holdoutQueueSha256": native / "holdout-recipes.json",
        "conceptExposureSha256": native / "concept-exposure-manifest.json",
        "goldStateContractSha256": native / "gold-state-manifest.json",
        "qualificationAnchorsSha256": native / "qualification-anchors.json",
    }
    recipes = candidate.get("recipes")
    if not isinstance(recipes, dict):
        errors.append("candidate recipes must be an object")
        return
    for field, path in recipe_paths.items():
        if recipes.get(field) != raw_sha256(path):
            errors.append(f"candidate recipes.{field} raw SHA mismatch")
    try:
        queue = read_json(native / "holdout-recipes.json")
    except ContractError as error:
        errors.append(str(error))
        return
    selected_id = recipes.get("selectedRecipeId")
    rows = [queue.get("formative"), *queue.get("holdouts", [])]
    matches = [
        row for row in rows
        if isinstance(row, dict) and row.get("id") == selected_id
    ]
    if len(matches) != 1:
        errors.append("candidate selected recipe is absent or duplicated")
    else:
        selected_hash = "sha256:" + hashlib.sha256(
            canonical_json_bytes(matches[0])
        ).hexdigest()
        if recipes.get("selectedRecipeSha256") != selected_hash:
            errors.append("candidate selectedRecipeSha256 projection mismatch")

    execution = candidate.get("execution")
    if not isinstance(execution, dict):
        errors.append("candidate execution must be an object")
        return
    component_fields = (
        ("godotExecutablePath", "godotExecutableSha256"),
        ("managedAssemblyPath", "managedAssemblySha256"),
        ("pckResourceManifestPath", "pckResourceManifestSha256"),
    )
    if execution.get("packagePath") is not None:
        component_fields = (*component_fields, ("packagePath", "packageSha256"))
    for path_field, hash_field in component_fields:
        raw_path = execution.get(path_field)
        if not isinstance(raw_path, str):
            errors.append(f"candidate execution.{path_field} must be an absolute path")
            continue
        path = Path(raw_path)
        try:
            resolved = path.resolve(strict=True)
        except OSError as error:
            errors.append(f"candidate execution.{path_field} cannot be opened: {error}")
            continue
        require(
            path.is_absolute() and raw_path == str(resolved) and resolved.is_file(),
            f"candidate execution.{path_field} must be a canonical regular file without symlinks",
            errors,
        )
        if resolved.is_file():
            require(
                execution.get(hash_field) == raw_sha256(resolved),
                f"candidate execution.{hash_field} raw SHA mismatch",
                errors,
            )
    execution_projection = {
        "godotExecutableSha256": execution.get("godotExecutableSha256"),
        "managedAssemblySha256": execution.get("managedAssemblySha256"),
        "pckResourceManifestSha256": execution.get("pckResourceManifestSha256"),
        "packageSha256": execution.get("packageSha256"),
        "packageStatus": execution.get("packageStatus"),
    }
    require(
        execution.get("executionArtifactSha256")
        == "sha256:" + hashlib.sha256(
            canonical_json_bytes(execution_projection)
        ).hexdigest(),
        "candidate executionArtifactSha256 canonical component projection mismatch",
        errors,
    )


def candidate_reuse_sha256(candidate: dict[str, Any]) -> str:
    source = candidate.get("source", {})
    execution = candidate.get("execution", {})
    projection = {
        "authorityHashes": candidate.get("authorityHashes"),
        "executionArtifactSha256": execution.get("executionArtifactSha256"),
        "sourceCommit": source.get("commit"),
    }
    return "sha256:" + hashlib.sha256(canonical_json_bytes(projection)).hexdigest()


def selected_recipe_projection(row: dict[str, Any]) -> dict[str, Any]:
    presentation_ids = (
        EPISODES
        if row.get("coverageArtifactOrder") == "EPISODE_ASCENDING"
        else list(reversed(EPISODES))
    )
    return {
        "recipeId": row.get("id"),
        "ordinal": row.get("ordinal"),
        "selectedRecipeSha256": "sha256:" + hashlib.sha256(
            canonical_json_bytes(row)
        ).hexdigest(),
        "missionPrototypeBits": row.get("missionPrototypeBits"),
        "routeFamily": row.get("routeFamily"),
        "promiseBranchOrder": row.get("promiseBranchOrder"),
        "actorArtifactPermutation": row.get("actorArtifactPermutation"),
        "coverageArtifactOrder": row.get("coverageArtifactOrder"),
        "coveragePresentationEpisodeIds": presentation_ids,
    }


def canonical_holdout_registry_path(native: Path) -> Path:
    repository_root = native.parents[2]
    try:
        common_dir = subprocess.run(
            ["git", "rev-parse", "--git-common-dir"],
            cwd=repository_root,
            check=True,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=10,
        ).stdout.strip()
    except (OSError, subprocess.SubprocessError) as error:
        raise ContractError(
            f"cannot resolve repository-local holdout registry authority: {error}"
        ) from error
    common_path = Path(common_dir)
    if not common_path.is_absolute():
        common_path = repository_root / common_path
    return (
        common_path.resolve(strict=False)
        / "gridworks-commercial-ux"
        / "holdout-consumption-registry-v1.json"
    )


def holdout_claim_transaction_sha256(
    receipt: dict[str, Any],
    registry_before: dict[str, Any],
) -> str:
    selected = receipt.get("selectedRecipe", {})
    projection = {
        "queueAuthorityId": receipt.get("queueAuthorityId"),
        "registryBeforeSha256": registry_before.get(
            "holdoutConsumptionRegistrySha256"
        ),
        "candidateReuseSha256": receipt.get("candidateReuseSha256"),
        "candidateManifestSha256": receipt.get("candidateManifestSha256"),
        "selectedRecipeSha256": (
            selected.get("selectedRecipeSha256")
            if isinstance(selected, dict)
            else None
        ),
        "ordinal": selected.get("ordinal") if isinstance(selected, dict) else None,
    }
    return "sha256:" + hashlib.sha256(canonical_json_bytes(projection)).hexdigest()


def canonical_holdout_receipt_path(
    native: Path,
    transaction_id: str,
    candidate_reuse_sha256: str,
) -> Path:
    if (
        SHA_PATTERN.fullmatch(transaction_id) is None
        or SHA_PATTERN.fullmatch(candidate_reuse_sha256) is None
    ):
        raise ContractError("holdout receipt path requires canonical SHA-256 claim keys")
    return (
        canonical_holdout_registry_path(native).parent
        / "holdout-receipts"
        / (
            transaction_id.removeprefix("sha256:")
            + "-"
            + candidate_reuse_sha256.removeprefix("sha256:")
            + ".json"
        )
    )


def validate_registry_semantics(
    registry: dict[str, Any],
    queue: dict[str, Any],
    label: str,
    errors: list[str],
) -> None:
    rows = registry.get("consumptions")
    if not isinstance(rows, list):
        errors.append(f"{label} consumptions must be an array")
        return
    ordinals = [row.get("ordinal") for row in rows if isinstance(row, dict)]
    recipe_ids = [row.get("recipeId") for row in rows if isinstance(row, dict)]
    candidate_keys = [
        row.get("candidateReuseSha256") for row in rows if isinstance(row, dict)
    ]
    require(
        registry.get("revision") == len(rows),
        f"{label} revision must equal consumption count",
        errors,
    )
    require(
        ordinals == list(range(1, len(rows) + 1)),
        f"{label} ordinals must be the contiguous consumed prefix",
        errors,
    )
    require(
        recipe_ids == [f"HOLDOUT-{ordinal:02d}" for ordinal in ordinals],
        f"{label} recipe IDs must match their ordinals",
        errors,
    )
    require(
        len(candidate_keys) == len(set(candidate_keys)),
        f"{label} candidateReuseSha256 values must be unique",
        errors,
    )
    require(
        registry.get("queueAuthorityId") == queue.get("queueAuthorityId")
        and registry.get("holdoutQueueSha256")
        == queue.get("_observedRawSha256")
        and registry.get("registryScope")
        == queue.get("registryPolicy", {}).get("scope")
        and registry.get("registryAuthorityLimit")
        == queue.get("registryPolicy", {}).get("authorityLimit")
        and registry.get("registryPathRule")
        == queue.get("registryPolicy", {}).get("registryPathRule"),
        f"{label} queue authority binding mismatch",
        errors,
    )
    transaction_ids = [
        row.get("transactionId") for row in rows if isinstance(row, dict)
    ]
    require(
        len(transaction_ids) == len(set(transaction_ids))
        and all(
            isinstance(value, str) and SHA_PATTERN.fullmatch(value) is not None
            for value in transaction_ids
        ),
        f"{label} transactionId values must be unique canonical SHA-256 claims",
        errors,
    )


def validate_holdout_consumption_semantics(
    receipt: dict[str, Any],
    receipt_path: Path,
    native: Path,
    candidate: dict[str, Any] | None,
    queue: dict[str, Any],
    registry_before: dict[str, Any] | None,
    registry_after: dict[str, Any] | None,
    errors: list[str],
) -> None:
    atomic = receipt.get("atomicClaim", {})
    canonical_receipt_path = str(receipt_path.resolve())
    require(
        isinstance(atomic, dict)
        and atomic.get("receiptInputPath") == canonical_receipt_path
        and atomic.get("canonicalReceiptPath") == canonical_receipt_path,
        "holdout receipt embedded receipt paths must equal the exact canonical input path",
        errors,
    )
    require(
        receipt.get("holdoutConsumptionReceiptSchemaSha256")
        == raw_sha256(native / "holdout-consumption-receipt.schema.json"),
        "holdout receipt schema raw SHA mismatch",
        errors,
    )
    require(
        receipt.get("holdoutQueueSha256") == queue.get("_observedRawSha256")
        and receipt.get("queueAuthorityId") == queue.get("queueAuthorityId"),
        "holdout receipt queue authority mismatch",
        errors,
    )
    selected = receipt.get("selectedRecipe")
    queue_rows = [queue.get("formative"), *queue.get("holdouts", [])]
    matches = [
        row for row in queue_rows
        if isinstance(row, dict)
        and isinstance(selected, dict)
        and row.get("id") == selected.get("recipeId")
    ]
    if len(matches) != 1:
        errors.append("holdout receipt selected recipe is absent or duplicated")
    else:
        require(
            selected == selected_recipe_projection(matches[0]),
            "holdout receipt selectedRecipe exact projection mismatch",
            errors,
        )
    if candidate is not None:
        require(
            receipt.get("candidateId") == candidate.get("candidateId")
            and receipt.get("candidateManifestSha256")
            == candidate.get("candidateManifestSha256")
            and receipt.get("sourceCommit") == candidate.get("source", {}).get("commit")
            and receipt.get("candidateReuseSha256") == candidate_reuse_sha256(candidate),
            "holdout receipt candidate binding mismatch",
            errors,
        )
        recipes = candidate.get("recipes", {})
        if isinstance(selected, dict):
            require(
                selected.get("recipeId") == recipes.get("selectedRecipeId")
                and selected.get("selectedRecipeSha256")
                == recipes.get("selectedRecipeSha256"),
                "holdout receipt selected recipe does not match candidate manifest",
                errors,
            )
    if registry_before is None or registry_after is None:
        errors.append("holdout receipt validation requires registry before and after envelopes")
        return
    validate_registry_semantics(registry_before, queue, "registry before", errors)
    validate_registry_semantics(registry_after, queue, "registry after", errors)
    before_rows = registry_before.get("consumptions", [])
    after_rows = registry_after.get("consumptions", [])
    require(
        receipt.get("priorConsumptions") == before_rows,
        "holdout receipt priorConsumptions must equal registry-before rows",
        errors,
    )
    if isinstance(atomic, dict):
        require(
            atomic.get("priorConsumptionSetSha256")
            == "sha256:" + hashlib.sha256(
                canonical_json_bytes(before_rows)
            ).hexdigest(),
            "holdout receipt priorConsumptionSetSha256 projection mismatch",
            errors,
        )
    if isinstance(atomic, dict):
        try:
            expected_registry_path = str(canonical_holdout_registry_path(native))
            expected_receipt_path = str(
                canonical_holdout_receipt_path(
                    native,
                    atomic.get("transactionId"),
                    receipt.get("candidateReuseSha256"),
                )
            )
        except ContractError as error:
            errors.append(str(error))
        else:
            require(
                atomic.get("canonicalRegistryPath")
                == registry_before.get("canonicalRegistryPath")
                == registry_after.get("canonicalRegistryPath")
                == atomic.get("registryInputPath")
                == expected_registry_path,
                "holdout registry canonical/input path must equal the git-common-dir singleton",
                errors,
            )
            require(
                atomic.get("receiptInputPath")
                == atomic.get("canonicalReceiptPath")
                == canonical_receipt_path
                == expected_receipt_path,
                "holdout receipt path must be the transaction/candidate-derived git-common-dir singleton",
                errors,
            )
        require(
            atomic.get("transactionId")
            == holdout_claim_transaction_sha256(receipt, registry_before),
            "holdout transactionId must be the exact canonical claim projection",
            errors,
        )
    if receipt.get("evaluationPhase") == "FORMATIVE":
        require(
            after_rows == before_rows,
            "formative receipt must not consume the official holdout registry",
            errors,
        )
    elif isinstance(selected, dict):
        selected_ordinal = selected.get("ordinal")
        require(
            selected_ordinal == len(before_rows) + 1,
            "holdout receipt must select the lowest unused ordinal",
            errors,
        )
        expected_append = {
            "ordinal": selected_ordinal,
            "recipeId": selected.get("recipeId"),
            "candidateReuseSha256": receipt.get("candidateReuseSha256"),
            "candidateManifestSha256": receipt.get("candidateManifestSha256"),
            "sourceCommit": receipt.get("sourceCommit"),
            "transactionId": atomic.get("transactionId") if isinstance(atomic, dict) else None,
        }
        require(
            after_rows == [*before_rows, expected_append],
            "holdout registry-after must be exactly registry-before plus one atomic consumption",
            errors,
        )
        require(
            receipt.get("candidateReuseSha256")
            not in {
                row.get("candidateReuseSha256")
                for row in before_rows if isinstance(row, dict)
            },
            "candidateReuseSha256 has already consumed a holdout ordinal",
            errors,
        )


def require(condition: bool, message: str, errors: list[str]) -> None:
    if not condition:
        errors.append(message)


def exact_keys(value: Any, expected: set[str], label: str, errors: list[str]) -> None:
    require(isinstance(value, dict), f"{label} must be an object", errors)
    if isinstance(value, dict):
        actual = set(value)
        require(actual == expected, f"{label} keys mismatch: {sorted(actual ^ expected)}", errors)


def unique_strings(value: Any, label: str, errors: list[str]) -> list[str]:
    require(isinstance(value, list), f"{label} must be an array", errors)
    if not isinstance(value, list):
        return []
    require(all(isinstance(item, str) for item in value), f"{label} must contain strings", errors)
    strings = [item for item in value if isinstance(item, str)]
    require(len(strings) == len(set(strings)), f"{label} contains duplicates", errors)
    return strings


def schema_type_matches(instance: Any, expected: str) -> bool:
    if expected == "object":
        return isinstance(instance, dict)
    if expected == "array":
        return isinstance(instance, list)
    if expected == "string":
        return isinstance(instance, str)
    if expected == "integer":
        return isinstance(instance, int) and not isinstance(instance, bool)
    if expected == "number":
        return isinstance(instance, (int, float)) and not isinstance(instance, bool) and math.isfinite(instance)
    if expected == "boolean":
        return isinstance(instance, bool)
    if expected == "null":
        return instance is None
    return False


def resolve_local_ref(root: dict[str, Any], reference: str) -> dict[str, Any]:
    if not reference.startswith("#/"):
        raise ContractError(f"external $ref is not supported by the isolated validator: {reference}")
    current: Any = root
    for token in reference[2:].split("/"):
        token = token.replace("~1", "/").replace("~0", "~")
        if not isinstance(current, dict) or token not in current:
            raise ContractError(f"unresolved local $ref: {reference}")
        current = current[token]
    if not isinstance(current, dict):
        raise ContractError(f"$ref does not resolve to a schema object: {reference}")
    return current


def instance_errors(
    instance: Any,
    schema: dict[str, Any],
    root: dict[str, Any] | None = None,
    path: str = "$",
) -> list[str]:
    root = root or schema
    errors: list[str] = []
    if "$ref" in schema:
        try:
            target = resolve_local_ref(root, schema["$ref"])
        except ContractError as error:
            return [f"{path}: {error}"]
        errors.extend(instance_errors(instance, target, root, path))

    if "allOf" in schema:
        for index, branch in enumerate(schema["allOf"]):
            errors.extend(instance_errors(instance, branch, root, f"{path}.allOf[{index}]"))
    if "anyOf" in schema:
        branch_errors = [instance_errors(instance, branch, root, path) for branch in schema["anyOf"]]
        if not any(not branch for branch in branch_errors):
            errors.append(f"{path}: no anyOf branch matched")
    if "oneOf" in schema:
        matches = sum(
            not instance_errors(instance, branch, root, path)
            for branch in schema["oneOf"]
        )
        if matches != 1:
            errors.append(f"{path}: expected exactly one oneOf match, got {matches}")
    if "if" in schema:
        condition_matches = not instance_errors(instance, schema["if"], root, path)
        selected = schema.get("then") if condition_matches else schema.get("else")
        if isinstance(selected, dict):
            errors.extend(instance_errors(instance, selected, root, path))

    expected_type = schema.get("type")
    if expected_type is not None:
        choices = expected_type if isinstance(expected_type, list) else [expected_type]
        if not any(schema_type_matches(instance, choice) for choice in choices):
            errors.append(f"{path}: type mismatch, expected {choices}")
            return errors
    if "const" in schema and instance != schema["const"]:
        errors.append(f"{path}: const mismatch")
    if "enum" in schema and instance not in schema["enum"]:
        errors.append(f"{path}: value is outside enum")

    if isinstance(instance, str):
        if "minLength" in schema and len(instance) < schema["minLength"]:
            errors.append(f"{path}: shorter than minLength")
        if "maxLength" in schema and len(instance) > schema["maxLength"]:
            errors.append(f"{path}: longer than maxLength")
        if "pattern" in schema and re.fullmatch(schema["pattern"], instance) is None:
            errors.append(f"{path}: pattern mismatch")
    if isinstance(instance, (int, float)) and not isinstance(instance, bool):
        if "minimum" in schema and instance < schema["minimum"]:
            errors.append(f"{path}: below minimum")
        if "maximum" in schema and instance > schema["maximum"]:
            errors.append(f"{path}: above maximum")

    if isinstance(instance, dict):
        required = schema.get("required", [])
        for key in required:
            if key not in instance:
                errors.append(f"{path}: missing required property {key}")
        properties = schema.get("properties", {})
        for key, value in instance.items():
            if key in properties:
                errors.extend(instance_errors(value, properties[key], root, f"{path}.{key}"))
            elif schema.get("additionalProperties") is False:
                errors.append(f"{path}: additional property {key}")
        if "minProperties" in schema and len(instance) < schema["minProperties"]:
            errors.append(f"{path}: fewer than minProperties")
        property_names = schema.get("propertyNames")
        if isinstance(property_names, dict):
            for key in instance:
                errors.extend(instance_errors(key, property_names, root, f"{path}.<propertyName>"))

    if isinstance(instance, list):
        if "minItems" in schema and len(instance) < schema["minItems"]:
            errors.append(f"{path}: fewer than minItems")
        if "maxItems" in schema and len(instance) > schema["maxItems"]:
            errors.append(f"{path}: more than maxItems")
        if schema.get("uniqueItems"):
            encoded = [canonical_json_bytes(item) for item in instance]
            if len(encoded) != len(set(encoded)):
                errors.append(f"{path}: duplicate array items")
        prefix = schema.get("prefixItems", [])
        for index, item_schema in enumerate(prefix[: len(instance)]):
            errors.extend(instance_errors(instance[index], item_schema, root, f"{path}[{index}]"))
        items = schema.get("items")
        if items is False and len(instance) > len(prefix):
            errors.append(f"{path}: items beyond prefixItems are forbidden")
        elif isinstance(items, dict):
            start = len(prefix) if prefix else 0
            for index in range(start, len(instance)):
                errors.extend(instance_errors(instance[index], items, root, f"{path}[{index}]"))
        if "contains" in schema:
            match_count = sum(
                not instance_errors(item, schema["contains"], root, f"{path}[{index}]")
                for index, item in enumerate(instance)
            )
            minimum = schema.get("minContains", 1)
            maximum = schema.get("maxContains")
            if match_count < minimum or (maximum is not None and match_count > maximum):
                errors.append(f"{path}: contains count {match_count} outside [{minimum}, {maximum}]")
    return errors


def audit_schema(value: Any, label: str, errors: list[str], root: dict[str, Any]) -> None:
    if isinstance(value, dict):
        if value.get("type") == "object" and "properties" in value:
            require(
                value.get("additionalProperties") is False,
                f"{label}: object schema must set additionalProperties=false",
                errors,
            )
            required = value.get("required", [])
            require(isinstance(required, list), f"{label}.required must be an array", errors)
            if isinstance(required, list):
                require(
                    set(required).issubset(value["properties"]),
                    f"{label}: required contains an undeclared property",
                    errors,
                )
        reference = value.get("$ref")
        if isinstance(reference, str):
            if reference.startswith("#/"):
                try:
                    resolve_local_ref(root, reference)
                except ContractError as error:
                    errors.append(f"{label}: {error}")
            else:
                require(
                    "/" not in reference and reference.endswith(".schema.json"),
                    f"{label}: external $ref must be a sibling schema filename",
                    errors,
                )
        for key, child in value.items():
            audit_schema(child, f"{label}.{key}", errors, root)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            audit_schema(child, f"{label}[{index}]", errors, root)


def validate_rubric(rubric: dict[str, Any], errors: list[str]) -> None:
    require(rubric.get("schemaVersion") == "gridworks.commercial-ux.rubric.v1", "rubric schemaVersion drift", errors)
    require(rubric.get("protocol") == "GRIDWORKS-COMMERCIAL-UX-v1", "rubric protocol drift", errors)
    judge = rubric.get("judge", {})
    require(
        judge == {"model": "gpt-5.6-sol", "reasoningEffort": "ultra", "slot": "SOL-ULTRA"},
        "rubric judge identity drift",
        errors,
    )
    expected_labels = {
        "EXCELLENT": (4, 100),
        "STRONG": (3, 85),
        "SERVICEABLE": (2, 70),
        "WEAK": (1, 40),
        "BROKEN": (0, 0),
    }
    actual_labels = {
        row.get("id"): (row.get("ordinal"), row.get("score"))
        for row in rubric.get("labels", [])
        if isinstance(row, dict)
    }
    require(actual_labels == expected_labels, "rubric label/ordinal/score drift", errors)
    native = rubric.get("native", {})
    require(native.get("overallTarget") == 87.0, "native target drift", errors)
    require(native.get("requiredCellMinimum") == 70, "required cell floor drift", errors)
    require(native.get("categoryWeightTotal") == 100, "categoryWeightTotal drift", errors)
    actual: dict[str, Any] = {}
    for category in native.get("categories", []):
        if not isinstance(category, dict):
            continue
        actual[category.get("id")] = (
            category.get("weight"),
            category.get("minimumScore"),
            {
                cell.get("id"): (cell.get("weight"), tuple(cell.get("laneOwnership", [])))
                for cell in category.get("cells", [])
                if isinstance(cell, dict)
            },
        )
    require(actual == EXPECTED_CATEGORIES, "native category/cell/lane matrix drift", errors)
    require(len(ALL_CELLS) == len(set(ALL_CELLS)) == 39, "native cell count must be 39 unique", errors)
    require(len(COLD_CELLS) == 30, "cold lane must own 30 cells", errors)
    require(len(COVERAGE_CELLS) == 34, "coverage lane must own 34 cells", errors)


def validate_coverage_and_concepts(
    native: Path,
    concept: dict[str, Any],
    coverage: dict[str, Any],
    errors: list[str],
) -> None:
    require(concept.get("protocol") == PROTOCOL, "concept protocol mismatch", errors)
    require(coverage.get("protocol") == PROTOCOL, "coverage protocol mismatch", errors)
    require(concept.get("requiredCells") == ALL_CELLS, "concept requiredCells must equal rubric order", errors)
    probes = concept.get("probes", [])
    require(isinstance(probes, list) and len(probes) == 25, "concept must contain 25 probes", errors)
    probe_ids = [probe.get("id") for probe in probes if isinstance(probe, dict)]
    require(probe_ids == EXPECTED_PROBE_IDS, "concept probe IDs/order drift", errors)
    require(len(probe_ids) == len(set(probe_ids)) == 25, "concept probe IDs must be unique", errors)
    cold_probe_order = unique_strings(concept.get("coldProbeOrder"), "concept.coldProbeOrder", errors)
    require(cold_probe_order == EXPECTED_COLD_PROBE_ORDER, "concept coldProbeOrder drift", errors)

    episodes = coverage.get("episodes", [])
    episode_ids = [episode.get("id") for episode in episodes if isinstance(episode, dict)]
    require(episode_ids == EPISODES, "coverage must contain ordered E00..E11", errors)
    episode_map = {episode.get("id"): episode for episode in episodes if isinstance(episode, dict)}
    scoring_union: set[str] = set()
    seen_checkpoints: set[tuple[str, str]] = set()
    seen_action_occurrences: set[str] = set()
    for episode_id in EPISODES:
        episode = episode_map.get(episode_id)
        if not isinstance(episode, dict):
            continue
        checkpoints = unique_strings(episode.get("checkpoints"), f"coverage.{episode_id}.checkpoints", errors)
        actions = unique_strings(episode.get("actions"), f"coverage.{episode_id}.actions", errors)
        cells = unique_strings(episode.get("cells"), f"coverage.{episode_id}.cells", errors)
        require(bool(checkpoints), f"coverage.{episode_id} needs checkpoints", errors)
        require(bool(actions), f"coverage.{episode_id} needs actions", errors)
        for action in actions:
            require(
                action not in seen_action_occurrences,
                f"coverage action occurrence ID is not globally unique: {action}",
                errors,
            )
            seen_action_occurrences.add(action)
        outside = set(cells) - set(COVERAGE_CELLS)
        require(not outside, f"coverage.{episode_id} score-bearing cells outside lane: {sorted(outside)}", errors)
        scoring_union.update(cells)
        for checkpoint in checkpoints:
            key = (episode_id, checkpoint)
            require(key not in seen_checkpoints, f"duplicate coverage checkpoint: {key}", errors)
            seen_checkpoints.add(key)
    require(scoring_union == set(COVERAGE_CELLS), "coverage scoring union must equal 34 owned cells", errors)
    require(coverage.get("goldStateManifest") == "gold-state-manifest.json", "coverage gold manifest ref drift", errors)
    require((native / "gold-state-manifest.json").is_file(), "coverage gold manifest reference is missing", errors)

    for probe in probes:
        if not isinstance(probe, dict):
            continue
        probe_id = probe.get("id", "<unknown>")
        cells = unique_strings(probe.get("cells"), f"concept.{probe_id}.cells", errors)
        require(set(cells).issubset(ALL_CELLS), f"concept.{probe_id} has unknown cells", errors)
        episode = probe.get("firstEpisode")
        checkpoint = probe.get("firstCheckpoint")
        require(episode in episode_map, f"concept.{probe_id} references unknown episode", errors)
        if episode in episode_map:
            require(
                checkpoint in episode_map[episode].get("checkpoints", []),
                f"concept.{probe_id} first checkpoint is not in coverage recipe",
                errors,
            )
        if probe.get("requiredForCold") is True:
            require(
                episode in EPISODES[:10],
                f"concept.{probe_id} requiredForCold firstEpisode must be reachable in E00..E09",
                errors,
            )
    cold_probe_union = {
        cell
        for probe in probes
        if isinstance(probe, dict) and probe.get("requiredForCold") is True
        for cell in probe.get("cells", [])
    }
    coverage_probe_union = {
        cell
        for probe in probes
        if isinstance(probe, dict) and probe.get("requiredForCoverage") is True
        for cell in probe.get("cells", [])
    }
    require(
        sum(1 for probe in probes if isinstance(probe, dict) and probe.get("requiredForCold") is True) == 18,
        "concept must contain exactly 18 required cold probes",
        errors,
    )
    required_cold_ids = {
        probe.get("id")
        for probe in probes
        if isinstance(probe, dict) and probe.get("requiredForCold") is True
    }
    require(
        len(cold_probe_order) == 18 and set(cold_probe_order) == required_cold_ids,
        "concept coldProbeOrder must exactly permute required cold probe IDs",
        errors,
    )
    authored_error_ids = {
        "PX03-PLACEMENT-RECOVERY",
        "PX04-LINE-DRAFT",
        "PX13-FLOOD-DEADLINE-RECOVERY",
    }
    require(
        all(
            probe.get("requiredForCold") is False and probe.get("requiredForCoverage") is True
            for probe in probes
            if isinstance(probe, dict) and probe.get("id") in authored_error_ids
        ),
        "authored error probes must be coverage-only",
        errors,
    )
    require(set(COLD_CELLS).issubset(cold_probe_union), "required cold probes do not cover all 30 cold cells", errors)
    require(
        set(COVERAGE_CELLS).issubset(coverage_probe_union),
        "required coverage probes do not cover all 34 coverage cells",
        errors,
    )


def validate_cold_checkpoint_sequence(
    cold_recipe: dict[str, Any],
    coverage: dict[str, Any],
    concept: dict[str, Any],
    errors: list[str],
) -> None:
    """Freeze the actual cold timeline, including the E09 restart inside E04."""
    episode_map = {
        row.get("id"): row
        for row in coverage.get("episodes", [])
        if isinstance(row, dict)
    }
    ordered_pairs: list[tuple[str, str]] = []

    def extend_episode(episode_id: str, start: int = 0, stop: int | None = None) -> None:
        episode = episode_map.get(episode_id, {})
        checkpoints = episode.get("checkpoints", []) if isinstance(episode, dict) else []
        if isinstance(checkpoints, list):
            ordered_pairs.extend((episode_id, checkpoint) for checkpoint in checkpoints[start:stop])

    for episode_id in EPISODES[:4]:
        extend_episode(episode_id)
    extend_episode("E04-NORTH-BANK", 0, 1)
    extend_episode("E09-MID-RESUME")
    extend_episode("E04-NORTH-BANK", 1)
    for episode_id in EPISODES[5:9]:
        extend_episode(episode_id)

    branch_groups = {
        ("E04-NORTH-BANK", "north-bank-keep-result"): "E04-RESULT",
        ("E04-NORTH-BANK", "north-bank-defer-result"): "E04-RESULT",
        ("E05-WHOSE-MARGIN", "whose-margin-keep-result"): "E05-RESULT",
        ("E05-WHOSE-MARGIN", "whose-margin-defer-result"): "E05-RESULT",
        ("E07-MAINTENANCE", "maintenance-keep-result"): "E07-RESULT",
        ("E07-MAINTENANCE", "maintenance-defer-result"): "E07-RESULT",
        ("E08-FINALE", "finale-keep-result"): "E08-RESULT",
        ("E08-FINALE", "finale-defer-result"): "E08-RESULT",
    }
    expected: list[dict[str, Any]] = []
    ordinal = 0
    previous_group: str | None = None
    for episode_id, checkpoint in ordered_pairs:
        group = branch_groups.get((episode_id, checkpoint))
        if group is None or group != previous_group:
            ordinal += 1
        expected.append({
            "sequenceOrdinal": ordinal,
            "episode": episode_id,
            "checkpoint": checkpoint,
            "branchAlternativeGroup": group,
        })
        previous_group = group
    actual = cold_recipe.get("checkpointSequence")
    require(
        actual == expected and len(expected) == 45 and ordinal == 41,
        "cold checkpoint sequence must equal the 45-row/41-rank E09-inside-E04 authority",
        errors,
    )
    rank_by_checkpoint = {
        (row["episode"], row["checkpoint"]): row["sequenceOrdinal"]
        for row in expected
    }
    probe_by_id = {
        row.get("id"): row
        for row in concept.get("probes", [])
        if isinstance(row, dict)
    }
    cold_order = concept.get("coldProbeOrder", [])
    cold_ranks = [
        rank_by_checkpoint.get(
            (
                probe_by_id.get(probe_id, {}).get("firstEpisode"),
                probe_by_id.get(probe_id, {}).get("firstCheckpoint"),
            )
        )
        for probe_id in cold_order
    ] if isinstance(cold_order, list) else []
    require(
        len(cold_ranks) == 18
        and all(isinstance(rank, int) for rank in cold_ranks)
        and cold_ranks == sorted(cold_ranks),
        "coldProbeOrder must be monotonic in the frozen cold checkpoint sequence",
        errors,
    )


def validate_holdouts(holdouts: dict[str, Any], errors: list[str]) -> None:
    require(holdouts.get("protocol") == PROTOCOL, "holdout protocol mismatch", errors)
    require(holdouts.get("selectionRule") == "LOWEST_UNUSED_ORDINAL", "holdout selection rule drift", errors)
    require(holdouts.get("reuseAllowed") is False, "holdout reuse must be false", errors)
    require(holdouts.get("baseCoverageRecipe") == "coverage-recipe.json", "holdout base recipe drift", errors)
    require(
        holdouts.get("queueAuthorityId")
        == "GRIDWORKS-COMMERCIAL-UX-HOLDOUT-QUEUE-v1.1",
        "holdout queue authority ID drift",
        errors,
    )
    require(
        holdouts.get("registryPolicy") == {
            "scope": "LOCAL_REPOSITORY_REGISTRY_ONLY",
            "authorityLimit": "NOT_GLOBAL_ACROSS_REPOSITORY_CLONES",
            "registryPathRule": "GIT_COMMON_DIR/gridworks-commercial-ux/holdout-consumption-registry-v1.json",
            "receiptPathRule": "GIT_COMMON_DIR/gridworks-commercial-ux/holdout-receipts/{transactionSha256Hex}-{candidateReuseSha256Hex}.json",
            "receiptCreatePolicy": "O_EXCL_CREATE_AND_FSYNC_BEFORE_ATOMIC_REGISTRY_RENAME",
            "transaction": "LOCKED_COMPARE_LOWEST_UNUSED_APPEND_FSYNC_ATOMIC_RENAME",
            "recipeReuseKeyFields": ["queueAuthorityId", "recipeId"],
            "candidateReuseKeyField": "candidateReuseSha256",
            "candidateReuseKeyUnique": True,
        },
        "holdout local registry policy drift",
        errors,
    )
    formative = holdouts.get("formative", {})
    require(
        isinstance(formative, dict)
        and formative.get("routeFamily") == "NATIVE_SMOKE_WITNESS"
        and formative.get("missionPrototypeBits") == "001",
        "formative native-smoke prototype realization must be exact 001",
        errors,
    )
    rows = holdouts.get("holdouts", [])
    require(isinstance(rows, list) and len(rows) == 8, "holdout queue must contain 8 rows", errors)
    expected_bits = ["000", "111", "010", "101", "001", "110", "011", "100"]
    for index, row in enumerate(rows, start=1):
        if not isinstance(row, dict):
            errors.append(f"holdout row {index} must be an object")
            continue
        require(row.get("id") == f"HOLDOUT-{index:02d}", f"holdout {index} ID drift", errors)
        require(row.get("ordinal") == index, f"holdout {index} ordinal drift", errors)
        require(row.get("missionPrototypeBits") == expected_bits[index - 1], f"holdout {index} bits drift", errors)
        branch = row.get("promiseBranchOrder")
        require(sorted(branch) == ["defer", "keep"] if isinstance(branch, list) else False, f"holdout {index} branch order invalid", errors)
        permutation = row.get("actorArtifactPermutation")
        require(sorted(permutation) == [0, 1, 2] if isinstance(permutation, list) else False, f"holdout {index} actor permutation invalid", errors)
        require(row.get("coverageArtifactOrder") in {"EPISODE_ASCENDING", "EPISODE_DESCENDING"}, f"holdout {index} coverage order invalid", errors)


def qualification_projection(
    anchors: dict[str, Any],
    transport_map: dict[str, Any],
    anchors_raw_sha256: str,
    rubric_sha256: str,
) -> dict[str, Any]:
    authority_rows = {
        row["anchorId"]: row for row in anchors["anchors"] if isinstance(row, dict)
    }
    projected = {
        "schemaVersion": "gridworks.commercial-ux.native-qualification-input.v1",
        "protocol": PROTOCOL,
        "candidateIndependent": True,
        "judgeInputSha256": None,
        "qualificationAnchorsAuthoritySha256": anchors_raw_sha256,
        "rubricSha256": rubric_sha256,
        "anchors": [
            {
                "anchorId": entry["transportId"],
                "assignedExperience": authority_rows[entry["authorityAnchorId"]]["assignedExperience"],
                "trace": authority_rows[entry["authorityAnchorId"]]["trace"],
            }
            for entry in transport_map["entries"]
        ],
    }
    projected["judgeInputSha256"] = self_hash(projected, "judgeInputSha256")
    return projected


def validate_qualification(
    anchors: dict[str, Any],
    transport_map: dict[str, Any],
    input_schema: dict[str, Any],
    anchors_raw_sha256: str,
    rubric_sha256: str,
    errors: list[str],
) -> None:
    require(anchors.get("protocol") == PROTOCOL, "qualification anchor protocol mismatch", errors)
    require(anchors.get("candidateIndependent") is True, "qualification anchors must be independent", errors)
    pass_rule = anchors.get("passRule", {})
    require(
        pass_rule == {
            "minimumExactPerJudge": 19,
            "anchorCount": 20,
            "excellentAndBrokenAllExact": True,
            "schemaValidCount": 20,
            "fullPanelReplacementMaximum": 1,
        },
        "qualification pass rule drift",
        errors,
    )
    rows = anchors.get("anchors", [])
    require(isinstance(rows, list) and len(rows) == 20, "qualification must contain 20 anchors", errors)
    labels = Counter(row.get("expectedLabel") for row in rows if isinstance(row, dict))
    require(labels == Counter({label: 4 for label in LABELS}), "qualification labels must be 4 per band", errors)
    experiences: dict[str, set[str]] = {label: set() for label in LABELS}
    ids: list[str] = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        exact_keys(row, {"anchorId", "expectedLabel", "assignedExperience", "trace"}, "qualification anchor", errors)
        ids.append(row.get("anchorId"))
        label = row.get("expectedLabel")
        if label in experiences:
            experiences[label].add(row.get("assignedExperience"))
    require(len(ids) == len(set(ids)) == 20, "qualification authority IDs must be unique", errors)
    require(len({frozenset(value) for value in experiences.values()}) == 1, "each label must cover the same four experiences", errors)

    entries = transport_map.get("entries", [])
    require(isinstance(entries, list) and len(entries) == 20, "qualification map must contain 20 entries", errors)
    transport_ids = [entry.get("transportId") for entry in entries if isinstance(entry, dict)]
    authority_ids = [entry.get("authorityAnchorId") for entry in entries if isinstance(entry, dict)]
    require(transport_ids == [f"QA-{index:04d}" for index in range(1, 21)], "qualification transport IDs/order drift", errors)
    require(set(authority_ids) == set(ids), "qualification map must be a full authority permutation", errors)
    require(len(authority_ids) == len(set(authority_ids)) == 20, "qualification map repeats authority IDs", errors)

    projected = qualification_projection(
        anchors,
        transport_map,
        anchors_raw_sha256,
        rubric_sha256,
    )
    serialized = json.dumps(projected, ensure_ascii=False, sort_keys=True)
    require("expectedLabel" not in serialized and "authorityAnchorId" not in serialized, "qualification projection leaks authority fields", errors)
    for label in LABELS:
        require(label not in serialized, f"qualification projection leaks label token {label}", errors)
    errors.extend(f"qualification projection {error}" for error in instance_errors(projected, input_schema))


def validate_schema_bindings(native: Path, errors: list[str]) -> dict[str, dict[str, Any]]:
    schemas: dict[str, dict[str, Any]] = {}
    for path in sorted(native.glob("*.schema.json")):
        try:
            schema = read_json(path)
        except ContractError as error:
            errors.append(str(error))
            continue
        schemas[path.name] = schema
        require(schema.get("$schema") == "https://json-schema.org/draft/2020-12/schema", f"{path.name} draft drift", errors)
        audit_schema(schema, path.name, errors, schema)
        protocol_nodes: list[str] = []

        def collect(value: Any) -> None:
            if isinstance(value, dict):
                if value.get("const", "") == PROTOCOL:
                    protocol_nodes.append(PROTOCOL)
                for child in value.values():
                    collect(child)
            elif isinstance(value, list):
                for child in value:
                    collect(child)

        collect(schema)
        require(bool(protocol_nodes), f"{path.name} does not bind protocol {PROTOCOL}", errors)

    def external_refs(value: Any) -> list[str]:
        found: list[str] = []
        if isinstance(value, dict):
            reference = value.get("$ref")
            if isinstance(reference, str) and not reference.startswith("#/"):
                found.append(reference)
            for child in value.values():
                found.extend(external_refs(child))
        elif isinstance(value, list):
            for child in value:
                found.extend(external_refs(child))
        return found

    for schema_name, schema in schemas.items():
        for reference in external_refs(schema):
            require(reference in schemas, f"{schema_name} external $ref is missing: {reference}", errors)

    evidence = schemas.get("evidence-set.schema.json", {})
    require(evidence.get("$defs", {}).get("coldCellId", {}).get("enum") == COLD_CELLS, "evidence schema cold cells drift", errors)
    require(evidence.get("$defs", {}).get("coverageCellId", {}).get("enum") == COVERAGE_CELLS, "evidence schema coverage cells drift", errors)
    judge = schemas.get("native-judge.schema.json", {})
    require(judge.get("$defs", {}).get("coldCellId", {}).get("enum") == COLD_CELLS, "judge schema cold cells drift", errors)
    require(judge.get("$defs", {}).get("coverageCellId", {}).get("enum") == COVERAGE_CELLS, "judge schema coverage cells drift", errors)
    opaque_ids = [f"QA-{index:04d}" for index in range(1, 21)]
    require(judge.get("$defs", {}).get("qualificationAnchorId", {}).get("enum") == opaque_ids, "judge qualification IDs are not opaque", errors)

    scorecard = schemas.get("native-scorecard.schema.json", {})
    score_properties = scorecard.get("$defs", {}).get("cellScores", {}).get("properties", {})
    require(list(score_properties) == ALL_CELLS, "scorecard must contain all 39 cells in rubric order", errors)
    expected_ref = {
        ("COLD-JOURNEY",): "#/$defs/coldOnlyScore",
        ("COVERAGE-JOURNEY",): "#/$defs/coverageOnlyScore",
        ("COLD-JOURNEY", "COVERAGE-JOURNEY"): "#/$defs/bothLaneScore",
    }
    for _, _, cells in EXPECTED_CATEGORIES.values():
        for cell, (_, lanes) in cells.items():
            require(score_properties.get(cell, {}).get("$ref") == expected_ref[lanes], f"scorecard lane binding drift for {cell}", errors)
    gate_prefix = schemas.get("oracle-hard-gate-ledger.schema.json", {}).get("properties", {}).get("hardGates", {}).get("prefixItems", [])
    gate_ids = [row.get("properties", {}).get("gateId", {}).get("const") for row in gate_prefix]
    require(gate_ids == HARD_GATES, "oracle ledger hard-gate order drift", errors)
    oracle_required = set(
        schemas.get("oracle-hard-gate-ledger.schema.json", {}).get("required", [])
    )
    require(
        {
            "candidateManifestSha256", "holdoutConsumptionReceiptSha256",
            "goldBindingManifestSha256", "coldActorResponseSha256",
            "coldActorResponseRawSha256", "actorTraceSha256",
            "coverageActionLedgerSha256", "coverageArtifactId",
            "recordingManifestSha256", "anonymizationManifestSha256",
            "evidenceSetSha256", "sanitizedEvidenceBundleManifestSha256",
            "sanitizedEvidenceContentRootSha256", "candidateJudgeInputSha256",
            "verificationInputSha256", "verificationOutputSha256",
            "rubricSha256", "contractBindingsSha256", "canonicalHashPolicySha256",
            "goldStateContractSha256", "coverageRecipeSha256", "conceptManifestSha256",
            "nativeAggregatorSha256", "contractValidatorSha256", "goldValidatorSha256",
        }.issubset(oracle_required),
        "oracle ledger does not bind its complete artifact/tool/authority DAG",
        errors,
    )
    coverage_trace = schemas.get("coverage-trace.schema.json", {})
    coverage_prefix = coverage_trace.get("properties", {}).get("episodes", {}).get("prefixItems", [])
    coverage_episode_ids = [row.get("properties", {}).get("episodeId", {}).get("const") for row in coverage_prefix]
    require(coverage_episode_ids == EPISODES, "coverage trace must bind ordered E00..E11", errors)
    require(
        "actionOccurrenceId"
        in coverage_trace.get("$defs", {}).get("traceRow", {}).get("required", []),
        "coverage trace does not bind occurrence-specific recipe action IDs",
        errors,
    )
    qualification_receipt = schemas.get("qualification-receipt.schema.json", {})
    require(
        "qualificationReceiptSha256" in qualification_receipt.get("properties", {}),
        "qualification receipt lacks its canonical self-hash",
        errors,
    )
    replacement_receipt = schemas.get("native-replacement-receipt.schema.json", {})
    replacement_properties = replacement_receipt.get("properties", {})
    require(
        replacement_properties.get("claimPolicy", {}).get("const")
        == "VERIFIED_INITIAL_FINALIZATION_SEAL_THEN_O_EXCL_BEFORE_ATTEMPT_READ_AND_FINALIZE_SAME_DESCRIPTOR"
        and replacement_properties.get("replacementReceiptPathRule", {}).get("const")
        == "GIT_COMMON_DIR/gridworks-commercial-ux/replacement-receipts/{initialPanelSha256Hex}.json"
        and {
            "initialPanelFinalizationSealPath",
            "initialPanelFinalizationSealSha256",
            "initialPanelFinalizationSealRawSha256",
        }.issubset(set(replacement_receipt.get("required", []))),
        "replacement receipt does not freeze authority-preflight/O_EXCL/read/finalize ordering",
        errors,
    )
    require(
        replacement_properties.get("authorityPreflightStatus", {}).get("const")
        == "EXACT_BEFORE_CLAIM",
        "replacement receipt does not prove exact authority preflight before claiming",
        errors,
    )
    require(
        {
            "rawAggregationInputSha256", "qualificationReceiptSha256",
            "evaluationRunManifestSha256", "panelAttempt", "judgmentAttempts",
        }.issubset(replacement_properties),
        "replacement receipt is missing preclaim authority or slot-attempt provenance",
        errors,
    )
    receipt_slots = replacement_properties.get("judgmentAttempts", {}).get("prefixItems", [])
    receipt_slot_ids = [
        row.get("allOf", [{}, {}])[1]
        .get("properties", {}).get("slotId", {}).get("const")
        if isinstance(row, dict) and len(row.get("allOf", [])) >= 2
        else None
        for row in receipt_slots
    ]
    require(
        receipt_slot_ids == ["JUDGE-01", "JUDGE-02", "JUDGE-03"]
        and replacement_properties.get("judgmentAttempts", {}).get("uniqueItems") is not True,
        "replacement receipt judgment attempts must use three stable slots and allow duplicate raw bytes",
        errors,
    )
    judge_panel = schemas.get("judge-panel.schema.json", {})
    require("judgePanelSha256" in judge_panel.get("properties", {}), "judge panel lacks self-hash", errors)
    require(
        "scorecardSha256" not in judge_panel.get("properties", {})
        and "aggregateStatus" not in judge_panel.get("properties", {}),
        "pre-aggregate judge panel contains a post-aggregate hash cycle",
        errors,
    )
    evaluation_run = schemas.get("evaluation-run-manifest.schema.json", {})
    run_artifact_properties = evaluation_run.get("$defs", {}).get("artifacts", {}).get("properties", {})
    run_artifact_required = set(
        evaluation_run.get("$defs", {}).get("artifacts", {}).get("required", [])
    )
    retry_schema = evaluation_run.get("$defs", {}).get("retry", {})
    retry_required = set(retry_schema.get("required", []))
    retry_all_of = retry_schema.get("allOf", [])
    terminal_states = evaluation_run.get("properties", {}).get("terminalStates", {})
    require(
        terminal_states.get("uniqueItems") is True,
        "evaluation run terminal rows must reject duplicates",
        errors,
    )
    require(
        "scorecardSha256" not in run_artifact_properties
        and "aggregationInputSha256" not in run_artifact_properties,
        "pre-aggregate evaluation run contains a post-aggregate hash cycle",
        errors,
    )
    require(
        {
            "coldActorResponseSha256", "coldActorResponseRawSha256",
            "actorTraceSha256", "actorTraceRawSha256",
            "verificationInputSha256", "verificationInputRawSha256",
            "goldBindingManifestSha256", "goldBindingManifestRawSha256",
            "holdoutConsumptionReceiptSha256",
            "holdoutConsumptionReceiptRawSha256",
            "coverageActionLedgerSha256", "coverageActionLedgerRawSha256",
            "anonymizationManifestSha256", "anonymizationManifestRawSha256",
            "evidenceSetSha256", "evidenceSetRawSha256",
            "sanitizedEvidenceBundleManifestSha256",
            "sanitizedEvidenceBundleManifestRawSha256",
            "sanitizedEvidenceContentRootSha256",
            "candidateJudgeInputSha256", "candidateJudgeInputRawSha256",
        }.issubset(run_artifact_required),
        "evaluation run does not bind required raw/self pre-aggregate artifacts",
        errors,
    )
    require(
        {
            "coldActorResponseSha256", "coldActorResponseRawSha256",
        }.issubset(set(schemas.get("actor-observation.schema.json", {}).get("required", [])))
        and {
            "coldActorResponseSha256", "coldActorResponseRawSha256",
        }.issubset(set(schemas.get("actor-trace.schema.json", {}).get("required", [])))
        and "coldActorResponseSha256"
        in schemas.get("cold-actor-response.schema.json", {}).get("required", []),
        "cold actor semantic response lacks exact raw/self observation/trace binding",
        errors,
    )
    require(
        "recipeCheckpointSequenceOrdinal"
        in schemas.get("actor-observation.schema.json", {})
        .get("$defs", {}).get("checkpoint", {}).get("required", []),
        "actor observation checkpoint lacks recorder-owned recipe sequence rank",
        errors,
    )
    response = schemas.get("cold-actor-response.schema.json", {})
    response_first_use = set(response.get("$defs", {}).get("firstUseRecord", {}).get("required", []))
    observation_schema = schemas.get("actor-observation.schema.json", {})
    observation_first_use = set(
        observation_schema.get("$defs", {}).get("firstUseRecord", {}).get("required", [])
    )
    require(
        {
            "firstUseOrdinal", "probeId", "currentGoal", "expectedVisibleConsequence",
            "citedVisibleSourceDescription",
        }.issubset(response_first_use)
        and response_first_use.issubset(observation_first_use),
        "cold actor response semantic first-use projection drift",
        errors,
    )
    semantic_projection_fields = {
        "firstUseRecord": {
            "firstUseOrdinal", "probeId", "currentGoal", "expectedVisibleConsequence",
            "citedVisibleSourceDescription",
        },
        "approvalRecord": {
            "approvalOrdinal", "predictionImmediatelyBeforeApproval", "observedResult",
            "causalAccount",
        },
        "incident": {
            "incidentOrdinal", "incidentType", "confusionBoundary", "severity", "description",
        },
    }
    for definition, fields in semantic_projection_fields.items():
        response_definition = response.get("$defs", {}).get(definition, {})
        observation_definition = observation_schema.get("$defs", {}).get(definition, {})
        require(
            set(response_definition.get("required", [])) == fields
            and fields.issubset(set(observation_definition.get("required", [])))
            and all(
                response_definition.get("properties", {}).get(field)
                == observation_definition.get("properties", {}).get(field)
                for field in fields
            ),
            f"cold actor response {definition} exact semantic projection drift",
            errors,
        )
    require(
        "actorTraceSha256"
        in schemas.get("actor-trace.schema.json", {}).get("required", []),
        "actor trace lacks a distinct canonical self-hash",
        errors,
    )
    require(
        "recordingManifestRawSha256"
        in schemas.get("actor-trace.schema.json", {}).get("required", [])
        and "recordingManifestRawSha256"
        in schemas.get("coverage-trace.schema.json", {}).get("required", []),
        "actor/coverage traces must bind recording manifest raw and self hashes",
        errors,
    )
    evidence_trace_required = set(
        evidence.get("$defs", {}).get("traceRow", {}).get("required", [])
    )
    require(
        {
            "checkpointBranchId", "semanticActionKind", "actionOccurrenceId",
            "prototypeSlot", "prototypeKind", "branchSequenceOrdinal",
            "branchDecision",
        }.issubset(evidence_trace_required),
        "sanitized evidence trace rows do not preserve exact action realization fields",
        errors,
    )
    anonymization = schemas.get("anonymization-manifest.schema.json", {})
    require(
        "sanitizedArtifactSha256"
        not in anonymization.get("$defs", {}).get("sourceActor", {}).get("properties", {})
        and "sanitizedArtifactSha256"
        not in anonymization.get("$defs", {}).get("sourceCoverage", {}).get("properties", {}),
        "anonymization manifest reintroduces a sanitized-evidence hash cycle",
        errors,
    )
    recording = schemas.get("recording-manifest.schema.json", {})
    recording_required = set(recording.get("required", []))
    recording_artifact = recording.get("$defs", {}).get("artifact", {})
    recording_actor_rule = recording.get("allOf", [{}])[0]
    actor_artifact_rule = (
        recording_actor_rule.get("then", {}).get("properties", {}).get("artifacts", {})
        if isinstance(recording_actor_rule, dict)
        else {}
    )
    valid_frame = {
        "artifactId": "frame-1", "kind": "FRAME", "locator": "frames/one.png",
        "rawSha256": "sha256:" + "1" * 64, "byteLength": 1,
        "mimeType": "image/png",
    }
    invalid_frame = {**valid_frame, "mimeType": "text/plain"}
    require(
        recording.get("properties", {}).get("locatorPolicy", {}).get("const")
        == "CANONICAL_RELATIVE_NO_DOTDOT_REJECT_ALL_SYMLINKS"
        and recording.get("properties", {}).get("mimePolicy", {}).get("const")
        == "PNG_WAV_OR_STRICT_JSON_OBJECT_MATCHING_MAGIC_AND_SCHEMA"
        and {"actorCaptureSlot", "actorRunId", "coverageRunId", "processTreeId"}
        .issubset(recording_required)
        and {"actionLedgerSchemaSha256", "actionLedgerArtifactRawSha256"}
        .issubset(recording_required)
        and actor_artifact_rule.get("minContains") == 1
        and actor_artifact_rule.get("maxContains") == 1
        and actor_artifact_rule.get("contains", {}).get("properties", {}).get("kind", {}).get("const")
        == "ACTION_LEDGER"
        and not instance_errors(valid_frame, recording_artifact, recording)
        and bool(instance_errors(invalid_frame, recording_artifact, recording))
        and bool(instance_errors("../escape.png", recording_artifact.get("properties", {}).get("locator", {}), recording)),
        "recording manifest path/symlink/MIME verification policy drift",
        errors,
    )
    actor_action_ledger = schemas.get("actor-action-ledger.schema.json", {})
    actor_ledger_required = set(actor_action_ledger.get("required", []))
    actor_ledger_action_required = set(
        actor_action_ledger.get("$defs", {}).get("action", {}).get("required", [])
    )
    observation_action_schema = (
        schemas.get("actor-observation.schema.json", {}).get("$defs", {}).get("action", {})
    )
    actor_ledger_checkpoint_required = set(
        actor_action_ledger.get("$defs", {}).get("checkpointPostState", {}).get("required", [])
    )
    require(
        {
            "candidateManifestSha256", "coldActorResponseSha256", "actorRunId",
            "processTreeId", "actionCount", "checkpointCount", "actions",
            "checkpointPostStates", "projectionRule",
        }.issubset(actor_ledger_required)
        and {
            "actionIndex", "preStateSha256", "postStateSha256", "appActive",
            "rationalInProductAction",
        }.issubset(actor_ledger_action_required)
        and {
            "checkpointOrdinal", "recipeCheckpointSequenceOrdinal",
            "appActiveActionIndex", "progressStateSha256", "actionPostStateSha256",
        }.issubset(actor_ledger_checkpoint_required)
        and actor_action_ledger.get("$defs", {}).get("action") == observation_action_schema,
        "actor action ledger cannot derive observation actions/checkpoint post-state linkage",
        errors,
    )
    coverage_required = set(
        schemas.get("coverage-trace.schema.json", {}).get("required", [])
    )
    require(
        {
            "coverageRunId", "coverageActionLedgerSha256",
            "coverageActionLedgerRawSha256", "coverageActionLedgerSchemaSha256",
        }.issubset(coverage_required),
        "coverage trace does not bind the strict raw/self action ledger",
        errors,
    )
    sanitized = schemas.get("sanitized-evidence-bundle-manifest.schema.json", {})
    require(
        sanitized.get("properties", {}).get("sourceRecordingRootsExposedToJudge", {}).get("const")
        is False
        and sanitized.get("properties", {}).get("bundles", {}).get("minItems") == 4
        and sanitized.get("$defs", {}).get("bundle", {}).get("properties", {}).get("extraFileCount", {}).get("const") == 0
        and sanitized.get("$defs", {}).get("bundle", {}).get("properties", {}).get("symlinkCount", {}).get("const") == 0,
        "sanitized verifier bundle does not own four complete identity-free content roots",
        errors,
    )
    candidate_judge_input_required = set(
        schemas.get("candidate-judge-input.schema.json", {}).get("required", [])
    )
    require(
        {
            "judgeInputSha256", "qualificationReceiptSha256", "qualificationStatus",
            "evidenceSetSha256", "evidenceSetRawSha256",
            "sanitizedEvidenceBundleManifestSha256",
            "sanitizedEvidenceBundleManifestRawSha256",
            "sanitizedEvidenceContentRootSha256", "artifactOrder",
            "promptTemplateSha256", "judgmentSchemaSha256", "rubricSha256",
        }.issubset(candidate_judge_input_required),
        "candidate judge input lacks its exact qualified evidence/prompt/schema projection",
        errors,
    )
    scorecard_provenance_required = set(
        scorecard.get("$defs", {}).get("provenance", {}).get("required", [])
    )
    require(
        {
            "holdoutConsumptionReceiptSha256", "holdoutConsumptionReceiptRawSha256",
            "goldBindingManifestSha256", "goldBindingManifestRawSha256",
            "coldActorResponseSha256", "coldActorResponseRawSha256",
            "anonymizationManifestSha256", "anonymizationManifestRawSha256",
            "evidenceSetSha256", "evidenceSetRawSha256",
            "sanitizedEvidenceBundleManifestSha256",
            "sanitizedEvidenceBundleManifestRawSha256",
            "sanitizedEvidenceContentRootSha256",
            "candidateJudgeInputSha256", "candidateJudgeInputRawSha256",
        }.issubset(scorecard_provenance_required),
        "scorecard provenance cannot prove replacement uses the same holdout/evidence authority",
        errors,
    )
    finalization = schemas.get("panel-finalization-seal.schema.json", {})
    require(
        finalization.get("properties", {}).get("claimPolicy", {}).get("const")
        == "SCORECARD_EXCLUSIVE_WRITE_THEN_O_EXCL_SEAL_FSYNC"
        and finalization.get("properties", {}).get("sealPathRule", {}).get("const")
        == "GIT_COMMON_DIR/gridworks-commercial-ux/panel-finalizations/{initialPanelSha256Hex}-{panelKindLower}.json",
        "panel finalization seal path/claim policy drift",
        errors,
    )
    candidate_execution = schemas.get("candidate-manifest.schema.json", {}).get(
        "$defs", {}
    ).get("execution", {})
    require(
        candidate_execution.get("properties", {}).get("componentPathPolicy", {}).get("const")
        == "CANONICAL_ABSOLUTE_REGULAR_FILE_REJECT_SYMLINKS"
        and candidate_execution.get("properties", {}).get("executionArtifactHashRule", {}).get("const")
        == "SHA256_OF_RFC8785_GODOT_EXECUTABLE_SHA256_MANAGED_ASSEMBLY_SHA256_PCK_RESOURCE_MANIFEST_SHA256_PACKAGE_SHA256_PACKAGE_STATUS"
        and {
            "godotExecutablePath", "managedAssemblyPath",
            "pckResourceManifestPath", "packagePath",
        }.issubset(set(candidate_execution.get("required", []))),
        "candidate execution identity is not derived from opened canonical component bytes",
        errors,
    )
    gold_binding = schemas.get("gold-binding-manifest.schema.json", {})
    require(
        gold_binding.get("properties", {}).get("goldBundleEntryCount", {}).get("const") == 112
        and gold_binding.get("properties", {}).get("goldBundleExtraFileCount", {}).get("const") == 0
        and gold_binding.get("properties", {}).get("goldBundleSymlinkCount", {}).get("const") == 0
        and {"locator", "byteLength"}.issubset(
            set(gold_binding.get("$defs", {}).get("journalBinding", {}).get("required", []))
        )
        and {"locator", "byteLength"}.issubset(
            set(gold_binding.get("$defs", {}).get("snapshotBinding", {}).get("required", []))
        ),
        "gold overlay hashes are not backed by a complete opened raw bundle",
        errors,
    )
    require(
        "BLOCKED_PRE_CAPTURE"
        in scorecard.get("$defs", {}).get("status", {}).get("enum", [])
        and "BLOCKED_PRE_CAPTURE"
        in scorecard.get("$defs", {}).get("finalVerdict", {}).get("enum", []),
        "scorecard lacks the scoreless BLOCKED_PRE_CAPTURE state",
        errors,
    )
    require(
        {"runSlot", "readStatus", "rawArtifactSha256"}.issubset(retry_required)
        and any(
            branch.get("then", {}).get("properties", {}).get("runSlot", {}).get("enum")
            == ["PANEL", "JUDGE-01", "JUDGE-02", "JUDGE-03"]
            for branch in retry_all_of
            if isinstance(branch, dict)
        ),
        "evaluation retry ledger does not bind exact judge/replacement failure slots",
        errors,
    )
    actor_incident = schemas.get("actor-observation.schema.json", {}).get("$defs", {}).get("incident", {})
    require(
        "confusionBoundary" in actor_incident.get("required", []),
        "actor incidents do not bind the severe-confusion boundary kind",
        errors,
    )
    scorecard_provenance = scorecard.get("$defs", {}).get("provenance", {}).get("required", [])
    candidate_contract_required = set(
        schemas.get("candidate-manifest.schema.json", {})
        .get("$defs", {}).get("contractHashes", {}).get("required", [])
    )
    aggregation_provenance_required = set(
        schemas.get("native-aggregation-input.schema.json", {})
        .get("$defs", {}).get("provenance", {}).get("required", [])
    )
    require(
        "nativeAggregatorSha256" in candidate_contract_required
        and "nativeAggregatorSha256" in aggregation_provenance_required,
        "candidate/aggregation provenance does not bind the score-producing tool",
        errors,
    )
    require(
        {
            "nativeAggregatorSha256", "qualificationReceiptSha256",
            "evaluationRunManifestSha256", "aggregationInputRawSha256",
        }
        .issubset(scorecard_provenance),
        "scorecard provenance does not close the qualification/run/input DAG",
        errors,
    )
    return schemas


def validate_prompts(native: Path, errors: list[str]) -> None:
    expected_placeholders = {
        "cold-actor-prompt.template.txt": {
            "__ACTOR_RUN_ID__",
        },
        "native-judge-prompt.template.txt": {
            "__JUDGE_RUN_ID__", "__JUDGMENT_MODE__", "__PROMPT_TEMPLATE_SHA256__",
            "__JUDGMENT_SCHEMA_SHA256__", "__RUBRIC_SHA256__", "__JUDGE_INPUT_SHA256__",
            "__EVIDENCE_SET_SHA256__", "__QUALIFICATION_ANCHORS_SHA256__", "__JUDGE_INPUT__",
        },
        "native-evidence-verifier-prompt.template.txt": {
            "__VERIFIER_RUN_ID__", "__PROMPT_TEMPLATE_SHA256__", "__VERIFIER_SCHEMA_SHA256__",
            "__VERIFICATION_INPUT_SCHEMA_SHA256__", "__VERIFICATION_INPUT_SHA256__",
            "__EVIDENCE_SET_SHA256__", "__OPAQUE_JUDGE_PANEL_SHA256__", "__VERIFICATION_INPUT__",
        },
    }
    placeholder_pattern = re.compile(r"__[A-Z0-9_]+__")
    for filename, expected in expected_placeholders.items():
        path = native / filename
        try:
            text = path.read_text(encoding="utf-8")
        except (OSError, UnicodeError) as error:
            errors.append(f"cannot read prompt {path}: {error}")
            continue
        require(text.endswith("\n"), f"{filename} must end with newline", errors)
        require(set(placeholder_pattern.findall(text)) == expected, f"{filename} placeholder set drift", errors)
        require("87" not in text, f"{filename} leaks target", errors)
        require("gpt-5.6-sol" in text and "ultra" in text, f"{filename} model/effort drift", errors)
    actor = (native / "cold-actor-prompt.template.txt").read_text(encoding="utf-8")
    require("Computer Use" in actor and "capture/trace transport" in actor, "actor prompt lacks allowed native control boundary", errors)
    require(
        "terminals" in actor and "the web" in actor and re.search(r"save\s+contents", actor) is not None,
        "actor prompt isolation boundary incomplete",
        errors,
    )
    judge = (native / "native-judge-prompt.template.txt").read_text(encoding="utf-8")
    verifier = (native / "native-evidence-verifier-prompt.template.txt").read_text(encoding="utf-8")
    for name, text in (("judge", judge), ("verifier", verifier)):
        require("harness-provided media reader" in text, f"{name} prompt does not allow supplied media", errors)
        require("Do not use a shell" in text, f"{name} prompt lacks shell/source/web denylist", errors)


def validate_hash_policy(native: Path, policy: dict[str, Any], schemas: dict[str, dict[str, Any]], errors: list[str]) -> None:
    require(policy.get("protocol") == PROTOCOL, "canonical hash policy protocol mismatch", errors)
    require(policy.get("canonicalization") == "RFC8785_JSON_CANONICALIZATION_SCHEME", "canonicalization policy drift", errors)
    require(policy.get("checkedInFileRule") == "SHA256_OF_EXACT_RAW_BYTES", "checked-in raw hash rule drift", errors)
    require(
        policy.get("generatedEnvelopeRule") == "RFC8785_WITH_DESIGNATED_SELF_HASH_FIELD_NULL",
        "generated envelope hash rule drift",
        errors,
    )
    require(policy.get("selfHashRule") == "SET_DESIGNATED_FIELD_TO_NULL_BEFORE_CANONICALIZATION", "self-hash rule drift", errors)
    require(
        policy.get("projectionHashRules") == {
            "selectedRecipeSha256": "SHA256_OF_RFC8785_CANONICAL_SELECTED_FORMATIVE_OR_HOLDOUT_ROW"
        },
        "selected recipe projection hash rule drift",
        errors,
    )
    expected = {
        ("cold-actor-response.schema.json", "coldActorResponseSha256"),
        ("actor-trace.schema.json", "actorTraceSha256"),
        ("evidence-set.schema.json", "evidenceSetSha256"),
        ("qualification-input.schema.json", "judgeInputSha256"),
        ("native-evidence-verification-input.schema.json", "verificationInputSha256"),
        ("candidate-judge-input.schema.json", "judgeInputSha256"),
        ("candidate-manifest.schema.json", "candidateManifestSha256"),
        ("evaluation-run-manifest.schema.json", "evaluationRunManifestSha256"),
        ("coverage-trace.schema.json", "coverageArtifactId"),
        ("coverage-action-ledger.schema.json", "coverageActionLedgerSha256"),
        ("qualification-receipt.schema.json", "qualificationReceiptSha256"),
        ("judge-panel.schema.json", "judgePanelSha256"),
        ("holdout-consumption-receipt.schema.json", "holdoutConsumptionReceiptSha256"),
        ("holdout-consumption-registry.schema.json", "holdoutConsumptionRegistrySha256"),
        ("gold-binding-manifest.schema.json", "goldBindingManifestSha256"),
        ("anonymization-manifest.schema.json", "anonymizationManifestSha256"),
        ("recording-manifest.schema.json", "recordingManifestSha256"),
        ("sanitized-evidence-bundle-manifest.schema.json", "sanitizedEvidenceBundleManifestSha256"),
        ("panel-finalization-seal.schema.json", "panelFinalizationSealSha256"),
    }
    actual = {
        (row.get("artifactSchema"), row.get("field"))
        for row in policy.get("bindings", [])
        if isinstance(row, dict)
    }
    require(actual == expected, "canonical self-hash bindings drift", errors)
    for schema_name, field in expected:
        schema = schemas.get(schema_name)
        require(schema is not None, f"self-hash schema missing: {schema_name}", errors)
        if schema is not None:
            require(field in schema.get("properties", {}), f"self-hash field {field} absent in {schema_name}", errors)
    post_fields = policy.get("postRunActorFields")
    require(
        post_fields == [
            "coldActorResponseSha256", "coldActorResponseRawSha256",
            "userDataSha256", "saveSha256", "journalSha256",
            "recordingManifestSha256", "recordingManifestRawSha256",
            "actorArtifactId", "actorTraceSha256",
        ],
        "post-run actor packager fields drift",
        errors,
    )


def validate_contract_bindings(
    native: Path,
    bindings: dict[str, Any],
    schema: dict[str, Any],
    schemas: dict[str, dict[str, Any]],
    errors: list[str],
) -> None:
    errors.extend(f"contract bindings {error}" for error in instance_errors(bindings, schema))
    tool_policy = bindings.get("toolBindingPolicy", {})
    require(
        tool_policy.get("boundImplementedTools")
        == ["../aggregate-native.py", "validate-contract.py", "validate-gold-state.py"]
        and tool_policy.get("unboundImplementationStatus")
        == "BLOCKED_UNTIL_IMPLEMENTED_AND_RAW_HASH_BOUND"
        and tool_policy.get("scoreBearingCaptureAllowed") is False,
        "unimplemented producer tools must block score-bearing capture until raw-hash bound",
        errors,
    )
    expected_kinds = {
        "../rubric.json": "RUBRIC",
        "../aggregate-native.py": "TOOL",
        "validate-contract.py": "TOOL",
        "validate-gold-state.py": "TOOL",
        **{name: "SCHEMA" for name in schemas},
        "canonical-hash-policy.json": "POLICY",
        "cold-journey-recipe.json": "RECIPE",
        "concept-exposure-manifest.json": "AUTHORITY",
        "coverage-recipe.json": "RECIPE",
        "gold-state-manifest.json": "AUTHORITY",
        "holdout-recipes.json": "RECIPE",
        "qualification-anchors.json": "AUTHORITY",
        "qualification-transport-map.json": "AUTHORITY",
        "cold-actor-prompt.template.txt": "PROMPT",
        "native-judge-prompt.template.txt": "PROMPT",
        "native-evidence-verifier-prompt.template.txt": "PROMPT",
    }
    rows = bindings.get("fileHashes", [])
    row_map = {
        row.get("path"): row
        for row in rows
        if isinstance(row, dict) and isinstance(row.get("path"), str)
    }
    require(len(row_map) == len(rows), "contract bindings file paths must be unique", errors)
    require(set(row_map) == set(expected_kinds), "contract bindings file set drift", errors)
    for path, kind in expected_kinds.items():
        row = row_map.get(path, {})
        require(row.get("kind") == kind, f"contract binding kind mismatch: {path}", errors)
        target = (native / path).resolve()
        require(target.is_file(), f"contract binding target is missing: {path}", errors)
        if target.is_file():
            require(row.get("sha256") == raw_sha256(target), f"contract binding raw SHA mismatch: {path}", errors)

    expected_stage_ids = [
        "CANDIDATE-MANIFEST-PACKAGER", "HOLDOUT-CONSUMPTION-PACKAGER",
        "GOLD-BINDING-PACKAGER", "COLD-ACTOR", "COLD-OBSERVATION-PACKAGER",
        "COLD-PACKAGER",
        "QUALIFICATION-INPUT-PACKAGER", "QUALIFICATION-JUDGE",
        "QUALIFICATION-RECEIPT-PACKAGER", "COVERAGE-ACTION-LEDGER-PACKAGER",
        "COVERAGE-RUN-PACKAGER", "ANONYMIZATION-PACKAGER",
        "EVIDENCE-SET-PACKAGER", "CANDIDATE-JUDGE-INPUT-PACKAGER",
        "CANDIDATE-JUDGE", "JUDGE-PANEL-PACKAGER",
        "VERIFICATION-INPUT-PACKAGER", "EVIDENCE-VERIFIER",
        "ORACLE-HARD-GATES", "EVALUATION-RUN-PACKAGER",
        "AGGREGATION-INPUT-PACKAGER", "NATIVE-AGGREGATE",
        "PANEL-FINALIZATION-SEAL-PACKAGER",
    ]
    stages = bindings.get("stageBindings", [])
    require(
        [row.get("stageId") for row in stages if isinstance(row, dict)] == expected_stage_ids,
        "contract stage DAG/order drift",
        errors,
    )
    stage_map = {row.get("stageId"): row for row in stages if isinstance(row, dict)}
    policy_producers = {
        row.get("producer")
        for row in read_json(native / "canonical-hash-policy.json").get("bindings", [])
        if isinstance(row, dict)
    }
    producer_stage_map = {
        "COLD_ACTOR_TRANSPORT": "COLD-ACTOR",
        "HARNESS_FINAL_PACKAGER": "COLD-PACKAGER",
        "QUALIFICATION_INPUT_PACKAGER": "QUALIFICATION-INPUT-PACKAGER",
        "EVIDENCE_SET_PACKAGER": "EVIDENCE-SET-PACKAGER",
        "VERIFICATION_INPUT_PACKAGER": "VERIFICATION-INPUT-PACKAGER",
        "CANDIDATE_MANIFEST_PACKAGER": "CANDIDATE-MANIFEST-PACKAGER",
        "EVALUATION_RUN_PACKAGER": "EVALUATION-RUN-PACKAGER",
        "COVERAGE_FINAL_PACKAGER": "COVERAGE-RUN-PACKAGER",
        "COVERAGE_ACTION_LEDGER_PACKAGER": "COVERAGE-ACTION-LEDGER-PACKAGER",
        "QUALIFICATION_RECEIPT_PACKAGER": "QUALIFICATION-RECEIPT-PACKAGER",
        "JUDGE_PANEL_PACKAGER": "JUDGE-PANEL-PACKAGER",
        "HOLDOUT_CONSUMPTION_PACKAGER": "HOLDOUT-CONSUMPTION-PACKAGER",
        "GOLD_BINDING_PACKAGER": "GOLD-BINDING-PACKAGER",
        "ANONYMIZATION_PACKAGER": "ANONYMIZATION-PACKAGER",
        "ACTOR_RECORDING_PACKAGER": "COLD-OBSERVATION-PACKAGER",
        "COVERAGE_RECORDING_PACKAGER": "COVERAGE-RUN-PACKAGER",
        "CANDIDATE_JUDGE_INPUT_PACKAGER": "CANDIDATE-JUDGE-INPUT-PACKAGER",
        "PANEL_FINALIZATION_SEAL_PACKAGER": "PANEL-FINALIZATION-SEAL-PACKAGER",
    }
    require(set(producer_stage_map) == policy_producers, "canonical producer set lacks an explicit stage", errors)
    require(
        all(stage in stage_map for stage in producer_stage_map.values()),
        "canonical packager stage is missing",
        errors,
    )
    require("JUDGE-PANEL-PACKAGER" in stage_map, "judge-panel packager stage is missing", errors)
    require(
        stage_map.get("CANDIDATE-MANIFEST-PACKAGER", {}).get("inputSchemas") == []
        and stage_map.get("HOLDOUT-CONSUMPTION-PACKAGER", {}).get("inputSchemas") == [
            "candidate-manifest.schema.json",
            "holdout-consumption-registry.schema.json",
        ]
        and stage_map.get("GOLD-BINDING-PACKAGER", {}).get("inputSchemas") == [
            "candidate-manifest.schema.json",
            "holdout-consumption-receipt.schema.json",
        ],
        "candidate -> receipt -> gold-binding DAG must remain exact and acyclic",
        errors,
    )
    require(
        stage_map.get("COLD-ACTOR", {}).get("inputSchemas") == [
            "candidate-manifest.schema.json",
            "holdout-consumption-receipt.schema.json",
            "gold-binding-manifest.schema.json",
        ]
        and stage_map.get("COLD-ACTOR", {}).get("modelVisibleInputSchemas") == []
        and stage_map.get("COLD-ACTOR", {}).get("outputSchema")
        == "cold-actor-response.schema.json",
        "cold actor must be exposed only after candidate, holdout claim, and gold readiness",
        errors,
    )
    require(
        stage_map.get("COLD-OBSERVATION-PACKAGER", {}).get("inputSchemas") == [
            "candidate-manifest.schema.json",
            "holdout-consumption-receipt.schema.json",
            "gold-binding-manifest.schema.json",
            "cold-actor-response.schema.json",
        ]
        and stage_map.get("COLD-OBSERVATION-PACKAGER", {}).get("outputSchema")
        == "actor-observation.schema.json"
        and stage_map.get("COLD-OBSERVATION-PACKAGER", {}).get("packagedOutputSchema")
        == "recording-manifest.schema.json"
        and stage_map.get("COLD-OBSERVATION-PACKAGER", {}).get("authorities") == [
            "canonical-hash-policy.json", "cold-journey-recipe.json",
            "concept-exposure-manifest.json", "actor-action-ledger.schema.json",
        ]
        and stage_map.get("COLD-PACKAGER", {}).get("inputSchemas") == [
            "candidate-manifest.schema.json",
            "holdout-consumption-receipt.schema.json",
            "gold-binding-manifest.schema.json",
            "cold-actor-response.schema.json",
            "actor-observation.schema.json",
            "recording-manifest.schema.json",
        ]
        and stage_map.get("COLD-PACKAGER", {}).get("packagedOutputSchema") is None,
        "cold actor response must pass through the recorder-owned observation packager",
        errors,
    )
    require(
        stage_map.get("COVERAGE-RUN-PACKAGER", {}).get("inputSchemas") == [
            "candidate-manifest.schema.json",
            "holdout-consumption-receipt.schema.json",
            "gold-binding-manifest.schema.json",
            "coverage-action-ledger.schema.json",
        ],
        "coverage trace must be derived from the opened strict action ledger",
        errors,
    )
    require(
        stage_map.get("CANDIDATE-JUDGE-INPUT-PACKAGER", {}).get("inputSchemas") == [
            "candidate-manifest.schema.json", "qualification-receipt.schema.json",
            "holdout-consumption-receipt.schema.json", "gold-binding-manifest.schema.json",
            "evidence-set.schema.json", "sanitized-evidence-bundle-manifest.schema.json",
        ]
        and stage_map.get("CANDIDATE-JUDGE", {}).get("inputSchemas") == [
            "candidate-judge-input.schema.json", "evidence-set.schema.json",
            "sanitized-evidence-bundle-manifest.schema.json",
        ]
        and stage_map.get("CANDIDATE-JUDGE", {}).get("modelVisibleInputSchemas") == [
            "candidate-judge-input.schema.json", "evidence-set.schema.json",
            "sanitized-evidence-bundle-manifest.schema.json",
        ],
        "candidate judges must receive canonical input plus exact evidence bodies after qualification PASS",
        errors,
    )
    require(
        stage_map.get("QUALIFICATION-JUDGE", {}).get("modelVisibleInputSchemas")
        == ["qualification-input.schema.json"]
        and stage_map.get("EVIDENCE-VERIFIER", {}).get("inputSchemas") == [
            "native-evidence-verification-input.schema.json", "evidence-set.schema.json",
            "sanitized-evidence-bundle-manifest.schema.json",
        ]
        and stage_map.get("EVIDENCE-VERIFIER", {}).get("modelVisibleInputSchemas") == [
            "native-evidence-verification-input.schema.json", "evidence-set.schema.json",
            "sanitized-evidence-bundle-manifest.schema.json",
        ],
        "LLM stages must expose only their exact model-visible input bodies",
        errors,
    )
    require(
        stage_map.get("JUDGE-PANEL-PACKAGER", {}).get("inputSchemas") == [
            "candidate-manifest.schema.json",
            "qualification-receipt.schema.json",
            "candidate-judge-input.schema.json",
            "evidence-set.schema.json",
            "sanitized-evidence-bundle-manifest.schema.json",
            "native-judge.schema.json",
        ],
        "judge-panel packager must bind candidate, qualification, evidence, and judgments",
        errors,
    )
    require(
        stage_map.get("EVIDENCE-SET-PACKAGER", {}).get("inputSchemas")
        == [
            "candidate-manifest.schema.json", "holdout-consumption-receipt.schema.json",
            "gold-binding-manifest.schema.json", "actor-trace.schema.json",
            "coverage-action-ledger.schema.json", "coverage-trace.schema.json",
            "recording-manifest.schema.json", "anonymization-manifest.schema.json",
        ],
        "evidence-set packager must bind cold and coverage source envelopes",
        errors,
    )
    require(
        stage_map.get("EVALUATION-RUN-PACKAGER", {}).get("inputSchemas") == [
            "candidate-manifest.schema.json",
            "holdout-consumption-receipt.schema.json",
            "gold-binding-manifest.schema.json",
            "qualification-receipt.schema.json",
            "cold-actor-response.schema.json",
            "actor-observation.schema.json",
            "actor-trace.schema.json",
            "coverage-action-ledger.schema.json",
            "coverage-trace.schema.json",
            "recording-manifest.schema.json",
            "anonymization-manifest.schema.json",
            "evidence-set.schema.json",
            "sanitized-evidence-bundle-manifest.schema.json",
            "candidate-judge-input.schema.json",
            "native-judge.schema.json",
            "judge-panel.schema.json",
            "native-evidence-verification-input.schema.json",
            "native-evidence-verifier.schema.json",
            "oracle-hard-gate-ledger.schema.json",
        ],
        "evaluation-run packager must bind every pre-aggregate execution artifact",
        errors,
    )
    require(
        stage_map.get("NATIVE-AGGREGATE", {}).get("inputSchemas") == [
            "native-aggregation-input.schema.json",
            "candidate-manifest.schema.json",
            "holdout-consumption-receipt.schema.json",
            "gold-binding-manifest.schema.json",
            "qualification-receipt.schema.json",
            "judge-panel.schema.json",
            "evaluation-run-manifest.schema.json",
            "cold-actor-response.schema.json",
            "actor-observation.schema.json",
            "actor-trace.schema.json",
            "coverage-action-ledger.schema.json",
            "coverage-trace.schema.json",
            "recording-manifest.schema.json",
            "anonymization-manifest.schema.json",
            "evidence-set.schema.json",
            "sanitized-evidence-bundle-manifest.schema.json",
            "candidate-judge-input.schema.json",
            "native-judge.schema.json",
            "native-evidence-verification-input.schema.json",
            "native-evidence-verifier.schema.json",
            "oracle-hard-gate-ledger.schema.json",
        ],
        "native aggregate stage does not bind every pre-aggregate authority",
        errors,
    )
    require(
        stage_map.get("ORACLE-HARD-GATES", {}).get("inputSchemas") == [
            "candidate-manifest.schema.json",
            "holdout-consumption-receipt.schema.json",
            "gold-binding-manifest.schema.json",
            "cold-actor-response.schema.json",
            "actor-observation.schema.json",
            "actor-trace.schema.json",
            "coverage-action-ledger.schema.json",
            "coverage-trace.schema.json",
            "recording-manifest.schema.json",
            "anonymization-manifest.schema.json",
            "evidence-set.schema.json",
            "sanitized-evidence-bundle-manifest.schema.json",
            "candidate-judge-input.schema.json",
            "native-evidence-verification-input.schema.json",
            "native-evidence-verifier.schema.json",
        ]
        and stage_map.get("ORACLE-HARD-GATES", {}).get("authorities") == [
            "gold-state-manifest.json", "coverage-recipe.json",
            "concept-exposure-manifest.json", "../rubric.json",
            "contract-bindings.json", "canonical-hash-policy.json",
            "validate-gold-state.py",
        ],
        "oracle stage does not bind its complete candidate/verifier/rubric/contract DAG",
        errors,
    )
    aggregation_inputs = stage_map.get("AGGREGATION-INPUT-PACKAGER", {}).get(
        "inputSchemas"
    )
    require(
        isinstance(aggregation_inputs, list)
        and set(aggregation_inputs)
        == set(stage_map.get("NATIVE-AGGREGATE", {}).get("inputSchemas", []))
        - {"native-aggregation-input.schema.json"},
        "aggregation-input packager must own every pre-aggregate authority",
        errors,
    )
    require(
        stage_map.get("NATIVE-AGGREGATE", {}).get("authorities")
        == [
            "../rubric.json", "../aggregate-native.py", "validate-contract.py",
            "validate-gold-state.py",
        ],
        "native aggregate stage does not bind its exact score-producing tool bytes",
        errors,
    )
    require(
        stage_map.get("PANEL-FINALIZATION-SEAL-PACKAGER", {}).get("implementationTool")
        == "../aggregate-native.py"
        and stage_map.get("PANEL-FINALIZATION-SEAL-PACKAGER", {}).get("outputSchema")
        == "panel-finalization-seal.schema.json",
        "initial/replacement scorecards must end in a canonical aggregate-produced finalization seal",
        errors,
    )
    hashed_paths = set(row_map)
    unbound_stages = set(tool_policy.get("currentlyUnboundProducerStages", []))
    for stage_id, row in stage_map.items():
        implementation_tool = row.get("implementationTool")
        if row.get("producerType") == "LLM" or stage_id in unbound_stages:
            require(
                implementation_tool is None,
                f"contract stage {stage_id} must remain honestly implementationTool=null",
                errors,
            )
        else:
            require(
                implementation_tool == "../aggregate-native.py",
                f"implemented deterministic stage {stage_id} tool binding mismatch",
                errors,
            )
        references = [
            row.get("implementationTool"), row.get("promptFile"),
            row.get("outputSchema"), row.get("packagedOutputSchema"),
        ]
        references.extend(row.get("inputSchemas", []))
        references.extend(row.get("modelVisibleInputSchemas", []))
        references.extend(row.get("authorities", []))
        require(
            all(
                reference is None
                or reference in hashed_paths
                or reference == "contract-bindings.json"
                for reference in references
            ),
            f"contract stage {stage_id} has an unhashed reference",
            errors,
        )
    require(
        stage_map.get("QUALIFICATION-JUDGE", {}).get("promptFile")
        == stage_map.get("CANDIDATE-JUDGE", {}).get("promptFile")
        == "native-judge-prompt.template.txt",
        "qualification and candidate judge must share the exact prompt",
        errors,
    )
    require(
        stage_map.get("QUALIFICATION-JUDGE", {}).get("outputSchema")
        == stage_map.get("CANDIDATE-JUDGE", {}).get("outputSchema")
        == "native-judge.schema.json",
        "qualification and candidate judge must share the exact schema",
        errors,
    )
    require(
        stage_map.get("QUALIFICATION-JUDGE", {}).get("authorities") == ["../rubric.json"],
        "qualification judge must not receive the expected-label authority or transport map",
        errors,
    )


def validate_contract(native: Path, rubric_path: Path) -> tuple[list[str], dict[str, Any]]:
    errors: list[str] = []
    required_json = [
        "concept-exposure-manifest.json",
        "coverage-recipe.json",
        "holdout-recipes.json",
        "qualification-anchors.json",
        "qualification-transport-map.json",
        "cold-journey-recipe.json",
        "canonical-hash-policy.json",
        "contract-bindings.json",
        "gold-state-manifest.json",
    ]
    documents: dict[str, dict[str, Any]] = {}
    for filename in required_json:
        try:
            documents[filename] = read_json(native / filename)
        except ContractError as error:
            errors.append(str(error))
    try:
        rubric = read_json(rubric_path)
    except ContractError as error:
        errors.append(str(error))
        rubric = {}
    if rubric:
        validate_rubric(rubric, errors)
    if all(name in documents for name in ("concept-exposure-manifest.json", "coverage-recipe.json")):
        validate_coverage_and_concepts(
            native,
            documents["concept-exposure-manifest.json"],
            documents["coverage-recipe.json"],
            errors,
        )
    if "holdout-recipes.json" in documents:
        validate_holdouts(documents["holdout-recipes.json"], errors)
    schemas = validate_schema_bindings(native, errors)
    cold_recipe = documents.get("cold-journey-recipe.json")
    cold_schema = schemas.get("cold-journey-recipe.schema.json")
    if cold_recipe is not None and cold_schema is not None:
        errors.extend(f"cold recipe {error}" for error in instance_errors(cold_recipe, cold_schema))
    if (
        cold_recipe is not None
        and "coverage-recipe.json" in documents
        and "concept-exposure-manifest.json" in documents
    ):
        validate_cold_checkpoint_sequence(
            cold_recipe,
            documents["coverage-recipe.json"],
            documents["concept-exposure-manifest.json"],
            errors,
        )
    map_document = documents.get("qualification-transport-map.json")
    map_schema = schemas.get("qualification-transport-map.schema.json")
    if map_document is not None and map_schema is not None:
        errors.extend(f"qualification map {error}" for error in instance_errors(map_document, map_schema))
    if (
        "qualification-anchors.json" in documents
        and map_document is not None
        and "qualification-input.schema.json" in schemas
        and rubric_path.is_file()
    ):
        validate_qualification(
            documents["qualification-anchors.json"],
            map_document,
            schemas["qualification-input.schema.json"],
            raw_sha256(native / "qualification-anchors.json"),
            raw_sha256(rubric_path),
            errors,
        )
    validate_prompts(native, errors)
    if "canonical-hash-policy.json" in documents:
        validate_hash_policy(native, documents["canonical-hash-policy.json"], schemas, errors)
    if "contract-bindings.json" in documents and "contract-bindings.schema.json" in schemas:
        validate_contract_bindings(
            native,
            documents["contract-bindings.json"],
            schemas["contract-bindings.schema.json"],
            schemas,
            errors,
        )

    gold = documents.get("gold-state-manifest.json", {})
    if gold:
        gold_schema = schemas.get("gold-state-manifest.schema.json")
        if gold_schema is not None:
            errors.extend(
                f"gold-state manifest {error}"
                for error in instance_errors(gold, gold_schema)
            )
        require(gold.get("candidateIndependent") is True, "gold state must remain candidate-independent", errors)
        require(gold.get("bindingComplete") is False, "pre-capture gold bindingComplete must remain false", errors)
        require(gold.get("scoreBearingCaptureAllowed") is False, "pre-capture gold must block score-bearing capture", errors)
        authorities = gold.get("authorities", {})
        if isinstance(authorities, dict):
            require(
                authorities.get("coverageRecipe", {}).get("sha256") == raw_sha256(native / "coverage-recipe.json"),
                "gold coverage recipe hash mismatch",
                errors,
            )
            require(
                authorities.get("conceptExposure", {}).get("sha256") == raw_sha256(native / "concept-exposure-manifest.json"),
                "gold concept manifest hash mismatch",
                errors,
            )

    summary = {
        "status": "PASS" if not errors else "FAIL",
        "nativeCellCount": len(ALL_CELLS),
        "coldAssignedCellCount": len(COLD_CELLS),
        "coverageAssignedCellCount": len(COVERAGE_CELLS),
        "probeCount": len(documents.get("concept-exposure-manifest.json", {}).get("probes", [])),
        "episodeCount": len(documents.get("coverage-recipe.json", {}).get("episodes", [])),
        "holdoutCount": len(documents.get("holdout-recipes.json", {}).get("holdouts", [])),
        "qualificationAnchorCount": len(documents.get("qualification-anchors.json", {}).get("anchors", [])),
        "schemaCount": len(schemas),
        "scoreBearingReady": False,
        "scoreBearingBlocker": (
            "gold-state bindings/E09 witness and unimplemented producer-tool raw hashes "
            "remain blocked pre-capture"
        ),
    }
    return errors, summary


def validate_runtime_contract_bytes(
    native: Path,
    rubric_path: Path,
    *,
    candidate_manifest_bytes: bytes | None = None,
    qualification_receipt_bytes: bytes | None = None,
    gold_binding_manifest_bytes: bytes | None = None,
    holdout_consumption_receipt_bytes: bytes | None = None,
    registry_before_bytes: bytes | None = None,
    registry_after_bytes: bytes | None = None,
    candidate_manifest_path_label: Path | None = None,
    qualification_receipt_path_label: Path | None = None,
    gold_binding_manifest_path_label: Path | None = None,
    holdout_consumption_receipt_path_label: Path | None = None,
    registry_before_path_label: Path | None = None,
    registry_after_path_label: Path | None = None,
) -> tuple[list[str], dict[str, Any]]:
    """Validate exact runtime bytes supplied by an already-opened caller.

    Runtime path arguments are diagnostic/canonical-path labels only.  This API
    never reopens them, so a caller can bind the result to the exact bytes it
    previously read.  Candidate execution components and checked-in contract
    authorities remain independently opened because their bytes are themselves
    the semantic authorities being recomputed.
    """

    native = native.resolve()
    rubric_path = rubric_path.resolve()
    errors, summary = validate_contract(native, rubric_path)
    observed: dict[str, str] = {}

    def parse_runtime(
        data: bytes | None,
        key: str,
        default_label: str,
        path_label: Path | None,
    ) -> dict[str, Any] | None:
        if data is None:
            return None
        observed[key] = bytes_sha256(data)
        label = str(path_label) if path_label is not None else default_label
        try:
            return read_json_bytes(data, label)
        except ContractError as error:
            errors.append(str(error))
            return None

    candidate = parse_runtime(
        candidate_manifest_bytes,
        "candidateManifestRawSha256",
        "candidate manifest bytes",
        candidate_manifest_path_label,
    )
    if candidate_manifest_bytes is not None and candidate is not None:
        try:
            schema = read_json(native / "candidate-manifest.schema.json")
        except ContractError as error:
            errors.append(str(error))
        else:
            errors.extend(
                f"candidate manifest {error}"
                for error in instance_errors(candidate, schema)
            )
            validate_candidate_manifest_semantics(candidate, native, rubric_path, errors)
            expected = candidate.get("candidateManifestSha256")
            try:
                actual = self_hash(candidate, "candidateManifestSha256")
            except ContractError as error:
                errors.append(str(error))
            else:
                require(
                    expected == actual,
                    "candidate manifest canonical self-hash mismatch",
                    errors,
                )

    qualification = parse_runtime(
        qualification_receipt_bytes,
        "qualificationReceiptRawSha256",
        "qualification receipt bytes",
        qualification_receipt_path_label,
    )
    if qualification_receipt_bytes is not None and qualification is not None:
        try:
            schema = read_json(native / "qualification-receipt.schema.json")
        except ContractError as error:
            errors.append(str(error))
        else:
            errors.extend(
                f"qualification receipt {error}"
                for error in instance_errors(qualification, schema)
            )
            validate_qualification_receipt_semantics(qualification, errors)
            try:
                actual = self_hash(qualification, "qualificationReceiptSha256")
            except ContractError as error:
                errors.append(str(error))
            else:
                require(
                    qualification.get("qualificationReceiptSha256") == actual,
                    "qualification receipt canonical self-hash mismatch",
                    errors,
                )

    binding = parse_runtime(
        gold_binding_manifest_bytes,
        "goldBindingManifestRawSha256",
        "gold binding manifest bytes",
        gold_binding_manifest_path_label,
    )
    if gold_binding_manifest_bytes is not None and binding is not None:
        try:
            binding_schema = read_json(native / "gold-binding-manifest.schema.json")
        except ContractError as error:
            errors.append(str(error))
        else:
            errors.extend(
                f"gold binding manifest {error}"
                for error in instance_errors(binding, binding_schema)
            )
            try:
                binding_self_hash = self_hash(
                    binding,
                    "goldBindingManifestSha256",
                )
            except ContractError as error:
                errors.append(str(error))
            else:
                require(
                    binding.get("goldBindingManifestSha256") == binding_self_hash,
                    "gold binding manifest canonical self-hash mismatch",
                    errors,
                )
            require(
                binding.get("goldBindingSchemaSha256")
                == raw_sha256(native / "gold-binding-manifest.schema.json"),
                "gold binding manifest schema raw SHA mismatch",
                errors,
            )
            if candidate is not None:
                require(
                    binding.get("candidateManifestSha256")
                    == candidate.get("candidateManifestSha256")
                    and binding.get("selectedRecipeId")
                    == candidate.get("recipes", {}).get("selectedRecipeId")
                    and binding.get("selectedRecipeSha256")
                    == candidate.get("recipes", {}).get("selectedRecipeSha256")
                    and binding.get("executionArtifactSha256")
                    == candidate.get("execution", {}).get("executionArtifactSha256"),
                    "gold binding manifest candidate/recipe/execution binding mismatch",
                    errors,
                )

    receipt = parse_runtime(
        holdout_consumption_receipt_bytes,
        "holdoutConsumptionReceiptRawSha256",
        "holdout consumption receipt bytes",
        holdout_consumption_receipt_path_label,
    )
    registry_before = parse_runtime(
        registry_before_bytes,
        "registryBeforeRawSha256",
        "registry before bytes",
        registry_before_path_label,
    )
    registry_after = parse_runtime(
        registry_after_bytes,
        "registryAfterRawSha256",
        "registry after bytes",
        registry_after_path_label,
    )
    if holdout_consumption_receipt_bytes is not None and receipt is not None:
        try:
            receipt_schema = read_json(
                native / "holdout-consumption-receipt.schema.json"
            )
            registry_schema = read_json(
                native / "holdout-consumption-registry.schema.json"
            )
            queue = read_json(native / "holdout-recipes.json")
        except ContractError as error:
            errors.append(str(error))
        else:
            queue["_observedRawSha256"] = raw_sha256(
                native / "holdout-recipes.json"
            )
            errors.extend(
                f"holdout consumption receipt {error}"
                for error in instance_errors(receipt, receipt_schema)
            )
            try:
                receipt_self_hash = self_hash(
                    receipt,
                    "holdoutConsumptionReceiptSha256",
                )
            except ContractError as error:
                errors.append(str(error))
            else:
                require(
                    receipt.get("holdoutConsumptionReceiptSha256")
                    == receipt_self_hash,
                    "holdout consumption receipt canonical self-hash mismatch",
                    errors,
                )
            for label, registry, raw_bytes, raw_field, self_field in (
                (
                    "registry before",
                    registry_before,
                    registry_before_bytes,
                    "registryBeforeRawSha256",
                    "registryBeforeSha256",
                ),
                (
                    "registry after",
                    registry_after,
                    registry_after_bytes,
                    "registryAfterRawSha256",
                    "registryAfterSha256",
                ),
            ):
                if registry is None or raw_bytes is None:
                    continue
                errors.extend(
                    f"{label} {error}"
                    for error in instance_errors(registry, registry_schema)
                )
                try:
                    registry_self_hash = self_hash(
                        registry,
                        "holdoutConsumptionRegistrySha256",
                    )
                except ContractError as error:
                    errors.append(str(error))
                else:
                    require(
                        registry.get("holdoutConsumptionRegistrySha256")
                        == registry_self_hash,
                        f"{label} canonical self-hash mismatch",
                        errors,
                    )
                atomic = receipt.get("atomicClaim", {})
                require(
                    isinstance(atomic, dict)
                    and atomic.get(raw_field) == bytes_sha256(raw_bytes)
                    and atomic.get(self_field)
                    == registry.get("holdoutConsumptionRegistrySha256"),
                    f"{label} raw/self receipt binding mismatch",
                    errors,
                )
            if holdout_consumption_receipt_path_label is None:
                errors.append(
                    "holdout receipt exact-byte validation requires its canonical path label"
                )
            else:
                validate_holdout_consumption_semantics(
                    receipt,
                    holdout_consumption_receipt_path_label,
                    native,
                    candidate,
                    queue,
                    registry_before,
                    registry_after,
                    errors,
                )

    summary["observedRawSha256"] = observed
    summary["status"] = "PASS" if not errors else "FAIL"
    return errors, summary


def _read_cli_bytes(
    path: Path | None,
    label: str,
    errors: list[str],
) -> tuple[bytes | None, Path | None]:
    if path is None:
        return None, None
    resolved = path.resolve(strict=False)
    try:
        return resolved.read_bytes(), resolved
    except OSError as error:
        errors.append(f"cannot read {label} {resolved}: {error}")
        return None, resolved


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    default_native = Path(__file__).resolve().parent
    parser.add_argument("--native-dir", type=Path, default=default_native)
    parser.add_argument("--rubric", type=Path, default=default_native.parent / "rubric.json")
    parser.add_argument("--candidate-manifest", type=Path)
    parser.add_argument("--qualification-receipt", type=Path)
    parser.add_argument("--gold-binding-manifest", type=Path)
    parser.add_argument("--holdout-consumption-receipt", type=Path)
    parser.add_argument("--registry-before", type=Path)
    parser.add_argument("--registry-after", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    native = args.native_dir.resolve()
    cli_errors: list[str] = []
    candidate_bytes, candidate_path = _read_cli_bytes(
        args.candidate_manifest,
        "candidate manifest",
        cli_errors,
    )
    qualification_bytes, qualification_path = _read_cli_bytes(
        args.qualification_receipt,
        "qualification receipt",
        cli_errors,
    )
    binding_bytes, binding_path = _read_cli_bytes(
        args.gold_binding_manifest,
        "gold binding manifest",
        cli_errors,
    )
    receipt_bytes, receipt_path = _read_cli_bytes(
        args.holdout_consumption_receipt,
        "holdout consumption receipt",
        cli_errors,
    )
    registry_before_bytes, registry_before_path = _read_cli_bytes(
        args.registry_before,
        "registry before",
        cli_errors,
    )
    registry_after_bytes, registry_after_path = _read_cli_bytes(
        args.registry_after,
        "registry after",
        cli_errors,
    )
    errors, summary = validate_runtime_contract_bytes(
        native,
        args.rubric.resolve(),
        candidate_manifest_bytes=candidate_bytes,
        qualification_receipt_bytes=qualification_bytes,
        gold_binding_manifest_bytes=binding_bytes,
        holdout_consumption_receipt_bytes=receipt_bytes,
        registry_before_bytes=registry_before_bytes,
        registry_after_bytes=registry_after_bytes,
        candidate_manifest_path_label=candidate_path,
        qualification_receipt_path_label=qualification_path,
        gold_binding_manifest_path_label=binding_path,
        holdout_consumption_receipt_path_label=receipt_path,
        registry_before_path_label=registry_before_path,
        registry_after_path_label=registry_after_path,
    )
    errors = [*cli_errors, *errors]
    summary["status"] = "PASS" if not errors else "FAIL"
    if args.json:
        print(json.dumps({**summary, "errors": errors}, ensure_ascii=False, sort_keys=True))
    elif errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
    else:
        print(
            "PASS native evaluator contract: "
            f"cells={summary['nativeCellCount']} "
            f"cold={summary['coldAssignedCellCount']} "
            f"coverage={summary['coverageAssignedCellCount']} "
            f"probes={summary['probeCount']} episodes={summary['episodeCount']} "
            f"holdouts={summary['holdoutCount']} anchors={summary['qualificationAnchorCount']} "
            f"schemas={summary['schemaCount']} scoreBearing=BLOCKED_PRE_CAPTURE"
        )
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
