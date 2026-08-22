#!/usr/bin/env python3
"""Mutation tests for validate-gold-state.py."""

from __future__ import annotations

import copy
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

    def test_honest_pre_execution_contract_passes_but_reports_blocked(self) -> None:
        result = self.invoke(MANIFEST)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("episodes=12 checkpoints=49", result.stdout)
        self.assertIn("scoreBearing=BLOCKED", result.stdout)

    def test_score_ready_mode_fails_closed(self) -> None:
        result = self.invoke(MANIFEST, "--require-score-ready")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("requires --candidate-manifest", result.stderr)

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


if __name__ == "__main__":
    unittest.main(verbosity=2)
