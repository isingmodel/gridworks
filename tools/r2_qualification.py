#!/usr/bin/env python3
"""Run or reconstruct the bounded current-R2 app persistence qualification."""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
from typing import Any

import r2_candidate as candidate


ROOT = candidate.ROOT
GAME = candidate.GAME
TOOL_PATH = ROOT / "tools/r2_qualification.py"
SCHEMA = "gridworks.r2-app-persistence-qualification.v1"
RECORD_NAME = "Gridworks-current-r2-macOS-internal.qualification.json"
RUNNER_SCENE = "res://realtime/r2/RealtimeProductEntrySmokeRunner.tscn"
MARKER_PREFIX = "REALTIME_R2_QUALIFICATION_DATA_READY "
TITLE_MARKER = candidate.TITLE_MARKER

SETTINGS_FILE = "realtime-settings-v1.json"
SAVE_FILE = "gridworks-r2-campaign-save-v1.json"


def fail(message: str) -> None:
    raise candidate.CandidateError(message)


def combined_output(
    result: subprocess.CompletedProcess[str],
    log_path: Path,
) -> str:
    output = result.stdout + result.stderr
    if log_path.is_file() and not log_path.is_symlink():
        output += log_path.read_text(encoding="utf-8", errors="replace")
    return output


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
    markers = {
        line.strip()
        for line in output.splitlines()
        if line.strip().startswith(MARKER_PREFIX)
    }
    if markers != {expected}:
        fail(
            "packaged qualification marker drift: "
            f"expected {expected!r}, got {sorted(markers)!r}"
        )
    return expected


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
    marker = exact_qualification_marker(
        output,
        settings=settings,
        continuation=continuation,
    )
    if root_files(data_root, expected_root_names) != before_root:
        fail("packaged product-title stage changed app-owned persistence bytes")
    return marker


def root_files(data_root: Path, expected_names: set[str]) -> list[dict[str, Any]]:
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


def default_product_file_snapshot() -> dict[str, dict[str, Any] | None]:
    default_root = (
        Path.home()
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
        or source.get("routeId") != "ProductCampaign"
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


def reconstruct(manifest_path: Path) -> dict[str, Any]:
    manifest_path = manifest_path.resolve()
    if manifest_path.name != candidate.MANIFEST_NAME:
        fail("qualification requires the fixed current R2 candidate manifest name")
    candidate.verify_manifest(manifest_path)
    manifest = candidate.strict_json(
        manifest_path,
        label="candidate manifest",
        canonical=True,
    )
    if not isinstance(manifest, dict):
        fail("candidate manifest must be an object")
    archive = manifest_path.parent / candidate.ARCHIVE_NAME

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
        timeout=600,
    )

    before_default = default_product_file_snapshot()
    with tempfile.TemporaryDirectory(prefix="gridworks-r2-qualification-") as raw:
        work = Path(raw).resolve(strict=True)
        extracted = work / "package"
        baseline_tree = candidate.extract_archive(archive, extracted)
        app = extracted / "Gridworks.app"
        data_root = work / "product-data"
        data_root.mkdir(mode=0o700)
        data_root = data_root.resolve(strict=True)
        validate_empty_root(data_root)
        logs = work / "logs"
        logs.mkdir(mode=0o700)

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

        if candidate.tree_entries(extracted) != baseline_tree:
            fail("packaged app tree changed during qualification")

    if default_product_file_snapshot() != before_default:
        fail("default current R2 save/settings changed during qualification")
    if candidate.require_clean_source() != manifest["source"]["commit"]:
        fail("source changed during qualification")

    return {
        "claims": {
            "appOwnedPersistenceQualified": True,
            "engineUserDataIsolated": False,
            "fullProductionInputE2E": False,
            "humanQa": False,
            "osHardwareInput": False,
            "speakerAudioQualified": False,
        },
        "isolation": {
            "environmentVariable": candidate.QUALIFICATION_DATA_ENV,
            "homeReassigned": False,
            "initialRootEmpty": True,
            "packageAppTreeUnchanged": True,
            "packageUserArgumentCount": 0,
            "realDefaultProductFilesUnchanged": True,
            "scope": "GRIDWORKS_OWNED_SAVE_AND_SETTINGS_ONLY",
        },
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
    record_path = record_path.resolve()
    if record_path.name != RECORD_NAME:
        fail("qualification record filename drift")
    record = candidate.strict_json(
        record_path,
        label="qualification record",
        canonical=True,
    )
    if not isinstance(record, dict) or set(record) != {
        "claims",
        "isolation",
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
