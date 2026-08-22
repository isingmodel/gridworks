#!/usr/bin/env python3
"""Self-tests for the deterministic Gridworks native UX aggregator."""

from __future__ import annotations

import copy
import importlib.util
import inspect
import json
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Any, Callable


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
            "godotExecutableSha256": identity_sha("godot-executable"),
            "managedAssemblySha256": identity_sha("managed-assembly"),
            "pckResourceManifestSha256": identity_sha("pck-manifest"),
            "executionArtifactSha256": execution_artifact_sha,
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
            "checkpoint": "fixture-checkpoint",
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
                        "checkpoint": "fixture-checkpoint-1",
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
                        "checkpoint": "fixture-checkpoint-1",
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
    return {
        "schemaVersion": aggregator.ACTOR_OBSERVATION_SCHEMA,
        "protocol": aggregator.PROTOCOL,
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
                "checkpoint": f"fixture-checkpoint-{ordinal}",
                "appActiveActionIndex": 1,
                "progressStateSha256": identity_sha(
                    f"actor-checkpoint-{actor_index}-{ordinal}-{panel_suffix}"
                ),
                "artifactRefs": [actor_artifact_ref(actor_index)],
            }
            for ordinal in (1, 2)
        ],
        "firstUseRecords": [
            {
                "probeId": "PX01-FIXTURE",
                "episode": "E01-FIRST-LIGHT",
                "checkpoint": "fixture-checkpoint-1",
                "checkpointOrdinal": 1,
                "currentGoal": "Finish the fixture journey.",
                "expectedVisibleConsequence": "The fixture state should advance.",
                "citedVisibleSource": actor_artifact_ref(actor_index),
            }
        ],
        "approvalRecords": [
            {
                "episode": "E01-FIRST-LIGHT",
                "checkpoint": "fixture-checkpoint-1",
                "checkpointOrdinal": 1,
                "predictionImmediatelyBeforeApproval": "The fixture will complete.",
                "observedResult": "The fixture completed.",
                "causalAccount": "The submitted fixture satisfied the visible objective.",
                "artifactRefs": [actor_artifact_ref(actor_index)],
            },
            {
                "episode": "E01-FIRST-LIGHT",
                "checkpoint": "fixture-checkpoint-2",
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
        hard_gates.append(
            {
                "gateId": gate_id,
                "producer": "FIXTURE_PRODUCER",
                "predicate": f"Fixture predicate for {gate_id}.",
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


def aggregate_fixture(
    fixture: dict[str, Any],
    *,
    output_name: str = "scorecard.json",
    replacement_for: Path | None = None,
    retry_reason: str | None = None,
    retry_slots: tuple[str, ...] | None = None,
    refresh_authorities: bool = True,
) -> dict[str, Any]:
    if refresh_authorities:
        refresh_fixture_authorities(
            fixture,
            replacement_for,
            retry_reason=retry_reason,
            retry_slots=retry_slots,
        )
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
    )


def actor_inputs_for_authority_test(
    fixture: dict[str, Any],
    observations: list[dict[str, Any]],
) -> tuple[list[tuple[dict[str, Any], bytes]], dict[str, Any]]:
    inputs: list[tuple[dict[str, Any], bytes]] = []
    raw_shas: list[str] = []
    for observation in observations:
        raw_bytes = (
            json.dumps(observation, ensure_ascii=False, indent=2, sort_keys=True).encode("utf-8")
            + b"\n"
        )
        inputs.append((observation, raw_bytes))
        raw_shas.append(aggregator.bytes_sha256(raw_bytes))
    evaluation = copy.deepcopy(fixture["evaluationRun"])
    evaluation["artifacts"]["actorObservationRawSha256"] = raw_shas
    for index, raw_sha in enumerate(raw_shas):
        evaluation["terminalStates"][index]["actorObservationRawSha256"] = raw_sha
    return inputs, evaluation


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
            inputs, evaluation = actor_inputs_for_authority_test(
                fixture,
                copy.deepcopy(fixture["actorObservations"]),
            )
            rows = aggregator.validate_actor_observation_authorities(
                list(reversed(inputs)),
                evaluation,
            )
            self.assertEqual(len(rows), 3)
            evaluation["terminalStates"][0]["actorArtifactId"], evaluation["terminalStates"][1][
                "actorArtifactId"
            ] = (
                evaluation["terminalStates"][1]["actorArtifactId"],
                evaluation["terminalStates"][0]["actorArtifactId"],
            )
            with self.assertRaisesRegex(aggregator.ProvenanceFailure, "exactly map"):
                aggregator.validate_actor_observation_authorities(inputs, evaluation)

    def test_actor_terminal_state_and_incident_key_are_exact(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw))
            inputs, evaluation = actor_inputs_for_authority_test(
                fixture,
                copy.deepcopy(fixture["actorObservations"]),
            )
            evaluation["terminalStates"][0]["state"] = "PLAYER_STALLED"
            evaluation["terminalStates"][0]["terminalIncidentKey"] = (
                "FIRST_LIGHT/NONE/OPERATIONS/UX_STALL"
            )
            with self.assertRaisesRegex(aggregator.ProvenanceFailure, "terminal state mismatch"):
                aggregator.validate_actor_observation_authorities(inputs, evaluation)

    def test_actor_action_index_and_incident_checkpoint_mapping_are_exact(self) -> None:
        recovery = make_incident("RECOVERY_FRICTION", 69, ["ARTIFACT-A"])
        with tempfile.TemporaryDirectory() as raw:
            fixture = make_fixture(Path(raw), incidents=[recovery])
            observations = copy.deepcopy(fixture["actorObservations"])
            observations[0]["actionLedger"][0]["actionIndex"] = 2
            inputs, evaluation = actor_inputs_for_authority_test(fixture, observations)
            with self.assertRaisesRegex(aggregator.ProvenanceFailure, "exactly 1..N"):
                aggregator.validate_actor_observation_authorities(inputs, evaluation)

            observations = copy.deepcopy(fixture["actorObservations"])
            cited_index = observations[0]["incidents"][0]["actionIndexes"][0]
            observations[0]["actionLedger"][cited_index - 1]["checkpoint"] = "outside-checkpoint"
            inputs, evaluation = actor_inputs_for_authority_test(fixture, observations)
            with self.assertRaisesRegex(aggregator.ProvenanceFailure, "outside its cited"):
                aggregator.validate_actor_observation_authorities(inputs, evaluation)

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
            inputs, evaluation = actor_inputs_for_authority_test(fixture, observations)
            rows = aggregator.validate_actor_observation_authorities(inputs, evaluation)
            self.assertEqual(rows[0]["severeIncidentKeys"], [])
            self.assertEqual(rows[1]["severeIncidentKeys"], [])

            severe_fixture = make_fixture(Path(raw) / "severe", incidents=[make_incident(
                "RECOVERY_FRICTION", 69, ["ARTIFACT-A"]
            )])
            severe_inputs, severe_evaluation = actor_inputs_for_authority_test(
                severe_fixture,
                copy.deepcopy(severe_fixture["actorObservations"]),
            )
            severe_rows = aggregator.validate_actor_observation_authorities(
                severe_inputs,
                severe_evaluation,
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
            inputs, evaluation = actor_inputs_for_authority_test(fixture, observations)
            for actor_index in (0, 1):
                evaluation["terminalStates"][actor_index]["state"] = "COMPLETED"
                evaluation["terminalStates"][actor_index]["terminalIncidentKey"] = None
            with self.assertRaisesRegex(
                aggregator.ProvenanceFailure,
                "severity is not derivable",
            ):
                aggregator.validate_actor_observation_authorities(inputs, evaluation)

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
            with self.assertRaisesRegex(aggregator.ProvenanceFailure, "occurrences/order"):
                aggregator.validate_coverage_trace_authority(
                    trace,
                    raw_bytes,
                    fixture["candidateManifest"],
                    evaluation,
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


if __name__ == "__main__":
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(NativeAggregateTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    if not result.wasSuccessful():
        raise SystemExit(1)
    print(
        f"native aggregate self-tests PASS: {result.testsRun} tests; "
        "checked-in aggregation-input, replacement-receipt, and scorecard schemas enforced"
    )
