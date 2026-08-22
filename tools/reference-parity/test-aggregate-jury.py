#!/usr/bin/env python3
"""Deterministic regression examples for the categorical jury arithmetic."""

from __future__ import annotations

import json
import subprocess
import tempfile
from pathlib import Path


CRITERIA = ["camera", "density", "river", "scale", "material", "grid", "hud", "state", "timeline"]
IDENTITIES = [
    ("REFERENCE_FIRST", 1),
    ("CANDIDATE_FIRST", 1),
    ("REFERENCE_FIRST", 2),
    ("CANDIDATE_FIRST", 2),
]


def judgment(order: str, replicate: int, labels: dict[str, str]) -> dict:
    return {
        "pairId": "PAIR-SELF-TEST",
        "judgeSlot": "SOL-ULTRA",
        "order": order,
        "replicate": replicate,
        "criteria": [
            {
                "criterion": criterion,
                "label": labels[criterion],
                "confidence": "HIGH",
                "similar": ["fixed self-test similarity"],
                "different": ["fixed self-test difference"],
                "evidence": [
                    {"imageRole": "REFERENCE", "roi": "MAP", "box": [0, 0, 400, 400], "observation": "reference"},
                    {"imageRole": "CANDIDATE", "roi": "MAP", "box": [500, 500, 900, 900], "observation": "candidate"},
                ],
                "criticalFailure": False,
            }
            for criterion in CRITERIA
        ],
    }


def aggregate(call_labels: list[dict[str, str]]) -> dict:
    with tempfile.TemporaryDirectory(prefix="gridworks-jury-self-test-") as directory:
        root = Path(directory)
        paths = []
        for index, ((order, replicate), labels) in enumerate(zip(IDENTITIES, call_labels), start=1):
            path = root / f"call-{index}.json"
            path.write_text(json.dumps(judgment(order, replicate, labels)) + "\n", encoding="utf-8")
            paths.append(path.name)
        manifest = root / "manifest.json"
        manifest.write_text(json.dumps({
            "pairs": [{
                "pairId": "PAIR-SELF-TEST",
                "criteria": CRITERIA,
                "judgments": paths,
            }]
        }) + "\n", encoding="utf-8")
        output = root / "aggregate.json"
        script = Path(__file__).with_name("aggregate-jury.py")
        subprocess.run(
            ["python3", str(script), str(manifest), "--output", str(output)],
            check=True,
            capture_output=True,
            text=True,
        )
        return json.loads(output.read_text(encoding="utf-8"))


def labels(default: str, **overrides: str) -> dict[str, str]:
    return {criterion: overrides.get(criterion, default) for criterion in CRITERIA}


def main() -> None:
    all_close = aggregate([labels("CLOSE") for _ in range(4)])
    assert all_close["referenceParity"] == 85
    assert all_close["verdict"] == "PASS"
    assert "referenceParity<=80" not in all_close["visualFailures"]

    exact_eighty = aggregate([
        labels("CLOSE", scale="WEAK")
        for _ in range(4)
    ])
    assert exact_eighty["referenceParity"] == 80
    assert exact_eighty["verdict"] == "FAIL_VISUAL"
    assert "referenceParity<=80" in exact_eighty["visualFailures"]

    river_low = aggregate([labels("PARITY", river="RELATED") for _ in range(4)])
    assert river_low["referenceParity"] > 85
    assert river_low["verdict"] == "FAIL_VISUAL"
    assert "river<85" in river_low["visualFailures"]

    spread_labels = [
        labels("WEAK"),
        labels("RELATED"),
        labels("RELATED"),
        labels("RELATED"),
    ]
    high_spread = aggregate(spread_labels)
    assert high_spread["disagreementPenalty"] == 7.5
    assert high_spread["verdict"] == "FAIL_VISUAL"
    assert "penalty>5" in high_spread["visualFailures"]

    unstable = aggregate([
        labels("DIFFERENT"),
        labels("PARITY"),
        labels("DIFFERENT"),
        labels("PARITY"),
    ])
    assert unstable["verdict"] == "BLOCKED_JUDGE_INSTABILITY"
    assert unstable["referenceParity"] is None
    print("aggregate-jury self-test: PASS (5 scenarios)")


if __name__ == "__main__":
    main()
