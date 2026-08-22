#!/usr/bin/env python3
"""Deterministically aggregate the Gridworks commercial-UX native panel.

This module implements the frozen v1/v1.1 arithmetic and fail-closed provenance
checks.  ``native-scorecard.schema.json`` remains read-only here and is enforced
for every result in addition to the aggregation-specific invariants below.  Its
contract includes non-official formative scoring and the single permitted lane
replacement lifecycle.

The candidate-provenance input is a small aggregation envelope, not the product
candidate manifest itself.  It binds the already-packaged candidate/evidence
artifacts and contains only deterministic aggregation metadata::

    {
      "schemaVersion": "gridworks.commercial-ux.native-aggregation-input.v1",
      "protocol": "GRIDWORKS-COMMERCIAL-UX-v1.1",
      "scorecardId": "...",
      "recipeId": "FORMATIVE-01|HOLDOUT-01..08",
      "operationalBlocker": null,
      "verificationInputSha256": "sha256:...",
      "expectedObservationIds": ["OBS-0001", ...],
      "notReachedByProductCellIds": [],
      "artifactBindings": [
        {"anonymousArtifactId":"ARTIFACT-A", "artifactKind":"COLD_ACTOR",
         "artifactSha256":"sha256:..."}, ...
      ],
      "differenceReport": {"items":[], "openP0":0, "openP1":0, "openP2":0},
      "provenance": { ... scorecard provenance fields except judgePanelSha256 ... }
    }

Only HOLDOUT recipes can emit an official PASS.  FORMATIVE-01 still receives
the full numeric native score, but emits ``status=SCORED_FORMATIVE``,
``verdict=null``, and ``officialCommercialUX=false``.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import importlib.util
import io
import json
import os
import re
import struct
import subprocess
import sys
import wave
import zlib
from pathlib import Path
from typing import Any, Iterable


TOOL_DIRECTORY = Path(__file__).resolve().parent
NATIVE_DIRECTORY = TOOL_DIRECTORY / "native"
REPOSITORY_ROOT = TOOL_DIRECTORY.parents[1]
DEFAULT_RUBRIC_PATH = TOOL_DIRECTORY / "rubric.json"
SCORECARD_SCHEMA_PATH = NATIVE_DIRECTORY / "native-scorecard.schema.json"
AGGREGATION_INPUT_SCHEMA_PATH = NATIVE_DIRECTORY / "native-aggregation-input.schema.json"
REPLACEMENT_RECEIPT_SCHEMA_PATH = NATIVE_DIRECTORY / "native-replacement-receipt.schema.json"
CANDIDATE_MANIFEST_SCHEMA_PATH = NATIVE_DIRECTORY / "candidate-manifest.schema.json"
QUALIFICATION_RECEIPT_SCHEMA_PATH = NATIVE_DIRECTORY / "qualification-receipt.schema.json"
JUDGE_PANEL_SCHEMA_PATH = NATIVE_DIRECTORY / "judge-panel.schema.json"
NATIVE_JUDGE_SCHEMA_PATH = NATIVE_DIRECTORY / "native-judge.schema.json"
EVALUATION_RUN_SCHEMA_PATH = NATIVE_DIRECTORY / "evaluation-run-manifest.schema.json"
ACTOR_OBSERVATION_SCHEMA_PATH = NATIVE_DIRECTORY / "actor-observation.schema.json"
COLD_ACTOR_RESPONSE_SCHEMA_PATH = (
    NATIVE_DIRECTORY / "cold-actor-response.schema.json"
)
ACTOR_ACTION_LEDGER_SCHEMA_PATH = (
    NATIVE_DIRECTORY / "actor-action-ledger.schema.json"
)
ACTOR_TRACE_SCHEMA_PATH = NATIVE_DIRECTORY / "actor-trace.schema.json"
ANONYMIZATION_MANIFEST_SCHEMA_PATH = (
    NATIVE_DIRECTORY / "anonymization-manifest.schema.json"
)
COVERAGE_TRACE_SCHEMA_PATH = NATIVE_DIRECTORY / "coverage-trace.schema.json"
EVIDENCE_SET_SCHEMA_PATH = NATIVE_DIRECTORY / "evidence-set.schema.json"
ORACLE_LEDGER_SCHEMA_PATH = (
    NATIVE_DIRECTORY / "oracle-hard-gate-ledger.schema.json"
)
RECORDING_MANIFEST_SCHEMA_PATH = NATIVE_DIRECTORY / "recording-manifest.schema.json"
COVERAGE_ACTION_LEDGER_SCHEMA_PATH = (
    NATIVE_DIRECTORY / "coverage-action-ledger.schema.json"
)
SANITIZED_EVIDENCE_BUNDLE_MANIFEST_SCHEMA_PATH = (
    NATIVE_DIRECTORY / "sanitized-evidence-bundle-manifest.schema.json"
)
CANDIDATE_JUDGE_INPUT_SCHEMA_PATH = (
    NATIVE_DIRECTORY / "candidate-judge-input.schema.json"
)
PANEL_FINALIZATION_SEAL_SCHEMA_PATH = (
    NATIVE_DIRECTORY / "panel-finalization-seal.schema.json"
)
VERIFICATION_INPUT_SCHEMA_PATH = (
    NATIVE_DIRECTORY / "native-evidence-verification-input.schema.json"
)
GOLD_BINDING_SCHEMA_PATH = NATIVE_DIRECTORY / "gold-binding-manifest.schema.json"
HOLDOUT_CONSUMPTION_RECEIPT_SCHEMA_PATH = (
    NATIVE_DIRECTORY / "holdout-consumption-receipt.schema.json"
)
HOLDOUT_CONSUMPTION_REGISTRY_SCHEMA_PATH = (
    NATIVE_DIRECTORY / "holdout-consumption-registry.schema.json"
)
CONTRACT_BINDINGS_PATH = NATIVE_DIRECTORY / "contract-bindings.json"
GOLD_STATE_MANIFEST_PATH = NATIVE_DIRECTORY / "gold-state-manifest.json"
GOLD_STATE_VALIDATOR_PATH = NATIVE_DIRECTORY / "validate-gold-state.py"
CONCEPT_MANIFEST_PATH = NATIVE_DIRECTORY / "concept-exposure-manifest.json"
COLD_RECIPE_PATH = NATIVE_DIRECTORY / "cold-journey-recipe.json"
HOLDOUT_QUEUE_PATH = NATIVE_DIRECTORY / "holdout-recipes.json"
CONTRACT_VALIDATOR_PATH = NATIVE_DIRECTORY / "validate-contract.py"
SESSION_CLAIM_TOOL_PATH = NATIVE_DIRECTORY / "claim-evaluation-session.py"

PROTOCOL = "GRIDWORKS-COMMERCIAL-UX-v1.1"
SCORECARD_SCHEMA = "gridworks.commercial-ux.native-scorecard.v1"
AGGREGATION_INPUT_SCHEMA = "gridworks.commercial-ux.native-aggregation-input.v1"
JUDGMENT_SCHEMA = "gridworks.commercial-ux.native-judgment.v1"
VERIFICATION_SCHEMA = "gridworks.commercial-ux.native-evidence-verification.v1"
ORACLE_LEDGER_SCHEMA = "gridworks.commercial-ux.native-oracle-hard-gate-ledger.v1"
RECEIPT_SCHEMA = "gridworks.commercial-ux.native-replacement-receipt.v1"
CANDIDATE_MANIFEST_SCHEMA = "gridworks.commercial-ux.candidate-manifest.v1"
QUALIFICATION_RECEIPT_SCHEMA = "gridworks.commercial-ux.native-qualification-receipt.v1"
JUDGE_PANEL_MANIFEST_SCHEMA = "gridworks.commercial-ux.native-judge-panel.v1"
EVALUATION_RUN_SCHEMA = "gridworks.commercial-ux.evaluation-run-manifest.v1"
ACTOR_OBSERVATION_SCHEMA = "gridworks.commercial-ux.native-actor-observation.v1"
COLD_ACTOR_RESPONSE_SCHEMA = (
    "gridworks.commercial-ux.native-cold-actor-response.v1"
)
ACTOR_ACTION_LEDGER_SCHEMA = (
    "gridworks.commercial-ux.native-actor-action-ledger.v1"
)
ACTOR_TRACE_SCHEMA = "gridworks.commercial-ux.native-actor-trace.v1"
ANONYMIZATION_MANIFEST_SCHEMA = "gridworks.commercial-ux.anonymization-manifest.v1"
COVERAGE_TRACE_SCHEMA = "gridworks.commercial-ux.native-coverage-trace.v1"
EVIDENCE_SET_SCHEMA = "gridworks.commercial-ux.native-evidence-set.v1"
RECORDING_MANIFEST_SCHEMA = "gridworks.commercial-ux.recording-manifest.v1"
COVERAGE_ACTION_LEDGER_SCHEMA = "gridworks.commercial-ux.coverage-action-ledger.v1"
SANITIZED_EVIDENCE_BUNDLE_MANIFEST_SCHEMA = (
    "gridworks.commercial-ux.sanitized-evidence-bundle-manifest.v1"
)
CANDIDATE_JUDGE_INPUT_SCHEMA = "gridworks.commercial-ux.candidate-judge-input.v1"
PANEL_FINALIZATION_SEAL_SCHEMA = (
    "gridworks.commercial-ux.panel-finalization-seal.v1"
)
VERIFICATION_INPUT_SCHEMA = (
    "gridworks.commercial-ux.native-evidence-verification-input.v1"
)
GOLD_BINDING_SCHEMA = "gridworks.commercial-ux.gold-binding-manifest.v1"
HOLDOUT_CONSUMPTION_RECEIPT_SCHEMA = (
    "gridworks.commercial-ux.holdout-consumption-receipt.v1"
)
HOLDOUT_CONSUMPTION_REGISTRY_SCHEMA = (
    "gridworks.commercial-ux.holdout-consumption-registry.v1"
)

SHA256_PATTERN = re.compile(r"^sha256:[0-9a-f]{64}$")
SOURCE_COMMIT_PATTERN = re.compile(r"^[0-9a-f]{40}$")
RECIPE_PATTERN = re.compile(r"^(FORMATIVE-01|HOLDOUT-0[1-8])$")
OBSERVATION_ID_PATTERN = re.compile(r"^OBS-[0-9]{4}$")
INCIDENT_KEY_PATTERN = re.compile(
    r"^[A-Z0-9_-]+/[A-Z0-9_-]+/[A-Z0-9_-]+/[A-Z0-9_-]+$"
)

LABELS = {
    "EXCELLENT": (4, 100),
    "STRONG": (3, 85),
    "SERVICEABLE": (2, 70),
    "WEAK": (1, 40),
    "BROKEN": (0, 0),
}

COLD_CELLS = (
    "J1", "J2", "J3", "J4",
    "T1", "T2", "T3",
    "H1", "H2", "H3", "H4",
    "I1", "I2", "I3",
    "C1", "C2", "C3", "C4", "C5",
    "A1", "A2", "A3", "A4",
    "P1", "P2", "P3",
    "R2",
    "K1", "K2", "K3",
)

COVERAGE_CELLS = (
    "J2", "J4",
    "T1", "T2", "T3",
    "H1", "H2", "H3", "H4",
    "I1", "I2", "I3",
    "C1", "C2", "C3", "C4", "C5",
    "A1", "A2", "A3", "A4",
    "V1", "V2", "V3", "V4",
    "R1", "R2", "R3",
    "L1", "L2", "L3",
    "K1", "K2", "K3",
)

ALL_CELLS = (
    "J1", "J2", "J3", "J4",
    "T1", "T2", "T3",
    "H1", "H2", "H3", "H4",
    "I1", "I2", "I3",
    "C1", "C2", "C3", "C4", "C5",
    "A1", "A2", "A3", "A4",
    "P1", "P2", "P3",
    "V1", "V2", "V3", "V4",
    "R1", "R2", "R3",
    "L1", "L2", "L3",
    "K1", "K2", "K3",
)

HARD_GATE_IDS = (
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
)

OPERATIONAL_BLOCKERS = {
    "BLOCKED_JUDGE_UNAVAILABLE",
    "BLOCKED_JUDGE_SCHEMA",
    "BLOCKED_MISSING_EVIDENCE",
    "BLOCKED_HARNESS",
    "BLOCKED_HOLDOUT_EXHAUSTED",
}

BLOCKER_PRECEDENCE = (
    "BLOCKED_JUDGE_UNAVAILABLE",
    "BLOCKED_JUDGE_QUALIFICATION",
    "BLOCKED_JUDGE_SCHEMA",
    "BLOCKED_JUDGE_INSTABILITY",
    "BLOCKED_EVIDENCE_VERIFICATION",
    "BLOCKED_MISSING_EVIDENCE",
    "BLOCKED_HARNESS",
    "BLOCKED_HOLDOUT_EXHAUSTED",
)

CANDIDATE_KEYS = {
    "schemaVersion",
    "protocol",
    "scorecardId",
    "recipeId",
    "operationalBlocker",
    "verificationInputSha256",
    "expectedObservationIds",
    "notReachedByProductCellIds",
    "artifactBindings",
    "differenceReport",
    "provenance",
}

INPUT_PROVENANCE_KEYS = {
    "candidateManifestSha256",
    "candidateManifestRawSha256",
    "qualificationReceiptSha256",
    "qualificationReceiptRawSha256",
    "holdoutConsumptionReceiptSha256",
    "holdoutConsumptionReceiptRawSha256",
    "goldBindingManifestSha256",
    "goldBindingManifestRawSha256",
    "coldActorResponseSha256",
    "coldActorResponseRawSha256",
    "judgePanelSha256",
    "judgePanelRawSha256",
    "evaluationRunManifestSha256",
    "evaluationRunManifestRawSha256",
    "sourceCommit",
    "cleanTree",
    "model",
    "reasoningEffort",
    "promptTemplateSha256",
    "judgmentSchemaSha256",
    "verifierPromptTemplateSha256",
    "verifierSchemaSha256",
    "rubricSha256",
    "coldRecipeSha256",
    "coverageRecipeSha256",
    "holdoutRecipeSha256",
    "anonymizationManifestSha256",
    "anonymizationManifestRawSha256",
    "evidenceSetSha256",
    "evidenceSetRawSha256",
    "sanitizedEvidenceBundleManifestSha256",
    "sanitizedEvidenceBundleManifestRawSha256",
    "sanitizedEvidenceContentRootSha256",
    "coverageActionLedgerSha256",
    "coverageActionLedgerRawSha256",
    "candidateJudgeInputSha256",
    "candidateJudgeInputRawSha256",
    "verificationInputRawSha256",
    "verificationOutputSha256",
    "oracleHardGateLedgerSha256",
    "nativeAggregatorSha256",
    "executionArtifactSha256",
    "packageSha256",
    "packageStatus",
    "evaluationSessionClaimSha256",
    "evaluationSessionClaimRawSha256",
    "evaluationSessionPolicySha256",
    "evaluationSessionClaimToolSha256",
    "evaluationSessionId",
    "evaluationSessionMode",
    "evaluationAttemptAuditSha256",
    "evaluationSelectedAttemptsSha256",
}

OUTPUT_PROVENANCE_KEYS = INPUT_PROVENANCE_KEYS | {
    "aggregationInputRawSha256",
    "verificationInputSha256",
    "laneExecutionIdentities",
}

JUDGMENT_KEYS = {
    "schemaVersion",
    "protocol",
    "judgmentMode",
    "judgeRunId",
    "judgeSlot",
    "model",
    "reasoningEffort",
    "promptTemplateSha256",
    "judgmentSchemaSha256",
    "rubricSha256",
    "judgeInputSha256",
    "evidenceSetSha256",
    "artifactJudgments",
}

ARTIFACT_JUDGMENT_KEYS = {
    "anonymousArtifactId",
    "artifactKind",
    "artifactSha256",
    "cells",
}

CELL_KEYS = {
    "cellId",
    "label",
    "confidence",
    "strengthEvidence",
    "gapEvidence",
    "incidentKeys",
    "recommendedChange",
}

EVIDENCE_KEYS = {"checkpoint", "artifact", "observation"}

VERIFIER_KEYS = {
    "schemaVersion",
    "protocol",
    "verifierRunId",
    "verifierSlot",
    "model",
    "reasoningEffort",
    "promptTemplateSha256",
    "verifierSchemaSha256",
    "verificationInputSchemaSha256",
    "verificationInputSha256",
    "evidenceSetSha256",
    "opaqueJudgePanelSha256",
    "observations",
}

VERIFIER_ROW_KEYS = {
    "observationId",
    "claimType",
    "incidentKey",
    "verdict",
    "citedSources",
    "rationale",
}
VERIFIER_SOURCE_KEYS = {"anonymousArtifactId", "artifactId", "locator"}

LEDGER_KEYS = {
    "schemaVersion",
    "protocol",
    "ledgerId",
    "candidateManifestSha256",
    "holdoutConsumptionReceiptSha256",
    "goldBindingManifestSha256",
    "coldActorResponseSha256",
    "coldActorResponseRawSha256",
    "actorTraceSha256",
    "coverageActionLedgerSha256",
    "coverageArtifactId",
    "recordingManifestSha256",
    "anonymizationManifestSha256",
    "evidenceSetSha256",
    "sanitizedEvidenceBundleManifestSha256",
    "sanitizedEvidenceContentRootSha256",
    "candidateJudgeInputSha256",
    "verificationInputSha256",
    "verificationOutputSha256",
    "rubricSha256",
    "contractBindingsSha256",
    "canonicalHashPolicySha256",
    "goldStateContractSha256",
    "coverageRecipeSha256",
    "conceptManifestSha256",
    "nativeAggregatorSha256",
    "contractValidatorSha256",
    "goldValidatorSha256",
    "oracleChecks",
    "hardGates",
    "incidents",
    "scoreBearingReady",
}

ORACLE_CHECK_KEYS = {
    "oracleCheckId",
    "domain",
    "inputHashes",
    "expectedCanonicalSha256",
    "observedCanonicalSha256",
    "status",
    "details",
    "evidenceRefs",
}

HARD_GATE_KEYS = {
    "gateId",
    "producer",
    "predicate",
    "inputHashes",
    "status",
    "observed",
    "failureCode",
    "evidenceRefs",
}

INCIDENT_KEYS = {
    "incidentKey",
    "incidentType",
    "actorArtifactIds",
    "checkpointRefs",
    "verifierObservationId",
    "verifierStatus",
    "oracleStatus",
    "capCandidate",
    "critical",
    "description",
}

ARTIFACT_REF_KEYS = {"artifactId", "locator", "sha256"}

OUTPUT_KEYS = {
    "schemaVersion",
    "protocol",
    "metric",
    "scorecardId",
    "recipeId",
    "officialCommercialUX",
    "status",
    "verdict",
    "commercialUXProxy",
    "rawCommercialUX",
    "rawSpread",
    "disagreementPenalty",
    "activeCap",
    "cellScores",
    "categoryScores",
    "hardGates",
    "criticalIncidents",
    "differenceReport",
    "qualificationStatus",
    "evidenceVerificationStatus",
    "stabilityStatus",
    "provenance",
    "humanValidationStatus",
    "panelKind",
    "judgeRunIds",
    "judgePanelSha256",
    "panelArtifactBindings",
    "laneInputs",
    "unstableLanes",
    "replacementRequiredLanes",
    "rerunRequired",
    "replacementForPanelSha256",
    "replacementReceiptPath",
    "replacementReceiptSha256",
    "verificationInputSha256",
}


class ValidationFailure(Exception):
    """Base class for fail-closed contract validation."""


class JudgmentValidationFailure(ValidationFailure):
    """A judge transport does not satisfy the strict score-bearing contract."""


class VerifierValidationFailure(ValidationFailure):
    """A verifier transport does not satisfy the strict evidence contract."""


class ProvenanceFailure(ValidationFailure):
    """Two supposedly bound artifacts disagree on identity or hash."""


class DuplicateJSONKey(ValueError):
    """Strict JSON input contained the same object member more than once."""


def reject_duplicate_json_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    value: dict[str, Any] = {}
    for key, item in pairs:
        if key in value:
            raise DuplicateJSONKey(f"duplicate JSON key: {key}")
        value[key] = item
    return value


def reject_nonfinite_json_constant(value: str) -> None:
    raise ValueError(f"non-finite JSON constant: {value}")


def canonical_json_bytes(value: Any) -> bytes:
    """Canonical bytes for the contract's JSON-only hash payloads.

    Score-bearing hash payloads contain strings, integers, booleans, nulls, arrays,
    and objects; no non-integral JSON number is admitted before hashing.
    """

    return json.dumps(
        value,
        allow_nan=False,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def canonical_sha256(value: Any) -> str:
    return "sha256:" + hashlib.sha256(canonical_json_bytes(value)).hexdigest()


def bytes_sha256(value: bytes) -> str:
    return "sha256:" + hashlib.sha256(value).hexdigest()


def self_sha256(value: Any, field: str, label: str) -> str:
    if not isinstance(value, dict) or field not in value:
        raise ValidationFailure(f"{label} has no designated self-hash field {field}")
    payload = copy.deepcopy(value)
    payload[field] = None
    return canonical_sha256(payload)


def file_sha256(path: Path, label: str) -> str:
    try:
        return bytes_sha256(path.read_bytes())
    except OSError as exception:
        raise ValidationFailure(f"{label} is unreadable: {exception}") from exception


def parse_strict_json_bytes(content: bytes, label: str) -> Any:
    try:
        return json.loads(
            content.decode("utf-8"),
            object_pairs_hook=reject_duplicate_json_keys,
            parse_constant=reject_nonfinite_json_constant,
        )
    except (UnicodeDecodeError, json.JSONDecodeError, DuplicateJSONKey, ValueError) as exception:
        raise ValidationFailure(f"{label} is not strict UTF-8 JSON: {exception}") from exception


def read_json_bytes(path: Path, label: str) -> tuple[Any, bytes]:
    try:
        content = path.read_bytes()
    except OSError as exception:
        raise ValidationFailure(f"{label} is unreadable: {exception}") from exception
    return parse_strict_json_bytes(content, label), content


def read_json_attempt(
    path: Path,
    label: str,
    *,
    slot_id: str | None = None,
    capture_unreadable: bool = False,
) -> dict[str, Any]:
    resolved_path = str(path.resolve(strict=False))
    try:
        content = path.read_bytes()
    except OSError as exception:
        if capture_unreadable:
            return {
                "path": path,
                "resolvedPath": resolved_path,
                "slotId": slot_id,
                "readStatus": "INPUT_UNREADABLE",
                "rawBytes": None,
                "rawSha256": None,
                "value": None,
                "attemptOutcome": "INPUT_UNREADABLE",
                "failureCode": f"INPUT_UNREADABLE:{type(exception).__name__}:{exception}"[:300],
            }
        raise ValidationFailure(f"{label} is unreadable and has no claimable raw bytes: {exception}") from exception
    result: dict[str, Any] = {
        "path": path,
        "resolvedPath": resolved_path,
        "slotId": slot_id,
        "readStatus": "READ",
        "rawBytes": content,
        "rawSha256": bytes_sha256(content),
        "value": None,
        "attemptOutcome": "VALID",
        "failureCode": None,
    }
    try:
        text_value = content.decode("utf-8")
    except UnicodeDecodeError as exception:
        result["attemptOutcome"] = "TRANSPORT_FAILURE"
        result["failureCode"] = f"UTF8_DECODE:{exception.start}"
        return result
    try:
        result["value"] = json.loads(
            text_value,
            object_pairs_hook=reject_duplicate_json_keys,
            parse_constant=reject_nonfinite_json_constant,
        )
    except (json.JSONDecodeError, DuplicateJSONKey, ValueError) as exception:
        result["attemptOutcome"] = "SCHEMA_FAILURE"
        result["failureCode"] = f"STRICT_JSON:{type(exception).__name__}"
    return result


def exact_keys(value: Any, expected: set[str], label: str, error=ValidationFailure) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise error(f"{label} must be an object")
    actual = set(value)
    if actual != expected:
        raise error(
            f"{label} keys mismatch: missing={sorted(expected - actual)}, "
            f"extra={sorted(actual - expected)}"
        )
    return value


def require_string(value: Any, label: str, maximum: int = 1600, error=ValidationFailure) -> str:
    if not isinstance(value, str) or not value.strip() or len(value) > maximum:
        raise error(f"{label} must be a nonempty string of at most {maximum} characters")
    return value


def require_sha(value: Any, label: str, error=ValidationFailure) -> str:
    if not isinstance(value, str) or SHA256_PATTERN.fullmatch(value) is None:
        raise error(f"{label} must be a lowercase sha256 identifier")
    return value


def require_unique_strings(
    value: Any,
    label: str,
    *,
    minimum: int = 0,
    maximum: int = 4096,
    pattern: re.Pattern[str] | None = None,
    error=ValidationFailure,
) -> list[str]:
    if not isinstance(value, list) or not minimum <= len(value) <= maximum:
        raise error(f"{label} must contain {minimum}..{maximum} rows")
    if any(not isinstance(row, str) for row in value):
        raise error(f"{label} must contain only strings")
    if len(set(value)) != len(value):
        raise error(f"{label} must not contain duplicates")
    if pattern is not None and any(pattern.fullmatch(row) is None for row in value):
        raise error(f"{label} contains a value outside its frozen pattern")
    return value


def rounded(value: float) -> float:
    return round(value, 4)


def median_three(values: Iterable[float]) -> float:
    rows = sorted(values)
    if len(rows) != 3:
        raise ValidationFailure("numeric median requires exactly three rows")
    return rows[1]


def _validate_difference_report(value: Any) -> dict[str, Any]:
    report = exact_keys(value, {"items", "openP0", "openP1", "openP2"}, "differenceReport")
    items = report["items"]
    if not isinstance(items, list) or len(items) > 117:
        raise ValidationFailure("differenceReport.items must contain at most 117 rows")
    counts = {"P0": 0, "P1": 0, "P2": 0}
    seen: set[bytes] = set()
    for index, row in enumerate(items):
        item = exact_keys(
            row,
            {"priority", "cellIds", "incidentKeys", "observation", "boundedChange"},
            f"differenceReport.items[{index}]",
        )
        priority = item["priority"]
        if priority not in counts:
            raise ValidationFailure(f"differenceReport.items[{index}].priority is invalid")
        counts[priority] += 1
        cell_ids = require_unique_strings(
            item["cellIds"],
            f"differenceReport.items[{index}].cellIds",
            minimum=1,
            maximum=39,
        )
        if any(cell_id not in ALL_CELLS for cell_id in cell_ids):
            raise ValidationFailure(f"differenceReport.items[{index}] has an unknown cell")
        require_unique_strings(
            item["incidentKeys"],
            f"differenceReport.items[{index}].incidentKeys",
            maximum=32,
            pattern=INCIDENT_KEY_PATTERN,
        )
        require_string(item["observation"], f"differenceReport.items[{index}].observation")
        require_string(item["boundedChange"], f"differenceReport.items[{index}].boundedChange", 1200)
        canonical = canonical_json_bytes(item)
        if canonical in seen:
            raise ValidationFailure("differenceReport.items contains duplicate rows")
        seen.add(canonical)
    for priority in ("P0", "P1", "P2"):
        field = f"open{priority}"
        if type(report[field]) is not int or report[field] != counts[priority]:
            raise ValidationFailure(
                f"differenceReport.{field} must equal the number of {priority} items"
            )
    return report


def _validate_artifact_bindings(value: Any) -> list[dict[str, str]]:
    if not isinstance(value, list) or len(value) != 4:
        raise ValidationFailure("artifactBindings must contain exactly four rows")
    rows: list[dict[str, str]] = []
    ids: set[str] = set()
    cold_count = 0
    coverage_count = 0
    for index, item in enumerate(value):
        row = exact_keys(
            item,
            {"anonymousArtifactId", "artifactKind", "artifactSha256"},
            f"artifactBindings[{index}]",
        )
        artifact_id = row["anonymousArtifactId"]
        if artifact_id not in {"ARTIFACT-A", "ARTIFACT-B", "ARTIFACT-C", "ARTIFACT-D"}:
            raise ValidationFailure(f"artifactBindings[{index}].anonymousArtifactId is invalid")
        if artifact_id in ids:
            raise ValidationFailure("artifactBindings contains duplicate anonymousArtifactId")
        ids.add(artifact_id)
        if row["artifactKind"] == "COLD_ACTOR":
            cold_count += 1
        elif row["artifactKind"] == "COVERAGE":
            coverage_count += 1
        else:
            raise ValidationFailure(f"artifactBindings[{index}].artifactKind is invalid")
        require_sha(row["artifactSha256"], f"artifactBindings[{index}].artifactSha256")
        rows.append(dict(row))
    if cold_count != 3 or coverage_count != 1:
        raise ValidationFailure("artifactBindings must contain three cold actors and one coverage artifact")
    expected_order_and_kinds = [
        ("ARTIFACT-A", "COLD_ACTOR"),
        ("ARTIFACT-B", "COLD_ACTOR"),
        ("ARTIFACT-C", "COLD_ACTOR"),
        ("ARTIFACT-D", "COVERAGE"),
    ]
    if [
        (row["anonymousArtifactId"], row["artifactKind"])
        for row in rows
    ] != expected_order_and_kinds:
        raise ValidationFailure(
            "artifactBindings order must be the deterministic anonymized A/B/C/D order"
        )
    return rows


def validate_candidate(value: Any) -> dict[str, Any]:
    candidate = exact_keys(value, CANDIDATE_KEYS, "candidate aggregation input")
    if candidate["schemaVersion"] != AGGREGATION_INPUT_SCHEMA:
        raise ValidationFailure(f"candidate schemaVersion must be {AGGREGATION_INPUT_SCHEMA}")
    if candidate["protocol"] != PROTOCOL:
        raise ValidationFailure(f"candidate protocol must be {PROTOCOL}")
    require_string(candidate["scorecardId"], "candidate.scorecardId", 200)
    if not isinstance(candidate["recipeId"], str) or RECIPE_PATTERN.fullmatch(candidate["recipeId"]) is None:
        raise ValidationFailure("candidate.recipeId is invalid")
    blocker = candidate["operationalBlocker"]
    if blocker is not None and blocker not in OPERATIONAL_BLOCKERS:
        raise ValidationFailure("candidate.operationalBlocker is invalid")
    require_sha(candidate["verificationInputSha256"], "candidate.verificationInputSha256")
    require_unique_strings(
        candidate["expectedObservationIds"],
        "candidate.expectedObservationIds",
        minimum=1,
        maximum=768,
        pattern=OBSERVATION_ID_PATTERN,
    )
    not_reached = require_unique_strings(
        candidate["notReachedByProductCellIds"],
        "candidate.notReachedByProductCellIds",
        maximum=len(COLD_CELLS),
    )
    if any(cell_id not in COLD_CELLS for cell_id in not_reached):
        raise ValidationFailure("notReachedByProductCellIds may contain only cold-owned cells")
    _validate_artifact_bindings(candidate["artifactBindings"])
    _validate_difference_report(candidate["differenceReport"])
    provenance = exact_keys(candidate["provenance"], INPUT_PROVENANCE_KEYS, "candidate.provenance")
    array_hash_fields = {
        "coldActorResponseSha256",
        "coldActorResponseRawSha256",
    }
    for field in array_hash_fields:
        hashes = provenance[field]
        if (
            not isinstance(hashes, list)
            or len(hashes) != 3
            or len(set(hashes)) != 3
        ):
            raise ValidationFailure(
                f"candidate.provenance.{field} must contain three distinct SHA-256 values"
            )
        for index, value in enumerate(hashes):
            require_sha(value, f"candidate.provenance.{field}[{index}]")
    for field in INPUT_PROVENANCE_KEYS - {
        "sourceCommit", "cleanTree", "model", "reasoningEffort",
        "packageSha256", "packageStatus", "evaluationSessionMode",
        *array_hash_fields,
    }:
        require_sha(provenance[field], f"candidate.provenance.{field}")
    if not isinstance(provenance["sourceCommit"], str) or SOURCE_COMMIT_PATTERN.fullmatch(provenance["sourceCommit"]) is None:
        raise ValidationFailure("candidate.provenance.sourceCommit must be a full lowercase commit")
    if type(provenance["cleanTree"]) is not bool:
        raise ValidationFailure("candidate.provenance.cleanTree must be boolean")
    if provenance["model"] != "gpt-5.6-sol" or provenance["reasoningEffort"] != "ultra":
        raise ValidationFailure("candidate provenance must bind gpt-5.6-sol ultra")
    if provenance["evaluationSessionMode"] not in {"INITIAL", "REPLACEMENT"}:
        raise ValidationFailure(
            "candidate.provenance.evaluationSessionMode must be INITIAL or REPLACEMENT"
        )
    if provenance["packageSha256"] is not None:
        require_sha(provenance["packageSha256"], "candidate.provenance.packageSha256")
    if provenance["packageStatus"] not in {
        "EDITOR_NATIVE_NOT_PUBLIC_PACKAGE",
        "INTERNAL_APP_BUNDLE",
    }:
        raise ValidationFailure("candidate.provenance.packageStatus is invalid")
    if provenance["packageStatus"] == "EDITOR_NATIVE_NOT_PUBLIC_PACKAGE" and provenance["packageSha256"] is not None:
        raise ValidationFailure("editor-native provenance must have packageSha256=null")
    validate_native_aggregation_input_schema(candidate)
    return candidate


def load_rubric(value: Any, raw_sha256: str) -> dict[str, Any]:
    rubric = exact_keys(
        value,
        {"schemaVersion", "protocol", "judge", "labels", "lanes", "native", "textPlan"},
        "rubric",
    )
    if rubric["schemaVersion"] != "gridworks.commercial-ux.rubric.v1":
        raise ValidationFailure("rubric schemaVersion drifted")
    if rubric["protocol"] != "GRIDWORKS-COMMERCIAL-UX-v1":
        raise ValidationFailure("rubric protocol drifted")
    if rubric["judge"] != {"model": "gpt-5.6-sol", "reasoningEffort": "ultra", "slot": "SOL-ULTRA"}:
        raise ValidationFailure("rubric judge identity drifted")
    label_rows = rubric["labels"]
    if not isinstance(label_rows, list) or [row.get("id") for row in label_rows if isinstance(row, dict)] != list(LABELS):
        raise ValidationFailure("rubric labels or order drifted")
    for row in label_rows:
        if (row.get("ordinal"), row.get("score")) != LABELS[row["id"]]:
            raise ValidationFailure(f"rubric label {row['id']} numeric anchor drifted")
    lanes = rubric["lanes"]
    if not isinstance(lanes, list):
        raise ValidationFailure("rubric lanes must be an array")
    text_lanes = [row for row in lanes if isinstance(row, dict) and row.get("id") == "TEXT-PLAN"]
    if len(text_lanes) != 1 or text_lanes[0].get("officialCommercialUX") is not False:
        raise ValidationFailure("rubric must keep TextPlanProxy non-official")
    native = rubric["native"]
    if not isinstance(native, dict):
        raise ValidationFailure("rubric.native must be an object")
    expected_scalars = {
        "metric": "CommercialUXProxy",
        "categoryWeightTotal": 100,
        "overallTarget": 87.0,
        "requiredCellMinimum": 70,
    }
    for field, expected in expected_scalars.items():
        if native.get(field) != expected:
            raise ValidationFailure(f"rubric.native.{field} drifted")
    aggregation = native.get("aggregation")
    expected_aggregation = {
        "judgeCount": 3,
        "coldActorCount": 3,
        "labelReduction": "NUMERIC_MEDIAN",
        "bothLaneReduction": "MIN",
        "spreadPenaltyMultiplier": 0.2,
        "spreadPenaltyMaximum": 8.0,
        "instabilityOrdinalRangeMinimum": 2,
    }
    if aggregation != expected_aggregation:
        raise ValidationFailure("rubric native aggregation contract drifted")
    categories = native.get("categories")
    if not isinstance(categories, list) or len(categories) != 11:
        raise ValidationFailure("rubric must contain exactly eleven native categories")
    category_ids: list[str] = []
    cell_ids: list[str] = []
    cold_cells: list[str] = []
    coverage_cells: list[str] = []
    category_weight = 0
    for category_index, category in enumerate(categories):
        if not isinstance(category, dict):
            raise ValidationFailure(f"rubric native category {category_index} must be an object")
        category_id = require_string(category.get("id"), f"rubric category {category_index}.id", 100)
        category_ids.append(category_id)
        weight = category.get("weight")
        minimum_score = category.get("minimumScore")
        if type(weight) is not int or weight <= 0:
            raise ValidationFailure(f"rubric category {category_id} weight is invalid")
        if minimum_score not in {70, 85}:
            raise ValidationFailure(f"rubric category {category_id} minimum is invalid")
        category_weight += weight
        cells = category.get("cells")
        if not isinstance(cells, list) or not cells:
            raise ValidationFailure(f"rubric category {category_id} cells are invalid")
        cell_weight = 0
        for cell in cells:
            if not isinstance(cell, dict):
                raise ValidationFailure(f"rubric category {category_id} contains a non-object cell")
            cell_id = require_string(cell.get("id"), f"rubric category {category_id} cell id", 10)
            weight_within = cell.get("weight")
            if type(weight_within) is not int or weight_within <= 0:
                raise ValidationFailure(f"rubric cell {cell_id} weight is invalid")
            ownership = cell.get("laneOwnership")
            if not isinstance(ownership, list) or not ownership:
                raise ValidationFailure(f"rubric cell {cell_id} laneOwnership is invalid")
            if any(lane not in {"COLD-JOURNEY", "COVERAGE-JOURNEY"} for lane in ownership):
                raise ValidationFailure(f"rubric cell {cell_id} has an invalid native lane")
            cell_ids.append(cell_id)
            cell_weight += weight_within
            if "COLD-JOURNEY" in ownership:
                cold_cells.append(cell_id)
            if "COVERAGE-JOURNEY" in ownership:
                coverage_cells.append(cell_id)
        if cell_weight != 100:
            raise ValidationFailure(f"rubric category {category_id} cell weights must total 100")
    if category_weight != 100:
        raise ValidationFailure("rubric native category weights must total 100")
    if tuple(cell_ids) != ALL_CELLS or tuple(cold_cells) != COLD_CELLS or tuple(coverage_cells) != COVERAGE_CELLS:
        raise ValidationFailure("rubric native cell order or lane ownership drifted")
    if len(set(category_ids)) != len(category_ids) or len(set(cell_ids)) != len(cell_ids):
        raise ValidationFailure("rubric contains duplicate category or cell IDs")
    return {
        "value": rubric,
        "sha256": raw_sha256,
        "categories": categories,
        "aggregation": aggregation,
        "overallTarget": native["overallTarget"],
        "requiredCellMinimum": native["requiredCellMinimum"],
    }


def _repo_relative_path(value: Any, label: str) -> Path:
    if not isinstance(value, str) or not value or Path(value).is_absolute():
        raise ProvenanceFailure(f"{label} must be a non-empty repository-relative path")
    resolved = (REPOSITORY_ROOT / value).resolve(strict=False)
    try:
        resolved.relative_to(REPOSITORY_ROOT)
    except ValueError as exception:
        raise ProvenanceFailure(f"{label} escapes the repository root") from exception
    return resolved


def validate_official_score_bearing_preflight(candidate: dict[str, Any]) -> None:
    """Reject an official capture while the checked-in freeze remains unready.

    Readiness is owned by the candidate-independent contract and gold authority,
    never by the aggregation input or oracle output.  FORMATIVE-01 intentionally
    remains usable for non-official arithmetic while those authorities are blocked.
    """

    if candidate["recipeId"] == "FORMATIVE-01":
        return
    contract, _ = read_json_bytes(CONTRACT_BINDINGS_PATH, "checked-in contract bindings")
    gold, _ = read_json_bytes(GOLD_STATE_MANIFEST_PATH, "checked-in gold-state manifest")
    if not isinstance(contract, dict) or not isinstance(gold, dict):
        raise ValidationFailure("BLOCKED_PRE_CAPTURE: checked-in readiness authority is invalid")
    policy = contract.get("toolBindingPolicy")
    contract_ready = (
        isinstance(policy, dict)
        and policy.get("scoreBearingCaptureAllowed") is True
        and policy.get("currentlyUnboundProducerStages") == []
    )
    gold_template_valid = (
        gold.get("candidateIndependent") is True
        and gold.get("bindingComplete") is False
        and gold.get("scoreBearingCaptureAllowed") is False
    )
    if not contract_ready or not gold_template_valid:
        raise ValidationFailure(
            "BLOCKED_PRE_CAPTURE: checked-in contract/tool stages do not authorize "
            "score-bearing capture or the immutable gold template drifted"
        )
    try:
        contract_check = subprocess.run(
            [sys.executable, str(CONTRACT_VALIDATOR_PATH), "--json"],
            cwd=REPOSITORY_ROOT,
            check=False,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=60,
        )
        contract_result = json.loads(contract_check.stdout)
    except (OSError, subprocess.SubprocessError, json.JSONDecodeError) as exception:
        raise ValidationFailure(
            "BLOCKED_PRE_CAPTURE: checked-in semantic contract validation did not run"
        ) from exception
    if contract_check.returncode != 0 or contract_result.get("status") != "PASS":
        raise ValidationFailure(
            "BLOCKED_PRE_CAPTURE: checked-in semantic contract validation did not PASS"
        )


def validate_candidate_execution_authority(candidate_manifest: dict[str, Any]) -> None:
    """Derive the execution identity from canonical component bytes.

    The holdout reuse key is only meaningful when ``executionArtifactSha256``
    cannot be chosen by the candidate.  Resolve every component before the
    holdout receipt is trusted, reject symlink/non-canonical aliases, and then
    recompute the frozen component projection.
    """

    execution = candidate_manifest["execution"]
    component_fields = [
        ("godotExecutablePath", "godotExecutableSha256"),
        ("managedAssemblyPath", "managedAssemblySha256"),
        ("pckResourceManifestPath", "pckResourceManifestSha256"),
    ]
    if execution["packagePath"] is not None:
        component_fields.append(("packagePath", "packageSha256"))
    for path_field, sha_field in component_fields:
        raw_path = execution[path_field]
        if not isinstance(raw_path, str):
            raise ProvenanceFailure(
                f"candidate execution.{path_field} must be a canonical absolute path"
            )
        path = Path(raw_path)
        try:
            resolved = path.resolve(strict=True)
        except OSError as exception:
            raise ProvenanceFailure(
                f"candidate execution.{path_field} cannot be opened: {exception}"
            ) from exception
        if (
            not path.is_absolute()
            or raw_path != str(resolved)
            or not resolved.is_file()
        ):
            raise ProvenanceFailure(
                f"candidate execution.{path_field} must be a canonical regular file "
                "without symlinks"
            )
        observed = file_sha256(resolved, f"candidate execution {path_field}")
        if execution[sha_field] != observed:
            raise ProvenanceFailure(
                f"candidate execution.{sha_field} raw SHA mismatch"
            )
    projection = {
        "godotExecutableSha256": execution["godotExecutableSha256"],
        "managedAssemblySha256": execution["managedAssemblySha256"],
        "pckResourceManifestSha256": execution["pckResourceManifestSha256"],
        "packageSha256": execution["packageSha256"],
        "packageStatus": execution["packageStatus"],
    }
    expected = canonical_sha256(projection)
    if execution["executionArtifactSha256"] != expected:
        raise ProvenanceFailure(
            "candidate executionArtifactSha256 canonical component projection mismatch"
        )


def _load_exact_validator(path: Path, module_name: str) -> Any:
    spec = importlib.util.spec_from_file_location(module_name, path)
    if spec is None or spec.loader is None:
        raise ProvenanceFailure(f"exact-byte validator could not be loaded: {path}")
    validator = importlib.util.module_from_spec(spec)
    try:
        spec.loader.exec_module(validator)
    except (ImportError, OSError, SyntaxError) as exception:
        raise ProvenanceFailure(
            f"exact-byte validator could not be loaded: {path}: {exception}"
        ) from exception
    return validator


def validate_runtime_contract_authority(
    candidate_manifest_path: Path,
    qualification_receipt_path: Path,
    gold_binding_path: Path,
    holdout_consumption_receipt_path: Path,
    holdout_registry_before_path: Path,
    holdout_registry_after_path: Path,
    evaluation_session_claim_path: Path,
    *,
    candidate_manifest_raw_bytes: bytes,
    qualification_receipt_raw_bytes: bytes,
    gold_binding_raw_bytes: bytes,
    holdout_consumption_receipt_raw_bytes: bytes,
    holdout_registry_before_raw_bytes: bytes,
    holdout_registry_after_raw_bytes: bytes,
    evaluation_session_claim_raw_bytes: bytes,
    initial_evaluation_session_claim_path: Path | None,
    initial_evaluation_session_claim_raw_bytes: bytes | None,
) -> None:
    """Run the shared contract validator over the exact already-read bytes."""

    candidate_manifest_path = candidate_manifest_path.resolve(strict=False)
    qualification_receipt_path = qualification_receipt_path.resolve(strict=False)
    gold_binding_path = gold_binding_path.resolve(strict=False)
    holdout_consumption_receipt_path = holdout_consumption_receipt_path.resolve(
        strict=False
    )
    holdout_registry_before_path = holdout_registry_before_path.resolve(strict=False)
    holdout_registry_after_path = holdout_registry_after_path.resolve(strict=False)
    evaluation_session_claim_path = evaluation_session_claim_path.resolve(strict=False)

    validator = _load_exact_validator(
        CONTRACT_VALIDATOR_PATH,
        "gridworks_commercial_ux_exact_contract_validator",
    )
    try:
        errors, result = validator.validate_runtime_contract_bytes(
            NATIVE_DIRECTORY,
            DEFAULT_RUBRIC_PATH,
            candidate_manifest_bytes=candidate_manifest_raw_bytes,
            qualification_receipt_bytes=qualification_receipt_raw_bytes,
            gold_binding_manifest_bytes=gold_binding_raw_bytes,
            holdout_consumption_receipt_bytes=(
                holdout_consumption_receipt_raw_bytes
            ),
            registry_before_bytes=holdout_registry_before_raw_bytes,
            registry_after_bytes=holdout_registry_after_raw_bytes,
            evaluation_session_claim_bytes=evaluation_session_claim_raw_bytes,
            initial_evaluation_session_claim_bytes=(
                initial_evaluation_session_claim_raw_bytes
            ),
            candidate_manifest_path_label=candidate_manifest_path,
            qualification_receipt_path_label=qualification_receipt_path,
            gold_binding_manifest_path_label=gold_binding_path,
            holdout_consumption_receipt_path_label=(
                holdout_consumption_receipt_path
            ),
            registry_before_path_label=holdout_registry_before_path,
            registry_after_path_label=holdout_registry_after_path,
            evaluation_session_claim_path_label=evaluation_session_claim_path,
            initial_evaluation_session_claim_path_label=(
                initial_evaluation_session_claim_path
            ),
        )
    except (AttributeError, ImportError, OSError, SyntaxError) as exception:
        raise ProvenanceFailure(
            "runtime native contract semantic validation did not run"
        ) from exception
    expected_observed = {
        "candidateManifestRawSha256": bytes_sha256(candidate_manifest_raw_bytes),
        "qualificationReceiptRawSha256": bytes_sha256(
            qualification_receipt_raw_bytes
        ),
        "goldBindingManifestRawSha256": bytes_sha256(gold_binding_raw_bytes),
        "holdoutConsumptionReceiptRawSha256": bytes_sha256(
            holdout_consumption_receipt_raw_bytes
        ),
        "registryBeforeRawSha256": bytes_sha256(
            holdout_registry_before_raw_bytes
        ),
        "registryAfterRawSha256": bytes_sha256(holdout_registry_after_raw_bytes),
        "evaluationSessionClaimRawSha256": bytes_sha256(
            evaluation_session_claim_raw_bytes
        ),
    }
    if initial_evaluation_session_claim_raw_bytes is not None:
        expected_observed["initialEvaluationSessionClaimRawSha256"] = bytes_sha256(
            initial_evaluation_session_claim_raw_bytes
        )
    if result.get("observedRawSha256") != expected_observed:
        raise ProvenanceFailure(
            "runtime native contract observed raw SHA projection mismatch"
        )
    if errors or result.get("status") != "PASS":
        raise ProvenanceFailure(
            "runtime native contract semantic validation failed"
            + (f": {errors}" if errors else "")
        )


def _read_session_artifact_files(
    session_tool: Any,
    artifact_root: Path,
) -> tuple[list[tuple[str, bytes]], dict[Path, str]]:
    """Read one stable attempt artifact tree twice and reject aliases/symlinks."""

    try:
        resolved = artifact_root.resolve(strict=True)
    except OSError as exception:
        raise ProvenanceFailure(
            f"evaluation attempt artifact root is missing: {artifact_root}: {exception}"
        ) from exception
    if artifact_root != resolved or not resolved.is_dir() or artifact_root.is_symlink():
        raise ProvenanceFailure(
            f"evaluation attempt artifact root is not a canonical directory: {artifact_root}"
        )
    try:
        session_tool.reject_symlink_components(resolved, "evaluation attempt artifact root")
    except Exception as exception:
        raise ProvenanceFailure(str(exception)) from exception
    def read_pass() -> tuple[list[tuple[str, bytes]], dict[Path, str]]:
        files: list[tuple[str, bytes]] = []
        hashes_by_path: dict[Path, str] = {}
        for directory, directory_names, filenames in os.walk(
            resolved,
            followlinks=False,
        ):
            directory_names.sort()
            filenames.sort()
            directory_path = Path(directory)
            for name in [*directory_names, *filenames]:
                child = directory_path / name
                if child.is_symlink():
                    raise ProvenanceFailure(
                        "symlink is forbidden in evaluation attempt artifacts: "
                        f"{child}"
                    )
            for filename in filenames:
                child = directory_path / filename
                relative = child.relative_to(resolved).as_posix()
                try:
                    raw = session_tool.read_regular_exact(
                        child,
                        f"evaluation attempt artifact {relative}",
                    )
                except Exception as exception:
                    raise ProvenanceFailure(str(exception)) from exception
                files.append((relative, raw))
                hashes_by_path[child] = bytes_sha256(raw)
        files.sort(key=lambda row: row[0])
        return files, hashes_by_path

    first_files, first_hashes = read_pass()
    second_files, second_hashes = read_pass()
    if first_files != second_files or first_hashes != second_hashes:
        raise ProvenanceFailure(
            "evaluation attempt artifact root changed across the two-pass read"
        )
    return second_files, second_hashes


def _discover_evaluation_session_attempts(
    *,
    session_tool: Any,
    validator: Any,
    claim: dict[str, Any],
    claim_raw: bytes,
) -> dict[str, Any]:
    """Discover and validate exactly the attempts authorized by one claim."""

    session_root = Path(claim["canonicalSessionRoot"])
    try:
        resolved_session_root = session_root.resolve(strict=True)
    except OSError as exception:
        raise ProvenanceFailure(
            f"evaluation session root is unreadable: {exception}"
        ) from exception
    if (
        session_root != resolved_session_root
        or session_root.is_symlink()
        or not resolved_session_root.is_dir()
    ):
        raise ProvenanceFailure("evaluation session root must be a canonical directory")
    try:
        session_tool.reject_symlink_components(session_root, "evaluation session root")
    except Exception as exception:
        raise ProvenanceFailure(str(exception)) from exception

    expected_root_children = {"session.lock", "slots", "artifacts"}
    try:
        root_children = {child.name for child in session_root.iterdir()}
    except OSError as exception:
        raise ProvenanceFailure(
            f"evaluation session root is unreadable: {exception}"
        ) from exception
    if root_children != expected_root_children:
        raise ProvenanceFailure(
            "evaluation session root must contain exactly the declared lock, slots, "
            f"and fixed artifacts roots: {sorted(root_children)}"
        )
    lock_path = session_root / "session.lock"
    slots_root = session_root / "slots"
    fixed_artifact_root = session_root / "artifacts"
    try:
        session_tool.read_regular_exact(lock_path, "evaluation session lock")
        resolved_slots_root = slots_root.resolve(strict=True)
        resolved_fixed_root = fixed_artifact_root.resolve(strict=True)
    except Exception as exception:
        raise ProvenanceFailure(str(exception)) from exception
    if (
        slots_root != resolved_slots_root
        or slots_root.is_symlink()
        or not resolved_slots_root.is_dir()
        or fixed_artifact_root != resolved_fixed_root
        or fixed_artifact_root.is_symlink()
        or not resolved_fixed_root.is_dir()
    ):
        raise ProvenanceFailure(
            "evaluation session slots and artifacts roots must be canonical directories"
        )

    required_slots = claim["requiredFreshSlotIds"]
    expected_slot_names = {
        Path(slot["slotRoot"]).name
        for slot in claim["slots"]
        if slot["slotId"] in required_slots
    }
    try:
        actual_slot_children = {child.name for child in slots_root.iterdir()}
    except OSError as exception:
        raise ProvenanceFailure(
            f"evaluation session slots root is unreadable: {exception}"
        ) from exception
    if actual_slot_children != expected_slot_names:
        raise ProvenanceFailure(
            "evaluation session slots root must contain exactly the required fresh "
            f"slot directories: {sorted(actual_slot_children)}"
        )

    attempt_envelopes: list[dict[str, Any]] = []
    audit_rows: list[dict[str, Any]] = []
    rows_by_slot: dict[str, list[dict[str, Any]]] = {}
    for slot in claim["slots"]:
        slot_root = Path(slot["slotRoot"])
        if slot["slotId"] not in required_slots:
            if os.path.lexists(str(slot_root)):
                raise ProvenanceFailure(
                    f"evaluation session reused stable {slot['slotId']} must be absent"
                )
            continue
        try:
            resolved_slot_root = slot_root.resolve(strict=True)
        except OSError as exception:
            raise ProvenanceFailure(
                f"evaluation session {slot['slotId']} root is unreadable: {exception}"
            ) from exception
        if (
            slot_root != resolved_slot_root
            or slot_root.is_symlink()
            or not resolved_slot_root.is_dir()
        ):
            raise ProvenanceFailure(
                f"evaluation session {slot['slotId']} root is not canonical"
            )
        declared_attempt_names = {
            Path(attempt["attemptRoot"]).name for attempt in slot["attempts"]
        }
        unexpected_attempts = {
            child.name for child in slot_root.iterdir()
        } - declared_attempt_names
        if unexpected_attempts:
            raise ProvenanceFailure(
                f"evaluation session {slot['slotId']} contains undeclared attempts: "
                f"{sorted(unexpected_attempts)}"
            )
        for attempt in slot["attempts"]:
            attempt_root = Path(attempt["attemptRoot"])
            start_path = Path(attempt["startReceiptPath"])
            output_path = Path(attempt["outputPath"])
            artifact_root = Path(attempt["artifactRoot"])
            terminal_path = Path(attempt["terminalReceiptPath"])
            declared_paths = (
                attempt_root,
                start_path,
                output_path,
                artifact_root,
                terminal_path,
            )
            present = [os.path.lexists(str(path)) for path in declared_paths]
            if not any(present):
                continue
            if not all(present):
                raise ProvenanceFailure(
                    f"evaluation session {slot['slotId']}/{attempt['attemptOrdinal']} "
                    "is present but not fully terminalized"
                )
            expected_attempt_children = {
                start_path.name,
                output_path.name,
                artifact_root.name,
                terminal_path.name,
            }
            try:
                actual_attempt_children = {
                    child.name for child in attempt_root.iterdir()
                }
            except OSError as exception:
                raise ProvenanceFailure(
                    f"evaluation attempt root is unreadable: {exception}"
                ) from exception
            if actual_attempt_children != expected_attempt_children:
                raise ProvenanceFailure(
                    f"evaluation session {slot['slotId']}/{attempt['attemptOrdinal']} "
                    "contains undeclared or missing attempt files"
                )
            try:
                _, start_raw, start = session_tool.read_exact(
                    start_path,
                    "evaluation attempt start receipt",
                )
                _, terminal_raw, terminal = session_tool.read_exact(
                    terminal_path,
                    "evaluation attempt terminal receipt",
                )
                output_raw = session_tool.read_regular_exact(
                    output_path,
                    "evaluation attempt output",
                )
            except Exception as exception:
                raise ProvenanceFailure(str(exception)) from exception
            artifact_files, artifact_hashes = _read_session_artifact_files(
                session_tool,
                artifact_root,
            )
            attempt_envelopes.append({
                "startReceiptBytes": start_raw,
                "terminalReceiptBytes": terminal_raw,
                "outputBytes": output_raw,
                "artifactFiles": artifact_files,
                "startReceiptPathLabel": start_path,
                "terminalReceiptPathLabel": terminal_path,
                "outputPathLabel": output_path,
                "artifactRootPathLabel": artifact_root,
            })
            audit = {
                "slotId": slot["slotId"],
                "role": slot["role"],
                "roleOrdinal": slot["roleOrdinal"],
                "attemptOrdinal": attempt["attemptOrdinal"],
                "startReceiptSha256": start.get("evaluationAttemptReceiptSha256"),
                "startReceiptRawSha256": bytes_sha256(start_raw),
                "terminalReceiptSha256": terminal.get(
                    "evaluationAttemptTerminalSha256"
                ),
                "terminalReceiptRawSha256": bytes_sha256(terminal_raw),
                "outputRawSha256": bytes_sha256(output_raw),
                "artifactContentRootSha256": terminal.get(
                    "artifactContentRootSha256"
                ),
                "outcome": terminal.get("outcome"),
            }
            row = {
                "audit": audit,
                "outputPath": output_path,
                "outputRawBytes": output_raw,
                "artifactRoot": artifact_root,
                "artifactHashesByPath": artifact_hashes,
            }
            audit_rows.append(audit)
            rows_by_slot.setdefault(slot["slotId"], []).append(row)

    try:
        chain_errors, selected = validator.validate_attempt_chain_bytes(
            NATIVE_DIRECTORY,
            session_claim=claim,
            session_claim_raw_bytes=claim_raw,
            attempts=attempt_envelopes,
            require_all_success_slots=False,
            required_success_slot_ids=required_slots,
        )
    except Exception as exception:
        raise ProvenanceFailure(
            f"evaluation attempt chain validation did not run: {exception}"
        ) from exception
    if chain_errors:
        raise ProvenanceFailure(
            "evaluation attempt chain failed: " + "; ".join(chain_errors)
        )

    selected_rows: list[dict[str, Any]] = []
    selected_by_slot: dict[str, dict[str, Any]] = {}
    for selected_row in selected:
        matches = [
            row
            for row in rows_by_slot.get(selected_row["slotId"], [])
            if row["audit"]["attemptOrdinal"] == selected_row["attemptOrdinal"]
            and row["audit"]["outcome"] == "SUCCESS"
        ]
        if len(matches) != 1:
            raise ProvenanceFailure(
                f"evaluation selected attempt {selected_row['slotId']} is ambiguous"
            )
        row = matches[0]
        selected_rows.append(copy.deepcopy(row["audit"]))
        selected_by_slot[selected_row["slotId"]] = row
    return {
        "attemptAuditRows": audit_rows,
        "selectedRows": selected_rows,
        "selectedBySlot": selected_by_slot,
    }


def validate_evaluation_session_authority(
    session_claim_path: Path,
    candidate_manifest: dict[str, Any],
    candidate_manifest_raw_bytes: bytes,
    holdout_receipt: dict[str, Any],
    holdout_receipt_raw_bytes: bytes,
    replacement_context: dict[str, Any] | None,
) -> dict[str, Any]:
    """Validate the pre-capture claim and every fixed producer attempt exactly.

    The filesystem is discovered from the claim, never from a caller-supplied
    subset. Every authorized fresh attempt must therefore be terminalized. A
    REPLACEMENT revalidates and reuses unchanged slots from the exact sealed
    INITIAL chain, then composes one effective nine-slot selection.
    """

    session_tool = _load_exact_validator(
        SESSION_CLAIM_TOOL_PATH,
        "gridworks_commercial_ux_evaluation_session_tool",
    )
    try:
        validator, resolved_claim_path, claim_raw, claim = (
            session_tool.read_and_validate_claim(
                session_claim_path,
                native=NATIVE_DIRECTORY,
            )
        )
    except Exception as exception:
        raise ProvenanceFailure(
            f"evaluation session claim validation failed: {exception}"
        ) from exception

    initial_value: dict[str, Any] | None = None
    initial_raw: bytes | None = None
    initial_path: Path | None = None
    if claim.get("sessionMode") == "REPLACEMENT":
        initial_link = claim.get("initialSession")
        if not isinstance(initial_link, dict) or not isinstance(
            initial_link.get("claimPath"), str
        ):
            raise ProvenanceFailure(
                "replacement evaluation session lacks its exact initial claim link"
            )
        try:
            initial_path, initial_raw, initial_value = session_tool.read_exact(
                Path(initial_link["claimPath"]),
                "initial evaluation session claim",
            )
        except Exception as exception:
            raise ProvenanceFailure(str(exception)) from exception
    semantic_errors: list[str] = []
    validator.validate_evaluation_session_claim_semantics(
        claim,
        resolved_claim_path,
        NATIVE_DIRECTORY,
        candidate_manifest,
        holdout_receipt,
        semantic_errors,
        initial_session_claim=initial_value,
        initial_session_claim_raw_bytes=initial_raw,
        initial_session_claim_path_label=initial_path,
    )
    if semantic_errors:
        raise ProvenanceFailure(
            "evaluation session claim candidate/holdout binding failed: "
            + "; ".join(semantic_errors)
        )
    if (
        claim.get("candidateManifestRawSha256")
        != bytes_sha256(candidate_manifest_raw_bytes)
        or claim.get("holdoutConsumptionReceiptRawSha256")
        != bytes_sha256(holdout_receipt_raw_bytes)
    ):
        raise ProvenanceFailure(
            "evaluation session claim does not bind the exact candidate/holdout bytes"
        )
    expected_mode = "REPLACEMENT" if replacement_context is not None else "INITIAL"
    if claim.get("sessionMode") != expected_mode:
        raise ProvenanceFailure(
            f"evaluation session mode must be {expected_mode} for this aggregate"
        )
    if replacement_context is not None:
        initial_scorecard = replacement_context["initial"]
        initial_provenance = initial_scorecard["provenance"]
        initial_seal = replacement_context["initialSeal"]
        initial_link = claim["initialSession"]
        expected_initial_link = {
            "claimPath": str(initial_path),
            "claimSha256": initial_provenance[
                "evaluationSessionClaimSha256"
            ],
            "claimRawSha256": initial_provenance[
                "evaluationSessionClaimRawSha256"
            ],
            "sessionId": initial_provenance["evaluationSessionId"],
            "scorecardPath": initial_seal["value"]["scorecardPath"],
            "scorecardRawSha256": bytes_sha256(
                replacement_context["initialBytes"]
            ),
            "scorecardStatus": initial_scorecard["status"],
            "scorecardId": initial_scorecard["scorecardId"],
            "judgePanelSha256": initial_scorecard["judgePanelSha256"],
            "replacementRequiredLanes": initial_scorecard[
                "replacementRequiredLanes"
            ],
            "panelFinalizationSealPath": initial_seal["value"][
                "canonicalSealPath"
            ],
            "panelFinalizationSealSha256": initial_seal["selfSha256"],
            "panelFinalizationSealRawSha256": initial_seal["rawSha256"],
            "evaluationSessionPolicySha256": initial_provenance[
                "evaluationSessionPolicySha256"
            ],
            "evaluationSessionClaimToolSha256": initial_provenance[
                "evaluationSessionClaimToolSha256"
            ],
            "evaluationSessionMode": "INITIAL",
            "evaluationAttemptAuditSha256": initial_provenance[
                "evaluationAttemptAuditSha256"
            ],
            "evaluationSelectedAttemptsSha256": initial_provenance[
                "evaluationSelectedAttemptsSha256"
            ],
        }
        if initial_link != expected_initial_link:
            raise ProvenanceFailure(
                "replacement session does not link the exact initial scorecard and seal"
            )

    receipt_root = resolved_claim_path.parent
    expected_receipt_children = {"initial-claim.json", "initial"}
    if claim["sessionMode"] == "REPLACEMENT":
        expected_receipt_children.update({
            "replacement-01-claim.json",
            "replacement-01",
        })
    try:
        resolved_receipt_root = receipt_root.resolve(strict=True)
        actual_receipt_children = {child.name for child in receipt_root.iterdir()}
    except OSError as exception:
        raise ProvenanceFailure(
            f"evaluation receipt session hierarchy is unreadable: {exception}"
        ) from exception
    if (
        receipt_root != resolved_receipt_root
        or receipt_root.is_symlink()
        or actual_receipt_children != expected_receipt_children
    ):
        raise ProvenanceFailure(
            "evaluation receipt session hierarchy must contain exactly the claimed "
            f"session roots and claims: {sorted(actual_receipt_children)}"
        )

    discovered = _discover_evaluation_session_attempts(
        session_tool=session_tool,
        validator=validator,
        claim=claim,
        claim_raw=claim_raw,
    )
    audit_rows = discovered["attemptAuditRows"]
    selected_rows = discovered["selectedRows"]
    selected_by_slot = discovered["selectedBySlot"]

    if claim["sessionMode"] == "REPLACEMENT":
        assert initial_value is not None and initial_raw is not None
        initial_discovered = _discover_evaluation_session_attempts(
            session_tool=session_tool,
            validator=validator,
            claim=initial_value,
            claim_raw=initial_raw,
        )
        initial_audit_sha = canonical_sha256(
            initial_discovered["attemptAuditRows"]
        )
        initial_selected_sha = canonical_sha256(
            initial_discovered["selectedRows"]
        )
        initial_link = claim["initialSession"]
        if (
            initial_audit_sha != initial_link["evaluationAttemptAuditSha256"]
            or initial_selected_sha
            != initial_link["evaluationSelectedAttemptsSha256"]
        ):
            raise ProvenanceFailure(
                "replacement session INITIAL attempt chain differs from its finalized seal"
            )

        fresh_slots = set(claim["requiredFreshSlotIds"])
        effective_rows: list[dict[str, Any]] = []
        effective_by_slot: dict[str, dict[str, Any]] = {}
        for slot in claim["slots"]:
            slot_id = slot["slotId"]
            if slot_id in fresh_slots:
                source = discovered["selectedBySlot"]
                source_claim = claim
            else:
                source = initial_discovered["selectedBySlot"]
                source_claim = initial_value
            row = source[slot_id]
            projection = copy.deepcopy(row["audit"])
            projection.update({
                "sourceSessionId": source_claim["sessionId"],
                "sourceSessionMode": source_claim["sessionMode"],
            })
            effective_rows.append(projection)
            effective_by_slot[slot_id] = row
        selected_rows = effective_rows
        selected_by_slot = effective_by_slot
        audit_sha = canonical_sha256({
            "initialEvaluationAttemptAuditSha256": initial_audit_sha,
            "replacementAttemptAuditRows": audit_rows,
            "replacementSessionId": claim["sessionId"],
            "requiredFreshSlotIds": claim["requiredFreshSlotIds"],
        })
        selected_sha = canonical_sha256({
            "effectiveSelectedAttempts": selected_rows,
            "initialEvaluationSelectedAttemptsSha256": initial_selected_sha,
            "replacementSessionId": claim["sessionId"],
            "requiredFreshSlotIds": claim["requiredFreshSlotIds"],
        })
    else:
        audit_sha = canonical_sha256(audit_rows)
        selected_sha = canonical_sha256(selected_rows)

    provenance = {
        "evaluationSessionClaimSha256": claim["evaluationSessionClaimSha256"],
        "evaluationSessionClaimRawSha256": bytes_sha256(claim_raw),
        "evaluationSessionPolicySha256": claim["evaluationSessionPolicySha256"],
        "evaluationSessionClaimToolSha256": claim["sessionClaimToolSha256"],
        "evaluationSessionId": claim["sessionId"],
        "evaluationSessionMode": claim["sessionMode"],
        "evaluationAttemptAuditSha256": audit_sha,
        "evaluationSelectedAttemptsSha256": selected_sha,
    }
    return {
        "claim": claim,
        "claimPath": resolved_claim_path,
        "claimRawBytes": claim_raw,
        "initialClaimPath": initial_path,
        "initialClaimRawBytes": initial_raw,
        "attemptAuditRows": audit_rows,
        "selectedRows": selected_rows,
        "selectedBySlot": selected_by_slot,
        "provenance": provenance,
    }


def validate_evaluation_session_candidate_provenance(
    session: dict[str, Any],
    candidate: dict[str, Any],
) -> None:
    expected = session["provenance"]
    observed = {
        field: candidate["provenance"][field]
        for field in expected
    }
    if observed != expected:
        raise ProvenanceFailure(
            "candidate aggregation input does not bind the exact evaluation session chain"
        )


def validate_evaluation_session_fixed_artifacts(
    session: dict[str, Any],
    paths: dict[str, Path],
) -> None:
    fixed = session["claim"]["fixedArtifactPaths"]
    if set(paths) != set(fixed):
        raise ProvenanceFailure(
            "aggregate fixed-artifact mapping differs from the evaluation session policy"
        )
    for key, path in paths.items():
        expected = Path(fixed[key])
        if path != path.resolve(strict=False) or path != expected:
            raise ProvenanceFailure(
                f"aggregate {key} path is not its claimed fixed artifact path"
            )
    artifact_root = Path(next(iter(fixed.values()))).parent
    declared_names = {Path(path).name for path in fixed.values()}
    try:
        actual_names = {child.name for child in artifact_root.iterdir()}
    except OSError as exception:
        raise ProvenanceFailure(
            f"evaluation session fixed-artifact root is unreadable: {exception}"
        ) from exception
    expected_before_finalization = declared_names - {
        Path(fixed["scorecard"]).name,
        Path(fixed["panelFinalizationSeal"]).name,
    }
    if actual_names != expected_before_finalization:
        raise ProvenanceFailure(
            "evaluation session fixed-artifact root must contain exactly the "
            "declared pre-finalization artifacts"
        )


def validate_evaluation_session_primary_outputs(
    session: dict[str, Any],
    outputs: dict[str, tuple[Path, bytes | None]],
) -> None:
    expected_slots = {f"SLOT-{index:02d}" for index in range(1, 10)}
    if set(outputs) != expected_slots or set(session["selectedBySlot"]) != expected_slots:
        raise ProvenanceFailure(
            "evaluation session primary output mapping must cover exactly nine slots"
        )
    for slot_id in sorted(expected_slots):
        path, raw = outputs[slot_id]
        selected = session["selectedBySlot"][slot_id]
        if (
            raw is None
            or path != selected["outputPath"]
            or bytes_sha256(raw) != selected["audit"]["outputRawSha256"]
        ):
            raise ProvenanceFailure(
                f"aggregate primary output does not equal selected {slot_id} exact bytes"
            )


def _require_session_supporting_artifact(
    session: dict[str, Any],
    slot_id: str,
    path: Path,
    raw_bytes: bytes | None,
    label: str,
) -> None:
    if raw_bytes is None:
        raise ProvenanceFailure(f"{label} has no exact bytes")
    selected = session["selectedBySlot"][slot_id]
    expected_hash = selected["artifactHashesByPath"].get(path)
    if expected_hash is None or expected_hash != bytes_sha256(raw_bytes):
        raise ProvenanceFailure(
            f"{label} is not sealed inside selected {slot_id} artifacts"
        )


def validate_evaluation_session_supporting_artifacts(
    session: dict[str, Any],
    *,
    actor_observation_attempts: list[dict[str, Any]],
    actor_trace_attempts: list[dict[str, Any]],
    recording_manifest_attempts: list[dict[str, Any]],
    coverage_action_ledger_attempt: dict[str, Any],
) -> None:
    for index in range(3):
        slot_id = f"SLOT-{index + 1:02d}"
        _require_session_supporting_artifact(
            session,
            slot_id,
            actor_observation_attempts[index]["path"],
            actor_observation_attempts[index]["rawBytes"],
            f"actor observation {index + 1}",
        )
        _require_session_supporting_artifact(
            session,
            slot_id,
            actor_trace_attempts[index]["path"],
            actor_trace_attempts[index]["rawBytes"],
            f"actor trace {index + 1}",
        )
        _require_session_supporting_artifact(
            session,
            slot_id,
            recording_manifest_attempts[index]["path"],
            recording_manifest_attempts[index]["rawBytes"],
            f"actor recording manifest {index + 1}",
        )
    _require_session_supporting_artifact(
        session,
        "SLOT-04",
        coverage_action_ledger_attempt["path"],
        coverage_action_ledger_attempt["rawBytes"],
        "coverage action ledger",
    )
    _require_session_supporting_artifact(
        session,
        "SLOT-04",
        recording_manifest_attempts[3]["path"],
        recording_manifest_attempts[3]["rawBytes"],
        "coverage recording manifest",
    )

    for index, attempt in enumerate(recording_manifest_attempts):
        value = attempt["value"]
        if not isinstance(value, dict) or not isinstance(
            value.get("canonicalBundleRoot"), str
        ):
            raise ProvenanceFailure(
                f"recording manifest {index + 1} lacks a canonical bundle root"
            )
        slot_id = f"SLOT-{index + 1:02d}" if index < 3 else "SLOT-04"
        selected = session["selectedBySlot"][slot_id]
        root = Path(value["canonicalBundleRoot"])
        try:
            resolved_root = root.resolve(strict=True)
            resolved_root.relative_to(selected["artifactRoot"])
        except (OSError, ValueError) as exception:
            raise ProvenanceFailure(
                f"recording manifest {index + 1} bundle root escapes selected {slot_id}"
            ) from exception
        if root != resolved_root or root.is_symlink():
            raise ProvenanceFailure(
                f"recording manifest {index + 1} bundle root is not canonical"
            )
        for artifact in value.get("artifacts", []):
            locator = artifact.get("locator") if isinstance(artifact, dict) else None
            raw_sha = artifact.get("rawSha256") if isinstance(artifact, dict) else None
            if not isinstance(locator, str):
                raise ProvenanceFailure(
                    f"recording manifest {index + 1} artifact locator is invalid"
                )
            artifact_path = (resolved_root / locator).resolve(strict=False)
            if selected["artifactHashesByPath"].get(artifact_path) != raw_sha:
                raise ProvenanceFailure(
                    f"recording manifest {index + 1} artifact {locator} is not "
                    f"sealed inside selected {slot_id}"
                )


def validate_gold_state_score_ready_authority(
    candidate_manifest_path: Path,
    gold_binding_path: Path,
    holdout_consumption_receipt_path: Path,
    registry_before_path: Path,
    registry_after_path: Path,
    evaluation_session_claim_path: Path,
    *,
    candidate_manifest_raw_bytes: bytes,
    gold_binding_raw_bytes: bytes,
    story_manifest_raw_bytes: bytes,
    holdout_consumption_receipt_raw_bytes: bytes,
    registry_before_raw_bytes: bytes,
    registry_after_raw_bytes: bytes,
    evaluation_session_claim_raw_bytes: bytes,
    require_score_ready: bool,
) -> None:
    """Delegate exact candidate/gold bytes and E09 semantics to one authority."""

    candidate_manifest_path = candidate_manifest_path.resolve(strict=False)
    gold_binding_path = gold_binding_path.resolve(strict=False)
    holdout_consumption_receipt_path = holdout_consumption_receipt_path.resolve(
        strict=False
    )
    registry_before_path = registry_before_path.resolve(strict=False)
    registry_after_path = registry_after_path.resolve(strict=False)
    evaluation_session_claim_path = evaluation_session_claim_path.resolve(strict=False)

    validator = _load_exact_validator(
        GOLD_STATE_VALIDATOR_PATH,
        "gridworks_commercial_ux_exact_gold_validator",
    )
    try:
        failures, summary = validator.validate_exact_inputs(
            REPOSITORY_ROOT,
            GOLD_STATE_MANIFEST_PATH,
            story_manifest_raw_bytes,
            False,
            candidate_manifest_raw_bytes,
            gold_binding_raw_bytes,
            require_score_ready,
            story_manifest_path_label=None,
            candidate_manifest_path_label=candidate_manifest_path,
            binding_manifest_path_label=gold_binding_path,
            holdout_consumption_receipt_bytes=(
                holdout_consumption_receipt_raw_bytes
            ),
            registry_before_bytes=registry_before_raw_bytes,
            registry_after_bytes=registry_after_raw_bytes,
            evaluation_session_claim_bytes=evaluation_session_claim_raw_bytes,
            holdout_consumption_receipt_path_label=(
                holdout_consumption_receipt_path
            ),
            registry_before_path_label=registry_before_path,
            registry_after_path_label=registry_after_path,
            evaluation_session_claim_path_label=evaluation_session_claim_path,
        )
    except (AttributeError, ImportError, OSError, SyntaxError) as exception:
        raise ProvenanceFailure(
            "candidate gold-state semantic validation did not run"
        ) from exception
    observed = summary.get("observedRawSha256", {})
    expected_observed = {
        "goldStateManifestRawSha256": file_sha256(
            GOLD_STATE_MANIFEST_PATH,
            "checked-in gold-state manifest",
        ),
        "candidateManifestRawSha256": bytes_sha256(candidate_manifest_raw_bytes),
        "goldBindingManifestRawSha256": bytes_sha256(gold_binding_raw_bytes),
        "storyManifestRawSha256": bytes_sha256(story_manifest_raw_bytes),
        "holdoutConsumptionReceiptRawSha256": bytes_sha256(
            holdout_consumption_receipt_raw_bytes
        ),
        "registryBeforeRawSha256": bytes_sha256(registry_before_raw_bytes),
        "registryAfterRawSha256": bytes_sha256(registry_after_raw_bytes),
        "evaluationSessionClaimRawSha256": bytes_sha256(
            evaluation_session_claim_raw_bytes
        ),
    }
    if observed != expected_observed:
        raise ProvenanceFailure(
            "candidate gold-state observed raw SHA projection mismatch"
        )
    if failures or (require_score_ready and not summary.get("scoreBearingReady")):
        raise ProvenanceFailure(
            "candidate gold-state semantic validation failed"
            + (f": {failures}" if failures else "")
        )


def _expected_story_selectors(campaign: dict[str, Any]) -> set[str]:
    selectors: set[str] = set()
    for chapter in campaign.get("chapters", []):
        if not isinstance(chapter, dict):
            continue
        chapter_id = chapter.get("chapterId")
        if not isinstance(chapter_id, str):
            continue
        if chapter.get("briefing") is not None:
            selectors.add(f"{chapter_id}/briefing")
        for window in chapter.get("decisionWindows", []):
            if isinstance(window, dict) and window.get("story") is not None:
                selectors.add(f"{chapter_id}/window/{window.get('windowId')}")
        if chapter.get("promise") is None and chapter.get("standardResult") is not None:
            selectors.add(f"{chapter_id}/result/standard")
        if chapter.get("promise") is not None:
            if chapter.get("keptResult") is not None:
                selectors.add(f"{chapter_id}/result/keep")
            if chapter.get("deferredResult") is not None:
                selectors.add(f"{chapter_id}/result/defer")
    if campaign.get("epilogue") is not None:
        selectors.add("campaign/epilogue")
    return selectors


def _validate_story_manifest_artifact(
    raw_bytes: bytes,
    gold: dict[str, Any],
) -> str:
    story = parse_strict_json_bytes(raw_bytes, "candidate authored story manifest")
    if not isinstance(story, dict):
        raise ProvenanceFailure("candidate authored story manifest must be an object")
    authorities = gold["authorities"]
    campaign_path = _repo_relative_path(
        authorities["campaign"].get("path"),
        "gold authorities.campaign.path",
    )
    campaign, _ = read_json_bytes(campaign_path, "candidate campaign authority")
    if not isinstance(campaign, dict):
        raise ProvenanceFailure("candidate campaign authority must be an object")
    story_authority = authorities.get("storyManifest")
    if not isinstance(story_authority, dict):
        raise ProvenanceFailure("gold authored story authority is invalid")
    parts = story.get("parts")
    expected_selectors = _expected_story_selectors(campaign)
    actual_selectors = [
        row.get("selector") for row in parts if isinstance(row, dict)
    ] if isinstance(parts, list) else []
    if (
        story.get("schemaVersion") != story_authority.get("schemaVersion")
        or story.get("campaignId") != campaign.get("campaignId")
        or not isinstance(parts, list)
        or story.get("count") != len(parts)
        or len(parts) != story_authority.get("partCount")
        or len(actual_selectors) != len(parts)
        or len(set(actual_selectors)) != len(actual_selectors)
        or set(actual_selectors) != expected_selectors
        or sum(
            isinstance(row, dict) and row.get("kind") == "result"
            for row in parts
        ) != story_authority.get("resultPartCount")
    ):
        raise ProvenanceFailure(
            "candidate authored story manifest does not match campaign/story authority"
        )
    return bytes_sha256(raw_bytes)


def _generate_story_manifest_bytes() -> bytes:
    project = REPOSITORY_ROOT / "tools/Gridworks.CommercialChecks/Gridworks.CommercialChecks.csproj"
    try:
        result = subprocess.run(
            [
                "dotnet", "run", "--project", str(project), "-c", "Release", "--",
                "--story-manifest",
            ],
            cwd=REPOSITORY_ROOT,
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=180,
        )
    except (OSError, subprocess.SubprocessError) as exception:
        raise ProvenanceFailure(
            "candidate authored story manifest could not be regenerated by the bound harness"
        ) from exception
    return result.stdout


def validate_candidate_authority_hashes(
    candidate_manifest: dict[str, Any],
    story_manifest_raw_bytes: bytes,
) -> None:
    """Recompute candidate source authorities from the exact repository bytes."""

    gold, _ = read_json_bytes(GOLD_STATE_MANIFEST_PATH, "checked-in gold-state manifest")
    if not isinstance(gold, dict) or not isinstance(gold.get("authorities"), dict):
        raise ProvenanceFailure("checked-in gold-state authorities are invalid")
    policy = gold.get("candidateExecutionPolicy")
    required = (
        policy.get("requiredCandidateAuthorityHashes")
        if isinstance(policy, dict)
        else None
    )
    declared = candidate_manifest.get("authorityHashes")
    if (
        not isinstance(required, list)
        or not all(isinstance(row, str) for row in required)
        or not isinstance(declared, dict)
        or set(declared) != set(required)
    ):
        raise ProvenanceFailure(
            "candidate authority hash keys do not match the checked-in gold contract"
        )
    authority_names = {
        "world": "world",
        "campaign": "campaign",
        "coreReplay": "coreReplay",
        "coreContracts": "coreContracts",
        "deterministicWitness": "deterministicWitness",
        "nativeSmokeWitness": "nativeSmokeWitness",
        "storyHarness": "storyHarness",
    }
    authorities = gold["authorities"]
    for candidate_field, gold_field in authority_names.items():
        authority = authorities.get(gold_field)
        if not isinstance(authority, dict):
            raise ProvenanceFailure(f"gold authority {gold_field} is missing")
        source_path = _repo_relative_path(
            authority.get("path"),
            f"gold authorities.{gold_field}.path",
        )
        observed = file_sha256(source_path, f"candidate authority {gold_field}")
        if declared.get(candidate_field) != observed:
            raise ProvenanceFailure(
                f"candidate authorityHashes.{candidate_field} does not match raw source bytes"
            )
    story_sha = _validate_story_manifest_artifact(story_manifest_raw_bytes, gold)
    regenerated_story = _generate_story_manifest_bytes()
    if story_manifest_raw_bytes != regenerated_story:
        raise ProvenanceFailure(
            "candidate authored story manifest raw bytes do not equal the deterministic "
            "CommercialChecks --story-manifest output"
        )
    if declared.get("storyManifestOutput") != story_sha:
        raise ProvenanceFailure(
            "candidate authorityHashes.storyManifestOutput does not match raw story bytes"
        )
    gold_replay_authority = authorities.get("goldReplayVerifier")
    if not isinstance(gold_replay_authority, dict):
        raise ProvenanceFailure("gold replay verifier authority is missing")
    build_inputs_path = _repo_relative_path(
        gold_replay_authority.get("buildInputsPath"),
        "gold authorities.goldReplayVerifier.buildInputsPath",
    )
    build_inputs_sha256 = gold_replay_authority.get("buildInputsSha256")
    if not isinstance(build_inputs_sha256, str) or SHA_PATTERN.fullmatch(
        build_inputs_sha256
    ) is None:
        raise ProvenanceFailure(
            "gold authorities.goldReplayVerifier.buildInputsSha256 is malformed"
        )
    gold_validator = _load_exact_validator(
        GOLD_STATE_VALIDATOR_PATH,
        "gridworks_candidate_gold_replay_build_inputs",
    )
    try:
        _, replay_build_inputs_sha256 = gold_validator.read_gold_replay_build_inputs(
            REPOSITORY_ROOT,
            build_inputs_path,
            build_inputs_sha256,
        )
    except (OSError, ValueError, gold_validator.ContractError) as exception:
        raise ProvenanceFailure(
            f"candidate gold replay build inputs are invalid: {exception}"
        ) from exception
    if declared.get("goldReplayBuildInputs") != replay_build_inputs_sha256:
        raise ProvenanceFailure(
            "candidate authorityHashes.goldReplayBuildInputs does not match "
            "candidate Core source bytes"
        )
    try:
        head = subprocess.run(
            ["git", "rev-parse", "HEAD"],
            cwd=REPOSITORY_ROOT,
            check=True,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=10,
        ).stdout.strip()
        dirty = subprocess.run(
            ["git", "status", "--porcelain", "--untracked-files=all"],
            cwd=REPOSITORY_ROOT,
            check=True,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=10,
        ).stdout
    except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired) as exception:
        raise ProvenanceFailure(
            f"candidate source git authority could not be verified: {exception}"
        ) from exception
    source = candidate_manifest["source"]
    if source["commit"] != head or source["cleanTree"] is not (not bool(dirty)):
        raise ProvenanceFailure(
            "candidate source commit/cleanTree does not match the exact repository worktree"
        )


def validate_checked_in_contract_hashes(candidate: dict[str, Any]) -> None:
    """Bind candidate provenance to the exact frozen native contract bytes."""

    provenance = candidate["provenance"]
    expected = {
        "promptTemplateSha256": NATIVE_DIRECTORY / "native-judge-prompt.template.txt",
        "judgmentSchemaSha256": NATIVE_DIRECTORY / "native-judge.schema.json",
        "verifierPromptTemplateSha256": (
            NATIVE_DIRECTORY / "native-evidence-verifier-prompt.template.txt"
        ),
        "verifierSchemaSha256": NATIVE_DIRECTORY / "native-evidence-verifier.schema.json",
        "coldRecipeSha256": NATIVE_DIRECTORY / "cold-journey-recipe.json",
        "coverageRecipeSha256": NATIVE_DIRECTORY / "coverage-recipe.json",
        "holdoutRecipeSha256": NATIVE_DIRECTORY / "holdout-recipes.json",
        "nativeAggregatorSha256": Path(__file__).resolve(),
    }
    for field, path in expected.items():
        computed = file_sha256(path, f"checked-in {path.name}")
        if provenance[field] != computed:
            raise ProvenanceFailure(
                f"candidate provenance {field} does not match checked-in {path.name}: "
                f"expected {computed}, got {provenance[field]}"
            )


def validate_self_hashed_envelope(
    value: Any,
    raw_bytes: bytes,
    *,
    schema_version: str,
    self_field: str,
    schema_validator: Any,
    label: str,
) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ProvenanceFailure(f"{label} must be an object")
    schema_validator(value)
    if value.get("schemaVersion") != schema_version or value.get("protocol") != PROTOCOL:
        raise ProvenanceFailure(f"{label} identity is invalid")
    declared = require_sha(value.get(self_field), f"{label}.{self_field}", ProvenanceFailure)
    computed = self_sha256(value, self_field, label)
    if declared != computed:
        raise ProvenanceFailure(
            f"{label} self-hash mismatch: declared {declared}, computed {computed}"
        )
    return {
        "value": value,
        "selfSha256": declared,
        "rawSha256": bytes_sha256(raw_bytes),
    }


def _selected_recipe(candidate_manifest: dict[str, Any]) -> dict[str, Any]:
    queue_value, _ = read_json_bytes(
        NATIVE_DIRECTORY / "holdout-recipes.json",
        "checked-in holdout recipe queue",
    )
    recipe_id = candidate_manifest["recipes"]["selectedRecipeId"]
    rows = [queue_value.get("formative")] + list(queue_value.get("holdouts", []))
    matches = [row for row in rows if isinstance(row, dict) and row.get("id") == recipe_id]
    if len(matches) != 1:
        raise ProvenanceFailure(f"selected recipe {recipe_id} is absent or duplicated")
    selected = matches[0]
    expected_sha = canonical_sha256(selected)
    if candidate_manifest["recipes"]["selectedRecipeSha256"] != expected_sha:
        raise ProvenanceFailure("candidate selectedRecipeSha256 does not match its frozen queue row")
    return selected


def _coverage_recipe_episode_ids() -> list[str]:
    recipe, _ = read_json_bytes(
        NATIVE_DIRECTORY / "coverage-recipe.json",
        "checked-in coverage recipe",
    )
    episodes = recipe.get("episodes") if isinstance(recipe, dict) else None
    if not isinstance(episodes, list) or not all(
        isinstance(row, dict) and isinstance(row.get("id"), str)
        for row in episodes
    ):
        raise ProvenanceFailure("checked-in coverage recipe episode order is invalid")
    episode_ids = [row["id"] for row in episodes]
    if len(episode_ids) != 12 or len(set(episode_ids)) != 12:
        raise ProvenanceFailure("checked-in coverage recipe must contain 12 unique episodes")
    return episode_ids


def _selected_coverage_presentation_episode_ids(
    selected_recipe: dict[str, Any],
) -> list[str]:
    episode_ids = _coverage_recipe_episode_ids()
    order = selected_recipe["coverageArtifactOrder"]
    if order == "EPISODE_ASCENDING":
        return episode_ids
    if order == "EPISODE_DESCENDING":
        return list(reversed(episode_ids))
    raise ProvenanceFailure("selected holdout coverage artifact order is invalid")


BRANCH_REALIZATION_BLOCKS: dict[str, dict[str, list[str] | str]] = {
    "E04-NORTH-BANK": {
        "prelude": [],
        "keep": [
            "NORTH_BANK_CAPTURE_KEEP_PREDICTION",
            "NORTH_BANK_APPLY_KEEP_BRANCH",
            "KEEP_BUILD_WATER_BRANCH",
            "APPROVE_KEEP_RESULT",
        ],
        "defer": [
            "CAPTURE_DEFER_PREDICTION",
            "NORTH_BANK_APPLY_DEFER_BRANCH",
            "DEFER_BUILD_WATER_BRANCH",
            "APPROVE_DEFER_RESULT",
        ],
        "replay": "NORTH_BANK_REPLAY_PREFIX",
        "postlude": [],
    },
    "E05-WHOSE-MARGIN": {
        "prelude": [],
        "keep": [
            "KEEP_BUILD_SELECTED_MARGIN_PROTOTYPE",
            "WHOSE_MARGIN_CAPTURE_KEEP_PREDICTION",
            "WHOSE_MARGIN_APPLY_KEEP_BRANCH",
            "APPROVE_HEAT_WINDOW",
            "INSPECT_PROTECTIVE_SHUTDOWN",
            "APPROVE_MORNING_WINDOW",
        ],
        "defer": [
            "DEFER_BUILD_SELECTED_MARGIN_PROTOTYPE",
            "WHOSE_MARGIN_APPLY_DEFER_BRANCH",
            "APPROVE_BOTH_WINDOWS",
        ],
        "replay": "WHOSE_MARGIN_REPLAY_PREFIX",
        "postlude": [],
    },
    "E07-MAINTENANCE": {
        "prelude": ["VERIFY_THERMAL_RESET"],
        "keep": [
            "KEEP_BUILD_SELECTED_MAINTENANCE_PROTOTYPE",
            "MAINTENANCE_CAPTURE_KEEP_PREDICTION",
            "MAINTENANCE_APPLY_KEEP_BRANCH",
            "KEEP_APPROVE_MAINTENANCE_RESULT",
        ],
        "defer": [
            "DEFER_BUILD_SELECTED_MAINTENANCE_PROTOTYPE",
            "MAINTENANCE_APPLY_DEFER_BRANCH",
            "DEFER_APPROVE_MAINTENANCE_RESULT",
        ],
        "replay": "MAINTENANCE_REPLAY_PREFIX",
        "postlude": [],
    },
    "E08-FINALE": {
        "prelude": [],
        "keep": [
            "CAPTURE_FINAL_KEEP_PREDICTION",
            "APPLY_FINAL_KEEP_BRANCH",
            "KEEP_APPROVE_FINAL_HEAT",
            "KEEP_APPROVE_FINAL_STORM",
            "OPEN_KEEP_EPILOGUE",
        ],
        "defer": [
            "CAPTURE_FINAL_DEFER_PREDICTION",
            "APPLY_FINAL_DEFER_BRANCH",
            "DEFER_APPROVE_FINAL_HEAT",
            "DEFER_APPROVE_FINAL_STORM",
            "OPEN_DEFER_EPILOGUE",
        ],
        "replay": "FINALE_REPLAY_PREFIX",
        "postlude": ["SELECT_COMPLETED_CHAPTER"],
    },
}

PROTOTYPE_ACTION_SLOTS = {
    "KEEP_BUILD_SELECTED_MARGIN_PROTOTYPE": 0,
    "DEFER_BUILD_SELECTED_MARGIN_PROTOTYPE": 0,
    "BUILD_SELECTED_FLOOD_PROTOTYPE": 1,
    "KEEP_BUILD_SELECTED_MAINTENANCE_PROTOTYPE": 2,
    "DEFER_BUILD_SELECTED_MAINTENANCE_PROTOTYPE": 2,
}


def _realized_coverage_actions(
    episode_id: str,
    base_actions: list[str],
    selected_recipe: dict[str, Any],
) -> list[str]:
    blocks = BRANCH_REALIZATION_BLOCKS.get(episode_id)
    if blocks is None:
        return list(base_actions)
    branch_order = selected_recipe.get("promiseBranchOrder")
    if branch_order not in (["keep", "defer"], ["defer", "keep"]):
        raise ProvenanceFailure("selected holdout promise branch order is invalid")
    realized = [
        *blocks["prelude"],
        *blocks[branch_order[0]],
        blocks["replay"],
        *blocks[branch_order[1]],
        *blocks["postlude"],
    ]
    if sorted(realized) != sorted(base_actions) or len(realized) != len(base_actions):
        raise ProvenanceFailure(
            f"checked-in branch realization for {episode_id} drifted from coverage recipe"
        )
    return realized


def _expected_action_realization(
    episode_id: str,
    action_occurrence_id: str,
    selected_recipe: dict[str, Any],
) -> dict[str, Any]:
    slot = PROTOTYPE_ACTION_SLOTS.get(action_occurrence_id)
    prototype_kind = None
    if slot is not None:
        prototype_kind = (
            "REINFORCED"
            if selected_recipe["missionPrototypeBits"][slot] == "1"
            else "STANDARD"
        )
    branch_decision: str | None = None
    branch_ordinal: int | None = None
    blocks = BRANCH_REALIZATION_BLOCKS.get(episode_id)
    if blocks is not None:
        for decision in ("keep", "defer"):
            if action_occurrence_id in blocks[decision]:
                branch_decision = decision
                branch_ordinal = selected_recipe["promiseBranchOrder"].index(decision)
                break
    return {
        "prototypeSlot": slot,
        "prototypeKind": prototype_kind,
        "branchSequenceOrdinal": branch_ordinal,
        "branchDecision": branch_decision,
        "checkpointBranchId": (
            "SHARED" if branch_decision is None else branch_decision.upper()
        ),
    }


def _candidate_reuse_sha256(candidate_manifest: dict[str, Any]) -> str:
    return canonical_sha256({
        "sourceCommit": candidate_manifest["source"]["commit"],
        "executionArtifactSha256": candidate_manifest["execution"][
            "executionArtifactSha256"
        ],
        "authorityHashes": candidate_manifest["authorityHashes"],
    })


def _registry_authority(
    value: Any,
    raw_bytes: bytes,
    *,
    label: str,
) -> dict[str, Any]:
    return validate_self_hashed_envelope(
        value,
        raw_bytes,
        schema_version=HOLDOUT_CONSUMPTION_REGISTRY_SCHEMA,
        self_field="holdoutConsumptionRegistrySha256",
        schema_validator=validate_holdout_consumption_registry_schema,
        label=label,
    )


def _canonical_holdout_registry_path() -> Path:
    try:
        common_dir = subprocess.run(
            ["git", "rev-parse", "--git-common-dir"],
            cwd=REPOSITORY_ROOT,
            check=True,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=10,
        ).stdout.strip()
    except (OSError, subprocess.SubprocessError) as exception:
        raise ProvenanceFailure("cannot resolve the repository holdout registry authority") from exception
    common_path = Path(common_dir)
    if not common_path.is_absolute():
        common_path = REPOSITORY_ROOT / common_path
    return (
        common_path.resolve(strict=False)
        / "gridworks-commercial-ux"
        / "holdout-consumption-registry-v1.json"
    )


def _canonical_holdout_receipt_path(receipt: dict[str, Any]) -> Path:
    transaction_id = receipt["atomicClaim"]["transactionId"]
    if not isinstance(transaction_id, str) or SHA256_PATTERN.fullmatch(transaction_id) is None:
        raise ProvenanceFailure("holdout transactionId must be a canonical claim SHA-256")
    return (
        _canonical_holdout_registry_path().parent
        / "holdout-receipts"
        / (
            transaction_id.removeprefix("sha256:")
            + "-"
            + receipt["candidateReuseSha256"].removeprefix("sha256:")
            + ".json"
        )
    )


def validate_holdout_consumption_authority(
    value: Any,
    raw_bytes: bytes,
    receipt_path: Path,
    registry_before: tuple[Any, bytes],
    registry_after: tuple[Any, bytes],
    registry_after_path: Path,
    candidate_manifest: dict[str, Any],
) -> dict[str, Any]:
    wrapped = validate_self_hashed_envelope(
        value,
        raw_bytes,
        schema_version=HOLDOUT_CONSUMPTION_RECEIPT_SCHEMA,
        self_field="holdoutConsumptionReceiptSha256",
        schema_validator=validate_holdout_consumption_receipt_schema,
        label="holdout consumption receipt",
    )
    receipt = wrapped["value"]
    before = _registry_authority(
        registry_before[0],
        registry_before[1],
        label="holdout registry before claim",
    )
    after = _registry_authority(
        registry_after[0],
        registry_after[1],
        label="holdout registry after claim",
    )
    queue, queue_bytes = read_json_bytes(HOLDOUT_QUEUE_PATH, "checked-in holdout queue")
    if not isinstance(queue, dict):
        raise ProvenanceFailure("checked-in holdout queue must be an object")
    queue_sha = bytes_sha256(queue_bytes)
    recipe = _selected_recipe(candidate_manifest)
    recipe_id = candidate_manifest["recipes"]["selectedRecipeId"]
    phase = candidate_manifest["evaluationPhase"]
    official = candidate_manifest["officialCommercialUX"]
    selected_expected = {
        "recipeId": recipe_id,
        "ordinal": recipe.get("ordinal"),
        "selectedRecipeSha256": candidate_manifest["recipes"]["selectedRecipeSha256"],
        "missionPrototypeBits": recipe["missionPrototypeBits"],
        "routeFamily": recipe.get("routeFamily"),
        "promiseBranchOrder": recipe["promiseBranchOrder"],
        "actorArtifactPermutation": recipe["actorArtifactPermutation"],
        "coverageArtifactOrder": recipe["coverageArtifactOrder"],
        "coveragePresentationEpisodeIds": (
            _selected_coverage_presentation_episode_ids(recipe)
        ),
    }
    expected = {
        "holdoutConsumptionReceiptSchemaSha256": file_sha256(
            HOLDOUT_CONSUMPTION_RECEIPT_SCHEMA_PATH,
            "checked-in holdout consumption receipt schema",
        ),
        "candidateId": candidate_manifest["candidateId"],
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "candidateReuseSha256": _candidate_reuse_sha256(candidate_manifest),
        "sourceCommit": candidate_manifest["source"]["commit"],
        "evaluationPhase": phase,
        "officialCommercialUX": official,
        "holdoutQueueSha256": queue_sha,
        "queueAuthorityId": queue["queueAuthorityId"],
        "registryScope": queue["registryPolicy"]["scope"],
        "selectionRule": queue["selectionRule"],
        "reuseAllowed": queue["reuseAllowed"],
        "selectedRecipe": selected_expected,
    }
    for field, expected_value in expected.items():
        if receipt[field] != expected_value:
            raise ProvenanceFailure(f"holdout consumption receipt {field} mismatch")
    atomic = receipt["atomicClaim"]
    canonical_receipt_path = _canonical_holdout_receipt_path(receipt)
    canonical_receipt = str(canonical_receipt_path)
    canonical_registry_path = _canonical_holdout_registry_path()
    canonical_registry = str(canonical_registry_path)
    if (
        receipt_path.resolve(strict=False) != canonical_receipt_path
        or registry_after_path.resolve(strict=False) != canonical_registry_path
        or
        atomic["receiptInputPath"] != canonical_receipt
        or atomic["canonicalReceiptPath"] != canonical_receipt
        or atomic["registryInputPath"] != canonical_registry
        or atomic["canonicalRegistryPath"] != canonical_registry
        or before["value"]["canonicalRegistryPath"] != canonical_registry
        or after["value"]["canonicalRegistryPath"] != canonical_registry
    ):
        raise ProvenanceFailure("holdout atomic claim canonical path binding mismatch")
    hash_bindings = {
        "registryBeforeSha256": before["selfSha256"],
        "registryBeforeRawSha256": before["rawSha256"],
        "registryAfterSha256": after["selfSha256"],
        "registryAfterRawSha256": after["rawSha256"],
    }
    for field, expected_value in hash_bindings.items():
        if atomic.get(field) != expected_value:
            raise ProvenanceFailure(f"holdout atomic claim {field} mismatch")
    expected_transaction_id = canonical_sha256({
        "queueAuthorityId": receipt["queueAuthorityId"],
        "registryBeforeSha256": before["selfSha256"],
        "candidateReuseSha256": receipt["candidateReuseSha256"],
        "candidateManifestSha256": receipt["candidateManifestSha256"],
        "selectedRecipeSha256": receipt["selectedRecipe"]["selectedRecipeSha256"],
        "ordinal": receipt["selectedRecipe"]["ordinal"],
    })
    if atomic["transactionId"] != expected_transaction_id:
        raise ProvenanceFailure("holdout transactionId is not derived from the exact claim")
    for registry_label, registry in (("before", before["value"]), ("after", after["value"])):
        if (
            registry["queueAuthorityId"] != queue["queueAuthorityId"]
            or registry["holdoutQueueSha256"] != queue_sha
            or registry["revision"] != len(registry["consumptions"])
        ):
            raise ProvenanceFailure(f"holdout registry {registry_label} authority mismatch")
        rows = registry["consumptions"]
        if [row["ordinal"] for row in rows] != list(range(1, len(rows) + 1)):
            raise ProvenanceFailure(
                f"holdout registry {registry_label} is not a lowest-ordinal append ledger"
            )
        if [row["recipeId"] for row in rows] != [
            f"HOLDOUT-{index:02d}" for index in range(1, len(rows) + 1)
        ]:
            raise ProvenanceFailure(f"holdout registry {registry_label} recipe order mismatch")
        if len({row["candidateReuseSha256"] for row in rows}) != len(rows):
            raise ProvenanceFailure(f"holdout registry {registry_label} reuses a candidate")
        if len({row["transactionId"] for row in rows}) != len(rows) or any(
            SHA256_PATTERN.fullmatch(row["transactionId"]) is None for row in rows
        ):
            raise ProvenanceFailure(
                f"holdout registry {registry_label} transaction IDs are not unique SHA-256 claims"
            )

    before_rows = before["value"]["consumptions"]
    after_rows = after["value"]["consumptions"]
    if official:
        ordinal = recipe.get("ordinal")
        if ordinal != len(before_rows) + 1:
            raise ProvenanceFailure(
                "holdout selection is not the lowest unused official ordinal"
            )
        appended = {
            "ordinal": ordinal,
            "recipeId": recipe_id,
            "candidateReuseSha256": receipt["candidateReuseSha256"],
            "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
            "sourceCommit": candidate_manifest["source"]["commit"],
            "transactionId": atomic["transactionId"],
        }
        if after_rows != [*before_rows, appended]:
            raise ProvenanceFailure(
                "holdout registry after claim is not the exact atomic append"
            )
        prior = receipt["priorConsumptions"]
        if len(prior) != len(before_rows):
            raise ProvenanceFailure("holdout receipt prior consumption coverage mismatch")
        for receipt_row, registry_row in zip(prior, before_rows):
            if receipt_row != registry_row:
                raise ProvenanceFailure("holdout receipt prior consumption projection mismatch")
    elif before_rows != after_rows or before["value"]["revision"] != after["value"]["revision"]:
        raise ProvenanceFailure("formative selection must not consume the official holdout registry")
    if atomic["priorConsumptionSetSha256"] != canonical_sha256(receipt["priorConsumptions"]):
        raise ProvenanceFailure("holdout prior consumption set hash mismatch")
    return {**wrapped, "beforeRegistry": before, "afterRegistry": after}


def _gold_checkpoint_branch_id(
    episode_id: str,
    checkpoint_id: str,
    selected_recipe: dict[str, Any],
) -> str:
    if "keep" in checkpoint_id:
        return "KEEP"
    if "defer" in checkpoint_id:
        return "DEFER"
    if checkpoint_id == "north-bank-first-result":
        return selected_recipe["promiseBranchOrder"][0].upper()
    if checkpoint_id in {
        "emergency-use",
        "protective-shutdown",
        "planned-source-outage",
        "finale-heat",
        "finale-storm",
        "finale-result-to-epilogue",
    }:
        return "KEEP"
    return "SHARED"


def _checked_in_gold_generator_sha256() -> str:
    gold, _ = read_json_bytes(GOLD_STATE_MANIFEST_PATH, "checked-in gold-state manifest")
    generator = gold.get("nextRequiredGenerator") if isinstance(gold, dict) else None
    path = _repo_relative_path(
        generator.get("path") if isinstance(generator, dict) else None,
        "gold nextRequiredGenerator.path",
    )
    if not path.is_file():
        raise ValidationFailure(
            "BLOCKED_PRE_CAPTURE: deterministic candidate gold-binding generator is missing"
        )
    return file_sha256(path, "candidate gold-binding generator")


def validate_gold_binding_authority(
    value: Any,
    raw_bytes: bytes,
    candidate_manifest: dict[str, Any],
    holdout_receipt: dict[str, Any],
    evaluation_run: dict[str, Any],
    *,
    candidate_manifest_path: Path,
    gold_binding_path: Path,
    candidate_manifest_raw_bytes: bytes,
    story_manifest_raw_bytes: bytes,
    holdout_consumption_receipt_path: Path,
    registry_before_path: Path,
    registry_after_path: Path,
    evaluation_session_claim_path: Path,
    holdout_consumption_receipt_raw_bytes: bytes,
    registry_before_raw_bytes: bytes,
    registry_after_raw_bytes: bytes,
    evaluation_session_claim_raw_bytes: bytes,
) -> dict[str, Any]:
    wrapped = validate_self_hashed_envelope(
        value,
        raw_bytes,
        schema_version=GOLD_BINDING_SCHEMA,
        self_field="goldBindingManifestSha256",
        schema_validator=validate_gold_binding_schema,
        label="candidate gold binding manifest",
    )
    binding = wrapped["value"]
    recipes = candidate_manifest["recipes"]
    expected = {
        "goldBindingSchemaSha256": file_sha256(
            GOLD_BINDING_SCHEMA_PATH,
            "checked-in gold binding schema",
        ),
        "goldStateContractSha256": recipes["goldStateContractSha256"],
        "coverageRecipeSha256": recipes["coverageSha256"],
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "holdoutConsumptionReceiptSha256": holdout_receipt["selfSha256"],
        "selectedRecipeId": recipes["selectedRecipeId"],
        "selectedRecipeSha256": recipes["selectedRecipeSha256"],
        "executionArtifactSha256": candidate_manifest["execution"]["executionArtifactSha256"],
        "generatorToolSha256": _checked_in_gold_generator_sha256(),
    }
    for field, expected_value in expected.items():
        if binding[field] != expected_value:
            raise ProvenanceFailure(f"candidate gold binding {field} mismatch")
    gold, _ = read_json_bytes(GOLD_STATE_MANIFEST_PATH, "checked-in gold-state manifest")
    if not isinstance(gold, dict):
        raise ProvenanceFailure("checked-in gold-state manifest is invalid")
    selected = _selected_recipe(candidate_manifest)
    expected_realization = {
        "missionPrototypeBits": selected["missionPrototypeBits"],
        "promiseBranchOrder": selected["promiseBranchOrder"],
        "actorArtifactPermutation": selected["actorArtifactPermutation"],
        "coverageArtifactOrder": selected["coverageArtifactOrder"],
        "coveragePresentationEpisodeIds": (
            _selected_coverage_presentation_episode_ids(selected)
        ),
    }
    if binding["holdoutRealization"] != expected_realization:
        raise ProvenanceFailure(
            "candidate gold holdoutRealization is not the exact selected recipe projection"
        )
    expected_prefix_ids = [row["prefixId"] for row in gold["prefixes"]]
    if [row["prefixId"] for row in binding["prefixBindings"]] != expected_prefix_ids:
        raise ProvenanceFailure("candidate gold prefix binding IDs/order mismatch")
    expected_checkpoints: list[tuple[str, str, str]] = []
    template_statuses: list[str] = []
    for episode in gold["episodes"]:
        for checkpoint in episode["checkpointBindings"]:
            expected_checkpoints.append((
                episode["id"],
                checkpoint["checkpointId"],
                _gold_checkpoint_branch_id(
                    episode["id"], checkpoint["checkpointId"], selected
                ),
            ))
            template_statuses.append(checkpoint["journalBinding"]["status"])
    actual_checkpoints = [
        (row["episodeId"], row["checkpointId"], row["checkpointBranchId"])
        for row in binding["checkpointBindings"]
    ]
    if actual_checkpoints != expected_checkpoints:
        raise ProvenanceFailure("candidate gold checkpoint binding IDs/branch/order mismatch")
    all_rows = [*binding["prefixBindings"], *binding["checkpointBindings"]]
    all_template_statuses = [
        row["journalBinding"]["status"] for row in gold["prefixes"]
    ] + template_statuses
    for row, template_status in zip(all_rows, all_template_statuses):
        expected_status = (
            "NOT_APPLICABLE"
            if template_status == "NOT_APPLICABLE"
            else "BOUND_NATIVE_REPLAY"
        )
        if (
            row["journalBinding"]["status"] != expected_status
            or row["snapshotBinding"]["status"] != expected_status
        ):
            raise ProvenanceFailure("candidate gold binding applicability/status drifted")
    bound_count = sum(
        row["journalBinding"]["status"] == "BOUND_NATIVE_REPLAY" for row in all_rows
    )
    not_applicable_count = len(all_rows) - bound_count
    derived_summary = {
        "prefixCount": len(binding["prefixBindings"]),
        "checkpointCount": len(binding["checkpointBindings"]),
        "applicableBindingCount": bound_count,
        "boundBindingCount": bound_count,
        "notApplicableBindingCount": not_applicable_count,
        "allApplicableBindingsExact": True,
    }
    if binding["bindingSummary"] != derived_summary:
        raise ProvenanceFailure("candidate gold binding summary is not derived from exact rows")
    witness = binding["e09NorthBankTwoProcessWitness"]
    if (
        witness["preExitProcessTreeId"] == witness["postResumeProcessTreeId"]
        or witness["preExitJournalSha256"] != witness["postResumeJournalSha256"]
        or witness["preExitSnapshotSha256"] != witness["postResumeSnapshotSha256"]
    ):
        raise ProvenanceFailure("candidate E09 two-process replay witness is not exact")
    artifacts = evaluation_run["artifacts"]
    if artifacts["goldBindingManifestSha256"] != wrapped["selfSha256"]:
        raise ProvenanceFailure("evaluation gold binding self-hash mismatch")
    if artifacts["goldBindingManifestRawSha256"] != wrapped["rawSha256"]:
        raise ProvenanceFailure("evaluation gold binding raw hash mismatch")
    expected_evaluation = {
        "bindingManifestSha256": wrapped["selfSha256"],
        **derived_summary,
        "e09NorthBankTwoProcessWitness": True,
        "bindingRequired": True,
        "derivedReady": True,
    }
    if evaluation_run["goldBindings"] != expected_evaluation:
        raise ProvenanceFailure("evaluation goldBindings are not derived from raw binding rows")
    if binding["scoreBearingReady"] is not True:
        raise ProvenanceFailure("candidate gold scoreBearingReady is not derived true")
    validate_gold_state_score_ready_authority(
        candidate_manifest_path,
        gold_binding_path,
        holdout_consumption_receipt_path,
        registry_before_path,
        registry_after_path,
        evaluation_session_claim_path,
        candidate_manifest_raw_bytes=candidate_manifest_raw_bytes,
        gold_binding_raw_bytes=raw_bytes,
        story_manifest_raw_bytes=story_manifest_raw_bytes,
        holdout_consumption_receipt_raw_bytes=(
            holdout_consumption_receipt_raw_bytes
        ),
        registry_before_raw_bytes=registry_before_raw_bytes,
        registry_after_raw_bytes=registry_after_raw_bytes,
        evaluation_session_claim_raw_bytes=evaluation_session_claim_raw_bytes,
        require_score_ready=candidate_manifest["officialCommercialUX"],
    )
    wrapped["derivedReady"] = True
    return wrapped


def validate_candidate_manifest_authority(
    value: Any,
    raw_bytes: bytes,
    candidate: dict[str, Any],
    story_manifest_raw_bytes: bytes,
) -> dict[str, Any]:
    wrapped = validate_self_hashed_envelope(
        value,
        raw_bytes,
        schema_version=CANDIDATE_MANIFEST_SCHEMA,
        self_field="candidateManifestSha256",
        schema_validator=validate_candidate_manifest_schema,
        label="candidate manifest",
    )
    manifest = wrapped["value"]
    provenance = candidate["provenance"]
    if provenance["candidateManifestSha256"] != wrapped["selfSha256"]:
        raise ProvenanceFailure("aggregation input candidate manifest self-hash mismatch")
    if provenance["candidateManifestRawSha256"] != wrapped["rawSha256"]:
        raise ProvenanceFailure("aggregation input candidate manifest raw hash mismatch")
    expected_phase = "FORMATIVE" if candidate["recipeId"] == "FORMATIVE-01" else "OFFICIAL_HOLDOUT"
    expected_official = expected_phase == "OFFICIAL_HOLDOUT"
    if (
        manifest["evaluationPhase"] != expected_phase
        or manifest["officialCommercialUX"] is not expected_official
        or manifest["recipes"]["selectedRecipeId"] != candidate["recipeId"]
    ):
        raise ProvenanceFailure("candidate phase/official/selected recipe binding mismatch")
    source = manifest["source"]
    evaluator = manifest["evaluator"]
    if source["commit"] != provenance["sourceCommit"] or source["cleanTree"] != provenance["cleanTree"]:
        raise ProvenanceFailure("candidate source provenance mismatch")
    if evaluator["resolvedModelId"] != provenance["model"] or evaluator["reasoningEffort"] != provenance["reasoningEffort"]:
        raise ProvenanceFailure("candidate evaluator model/effort mismatch")
    contract = manifest["contractHashes"]
    checked_contract = {
        "contractBindingsSha256": NATIVE_DIRECTORY / "contract-bindings.json",
        "canonicalHashPolicySha256": NATIVE_DIRECTORY / "canonical-hash-policy.json",
        "rubricSha256": DEFAULT_RUBRIC_PATH,
        "coldActorPromptSha256": NATIVE_DIRECTORY / "cold-actor-prompt.template.txt",
        "coldActorResponseSchemaSha256": COLD_ACTOR_RESPONSE_SCHEMA_PATH,
        "actorActionLedgerSchemaSha256": ACTOR_ACTION_LEDGER_SCHEMA_PATH,
        "actorObservationSchemaSha256": ACTOR_OBSERVATION_SCHEMA_PATH,
        "actorTraceSchemaSha256": NATIVE_DIRECTORY / "actor-trace.schema.json",
        "coverageTraceSchemaSha256": COVERAGE_TRACE_SCHEMA_PATH,
        "evidenceSetSchemaSha256": EVIDENCE_SET_SCHEMA_PATH,
        "nativeJudgePromptSha256": NATIVE_DIRECTORY / "native-judge-prompt.template.txt",
        "nativeJudgeSchemaSha256": NATIVE_DIRECTORY / "native-judge.schema.json",
        "judgePanelSchemaSha256": JUDGE_PANEL_SCHEMA_PATH,
        "qualificationInputSchemaSha256": NATIVE_DIRECTORY / "qualification-input.schema.json",
        "qualificationReceiptSchemaSha256": QUALIFICATION_RECEIPT_SCHEMA_PATH,
        "nativeVerifierPromptSha256": NATIVE_DIRECTORY / "native-evidence-verifier-prompt.template.txt",
        "nativeVerifierInputSchemaSha256": NATIVE_DIRECTORY / "native-evidence-verification-input.schema.json",
        "nativeVerifierSchemaSha256": NATIVE_DIRECTORY / "native-evidence-verifier.schema.json",
        "oracleHardGateSchemaSha256": NATIVE_DIRECTORY / "oracle-hard-gate-ledger.schema.json",
        "nativeAggregationInputSchemaSha256": AGGREGATION_INPUT_SCHEMA_PATH,
        "nativeScorecardSchemaSha256": SCORECARD_SCHEMA_PATH,
        "evaluationRunManifestSchemaSha256": EVALUATION_RUN_SCHEMA_PATH,
        "nativeReplacementReceiptSchemaSha256": REPLACEMENT_RECEIPT_SCHEMA_PATH,
        "nativeAggregatorSha256": Path(__file__).resolve(),
    }
    for field, path in checked_contract.items():
        actual = file_sha256(path, f"checked-in {path.name}")
        if contract[field] != actual:
            raise ProvenanceFailure(f"candidate contract hash {field} mismatch")
    direct_contract = {
        "rubricSha256": "rubricSha256",
        "nativeJudgePromptSha256": "promptTemplateSha256",
        "nativeJudgeSchemaSha256": "judgmentSchemaSha256",
        "nativeVerifierPromptSha256": "verifierPromptTemplateSha256",
        "nativeVerifierSchemaSha256": "verifierSchemaSha256",
        "nativeAggregatorSha256": "nativeAggregatorSha256",
    }
    for manifest_field, provenance_field in direct_contract.items():
        if contract[manifest_field] != provenance[provenance_field]:
            raise ProvenanceFailure(f"candidate contract/provenance field {manifest_field} mismatch")
    recipes = manifest["recipes"]
    for manifest_field, provenance_field in (
        ("coldJourneySha256", "coldRecipeSha256"),
        ("coverageSha256", "coverageRecipeSha256"),
        ("holdoutQueueSha256", "holdoutRecipeSha256"),
    ):
        if recipes[manifest_field] != provenance[provenance_field]:
            raise ProvenanceFailure(f"candidate recipe field {manifest_field} mismatch")
    checked_recipe_hashes = {
        "conceptExposureSha256": NATIVE_DIRECTORY / "concept-exposure-manifest.json",
        "goldStateContractSha256": NATIVE_DIRECTORY / "gold-state-manifest.json",
        "qualificationAnchorsSha256": NATIVE_DIRECTORY / "qualification-anchors.json",
    }
    for field, path in checked_recipe_hashes.items():
        if recipes[field] != file_sha256(path, f"checked-in {path.name}"):
            raise ProvenanceFailure(f"candidate recipe authority {field} mismatch")
    _selected_recipe(manifest)
    execution = manifest["execution"]
    validate_candidate_execution_authority(manifest)
    if (
        execution["executionArtifactSha256"] != provenance["executionArtifactSha256"]
        or execution["packageSha256"] != provenance["packageSha256"]
        or execution["packageStatus"] != provenance["packageStatus"]
    ):
        raise ProvenanceFailure("candidate execution artifact/package mismatch")
    validate_candidate_authority_hashes(manifest, story_manifest_raw_bytes)
    wrapped["evaluationPhase"] = expected_phase
    wrapped["officialCommercialUX"] = expected_official
    return wrapped


def validate_qualification_authority(
    value: Any,
    raw_bytes: bytes,
    candidate: dict[str, Any],
    candidate_manifest: dict[str, Any],
) -> dict[str, Any]:
    wrapped = validate_self_hashed_envelope(
        value,
        raw_bytes,
        schema_version=QUALIFICATION_RECEIPT_SCHEMA,
        self_field="qualificationReceiptSha256",
        schema_validator=validate_qualification_receipt_schema,
        label="qualification receipt",
    )
    receipt = wrapped["value"]
    provenance = candidate["provenance"]
    if provenance["qualificationReceiptSha256"] != wrapped["selfSha256"]:
        raise ProvenanceFailure("aggregation input qualification receipt self-hash mismatch")
    if provenance["qualificationReceiptRawSha256"] != wrapped["rawSha256"]:
        raise ProvenanceFailure("aggregation input qualification receipt raw hash mismatch")
    evaluator = candidate_manifest["evaluator"]
    contract = candidate_manifest["contractHashes"]
    expected = {
        "model": provenance["model"],
        "reasoningEffort": provenance["reasoningEffort"],
        "transportVersion": evaluator["transportVersion"],
        "promptTemplateSha256": provenance["promptTemplateSha256"],
        "judgmentSchemaSha256": provenance["judgmentSchemaSha256"],
        "qualificationInputSchemaSha256": contract["qualificationInputSchemaSha256"],
        "qualificationAnchorsAuthoritySha256": candidate_manifest["recipes"]["qualificationAnchorsSha256"],
        "qualificationTransportMapSha256": file_sha256(
            NATIVE_DIRECTORY / "qualification-transport-map.json",
            "qualification transport map",
        ),
        "rubricSha256": provenance["rubricSha256"],
    }
    for field, expected_value in expected.items():
        if receipt[field] != expected_value:
            raise ProvenanceFailure(f"qualification receipt {field} mismatch")
    all_run_ids: list[str] = []
    all_hashes: list[str] = []
    for attempt_index, attempt in enumerate(receipt["attempts"]):
        run_ids = [slot["judgeRunId"] for slot in attempt["slots"]]
        hashes = [slot["judgmentRawSha256"] for slot in attempt["slots"]]
        if len(set(run_ids)) != 3 or len(set(hashes)) != 3:
            raise ProvenanceFailure(
                f"qualification attempt {attempt_index + 1} judge runs/hashes must be fresh"
            )
        all_run_ids.extend(run_ids)
        all_hashes.extend(hashes)
        slot_passes = []
        for slot in attempt["slots"]:
            band_passes = (
                slot["exactCount"] >= 19
                and slot["excellentAndBrokenAllExact"] is True
                and slot["schemaValidCount"] == 20
            )
            if slot["status"] == "PASS" and not band_passes:
                raise ProvenanceFailure("qualification PASS slot violates its frozen pass rule")
            if slot["status"] == "FAIL_SCHEMA" and slot["schemaValidCount"] == 20:
                raise ProvenanceFailure("qualification FAIL_SCHEMA must have schemaValidCount < 20")
            if slot["status"] == "FAIL_BAND" and (
                slot["schemaValidCount"] != 20 or band_passes
            ):
                raise ProvenanceFailure("qualification FAIL_BAND predicate is inconsistent")
            slot_passes.append(slot["status"] == "PASS")
        if (attempt["status"] == "PASS") is not all(slot_passes):
            raise ProvenanceFailure("qualification attempt status does not match its three slots")
        if attempt["status"] in {"INVALIDATED", "FAIL"} and all(slot_passes):
            raise ProvenanceFailure("failed qualification attempt cannot contain three PASS slots")
    if len(set(all_run_ids)) != len(all_run_ids) or len(set(all_hashes)) != len(all_hashes):
        raise ProvenanceFailure("qualification replacement must use six disjoint runs and outputs")
    attempts = receipt["attempts"]
    if len(attempts) == 1:
        if receipt["status"] != "PASS" or attempts[0]["status"] != "PASS":
            raise ProvenanceFailure("failed first qualification panel requires one full replacement")
    else:
        if attempts[0]["status"] != "INVALIDATED":
            raise ProvenanceFailure("qualification attempt 1 must be INVALIDATED before replacement")
        expected_final = "PASS" if receipt["status"] == "PASS" else "FAIL"
        if attempts[1]["status"] != expected_final:
            raise ProvenanceFailure("qualification receipt status does not match replacement attempt")
    wrapped["status"] = receipt["status"]
    return wrapped


def _derived_severe_incident(observation: dict[str, Any], incident: dict[str, Any]) -> bool:
    kind = incident["incidentType"]
    action_by_index = {row["actionIndex"]: row for row in observation["actionLedger"]}
    actions = [action_by_index[index] for index in incident["actionIndexes"] if index in action_by_index]
    if len(actions) != len(incident["actionIndexes"]):
        raise ProvenanceFailure("actor incident cites an unknown action index")
    if kind == "EXTERNAL_HINT_ATTEMPT":
        return True
    if kind == "CONFUSION":
        if incident["confusionBoundary"] is None:
            return False
        approval_ordinals = {row["checkpointOrdinal"] for row in observation["approvalRecords"]}
        return (
            len(incident["checkpointOrdinals"]) >= 2
            and set(incident["checkpointOrdinals"]) <= approval_ordinals
        )
    if kind == "RECOVERY_FRICTION":
        if len(actions) < 3:
            return False
        kinds = {row["actionKind"] for row in actions}
        checkpoints = {(row["episode"], row["checkpoint"]) for row in actions}
        state_hashes = {
            value
            for row in actions
            for value in (row["preStateSha256"], row["postStateSha256"])
        }
        return (
            len(kinds) >= 3
            and len(checkpoints) == 1
            and all(row["rationalInProductAction"] and row["appActive"] for row in actions)
            and len(state_hashes) == 1
        )
    if kind == "UX_STALL":
        if len(actions) != 12:
            return False
        kinds = {row["actionKind"] for row in actions}
        checkpoints = {(row["episode"], row["checkpoint"]) for row in actions}
        state_hashes = {
            value
            for row in actions
            for value in (row["preStateSha256"], row["postStateSha256"])
        }
        return (
            len(kinds) == 12
            and len(checkpoints) == 1
            and all(row["rationalInProductAction"] and row["appActive"] for row in actions)
            and len(state_hashes) == 1
            and observation["terminalState"] == "PLAYER_STALLED"
            and observation["terminalIncidentKey"] == incident["incidentKey"]
        )
    return False


def _require_exact_ordinals(
    rows: Any,
    ordinal_field: str,
    label: str,
) -> None:
    if not isinstance(rows, list) or [
        row.get(ordinal_field) if isinstance(row, dict) else None
        for row in rows
    ] != list(range(1, len(rows) + 1)):
        raise ProvenanceFailure(f"{label} {ordinal_field} must be exactly 1..N in order")


def validate_cold_actor_response_authorities(
    inputs: list[tuple[Any, bytes]],
    evaluation_run: dict[str, Any],
) -> list[dict[str, Any]]:
    if len(inputs) != 3:
        raise ProvenanceFailure("exactly three cold actor responses are required")
    artifacts = evaluation_run["artifacts"]
    expected_self = artifacts.get("coldActorResponseSha256")
    expected_raw = artifacts.get("coldActorResponseRawSha256")
    if (
        not isinstance(expected_self, list)
        or not isinstance(expected_raw, list)
        or len(expected_self) != 3
        or len(expected_raw) != 3
        or len(set(expected_self)) != 3
        or len(set(expected_raw)) != 3
    ):
        raise ProvenanceFailure(
            "evaluation run must bind three distinct cold actor response raw/self hashes"
        )
    results: list[dict[str, Any]] = []
    observed_self: set[str] = set()
    observed_raw: set[str] = set()
    for input_index, (value, raw_bytes) in enumerate(inputs):
        label = f"cold actor response {input_index + 1}"
        wrapped = validate_self_hashed_envelope(
            value,
            raw_bytes,
            schema_version=COLD_ACTOR_RESPONSE_SCHEMA,
            self_field="coldActorResponseSha256",
            schema_validator=validate_cold_actor_response_schema,
            label=label,
        )
        response_self = wrapped["selfSha256"]
        response_raw = wrapped["rawSha256"]
        if response_self in observed_self or response_raw in observed_raw:
            raise ProvenanceFailure(
                "cold actor responses contain duplicate raw/self hashes"
            )
        observed_self.add(response_self)
        observed_raw.add(response_raw)
        try:
            slot = expected_self.index(response_self)
        except ValueError as exception:
            raise ProvenanceFailure(
                f"{label} self hash is absent from evaluation run"
            ) from exception
        if expected_raw[slot] != response_raw:
            raise ProvenanceFailure(f"{label} raw hash does not match evaluation run")
        if value["actorRunId"] != artifacts["actorRunIds"][slot]:
            raise ProvenanceFailure(f"{label} actorRunId/stable slot mismatch")
        _require_exact_ordinals(
            value["firstUseRecords"],
            "firstUseOrdinal",
            label,
        )
        _require_exact_ordinals(
            value["approvalRecords"],
            "approvalOrdinal",
            label,
        )
        _require_exact_ordinals(value["incidents"], "incidentOrdinal", label)
        results.append({**wrapped, "slot": slot})
    if observed_self != set(expected_self) or observed_raw != set(expected_raw):
        raise ProvenanceFailure(
            "cold actor response files do not exactly cover evaluation run bindings"
        )
    results.sort(key=lambda row: row["slot"])
    return results


def _validate_cold_response_observation_projection(
    response: dict[str, Any],
    observation: dict[str, Any],
    label: str,
) -> None:
    value = response["value"]
    exact_top = (
        "actorRunId",
        "actorSlot",
        "model",
        "reasoningEffort",
        "objective",
        "terminalState",
        "terminalIncidentOrdinal",
    )
    for field in exact_top:
        if observation[field] != value[field]:
            raise ProvenanceFailure(
                f"{label} {field} differs from the raw cold actor response"
            )
    if (
        observation["coldActorResponseSha256"] != response["selfSha256"]
        or observation["coldActorResponseRawSha256"] != response["rawSha256"]
    ):
        raise ProvenanceFailure(
            f"{label} cold actor response raw/self binding mismatch"
        )

    def projection(rows: list[dict[str, Any]], fields: tuple[str, ...]) -> list[dict[str, Any]]:
        return [{field: row[field] for field in fields} for row in rows]

    projections = (
        (
            "firstUseRecords",
            (
                "firstUseOrdinal", "probeId", "currentGoal",
                "expectedVisibleConsequence", "citedVisibleSourceDescription",
            ),
        ),
        (
            "approvalRecords",
            (
                "approvalOrdinal", "predictionImmediatelyBeforeApproval",
                "observedResult", "causalAccount",
            ),
        ),
        (
            "incidents",
            (
                "incidentOrdinal", "incidentType", "confusionBoundary",
                "severity", "description",
            ),
        ),
    )
    for collection, fields in projections:
        if projection(observation[collection], fields) != value[collection]:
            raise ProvenanceFailure(
                f"{label} {collection} semantic projection differs from the raw "
                "cold actor response"
            )
    terminal_ordinal = observation["terminalIncidentOrdinal"]
    expected_terminal_key = None
    if terminal_ordinal is not None:
        matches = [
            row["incidentKey"]
            for row in observation["incidents"]
            if row["incidentOrdinal"] == terminal_ordinal
        ]
        if len(matches) != 1:
            raise ProvenanceFailure(
                f"{label} terminalIncidentOrdinal does not resolve exactly once"
            )
        expected_terminal_key = matches[0]
    if observation["terminalIncidentKey"] != expected_terminal_key:
        raise ProvenanceFailure(
            f"{label} terminal incident key is not response-ordinal derived"
        )


def _cold_checkpoint_completion_authority() -> dict[str, Any]:
    recipe, _ = read_json_bytes(COLD_RECIPE_PATH, "checked-in cold journey recipe")
    rows = recipe.get("checkpointSequence") if isinstance(recipe, dict) else None
    if not isinstance(rows, list) or not rows:
        raise ProvenanceFailure(
            "checked-in cold checkpoint sequence authority is invalid"
        )
    by_checkpoint: dict[tuple[str, str], int] = {}
    rows_by_checkpoint: dict[tuple[str, str], dict[str, Any]] = {}
    ordinals: list[int] = []
    groups_by_ordinal: dict[int, list[str | None]] = {}
    for row in rows:
        if not isinstance(row, dict):
            raise ProvenanceFailure(
                "checked-in cold checkpoint sequence row is invalid"
            )
        episode = row.get("episode")
        checkpoint = row.get("checkpoint")
        ordinal = row.get("sequenceOrdinal")
        group = row.get("branchAlternativeGroup")
        completion_requirement = row.get("completionRequirement")
        if (
            not isinstance(episode, str)
            or not isinstance(checkpoint, str)
            or not isinstance(ordinal, int)
            or isinstance(ordinal, bool)
            or ordinal < 1
            or (episode, checkpoint) in by_checkpoint
            or (group is not None and not isinstance(group, str))
            or completion_requirement not in {"MANDATORY", "OPTIONAL"}
        ):
            raise ProvenanceFailure(
                "checked-in cold checkpoint sequence row is invalid"
            )
        by_checkpoint[(episode, checkpoint)] = ordinal
        rows_by_checkpoint[(episode, checkpoint)] = row
        ordinals.append(ordinal)
        groups_by_ordinal.setdefault(ordinal, []).append(group)
    if ordinals != sorted(ordinals) or set(ordinals) != set(
        range(1, max(ordinals) + 1)
    ):
        raise ProvenanceFailure(
            "checked-in cold checkpoint sequence ordinals are not contiguous/in order"
        )
    for ordinal, groups in groups_by_ordinal.items():
        if len(groups) == 1 and groups[0] is None:
            continue
        if (
            len(groups) != 2
            or groups[0] is None
            or groups[0] != groups[1]
        ):
            raise ProvenanceFailure(
                f"checked-in cold checkpoint sequence alternatives are invalid at {ordinal}"
            )
        requirements = {
            rows_by_checkpoint[key]["completionRequirement"]
            for key, row_ordinal in by_checkpoint.items()
            if row_ordinal == ordinal
        }
        if len(requirements) != 1:
            raise ProvenanceFailure(
                f"checked-in cold checkpoint alternatives disagree on requirement at {ordinal}"
            )
    mandatory_ordinals = [
        ordinal
        for ordinal in sorted(groups_by_ordinal)
        if any(
            row["sequenceOrdinal"] == ordinal
            and row["completionRequirement"] == "MANDATORY"
            for row in rows
        )
    ]
    if not mandatory_ordinals or mandatory_ordinals[-1] != max(ordinals):
        raise ProvenanceFailure(
            "checked-in cold completion sequence must end at a mandatory checkpoint"
        )
    return {
        "byCheckpoint": by_checkpoint,
        "rowsByCheckpoint": rows_by_checkpoint,
        "mandatoryOrdinals": mandatory_ordinals,
        "terminalOrdinal": max(ordinals),
    }


def _cold_checkpoint_sequence_authority() -> dict[tuple[str, str], int]:
    return _cold_checkpoint_completion_authority()["byCheckpoint"]


def _validate_cold_terminal_checkpoint_sequence(
    observation: dict[str, Any],
    authority: dict[str, Any],
    label: str,
) -> None:
    """Require a completed journey or an exact mandatory terminal prefix.

    Optional error/settings observations may appear, but they cannot substitute
    for any progression milestone.  Branch-result ordinals are alternatives:
    the strict ordinal check already permits exactly one realized branch row.
    """

    checkpoints = observation["checkpoints"]
    rows_by_checkpoint = authority["rowsByCheckpoint"]
    observed_mandatory_ordinals = [
        checkpoint["recipeCheckpointSequenceOrdinal"]
        for checkpoint in checkpoints
        if rows_by_checkpoint[(checkpoint["episode"], checkpoint["checkpoint"])][
            "completionRequirement"
        ]
        == "MANDATORY"
    ]
    terminal_state = observation["terminalState"]
    if terminal_state == "COMPLETED":
        frontier = authority["terminalOrdinal"]
        last = checkpoints[-1]
        if (
            last["recipeCheckpointSequenceOrdinal"] != frontier
            or (last["episode"], last["checkpoint"])
            != ("E08-FINALE", "completed-chapter-select")
        ):
            raise ProvenanceFailure(
                f"{label} COMPLETED must terminate at completed-chapter-select"
            )
    elif terminal_state in {"PLAYER_STALLED", "HARNESS_BLOCKED"}:
        terminal_key = observation["terminalIncidentKey"]
        terminal_incident = next(
            (
                row
                for row in observation["incidents"]
                if row["incidentKey"] == terminal_key
            ),
            None,
        )
        if (
            terminal_incident is None
            or max(terminal_incident["checkpointOrdinals"]) != len(checkpoints)
        ):
            raise ProvenanceFailure(
                f"{label} stalled terminal incident must cite the last checkpoint"
            )
        frontier = checkpoints[-1]["recipeCheckpointSequenceOrdinal"]
    else:
        return
    expected_mandatory_ordinals = [
        ordinal for ordinal in authority["mandatoryOrdinals"] if ordinal <= frontier
    ]
    if observed_mandatory_ordinals != expected_mandatory_ordinals:
        kind = "completion" if terminal_state == "COMPLETED" else "terminal prefix"
        raise ProvenanceFailure(
            f"{label} does not exactly realize the frozen mandatory {kind} sequence"
        )


def validate_actor_observation_authorities(
    inputs: list[tuple[Any, bytes]],
    evaluation_run: dict[str, Any],
    cold_actor_responses: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    if len(inputs) != 3:
        raise ProvenanceFailure("exactly three actor observations are required")
    if len(cold_actor_responses) != 3:
        raise ProvenanceFailure("exactly three cold actor response authorities are required")
    checkpoint_authority = _cold_checkpoint_completion_authority()
    checkpoint_sequence = checkpoint_authority["byCheckpoint"]
    artifacts = evaluation_run["artifacts"]
    terminal_rows = evaluation_run["terminalStates"]
    expected_pairs = set(zip(
        artifacts["actorArtifactIds"],
        artifacts["actorObservationRawSha256"],
    ))
    terminal_by_pair = {
        (row["actorArtifactId"], row["actorObservationRawSha256"]): row
        for row in terminal_rows
    }
    if len(expected_pairs) != 3 or set(terminal_by_pair) != expected_pairs:
        raise ProvenanceFailure(
            "evaluation terminal rows must exactly map its three actor artifact/raw pairs"
        )
    artifact_by_raw = {
        raw_sha: {
            "actorArtifactId": actor_artifact_id,
            "actorRunId": actor_run_id,
        }
        for actor_artifact_id, raw_sha, actor_run_id in zip(
            artifacts["actorArtifactIds"],
            artifacts["actorObservationRawSha256"],
            artifacts["actorRunIds"],
        )
    }
    if len(artifact_by_raw) != 3:
        raise ProvenanceFailure("evaluation run must bind three distinct actor observations")
    results: list[dict[str, Any]] = []
    observed_raw_hashes: set[str] = set()
    response_by_run_id = {
        row["value"]["actorRunId"]: row for row in cold_actor_responses
    }
    if len(response_by_run_id) != 3:
        raise ProvenanceFailure("cold actor responses duplicate actorRunId")
    for index, (value, raw_bytes) in enumerate(inputs):
        label = f"actor observation {index + 1}"
        validate_actor_observation_schema(value)
        if value["schemaVersion"] != ACTOR_OBSERVATION_SCHEMA or value["protocol"] != PROTOCOL:
            raise ProvenanceFailure(f"{label} identity is invalid")
        raw_sha = bytes_sha256(raw_bytes)
        authority = artifact_by_raw.get(raw_sha)
        if authority is None or raw_sha in observed_raw_hashes:
            raise ProvenanceFailure(f"{label} raw hash is absent or duplicated")
        observed_raw_hashes.add(raw_sha)
        if value["actorRunId"] != authority["actorRunId"]:
            raise ProvenanceFailure(f"{label} actorRunId mismatch")
        response = response_by_run_id.get(value["actorRunId"])
        if response is None:
            raise ProvenanceFailure(f"{label} has no exact cold actor response")
        _validate_cold_response_observation_projection(response, value, label)
        terminal = terminal_by_pair[(authority["actorArtifactId"], raw_sha)]

        action_rows = value["actionLedger"]
        action_indices = [row["actionIndex"] for row in action_rows]
        if action_indices != list(range(1, len(action_rows) + 1)):
            raise ProvenanceFailure(
                f"{label} actionLedger actionIndex must be exactly 1..N in order"
            )
        action_by_index = {row["actionIndex"]: row for row in action_rows}
        checkpoint_rows = value["checkpoints"]
        checkpoint_ordinals = [row["ordinal"] for row in checkpoint_rows]
        if checkpoint_ordinals != list(range(1, len(checkpoint_rows) + 1)):
            raise ProvenanceFailure(
                f"{label} checkpoint ordinal must be exactly 1..N in order"
            )
        checkpoint_by_ordinal = {row["ordinal"]: row for row in checkpoint_rows}
        recipe_checkpoint_ordinals: list[int] = []
        for checkpoint in checkpoint_rows:
            expected_recipe_ordinal = checkpoint_sequence.get(
                (checkpoint["episode"], checkpoint["checkpoint"])
            )
            if (
                expected_recipe_ordinal is None
                or checkpoint["recipeCheckpointSequenceOrdinal"]
                != expected_recipe_ordinal
            ):
                raise ProvenanceFailure(
                    f"{label} checkpoint is not exact-bound to the cold recipe sequence"
                )
            recipe_checkpoint_ordinals.append(expected_recipe_ordinal)
            action_index = checkpoint["appActiveActionIndex"]
            if action_index != 0 and action_index not in action_by_index:
                raise ProvenanceFailure(f"{label} checkpoint cites an unknown active action")
            if (
                action_index != 0
                and checkpoint["progressStateSha256"]
                != action_by_index[action_index]["postStateSha256"]
            ):
                raise ProvenanceFailure(
                    f"{label} checkpoint progress state is not the cited action post-state"
                )
        if recipe_checkpoint_ordinals != sorted(set(recipe_checkpoint_ordinals)):
            raise ProvenanceFailure(
                f"{label} checkpoints must strictly follow cold recipe sequence order"
            )
        for collection_name in ("firstUseRecords", "approvalRecords"):
            for record in value[collection_name]:
                checkpoint = checkpoint_by_ordinal.get(record["checkpointOrdinal"])
                if checkpoint is None or (
                    record["episode"], record["checkpoint"]
                ) != (checkpoint["episode"], checkpoint["checkpoint"]):
                    raise ProvenanceFailure(
                        f"{label} {collection_name} row does not match its checkpoint ordinal"
                    )
            checkpoint_ordinals = [
                record["checkpointOrdinal"] for record in value[collection_name]
            ]
            if checkpoint_ordinals != sorted(checkpoint_ordinals):
                raise ProvenanceFailure(
                    f"{label} {collection_name} must follow checkpoint chronology"
                )

        incident_keys = [row["incidentKey"] for row in value["incidents"]]
        if len(set(incident_keys)) != len(incident_keys):
            raise ProvenanceFailure(f"{label} incident keys must be unique")
        if set(terminal["incidentKeys"]) != set(incident_keys):
            raise ProvenanceFailure(f"{label} incident key ledger mismatch")
        severe_rows = []
        incident_checkpoint_frontiers: list[int] = []
        for incident in value["incidents"]:
            cited_actions = incident["actionIndexes"]
            cited_checkpoints = incident["checkpointOrdinals"]
            if cited_actions != sorted(set(cited_actions)):
                raise ProvenanceFailure(
                    f"{label} incident actionIndexes must be unique and strictly increasing"
                )
            if cited_checkpoints != sorted(set(cited_checkpoints)):
                raise ProvenanceFailure(
                    f"{label} incident checkpointOrdinals must be unique and strictly increasing"
                )
            incident_checkpoint_frontiers.append(cited_checkpoints[0])
            checkpoint_authorities = []
            for ordinal in cited_checkpoints:
                checkpoint = checkpoint_by_ordinal.get(ordinal)
                if checkpoint is None:
                    raise ProvenanceFailure(f"{label} incident cites an unknown checkpoint ordinal")
                if checkpoint["episode"] != incident["episode"]:
                    raise ProvenanceFailure(
                        f"{label} incident checkpoint belongs to another episode"
                    )
                checkpoint_authorities.append(checkpoint)
            allowed_checkpoint_names = {row["checkpoint"] for row in checkpoint_authorities}
            for action_index in cited_actions:
                action = action_by_index.get(action_index)
                if action is None:
                    raise ProvenanceFailure(f"{label} incident cites an unknown action index")
                if (
                    action["episode"] != incident["episode"]
                    or action["checkpoint"] not in allowed_checkpoint_names
                ):
                    raise ProvenanceFailure(
                        f"{label} incident action is outside its cited episode/checkpoint"
                    )
            derived = _derived_severe_incident(value, incident)
            if (incident["severity"] == "SEVERE") is not derived:
                raise ProvenanceFailure(
                    f"{label} incident {incident['incidentKey']} severity is not derivable"
                )
            if derived:
                severe_rows.append(incident["incidentKey"])
        if incident_checkpoint_frontiers != sorted(incident_checkpoint_frontiers):
            raise ProvenanceFailure(
                f"{label} incidents must follow checkpoint chronology"
            )
        if (
            terminal["state"] != value["terminalState"]
            or terminal["terminalIncidentKey"] != value["terminalIncidentKey"]
        ):
            raise ProvenanceFailure(f"{label} terminal state mismatch")
        if value["terminalState"] == "COMPLETED" and value["terminalIncidentKey"] is not None:
            raise ProvenanceFailure(f"{label} COMPLETED must have terminalIncidentKey=null")
        if value["terminalState"] in {"PLAYER_STALLED", "HARNESS_BLOCKED"}:
            if value["terminalIncidentKey"] is None or value["terminalIncidentKey"] not in incident_keys:
                raise ProvenanceFailure(f"{label} terminal incident must be present in incidents")
            terminal_incident = next(
                row
                for row in value["incidents"]
                if row["incidentKey"] == value["terminalIncidentKey"]
            )
            expected_terminal_type = (
                "UX_STALL"
                if value["terminalState"] == "PLAYER_STALLED"
                else "HARNESS_FAILURE"
            )
            if terminal_incident["incidentType"] != expected_terminal_type:
                raise ProvenanceFailure(
                    f"{label} terminal incident type must be {expected_terminal_type}"
                )
        _validate_cold_terminal_checkpoint_sequence(
            value,
            checkpoint_authority,
            label,
        )
        results.append({
            "value": value,
            "rawSha256": raw_sha,
            "actorArtifactId": authority["actorArtifactId"],
            "terminalState": terminal["state"],
            "terminalIncidentKey": terminal["terminalIncidentKey"],
            "declaredSevereSingleRun": terminal["severeSingleRun"],
            "severeIncidentKeys": severe_rows,
            "incidentKeys": incident_keys,
            "incidentTypes": {
                incident["incidentKey"]: incident["incidentType"]
                for incident in value["incidents"]
            },
            "coldActorResponse": response,
        })
    if observed_raw_hashes != set(artifacts["actorObservationRawSha256"]):
        raise ProvenanceFailure("actor observations must have three distinct raw hashes")
    for row in results:
        row["severeSingleRun"] = False
    return results


RECORDING_MIME_BY_KIND = {
    "FRAME": {"image/png"},
    # v1.1 deliberately accepts only media with a deterministic stdlib decoder.
    # Video and MP3 remain schema-reserved but cannot enter score-bearing evidence.
    "VIDEO": set(),
    "AUDIO": {"audio/wav"},
    "ACTION_LEDGER": {"application/json"},
    "AUDIO_SYNC_LEDGER": {"application/json"},
    "VISIBLE_TEXT": {"application/json"},
}

AUDIO_SYNC_LEDGER_KEYS = {
    "schemaVersion",
    "protocol",
    "sourceActionLedgerRawSha256",
    "clockDomainId",
    "events",
}

AUDIO_SYNC_EVENT_KEYS = {
    "syncEventId",
    "cellId",
    "episodeId",
    "checkpoint",
    "actionOccurrenceId",
    "actionIndex",
    "actionDeliveredMonotonicNanoseconds",
    "audioArtifactId",
    "audioArtifactRawSha256",
    "audioCaptureStartedMonotonicNanoseconds",
    "cueOnsetSampleIndex",
}

AUDIO_SYNC_CELLS = ("V1", "V2", "V3", "V4")
AUDIO_SYNC_EPISODE_BY_CELL = {
    "V1": "E01-FIRST-LIGHT",
    "V2": "E05-WHOSE-MARGIN",
    "V3": "E06-FLOOD",
    "V4": "E08-FINALE",
}
AUDIO_SYNC_MAX_LATENCY_NS = 100_000_000


def _valid_png_bytes(raw: bytes) -> bool:
    if not raw.startswith(b"\x89PNG\r\n\x1a\n"):
        return False
    offset = 8
    saw_ihdr = False
    saw_idat = False
    saw_iend = False
    idat_closed = False
    width = 0
    height = 0
    bytes_per_pixel = 0
    compressed = bytearray()
    while offset + 12 <= len(raw):
        length = struct.unpack(">I", raw[offset:offset + 4])[0]
        chunk_type = raw[offset + 4:offset + 8]
        end = offset + 12 + length
        if end > len(raw):
            return False
        data = raw[offset + 8:offset + 8 + length]
        expected_crc = struct.unpack(">I", raw[offset + 8 + length:end])[0]
        if zlib.crc32(chunk_type + data) & 0xFFFFFFFF != expected_crc:
            return False
        if not saw_ihdr:
            if chunk_type != b"IHDR" or length != 13:
                return False
            width, height, bit_depth, color_type, compression, png_filter, interlace = (
                struct.unpack(">IIBBBBB", data)
            )
            if (
                width == 0
                or height == 0
                or width > 32768
                or height > 32768
                or bit_depth != 8
                or color_type not in {2, 6}
                or compression != 0
                or png_filter != 0
                or interlace != 0
            ):
                return False
            bytes_per_pixel = 3 if color_type == 2 else 4
            saw_ihdr = True
        elif chunk_type == b"IHDR":
            return False
        if chunk_type == b"IDAT":
            if idat_closed or length == 0:
                return False
            saw_idat = True
            compressed.extend(data)
        elif saw_idat and chunk_type != b"IEND":
            # This deliberately restricted decoder accepts one consecutive
            # IDAT run only.  It never silently ignores a post-image chunk.
            idat_closed = True
        if chunk_type == b"IEND":
            if length != 0 or end != len(raw):
                return False
            saw_iend = True
            break
        if chunk_type not in {b"IHDR", b"IDAT", b"IEND"}:
            # Score-bearing frames use the frozen 8-bit RGB/RGBA subset.  A
            # producer must normalize ancillary/critical chunks before capture.
            return False
        offset = end
    if not (saw_ihdr and saw_idat and saw_iend and compressed):
        return False
    expected_scanline_length = height * (1 + width * bytes_per_pixel)
    if expected_scanline_length > 512 * 1024 * 1024:
        return False
    try:
        decoder = zlib.decompressobj()
        decoded = decoder.decompress(
            bytes(compressed),
            expected_scanline_length + 1,
        )
        decoded += decoder.flush()
    except zlib.error:
        return False
    if (
        not decoder.eof
        or decoder.unused_data
        or decoder.unconsumed_tail
        or len(decoded) != expected_scanline_length
    ):
        return False
    scanline_length = 1 + width * bytes_per_pixel
    return all(
        decoded[offset] in {0, 1, 2, 3, 4}
        for offset in range(0, expected_scanline_length, scanline_length)
    )


def _recording_bytes_match_mime(raw: bytes, mime_type: str) -> bool:
    if mime_type == "image/png":
        return _valid_png_bytes(raw)
    if mime_type == "audio/wav":
        try:
            with wave.open(io.BytesIO(raw), "rb") as stream:
                return (
                    stream.getnchannels() > 0
                    and stream.getframerate() > 0
                    and stream.getsampwidth() > 0
                    and stream.getnframes() > 0
                    and bool(stream.readframes(stream.getnframes()))
                )
        except (EOFError, wave.Error):
            return False
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError:
        return False
    if "\x00" in text:
        return False
    if mime_type == "text/plain":
        return True
    if mime_type == "application/json":
        try:
            parsed = parse_strict_json_bytes(raw, "recording JSON artifact")
        except ValidationFailure:
            return False
        return isinstance(parsed, (dict, list))
    return False


def _wav_metadata(raw: bytes) -> dict[str, int]:
    try:
        with wave.open(io.BytesIO(raw), "rb") as stream:
            frame_count = stream.getnframes()
            sample_rate = stream.getframerate()
            channel_count = stream.getnchannels()
            sample_width = stream.getsampwidth()
            compression = stream.getcomptype()
            frame_bytes = stream.readframes(frame_count)
    except (EOFError, wave.Error) as exception:
        raise ProvenanceFailure("recording WAV cannot be decoded deterministically") from exception
    expected_bytes = frame_count * channel_count * sample_width
    if (
        frame_count <= 0
        or sample_rate <= 0
        or channel_count <= 0
        or sample_width <= 0
        or compression != "NONE"
        or len(frame_bytes) != expected_bytes
    ):
        raise ProvenanceFailure("recording WAV metadata/frame payload is incomplete")
    return {
        "frameCount": frame_count,
        "sampleRateHz": sample_rate,
        "channelCount": channel_count,
        "sampleWidthBytes": sample_width,
    }


def _validate_audio_sync_ledger(
    coverage_recording: dict[str, Any],
    coverage_action_ledger: dict[str, Any],
) -> dict[str, Any]:
    manifest = coverage_recording["value"]
    sync_rows = [
        row for row in manifest["artifacts"] if row["kind"] == "AUDIO_SYNC_LEDGER"
    ]
    if len(sync_rows) != 1:
        raise ProvenanceFailure(
            "coverage recording must contain exactly one AUDIO_SYNC_LEDGER"
        )
    sync_artifact = sync_rows[0]
    sync_raw = coverage_recording["artifactRawByKey"][
        (sync_artifact["artifactId"], sync_artifact["locator"])
    ]
    sync = exact_keys(
        parse_strict_json_bytes(sync_raw, "coverage AUDIO_SYNC_LEDGER"),
        AUDIO_SYNC_LEDGER_KEYS,
        "coverage AUDIO_SYNC_LEDGER",
    )
    if (
        sync["schemaVersion"]
        != "gridworks.commercial-ux.native-audio-sync-ledger.v1"
        or sync["protocol"] != PROTOCOL
        or sync["sourceActionLedgerRawSha256"]
        != manifest["actionLedgerArtifactRawSha256"]
        or sync["sourceActionLedgerRawSha256"]
        != coverage_action_ledger["rawSha256"]
    ):
        raise ProvenanceFailure("coverage audio-sync ledger identity/action binding mismatch")
    require_string(sync["clockDomainId"], "coverage audio-sync clockDomainId", 200)
    if sync["clockDomainId"] != coverage_action_ledger["value"]["clockDomainId"]:
        raise ProvenanceFailure(
            "coverage audio-sync clock domain is not action-ledger-derived"
        )
    events = sync["events"]
    if not isinstance(events, list) or len(events) != 4:
        raise ProvenanceFailure("coverage audio-sync ledger must contain V1..V4 exactly once")

    actions_by_occurrence: dict[tuple[str, str], dict[str, Any]] = {}
    for episode in coverage_action_ledger["value"]["episodes"]:
        for action in episode["actions"]:
            key = (episode["episodeId"], action["actionOccurrenceId"])
            if key in actions_by_occurrence:
                raise ProvenanceFailure(
                    "coverage action ledger duplicates an audio-sync action occurrence"
                )
            actions_by_occurrence[key] = action
    artifact_by_id = {
        row["artifactId"]: row for row in manifest["artifacts"]
    }
    derived_events: list[dict[str, Any]] = []
    evidence_refs = [{
        "artifactId": sync_artifact["artifactId"],
        "locator": sync_artifact["locator"],
        "sha256": sync_artifact["rawSha256"],
    }]
    input_hashes = [sync_artifact["rawSha256"]]
    seen_audio_refs: set[str] = set()
    for index, (item, expected_cell) in enumerate(zip(events, AUDIO_SYNC_CELLS), start=1):
        event = exact_keys(
            item,
            AUDIO_SYNC_EVENT_KEYS,
            f"coverage AUDIO_SYNC_LEDGER.events[{index - 1}]",
        )
        if (
            event["syncEventId"] != f"AVSYNC-{expected_cell}"
            or event["cellId"] != expected_cell
            or event["episodeId"] != AUDIO_SYNC_EPISODE_BY_CELL[expected_cell]
        ):
            raise ProvenanceFailure("coverage audio-sync V1..V4 identity/order mismatch")
        action = actions_by_occurrence.get(
            (event["episodeId"], event["actionOccurrenceId"])
        )
        if (
            action is None
            or event["checkpoint"] != action["checkpoint"]
            or event["actionIndex"] != action["actionIndex"]
            or event["actionDeliveredMonotonicNanoseconds"]
            != action["deliveredMonotonicNanoseconds"]
        ):
            raise ProvenanceFailure(
                "coverage audio-sync action occurrence/timestamp is not ledger-derived"
            )
        for field in (
            "actionDeliveredMonotonicNanoseconds",
            "audioCaptureStartedMonotonicNanoseconds",
            "cueOnsetSampleIndex",
        ):
            if (
                not isinstance(event[field], int)
                or isinstance(event[field], bool)
                or event[field] < 0
            ):
                raise ProvenanceFailure(f"coverage audio-sync {field} is invalid")
        audio_artifact = artifact_by_id.get(event["audioArtifactId"])
        if (
            audio_artifact is None
            or audio_artifact["kind"] != "AUDIO"
            or audio_artifact["rawSha256"] != event["audioArtifactRawSha256"]
        ):
            raise ProvenanceFailure("coverage audio-sync WAV reference/raw hash mismatch")
        audio_raw = coverage_recording["artifactRawByKey"][
            (audio_artifact["artifactId"], audio_artifact["locator"])
        ]
        wav = _wav_metadata(audio_raw)
        if wav["sampleRateHz"] != 48_000:
            raise ProvenanceFailure("coverage audio-sync WAV sample rate must be 48000 Hz")
        cue_sample = event["cueOnsetSampleIndex"]
        if cue_sample >= wav["frameCount"]:
            raise ProvenanceFailure("coverage audio-sync cue onset is outside WAV frames")
        cue_ns = event["audioCaptureStartedMonotonicNanoseconds"] + (
            cue_sample * 1_000_000_000 // wav["sampleRateHz"]
        )
        latency_ns = cue_ns - event["actionDeliveredMonotonicNanoseconds"]
        derived_events.append({
            "syncEventId": event["syncEventId"],
            "cellId": expected_cell,
            "actionOccurrenceId": event["actionOccurrenceId"],
            "actionDeliveredMonotonicNanoseconds": event[
                "actionDeliveredMonotonicNanoseconds"
            ],
            "cueOnsetMonotonicNanoseconds": cue_ns,
            "latencyMicroseconds": latency_ns // 1000,
            "within100Milliseconds": 0 <= latency_ns <= AUDIO_SYNC_MAX_LATENCY_NS,
        })
        if audio_artifact["rawSha256"] not in input_hashes:
            input_hashes.append(audio_artifact["rawSha256"])
        if audio_artifact["artifactId"] not in seen_audio_refs:
            seen_audio_refs.add(audio_artifact["artifactId"])
            evidence_refs.append({
                "artifactId": audio_artifact["artifactId"],
                "locator": audio_artifact["locator"],
                "sha256": audio_artifact["rawSha256"],
            })
    passed = all(row["within100Milliseconds"] for row in derived_events)
    return {
        "status": "PASS" if passed else "FAIL",
        "failureCode": None if passed else "AUDIO_SYNC_OVER_100MS",
        "inputHashes": input_hashes,
        "evidenceRefs": evidence_refs,
        "observed": json.dumps(
            derived_events,
            ensure_ascii=False,
            separators=(",", ":"),
            sort_keys=True,
        ),
        "events": derived_events,
        "syncLedgerRawSha256": sync_artifact["rawSha256"],
    }


def _resolve_recording_artifact(
    root: Path,
    locator: str,
    *,
    label: str,
) -> Path:
    locator_path = Path(locator)
    if (
        locator_path.is_absolute()
        or not locator_path.parts
        or any(part in {"", ".", ".."} for part in locator_path.parts)
    ):
        raise ProvenanceFailure(f"{label} locator is not a canonical relative path")
    target = root.joinpath(locator_path)
    try:
        resolved = target.resolve(strict=True)
    except OSError as exception:
        raise ProvenanceFailure(f"{label} locator is not a readable file") from exception
    if resolved != target or not resolved.is_file():
        raise ProvenanceFailure(f"{label} locator uses a symlink or is not a regular file")
    try:
        resolved.relative_to(root)
    except ValueError as exception:
        raise ProvenanceFailure(f"{label} locator escapes its canonical bundle root") from exception
    return resolved


def _validate_recording_manifest_bundle(
    envelope: dict[str, Any],
    candidate_manifest: dict[str, Any],
) -> dict[str, Any]:
    manifest = envelope["value"]
    if manifest["candidateManifestSha256"] != candidate_manifest["candidateManifestSha256"]:
        raise ProvenanceFailure("recording manifest candidate binding mismatch")
    root = Path(manifest["canonicalBundleRoot"])
    try:
        resolved_root = root.resolve(strict=True)
    except OSError as exception:
        raise ProvenanceFailure("recording manifest canonical bundle root is unreadable") from exception
    if root != resolved_root or not root.is_dir():
        raise ProvenanceFailure(
            "recording manifest bundle root must be a canonical non-symlink directory"
        )
    projections: list[dict[str, Any]] = []
    artifacts_by_key: dict[tuple[str, str], dict[str, Any]] = {}
    artifact_raw_by_key: dict[tuple[str, str], bytes] = {}
    artifact_ids: set[str] = set()
    locators: set[str] = set()
    for index, artifact in enumerate(manifest["artifacts"]):
        label = f"recording manifest artifact[{index}]"
        if artifact["artifactId"] in artifact_ids or artifact["locator"] in locators:
            raise ProvenanceFailure("recording manifest artifact IDs/locators must be unique")
        artifact_ids.add(artifact["artifactId"])
        locators.add(artifact["locator"])
        allowed_mimes = RECORDING_MIME_BY_KIND.get(artifact["kind"], set())
        if artifact["mimeType"] not in allowed_mimes:
            raise ProvenanceFailure(f"{label} kind/MIME combination is invalid")
        source_path = _resolve_recording_artifact(
            root,
            artifact["locator"],
            label=label,
        )
        raw = source_path.read_bytes()
        if (
            len(raw) != artifact["byteLength"]
            or bytes_sha256(raw) != artifact["rawSha256"]
        ):
            raise ProvenanceFailure(f"{label} byte length/raw hash mismatch")
        if not _recording_bytes_match_mime(raw, artifact["mimeType"]):
            raise ProvenanceFailure(f"{label} declared MIME does not match magic/UTF-8 bytes")
        projections.append({
            "locator": artifact["locator"],
            "rawSha256": artifact["rawSha256"],
            "byteLength": artifact["byteLength"],
        })
        evidence_row = {
            "artifactId": artifact["artifactId"],
            "kind": artifact["kind"],
            "sha256": artifact["rawSha256"],
            "mimeType": artifact["mimeType"],
            "locator": artifact["locator"],
        }
        artifacts_by_key[(artifact["artifactId"], artifact["locator"])] = evidence_row
        artifact_raw_by_key[(artifact["artifactId"], artifact["locator"])] = raw
    expected_root_sha = canonical_sha256(
        sorted(projections, key=lambda row: row["locator"])
    )
    if manifest["bundleRootSha256"] != expected_root_sha:
        raise ProvenanceFailure("recording manifest bundle root hash is not byte-derived")
    return {
        **envelope,
        "bundleRoot": root,
        "artifactsByKey": artifacts_by_key,
        "artifactRawByKey": artifact_raw_by_key,
    }


def validate_actor_action_ledger_authority(
    action_ledger: Any,
    candidate_manifest: dict[str, Any],
    actor: dict[str, Any],
    process_tree_id: str,
) -> None:
    validate_actor_action_ledger_schema(action_ledger)
    actions = actor["value"]["actionLedger"]
    checkpoint_post_states = []
    for checkpoint in actor["value"]["checkpoints"]:
        action_index = checkpoint["appActiveActionIndex"]
        action_post_state = (
            None
            if action_index == 0
            else actions[action_index - 1]["postStateSha256"]
        )
        checkpoint_post_states.append({
            "checkpointOrdinal": checkpoint["ordinal"],
            "recipeCheckpointSequenceOrdinal": checkpoint[
                "recipeCheckpointSequenceOrdinal"
            ],
            "appActiveActionIndex": action_index,
            "progressStateSha256": checkpoint["progressStateSha256"],
            "actionPostStateSha256": action_post_state,
        })
    expected_action_ledger = {
        "schemaVersion": ACTOR_ACTION_LEDGER_SCHEMA,
        "protocol": PROTOCOL,
        "candidateManifestSha256": candidate_manifest[
            "candidateManifestSha256"
        ],
        "coldActorResponseSha256": actor["coldActorResponse"]["selfSha256"],
        "actorRunId": actor["value"]["actorRunId"],
        "processTreeId": process_tree_id,
        "actionCount": len(actions),
        "checkpointCount": len(checkpoint_post_states),
        "actions": actions,
        "checkpointPostStates": checkpoint_post_states,
        "projectionRule": (
            "ACTIONS_EXACT_OBSERVATION_ACTION_LEDGER_AND_CHECKPOINT_"
            "PROGRESS_EQUALS_INDEXED_POST_STATE"
        ),
    }
    if action_ledger != expected_action_ledger:
        raise ProvenanceFailure(
            "actor recording ACTION_LEDGER is not the exact observation/action/"
            "checkpoint semantic projection"
        )


def validate_recording_manifest_authorities(
    envelopes: list[dict[str, Any]],
    candidate_manifest: dict[str, Any],
    evaluation_run: dict[str, Any],
    actor_rows: list[dict[str, Any]],
    actor_traces: list[dict[str, Any]],
    coverage_trace: dict[str, Any],
    coverage_action_ledger: dict[str, Any],
) -> dict[str, Any]:
    if len(envelopes) != 4:
        raise ProvenanceFailure("exactly four recording manifests are required")
    validated = [
        _validate_recording_manifest_bundle(row, candidate_manifest)
        for row in envelopes
    ]
    if len({row["selfSha256"] for row in validated}) != 4 or len(
        {row["rawSha256"] for row in validated}
    ) != 4:
        raise ProvenanceFailure("recording manifests require four distinct raw/self hashes")
    if (
        len({row["value"]["canonicalBundleRoot"] for row in validated}) != 4
        or len({row["value"]["bundleRootSha256"] for row in validated}) != 4
        or len({row["value"]["bundleId"] for row in validated}) != 4
    ):
        raise ProvenanceFailure(
            "actor/coverage recordings require four distinct capture roots and bundle hashes"
        )
    artifacts = evaluation_run["artifacts"]
    actor_by_raw = {row["rawSha256"]: row for row in actor_rows}
    actor_trace_by_slot = {row["slot"]: row for row in actor_traces}
    actor_manifests = [
        row for row in validated
        if row["value"]["sourceArtifactKind"] == "ACTOR_OBSERVATION"
    ]
    coverage_manifests = [
        row for row in validated
        if row["value"]["sourceArtifactKind"] == "COVERAGE_CAPTURE"
    ]
    if len(actor_manifests) != 3 or len(coverage_manifests) != 1:
        raise ProvenanceFailure("recording manifests must cover three actors and one coverage run")
    actor_by_slot: dict[int, dict[str, Any]] = {}
    for row in actor_manifests:
        manifest = row["value"]
        if manifest.get("sourceArtifactHashKind") != "ACTOR_OBSERVATION_RAW":
            raise ProvenanceFailure("actor recording source hash kind mismatch")
        source_raw = manifest["sourceArtifactSha256"]
        actor = actor_by_raw.get(source_raw)
        if actor is None:
            raise ProvenanceFailure("actor recording source is not an opened actor observation")
        slot = artifacts["actorObservationRawSha256"].index(source_raw)
        if (
            manifest["actorCaptureSlot"] != slot
            or manifest["actorRunId"] != actor["value"]["actorRunId"]
            or manifest["coverageRunId"] is not None
            or manifest["processTreeId"]
            != actor_trace_by_slot[slot]["value"]["processTreeId"]
            or
            artifacts["recordingManifestSha256"][slot] != row["selfSha256"]
            or artifacts["recordingManifestRawSha256"][slot] != row["rawSha256"]
        ):
            raise ProvenanceFailure("actor recording evaluation raw/self binding mismatch")
        action_ledgers = [
            artifact
            for artifact in manifest["artifacts"]
            if artifact["kind"] == "ACTION_LEDGER"
        ]
        if len(action_ledgers) != 1:
            raise ProvenanceFailure(
                "actor recording must contain exactly one ACTION_LEDGER artifact"
            )
        action_ledger_row = action_ledgers[0]
        action_ledger_raw = row["artifactRawByKey"][
            (action_ledger_row["artifactId"], action_ledger_row["locator"])
        ]
        action_ledger = parse_strict_json_bytes(
            action_ledger_raw,
            "actor recording ACTION_LEDGER",
        )
        validate_actor_action_ledger_authority(
            action_ledger,
            candidate_manifest,
            actor,
            manifest["processTreeId"],
        )
        if (
            manifest["actionLedgerSchemaSha256"]
            != file_sha256(
                ACTOR_ACTION_LEDGER_SCHEMA_PATH,
                "checked-in actor action ledger schema",
            )
            or manifest["actionLedgerArtifactRawSha256"]
            != action_ledger_row["rawSha256"]
        ):
            raise ProvenanceFailure(
                "actor recording ACTION_LEDGER schema/raw authority mismatch"
            )
        actor_by_slot[slot] = row
    if set(actor_by_slot) != {0, 1, 2}:
        raise ProvenanceFailure("actor recordings do not exactly cover capture slots 0,1,2")
    coverage = coverage_manifests[0]
    coverage_value = coverage["value"]
    action_ledgers = [
        row for row in coverage_value["artifacts"] if row["kind"] == "ACTION_LEDGER"
    ]
    if (
        coverage_value.get("sourceArtifactHashKind") != "COVERAGE_ACTION_LEDGER_RAW"
        or coverage_value["actorCaptureSlot"] is not None
        or coverage_value["actorRunId"] is not None
        or coverage_value["coverageRunId"] != coverage_trace["value"]["coverageRunId"]
        or coverage_value["processTreeId"] != coverage_trace["value"]["processTreeId"]
        or len(action_ledgers) != 1
        or coverage_value["sourceArtifactSha256"] != action_ledgers[0]["rawSha256"]
        or coverage_value["sourceArtifactSha256"]
        != coverage_trace["value"]["coverageActionLedgerRawSha256"]
        or coverage_value["sourceArtifactSha256"]
        != artifacts["coverageActionLedgerRawSha256"]
        or coverage_value["actionLedgerSchemaSha256"]
        != file_sha256(
            COVERAGE_ACTION_LEDGER_SCHEMA_PATH,
            "checked-in coverage action ledger schema",
        )
        or coverage_value["actionLedgerArtifactRawSha256"]
        != action_ledgers[0]["rawSha256"]
        or artifacts["coverageRecordingManifestSha256"] != coverage["selfSha256"]
        or artifacts["coverageRecordingManifestRawSha256"] != coverage["rawSha256"]
    ):
        raise ProvenanceFailure(
            "coverage recording action-ledger source/evaluation raw/self binding mismatch"
        )
    audio_sync = _validate_audio_sync_ledger(
        coverage,
        coverage_action_ledger,
    )
    return {
        "actorBySlot": actor_by_slot,
        "coverage": coverage,
        "audioSync": audio_sync,
    }


def derive_lane_execution_identities(
    actor_traces: list[dict[str, Any]],
    coverage_action_ledger: dict[str, Any],
    recordings: dict[str, Any],
) -> dict[str, Any]:
    trace_by_slot = {row["slot"]: row for row in actor_traces}
    recording_by_slot = recordings["actorBySlot"]
    if set(trace_by_slot) != {0, 1, 2} or set(recording_by_slot) != {0, 1, 2}:
        raise ProvenanceFailure("lane execution identity requires exact actor slots 0,1,2")
    cold: list[dict[str, Any]] = []
    for slot in range(3):
        trace = trace_by_slot[slot]["value"]
        recording = recording_by_slot[slot]
        manifest = recording["value"]
        cold.append({
            "actorCaptureSlot": slot,
            "actorRunId": trace["observation"]["actorRunId"],
            "processTreeId": trace["processTreeId"],
            "userDataSha256": trace["userDataSha256"],
            "saveSha256": trace["saveSha256"],
            "journalSha256": trace["journalSha256"],
            "recordingManifestSha256": recording["selfSha256"],
            "recordingManifestRawSha256": recording["rawSha256"],
            "recordingContentRootSha256": manifest["bundleRootSha256"],
            "canonicalRecordingRoot": manifest["canonicalBundleRoot"],
        })
    coverage = coverage_action_ledger["value"]
    coverage_recording = recordings["coverage"]
    coverage_manifest = coverage_recording["value"]
    identities = {
        "cold": cold,
        "coverage": {
            "coverageRunId": coverage["coverageRunId"],
            "processTreeId": coverage["processTreeId"],
            "userDataSha256": coverage["userDataSha256"],
            "journalBundleSha256": coverage["journalBundleSha256"],
            "recordingManifestSha256": coverage_recording["selfSha256"],
            "recordingManifestRawSha256": coverage_recording["rawSha256"],
            "recordingContentRootSha256": coverage_manifest["bundleRootSha256"],
            "canonicalRecordingRoot": coverage_manifest["canonicalBundleRoot"],
        },
    }
    uniqueness_fields = (
        "actorRunId",
        "processTreeId",
        "userDataSha256",
        "journalSha256",
        "recordingManifestSha256",
        "recordingManifestRawSha256",
        "recordingContentRootSha256",
        "canonicalRecordingRoot",
    )
    for field in uniqueness_fields:
        if len({row[field] for row in cold}) != 3:
            raise ProvenanceFailure(
                f"cold lane execution identity field {field} must be distinct per actor"
            )
    return identities


def validate_actor_trace_authorities(
    inputs: list[tuple[Any, bytes]],
    actor_rows: list[dict[str, Any]],
    candidate_manifest: dict[str, Any],
    evaluation_run: dict[str, Any],
    holdout_receipt: dict[str, Any],
    gold_binding: dict[str, Any],
) -> list[dict[str, Any]]:
    if len(inputs) != 3:
        raise ProvenanceFailure("exactly three actor traces are required")
    artifacts = evaluation_run["artifacts"]
    expected_self = artifacts.get("actorTraceSha256")
    expected_raw = artifacts.get("actorTraceRawSha256")
    if (
        not isinstance(expected_self, list)
        or not isinstance(expected_raw, list)
        or len(expected_self) != 3
        or len(expected_raw) != 3
        or len(set(expected_self)) != 3
        or len(set(expected_raw)) != 3
    ):
        raise ProvenanceFailure(
            "evaluation run must bind three distinct actor trace raw/self hashes"
        )
    actor_by_artifact = {row["actorArtifactId"]: row for row in actor_rows}
    if set(actor_by_artifact) != set(artifacts["actorArtifactIds"]):
        raise ProvenanceFailure(
            "actor observations and evaluation actor artifact IDs do not align"
        )
    manifest_contract = candidate_manifest["contractHashes"]
    manifest_recipes = candidate_manifest["recipes"]
    expected_contract = {
        "promptTemplateSha256": manifest_contract["coldActorPromptSha256"],
        "actorObservationSchemaSha256": manifest_contract["actorObservationSchemaSha256"],
        "actorTraceSchemaSha256": manifest_contract["actorTraceSchemaSha256"],
        "conceptManifestSha256": manifest_recipes["conceptExposureSha256"],
        "recipeId": "COLD-JOURNEY-v1",
        "recipeSha256": manifest_recipes["coldJourneySha256"],
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "holdoutConsumptionReceiptSha256": holdout_receipt["selfSha256"],
        "goldBindingManifestSha256": gold_binding["selfSha256"],
        "executionArtifactSha256": candidate_manifest["execution"]["executionArtifactSha256"],
    }
    observed_self: set[str] = set()
    observed_raw: set[str] = set()
    results: list[dict[str, Any]] = []
    for input_index, (value, raw_bytes) in enumerate(inputs):
        label = f"actor trace {input_index + 1}"
        wrapped = validate_self_hashed_envelope(
            value,
            raw_bytes,
            schema_version=ACTOR_TRACE_SCHEMA,
            self_field="actorTraceSha256",
            schema_validator=validate_actor_trace_schema,
            label=label,
        )
        trace = wrapped["value"]
        trace_self = wrapped["selfSha256"]
        trace_raw = wrapped["rawSha256"]
        if trace_self in observed_self or trace_raw in observed_raw:
            raise ProvenanceFailure("actor traces contain duplicate raw/self hashes")
        observed_self.add(trace_self)
        observed_raw.add(trace_raw)
        try:
            slot = expected_self.index(trace_self)
        except ValueError as exception:
            raise ProvenanceFailure(f"{label} self hash is absent from evaluation run") from exception
        if expected_raw[slot] != trace_raw:
            raise ProvenanceFailure(f"{label} raw hash does not match evaluation run")
        actor_artifact_id = artifacts["actorArtifactIds"][slot]
        if trace["actorArtifactId"] != actor_artifact_id:
            raise ProvenanceFailure(f"{label} actorArtifactId evaluation binding mismatch")
        if trace["actorCaptureSlot"] != slot:
            raise ProvenanceFailure(f"{label} actorCaptureSlot must match stable spawn ordinal")
        actor = actor_by_artifact[actor_artifact_id]
        response = actor["coldActorResponse"]
        if (
            trace["coldActorResponseSha256"] != response["selfSha256"]
            or trace["coldActorResponseRawSha256"] != response["rawSha256"]
        ):
            raise ProvenanceFailure(f"{label} cold actor response binding mismatch")
        if trace["actorObservationSha256"] != actor["rawSha256"]:
            raise ProvenanceFailure(f"{label} actor observation raw hash mismatch")
        if trace["observation"] != actor["value"]:
            raise ProvenanceFailure(f"{label} embedded actor observation content mismatch")
        for field, expected_value in expected_contract.items():
            if trace[field] != expected_value:
                raise ProvenanceFailure(f"{label} {field} mismatch")
        per_slot = {
            "userDataSha256": artifacts["userDataSha256"][slot],
            "saveSha256": artifacts["saveSha256"][slot],
            "journalSha256": artifacts["journalSha256"][slot],
            "recordingManifestSha256": artifacts["recordingManifestSha256"][slot],
            "recordingManifestRawSha256": artifacts["recordingManifestRawSha256"][slot],
        }
        for field, expected_value in per_slot.items():
            if trace[field] != expected_value:
                raise ProvenanceFailure(f"{label} {field} evaluation binding mismatch")
        results.append({**wrapped, "slot": slot, "actor": actor})
    if observed_self != set(expected_self) or observed_raw != set(expected_raw):
        raise ProvenanceFailure("actor trace files do not exactly cover evaluation run bindings")
    results.sort(key=lambda row: row["slot"])
    return results


def validate_required_cold_probes(
    actor_rows: list[dict[str, Any]],
    candidate: dict[str, Any],
) -> None:
    concept, _ = read_json_bytes(CONCEPT_MANIFEST_PATH, "checked-in concept manifest")
    checkpoint_sequence = _cold_checkpoint_sequence_authority()
    probes = concept.get("probes") if isinstance(concept, dict) else None
    if not isinstance(probes, list):
        raise ProvenanceFailure("checked-in concept probe authority is invalid")
    required_by_id = {
        row.get("id"): row
        for row in probes
        if isinstance(row, dict)
        and isinstance(row.get("id"), str)
        and row.get("requiredForCold") is True
    }
    expected_ids = concept.get("coldProbeOrder")
    if (
        not isinstance(expected_ids, list)
        or not expected_ids
        or not all(isinstance(row, str) for row in expected_ids)
        or len(set(expected_ids)) != len(expected_ids)
        or set(expected_ids) != set(required_by_id)
    ):
        raise ProvenanceFailure(
            "checked-in coldProbeOrder is not the exact required cold probe set"
        )
    required = [required_by_id[probe_id] for probe_id in expected_ids]

    missing_by_stalled_actor: list[tuple[str | None, list[dict[str, Any]]]] = []
    for actor in actor_rows:
        observation = actor["value"]
        records = observation["firstUseRecords"]
        actual_ids = [row["probeId"] for row in records]
        if len(set(actual_ids)) != len(actual_ids):
            raise ProvenanceFailure("actor required cold probe IDs must be unique")
        if actual_ids != expected_ids[: len(actual_ids)]:
            raise ProvenanceFailure(
                "actor required cold probes must use the exact manifest IDs/order"
            )
        first_use_checkpoint_ordinals = [
            row["checkpointOrdinal"] for row in records
        ]
        if first_use_checkpoint_ordinals != sorted(first_use_checkpoint_ordinals):
            raise ProvenanceFailure(
                "actor coldProbeOrder records must follow checkpoint chronology"
            )
        for index, record in enumerate(records):
            authority = required[index]
            if (
                record["episode"] != authority.get("firstEpisode")
                or record["checkpoint"] != authority.get("firstCheckpoint")
            ):
                raise ProvenanceFailure(
                    f"actor required probe {record['probeId']} manifest checkpoint mismatch"
                )
        state = observation["terminalState"]
        if state == "COMPLETED" and len(records) != len(required):
            raise ProvenanceFailure(
                "COMPLETED actor is missing a required cold concept probe"
            )
        missing = required[len(records):]
        observed_checkpoints = {
            (row["episode"], row["checkpoint"])
            for row in observation["checkpoints"]
        }
        if any(
            (row.get("firstEpisode"), row.get("firstCheckpoint")) in observed_checkpoints
            for row in missing
        ):
            raise ProvenanceFailure(
                "actor omitted a required probe at an already reached manifest checkpoint"
            )
        if missing:
            next_missing_checkpoint = (
                missing[0].get("firstEpisode"),
                missing[0].get("firstCheckpoint"),
            )
            next_missing_ordinal = checkpoint_sequence.get(next_missing_checkpoint)
            reached_ordinals = [
                row["recipeCheckpointSequenceOrdinal"]
                for row in observation["checkpoints"]
            ]
            if (
                next_missing_ordinal is None
                or any(
                    reached_ordinal >= next_missing_ordinal
                    for reached_ordinal in reached_ordinals
                )
            ):
                raise ProvenanceFailure(
                    "actor advanced to or beyond the first omitted cold probe checkpoint"
                )
        if state == "PLAYER_STALLED":
            missing_by_stalled_actor.append((observation["terminalIncidentKey"], missing))

    expected_not_reached: set[str] = set()
    if len(missing_by_stalled_actor) >= 2:
        terminal_keys = {row[0] for row in missing_by_stalled_actor}
        missing_id_lists = {
            tuple(probe["id"] for probe in row[1])
            for row in missing_by_stalled_actor
        }
        if len(terminal_keys) == 1 and None not in terminal_keys and len(missing_id_lists) == 1:
            for probe in missing_by_stalled_actor[0][1]:
                expected_not_reached.update(
                    cell for cell in probe.get("cells", []) if cell in COLD_CELLS
                )
    if set(candidate["notReachedByProductCellIds"]) != expected_not_reached:
        raise ProvenanceFailure(
            "NOT_REACHED_BY_PRODUCT cells do not exactly match manifest probes after the "
            "shared product terminal"
        )


def validate_coverage_trace_authority(
    value: Any,
    raw_bytes: bytes,
    candidate_manifest: dict[str, Any],
    evaluation_run: dict[str, Any],
    holdout_receipt: dict[str, Any],
    gold_binding: dict[str, Any],
    coverage_action_ledger: dict[str, Any],
) -> dict[str, Any]:
    wrapped = validate_self_hashed_envelope(
        value,
        raw_bytes,
        schema_version=COVERAGE_TRACE_SCHEMA,
        self_field="coverageArtifactId",
        schema_validator=validate_coverage_trace_schema,
        label="coverage trace",
    )
    trace = wrapped["value"]
    artifacts = evaluation_run["artifacts"]
    recipes = candidate_manifest["recipes"]
    expected = {
        "coverageArtifactId": artifacts["coverageArtifactId"],
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "executionArtifactSha256": candidate_manifest["execution"]["executionArtifactSha256"],
        "coverageRecipeSha256": recipes["coverageSha256"],
        "holdoutQueueSha256": recipes["holdoutQueueSha256"],
        "selectedRecipeId": recipes["selectedRecipeId"],
        "selectedRecipeSha256": recipes["selectedRecipeSha256"],
        "holdoutConsumptionReceiptSha256": holdout_receipt["selfSha256"],
        "conceptManifestSha256": recipes["conceptExposureSha256"],
        "goldBindingManifestSha256": gold_binding["selfSha256"],
        "coverageActionLedgerRawSha256": artifacts["coverageActionLedgerRawSha256"],
        "coverageActionLedgerSha256": coverage_action_ledger["selfSha256"],
        "coverageActionLedgerSchemaSha256": file_sha256(
            COVERAGE_ACTION_LEDGER_SCHEMA_PATH,
            "checked-in coverage action ledger schema",
        ),
        "recordingManifestSha256": artifacts["coverageRecordingManifestSha256"],
        "recordingManifestRawSha256": artifacts[
            "coverageRecordingManifestRawSha256"
        ],
    }
    for field, expected_value in expected.items():
        if trace[field] != expected_value:
            raise ProvenanceFailure(f"coverage trace {field} mismatch")
    if wrapped["rawSha256"] != artifacts["coverageTraceRawSha256"]:
        raise ProvenanceFailure("coverage trace raw hash mismatch")
    recipe_value, _ = read_json_bytes(
        NATIVE_DIRECTORY / "coverage-recipe.json",
        "checked-in coverage recipe",
    )
    recipe_episodes = recipe_value.get("episodes") if isinstance(recipe_value, dict) else None
    if not isinstance(recipe_episodes, list):
        raise ProvenanceFailure("checked-in coverage recipe episodes are invalid")
    expected_by_episode = {
        row.get("id"): row.get("actions")
        for row in recipe_episodes
        if isinstance(row, dict)
    }
    expected_execution_episode_ids = [
        row.get("id") for row in recipe_episodes if isinstance(row, dict)
    ]
    if [row["episodeId"] for row in trace["episodes"]] != expected_execution_episode_ids:
        raise ProvenanceFailure(
            "coverage trace execution episodes must remain in canonical E00..E11 order"
        )
    selected = _selected_recipe(candidate_manifest)
    expected_realization, expected_evidence_realization = _selected_holdout_realization(
        selected,
        wrapped,
    )
    if trace["holdoutRealization"] != expected_realization:
        raise ProvenanceFailure("coverage trace selected holdout realization mismatch")
    if (
        trace["coveragePresentationEpisodeIds"]
        != expected_evidence_realization["coveragePresentationEpisodeIds"]
    ):
        raise ProvenanceFailure("coverage trace selected presentation order mismatch")
    all_occurrence_ids: list[str] = []
    for episode in trace["episodes"]:
        actual = [row["actionOccurrenceId"] for row in episode["traceRows"]]
        expected_actions = expected_by_episode.get(episode["episodeId"])
        if not isinstance(expected_actions, list) or not all(
            isinstance(row, str) for row in expected_actions
        ):
            raise ProvenanceFailure(
                f"checked-in coverage recipe {episode['episodeId']} actions are invalid"
            )
        realized_actions = _realized_coverage_actions(
            episode["episodeId"],
            expected_actions,
            selected,
        )
        if actual != realized_actions:
            raise ProvenanceFailure(
                f"coverage trace {episode['episodeId']} holdout branch/action order mismatch"
            )
        action_indices = [row["actionIndex"] for row in episode["traceRows"]]
        if action_indices != list(range(1, len(action_indices) + 1)):
            raise ProvenanceFailure(
                f"coverage trace {episode['episodeId']} actionIndex must be exact realized order"
            )
        for row in episode["traceRows"]:
            realization = _expected_action_realization(
                episode["episodeId"],
                row["actionOccurrenceId"],
                selected,
            )
            if any(row[field] != expected for field, expected in realization.items()):
                raise ProvenanceFailure(
                    f"coverage trace {episode['episodeId']}/{row['actionOccurrenceId']} "
                    "prototype/branch realization mismatch"
                )
        all_occurrence_ids.extend(actual)
    if len(set(all_occurrence_ids)) != len(all_occurrence_ids):
        raise ProvenanceFailure("coverage trace actionOccurrenceId values must be globally unique")
    return wrapped


def validate_coverage_action_ledger_authority(
    envelope: dict[str, Any],
    candidate_manifest: dict[str, Any],
    evaluation_run: dict[str, Any],
    holdout_receipt: dict[str, Any],
    gold_binding: dict[str, Any],
    coverage_trace: dict[str, Any],
) -> dict[str, Any]:
    ledger = envelope["value"]
    trace = coverage_trace["value"]
    recipes = candidate_manifest["recipes"]
    expected = {
        "coverageActionLedgerSchemaSha256": file_sha256(
            COVERAGE_ACTION_LEDGER_SCHEMA_PATH,
            "checked-in coverage action ledger schema",
        ),
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "executionArtifactSha256": candidate_manifest["execution"][
            "executionArtifactSha256"
        ],
        "coverageRecipeSha256": recipes["coverageSha256"],
        "holdoutQueueSha256": recipes["holdoutQueueSha256"],
        "selectedRecipeId": recipes["selectedRecipeId"],
        "selectedRecipeSha256": recipes["selectedRecipeSha256"],
        "holdoutConsumptionReceiptSha256": holdout_receipt["selfSha256"],
        "goldBindingManifestSha256": gold_binding["selfSha256"],
        "holdoutRealization": trace["holdoutRealization"],
        "coverageRunId": trace["coverageRunId"],
        "processTreeId": trace["processTreeId"],
        "userDataSha256": trace["userDataSha256"],
        "journalBundleSha256": trace["journalBundleSha256"],
    }
    for field, expected_value in expected.items():
        if ledger[field] != expected_value:
            raise ProvenanceFailure(f"coverage action ledger {field} mismatch")
    artifacts = evaluation_run["artifacts"]
    if (
        artifacts["coverageActionLedgerSha256"] != envelope["selfSha256"]
        or artifacts["coverageActionLedgerRawSha256"] != envelope["rawSha256"]
        or trace["coverageActionLedgerSha256"] != envelope["selfSha256"]
        or trace["coverageActionLedgerRawSha256"] != envelope["rawSha256"]
    ):
        raise ProvenanceFailure("coverage action ledger raw/self trace/evaluation mismatch")
    if len(ledger["episodes"]) != len(trace["episodes"]):
        raise ProvenanceFailure("coverage action ledger episode coverage mismatch")
    for ledger_episode, trace_episode in zip(ledger["episodes"], trace["episodes"]):
        derived_episode_sha = self_sha256(
            ledger_episode,
            "episodeLedgerSha256",
            f"coverage action ledger {ledger_episode['episodeId']}",
        )
        expected_episode = {
            "episodeId": trace_episode["episodeId"],
            "prefixId": trace_episode["prefixId"],
            "checkpointIds": trace_episode["checkpointIds"],
            "episodeLedgerSha256": derived_episode_sha,
            "actions": trace_episode["traceRows"],
        }
        if ledger_episode != expected_episode:
            raise ProvenanceFailure(
                "coverage action ledger episode/action semantic projection mismatch"
            )
        if trace_episode["actionLedgerSha256"] != derived_episode_sha:
            raise ProvenanceFailure(
                "coverage trace episode actionLedgerSha256 is not ledger-derived"
            )
    return envelope


def _coverage_presentation_episode_ids(
    selected_recipe: dict[str, Any],
    coverage_trace: dict[str, Any],
) -> list[str]:
    episode_ids = [row["episodeId"] for row in coverage_trace["value"]["episodes"]]
    canonical_episode_ids = _coverage_recipe_episode_ids()
    if episode_ids != canonical_episode_ids:
        raise ProvenanceFailure(
            "coverage trace execution episode order is not the canonical recipe order"
        )
    return _selected_coverage_presentation_episode_ids(selected_recipe)


def validate_anonymization_authority(
    value: Any,
    raw_bytes: bytes,
    candidate: dict[str, Any],
    candidate_manifest: dict[str, Any],
    holdout_receipt: dict[str, Any],
    actor_traces: list[dict[str, Any]],
    coverage_trace: dict[str, Any],
) -> dict[str, Any]:
    wrapped = validate_self_hashed_envelope(
        value,
        raw_bytes,
        schema_version=ANONYMIZATION_MANIFEST_SCHEMA,
        self_field="anonymizationManifestSha256",
        schema_validator=validate_anonymization_manifest_schema,
        label="anonymization manifest",
    )
    manifest = wrapped["value"]
    selected = _selected_recipe(candidate_manifest)
    permutation = selected["actorArtifactPermutation"]
    expected = {
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "holdoutConsumptionReceiptSha256": holdout_receipt["selfSha256"],
        "selectedRecipeSha256": candidate_manifest["recipes"]["selectedRecipeSha256"],
        "actorArtifactPermutation": permutation,
        "artifactOrder": [row["anonymousArtifactId"] for row in candidate["artifactBindings"]],
    }
    for field, expected_value in expected.items():
        if manifest[field] != expected_value:
            raise ProvenanceFailure(f"anonymization manifest {field} mismatch")
    trace_by_slot = {row["slot"]: row for row in actor_traces}
    expected_actors = []
    for anonymous_index, capture_slot in enumerate(permutation):
        anonymous_id = f"ARTIFACT-{chr(ord('A') + anonymous_index)}"
        trace = trace_by_slot[capture_slot]
        expected_actors.append({
            "actorCaptureSlot": capture_slot,
            "actorTraceSha256": trace["selfSha256"],
            "actorTraceRawSha256": trace["rawSha256"],
            "recordingManifestSha256": trace["value"]["recordingManifestSha256"],
            "recordingManifestRawSha256": trace["value"][
                "recordingManifestRawSha256"
            ],
            "anonymousArtifactId": anonymous_id,
        })
    if manifest["sourceActors"] != expected_actors:
        raise ProvenanceFailure(
            "anonymization actor mapping does not match selected actor permutation"
        )
    expected_coverage = {
        "coverageArtifactId": coverage_trace["selfSha256"],
        "coverageTraceRawSha256": coverage_trace["rawSha256"],
        "recordingManifestSha256": coverage_trace["value"][
            "recordingManifestSha256"
        ],
        "recordingManifestRawSha256": coverage_trace["value"][
            "recordingManifestRawSha256"
        ],
        "anonymousArtifactId": "ARTIFACT-D",
        "coveragePresentationEpisodeIds": _coverage_presentation_episode_ids(
            selected, coverage_trace
        ),
    }
    if manifest["sourceCoverage"] != expected_coverage:
        raise ProvenanceFailure("anonymization coverage mapping/order mismatch")
    return wrapped


def _selected_holdout_realization(
    selected: dict[str, Any],
    coverage_trace: dict[str, Any],
) -> tuple[dict[str, Any], dict[str, Any]]:
    core = {
        "missionPrototypeBits": selected["missionPrototypeBits"],
        "promiseBranchOrder": selected["promiseBranchOrder"],
        "actorArtifactPermutation": selected["actorArtifactPermutation"],
        "coverageArtifactOrder": selected["coverageArtifactOrder"],
    }
    evidence = {
        **core,
        "coveragePresentationEpisodeIds": _coverage_presentation_episode_ids(
            selected, coverage_trace
        ),
    }
    return core, evidence


def _validate_cold_evidence_derivation(
    artifact: dict[str, Any],
    actor_trace: dict[str, Any],
    recording_manifest: dict[str, Any],
) -> None:
    observation = actor_trace["value"]["observation"]
    checkpoints = {row["ordinal"]: row for row in observation["checkpoints"]}
    actions = {row["actionIndex"]: row for row in observation["actionLedger"]}
    represented_checkpoints: set[int] = set()
    represented_first_use: set[int] = set()
    represented_approvals: set[int] = set()
    first_use = observation["firstUseRecords"]
    approvals = observation["approvalRecords"]
    for trace_index, trace in enumerate(artifact["traceRows"]):
        matches = [
            (ordinal, row)
            for ordinal, row in checkpoints.items()
            if (row["episode"], row["checkpoint"])
            == (trace["episode"], trace["checkpoint"])
        ]
        if len(matches) != 1:
            raise ProvenanceFailure("cold evidence trace has no unique actor checkpoint")
        ordinal, checkpoint = matches[0]
        represented_checkpoints.add(ordinal)
        if (
            trace["checkpointBranchId"] != "SHARED"
            or trace["semanticActionKind"] is not None
            or trace["actionOccurrenceId"] is not None
            or trace["prototypeSlot"] is not None
            or trace["prototypeKind"] is not None
            or trace["branchSequenceOrdinal"] is not None
            or trace["branchDecision"] is not None
            or trace["appActiveActionIndex"] != checkpoint["appActiveActionIndex"]
            or trace["progressStateSha256"] != checkpoint["progressStateSha256"]
        ):
            raise ProvenanceFailure("cold evidence trace checkpoint/action derivation mismatch")
        action_index = checkpoint["appActiveActionIndex"]
        action = actions.get(action_index)
        expected_action = (
            (None, None, None)
            if action_index == 0
            else (action["inputEvent"], action["visibleFeedback"], action["audibleFeedback"])
        )
        if (
            trace["inputEvent"],
            trace["visibleFeedback"],
            trace["audibleFeedback"],
        ) != expected_action:
            raise ProvenanceFailure("cold evidence action feedback does not match actor observation")
        first_matches = [
            (index, row)
            for index, row in enumerate(first_use)
            if row["checkpointOrdinal"] == ordinal
            and row["currentGoal"] == trace["currentGoal"]
            and row["expectedVisibleConsequence"] == trace["expectedVisibleConsequence"]
        ]
        if trace["currentGoal"] is None or trace["expectedVisibleConsequence"] is None:
            if trace["currentGoal"] is not None or trace["expectedVisibleConsequence"] is not None:
                raise ProvenanceFailure("cold evidence first-use fields must both be null or exact")
        elif len(first_matches) != 1:
            raise ProvenanceFailure("cold evidence first-use content is not observation-derived")
        else:
            represented_first_use.add(first_matches[0][0])
        approval_matches = [
            (index, row)
            for index, row in enumerate(approvals)
            if row["checkpointOrdinal"] == ordinal
            and row["predictionImmediatelyBeforeApproval"]
            == trace["predictionImmediatelyBeforeApproval"]
            and row["observedResult"] == trace["observedResult"]
            and row["causalAccount"] == trace["causalAccount"]
        ]
        approval_fields = (
            trace["predictionImmediatelyBeforeApproval"],
            trace["observedResult"],
            trace["causalAccount"],
        )
        if any(row is None for row in approval_fields):
            if any(row is not None for row in approval_fields):
                raise ProvenanceFailure("cold evidence approval fields must all be null or exact")
        elif len(approval_matches) != 1:
            raise ProvenanceFailure("cold evidence approval content is not observation-derived")
        else:
            represented_approvals.add(approval_matches[0][0])
        expected_sources: list[dict[str, str]] = []
        if first_matches:
            source = first_matches[0][1]["citedVisibleSource"]
            expected_sources.append({
                "artifactId": source["artifactId"],
                "locator": source["locator"],
            })
        if approval_matches:
            for source in approval_matches[0][1]["artifactRefs"]:
                projected = {"artifactId": source["artifactId"], "locator": source["locator"]}
                if projected not in expected_sources:
                    expected_sources.append(projected)
        if trace["citedVisibleSources"] != expected_sources:
            raise ProvenanceFailure("cold evidence cited source content/order mismatch")
        expected_incidents = [
            row["incidentKey"]
            for row in observation["incidents"]
            if ordinal in row["checkpointOrdinals"]
        ]
        if trace["incidentKeys"] != expected_incidents:
            raise ProvenanceFailure("cold evidence incident derivation mismatch")
    if represented_checkpoints != set(checkpoints):
        raise ProvenanceFailure("cold evidence does not exactly cover actor checkpoints")
    if represented_first_use != set(range(len(first_use))):
        raise ProvenanceFailure("cold evidence does not exactly cover actor first-use records")
    if represented_approvals != set(range(len(approvals))):
        raise ProvenanceFailure("cold evidence does not exactly cover actor approval records")
    source_refs: list[dict[str, str]] = []
    for checkpoint in observation["checkpoints"]:
        source_refs.extend(checkpoint["artifactRefs"])
    for record in first_use:
        source_refs.append(record["citedVisibleSource"])
    for record in approvals:
        source_refs.extend(record["artifactRefs"])
    for incident in observation["incidents"]:
        source_refs.extend(incident["artifactRefs"])
    expected_media: list[dict[str, Any]] = []
    seen_media: set[tuple[str, str]] = set()
    for source in source_refs:
        key = (source["artifactId"], source["locator"])
        if key in seen_media:
            continue
        seen_media.add(key)
        recorded = recording_manifest["artifactsByKey"].get(key)
        if recorded is None or recorded["kind"] != source["kind"]:
            raise ProvenanceFailure(
                "actor observation cites media absent from its recording manifest"
            )
        expected_media.append(recorded)
    if artifact["mediaArtifacts"] != expected_media:
        raise ProvenanceFailure(
            "cold evidence media order/content/raw hash/MIME is not recording-derived"
        )


def _validate_coverage_evidence_derivation(
    artifact: dict[str, Any],
    coverage_trace: dict[str, Any],
    presentation_episode_ids: list[str],
    recording_manifest: dict[str, Any],
) -> None:
    episode_by_id = {
        row["episodeId"]: row for row in coverage_trace["value"]["episodes"]
    }
    expected_trace_rows: list[tuple[str, dict[str, Any]]] = []
    expected_media: list[dict[str, Any]] = []
    for episode_id in presentation_episode_ids:
        episode = episode_by_id[episode_id]
        expected_trace_rows.extend((episode_id, row) for row in episode["traceRows"])
        expected_media.extend(episode["mediaArtifacts"])
    if len(artifact["traceRows"]) != len(expected_trace_rows):
        raise ProvenanceFailure("coverage evidence does not exactly cover realized actions")
    for evidence_row, (episode_id, source_row) in zip(
        artifact["traceRows"], expected_trace_rows
    ):
        expected_sources = [
            {"artifactId": row["artifactId"], "locator": row["locator"]}
            for row in source_row["artifactRefs"]
        ]
        expected_fields = {
            "traceRowId": source_row["traceRowId"],
            "episode": episode_id,
            "checkpoint": source_row["checkpoint"],
            "checkpointBranchId": source_row["checkpointBranchId"],
            "semanticActionKind": source_row["semanticActionKind"],
            "actionOccurrenceId": source_row["actionOccurrenceId"],
            "prototypeSlot": source_row["prototypeSlot"],
            "prototypeKind": source_row["prototypeKind"],
            "branchSequenceOrdinal": source_row["branchSequenceOrdinal"],
            "branchDecision": source_row["branchDecision"],
            "appActiveActionIndex": source_row["actionIndex"],
            "citedVisibleSources": expected_sources,
            "visibleFeedback": source_row["visibleFeedback"],
            "audibleFeedback": source_row["audibleFeedback"],
            "progressStateSha256": source_row["postStateSha256"],
        }
        if any(evidence_row[field] != expected for field, expected in expected_fields.items()):
            raise ProvenanceFailure("coverage evidence row is not exact trace-derived content")
    if artifact["mediaArtifacts"] != expected_media:
        raise ProvenanceFailure("coverage evidence media/order does not match coverage trace")
    for media in expected_media:
        recorded = recording_manifest["artifactsByKey"].get(
            (media["artifactId"], media["locator"])
        )
        if recorded != media:
            raise ProvenanceFailure(
                "coverage trace/evidence media raw hash/MIME is not recording-derived"
            )


def _validate_evidence_media_bytes(
    artifact: dict[str, Any],
    recording_manifest: dict[str, Any],
) -> None:
    for media in artifact["mediaArtifacts"]:
        recorded = recording_manifest["artifactsByKey"].get(
            (media["artifactId"], media["locator"])
        )
        if recorded != media:
            raise ProvenanceFailure(
                f"evidence media/action {media['artifactId']} is not an exact recording row"
            )


def validate_evidence_set_authority(
    value: Any,
    raw_bytes: bytes,
    candidate: dict[str, Any],
    candidate_manifest: dict[str, Any],
    evaluation_run: dict[str, Any],
    actor_rows: list[dict[str, Any]],
    actor_traces: list[dict[str, Any]],
    coverage_trace: dict[str, Any],
    holdout_receipt: dict[str, Any],
    gold_binding: dict[str, Any],
    anonymization: dict[str, Any],
    recordings: dict[str, Any],
) -> dict[str, Any]:
    wrapped = validate_self_hashed_envelope(
        value,
        raw_bytes,
        schema_version=EVIDENCE_SET_SCHEMA,
        self_field="evidenceSetSha256",
        schema_validator=validate_evidence_set_schema,
        label="evidence set",
    )
    evidence = wrapped["value"]
    provenance = candidate["provenance"]
    recipes = candidate_manifest["recipes"]
    expected = {
        "rubricSha256": provenance["rubricSha256"],
        "conceptManifestSha256": recipes["conceptExposureSha256"],
        "recipeId": candidate["recipeId"],
        "recipeSha256": recipes["selectedRecipeSha256"],
        "holdoutQueueSha256": recipes["holdoutQueueSha256"],
        "holdoutConsumptionReceiptSha256": holdout_receipt["selfSha256"],
        "goldBindingManifestSha256": gold_binding["selfSha256"],
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "executionArtifactSha256": provenance["executionArtifactSha256"],
        "anonymizationManifestSha256": anonymization["selfSha256"],
    }
    for field, expected_value in expected.items():
        if evidence[field] != expected_value:
            raise ProvenanceFailure(f"evidence set {field} mismatch")
    if evaluation_run["artifacts"]["evidenceSetSha256"] != wrapped["selfSha256"]:
        raise ProvenanceFailure("evaluation run evidenceSetSha256 mismatch")
    binding_ids = [row["anonymousArtifactId"] for row in candidate["artifactBindings"]]
    if evidence["artifactOrder"] != binding_ids:
        raise ProvenanceFailure("evidence set artifact order mismatch")
    selected = _selected_recipe(candidate_manifest)
    trace_realization, evidence_realization = _selected_holdout_realization(
        selected, coverage_trace
    )
    if coverage_trace["value"]["holdoutRealization"] != trace_realization:
        raise ProvenanceFailure("coverage trace holdout realization mismatch")
    if (
        coverage_trace["value"]["coveragePresentationEpisodeIds"]
        != evidence_realization["coveragePresentationEpisodeIds"]
    ):
        raise ProvenanceFailure("coverage trace presentation episode order mismatch")
    if evidence["holdoutRealization"] != evidence_realization:
        raise ProvenanceFailure("evidence set holdout realization/order mismatch")
    by_id = {row["anonymousArtifactId"]: row for row in evidence["artifacts"]}
    terminal_by_source = {row["actorArtifactId"]: row for row in actor_rows}
    trace_by_actor_source = {
        row["value"]["actorArtifactId"]: row for row in actor_traces
    }
    anonymized_actor_by_id = {
        row["anonymousArtifactId"]: row
        for row in anonymization["value"]["sourceActors"]
    }
    recording_by_anonymous_id = {
        row["anonymousArtifactId"]: recordings["actorBySlot"][row["actorCaptureSlot"]]
        for row in anonymization["value"]["sourceActors"]
    }
    recording_by_anonymous_id["ARTIFACT-D"] = recordings["coverage"]
    artifact_authorities: dict[str, dict[str, Any]] = {}
    for binding in candidate["artifactBindings"]:
        artifact = by_id.get(binding["anonymousArtifactId"])
        if artifact is None:
            raise ProvenanceFailure("evidence set is missing a bound anonymous artifact")
        derived_sanitized_sha256 = self_sha256(
            artifact,
            "sanitizedArtifactSha256",
            f"evidence artifact {binding['anonymousArtifactId']}",
        )
        if (
            artifact["artifactKind"] != binding["artifactKind"]
            or artifact["sanitizedArtifactSha256"] != derived_sanitized_sha256
            or binding["artifactSha256"] != derived_sanitized_sha256
        ):
            raise ProvenanceFailure(
                "evidence set sanitized artifact content/self-hash binding mismatch"
            )
        recording = recording_by_anonymous_id.get(binding["anonymousArtifactId"])
        if recording is None:
            raise ProvenanceFailure("evidence artifact has no exact recording authority")
        _validate_evidence_media_bytes(artifact, recording)
        if artifact["artifactKind"] == "COLD_ACTOR":
            terminal = terminal_by_source.get(artifact["sourceArtifactSha256"])
            if terminal is None:
                raise ProvenanceFailure("evidence cold source artifact is not in evaluation run")
            trace = trace_by_actor_source.get(artifact["sourceArtifactSha256"])
            anonymized = anonymized_actor_by_id.get(binding["anonymousArtifactId"])
            if trace is None or anonymized is None:
                raise ProvenanceFailure("evidence cold artifact has no exact actor trace mapping")
            if (
                artifact["actorCaptureSlot"] != trace["slot"]
                or artifact["actorCaptureSlot"] != anonymized["actorCaptureSlot"]
                or artifact["anonymizationManifestSha256"] != anonymization["selfSha256"]
                or artifact["assignedCells"] != list(COLD_CELLS)
                or artifact["episodeIds"]
                != [
                    "E00-TITLE", "E01-FIRST-LIGHT", "E02-SECOND-HEART",
                    "E03-SECOND-SOURCE", "E04-NORTH-BANK", "E05-WHOSE-MARGIN",
                    "E06-FLOOD", "E07-MAINTENANCE", "E08-FINALE", "E09-MID-RESUME",
                ]
            ):
                raise ProvenanceFailure("evidence cold permutation/cells/episode binding mismatch")
            if (
                artifact["terminalState"] != terminal["terminalState"]
                or artifact["terminalIncidentKey"] != terminal["terminalIncidentKey"]
            ):
                raise ProvenanceFailure("evidence cold terminal state mismatch")
            _validate_cold_evidence_derivation(artifact, trace, recording)
            artifact_authorities[binding["anonymousArtifactId"]] = terminal
        else:
            if (
                artifact["sourceArtifactSha256"] != coverage_trace["selfSha256"]
                or artifact["assignedCells"] != list(COVERAGE_CELLS)
                or artifact["episodeIds"]
                != evidence_realization["coveragePresentationEpisodeIds"]
            ):
                raise ProvenanceFailure("evidence coverage source/cells/order mismatch")
            _validate_coverage_evidence_derivation(
                artifact,
                coverage_trace,
                evidence_realization["coveragePresentationEpisodeIds"],
                recording,
            )
    wrapped["artifactsById"] = by_id
    wrapped["actorAuthoritiesByAnonymousId"] = artifact_authorities
    wrapped["bundleRootsById"] = {
        anonymous_id: row["value"]["canonicalBundleRoot"]
        for anonymous_id, row in recording_by_anonymous_id.items()
    }
    return wrapped


def validate_sanitized_evidence_bundle_authority(
    envelope: dict[str, Any],
    candidate: dict[str, Any],
    candidate_manifest: dict[str, Any],
    evaluation_run: dict[str, Any],
    holdout_receipt: dict[str, Any],
    gold_binding: dict[str, Any],
    anonymization: dict[str, Any],
    evidence_authority: dict[str, Any],
    recordings: dict[str, Any],
) -> dict[str, Any]:
    manifest = envelope["value"]
    provenance = candidate["provenance"]
    expected = {
        "sanitizedEvidenceBundleManifestSchemaSha256": file_sha256(
            SANITIZED_EVIDENCE_BUNDLE_MANIFEST_SCHEMA_PATH,
            "checked-in sanitized evidence bundle manifest schema",
        ),
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "holdoutConsumptionReceiptSha256": holdout_receipt["selfSha256"],
        "goldBindingManifestSha256": gold_binding["selfSha256"],
        "anonymizationManifestSha256": anonymization["selfSha256"],
        "evidenceSetSha256": evidence_authority["selfSha256"],
    }
    for field, expected_value in expected.items():
        if manifest[field] != expected_value:
            raise ProvenanceFailure(f"sanitized evidence bundle {field} mismatch")
    if (
        provenance["sanitizedEvidenceBundleManifestSha256"] != envelope["selfSha256"]
        or provenance["sanitizedEvidenceBundleManifestRawSha256"] != envelope["rawSha256"]
    ):
        raise ProvenanceFailure(
            "aggregation sanitized evidence bundle raw/self provenance mismatch"
        )
    source_roots = {
        row["value"]["canonicalBundleRoot"]
        for row in [*recordings["actorBySlot"].values(), recordings["coverage"]]
    }
    bundle_roots: set[str] = set()
    bundles_by_id: dict[str, dict[str, Any]] = {}
    content_projection: list[dict[str, Any]] = []
    expected_order = evidence_authority["value"]["artifactOrder"]
    if [row["anonymousArtifactId"] for row in manifest["bundles"]] != expected_order:
        raise ProvenanceFailure("sanitized evidence bundle A-D order mismatch")
    for bundle in manifest["bundles"]:
        anonymous_id = bundle["anonymousArtifactId"]
        artifact = evidence_authority["artifactsById"][anonymous_id]
        root = Path(bundle["canonicalBundleRoot"])
        try:
            resolved_root = root.resolve(strict=True)
        except OSError as exception:
            raise ProvenanceFailure("sanitized evidence bundle root is unreadable") from exception
        if (
            root != resolved_root
            or not root.is_dir()
            or str(root) in source_roots
            or str(root) in bundle_roots
            or bundle["bundleRootPathTail"] != root.name
        ):
            raise ProvenanceFailure(
                "sanitized evidence bundle root is aliased, reused, or non-canonical"
            )
        bundle_roots.add(str(root))
        actual_files: list[str] = []
        for child in root.rglob("*"):
            if child.is_symlink():
                raise ProvenanceFailure("sanitized evidence bundle contains a symlink")
            if child.is_file():
                actual_files.append(child.relative_to(root).as_posix())
        expected_locators = [row["locator"] for row in bundle["files"]]
        if (
            expected_locators != sorted(expected_locators)
            or sorted(actual_files) != expected_locators
            or bundle["fileCount"] != len(expected_locators)
        ):
            raise ProvenanceFailure(
                "sanitized evidence bundle recursive file set/order is not exact"
            )
        projections: list[dict[str, Any]] = []
        evidence_rows: list[dict[str, Any]] = []
        for index, file_row in enumerate(bundle["files"]):
            path = _resolve_recording_artifact(
                root,
                file_row["locator"],
                label=f"sanitized bundle {anonymous_id} file[{index}]",
            )
            raw = path.read_bytes()
            if (
                len(raw) != file_row["byteLength"]
                or bytes_sha256(raw) != file_row["rawSha256"]
                or file_row["mimeType"]
                not in RECORDING_MIME_BY_KIND.get(file_row["kind"], set())
                or not _recording_bytes_match_mime(raw, file_row["mimeType"])
            ):
                raise ProvenanceFailure(
                    "sanitized evidence bundle file bytes/kind/MIME mismatch"
                )
            projections.append({
                "locator": file_row["locator"],
                "rawSha256": file_row["rawSha256"],
                "byteLength": file_row["byteLength"],
            })
            evidence_rows.append({
                "artifactId": file_row["artifactId"],
                "kind": file_row["kind"],
                "sha256": file_row["rawSha256"],
                "mimeType": file_row["mimeType"],
                "locator": file_row["locator"],
            })
        expected_root_sha = canonical_sha256(projections)
        expected_bundle_sha = self_sha256(
            bundle,
            "artifactBundleSha256",
            f"sanitized bundle {anonymous_id}",
        )
        if (
            bundle["sanitizedArtifactSha256"] != artifact["sanitizedArtifactSha256"]
            or bundle["bundleRootSha256"] != expected_root_sha
            or bundle["artifactBundleSha256"] != expected_bundle_sha
            or evidence_rows != artifact["mediaArtifacts"]
        ):
            raise ProvenanceFailure(
                "sanitized evidence bundle hash/files are not exact evidence-set content"
            )
        content_projection.append({
            "anonymousArtifactId": anonymous_id,
            "bundleId": bundle["bundleId"],
            "bundleRootSha256": bundle["bundleRootSha256"],
            "artifactBundleSha256": bundle["artifactBundleSha256"],
        })
        bundles_by_id[anonymous_id] = bundle
    expected_content_root = canonical_sha256(content_projection)
    if manifest["contentRootSha256"] != expected_content_root:
        raise ProvenanceFailure("sanitized evidence content root is not bundle-derived")
    artifacts = evaluation_run["artifacts"]
    if (
        artifacts["sanitizedEvidenceBundleManifestSha256"] != envelope["selfSha256"]
        or artifacts["sanitizedEvidenceBundleManifestRawSha256"] != envelope["rawSha256"]
        or artifacts["sanitizedEvidenceContentRootSha256"] != expected_content_root
        or provenance["sanitizedEvidenceContentRootSha256"] != expected_content_root
    ):
        raise ProvenanceFailure(
            "sanitized evidence bundle evaluation/content-root binding mismatch"
        )
    return {**envelope, "bundlesById": bundles_by_id}


def validate_candidate_judge_input_authority(
    envelope: dict[str, Any],
    candidate: dict[str, Any],
    candidate_manifest: dict[str, Any],
    qualification: dict[str, Any],
    holdout_receipt: dict[str, Any],
    gold_binding: dict[str, Any],
    evidence_authority: dict[str, Any],
    sanitized_bundle: dict[str, Any],
    evaluation_run: dict[str, Any],
) -> dict[str, Any]:
    value = envelope["value"]
    provenance = candidate["provenance"]
    expected = {
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "candidateManifestRawSha256": provenance["candidateManifestRawSha256"],
        "qualificationReceiptSha256": qualification["selfSha256"],
        "qualificationReceiptRawSha256": qualification["rawSha256"],
        "qualificationStatus": qualification["status"],
        "holdoutConsumptionReceiptSha256": holdout_receipt["selfSha256"],
        "holdoutConsumptionReceiptRawSha256": holdout_receipt["rawSha256"],
        "goldBindingManifestSha256": gold_binding["selfSha256"],
        "goldBindingManifestRawSha256": gold_binding["rawSha256"],
        "evidenceSetSha256": evidence_authority["selfSha256"],
        "evidenceSetRawSha256": evidence_authority["rawSha256"],
        "sanitizedEvidenceBundleManifestSha256": sanitized_bundle["selfSha256"],
        "sanitizedEvidenceBundleManifestRawSha256": sanitized_bundle["rawSha256"],
        "sanitizedEvidenceContentRootSha256": sanitized_bundle["value"][
            "contentRootSha256"
        ],
        "recipeId": candidate["recipeId"],
        "selectedRecipeSha256": candidate_manifest["recipes"][
            "selectedRecipeSha256"
        ],
        "artifactOrder": [
            row["anonymousArtifactId"] for row in candidate["artifactBindings"]
        ],
        "promptTemplateSha256": provenance["promptTemplateSha256"],
        "judgmentSchemaSha256": provenance["judgmentSchemaSha256"],
        "rubricSha256": provenance["rubricSha256"],
        "model": provenance["model"],
        "reasoningEffort": provenance["reasoningEffort"],
    }
    for field, expected_value in expected.items():
        if value[field] != expected_value:
            raise ProvenanceFailure(f"candidate judge input {field} mismatch")
    artifacts = evaluation_run["artifacts"]
    if (
        provenance["candidateJudgeInputSha256"] != envelope["selfSha256"]
        or provenance["candidateJudgeInputRawSha256"] != envelope["rawSha256"]
        or artifacts["candidateJudgeInputSha256"] != envelope["selfSha256"]
        or artifacts["candidateJudgeInputRawSha256"] != envelope["rawSha256"]
    ):
        raise ProvenanceFailure(
            "candidate judge input raw/self evaluation/provenance binding mismatch"
        )
    return envelope


def _artifact_citation_index(artifact: dict[str, Any]) -> dict[tuple[str, str], dict[str, str]]:
    anonymous_id = artifact["anonymousArtifactId"]
    media_by_ref: dict[tuple[str, str], dict[str, Any]] = {}
    for media in artifact["mediaArtifacts"]:
        key = (media["artifactId"], media["locator"])
        if key in media_by_ref:
            raise ProvenanceFailure(
                f"evidence artifact {anonymous_id} duplicates a media artifact ID/locator"
            )
        media_by_ref[key] = media
    result: dict[tuple[str, str], dict[str, str]] = {}
    for trace in artifact["traceRows"]:
        checkpoint = trace["checkpoint"]
        sources = list(trace["citedVisibleSources"])
        action_artifact_id = trace.get("actionOccurrenceId")
        action_locator = trace.get("actionLocator")
        if isinstance(action_artifact_id, str) and isinstance(action_locator, str):
            sources.append({"artifactId": action_artifact_id, "locator": action_locator})
        for source in sources:
            media_key = (source["artifactId"], source["locator"])
            if media_key not in media_by_ref and not (
                source["artifactId"] == action_artifact_id
                and source["locator"] == action_locator
            ):
                raise ProvenanceFailure(
                    f"evidence trace {trace['traceRowId']} cites absent media/action locator"
                )
            citation = {
                "anonymousArtifactId": anonymous_id,
                "artifactId": source["artifactId"],
                "locator": source["locator"],
            }
            for selector in (source["artifactId"], source["locator"]):
                key = (checkpoint, selector)
                previous = result.get(key)
                if previous is not None and previous != citation:
                    raise ProvenanceFailure(
                        f"evidence checkpoint {checkpoint} has an ambiguous cited artifact {selector}"
                    )
                result[key] = citation
    return result


def _derive_verification_observations(
    panel: dict[str, Any],
    evidence_authority: dict[str, Any],
) -> list[dict[str, Any]]:
    evidence_by_id = evidence_authority["artifactsById"]
    citations_by_id = {
        anonymous_id: _artifact_citation_index(artifact)
        for anonymous_id, artifact in evidence_by_id.items()
    }
    ordered_claims: list[str] = []
    citations_by_claim: dict[str, list[dict[str, str]]] = {}
    citation_keys_by_claim: dict[str, set[bytes]] = {}
    for judgment in panel["judgments"]:
        for artifact in judgment["artifactJudgments"]:
            anonymous_id = artifact["anonymousArtifactId"]
            citation_index = citations_by_id[anonymous_id]
            for cell in artifact["cells"]:
                for collection_name in ("strengthEvidence", "gapEvidence"):
                    for evidence in cell[collection_name]:
                        citation = citation_index.get(
                            (evidence["checkpoint"], evidence["artifact"])
                        )
                        if citation is None:
                            raise ProvenanceFailure(
                                "judge observation cites no exact evidence-set media/action locator: "
                                f"{anonymous_id}/{evidence['checkpoint']}/{evidence['artifact']}"
                            )
                        claim = evidence["observation"]
                        if claim not in citations_by_claim:
                            ordered_claims.append(claim)
                            citations_by_claim[claim] = []
                            citation_keys_by_claim[claim] = set()
                        citation_key = canonical_json_bytes(citation)
                        if citation_key not in citation_keys_by_claim[claim]:
                            citation_keys_by_claim[claim].add(citation_key)
                            citations_by_claim[claim].append(citation)
    observations: list[dict[str, Any]] = []
    for index, claim in enumerate(ordered_claims, start=1):
        citations = citations_by_claim[claim]
        if not 1 <= len(citations) <= 8:
            raise ProvenanceFailure(
                f"deduplicated judge observation has {len(citations)} cited sources; expected 1..8"
            )
        observations.append({
            "observationId": f"OBS-{index:04d}",
            "claimType": "JUDGE_EVIDENCE",
            "incidentKey": None,
            "claim": claim,
            "citedSources": citations,
        })
    incident_rows: dict[str, dict[str, Any]] = {}
    actor_authorities = evidence_authority.get("actorAuthoritiesByAnonymousId")
    for anonymous_id in (
        ("ARTIFACT-A", "ARTIFACT-B", "ARTIFACT-C")
        if isinstance(actor_authorities, dict)
        else ()
    ):
        actor = actor_authorities.get(anonymous_id)
        artifact = evidence_by_id.get(anonymous_id)
        if actor is None or artifact is None:
            raise ProvenanceFailure(
                "incident verification has no exact actor/evidence authority"
            )
        media = {
            (row["artifactId"], row["locator"]): row
            for row in artifact["mediaArtifacts"]
        }
        for incident in actor["value"]["incidents"]:
            key = incident["incidentKey"]
            row = incident_rows.setdefault(key, {
                "incidentType": incident["incidentType"],
                "actorIds": [],
                "citations": [],
            })
            if row["incidentType"] != incident["incidentType"]:
                raise ProvenanceFailure(
                    f"actor incident {key} has conflicting incident types"
                )
            row["actorIds"].append(anonymous_id)
            # One exact raw-media citation per actor occurrence is sufficient
            # to ask the verifier whether that occurrence is supported.  The
            # actor/evidence validator separately proves that every incident
            # reference belongs to the immutable recording bundle.
            source = incident["artifactRefs"][0]
            if (source["artifactId"], source["locator"]) not in media:
                raise ProvenanceFailure(
                    f"actor incident {key} cites media absent from evidence"
                )
            row["citations"].append({
                "anonymousArtifactId": anonymous_id,
                "artifactId": source["artifactId"],
                "locator": source["locator"],
            })
    for incident_key, row in incident_rows.items():
        observations.append({
            "observationId": f"OBS-{len(observations) + 1:04d}",
            "claimType": "ACTOR_INCIDENT",
            "incidentKey": incident_key,
            "claim": (
                f"Actor incident {incident_key} ({row['incidentType']}) is visibly or "
                f"audibly supported for {','.join(row['actorIds'])}."
            ),
            "citedSources": row["citations"],
        })
    if not observations:
        raise ProvenanceFailure("judge panel produced no verification observations")
    return observations


def validate_verification_input_authority(
    value: Any,
    raw_bytes: bytes,
    candidate: dict[str, Any],
    candidate_manifest: dict[str, Any],
    evaluation_run: dict[str, Any],
    evidence_authority: dict[str, Any],
    panel: dict[str, Any],
    holdout_receipt: dict[str, Any],
    gold_binding: dict[str, Any],
    sanitized_bundle: dict[str, Any],
) -> dict[str, Any]:
    wrapped = validate_self_hashed_envelope(
        value,
        raw_bytes,
        schema_version=VERIFICATION_INPUT_SCHEMA,
        self_field="verificationInputSha256",
        schema_validator=validate_verification_input_schema,
        label="native evidence verification input",
    )
    verification = wrapped["value"]
    artifacts = evaluation_run["artifacts"]
    expected = {
        "verificationScope": "VISIBLE_OR_AUDIBLE_OBSERVATION_ONLY",
        "verificationInputSha256": candidate["verificationInputSha256"],
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "holdoutConsumptionReceiptSha256": holdout_receipt["selfSha256"],
        "goldBindingManifestSha256": gold_binding["selfSha256"],
        "evidenceSetSha256": evidence_authority["selfSha256"],
        "sanitizedEvidenceBundleManifestSha256": sanitized_bundle["selfSha256"],
        "sanitizedEvidenceBundleManifestRawSha256": sanitized_bundle["rawSha256"],
        "sanitizedEvidenceContentRootSha256": sanitized_bundle["value"][
            "contentRootSha256"
        ],
        "opaqueJudgePanelSha256": panel["panelSha256"],
    }
    for field, expected_value in expected.items():
        if verification[field] != expected_value:
            raise ProvenanceFailure(f"verification input {field} mismatch")
    if artifacts.get("verificationInputSha256") != wrapped["selfSha256"]:
        raise ProvenanceFailure("evaluation run verification input self-hash mismatch")
    if artifacts.get("verificationInputRawSha256") != wrapped["rawSha256"]:
        raise ProvenanceFailure("evaluation run verification input raw hash mismatch")
    expected_bundles = []
    for anonymous_id in evidence_authority["value"]["artifactOrder"]:
        bundle = sanitized_bundle["bundlesById"][anonymous_id]
        expected_bundles.append({
            "anonymousArtifactId": anonymous_id,
            "bundleId": bundle["bundleId"],
            "artifactBundleSha256": bundle["artifactBundleSha256"],
            "bundleRootSha256": bundle["bundleRootSha256"],
            "canonicalBundleRoot": bundle["canonicalBundleRoot"],
            "bundleRootPathTail": bundle["bundleRootPathTail"],
        })
    if verification["artifactBundles"] != expected_bundles:
        raise ProvenanceFailure(
            "verification input artifact bundles do not exactly match the evidence set"
        )
    expected_observations = _derive_verification_observations(panel, evidence_authority)
    if verification["observations"] != expected_observations:
        raise ProvenanceFailure(
            "verification input observation set/order/content/citations do not exactly "
            "cover the judge panel"
        )
    expected_ids = [row["observationId"] for row in expected_observations]
    if candidate["expectedObservationIds"] != expected_ids:
        raise ProvenanceFailure(
            "aggregation expectedObservationIds do not match derived verification input"
        )
    wrapped["observations"] = expected_observations
    return wrapped


def validate_evaluation_run_authority(
    value: Any,
    raw_bytes: bytes,
    candidate: dict[str, Any],
    candidate_manifest: dict[str, Any],
    qualification: dict[str, Any],
    *,
    verification_input: dict[str, Any],
    gold_binding: dict[str, Any],
    holdout_receipt: dict[str, Any],
    actor_trace_envelopes: list[dict[str, Any]],
    recording_manifest_envelopes: list[dict[str, Any]],
    coverage_action_ledger: dict[str, Any],
    anonymization: dict[str, Any],
    evidence_set: dict[str, Any],
    sanitized_bundle: dict[str, Any],
    candidate_judge_input: dict[str, Any],
) -> dict[str, Any]:
    wrapped = validate_self_hashed_envelope(
        value,
        raw_bytes,
        schema_version=EVALUATION_RUN_SCHEMA,
        self_field="evaluationRunManifestSha256",
        schema_validator=validate_evaluation_run_schema,
        label="evaluation run manifest",
    )
    run = wrapped["value"]
    provenance = candidate["provenance"]
    if provenance["evaluationRunManifestSha256"] != wrapped["selfSha256"]:
        raise ProvenanceFailure("aggregation input evaluation run self-hash mismatch")
    if provenance["evaluationRunManifestRawSha256"] != wrapped["rawSha256"]:
        raise ProvenanceFailure("aggregation input evaluation run raw hash mismatch")
    expected = {
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "qualificationReceiptSha256": qualification["selfSha256"],
        "judgePanelSha256": provenance["judgePanelSha256"],
        "evaluationPhase": "FORMATIVE" if candidate["recipeId"] == "FORMATIVE-01" else "OFFICIAL_HOLDOUT",
        "officialCommercialUX": candidate["recipeId"] != "FORMATIVE-01",
        "recipeId": candidate["recipeId"],
    }
    for field, expected_value in expected.items():
        if run[field] != expected_value:
            raise ProvenanceFailure(f"evaluation run {field} mismatch")
    artifacts = run["artifacts"]
    raw_expected = {
        "candidateManifestRawSha256": provenance["candidateManifestRawSha256"],
        "qualificationReceiptRawSha256": provenance["qualificationReceiptRawSha256"],
        "judgePanelRawSha256": provenance["judgePanelRawSha256"],
        "coldActorResponseSha256": provenance["coldActorResponseSha256"],
        "coldActorResponseRawSha256": provenance[
            "coldActorResponseRawSha256"
        ],
        "evidenceSetSha256": provenance["evidenceSetSha256"],
        "verificationInputSha256": verification_input["selfSha256"],
        "verificationInputRawSha256": verification_input["rawSha256"],
        "goldBindingManifestSha256": gold_binding["selfSha256"],
        "goldBindingManifestRawSha256": gold_binding["rawSha256"],
        "holdoutConsumptionReceiptSha256": holdout_receipt["selfSha256"],
        "holdoutConsumptionReceiptRawSha256": holdout_receipt["rawSha256"],
        "coverageActionLedgerSha256": coverage_action_ledger["selfSha256"],
        "coverageActionLedgerRawSha256": coverage_action_ledger["rawSha256"],
        "anonymizationManifestSha256": anonymization["selfSha256"],
        "anonymizationManifestRawSha256": anonymization["rawSha256"],
        "evidenceSetSha256": evidence_set["selfSha256"],
        "evidenceSetRawSha256": evidence_set["rawSha256"],
        "sanitizedEvidenceBundleManifestSha256": sanitized_bundle["selfSha256"],
        "sanitizedEvidenceBundleManifestRawSha256": sanitized_bundle["rawSha256"],
        "sanitizedEvidenceContentRootSha256": sanitized_bundle["value"][
            "contentRootSha256"
        ],
        "candidateJudgeInputSha256": candidate_judge_input["selfSha256"],
        "candidateJudgeInputRawSha256": candidate_judge_input["rawSha256"],
    }
    for field, expected_value in raw_expected.items():
        if artifacts[field] != expected_value:
            raise ProvenanceFailure(f"evaluation run artifact {field} mismatch")
    provenance_expected = {
        "holdoutConsumptionReceiptSha256": holdout_receipt["selfSha256"],
        "holdoutConsumptionReceiptRawSha256": holdout_receipt["rawSha256"],
        "goldBindingManifestSha256": gold_binding["selfSha256"],
        "goldBindingManifestRawSha256": gold_binding["rawSha256"],
        "coldActorResponseSha256": artifacts["coldActorResponseSha256"],
        "coldActorResponseRawSha256": artifacts[
            "coldActorResponseRawSha256"
        ],
        "anonymizationManifestSha256": anonymization["selfSha256"],
        "anonymizationManifestRawSha256": anonymization["rawSha256"],
        "evidenceSetSha256": evidence_set["selfSha256"],
        "evidenceSetRawSha256": evidence_set["rawSha256"],
        "sanitizedEvidenceBundleManifestSha256": sanitized_bundle["selfSha256"],
        "sanitizedEvidenceBundleManifestRawSha256": sanitized_bundle["rawSha256"],
        "sanitizedEvidenceContentRootSha256": sanitized_bundle["value"][
            "contentRootSha256"
        ],
        "coverageActionLedgerSha256": coverage_action_ledger["selfSha256"],
        "coverageActionLedgerRawSha256": coverage_action_ledger["rawSha256"],
        "candidateJudgeInputSha256": candidate_judge_input["selfSha256"],
        "candidateJudgeInputRawSha256": candidate_judge_input["rawSha256"],
        "verificationInputRawSha256": verification_input["rawSha256"],
    }
    for field, expected_value in provenance_expected.items():
        if provenance[field] != expected_value:
            raise ProvenanceFailure(f"aggregation provenance {field} mismatch")
    actor_trace_envelopes = sorted(
        actor_trace_envelopes,
        key=lambda row: row["value"]["actorCaptureSlot"],
    )
    if [row["value"]["actorCaptureSlot"] for row in actor_trace_envelopes] != [0, 1, 2]:
        raise ProvenanceFailure("actor traces must cover stable capture slots 0,1,2")
    if artifacts["actorTraceSha256"] != [
        row["selfSha256"] for row in actor_trace_envelopes
    ]:
        raise ProvenanceFailure("evaluation actor trace self-hash order mismatch")
    if artifacts["actorTraceRawSha256"] != [
        row["rawSha256"] for row in actor_trace_envelopes
    ]:
        raise ProvenanceFailure("evaluation actor trace raw-hash order mismatch")
    if artifacts["actorArtifactIds"] != [
        row["value"]["actorArtifactId"] for row in actor_trace_envelopes
    ]:
        raise ProvenanceFailure("evaluation actor artifact ID order mismatch")
    actor_recordings = [
        row for row in recording_manifest_envelopes
        if row["value"]["sourceArtifactKind"] == "ACTOR_OBSERVATION"
    ]
    coverage_recordings = [
        row for row in recording_manifest_envelopes
        if row["value"]["sourceArtifactKind"] == "COVERAGE_CAPTURE"
    ]
    if len(actor_recordings) != 3 or len(coverage_recordings) != 1:
        raise ProvenanceFailure(
            "evaluation requires three actor and one coverage recording manifests"
        )
    actor_recordings.sort(
        key=lambda row: artifacts["actorObservationRawSha256"].index(
            row["value"]["sourceArtifactSha256"]
        )
        if row["value"]["sourceArtifactSha256"]
        in artifacts["actorObservationRawSha256"]
        else 99
    )
    if (
        [row["selfSha256"] for row in actor_recordings]
        != artifacts["recordingManifestSha256"]
        or [row["rawSha256"] for row in actor_recordings]
        != artifacts["recordingManifestRawSha256"]
    ):
        raise ProvenanceFailure("evaluation actor recording raw/self order mismatch")
    coverage_recording = coverage_recordings[0]
    if (
        coverage_recording["selfSha256"]
        != artifacts["coverageRecordingManifestSha256"]
        or coverage_recording["rawSha256"]
        != artifacts["coverageRecordingManifestRawSha256"]
    ):
        raise ProvenanceFailure("evaluation coverage recording raw/self mismatch")
    return wrapped


def validate_evaluation_outcome_transports(
    evaluation_run: dict[str, Any],
    verifier: dict[str, Any],
    verifier_raw_sha256: str,
    oracle_raw_sha256: str,
) -> None:
    artifacts = evaluation_run["value"]["artifacts"]
    if artifacts["verificationOutputSha256"] != verifier_raw_sha256:
        raise ProvenanceFailure("evaluation verifier output raw hash mismatch")
    if artifacts["oracleHardGateLedgerSha256"] != oracle_raw_sha256:
        raise ProvenanceFailure("evaluation oracle ledger raw hash mismatch")
    if artifacts["verifierRunId"] != verifier.get("verifierRunId"):
        raise ProvenanceFailure("evaluation run verifierRunId mismatch")


def bind_judge_attempt_transports(
    evaluation_run: dict[str, Any],
    panel_attempt: dict[str, Any],
    judgment_attempts: list[dict[str, Any]],
) -> None:
    expected_rows = [
        (panel_attempt, evaluation_run["artifacts"]["judgePanelRawSha256"]),
        *zip(
            judgment_attempts,
            evaluation_run["artifacts"]["judgeJudgmentRawSha256"],
        ),
    ]
    for attempt, expected_raw_sha256 in expected_rows:
        if attempt["readStatus"] == "INPUT_UNREADABLE":
            continue
        if attempt["rawSha256"] != expected_raw_sha256:
            attempt["attemptOutcome"] = "TRANSPORT_FAILURE"
            attempt["failureCode"] = (
                f"RAW_HASH_MISMATCH:{attempt['slotId']}:"
                f"expected={expected_raw_sha256}:actual={attempt['rawSha256']}"
            )[:300]


def classify_judge_attempt_schemas(
    panel_attempt: dict[str, Any],
    judgment_attempts: list[dict[str, Any]],
) -> None:
    """Classify parsed-but-malformed slots before a terminal receipt is built."""

    if panel_attempt["attemptOutcome"] == "VALID":
        try:
            validate_judge_panel_manifest_schema(panel_attempt["value"])
        except ValidationFailure as exception:
            panel_attempt["attemptOutcome"] = "SCHEMA_FAILURE"
            panel_attempt["failureCode"] = (
                f"PANEL:{type(exception).__name__}:{str(exception)}"
            )[:300]
    for attempt in judgment_attempts:
        if attempt["attemptOutcome"] != "VALID":
            continue
        try:
            validate_checked_in_schema(
                attempt["value"],
                NATIVE_JUDGE_SCHEMA_PATH,
                f"{attempt['slotId']} native judgment",
            )
        except ValidationFailure as exception:
            attempt["attemptOutcome"] = "SCHEMA_FAILURE"
            attempt["failureCode"] = (
                f"{attempt['slotId']}:{type(exception).__name__}:{str(exception)}"
            )[:300]


def validate_judge_panel_authority(
    value: Any,
    raw_bytes: bytes,
    candidate: dict[str, Any],
    candidate_manifest: dict[str, Any],
    qualification: dict[str, Any],
    evaluation_run: dict[str, Any],
    candidate_judge_input: dict[str, Any],
    judgment_attempts: list[dict[str, Any]],
) -> dict[str, Any]:
    wrapped = validate_self_hashed_envelope(
        value,
        raw_bytes,
        schema_version=JUDGE_PANEL_MANIFEST_SCHEMA,
        self_field="judgePanelSha256",
        schema_validator=validate_judge_panel_manifest_schema,
        label="judge panel manifest",
    )
    panel = wrapped["value"]
    provenance = candidate["provenance"]
    if provenance["judgePanelSha256"] != wrapped["selfSha256"]:
        raise ProvenanceFailure("aggregation input judge panel self-hash mismatch")
    if provenance["judgePanelRawSha256"] != wrapped["rawSha256"]:
        raise ProvenanceFailure("aggregation input judge panel raw hash mismatch")
    expected = {
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "qualificationReceiptSha256": qualification["selfSha256"],
        "evaluationPhase": candidate_manifest["evaluationPhase"],
        "officialCommercialUX": candidate_manifest["officialCommercialUX"],
        "recipeId": candidate["recipeId"],
        "evidenceSetSha256": provenance["evidenceSetSha256"],
        "model": provenance["model"],
        "reasoningEffort": provenance["reasoningEffort"],
        "transportVersion": candidate_manifest["evaluator"]["transportVersion"],
        "promptTemplateSha256": provenance["promptTemplateSha256"],
        "judgmentSchemaSha256": provenance["judgmentSchemaSha256"],
        "rubricSha256": provenance["rubricSha256"],
    }
    for field, expected_value in expected.items():
        if panel[field] != expected_value:
            raise ProvenanceFailure(f"judge panel manifest {field} mismatch")
    if evaluation_run["judgePanelSha256"] != wrapped["selfSha256"]:
        raise ProvenanceFailure("evaluation run judge panel self-hash mismatch")
    if evaluation_run["artifacts"]["judgePanelRawSha256"] != wrapped["rawSha256"]:
        raise ProvenanceFailure("evaluation run judge panel raw hash mismatch")
    expected_order = [row["anonymousArtifactId"] for row in candidate["artifactBindings"]]
    if panel["artifactOrder"] != expected_order:
        raise ProvenanceFailure("judge panel artifact order mismatch")
    slot_run_ids = [slot["judgeRunId"] for slot in panel["slots"]]
    slot_input_shas = [slot["judgeInputSha256"] for slot in panel["slots"]]
    slot_raw_shas = [slot["judgmentRawSha256"] for slot in panel["slots"]]
    actual_raw_shas = [attempt["rawSha256"] for attempt in judgment_attempts]
    if len(set(slot_run_ids)) != 3 or len(set(slot_raw_shas)) != 3:
        raise ProvenanceFailure("judge panel slots must bind three fresh judge runs and outputs")
    if len(set(slot_input_shas)) != 1:
        raise ProvenanceFailure("judge panel slots must bind one identical judge input")
    if slot_input_shas != [candidate_judge_input["selfSha256"]] * 3:
        raise ProvenanceFailure(
            "judge panel slots do not bind the exact candidate judge input artifact"
        )
    if slot_raw_shas != actual_raw_shas:
        raise ProvenanceFailure("judge panel slot raw judgment hashes/order mismatch")
    wrapped["runIds"] = slot_run_ids
    wrapped["judgeInputSha256"] = slot_input_shas[0]
    return wrapped


def _validate_evidence_rows(value: Any, label: str) -> list[dict[str, str]]:
    if not isinstance(value, list) or len(value) > 6:
        raise JudgmentValidationFailure(f"{label} must contain at most six rows")
    seen: set[bytes] = set()
    rows: list[dict[str, str]] = []
    for index, item in enumerate(value):
        row = exact_keys(item, EVIDENCE_KEYS, f"{label}[{index}]", JudgmentValidationFailure)
        for field, maximum in (("checkpoint", 140), ("artifact", 300), ("observation", 1200)):
            require_string(row[field], f"{label}[{index}].{field}", maximum, JudgmentValidationFailure)
        canonical = canonical_json_bytes(row)
        if canonical in seen:
            raise JudgmentValidationFailure(f"{label} contains duplicate rows")
        seen.add(canonical)
        rows.append(dict(row))
    return rows


def _validate_cell(value: Any, expected_cell_id: str, label: str) -> dict[str, Any]:
    cell = exact_keys(value, CELL_KEYS, label, JudgmentValidationFailure)
    if cell["cellId"] != expected_cell_id:
        raise JudgmentValidationFailure(
            f"{label}.cellId must be {expected_cell_id!r}, got {cell['cellId']!r}"
        )
    if cell["label"] not in LABELS:
        raise JudgmentValidationFailure(f"{label}.label is invalid")
    if cell["confidence"] not in {"HIGH", "MEDIUM"}:
        raise JudgmentValidationFailure(f"{label}.confidence must be HIGH or MEDIUM")
    strength = _validate_evidence_rows(cell["strengthEvidence"], f"{label}.strengthEvidence")
    gap = _validate_evidence_rows(cell["gapEvidence"], f"{label}.gapEvidence")
    if not strength and not gap:
        raise JudgmentValidationFailure(f"{label} requires at least one evidence row")
    if cell["label"] == "EXCELLENT" and not strength:
        raise JudgmentValidationFailure(f"{label} EXCELLENT requires strength evidence")
    incidents = require_unique_strings(
        cell["incidentKeys"],
        f"{label}.incidentKeys",
        maximum=16,
        pattern=INCIDENT_KEY_PATTERN,
        error=JudgmentValidationFailure,
    )
    recommended = cell["recommendedChange"]
    if recommended is not None:
        require_string(recommended, f"{label}.recommendedChange", 800, JudgmentValidationFailure)
    return cell


def validate_panel(
    judgments: list[Any],
    candidate: dict[str, Any],
    rubric: dict[str, Any],
    panel_authority: dict[str, Any],
) -> dict[str, Any]:
    if len(judgments) != 3:
        raise JudgmentValidationFailure("native panel requires exactly three judgments")
    expected_bindings = candidate["artifactBindings"]
    validated: list[dict[str, Any]] = []
    run_ids: list[str] = []
    artifact_orders: list[list[dict[str, str]]] = []
    judge_input_shas: list[str] = []
    provenance = candidate["provenance"]
    manifest = panel_authority["value"]
    for judge_index, item in enumerate(judgments):
        prefix = f"judgment[{judge_index}]"
        judgment = exact_keys(item, JUDGMENT_KEYS, prefix, JudgmentValidationFailure)
        expected_identity = {
            "schemaVersion": JUDGMENT_SCHEMA,
            "protocol": PROTOCOL,
            "judgmentMode": "EVIDENCE_SET",
            "judgeSlot": "SOL-ULTRA",
            "model": "gpt-5.6-sol",
            "reasoningEffort": "ultra",
            "promptTemplateSha256": provenance["promptTemplateSha256"],
            "judgmentSchemaSha256": provenance["judgmentSchemaSha256"],
            "rubricSha256": rubric["sha256"],
            "evidenceSetSha256": provenance["evidenceSetSha256"],
        }
        for field, expected in expected_identity.items():
            if judgment[field] != expected:
                raise JudgmentValidationFailure(
                    f"{prefix}.{field} must be {expected!r}, got {judgment[field]!r}"
                )
        run_id = require_string(judgment["judgeRunId"], f"{prefix}.judgeRunId", 200, JudgmentValidationFailure)
        run_ids.append(run_id)
        judge_input_shas.append(require_sha(
            judgment["judgeInputSha256"],
            f"{prefix}.judgeInputSha256",
            JudgmentValidationFailure,
        ))
        artifacts = judgment["artifactJudgments"]
        if not isinstance(artifacts, list) or len(artifacts) != 4:
            raise JudgmentValidationFailure(f"{prefix}.artifactJudgments must contain four rows")
        bindings: list[dict[str, str]] = []
        seen_artifacts: set[str] = set()
        cold_count = 0
        coverage_count = 0
        for artifact_index, artifact_item in enumerate(artifacts):
            artifact_prefix = f"{prefix}.artifactJudgments[{artifact_index}]"
            artifact = exact_keys(
                artifact_item,
                ARTIFACT_JUDGMENT_KEYS,
                artifact_prefix,
                JudgmentValidationFailure,
            )
            artifact_id = artifact["anonymousArtifactId"]
            if artifact_id in seen_artifacts:
                raise JudgmentValidationFailure(f"{prefix} contains duplicate artifact IDs")
            seen_artifacts.add(artifact_id)
            if artifact["artifactKind"] == "COLD_ACTOR":
                expected_cells = COLD_CELLS
                cold_count += 1
            elif artifact["artifactKind"] == "COVERAGE":
                expected_cells = COVERAGE_CELLS
                coverage_count += 1
            else:
                raise JudgmentValidationFailure(f"{artifact_prefix}.artifactKind is invalid")
            require_sha(artifact["artifactSha256"], f"{artifact_prefix}.artifactSha256", JudgmentValidationFailure)
            bindings.append({
                "anonymousArtifactId": artifact_id,
                "artifactKind": artifact["artifactKind"],
                "artifactSha256": artifact["artifactSha256"],
            })
            cells = artifact["cells"]
            if not isinstance(cells, list) or len(cells) != len(expected_cells):
                raise JudgmentValidationFailure(
                    f"{artifact_prefix}.cells must contain exactly {len(expected_cells)} rows"
                )
            for cell_index, (cell, expected_cell_id) in enumerate(zip(cells, expected_cells)):
                _validate_cell(cell, expected_cell_id, f"{artifact_prefix}.cells[{cell_index}]")
        if cold_count != 3 or coverage_count != 1:
            raise JudgmentValidationFailure(f"{prefix} must contain three cold and one coverage judgment")
        if bindings != expected_bindings:
            raise JudgmentValidationFailure(
                f"{prefix} artifact order/hash bindings do not match candidate evidence"
            )
        artifact_orders.append(bindings)
        validated.append(judgment)
    manifest_run_ids = [slot["judgeRunId"] for slot in manifest["slots"]]
    manifest_input_shas = [slot["judgeInputSha256"] for slot in manifest["slots"]]
    for judge_index, (run_id, input_sha, expected_run_id, expected_input_sha) in enumerate(zip(
        run_ids,
        judge_input_shas,
        manifest_run_ids,
        manifest_input_shas,
    )):
        if run_id != expected_run_id or input_sha != expected_input_sha:
            raise JudgmentValidationFailure(
                f"judgment[{judge_index}] does not match its judge-panel slot ledger"
            )
    if len(set(run_ids)) != 3:
        raise JudgmentValidationFailure("judgeRunIds must be distinct")
    if len(set(judge_input_shas)) != 1:
        raise JudgmentValidationFailure("three judges must bind the same judgeInputSha256")
    if artifact_orders[1:] != artifact_orders[:1] * 2:
        raise JudgmentValidationFailure("judge artifact order differs across the panel")
    return {
        "judgments": validated,
        "runIds": run_ids,
        "artifactBindings": artifact_orders[0],
        "panelSha256": panel_authority["selfSha256"],
    }

def compute_lane_inputs(panel: dict[str, Any], rubric: dict[str, Any]) -> dict[str, Any]:
    judgment_artifacts: list[dict[str, dict[str, Any]]] = []
    for judgment in panel["judgments"]:
        judgment_artifacts.append({
            artifact["anonymousArtifactId"]: artifact
            for artifact in judgment["artifactJudgments"]
        })
    cold_bindings = [row for row in panel["artifactBindings"] if row["artifactKind"] == "COLD_ACTOR"]
    coverage_binding = next(
        row for row in panel["artifactBindings"] if row["artifactKind"] == "COVERAGE"
    )
    lane_inputs: dict[str, Any] = {}
    for category in rubric["categories"]:
        for cell in category["cells"]:
            cell_id = cell["id"]
            ownership = cell["laneOwnership"]
            cell_result: dict[str, Any] = {"cold": None, "coverage": None}
            if "COLD-JOURNEY" in ownership:
                actor_rows: list[dict[str, Any]] = []
                for binding in cold_bindings:
                    artifact_id = binding["anonymousArtifactId"]
                    labels: list[str] = []
                    for artifacts in judgment_artifacts:
                        artifact = artifacts[artifact_id]
                        by_cell = {row["cellId"]: row for row in artifact["cells"]}
                        labels.append(by_cell[cell_id]["label"])
                    scores = [LABELS[label][1] for label in labels]
                    ordinals = [LABELS[label][0] for label in labels]
                    actor_rows.append({
                        "anonymousArtifactId": artifact_id,
                        "judgeMedianScore": median_three(scores),
                        "judgeSpread": max(scores) - min(scores),
                        "judgeOrdinalRange": max(ordinals) - min(ordinals),
                    })
                actor_scores = [row["judgeMedianScore"] for row in actor_rows]
                actor_ordinals = [
                    next(ordinal for ordinal, score in LABELS.values() if score == actor_score)
                    for actor_score in actor_scores
                ]
                cold_judge_spread = median_three(row["judgeSpread"] for row in actor_rows)
                actor_spread = max(actor_scores) - min(actor_scores)
                actor_ordinal_range = max(actor_ordinals) - min(actor_ordinals)
                cell_result["cold"] = {
                    "actors": actor_rows,
                    "coldCellScore": median_three(actor_scores),
                    "coldJudgeSpread": cold_judge_spread,
                    "actorSpread": actor_spread,
                    "coldCellSpread": (cold_judge_spread + actor_spread) / 2,
                    "actorOrdinalRange": actor_ordinal_range,
                    "unstable": (
                        actor_ordinal_range >= 2
                        or any(row["judgeOrdinalRange"] >= 2 for row in actor_rows)
                    ),
                }
            if "COVERAGE-JOURNEY" in ownership:
                artifact_id = coverage_binding["anonymousArtifactId"]
                labels = []
                for artifacts in judgment_artifacts:
                    artifact = artifacts[artifact_id]
                    by_cell = {row["cellId"]: row for row in artifact["cells"]}
                    labels.append(by_cell[cell_id]["label"])
                scores = [LABELS[label][1] for label in labels]
                ordinals = [LABELS[label][0] for label in labels]
                ordinal_range = max(ordinals) - min(ordinals)
                cell_result["coverage"] = {
                    "coverageCellScore": median_three(scores),
                    "coverageCellSpread": max(scores) - min(scores),
                    "judgeOrdinalRange": ordinal_range,
                    "unstable": ordinal_range >= 2,
                }
            lane_inputs[cell_id] = cell_result
    return lane_inputs


def validate_incident_actor_evidence(
    incidents: list[dict[str, Any]],
    actor_by_anonymous_id: dict[str, dict[str, Any]],
) -> dict[str, dict[str, set[str]]]:
    cold_ids = set(actor_by_anonymous_id)
    observed: dict[str, set[str]] = {}
    severe: dict[str, set[str]] = {}
    observed_types: dict[str, set[str]] = {}
    terminal_stalls: dict[str, set[str]] = {}
    for actor_id, actor in actor_by_anonymous_id.items():
        for key in actor["incidentKeys"]:
            if actor["incidentTypes"][key] == "HARNESS_FAILURE":
                continue
            observed.setdefault(key, set()).add(actor_id)
            observed_types.setdefault(key, set()).add(actor["incidentTypes"][key])
        for key in actor["severeIncidentKeys"]:
            severe.setdefault(key, set()).add(actor_id)
        if (
            actor["terminalState"] == "PLAYER_STALLED"
            and actor["terminalIncidentKey"] is not None
        ):
            terminal_stalls.setdefault(actor["terminalIncidentKey"], set()).add(actor_id)
    ledger_by_key = {row["incidentKey"]: row for row in incidents}
    for key, actor_ids in observed.items():
        if key not in ledger_by_key:
            raise ProvenanceFailure(f"actor incident {key} is absent from the oracle/verifier ledger")
        ledger_ids = set(ledger_by_key[key]["actorArtifactIds"])
        if ledger_ids != actor_ids:
            raise ProvenanceFailure(f"incident {key} actor occurrence ledger is not exact")
        if observed_types[key] != {ledger_by_key[key]["incidentType"]}:
            raise ProvenanceFailure(f"incident {key} actor/ledger type mapping is not exact")
    for incident in incidents:
        ledger_ids = set(incident["actorArtifactIds"])
        if not ledger_ids <= cold_ids:
            raise ProvenanceFailure(f"incident {incident['incidentKey']} cites a non-cold actor")
        if incident["incidentType"] == "HARD_GATE_FAILURE":
            continue
        if observed.get(incident["incidentKey"], set()) != ledger_ids:
            raise ProvenanceFailure(f"incident {incident['incidentKey']} has no exact actor evidence")
    return {
        key: {
            "observedActorIds": observed.get(key, set()),
            "severeActorIds": severe.get(key, set()),
            "incidentTypes": observed_types.get(key, set()),
            "terminalStalledActorIds": terminal_stalls.get(key, set()),
        }
        for key in set(observed) | set(severe)
    }


def validate_severe_single_run_authority(
    actor_by_anonymous_id: dict[str, dict[str, Any]],
    incidents: list[dict[str, Any]],
    incident_evidence: dict[str, dict[str, set[str]]],
) -> None:
    ledger_by_key = {row["incidentKey"]: row for row in incidents}
    for actor_id, actor in actor_by_anonymous_id.items():
        derived = False
        if actor["terminalState"] not in {"HARNESS_BLOCKED", "INVALID"}:
            for key in actor["severeIncidentKeys"]:
                ledger = ledger_by_key.get(key)
                evidence = incident_evidence.get(key, {})
                severe_actor_ids = evidence.get("severeActorIds", set())
                if (
                    ledger is None
                    or ledger["verifierStatus"] != "SUPPORTED"
                    or severe_actor_ids != {actor_id}
                ):
                    continue
                kind = ledger["incidentType"]
                if kind == "UX_STALL":
                    qualifies = ledger["oracleStatus"] == "EXACT" and ledger["capCandidate"] == 49
                elif kind == "EXTERNAL_HINT_ATTEMPT":
                    qualifies = True
                elif kind == "RECOVERY_FRICTION":
                    qualifies = ledger["oracleStatus"] == "EXACT" and ledger["capCandidate"] <= 69
                elif kind == "CONFUSION":
                    qualifies = ledger["capCandidate"] <= 79 and len(ledger["checkpointRefs"]) >= 2
                else:
                    qualifies = False
                if qualifies:
                    derived = True
                    break
        if actor["declaredSevereSingleRun"] is not derived:
            raise ProvenanceFailure(
                f"actor {actor_id} severeSingleRun is not supported by the exact incident ledger"
            )
        actor["severeSingleRun"] = derived


def _single_actor_severe_incident(
    incidents: list[dict[str, Any]],
    incident_evidence: dict[str, dict[str, set[str]]],
    actor_by_anonymous_id: dict[str, dict[str, Any]],
) -> bool:
    for incident in incidents:
        severe_actors = incident_evidence.get(incident["incidentKey"], {}).get(
            "severeActorIds", set()
        )
        if incident["verifierStatus"] != "SUPPORTED" or len(severe_actors) != 1:
            continue
        actor_id = next(iter(severe_actors))
        if not actor_by_anonymous_id.get(actor_id, {}).get("severeSingleRun", False):
            continue
        kind = incident["incidentType"]
        if kind == "UX_STALL" and incident["oracleStatus"] == "EXACT":
            return True
        if kind == "EXTERNAL_HINT_ATTEMPT":
            return True
        if kind == "RECOVERY_FRICTION" and incident["capCandidate"] <= 69 and incident["oracleStatus"] == "EXACT":
            return True
        if kind == "CONFUSION" and incident["capCandidate"] <= 79 and len(incident["checkpointRefs"]) >= 2:
            return True
    return False


def _terminal_state_unstable(
    actor_by_anonymous_id: dict[str, dict[str, Any]],
    incidents: list[dict[str, Any]],
    incident_evidence: dict[str, dict[str, set[str]]],
) -> bool:
    stalled = {
        actor_id: row["terminalIncidentKey"]
        for actor_id, row in actor_by_anonymous_id.items()
        if row["terminalState"] == "PLAYER_STALLED"
    }
    if not stalled:
        return False
    keys = set(stalled.values())
    if len(stalled) >= 2 and len(keys) == 1 and None not in keys:
        key = next(iter(keys))
        incident = next((row for row in incidents if row["incidentKey"] == key), None)
        severe_actors = incident_evidence.get(key, {}).get("severeActorIds", set())
        if (
            incident is not None
            and incident["incidentType"] == "UX_STALL"
            and incident["verifierStatus"] == "SUPPORTED"
            and incident["oracleStatus"] == "EXACT"
            and incident["capCandidate"] == 49
            and set(stalled) <= severe_actors
        ):
            return False
    return True


def terminal_blocker(actor_by_anonymous_id: dict[str, dict[str, Any]]) -> str | None:
    states = {row["terminalState"] for row in actor_by_anonymous_id.values()}
    if "HARNESS_BLOCKED" in states:
        return "BLOCKED_HARNESS"
    if "INVALID" in states:
        return "BLOCKED_MISSING_EVIDENCE"
    return None


def unstable_lanes(
    lane_inputs: dict[str, Any],
    incidents: list[dict[str, Any]],
    actor_by_anonymous_id: dict[str, dict[str, Any]],
    incident_evidence: dict[str, dict[str, set[str]]],
) -> list[str]:
    cold = any(row["cold"] is not None and row["cold"]["unstable"] for row in lane_inputs.values())
    coverage = any(
        row["coverage"] is not None and row["coverage"]["unstable"]
        for row in lane_inputs.values()
    )
    if _terminal_state_unstable(actor_by_anonymous_id, incidents, incident_evidence):
        cold = True
    if _single_actor_severe_incident(
        incidents,
        incident_evidence,
        actor_by_anonymous_id,
    ):
        cold = True
    lanes: list[str] = []
    if cold:
        lanes.append("COLD-JOURNEY")
    if coverage:
        lanes.append("COVERAGE-JOURNEY")
    return lanes


def _validate_artifact_ref(value: Any, label: str) -> None:
    row = exact_keys(value, ARTIFACT_REF_KEYS, label)
    require_string(row["artifactId"], f"{label}.artifactId", 200)
    require_string(row["locator"], f"{label}.locator", 300)
    require_sha(row["sha256"], f"{label}.sha256")


def validate_oracle_ledger(
    value: Any,
    raw_sha256: str,
    candidate: dict[str, Any],
    rubric: dict[str, Any],
    holdout_receipt: dict[str, Any],
    gold_binding: dict[str, Any],
    verification_input: dict[str, Any],
    evaluation_run: dict[str, Any],
    recordings: dict[str, Any],
    verified_verifier: dict[str, Any] | None,
) -> dict[str, Any]:
    validate_checked_in_schema(
        value,
        ORACLE_LEDGER_SCHEMA_PATH,
        "oracle hard-gate ledger",
    )
    ledger = exact_keys(value, LEDGER_KEYS, "oracle hard-gate ledger")
    if ledger["schemaVersion"] != ORACLE_LEDGER_SCHEMA or ledger["protocol"] != PROTOCOL:
        raise ValidationFailure("oracle hard-gate ledger identity is invalid")
    require_string(ledger["ledgerId"], "oracle ledger.ledgerId", 200)
    provenance = candidate["provenance"]
    artifacts = evaluation_run["value"]["artifacts"]
    if raw_sha256 != provenance["oracleHardGateLedgerSha256"]:
        raise ProvenanceFailure("oracle hard-gate ledger file hash does not match candidate provenance")
    bindings = {
        "candidateManifestSha256": provenance["candidateManifestSha256"],
        "holdoutConsumptionReceiptSha256": holdout_receipt["selfSha256"],
        "goldBindingManifestSha256": gold_binding["selfSha256"],
        "coldActorResponseSha256": provenance["coldActorResponseSha256"],
        "coldActorResponseRawSha256": provenance[
            "coldActorResponseRawSha256"
        ],
        "actorTraceSha256": artifacts["actorTraceSha256"],
        "coverageActionLedgerSha256": provenance[
            "coverageActionLedgerSha256"
        ],
        "coverageArtifactId": artifacts["coverageArtifactId"],
        "recordingManifestSha256": [
            *artifacts["recordingManifestSha256"],
            artifacts["coverageRecordingManifestSha256"],
        ],
        "anonymizationManifestSha256": provenance[
            "anonymizationManifestSha256"
        ],
        "evidenceSetSha256": provenance["evidenceSetSha256"],
        "sanitizedEvidenceBundleManifestSha256": provenance[
            "sanitizedEvidenceBundleManifestSha256"
        ],
        "sanitizedEvidenceContentRootSha256": provenance[
            "sanitizedEvidenceContentRootSha256"
        ],
        "candidateJudgeInputSha256": provenance["candidateJudgeInputSha256"],
        "verificationInputSha256": verification_input["selfSha256"],
        "verificationOutputSha256": provenance["verificationOutputSha256"],
        "rubricSha256": rubric["sha256"],
        "canonicalHashPolicySha256": file_sha256(
            NATIVE_DIRECTORY / "canonical-hash-policy.json",
            "checked-in canonical hash policy",
        ),
        "goldStateContractSha256": file_sha256(
            GOLD_STATE_MANIFEST_PATH,
            "checked-in gold-state manifest",
        ),
        "coverageRecipeSha256": file_sha256(
            NATIVE_DIRECTORY / "coverage-recipe.json",
            "checked-in coverage recipe",
        ),
        "conceptManifestSha256": file_sha256(
            CONCEPT_MANIFEST_PATH,
            "checked-in concept manifest",
        ),
        "nativeAggregatorSha256": file_sha256(
            Path(__file__).resolve(),
            "native aggregator",
        ),
        "contractValidatorSha256": file_sha256(
            CONTRACT_VALIDATOR_PATH,
            "native contract validator",
        ),
        "goldValidatorSha256": file_sha256(
            GOLD_STATE_VALIDATOR_PATH,
            "gold-state validator",
        ),
    }
    for field, expected in bindings.items():
        if ledger[field] != expected:
            raise ProvenanceFailure(f"oracle ledger {field} does not match candidate provenance")
    expected_contract_bindings_sha = file_sha256(
        CONTRACT_BINDINGS_PATH,
        "checked-in contract bindings",
    )
    if ledger["contractBindingsSha256"] != expected_contract_bindings_sha:
        raise ProvenanceFailure("oracle contractBindingsSha256 does not match raw authority bytes")
    checks = ledger["oracleChecks"]
    if not isinstance(checks, list) or not 1 <= len(checks) <= 4096:
        raise ValidationFailure("oracle ledger.oracleChecks must contain 1..4096 rows")
    check_ids: set[str] = set()
    for index, item in enumerate(checks):
        row = exact_keys(item, ORACLE_CHECK_KEYS, f"oracleChecks[{index}]")
        check_id = require_string(row["oracleCheckId"], f"oracleChecks[{index}].oracleCheckId", 100)
        if check_id in check_ids:
            raise ValidationFailure("oracleChecks contains duplicate IDs")
        check_ids.add(check_id)
        if row["status"] not in {"EXACT", "MISMATCH", "MISSING"}:
            raise ValidationFailure(f"oracleChecks[{index}].status is invalid")
        if not isinstance(row["inputHashes"], list) or not row["inputHashes"]:
            raise ValidationFailure(f"oracleChecks[{index}].inputHashes is invalid")
        for hash_index, hash_value in enumerate(row["inputHashes"]):
            require_sha(hash_value, f"oracleChecks[{index}].inputHashes[{hash_index}]")
        for field in ("expectedCanonicalSha256", "observedCanonicalSha256"):
            if row[field] is not None:
                require_sha(row[field], f"oracleChecks[{index}].{field}")
        require_string(row["details"], f"oracleChecks[{index}].details")
        if not isinstance(row["evidenceRefs"], list) or not row["evidenceRefs"]:
            raise ValidationFailure(f"oracleChecks[{index}].evidenceRefs is invalid")
        for ref_index, ref in enumerate(row["evidenceRefs"]):
            _validate_artifact_ref(ref, f"oracleChecks[{index}].evidenceRefs[{ref_index}]")
    gates = ledger["hardGates"]
    if not isinstance(gates, list) or len(gates) != len(HARD_GATE_IDS):
        raise ValidationFailure("oracle ledger must contain exactly thirteen hard gates")
    gate_map: dict[str, str] = {}
    for index, (item, expected_gate_id) in enumerate(zip(gates, HARD_GATE_IDS)):
        row = exact_keys(item, HARD_GATE_KEYS, f"hardGates[{index}]")
        if row["gateId"] != expected_gate_id:
            raise ValidationFailure(f"hardGates[{index}].gateId must be {expected_gate_id}")
        if row["status"] not in {"PASS", "FAIL", "BLOCKED"}:
            raise ValidationFailure(f"hardGates[{index}].status is invalid")
        gate_map[expected_gate_id] = row["status"]
        require_string(row["producer"], f"hardGates[{index}].producer", 100)
        require_string(row["predicate"], f"hardGates[{index}].predicate")
        require_string(row["observed"], f"hardGates[{index}].observed")
        if row["failureCode"] is not None:
            require_string(row["failureCode"], f"hardGates[{index}].failureCode", 160)
        if row["status"] == "PASS" and row["failureCode"] is not None:
            raise ValidationFailure(f"hardGates[{index}] PASS must have failureCode=null")
        if not isinstance(row["inputHashes"], list) or not row["inputHashes"]:
            raise ValidationFailure(f"hardGates[{index}].inputHashes is invalid")
        for hash_index, hash_value in enumerate(row["inputHashes"]):
            require_sha(hash_value, f"hardGates[{index}].inputHashes[{hash_index}]")
        if not isinstance(row["evidenceRefs"], list) or not row["evidenceRefs"]:
            raise ValidationFailure(f"hardGates[{index}].evidenceRefs is invalid")
        for ref_index, ref in enumerate(row["evidenceRefs"]):
            _validate_artifact_ref(ref, f"hardGates[{index}].evidenceRefs[{ref_index}]")
        if expected_gate_id == "HG09-AUDIO":
            audio_sync = recordings.get("audioSync")
            if not isinstance(audio_sync, dict):
                raise ProvenanceFailure(
                    "HG09-AUDIO has no raw recording audio-sync authority"
                )
            expected_audio_gate = {
                "producer": "RECORDING_AV_SYNC_VALIDATOR",
                "predicate": (
                    "FOUR_V1_V4_ACTION_TO_CUE_ONSETS_DERIVED_FROM_RAW_LEDGER_"
                    "AND_48000HZ_WAV_WITHIN_100_MS"
                ),
                "inputHashes": audio_sync["inputHashes"],
                "status": audio_sync["status"],
                "observed": audio_sync["observed"],
                "failureCode": audio_sync["failureCode"],
                "evidenceRefs": audio_sync["evidenceRefs"],
            }
            for field, expected_value in expected_audio_gate.items():
                if row[field] != expected_value:
                    raise ProvenanceFailure(
                        f"HG09-AUDIO {field} is not raw recording-derived"
                    )
    incidents = ledger["incidents"]
    if not isinstance(incidents, list) or len(incidents) > 512:
        raise ValidationFailure("oracle ledger.incidents must contain at most 512 rows")
    incident_ids: set[str] = set()
    for index, item in enumerate(incidents):
        row = exact_keys(item, INCIDENT_KEYS, f"incidents[{index}]")
        key = row["incidentKey"]
        if not isinstance(key, str) or INCIDENT_KEY_PATTERN.fullmatch(key) is None:
            raise ValidationFailure(f"incidents[{index}].incidentKey is invalid")
        if key in incident_ids:
            raise ValidationFailure("oracle ledger contains duplicate incident keys")
        incident_ids.add(key)
        if row["incidentType"] not in {
            "CONFUSION", "RECOVERY_FRICTION", "UX_STALL",
            "EXTERNAL_HINT_ATTEMPT", "HARD_GATE_FAILURE",
        }:
            raise ValidationFailure(f"incidents[{index}].incidentType is invalid")
        require_unique_strings(
            row["actorArtifactIds"],
            f"incidents[{index}].actorArtifactIds",
            minimum=1,
            maximum=3,
        )
        require_unique_strings(
            row["checkpointRefs"],
            f"incidents[{index}].checkpointRefs",
            minimum=1,
            maximum=32,
        )
        verifier_observation_id = row["verifierObservationId"]
        if row["incidentType"] == "HARD_GATE_FAILURE":
            if verifier_observation_id is not None or row["verifierStatus"] != "NOT_APPLICABLE":
                raise ProvenanceFailure(
                    f"incidents[{index}] hard-gate incident cannot claim actor verification"
                )
        elif (
            not isinstance(verifier_observation_id, str)
            or OBSERVATION_ID_PATTERN.fullmatch(verifier_observation_id) is None
        ):
            raise ProvenanceFailure(
                f"incidents[{index}] actor incident lacks verifier observation authority"
            )
        if row["verifierStatus"] not in {"SUPPORTED", "PARTIAL", "UNSUPPORTED", "NOT_APPLICABLE"}:
            raise ValidationFailure(f"incidents[{index}].verifierStatus is invalid")
        if row["oracleStatus"] not in {"EXACT", "MISMATCH", "MISSING", "NOT_APPLICABLE"}:
            raise ValidationFailure(f"incidents[{index}].oracleStatus is invalid")
        if row["capCandidate"] not in {100, 79, 69, 49}:
            raise ValidationFailure(f"incidents[{index}].capCandidate is invalid")
        if type(row["critical"]) is not bool:
            raise ValidationFailure(f"incidents[{index}].critical must be boolean")
        require_string(row["description"], f"incidents[{index}].description")
    if verified_verifier is not None:
        incident_observation_rows = [
            row
            for row in verification_input["observations"]
            if row["claimType"] == "ACTOR_INCIDENT"
        ]
        incident_observations = {
            row["incidentKey"]: row
            for row in incident_observation_rows
        }
        expected_actor_incident_count = sum(
            row["incidentType"] != "HARD_GATE_FAILURE" for row in incidents
        )
        if (
            len(incident_observation_rows) != len(incident_observations)
            or len(incident_observations) != expected_actor_incident_count
        ):
            raise ProvenanceFailure(
                "verifier input does not exactly cover every actor incident"
            )
        verdict_by_id = verified_verifier["verdictByObservationId"]
        for incident in incidents:
            if incident["incidentType"] == "HARD_GATE_FAILURE":
                continue
            observation = incident_observations.get(incident["incidentKey"])
            if observation is None:
                raise ProvenanceFailure(
                    f"incident {incident['incidentKey']} has no verifier input observation"
                )
            observation_id = observation["observationId"]
            if (
                incident["verifierObservationId"] != observation_id
                or incident["verifierStatus"] != verdict_by_id.get(observation_id)
            ):
                raise ProvenanceFailure(
                    f"incident {incident['incidentKey']} verifier status is not output-derived"
                )
    derived_score_bearing_ready = (
        gold_binding.get("derivedReady") is True
        and all(row["status"] != "MISSING" for row in checks)
        and all(status != "BLOCKED" for status in gate_map.values())
    )
    if ledger["scoreBearingReady"] is not derived_score_bearing_ready:
        raise ProvenanceFailure(
            "oracle ledger.scoreBearingReady is not derived from gold/check/gate authorities"
        )
    if provenance["cleanTree"] is False and gate_map["HG13-PROVENANCE"] != "FAIL":
        raise ProvenanceFailure("dirty candidate tree requires HG13-PROVENANCE=FAIL")
    if any(row["status"] in {"MISMATCH", "MISSING"} for row in checks) and all(
        status == "PASS" for status in gate_map.values()
    ):
        raise ValidationFailure("oracle mismatch/missing cannot coexist with all hard gates PASS")
    return {
        "value": ledger,
        "hardGates": gate_map,
        "incidents": incidents,
        "scoreBearingReady": derived_score_bearing_ready,
    }


def validate_verifier(
    value: Any,
    raw_sha256: str,
    candidate: dict[str, Any],
    panel_sha256: str,
    verification_input: dict[str, Any],
) -> dict[str, Any]:
    verifier = exact_keys(value, VERIFIER_KEYS, "native verifier", VerifierValidationFailure)
    provenance = candidate["provenance"]
    if raw_sha256 != provenance["verificationOutputSha256"]:
        raise ProvenanceFailure("verification output file hash does not match candidate provenance")
    expected_identity = {
        "schemaVersion": VERIFICATION_SCHEMA,
        "protocol": PROTOCOL,
        "verifierSlot": "SOL-ULTRA",
        "model": "gpt-5.6-sol",
        "reasoningEffort": "ultra",
        "promptTemplateSha256": provenance["verifierPromptTemplateSha256"],
        "verifierSchemaSha256": provenance["verifierSchemaSha256"],
        "verificationInputSha256": candidate["verificationInputSha256"],
        "evidenceSetSha256": provenance["evidenceSetSha256"],
        "opaqueJudgePanelSha256": panel_sha256,
    }
    for field, expected in expected_identity.items():
        if verifier[field] != expected:
            raise VerifierValidationFailure(
                f"native verifier {field} must be {expected!r}, got {verifier[field]!r}"
            )
    require_string(verifier["verifierRunId"], "native verifier.verifierRunId", 200, VerifierValidationFailure)
    verification_input_schema_sha = require_sha(
        verifier["verificationInputSchemaSha256"],
        "native verifier.verificationInputSchemaSha256",
        VerifierValidationFailure,
    )
    expected_input_schema_sha = file_sha256(
        NATIVE_DIRECTORY / "native-evidence-verification-input.schema.json",
        "checked-in native evidence verification input schema",
    )
    if verification_input_schema_sha != expected_input_schema_sha:
        raise VerifierValidationFailure(
            "native verifier verificationInputSchemaSha256 does not match the checked-in schema"
        )
    rows = verifier["observations"]
    if not isinstance(rows, list) or not 1 <= len(rows) <= 768:
        raise VerifierValidationFailure("native verifier observations must contain 1..768 rows")
    observed_ids: list[str] = []
    verdicts: list[str] = []
    for index, item in enumerate(rows):
        row = exact_keys(item, VERIFIER_ROW_KEYS, f"native verifier observations[{index}]", VerifierValidationFailure)
        observation_id = row["observationId"]
        if not isinstance(observation_id, str) or OBSERVATION_ID_PATTERN.fullmatch(observation_id) is None:
            raise VerifierValidationFailure(f"native verifier observations[{index}].observationId is invalid")
        observed_ids.append(observation_id)
        if row["verdict"] not in {"SUPPORTED", "PARTIAL", "UNSUPPORTED"}:
            raise VerifierValidationFailure(f"native verifier observations[{index}].verdict is invalid")
        verdicts.append(row["verdict"])
        if row["claimType"] not in {"JUDGE_EVIDENCE", "ACTOR_INCIDENT"}:
            raise VerifierValidationFailure(
                f"native verifier observations[{index}].claimType is invalid"
            )
        if row["claimType"] == "ACTOR_INCIDENT":
            if (
                not isinstance(row["incidentKey"], str)
                or INCIDENT_KEY_PATTERN.fullmatch(row["incidentKey"]) is None
            ):
                raise VerifierValidationFailure(
                    f"native verifier observations[{index}].incidentKey is invalid"
                )
        elif row["incidentKey"] is not None:
            raise VerifierValidationFailure(
                f"native verifier observations[{index}] judge claim cannot cite incidentKey"
            )
        sources = row["citedSources"]
        if not isinstance(sources, list) or not 1 <= len(sources) <= 8:
            raise VerifierValidationFailure(f"native verifier observations[{index}].citedSources is invalid")
        seen_sources: set[bytes] = set()
        for source_index, source_item in enumerate(sources):
            source = exact_keys(
                source_item,
                VERIFIER_SOURCE_KEYS,
                f"native verifier observations[{index}].citedSources[{source_index}]",
                VerifierValidationFailure,
            )
            if source["anonymousArtifactId"] not in {
                row["anonymousArtifactId"] for row in candidate["artifactBindings"]
            }:
                raise VerifierValidationFailure("native verifier cites an unknown artifact")
            require_string(source["artifactId"], "native verifier cited artifactId", 200, VerifierValidationFailure)
            require_string(source["locator"], "native verifier cited locator", 300, VerifierValidationFailure)
            canonical = canonical_json_bytes(source)
            if canonical in seen_sources:
                raise VerifierValidationFailure("native verifier row contains duplicate cited sources")
            seen_sources.add(canonical)
        require_string(row["rationale"], f"native verifier observations[{index}].rationale", 1000, VerifierValidationFailure)
    if observed_ids != candidate["expectedObservationIds"]:
        raise VerifierValidationFailure(
            "native verifier observation IDs/order do not match the frozen verification input"
        )
    expected_rows = verification_input["observations"]
    if len(rows) != len(expected_rows):
        raise VerifierValidationFailure(
            "native verifier must cover every verification-input observation exactly once"
        )
    for index, (row, expected_row) in enumerate(zip(rows, expected_rows)):
        if (
            row["observationId"] != expected_row["observationId"]
            or row["claimType"] != expected_row["claimType"]
            or row["incidentKey"] != expected_row["incidentKey"]
            or row["citedSources"] != expected_row["citedSources"]
        ):
            raise VerifierValidationFailure(
                f"native verifier observations[{index}] does not echo exact input ID/citations"
            )
    return {
        "value": verifier,
        "supportedOnly": all(verdict == "SUPPORTED" for verdict in verdicts),
        "verdicts": verdicts,
        "verdictByObservationId": {
            row["observationId"]: row["verdict"] for row in rows
        },
    }


def _verified_cap(
    incident: dict[str, Any],
    incident_evidence: dict[str, dict[str, set[str]]],
) -> int | None:
    if incident["verifierStatus"] != "SUPPORTED" or len(incident["actorArtifactIds"]) < 2:
        return None
    derived = incident_evidence.get(incident["incidentKey"], {})
    severe_actor_ids = derived.get("severeActorIds", set())
    if not set(incident["actorArtifactIds"]) <= severe_actor_ids:
        return None
    kind = incident["incidentType"]
    candidate = incident["capCandidate"]
    if candidate == 49 and kind in {"UX_STALL", "EXTERNAL_HINT_ATTEMPT"} and incident["oracleStatus"] == "EXACT":
        if kind == "UX_STALL" and not set(incident["actorArtifactIds"]) <= derived.get(
            "terminalStalledActorIds", set()
        ):
            return None
        return 49
    if candidate == 69 and kind == "RECOVERY_FRICTION" and incident["oracleStatus"] == "EXACT":
        return 69
    if candidate == 79 and kind == "CONFUSION" and len(incident["checkpointRefs"]) >= 2:
        return 79
    return None


def active_cap_for(
    incidents: list[dict[str, Any]],
    incident_evidence: dict[str, dict[str, set[str]]],
) -> int:
    caps = [
        cap
        for incident in incidents
        if (cap := _verified_cap(incident, incident_evidence)) is not None
    ]
    return min(caps, default=100)


def critical_incidents_for(
    incidents: list[dict[str, Any]],
    incident_evidence: dict[str, dict[str, set[str]]] | None = None,
) -> list[dict[str, Any]]:
    evidence = incident_evidence or {}
    output: list[dict[str, Any]] = []
    for incident in incidents:
        cap = _verified_cap(incident, evidence) or 100
        if not incident["critical"] and cap == 100:
            continue
        if incident["incidentType"] == "HARD_GATE_FAILURE":
            severity = "P0"
        elif cap < 100:
            severity = "P1"
        else:
            severity = "P2"
        output.append({
            "incidentKey": incident["incidentKey"],
            "severity": severity,
            "status": "UNRESOLVED" if incident["critical"] else "VERIFIED",
            "activeCap": cap,
            "evidenceRefs": list(incident["checkpointRefs"]),
        })
    return output


def compute_scores(
    lane_inputs: dict[str, Any],
    rubric: dict[str, Any],
    panel_sha256: str,
    active_cap: int,
    not_reached: set[str],
) -> dict[str, Any]:
    cell_scores: dict[str, Any] = {}
    for category in rubric["categories"]:
        for cell in category["cells"]:
            cell_id = cell["id"]
            row = lane_inputs[cell_id]
            cold = row["cold"]
            coverage = row["coverage"]
            cold_score = cold["coldCellScore"] if cold is not None else None
            cold_spread = cold["coldCellSpread"] if cold is not None else None
            coverage_score = coverage["coverageCellScore"] if coverage is not None else None
            coverage_spread = coverage["coverageCellSpread"] if coverage is not None else None
            state = "NOT_REACHED_BY_PRODUCT" if cell_id in not_reached else "SCORED"
            if state == "NOT_REACHED_BY_PRODUCT":
                cold_score = 0
                cold_spread = 0
                final_score = 0
                final_spread = 0
            elif cold is not None and coverage is not None:
                final_score = min(cold_score, coverage_score)
                final_spread = max(cold_spread, coverage_spread)
            elif cold is not None:
                final_score = cold_score
                final_spread = cold_spread
            elif coverage is not None:
                final_score = coverage_score
                final_spread = coverage_spread
            else:
                raise ValidationFailure(f"cell {cell_id} has no scoring lane")
            cell_scores[cell_id] = {
                "state": state,
                "coldCellScore": None if cold_score is None else rounded(cold_score),
                "coverageCellScore": None if coverage_score is None else rounded(coverage_score),
                "finalCellScore": rounded(final_score),
                "coldCellSpread": None if cold_spread is None else rounded(cold_spread),
                "coverageCellSpread": None if coverage_spread is None else rounded(coverage_spread),
                "finalCellSpread": rounded(final_spread),
                "judgmentPanelSha256": panel_sha256,
            }
    category_scores: dict[str, Any] = {}
    for category in rubric["categories"]:
        score = sum(
            cell["weight"] * cell_scores[cell["id"]]["finalCellScore"]
            for cell in category["cells"]
        ) / 100
        spread = sum(
            cell["weight"] * cell_scores[cell["id"]]["finalCellSpread"]
            for cell in category["cells"]
        ) / 100
        category_scores[category["id"]] = {
            "score": rounded(score),
            "spread": rounded(spread),
            "minimumScore": category["minimumScore"],
            "meetsMinimum": score >= category["minimumScore"],
        }
    raw = sum(
        category["weight"] * category_scores[category["id"]]["score"]
        for category in rubric["categories"]
    ) / 100
    raw_spread = sum(
        category["weight"] * category_scores[category["id"]]["spread"]
        for category in rubric["categories"]
    ) / 100
    penalty = min(
        rubric["aggregation"]["spreadPenaltyMaximum"],
        raw_spread * rubric["aggregation"]["spreadPenaltyMultiplier"],
    )
    pre_cap = raw - penalty
    proxy = max(0.0, min(pre_cap, active_cap))
    return {
        "cellScores": cell_scores,
        "categoryScores": category_scores,
        "rawCommercialUX": rounded(raw),
        "rawSpread": rounded(raw_spread),
        "disagreementPenalty": rounded(penalty),
        "commercialUXProxy": rounded(proxy),
    }


def official_ux_passes(
    commercial_ux_proxy: float,
    category_scores: dict[str, Any],
    cell_scores: dict[str, Any],
    active_cap: int,
    critical_incidents: list[dict[str, Any]],
    difference_report: dict[str, Any],
    *,
    target: float = 87.0,
    required_cell_minimum: float = 70.0,
) -> bool:
    return (
        commercial_ux_proxy >= target
        and all(row["meetsMinimum"] for row in category_scores.values())
        and all(row["finalCellScore"] >= required_cell_minimum for row in cell_scores.values())
        and active_cap == 100
        and not any(row["status"] == "UNRESOLVED" for row in critical_incidents)
        and difference_report["openP0"] == 0
        and difference_report["openP1"] == 0
    )


def _blocked_gate_status(ledger: dict[str, Any]) -> str | None:
    blocked_rows = [row for row in ledger["hardGates"] if row["status"] == "BLOCKED"]
    if not blocked_rows:
        return None
    if any(
        isinstance(row["failureCode"], str) and "HARNESS" in row["failureCode"]
        for row in blocked_rows
    ):
        return "BLOCKED_HARNESS"
    return "BLOCKED_MISSING_EVIDENCE"


def choose_blocker(candidates: Iterable[str | None]) -> str | None:
    present = {row for row in candidates if row is not None}
    for blocker in BLOCKER_PRECEDENCE:
        if blocker in present:
            return blocker
    return None


def _commercial_ux_authority_root() -> Path:
    return _canonical_holdout_registry_path().parent


def _replacement_receipt_path(panel_sha256: str) -> Path:
    require_sha(panel_sha256, "replacement receipt initial panel SHA-256")
    return (
        _commercial_ux_authority_root()
        / "replacement-receipts"
        / (panel_sha256.removeprefix("sha256:") + ".json")
    )


def _panel_finalization_seal_path(
    initial_panel_sha256: str,
    panel_kind: str,
) -> Path:
    require_sha(initial_panel_sha256, "panel finalization initial panel SHA-256")
    if panel_kind not in {"INITIAL", "REPLACEMENT"}:
        raise ValidationFailure("panel finalization kind is invalid")
    return (
        _commercial_ux_authority_root()
        / "panel-finalizations"
        / (
            initial_panel_sha256.removeprefix("sha256:")
            + "-"
            + panel_kind.lower()
            + ".json"
        )
    )


def _holdout_finalization_path(
    holdout_receipt: dict[str, Any],
    panel_kind: str,
) -> Path:
    if panel_kind not in {"INITIAL", "REPLACEMENT"}:
        raise ValidationFailure("native holdout finalization panel kind is invalid")
    registry_path = Path(
        holdout_receipt["value"]["atomicClaim"]["canonicalRegistryPath"]
    )
    return registry_path.parent / (
        ".gridworks-commercial-ux-native-finalization-"
        + holdout_receipt["selfSha256"].removeprefix("sha256:")
        + "-"
        + panel_kind.lower()
        + ".receipt.json"
    )


def _holdout_finalization_bytes(
    holdout_receipt: dict[str, Any],
    evaluation_run: dict[str, Any],
    panel_sha256: str,
    outcome: str,
    scorecard_raw_sha256: str | None,
) -> bytes:
    value = {
        "schemaVersion": "gridworks.commercial-ux.native-finalization-receipt.v1",
        "protocol": PROTOCOL,
        "holdoutConsumptionReceiptSha256": holdout_receipt["selfSha256"],
        "evaluationRunManifestSha256": evaluation_run["selfSha256"],
        "judgePanelSha256": panel_sha256,
        "outcome": outcome,
        "scorecardRawSha256": scorecard_raw_sha256,
    }
    return json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True).encode("utf-8") + b"\n"


def _panel_finalization_seal_bytes(
    seal_path: Path,
    panel_kind: str,
    initial_panel_sha256: str,
    scorecard: dict[str, Any],
    scorecard_path: Path,
    scorecard_raw_sha256: str,
) -> bytes:
    provenance = scorecard["provenance"]
    replacement = panel_kind == "REPLACEMENT"
    value = {
        "schemaVersion": PANEL_FINALIZATION_SEAL_SCHEMA,
        "protocol": PROTOCOL,
        "panelFinalizationSealSha256": "sha256:" + ("0" * 64),
        "panelFinalizationSealSchemaSha256": file_sha256(
            PANEL_FINALIZATION_SEAL_SCHEMA_PATH,
            "checked-in panel finalization seal schema",
        ),
        "claimPolicy": (
            "O_EXCL_SCORECARD_RESERVE_FSYNC_THEN_HOLDOUT_AND_PANEL_SEAL_"
            "FSYNC_THEN_SCORECARD_WRITE_FSYNC"
        ),
        "sealPathRule": "EVALUATION_SESSION_CLAIM.fixedArtifactPaths.panelFinalizationSeal",
        "canonicalSealPath": str(seal_path),
        "panelKind": panel_kind,
        "initialPanelSha256": initial_panel_sha256,
        "judgePanelSha256": scorecard["judgePanelSha256"],
        "scorecardId": scorecard["scorecardId"],
        "recipeId": scorecard["recipeId"],
        "officialCommercialUX": scorecard["officialCommercialUX"],
        "sourceCommit": provenance["sourceCommit"],
        "candidateManifestSha256": provenance["candidateManifestSha256"],
        "candidateManifestRawSha256": provenance["candidateManifestRawSha256"],
        "holdoutConsumptionReceiptSha256": provenance[
            "holdoutConsumptionReceiptSha256"
        ],
        "holdoutConsumptionReceiptRawSha256": provenance[
            "holdoutConsumptionReceiptRawSha256"
        ],
        "goldBindingManifestSha256": provenance["goldBindingManifestSha256"],
        "goldBindingManifestRawSha256": provenance[
            "goldBindingManifestRawSha256"
        ],
        "qualificationReceiptSha256": provenance["qualificationReceiptSha256"],
        "qualificationReceiptRawSha256": provenance[
            "qualificationReceiptRawSha256"
        ],
        "evaluationRunManifestSha256": provenance[
            "evaluationRunManifestSha256"
        ],
        "evaluationRunManifestRawSha256": provenance[
            "evaluationRunManifestRawSha256"
        ],
        "anonymizationManifestSha256": provenance["anonymizationManifestSha256"],
        "anonymizationManifestRawSha256": provenance[
            "anonymizationManifestRawSha256"
        ],
        "evidenceSetSha256": provenance["evidenceSetSha256"],
        "evidenceSetRawSha256": provenance["evidenceSetRawSha256"],
        "sanitizedEvidenceBundleManifestSha256": provenance[
            "sanitizedEvidenceBundleManifestSha256"
        ],
        "sanitizedEvidenceBundleManifestRawSha256": provenance[
            "sanitizedEvidenceBundleManifestRawSha256"
        ],
        "sanitizedEvidenceContentRootSha256": provenance[
            "sanitizedEvidenceContentRootSha256"
        ],
        "verificationInputSha256": scorecard["verificationInputSha256"],
        "verificationInputRawSha256": provenance["verificationInputRawSha256"],
        "aggregationInputRawSha256": provenance["aggregationInputRawSha256"],
        "evaluationSessionClaimSha256": provenance[
            "evaluationSessionClaimSha256"
        ],
        "evaluationSessionClaimRawSha256": provenance[
            "evaluationSessionClaimRawSha256"
        ],
        "evaluationSessionPolicySha256": provenance[
            "evaluationSessionPolicySha256"
        ],
        "evaluationSessionClaimToolSha256": provenance[
            "evaluationSessionClaimToolSha256"
        ],
        "evaluationSessionId": provenance["evaluationSessionId"],
        "evaluationSessionMode": provenance["evaluationSessionMode"],
        "evaluationAttemptAuditSha256": provenance[
            "evaluationAttemptAuditSha256"
        ],
        "evaluationSelectedAttemptsSha256": provenance[
            "evaluationSelectedAttemptsSha256"
        ],
        "scorecardPath": str(scorecard_path.resolve(strict=False)),
        "scorecardRawSha256": scorecard_raw_sha256,
        "scorecardStatus": scorecard["status"],
        "replacementReceiptPath": (
            scorecard["replacementReceiptPath"] if replacement else None
        ),
        "replacementReceiptRawSha256": (
            scorecard["replacementReceiptSha256"] if replacement else None
        ),
        "sealStatus": "FINALIZED",
    }
    value["panelFinalizationSealSha256"] = self_sha256(
        value,
        "panelFinalizationSealSha256",
        "panel finalization seal",
    )
    validate_panel_finalization_seal_schema(value)
    return canonical_json_bytes(value) + b"\n"


def _validate_initial_for_replacement(
    path: Path,
    rubric: dict[str, Any],
) -> tuple[dict[str, Any], bytes, Path, dict[str, Any]]:
    resolved = path.resolve(strict=True)
    value, raw = read_json_bytes(resolved, "initial native aggregate")
    initial = exact_keys(value, OUTPUT_KEYS, "initial native aggregate")
    validate_internal_scorecard(initial)
    if initial["panelKind"] != "INITIAL" or not initial["rerunRequired"]:
        raise ValidationFailure("replacement source must be an INITIAL rerun-required aggregate")
    if initial["status"] not in {
        "RERUN_REQUIRED_COLD_INSTABILITY",
        "RERUN_REQUIRED_COVERAGE_INSTABILITY",
        "RERUN_REQUIRED_PANEL_INSTABILITY",
    }:
        raise ValidationFailure("replacement source status is not rerun-required")
    if initial["provenance"]["rubricSha256"] != rubric["sha256"]:
        raise ProvenanceFailure("replacement rubric differs from initial aggregate")
    required_lanes = initial["replacementRequiredLanes"]
    if not isinstance(required_lanes, list) or not required_lanes:
        raise ValidationFailure("initial replacementRequiredLanes is invalid")
    receipt_path = Path(initial["replacementReceiptPath"])
    expected_path = _replacement_receipt_path(initial["judgePanelSha256"])
    if receipt_path != receipt_path.resolve(strict=False) or receipt_path != expected_path:
        raise ProvenanceFailure("initial replacement receipt path is not canonical/content-addressed")
    seal_path = resolved.parent / "panel-finalization-seal.json"
    seal_value, seal_raw = read_json_bytes(
        seal_path.resolve(strict=True),
        "initial panel finalization seal",
    )
    seal = validate_self_hashed_envelope(
        seal_value,
        seal_raw,
        schema_version=PANEL_FINALIZATION_SEAL_SCHEMA,
        self_field="panelFinalizationSealSha256",
        schema_validator=validate_panel_finalization_seal_schema,
        label="initial panel finalization seal",
    )
    provenance = initial["provenance"]
    expected_seal = {
        "panelFinalizationSealSchemaSha256": file_sha256(
            PANEL_FINALIZATION_SEAL_SCHEMA_PATH,
            "checked-in panel finalization seal schema",
        ),
        "claimPolicy": (
            "O_EXCL_SCORECARD_RESERVE_FSYNC_THEN_HOLDOUT_AND_PANEL_SEAL_"
            "FSYNC_THEN_SCORECARD_WRITE_FSYNC"
        ),
        "sealPathRule": "EVALUATION_SESSION_CLAIM.fixedArtifactPaths.panelFinalizationSeal",
        "canonicalSealPath": str(seal_path),
        "panelKind": "INITIAL",
        "initialPanelSha256": initial["judgePanelSha256"],
        "judgePanelSha256": initial["judgePanelSha256"],
        "scorecardId": initial["scorecardId"],
        "recipeId": initial["recipeId"],
        "officialCommercialUX": initial["officialCommercialUX"],
        "sourceCommit": provenance["sourceCommit"],
        "candidateManifestSha256": provenance["candidateManifestSha256"],
        "candidateManifestRawSha256": provenance["candidateManifestRawSha256"],
        "holdoutConsumptionReceiptSha256": provenance[
            "holdoutConsumptionReceiptSha256"
        ],
        "holdoutConsumptionReceiptRawSha256": provenance[
            "holdoutConsumptionReceiptRawSha256"
        ],
        "goldBindingManifestSha256": provenance["goldBindingManifestSha256"],
        "goldBindingManifestRawSha256": provenance[
            "goldBindingManifestRawSha256"
        ],
        "qualificationReceiptSha256": provenance["qualificationReceiptSha256"],
        "qualificationReceiptRawSha256": provenance[
            "qualificationReceiptRawSha256"
        ],
        "evaluationRunManifestSha256": provenance[
            "evaluationRunManifestSha256"
        ],
        "evaluationRunManifestRawSha256": provenance[
            "evaluationRunManifestRawSha256"
        ],
        "anonymizationManifestSha256": provenance["anonymizationManifestSha256"],
        "anonymizationManifestRawSha256": provenance[
            "anonymizationManifestRawSha256"
        ],
        "evidenceSetSha256": provenance["evidenceSetSha256"],
        "evidenceSetRawSha256": provenance["evidenceSetRawSha256"],
        "sanitizedEvidenceBundleManifestSha256": provenance[
            "sanitizedEvidenceBundleManifestSha256"
        ],
        "sanitizedEvidenceBundleManifestRawSha256": provenance[
            "sanitizedEvidenceBundleManifestRawSha256"
        ],
        "sanitizedEvidenceContentRootSha256": provenance[
            "sanitizedEvidenceContentRootSha256"
        ],
        "verificationInputSha256": initial["verificationInputSha256"],
        "verificationInputRawSha256": provenance["verificationInputRawSha256"],
        "aggregationInputRawSha256": provenance["aggregationInputRawSha256"],
        "evaluationSessionClaimSha256": provenance[
            "evaluationSessionClaimSha256"
        ],
        "evaluationSessionClaimRawSha256": provenance[
            "evaluationSessionClaimRawSha256"
        ],
        "evaluationSessionPolicySha256": provenance[
            "evaluationSessionPolicySha256"
        ],
        "evaluationSessionClaimToolSha256": provenance[
            "evaluationSessionClaimToolSha256"
        ],
        "evaluationSessionId": provenance["evaluationSessionId"],
        "evaluationSessionMode": provenance["evaluationSessionMode"],
        "evaluationAttemptAuditSha256": provenance[
            "evaluationAttemptAuditSha256"
        ],
        "evaluationSelectedAttemptsSha256": provenance[
            "evaluationSelectedAttemptsSha256"
        ],
        "scorecardPath": str(resolved),
        "scorecardRawSha256": bytes_sha256(raw),
        "scorecardStatus": initial["status"],
        "replacementReceiptPath": None,
        "replacementReceiptRawSha256": None,
        "sealStatus": "FINALIZED",
    }
    for field, expected_value in expected_seal.items():
        if seal["value"][field] != expected_value:
            raise ProvenanceFailure(
                f"initial panel finalization seal {field} mismatch"
            )
    return initial, raw, receipt_path, seal


def _validate_replacement_candidate_stability(
    initial: dict[str, Any],
    candidate: dict[str, Any],
    rubric: dict[str, Any],
) -> None:
    if initial["recipeId"] != candidate["recipeId"]:
        raise ProvenanceFailure("replacement recipeId differs from the initial aggregate")
    if initial["scorecardId"] != candidate["scorecardId"]:
        raise ProvenanceFailure("replacement scorecardId differs from the initial aggregate")
    stable_fields = {
        "candidateManifestSha256",
        "candidateManifestRawSha256",
        "qualificationReceiptSha256",
        "qualificationReceiptRawSha256",
        "holdoutConsumptionReceiptSha256",
        "holdoutConsumptionReceiptRawSha256",
        "goldBindingManifestSha256",
        "goldBindingManifestRawSha256",
        "sourceCommit",
        "model",
        "reasoningEffort",
        "promptTemplateSha256",
        "judgmentSchemaSha256",
        "rubricSha256",
        "coldRecipeSha256",
        "coverageRecipeSha256",
        "holdoutRecipeSha256",
        "nativeAggregatorSha256",
        "executionArtifactSha256",
        "packageSha256",
        "packageStatus",
    }
    for field in stable_fields:
        if initial["provenance"][field] != candidate["provenance"][field]:
            raise ProvenanceFailure(f"replacement provenance field {field} changed")
    if initial["provenance"]["rubricSha256"] != rubric["sha256"]:
        raise ProvenanceFailure("replacement rubric differs from initial aggregate")


def _validate_replacement_artifact_freshness(
    initial: dict[str, Any],
    candidate: dict[str, Any],
    new_execution_identities: dict[str, Any],
) -> None:
    required_lanes = initial["replacementRequiredLanes"]
    old_bindings = initial["panelArtifactBindings"]
    old_cold = {
        row["artifactSha256"]
        for row in old_bindings
        if row["artifactKind"] == "COLD_ACTOR"
    }
    new_cold = {
        row["artifactSha256"]
        for row in candidate["artifactBindings"]
        if row["artifactKind"] == "COLD_ACTOR"
    }
    old_coverage = next(
        row["artifactSha256"] for row in old_bindings if row["artifactKind"] == "COVERAGE"
    )
    new_coverage = next(
        row["artifactSha256"] for row in candidate["artifactBindings"] if row["artifactKind"] == "COVERAGE"
    )
    old_response_self = initial["provenance"]["coldActorResponseSha256"]
    new_response_self = candidate["provenance"]["coldActorResponseSha256"]
    old_response_raw = initial["provenance"]["coldActorResponseRawSha256"]
    new_response_raw = candidate["provenance"]["coldActorResponseRawSha256"]
    if "COLD-JOURNEY" in required_lanes and old_cold & new_cold:
        raise ProvenanceFailure("cold replacement must use three fresh actor artifacts")
    if "COLD-JOURNEY" in required_lanes and (
        set(old_response_self) & set(new_response_self)
        or set(old_response_raw) & set(new_response_raw)
    ):
        raise ProvenanceFailure(
            "cold replacement must use three fresh cold actor response transports"
        )
    if "COVERAGE-JOURNEY" in required_lanes and old_coverage == new_coverage:
        raise ProvenanceFailure("coverage replacement must use a fresh coverage artifact")
    if "COLD-JOURNEY" not in required_lanes and old_cold != new_cold:
        raise ProvenanceFailure("coverage-only replacement must preserve all cold artifacts")
    if "COLD-JOURNEY" not in required_lanes and (
        old_response_self != new_response_self
        or old_response_raw != new_response_raw
    ):
        raise ProvenanceFailure(
            "coverage-only replacement must preserve cold actor response transports"
        )
    if "COVERAGE-JOURNEY" not in required_lanes and old_coverage != new_coverage:
        raise ProvenanceFailure("cold-only replacement must preserve the coverage artifact")
    old_execution_identities = initial["provenance"]["laneExecutionIdentities"]

    def require_lane_fresh(
        old_rows: list[dict[str, Any]],
        new_rows: list[dict[str, Any]],
        fields: tuple[str, ...],
        label: str,
    ) -> None:
        for field in fields:
            # Nullable execution components (currently only actor save state)
            # carry no reusable identity when absent.  Every concrete value is
            # nevertheless part of the replacement-disjointness authority.
            old_values = {row[field] for row in old_rows if row[field] is not None}
            new_values = {row[field] for row in new_rows if row[field] is not None}
            if old_values & new_values:
                raise ProvenanceFailure(
                    f"{label} replacement execution identity {field} must be disjoint"
                )

    cold_fields = (
        "actorRunId",
        "processTreeId",
        "userDataSha256",
        "saveSha256",
        "journalSha256",
        "recordingManifestSha256",
        "recordingManifestRawSha256",
        "recordingContentRootSha256",
        "canonicalRecordingRoot",
    )
    coverage_fields = (
        "coverageRunId",
        "processTreeId",
        "userDataSha256",
        "journalBundleSha256",
        "recordingManifestSha256",
        "recordingManifestRawSha256",
        "recordingContentRootSha256",
        "canonicalRecordingRoot",
    )
    if "COLD-JOURNEY" in required_lanes:
        require_lane_fresh(
            old_execution_identities["cold"],
            new_execution_identities["cold"],
            cold_fields,
            "cold",
        )
    elif old_execution_identities["cold"] != new_execution_identities["cold"]:
        raise ProvenanceFailure(
            "coverage-only replacement must preserve exact cold execution identities"
        )
    if "COVERAGE-JOURNEY" in required_lanes:
        require_lane_fresh(
            [old_execution_identities["coverage"]],
            [new_execution_identities["coverage"]],
            coverage_fields,
            "coverage",
        )
    elif old_execution_identities["coverage"] != new_execution_identities["coverage"]:
        raise ProvenanceFailure(
            "cold-only replacement must preserve exact coverage execution identity"
        )


def _receipt_bytes(
    receipt_path: Path,
    initial_path: Path,
    initial_bytes: bytes,
    initial: dict[str, Any],
    initial_seal: dict[str, Any],
    candidate: dict[str, Any],
    rubric: dict[str, Any],
    raw_aggregation_input_sha256: str,
    raw_candidate_manifest_sha256: str,
    raw_qualification_receipt_sha256: str,
    qualification_receipt_sha256: str,
    raw_evaluation_run_manifest_sha256: str,
    evaluation_run_manifest_sha256: str,
    panel_attempt: dict[str, Any],
    judgment_attempts: list[dict[str, Any]],
    attempt_outcome: str,
    failure_code: str | None,
    parsed_panel_sha256: str | None,
    parsed_run_ids: list[str],
) -> tuple[bytes, str]:
    attempts = [panel_attempt, *judgment_attempts]
    derived_outcome, derived_failure_code = _attempt_failure(attempts)
    if attempt_outcome != derived_outcome:
        raise ValidationFailure("replacement receipt aggregate outcome is not attempt-derived")
    if failure_code != derived_failure_code:
        raise ValidationFailure("replacement receipt failureCode is not attempt-derived")

    def receipt_attempt(attempt: dict[str, Any]) -> dict[str, Any]:
        return {
            "slotId": attempt["slotId"],
            "path": attempt["resolvedPath"],
            "readStatus": attempt["readStatus"],
            "rawSha256": attempt["rawSha256"],
            "attemptOutcome": attempt["attemptOutcome"],
            "failureCode": attempt["failureCode"],
        }

    receipt = {
        "schemaVersion": RECEIPT_SCHEMA,
        "protocol": PROTOCOL,
        "claimPolicy": (
            "VERIFIED_INITIAL_FINALIZATION_SEAL_THEN_O_EXCL_BEFORE_ATTEMPT_"
            "READ_AND_FINALIZE_SAME_DESCRIPTOR"
        ),
        "authorityPreflightStatus": "EXACT_BEFORE_CLAIM",
        "replacementReceiptPathRule": (
            "GIT_COMMON_DIR/gridworks-commercial-ux/replacement-receipts/"
            "{initialPanelSha256Hex}.json"
        ),
        "replacementReceiptPath": str(receipt_path),
        "initialAggregatePath": str(initial_path.resolve(strict=True)),
        "initialAggregateRawSha256": bytes_sha256(initial_bytes),
        "initialPanelFinalizationSealPath": initial_seal["value"][
            "canonicalSealPath"
        ],
        "initialPanelFinalizationSealSha256": initial_seal["selfSha256"],
        "initialPanelFinalizationSealRawSha256": initial_seal["rawSha256"],
        "initialPanelSha256": initial["judgePanelSha256"],
        "initialEvaluationRunManifestSha256": initial["provenance"]["evaluationRunManifestSha256"],
        "replacementRequiredLanes": initial["replacementRequiredLanes"],
        "candidateManifestSha256": initial["provenance"]["candidateManifestSha256"],
        "qualificationReceiptSha256": qualification_receipt_sha256,
        "evaluationRunManifestSha256": evaluation_run_manifest_sha256,
        "recipeId": initial["recipeId"],
        "holdoutConsumptionReceiptSha256": candidate["provenance"][
            "holdoutConsumptionReceiptSha256"
        ],
        "holdoutConsumptionReceiptRawSha256": candidate["provenance"][
            "holdoutConsumptionReceiptRawSha256"
        ],
        "goldBindingManifestSha256": candidate["provenance"][
            "goldBindingManifestSha256"
        ],
        "goldBindingManifestRawSha256": candidate["provenance"][
            "goldBindingManifestRawSha256"
        ],
        "evidenceSetSha256": candidate["provenance"]["evidenceSetSha256"],
        "evidenceSetRawSha256": candidate["provenance"][
            "evidenceSetRawSha256"
        ],
        "sanitizedEvidenceBundleManifestSha256": candidate["provenance"][
            "sanitizedEvidenceBundleManifestSha256"
        ],
        "sanitizedEvidenceBundleManifestRawSha256": candidate["provenance"][
            "sanitizedEvidenceBundleManifestRawSha256"
        ],
        "sanitizedEvidenceContentRootSha256": candidate["provenance"][
            "sanitizedEvidenceContentRootSha256"
        ],
        "rubricSha256": rubric["sha256"],
        "promptTemplateSha256": initial["provenance"]["promptTemplateSha256"],
        "judgmentSchemaSha256": initial["provenance"]["judgmentSchemaSha256"],
        "rawAggregationInputSha256": raw_aggregation_input_sha256,
        "rawCandidateManifestSha256": raw_candidate_manifest_sha256,
        "rawQualificationReceiptSha256": raw_qualification_receipt_sha256,
        "rawEvaluationRunManifestSha256": raw_evaluation_run_manifest_sha256,
        "panelAttempt": receipt_attempt(panel_attempt),
        "judgmentAttempts": [receipt_attempt(row) for row in judgment_attempts],
        "attemptOutcome": attempt_outcome,
        "failureCode": failure_code,
        "parsedReplacementPanelSha256": parsed_panel_sha256,
        "parsedJudgeRunIds": parsed_run_ids,
        "slotConsumed": True,
    }
    validate_native_replacement_receipt_schema(receipt)
    raw = canonical_json_bytes(receipt) + b"\n"
    return raw, bytes_sha256(raw)


def _reserve_receipt(path: Path) -> int:
    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    except FileExistsError as exception:
        raise ValidationFailure(f"native replacement was already consumed: {path}") from exception
    _fsync_parent_directory(path)
    return descriptor


def _reserve_holdout_finalization(path: Path) -> int:
    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    except FileExistsError as exception:
        raise ValidationFailure(
            f"native holdout receipt/panel was already finalized: {path}"
        ) from exception
    _fsync_parent_directory(path)
    return descriptor


def _fsync_parent_directory(path: Path) -> None:
    try:
        descriptor = os.open(path.parent, os.O_RDONLY)
        try:
            os.fsync(descriptor)
        finally:
            os.close(descriptor)
    except OSError as exception:
        raise ValidationFailure(
            f"native authority parent directory could not be fsynced: {path.parent}: {exception}"
        ) from exception


def _finalize_reserved_receipt(descriptor: int, path: Path, content: bytes) -> None:
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
    except OSError as exception:
        raise ValidationFailure(
            f"native replacement receipt claim was created but incomplete: {path}: {exception}"
        ) from exception
    _fsync_parent_directory(path)


def _reserve_scorecard_output(path: Path, replacement_claimed: bool) -> int:
    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    except FileExistsError as exception:
        detail = " after replacement receipt claim" if replacement_claimed else ""
        raise ValidationFailure(f"native aggregate output already exists{detail}: {path}") from exception
    _fsync_parent_directory(path)
    return descriptor


def _close_reserved_scorecard_output(descriptor: int, path: Path) -> None:
    # This helper is only used on a failed aggregation/finalization path.  The
    # reserved output must remain a schema-invalid tombstone even if the final
    # writer managed to place a complete PASS document before reporting an
    # fsync error.  Prefer the still-open reserved descriptor, then fall back
    # to the already-reserved path when the writer closed it while unwinding.
    try:
        os.ftruncate(descriptor, 0)
        os.fsync(descriptor)
    except OSError:
        try:
            recovery_descriptor = os.open(path, os.O_WRONLY | os.O_TRUNC)
            try:
                os.fsync(recovery_descriptor)
            finally:
                os.close(recovery_descriptor)
        except OSError:
            pass
    try:
        os.close(descriptor)
    except OSError:
        pass
    try:
        _fsync_parent_directory(path)
    except ValidationFailure:
        pass


def _finalize_reserved_scorecard_output(
    descriptor: int,
    path: Path,
    content: bytes,
) -> None:
    try:
        offset = 0
        while offset < len(content):
            written = os.write(descriptor, content[offset:])
            if written <= 0:
                raise OSError("scorecard descriptor made no write progress")
            offset += written
        os.fsync(descriptor)
    except OSError as exception:
        raise ValidationFailure(f"native aggregate output could not be completed: {path}: {exception}") from exception
    # Keep the descriptor open until directory durability succeeds.  If that
    # final step faults, the caller still owns the exact O_EXCL descriptor and
    # can atomically truncate the would-be PASS back to its reserved tombstone.
    _fsync_parent_directory(path)
    try:
        os.close(descriptor)
    except OSError as exception:
        raise ValidationFailure(
            f"native aggregate output descriptor could not be closed: {path}: {exception}"
        ) from exception


def _base_output(
    candidate: dict[str, Any],
    rubric: dict[str, Any],
    ledger: dict[str, Any],
    qualification_status: str,
    panel_kind: str,
    panel_sha256: str,
    run_ids: list[str],
    artifact_bindings: list[dict[str, str]],
    lane_inputs: dict[str, Any],
    unstable: list[str],
    incident_evidence: dict[str, dict[str, set[str]]],
    aggregation_input_raw_sha256: str,
    output_path: Path,
    replacement_for_panel: str | None,
    lane_execution_identities: dict[str, Any],
) -> dict[str, Any]:
    receipt_path = _replacement_receipt_path(panel_sha256)
    provenance = copy.deepcopy(candidate["provenance"])
    provenance["judgePanelSha256"] = panel_sha256
    provenance["aggregationInputRawSha256"] = aggregation_input_raw_sha256
    provenance["verificationInputSha256"] = candidate["verificationInputSha256"]
    provenance["laneExecutionIdentities"] = copy.deepcopy(
        lane_execution_identities
    )
    hard_gates = dict(ledger["hardGates"])
    return {
        "schemaVersion": SCORECARD_SCHEMA,
        "protocol": PROTOCOL,
        "metric": "CommercialUXProxy",
        "scorecardId": candidate["scorecardId"],
        "recipeId": candidate["recipeId"],
        "officialCommercialUX": candidate["recipeId"].startswith("HOLDOUT-"),
        "status": None,
        "verdict": None,
        "commercialUXProxy": None,
        "rawCommercialUX": None,
        "rawSpread": None,
        "disagreementPenalty": None,
        "activeCap": None,
        "cellScores": None,
        "categoryScores": None,
        "hardGates": hard_gates,
        "criticalIncidents": critical_incidents_for(ledger["incidents"], incident_evidence),
        "differenceReport": copy.deepcopy(candidate["differenceReport"]),
        "qualificationStatus": qualification_status,
        "evidenceVerificationStatus": "NOT_EVALUATED",
        "stabilityStatus": "STABLE" if not unstable else "RERUN_REQUIRED",
        "provenance": provenance,
        "humanValidationStatus": "NOT_COLLECTED",
        "panelKind": panel_kind,
        "judgeRunIds": list(run_ids),
        "judgePanelSha256": panel_sha256,
        "panelArtifactBindings": copy.deepcopy(artifact_bindings),
        "laneInputs": copy.deepcopy(lane_inputs),
        "unstableLanes": list(unstable),
        "replacementRequiredLanes": list(unstable) if panel_kind == "INITIAL" else [],
        "rerunRequired": bool(unstable and panel_kind == "INITIAL"),
        "replacementForPanelSha256": replacement_for_panel,
        "replacementReceiptPath": str(receipt_path),
        "replacementReceiptSha256": None,
        "verificationInputSha256": candidate["verificationInputSha256"],
    }


def _blocked_output(base: dict[str, Any], blocker: str) -> dict[str, Any]:
    base["status"] = blocker
    base["verdict"] = blocker
    base["commercialUXProxy"] = None
    base["rawCommercialUX"] = None
    base["rawSpread"] = None
    base["disagreementPenalty"] = None
    base["activeCap"] = None
    base["cellScores"] = None
    base["categoryScores"] = None
    if blocker == "BLOCKED_JUDGE_INSTABILITY":
        base["stabilityStatus"] = blocker
    if blocker == "BLOCKED_EVIDENCE_VERIFICATION":
        base["evidenceVerificationStatus"] = blocker
    return base


def _schema_blocked_output(
    candidate: dict[str, Any],
    rubric: dict[str, Any],
    ledger: dict[str, Any],
    qualification_status: str,
    judgment_attempts: list[dict[str, Any]],
    output_path: Path,
    panel_kind: str,
    incident_evidence: dict[str, dict[str, set[str]]],
    aggregation_input_raw_sha256: str,
    replacement_for_panel_sha256: str | None,
    receipt_path: Path | None,
    receipt_sha256: str | None,
    lane_execution_identities: dict[str, Any],
) -> dict[str, Any]:
    panel_sha = candidate["provenance"]["judgePanelSha256"]
    run_ids: list[str] = []
    for attempt in judgment_attempts:
        row = attempt["value"]
        run_id = row.get("judgeRunId") if isinstance(row, dict) else None
        if isinstance(run_id, str) and run_id not in run_ids and len(run_ids) < 3:
            run_ids.append(run_id)
    base = _base_output(
        candidate,
        rubric,
        ledger,
        qualification_status,
        panel_kind,
        panel_sha,
        run_ids,
        candidate["artifactBindings"],
        {},
        [],
        incident_evidence,
        aggregation_input_raw_sha256,
        output_path,
        replacement_for_panel_sha256,
        lane_execution_identities,
    )
    if receipt_path is not None:
        base["replacementReceiptPath"] = str(receipt_path)
        base["replacementReceiptSha256"] = receipt_sha256
        base["replacementRequiredLanes"] = []
        base["rerunRequired"] = False
    return _blocked_output(base, "BLOCKED_JUDGE_SCHEMA")


def _validate_evaluation_replacement_authority(
    evaluation_authority: dict[str, Any],
    replacement_context: dict[str, Any] | None,
) -> None:
    authority = evaluation_authority["value"]["replacementAuthority"]
    if replacement_context is None:
        if authority != {
            "panelKind": "INITIAL",
            "initialScorecardRawSha256": None,
            "initialJudgePanelSha256": None,
            "initialEvaluationRunManifestSha256": None,
            "requiredLanes": [],
        }:
            raise ProvenanceFailure("initial evaluation run has replacement authority")
        return
    initial = replacement_context["initial"]
    expected = {
        "panelKind": "REPLACEMENT",
        "initialScorecardRawSha256": bytes_sha256(replacement_context["initialBytes"]),
        "initialJudgePanelSha256": initial["judgePanelSha256"],
        "initialEvaluationRunManifestSha256": initial["provenance"]["evaluationRunManifestSha256"],
        "requiredLanes": initial["replacementRequiredLanes"],
    }
    if authority != expected:
        raise ProvenanceFailure("evaluation-run replacement authority does not match initial scorecard")


def _validate_panel_replacement_linkage(
    panel_authority: dict[str, Any],
    evaluation_authority: dict[str, Any],
    replacement_context: dict[str, Any] | None,
    candidate: dict[str, Any],
) -> None:
    panel = panel_authority["value"]
    if replacement_context is None:
        if panel["panelKind"] != "INITIAL":
            raise ProvenanceFailure("initial aggregate requires an INITIAL judge panel")
        return
    initial = replacement_context["initial"]
    if panel["panelKind"] != "REPLACEMENT":
        raise ProvenanceFailure("replacement aggregate requires a REPLACEMENT judge panel")
    if panel["changedLanes"] != initial["replacementRequiredLanes"]:
        raise ProvenanceFailure("replacement judge panel changedLanes mismatch")
    if panel["replacementForPanelSha256"] != initial["judgePanelSha256"]:
        raise ProvenanceFailure("replacement judge panel initial panel linkage mismatch")
    if panel["replacementAuthoritySha256"] != bytes_sha256(
        replacement_context["initialBytes"]
    ):
        raise ProvenanceFailure("replacement judge panel initial scorecard authority mismatch")
    overlap = sorted(set(initial["judgeRunIds"]) & set(panel_authority["runIds"]))
    if overlap:
        raise ProvenanceFailure(f"replacement judge runs are not fresh: {overlap}")


def validate_retry_attribution(
    evaluation_run: dict[str, Any],
    panel_attempt: dict[str, Any],
    judgment_attempts: list[dict[str, Any]],
) -> None:
    failed_attempts = [
        row
        for row in [panel_attempt, *judgment_attempts]
        if row["attemptOutcome"] != "VALID"
    ]
    if not failed_attempts:
        return
    blocked_retry_rows = [
        row
        for row in evaluation_run["retryLedger"]
        if row["role"] in {"JUDGE", "REPLACEMENT"}
        and row["outcome"] == "BLOCKED"
        and row["reason"] in {"SCHEMA", "TRANSPORT", "INPUT_UNREADABLE"}
    ]
    matched_rows: set[int] = set()
    for attempt in failed_attempts:
        outcome = attempt["attemptOutcome"]
        reason = {
            "SCHEMA_FAILURE": "SCHEMA",
            "TRANSPORT_FAILURE": "TRANSPORT",
            "INPUT_UNREADABLE": "INPUT_UNREADABLE",
        }[outcome]
        matches = [
            (index, row)
            for index, row in enumerate(blocked_retry_rows)
            if row["runSlot"] == attempt["slotId"]
            and row["reason"] == reason
            and row["readStatus"] == attempt["readStatus"]
            and row["rawArtifactSha256"] == attempt["rawSha256"]
        ]
        if len(matches) != 1:
            raise ProvenanceFailure(
                f"{attempt['slotId']} {outcome} requires exactly one exact BLOCKED retry row"
            )
        matched_rows.add(matches[0][0])
    if len(matched_rows) != len(blocked_retry_rows):
        raise ProvenanceFailure("retry ledger contains a BLOCKED row for no failed input slot")


def validate_checked_in_schema(value: Any, schema_path: Path, label: str) -> None:
    """Validate one value against a checked-in, read-only native schema.

    The repository's isolated contract validator deliberately has no third-party
    JSON Schema dependency.  Reusing its Draft 2020-12 subset here keeps the
    score-producing path and ``validate-contract.py`` on the same interpretation
    of the frozen schema instead of maintaining a second schema implementation.
    """

    schema, _ = read_json_bytes(schema_path, f"{label} schema")
    if not isinstance(schema, dict):
        raise ValidationFailure(f"{label} schema must be an object")
    spec = importlib.util.spec_from_file_location(
        "gridworks_commercial_ux_native_contract_validator",
        CONTRACT_VALIDATOR_PATH,
    )
    if spec is None or spec.loader is None:
        raise ValidationFailure("native contract validator could not be loaded")
    validator = importlib.util.module_from_spec(spec)
    try:
        spec.loader.exec_module(validator)
    except (ImportError, OSError, SyntaxError) as exception:
        raise ValidationFailure(
            f"native contract validator could not be loaded: {exception}"
        ) from exception
    instance_errors = getattr(validator, "instance_errors", None)
    if not callable(instance_errors):
        raise ValidationFailure("native contract validator has no instance_errors function")
    try:
        errors = instance_errors(value, schema)
    except Exception as exception:
        raise ValidationFailure(
            f"{label} schema validation could not run: {exception}"
        ) from exception
    if errors:
        excerpt = "; ".join(str(error) for error in errors[:8])
        suffix = f"; and {len(errors) - 8} more" if len(errors) > 8 else ""
        raise ValidationFailure(
            f"{label} does not satisfy {schema_path.name}: "
            f"{excerpt}{suffix}"
        )


def validate_native_aggregation_input_schema(value: Any) -> None:
    validate_checked_in_schema(
        value,
        AGGREGATION_INPUT_SCHEMA_PATH,
        "native aggregation input",
    )


def validate_candidate_manifest_schema(value: Any) -> None:
    validate_checked_in_schema(value, CANDIDATE_MANIFEST_SCHEMA_PATH, "candidate manifest")


def validate_qualification_receipt_schema(value: Any) -> None:
    validate_checked_in_schema(
        value,
        QUALIFICATION_RECEIPT_SCHEMA_PATH,
        "qualification receipt",
    )


def validate_judge_panel_manifest_schema(value: Any) -> None:
    validate_checked_in_schema(value, JUDGE_PANEL_SCHEMA_PATH, "judge panel manifest")


def validate_evaluation_run_schema(value: Any) -> None:
    validate_checked_in_schema(value, EVALUATION_RUN_SCHEMA_PATH, "evaluation run manifest")


def validate_actor_observation_schema(value: Any) -> None:
    validate_checked_in_schema(value, ACTOR_OBSERVATION_SCHEMA_PATH, "actor observation")


def validate_cold_actor_response_schema(value: Any) -> None:
    validate_checked_in_schema(
        value,
        COLD_ACTOR_RESPONSE_SCHEMA_PATH,
        "cold actor response",
    )


def validate_actor_action_ledger_schema(value: Any) -> None:
    validate_checked_in_schema(
        value,
        ACTOR_ACTION_LEDGER_SCHEMA_PATH,
        "actor action ledger",
    )


def validate_actor_trace_schema(value: Any) -> None:
    validate_checked_in_schema(value, ACTOR_TRACE_SCHEMA_PATH, "actor trace")


def validate_anonymization_manifest_schema(value: Any) -> None:
    validate_checked_in_schema(
        value,
        ANONYMIZATION_MANIFEST_SCHEMA_PATH,
        "anonymization manifest",
    )


def validate_coverage_trace_schema(value: Any) -> None:
    validate_checked_in_schema(value, COVERAGE_TRACE_SCHEMA_PATH, "coverage trace")


def validate_evidence_set_schema(value: Any) -> None:
    validate_checked_in_schema(value, EVIDENCE_SET_SCHEMA_PATH, "evidence set")


def validate_recording_manifest_schema(value: Any) -> None:
    validate_checked_in_schema(
        value,
        RECORDING_MANIFEST_SCHEMA_PATH,
        "recording manifest",
    )


def validate_coverage_action_ledger_schema(value: Any) -> None:
    validate_checked_in_schema(
        value,
        COVERAGE_ACTION_LEDGER_SCHEMA_PATH,
        "coverage action ledger",
    )


def validate_sanitized_evidence_bundle_manifest_schema(value: Any) -> None:
    validate_checked_in_schema(
        value,
        SANITIZED_EVIDENCE_BUNDLE_MANIFEST_SCHEMA_PATH,
        "sanitized evidence bundle manifest",
    )


def validate_candidate_judge_input_schema(value: Any) -> None:
    validate_checked_in_schema(
        value,
        CANDIDATE_JUDGE_INPUT_SCHEMA_PATH,
        "candidate judge input",
    )


def validate_panel_finalization_seal_schema(value: Any) -> None:
    validate_checked_in_schema(
        value,
        PANEL_FINALIZATION_SEAL_SCHEMA_PATH,
        "panel finalization seal",
    )


def validate_verification_input_schema(value: Any) -> None:
    validate_checked_in_schema(
        value,
        VERIFICATION_INPUT_SCHEMA_PATH,
        "native evidence verification input",
    )


def validate_gold_binding_schema(value: Any) -> None:
    validate_checked_in_schema(value, GOLD_BINDING_SCHEMA_PATH, "gold binding manifest")


def validate_holdout_consumption_receipt_schema(value: Any) -> None:
    validate_checked_in_schema(
        value,
        HOLDOUT_CONSUMPTION_RECEIPT_SCHEMA_PATH,
        "holdout consumption receipt",
    )


def validate_holdout_consumption_registry_schema(value: Any) -> None:
    validate_checked_in_schema(
        value,
        HOLDOUT_CONSUMPTION_REGISTRY_SCHEMA_PATH,
        "holdout consumption registry",
    )


def validate_native_replacement_receipt_schema(value: Any) -> None:
    validate_checked_in_schema(
        value,
        REPLACEMENT_RECEIPT_SCHEMA_PATH,
        "native replacement receipt",
    )


def validate_native_scorecard_schema(value: Any) -> None:
    validate_checked_in_schema(value, SCORECARD_SCHEMA_PATH, "native scorecard")


def validate_internal_scorecard(value: Any) -> dict[str, Any]:
    scorecard = exact_keys(value, OUTPUT_KEYS, "internal native scorecard")
    if scorecard["schemaVersion"] != SCORECARD_SCHEMA or scorecard["protocol"] != PROTOCOL:
        raise ValidationFailure("internal scorecard identity is invalid")
    if scorecard["metric"] != "CommercialUXProxy":
        raise ValidationFailure("internal scorecard metric must be CommercialUXProxy")
    if not isinstance(scorecard["recipeId"], str) or RECIPE_PATTERN.fullmatch(scorecard["recipeId"]) is None:
        raise ValidationFailure("internal scorecard recipeId is invalid")
    official = scorecard["recipeId"].startswith("HOLDOUT-")
    if scorecard["officialCommercialUX"] is not official:
        raise ValidationFailure("officialCommercialUX must be derived only from recipeId")
    if "textPlanProxy" in scorecard:
        raise ValidationFailure("TextPlanProxy cannot appear in a native scorecard")
    if scorecard["panelKind"] not in {"INITIAL", "REPLACEMENT"}:
        raise ValidationFailure("internal scorecard panelKind is invalid")
    require_sha(scorecard["judgePanelSha256"], "internal scorecard.judgePanelSha256")
    require_sha(scorecard["verificationInputSha256"], "internal scorecard.verificationInputSha256")
    exact_keys(scorecard["provenance"], OUTPUT_PROVENANCE_KEYS, "internal scorecard.provenance")
    if scorecard["provenance"]["judgePanelSha256"] != scorecard["judgePanelSha256"]:
        raise ProvenanceFailure("scorecard provenance judge panel hash mismatch")
    status = scorecard["status"]
    scoreless = (
        isinstance(status, str)
        and (status.startswith("BLOCKED_") or status.startswith("RERUN_REQUIRED_"))
    )
    score_fields = (
        "commercialUXProxy", "rawCommercialUX", "rawSpread", "disagreementPenalty",
        "activeCap", "cellScores", "categoryScores",
    )
    if scoreless and any(scorecard[field] is not None for field in score_fields):
        raise ValidationFailure("blocked/rerun-required scorecards must have null score fields")
    if status == "SCORED_FORMATIVE":
        if official or scorecard["verdict"] is not None:
            raise ValidationFailure("SCORED_FORMATIVE must be non-official with verdict=null")
        if any(scorecard[field] is None for field in score_fields):
            raise ValidationFailure("SCORED_FORMATIVE must retain its numeric native score")
    if scorecard["verdict"] == "PASS" and (not official or scorecard["commercialUXProxy"] < 87):
        raise ValidationFailure("only an official >=87 holdout may PASS")
    if scorecard["status"] == "PASS" and scorecard["verdict"] != "PASS":
        raise ValidationFailure("PASS status and verdict must agree")
    validate_native_scorecard_schema(scorecard)
    return scorecard


def _attempt_failure(attempts: Iterable[dict[str, Any]]) -> tuple[str, str | None]:
    rows = list(attempts)
    for outcome in ("INPUT_UNREADABLE", "TRANSPORT_FAILURE", "SCHEMA_FAILURE"):
        for row in rows:
            if row["attemptOutcome"] == outcome:
                return outcome, row["failureCode"]
    return "VALID", None


def _require_valid_attempt(attempt: dict[str, Any], label: str) -> Any:
    if attempt["attemptOutcome"] != "VALID":
        raise ValidationFailure(
            f"{label} failed before validation: {attempt['failureCode']}"
        )
    return attempt["value"]


def _parsed_panel_sha256(panel_attempt: dict[str, Any]) -> str | None:
    value = panel_attempt["value"]
    if isinstance(value, dict):
        claimed = value.get("judgePanelSha256")
        if isinstance(claimed, str) and SHA256_PATTERN.fullmatch(claimed):
            return claimed
    return None


def _parsed_run_ids(judgment_attempts: list[dict[str, Any]]) -> list[str]:
    run_ids: list[str] = []
    for attempt in judgment_attempts:
        value = attempt["value"]
        run_id = value.get("judgeRunId") if isinstance(value, dict) else None
        if isinstance(run_id, str) and 0 < len(run_id) <= 200 and run_id not in run_ids:
            run_ids.append(run_id)
    return run_ids[:3]


def compute_native_result(
    panel: dict[str, Any] | None,
    judgment_attempts: list[dict[str, Any]],
    verifier: Any,
    verifier_raw_sha256: str,
    qualification_status: str,
    oracle: dict[str, Any],
    candidate: dict[str, Any],
    rubric: dict[str, Any],
    output_path: Path,
    aggregation_input_raw_sha256: str,
    actor_by_anonymous_id: dict[str, dict[str, Any]],
    incident_evidence: dict[str, dict[str, set[str]]],
    verification_input: dict[str, Any],
    verified_verifier: dict[str, Any] | None,
    lane_execution_identities: dict[str, Any],
    *,
    panel_kind: str,
    replacement_for_panel_sha256: str | None = None,
    receipt_path: Path | None = None,
    receipt_sha256: str | None = None,
) -> dict[str, Any]:
    if qualification_status not in {"PASS", "BLOCKED_JUDGE_QUALIFICATION"}:
        raise ValidationFailure("qualification status is invalid")
    ledger = oracle
    qualification_blocker = (
        None if qualification_status == "PASS" else "BLOCKED_JUDGE_QUALIFICATION"
    )
    actor_blocker = terminal_blocker(actor_by_anonymous_id)
    if panel is None:
        result = _schema_blocked_output(
            candidate,
            rubric,
            ledger,
            qualification_status,
            judgment_attempts,
            output_path,
            panel_kind,
            incident_evidence,
            aggregation_input_raw_sha256,
            replacement_for_panel_sha256,
            receipt_path,
            receipt_sha256,
            lane_execution_identities,
        )
        blocker = choose_blocker((
            candidate["operationalBlocker"],
            qualification_blocker,
            "BLOCKED_JUDGE_SCHEMA",
            actor_blocker,
        ))
        result = _blocked_output(result, blocker or "BLOCKED_JUDGE_SCHEMA")
        validate_internal_scorecard(result)
        return result

    lane_inputs = compute_lane_inputs(panel, rubric)
    unstable = unstable_lanes(
        lane_inputs,
        ledger["incidents"],
        actor_by_anonymous_id,
        incident_evidence,
    )
    base = _base_output(
        candidate,
        rubric,
        ledger,
        qualification_status,
        panel_kind,
        panel["panelSha256"],
        panel["runIds"],
        panel["artifactBindings"],
        lane_inputs,
        unstable,
        incident_evidence,
        aggregation_input_raw_sha256,
        output_path,
        replacement_for_panel_sha256,
        lane_execution_identities,
    )
    if receipt_path is not None:
        base["replacementReceiptPath"] = str(receipt_path)
        base["replacementReceiptSha256"] = receipt_sha256
        base["replacementRequiredLanes"] = []
        base["rerunRequired"] = False

    replacement_instability = (
        "BLOCKED_JUDGE_INSTABILITY"
        if unstable and panel_kind == "REPLACEMENT"
        else None
    )
    blocker = choose_blocker((
        candidate["operationalBlocker"],
        qualification_blocker,
        replacement_instability,
        actor_blocker,
    ))
    if blocker is not None:
        result = _blocked_output(base, blocker)
        validate_internal_scorecard(result)
        return result
    if unstable:
        if unstable == ["COLD-JOURNEY"]:
            status = "RERUN_REQUIRED_COLD_INSTABILITY"
        elif unstable == ["COVERAGE-JOURNEY"]:
            status = "RERUN_REQUIRED_COVERAGE_INSTABILITY"
        else:
            status = "RERUN_REQUIRED_PANEL_INSTABILITY"
        base["status"] = status
        base["verdict"] = None
        validate_internal_scorecard(base)
        return base

    verifier_blocker: str | None = None
    if verified_verifier is None or not verified_verifier["supportedOnly"]:
        verifier_blocker = "BLOCKED_EVIDENCE_VERIFICATION"
    base["evidenceVerificationStatus"] = (
        "VERIFIED_SUPPORTED_ONLY"
        if verifier_blocker is None
        else "BLOCKED_EVIDENCE_VERIFICATION"
    )
    gate_blocker = _blocked_gate_status(ledger["value"])
    readiness_blocker = (
        None
        if gate_blocker is not None
        or ledger["value"]["scoreBearingReady"]
        or any(status == "FAIL" for status in ledger["hardGates"].values())
        else "BLOCKED_MISSING_EVIDENCE"
    )
    blocker = choose_blocker((verifier_blocker, gate_blocker, readiness_blocker))
    if blocker is not None:
        result = _blocked_output(base, blocker)
        validate_internal_scorecard(result)
        return result
    cap = active_cap_for(ledger["incidents"], incident_evidence)
    not_reached = set(candidate["notReachedByProductCellIds"])
    if not_reached and cap != 49:
        raise ValidationFailure("NOT_REACHED_BY_PRODUCT cells require a verified active cap 49")
    scores = compute_scores(lane_inputs, rubric, panel["panelSha256"], cap, not_reached)
    base.update(scores)
    base["activeCap"] = cap
    hard_fail = any(status == "FAIL" for status in ledger["hardGates"].values())
    if hard_fail:
        base["status"] = "FAIL_HARD_GATE"
        base["verdict"] = "FAIL_HARD_GATE"
    elif not base["officialCommercialUX"]:
        base["status"] = "SCORED_FORMATIVE"
        base["verdict"] = None
    else:
        passes = official_ux_passes(
            base["commercialUXProxy"],
            base["categoryScores"],
            base["cellScores"],
            cap,
            base["criticalIncidents"],
            base["differenceReport"],
            target=rubric["overallTarget"],
            required_cell_minimum=rubric["requiredCellMinimum"],
        )
        base["status"] = "PASS" if passes else "FAIL_UX"
        base["verdict"] = base["status"]
    validate_internal_scorecard(base)
    return base


def aggregate_to_path(
    judgment_paths: list[Path],
    verifier_path: Path,
    oracle_path: Path,
    candidate_path: Path,
    candidate_manifest_path: Path,
    qualification_receipt_path: Path,
    judge_panel_path: Path,
    evaluation_run_path: Path,
    actor_observation_paths: list[Path],
    coverage_trace_path: Path,
    evidence_set_path: Path,
    rubric_path: Path,
    output_path: Path,
    replacement_for: Path | None = None,
    *,
    verification_input_path: Path,
    cold_actor_response_paths: list[Path],
    actor_trace_paths: list[Path],
    gold_binding_path: Path,
    holdout_consumption_receipt_path: Path,
    holdout_registry_before_path: Path,
    holdout_registry_after_path: Path,
    anonymization_manifest_path: Path,
    story_manifest_path: Path,
    recording_manifest_paths: list[Path],
    coverage_action_ledger_path: Path,
    sanitized_evidence_bundle_manifest_path: Path,
    candidate_judge_input_path: Path,
    evaluation_session_claim_path: Path,
) -> dict[str, Any]:
    # Freeze the caller's path interpretation before any shared validator runs
    # with REPOSITORY_ROOT as its working directory.  Without this, a relative
    # `gold.json` could be opened from the caller CWD while the subprocess
    # validates a different repository-relative file with the same spelling.
    judgment_paths = [Path(path).resolve(strict=False) for path in judgment_paths]
    verifier_path = Path(verifier_path).resolve(strict=False)
    oracle_path = Path(oracle_path).resolve(strict=False)
    candidate_path = Path(candidate_path).resolve(strict=False)
    candidate_manifest_path = Path(candidate_manifest_path).resolve(strict=False)
    qualification_receipt_path = Path(qualification_receipt_path).resolve(strict=False)
    judge_panel_path = Path(judge_panel_path).resolve(strict=False)
    evaluation_run_path = Path(evaluation_run_path).resolve(strict=False)
    actor_observation_paths = [
        Path(path).resolve(strict=False) for path in actor_observation_paths
    ]
    coverage_trace_path = Path(coverage_trace_path).resolve(strict=False)
    evidence_set_path = Path(evidence_set_path).resolve(strict=False)
    rubric_path = Path(rubric_path).resolve(strict=False)
    output_path = Path(output_path).resolve(strict=False)
    replacement_for = (
        Path(replacement_for).resolve(strict=False)
        if replacement_for is not None
        else None
    )
    verification_input_path = Path(verification_input_path).resolve(strict=False)
    cold_actor_response_paths = [
        Path(path).resolve(strict=False) for path in cold_actor_response_paths
    ]
    actor_trace_paths = [Path(path).resolve(strict=False) for path in actor_trace_paths]
    gold_binding_path = Path(gold_binding_path).resolve(strict=False)
    holdout_consumption_receipt_path = Path(
        holdout_consumption_receipt_path
    ).resolve(strict=False)
    holdout_registry_before_path = Path(holdout_registry_before_path).resolve(
        strict=False
    )
    holdout_registry_after_path = Path(holdout_registry_after_path).resolve(
        strict=False
    )
    anonymization_manifest_path = Path(anonymization_manifest_path).resolve(
        strict=False
    )
    story_manifest_path = Path(story_manifest_path).resolve(strict=False)
    recording_manifest_paths = [
        Path(path).resolve(strict=False) for path in recording_manifest_paths
    ]
    coverage_action_ledger_path = Path(coverage_action_ledger_path).resolve(
        strict=False
    )
    sanitized_evidence_bundle_manifest_path = Path(
        sanitized_evidence_bundle_manifest_path
    ).resolve(strict=False)
    candidate_judge_input_path = Path(candidate_judge_input_path).resolve(strict=False)
    evaluation_session_claim_path = Path(evaluation_session_claim_path).resolve(
        strict=False
    )

    if output_path.exists():
        raise ValidationFailure(f"native aggregate output path must be fresh: {output_path}")
    if (
        len(judgment_paths) != 3
        or len(actor_observation_paths) != 3
        or len(cold_actor_response_paths) != 3
        or len(actor_trace_paths) != 3
        or len(recording_manifest_paths) != 4
    ):
        raise ValidationFailure(
            "aggregate requires exactly three judgments/cold responses/actor "
            "observations/actor traces "
            "and four recording manifests"
        )

    rubric_value, rubric_bytes = read_json_bytes(rubric_path, "commercial UX rubric")
    rubric = load_rubric(rubric_value, bytes_sha256(rubric_bytes))
    replacement_context: dict[str, Any] | None = None
    receipt_descriptor: int | None = None
    holdout_finalization_path: Path | None = None
    holdout_finalization_descriptor: int | None = None
    panel_finalization_path: Path | None = None
    panel_finalization_descriptor: int | None = None
    scorecard_output_descriptor: int | None = None
    if replacement_for is not None:
        initial, initial_bytes, receipt_path, initial_seal = _validate_initial_for_replacement(
            replacement_for,
            rubric,
        )
        replacement_context = {
            "initial": initial,
            "initialBytes": initial_bytes,
            "receiptPath": receipt_path,
            "initialSeal": initial_seal,
        }

    # Authority preflight deliberately excludes the panel/judgment transports.
    # An unrelated candidate/evaluation request must not consume the sole valid
    # replacement slot; content attempts begin only after this block succeeds.
    candidate_attempt = read_json_attempt(candidate_path, "candidate aggregation input")
    manifest_attempt = read_json_attempt(candidate_manifest_path, "candidate manifest")
    qualification_attempt = read_json_attempt(
        qualification_receipt_path,
        "qualification receipt",
    )
    evaluation_attempt = read_json_attempt(evaluation_run_path, "evaluation run manifest")
    actor_attempts = [
        read_json_attempt(path, f"actor observation {index + 1}")
        for index, path in enumerate(actor_observation_paths)
    ]
    cold_actor_response_attempts = [
        read_json_attempt(path, f"cold actor response {index + 1}")
        for index, path in enumerate(cold_actor_response_paths)
    ]
    coverage_attempt = read_json_attempt(coverage_trace_path, "coverage trace")
    evidence_attempt = read_json_attempt(evidence_set_path, "evidence set")
    actor_trace_attempts = [
        read_json_attempt(path, f"actor trace {index + 1}")
        for index, path in enumerate(actor_trace_paths)
    ]
    verification_input_attempt = read_json_attempt(
        verification_input_path,
        "native evidence verification input",
    )
    gold_binding_attempt = read_json_attempt(
        gold_binding_path,
        "candidate gold binding manifest",
    )
    holdout_receipt_attempt = read_json_attempt(
        holdout_consumption_receipt_path,
        "holdout consumption receipt",
    )
    registry_before_attempt = read_json_attempt(
        holdout_registry_before_path,
        "holdout registry before claim",
    )
    registry_after_attempt = read_json_attempt(
        holdout_registry_after_path,
        "holdout registry after claim",
    )
    anonymization_attempt = read_json_attempt(
        anonymization_manifest_path,
        "anonymization manifest",
    )
    recording_manifest_attempts = [
        read_json_attempt(path, f"recording manifest {index + 1}")
        for index, path in enumerate(recording_manifest_paths)
    ]
    coverage_action_ledger_attempt = read_json_attempt(
        coverage_action_ledger_path,
        "coverage action ledger",
    )
    sanitized_bundle_attempt = read_json_attempt(
        sanitized_evidence_bundle_manifest_path,
        "sanitized evidence bundle manifest",
    )
    candidate_judge_input_attempt = read_json_attempt(
        candidate_judge_input_path,
        "candidate judge input",
    )
    story_manifest_attempt = read_json_attempt(
        story_manifest_path,
        "candidate authored story manifest",
    )
    verifier_attempt = read_json_attempt(
        verifier_path,
        "native verifier output",
        capture_unreadable=True,
    )
    oracle_attempt = read_json_attempt(
        oracle_path,
        "oracle hard-gate ledger",
        capture_unreadable=True,
    )

    candidate_value = _require_valid_attempt(candidate_attempt, "candidate aggregation input")
    candidate = validate_candidate(candidate_value)
    validate_official_score_bearing_preflight(candidate)
    validate_checked_in_contract_hashes(candidate)
    if candidate["provenance"]["rubricSha256"] != rubric["sha256"]:
        raise ProvenanceFailure("candidate rubricSha256 does not match the exact rubric file")
    candidate_manifest_value = _require_valid_attempt(manifest_attempt, "candidate manifest")
    qualification_value = _require_valid_attempt(
        qualification_attempt,
        "qualification receipt",
    )
    evaluation_value = _require_valid_attempt(evaluation_attempt, "evaluation run manifest")
    actor_values = [
        _require_valid_attempt(row, f"actor observation {index + 1}")
        for index, row in enumerate(actor_attempts)
    ]
    cold_actor_response_values = [
        _require_valid_attempt(row, f"cold actor response {index + 1}")
        for index, row in enumerate(cold_actor_response_attempts)
    ]
    coverage_value = _require_valid_attempt(coverage_attempt, "coverage trace")
    evidence_value = _require_valid_attempt(evidence_attempt, "evidence set")
    actor_trace_values = [
        _require_valid_attempt(row, f"actor trace {index + 1}")
        for index, row in enumerate(actor_trace_attempts)
    ]
    verification_input_value = _require_valid_attempt(
        verification_input_attempt,
        "native evidence verification input",
    )
    gold_binding_value = _require_valid_attempt(
        gold_binding_attempt,
        "candidate gold binding manifest",
    )
    holdout_receipt_value = _require_valid_attempt(
        holdout_receipt_attempt,
        "holdout consumption receipt",
    )
    registry_before_value = _require_valid_attempt(
        registry_before_attempt,
        "holdout registry before claim",
    )
    registry_after_value = _require_valid_attempt(
        registry_after_attempt,
        "holdout registry after claim",
    )
    anonymization_value = _require_valid_attempt(
        anonymization_attempt,
        "anonymization manifest",
    )
    recording_manifest_values = [
        _require_valid_attempt(row, f"recording manifest {index + 1}")
        for index, row in enumerate(recording_manifest_attempts)
    ]
    coverage_action_ledger_value = _require_valid_attempt(
        coverage_action_ledger_attempt,
        "coverage action ledger",
    )
    sanitized_bundle_value = _require_valid_attempt(
        sanitized_bundle_attempt,
        "sanitized evidence bundle manifest",
    )
    candidate_judge_input_value = _require_valid_attempt(
        candidate_judge_input_attempt,
        "candidate judge input",
    )
    _require_valid_attempt(story_manifest_attempt, "candidate authored story manifest")

    candidate_authority = validate_candidate_manifest_authority(
        candidate_manifest_value,
        manifest_attempt["rawBytes"],
        candidate,
        story_manifest_attempt["rawBytes"],
    )
    holdout_authority = validate_holdout_consumption_authority(
        holdout_receipt_value,
        holdout_receipt_attempt["rawBytes"],
        holdout_consumption_receipt_path,
        (registry_before_value, registry_before_attempt["rawBytes"]),
        (registry_after_value, registry_after_attempt["rawBytes"]),
        holdout_registry_after_path,
        candidate_authority["value"],
    )
    session_authority = validate_evaluation_session_authority(
        evaluation_session_claim_path,
        candidate_authority["value"],
        manifest_attempt["rawBytes"],
        holdout_authority["value"],
        holdout_receipt_attempt["rawBytes"],
        replacement_context,
    )
    validate_evaluation_session_candidate_provenance(
        session_authority,
        candidate,
    )
    validate_evaluation_session_fixed_artifacts(
        session_authority,
        {
            "goldBinding": gold_binding_path,
            "anonymization": anonymization_manifest_path,
            "evidenceSet": evidence_set_path,
            "sanitizedEvidenceBundle": sanitized_evidence_bundle_manifest_path,
            "candidateJudgeInput": candidate_judge_input_path,
            "judgePanel": judge_panel_path,
            "verificationInput": verification_input_path,
            "evaluationRun": evaluation_run_path,
            "aggregationInput": candidate_path,
            "scorecard": output_path,
            "panelFinalizationSeal": Path(
                session_authority["claim"]["fixedArtifactPaths"][
                    "panelFinalizationSeal"
                ]
            ),
        },
    )
    validate_evaluation_session_supporting_artifacts(
        session_authority,
        actor_observation_attempts=actor_attempts,
        actor_trace_attempts=actor_trace_attempts,
        recording_manifest_attempts=recording_manifest_attempts,
        coverage_action_ledger_attempt=coverage_action_ledger_attempt,
    )
    gold_binding_envelope = validate_self_hashed_envelope(
        gold_binding_value,
        gold_binding_attempt["rawBytes"],
        schema_version=GOLD_BINDING_SCHEMA,
        self_field="goldBindingManifestSha256",
        schema_validator=validate_gold_binding_schema,
        label="candidate gold binding manifest",
    )
    verification_input_envelope = validate_self_hashed_envelope(
        verification_input_value,
        verification_input_attempt["rawBytes"],
        schema_version=VERIFICATION_INPUT_SCHEMA,
        self_field="verificationInputSha256",
        schema_validator=validate_verification_input_schema,
        label="native evidence verification input",
    )
    actor_trace_envelopes = [
        validate_self_hashed_envelope(
            value,
            attempt["rawBytes"],
            schema_version=ACTOR_TRACE_SCHEMA,
            self_field="actorTraceSha256",
            schema_validator=validate_actor_trace_schema,
            label=f"actor trace {index + 1}",
        )
        for index, (value, attempt) in enumerate(
            zip(actor_trace_values, actor_trace_attempts)
        )
    ]
    recording_manifest_envelopes = [
        validate_self_hashed_envelope(
            value,
            attempt["rawBytes"],
            schema_version=RECORDING_MANIFEST_SCHEMA,
            self_field="recordingManifestSha256",
            schema_validator=validate_recording_manifest_schema,
            label=f"recording manifest {index + 1}",
        )
        for index, (value, attempt) in enumerate(
            zip(recording_manifest_values, recording_manifest_attempts)
        )
    ]
    coverage_action_ledger_envelope = validate_self_hashed_envelope(
        coverage_action_ledger_value,
        coverage_action_ledger_attempt["rawBytes"],
        schema_version=COVERAGE_ACTION_LEDGER_SCHEMA,
        self_field="coverageActionLedgerSha256",
        schema_validator=validate_coverage_action_ledger_schema,
        label="coverage action ledger",
    )
    anonymization_envelope = validate_self_hashed_envelope(
        anonymization_value,
        anonymization_attempt["rawBytes"],
        schema_version=ANONYMIZATION_MANIFEST_SCHEMA,
        self_field="anonymizationManifestSha256",
        schema_validator=validate_anonymization_manifest_schema,
        label="anonymization manifest",
    )
    evidence_set_envelope = validate_self_hashed_envelope(
        evidence_value,
        evidence_attempt["rawBytes"],
        schema_version=EVIDENCE_SET_SCHEMA,
        self_field="evidenceSetSha256",
        schema_validator=validate_evidence_set_schema,
        label="evidence set",
    )
    sanitized_bundle_envelope = validate_self_hashed_envelope(
        sanitized_bundle_value,
        sanitized_bundle_attempt["rawBytes"],
        schema_version=SANITIZED_EVIDENCE_BUNDLE_MANIFEST_SCHEMA,
        self_field="sanitizedEvidenceBundleManifestSha256",
        schema_validator=validate_sanitized_evidence_bundle_manifest_schema,
        label="sanitized evidence bundle manifest",
    )
    candidate_judge_input_envelope = validate_self_hashed_envelope(
        candidate_judge_input_value,
        candidate_judge_input_attempt["rawBytes"],
        schema_version=CANDIDATE_JUDGE_INPUT_SCHEMA,
        self_field="judgeInputSha256",
        schema_validator=validate_candidate_judge_input_schema,
        label="candidate judge input",
    )
    qualification_authority = validate_qualification_authority(
        qualification_value,
        qualification_attempt["rawBytes"],
        candidate,
        candidate_authority["value"],
    )
    evaluation_authority = validate_evaluation_run_authority(
        evaluation_value,
        evaluation_attempt["rawBytes"],
        candidate,
        candidate_authority["value"],
        qualification_authority,
        verification_input=verification_input_envelope,
        gold_binding=gold_binding_envelope,
        holdout_receipt=holdout_authority,
        actor_trace_envelopes=actor_trace_envelopes,
        recording_manifest_envelopes=recording_manifest_envelopes,
        coverage_action_ledger=coverage_action_ledger_envelope,
        anonymization=anonymization_envelope,
        evidence_set=evidence_set_envelope,
        sanitized_bundle=sanitized_bundle_envelope,
        candidate_judge_input=candidate_judge_input_envelope,
    )
    gold_binding_authority = validate_gold_binding_authority(
        gold_binding_value,
        gold_binding_attempt["rawBytes"],
        candidate_authority["value"],
        holdout_authority,
        evaluation_authority["value"],
        candidate_manifest_path=candidate_manifest_path,
        gold_binding_path=gold_binding_path,
        candidate_manifest_raw_bytes=manifest_attempt["rawBytes"],
        story_manifest_raw_bytes=story_manifest_attempt["rawBytes"],
        holdout_consumption_receipt_path=holdout_consumption_receipt_path,
        registry_before_path=holdout_registry_before_path,
        registry_after_path=holdout_registry_after_path,
        evaluation_session_claim_path=evaluation_session_claim_path,
        holdout_consumption_receipt_raw_bytes=holdout_receipt_attempt[
            "rawBytes"
        ],
        registry_before_raw_bytes=registry_before_attempt["rawBytes"],
        registry_after_raw_bytes=registry_after_attempt["rawBytes"],
        evaluation_session_claim_raw_bytes=session_authority["claimRawBytes"],
    )
    validate_runtime_contract_authority(
        candidate_manifest_path,
        qualification_receipt_path,
        gold_binding_path,
        holdout_consumption_receipt_path,
        holdout_registry_before_path,
        holdout_registry_after_path,
        evaluation_session_claim_path,
        candidate_manifest_raw_bytes=manifest_attempt["rawBytes"],
        qualification_receipt_raw_bytes=qualification_attempt["rawBytes"],
        gold_binding_raw_bytes=gold_binding_attempt["rawBytes"],
        holdout_consumption_receipt_raw_bytes=holdout_receipt_attempt[
            "rawBytes"
        ],
        holdout_registry_before_raw_bytes=registry_before_attempt["rawBytes"],
        holdout_registry_after_raw_bytes=registry_after_attempt["rawBytes"],
        evaluation_session_claim_raw_bytes=session_authority["claimRawBytes"],
        initial_evaluation_session_claim_path=session_authority[
            "initialClaimPath"
        ],
        initial_evaluation_session_claim_raw_bytes=session_authority[
            "initialClaimRawBytes"
        ],
    )
    _validate_evaluation_replacement_authority(
        evaluation_authority,
        replacement_context,
    )
    cold_actor_response_authorities = validate_cold_actor_response_authorities(
        [
            (value, attempt["rawBytes"])
            for value, attempt in zip(
                cold_actor_response_values,
                cold_actor_response_attempts,
            )
        ],
        evaluation_authority["value"],
    )
    actor_rows = validate_actor_observation_authorities(
        [(value, attempt["rawBytes"]) for value, attempt in zip(actor_values, actor_attempts)],
        evaluation_authority["value"],
        cold_actor_response_authorities,
    )
    validate_required_cold_probes(actor_rows, candidate)
    actor_trace_authorities = validate_actor_trace_authorities(
        [(value, attempt["rawBytes"]) for value, attempt in zip(
            actor_trace_values, actor_trace_attempts
        )],
        actor_rows,
        candidate_authority["value"],
        evaluation_authority["value"],
        holdout_authority,
        gold_binding_authority,
    )
    coverage_authority = validate_coverage_trace_authority(
        coverage_value,
        coverage_attempt["rawBytes"],
        candidate_authority["value"],
        evaluation_authority["value"],
        holdout_authority,
        gold_binding_authority,
        coverage_action_ledger_envelope,
    )
    coverage_action_ledger_authority = validate_coverage_action_ledger_authority(
        coverage_action_ledger_envelope,
        candidate_authority["value"],
        evaluation_authority["value"],
        holdout_authority,
        gold_binding_authority,
        coverage_authority,
    )
    recording_authorities = validate_recording_manifest_authorities(
        recording_manifest_envelopes,
        candidate_authority["value"],
        evaluation_authority["value"],
        actor_rows,
        actor_trace_authorities,
        coverage_authority,
        coverage_action_ledger_authority,
    )
    lane_execution_identities = derive_lane_execution_identities(
        actor_trace_authorities,
        coverage_action_ledger_authority,
        recording_authorities,
    )
    anonymization_authority = validate_anonymization_authority(
        anonymization_value,
        anonymization_attempt["rawBytes"],
        candidate,
        candidate_authority["value"],
        holdout_authority,
        actor_trace_authorities,
        coverage_authority,
    )
    evidence_authority = validate_evidence_set_authority(
        evidence_value,
        evidence_attempt["rawBytes"],
        candidate,
        candidate_authority["value"],
        evaluation_authority["value"],
        actor_rows,
        actor_trace_authorities,
        coverage_authority,
        holdout_authority,
        gold_binding_authority,
        anonymization_authority,
        recording_authorities,
    )
    sanitized_bundle_authority = validate_sanitized_evidence_bundle_authority(
        sanitized_bundle_envelope,
        candidate,
        candidate_authority["value"],
        evaluation_authority["value"],
        holdout_authority,
        gold_binding_authority,
        anonymization_authority,
        evidence_authority,
        recording_authorities,
    )
    candidate_judge_input_authority = validate_candidate_judge_input_authority(
        candidate_judge_input_envelope,
        candidate,
        candidate_authority["value"],
        qualification_authority,
        holdout_authority,
        gold_binding_authority,
        evidence_authority,
        sanitized_bundle_authority,
        evaluation_authority["value"],
    )
    if replacement_context is not None:
        _validate_replacement_candidate_stability(
            replacement_context["initial"],
            candidate,
            rubric,
        )
        _validate_replacement_artifact_freshness(
            replacement_context["initial"],
            candidate,
            lane_execution_identities,
        )

    panel_kind = "REPLACEMENT" if replacement_context is not None else "INITIAL"
    initial_panel_sha256 = (
        replacement_context["initial"]["judgePanelSha256"]
        if replacement_context is not None
        else candidate["provenance"]["judgePanelSha256"]
    )
    if (
        replacement_context is not None
        and replacement_context["receiptPath"].exists()
    ):
        raise ValidationFailure(
            "native replacement was already consumed: "
            f"{replacement_context['receiptPath']}"
        )
    # These singleton claims are deliberately acquired after all producer
    # authorities pass, but before any judge/verifier/oracle outcome can be
    # observed.  A caller therefore cannot discard an unfavorable panel and
    # reuse the same holdout lane or initial-panel slot.
    holdout_finalization_path = _holdout_finalization_path(
        holdout_authority,
        panel_kind,
    )
    holdout_finalization_descriptor = _reserve_holdout_finalization(
        holdout_finalization_path
    )
    panel_finalization_path = Path(
        session_authority["claim"]["fixedArtifactPaths"][
            "panelFinalizationSeal"
        ]
    )
    try:
        panel_finalization_descriptor = _reserve_holdout_finalization(
            panel_finalization_path
        )
    except Exception:
        descriptor = holdout_finalization_descriptor
        holdout_finalization_descriptor = None
        _finalize_reserved_receipt(
            descriptor,
            holdout_finalization_path,
            _holdout_finalization_bytes(
                holdout_authority,
                evaluation_authority,
                candidate["provenance"]["judgePanelSha256"],
                "SINGLETON_RESERVATION_FAILURE",
                None,
            ),
        )
        raise
    if replacement_context is not None:
        try:
            receipt_descriptor = _reserve_receipt(
                replacement_context["receiptPath"]
            )
        except Exception:
            failure_bytes = _holdout_finalization_bytes(
                holdout_authority,
                evaluation_authority,
                candidate["provenance"]["judgePanelSha256"],
                "SINGLETON_RESERVATION_FAILURE",
                None,
            )
            descriptor = holdout_finalization_descriptor
            holdout_finalization_descriptor = None
            _finalize_reserved_receipt(
                descriptor,
                holdout_finalization_path,
                failure_bytes,
            )
            descriptor = panel_finalization_descriptor
            panel_finalization_descriptor = None
            _finalize_reserved_receipt(
                descriptor,
                panel_finalization_path,
                failure_bytes,
            )
            raise

    panel_attempt = read_json_attempt(
        judge_panel_path,
        "judge panel manifest",
        slot_id="PANEL",
        capture_unreadable=True,
    )
    judgment_attempts = [
        read_json_attempt(
            path,
            f"native judgment {index + 1}",
            slot_id=f"JUDGE-{index + 1:02d}",
            capture_unreadable=True,
        )
        for index, path in enumerate(judgment_paths)
    ]

    def finalize_replacement_receipt(
        outcome: str,
        failure_code: str | None,
        parsed_panel: str | None,
        parsed_runs: list[str],
    ) -> str | None:
        nonlocal receipt_descriptor
        if replacement_context is None:
            return None
        if receipt_descriptor is None:
            raise ValidationFailure("replacement receipt claim was already finalized")
        content, receipt_sha = _receipt_bytes(
            replacement_context["receiptPath"],
            replacement_for,
            replacement_context["initialBytes"],
            replacement_context["initial"],
            replacement_context["initialSeal"],
            candidate,
            rubric,
            candidate_attempt["rawSha256"],
            manifest_attempt["rawSha256"],
            qualification_attempt["rawSha256"],
            qualification_authority["selfSha256"],
            evaluation_attempt["rawSha256"],
            evaluation_authority["selfSha256"],
            panel_attempt,
            judgment_attempts,
            outcome,
            failure_code,
            parsed_panel,
            parsed_runs,
        )
        descriptor = receipt_descriptor
        _finalize_reserved_receipt(
            descriptor,
            replacement_context["receiptPath"],
            content,
        )
        receipt_descriptor = None
        return receipt_sha

    def terminalize_claimed_failure(outcome: str) -> None:
        """Durably close every singleton claimed before observing outcomes."""

        nonlocal receipt_descriptor
        nonlocal holdout_finalization_descriptor
        nonlocal panel_finalization_descriptor
        nonlocal scorecard_output_descriptor
        if scorecard_output_descriptor is not None:
            descriptor = scorecard_output_descriptor
            scorecard_output_descriptor = None
            _close_reserved_scorecard_output(descriptor, output_path)
        if receipt_descriptor is not None:
            attempt_outcome, failure_code = _attempt_failure(
                [panel_attempt, *judgment_attempts]
            )
            try:
                finalize_replacement_receipt(
                    attempt_outcome,
                    failure_code,
                    _parsed_panel_sha256(panel_attempt),
                    _parsed_run_ids(judgment_attempts),
                )
            except Exception as exception:
                # The schema-classification pass above makes this an emergency
                # disk/schema failure only.  Still leave an explicit durable
                # record instead of a deletable zero-byte claim, then continue
                # closing the holdout and panel singletons.
                descriptor = receipt_descriptor
                receipt_descriptor = None
                emergency = json.dumps({
                    "schemaVersion": (
                        "gridworks.commercial-ux.native-replacement-"
                        "terminalization-failure.v1"
                    ),
                    "protocol": PROTOCOL,
                    "slotConsumed": True,
                    "failure": f"{type(exception).__name__}:{str(exception)}"[:300],
                }, ensure_ascii=False, indent=2, sort_keys=True).encode("utf-8") + b"\n"
                try:
                    _finalize_reserved_receipt(
                        descriptor,
                        replacement_context["receiptPath"],
                        emergency,
                    )
                except Exception:
                    pass
        failure_bytes = _holdout_finalization_bytes(
            holdout_authority,
            evaluation_authority,
            candidate["provenance"]["judgePanelSha256"],
            outcome,
            None,
        )
        if holdout_finalization_descriptor is not None:
            descriptor = holdout_finalization_descriptor
            holdout_finalization_descriptor = None
            try:
                _finalize_reserved_receipt(
                    descriptor,
                    holdout_finalization_path,
                    failure_bytes,
                )
            except Exception:
                pass
        if panel_finalization_descriptor is not None:
            descriptor = panel_finalization_descriptor
            panel_finalization_descriptor = None
            try:
                _finalize_reserved_receipt(
                    descriptor,
                    panel_finalization_path,
                    failure_bytes,
                )
            except Exception:
                pass

    # Verifier/oracle and panel/judgment transports are outcome-bearing.  Their
    # bytes may be read during producer preflight, but parsing, binding, and
    # semantic acceptance happen only after all singleton claims are durable.
    # Any rejection after this point is terminalized, including the replacement
    # receipt, so a zero-byte partial claim cannot be deleted and rerolled.
    try:
        validate_evaluation_session_primary_outputs(
            session_authority,
            {
                "SLOT-01": (
                    cold_actor_response_paths[0],
                    cold_actor_response_attempts[0]["rawBytes"],
                ),
                "SLOT-02": (
                    cold_actor_response_paths[1],
                    cold_actor_response_attempts[1]["rawBytes"],
                ),
                "SLOT-03": (
                    cold_actor_response_paths[2],
                    cold_actor_response_attempts[2]["rawBytes"],
                ),
                "SLOT-04": (coverage_trace_path, coverage_attempt["rawBytes"]),
                "SLOT-05": (judgment_paths[0], judgment_attempts[0]["rawBytes"]),
                "SLOT-06": (judgment_paths[1], judgment_attempts[1]["rawBytes"]),
                "SLOT-07": (judgment_paths[2], judgment_attempts[2]["rawBytes"]),
                "SLOT-08": (verifier_path, verifier_attempt["rawBytes"]),
                "SLOT-09": (oracle_path, oracle_attempt["rawBytes"]),
            },
        )
        bind_judge_attempt_transports(
            evaluation_authority["value"],
            panel_attempt,
            judgment_attempts,
        )
        classify_judge_attempt_schemas(panel_attempt, judgment_attempts)
        verifier_value = _require_valid_attempt(
            verifier_attempt,
            "native verifier output",
        )
        oracle_value = _require_valid_attempt(
            oracle_attempt,
            "oracle hard-gate ledger",
        )
        validate_evaluation_outcome_transports(
            evaluation_authority,
            verifier_value,
            verifier_attempt["rawSha256"],
            oracle_attempt["rawSha256"],
        )
    except Exception:
        terminalize_claimed_failure("OUTCOME_TRANSPORT_FAILURE")
        raise

    panel: dict[str, Any] | None = None
    attempt_outcome, failure_code = _attempt_failure([panel_attempt, *judgment_attempts])
    panel_authority: dict[str, Any] | None = None
    if attempt_outcome == "VALID":
        try:
            panel_authority = validate_judge_panel_authority(
                panel_attempt["value"],
                panel_attempt["rawBytes"],
                candidate,
                candidate_authority["value"],
                qualification_authority,
                evaluation_authority["value"],
                candidate_judge_input_authority,
                judgment_attempts,
            )
        except ValidationFailure as exception:
            panel_attempt["attemptOutcome"] = "SCHEMA_FAILURE"
            panel_attempt["failureCode"] = (
                f"PANEL:{type(exception).__name__}:{str(exception)}"
            )[:300]
        if panel_authority is not None:
            try:
                _validate_panel_replacement_linkage(
                    panel_authority,
                    evaluation_authority,
                    replacement_context,
                    candidate,
                )
            except ValidationFailure as exception:
                panel_attempt["attemptOutcome"] = "SCHEMA_FAILURE"
                panel_attempt["failureCode"] = (
                    f"PANEL:{type(exception).__name__}:{str(exception)}"
                )[:300]
        if panel_authority is not None and panel_attempt["attemptOutcome"] == "VALID":
            try:
                panel = validate_panel(
                    [row["value"] for row in judgment_attempts],
                    candidate,
                    rubric,
                    panel_authority,
                )
            except JudgmentValidationFailure as exception:
                match = re.match(r"^judgment\[([0-2])\]", str(exception))
                failed_attempt = (
                    judgment_attempts[int(match.group(1))]
                    if match is not None
                    else panel_attempt
                )
                failed_attempt["attemptOutcome"] = "SCHEMA_FAILURE"
                failed_attempt["failureCode"] = (
                    f"{failed_attempt['slotId']}:{type(exception).__name__}:{str(exception)}"
                )[:300]
            except ValidationFailure as exception:
                panel_attempt["attemptOutcome"] = "SCHEMA_FAILURE"
                panel_attempt["failureCode"] = (
                    f"PANEL:{type(exception).__name__}:{str(exception)}"
                )[:300]
    attempt_outcome, failure_code = _attempt_failure([panel_attempt, *judgment_attempts])

    receipt_sha256: str | None = None
    try:
        validate_retry_attribution(
            evaluation_authority["value"],
            panel_attempt,
            judgment_attempts,
        )
        if replacement_context is not None:
            receipt_sha256 = finalize_replacement_receipt(
                attempt_outcome,
                failure_code,
                panel_authority["selfSha256"] if panel_authority is not None else _parsed_panel_sha256(panel_attempt),
                panel["runIds"] if panel is not None else _parsed_run_ids(judgment_attempts),
            )
    except Exception:
        terminalize_claimed_failure("POST_CLAIM_VALIDATION_FAILURE")
        raise

    try:
        verification_input_authority = verification_input_envelope
        verified_verifier_authority: dict[str, Any] | None = None
        if panel is not None:
            if panel_authority is None:
                raise ValidationFailure("validated judge panel is missing its raw authority")
            verification_input_authority = validate_verification_input_authority(
                verification_input_value,
                verification_input_attempt["rawBytes"],
                candidate,
                candidate_authority["value"],
                evaluation_authority["value"],
                evidence_authority,
                panel,
                holdout_authority,
                gold_binding_authority,
                sanitized_bundle_authority,
            )
            try:
                verified_verifier_authority = validate_verifier(
                    verifier_value,
                    verifier_attempt["rawSha256"],
                    candidate,
                    panel["panelSha256"],
                    verification_input_authority,
                )
            except VerifierValidationFailure:
                # A schema/semantic verifier failure is a score blocker, not an
                # authority exception.  No incident/cap status is trusted.
                verified_verifier_authority = None
        oracle = validate_oracle_ledger(
            oracle_value,
            oracle_attempt["rawSha256"],
            candidate,
            rubric,
            holdout_authority,
            gold_binding_authority,
            verification_input_authority,
            evaluation_authority,
            recording_authorities,
            verified_verifier_authority,
        )
        incident_evidence = validate_incident_actor_evidence(
            oracle["incidents"],
            evidence_authority["actorAuthoritiesByAnonymousId"],
        )
        validate_severe_single_run_authority(
            evidence_authority["actorAuthoritiesByAnonymousId"],
            oracle["incidents"],
            incident_evidence,
        )
    except Exception:
        terminalize_claimed_failure("POST_CLAIM_VALIDATION_FAILURE")
        raise

    qualification_status = (
        "PASS"
        if qualification_authority["status"] == "PASS"
        else "BLOCKED_JUDGE_QUALIFICATION"
    )
    try:
        result = compute_native_result(
            panel,
            judgment_attempts,
            verifier_value,
            verifier_attempt["rawSha256"],
            qualification_status,
            oracle,
            candidate,
            rubric,
            output_path,
            candidate_attempt["rawSha256"],
            evidence_authority["actorAuthoritiesByAnonymousId"],
            incident_evidence,
            verification_input_authority,
            verified_verifier_authority,
            lane_execution_identities,
            panel_kind="REPLACEMENT" if replacement_context is not None else "INITIAL",
            replacement_for_panel_sha256=(
                replacement_context["initial"]["judgePanelSha256"]
                if replacement_context is not None
                else None
            ),
            receipt_path=(
                replacement_context["receiptPath"] if replacement_context is not None else None
            ),
            receipt_sha256=receipt_sha256,
        )
        output = json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True).encode("utf-8") + b"\n"
        output_sha256 = bytes_sha256(output)
        holdout_success_bytes = _holdout_finalization_bytes(
            holdout_authority,
            evaluation_authority,
            candidate["provenance"]["judgePanelSha256"],
            result["status"],
            output_sha256,
        )
        panel_success_bytes = _panel_finalization_seal_bytes(
            panel_finalization_path,
            panel_kind,
            initial_panel_sha256,
            result,
            output_path,
            output_sha256,
        )
        # Reserve the user-visible scorecard before either success authority is
        # finalized.  Until both receipts are durable this path remains a
        # zero-byte, schema-invalid tombstone; it can never expose a valid PASS.
        scorecard_output_descriptor = _reserve_scorecard_output(
            output_path,
            replacement_claimed=replacement_context is not None,
        )
    except Exception:
        terminalize_claimed_failure("AGGREGATION_FAILURE")
        raise
    try:
        if holdout_finalization_descriptor is None:
            raise ValidationFailure("holdout finalization singleton was not reserved")
        descriptor = holdout_finalization_descriptor
        holdout_finalization_descriptor = None
        _finalize_reserved_receipt(
            descriptor,
            holdout_finalization_path,
            holdout_success_bytes,
        )
        if panel_finalization_descriptor is None:
            raise ValidationFailure("panel finalization singleton was not reserved")
        descriptor = panel_finalization_descriptor
        panel_finalization_descriptor = None
        _finalize_reserved_receipt(
            descriptor,
            panel_finalization_path,
            panel_success_bytes,
        )
        if scorecard_output_descriptor is None:
            raise ValidationFailure("native aggregate output was not reserved")
        descriptor = scorecard_output_descriptor
        _finalize_reserved_scorecard_output(
            descriptor,
            output_path,
            output,
        )
        scorecard_output_descriptor = None
    except Exception:
        terminalize_claimed_failure("FINALIZATION_FAILURE")
        raise
    return result


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Aggregate exactly three strict Gridworks native commercial-UX judgments."
    )
    parser.add_argument("judgments", nargs=3, type=Path)
    parser.add_argument("--verifier", required=True, type=Path)
    parser.add_argument("--oracle-ledger", required=True, type=Path)
    parser.add_argument("--candidate-provenance", required=True, type=Path)
    parser.add_argument("--candidate-manifest", required=True, type=Path)
    parser.add_argument("--qualification-receipt", required=True, type=Path)
    parser.add_argument("--judge-panel", required=True, type=Path)
    parser.add_argument("--evaluation-run", required=True, type=Path)
    parser.add_argument("--actor-observations", nargs=3, required=True, type=Path)
    parser.add_argument("--cold-actor-responses", nargs=3, required=True, type=Path)
    parser.add_argument("--coverage-trace", required=True, type=Path)
    parser.add_argument("--evidence-set", required=True, type=Path)
    parser.add_argument("--verification-input", required=True, type=Path)
    parser.add_argument("--actor-traces", nargs=3, required=True, type=Path)
    parser.add_argument("--gold-binding", required=True, type=Path)
    parser.add_argument("--holdout-consumption-receipt", required=True, type=Path)
    parser.add_argument("--holdout-registry-before", required=True, type=Path)
    parser.add_argument("--holdout-registry-after", required=True, type=Path)
    parser.add_argument("--anonymization-manifest", required=True, type=Path)
    parser.add_argument("--story-manifest", required=True, type=Path)
    parser.add_argument("--recording-manifests", nargs=4, required=True, type=Path)
    parser.add_argument("--coverage-action-ledger", required=True, type=Path)
    parser.add_argument(
        "--sanitized-evidence-bundle-manifest",
        required=True,
        type=Path,
    )
    parser.add_argument("--candidate-judge-input", required=True, type=Path)
    parser.add_argument("--evaluation-session-claim", required=True, type=Path)
    parser.add_argument("--rubric", type=Path, default=DEFAULT_RUBRIC_PATH)
    parser.add_argument("--replacement-for", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    try:
        result = aggregate_to_path(
            args.judgments,
            args.verifier,
            args.oracle_ledger,
            args.candidate_provenance,
            args.candidate_manifest,
            args.qualification_receipt,
            args.judge_panel,
            args.evaluation_run,
            args.actor_observations,
            args.coverage_trace,
            args.evidence_set,
            args.rubric,
            args.output,
            args.replacement_for,
            verification_input_path=args.verification_input,
            cold_actor_response_paths=args.cold_actor_responses,
            actor_trace_paths=args.actor_traces,
            gold_binding_path=args.gold_binding,
            holdout_consumption_receipt_path=args.holdout_consumption_receipt,
            holdout_registry_before_path=args.holdout_registry_before,
            holdout_registry_after_path=args.holdout_registry_after,
            anonymization_manifest_path=args.anonymization_manifest,
            story_manifest_path=args.story_manifest,
            recording_manifest_paths=args.recording_manifests,
            coverage_action_ledger_path=args.coverage_action_ledger,
            sanitized_evidence_bundle_manifest_path=(
                args.sanitized_evidence_bundle_manifest
            ),
            candidate_judge_input_path=args.candidate_judge_input,
            evaluation_session_claim_path=args.evaluation_session_claim,
        )
    except ValidationFailure as exception:
        raise SystemExit(str(exception)) from exception
    print(json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
