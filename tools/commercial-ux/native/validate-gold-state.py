#!/usr/bin/env python3
"""Validate the candidate-independent Commercial UX gold-state contract.

This validator deliberately treats an honest pre-execution manifest as valid while
reporting it as not score-ready.  Callers that are about to capture score-bearing
evidence must pass --require-score-ready; pending or unbound bindings then fail
closed.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shlex
import subprocess
import sys
from collections import Counter
from pathlib import Path
from typing import Any


SCHEMA_VERSION = "gridworks.commercial-ux.gold-state-manifest.v1"
PROTOCOL = "GRIDWORKS-COMMERCIAL-UX-v1.1"
PENDING = "PENDING_NATIVE_REPLAY"
UNBOUND = "UNBOUND_REQUIRED_WITNESS"
NOT_APPLICABLE = "NOT_APPLICABLE"
BOUND = "BOUND_NATIVE_REPLAY"
ALLOWED_BINDING_STATUSES = {PENDING, UNBOUND, NOT_APPLICABLE, BOUND}
BLOCKING_BINDING_STATUSES = {PENDING, UNBOUND}
SHA256_PATTERN = re.compile(r"sha256:[0-9a-f]{64}\Z")


class ContractError(ValueError):
    pass


def _reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ContractError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def load_json(path: Path) -> Any:
    try:
        return json.loads(
            path.read_text(encoding="utf-8"),
            object_pairs_hook=_reject_duplicate_keys,
        )
    except (OSError, UnicodeError, json.JSONDecodeError, ContractError) as error:
        raise ContractError(f"cannot read strict JSON {path}: {error}") from error


def load_json_bytes(data: bytes, label: str) -> Any:
    try:
        return json.loads(data, object_pairs_hook=_reject_duplicate_keys)
    except (UnicodeError, json.JSONDecodeError, ContractError) as error:
        raise ContractError(f"cannot read strict JSON {label}: {error}") from error


def sha256_bytes(data: bytes) -> str:
    return "sha256:" + hashlib.sha256(data).hexdigest()


def repo_path(root: Path, value: Any, label: str) -> Path:
    if not isinstance(value, str) or not value or Path(value).is_absolute():
        raise ContractError(f"{label} must be a non-empty repo-relative path")
    resolved = (root / value).resolve()
    try:
        resolved.relative_to(root)
    except ValueError as error:
        raise ContractError(f"{label} escapes repository root: {value}") from error
    return resolved


def require(condition: bool, message: str, failures: list[str]) -> None:
    if not condition:
        failures.append(message)


def source_sets(world: dict[str, Any], campaign: dict[str, Any]) -> dict[str, set[str]]:
    chapters = campaign.get("chapters", [])
    chapter_ids = {chapter.get("chapterId") for chapter in chapters}
    window_ids = {
        window.get("windowId")
        for chapter in chapters
        for window in chapter.get("decisionWindows", [])
    }
    phase_ids = {
        phase.get("phaseId")
        for chapter in chapters
        for phase in chapter.get("operatingPhases", [])
    }
    load_ids = {
        load.get("loadId")
        for chapter in chapters
        for phase in chapter.get("operatingPhases", [])
        for load in phase.get("loads", [])
    }
    selectors: set[str] = set()
    for chapter in chapters:
        chapter_id = chapter.get("chapterId")
        if chapter.get("briefing") is not None:
            selectors.add(f"{chapter_id}/briefing")
        for window in chapter.get("decisionWindows", []):
            if window.get("story") is not None:
                selectors.add(f"{chapter_id}/window/{window.get('windowId')}")
        if chapter.get("promise") is None and chapter.get("standardResult") is not None:
            selectors.add(f"{chapter_id}/result/standard")
        if chapter.get("promise") is not None:
            if chapter.get("keptResult") is not None:
                selectors.add(f"{chapter_id}/result/keep")
            if chapter.get("deferredResult") is not None:
                selectors.add(f"{chapter_id}/result/defer")
    if campaign.get("epilogue") is not None:
        selectors.add("campaign/epilogue")

    def all_string_ids(document: Any) -> set[str]:
        result: set[str] = set()
        if isinstance(document, dict):
            for key, item in document.items():
                if (key.endswith("Id") or key.endswith("Ids")) and isinstance(item, str):
                    result.add(item)
                elif key.endswith("Ids") and isinstance(item, list):
                    result.update(entry for entry in item if isinstance(entry, str))
                result.update(all_string_ids(item))
        elif isinstance(document, list):
            for item in document:
                result.update(all_string_ids(item))
        return result

    return {
        "chapter": {item for item in chapter_ids if isinstance(item, str)},
        "window": {item for item in window_ids if isinstance(item, str)},
        "phase": {item for item in phase_ids if isinstance(item, str)},
        "load": {item for item in load_ids if isinstance(item, str)},
        "selector": selectors,
        "asset": all_string_ids(world) | all_string_ids(campaign),
    }


def validate_typed_identifiers(
    value: Any,
    label: str,
    ids: dict[str, set[str]],
    failures: list[str],
) -> None:
    scalar_rules = {
        "chapterId": "chapter",
        "completedChapterId": "chapter",
        "selectedChapterId": "chapter",
        "decisionWindowId": "window",
        "phaseId": "phase",
        "promiseDemandId": "load",
        "requiredSuppliedDemandId": "load",
        "requiredDeferredDemandId": "load",
        "requiredSourceNodeId": "asset",
        "nodeClassId": "asset",
        "storySelector": "selector",
    }
    list_rules = {
        "phaseIds": "phase",
        "suppliedDemandIds": "load",
        "requiredDemandIds": "load",
        "requiredSuppliedDemandIds": "load",
        "unavailableAssetIds": "asset",
        "activeRiskAreaIds": "asset",
    }
    if isinstance(value, dict):
        for key, item in value.items():
            if key in scalar_rules and item is not None:
                require(
                    isinstance(item, str) and item in ids[scalar_rules[key]],
                    f"{label}.{key} is not present in its source authority: {item!r}",
                    failures,
                )
            elif key == "selector" and isinstance(item, str) and "/" in item:
                require(
                    item in ids["selector"],
                    f"{label}.selector is not an authored story selector: {item!r}",
                    failures,
                )
            elif key in list_rules:
                require(isinstance(item, list), f"{label}.{key} must be an array", failures)
                if isinstance(item, list):
                    for entry in item:
                        require(
                            isinstance(entry, str) and entry in ids[list_rules[key]],
                            f"{label}.{key} contains an unknown source id: {entry!r}",
                            failures,
                        )
            validate_typed_identifiers(item, f"{label}.{key}", ids, failures)
    elif isinstance(value, list):
        for index, item in enumerate(value):
            validate_typed_identifiers(item, f"{label}[{index}]", ids, failures)


def validate_binding(
    owner: str,
    journal: Any,
    snapshot: Any,
    failures: list[str],
) -> str | None:
    require(isinstance(journal, dict), f"{owner}.journalBinding must be an object", failures)
    require(isinstance(snapshot, dict), f"{owner}.snapshotBinding must be an object", failures)
    if not isinstance(journal, dict) or not isinstance(snapshot, dict):
        return None
    journal_status = journal.get("status")
    snapshot_status = snapshot.get("status")
    require(
        journal_status in ALLOWED_BINDING_STATUSES,
        f"{owner}.journalBinding has invalid status {journal_status!r}",
        failures,
    )
    require(
        snapshot_status in ALLOWED_BINDING_STATUSES,
        f"{owner}.snapshotBinding has invalid status {snapshot_status!r}",
        failures,
    )
    require(
        journal_status == snapshot_status,
        f"{owner} journal/snapshot statuses must match",
        failures,
    )
    if journal_status == BOUND:
        require(
            isinstance(journal.get("sha256"), str)
            and SHA256_PATTERN.fullmatch(journal["sha256"]) is not None,
            f"{owner}.journalBinding bound hash must be sha256:<64 lowercase hex>",
            failures,
        )
        command_count = journal.get("commandCount")
        require(
            isinstance(command_count, int) and not isinstance(command_count, bool)
            and command_count >= 0,
            f"{owner}.journalBinding bound commandCount must be a non-negative integer",
            failures,
        )
        require(
            isinstance(snapshot.get("sha256"), str)
            and SHA256_PATTERN.fullmatch(snapshot["sha256"]) is not None,
            f"{owner}.snapshotBinding bound hash must be sha256:<64 lowercase hex>",
            failures,
        )
    elif journal_status in {PENDING, UNBOUND, NOT_APPLICABLE}:
        require(
            journal.get("sha256") is None and journal.get("commandCount") is None,
            f"{owner}.journalBinding {journal_status} must keep hash/count null",
            failures,
        )
        require(
            snapshot.get("sha256") is None,
            f"{owner}.snapshotBinding {journal_status} must keep hash null",
            failures,
        )
    return journal_status if journal_status in ALLOWED_BINDING_STATUSES else None


def validate_story_manifest_bytes(
    data: bytes,
    label: str,
    authority: dict[str, Any],
    campaign: dict[str, Any],
    selectors: set[str],
    failures: list[str],
) -> str:
    observed_sha256 = sha256_bytes(data)
    try:
        story = load_json_bytes(data, label)
    except ContractError as error:
        failures.append(str(error))
        return observed_sha256
    require(isinstance(story, dict), f"{label} must be a JSON object", failures)
    if not isinstance(story, dict):
        return observed_sha256
    parts = story.get("parts")
    require(
        story.get("schemaVersion") == authority.get("schemaVersion"),
        f"{label} schemaVersion mismatch",
        failures,
    )
    require(
        story.get("campaignId") == campaign.get("campaignId"),
        f"{label} campaignId mismatch",
        failures,
    )
    require(isinstance(parts, list), f"{label}.parts must be an array", failures)
    if not isinstance(parts, list):
        return observed_sha256
    require(story.get("count") == len(parts), f"{label}.count mismatch", failures)
    require(len(parts) == authority.get("partCount"), f"{label} partCount mismatch", failures)
    actual_selectors = [part.get("selector") for part in parts if isinstance(part, dict)]
    require(
        len(actual_selectors) == len(set(actual_selectors)),
        f"{label} contains duplicate selectors",
        failures,
    )
    require(set(actual_selectors) == selectors, f"{label} selector set mismatch", failures)
    kind_counts = Counter(
        part.get("kind") for part in parts if isinstance(part, dict)
    )
    require(
        kind_counts.get("result", 0) == authority.get("resultPartCount"),
        f"{label} resultPartCount mismatch",
        failures,
    )
    return observed_sha256


def validate_candidate_execution_manifest(
    root: Path,
    path: Path,
    gold_manifest_path: Path,
    gold_manifest: dict[str, Any],
    observed_story_sha256: str | None,
    failures: list[str],
) -> bool:
    """Validate the pre-capture candidate identity and gold-contract links."""
    failure_count_before = len(failures)
    contract_validator = gold_manifest_path.parent / "validate-contract.py"
    if contract_validator.is_file():
        completed = subprocess.run(
            [
                sys.executable,
                str(contract_validator),
                "--candidate-manifest",
                str(path),
                "--json",
            ],
            cwd=root,
            check=False,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=30,
        )
        require(
            completed.returncode == 0,
            "candidate manifest failed native contract schema/self-hash validation: "
            + (completed.stdout.strip() or completed.stderr.strip()),
            failures,
        )
    else:
        failures.append("native contract validator is missing; candidate manifest cannot be trusted")
    try:
        candidate = load_json(path)
    except ContractError as error:
        failures.append(str(error))
        return False
    require(isinstance(candidate, dict), "candidate manifest must be an object", failures)
    if not isinstance(candidate, dict):
        return False
    require(
        candidate.get("schemaVersion")
        == "gridworks.commercial-ux.candidate-manifest.v1",
        "candidate manifest schemaVersion mismatch",
        failures,
    )
    require(candidate.get("protocol") == PROTOCOL, "candidate manifest protocol mismatch", failures)
    authorities = gold_manifest.get("authorities", {})
    authority_hashes = candidate.get("authorityHashes")
    required_authority_keys = set(
        gold_manifest.get("candidateExecutionPolicy", {}).get(
            "requiredCandidateAuthorityHashes", []
        )
    )
    require(isinstance(authority_hashes, dict), "candidate authorityHashes must be an object", failures)
    if isinstance(authority_hashes, dict):
        require(
            set(authority_hashes) == required_authority_keys,
            "candidate authorityHashes keys do not match the gold contract",
            failures,
        )
        for key, value in authority_hashes.items():
            require(
                isinstance(value, str) and SHA256_PATTERN.fullmatch(value) is not None,
                f"candidate authorityHashes.{key} is malformed",
                failures,
            )
        source_authorities = {
            "world": "world",
            "campaign": "campaign",
            "coreReplay": "coreReplay",
            "coreContracts": "coreContracts",
            "deterministicWitness": "deterministicWitness",
            "nativeSmokeWitness": "nativeSmokeWitness",
            "storyHarness": "storyHarness",
        }
        for candidate_key, gold_key in source_authorities.items():
            authority = authorities.get(gold_key, {})
            try:
                source_path = repo_path(
                    root,
                    authority.get("path"),
                    f"authorities.{gold_key}.path",
                )
            except ContractError as error:
                failures.append(str(error))
                continue
            if source_path.is_file():
                require(
                    authority_hashes.get(candidate_key)
                    == sha256_bytes(source_path.read_bytes()),
                    f"candidate authorityHashes.{candidate_key} does not match candidate bytes",
                    failures,
                )
        if observed_story_sha256 is None:
            failures.append(
                "candidate readiness must run or provide the authored story manifest "
                "so authorityHashes.storyManifestOutput can be recomputed"
            )
        else:
            require(
                authority_hashes.get("storyManifestOutput") == observed_story_sha256,
                "candidate authorityHashes.storyManifestOutput mismatch",
                failures,
            )

    recipes = candidate.get("recipes")
    require(isinstance(recipes, dict), "candidate recipes must be an object", failures)
    if isinstance(recipes, dict):
        require(
            recipes.get("coverageSha256")
            == authorities.get("coverageRecipe", {}).get("sha256"),
            "candidate recipes.coverageSha256 mismatch",
            failures,
        )
        require(
            recipes.get("conceptExposureSha256")
            == authorities.get("conceptExposure", {}).get("sha256"),
            "candidate recipes.conceptExposureSha256 mismatch",
            failures,
        )
        require(
            recipes.get("goldStateContractSha256")
            == sha256_bytes(gold_manifest_path.read_bytes()),
            "candidate recipes.goldStateContractSha256 mismatch",
            failures,
        )
    source = candidate.get("source")
    require(isinstance(source, dict), "candidate source must be an object", failures)
    try:
        commit = subprocess.run(
            ["git", "rev-parse", "HEAD"], cwd=root, check=True,
            text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=10,
        ).stdout.strip()
        dirty = subprocess.run(
            ["git", "status", "--porcelain", "--untracked-files=all"], cwd=root,
            check=True, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            timeout=10,
        ).stdout
    except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
        failures.append(f"cannot verify candidate source provenance: {error}")
    else:
        if isinstance(source, dict):
            require(source.get("commit") == commit, "candidate source.commit does not match HEAD", failures)
            require(source.get("cleanTree") is True and not dirty, "candidate source is not a clean tree", failures)
    return len(failures) == failure_count_before


def validate(
    root: Path,
    manifest_path: Path,
    story_manifest_path: Path | None,
    run_story_manifest: bool,
    candidate_execution_manifest_path: Path | None,
    require_score_ready: bool,
) -> tuple[list[str], dict[str, Any]]:
    failures: list[str] = []
    try:
        manifest = load_json(manifest_path)
    except ContractError as error:
        return [str(error)], {}
    if not isinstance(manifest, dict):
        return ["gold-state manifest must be a JSON object"], {}

    require(manifest.get("schemaVersion") == SCHEMA_VERSION, "schemaVersion mismatch", failures)
    require(manifest.get("protocol") == PROTOCOL, "protocol mismatch", failures)
    require(manifest.get("candidateIndependent") is True, "candidateIndependent must be true", failures)
    require(manifest.get("scoreBearingCapture") is False, "scoreBearingCapture must be false", failures)
    candidate_policy = manifest.get("candidateExecutionPolicy")
    require(isinstance(candidate_policy, dict), "candidateExecutionPolicy must be an object", failures)
    if isinstance(candidate_policy, dict):
        require(
            candidate_policy.get("candidateAuthorityHashOwner") == "candidate manifest",
            "candidateExecutionPolicy.candidateAuthorityHashOwner mismatch",
            failures,
        )
        require(
            candidate_policy.get("candidateGoldBindingHashOwner")
            == "binding manifest referenced by evaluation-run manifest",
            "candidateExecutionPolicy.candidateGoldBindingHashOwner mismatch",
            failures,
        )
        required_candidate_hashes = candidate_policy.get("requiredCandidateAuthorityHashes")
        require(
            isinstance(required_candidate_hashes, list)
            and set(required_candidate_hashes)
            == {
                "world",
                "campaign",
                "coreReplay",
                "coreContracts",
                "deterministicWitness",
                "nativeSmokeWitness",
                "storyHarness",
                "storyManifestOutput",
            },
            "candidateExecutionPolicy.requiredCandidateAuthorityHashes mismatch",
            failures,
        )

    authorities = manifest.get("authorities")
    require(isinstance(authorities, dict), "authorities must be an object", failures)
    if not isinstance(authorities, dict):
        return failures, {}
    required_file_authorities = (
        "world",
        "campaign",
        "coverageRecipe",
        "conceptExposure",
        "coreReplay",
        "coreContracts",
        "deterministicWitness",
        "nativeSmokeWitness",
        "storyHarness",
    )
    immutable_file_authorities = {"coverageRecipe", "conceptExposure"}
    semantic_json_authorities = {"world", "campaign"}
    semantic_code_authorities = {
        "coreReplay",
        "coreContracts",
        "deterministicWitness",
        "nativeSmokeWitness",
        "storyHarness",
    }
    authority_documents: dict[str, Any] = {}
    for name in required_file_authorities:
        authority = authorities.get(name)
        require(isinstance(authority, dict), f"authorities.{name} must be an object", failures)
        if not isinstance(authority, dict):
            continue
        try:
            path = repo_path(root, authority.get("path"), f"authorities.{name}.path")
        except ContractError as error:
            failures.append(str(error))
            continue
        require(path.is_file(), f"authorities.{name}.path does not exist: {path}", failures)
        if name in immutable_file_authorities:
            expected_hash = authority.get("sha256")
            require(
                isinstance(expected_hash, str) and SHA256_PATTERN.fullmatch(expected_hash) is not None,
                f"authorities.{name}.sha256 must be sha256:<64 lowercase hex>",
                failures,
            )
            if path.is_file():
                actual_hash = sha256_bytes(path.read_bytes())
                require(actual_hash == expected_hash, f"authorities.{name} raw SHA-256 mismatch", failures)
        else:
            require(
                "sha256" not in authority,
                f"authorities.{name} must not freeze candidate-dependent source bytes",
                failures,
            )
            require(
                authority.get("candidateExecutionSha256") is None,
                f"authorities.{name}.candidateExecutionSha256 must be deferred to the candidate manifest",
                failures,
            )
        if name in semantic_code_authorities:
            require(
                authority.get("referenceMode") == "PATH_AND_MEMBER_SEMANTIC",
                f"authorities.{name}.referenceMode mismatch",
                failures,
            )
            members = authority.get("members")
            require(
                isinstance(members, list) and bool(members)
                and len(members) == len(set(members)),
                f"authorities.{name}.members must be a non-empty unique array",
                failures,
            )
            if path.is_file() and isinstance(members, list):
                try:
                    source_text = path.read_text(encoding="utf-8")
                except (OSError, UnicodeError) as error:
                    failures.append(f"cannot inspect authorities.{name}: {error}")
                else:
                    for member in members:
                        require(
                            isinstance(member, str) and member in source_text,
                            f"authorities.{name}.members contains an absent source member: {member!r}",
                            failures,
                        )
        if path.is_file():
            if name in immutable_file_authorities | semantic_json_authorities:
                try:
                    authority_documents[name] = load_json(path)
                except ContractError as error:
                    failures.append(str(error))

    world = authority_documents.get("world")
    campaign = authority_documents.get("campaign")
    recipe = authority_documents.get("coverageRecipe")
    if not all(isinstance(item, dict) for item in (world, campaign, recipe)):
        return failures, {}
    assert isinstance(world, dict) and isinstance(campaign, dict) and isinstance(recipe, dict)
    require(
        world.get("schemaVersion") == authorities["world"].get("schemaVersion")
        and world.get("worldId") == authorities["world"].get("authorityId"),
        "world schema/id authority mismatch",
        failures,
    )
    require(
        campaign.get("schemaVersion") == authorities["campaign"].get("schemaVersion")
        and campaign.get("campaignId") == authorities["campaign"].get("authorityId"),
        "campaign schema/id authority mismatch",
        failures,
    )
    require(recipe.get("protocol") == manifest.get("protocol"), "coverage recipe protocol mismatch", failures)
    require(recipe.get("goldStateManifest") == "gold-state-manifest.json", "coverage recipe goldStateManifest mismatch", failures)

    ids = source_sets(world, campaign)
    recipe_episodes = recipe.get("episodes")
    manifest_episodes = manifest.get("episodes")
    prefixes = manifest.get("prefixes")
    require(isinstance(recipe_episodes, list), "coverage recipe episodes must be an array", failures)
    require(isinstance(manifest_episodes, list), "manifest episodes must be an array", failures)
    require(isinstance(prefixes, list), "manifest prefixes must be an array", failures)
    if not all(isinstance(item, list) for item in (recipe_episodes, manifest_episodes, prefixes)):
        return failures, {}
    assert isinstance(recipe_episodes, list) and isinstance(manifest_episodes, list) and isinstance(prefixes, list)

    recipe_episode_ids = [episode.get("id") for episode in recipe_episodes if isinstance(episode, dict)]
    manifest_episode_ids = [episode.get("id") for episode in manifest_episodes if isinstance(episode, dict)]
    require(manifest_episode_ids == recipe_episode_ids, "manifest episode ids/order must exactly match coverage recipe", failures)
    require(len(recipe_episode_ids) == len(set(recipe_episode_ids)), "coverage recipe episode ids must be unique", failures)

    recipe_prefix_ids = [episode.get("prefixId") for episode in recipe_episodes if isinstance(episode, dict)]
    manifest_prefix_ids = [prefix.get("prefixId") for prefix in prefixes if isinstance(prefix, dict)]
    require(manifest_prefix_ids == recipe_prefix_ids, "manifest prefix ids/order must exactly match coverage recipe", failures)
    require(len(manifest_prefix_ids) == len(set(manifest_prefix_ids)), "manifest prefix ids must be unique", failures)

    status_owners: dict[str, str] = {}
    for prefix in prefixes:
        if not isinstance(prefix, dict):
            failures.append("each manifest prefix must be an object")
            continue
        prefix_id = prefix.get("prefixId")
        owner = f"prefix:{prefix_id}"
        status = validate_binding(owner, prefix.get("journalBinding"), prefix.get("snapshotBinding"), failures)
        if status is not None:
            status_owners[owner] = status
        expectations = prefix.get("expectedStart")
        require(isinstance(expectations, dict) and bool(expectations), f"{owner}.expectedStart must be a non-empty object", failures)
        if isinstance(expectations, dict):
            validate_typed_identifiers(expectations, f"{owner}.expectedStart", ids, failures)
        replay_authority = prefix.get("replayAuthority")
        require(isinstance(replay_authority, list) and bool(replay_authority), f"{owner}.replayAuthority must be non-empty", failures)
        if isinstance(replay_authority, list):
            for index, entry in enumerate(replay_authority):
                entry_label = f"{owner}.replayAuthority[{index}]"
                require(isinstance(entry, dict), f"{entry_label} must be an object", failures)
                if not isinstance(entry, dict):
                    continue
                try:
                    source_path = repo_path(root, entry.get("path"), f"{entry_label}.path")
                except ContractError as error:
                    failures.append(str(error))
                    continue
                require(
                    source_path.is_file(),
                    f"{entry_label}.path does not exist: {source_path}",
                    failures,
                )
                member = entry.get("member")
                role = entry.get("role")
                require(isinstance(member, str) and bool(member), f"{entry_label}.member must be non-empty", failures)
                require(isinstance(role, str) and bool(role), f"{entry_label}.role must be non-empty", failures)
                if source_path.is_file() and isinstance(member, str) and member:
                    probe = member.split("(", 1)[0]
                    try:
                        source_text = source_path.read_text(encoding="utf-8")
                    except (OSError, UnicodeError) as error:
                        failures.append(f"cannot inspect {entry_label}: {error}")
                    else:
                        require(probe in source_text, f"{entry_label}.member is absent from source: {member}", failures)

    recipe_by_id = {
        episode.get("id"): episode
        for episode in recipe_episodes
        if isinstance(episode, dict)
    }
    seen_checkpoints: set[str] = set()
    for episode in manifest_episodes:
        if not isinstance(episode, dict):
            failures.append("each manifest episode must be an object")
            continue
        episode_id = episode.get("id")
        recipe_episode = recipe_by_id.get(episode_id)
        if not isinstance(recipe_episode, dict):
            continue
        require(episode.get("prefixId") == recipe_episode.get("prefixId"), f"{episode_id} prefixId mismatch", failures)
        bindings = episode.get("checkpointBindings")
        require(isinstance(bindings, list), f"{episode_id}.checkpointBindings must be an array", failures)
        if not isinstance(bindings, list):
            continue
        checkpoint_ids = [binding.get("checkpointId") for binding in bindings if isinstance(binding, dict)]
        require(
            checkpoint_ids == recipe_episode.get("checkpoints"),
            f"{episode_id} checkpoint ids/order must exactly match coverage recipe",
            failures,
        )
        for binding in bindings:
            if not isinstance(binding, dict):
                failures.append(f"{episode_id} checkpoint binding must be an object")
                continue
            checkpoint_id = binding.get("checkpointId")
            owner = f"checkpoint:{episode_id}/{checkpoint_id}"
            require(checkpoint_id not in seen_checkpoints, f"duplicate manifest checkpoint id: {checkpoint_id}", failures)
            if isinstance(checkpoint_id, str):
                seen_checkpoints.add(checkpoint_id)
            expectations = binding.get("typedExpectations")
            require(isinstance(expectations, dict) and bool(expectations), f"{owner}.typedExpectations must be non-empty", failures)
            if isinstance(expectations, dict):
                validate_typed_identifiers(expectations, f"{owner}.typedExpectations", ids, failures)
            status = validate_binding(owner, binding.get("journalBinding"), binding.get("snapshotBinding"), failures)
            if status is not None:
                status_owners[owner] = status

    e09_prefix_status = status_owners.get("prefix:PREFIX-NORTH-BANK-MID-DRAFT")
    require(e09_prefix_status == UNBOUND, "E09 prefix must remain UNBOUND_REQUIRED_WITNESS until a matching chapter-four witness exists", failures)
    e09_checkpoint_owners = [
        owner for owner in status_owners if owner.startswith("checkpoint:E09-MID-RESUME/")
    ]
    require(bool(e09_checkpoint_owners), "E09 checkpoint bindings are missing", failures)
    for owner in e09_checkpoint_owners:
        require(status_owners[owner] == UNBOUND, f"{owner} must remain UNBOUND_REQUIRED_WITNESS", failures)
    e09_prefix = next(
        (prefix for prefix in prefixes if isinstance(prefix, dict) and prefix.get("prefixId") == "PREFIX-NORTH-BANK-MID-DRAFT"),
        None,
    )
    require(
        isinstance(e09_prefix, dict)
        and any(
            isinstance(item, dict) and "non-matching" in str(item.get("role", ""))
            for item in e09_prefix.get("replayAuthority", [])
        ),
        "E09 must disclose the non-matching current native smoke witness",
        failures,
    )
    limitations = manifest.get("knownLimitations")
    require(isinstance(limitations, list), "knownLimitations must be an array", failures)
    limitation_ids = {
        item.get("id") for item in limitations or [] if isinstance(item, dict)
    }
    require("GL02-E09-WITNESS-MISMATCH" in limitation_ids, "E09 witness mismatch limitation is required", failures)

    blocking_owners = sorted(
        owner for owner, status in status_owners.items() if status in BLOCKING_BINDING_STATUSES
    )
    has_blockers = bool(blocking_owners)
    binding_complete = manifest.get("bindingComplete")
    capture_allowed = manifest.get("scoreBearingCaptureAllowed")
    if has_blockers:
        require(binding_complete is False, "bindingComplete cannot be true while pending/unbound bindings exist", failures)
        require(capture_allowed is False, "scoreBearingCaptureAllowed cannot be true while pending/unbound bindings exist", failures)
    else:
        require(isinstance(binding_complete, bool), "bindingComplete must be boolean", failures)
        require(isinstance(capture_allowed, bool), "scoreBearingCaptureAllowed must be boolean", failures)

    next_generator = manifest.get("nextRequiredGenerator")
    if has_blockers:
        require(isinstance(next_generator, dict), "nextRequiredGenerator is required while bindings block capture", failures)
        if isinstance(next_generator, dict):
            require(next_generator.get("status") == "MISSING_IN_CURRENT_SCOPE", "nextRequiredGenerator.status mismatch", failures)
            require(next_generator.get("path") == "tools/commercial-ux/native/capture-gold-state.py", "nextRequiredGenerator.path mismatch", failures)
            command_template = next_generator.get("commandTemplate")
            require(
                isinstance(command_template, str)
                and next_generator.get("path") in command_template
                and "{RECIPE_ID}" in command_template
                and "{ABSOLUTE_NATIVE_EXECUTABLE}" in command_template
                and "{ABSOLUTE_OUTPUT_DIRECTORY}" in command_template,
                "nextRequiredGenerator.commandTemplate must identify the missing deterministic capture interface",
                failures,
            )
            outputs = next_generator.get("requiredOutputs")
            require(isinstance(outputs, list) and len(outputs) >= 3, "nextRequiredGenerator.requiredOutputs is incomplete", failures)

    observed_story_sha256: str | None = None
    story_authority = authorities.get("storyManifest")
    require(isinstance(story_authority, dict), "authorities.storyManifest must be an object", failures)
    if isinstance(story_authority, dict):
        require(
            "rawOutputSha256" not in story_authority,
            "authorities.storyManifest must not freeze candidate-dependent story bytes",
            failures,
        )
        require(
            story_authority.get("candidateExecutionRawOutputSha256") is None,
            "authorities.storyManifest.candidateExecutionRawOutputSha256 must be deferred",
            failures,
        )
        require(story_authority.get("partCount") == len(ids["selector"]), "story authority partCount mismatch", failures)
        require(
            story_authority.get("resultPartCount")
            == len([selector for selector in ids["selector"] if "/result/" in selector]),
            "story authority resultPartCount mismatch",
            failures,
        )
        story_checkpoint = next(
            (
                binding
                for episode in manifest_episodes
                if isinstance(episode, dict) and episode.get("id") == "E11-AUTHORED-TEXT"
                for binding in episode.get("checkpointBindings", [])
                if isinstance(binding, dict) and binding.get("checkpointId") == "native-story-manifest-binding"
            ),
            None,
        )
        story_expectation = (
            story_checkpoint.get("typedExpectations", {}).get("story", {})
            if isinstance(story_checkpoint, dict)
            else {}
        )
        require(
            "rawOutputSha256" not in story_expectation
            and story_expectation.get("candidateExecutionRawOutputSha256Required") is True,
            "E11 must defer exact story output hash to the candidate execution manifest",
            failures,
        )
        story_data: bytes | None = None
        story_label = "story manifest"
        if story_manifest_path is not None:
            try:
                story_data = story_manifest_path.read_bytes()
                story_label = str(story_manifest_path)
            except OSError as error:
                failures.append(f"cannot read story manifest {story_manifest_path}: {error}")
        if run_story_manifest:
            command = story_authority.get("generator")
            require(isinstance(command, str) and bool(command), "story manifest generator command is missing", failures)
            if isinstance(command, str) and command:
                try:
                    completed = subprocess.run(
                        shlex.split(command),
                        cwd=root,
                        check=False,
                        stdout=subprocess.PIPE,
                        stderr=subprocess.PIPE,
                        timeout=120,
                    )
                except (OSError, subprocess.TimeoutExpired) as error:
                    failures.append(f"story manifest generator failed to execute: {error}")
                else:
                    require(
                        completed.returncode == 0,
                        "story manifest generator returned nonzero: "
                        + completed.stderr.decode("utf-8", errors="replace"),
                        failures,
                    )
                    if completed.returncode == 0:
                        story_data = completed.stdout
                        story_label = command
        if story_data is not None:
            observed_story_sha256 = validate_story_manifest_bytes(
                story_data,
                story_label,
                story_authority,
                campaign,
                ids["selector"],
                failures,
            )

    candidate_execution_valid = False
    if candidate_execution_manifest_path is not None:
        candidate_execution_valid = validate_candidate_execution_manifest(
            root,
            candidate_execution_manifest_path,
            manifest_path,
            manifest,
            observed_story_sha256,
            failures,
        )

    if require_score_ready:
        require(
            candidate_execution_manifest_path is not None,
            "score-bearing readiness requires --candidate-manifest; gold template bindings never substitute for candidate execution hashes",
            failures,
        )
        require(
            candidate_execution_valid,
            "score-bearing readiness requires a valid candidate manifest bound to this gold contract",
            failures,
        )
        require(
            not has_blockers,
            "score-bearing readiness blocked by PENDING_NATIVE_REPLAY or UNBOUND_REQUIRED_WITNESS; a future binding-manifest validator must resolve these without mutating candidate-independent predicates",
            failures,
        )
        generator_path: Path | None = None
        if isinstance(next_generator, dict):
            try:
                generator_path = repo_path(
                    root,
                    next_generator.get("path"),
                    "nextRequiredGenerator.path",
                )
            except ContractError as error:
                failures.append(str(error))
        require(
            generator_path is not None and generator_path.is_file(),
            "score-bearing readiness blocked until the deterministic candidate binding generator exists",
            failures,
        )

    summary = {
        "episodes": len(manifest_episode_ids),
        "checkpoints": len(seen_checkpoints),
        "blockingOwners": len(blocking_owners),
        "pendingOwners": sum(status == PENDING for status in status_owners.values()),
        "unboundOwners": sum(status == UNBOUND for status in status_owners.values()),
        "scoreBearingReady": (
            candidate_execution_valid
            and not has_blockers
            and isinstance(next_generator, dict)
            and (root / str(next_generator.get("path", ""))).is_file()
        ),
        "storyOutputSha256": observed_story_sha256,
    }
    return failures, summary


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    default_root = Path(__file__).resolve().parents[3]
    parser.add_argument("--root", type=Path, default=default_root)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--story-manifest", type=Path)
    parser.add_argument("--run-story-manifest", action="store_true")
    parser.add_argument(
        "--candidate-manifest",
        "--candidate-execution-manifest",
        dest="candidate_execution_manifest",
        type=Path,
    )
    parser.add_argument("--require-score-ready", action="store_true")
    args = parser.parse_args()
    root = args.root.resolve()
    manifest_path = (
        args.manifest.resolve()
        if args.manifest is not None
        else root / "tools/commercial-ux/native/gold-state-manifest.json"
    )
    failures, summary = validate(
        root,
        manifest_path,
        args.story_manifest.resolve() if args.story_manifest is not None else None,
        args.run_story_manifest,
        (
            args.candidate_execution_manifest.resolve()
            if args.candidate_execution_manifest is not None
            else None
        ),
        args.require_score_ready,
    )
    if failures:
        for failure in failures:
            print(f"ERROR: {failure}", file=sys.stderr)
        return 1
    readiness = "READY" if summary.get("scoreBearingReady") else "BLOCKED"
    print(
        "PASS gold-state contract: "
        f"episodes={summary.get('episodes')} "
        f"checkpoints={summary.get('checkpoints')} "
        f"blockingOwners={summary.get('blockingOwners')} "
        f"pendingOwners={summary.get('pendingOwners')} "
        f"unboundOwners={summary.get('unboundOwners')} "
        f"scoreBearing={readiness}"
    )
    if summary.get("storyOutputSha256") is not None:
        print(f"OBSERVED candidate story output {summary['storyOutputSha256']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
