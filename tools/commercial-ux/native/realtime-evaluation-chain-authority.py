#!/usr/bin/env python3
"""Create and verify an immutable evaluation-chain parent claim.

This unit does not create evidence, actor, judge, verifier, oracle, aggregate,
capture, or score artifacts.  It snapshots one fully finalized diagnostic
session prefix under the session claim lock and fixes the only paths a later
unit may use.
"""

from __future__ import annotations

import argparse
import dataclasses
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import stat
import sys
from typing import Any, Sequence


SHA256_PREFIX = "sha256:"
CANONICALIZATION = "GRIDWORKS_CANONICAL_JSON_V1"
CHAIN_CLAIM_SCHEMA = "gridworks.realtime-evaluation-chain-claim.v1"
CHAIN_PRODUCER_SCHEMA = "gridworks.realtime-evaluation-chain-producer-authority.v1"

SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_REPOSITORY_ROOT = SCRIPT_DIR.parents[2]
SESSION_MODULE_PATH = SCRIPT_DIR / "realtime-session-authority.py"
POLICY_PATH = SCRIPT_DIR / "realtime-evaluation-chain-policy.json"
CLAIM_SCHEMA_PATH = SCRIPT_DIR / "realtime-evaluation-chain-claim.schema.json"
EXPECTED_POLICY_RAW_SHA256 = (
    "sha256:accef28faf6583f844e082e0a4c22f2087810111897cfb4c7bbf8c287a37e6d0"
)

CHAIN_PRODUCER_PATH_ROLES = (
    (
        "tools/commercial-ux/native/realtime-evaluation-chain-authority.py",
        "CHAIN_CLAIM_PRODUCER_AND_SEMANTIC_VERIFIER",
    ),
    (
        "tools/commercial-ux/native/realtime-evaluation-chain-claim.schema.json",
        "STRUCTURAL_CHAIN_CLAIM_SCHEMA_NON_AUTHORITY",
    ),
    (
        "tools/commercial-ux/native/realtime-evaluation-chain-policy.json",
        "CHAIN_POLICY",
    ),
    (
        "tools/commercial-ux/native/realtime-session-authority.py",
        "BOUND_PARENT_SESSION_SEMANTIC_VERIFIER_DEPENDENCY",
    ),
    (
        "tools/commercial-ux/native/test-realtime-evaluation-chain-authority.py",
        "ADVERSARIAL_CHAIN_CLAIM_TEST_SPEC_NON_RUNTIME",
    ),
)

FUTURE_ARTIFACT_RELATIVE_PATHS = (
    "artifacts/evidence-index.json",
    "artifacts/actor-terminal.json",
    "artifacts/judge-input.json",
    "artifacts/judge-terminal.json",
    "artifacts/verifier-result.json",
    "artifacts/oracle-ledger.json",
    "artifacts/aggregate.json",
)


class ChainAuthorityError(ValueError):
    """Raised when an evaluation-chain parent cannot be established exactly."""


def _load_session_module() -> Any:
    spec = importlib.util.spec_from_file_location(
        "realtime_session_authority_for_chain",
        SESSION_MODULE_PATH,
    )
    if spec is None or spec.loader is None:
        raise ChainAuthorityError(
            f"cannot load session authority module {SESSION_MODULE_PATH}"
        )
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


SESSION = _load_session_module()
CANDIDATE = SESSION.CANDIDATE


def sha256_bytes(data: bytes) -> str:
    return SESSION.sha256_bytes(data)


def canonical_bytes(value: Any) -> bytes:
    return SESSION.canonical_bytes(value)


def canonical_sha256(value: Any) -> str:
    return SESSION.canonical_sha256(value)


def strict_typed_equal(left: Any, right: Any) -> bool:
    return SESSION.strict_typed_equal(left, right)


def strict_json_bytes(data: bytes, label: str) -> dict[str, Any]:
    try:
        return SESSION.strict_json_bytes(data, label)
    except SESSION.SessionAuthorityError as error:
        raise ChainAuthorityError(str(error)) from error


def json_file_bytes(value: dict[str, Any]) -> bytes:
    return SESSION.json_file_bytes(value)


def self_hash(value: dict[str, Any], field: str) -> str:
    return SESSION.self_hash(value, field)


def require_exact_keys(
    value: dict[str, Any],
    expected: Sequence[str] | frozenset[str] | set[str],
    label: str,
) -> None:
    if set(value) != set(expected):
        raise ChainAuthorityError(f"{label} field set drift")


def read_regular_exact(path: Path, label: str) -> tuple[Path, bytes]:
    try:
        return SESSION.read_regular_exact(path, label)
    except SESSION.SessionAuthorityError as error:
        raise ChainAuthorityError(str(error)) from error


def canonical_existing_directory(path: Path, label: str) -> Path:
    try:
        return SESSION.canonical_existing_directory(path, label)
    except SESSION.SessionAuthorityError as error:
        raise ChainAuthorityError(str(error)) from error


def exclusive_create_directory(path: Path, label: str) -> Path:
    try:
        return SESSION.exclusive_create_directory(path, label)
    except SESSION.SessionAuthorityError as error:
        raise ChainAuthorityError(str(error)) from error


def exclusive_write(path: Path, content: bytes, label: str) -> None:
    try:
        SESSION.exclusive_write(path, content, label)
    except SESSION.SessionAuthorityError as error:
        raise ChainAuthorityError(str(error)) from error


def fsync_directory(path: Path) -> None:
    SESSION.fsync_directory(path)


def load_chain_policy() -> tuple[dict[str, Any], bytes]:
    _resolved, data = read_regular_exact(POLICY_PATH, "evaluation chain policy")
    if sha256_bytes(data) != EXPECTED_POLICY_RAW_SHA256:
        raise ChainAuthorityError("evaluation chain policy raw-byte drift")
    policy = strict_json_bytes(data, "evaluation chain policy")
    return policy, data


def validate_chain_policy(
    policy: dict[str, Any],
    data: bytes | None = None,
) -> None:
    expected, expected_bytes = load_chain_policy()
    if not strict_typed_equal(policy, expected):
        raise ChainAuthorityError("evaluation chain policy object drift")
    if data is not None and data != expected_bytes:
        raise ChainAuthorityError("evaluation chain policy byte drift")
    require_exact_keys(
        policy,
        {
            "schemaVersion",
            "canonicalization",
            "parentSessionAuthority",
            "evaluatorProducerAuthority",
            "chainAuthority",
            "routeBoundaries",
            "futureArtifactPlan",
            "producerPlan",
            "limitations",
        },
        "evaluation chain policy",
    )
    parent = policy["parentSessionAuthority"]
    chain = policy["chainAuthority"]
    artifacts = policy["futureArtifactPlan"]
    if (
        policy["schemaVersion"]
        != "gridworks.realtime-evaluation-chain-policy.v1"
        or parent["sourceCommit"]
        != "5a31ff35a6e2d293c2f1800e4297945ecf3a5584"
        or parent["evaluatorProducerFilesSha256"]
        != "sha256:faea99acbc3a09334ccc8fbb9140a894f99fbfb0340ce224e9f12cba9b54b3e9"
        or chain["chainRootSuffix"] != ".evaluation-chain-v1"
        or chain["officialCommercialUX"] is not False
        or chain["scoreBearingCaptureAllowed"] is not False
        or chain["commercialUXProxy"] is not None
        or artifacts["orderedPaths"] != list(FUTURE_ARTIFACT_RELATIVE_PATHS)
        or artifacts["creationAuthorizedByThisUnit"] is not False
    ):
        raise ChainAuthorityError("evaluation chain policy invariant drift")


def bind_chain_evaluator_authority(
    repository_root: Path,
    revision: str,
) -> dict[str, Any]:
    root = CANDIDATE.resolve_repository_root(repository_root)
    expected_script_dir = (
        root / "tools" / "commercial-ux" / "native"
    ).resolve(strict=True)
    if SCRIPT_DIR != expected_script_dir or Path(__file__).resolve(strict=True) != (
        expected_script_dir / "realtime-evaluation-chain-authority.py"
    ).resolve(strict=True):
        raise ChainAuthorityError(
            "running chain evaluator is outside the candidate repository"
        )
    try:
        source_commit = CANDIDATE.resolve_source_commit(root, revision)
        entries = CANDIDATE.git_tree_entries(root, source_commit)
    except CANDIDATE.CandidateAuthorityError as error:
        raise ChainAuthorityError(str(error)) from error
    by_path = {
        path: (mode, object_type, object_id)
        for mode, object_type, object_id, path in entries
    }
    rows: list[dict[str, Any]] = []
    for path, role in CHAIN_PRODUCER_PATH_ROLES:
        entry = by_path.get(path)
        if entry is None:
            raise ChainAuthorityError(
                f"chain authority source commit lacks evaluator file: {path}"
            )
        mode, object_type, object_id = entry
        if mode not in {"100644", "100755"} or object_type != "blob":
            raise ChainAuthorityError(
                f"chain evaluator authority is not a regular Git blob: {path}"
            )
        try:
            git_data = CANDIDATE.run_git_command(
                root,
                ["cat-file", "blob", "--", object_id],
                label=f"chain evaluator Git blob {path}",
            )
        except CANDIDATE.CandidateAuthorityError as error:
            raise ChainAuthorityError(str(error)) from error
        _resolved, running_data = read_regular_exact(
            root / path,
            f"running chain evaluator authority {path}",
        )
        if running_data != git_data:
            raise ChainAuthorityError(
                f"running chain evaluator differs from source commit: {path}"
            )
        rows.append(CANDIDATE.GitBlob(path, mode, object_id, role, git_data).row())
    rows.sort(key=lambda row: row["path"])
    policy, _policy_bytes = load_chain_policy()
    if [row["path"] for row in rows] != policy["evaluatorProducerAuthority"]["paths"]:
        raise ChainAuthorityError("chain evaluator path order differs from policy")
    return {
        "schemaVersion": CHAIN_PRODUCER_SCHEMA,
        "sourceCommit": source_commit,
        "fileCount": len(rows),
        "files": rows,
        "filesSha256": canonical_sha256(rows),
        "runningFilesMatchGitBlobs": True,
        "gitCommandAuthority": CANDIDATE.bind_git_command_authority(root),
        "parentSessionSemanticVerifierDependencyBound": True,
        "semanticVerifierEntryPoint": (
            "verify_chain_claim_against_reconstructed_authority"
        ),
        "structuralSchemaAuthority": "STRUCTURAL_ONLY_NOT_CHAIN_AUTHORITY",
    }


@dataclasses.dataclass(frozen=True)
class SnapshotFile:
    source_path: Path | None
    source_logical_path: str
    snapshot_relative_path: str
    role: str
    materialization: str
    data: bytes

    def row(self) -> dict[str, Any]:
        canonical: str | None = None
        if self.role == "ATTEMPT_DIAGNOSTIC_OUTPUT":
            def reject_nonfinite(value: str) -> None:
                raise ValueError(f"non-JSON numeric token {value}")

            try:
                parsed = json.loads(
                    self.data.decode("utf-8"),
                    parse_constant=reject_nonfinite,
                )
            except (UnicodeError, json.JSONDecodeError, ValueError):
                pass
            else:
                canonical = canonical_sha256(parsed)
        else:
            value = strict_json_bytes(self.data, self.source_logical_path)
            canonical = canonical_sha256(value)
        return {
            "sourcePath": str(self.source_path) if self.source_path is not None else None,
            "sourceLogicalPath": self.source_logical_path,
            "snapshotRelativePath": self.snapshot_relative_path,
            "role": self.role,
            "materialization": self.materialization,
            "rawSha256": sha256_bytes(self.data),
            "canonicalSha256": canonical,
            "byteLength": len(self.data),
        }


@dataclasses.dataclass(frozen=True)
class ParentPrefix:
    context: Any
    claim: dict[str, Any]
    claim_bytes: bytes
    files: tuple[SnapshotFile, ...]
    attempt_audit: tuple[dict[str, Any], ...]
    selected_terminal: dict[str, Any]


def _snapshot_file(
    source_path: Path,
    session_root: Path,
    role: str,
) -> SnapshotFile:
    resolved, data = read_regular_exact(source_path, f"parent prefix {role}")
    try:
        logical = str(resolved.relative_to(session_root))
    except ValueError as error:
        raise ChainAuthorityError("parent prefix file escaped session root") from error
    return SnapshotFile(
        resolved,
        logical,
        f"inputs/session/{logical}",
        role,
        "EXACT_PARENT_FILE_BYTES_UNDER_SESSION_CLAIM_FLOCK",
        data,
    )


def _validate_parent_authority(claim: dict[str, Any]) -> None:
    policy, _policy_bytes = load_chain_policy()
    expected = policy["parentSessionAuthority"]
    if (
        claim.get("status") != expected["sessionStatus"]
        or claim.get("sessionAuthoritySourceCommit") != expected["sourceCommit"]
        or claim.get("sessionEvaluatorAuthority", {}).get("filesSha256")
        != expected["evaluatorProducerFilesSha256"]
        or claim.get("sessionPolicyRawSha256") != expected["sessionPolicyRawSha256"]
        or claim.get("candidateAuthority", {}).get("candidateSha256")
        != expected["candidateSha256"]
        or claim.get("officialCommercialUX") is not False
        or claim.get("scoreBearingCaptureAllowed") is not False
    ):
        raise ChainAuthorityError("parent session reviewed authority projection drift")


def _read_finalized_parent_prefix_without_lock(
    repository_root: Path,
    session_claim_path: Path,
) -> ParentPrefix:
    try:
        context, claim, claim_bytes = (
            SESSION.verify_session_claim_against_reconstructed_authority(
                repository_root,
                session_claim_path,
            )
        )
        SESSION._verify_session_state_without_lock(repository_root, session_claim_path)
    except SESSION.SessionAuthorityError as error:
        raise ChainAuthorityError(str(error)) from error
    _validate_parent_authority(claim)
    root = Path(claim["canonicalSessionRoot"])
    files: list[SnapshotFile] = [
        SnapshotFile(
            Path(claim["canonicalClaimPath"]),
            "session-claim.json",
            "inputs/session/session-claim.json",
            "FINALIZED_PARENT_SESSION_CLAIM",
            "EXACT_PARENT_FILE_BYTES_UNDER_SESSION_CLAIM_FLOCK",
            claim_bytes,
        ),
        _snapshot_file(
            Path(claim["candidateAuthority"]["candidateManifestPath"]),
            root,
            "BOUND_CANDIDATE_MANIFEST",
        ),
        _snapshot_file(
            Path(claim["candidateAuthority"]["storyManifestPath"]),
            root,
            "BOUND_STORY_MANIFEST",
        ),
    ]
    audit: list[dict[str, Any]] = []
    route = claim["routeBinding"]
    if route["executionAuthorized"] is False:
        terminal = claim.get("unavailableRouteTerminal")
        if not isinstance(terminal, dict) or claim.get("attempts") != []:
            raise ChainAuthorityError("unavailable route terminal boundary drift")
        if terminal.get("outcome") != "ROUTE_UNAVAILABLE_NO_EXECUTION":
            raise ChainAuthorityError("unavailable route outcome drift")
        terminal_bytes = json_file_bytes(terminal)
        files.append(SnapshotFile(
            None,
            "unavailable-route-terminal.json",
            "inputs/session/unavailable-route-terminal.json",
            "CANONICAL_NESTED_UNAVAILABLE_ROUTE_TERMINAL",
            "CANONICAL_EXTRACTION_FROM_FINALIZED_SESSION_CLAIM",
            terminal_bytes,
        ))
        selected = {
            "terminalKind": "UNAVAILABLE_ROUTE_TERMINAL",
            "attemptOrdinal": None,
            "attemptId": None,
            "outcome": terminal["outcome"],
            "terminalSnapshotRelativePath": (
                "inputs/session/unavailable-route-terminal.json"
            ),
            "terminalRawSha256": sha256_bytes(terminal_bytes),
            "terminalSelfSha256": terminal["unavailableRouteTerminalSha256"],
        }
    else:
        predecessor: tuple[bytes, dict[str, Any]] | None = None
        terminal_count = 0
        success_count = 0
        selected = {}
        for attempt in claim["attempts"]:
            attempt_root = Path(attempt["attemptRoot"])
            if not os.path.lexists(attempt_root):
                break
            if not os.path.lexists(Path(attempt["terminalReceiptPath"])):
                raise ChainAuthorityError(
                    "executable parent session has a non-terminal started attempt"
                )
            try:
                terminal_bytes, terminal = SESSION._read_and_validate_terminal(
                    claim,
                    claim_bytes,
                    attempt,
                    predecessor,
                )
            except SESSION.SessionAuthorityError as error:
                raise ChainAuthorityError(str(error)) from error
            start = _snapshot_file(
                Path(attempt["startReceiptPath"]),
                root,
                "ATTEMPT_START_RECEIPT",
            )
            output = _snapshot_file(
                Path(attempt["outputPath"]),
                root,
                "ATTEMPT_DIAGNOSTIC_OUTPUT",
            )
            terminal_file = _snapshot_file(
                Path(attempt["terminalReceiptPath"]),
                root,
                "ATTEMPT_TERMINAL_RECEIPT",
            )
            if terminal_file.data != terminal_bytes:
                raise ChainAuthorityError("terminal bytes changed during prefix read")
            start_value = strict_json_bytes(start.data, "snapshotted attempt start")
            start_self = start_value.get("evaluationAttemptStartSha256")
            if (
                sha256_bytes(start.data)
                != terminal["attemptStartReceiptRawSha256"]
                or start_self != terminal["evaluationAttemptStartSha256"]
                or sha256_bytes(output.data) != terminal["outputRawSha256"]
                or len(output.data) != terminal["outputByteLength"]
                or output.row()["canonicalSha256"]
                != terminal["outputCanonicalSha256"]
            ):
                raise ChainAuthorityError(
                    "snapshotted attempt bytes disagree with terminal bindings"
                )
            files.extend((start, output, terminal_file))
            audit.append({
                "attemptOrdinal": attempt["attemptOrdinal"],
                "attemptId": attempt["attemptId"],
                "outcome": terminal["outcome"],
                "outcomeRetryable": terminal["outcomeRetryable"],
                "nextAttemptAllowed": terminal["nextAttemptAllowed"],
                "startReceiptRawSha256": sha256_bytes(start.data),
                "outputRawSha256": sha256_bytes(output.data),
                "outputCanonicalSha256": terminal["outputCanonicalSha256"],
                "terminalReceiptRawSha256": sha256_bytes(terminal_bytes),
                "terminalReceiptSha256": terminal[
                    "evaluationAttemptTerminalSha256"
                ],
            })
            terminal_count += 1
            if terminal["outcome"] == "SUCCESS":
                success_count += 1
            predecessor = (terminal_bytes, terminal)
            selected = {
                "terminalKind": "ATTEMPT_SUCCESS",
                "attemptOrdinal": attempt["attemptOrdinal"],
                "attemptId": attempt["attemptId"],
                "outcome": terminal["outcome"],
                "terminalSnapshotRelativePath": terminal_file.snapshot_relative_path,
                "terminalRawSha256": sha256_bytes(terminal_bytes),
                "terminalSelfSha256": terminal[
                    "evaluationAttemptTerminalSha256"
                ],
            }
            if terminal["nextAttemptAllowed"] is False:
                break
        if (
            terminal_count == 0
            or success_count != 1
            or selected.get("outcome") != "SUCCESS"
            or predecessor is None
            or predecessor[1]["nextAttemptAllowed"] is not False
        ):
            raise ChainAuthorityError(
                "executable parent session must end in exactly one SUCCESS"
            )
        for later in claim["attempts"][terminal_count:]:
            if os.path.lexists(Path(later["attemptRoot"])):
                raise ChainAuthorityError("parent attempt exists after selected terminal")
    return ParentPrefix(
        context,
        claim,
        claim_bytes,
        tuple(files),
        tuple(audit),
        selected,
    )


def _route_boundary(prefix: ParentPrefix) -> dict[str, Any]:
    route = prefix.claim["routeBinding"]
    policy, _policy_bytes = load_chain_policy()
    kind = route["routeKind"]
    expected = policy["routeBoundaries"].get(kind)
    if not isinstance(expected, dict):
        raise ChainAuthorityError("parent route kind is outside chain policy")
    if (
        route.get("routeDisposition") != expected["requiredDisposition"]
        or route.get("nativePresentationObserved") is not False
        or route.get("scoreBearingEvidence") is not False
        or prefix.selected_terminal["outcome"] != expected["selectedTerminalOutcome"]
    ):
        raise ChainAuthorityError("parent route claim exceeds evaluation-chain boundary")
    boundary: dict[str, Any] = {
        "routeKind": kind,
        "candidateProfileId": route["candidateProfileId"],
        "sessionProfileId": route["sessionProfileId"],
        "selector": route["selector"],
        "availability": route["availability"],
        "executionAuthorized": route["executionAuthorized"],
        "routeDisposition": route["routeDisposition"],
        "routeBindingSha256": route["routeBindingSha256"],
        "evidenceClass": expected["evidenceClass"],
        "nativePresentationObserved": False,
        "scoreBearingEvidence": False,
    }
    if kind == "TARGETED_CHECKPOINT":
        future = route.get("futureEventStatusBar")
        if (
            not isinstance(future, dict)
            or future.get("requiredSignals") != expected["requiredFutureEventSignals"]
            or future.get("headlessWiringStatus")
            != expected["futureEventHeadlessWiringStatus"]
            or future.get("nativeQualityStatus")
            != expected["futureEventNativeQualityStatus"]
            or route.get("nativeQualityObserved") is not False
        ):
            raise ChainAuthorityError("future-event status bar boundary drift")
        boundary["futureEventStatusBar"] = {
            "requiredSignals": list(future["requiredSignals"]),
            "headlessWiringStatus": future["headlessWiringStatus"],
            "nativeQualityStatus": future["nativeQualityStatus"],
            "nativeQualityObserved": False,
        }
        boundary["authoredReachabilityOnly"] = False
        boundary["nativeReachabilityClaim"] = False
    elif kind == "STORY_PART_UNIT":
        if (
            route.get("authoredReachabilityOnly") is not True
            or route.get("nativeReachabilityClaim") is not False
        ):
            raise ChainAuthorityError("story-part reachability boundary drift")
        boundary["futureEventStatusBar"] = None
        boundary["authoredReachabilityOnly"] = True
        boundary["nativeReachabilityClaim"] = False
    else:
        if (
            route.get("executionAuthorized") is not False
            or route.get("availability") != expected["availability"]
            or prefix.attempt_audit
        ):
            raise ChainAuthorityError("full-flow unavailable boundary drift")
        boundary["futureEventStatusBar"] = None
        boundary["authoredReachabilityOnly"] = False
        boundary["nativeReachabilityClaim"] = False
    boundary["routeBoundarySha256"] = self_hash(
        boundary,
        "routeBoundarySha256",
    )
    return boundary


def _snapshot_rows(prefix: ParentPrefix) -> list[dict[str, Any]]:
    rows = [value.row() for value in prefix.files]
    paths = [row["snapshotRelativePath"] for row in rows]
    if len(paths) != len(set(paths)):
        raise ChainAuthorityError("parent prefix snapshot path collision")
    return rows


def _derive_chain_root(session_root: Path) -> Path:
    policy, _policy_bytes = load_chain_policy()
    root = canonical_existing_directory(session_root, "parent session root")
    return root.parent / f"{root.name}{policy['chainAuthority']['chainRootSuffix']}"


def compose_chain_claim(
    prefix: ParentPrefix,
    evaluator_authority: dict[str, Any],
    chain_root: Path,
    chain_nonce: str,
) -> dict[str, Any]:
    if re.fullmatch(r"[0-9a-f]{64}", chain_nonce) is None:
        raise ChainAuthorityError("chain nonce must be 32 lowercase hex bytes")
    root = canonical_existing_directory(chain_root, "evaluation chain root")
    expected_root = _derive_chain_root(Path(prefix.claim["canonicalSessionRoot"]))
    if root != expected_root:
        raise ChainAuthorityError("evaluation chain root is not the deterministic sibling")
    policy, policy_bytes = load_chain_policy()
    rows = _snapshot_rows(prefix)
    boundary = _route_boundary(prefix)
    root_binding = sha256_bytes(str(root).encode("utf-8"))
    selected_sha = prefix.selected_terminal["terminalSelfSha256"]
    chain_id = canonical_sha256({
        "schemaVersion": CHAIN_CLAIM_SCHEMA,
        "chainNonce": chain_nonce,
        "chainRootBindingSha256": root_binding,
        "parentSessionId": prefix.claim["sessionId"],
        "parentSessionClaimRawSha256": sha256_bytes(prefix.claim_bytes),
        "selectedTerminalSelfSha256": selected_sha,
        "inputSnapshotTreeSha256": canonical_sha256(rows),
        "chainPolicyRawSha256": sha256_bytes(policy_bytes),
        "chainEvaluatorFilesSha256": evaluator_authority["filesSha256"],
    })
    claim_path = root / policy["chainAuthority"]["claimFileName"]
    artifact_root = root / policy["futureArtifactPlan"]["artifactRootRelativePath"]
    claim: dict[str, Any] = {
        "schemaVersion": CHAIN_CLAIM_SCHEMA,
        "canonicalization": CANONICALIZATION,
        "status": "FINALIZED_PARENT_PREFIX_NO_EVALUATION_OUTPUTS",
        "chainPolicyRawSha256": sha256_bytes(policy_bytes),
        "chainClaimSchemaRawSha256": sha256_bytes(
            read_regular_exact(CLAIM_SCHEMA_PATH, "evaluation chain claim schema")[1]
        ),
        "chainAuthoritySourceCommit": evaluator_authority["sourceCommit"],
        "chainEvaluatorAuthority": evaluator_authority,
        "chainId": chain_id,
        "chainNonce": chain_nonce,
        "canonicalChainRoot": str(root),
        "canonicalClaimPath": str(claim_path),
        "chainRootBindingSha256": root_binding,
        "parentSessionAuthority": {
            "sessionId": prefix.claim["sessionId"],
            "canonicalSessionRoot": prefix.claim["canonicalSessionRoot"],
            "canonicalSessionClaimPath": prefix.claim["canonicalClaimPath"],
            "sessionClaimSha256": prefix.claim["sessionClaimSha256"],
            "sessionClaimRawSha256": sha256_bytes(prefix.claim_bytes),
            "sessionAuthoritySourceCommit": prefix.claim[
                "sessionAuthoritySourceCommit"
            ],
            "sessionEvaluatorProducerFilesSha256": prefix.claim[
                "sessionEvaluatorAuthority"
            ]["filesSha256"],
            "sessionPolicyRawSha256": prefix.claim["sessionPolicyRawSha256"],
            "candidateSha256": prefix.claim["candidateAuthority"]["candidateSha256"],
            "candidateSourceCommit": prefix.claim["candidateAuthority"][
                "candidateSourceCommit"
            ],
            "candidateManifestRawSha256": prefix.claim["candidateAuthority"][
                "candidateManifestRawSha256"
            ],
            "candidateExecutionSha256": prefix.claim["candidateAuthority"][
                "candidateExecutionSha256"
            ],
            "candidatePackageTreeSha256": prefix.claim["candidateAuthority"][
                "candidatePackageTreeSha256"
            ],
            "candidateEvaluatorProducerFilesSha256": prefix.claim[
                "candidateAuthority"
            ]["candidateEvaluatorProducerFilesSha256"],
            "storyManifestRawSha256": prefix.claim["candidateAuthority"][
                "storyManifestRawSha256"
            ],
            "routeBindingSha256": prefix.claim["routeBinding"][
                "routeBindingSha256"
            ],
            "parentSemanticVerificationUnderClaimLock": True,
        },
        "routeBoundary": boundary,
        "attemptAudit": list(prefix.attempt_audit),
        "selectedRouteTerminal": dict(prefix.selected_terminal),
        "inputSnapshot": {
            "canonicalInputRoot": str(root / "inputs" / "session"),
            "fileCount": len(rows),
            "files": rows,
            "treeSha256": canonical_sha256(rows),
            "fullFinalizedPrefixCapturedUnderSessionClaimLock": True,
        },
        "fixedFutureArtifactPaths": [
            str(root / relative) for relative in FUTURE_ARTIFACT_RELATIVE_PATHS
        ],
        "futureArtifactPlan": {
            "canonicalArtifactRoot": str(artifact_root),
            "artifactRootRelativePath": "artifacts",
            "artifactRootCreatedAtClaimCreation": False,
            "creationAuthorizedByThisUnit": False,
            "orderedRelativePaths": list(FUTURE_ARTIFACT_RELATIVE_PATHS),
        },
        "producerPlan": dict(policy["producerPlan"]),
        "modelExecutionAuthority": {
            "status": "UNAVAILABLE",
            "requiredModel": "gpt-5.6-sol",
            "requiredReasoningEffort": "ultra",
            "platformOrEquivalentTranscriptAuthorityBound": False,
            "judgeExecutionAuthorized": False,
        },
        "finalizationAuthority": {
            "sessionClaimFlockHeldAcrossVerificationSelectionSnapshotsAndClaim": True,
            "inputFilesExclusiveWriteFsyncCompleted": True,
            "inputDirectoriesFsyncCompleted": True,
            "artifactRootAbsentAtClaimCreation": True,
            "claimPathAbsentUntilAllInputsFinalized": True,
            "claimFileIsLastCommitMarker": True,
            "claimWriteMode": "O_EXCL_FSYNC_FILE_AND_PARENT",
        },
        "officialCommercialUX": False,
        "scoreBearingCaptureAllowed": False,
        "commercialUXProxy": None,
        "claimProducerExecutionAttested": False,
        "freshnessAuthority": "LOCAL_OS_RANDOM_NON_SCORE_ONLY",
        "limitations": list(policy["limitations"]),
    }
    claim["evaluationChainClaimSha256"] = self_hash(
        claim,
        "evaluationChainClaimSha256",
    )
    return claim


def _create_snapshot_parent(chain_root: Path, relative_path: str) -> Path:
    path = chain_root / relative_path
    current = chain_root
    for component in Path(relative_path).parts[:-1]:
        current = current / component
        if os.path.lexists(current):
            canonical_existing_directory(current, f"snapshot directory {component}")
        else:
            exclusive_create_directory(current, f"snapshot directory {component}")
    return path


def create_chain_claim(
    repository_root: Path,
    session_claim_path: Path,
    *,
    chain_authority_revision: str = "HEAD",
) -> tuple[Path, dict[str, Any]]:
    policy, policy_bytes = load_chain_policy()
    validate_chain_policy(policy, policy_bytes)
    evaluator = bind_chain_evaluator_authority(
        repository_root,
        chain_authority_revision,
    )
    try:
        lock = SESSION.exclusive_claim_lock(session_claim_path)
        with lock:
            prefix = _read_finalized_parent_prefix_without_lock(
                repository_root,
                session_claim_path,
            )
            expected_root = _derive_chain_root(
                Path(prefix.claim["canonicalSessionRoot"])
            )
            root = exclusive_create_directory(expected_root, "evaluation chain root")
            for snapshot in prefix.files:
                target = _create_snapshot_parent(root, snapshot.snapshot_relative_path)
                exclusive_write(target, snapshot.data, f"chain snapshot {snapshot.role}")
            input_root = canonical_existing_directory(
                root / "inputs" / "session",
                "chain input snapshot root",
            )
            fsync_directory(input_root)
            fsync_directory(input_root.parent)
            refreshed_prefix = _read_finalized_parent_prefix_without_lock(
                repository_root,
                session_claim_path,
            )
            if (
                refreshed_prefix.claim_bytes != prefix.claim_bytes
                or not strict_typed_equal(
                    _snapshot_rows(refreshed_prefix),
                    _snapshot_rows(prefix),
                )
                or not strict_typed_equal(
                    list(refreshed_prefix.attempt_audit),
                    list(prefix.attempt_audit),
                )
                or not strict_typed_equal(
                    refreshed_prefix.selected_terminal,
                    prefix.selected_terminal,
                )
            ):
                raise ChainAuthorityError(
                    "parent session prefix changed during snapshot finalization"
                )
            for snapshot in refreshed_prefix.files:
                _resolved, copied = read_regular_exact(
                    root / snapshot.snapshot_relative_path,
                    f"final chain snapshot {snapshot.snapshot_relative_path}",
                )
                if copied != snapshot.data:
                    raise ChainAuthorityError(
                        "chain snapshot changed before claim commit"
                    )
            nonce = secrets.token_bytes(32).hex()
            claim = compose_chain_claim(refreshed_prefix, evaluator, root, nonce)
            claim_path = root / policy["chainAuthority"]["claimFileName"]
            exclusive_write(
                claim_path,
                json_file_bytes(claim),
                "evaluation chain claim",
            )
            return claim_path, claim
    except SESSION.SessionAuthorityError as error:
        raise ChainAuthorityError(str(error)) from error


def _validate_chain_inventory(
    claim: dict[str, Any],
) -> None:
    root = canonical_existing_directory(
        Path(claim["canonicalChainRoot"]),
        "bound evaluation chain root",
    )
    names = {path.name for path in root.iterdir()}
    expected = {"inputs", "evaluation-chain-claim.json"}
    if names != expected:
        raise ChainAuthorityError("evaluation chain root inventory drift")
    inputs = canonical_existing_directory(root / "inputs", "chain inputs root")
    if [path.name for path in inputs.iterdir()] != ["session"]:
        raise ChainAuthorityError("chain inputs root inventory drift")
    rows = claim["inputSnapshot"]["files"]
    expected_files = {row["snapshotRelativePath"] for row in rows}
    actual_files: set[str] = set()
    actual_directories: set[str] = {"inputs"}
    for directory, directory_names, file_names in os.walk(inputs, followlinks=False):
        base = Path(directory)
        for name in directory_names:
            child = canonical_existing_directory(
                base / name,
                f"chain input directory {name}",
            )
            actual_directories.add(str(child.relative_to(root)))
        for name in file_names:
            path = base / name
            read_regular_exact(path, f"chain input file {name}")
            actual_files.add(str(path.relative_to(root)))
    if actual_files != expected_files:
        raise ChainAuthorityError("chain input snapshot inventory drift")
    expected_directories = {"inputs"}
    for relative in expected_files:
        current = Path(relative).parent
        while str(current) != ".":
            expected_directories.add(str(current))
            current = current.parent
    if actual_directories != expected_directories:
        raise ChainAuthorityError("chain input snapshot directory inventory drift")


def _verify_snapshot_bytes(
    chain_root: Path,
    prefix: ParentPrefix,
    submitted_rows: list[dict[str, Any]],
) -> None:
    expected_rows = _snapshot_rows(prefix)
    if not strict_typed_equal(submitted_rows, expected_rows):
        raise ChainAuthorityError("chain input snapshot row reconstruction mismatch")
    by_relative = {
        value.snapshot_relative_path: value for value in prefix.files
    }
    for row in expected_rows:
        snapshot = by_relative[row["snapshotRelativePath"]]
        _resolved, data = read_regular_exact(
            chain_root / row["snapshotRelativePath"],
            f"chain snapshot {row['snapshotRelativePath']}",
        )
        if data != snapshot.data:
            raise ChainAuthorityError("chain snapshot differs from finalized parent bytes")


def verify_chain_claim_against_reconstructed_authority(
    repository_root: Path,
    claim_path: Path,
) -> tuple[ParentPrefix, dict[str, Any], bytes]:
    resolved_claim, claim_bytes = read_regular_exact(claim_path, "evaluation chain claim")
    claim = strict_json_bytes(claim_bytes, "evaluation chain claim")
    if claim_bytes != json_file_bytes(claim):
        raise ChainAuthorityError("evaluation chain claim is not canonical JSON file bytes")
    if claim.get("evaluationChainClaimSha256") != self_hash(
        claim,
        "evaluationChainClaimSha256",
    ):
        raise ChainAuthorityError("evaluation chain claim self-hash mismatch")
    if claim.get("canonicalClaimPath") != str(resolved_claim):
        raise ChainAuthorityError("evaluation chain claim path differs from bound path")
    root_value = claim.get("canonicalChainRoot")
    if not isinstance(root_value, str):
        raise ChainAuthorityError("evaluation chain root is missing")
    root = canonical_existing_directory(Path(root_value), "bound evaluation chain root")
    if resolved_claim.parent != root:
        raise ChainAuthorityError("evaluation chain claim is outside its bound root")
    if claim.get("chainRootBindingSha256") != sha256_bytes(
        str(root).encode("utf-8")
    ):
        raise ChainAuthorityError("evaluation chain root binding mismatch")
    policy, policy_bytes = load_chain_policy()
    validate_chain_policy(policy, policy_bytes)
    parent = claim.get("parentSessionAuthority")
    if not isinstance(parent, dict):
        raise ChainAuthorityError("parent session authority is missing")
    session_claim_value = parent.get("canonicalSessionClaimPath")
    if not isinstance(session_claim_value, str):
        raise ChainAuthorityError("parent session claim path is missing")
    source_commit = claim.get("chainAuthoritySourceCommit")
    if not isinstance(source_commit, str):
        raise ChainAuthorityError("chain evaluator source commit is missing")
    evaluator = bind_chain_evaluator_authority(repository_root, source_commit)
    try:
        with SESSION.exclusive_claim_lock(Path(session_claim_value)):
            prefix = _read_finalized_parent_prefix_without_lock(
                repository_root,
                Path(session_claim_value),
            )
            expected_root = _derive_chain_root(
                Path(prefix.claim["canonicalSessionRoot"])
            )
            if root != expected_root:
                raise ChainAuthorityError(
                    "evaluation chain root deterministic sibling binding drift"
                )
            _verify_snapshot_bytes(
                root,
                prefix,
                claim.get("inputSnapshot", {}).get("files", []),
            )
            expected = compose_chain_claim(
                prefix,
                evaluator,
                root,
                claim.get("chainNonce"),
            )
            if not strict_typed_equal(claim, expected):
                raise ChainAuthorityError(
                    "evaluation chain claim differs from reconstructed authority"
                )
            _validate_chain_inventory(claim)
            _verify_snapshot_bytes(
                root,
                prefix,
                claim["inputSnapshot"]["files"],
            )
            _resolved_again, claim_bytes_again = read_regular_exact(
                resolved_claim,
                "evaluation chain claim post-verification",
            )
            if claim_bytes_again != claim_bytes:
                raise ChainAuthorityError(
                    "evaluation chain claim changed during verification"
                )
            return prefix, claim, claim_bytes
    except SESSION.SessionAuthorityError as error:
        raise ChainAuthorityError(str(error)) from error


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Create and verify a non-score finalized evaluation-chain claim."
    )
    commands = parser.add_subparsers(dest="command", required=True)
    create = commands.add_parser("create-chain")
    create.add_argument(
        "--repository-root",
        type=Path,
        default=DEFAULT_REPOSITORY_ROOT,
    )
    create.add_argument("--session-claim", type=Path, required=True)
    create.add_argument("--chain-authority-revision", default="HEAD")
    verify = commands.add_parser("verify-chain")
    verify.add_argument(
        "--repository-root",
        type=Path,
        default=DEFAULT_REPOSITORY_ROOT,
    )
    verify.add_argument("--chain-claim", type=Path, required=True)
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
        if args.command == "create-chain":
            claim_path, claim = create_chain_claim(
                args.repository_root,
                args.session_claim,
                chain_authority_revision=args.chain_authority_revision,
            )
            _print_result({
                "chainClaimPath": str(claim_path),
                "chainId": claim["chainId"],
                "status": claim["status"],
                "routeKind": claim["routeBoundary"]["routeKind"],
                "officialCommercialUX": False,
                "commercialUXProxy": None,
            })
        elif args.command == "verify-chain":
            _prefix, claim, claim_bytes = (
                verify_chain_claim_against_reconstructed_authority(
                    args.repository_root,
                    args.chain_claim,
                )
            )
            _print_result({
                "chainId": claim["chainId"],
                "chainClaimRawSha256": sha256_bytes(claim_bytes),
                "status": claim["status"],
                "routeKind": claim["routeBoundary"]["routeKind"],
                "officialCommercialUX": False,
                "commercialUXProxy": None,
            })
        else:
            raise ChainAuthorityError("unknown command")
    except (
        ChainAuthorityError,
        SESSION.SessionAuthorityError,
        CANDIDATE.CandidateAuthorityError,
    ) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
