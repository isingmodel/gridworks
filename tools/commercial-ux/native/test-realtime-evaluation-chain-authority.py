#!/usr/bin/env python3
"""Adversarial checks for the realtime evaluation-chain parent claim."""

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
import re
import sys
import tempfile
import unittest
from unittest import mock


SCRIPT_DIR = Path(__file__).resolve().parent
REPOSITORY_ROOT = SCRIPT_DIR.parents[2]
MODULE_PATH = SCRIPT_DIR / "realtime-evaluation-chain-authority.py"
FIXED_CANDIDATE_SOURCE_COMMIT = "379e9800c81ca315976ab4c28d511664df6ab7ed"
FIXED_SESSION_SOURCE_COMMIT = "5a31ff35a6e2d293c2f1800e4297945ecf3a5584"
FIXED_CANDIDATE_RAW_SHA256 = (
    "sha256:ca7826d38cae6e8a28e142e10e522e9c1425ba6abcec938182d9819ab0b2a816"
)
FIXED_CANDIDATE_SHA256 = (
    "sha256:373785e45a4485dfeded43466a5bff0f66de4a0c106c972262686e7a432cbdd6"
)

SPEC = importlib.util.spec_from_file_location(
    "realtime_evaluation_chain_authority_under_test",
    MODULE_PATH,
)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot load {MODULE_PATH}")
AUTHORITY = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = AUTHORITY
SPEC.loader.exec_module(AUTHORITY)
SESSION = AUTHORITY.SESSION
CANDIDATE = AUTHORITY.CANDIDATE


def _resolve_local_ref(root: dict, reference: str):
    if not reference.startswith("#/"):
        raise AssertionError(f"unsupported non-local schema reference: {reference}")
    value = root
    for raw_token in reference[2:].split("/"):
        token = raw_token.replace("~1", "/").replace("~0", "~")
        value = value[token]
    return value


def _matches_type(instance, expected_type) -> bool:
    choices = expected_type if isinstance(expected_type, list) else [expected_type]
    matches = {
        "object": isinstance(instance, dict),
        "array": isinstance(instance, list),
        "string": isinstance(instance, str),
        "integer": isinstance(instance, int) and not isinstance(instance, bool),
        "boolean": isinstance(instance, bool),
        "null": instance is None,
    }
    return any(matches.get(choice, False) for choice in choices)


def validate_schema_subset(instance, schema, root, path: str = "$") -> None:
    """Validate every Draft 2020-12 assertion used by the chain schema."""

    if schema is False:
        raise AssertionError(f"{path}: boolean false schema")
    if schema is True:
        return
    if not isinstance(schema, dict):
        raise AssertionError(f"{path}: schema must be an object or boolean")
    if "$ref" in schema:
        validate_schema_subset(
            instance,
            _resolve_local_ref(root, schema["$ref"]),
            root,
            path,
        )
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
    if expected_type is not None and not _matches_type(instance, expected_type):
        raise AssertionError(f"{path}: expected {expected_type}")
    if "const" in schema and not AUTHORITY.strict_typed_equal(
        instance,
        schema["const"],
    ):
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


class RealtimeEvaluationChainAuthorityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.stack = contextlib.ExitStack()
        cls.authority_workspace = Path(cls.stack.enter_context(
            tempfile.TemporaryDirectory(
                prefix=".realtime-chain-authority-",
                dir=REPOSITORY_ROOT,
            )
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
        cls.context = SESSION.CandidateContext(
            repository_root=REPOSITORY_ROOT.resolve(strict=True),
            manifest_path=cls.candidate_path.resolve(strict=True),
            story_manifest_path=None,
            manifest_bytes=cls.manifest_bytes,
            manifest=cls.manifest,
            story_manifest_bytes=cls.build.story_bytes,
            story_manifest=cls.story_manifest,
            semantic_verification_performed=True,
        )
        cls.chain_authority_revision = CANDIDATE.resolve_source_commit(
            REPOSITORY_ROOT,
            "HEAD",
        )
        cls.real_evaluator = None
        try:
            cls.real_evaluator = AUTHORITY.bind_chain_evaluator_authority(
                REPOSITORY_ROOT,
                cls.chain_authority_revision,
            )
        except AUTHORITY.ChainAuthorityError:
            pass
        cls.fallback_evaluator = cls._build_fallback_evaluator()
        cls.schema = json.loads(
            AUTHORITY.CLAIM_SCHEMA_PATH.read_text(encoding="utf-8")
        )

    @classmethod
    def _build_fallback_evaluator(cls) -> dict:
        rows = []
        for relative, role in AUTHORITY.CHAIN_PRODUCER_PATH_ROLES:
            data = (REPOSITORY_ROOT / relative).read_bytes()
            rows.append({
                "path": relative,
                "role": role,
                "gitMode": "100644",
                "gitObjectId": hashlib.sha1(data).hexdigest(),
                "rawSha256": AUTHORITY.sha256_bytes(data),
                "byteLength": len(data),
            })
        rows.sort(key=lambda row: row["path"])
        return {
            "schemaVersion": AUTHORITY.CHAIN_PRODUCER_SCHEMA,
            "sourceCommit": cls.chain_authority_revision,
            "fileCount": 5,
            "files": rows,
            "filesSha256": AUTHORITY.canonical_sha256(rows),
            "runningFilesMatchGitBlobs": True,
            "gitCommandAuthority": CANDIDATE.bind_git_command_authority(
                REPOSITORY_ROOT
            ),
            "parentSessionSemanticVerifierDependencyBound": True,
            "semanticVerifierEntryPoint": (
                "verify_chain_claim_against_reconstructed_authority"
            ),
            "structuralSchemaAuthority": "STRUCTURAL_ONLY_NOT_CHAIN_AUTHORITY",
        }

    @classmethod
    def tearDownClass(cls) -> None:
        cls.stack.close()

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix=".realtime-chain-test-",
            dir=REPOSITORY_ROOT,
        )
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.counter = 0

    def assertRejected(self, callable_value, fragment: str | None = None) -> None:
        with self.assertRaises(AUTHORITY.ChainAuthorityError) as captured:
            callable_value()
        if fragment is not None:
            self.assertIn(fragment, str(captured.exception))

    @contextlib.contextmanager
    def evaluator_binding(self):
        if self.real_evaluator is not None:
            yield
        else:
            with mock.patch.object(
                AUTHORITY,
                "bind_chain_evaluator_authority",
                return_value=self.fallback_evaluator,
            ):
                yield

    def create_parent(
        self,
        route_kind: str,
        selector: str | None,
        outputs: list[bytes | str] | None,
    ) -> tuple[Path, dict]:
        self.counter += 1
        path, claim = SESSION.create_session_claim(
            self.context,
            self.root / f"session-{self.counter:03d}",
            route_kind,
            selector,
            session_authority_revision=FIXED_SESSION_SOURCE_COMMIT,
        )
        for ordinal, output in enumerate(outputs or [], start=1):
            SESSION.reserve_attempt(REPOSITORY_ROOT, path, ordinal)
            if output == "EXPECTED":
                SESSION.write_expected_attempt_output(
                    REPOSITORY_ROOT,
                    path,
                    ordinal,
                )
            elif isinstance(output, bytes):
                Path(claim["attempts"][ordinal - 1]["outputPath"]).write_bytes(output)
            else:
                raise AssertionError("unknown test output fixture")
            SESSION.finalize_attempt(REPOSITORY_ROOT, path, ordinal)
        return path, claim

    def create_chain(self, session_claim: Path) -> tuple[Path, dict]:
        with self.evaluator_binding():
            return AUTHORITY.create_chain_claim(
                REPOSITORY_ROOT,
                session_claim,
                chain_authority_revision=self.chain_authority_revision,
            )

    def verify_chain(self, chain_claim: Path):
        with self.evaluator_binding():
            return AUTHORITY.verify_chain_claim_against_reconstructed_authority(
                REPOSITORY_ROOT,
                chain_claim,
            )

    def rewrite_claim(self, path: Path, claim: dict) -> None:
        claim["evaluationChainClaimSha256"] = AUTHORITY.self_hash(
            claim,
            "evaluationChainClaimSha256",
        )
        path.write_bytes(AUTHORITY.json_file_bytes(claim))

    def test_policy_schema_and_five_file_evaluator_boundary(self) -> None:
        policy, policy_bytes = AUTHORITY.load_chain_policy()
        AUTHORITY.validate_chain_policy(policy, policy_bytes)
        self.assertEqual(
            FIXED_SESSION_SOURCE_COMMIT,
            policy["parentSessionAuthority"]["sourceCommit"],
        )
        self.assertEqual(
            list(AUTHORITY.FUTURE_ARTIFACT_RELATIVE_PATHS),
            policy["futureArtifactPlan"]["orderedPaths"],
        )
        self.assertEqual(5, policy["evaluatorProducerAuthority"]["expectedFileCount"])
        if self.real_evaluator is not None:
            self.assertEqual(5, self.real_evaluator["fileCount"])
            self.assertTrue(self.real_evaluator["runningFilesMatchGitBlobs"])
        else:
            self.assertRejected(
                lambda: AUTHORITY.bind_chain_evaluator_authority(
                    REPOSITORY_ROOT,
                    self.chain_authority_revision,
                ),
                "lacks evaluator file",
            )

    def test_both_targeted_routes_snapshot_success_and_future_bar(self) -> None:
        for checkpoint in ("A1_NORMAL_READY", "A1_CONSTRUCTION_DUE_1M"):
            with self.subTest(checkpoint=checkpoint):
                parent_path, _parent = self.create_parent(
                    "TARGETED_CHECKPOINT",
                    checkpoint,
                    ["EXPECTED"],
                )
                chain_path, claim = self.create_chain(parent_path)
                boundary = claim["routeBoundary"]
                self.assertEqual(checkpoint, boundary["selector"])
                self.assertEqual(
                    list(CANDIDATE.FUTURE_EVENT_SIGNALS),
                    boundary["futureEventStatusBar"]["requiredSignals"],
                )
                self.assertEqual(
                    "EXACT_PACKAGE_TWO_CHECKPOINT_SCENE_LOAD_PASS",
                    boundary["futureEventStatusBar"]["headlessWiringStatus"],
                )
                self.assertEqual(
                    "NOT_OBSERVED",
                    boundary["futureEventStatusBar"]["nativeQualityStatus"],
                )
                self.assertFalse(boundary["nativePresentationObserved"])
                self.assertFalse(claim["officialCommercialUX"])
                self.assertIsNone(claim["commercialUXProxy"])
                self.assertEqual(7, len(claim["fixedFutureArtifactPaths"]))
                self.assertFalse(Path(claim["futureArtifactPlan"]["canonicalArtifactRoot"]).exists())
                self.verify_chain(chain_path)
                validate_schema_subset(claim, self.schema, self.schema)

    def test_story_part_is_authored_only_and_full_flow_stays_unavailable(self) -> None:
        story_parent, _ = self.create_parent(
            "STORY_PART_UNIT",
            "campaign/epilogue/promise/NORTH_BANK_PROMISE/keep",
            ["EXPECTED"],
        )
        story_chain_path, story = self.create_chain(story_parent)
        self.assertEqual("AUTHORED_CONTENT_UNIT_ONLY", story["routeBoundary"]["evidenceClass"])
        self.assertTrue(story["routeBoundary"]["authoredReachabilityOnly"])
        self.assertFalse(story["routeBoundary"]["nativeReachabilityClaim"])
        self.verify_chain(story_chain_path)
        validate_schema_subset(story, self.schema, self.schema)

        full_parent, _ = self.create_parent("FULL_FLOW_EXCEPTION", None, None)
        full_chain_path, full = self.create_chain(full_parent)
        self.assertEqual([], full["attemptAudit"])
        self.assertEqual("UNAVAILABLE_ROUTE_TERMINAL", full["selectedRouteTerminal"]["terminalKind"])
        self.assertEqual(4, full["inputSnapshot"]["fileCount"])
        self.assertFalse(full["routeBoundary"]["executionAuthorized"])
        self.verify_chain(full_chain_path)
        validate_schema_subset(full, self.schema, self.schema)

    def test_retry_prefixes_with_empty_and_malformed_outputs_reach_success(self) -> None:
        second_parent, _ = self.create_parent(
            "TARGETED_CHECKPOINT",
            "A1_NORMAL_READY",
            [b"", "EXPECTED"],
        )
        second_path, second = self.create_chain(second_parent)
        self.assertEqual(["PRODUCER_NO_OUTPUT", "SUCCESS"], [row["outcome"] for row in second["attemptAudit"]])
        self.assertIsNone(second["inputSnapshot"]["files"][4]["canonicalSha256"])
        self.assertEqual(2, second["selectedRouteTerminal"]["attemptOrdinal"])
        self.verify_chain(second_path)
        validate_schema_subset(second, self.schema, self.schema)

        third_parent, _ = self.create_parent(
            "TARGETED_CHECKPOINT",
            "A1_CONSTRUCTION_DUE_1M",
            [b"{malformed", b"", "EXPECTED"],
        )
        third_path, third = self.create_chain(third_parent)
        self.assertEqual(
            ["TRANSPORT_FAILURE", "PRODUCER_NO_OUTPUT", "SUCCESS"],
            [row["outcome"] for row in third["attemptAudit"]],
        )
        self.assertEqual(3, third["selectedRouteTerminal"]["attemptOrdinal"])
        self.verify_chain(third_path)
        validate_schema_subset(third, self.schema, self.schema)

    def test_incomplete_and_non_success_sessions_are_rejected(self) -> None:
        not_started, _ = self.create_parent(
            "TARGETED_CHECKPOINT",
            "A1_NORMAL_READY",
            [],
        )
        self.assertRejected(
            lambda: self.create_chain(not_started),
            "exactly one SUCCESS",
        )
        started, _ = self.create_parent(
            "TARGETED_CHECKPOINT",
            "A1_NORMAL_READY",
            [],
        )
        SESSION.reserve_attempt(REPOSITORY_ROOT, started, 1)
        self.assertRejected(
            lambda: self.create_chain(started),
            "non-terminal started attempt",
        )
        integrity, _ = self.create_parent(
            "TARGETED_CHECKPOINT",
            "A1_NORMAL_READY",
            [b'{"different":true}\n'],
        )
        self.assertRejected(
            lambda: self.create_chain(integrity),
            "exactly one SUCCESS",
        )

    def test_claim_snapshot_parent_and_route_mutations_fail_closed(self) -> None:
        parent_path, _ = self.create_parent(
            "TARGETED_CHECKPOINT",
            "A1_NORMAL_READY",
            ["EXPECTED"],
        )
        chain_path, claim = self.create_chain(parent_path)
        snapshot = Path(claim["canonicalChainRoot"]) / claim["inputSnapshot"]["files"][0]["snapshotRelativePath"]
        snapshot.write_bytes(snapshot.read_bytes() + b" ")
        self.assertRejected(lambda: self.verify_chain(chain_path), "snapshot differs")

        parent_path, _ = self.create_parent(
            "STORY_PART_UNIT",
            "FIRST_LIGHT/briefing",
            ["EXPECTED"],
        )
        chain_path, claim = self.create_chain(parent_path)
        forged = copy.deepcopy(claim)
        forged["routeBoundary"]["nativePresentationObserved"] = True
        forged["routeBoundary"]["routeBoundarySha256"] = AUTHORITY.self_hash(
            forged["routeBoundary"],
            "routeBoundarySha256",
        )
        self.rewrite_claim(chain_path, forged)
        self.assertRejected(lambda: self.verify_chain(chain_path), "reconstructed authority")

        parent_path, parent = self.create_parent(
            "TARGETED_CHECKPOINT",
            "A1_CONSTRUCTION_DUE_1M",
            ["EXPECTED"],
        )
        chain_path, _claim = self.create_chain(parent_path)
        Path(parent["attempts"][0]["outputPath"]).write_bytes(b"{}\n")
        self.assertRejected(lambda: self.verify_chain(chain_path))

    def test_exact_inventory_rejects_artifacts_empty_dirs_and_symlinks(self) -> None:
        for mutation in ("artifact", "empty-dir", "symlink"):
            with self.subTest(mutation=mutation):
                parent_path, _ = self.create_parent(
                    "TARGETED_CHECKPOINT",
                    "A1_NORMAL_READY",
                    ["EXPECTED"],
                )
                chain_path, claim = self.create_chain(parent_path)
                root = Path(claim["canonicalChainRoot"])
                if mutation == "artifact":
                    (root / "artifacts").mkdir()
                elif mutation == "empty-dir":
                    (root / "inputs" / "session" / "extra").mkdir()
                else:
                    os.symlink(root / "inputs", root / "inputs" / "session" / "alias")
                self.assertRejected(lambda: self.verify_chain(chain_path))

    def test_deterministic_sibling_is_single_use_and_claim_is_last(self) -> None:
        parent_path, parent = self.create_parent(
            "TARGETED_CHECKPOINT",
            "A1_NORMAL_READY",
            ["EXPECTED"],
        )
        chain_path, claim = self.create_chain(parent_path)
        expected_root = Path(f"{parent['canonicalSessionRoot']}.evaluation-chain-v1")
        self.assertEqual(expected_root, Path(claim["canonicalChainRoot"]))
        self.assertEqual(chain_path, expected_root / "evaluation-chain-claim.json")
        self.assertEqual({"inputs", "evaluation-chain-claim.json"}, {value.name for value in expected_root.iterdir()})
        self.assertRejected(lambda: self.create_chain(parent_path), "exclusive create failed")

        interrupted_parent, parent = self.create_parent(
            "TARGETED_CHECKPOINT",
            "A1_NORMAL_READY",
            ["EXPECTED"],
        )
        real_write = AUTHORITY.exclusive_write

        def fail_claim(path: Path, data: bytes, label: str) -> None:
            if path.name == "evaluation-chain-claim.json":
                raise AUTHORITY.ChainAuthorityError("injected claim interruption")
            real_write(path, data, label)

        with mock.patch.object(AUTHORITY, "exclusive_write", side_effect=fail_claim):
            self.assertRejected(
                lambda: self.create_chain(interrupted_parent),
                "injected claim interruption",
            )
        interrupted_root = Path(f"{parent['canonicalSessionRoot']}.evaluation-chain-v1")
        self.assertFalse((interrupted_root / "evaluation-chain-claim.json").exists())
        self.assertRejected(lambda: self.create_chain(interrupted_parent), "exclusive create failed")

    def test_concurrent_creation_has_one_winner(self) -> None:
        parent_path, _ = self.create_parent(
            "TARGETED_CHECKPOINT",
            "A1_NORMAL_READY",
            ["EXPECTED"],
        )

        def create_once():
            try:
                return ("ok", AUTHORITY.create_chain_claim(
                    REPOSITORY_ROOT,
                    parent_path,
                    chain_authority_revision=self.chain_authority_revision,
                )[0])
            except AUTHORITY.ChainAuthorityError as error:
                return ("error", str(error))

        with self.evaluator_binding():
            with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
                results = list(executor.map(lambda _value: create_once(), range(2)))
        self.assertEqual(1, sum(kind == "ok" for kind, _value in results))
        self.assertEqual(1, sum(kind == "error" for kind, _value in results))

    def test_schema_only_forgery_and_score_fields_never_open_authority(self) -> None:
        parent_path, _ = self.create_parent(
            "TARGETED_CHECKPOINT",
            "A1_NORMAL_READY",
            ["EXPECTED"],
        )
        chain_path, claim = self.create_chain(parent_path)
        validate_schema_subset(claim, self.schema, self.schema)
        forged = copy.deepcopy(claim)
        forged["officialCommercialUX"] = True
        forged["commercialUXProxy"] = 99
        with self.assertRaises(AssertionError):
            validate_schema_subset(forged, self.schema, self.schema)
        self.rewrite_claim(chain_path, forged)
        self.assertRejected(lambda: self.verify_chain(chain_path), "reconstructed authority")

    def test_production_api_has_no_nonce_or_artifact_relaxation(self) -> None:
        create_signature = inspect.signature(AUTHORITY.create_chain_claim)
        verify_signature = inspect.signature(
            AUTHORITY.verify_chain_claim_against_reconstructed_authority
        )
        self.assertNotIn("chain_nonce", create_signature.parameters)
        self.assertNotIn("allow_artifact_root", verify_signature.parameters)
        parser = AUTHORITY.build_argument_parser()
        with self.assertRaises(SystemExit):
            parser.parse_args([
                "create-chain",
                "--session-claim",
                "/tmp/session-claim.json",
                "--chain-nonce",
                "00" * 32,
            ])


if __name__ == "__main__":
    unittest.main(verbosity=2)
