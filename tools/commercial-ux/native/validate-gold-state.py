#!/usr/bin/env python3
"""Validate the candidate-independent Commercial UX gold-state contract.

This validator deliberately treats an honest pre-execution manifest as valid while
reporting it as not score-ready.  Callers that are about to capture score-bearing
evidence must pass --require-score-ready with the candidate, candidate-specific
gold binding, exact holdout registry transition, and canonical pre-capture session
claim.  The immutable pending template is never rewritten to impersonate captured
native evidence.
"""

from __future__ import annotations

import argparse
import base64
import copy
import hashlib
import importlib.util
import json
import os
import re
import shlex
import shutil
import subprocess
import sys
import tempfile
from collections import Counter
from contextlib import contextmanager
from pathlib import Path
from typing import Any, Iterator


SCHEMA_VERSION = "gridworks.commercial-ux.gold-state-manifest.v1"
PROTOCOL = "GRIDWORKS-COMMERCIAL-UX-v1.1"
PENDING = "PENDING_NATIVE_REPLAY"
UNBOUND = "UNBOUND_REQUIRED_WITNESS"
NOT_APPLICABLE = "NOT_APPLICABLE"
BOUND = "BOUND_NATIVE_REPLAY"
ALLOWED_BINDING_STATUSES = {PENDING, UNBOUND, NOT_APPLICABLE, BOUND}
BLOCKING_BINDING_STATUSES = {PENDING, UNBOUND}
SHA256_PATTERN = re.compile(r"sha256:[0-9a-f]{64}\Z")
EPISODES = [
    "E00-TITLE", "E01-FIRST-LIGHT", "E02-SECOND-HEART", "E03-SECOND-SOURCE",
    "E04-NORTH-BANK", "E05-WHOSE-MARGIN", "E06-FLOOD", "E07-MAINTENANCE",
    "E08-FINALE", "E09-MID-RESUME", "E10-COMPLETE-RESUME", "E11-AUTHORED-TEXT",
]
GOLD_REPLAY_BUILD_INPUT_MANIFEST_PATH = (
    "tools/commercial-ux/native/gold-replay-build-inputs.json"
)
GOLD_REPLAY_PROJECT_PATH = (
    "tools/Gridworks.GoldReplayVerifier/Gridworks.GoldReplayVerifier.csproj"
)
GOLD_REPLAY_BUILD_INPUTS = (
    ("global.json", "SDK_LOCK"),
    (GOLD_REPLAY_PROJECT_PATH, "VERIFIER_PROJECT"),
    ("tools/Gridworks.GoldReplayVerifier/Program.cs", "VERIFIER_ENTRYPOINT"),
    (
        "tools/Gridworks.CommercialChecks/CommercialGoldReplayVerifier.cs",
        "VERIFIER_SOURCE",
    ),
    ("src/Gridworks.Core/Gridworks.Core.csproj", "CORE_PROJECT"),
    (
        "src/Gridworks.Core/Release/V2/CommercialCampaignContracts.cs",
        "CORE_SOURCE",
    ),
    (
        "src/Gridworks.Core/Release/V2/CommercialCampaignLoader.cs",
        "CORE_SOURCE",
    ),
    (
        "src/Gridworks.Core/Release/V2/CommercialCampaignPersistence.cs",
        "CORE_SOURCE",
    ),
    ("src/Gridworks.Core/Release/V2/CommercialCoreContracts.cs", "CORE_SOURCE"),
    ("src/Gridworks.Core/Release/V2/CommercialCoreLoader.cs", "CORE_SOURCE"),
    (
        "src/Gridworks.Core/Release/V2/CommercialCorePersistence.cs",
        "CORE_SOURCE",
    ),
    ("src/Gridworks.Core/Release/V2/CommercialCoreRun.cs", "CORE_SOURCE"),
    (
        "src/Gridworks.Core/Release/V2/CommercialCoreRunContracts.cs",
        "CORE_SOURCE",
    ),
    (
        "src/Gridworks.Core/Release/V2/CommercialSettingsPersistence.cs",
        "CORE_SOURCE",
    ),
    ("src/Gridworks.Core/Release/V2/CommercialWorldContracts.cs", "CORE_SOURCE"),
    ("src/Gridworks.Core/Release/V2/CommercialWorldLoader.cs", "CORE_SOURCE"),
    ("src/Gridworks.Core/Release/V2/ConstructionContracts.cs", "CORE_SOURCE"),
    ("src/Gridworks.Core/Release/V2/ConstructionSession.cs", "CORE_SOURCE"),
    ("src/Gridworks.Core/Release/V2/FixedGeometry.cs", "CORE_SOURCE"),
    ("src/Gridworks.Core/Release/V2/PlacementValidator.cs", "CORE_SOURCE"),
    ("src/Gridworks.Core/Release/V2/SpatialContracts.cs", "CORE_SOURCE"),
    ("src/Gridworks.Core/Release/V2/SpatialWorldLoader.cs", "CORE_SOURCE"),
    ("src/Gridworks.Core/Release/V2/ThermalContracts.cs", "CORE_SOURCE"),
    ("src/Gridworks.Core/Release/V2/ThermalEvaluator.cs", "CORE_SOURCE"),
)
GOLD_REPLAY_ISOLATION_POLICY = {
    "sourceMaterialization": "COPY_EXACT_HASHED_INPUTS_TO_PRIVATE_TEMP_ROOT",
    "directoryBuildProps": "DISABLED",
    "directoryBuildTargets": "DISABLED",
    "directoryPackagesProps": "DISABLED",
    "nugetConfiguration": "GENERATED_CLEAR_SOURCES",
    "repositoryArtifacts": "EXCLUDED",
    "environment": "SANITIZED_ALLOWLIST",
}


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


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def canonical_self_hash(value: dict[str, Any], field: str) -> str:
    payload = copy.deepcopy(value)
    if field not in payload:
        raise ContractError(f"self-hash field absent: {field}")
    payload[field] = None
    return sha256_bytes(canonical_json_bytes(payload))


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
    candidate: dict[str, Any],
    gold_manifest_raw_sha256: str,
    gold_manifest: dict[str, Any],
    observed_story_sha256: str | None,
    failures: list[str],
) -> bool:
    """Validate the pre-capture candidate identity and gold-contract links."""
    failure_count_before = len(failures)
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
            == gold_manifest_raw_sha256,
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


def checkpoint_branch_id(checkpoint_id: str, promise_branch_order: list[str]) -> str:
    if "keep-result" in checkpoint_id:
        return "KEEP"
    if "defer-result" in checkpoint_id:
        return "DEFER"
    if checkpoint_id == "north-bank-first-result":
        return promise_branch_order[0].upper()
    if checkpoint_id in {
        "emergency-use",
        "protective-shutdown",
        "planned-source-outage",
        "finale-heat",
        "finale-storm",
        "finale-result-to-epilogue",
    }:
        return "KEEP"
    return "SHARED"


def read_gold_replay_build_inputs(
    repository_root: Path,
    build_inputs_manifest_path: Path,
) -> dict[str, bytes]:
    """Open and validate every exact source byte allowed into the verifier build."""

    root = repository_root.resolve(strict=True)
    expected_manifest_path = root / GOLD_REPLAY_BUILD_INPUT_MANIFEST_PATH
    try:
        resolved_manifest_path = build_inputs_manifest_path.resolve(strict=True)
    except OSError as error:
        raise ContractError(
            f"gold replay build-input manifest cannot be opened: {error}"
        ) from error
    if (
        build_inputs_manifest_path.is_symlink()
        or resolved_manifest_path != expected_manifest_path
        or expected_manifest_path.absolute() != resolved_manifest_path
        or not resolved_manifest_path.is_file()
    ):
        raise ContractError(
            "gold replay build-input manifest must be the regular canonical "
            f"repository file {GOLD_REPLAY_BUILD_INPUT_MANIFEST_PATH}"
        )
    try:
        manifest_bytes = resolved_manifest_path.read_bytes()
        manifest = load_json_bytes(
            manifest_bytes,
            "gold replay build-input manifest",
        )
    except OSError as error:
        raise ContractError(
            f"gold replay build-input manifest cannot be read: {error}"
        ) from error
    if not isinstance(manifest, dict):
        raise ContractError("gold replay build-input manifest must be an object")
    expected_top_level = {
        "schemaVersion",
        "protocol",
        "sourceRoot",
        "projectPath",
        "sdkLockPath",
        "fileCount",
        "isolationPolicy",
        "files",
    }
    if set(manifest) != expected_top_level:
        raise ContractError("gold replay build-input manifest field set drift")
    if (
        manifest.get("schemaVersion")
        != "gridworks.commercial-ux.gold-replay-build-inputs.v1"
        or manifest.get("protocol") != PROTOCOL
        or manifest.get("sourceRoot") != "REPOSITORY_ROOT"
        or manifest.get("projectPath") != GOLD_REPLAY_PROJECT_PATH
        or manifest.get("sdkLockPath") != "global.json"
        or manifest.get("fileCount") != len(GOLD_REPLAY_BUILD_INPUTS)
        or manifest.get("isolationPolicy") != GOLD_REPLAY_ISOLATION_POLICY
    ):
        raise ContractError("gold replay build-input manifest authority drift")
    rows = manifest.get("files")
    if not isinstance(rows, list) or len(rows) != len(GOLD_REPLAY_BUILD_INPUTS):
        raise ContractError("gold replay build-input manifest must list 24 files")
    observed_path_roles = []
    source_bytes: dict[str, bytes] = {}
    for index, row in enumerate(rows):
        if not isinstance(row, dict) or set(row) != {
            "path", "role", "sha256", "byteLength"
        }:
            raise ContractError(
                f"gold replay build input row {index} field set drift"
            )
        path_value = row.get("path")
        role = row.get("role")
        observed_path_roles.append((path_value, role))
        if not isinstance(path_value, str):
            raise ContractError(f"gold replay build input row {index} path is invalid")
        lexical_path = root / path_value
        try:
            resolved_path = lexical_path.resolve(strict=True)
            resolved_path.relative_to(root)
        except (OSError, ValueError) as error:
            raise ContractError(
                f"gold replay build input cannot be opened inside the repository: "
                f"{path_value}: {error}"
            ) from error
        if (
            lexical_path.is_symlink()
            or lexical_path.absolute() != resolved_path
            or not resolved_path.is_file()
        ):
            raise ContractError(
                "gold replay build input must be a regular canonical file without "
                f"symlinks: {path_value}"
            )
        try:
            data = resolved_path.read_bytes()
        except OSError as error:
            raise ContractError(
                f"gold replay build input cannot be read: {path_value}: {error}"
            ) from error
        byte_length = row.get("byteLength")
        if (
            not isinstance(byte_length, int)
            or isinstance(byte_length, bool)
            or byte_length <= 0
            or byte_length != len(data)
            or row.get("sha256") != sha256_bytes(data)
        ):
            raise ContractError(
                f"gold replay build input raw bytes mismatch: {path_value}"
            )
        source_bytes[path_value] = data
    if observed_path_roles != list(GOLD_REPLAY_BUILD_INPUTS):
        raise ContractError("gold replay build input path/role set or order drift")
    if len(source_bytes) != len(GOLD_REPLAY_BUILD_INPUTS):
        raise ContractError("gold replay build input paths must be unique")
    return source_bytes


@contextmanager
def isolated_gold_replay_verifier_assembly(
    repository_root: Path,
    build_inputs_manifest_path: Path,
) -> Iterator[Path]:
    """Build the verifier from only the exact bytes in its bound input manifest."""

    source_bytes = read_gold_replay_build_inputs(
        repository_root,
        build_inputs_manifest_path,
    )

    with tempfile.TemporaryDirectory(
        prefix="gridworks-gold-replay-build-"
    ) as temporary:
        build_root = Path(temporary).resolve()
        source_root = build_root / "source"
        artifacts_path = build_root / "artifacts"
        packages_path = build_root / "packages"
        cli_home = build_root / "dotnet-home"
        temporary_path = build_root / "tmp"
        for directory in (
            source_root,
            artifacts_path,
            packages_path,
            cli_home,
            temporary_path,
        ):
            directory.mkdir(parents=True, exist_ok=False)
        for relative_path, data in source_bytes.items():
            target = source_root / relative_path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        materialized_files = sorted(
            path.relative_to(source_root).as_posix()
            for path in source_root.rglob("*")
            if path.is_file()
        )
        if materialized_files != sorted(source_bytes):
            raise ContractError(
                "isolated gold replay source tree differs from bound build inputs"
            )
        verifier_project_path = source_root / GOLD_REPLAY_PROJECT_PATH
        nuget_config_path = build_root / "NuGet.Config"
        nuget_config_path.write_bytes(
            b'<?xml version="1.0" encoding="utf-8"?>\n'
            b"<configuration>\n"
            b"  <packageSources><clear /></packageSources>\n"
            b"</configuration>\n"
        )
        dotnet = shutil.which("dotnet")
        if dotnet is None:
            raise ContractError("dotnet executable is unavailable")
        try:
            dotnet_path = Path(dotnet).resolve(strict=True)
        except OSError as error:
            raise ContractError(f"dotnet executable cannot be resolved: {error}") from error
        build_environment = {
            key: os.environ[key]
            for key in ("PATH", "DOTNET_ROOT", "LANG", "LC_ALL")
            if key in os.environ
        }
        build_environment.update({
            "DOTNET_CLI_HOME": str(cli_home),
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
            "DOTNET_HOST_PATH": str(dotnet_path),
            "DOTNET_MULTILEVEL_LOOKUP": "0",
            "DOTNET_NOLOGO": "1",
            "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
            "NUGET_PACKAGES": str(packages_path),
            "TMPDIR": str(temporary_path),
        })
        isolated_properties = [
            "-p:UseArtifactsOutput=true",
            f"-p:ArtifactsPath={artifacts_path}",
            f"-p:RestorePackagesPath={packages_path}",
            "-p:ImportDirectoryBuildProps=false",
            "-p:ImportDirectoryBuildTargets=false",
            "-p:ImportDirectoryPackagesProps=false",
            "-p:ContinuousIntegrationBuild=true",
            "-p:Deterministic=true",
        ]
        restore = subprocess.run(
            [
                str(dotnet_path), "restore", str(verifier_project_path),
                "--configfile", str(nuget_config_path),
                *isolated_properties,
            ],
            cwd=source_root,
            env=build_environment,
            check=False,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=180,
        )
        if restore.returncode != 0:
            details = (
                restore.stderr.decode("utf-8", errors="replace").strip()
                or restore.stdout.decode("utf-8", errors="replace").strip()
            )
            raise ContractError(
                "isolated gold Core replay verifier restore failed: " + details
            )
        build = subprocess.run(
            [
                str(dotnet_path), "build", str(verifier_project_path),
                "-c", "Release", "--no-restore",
                *isolated_properties,
            ],
            cwd=source_root,
            env=build_environment,
            check=False,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=180,
        )
        if build.returncode != 0:
            details = (
                build.stderr.decode("utf-8", errors="replace").strip()
                or build.stdout.decode("utf-8", errors="replace").strip()
            )
            raise ContractError(
                "isolated gold Core replay verifier build failed: " + details
            )
        assembly_path = (
            artifacts_path
            / "bin"
            / verifier_project_path.stem
            / "release"
            / f"{verifier_project_path.stem}.dll"
        )
        try:
            resolved_assembly = assembly_path.resolve(strict=True)
            resolved_assembly.relative_to(build_root)
        except (OSError, ValueError) as error:
            raise ContractError(
                "isolated gold Core replay verifier assembly is missing or escaped "
                f"the clean build root: {error}"
            ) from error
        if assembly_path.is_symlink() or not resolved_assembly.is_file():
            raise ContractError(
                "isolated gold Core replay verifier assembly must be a regular "
                "newly built DLL"
            )
        yield resolved_assembly


def validate_gold_bundle(
    repository_root: Path,
    manifest: dict[str, Any],
    binding: dict[str, Any],
    prefix_rows: list[Any],
    checkpoint_rows: list[Any],
    failures: list[str],
) -> dict[str, dict[str, Any]]:
    root_value = binding.get("canonicalGoldBundleRoot")
    if not isinstance(root_value, str):
        failures.append("gold binding canonicalGoldBundleRoot must be an absolute path")
        return {}
    root = Path(root_value)
    try:
        resolved_root = root.resolve(strict=True)
    except OSError as error:
        failures.append(f"gold binding bundle root cannot be opened: {error}")
        return {}
    require(
        root.is_absolute() and root_value == str(resolved_root) and resolved_root.is_dir(),
        "gold binding bundle root must be a canonical directory without symlinks",
        failures,
    )
    referenced: list[str] = []
    root_rows: list[dict[str, Any]] = []
    replay_entries: list[dict[str, Any]] = []
    row_pairs = [
        *(
            (f"prefix:{row.get('prefixId')}", row)
            for row in prefix_rows if isinstance(row, dict)
        ),
        *(
            (
                f"checkpoint:{row.get('episodeId')}/{row.get('checkpointId')}",
                row,
            )
            for row in checkpoint_rows if isinstance(row, dict)
        ),
    ]
    for owner, row in row_pairs:
        if not isinstance(row, dict):
            continue
        exact_components: dict[str, bytes] = {}
        for field in ("journalBinding", "snapshotBinding"):
            component = row.get(field)
            if not isinstance(component, dict) or component.get("status") != BOUND:
                continue
            locator = component.get("locator")
            byte_length = component.get("byteLength")
            expected_sha = component.get("sha256")
            if not isinstance(locator, str):
                failures.append(f"gold binding {field} is missing its raw locator")
                continue
            referenced.append(locator)
            target = resolved_root / locator
            try:
                resolved_target = target.resolve(strict=True)
            except OSError as error:
                failures.append(f"gold binding locator cannot be opened: {locator}: {error}")
                continue
            require(
                str(target) == str(resolved_target)
                and resolved_target.is_file()
                and resolved_root in resolved_target.parents,
                f"gold binding locator escapes its canonical root or uses a symlink: {locator}",
                failures,
            )
            if not resolved_target.is_file():
                continue
            data = resolved_target.read_bytes()
            observed_sha = sha256_bytes(data)
            require(
                expected_sha == observed_sha and byte_length == len(data),
                f"gold binding raw bytes mismatch: {locator}",
                failures,
            )
            root_rows.append({
                "locator": locator,
                "rawSha256": observed_sha,
                "byteLength": len(data),
            })
            exact_components[field] = data
        if set(exact_components) == {"journalBinding", "snapshotBinding"}:
            replay_entries.append({
                "owner": owner,
                "journalBytesBase64": base64.b64encode(
                    exact_components["journalBinding"]
                ).decode("ascii"),
                "snapshotBytesBase64": base64.b64encode(
                    exact_components["snapshotBinding"]
                ).decode("ascii"),
            })
    bundle_integrity_failure_count = len(failures)
    require(
        len(referenced) == 112
        and len(referenced) == len(set(referenced))
        and binding.get("goldBundleEntryCount") == len(referenced),
        "gold binding bundle must contain 112 unique applicable journal/snapshot locators",
        failures,
    )
    disk_files: list[str] = []
    symlink_count = 0
    if resolved_root.is_dir():
        for path in resolved_root.rglob("*"):
            if path.is_symlink():
                symlink_count += 1
            elif path.is_file():
                disk_files.append(path.relative_to(resolved_root).as_posix())
    require(
        symlink_count == 0
        and binding.get("goldBundleSymlinkCount") == 0,
        "gold binding bundle must reject every symlink",
        failures,
    )
    require(
        sorted(disk_files) == sorted(referenced)
        and binding.get("goldBundleExtraFileCount") == 0,
        "gold binding bundle recursive file set has missing or extra files",
        failures,
    )
    root_rows.sort(key=lambda row: row["locator"])
    require(
        binding.get("goldBundleRootSha256")
        == sha256_bytes(canonical_json_bytes(root_rows)),
        "gold binding bundle content-root SHA mismatch",
        failures,
    )
    if len(failures) != bundle_integrity_failure_count:
        return {}
    if len(replay_entries) != 56 or len(root_rows) != 112:
        failures.append(
            "gold binding semantic replay requires 56 exact journal/snapshot pairs"
        )
        return {}

    authorities = manifest.get("authorities", {})
    try:
        world_path = repo_path(
            repository_root,
            authorities.get("world", {}).get("path"),
            "authorities.world.path",
        )
        campaign_path = repo_path(
            repository_root,
            authorities.get("campaign", {}).get("path"),
            "authorities.campaign.path",
        )
        verifier_path = repo_path(
            repository_root,
            authorities.get("goldReplayVerifier", {}).get("path"),
            "authorities.goldReplayVerifier.path",
        )
        verifier_entrypoint_path = repo_path(
            repository_root,
            authorities.get("goldReplayVerifier", {}).get("entrypointPath"),
            "authorities.goldReplayVerifier.entrypointPath",
        )
        verifier_project_path = repo_path(
            repository_root,
            authorities.get("goldReplayVerifier", {}).get("projectPath"),
            "authorities.goldReplayVerifier.projectPath",
        )
        verifier_build_inputs_path = repo_path(
            repository_root,
            authorities.get("goldReplayVerifier", {}).get("buildInputsPath"),
            "authorities.goldReplayVerifier.buildInputsPath",
        )
    except ContractError as error:
        failures.append(str(error))
        return {}
    if not all(path.is_file() for path in (
        world_path,
        campaign_path,
        verifier_path,
        verifier_entrypoint_path,
        verifier_project_path,
        verifier_build_inputs_path,
    )):
        failures.append("gold binding semantic replay authorities are missing")
        return {}
    verifier_sha = sha256_bytes(verifier_path.read_bytes())
    verifier_authority_exact = (
        authorities.get("goldReplayVerifier", {}).get("sha256") == verifier_sha
        and authorities.get("goldReplayVerifier", {}).get("entrypointSha256")
        == sha256_bytes(verifier_entrypoint_path.read_bytes())
        and authorities.get("goldReplayVerifier", {}).get("projectSha256")
        == sha256_bytes(verifier_project_path.read_bytes())
        and authorities.get("goldReplayVerifier", {}).get("buildInputsSha256")
        == sha256_bytes(verifier_build_inputs_path.read_bytes())
    )
    require(
        verifier_authority_exact,
        "gold replay verifier source/entrypoint/project/build-input raw SHA mismatch",
        failures,
    )
    if not verifier_authority_exact:
        return {}

    batch = {
        "schemaVersion": "gridworks.commercial-ux.gold-replay-batch-input.v1",
        "worldBytesBase64": base64.b64encode(world_path.read_bytes()).decode("ascii"),
        "campaignBytesBase64": base64.b64encode(campaign_path.read_bytes()).decode("ascii"),
        "entries": replay_entries,
    }
    batch_path: Path | None = None
    try:
        descriptor, raw_path = tempfile.mkstemp(prefix="gridworks-gold-replay-", suffix=".json")
        batch_path = Path(raw_path)
        batch_bytes = canonical_json_bytes(batch)
        with os.fdopen(descriptor, "wb", closefd=True) as stream:
            stream.write(batch_bytes)
            stream.flush()
            os.fsync(stream.fileno())
        with isolated_gold_replay_verifier_assembly(
            repository_root,
            verifier_build_inputs_path,
        ) as verifier_assembly:
            completed = subprocess.run(
                [
                    "dotnet", str(verifier_assembly),
                    "--verify-batch", str(batch_path),
                ],
                cwd=repository_root,
                check=False,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                timeout=180,
            )
    except (ContractError, OSError, subprocess.TimeoutExpired) as error:
        failures.append(f"gold Core replay verifier failed to execute: {error}")
        return {}
    finally:
        if batch_path is not None:
            try:
                batch_path.unlink()
            except OSError:
                pass
    if completed.returncode != 0:
        failures.append(
            "gold Core replay verifier rejected the bundle: "
            + completed.stderr.decode("utf-8", errors="replace").strip()
        )
        return {}
    try:
        report = load_json_bytes(completed.stdout, "gold Core replay verifier report")
    except ContractError as error:
        failures.append(str(error))
        return {}
    if not isinstance(report, dict) or report.get("schemaVersion") != (
        "gridworks.commercial-ux.gold-replay-batch-report.v1"
    ):
        failures.append("gold Core replay verifier report schema mismatch")
        return {}
    report_rows = report.get("entries")
    if not isinstance(report_rows, list):
        failures.append("gold Core replay verifier entries must be an array")
        return {}
    reports_by_owner = {
        row.get("owner"): row for row in report_rows if isinstance(row, dict)
    }
    require(
        len(report_rows) == 56
        and len(reports_by_owner) == 56
        and set(reports_by_owner) == {entry["owner"] for entry in replay_entries},
        "gold Core replay verifier owner set mismatch",
        failures,
    )
    binding_rows = {owner: row for owner, row in row_pairs}
    for owner, report_row in reports_by_owner.items():
        binding_row = binding_rows.get(owner, {})
        journal = binding_row.get("journalBinding", {})
        snapshot = binding_row.get("snapshotBinding", {})
        require(
            report_row.get("journalRawSha256") == journal.get("sha256")
            and report_row.get("commandCount") == journal.get("commandCount")
            and report_row.get("state", {}).get("commandCount")
            == journal.get("commandCount")
            and report_row.get("snapshotRawSha256") == snapshot.get("sha256"),
            f"{owner} replay-derived hash/count mismatch",
            failures,
        )
    return reports_by_owner


def _enum_equal(observed: Any, expected: Any) -> bool:
    if observed is None or expected is None:
        return observed is expected
    return str(observed).casefold() == str(expected).casefold()


def validate_replay_derived_core_expectations(
    manifest: dict[str, Any],
    reports_by_owner: dict[str, dict[str, Any]],
    failures: list[str],
) -> None:
    """Check every replay-derivable typed core expectation against Core output."""

    expectations_by_owner: dict[str, dict[str, Any]] = {}
    for row in manifest.get("prefixes", []):
        if isinstance(row, dict):
            expectations_by_owner[f"prefix:{row.get('prefixId')}"] = (
                row.get("expectedStart", {})
            )
    for episode in manifest.get("episodes", []):
        if not isinstance(episode, dict):
            continue
        for row in episode.get("checkpointBindings", []):
            if isinstance(row, dict):
                expectations_by_owner[
                    f"checkpoint:{episode.get('id')}/{row.get('checkpointId')}"
                ] = row.get("typedExpectations", {}).get("core", {})

    direct_fields = {
        "chapterId": "chapterId",
        "decisionWindowId": "decisionWindowId",
        "decisionWindowIndex": "decisionWindowIndex",
        "chapterResultsCount": "chapterResultsCount",
        "campaignComplete": "campaignComplete",
        "nodeDraftPresent": "nodeDraftPresent",
        "lineDraftPresent": "lineDraftPresent",
        "thermalMemoryProtectiveOutageCount": "thermalMemoryProtectiveOutageCount",
    }
    enum_fields = {
        "constructionPhase": "constructionPhase",
        "promiseDecision": "promiseDecision",
    }
    for owner, report in reports_by_owner.items():
        state = report.get("state")
        expected = expectations_by_owner.get(owner, {})
        if not isinstance(state, dict) or not isinstance(expected, dict):
            failures.append(f"{owner} replay-derived state/expectation is malformed")
            continue
        for expectation_key, state_key in direct_fields.items():
            if expectation_key in expected:
                require(
                    state.get(state_key) == expected.get(expectation_key),
                    f"{owner} replay-derived {expectation_key} mismatch",
                    failures,
                )
        for expectation_key, state_key in enum_fields.items():
            if expectation_key in expected:
                require(
                    _enum_equal(state.get(state_key), expected.get(expectation_key)),
                    f"{owner} replay-derived {expectation_key} mismatch",
                    failures,
                )
        if expected.get("lineDraftRequired") is True:
            require(
                state.get("lineDraftPresent") is True,
                f"{owner} replay-derived lineDraftRequired mismatch",
                failures,
            )


def validate_replay_derived_e09(
    reports_by_owner: dict[str, dict[str, Any]],
    witness: dict[str, Any],
    failures: list[str],
) -> None:
    owners = {
        "pre": "checkpoint:E09-MID-RESUME/mid-save-before-exit",
        "resume": "checkpoint:E09-MID-RESUME/resume-orientation",
        "editable": "checkpoint:E09-MID-RESUME/resumed-editable-draft",
        "prefix": "prefix:PREFIX-NORTH-BANK-MID-DRAFT",
    }
    reports = {key: reports_by_owner.get(owner) for key, owner in owners.items()}
    if not all(isinstance(row, dict) for row in reports.values()):
        failures.append("gold binding E09 semantic replay reports are incomplete")
        return

    geometry_hashes = {
        key: sha256_bytes(canonical_json_bytes(row.get("draftGeometry")))
        for key, row in reports.items() if isinstance(row, dict)
    }
    projection_hashes = {
        key: sha256_bytes(canonical_json_bytes(row.get("draftProjection")))
        for key, row in reports.items() if isinstance(row, dict)
    }
    require(
        witness.get("preExitDraftGeometrySha256") == geometry_hashes.get("pre")
        and witness.get("postResumeDraftGeometrySha256") == geometry_hashes.get("resume")
        and witness.get("resumedEditableDraftGeometrySha256") == geometry_hashes.get("editable")
        and geometry_hashes.get("prefix") == geometry_hashes.get("pre"),
        "gold binding E09 draft geometry hashes must be derived from replayed snapshots",
        failures,
    )
    require(
        witness.get("preExitDraftProjectionSha256") == projection_hashes.get("pre")
        and witness.get("postResumeDraftProjectionSha256") == projection_hashes.get("resume")
        and witness.get("resumedEditableDraftProjectionSha256") == projection_hashes.get("editable")
        and projection_hashes.get("prefix") == projection_hashes.get("pre"),
        "gold binding E09 draft projection hashes must be derived from replayed Core previews",
        failures,
    )
    require(
        len(set(geometry_hashes.values())) == 1
        and len(set(projection_hashes.values())) == 1,
        "gold binding E09 add+undo did not restore replay-derived geometry/projection",
        failures,
    )


def validate_candidate_binding_overlay(
    root: Path,
    binding: dict[str, Any],
    manifest_path: Path,
    manifest_raw_sha256: str,
    manifest: dict[str, Any],
    candidate: dict[str, Any] | None,
    failures: list[str],
) -> bool:
    failure_count_before = len(failures)
    require(
        binding.get("goldStateContractSha256")
        == manifest_raw_sha256,
        "gold binding manifest gold-state contract raw SHA mismatch",
        failures,
    )
    require(
        binding.get("coverageRecipeSha256")
        == manifest.get("authorities", {}).get("coverageRecipe", {}).get("sha256"),
        "gold binding manifest coverage recipe raw SHA mismatch",
        failures,
    )
    try:
        queue = load_json(manifest_path.parent / "holdout-recipes.json")
    except ContractError as error:
        failures.append(str(error))
        queue = {}
    selected_rows = [
        row for row in [queue.get("formative"), *queue.get("holdouts", [])]
        if isinstance(row, dict) and row.get("id") == binding.get("selectedRecipeId")
    ]
    if len(selected_rows) != 1:
        failures.append("gold binding selected recipe is absent or duplicated")
        expected_realization: dict[str, Any] = {}
    else:
        selected_row = selected_rows[0]
        require(
            binding.get("selectedRecipeSha256")
            == sha256_bytes(canonical_json_bytes(selected_row)),
            "gold binding selected recipe projection hash mismatch",
            failures,
        )
        expected_realization = {
            "missionPrototypeBits": selected_row.get("missionPrototypeBits"),
            "promiseBranchOrder": selected_row.get("promiseBranchOrder"),
            "actorArtifactPermutation": selected_row.get("actorArtifactPermutation"),
            "coverageArtifactOrder": selected_row.get("coverageArtifactOrder"),
            "coveragePresentationEpisodeIds": (
                EPISODES
                if selected_row.get("coverageArtifactOrder") == "EPISODE_ASCENDING"
                else list(reversed(EPISODES))
            ),
        }
        require(
            binding.get("holdoutRealization") == expected_realization,
            "gold binding holdoutRealization exact projection mismatch",
            failures,
        )
    promise_branch_order = expected_realization.get("promiseBranchOrder")
    if not isinstance(promise_branch_order, list) or len(promise_branch_order) != 2:
        promise_branch_order = ["keep", "defer"]
    template_prefixes = manifest.get("prefixes", [])
    expected_prefix_ids = [
        row.get("prefixId") for row in template_prefixes if isinstance(row, dict)
    ]
    prefix_rows = binding.get("prefixBindings")
    require(isinstance(prefix_rows, list), "gold binding prefixBindings must be an array", failures)
    prefix_rows = prefix_rows if isinstance(prefix_rows, list) else []
    require(
        [row.get("prefixId") for row in prefix_rows if isinstance(row, dict)]
        == expected_prefix_ids,
        "gold binding prefix ids/order must exactly match the immutable template",
        failures,
    )
    expected_checkpoints = [
        (episode.get("id"), row.get("checkpointId"))
        for episode in manifest.get("episodes", []) if isinstance(episode, dict)
        for row in episode.get("checkpointBindings", []) if isinstance(row, dict)
    ]
    checkpoint_rows = binding.get("checkpointBindings")
    require(
        isinstance(checkpoint_rows, list),
        "gold binding checkpointBindings must be an array",
        failures,
    )
    checkpoint_rows = checkpoint_rows if isinstance(checkpoint_rows, list) else []
    require(
        [
            (row.get("episodeId"), row.get("checkpointId"))
            for row in checkpoint_rows if isinstance(row, dict)
        ] == expected_checkpoints,
        "gold binding checkpoint ids/order must exactly match the immutable template",
        failures,
    )
    template_statuses = {
        f"prefix:{row.get('prefixId')}": row.get("journalBinding", {}).get("status")
        for row in template_prefixes if isinstance(row, dict)
    }
    template_statuses.update({
        f"checkpoint:{episode.get('id')}/{row.get('checkpointId')}":
            row.get("journalBinding", {}).get("status")
        for episode in manifest.get("episodes", []) if isinstance(episode, dict)
        for row in episode.get("checkpointBindings", []) if isinstance(row, dict)
    })
    observed_statuses: dict[str, str | None] = {}
    for row in prefix_rows:
        if not isinstance(row, dict):
            failures.append("gold binding prefix row must be an object")
            continue
        owner = f"prefix:{row.get('prefixId')}"
        observed_statuses[owner] = validate_binding(
            owner, row.get("journalBinding"), row.get("snapshotBinding"), failures
        )
    for row in checkpoint_rows:
        if not isinstance(row, dict):
            failures.append("gold binding checkpoint row must be an object")
            continue
        owner = f"checkpoint:{row.get('episodeId')}/{row.get('checkpointId')}"
        observed_statuses[owner] = validate_binding(
            owner, row.get("journalBinding"), row.get("snapshotBinding"), failures
        )
        if isinstance(row.get("checkpointId"), str):
            require(
                row.get("checkpointBranchId")
                == checkpoint_branch_id(row["checkpointId"], promise_branch_order),
                f"{owner} checkpointBranchId mismatch",
                failures,
            )
    for owner, template_status in template_statuses.items():
        expected_status = NOT_APPLICABLE if template_status == NOT_APPLICABLE else BOUND
        require(
            observed_statuses.get(owner) == expected_status,
            f"{owner} overlay status must be {expected_status}",
            failures,
        )
    bound_count = sum(status == BOUND for status in observed_statuses.values())
    not_applicable_count = sum(
        status == NOT_APPLICABLE for status in observed_statuses.values()
    )
    require(
        binding.get("bindingSummary") == {
            "prefixCount": 12,
            "checkpointCount": 49,
            "applicableBindingCount": 56,
            "boundBindingCount": bound_count,
            "notApplicableBindingCount": not_applicable_count,
            "allApplicableBindingsExact": bound_count == 56,
        },
        "gold binding summary is not the exact derived 12/49/56/5 projection",
        failures,
    )
    replay_reports = validate_gold_bundle(
        root,
        manifest,
        binding,
        prefix_rows,
        checkpoint_rows,
        failures,
    )
    validate_replay_derived_core_expectations(manifest, replay_reports, failures)
    witness = binding.get("e09NorthBankTwoProcessWitness")
    require(isinstance(witness, dict), "gold binding E09 witness must be an object", failures)
    if isinstance(witness, dict):
        require(
            witness.get("preExitProcessTreeId")
            != witness.get("postResumeProcessTreeId"),
            "gold binding E09 pre-exit and post-resume process trees must be distinct",
            failures,
        )
        require(
            witness.get("preExitJournalSha256")
            == witness.get("postResumeJournalSha256")
            and witness.get("preExitSnapshotSha256")
            == witness.get("postResumeSnapshotSha256")
            and witness.get("preExitCommandCount")
            == witness.get("postResumeCommandCount"),
            "gold binding E09 mid-save and resume-orientation bytes/count must be exact",
            failures,
        )
        require(
            isinstance(witness.get("resumedEditableDraftCommandCount"), int)
            and witness.get("resumedEditableDraftCommandCount")
            == witness.get("postResumeCommandCount", -2) + 2
            and witness.get("resumedEditableDraftJournalSha256")
            != witness.get("postResumeJournalSha256"),
            "gold binding E09 resumed editable add+undo journal must differ and advance by two commands",
            failures,
        )
        require(
            witness.get("preExitDraftGeometrySha256")
            == witness.get("postResumeDraftGeometrySha256")
            == witness.get("resumedEditableDraftGeometrySha256")
            and witness.get("preExitDraftProjectionSha256")
            == witness.get("postResumeDraftProjectionSha256")
            == witness.get("resumedEditableDraftProjectionSha256"),
            "gold binding E09 content-derived draft geometry/projection hashes must restore exactly",
            failures,
        )
        e09_rows = {
            row.get("checkpointId"): row
            for row in checkpoint_rows
            if isinstance(row, dict) and row.get("episodeId") == "E09-MID-RESUME"
        }
        for checkpoint_id in ("mid-save-before-exit", "resume-orientation"):
            row = e09_rows.get(checkpoint_id, {})
            require(
                row.get("journalBinding", {}).get("sha256")
                == witness.get("preExitJournalSha256")
                and row.get("snapshotBinding", {}).get("sha256")
                == witness.get("preExitSnapshotSha256")
                and row.get("journalBinding", {}).get("commandCount")
                == witness.get("preExitCommandCount"),
                f"gold binding E09 {checkpoint_id} does not match exact pre/resume bytes",
                failures,
            )
        e09_prefix = next(
            (
                row
                for row in prefix_rows
                if isinstance(row, dict)
                and row.get("prefixId") == "PREFIX-NORTH-BANK-MID-DRAFT"
            ),
            {},
        )
        require(
            e09_prefix.get("journalBinding", {}).get("sha256")
            == witness.get("preExitJournalSha256")
            and e09_prefix.get("snapshotBinding", {}).get("sha256")
            == witness.get("preExitSnapshotSha256")
            and e09_prefix.get("journalBinding", {}).get("commandCount")
            == witness.get("preExitCommandCount"),
            "gold binding E09 mid-draft prefix does not match exact pre-exit bytes",
            failures,
        )
        editable = e09_rows.get("resumed-editable-draft", {})
        require(
            editable.get("journalBinding", {}).get("sha256")
            == witness.get("resumedEditableDraftJournalSha256")
            and editable.get("snapshotBinding", {}).get("sha256")
            == witness.get("resumedEditableDraftSnapshotSha256")
            and editable.get("journalBinding", {}).get("commandCount")
            == witness.get("resumedEditableDraftCommandCount"),
            "gold binding E09 resumed editable add+undo bytes are not separately bound",
            failures,
        )
        validate_replay_derived_e09(replay_reports, witness, failures)
    if candidate is not None:
        require(
            binding.get("candidateManifestSha256")
            == candidate.get("candidateManifestSha256"),
            "gold binding manifest candidate self-hash mismatch",
            failures,
        )
    generator = manifest.get("nextRequiredGenerator", {})
    try:
        generator_path = repo_path(root, generator.get("path"), "nextRequiredGenerator.path")
    except ContractError as error:
        failures.append(str(error))
    else:
        require(generator_path.is_file(), "gold binding generator tool is missing", failures)
        if generator_path.is_file():
            require(
                binding.get("generatorToolSha256")
                == sha256_bytes(generator_path.read_bytes()),
                "gold binding generator tool raw SHA mismatch",
                failures,
            )
    return len(failures) == failure_count_before


def _load_contract_validator(path: Path) -> Any:
    """Load the sibling validator in-process so runtime paths are never reopened."""
    spec = importlib.util.spec_from_file_location(
        "gridworks_commercial_ux_validate_contract",
        path,
    )
    if spec is None or spec.loader is None:
        raise ContractError(f"cannot load native contract validator: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def validate_exact_inputs(
    root: Path,
    manifest_path: Path,
    story_manifest_bytes: bytes | None,
    run_story_manifest: bool,
    candidate_manifest_bytes: bytes | None,
    binding_manifest_bytes: bytes | None,
    require_score_ready: bool,
    *,
    story_manifest_path_label: Path | None = None,
    candidate_manifest_path_label: Path | None = None,
    binding_manifest_path_label: Path | None = None,
    holdout_consumption_receipt_bytes: bytes | None = None,
    registry_before_bytes: bytes | None = None,
    registry_after_bytes: bytes | None = None,
    evaluation_session_claim_bytes: bytes | None = None,
    holdout_consumption_receipt_path_label: Path | None = None,
    registry_before_path_label: Path | None = None,
    registry_after_path_label: Path | None = None,
    evaluation_session_claim_path_label: Path | None = None,
) -> tuple[list[str], dict[str, Any]]:
    """Validate already-opened candidate/gold bytes and report their raw hashes.

    The three path-label arguments are diagnostic only and are never opened.
    Candidate component files and the gold replay bundle are still opened from
    their content-addressed declarations because validating those bytes is the
    purpose of this authority.
    """

    failures: list[str] = []
    try:
        manifest_bytes = manifest_path.read_bytes()
        manifest = load_json_bytes(manifest_bytes, str(manifest_path))
    except (OSError, ContractError) as error:
        return [str(error)], {}
    if not isinstance(manifest, dict):
        return ["gold-state manifest must be a JSON object"], {}
    manifest_raw_sha256 = sha256_bytes(manifest_bytes)
    observed_raw_sha256: dict[str, str] = {
        "goldStateManifestRawSha256": manifest_raw_sha256,
    }

    candidate: dict[str, Any] | None = None
    if candidate_manifest_bytes is not None:
        observed_raw_sha256["candidateManifestRawSha256"] = sha256_bytes(
            candidate_manifest_bytes
        )
        try:
            candidate_value = load_json_bytes(
                candidate_manifest_bytes,
                str(candidate_manifest_path_label or "candidate manifest bytes"),
            )
        except ContractError as error:
            failures.append(str(error))
        else:
            if isinstance(candidate_value, dict):
                candidate = candidate_value
            else:
                failures.append("candidate manifest must be a JSON object")

    binding_manifest: dict[str, Any] | None = None
    if binding_manifest_bytes is not None:
        observed_raw_sha256["goldBindingManifestRawSha256"] = sha256_bytes(
            binding_manifest_bytes
        )
        try:
            binding_value = load_json_bytes(
                binding_manifest_bytes,
                str(binding_manifest_path_label or "gold binding manifest bytes"),
            )
        except ContractError as error:
            failures.append(str(error))
        else:
            if isinstance(binding_value, dict):
                binding_manifest = binding_value
            else:
                failures.append("gold binding manifest must be a JSON object")

    session_claim: dict[str, Any] | None = None
    runtime_inputs = (
        (
            "holdoutConsumptionReceiptRawSha256",
            holdout_consumption_receipt_bytes,
        ),
        ("registryBeforeRawSha256", registry_before_bytes),
        ("registryAfterRawSha256", registry_after_bytes),
        ("evaluationSessionClaimRawSha256", evaluation_session_claim_bytes),
    )
    for field, data in runtime_inputs:
        if data is not None:
            observed_raw_sha256[field] = sha256_bytes(data)
    if evaluation_session_claim_bytes is not None:
        try:
            session_value = load_json_bytes(
                evaluation_session_claim_bytes,
                str(evaluation_session_claim_path_label or "evaluation session claim bytes"),
            )
        except ContractError as error:
            failures.append(str(error))
        else:
            if isinstance(session_value, dict):
                session_claim = session_value
            else:
                failures.append("evaluation session claim must be a JSON object")

    contract_validator: Any | None = None
    contract_runtime_valid = False
    if any(
        data is not None
        for data in (
            candidate_manifest_bytes,
            binding_manifest_bytes,
            holdout_consumption_receipt_bytes,
            registry_before_bytes,
            registry_after_bytes,
            evaluation_session_claim_bytes,
        )
    ):
        contract_validator_path = manifest_path.parent / "validate-contract.py"
        try:
            contract_validator = _load_contract_validator(contract_validator_path)
            contract_errors, contract_summary = (
                contract_validator.validate_runtime_contract_bytes(
                    manifest_path.parent,
                    manifest_path.parent.parent / "rubric.json",
                    candidate_manifest_bytes=candidate_manifest_bytes,
                    gold_binding_manifest_bytes=binding_manifest_bytes,
                    holdout_consumption_receipt_bytes=(
                        holdout_consumption_receipt_bytes
                    ),
                    registry_before_bytes=registry_before_bytes,
                    registry_after_bytes=registry_after_bytes,
                    evaluation_session_claim_bytes=evaluation_session_claim_bytes,
                    candidate_manifest_path_label=candidate_manifest_path_label,
                    gold_binding_manifest_path_label=binding_manifest_path_label,
                    holdout_consumption_receipt_path_label=(
                        holdout_consumption_receipt_path_label
                    ),
                    registry_before_path_label=registry_before_path_label,
                    registry_after_path_label=registry_after_path_label,
                    evaluation_session_claim_path_label=(
                        evaluation_session_claim_path_label
                    ),
                )
            )
        except (OSError, AttributeError, ContractError) as error:
            failures.append(f"native contract exact-byte validation did not run: {error}")
        else:
            contract_runtime_valid = not contract_errors
            failures.extend(
                f"native contract exact-byte validation: {error}"
                for error in contract_errors
            )
            contract_observed = contract_summary.get("observedRawSha256", {})
            expected_contract_observed: dict[str, str] = {}
            if candidate_manifest_bytes is not None:
                expected_contract_observed["candidateManifestRawSha256"] = (
                    observed_raw_sha256["candidateManifestRawSha256"]
                )
            if binding_manifest_bytes is not None:
                expected_contract_observed["goldBindingManifestRawSha256"] = (
                    observed_raw_sha256["goldBindingManifestRawSha256"]
                )
            for field, data in runtime_inputs:
                if data is not None:
                    expected_contract_observed[field] = observed_raw_sha256[field]
            require(
                contract_observed == expected_contract_observed,
                "native contract observed raw SHA projection mismatch",
                failures,
            )

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

    require(
        manifest.get("candidateBindingOverlay") == {
            "schema": "gold-binding-manifest.schema.json",
            "selfHashField": "goldBindingManifestSha256",
            "rawHashOwner": "evaluation-run manifest artifacts.goldBindingManifestRawSha256",
            "prefixCount": 12,
            "checkpointCount": 49,
            "applicableBindingCount": 56,
            "notApplicableBindingCount": 5,
            "e09WitnessRequired": True,
            "rawBundleRequired": True,
            "rawBundleEntryCount": 112,
            "rawBundleValidation": "OPEN_EXACT_BYTES_RECOMPUTE_RAW_HASH_LENGTH_REJECT_EXTRA_SYMLINK_THEN_CORE_DESERIALIZE_RESTORE_AND_EXACT_CANONICAL_SNAPSHOT_COMPARE",
            "immutableTemplateBindingsRemainPending": True,
            "scoreReadyCommandRequires": [
                "--binding-manifest", "--candidate-manifest", "--require-score-ready"
            ],
        },
        "candidateBindingOverlay contract drift",
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
        "goldReplayVerifier",
    )
    immutable_file_authorities = {
        "coverageRecipe", "conceptExposure", "goldReplayVerifier"
    }
    immutable_json_authorities = {"coverageRecipe", "conceptExposure"}
    semantic_json_authorities = {"world", "campaign"}
    semantic_code_authorities = {
        "coreReplay",
        "coreContracts",
        "deterministicWitness",
        "nativeSmokeWitness",
        "storyHarness",
        "goldReplayVerifier",
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
            expected_reference_mode = (
                "EXACT_RAW_SHA_AND_CORE_REPLAY"
                if name == "goldReplayVerifier"
                else "PATH_AND_MEMBER_SEMANTIC"
            )
            require(
                authority.get("referenceMode") == expected_reference_mode,
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
        if name == "goldReplayVerifier":
            for path_field, hash_field in (
                ("entrypointPath", "entrypointSha256"),
                ("projectPath", "projectSha256"),
                ("buildInputsPath", "buildInputsSha256"),
            ):
                try:
                    exact_path = repo_path(
                        root,
                        authority.get(path_field),
                        f"authorities.{name}.{path_field}",
                    )
                except ContractError as error:
                    failures.append(str(error))
                    continue
                require(
                    exact_path.is_file(),
                    f"authorities.{name}.{path_field} does not exist",
                    failures,
                )
                if exact_path.is_file():
                    require(
                        authority.get(hash_field)
                        == sha256_bytes(exact_path.read_bytes()),
                        f"authorities.{name}.{hash_field} raw SHA mismatch",
                        failures,
                    )
            require(
                authority.get("buildInputsPath")
                == GOLD_REPLAY_BUILD_INPUT_MANIFEST_PATH,
                "authorities.goldReplayVerifier.buildInputsPath mismatch",
                failures,
            )
            try:
                read_gold_replay_build_inputs(
                    root,
                    root / GOLD_REPLAY_BUILD_INPUT_MANIFEST_PATH,
                )
            except (ContractError, OSError) as error:
                failures.append(f"gold replay build inputs are invalid: {error}")
        if path.is_file():
            if name in immutable_json_authorities | semantic_json_authorities:
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
        story_data = story_manifest_bytes
        story_label = str(story_manifest_path_label or "story manifest bytes")
        if story_manifest_bytes is not None:
            observed_raw_sha256["storyManifestRawSha256"] = sha256_bytes(
                story_manifest_bytes
            )
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
    if candidate is not None:
        candidate_execution_valid = validate_candidate_execution_manifest(
            root,
            candidate,
            manifest_raw_sha256,
            manifest,
            observed_story_sha256,
            failures,
        )

    binding_manifest_valid = False
    if binding_manifest is not None:
        binding_manifest_valid = validate_candidate_binding_overlay(
            root,
            binding_manifest,
            manifest_path,
            manifest_raw_sha256,
            manifest,
            candidate,
            failures,
        )

    if require_score_ready:
        require(
            candidate_manifest_bytes is not None,
            "score-bearing readiness requires --candidate-manifest; gold template bindings never substitute for candidate execution hashes",
            failures,
        )
        require(
            candidate_execution_valid,
            "score-bearing readiness requires a valid candidate manifest bound to this gold contract",
            failures,
        )
        require(
            binding_manifest_bytes is not None,
            "score-bearing readiness requires --binding-manifest; immutable template bindings are never mutated",
            failures,
        )
        require(
            binding_manifest_valid,
            "score-bearing readiness requires a valid candidate-specific binding manifest",
            failures,
        )
        require(
            holdout_consumption_receipt_bytes is not None,
            "score-bearing readiness requires --holdout-consumption-receipt",
            failures,
        )
        require(
            registry_before_bytes is not None and registry_after_bytes is not None,
            "score-bearing readiness requires --registry-before and --registry-after exact transition bytes",
            failures,
        )
        require(
            evaluation_session_claim_bytes is not None,
            "score-bearing readiness requires --evaluation-session-claim created before capture",
            failures,
        )
        require(
            contract_runtime_valid,
            "score-bearing readiness requires a valid canonical holdout transition and evaluation session claim",
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
        if contract_validator is None:
            failures.append(
                "score-bearing readiness could not load the native contract readiness authority"
            )
            contract_readiness_errors = ["contract validator unavailable"]
        else:
            try:
                contract_readiness_errors = (
                    contract_validator.score_bearing_contract_readiness_errors(
                        manifest_path.parent,
                        evaluation_session_claim=session_claim,
                    )
                )
            except (OSError, AttributeError, ContractError) as error:
                contract_readiness_errors = [
                    f"native contract readiness authority failed: {error}"
                ]
            failures.extend(
                f"score-bearing readiness: {error}"
                for error in contract_readiness_errors
            )
    else:
        contract_readiness_errors = ["not requested"]

    summary = {
        "episodes": len(manifest_episode_ids),
        "checkpoints": len(seen_checkpoints),
        "blockingOwners": len(blocking_owners),
        "pendingOwners": sum(status == PENDING for status in status_owners.values()),
        "unboundOwners": sum(status == UNBOUND for status in status_owners.values()),
        "scoreBearingReady": (
            candidate_execution_valid
            and binding_manifest_valid
            and contract_runtime_valid
            and not contract_readiness_errors
            and isinstance(next_generator, dict)
            and (root / str(next_generator.get("path", ""))).is_file()
        ),
        "storyOutputSha256": observed_story_sha256,
        "observedRawSha256": observed_raw_sha256,
    }
    return failures, summary


def _read_optional_bytes(
    path: Path | None,
    label: str,
    failures: list[str],
) -> tuple[bytes | None, Path | None]:
    if path is None:
        return None, None
    resolved = path.resolve(strict=False)
    try:
        return resolved.read_bytes(), resolved
    except OSError as error:
        failures.append(f"cannot read {label} {resolved}: {error}")
        return None, resolved


def validate(
    root: Path,
    manifest_path: Path,
    story_manifest_path: Path | None,
    run_story_manifest: bool,
    candidate_execution_manifest_path: Path | None,
    binding_manifest_path: Path | None,
    require_score_ready: bool,
    holdout_consumption_receipt_path: Path | None = None,
    registry_before_path: Path | None = None,
    registry_after_path: Path | None = None,
    evaluation_session_claim_path: Path | None = None,
) -> tuple[list[str], dict[str, Any]]:
    """Path-based CLI compatibility wrapper that opens each runtime file once."""

    read_failures: list[str] = []
    story_bytes, story_label = _read_optional_bytes(
        story_manifest_path,
        "story manifest",
        read_failures,
    )
    candidate_bytes, candidate_label = _read_optional_bytes(
        candidate_execution_manifest_path,
        "candidate manifest",
        read_failures,
    )
    binding_bytes, binding_label = _read_optional_bytes(
        binding_manifest_path,
        "gold binding manifest",
        read_failures,
    )
    receipt_bytes, receipt_label = _read_optional_bytes(
        holdout_consumption_receipt_path,
        "holdout consumption receipt",
        read_failures,
    )
    registry_before_bytes, registry_before_label = _read_optional_bytes(
        registry_before_path,
        "registry before",
        read_failures,
    )
    registry_after_bytes, registry_after_label = _read_optional_bytes(
        registry_after_path,
        "registry after",
        read_failures,
    )
    session_bytes, session_label = _read_optional_bytes(
        evaluation_session_claim_path,
        "evaluation session claim",
        read_failures,
    )
    failures, summary = validate_exact_inputs(
        root,
        manifest_path,
        story_bytes,
        run_story_manifest,
        candidate_bytes,
        binding_bytes,
        require_score_ready,
        story_manifest_path_label=story_label,
        candidate_manifest_path_label=candidate_label,
        binding_manifest_path_label=binding_label,
        holdout_consumption_receipt_bytes=receipt_bytes,
        registry_before_bytes=registry_before_bytes,
        registry_after_bytes=registry_after_bytes,
        evaluation_session_claim_bytes=session_bytes,
        holdout_consumption_receipt_path_label=receipt_label,
        registry_before_path_label=registry_before_label,
        registry_after_path_label=registry_after_label,
        evaluation_session_claim_path_label=session_label,
    )
    return [*read_failures, *failures], summary


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
    parser.add_argument("--binding-manifest", type=Path)
    parser.add_argument("--holdout-consumption-receipt", type=Path)
    parser.add_argument("--registry-before", type=Path)
    parser.add_argument("--registry-after", type=Path)
    parser.add_argument("--evaluation-session-claim", type=Path)
    parser.add_argument("--require-score-ready", action="store_true")
    parser.add_argument("--json", action="store_true")
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
        args.binding_manifest.resolve() if args.binding_manifest is not None else None,
        args.require_score_ready,
        (
            args.holdout_consumption_receipt.resolve()
            if args.holdout_consumption_receipt is not None
            else None
        ),
        args.registry_before.resolve() if args.registry_before is not None else None,
        args.registry_after.resolve() if args.registry_after is not None else None,
        (
            args.evaluation_session_claim.resolve()
            if args.evaluation_session_claim is not None
            else None
        ),
    )
    if args.json:
        print(
            json.dumps(
                {
                    **summary,
                    "status": "PASS" if not failures else "FAIL",
                    "errors": failures,
                },
                ensure_ascii=False,
                sort_keys=True,
            )
        )
        return 1 if failures else 0
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
