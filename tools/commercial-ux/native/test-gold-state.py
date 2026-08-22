#!/usr/bin/env python3
"""Mutation tests for validate-gold-state.py."""

from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[3]
NATIVE = ROOT / "tools/commercial-ux/native"
VALIDATOR = NATIVE / "validate-gold-state.py"
MANIFEST = NATIVE / "gold-state-manifest.json"
BINDING_SCHEMA = NATIVE / "gold-binding-manifest.schema.json"
HOLDOUT_QUEUE = NATIVE / "holdout-recipes.json"


def sha(digit: str) -> str:
    return "sha256:" + digit * 64


def canonical_sha256(value: Any) -> str:
    payload = json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return "sha256:" + hashlib.sha256(payload).hexdigest()


class GoldStateContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.base: dict[str, Any] = json.loads(MANIFEST.read_text(encoding="utf-8"))

    def invoke(self, manifest: Path, *extra: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                sys.executable,
                str(VALIDATOR),
                "--root",
                str(ROOT),
                "--manifest",
                str(manifest),
                *extra,
            ],
            cwd=ROOT,
            check=False,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=180,
        )

    def write_manifest(self, directory: Path, value: Any) -> Path:
        path = directory / "gold-state-manifest.json"
        path.write_text(
            json.dumps(value, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        return path

    def seal_binding(self, value: dict[str, Any]) -> None:
        value["goldBindingManifestSha256"] = None
        value["goldBindingManifestSha256"] = canonical_sha256(value)

    def binding_fixture(self) -> dict[str, Any]:
        queue = json.loads(HOLDOUT_QUEUE.read_text(encoding="utf-8"))
        selected = queue["formative"]
        pre_journal = sha("1")
        pre_snapshot = sha("2")
        editable_journal = sha("3")
        editable_snapshot = sha("4")

        def bound_pair(status: str) -> tuple[dict[str, Any], dict[str, Any]]:
            if status == "NOT_APPLICABLE":
                return (
                    {"status": "NOT_APPLICABLE", "sha256": None, "commandCount": None, "locator": None, "byteLength": None},
                    {"status": "NOT_APPLICABLE", "sha256": None, "locator": None, "byteLength": None},
                )
            return (
                {"status": "BOUND_NATIVE_REPLAY", "sha256": sha("7"), "commandCount": 3, "locator": "pending", "byteLength": 1},
                {"status": "BOUND_NATIVE_REPLAY", "sha256": sha("8"), "locator": "pending", "byteLength": 1},
            )

        prefixes: list[dict[str, Any]] = []
        for template in self.base["prefixes"]:
            journal, snapshot = bound_pair(template["journalBinding"]["status"])
            row = {
                "prefixId": template["prefixId"],
                "journalBinding": journal,
                "snapshotBinding": snapshot,
            }
            if template["prefixId"] == "PREFIX-NORTH-BANK-MID-DRAFT":
                row["journalBinding"] = {
                    "status": "BOUND_NATIVE_REPLAY",
                    "sha256": pre_journal,
                    "commandCount": 10,
                    "locator": "pending",
                    "byteLength": 1,
                }
                row["snapshotBinding"] = {
                    "status": "BOUND_NATIVE_REPLAY",
                    "sha256": pre_snapshot,
                    "locator": "pending",
                    "byteLength": 1,
                }
            prefixes.append(row)

        promise_order = selected["promiseBranchOrder"]

        def branch_id(checkpoint_id: str) -> str:
            if "keep-result" in checkpoint_id:
                return "KEEP"
            if "defer-result" in checkpoint_id:
                return "DEFER"
            if checkpoint_id == "north-bank-first-result":
                return promise_order[0].upper()
            if checkpoint_id in {
                "emergency-use", "protective-shutdown", "planned-source-outage",
                "finale-heat", "finale-storm", "finale-result-to-epilogue",
            }:
                return "KEEP"
            return "SHARED"

        checkpoints: list[dict[str, Any]] = []
        for episode in self.base["episodes"]:
            for template in episode["checkpointBindings"]:
                checkpoint_id = template["checkpointId"]
                journal, snapshot = bound_pair(template["journalBinding"]["status"])
                row = {
                    "episodeId": episode["id"],
                    "checkpointId": checkpoint_id,
                    "checkpointBranchId": branch_id(checkpoint_id),
                    "journalBinding": journal,
                    "snapshotBinding": snapshot,
                }
                if episode["id"] == "E09-MID-RESUME" and checkpoint_id in {
                    "mid-save-before-exit", "resume-orientation",
                }:
                    row["journalBinding"] = {
                        "status": "BOUND_NATIVE_REPLAY",
                        "sha256": pre_journal,
                        "commandCount": 10,
                        "locator": "pending",
                        "byteLength": 1,
                    }
                    row["snapshotBinding"] = {
                        "status": "BOUND_NATIVE_REPLAY",
                        "sha256": pre_snapshot,
                        "locator": "pending",
                        "byteLength": 1,
                    }
                if episode["id"] == "E09-MID-RESUME" and checkpoint_id == "resumed-editable-draft":
                    row["journalBinding"] = {
                        "status": "BOUND_NATIVE_REPLAY",
                        "sha256": editable_journal,
                        "commandCount": 12,
                        "locator": "pending",
                        "byteLength": 1,
                    }
                    row["snapshotBinding"] = {
                        "status": "BOUND_NATIVE_REPLAY",
                        "sha256": editable_snapshot,
                        "locator": "pending",
                        "byteLength": 1,
                    }
                checkpoints.append(row)

        binding_index = 0
        for row in [*prefixes, *checkpoints]:
            for field, directory in (
                ("journalBinding", "journals"),
                ("snapshotBinding", "snapshots"),
            ):
                component = row[field]
                if component["status"] == "BOUND_NATIVE_REPLAY":
                    binding_index += 1
                    component["locator"] = f"{directory}/binding-{binding_index:03d}.bin"

        binding = {
            "schemaVersion": "gridworks.commercial-ux.gold-binding-manifest.v1",
            "protocol": "GRIDWORKS-COMMERCIAL-UX-v1.1",
            "goldBindingManifestSha256": None,
            "goldBindingSchemaSha256": "sha256:" + hashlib.sha256(BINDING_SCHEMA.read_bytes()).hexdigest(),
            "goldStateContractSha256": "sha256:" + hashlib.sha256(MANIFEST.read_bytes()).hexdigest(),
            "coverageRecipeSha256": self.base["authorities"]["coverageRecipe"]["sha256"],
            "candidateManifestSha256": sha("a"),
            "holdoutConsumptionReceiptSha256": sha("b"),
            "selectedRecipeId": selected["id"],
            "selectedRecipeSha256": canonical_sha256(selected),
            "executionArtifactSha256": sha("c"),
            "holdoutRealization": {
                "missionPrototypeBits": selected["missionPrototypeBits"],
                "promiseBranchOrder": selected["promiseBranchOrder"],
                "actorArtifactPermutation": selected["actorArtifactPermutation"],
                "coverageArtifactOrder": selected["coverageArtifactOrder"],
                "coveragePresentationEpisodeIds": [
                    episode["id"] for episode in self.base["episodes"]
                ],
            },
            "generatorToolSha256": sha("d"),
            "canonicalGoldBundleRoot": "/tmp/gridworks-gold-fixture",
            "goldBundleRootSha256": sha("e"),
            "goldBundleRootHashRule": "SHA256_OF_UTF8_RFC8785_CANONICAL_LOCATOR_SORTED_ROWS_LOCATOR_RAW_SHA256_BYTE_LENGTH",
            "goldBundleLocatorPolicy": "CANONICAL_RELATIVE_NO_DOTDOT_REJECT_ALL_SYMLINKS_COMPLETE_RECURSIVE_FILE_SET",
            "goldBundleEntryCount": 112,
            "goldBundleExtraFileCount": 0,
            "goldBundleSymlinkCount": 0,
            "prefixBindings": prefixes,
            "checkpointBindings": checkpoints,
            "bindingSummary": {
                "prefixCount": 12,
                "checkpointCount": 49,
                "applicableBindingCount": 56,
                "boundBindingCount": 56,
                "notApplicableBindingCount": 5,
                "allApplicableBindingsExact": True,
            },
            "e09NorthBankTwoProcessWitness": {
                "status": "EXACT_TWO_PROCESS_REPLAY",
                "chapterId": "NORTH_BANK_PROMISE",
                "decisionWindowId": "NORTH_BANK_BUILD",
                "constructionPhase": "LineDrafting",
                "preExitProcessTreeId": "process-tree-a",
                "postResumeProcessTreeId": "process-tree-b",
                "preExitCommandCount": 10,
                "postResumeCommandCount": 10,
                "preExitJournalSha256": pre_journal,
                "postResumeJournalSha256": pre_journal,
                "preExitSnapshotSha256": pre_snapshot,
                "postResumeSnapshotSha256": pre_snapshot,
                "journalExactEquality": True,
                "snapshotExactEquality": True,
                "resumedEditableDraftJournalSha256": editable_journal,
                "resumedEditableDraftSnapshotSha256": editable_snapshot,
                "resumedEditableDraftCommandCount": 12,
                "geometryRestoredAfterAddUndo": True,
                "projectionRestoredAfterAddUndo": True,
                "draftGeometryHashRule": "SHA256_OF_RFC8785_CANONICAL_DRAFT_GEOMETRY_COMPONENT_EXTRACTED_FROM_SNAPSHOT",
                "draftProjectionHashRule": "SHA256_OF_RFC8785_CANONICAL_DRAFT_PROJECTION_COMPONENT_EXTRACTED_FROM_SNAPSHOT",
                "preExitDraftGeometrySha256": sha("5"),
                "postResumeDraftGeometrySha256": sha("5"),
                "resumedEditableDraftGeometrySha256": sha("5"),
                "preExitDraftProjectionSha256": sha("6"),
                "postResumeDraftProjectionSha256": sha("6"),
                "resumedEditableDraftProjectionSha256": sha("6"),
            },
            "scoreBearingReady": True,
        }
        self.seal_binding(binding)
        return binding

    def write_binding(self, directory: Path, value: dict[str, Any]) -> Path:
        self.seal_binding(value)
        path = directory / "gold-binding-manifest.json"
        path.write_text(
            json.dumps(value, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        return path

    def test_honest_pre_execution_contract_passes_but_reports_blocked(self) -> None:
        result = self.invoke(MANIFEST)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("episodes=12 checkpoints=49", result.stdout)
        self.assertIn("scoreBearing=BLOCKED", result.stdout)

    def test_score_ready_mode_fails_closed(self) -> None:
        result = self.invoke(MANIFEST, "--require-score-ready")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("requires --candidate-manifest", result.stderr)
        self.assertIn("requires --binding-manifest", result.stderr)

    def test_overlay_prefix_order_mutation_is_rejected(self) -> None:
        value = self.binding_fixture()
        value["prefixBindings"][1], value["prefixBindings"][2] = (
            value["prefixBindings"][2], value["prefixBindings"][1]
        )
        with tempfile.TemporaryDirectory() as temporary:
            path = self.write_binding(Path(temporary), value)
            result = self.invoke(MANIFEST, "--binding-manifest", str(path))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("prefix ids/order must exactly match", result.stderr)

    def test_overlay_checkpoint_branch_mapping_is_recipe_derived(self) -> None:
        value = self.binding_fixture()
        north_bank = next(
            row for row in value["checkpointBindings"]
            if row["checkpointId"] == "north-bank-first-result"
        )
        emergency = next(
            row for row in value["checkpointBindings"]
            if row["checkpointId"] == "emergency-use"
        )
        north_bank["checkpointBranchId"] = "DEFER"
        emergency["checkpointBranchId"] = "SHARED"
        with tempfile.TemporaryDirectory() as temporary:
            path = self.write_binding(Path(temporary), value)
            result = self.invoke(MANIFEST, "--binding-manifest", str(path))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("north-bank-first-result checkpointBranchId mismatch", result.stderr)
        self.assertIn("emergency-use checkpointBranchId mismatch", result.stderr)

    def test_overlay_e09_add_undo_journal_mutation_is_rejected(self) -> None:
        value = self.binding_fixture()
        witness = value["e09NorthBankTwoProcessWitness"]
        witness["resumedEditableDraftJournalSha256"] = witness["postResumeJournalSha256"]
        editable = next(
            row for row in value["checkpointBindings"]
            if row["checkpointId"] == "resumed-editable-draft"
        )
        editable["journalBinding"]["sha256"] = witness["postResumeJournalSha256"]
        with tempfile.TemporaryDirectory() as temporary:
            path = self.write_binding(Path(temporary), value)
            result = self.invoke(MANIFEST, "--binding-manifest", str(path))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("add+undo journal must differ", result.stderr)

    def test_overlay_e09_mid_resume_exact_equality_is_enforced(self) -> None:
        value = self.binding_fixture()
        witness = value["e09NorthBankTwoProcessWitness"]
        witness["postResumeJournalSha256"] = sha("9")
        resume = next(
            row for row in value["checkpointBindings"]
            if row["checkpointId"] == "resume-orientation"
        )
        resume["journalBinding"]["sha256"] = sha("9")
        with tempfile.TemporaryDirectory() as temporary:
            path = self.write_binding(Path(temporary), value)
            result = self.invoke(MANIFEST, "--binding-manifest", str(path))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("mid-save and resume-orientation bytes/count must be exact", result.stderr)

    def test_overlay_e09_component_hash_mutation_is_rejected(self) -> None:
        value = self.binding_fixture()
        value["e09NorthBankTwoProcessWitness"]["resumedEditableDraftGeometrySha256"] = sha("9")
        with tempfile.TemporaryDirectory() as temporary:
            path = self.write_binding(Path(temporary), value)
            result = self.invoke(MANIFEST, "--binding-manifest", str(path))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("content-derived draft geometry/projection hashes", result.stderr)

    def test_overlay_e09_prefix_must_bind_pre_exit_bytes(self) -> None:
        value = self.binding_fixture()
        prefix = next(
            row for row in value["prefixBindings"]
            if row["prefixId"] == "PREFIX-NORTH-BANK-MID-DRAFT"
        )
        prefix["snapshotBinding"]["sha256"] = sha("9")
        with tempfile.TemporaryDirectory() as temporary:
            path = self.write_binding(Path(temporary), value)
            result = self.invoke(MANIFEST, "--binding-manifest", str(path))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("mid-draft prefix does not match exact pre-exit bytes", result.stderr)

    def test_overlay_missing_raw_replay_bundle_is_rejected(self) -> None:
        value = self.binding_fixture()
        with tempfile.TemporaryDirectory() as temporary:
            path = self.write_binding(Path(temporary), value)
            result = self.invoke(MANIFEST, "--binding-manifest", str(path))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("gold binding bundle root cannot be opened", result.stderr)

    def test_score_ready_rejects_unbound_candidate_manifest(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            candidate = Path(temporary) / "candidate-manifest.json"
            candidate.write_text("{}\n", encoding="utf-8")
            result = self.invoke(
                MANIFEST,
                "--run-story-manifest",
                "--candidate-manifest",
                str(candidate),
                "--require-score-ready",
            )
        self.assertNotEqual(0, result.returncode)
        self.assertIn("candidate manifest schemaVersion mismatch", result.stderr)
        self.assertIn("valid candidate manifest", result.stderr)

    def test_missing_required_checkpoint_is_rejected(self) -> None:
        value = copy.deepcopy(self.base)
        value["episodes"][4]["checkpointBindings"].pop()
        with tempfile.TemporaryDirectory() as temporary:
            result = self.invoke(self.write_manifest(Path(temporary), value))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("checkpoint ids/order must exactly match", result.stderr)

    def test_pending_binding_cannot_claim_a_hash(self) -> None:
        value = copy.deepcopy(self.base)
        checkpoint = value["episodes"][1]["checkpointBindings"][0]
        checkpoint["journalBinding"]["sha256"] = "sha256:" + "0" * 64
        with tempfile.TemporaryDirectory() as temporary:
            result = self.invoke(self.write_manifest(Path(temporary), value))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("must keep hash/count null", result.stderr)

    def test_pending_bindings_cannot_claim_readiness(self) -> None:
        value = copy.deepcopy(self.base)
        value["bindingComplete"] = True
        value["scoreBearingCaptureAllowed"] = True
        with tempfile.TemporaryDirectory() as temporary:
            result = self.invoke(self.write_manifest(Path(temporary), value))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("bindingComplete cannot be true", result.stderr)
        self.assertIn("scoreBearingCaptureAllowed cannot be true", result.stderr)

    def test_e09_cannot_relabel_nonmatching_smoke_as_gold(self) -> None:
        value = copy.deepcopy(self.base)
        prefix = next(
            item
            for item in value["prefixes"]
            if item["prefixId"] == "PREFIX-NORTH-BANK-MID-DRAFT"
        )
        prefix["journalBinding"]["status"] = "PENDING_NATIVE_REPLAY"
        prefix["snapshotBinding"]["status"] = "PENDING_NATIVE_REPLAY"
        with tempfile.TemporaryDirectory() as temporary:
            result = self.invoke(self.write_manifest(Path(temporary), value))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("E09 prefix must remain UNBOUND_REQUIRED_WITNESS", result.stderr)

    def test_authority_byte_drift_is_rejected(self) -> None:
        value = copy.deepcopy(self.base)
        value["authorities"]["coverageRecipe"]["sha256"] = "sha256:" + "0" * 64
        with tempfile.TemporaryDirectory() as temporary:
            result = self.invoke(self.write_manifest(Path(temporary), value))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("authorities.coverageRecipe raw SHA-256 mismatch", result.stderr)

    def test_candidate_dependent_source_hash_cannot_be_frozen_in_gold(self) -> None:
        value = copy.deepcopy(self.base)
        value["authorities"]["world"]["sha256"] = "sha256:" + "0" * 64
        with tempfile.TemporaryDirectory() as temporary:
            result = self.invoke(self.write_manifest(Path(temporary), value))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("must not freeze candidate-dependent source bytes", result.stderr)

    def test_duplicate_json_key_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "gold-state-manifest.json"
            path.write_text(
                '{"schemaVersion":"one","schemaVersion":"two"}\n',
                encoding="utf-8",
            )
            result = self.invoke(path)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("duplicate JSON key", result.stderr)

    def test_authoritative_story_generator_matches_frozen_shape(self) -> None:
        result = self.invoke(MANIFEST, "--run-story-manifest")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("scoreBearing=BLOCKED", result.stdout)
        self.assertIn("OBSERVED candidate story output sha256:", result.stdout)

    def test_exact_byte_api_reports_the_bytes_not_later_path_content(self) -> None:
        spec = importlib.util.spec_from_file_location(
            "gridworks_gold_exact_byte_test",
            VALIDATOR,
        )
        self.assertIsNotNone(spec)
        self.assertIsNotNone(spec.loader if spec is not None else None)
        assert spec is not None and spec.loader is not None
        validator = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(validator)
        candidate_a = b'{"runtime":"candidate-a"}\n'
        binding_a = b'{"runtime":"binding-a"}\n'
        with tempfile.TemporaryDirectory() as temporary:
            candidate_path = Path(temporary) / "candidate.json"
            binding_path = Path(temporary) / "binding.json"
            candidate_path.write_bytes(b'{"runtime":"candidate-b"}\n')
            binding_path.write_bytes(b'{"runtime":"binding-b"}\n')
            _, summary = validator.validate_exact_inputs(
                ROOT,
                MANIFEST,
                None,
                False,
                candidate_a,
                binding_a,
                False,
                candidate_manifest_path_label=candidate_path,
                binding_manifest_path_label=binding_path,
            )
        self.assertEqual(
            summary["observedRawSha256"]["candidateManifestRawSha256"],
            validator.sha256_bytes(candidate_a),
        )
        self.assertEqual(
            summary["observedRawSha256"]["goldBindingManifestRawSha256"],
            validator.sha256_bytes(binding_a),
        )

        class ExtraObservedContractValidator:
            @staticmethod
            def validate_runtime_contract_bytes(*_args, **_kwargs):
                return [], {
                    "observedRawSha256": {
                        "candidateManifestRawSha256": validator.sha256_bytes(
                            candidate_a
                        ),
                        "goldBindingManifestRawSha256": validator.sha256_bytes(
                            binding_a
                        ),
                        "unexpectedRawSha256": sha("f"),
                    }
                }

        validator._load_contract_validator = (
            lambda _path: ExtraObservedContractValidator()
        )
        failures, _ = validator.validate_exact_inputs(
            ROOT,
            MANIFEST,
            None,
            False,
            candidate_a,
            binding_a,
            False,
        )
        self.assertTrue(
            any(
                "native contract observed raw SHA projection mismatch" in failure
                for failure in failures
            )
        )


if __name__ == "__main__":
    unittest.main(verbosity=2)
