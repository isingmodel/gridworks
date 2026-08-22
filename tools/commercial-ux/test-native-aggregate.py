#!/usr/bin/env python3
"""Self-tests for the deterministic Gridworks native UX aggregator."""

from __future__ import annotations

import copy
import contextlib
import io
import importlib.util
import inspect
import json
import os
import struct
import subprocess
import sys
import tempfile
import unittest
import wave
import zlib
from pathlib import Path
from typing import Any, Callable
from unittest import mock


TOOL_DIRECTORY = Path(__file__).resolve().parent
AGGREGATOR_PATH = TOOL_DIRECTORY / "aggregate-native.py"


def load_aggregator():
    spec = importlib.util.spec_from_file_location(
        "gridworks_commercial_ux_native_aggregator_tests",
        AGGREGATOR_PATH,
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"could not load {AGGREGATOR_PATH}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


aggregator = load_aggregator()


def validate_synthetic_actor_observation_authorities(*args: Any, **kwargs: Any):
    """Exercise legacy actor fixtures while isolating the new E2E-sequence rule."""

    with mock.patch.object(
        aggregator,
        "_validate_cold_terminal_checkpoint_sequence",
    ):
        return aggregator.validate_actor_observation_authorities(*args, **kwargs)


def identity_sha(name: str) -> str:
    return aggregator.canonical_sha256({"fixture": name})


def write_json(path: Path, value: Any) -> str:
    path.parent.mkdir(parents=True, exist_ok=True)
    content = json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True).encode("utf-8") + b"\n"
    path.write_bytes(content)
    return aggregator.bytes_sha256(content)


def base_difference_report() -> dict[str, Any]:
    return {"items": [], "openP0": 0, "openP1": 0, "openP2": 0}


def one_reference(name: str) -> dict[str, str]:
    return {
        "artifactId": name,
        "locator": f"fixture/{name}",
        "sha256": identity_sha(f"reference-{name}"),
    }


def png_chunk(kind: bytes, payload: bytes) -> bytes:
    checksum = zlib.crc32(payload, zlib.crc32(kind)) & 0xFFFFFFFF
    return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", checksum)


def restricted_png(scanline: bytes, *, width: int = 1, height: int = 1) -> bytes:
    return b"".join((
        b"\x89PNG\r\n\x1a\n",
        png_chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)),
        png_chunk(b"IDAT", zlib.compress(scanline)),
        png_chunk(b"IEND", b""),
    ))


def wav_48000(frame_count: int = 9601) -> bytes:
    output = io.BytesIO()
    with wave.open(output, "wb") as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(48_000)
        stream.writeframes(b"\x00\x00" * frame_count)
    return output.getvalue()


def audio_sync_authorities(
    *,
    latency_nanoseconds: int = 40_000_000,
    first_ledger_delivery_delta: int = 0,
    sync_clock_domain: str = "CLOCK-MONOTONIC-1",
    ledger_clock_domain: str = "CLOCK-MONOTONIC-1",
) -> tuple[dict[str, Any], dict[str, Any]]:
    episodes = (
        ("V1", "E01-FIRST-LIGHT"),
        ("V2", "E05-WHOSE-MARGIN"),
        ("V3", "E06-FLOOD"),
        ("V4", "E08-FINALE"),
    )
    capture_started = 2_000_000_000
    cue_sample = 4_800
    cue_onset = capture_started + cue_sample * 1_000_000_000 // 48_000
    action_delivered = cue_onset - latency_nanoseconds
    ledger_value = {
        "clockDomainId": ledger_clock_domain,
        "episodes": [
            {
                "episodeId": episode_id,
                "actions": [{
                    "checkpoint": f"checkpoint-{cell.lower()}",
                    "actionOccurrenceId": f"OCCURRENCE_{cell}",
                    "actionIndex": 1,
                    "deliveredMonotonicNanoseconds": (
                        action_delivered
                        + (first_ledger_delivery_delta if cell == "V1" else 0)
                    ),
                }],
            }
            for cell, episode_id in episodes
        ],
    }
    ledger_raw_sha = aggregator.canonical_sha256(ledger_value)
    action_ledger = {"value": ledger_value, "rawSha256": ledger_raw_sha}
    audio_raw = wav_48000()
    audio_raw_sha = aggregator.bytes_sha256(audio_raw)
    sync_value = {
        "schemaVersion": "gridworks.commercial-ux.native-audio-sync-ledger.v1",
        "protocol": aggregator.PROTOCOL,
        "sourceActionLedgerRawSha256": ledger_raw_sha,
        "clockDomainId": sync_clock_domain,
        "events": [
            {
                "syncEventId": f"AVSYNC-{cell}",
                "cellId": cell,
                "episodeId": episode_id,
                "checkpoint": f"checkpoint-{cell.lower()}",
                "actionOccurrenceId": f"OCCURRENCE_{cell}",
                "actionIndex": 1,
                "actionDeliveredMonotonicNanoseconds": action_delivered,
                "audioArtifactId": "coverage-audio",
                "audioArtifactRawSha256": audio_raw_sha,
                "audioCaptureStartedMonotonicNanoseconds": capture_started,
                "cueOnsetSampleIndex": cue_sample,
            }
            for cell, episode_id in episodes
        ],
    }
    sync_raw = json.dumps(
        sync_value,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    sync_raw_sha = aggregator.bytes_sha256(sync_raw)
    sync_artifact = {
        "artifactId": "coverage-audio-sync",
        "kind": "AUDIO_SYNC_LEDGER",
        "locator": "audio-sync.json",
        "rawSha256": sync_raw_sha,
    }
    audio_artifact = {
        "artifactId": "coverage-audio",
        "kind": "AUDIO",
        "locator": "audio.wav",
        "rawSha256": audio_raw_sha,
    }
    recording = {
        "value": {
            "actionLedgerArtifactRawSha256": ledger_raw_sha,
            "artifacts": [sync_artifact, audio_artifact],
        },
        "artifactRawByKey": {
            (sync_artifact["artifactId"], sync_artifact["locator"]): sync_raw,
            (audio_artifact["artifactId"], audio_artifact["locator"]): audio_raw,
        },
    }
    return recording, action_ledger


Labeler = Callable[[int, str, str, str], str]


def constant_label(label: str) -> Labeler:
    return lambda _judge, _kind, _artifact, _cell: label


def make_cell(cell_id: str, label: str) -> dict[str, Any]:
    return {
        "cellId": cell_id,
        "label": label,
        "confidence": "HIGH",
        "strengthEvidence": [
            {
                "checkpoint": "E01/fixture",
                "artifact": "frame-fixture",
                "observation": f"Visible fixture evidence for {cell_id}.",
            }
        ],
        "gapEvidence": [],
        "incidentKeys": [],
        "recommendedChange": None,
    }


def make_incident(
    incident_type: str,
    cap: int,
    actors: list[str],
    *,
    checkpoints: list[str] | None = None,
    critical: bool = False,
    oracle_status: str = "EXACT",
) -> dict[str, Any]:
    return {
        "incidentKey": f"FIRST_LIGHT/NONE/OPERATIONS/{incident_type}",
        "incidentType": incident_type,
        "actorArtifactIds": actors,
        "checkpointRefs": checkpoints or ["E01/fixture"],
        "verifierObservationId": "OBS-0001",
        "verifierStatus": "SUPPORTED",
        "oracleStatus": oracle_status,
        "capCandidate": cap,
        "critical": critical,
        "description": f"Fixture {incident_type} incident.",
    }


EPISODE_IDS = (
    "E00-TITLE",
    "E01-FIRST-LIGHT",
    "E02-SECOND-HEART",
    "E03-SECOND-SOURCE",
    "E04-NORTH-BANK",
    "E05-WHOSE-MARGIN",
    "E06-FLOOD",
    "E07-MAINTENANCE",
    "E08-FINALE",
    "E09-MID-RESUME",
    "E10-COMPLETE-RESUME",
    "E11-AUTHORED-TEXT",
)


def native_file_sha(name: str) -> str:
    return aggregator.file_sha256(aggregator.NATIVE_DIRECTORY / name, f"fixture {name}")


def write_self_hashed_json(path: Path, value: dict[str, Any], field: str) -> tuple[str, str]:
    value[field] = identity_sha(f"placeholder-{field}")
    value[field] = aggregator.self_sha256(value, field, f"fixture {path.name}")
    raw_sha = write_json(path, value)
    return value[field], raw_sha


def selected_recipe(recipe_id: str) -> dict[str, Any]:
    queue = json.loads((aggregator.NATIVE_DIRECTORY / "holdout-recipes.json").read_text())
    rows = [queue["formative"], *queue["holdouts"]]
    return copy.deepcopy(next(row for row in rows if row["id"] == recipe_id))


def make_candidate_manifest(
    directory: Path,
    recipe_id: str,
    rubric_sha: str,
    execution_artifact_sha: str,
    *,
    clean_tree: bool,
) -> tuple[dict[str, Any], Path, str]:
    phase = "FORMATIVE" if recipe_id == "FORMATIVE-01" else "OFFICIAL_HOLDOUT"
    recipe = selected_recipe(recipe_id)
    contract_files = {
        "contractBindingsSha256": "contract-bindings.json",
        "canonicalHashPolicySha256": "canonical-hash-policy.json",
        "rubricSha256": "../rubric.json",
        "coldActorPromptSha256": "cold-actor-prompt.template.txt",
        "coldActorResponseSchemaSha256": "cold-actor-response.schema.json",
        "actorActionLedgerSchemaSha256": "actor-action-ledger.schema.json",
        "actorObservationSchemaSha256": "actor-observation.schema.json",
        "actorTraceSchemaSha256": "actor-trace.schema.json",
        "coverageTraceSchemaSha256": "coverage-trace.schema.json",
        "evidenceSetSchemaSha256": "evidence-set.schema.json",
        "nativeJudgePromptSha256": "native-judge-prompt.template.txt",
        "nativeJudgeSchemaSha256": "native-judge.schema.json",
        "judgePanelSchemaSha256": "judge-panel.schema.json",
        "qualificationInputSchemaSha256": "qualification-input.schema.json",
        "qualificationReceiptSchemaSha256": "qualification-receipt.schema.json",
        "nativeVerifierPromptSha256": "native-evidence-verifier-prompt.template.txt",
        "nativeVerifierInputSchemaSha256": "native-evidence-verification-input.schema.json",
        "nativeVerifierSchemaSha256": "native-evidence-verifier.schema.json",
        "oracleHardGateSchemaSha256": "oracle-hard-gate-ledger.schema.json",
        "nativeAggregationInputSchemaSha256": "native-aggregation-input.schema.json",
        "nativeScorecardSchemaSha256": "native-scorecard.schema.json",
        "evaluationRunManifestSchemaSha256": "evaluation-run-manifest.schema.json",
        "nativeReplacementReceiptSchemaSha256": "native-replacement-receipt.schema.json",
        "nativeAggregatorSha256": "../aggregate-native.py",
    }
    contract_hashes: dict[str, str] = {}
    for field, relative in contract_files.items():
        path = {
            "../rubric.json": TOOL_DIRECTORY / "rubric.json",
            "../aggregate-native.py": AGGREGATOR_PATH,
        }.get(relative, aggregator.NATIVE_DIRECTORY / relative)
        contract_hashes[field] = aggregator.file_sha256(path, f"fixture contract {relative}")
    manifest = {
        "schemaVersion": aggregator.CANDIDATE_MANIFEST_SCHEMA,
        "protocol": aggregator.PROTOCOL,
        "candidateManifestSha256": identity_sha("candidate-placeholder"),
        "candidateId": "fixture-candidate",
        "evaluationPhase": phase,
        "officialCommercialUX": phase == "OFFICIAL_HOLDOUT",
        "source": {"commit": "a" * 40, "cleanTree": clean_tree},
        "evaluator": {
            "resolvedModelId": "gpt-5.6-sol",
            "reasoningEffort": "ultra",
            "actorCount": 3,
            "judgeCount": 3,
            "verifierCount": 1,
            "transportVersion": "fixture-responses-v1",
            "samplingSupported": False,
            "seedSupported": False,
            "samplingValue": None,
            "seedValue": None,
        },
        "contractHashes": contract_hashes,
        "authorityHashes": {
            field: identity_sha(f"authority-{field}")
            for field in (
                "world",
                "campaign",
                "coreReplay",
                "coreContracts",
                "deterministicWitness",
                "nativeSmokeWitness",
                "storyHarness",
                "storyManifestOutput",
            )
        },
        "recipes": {
            "coldJourneySha256": native_file_sha("cold-journey-recipe.json"),
            "coverageSha256": native_file_sha("coverage-recipe.json"),
            "holdoutQueueSha256": native_file_sha("holdout-recipes.json"),
            "selectedRecipeId": recipe_id,
            "selectedRecipeSha256": aggregator.canonical_sha256(recipe),
            "conceptExposureSha256": native_file_sha("concept-exposure-manifest.json"),
            "goldStateContractSha256": native_file_sha("gold-state-manifest.json"),
            "qualificationAnchorsSha256": native_file_sha("qualification-anchors.json"),
        },
        "execution": {
            "os": "macOS fixture",
            "architecture": "arm64",
            "viewport": "1920x1080",
            "uiScalesPercent": [100, 125],
            "inputModes": ["POINTER_KEYBOARD_ACTUAL_INPUT", "KEYBOARD_ONLY_ACTUAL_INPUT"],
            "reduceMotionValues": [False, True],
            "audioPercent": 100,
            "godotVersion": "4.7.1.stable.mono.official.a13da4feb",
            "componentPathPolicy": "CANONICAL_ABSOLUTE_REGULAR_FILE_REJECT_SYMLINKS",
            "executionArtifactHashRule": (
                "SHA256_OF_RFC8785_GODOT_EXECUTABLE_SHA256_MANAGED_ASSEMBLY_SHA256_"
                "PCK_RESOURCE_MANIFEST_SHA256_PACKAGE_SHA256_PACKAGE_STATUS"
            ),
            "godotExecutablePath": str((TOOL_DIRECTORY / "fixture-godot").resolve()),
            "godotExecutableSha256": identity_sha("godot-executable"),
            "managedAssemblyPath": str((TOOL_DIRECTORY / "fixture-assembly.dll").resolve()),
            "managedAssemblySha256": identity_sha("managed-assembly"),
            "pckResourceManifestPath": str(
                (TOOL_DIRECTORY / "fixture-pck-manifest.json").resolve()
            ),
            "pckResourceManifestSha256": identity_sha("pck-manifest"),
            "executionArtifactSha256": execution_artifact_sha,
            "packagePath": None,
            "packageSha256": None,
            "packageStatus": "EDITOR_NATIVE_NOT_PUBLIC_PACKAGE",
        },
    }
    path = directory / "candidate-manifest.json"
    _, raw_sha = write_self_hashed_json(path, manifest, "candidateManifestSha256")
    return manifest, path, raw_sha


def make_qualification_receipt(
    directory: Path,
    manifest: dict[str, Any],
    rubric_sha: str,
    status: str,
) -> tuple[dict[str, Any], Path, str]:
    def slot(attempt: int, index: int, passed: bool) -> dict[str, Any]:
        return {
            "slotId": f"JUDGE-0{index}",
            "judgeRunId": f"qualification-{attempt}-{index}",
            "judgmentRawSha256": identity_sha(f"qualification-output-{attempt}-{index}"),
            "exactCount": 20 if passed else 18,
            "excellentAndBrokenAllExact": passed,
            "schemaValidCount": 20,
            "status": "PASS" if passed else "FAIL_BAND",
        }

    if status == "PASS":
        attempts = [
            {
                "attempt": 1,
                "slots": [slot(1, index, True) for index in range(1, 4)],
                "status": "PASS",
            }
        ]
    else:
        attempts = [
            {
                "attempt": attempt,
                "slots": [slot(attempt, index, False) for index in range(1, 4)],
                "status": "INVALIDATED" if attempt == 1 else "FAIL",
            }
            for attempt in (1, 2)
        ]
    receipt = {
        "schemaVersion": aggregator.QUALIFICATION_RECEIPT_SCHEMA,
        "protocol": aggregator.PROTOCOL,
        "qualificationReceiptSha256": identity_sha("qualification-placeholder"),
        "candidateIndependent": True,
        "model": "gpt-5.6-sol",
        "reasoningEffort": "ultra",
        "transportVersion": manifest["evaluator"]["transportVersion"],
        "promptTemplateSha256": manifest["contractHashes"]["nativeJudgePromptSha256"],
        "judgmentSchemaSha256": manifest["contractHashes"]["nativeJudgeSchemaSha256"],
        "qualificationInputSchemaSha256": manifest["contractHashes"]["qualificationInputSchemaSha256"],
        "qualificationInputSha256": identity_sha("qualification-input"),
        "qualificationAnchorsAuthoritySha256": manifest["recipes"]["qualificationAnchorsSha256"],
        "qualificationTransportMapSha256": native_file_sha("qualification-transport-map.json"),
        "rubricSha256": rubric_sha,
        "passRule": {
            "minimumExactPerJudge": 19,
            "anchorCount": 20,
            "excellentAndBrokenAllExact": True,
            "schemaValidCount": 20,
            "fullPanelReplacementMaximum": 1,
        },
        "attempts": attempts,
        "status": status,
    }
    path = directory / "qualification-receipt.json"
    _, raw_sha = write_self_hashed_json(path, receipt, "qualificationReceiptSha256")
    return receipt, path, raw_sha


def actor_artifact_ref(actor_index: int) -> dict[str, str]:
    return {
        "artifactId": f"actor-{actor_index}-frame",
        "kind": "FRAME",
        "locator": f"fixture/actor-{actor_index}-frame.png",
    }


def make_actor_observation(
    actor_index: int,
    panel_suffix: str,
    anonymous_id: str,
    ledger_incidents: list[dict[str, Any]],
    terminal: tuple[str, str | None],
) -> dict[str, Any]:
    state_seed = identity_sha(f"actor-state-{actor_index}-{panel_suffix}")
    next_action = 1
    actions = [
        {
            "actionIndex": next_action,
            "episode": "E01-FIRST-LIGHT",
            "checkpoint": "first-operations",
            "actionKind": "OPEN_BRIEFING",
            "inputEvent": "fixture input",
            "visibleFeedback": "fixture visible response",
            "audibleFeedback": "fixture audible response",
            "preStateSha256": state_seed,
            "postStateSha256": identity_sha(f"actor-post-{actor_index}-{panel_suffix}"),
            "appActive": True,
            "rationalInProductAction": True,
        }
    ]
    next_action += 1
    actor_incidents: list[dict[str, Any]] = []
    for incident in ledger_incidents:
        if anonymous_id not in incident["actorArtifactIds"]:
            continue
        kind = incident["incidentType"]
        if kind == "HARD_GATE_FAILURE":
            continue
        action_indexes: list[int] = []
        if kind == "RECOVERY_FRICTION":
            for offset, action_kind in enumerate(("RECOVER_SELECT", "RECOVER_ROUTE", "RECOVER_CONFIRM")):
                action_indexes.append(next_action)
                actions.append(
                    {
                        "actionIndex": next_action,
                        "episode": "E01-FIRST-LIGHT",
                        "checkpoint": "first-operations",
                        "actionKind": action_kind,
                        "inputEvent": f"recovery input {offset}",
                        "visibleFeedback": "state remained visibly unchanged",
                        "audibleFeedback": "",
                        "preStateSha256": state_seed,
                        "postStateSha256": state_seed,
                        "appActive": True,
                        "rationalInProductAction": True,
                    }
                )
                next_action += 1
        elif kind == "UX_STALL":
            for offset in range(12):
                action_indexes.append(next_action)
                actions.append(
                    {
                        "actionIndex": next_action,
                        "episode": "E01-FIRST-LIGHT",
                        "checkpoint": "first-operations",
                        "actionKind": f"STALL_ACTION_{offset + 1}",
                        "inputEvent": f"stall input {offset}",
                        "visibleFeedback": "no progress state change",
                        "audibleFeedback": "",
                        "preStateSha256": state_seed,
                        "postStateSha256": state_seed,
                        "appActive": True,
                        "rationalInProductAction": True,
                    }
                )
                next_action += 1
        severe = kind in {
            "CONFUSION",
            "RECOVERY_FRICTION",
            "UX_STALL",
            "EXTERNAL_HINT_ATTEMPT",
        }
        actor_incidents.append(
            {
                "incidentKey": incident["incidentKey"],
                "incidentOrdinal": len(actor_incidents) + 1,
                "episode": "E01-FIRST-LIGHT",
                "checkpointOrdinals": [1, 2] if kind == "CONFUSION" else [1],
                "incidentType": kind,
                "confusionBoundary": "ACTUAL_PROJECTION" if kind == "CONFUSION" else None,
                "severity": "SEVERE" if severe else "LOCAL",
                "description": f"Actor fixture incident {kind}.",
                "actionIndexes": action_indexes,
                "artifactRefs": [actor_artifact_ref(actor_index)],
            }
        )
    terminal_state, terminal_key = terminal
    terminal_ordinal = next(
        (
            row["incidentOrdinal"]
            for row in actor_incidents
            if row["incidentKey"] == terminal_key
        ),
        None,
    )
    return {
        "schemaVersion": aggregator.ACTOR_OBSERVATION_SCHEMA,
        "protocol": aggregator.PROTOCOL,
        "coldActorResponseSha256": identity_sha(
            f"pending-cold-response-{actor_index}-{panel_suffix}"
        ),
        "coldActorResponseRawSha256": identity_sha(
            f"pending-cold-response-raw-{actor_index}-{panel_suffix}"
        ),
        "actorRunId": f"actor-{panel_suffix}-{actor_index}",
        "actorSlot": "SOL-ULTRA",
        "model": "gpt-5.6-sol",
        "reasoningEffort": "ultra",
        "objective": "에필로그까지 플레이하세요.",
        "freshUserData": True,
        "checkpoints": [
            {
                "ordinal": ordinal,
                "episode": "E01-FIRST-LIGHT",
                "checkpoint": checkpoint,
                "recipeCheckpointSequenceOrdinal": recipe_ordinal,
                "appActiveActionIndex": 1,
                "progressStateSha256": actions[0]["postStateSha256"],
                "artifactRefs": [actor_artifact_ref(actor_index)],
            }
            for ordinal, checkpoint, recipe_ordinal in (
                (1, "first-operations", 4),
                (2, "first-energized-path", 8),
            )
        ],
        "firstUseRecords": [
            {
                "probeId": "PX01-FIXTURE",
                "firstUseOrdinal": 1,
                "episode": "E01-FIRST-LIGHT",
                "checkpoint": "first-operations",
                "checkpointOrdinal": 1,
                "currentGoal": "Finish the fixture journey.",
                "expectedVisibleConsequence": "The fixture state should advance.",
                "citedVisibleSource": actor_artifact_ref(actor_index),
                "citedVisibleSourceDescription": "The fixture frame shows the first use.",
            }
        ],
        "approvalRecords": [
            {
                "approvalOrdinal": 1,
                "episode": "E01-FIRST-LIGHT",
                "checkpoint": "first-operations",
                "checkpointOrdinal": 1,
                "predictionImmediatelyBeforeApproval": "The fixture will complete.",
                "observedResult": "The fixture completed.",
                "causalAccount": "The submitted fixture satisfied the visible objective.",
                "artifactRefs": [actor_artifact_ref(actor_index)],
            },
            {
                "approvalOrdinal": 2,
                "episode": "E01-FIRST-LIGHT",
                "checkpoint": "first-energized-path",
                "checkpointOrdinal": 2,
                "predictionImmediatelyBeforeApproval": "The second fixture step will complete.",
                "observedResult": "The second fixture step completed.",
                "causalAccount": "The visible second action caused the fixture transition.",
                "artifactRefs": [actor_artifact_ref(actor_index)],
            },
        ],
        "actionLedger": actions,
        "incidents": actor_incidents,
        "terminalState": terminal_state,
        "terminalIncidentKey": terminal_key,
        "terminalIncidentOrdinal": terminal_ordinal,
    }


def make_coverage_trace(
    directory: Path,
    manifest: dict[str, Any],
    coverage_suffix: str,
) -> tuple[dict[str, Any], Path, str]:
    recipe = json.loads((aggregator.NATIVE_DIRECTORY / "coverage-recipe.json").read_text())
    actions_by_episode = {row["id"]: row["actions"] for row in recipe["episodes"]}
    episodes = []
    trace_ordinal = 1
    for index, episode_id in enumerate(EPISODE_IDS, start=1):
        artifact = {
            "artifactId": f"coverage-frame-{index}",
            "kind": "FRAME",
            "sha256": identity_sha(f"coverage-frame-{index}-{coverage_suffix}"),
            "mimeType": "image/png",
            "locator": f"fixture/coverage-frame-{index}.png",
        }
        trace_rows = []
        for action_index, occurrence_id in enumerate(actions_by_episode[episode_id], start=1):
            trace_rows.append(
                {
                    "traceRowId": f"TRACE-{trace_ordinal:04d}",
                    "checkpoint": f"coverage-checkpoint-{index}",
                    "actionOccurrenceId": occurrence_id,
                    "semanticActionKind": "ADVANCE_FIXTURE",
                    "actionIndex": action_index,
                    "preStateSha256": identity_sha(
                        f"coverage-pre-{index}-{action_index}-{coverage_suffix}"
                    ),
                    "postStateSha256": identity_sha(
                        f"coverage-post-{index}-{action_index}-{coverage_suffix}"
                    ),
                    "visibleFeedback": "Fixture visible feedback.",
                    "audibleFeedback": "Fixture audible feedback.",
                    "artifactRefs": [artifact],
                }
            )
            trace_ordinal += 1
        episodes.append(
            {
                "episodeId": episode_id,
                "prefixId": f"PREFIX-{episode_id}",
                "checkpointIds": [f"coverage-checkpoint-{index}"],
                "actionLedgerSha256": identity_sha(f"coverage-ledger-{index}-{coverage_suffix}"),
                "traceRows": trace_rows,
                "mediaArtifacts": [artifact],
                "terminalState": "COMPLETED",
                "terminalIncidentKey": None,
            }
        )
    trace = {
        "schemaVersion": aggregator.COVERAGE_TRACE_SCHEMA,
        "protocol": aggregator.PROTOCOL,
        "coverageArtifactId": identity_sha("coverage-placeholder"),
        "candidateManifestSha256": manifest["candidateManifestSha256"],
        "executionArtifactSha256": manifest["execution"]["executionArtifactSha256"],
        "coverageRecipeSha256": manifest["recipes"]["coverageSha256"],
        "holdoutQueueSha256": manifest["recipes"]["holdoutQueueSha256"],
        "selectedRecipeId": manifest["recipes"]["selectedRecipeId"],
        "selectedRecipeSha256": manifest["recipes"]["selectedRecipeSha256"],
        "conceptManifestSha256": manifest["recipes"]["conceptExposureSha256"],
        "goldBindingManifestSha256": manifest["recipes"]["goldStateContractSha256"],
        "processTreeId": f"coverage-process-{coverage_suffix}",
        "runnerId": "COVERAGE_FINAL_PACKAGER",
        "userDataSha256": identity_sha(f"coverage-user-data-{coverage_suffix}"),
        "journalBundleSha256": identity_sha(f"coverage-journal-{coverage_suffix}"),
        "recordingManifestSha256": identity_sha(f"coverage-recording-{coverage_suffix}"),
        "episodes": episodes,
    }
    path = directory / "coverage-trace.json"
    _, raw_sha = write_self_hashed_json(path, trace, "coverageArtifactId")
    return trace, path, raw_sha


def evidence_media(artifact_id: str, suffix: str) -> dict[str, str]:
    return {
        "artifactId": artifact_id,
        "kind": "FRAME",
        "sha256": identity_sha(f"media-{artifact_id}-{suffix}"),
        "mimeType": "image/png",
        "locator": f"fixture/{artifact_id}.png",
    }


def evidence_trace_row(index: int, suffix: str) -> dict[str, Any]:
    return {
        "traceRowId": f"TRACE-{index:04d}",
        "episode": "E01-FIRST-LIGHT",
        "checkpoint": f"evidence-checkpoint-{index}",
        "appActiveActionIndex": index,
        "currentGoal": "Complete the fixture objective.",
        "expectedVisibleConsequence": "The fixture should visibly advance.",
        "citedVisibleSources": [
            {"artifactId": f"evidence-frame-{index}", "locator": f"fixture/evidence-frame-{index}.png"}
        ],
        "inputEvent": "fixture input",
        "visibleFeedback": "fixture visible feedback",
        "audibleFeedback": "fixture audible feedback",
        "progressStateSha256": identity_sha(f"evidence-progress-{index}-{suffix}"),
        "predictionImmediatelyBeforeApproval": "The fixture should complete.",
        "observedResult": "The fixture completed.",
        "causalAccount": "The visible action caused the fixture transition.",
        "incidentKeys": [],
    }


def make_fixture(
    directory: Path,
    *,
    recipe_id: str = "HOLDOUT-01",
    labeler: Labeler | None = None,
    cold_suffix: str = "base",
    coverage_suffix: str = "base",
    panel_suffix: str = "base",
    candidate_manifest_sha: str | None = None,
    incidents: list[dict[str, Any]] | None = None,
    gate_overrides: dict[str, tuple[str, str | None]] | None = None,
    score_bearing_ready: bool = True,
    operational_blocker: str | None = None,
    not_reached: list[str] | None = None,
    expected_observation_ids: list[str] | None = None,
    verifier_observation_ids: list[str] | None = None,
    verifier_verdicts: dict[str, str] | None = None,
    difference_report: dict[str, Any] | None = None,
    clean_tree: bool = True,
    qualification_status: str = "PASS",
    actor_terminals: dict[str, tuple[str, str | None]] | None = None,
) -> dict[str, Any]:
    directory.mkdir(parents=True, exist_ok=True)
    labeler = labeler or constant_label("STRONG")
    incidents = copy.deepcopy(incidents or [])
    gate_overrides = gate_overrides or {}
    not_reached = list(not_reached or [])
    expected_observation_ids = expected_observation_ids or ["OBS-0001"]
    verifier_observation_ids = verifier_observation_ids or list(expected_observation_ids)
    verifier_verdicts = verifier_verdicts or {}
    if qualification_status not in {"PASS", "BLOCKED_JUDGE_QUALIFICATION"}:
        raise ValueError("fixture qualification status is invalid")
    actor_terminals = dict(actor_terminals or {})
    for incident in incidents:
        if incident["incidentType"] != "UX_STALL":
            continue
        for anonymous_id in incident["actorArtifactIds"]:
            actor_terminals.setdefault(
                anonymous_id,
                ("PLAYER_STALLED", incident["incidentKey"]),
            )

    rubric_path = TOOL_DIRECTORY / "rubric.json"
    rubric_bytes = rubric_path.read_bytes()
    rubric_sha = aggregator.bytes_sha256(rubric_bytes)
    rubric_value = json.loads(rubric_bytes)
    rubric = aggregator.load_rubric(rubric_value, rubric_sha)

    bindings = [
        {
            "anonymousArtifactId": "ARTIFACT-A",
            "artifactKind": "COLD_ACTOR",
            "artifactSha256": identity_sha(f"cold-a-{cold_suffix}"),
        },
        {
            "anonymousArtifactId": "ARTIFACT-B",
            "artifactKind": "COLD_ACTOR",
            "artifactSha256": identity_sha(f"cold-b-{cold_suffix}"),
        },
        {
            "anonymousArtifactId": "ARTIFACT-C",
            "artifactKind": "COLD_ACTOR",
            "artifactSha256": identity_sha(f"cold-c-{cold_suffix}"),
        },
        {
            "anonymousArtifactId": "ARTIFACT-D",
            "artifactKind": "COVERAGE",
            "artifactSha256": identity_sha(f"coverage-{coverage_suffix}"),
        },
    ]

    execution_artifact_sha = identity_sha("execution-artifact")
    candidate_manifest, candidate_manifest_path, candidate_manifest_raw_sha = make_candidate_manifest(
        directory,
        recipe_id,
        rubric_sha,
        execution_artifact_sha,
        clean_tree=clean_tree,
    )
    if candidate_manifest_sha is not None:
        candidate_manifest["candidateManifestSha256"] = candidate_manifest_sha
        candidate_manifest_raw_sha = write_json(candidate_manifest_path, candidate_manifest)
    qualification, qualification_path, qualification_raw_sha = make_qualification_receipt(
        directory,
        candidate_manifest,
        rubric_sha,
        qualification_status,
    )

    actor_observations: list[dict[str, Any]] = []
    actor_observation_paths: list[Path] = []
    actor_observation_raw_shas: list[str] = []
    actor_artifact_ids: list[str] = []
    for actor_index, anonymous_id in enumerate(("ARTIFACT-A", "ARTIFACT-B", "ARTIFACT-C"), start=1):
        terminal = actor_terminals.get(anonymous_id, ("COMPLETED", None))
        observation = make_actor_observation(
            actor_index,
            panel_suffix,
            anonymous_id,
            incidents,
            terminal,
        )
        path = directory / f"actor-observation-{actor_index}.json"
        raw_sha = write_json(path, observation)
        actor_observations.append(observation)
        actor_observation_paths.append(path)
        actor_observation_raw_shas.append(raw_sha)
        actor_artifact_ids.append(identity_sha(f"actor-artifact-{actor_index}-{cold_suffix}"))

    coverage_trace, coverage_trace_path, coverage_trace_raw_sha = make_coverage_trace(
        directory,
        candidate_manifest,
        coverage_suffix,
    )

    cold_episode_ids = list(EPISODE_IDS[:10])
    evidence_artifacts: list[dict[str, Any]] = []
    for index, binding in enumerate(bindings[:3], start=1):
        terminal_state, terminal_key = actor_terminals.get(
            binding["anonymousArtifactId"],
            ("COMPLETED", None),
        )
        evidence_artifacts.append(
            {
                "anonymousArtifactId": binding["anonymousArtifactId"],
                "artifactKind": "COLD_ACTOR",
                "sourceArtifactSha256": actor_artifact_ids[index - 1],
                "sanitizedArtifactSha256": binding["artifactSha256"],
                "assignedCells": list(aggregator.COLD_CELLS),
                "episodeIds": cold_episode_ids,
                "traceRows": [evidence_trace_row(index, panel_suffix)],
                "mediaArtifacts": [evidence_media(f"evidence-frame-{index}", panel_suffix)],
                "terminalState": terminal_state,
                "terminalIncidentKey": terminal_key,
            }
        )
    evidence_artifacts.append(
        {
            "anonymousArtifactId": "ARTIFACT-D",
            "artifactKind": "COVERAGE",
            "sourceArtifactSha256": coverage_trace["coverageArtifactId"],
            "sanitizedArtifactSha256": bindings[3]["artifactSha256"],
            "assignedCells": list(aggregator.COVERAGE_CELLS),
            "episodeIds": list(EPISODE_IDS),
            "traceRows": [evidence_trace_row(4, panel_suffix)],
            "mediaArtifacts": [evidence_media("evidence-frame-4", panel_suffix)],
            "terminalState": "COMPLETED",
            "terminalIncidentKey": None,
        }
    )
    evidence_set = {
        "schemaVersion": aggregator.EVIDENCE_SET_SCHEMA,
        "protocol": aggregator.PROTOCOL,
        "evidenceSetSha256": identity_sha("evidence-placeholder"),
        "rubricSha256": rubric_sha,
        "conceptManifestSha256": candidate_manifest["recipes"]["conceptExposureSha256"],
        "recipeId": recipe_id,
        "recipeSha256": candidate_manifest["recipes"]["selectedRecipeSha256"],
        "holdoutQueueSha256": candidate_manifest["recipes"]["holdoutQueueSha256"],
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "executionArtifactSha256": execution_artifact_sha,
        "anonymizationManifestSha256": identity_sha(f"anonymization-{panel_suffix}"),
        "artifactOrder": [row["anonymousArtifactId"] for row in bindings],
        "artifacts": evidence_artifacts,
    }
    evidence_set_path = directory / "evidence-set.json"
    evidence_set_sha, _ = write_self_hashed_json(
        evidence_set_path,
        evidence_set,
        "evidenceSetSha256",
    )

    provenance = {
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "candidateManifestRawSha256": candidate_manifest_raw_sha,
        "qualificationReceiptSha256": qualification["qualificationReceiptSha256"],
        "qualificationReceiptRawSha256": qualification_raw_sha,
        "judgePanelSha256": identity_sha("pending-panel-self"),
        "judgePanelRawSha256": identity_sha("pending-panel-raw"),
        "evaluationRunManifestSha256": identity_sha("pending-evaluation-self"),
        "evaluationRunManifestRawSha256": identity_sha("pending-evaluation-raw"),
        "sourceCommit": "a" * 40,
        "cleanTree": clean_tree,
        "model": "gpt-5.6-sol",
        "reasoningEffort": "ultra",
        "promptTemplateSha256": aggregator.file_sha256(
            aggregator.NATIVE_DIRECTORY / "native-judge-prompt.template.txt",
            "judge prompt",
        ),
        "judgmentSchemaSha256": aggregator.file_sha256(
            aggregator.NATIVE_DIRECTORY / "native-judge.schema.json",
            "judge schema",
        ),
        "verifierPromptTemplateSha256": aggregator.file_sha256(
            aggregator.NATIVE_DIRECTORY / "native-evidence-verifier-prompt.template.txt",
            "verifier prompt",
        ),
        "verifierSchemaSha256": aggregator.file_sha256(
            aggregator.NATIVE_DIRECTORY / "native-evidence-verifier.schema.json",
            "verifier schema",
        ),
        "rubricSha256": rubric_sha,
        "coldRecipeSha256": aggregator.file_sha256(
            aggregator.NATIVE_DIRECTORY / "cold-journey-recipe.json",
            "cold recipe",
        ),
        "coverageRecipeSha256": aggregator.file_sha256(
            aggregator.NATIVE_DIRECTORY / "coverage-recipe.json",
            "coverage recipe",
        ),
        "holdoutRecipeSha256": aggregator.file_sha256(
            aggregator.NATIVE_DIRECTORY / "holdout-recipes.json",
            "holdout recipes",
        ),
        "evidenceSetSha256": evidence_set_sha,
        "verificationOutputSha256": identity_sha("pending-verifier"),
        "oracleHardGateLedgerSha256": identity_sha("pending-ledger"),
        "nativeAggregatorSha256": aggregator.file_sha256(
            AGGREGATOR_PATH,
            "native aggregator",
        ),
        "executionArtifactSha256": execution_artifact_sha,
        "packageSha256": None,
        "packageStatus": "EDITOR_NATIVE_NOT_PUBLIC_PACKAGE",
        "evaluationSessionClaimSha256": identity_sha(
            f"evaluation-session-claim-{panel_suffix}"
        ),
        "evaluationSessionClaimRawSha256": identity_sha(
            f"evaluation-session-claim-raw-{panel_suffix}"
        ),
        "evaluationSessionPolicySha256": identity_sha(
            "evaluation-session-policy"
        ),
        "evaluationSessionClaimToolSha256": identity_sha(
            "evaluation-session-claim-tool"
        ),
        "evaluationSessionId": identity_sha(
            f"evaluation-session-id-{panel_suffix}"
        ),
        "evaluationSessionMode": "INITIAL",
        "evaluationAttemptAuditSha256": identity_sha(
            f"evaluation-attempt-audit-{panel_suffix}"
        ),
        "evaluationSelectedAttemptsSha256": identity_sha(
            f"evaluation-selected-attempts-{panel_suffix}"
        ),
    }

    candidate = {
        "schemaVersion": aggregator.AGGREGATION_INPUT_SCHEMA,
        "protocol": aggregator.PROTOCOL,
        "scorecardId": f"scorecard-{panel_suffix}",
        "recipeId": recipe_id,
        "operationalBlocker": operational_blocker,
        "verificationInputSha256": identity_sha(f"verification-input-{panel_suffix}"),
        "expectedObservationIds": list(expected_observation_ids),
        "notReachedByProductCellIds": not_reached,
        "artifactBindings": bindings,
        "differenceReport": copy.deepcopy(difference_report or base_difference_report()),
        "provenance": provenance,
    }

    judgments: list[dict[str, Any]] = []
    for judge_index in range(3):
        artifact_judgments: list[dict[str, Any]] = []
        for binding in bindings:
            cells = (
                aggregator.COLD_CELLS
                if binding["artifactKind"] == "COLD_ACTOR"
                else aggregator.COVERAGE_CELLS
            )
            artifact_judgments.append(
                {
                    "anonymousArtifactId": binding["anonymousArtifactId"],
                    "artifactKind": binding["artifactKind"],
                    "artifactSha256": binding["artifactSha256"],
                    "cells": [
                        make_cell(
                            cell_id,
                            labeler(
                                judge_index,
                                binding["artifactKind"],
                                binding["anonymousArtifactId"],
                                cell_id,
                            ),
                        )
                        for cell_id in cells
                    ],
                }
            )
        judgments.append(
            {
                "schemaVersion": aggregator.JUDGMENT_SCHEMA,
                "protocol": aggregator.PROTOCOL,
                "judgmentMode": "EVIDENCE_SET",
                "judgeRunId": f"judge-{panel_suffix}-{judge_index + 1}",
                "judgeSlot": "SOL-ULTRA",
                "model": "gpt-5.6-sol",
                "reasoningEffort": "ultra",
                "promptTemplateSha256": provenance["promptTemplateSha256"],
                "judgmentSchemaSha256": provenance["judgmentSchemaSha256"],
                "rubricSha256": rubric_sha,
                "judgeInputSha256": identity_sha(f"judge-input-{panel_suffix}"),
                "evidenceSetSha256": evidence_set_sha,
                "artifactJudgments": artifact_judgments,
            }
        )

    judgment_paths: list[Path] = []
    judgment_raw_shas: list[str] = []
    for index, judgment in enumerate(judgments, start=1):
        path = directory / f"judge-{index}.json"
        judgment_raw_shas.append(write_json(path, judgment))
        judgment_paths.append(path)

    judge_panel = {
        "schemaVersion": aggregator.JUDGE_PANEL_MANIFEST_SCHEMA,
        "protocol": aggregator.PROTOCOL,
        "judgePanelSha256": identity_sha("panel-placeholder"),
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "qualificationReceiptSha256": qualification["qualificationReceiptSha256"],
        "evaluationPhase": candidate_manifest["evaluationPhase"],
        "officialCommercialUX": candidate_manifest["officialCommercialUX"],
        "recipeId": recipe_id,
        "evidenceSetSha256": evidence_set_sha,
        "model": "gpt-5.6-sol",
        "reasoningEffort": "ultra",
        "transportVersion": candidate_manifest["evaluator"]["transportVersion"],
        "promptTemplateSha256": provenance["promptTemplateSha256"],
        "judgmentSchemaSha256": provenance["judgmentSchemaSha256"],
        "rubricSha256": rubric_sha,
        "panelKind": "INITIAL",
        "changedLanes": ["COLD-JOURNEY", "COVERAGE-JOURNEY"],
        "replacementForPanelSha256": None,
        "replacementAuthoritySha256": None,
        "artifactOrder": [row["anonymousArtifactId"] for row in bindings],
        "slots": [
            {
                "slotId": f"JUDGE-0{index}",
                "judgeRunId": judgments[index - 1]["judgeRunId"],
                "judgeInputSha256": judgments[index - 1]["judgeInputSha256"],
                "judgmentRawSha256": judgment_raw_shas[index - 1],
            }
            for index in range(1, 4)
        ],
    }
    judge_panel_path = directory / "judge-panel.json"
    judge_panel_sha, judge_panel_raw_sha = write_self_hashed_json(
        judge_panel_path,
        judge_panel,
        "judgePanelSha256",
    )
    provenance["judgePanelSha256"] = judge_panel_sha
    provenance["judgePanelRawSha256"] = judge_panel_raw_sha
    verifier = {
        "schemaVersion": aggregator.VERIFICATION_SCHEMA,
        "protocol": aggregator.PROTOCOL,
        "verifierRunId": f"verifier-{panel_suffix}",
        "verifierSlot": "SOL-ULTRA",
        "model": "gpt-5.6-sol",
        "reasoningEffort": "ultra",
        "promptTemplateSha256": provenance["verifierPromptTemplateSha256"],
        "verifierSchemaSha256": provenance["verifierSchemaSha256"],
        "verificationInputSchemaSha256": aggregator.file_sha256(
            aggregator.NATIVE_DIRECTORY / "native-evidence-verification-input.schema.json",
            "verification input schema",
        ),
        "verificationInputSha256": candidate["verificationInputSha256"],
        "evidenceSetSha256": provenance["evidenceSetSha256"],
        "opaqueJudgePanelSha256": judge_panel_sha,
        "observations": [
            {
                "observationId": observation_id,
                "claimType": "JUDGE_EVIDENCE",
                "incidentKey": None,
                "verdict": verifier_verdicts.get(observation_id, "SUPPORTED"),
                "citedSources": [
                    {
                        "anonymousArtifactId": "ARTIFACT-A",
                        "artifactId": "frame-fixture",
                        "locator": "fixture/frame-fixture.png",
                    }
                ],
                "rationale": "The cited fixture directly supports the observation.",
            }
            for observation_id in verifier_observation_ids
        ],
    }
    verifier_path = directory / "verifier.json"
    provenance["verificationOutputSha256"] = write_json(verifier_path, verifier)

    hard_gates: list[dict[str, Any]] = []
    for gate_id in aggregator.HARD_GATE_IDS:
        status, failure_code = gate_overrides.get(gate_id, ("PASS", None))
        producer = "FIXTURE_PRODUCER"
        predicate = f"Fixture predicate for {gate_id}."
        if gate_id == "HG09-AUDIO":
            producer = "RECORDING_AV_SYNC_VALIDATOR"
            predicate = (
                "FOUR_V1_V4_ACTION_TO_CUE_ONSETS_DERIVED_FROM_RAW_LEDGER_"
                "AND_48000HZ_WAV_WITHIN_100_MS"
            )
        hard_gates.append(
            {
                "gateId": gate_id,
                "producer": producer,
                "predicate": predicate,
                "inputHashes": [identity_sha(f"gate-input-{gate_id}-{panel_suffix}")],
                "status": status,
                "observed": f"Fixture observed state for {gate_id}.",
                "failureCode": failure_code,
                "evidenceRefs": [one_reference(f"gate-{gate_id}-{panel_suffix}")],
            }
        )
    ledger = {
        "schemaVersion": aggregator.ORACLE_LEDGER_SCHEMA,
        "protocol": aggregator.PROTOCOL,
        "ledgerId": f"ledger-{panel_suffix}",
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "evidenceSetSha256": provenance["evidenceSetSha256"],
        "verificationOutputSha256": provenance["verificationOutputSha256"],
        "rubricSha256": rubric_sha,
        "contractBindingsSha256": identity_sha("contract-bindings"),
        "oracleChecks": [
            {
                "oracleCheckId": "ORACLE-0001",
                "domain": "STATE_HASH",
                "inputHashes": [identity_sha(f"oracle-input-{panel_suffix}")],
                "expectedCanonicalSha256": identity_sha("oracle-exact"),
                "observedCanonicalSha256": identity_sha("oracle-exact"),
                "status": "EXACT",
                "details": "Fixture exact state hash.",
                "evidenceRefs": [one_reference(f"oracle-{panel_suffix}")],
            }
        ],
        "hardGates": hard_gates,
        "incidents": incidents,
        "scoreBearingReady": score_bearing_ready,
    }
    ledger_path = directory / "oracle-ledger.json"
    provenance["oracleHardGateLedgerSha256"] = write_json(ledger_path, ledger)

    incident_keys_by_actor: list[list[str]] = []
    severe_keys_by_actor: list[list[str]] = []
    for observation in actor_observations:
        incident_keys = [row["incidentKey"] for row in observation["incidents"]]
        severe_keys = [
            row["incidentKey"]
            for row in observation["incidents"]
            if row["severity"] == "SEVERE"
        ]
        incident_keys_by_actor.append(incident_keys)
        severe_keys_by_actor.append(severe_keys)
    severe_counts = {
        key: sum(key in keys for keys in severe_keys_by_actor)
        for keys in severe_keys_by_actor
        for key in keys
    }
    evaluation_run = {
        "schemaVersion": aggregator.EVALUATION_RUN_SCHEMA,
        "protocol": aggregator.PROTOCOL,
        "evaluationRunManifestSha256": identity_sha("evaluation-placeholder"),
        "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
        "qualificationReceiptSha256": qualification["qualificationReceiptSha256"],
        "judgePanelSha256": judge_panel_sha,
        "evaluationPhase": candidate_manifest["evaluationPhase"],
        "officialCommercialUX": candidate_manifest["officialCommercialUX"],
        "recipeId": recipe_id,
        "goldBindings": {
            "bindingManifestSha256": candidate_manifest["recipes"]["goldStateContractSha256"],
            "prefixCount": 12,
            "checkpointCount": 49,
            "applicableBindingCount": 60,
            "boundBindingCount": 60,
            "notApplicableBindingCount": 1,
            "allApplicableBindingsExact": True,
            "e09NorthBankTwoProcessWitness": True,
        },
        "artifacts": {
            "candidateManifestRawSha256": candidate_manifest_raw_sha,
            "qualificationReceiptRawSha256": qualification_raw_sha,
            "judgePanelRawSha256": judge_panel_raw_sha,
            "actorObservationRawSha256": actor_observation_raw_shas,
            "actorArtifactIds": actor_artifact_ids,
            "userDataSha256": [identity_sha(f"user-data-{index}-{cold_suffix}") for index in range(1, 4)],
            "saveSha256": [None, None, None],
            "journalSha256": [identity_sha(f"journal-{index}-{cold_suffix}") for index in range(1, 4)],
            "recordingManifestSha256": [identity_sha(f"recording-{index}-{cold_suffix}") for index in range(1, 4)],
            "actorRunIds": [row["actorRunId"] for row in actor_observations],
            "coverageArtifactId": coverage_trace["coverageArtifactId"],
            "coverageTraceRawSha256": coverage_trace_raw_sha,
            "evidenceSetSha256": evidence_set_sha,
            "judgeJudgmentRawSha256": judgment_raw_shas,
            "verifierRunId": verifier["verifierRunId"],
            "verificationOutputSha256": provenance["verificationOutputSha256"],
            "oracleHardGateLedgerSha256": provenance["oracleHardGateLedgerSha256"],
        },
        "retryLedger": [],
        "invalidationLedger": [],
        "terminalStates": [
            {
                "actorArtifactId": actor_artifact_ids[index],
                "actorObservationRawSha256": actor_observation_raw_shas[index],
                "state": actor_observations[index]["terminalState"],
                "severeSingleRun": any(
                    severe_counts[key] == 1 for key in severe_keys_by_actor[index]
                ),
                "incidentKeys": incident_keys_by_actor[index],
                "terminalIncidentKey": actor_observations[index]["terminalIncidentKey"],
            }
            for index in range(3)
        ],
        "replacementAuthority": {
            "panelKind": "INITIAL",
            "initialScorecardRawSha256": None,
            "initialJudgePanelSha256": None,
            "initialEvaluationRunManifestSha256": None,
            "requiredLanes": [],
        },
    }
    evaluation_run_path = directory / "evaluation-run.json"
    evaluation_run_sha, evaluation_run_raw_sha = write_self_hashed_json(
        evaluation_run_path,
        evaluation_run,
        "evaluationRunManifestSha256",
    )
    provenance["evaluationRunManifestSha256"] = evaluation_run_sha
    provenance["evaluationRunManifestRawSha256"] = evaluation_run_raw_sha

    candidate_path = directory / "candidate-provenance.json"
    write_json(candidate_path, candidate)
    return {
        "directory": directory,
        "coldSuffix": cold_suffix,
        "coverageSuffix": coverage_suffix,
        "candidate": candidate,
        "candidatePath": candidate_path,
        "judgments": judgments,
        "judgmentPaths": judgment_paths,
        "judgmentRawSha256": judgment_raw_shas,
        "verifier": verifier,
        "verifierPath": verifier_path,
        "ledger": ledger,
        "ledgerPath": ledger_path,
        "rubricPath": rubric_path,
        "panelSha256": judge_panel_sha,
        "candidateManifest": candidate_manifest,
        "candidateManifestPath": candidate_manifest_path,
        "qualificationReceipt": qualification,
        "qualificationReceiptPath": qualification_path,
        "actorObservations": actor_observations,
        "actorObservationPaths": actor_observation_paths,
        "coverageTrace": coverage_trace,
        "coverageTracePath": coverage_trace_path,
        "evidenceSet": evidence_set,
        "evidenceSetPath": evidence_set_path,
        "judgePanel": judge_panel,
        "judgePanelPath": judge_panel_path,
        "evaluationRun": evaluation_run,
        "evaluationRunPath": evaluation_run_path,
    }


def refresh_fixture_authorities(
    fixture: dict[str, Any],
    replacement_for: Path | None,
    *,
    retry_reason: str | None = None,
    retry_slots: tuple[str, ...] | None = None,
) -> None:
    judgment_attempts = [
        aggregator.read_json_attempt(
            path,
            f"fixture judgment {index}",
            slot_id=f"JUDGE-{index:02d}",
            capture_unreadable=True,
        )
        for index, path in enumerate(fixture["judgmentPaths"], start=1)
    ]
    expected_judgment_raw_shas = list(
        fixture["evaluationRun"]["artifacts"]["judgeJudgmentRawSha256"]
    )
    judgment_raw_shas = [
        row["rawSha256"]
        if row["rawSha256"] is not None
        else expected_judgment_raw_shas[index]
        for index, row in enumerate(judgment_attempts)
    ]
    panel = fixture["judgePanel"]
    for index, attempt in enumerate(judgment_attempts):
        panel["slots"][index]["judgmentRawSha256"] = judgment_raw_shas[index]
        value = attempt["value"]
        if isinstance(value, dict):
            if isinstance(value.get("judgeRunId"), str):
                panel["slots"][index]["judgeRunId"] = value["judgeRunId"]
            if isinstance(value.get("judgeInputSha256"), str):
                panel["slots"][index]["judgeInputSha256"] = value["judgeInputSha256"]

    initial: dict[str, Any] | None = None
    initial_bytes: bytes | None = None
    if replacement_for is None:
        panel.update(
            {
                "panelKind": "INITIAL",
                "changedLanes": ["COLD-JOURNEY", "COVERAGE-JOURNEY"],
                "replacementForPanelSha256": None,
                "replacementAuthoritySha256": None,
            }
        )
    else:
        initial_bytes = replacement_for.read_bytes()
        initial = json.loads(initial_bytes)
        fixture["candidate"]["scorecardId"] = initial["scorecardId"]
        panel.update(
            {
                "panelKind": "REPLACEMENT",
                "changedLanes": list(initial["replacementRequiredLanes"]),
                "replacementForPanelSha256": initial["judgePanelSha256"],
                "replacementAuthoritySha256": aggregator.bytes_sha256(initial_bytes),
            }
        )
    panel_sha, panel_raw_sha = write_self_hashed_json(
        fixture["judgePanelPath"],
        panel,
        "judgePanelSha256",
    )
    fixture["panelSha256"] = panel_sha

    verifier = fixture["verifier"]
    verifier["opaqueJudgePanelSha256"] = panel_sha
    verifier_raw_sha = write_json(fixture["verifierPath"], verifier)

    ledger = fixture["ledger"]
    ledger["verificationOutputSha256"] = verifier_raw_sha
    ledger_raw_sha = write_json(fixture["ledgerPath"], ledger)

    evaluation = fixture["evaluationRun"]
    evaluation["judgePanelSha256"] = panel_sha
    evaluation["artifacts"]["judgePanelRawSha256"] = panel_raw_sha
    evaluation["artifacts"]["judgeJudgmentRawSha256"] = judgment_raw_shas
    evaluation["artifacts"]["verificationOutputSha256"] = verifier_raw_sha
    evaluation["artifacts"]["oracleHardGateLedgerSha256"] = ledger_raw_sha
    if retry_reason is None:
        evaluation["retryLedger"] = []
    else:
        attempts_by_slot = {row["slotId"]: row for row in judgment_attempts}
        selected_slots = retry_slots
        if selected_slots is None:
            selected_slots = tuple(
                row["slotId"]
                for row in judgment_attempts
                if row["attemptOutcome"] != "VALID"
            )
            if not selected_slots:
                # Semantic judgment validation happens after this fixture refresh;
                # all current semantic mutations target judgment[0].
                selected_slots = ("JUDGE-01",)
        evaluation["retryLedger"] = []
        for slot in selected_slots:
            attempt = attempts_by_slot[slot]
            evaluation["retryLedger"].append(
                {
                    "runSlot": slot,
                    "role": "REPLACEMENT",
                    "reason": retry_reason,
                    "attempt": 1,
                    "outcome": "BLOCKED",
                    "readStatus": attempt["readStatus"],
                    "rawArtifactSha256": attempt["rawSha256"],
                }
            )
    if initial is None or initial_bytes is None:
        evaluation["replacementAuthority"] = {
            "panelKind": "INITIAL",
            "initialScorecardRawSha256": None,
            "initialJudgePanelSha256": None,
            "initialEvaluationRunManifestSha256": None,
            "requiredLanes": [],
        }
    else:
        evaluation["replacementAuthority"] = {
            "panelKind": "REPLACEMENT",
            "initialScorecardRawSha256": aggregator.bytes_sha256(initial_bytes),
            "initialJudgePanelSha256": initial["judgePanelSha256"],
            "initialEvaluationRunManifestSha256": initial["provenance"][
                "evaluationRunManifestSha256"
            ],
            "requiredLanes": list(initial["replacementRequiredLanes"]),
        }
    evaluation_sha, evaluation_raw_sha = write_self_hashed_json(
        fixture["evaluationRunPath"],
        evaluation,
        "evaluationRunManifestSha256",
    )

    provenance = fixture["candidate"]["provenance"]
    provenance["judgePanelSha256"] = panel_sha
    provenance["judgePanelRawSha256"] = panel_raw_sha
    provenance["evaluationRunManifestSha256"] = evaluation_sha
    provenance["evaluationRunManifestRawSha256"] = evaluation_raw_sha
    provenance["verificationOutputSha256"] = verifier_raw_sha
    provenance["oracleHardGateLedgerSha256"] = ledger_raw_sha
    write_json(fixture["candidatePath"], fixture["candidate"])
    fixture["judgmentRawSha256"] = judgment_raw_shas


_legacy_make_fixture = make_fixture
_legacy_refresh_fixture_authorities = refresh_fixture_authorities


def _make_synthetic_envelope(
    path: Path,
    schema_version: str,
    self_field: str,
    **fields: Any,
) -> tuple[dict[str, Any], str]:
    value = {
        "schemaVersion": schema_version,
        "protocol": aggregator.PROTOCOL,
        self_field: identity_sha(f"placeholder-{path.name}"),
        **fields,
    }
    _, raw_sha = write_self_hashed_json(path, value, self_field)
    return value, raw_sha


def _upgrade_synthetic_runtime_fixture(fixture: dict[str, Any]) -> None:
    """Add opaque producer inputs used only by legacy scoring-core tests.

    The P1 regressions below exercise each real validator directly.  Existing
    arithmetic/replacement tests keep their narrow historical fixtures and use
    test-scoped dependency injection; production aggregate_to_path has no bypass.
    """

    directory = fixture["directory"]
    candidate = fixture["candidate"]
    evaluation = fixture["evaluationRun"]
    artifacts = evaluation["artifacts"]

    cold_actor_response_paths: list[Path] = []
    cold_actor_responses: list[dict[str, Any]] = []
    cold_actor_response_raw: list[str] = []
    actor_observation_raw: list[str] = []
    for slot, observation in enumerate(fixture["actorObservations"]):
        response_path = directory / f"cold-actor-response-{slot + 1}.json"
        response = {
            "schemaVersion": aggregator.COLD_ACTOR_RESPONSE_SCHEMA,
            "protocol": aggregator.PROTOCOL,
            "coldActorResponseSha256": identity_sha(
                f"cold-response-placeholder-{slot}"
            ),
            "actorRunId": observation["actorRunId"],
            "actorSlot": observation["actorSlot"],
            "model": observation["model"],
            "reasoningEffort": observation["reasoningEffort"],
            "objective": observation["objective"],
            "firstUseRecords": [
                {
                    field: row[field]
                    for field in (
                        "firstUseOrdinal",
                        "probeId",
                        "currentGoal",
                        "expectedVisibleConsequence",
                        "citedVisibleSourceDescription",
                    )
                }
                for row in observation["firstUseRecords"]
            ],
            "approvalRecords": [
                {
                    field: row[field]
                    for field in (
                        "approvalOrdinal",
                        "predictionImmediatelyBeforeApproval",
                        "observedResult",
                        "causalAccount",
                    )
                }
                for row in observation["approvalRecords"]
            ],
            "incidents": [
                {
                    field: row[field]
                    for field in (
                        "incidentOrdinal",
                        "incidentType",
                        "confusionBoundary",
                        "severity",
                        "description",
                    )
                }
                for row in observation["incidents"]
            ],
            "terminalState": observation["terminalState"],
            "terminalIncidentOrdinal": observation["terminalIncidentOrdinal"],
        }
        response_self, response_raw = write_self_hashed_json(
            response_path,
            response,
            "coldActorResponseSha256",
        )
        observation["coldActorResponseSha256"] = response_self
        observation["coldActorResponseRawSha256"] = response_raw
        observation_raw = write_json(
            fixture["actorObservationPaths"][slot],
            observation,
        )
        cold_actor_response_paths.append(response_path)
        cold_actor_responses.append(response)
        cold_actor_response_raw.append(response_raw)
        actor_observation_raw.append(observation_raw)
        evaluation["terminalStates"][slot][
            "actorObservationRawSha256"
        ] = observation_raw
    artifacts["actorObservationRawSha256"] = actor_observation_raw
    artifacts["coldActorResponseSha256"] = [
        row["coldActorResponseSha256"] for row in cold_actor_responses
    ]
    artifacts["coldActorResponseRawSha256"] = cold_actor_response_raw

    registry_path = directory / "holdout-registry.json"
    registry_before_path = directory / "holdout-registry-before.json"
    write_json(registry_path, {})
    write_json(registry_before_path, {})
    receipt_path = directory / "holdout-consumption-receipt.json"
    receipt, receipt_raw = _make_synthetic_envelope(
        receipt_path,
        aggregator.HOLDOUT_CONSUMPTION_RECEIPT_SCHEMA,
        "holdoutConsumptionReceiptSha256",
        atomicClaim={"canonicalRegistryPath": str(registry_path.resolve())},
    )
    gold_path = directory / "gold-binding-manifest.json"
    gold, gold_raw = _make_synthetic_envelope(
        gold_path,
        aggregator.GOLD_BINDING_SCHEMA,
        "goldBindingManifestSha256",
        scoreBearingReady=True,
    )

    actor_trace_paths: list[Path] = []
    actor_traces: list[dict[str, Any]] = []
    actor_trace_raw: list[str] = []
    recording_paths: list[Path] = []
    recording_values: list[dict[str, Any]] = []
    recording_raw: list[str] = []
    for slot in range(3):
        recording_path = directory / f"actor-recording-{slot + 1}.json"
        recording, raw_sha = _make_synthetic_envelope(
            recording_path,
            aggregator.RECORDING_MANIFEST_SCHEMA,
            "recordingManifestSha256",
            sourceArtifactKind="ACTOR_OBSERVATION",
            sourceArtifactSha256=artifacts["actorObservationRawSha256"][slot],
        )
        recording_paths.append(recording_path)
        recording_values.append(recording)
        recording_raw.append(raw_sha)
        trace_path = directory / f"actor-trace-{slot + 1}.json"
        trace, trace_raw = _make_synthetic_envelope(
            trace_path,
            aggregator.ACTOR_TRACE_SCHEMA,
            "actorTraceSha256",
            actorCaptureSlot=slot,
            actorArtifactId=artifacts["actorArtifactIds"][slot],
            coldActorResponseSha256=cold_actor_responses[slot][
                "coldActorResponseSha256"
            ],
            coldActorResponseRawSha256=cold_actor_response_raw[slot],
            recordingManifestSha256=recording["recordingManifestSha256"],
            recordingManifestRawSha256=raw_sha,
        )
        actor_trace_paths.append(trace_path)
        actor_traces.append(trace)
        actor_trace_raw.append(trace_raw)
    coverage_action_ledger_path = directory / "coverage-action-ledger.json"
    coverage_action_ledger, coverage_action_ledger_raw = _make_synthetic_envelope(
        coverage_action_ledger_path,
        aggregator.COVERAGE_ACTION_LEDGER_SCHEMA,
        "coverageActionLedgerSha256",
    )
    coverage_recording_path = directory / "coverage-recording.json"
    coverage_recording, coverage_recording_raw = _make_synthetic_envelope(
        coverage_recording_path,
        aggregator.RECORDING_MANIFEST_SCHEMA,
        "recordingManifestSha256",
        sourceArtifactKind="COVERAGE_CAPTURE",
        sourceArtifactSha256=coverage_action_ledger_raw,
    )
    recording_paths.append(coverage_recording_path)
    recording_values.append(coverage_recording)
    recording_raw.append(coverage_recording_raw)

    coverage_trace = fixture["coverageTrace"]
    selected = selected_recipe(candidate["recipeId"])
    coverage_trace.update({
        "holdoutConsumptionReceiptSha256": receipt[
            "holdoutConsumptionReceiptSha256"
        ],
        "holdoutRealization": {
            "missionPrototypeBits": selected["missionPrototypeBits"],
            "promiseBranchOrder": selected["promiseBranchOrder"],
            "actorArtifactPermutation": selected["actorArtifactPermutation"],
            "coverageArtifactOrder": selected["coverageArtifactOrder"],
        },
        "coveragePresentationEpisodeIds": (
            list(EPISODE_IDS)
            if selected["coverageArtifactOrder"] == "EPISODE_ASCENDING"
            else list(reversed(EPISODE_IDS))
        ),
        "goldBindingManifestSha256": gold["goldBindingManifestSha256"],
        "coverageRunId": "fixture-coverage-run",
        "coverageActionLedgerSha256": coverage_action_ledger[
            "coverageActionLedgerSha256"
        ],
        "coverageActionLedgerRawSha256": coverage_action_ledger_raw,
        "coverageActionLedgerSchemaSha256": native_file_sha(
            "coverage-action-ledger.schema.json"
        ),
        "recordingManifestSha256": coverage_recording[
            "recordingManifestSha256"
        ],
        "recordingManifestRawSha256": coverage_recording_raw,
    })
    coverage_recipe = json.loads(
        (aggregator.NATIVE_DIRECTORY / "coverage-recipe.json").read_text()
    )
    actions_by_episode = {
        row["id"]: row["actions"] for row in coverage_recipe["episodes"]
    }
    for episode in coverage_trace["episodes"]:
        realized = aggregator._realized_coverage_actions(
            episode["episodeId"],
            actions_by_episode[episode["episodeId"]],
            selected,
        )
        for action_index, (row, occurrence_id) in enumerate(
            zip(episode["traceRows"], realized), start=1
        ):
            row["actionOccurrenceId"] = occurrence_id
            row["actionIndex"] = action_index
            row.update(aggregator._expected_action_realization(
                episode["episodeId"], occurrence_id, selected
            ))
    coverage_self, coverage_raw = write_self_hashed_json(
        fixture["coverageTracePath"],
        coverage_trace,
        "coverageArtifactId",
    )
    artifacts["coverageArtifactId"] = coverage_self
    artifacts["coverageTraceRawSha256"] = coverage_raw

    anonymization_path = directory / "anonymization-manifest.json"
    anonymization, anonymization_raw = _make_synthetic_envelope(
        anonymization_path,
        aggregator.ANONYMIZATION_MANIFEST_SCHEMA,
        "anonymizationManifestSha256",
    )
    fixture["evidenceSet"]["anonymizationManifestSha256"] = anonymization[
        "anonymizationManifestSha256"
    ]
    _, evidence_raw = write_self_hashed_json(
        fixture["evidenceSetPath"],
        fixture["evidenceSet"],
        "evidenceSetSha256",
    )
    evidence_sha = fixture["evidenceSet"]["evidenceSetSha256"]
    candidate["provenance"]["evidenceSetSha256"] = evidence_sha
    for judgment in fixture["judgments"]:
        judgment["evidenceSetSha256"] = evidence_sha
    for judgment, path in zip(fixture["judgments"], fixture["judgmentPaths"]):
        write_json(path, judgment)
    fixture["judgePanel"]["evidenceSetSha256"] = evidence_sha
    evaluation["artifacts"]["evidenceSetSha256"] = evidence_sha

    sanitized_bundle_path = directory / "sanitized-evidence-bundle-manifest.json"
    sanitized_bundle, sanitized_bundle_raw = _make_synthetic_envelope(
        sanitized_bundle_path,
        aggregator.SANITIZED_EVIDENCE_BUNDLE_MANIFEST_SCHEMA,
        "sanitizedEvidenceBundleManifestSha256",
        contentRootSha256=identity_sha("sanitized-evidence-content-root"),
    )

    candidate_judge_input_path = directory / "candidate-judge-input.json"
    candidate_judge_input, candidate_judge_input_raw = _make_synthetic_envelope(
        candidate_judge_input_path,
        aggregator.CANDIDATE_JUDGE_INPUT_SCHEMA,
        "judgeInputSha256",
        candidateManifestSha256=fixture["candidateManifest"][
            "candidateManifestSha256"
        ],
        candidateManifestRawSha256=candidate["provenance"][
            "candidateManifestRawSha256"
        ],
        qualificationReceiptSha256=fixture["qualificationReceipt"][
            "qualificationReceiptSha256"
        ],
        qualificationReceiptRawSha256=candidate["provenance"][
            "qualificationReceiptRawSha256"
        ],
        qualificationStatus=fixture["qualificationReceipt"]["status"],
        holdoutConsumptionReceiptSha256=receipt[
            "holdoutConsumptionReceiptSha256"
        ],
        holdoutConsumptionReceiptRawSha256=receipt_raw,
        goldBindingManifestSha256=gold["goldBindingManifestSha256"],
        goldBindingManifestRawSha256=gold_raw,
        evidenceSetSha256=evidence_sha,
        evidenceSetRawSha256=evidence_raw,
        sanitizedEvidenceBundleManifestSha256=sanitized_bundle[
            "sanitizedEvidenceBundleManifestSha256"
        ],
        sanitizedEvidenceBundleManifestRawSha256=sanitized_bundle_raw,
        sanitizedEvidenceContentRootSha256=sanitized_bundle[
            "contentRootSha256"
        ],
        recipeId=candidate["recipeId"],
        selectedRecipeSha256=fixture["candidateManifest"]["recipes"][
            "selectedRecipeSha256"
        ],
        artifactOrder=[
            row["anonymousArtifactId"] for row in candidate["artifactBindings"]
        ],
        promptTemplateSha256=candidate["provenance"]["promptTemplateSha256"],
        judgmentSchemaSha256=candidate["provenance"]["judgmentSchemaSha256"],
        rubricSha256=candidate["provenance"]["rubricSha256"],
        model=candidate["provenance"]["model"],
        reasoningEffort=candidate["provenance"]["reasoningEffort"],
    )
    for judgment in fixture["judgments"]:
        judgment["judgeInputSha256"] = candidate_judge_input["judgeInputSha256"]
    for judgment, path in zip(fixture["judgments"], fixture["judgmentPaths"]):
        write_json(path, judgment)
    for slot in fixture["judgePanel"]["slots"]:
        slot["judgeInputSha256"] = candidate_judge_input["judgeInputSha256"]

    verification_path = directory / "verification-input.json"
    observations = [
        {
            "observationId": observation_id,
            "claimType": "JUDGE_EVIDENCE",
            "incidentKey": None,
            "claim": f"Synthetic scoring-core claim {observation_id}.",
            "citedSources": [{
                "anonymousArtifactId": "ARTIFACT-A",
                "artifactId": "frame-fixture",
                "locator": "fixture/frame-fixture.png",
            }],
        }
        for observation_id in candidate["expectedObservationIds"]
    ]
    for incident in fixture["ledger"]["incidents"]:
        observation_id = f"OBS-{len(observations) + 1:04d}"
        incident["verifierObservationId"] = observation_id
        observations.append({
            "observationId": observation_id,
            "claimType": "ACTOR_INCIDENT",
            "incidentKey": incident["incidentKey"],
            "claim": f"Synthetic actor incident {incident['incidentKey']}.",
            "citedSources": [{
                "anonymousArtifactId": incident["actorArtifactIds"][0],
                "artifactId": "frame-fixture",
                "locator": "fixture/frame-fixture.png",
            }],
        })
    candidate["expectedObservationIds"] = [
        row["observationId"] for row in observations
    ]
    verification, verification_raw = _make_synthetic_envelope(
        verification_path,
        aggregator.VERIFICATION_INPUT_SCHEMA,
        "verificationInputSha256",
        observations=observations,
    )
    candidate["verificationInputSha256"] = verification["verificationInputSha256"]
    fixture["verifier"]["verificationInputSha256"] = verification[
        "verificationInputSha256"
    ]
    fixture["verifier"]["evidenceSetSha256"] = evidence_sha
    existing_verdicts = {
        row["observationId"]: row["verdict"]
        for row in fixture["verifier"]["observations"]
    }
    incident_verdicts = {
        row["incidentKey"]: row["verifierStatus"]
        for row in fixture["ledger"]["incidents"]
    }
    fixture["verifier"]["observations"] = [
        {
            "observationId": row["observationId"],
            "claimType": row["claimType"],
            "incidentKey": row["incidentKey"],
            "verdict": (
                incident_verdicts[row["incidentKey"]]
            ) if row["claimType"] == "ACTOR_INCIDENT" else existing_verdicts.get(
                row["observationId"], "SUPPORTED"
            ),
            "citedSources": copy.deepcopy(row["citedSources"]),
            "rationale": "The cited fixture directly supports the observation.",
        }
        for row in observations
        if (
            row["claimType"] == "ACTOR_INCIDENT"
            or row["observationId"] in existing_verdicts
        )
    ]

    story_path = directory / "story-manifest.json"
    write_json(story_path, {"fixture": True})
    evaluation_session_claim_path = directory / "evaluation-session-claim.json"
    write_json(evaluation_session_claim_path, {"fixture": True})

    evaluation["goldBindings"] = {
        "bindingManifestSha256": gold["goldBindingManifestSha256"],
        "prefixCount": 12,
        "checkpointCount": 49,
        "applicableBindingCount": 56,
        "boundBindingCount": 56,
        "notApplicableBindingCount": 5,
        "allApplicableBindingsExact": True,
        "e09NorthBankTwoProcessWitness": True,
        "bindingRequired": True,
        "derivedReady": True,
    }
    artifacts.update({
        "actorTraceSha256": [row["actorTraceSha256"] for row in actor_traces],
        "actorTraceRawSha256": actor_trace_raw,
        "recordingManifestSha256": [
            row["recordingManifestSha256"] for row in recording_values[:3]
        ],
        "recordingManifestRawSha256": recording_raw[:3],
        "coverageRecordingManifestSha256": coverage_recording[
            "recordingManifestSha256"
        ],
        "coverageRecordingManifestRawSha256": coverage_recording_raw,
        "coverageActionLedgerSha256": coverage_action_ledger[
            "coverageActionLedgerSha256"
        ],
        "coverageActionLedgerRawSha256": coverage_action_ledger_raw,
        "anonymizationManifestSha256": anonymization[
            "anonymizationManifestSha256"
        ],
        "anonymizationManifestRawSha256": anonymization_raw,
        "evidenceSetRawSha256": evidence_raw,
        "sanitizedEvidenceBundleManifestSha256": sanitized_bundle[
            "sanitizedEvidenceBundleManifestSha256"
        ],
        "sanitizedEvidenceBundleManifestRawSha256": sanitized_bundle_raw,
        "sanitizedEvidenceContentRootSha256": sanitized_bundle[
            "contentRootSha256"
        ],
        "verificationInputSha256": verification["verificationInputSha256"],
        "verificationInputRawSha256": verification_raw,
        "candidateJudgeInputSha256": candidate_judge_input["judgeInputSha256"],
        "candidateJudgeInputRawSha256": candidate_judge_input_raw,
        "goldBindingManifestSha256": gold["goldBindingManifestSha256"],
        "goldBindingManifestRawSha256": gold_raw,
        "holdoutConsumptionReceiptSha256": receipt[
            "holdoutConsumptionReceiptSha256"
        ],
        "holdoutConsumptionReceiptRawSha256": receipt_raw,
    })

    candidate["provenance"].update({
        "coldActorResponseSha256": [
            row["coldActorResponseSha256"] for row in cold_actor_responses
        ],
        "coldActorResponseRawSha256": cold_actor_response_raw,
        "holdoutConsumptionReceiptSha256": receipt[
            "holdoutConsumptionReceiptSha256"
        ],
        "holdoutConsumptionReceiptRawSha256": receipt_raw,
        "goldBindingManifestSha256": gold["goldBindingManifestSha256"],
        "goldBindingManifestRawSha256": gold_raw,
        "anonymizationManifestSha256": anonymization[
            "anonymizationManifestSha256"
        ],
        "anonymizationManifestRawSha256": anonymization_raw,
        "evidenceSetSha256": evidence_sha,
        "evidenceSetRawSha256": evidence_raw,
        "sanitizedEvidenceBundleManifestSha256": sanitized_bundle[
            "sanitizedEvidenceBundleManifestSha256"
        ],
        "sanitizedEvidenceBundleManifestRawSha256": sanitized_bundle_raw,
        "sanitizedEvidenceContentRootSha256": sanitized_bundle[
            "contentRootSha256"
        ],
        "coverageActionLedgerSha256": coverage_action_ledger[
            "coverageActionLedgerSha256"
        ],
        "coverageActionLedgerRawSha256": coverage_action_ledger_raw,
        "verificationInputRawSha256": verification_raw,
        "candidateJudgeInputSha256": candidate_judge_input["judgeInputSha256"],
        "candidateJudgeInputRawSha256": candidate_judge_input_raw,
    })

    ledger = fixture["ledger"]
    ledger.update({
        "holdoutConsumptionReceiptSha256": receipt[
            "holdoutConsumptionReceiptSha256"
        ],
        "goldBindingManifestSha256": gold["goldBindingManifestSha256"],
        "coldActorResponseSha256": [
            row["coldActorResponseSha256"] for row in cold_actor_responses
        ],
        "coldActorResponseRawSha256": cold_actor_response_raw,
        "actorTraceSha256": [row["actorTraceSha256"] for row in actor_traces],
        "coverageActionLedgerSha256": coverage_action_ledger[
            "coverageActionLedgerSha256"
        ],
        "coverageArtifactId": coverage_trace["coverageArtifactId"],
        "recordingManifestSha256": [
            *[row["recordingManifestSha256"] for row in recording_values[:3]],
            coverage_recording["recordingManifestSha256"],
        ],
        "anonymizationManifestSha256": anonymization[
            "anonymizationManifestSha256"
        ],
        "verificationInputSha256": verification["verificationInputSha256"],
        "evidenceSetSha256": evidence_sha,
        "sanitizedEvidenceBundleManifestSha256": sanitized_bundle[
            "sanitizedEvidenceBundleManifestSha256"
        ],
        "sanitizedEvidenceContentRootSha256": sanitized_bundle[
            "contentRootSha256"
        ],
        "candidateJudgeInputSha256": candidate_judge_input["judgeInputSha256"],
        "contractBindingsSha256": native_file_sha("contract-bindings.json"),
        "canonicalHashPolicySha256": native_file_sha("canonical-hash-policy.json"),
        "goldStateContractSha256": native_file_sha("gold-state-manifest.json"),
        "coverageRecipeSha256": native_file_sha("coverage-recipe.json"),
        "conceptManifestSha256": native_file_sha(
            "concept-exposure-manifest.json"
        ),
        "nativeAggregatorSha256": aggregator.file_sha256(
            AGGREGATOR_PATH,
            "native aggregator",
        ),
        "contractValidatorSha256": native_file_sha("validate-contract.py"),
        "goldValidatorSha256": native_file_sha("validate-gold-state.py"),
    })

    fixture.update({
        "actorTraces": actor_traces,
        "actorTracePaths": actor_trace_paths,
        "recordingManifests": recording_values,
        "recordingManifestPaths": recording_paths,
        "verificationInput": verification,
        "verificationInputPath": verification_path,
        "goldBinding": gold,
        "goldBindingPath": gold_path,
        "holdoutReceipt": receipt,
        "holdoutReceiptPath": receipt_path,
        "registryBeforePath": registry_before_path,
        "registryAfterPath": registry_path,
        "anonymizationManifest": anonymization,
        "anonymizationManifestPath": anonymization_path,
        "coverageActionLedger": coverage_action_ledger,
        "coverageActionLedgerPath": coverage_action_ledger_path,
        "sanitizedEvidenceBundleManifest": sanitized_bundle,
        "sanitizedEvidenceBundleManifestPath": sanitized_bundle_path,
        "candidateJudgeInput": candidate_judge_input,
        "candidateJudgeInputPath": candidate_judge_input_path,
        "storyManifestPath": story_path,
        "coldActorResponses": cold_actor_responses,
        "coldActorResponsePaths": cold_actor_response_paths,
        "evaluationSessionClaimPath": evaluation_session_claim_path,
    })


def make_fixture(*args: Any, **kwargs: Any) -> dict[str, Any]:
    fixture = _legacy_make_fixture(*args, **kwargs)
    _upgrade_synthetic_runtime_fixture(fixture)
    refresh_fixture_authorities(fixture, None)
    return fixture


def refresh_fixture_authorities(
    fixture: dict[str, Any],
    replacement_for: Path | None,
    *,
    retry_reason: str | None = None,
    retry_slots: tuple[str, ...] | None = None,
) -> None:
    _legacy_refresh_fixture_authorities(
        fixture,
        replacement_for,
        retry_reason=retry_reason,
        retry_slots=retry_slots,
    )
    candidate = fixture["candidate"]
    provenance = candidate["provenance"]
    session_suffix = (
        f"{fixture['coldSuffix']}-{fixture['coverageSuffix']}-"
        f"{'replacement' if replacement_for is not None else 'initial'}"
    )
    provenance.update({
        "evaluationSessionClaimSha256": identity_sha(
            f"evaluation-session-claim-{session_suffix}"
        ),
        "evaluationSessionClaimRawSha256": identity_sha(
            f"evaluation-session-claim-raw-{session_suffix}"
        ),
        "evaluationSessionId": identity_sha(
            f"evaluation-session-id-{session_suffix}"
        ),
        "evaluationSessionMode": (
            "REPLACEMENT" if replacement_for is not None else "INITIAL"
        ),
        "evaluationAttemptAuditSha256": identity_sha(
            f"evaluation-attempt-audit-{session_suffix}"
        ),
        "evaluationSelectedAttemptsSha256": identity_sha(
            f"evaluation-selected-attempts-{session_suffix}"
        ),
    })
    if replacement_for is not None:
        initial = json.loads(replacement_for.read_text())
        for field in (
            "holdoutConsumptionReceiptSha256",
            "holdoutConsumptionReceiptRawSha256",
            "goldBindingManifestSha256",
            "goldBindingManifestRawSha256",
        ):
            provenance[field] = initial["provenance"][field]
    verification = fixture["verificationInput"]
    if [row["observationId"] for row in verification["observations"]] != candidate[
        "expectedObservationIds"
    ]:
        raise AssertionError("synthetic verification observations drifted from candidate")
    verification_self, verification_raw = write_self_hashed_json(
        fixture["verificationInputPath"],
        verification,
        "verificationInputSha256",
    )
    candidate["verificationInputSha256"] = verification_self
    verifier = fixture["verifier"]
    verifier["verificationInputSha256"] = verification_self
    verifier_raw = write_json(fixture["verifierPath"], verifier)
    provenance["verificationOutputSha256"] = verifier_raw
    ledger = fixture["ledger"]
    ledger["verificationInputSha256"] = verification_self
    ledger["verificationOutputSha256"] = verifier_raw
    ledger["contractBindingsSha256"] = native_file_sha("contract-bindings.json")
    ledger_raw = write_json(fixture["ledgerPath"], ledger)
    provenance["oracleHardGateLedgerSha256"] = ledger_raw
    evaluation = fixture["evaluationRun"]
    evaluation["artifacts"].update({
        "verificationInputSha256": verification_self,
        "verificationInputRawSha256": verification_raw,
        "verificationOutputSha256": verifier_raw,
        "oracleHardGateLedgerSha256": ledger_raw,
    })
    evaluation_self, evaluation_raw = write_self_hashed_json(
        fixture["evaluationRunPath"],
        evaluation,
        "evaluationRunManifestSha256",
    )
    provenance["evaluationRunManifestSha256"] = evaluation_self
    provenance["evaluationRunManifestRawSha256"] = evaluation_raw
    write_json(fixture["candidatePath"], candidate)


def _synthetic_full_authority_patches(fixture: dict[str, Any], *, patch_preflight: bool):
    stack = contextlib.ExitStack()
    real_envelope = aggregator.validate_self_hashed_envelope
    directory = fixture["directory"]
    shared_names = ("initial", "replacement", "corrected")
    authority_parent = (
        directory.parent
        if directory.name.startswith(shared_names)
        else directory
    )
    synthetic_authority_root = (
        authority_parent / ".commercial-ux-authority"
    ).resolve(strict=False)

    def envelope(value: Any, raw_bytes: bytes, **kwargs: Any) -> dict[str, Any]:
        label = kwargs.get("label", "")
        if label.startswith((
            "candidate gold binding", "native evidence verification input",
            "actor trace", "recording manifest", "coverage action ledger",
            "anonymization manifest", "evidence set",
            "sanitized evidence bundle manifest",
            "candidate judge input",
        )):
            field = kwargs["self_field"]
            return {
                "value": value,
                "selfSha256": value[field],
                "rawSha256": aggregator.bytes_sha256(raw_bytes),
            }
        return real_envelope(value, raw_bytes, **kwargs)

    def holdout(value: Any, raw_bytes: bytes, *_args: Any) -> dict[str, Any]:
        return {
            "value": value,
            "selfSha256": value["holdoutConsumptionReceiptSha256"],
            "rawSha256": aggregator.bytes_sha256(raw_bytes),
        }

    def gold(
        value: Any,
        raw_bytes: bytes,
        *_args: Any,
        **_kwargs: Any,
    ) -> dict[str, Any]:
        return {
            "value": value,
            "selfSha256": value["goldBindingManifestSha256"],
            "rawSha256": aggregator.bytes_sha256(raw_bytes),
            "derivedReady": True,
        }

    def evaluation_session(*_args: Any, **_kwargs: Any) -> dict[str, Any]:
        provenance = fixture["candidate"]["provenance"]
        fixed_artifact_paths = {
            "goldBinding": fixture["goldBindingPath"],
            "anonymization": fixture["anonymizationManifestPath"],
            "evidenceSet": fixture["evidenceSetPath"],
            "sanitizedEvidenceBundle": fixture[
                "sanitizedEvidenceBundleManifestPath"
            ],
            "candidateJudgeInput": fixture["candidateJudgeInputPath"],
            "judgePanel": fixture["judgePanelPath"],
            "verificationInput": fixture["verificationInputPath"],
            "evaluationRun": fixture["evaluationRunPath"],
            "aggregationInput": fixture["candidatePath"],
            "scorecard": fixture["directory"] / "scorecard.json",
            "panelFinalizationSeal": (
                fixture["directory"] / "panel-finalization-seal.json"
            ),
        }
        return {
            "claim": {
                "fixedArtifactPaths": {
                    key: str(Path(path).resolve(strict=False))
                    for key, path in fixed_artifact_paths.items()
                },
            },
            "claimPath": fixture["evaluationSessionClaimPath"].resolve(
                strict=False
            ),
            "claimRawBytes": fixture["evaluationSessionClaimPath"].read_bytes(),
            "initialClaimPath": None,
            "initialClaimRawBytes": None,
            "attemptAuditRows": [],
            "selectedRows": [],
            "selectedBySlot": {},
            "provenance": {
                field: copy.deepcopy(provenance[field])
                for field in (
                    "evaluationSessionClaimSha256",
                    "evaluationSessionClaimRawSha256",
                    "evaluationSessionPolicySha256",
                    "evaluationSessionClaimToolSha256",
                    "evaluationSessionId",
                    "evaluationSessionMode",
                    "evaluationAttemptAuditSha256",
                    "evaluationSelectedAttemptsSha256",
                )
            },
        }

    def actor_traces(_inputs: Any, actor_rows: Any, *_args: Any) -> list[dict[str, Any]]:
        return [
            {
                "value": fixture["actorTraces"][slot],
                "selfSha256": fixture["actorTraces"][slot]["actorTraceSha256"],
                "rawSha256": aggregator.file_sha256(
                    fixture["actorTracePaths"][slot], "synthetic actor trace"
                ),
                "slot": slot,
                "actor": actor_rows[slot],
            }
            for slot in range(3)
        ]

    def coverage(value: Any, raw_bytes: bytes, *_args: Any) -> dict[str, Any]:
        return {
            "value": value,
            "selfSha256": value["coverageArtifactId"],
            "rawSha256": aggregator.bytes_sha256(raw_bytes),
        }

    def anonymization(value: Any, raw_bytes: bytes, *_args: Any) -> dict[str, Any]:
        return {
            "value": value,
            "selfSha256": value["anonymizationManifestSha256"],
            "rawSha256": aggregator.bytes_sha256(raw_bytes),
        }

    def evidence(value: Any, raw_bytes: bytes, candidate: Any, *_args: Any) -> dict[str, Any]:
        actor_rows = _args[2]
        return {
            "value": value,
            "selfSha256": value["evidenceSetSha256"],
            "rawSha256": aggregator.bytes_sha256(raw_bytes),
            "artifactsById": {
                row["anonymousArtifactId"]: row for row in value["artifacts"]
            },
            "actorAuthoritiesByAnonymousId": {
                f"ARTIFACT-{chr(ord('A') + slot)}": actor_rows[slot]
                for slot in range(3)
            },
        }

    def verification(value: Any, raw_bytes: bytes, *_args: Any) -> dict[str, Any]:
        return {
            "value": value,
            "selfSha256": value["verificationInputSha256"],
            "rawSha256": aggregator.bytes_sha256(raw_bytes),
            "observations": value["observations"],
        }

    def evaluation(value: Any, raw_bytes: bytes, *_args: Any, **_kwargs: Any) -> dict[str, Any]:
        return {
            "value": value,
            "selfSha256": value["evaluationRunManifestSha256"],
            "rawSha256": aggregator.bytes_sha256(raw_bytes),
        }

    def coverage_ledger(envelope: Any, *_args: Any) -> dict[str, Any]:
        return envelope

    def sanitized(envelope: Any, *_args: Any) -> dict[str, Any]:
        return envelope

    def candidate_judge_input(envelope: Any, *_args: Any) -> dict[str, Any]:
        return envelope

    def recordings(*_args: Any) -> dict[str, Any]:
        gate = next(
            row
            for row in fixture["ledger"]["hardGates"]
            if row["gateId"] == "HG09-AUDIO"
        )
        return {
            "audioSync": {
                field: copy.deepcopy(gate[field])
                for field in (
                    "status",
                    "failureCode",
                    "inputHashes",
                    "evidenceRefs",
                    "observed",
                )
            }
        }

    def lane_execution_identities(*_args: Any) -> dict[str, Any]:
        artifacts = fixture["evaluationRun"]["artifacts"]
        cold_suffix = fixture["coldSuffix"]
        coverage_suffix = fixture["coverageSuffix"]
        return {
            "cold": [
                {
                    "actorCaptureSlot": slot,
                    "actorRunId": artifacts["actorRunIds"][slot],
                    "processTreeId": f"actor-process-{slot}-{cold_suffix}",
                    "userDataSha256": artifacts["userDataSha256"][slot],
                    "saveSha256": artifacts["saveSha256"][slot],
                    "journalSha256": artifacts["journalSha256"][slot],
                    "recordingManifestSha256": artifacts[
                        "recordingManifestSha256"
                    ][slot],
                    "recordingManifestRawSha256": artifacts[
                        "recordingManifestRawSha256"
                    ][slot],
                    "recordingContentRootSha256": identity_sha(
                        f"recording-content-{slot}-{cold_suffix}"
                    ),
                    "canonicalRecordingRoot": (
                        f"/synthetic/actor-{slot}-{cold_suffix}"
                    ),
                }
                for slot in range(3)
            ],
            "coverage": {
                "coverageRunId": f"coverage-run-{coverage_suffix}",
                "processTreeId": f"coverage-process-{coverage_suffix}",
                "userDataSha256": identity_sha(
                    f"coverage-user-data-{coverage_suffix}"
                ),
                "journalBundleSha256": identity_sha(
                    f"coverage-journal-{coverage_suffix}"
                ),
                "recordingManifestSha256": artifacts[
                    "coverageRecordingManifestSha256"
                ],
                "recordingManifestRawSha256": artifacts[
                    "coverageRecordingManifestRawSha256"
                ],
                "recordingContentRootSha256": identity_sha(
                    f"coverage-recording-content-{coverage_suffix}"
                ),
                "canonicalRecordingRoot": (
                    f"/synthetic/coverage-{coverage_suffix}"
                ),
            },
        }

    stack.enter_context(mock.patch.object(aggregator, "validate_self_hashed_envelope", side_effect=envelope))
    stack.enter_context(mock.patch.object(
        aggregator,
        "_commercial_ux_authority_root",
        return_value=synthetic_authority_root,
    ))
    stack.enter_context(mock.patch.object(aggregator, "validate_candidate_authority_hashes"))
    stack.enter_context(mock.patch.object(aggregator, "validate_candidate_execution_authority"))
    stack.enter_context(mock.patch.object(aggregator, "validate_runtime_contract_authority"))
    stack.enter_context(mock.patch.object(
        aggregator,
        "validate_evaluation_session_authority",
        side_effect=evaluation_session,
    ))
    stack.enter_context(mock.patch.object(
        aggregator,
        "validate_evaluation_session_candidate_provenance",
    ))
    stack.enter_context(mock.patch.object(
        aggregator,
        "validate_evaluation_session_fixed_artifacts",
    ))
    stack.enter_context(mock.patch.object(
        aggregator,
        "validate_evaluation_session_primary_outputs",
    ))
    stack.enter_context(mock.patch.object(
        aggregator,
        "validate_evaluation_session_supporting_artifacts",
    ))
    stack.enter_context(mock.patch.object(
        aggregator,
        "_validate_cold_terminal_checkpoint_sequence",
    ))
    if patch_preflight:
        stack.enter_context(mock.patch.object(aggregator, "validate_official_score_bearing_preflight"))
    stack.enter_context(mock.patch.object(aggregator, "validate_holdout_consumption_authority", side_effect=holdout))
    stack.enter_context(mock.patch.object(aggregator, "validate_gold_binding_authority", side_effect=gold))
    stack.enter_context(mock.patch.object(aggregator, "validate_evaluation_run_authority", side_effect=evaluation))
    stack.enter_context(mock.patch.object(aggregator, "validate_required_cold_probes"))
    stack.enter_context(mock.patch.object(aggregator, "validate_actor_trace_authorities", side_effect=actor_traces))
    stack.enter_context(mock.patch.object(aggregator, "validate_coverage_trace_authority", side_effect=coverage))
    stack.enter_context(mock.patch.object(aggregator, "validate_coverage_action_ledger_authority", side_effect=coverage_ledger))
    stack.enter_context(mock.patch.object(
        aggregator,
        "validate_recording_manifest_authorities",
        side_effect=recordings,
    ))
    stack.enter_context(mock.patch.object(
        aggregator,
        "derive_lane_execution_identities",
        side_effect=lane_execution_identities,
    ))
    stack.enter_context(mock.patch.object(aggregator, "validate_anonymization_authority", side_effect=anonymization))
    stack.enter_context(mock.patch.object(aggregator, "validate_evidence_set_authority", side_effect=evidence))
    stack.enter_context(mock.patch.object(aggregator, "validate_verification_input_authority", side_effect=verification))
    stack.enter_context(mock.patch.object(aggregator, "validate_sanitized_evidence_bundle_authority", side_effect=sanitized))
    stack.enter_context(mock.patch.object(aggregator, "validate_candidate_judge_input_authority", side_effect=candidate_judge_input))
    return stack


def aggregate_fixture(
    fixture: dict[str, Any],
    *,
    output_name: str = "scorecard.json",
    replacement_for: Path | None = None,
    retry_reason: str | None = None,
    retry_slots: tuple[str, ...] | None = None,
    refresh_authorities: bool = True,
    patch_preflight: bool = True,
) -> dict[str, Any]:
    if refresh_authorities:
        refresh_fixture_authorities(
            fixture,
            replacement_for,
            retry_reason=retry_reason,
            retry_slots=retry_slots,
        )
    with _synthetic_full_authority_patches(
        fixture,
        patch_preflight=patch_preflight,
    ):
        return aggregator.aggregate_to_path(
            fixture["judgmentPaths"],
            fixture["verifierPath"],
            fixture["ledgerPath"],
            fixture["candidatePath"],
            fixture["candidateManifestPath"],
            fixture["qualificationReceiptPath"],
            fixture["judgePanelPath"],
            fixture["evaluationRunPath"],
            fixture["actorObservationPaths"],
            fixture["coverageTracePath"],
            fixture["evidenceSetPath"],
            fixture["rubricPath"],
            fixture["directory"] / output_name,
            replacement_for,
            verification_input_path=fixture["verificationInputPath"],
            cold_actor_response_paths=fixture["coldActorResponsePaths"],
            actor_trace_paths=fixture["actorTracePaths"],
            gold_binding_path=fixture["goldBindingPath"],
            holdout_consumption_receipt_path=fixture["holdoutReceiptPath"],
            holdout_registry_before_path=fixture["registryBeforePath"],
            holdout_registry_after_path=fixture["registryAfterPath"],
            anonymization_manifest_path=fixture["anonymizationManifestPath"],
            story_manifest_path=fixture["storyManifestPath"],
            recording_manifest_paths=fixture["recordingManifestPaths"],
            coverage_action_ledger_path=fixture["coverageActionLedgerPath"],
            sanitized_evidence_bundle_manifest_path=(
                fixture["sanitizedEvidenceBundleManifestPath"]
            ),
            candidate_judge_input_path=fixture["candidateJudgeInputPath"],
            evaluation_session_claim_path=fixture["evaluationSessionClaimPath"],
        )


class _SessionContractCommonRootProxy:
    """Keep production validation semantics while relocating git-common test state."""

    def __init__(self, validator: Any, common_root: Path):
        self._validator = validator
        self._common_root = common_root

    def __getattr__(self, name: str) -> Any:
        return getattr(self._validator, name)

    def validate_evaluation_session_claim_semantics(
        self,
        *args: Any,
        **kwargs: Any,
    ) -> None:
        kwargs["common_dir_override"] = self._common_root
        self._validator.validate_evaluation_session_claim_semantics(
            *args,
            **kwargs,
        )


class _SessionToolCommonRootProxy:
    """Inject only the test common-root override into the checked-in claim tool."""

    def __init__(self, session_tool: Any, common_root: Path):
        self._session_tool = session_tool
        self._common_root = common_root

    def __getattr__(self, name: str) -> Any:
        return getattr(self._session_tool, name)

    def read_and_validate_claim(self, claim_path: Path, *, native: Path):
        validator, resolved, raw_bytes, claim = (
            self._session_tool.read_and_validate_claim(
                claim_path,
                native=native,
                common_dir_override=self._common_root,
            )
        )
        return (
            _SessionContractCommonRootProxy(validator, self._common_root),
            resolved,
            raw_bytes,
            claim,
        )


def make_production_session_authority_fixture(
    common_root: Path,
    *,
    tag: str,
    terminal_failure_before_success: bool = False,
) -> dict[str, Any]:
    """Create a real nine-slot session using the checked-in reserve/finalize tool."""

    session_tool = aggregator._load_exact_validator(
        aggregator.SESSION_CLAIM_TOOL_PATH,
        f"gridworks_session_authority_fixture_{tag}",
    )
    native = aggregator.NATIVE_DIRECTORY
    policy_bytes = (native / "evaluation-session-policy.json").read_bytes()
    policy = json.loads(policy_bytes)
    policy_sha = session_tool.sha256_bytes(policy_bytes)
    receipt_sha = identity_sha(f"session-authority-receipt-{tag}")
    receipt_root = (
        common_root
        / "gridworks-commercial-ux"
        / "evaluation-sessions"
        / receipt_sha.removeprefix("sha256:")
    )
    session_root = receipt_root / "initial"
    claim_path = receipt_root / "initial-claim.json"
    source_commit = identity_sha(
        f"session-authority-commit-{tag}"
    ).removeprefix("sha256:")[:40]
    candidate = {
        "candidateId": f"candidate-session-authority-{tag}",
        "candidateManifestSha256": identity_sha(
            f"session-authority-candidate-{tag}"
        ),
        "source": {"commit": source_commit},
        "authorityHashes": {
            "world": identity_sha(f"session-authority-world-{tag}"),
        },
        "execution": {
            "executionArtifactSha256": identity_sha(
                f"session-authority-execution-{tag}"
            ),
        },
    }
    candidate_raw = (
        json.dumps(candidate, ensure_ascii=False, indent=2, sort_keys=True).encode(
            "utf-8"
        )
        + b"\n"
    )
    playable_fingerprint = session_tool.playable_fingerprint(candidate)
    receipt = {
        "holdoutConsumptionReceiptSha256": receipt_sha,
        "candidatePlayableFingerprintSha256": playable_fingerprint,
        "sourceCommit": source_commit,
        "evaluationPhase": "OFFICIAL_HOLDOUT",
        "officialCommercialUX": True,
        "selectedRecipe": {
            "recipeId": "HOLDOUT-01",
            "selectedRecipeSha256": identity_sha(
                f"session-authority-recipe-{tag}"
            ),
        },
    }
    receipt_raw = (
        json.dumps(receipt, ensure_ascii=False, indent=2, sort_keys=True).encode(
            "utf-8"
        )
        + b"\n"
    )
    fixed_paths = {
        key: str(session_root / "artifacts" / filename)
        for key, filename in policy["fixedArtifactNames"].items()
    }
    claim = {
        "schemaVersion": "gridworks.commercial-ux.evaluation-session-claim.v1",
        "protocol": session_tool.PROTOCOL,
        "evaluationSessionClaimSha256": "sha256:" + "0" * 64,
        "evaluationSessionClaimSchemaSha256": session_tool.sha256_bytes(
            (native / "evaluation-session-claim.schema.json").read_bytes()
        ),
        "evaluationSessionPolicySha256": policy_sha,
        "sessionClaimToolSha256": session_tool.sha256_bytes(
            aggregator.SESSION_CLAIM_TOOL_PATH.read_bytes()
        ),
        "policyId": policy["policyId"],
        "sessionId": session_tool.session_id(
            receipt_sha,
            "INITIAL",
            None,
            policy_sha,
        ),
        "sessionMode": "INITIAL",
        "evaluationPhase": "OFFICIAL_HOLDOUT",
        "officialCommercialUX": True,
        "candidateId": candidate["candidateId"],
        "candidateManifestSha256": candidate["candidateManifestSha256"],
        "candidateManifestRawSha256": session_tool.sha256_bytes(candidate_raw),
        "sourceCommit": source_commit,
        "candidatePlayableFingerprintSha256": playable_fingerprint,
        "executionArtifactSha256": candidate["execution"][
            "executionArtifactSha256"
        ],
        "authorityHashesSha256": session_tool.projection_sha256(
            candidate["authorityHashes"]
        ),
        "holdoutConsumptionReceiptSha256": receipt_sha,
        "holdoutConsumptionReceiptRawSha256": session_tool.sha256_bytes(
            receipt_raw
        ),
        "selectedRecipeId": "HOLDOUT-01",
        "selectedRecipeSha256": receipt["selectedRecipe"][
            "selectedRecipeSha256"
        ],
        "canonicalSessionRoot": str(session_root),
        "canonicalClaimPath": str(claim_path),
        "initialSession": None,
        "replacementClaimPath": str(receipt_root / "replacement-01-claim.json"),
        "sessionLockPath": str(session_root / "session.lock"),
        "requiredFreshSlotIds": session_tool.required_fresh_slot_ids(
            "INITIAL", None, policy
        ),
        "slots": session_tool.build_slots(session_root, policy),
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
    claim["evaluationSessionClaimSha256"] = session_tool.self_hash(
        claim,
        "evaluationSessionClaimSha256",
    )
    write_json(claim_path, claim)
    session_root.mkdir()
    (session_root / "artifacts").mkdir()

    product_fixture = make_fixture(
        common_root / f"role-outputs-{tag}",
        panel_suffix=f"session-authority-{tag}",
    )
    outputs = {
        "SLOT-01": product_fixture["coldActorResponsePaths"][0].read_bytes(),
        "SLOT-02": product_fixture["coldActorResponsePaths"][1].read_bytes(),
        "SLOT-03": product_fixture["coldActorResponsePaths"][2].read_bytes(),
        "SLOT-04": product_fixture["coverageTracePath"].read_bytes(),
        "SLOT-05": product_fixture["judgmentPaths"][0].read_bytes(),
        "SLOT-06": product_fixture["judgmentPaths"][1].read_bytes(),
        "SLOT-07": product_fixture["judgmentPaths"][2].read_bytes(),
        "SLOT-08": product_fixture["verifierPath"].read_bytes(),
        "SLOT-09": product_fixture["ledgerPath"].read_bytes(),
    }

    if terminal_failure_before_success:
        session_tool.reserve_attempt(
            native=native,
            claim_path=claim_path,
            slot_id="SLOT-01",
            attempt_ordinal=1,
            common_dir_override=common_root,
        )
        first_attempt = claim["slots"][0]["attempts"][0]
        Path(first_attempt["outputPath"]).write_bytes(b"{malformed")
        _, first_terminal = session_tool.finalize_attempt(
            native=native,
            claim_path=claim_path,
            slot_id="SLOT-01",
            attempt_ordinal=1,
            common_dir_override=common_root,
        )
        if first_terminal["outcome"] != "TRANSPORT_FAILURE":
            raise AssertionError("fixture failure attempt was not transport-derived")

    for slot_index in range(1, 10):
        slot_id = f"SLOT-{slot_index:02d}"
        attempt_ordinal = (
            2 if terminal_failure_before_success and slot_id == "SLOT-01" else 1
        )
        session_tool.reserve_attempt(
            native=native,
            claim_path=claim_path,
            slot_id=slot_id,
            attempt_ordinal=attempt_ordinal,
            common_dir_override=common_root,
        )
        attempt = claim["slots"][slot_index - 1]["attempts"][
            attempt_ordinal - 1
        ]
        Path(attempt["outputPath"]).write_bytes(outputs[slot_id])
        _, terminal = session_tool.finalize_attempt(
            native=native,
            claim_path=claim_path,
            slot_id=slot_id,
            attempt_ordinal=attempt_ordinal,
            common_dir_override=common_root,
        )
        if terminal["outcome"] != "SUCCESS":
            raise AssertionError(
                f"fixture {slot_id}/{attempt_ordinal} was not schema-valid: {terminal}"
            )

    return {
        "commonRoot": common_root,
        "sessionTool": session_tool,
        "sessionToolProxy": _SessionToolCommonRootProxy(
            session_tool,
            common_root,
        ),
        "claimPath": claim_path,
        "claim": claim,
        "candidate": candidate,
        "candidateRaw": candidate_raw,
        "receipt": receipt,
        "receiptRaw": receipt_raw,
        "outputs": outputs,
    }


def make_production_replacement_session_authority_fixture(
    common_root: Path,
    *,
    tag: str,
    unstable_lane: str = "COLD-JOURNEY",
) -> dict[str, Any]:
    """Extend a real INITIAL session with exact scorecard/seal and REPLACEMENT."""

    initial = make_production_session_authority_fixture(
        common_root,
        tag=f"{tag}-initial",
    )
    initial_authority = validate_production_session_authority(initial)
    session_tool = initial["sessionTool"]
    initial_claim = initial["claim"]
    initial_claim_path = initial["claimPath"]
    initial_claim_raw = initial_claim_path.read_bytes()

    def unstable_labeler(judge: int, kind: str, artifact: str, cell: str) -> str:
        cold_target = (
            unstable_lane == "COLD-JOURNEY"
            and kind == "COLD_ACTOR"
            and artifact == "ARTIFACT-A"
            and cell == "J1"
        )
        coverage_target = (
            unstable_lane == "COVERAGE-JOURNEY"
            and kind == "COVERAGE"
            and cell == "V1"
        )
        if cold_target or coverage_target:
            return ("EXCELLENT", "SERVICEABLE", "STRONG")[judge]
        return "STRONG"

    scorecard_template = make_fixture(
        common_root / f"replacement-scorecard-template-{tag}",
        labeler=unstable_labeler,
        panel_suffix=f"replacement-scorecard-template-{tag}",
    )
    scorecard = aggregate_fixture(scorecard_template)
    expected_status = {
        "COLD-JOURNEY": "RERUN_REQUIRED_COLD_INSTABILITY",
        "COVERAGE-JOURNEY": "RERUN_REQUIRED_COVERAGE_INSTABILITY",
    }[unstable_lane]
    if scorecard["status"] != expected_status:
        raise AssertionError("replacement fixture did not produce a rerun scorecard")
    provenance = scorecard["provenance"]
    provenance.update(initial_authority["provenance"])
    provenance.update({
        "candidateManifestSha256": initial_claim["candidateManifestSha256"],
        "candidateManifestRawSha256": initial_claim[
            "candidateManifestRawSha256"
        ],
        "sourceCommit": initial_claim["sourceCommit"],
        "executionArtifactSha256": initial_claim["executionArtifactSha256"],
        "holdoutConsumptionReceiptSha256": initial_claim[
            "holdoutConsumptionReceiptSha256"
        ],
        "holdoutConsumptionReceiptRawSha256": initial_claim[
            "holdoutConsumptionReceiptRawSha256"
        ],
    })
    scorecard_path = Path(initial_claim["fixedArtifactPaths"]["scorecard"])
    scorecard_raw = aggregator.canonical_json_bytes(scorecard) + b"\n"
    scorecard_path.write_bytes(scorecard_raw)
    seal_path = Path(
        initial_claim["fixedArtifactPaths"]["panelFinalizationSeal"]
    )
    seal_raw = aggregator._panel_finalization_seal_bytes(
        seal_path,
        "INITIAL",
        scorecard["judgePanelSha256"],
        scorecard,
        scorecard_path,
        aggregator.bytes_sha256(scorecard_raw),
    )
    seal_path.write_bytes(seal_raw)
    seal_value = json.loads(seal_raw)
    initial_seal = {
        "value": seal_value,
        "selfSha256": seal_value["panelFinalizationSealSha256"],
        "rawSha256": aggregator.bytes_sha256(seal_raw),
    }
    replacement_context = {
        "initial": scorecard,
        "initialBytes": scorecard_raw,
        "initialSeal": initial_seal,
    }

    validator = session_tool.load_validator()
    initial_reference = session_tool.read_and_validate_initial_finalization(
        validator=validator,
        initial_claim=initial_claim,
        initial_claim_path=initial_claim_path,
        initial_claim_raw_bytes=initial_claim_raw,
        scorecard_path=scorecard_path,
        panel_finalization_seal_path=seal_path,
    )
    policy = json.loads(
        (aggregator.NATIVE_DIRECTORY / "evaluation-session-policy.json").read_bytes()
    )
    receipt_sha = initial_claim["holdoutConsumptionReceiptSha256"]
    receipt_root = initial_claim_path.parent
    replacement_root = receipt_root / "replacement-01"
    replacement_claim_path = receipt_root / "replacement-01-claim.json"
    fixed_paths = {
        key: str(replacement_root / "artifacts" / filename)
        for key, filename in policy["fixedArtifactNames"].items()
    }
    replacement_claim = {
        **{
            key: copy.deepcopy(initial_claim[key])
            for key in (
                "schemaVersion",
                "protocol",
                "evaluationSessionClaimSchemaSha256",
                "evaluationSessionPolicySha256",
                "sessionClaimToolSha256",
                "policyId",
                "evaluationPhase",
                "officialCommercialUX",
                "candidateId",
                "candidateManifestSha256",
                "candidateManifestRawSha256",
                "sourceCommit",
                "candidatePlayableFingerprintSha256",
                "executionArtifactSha256",
                "authorityHashesSha256",
                "holdoutConsumptionReceiptSha256",
                "holdoutConsumptionReceiptRawSha256",
                "selectedRecipeId",
                "selectedRecipeSha256",
                "replacementClaimPath",
                "atomicClaim",
                "status",
            )
        },
        "evaluationSessionClaimSha256": "sha256:" + "0" * 64,
        "sessionId": session_tool.session_id(
            receipt_sha,
            "REPLACEMENT",
            initial_claim["evaluationSessionClaimSha256"],
            initial_claim["evaluationSessionPolicySha256"],
        ),
        "sessionMode": "REPLACEMENT",
        "canonicalSessionRoot": str(replacement_root),
        "canonicalClaimPath": str(replacement_claim_path),
        "initialSession": initial_reference,
        "sessionLockPath": str(replacement_root / "session.lock"),
        "requiredFreshSlotIds": session_tool.required_fresh_slot_ids(
            "REPLACEMENT", initial_reference, policy
        ),
        "slots": session_tool.build_slots(replacement_root, policy),
        "fixedArtifactPaths": fixed_paths,
    }
    replacement_claim["evaluationSessionClaimSha256"] = session_tool.self_hash(
        replacement_claim,
        "evaluationSessionClaimSha256",
    )
    write_json(replacement_claim_path, replacement_claim)
    replacement_root.mkdir()
    (replacement_root / "artifacts").mkdir()
    for slot_index in range(1, 10):
        slot_id = f"SLOT-{slot_index:02d}"
        if slot_id not in replacement_claim["requiredFreshSlotIds"]:
            continue
        session_tool.reserve_attempt(
            native=aggregator.NATIVE_DIRECTORY,
            claim_path=replacement_claim_path,
            slot_id=slot_id,
            attempt_ordinal=1,
            common_dir_override=common_root,
        )
        attempt = replacement_claim["slots"][slot_index - 1]["attempts"][0]
        Path(attempt["outputPath"]).write_bytes(initial["outputs"][slot_id])
        _, terminal = session_tool.finalize_attempt(
            native=aggregator.NATIVE_DIRECTORY,
            claim_path=replacement_claim_path,
            slot_id=slot_id,
            attempt_ordinal=1,
            common_dir_override=common_root,
        )
        if terminal["outcome"] != "SUCCESS":
            raise AssertionError(
                f"replacement fixture {slot_id} was not schema-valid: {terminal}"
            )
    return {
        **initial,
        "claimPath": replacement_claim_path,
        "claim": replacement_claim,
        "replacementContext": replacement_context,
    }


def validate_production_session_authority(
    fixture: dict[str, Any],
    replacement_context: dict[str, Any] | None = None,
) -> dict[str, Any]:
    real_loader = aggregator._load_exact_validator

    def load_exact(path: Path, module_name: str) -> Any:
        if path == aggregator.SESSION_CLAIM_TOOL_PATH:
            return fixture["sessionToolProxy"]
        return real_loader(path, module_name)

    with mock.patch.object(
        aggregator,
        "_load_exact_validator",
        side_effect=load_exact,
    ):
        return aggregator.validate_evaluation_session_authority(
            fixture["claimPath"],
            fixture["candidate"],
            fixture["candidateRaw"],
            fixture["receipt"],
            fixture["receiptRaw"],
            replacement_context,
        )


def actor_inputs_for_authority_test(
    fixture: dict[str, Any],
    observations: list[dict[str, Any]],
) -> tuple[
    list[tuple[dict[str, Any], bytes]],
    dict[str, Any],
    list[dict[str, Any]],
]:
    evaluation = copy.deepcopy(fixture["evaluationRun"])
    response_inputs: list[tuple[dict[str, Any], bytes]] = []
    response_self_shas: list[str] = []
    response_raw_shas: list[str] = []
    for base_response, observation in zip(
        fixture["coldActorResponses"],
        observations,
    ):
        response = copy.deepcopy(base_response)
        for field in (
            "actorRunId",
            "actorSlot",
            "model",
            "reasoningEffort",
            "objective",
            "terminalState",
            "terminalIncidentOrdinal",
        ):
            response[field] = observation[field]
        response["firstUseRecords"] = [
            {
                field: row[field]
                for field in (
                    "firstUseOrdinal",
                    "probeId",
                    "currentGoal",
                    "expectedVisibleConsequence",
                    "citedVisibleSourceDescription",
                )
            }
            for row in observation["firstUseRecords"]
        ]
        response["approvalRecords"] = [
            {
                field: row[field]
                for field in (
                    "approvalOrdinal",
                    "predictionImmediatelyBeforeApproval",
                    "observedResult",
                    "causalAccount",
                )
            }
            for row in observation["approvalRecords"]
        ]
        response["incidents"] = [
            {
                field: row[field]
                for field in (
                    "incidentOrdinal",
                    "incidentType",
                    "confusionBoundary",
                    "severity",
                    "description",
                )
            }
            for row in observation["incidents"]
        ]
        response["coldActorResponseSha256"] = aggregator.self_sha256(
            response,
            "coldActorResponseSha256",
            "cold actor response fixture",
        )
        response_raw = (
            json.dumps(response, ensure_ascii=False, indent=2, sort_keys=True).encode("utf-8")
            + b"\n"
        )
        response_raw_sha = aggregator.bytes_sha256(response_raw)
        observation["coldActorResponseSha256"] = response["coldActorResponseSha256"]
        observation["coldActorResponseRawSha256"] = response_raw_sha
        response_inputs.append((response, response_raw))
        response_self_shas.append(response["coldActorResponseSha256"])
        response_raw_shas.append(response_raw_sha)

    inputs: list[tuple[dict[str, Any], bytes]] = []
    raw_shas: list[str] = []
    for observation in observations:
        raw_bytes = (
            json.dumps(observation, ensure_ascii=False, indent=2, sort_keys=True).encode("utf-8")
            + b"\n"
        )
        inputs.append((observation, raw_bytes))
        raw_shas.append(aggregator.bytes_sha256(raw_bytes))
    evaluation["artifacts"]["actorObservationRawSha256"] = raw_shas
    evaluation["artifacts"]["coldActorResponseSha256"] = response_self_shas
    evaluation["artifacts"]["coldActorResponseRawSha256"] = response_raw_shas
    for index, raw_sha in enumerate(raw_shas):
        evaluation["terminalStates"][index]["actorObservationRawSha256"] = raw_sha
    responses = aggregator.validate_cold_actor_response_authorities(
        response_inputs,
        evaluation,
    )
    return inputs, evaluation, responses


def rebind_evaluation_fixture(fixture: dict[str, Any]) -> None:
    evaluation_sha, evaluation_raw_sha = write_self_hashed_json(
        fixture["evaluationRunPath"],
        fixture["evaluationRun"],
        "evaluationRunManifestSha256",
    )
    fixture["candidate"]["provenance"]["evaluationRunManifestSha256"] = evaluation_sha
    fixture["candidate"]["provenance"]["evaluationRunManifestRawSha256"] = evaluation_raw_sha
    write_json(fixture["candidatePath"], fixture["candidate"])


class NativeAggregateTests(unittest.TestCase):
    def test_cold_completion_and_stall_use_exact_mandatory_branch_prefixes(self) -> None:
        authority = aggregator._cold_checkpoint_completion_authority()

        def checkpoints(
            *,
            frontier: int = 41,
            branch: str = "keep",
            include_optional: bool = False,
        ) -> list[dict[str, Any]]:
            selected: list[dict[str, Any]] = []
            seen_ordinals: set[int] = set()
            for (episode, checkpoint), row in authority["rowsByCheckpoint"].items():
                ordinal = row["sequenceOrdinal"]
                if ordinal > frontier or ordinal in seen_ordinals:
                    continue
                if row["branchAlternativeGroup"] is not None and branch not in checkpoint:
                    continue
                if (
                    row["completionRequirement"] == "OPTIONAL"
                    and not include_optional
                ):
                    continue
                seen_ordinals.add(ordinal)
                selected.append({
                    "episode": episode,
                    "checkpoint": checkpoint,
                    "recipeCheckpointSequenceOrdinal": ordinal,
                })
            return selected

        for branch in ("keep", "defer"):
            for include_optional in (False, True):
                with self.subTest(branch=branch, optional=include_optional):
                    completed = {
                        "checkpoints": checkpoints(
                            branch=branch,
                            include_optional=include_optional,
                        ),
                        "terminalState": "COMPLETED",
                        "terminalIncidentKey": None,
                        "incidents": [],
                    }
                    aggregator._validate_cold_terminal_checkpoint_sequence(
                        completed,
                        authority,
                        "completed fixture",
                    )

        incomplete = {
            "checkpoints": [
                row
                for row in checkpoints()
                if row["recipeCheckpointSequenceOrdinal"] != 17
            ],
            "terminalState": "COMPLETED",
            "terminalIncidentKey": None,
            "incidents": [],
        }
        with self.assertRaisesRegex(
            aggregator.ProvenanceFailure,
            "mandatory completion sequence",
        ):
            aggregator._validate_cold_terminal_checkpoint_sequence(
                incomplete,
                authority,
                "incomplete fixture",
            )

        stalled_checkpoints = checkpoints(frontier=24, branch="defer")
        stalled = {
            "checkpoints": stalled_checkpoints,
            "terminalState": "PLAYER_STALLED",
            "terminalIncidentKey": "STALL-1",
            "incidents": [{
                "incidentKey": "STALL-1",
                "checkpointOrdinals": [len(stalled_checkpoints)],
            }],
        }
        aggregator._validate_cold_terminal_checkpoint_sequence(
            stalled,
            authority,
            "stalled fixture",
        )
        stalled["checkpoints"] = [
            row
            for row in stalled_checkpoints
            if row["recipeCheckpointSequenceOrdinal"] != 16
        ]
        stalled["incidents"][0]["checkpointOrdinals"] = [
            len(stalled["checkpoints"])
        ]
        with self.assertRaisesRegex(
            aggregator.ProvenanceFailure,
            "mandatory terminal prefix sequence",
        ):
            aggregator._validate_cold_terminal_checkpoint_sequence(
                stalled,
                authority,
                "stalled fixture",
            )

    def test_restricted_png_decoder_rejects_scanline_filter_order_and_crc_drift(self) -> None:
        valid = restricted_png(b"\x00\x10\x20\x30")
        self.assertTrue(aggregator._valid_png_bytes(valid))
        mutations = {
            "truncated scanline": restricted_png(b"\x00\x10\x20"),
            "invalid filter": restricted_png(b"\x05\x10\x20\x30"),
            "IDAT before IHDR": b"".join((
                b"\x89PNG\r\n\x1a\n",
                png_chunk(b"IDAT", zlib.compress(b"\x00\x10\x20\x30")),
                png_chunk(
                    b"IHDR",
                    struct.pack(">IIBBBBB", 1, 1, 8, 2, 0, 0, 0),
                ),
                png_chunk(b"IEND", b""),
            )),
            "bad CRC": bytes(bytearray(valid[:-1]) + bytes([valid[-1] ^ 1])),
        }
        for label, raw in mutations.items():
            with self.subTest(label=label):
                self.assertFalse(aggregator._valid_png_bytes(raw))

    def test_audio_sync_is_derived_from_action_delivery_and_wav_sample_time(self) -> None:
        recording, action_ledger = audio_sync_authorities()
        derived = aggregator._validate_audio_sync_ledger(recording, action_ledger)
        self.assertEqual(derived["status"], "PASS")
        self.assertEqual(len(derived["events"]), 4)
        self.assertTrue(all(
            row["latencyMicroseconds"] == 40_000
            and row["within100Milliseconds"]
            for row in derived["events"]
        ))

        timestamp_mutation = audio_sync_authorities(
            first_ledger_delivery_delta=1,
        )
        with self.assertRaisesRegex(
            aggregator.ProvenanceFailure,
            "timestamp is not ledger-derived",
        ):
            aggregator._validate_audio_sync_ledger(*timestamp_mutation)

        clock_mutation = audio_sync_authorities(
            sync_clock_domain="UNRELATED-CLOCK",
        )
        with self.assertRaisesRegex(
            aggregator.ProvenanceFailure,
            "clock domain is not action-ledger-derived",
        ):
            aggregator._validate_audio_sync_ledger(*clock_mutation)

        slow_recording, slow_action_ledger = audio_sync_authorities(
            latency_nanoseconds=100_000_001,
        )
        slow = aggregator._validate_audio_sync_ledger(
            slow_recording,
            slow_action_ledger,
        )
        self.assertEqual(slow["status"], "FAIL")
        self.assertEqual(slow["failureCode"], "AUDIO_SYNC_OVER_100MS")

    def test_replacement_execution_roots_are_disjoint_or_exact_by_lane(self) -> None:
        def identities(suffix: str) -> dict[str, Any]:
            return {
                "cold": [
                    {
                        "actorCaptureSlot": slot,
                        "actorRunId": f"actor-{slot}-{suffix}",
                        "processTreeId": f"process-{slot}-{suffix}",
                        "userDataSha256": identity_sha(f"user-{slot}-{suffix}"),
                        "saveSha256": identity_sha(f"save-{slot}-{suffix}"),
                        "journalSha256": identity_sha(f"journal-{slot}-{suffix}"),
                        "recordingManifestSha256": identity_sha(
                            f"recording-self-{slot}-{suffix}"
                        ),
                        "recordingManifestRawSha256": identity_sha(
                            f"recording-raw-{slot}-{suffix}"
                        ),
                        "recordingContentRootSha256": identity_sha(
                            f"recording-content-{slot}-{suffix}"
                        ),
                        "canonicalRecordingRoot": f"/recordings/{slot}/{suffix}",
                    }
                    for slot in range(3)
                ],
                "coverage": {
                    "coverageRunId": f"coverage-{suffix}",
                    "processTreeId": f"coverage-process-{suffix}",
                    "userDataSha256": identity_sha(f"coverage-user-{suffix}"),
                    "journalBundleSha256": identity_sha(
                        f"coverage-journal-{suffix}"
                    ),
                    "recordingManifestSha256": identity_sha(
                        f"coverage-recording-self-{suffix}"
                    ),
                    "recordingManifestRawSha256": identity_sha(
                        f"coverage-recording-raw-{suffix}"
                    ),
                    "recordingContentRootSha256": identity_sha(
                        f"coverage-recording-content-{suffix}"
                    ),
                    "canonicalRecordingRoot": f"/coverage/{suffix}",
                },
            }

        old_identities = identities("old")
        new_identities = identities("new")
        new_identities["coverage"] = copy.deepcopy(old_identities["coverage"])
        initial = {
            "replacementRequiredLanes": ["COLD-JOURNEY"],
            "panelArtifactBindings": [
                *[
                    {
                        "artifactKind": "COLD_ACTOR",
                        "artifactSha256": identity_sha(f"old-cold-{slot}"),
                    }
                    for slot in range(3)
                ],
                {
                    "artifactKind": "COVERAGE",
                    "artifactSha256": identity_sha("shared-coverage"),
                },
            ],
            "provenance": {
                "coldActorResponseSha256": [
                    identity_sha(f"old-response-{slot}") for slot in range(3)
                ],
                "coldActorResponseRawSha256": [
                    identity_sha(f"old-response-raw-{slot}") for slot in range(3)
                ],
                "laneExecutionIdentities": old_identities,
            },
        }
        candidate = {
            "artifactBindings": [
                *[
                    {
                        "artifactKind": "COLD_ACTOR",
                        "artifactSha256": identity_sha(f"new-cold-{slot}"),
                    }
                    for slot in range(3)
                ],
                {
                    "artifactKind": "COVERAGE",
                    "artifactSha256": identity_sha("shared-coverage"),
                },
            ],
            "provenance": {
                "coldActorResponseSha256": [
                    identity_sha(f"new-response-{slot}") for slot in range(3)
                ],
                "coldActorResponseRawSha256": [
                    identity_sha(f"new-response-raw-{slot}") for slot in range(3)
                ],
            },
        }
        repackaged = copy.deepcopy(new_identities)
        repackaged["cold"][1]["recordingContentRootSha256"] = (
            old_identities["cold"][1]["recordingContentRootSha256"]
        )
        with self.assertRaisesRegex(
            aggregator.ProvenanceFailure,
            "recordingContentRootSha256 must be disjoint",
        ):
            aggregator._validate_replacement_artifact_freshness(
                initial,
                candidate,
                repackaged,
            )

        aggregator._validate_replacement_artifact_freshness(
            initial,
            candidate,
            new_identities,
        )
        drifted_unchanged_lane = copy.deepcopy(new_identities)
        drifted_unchanged_lane["coverage"]["journalBundleSha256"] = identity_sha(
            "drifted-unchanged-coverage-journal"
        )
        with self.assertRaisesRegex(
            aggregator.ProvenanceFailure,
            "preserve exact coverage execution identity",
        ):
            aggregator._validate_replacement_artifact_freshness(
                initial,
                candidate,
                drifted_unchanged_lane,
            )

    def test_official_finalization_reserves_output_then_seals_then_writes(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(
                Path(raw),
                labeler=constant_label("EXCELLENT"),
            )
            output_path = fixture["directory"] / "scorecard.json"
            original_receipt = aggregator._finalize_reserved_receipt
            original_scorecard = aggregator._finalize_reserved_scorecard_output
            finalized_receipts: list[tuple[Path, bytes]] = []

            def finalize_receipt(descriptor: int, path: Path, content: bytes) -> None:
                self.assertTrue(output_path.exists())
                self.assertEqual(output_path.read_bytes(), b"")
                original_receipt(descriptor, path, content)
                finalized_receipts.append((path, content))

            def finalize_scorecard(descriptor: int, path: Path, content: bytes) -> None:
                self.assertEqual(len(finalized_receipts), 2)
                for receipt_path, receipt_bytes in finalized_receipts:
                    self.assertEqual(receipt_path.read_bytes(), receipt_bytes)
                original_scorecard(descriptor, path, content)

            with (
                mock.patch.object(
                    aggregator,
                    "_finalize_reserved_receipt",
                    side_effect=finalize_receipt,
                ),
                mock.patch.object(
                    aggregator,
                    "_finalize_reserved_scorecard_output",
                    side_effect=finalize_scorecard,
                ),
            ):
                result = aggregate_fixture(fixture)
            self.assertEqual(result["status"], "PASS")
            self.assertEqual(json.loads(output_path.read_bytes()), result)
            aggregator.validate_native_scorecard_schema(result)
            panel_seals = [
                json.loads(content)
                for _path, content in finalized_receipts
                if json.loads(content)["schemaVersion"]
                == aggregator.PANEL_FINALIZATION_SEAL_SCHEMA
            ]
            self.assertEqual(len(panel_seals), 1)
            self.assertEqual(
                panel_seals[0]["claimPolicy"],
                "O_EXCL_SCORECARD_RESERVE_FSYNC_THEN_HOLDOUT_AND_PANEL_SEAL_FSYNC_"
                "THEN_SCORECARD_WRITE_FSYNC",
            )

    def test_receipt_or_panel_finalizer_fault_leaves_no_valid_pass_scorecard(self) -> None:
        original = aggregator._finalize_reserved_receipt
        for fault_call in (1, 2):
            with self.subTest(fault_call=fault_call), tempfile.TemporaryDirectory() as raw:
                fixture = make_fixture(
                    Path(raw),
                    labeler=constant_label("EXCELLENT"),
                    panel_suffix=f"fault-{fault_call}",
                )
                output_path = fixture["directory"] / "scorecard.json"
                call_count = 0

                def fail_once(descriptor: int, path: Path, content: bytes) -> None:
                    nonlocal call_count
                    call_count += 1
                    if call_count == fault_call:
                        os.close(descriptor)
                        raise OSError(f"injected finalizer fault {fault_call}")
                    original(descriptor, path, content)

                with mock.patch.object(
                    aggregator,
                    "_finalize_reserved_receipt",
                    side_effect=fail_once,
                ):
                    with self.assertRaisesRegex(OSError, "injected finalizer fault"):
                        aggregate_fixture(fixture)
                self.assertTrue(output_path.exists())
                self.assertEqual(output_path.read_bytes(), b"")

    def test_scorecard_finalizer_fault_after_full_write_truncates_pass(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(
                Path(raw),
                labeler=constant_label("EXCELLENT"),
            )
            output_path = fixture["directory"] / "scorecard.json"
            original = aggregator._finalize_reserved_scorecard_output

            def fail_after_write(descriptor: int, path: Path, content: bytes) -> None:
                original(descriptor, path, content)
                self.assertEqual(json.loads(path.read_bytes())["status"], "PASS")
                raise aggregator.ValidationFailure(
                    "injected post-write scorecard fsync fault"
                )

            with mock.patch.object(
                aggregator,
                "_finalize_reserved_scorecard_output",
                side_effect=fail_after_write,
            ):
                with self.assertRaisesRegex(
                    aggregator.ValidationFailure,
                    "injected post-write scorecard fsync fault",
                ):
                    aggregate_fixture(fixture)
            self.assertTrue(output_path.exists())
            self.assertEqual(output_path.read_bytes(), b"")

    def test_actor_incident_verifier_status_is_output_derived(self) -> None:
        incident = make_incident("CONFUSION", 79, ["ARTIFACT-A"])
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw), incidents=[incident])
            fixture["ledger"]["incidents"][0]["verifierStatus"] = "PARTIAL"
            ledger_raw_sha = write_json(fixture["ledgerPath"], fixture["ledger"])
            fixture["candidate"]["provenance"][
                "oracleHardGateLedgerSha256"
            ] = ledger_raw_sha
            fixture["evaluationRun"]["artifacts"][
                "oracleHardGateLedgerSha256"
            ] = ledger_raw_sha
            rebind_evaluation_fixture(fixture)
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "verifier status is not output-derived",
            ):
                aggregate_fixture(fixture, refresh_authorities=False)

    def test_official_scoring_rejects_checked_in_blocked_pre_capture_contract(self) -> None:
        """Self-declared gold/oracle readiness cannot bypass the frozen block."""

        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(
                Path(raw),
                labeler=constant_label("EXCELLENT"),
                score_bearing_ready=True,
            )
            with self.assertRaisesRegex(
                aggregator.ValidationFailure,
                "BLOCKED_PRE_CAPTURE|score-bearing capture",
            ):
                aggregate_fixture(fixture, patch_preflight=False)

    def test_candidate_execution_identity_is_recomputed_from_component_bytes(self) -> None:
        """A resealed manifest cannot preserve a declared execution identity after byte drift."""

        with tempfile.TemporaryDirectory() as raw:
            directory = Path(raw).resolve()
            components = {
                "godotExecutable": directory / "godot",
                "managedAssembly": directory / "Gridworks.dll",
                "pckResourceManifest": directory / "resources.json",
            }
            for name, path in components.items():
                path.write_bytes(f"exact-{name}".encode("utf-8"))
            execution = {
                "godotExecutablePath": str(components["godotExecutable"]),
                "godotExecutableSha256": aggregator.file_sha256(
                    components["godotExecutable"], "test Godot executable"
                ),
                "managedAssemblyPath": str(components["managedAssembly"]),
                "managedAssemblySha256": aggregator.file_sha256(
                    components["managedAssembly"], "test managed assembly"
                ),
                "pckResourceManifestPath": str(components["pckResourceManifest"]),
                "pckResourceManifestSha256": aggregator.file_sha256(
                    components["pckResourceManifest"], "test PCK manifest"
                ),
                "packagePath": None,
                "packageSha256": None,
                "packageStatus": "EDITOR_NATIVE_NOT_PUBLIC_PACKAGE",
            }
            execution["executionArtifactSha256"] = aggregator.canonical_sha256({
                "godotExecutableSha256": execution["godotExecutableSha256"],
                "managedAssemblySha256": execution["managedAssemblySha256"],
                "pckResourceManifestSha256": execution[
                    "pckResourceManifestSha256"
                ],
                "packageSha256": None,
                "packageStatus": execution["packageStatus"],
            })
            manifest = {"execution": execution}
            aggregator.validate_candidate_execution_authority(manifest)
            components["managedAssembly"].write_bytes(b"mutated-assembly")
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "managedAssemblySha256 raw SHA mismatch",
            ):
                aggregator.validate_candidate_execution_authority(manifest)

    def test_runtime_contract_preflight_receives_the_complete_runtime_dag(self) -> None:
        """Production preflight cannot silently fall back to checked-in booleans."""

        with tempfile.TemporaryDirectory() as raw:
            directory = Path(raw)
            paths = [directory / f"authority-{index}.json" for index in range(7)]
            raw_inputs = [f'{{"row":{index}}}\n'.encode() for index in range(7)]
            captured: dict[str, Any] = {}

            def validate_exact(_native, _rubric, **kwargs):
                captured.update(kwargs)
                return [], {
                    "status": "PASS",
                    "observedRawSha256": {
                        "candidateManifestRawSha256": aggregator.bytes_sha256(
                            kwargs["candidate_manifest_bytes"]
                        ),
                        "qualificationReceiptRawSha256": aggregator.bytes_sha256(
                            kwargs["qualification_receipt_bytes"]
                        ),
                        "goldBindingManifestRawSha256": aggregator.bytes_sha256(
                            kwargs["gold_binding_manifest_bytes"]
                        ),
                        "holdoutConsumptionReceiptRawSha256": aggregator.bytes_sha256(
                            kwargs["holdout_consumption_receipt_bytes"]
                        ),
                        "registryBeforeRawSha256": aggregator.bytes_sha256(
                            kwargs["registry_before_bytes"]
                        ),
                        "registryAfterRawSha256": aggregator.bytes_sha256(
                            kwargs["registry_after_bytes"]
                        ),
                        "evaluationSessionClaimRawSha256": aggregator.bytes_sha256(
                            kwargs["evaluation_session_claim_bytes"]
                        ),
                    },
                }

            validator = mock.Mock(
                validate_runtime_contract_bytes=validate_exact,
            )
            with mock.patch.object(
                aggregator,
                "_load_exact_validator",
                return_value=validator,
            ):
                aggregator.validate_runtime_contract_authority(
                    *paths,
                    candidate_manifest_raw_bytes=raw_inputs[0],
                    qualification_receipt_raw_bytes=raw_inputs[1],
                    gold_binding_raw_bytes=raw_inputs[2],
                    holdout_consumption_receipt_raw_bytes=raw_inputs[3],
                    holdout_registry_before_raw_bytes=raw_inputs[4],
                    holdout_registry_after_raw_bytes=raw_inputs[5],
                    evaluation_session_claim_raw_bytes=raw_inputs[6],
                    initial_evaluation_session_claim_path=None,
                    initial_evaluation_session_claim_raw_bytes=None,
                )
            byte_fields = (
                "candidate_manifest_bytes",
                "qualification_receipt_bytes",
                "gold_binding_manifest_bytes",
                "holdout_consumption_receipt_bytes",
                "registry_before_bytes",
                "registry_after_bytes",
                "evaluation_session_claim_bytes",
            )
            path_fields = (
                "candidate_manifest_path_label",
                "qualification_receipt_path_label",
                "gold_binding_manifest_path_label",
                "holdout_consumption_receipt_path_label",
                "registry_before_path_label",
                "registry_after_path_label",
                "evaluation_session_claim_path_label",
            )
            for field, expected in zip(byte_fields, raw_inputs):
                self.assertEqual(captured[field], expected)
            for field, path in zip(path_fields, paths):
                self.assertEqual(captured[field], path.resolve(strict=False))

    def test_evaluation_session_authority_discovers_complete_nine_slot_session(self) -> None:
        """The production authority discovers one successful attempt per fixed slot."""

        with tempfile.TemporaryDirectory() as raw:
            fixture = make_production_session_authority_fixture(
                Path(raw).resolve(),
                tag="complete-nine-slot",
            )
            authority = validate_production_session_authority(fixture)
        self.assertEqual(len(authority["attemptAuditRows"]), 9)
        self.assertEqual(len(authority["selectedRows"]), 9)
        self.assertEqual(
            set(authority["selectedBySlot"]),
            {f"SLOT-{index:02d}" for index in range(1, 10)},
        )
        self.assertTrue(
            all(row["outcome"] == "SUCCESS" for row in authority["attemptAuditRows"])
        )

    def test_evaluation_session_authority_cannot_omit_present_failure_attempt(self) -> None:
        """A terminal failure remains in the full audit when its retry succeeds."""

        with tempfile.TemporaryDirectory() as raw:
            fixture = make_production_session_authority_fixture(
                Path(raw).resolve(),
                tag="failure-then-success",
                terminal_failure_before_success=True,
            )
            authority = validate_production_session_authority(fixture)
        self.assertEqual(len(authority["attemptAuditRows"]), 10)
        slot_one = [
            row
            for row in authority["attemptAuditRows"]
            if row["slotId"] == "SLOT-01"
        ]
        self.assertEqual(
            [(row["attemptOrdinal"], row["outcome"]) for row in slot_one],
            [(1, "TRANSPORT_FAILURE"), (2, "SUCCESS")],
        )
        selected_slot_one = next(
            row for row in authority["selectedRows"] if row["slotId"] == "SLOT-01"
        )
        self.assertEqual(selected_slot_one["attemptOrdinal"], 2)
        self.assertEqual(
            authority["provenance"]["evaluationAttemptAuditSha256"],
            aggregator.canonical_sha256(authority["attemptAuditRows"]),
        )

    def test_evaluation_session_authority_rejects_undeclared_attempt_and_root(self) -> None:
        """Filesystem discovery fails closed on both slot and session-root extras."""

        with tempfile.TemporaryDirectory() as raw:
            common_root = Path(raw).resolve()
            undeclared_attempt = make_production_session_authority_fixture(
                common_root,
                tag="undeclared-attempt",
            )
            slot_root = Path(
                undeclared_attempt["claim"]["slots"][0]["slotRoot"]
            )
            (slot_root / "attempt-99").mkdir()
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "contains undeclared attempts",
            ):
                validate_production_session_authority(undeclared_attempt)

            undeclared_root = make_production_session_authority_fixture(
                common_root,
                tag="undeclared-root",
            )
            session_root = Path(
                undeclared_root["claim"]["canonicalSessionRoot"]
            )
            (session_root / "unclaimed-root").mkdir()
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "session root must contain exactly",
            ):
                validate_production_session_authority(undeclared_root)

            undeclared_slot = make_production_session_authority_fixture(
                common_root,
                tag="undeclared-slot-root",
            )
            slots_root = (
                Path(undeclared_slot["claim"]["canonicalSessionRoot"]) / "slots"
            )
            (slots_root / "slot-10" / "attempt-01").mkdir(parents=True)
            (slots_root / "slot-10" / "attempt-01" / "output.json").write_text(
                "{}\n",
                encoding="utf-8",
            )
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "slots root must contain exactly",
            ):
                validate_production_session_authority(undeclared_slot)

            undeclared_sibling = make_production_session_authority_fixture(
                common_root,
                tag="undeclared-session-sibling",
            )
            receipt_root = undeclared_sibling["claimPath"].parent
            (receipt_root / "initial-discarded" / "slots").mkdir(parents=True)
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "receipt session hierarchy must contain exactly",
            ):
                validate_production_session_authority(undeclared_sibling)

    def test_evaluation_session_authority_rejects_attempt_and_fixed_symlink_aliases(self) -> None:
        """Neither captured output nor a fixed artifact may be a symlink alias."""

        with tempfile.TemporaryDirectory() as raw:
            common_root = Path(raw).resolve()
            attempt_alias = make_production_session_authority_fixture(
                common_root,
                tag="attempt-symlink",
            )
            attempt = attempt_alias["claim"]["slots"][1]["attempts"][0]
            output_path = Path(attempt["outputPath"])
            output_target = common_root / "aliased-attempt-output.json"
            output_target.write_bytes(output_path.read_bytes())
            output_path.unlink()
            output_path.symlink_to(output_target)
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "absolute and canonical|symlink",
            ):
                validate_production_session_authority(attempt_alias)

            fixed_alias = make_production_session_authority_fixture(
                common_root,
                tag="fixed-symlink",
            )
            authority = validate_production_session_authority(fixed_alias)
            fixed_paths = {
                key: Path(path)
                for key, path in fixed_alias["claim"]["fixedArtifactPaths"].items()
            }
            for key, path in fixed_paths.items():
                if key not in {"scorecard", "panelFinalizationSeal"}:
                    path.write_bytes(b"{}\n")
            fixed_target = common_root / "aliased-fixed-artifact.json"
            fixed_target.write_bytes(fixed_paths["goldBinding"].read_bytes())
            fixed_paths["goldBinding"].unlink()
            fixed_paths["goldBinding"].symlink_to(fixed_target)
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "not its claimed fixed artifact path",
            ):
                aggregator.validate_evaluation_session_fixed_artifacts(
                    authority,
                    fixed_paths,
                )

    def test_replacement_session_requires_exact_initial_scorecard_and_seal_link(self) -> None:
        """REPLACEMENT cannot drift one byte of its INITIAL finalization authority."""

        with tempfile.TemporaryDirectory() as raw:
            fixture = make_production_replacement_session_authority_fixture(
                Path(raw).resolve(),
                tag="exact-initial-link",
            )
            exact = validate_production_session_authority(
                fixture,
                fixture["replacementContext"],
            )
            self.assertEqual(exact["claim"]["sessionMode"], "REPLACEMENT")
            self.assertEqual(len(exact["selectedRows"]), 9)

            drifted_context = copy.deepcopy(fixture["replacementContext"])
            drifted_context["initialSeal"]["rawSha256"] = identity_sha(
                "drifted-initial-panel-finalization-seal-raw"
            )
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "does not link the exact initial scorecard and seal",
            ):
                validate_production_session_authority(
                    fixture,
                    drifted_context,
                )

    def test_replacement_session_executes_only_cold_fresh_slots_and_reuses_coverage(self) -> None:
        """Cold-only replacement selects INITIAL coverage without copying its root."""

        with tempfile.TemporaryDirectory() as raw:
            fixture = make_production_replacement_session_authority_fixture(
                Path(raw).resolve(),
                tag="cold-only-fresh-slots",
                unstable_lane="COLD-JOURNEY",
            )
            authority = validate_production_session_authority(
                fixture,
                fixture["replacementContext"],
            )
            self.assertEqual(
                fixture["claim"]["requiredFreshSlotIds"],
                [
                    "SLOT-01", "SLOT-02", "SLOT-03", "SLOT-05",
                    "SLOT-06", "SLOT-07", "SLOT-08", "SLOT-09",
                ],
            )
            self.assertEqual(len(authority["attemptAuditRows"]), 8)
            selected = {row["slotId"]: row for row in authority["selectedRows"]}
            self.assertEqual(selected["SLOT-04"]["sourceSessionMode"], "INITIAL")
            self.assertEqual(selected["SLOT-01"]["sourceSessionMode"], "REPLACEMENT")
            with self.assertRaisesRegex(
                fixture["sessionTool"].SessionClaimError,
                "reused stable lane",
            ):
                fixture["sessionTool"].reserve_attempt(
                    native=aggregator.NATIVE_DIRECTORY,
                    claim_path=fixture["claimPath"],
                    slot_id="SLOT-04",
                    attempt_ordinal=1,
                    common_dir_override=fixture["commonRoot"],
                )

    def test_replacement_session_executes_only_coverage_fresh_slots_and_reuses_cold(self) -> None:
        """Coverage-only replacement selects three INITIAL cold attempts exactly."""

        with tempfile.TemporaryDirectory() as raw:
            fixture = make_production_replacement_session_authority_fixture(
                Path(raw).resolve(),
                tag="coverage-only-fresh-slots",
                unstable_lane="COVERAGE-JOURNEY",
            )
            authority = validate_production_session_authority(
                fixture,
                fixture["replacementContext"],
            )
            self.assertEqual(
                fixture["claim"]["requiredFreshSlotIds"],
                [
                    "SLOT-04", "SLOT-05", "SLOT-06",
                    "SLOT-07", "SLOT-08", "SLOT-09",
                ],
            )
            self.assertEqual(len(authority["attemptAuditRows"]), 6)
            selected = {row["slotId"]: row for row in authority["selectedRows"]}
            self.assertTrue(
                all(
                    selected[f"SLOT-{index:02d}"]["sourceSessionMode"] == "INITIAL"
                    for index in range(1, 4)
                )
            )
            self.assertEqual(selected["SLOT-04"]["sourceSessionMode"], "REPLACEMENT")

    def test_shared_validators_freeze_relative_paths_before_changing_cwd(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            caller = Path(raw).resolve()
            relative_paths = [Path(f"authority-{index}.json") for index in range(7)]
            raw_inputs = [f'{{"row":{index}}}'.encode() for index in range(7)]
            captured: dict[str, Any] = {}

            def validate_exact(_native, _rubric, **kwargs):
                captured.update(kwargs)
                return [], {
                    "status": "PASS",
                    "observedRawSha256": {
                        "candidateManifestRawSha256": aggregator.bytes_sha256(raw_inputs[0]),
                        "qualificationReceiptRawSha256": aggregator.bytes_sha256(raw_inputs[1]),
                        "goldBindingManifestRawSha256": aggregator.bytes_sha256(raw_inputs[2]),
                        "holdoutConsumptionReceiptRawSha256": aggregator.bytes_sha256(raw_inputs[3]),
                        "registryBeforeRawSha256": aggregator.bytes_sha256(raw_inputs[4]),
                        "registryAfterRawSha256": aggregator.bytes_sha256(raw_inputs[5]),
                        "evaluationSessionClaimRawSha256": aggregator.bytes_sha256(raw_inputs[6]),
                    },
                }

            prior_cwd = Path.cwd()
            try:
                os.chdir(caller)
                with mock.patch.object(
                    aggregator,
                    "_load_exact_validator",
                    return_value=mock.Mock(
                        validate_runtime_contract_bytes=validate_exact,
                    ),
                ):
                    aggregator.validate_runtime_contract_authority(
                        *relative_paths,
                        candidate_manifest_raw_bytes=raw_inputs[0],
                        qualification_receipt_raw_bytes=raw_inputs[1],
                        gold_binding_raw_bytes=raw_inputs[2],
                        holdout_consumption_receipt_raw_bytes=raw_inputs[3],
                        holdout_registry_before_raw_bytes=raw_inputs[4],
                        holdout_registry_after_raw_bytes=raw_inputs[5],
                        evaluation_session_claim_raw_bytes=raw_inputs[6],
                        initial_evaluation_session_claim_path=None,
                        initial_evaluation_session_claim_raw_bytes=None,
                    )
            finally:
                os.chdir(prior_cwd)
            labels = [
                captured[field]
                for field in (
                    "candidate_manifest_path_label",
                    "qualification_receipt_path_label",
                    "gold_binding_manifest_path_label",
                    "holdout_consumption_receipt_path_label",
                    "registry_before_path_label",
                    "registry_after_path_label",
                    "evaluation_session_claim_path_label",
                )
            ]
            self.assertEqual(
                labels,
                [(caller / path).resolve(strict=False) for path in relative_paths],
            )

    def test_exact_validator_observed_raw_sha_cannot_be_substituted(self) -> None:
        paths = [Path(f"/tmp/exact-authority-{index}.json") for index in range(7)]
        raw_inputs = [f'{{"row":{index}}}'.encode() for index in range(7)]
        wrong_contract_observed = {
            "candidateManifestRawSha256": identity_sha("wrong-candidate"),
            "qualificationReceiptRawSha256": aggregator.bytes_sha256(raw_inputs[1]),
            "goldBindingManifestRawSha256": aggregator.bytes_sha256(raw_inputs[2]),
            "holdoutConsumptionReceiptRawSha256": aggregator.bytes_sha256(raw_inputs[3]),
            "registryBeforeRawSha256": aggregator.bytes_sha256(raw_inputs[4]),
            "registryAfterRawSha256": aggregator.bytes_sha256(raw_inputs[5]),
            "evaluationSessionClaimRawSha256": aggregator.bytes_sha256(raw_inputs[6]),
        }
        with mock.patch.object(
            aggregator,
            "_load_exact_validator",
            return_value=mock.Mock(
                validate_runtime_contract_bytes=mock.Mock(
                    return_value=([], {
                        "status": "PASS",
                        "observedRawSha256": wrong_contract_observed,
                    })
                ),
            ),
        ), self.assertRaisesRegex(
            aggregator.ProvenanceFailure,
            "observed raw SHA projection mismatch",
        ):
            aggregator.validate_runtime_contract_authority(
                *paths,
                candidate_manifest_raw_bytes=raw_inputs[0],
                qualification_receipt_raw_bytes=raw_inputs[1],
                gold_binding_raw_bytes=raw_inputs[2],
                holdout_consumption_receipt_raw_bytes=raw_inputs[3],
                holdout_registry_before_raw_bytes=raw_inputs[4],
                holdout_registry_after_raw_bytes=raw_inputs[5],
                evaluation_session_claim_raw_bytes=raw_inputs[6],
                initial_evaluation_session_claim_path=None,
                initial_evaluation_session_claim_raw_bytes=None,
            )

        candidate_raw, binding_raw, story_raw = raw_inputs[:3]
        with mock.patch.object(
            aggregator,
            "_load_exact_validator",
            return_value=mock.Mock(
                validate_exact_inputs=mock.Mock(
                    return_value=([], {
                        "scoreBearingReady": True,
                        "observedRawSha256": {
                            "goldStateManifestRawSha256": aggregator.file_sha256(
                                aggregator.GOLD_STATE_MANIFEST_PATH,
                                "test checked-in gold-state manifest",
                            ),
                            "candidateManifestRawSha256": aggregator.bytes_sha256(candidate_raw),
                            "goldBindingManifestRawSha256": aggregator.bytes_sha256(binding_raw),
                            "storyManifestRawSha256": aggregator.bytes_sha256(story_raw),
                            "unexpectedRawSha256": identity_sha("unexpected"),
                        },
                    })
                ),
            ),
        ), self.assertRaisesRegex(
            aggregator.ProvenanceFailure,
            "observed raw SHA projection mismatch",
        ):
            aggregator.validate_gold_state_score_ready_authority(
                paths[0],
                paths[2],
                paths[3],
                paths[4],
                paths[5],
                paths[6],
                candidate_manifest_raw_bytes=candidate_raw,
                gold_binding_raw_bytes=binding_raw,
                story_manifest_raw_bytes=story_raw,
                holdout_consumption_receipt_raw_bytes=raw_inputs[3],
                registry_before_raw_bytes=raw_inputs[4],
                registry_after_raw_bytes=raw_inputs[5],
                evaluation_session_claim_raw_bytes=raw_inputs[6],
                require_score_ready=True,
            )

    def test_semantic_preflights_use_the_current_absolute_interpreter(self) -> None:
        completed_json = subprocess.CompletedProcess(
            args=[],
            returncode=0,
            stdout=json.dumps({"status": "PASS"}),
            stderr="",
        )
        ready_contract = {
            "toolBindingPolicy": {
                "scoreBearingCaptureAllowed": True,
                "currentlyUnboundProducerStages": [],
            }
        }
        frozen_gold = {
            "candidateIndependent": True,
            "bindingComplete": False,
            "scoreBearingCaptureAllowed": False,
        }
        with mock.patch.object(
            aggregator,
            "read_json_bytes",
            side_effect=[(ready_contract, b"{}"), (frozen_gold, b"{}")],
        ), mock.patch.object(
            aggregator.subprocess,
            "run",
            return_value=completed_json,
        ) as run:
            aggregator.validate_official_score_bearing_preflight(
                {"recipeId": "HOLDOUT-01"}
            )
        self.assertEqual(run.call_args.args[0][0], sys.executable)

        candidate_raw = b'{"candidate":"exact"}\n'
        binding_raw = b'{"binding":"exact"}\n'
        story_raw = b'{"story":"exact"}\n'
        holdout_raw = b'{"holdout":"exact"}\n'
        registry_before_raw = b'{"registry":"before"}\n'
        registry_after_raw = b'{"registry":"after"}\n'
        session_claim_raw = b'{"session":"exact"}\n'
        captured: dict[str, Any] = {}

        def validate_gold_exact(_root, _manifest, story, _run, candidate, binding, _required, **_labels):
            captured.update({"story": story, "candidate": candidate, "binding": binding})
            return [], {
                "scoreBearingReady": True,
                "observedRawSha256": {
                    "goldStateManifestRawSha256": aggregator.file_sha256(
                        aggregator.GOLD_STATE_MANIFEST_PATH,
                        "test checked-in gold-state manifest",
                    ),
                    "candidateManifestRawSha256": aggregator.bytes_sha256(candidate),
                    "goldBindingManifestRawSha256": aggregator.bytes_sha256(binding),
                    "storyManifestRawSha256": aggregator.bytes_sha256(story),
                    "holdoutConsumptionReceiptRawSha256": aggregator.bytes_sha256(
                        _labels["holdout_consumption_receipt_bytes"]
                    ),
                    "registryBeforeRawSha256": aggregator.bytes_sha256(
                        _labels["registry_before_bytes"]
                    ),
                    "registryAfterRawSha256": aggregator.bytes_sha256(
                        _labels["registry_after_bytes"]
                    ),
                    "evaluationSessionClaimRawSha256": aggregator.bytes_sha256(
                        _labels["evaluation_session_claim_bytes"]
                    ),
                },
            }

        with mock.patch.object(
            aggregator,
            "_load_exact_validator",
            return_value=mock.Mock(validate_exact_inputs=validate_gold_exact),
        ):
            aggregator.validate_gold_state_score_ready_authority(
                Path("candidate.json"),
                Path("gold.json"),
                Path("holdout.json"),
                Path("registry-before.json"),
                Path("registry-after.json"),
                Path("session-claim.json"),
                candidate_manifest_raw_bytes=candidate_raw,
                gold_binding_raw_bytes=binding_raw,
                story_manifest_raw_bytes=story_raw,
                holdout_consumption_receipt_raw_bytes=holdout_raw,
                registry_before_raw_bytes=registry_before_raw,
                registry_after_raw_bytes=registry_after_raw,
                evaluation_session_claim_raw_bytes=session_claim_raw,
                require_score_ready=True,
            )
        self.assertEqual(
            captured,
            {"story": story_raw, "candidate": candidate_raw, "binding": binding_raw},
        )

    def test_shared_gold_validator_rejects_e09_geometry_mutation(self) -> None:
        """Aggregate gold readiness delegates to the full raw-bundle/E09 validator."""

        test_path = aggregator.NATIVE_DIRECTORY / "test-gold-state.py"
        spec = importlib.util.spec_from_file_location(
            "gridworks_native_gold_state_fixture_for_aggregate",
            test_path,
        )
        if spec is None or spec.loader is None:
            self.fail(f"could not load {test_path}")
        gold_tests = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(gold_tests)
        gold_tests.GoldStateContractTests.setUpClass()
        fixture_builder = gold_tests.GoldStateContractTests()
        valid_binding = fixture_builder.binding_fixture()
        changed_binding = copy.deepcopy(valid_binding)
        changed_binding["e09NorthBankTwoProcessWitness"][
            "postResumeDraftGeometrySha256"
        ] = gold_tests.sha("9")
        with tempfile.TemporaryDirectory() as raw:
            directory = Path(raw)
            binding_path = fixture_builder.write_binding(directory, changed_binding)
            changed_binding_raw = binding_path.read_bytes()
            fixture_builder.write_binding(directory, valid_binding)
            candidate_path = directory / "candidate-manifest.json"
            candidate_path.write_text("{}\n", encoding="utf-8")
            real_loader = aggregator._load_exact_validator

            class ExactByteContractStub:
                @staticmethod
                def validate_runtime_contract_bytes(*_args: Any, **kwargs: Any):
                    fields = {
                        "candidateManifestRawSha256": "candidate_manifest_bytes",
                        "goldBindingManifestRawSha256": "gold_binding_manifest_bytes",
                        "holdoutConsumptionReceiptRawSha256": (
                            "holdout_consumption_receipt_bytes"
                        ),
                        "registryBeforeRawSha256": "registry_before_bytes",
                        "registryAfterRawSha256": "registry_after_bytes",
                        "evaluationSessionClaimRawSha256": (
                            "evaluation_session_claim_bytes"
                        ),
                    }
                    return [], {
                        "observedRawSha256": {
                            field: aggregator.bytes_sha256(kwargs[input_field])
                            for field, input_field in fields.items()
                        },
                    }

            def load_gold_validator(path: Path, module_name: str):
                validator = real_loader(path, module_name)
                if path == aggregator.GOLD_STATE_VALIDATOR_PATH:
                    validator._load_contract_validator = (
                        lambda _path: ExactByteContractStub()
                    )
                return validator

            with mock.patch.object(
                aggregator,
                "_load_exact_validator",
                side_effect=load_gold_validator,
            ), self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "content-derived draft geometry/projection hashes",
            ):
                aggregator.validate_gold_state_score_ready_authority(
                    candidate_path,
                    binding_path,
                    directory / "holdout.json",
                    directory / "registry-before.json",
                    directory / "registry-after.json",
                    directory / "session-claim.json",
                    candidate_manifest_raw_bytes=candidate_path.read_bytes(),
                    gold_binding_raw_bytes=changed_binding_raw,
                    story_manifest_raw_bytes=b"{}\n",
                    holdout_consumption_receipt_raw_bytes=b"{}\n",
                    registry_before_raw_bytes=b"{}\n",
                    registry_after_raw_bytes=b"{}\n",
                    evaluation_session_claim_raw_bytes=b"{}\n",
                    require_score_ready=True,
                )

    def test_artifact_binding_order_is_not_candidate_controlled(self) -> None:
        bindings = [
            {
                "anonymousArtifactId": f"ARTIFACT-{letter}",
                "artifactKind": "COVERAGE" if letter == "D" else "COLD_ACTOR",
                "artifactSha256": identity_sha(f"artifact-{letter}"),
            }
            for letter in "ABCD"
        ]
        aggregator._validate_artifact_bindings(bindings)
        bindings[0], bindings[1] = bindings[1], bindings[0]
        with self.assertRaisesRegex(
            aggregator.ValidationFailure,
            "deterministic anonymized A/B/C/D order",
        ):
            aggregator._validate_artifact_bindings(bindings)

    def test_verifier_must_cover_every_judge_claim_with_real_cited_sources(self) -> None:
        """One invented SUPPORTED row cannot validate an otherwise complete panel."""

        with tempfile.TemporaryDirectory() as raw:
            directory = Path(raw)
            artifact_order = [f"ARTIFACT-{letter}" for letter in "ABCD"]
            artifacts_by_id: dict[str, dict[str, Any]] = {}
            for anonymous_id in artifact_order:
                artifact_id = f"frame-{anonymous_id[-1].lower()}"
                locator = f"frames/{artifact_id}.png"
                artifacts_by_id[anonymous_id] = {
                    "anonymousArtifactId": anonymous_id,
                    "sanitizedArtifactSha256": identity_sha(f"sanitized-{anonymous_id}"),
                    "traceRows": [{
                        "traceRowId": f"TRACE-{len(artifacts_by_id) + 1:04d}",
                        "checkpoint": "exact-checkpoint",
                        "citedVisibleSources": [{
                            "artifactId": artifact_id,
                            "locator": locator,
                        }],
                    }],
                    "mediaArtifacts": [{
                        "artifactId": artifact_id,
                        "kind": "FRAME",
                        "sha256": identity_sha(f"raw-{anonymous_id}"),
                        "mimeType": "image/png",
                        "locator": locator,
                    }],
                }
            claims = [f"Exact visible claim for {cell_id}." for cell_id in aggregator.ALL_CELLS]
            panel = {
                "panelSha256": identity_sha("direct-panel"),
                "judgments": [{
                    "artifactJudgments": [{
                        "anonymousArtifactId": "ARTIFACT-A",
                        "cells": [{
                            "strengthEvidence": [{
                                "checkpoint": "exact-checkpoint",
                                "artifact": "frame-a",
                                "observation": claim,
                            }],
                            "gapEvidence": [],
                        } for claim in claims],
                    }],
                }],
            }
            evidence = {
                "selfSha256": identity_sha("direct-evidence"),
                "value": {"artifactOrder": artifact_order},
                "artifactsById": artifacts_by_id,
                "bundleRootsById": {
                    anonymous_id: str(directory / anonymous_id)
                    for anonymous_id in artifact_order
                },
            }
            expected = aggregator._derive_verification_observations(panel, evidence)
            self.assertEqual(len(expected), len(aggregator.ALL_CELLS))
            candidate_manifest = {"candidateManifestSha256": identity_sha("direct-candidate")}
            holdout = {"selfSha256": identity_sha("direct-receipt")}
            gold = {"selfSha256": identity_sha("direct-gold")}
            sanitized_bundles = {
                anonymous_id: {
                    "anonymousArtifactId": anonymous_id,
                    "bundleId": f"bundle-{anonymous_id[-1].lower()}",
                    "artifactBundleSha256": identity_sha(
                        f"bundle-{anonymous_id}"
                    ),
                    "bundleRootSha256": identity_sha(f"root-{anonymous_id}"),
                    "canonicalBundleRoot": evidence["bundleRootsById"][anonymous_id],
                    "bundleRootPathTail": anonymous_id,
                }
                for anonymous_id in artifact_order
            }
            sanitized = {
                "selfSha256": identity_sha("direct-sanitized-manifest"),
                "rawSha256": identity_sha("direct-sanitized-manifest-raw"),
                "value": {"contentRootSha256": identity_sha("direct-content-root")},
                "bundlesById": sanitized_bundles,
            }
            verification = {
                "schemaVersion": aggregator.VERIFICATION_INPUT_SCHEMA,
                "protocol": aggregator.PROTOCOL,
                "verificationScope": "VISIBLE_OR_AUDIBLE_OBSERVATION_ONLY",
                "verificationInputSha256": identity_sha("verification-placeholder"),
                "candidateManifestSha256": candidate_manifest["candidateManifestSha256"],
                "holdoutConsumptionReceiptSha256": holdout["selfSha256"],
                "goldBindingManifestSha256": gold["selfSha256"],
                "evidenceSetSha256": evidence["selfSha256"],
                "sanitizedEvidenceBundleManifestSha256": sanitized["selfSha256"],
                "sanitizedEvidenceBundleManifestRawSha256": sanitized["rawSha256"],
                "sanitizedEvidenceContentRootSha256": sanitized["value"][
                    "contentRootSha256"
                ],
                "opaqueJudgePanelSha256": panel["panelSha256"],
                "artifactBundles": [sanitized_bundles[anonymous_id]
                    for anonymous_id in artifact_order],
                # The original defect accepted this one invented row for all 39 claims.
                "observations": [{
                    "observationId": "OBS-0001",
                    "claimType": "JUDGE_EVIDENCE",
                    "incidentKey": None,
                    "claim": "Invented blanket support.",
                    "citedSources": expected[0]["citedSources"],
                }],
            }
            verification["verificationInputSha256"] = aggregator.self_sha256(
                verification,
                "verificationInputSha256",
                "direct verification input",
            )
            raw_bytes = aggregator.canonical_json_bytes(verification) + b"\n"
            candidate = {
                "verificationInputSha256": verification["verificationInputSha256"],
                "expectedObservationIds": ["OBS-0001"],
            }
            evaluation = {"artifacts": {
                "verificationInputSha256": verification["verificationInputSha256"],
                "verificationInputRawSha256": aggregator.bytes_sha256(raw_bytes),
            }}
            with self.assertRaisesRegex(
                aggregator.ValidationFailure,
                "observation set/order/content/citations",
            ):
                aggregator.validate_verification_input_authority(
                    verification,
                    raw_bytes,
                    candidate,
                    candidate_manifest,
                    evaluation,
                    evidence,
                    panel,
                    holdout,
                    gold,
                    sanitized,
                )

    def test_selected_holdout_semantics_and_consumption_are_enforced(self) -> None:
        """A hash label alone cannot substitute for the selected holdout recipe."""

        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(
                Path(raw),
                recipe_id="HOLDOUT-01",
                labeler=constant_label("EXCELLENT"),
            )
            trace = copy.deepcopy(fixture["coverageTrace"])
            selected = selected_recipe("HOLDOUT-01")
            trace.update({
                "holdoutConsumptionReceiptSha256": fixture["holdoutReceipt"][
                    "holdoutConsumptionReceiptSha256"
                ],
                "holdoutRealization": {
                    "missionPrototypeBits": selected["missionPrototypeBits"],
                    "promiseBranchOrder": selected["promiseBranchOrder"],
                    "actorArtifactPermutation": selected["actorArtifactPermutation"],
                    "coverageArtifactOrder": selected["coverageArtifactOrder"],
                },
                "coveragePresentationEpisodeIds": list(reversed(EPISODE_IDS)),
                "goldBindingManifestSha256": fixture["goldBinding"][
                    "goldBindingManifestSha256"
                ],
                "coverageActionLedgerRawSha256": identity_sha("direct-coverage-ledger"),
                "recordingManifestRawSha256": identity_sha("direct-coverage-recording-raw"),
            })
            recipe = json.loads(
                (aggregator.NATIVE_DIRECTORY / "coverage-recipe.json").read_text()
            )
            actions_by_episode = {row["id"]: row["actions"] for row in recipe["episodes"]}
            for episode in trace["episodes"]:
                realized = aggregator._realized_coverage_actions(
                    episode["episodeId"],
                    actions_by_episode[episode["episodeId"]],
                    selected,
                )
                for action_index, (row, occurrence_id) in enumerate(
                    zip(episode["traceRows"], realized), start=1
                ):
                    row["actionOccurrenceId"] = occurrence_id
                    row["actionIndex"] = action_index
                    row.update(aggregator._expected_action_realization(
                        episode["episodeId"], occurrence_id, selected
                    ))
            episode = next(
                row
                for row in trace["episodes"]
                if row["episodeId"] == "E04-NORTH-BANK"
            )
            branch_actions = [
                row["actionOccurrenceId"]
                for row in episode["traceRows"]
                if "APPLY_" in row["actionOccurrenceId"]
            ]
            self.assertEqual(
                branch_actions,
                ["NORTH_BANK_APPLY_DEFER_BRANCH", "NORTH_BANK_APPLY_KEEP_BRANCH"],
            )
            self.assertEqual(
                selected_recipe("HOLDOUT-01")["promiseBranchOrder"],
                ["defer", "keep"],
            )
            # Reorder the realized branch blocks to KEEP-first while the selected
            # receipt remains HOLDOUT-01/DEFER-first.
            keep_first = copy.deepcopy(selected)
            keep_first["promiseBranchOrder"] = ["keep", "defer"]
            wrong = aggregator._realized_coverage_actions(
                episode["episodeId"],
                actions_by_episode[episode["episodeId"]],
                keep_first,
            )
            for row, occurrence_id in zip(episode["traceRows"], wrong):
                row["actionOccurrenceId"] = occurrence_id
                row.update(aggregator._expected_action_realization(
                    episode["episodeId"], occurrence_id, keep_first
                ))
            trace["coverageArtifactId"] = aggregator.self_sha256(
                trace, "coverageArtifactId", "wrong holdout coverage trace"
            )
            raw_bytes = aggregator.canonical_json_bytes(trace) + b"\n"
            evaluation = copy.deepcopy(fixture["evaluationRun"])
            evaluation["artifacts"].update({
                "coverageArtifactId": trace["coverageArtifactId"],
                "coverageTraceRawSha256": aggregator.bytes_sha256(raw_bytes),
                "coverageActionLedgerRawSha256": trace[
                    "coverageActionLedgerRawSha256"
                ],
                "coverageRecordingManifestSha256": trace["recordingManifestSha256"],
                "coverageRecordingManifestRawSha256": trace[
                    "recordingManifestRawSha256"
                ],
            })
            with self.assertRaisesRegex(aggregator.ProvenanceFailure, "branch/action order"):
                aggregator.validate_coverage_trace_authority(
                    trace,
                    raw_bytes,
                    fixture["candidateManifest"],
                    evaluation,
                    {"selfSha256": trace["holdoutConsumptionReceiptSha256"]},
                    {"selfSha256": trace["goldBindingManifestSha256"]},
                    {"selfSha256": trace["coverageActionLedgerSha256"]},
                )

    def test_required_cold_probes_and_actor_trace_are_enforced(self) -> None:
        """A fake single probe cannot stand in for the frozen cold concept set."""

        concept = json.loads(
            (aggregator.NATIVE_DIRECTORY / "concept-exposure-manifest.json").read_text()
        )
        required = [row["id"] for row in concept["probes"] if row["requiredForCold"]]
        self.assertEqual(len(required), 18)
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(
                Path(raw),
                labeler=constant_label("EXCELLENT"),
            )
            self.assertEqual(
                [row["probeId"] for row in fixture["actorObservations"][0]["firstUseRecords"]],
                ["PX01-FIXTURE"],
            )
            actor_rows = [{"value": row} for row in fixture["actorObservations"]]
            with self.assertRaisesRegex(
                aggregator.ValidationFailure,
                "required cold concept probe|required cold probes",
            ):
                aggregator.validate_required_cold_probes(actor_rows, fixture["candidate"])

    def test_required_cold_probe_prefix_uses_explicit_chronological_order(self) -> None:
        concept = json.loads(
            (aggregator.NATIVE_DIRECTORY / "concept-exposure-manifest.json").read_text()
        )
        by_id = {row["id"]: row for row in concept["probes"]}
        chronological = concept["coldProbeOrder"]
        manifest_row_order = [
            row["id"] for row in concept["probes"] if row["requiredForCold"]
        ]
        self.assertNotEqual(chronological, manifest_row_order)
        cold_recipe = json.loads(aggregator.COLD_RECIPE_PATH.read_text())
        checkpoint_rank = {
            (row["episode"], row["checkpoint"]): row["sequenceOrdinal"]
            for row in cold_recipe["checkpointSequence"]
        }

        def actor_with(probe_ids: list[str]) -> dict[str, Any]:
            records = [
                {
                    "checkpointOrdinal": index,
                    "probeId": probe_id,
                    "episode": by_id[probe_id]["firstEpisode"],
                    "checkpoint": by_id[probe_id]["firstCheckpoint"],
                }
                for index, probe_id in enumerate(probe_ids, start=1)
            ]
            return {"value": {
                "firstUseRecords": records,
                "terminalState": "COMPLETED",
                "terminalIncidentKey": None,
                "checkpoints": [
                    {
                        "episode": record["episode"],
                        "checkpoint": record["checkpoint"],
                        "recipeCheckpointSequenceOrdinal": checkpoint_rank[
                            (record["episode"], record["checkpoint"])
                        ],
                    }
                    for record in records
                ],
            }}

        candidate = {"notReachedByProductCellIds": []}
        aggregator.validate_required_cold_probes(
            [actor_with(chronological) for _ in range(3)],
            candidate,
        )
        with self.assertRaisesRegex(
            aggregator.ProvenanceFailure,
            "exact manifest IDs/order",
        ):
            aggregator.validate_required_cold_probes(
                [actor_with(manifest_row_order) for _ in range(3)],
                candidate,
            )

        stalled_after_finale = actor_with(chronological[:2])
        stalled_after_finale["value"]["terminalState"] = "PLAYER_STALLED"
        stalled_after_finale["value"]["terminalIncidentKey"] = (
            "E08-FINALE/finale-heat/OPERATIONS/UX_STALL"
        )
        stalled_after_finale["value"]["checkpoints"].append({
            "episode": "E08-FINALE",
            "checkpoint": "finale-heat",
            "recipeCheckpointSequenceOrdinal": 37,
        })
        with self.assertRaisesRegex(
            aggregator.ProvenanceFailure,
            "advanced to or beyond the first omitted",
        ):
            aggregator.validate_required_cold_probes(
                [copy.deepcopy(stalled_after_finale) for _ in range(3)],
                candidate,
            )

    def test_all_strong_is_85_and_fails_target(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw), labeler=constant_label("STRONG"))
            result = aggregate_fixture(fixture)
            self.assertEqual(result["rawCommercialUX"], 85.0)
            self.assertEqual(result["rawSpread"], 0.0)
            self.assertEqual(result["disagreementPenalty"], 0.0)
            self.assertEqual(result["commercialUXProxy"], 85.0)
            self.assertEqual(result["status"], "FAIL_UX")

    def test_some_excellent_cells_make_87_plus_possible(self) -> None:
        excellent_cells = {"J1", "T1", "H1", "I1", "C1", "A1", "P1", "V1", "R1", "L1", "K1"}

        def labeler(_judge: int, _kind: str, _artifact: str, cell: str) -> str:
            return "EXCELLENT" if cell in excellent_cells else "STRONG"

        with tempfile.TemporaryDirectory() as raw:
            result = aggregate_fixture(make_fixture(Path(raw), labeler=labeler))
            self.assertGreaterEqual(result["commercialUXProxy"], 87.0)
            self.assertEqual(result["status"], "PASS")

    def test_raw_90_is_capped_at_79(self) -> None:
        raw_90_labels = {
            **{cell: "EXCELLENT" for cell in (
                "J1", "J2", "J3", "J4", "T1", "T2", "T3",
                "H1", "H2", "H3", "H4", "I1", "I2", "I3",
                "C1", "C2", "C3", "C4", "C5", "A1", "A2", "A3", "A4",
                "P1", "P2", "P3", "V1", "V2", "V3", "V4",
            )},
            "R1": "WEAK",
            "R2": "BROKEN",
            "R3": "WEAK",
            "L1": "BROKEN",
            "L2": "BROKEN",
            "L3": "WEAK",
            "K1": "WEAK",
            "K2": "BROKEN",
            "K3": "STRONG",
        }

        def labeler(_judge: int, _kind: str, _artifact: str, cell: str) -> str:
            return raw_90_labels[cell]

        incident = make_incident(
            "CONFUSION",
            79,
            ["ARTIFACT-A", "ARTIFACT-B"],
            checkpoints=["E04/approval", "E05/approval"],
        )
        with tempfile.TemporaryDirectory() as raw:
            result = aggregate_fixture(
                make_fixture(Path(raw), labeler=labeler, incidents=[incident])
            )
            self.assertEqual(result["rawCommercialUX"], 90.0)
            self.assertEqual(result["activeCap"], 79)
            self.assertEqual(result["commercialUXProxy"], 79.0)
            self.assertEqual(result["status"], "FAIL_UX")

    def test_required_cell_below_70_fails_even_when_overall_exceeds_87(self) -> None:
        def labeler(_judge: int, _kind: str, _artifact: str, cell: str) -> str:
            return "WEAK" if cell == "L3" else "EXCELLENT"

        with tempfile.TemporaryDirectory() as raw:
            result = aggregate_fixture(make_fixture(Path(raw), labeler=labeler))
            self.assertGreater(result["commercialUXProxy"], 87.0)
            self.assertEqual(result["cellScores"]["L3"]["finalCellScore"], 40.0)
            self.assertEqual(result["status"], "FAIL_UX")

    def test_hard_gate_failure_wins_over_all_excellent(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(
                Path(raw),
                labeler=constant_label("EXCELLENT"),
                gate_overrides={"HG04-BUILD": ("FAIL", "BUILD_FAILED")},
            )
            result = aggregate_fixture(fixture)
            self.assertEqual(result["commercialUXProxy"], 100.0)
            self.assertEqual(result["status"], "FAIL_HARD_GATE")
            self.assertEqual(result["verdict"], "FAIL_HARD_GATE")

    def test_exact_target_boundary(self) -> None:
        category_scores = {"fixture": {"meetsMinimum": True}}
        cell_scores = {"fixture": {"finalCellScore": 70.0}}
        difference = base_difference_report()
        self.assertTrue(
            aggregator.official_ux_passes(
                87.0,
                category_scores,
                cell_scores,
                100,
                [],
                difference,
            )
        )
        self.assertFalse(
            aggregator.official_ux_passes(
                86.99,
                category_scores,
                cell_scores,
                100,
                [],
                difference,
            )
        )

    def test_formative_100_is_scored_but_never_promoted_to_pass(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(
                Path(raw),
                recipe_id="FORMATIVE-01",
                labeler=constant_label("EXCELLENT"),
            )
            result = aggregate_fixture(fixture)
            self.assertEqual(result["commercialUXProxy"], 100.0)
            self.assertEqual(result["status"], "SCORED_FORMATIVE")
            self.assertIsNone(result["verdict"])
            self.assertFalse(result["officialCommercialUX"])
            self.assertNotIn("textPlanProxy", result)

    def test_checked_in_scorecard_schema_rejects_formative_promotion(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            result = aggregate_fixture(
                make_fixture(
                    Path(raw),
                    recipe_id="FORMATIVE-01",
                    labeler=constant_label("EXCELLENT"),
                )
            )
            promoted = copy.deepcopy(result)
            promoted["status"] = "PASS"
            promoted["verdict"] = "PASS"
            promoted["officialCommercialUX"] = True
            with self.assertRaises(aggregator.ValidationFailure):
                aggregator.validate_native_scorecard_schema(promoted)

    def test_text_plan_proxy_cannot_enter_native_candidate_or_output(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw))
            candidate = copy.deepcopy(fixture["candidate"])
            candidate["textPlanProxy"] = 90
            with self.assertRaises(aggregator.ValidationFailure):
                aggregator.validate_candidate(candidate)
            result = aggregate_fixture(fixture)
            self.assertNotIn("textPlanProxy", result)
            self.assertEqual(result["metric"], "CommercialUXProxy")

    def _assert_judgment_schema_blocked(self, mutator: Callable[[dict[str, Any]], None]) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw))
            judgment = copy.deepcopy(fixture["judgments"][0])
            mutator(judgment)
            write_json(fixture["judgmentPaths"][0], judgment)
            result = aggregate_fixture(fixture, retry_reason="SCHEMA")
            self.assertEqual(result["status"], "BLOCKED_JUDGE_SCHEMA")
            self.assertIsNone(result["commercialUXProxy"])

    def test_duplicate_cell_is_blocked_schema(self) -> None:
        self._assert_judgment_schema_blocked(
            lambda judgment: judgment["artifactJudgments"][0]["cells"].__setitem__(
                1,
                copy.deepcopy(judgment["artifactJudgments"][0]["cells"][0]),
            )
        )

    def test_missing_cell_is_blocked_schema(self) -> None:
        self._assert_judgment_schema_blocked(
            lambda judgment: judgment["artifactJudgments"][0]["cells"].pop()
        )

    def test_low_confidence_is_blocked_schema(self) -> None:
        self._assert_judgment_schema_blocked(
            lambda judgment: judgment["artifactJudgments"][0]["cells"][0].__setitem__(
                "confidence", "LOW"
            )
        )

    def test_judgment_provenance_mismatch_is_blocked_schema(self) -> None:
        self._assert_judgment_schema_blocked(
            lambda judgment: judgment.__setitem__(
                "evidenceSetSha256", identity_sha("wrong-evidence-set")
            )
        )

    def test_checked_in_contract_hash_mismatch_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw))
            wrong_prompt = identity_sha("wrong-prompt")
            fixture["candidateManifest"]["contractHashes"]["nativeJudgePromptSha256"] = (
                wrong_prompt
            )
            manifest_self, manifest_raw = write_self_hashed_json(
                fixture["candidateManifestPath"],
                fixture["candidateManifest"],
                "candidateManifestSha256",
            )
            fixture["candidate"]["provenance"].update(
                {
                    "candidateManifestSha256": manifest_self,
                    "candidateManifestRawSha256": manifest_raw,
                    "promptTemplateSha256": wrong_prompt,
                }
            )
            write_json(fixture["candidatePath"], fixture["candidate"])
            with self.assertRaises(aggregator.ProvenanceFailure):
                aggregate_fixture(fixture, refresh_authorities=False)

    def test_native_aggregator_raw_hash_cannot_be_forged(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw))
            forged = identity_sha("forged-native-aggregator")
            fixture["candidateManifest"]["contractHashes"]["nativeAggregatorSha256"] = forged
            manifest_self, manifest_raw = write_self_hashed_json(
                fixture["candidateManifestPath"],
                fixture["candidateManifest"],
                "candidateManifestSha256",
            )
            fixture["candidate"]["provenance"].update(
                {
                    "candidateManifestSha256": manifest_self,
                    "candidateManifestRawSha256": manifest_raw,
                    "nativeAggregatorSha256": forged,
                }
            )
            write_json(fixture["candidatePath"], fixture["candidate"])
            with self.assertRaises(aggregator.ProvenanceFailure):
                aggregate_fixture(fixture, refresh_authorities=False)

    def test_partial_evidence_blocks_all_scoring(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(
                Path(raw),
                verifier_verdicts={"OBS-0001": "PARTIAL"},
            )
            result = aggregate_fixture(fixture)
            self.assertEqual(result["status"], "BLOCKED_EVIDENCE_VERIFICATION")
            self.assertIsNone(result["commercialUXProxy"])

    def test_missing_verifier_observation_blocks_all_scoring(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(
                Path(raw),
                expected_observation_ids=["OBS-0001", "OBS-0002"],
                verifier_observation_ids=["OBS-0001"],
            )
            result = aggregate_fixture(fixture)
            self.assertEqual(result["status"], "BLOCKED_EVIDENCE_VERIFICATION")

    def test_both_lane_cell_uses_lower_lane_score(self) -> None:
        def labeler(_judge: int, kind: str, _artifact: str, cell: str) -> str:
            if cell == "T1":
                return "EXCELLENT" if kind == "COLD_ACTOR" else "STRONG"
            return "EXCELLENT"

        with tempfile.TemporaryDirectory() as raw:
            result = aggregate_fixture(make_fixture(Path(raw), labeler=labeler))
            row = result["cellScores"]["T1"]
            self.assertEqual(row["coldCellScore"], 100.0)
            self.assertEqual(row["coverageCellScore"], 85.0)
            self.assertEqual(row["finalCellScore"], 85.0)

    def test_spread_reduction_matches_cell_category_global_formula(self) -> None:
        def labeler(judge: int, kind: str, artifact: str, cell: str) -> str:
            if (
                kind == "COLD_ACTOR"
                and artifact in {"ARTIFACT-A", "ARTIFACT-B"}
                and cell == "J1"
                and judge == 0
            ):
                return "EXCELLENT"
            return "STRONG"

        with tempfile.TemporaryDirectory() as raw:
            result = aggregate_fixture(make_fixture(Path(raw), labeler=labeler))
            self.assertEqual(result["cellScores"]["J1"]["coldCellSpread"], 7.5)
            self.assertEqual(result["categoryScores"]["journey"]["spread"], 1.5)
            self.assertEqual(result["rawSpread"], 0.18)
            self.assertEqual(result["disagreementPenalty"], 0.036)
            self.assertEqual(result["commercialUXProxy"], 84.964)

    def test_category_floor_can_fail_above_87(self) -> None:
        def labeler(_judge: int, _kind: str, _artifact: str, cell: str) -> str:
            return "SERVICEABLE" if cell.startswith("J") else "EXCELLENT"

        with tempfile.TemporaryDirectory() as raw:
            result = aggregate_fixture(make_fixture(Path(raw), labeler=labeler))
            self.assertGreater(result["commercialUXProxy"], 87.0)
            self.assertFalse(result["categoryScores"]["journey"]["meetsMinimum"])
            self.assertEqual(result["status"], "FAIL_UX")

    def test_qualification_block_precedes_scoring(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            result = aggregate_fixture(
                make_fixture(
                    Path(raw),
                    labeler=constant_label("EXCELLENT"),
                    qualification_status="BLOCKED_JUDGE_QUALIFICATION",
                ),
            )
            self.assertEqual(result["status"], "BLOCKED_JUDGE_QUALIFICATION")
            self.assertIsNone(result["commercialUXProxy"])

    def test_operational_unavailable_has_deterministic_blocker_precedence(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            result = aggregate_fixture(
                make_fixture(
                    Path(raw),
                    operational_blocker="BLOCKED_JUDGE_UNAVAILABLE",
                    qualification_status="BLOCKED_JUDGE_QUALIFICATION",
                ),
            )
            self.assertEqual(result["status"], "BLOCKED_JUDGE_UNAVAILABLE")

    def test_active_caps_69_and_49_and_not_reached_zero(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            cap69 = make_fixture(
                Path(raw) / "cap69",
                labeler=constant_label("EXCELLENT"),
                incidents=[
                    make_incident(
                        "RECOVERY_FRICTION",
                        69,
                        ["ARTIFACT-A", "ARTIFACT-B"],
                    )
                ],
            )
            result69 = aggregate_fixture(cap69)
            self.assertEqual(result69["activeCap"], 69)
            self.assertEqual(result69["commercialUXProxy"], 69.0)

            cap49 = make_fixture(
                Path(raw) / "cap49",
                labeler=constant_label("EXCELLENT"),
                incidents=[
                    make_incident(
                        "UX_STALL",
                        49,
                        ["ARTIFACT-A", "ARTIFACT-B"],
                        critical=True,
                    )
                ],
                not_reached=["J4"],
            )
            result49 = aggregate_fixture(cap49)
            self.assertEqual(result49["activeCap"], 49)
            self.assertEqual(result49["cellScores"]["J4"]["state"], "NOT_REACHED_BY_PRODUCT")
            self.assertEqual(result49["cellScores"]["J4"]["finalCellScore"], 0.0)
            self.assertLessEqual(result49["commercialUXProxy"], 49.0)

    def test_blocked_hard_gate_maps_harness_failure(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            result = aggregate_fixture(
                make_fixture(
                    Path(raw),
                    gate_overrides={"HG03-REACHABILITY": ("BLOCKED", "HARNESS_INPUT_BLOCKED")},
                    score_bearing_ready=False,
                )
            )
            self.assertEqual(result["status"], "BLOCKED_HARNESS")

    def test_initial_cold_instability_requires_full_fresh_replacement(self) -> None:
        def unstable_labeler(judge: int, kind: str, artifact: str, cell: str) -> str:
            if kind == "COLD_ACTOR" and artifact == "ARTIFACT-A" and cell == "J1":
                return ("EXCELLENT", "SERVICEABLE", "STRONG")[judge]
            return "STRONG"

        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            initial = make_fixture(
                root / "initial",
                labeler=unstable_labeler,
                cold_suffix="initial",
                coverage_suffix="shared",
                panel_suffix="initial",
            )
            initial_result = aggregate_fixture(initial)
            initial_path = initial["directory"] / "scorecard.json"
            self.assertEqual(initial_result["status"], "RERUN_REQUIRED_COLD_INSTABILITY")
            self.assertEqual(initial_result["replacementRequiredLanes"], ["COLD-JOURNEY"])
            self.assertIsNone(initial_result["commercialUXProxy"])

            replacement = make_fixture(
                root / "replacement",
                labeler=constant_label("STRONG"),
                cold_suffix="replacement-1",
                coverage_suffix="shared",
                panel_suffix="replacement-1",
            )
            result = aggregate_fixture(
                replacement,
                replacement_for=initial_path,
            )
            self.assertEqual(result["panelKind"], "REPLACEMENT")
            self.assertEqual(result["replacementForPanelSha256"], initial_result["judgePanelSha256"])
            self.assertEqual(result["status"], "FAIL_UX")
            receipt_path = Path(result["replacementReceiptPath"])
            self.assertTrue(receipt_path.exists())
            replacement_receipt = json.loads(receipt_path.read_bytes())
            self.assertEqual(
                replacement_receipt["initialPanelFinalizationSealPath"],
                str((initial["directory"] / "panel-finalization-seal.json").resolve()),
            )
            self.assertEqual(
                aggregator.file_sha256(receipt_path, "replacement receipt"),
                result["replacementReceiptSha256"],
            )

            second = make_fixture(
                root / "replacement-2",
                labeler=constant_label("STRONG"),
                cold_suffix="replacement-2",
                coverage_suffix="shared",
                panel_suffix="replacement-2",
            )
            with self.assertRaisesRegex(aggregator.ValidationFailure, "already consumed"):
                aggregate_fixture(second, replacement_for=initial_path)

    def test_replacement_instability_is_final_block_and_consumes_receipt(self) -> None:
        def unstable_labeler(judge: int, kind: str, artifact: str, cell: str) -> str:
            if kind == "COLD_ACTOR" and artifact == "ARTIFACT-A" and cell == "J1":
                return ("EXCELLENT", "SERVICEABLE", "STRONG")[judge]
            return "STRONG"

        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            initial = make_fixture(
                root / "initial",
                labeler=unstable_labeler,
                cold_suffix="initial",
                coverage_suffix="shared",
                panel_suffix="initial",
            )
            aggregate_fixture(initial)
            initial_path = initial["directory"] / "scorecard.json"
            replacement = make_fixture(
                root / "replacement",
                labeler=unstable_labeler,
                cold_suffix="replacement",
                coverage_suffix="shared",
                panel_suffix="replacement",
            )
            result = aggregate_fixture(replacement, replacement_for=initial_path)
            self.assertEqual(result["status"], "BLOCKED_JUDGE_INSTABILITY")
            self.assertIsNone(result["commercialUXProxy"])
            self.assertTrue(Path(result["replacementReceiptPath"]).exists())

    def test_actor_ordinal_range_two_requests_replacement(self) -> None:
        def actor_range_labeler(_judge: int, kind: str, artifact: str, cell: str) -> str:
            if kind == "COLD_ACTOR" and cell == "J1":
                return {
                    "ARTIFACT-A": "EXCELLENT",
                    "ARTIFACT-B": "STRONG",
                    "ARTIFACT-C": "SERVICEABLE",
                }[artifact]
            return "STRONG"

        with tempfile.TemporaryDirectory() as raw:
            result = aggregate_fixture(make_fixture(Path(raw), labeler=actor_range_labeler))
            self.assertEqual(result["status"], "RERUN_REQUIRED_COLD_INSTABILITY")
            self.assertTrue(result["laneInputs"]["J1"]["cold"]["unstable"])
            self.assertEqual(result["laneInputs"]["J1"]["cold"]["actorOrdinalRange"], 2)

    def test_coverage_instability_requests_coverage_lane_replacement(self) -> None:
        def labeler(judge: int, kind: str, _artifact: str, cell: str) -> str:
            if kind == "COVERAGE" and cell == "V1":
                return ("EXCELLENT", "SERVICEABLE", "STRONG")[judge]
            return "STRONG"

        with tempfile.TemporaryDirectory() as raw:
            result = aggregate_fixture(make_fixture(Path(raw), labeler=labeler))
            self.assertEqual(result["status"], "RERUN_REQUIRED_COVERAGE_INSTABILITY")
            self.assertEqual(result["replacementRequiredLanes"], ["COVERAGE-JOURNEY"])

    def test_candidate_manifest_phase_recipe_raw_and_self_hash_are_authoritative(self) -> None:
        cases = ("phase", "recipe", "raw", "self")
        for case in cases:
            with self.subTest(case=case), tempfile.TemporaryDirectory() as raw:
                fixture = make_fixture(Path(raw))
                manifest = fixture["candidateManifest"]
                if case == "phase":
                    formative = selected_recipe("FORMATIVE-01")
                    manifest["evaluationPhase"] = "FORMATIVE"
                    manifest["officialCommercialUX"] = False
                    manifest["recipes"]["selectedRecipeId"] = "FORMATIVE-01"
                    manifest["recipes"]["selectedRecipeSha256"] = aggregator.canonical_sha256(
                        formative
                    )
                elif case == "recipe":
                    alternate = selected_recipe("HOLDOUT-02")
                    manifest["recipes"]["selectedRecipeId"] = "HOLDOUT-02"
                    manifest["recipes"]["selectedRecipeSha256"] = aggregator.canonical_sha256(
                        alternate
                    )
                elif case == "raw":
                    fixture["candidateManifestPath"].write_bytes(
                        aggregator.canonical_json_bytes(manifest) + b"\n"
                    )
                    with self.assertRaises(aggregator.ProvenanceFailure):
                        aggregate_fixture(fixture, refresh_authorities=False)
                    continue
                else:
                    manifest["candidateManifestSha256"] = identity_sha("forged-self-hash")
                    raw_sha = write_json(fixture["candidateManifestPath"], manifest)
                    fixture["candidate"]["provenance"]["candidateManifestSha256"] = manifest[
                        "candidateManifestSha256"
                    ]
                    fixture["candidate"]["provenance"]["candidateManifestRawSha256"] = raw_sha
                    write_json(fixture["candidatePath"], fixture["candidate"])
                    with self.assertRaises(aggregator.ProvenanceFailure):
                        aggregate_fixture(fixture, refresh_authorities=False)
                    continue
                self_sha, raw_sha = write_self_hashed_json(
                    fixture["candidateManifestPath"],
                    manifest,
                    "candidateManifestSha256",
                )
                fixture["candidate"]["provenance"]["candidateManifestSha256"] = self_sha
                fixture["candidate"]["provenance"]["candidateManifestRawSha256"] = raw_sha
                write_json(fixture["candidatePath"], fixture["candidate"])
                with self.assertRaises(aggregator.ProvenanceFailure):
                    aggregate_fixture(fixture, refresh_authorities=False)

    def test_qualification_receipt_is_the_only_status_authority(self) -> None:
        self.assertNotIn(
            "qualification_status",
            inspect.signature(aggregator.aggregate_to_path).parameters,
        )
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(
                Path(raw),
                labeler=constant_label("EXCELLENT"),
                qualification_status="BLOCKED_JUDGE_QUALIFICATION",
            )
            result = aggregate_fixture(fixture)
            self.assertEqual(result["status"], "BLOCKED_JUDGE_QUALIFICATION")
            self.assertIsNone(result["commercialUXProxy"])

    def test_qualification_attempt_sequence_cannot_fake_pass(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw))
            receipt = fixture["qualificationReceipt"]
            receipt["attempts"][0]["slots"][0].update(
                {
                    "exactCount": 18,
                    "excellentAndBrokenAllExact": False,
                    "status": "FAIL_BAND",
                }
            )
            receipt["attempts"][0]["status"] = "FAIL"
            self_sha, raw_sha = write_self_hashed_json(
                fixture["qualificationReceiptPath"],
                receipt,
                "qualificationReceiptSha256",
            )
            fixture["candidate"]["provenance"]["qualificationReceiptSha256"] = self_sha
            fixture["candidate"]["provenance"]["qualificationReceiptRawSha256"] = raw_sha
            write_json(fixture["candidatePath"], fixture["candidate"])
            with self.assertRaises(aggregator.ValidationFailure):
                aggregate_fixture(fixture, refresh_authorities=False)

    def test_actor_authority_is_order_independent_but_pair_exact(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw))
            inputs, evaluation, responses = actor_inputs_for_authority_test(
                fixture,
                copy.deepcopy(fixture["actorObservations"]),
            )
            rows = validate_synthetic_actor_observation_authorities(
                list(reversed(inputs)),
                evaluation,
                responses,
            )
            self.assertEqual(len(rows), 3)
            evaluation["terminalStates"][0]["actorArtifactId"], evaluation["terminalStates"][1][
                "actorArtifactId"
            ] = (
                evaluation["terminalStates"][1]["actorArtifactId"],
                evaluation["terminalStates"][0]["actorArtifactId"],
            )
            with self.assertRaisesRegex(aggregator.ProvenanceFailure, "exactly map"):
                validate_synthetic_actor_observation_authorities(
                    inputs,
                    evaluation,
                    responses,
                )

    def test_actor_terminal_state_and_incident_key_are_exact(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw))
            inputs, evaluation, responses = actor_inputs_for_authority_test(
                fixture,
                copy.deepcopy(fixture["actorObservations"]),
            )
            evaluation["terminalStates"][0]["state"] = "PLAYER_STALLED"
            evaluation["terminalStates"][0]["terminalIncidentKey"] = (
                "FIRST_LIGHT/NONE/OPERATIONS/UX_STALL"
            )
            with self.assertRaisesRegex(aggregator.ProvenanceFailure, "terminal state mismatch"):
                validate_synthetic_actor_observation_authorities(
                    inputs,
                    evaluation,
                    responses,
                )

    def test_actor_action_index_and_incident_checkpoint_mapping_are_exact(self) -> None:
        recovery = make_incident("RECOVERY_FRICTION", 69, ["ARTIFACT-A"])
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw), incidents=[recovery])
            observations = copy.deepcopy(fixture["actorObservations"])
            observations[0]["actionLedger"][0]["actionIndex"] = 2
            inputs, evaluation, responses = actor_inputs_for_authority_test(
                fixture,
                observations,
            )
            with self.assertRaisesRegex(aggregator.ProvenanceFailure, "exactly 1..N"):
                validate_synthetic_actor_observation_authorities(
                    inputs,
                    evaluation,
                    responses,
                )

            observations = copy.deepcopy(fixture["actorObservations"])
            cited_index = observations[0]["incidents"][0]["actionIndexes"][0]
            observations[0]["actionLedger"][cited_index - 1]["checkpoint"] = "outside-checkpoint"
            inputs, evaluation, responses = actor_inputs_for_authority_test(
                fixture,
                observations,
            )
            with self.assertRaisesRegex(aggregator.ProvenanceFailure, "outside its cited"):
                validate_synthetic_actor_observation_authorities(
                    inputs,
                    evaluation,
                    responses,
                )

            observations = copy.deepcopy(fixture["actorObservations"])
            observations[0]["checkpoints"][0]["progressStateSha256"] = identity_sha(
                "fabricated-checkpoint-progress"
            )
            inputs, evaluation, responses = actor_inputs_for_authority_test(
                fixture,
                observations,
            )
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "progress state is not the cited action post-state",
            ):
                validate_synthetic_actor_observation_authorities(
                    inputs,
                    evaluation,
                    responses,
                )

            observations = copy.deepcopy(fixture["actorObservations"])
            observations[0]["approvalRecords"][0]["checkpointOrdinal"] = 2
            observations[0]["approvalRecords"][0]["checkpoint"] = (
                "first-energized-path"
            )
            observations[0]["approvalRecords"][1]["checkpointOrdinal"] = 1
            observations[0]["approvalRecords"][1]["checkpoint"] = "first-operations"
            inputs, evaluation, responses = actor_inputs_for_authority_test(
                fixture,
                observations,
            )
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "approvalRecords must follow checkpoint chronology",
            ):
                validate_synthetic_actor_observation_authorities(
                    inputs,
                    evaluation,
                    responses,
                )

    def test_cold_actor_response_semantics_are_exact_bound_to_observation(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw))
            observations = copy.deepcopy(fixture["actorObservations"])
            inputs, evaluation, responses = actor_inputs_for_authority_test(
                fixture,
                observations,
            )
            mutated = copy.deepcopy(responses[0]["value"])
            mutated["firstUseRecords"][0]["currentGoal"] = (
                "A different goal was shown to the recorder."
            )
            mutated["coldActorResponseSha256"] = aggregator.self_sha256(
                mutated,
                "coldActorResponseSha256",
                "mutated cold actor response",
            )
            mutated_raw = (
                json.dumps(mutated, ensure_ascii=False, indent=2, sort_keys=True).encode(
                    "utf-8"
                )
                + b"\n"
            )
            mutated_raw_sha = aggregator.bytes_sha256(mutated_raw)
            responses[0] = {
                "value": mutated,
                "selfSha256": mutated["coldActorResponseSha256"],
                "rawSha256": mutated_raw_sha,
                "slot": 0,
            }
            evaluation["artifacts"]["coldActorResponseSha256"][0] = mutated[
                "coldActorResponseSha256"
            ]
            evaluation["artifacts"]["coldActorResponseRawSha256"][0] = mutated_raw_sha
            observations[0]["coldActorResponseSha256"] = mutated[
                "coldActorResponseSha256"
            ]
            observations[0]["coldActorResponseRawSha256"] = mutated_raw_sha
            rebound_raw = (
                json.dumps(
                    observations[0],
                    ensure_ascii=False,
                    indent=2,
                    sort_keys=True,
                ).encode("utf-8")
                + b"\n"
            )
            inputs[0] = (observations[0], rebound_raw)
            rebound_raw_sha = aggregator.bytes_sha256(rebound_raw)
            evaluation["artifacts"]["actorObservationRawSha256"][0] = rebound_raw_sha
            evaluation["terminalStates"][0]["actorObservationRawSha256"] = (
                rebound_raw_sha
            )
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "semantic projection differs",
            ):
                validate_synthetic_actor_observation_authorities(
                    inputs,
                    evaluation,
                    responses,
                )

    def test_actor_action_ledger_is_observation_derived(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw))
            inputs, evaluation, responses = actor_inputs_for_authority_test(
                fixture,
                copy.deepcopy(fixture["actorObservations"]),
            )
            actor = validate_synthetic_actor_observation_authorities(
                inputs,
                evaluation,
                responses,
            )[0]
            process_tree_id = "actor-ledger-process-tree"
            observation = actor["value"]
            action_ledger = {
                "schemaVersion": aggregator.ACTOR_ACTION_LEDGER_SCHEMA,
                "protocol": aggregator.PROTOCOL,
                "candidateManifestSha256": fixture["candidateManifest"][
                    "candidateManifestSha256"
                ],
                "coldActorResponseSha256": actor["coldActorResponse"][
                    "selfSha256"
                ],
                "actorRunId": observation["actorRunId"],
                "processTreeId": process_tree_id,
                "actionCount": len(observation["actionLedger"]),
                "checkpointCount": len(observation["checkpoints"]),
                "actions": copy.deepcopy(observation["actionLedger"]),
                "checkpointPostStates": [
                    {
                        "checkpointOrdinal": checkpoint["ordinal"],
                        "recipeCheckpointSequenceOrdinal": checkpoint[
                            "recipeCheckpointSequenceOrdinal"
                        ],
                        "appActiveActionIndex": checkpoint[
                            "appActiveActionIndex"
                        ],
                        "progressStateSha256": checkpoint["progressStateSha256"],
                        "actionPostStateSha256": observation["actionLedger"][
                            checkpoint["appActiveActionIndex"] - 1
                        ]["postStateSha256"],
                    }
                    for checkpoint in observation["checkpoints"]
                ],
                "projectionRule": (
                    "ACTIONS_EXACT_OBSERVATION_ACTION_LEDGER_AND_CHECKPOINT_"
                    "PROGRESS_EQUALS_INDEXED_POST_STATE"
                ),
            }
            aggregator.validate_actor_action_ledger_authority(
                action_ledger,
                fixture["candidateManifest"],
                actor,
                process_tree_id,
            )
            fabricated = copy.deepcopy(action_ledger)
            fabricated["actions"][0]["visibleFeedback"] = (
                "Unrelated recorder-owned ledger content."
            )
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "not the exact observation",
            ):
                aggregator.validate_actor_action_ledger_authority(
                    fabricated,
                    fixture["candidateManifest"],
                    actor,
                    process_tree_id,
                )

    def test_recovery_severity_is_derived_per_actor_without_cross_actor_sum(self) -> None:
        recovery = make_incident(
            "RECOVERY_FRICTION",
            69,
            ["ARTIFACT-A", "ARTIFACT-B"],
        )
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw), incidents=[recovery])
            observations = copy.deepcopy(fixture["actorObservations"])
            for actor_index in (0, 1):
                observations[actor_index]["actionLedger"].pop()
                observations[actor_index]["incidents"][0]["actionIndexes"].pop()
                observations[actor_index]["incidents"][0]["severity"] = "LOCAL"
            inputs, evaluation, responses = actor_inputs_for_authority_test(
                fixture,
                observations,
            )
            rows = validate_synthetic_actor_observation_authorities(
                inputs,
                evaluation,
                responses,
            )
            self.assertEqual(rows[0]["severeIncidentKeys"], [])
            self.assertEqual(rows[1]["severeIncidentKeys"], [])

            severe_fixture = make_fixture(Path(raw) / "severe", incidents=[make_incident(
                "RECOVERY_FRICTION", 69, ["ARTIFACT-A"]
            )])
            severe_inputs, severe_evaluation, severe_responses = actor_inputs_for_authority_test(
                severe_fixture,
                copy.deepcopy(severe_fixture["actorObservations"]),
            )
            severe_rows = validate_synthetic_actor_observation_authorities(
                severe_inputs,
                severe_evaluation,
                severe_responses,
            )
            self.assertEqual(severe_rows[0]["severeIncidentKeys"], [recovery["incidentKey"]])

    def test_terminal_disagreement_requires_cold_replacement(self) -> None:
        stall = make_incident("UX_STALL", 49, ["ARTIFACT-A"], critical=True)
        with tempfile.TemporaryDirectory() as raw:
            result = aggregate_fixture(
                make_fixture(
                    Path(raw),
                    incidents=[stall],
                    actor_terminals={"ARTIFACT-A": ("PLAYER_STALLED", stall["incidentKey"])},
                )
            )
            self.assertEqual(result["status"], "RERUN_REQUIRED_COLD_INSTABILITY")
            self.assertEqual(result["replacementRequiredLanes"], ["COLD-JOURNEY"])

    def test_two_exact_same_product_stalls_activate_cap_without_replacement(self) -> None:
        stall = make_incident(
            "UX_STALL",
            49,
            ["ARTIFACT-A", "ARTIFACT-B"],
            critical=True,
        )
        terminals = {
            "ARTIFACT-A": ("PLAYER_STALLED", stall["incidentKey"]),
            "ARTIFACT-B": ("PLAYER_STALLED", stall["incidentKey"]),
        }
        with tempfile.TemporaryDirectory() as raw:
            result = aggregate_fixture(
                make_fixture(
                    Path(raw),
                    labeler=constant_label("EXCELLENT"),
                    incidents=[stall],
                    actor_terminals=terminals,
                    not_reached=["J4"],
                )
            )
            self.assertFalse(result["rerunRequired"])
            self.assertEqual(result["activeCap"], 49)
            self.assertEqual(result["cellScores"]["J4"]["state"], "NOT_REACHED_BY_PRODUCT")

    def test_two_severe_stall_claims_with_completed_terminals_cannot_activate_cap_49(self) -> None:
        stall = make_incident(
            "UX_STALL",
            49,
            ["ARTIFACT-A", "ARTIFACT-B"],
            critical=True,
        )
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw), incidents=[stall])
            observations = copy.deepcopy(fixture["actorObservations"])
            for actor_index in (0, 1):
                observations[actor_index]["terminalState"] = "COMPLETED"
                observations[actor_index]["terminalIncidentKey"] = None
                observations[actor_index]["terminalIncidentOrdinal"] = None
            inputs, evaluation, responses = actor_inputs_for_authority_test(
                fixture,
                observations,
            )
            for actor_index in (0, 1):
                evaluation["terminalStates"][actor_index]["state"] = "COMPLETED"
                evaluation["terminalStates"][actor_index]["terminalIncidentKey"] = None
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "severity is not derivable",
            ):
                validate_synthetic_actor_observation_authorities(
                    inputs,
                    evaluation,
                    responses,
                )

    def test_coverage_action_occurrence_sequence_is_frozen(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw))
            trace = copy.deepcopy(fixture["coverageTrace"])
            first = trace["episodes"][0]["traceRows"]
            first[0]["actionOccurrenceId"], first[1]["actionOccurrenceId"] = (
                first[1]["actionOccurrenceId"],
                first[0]["actionOccurrenceId"],
            )
            trace["coverageArtifactId"] = aggregator.self_sha256(
                trace,
                "coverageArtifactId",
                "mutated coverage trace",
            )
            raw_bytes = aggregator.canonical_json_bytes(trace) + b"\n"
            evaluation = copy.deepcopy(fixture["evaluationRun"])
            evaluation["artifacts"]["coverageArtifactId"] = trace["coverageArtifactId"]
            evaluation["artifacts"]["coverageTraceRawSha256"] = aggregator.bytes_sha256(raw_bytes)
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "branch/action order|occurrences/order",
            ):
                aggregator.validate_coverage_trace_authority(
                    trace,
                    raw_bytes,
                    fixture["candidateManifest"],
                    evaluation,
                    {"selfSha256": trace["holdoutConsumptionReceiptSha256"]},
                    {"selfSha256": trace["goldBindingManifestSha256"]},
                    {"selfSha256": trace["coverageActionLedgerSha256"]},
                )

    def test_every_malformed_replacement_consumes_its_only_slot(self) -> None:
        def unstable_labeler(judge: int, kind: str, artifact: str, cell: str) -> str:
            if kind == "COLD_ACTOR" and artifact == "ARTIFACT-A" and cell == "J1":
                return ("EXCELLENT", "SERVICEABLE", "STRONG")[judge]
            return "STRONG"

        def mutate(kind: str, fixture: dict[str, Any]) -> None:
            if kind == "invalid-json":
                fixture["judgmentPaths"][0].write_bytes(b"{invalid-json")
                return
            judgment = copy.deepcopy(fixture["judgments"][0])
            cells = judgment["artifactJudgments"][0]["cells"]
            if kind == "missing-cell":
                cells.pop()
            elif kind == "duplicate-cell":
                cells[1] = copy.deepcopy(cells[0])
            elif kind == "low-confidence":
                cells[0]["confidence"] = "LOW"
            elif kind == "hash-drift":
                judgment["evidenceSetSha256"] = identity_sha("replacement-wrong-evidence")
            elif kind == "lane-drift":
                judgment["artifactJudgments"][0]["artifactKind"] = "COVERAGE"
            else:
                raise AssertionError(kind)
            write_json(fixture["judgmentPaths"][0], judgment)

        for case_index, case in enumerate(
            (
                "invalid-json",
                "missing-cell",
                "duplicate-cell",
                "low-confidence",
                "hash-drift",
                "lane-drift",
            ),
            start=1,
        ):
            with self.subTest(case=case), tempfile.TemporaryDirectory() as raw:
                root = Path(raw)
                initial = make_fixture(
                    root / "initial",
                    labeler=unstable_labeler,
                    cold_suffix=f"initial-{case_index}",
                    coverage_suffix="shared",
                    panel_suffix=f"initial-{case_index}",
                )
                initial_result = aggregate_fixture(initial)
                initial_path = initial["directory"] / "scorecard.json"
                self.assertTrue(initial_result["rerunRequired"])

                replacement = make_fixture(
                    root / "replacement",
                    cold_suffix=f"replacement-{case_index}",
                    coverage_suffix="shared",
                    panel_suffix=f"replacement-{case_index}",
                )
                mutate(case, replacement)
                result = aggregate_fixture(
                    replacement,
                    replacement_for=initial_path,
                    retry_reason="SCHEMA",
                )
                self.assertEqual(result["status"], "BLOCKED_JUDGE_SCHEMA")
                receipt_path = Path(result["replacementReceiptPath"])
                self.assertTrue(receipt_path.exists())
                receipt = json.loads(receipt_path.read_text())
                self.assertEqual(receipt["attemptOutcome"], "SCHEMA_FAILURE")
                self.assertTrue(receipt["slotConsumed"])

                corrected = make_fixture(
                    root / "corrected",
                    cold_suffix=f"corrected-{case_index}",
                    coverage_suffix="shared",
                    panel_suffix=f"corrected-{case_index}",
                )
                with self.assertRaisesRegex(aggregator.ValidationFailure, "already consumed"):
                    aggregate_fixture(corrected, replacement_for=initial_path)

    def test_transport_failure_receipt_allows_duplicate_raw_hashes_and_consumes_slot(self) -> None:
        def unstable_labeler(judge: int, kind: str, artifact: str, cell: str) -> str:
            if kind == "COLD_ACTOR" and artifact == "ARTIFACT-A" and cell == "J1":
                return ("EXCELLENT", "SERVICEABLE", "STRONG")[judge]
            return "STRONG"

        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            initial = make_fixture(
                root / "initial",
                labeler=unstable_labeler,
                cold_suffix="transport-initial",
                coverage_suffix="shared",
                panel_suffix="transport-initial",
            )
            aggregate_fixture(initial)
            initial_path = initial["directory"] / "scorecard.json"
            replacement = make_fixture(
                root / "replacement",
                cold_suffix="transport-replacement",
                coverage_suffix="shared",
                panel_suffix="transport-replacement",
            )
            for path in replacement["judgmentPaths"]:
                path.write_bytes(b"\xff")
            result = aggregate_fixture(
                replacement,
                replacement_for=initial_path,
                retry_reason="TRANSPORT",
            )
            self.assertEqual(result["status"], "BLOCKED_JUDGE_SCHEMA")
            receipt = json.loads(Path(result["replacementReceiptPath"]).read_text())
            self.assertEqual(receipt["attemptOutcome"], "TRANSPORT_FAILURE")
            raw_hashes = [row["rawSha256"] for row in receipt["judgmentAttempts"]]
            self.assertEqual(len(set(raw_hashes)), 1)
            self.assertEqual(
                [row["slotId"] for row in receipt["judgmentAttempts"]],
                ["JUDGE-01", "JUDGE-02", "JUDGE-03"],
            )
            aggregator.validate_native_replacement_receipt_schema(receipt)

    def test_unrelated_replacement_authority_fails_before_receipt_claim(self) -> None:
        def unstable_labeler(judge: int, kind: str, artifact: str, cell: str) -> str:
            if kind == "COLD_ACTOR" and artifact == "ARTIFACT-A" and cell == "J1":
                return ("EXCELLENT", "SERVICEABLE", "STRONG")[judge]
            return "STRONG"

        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            initial = make_fixture(
                root / "initial",
                labeler=unstable_labeler,
                cold_suffix="preflight-initial",
                coverage_suffix="shared",
                panel_suffix="preflight-initial",
            )
            initial_result = aggregate_fixture(initial)
            initial_path = initial["directory"] / "scorecard.json"
            receipt_path = Path(initial_result["replacementReceiptPath"])
            replacement = make_fixture(
                root / "replacement",
                cold_suffix="preflight-replacement",
                coverage_suffix="shared",
                panel_suffix="preflight-replacement",
            )
            refresh_fixture_authorities(replacement, initial_path)
            replacement["candidate"]["provenance"]["sourceCommit"] = "b" * 40
            write_json(replacement["candidatePath"], replacement["candidate"])
            with self.assertRaisesRegex(aggregator.ProvenanceFailure, "source provenance"):
                aggregate_fixture(
                    replacement,
                    replacement_for=initial_path,
                    refresh_authorities=False,
                )
            self.assertFalse(receipt_path.exists())

            replacement["candidate"]["provenance"]["sourceCommit"] = "a" * 40
            result = aggregate_fixture(replacement, replacement_for=initial_path)
            self.assertNotEqual(result["status"], "BLOCKED_JUDGE_SCHEMA")

    def test_unreadable_judgment_finalizes_nonempty_terminal_receipt(self) -> None:
        def unstable_labeler(judge: int, kind: str, artifact: str, cell: str) -> str:
            if kind == "COLD_ACTOR" and artifact == "ARTIFACT-A" and cell == "J1":
                return ("EXCELLENT", "SERVICEABLE", "STRONG")[judge]
            return "STRONG"

        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            initial = make_fixture(
                root / "initial",
                labeler=unstable_labeler,
                cold_suffix="unreadable-initial",
                coverage_suffix="shared",
                panel_suffix="unreadable-initial",
            )
            initial_result = aggregate_fixture(initial)
            initial_path = initial["directory"] / "scorecard.json"
            replacement = make_fixture(
                root / "replacement",
                cold_suffix="unreadable-replacement",
                coverage_suffix="shared",
                panel_suffix="unreadable-replacement",
            )
            replacement["judgmentPaths"][0].unlink()
            result = aggregate_fixture(
                replacement,
                replacement_for=initial_path,
                retry_reason="INPUT_UNREADABLE",
            )
            self.assertEqual(result["status"], "BLOCKED_JUDGE_SCHEMA")
            receipt_path = Path(result["replacementReceiptPath"])
            self.assertGreater(receipt_path.stat().st_size, 0)
            receipt = json.loads(receipt_path.read_text())
            self.assertEqual(receipt["attemptOutcome"], "INPUT_UNREADABLE")
            missing = receipt["judgmentAttempts"][0]
            self.assertEqual(missing["slotId"], "JUDGE-01")
            self.assertEqual(missing["readStatus"], "INPUT_UNREADABLE")
            self.assertIsNone(missing["rawSha256"])
            self.assertEqual(missing["attemptOutcome"], "INPUT_UNREADABLE")
            aggregator.validate_native_replacement_receipt_schema(receipt)

            corrected = make_fixture(
                root / "corrected",
                cold_suffix="unreadable-corrected",
                coverage_suffix="shared",
                panel_suffix="unreadable-corrected",
            )
            with self.assertRaisesRegex(aggregator.ValidationFailure, "already consumed"):
                aggregate_fixture(corrected, replacement_for=initial_path)

    def test_post_claim_oracle_failure_terminalizes_both_singletons(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw) / "oracle-case")
            receipt = fixture["holdoutReceipt"]
            holdout_path = (
                Path(receipt["atomicClaim"]["canonicalRegistryPath"]).parent
                / (
                    ".gridworks-commercial-ux-native-finalization-"
                    + receipt["holdoutConsumptionReceiptSha256"].removeprefix(
                        "sha256:"
                    )
                    + "-initial.receipt.json"
                )
            )
            panel_path = fixture["directory"] / "panel-finalization-seal.json"
            with mock.patch.object(
                aggregator,
                "validate_oracle_ledger",
                side_effect=aggregator.ProvenanceFailure("forced oracle failure"),
            ):
                with self.assertRaisesRegex(
                    aggregator.ProvenanceFailure,
                    "forced oracle failure",
                ):
                    aggregate_fixture(fixture)
            for path in (holdout_path, panel_path):
                self.assertTrue(path.exists())
                self.assertGreater(path.stat().st_size, 0)
                terminal = json.loads(path.read_text())
                self.assertEqual(
                    terminal["outcome"],
                    "POST_CLAIM_VALIDATION_FAILURE",
                )

    def test_malformed_replacement_outcome_writes_schema_valid_receipt(self) -> None:
        def unstable_labeler(judge: int, kind: str, artifact: str, cell: str) -> str:
            if kind == "COLD_ACTOR" and artifact == "ARTIFACT-A" and cell == "J1":
                return ("EXCELLENT", "SERVICEABLE", "STRONG")[judge]
            return "STRONG"

        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            initial = make_fixture(
                root / "initial",
                labeler=unstable_labeler,
                cold_suffix="malformed-outcome-initial",
                coverage_suffix="shared",
                panel_suffix="malformed-outcome-initial",
            )
            initial_result = aggregate_fixture(initial)
            initial_path = initial["directory"] / "scorecard.json"
            replacement = make_fixture(
                root / "replacement",
                cold_suffix="malformed-outcome-replacement",
                coverage_suffix="shared",
                panel_suffix="malformed-outcome-replacement",
            )
            refresh_fixture_authorities(
                replacement,
                initial_path,
                retry_reason="SCHEMA",
            )
            malformed_judge_raw = write_json(replacement["judgmentPaths"][0], {})
            replacement["evaluationRun"]["artifacts"][
                "judgeJudgmentRawSha256"
            ][0] = malformed_judge_raw
            rebind_evaluation_fixture(replacement)
            replacement["verifierPath"].write_bytes(b"{bad")
            with self.assertRaisesRegex(
                aggregator.ValidationFailure,
                "native verifier output failed before validation",
            ):
                aggregate_fixture(
                    replacement,
                    replacement_for=initial_path,
                    refresh_authorities=False,
                )
            receipt_path = Path(initial_result["replacementReceiptPath"])
            self.assertGreater(receipt_path.stat().st_size, 0)
            receipt = json.loads(receipt_path.read_text())
            self.assertEqual(receipt["attemptOutcome"], "SCHEMA_FAILURE")
            self.assertEqual(
                receipt["judgmentAttempts"][0]["attemptOutcome"],
                "SCHEMA_FAILURE",
            )
            aggregator.validate_native_replacement_receipt_schema(receipt)

    def test_partial_singleton_reservation_is_terminalized(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw) / "reservation-case")
            panel_path = fixture["directory"] / "panel-finalization-seal.json"
            panel_path.parent.mkdir(parents=True, exist_ok=True)
            panel_path.write_text("already-finalized\n")
            receipt = fixture["holdoutReceipt"]
            holdout_path = (
                Path(receipt["atomicClaim"]["canonicalRegistryPath"]).parent
                / (
                    ".gridworks-commercial-ux-native-finalization-"
                    + receipt["holdoutConsumptionReceiptSha256"].removeprefix(
                        "sha256:"
                    )
                    + "-initial.receipt.json"
                )
            )
            with self.assertRaisesRegex(
                aggregator.ValidationFailure,
                "already finalized",
            ):
                aggregate_fixture(fixture)
            self.assertGreater(holdout_path.stat().st_size, 0)
            terminal = json.loads(holdout_path.read_text())
            self.assertEqual(terminal["outcome"], "SINGLETON_RESERVATION_FAILURE")

    def test_failed_judge_slot_cannot_use_valid_other_slot_retry_attribution(self) -> None:
        def unstable_labeler(judge: int, kind: str, artifact: str, cell: str) -> str:
            if kind == "COLD_ACTOR" and artifact == "ARTIFACT-A" and cell == "J1":
                return ("EXCELLENT", "SERVICEABLE", "STRONG")[judge]
            return "STRONG"

        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            initial = make_fixture(
                root / "initial",
                labeler=unstable_labeler,
                cold_suffix="retry-initial",
                coverage_suffix="shared",
                panel_suffix="retry-initial",
            )
            initial_result = aggregate_fixture(initial)
            initial_path = initial["directory"] / "scorecard.json"
            receipt_path = Path(initial_result["replacementReceiptPath"])
            replacement = make_fixture(
                root / "replacement",
                cold_suffix="retry-replacement",
                coverage_suffix="shared",
                panel_suffix="retry-replacement",
            )
            malformed = copy.deepcopy(replacement["judgments"][0])
            malformed["artifactJudgments"][0]["cells"].pop()
            write_json(replacement["judgmentPaths"][0], malformed)
            with self.assertRaisesRegex(aggregator.ProvenanceFailure, "JUDGE-01"):
                aggregate_fixture(
                    replacement,
                    replacement_for=initial_path,
                    retry_reason="SCHEMA",
                    retry_slots=("JUDGE-02",),
                )
            self.assertGreater(receipt_path.stat().st_size, 0)
            receipt = json.loads(receipt_path.read_text())
            self.assertEqual(receipt["attemptOutcome"], "SCHEMA_FAILURE")
            self.assertEqual(
                receipt["judgmentAttempts"][0]["attemptOutcome"],
                "SCHEMA_FAILURE",
            )
            aggregator.validate_native_replacement_receipt_schema(receipt)

            corrected = make_fixture(
                root / "corrected",
                cold_suffix="retry-corrected",
                coverage_suffix="shared",
                panel_suffix="retry-corrected",
            )
            with self.assertRaisesRegex(aggregator.ValidationFailure, "already consumed"):
                aggregate_fixture(corrected, replacement_for=initial_path)

    def test_copied_initial_scorecard_cannot_mint_a_replacement(self) -> None:
        """Replacement authority is the canonical INITIAL seal, not copied JSON."""

        def unstable_labeler(judge: int, kind: str, artifact: str, cell: str) -> str:
            if kind == "COLD_ACTOR" and artifact == "ARTIFACT-A" and cell == "J1":
                return ("EXCELLENT", "SERVICEABLE", "STRONG")[judge]
            return "STRONG"

        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            initial = make_fixture(
                root / "initial",
                labeler=unstable_labeler,
                cold_suffix="sealed-initial",
                coverage_suffix="shared",
                panel_suffix="sealed-initial",
            )
            result = aggregate_fixture(initial)
            self.assertTrue(result["rerunRequired"])
            initial_path = initial["directory"] / "scorecard.json"
            copied_path = root / "copied-scorecard.json"
            copied_path.write_bytes(initial_path.read_bytes())
            (root / "panel-finalization-seal.json").write_bytes(
                (initial["directory"] / "panel-finalization-seal.json").read_bytes()
            )
            replacement = make_fixture(
                root / "replacement",
                cold_suffix="sealed-replacement",
                coverage_suffix="shared",
                panel_suffix="sealed-replacement",
            )
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "panel finalization seal canonicalSealPath mismatch",
            ):
                aggregate_fixture(replacement, replacement_for=copied_path)

    def test_same_holdout_lane_cannot_be_rerolled_with_a_different_panel(self) -> None:
        """The INITIAL singleton key is the receipt/lane, not the panel hash."""

        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            first = make_fixture(
                root / "initial",
                labeler=constant_label("EXCELLENT"),
                panel_suffix="first-panel",
            )
            first_result = aggregate_fixture(first)
            self.assertEqual(first_result["status"], "PASS")
            second = make_fixture(
                root / "replacement",
                labeler=constant_label("EXCELLENT"),
                panel_suffix="different-panel",
            )
            self.assertNotEqual(
                first["candidate"]["provenance"]["judgePanelSha256"],
                second["candidate"]["provenance"]["judgePanelSha256"],
            )
            second["holdoutReceipt"]["atomicClaim"]["canonicalRegistryPath"] = (
                first["holdoutReceipt"]["atomicClaim"]["canonicalRegistryPath"]
            )
            second["holdoutReceipt"]["holdoutConsumptionReceiptSha256"] = first[
                "holdoutReceipt"
            ]["holdoutConsumptionReceiptSha256"]
            write_json(second["holdoutReceiptPath"], second["holdoutReceipt"])
            with self.assertRaisesRegex(
                aggregator.ValidationFailure,
                "already finalized",
            ):
                aggregate_fixture(second, output_name="fresh-scorecard.json")


if __name__ == "__main__":
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(NativeAggregateTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    if not result.wasSuccessful():
        raise SystemExit(1)
    print(
        f"native aggregate self-tests PASS: {result.testsRun} tests; "
        "checked-in aggregation-input, replacement-receipt, and scorecard schemas enforced"
    )
