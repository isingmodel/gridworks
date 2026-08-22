#!/usr/bin/env python3
"""Pin the ten G.3 comparison pairs and their four-call jury ledger."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("repository", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    root = args.repository.resolve()
    output = args.output.resolve()
    candidate = root / "playtests/commercial-2d/g3-final-candidate"
    pairs = [
        ("PAIR-NORMAL", root / "assets/01-grid-construction.png", candidate / "runtime/pair-normal.png",
         ["camera", "density", "river", "scale", "material", "grid", "hud", "timeline"]),
        ("PAIR-HEAT", root / "assets/02-heatwave-outage.png", candidate / "runtime/pair-heat.png",
         ["camera", "density", "river", "scale", "material", "grid", "hud", "state", "timeline"]),
        ("PAIR-ROUTE", root / "assets/03-route-comparison.png", candidate / "runtime/pair-route.png",
         ["camera", "density", "river", "scale", "material", "grid", "hud", "timeline"]),
        ("PAIR-SITING", root / "assets/04-plant-siting.png", candidate / "runtime/pair-siting.png",
         ["camera", "density", "river", "scale", "material", "grid", "hud"]),
        ("PAIR-FLOOD", candidate / "boards/pair-flood-reference.png", candidate / "boards/pair-flood-candidate-comparison.png",
         ["camera", "river", "material", "grid", "state"]),
        ("PAIR-KIT-GROUND", candidate / "boards/pair-kit-ground-reference.png", candidate / "boards/pair-kit-ground-candidate-comparison.png",
         ["camera", "material", "state"]),
        ("PAIR-KIT-RIVER", candidate / "boards/pair-kit-river-reference.png", candidate / "boards/pair-kit-river-candidate-comparison.png",
         ["camera", "river", "material", "state"]),
        ("PAIR-KIT-GRID", candidate / "boards/pair-kit-grid-reference.png", candidate / "boards/pair-kit-grid-candidate-comparison.png",
         ["camera", "scale", "material", "grid"]),
        ("PAIR-KIT-CITY", candidate / "boards/pair-kit-city-reference.png", candidate / "boards/pair-kit-city-candidate-comparison.png",
         ["camera", "scale", "material"]),
        ("PAIR-KIT-UI", candidate / "boards/pair-kit-ui-reference.png", candidate / "boards/pair-kit-ui-candidate.png",
         ["material", "hud", "timeline"]),
    ]

    rows = []
    for pair_id, reference, comparison, criteria in pairs:
        for path in (reference, comparison):
            if not path.is_file():
                raise SystemExit(f"missing jury image: {path}")
        slug = pair_id.lower().replace("pair-", "").replace("_", "-")
        judgments = [
            f"judgments/{slug}-reference-first-r1.json",
            f"judgments/{slug}-candidate-first-r1.json",
            f"judgments/{slug}-reference-first-r2.json",
            f"judgments/{slug}-candidate-first-r2.json",
        ]
        rows.append({
            "pairId": pair_id,
            "referencePath": str(reference),
            "referenceSha256": sha256(reference),
            "candidatePath": str(comparison),
            "candidateSha256": sha256(comparison),
            "criteria": criteria,
            "judgments": judgments,
        })

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps({
        "protocol": "G3-REFERENCE-PARITY-v2",
        "judge": {
            "model": "gpt-5.6-sol",
            "reasoningEffort": "ultra",
            "slot": "SOL-ULTRA",
            "codexCliVersion": "codex-cli 0.149.0",
        },
        "viewport": "1920x1080",
        "uiScalePercent": 100,
        "pairCount": len(rows),
        "callCount": len(rows) * 4,
        "pairs": rows,
    }, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
