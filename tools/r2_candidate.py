#!/usr/bin/env python3
"""Build and verify the current R2 internal macOS package vertical slice."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import plistlib
import posixpath
import shutil
import stat
import struct
import subprocess
import sys
import tempfile
from typing import Any
import unicodedata
import zipfile


ROOT = Path(__file__).resolve().parents[1]
GAME = ROOT / "game"
DIST = ROOT / "dist"
TOOL_PATH = ROOT / "tools/r2_candidate.py"
PRESET_PATH = GAME / "export_presets.cfg"
PROJECT_PATH = GAME / "project.godot"
PRESET = "Current R2 macOS Internal Candidate"
ARCHIVE_NAME = "Gridworks-current-r2-macOS-internal.zip"
MANIFEST_NAME = "Gridworks-current-r2-macOS-internal.manifest.json"
SCHEMA = "gridworks.r2-package-manifest.v1"
TITLE_MARKER = "REALTIME_R2_PRODUCT_TITLE_READY"
GODOT_VERSION = "4.7.1.stable.mono.official.a13da4feb"
DOTNET_SDK_VERSION = "8.0.129"
DOTNET_RUNTIME_VERSION = "8.0.29"
BUNDLE_ID = "com.gridworks.game"
PRODUCT_VERSION = "0.2.0"
MINIMUM_MACOS = "14.0"
DEFAULT_SCENE = "res://realtime/r2/RealtimeSliceMain.tscn"
SAVE_PATH = "user://gridworks-r2-campaign-save-v1.json"
SAVE_SCHEMA = "gridworks.realtime.campaign-save.v3"
SETTINGS_PATH = "user://realtime-settings-v1.json"
SETTINGS_SCHEMA = "gridworks.realtime-settings.v1"
QUALIFICATION_DATA_ENV = "GRIDWORKS_R2_QUALIFICATION_USER_DATA_DIR"
QUALIFICATION_SCENARIO_ENV = "GRIDWORKS_R2_QUALIFICATION_SCENARIO"
LEGAL_PATHS = (
    "INSTALL.md",
    "CREDITS.md",
    "ASSET_MANIFEST.md",
    "THIRD_PARTY_NOTICES.md",
    "LICENSE.md",
    "licenses/GODOT-4.7.1-COPYRIGHT.txt",
    "licenses/DOTNET-RUNTIME-8.0.29-LICENSE.txt",
    "licenses/DOTNET-RUNTIME-8.0.29-THIRD-PARTY-NOTICES.txt",
)
CLAIMS = {
    "developerIdSigned": False,
    "evaluationReady": False,
    "freshUserDataQualified": False,
    "fullProductionInputE2E": False,
    "humanQa": False,
    "notarized": False,
    "scoreBearing": False,
}
MAX_ARCHIVE_BYTES = 1_000_000_000
MAX_ARCHIVE_ENTRIES = 4_096
MAX_ARCHIVE_ENTRY_BYTES = 600_000_000
MAX_ARCHIVE_EXPANDED_BYTES = 2_000_000_000
MAX_COMPRESSION_RATIO = 200
MAX_PCK_ENTRIES = 4_096


class CandidateError(RuntimeError):
    pass


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def file_contains(path: Path, needle: bytes) -> bool:
    overlap = max(0, len(needle) - 1)
    carry = b""
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            data = carry + chunk
            if needle in data:
                return True
            carry = data[-overlap:] if overlap else b""
    return False


def canonical_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def run(
    arguments: list[str],
    *,
    cwd: Path | None = None,
    env: dict[str, str] | None = None,
    timeout: int = 300,
) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(
        arguments,
        cwd=cwd,
        env=env,
        text=True,
        capture_output=True,
        timeout=timeout,
        check=False,
    )
    if result.returncode != 0:
        command = " ".join(arguments)
        raise CandidateError(
            f"command failed ({result.returncode}): {command}\n"
            f"{result.stdout}{result.stderr}"
        )
    return result


def require_tool(name: str, fixed: str | None = None) -> str:
    resolved = fixed if fixed and Path(fixed).is_file() else shutil.which(name)
    if not resolved:
        raise CandidateError(f"required tool is unavailable: {name}")
    return str(Path(resolved).resolve())


def godot_path() -> str:
    configured = os.environ.get("GRIDWORKS_GODOT_BIN")
    bundled = (
        ROOT
        / ".tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot"
    )
    return require_tool("Godot", configured or str(bundled))


def git(*arguments: str) -> str:
    return run([require_tool("git"), "-C", str(ROOT), *arguments]).stdout.strip()


def require_clean_source() -> str:
    status = git("status", "--porcelain=v1", "--untracked-files=all")
    if status:
        raise CandidateError(
            "candidate build requires a clean committed worktree; "
            f"first change: {status.splitlines()[0]}"
        )
    commit = git("rev-parse", "--verify", "HEAD")
    if len(commit) != 40 or any(char not in "0123456789abcdef" for char in commit):
        raise CandidateError("HEAD is not a canonical SHA-1 commit identity")
    return commit


def require_host_versions() -> tuple[str, str]:
    if sys.platform != "darwin":
        raise CandidateError("the current R2 package vertical slice is macOS-only")
    actual_godot = run([godot_path(), "--version"]).stdout.strip()
    if actual_godot != GODOT_VERSION:
        raise CandidateError(
            f"Godot version drift: expected {GODOT_VERSION}, got {actual_godot}"
        )
    dotnet = require_tool("dotnet")
    actual_dotnet = run([dotnet, "--version"]).stdout.strip()
    if actual_dotnet != DOTNET_SDK_VERSION:
        raise CandidateError(
            f".NET SDK drift: expected {DOTNET_SDK_VERSION}, got {actual_dotnet}"
        )
    runtimes = run([dotnet, "--list-runtimes"]).stdout.splitlines()
    runtime_prefix = f"Microsoft.NETCore.App {DOTNET_RUNTIME_VERSION} ["
    if not any(line.startswith(runtime_prefix) and line.endswith("]") for line in runtimes):
        raise CandidateError(
            f".NET runtime drift: expected {DOTNET_RUNTIME_VERSION}"
        )
    return actual_godot, actual_dotnet


def normalized_zip_name(raw: str) -> str:
    if not raw or "\x00" in raw or "\\" in raw or raw.startswith("/"):
        raise CandidateError(f"unsafe archive path: {raw!r}")
    trimmed = raw[:-1] if raw.endswith("/") else raw
    path = PurePosixPath(trimmed)
    if (
        not trimmed
        or path.is_absolute()
        or trimmed != path.as_posix()
        or any(part in ("", ".", "..") for part in path.parts)
    ):
        raise CandidateError(f"unsafe archive path: {raw!r}")
    return path.as_posix()


def macos_path_key(path: str) -> tuple[str, ...]:
    return tuple(
        unicodedata.normalize("NFD", part).casefold()
        for part in PurePosixPath(path).parts
    )


def zip_entry_count(path: Path) -> int:
    file_size = path.stat().st_size
    with path.open("rb") as stream:
        stream.seek(max(0, file_size - (65_535 + 22)))
        tail = stream.read()
    signature = b"PK\x05\x06"
    search_end = len(tail)
    fields: tuple[Any, ...] | None = None
    eocd_offset = -1
    while search_end:
        index = tail.rfind(signature, 0, search_end)
        if index < 0:
            break
        if len(tail) - index >= 22:
            candidate = struct.unpack("<4s4H2LH", tail[index : index + 22])
            if index + 22 + candidate[-1] == len(tail):
                fields = candidate
                eocd_offset = file_size - len(tail) + index
                break
        search_end = index
    if fields is None:
        raise CandidateError("candidate archive end-of-directory record is missing")
    _, disk, directory_disk, disk_entries, total_entries, size, offset, _ = fields
    if disk or directory_disk or disk_entries != total_entries:
        raise CandidateError("multi-disk candidate archives are unsupported")
    if total_entries in (0, 0xFFFF) or total_entries > MAX_ARCHIVE_ENTRIES:
        raise CandidateError("candidate archive entry count is outside bounds")
    if size == 0xFFFFFFFF or offset == 0xFFFFFFFF or size > 16_000_000:
        raise CandidateError("candidate archive central directory is outside bounds")
    if offset + size > eocd_offset:
        raise CandidateError("candidate archive central directory range is invalid")
    return total_entries


def validate_zip(path: Path) -> None:
    if not path.is_file() or path.is_symlink():
        raise CandidateError(f"candidate archive is not a regular file: {path}")
    if path.stat().st_size > MAX_ARCHIVE_BYTES:
        raise CandidateError("candidate archive exceeds the bounded byte ceiling")
    expected_entry_count = zip_entry_count(path)
    seen: dict[tuple[str, ...], tuple[str, bool]] = {}
    archive_entries: list[tuple[str, bool]] = []
    expanded_bytes = 0
    with zipfile.ZipFile(path) as archive:
        infos = archive.infolist()
        if len(infos) != expected_entry_count:
            raise CandidateError("candidate archive entry count is outside bounds")
        for info in infos:
            original_name = getattr(info, "orig_filename", info.filename)
            if original_name != info.filename:
                raise CandidateError(f"unsafe raw archive path: {original_name!r}")
            normalized = normalized_zip_name(original_name)
            mode = (info.external_attr >> 16) & 0xFFFF
            kind = stat.S_IFMT(mode)
            if kind not in (0, stat.S_IFREG, stat.S_IFDIR, stat.S_IFLNK):
                raise CandidateError(f"unsupported archive entry type: {normalized}")
            if info.flag_bits & 0x1:
                raise CandidateError(f"encrypted archive entry is unsupported: {normalized}")
            if info.compress_type not in (zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED):
                raise CandidateError(f"unsupported archive compression: {normalized}")
            is_directory = kind == stat.S_IFDIR or (kind == 0 and info.is_dir())
            if info.is_dir() != is_directory:
                raise CandidateError(f"archive entry type/name mismatch: {normalized}")
            if info.file_size > MAX_ARCHIVE_ENTRY_BYTES:
                raise CandidateError(f"archive entry exceeds byte ceiling: {normalized}")
            expanded_bytes += info.file_size
            if expanded_bytes > MAX_ARCHIVE_EXPANDED_BYTES:
                raise CandidateError("candidate archive expanded bytes exceed bounds")
            if (
                info.file_size > 1_000_000
                and info.file_size > max(1, info.compress_size) * MAX_COMPRESSION_RATIO
            ):
                raise CandidateError(f"archive entry compression ratio is unsafe: {normalized}")

            logical_key = macos_path_key(normalized)
            if logical_key in seen:
                prior = seen[logical_key][0]
                raise CandidateError(
                    f"duplicate macOS archive path: {prior!r} and {normalized!r}"
                )
            for index in range(1, len(logical_key)):
                ancestor = seen.get(logical_key[:index])
                if ancestor is not None and not ancestor[1]:
                    raise CandidateError(
                        f"archive path descends through non-directory: {normalized}"
                    )
            if not is_directory and any(
                len(other) > len(logical_key) and other[: len(logical_key)] == logical_key
                for other in seen
            ):
                raise CandidateError(
                    f"archive non-directory shadows an existing path: {normalized}"
                )
            seen[logical_key] = (normalized, is_directory)
            archive_entries.append((normalized, is_directory))

            if kind == stat.S_IFLNK:
                if info.file_size > 4_096:
                    raise CandidateError(f"archive symlink target is too large: {normalized}")
                try:
                    target = archive.read(info).decode("utf-8")
                except UnicodeDecodeError as exception:
                    raise CandidateError(
                        f"non-UTF-8 symlink target: {normalized}"
                    ) from exception
                joined = posixpath.normpath(
                    posixpath.join(posixpath.dirname(normalized), target)
                )
                if (
                    not target
                    or "\x00" in target
                    or posixpath.isabs(target)
                    or joined == ".."
                    or joined.startswith("../")
                ):
                    raise CandidateError(
                        f"archive symlink escapes extraction root: {normalized} -> {target}"
                    )
    for normalized, is_directory in archive_entries:
        if not normalized.startswith("__MACOSX/") or is_directory:
            continue
        parts = PurePosixPath(normalized).parts
        if len(parts) < 3 or not parts[-1].startswith("._"):
            raise CandidateError(f"invalid AppleDouble archive entry: {normalized}")
        target = PurePosixPath(*parts[1:-1], parts[-1][2:]).as_posix()
        if macos_path_key(target) not in seen:
            raise CandidateError(f"orphan AppleDouble archive entry: {normalized}")


def tree_entries(root: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for current, directory_names, file_names in os.walk(root, followlinks=False):
        current_path = Path(current)
        names = sorted(directory_names + file_names)
        for name in names:
            path = current_path / name
            relative = path.relative_to(root).as_posix()
            metadata = path.lstat()
            mode = stat.S_IMODE(metadata.st_mode)
            if stat.S_ISLNK(metadata.st_mode):
                target = os.readlink(path)
                joined = posixpath.normpath(
                    posixpath.join(posixpath.dirname(relative), target)
                )
                if posixpath.isabs(target) or joined == ".." or joined.startswith("../"):
                    raise CandidateError(
                        f"extracted symlink escapes tree: {relative} -> {target}"
                    )
                rows.append(
                    {
                        "mode": f"{mode:04o}",
                        "path": relative,
                        "sha256": sha256_bytes(target.encode("utf-8")),
                        "size": len(target.encode("utf-8")),
                        "type": "symlink",
                    }
                )
                if name in directory_names:
                    directory_names.remove(name)
            elif stat.S_ISDIR(metadata.st_mode):
                rows.append(
                    {
                        "mode": f"{mode:04o}",
                        "path": relative,
                        "sha256": None,
                        "size": 0,
                        "type": "directory",
                    }
                )
            elif stat.S_ISREG(metadata.st_mode):
                rows.append(
                    {
                        "mode": f"{mode:04o}",
                        "path": relative,
                        "sha256": sha256_file(path),
                        "size": metadata.st_size,
                        "type": "file",
                    }
                )
            else:
                raise CandidateError(f"unsupported extracted entry: {relative}")
    rows.sort(key=lambda row: row["path"].encode("utf-8"))
    paths = [row["path"] for row in rows]
    if len(paths) != len(set(paths)):
        raise CandidateError("extracted tree contains duplicate logical paths")
    macos_keys = [macos_path_key(path) for path in paths]
    if len(macos_keys) != len(set(macos_keys)):
        raise CandidateError("extracted tree contains duplicate macOS paths")
    return rows


def extract_archive(archive: Path, destination: Path) -> list[dict[str, Any]]:
    validate_zip(archive)
    destination.mkdir(parents=True, exist_ok=False)
    run([require_tool("ditto", "/usr/bin/ditto"), "-x", "-k", str(archive), str(destination)])
    return tree_entries(destination)


def aggregate_files(paths: list[Path]) -> tuple[int, str]:
    rows = [
        {
            "path": path.relative_to(ROOT).as_posix(),
            "sha256": sha256_file(path),
            "size": path.stat().st_size,
        }
        for path in sorted(paths, key=lambda item: item.as_posix().encode("utf-8"))
    ]
    return len(rows), sha256_bytes(canonical_bytes(rows))


def g3_identity() -> tuple[int, str, list[str]]:
    files = sorted(
        (GAME / "art/commercial/g3").rglob("*.png"),
        key=lambda path: path.as_posix().encode("utf-8"),
    )
    count, digest = aggregate_files(files)
    if count != 57:
        raise CandidateError(f"canonical G3 source count drift: expected 57, got {count}")
    resources = ["res://" + path.relative_to(GAME).as_posix() for path in files]
    return count, digest, resources


def strict_json(path: Path, *, label: str, canonical: bool = False) -> Any:
    def pairs_hook(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise CandidateError(f"duplicate JSON key: {key}")
            result[key] = value
        return result

    def reject_constant(value: str) -> None:
        raise CandidateError(f"non-finite JSON value in {label}: {value}")

    try:
        raw = path.read_bytes()
        value = json.loads(
            raw.decode("utf-8"),
            object_pairs_hook=pairs_hook,
            parse_constant=reject_constant,
        )
    except (OSError, UnicodeError, json.JSONDecodeError) as exception:
        raise CandidateError(f"invalid {label}: {exception}") from exception
    if canonical and raw != canonical_bytes(value) + b"\n":
        raise CandidateError(f"{label} is not canonical JSON")
    return value


def strings(path: Path) -> str:
    result = run(
        [require_tool("strings", "/usr/bin/strings"), str(path)],
        env={**os.environ, "LC_ALL": "C", "LANG": "C"},
    )
    return result.stdout


def require_contains(text: str, markers: tuple[str, ...], label: str) -> None:
    missing = [marker for marker in markers if marker not in text]
    if missing:
        raise CandidateError(f"{label} is missing marker: {missing[0]}")


def require_excludes(text: str, markers: tuple[str, ...], label: str) -> None:
    found = [marker for marker in markers if marker in text]
    if found:
        raise CandidateError(f"{label} contains forbidden marker: {found[0]}")


def read_exact(stream: Any, byte_count: int, label: str) -> bytes:
    data = stream.read(byte_count)
    if len(data) != byte_count:
        raise CandidateError(f"truncated {label}")
    return data


def read_u32(stream: Any, label: str) -> int:
    return struct.unpack("<I", read_exact(stream, 4, label))[0]


def read_u64(stream: Any, label: str) -> int:
    return struct.unpack("<Q", read_exact(stream, 8, label))[0]


def md5_range(stream: Any, offset: int, byte_count: int) -> bytes:
    digest = hashlib.md5(usedforsecurity=False)
    stream.seek(offset)
    remaining = byte_count
    while remaining:
        chunk = read_exact(stream, min(remaining, 1024 * 1024), "PCK entry data")
        digest.update(chunk)
        remaining -= len(chunk)
    return digest.digest()


def pck_entries(path: Path) -> dict[str, tuple[int, int]]:
    file_size = path.stat().st_size
    entries: list[tuple[str, int, int, bytes]] = []
    logical_paths: dict[tuple[str, ...], str] = {}
    with path.open("rb") as stream:
        if read_exact(stream, 4, "PCK magic") != b"GDPC":
            raise CandidateError("invalid standalone PCK magic")
        header = (
            read_u32(stream, "PCK format version"),
            read_u32(stream, "PCK Godot major"),
            read_u32(stream, "PCK Godot minor"),
            read_u32(stream, "PCK Godot patch"),
        )
        if header != (4, 4, 7, 1):
            raise CandidateError(f"unexpected PCK/Godot format identity: {header}")
        if read_u32(stream, "PCK flags") != 2:
            raise CandidateError("PCK must be unencrypted with a relative file base")
        file_base = read_u64(stream, "PCK file base")
        directory_offset = read_u64(stream, "PCK directory offset")
        if read_exact(stream, 64, "PCK reserved header") != bytes(64):
            raise CandidateError("PCK reserved header is nonzero")
        if not (stream.tell() <= file_base <= directory_offset < file_size):
            raise CandidateError("PCK file/directory offsets are outside bounds")

        stream.seek(directory_offset)
        file_count = read_u32(stream, "PCK file count")
        if not 0 < file_count <= MAX_PCK_ENTRIES:
            raise CandidateError("PCK entry count is outside bounds")
        for _ in range(file_count):
            encoded_length = read_u32(stream, "PCK path length")
            if not 0 < encoded_length <= 1_048_576 or encoded_length % 4:
                raise CandidateError("PCK path length is invalid")
            encoded_path = read_exact(stream, encoded_length, "PCK path")
            unpadded = encoded_path.rstrip(b"\x00")
            if not unpadded or b"\x00" in unpadded:
                raise CandidateError("PCK path padding is invalid")
            try:
                raw_path = unpadded.decode("utf-8")
            except UnicodeDecodeError as exception:
                raise CandidateError("PCK path is not UTF-8") from exception
            normalized = normalized_zip_name(raw_path)
            logical_key = macos_path_key(normalized)
            if logical_key in logical_paths:
                raise CandidateError(
                    "duplicate macOS PCK path: "
                    f"{logical_paths[logical_key]!r} and {normalized!r}"
                )
            logical_paths[logical_key] = normalized

            relative_offset = read_u64(stream, "PCK entry offset")
            byte_count = read_u64(stream, "PCK entry size")
            expected_md5 = read_exact(stream, 16, "PCK entry MD5")
            if read_u32(stream, "PCK entry flags") != 0:
                raise CandidateError(f"unsupported PCK entry flags: {normalized}")
            absolute_offset = file_base + relative_offset
            if absolute_offset < file_base or absolute_offset + byte_count > directory_offset:
                raise CandidateError(f"PCK entry range is outside bounds: {normalized}")
            entries.append((normalized, absolute_offset, byte_count, expected_md5))
        if stream.tell() != file_size:
            raise CandidateError("PCK directory does not end at the file boundary")

        previous_end = file_base
        for normalized, offset, byte_count, expected_md5 in sorted(
            entries, key=lambda item: (item[1], item[2], item[0])
        ):
            if byte_count and offset < previous_end:
                raise CandidateError(f"overlapping PCK entry range: {normalized}")
            if md5_range(stream, offset, byte_count) != expected_md5:
                raise CandidateError(f"PCK entry hash mismatch: {normalized}")
            previous_end = max(previous_end, offset + byte_count)
    return {entry[0]: (entry[1], entry[2]) for entry in entries}


def pck_text(path: Path, entry: tuple[int, int], label: str) -> str:
    offset, byte_count = entry
    if byte_count > 1_000_000:
        raise CandidateError(f"PCK text entry is too large: {label}")
    with path.open("rb") as stream:
        stream.seek(offset)
        raw = read_exact(stream, byte_count, f"PCK {label}")
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError as exception:
        raise CandidateError(f"PCK text entry is not UTF-8: {label}") from exception


def validate_packaged_runtime(resources: Path) -> None:
    expected_directories = {
        "arm64": resources / "data_Gridworks.Game_macos_arm64",
        "x86_64": resources / "data_Gridworks.Game_macos_x86_64",
    }
    actual_directories = {
        path for path in resources.glob("data_Gridworks.Game_macos_*")
        if path.is_dir()
    }
    if actual_directories != set(expected_directories.values()):
        raise CandidateError("packaged .NET architecture directories drift")

    required_runtime_files = (
        "Gridworks.Game.dll",
        "Gridworks.Core.dll",
        "System.Private.CoreLib.dll",
        "createdump",
        "libcoreclr.dylib",
        "libhostfxr.dylib",
        "libhostpolicy.dylib",
    )
    for architecture, directory in expected_directories.items():
        if directory.is_symlink():
            raise CandidateError(f"packaged runtime directory is a symlink: {architecture}")
        for name in required_runtime_files:
            payload = directory / name
            if not payload.is_file() or payload.is_symlink():
                raise CandidateError(
                    f"packaged runtime payload is missing: {architecture}/{name}"
                )
        native_payloads = [directory / "createdump", *sorted(directory.glob("*.dylib"))]
        if len(native_payloads) < 2:
            raise CandidateError(f"packaged native runtime is incomplete: {architecture}")
        for payload in native_payloads:
            payload_architectures = run(
                [require_tool("lipo", "/usr/bin/lipo"), "-archs", str(payload)]
            ).stdout.split()
            if payload_architectures != [architecture]:
                raise CandidateError(
                    "packaged native runtime architecture drift: "
                    f"{architecture}/{payload.name}={payload_architectures}"
                )

        runtime_config = strict_json(
            directory / "Gridworks.Game.runtimeconfig.json",
            label=f"{architecture} runtime config",
        )
        expected_frameworks = [
            {"name": "Microsoft.NETCore.App", "version": DOTNET_RUNTIME_VERSION}
        ]
        if (
            not isinstance(runtime_config, dict)
            or not isinstance(runtime_config.get("runtimeOptions"), dict)
            or runtime_config["runtimeOptions"].get("tfm") != "net8.0"
            or runtime_config["runtimeOptions"].get("includedFrameworks")
            != expected_frameworks
        ):
            raise CandidateError(f"packaged runtime config drift: {architecture}")

        deps = strict_json(
            directory / "Gridworks.Game.deps.json",
            label=f"{architecture} dependency manifest",
        )
        rid = "osx-arm64" if architecture == "arm64" else "osx-x64"
        target_name = f".NETCoreApp,Version=v8.0/{rid}"
        runtime_pack = (
            f"runtimepack.Microsoft.NETCore.App.Runtime.{rid}/"
            f"{DOTNET_RUNTIME_VERSION}"
        )
        targets = deps.get("targets") if isinstance(deps, dict) else None
        if (
            not isinstance(deps, dict)
            or deps.get("runtimeTarget") != {"name": target_name, "signature": ""}
            or not isinstance(targets, dict)
            or not isinstance(targets.get(target_name), dict)
            or runtime_pack not in targets[target_name]
        ):
            raise CandidateError(f"packaged dependency/runtime drift: {architecture}")


def validate_payload(root: Path) -> dict[str, Any]:
    expected_top_level = {"Gridworks.app"} | {
        PurePosixPath(relative).parts[0] for relative in LEGAL_PATHS
    }
    actual_top_level = {path.name for path in root.iterdir()}
    if actual_top_level != expected_top_level:
        difference = sorted(actual_top_level ^ expected_top_level)
        raise CandidateError(f"package root closure drift: {difference[0]}")
    app = root / "Gridworks.app"
    if not app.is_dir() or app.is_symlink():
        raise CandidateError("package must contain one regular Gridworks.app directory")
    plist_path = app / "Contents/Info.plist"
    try:
        plist = plistlib.loads(plist_path.read_bytes())
    except (OSError, plistlib.InvalidFileException) as exception:
        raise CandidateError(f"invalid packaged Info.plist: {exception}") from exception
    if plist.get("CFBundleIdentifier") != BUNDLE_ID:
        raise CandidateError("packaged bundle identifier drift")
    if plist.get("CFBundleShortVersionString") != PRODUCT_VERSION:
        raise CandidateError("packaged short version drift")
    if plist.get("CFBundleVersion") != PRODUCT_VERSION:
        raise CandidateError("packaged bundle version drift")
    minimums = plist.get("LSMinimumSystemVersionByArchitecture")
    if minimums != {"arm64": MINIMUM_MACOS, "x86_64": MINIMUM_MACOS}:
        raise CandidateError(f"packaged minimum macOS drift: {minimums!r}")

    executable = app / "Contents/MacOS/Gridworks"
    if not executable.is_file() or executable.is_symlink():
        raise CandidateError("packaged executable is missing")
    architectures = run([require_tool("lipo", "/usr/bin/lipo"), "-archs", str(executable)]).stdout.split()
    if set(architectures) != {"arm64", "x86_64"} or len(architectures) != 2:
        raise CandidateError(f"packaged architecture drift: {architectures}")
    codesign = require_tool("codesign", "/usr/bin/codesign")
    run([codesign, "--verify", "--deep", "--strict", "--verbose=2", str(app)])
    signature = run([codesign, "-dv", "--verbose=4", str(app)]).stderr
    if "Signature=adhoc" not in signature:
        raise CandidateError("package is not bound to the expected ad-hoc signature")

    pdbs = sorted(root.rglob("*.pdb"))
    if pdbs:
        raise CandidateError(f"package contains debug symbols: {pdbs[0].relative_to(root)}")
    if list(root.rglob("Gridworks.LegacyCore.dll")):
        raise CandidateError("package contains Gridworks.LegacyCore.dll")

    game_assemblies = sorted(root.rglob("Gridworks.Game.dll"))
    core_assemblies = sorted(root.rglob("Gridworks.Core.dll"))
    if len(game_assemblies) != 2 or len(core_assemblies) != 2:
        raise CandidateError(
            "unexpected managed assembly count: "
            f"Game={len(game_assemblies)} Core={len(core_assemblies)}"
        )
    for assembly in game_assemblies:
        content = strings(assembly)
        require_contains(content, ("RealtimeSliceMain", "RealtimeAudio", "RealtimeProductTitle"), "Game assembly")
        require_excludes(
            content,
            (
                "CommercialMain",
                "CommercialAudioLibrary",
                "CommercialMapTransform",
                "ReleaseMain",
                "Scope1Main",
            ),
            "Game assembly",
        )
        require_excludes(content, ("/Users/", "/home/", "/private/tmp/"), "Game assembly")
    for assembly in core_assemblies:
        content = strings(assembly)
        require_contains(content, ("RealtimeCampaignRun", "Gridworks.Core.Release.V3"), "Core assembly")
        require_excludes(content, ("Gridworks.LegacyCore", "/Users/", "/home/", "/private/tmp/"), "Core assembly")

    resources = app / "Contents/Resources"
    expected_pck = resources / "Gridworks.pck"
    pcks = sorted(root.rglob("*.pck"))
    if pcks != [expected_pck] or expected_pck.is_symlink():
        raise CandidateError("package must contain only the expected standalone PCK")
    loose_resources = sorted(
        path.relative_to(root)
        for path in root.rglob("*")
        if path.is_file()
        and path != expected_pck
        and path.suffix.lower()
        in {
            ".ctex",
            ".import",
            ".jpg",
            ".mp3",
            ".ogg",
            ".png",
            ".res",
            ".sample",
            ".scn",
            ".svg",
            ".tres",
            ".tscn",
            ".wav",
        }
    )
    if loose_resources:
        raise CandidateError(f"package contains loose resource: {loose_resources[0]}")
    packaged_entries = pck_entries(expected_pck)
    required_entries = {
        "realtime/r2/RealtimeSliceMain.tscn.remap",
        "default_bus_layout.tres.remap",
    }
    missing_entries = required_entries - packaged_entries.keys()
    if missing_entries:
        raise CandidateError(f"PCK is missing current entry: {min(missing_entries)}")
    g3_count, g3_sha256, g3_resources = g3_identity()
    expected_g3_entries = {
        resource.removeprefix("res://") + ".import" for resource in g3_resources
    }
    actual_g3_entries = {
        entry for entry in packaged_entries
        if entry.startswith("art/commercial/g3/")
    }
    if actual_g3_entries != expected_g3_entries:
        difference = sorted(actual_g3_entries ^ expected_g3_entries)
        raise CandidateError(f"PCK G3 closure drift: {difference[0]}")
    g3_targets: set[str] = set()
    for import_path in sorted(expected_g3_entries):
        source_path = import_path.removesuffix(".import")
        import_text = pck_text(
            expected_pck,
            packaged_entries[import_path],
            import_path,
        )
        target_lines = [
            line for line in import_text.splitlines()
            if line.startswith('path="res://') and line.endswith('"')
        ]
        if len(target_lines) != 1:
            raise CandidateError(f"PCK G3 import target drift: {import_path}")
        target = target_lines[0][len('path="res://') : -1]
        target_prefix = f".godot/imported/{PurePosixPath(source_path).name}-"
        if (
            not target.startswith(target_prefix)
            or not target.endswith(".ctex")
            or target not in packaged_entries
        ):
            raise CandidateError(f"PCK G3 import backing drift: {import_path}")
        g3_targets.add(target)
    actual_ctex = {
        entry for entry in packaged_entries if entry.endswith(".ctex")
    }
    if len(g3_targets) != g3_count or actual_ctex != g3_targets:
        difference = sorted(actual_ctex ^ g3_targets)
        detail = difference[0] if difference else "duplicate target"
        raise CandidateError(f"PCK G3 imported texture closure drift: {detail}")
    forbidden_entries = {
        "CommercialMain.tscn.remap",
        "CommercialTheme.tres.remap",
        "ReleaseMain.tscn.remap",
    }
    forbidden = sorted(
        entry for entry in packaged_entries
        if entry in forbidden_entries
        or entry.startswith("realtime/evidence/")
        or entry.startswith("assets/commercial/portraits/")
        or entry.startswith("assets/realtime/")
        or entry.lower().removesuffix(".import").endswith((".mp3", ".ogg", ".wav"))
        or entry.lower().endswith((".mp3str", ".oggvorbisstr", ".sample"))
    )
    if forbidden:
        raise CandidateError(f"PCK contains forbidden entry: {forbidden[0]}")
    validate_packaged_runtime(resources)

    checkout = str(ROOT.resolve()).encode("utf-8")
    for path in root.rglob("*"):
        if path.is_file() and not path.is_symlink() and file_contains(path, checkout):
            raise CandidateError(
                f"package contains checkout path: {path.relative_to(root)}"
            )

    legal_hashes: dict[str, str] = {}
    for relative in LEGAL_PATHS:
        packaged = root / relative
        source = ROOT / relative
        if not packaged.is_file() or packaged.is_symlink():
            raise CandidateError(f"packaged legal file is missing: {relative}")
        if sha256_file(packaged) != sha256_file(source):
            raise CandidateError(f"packaged legal file drift: {relative}")
        legal_hashes[relative] = sha256_file(source)

    return {
        "appTreeEntryCount": len(tree_entries(app)),
        "architectures": sorted(architectures),
        "bundleIdentifier": BUNDLE_ID,
        "g3Count": g3_count,
        "g3Sha256": g3_sha256,
        "legalFiles": legal_hashes,
        "minimumMacOS": MINIMUM_MACOS,
        "productVersion": PRODUCT_VERSION,
        "signature": "AD_HOC_LOCAL",
    }


def packaged_title_smoke(root: Path) -> None:
    source_app = root / "Gridworks.app"
    with tempfile.TemporaryDirectory(prefix="gridworks-r2-title-smoke-") as raw:
        smoke_root = Path(raw)
        smoke_app = smoke_root / "Gridworks.app"
        run([require_tool("ditto", "/usr/bin/ditto"), str(source_app), str(smoke_app)])
        executable = smoke_app / "Contents/MacOS/Gridworks"
        log = smoke_root / "title.log"
        environment = dict(os.environ)
        environment.pop(QUALIFICATION_DATA_ENV, None)
        environment.pop(QUALIFICATION_SCENARIO_ENV, None)
        result = run(
            [
                str(executable),
                "--headless",
                "--audio-driver",
                "Dummy",
                "--quit-after",
                "8",
                "--log-file",
                str(log),
            ],
            cwd=smoke_root,
            env=environment,
            timeout=60,
        )
        output = result.stdout + result.stderr
        if log.is_file():
            output += log.read_text(encoding="utf-8", errors="replace")
        if TITLE_MARKER not in output:
            raise CandidateError("packaged no-argument boot did not reach the R2 product title")
        if "ERROR" in output.upper():
            raise CandidateError("packaged no-argument title boot logged an error")


def source_identity(source_commit: str) -> dict[str, Any]:
    g3_count, g3_sha256, _ = g3_identity()
    return {
        "commit": source_commit,
        "g3Count": g3_count,
        "g3Sha256": g3_sha256,
        "producerPath": TOOL_PATH.relative_to(ROOT).as_posix(),
        "producerSha256": sha256_file(TOOL_PATH),
        "projectSha256": sha256_file(PROJECT_PATH),
        "exportPresetSha256": sha256_file(PRESET_PATH),
    }


def expected_manifest(
    archive: Path,
    extracted: Path,
    tree: list[dict[str, Any]],
    source_commit: str,
) -> dict[str, Any]:
    payload = validate_payload(extracted)
    source = source_identity(source_commit)
    if payload["g3Count"] != source["g3Count"] or payload["g3Sha256"] != source["g3Sha256"]:
        raise CandidateError("packaged G3 identity differs from the clean source")
    return {
        "candidateKind": "INTERNAL_PACKAGE_IDENTITY_VERTICAL_SLICE",
        "claims": CLAIMS,
        "content": {
            "externalAudioAssets": 0,
            "g3Count": payload["g3Count"],
            "g3Sha256": payload["g3Sha256"],
            "generatedAudio": True,
            "legalFiles": payload["legalFiles"],
        },
        "entry": {
            "defaultScene": DEFAULT_SCENE,
            "packagedNoArgumentTitleBoot": True,
            "titleReadyMarker": TITLE_MARKER,
        },
        "export": {
            "configuration": "ExportRelease",
            "dotnetRuntimeVersion": DOTNET_RUNTIME_VERSION,
            "dotnetSdkVersion": DOTNET_SDK_VERSION,
            "godotVersion": GODOT_VERSION,
            "graphSelector": "GridworksCurrentR2Export=true",
            "preset": PRESET,
        },
        "package": {
            "appTreeEntryCount": payload["appTreeEntryCount"],
            "byteLength": archive.stat().st_size,
            "fileName": ARCHIVE_NAME,
            "sha256": sha256_file(archive),
            "treeEntryCount": len(tree),
            "treeSha256": sha256_bytes(canonical_bytes(tree)),
        },
        "persistence": {
            "save": {"logicalPath": SAVE_PATH, "schemaVersion": SAVE_SCHEMA},
            "settings": {
                "defaults": {
                    "ambientVolumePercent": 100,
                    "masterVolumePercent": 100,
                    "reduceMotion": False,
                    "sfxVolumePercent": 100,
                    "uiScalePercent": 100,
                    "windowMode": "windowed",
                },
                "logicalPath": SETTINGS_PATH,
                "schemaVersion": SETTINGS_SCHEMA,
            },
        },
        "platform": {
            "architectures": payload["architectures"],
            "bundleIdentifier": payload["bundleIdentifier"],
            "minimumMacOS": payload["minimumMacOS"],
            "operatingSystem": "macOS",
            "productVersion": payload["productVersion"],
            "signature": payload["signature"],
        },
        "schemaVersion": SCHEMA,
        "source": source,
    }


def verify_manifest(manifest_path: Path) -> None:
    require_host_versions()
    source_commit = require_clean_source()
    manifest_path = manifest_path.resolve()
    manifest = strict_json(
        manifest_path,
        label="candidate manifest",
        canonical=True,
    )
    if not isinstance(manifest, dict) or manifest.get("schemaVersion") != SCHEMA:
        raise CandidateError("unsupported candidate manifest schema")
    package = manifest.get("package")
    if not isinstance(package, dict) or package.get("fileName") != ARCHIVE_NAME:
        raise CandidateError("candidate manifest package filename drift")
    source = manifest.get("source")
    if not isinstance(source, dict) or source.get("commit") != source_commit:
        raise CandidateError("candidate manifest source commit differs from clean HEAD")
    archive = manifest_path.parent / ARCHIVE_NAME
    claimed_size = package.get("byteLength")
    claimed_sha256 = package.get("sha256")
    if (
        type(claimed_size) is not int
        or claimed_size < 1
        or claimed_size > MAX_ARCHIVE_BYTES
    ):
        raise CandidateError("candidate manifest archive size is invalid")
    if (
        not isinstance(claimed_sha256, str)
        or len(claimed_sha256) != 64
        or any(character not in "0123456789abcdef" for character in claimed_sha256)
    ):
        raise CandidateError("candidate manifest archive hash is invalid")
    if not archive.is_file() or archive.is_symlink():
        raise CandidateError("candidate archive is missing or not a regular file")
    actual_size = archive.stat().st_size
    if actual_size > MAX_ARCHIVE_BYTES:
        raise CandidateError("candidate archive exceeds the bounded byte ceiling")
    if actual_size != claimed_size or sha256_file(archive) != claimed_sha256:
        raise CandidateError("candidate archive bytes differ from manifest identity")
    with tempfile.TemporaryDirectory(prefix="gridworks-r2-verify-") as raw:
        extracted = Path(raw) / "package"
        tree = extract_archive(archive, extracted)
        expected = expected_manifest(archive, extracted, tree, source_commit)
        if canonical_bytes(manifest) != canonical_bytes(expected):
            raise CandidateError(
                "candidate manifest differs from independently reconstructed authority"
            )
        packaged_title_smoke(extracted)
    print(f"R2_CANDIDATE_VERIFY_PASS manifest={manifest_path} archive={archive}")


def copy_legal_files(stage: Path) -> None:
    for relative in LEGAL_PATHS:
        source = ROOT / relative
        if not source.is_file() or source.is_symlink():
            raise CandidateError(f"required legal source is missing: {relative}")
        target = stage / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)


def publish_candidate(candidate_archive: Path, manifest_bytes: bytes) -> None:
    final_archive = DIST / ARCHIVE_NAME
    final_manifest = DIST / MANIFEST_NAME
    archive_temp = DIST / f".{ARCHIVE_NAME}.tmp"
    manifest_temp = DIST / f".{MANIFEST_NAME}.tmp"
    archive_backup = DIST / f".{ARCHIVE_NAME}.previous"
    manifest_backup = DIST / f".{MANIFEST_NAME}.previous"

    if archive_backup.exists():
        os.replace(archive_backup, final_archive)
    if manifest_backup.exists():
        os.replace(manifest_backup, final_manifest)
    shutil.copy2(candidate_archive, archive_temp)
    manifest_temp.write_bytes(manifest_bytes)
    had_archive = final_archive.exists()
    had_manifest = final_manifest.exists()
    try:
        if had_archive:
            os.replace(final_archive, archive_backup)
        if had_manifest:
            os.replace(final_manifest, manifest_backup)
        os.replace(archive_temp, final_archive)
        os.replace(manifest_temp, final_manifest)
    except BaseException:
        if had_archive and archive_backup.exists():
            os.replace(archive_backup, final_archive)
        elif not had_archive:
            final_archive.unlink(missing_ok=True)
        if had_manifest and manifest_backup.exists():
            os.replace(manifest_backup, final_manifest)
        elif not had_manifest:
            final_manifest.unlink(missing_ok=True)
        raise
    else:
        archive_backup.unlink(missing_ok=True)
        manifest_backup.unlink(missing_ok=True)


def build_candidate() -> None:
    source_commit = require_clean_source()
    require_host_versions()
    DIST.mkdir(parents=True, exist_ok=True)
    final_archive = DIST / ARCHIVE_NAME
    final_manifest = DIST / MANIFEST_NAME
    with tempfile.TemporaryDirectory(prefix="gridworks-r2-build-") as raw:
        work = Path(raw)
        raw_archive = work / "raw.zip"
        export_log = work / "export.log"
        environment = dict(os.environ)
        environment.pop(QUALIFICATION_DATA_ENV, None)
        environment.pop(QUALIFICATION_SCENARIO_ENV, None)
        environment["GridworksCurrentR2Export"] = "true"
        environment.pop("GridworksLegacyV2Export", None)
        run(
            [
                require_tool("dotnet"),
                "clean",
                str(GAME / "Gridworks.Game.csproj"),
                "-c",
                "ExportRelease",
                "--nologo",
                "-v:minimal",
            ],
            env=environment,
        )
        run(
            [
                godot_path(),
                "--headless",
                "--path",
                str(GAME),
                "--export-release",
                PRESET,
                str(raw_archive),
                "--log-file",
                str(export_log),
            ],
            env=environment,
            timeout=900,
        )
        stage = work / "stage"
        extract_archive(raw_archive, stage)
        app = stage / "Gridworks.app"
        if not app.is_dir():
            raise CandidateError("Godot export did not produce Gridworks.app")
        if list(app.rglob("*.pdb")):
            raise CandidateError("raw current R2 export contains a PDB")
        codesign = require_tool("codesign", "/usr/bin/codesign")
        run(
            [
                codesign,
                "--force",
                "--deep",
                "--sign",
                "-",
                "--options",
                "runtime",
                "--preserve-metadata=entitlements",
                str(app),
            ]
        )
        copy_legal_files(stage)
        candidate_archive = work / ARCHIVE_NAME
        run(
            [
                require_tool("ditto", "/usr/bin/ditto"),
                "-c",
                "-k",
                "--sequesterRsrc",
                "--keepParent",
                str(app),
                str(candidate_archive),
            ]
        )
        run(
            [require_tool("zip", "/usr/bin/zip"), "-q", "-r", str(candidate_archive), *LEGAL_PATHS],
            cwd=stage,
        )
        verification = work / "verification"
        tree = extract_archive(candidate_archive, verification)
        manifest = expected_manifest(candidate_archive, verification, tree, source_commit)
        if require_clean_source() != source_commit:
            raise CandidateError("source changed while the candidate was being built")
        manifest_bytes = canonical_bytes(manifest) + b"\n"
        candidate_manifest = work / MANIFEST_NAME
        candidate_manifest.write_bytes(manifest_bytes)
        verify_manifest(candidate_manifest)
        publish_candidate(candidate_archive, manifest_bytes)
    print(f"R2_CANDIDATE_BUILD_PASS manifest={final_manifest} archive={final_archive}")


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    subparsers.add_parser("build", help="build and verify the clean-HEAD candidate")
    verify = subparsers.add_parser("verify", help="verify an existing candidate manifest")
    verify.add_argument("manifest", type=Path)
    return parser.parse_args()


def main() -> int:
    try:
        arguments = parse_arguments()
        if arguments.command == "build":
            build_candidate()
        else:
            verify_manifest(arguments.manifest)
        return 0
    except (CandidateError, OSError, subprocess.TimeoutExpired, zipfile.BadZipFile) as exception:
        print(f"R2_CANDIDATE_FAIL {exception}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
