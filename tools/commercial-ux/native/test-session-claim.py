#!/usr/bin/env python3
"""Deterministic filesystem and exact-byte tests for evaluation session claims."""

from __future__ import annotations

import copy
import importlib.util
import json
import sys
import tempfile
from pathlib import Path
from types import ModuleType
from typing import Any


ROOT = Path(__file__).resolve().parent
SESSION_TOOL = ROOT / "claim-evaluation-session.py"
VALIDATOR = ROOT / "validate-contract.py"
AGGREGATE_TEST = ROOT.parent / "test-native-aggregate.py"
SHA = "sha256:" + "a" * 64


def load(name: str, path: Path) -> ModuleType:
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def write_json(path: Path, value: dict[str, Any]) -> bytes:
    data = json.dumps(
        value, ensure_ascii=False, sort_keys=True, indent=2
    ).encode("utf-8") + b"\n"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)
    return data


def make_claim(
    session: ModuleType,
    temporary_root: Path,
) -> tuple[Path, bytes, dict[str, Any], dict[str, Any], dict[str, Any]]:
    policy_bytes = (ROOT / "evaluation-session-policy.json").read_bytes()
    policy = json.loads(policy_bytes)
    receipt_sha = "sha256:" + "1" * 64
    receipt_root = (
        temporary_root
        / "gridworks-commercial-ux"
        / "evaluation-sessions"
        / receipt_sha.removeprefix("sha256:")
    )
    root = receipt_root / "initial"
    candidate = {
        "candidateId": "candidate-session-test",
        "candidateManifestSha256": "sha256:" + "2" * 64,
        "source": {"commit": "3" * 40},
        "authorityHashes": {"world": "sha256:" + "4" * 64},
        "execution": {"executionArtifactSha256": "sha256:" + "5" * 64},
    }
    fingerprint = session.playable_fingerprint(candidate)
    receipt = {
        "holdoutConsumptionReceiptSha256": receipt_sha,
        "candidatePlayableFingerprintSha256": fingerprint,
        "sourceCommit": candidate["source"]["commit"],
        "evaluationPhase": "OFFICIAL_HOLDOUT",
        "officialCommercialUX": True,
        "selectedRecipe": {
            "recipeId": "HOLDOUT-01",
            "selectedRecipeSha256": "sha256:" + "6" * 64,
        },
    }
    claim_path = receipt_root / "initial-claim.json"
    fixed_paths = {
        key: str(root / "artifacts" / filename)
        for key, filename in policy["fixedArtifactNames"].items()
    }
    claim = {
        "schemaVersion": "gridworks.commercial-ux.evaluation-session-claim.v1",
        "protocol": session.PROTOCOL,
        "evaluationSessionClaimSha256": SHA,
        "evaluationSessionClaimSchemaSha256": session.sha256_bytes(
            (ROOT / "evaluation-session-claim.schema.json").read_bytes()
        ),
        "evaluationSessionPolicySha256": session.sha256_bytes(policy_bytes),
        "sessionClaimToolSha256": session.sha256_bytes(SESSION_TOOL.read_bytes()),
        "policyId": policy["policyId"],
        "sessionId": session.session_id(
            receipt_sha, "INITIAL", None, session.sha256_bytes(policy_bytes)
        ),
        "sessionMode": "INITIAL",
        "evaluationPhase": "OFFICIAL_HOLDOUT",
        "officialCommercialUX": True,
        "candidateId": candidate["candidateId"],
        "candidateManifestSha256": candidate["candidateManifestSha256"],
        "candidateManifestRawSha256": "sha256:" + "7" * 64,
        "sourceCommit": candidate["source"]["commit"],
        "candidatePlayableFingerprintSha256": fingerprint,
        "executionArtifactSha256": candidate["execution"]["executionArtifactSha256"],
        "authorityHashesSha256": session.projection_sha256(
            candidate["authorityHashes"]
        ),
        "holdoutConsumptionReceiptSha256": receipt_sha,
        "holdoutConsumptionReceiptRawSha256": "sha256:" + "8" * 64,
        "selectedRecipeId": "HOLDOUT-01",
        "selectedRecipeSha256": receipt["selectedRecipe"]["selectedRecipeSha256"],
        "canonicalSessionRoot": str(root),
        "canonicalClaimPath": str(claim_path),
        "initialSession": None,
        "replacementClaimPath": str(
            receipt_root / "replacement-01-claim.json"
        ),
        "sessionLockPath": str(root / "session.lock"),
        "slots": session.build_slots(root, policy),
        "fixedArtifactPaths": fixed_paths,
        "atomicClaim": {
            "claimPolicy": "HOLDOUT_EXACT_BYTES_VALIDATED_THEN_CLAIM_O_EXCL_FSYNC_BEFORE_SESSION_ROOT_MKDIR_OR_ARTIFACTS",
            "holdoutValidatedBeforeClaim": True,
            "sessionRootAbsentBeforeClaim": True,
            "claimPrecedesSessionRootCreation": True,
            "claimPathAbsentBeforeOpen": True,
            "exclusiveCreateCompleted": True,
            "claimFsyncCompleted": True,
            "parentDirectoryFsyncCompleted": True,
        },
        "status": "CLAIMED_BEFORE_CAPTURE",
    }
    claim["evaluationSessionClaimSha256"] = session.self_hash(
        claim, "evaluationSessionClaimSha256"
    )
    claim_bytes = write_json(claim_path, claim)
    root.mkdir()
    return claim_path, claim_bytes, claim, candidate, receipt


def make_initial_finalization(
    session: ModuleType,
    fixtures: ModuleType,
    claim_path: Path,
    claim_bytes: bytes,
    claim: dict[str, Any],
    fixture_root: Path,
) -> tuple[Path, bytes, dict[str, Any], Path, bytes, dict[str, Any]]:
    fixture = fixtures.make_fixture(
        fixture_root,
        labeler=lambda judge, _kind, _artifact, _cell: (
            "STRONG" if judge < 2 else "BROKEN"
        ),
    )
    scorecard = fixtures.aggregate_fixture(fixture)
    assert scorecard["status"] == "RERUN_REQUIRED_PANEL_INSTABILITY"
    provenance = scorecard["provenance"]
    provenance.update({
        "candidateManifestSha256": claim["candidateManifestSha256"],
        "candidateManifestRawSha256": claim["candidateManifestRawSha256"],
        "sourceCommit": claim["sourceCommit"],
        "executionArtifactSha256": claim["executionArtifactSha256"],
        "holdoutConsumptionReceiptSha256": (
            claim["holdoutConsumptionReceiptSha256"]
        ),
        "holdoutConsumptionReceiptRawSha256": (
            claim["holdoutConsumptionReceiptRawSha256"]
        ),
        "evaluationSessionClaimSha256": claim[
            "evaluationSessionClaimSha256"
        ],
        "evaluationSessionClaimRawSha256": session.sha256_bytes(claim_bytes),
        "evaluationSessionPolicySha256": claim[
            "evaluationSessionPolicySha256"
        ],
        "evaluationSessionClaimToolSha256": claim["sessionClaimToolSha256"],
        "evaluationSessionId": claim["sessionId"],
        "evaluationSessionMode": "INITIAL",
        "evaluationAttemptAuditSha256": "sha256:" + "9" * 64,
        "evaluationSelectedAttemptsSha256": "sha256:" + "a" * 64,
    })
    scorecard_path = Path(claim["fixedArtifactPaths"]["scorecard"])
    scorecard_bytes = write_json(scorecard_path, scorecard)
    seal_path = Path(claim["fixedArtifactPaths"]["panelFinalizationSeal"])
    seal_bytes = fixtures.aggregator._panel_finalization_seal_bytes(
        seal_path,
        "INITIAL",
        scorecard["judgePanelSha256"],
        scorecard,
        scorecard_path,
        session.sha256_bytes(scorecard_bytes),
    )
    seal_path.parent.mkdir(parents=True, exist_ok=True)
    seal_path.write_bytes(seal_bytes)
    seal = json.loads(seal_bytes)
    return (
        scorecard_path,
        scorecard_bytes,
        scorecard,
        seal_path,
        seal_bytes,
        seal,
    )


def make_replacement_claim(
    session: ModuleType,
    initial_claim: dict[str, Any],
    reference: dict[str, Any],
) -> tuple[Path, bytes, dict[str, Any]]:
    policy_bytes = (ROOT / "evaluation-session-policy.json").read_bytes()
    policy = json.loads(policy_bytes)
    receipt_root = Path(initial_claim["canonicalClaimPath"]).parent
    replacement_root = receipt_root / "replacement-01"
    replacement_path = receipt_root / "replacement-01-claim.json"
    replacement = copy.deepcopy(initial_claim)
    replacement.update({
        "evaluationSessionClaimSha256": SHA,
        "sessionId": session.session_id(
            initial_claim["holdoutConsumptionReceiptSha256"],
            "REPLACEMENT",
            initial_claim["evaluationSessionClaimSha256"],
            initial_claim["evaluationSessionPolicySha256"],
        ),
        "sessionMode": "REPLACEMENT",
        "canonicalSessionRoot": str(replacement_root),
        "canonicalClaimPath": str(replacement_path),
        "initialSession": copy.deepcopy(reference),
        "replacementClaimPath": str(replacement_path),
        "sessionLockPath": str(replacement_root / "session.lock"),
        "slots": session.build_slots(replacement_root, policy),
        "fixedArtifactPaths": {
            key: str(replacement_root / "artifacts" / filename)
            for key, filename in policy["fixedArtifactNames"].items()
        },
    })
    replacement["evaluationSessionClaimSha256"] = session.self_hash(
        replacement,
        "evaluationSessionClaimSha256",
    )
    replacement_bytes = write_json(replacement_path, replacement)
    replacement_root.mkdir()
    return replacement_path, replacement_bytes, replacement


def attempt_envelope(
    claim: dict[str, Any],
    slot_id: str,
    attempt_ordinal: int,
) -> dict[str, Any]:
    slot = next(row for row in claim["slots"] if row["slotId"] == slot_id)
    attempt = slot["attempts"][attempt_ordinal - 1]
    root = Path(attempt["artifactRoot"])
    artifact_files = []
    for path in sorted(root.rglob("*")):
        if path.is_file():
            artifact_files.append((path.relative_to(root).as_posix(), path.read_bytes()))
    return {
        "startReceiptBytes": Path(attempt["startReceiptPath"]).read_bytes(),
        "terminalReceiptBytes": Path(attempt["terminalReceiptPath"]).read_bytes(),
        "outputBytes": Path(attempt["outputPath"]).read_bytes(),
        "artifactFiles": artifact_files,
        "startReceiptPathLabel": Path(attempt["startReceiptPath"]),
        "terminalReceiptPathLabel": Path(attempt["terminalReceiptPath"]),
        "outputPathLabel": Path(attempt["outputPath"]),
        "artifactRootPathLabel": root,
    }


def main() -> int:
    session = load("gridworks_session_claim_tool_test", SESSION_TOOL)
    validator = load("gridworks_session_claim_validator_test", VALIDATOR)
    fixtures = load("gridworks_session_claim_fixture_test", AGGREGATE_TEST)
    checks = 0

    with tempfile.TemporaryDirectory(prefix="gridworks-session-claim-") as temporary:
        temporary_root = Path(temporary).resolve()
        root_probe = temporary_root / "exclusive-root-probe" / "initial"
        session.exclusive_create_session_root(root_probe)
        try:
            session.exclusive_create_session_root(root_probe)
            raise AssertionError("preexisting session root unexpectedly accepted")
        except (OSError, session.SessionClaimError):
            pass
        checks += 1
        claim_path, claim_bytes, claim, candidate, receipt = make_claim(
            session, temporary_root
        )
        claim_schema = validator.read_json(ROOT / "evaluation-session-claim.schema.json")
        assert not validator.instance_errors(claim, claim_schema)
        semantic_errors: list[str] = []
        validator.validate_evaluation_session_claim_semantics(
            claim,
            claim_path,
            ROOT,
            candidate,
            receipt,
            semantic_errors,
            common_dir_override=temporary_root,
        )
        assert not semantic_errors, semantic_errors
        checks += 1

        (
            scorecard_path,
            scorecard_bytes,
            scorecard,
            seal_path,
            seal_bytes,
            _,
        ) = make_initial_finalization(
            session,
            fixtures,
            claim_path,
            claim_bytes,
            claim,
            temporary_root / "initial-finalization-fixture",
        )
        initial_reference = session.read_and_validate_initial_finalization(
            validator=validator,
            initial_claim=claim,
            initial_claim_path=claim_path,
            initial_claim_raw_bytes=claim_bytes,
            scorecard_path=scorecard_path,
            panel_finalization_seal_path=seal_path,
        )
        assert initial_reference["scorecardRawSha256"] == session.sha256_bytes(
            scorecard_bytes
        )
        assert initial_reference["panelFinalizationSealRawSha256"] == (
            session.sha256_bytes(seal_bytes)
        )
        assert initial_reference["replacementRequiredLanes"] == [
            "COLD-JOURNEY",
            "COVERAGE-JOURNEY",
        ]
        checks += 1

        replacement_path, replacement_bytes, replacement = make_replacement_claim(
            session,
            claim,
            initial_reference,
        )
        replacement_schema_errors = validator.instance_errors(
            replacement,
            claim_schema,
        )
        assert not replacement_schema_errors, replacement_schema_errors
        _, validated_replacement_path, validated_replacement_bytes, validated = (
            session.read_and_validate_claim(
                replacement_path,
                native=ROOT,
                common_dir_override=temporary_root,
            )
        )
        assert validated_replacement_path == replacement_path
        assert validated_replacement_bytes == replacement_bytes
        assert validated["initialSession"] == initial_reference
        checks += 1

        drifted = copy.deepcopy(replacement)
        drifted["initialSession"]["evaluationAttemptAuditSha256"] = (
            "sha256:" + "b" * 64
        )
        drifted["evaluationSessionClaimSha256"] = session.self_hash(
            drifted,
            "evaluationSessionClaimSha256",
        )
        write_json(replacement_path, drifted)
        try:
            session.read_and_validate_claim(
                replacement_path,
                native=ROOT,
                common_dir_override=temporary_root,
            )
            raise AssertionError("replacement attempt-audit drift unexpectedly accepted")
        except session.SessionClaimError:
            pass
        replacement_path.write_bytes(replacement_bytes)

        scorecard_path.write_bytes(
            json.dumps(scorecard, ensure_ascii=False, sort_keys=True).encode("utf-8")
        )
        try:
            session.read_and_validate_claim(
                replacement_path,
                native=ROOT,
                common_dir_override=temporary_root,
            )
            raise AssertionError("replacement scorecard raw-byte drift unexpectedly accepted")
        except session.SessionClaimError:
            pass
        scorecard_path.write_bytes(scorecard_bytes)

        seal_value = json.loads(seal_bytes)
        seal_path.write_bytes(
            json.dumps(seal_value, ensure_ascii=False, sort_keys=True).encode("utf-8")
        )
        try:
            session.read_and_validate_claim(
                replacement_path,
                native=ROOT,
                common_dir_override=temporary_root,
            )
            raise AssertionError("replacement seal raw-byte drift unexpectedly accepted")
        except session.SessionClaimError:
            pass
        seal_path.write_bytes(seal_bytes)

        wrong_lanes = copy.deepcopy(scorecard)
        wrong_lanes["replacementRequiredLanes"] = ["COLD-JOURNEY"]
        write_json(scorecard_path, wrong_lanes)
        try:
            session.read_and_validate_initial_finalization(
                validator=validator,
                initial_claim=claim,
                initial_claim_path=claim_path,
                initial_claim_raw_bytes=claim_bytes,
                scorecard_path=scorecard_path,
                panel_finalization_seal_path=seal_path,
            )
            raise AssertionError("scorecard required-lane drift unexpectedly accepted")
        except session.SessionClaimError:
            pass
        scorecard_path.write_bytes(scorecard_bytes)
        checks += 1

        final_symlink = temporary_root / "claim-link.json"
        final_symlink.symlink_to(claim_path)
        try:
            session.read_exact(final_symlink, "symlink claim")
            raise AssertionError("final symlink unexpectedly accepted")
        except session.SessionClaimError:
            pass
        ancestor_link = temporary_root / "linked-root"
        ancestor_link.symlink_to(claim_path.parent, target_is_directory=True)
        try:
            session.read_exact(ancestor_link / claim_path.name, "ancestor symlink claim")
            raise AssertionError("ancestor symlink unexpectedly accepted")
        except session.SessionClaimError:
            pass
        checks += 1

        _, first_start = session.reserve_attempt(
            native=ROOT,
            claim_path=claim_path,
            slot_id="SLOT-01",
            attempt_ordinal=1,
            common_dir_override=temporary_root,
        )
        first_attempt = claim["slots"][0]["attempts"][0]
        assert Path(first_attempt["outputPath"]).read_bytes() == b""
        assert first_start["status"] == "STARTED_BEFORE_PRODUCER"
        Path(first_attempt["outputPath"]).write_bytes(b"{malformed")
        _, first_terminal = session.finalize_attempt(
            native=ROOT,
            claim_path=claim_path,
            slot_id="SLOT-01",
            attempt_ordinal=1,
            common_dir_override=temporary_root,
        )
        assert first_terminal["outcome"] == "TRANSPORT_FAILURE"
        assert first_terminal["nextAttemptAllowed"] is True
        checks += 1

        session.reserve_attempt(
            native=ROOT,
            claim_path=claim_path,
            slot_id="SLOT-01",
            attempt_ordinal=2,
            common_dir_override=temporary_root,
        )
        with tempfile.TemporaryDirectory(prefix="gridworks-session-fixture-") as fixture_dir:
            fixture = fixtures.make_fixture(
                Path(fixture_dir).resolve(),
                labeler=fixtures.constant_label("BROKEN"),
            )
            valid_unfavorable = fixture["coldActorResponsePaths"][0].read_bytes()
            role_outputs = {
                "SLOT-02": fixture["coldActorResponsePaths"][1].read_bytes(),
                "SLOT-03": fixture["coldActorResponsePaths"][2].read_bytes(),
                "SLOT-04": fixture["coverageTracePath"].read_bytes(),
                "SLOT-05": fixture["judgmentPaths"][0].read_bytes(),
                "SLOT-06": fixture["judgmentPaths"][1].read_bytes(),
                "SLOT-07": fixture["judgmentPaths"][2].read_bytes(),
                "SLOT-08": fixture["verifierPath"].read_bytes(),
                "SLOT-09": fixture["ledgerPath"].read_bytes(),
            }
        second_attempt = claim["slots"][0]["attempts"][1]
        Path(second_attempt["outputPath"]).write_bytes(valid_unfavorable)
        artifact_path = Path(second_attempt["artifactRoot"]) / "frames" / "frame.bin"
        artifact_path.parent.mkdir(parents=True, exist_ok=True)
        artifact_path.write_bytes(b"sealed supporting evidence")
        _, second_terminal = session.finalize_attempt(
            native=ROOT,
            claim_path=claim_path,
            slot_id="SLOT-01",
            attempt_ordinal=2,
            common_dir_override=temporary_root,
        )
        assert second_terminal["outcome"] == "SUCCESS"
        assert second_terminal["nextAttemptAllowed"] is False
        try:
            session.reserve_attempt(
                native=ROOT,
                claim_path=claim_path,
                slot_id="SLOT-01",
                attempt_ordinal=3,
                common_dir_override=temporary_root,
            )
            raise AssertionError("retry after schema-valid unfavorable output accepted")
        except session.SessionClaimError:
            pass
        checks += 1

        chain = [
            attempt_envelope(claim, "SLOT-01", 1),
            attempt_envelope(claim, "SLOT-01", 2),
        ]
        chain_errors, selected = validator.validate_attempt_chain_bytes(
            ROOT,
            session_claim=claim,
            session_claim_raw_bytes=claim_bytes,
            attempts=chain,
            require_all_success_slots=False,
        )
        assert not chain_errors, chain_errors
        assert len(selected) == 1 and selected[0]["attemptOrdinal"] == 2

        lied = copy.deepcopy(second_terminal)
        lied.update({
            "outcome": "SCHEMA_FAILURE",
            "failureCode": "CALLER_DISLIKED_VALID_OUTPUT",
            "nextAttemptAllowed": True,
        })
        lied["evaluationAttemptTerminalSha256"] = validator.self_hash(
            lied, "evaluationAttemptTerminalSha256"
        )
        lied_chain = copy.deepcopy(chain)
        lied_chain[1]["terminalReceiptBytes"] = json.dumps(
            lied, ensure_ascii=False, sort_keys=True, indent=2
        ).encode("utf-8") + b"\n"
        lied_errors, _ = validator.validate_attempt_chain_bytes(
            ROOT,
            session_claim=claim,
            session_claim_raw_bytes=claim_bytes,
            attempts=lied_chain,
            require_all_success_slots=False,
        )
        assert any("caller-controlled outcome" in error for error in lied_errors)

        changed_artifacts = copy.deepcopy(chain)
        changed_artifacts[1]["artifactFiles"].append(("frames/late.bin", b"late"))
        artifact_errors, _ = validator.validate_attempt_chain_bytes(
            ROOT,
            session_claim=claim,
            session_claim_raw_bytes=claim_bytes,
            attempts=changed_artifacts,
            require_all_success_slots=False,
        )
        assert any("artifact content-root binding mismatch" in error for error in artifact_errors)
        checks += 1

        prepop_root = temporary_root / "prepopulation-case"
        prepop_claim_path, _, prepop_claim, _, _ = make_claim(
            session, prepop_root
        )
        preexisting = prepop_claim["slots"][1]["attempts"][0]
        Path(preexisting["artifactRoot"]).mkdir(parents=True, exist_ok=True)
        Path(preexisting["outputPath"]).write_bytes(b"staged before reservation")
        try:
            session.reserve_attempt(
                native=ROOT,
                claim_path=prepop_claim_path,
                slot_id="SLOT-02",
                attempt_ordinal=1,
                common_dir_override=prepop_root,
            )
            raise AssertionError("preexisting output unexpectedly accepted")
        except session.SessionClaimError:
            pass
        assert not Path(preexisting["terminalReceiptPath"]).exists()
        checks += 1

        interrupted_root = temporary_root / "interrupted-reservation-case"
        interrupted_claim_path, _, interrupted_claim, _, _ = make_claim(
            session, interrupted_root
        )
        original_write = session.exclusive_write
        injected = {"raised": False}

        def fail_once(path: Path, content: bytes) -> None:
            if path.name == "start-receipt.json" and not injected["raised"]:
                injected["raised"] = True
                raise OSError("injected start receipt failure")
            original_write(path, content)

        session.exclusive_write = fail_once
        try:
            session.reserve_attempt(
                native=ROOT,
                claim_path=interrupted_claim_path,
                slot_id="SLOT-03",
                attempt_ordinal=1,
                common_dir_override=interrupted_root,
            )
            raise AssertionError("injected reservation failure unexpectedly passed")
        except (OSError, session.SessionClaimError):
            pass
        finally:
            session.exclusive_write = original_write
        try:
            session.reserve_attempt(
                native=ROOT,
                claim_path=interrupted_claim_path,
                slot_id="SLOT-03",
                attempt_ordinal=1,
                common_dir_override=interrupted_root,
            )
            raise AssertionError("partial reservation recovery unexpectedly returned success")
        except session.SessionClaimError:
            pass
        recovered = interrupted_claim["slots"][2]["attempts"][0]
        assert Path(recovered["outputPath"]).is_file()
        assert not Path(recovered["terminalReceiptPath"]).exists()
        checks += 1

        terminal_fault_root = temporary_root / "interrupted-terminal-case"
        terminal_fault_claim_path, _, terminal_fault_claim, _, _ = make_claim(
            session,
            terminal_fault_root,
        )
        session.reserve_attempt(
            native=ROOT,
            claim_path=terminal_fault_claim_path,
            slot_id="SLOT-04",
            attempt_ordinal=1,
            common_dir_override=terminal_fault_root,
        )
        terminal_fault_attempt = terminal_fault_claim["slots"][3]["attempts"][0]
        Path(terminal_fault_attempt["outputPath"]).write_bytes(
            role_outputs["SLOT-04"]
        )
        original_finalize = session.finalize_reserved_file

        def fail_terminal_write(
            descriptor: int,
            path: Path,
            content: bytes,
        ) -> None:
            session.os.close(descriptor)
            del path, content
            raise OSError("injected terminal write failure")

        session.finalize_reserved_file = fail_terminal_write
        try:
            session.finalize_attempt(
                native=ROOT,
                claim_path=terminal_fault_claim_path,
                slot_id="SLOT-04",
                attempt_ordinal=1,
                common_dir_override=terminal_fault_root,
            )
            raise AssertionError("injected terminal write failure unexpectedly passed")
        except OSError:
            pass
        finally:
            session.finalize_reserved_file = original_finalize
        terminal_tombstone = Path(
            terminal_fault_attempt["terminalReceiptPath"]
        )
        assert terminal_tombstone.is_file()
        assert terminal_tombstone.read_bytes() == b""
        Path(terminal_fault_attempt["outputPath"]).write_bytes(b"{malformed")
        try:
            session.finalize_attempt(
                native=ROOT,
                claim_path=terminal_fault_claim_path,
                slot_id="SLOT-04",
                attempt_ordinal=1,
                common_dir_override=terminal_fault_root,
            )
            raise AssertionError("terminal tombstone was unexpectedly reclassified")
        except session.SessionClaimError:
            pass
        try:
            session.reserve_attempt(
                native=ROOT,
                claim_path=terminal_fault_claim_path,
                slot_id="SLOT-04",
                attempt_ordinal=2,
                common_dir_override=terminal_fault_root,
            )
            raise AssertionError("terminal tombstone unexpectedly authorized retry")
        except session.SessionClaimError:
            pass
        checks += 1

        unreadable_root = temporary_root / "input-unreadable-case"
        unreadable_claim_path, _, unreadable_claim, _, _ = make_claim(
            session,
            unreadable_root,
        )
        session.reserve_attempt(
            native=ROOT,
            claim_path=unreadable_claim_path,
            slot_id="SLOT-05",
            attempt_ordinal=1,
            common_dir_override=unreadable_root,
        )
        _, unreadable_terminal = session.finalize_attempt(
            native=ROOT,
            claim_path=unreadable_claim_path,
            slot_id="SLOT-05",
            attempt_ordinal=1,
            common_dir_override=unreadable_root,
        )
        assert unreadable_terminal["outcome"] == "INPUT_UNREADABLE"
        assert unreadable_terminal["nextAttemptAllowed"] is False
        try:
            session.reserve_attempt(
                native=ROOT,
                claim_path=unreadable_claim_path,
                slot_id="SLOT-05",
                attempt_ordinal=2,
                common_dir_override=unreadable_root,
            )
            raise AssertionError("INPUT_UNREADABLE unexpectedly authorized retry")
        except session.SessionClaimError:
            pass
        assert unreadable_claim["slots"][4]["attempts"][0][
            "terminalReceiptPath"
        ] == unreadable_terminal["canonicalTerminalReceiptPath"]
        checks += 1

        artifact_probe = temporary_root / "two-pass-artifact-probe"
        artifact_probe.mkdir()
        original_entries = session._artifact_entries
        artifact_pass = {"count": 0}

        def divergent_artifact_entries(root: Path) -> list[dict[str, Any]]:
            artifact_pass["count"] += 1
            entries = original_entries(root)
            if artifact_pass["count"] == 2:
                return [
                    {
                        "locator": "late.bin",
                        "rawSha256": session.sha256_bytes(b"late"),
                        "byteLength": 4,
                    }
                ]
            return entries

        session._artifact_entries = divergent_artifact_entries
        try:
            session.artifact_manifest(artifact_probe)
            raise AssertionError("two-pass artifact mutation unexpectedly accepted")
        except session.SessionClaimError:
            pass
        finally:
            session._artifact_entries = original_entries
        checks += 1

        for slot_index in range(2, 10):
            slot_id = f"SLOT-{slot_index:02d}"
            attempt_ordinal = 1
            session.reserve_attempt(
                native=ROOT,
                claim_path=claim_path,
                slot_id=slot_id,
                attempt_ordinal=attempt_ordinal,
                common_dir_override=temporary_root,
            )
            attempt = claim["slots"][slot_index - 1]["attempts"][
                attempt_ordinal - 1
            ]
            Path(attempt["outputPath"]).write_bytes(role_outputs[slot_id])
            _, terminal = session.finalize_attempt(
                native=ROOT,
                claim_path=claim_path,
                slot_id=slot_id,
                attempt_ordinal=attempt_ordinal,
                common_dir_override=temporary_root,
            )
            assert terminal["outcome"] == "SUCCESS", (slot_id, terminal)

        complete_chain: list[dict[str, Any]] = []
        for slot in claim["slots"]:
            for attempt in slot["attempts"]:
                if Path(attempt["terminalReceiptPath"]).is_file():
                    complete_chain.append(
                        attempt_envelope(
                            claim, slot["slotId"], attempt["attemptOrdinal"]
                        )
                    )
        complete_errors, complete_selected = validator.validate_attempt_chain_bytes(
            ROOT,
            session_claim=claim,
            session_claim_raw_bytes=claim_bytes,
            attempts=complete_chain,
            require_all_success_slots=True,
        )
        assert not complete_errors, complete_errors
        assert len(complete_selected) == 9
        checks += 1

        blockers = validator.score_bearing_contract_readiness_errors(
            ROOT, evaluation_session_claim=claim
        )
        assert any("tool policy still blocks" in error for error in blockers)
        assert not any("gold-state" in error for error in blockers)
        checks += 1

    print(f"PASS evaluation session claim checks: {checks}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
