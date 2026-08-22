#!/usr/bin/env python3
"""Run or resume the exact four-call jury with bounded parallelism."""

from __future__ import annotations

import argparse
import concurrent.futures
import hashlib
import json
import subprocess
from pathlib import Path


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--workers", type=int, default=4)
    parser.add_argument("--timeout-seconds", type=int, default=1200)
    args = parser.parse_args()
    manifest_path = args.manifest.resolve()
    payload = json.loads(manifest_path.read_text(encoding="utf-8"))
    root = Path(__file__).resolve().parents[2]
    wrapper = root / "tools/reference-parity/run-gpt56sol-judge.sh"
    validator = root / "tools/reference-parity/validate-judgment.py"
    base = manifest_path.parent

    tasks: list[tuple[list[str], Path, Path]] = []
    identities = [
        ("REFERENCE_FIRST", 1),
        ("CANDIDATE_FIRST", 1),
        ("REFERENCE_FIRST", 2),
        ("CANDIDATE_FIRST", 2),
    ]
    for pair in payload["pairs"]:
        reference = Path(pair["referencePath"])
        candidate = Path(pair["candidatePath"])
        if sha256(reference) != pair["referenceSha256"] or sha256(candidate) != pair["candidateSha256"]:
            raise SystemExit(f"image hash drift: {pair['pairId']}")
        criteria = ",".join(pair["criteria"])
        for judgment, (order, replicate) in zip(pair["judgments"], identities):
            output = base / judgment
            output.parent.mkdir(parents=True, exist_ok=True)
            validation = [
                "python3", str(validator), str(output), pair["pairId"], order,
                str(replicate), criteria,
            ]
            if output.is_file() and subprocess.run(validation, capture_output=True).returncode == 0:
                continue
            log = output.with_suffix(".log")
            command = [
                str(wrapper), pair["pairId"], str(reference), str(candidate), order,
                str(replicate), criteria, str(output),
            ]
            tasks.append((command, log, output))

    def run(task: tuple[list[str], Path, Path]) -> str:
        command, log, output = task
        with log.open("wb") as stream:
            try:
                completed = subprocess.run(
                    command,
                    stdout=stream,
                    stderr=subprocess.STDOUT,
                    timeout=args.timeout_seconds,
                    cwd=root,
                )
            except subprocess.TimeoutExpired:
                return f"TIMEOUT {output}"
        return f"{'PASS' if completed.returncode == 0 else 'FAIL'} {output}"

    failures = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, args.workers)) as executor:
        for result in executor.map(run, tasks):
            print(result, flush=True)
            if not result.startswith("PASS "):
                failures.append(result)
    if failures:
        raise SystemExit("jury calls failed; rerun resumes completed calls")
    print(f"jury complete: executed={len(tasks)} total={payload['callCount']}")


if __name__ == "__main__":
    main()
