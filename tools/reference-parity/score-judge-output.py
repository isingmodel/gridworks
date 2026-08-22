#!/usr/bin/env python3
"""Convert the pinned categorical Gridworks judge output into a deterministic score."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


LABEL_SCORES = {
    "PARITY": 100,
    "CLOSE": 85,
    "RELATED": 65,
    "WEAK": 35,
    "DIFFERENT": 0,
}

WEIGHTS = {
    "camera": 15,
    "density": 15,
    "river": 15,
    "scale": 10,
    "material": 10,
    "grid": 10,
    "hud": 10,
    "state": 10,
    "timeline": 5,
}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("judge_json", type=Path)
    parser.add_argument(
        "--criteria",
        required=True,
        help="Comma-separated criterion names in the predeclared pair rubric",
    )
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    expected = [item.strip() for item in args.criteria.split(",") if item.strip()]
    if len(expected) != len(set(expected)) or not expected:
        raise SystemExit("criteria must be a non-empty unique comma-separated list")
    unknown = [criterion for criterion in expected if criterion not in WEIGHTS]
    if unknown:
        raise SystemExit(f"unknown criteria: {unknown}")

    payload = json.loads(args.judge_json.read_text(encoding="utf-8"))
    if payload.get("judgeSlot") != "SOL-ULTRA":
        raise SystemExit("judgeSlot must be SOL-ULTRA")

    rows = payload.get("criteria")
    if not isinstance(rows, list):
        raise SystemExit("judge output criteria must be a list")
    by_name = {row.get("criterion"): row for row in rows}
    if set(by_name) != set(expected) or len(rows) != len(by_name):
        raise SystemExit(
            f"judge criteria mismatch: expected {expected}, got {list(by_name)}"
        )

    weighted_sum = 0
    weight_sum = 0
    result_rows = []
    critical_failure = False
    for criterion in expected:
        row = by_name[criterion]
        label = row.get("label")
        if label not in LABEL_SCORES:
            raise SystemExit(f"invalid label for {criterion}: {label!r}")
        score = LABEL_SCORES[label]
        weight = WEIGHTS[criterion]
        weighted_sum += score * weight
        weight_sum += weight
        critical_failure = critical_failure or row.get("criticalFailure") is True
        result_rows.append(
            {
                "criterion": criterion,
                "label": label,
                "score": score,
                "weight": weight,
            }
        )

    pair_score = round(weighted_sum / weight_sum, 4)
    result = {
        "protocol": "G3-REFERENCE-PARITY-v2",
        "pairId": payload.get("pairId"),
        "judgeSlot": payload.get("judgeSlot"),
        "order": payload.get("order"),
        "replicate": payload.get("replicate"),
        "pairScore": pair_score,
        "criticalFailure": critical_failure,
        "threshold": 96,
        "thresholdPassed": pair_score > 96 and not critical_failure,
        "criteria": result_rows,
    }
    output = json.dumps(result, ensure_ascii=False, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(output, encoding="utf-8")
    print(output, end="")


if __name__ == "__main__":
    main()
