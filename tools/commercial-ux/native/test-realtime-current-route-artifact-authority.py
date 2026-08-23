#!/usr/bin/env python3
"""Adversarial checks for the finalized blocked current-route artifact chain."""

from __future__ import annotations

import concurrent.futures
import contextlib
import copy
import importlib.util
import inspect
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock


SCRIPT_DIR = Path(__file__).resolve().parent
REPOSITORY_ROOT = SCRIPT_DIR.parents[2]
MODULE_PATH = SCRIPT_DIR / "realtime-current-route-artifact-authority.py"
FIXED_CANDIDATE_SOURCE_COMMIT = "379e9800c81ca315976ab4c28d511664df6ab7ed"
FIXED_SESSION_SOURCE_COMMIT = "5a31ff35a6e2d293c2f1800e4297945ecf3a5584"
FIXED_CHAIN_SOURCE_COMMIT = "74ba7256766f41c1398fba98f59c1c942a4cb96e"
FIXED_CANDIDATE_RAW_SHA256 = (
    "sha256:ca7826d38cae6e8a28e142e10e522e9c1425ba6abcec938182d9819ab0b2a816"
)
FIXED_CANDIDATE_SHA256 = (
    "sha256:373785e45a4485dfeded43466a5bff0f66de4a0c106c972262686e7a432cbdd6"
)

SPEC = importlib.util.spec_from_file_location(
    "realtime_current_route_artifact_authority_under_test",
    MODULE_PATH,
)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot load {MODULE_PATH}")
AUTHORITY = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = AUTHORITY
SPEC.loader.exec_module(AUTHORITY)
CHAIN = AUTHORITY.CHAIN
SESSION = AUTHORITY.SESSION
CANDIDATE = AUTHORITY.CANDIDATE


class RealtimeCurrentRouteArtifactAuthorityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.stack = contextlib.ExitStack()
        cls.authority_workspace = Path(cls.stack.enter_context(
            tempfile.TemporaryDirectory(
                prefix=".realtime-artifact-authority-",
                dir=REPOSITORY_ROOT,
            )
        ))
        cls.build = cls.stack.enter_context(CANDIDATE.isolated_managed_build(
            REPOSITORY_ROOT,
            revision=FIXED_CANDIDATE_SOURCE_COMMIT,
        ))
        candidate_policy, candidate_policy_bytes = CANDIDATE.load_policy()
        godot_app_root = CANDIDATE.default_godot_app(REPOSITORY_ROOT)
        engine_rows, engine_sha256 = CANDIDATE.bind_engine_tree(godot_app_root)
        headless = CANDIDATE.run_headless_execution_authority(
            cls.build,
            godot_app_root,
            engine_rows,
            engine_sha256,
        )
        cls.manifest = CANDIDATE.build_manifest(
            cls.build,
            engine_rows,
            engine_sha256,
            headless,
            candidate_policy,
            candidate_policy_bytes,
        )
        CANDIDATE.verify_manifest_against_reconstructed_authority(
            cls.manifest,
            cls.build,
            godot_app_root,
            candidate_policy,
            candidate_policy_bytes,
        )
        cls.manifest_bytes = CHAIN.json_file_bytes(cls.manifest)
        if CHAIN.sha256_bytes(cls.manifest_bytes) != FIXED_CANDIDATE_RAW_SHA256:
            raise AssertionError("fixed candidate raw bytes drifted during setup")
        if cls.manifest["candidateSha256"] != FIXED_CANDIDATE_SHA256:
            raise AssertionError("fixed candidate self-hash drifted during setup")
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
        cls.artifact_authority_revision = CANDIDATE.resolve_source_commit(
            REPOSITORY_ROOT,
            "HEAD",
        )
        cls.evaluator = AUTHORITY.bind_artifact_evaluator_authority(
            REPOSITORY_ROOT,
            cls.artifact_authority_revision,
        )
        cls.schemas = {
            kind: json.loads(path.read_text(encoding="utf-8"))
            for kind, path in AUTHORITY.SCHEMA_PATHS.items()
        }

    @classmethod
    def tearDownClass(cls) -> None:
        cls.stack.close()

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix=".realtime-artifact-test-",
            dir=REPOSITORY_ROOT,
        )
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.counter = 0

    def assertRejected(self, callable_value, fragment: str | None = None) -> None:
        with self.assertRaises(
            AUTHORITY.CurrentRouteArtifactAuthorityError
        ) as captured:
            callable_value()
        if fragment is not None:
            self.assertIn(fragment, str(captured.exception))

    def create_session(
        self,
        route_kind: str,
        selector: str | None,
        outputs: list[bytes | str] | None,
    ) -> Path:
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
                Path(claim["attempts"][ordinal - 1]["outputPath"]).write_bytes(
                    output
                )
            else:
                raise AssertionError("unknown output fixture")
            SESSION.finalize_attempt(REPOSITORY_ROOT, path, ordinal)
        return path

    def create_parent_chain(
        self,
        route_kind: str,
        selector: str | None,
        outputs: list[bytes | str] | None,
    ) -> tuple[Path, dict]:
        session = self.create_session(route_kind, selector, outputs)
        return CHAIN.create_chain_claim(
            REPOSITORY_ROOT,
            session,
            chain_authority_revision=FIXED_CHAIN_SOURCE_COMMIT,
        )

    def create_artifacts(self, chain_path: Path) -> tuple[Path, dict]:
        return AUTHORITY.create_current_route_artifact_chain(
            REPOSITORY_ROOT,
            chain_path,
            artifact_authority_revision=self.artifact_authority_revision,
        )

    def verify_artifacts(self, aggregate_path: Path):
        return (
            AUTHORITY.verify_current_route_artifact_chain_against_reconstructed_authority(
                REPOSITORY_ROOT,
                aggregate_path,
            )
        )

    def artifact_objects(self, parent: dict) -> list[dict]:
        return [
            json.loads(Path(path).read_text(encoding="utf-8"))
            for path in parent["fixedFutureArtifactPaths"]
        ]

    def rewrite_chain_claim(self, path: Path, claim: dict) -> None:
        claim["evaluationChainClaimSha256"] = CHAIN.self_hash(
            claim,
            "evaluationChainClaimSha256",
        )
        path.write_bytes(CHAIN.json_file_bytes(claim))

    def rewrite_artifact_suffix(
        self,
        parent: dict,
        start_index: int,
        mutation,
    ) -> None:
        paths = [Path(path) for path in parent["fixedFutureArtifactPaths"]]
        values = [json.loads(path.read_text(encoding="utf-8")) for path in paths]
        prior = []
        for index, (path, value) in enumerate(zip(paths, values)):
            if index == start_index:
                mutation(value)
            if index >= start_index:
                value["priorArtifacts"] = copy.deepcopy(prior)
                value["priorArtifactsTreeSha256"] = AUTHORITY.canonical_sha256(
                    prior
                )
                if value["artifactKind"] == "AGGREGATE":
                    value["payload"]["upstreamArtifacts"] = copy.deepcopy(prior)
                value["artifactSha256"] = AUTHORITY.self_hash(value)
                data = AUTHORITY.json_file_bytes(value)
                path.write_bytes(data)
            else:
                data = path.read_bytes()
            if value["artifactKind"] != "AGGREGATE":
                prior.append(AUTHORITY._artifact_row(value, data))

    def _find_ajv_root(self) -> Path:
        candidates = sorted(
            Path.home().glob(".npm/_npx/*/node_modules"),
            reverse=True,
        )
        for root in candidates:
            package = root / "ajv" / "package.json"
            module = root / "ajv" / "dist" / "2020.js"
            if package.is_file() and module.is_file():
                version = json.loads(package.read_text(encoding="utf-8"))["version"]
                if version == "8.20.0":
                    return root
        raise AssertionError("AJV 8.20.0 Draft 2020-12 runtime is unavailable")

    def assert_ajv_valid_and_path_near_miss_invalid(
        self,
        artifacts: list[dict],
    ) -> None:
        fixture = self.root / "ajv-artifact-fixtures.json"
        rows = []
        for artifact in artifacts:
            invalid = copy.deepcopy(artifact)
            invalid["canonicalArtifactPath"] += ".near-miss"
            rows.append({
                "schema": str(AUTHORITY.SCHEMA_PATHS[artifact["artifactKind"]]),
                "valid": artifact,
                "invalid": invalid,
            })
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
    console.error(row.schema, validate.errors);
    process.exit(2);
  }
  if (validate(row.invalid)) {
    console.error('near-miss path unexpectedly valid', row.schema);
    process.exit(3);
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

    def assert_ajv_representative_mutations_invalid(
        self,
        artifacts: list[dict],
    ) -> None:
        mutations: list[tuple[int, object]] = []
        extra = copy.deepcopy(artifacts[6])
        extra["unexpected"] = None
        mutations.append((6, extra))
        confused = copy.deepcopy(artifacts[1])
        confused["payload"]["actorInvocationCountByThisAuthority"] = "0"
        mutations.append((1, confused))
        status = copy.deepcopy(artifacts[3])
        status["status"] = "PASS"
        mutations.append((3, status))
        ordinal = copy.deepcopy(artifacts[4])
        ordinal["artifactOrdinal"] = 4
        mutations.append((4, ordinal))
        missing_signal = copy.deepcopy(artifacts[0])
        del missing_signal["payload"]["routeBoundary"]["futureEventStatusBar"][
            "requiredSignals"
        ][0]
        mutations.append((0, missing_signal))
        reordered_signal = copy.deepcopy(artifacts[0])
        signals = reordered_signal["payload"]["routeBoundary"][
            "futureEventStatusBar"
        ]["requiredSignals"]
        signals[0], signals[1] = signals[1], signals[0]
        mutations.append((0, reordered_signal))
        fixture = self.root / "ajv-negative-artifact-fixtures.json"
        fixture.write_text(json.dumps([
            {
                "schema": str(AUTHORITY.SCHEMA_PATHS[artifacts[index]["artifactKind"]]),
                "invalid": invalid,
            }
            for index, invalid in mutations
        ]), encoding="utf-8")
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
  if (validate(row.invalid)) {
    console.error('representative mutation unexpectedly valid', row.schema);
    process.exit(4);
  }
}
"""
        completed = subprocess.run(
            ["node", "-e", script, str(self._find_ajv_root()), str(fixture)],
            cwd=REPOSITORY_ROOT,
            capture_output=True,
            text=True,
            check=False,
        )
        self.assertEqual(0, completed.returncode, completed.stderr)

    def test_three_routes_retry_ordinals_future_bar_and_all_schema_instances(self) -> None:
        fixtures = [
            ("TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"], 1),
            (
                "TARGETED_CHECKPOINT",
                "A1_CONSTRUCTION_DUE_1M",
                [b"", "EXPECTED"],
                2,
            ),
            (
                "STORY_PART_UNIT",
                "campaign/epilogue/promise/NORTH_BANK_PROMISE/keep",
                [b"{malformed", b"", "EXPECTED"],
                3,
            ),
            ("FULL_FLOW_EXCEPTION", None, None, None),
        ]
        all_route_artifacts: list[dict] = []
        targeted_artifacts: list[dict] | None = None
        for route_kind, selector, outputs, selected_ordinal in fixtures:
            with self.subTest(route=route_kind, selector=selector):
                chain_path, parent = self.create_parent_chain(
                    route_kind,
                    selector,
                    outputs,
                )
                CHAIN.verify_chain_claim_against_reconstructed_authority(
                    REPOSITORY_ROOT,
                    chain_path,
                )
                aggregate_path, aggregate = self.create_artifacts(chain_path)
                verified, aggregate_raw_sha256 = self.verify_artifacts(
                    aggregate_path
                )
                self.assertTrue(AUTHORITY.strict_typed_equal(aggregate, verified))
                self.assertEqual(
                    AUTHORITY.sha256_bytes(aggregate_path.read_bytes()),
                    aggregate_raw_sha256,
                )
                artifacts = self.artifact_objects(parent)
                self.assertEqual(list(range(1, 8)), [a["artifactOrdinal"] for a in artifacts])
                self.assertEqual(
                    [row["status"] for row in AUTHORITY.load_artifact_policy()[0]["orderedArtifacts"]],
                    [a["status"] for a in artifacts],
                )
                evidence = artifacts[0]["payload"]
                self.assertEqual(route_kind, evidence["routeBoundary"]["routeKind"])
                self.assertEqual(selected_ordinal, evidence["selectedRouteTerminal"]["attemptOrdinal"])
                self.assertEqual(0, evidence["boundNativeEvidenceItemCount"])
                self.assertFalse(evidence["nativeCaptureAttemptedByThisAuthority"])
                if route_kind == "TARGETED_CHECKPOINT":
                    self.assertEqual(
                        list(CANDIDATE.FUTURE_EVENT_SIGNALS),
                        evidence["routeBoundary"]["futureEventStatusBar"]["requiredSignals"],
                    )
                    self.assertEqual(
                        "NOT_OBSERVED",
                        evidence["routeBoundary"]["futureEventStatusBar"]["nativeQualityStatus"],
                    )
                elif route_kind == "STORY_PART_UNIT":
                    self.assertEqual(
                        "AUTHORED_CONTENT_UNIT_ONLY",
                        evidence["routeBoundary"]["evidenceClass"],
                    )
                    self.assertFalse(evidence["routeBoundary"]["nativeReachabilityClaim"])
                else:
                    self.assertEqual([], evidence["attemptAudit"])
                    self.assertEqual(4, evidence["inputSnapshot"]["fileCount"])
                    self.assertEqual(1, len(evidence["selectedRouteMaterialPaths"]))
                all_route_artifacts.extend(artifacts)
                if targeted_artifacts is None:
                    targeted_artifacts = artifacts
        self.assertEqual(28, len(all_route_artifacts))
        self.assert_ajv_valid_and_path_near_miss_invalid(all_route_artifacts)
        assert targeted_artifacts is not None
        self.assert_ajv_representative_mutations_invalid(targeted_artifacts)

    def test_exact_blocked_payloads_are_non_score_and_roles_are_distinct(self) -> None:
        chain_path, parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT",
            "A1_NORMAL_READY",
            ["EXPECTED"],
        )
        aggregate_path, _aggregate = self.create_artifacts(chain_path)
        artifacts = self.artifact_objects(parent)
        for artifact in artifacts:
            self.assertFalse(artifact["officialCommercialUX"])
            self.assertFalse(artifact["scoreBearingCaptureAllowed"])
            self.assertIsNone(artifact["commercialUXProxy"])
            self.assertFalse(artifact["artifactProducerExecutionAttested"])
        actor, judge_input, judge, verifier, oracle, aggregate = [
            artifact["payload"] for artifact in artifacts[1:]
        ]
        self.assertEqual(0, actor["actorInvocationCountByThisAuthority"])
        self.assertIsNone(actor["actorResult"])
        self.assertFalse(judge_input["executableJudgeInput"])
        self.assertEqual("gpt-5.6-sol", judge_input["futureModelRequirement"]["model"])
        self.assertEqual("ultra", judge["futureModelRequirement"]["reasoningEffort"])
        self.assertFalse(judge["modelExecutionAuthorized"])
        self.assertIsNone(judge["modelExecutionReceipt"])
        self.assertIsNone(judge["judgment"])
        self.assertFalse(verifier["artifactSemanticVerifierIsEvidenceVerifier"])
        self.assertFalse(verifier["evidenceVerifierExecutedByThisAuthority"])
        self.assertIsNone(verifier["unsupportedEvidenceClaimCount"])
        self.assertTrue(oracle["artifactIntegrityChecksAreNotProductOracleEvidence"])
        self.assertFalse(oracle["productOracleExecutedByThisAuthority"])
        self.assertFalse(oracle["hardGatesEvaluatedByThisAuthority"])
        self.assertEqual([], oracle["ledgerRows"])
        self.assertFalse(aggregate["scoreAggregationPerformedByThisAuthority"])
        self.assertEqual("UNAVAILABLE", aggregate["modelExecutionStatus"])
        self.assertEqual("NOT_EVALUATED", aggregate["hardGateStatus"])
        self.assertIsNone(aggregate["verdict"])
        self.verify_artifacts(aggregate_path)

    def test_downstream_rehashed_forgery_and_structural_only_path_forgery_fail(self) -> None:
        chain_path, parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT",
            "A1_NORMAL_READY",
            ["EXPECTED"],
        )
        aggregate_path, _ = self.create_artifacts(chain_path)
        self.rewrite_artifact_suffix(
            parent,
            0,
            lambda evidence: evidence["payload"].__setitem__(
                "nativeCaptureAttemptedByThisAuthority",
                True,
            ),
        )
        self.assertRejected(lambda: self.verify_artifacts(aggregate_path))

        signal_mutations = {
            "delete": lambda signals: signals.pop(0),
            "reorder": lambda signals: signals.__setitem__(
                slice(0, 2),
                [signals[1], signals[0]],
            ),
            "change": lambda signals: signals.__setitem__(0, "FAKE_SIGNAL"),
            "add": lambda signals: signals.append("FAKE_SIGNAL"),
        }
        for name, signal_mutation in signal_mutations.items():
            with self.subTest(future_signal_mutation=name):
                chain_path, parent = self.create_parent_chain(
                    "TARGETED_CHECKPOINT",
                    "A1_NORMAL_READY",
                    ["EXPECTED"],
                )
                aggregate_path, _ = self.create_artifacts(chain_path)

                def mutate_signals(evidence, operation=signal_mutation):
                    operation(evidence["payload"]["routeBoundary"][
                        "futureEventStatusBar"
                    ]["requiredSignals"])
                    evidence["payload"]["routeBoundary"][
                        "routeBoundarySha256"
                    ] = CHAIN.self_hash(
                        evidence["payload"]["routeBoundary"],
                        "routeBoundarySha256",
                    )

                self.rewrite_artifact_suffix(parent, 0, mutate_signals)
                self.assertRejected(lambda: self.verify_artifacts(aggregate_path))

        chain_path, parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT",
            "A1_NORMAL_READY",
            ["EXPECTED"],
        )
        aggregate_path, _ = self.create_artifacts(chain_path)
        arbitrary = "/tmp/schema-valid-but-unbound-terminal.json"
        self.rewrite_artifact_suffix(
            parent,
            0,
            lambda evidence: evidence["payload"]["selectedRouteMaterialPaths"].__setitem__(
                0,
                arbitrary,
            ),
        )
        self.assertRejected(lambda: self.verify_artifacts(aggregate_path))

    def test_cross_chain_swap_exact_inventory_symlink_and_hardlink_fail(self) -> None:
        first_chain, first_parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
        )
        first_aggregate, _ = self.create_artifacts(first_chain)
        second_chain, second_parent = self.create_parent_chain(
            "STORY_PART_UNIT", "FIRST_LIGHT/briefing", ["EXPECTED"]
        )
        _second_aggregate, _ = self.create_artifacts(second_chain)
        first_actor = Path(first_parent["fixedFutureArtifactPaths"][1])
        second_actor = Path(second_parent["fixedFutureArtifactPaths"][1])
        first_actor.write_bytes(second_actor.read_bytes())
        self.assertRejected(lambda: self.verify_artifacts(first_aggregate))

        chain_path, parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
        )
        aggregate_path, _ = self.create_artifacts(chain_path)
        artifact_root = aggregate_path.parent
        (artifact_root / "extra.json").write_bytes(b"{}\n")
        self.assertRejected(lambda: self.verify_artifacts(aggregate_path), "inventory")

        chain_path, parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
        )
        aggregate_path, _ = self.create_artifacts(chain_path)
        actor_path = Path(parent["fixedFutureArtifactPaths"][1])
        actor_bytes = actor_path.read_bytes()
        actor_path.unlink()
        target = self.root / "actor-target.json"
        target.write_bytes(actor_bytes)
        os.symlink(target, actor_path)
        self.assertRejected(lambda: self.verify_artifacts(aggregate_path))

        chain_path, parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
        )
        aggregate_path, _ = self.create_artifacts(chain_path)
        evidence_path = Path(parent["fixedFutureArtifactPaths"][0])
        os.link(evidence_path, self.root / "evidence-hardlink.json")
        self.assertRejected(lambda: self.verify_artifacts(aggregate_path), "link count one")

    def test_partial_interruption_poison_and_concurrent_single_winner(self) -> None:
        chain_path, parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
        )
        real_write = AUTHORITY.exclusive_write

        def fail_judge(path: Path, data: bytes, label: str) -> None:
            if label == "current route artifact JUDGE_TERMINAL":
                raise AUTHORITY.CurrentRouteArtifactAuthorityError(
                    "injected artifact interruption"
                )
            real_write(path, data, label)

        with mock.patch.object(AUTHORITY, "exclusive_write", side_effect=fail_judge):
            self.assertRejected(
                lambda: self.create_artifacts(chain_path),
                "injected artifact interruption",
            )
        self.assertFalse(Path(parent["fixedFutureArtifactPaths"][6]).exists())
        self.assertRejected(lambda: self.create_artifacts(chain_path))

        chain_path, _parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_CONSTRUCTION_DUE_1M", ["EXPECTED"]
        )

        def create_once():
            try:
                return ("ok", self.create_artifacts(chain_path)[0])
            except AUTHORITY.CurrentRouteArtifactAuthorityError as error:
                return ("error", str(error))

        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
            results = list(executor.map(lambda _value: create_once(), range(2)))
        self.assertEqual(1, sum(kind == "ok" for kind, _value in results))
        self.assertEqual(1, sum(kind == "error" for kind, _value in results))

    def test_every_artifact_write_boundary_and_final_fsync_are_crash_safe(self) -> None:
        policy = AUTHORITY.load_artifact_policy()[0]
        real_write = AUTHORITY.exclusive_write
        for descriptor in policy["orderedArtifacts"]:
            for phase in ("before", "after"):
                with self.subTest(kind=descriptor["kind"], phase=phase):
                    chain_path, parent = self.create_parent_chain(
                        "TARGETED_CHECKPOINT",
                        "A1_NORMAL_READY",
                        ["EXPECTED"],
                    )
                    target_label = (
                        f"current route artifact {descriptor['kind']}"
                    )

                    def interrupt(
                        path: Path,
                        data: bytes,
                        label: str,
                        *,
                        expected_label: str = target_label,
                        expected_phase: str = phase,
                    ) -> None:
                        if label == expected_label and expected_phase == "before":
                            raise AUTHORITY.CurrentRouteArtifactAuthorityError(
                                "injected pre-write interruption"
                            )
                        real_write(path, data, label)
                        if label == expected_label and expected_phase == "after":
                            raise AUTHORITY.CurrentRouteArtifactAuthorityError(
                                "injected post-write interruption"
                            )

                    with mock.patch.object(
                        AUTHORITY,
                        "exclusive_write",
                        side_effect=interrupt,
                    ):
                        self.assertRejected(lambda: self.create_artifacts(chain_path))
                    aggregate_path = Path(parent["fixedFutureArtifactPaths"][6])
                    aggregate_was_committed = (
                        descriptor["kind"] == "AGGREGATE" and phase == "after"
                    )
                    self.assertEqual(aggregate_was_committed, aggregate_path.exists())
                    if aggregate_was_committed:
                        self.verify_artifacts(aggregate_path)
                    else:
                        self.assertRejected(
                            lambda: self.verify_artifacts(aggregate_path)
                        )
                    self.assertRejected(
                        lambda: self.create_artifacts(chain_path),
                    )

        chain_path, parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT",
            "A1_NORMAL_READY",
            ["EXPECTED"],
        )
        with mock.patch.object(
            AUTHORITY,
            "fsync_directory",
            side_effect=OSError("injected artifact-root fsync failure"),
        ):
            self.assertRejected(lambda: self.create_artifacts(chain_path))
        aggregate_path = Path(parent["fixedFutureArtifactPaths"][6])
        self.assertTrue(aggregate_path.exists())
        self.verify_artifacts(aggregate_path)
        self.assertRejected(
            lambda: self.create_artifacts(chain_path),
        )

    def test_lock_write_order_prefix_race_and_second_pass_race_fail_closed(self) -> None:
        chain_path, parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
        )
        real_lock = SESSION.exclusive_claim_lock
        real_write = AUTHORITY.exclusive_write
        state = {"held": False}
        writes = []

        @contextlib.contextmanager
        def observed_lock(path: Path):
            with real_lock(path):
                state["held"] = True
                try:
                    yield
                finally:
                    state["held"] = False

        def observed_write(path: Path, data: bytes, label: str) -> None:
            self.assertTrue(state["held"])
            writes.append(path.name)
            real_write(path, data, label)

        with mock.patch.object(SESSION, "exclusive_claim_lock", observed_lock):
            with mock.patch.object(AUTHORITY, "exclusive_write", side_effect=observed_write):
                aggregate_path, _ = self.create_artifacts(chain_path)
        self.assertEqual(
            [Path(path).name for path in parent["fixedFutureArtifactPaths"]],
            writes,
        )
        self.assertEqual("aggregate.json", writes[-1])
        self.verify_artifacts(aggregate_path)

        chain_path, parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
        )

        def mutate_prefix(path: Path, data: bytes, label: str) -> None:
            real_write(path, data, label)
            if label == "current route artifact ACTOR_TERMINAL":
                evidence = Path(parent["fixedFutureArtifactPaths"][0])
                evidence.write_bytes(evidence.read_bytes() + b" ")

        with mock.patch.object(AUTHORITY, "exclusive_write", side_effect=mutate_prefix):
            self.assertRejected(lambda: self.create_artifacts(chain_path))
        self.assertFalse(Path(parent["fixedFutureArtifactPaths"][6]).exists())

        chain_path, parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
        )
        aggregate_path, _ = self.create_artifacts(chain_path)
        real_read = AUTHORITY._read_canonical_artifact
        mutated = {"done": False}

        def mutate_during_second_pass(path: Path, label: str):
            result = real_read(path, label)
            if label.startswith("final current route artifact EVIDENCE") and not mutated["done"]:
                actor = Path(parent["fixedFutureArtifactPaths"][1])
                actor.write_bytes(actor.read_bytes() + b" ")
                mutated["done"] = True
            return result

        with mock.patch.object(
            AUTHORITY,
            "_read_canonical_artifact",
            side_effect=mutate_during_second_pass,
        ):
            self.assertRejected(lambda: self.verify_artifacts(aggregate_path))

        chain_path, parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
        )
        aggregate_path, _ = self.create_artifacts(chain_path)
        real_inventory = AUTHORITY._validate_artifact_inventory
        injected = {"done": False}

        def inject_extra_after_initial_inventory(parent_context, expected_count):
            result = real_inventory(parent_context, expected_count)
            if expected_count == 7 and not injected["done"]:
                (result / "extra.json").write_bytes(b"{}\n")
                injected["done"] = True
            return result

        with mock.patch.object(
            AUTHORITY,
            "_validate_artifact_inventory",
            side_effect=inject_extra_after_initial_inventory,
        ):
            self.assertRejected(
                lambda: self.verify_artifacts(aggregate_path),
                "inventory",
            )

        chain_path, _parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
        )
        aggregate_path, _ = self.create_artifacts(chain_path)
        real_directory = AUTHORITY.canonical_existing_directory
        artifact_root_calls = {"count": 0}

        def remove_root_before_final_iterdir(path: Path, label: str):
            result = real_directory(path, label)
            if label == "artifact root":
                artifact_root_calls["count"] += 1
                if artifact_root_calls["count"] == 2:
                    result.rename(result.with_name("artifacts-moved"))
            return result

        with mock.patch.object(
            AUTHORITY,
            "canonical_existing_directory",
            side_effect=remove_root_before_final_iterdir,
        ):
            self.assertRejected(
                lambda: self.verify_artifacts(aggregate_path),
                "filesystem changed during",
            )

    def test_parent_and_producer_final_races_fail_closed(self) -> None:
        chain_path, parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
        )
        real_verify = AUTHORITY._verify_artifacts_without_lock

        def mutate_parent_after_artifacts(parent_context, producer):
            result = real_verify(parent_context, producer)
            snapshot = (
                Path(parent["canonicalChainRoot"])
                / parent["inputSnapshot"]["files"][0]["snapshotRelativePath"]
            )
            snapshot.write_bytes(snapshot.read_bytes() + b" ")
            return result

        with mock.patch.object(
            AUTHORITY,
            "_verify_artifacts_without_lock",
            side_effect=mutate_parent_after_artifacts,
        ):
            self.assertRejected(lambda: self.create_artifacts(chain_path))

        verify_chain, verify_parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
        )
        verify_aggregate, _ = self.create_artifacts(verify_chain)

        def mutate_parent_during_public_verify(parent_context, producer):
            result = real_verify(parent_context, producer)
            snapshot = (
                Path(verify_parent["canonicalChainRoot"])
                / verify_parent["inputSnapshot"]["files"][0]["snapshotRelativePath"]
            )
            snapshot.write_bytes(snapshot.read_bytes() + b" ")
            return result

        with mock.patch.object(
            AUTHORITY,
            "_verify_artifacts_without_lock",
            side_effect=mutate_parent_during_public_verify,
        ):
            self.assertRejected(lambda: self.verify_artifacts(verify_aggregate))

        chain_path, parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
        )
        producer_verify_chain, _producer_verify_parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_CONSTRUCTION_DUE_1M", ["EXPECTED"]
        )
        producer_verify_aggregate, _ = self.create_artifacts(
            producer_verify_chain
        )
        final_rebind_chain, final_rebind_parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
        )
        calls = {"count": 0}

        def fail_rebind(_root, _revision):
            calls["count"] += 1
            if calls["count"] == 1:
                return self.evaluator
            raise AUTHORITY.CurrentRouteArtifactAuthorityError(
                "injected schema/source drift after binding"
            )

        with mock.patch.object(
            AUTHORITY,
            "bind_artifact_evaluator_authority",
            side_effect=fail_rebind,
        ):
            self.assertRejected(
                lambda: self.create_artifacts(chain_path),
                "injected schema/source drift",
            )
        self.assertFalse(Path(parent["fixedFutureArtifactPaths"][6]).exists())

        final_calls = {"count": 0}

        def fail_final_create_rebind(_root, _revision):
            final_calls["count"] += 1
            if final_calls["count"] < 3:
                return self.evaluator
            raise AUTHORITY.CurrentRouteArtifactAuthorityError(
                "injected final create source drift"
            )

        with mock.patch.object(
            AUTHORITY,
            "bind_artifact_evaluator_authority",
            side_effect=fail_final_create_rebind,
        ):
            self.assertRejected(
                lambda: self.create_artifacts(final_rebind_chain),
                "injected final create source drift",
            )
        final_rebind_aggregate = Path(
            final_rebind_parent["fixedFutureArtifactPaths"][6]
        )
        self.assertTrue(final_rebind_aggregate.exists())
        with mock.patch.object(
            AUTHORITY,
            "bind_artifact_evaluator_authority",
            return_value=self.evaluator,
        ):
            self.verify_artifacts(final_rebind_aggregate)

        verify_calls = {"count": 0}

        def fail_verify_rebind(_root, _revision):
            verify_calls["count"] += 1
            if verify_calls["count"] == 1:
                return self.evaluator
            raise AUTHORITY.CurrentRouteArtifactAuthorityError(
                "injected verifier source drift after binding"
            )

        with mock.patch.object(
            AUTHORITY,
            "bind_artifact_evaluator_authority",
            side_effect=fail_verify_rebind,
        ):
            self.assertRejected(
                lambda: self.verify_artifacts(producer_verify_aggregate),
                "injected verifier source drift",
            )

    def test_fake_capture_model_judgment_verifier_oracle_and_score_fail(self) -> None:
        mutations = [
            (0, lambda value: value["payload"].__setitem__("boundNativeEvidenceItemCount", 1)),
            (1, lambda value: value["payload"].__setitem__("actorResult", {"fake": True})),
            (3, lambda value: value["payload"].__setitem__("modelExecutionReceipt", {"fake": True})),
            (3, lambda value: value["payload"].__setitem__("judgment", {"verdict": "PASS"})),
            (4, lambda value: value["payload"].__setitem__("unsupportedEvidenceClaimCount", 0)),
            (5, lambda value: value["payload"].__setitem__("hardGateStatus", "PASS")),
            (6, lambda value: value["payload"].__setitem__("score", 99)),
            (6, lambda value: value.__setitem__("officialCommercialUX", True)),
        ]
        for index, mutation in mutations:
            with self.subTest(index=index, mutation=repr(mutation)):
                chain_path, parent = self.create_parent_chain(
                    "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
                )
                aggregate_path, _ = self.create_artifacts(chain_path)
                self.rewrite_artifact_suffix(parent, index, mutation)
                self.assertRejected(lambda: self.verify_artifacts(aggregate_path))

    def test_parent_public_verifier_phase_and_malformed_short_paths_fail_typed(self) -> None:
        chain_path, parent = self.create_parent_chain(
            "TARGETED_CHECKPOINT", "A1_NORMAL_READY", ["EXPECTED"]
        )
        CHAIN.verify_chain_claim_against_reconstructed_authority(
            REPOSITORY_ROOT,
            chain_path,
        )
        aggregate_path, _ = self.create_artifacts(chain_path)
        with self.assertRaises(CHAIN.ChainAuthorityError):
            CHAIN.verify_chain_claim_against_reconstructed_authority(
                REPOSITORY_ROOT,
                chain_path,
            )
        self.verify_artifacts(aggregate_path)
        malformed = copy.deepcopy(parent)
        malformed["fixedFutureArtifactPaths"] = malformed[
            "fixedFutureArtifactPaths"
        ][:2]
        self.rewrite_chain_claim(chain_path, malformed)
        self.assertRejected(
            lambda: self.verify_artifacts(aggregate_path),
            "malformed parent",
        )

    def test_policy_producer_schema_projection_and_exact_public_api(self) -> None:
        policy, policy_bytes = AUTHORITY.load_artifact_policy()
        AUTHORITY.validate_artifact_policy(policy, policy_bytes)
        self.assertEqual(
            FIXED_CHAIN_SOURCE_COMMIT,
            policy["parentEvaluationChainAuthority"]["sourceCommit"],
        )
        self.assertEqual(11, self.evaluator["fileCount"])
        self.assertEqual(
            [path for path, _role in AUTHORITY.PRODUCER_PATH_ROLES],
            [row["path"] for row in self.evaluator["files"]],
        )
        self.assertEqual(
            AUTHORITY.EXPECTED_POLICY_RAW_SHA256,
            AUTHORITY.sha256_bytes(policy_bytes),
        )
        create_signature = inspect.signature(
            AUTHORITY.create_current_route_artifact_chain
        )
        verify_signature = inspect.signature(
            AUTHORITY.verify_current_route_artifact_chain_against_reconstructed_authority
        )
        self.assertEqual(
            ["repository_root", "chain_claim_path", "artifact_authority_revision"],
            list(create_signature.parameters),
        )
        self.assertEqual(
            inspect.Parameter.KEYWORD_ONLY,
            create_signature.parameters["artifact_authority_revision"].kind,
        )
        self.assertEqual(
            ["repository_root", "aggregate_path"],
            list(verify_signature.parameters),
        )


if __name__ == "__main__":
    unittest.main(verbosity=2)
