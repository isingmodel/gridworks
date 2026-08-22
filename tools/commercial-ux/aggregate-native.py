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
import json
import os
import re
from pathlib import Path
from typing import Any, Iterable


TOOL_DIRECTORY = Path(__file__).resolve().parent
NATIVE_DIRECTORY = TOOL_DIRECTORY / "native"
DEFAULT_RUBRIC_PATH = TOOL_DIRECTORY / "rubric.json"
SCORECARD_SCHEMA_PATH = NATIVE_DIRECTORY / "native-scorecard.schema.json"
AGGREGATION_INPUT_SCHEMA_PATH = NATIVE_DIRECTORY / "native-aggregation-input.schema.json"
REPLACEMENT_RECEIPT_SCHEMA_PATH = NATIVE_DIRECTORY / "native-replacement-receipt.schema.json"
CANDIDATE_MANIFEST_SCHEMA_PATH = NATIVE_DIRECTORY / "candidate-manifest.schema.json"
QUALIFICATION_RECEIPT_SCHEMA_PATH = NATIVE_DIRECTORY / "qualification-receipt.schema.json"
JUDGE_PANEL_SCHEMA_PATH = NATIVE_DIRECTORY / "judge-panel.schema.json"
EVALUATION_RUN_SCHEMA_PATH = NATIVE_DIRECTORY / "evaluation-run-manifest.schema.json"
ACTOR_OBSERVATION_SCHEMA_PATH = NATIVE_DIRECTORY / "actor-observation.schema.json"
COVERAGE_TRACE_SCHEMA_PATH = NATIVE_DIRECTORY / "coverage-trace.schema.json"
EVIDENCE_SET_SCHEMA_PATH = NATIVE_DIRECTORY / "evidence-set.schema.json"
CONTRACT_VALIDATOR_PATH = NATIVE_DIRECTORY / "validate-contract.py"

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
COVERAGE_TRACE_SCHEMA = "gridworks.commercial-ux.native-coverage-trace.v1"
EVIDENCE_SET_SCHEMA = "gridworks.commercial-ux.native-evidence-set.v1"

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
    "evidenceSetSha256",
    "verificationOutputSha256",
    "oracleHardGateLedgerSha256",
    "nativeAggregatorSha256",
    "executionArtifactSha256",
    "packageSha256",
    "packageStatus",
}

OUTPUT_PROVENANCE_KEYS = INPUT_PROVENANCE_KEYS | {"aggregationInputRawSha256"}

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

VERIFIER_ROW_KEYS = {"observationId", "verdict", "citedSources", "rationale"}
VERIFIER_SOURCE_KEYS = {"anonymousArtifactId", "artifactId", "locator"}

LEDGER_KEYS = {
    "schemaVersion",
    "protocol",
    "ledgerId",
    "candidateManifestSha256",
    "evidenceSetSha256",
    "verificationOutputSha256",
    "rubricSha256",
    "contractBindingsSha256",
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
    for field in INPUT_PROVENANCE_KEYS - {"sourceCommit", "cleanTree", "model", "reasoningEffort", "packageSha256", "packageStatus"}:
        require_sha(provenance[field], f"candidate.provenance.{field}")
    if not isinstance(provenance["sourceCommit"], str) or SOURCE_COMMIT_PATTERN.fullmatch(provenance["sourceCommit"]) is None:
        raise ValidationFailure("candidate.provenance.sourceCommit must be a full lowercase commit")
    if type(provenance["cleanTree"]) is not bool:
        raise ValidationFailure("candidate.provenance.cleanTree must be boolean")
    if provenance["model"] != "gpt-5.6-sol" or provenance["reasoningEffort"] != "ultra":
        raise ValidationFailure("candidate provenance must bind gpt-5.6-sol ultra")
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


def validate_candidate_manifest_authority(
    value: Any,
    raw_bytes: bytes,
    candidate: dict[str, Any],
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
    if (
        execution["executionArtifactSha256"] != provenance["executionArtifactSha256"]
        or execution["packageSha256"] != provenance["packageSha256"]
        or execution["packageStatus"] != provenance["packageStatus"]
    ):
        raise ProvenanceFailure("candidate execution artifact/package mismatch")
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


def validate_actor_observation_authorities(
    inputs: list[tuple[Any, bytes]],
    evaluation_run: dict[str, Any],
) -> list[dict[str, Any]]:
    if len(inputs) != 3:
        raise ProvenanceFailure("exactly three actor observations are required")
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
        for checkpoint in checkpoint_rows:
            action_index = checkpoint["appActiveActionIndex"]
            if action_index != 0 and action_index not in action_by_index:
                raise ProvenanceFailure(f"{label} checkpoint cites an unknown active action")
        for collection_name in ("firstUseRecords", "approvalRecords"):
            for record in value[collection_name]:
                checkpoint = checkpoint_by_ordinal.get(record["checkpointOrdinal"])
                if checkpoint is None or (
                    record["episode"], record["checkpoint"]
                ) != (checkpoint["episode"], checkpoint["checkpoint"]):
                    raise ProvenanceFailure(
                        f"{label} {collection_name} row does not match its checkpoint ordinal"
                    )

        incident_keys = [row["incidentKey"] for row in value["incidents"]]
        if len(set(incident_keys)) != len(incident_keys):
            raise ProvenanceFailure(f"{label} incident keys must be unique")
        if set(terminal["incidentKeys"]) != set(incident_keys):
            raise ProvenanceFailure(f"{label} incident key ledger mismatch")
        severe_rows = []
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
        })
    if observed_raw_hashes != set(artifacts["actorObservationRawSha256"]):
        raise ProvenanceFailure("actor observations must have three distinct raw hashes")
    for row in results:
        row["severeSingleRun"] = False
    return results


def validate_coverage_trace_authority(
    value: Any,
    raw_bytes: bytes,
    candidate_manifest: dict[str, Any],
    evaluation_run: dict[str, Any],
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
        "conceptManifestSha256": recipes["conceptExposureSha256"],
        "goldBindingManifestSha256": recipes["goldStateContractSha256"],
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
    all_occurrence_ids: list[str] = []
    for episode in trace["episodes"]:
        actual = [row["actionOccurrenceId"] for row in episode["traceRows"]]
        expected_actions = expected_by_episode.get(episode["episodeId"])
        if actual != expected_actions:
            raise ProvenanceFailure(
                f"coverage trace {episode['episodeId']} action occurrences/order mismatch"
            )
        all_occurrence_ids.extend(actual)
    if len(set(all_occurrence_ids)) != len(all_occurrence_ids):
        raise ProvenanceFailure("coverage trace actionOccurrenceId values must be globally unique")
    return wrapped


def validate_evidence_set_authority(
    value: Any,
    raw_bytes: bytes,
    candidate: dict[str, Any],
    candidate_manifest: dict[str, Any],
    evaluation_run: dict[str, Any],
    actor_rows: list[dict[str, Any]],
    coverage_trace: dict[str, Any],
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
        "evidenceSetSha256": provenance["evidenceSetSha256"],
        "rubricSha256": provenance["rubricSha256"],
        "conceptManifestSha256": recipes["conceptExposureSha256"],
        "recipeId": candidate["recipeId"],
        "recipeSha256": recipes["selectedRecipeSha256"],
        "holdoutQueueSha256": recipes["holdoutQueueSha256"],
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "executionArtifactSha256": provenance["executionArtifactSha256"],
    }
    for field, expected_value in expected.items():
        if evidence[field] != expected_value:
            raise ProvenanceFailure(f"evidence set {field} mismatch")
    if evaluation_run["artifacts"]["evidenceSetSha256"] != wrapped["selfSha256"]:
        raise ProvenanceFailure("evaluation run evidenceSetSha256 mismatch")
    binding_ids = [row["anonymousArtifactId"] for row in candidate["artifactBindings"]]
    if evidence["artifactOrder"] != binding_ids:
        raise ProvenanceFailure("evidence set artifact order mismatch")
    by_id = {row["anonymousArtifactId"]: row for row in evidence["artifacts"]}
    terminal_by_source = {row["actorArtifactId"]: row for row in actor_rows}
    artifact_authorities: dict[str, dict[str, Any]] = {}
    for binding in candidate["artifactBindings"]:
        artifact = by_id.get(binding["anonymousArtifactId"])
        if artifact is None:
            raise ProvenanceFailure("evidence set is missing a bound anonymous artifact")
        if (
            artifact["artifactKind"] != binding["artifactKind"]
            or artifact["sanitizedArtifactSha256"] != binding["artifactSha256"]
        ):
            raise ProvenanceFailure("evidence set sanitized artifact binding mismatch")
        if artifact["artifactKind"] == "COLD_ACTOR":
            terminal = terminal_by_source.get(artifact["sourceArtifactSha256"])
            if terminal is None:
                raise ProvenanceFailure("evidence cold source artifact is not in evaluation run")
            if (
                artifact["terminalState"] != terminal["terminalState"]
                or artifact["terminalIncidentKey"] != terminal["terminalIncidentKey"]
            ):
                raise ProvenanceFailure("evidence cold terminal state mismatch")
            artifact_authorities[binding["anonymousArtifactId"]] = terminal
        elif artifact["sourceArtifactSha256"] != coverage_trace["selfSha256"]:
            raise ProvenanceFailure("evidence coverage source artifact mismatch")
    wrapped["artifactsById"] = by_id
    wrapped["actorAuthoritiesByAnonymousId"] = artifact_authorities
    return wrapped


def validate_evaluation_run_authority(
    value: Any,
    raw_bytes: bytes,
    candidate: dict[str, Any],
    candidate_manifest: dict[str, Any],
    qualification: dict[str, Any],
    *,
    verifier: Any,
    verifier_raw_sha256: str,
    oracle_raw_sha256: str,
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
        "verificationOutputSha256": verifier_raw_sha256,
        "oracleHardGateLedgerSha256": oracle_raw_sha256,
        "evidenceSetSha256": provenance["evidenceSetSha256"],
    }
    for field, expected_value in raw_expected.items():
        if artifacts[field] != expected_value:
            raise ProvenanceFailure(f"evaluation run artifact {field} mismatch")
    if not isinstance(verifier, dict) or artifacts["verifierRunId"] != verifier.get("verifierRunId"):
        raise ProvenanceFailure("evaluation run verifierRunId mismatch")
    return wrapped


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


def validate_judge_panel_authority(
    value: Any,
    raw_bytes: bytes,
    candidate: dict[str, Any],
    candidate_manifest: dict[str, Any],
    qualification: dict[str, Any],
    evaluation_run: dict[str, Any],
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
) -> dict[str, Any]:
    ledger = exact_keys(value, LEDGER_KEYS, "oracle hard-gate ledger")
    if ledger["schemaVersion"] != ORACLE_LEDGER_SCHEMA or ledger["protocol"] != PROTOCOL:
        raise ValidationFailure("oracle hard-gate ledger identity is invalid")
    require_string(ledger["ledgerId"], "oracle ledger.ledgerId", 200)
    provenance = candidate["provenance"]
    if raw_sha256 != provenance["oracleHardGateLedgerSha256"]:
        raise ProvenanceFailure("oracle hard-gate ledger file hash does not match candidate provenance")
    bindings = {
        "candidateManifestSha256": provenance["candidateManifestSha256"],
        "evidenceSetSha256": provenance["evidenceSetSha256"],
        "verificationOutputSha256": provenance["verificationOutputSha256"],
        "rubricSha256": rubric["sha256"],
    }
    for field, expected in bindings.items():
        if ledger[field] != expected:
            raise ProvenanceFailure(f"oracle ledger {field} does not match candidate provenance")
    require_sha(ledger["contractBindingsSha256"], "oracle ledger.contractBindingsSha256")
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
        if row["verifierStatus"] not in {"SUPPORTED", "PARTIAL", "UNSUPPORTED", "NOT_APPLICABLE"}:
            raise ValidationFailure(f"incidents[{index}].verifierStatus is invalid")
        if row["oracleStatus"] not in {"EXACT", "MISMATCH", "MISSING", "NOT_APPLICABLE"}:
            raise ValidationFailure(f"incidents[{index}].oracleStatus is invalid")
        if row["capCandidate"] not in {100, 79, 69, 49}:
            raise ValidationFailure(f"incidents[{index}].capCandidate is invalid")
        if type(row["critical"]) is not bool:
            raise ValidationFailure(f"incidents[{index}].critical must be boolean")
        require_string(row["description"], f"incidents[{index}].description")
    if type(ledger["scoreBearingReady"]) is not bool:
        raise ValidationFailure("oracle ledger.scoreBearingReady must be boolean")
    if provenance["cleanTree"] is False and gate_map["HG13-PROVENANCE"] != "FAIL":
        raise ProvenanceFailure("dirty candidate tree requires HG13-PROVENANCE=FAIL")
    if any(row["status"] in {"MISMATCH", "MISSING"} for row in checks) and all(
        status == "PASS" for status in gate_map.values()
    ):
        raise ValidationFailure("oracle mismatch/missing cannot coexist with all hard gates PASS")
    return {"value": ledger, "hardGates": gate_map, "incidents": incidents}


def validate_verifier(
    value: Any,
    raw_sha256: str,
    candidate: dict[str, Any],
    panel_sha256: str,
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
    return {
        "value": verifier,
        "supportedOnly": all(verdict == "SUPPORTED" for verdict in verdicts),
        "verdicts": verdicts,
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


def _replacement_receipt_path(output_path: Path, panel_sha256: str) -> Path:
    resolved = output_path.resolve(strict=False)
    return resolved.parent / (
        ".gridworks-commercial-ux-native-replacement-"
        + panel_sha256.removeprefix("sha256:")
        + ".receipt.json"
    )


def _validate_initial_for_replacement(
    path: Path,
    rubric: dict[str, Any],
) -> tuple[dict[str, Any], bytes, Path]:
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
    expected_path = _replacement_receipt_path(resolved, initial["judgePanelSha256"])
    if receipt_path != receipt_path.resolve(strict=False) or receipt_path != expected_path:
        raise ProvenanceFailure("initial replacement receipt path is not canonical/content-addressed")
    return initial, raw, receipt_path


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
    if "COLD-JOURNEY" in required_lanes and old_cold & new_cold:
        raise ProvenanceFailure("cold replacement must use three fresh actor artifacts")
    if "COVERAGE-JOURNEY" in required_lanes and old_coverage == new_coverage:
        raise ProvenanceFailure("coverage replacement must use a fresh coverage artifact")
    if "COLD-JOURNEY" not in required_lanes and old_cold != new_cold:
        raise ProvenanceFailure("coverage-only replacement must preserve all cold artifacts")
    if "COVERAGE-JOURNEY" not in required_lanes and old_coverage != new_coverage:
        raise ProvenanceFailure("cold-only replacement must preserve the coverage artifact")


def _receipt_bytes(
    receipt_path: Path,
    initial_path: Path,
    initial_bytes: bytes,
    initial: dict[str, Any],
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
            "O_EXCL_AFTER_AUTHORITY_PREFLIGHT_BEFORE_ATTEMPT_READ_"
            "THEN_FINALIZE_SAME_DESCRIPTOR"
        ),
        "authorityPreflightStatus": "EXACT_BEFORE_CLAIM",
        "replacementReceiptPath": str(receipt_path),
        "initialAggregatePath": str(initial_path.resolve(strict=True)),
        "initialAggregateRawSha256": bytes_sha256(initial_bytes),
        "initialPanelSha256": initial["judgePanelSha256"],
        "initialEvaluationRunManifestSha256": initial["provenance"]["evaluationRunManifestSha256"],
        "replacementRequiredLanes": initial["replacementRequiredLanes"],
        "candidateManifestSha256": initial["provenance"]["candidateManifestSha256"],
        "qualificationReceiptSha256": qualification_receipt_sha256,
        "evaluationRunManifestSha256": evaluation_run_manifest_sha256,
        "recipeId": initial["recipeId"],
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
        return os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    except FileExistsError as exception:
        raise ValidationFailure(f"native replacement was already consumed: {path}") from exception


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


def _exclusive_write(path: Path, content: bytes, replacement_claimed: bool) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    except FileExistsError as exception:
        detail = " after replacement receipt claim" if replacement_claimed else ""
        raise ValidationFailure(f"native aggregate output already exists{detail}: {path}") from exception
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
    except OSError as exception:
        raise ValidationFailure(f"native aggregate output could not be completed: {path}: {exception}") from exception


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
) -> dict[str, Any]:
    receipt_path = _replacement_receipt_path(output_path, panel_sha256)
    provenance = copy.deepcopy(candidate["provenance"])
    provenance["judgePanelSha256"] = panel_sha256
    provenance["aggregationInputRawSha256"] = aggregation_input_raw_sha256
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


def validate_coverage_trace_schema(value: Any) -> None:
    validate_checked_in_schema(value, COVERAGE_TRACE_SCHEMA_PATH, "coverage trace")


def validate_evidence_set_schema(value: Any) -> None:
    validate_checked_in_schema(value, EVIDENCE_SET_SCHEMA_PATH, "evidence set")


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
    try:
        verified = validate_verifier(
            verifier,
            verifier_raw_sha256,
            candidate,
            panel["panelSha256"],
        )
        if not verified["supportedOnly"]:
            verifier_blocker = "BLOCKED_EVIDENCE_VERIFICATION"
    except VerifierValidationFailure:
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
) -> dict[str, Any]:
    if output_path.exists():
        raise ValidationFailure(f"native aggregate output path must be fresh: {output_path}")
    if len(judgment_paths) != 3 or len(actor_observation_paths) != 3:
        raise ValidationFailure("aggregate requires exactly three judgments and actor observations")

    rubric_value, rubric_bytes = read_json_bytes(rubric_path, "commercial UX rubric")
    rubric = load_rubric(rubric_value, bytes_sha256(rubric_bytes))
    replacement_context: dict[str, Any] | None = None
    receipt_descriptor: int | None = None
    if replacement_for is not None:
        initial, initial_bytes, receipt_path = _validate_initial_for_replacement(
            replacement_for,
            rubric,
        )
        replacement_context = {
            "initial": initial,
            "initialBytes": initial_bytes,
            "receiptPath": receipt_path,
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
    coverage_attempt = read_json_attempt(coverage_trace_path, "coverage trace")
    evidence_attempt = read_json_attempt(evidence_set_path, "evidence set")
    verifier_attempt = read_json_attempt(verifier_path, "native verifier output")
    oracle_attempt = read_json_attempt(oracle_path, "oracle hard-gate ledger")

    candidate_value = _require_valid_attempt(candidate_attempt, "candidate aggregation input")
    candidate = validate_candidate(candidate_value)
    validate_checked_in_contract_hashes(candidate)
    if candidate["provenance"]["rubricSha256"] != rubric["sha256"]:
        raise ProvenanceFailure("candidate rubricSha256 does not match the exact rubric file")
    candidate_manifest_value = _require_valid_attempt(manifest_attempt, "candidate manifest")
    qualification_value = _require_valid_attempt(
        qualification_attempt,
        "qualification receipt",
    )
    evaluation_value = _require_valid_attempt(evaluation_attempt, "evaluation run manifest")
    verifier_value = _require_valid_attempt(verifier_attempt, "native verifier output")
    oracle_value = _require_valid_attempt(oracle_attempt, "oracle hard-gate ledger")
    actor_values = [
        _require_valid_attempt(row, f"actor observation {index + 1}")
        for index, row in enumerate(actor_attempts)
    ]
    coverage_value = _require_valid_attempt(coverage_attempt, "coverage trace")
    evidence_value = _require_valid_attempt(evidence_attempt, "evidence set")

    candidate_authority = validate_candidate_manifest_authority(
        candidate_manifest_value,
        manifest_attempt["rawBytes"],
        candidate,
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
        verifier=verifier_value,
        verifier_raw_sha256=verifier_attempt["rawSha256"],
        oracle_raw_sha256=oracle_attempt["rawSha256"],
    )
    _validate_evaluation_replacement_authority(
        evaluation_authority,
        replacement_context,
    )
    actor_rows = validate_actor_observation_authorities(
        [(value, attempt["rawBytes"]) for value, attempt in zip(actor_values, actor_attempts)],
        evaluation_authority["value"],
    )
    coverage_authority = validate_coverage_trace_authority(
        coverage_value,
        coverage_attempt["rawBytes"],
        candidate_authority["value"],
        evaluation_authority["value"],
    )
    evidence_authority = validate_evidence_set_authority(
        evidence_value,
        evidence_attempt["rawBytes"],
        candidate,
        candidate_authority["value"],
        evaluation_authority["value"],
        actor_rows,
        coverage_authority,
    )
    oracle = validate_oracle_ledger(
        oracle_value,
        oracle_attempt["rawSha256"],
        candidate,
        rubric,
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
    if replacement_context is not None:
        _validate_replacement_candidate_stability(
            replacement_context["initial"],
            candidate,
            rubric,
        )
        _validate_replacement_artifact_freshness(
            replacement_context["initial"],
            candidate,
        )
        receipt_descriptor = _reserve_receipt(replacement_context["receiptPath"])

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
    bind_judge_attempt_transports(
        evaluation_authority["value"],
        panel_attempt,
        judgment_attempts,
    )

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
        receipt_descriptor = None
        _finalize_reserved_receipt(
            descriptor,
            replacement_context["receiptPath"],
            content,
        )
        return receipt_sha

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
    except ValidationFailure as exception:
        if receipt_descriptor is not None:
            finalize_replacement_receipt(
                attempt_outcome,
                failure_code,
                panel_authority["selfSha256"] if panel_authority is not None else _parsed_panel_sha256(panel_attempt),
                panel["runIds"] if panel is not None else _parsed_run_ids(judgment_attempts),
            )
        raise

    qualification_status = (
        "PASS"
        if qualification_authority["status"] == "PASS"
        else "BLOCKED_JUDGE_QUALIFICATION"
    )
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
    _exclusive_write(
        output_path,
        output,
        replacement_claimed=replacement_context is not None,
    )
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
    parser.add_argument("--coverage-trace", required=True, type=Path)
    parser.add_argument("--evidence-set", required=True, type=Path)
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
        )
    except ValidationFailure as exception:
        raise SystemExit(str(exception)) from exception
    print(json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
