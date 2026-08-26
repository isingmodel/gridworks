#!/usr/bin/env python3
"""Run or reconstruct the bounded current-R2 app persistence qualification."""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import pwd
import re
import shutil
import stat
import subprocess
import sys
import tempfile
from typing import Any

import r2_candidate as candidate


ROOT = candidate.ROOT
GAME = candidate.GAME
TOOL_PATH = ROOT / "tools/r2_qualification.py"
SCHEMA = "gridworks.r2-app-persistence-qualification.v2"
RECORD_NAME = "Gridworks-current-r2-macOS-internal.qualification.json"
RUNNER_SCENE = "res://realtime/r2/RealtimeProductEntrySmokeRunner.tscn"
MARKER_PREFIX = "REALTIME_R2_QUALIFICATION_DATA_READY "
LIFECYCLE_MARKER_PREFIX = "REALTIME_R2_QUALIFICATION_LIFECYCLE_READY "
TITLE_MARKER = candidate.TITLE_MARKER

SETTINGS_FILE = "realtime-settings-v1.json"
SAVE_FILE = "gridworks-r2-campaign-save-v1.json"
PRODUCT_ROUTE_ID = "--release-through=LONGEST_NIGHT"

LIFECYCLE_EXPECTATIONS = {
    "EMPTY_NEW_GAME": {
        "pointerInputs": 6,
        "keyInputs": 0,
        "title": "HIDDEN",
        "session": "INITIAL_BRIEFING",
        "settings": "DEFAULT",
        "save": "MISSING_TO_INITIAL",
        "dataSettings": "MISSING",
        "continuation": "MISSING",
    },
    "PROGRESS_CONTINUE": {
        "pointerInputs": 3,
        "keyInputs": 0,
        "title": "HIDDEN",
        "session": "INITIAL_BRIEFING",
        "settings": "DEFAULT",
        "save": "PROGRESS_UNCHANGED",
        "dataSettings": "MISSING",
        "continuation": "RESTORABLE",
    },
    "COMPLETED_CONTINUE": {
        "pointerInputs": 3,
        "keyInputs": 0,
        "title": "HIDDEN",
        "session": "ENDED",
        "settings": "DEFAULT",
        "save": "COMPLETED_UNCHANGED",
        "dataSettings": "MISSING",
        "continuation": "COMPLETED",
    },
    "COMPLETED_NEW_GAME": {
        "pointerInputs": 3,
        "keyInputs": 0,
        "title": "HIDDEN",
        "session": "INITIAL_BRIEFING",
        "settings": "DEFAULT",
        "save": "COMPLETED_TO_INITIAL",
        "dataSettings": "MISSING",
        "continuation": "COMPLETED",
    },
    "RESET_NEW_GAME": {
        "pointerInputs": 6,
        "keyInputs": 0,
        "title": "HIDDEN",
        "session": "INITIAL_BRIEFING",
        "settings": "DEFAULT",
        "save": "PROGRESS_TO_INITIAL_BACKUP",
        "dataSettings": "MISSING",
        "continuation": "RESTORABLE",
    },
    "SETTINGS_APPLY": {
        "pointerInputs": 39,
        "keyInputs": 2,
        "title": "VISIBLE",
        "session": "NONE",
        "settings": "APPLIED",
        "save": "MISSING",
        "dataSettings": "MISSING",
        "continuation": "MISSING",
    },
    "SETTINGS_RESTORE": {
        "pointerInputs": 3,
        "keyInputs": 2,
        "title": "VISIBLE",
        "session": "NONE",
        "settings": "RESTORED",
        "save": "MISSING",
        "dataSettings": "LOADED",
        "continuation": "MISSING",
    },
}
RESET_BACKUP_PATTERN = f"{SAVE_FILE}.reset-<32-lowercase-hex>.bak"
RESET_BACKUP_NAME = re.compile(
    rf"^{re.escape(SAVE_FILE)}\.reset-[0-9a-f]{{32}}\.bak$"
)


def fail(message: str) -> None:
    raise candidate.CandidateError(message)


def combined_output(
    result: subprocess.CompletedProcess[str],
    log_path: Path,
) -> str:
    return result.stdout + result.stderr + read_log(log_path)


def read_log(log_path: Path) -> str:
    if not log_path.is_file() or log_path.is_symlink():
        fail(f"qualification log is missing or unsafe: {log_path.name}")
    return log_path.read_text(encoding="utf-8", errors="replace")


def require_success_output(
    output: str,
    marker: str,
    label: str,
    *,
    allowed_error_prefixes: tuple[str, ...] = (),
) -> None:
    if marker not in output:
        fail(f"{label} did not emit {marker}")
    error_lines = {
        line.strip()
        for line in output.splitlines()
        if line.lstrip().upper().startswith(("ERROR:", "SCRIPT ERROR:"))
    }
    unexpected = sorted(
        line
        for line in error_lines
        if not any(line.startswith(prefix) for prefix in allowed_error_prefixes)
    )
    if unexpected:
        fail(f"{label} logged an unexpected error: {unexpected[0]}")


def run_source_smoke(
    argument: str,
    expected_marker: str,
    log_path: Path,
    *,
    allowed_error_prefixes: tuple[str, ...] = (),
) -> None:
    environment = dict(os.environ)
    environment.pop(candidate.QUALIFICATION_DATA_ENV, None)
    environment.pop(candidate.QUALIFICATION_SCENARIO_ENV, None)
    environment.pop("GridworksCurrentR2Export", None)
    environment.pop("GridworksLegacyV2Export", None)
    result = candidate.run(
        [
            candidate.godot_path(),
            "--headless",
            "--path",
            str(GAME),
            "--scene",
            RUNNER_SCENE,
            "--log-file",
            str(log_path),
            "--",
            argument,
        ],
        cwd=ROOT,
        env=environment,
        timeout=300,
    )
    require_success_output(
        combined_output(result, log_path),
        expected_marker,
        f"source actual-scene stage {expected_marker}",
        allowed_error_prefixes=allowed_error_prefixes,
    )


def exact_qualification_marker(
    output: str,
    *,
    settings: str,
    continuation: str,
) -> str:
    expected = (
        f"{MARKER_PREFIX}user_args=0 settings={settings} "
        f"continuation={continuation}"
    )
    markers = [
        line.strip()
        for line in output.splitlines()
        if line.strip().startswith(MARKER_PREFIX)
    ]
    if markers != [expected]:
        fail(
            "packaged qualification marker drift: "
            f"expected one {expected!r}, got {markers!r}"
        )
    return expected


def exact_title_marker(output: str, label: str) -> None:
    markers = [
        line.strip()
        for line in output.splitlines()
        if line.strip() == TITLE_MARKER
    ]
    if markers != [TITLE_MARKER]:
        fail(
            f"{label} title marker drift: expected exactly one "
            f"{TITLE_MARKER!r}, got {markers!r}"
        )


def exact_lifecycle_marker(output: str, scenario: str) -> str:
    expectation = LIFECYCLE_EXPECTATIONS[scenario]
    markers = [
        line.strip()
        for line in output.splitlines()
        if line.strip().startswith(LIFECYCLE_MARKER_PREFIX)
    ]
    if len(markers) != 1:
        fail(
            f"packaged lifecycle marker count drift for {scenario}: "
            f"expected one, got {markers!r}"
        )
    pattern = re.compile(
        rf"^{re.escape(LIFECYCLE_MARKER_PREFIX)}"
        rf"scenario={re.escape(scenario)} "
        rf"pointer_inputs={expectation['pointerInputs']} "
        rf"key_inputs={expectation['keyInputs']} "
        rf"title={expectation['title']} "
        rf"session={expectation['session']} "
        rf"settings={expectation['settings']} "
        rf"save={expectation['save']} "
        r"audio=AMBIENT_READY_SFX_QUIET$"
    )
    match = pattern.fullmatch(markers[0])
    if match is None:
        fail(
            f"packaged lifecycle marker field drift for {scenario}: "
            f"{markers[0]!r}"
        )
    return markers[0]


def require_no_lifecycle_marker(output: str, label: str) -> None:
    markers = [
        line.strip()
        for line in output.splitlines()
        if line.strip().startswith(LIFECYCLE_MARKER_PREFIX)
    ]
    if markers:
        fail(f"{label} unexpectedly emitted lifecycle markers: {markers!r}")


def run_packaged_title(
    app: Path,
    data_root: Path,
    log_path: Path,
    *,
    settings: str,
    continuation: str,
    expected_root_names: set[str],
) -> str:
    executable = app / "Contents/MacOS/Gridworks"
    if not executable.is_file() or executable.is_symlink():
        fail("qualified package executable is missing")
    environment = dict(os.environ)
    environment[candidate.QUALIFICATION_DATA_ENV] = str(data_root)
    environment.pop(candidate.QUALIFICATION_SCENARIO_ENV, None)
    environment.pop("GridworksCurrentR2Export", None)
    environment.pop("GridworksLegacyV2Export", None)
    before_root = root_files(data_root, expected_root_names)
    result = candidate.run(
        [
            str(executable),
            "--headless",
            "--audio-driver",
            "Dummy",
            "--quit-after",
            "8",
            "--log-file",
            str(log_path),
        ],
        cwd=log_path.parent,
        env=environment,
        timeout=60,
    )
    output = combined_output(result, log_path)
    require_success_output(output, TITLE_MARKER, "packaged product-title stage")
    log_output = read_log(log_path)
    exact_title_marker(log_output, "packaged product-title stage")
    require_no_lifecycle_marker(log_output, "packaged product-title stage")
    marker = exact_qualification_marker(
        log_output,
        settings=settings,
        continuation=continuation,
    )
    if root_files(data_root, expected_root_names) != before_root:
        fail("packaged product-title stage changed app-owned persistence bytes")
    return marker


def run_packaged_lifecycle(
    app: Path,
    data_root: Path,
    log_path: Path,
    scenario: str,
    *,
    expected_root_names: set[str],
) -> str:
    expectation = LIFECYCLE_EXPECTATIONS.get(scenario)
    if expectation is None:
        fail(f"unknown packaged lifecycle scenario: {scenario}")
    executable = app / "Contents/MacOS/Gridworks"
    if not executable.is_file() or executable.is_symlink():
        fail("qualified package executable is missing")
    root_files(data_root, expected_root_names)
    environment = dict(os.environ)
    environment[candidate.QUALIFICATION_DATA_ENV] = str(data_root)
    environment[candidate.QUALIFICATION_SCENARIO_ENV] = scenario
    environment.pop("GridworksCurrentR2Export", None)
    environment.pop("GridworksLegacyV2Export", None)
    result = candidate.run(
        [
            str(executable),
            "--headless",
            "--audio-driver",
            "Dummy",
            "--log-file",
            str(log_path),
        ],
        cwd=log_path.parent,
        env=environment,
        timeout=60,
    )
    output = combined_output(result, log_path)
    require_success_output(
        output,
        LIFECYCLE_MARKER_PREFIX,
        f"packaged lifecycle stage {scenario}",
    )
    log_output = read_log(log_path)
    exact_title_marker(log_output, f"packaged lifecycle stage {scenario}")
    exact_qualification_marker(
        log_output,
        settings=expectation["dataSettings"],
        continuation=expectation["continuation"],
    )
    return exact_lifecycle_marker(log_output, scenario)


def root_files(data_root: Path, expected_names: set[str]) -> list[dict[str, Any]]:
    root_metadata = data_root.lstat()
    if not stat.S_ISDIR(root_metadata.st_mode) or data_root.is_symlink():
        fail("qualification data root stopped being a real directory")
    if data_root.resolve(strict=True) != data_root:
        fail("qualification data root stopped being canonical")
    actual_names: set[str] = set()
    rows: list[dict[str, Any]] = []
    for path in sorted(data_root.iterdir(), key=lambda item: item.name.encode("utf-8")):
        metadata = path.lstat()
        if not stat.S_ISREG(metadata.st_mode) or path.is_symlink():
            fail(f"qualification root contains a non-regular entry: {path.name}")
        actual_names.add(path.name)
        rows.append(
            {
                "fileName": path.name,
                "byteLength": metadata.st_size,
                "sha256": candidate.sha256_file(path),
            }
        )
    if actual_names != expected_names:
        fail(
            "qualification root closure drift: "
            f"expected {sorted(expected_names)}, got {sorted(actual_names)}"
        )
    return rows


def fresh_data_root(work: Path, name: str) -> Path:
    data_root = work / name
    data_root.mkdir(mode=0o700)
    data_root = data_root.resolve(strict=True)
    validate_empty_root(data_root)
    return data_root


def run_rejected_packaged_lifecycle(
    app: Path,
    guard_root: Path,
    log_path: Path,
    *,
    rejection_id: str,
    scenario: str,
    data_environment: Path,
    expected_error: str,
) -> dict[str, Any]:
    executable = app / "Contents/MacOS/Gridworks"
    if not executable.is_file() or executable.is_symlink():
        fail("qualified package executable is missing")
    before = root_files(guard_root, set())
    environment = dict(os.environ)
    environment[candidate.QUALIFICATION_DATA_ENV] = str(data_environment)
    environment[candidate.QUALIFICATION_SCENARIO_ENV] = scenario
    environment.pop("GridworksCurrentR2Export", None)
    environment.pop("GridworksLegacyV2Export", None)
    result = subprocess.run(
        [
            str(executable),
            "--headless",
            "--audio-driver",
            "Dummy",
            "--log-file",
            str(log_path),
        ],
        cwd=log_path.parent,
        env=environment,
        text=True,
        capture_output=True,
        timeout=60,
        check=False,
    )
    output = combined_output(result, log_path)
    if result.returncode != 1:
        fail(
            f"{rejection_id} returned {result.returncode}, expected fail-closed 1"
        )
    forbidden_markers = (
        TITLE_MARKER,
        MARKER_PREFIX,
        LIFECYCLE_MARKER_PREFIX,
    )
    if any(marker in output for marker in forbidden_markers):
        fail(f"{rejection_id} reached a qualification-ready marker")
    log_output = read_log(log_path)
    error_lines = [
        line.strip()
        for line in log_output.splitlines()
        if line.lstrip().upper().startswith(("ERROR:", "SCRIPT ERROR:"))
    ]
    if error_lines != [f"ERROR: {expected_error}"]:
        fail(
            f"{rejection_id} error output drift: "
            f"expected one {expected_error!r}, got {error_lines!r}"
        )
    after = root_files(guard_root, set())
    if after != before:
        fail(f"{rejection_id} changed its guard root")
    return {
        "exitCode": result.returncode,
        "id": rejection_id,
        "readyMarkersEmitted": False,
        "rootFiles": after,
    }


def copy_fixture(source: Path, target: Path) -> None:
    if not source.is_file() or source.is_symlink():
        fail(f"qualification fixture source is unsafe: {source.name}")
    if target.exists() or target.is_symlink():
        fail(f"qualification fixture target was not absent: {target.name}")
    shutil.copyfile(source, target)
    if not target.is_file() or target.is_symlink():
        fail(f"qualification fixture copy is unsafe: {target.name}")


def require_fixture_bytes(path: Path, fixture: Path, label: str) -> None:
    if (
        not fixture.is_file()
        or fixture.is_symlink()
        or not path.is_file()
        or path.is_symlink()
        or path.stat().st_size != fixture.stat().st_size
        or candidate.sha256_file(path) != candidate.sha256_file(fixture)
    ):
        fail(f"{label} differs from its source actual-scene fixture bytes")


def normalized_reset_root_files(
    data_root: Path,
    primary_fixture: Path,
    backup_fixture: Path,
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    root_metadata = data_root.lstat()
    if not stat.S_ISDIR(root_metadata.st_mode) or data_root.is_symlink():
        fail("reset lifecycle data root stopped being a real directory")
    if data_root.resolve(strict=True) != data_root:
        fail("reset lifecycle data root stopped being canonical")
    names = {path.name for path in data_root.iterdir()}
    backups = sorted(name for name in names if RESET_BACKUP_NAME.fullmatch(name))
    if names != {SAVE_FILE, *backups} or len(backups) != 1:
        fail(
            "reset lifecycle root closure drift: expected the primary and one "
            f"GUID sibling backup, got {sorted(names)}"
        )
    backup_name = backups[0]
    primary = data_root / SAVE_FILE
    backup = data_root / backup_name
    require_fixture_bytes(primary, primary_fixture, "reset primary save")
    require_fixture_bytes(backup, backup_fixture, "reset sibling backup")
    rows = root_files(data_root, names)
    backup_row: dict[str, Any] | None = None
    for row in rows:
        if row["fileName"] == backup_name:
            row["fileName"] = RESET_BACKUP_PATTERN
            backup_row = dict(row)
    if backup_row is None:
        fail("reset lifecycle normalized backup metadata is missing")
    rows.sort(key=lambda row: row["fileName"].encode("utf-8"))
    return rows, backup_row


def verified_account_home() -> Path:
    raw_environment_home = os.environ.get("HOME")
    if raw_environment_home is None or not Path(raw_environment_home).is_absolute():
        fail("qualification requires the current account HOME")
    try:
        account_home = Path(pwd.getpwuid(os.getuid()).pw_dir).resolve(strict=True)
        environment_home = Path(raw_environment_home).resolve(strict=True)
    except (KeyError, OSError) as exception:
        fail(f"qualification could not resolve the current account HOME: {exception}")
    if environment_home != account_home or Path.home().resolve(strict=True) != account_home:
        fail("qualification refuses a reassigned HOME")
    return account_home


def default_product_file_snapshot(
    account_home: Path,
) -> dict[str, dict[str, Any] | None]:
    default_root = (
        account_home
        / "Library/Application Support/Godot/app_userdata/Gridworks"
    )
    result: dict[str, dict[str, Any] | None] = {}
    for name in (SAVE_FILE, SETTINGS_FILE):
        path = default_root / name
        if not path.exists():
            result[name] = None
            continue
        metadata = path.lstat()
        if not stat.S_ISREG(metadata.st_mode) or path.is_symlink():
            fail(f"default product path is not a regular file: {name}")
        result[name] = {
            "byteLength": metadata.st_size,
            "sha256": candidate.sha256_file(path),
        }
    return result


def require_json_identity(path: Path, schema: str, label: str) -> dict[str, Any]:
    value = candidate.strict_json(path, label=label)
    if not isinstance(value, dict) or value.get("schemaVersion") != schema:
        fail(f"{label} schema drift")
    return value


def save_facts(path: Path) -> dict[str, Any]:
    value = require_json_identity(path, candidate.SAVE_SCHEMA, "qualification save")
    source = value.get("source")
    commands = value.get("commands")
    if (
        not isinstance(source, dict)
        or source.get("routeId") != PRODUCT_ROUTE_ID
        or not isinstance(commands, list)
        or type(value.get("closedStoryCount")) is not int
        or not isinstance(value.get("canonicalStateSha256"), str)
    ):
        fail("qualification save identity drift")
    return {
        "byteLength": path.stat().st_size,
        "canonicalStateSha256": value["canonicalStateSha256"],
        "closedStoryCount": value["closedStoryCount"],
        "commandCount": len(commands),
        "routeId": source["routeId"],
        "schemaVersion": value["schemaVersion"],
        "sha256": candidate.sha256_file(path),
    }


def settings_facts(path: Path) -> dict[str, Any]:
    value = require_json_identity(
        path,
        candidate.SETTINGS_SCHEMA,
        "qualification settings",
    )
    return {
        "byteLength": path.stat().st_size,
        "schemaVersion": value["schemaVersion"],
        "sha256": candidate.sha256_file(path),
    }


def validate_empty_root(data_root: Path) -> None:
    metadata = data_root.lstat()
    if not stat.S_ISDIR(metadata.st_mode) or data_root.is_symlink():
        fail("qualification data root is not a real directory")
    if data_root.resolve(strict=True) != data_root:
        fail("qualification data root is not canonical")
    if any(data_root.iterdir()):
        fail("qualification data root did not start exact-empty")


def package_identity(manifest_path: Path, manifest: dict[str, Any]) -> dict[str, Any]:
    package = manifest.get("package")
    source = manifest.get("source")
    if not isinstance(package, dict) or not isinstance(source, dict):
        fail("candidate manifest identity is malformed")
    return {
        "archiveFileName": candidate.ARCHIVE_NAME,
        "archiveSha256": package.get("sha256"),
        "manifestFileName": manifest_path.name,
        "manifestSha256": candidate.sha256_file(manifest_path),
        "sourceCommit": source.get("commit"),
        "treeSha256": package.get("treeSha256"),
    }


def run_unchanged_save_lifecycle(
    app: Path,
    work: Path,
    logs: Path,
    scenario: str,
    fixture: Path,
) -> dict[str, Any]:
    data_root = fresh_data_root(
        work,
        f"lifecycle-{scenario.lower().replace('_', '-')}",
    )
    save_path = data_root / SAVE_FILE
    copy_fixture(fixture, save_path)
    before = root_files(data_root, {SAVE_FILE})
    marker = run_packaged_lifecycle(
        app,
        data_root,
        logs / f"lifecycle-{scenario.lower().replace('_', '-')}.log",
        scenario,
        expected_root_names={SAVE_FILE},
    )
    require_fixture_bytes(save_path, fixture, f"{scenario} primary save")
    after = root_files(data_root, {SAVE_FILE})
    if after != before:
        fail(f"{scenario} changed the app-owned root bytes")
    return {
        "id": scenario,
        "marker": marker,
        "rootFilesAfter": after,
        "rootFilesBefore": before,
        "save": save_facts(save_path),
    }


def run_save_transition_lifecycle(
    app: Path,
    work: Path,
    logs: Path,
    scenario: str,
    *,
    source_fixture: Path | None,
    result_fixture: Path,
    expect_reset_backup: bool = False,
) -> dict[str, Any]:
    data_root = fresh_data_root(
        work,
        f"lifecycle-{scenario.lower().replace('_', '-')}",
    )
    save_path = data_root / SAVE_FILE
    expected_before: set[str] = set()
    if source_fixture is not None:
        copy_fixture(source_fixture, save_path)
        expected_before.add(SAVE_FILE)
    before = root_files(data_root, expected_before)
    marker = run_packaged_lifecycle(
        app,
        data_root,
        logs / f"lifecycle-{scenario.lower().replace('_', '-')}.log",
        scenario,
        expected_root_names=expected_before,
    )
    require_fixture_bytes(save_path, result_fixture, f"{scenario} primary save")
    source_sha256 = (
        None
        if source_fixture is None
        else candidate.sha256_file(source_fixture)
    )
    result_sha256 = candidate.sha256_file(result_fixture)
    same_primary_bytes = (
        None if source_sha256 is None else source_sha256 == result_sha256
    )
    if expect_reset_backup and same_primary_bytes is not True:
        fail(
            "the current reset probe requires the source-generated c0 progress "
            "fixture to equal the canonical initial result bytes"
        )
    result: dict[str, Any] = {
        "id": scenario,
        "marker": marker,
        "rootFilesBefore": before,
        "save": save_facts(save_path),
        "transition": {
            "resultSaveSha256": result_sha256,
            "samePrimaryBytes": same_primary_bytes,
            "sourceSaveSha256": source_sha256,
        },
    }
    if expect_reset_backup:
        if source_fixture is None:
            fail("a reset lifecycle requires source save bytes")
        after, backup = normalized_reset_root_files(
            data_root,
            result_fixture,
            source_fixture,
        )
        result["backup"] = backup
        result["rootFilesAfter"] = after
    else:
        result["rootFilesAfter"] = root_files(data_root, {SAVE_FILE})
    return result


def reconstruct(manifest_path: Path) -> dict[str, Any]:
    manifest_path = manifest_path.resolve()
    if manifest_path.name != candidate.MANIFEST_NAME:
        fail("qualification requires the fixed current R2 candidate manifest name")
    account_home = verified_account_home()
    before_default = default_product_file_snapshot(account_home)
    candidate.verify_manifest(manifest_path)
    manifest = candidate.strict_json(
        manifest_path,
        label="candidate manifest",
        canonical=True,
    )
    if not isinstance(manifest, dict):
        fail("candidate manifest must be an object")
    manifest_package = manifest.get("package")
    if not isinstance(manifest_package, dict):
        fail("candidate manifest package identity is malformed")
    manifest_sha256 = candidate.sha256_bytes(
        candidate.canonical_bytes(manifest) + b"\n"
    )
    source_archive = manifest_path.parent / candidate.ARCHIVE_NAME

    build_environment = dict(os.environ)
    build_environment.pop(candidate.QUALIFICATION_DATA_ENV, None)
    build_environment.pop(candidate.QUALIFICATION_SCENARIO_ENV, None)
    candidate.run(
        [
            candidate.require_tool("dotnet"),
            "build",
            str(ROOT / "Gridworks.sln"),
            "-c",
            "Debug",
            "--nologo",
            "-v:minimal",
        ],
        cwd=ROOT,
        env=build_environment,
        timeout=600,
    )

    with tempfile.TemporaryDirectory(prefix="gridworks-r2-qualification-") as raw:
        work = Path(raw).resolve(strict=True)
        pinned_root = work / "candidate"
        pinned_root.mkdir(mode=0o700)
        pinned_manifest = pinned_root / candidate.MANIFEST_NAME
        pinned_archive = pinned_root / candidate.ARCHIVE_NAME
        shutil.copyfile(manifest_path, pinned_manifest)
        shutil.copyfile(source_archive, pinned_archive)
        pinned_manifest.chmod(0o400)
        pinned_archive.chmod(0o400)
        if (
            candidate.sha256_file(pinned_manifest) != manifest_sha256
            or pinned_archive.stat().st_size
            != manifest_package.get("byteLength")
            or candidate.sha256_file(pinned_archive)
            != manifest_package.get("sha256")
        ):
            fail("candidate manifest/archive changed while being pinned")
        candidate.verify_manifest(pinned_manifest)

        extracted = work / "package"
        baseline_tree = candidate.extract_archive(pinned_archive, extracted)
        if (
            len(baseline_tree) != manifest_package.get("treeEntryCount")
            or candidate.sha256_bytes(candidate.canonical_bytes(baseline_tree))
            != manifest_package.get("treeSha256")
        ):
            fail("qualification extraction differs from candidate tree identity")
        app = extracted / "Gridworks.app"
        data_root = work / "product-data"
        data_root.mkdir(mode=0o700)
        data_root = data_root.resolve(strict=True)
        validate_empty_root(data_root)
        logs = work / "logs"
        logs.mkdir(mode=0o700)
        invalid_scenario_root = fresh_data_root(
            work,
            "lifecycle-invalid-scenario",
        )
        invalid_root_guard = fresh_data_root(
            work,
            "lifecycle-invalid-root-guard",
        )
        lifecycle_rejections = [
            run_rejected_packaged_lifecycle(
                app,
                invalid_scenario_root,
                logs / "lifecycle-invalid-scenario.log",
                rejection_id="INVALID_SCENARIO",
                scenario="NOT_A_QUALIFICATION_SCENARIO",
                data_environment=invalid_scenario_root,
                expected_error=(
                    "R2 qualification user-data rejected: "
                    "GRIDWORKS_R2_QUALIFICATION_SCENARIO is not a fixed "
                    "supported scenario."
                ),
            ),
            run_rejected_packaged_lifecycle(
                app,
                invalid_root_guard,
                logs / "lifecycle-invalid-root.log",
                rejection_id="INVALID_ROOT",
                scenario="EMPTY_NEW_GAME",
                data_environment=invalid_root_guard / "missing",
                expected_error=(
                    "R2 qualification user-data rejected: "
                    "GRIDWORKS_R2_QUALIFICATION_USER_DATA_DIR must already exist."
                ),
            ),
        ]
        fixtures = work / "source-fixtures"
        fixtures.mkdir(mode=0o700)
        settings_fixture = fixtures / "settings.json"
        progress_fixture = fixtures / "progress.json"
        completed_fixture = fixtures / "completed.json"

        stages: list[dict[str, Any]] = []
        stages.append(
            {
                "id": "EMPTY",
                "marker": run_packaged_title(
                    app,
                    data_root,
                    logs / "empty.log",
                    settings="MISSING",
                    continuation="MISSING",
                    expected_root_names=set(),
                ),
                "rootFiles": root_files(data_root, set()),
            }
        )

        settings_path = data_root / SETTINGS_FILE
        run_source_smoke(
            f"--settings-create={settings_path}",
            "REALTIME_PRODUCT_ENTRY_SETTINGS_CREATE_PASS",
            logs / "settings-source.log",
            allowed_error_prefixes=("ERROR: R2 settings save failed:",),
        )
        copy_fixture(settings_path, settings_fixture)
        settings = settings_facts(settings_path)
        stages.append(
            {
                "id": "SETTINGS_LOADED",
                "marker": run_packaged_title(
                    app,
                    data_root,
                    logs / "settings-package.log",
                    settings="LOADED",
                    continuation="MISSING",
                    expected_root_names={SETTINGS_FILE},
                ),
                "rootFiles": root_files(data_root, {SETTINGS_FILE}),
                "settings": settings,
            }
        )

        save_path = data_root / SAVE_FILE
        run_source_smoke(
            f"--save-create={save_path}",
            "REALTIME_PRODUCT_ENTRY_SAVE_CREATE_PASS",
            logs / "progress-source.log",
        )
        copy_fixture(save_path, progress_fixture)
        progress = save_facts(save_path)
        stages.append(
            {
                "id": "PROGRESS_RESTORABLE",
                "marker": run_packaged_title(
                    app,
                    data_root,
                    logs / "progress-package.log",
                    settings="LOADED",
                    continuation="RESTORABLE",
                    expected_root_names={SAVE_FILE, SETTINGS_FILE},
                ),
                "rootFiles": root_files(data_root, {SAVE_FILE, SETTINGS_FILE}),
                "save": progress,
            }
        )

        save_path.unlink()
        run_source_smoke(
            f"--save-completed-create={save_path}",
            "REALTIME_PRODUCT_ENTRY_SAVE_COMPLETED_CREATE_PASS",
            logs / "completed-source.log",
        )
        copy_fixture(save_path, completed_fixture)
        completed = save_facts(save_path)
        if completed["sha256"] == progress["sha256"]:
            fail("completed save is indistinguishable from initial progress")
        stages.append(
            {
                "id": "COMPLETED_RESTORABLE",
                "marker": run_packaged_title(
                    app,
                    data_root,
                    logs / "completed-package.log",
                    settings="LOADED",
                    continuation="COMPLETED",
                    expected_root_names={SAVE_FILE, SETTINGS_FILE},
                ),
                "rootFiles": root_files(data_root, {SAVE_FILE, SETTINGS_FILE}),
                "save": completed,
            }
        )

        lifecycle_stages = [
            run_save_transition_lifecycle(
                app,
                work,
                logs,
                "EMPTY_NEW_GAME",
                source_fixture=None,
                result_fixture=progress_fixture,
            ),
            run_unchanged_save_lifecycle(
                app,
                work,
                logs,
                "PROGRESS_CONTINUE",
                progress_fixture,
            ),
            run_unchanged_save_lifecycle(
                app,
                work,
                logs,
                "COMPLETED_CONTINUE",
                completed_fixture,
            ),
            run_save_transition_lifecycle(
                app,
                work,
                logs,
                "COMPLETED_NEW_GAME",
                source_fixture=completed_fixture,
                result_fixture=progress_fixture,
            ),
            run_save_transition_lifecycle(
                app,
                work,
                logs,
                "RESET_NEW_GAME",
                source_fixture=progress_fixture,
                result_fixture=progress_fixture,
                expect_reset_backup=True,
            ),
        ]

        settings_lifecycle_root = fresh_data_root(
            work,
            "lifecycle-settings",
        )
        settings_apply_before = root_files(settings_lifecycle_root, set())
        settings_apply_marker = run_packaged_lifecycle(
            app,
            settings_lifecycle_root,
            logs / "lifecycle-settings-apply.log",
            "SETTINGS_APPLY",
            expected_root_names=set(),
        )
        applied_settings = settings_lifecycle_root / SETTINGS_FILE
        require_fixture_bytes(
            applied_settings,
            settings_fixture,
            "settings Apply primary file",
        )
        settings_apply_after = root_files(
            settings_lifecycle_root,
            {SETTINGS_FILE},
        )
        lifecycle_stages.append(
            {
                "id": "SETTINGS_APPLY",
                "marker": settings_apply_marker,
                "rootFilesAfter": settings_apply_after,
                "rootFilesBefore": settings_apply_before,
                "settings": settings_facts(applied_settings),
            }
        )

        settings_restore_before = root_files(
            settings_lifecycle_root,
            {SETTINGS_FILE},
        )
        settings_restore_marker = run_packaged_lifecycle(
            app,
            settings_lifecycle_root,
            logs / "lifecycle-settings-restore.log",
            "SETTINGS_RESTORE",
            expected_root_names={SETTINGS_FILE},
        )
        require_fixture_bytes(
            applied_settings,
            settings_fixture,
            "settings Restore primary file",
        )
        settings_restore_after = root_files(
            settings_lifecycle_root,
            {SETTINGS_FILE},
        )
        if settings_restore_after != settings_restore_before:
            fail("settings Restore changed the app-owned root bytes")
        lifecycle_stages.append(
            {
                "id": "SETTINGS_RESTORE",
                "marker": settings_restore_marker,
                "rootFilesAfter": settings_restore_after,
                "rootFilesBefore": settings_restore_before,
                "settings": settings_facts(applied_settings),
            }
        )

        if candidate.tree_entries(extracted) != baseline_tree:
            fail("packaged app tree changed during qualification")
        if (
            pinned_archive.stat().st_size != manifest_package.get("byteLength")
            or candidate.sha256_file(pinned_archive)
            != manifest_package.get("sha256")
        ):
            fail("pinned candidate archive changed during qualification")

    if (
        not manifest_path.is_file()
        or manifest_path.is_symlink()
        or candidate.sha256_file(manifest_path) != manifest_sha256
        or not source_archive.is_file()
        or source_archive.is_symlink()
        or source_archive.stat().st_size != manifest_package.get("byteLength")
        or candidate.sha256_file(source_archive) != manifest_package.get("sha256")
    ):
        fail("candidate manifest/archive changed during qualification")
    if default_product_file_snapshot(account_home) != before_default:
        fail("default current R2 save/settings changed during qualification")
    if candidate.require_clean_source() != manifest["source"]["commit"]:
        fail("source changed during qualification")

    return {
        "claims": {
            "appOwnedPersistenceQualified": True,
            "engineUserDataIsolated": False,
            "fullProductionInputE2E": False,
            "generatedAudioWiringQualified": True,
            "humanQa": False,
            "osHardwareInput": False,
            "packagedLifecycleInputQualified": True,
            "speakerAudioQualified": False,
        },
        "isolation": {
            "environmentVariable": candidate.QUALIFICATION_DATA_ENV,
            "homeReassigned": False,
            "initialRootEmpty": True,
            "lifecycleEnvironmentVariable": candidate.QUALIFICATION_SCENARIO_ENV,
            "lifecycleSaveRootsIndependent": True,
            "lifecycleSettingsApplyRestoreRootShared": True,
            "packageAppTreeUnchanged": True,
            "packageUserArgumentCount": 0,
            "realDefaultProductFilesUnchanged": True,
            "scope": "GRIDWORKS_OWNED_DATA_AND_PACKAGED_LIFECYCLE_SEAMS_ONLY",
        },
        "lifecycleRejections": lifecycle_rejections,
        "lifecycleStages": lifecycle_stages,
        "package": package_identity(manifest_path, manifest),
        "producer": {
            "path": TOOL_PATH.relative_to(ROOT).as_posix(),
            "sha256": candidate.sha256_file(TOOL_PATH),
        },
        "schemaVersion": SCHEMA,
        "stages": stages,
    }


def write_record(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.parent / f".{path.name}.tmp"
    data = candidate.canonical_bytes(value) + b"\n"
    try:
        with temporary.open("xb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def run_qualification(manifest_path: Path) -> None:
    value = reconstruct(manifest_path)
    record_path = manifest_path.resolve().parent / RECORD_NAME
    write_record(record_path, value)
    print(f"R2_QUALIFICATION_RUN_PASS record={record_path}")


def verify_record(record_path: Path) -> None:
    if record_path.is_symlink():
        fail("qualification record must not be a symlink")
    record_path = record_path.resolve()
    if record_path.name != RECORD_NAME:
        fail("qualification record filename drift")
    initial_metadata = record_path.lstat()
    if not stat.S_ISREG(initial_metadata.st_mode):
        fail("qualification record is not a regular file")
    initial_size = initial_metadata.st_size
    initial_sha256 = candidate.sha256_file(record_path)
    record = candidate.strict_json(
        record_path,
        label="qualification record",
        canonical=True,
    )
    parsed_bytes = candidate.canonical_bytes(record) + b"\n"
    if (
        len(parsed_bytes) != initial_size
        or candidate.sha256_bytes(parsed_bytes) != initial_sha256
    ):
        fail("qualification record changed before or during parsing")
    if not isinstance(record, dict) or set(record) != {
        "claims",
        "isolation",
        "lifecycleRejections",
        "lifecycleStages",
        "package",
        "producer",
        "schemaVersion",
        "stages",
    }:
        fail("qualification record top-level shape drift")
    if record.get("schemaVersion") != SCHEMA:
        fail("unsupported qualification record schema")
    package = record.get("package")
    if not isinstance(package, dict) or package.get("manifestFileName") != candidate.MANIFEST_NAME:
        fail("qualification record package identity drift")
    expected = reconstruct(record_path.parent / candidate.MANIFEST_NAME)
    if candidate.canonical_bytes(record) != candidate.canonical_bytes(expected):
        fail("qualification record differs from reconstructed authority")
    final_metadata = record_path.lstat()
    if (
        not stat.S_ISREG(final_metadata.st_mode)
        or final_metadata.st_size != initial_size
        or candidate.sha256_file(record_path) != initial_sha256
    ):
        fail("qualification record changed during verification")
    print(f"R2_QUALIFICATION_VERIFY_PASS record={record_path}")


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    run_parser = subparsers.add_parser("run", help="run and write a qualification record")
    run_parser.add_argument("manifest", type=Path)
    verify_parser = subparsers.add_parser("verify", help="reconstruct a qualification record")
    verify_parser.add_argument("record", type=Path)
    return parser.parse_args()


def main() -> int:
    try:
        arguments = parse_arguments()
        if arguments.command == "run":
            run_qualification(arguments.manifest)
        else:
            verify_record(arguments.record)
        return 0
    except (
        candidate.CandidateError,
        OSError,
        subprocess.TimeoutExpired,
    ) as exception:
        print(f"R2_QUALIFICATION_FAIL {exception}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
