#!/usr/bin/env python3
"""Aggregate the fixed four-call G.3 categorical jury without LLM arithmetic."""

from __future__ import annotations

import argparse
import json
import statistics
from pathlib import Path


LABELS = ["DIFFERENT", "WEAK", "RELATED", "CLOSE", "PARITY"]
SCORES = {"DIFFERENT": 0, "WEAK": 35, "RELATED": 65, "CLOSE": 85, "PARITY": 100}
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


def lower_median(values: list[int]) -> int:
    ordered = sorted(values)
    return ordered[(len(ordered) - 1) // 2]


def numeric_median(values: list[float]) -> float:
    return float(statistics.median(values))


def read_judgment(path: Path, pair_id: str, criteria: list[str]) -> dict:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if payload.get("pairId") != pair_id or payload.get("judgeSlot") != "SOL-ULTRA":
        raise SystemExit(f"identity mismatch in {path}")
    rows = payload.get("criteria")
    if not isinstance(rows, list) or {row.get("criterion") for row in rows} != set(criteria):
        raise SystemExit(f"criterion mismatch in {path}")
    if any(row.get("label") not in SCORES for row in rows):
        raise SystemExit(f"invalid label in {path}")
    return payload


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    base = args.manifest.parent

    pair_scores: dict[str, float] = {}
    pair_results: dict[str, dict] = {}
    category_verdicts: dict[str, list[float]] = {name: [] for name in WEIGHTS}
    category_spreads: dict[str, list[float]] = {name: [] for name in WEIGHTS}
    blockers: list[str] = []
    critical_failures: list[dict] = []

    for pair in manifest["pairs"]:
        pair_id = pair["pairId"]
        criteria = pair["criteria"]
        paths = [base / item for item in pair["judgments"]]
        if len(paths) != 4 or any(not path.is_file() for path in paths):
            blockers.append(f"{pair_id}:missing-four-call-jury")
            continue
        calls = [read_judgment(path, pair_id, criteria) for path in paths]
        identities = [(call["order"], call["replicate"]) for call in calls]
        required = {
            ("REFERENCE_FIRST", 1), ("CANDIDATE_FIRST", 1),
            ("REFERENCE_FIRST", 2), ("CANDIDATE_FIRST", 2),
        }
        if set(identities) != required:
            raise SystemExit(f"{pair_id}: order/replicate set mismatch: {identities}")

        criterion_results: dict[str, dict] = {}
        for criterion in criteria:
            rows = [next(row for row in call["criteria"] if row["criterion"] == criterion) for call in calls]
            ordinals = [LABELS.index(row["label"]) for row in rows]
            reference_first = [
                ordinal for ordinal, call in zip(ordinals, calls)
                if call["order"] == "REFERENCE_FIRST"
            ]
            candidate_first = [
                ordinal for ordinal, call in zip(ordinals, calls)
                if call["order"] == "CANDIDATE_FIRST"
            ]
            order_gap = abs(lower_median(reference_first) - lower_median(candidate_first))
            ordinal_range = max(ordinals) - min(ordinals)
            unstable = order_gap > 1 or ordinal_range >= 2
            if unstable:
                blockers.append(f"{pair_id}:{criterion}:jury-instability")
            verdict_ordinal = lower_median(ordinals)
            verdict_label = LABELS[verdict_ordinal]
            verdict_score = SCORES[verdict_label]
            spread = max(SCORES[row["label"]] for row in rows) - min(
                SCORES[row["label"]] for row in rows
            )
            critical_count = sum(row.get("criticalFailure") is True for row in rows)
            if critical_count >= 3:
                critical_failures.append({
                    "pairId": pair_id,
                    "criterion": criterion,
                    "count": critical_count,
                    "verified": False,
                })
            criterion_results[criterion] = {
                "labels": [row["label"] for row in rows],
                "verdict": verdict_label,
                "score": verdict_score,
                "spread": spread,
                "orderOrdinalGap": order_gap,
                "ordinalRange": ordinal_range,
                "unstable": unstable,
                "criticalFailureCount": critical_count,
            }
            category_verdicts[criterion].append(float(verdict_score))
            category_spreads[criterion].append(float(spread))

        weight_sum = sum(WEIGHTS[criterion] for criterion in criteria)
        pair_score = sum(
            WEIGHTS[criterion] * criterion_results[criterion]["score"]
            for criterion in criteria
        ) / weight_sum
        pair_scores[pair_id] = round(pair_score, 4)
        pair_results[pair_id] = {
            "score": round(pair_score, 4),
            "criteria": criterion_results,
        }

    missing_categories = [name for name, values in category_verdicts.items() if not values]
    if missing_categories:
        blockers.append("missing-categories:" + ",".join(missing_categories))

    category_scores = {
        criterion: numeric_median(values)
        for criterion, values in category_verdicts.items() if values
    }
    category_spread_scores = {
        criterion: numeric_median(values)
        for criterion, values in category_spreads.items() if values
    }
    raw_jury = sum(WEIGHTS[name] * value for name, value in category_scores.items()) / 100
    raw_spread = sum(WEIGHTS[name] * value for name, value in category_spread_scores.items()) / 100
    penalty = min(10.0, raw_spread * 0.25)
    parity = raw_jury - penalty

    visual_failures: list[str] = []
    if parity <= 80:
        visual_failures.append("referenceParity<=80")
    for criterion in ("camera", "density", "river"):
        if category_scores.get(criterion, 0) < 85:
            visual_failures.append(f"{criterion}<85")
    for pair_id, score in pair_scores.items():
        if score < 75:
            visual_failures.append(f"{pair_id}<75")
    if penalty > 5:
        visual_failures.append("penalty>5")
    if critical_failures:
        visual_failures.append("unverified-critical-failures")

    if blockers:
        verdict = "BLOCKED_JUDGE_INSTABILITY" if any(
            "instability" in item for item in blockers
        ) else "BLOCKED_MISSING_EVIDENCE"
        official_parity: float | None = None
    elif visual_failures:
        verdict = "FAIL_VISUAL"
        official_parity = round(parity, 4)
    else:
        verdict = "PASS"
        official_parity = round(parity, 4)

    output = {
        "protocol": "G3-REFERENCE-PARITY-v2",
        "verdict": verdict,
        "referenceParity": official_parity,
        "rawJuryParity": round(raw_jury, 4),
        "disagreementPenalty": round(penalty, 4),
        "rawSpread": round(raw_spread, 4),
        "pairScores": pair_scores,
        "categoryScores": {key: round(value, 4) for key, value in category_scores.items()},
        "categorySpreads": {
            key: round(value, 4) for key, value in category_spread_scores.items()
        },
        "criticalFailures": critical_failures,
        "blockers": blockers,
        "visualFailures": visual_failures,
        "pairs": pair_results,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(output, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(output, ensure_ascii=False, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
