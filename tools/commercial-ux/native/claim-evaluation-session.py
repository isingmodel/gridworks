#!/usr/bin/env python3
"""Claim one native UX evaluation session and its irreversible run attempts.

The session claim is created immediately after the holdout receipt is validated.
Every producer attempt must then reserve its fixed opaque slot before it starts.
This tool never captures game evidence and never invokes an LLM.
"""

from __future__ import annotations

import argparse
import copy
import fcntl
import hashlib
import importlib.util
import json
import os
import stat
import sys
from contextlib import contextmanager
from pathlib import Path
from types import ModuleType
from typing import Any, Iterator


PROTOCOL = "GRIDWORKS-COMMERCIAL-UX-v1.1"
NATIVE = Path(__file__).resolve().parent
RUBRIC = NATIVE.parent / "rubric.json"
POLICY_PATH = NATIVE / "evaluation-session-policy.json"
CLAIM_SCHEMA_PATH = NATIVE / "evaluation-session-claim.schema.json"
ATTEMPT_SCHEMA_PATH = NATIVE / "evaluation-attempt-receipt.schema.json"
TERMINAL_SCHEMA_PATH = NATIVE / "evaluation-attempt-terminal.schema.json"
SCORECARD_SCHEMA_PATH = NATIVE / "native-scorecard.schema.json"
PANEL_FINALIZATION_SEAL_SCHEMA_PATH = NATIVE / "panel-finalization-seal.schema.json"
VALIDATOR_PATH = NATIVE / "validate-contract.py"
RETRYABLE_OUTCOMES = {
    "SCHEMA_FAILURE",
    "TRANSPORT_FAILURE",
}
TERMINAL_FAILURE_OUTCOMES = RETRYABLE_OUTCOMES | {
    "HARNESS_FAILURE",
    "ORACLE_FAILURE",
    "INPUT_UNREADABLE",
}
ROLE_OUTPUT_SCHEMAS = {
    "COLD_ACTOR": "cold-actor-response.schema.json",
    "COVERAGE": "coverage-trace.schema.json",
    "CANDIDATE_JUDGE": "native-judge.schema.json",
    "EVIDENCE_VERIFIER": "native-evidence-verifier.schema.json",
    "ORACLE": "oracle-hard-gate-ledger.schema.json",
}
RERUN_STATUS_LANES = {
    "RERUN_REQUIRED_COLD_INSTABILITY": ["COLD-JOURNEY"],
    "RERUN_REQUIRED_COVERAGE_INSTABILITY": ["COVERAGE-JOURNEY"],
    "RERUN_REQUIRED_PANEL_INSTABILITY": [
        "COLD-JOURNEY",
        "COVERAGE-JOURNEY",
    ],
}


class SessionClaimError(ValueError):
    pass


def load_validator() -> ModuleType:
    spec = importlib.util.spec_from_file_location(
        "gridworks_native_contract_validator_for_session_claim",
        VALIDATOR_PATH,
    )
    if spec is None or spec.loader is None:
        raise SessionClaimError("cannot load native contract validator")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def strict_json_bytes(data: bytes, label: str) -> dict[str, Any]:
    def reject_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise SessionClaimError(f"{label} contains duplicate key {key}")
            result[key] = value
        return result

    try:
        value = json.loads(data, object_pairs_hook=reject_duplicates)
    except (UnicodeError, json.JSONDecodeError) as error:
        raise SessionClaimError(f"cannot parse strict JSON {label}: {error}") from error
    if not isinstance(value, dict):
        raise SessionClaimError(f"{label} must contain one JSON object")
    return value


def read_exact(path: Path, label: str) -> tuple[Path, bytes, dict[str, Any]]:
    if not path.is_absolute():
        raise SessionClaimError(f"{label} path must be absolute and canonical")
    try:
        resolved = path.resolve(strict=True)
    except OSError as error:
        raise SessionClaimError(f"cannot resolve {label}: {error}") from error
    if path != resolved or not resolved.is_file() or path.is_symlink():
        raise SessionClaimError(f"{label} must be a canonical regular file")
    reject_symlink_components(path, label)
    try:
        data = resolved.read_bytes()
    except OSError as error:
        raise SessionClaimError(f"cannot read {label}: {error}") from error
    return resolved, data, strict_json_bytes(data, label)


def sha256_bytes(data: bytes) -> str:
    return "sha256:" + hashlib.sha256(data).hexdigest()


def canonical_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def projection_sha256(value: Any) -> str:
    return sha256_bytes(canonical_bytes(value))


def self_hash(value: dict[str, Any], field: str) -> str:
    projected = copy.deepcopy(value)
    if field not in projected:
        raise SessionClaimError(f"self-hash field absent: {field}")
    projected[field] = None
    return projection_sha256(projected)


def json_file_bytes(value: dict[str, Any]) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2).encode("utf-8")
        + b"\n"
    )


def fsync_directory(path: Path) -> None:
    descriptor = os.open(path, os.O_RDONLY)
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def reject_symlink_components(path: Path, label: str) -> None:
    if not path.is_absolute():
        raise SessionClaimError(f"{label} path must be absolute")
    current = Path(path.anchor)
    for component in path.parts[1:]:
        current = current / component
        try:
            mode = current.lstat().st_mode
        except FileNotFoundError:
            continue
        if stat.S_ISLNK(mode):
            raise SessionClaimError(f"symlink is forbidden in {label}: {current}")


def ensure_canonical_directory(path: Path) -> Path:
    if not path.is_absolute():
        raise SessionClaimError(f"directory path must be absolute: {path}")
    path.mkdir(parents=True, exist_ok=True, mode=0o700)
    resolved = path.resolve(strict=True)
    if path != resolved or not resolved.is_dir():
        raise SessionClaimError(f"directory is not canonical: {path}")
    reject_symlink_components(path, "session directory")
    return resolved


def exclusive_create_session_root(path: Path) -> None:
    parent = ensure_canonical_directory(path.parent)
    try:
        os.mkdir(path, 0o700)
    except FileExistsError as error:
        raise SessionClaimError(
            "canonical session root was already consumed before claim"
        ) from error
    fsync_directory(parent)


def exclusive_create_attempt_root(attempt_root: Path, artifact_root: Path) -> None:
    parent = ensure_canonical_directory(attempt_root.parent)
    try:
        os.mkdir(attempt_root, 0o700)
    except FileExistsError as error:
        raise SessionClaimError("attempt root was already consumed before reservation") from error
    fsync_directory(parent)
    try:
        os.mkdir(artifact_root, 0o700)
    except OSError as error:
        raise SessionClaimError(
            "artifact root could not be created exclusively after attempt reservation"
        ) from error
    fsync_directory(attempt_root)


def exclusive_write(path: Path, content: bytes) -> None:
    parent = ensure_canonical_directory(path.parent)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    descriptor = os.open(path, flags, 0o600)
    try:
        view = memoryview(content)
        while view:
            written = os.write(descriptor, view)
            if written <= 0:
                raise OSError("short write while creating exclusive receipt")
            view = view[written:]
        os.fsync(descriptor)
    finally:
        os.close(descriptor)
    fsync_directory(parent)


def reserve_zero_byte_output(path: Path) -> None:
    parent = ensure_canonical_directory(path.parent)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    descriptor = os.open(path, flags, 0o600)
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)
    fsync_directory(parent)


def reserve_zero_byte_file(path: Path) -> int:
    parent = ensure_canonical_directory(path.parent)
    flags = os.O_RDWR | os.O_CREAT | os.O_EXCL
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    descriptor = os.open(path, flags, 0o600)
    os.fsync(descriptor)
    fsync_directory(parent)
    return descriptor


def finalize_reserved_file(descriptor: int, path: Path, content: bytes) -> None:
    try:
        os.lseek(descriptor, 0, os.SEEK_SET)
        view = memoryview(content)
        while view:
            written = os.write(descriptor, view)
            if written <= 0:
                raise OSError("short write while finalizing reserved receipt")
            view = view[written:]
        os.ftruncate(descriptor, len(content))
        os.fsync(descriptor)
    finally:
        os.close(descriptor)
    fsync_directory(path.parent)


def read_regular_exact(path: Path, label: str) -> bytes:
    if not path.is_absolute() or path != path.resolve(strict=False):
        raise SessionClaimError(f"{label} path must be absolute and canonical")
    reject_symlink_components(path, label)
    flags = os.O_RDONLY
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    try:
        descriptor = os.open(path, flags)
    except OSError as error:
        raise SessionClaimError(f"cannot open {label}: {error}") from error
    try:
        before = os.fstat(descriptor)
        if not stat.S_ISREG(before.st_mode):
            raise SessionClaimError(f"{label} must be a regular file")
        chunks: list[bytes] = []
        while True:
            chunk = os.read(descriptor, 1024 * 1024)
            if not chunk:
                break
            chunks.append(chunk)
        after = os.fstat(descriptor)
        if (
            before.st_dev,
            before.st_ino,
            before.st_size,
            before.st_mtime_ns,
        ) != (
            after.st_dev,
            after.st_ino,
            after.st_size,
            after.st_mtime_ns,
        ):
            raise SessionClaimError(f"{label} changed while being sealed")
        data = b"".join(chunks)
        if len(data) != after.st_size:
            raise SessionClaimError(f"{label} byte length changed while being sealed")
        return data
    finally:
        os.close(descriptor)


def _artifact_entries(root: Path) -> list[dict[str, Any]]:
    if not root.is_absolute() or root != root.resolve(strict=True):
        raise SessionClaimError("artifact root must be an existing canonical directory")
    reject_symlink_components(root, "artifact root")
    entries: list[dict[str, Any]] = []
    for directory, directory_names, filenames in os.walk(root, followlinks=False):
        directory_path = Path(directory)
        for name in [*directory_names, *filenames]:
            child = directory_path / name
            if stat.S_ISLNK(child.lstat().st_mode):
                raise SessionClaimError(f"symlink is forbidden in artifact root: {child}")
        for filename in sorted(filenames):
            child = directory_path / filename
            relative = child.relative_to(root).as_posix()
            data = read_regular_exact(child, f"artifact {relative}")
            entries.append({
                "locator": relative,
                "rawSha256": sha256_bytes(data),
                "byteLength": len(data),
            })
    entries.sort(key=lambda row: row["locator"])
    return entries


def artifact_manifest(root: Path) -> tuple[list[dict[str, Any]], str]:
    first = _artifact_entries(root)
    second = _artifact_entries(root)
    if first != second:
        raise SessionClaimError("artifact root changed across the two-pass seal")
    return second, projection_sha256(second)


def classify_output(
    validator: ModuleType,
    role: str,
    output_bytes: bytes,
) -> tuple[str, str | None]:
    if not output_bytes:
        return "INPUT_UNREADABLE", "EMPTY_RESERVED_OUTPUT"
    try:
        value = strict_json_bytes(output_bytes, f"{role} output")
    except SessionClaimError as error:
        return "TRANSPORT_FAILURE", ("STRICT_JSON_FAILURE:" + str(error))[:200]
    schema_name = ROLE_OUTPUT_SCHEMAS.get(role)
    if schema_name is None:
        raise SessionClaimError(f"no frozen output schema for role {role}")
    schema = validator.read_json(NATIVE / schema_name)
    schema_errors = validator.instance_errors(value, schema)
    if schema_errors:
        return "SCHEMA_FAILURE", ("SCHEMA_FAILURE:" + schema_errors[0])[:200]
    return "SUCCESS", None


@contextmanager
def exclusive_session_lock(path: Path) -> Iterator[None]:
    parent = ensure_canonical_directory(path.parent)
    flags = os.O_RDWR | os.O_CREAT
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    descriptor = os.open(path, flags, 0o600)
    try:
        fcntl.flock(descriptor, fcntl.LOCK_EX)
        yield
        os.fsync(descriptor)
    finally:
        fcntl.flock(descriptor, fcntl.LOCK_UN)
        os.close(descriptor)
        fsync_directory(parent)


def canonical_session_base(
    validator: ModuleType,
    native: Path,
    common_dir_override: Path | None = None,
) -> Path:
    if common_dir_override is None:
        common_root = validator.canonical_holdout_registry_path(native).parent
    else:
        common_root = common_dir_override.resolve(strict=False) / "gridworks-commercial-ux"
    return common_root / "evaluation-sessions"


def playable_fingerprint(candidate: dict[str, Any]) -> str:
    return projection_sha256({
        "authorityHashes": candidate.get("authorityHashes"),
        "executionArtifactSha256": candidate.get("execution", {}).get(
            "executionArtifactSha256"
        ),
    })


def session_id(
    receipt_sha256: str,
    mode: str,
    initial_claim_sha256: str | None,
    policy_sha256: str,
) -> str:
    return projection_sha256({
        "evaluationSessionPolicySha256": policy_sha256,
        "holdoutConsumptionReceiptSha256": receipt_sha256,
        "initialSessionClaimSha256": initial_claim_sha256,
        "sessionMode": mode,
    })


def build_slots(root: Path, policy: dict[str, Any]) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    for frozen in policy["slots"]:
        slot_root = root / "slots" / frozen["slotId"].lower()
        attempts: list[dict[str, Any]] = []
        for attempt_ordinal in range(1, policy["attemptBudgetPerSlot"] + 1):
            attempt_root = slot_root / f"attempt-{attempt_ordinal:02d}"
            attempts.append({
                "attemptOrdinal": attempt_ordinal,
                "attemptRoot": str(attempt_root),
                "startReceiptPath": str(attempt_root / "start-receipt.json"),
                "outputPath": str(attempt_root / "output.json"),
                "artifactRoot": str(attempt_root / "artifacts"),
                "terminalReceiptPath": str(attempt_root / "terminal-receipt.json"),
            })
        result.append({
            **frozen,
            "slotRoot": str(slot_root),
            "attemptBudget": policy["attemptBudgetPerSlot"],
            "attempts": attempts,
        })
    return result


def required_fresh_slot_ids(
    mode: str,
    initial_reference: dict[str, Any] | None,
    policy: dict[str, Any],
) -> list[str]:
    """Derive the only slots that a claimed session may execute.

    INITIAL captures every lane.  REPLACEMENT captures only the unstable
    evidence lane(s), while judges, the evidence verifier, and the oracle are
    always fresh.  Stable evidence lanes are reused from the exact sealed
    INITIAL attempt chain rather than copied into a new attempt root.
    """

    rule = policy["freshSlotRule"]
    if mode == "INITIAL":
        return list(rule["initial"])
    if mode != "REPLACEMENT" or initial_reference is None:
        raise SessionClaimError("replacement fresh slots require initial finalization")
    selected = set(rule["replacementAlways"])
    for lane in initial_reference["replacementRequiredLanes"]:
        try:
            selected.update(rule["replacementByLane"][lane])
        except KeyError as error:
            raise SessionClaimError(f"unknown replacement lane: {lane}") from error
    return [slot_id for slot_id in rule["initial"] if slot_id in selected]


def validate_schema(
    validator: ModuleType,
    value: dict[str, Any],
    schema_path: Path,
    label: str,
) -> None:
    schema = validator.read_json(schema_path)
    errors = validator.instance_errors(value, schema)
    if errors:
        raise SessionClaimError(f"{label} schema failure: {'; '.join(errors)}")


def read_and_validate_initial_finalization(
    *,
    validator: ModuleType,
    initial_claim: dict[str, Any],
    initial_claim_path: Path,
    initial_claim_raw_bytes: bytes,
    scorecard_path: Path,
    panel_finalization_seal_path: Path,
    expected_reference: dict[str, Any] | None = None,
) -> dict[str, Any]:
    """Bind a replacement to the exact finalized INITIAL scoring decision.

    Native scorecards are not self-hashed envelopes, so their exact raw-byte
    hash is the scorecard authority. The panel-finalization seal is both
    exact-byte bound and checked against its declared canonical self-hash.
    """

    if initial_claim.get("sessionMode") != "INITIAL":
        raise SessionClaimError("replacement finalization source must be INITIAL")
    fixed = initial_claim.get("fixedArtifactPaths")
    if not isinstance(fixed, dict):
        raise SessionClaimError("initial claim lacks fixed artifact paths")
    try:
        expected_scorecard_path = Path(fixed["scorecard"])
        expected_seal_path = Path(fixed["panelFinalizationSeal"])
    except (KeyError, TypeError) as error:
        raise SessionClaimError(
            "initial claim lacks scorecard/panel finalization fixed paths"
        ) from error

    scorecard_resolved, scorecard_bytes, scorecard = read_exact(
        scorecard_path,
        "initial native scorecard",
    )
    seal_resolved, seal_bytes, seal = read_exact(
        panel_finalization_seal_path,
        "initial panel finalization seal",
    )
    if scorecard_resolved != expected_scorecard_path:
        raise SessionClaimError(
            "initial scorecard path differs from the INITIAL claim fixed path"
        )
    if seal_resolved != expected_seal_path:
        raise SessionClaimError(
            "initial panel finalization seal path differs from the INITIAL claim fixed path"
        )
    validate_schema(
        validator,
        scorecard,
        SCORECARD_SCHEMA_PATH,
        "initial native scorecard",
    )
    validate_schema(
        validator,
        seal,
        PANEL_FINALIZATION_SEAL_SCHEMA_PATH,
        "initial panel finalization seal",
    )

    status = scorecard.get("status")
    expected_lanes = RERUN_STATUS_LANES.get(status)
    if (
        scorecard.get("panelKind") != "INITIAL"
        or scorecard.get("rerunRequired") is not True
        or expected_lanes is None
        or scorecard.get("replacementRequiredLanes") != expected_lanes
    ):
        raise SessionClaimError(
            "replacement requires a finalized INITIAL rerun scorecard with exact required lanes"
        )
    provenance = scorecard.get("provenance")
    if not isinstance(provenance, dict):
        raise SessionClaimError("initial scorecard lacks session provenance")
    initial_claim_raw_sha = sha256_bytes(initial_claim_raw_bytes)
    expected_session_provenance = {
        "evaluationSessionClaimSha256": initial_claim.get(
            "evaluationSessionClaimSha256"
        ),
        "evaluationSessionClaimRawSha256": initial_claim_raw_sha,
        "evaluationSessionPolicySha256": initial_claim.get(
            "evaluationSessionPolicySha256"
        ),
        "evaluationSessionClaimToolSha256": initial_claim.get(
            "sessionClaimToolSha256"
        ),
        "evaluationSessionId": initial_claim.get("sessionId"),
        "evaluationSessionMode": "INITIAL",
    }
    for field, expected in expected_session_provenance.items():
        if provenance.get(field) != expected:
            raise SessionClaimError(
                f"initial scorecard session provenance mismatch: {field}"
            )
    for field in (
        "evaluationAttemptAuditSha256",
        "evaluationSelectedAttemptsSha256",
    ):
        value = provenance.get(field)
        if not isinstance(value, str) or not value.startswith("sha256:"):
            raise SessionClaimError(f"initial scorecard lacks valid {field}")

    scorecard_claim_fields = {
        "candidateManifestSha256": "candidateManifestSha256",
        "candidateManifestRawSha256": "candidateManifestRawSha256",
        "sourceCommit": "sourceCommit",
        "executionArtifactSha256": "executionArtifactSha256",
        "holdoutConsumptionReceiptSha256": "holdoutConsumptionReceiptSha256",
        "holdoutConsumptionReceiptRawSha256": (
            "holdoutConsumptionReceiptRawSha256"
        ),
    }
    for provenance_field, claim_field in scorecard_claim_fields.items():
        if provenance.get(provenance_field) != initial_claim.get(claim_field):
            raise SessionClaimError(
                f"initial scorecard INITIAL claim binding mismatch: {provenance_field}"
            )
    if (
        scorecard.get("recipeId") != initial_claim.get("selectedRecipeId")
        or scorecard.get("officialCommercialUX")
        is not initial_claim.get("officialCommercialUX")
    ):
        raise SessionClaimError("initial scorecard recipe/official binding mismatch")

    if seal.get("panelFinalizationSealSha256") != self_hash(
        seal,
        "panelFinalizationSealSha256",
    ):
        raise SessionClaimError("initial panel finalization seal self-hash mismatch")
    expected_seal_fields = {
        "panelFinalizationSealSchemaSha256": sha256_bytes(
            PANEL_FINALIZATION_SEAL_SCHEMA_PATH.read_bytes()
        ),
        "canonicalSealPath": str(seal_resolved),
        "panelKind": "INITIAL",
        "initialPanelSha256": scorecard.get("judgePanelSha256"),
        "judgePanelSha256": scorecard.get("judgePanelSha256"),
        "scorecardId": scorecard.get("scorecardId"),
        "recipeId": scorecard.get("recipeId"),
        "officialCommercialUX": scorecard.get("officialCommercialUX"),
        "sourceCommit": provenance.get("sourceCommit"),
        "candidateManifestSha256": provenance.get("candidateManifestSha256"),
        "candidateManifestRawSha256": provenance.get(
            "candidateManifestRawSha256"
        ),
        "holdoutConsumptionReceiptSha256": provenance.get(
            "holdoutConsumptionReceiptSha256"
        ),
        "holdoutConsumptionReceiptRawSha256": provenance.get(
            "holdoutConsumptionReceiptRawSha256"
        ),
        "goldBindingManifestSha256": provenance.get(
            "goldBindingManifestSha256"
        ),
        "goldBindingManifestRawSha256": provenance.get(
            "goldBindingManifestRawSha256"
        ),
        "qualificationReceiptSha256": provenance.get(
            "qualificationReceiptSha256"
        ),
        "qualificationReceiptRawSha256": provenance.get(
            "qualificationReceiptRawSha256"
        ),
        "evaluationRunManifestSha256": provenance.get(
            "evaluationRunManifestSha256"
        ),
        "evaluationRunManifestRawSha256": provenance.get(
            "evaluationRunManifestRawSha256"
        ),
        "anonymizationManifestSha256": provenance.get(
            "anonymizationManifestSha256"
        ),
        "anonymizationManifestRawSha256": provenance.get(
            "anonymizationManifestRawSha256"
        ),
        "evidenceSetSha256": provenance.get("evidenceSetSha256"),
        "evidenceSetRawSha256": provenance.get("evidenceSetRawSha256"),
        "sanitizedEvidenceBundleManifestSha256": provenance.get(
            "sanitizedEvidenceBundleManifestSha256"
        ),
        "sanitizedEvidenceBundleManifestRawSha256": provenance.get(
            "sanitizedEvidenceBundleManifestRawSha256"
        ),
        "sanitizedEvidenceContentRootSha256": provenance.get(
            "sanitizedEvidenceContentRootSha256"
        ),
        "verificationInputSha256": scorecard.get("verificationInputSha256"),
        "verificationInputRawSha256": provenance.get(
            "verificationInputRawSha256"
        ),
        "aggregationInputRawSha256": provenance.get(
            "aggregationInputRawSha256"
        ),
        "evaluationSessionClaimSha256": provenance.get(
            "evaluationSessionClaimSha256"
        ),
        "evaluationSessionClaimRawSha256": provenance.get(
            "evaluationSessionClaimRawSha256"
        ),
        "evaluationSessionPolicySha256": provenance.get(
            "evaluationSessionPolicySha256"
        ),
        "evaluationSessionClaimToolSha256": provenance.get(
            "evaluationSessionClaimToolSha256"
        ),
        "evaluationSessionId": provenance.get("evaluationSessionId"),
        "evaluationSessionMode": "INITIAL",
        "evaluationAttemptAuditSha256": provenance.get(
            "evaluationAttemptAuditSha256"
        ),
        "evaluationSelectedAttemptsSha256": provenance.get(
            "evaluationSelectedAttemptsSha256"
        ),
        "scorecardPath": str(scorecard_resolved),
        "scorecardRawSha256": sha256_bytes(scorecard_bytes),
        "scorecardStatus": status,
        "replacementReceiptPath": None,
        "replacementReceiptRawSha256": None,
        "sealStatus": "FINALIZED",
    }
    for field, expected in expected_seal_fields.items():
        if seal.get(field) != expected:
            raise SessionClaimError(
                f"initial panel finalization seal mismatch: {field}"
            )

    # Close the read/read validation race before the replacement claim is made.
    if read_regular_exact(scorecard_resolved, "initial native scorecard") != scorecard_bytes:
        raise SessionClaimError("initial scorecard changed while replacement was claimed")
    if read_regular_exact(seal_resolved, "initial panel finalization seal") != seal_bytes:
        raise SessionClaimError(
            "initial panel finalization seal changed while replacement was claimed"
        )

    reference = {
        "claimPath": str(initial_claim_path),
        "claimSha256": initial_claim["evaluationSessionClaimSha256"],
        "claimRawSha256": initial_claim_raw_sha,
        "sessionId": initial_claim["sessionId"],
        "scorecardPath": str(scorecard_resolved),
        "scorecardRawSha256": sha256_bytes(scorecard_bytes),
        "scorecardStatus": status,
        "scorecardId": scorecard["scorecardId"],
        "judgePanelSha256": scorecard["judgePanelSha256"],
        "replacementRequiredLanes": list(expected_lanes),
        "panelFinalizationSealPath": str(seal_resolved),
        "panelFinalizationSealSha256": seal["panelFinalizationSealSha256"],
        "panelFinalizationSealRawSha256": sha256_bytes(seal_bytes),
        "evaluationSessionPolicySha256": provenance[
            "evaluationSessionPolicySha256"
        ],
        "evaluationSessionClaimToolSha256": provenance[
            "evaluationSessionClaimToolSha256"
        ],
        "evaluationSessionMode": provenance["evaluationSessionMode"],
        "evaluationAttemptAuditSha256": provenance[
            "evaluationAttemptAuditSha256"
        ],
        "evaluationSelectedAttemptsSha256": provenance[
            "evaluationSelectedAttemptsSha256"
        ],
    }
    if expected_reference is not None and expected_reference != reference:
        raise SessionClaimError(
            "replacement initialSession reference differs from exact INITIAL finalization bytes"
        )
    return reference


def create_session_claim(
    *,
    native: Path,
    rubric: Path,
    candidate_path: Path,
    holdout_receipt_path: Path,
    registry_before_path: Path,
    registry_after_path: Path,
    mode: str,
    initial_claim_path: Path | None = None,
    initial_scorecard_path: Path | None = None,
    initial_panel_finalization_seal_path: Path | None = None,
    common_dir_override: Path | None = None,
) -> tuple[Path, dict[str, Any]]:
    validator = load_validator()
    candidate_resolved, candidate_bytes, candidate = read_exact(
        candidate_path, "candidate manifest"
    )
    receipt_resolved, receipt_bytes, receipt = read_exact(
        holdout_receipt_path, "holdout consumption receipt"
    )
    before_resolved, before_bytes, _ = read_exact(
        registry_before_path, "holdout registry before"
    )
    after_resolved, after_bytes, registry_after = read_exact(
        registry_after_path, "holdout registry after"
    )
    runtime_errors, _ = validator.validate_runtime_contract_bytes(
        native,
        rubric,
        candidate_manifest_bytes=candidate_bytes,
        holdout_consumption_receipt_bytes=receipt_bytes,
        registry_before_bytes=before_bytes,
        registry_after_bytes=after_bytes,
        candidate_manifest_path_label=candidate_resolved,
        holdout_consumption_receipt_path_label=receipt_resolved,
        registry_before_path_label=before_resolved,
        registry_after_path_label=after_resolved,
    )
    if runtime_errors:
        raise SessionClaimError(
            "candidate/holdout preflight failed: " + "; ".join(runtime_errors)
        )

    fingerprint = playable_fingerprint(candidate)
    if receipt.get("candidatePlayableFingerprintSha256") != fingerprint:
        raise SessionClaimError("holdout receipt playable fingerprint mismatch")
    if receipt.get("sourceCommit") != candidate.get("source", {}).get("commit"):
        raise SessionClaimError("holdout receipt source commit mismatch")
    if receipt.get("evaluationPhase") == "OFFICIAL_HOLDOUT":
        matching_rows = [
            row for row in registry_after.get("consumptions", [])
            if isinstance(row, dict)
            and row.get("candidatePlayableFingerprintSha256") == fingerprint
            and row.get("sourceCommit") == receipt.get("sourceCommit")
            and row.get("transactionId") == receipt.get("atomicClaim", {}).get("transactionId")
        ]
        if len(matching_rows) != 1:
            raise SessionClaimError(
                "official registry does not contain exactly one matching source/fingerprint consumption"
            )

    policy_bytes = POLICY_PATH.read_bytes()
    policy = strict_json_bytes(policy_bytes, "evaluation session policy")
    claim_schema_bytes = CLAIM_SCHEMA_PATH.read_bytes()
    tool_bytes = Path(__file__).resolve().read_bytes()
    receipt_sha = receipt["holdoutConsumptionReceiptSha256"]
    base = canonical_session_base(validator, native, common_dir_override)
    receipt_root = base / receipt_sha.removeprefix("sha256:")
    initial_root = receipt_root / "initial"

    initial_reference: dict[str, Any] | None = None
    initial_claim_value: dict[str, Any] | None = None
    initial_claim_bytes: bytes | None = None
    initial_claim_resolved: Path | None = None
    if mode == "INITIAL":
        if any(path is not None for path in (
            initial_claim_path,
            initial_scorecard_path,
            initial_panel_finalization_seal_path,
        )):
            raise SessionClaimError(
                "INITIAL session must not specify initial claim/finalization artifacts"
            )
        root = initial_root
        initial_sha: str | None = None
    elif mode == "REPLACEMENT":
        if (
            initial_claim_path is None
            or initial_scorecard_path is None
            or initial_panel_finalization_seal_path is None
        ):
            raise SessionClaimError(
                "REPLACEMENT session requires the initial claim, scorecard, and panel finalization seal"
            )
        initial_resolved, initial_bytes, initial_claim = read_exact(
            initial_claim_path, "initial evaluation session claim"
        )
        initial_claim_value = initial_claim
        initial_claim_bytes = initial_bytes
        initial_claim_resolved = initial_resolved
        validate_schema(
            validator, initial_claim, CLAIM_SCHEMA_PATH, "initial evaluation session claim"
        )
        semantic_errors: list[str] = []
        validator.validate_evaluation_session_claim_semantics(
            initial_claim,
            initial_resolved,
            native,
            candidate,
            receipt,
            semantic_errors,
            common_dir_override=common_dir_override,
        )
        if semantic_errors:
            raise SessionClaimError(
                "initial session claim semantic failure: " + "; ".join(semantic_errors)
            )
        if initial_claim.get("sessionMode") != "INITIAL":
            raise SessionClaimError("replacement must link an INITIAL session claim")
        if initial_claim.get("holdoutConsumptionReceiptSha256") != receipt_sha:
            raise SessionClaimError("replacement must reuse the initial holdout receipt")
        if (
            initial_claim.get("candidateManifestSha256")
            != candidate.get("candidateManifestSha256")
            or initial_claim.get("candidatePlayableFingerprintSha256") != fingerprint
            or initial_claim.get("sourceCommit") != receipt.get("sourceCommit")
        ):
            raise SessionClaimError("replacement candidate must equal the initial session candidate")
        initial_sha = initial_claim["evaluationSessionClaimSha256"]
        initial_reference = read_and_validate_initial_finalization(
            validator=validator,
            initial_claim=initial_claim,
            initial_claim_path=initial_resolved,
            initial_claim_raw_bytes=initial_bytes,
            scorecard_path=initial_scorecard_path,
            panel_finalization_seal_path=initial_panel_finalization_seal_path,
        )
        root = initial_root.parent / "replacement-01"
    else:
        raise SessionClaimError(f"unknown session mode: {mode}")

    claim_path = receipt_root / (
        "initial-claim.json" if mode == "INITIAL" else "replacement-01-claim.json"
    )
    replacement_claim_path = receipt_root / "replacement-01-claim.json"
    fixed_paths = {
        key: str(root / "artifacts" / name)
        for key, name in policy["fixedArtifactNames"].items()
    }
    claim: dict[str, Any] = {
        "schemaVersion": "gridworks.commercial-ux.evaluation-session-claim.v1",
        "protocol": PROTOCOL,
        "evaluationSessionClaimSha256": "sha256:" + "0" * 64,
        "evaluationSessionClaimSchemaSha256": sha256_bytes(claim_schema_bytes),
        "evaluationSessionPolicySha256": sha256_bytes(policy_bytes),
        "sessionClaimToolSha256": sha256_bytes(tool_bytes),
        "policyId": policy["policyId"],
        "sessionId": session_id(receipt_sha, mode, initial_sha, sha256_bytes(policy_bytes)),
        "sessionMode": mode,
        "evaluationPhase": receipt["evaluationPhase"],
        "officialCommercialUX": receipt["officialCommercialUX"],
        "candidateId": candidate["candidateId"],
        "candidateManifestSha256": candidate["candidateManifestSha256"],
        "candidateManifestRawSha256": sha256_bytes(candidate_bytes),
        "sourceCommit": candidate["source"]["commit"],
        "candidatePlayableFingerprintSha256": fingerprint,
        "executionArtifactSha256": candidate["execution"]["executionArtifactSha256"],
        "authorityHashesSha256": projection_sha256(candidate["authorityHashes"]),
        "holdoutConsumptionReceiptSha256": receipt_sha,
        "holdoutConsumptionReceiptRawSha256": sha256_bytes(receipt_bytes),
        "selectedRecipeId": receipt["selectedRecipe"]["recipeId"],
        "selectedRecipeSha256": receipt["selectedRecipe"]["selectedRecipeSha256"],
        "canonicalSessionRoot": str(root),
        "canonicalClaimPath": str(claim_path),
        "initialSession": initial_reference,
        "replacementClaimPath": str(replacement_claim_path),
        "sessionLockPath": str(root / "session.lock"),
        "requiredFreshSlotIds": required_fresh_slot_ids(
            mode,
            initial_reference,
            policy,
        ),
        "slots": build_slots(root, policy),
        "fixedArtifactPaths": fixed_paths,
        "atomicClaim": {
            "claimPolicy": "HOLDOUT_EXACT_BYTES_VALIDATED_THEN_CLAIM_O_EXCL_FSYNC_BEFORE_SESSION_ROOT_MKDIR_OR_ARTIFACTS",
            "holdoutValidatedBeforeClaim": True,
            "sessionRootAbsentBeforeClaim": True,
            "claimPrecedesSessionRootCreation": True,
            "claimPathAbsentBeforeOpen": True,
            "exclusiveCreateCompleted": True,
            "claimFsyncCompleted": True,
            "parentDirectoryFsyncCompleted": True,
        },
        "status": "CLAIMED_BEFORE_CAPTURE",
    }
    claim["evaluationSessionClaimSha256"] = self_hash(
        claim, "evaluationSessionClaimSha256"
    )
    validate_schema(validator, claim, CLAIM_SCHEMA_PATH, "evaluation session claim")
    semantic_errors = []
    validator.validate_evaluation_session_claim_semantics(
        claim,
        claim_path,
        native,
        candidate,
        receipt,
        semantic_errors,
        common_dir_override=common_dir_override,
        initial_session_claim=initial_claim_value,
        initial_session_claim_raw_bytes=initial_claim_bytes,
        initial_session_claim_path_label=initial_claim_resolved,
    )
    if semantic_errors:
        raise SessionClaimError(
            "evaluation session claim semantic failure: " + "; ".join(semantic_errors)
        )
    if os.path.lexists(root):
        raise SessionClaimError("canonical session root existed before the claim")
    exclusive_write(claim_path, json_file_bytes(claim))
    exclusive_create_session_root(root)
    return claim_path, claim


def read_and_validate_claim(
    claim_path: Path,
    *,
    native: Path,
    common_dir_override: Path | None = None,
) -> tuple[ModuleType, Path, bytes, dict[str, Any]]:
    validator = load_validator()
    resolved, raw_bytes, claim = read_exact(claim_path, "evaluation session claim")
    validate_schema(validator, claim, CLAIM_SCHEMA_PATH, "evaluation session claim")
    initial_value: dict[str, Any] | None = None
    initial_bytes: bytes | None = None
    initial_path: Path | None = None
    if claim.get("sessionMode") == "REPLACEMENT":
        initial_link = claim.get("initialSession")
        if not isinstance(initial_link, dict) or not isinstance(
            initial_link.get("claimPath"), str
        ):
            raise SessionClaimError("replacement session lacks its initial claim path")
        initial_path, initial_bytes, initial_value = read_exact(
            Path(initial_link["claimPath"]), "replacement initial session claim"
        )
    errors: list[str] = []
    validator.validate_evaluation_session_claim_semantics(
        claim,
        resolved,
        native,
        None,
        None,
        errors,
        common_dir_override=common_dir_override,
        initial_session_claim=initial_value,
        initial_session_claim_raw_bytes=initial_bytes,
        initial_session_claim_path_label=initial_path,
    )
    if errors:
        raise SessionClaimError("evaluation session claim semantic failure: " + "; ".join(errors))
    if claim.get("sessionMode") == "REPLACEMENT":
        assert isinstance(initial_value, dict)
        assert initial_bytes is not None and initial_path is not None
        initial_link = claim["initialSession"]
        read_and_validate_initial_finalization(
            validator=validator,
            initial_claim=initial_value,
            initial_claim_path=initial_path,
            initial_claim_raw_bytes=initial_bytes,
            scorecard_path=Path(initial_link["scorecardPath"]),
            panel_finalization_seal_path=Path(
                initial_link["panelFinalizationSealPath"]
            ),
            expected_reference=initial_link,
        )
    return validator, resolved, raw_bytes, claim


def find_attempt(
    claim: dict[str, Any], slot_id: str, attempt_ordinal: int
) -> tuple[dict[str, Any], dict[str, Any]]:
    slots = [row for row in claim["slots"] if row["slotId"] == slot_id]
    if len(slots) != 1:
        raise SessionClaimError(f"unknown or duplicated slot {slot_id}")
    attempts = [
        row for row in slots[0]["attempts"]
        if row["attemptOrdinal"] == attempt_ordinal
    ]
    if len(attempts) != 1:
        raise SessionClaimError(
            f"unknown or duplicated attempt {slot_id}/{attempt_ordinal}"
        )
    return slots[0], attempts[0]


def build_terminal_value(
    *,
    claim: dict[str, Any],
    slot: dict[str, Any],
    attempt: dict[str, Any],
    start: dict[str, Any],
    start_bytes: bytes,
    output_bytes: bytes | None,
    outcome: str,
    failure_code: str | None,
) -> dict[str, Any]:
    artifact_entries, artifact_root_sha = artifact_manifest(
        Path(attempt["artifactRoot"])
    )
    if outcome == "SUCCESS":
        if failure_code is not None or not output_bytes:
            raise SessionClaimError("successful attempt requires non-empty output and no failure")
        output_sha: str | None = sha256_bytes(output_bytes)
    else:
        if outcome not in TERMINAL_FAILURE_OUTCOMES:
            raise SessionClaimError(f"unknown attempt outcome: {outcome}")
        if not failure_code:
            raise SessionClaimError("failed attempt requires a failure code")
        output_sha = sha256_bytes(output_bytes) if output_bytes else None
    terminal_path = Path(attempt["terminalReceiptPath"])
    terminal: dict[str, Any] = {
        "schemaVersion": "gridworks.commercial-ux.evaluation-attempt-terminal.v1",
        "protocol": PROTOCOL,
        "evaluationAttemptTerminalSha256": "sha256:" + "0" * 64,
        "evaluationAttemptTerminalSchemaSha256": sha256_bytes(
            TERMINAL_SCHEMA_PATH.read_bytes()
        ),
        "evaluationSessionPolicySha256": claim["evaluationSessionPolicySha256"],
        "sessionClaimToolSha256": sha256_bytes(Path(__file__).resolve().read_bytes()),
        "evaluationAttemptReceiptSha256": start["evaluationAttemptReceiptSha256"],
        "evaluationAttemptReceiptRawSha256": sha256_bytes(start_bytes),
        "evaluationSessionClaimSha256": claim["evaluationSessionClaimSha256"],
        "sessionId": claim["sessionId"],
        "sessionMode": claim["sessionMode"],
        "slotId": slot["slotId"],
        "role": slot["role"],
        "roleOrdinal": slot["roleOrdinal"],
        "attemptOrdinal": attempt["attemptOrdinal"],
        "canonicalTerminalReceiptPath": str(terminal_path),
        "canonicalOutputPath": attempt["outputPath"],
        "canonicalArtifactRoot": attempt["artifactRoot"],
        "artifactEntries": artifact_entries,
        "artifactEntryCount": len(artifact_entries),
        "artifactContentRootSha256": artifact_root_sha,
        "outputRawSha256": output_sha,
        "outputByteLength": len(output_bytes) if output_bytes is not None else 0,
        "outcome": outcome,
        "failureCode": failure_code,
        "nextAttemptAllowed": outcome in RETRYABLE_OUTCOMES
        and attempt["attemptOrdinal"] < 3,
        "terminalization": {
            "policy": "TERMINAL_PATH_ZERO_BYTE_O_EXCL_FSYNC_BEFORE_OUTPUT_OBSERVATION_THEN_EXACT_TERMINAL_WRITE_FSYNC",
            "sessionLockAcquired": True,
            "terminalPathAbsentBeforeOpen": True,
            "terminalReservedBeforeOutputObservation": True,
            "exclusiveCreateCompleted": True,
            "terminalFsyncCompleted": True,
            "parentDirectoryFsyncCompleted": True,
        },
        "status": "TERMINAL",
    }
    terminal["evaluationAttemptTerminalSha256"] = self_hash(
        terminal, "evaluationAttemptTerminalSha256"
    )
    return terminal


def build_start_receipt(
    *,
    claim: dict[str, Any],
    claim_bytes: bytes,
    slot: dict[str, Any],
    attempt: dict[str, Any],
    predecessor_sha256: str | None,
    output_reserved_exclusively: bool,
) -> dict[str, Any]:
    receipt: dict[str, Any] = {
        "schemaVersion": "gridworks.commercial-ux.evaluation-attempt-receipt.v1",
        "protocol": PROTOCOL,
        "evaluationAttemptReceiptSha256": "sha256:" + "0" * 64,
        "evaluationAttemptReceiptSchemaSha256": sha256_bytes(
            ATTEMPT_SCHEMA_PATH.read_bytes()
        ),
        "evaluationSessionPolicySha256": claim["evaluationSessionPolicySha256"],
        "sessionClaimToolSha256": sha256_bytes(Path(__file__).resolve().read_bytes()),
        "evaluationSessionClaimSha256": claim["evaluationSessionClaimSha256"],
        "evaluationSessionClaimRawSha256": sha256_bytes(claim_bytes),
        "sessionId": claim["sessionId"],
        "sessionMode": claim["sessionMode"],
        "candidateManifestSha256": claim["candidateManifestSha256"],
        "candidatePlayableFingerprintSha256": claim[
            "candidatePlayableFingerprintSha256"
        ],
        "sourceCommit": claim["sourceCommit"],
        "holdoutConsumptionReceiptSha256": claim[
            "holdoutConsumptionReceiptSha256"
        ],
        "slotId": slot["slotId"],
        "role": slot["role"],
        "roleOrdinal": slot["roleOrdinal"],
        "attemptOrdinal": attempt["attemptOrdinal"],
        "attemptRoot": attempt["attemptRoot"],
        "canonicalReceiptPath": attempt["startReceiptPath"],
        "canonicalOutputPath": attempt["outputPath"],
        "canonicalArtifactRoot": attempt["artifactRoot"],
        "canonicalTerminalReceiptPath": attempt["terminalReceiptPath"],
        "predecessorTerminalReceiptSha256": predecessor_sha256,
        "reservation": {
            "policy": "SESSION_LOCK_THEN_ZERO_BYTE_OUTPUT_O_EXCL_FSYNC_THEN_START_RECEIPT_O_EXCL_FSYNC_BEFORE_PRODUCER_START",
            "sessionLockAcquired": True,
            "receiptPathAbsentBeforeOpen": True,
            "outputPathAbsentBeforeOpen": output_reserved_exclusively,
            "receiptExclusiveCreateCompleted": True,
            "receiptFsyncCompleted": True,
            "receiptParentDirectoryFsyncCompleted": True,
            "outputReservationExclusiveCreateCompleted": output_reserved_exclusively,
            "outputReservationFsyncCompleted": output_reserved_exclusively,
            "outputReservationParentDirectoryFsyncCompleted": output_reserved_exclusively,
            "outputReservationByteLength": 0,
        },
        "status": (
            "STARTED_BEFORE_PRODUCER"
            if output_reserved_exclusively
            else "RESERVATION_FAILED_TERMINAL_REQUIRED"
        ),
    }
    receipt["evaluationAttemptReceiptSha256"] = self_hash(
        receipt, "evaluationAttemptReceiptSha256"
    )
    return receipt


def reserve_attempt(
    *,
    native: Path,
    claim_path: Path,
    slot_id: str,
    attempt_ordinal: int,
    common_dir_override: Path | None = None,
) -> tuple[Path, dict[str, Any]]:
    validator, _, claim_bytes, claim = read_and_validate_claim(
        claim_path, native=native, common_dir_override=common_dir_override
    )
    if slot_id not in claim["requiredFreshSlotIds"]:
        raise SessionClaimError(
            f"{slot_id} is a reused stable lane and cannot be reserved in this session"
        )
    slot, attempt = find_attempt(claim, slot_id, attempt_ordinal)
    predecessor_sha: str | None = None
    if attempt_ordinal > 1:
        _, predecessor = find_attempt(claim, slot_id, attempt_ordinal - 1)
        predecessor_path = Path(predecessor["terminalReceiptPath"])
        _, predecessor_bytes, predecessor_value = read_exact(
            predecessor_path, "predecessor terminal receipt"
        )
        validate_schema(
            validator,
            predecessor_value,
            TERMINAL_SCHEMA_PATH,
            "predecessor terminal receipt",
        )
        if (
            predecessor_value.get("slotId") != slot_id
            or predecessor_value.get("attemptOrdinal") != attempt_ordinal - 1
            or predecessor_value.get("evaluationSessionClaimSha256")
            != claim["evaluationSessionClaimSha256"]
            or predecessor_value.get("nextAttemptAllowed") is not True
        ):
            raise SessionClaimError("predecessor terminal receipt does not authorize retry")
        expected = self_hash(predecessor_value, "evaluationAttemptTerminalSha256")
        if predecessor_value.get("evaluationAttemptTerminalSha256") != expected:
            raise SessionClaimError("predecessor terminal receipt self-hash mismatch")
        predecessor_sha = predecessor_value["evaluationAttemptTerminalSha256"]
        if sha256_bytes(predecessor_bytes) == sha256_bytes(b""):
            raise SessionClaimError("predecessor terminal receipt cannot be empty")

    receipt_path = Path(attempt["startReceiptPath"])
    output_path = Path(attempt["outputPath"])
    terminal_path = Path(attempt["terminalReceiptPath"])
    attempt_root = Path(attempt["attemptRoot"])
    artifact_root = Path(attempt["artifactRoot"])
    lock_path = Path(claim["sessionLockPath"])
    with exclusive_session_lock(lock_path):
        if any(
            os.path.lexists(str(path))
            for path in (attempt_root, receipt_path, output_path, artifact_root, terminal_path)
        ):
            raise SessionClaimError(
                "attempt path was prepopulated or an earlier reservation was interrupted; "
                "the session is fail-closed"
            )
        exclusive_create_attempt_root(attempt_root, artifact_root)
        reserve_zero_byte_output(output_path)
        receipt = build_start_receipt(
            claim=claim,
            claim_bytes=claim_bytes,
            slot=slot,
            attempt=attempt,
            predecessor_sha256=predecessor_sha,
            output_reserved_exclusively=True,
        )
        validate_schema(validator, receipt, ATTEMPT_SCHEMA_PATH, "attempt start receipt")
        exclusive_write(receipt_path, json_file_bytes(receipt))
    return receipt_path, receipt


def finalize_attempt(
    *,
    native: Path,
    claim_path: Path,
    slot_id: str,
    attempt_ordinal: int,
    common_dir_override: Path | None = None,
) -> tuple[Path, dict[str, Any]]:
    validator, _, _, claim = read_and_validate_claim(
        claim_path, native=native, common_dir_override=common_dir_override
    )
    if slot_id not in claim["requiredFreshSlotIds"]:
        raise SessionClaimError(
            f"{slot_id} is a reused stable lane and cannot be finalized in this session"
        )
    slot, attempt = find_attempt(claim, slot_id, attempt_ordinal)
    output_path = Path(attempt["outputPath"])
    terminal_path = Path(attempt["terminalReceiptPath"])
    with exclusive_session_lock(Path(claim["sessionLockPath"])):
        if os.path.lexists(str(terminal_path)):
            raise SessionClaimError("attempt was already terminalized")
        start_path, start_bytes, start = read_exact(
            Path(attempt["startReceiptPath"]), "attempt start receipt"
        )
        validate_schema(validator, start, ATTEMPT_SCHEMA_PATH, "attempt start receipt")
        if (
            start.get("evaluationSessionClaimSha256")
            != claim["evaluationSessionClaimSha256"]
            or start.get("slotId") != slot_id
            or start.get("attemptOrdinal") != attempt_ordinal
            or start.get("canonicalReceiptPath") != str(start_path)
            or start.get("status") != "STARTED_BEFORE_PRODUCER"
        ):
            raise SessionClaimError("attempt start receipt does not match a runnable fixed slot")
        if start.get("evaluationAttemptReceiptSha256") != self_hash(
            start, "evaluationAttemptReceiptSha256"
        ):
            raise SessionClaimError("attempt start receipt self-hash mismatch")
        terminal_descriptor = reserve_zero_byte_file(terminal_path)
        try:
            output_bytes = read_regular_exact(output_path, "reserved attempt output")
            outcome, failure_code = classify_output(
                validator, slot["role"], output_bytes
            )
            terminal = build_terminal_value(
                claim=claim,
                slot=slot,
                attempt=attempt,
                start=start,
                start_bytes=start_bytes,
                output_bytes=output_bytes,
                outcome=outcome,
                failure_code=failure_code,
            )
            validate_schema(
                validator, terminal, TERMINAL_SCHEMA_PATH, "attempt terminal receipt"
            )
            descriptor = terminal_descriptor
            terminal_descriptor = -1
            finalize_reserved_file(
                descriptor,
                terminal_path,
                json_file_bytes(terminal),
            )
        except Exception:
            if terminal_descriptor >= 0:
                os.close(terminal_descriptor)
                fsync_directory(terminal_path.parent)
            raise
    return terminal_path, terminal


def emit_result(path: Path, value: dict[str, Any], hash_field: str) -> None:
    print(json.dumps({
        "status": "PASS",
        "path": str(path),
        hash_field: value[hash_field],
    }, ensure_ascii=False, sort_keys=True))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--native-dir", type=Path, default=NATIVE)
    parser.add_argument("--rubric", type=Path, default=RUBRIC)
    subparsers = parser.add_subparsers(dest="command", required=True)

    create = subparsers.add_parser("create-session")
    create.add_argument("--candidate-manifest", type=Path, required=True)
    create.add_argument("--holdout-consumption-receipt", type=Path, required=True)
    create.add_argument("--registry-before", type=Path, required=True)
    create.add_argument("--registry-after", type=Path, required=True)
    create.add_argument("--mode", choices=["INITIAL", "REPLACEMENT"], required=True)
    create.add_argument("--initial-session-claim", type=Path)
    create.add_argument("--initial-scorecard", type=Path)
    create.add_argument("--initial-panel-finalization-seal", type=Path)

    reserve = subparsers.add_parser("reserve-attempt")
    reserve.add_argument("--session-claim", type=Path, required=True)
    reserve.add_argument("--slot-id", choices=[f"SLOT-{index:02d}" for index in range(1, 10)], required=True)
    reserve.add_argument("--attempt", type=int, choices=[1, 2, 3], required=True)

    finalize = subparsers.add_parser("finalize-attempt")
    finalize.add_argument("--session-claim", type=Path, required=True)
    finalize.add_argument("--slot-id", choices=[f"SLOT-{index:02d}" for index in range(1, 10)], required=True)
    finalize.add_argument("--attempt", type=int, choices=[1, 2, 3], required=True)

    args = parser.parse_args()
    native = args.native_dir.resolve()
    try:
        if args.command == "create-session":
            path, value = create_session_claim(
                native=native,
                rubric=args.rubric.resolve(),
                candidate_path=args.candidate_manifest,
                holdout_receipt_path=args.holdout_consumption_receipt,
                registry_before_path=args.registry_before,
                registry_after_path=args.registry_after,
                mode=args.mode,
                initial_claim_path=args.initial_session_claim,
                initial_scorecard_path=args.initial_scorecard,
                initial_panel_finalization_seal_path=(
                    args.initial_panel_finalization_seal
                ),
            )
            emit_result(path, value, "evaluationSessionClaimSha256")
        elif args.command == "reserve-attempt":
            path, value = reserve_attempt(
                native=native,
                claim_path=args.session_claim,
                slot_id=args.slot_id,
                attempt_ordinal=args.attempt,
            )
            emit_result(path, value, "evaluationAttemptReceiptSha256")
        else:
            path, value = finalize_attempt(
                native=native,
                claim_path=args.session_claim,
                slot_id=args.slot_id,
                attempt_ordinal=args.attempt,
            )
            emit_result(path, value, "evaluationAttemptTerminalSha256")
    except (OSError, SessionClaimError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
