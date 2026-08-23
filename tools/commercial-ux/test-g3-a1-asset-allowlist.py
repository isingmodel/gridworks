#!/usr/bin/env python3
"""Deterministically verify the canonical realtime G3 visual import."""

from __future__ import annotations

import hashlib
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
SOURCE_COMMIT = "cf5da56"
ASSET_ROOT = ROOT / "game/art/commercial/g3"
MANIFEST = ASSET_ROOT / "a1-g3-allowlist.sha256"
LEDGER = ROOT / "game/art/commercial/g3-assets.prompts.md"
LEDGER_BLOB_SHA1 = "e4cde08c90605e6144dbc18604dcbc939a597201"
EXPECTED_PNG_COUNT = 57


def relative(path: Path) -> Path:
    return path.relative_to(ROOT)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git_output(arguments: list[str]) -> bytes:
    result = subprocess.run(
        ["git", *arguments],
        cwd=ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if result.returncode != 0:
        raise AssertionError(
            f"git command failed ({result.returncode}): {' '.join(arguments)}\\n"
            f"{result.stderr.decode('utf-8', errors='replace')}"
        )
    return result.stdout


def pinned_bytes(path: Path) -> bytes:
    return git_output(["show", f"{SOURCE_COMMIT}:{path.as_posix()}"])


def parse_manifest() -> dict[Path, str]:
    assert MANIFEST.is_file(), f"missing realtime G3 allowlist: {MANIFEST}"
    entries: dict[Path, str] = {}
    for line_number, raw in enumerate(MANIFEST.read_text(encoding="utf-8").splitlines(), start=1):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        digest, separator, raw_path = line.partition("  ")
        assert separator and len(digest) == 64 and all(c in "0123456789abcdef" for c in digest), (
            f"invalid allowlist entry at line {line_number}: {raw}"
        )
        path = Path(raw_path)
        assert path.parts[:4] == ("game", "art", "commercial", "g3"), (
            f"entry escapes G3 root at line {line_number}: {path}"
        )
        assert path.suffix == ".png", f"entry is not a PNG at line {line_number}: {path}"
        assert path not in entries, f"duplicate allowlist entry: {path}"
        entries[path] = digest
    assert len(entries) == EXPECTED_PNG_COUNT, (
        f"allowlist must contain {EXPECTED_PNG_COUNT} PNGs, found {len(entries)}"
    )
    return entries


def main() -> None:
    entries = parse_manifest()
    expected_pngs = set(entries)
    actual_pngs = {relative(path) for path in ASSET_ROOT.rglob("*.png")}
    assert actual_pngs == expected_pngs, (
        "G3 PNG tree must be exactly the canonical realtime allowlist; "
        f"missing={sorted(str(path) for path in expected_pngs - actual_pngs)}, "
        f"unlisted={sorted(str(path) for path in actual_pngs - expected_pngs)}"
    )

    expected_imports = {Path(f"{path}.import") for path in expected_pngs}
    actual_imports = {relative(path) for path in ASSET_ROOT.rglob("*.png.import")}
    assert actual_imports == expected_imports, (
        "G3 import sidecars must be exactly one per allowlisted PNG; "
        f"missing={sorted(str(path) for path in expected_imports - actual_imports)}, "
        f"unlisted={sorted(str(path) for path in actual_imports - expected_imports)}"
    )

    for path, expected_digest in sorted(entries.items()):
        local = ROOT / path
        assert sha256(local) == expected_digest, f"SHA-256 mismatch: {path}"
        assert local.read_bytes() == pinned_bytes(path), f"pinned source mismatch: {path}"
        import_path = Path(f"{path}.import")
        local_import = ROOT / import_path
        assert local_import.read_bytes() == pinned_bytes(import_path), (
            f"pinned import sidecar mismatch: {import_path}"
        )

    assert LEDGER.is_file(), f"missing G3 provenance ledger: {LEDGER}"
    ledger_blob = git_output(["hash-object", "--no-filters", str(LEDGER)]).decode().strip()
    assert ledger_blob == LEDGER_BLOB_SHA1, "G3 provenance ledger blob differs from pinned source"
    assert LEDGER.read_bytes() == pinned_bytes(relative(LEDGER)), "G3 provenance ledger bytes differ"
    print(f"PASS: canonical realtime G3 allowlist ({len(entries)} PNGs), import sidecars, hashes, and pinned provenance")


if __name__ == "__main__":
    main()
