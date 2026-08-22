#!/usr/bin/env python3
"""Reject malformed or weakly grounded G.3 jury responses before aggregation."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


LABELS = {"PARITY", "CLOSE", "RELATED", "WEAK", "DIFFERENT"}
CONFIDENCE = {"HIGH", "MEDIUM", "LOW"}
ROLES = {"REFERENCE", "CANDIDATE"}


def fail(message: str) -> None:
    raise SystemExit(message)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("judgment", type=Path)
    parser.add_argument("pair_id")
    parser.add_argument("order")
    parser.add_argument("replicate", type=int)
    parser.add_argument("criteria")
    args = parser.parse_args()

    try:
        payload = json.loads(args.judgment.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exception:
        fail(f"unreadable judgment: {exception}")

    expected_criteria = args.criteria.split(",")
    expected_identity = (args.pair_id, "SOL-ULTRA", args.order, args.replicate)
    actual_identity = (
        payload.get("pairId"),
        payload.get("judgeSlot"),
        payload.get("order"),
        payload.get("replicate"),
    )
    if actual_identity != expected_identity:
        fail(f"identity mismatch: expected {expected_identity}, got {actual_identity}")

    rows = payload.get("criteria")
    if not isinstance(rows, list) or len(rows) != len(expected_criteria):
        fail("criterion count mismatch")
    names = [row.get("criterion") for row in rows if isinstance(row, dict)]
    if len(names) != len(rows) or set(names) != set(expected_criteria) or len(set(names)) != len(names):
        fail(f"criterion set mismatch: expected {expected_criteria}, got {names}")

    for row in rows:
        name = row["criterion"]
        if row.get("label") not in LABELS or row.get("confidence") not in CONFIDENCE:
            fail(f"{name}: invalid label or confidence")
        for field in ("similar", "different"):
            facts = row.get(field)
            if not isinstance(facts, list) or not 1 <= len(facts) <= 3 or any(
                not isinstance(item, str) or not item.strip() for item in facts
            ):
                fail(f"{name}: {field} must contain 1-3 nonempty observations")
        evidence = row.get("evidence")
        if not isinstance(evidence, list) or not 2 <= len(evidence) <= 3:
            fail(f"{name}: evidence must contain 2-3 grounded boxes")
        roles = {item.get("imageRole") for item in evidence if isinstance(item, dict)}
        if roles != ROLES:
            fail(f"{name}: evidence must ground both REFERENCE and CANDIDATE")
        for item in evidence:
            box = item.get("box")
            if (
                not isinstance(box, list)
                or len(box) != 4
                or any(not isinstance(value, int) or value < 0 or value > 1000 for value in box)
                or box[0] >= box[2]
                or box[1] >= box[3]
            ):
                fail(f"{name}: invalid normalized evidence box {box}")
            if not isinstance(item.get("observation"), str) or not item["observation"].strip():
                fail(f"{name}: empty evidence observation")
        if not isinstance(row.get("criticalFailure"), bool):
            fail(f"{name}: criticalFailure must be boolean")


if __name__ == "__main__":
    main()
