#!/usr/bin/env python3
"""Create and verify one controlled local Codex transcript authority.

This authority runs one fresh, isolated, non-score echo probe.  It binds the
reviewed blocked current-route aggregate to an exact signed Codex CLI binary,
claim-first execution contract, stdout/stderr/output, and one new local rollout.
It is local process/transcript authority, not platform attestation, native
evidence, judgment, score aggregation, or an official CommercialUX score.
"""

from __future__ import annotations

import argparse
import dataclasses
import fcntl
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import pwd
import re
import signal
import stat
import subprocess
import sys
import time
from typing import Any, Mapping, Sequence


CANONICALIZATION = "GRIDWORKS_CANONICAL_JSON_V1"
PRODUCER_SCHEMA = "gridworks.realtime-controlled-codex-transcript-producer-authority.v1"
START_SCHEMA = "gridworks.realtime-controlled-codex-transcript-start.v1"
FINAL_SCHEMA = "gridworks.realtime-controlled-codex-transcript.v1"
INVENTORY_SCHEMA = "gridworks.realtime-controlled-codex-rollout-inventory.v1"
PROBE_OUTPUT_SCHEMA = "gridworks.realtime-controlled-codex-probe-output.v1"

SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_REPOSITORY_ROOT = SCRIPT_DIR.parents[2]
ARTIFACT_MODULE_PATH = SCRIPT_DIR / "realtime-current-route-artifact-authority.py"
POLICY_PATH = SCRIPT_DIR / "realtime-controlled-codex-transcript-policy.json"
START_SCHEMA_PATH = SCRIPT_DIR / "realtime-controlled-codex-transcript-start.schema.json"
FINAL_SCHEMA_PATH = SCRIPT_DIR / "realtime-controlled-codex-transcript.schema.json"
OUTPUT_SCHEMA_PATH = SCRIPT_DIR / "realtime-controlled-codex-probe-output.schema.json"
EXPECTED_POLICY_RAW_SHA256 = (
    "sha256:ff77b3f3b95958b4813efb2a2a91ac3533faefd99e208f4d098640d1bc739cf6"
)

PRODUCER_PATH_ROLES = (
    (
        "tools/commercial-ux/native/realtime-controlled-codex-probe-output.schema.json",
        "STRUCTURED_PROBE_OUTPUT_SCHEMA_NON_AUTHORITY",
    ),
    (
        "tools/commercial-ux/native/realtime-controlled-codex-transcript-authority.py",
        "CONTROLLED_CODEX_RUNNER_AND_SEMANTIC_VERIFIER",
    ),
    (
        "tools/commercial-ux/native/realtime-controlled-codex-transcript-policy.json",
        "CONTROLLED_CODEX_TRANSCRIPT_TRUST_POLICY",
    ),
    (
        "tools/commercial-ux/native/realtime-controlled-codex-transcript-start.schema.json",
        "STRUCTURAL_START_RECEIPT_SCHEMA_NON_AUTHORITY",
    ),
    (
        "tools/commercial-ux/native/realtime-controlled-codex-transcript.schema.json",
        "STRUCTURAL_FINAL_RECEIPT_SCHEMA_NON_AUTHORITY",
    ),
    (
        "tools/commercial-ux/native/realtime-current-route-artifact-authority.py",
        "BOUND_PARENT_CURRENT_ROUTE_ARTIFACT_SEMANTIC_VERIFIER_DEPENDENCY",
    ),
    (
        "tools/commercial-ux/native/test-realtime-controlled-codex-transcript-authority.py",
        "ADVERSARIAL_CONTROLLED_CODEX_TRANSCRIPT_TEST_SPEC_NON_RUNTIME",
    ),
)

EMPTY_RAW_SHA256 = "sha256:" + hashlib.sha256(b"").hexdigest()
EXPECTED_STDERR = b""
ROLLOUT_RELATIVE_PATTERN = re.compile(
    r"^[0-9]{4}/[0-9]{2}/[0-9]{2}/"
    r"rollout-[0-9TZ:-]+-[0-9a-f-]{36}\.jsonl$"
)
THREAD_ID_PATTERN = re.compile(r"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")


class ControlledCodexTranscriptAuthorityError(ValueError):
    """Raised when controlled execution or transcript authority is not exact."""


def _load_artifact_module() -> Any:
    spec = importlib.util.spec_from_file_location(
        "realtime_current_route_artifact_for_controlled_codex_transcript",
        ARTIFACT_MODULE_PATH,
    )
    if spec is None or spec.loader is None:
        raise ControlledCodexTranscriptAuthorityError(
            f"cannot load current-route artifact authority {ARTIFACT_MODULE_PATH}"
        )
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


ARTIFACT = _load_artifact_module()
CHAIN = ARTIFACT.CHAIN
SESSION = ARTIFACT.SESSION
CANDIDATE = ARTIFACT.CANDIDATE


def sha256_bytes(data: bytes) -> str:
    return ARTIFACT.sha256_bytes(data)


def canonical_bytes(value: Any) -> bytes:
    return CANDIDATE.canonical_bytes(value)


def canonical_sha256(value: Any) -> str:
    return ARTIFACT.canonical_sha256(value)


def strict_typed_equal(left: Any, right: Any) -> bool:
    return ARTIFACT.strict_typed_equal(left, right)


def json_file_bytes(value: dict[str, Any]) -> bytes:
    return ARTIFACT.json_file_bytes(value)


def self_hash(value: dict[str, Any], field: str) -> str:
    return ARTIFACT.self_hash(value, field)


def strict_json_bytes(data: bytes, label: str) -> dict[str, Any]:
    try:
        return ARTIFACT.strict_json_bytes(data, label)
    except ARTIFACT.CurrentRouteArtifactAuthorityError as error:
        raise ControlledCodexTranscriptAuthorityError(str(error)) from error


def _reject_symlink_components(path: Path, label: str) -> None:
    try:
        SESSION.reject_symlink_components(path, label)
    except SESSION.SessionAuthorityError as error:
        raise ControlledCodexTranscriptAuthorityError(str(error)) from error


def _canonical_directory(path: Path, label: str) -> Path:
    try:
        return ARTIFACT.canonical_existing_directory(path, label)
    except ARTIFACT.CurrentRouteArtifactAuthorityError as error:
        raise ControlledCodexTranscriptAuthorityError(str(error)) from error


@dataclasses.dataclass(frozen=True)
class FileObservation:
    path: Path
    device: int
    inode: int
    mode: int
    link_count: int
    byte_length: int
    modified_ns: int
    changed_ns: int
    raw_sha256: str

    def row(self) -> dict[str, Any]:
        return {
            "canonicalPath": str(self.path),
            "device": self.device,
            "inode": self.inode,
            "mode": self.mode,
            "linkCount": self.link_count,
            "byteLength": self.byte_length,
            "modifiedUnixNs": self.modified_ns,
            "changedUnixNs": self.changed_ns,
            "rawSha256": self.raw_sha256,
        }


def _stat_signature(value: os.stat_result) -> tuple[int, ...]:
    return (
        value.st_dev,
        value.st_ino,
        value.st_mode,
        value.st_nlink,
        value.st_size,
        value.st_mtime_ns,
        value.st_ctime_ns,
    )


def _read_regular_nlink_one(path: Path, label: str) -> tuple[FileObservation, bytes]:
    absolute = path if path.is_absolute() else path.absolute()
    _reject_symlink_components(absolute, label)
    try:
        before = os.lstat(absolute)
    except OSError as error:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} cannot be inspected: {error}"
        ) from error
    if not stat.S_ISREG(before.st_mode) or before.st_nlink != 1:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} must be one regular nlink-one file"
        )
    flags = os.O_RDONLY
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    try:
        descriptor = os.open(absolute, flags)
    except OSError as error:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} cannot be opened without following links: {error}"
        ) from error
    try:
        opened = os.fstat(descriptor)
        if (
            before.st_dev != opened.st_dev
            or before.st_ino != opened.st_ino
            or not stat.S_ISREG(opened.st_mode)
            or opened.st_nlink != 1
        ):
            raise ControlledCodexTranscriptAuthorityError(
                f"{label} identity changed while opening"
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
    if _stat_signature(opened) != _stat_signature(after):
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} changed while reading"
        )
    data = b"".join(chunks)
    if len(data) != after.st_size:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} byte length changed while reading"
        )
    try:
        resolved = absolute.resolve(strict=True)
    except OSError as error:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} cannot be resolved after reading: {error}"
        ) from error
    if resolved != absolute:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} path is not canonical"
        )
    return (
        FileObservation(
            resolved,
            after.st_dev,
            after.st_ino,
            after.st_mode,
            after.st_nlink,
            after.st_size,
            after.st_mtime_ns,
            after.st_ctime_ns,
            sha256_bytes(data),
        ),
        data,
    )


def _stream_regular_sha256(path: Path, label: str) -> tuple[FileObservation, bytes | None]:
    absolute = path if path.is_absolute() else path.absolute()
    _reject_symlink_components(absolute, label)
    before = os.lstat(absolute)
    if not stat.S_ISREG(before.st_mode) or before.st_nlink != 1:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} must be one regular nlink-one file"
        )
    flags = os.O_RDONLY
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    descriptor = os.open(absolute, flags)
    digest = hashlib.sha256()
    try:
        opened = os.fstat(descriptor)
        if before.st_dev != opened.st_dev or before.st_ino != opened.st_ino:
            raise ControlledCodexTranscriptAuthorityError(
                f"{label} identity changed while opening"
            )
        while True:
            chunk = os.read(descriptor, 1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
        after = os.fstat(descriptor)
    finally:
        os.close(descriptor)
    if _stat_signature(opened) != _stat_signature(after):
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} changed while hashing"
        )
    resolved = absolute.resolve(strict=True)
    if resolved != absolute:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} path is not canonical"
        )
    return (
        FileObservation(
            resolved,
            after.st_dev,
            after.st_ino,
            after.st_mode,
            after.st_nlink,
            after.st_size,
            after.st_mtime_ns,
            after.st_ctime_ns,
            "sha256:" + digest.hexdigest(),
        ),
        None,
    )


def _exclusive_directory(path: Path, label: str) -> Path:
    absolute = path if path.is_absolute() else path.absolute()
    parent = _canonical_directory(absolute.parent, f"{label} parent")
    if absolute.parent != parent:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} parent path is not canonical"
        )
    try:
        os.mkdir(absolute, 0o700)
    except OSError as error:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} cannot be exclusively created: {error}"
        ) from error
    result = _canonical_directory(absolute, label)
    _fsync_directory(parent)
    return result


def _require_outside_repository(
    path: Path,
    repository_root: Path,
    label: str,
) -> None:
    canonical_repository = CANDIDATE.resolve_repository_root(repository_root)
    candidate = path.resolve(strict=False)
    try:
        candidate.relative_to(canonical_repository)
    except ValueError:
        return
    raise ControlledCodexTranscriptAuthorityError(
        f"{label} must remain outside the source repository"
    )


def _fsync_directory(path: Path) -> None:
    descriptor = os.open(path, os.O_RDONLY)
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def _exclusive_write(path: Path, data: bytes, label: str) -> FileObservation:
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    descriptor = os.open(path, flags, 0o600)
    try:
        view = memoryview(data)
        while view:
            written = os.write(descriptor, view)
            if written <= 0:
                raise ControlledCodexTranscriptAuthorityError(
                    f"{label} made no write progress"
                )
            view = view[written:]
        os.fsync(descriptor)
    finally:
        os.close(descriptor)
    _fsync_directory(path.parent)
    observation, actual = _read_regular_nlink_one(path, label)
    if actual != data:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} changed after exclusive write"
        )
    return observation


def _reserve_empty_file(path: Path, label: str) -> tuple[int, FileObservation]:
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    descriptor = os.open(path, flags, 0o600)
    os.fsync(descriptor)
    current = os.fstat(descriptor)
    if not stat.S_ISREG(current.st_mode) or current.st_nlink != 1 or current.st_size != 0:
        os.close(descriptor)
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} reservation is not one empty regular file"
        )
    _fsync_directory(path.parent)
    return descriptor, FileObservation(
        path.resolve(strict=True),
        current.st_dev,
        current.st_ino,
        current.st_mode,
        current.st_nlink,
        0,
        current.st_mtime_ns,
        current.st_ctime_ns,
        EMPTY_RAW_SHA256,
    )


def _write_reserved_descriptor(
    descriptor: int,
    reserved: FileObservation,
    data: bytes,
    label: str,
) -> FileObservation:
    current = os.fstat(descriptor)
    if (
        current.st_dev != reserved.device
        or current.st_ino != reserved.inode
        or current.st_nlink != 1
        or current.st_size != 0
    ):
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} zero-length reservation changed before final write"
        )
    view = memoryview(data)
    while view:
        written = os.write(descriptor, view)
        if written <= 0:
            raise ControlledCodexTranscriptAuthorityError(
                f"{label} made no final write progress"
            )
        view = view[written:]
    os.fsync(descriptor)
    observed, actual = _read_regular_nlink_one(reserved.path, label)
    _assert_reserved_identity(reserved, observed, label)
    if actual != data:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} changed after reserved final write"
        )
    _fsync_directory(reserved.path.parent)
    return observed


def _assert_reserved_identity(
    reserved: FileObservation,
    observed: FileObservation,
    label: str,
) -> None:
    if (
        reserved.path != observed.path
        or reserved.device != observed.device
        or reserved.inode != observed.inode
        or reserved.mode != observed.mode
        or observed.link_count != 1
    ):
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} reservation identity changed"
        )


def _assert_observation_and_bytes_unchanged(
    first: FileObservation,
    expected_data: bytes,
    label: str,
) -> None:
    second, second_data = _read_regular_nlink_one(first.path, label)
    if second != first or second_data != expected_data:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} bytes or file identity changed"
        )


def _observation_from_receipt_row(
    row: Any,
    label: str,
    *,
    role: str | None = None,
) -> FileObservation:
    keys = {
        "canonicalPath",
        "device",
        "inode",
        "mode",
        "linkCount",
        "byteLength",
        "modifiedUnixNs",
        "changedUnixNs",
        "rawSha256",
    }
    expected_keys = keys | ({"role"} if role is not None else set())
    if not isinstance(row, dict) or set(row) != expected_keys:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} file observation shape drift"
        )
    if role is not None and row.get("role") != role:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} file observation role drift"
        )
    integers = (
        "device",
        "inode",
        "mode",
        "linkCount",
        "byteLength",
        "modifiedUnixNs",
        "changedUnixNs",
    )
    if (
        not isinstance(row.get("canonicalPath"), str)
        or not Path(row["canonicalPath"]).is_absolute()
        or any(type(row.get(key)) is not int for key in integers)
        or row["device"] < 0
        or row["inode"] <= 0
        or row["mode"] <= 0
        or row["linkCount"] != 1
        or row["byteLength"] < 0
        or row["modifiedUnixNs"] <= 0
        or row["changedUnixNs"] <= 0
        or not isinstance(row.get("rawSha256"), str)
        or re.fullmatch(r"sha256:[0-9a-f]{64}", row["rawSha256"]) is None
    ):
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} file observation value drift"
        )
    return FileObservation(
        Path(row["canonicalPath"]),
        row["device"],
        row["inode"],
        row["mode"],
        row["linkCount"],
        row["byteLength"],
        row["modifiedUnixNs"],
        row["changedUnixNs"],
        row["rawSha256"],
    )


def load_transcript_policy() -> tuple[dict[str, Any], bytes]:
    _observation, data = _read_regular_nlink_one(
        POLICY_PATH,
        "controlled Codex transcript policy",
    )
    if sha256_bytes(data) != EXPECTED_POLICY_RAW_SHA256:
        raise ControlledCodexTranscriptAuthorityError(
            "controlled Codex transcript policy raw-byte drift"
        )
    return strict_json_bytes(data, "controlled Codex transcript policy"), data


def validate_transcript_policy(policy: dict[str, Any], data: bytes) -> None:
    expected, expected_data = load_transcript_policy()
    if data != expected_data or not strict_typed_equal(policy, expected):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled Codex transcript policy object/byte drift"
        )
    parent = policy.get("parentCurrentRouteArtifactAuthority", {})
    producer = policy.get("evaluatorProducerAuthority", {})
    invocation = policy.get("invocation", {})
    cli = policy.get("codexCliAuthority", {})
    trust = policy.get("trustBoundary", {})
    if (
        policy.get("schemaVersion")
        != "gridworks.realtime-controlled-codex-transcript-policy.v1"
        or parent.get("sourceCommit")
        != "a270339a778e49ce0458c61cef383fc96283a596"
        or parent.get("producerFilesSha256")
        != "sha256:225696ad11902e33213693e75e9576368a091b1a16ba32a3c0a449e6179dea1d"
        or producer.get("paths") != [path for path, _role in PRODUCER_PATH_ROLES]
        or producer.get("expectedFileCount") != len(PRODUCER_PATH_ROLES)
        or invocation.get("model") != "gpt-5.6-sol"
        or invocation.get("reasoningEffort") != "ultra"
        or invocation.get("sandbox") != "read-only"
        or invocation.get("approvalPolicy") != "never"
        or invocation.get("requiredRolloutOriginator") != "codex_exec"
        or invocation.get("resumeForkOrParentMetadataAllowed") is not False
        or invocation.get("stderrPolicy") != "EMPTY_ON_SUCCESS"
        or invocation.get("stderrExactRawSha256") != sha256_bytes(EXPECTED_STDERR)
        or invocation.get("stderrExactByteLength") != len(EXPECTED_STDERR)
        or invocation.get("structuredOutputSchemaRawSha256")
        != "sha256:cba98936db38d9455fa58d2a96368254e297b3021351952a8b3e34f9124bd845"
        or cli.get("rawSha256")
        != "sha256:f4a74117b8142cda581c95ff753abf4508b5636d89682c1ed77e4a9249af8963"
        or cli.get("byteLength") != 220538240
        or cli.get("version") != "codex-cli 0.149.0"
        or trust.get("officialCommercialUX") is not False
        or trust.get("scoreBearing") is not False
        or trust.get("commercialUXProxy") is not None
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled Codex transcript policy invariant drift"
        )


def _producer_projection(producer: dict[str, Any]) -> dict[str, Any]:
    return {
        "schemaVersion": producer["schemaVersion"],
        "sourceCommit": producer["sourceCommit"],
        "fileCount": producer["fileCount"],
        "files": producer["files"],
        "filesSha256": producer["filesSha256"],
        "runningFilesMatchGitBlobs": True,
        "gitCommandAuthoritySha256": canonical_sha256(
            producer["gitCommandAuthority"]
        ),
        "parentArtifactSemanticVerifierDependencyBound": True,
        "semanticVerifierEntryPoint": (
            "verify_controlled_codex_transcript_against_reconstructed_authority"
        ),
        "structuralSchemasAuthority": "STRUCTURAL_ONLY_NOT_TRANSCRIPT_AUTHORITY",
    }


def bind_transcript_evaluator_authority(
    repository_root: Path,
    revision: str,
) -> dict[str, Any]:
    root = CANDIDATE.resolve_repository_root(repository_root)
    expected_dir = (root / "tools" / "commercial-ux" / "native").resolve(strict=True)
    if SCRIPT_DIR != expected_dir or Path(__file__).resolve(strict=True) != (
        expected_dir / "realtime-controlled-codex-transcript-authority.py"
    ).resolve(strict=True):
        raise ControlledCodexTranscriptAuthorityError(
            "running transcript authority is outside the candidate repository"
        )
    try:
        source_commit = CANDIDATE.resolve_source_commit(root, revision)
        entries = CANDIDATE.git_tree_entries(root, source_commit)
    except CANDIDATE.CandidateAuthorityError as error:
        raise ControlledCodexTranscriptAuthorityError(str(error)) from error
    by_path = {
        path: (mode, object_type, object_id)
        for mode, object_type, object_id, path in entries
    }
    rows: list[dict[str, Any]] = []
    for path, role in PRODUCER_PATH_ROLES:
        entry = by_path.get(path)
        if entry is None:
            raise ControlledCodexTranscriptAuthorityError(
                f"transcript authority source commit lacks evaluator file: {path}"
            )
        mode, object_type, object_id = entry
        if mode not in {"100644", "100755"} or object_type != "blob":
            raise ControlledCodexTranscriptAuthorityError(
                f"transcript evaluator is not a regular Git blob: {path}"
            )
        try:
            git_data = CANDIDATE.run_git_command(
                root,
                ["cat-file", "blob", "--", object_id],
                label=f"transcript evaluator Git blob {path}",
            )
        except CANDIDATE.CandidateAuthorityError as error:
            raise ControlledCodexTranscriptAuthorityError(str(error)) from error
        _observation, running_data = _read_regular_nlink_one(
            root / path,
            f"running transcript evaluator {path}",
        )
        if running_data != git_data:
            raise ControlledCodexTranscriptAuthorityError(
                f"running transcript evaluator differs from source commit: {path}"
            )
        rows.append(CANDIDATE.GitBlob(path, mode, object_id, role, git_data).row())
    rows.sort(key=lambda row: row["path"])
    if [row["path"] for row in rows] != [path for path, _role in PRODUCER_PATH_ROLES]:
        raise ControlledCodexTranscriptAuthorityError(
            "transcript evaluator path order drift"
        )
    producer = {
        "schemaVersion": PRODUCER_SCHEMA,
        "sourceCommit": source_commit,
        "fileCount": len(rows),
        "files": rows,
        "filesSha256": canonical_sha256(rows),
        "runningFilesMatchGitBlobs": True,
        "gitCommandAuthority": CANDIDATE.bind_git_command_authority(root),
    }
    return _producer_projection(producer)


def _assert_producer_unchanged(
    repository_root: Path,
    first: dict[str, Any],
    label: str,
) -> None:
    second = bind_transcript_evaluator_authority(
        repository_root,
        first["sourceCommit"],
    )
    if not strict_typed_equal(first, second):
        raise ControlledCodexTranscriptAuthorityError(
            f"transcript evaluator authority changed during {label}"
        )


@dataclasses.dataclass(frozen=True)
class ParentArtifactContext:
    parent: Any
    artifact_producer: dict[str, Any]
    aggregate_path: Path
    aggregate: dict[str, Any]
    aggregate_bytes: bytes
    artifacts: tuple[tuple[Path, bytes, dict[str, Any]], ...]
    chain_claim_path: Path
    session_claim_path: Path


def _locate_session_claim_from_aggregate(aggregate_path: Path) -> tuple[Path, Path]:
    aggregate_observation, _aggregate_bytes = _read_regular_nlink_one(
        aggregate_path,
        "submitted blocked aggregate",
    )
    if (
        aggregate_observation.path.name != "aggregate.json"
        or aggregate_observation.path.parent.name != "artifacts"
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "submitted aggregate is outside the exact finalized artifact path"
        )
    chain_claim_path = aggregate_observation.path.parent.parent / "evaluation-chain-claim.json"
    try:
        _resolved, claim, _data = ARTIFACT._read_parent_chain_claim(chain_claim_path)
    except ARTIFACT.CurrentRouteArtifactAuthorityError as error:
        raise ControlledCodexTranscriptAuthorityError(str(error)) from error
    session_value = claim.get("parentSessionAuthority", {}).get(
        "canonicalSessionClaimPath"
    )
    if not isinstance(session_value, str):
        raise ControlledCodexTranscriptAuthorityError(
            "parent chain lacks canonical session claim path"
        )
    return chain_claim_path, Path(session_value)


def _reconstruct_parent_artifacts_without_lock(
    repository_root: Path,
    aggregate_path: Path,
) -> ParentArtifactContext:
    resolved_aggregate, submitted_bytes = _read_regular_nlink_one(
        aggregate_path,
        "submitted blocked aggregate",
    )
    chain_claim_path = resolved_aggregate.path.parent.parent / "evaluation-chain-claim.json"
    try:
        _resolved_claim, claim, _claim_bytes = ARTIFACT._read_parent_chain_claim(
            chain_claim_path
        )
    except ARTIFACT.CurrentRouteArtifactAuthorityError as error:
        raise ControlledCodexTranscriptAuthorityError(str(error)) from error
    policy, policy_bytes = load_transcript_policy()
    validate_transcript_policy(policy, policy_bytes)
    submitted_object = strict_json_bytes(
        submitted_bytes,
        "submitted blocked aggregate",
    )
    expected_parent = policy["parentCurrentRouteArtifactAuthority"]
    source_commit = submitted_object.get("artifactAuthoritySourceCommit")
    producer_hash = submitted_object.get("artifactProducerFilesSha256")
    if (
        source_commit != expected_parent["sourceCommit"]
        or producer_hash != expected_parent["producerFilesSha256"]
        or submitted_object.get("status") != expected_parent["requiredAggregateStatus"]
        or submitted_object.get("officialCommercialUX") is not False
        or submitted_object.get("scoreBearingCaptureAllowed") is not False
        or submitted_object.get("commercialUXProxy") is not None
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "submitted aggregate is not the reviewed blocked parent authority"
        )
    try:
        artifact_producer = ARTIFACT.bind_artifact_evaluator_authority(
            repository_root,
            source_commit,
        )
        parent = ARTIFACT._reconstruct_parent_chain_without_lock(
            repository_root,
            chain_claim_path,
            finalized_artifacts=True,
        )
        verified_aggregate, verified_bytes = ARTIFACT._verify_artifacts_without_lock(
            parent,
            artifact_producer,
        )
    except ARTIFACT.CurrentRouteArtifactAuthorityError as error:
        raise ControlledCodexTranscriptAuthorityError(str(error)) from error
    if (
        verified_bytes != submitted_bytes
        or not strict_typed_equal(verified_aggregate, submitted_object)
        or Path(parent.claim["fixedFutureArtifactPaths"][6]) != resolved_aggregate.path
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "submitted aggregate differs from reconstructed blocked parent"
        )
    artifacts: list[tuple[Path, bytes, dict[str, Any]]] = []
    for index, path_value in enumerate(parent.claim["fixedFutureArtifactPaths"]):
        try:
            data, value = ARTIFACT._read_canonical_artifact(
                Path(path_value),
                f"bound parent artifact {index + 1}",
            )
        except ARTIFACT.CurrentRouteArtifactAuthorityError as error:
            raise ControlledCodexTranscriptAuthorityError(str(error)) from error
        artifacts.append((Path(path_value), data, value))
    judge_path, judge_bytes, judge = artifacts[2]
    if (
        judge.get("artifactKind") != "JUDGE_INPUT"
        or judge.get("status") != expected_parent["requiredJudgeInputStatus"]
        or judge.get("payload", {}).get("executableJudgeInput") is not False
        or judge.get("payload", {}).get("futureModelRequirement")
        != {
            "model": "gpt-5.6-sol",
            "reasoningEffort": "ultra",
            "requirementOnlyNotExecutionClaim": True,
        }
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "bound judge input is not the exact blocked non-executable artifact"
        )
    final_parent = ARTIFACT._reconstruct_parent_chain_without_lock(
        repository_root,
        chain_claim_path,
        finalized_artifacts=True,
    )
    try:
        ARTIFACT._assert_parent_context_unchanged(
            parent,
            final_parent,
            "controlled transcript parent reconstruction",
        )
        ARTIFACT._assert_producer_unchanged(
            repository_root,
            artifact_producer,
            "controlled transcript parent reconstruction",
        )
    except ARTIFACT.CurrentRouteArtifactAuthorityError as error:
        raise ControlledCodexTranscriptAuthorityError(str(error)) from error
    return ParentArtifactContext(
        parent,
        artifact_producer,
        resolved_aggregate.path,
        verified_aggregate,
        verified_bytes,
        tuple(artifacts),
        chain_claim_path,
        Path(parent.claim["parentSessionAuthority"]["canonicalSessionClaimPath"]),
    )


def _assert_parent_unchanged(
    repository_root: Path,
    first: ParentArtifactContext,
    label: str,
) -> ParentArtifactContext:
    second = _reconstruct_parent_artifacts_without_lock(
        repository_root,
        first.aggregate_path,
    )
    first_artifacts = tuple((str(path), data, value) for path, data, value in first.artifacts)
    second_artifacts = tuple((str(path), data, value) for path, data, value in second.artifacts)
    if (
        first.aggregate_bytes != second.aggregate_bytes
        or not strict_typed_equal(first.aggregate, second.aggregate)
        or first_artifacts != second_artifacts
        or first.chain_claim_path != second.chain_claim_path
        or first.session_claim_path != second.session_claim_path
    ):
        raise ControlledCodexTranscriptAuthorityError(
            f"blocked parent artifact chain changed during {label}"
        )
    return second


def _parent_projection(context: ParentArtifactContext) -> dict[str, Any]:
    judge_path, judge_bytes, judge = context.artifacts[2]
    claim = context.parent.claim
    return {
        "evaluationChainRoot": claim["canonicalChainRoot"],
        "evaluationChainId": claim["chainId"],
        "evaluationChainClaimPath": claim["canonicalClaimPath"],
        "evaluationChainClaimRawSha256": sha256_bytes(context.parent.claim_bytes),
        "evaluationChainClaimSha256": claim["evaluationChainClaimSha256"],
        "routeBoundary": claim["routeBoundary"],
        "sessionClaimPath": claim["parentSessionAuthority"]["canonicalSessionClaimPath"],
        "sessionClaimRawSha256": claim["parentSessionAuthority"]["sessionClaimRawSha256"],
        "artifactChainId": context.aggregate["artifactChainId"],
        "aggregatePath": str(context.aggregate_path),
        "aggregateRawSha256": sha256_bytes(context.aggregate_bytes),
        "aggregateArtifactSha256": context.aggregate["artifactSha256"],
        "aggregateStatus": context.aggregate["status"],
        "artifactAuthoritySourceCommit": context.aggregate[
            "artifactAuthoritySourceCommit"
        ],
        "artifactProducerFilesSha256": context.aggregate[
            "artifactProducerFilesSha256"
        ],
        "blockedJudgeInputPath": str(judge_path),
        "blockedJudgeInputRawSha256": sha256_bytes(judge_bytes),
        "blockedJudgeInputArtifactSha256": judge["artifactSha256"],
        "blockedJudgeInputStatus": judge["status"],
        "blockedJudgeInputExecutable": False,
        "blockedJudgeInputFutureModelRequirement": judge["payload"][
            "futureModelRequirement"
        ],
        "sevenArtifactChainRemainsFinalizedAndUnmodified": True,
    }


def _fixed_process_environment() -> tuple[dict[str, str], Path, Path]:
    policy, policy_bytes = load_transcript_policy()
    validate_transcript_policy(policy, policy_bytes)
    ambient_blockers = sorted(
        set(os.environ) & set(policy["invocation"]["rejectedAmbientEnvironmentNames"])
    )
    if ambient_blockers:
        raise ControlledCodexTranscriptAuthorityError(
            "ambient Codex/provider/key/base-URL/proxy variables are forbidden: "
            + ", ".join(ambient_blockers)
        )
    account = pwd.getpwuid(os.getuid())
    home = _canonical_directory(Path(account.pw_dir), "password-database home")
    codex_home = _canonical_directory(home / ".codex", "canonical Codex home")
    sessions_root = _canonical_directory(codex_home / "sessions", "Codex rollout root")
    values = {
        "CODEX_HOME": str(codex_home),
        "HOME": str(home),
        "LANG": "en_US.UTF-8",
        "LC_ALL": "en_US.UTF-8",
        "LOGNAME": account.pw_name,
        "NO_COLOR": "1",
        "PATH": "/usr/bin:/bin",
        "SHELL": account.pw_shell,
        "TMPDIR": "/private/tmp",
        "USER": account.pw_name,
    }
    if sorted(values) != sorted(policy["invocation"]["exactEnvironmentNames"]):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled Codex environment allowlist drift"
        )
    return dict(sorted(values.items())), codex_home, sessions_root


def _acquire_sessions_directory_flock(path: Path) -> int:
    root = _canonical_directory(path, "Codex sessions directory lock root")
    _reject_symlink_components(root, "Codex sessions directory lock root")
    before = os.lstat(root)
    descriptor = os.open(root, os.O_RDONLY)
    try:
        opened = os.fstat(descriptor)
        if (
            before.st_dev != opened.st_dev
            or before.st_ino != opened.st_ino
            or not stat.S_ISDIR(opened.st_mode)
        ):
            raise ControlledCodexTranscriptAuthorityError(
                "Codex sessions directory changed while locking"
            )
        fcntl.flock(descriptor, fcntl.LOCK_EX)
        return descriptor
    except BaseException:
        os.close(descriptor)
        raise


def _release_sessions_directory_flock(descriptor: int) -> None:
    try:
        fcntl.flock(descriptor, fcntl.LOCK_UN)
    finally:
        os.close(descriptor)


def _run_fixed_command(arguments: Sequence[str], label: str) -> tuple[int, bytes, bytes]:
    environment = {
        "PATH": "/usr/bin:/bin",
        "LANG": "C",
        "LC_ALL": "C",
    }
    completed = subprocess.run(
        list(arguments),
        cwd="/",
        env=environment,
        stdin=subprocess.DEVNULL,
        capture_output=True,
        check=False,
    )
    return completed.returncode, completed.stdout, completed.stderr


def _parse_codesign_inspection(data: bytes) -> tuple[str, str, str]:
    try:
        text = data.decode("utf-8")
    except UnicodeDecodeError as error:
        raise ControlledCodexTranscriptAuthorityError(
            "Codex codesign inspection output is not UTF-8"
        ) from error
    identifiers = re.findall(r"^Identifier=(.+)$", text, flags=re.MULTILINE)
    teams = re.findall(r"^TeamIdentifier=(.+)$", text, flags=re.MULTILINE)
    cdhashes = re.findall(r"^CDHash=(.+)$", text, flags=re.MULTILINE)
    if len(identifiers) != 1 or len(teams) != 1 or len(cdhashes) != 1:
        raise ControlledCodexTranscriptAuthorityError(
            "Codex codesign inspection lacks one identifier/team/CDHash"
        )
    return identifiers[0], teams[0], cdhashes[0]


def bind_codex_cli_authority() -> dict[str, Any]:
    policy, policy_bytes = load_transcript_policy()
    validate_transcript_policy(policy, policy_bytes)
    expected = policy["codexCliAuthority"]
    executable = Path(expected["canonicalNativeExecutablePath"])
    observation, _unused = _stream_regular_sha256(executable, "pinned Codex native CLI")
    if (
        observation.raw_sha256 != expected["rawSha256"]
        or observation.byte_length != expected["byteLength"]
        or observation.link_count != expected["requiredLinkCount"]
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "pinned Codex native CLI bytes/stat drift"
        )
    version_code, version_stdout, version_stderr = _run_fixed_command(
        [str(executable), "--version"],
        "Codex version",
    )
    try:
        version = version_stdout.decode("utf-8").rstrip("\n")
    except UnicodeDecodeError as error:
        raise ControlledCodexTranscriptAuthorityError(
            "Codex version output is not UTF-8"
        ) from error
    if version_code != 0 or version_stderr != b"" or version != expected["version"]:
        raise ControlledCodexTranscriptAuthorityError(
            "pinned Codex native CLI version drift"
        )
    verify_code, verify_stdout, verify_stderr = _run_fixed_command(
        ["/usr/bin/codesign", "--verify", "--strict", "--verbose=2", str(executable)],
        "Codex codesign verification",
    )
    if verify_code != 0 or verify_stdout != b"":
        raise ControlledCodexTranscriptAuthorityError(
            "pinned Codex native CLI codesign verification failed"
        )
    inspect_code, inspect_stdout, inspect_stderr = _run_fixed_command(
        ["/usr/bin/codesign", "-dv", "--verbose=4", str(executable)],
        "Codex codesign inspection",
    )
    if inspect_code != 0 or inspect_stdout != b"":
        raise ControlledCodexTranscriptAuthorityError(
            "pinned Codex native CLI codesign inspection failed"
        )
    identifier, team, cdhash = _parse_codesign_inspection(inspect_stderr)
    requirement_code, requirement_stdout, requirement_stderr = _run_fixed_command(
        ["/usr/bin/codesign", "-dr", "-", str(executable)],
        "Codex codesign designated requirement",
    )
    try:
        requirement_lines = requirement_stdout.decode("utf-8").splitlines()
    except UnicodeDecodeError as error:
        raise ControlledCodexTranscriptAuthorityError(
            "Codex designated requirement output is not UTF-8"
        ) from error
    designated = _one(
        [line.removeprefix("designated => ") for line in requirement_lines if line.startswith("designated => ")],
        "Codex designated requirement",
    )
    if (
        identifier != expected["codesignIdentifier"]
        or team != expected["codesignTeamIdentifier"]
        or cdhash != expected["codesignCDHash"]
        or requirement_code != 0
        or requirement_stdout != f"designated => {designated}\n".encode("utf-8")
        or requirement_stderr != f"Executable={executable}\n".encode("utf-8")
        or designated != expected["codesignDesignatedRequirement"]
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "pinned Codex native CLI signing identity drift"
        )
    return {
        "canonicalNativeExecutablePath": str(observation.path),
        "version": version,
        "rawSha256": observation.raw_sha256,
        "byteLength": observation.byte_length,
        "device": observation.device,
        "inode": observation.inode,
        "mode": observation.mode,
        "linkCount": observation.link_count,
        "modifiedUnixNs": observation.modified_ns,
        "changedUnixNs": observation.changed_ns,
        "codesignIdentifier": identifier,
        "codesignTeamIdentifier": team,
        "codesignCDHash": cdhash,
        "codesignDesignatedRequirement": designated,
        "codesignVerifyStdoutRawSha256": sha256_bytes(verify_stdout),
        "codesignVerifyStderrRawSha256": sha256_bytes(verify_stderr),
        "codesignInspectRawSha256": sha256_bytes(inspect_stderr),
        "codesignRequirementRawSha256": sha256_bytes(requirement_stderr),
        "versionStdoutRawSha256": sha256_bytes(version_stdout),
        "versionStderrRawSha256": sha256_bytes(version_stderr),
        "bindingScope": (
            "EXACT_NATIVE_MACH_O_BYTES_STAT_VERSION_AND_APPLE_CODESIGN_IDENTITY_"
            "TRANSITIVE_CLOSURE_UNBOUND"
        ),
    }


def _assert_cli_unchanged(first: dict[str, Any], label: str) -> None:
    second = bind_codex_cli_authority()
    if not strict_typed_equal(first, second):
        raise ControlledCodexTranscriptAuthorityError(
            f"pinned Codex native CLI changed during {label}"
        )


@dataclasses.dataclass(frozen=True)
class PrivateRolloutEntry:
    relative_path: str
    path: Path
    device: int
    inode: int
    link_count: int
    byte_length: int
    modified_ns: int
    changed_ns: int
    digest: str


def _salted_path_digest(nonce: str, relative_path: str) -> str:
    try:
        nonce_bytes = bytes.fromhex(nonce)
    except ValueError as error:
        raise ControlledCodexTranscriptAuthorityError(
            "rollout inventory nonce is not lower hexadecimal"
        ) from error
    return sha256_bytes(nonce_bytes + b"\x00" + relative_path.encode("utf-8"))


def _project_inventory_entry(
    nonce: str,
    relative_path: str,
    device: int,
    inode: int,
    link_count: int,
) -> dict[str, Any]:
    row = {
        "pathDigest": _salted_path_digest(nonce, relative_path),
        "device": device,
        "inode": inode,
        "linkCount": link_count,
        "entrySha256": "",
    }
    row["entrySha256"] = self_hash(row, "entrySha256")
    return row


def _scan_rollout_inventory(
    sessions_root: Path,
    nonce: str,
    phase: str,
) -> tuple[dict[str, PrivateRolloutEntry], dict[str, Any]]:
    root = _canonical_directory(sessions_root, "Codex rollout inventory root")
    private: dict[str, PrivateRolloutEntry] = {}
    for directory, directory_names, file_names in os.walk(root, followlinks=False):
        base = Path(directory)
        for name in list(directory_names):
            child = base / name
            info = os.lstat(child)
            if not stat.S_ISDIR(info.st_mode):
                raise ControlledCodexTranscriptAuthorityError(
                    "Codex rollout inventory contains a non-directory or symlink component"
                )
            resolved = child.resolve(strict=True)
            if resolved != child:
                raise ControlledCodexTranscriptAuthorityError(
                    "Codex rollout inventory directory is not canonical"
                )
        for name in file_names:
            child = base / name
            _reject_symlink_components(child, "Codex rollout inventory entry")
            info = os.lstat(child)
            if not stat.S_ISREG(info.st_mode) or info.st_nlink != 1:
                raise ControlledCodexTranscriptAuthorityError(
                    "Codex rollout inventory entry is not one regular nlink-one file"
                )
            relative = child.relative_to(root).as_posix()
            if ROLLOUT_RELATIVE_PATTERN.fullmatch(relative) is None:
                raise ControlledCodexTranscriptAuthorityError(
                    "Codex rollout inventory contains an unexpected file path"
                )
            projected = _project_inventory_entry(
                nonce,
                relative,
                info.st_dev,
                info.st_ino,
                info.st_nlink,
            )
            digest = projected["entrySha256"]
            if relative in private:
                raise ControlledCodexTranscriptAuthorityError(
                    "Codex rollout inventory repeats a relative path"
                )
            private[relative] = PrivateRolloutEntry(
                relative,
                child.resolve(strict=True),
                info.st_dev,
                info.st_ino,
                info.st_nlink,
                info.st_size,
                info.st_mtime_ns,
                info.st_ctime_ns,
                digest,
            )
    entries = sorted(
        [
            _project_inventory_entry(
                nonce,
                entry.relative_path,
                entry.device,
                entry.inode,
                entry.link_count,
            )
            for entry in private.values()
        ],
        key=lambda row: row["pathDigest"],
    )
    inventory = {
        "schemaVersion": INVENTORY_SCHEMA,
        "phase": phase,
        "canonicalSessionsRoot": str(root),
        "nonce": nonce,
        "fileCount": len(entries),
        "entries": entries,
        "entryTreeSha256": canonical_sha256(entries),
        "historicalRawPathsThreadIdsSizesAndTimesPersisted": False,
        "inventorySha256": "",
    }
    inventory["inventorySha256"] = self_hash(inventory, "inventorySha256")
    return private, inventory


def _validate_rollout_delta(
    before_private: dict[str, PrivateRolloutEntry],
    before: dict[str, Any],
    after_private: dict[str, PrivateRolloutEntry],
    after: dict[str, Any],
) -> PrivateRolloutEntry:
    removed_paths = sorted(set(before_private) - set(after_private))
    added_paths = sorted(set(after_private) - set(before_private))
    if removed_paths or len(added_paths) != 1:
        raise ControlledCodexTranscriptAuthorityError(
            "Codex rollout delta must contain exactly one new file and no removal"
        )
    for path, earlier in before_private.items():
        later = after_private[path]
        if (
            earlier.device != later.device
            or earlier.inode != later.inode
            or earlier.link_count != later.link_count
            or earlier.digest != later.digest
            or later.byte_length < earlier.byte_length
        ):
            raise ControlledCodexTranscriptAuthorityError(
                "pre-existing Codex rollout changed identity or shrank"
            )
    before_digests = {row["entrySha256"] for row in before["entries"]}
    after_digests = {row["entrySha256"] for row in after["entries"]}
    if (
        before_digests - after_digests
        or len(after_digests - before_digests) != 1
        or after["fileCount"] != before["fileCount"] + 1
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "persisted Codex rollout digest delta is not exactly one"
        )
    added = after_private[added_paths[0]]
    if after_digests - before_digests != {added.digest}:
        raise ControlledCodexTranscriptAuthorityError(
            "new Codex rollout digest/path relation drift"
        )
    return added


def _assert_rollout_inventory_still_exact(
    sessions_root: Path,
    nonce: str,
    expected: dict[str, Any],
    label: str,
) -> None:
    _private, observed = _scan_rollout_inventory(
        sessions_root,
        nonce,
        "AFTER",
    )
    if not strict_typed_equal(observed, expected):
        raise ControlledCodexTranscriptAuthorityError(
            f"Codex rollout inventory changed during {label}"
        )


def _compose_prompt(parent: dict[str, Any], nonce: str) -> bytes:
    lines = [
        "Return exactly one JSON object matching the supplied output schema.",
        "Do not call tools, inspect files, browse, or modify anything.",
        "This is a local non-score transcript-authority echo probe, not a judgment.",
        f"schemaVersion={PROBE_OUTPUT_SCHEMA}",
        "probe=GRIDWORKS_CONTROLLED_CODEX_TRANSCRIPT_NON_SCORE_V1",
        f"nonce={nonce}",
        f"parentEvaluationChainClaimRawSha256={parent['evaluationChainClaimRawSha256']}",
        f"parentAggregateRawSha256={parent['aggregateRawSha256']}",
        f"blockedJudgeInputRawSha256={parent['blockedJudgeInputRawSha256']}",
        "modelEcho=gpt-5.6-sol",
        "reasoningEffortEcho=ultra",
        "acknowledged=true",
        "officialCommercialUX=false",
        "commercialUXProxy=null",
    ]
    return "\n".join(lines).encode("utf-8")


def _expected_probe_output(parent: dict[str, Any], nonce: str) -> dict[str, Any]:
    return {
        "schemaVersion": PROBE_OUTPUT_SCHEMA,
        "probe": "GRIDWORKS_CONTROLLED_CODEX_TRANSCRIPT_NON_SCORE_V1",
        "nonce": nonce,
        "parentEvaluationChainClaimRawSha256": parent[
            "evaluationChainClaimRawSha256"
        ],
        "parentAggregateRawSha256": parent["aggregateRawSha256"],
        "blockedJudgeInputRawSha256": parent["blockedJudgeInputRawSha256"],
        "modelEcho": "gpt-5.6-sol",
        "reasoningEffortEcho": "ultra",
        "acknowledged": True,
        "officialCommercialUX": False,
        "commercialUXProxy": None,
    }


def _environment_rows(environment: Mapping[str, str]) -> list[dict[str, str]]:
    return [{"name": name, "value": environment[name]} for name in sorted(environment)]


def _build_argv(
    cli: dict[str, Any],
    capsule: Path,
    output_path: Path,
) -> list[str]:
    return [
        cli["canonicalNativeExecutablePath"],
        "exec",
        "--strict-config",
        "--ignore-user-config",
        "--ignore-rules",
        "--model",
        "gpt-5.6-sol",
        "--sandbox",
        "read-only",
        "--config",
        'approval_policy="never"',
        "--config",
        'model_reasoning_effort="ultra"',
        "--cd",
        str(capsule),
        "--skip-git-repo-check",
        "--output-schema",
        str(OUTPUT_SCHEMA_PATH),
        "--color",
        "never",
        "--json",
        "--output-last-message",
        str(output_path),
        "-",
    ]


def _transcript_authority_id(
    parent: dict[str, Any],
    producer: dict[str, Any],
    policy_bytes: bytes,
    nonce: str,
) -> str:
    return canonical_sha256({
        "schemaVersion": FINAL_SCHEMA,
        "parentEvaluationChainClaimRawSha256": parent[
            "evaluationChainClaimRawSha256"
        ],
        "parentAggregateRawSha256": parent["aggregateRawSha256"],
        "blockedJudgeInputRawSha256": parent["blockedJudgeInputRawSha256"],
        "transcriptPolicyRawSha256": sha256_bytes(policy_bytes),
        "transcriptProducerFilesSha256": producer["filesSha256"],
        "nonce": nonce,
    })


def _compose_start_receipt(
    *,
    root: Path,
    capsule: Path,
    parent: dict[str, Any],
    producer: dict[str, Any],
    policy_bytes: bytes,
    nonce: str,
    prompt_observation: FileObservation,
    prompt: bytes,
    output_schema_observation: FileObservation,
    before_inventory: dict[str, Any],
    before_inventory_observation: FileObservation,
    reservations: Mapping[str, FileObservation],
    final_receipt_reservation: FileObservation,
    cli: dict[str, Any],
    argv: list[str],
    environment: Mapping[str, str],
    freshness_window_start_ns: int,
) -> dict[str, Any]:
    receipt = {
        "schemaVersion": START_SCHEMA,
        "canonicalization": CANONICALIZATION,
        "status": "FINALIZED_BEFORE_FRESH_CODEX_EXECUTION",
        "canonicalTranscriptRoot": str(root),
        "canonicalStartReceiptPath": str(root / "probe-start-receipt.json"),
        "transcriptAuthorityId": _transcript_authority_id(
            parent, producer, policy_bytes, nonce
        ),
        "transcriptAuthoritySourceCommit": producer["sourceCommit"],
        "transcriptEvaluatorAuthority": producer,
        "transcriptPolicyRawSha256": sha256_bytes(policy_bytes),
        "parentAuthority": parent,
        "nonce": nonce,
        "freshnessWindowStartUnixNs": freshness_window_start_ns,
        "prompt": {
            "canonicalPath": str(prompt_observation.path),
            "rawSha256": sha256_bytes(prompt),
            "byteLength": len(prompt),
            "transport": "STDIN_DASH_EXACT_BYTES",
            "nonceChainParentAndBlockedJudgeHashesEchoRequired": True,
        },
        "structuredOutputSchema": {
            "canonicalPath": str(output_schema_observation.path),
            "rawSha256": output_schema_observation.raw_sha256,
            "byteLength": output_schema_observation.byte_length,
            "authority": "SOURCE_BOUND_STRUCTURAL_OUTPUT_CONTRACT_SEMANTIC_ECHO_RECONSTRUCTED",
        },
        "rolloutInventoryBefore": before_inventory,
        "rolloutInventoryBeforeRawSha256": before_inventory_observation.raw_sha256,
        "rolloutInventoryBeforePath": str(before_inventory_observation.path),
        "executionCapsule": {
            "canonicalPath": str(capsule),
            "inventoryBeforeExecution": [],
            "gitRepositoryCheckSkipped": True,
            "repositoryAgentsRulesOrCodeLoadedFromCwd": False,
        },
        "reservedProcessFiles": [
            {"role": role, **reservations[role].row()}
            for role in ("STDOUT", "STDERR", "OUTPUT")
        ],
        "reservedFinalReceipt": final_receipt_reservation.row(),
        "codexCliAuthority": cli,
        "argv": argv,
        "environment": _environment_rows(environment),
        "cwd": str(capsule),
        "requiredModel": "gpt-5.6-sol",
        "requiredReasoningEffort": "ultra",
        "requiredSandbox": "read-only",
        "requiredApprovalPolicy": "never",
        "subprocessStartedAtReceiptFinalization": False,
        "officialCommercialUX": False,
        "scoreBearing": False,
        "commercialUXProxy": None,
        "platformAttestationStatus": "UNAVAILABLE_NOT_A_PLATFORM_SIGNED_EXECUTION_RECEIPT",
        "probeStartReceiptSha256": "",
    }
    receipt["probeStartReceiptSha256"] = self_hash(
        receipt,
        "probeStartReceiptSha256",
    )
    return receipt


def _strict_json_lines(data: bytes, label: str) -> list[dict[str, Any]]:
    try:
        text = data.decode("utf-8")
    except UnicodeDecodeError as error:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} is not UTF-8"
        ) from error
    if not text.endswith("\n"):
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} must end in one JSONL newline"
        )
    lines = text.splitlines()
    if not lines or any(line == "" for line in lines):
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} contains an empty or missing JSONL record"
        )
    return [strict_json_bytes(line.encode("utf-8"), f"{label} record") for line in lines]


def _one(values: list[Any], label: str) -> Any:
    if len(values) != 1:
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} must occur exactly once"
        )
    return values[0]


def _usage_projection(value: Any, label: str) -> dict[str, int]:
    if not isinstance(value, dict):
        raise ControlledCodexTranscriptAuthorityError(f"{label} must be an object")
    expected = {
        "input_tokens",
        "cached_input_tokens",
        "cache_write_input_tokens",
        "output_tokens",
        "reasoning_output_tokens",
    }
    if set(value) != expected or any(type(value[key]) is not int or value[key] < 0 for key in expected):
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} field/type drift"
        )
    return {key: value[key] for key in sorted(expected)}


def _extract_message_text(message: dict[str, Any], role: str, content_type: str) -> str:
    if message.get("type") != "message" or message.get("role") != role:
        raise ControlledCodexTranscriptAuthorityError(
            f"rollout {role} message identity drift"
        )
    content = message.get("content")
    if not isinstance(content, list) or len(content) != 1:
        raise ControlledCodexTranscriptAuthorityError(
            f"rollout {role} message content shape drift"
        )
    row = content[0]
    if not isinstance(row, dict) or row.get("type") != content_type or set(row) != {"type", "text"}:
        raise ControlledCodexTranscriptAuthorityError(
            f"rollout {role} message content type drift"
        )
    if not isinstance(row.get("text"), str):
        raise ControlledCodexTranscriptAuthorityError(
            f"rollout {role} message text is missing"
        )
    return row["text"]


def _verify_execution_transcript(
    *,
    start: dict[str, Any],
    stdout_bytes: bytes,
    stderr_bytes: bytes,
    output_bytes: bytes,
    rollout_observation: FileObservation,
    rollout_bytes: bytes,
    prompt_bytes: bytes,
) -> dict[str, Any]:
    if stderr_bytes != EXPECTED_STDERR:
        raise ControlledCodexTranscriptAuthorityError(
            "controlled Codex stderr must be empty on success"
        )
    output = strict_json_bytes(output_bytes, "controlled Codex final output")
    expected_output = _expected_probe_output(start["parentAuthority"], start["nonce"])
    if not strict_typed_equal(output, expected_output):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled Codex final output echo mismatch"
        )
    stdout = _strict_json_lines(stdout_bytes, "controlled Codex stdout")
    if [row.get("type") for row in stdout] != [
        "thread.started",
        "turn.started",
        "item.completed",
        "turn.completed",
    ]:
        raise ControlledCodexTranscriptAuthorityError(
            "controlled Codex stdout event order/tool-free shape drift"
        )
    thread_id = stdout[0].get("thread_id")
    if not isinstance(thread_id, str) or THREAD_ID_PATTERN.fullmatch(thread_id) is None:
        raise ControlledCodexTranscriptAuthorityError(
            "controlled Codex stdout thread id drift"
        )
    item = stdout[2].get("item")
    if not isinstance(item, dict) or set(item) != {"id", "type", "text"} or item.get("type") != "agent_message":
        raise ControlledCodexTranscriptAuthorityError(
            "controlled Codex stdout final item drift"
        )
    try:
        output_text = output_bytes.decode("utf-8")
    except UnicodeDecodeError as error:
        raise ControlledCodexTranscriptAuthorityError(
            "controlled Codex output is not UTF-8"
        ) from error
    if item.get("text") != output_text:
        raise ControlledCodexTranscriptAuthorityError(
            "controlled Codex stdout/output echo mismatch"
        )
    stdout_usage = _usage_projection(stdout[3].get("usage"), "stdout turn usage")
    rollout = _strict_json_lines(rollout_bytes, "fresh Codex rollout")
    allowed_top_level_types = {
        "session_meta",
        "event_msg",
        "response_item",
        "turn_context",
        "world_state",
    }
    allowed_event_types = {
        "task_started",
        "item_completed",
        "token_count",
        "task_complete",
    }
    for record in rollout:
        record_type = record.get("type")
        payload = record.get("payload")
        if record_type not in allowed_top_level_types or not isinstance(payload, dict):
            raise ControlledCodexTranscriptAuthorityError(
                "fresh Codex rollout contains an unexpected record type"
            )
        if record_type == "response_item" and payload.get("type") != "message":
            raise ControlledCodexTranscriptAuthorityError(
                "fresh Codex rollout contains a non-message response item"
            )
        if record_type == "event_msg" and payload.get("type") not in allowed_event_types:
            raise ControlledCodexTranscriptAuthorityError(
                "fresh Codex rollout contains an unexpected event"
            )
    session_meta = _one(
        [row["payload"] for row in rollout if row.get("type") == "session_meta"],
        "rollout session_meta",
    )
    turn_context_record = _one(
        [(index, row["payload"]) for index, row in enumerate(rollout) if row.get("type") == "turn_context"],
        "rollout turn_context",
    )
    turn_context_index, turn_context = turn_context_record
    if (
        session_meta.get("id") != thread_id
        or session_meta.get("session_id") != thread_id
        or session_meta.get("cwd") != start["cwd"]
        or session_meta.get("cli_version") != "0.149.0"
        or session_meta.get("source") != "exec"
        or session_meta.get("originator") != "codex_exec"
        or session_meta.get("model_provider") != "openai"
        or session_meta.get("git") not in (None, {})
        or session_meta.get("forked_from_id") is not None
        or session_meta.get("parent_thread_id") is not None
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "fresh Codex rollout session metadata mismatch"
        )
    turn_id = turn_context.get("turn_id")
    if not isinstance(turn_id, str) or THREAD_ID_PATTERN.fullmatch(turn_id) is None:
        raise ControlledCodexTranscriptAuthorityError(
            "fresh Codex rollout turn id drift"
        )
    if (
        turn_context.get("cwd") != start["cwd"]
        or turn_context.get("model") != "gpt-5.6-sol"
        or turn_context.get("effort") != "ultra"
        or turn_context.get("approval_policy") != "never"
        or turn_context.get("sandbox_policy") != {"type": "read-only"}
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "fresh Codex rollout model/effort/cwd/sandbox/approval mismatch"
        )
    user_after_context = [
        row["payload"]
        for index, row in enumerate(rollout)
        if index > turn_context_index
        and row.get("type") == "response_item"
        and row.get("payload", {}).get("type") == "message"
        and row.get("payload", {}).get("role") == "user"
    ]
    user_message = _one(user_after_context, "exact prompt echo after turn context")
    prompt_text = _extract_message_text(user_message, "user", "input_text")
    try:
        expected_prompt_text = prompt_bytes.decode("utf-8")
    except UnicodeDecodeError as error:
        raise ControlledCodexTranscriptAuthorityError(
            "authority prompt is not UTF-8"
        ) from error
    if prompt_text != expected_prompt_text:
        raise ControlledCodexTranscriptAuthorityError(
            "fresh Codex rollout prompt echo mismatch"
        )
    assistant_messages = [
        row["payload"]
        for row in rollout
        if row.get("type") == "response_item"
        and row.get("payload", {}).get("type") == "message"
        and row.get("payload", {}).get("role") == "assistant"
        and row.get("payload", {}).get("phase") == "final_answer"
    ]
    assistant = _one(assistant_messages, "rollout final assistant message")
    if _extract_message_text(assistant, "assistant", "output_text") != output_text:
        raise ControlledCodexTranscriptAuthorityError(
            "fresh Codex rollout final assistant/output mismatch"
        )
    event_payloads = [
        row["payload"] for row in rollout if row.get("type") == "event_msg"
    ]
    if [row.get("type") for row in event_payloads] != [
        "task_started",
        "item_completed",
        "item_completed",
        "token_count",
        "task_complete",
    ]:
        raise ControlledCodexTranscriptAuthorityError(
            "fresh Codex rollout event order or tool-free event count drift"
        )
    task_started = _one(
        [row for row in event_payloads if row.get("type") == "task_started"],
        "rollout task_started",
    )
    task_complete = _one(
        [row for row in event_payloads if row.get("type") == "task_complete"],
        "rollout task_complete",
    )
    token_count = _one(
        [row for row in event_payloads if row.get("type") == "token_count"],
        "rollout final token_count",
    )
    if (
        task_started.get("turn_id") != turn_id
        or task_complete.get("turn_id") != turn_id
        or task_complete.get("last_agent_message") != output_text
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "fresh Codex rollout task identity/output mismatch"
        )
    total_usage = token_count.get("info", {}).get("total_token_usage")
    rollout_usage = _usage_projection(
        {key: total_usage.get(key) for key in stdout_usage} if isinstance(total_usage, dict) else None,
        "rollout total usage",
    )
    if rollout_usage != stdout_usage:
        raise ControlledCodexTranscriptAuthorityError(
            "fresh Codex stdout/rollout usage mismatch"
        )
    rollout_name = rollout_observation.path.name
    if thread_id not in rollout_name:
        raise ControlledCodexTranscriptAuthorityError(
            "fresh Codex rollout file name/thread id mismatch"
        )
    return {
        "schemaVersion": "gridworks.realtime-controlled-codex-sanitized-projection.v1",
        "threadId": thread_id,
        "turnId": turn_id,
        "localCodexClientReportedModel": "gpt-5.6-sol",
        "localCodexClientReportedReasoningEffort": "ultra",
        "platformModelAttested": False,
        "platformReasoningEffortAttested": False,
        "effectiveApiReasoningEffort": None,
        "approvalPolicy": "never",
        "sandbox": "read-only",
        "cwd": start["cwd"],
        "cliVersion": "0.149.0",
        "modelProvider": "openai",
        "sessionSource": "exec",
        "sessionOriginator": "codex_exec",
        "turnCount": 1,
        "toolCallCount": 0,
        "usage": stdout_usage,
        "promptEchoRawSha256": sha256_bytes(prompt_bytes),
        "outputEchoRawSha256": sha256_bytes(output_bytes),
        "rolloutRecordCount": len(rollout),
        "rolloutRawSha256": rollout_observation.raw_sha256,
        "rolloutByteLength": rollout_observation.byte_length,
        "baseDeveloperInstructionsProjected": False,
        "rawRolloutCopiedIntoTranscriptRoot": False,
    }


def _compose_final_receipt(
    *,
    root: Path,
    start: dict[str, Any],
    start_observation: FileObservation,
    parent: dict[str, Any],
    producer: dict[str, Any],
    policy_bytes: bytes,
    cli: dict[str, Any],
    after_inventory: dict[str, Any],
    after_inventory_observation: FileObservation,
    process_files: Mapping[str, FileObservation],
    rollout_observation: FileObservation,
    new_rollout_digest: str,
    projection: dict[str, Any],
    exit_code: int,
    freshness_window_end_ns: int,
) -> dict[str, Any]:
    receipt = {
        "schemaVersion": FINAL_SCHEMA,
        "canonicalization": CANONICALIZATION,
        "status": "FINALIZED_LOCAL_CODEX_ROLLOUT_MATCHED_REQUEST_NON_PLATFORM_ATTESTATION",
        "canonicalTranscriptRoot": str(root),
        "canonicalTranscriptReceiptPath": str(root / "controlled-codex-transcript.json"),
        "transcriptAuthorityId": start["transcriptAuthorityId"],
        "transcriptAuthoritySourceCommit": producer["sourceCommit"],
        "transcriptEvaluatorAuthority": producer,
        "transcriptPolicyRawSha256": sha256_bytes(policy_bytes),
        "parentAuthority": parent,
        "probeStartReceipt": {
            "canonicalPath": str(start_observation.path),
            "rawSha256": start_observation.raw_sha256,
            "probeStartReceiptSha256": start["probeStartReceiptSha256"],
            "status": start["status"],
        },
        "nonce": start["nonce"],
        "freshnessWindowStartUnixNs": start["freshnessWindowStartUnixNs"],
        "freshnessWindowEndUnixNs": freshness_window_end_ns,
        "rolloutInventoryBefore": start["rolloutInventoryBefore"],
        "rolloutInventoryAfter": after_inventory,
        "rolloutInventoryAfterRawSha256": after_inventory_observation.raw_sha256,
        "rolloutInventoryAfterPath": str(after_inventory_observation.path),
        "newRollout": {
            **rollout_observation.row(),
            "entryDigest": new_rollout_digest,
            "originalStoredOnlyInCanonicalCodexHome": True,
            "copiedIntoTranscriptRoot": False,
        },
        "processExitCode": exit_code,
        "processFiles": [
            {"role": role, **process_files[role].row()}
            for role in ("STDOUT", "STDERR", "OUTPUT")
        ],
        "codexCliAuthority": cli,
        "sanitizedTranscriptProjection": projection,
        "authorityClass": "LOCAL_CONTROLLED_CODEX_TRANSCRIPT_AUTHORITY",
        "platformAttestationStatus": "UNAVAILABLE_NOT_A_PLATFORM_SIGNED_EXECUTION_RECEIPT",
        "historicProbeStatus": "UNATTESTED_REFERENCE_ONLY_NOT_CONSUMED",
        "futureExecutableJudgeInputRequiresSeparateAuthorityAndReview": True,
        "thisProbeExecutesExecutableJudgeInput": False,
        "requestedModel": "gpt-5.6-sol",
        "requestedCodexClientEffort": "ultra",
        "rolloutReportedModel": "gpt-5.6-sol",
        "rolloutReportedCodexClientEffort": "ultra",
        "platformModelAttested": False,
        "platformReasoningEffortAttested": False,
        "platformFreshnessAttested": False,
        "serverSignedResponseReceipt": None,
        "effectiveApiReasoningEffort": None,
        "boundJudgeInputExecuted": False,
        "currentRouteJudgeModelCallCount": 0,
        "outputEchoIsNotModelIdentityAuthority": True,
        "officialCommercialUX": False,
        "scoreBearing": False,
        "scoreBearingCaptureAllowed": False,
        "commercialUXProxy": None,
        "limitations": load_transcript_policy()[0]["limitations"],
        "controlledCodexTranscriptSha256": "",
    }
    receipt["controlledCodexTranscriptSha256"] = self_hash(
        receipt,
        "controlledCodexTranscriptSha256",
    )
    return receipt


def _validate_inventory_object(value: dict[str, Any], phase: str, nonce: str) -> None:
    entries = value.get("entries")
    if not isinstance(entries, list):
        raise ControlledCodexTranscriptAuthorityError(
            f"{phase} rollout inventory entries are missing"
        )
    for row in entries:
        if (
            not isinstance(row, dict)
            or set(row) != {"pathDigest", "device", "inode", "linkCount", "entrySha256"}
            or not isinstance(row.get("pathDigest"), str)
            or re.fullmatch(r"sha256:[0-9a-f]{64}", row["pathDigest"]) is None
            or type(row.get("device")) is not int
            or row["device"] < 0
            or type(row.get("inode")) is not int
            or row["inode"] <= 0
            or row.get("linkCount") != 1
            or row.get("entrySha256") != self_hash(row, "entrySha256")
        ):
            raise ControlledCodexTranscriptAuthorityError(
                f"{phase} rollout inventory entry semantic drift"
            )
    if (
        value.get("schemaVersion") != INVENTORY_SCHEMA
        or value.get("phase") != phase
        or value.get("nonce") != nonce
        or value.get("historicalRawPathsThreadIdsSizesAndTimesPersisted") is not False
        or value.get("entries") != sorted(entries, key=lambda row: row["pathDigest"])
        or len(entries) != len({row["pathDigest"] for row in entries})
        or value.get("fileCount") != len(entries)
        or value.get("entryTreeSha256") != canonical_sha256(entries)
        or value.get("inventorySha256") != self_hash(value, "inventorySha256")
    ):
        raise ControlledCodexTranscriptAuthorityError(
            f"{phase} rollout inventory semantic drift"
        )


def _read_canonical_receipt(path: Path, label: str, self_field: str) -> tuple[FileObservation, bytes, dict[str, Any]]:
    observation, data = _read_regular_nlink_one(path, label)
    value = strict_json_bytes(data, label)
    if data != json_file_bytes(value):
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} is not canonical JSON file bytes"
        )
    if value.get(self_field) != self_hash(value, self_field):
        raise ControlledCodexTranscriptAuthorityError(
            f"{label} self-hash mismatch"
        )
    return observation, data, value


def _validate_root_inventory(root: Path, *, finalized: bool) -> None:
    policy, policy_bytes = load_transcript_policy()
    validate_transcript_policy(policy, policy_bytes)
    expected = set(policy["controlledExecution"]["exactFinalRootInventory"])
    if not finalized:
        expected.remove("rollout-inventory-after.json")
    actual = {path.name for path in root.iterdir()}
    if actual != expected:
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript root phase inventory drift"
        )
    capsule = _canonical_directory(root / "capsule", "controlled execution capsule")
    if any(capsule.iterdir()):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled execution capsule is not empty"
        )
    for name in actual - {"capsule"}:
        _read_regular_nlink_one(root / name, f"controlled transcript inventory {name}")


def _launch_codex_process(
    argv: Sequence[str],
    environment: Mapping[str, str],
    prompt: bytes,
    stdout_descriptor: int,
    stderr_descriptor: int,
    cwd: Path,
    timeout_seconds: int,
) -> int:
    process = subprocess.Popen(
        list(argv),
        cwd=cwd,
        env=dict(environment),
        stdin=subprocess.PIPE,
        stdout=stdout_descriptor,
        stderr=stderr_descriptor,
        close_fds=True,
        start_new_session=True,
    )
    try:
        process.communicate(prompt, timeout=timeout_seconds)
    except subprocess.TimeoutExpired as error:
        os.killpg(process.pid, signal.SIGKILL)
        process.wait()
        raise ControlledCodexTranscriptAuthorityError(
            "controlled Codex probe exceeded its fixed timeout"
        ) from error
    except BaseException:
        if process.poll() is None:
            os.killpg(process.pid, signal.SIGKILL)
            process.wait()
        raise
    return process.returncode


def _create_controlled_codex_transcript_authority(
    repository_root: Path,
    aggregate_path: Path,
    *,
    transcript_authority_revision: str = "HEAD",
) -> tuple[Path, dict[str, Any]]:
    root_repository = CANDIDATE.resolve_repository_root(repository_root)
    policy, policy_bytes = load_transcript_policy()
    validate_transcript_policy(policy, policy_bytes)
    producer = bind_transcript_evaluator_authority(
        root_repository,
        transcript_authority_revision,
    )
    cli = bind_codex_cli_authority()
    chain_claim_path, session_claim_path = _locate_session_claim_from_aggregate(
        aggregate_path
    )
    del chain_claim_path
    sessions_lock_descriptor: int | None = None
    final_receipt_descriptor: int | None = None
    try:
        with SESSION.exclusive_claim_lock(session_claim_path):
            environment, _codex_home, sessions_root = _fixed_process_environment()
            sessions_lock_descriptor = _acquire_sessions_directory_flock(
                sessions_root
            )
            parent_context = _reconstruct_parent_artifacts_without_lock(
                root_repository,
                aggregate_path,
            )
            if parent_context.session_claim_path != session_claim_path:
                raise ControlledCodexTranscriptAuthorityError(
                    "locked session claim differs from reconstructed parent authority"
                )
            parent = _parent_projection(parent_context)
            transcript_root_path = Path(
                str(parent_context.parent.claim["canonicalChainRoot"])
                + policy["controlledExecution"]["transcriptRootSuffix"]
            )
            _require_outside_repository(
                transcript_root_path,
                root_repository,
                "controlled Codex transcript root",
            )
            transcript_root = _exclusive_directory(
                transcript_root_path,
                "controlled Codex transcript root",
            )
            capsule = _exclusive_directory(
                transcript_root / policy["controlledExecution"]["capsuleDirectoryName"],
                "controlled Codex execution capsule",
            )
            if any(capsule.iterdir()):
                raise ControlledCodexTranscriptAuthorityError(
                    "controlled Codex execution capsule was prepopulated"
                )
            nonce = os.urandom(policy["controlledExecution"]["nonceByteCount"]).hex()
            freshness_window_start_ns = time.time_ns()
            before_private, before_inventory = _scan_rollout_inventory(
                sessions_root,
                nonce,
                "BEFORE",
            )
            before_path = transcript_root / policy["controlledExecution"][
                "beforeInventoryFileName"
            ]
            before_observation = _exclusive_write(
                before_path,
                json_file_bytes(before_inventory),
                "before rollout inventory",
            )
            prompt = _compose_prompt(parent, nonce)
            prompt_path = transcript_root / policy["controlledExecution"]["promptFileName"]
            prompt_observation = _exclusive_write(
                prompt_path,
                prompt,
                "controlled Codex prompt",
            )
            output_schema_observation, output_schema_bytes = _read_regular_nlink_one(
                OUTPUT_SCHEMA_PATH,
                "source-bound probe output schema",
            )
            if (
                output_schema_observation.raw_sha256
                != policy["invocation"]["structuredOutputSchemaRawSha256"]
                or strict_json_bytes(output_schema_bytes, "probe output schema").get("$schema")
                != "https://json-schema.org/draft/2020-12/schema"
            ):
                raise ControlledCodexTranscriptAuthorityError(
                    "source-bound probe output schema drift"
                )
            stdout_path = transcript_root / policy["controlledExecution"]["stdoutFileName"]
            stderr_path = transcript_root / policy["controlledExecution"]["stderrFileName"]
            output_path = transcript_root / policy["controlledExecution"]["outputFileName"]
            stdout_descriptor, stdout_reserved = _reserve_empty_file(
                stdout_path,
                "controlled Codex stdout",
            )
            stderr_descriptor, stderr_reserved = _reserve_empty_file(
                stderr_path,
                "controlled Codex stderr",
            )
            output_descriptor, output_reserved = _reserve_empty_file(
                output_path,
                "controlled Codex final output",
            )
            os.close(output_descriptor)
            final_path = transcript_root / policy["controlledExecution"]["finalReceiptFileName"]
            final_receipt_descriptor, final_receipt_reserved = _reserve_empty_file(
                final_path,
                "controlled Codex final transcript receipt reservation",
            )
            reservations = {
                "STDOUT": stdout_reserved,
                "STDERR": stderr_reserved,
                "OUTPUT": output_reserved,
            }
            argv = _build_argv(cli, capsule, output_path)
            start = _compose_start_receipt(
                root=transcript_root,
                capsule=capsule,
                parent=parent,
                producer=producer,
                policy_bytes=policy_bytes,
                nonce=nonce,
                prompt_observation=prompt_observation,
                prompt=prompt,
                output_schema_observation=output_schema_observation,
                before_inventory=before_inventory,
                before_inventory_observation=before_observation,
                reservations=reservations,
                final_receipt_reservation=final_receipt_reserved,
                cli=cli,
                argv=argv,
                environment=environment,
                freshness_window_start_ns=freshness_window_start_ns,
            )
            start_path = transcript_root / policy["controlledExecution"]["startReceiptFileName"]
            start_observation = _exclusive_write(
                start_path,
                json_file_bytes(start),
                "controlled Codex probe start receipt",
            )
            _start_obs_again, start_bytes_again, start_again = _read_canonical_receipt(
                start_path,
                "controlled Codex probe start receipt pre-execution reread",
                "probeStartReceiptSha256",
            )
            if start_bytes_again != json_file_bytes(start) or not strict_typed_equal(start, start_again):
                raise ControlledCodexTranscriptAuthorityError(
                    "controlled Codex start receipt changed before execution"
                )
            _validate_root_inventory(transcript_root, finalized=False)
            _assert_parent_unchanged(
                root_repository,
                parent_context,
                "pre-execution finalization",
            )
            _assert_producer_unchanged(
                root_repository,
                producer,
                "pre-execution finalization",
            )
            _assert_cli_unchanged(cli, "pre-execution finalization")
            try:
                exit_code = _launch_codex_process(
                    argv,
                    environment,
                    prompt,
                    stdout_descriptor,
                    stderr_descriptor,
                    capsule,
                    policy["invocation"]["timeoutSeconds"],
                )
                os.fsync(stdout_descriptor)
                os.fsync(stderr_descriptor)
            finally:
                os.close(stdout_descriptor)
                os.close(stderr_descriptor)
            freshness_window_end_ns = time.time_ns()
            _assert_cli_unchanged(cli, "immediate post-execution observation")
            if exit_code != policy["invocation"]["requiredExitCode"]:
                raise ControlledCodexTranscriptAuthorityError(
                    f"controlled Codex probe exited {exit_code}, expected zero"
                )
            process_files: dict[str, FileObservation] = {}
            process_bytes: dict[str, bytes] = {}
            for role, path in (
                ("STDOUT", stdout_path),
                ("STDERR", stderr_path),
                ("OUTPUT", output_path),
            ):
                observed, data = _read_regular_nlink_one(
                    path,
                    f"controlled Codex {role.lower()} final",
                )
                _assert_reserved_identity(reservations[role], observed, role)
                process_files[role] = observed
                process_bytes[role] = data
            after_private, after_inventory = _scan_rollout_inventory(
                sessions_root,
                nonce,
                "AFTER",
            )
            new_rollout = _validate_rollout_delta(
                before_private,
                before_inventory,
                after_private,
                after_inventory,
            )
            if not (
                freshness_window_start_ns <= new_rollout.changed_ns <= freshness_window_end_ns
                and freshness_window_start_ns <= new_rollout.modified_ns <= freshness_window_end_ns
            ):
                raise ControlledCodexTranscriptAuthorityError(
                    "new Codex rollout is stale or outside the execution window"
                )
            after_path = transcript_root / policy["controlledExecution"][
                "afterInventoryFileName"
            ]
            after_observation = _exclusive_write(
                after_path,
                json_file_bytes(after_inventory),
                "after rollout inventory",
            )
            rollout_observation, rollout_bytes = _read_regular_nlink_one(
                new_rollout.path,
                "fresh controlled Codex rollout",
            )
            if (
                rollout_observation.device != new_rollout.device
                or rollout_observation.inode != new_rollout.inode
                or rollout_observation.link_count != new_rollout.link_count
                or rollout_observation.byte_length != new_rollout.byte_length
                or rollout_observation.modified_ns != new_rollout.modified_ns
                or rollout_observation.changed_ns != new_rollout.changed_ns
            ):
                raise ControlledCodexTranscriptAuthorityError(
                    "fresh Codex rollout changed after the after inventory scan"
                )
            projection = _verify_execution_transcript(
                start=start,
                stdout_bytes=process_bytes["STDOUT"],
                stderr_bytes=process_bytes["STDERR"],
                output_bytes=process_bytes["OUTPUT"],
                rollout_observation=rollout_observation,
                rollout_bytes=rollout_bytes,
                prompt_bytes=prompt,
            )
            final = _compose_final_receipt(
                root=transcript_root,
                start=start,
                start_observation=start_observation,
                parent=parent,
                producer=producer,
                policy_bytes=policy_bytes,
                cli=cli,
                after_inventory=after_inventory,
                after_inventory_observation=after_observation,
                process_files=process_files,
                rollout_observation=rollout_observation,
                new_rollout_digest=new_rollout.digest,
                projection=projection,
                exit_code=exit_code,
                freshness_window_end_ns=freshness_window_end_ns,
            )
            _validate_root_inventory(transcript_root, finalized=True)
            for observation, expected_data, label in (
                (before_observation, json_file_bytes(before_inventory), "before inventory pre-finalization"),
                (prompt_observation, prompt, "prompt pre-finalization"),
                (output_schema_observation, output_schema_bytes, "output schema pre-finalization"),
                (start_observation, json_file_bytes(start), "start receipt pre-finalization"),
                (process_files["STDOUT"], process_bytes["STDOUT"], "stdout pre-finalization"),
                (process_files["STDERR"], process_bytes["STDERR"], "stderr pre-finalization"),
                (process_files["OUTPUT"], process_bytes["OUTPUT"], "output pre-finalization"),
                (after_observation, json_file_bytes(after_inventory), "after inventory pre-finalization"),
                (rollout_observation, rollout_bytes, "rollout pre-finalization"),
            ):
                _assert_observation_and_bytes_unchanged(
                    observation,
                    expected_data,
                    label,
                )
            final_empty_observation, final_empty_bytes = _read_regular_nlink_one(
                final_path,
                "final receipt reservation pre-finalization",
            )
            _assert_reserved_identity(
                final_receipt_reserved,
                final_empty_observation,
                "final receipt reservation pre-finalization",
            )
            if (
                final_empty_observation != final_receipt_reserved
                or final_empty_bytes != b""
            ):
                raise ControlledCodexTranscriptAuthorityError(
                    "final receipt reservation identity or bytes changed before commit"
                )
            _assert_parent_unchanged(
                root_repository,
                parent_context,
                "pre-final-receipt finalization",
            )
            _assert_producer_unchanged(
                root_repository,
                producer,
                "pre-final-receipt finalization",
            )
            _assert_cli_unchanged(cli, "pre-final-receipt finalization")
            _assert_rollout_inventory_still_exact(
                sessions_root,
                nonce,
                after_inventory,
                "pre-final-receipt finalization",
            )
            _write_reserved_descriptor(
                final_receipt_descriptor,
                final_receipt_reserved,
                json_file_bytes(final),
                "controlled Codex final transcript receipt",
            )
            os.close(final_receipt_descriptor)
            final_receipt_descriptor = None
            _fsync_directory(transcript_root)
            verified, _raw_hash = _verify_controlled_codex_transcript_without_lock(
                root_repository,
                final_path,
                parent_context=parent_context,
                producer=producer,
            )
            if not strict_typed_equal(final, verified):
                raise ControlledCodexTranscriptAuthorityError(
                    "controlled Codex final receipt verification drift"
                )
            _assert_observation_and_bytes_unchanged(
                rollout_observation,
                rollout_bytes,
                "rollout post-final-receipt verification",
            )
            _assert_parent_unchanged(
                root_repository,
                parent_context,
                "controlled transcript finalization",
            )
            _assert_producer_unchanged(
                root_repository,
                producer,
                "controlled transcript finalization",
            )
            _assert_cli_unchanged(cli, "controlled transcript finalization")
            _assert_rollout_inventory_still_exact(
                sessions_root,
                nonce,
                after_inventory,
                "post-final-receipt verification",
            )
            return final_path, final
    except (SESSION.SessionAuthorityError, ARTIFACT.CurrentRouteArtifactAuthorityError) as error:
        raise ControlledCodexTranscriptAuthorityError(str(error)) from error
    finally:
        if final_receipt_descriptor is not None:
            os.close(final_receipt_descriptor)
        if sessions_lock_descriptor is not None:
            _release_sessions_directory_flock(sessions_lock_descriptor)


def _verify_controlled_codex_transcript_without_lock(
    repository_root: Path,
    receipt_path: Path,
    *,
    parent_context: ParentArtifactContext | None = None,
    producer: dict[str, Any] | None = None,
) -> tuple[dict[str, Any], str]:
    final_observation, final_bytes, final = _read_canonical_receipt(
        receipt_path,
        "controlled Codex final transcript receipt",
        "controlledCodexTranscriptSha256",
    )
    root = _canonical_directory(final_observation.path.parent, "controlled transcript root")
    _require_outside_repository(
        root,
        repository_root,
        "controlled Codex transcript root",
    )
    policy, policy_bytes = load_transcript_policy()
    validate_transcript_policy(policy, policy_bytes)
    if (
        final_observation.path != root / policy["controlledExecution"]["finalReceiptFileName"]
        or final.get("canonicalTranscriptRoot") != str(root)
        or final.get("canonicalTranscriptReceiptPath") != str(final_observation.path)
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript final receipt path binding drift"
        )
    _validate_root_inventory(root, finalized=True)
    source_commit = final.get("transcriptAuthoritySourceCommit")
    if not isinstance(source_commit, str):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript lacks source commit"
        )
    bound_producer = producer or bind_transcript_evaluator_authority(
        repository_root,
        source_commit,
    )
    if not strict_typed_equal(final.get("transcriptEvaluatorAuthority"), bound_producer):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript producer projection mismatch"
        )
    aggregate_value = final.get("parentAuthority", {}).get("aggregatePath")
    if not isinstance(aggregate_value, str):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript lacks parent aggregate path"
        )
    parent = parent_context or _reconstruct_parent_artifacts_without_lock(
        repository_root,
        Path(aggregate_value),
    )
    parent_projection = _parent_projection(parent)
    if not strict_typed_equal(final.get("parentAuthority"), parent_projection):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript parent projection mismatch"
        )
    start_path = root / policy["controlledExecution"]["startReceiptFileName"]
    start_observation, start_bytes, start = _read_canonical_receipt(
        start_path,
        "controlled Codex probe start receipt",
        "probeStartReceiptSha256",
    )
    start_projection = final.get("probeStartReceipt")
    if start_projection != {
        "canonicalPath": str(start_path),
        "rawSha256": sha256_bytes(start_bytes),
        "probeStartReceiptSha256": start["probeStartReceiptSha256"],
        "status": start["status"],
    }:
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript start receipt binding mismatch"
        )
    if (
        start.get("canonicalTranscriptRoot") != str(root)
        or start.get("canonicalStartReceiptPath") != str(start_path)
        or start.get("transcriptAuthorityId") != final.get("transcriptAuthorityId")
        or start.get("transcriptAuthoritySourceCommit") != source_commit
        or not strict_typed_equal(start.get("transcriptEvaluatorAuthority"), bound_producer)
        or not strict_typed_equal(start.get("parentAuthority"), parent_projection)
        or start.get("transcriptPolicyRawSha256") != sha256_bytes(policy_bytes)
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript start receipt authority mismatch"
        )
    final_reservation = start.get("reservedFinalReceipt")
    final_reservation_observation = _observation_from_receipt_row(
        final_reservation,
        "controlled transcript final receipt reservation",
    )
    if (
        final_reservation.get("canonicalPath") != str(final_observation.path)
        or final_reservation.get("device") != final_observation.device
        or final_reservation.get("inode") != final_observation.inode
        or final_reservation.get("mode") != final_observation.mode
        or final_reservation.get("linkCount") != 1
        or final_reservation.get("byteLength") != 0
        or final_reservation.get("rawSha256") != EMPTY_RAW_SHA256
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript final receipt reservation binding mismatch"
        )
    nonce = start.get("nonce")
    if not isinstance(nonce, str) or re.fullmatch(r"[0-9a-f]{64}", nonce) is None:
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript nonce drift"
        )
    before_path = root / policy["controlledExecution"]["beforeInventoryFileName"]
    before_observation, before_bytes = _read_regular_nlink_one(
        before_path,
        "before rollout inventory",
    )
    before = strict_json_bytes(before_bytes, "before rollout inventory")
    _validate_inventory_object(before, "BEFORE", nonce)
    if (
        not strict_typed_equal(start.get("rolloutInventoryBefore"), before)
        or start.get("rolloutInventoryBeforeRawSha256") != before_observation.raw_sha256
        or start.get("rolloutInventoryBeforePath") != str(before_path)
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript before inventory binding mismatch"
        )
    after_path = root / policy["controlledExecution"]["afterInventoryFileName"]
    after_observation, after_bytes = _read_regular_nlink_one(
        after_path,
        "after rollout inventory",
    )
    after = strict_json_bytes(after_bytes, "after rollout inventory")
    _validate_inventory_object(after, "AFTER", nonce)
    if (
        not strict_typed_equal(final.get("rolloutInventoryBefore"), before)
        or not strict_typed_equal(final.get("rolloutInventoryAfter"), after)
        or final.get("rolloutInventoryAfterRawSha256") != after_observation.raw_sha256
        or final.get("rolloutInventoryAfterPath") != str(after_path)
        or {row["entrySha256"] for row in before["entries"]}
        - {row["entrySha256"] for row in after["entries"]}
        or len(
            {row["entrySha256"] for row in after["entries"]}
            - {row["entrySha256"] for row in before["entries"]}
        ) != 1
        or after["fileCount"] != before["fileCount"] + 1
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript after inventory delta mismatch"
        )
    prompt_path = root / policy["controlledExecution"]["promptFileName"]
    prompt_observation, prompt_bytes = _read_regular_nlink_one(
        prompt_path,
        "controlled Codex prompt",
    )
    expected_prompt = _compose_prompt(parent_projection, nonce)
    if prompt_bytes != expected_prompt or start.get("prompt") != {
        "canonicalPath": str(prompt_path),
        "rawSha256": prompt_observation.raw_sha256,
        "byteLength": prompt_observation.byte_length,
        "transport": "STDIN_DASH_EXACT_BYTES",
        "nonceChainParentAndBlockedJudgeHashesEchoRequired": True,
    }:
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript prompt reconstruction mismatch"
        )
    output_schema_observation, output_schema_bytes = _read_regular_nlink_one(
        OUTPUT_SCHEMA_PATH,
        "source-bound probe output schema",
    )
    if (
        output_schema_observation.raw_sha256
        != policy["invocation"]["structuredOutputSchemaRawSha256"]
        or start.get("structuredOutputSchema") != {
            "canonicalPath": str(OUTPUT_SCHEMA_PATH),
            "rawSha256": output_schema_observation.raw_sha256,
            "byteLength": output_schema_observation.byte_length,
            "authority": "SOURCE_BOUND_STRUCTURAL_OUTPUT_CONTRACT_SEMANTIC_ECHO_RECONSTRUCTED",
        }
        or not isinstance(strict_json_bytes(output_schema_bytes, "probe output schema"), dict)
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript output schema reconstruction mismatch"
        )
    capsule = _canonical_directory(root / policy["controlledExecution"]["capsuleDirectoryName"], "controlled execution capsule")
    if any(capsule.iterdir()) or start.get("executionCapsule") != {
        "canonicalPath": str(capsule),
        "inventoryBeforeExecution": [],
        "gitRepositoryCheckSkipped": True,
        "repositoryAgentsRulesOrCodeLoadedFromCwd": False,
    }:
        raise ControlledCodexTranscriptAuthorityError(
            "controlled execution capsule reconstruction mismatch"
        )
    cli = bind_codex_cli_authority()
    if (
        not strict_typed_equal(start.get("codexCliAuthority"), cli)
        or not strict_typed_equal(final.get("codexCliAuthority"), cli)
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript pinned CLI reconstruction mismatch"
        )
    environment, _codex_home, sessions_root = _fixed_process_environment()
    output_path = root / policy["controlledExecution"]["outputFileName"]
    expected_argv = _build_argv(cli, capsule, output_path)
    if (
        start.get("argv") != expected_argv
        or start.get("environment") != _environment_rows(environment)
        or start.get("cwd") != str(capsule)
        or start.get("requiredModel") != "gpt-5.6-sol"
        or start.get("requiredReasoningEffort") != "ultra"
        or start.get("requiredSandbox") != "read-only"
        or start.get("requiredApprovalPolicy") != "never"
        or start.get("subprocessStartedAtReceiptFinalization") is not False
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript exact invocation reconstruction mismatch"
        )
    process_observations: dict[str, FileObservation] = {}
    process_bytes: dict[str, bytes] = {}
    reserved_process_observations: dict[str, FileObservation] = {}
    final_rows = final.get("processFiles")
    start_rows = start.get("reservedProcessFiles")
    if not isinstance(final_rows, list) or not isinstance(start_rows, list):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript process file rows are missing"
        )
    for role, name in (
        ("STDOUT", policy["controlledExecution"]["stdoutFileName"]),
        ("STDERR", policy["controlledExecution"]["stderrFileName"]),
        ("OUTPUT", policy["controlledExecution"]["outputFileName"]),
    ):
        observation, data = _read_regular_nlink_one(
            root / name,
            f"controlled Codex {role.lower()}",
        )
        start_row = _one([row for row in start_rows if row.get("role") == role], f"start {role} reservation")
        final_row = _one([row for row in final_rows if row.get("role") == role], f"final {role} binding")
        reserved_process_observations[role] = _observation_from_receipt_row(
            start_row,
            f"controlled transcript start {role} reservation",
            role=role,
        )
        if (
            start_row.get("canonicalPath") != str(observation.path)
            or start_row.get("device") != observation.device
            or start_row.get("inode") != observation.inode
            or start_row.get("mode") != observation.mode
            or start_row.get("linkCount") != 1
            or start_row.get("byteLength") != 0
            or start_row.get("rawSha256") != EMPTY_RAW_SHA256
            or final_row != {"role": role, **observation.row()}
        ):
            raise ControlledCodexTranscriptAuthorityError(
                f"controlled transcript {role} reservation/final binding mismatch"
            )
        process_observations[role] = observation
        process_bytes[role] = data
    new_rollout = final.get("newRollout")
    if not isinstance(new_rollout, dict) or not isinstance(new_rollout.get("canonicalPath"), str):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript new rollout binding is missing"
        )
    rollout_observation, rollout_bytes = _read_regular_nlink_one(
        Path(new_rollout["canonicalPath"]),
        "fresh controlled Codex rollout",
    )
    try:
        rollout_relative = rollout_observation.path.relative_to(sessions_root).as_posix()
    except ValueError as error:
        raise ControlledCodexTranscriptAuthorityError(
            "fresh controlled Codex rollout escaped canonical sessions root"
        ) from error
    if ROLLOUT_RELATIVE_PATTERN.fullmatch(rollout_relative) is None:
        raise ControlledCodexTranscriptAuthorityError(
            "fresh controlled Codex rollout path shape drift"
        )
    projected_new_entry = _project_inventory_entry(
        nonce,
        rollout_relative,
        rollout_observation.device,
        rollout_observation.inode,
        rollout_observation.link_count,
    )
    expected_new_row = {
        **rollout_observation.row(),
        "entryDigest": projected_new_entry["entrySha256"],
        "originalStoredOnlyInCanonicalCodexHome": True,
        "copiedIntoTranscriptRoot": False,
    }
    if new_rollout != expected_new_row:
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript new rollout raw/stat binding mismatch"
        )
    freshness_start = start.get("freshnessWindowStartUnixNs")
    freshness_end = final.get("freshnessWindowEndUnixNs")
    if (
        type(freshness_start) is not int
        or type(freshness_end) is not int
        or freshness_start <= 0
        or freshness_end < freshness_start
        or not (
            freshness_start
            <= rollout_observation.changed_ns
            <= freshness_end
        )
        or not (
            freshness_start
            <= rollout_observation.modified_ns
            <= freshness_end
        )
    ):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript rollout freshness window mismatch"
        )
    expected_start = _compose_start_receipt(
        root=root,
        capsule=capsule,
        parent=parent_projection,
        producer=bound_producer,
        policy_bytes=policy_bytes,
        nonce=nonce,
        prompt_observation=prompt_observation,
        prompt=prompt_bytes,
        output_schema_observation=output_schema_observation,
        before_inventory=before,
        before_inventory_observation=before_observation,
        reservations=reserved_process_observations,
        final_receipt_reservation=final_reservation_observation,
        cli=cli,
        argv=expected_argv,
        environment=environment,
        freshness_window_start_ns=freshness_start,
    )
    if not strict_typed_equal(start, expected_start):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled Codex start receipt differs from reconstructed authority"
        )
    added_digests = (
        {row["entrySha256"] for row in after["entries"]}
        - {row["entrySha256"] for row in before["entries"]}
    )
    if added_digests != {new_rollout["entryDigest"]}:
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript new rollout/digest delta mismatch"
        )
    if projected_new_entry not in after["entries"]:
        raise ControlledCodexTranscriptAuthorityError(
            "controlled transcript new rollout is absent from salted after inventory"
        )
    projection = _verify_execution_transcript(
        start=start,
        stdout_bytes=process_bytes["STDOUT"],
        stderr_bytes=process_bytes["STDERR"],
        output_bytes=process_bytes["OUTPUT"],
        rollout_observation=rollout_observation,
        rollout_bytes=rollout_bytes,
        prompt_bytes=prompt_bytes,
    )
    if final.get("processExitCode") != 0:
        raise ControlledCodexTranscriptAuthorityError(
            "controlled Codex process exit code is not zero"
        )
    expected_final = _compose_final_receipt(
        root=root,
        start=start,
        start_observation=start_observation,
        parent=parent_projection,
        producer=bound_producer,
        policy_bytes=policy_bytes,
        cli=cli,
        after_inventory=after,
        after_inventory_observation=after_observation,
        process_files=process_observations,
        rollout_observation=rollout_observation,
        new_rollout_digest=new_rollout["entryDigest"],
        projection=projection,
        exit_code=final.get("processExitCode"),
        freshness_window_end_ns=final.get("freshnessWindowEndUnixNs"),
    )
    if not strict_typed_equal(final, expected_final):
        raise ControlledCodexTranscriptAuthorityError(
            "controlled Codex final receipt differs from reconstructed authority"
        )
    first_pass = {
        "final": final_bytes,
        "start": start_bytes,
        "before": before_bytes,
        "after": after_bytes,
        "prompt": prompt_bytes,
        "stdout": process_bytes["STDOUT"],
        "stderr": process_bytes["STDERR"],
        "output": process_bytes["OUTPUT"],
        "rollout": rollout_bytes,
    }
    first_observations = {
        "final": final_observation,
        "start": start_observation,
        "before": before_observation,
        "after": after_observation,
        "prompt": prompt_observation,
        "stdout": process_observations["STDOUT"],
        "stderr": process_observations["STDERR"],
        "output": process_observations["OUTPUT"],
        "rollout": rollout_observation,
    }
    second_paths = {
        "final": final_observation.path,
        "start": start_path,
        "before": before_path,
        "after": after_path,
        "prompt": prompt_path,
        "stdout": root / policy["controlledExecution"]["stdoutFileName"],
        "stderr": root / policy["controlledExecution"]["stderrFileName"],
        "output": output_path,
        "rollout": rollout_observation.path,
    }
    for label, path in second_paths.items():
        observation, data = _read_regular_nlink_one(
            path,
            f"final second-pass {label}",
        )
        if observation != first_observations[label] or data != first_pass[label]:
            raise ControlledCodexTranscriptAuthorityError(
                f"controlled transcript {label} bytes or identity changed during second pass"
            )
    _validate_root_inventory(root, finalized=True)
    _assert_parent_unchanged(
        repository_root,
        parent,
        "controlled transcript semantic verification",
    )
    _assert_producer_unchanged(
        repository_root,
        bound_producer,
        "controlled transcript semantic verification",
    )
    _assert_cli_unchanged(cli, "controlled transcript semantic verification")
    return final, sha256_bytes(final_bytes)


def create_controlled_codex_transcript_authority(
    repository_root: Path,
    aggregate_path: Path,
    *,
    transcript_authority_revision: str = "HEAD",
) -> tuple[Path, dict[str, Any]]:
    try:
        return _create_controlled_codex_transcript_authority(
            repository_root,
            aggregate_path,
            transcript_authority_revision=transcript_authority_revision,
        )
    except ControlledCodexTranscriptAuthorityError:
        raise
    except (KeyError, IndexError, TypeError, AttributeError, StopIteration) as error:
        raise ControlledCodexTranscriptAuthorityError(
            "malformed controlled transcript or parent authority structure"
        ) from error
    except OSError as error:
        raise ControlledCodexTranscriptAuthorityError(
            f"filesystem changed during controlled Codex transcript creation: {error}"
        ) from error


def verify_controlled_codex_transcript_against_reconstructed_authority(
    repository_root: Path,
    receipt_path: Path,
) -> tuple[dict[str, Any], str]:
    try:
        _observation, data = _read_regular_nlink_one(
            receipt_path,
            "submitted controlled Codex transcript receipt",
        )
        submitted = strict_json_bytes(data, "submitted controlled Codex transcript receipt")
        aggregate_value = submitted.get("parentAuthority", {}).get("aggregatePath")
        if not isinstance(aggregate_value, str):
            raise ControlledCodexTranscriptAuthorityError(
                "submitted controlled transcript lacks parent aggregate path"
            )
        _chain_claim_path, session_claim_path = _locate_session_claim_from_aggregate(
            Path(aggregate_value)
        )
        with SESSION.exclusive_claim_lock(session_claim_path):
            _environment, _codex_home, sessions_root = _fixed_process_environment()
            sessions_lock_descriptor = _acquire_sessions_directory_flock(
                sessions_root
            )
            try:
                parent = _reconstruct_parent_artifacts_without_lock(
                    repository_root,
                    Path(aggregate_value),
                )
                if parent.session_claim_path != session_claim_path:
                    raise ControlledCodexTranscriptAuthorityError(
                        "locked session claim differs from reconstructed parent authority"
                    )
                result, raw_hash = _verify_controlled_codex_transcript_without_lock(
                    repository_root,
                    receipt_path,
                    parent_context=parent,
                )
                _assert_parent_unchanged(
                    repository_root,
                    parent,
                    "public controlled transcript verification finalization",
                )
                return result, raw_hash
            finally:
                _release_sessions_directory_flock(sessions_lock_descriptor)
    except ControlledCodexTranscriptAuthorityError:
        raise
    except (KeyError, IndexError, TypeError, AttributeError, StopIteration) as error:
        raise ControlledCodexTranscriptAuthorityError(
            "malformed controlled transcript or parent authority structure"
        ) from error
    except (OSError, SESSION.SessionAuthorityError) as error:
        raise ControlledCodexTranscriptAuthorityError(
            f"filesystem changed during controlled Codex transcript verification: {error}"
        ) from error


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Create and verify one controlled local Codex non-score transcript."
    )
    commands = parser.add_subparsers(dest="command", required=True)
    create = commands.add_parser("create-controlled-transcript")
    create.add_argument("--repository-root", type=Path, default=DEFAULT_REPOSITORY_ROOT)
    create.add_argument("--aggregate", type=Path, required=True)
    create.add_argument("--transcript-authority-revision", default="HEAD")
    verify = commands.add_parser("verify-controlled-transcript")
    verify.add_argument("--repository-root", type=Path, default=DEFAULT_REPOSITORY_ROOT)
    verify.add_argument("--receipt", type=Path, required=True)
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
        if args.command == "create-controlled-transcript":
            path, receipt = create_controlled_codex_transcript_authority(
                args.repository_root,
                args.aggregate,
                transcript_authority_revision=args.transcript_authority_revision,
            )
            _print_result({
                "receiptPath": str(path),
                "transcriptAuthorityId": receipt["transcriptAuthorityId"],
                "status": receipt["status"],
                "authorityClass": receipt["authorityClass"],
                "platformAttestationStatus": receipt["platformAttestationStatus"],
                "officialCommercialUX": False,
                "commercialUXProxy": None,
            })
        elif args.command == "verify-controlled-transcript":
            receipt, raw_hash = verify_controlled_codex_transcript_against_reconstructed_authority(
                args.repository_root,
                args.receipt,
            )
            _print_result({
                "receiptPath": receipt["canonicalTranscriptReceiptPath"],
                "transcriptAuthorityId": receipt["transcriptAuthorityId"],
                "receiptRawSha256": raw_hash,
                "status": receipt["status"],
                "authorityClass": receipt["authorityClass"],
                "platformAttestationStatus": receipt["platformAttestationStatus"],
                "officialCommercialUX": False,
                "commercialUXProxy": None,
            })
        else:
            raise ControlledCodexTranscriptAuthorityError("unknown command")
    except (
        ControlledCodexTranscriptAuthorityError,
        ARTIFACT.CurrentRouteArtifactAuthorityError,
        CHAIN.ChainAuthorityError,
        SESSION.SessionAuthorityError,
        CANDIDATE.CandidateAuthorityError,
    ) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
