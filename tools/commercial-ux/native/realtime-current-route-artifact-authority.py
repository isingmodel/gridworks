#!/usr/bin/env python3
"""Finalize the current route as an exact, blocked, non-score artifact chain.

The seven artifacts are written in one append-only prefix.  Aggregate is the
last commit marker.  This authority never captures native evidence, calls a
model, judges, verifies evidence or product claims, evaluates product hard
gates, performs score aggregation, or scores.
"""

from __future__ import annotations

import argparse
import dataclasses
import importlib.util
import json
import os
from pathlib import Path
import stat
import sys
from typing import Any, Sequence


CANONICALIZATION = "GRIDWORKS_CANONICAL_JSON_V1"
PRODUCER_SCHEMA = "gridworks.realtime-current-route-artifact-producer-authority.v1"

SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_REPOSITORY_ROOT = SCRIPT_DIR.parents[2]
CHAIN_MODULE_PATH = SCRIPT_DIR / "realtime-evaluation-chain-authority.py"
POLICY_PATH = SCRIPT_DIR / "realtime-current-route-artifact-policy.json"
EXPECTED_POLICY_RAW_SHA256 = (
    "sha256:f27c3c49c00d547ee55ab5b0719fda1729ee13322dff6caccc48b2fea6297960"
)

SCHEMA_PATHS = {
    "EVIDENCE_INDEX": SCRIPT_DIR / "realtime-evidence-index.schema.json",
    "ACTOR_TERMINAL": SCRIPT_DIR / "realtime-actor-terminal.schema.json",
    "JUDGE_INPUT": SCRIPT_DIR / "realtime-judge-input.schema.json",
    "JUDGE_TERMINAL": SCRIPT_DIR / "realtime-judge-terminal.schema.json",
    "VERIFIER_RESULT": SCRIPT_DIR / "realtime-verifier-result.schema.json",
    "ORACLE_LEDGER": SCRIPT_DIR / "realtime-oracle-ledger.schema.json",
    "AGGREGATE": SCRIPT_DIR / "realtime-aggregate.schema.json",
}

PRODUCER_PATH_ROLES = (
    (
        "tools/commercial-ux/native/realtime-actor-terminal.schema.json",
        "STRUCTURAL_ACTOR_TERMINAL_SCHEMA_NON_AUTHORITY",
    ),
    (
        "tools/commercial-ux/native/realtime-aggregate.schema.json",
        "STRUCTURAL_AGGREGATE_SCHEMA_NON_AUTHORITY",
    ),
    (
        "tools/commercial-ux/native/realtime-current-route-artifact-authority.py",
        "CURRENT_ROUTE_ARTIFACT_PRODUCER_AND_SEMANTIC_VERIFIER",
    ),
    (
        "tools/commercial-ux/native/realtime-current-route-artifact-policy.json",
        "CURRENT_ROUTE_ARTIFACT_POLICY",
    ),
    (
        "tools/commercial-ux/native/realtime-evaluation-chain-authority.py",
        "BOUND_PARENT_CHAIN_SEMANTIC_VERIFIER_DEPENDENCY",
    ),
    (
        "tools/commercial-ux/native/realtime-evidence-index.schema.json",
        "STRUCTURAL_EVIDENCE_INDEX_SCHEMA_NON_AUTHORITY",
    ),
    (
        "tools/commercial-ux/native/realtime-judge-input.schema.json",
        "STRUCTURAL_JUDGE_INPUT_SCHEMA_NON_AUTHORITY",
    ),
    (
        "tools/commercial-ux/native/realtime-judge-terminal.schema.json",
        "STRUCTURAL_JUDGE_TERMINAL_SCHEMA_NON_AUTHORITY",
    ),
    (
        "tools/commercial-ux/native/realtime-oracle-ledger.schema.json",
        "STRUCTURAL_ORACLE_LEDGER_SCHEMA_NON_AUTHORITY",
    ),
    (
        "tools/commercial-ux/native/realtime-verifier-result.schema.json",
        "STRUCTURAL_VERIFIER_RESULT_SCHEMA_NON_AUTHORITY",
    ),
    (
        "tools/commercial-ux/native/test-realtime-current-route-artifact-authority.py",
        "ADVERSARIAL_CURRENT_ROUTE_ARTIFACT_TEST_SPEC_NON_RUNTIME",
    ),
)


class CurrentRouteArtifactAuthorityError(ValueError):
    """Raised when the blocked artifact chain is not exact."""


def _load_chain_module() -> Any:
    spec = importlib.util.spec_from_file_location(
        "realtime_evaluation_chain_for_current_route_artifacts",
        CHAIN_MODULE_PATH,
    )
    if spec is None or spec.loader is None:
        raise CurrentRouteArtifactAuthorityError(
            f"cannot load parent evaluation-chain authority {CHAIN_MODULE_PATH}"
        )
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


CHAIN = _load_chain_module()
SESSION = CHAIN.SESSION
CANDIDATE = CHAIN.CANDIDATE


def sha256_bytes(data: bytes) -> str:
    return CHAIN.sha256_bytes(data)


def canonical_sha256(value: Any) -> str:
    return CHAIN.canonical_sha256(value)


def strict_typed_equal(left: Any, right: Any) -> bool:
    return CHAIN.strict_typed_equal(left, right)


def strict_json_bytes(data: bytes, label: str) -> dict[str, Any]:
    try:
        return CHAIN.strict_json_bytes(data, label)
    except CHAIN.ChainAuthorityError as error:
        raise CurrentRouteArtifactAuthorityError(str(error)) from error


def json_file_bytes(value: dict[str, Any]) -> bytes:
    return CHAIN.json_file_bytes(value)


def self_hash(value: dict[str, Any], field: str = "artifactSha256") -> str:
    return CHAIN.self_hash(value, field)


def read_regular_exact(path: Path, label: str) -> tuple[Path, bytes]:
    try:
        return CHAIN.read_regular_exact(path, label)
    except CHAIN.ChainAuthorityError as error:
        raise CurrentRouteArtifactAuthorityError(str(error)) from error


def canonical_existing_directory(path: Path, label: str) -> Path:
    try:
        return CHAIN.canonical_existing_directory(path, label)
    except CHAIN.ChainAuthorityError as error:
        raise CurrentRouteArtifactAuthorityError(str(error)) from error


def exclusive_create_directory(path: Path, label: str) -> Path:
    try:
        return CHAIN.exclusive_create_directory(path, label)
    except CHAIN.ChainAuthorityError as error:
        raise CurrentRouteArtifactAuthorityError(str(error)) from error


def exclusive_write(path: Path, content: bytes, label: str) -> None:
    try:
        CHAIN.exclusive_write(path, content, label)
    except CHAIN.ChainAuthorityError as error:
        raise CurrentRouteArtifactAuthorityError(str(error)) from error


def fsync_directory(path: Path) -> None:
    CHAIN.fsync_directory(path)


def load_artifact_policy() -> tuple[dict[str, Any], bytes]:
    _resolved, data = read_regular_exact(POLICY_PATH, "current route artifact policy")
    if sha256_bytes(data) != EXPECTED_POLICY_RAW_SHA256:
        raise CurrentRouteArtifactAuthorityError(
            "current route artifact policy raw-byte drift"
        )
    policy = strict_json_bytes(data, "current route artifact policy")
    return policy, data


def validate_artifact_policy(
    policy: dict[str, Any],
    data: bytes | None = None,
) -> None:
    expected, expected_bytes = load_artifact_policy()
    if not strict_typed_equal(policy, expected):
        raise CurrentRouteArtifactAuthorityError(
            "current route artifact policy object drift"
        )
    if data is not None and data != expected_bytes:
        raise CurrentRouteArtifactAuthorityError(
            "current route artifact policy byte drift"
        )
    parent = policy.get("parentEvaluationChainAuthority", {})
    chain = policy.get("artifactChain", {})
    ordered = policy.get("orderedArtifacts")
    if (
        policy.get("schemaVersion")
        != "gridworks.realtime-current-route-artifact-policy.v1"
        or parent.get("sourceCommit")
        != "74ba7256766f41c1398fba98f59c1c942a4cb96e"
        or parent.get("producerFilesSha256")
        != "sha256:d87e605449e558d5debd2652f3cf0282f851da45eb19b85e1b0d811af18d218f"
        or parent.get("chainPolicyRawSha256")
        != "sha256:accef28faf6583f844e082e0a4c22f2087810111897cfb4c7bbf8c287a37e6d0"
        or chain.get("officialCommercialUX") is not False
        or chain.get("scoreBearingCaptureAllowed") is not False
        or chain.get("commercialUXProxy") is not None
        or not isinstance(ordered, list)
        or len(ordered) != 7
        or [row.get("ordinal") for row in ordered] != list(range(1, 8))
        or [row.get("relativePath") for row in ordered]
        != list(CHAIN.FUTURE_ARTIFACT_RELATIVE_PATHS)
        or policy.get("evaluatorProducerAuthority", {}).get("paths")
        != [path for path, _role in PRODUCER_PATH_ROLES]
    ):
        raise CurrentRouteArtifactAuthorityError(
            "current route artifact policy invariant drift"
        )


def bind_artifact_evaluator_authority(
    repository_root: Path,
    revision: str,
) -> dict[str, Any]:
    root = CANDIDATE.resolve_repository_root(repository_root)
    expected_script_dir = (
        root / "tools" / "commercial-ux" / "native"
    ).resolve(strict=True)
    if SCRIPT_DIR != expected_script_dir or Path(__file__).resolve(strict=True) != (
        expected_script_dir / "realtime-current-route-artifact-authority.py"
    ).resolve(strict=True):
        raise CurrentRouteArtifactAuthorityError(
            "running artifact evaluator is outside the candidate repository"
        )
    try:
        source_commit = CANDIDATE.resolve_source_commit(root, revision)
        entries = CANDIDATE.git_tree_entries(root, source_commit)
    except CANDIDATE.CandidateAuthorityError as error:
        raise CurrentRouteArtifactAuthorityError(str(error)) from error
    by_path = {
        path: (mode, object_type, object_id)
        for mode, object_type, object_id, path in entries
    }
    rows: list[dict[str, Any]] = []
    for path, role in PRODUCER_PATH_ROLES:
        entry = by_path.get(path)
        if entry is None:
            raise CurrentRouteArtifactAuthorityError(
                f"artifact authority source commit lacks evaluator file: {path}"
            )
        mode, object_type, object_id = entry
        if mode not in {"100644", "100755"} or object_type != "blob":
            raise CurrentRouteArtifactAuthorityError(
                f"artifact evaluator is not a regular Git blob: {path}"
            )
        try:
            git_data = CANDIDATE.run_git_command(
                root,
                ["cat-file", "blob", "--", object_id],
                label=f"artifact evaluator Git blob {path}",
            )
        except CANDIDATE.CandidateAuthorityError as error:
            raise CurrentRouteArtifactAuthorityError(str(error)) from error
        _resolved, running_data = read_regular_exact(
            root / path,
            f"running artifact evaluator {path}",
        )
        if running_data != git_data:
            raise CurrentRouteArtifactAuthorityError(
                f"running artifact evaluator differs from source commit: {path}"
            )
        rows.append(CANDIDATE.GitBlob(path, mode, object_id, role, git_data).row())
    rows.sort(key=lambda row: row["path"])
    policy, _policy_bytes = load_artifact_policy()
    if [row["path"] for row in rows] != policy["evaluatorProducerAuthority"]["paths"]:
        raise CurrentRouteArtifactAuthorityError(
            "artifact evaluator path order differs from policy"
        )
    return {
        "schemaVersion": PRODUCER_SCHEMA,
        "sourceCommit": source_commit,
        "fileCount": len(rows),
        "files": rows,
        "filesSha256": canonical_sha256(rows),
        "runningFilesMatchGitBlobs": True,
        "gitCommandAuthority": CANDIDATE.bind_git_command_authority(root),
        "parentChainSemanticVerifierDependencyBound": True,
        "semanticVerifierEntryPoint": (
            "verify_current_route_artifact_chain_against_reconstructed_authority"
        ),
        "structuralSchemasAuthority": "STRUCTURAL_ONLY_NOT_ARTIFACT_AUTHORITY",
    }


@dataclasses.dataclass(frozen=True)
class ParentChainContext:
    prefix: Any
    claim: dict[str, Any]
    claim_bytes: bytes
    chain_evaluator: dict[str, Any]


def _assert_parent_context_unchanged(
    first: ParentChainContext,
    second: ParentChainContext,
    label: str,
) -> None:
    first_files = tuple(
        (snapshot.row(), snapshot.data) for snapshot in first.prefix.files
    )
    second_files = tuple(
        (snapshot.row(), snapshot.data) for snapshot in second.prefix.files
    )
    if (
        first.claim_bytes != second.claim_bytes
        or first.prefix.claim_bytes != second.prefix.claim_bytes
        or not strict_typed_equal(first.claim, second.claim)
        or not strict_typed_equal(first.chain_evaluator, second.chain_evaluator)
        or not strict_typed_equal(first.prefix.claim, second.prefix.claim)
        or not strict_typed_equal(
            list(first.prefix.attempt_audit),
            list(second.prefix.attempt_audit),
        )
        or not strict_typed_equal(
            first.prefix.selected_terminal,
            second.prefix.selected_terminal,
        )
        or first_files != second_files
    ):
        raise CurrentRouteArtifactAuthorityError(
            f"parent evaluation chain changed during {label}"
        )


def _assert_producer_unchanged(
    repository_root: Path,
    first: dict[str, Any],
    label: str,
) -> None:
    second = bind_artifact_evaluator_authority(
        repository_root,
        first["sourceCommit"],
    )
    if not strict_typed_equal(first, second):
        raise CurrentRouteArtifactAuthorityError(
            f"artifact evaluator authority changed during {label}"
        )


def _read_parent_chain_claim(path: Path) -> tuple[Path, dict[str, Any], bytes]:
    resolved, data = read_regular_exact(path, "parent evaluation chain claim")
    claim = strict_json_bytes(data, "parent evaluation chain claim")
    if data != json_file_bytes(claim):
        raise CurrentRouteArtifactAuthorityError(
            "parent evaluation chain claim is not canonical JSON file bytes"
        )
    if claim.get("evaluationChainClaimSha256") != CHAIN.self_hash(
        claim,
        "evaluationChainClaimSha256",
    ):
        raise CurrentRouteArtifactAuthorityError(
            "parent evaluation chain claim self-hash mismatch"
        )
    if claim.get("canonicalClaimPath") != str(resolved):
        raise CurrentRouteArtifactAuthorityError(
            "parent evaluation chain claim path binding drift"
        )
    return resolved, claim, data


def _validate_parent_root_inventory(
    claim: dict[str, Any],
    *,
    finalized_artifacts: bool,
) -> None:
    root = canonical_existing_directory(
        Path(claim["canonicalChainRoot"]),
        "parent evaluation chain root",
    )
    expected = {"inputs", "evaluation-chain-claim.json"}
    if finalized_artifacts:
        expected.add("artifacts")
    if {path.name for path in root.iterdir()} != expected:
        raise CurrentRouteArtifactAuthorityError(
            "parent evaluation chain root phase inventory drift"
        )
    inputs = canonical_existing_directory(root / "inputs", "parent chain inputs")
    expected_files = {
        row["snapshotRelativePath"] for row in claim["inputSnapshot"]["files"]
    }
    expected_directories = {"inputs"}
    for relative in expected_files:
        current = Path(relative).parent
        while str(current) != ".":
            expected_directories.add(str(current))
            current = current.parent
    actual_files: set[str] = set()
    actual_directories: set[str] = {"inputs"}
    for directory, directory_names, file_names in os.walk(inputs, followlinks=False):
        base = Path(directory)
        for name in directory_names:
            child = canonical_existing_directory(
                base / name,
                f"parent chain input directory {name}",
            )
            actual_directories.add(str(child.relative_to(root)))
        for name in file_names:
            child, _data = read_regular_exact(
                base / name,
                f"parent chain input file {name}",
            )
            actual_files.add(str(child.relative_to(root)))
    if actual_files != expected_files or actual_directories != expected_directories:
        raise CurrentRouteArtifactAuthorityError(
            "parent evaluation chain input inventory drift"
        )


def _reconstruct_parent_chain_without_lock(
    repository_root: Path,
    chain_claim_path: Path,
    *,
    finalized_artifacts: bool,
) -> ParentChainContext:
    resolved, claim, claim_bytes = _read_parent_chain_claim(chain_claim_path)
    policy, policy_bytes = load_artifact_policy()
    validate_artifact_policy(policy, policy_bytes)
    parent_policy = policy["parentEvaluationChainAuthority"]
    if (
        claim.get("chainAuthoritySourceCommit") != parent_policy["sourceCommit"]
        or claim.get("chainEvaluatorAuthority", {}).get("filesSha256")
        != parent_policy["producerFilesSha256"]
        or claim.get("chainPolicyRawSha256")
        != parent_policy["chainPolicyRawSha256"]
        or claim.get("officialCommercialUX") is not False
        or claim.get("scoreBearingCaptureAllowed") is not False
        or claim.get("commercialUXProxy") is not None
    ):
        raise CurrentRouteArtifactAuthorityError(
            "parent evaluation chain reviewed authority projection drift"
        )
    root = canonical_existing_directory(
        Path(claim["canonicalChainRoot"]),
        "bound parent evaluation chain root",
    )
    if resolved.parent != root:
        raise CurrentRouteArtifactAuthorityError(
            "parent evaluation chain claim escaped its bound root"
        )
    if claim.get("chainRootBindingSha256") != sha256_bytes(
        str(root).encode("utf-8")
    ):
        raise CurrentRouteArtifactAuthorityError(
            "parent evaluation chain root binding mismatch"
        )
    chain_evaluator = CHAIN.bind_chain_evaluator_authority(
        repository_root,
        claim["chainAuthoritySourceCommit"],
    )
    session_claim_path = Path(
        claim["parentSessionAuthority"]["canonicalSessionClaimPath"]
    )
    try:
        prefix = CHAIN._read_finalized_parent_prefix_without_lock(
            repository_root,
            session_claim_path,
        )
    except CHAIN.ChainAuthorityError as error:
        raise CurrentRouteArtifactAuthorityError(str(error)) from error
    expected_root = CHAIN._derive_chain_root(
        Path(prefix.claim["canonicalSessionRoot"])
    )
    if root != expected_root:
        raise CurrentRouteArtifactAuthorityError(
            "parent evaluation chain deterministic sibling drift"
        )
    CHAIN._verify_snapshot_bytes(
        root,
        prefix,
        claim.get("inputSnapshot", {}).get("files", []),
    )
    expected_claim = CHAIN.compose_chain_claim(
        prefix,
        chain_evaluator,
        root,
        claim.get("chainNonce"),
    )
    if not strict_typed_equal(claim, expected_claim):
        raise CurrentRouteArtifactAuthorityError(
            "parent evaluation chain differs from reconstructed authority"
        )
    _validate_parent_root_inventory(
        claim,
        finalized_artifacts=finalized_artifacts,
    )
    _resolved_again, data_again = read_regular_exact(
        resolved,
        "parent evaluation chain claim post-reconstruction",
    )
    if data_again != claim_bytes:
        raise CurrentRouteArtifactAuthorityError(
            "parent evaluation chain claim changed during reconstruction"
        )
    return ParentChainContext(prefix, claim, claim_bytes, chain_evaluator)


def _artifact_chain_id(
    parent: ParentChainContext,
    producer: dict[str, Any],
    policy_bytes: bytes,
) -> str:
    return canonical_sha256({
        "schemaVersion": "gridworks.realtime-current-route-artifact-chain.v1",
        "parentEvaluationChainId": parent.claim["chainId"],
        "parentEvaluationChainClaimSha256": parent.claim[
            "evaluationChainClaimSha256"
        ],
        "parentEvaluationChainClaimRawSha256": sha256_bytes(parent.claim_bytes),
        "artifactPolicyRawSha256": sha256_bytes(policy_bytes),
        "artifactProducerFilesSha256": producer["filesSha256"],
    })


def _artifact_row(
    artifact: dict[str, Any],
    data: bytes,
) -> dict[str, Any]:
    return {
        "artifactOrdinal": artifact["artifactOrdinal"],
        "artifactKind": artifact["artifactKind"],
        "artifactPath": artifact["canonicalArtifactPath"],
        "artifactSha256": artifact["artifactSha256"],
        "artifactRawSha256": sha256_bytes(data),
        "byteLength": len(data),
    }


def _producer_schema_raw_sha256(
    producer: dict[str, Any],
    schema_path: Path,
) -> str:
    expected_path = str(schema_path.relative_to(DEFAULT_REPOSITORY_ROOT))
    rows = producer.get("files")
    if not isinstance(rows, list):
        raise CurrentRouteArtifactAuthorityError(
            "artifact producer lacks bound file rows"
        )
    matches = [
        row for row in rows
        if isinstance(row, dict) and row.get("path") == expected_path
    ]
    if len(matches) != 1 or not isinstance(matches[0].get("rawSha256"), str):
        raise CurrentRouteArtifactAuthorityError(
            f"artifact producer lacks one schema byte binding: {expected_path}"
        )
    return matches[0]["rawSha256"]


def _blockers(policy: dict[str, Any], route_kind: str) -> list[str]:
    route = policy["routeBlockers"].get(route_kind)
    if not isinstance(route, str):
        raise CurrentRouteArtifactAuthorityError(
            "current route lacks one exact blocking reason"
        )
    return list(policy["commonBlockers"]) + [route]


def _native_evidence_status(policy: dict[str, Any], route_kind: str) -> str:
    value = policy["nativeEvidenceStatuses"].get(route_kind)
    if not isinstance(value, str):
        raise CurrentRouteArtifactAuthorityError(
            "current route lacks one native evidence status"
        )
    return value


def _selected_material_paths(parent: ParentChainContext) -> list[str]:
    root = Path(parent.claim["canonicalChainRoot"])
    selected = parent.claim["selectedRouteTerminal"]
    if selected["terminalKind"] == "UNAVAILABLE_ROUTE_TERMINAL":
        relatives = ["inputs/session/unavailable-route-terminal.json"]
    else:
        ordinal = selected["attemptOrdinal"]
        relatives = [
            f"inputs/session/attempts/{ordinal:02d}/start-receipt.json",
            f"inputs/session/attempts/{ordinal:02d}/diagnostic-output.json",
            f"inputs/session/attempts/{ordinal:02d}/terminal-receipt.json",
        ]
    rows = {
        row["snapshotRelativePath"]: row
        for row in parent.claim["inputSnapshot"]["files"]
    }
    if any(relative not in rows for relative in relatives):
        raise CurrentRouteArtifactAuthorityError(
            "selected route material is outside the bound input snapshot"
        )
    paths = [str(root / relative) for relative in relatives]
    for path in paths:
        read_regular_exact(Path(path), "selected route material")
    return paths


def _payload_for(
    descriptor: dict[str, Any],
    parent: ParentChainContext,
    prior_rows: list[dict[str, Any]],
    policy: dict[str, Any],
) -> dict[str, Any]:
    route_kind = parent.claim["routeBoundary"]["routeKind"]
    blockers = _blockers(policy, route_kind)
    native_status = _native_evidence_status(policy, route_kind)
    kind = descriptor["kind"]
    if kind == "EVIDENCE_INDEX":
        input_snapshot = parent.claim["inputSnapshot"]
        return {
            "evidenceStatus": "FINALIZED_CURRENT_ROUTE_INDEX_NON_SCORE",
            "routeBoundary": parent.claim["routeBoundary"],
            "attemptAudit": parent.claim["attemptAudit"],
            "selectedRouteTerminal": parent.claim["selectedRouteTerminal"],
            "inputSnapshot": {
                "fileCount": input_snapshot["fileCount"],
                "files": input_snapshot["files"],
                "treeSha256": input_snapshot["treeSha256"],
            },
            "selectedRouteMaterialSource": "PARENT_CHAIN_INPUT_SNAPSHOTS",
            "selectedRouteMaterialPaths": _selected_material_paths(parent),
            "nativeEvidenceStatus": native_status,
            "boundNativeEvidenceItemCount": 0,
            "nativeCaptureAttemptedByThisAuthority": False,
            "nativeCaptureCountByThisAuthority": 0,
            "boundNativePresentationObserved": False,
            "blockers": blockers,
        }
    if kind == "ACTOR_TERMINAL":
        return {
            "actorStatus": "BLOCKED_NO_NATIVE_CAPTURE",
            "actorInvocationCountByThisAuthority": 0,
            "nativeCaptureAttemptedByThisAuthority": False,
            "nativeCaptureCountByThisAuthority": 0,
            "boundNativeEvidenceItemCount": 0,
            "actorResult": None,
            "blockers": blockers,
        }
    if kind == "JUDGE_INPUT":
        return {
            "judgeInputStatus": "BLOCKED_NO_EXECUTABLE_JUDGE_INPUT",
            "executableJudgeInput": False,
            "judgeInput": None,
            "boundNativeEvidenceItemCount": 0,
            "modelCallCountByThisAuthority": 0,
            "futureModelRequirement": {
                "model": policy["requiredFutureModel"],
                "reasoningEffort": policy["requiredFutureReasoningEffort"],
                "requirementOnlyNotExecutionClaim": True,
            },
            "blockers": blockers,
        }
    if kind == "JUDGE_TERMINAL":
        return {
            "judgeStatus": "BLOCKED_MODEL_EXECUTION_UNAUTHORIZED",
            "modelExecutionAuthorized": False,
            "modelCallCountByThisAuthority": 0,
            "modelExecutionReceipt": None,
            "futureModelRequirement": {
                "model": policy["requiredFutureModel"],
                "reasoningEffort": policy["requiredFutureReasoningEffort"],
                "requirementOnlyNotExecutionClaim": True,
            },
            "judgment": None,
            "blockers": blockers,
        }
    if kind == "VERIFIER_RESULT":
        return {
            "verifierStatus": "BLOCKED_NO_JUDGE_OUTPUT",
            "verifierRole": (
                "DOWNSTREAM_EVIDENCE_CLAIM_VERIFIER_NOT_ARTIFACT_SEMANTIC_AUTHORITY"
            ),
            "artifactSemanticVerifierIsEvidenceVerifier": False,
            "evidenceVerifierExecutedByThisAuthority": False,
            "verifierExecutionCountByThisAuthority": 0,
            "unsupportedEvidenceClaimCount": None,
            "verdict": None,
            "blockers": blockers,
        }
    if kind == "ORACLE_LEDGER":
        return {
            "oracleStatus": "BLOCKED_NO_NATIVE_ORACLE_INPUT",
            "oracleRole": (
                "PRODUCT_HARD_GATE_ORACLE_NOT_ARTIFACT_SEMANTIC_AUTHORITY"
            ),
            "artifactIntegrityChecksAreNotProductOracleEvidence": True,
            "productOracleExecutedByThisAuthority": False,
            "oracleExecutionCountByThisAuthority": 0,
            "boundNativeOracleInputCount": 0,
            "hardGatesEvaluatedByThisAuthority": False,
            "hardGateStatus": "NOT_EVALUATED",
            "hardGateViolationCount": None,
            "ledgerRows": [],
            "verdict": None,
            "blockers": blockers,
        }
    if kind == "AGGREGATE":
        if len(prior_rows) != 6:
            raise CurrentRouteArtifactAuthorityError(
                "aggregate requires exactly six finalized upstream artifacts"
            )
        return {
            "aggregateStatus": "FINALIZED_BLOCKED_NON_SCORE",
            "upstreamArtifacts": list(prior_rows),
            "upstreamExecutionStatus": {
                "evidenceIndex": "FINALIZED_CURRENT_ROUTE_INDEX_NON_SCORE",
                "actor": "BLOCKED_NO_NATIVE_CAPTURE_NOT_EXECUTED",
                "judgeInput": (
                    "BLOCKED_EXECUTABLE_JUDGE_PAYLOAD_NOT_MATERIALIZED"
                ),
                "judge": "BLOCKED_MODEL_EXECUTION_UNAUTHORIZED_NOT_EXECUTED",
                "evidenceClaimVerifier": "BLOCKED_NO_JUDGE_OUTPUT_NOT_EXECUTED",
                "productOracle": "BLOCKED_NO_NATIVE_ORACLE_INPUT_NOT_EXECUTED",
            },
            "scoreAggregationPerformedByThisAuthority": False,
            "nativeEvidenceStatus": native_status,
            "boundNativeEvidenceItemCount": 0,
            "modelExecutionStatus": "UNAVAILABLE",
            "modelCallCountByThisAuthority": 0,
            "modelExecutionReceipt": None,
            "hardGateStatus": "NOT_EVALUATED",
            "hardGateViolationCount": None,
            "verdict": None,
            "finalizationAuthority": {
                "aggregateIsLastFilesystemCommitMarker": True,
                "artifactSemanticVerificationRequired": True,
                "artifactSemanticVerificationIsNotEvidenceVerification": True,
            },
            "blockers": blockers,
        }
    raise CurrentRouteArtifactAuthorityError("unknown current route artifact kind")


def compose_artifact(
    descriptor: dict[str, Any],
    parent: ParentChainContext,
    producer: dict[str, Any],
    artifact_chain_id: str,
    prior_rows: list[dict[str, Any]],
) -> dict[str, Any]:
    policy, policy_bytes = load_artifact_policy()
    root = Path(parent.claim["canonicalChainRoot"])
    expected_path = Path(parent.claim["fixedFutureArtifactPaths"][
        descriptor["ordinal"] - 1
    ])
    if expected_path != root / descriptor["relativePath"]:
        raise CurrentRouteArtifactAuthorityError(
            "artifact path differs from parent fixed future path"
        )
    schema_path = SCHEMA_PATHS[descriptor["kind"]]
    artifact: dict[str, Any] = {
        "schemaVersion": descriptor["schemaVersion"],
        "canonicalization": CANONICALIZATION,
        "status": descriptor["status"],
        "artifactOrdinal": descriptor["ordinal"],
        "artifactKind": descriptor["kind"],
        "artifactRelativePath": descriptor["relativePath"],
        "artifactPolicyRawSha256": sha256_bytes(policy_bytes),
        "artifactSchemaRawSha256": _producer_schema_raw_sha256(
            producer,
            schema_path,
        ),
        "artifactAuthoritySourceCommit": producer["sourceCommit"],
        "artifactProducerFilesSha256": producer["filesSha256"],
        "artifactChainId": artifact_chain_id,
        "parentEvaluationChainId": parent.claim["chainId"],
        "parentEvaluationChainClaimSha256": parent.claim[
            "evaluationChainClaimSha256"
        ],
        "parentEvaluationChainClaimRawSha256": sha256_bytes(parent.claim_bytes),
        "parentChainRootBindingSha256": parent.claim["chainRootBindingSha256"],
        "parentInputSnapshotTreeSha256": parent.claim["inputSnapshot"][
            "treeSha256"
        ],
        "parentRouteBoundarySha256": parent.claim["routeBoundary"][
            "routeBoundarySha256"
        ],
        "selectedRouteTerminalSelfSha256": parent.claim[
            "selectedRouteTerminal"
        ]["terminalSelfSha256"],
        "selectedRouteTerminalRawSha256": parent.claim[
            "selectedRouteTerminal"
        ]["terminalRawSha256"],
        "candidateSha256": parent.claim["parentSessionAuthority"]["candidateSha256"],
        "routeBindingSha256": parent.claim["routeBoundary"]["routeBindingSha256"],
        "routeKind": parent.claim["routeBoundary"]["routeKind"],
        "canonicalArtifactPath": str(expected_path),
        "priorArtifacts": list(prior_rows),
        "priorArtifactsTreeSha256": canonical_sha256(prior_rows),
        "payload": _payload_for(descriptor, parent, prior_rows, policy),
        "officialCommercialUX": False,
        "scoreBearingCaptureAllowed": False,
        "commercialUXProxy": None,
        "artifactProducerExecutionAttested": False,
        "limitations": list(policy["limitations"]),
    }
    artifact["artifactSha256"] = self_hash(artifact)
    return artifact


def _read_canonical_artifact(
    path: Path,
    label: str,
) -> tuple[bytes, dict[str, Any]]:
    try:
        SESSION.reject_symlink_components(path, label)
        before_path = os.lstat(path)
        if not stat.S_ISREG(before_path.st_mode) or before_path.st_nlink != 1:
            raise CurrentRouteArtifactAuthorityError(
                f"{label} must be a unique regular file with link count one"
            )
        resolved = path.resolve(strict=True)
        flags = os.O_RDONLY
        if hasattr(os, "O_NOFOLLOW"):
            flags |= os.O_NOFOLLOW
        descriptor = os.open(resolved, flags)
        try:
            before = os.fstat(descriptor)
            if not stat.S_ISREG(before.st_mode) or before.st_nlink != 1:
                raise CurrentRouteArtifactAuthorityError(
                    f"{label} must be a unique regular file with link count one"
                )
            chunks: list[bytes] = []
            while True:
                chunk = os.read(descriptor, 1024 * 1024)
                if not chunk:
                    break
                chunks.append(chunk)
            after = os.fstat(descriptor)
        finally:
            os.close(descriptor)
        after_path = os.lstat(path)
    except CurrentRouteArtifactAuthorityError:
        raise
    except (OSError, SESSION.SessionAuthorityError) as error:
        raise CurrentRouteArtifactAuthorityError(
            f"{label} cannot be read as a unique regular file: {error}"
        ) from error
    identity_before = (
        before_path.st_dev,
        before_path.st_ino,
        before_path.st_size,
        before_path.st_mtime_ns,
        before_path.st_ctime_ns,
        before_path.st_nlink,
        before.st_dev,
        before.st_ino,
        before.st_size,
        before.st_mtime_ns,
        before.st_ctime_ns,
        before.st_nlink,
    )
    identity_after = (
        after_path.st_dev,
        after_path.st_ino,
        after_path.st_size,
        after_path.st_mtime_ns,
        after_path.st_ctime_ns,
        after_path.st_nlink,
        after.st_dev,
        after.st_ino,
        after.st_size,
        after.st_mtime_ns,
        after.st_ctime_ns,
        after.st_nlink,
    )
    if identity_before != identity_after or before.st_ino != before_path.st_ino:
        raise CurrentRouteArtifactAuthorityError(
            f"{label} changed identity while being read"
        )
    data = b"".join(chunks)
    if len(data) != after.st_size:
        raise CurrentRouteArtifactAuthorityError(
            f"{label} changed byte length while being read"
        )
    value = strict_json_bytes(data, label)
    if data != json_file_bytes(value):
        raise CurrentRouteArtifactAuthorityError(
            f"{label} is not canonical JSON file bytes"
        )
    if value.get("artifactSha256") != self_hash(value):
        raise CurrentRouteArtifactAuthorityError(f"{label} self-hash mismatch")
    return data, value


def _validate_artifact_inventory(
    parent: ParentChainContext,
    expected_count: int,
) -> Path:
    root = Path(parent.claim["canonicalChainRoot"])
    artifact_root = canonical_existing_directory(root / "artifacts", "artifact root")
    expected_names = [
        Path(path).name
        for path in parent.claim["fixedFutureArtifactPaths"][:expected_count]
    ]
    actual_names = [path.name for path in artifact_root.iterdir()]
    if sorted(actual_names) != sorted(expected_names) or len(actual_names) != expected_count:
        raise CurrentRouteArtifactAuthorityError(
            "current route artifact prefix inventory drift"
        )
    for name in actual_names:
        read_regular_exact(artifact_root / name, f"artifact inventory {name}")
    return artifact_root


def _verify_artifacts_without_lock(
    parent: ParentChainContext,
    producer: dict[str, Any],
) -> tuple[dict[str, Any], bytes]:
    policy, policy_bytes = load_artifact_policy()
    descriptors = policy["orderedArtifacts"]
    _validate_artifact_inventory(parent, 7)
    artifact_chain_id = _artifact_chain_id(parent, producer, policy_bytes)
    prior_rows: list[dict[str, Any]] = []
    aggregate: dict[str, Any] | None = None
    aggregate_bytes: bytes | None = None
    first_pass: list[tuple[bytes, dict[str, Any]]] = []
    for descriptor in descriptors:
        path = Path(parent.claim["fixedFutureArtifactPaths"][descriptor["ordinal"] - 1])
        data, submitted = _read_canonical_artifact(
            path,
            f"current route artifact {descriptor['kind']}",
        )
        expected = compose_artifact(
            descriptor,
            parent,
            producer,
            artifact_chain_id,
            prior_rows,
        )
        if not strict_typed_equal(submitted, expected):
            raise CurrentRouteArtifactAuthorityError(
                f"current route artifact reconstruction mismatch: {descriptor['kind']}"
            )
        first_pass.append((data, submitted))
        if descriptor["kind"] == "AGGREGATE":
            aggregate = submitted
            aggregate_bytes = data
        else:
            prior_rows.append(_artifact_row(submitted, data))
    if aggregate is None or aggregate_bytes is None:
        raise CurrentRouteArtifactAuthorityError(
            "aggregate last commit marker is missing"
        )
    for descriptor, (expected_data, expected_object) in zip(
        descriptors,
        first_pass,
    ):
        path = Path(parent.claim["fixedFutureArtifactPaths"][descriptor["ordinal"] - 1])
        data, submitted = _read_canonical_artifact(
            path,
            f"final current route artifact {descriptor['kind']}",
        )
        if data != expected_data or not strict_typed_equal(
            submitted,
            expected_object,
        ):
            raise CurrentRouteArtifactAuthorityError(
                f"current route artifact changed during final verification: "
                f"{descriptor['kind']}"
            )
    _validate_artifact_inventory(parent, 7)
    return aggregate, aggregate_bytes


def _create_current_route_artifact_chain(
    repository_root: Path,
    chain_claim_path: Path,
    *,
    artifact_authority_revision: str = "HEAD",
) -> tuple[Path, dict[str, Any]]:
    policy, policy_bytes = load_artifact_policy()
    validate_artifact_policy(policy, policy_bytes)
    producer = bind_artifact_evaluator_authority(
        repository_root,
        artifact_authority_revision,
    )
    try:
        _prefix, preclaim, _preclaim_bytes = (
            CHAIN.verify_chain_claim_against_reconstructed_authority(
                repository_root,
                chain_claim_path,
            )
        )
    except CHAIN.ChainAuthorityError as error:
        raise CurrentRouteArtifactAuthorityError(str(error)) from error
    session_claim_path = Path(
        preclaim["parentSessionAuthority"]["canonicalSessionClaimPath"]
    )
    try:
        with SESSION.exclusive_claim_lock(session_claim_path):
            parent = _reconstruct_parent_chain_without_lock(
                repository_root,
                chain_claim_path,
                finalized_artifacts=False,
            )
            artifact_root = exclusive_create_directory(
                Path(parent.claim["canonicalChainRoot"]) / "artifacts",
                "current route artifact root",
            )
            artifact_chain_id = _artifact_chain_id(parent, producer, policy_bytes)
            prior_rows: list[dict[str, Any]] = []
            written: list[tuple[Path, bytes, dict[str, Any], str]] = []
            aggregate_path: Path | None = None
            aggregate: dict[str, Any] | None = None
            for descriptor in policy["orderedArtifacts"]:
                expected_count = descriptor["ordinal"] - 1
                if expected_count:
                    _validate_artifact_inventory(parent, expected_count)
                elif any(artifact_root.iterdir()):
                    raise CurrentRouteArtifactAuthorityError(
                        "artifact root was prepopulated before evidence index"
                    )
                for (
                    written_path,
                    written_data,
                    written_object,
                    written_kind,
                ) in written:
                    data_again, object_again = _read_canonical_artifact(
                        written_path,
                        f"current route artifact prefix reread {written_kind}",
                    )
                    if data_again != written_data or not strict_typed_equal(
                        object_again,
                        written_object,
                    ):
                        raise CurrentRouteArtifactAuthorityError(
                            "current route artifact prefix changed during creation: "
                            f"{written_kind}"
                        )
                if descriptor["kind"] == "AGGREGATE":
                    _assert_producer_unchanged(
                        repository_root,
                        producer,
                        "pre-aggregate finalization",
                    )
                artifact = compose_artifact(
                    descriptor,
                    parent,
                    producer,
                    artifact_chain_id,
                    prior_rows,
                )
                path = Path(artifact["canonicalArtifactPath"])
                exclusive_write(
                    path,
                    json_file_bytes(artifact),
                    f"current route artifact {descriptor['kind']}",
                )
                data, submitted = _read_canonical_artifact(
                    path,
                    f"written current route artifact {descriptor['kind']}",
                )
                if not strict_typed_equal(submitted, artifact):
                    raise CurrentRouteArtifactAuthorityError(
                        "written current route artifact byte/object drift"
                    )
                written.append((path, data, submitted, descriptor["kind"]))
                if descriptor["kind"] == "AGGREGATE":
                    aggregate_path = path
                    aggregate = submitted
                else:
                    prior_rows.append(_artifact_row(submitted, data))
            fsync_directory(artifact_root)
            finalized_parent = _reconstruct_parent_chain_without_lock(
                repository_root,
                chain_claim_path,
                finalized_artifacts=True,
            )
            verified_aggregate, verified_bytes = _verify_artifacts_without_lock(
                finalized_parent,
                producer,
            )
            if (
                aggregate_path is None
                or aggregate is None
                or not strict_typed_equal(aggregate, verified_aggregate)
            ):
                raise CurrentRouteArtifactAuthorityError(
                    "aggregate last commit marker finalization drift"
                )
            _data_again, aggregate_again = _read_canonical_artifact(
                aggregate_path,
                "aggregate last commit marker reread",
            )
            if (
                not strict_typed_equal(aggregate_again, verified_aggregate)
                or sha256_bytes(_data_again) != sha256_bytes(verified_bytes)
            ):
                raise CurrentRouteArtifactAuthorityError(
                    "aggregate last commit marker changed after verification"
                )
            final_parent = _reconstruct_parent_chain_without_lock(
                repository_root,
                chain_claim_path,
                finalized_artifacts=True,
            )
            _assert_parent_context_unchanged(
                finalized_parent,
                final_parent,
                "artifact creation finalization",
            )
            _assert_producer_unchanged(
                repository_root,
                producer,
                "artifact creation finalization",
            )
            return aggregate_path, aggregate_again
    except (SESSION.SessionAuthorityError, CHAIN.ChainAuthorityError) as error:
        raise CurrentRouteArtifactAuthorityError(str(error)) from error


def _verify_current_route_artifact_chain_against_reconstructed_authority(
    repository_root: Path,
    aggregate_path: Path,
) -> tuple[dict[str, Any], str]:
    resolved_aggregate, _aggregate_bytes = read_regular_exact(
        aggregate_path,
        "submitted aggregate",
    )
    artifact_root = canonical_existing_directory(
        resolved_aggregate.parent,
        "submitted artifact root",
    )
    if resolved_aggregate.name != "aggregate.json" or artifact_root.name != "artifacts":
        raise CurrentRouteArtifactAuthorityError(
            "submitted aggregate is outside the exact artifact path"
        )
    chain_root = canonical_existing_directory(
        artifact_root.parent,
        "submitted aggregate parent chain root",
    )
    chain_claim_path = chain_root / "evaluation-chain-claim.json"
    _resolved_claim, claim, _claim_bytes = _read_parent_chain_claim(chain_claim_path)
    expected_aggregate_path = Path(claim["fixedFutureArtifactPaths"][6])
    if resolved_aggregate != expected_aggregate_path:
        raise CurrentRouteArtifactAuthorityError(
            "submitted aggregate differs from parent fixed aggregate path"
        )
    policy, policy_bytes = load_artifact_policy()
    validate_artifact_policy(policy, policy_bytes)
    submitted, _submitted_value = _read_canonical_artifact(
        resolved_aggregate,
        "submitted aggregate before source binding",
    )
    source_commit = _submitted_value.get("artifactAuthoritySourceCommit")
    if not isinstance(source_commit, str):
        raise CurrentRouteArtifactAuthorityError(
            "submitted aggregate lacks artifact authority source commit"
        )
    producer = bind_artifact_evaluator_authority(repository_root, source_commit)
    session_claim_path = Path(
        claim["parentSessionAuthority"]["canonicalSessionClaimPath"]
    )
    try:
        with SESSION.exclusive_claim_lock(session_claim_path):
            parent = _reconstruct_parent_chain_without_lock(
                repository_root,
                chain_claim_path,
                finalized_artifacts=True,
            )
            aggregate, aggregate_bytes = _verify_artifacts_without_lock(
                parent,
                producer,
            )
            if aggregate_bytes != submitted:
                raise CurrentRouteArtifactAuthorityError(
                    "submitted aggregate bytes changed during semantic verification"
                )
            _validate_parent_root_inventory(
                parent.claim,
                finalized_artifacts=True,
            )
            final_parent = _reconstruct_parent_chain_without_lock(
                repository_root,
                chain_claim_path,
                finalized_artifacts=True,
            )
            _assert_parent_context_unchanged(
                parent,
                final_parent,
                "artifact semantic verification",
            )
            _assert_producer_unchanged(
                repository_root,
                producer,
                "artifact semantic verification",
            )
            return aggregate, sha256_bytes(aggregate_bytes)
    except (SESSION.SessionAuthorityError, CHAIN.ChainAuthorityError) as error:
        raise CurrentRouteArtifactAuthorityError(str(error)) from error


def create_current_route_artifact_chain(
    repository_root: Path,
    chain_claim_path: Path,
    *,
    artifact_authority_revision: str = "HEAD",
) -> tuple[Path, dict[str, Any]]:
    try:
        return _create_current_route_artifact_chain(
            repository_root,
            chain_claim_path,
            artifact_authority_revision=artifact_authority_revision,
        )
    except CurrentRouteArtifactAuthorityError:
        raise
    except (KeyError, IndexError, TypeError, AttributeError, StopIteration) as error:
        raise CurrentRouteArtifactAuthorityError(
            "malformed parent or current-route artifact structure"
        ) from error
    except OSError as error:
        raise CurrentRouteArtifactAuthorityError(
            f"filesystem changed during current-route artifact creation: {error}"
        ) from error


def verify_current_route_artifact_chain_against_reconstructed_authority(
    repository_root: Path,
    aggregate_path: Path,
) -> tuple[dict[str, Any], str]:
    try:
        return _verify_current_route_artifact_chain_against_reconstructed_authority(
            repository_root,
            aggregate_path,
        )
    except CurrentRouteArtifactAuthorityError:
        raise
    except (KeyError, IndexError, TypeError, AttributeError, StopIteration) as error:
        raise CurrentRouteArtifactAuthorityError(
            "malformed parent or current-route artifact structure"
        ) from error
    except OSError as error:
        raise CurrentRouteArtifactAuthorityError(
            f"filesystem changed during current-route artifact semantic verification: "
            f"{error}"
        ) from error


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Finalize and verify the current route as blocked non-score artifacts."
    )
    commands = parser.add_subparsers(dest="command", required=True)
    create = commands.add_parser("create-artifact-chain")
    create.add_argument(
        "--repository-root",
        type=Path,
        default=DEFAULT_REPOSITORY_ROOT,
    )
    create.add_argument("--chain-claim", type=Path, required=True)
    create.add_argument("--artifact-authority-revision", default="HEAD")
    verify = commands.add_parser("verify-artifact-chain")
    verify.add_argument(
        "--repository-root",
        type=Path,
        default=DEFAULT_REPOSITORY_ROOT,
    )
    verify.add_argument("--aggregate", type=Path, required=True)
    return parser


def _print_result(value: dict[str, Any]) -> None:
    print(json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ))


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(argv)
    try:
        if args.command == "create-artifact-chain":
            aggregate_path, aggregate = create_current_route_artifact_chain(
                args.repository_root,
                args.chain_claim,
                artifact_authority_revision=args.artifact_authority_revision,
            )
            _print_result({
                "aggregatePath": str(aggregate_path),
                "artifactChainId": aggregate["artifactChainId"],
                "status": aggregate["status"],
                "officialCommercialUX": False,
                "commercialUXProxy": None,
            })
        elif args.command == "verify-artifact-chain":
            aggregate, aggregate_raw_sha256 = (
                verify_current_route_artifact_chain_against_reconstructed_authority(
                    args.repository_root,
                    args.aggregate,
                )
            )
            _print_result({
                "aggregatePath": aggregate["canonicalArtifactPath"],
                "artifactChainId": aggregate["artifactChainId"],
                "aggregateRawSha256": aggregate_raw_sha256,
                "status": aggregate["status"],
                "officialCommercialUX": False,
                "commercialUXProxy": None,
            })
        else:
            raise CurrentRouteArtifactAuthorityError("unknown command")
    except (
        CurrentRouteArtifactAuthorityError,
        CHAIN.ChainAuthorityError,
        SESSION.SessionAuthorityError,
        CANDIDATE.CandidateAuthorityError,
    ) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
