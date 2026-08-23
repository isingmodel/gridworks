#!/usr/bin/env python3
"""Adversarial checks for the controlled local Codex transcript authority.

The suite never starts Codex and never reads or writes the account's real
``~/.codex``.  Parent-chain, evaluator-source, and signed-CLI bindings are
fixed test doubles; claim-first files, rollout inventory, transcript parsing,
and the public verifier exercise the production implementation against an
isolated ``home/.codex/sessions`` tree.
"""

from __future__ import annotations

import concurrent.futures
import contextlib
import copy
import hashlib
import importlib.util
import inspect
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
import threading
import time
import types
import unittest
import uuid
from unittest import mock


SCRIPT_DIR = Path(__file__).resolve().parent
REPOSITORY_ROOT = SCRIPT_DIR.parents[2]
MODULE_PATH = SCRIPT_DIR / "realtime-controlled-codex-transcript-authority.py"

SPEC = importlib.util.spec_from_file_location(
    "realtime_controlled_codex_transcript_authority_under_test",
    MODULE_PATH,
)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot load {MODULE_PATH}")
AUTHORITY = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = AUTHORITY
SPEC.loader.exec_module(AUTHORITY)
SESSION = AUTHORITY.SESSION


def _jsonl(rows: list[dict]) -> bytes:
    return b"".join(
        json.dumps(row, sort_keys=True, separators=(",", ":")).encode("utf-8")
        + b"\n"
        for row in rows
    )


class ControlledTranscriptHarness:
    """One isolated parent chain and one possible controlled execution."""

    def __init__(self, base: Path, mode: str = "normal") -> None:
        self.base = base.resolve(strict=True)
        self.mode = mode
        self.policy_bytes = AUTHORITY.POLICY_PATH.read_bytes()
        self.policy = json.loads(self.policy_bytes)
        self.home = self.base / "home"
        self.codex_home = self.home / ".codex"
        self.sessions_root = self.codex_home / "sessions"
        self.sessions_root.mkdir(parents=True)
        self.environment = {
            "CODEX_HOME": str(self.codex_home),
            "HOME": str(self.home),
            "LANG": "en_US.UTF-8",
            "LC_ALL": "en_US.UTF-8",
            "LOGNAME": "controlled-test",
            "NO_COLOR": "1",
            "PATH": "/usr/bin:/bin",
            "SHELL": "/bin/zsh",
            "TMPDIR": "/private/tmp",
            "USER": "controlled-test",
        }
        self.historical_rollout = self._rollout_path(
            "2026-08-22T09-00-00",
            "11111111-1111-4111-8111-111111111111",
            day="22",
        )
        self.historical_rollout.parent.mkdir(parents=True)
        self.historical_rollout.write_bytes(b"{}\n")

        self.chain_root = self.base / "parent-evaluation-chain-v1"
        artifact_root = self.chain_root / "artifacts"
        artifact_root.mkdir(parents=True)
        self.chain_claim_path = self.chain_root / "evaluation-chain-claim.json"
        self.session_claim_path = self.base / "session" / "session-claim.json"
        self.session_claim_path.parent.mkdir(parents=True)
        self.session_claim_path.write_bytes(b"{}\n")
        self.aggregate_path = artifact_root / "aggregate.json"
        self.judge_path = artifact_root / "judge-input.json"

        digest = lambda label: AUTHORITY.sha256_bytes(label.encode("utf-8"))
        claim = {
            "canonicalChainRoot": str(self.chain_root),
            "canonicalClaimPath": str(self.chain_claim_path),
            "chainId": digest("evaluation-chain-id"),
            "evaluationChainClaimSha256": digest("evaluation-chain-self"),
            "routeBoundary": {
                "routeKind": "TARGETED_CHECKPOINT",
                "candidateProfileId": "TARGETED_CHECKPOINT_DEBUG",
                "sessionProfileId": "TARGETED_CHECKPOINT_DEBUG",
                "selector": "A1_NORMAL_READY",
                "availability": "AVAILABLE_DIAGNOSTIC_ONLY",
                "executionAuthorized": True,
                "routeDisposition": "DIAGNOSTIC_ATTEMPT_AUTHORIZED",
                "routeBindingSha256": digest("route-binding"),
                "evidenceClass": "HEADLESS_DIAGNOSTIC_ONLY",
                "nativePresentationObserved": False,
                "scoreBearingEvidence": False,
                "futureEventStatusBar": {
                    "requiredSignals": [
                        "CURRENT_TIME",
                        "NEXT_EVENT_COUNTDOWN",
                        "EVENT_START_END",
                        "CONSTRUCTION_COMPLETION",
                        "PROMISE_DECISION_DEADLINE",
                        "THERMAL_TRIP_RECOVERY",
                    ],
                    "headlessWiringStatus": (
                        "EXACT_PACKAGE_TWO_CHECKPOINT_SCENE_LOAD_PASS"
                    ),
                    "nativeQualityStatus": "NOT_OBSERVED",
                    "nativeQualityObserved": False,
                },
                "authoredReachabilityOnly": False,
                "nativeReachabilityClaim": False,
                "routeBoundarySha256": digest("route-boundary-self"),
            },
            "parentSessionAuthority": {
                "canonicalSessionClaimPath": str(self.session_claim_path),
                "sessionClaimRawSha256": digest("session-claim-raw"),
            },
        }
        claim_bytes = AUTHORITY.json_file_bytes(claim)
        self.chain_claim_path.write_bytes(claim_bytes)
        aggregate = {
            "artifactChainId": digest("artifact-chain-id"),
            "artifactSha256": digest("aggregate-self"),
            "status": "FINALIZED_BLOCKED_NON_SCORE",
            "artifactAuthoritySourceCommit": (
                "a270339a778e49ce0458c61cef383fc96283a596"
            ),
            "artifactProducerFilesSha256": (
                "sha256:225696ad11902e33213693e75e9576368a091b1a16ba32a3c0a449e6179dea1d"
            ),
        }
        aggregate_bytes = AUTHORITY.json_file_bytes(aggregate)
        self.aggregate_path.write_bytes(aggregate_bytes)
        artifact_names = [
            "evidence-index.json",
            "actor-terminal.json",
            "judge-input.json",
            "judge-terminal.json",
            "evidence-verifier-result.json",
            "product-oracle-ledger.json",
            "aggregate.json",
        ]
        artifact_rows: list[tuple[Path, bytes, dict]] = []
        for index, name in enumerate(artifact_names):
            path = artifact_root / name
            if index == 2:
                value = {
                    "artifactKind": "JUDGE_INPUT",
                    "artifactSha256": digest("judge-input-self"),
                    "status": "BLOCKED_NO_EXECUTABLE_JUDGE_INPUT",
                    "payload": {
                        "executableJudgeInput": False,
                        "futureModelRequirement": {
                            "model": "gpt-5.6-sol",
                            "reasoningEffort": "ultra",
                            "requirementOnlyNotExecutionClaim": True,
                        },
                    },
                }
            elif index == 6:
                value = aggregate
            else:
                value = {"fixtureOrdinal": index + 1}
            data = AUTHORITY.json_file_bytes(value)
            path.write_bytes(data)
            artifact_rows.append((path, data, value))
        self.parent_context = AUTHORITY.ParentArtifactContext(
            parent=types.SimpleNamespace(claim=claim, claim_bytes=claim_bytes),
            artifact_producer={"fixture": True},
            aggregate_path=self.aggregate_path,
            aggregate=aggregate,
            aggregate_bytes=aggregate_bytes,
            artifacts=tuple(artifact_rows),
            chain_claim_path=self.chain_claim_path,
            session_claim_path=self.session_claim_path,
        )
        self.producer = self._producer_fixture()
        self.cli = self._cli_fixture()
        self.thread_id = str(uuid.uuid4())
        self.turn_id = str(uuid.uuid4())
        self.usage = {
            "input_tokens": 101,
            "cached_input_tokens": 11,
            "cache_write_input_tokens": 0,
            "output_tokens": 37,
            "reasoning_output_tokens": 19,
        }
        self.launch_calls = 0
        self.launch_environments: list[dict[str, str]] = []
        self.launch_argv: list[list[str]] = []
        self.last_stdout = b""
        self.last_output = b""
        self.last_rollout = b""
        self.last_rollout_path: Path | None = None
        self._claim_mutex = threading.RLock()
        self._claim_depth = 0

    def _rollout_path(self, timestamp: str, thread_id: str, *, day: str = "23") -> Path:
        return (
            self.sessions_root
            / "2026"
            / "08"
            / day
            / f"rollout-{timestamp}-{thread_id}.jsonl"
        )

    def _producer_fixture(self) -> dict:
        rows = []
        for path, role in AUTHORITY.PRODUCER_PATH_ROLES:
            content = f"{path}\x00{role}".encode("utf-8")
            rows.append(
                {
                    "path": path,
                    "role": role,
                    "gitMode": "100644",
                    "gitObjectId": hashlib.sha1(content).hexdigest(),
                    "rawSha256": AUTHORITY.sha256_bytes(content),
                    "byteLength": len(content),
                }
            )
        return {
            "schemaVersion": AUTHORITY.PRODUCER_SCHEMA,
            "sourceCommit": "2222222222222222222222222222222222222222",
            "fileCount": len(rows),
            "files": rows,
            "filesSha256": AUTHORITY.canonical_sha256(rows),
            "runningFilesMatchGitBlobs": True,
            "gitCommandAuthoritySha256": AUTHORITY.sha256_bytes(b"fixed-test-git"),
            "parentArtifactSemanticVerifierDependencyBound": True,
            "semanticVerifierEntryPoint": (
                "verify_controlled_codex_transcript_against_reconstructed_authority"
            ),
            "structuralSchemasAuthority": "STRUCTURAL_ONLY_NOT_TRANSCRIPT_AUTHORITY",
        }

    def _cli_fixture(self) -> dict:
        expected = self.policy["codexCliAuthority"]
        empty = AUTHORITY.sha256_bytes(b"")
        return {
            "canonicalNativeExecutablePath": expected[
                "canonicalNativeExecutablePath"
            ],
            "version": expected["version"],
            "rawSha256": expected["rawSha256"],
            "byteLength": expected["byteLength"],
            "device": 1,
            "inode": 2,
            "mode": stat.S_IFREG | 0o755,
            "linkCount": 1,
            "modifiedUnixNs": 1,
            "changedUnixNs": 1,
            "codesignIdentifier": expected["codesignIdentifier"],
            "codesignTeamIdentifier": expected["codesignTeamIdentifier"],
            "codesignCDHash": expected["codesignCDHash"],
            "codesignDesignatedRequirement": expected[
                "codesignDesignatedRequirement"
            ],
            "codesignVerifyStdoutRawSha256": empty,
            "codesignVerifyStderrRawSha256": empty,
            "codesignInspectRawSha256": AUTHORITY.sha256_bytes(b"inspect"),
            "codesignRequirementRawSha256": AUTHORITY.sha256_bytes(
                b"requirement"
            ),
            "versionStdoutRawSha256": AUTHORITY.sha256_bytes(
                b"codex-cli 0.149.0\n"
            ),
            "versionStderrRawSha256": empty,
            "bindingScope": (
                "EXACT_NATIVE_MACH_O_BYTES_STAT_VERSION_AND_APPLE_CODESIGN_"
                "IDENTITY_TRANSITIVE_CLOSURE_UNBOUND"
            ),
        }

    def set_route_kind(self, route_kind: str) -> None:
        digest = lambda label: AUTHORITY.sha256_bytes(label.encode("utf-8"))
        if route_kind == "STORY_PART_UNIT":
            route = {
                "routeKind": route_kind,
                "candidateProfileId": None,
                "sessionProfileId": route_kind,
                "selector": "chapter-01.introduction",
                "availability": "AVAILABLE_CONTENT_UNIT_ONLY",
                "executionAuthorized": True,
                "routeDisposition": "CONTENT_UNIT_ATTEMPT_AUTHORIZED",
                "routeBindingSha256": digest("story-route-binding"),
                "evidenceClass": "AUTHORED_CONTENT_UNIT_ONLY",
                "nativePresentationObserved": False,
                "scoreBearingEvidence": False,
                "futureEventStatusBar": None,
                "authoredReachabilityOnly": True,
                "nativeReachabilityClaim": False,
                "routeBoundarySha256": "",
            }
        elif route_kind == "FULL_FLOW_EXCEPTION":
            route = {
                "routeKind": route_kind,
                "candidateProfileId": route_kind,
                "sessionProfileId": route_kind,
                "selector": None,
                "availability": "UNAVAILABLE_NOT_IMPLEMENTED",
                "executionAuthorized": False,
                "routeDisposition": "ROUTE_UNAVAILABLE_NO_EXECUTION",
                "routeBindingSha256": digest("full-flow-route-binding"),
                "evidenceClass": "UNAVAILABLE_ROUTE_TERMINAL_ONLY",
                "nativePresentationObserved": False,
                "scoreBearingEvidence": False,
                "futureEventStatusBar": None,
                "authoredReachabilityOnly": False,
                "nativeReachabilityClaim": False,
                "routeBoundarySha256": "",
            }
        else:
            raise AssertionError(f"unsupported fixture route {route_kind}")
        route["routeBoundarySha256"] = AUTHORITY.self_hash(
            route,
            "routeBoundarySha256",
        )
        claim = copy.deepcopy(self.parent_context.parent.claim)
        claim["routeBoundary"] = route
        claim_bytes = AUTHORITY.json_file_bytes(claim)
        self.chain_claim_path.write_bytes(claim_bytes)
        self.parent_context = AUTHORITY.ParentArtifactContext(
            parent=types.SimpleNamespace(claim=claim, claim_bytes=claim_bytes),
            artifact_producer=self.parent_context.artifact_producer,
            aggregate_path=self.parent_context.aggregate_path,
            aggregate=self.parent_context.aggregate,
            aggregate_bytes=self.parent_context.aggregate_bytes,
            artifacts=self.parent_context.artifacts,
            chain_claim_path=self.parent_context.chain_claim_path,
            session_claim_path=self.parent_context.session_claim_path,
        )

    @contextlib.contextmanager
    def claim_lock(self, _path: Path):
        with self._claim_mutex:
            self._claim_depth += 1
            try:
                yield
            finally:
                self._claim_depth -= 1

    def fixed_environment(self) -> tuple[dict[str, str], Path, Path]:
        return dict(self.environment), self.codex_home, self.sessions_root

    def _stdout_rows(self, output_text: str) -> list[dict]:
        return [
            {"type": "thread.started", "thread_id": self.thread_id},
            {"type": "turn.started"},
            {
                "type": "item.completed",
                "item": {
                    "id": "item-1",
                    "type": "agent_message",
                    "text": output_text,
                },
            },
            {"type": "turn.completed", "usage": dict(self.usage)},
        ]

    def _rollout_rows(self, cwd: Path, prompt_text: str, output_text: str) -> list[dict]:
        return [
            {
                "type": "session_meta",
                "payload": {
                    "id": self.thread_id,
                    "session_id": self.thread_id,
                    "cwd": str(cwd),
                    "cli_version": "0.149.0",
                    "source": "exec",
                    "originator": "codex_exec",
                    "model_provider": "openai",
                    "git": None,
                    "forked_from_id": None,
                    "parent_thread_id": None,
                },
            },
            {
                "type": "event_msg",
                "payload": {"type": "task_started", "turn_id": self.turn_id},
            },
            {
                "type": "response_item",
                "payload": {
                    "type": "message",
                    "role": "developer",
                    "content": [{"type": "input_text", "text": "fixture"}],
                },
            },
            {
                "type": "turn_context",
                "payload": {
                    "turn_id": self.turn_id,
                    "cwd": str(cwd),
                    "model": "gpt-5.6-sol",
                    "effort": "ultra",
                    "approval_policy": "never",
                    "sandbox_policy": {"type": "read-only"},
                },
            },
            {
                "type": "event_msg",
                "payload": {
                    "type": "item_completed",
                    "item": {"type": "user_message", "text": prompt_text},
                },
            },
            {
                "type": "response_item",
                "payload": {
                    "type": "message",
                    "role": "user",
                    "content": [{"type": "input_text", "text": prompt_text}],
                },
            },
            {
                "type": "event_msg",
                "payload": {
                    "type": "item_completed",
                    "item": {"type": "agent_message", "text": output_text},
                },
            },
            {
                "type": "response_item",
                "payload": {
                    "type": "message",
                    "role": "assistant",
                    "phase": "final_answer",
                    "content": [{"type": "output_text", "text": output_text}],
                },
            },
            {
                "type": "event_msg",
                "payload": {
                    "type": "token_count",
                    "info": {"total_token_usage": dict(self.usage)},
                },
            },
            {
                "type": "event_msg",
                "payload": {
                    "type": "task_complete",
                    "turn_id": self.turn_id,
                    "last_agent_message": output_text,
                },
            },
        ]

    def _write_reserved_output(self, path: Path, data: bytes) -> None:
        flags = os.O_WRONLY
        if hasattr(os, "O_NOFOLLOW"):
            flags |= os.O_NOFOLLOW
        descriptor = os.open(path, flags)
        try:
            if os.write(descriptor, data) != len(data):
                raise AssertionError("short fixture output write")
            os.fsync(descriptor)
        finally:
            os.close(descriptor)

    def launcher(
        self,
        argv,
        environment,
        prompt,
        stdout_descriptor,
        stderr_descriptor,
        cwd,
        timeout_seconds,
    ) -> int:
        if self._claim_depth <= 0:
            raise AssertionError("launcher ran outside the parent claim lock")
        self.launch_calls += 1
        self.launch_environments.append(dict(environment))
        self.launch_argv.append(list(argv))
        if dict(environment) != self.environment:
            raise AssertionError("launcher received ambient or incomplete environment")
        if timeout_seconds != self.policy["invocation"]["timeoutSeconds"]:
            raise AssertionError("timeout drift")
        if cwd != self.chain_root.with_name(
            self.chain_root.name
            + self.policy["controlledExecution"]["transcriptRootSuffix"]
        ) / "capsule":
            raise AssertionError("exclusive capsule cwd drift")
        transcript_root = cwd.parent
        start_path = transcript_root / self.policy["controlledExecution"][
            "startReceiptFileName"
        ]
        final_path = transcript_root / self.policy["controlledExecution"][
            "finalReceiptFileName"
        ]
        if not start_path.is_file() or start_path.stat().st_size == 0:
            raise AssertionError("start receipt was not finalized before launch")
        if not final_path.is_file() or final_path.stat().st_size != 0:
            raise AssertionError("final receipt inode was not reserved empty")
        start = json.loads(start_path.read_bytes())
        if prompt != (transcript_root / "prompt.txt").read_bytes():
            raise AssertionError("prompt transport bytes drift")
        if self.mode == "interrupt":
            os.write(stdout_descriptor, b"partial")
            raise AUTHORITY.ControlledCodexTranscriptAuthorityError(
                "injected mocked transport interruption"
            )
        output = AUTHORITY._expected_probe_output(
            start["parentAuthority"], start["nonce"]
        )
        output_bytes = AUTHORITY.json_file_bytes(output)
        output_text = output_bytes.decode("utf-8")
        stdout_bytes = _jsonl(self._stdout_rows(output_text))
        os.write(stdout_descriptor, stdout_bytes)
        os.write(stderr_descriptor, AUTHORITY.EXPECTED_STDERR)
        output_index = list(argv).index("--output-last-message") + 1
        output_path = Path(argv[output_index])
        self._write_reserved_output(output_path, output_bytes)
        self.last_stdout = stdout_bytes
        self.last_output = output_bytes
        if self.mode == "none":
            return 0
        prompt_text = prompt.decode("utf-8")
        rollout_rows = self._rollout_rows(cwd, prompt_text, output_text)
        rollout_bytes = _jsonl(rollout_rows)
        rollout_path = self._rollout_path(
            "2026-08-23T14-30-26", self.thread_id
        )
        rollout_path.parent.mkdir(parents=True, exist_ok=True)
        if self.mode == "symlink-rollout":
            outside = self.base / "rollout-symlink-target.jsonl"
            outside.write_bytes(rollout_bytes)
            os.symlink(outside, rollout_path)
        else:
            rollout_path.write_bytes(rollout_bytes)
        self.last_rollout = rollout_bytes
        self.last_rollout_path = rollout_path
        if self.mode == "multiple":
            second_id = str(uuid.uuid4())
            self._rollout_path(
                "2026-08-23T14-30-27", second_id
            ).write_bytes(rollout_bytes)
        elif self.mode == "stale":
            old = time.time_ns() - 60_000_000_000
            os.utime(rollout_path, ns=(old, old), follow_symlinks=False)
        elif self.mode == "hardlink-rollout":
            os.link(rollout_path, self.base / "rollout-hardlink.jsonl")
        elif self.mode == "unexpected-inventory":
            (self.sessions_root / "unexpected.txt").write_bytes(b"unexpected")
        elif self.mode == "replace-historical":
            moved = self.base / "historical-rollout-moved.jsonl"
            self.historical_rollout.rename(moved)
            self.historical_rollout.write_bytes(b"replacement\n")
        return 0

    @contextlib.contextmanager
    def patched_authority(self):
        with contextlib.ExitStack() as stack:
            stack.enter_context(
                mock.patch.object(
                    AUTHORITY,
                    "load_transcript_policy",
                    side_effect=lambda: (self.policy, self.policy_bytes),
                )
            )
            stack.enter_context(
                mock.patch.object(
                    AUTHORITY,
                    "bind_transcript_evaluator_authority",
                    return_value=self.producer,
                )
            )
            stack.enter_context(
                mock.patch.object(
                    AUTHORITY,
                    "bind_codex_cli_authority",
                    return_value=self.cli,
                )
            )
            stack.enter_context(
                mock.patch.object(
                    AUTHORITY,
                    "_fixed_process_environment",
                    side_effect=self.fixed_environment,
                )
            )
            stack.enter_context(
                mock.patch.object(
                    AUTHORITY,
                    "_locate_session_claim_from_aggregate",
                    return_value=(self.chain_claim_path, self.session_claim_path),
                )
            )
            stack.enter_context(
                mock.patch.object(
                    AUTHORITY,
                    "_reconstruct_parent_artifacts_without_lock",
                    return_value=self.parent_context,
                )
            )
            stack.enter_context(
                mock.patch.object(
                    AUTHORITY,
                    "_launch_codex_process",
                    side_effect=self.launcher,
                )
            )
            stack.enter_context(
                mock.patch.object(
                    SESSION,
                    "exclusive_claim_lock",
                    side_effect=self.claim_lock,
                )
            )
            yield

    def create(self):
        return AUTHORITY.create_controlled_codex_transcript_authority(
            REPOSITORY_ROOT,
            self.aggregate_path,
            transcript_authority_revision=self.producer["sourceCommit"],
        )

    def verify(self, receipt_path: Path):
        return AUTHORITY.verify_controlled_codex_transcript_against_reconstructed_authority(
            REPOSITORY_ROOT,
            receipt_path,
        )


class RealtimeControlledCodexTranscriptAuthorityTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix=".controlled-codex-transcript-test-",
        )
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve(strict=True)
        self.assertFalse(self.root.is_relative_to(REPOSITORY_ROOT))
        self.counter = 0

    def harness(self, mode: str = "normal") -> ControlledTranscriptHarness:
        self.counter += 1
        base = self.root / f"fixture-{self.counter:03d}"
        base.mkdir()
        return ControlledTranscriptHarness(base, mode)

    def assertRejected(self, callable_value, fragment: str | None = None) -> None:
        with self.assertRaises(
            AUTHORITY.ControlledCodexTranscriptAuthorityError
        ) as captured:
            callable_value()
        if fragment is not None:
            self.assertIn(fragment, str(captured.exception))

    def _happy(self) -> tuple[ControlledTranscriptHarness, Path, dict]:
        harness = self.harness()
        with harness.patched_authority():
            path, receipt = harness.create()
            verified, raw_hash = harness.verify(path)
        self.assertTrue(AUTHORITY.strict_typed_equal(receipt, verified))
        self.assertEqual(AUTHORITY.sha256_bytes(path.read_bytes()), raw_hash)
        return harness, path, receipt

    def _find_ajv_root(self) -> Path:
        candidates = sorted(
            Path.home().glob(".npm/_npx/*/node_modules"), reverse=True
        )
        for root in candidates:
            package = root / "ajv" / "package.json"
            module = root / "ajv" / "dist" / "2020.js"
            if package.is_file() and module.is_file():
                version = json.loads(package.read_text(encoding="utf-8"))[
                    "version"
                ]
                if version == "8.20.0":
                    return root
        raise AssertionError("AJV 8.20.0 Draft 2020-12 runtime is unavailable")

    def _assert_schema_instances(self, rows: list[dict]) -> None:
        fixture = self.root / "schema-instances.json"
        fixture.write_text(json.dumps(rows), encoding="utf-8")
        script = r"""
const fs = require('fs');
const path = require('path');
const root = process.argv[1];
const fixture = process.argv[2];
const AjvModule = require(path.join(root, 'ajv/dist/2020'));
const Ajv2020 = AjvModule.default || AjvModule;
for (const row of JSON.parse(fs.readFileSync(fixture, 'utf8'))) {
  const schema = JSON.parse(fs.readFileSync(row.schema, 'utf8'));
  const ajv = new Ajv2020({strict: true, allErrors: true});
  const validate = ajv.compile(schema);
  if (!validate(row.valid)) {
    console.error('genuine instance rejected', row.schema, validate.errors);
    process.exit(2);
  }
  for (const invalid of row.invalid) {
    if (validate(invalid)) {
      console.error('mutated instance accepted', row.schema);
      process.exit(3);
    }
  }
}
"""
        completed = subprocess.run(
            [
                "node",
                "-e",
                script,
                str(self._find_ajv_root()),
                str(fixture),
            ],
            cwd=REPOSITORY_ROOT,
            capture_output=True,
            text=True,
            check=False,
        )
        self.assertEqual(0, completed.returncode, completed.stderr)

    def test_mocked_create_verify_exact_fresh_rollout_and_non_score_boundary(self) -> None:
        with mock.patch.dict(
            os.environ,
            {
                "CODEX_HOME": "/forged/ambient/codex",
                "OPENAI_API_KEY": "must-not-cross-boundary",
                "OPENAI_BASE_URL": "https://forged.invalid",
                "HTTP_PROXY": "http://forged.invalid",
            },
        ):
            harness, path, receipt = self._happy()
        self.assertEqual(1, harness.launch_calls)
        self.assertEqual([harness.environment], harness.launch_environments)
        self.assertEqual(1, receipt["rolloutInventoryBefore"]["fileCount"])
        self.assertEqual(2, receipt["rolloutInventoryAfter"]["fileCount"])
        self.assertEqual(
            "FINALIZED_LOCAL_CODEX_ROLLOUT_MATCHED_REQUEST_NON_PLATFORM_ATTESTATION",
            receipt["status"],
        )
        self.assertEqual("gpt-5.6-sol", receipt["requestedModel"])
        self.assertEqual("ultra", receipt["requestedCodexClientEffort"])
        self.assertEqual("ultra", receipt["rolloutReportedCodexClientEffort"])
        self.assertFalse(receipt["platformModelAttested"])
        self.assertFalse(receipt["platformReasoningEffortAttested"])
        self.assertFalse(receipt["platformFreshnessAttested"])
        self.assertIsNone(receipt["serverSignedResponseReceipt"])
        self.assertIsNone(receipt["effectiveApiReasoningEffort"])
        self.assertFalse(receipt["boundJudgeInputExecuted"])
        self.assertEqual(0, receipt["currentRouteJudgeModelCallCount"])
        self.assertFalse(receipt["officialCommercialUX"])
        self.assertFalse(receipt["scoreBearing"])
        self.assertFalse(receipt["scoreBearingCaptureAllowed"])
        self.assertIsNone(receipt["commercialUXProxy"])
        self.assertEqual(
            "UNATTESTED_REFERENCE_ONLY_NOT_CONSUMED",
            receipt["historicProbeStatus"],
        )
        route = receipt["parentAuthority"]["routeBoundary"]
        self.assertEqual("TARGETED_CHECKPOINT", route["routeKind"])
        self.assertEqual(
            [
                "CURRENT_TIME",
                "NEXT_EVENT_COUNTDOWN",
                "EVENT_START_END",
                "CONSTRUCTION_COMPLETION",
                "PROMISE_DECISION_DEADLINE",
                "THERMAL_TRIP_RECOVERY",
            ],
            route["futureEventStatusBar"]["requiredSignals"],
        )
        self.assertEqual(
            "NOT_OBSERVED",
            route["futureEventStatusBar"]["nativeQualityStatus"],
        )
        self.assertEqual(
            {"model": "gpt-5.6-sol", "reasoningEffort": "ultra", "requirementOnlyNotExecutionClaim": True},
            receipt["parentAuthority"]["blockedJudgeInputFutureModelRequirement"],
        )
        transcript_root = path.parent
        self.assertFalse(transcript_root.is_relative_to(REPOSITORY_ROOT))
        self.assertFalse(any(transcript_root.rglob("rollout-*.jsonl")))
        self.assertTrue(Path(receipt["newRollout"]["canonicalPath"]).is_relative_to(
            harness.sessions_root
        ))
        self.assertEqual(
            set(harness.policy["controlledExecution"]["exactFinalRootInventory"]),
            {child.name for child in transcript_root.iterdir()},
        )
        start = json.loads((transcript_root / "probe-start-receipt.json").read_bytes())
        prompt = (transcript_root / "prompt.txt").read_text(encoding="utf-8")
        for field in (
            "evaluationChainClaimRawSha256",
            "aggregateRawSha256",
            "blockedJudgeInputRawSha256",
        ):
            self.assertIn(start["parentAuthority"][field], prompt)
        self.assertIn(start["nonce"], prompt)

    def test_genuine_start_final_and_probe_output_schema_instances(self) -> None:
        _harness, path, final = self._happy()
        root = path.parent
        start = json.loads((root / "probe-start-receipt.json").read_bytes())
        output = json.loads((root / "output.json").read_bytes())
        instances = []
        for schema_path, valid in (
            (AUTHORITY.START_SCHEMA_PATH, start),
            (AUTHORITY.FINAL_SCHEMA_PATH, final),
            (AUTHORITY.OUTPUT_SCHEMA_PATH, output),
        ):
            extra = copy.deepcopy(valid)
            extra["unexpected"] = None
            wrong_type = copy.deepcopy(valid)
            wrong_type[next(iter(valid))] = 17
            instances.append(
                {
                    "schema": str(schema_path),
                    "valid": valid,
                    "invalid": [extra, wrong_type],
                }
            )
        self._assert_schema_instances(instances)

    def test_story_and_full_flow_boundaries_remain_authored_only_or_unavailable(self) -> None:
        schema_instances = []
        for route_kind in ("STORY_PART_UNIT", "FULL_FLOW_EXCEPTION"):
            with self.subTest(route_kind=route_kind):
                harness = self.harness()
                harness.set_route_kind(route_kind)
                with harness.patched_authority():
                    path, receipt = harness.create()
                    verified, _raw_hash = harness.verify(path)
                self.assertTrue(AUTHORITY.strict_typed_equal(receipt, verified))
                route = receipt["parentAuthority"]["routeBoundary"]
                self.assertEqual(route_kind, route["routeKind"])
                self.assertFalse(route["nativeReachabilityClaim"])
                self.assertFalse(route["nativePresentationObserved"])
                self.assertFalse(route["scoreBearingEvidence"])
                self.assertIsNone(route["futureEventStatusBar"])
                if route_kind == "STORY_PART_UNIT":
                    self.assertTrue(route["authoredReachabilityOnly"])
                    self.assertEqual(
                        "AVAILABLE_CONTENT_UNIT_ONLY",
                        route["availability"],
                    )
                else:
                    self.assertFalse(route["executionAuthorized"])
                    self.assertEqual(
                        "UNAVAILABLE_NOT_IMPLEMENTED",
                        route["availability"],
                    )
                start = json.loads(
                    (path.parent / "probe-start-receipt.json").read_bytes()
                )
                for schema_path, valid in (
                    (AUTHORITY.START_SCHEMA_PATH, start),
                    (AUTHORITY.FINAL_SCHEMA_PATH, receipt),
                ):
                    promoted = copy.deepcopy(valid)
                    promoted["parentAuthority"]["routeBoundary"][
                        "nativeReachabilityClaim"
                    ] = True
                    schema_instances.append(
                        {
                            "schema": str(schema_path),
                            "valid": valid,
                            "invalid": [promoted],
                        }
                    )
        self._assert_schema_instances(schema_instances)

    def test_semantic_forged_output_model_effort_thread_usage_prompt_and_tool_fail(self) -> None:
        harness, path, _receipt = self._happy()
        root = path.parent
        start = json.loads((root / "probe-start-receipt.json").read_bytes())
        prompt = (root / "prompt.txt").read_bytes()
        original_stdout = [
            json.loads(line) for line in harness.last_stdout.decode("utf-8").splitlines()
        ]
        original_rollout = [
            json.loads(line) for line in harness.last_rollout.decode("utf-8").splitlines()
        ]
        original_output = json.loads(harness.last_output)
        cases = (
            "output",
            "model",
            "effort",
            "thread",
            "turn",
            "usage",
            "prompt",
            "tool",
            "stderr",
        )
        for case in cases:
            with self.subTest(case=case):
                stdout = copy.deepcopy(original_stdout)
                rollout = copy.deepcopy(original_rollout)
                output = copy.deepcopy(original_output)
                stderr = AUTHORITY.EXPECTED_STDERR
                if case == "output":
                    output["nonce"] = "0" * 64
                elif case == "model":
                    next(row for row in rollout if row["type"] == "turn_context")[
                        "payload"
                    ]["model"] = "forged-model"
                elif case == "effort":
                    next(row for row in rollout if row["type"] == "turn_context")[
                        "payload"
                    ]["effort"] = "high"
                elif case == "thread":
                    meta = next(row for row in rollout if row["type"] == "session_meta")[
                        "payload"
                    ]
                    meta["id"] = "33333333-3333-4333-8333-333333333333"
                elif case == "turn":
                    started = next(
                        row
                        for row in rollout
                        if row["type"] == "event_msg"
                        and row["payload"].get("type") == "task_started"
                    )["payload"]
                    started["turn_id"] = "44444444-4444-4444-8444-444444444444"
                elif case == "usage":
                    token = next(
                        row
                        for row in rollout
                        if row["type"] == "event_msg"
                        and row["payload"].get("type") == "token_count"
                    )
                    token["payload"]["info"]["total_token_usage"][
                        "output_tokens"
                    ] += 1
                elif case == "prompt":
                    user = next(
                        row
                        for row in rollout
                        if row["type"] == "response_item"
                        and row["payload"].get("role") == "user"
                    )
                    user["payload"]["content"][0]["text"] += "\nforged"
                elif case == "tool":
                    rollout.append(
                        {
                            "type": "response_item",
                            "payload": {"type": "function_call", "name": "forged"},
                        }
                    )
                elif case == "stderr":
                    stderr = b"forged stderr"
                output_bytes = AUTHORITY.json_file_bytes(output)
                stdout_bytes = _jsonl(stdout)
                rollout_bytes = _jsonl(rollout)
                scratch = (
                    self.root
                    / f"rollout-copy-{case}-{harness.thread_id}.jsonl"
                )
                scratch.write_bytes(rollout_bytes)
                observation, observed_bytes = AUTHORITY._read_regular_nlink_one(
                    scratch, f"{case} rollout fixture"
                )
                self.assertRejected(
                    lambda: AUTHORITY._verify_execution_transcript(
                        start=start,
                        stdout_bytes=stdout_bytes,
                        stderr_bytes=stderr,
                        output_bytes=output_bytes,
                        rollout_observation=observation,
                        rollout_bytes=observed_bytes,
                        prompt_bytes=prompt,
                    )
                )

    def test_no_multiple_stale_and_inventory_identity_rollouts_fail_closed(self) -> None:
        for mode in (
            "none",
            "multiple",
            "stale",
            "hardlink-rollout",
            "symlink-rollout",
            "unexpected-inventory",
            "replace-historical",
        ):
            with self.subTest(mode=mode):
                harness = self.harness(mode)
                with harness.patched_authority():
                    self.assertRejected(harness.create)
                final = (
                    harness.chain_root.with_name(
                        harness.chain_root.name
                        + harness.policy["controlledExecution"]["transcriptRootSuffix"]
                    )
                    / "controlled-codex-transcript.json"
                )
                self.assertTrue(final.is_file())
                self.assertEqual(0, final.stat().st_size)

    def test_o_excl_partial_poison_no_resume_and_concurrent_single_winner(self) -> None:
        interrupted = self.harness("interrupt")
        with interrupted.patched_authority():
            self.assertRejected(
                interrupted.create, "injected mocked transport interruption"
            )
            self.assertEqual(1, interrupted.launch_calls)
            transcript_root = interrupted.chain_root.with_name(
                interrupted.chain_root.name
                + interrupted.policy["controlledExecution"]["transcriptRootSuffix"]
            )
            self.assertGreater((transcript_root / "probe-start-receipt.json").stat().st_size, 0)
            self.assertEqual(
                0, (transcript_root / "controlled-codex-transcript.json").stat().st_size
            )
            self.assertRejected(interrupted.create)
            self.assertEqual(1, interrupted.launch_calls)

        concurrent_harness = self.harness()

        def create_once():
            try:
                return "ok", concurrent_harness.create()[0]
            except AUTHORITY.ControlledCodexTranscriptAuthorityError as error:
                return "error", str(error)

        with concurrent_harness.patched_authority():
            with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
                results = list(executor.map(lambda _unused: create_once(), range(2)))
        self.assertEqual(1, sum(kind == "ok" for kind, _value in results))
        self.assertEqual(1, sum(kind == "error" for kind, _value in results))
        self.assertEqual(1, concurrent_harness.launch_calls)

    def test_transcript_root_inside_source_repository_is_rejected_before_creation(self) -> None:
        harness = self.harness()
        forged_chain_root = REPOSITORY_ROOT / (
            ".forged-controlled-codex-parent-" + uuid.uuid4().hex
        )
        forged_transcript_root = Path(
            str(forged_chain_root)
            + harness.policy["controlledExecution"]["transcriptRootSuffix"]
        )
        harness.parent_context.parent.claim["canonicalChainRoot"] = str(
            forged_chain_root
        )
        with harness.patched_authority():
            self.assertRejected(harness.create, "outside the source repository")
        self.assertEqual(0, harness.launch_calls)
        self.assertFalse(forged_chain_root.exists())
        self.assertFalse(forged_transcript_root.exists())

    def test_symlink_hardlink_extra_root_and_rollout_mutations_fail_verifier(self) -> None:
        for mode in ("extra-root", "stdout-hardlink", "stdout-symlink", "rollout-hardlink"):
            with self.subTest(mode=mode):
                harness, path, _receipt = self._happy()
                root = path.parent
                if mode == "extra-root":
                    (root / "extra.json").write_bytes(b"{}\n")
                elif mode == "stdout-hardlink":
                    os.link(root / "stdout.jsonl", harness.base / "stdout-hardlink.jsonl")
                elif mode == "stdout-symlink":
                    stdout = root / "stdout.jsonl"
                    moved = harness.base / "stdout-moved.jsonl"
                    stdout.rename(moved)
                    os.symlink(moved, stdout)
                else:
                    rollout = Path(
                        json.loads(path.read_bytes())["newRollout"]["canonicalPath"]
                    )
                    os.link(rollout, harness.base / "fresh-rollout-hardlink.jsonl")
                with harness.patched_authority():
                    self.assertRejected(lambda: harness.verify(path))

    def test_parent_source_and_cli_pre_execution_races_fail_before_launch(self) -> None:
        race_kinds = ("parent", "source", "cli")
        for race_kind in race_kinds:
            with self.subTest(race=race_kind):
                harness = self.harness()
                with harness.patched_authority():
                    if race_kind == "parent":
                        calls = {"count": 0}

                        def parent_side_effect(*_args, **_kwargs):
                            calls["count"] += 1
                            if calls["count"] == 1:
                                return harness.parent_context
                            raise AUTHORITY.ControlledCodexTranscriptAuthorityError(
                                "injected parent race"
                            )

                        patcher = mock.patch.object(
                            AUTHORITY,
                            "_reconstruct_parent_artifacts_without_lock",
                            side_effect=parent_side_effect,
                        )
                    elif race_kind == "source":
                        changed = copy.deepcopy(harness.producer)
                        changed["filesSha256"] = AUTHORITY.sha256_bytes(
                            b"changed producer"
                        )
                        patcher = mock.patch.object(
                            AUTHORITY,
                            "bind_transcript_evaluator_authority",
                            side_effect=[harness.producer, changed],
                        )
                    else:
                        changed = copy.deepcopy(harness.cli)
                        changed["inode"] += 1
                        patcher = mock.patch.object(
                            AUTHORITY,
                            "bind_codex_cli_authority",
                            side_effect=[harness.cli, changed],
                        )
                    with patcher:
                        self.assertRejected(harness.create)
                self.assertEqual(0, harness.launch_calls)

    def test_parent_source_and_cli_final_verification_races_fail_closed(self) -> None:
        for race_kind in ("parent", "source", "cli"):
            with self.subTest(race=race_kind):
                harness = self.harness()
                with harness.patched_authority():
                    path, _receipt = harness.create()
                    if race_kind == "parent":
                        calls = {"count": 0}

                        def parent_side_effect(*_args, **_kwargs):
                            calls["count"] += 1
                            if calls["count"] == 1:
                                return harness.parent_context
                            raise AUTHORITY.ControlledCodexTranscriptAuthorityError(
                                "injected final parent race"
                            )

                        patcher = mock.patch.object(
                            AUTHORITY,
                            "_reconstruct_parent_artifacts_without_lock",
                            side_effect=parent_side_effect,
                        )
                    elif race_kind == "source":
                        changed = copy.deepcopy(harness.producer)
                        changed["filesSha256"] = AUTHORITY.sha256_bytes(
                            b"final changed producer"
                        )
                        patcher = mock.patch.object(
                            AUTHORITY,
                            "bind_transcript_evaluator_authority",
                            side_effect=[harness.producer, changed],
                        )
                    else:
                        changed = copy.deepcopy(harness.cli)
                        changed["inode"] += 1
                        patcher = mock.patch.object(
                            AUTHORITY,
                            "bind_codex_cli_authority",
                            side_effect=[harness.cli, changed],
                        )
                    with patcher:
                        self.assertRejected(lambda: harness.verify(path))
                self.assertEqual(1, harness.launch_calls)

    def test_rehashed_false_platform_execution_and_score_claims_fail(self) -> None:
        mutations = (
            ("platformModelAttested", True),
            ("platformReasoningEffortAttested", True),
            ("boundJudgeInputExecuted", True),
            ("currentRouteJudgeModelCallCount", 1),
            ("scoreBearingCaptureAllowed", True),
            ("officialCommercialUX", True),
            ("commercialUXProxy", 99),
            ("serverSignedResponseReceipt", {"forged": True}),
        )
        for field, value in mutations:
            with self.subTest(field=field):
                harness = self.harness()
                with harness.patched_authority():
                    path, receipt = harness.create()
                    forged = copy.deepcopy(receipt)
                    forged[field] = value
                    forged["controlledCodexTranscriptSha256"] = AUTHORITY.self_hash(
                        forged,
                        "controlledCodexTranscriptSha256",
                    )
                    path.write_bytes(AUTHORITY.json_file_bytes(forged))
                    self.assertRejected(lambda: harness.verify(path))

    def test_exact_public_api_policy_and_source_role_set(self) -> None:
        create_signature = inspect.signature(
            AUTHORITY.create_controlled_codex_transcript_authority
        )
        verify_signature = inspect.signature(
            AUTHORITY.verify_controlled_codex_transcript_against_reconstructed_authority
        )
        self.assertEqual(
            [
                "repository_root",
                "aggregate_path",
                "transcript_authority_revision",
            ],
            list(create_signature.parameters),
        )
        self.assertEqual(
            inspect.Parameter.KEYWORD_ONLY,
            create_signature.parameters["transcript_authority_revision"].kind,
        )
        self.assertEqual(
            ["repository_root", "receipt_path"],
            list(verify_signature.parameters),
        )
        policy = json.loads(AUTHORITY.POLICY_PATH.read_bytes())
        self.assertEqual(
            [path for path, _role in AUTHORITY.PRODUCER_PATH_ROLES],
            policy["evaluatorProducerAuthority"]["paths"],
        )
        self.assertEqual(7, policy["evaluatorProducerAuthority"]["expectedFileCount"])
        self.assertIn(
            "tools/commercial-ux/native/test-realtime-controlled-codex-transcript-authority.py",
            policy["evaluatorProducerAuthority"]["paths"],
        )
        self.assertNotIn("dangerously-skip-permissions", MODULE_PATH.read_text())

    def test_every_policy_denied_ambient_influence_variable_fails_before_home_access(self) -> None:
        harness = self.harness()
        with mock.patch.object(
            AUTHORITY,
            "load_transcript_policy",
            side_effect=lambda: (harness.policy, harness.policy_bytes),
        ):
            for name in harness.policy["invocation"][
                "rejectedAmbientEnvironmentNames"
            ]:
                with self.subTest(name=name), mock.patch.dict(
                    os.environ,
                    {name: "forged-ambient-influence"},
                    clear=True,
                ):
                    self.assertRejected(
                        AUTHORITY._fixed_process_environment,
                        "ambient Codex/provider/key/base-URL/proxy variables",
                    )


if __name__ == "__main__":
    unittest.main(verbosity=2)
