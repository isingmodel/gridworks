#!/usr/bin/env python3
"""Adversarial checks for the non-score realtime session authority."""

from __future__ import annotations

import concurrent.futures
import contextlib
import copy
import importlib.util
import inspect
import json
import os
from pathlib import Path
import re
import sys
import tempfile
import unittest
from unittest import mock


SCRIPT_DIR = Path(__file__).resolve().parent
REPOSITORY_ROOT = SCRIPT_DIR.parents[2]
MODULE_PATH = SCRIPT_DIR / "realtime-session-authority.py"
FIXED_CANDIDATE_SOURCE_COMMIT = "379e9800c81ca315976ab4c28d511664df6ab7ed"
FIXED_CANDIDATE_RAW_SHA256 = (
    "sha256:ca7826d38cae6e8a28e142e10e522e9c1425ba6abcec938182d9819ab0b2a816"
)
FIXED_CANDIDATE_SHA256 = (
    "sha256:373785e45a4485dfeded43466a5bff0f66de4a0c106c972262686e7a432cbdd6"
)

SPEC = importlib.util.spec_from_file_location(
    "realtime_session_authority_under_test",
    MODULE_PATH,
)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot load {MODULE_PATH}")
AUTHORITY = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = AUTHORITY
SPEC.loader.exec_module(AUTHORITY)
CANDIDATE = AUTHORITY.CANDIDATE


def _resolve_local_ref(root: dict, reference: str):
    if not reference.startswith("#/"):
        raise AssertionError(f"unsupported non-local schema reference: {reference}")
    value = root
    for raw_token in reference[2:].split("/"):
        token = raw_token.replace("~1", "/").replace("~0", "~")
        value = value[token]
    return value


def validate_schema_subset(instance, schema, root, path: str = "$") -> None:
    """Validate every Draft 2020-12 assertion used by the three schemas."""

    if schema is False:
        raise AssertionError(f"{path}: boolean false schema")
    if schema is True:
        return
    if not isinstance(schema, dict):
        raise AssertionError(f"{path}: schema must be an object or boolean")
    if "$ref" in schema:
        validate_schema_subset(instance, _resolve_local_ref(root, schema["$ref"]), root, path)
    for child in schema.get("allOf", []):
        validate_schema_subset(instance, child, root, path)
    if "oneOf" in schema:
        matches = 0
        for child in schema["oneOf"]:
            try:
                validate_schema_subset(instance, child, root, path)
                matches += 1
            except AssertionError:
                pass
        if matches != 1:
            raise AssertionError(f"{path}: oneOf matched {matches} branches")
    if "if" in schema:
        try:
            validate_schema_subset(instance, schema["if"], root, path)
            condition = True
        except AssertionError:
            condition = False
        branch = schema.get("then" if condition else "else")
        if branch is not None:
            validate_schema_subset(instance, branch, root, path)
    expected_type = schema.get("type")
    type_matches = {
        "object": isinstance(instance, dict),
        "array": isinstance(instance, list),
        "string": isinstance(instance, str),
        "integer": isinstance(instance, int) and not isinstance(instance, bool),
        "boolean": isinstance(instance, bool),
        "null": instance is None,
    }
    if expected_type is not None and not type_matches.get(expected_type, False):
        raise AssertionError(f"{path}: expected {expected_type}")
    if "const" in schema and not AUTHORITY.strict_typed_equal(instance, schema["const"]):
        raise AssertionError(f"{path}: const mismatch")
    if "enum" in schema and not any(
        AUTHORITY.strict_typed_equal(instance, choice) for choice in schema["enum"]
    ):
        raise AssertionError(f"{path}: enum mismatch")
    if isinstance(instance, dict):
        missing = [key for key in schema.get("required", []) if key not in instance]
        if missing:
            raise AssertionError(f"{path}: missing required keys {missing}")
        properties = schema.get("properties", {})
        for key, child in properties.items():
            if key in instance:
                validate_schema_subset(instance[key], child, root, f"{path}.{key}")
        if schema.get("additionalProperties") is False:
            extras = set(instance) - set(properties)
            if extras:
                raise AssertionError(f"{path}: extra keys {sorted(extras)}")
    if isinstance(instance, list):
        if len(instance) < schema.get("minItems", 0):
            raise AssertionError(f"{path}: too few items")
        if "maxItems" in schema and len(instance) > schema["maxItems"]:
            raise AssertionError(f"{path}: too many items")
        if schema.get("uniqueItems"):
            encoded = [
                json.dumps(value, sort_keys=True, separators=(",", ":"))
                for value in instance
            ]
            if len(encoded) != len(set(encoded)):
                raise AssertionError(f"{path}: duplicate items")
        prefixes = schema.get("prefixItems", [])
        for index, child in enumerate(prefixes):
            if index < len(instance):
                validate_schema_subset(instance[index], child, root, f"{path}[{index}]")
        items = schema.get("items")
        remaining = instance[len(prefixes):]
        if items is False and remaining:
            raise AssertionError(f"{path}: additional tuple items")
        if isinstance(items, dict):
            for offset, value in enumerate(remaining, start=len(prefixes)):
                validate_schema_subset(value, items, root, f"{path}[{offset}]")
    if isinstance(instance, str):
        if len(instance) < schema.get("minLength", 0):
            raise AssertionError(f"{path}: string too short")
        if len(instance) > schema.get("maxLength", len(instance)):
            raise AssertionError(f"{path}: string too long")
        if "pattern" in schema and re.search(schema["pattern"], instance) is None:
            raise AssertionError(f"{path}: pattern mismatch")
    if isinstance(instance, int) and not isinstance(instance, bool):
        if instance < schema.get("minimum", instance):
            raise AssertionError(f"{path}: below minimum")
        if instance > schema.get("maximum", instance):
            raise AssertionError(f"{path}: above maximum")


class RealtimeSessionAuthorityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.stack = contextlib.ExitStack()
        cls.authority_workspace = Path(cls.stack.enter_context(
            tempfile.TemporaryDirectory(prefix="realtime-session-authority-")
        ))
        cls.build = cls.stack.enter_context(CANDIDATE.isolated_managed_build(
            REPOSITORY_ROOT,
            revision=FIXED_CANDIDATE_SOURCE_COMMIT,
        ))
        cls.candidate_policy, cls.candidate_policy_bytes = CANDIDATE.load_policy()
        cls.godot_app_root = CANDIDATE.default_godot_app(REPOSITORY_ROOT)
        engine_rows, engine_sha256 = CANDIDATE.bind_engine_tree(cls.godot_app_root)
        headless = CANDIDATE.run_headless_execution_authority(
            cls.build,
            cls.godot_app_root,
            engine_rows,
            engine_sha256,
        )
        cls.manifest = CANDIDATE.build_manifest(
            cls.build,
            engine_rows,
            engine_sha256,
            headless,
            cls.candidate_policy,
            cls.candidate_policy_bytes,
        )
        CANDIDATE.verify_manifest_against_reconstructed_authority(
            cls.manifest,
            cls.build,
            cls.godot_app_root,
            cls.candidate_policy,
            cls.candidate_policy_bytes,
        )
        cls.manifest_bytes = AUTHORITY.json_file_bytes(cls.manifest)
        if AUTHORITY.sha256_bytes(cls.manifest_bytes) != FIXED_CANDIDATE_RAW_SHA256:
            raise AssertionError("fixed candidate raw bytes drifted during test setup")
        if cls.manifest["candidateSha256"] != FIXED_CANDIDATE_SHA256:
            raise AssertionError("fixed candidate self-hash drifted during test setup")
        cls.candidate_path = cls.authority_workspace / "candidate-manifest.json"
        cls.candidate_path.write_bytes(cls.manifest_bytes)
        cls.story_manifest = CANDIDATE.validate_story_manifest(cls.build.story_bytes)
        cls.context = AUTHORITY.CandidateContext(
            repository_root=REPOSITORY_ROOT.resolve(strict=True),
            manifest_path=cls.candidate_path.resolve(strict=True),
            story_manifest_path=None,
            manifest_bytes=cls.manifest_bytes,
            manifest=cls.manifest,
            story_manifest_bytes=cls.build.story_bytes,
            story_manifest=cls.story_manifest,
            semantic_verification_performed=True,
        )
        cls.session_authority_commit = CANDIDATE.resolve_source_commit(
            REPOSITORY_ROOT,
            "HEAD",
        )
        cls.schemas = {
            "claim": json.loads(AUTHORITY.CLAIM_SCHEMA_PATH.read_text(encoding="utf-8")),
            "start": json.loads(AUTHORITY.START_SCHEMA_PATH.read_text(encoding="utf-8")),
            "terminal": json.loads(
                AUTHORITY.TERMINAL_SCHEMA_PATH.read_text(encoding="utf-8")
            ),
        }

    @classmethod
    def tearDownClass(cls) -> None:
        cls.stack.close()

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="realtime-session-test-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.session_counter = 0

    def assertRejected(self, callable_value, fragment: str | None = None) -> None:
        with self.assertRaises(AUTHORITY.SessionAuthorityError) as captured:
            callable_value()
        if fragment is not None:
            self.assertIn(fragment, str(captured.exception))

    def create_session(
        self,
        route_kind: str,
        selector: str | None,
    ) -> tuple[Path, dict]:
        self.session_counter += 1
        session_root = self.root / f"session-{self.session_counter:03d}"
        nonce = self.session_counter.to_bytes(32, "big")
        with mock.patch.object(AUTHORITY.secrets, "token_bytes", return_value=nonce):
            return AUTHORITY.create_session_claim(
                self.context,
                session_root,
                route_kind,
                selector,
                session_authority_revision=self.session_authority_commit,
            )

    def write_output_bytes(self, claim: dict, ordinal: int, data: bytes) -> Path:
        attempt = claim["attempts"][ordinal - 1]
        output_path = Path(attempt["outputPath"])
        output_path.write_bytes(data)
        return output_path

    def test_policy_fixed_candidate_and_evaluator_producer_are_exact(self) -> None:
        policy, policy_bytes = AUTHORITY.load_session_policy()
        AUTHORITY.validate_session_policy(policy, policy_bytes)
        self.assertEqual(FIXED_CANDIDATE_SOURCE_COMMIT, policy["candidateAuthority"]["sourceCommit"])
        self.assertEqual(FIXED_CANDIDATE_SHA256, policy["candidateAuthority"]["candidateSha256"])
        self.assertEqual(7, policy["evaluatorProducerAuthority"]["expectedFileCount"])
        evaluator = AUTHORITY.bind_session_evaluator_authority(
            REPOSITORY_ROOT,
            self.session_authority_commit,
        )
        self.assertEqual(7, evaluator["fileCount"])
        self.assertTrue(evaluator["runningFilesMatchGitBlobs"])
        self.assertNotEqual(FIXED_CANDIDATE_SOURCE_COMMIT, evaluator["sourceCommit"])
        self.assertRejected(
            lambda: AUTHORITY.bind_session_evaluator_authority(
                REPOSITORY_ROOT,
                FIXED_CANDIDATE_SOURCE_COMMIT,
            ),
            "lacks evaluator file",
        )
        unverified = AUTHORITY.load_fixed_candidate(
            REPOSITORY_ROOT,
            self.candidate_path,
            semantic_verify=False,
        )
        self.assertFalse(unverified.semantic_verification_performed)
        self.assertRejected(
            lambda: AUTHORITY.create_session_claim(
                unverified,
                self.root / "unverified",
                "TARGETED_CHECKPOINT",
                "A1_NORMAL_READY",
                session_authority_revision=self.session_authority_commit,
            ),
            "requires reconstructed candidate verification",
        )

    def test_both_targeted_checkpoints_succeed_and_bind_future_event_bar(self) -> None:
        expected_signals = list(CANDIDATE.FUTURE_EVENT_SIGNALS)
        for checkpoint in ("A1_NORMAL_READY", "A1_CONSTRUCTION_DUE_1M"):
            with self.subTest(checkpoint=checkpoint):
                claim_path, claim = self.create_session("TARGETED_CHECKPOINT", checkpoint)
                route = claim["routeBinding"]
                self.assertEqual(checkpoint, route["selector"])
                self.assertEqual(expected_signals, route["futureEventStatusBar"]["requiredSignals"])
                self.assertEqual(
                    "EXACT_PACKAGE_TWO_CHECKPOINT_SCENE_LOAD_PASS",
                    route["futureEventStatusBar"]["headlessWiringStatus"],
                )
                self.assertEqual("NOT_OBSERVED", route["futureEventStatusBar"]["nativeQualityStatus"])
                self.assertFalse(route["nativePresentationObserved"])
                self.assertFalse(route["nativeQualityObserved"])
                AUTHORITY.reserve_attempt(REPOSITORY_ROOT, claim_path, 1)
                AUTHORITY.write_expected_attempt_output(REPOSITORY_ROOT, claim_path, 1)
                _terminal_path, terminal = AUTHORITY.finalize_attempt(
                    REPOSITORY_ROOT,
                    claim_path,
                    1,
                )
                self.assertEqual("SUCCESS", terminal["outcome"])
                self.assertFalse(terminal["outcomeRetryable"])
                self.assertFalse(terminal["nextAttemptAllowed"])
                state = AUTHORITY.verify_session_state(REPOSITORY_ROOT, claim_path)
                self.assertEqual("SUCCESS", state["attempts"][0]["outcome"])
                self.assertRejected(
                    lambda: AUTHORITY.reserve_attempt(REPOSITORY_ROOT, claim_path, 2),
                    "does not authorize retry",
                )

    def test_all_34_story_selectors_are_exact_content_units(self) -> None:
        parts = self.story_manifest["parts"]
        self.assertEqual(34, len(parts))
        for part in parts:
            selector = part["selector"]
            with self.subTest(selector=selector):
                claim_path, claim = self.create_session("STORY_PART_UNIT", selector)
                route = claim["routeBinding"]
                self.assertIsNone(route["candidateProfileId"])
                self.assertEqual("STORY_PART_UNIT", route["sessionProfileId"])
                self.assertEqual(selector, route["selector"])
                self.assertEqual(part, route["storyPart"])
                self.assertTrue(route["authoredReachabilityOnly"])
                self.assertFalse(route["nativeReachabilityClaim"])
                self.assertFalse(route["nativePresentationObserved"])
                self.assertFalse(route["scoreBearingEvidence"])
                AUTHORITY.verify_session_claim_against_reconstructed_authority(
                    REPOSITORY_ROOT,
                    claim_path,
                )
        representative_path, _claim = self.create_session(
            "STORY_PART_UNIT",
            "FIRST_LIGHT/briefing",
        )
        AUTHORITY.reserve_attempt(REPOSITORY_ROOT, representative_path, 1)
        AUTHORITY.write_expected_attempt_output(REPOSITORY_ROOT, representative_path, 1)
        _path, terminal = AUTHORITY.finalize_attempt(
            REPOSITORY_ROOT,
            representative_path,
            1,
        )
        self.assertEqual("SUCCESS", terminal["outcome"])
        for invalid in (
            "first_light/briefing",
            "FIRST_LIGHT//briefing",
            "UNKNOWN/briefing",
            "FULL_FLOW",
        ):
            self.assertRejected(
                lambda invalid=invalid: AUTHORITY.reconstruct_route_binding(
                    self.context,
                    "STORY_PART_UNIT",
                    invalid,
                ),
                "selector",
            )

    def test_full_flow_is_an_explicit_zero_execution_exception(self) -> None:
        claim_path, claim = self.create_session("FULL_FLOW_EXCEPTION", None)
        self.assertEqual("UNAVAILABLE_NOT_IMPLEMENTED", claim["routeBinding"]["availability"])
        self.assertFalse(claim["routeBinding"]["executionAuthorized"])
        self.assertEqual([], claim["attempts"])
        self.assertIsNone(claim["expectedAttemptOutput"])
        self.assertEqual(0, claim["attemptPolicy"]["maxAttempts"])
        self.assertFalse(claim["unavailableRouteTerminal"]["producerStarted"])
        self.assertFalse(claim["unavailableRouteTerminal"]["producerOutputReserved"])
        state = AUTHORITY.verify_session_state(REPOSITORY_ROOT, claim_path)
        self.assertEqual("ROUTE_UNAVAILABLE_NO_EXECUTION", state["terminalOutcome"])
        before = sorted(path.name for path in claim_path.parent.iterdir())
        for operation in (
            lambda: AUTHORITY.reserve_attempt(REPOSITORY_ROOT, claim_path, 1),
            lambda: AUTHORITY.write_expected_attempt_output(REPOSITORY_ROOT, claim_path, 1),
            lambda: AUTHORITY.finalize_attempt(REPOSITORY_ROOT, claim_path, 1),
        ):
            self.assertRejected(operation, "no executable attempt")
        self.assertEqual(before, sorted(path.name for path in claim_path.parent.iterdir()))
        self.assertRejected(
            lambda: AUTHORITY.reconstruct_route_binding(
                self.context,
                "FULL_FLOW_EXCEPTION",
                "FULL_FLOW",
            ),
            "does not accept a selector",
        )

    def test_retry_chain_is_append_only_and_attempt_three_exhausts_it(self) -> None:
        claim_path, claim = self.create_session("TARGETED_CHECKPOINT", "A1_NORMAL_READY")
        AUTHORITY.reserve_attempt(REPOSITORY_ROOT, claim_path, 1)
        _path, first = AUTHORITY.finalize_attempt(REPOSITORY_ROOT, claim_path, 1)
        self.assertEqual("PRODUCER_NO_OUTPUT", first["outcome"])
        self.assertTrue(first["outcomeRetryable"])
        self.assertTrue(first["nextAttemptAllowed"])
        self.assertRejected(
            lambda: AUTHORITY.write_expected_attempt_output(REPOSITORY_ROOT, claim_path, 1),
            "after terminal reservation",
        )

        _start_path, second_start = AUTHORITY.reserve_attempt(REPOSITORY_ROOT, claim_path, 2)
        self.assertEqual(first["evaluationAttemptTerminalSha256"], second_start["predecessorTerminalReceiptSha256"])
        self.write_output_bytes(claim, 2, b'{"truncated":')
        _path, second = AUTHORITY.finalize_attempt(REPOSITORY_ROOT, claim_path, 2)
        self.assertEqual("TRANSPORT_FAILURE", second["outcome"])
        self.assertTrue(second["nextAttemptAllowed"])

        _start_path, third_start = AUTHORITY.reserve_attempt(REPOSITORY_ROOT, claim_path, 3)
        self.assertEqual(second["evaluationAttemptTerminalSha256"], third_start["predecessorTerminalReceiptSha256"])
        _path, third = AUTHORITY.finalize_attempt(REPOSITORY_ROOT, claim_path, 3)
        self.assertEqual("PRODUCER_NO_OUTPUT", third["outcome"])
        self.assertTrue(third["outcomeRetryable"])
        self.assertFalse(third["nextAttemptAllowed"])
        state = AUTHORITY.verify_session_state(REPOSITORY_ROOT, claim_path)
        self.assertEqual(["TERMINAL", "TERMINAL", "TERMINAL"], [row["state"] for row in state["attempts"]])
        for ordinal in (0, 4, True, 1.0):
            self.assertRejected(
                lambda ordinal=ordinal: AUTHORITY.reserve_attempt(
                    REPOSITORY_ROOT,
                    claim_path,
                    ordinal,
                )
            )

    def test_every_valid_but_different_json_shape_is_integrity_failure(self) -> None:
        other_part = self.story_manifest["parts"][1]
        variants = (
            AUTHORITY.json_file_bytes(other_part),
            b"[]\n",
            b"true\n",
            b'{"duplicate":1,"duplicate":2}\n',
        )
        for index, raw in enumerate(variants):
            with self.subTest(index=index):
                claim_path, claim = self.create_session(
                    "STORY_PART_UNIT",
                    "FIRST_LIGHT/briefing",
                )
                AUTHORITY.reserve_attempt(REPOSITORY_ROOT, claim_path, 1)
                self.write_output_bytes(claim, 1, raw)
                _path, terminal = AUTHORITY.finalize_attempt(
                    REPOSITORY_ROOT,
                    claim_path,
                    1,
                )
                self.assertEqual("INTEGRITY_FAILURE", terminal["outcome"])
                self.assertFalse(terminal["outcomeRetryable"])
                self.assertFalse(terminal["nextAttemptAllowed"])
                self.assertRejected(
                    lambda: AUTHORITY.reserve_attempt(REPOSITORY_ROOT, claim_path, 2),
                    "does not authorize retry",
                )

    def test_snapshots_claim_last_and_semantic_mutations_fail_closed(self) -> None:
        claim_path, claim = self.create_session("STORY_PART_UNIT", "FIRST_LIGHT/briefing")
        root = claim_path.parent
        candidate_snapshot = root / "inputs" / "candidate-manifest.json"
        story_snapshot = root / "inputs" / "story-manifest.json"
        self.assertEqual(self.manifest_bytes, candidate_snapshot.read_bytes())
        self.assertEqual(self.build.story_bytes, story_snapshot.read_bytes())
        self.assertEqual({"inputs", "session-claim.json"}, {path.name for path in root.iterdir()})
        self.assertEqual("FINALIZED_BEFORE_ANY_ATTEMPT", claim["status"])
        self.assertTrue(claim["finalizationAuthority"]["claimFileIsLastCommitMarker"])
        self.assertEqual(
            "O_EXCL_FSYNC_FILE_AND_PARENT",
            claim["finalizationAuthority"]["claimWriteMode"],
        )
        self.assertEqual(self.manifest["packageAuthority"]["treeSha256"], claim["candidateAuthority"]["candidatePackageTreeSha256"])
        self.assertEqual(self.manifest["evaluatorProducerAuthority"]["filesSha256"], claim["candidateAuthority"]["candidateEvaluatorProducerFilesSha256"])

        candidate_snapshot.write_bytes(self.manifest_bytes + b" ")
        self.assertRejected(
            lambda: AUTHORITY.verify_session_claim_against_reconstructed_authority(
                REPOSITORY_ROOT,
                claim_path,
            ),
            "raw bytes",
        )

        claim_path, claim = self.create_session("STORY_PART_UNIT", "FIRST_LIGHT/briefing")
        story_snapshot = claim_path.parent / "inputs" / "story-manifest.json"
        story_snapshot.write_bytes(self.build.story_bytes + b" ")
        self.assertRejected(
            lambda: AUTHORITY.verify_session_claim_against_reconstructed_authority(
                REPOSITORY_ROOT,
                claim_path,
            ),
            "differs from candidate-bound Git bytes",
        )

        claim_path, claim = self.create_session("STORY_PART_UNIT", "FIRST_LIGHT/briefing")
        forged = copy.deepcopy(claim)
        forged["sessionNonce"] = "f" * 64
        forged["sessionClaimSha256"] = AUTHORITY.self_hash(forged, "sessionClaimSha256")
        claim_path.write_bytes(AUTHORITY.json_file_bytes(forged))
        validate_schema_subset(forged, self.schemas["claim"], self.schemas["claim"])
        self.assertRejected(
            lambda: AUTHORITY.verify_session_claim_against_reconstructed_authority(
                REPOSITORY_ROOT,
                claim_path,
            ),
            "differs from reconstructed",
        )

    def test_symlink_prepopulation_interruption_and_concurrency_fail_closed(self) -> None:
        real_parent = self.root / "real-parent"
        real_parent.mkdir()
        alias_parent = self.root / "alias-parent"
        alias_parent.symlink_to(real_parent, target_is_directory=True)
        with mock.patch.object(AUTHORITY.secrets, "token_bytes", return_value=b"x" * 32):
            self.assertRejected(
                lambda: AUTHORITY.create_session_claim(
                    self.context,
                    alias_parent / "session",
                    "TARGETED_CHECKPOINT",
                    "A1_NORMAL_READY",
                    session_authority_revision=self.session_authority_commit,
                ),
                "symlink component",
            )

        claim_path, _claim = self.create_session("TARGETED_CHECKPOINT", "A1_NORMAL_READY")
        (claim_path.parent / "attempts").mkdir()
        self.assertRejected(
            lambda: AUTHORITY.reserve_attempt(REPOSITORY_ROOT, claim_path, 1),
            "incomplete tombstone",
        )

        claim_path, claim = self.create_session("TARGETED_CHECKPOINT", "A1_NORMAL_READY")
        AUTHORITY.reserve_attempt(REPOSITORY_ROOT, claim_path, 1)
        terminal_path = Path(claim["attempts"][0]["terminalReceiptPath"])
        descriptor = AUTHORITY.reserve_zero_byte_file(terminal_path, "interrupted terminal")
        os.close(descriptor)
        self.assertRejected(
            lambda: AUTHORITY.finalize_attempt(REPOSITORY_ROOT, claim_path, 1),
            "reservation failed",
        )
        self.assertRejected(
            lambda: AUTHORITY.verify_session_state(REPOSITORY_ROOT, claim_path),
            "strict JSON",
        )

        claim_path, _claim = self.create_session("TARGETED_CHECKPOINT", "A1_NORMAL_READY")
        def reserve_once():
            try:
                AUTHORITY.reserve_attempt(REPOSITORY_ROOT, claim_path, 1)
                return "SUCCESS"
            except AUTHORITY.SessionAuthorityError:
                return "REJECTED"

        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
            outcomes = sorted(executor.map(lambda _index: reserve_once(), range(2)))
        self.assertEqual(["REJECTED", "SUCCESS"], outcomes)
        state = AUTHORITY.verify_session_state(REPOSITORY_ROOT, claim_path)
        self.assertEqual("STARTED_NOT_TERMINAL", state["attempts"][0]["state"])

    def test_exact_inventory_rejects_future_gate_artifacts(self) -> None:
        forbidden_names = (
            "evidence.json",
            "actor-result.json",
            "judge-result.json",
            "aggregate.json",
            "model-receipt.json",
            "capture.mov",
            "scorecard.json",
        )
        for route_kind, selector in (
            ("TARGETED_CHECKPOINT", "A1_NORMAL_READY"),
            ("STORY_PART_UNIT", "FIRST_LIGHT/briefing"),
            ("FULL_FLOW_EXCEPTION", None),
        ):
            claim_path, _claim = self.create_session(route_kind, selector)
            self.assertFalse(any((claim_path.parent / name).exists() for name in forbidden_names))
            (claim_path.parent / forbidden_names[0]).write_text("future gate", encoding="utf-8")
            self.assertRejected(
                lambda claim_path=claim_path: (
                    AUTHORITY.verify_session_claim_against_reconstructed_authority(
                        REPOSITORY_ROOT,
                        claim_path,
                    )
                ),
                "session root inventory drift",
            )

    def test_structural_schemas_match_receipts_but_are_not_authority(self) -> None:
        claim_path, claim = self.create_session("TARGETED_CHECKPOINT", "A1_NORMAL_READY")
        _start_path, start = AUTHORITY.reserve_attempt(REPOSITORY_ROOT, claim_path, 1)
        AUTHORITY.write_expected_attempt_output(REPOSITORY_ROOT, claim_path, 1)
        _terminal_path, terminal = AUTHORITY.finalize_attempt(
            REPOSITORY_ROOT,
            claim_path,
            1,
        )
        validate_schema_subset(claim, self.schemas["claim"], self.schemas["claim"])
        validate_schema_subset(start, self.schemas["start"], self.schemas["start"])
        validate_schema_subset(terminal, self.schemas["terminal"], self.schemas["terminal"])
        for schema in self.schemas.values():
            self.assertIn("STRUCTURAL VALIDATION ONLY", schema["$comment"])
        extra = copy.deepcopy(terminal)
        extra["futureScore"] = 100
        with self.assertRaises(AssertionError):
            validate_schema_subset(extra, self.schemas["terminal"], self.schemas["terminal"])
        self.assertFalse(claim["officialCommercialUX"])
        self.assertFalse(start["officialCommercialUX"])
        self.assertFalse(terminal["officialCommercialUX"])
        self.assertFalse(claim["scoreBearingCaptureAllowed"])
        self.assertFalse(start["scoreBearingCaptureAllowed"])
        self.assertFalse(terminal["scoreBearingCaptureAllowed"])

    def test_cli_and_function_surfaces_cannot_accept_outcome_or_route_aliases(self) -> None:
        parser = AUTHORITY.build_argument_parser()
        with self.assertRaises(SystemExit):
            parser.parse_args([
                "finalize-attempt",
                "--session-claim",
                "/tmp/not-a-claim",
                "--attempt",
                "1",
                "--outcome",
                "SUCCESS",
            ])
        self.assertNotIn("outcome", inspect.signature(AUTHORITY.finalize_attempt).parameters)
        self.assertNotIn("session_nonce", inspect.signature(AUTHORITY.create_session_claim).parameters)
        for route_kind in ("FULL_FLOW", "INTERACTIVE_NONDEFAULT_R2", "targeted_checkpoint"):
            self.assertRejected(
                lambda route_kind=route_kind: AUTHORITY.reconstruct_route_binding(
                    self.context,
                    route_kind,
                    None,
                ),
                "not authorized",
            )


if __name__ == "__main__":
    unittest.main(verbosity=2)
