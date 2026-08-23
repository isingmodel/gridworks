#!/usr/bin/env python3
"""Create and verify fail-closed realtime diagnostic session attempts.

This authority is intentionally non-score-bearing.  It binds the reviewed
Debug R2 candidate to either one exact targeted checkpoint or one exact
authored story part, then provides an append-only start/output/terminal
attempt lifecycle.  Interactive capture and full-flow substitution remain
forbidden.
"""

from __future__ import annotations

import argparse
import contextlib
import dataclasses
import fcntl
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import stat
import sys
from typing import Any, Iterator, Sequence


SHA256_PREFIX = "sha256:"
CANONICALIZATION = "GRIDWORKS_CANONICAL_JSON_V1"
SESSION_CLAIM_SCHEMA = "gridworks.realtime-evaluation-session-claim.v1"
ATTEMPT_START_SCHEMA = "gridworks.realtime-evaluation-attempt-start.v1"
ATTEMPT_TERMINAL_SCHEMA = "gridworks.realtime-evaluation-attempt-terminal.v1"
DIAGNOSTIC_OUTPUT_SCHEMA = "gridworks.realtime-diagnostic-attempt-output.v1"
SESSION_PRODUCER_SCHEMA = "gridworks.realtime-session-producer-authority.v1"

SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_REPOSITORY_ROOT = SCRIPT_DIR.parents[2]
CANDIDATE_MODULE_PATH = SCRIPT_DIR / "build-realtime-candidate-authority.py"
POLICY_PATH = SCRIPT_DIR / "realtime-session-policy.json"
CLAIM_SCHEMA_PATH = SCRIPT_DIR / "realtime-session-claim.schema.json"
START_SCHEMA_PATH = SCRIPT_DIR / "realtime-attempt-start.schema.json"
TERMINAL_SCHEMA_PATH = SCRIPT_DIR / "realtime-attempt-terminal.schema.json"
EXPECTED_POLICY_RAW_SHA256 = "sha256:2a3e8fdb4167b395115b032025ba5cc99b3c21274c3a9c75961fb1405102bfb5"

SESSION_PRODUCER_PATH_ROLES = (
    (
        "tools/commercial-ux/native/build-realtime-candidate-authority.py",
        "BOUND_CANDIDATE_SEMANTIC_VERIFIER_DEPENDENCY",
    ),
    (
        "tools/commercial-ux/native/realtime-attempt-start.schema.json",
        "STRUCTURAL_ATTEMPT_START_SCHEMA_NON_AUTHORITY",
    ),
    (
        "tools/commercial-ux/native/realtime-attempt-terminal.schema.json",
        "STRUCTURAL_ATTEMPT_TERMINAL_SCHEMA_NON_AUTHORITY",
    ),
    (
        "tools/commercial-ux/native/realtime-session-authority.py",
        "SESSION_PRODUCER_AND_SEMANTIC_VERIFIER",
    ),
    (
        "tools/commercial-ux/native/realtime-session-claim.schema.json",
        "STRUCTURAL_SESSION_CLAIM_SCHEMA_NON_AUTHORITY",
    ),
    (
        "tools/commercial-ux/native/realtime-session-policy.json",
        "SESSION_POLICY",
    ),
    (
        "tools/commercial-ux/native/test-realtime-session-authority.py",
        "ADVERSARIAL_SESSION_TEST_SPEC_NON_RUNTIME",
    ),
)

STORY_MANIFEST_PATH = (
    "playtests/commercial-ux-87-realtime/text-plan-r0/story-manifest.json"
)
ALLOWED_ROUTE_KINDS = (
    "TARGETED_CHECKPOINT",
    "STORY_PART_UNIT",
    "FULL_FLOW_EXCEPTION",
)
RETRYABLE_OUTCOMES = ("PRODUCER_NO_OUTPUT", "TRANSPORT_FAILURE")
NONRETRYABLE_OUTCOMES = ("SUCCESS", "INTEGRITY_FAILURE")


class SessionAuthorityError(ValueError):
    """Raised when session authority cannot be established exactly."""


def _load_candidate_module() -> Any:
    spec = importlib.util.spec_from_file_location(
        "realtime_candidate_authority_for_session",
        CANDIDATE_MODULE_PATH,
    )
    if spec is None or spec.loader is None:
        raise SessionAuthorityError(
            f"cannot load candidate authority module {CANDIDATE_MODULE_PATH}"
        )
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


CANDIDATE = _load_candidate_module()


def sha256_bytes(data: bytes) -> str:
    return CANDIDATE.sha256_bytes(data)


def canonical_bytes(value: Any) -> bytes:
    return CANDIDATE.canonical_bytes(value)


def canonical_sha256(value: Any) -> str:
    return sha256_bytes(canonical_bytes(value))


def strict_typed_equal(left: Any, right: Any) -> bool:
    return CANDIDATE.strict_typed_equal(left, right)


def strict_json_bytes(data: bytes, label: str) -> dict[str, Any]:
    try:
        return CANDIDATE.strict_json_bytes(data, label)
    except CANDIDATE.CandidateAuthorityError as error:
        raise SessionAuthorityError(str(error)) from error


def json_file_bytes(value: dict[str, Any]) -> bytes:
    return (
        json.dumps(
            value,
            ensure_ascii=False,
            allow_nan=False,
            indent=2,
            sort_keys=True,
        ).encode("utf-8")
        + b"\n"
    )


def self_hash(value: dict[str, Any], field: str) -> str:
    projection = dict(value)
    projection.pop(field, None)
    return canonical_sha256(projection)


def require_exact_keys(
    value: dict[str, Any],
    expected: Sequence[str] | frozenset[str],
    label: str,
) -> None:
    if set(value) != set(expected):
        raise SessionAuthorityError(f"{label} field set drift")


def fsync_directory(path: Path) -> None:
    descriptor = os.open(path, os.O_RDONLY)
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def reject_symlink_components(path: Path, label: str) -> None:
    absolute = path.absolute()
    current = Path(absolute.anchor)
    for component in absolute.parts[1:]:
        current = current / component
        try:
            metadata = current.lstat()
        except FileNotFoundError:
            return
        except OSError as error:
            raise SessionAuthorityError(f"{label} cannot inspect {current}: {error}") from error
        if stat.S_ISLNK(metadata.st_mode):
            raise SessionAuthorityError(f"{label} contains a symlink component: {current}")


def read_regular_exact(path: Path, label: str) -> tuple[Path, bytes]:
    reject_symlink_components(path, label)
    try:
        return CANDIDATE.read_regular_file(path, label)
    except CANDIDATE.CandidateAuthorityError as error:
        raise SessionAuthorityError(str(error)) from error


def canonical_existing_directory(path: Path, label: str) -> Path:
    reject_symlink_components(path, label)
    try:
        resolved = path.resolve(strict=True)
    except OSError as error:
        raise SessionAuthorityError(f"{label} cannot be resolved: {error}") from error
    if not resolved.is_dir():
        raise SessionAuthorityError(f"{label} must be a directory")
    return resolved


def exclusive_create_directory(path: Path, label: str) -> Path:
    parent = canonical_existing_directory(path.parent, f"{label} parent")
    target = parent / path.name
    reject_symlink_components(target, label)
    try:
        os.mkdir(target, 0o700)
        fsync_directory(parent)
    except OSError as error:
        raise SessionAuthorityError(f"{label} exclusive create failed: {error}") from error
    return target.resolve(strict=True)


def exclusive_write(path: Path, content: bytes, label: str) -> None:
    parent = canonical_existing_directory(path.parent, f"{label} parent")
    target = parent / path.name
    reject_symlink_components(target, label)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    try:
        descriptor = os.open(target, flags, 0o600)
        try:
            view = memoryview(content)
            while view:
                written = os.write(descriptor, view)
                if written <= 0:
                    raise OSError("short write")
                view = view[written:]
            os.fsync(descriptor)
        finally:
            os.close(descriptor)
        fsync_directory(parent)
    except OSError as error:
        raise SessionAuthorityError(f"{label} exclusive write failed: {error}") from error


def reserve_zero_byte_file(path: Path, label: str) -> int:
    parent = canonical_existing_directory(path.parent, f"{label} parent")
    target = parent / path.name
    reject_symlink_components(target, label)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    try:
        descriptor = os.open(target, flags, 0o600)
        os.fsync(descriptor)
        fsync_directory(parent)
        return descriptor
    except OSError as error:
        raise SessionAuthorityError(f"{label} reservation failed: {error}") from error


def finalize_reserved_descriptor(
    descriptor: int,
    path: Path,
    content: bytes,
    label: str,
) -> None:
    try:
        view = memoryview(content)
        while view:
            written = os.write(descriptor, view)
            if written <= 0:
                raise OSError("short write")
            view = view[written:]
        os.fsync(descriptor)
    except OSError as error:
        raise SessionAuthorityError(f"{label} finalization failed: {error}") from error
    finally:
        os.close(descriptor)
    fsync_directory(path.parent)


@contextlib.contextmanager
def exclusive_claim_lock(claim_path: Path) -> Iterator[None]:
    reject_symlink_components(claim_path, "session claim lock")
    flags = os.O_RDONLY
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    try:
        descriptor = os.open(claim_path, flags)
    except OSError as error:
        raise SessionAuthorityError(f"session claim lock cannot open: {error}") from error
    try:
        fcntl.flock(descriptor, fcntl.LOCK_EX)
        yield
    finally:
        fcntl.flock(descriptor, fcntl.LOCK_UN)
        os.close(descriptor)


@dataclasses.dataclass(frozen=True)
class CandidateContext:
    repository_root: Path
    manifest_path: Path
    story_manifest_path: Path | None
    manifest_bytes: bytes
    manifest: dict[str, Any]
    story_manifest_bytes: bytes
    story_manifest: dict[str, Any]
    semantic_verification_performed: bool


def load_session_policy() -> tuple[dict[str, Any], bytes]:
    _resolved, data = read_regular_exact(POLICY_PATH, "realtime session policy")
    if sha256_bytes(data) != EXPECTED_POLICY_RAW_SHA256:
        raise SessionAuthorityError("realtime session policy raw-byte drift")
    policy = strict_json_bytes(data, "realtime session policy")
    return policy, data


def validate_session_policy(policy: dict[str, Any], data: bytes | None = None) -> None:
    expected, expected_bytes = load_session_policy()
    if not strict_typed_equal(policy, expected):
        raise SessionAuthorityError("realtime session policy object drift")
    if data is not None and data != expected_bytes:
        raise SessionAuthorityError("realtime session policy byte drift")
    require_exact_keys(
        policy,
        {
            "schemaVersion",
            "canonicalization",
            "candidateAuthority",
            "evaluatorProducerAuthority",
            "sessionAuthority",
            "targetedCheckpointRoute",
            "storyPartRoute",
            "fullFlowExceptionRoute",
            "limitations",
        },
        "realtime session policy",
    )
    candidate = policy["candidateAuthority"]
    session = policy["sessionAuthority"]
    targeted = policy["targetedCheckpointRoute"]
    if (
        policy["schemaVersion"]
        != "gridworks.realtime-evaluation-session-policy.v1"
        or candidate["sourceCommit"]
        != "379e9800c81ca315976ab4c28d511664df6ab7ed"
        or candidate["candidateSha256"]
        != "sha256:373785e45a4485dfeded43466a5bff0f66de4a0c106c972262686e7a432cbdd6"
        or session["officialCommercialUX"] is not False
        or session["scoreBearingCaptureAllowed"] is not False
        or session["routeKinds"] != list(ALLOWED_ROUTE_KINDS)
        or session["maxAttempts"] != 3
        or session["claimStatus"] != "FINALIZED_BEFORE_ANY_ATTEMPT"
        or session["inputSnapshotPaths"] != [
            "inputs/candidate-manifest.json",
            "inputs/story-manifest.json",
        ]
        or session["claimCommitMarker"]
        != "SESSION_CLAIM_LAST_O_EXCL_FSYNC_FILE_AND_PARENT"
        or session["retryableOutcomes"] != list(RETRYABLE_OUTCOMES)
        or session["nonretryableOutcomes"] != list(NONRETRYABLE_OUTCOMES)
        or targeted["requiredFutureEventSignals"]
        != list(CANDIDATE.FUTURE_EVENT_SIGNALS)
    ):
        raise SessionAuthorityError("realtime session policy invariant drift")


def _candidate_manifest_story_bytes(
    repository_root: Path,
    manifest: dict[str, Any],
) -> bytes:
    row = next(
        (
            value
            for value in manifest.get("sourceAuthority", {}).get("files", [])
            if value.get("path") == STORY_MANIFEST_PATH
        ),
        None,
    )
    if not isinstance(row, dict):
        raise SessionAuthorityError("candidate manifest lacks bound story manifest row")
    object_id = row.get("gitObjectId")
    if not isinstance(object_id, str) or re.fullmatch(r"[0-9a-f]{40}", object_id) is None:
        raise SessionAuthorityError("bound story manifest Git object ID is invalid")
    try:
        data = CANDIDATE.run_git_command(
            repository_root,
            ["cat-file", "blob", "--", object_id],
            label="bound story manifest Git blob",
        )
    except CANDIDATE.CandidateAuthorityError as error:
        raise SessionAuthorityError(str(error)) from error
    if (
        sha256_bytes(data) != row.get("rawSha256")
        or len(data) != row.get("byteLength")
        or sha256_bytes(data)
        != manifest.get("storyAuthority", {}).get("storyManifestRawSha256")
    ):
        raise SessionAuthorityError("bound story manifest bytes disagree with candidate")
    return data


def load_fixed_candidate(
    repository_root: Path,
    candidate_path: Path,
    *,
    semantic_verify: bool,
    story_manifest_path: Path | None = None,
    godot_app_root: Path | None = None,
) -> CandidateContext:
    root = CANDIDATE.resolve_repository_root(repository_root)
    resolved, data = read_regular_exact(candidate_path, "realtime candidate manifest")
    policy, _policy_bytes = load_session_policy()
    fixed = policy["candidateAuthority"]
    if sha256_bytes(data) != fixed["candidateManifestRawSha256"]:
        raise SessionAuthorityError("candidate manifest raw bytes are not the reviewed candidate")
    manifest = strict_json_bytes(data, "realtime candidate manifest")
    unsigned = dict(manifest)
    submitted_sha = unsigned.pop("candidateSha256", None)
    if submitted_sha != canonical_sha256(unsigned):
        raise SessionAuthorityError("candidate manifest self-hash mismatch")
    if (
        submitted_sha != fixed["candidateSha256"]
        or manifest.get("sourceCommit") != fixed["sourceCommit"]
        or manifest.get("policySha256") != fixed["candidatePolicySha256"]
        or manifest.get("scoreBearingCaptureAllowed") is not False
        or manifest.get("officialCommercialUX") is not False
        or manifest.get("headlessExecutionAuthority", {}).get("executionSha256")
        != fixed["executionSha256"]
        or manifest.get("evaluatorProducerAuthority", {}).get("filesSha256")
        != fixed["producerFilesSha256"]
    ):
        raise SessionAuthorityError("candidate manifest reviewed authority projection drift")
    story_bytes = _candidate_manifest_story_bytes(root, manifest)
    resolved_story_path: Path | None = None
    if story_manifest_path is not None:
        resolved_story_path, snapshot_story_bytes = read_regular_exact(
            story_manifest_path,
            "bound story manifest snapshot",
        )
        if snapshot_story_bytes != story_bytes:
            raise SessionAuthorityError(
                "story manifest snapshot differs from candidate-bound Git bytes"
            )
    try:
        story = CANDIDATE.validate_story_manifest(story_bytes)
    except CANDIDATE.CandidateAuthorityError as error:
        raise SessionAuthorityError(str(error)) from error
    if sha256_bytes(story_bytes) != fixed["storyManifestRawSha256"]:
        raise SessionAuthorityError("reviewed story manifest raw hash drift")
    if semantic_verify:
        candidate_policy, candidate_policy_bytes = CANDIDATE.load_policy()
        engine_root = godot_app_root or CANDIDATE.default_godot_app(root)
        try:
            with CANDIDATE.isolated_managed_build(
                root,
                revision=fixed["sourceCommit"],
            ) as build:
                CANDIDATE.verify_manifest_against_reconstructed_authority(
                    manifest,
                    build,
                    engine_root,
                    candidate_policy,
                    candidate_policy_bytes,
                )
                if build.story_bytes != story_bytes:
                    raise SessionAuthorityError(
                        "semantic candidate rebuild story bytes drift"
                    )
        except CANDIDATE.CandidateAuthorityError as error:
            raise SessionAuthorityError(str(error)) from error
    return CandidateContext(
        root,
        resolved,
        resolved_story_path,
        data,
        manifest,
        story_bytes,
        story,
        semantic_verify,
    )


def bind_session_evaluator_authority(
    repository_root: Path,
    revision: str,
) -> dict[str, Any]:
    root = CANDIDATE.resolve_repository_root(repository_root)
    expected_script_dir = (
        root / "tools" / "commercial-ux" / "native"
    ).resolve(strict=True)
    if SCRIPT_DIR != expected_script_dir or Path(__file__).resolve(strict=True) != (
        expected_script_dir / "realtime-session-authority.py"
    ).resolve(strict=True):
        raise SessionAuthorityError(
            "running session evaluator is outside the candidate repository"
        )
    try:
        source_commit = CANDIDATE.resolve_source_commit(root, revision)
        entries = CANDIDATE.git_tree_entries(root, source_commit)
    except CANDIDATE.CandidateAuthorityError as error:
        raise SessionAuthorityError(str(error)) from error
    by_path = {
        path: (mode, object_type, object_id)
        for mode, object_type, object_id, path in entries
    }
    rows: list[dict[str, Any]] = []
    for path, role in SESSION_PRODUCER_PATH_ROLES:
        entry = by_path.get(path)
        if entry is None:
            raise SessionAuthorityError(
                f"session authority source commit lacks evaluator file: {path}"
            )
        mode, object_type, object_id = entry
        if mode not in {"100644", "100755"} or object_type != "blob":
            raise SessionAuthorityError(
                f"session evaluator authority is not a regular Git blob: {path}"
            )
        try:
            git_data = CANDIDATE.run_git_command(
                root,
                ["cat-file", "blob", "--", object_id],
                label=f"session evaluator Git blob {path}",
            )
        except CANDIDATE.CandidateAuthorityError as error:
            raise SessionAuthorityError(str(error)) from error
        _resolved, running_data = read_regular_exact(
            root / path,
            f"running session evaluator authority {path}",
        )
        if running_data != git_data:
            raise SessionAuthorityError(
                f"running session evaluator differs from source commit: {path}"
            )
        rows.append(CANDIDATE.GitBlob(path, mode, object_id, role, git_data).row())
    rows.sort(key=lambda row: row["path"])
    policy, _policy_bytes = load_session_policy()
    expected_paths = policy["evaluatorProducerAuthority"]["paths"]
    if [row["path"] for row in rows] != expected_paths:
        raise SessionAuthorityError("session evaluator path order differs from policy")
    return {
        "schemaVersion": SESSION_PRODUCER_SCHEMA,
        "sourceCommit": source_commit,
        "fileCount": len(rows),
        "files": rows,
        "filesSha256": canonical_sha256(rows),
        "runningFilesMatchGitBlobs": True,
        "gitCommandAuthority": CANDIDATE.bind_git_command_authority(root),
        "candidateSemanticVerifierDependencyBound": True,
        "semanticVerifierEntryPoint": (
            "verify_session_claim_against_reconstructed_authority"
        ),
        "structuralSchemasAuthority": (
            "STRUCTURAL_ONLY_NOT_SESSION_AUTHORITY"
        ),
    }


def _route_profile(manifest: dict[str, Any], profile_id: str) -> dict[str, Any]:
    profile = next(
        (
            value
            for value in manifest.get("routeProfiles", [])
            if value.get("profileId") == profile_id
        ),
        None,
    )
    if not isinstance(profile, dict):
        raise SessionAuthorityError(f"candidate lacks route profile {profile_id}")
    return profile


def reconstruct_route_binding(
    context: CandidateContext,
    route_kind: str,
    selector: str | None,
) -> dict[str, Any]:
    policy, _policy_bytes = load_session_policy()
    if route_kind == "TARGETED_CHECKPOINT":
        if selector not in policy["targetedCheckpointRoute"]["checkpointIds"]:
            raise SessionAuthorityError("targeted checkpoint selector is not authorized")
        profile = _route_profile(context.manifest, "TARGETED_CHECKPOINT_DEBUG")
        if (
            profile.get("routeKind") != "TARGETED_CHECKPOINT"
            or profile.get("availability") != "AVAILABLE_DIAGNOSTIC_ONLY"
        ):
            raise SessionAuthorityError("targeted checkpoint route is unavailable")
        checkpoint = next(
            (
                value
                for value in profile.get("checkpoints", [])
                if value.get("checkpointId") == selector
            ),
            None,
        )
        probe = next(
            (
                value
                for value in context.manifest.get(
                    "headlessExecutionAuthority", {}
                ).get("positiveCheckpointProbes", [])
                if value.get("checkpointId") == selector
            ),
            None,
        )
        if not isinstance(checkpoint, dict) or not isinstance(probe, dict):
            raise SessionAuthorityError("candidate lacks exact checkpoint/probe binding")
        future = context.manifest.get("futureEventStatusBar")
        if (
            not isinstance(future, dict)
            or future.get("requiredSignals")
            != policy["targetedCheckpointRoute"][
                "requiredFutureEventSignals"
            ]
            or future.get("headlessWiringStatus")
            != "EXACT_PACKAGE_TWO_CHECKPOINT_SCENE_LOAD_PASS"
            or future.get("nativeQualityStatus") != "NOT_OBSERVED"
        ):
            raise SessionAuthorityError("future-event status bar claim boundary drift")
        binding: dict[str, Any] = {
            "routeKind": route_kind,
            "candidateProfileId": profile["profileId"],
            "sessionProfileId": "TARGETED_CHECKPOINT_DEBUG",
            "availability": profile["availability"],
            "executionAuthorized": True,
            "selector": selector,
            "routeProfileSha256": canonical_sha256(profile),
            "checkpoint": checkpoint,
            "checkpointSha256": canonical_sha256(checkpoint),
            "candidateProbeReceipt": probe,
            "candidateProbeReceiptSha256": canonical_sha256(probe),
            "futureEventStatusBar": future,
            "nativePresentationObserved": False,
            "nativeQualityObserved": False,
            "authoredReachabilityOnly": False,
            "scoreBearingEvidence": False,
            "routeDisposition": "DIAGNOSTIC_ATTEMPT_AUTHORIZED",
        }
    elif route_kind == "STORY_PART_UNIT":
        if selector is None:
            raise SessionAuthorityError("story part route requires one selector")
        part = next(
            (
                value
                for value in context.story_manifest.get("parts", [])
                if value.get("selector") == selector
            ),
            None,
        )
        if not isinstance(part, dict):
            raise SessionAuthorityError("story part selector is not in the bound manifest")
        binding = {
            "routeKind": route_kind,
            "candidateProfileId": None,
            "sessionProfileId": "STORY_PART_UNIT",
            "availability": "AVAILABLE_CONTENT_UNIT_ONLY",
            "executionAuthorized": True,
            "selector": selector,
            "storyManifestRawSha256": sha256_bytes(context.story_manifest_bytes),
            "storyPart": part,
            "storyPartSha256": canonical_sha256(part),
            "nativePresentationObserved": False,
            "nativeQualityObserved": False,
            "authoredReachabilityOnly": True,
            "nativeReachabilityClaim": False,
            "scoreBearingEvidence": False,
            "routeDisposition": "CONTENT_UNIT_ATTEMPT_AUTHORIZED",
        }
    elif route_kind == "FULL_FLOW_EXCEPTION":
        if selector is not None:
            raise SessionAuthorityError("full-flow exception does not accept a selector")
        profile = _route_profile(context.manifest, "FULL_FLOW_EXCEPTION")
        expected = policy["fullFlowExceptionRoute"]
        if (
            profile.get("routeKind") != "FULL_FLOW_EXCEPTION"
            or profile.get("availability") != expected["availability"]
            or profile.get("scene") is not None
            or profile.get("allowedClaimPrefix") is not None
        ):
            raise SessionAuthorityError("full-flow exception candidate boundary drift")
        binding = {
            "routeKind": route_kind,
            "candidateProfileId": profile["profileId"],
            "sessionProfileId": "FULL_FLOW_EXCEPTION",
            "availability": profile["availability"],
            "executionAuthorized": False,
            "selector": None,
            "routeProfile": profile,
            "routeProfileSha256": canonical_sha256(profile),
            "nativePresentationObserved": False,
            "nativeQualityObserved": False,
            "authoredReachabilityOnly": False,
            "scoreBearingEvidence": False,
            "routeDisposition": "ROUTE_UNAVAILABLE_NO_EXECUTION",
        }
    else:
        raise SessionAuthorityError("session route kind is not authorized")
    binding["routeBindingSha256"] = self_hash(binding, "routeBindingSha256")
    return binding


def expected_diagnostic_output(
    session_id: str,
    candidate_sha256: str,
    route_binding: dict[str, Any],
) -> dict[str, Any] | None:
    if route_binding["executionAuthorized"] is False:
        return None
    if route_binding["routeKind"] == "TARGETED_CHECKPOINT":
        result = route_binding["candidateProbeReceipt"]
    elif route_binding["routeKind"] == "STORY_PART_UNIT":
        result = route_binding["storyPart"]
    else:
        raise SessionAuthorityError("executable route kind has no diagnostic output")
    return {
        "schemaVersion": DIAGNOSTIC_OUTPUT_SCHEMA,
        "sessionId": session_id,
        "candidateSha256": candidate_sha256,
        "routeBindingSha256": route_binding["routeBindingSha256"],
        "routeKind": route_binding["routeKind"],
        "officialCommercialUX": False,
        "scoreBearingCaptureAllowed": False,
        "result": result,
    }


def compose_session_claim(
    context: CandidateContext,
    evaluator_authority: dict[str, Any],
    session_root: Path,
    session_nonce: str,
    route_kind: str,
    selector: str | None,
) -> dict[str, Any]:
    if re.fullmatch(r"[0-9a-f]{64}", session_nonce) is None:
        raise SessionAuthorityError("session nonce must be 32 lowercase hex bytes")
    root = canonical_existing_directory(session_root, "session root")
    claim_path = root / "session-claim.json"
    input_root = root / "inputs"
    expected_candidate_path = input_root / "candidate-manifest.json"
    expected_story_path = input_root / "story-manifest.json"
    if context.manifest_path != expected_candidate_path:
        raise SessionAuthorityError(
            "session claim must consume its exact candidate manifest snapshot"
        )
    if context.story_manifest_path != expected_story_path:
        raise SessionAuthorityError(
            "session claim must consume its exact story manifest snapshot"
        )
    route = reconstruct_route_binding(context, route_kind, selector)
    policy, policy_bytes = load_session_policy()
    root_binding = sha256_bytes(str(root).encode("utf-8"))
    session_id = canonical_sha256({
        "schemaVersion": SESSION_CLAIM_SCHEMA,
        "sessionNonce": session_nonce,
        "sessionRootBindingSha256": root_binding,
        "candidateSha256": context.manifest["candidateSha256"],
        "candidateManifestRawSha256": sha256_bytes(context.manifest_bytes),
        "sessionPolicyRawSha256": sha256_bytes(policy_bytes),
        "sessionEvaluatorFilesSha256": evaluator_authority["filesSha256"],
        "routeBindingSha256": route["routeBindingSha256"],
    })
    expected_output = expected_diagnostic_output(
        session_id,
        context.manifest["candidateSha256"],
        route,
    )
    expected_bytes = (
        json_file_bytes(expected_output) if expected_output is not None else None
    )
    executable = route["executionAuthorized"] is True
    unavailable_terminal: dict[str, Any] | None = None
    if not executable:
        unavailable_terminal = {
            "schemaVersion": "gridworks.realtime-unavailable-route-terminal.v1",
            "sessionId": session_id,
            "routeBindingSha256": route["routeBindingSha256"],
            "outcome": "ROUTE_UNAVAILABLE_NO_EXECUTION",
            "producerStarted": False,
            "producerOutputReserved": False,
            "nextAttemptAllowed": False,
            "officialCommercialUX": False,
            "scoreBearingCaptureAllowed": False,
        }
        unavailable_terminal["unavailableRouteTerminalSha256"] = self_hash(
            unavailable_terminal,
            "unavailableRouteTerminalSha256",
        )
    attempts = [
        {
            "attemptOrdinal": ordinal,
            "attemptId": canonical_sha256({
                "sessionId": session_id,
                "routeBindingSha256": route["routeBindingSha256"],
                "attemptOrdinal": ordinal,
            }),
            "attemptRoot": str(root / "attempts" / f"{ordinal:02d}"),
            "startReceiptPath": str(
                root / "attempts" / f"{ordinal:02d}" / "start-receipt.json"
            ),
            "outputPath": str(
                root / "attempts" / f"{ordinal:02d}" / "diagnostic-output.json"
            ),
            "terminalReceiptPath": str(
                root / "attempts" / f"{ordinal:02d}" / "terminal-receipt.json"
            ),
        }
        for ordinal in range(1, 4)
    ] if executable else []
    claim: dict[str, Any] = {
        "schemaVersion": SESSION_CLAIM_SCHEMA,
        "canonicalization": CANONICALIZATION,
        "status": "FINALIZED_BEFORE_ANY_ATTEMPT",
        "sessionPolicyRawSha256": sha256_bytes(policy_bytes),
        "sessionClaimSchemaRawSha256": sha256_bytes(
            read_regular_exact(CLAIM_SCHEMA_PATH, "session claim schema")[1]
        ),
        "sessionAuthoritySourceCommit": evaluator_authority["sourceCommit"],
        "sessionEvaluatorAuthority": evaluator_authority,
        "sessionId": session_id,
        "sessionNonce": session_nonce,
        "canonicalSessionRoot": str(root),
        "canonicalClaimPath": str(claim_path),
        "sessionRootBindingSha256": root_binding,
        "candidateAuthority": {
            "candidateManifestPath": str(context.manifest_path),
            "candidateManifestRawSha256": sha256_bytes(context.manifest_bytes),
            "candidateSha256": context.manifest["candidateSha256"],
            "candidateSourceCommit": context.manifest["sourceCommit"],
            "candidateExecutionSha256": context.manifest[
                "headlessExecutionAuthority"
            ]["executionSha256"],
            "candidatePackageTreeSha256": context.manifest[
                "packageAuthority"
            ]["treeSha256"],
            "candidateEvaluatorProducerFilesSha256": context.manifest[
                "evaluatorProducerAuthority"
            ]["filesSha256"],
            "storyManifestPath": str(context.story_manifest_path),
            "storyManifestRawSha256": sha256_bytes(context.story_manifest_bytes),
            "semanticVerificationAtClaimCreation": True,
            "semanticVerifierEntryPoint": (
                "verify_manifest_against_reconstructed_authority"
            ),
        },
        "routeBinding": route,
        "routeDisposition": route["routeDisposition"],
        "unavailableRouteTerminal": unavailable_terminal,
        "expectedAttemptOutput": (
            {
                "rawSha256": sha256_bytes(expected_bytes),
                "canonicalSha256": canonical_sha256(expected_output),
                "byteLength": len(expected_bytes),
            }
            if expected_bytes is not None
            else None
        ),
        "attemptPolicy": {
            "maxAttempts": 3 if executable else 0,
            "attemptOutputContract": policy["sessionAuthority"][
                "attemptOutputContract"
            ],
            "retryableOutcomes": list(RETRYABLE_OUTCOMES),
            "nonretryableOutcomes": list(NONRETRYABLE_OUTCOMES),
            "producerOutputReservedForUnavailableRoute": False,
        },
        "attempts": attempts,
        "finalizationAuthority": {
            "inputSnapshotDirectory": str(input_root),
            "candidateSnapshotExclusiveWriteFsyncCompleted": True,
            "storySnapshotExclusiveWriteFsyncCompleted": True,
            "inputDirectoryFsyncCompleted": True,
            "claimPathAbsentUntilAllInputsFinalized": True,
            "claimFileIsLastCommitMarker": True,
            "claimWriteMode": "O_EXCL_FSYNC_FILE_AND_PARENT",
        },
        "officialCommercialUX": False,
        "scoreBearingCaptureAllowed": False,
        "claimProducerExecutionAttested": False,
        "freshnessAuthority": "LOCAL_OS_RANDOM_NON_SCORE_ONLY",
        "limitations": list(policy["limitations"]),
    }
    claim["sessionClaimSha256"] = self_hash(claim, "sessionClaimSha256")
    return claim


def create_session_claim(
    context: CandidateContext,
    session_root: Path,
    route_kind: str,
    selector: str | None,
    *,
    session_authority_revision: str = "HEAD",
) -> tuple[Path, dict[str, Any]]:
    if not context.semantic_verification_performed:
        raise SessionAuthorityError(
            "session claim creation requires reconstructed candidate verification"
        )
    policy, policy_bytes = load_session_policy()
    validate_session_policy(policy, policy_bytes)
    evaluator = bind_session_evaluator_authority(
        context.repository_root,
        session_authority_revision,
    )
    root = exclusive_create_directory(session_root, "session root")
    input_root = exclusive_create_directory(root / "inputs", "session input root")
    candidate_snapshot_path = input_root / "candidate-manifest.json"
    story_snapshot_path = input_root / "story-manifest.json"
    exclusive_write(
        candidate_snapshot_path,
        context.manifest_bytes,
        "candidate manifest snapshot",
    )
    exclusive_write(
        story_snapshot_path,
        context.story_manifest_bytes,
        "story manifest snapshot",
    )
    fsync_directory(input_root)
    snapshot_context = dataclasses.replace(
        context,
        manifest_path=candidate_snapshot_path,
        story_manifest_path=story_snapshot_path,
    )
    nonce = secrets.token_bytes(32).hex()
    claim = compose_session_claim(
        snapshot_context,
        evaluator,
        root,
        nonce,
        route_kind,
        selector,
    )
    claim_path = root / "session-claim.json"
    exclusive_write(
        claim_path,
        json_file_bytes(claim),
        "session claim",
    )
    return claim_path, claim


def _validate_session_root_inventory(claim: dict[str, Any]) -> None:
    root = Path(claim["canonicalSessionRoot"])
    root_names = {path.name for path in root.iterdir()}
    base_names = {"inputs", "session-claim.json"}
    attempts_root = root / "attempts"
    attempts_exists = os.path.lexists(attempts_root)
    expected_root_names = base_names | ({"attempts"} if attempts_exists else set())
    if root_names != expected_root_names:
        raise SessionAuthorityError("session root inventory drift")
    if claim["routeBinding"]["executionAuthorized"] is False:
        if attempts_exists:
            raise SessionAuthorityError(
                "unavailable route has an attempts or producer root"
            )
        return
    if not attempts_exists:
        return
    canonical_existing_directory(attempts_root, "attempts root")
    attempt_names = sorted(path.name for path in attempts_root.iterdir())
    if not attempt_names:
        raise SessionAuthorityError("attempts root is an incomplete tombstone")
    if attempt_names != [f"{ordinal:02d}" for ordinal in range(1, len(attempt_names) + 1)]:
        raise SessionAuthorityError("attempts root ordinal inventory drift")
    if len(attempt_names) > claim["attemptPolicy"]["maxAttempts"]:
        raise SessionAuthorityError("attempts root exceeds the fixed maximum")
    for name in attempt_names:
        attempt_root = canonical_existing_directory(
            attempts_root / name,
            f"attempt root {name}",
        )
        file_names = {path.name for path in attempt_root.iterdir()}
        allowed = {
            "diagnostic-output.json",
            "start-receipt.json",
        }
        if file_names not in (allowed, allowed | {"terminal-receipt.json"}):
            raise SessionAuthorityError(f"attempt root {name} inventory drift")


def verify_session_claim_against_reconstructed_authority(
    repository_root: Path,
    claim_path: Path,
) -> tuple[CandidateContext, dict[str, Any], bytes]:
    resolved_claim, claim_bytes = read_regular_exact(claim_path, "session claim")
    claim = strict_json_bytes(claim_bytes, "session claim")
    if claim_bytes != json_file_bytes(claim):
        raise SessionAuthorityError("session claim is not canonical JSON file bytes")
    if claim.get("sessionClaimSha256") != self_hash(
        claim,
        "sessionClaimSha256",
    ):
        raise SessionAuthorityError("session claim self-hash mismatch")
    if claim.get("canonicalClaimPath") != str(resolved_claim):
        raise SessionAuthorityError("session claim path differs from bound path")
    root_value = claim.get("canonicalSessionRoot")
    if not isinstance(root_value, str):
        raise SessionAuthorityError("session claim root is missing")
    root = canonical_existing_directory(Path(root_value), "bound session root")
    if resolved_claim.parent != root:
        raise SessionAuthorityError("session claim is outside its bound session root")
    if claim.get("sessionRootBindingSha256") != sha256_bytes(
        str(root).encode("utf-8")
    ):
        raise SessionAuthorityError("session root binding mismatch")
    policy, policy_bytes = load_session_policy()
    validate_session_policy(policy, policy_bytes)
    candidate_value = claim.get("candidateAuthority")
    if not isinstance(candidate_value, dict):
        raise SessionAuthorityError("session claim candidate authority is missing")
    candidate_path_value = candidate_value.get("candidateManifestPath")
    if not isinstance(candidate_path_value, str):
        raise SessionAuthorityError("session claim candidate path is missing")
    story_path_value = candidate_value.get("storyManifestPath")
    if not isinstance(story_path_value, str):
        raise SessionAuthorityError("session claim story snapshot path is missing")
    input_root = root / "inputs"
    if (
        Path(candidate_path_value) != input_root / "candidate-manifest.json"
        or Path(story_path_value) != input_root / "story-manifest.json"
    ):
        raise SessionAuthorityError("session input snapshot path binding drift")
    canonical_input_root = canonical_existing_directory(
        input_root,
        "session input snapshot root",
    )
    if sorted(path.name for path in canonical_input_root.iterdir()) != [
        "candidate-manifest.json",
        "story-manifest.json",
    ]:
        raise SessionAuthorityError("session input snapshot directory field set drift")
    context = load_fixed_candidate(
        repository_root,
        Path(candidate_path_value),
        semantic_verify=False,
        story_manifest_path=Path(story_path_value),
    )
    source_commit = claim.get("sessionAuthoritySourceCommit")
    if not isinstance(source_commit, str):
        raise SessionAuthorityError("session evaluator source commit is missing")
    evaluator = bind_session_evaluator_authority(repository_root, source_commit)
    route_value = claim.get("routeBinding")
    if not isinstance(route_value, dict):
        raise SessionAuthorityError("session route binding is missing")
    expected = compose_session_claim(
        context,
        evaluator,
        root,
        claim.get("sessionNonce"),
        route_value.get("routeKind"),
        route_value.get("selector"),
    )
    if not strict_typed_equal(claim, expected):
        raise SessionAuthorityError(
            "session claim differs from reconstructed candidate, route, policy, or paths"
        )
    _validate_session_root_inventory(claim)
    _resolved_again, claim_bytes_again = read_regular_exact(
        resolved_claim,
        "session claim post-verification",
    )
    if claim_bytes_again != claim_bytes:
        raise SessionAuthorityError("session claim changed during verification")
    return context, claim, claim_bytes


def _attempt_for(claim: dict[str, Any], attempt_ordinal: int) -> dict[str, Any]:
    if type(attempt_ordinal) is not int:
        raise SessionAuthorityError("attempt ordinal must be an exact integer")
    attempt = next(
        (
            value
            for value in claim.get("attempts", [])
            if value.get("attemptOrdinal") == attempt_ordinal
        ),
        None,
    )
    if not isinstance(attempt, dict):
        if claim.get("routeDisposition") == "ROUTE_UNAVAILABLE_NO_EXECUTION":
            raise SessionAuthorityError(
                "full-flow exception has no executable attempt or producer output"
            )
        raise SessionAuthorityError("attempt ordinal is outside the fixed session plan")
    return attempt


def _expected_output_bytes(
    claim: dict[str, Any],
) -> tuple[dict[str, Any], bytes]:
    output = expected_diagnostic_output(
        claim["sessionId"],
        claim["candidateAuthority"]["candidateSha256"],
        claim["routeBinding"],
    )
    if output is None:
        raise SessionAuthorityError("unavailable route has no diagnostic output")
    data = json_file_bytes(output)
    expected = claim.get("expectedAttemptOutput")
    if expected != {
        "rawSha256": sha256_bytes(data),
        "canonicalSha256": canonical_sha256(output),
        "byteLength": len(data),
    }:
        raise SessionAuthorityError("claim expected diagnostic output binding drift")
    return output, data


def _read_canonical_object(path: Path, label: str) -> tuple[bytes, dict[str, Any]]:
    _resolved, data = read_regular_exact(path, label)
    value = strict_json_bytes(data, label)
    if data != json_file_bytes(value):
        raise SessionAuthorityError(f"{label} is not canonical JSON file bytes")
    return data, value


def _validate_terminal_self_hash(terminal: dict[str, Any]) -> None:
    if terminal.get("evaluationAttemptTerminalSha256") != self_hash(
        terminal,
        "evaluationAttemptTerminalSha256",
    ):
        raise SessionAuthorityError("attempt terminal receipt self-hash mismatch")


def build_attempt_start_receipt(
    claim: dict[str, Any],
    claim_bytes: bytes,
    attempt: dict[str, Any],
    predecessor_terminal: dict[str, Any] | None,
    predecessor_terminal_bytes: bytes | None,
) -> dict[str, Any]:
    start: dict[str, Any] = {
        "schemaVersion": ATTEMPT_START_SCHEMA,
        "canonicalization": CANONICALIZATION,
        "attemptStartSchemaRawSha256": sha256_bytes(
            read_regular_exact(START_SCHEMA_PATH, "attempt start schema")[1]
        ),
        "sessionPolicyRawSha256": claim["sessionPolicyRawSha256"],
        "sessionId": claim["sessionId"],
        "sessionClaimSha256": claim["sessionClaimSha256"],
        "sessionClaimRawSha256": sha256_bytes(claim_bytes),
        "candidateSha256": claim["candidateAuthority"]["candidateSha256"],
        "routeBindingSha256": claim["routeBinding"]["routeBindingSha256"],
        "routeKind": claim["routeBinding"]["routeKind"],
        "attemptOrdinal": attempt["attemptOrdinal"],
        "attemptId": attempt["attemptId"],
        "canonicalAttemptRoot": attempt["attemptRoot"],
        "canonicalStartReceiptPath": attempt["startReceiptPath"],
        "canonicalOutputPath": attempt["outputPath"],
        "canonicalTerminalReceiptPath": attempt["terminalReceiptPath"],
        "predecessorTerminalReceiptSha256": (
            predecessor_terminal["evaluationAttemptTerminalSha256"]
            if predecessor_terminal is not None
            else None
        ),
        "predecessorTerminalReceiptRawSha256": (
            sha256_bytes(predecessor_terminal_bytes)
            if predecessor_terminal_bytes is not None
            else None
        ),
        "expectedOutputRawSha256": claim["expectedAttemptOutput"]["rawSha256"],
        "reservationAuthority": {
            "attemptRootExclusiveCreateCompleted": True,
            "outputPathAbsentBeforeReservation": True,
            "outputZeroByteReservationCompleted": True,
            "outputReservationFsyncCompleted": True,
            "startReceiptPathAbsentBeforeExclusiveWrite": True,
            "startReceiptExclusiveWriteFsyncCompleted": True,
            "producerStartedAfterReceipt": False,
        },
        "officialCommercialUX": False,
        "scoreBearingCaptureAllowed": False,
    }
    start["evaluationAttemptStartSha256"] = self_hash(
        start,
        "evaluationAttemptStartSha256",
    )
    return start


def _classify_output(
    claim: dict[str, Any],
    output_bytes: bytes,
) -> tuple[str, bool, str | None]:
    _expected_value, expected_bytes = _expected_output_bytes(claim)
    if len(output_bytes) == 0:
        return "PRODUCER_NO_OUTPUT", True, None
    if output_bytes == expected_bytes:
        return "SUCCESS", False, canonical_sha256(_expected_value)

    def reject_nonfinite(value: str) -> None:
        raise ValueError(f"non-JSON numeric token {value}")

    try:
        decoded = output_bytes.decode("utf-8")
        parsed = json.loads(decoded, parse_constant=reject_nonfinite)
    except (UnicodeError, json.JSONDecodeError, ValueError):
        return "TRANSPORT_FAILURE", True, None
    canonical_output_sha256 = canonical_sha256(parsed)
    return "INTEGRITY_FAILURE", False, canonical_output_sha256


def build_attempt_terminal_receipt(
    claim: dict[str, Any],
    claim_bytes: bytes,
    attempt: dict[str, Any],
    start: dict[str, Any],
    start_bytes: bytes,
    output_bytes: bytes,
) -> dict[str, Any]:
    outcome, retry, output_canonical_sha256 = _classify_output(
        claim,
        output_bytes,
    )
    next_attempt_allowed = (
        retry
        and attempt["attemptOrdinal"] < claim["attemptPolicy"]["maxAttempts"]
    )
    terminal: dict[str, Any] = {
        "schemaVersion": ATTEMPT_TERMINAL_SCHEMA,
        "canonicalization": CANONICALIZATION,
        "attemptTerminalSchemaRawSha256": sha256_bytes(
            read_regular_exact(TERMINAL_SCHEMA_PATH, "attempt terminal schema")[1]
        ),
        "sessionPolicyRawSha256": claim["sessionPolicyRawSha256"],
        "sessionId": claim["sessionId"],
        "sessionClaimSha256": claim["sessionClaimSha256"],
        "sessionClaimRawSha256": sha256_bytes(claim_bytes),
        "candidateSha256": claim["candidateAuthority"]["candidateSha256"],
        "routeBindingSha256": claim["routeBinding"]["routeBindingSha256"],
        "routeKind": claim["routeBinding"]["routeKind"],
        "attemptOrdinal": attempt["attemptOrdinal"],
        "attemptId": attempt["attemptId"],
        "evaluationAttemptStartSha256": start["evaluationAttemptStartSha256"],
        "attemptStartReceiptRawSha256": sha256_bytes(start_bytes),
        "canonicalOutputPath": attempt["outputPath"],
        "outputRawSha256": sha256_bytes(output_bytes),
        "outputCanonicalSha256": output_canonical_sha256,
        "outputByteLength": len(output_bytes),
        "expectedOutputRawSha256": claim["expectedAttemptOutput"]["rawSha256"],
        "outcome": outcome,
        "outcomeRetryable": retry,
        "nextAttemptAllowed": next_attempt_allowed,
        "canonicalTerminalReceiptPath": attempt["terminalReceiptPath"],
        "terminalizationAuthority": {
            "terminalPathAbsentBeforeReservation": True,
            "terminalZeroByteReservedBeforeOutputRead": True,
            "terminalReceiptFinalizedWithFsync": True,
            "callerSuppliedOutcomeAccepted": False,
        },
        "officialCommercialUX": False,
        "scoreBearingCaptureAllowed": False,
    }
    terminal["evaluationAttemptTerminalSha256"] = self_hash(
        terminal,
        "evaluationAttemptTerminalSha256",
    )
    return terminal


def _validate_start_receipt(
    claim: dict[str, Any],
    claim_bytes: bytes,
    attempt: dict[str, Any],
    predecessor: tuple[bytes, dict[str, Any]] | None,
) -> tuple[bytes, dict[str, Any]]:
    start_path = Path(attempt["startReceiptPath"])
    start_bytes, start = _read_canonical_object(start_path, "attempt start receipt")
    expected = build_attempt_start_receipt(
        claim,
        claim_bytes,
        attempt,
        predecessor[1] if predecessor is not None else None,
        predecessor[0] if predecessor is not None else None,
    )
    if not strict_typed_equal(start, expected):
        raise SessionAuthorityError("attempt start receipt reconstruction mismatch")
    return start_bytes, start


def _read_and_validate_terminal(
    claim: dict[str, Any],
    claim_bytes: bytes,
    attempt: dict[str, Any],
    predecessor: tuple[bytes, dict[str, Any]] | None,
) -> tuple[bytes, dict[str, Any]]:
    start_bytes, start = _validate_start_receipt(
        claim,
        claim_bytes,
        attempt,
        predecessor,
    )
    terminal_path = Path(attempt["terminalReceiptPath"])
    terminal_bytes, terminal = _read_canonical_object(
        terminal_path,
        "attempt terminal receipt",
    )
    _validate_terminal_self_hash(terminal)
    _resolved_output, output_bytes = read_regular_exact(
        Path(attempt["outputPath"]),
        "attempt diagnostic output",
    )
    expected = build_attempt_terminal_receipt(
        claim,
        claim_bytes,
        attempt,
        start,
        start_bytes,
        output_bytes,
    )
    if not strict_typed_equal(terminal, expected):
        raise SessionAuthorityError("attempt terminal receipt reconstruction mismatch")
    return terminal_bytes, terminal


def _validated_predecessor_for(
    claim: dict[str, Any],
    claim_bytes: bytes,
    attempt_ordinal: int,
) -> tuple[bytes, dict[str, Any]] | None:
    predecessor: tuple[bytes, dict[str, Any]] | None = None
    for ordinal in range(1, attempt_ordinal):
        predecessor = _read_and_validate_terminal(
            claim,
            claim_bytes,
            _attempt_for(claim, ordinal),
            predecessor,
        )
        if predecessor[1]["nextAttemptAllowed"] is not True:
            raise SessionAuthorityError(
                "predecessor terminal does not authorize retry"
            )
    return predecessor


def _ensure_attempt_parent(
    claim: dict[str, Any],
    attempt_ordinal: int,
) -> Path:
    root = Path(claim["canonicalSessionRoot"])
    parent = root / "attempts"
    if os.path.lexists(parent):
        if attempt_ordinal == 1:
            raise SessionAuthorityError(
                "attempts root was prepopulated before the first reservation"
            )
        return canonical_existing_directory(parent, "attempts root")
    if attempt_ordinal != 1:
        raise SessionAuthorityError("attempts root is missing after predecessor")
    return exclusive_create_directory(parent, "attempts root")


def reserve_attempt(
    repository_root: Path,
    claim_path: Path,
    attempt_ordinal: int,
) -> tuple[Path, dict[str, Any]]:
    with exclusive_claim_lock(claim_path):
        _context, claim, claim_bytes = (
            verify_session_claim_against_reconstructed_authority(
                repository_root,
                claim_path,
            )
        )
        if claim["routeBinding"]["executionAuthorized"] is not True:
            raise SessionAuthorityError(
                "unavailable full-flow route cannot reserve an attempt or output"
            )
        attempt = _attempt_for(claim, attempt_ordinal)
        predecessor = _validated_predecessor_for(
            claim,
            claim_bytes,
            attempt_ordinal,
        )
        _ensure_attempt_parent(claim, attempt_ordinal)
        attempt_root = exclusive_create_directory(
            Path(attempt["attemptRoot"]),
            "attempt root",
        )
        if attempt_root != Path(attempt["attemptRoot"]):
            raise SessionAuthorityError("attempt root canonical path drift")
        output_descriptor = reserve_zero_byte_file(
            Path(attempt["outputPath"]),
            "attempt output",
        )
        os.close(output_descriptor)
        start = build_attempt_start_receipt(
            claim,
            claim_bytes,
            attempt,
            predecessor[1] if predecessor is not None else None,
            predecessor[0] if predecessor is not None else None,
        )
        start_path = Path(attempt["startReceiptPath"])
        exclusive_write(
            start_path,
            json_file_bytes(start),
            "attempt start receipt",
        )
        return start_path, start


def write_expected_attempt_output(
    repository_root: Path,
    claim_path: Path,
    attempt_ordinal: int,
) -> Path:
    with exclusive_claim_lock(claim_path):
        _context, claim, claim_bytes = (
            verify_session_claim_against_reconstructed_authority(
                repository_root,
                claim_path,
            )
        )
        attempt = _attempt_for(claim, attempt_ordinal)
        predecessor = _validated_predecessor_for(
            claim,
            claim_bytes,
            attempt_ordinal,
        )
        _validate_start_receipt(
            claim,
            claim_bytes,
            attempt,
            predecessor,
        )
        output_path = Path(attempt["outputPath"])
        if os.path.lexists(Path(attempt["terminalReceiptPath"])):
            raise SessionAuthorityError(
                "attempt output cannot be written after terminal reservation"
            )
        resolved, existing = read_regular_exact(output_path, "attempt output reservation")
        if existing != b"":
            raise SessionAuthorityError("attempt output reservation is no longer empty")
        _expected_value, expected_bytes = _expected_output_bytes(claim)
        flags = os.O_WRONLY
        if hasattr(os, "O_NOFOLLOW"):
            flags |= os.O_NOFOLLOW
        descriptor = os.open(resolved, flags)
        try:
            before = os.fstat(descriptor)
            if not stat.S_ISREG(before.st_mode) or before.st_size != 0:
                raise SessionAuthorityError(
                    "attempt output reservation changed before producer write"
                )
            view = memoryview(expected_bytes)
            while view:
                written = os.write(descriptor, view)
                if written <= 0:
                    raise OSError("short write")
                view = view[written:]
            os.fsync(descriptor)
        finally:
            os.close(descriptor)
        fsync_directory(resolved.parent)
        return resolved


def finalize_attempt(
    repository_root: Path,
    claim_path: Path,
    attempt_ordinal: int,
) -> tuple[Path, dict[str, Any]]:
    with exclusive_claim_lock(claim_path):
        _context, claim, claim_bytes = (
            verify_session_claim_against_reconstructed_authority(
                repository_root,
                claim_path,
            )
        )
        attempt = _attempt_for(claim, attempt_ordinal)
        predecessor = _validated_predecessor_for(
            claim,
            claim_bytes,
            attempt_ordinal,
        )
        start_bytes, start = _validate_start_receipt(
            claim,
            claim_bytes,
            attempt,
            predecessor,
        )
        terminal_path = Path(attempt["terminalReceiptPath"])
        descriptor = reserve_zero_byte_file(
            terminal_path,
            "attempt terminal receipt",
        )
        try:
            _resolved_output, output_bytes = read_regular_exact(
                Path(attempt["outputPath"]),
                "attempt diagnostic output",
            )
            terminal = build_attempt_terminal_receipt(
                claim,
                claim_bytes,
                attempt,
                start,
                start_bytes,
                output_bytes,
            )
            receipt_bytes = json_file_bytes(terminal)
            active_descriptor = descriptor
            descriptor = -1
            finalize_reserved_descriptor(
                active_descriptor,
                terminal_path,
                receipt_bytes,
                "attempt terminal receipt",
            )
        finally:
            if descriptor >= 0:
                os.close(descriptor)
                fsync_directory(terminal_path.parent)
        return terminal_path, terminal


def _verify_session_state_without_lock(
    repository_root: Path,
    claim_path: Path,
) -> dict[str, Any]:
    _context, claim, claim_bytes = (
        verify_session_claim_against_reconstructed_authority(
            repository_root,
            claim_path,
        )
    )
    if claim["routeBinding"]["executionAuthorized"] is False:
        root = Path(claim["canonicalSessionRoot"])
        if os.path.lexists(root / "attempts"):
            raise SessionAuthorityError(
                "unavailable full-flow route unexpectedly has an attempts root"
            )
        return {
            "sessionId": claim["sessionId"],
            "routeDisposition": claim["routeDisposition"],
            "attemptState": "ROUTE_UNAVAILABLE_NO_EXECUTION",
            "terminalOutcome": claim["unavailableRouteTerminal"]["outcome"],
        }
    predecessor = None
    states: list[dict[str, Any]] = []
    gap_seen = False
    attempts_root = Path(claim["canonicalSessionRoot"]) / "attempts"
    attempts_root_exists = os.path.lexists(attempts_root)
    if attempts_root_exists:
        canonical_existing_directory(attempts_root, "attempts root")
    for attempt in claim["attempts"]:
        attempt_root = Path(attempt["attemptRoot"])
        if not os.path.lexists(attempt_root):
            if attempts_root_exists and not states:
                raise SessionAuthorityError(
                    "attempts root exists without the first reserved attempt"
                )
            gap_seen = True
            states.append({
                "attemptOrdinal": attempt["attemptOrdinal"],
                "state": "NOT_RESERVED",
            })
            continue
        if gap_seen:
            raise SessionAuthorityError("attempt chain contains an ordinal gap")
        canonical_existing_directory(attempt_root, "attempt root")
        terminal_path = Path(attempt["terminalReceiptPath"])
        expected_names = {
            "diagnostic-output.json",
            "start-receipt.json",
        }
        if os.path.lexists(terminal_path):
            expected_names.add("terminal-receipt.json")
        if {path.name for path in attempt_root.iterdir()} != expected_names:
            raise SessionAuthorityError("attempt root file set drift")
        start_bytes, _start = _validate_start_receipt(
            claim,
            claim_bytes,
            attempt,
            predecessor,
        )
        if not os.path.lexists(terminal_path):
            states.append({
                "attemptOrdinal": attempt["attemptOrdinal"],
                "state": "STARTED_NOT_TERMINAL",
                "startReceiptRawSha256": sha256_bytes(start_bytes),
            })
            gap_seen = True
            continue
        terminal_bytes, terminal = _read_and_validate_terminal(
            claim,
            claim_bytes,
            attempt,
            predecessor,
        )
        states.append({
            "attemptOrdinal": attempt["attemptOrdinal"],
            "state": "TERMINAL",
            "outcome": terminal["outcome"],
            "terminalReceiptRawSha256": sha256_bytes(terminal_bytes),
        })
        predecessor = (terminal_bytes, terminal)
        if terminal["nextAttemptAllowed"] is False:
            gap_seen = True
    return {
        "sessionId": claim["sessionId"],
        "routeDisposition": claim["routeDisposition"],
        "attemptState": "APPEND_ONLY_CHAIN",
        "attempts": states,
    }


def verify_session_state(
    repository_root: Path,
    claim_path: Path,
) -> dict[str, Any]:
    with exclusive_claim_lock(claim_path):
        return _verify_session_state_without_lock(repository_root, claim_path)


def _add_repository_and_claim_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument(
        "--repository-root",
        type=Path,
        default=DEFAULT_REPOSITORY_ROOT,
    )
    parser.add_argument("--session-claim", type=Path, required=True)


def _add_create_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument(
        "--repository-root",
        type=Path,
        default=DEFAULT_REPOSITORY_ROOT,
    )
    parser.add_argument("--candidate-manifest", type=Path, required=True)
    parser.add_argument("--session-root", type=Path, required=True)
    parser.add_argument(
        "--session-authority-revision",
        default="HEAD",
    )
    parser.add_argument("--godot-app-root", type=Path)


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Create and verify non-score realtime diagnostic session receipts."
        )
    )
    commands = parser.add_subparsers(dest="command", required=True)

    create = commands.add_parser("create-session")
    routes = create.add_subparsers(dest="route_command", required=True)
    targeted = routes.add_parser("targeted-checkpoint")
    _add_create_arguments(targeted)
    targeted.add_argument(
        "--checkpoint",
        required=True,
        choices=("A1_NORMAL_READY", "A1_CONSTRUCTION_DUE_1M"),
    )
    story = routes.add_parser("story-part-unit")
    _add_create_arguments(story)
    story.add_argument("--story-part", required=True)
    full = routes.add_parser("full-flow-exception")
    _add_create_arguments(full)

    verify_claim = commands.add_parser("verify-claim")
    _add_repository_and_claim_arguments(verify_claim)
    reserve = commands.add_parser("reserve-attempt")
    _add_repository_and_claim_arguments(reserve)
    reserve.add_argument("--attempt", type=int, required=True)
    write_expected = commands.add_parser("write-expected-output")
    _add_repository_and_claim_arguments(write_expected)
    write_expected.add_argument("--attempt", type=int, required=True)
    finalize = commands.add_parser("finalize-attempt")
    _add_repository_and_claim_arguments(finalize)
    finalize.add_argument("--attempt", type=int, required=True)
    verify = commands.add_parser("verify-session")
    _add_repository_and_claim_arguments(verify)
    return parser


def _print_result(value: dict[str, Any]) -> None:
    print(
        json.dumps(
            value,
            ensure_ascii=False,
            allow_nan=False,
            sort_keys=True,
            separators=(",", ":"),
        )
    )


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(argv)
    try:
        if args.command == "create-session":
            context = load_fixed_candidate(
                args.repository_root,
                args.candidate_manifest,
                semantic_verify=True,
                godot_app_root=args.godot_app_root,
            )
            if args.route_command == "targeted-checkpoint":
                route_kind = "TARGETED_CHECKPOINT"
                selector = args.checkpoint
            elif args.route_command == "story-part-unit":
                route_kind = "STORY_PART_UNIT"
                selector = args.story_part
            elif args.route_command == "full-flow-exception":
                route_kind = "FULL_FLOW_EXCEPTION"
                selector = None
            else:
                raise SessionAuthorityError("unknown session route command")
            claim_path, claim = create_session_claim(
                context,
                args.session_root,
                route_kind,
                selector,
                session_authority_revision=args.session_authority_revision,
            )
            _print_result({
                "claimPath": str(claim_path),
                "sessionId": claim["sessionId"],
                "status": claim["status"],
                "routeKind": claim["routeBinding"]["routeKind"],
                "routeDisposition": claim["routeDisposition"],
                "officialCommercialUX": False,
                "scoreBearingCaptureAllowed": False,
            })
        elif args.command == "verify-claim":
            _context, claim, claim_bytes = (
                verify_session_claim_against_reconstructed_authority(
                    args.repository_root,
                    args.session_claim,
                )
            )
            _print_result({
                "sessionId": claim["sessionId"],
                "sessionClaimRawSha256": sha256_bytes(claim_bytes),
                "status": claim["status"],
                "routeDisposition": claim["routeDisposition"],
            })
        elif args.command == "reserve-attempt":
            start_path, start = reserve_attempt(
                args.repository_root,
                args.session_claim,
                args.attempt,
            )
            _print_result({
                "startReceiptPath": str(start_path),
                "attemptId": start["attemptId"],
                "attemptOrdinal": start["attemptOrdinal"],
            })
        elif args.command == "write-expected-output":
            output_path = write_expected_attempt_output(
                args.repository_root,
                args.session_claim,
                args.attempt,
            )
            _print_result({
                "diagnosticOutputPath": str(output_path),
                "producerHelper": "EXACT_EXPECTED_OUTPUT_ONLY",
            })
        elif args.command == "finalize-attempt":
            terminal_path, terminal = finalize_attempt(
                args.repository_root,
                args.session_claim,
                args.attempt,
            )
            _print_result({
                "terminalReceiptPath": str(terminal_path),
                "attemptOrdinal": terminal["attemptOrdinal"],
                "outcome": terminal["outcome"],
                "outcomeRetryable": terminal["outcomeRetryable"],
                "nextAttemptAllowed": terminal["nextAttemptAllowed"],
            })
        elif args.command == "verify-session":
            _print_result(
                verify_session_state(
                    args.repository_root,
                    args.session_claim,
                )
            )
        else:
            raise SessionAuthorityError("unknown command")
    except (SessionAuthorityError, CANDIDATE.CandidateAuthorityError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
