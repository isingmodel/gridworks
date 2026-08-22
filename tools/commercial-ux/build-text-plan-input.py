#!/usr/bin/env python3
"""Build one hash-pinned blinded text-plan artifact from a strict story manifest."""

from __future__ import annotations

import argparse
import hashlib
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any


CONTEXT_SCHEMA = "gridworks.commercial-ux.text-plan-context.v1"
CAMPAIGN_SCHEMA = "gridworks.commercial.campaign.v2"
MANIFEST_SCHEMA = "gridworks.commercial.story-manifest.v1"
PART_SCHEMA = "gridworks.commercial.story-part-output.v1"
ARTIFACT_SCHEMA = "gridworks.commercial-ux.text-plan-input.v1"
ENVELOPE_SCHEMA = "gridworks.commercial-ux.text-plan-envelope.v1"


@dataclass(frozen=True)
class ExpectedPart:
    selector: str
    kind: str
    chapter_id: str | None
    window_id: str | None = None
    required_promise_branch: str | None = None


EXPECTED_PARTS = (
    ExpectedPart("FIRST_LIGHT/briefing", "briefing", "FIRST_LIGHT"),
    ExpectedPart("FIRST_LIGHT/result/standard", "result", "FIRST_LIGHT"),
    ExpectedPart("SECOND_HEART/briefing", "briefing", "SECOND_HEART"),
    ExpectedPart("SECOND_HEART/result/standard", "result", "SECOND_HEART"),
    ExpectedPart("SECOND_SOURCE/briefing", "briefing", "SECOND_SOURCE"),
    ExpectedPart(
        "SECOND_SOURCE/window/SECOND_SOURCE_BUILD",
        "window",
        "SECOND_SOURCE",
        window_id="SECOND_SOURCE_BUILD",
    ),
    ExpectedPart("SECOND_SOURCE/result/standard", "result", "SECOND_SOURCE"),
    ExpectedPart("NORTH_BANK_PROMISE/briefing", "briefing", "NORTH_BANK_PROMISE"),
    ExpectedPart(
        "NORTH_BANK_PROMISE/result/keep",
        "result",
        "NORTH_BANK_PROMISE",
        required_promise_branch="keep",
    ),
    ExpectedPart(
        "NORTH_BANK_PROMISE/result/defer",
        "result",
        "NORTH_BANK_PROMISE",
        required_promise_branch="defer",
    ),
    ExpectedPart("WHOSE_MARGIN/briefing", "briefing", "WHOSE_MARGIN"),
    ExpectedPart(
        "WHOSE_MARGIN/window/AFTER_HEAT_SAFETY",
        "window",
        "WHOSE_MARGIN",
        window_id="AFTER_HEAT_SAFETY",
    ),
    ExpectedPart(
        "WHOSE_MARGIN/result/keep",
        "result",
        "WHOSE_MARGIN",
        required_promise_branch="keep",
    ),
    ExpectedPart(
        "WHOSE_MARGIN/result/defer",
        "result",
        "WHOSE_MARGIN",
        required_promise_branch="defer",
    ),
    ExpectedPart("BEFORE_WATER_REACHES/briefing", "briefing", "BEFORE_WATER_REACHES"),
    ExpectedPart(
        "BEFORE_WATER_REACHES/window/FLOOD_BYPASS_BUILD",
        "window",
        "BEFORE_WATER_REACHES",
        window_id="FLOOD_BYPASS_BUILD",
    ),
    ExpectedPart("BEFORE_WATER_REACHES/result/standard", "result", "BEFORE_WATER_REACHES"),
    ExpectedPart("SHUT_DOWN_TO_KEEP/briefing", "briefing", "SHUT_DOWN_TO_KEEP"),
    ExpectedPart(
        "SHUT_DOWN_TO_KEEP/window/MAINTENANCE_BYPASS_BUILD",
        "window",
        "SHUT_DOWN_TO_KEEP",
        window_id="MAINTENANCE_BYPASS_BUILD",
    ),
    ExpectedPart(
        "SHUT_DOWN_TO_KEEP/result/keep",
        "result",
        "SHUT_DOWN_TO_KEEP",
        required_promise_branch="keep",
    ),
    ExpectedPart(
        "SHUT_DOWN_TO_KEEP/result/defer",
        "result",
        "SHUT_DOWN_TO_KEEP",
        required_promise_branch="defer",
    ),
    ExpectedPart("LONGEST_NIGHT/briefing", "briefing", "LONGEST_NIGHT"),
    ExpectedPart(
        "LONGEST_NIGHT/window/LAST_STORM_APPROVAL",
        "window",
        "LONGEST_NIGHT",
        window_id="LAST_STORM_APPROVAL",
    ),
    ExpectedPart(
        "LONGEST_NIGHT/result/keep",
        "result",
        "LONGEST_NIGHT",
        required_promise_branch="keep",
    ),
    ExpectedPart(
        "LONGEST_NIGHT/result/defer",
        "result",
        "LONGEST_NIGHT",
        required_promise_branch="defer",
    ),
    ExpectedPart("campaign/epilogue", "epilogue", None),
)

EXPECTED_CHAPTER_IDS = (
    "FIRST_LIGHT",
    "SECOND_HEART",
    "SECOND_SOURCE",
    "NORTH_BANK_PROMISE",
    "WHOSE_MARGIN",
    "BEFORE_WATER_REACHES",
    "SHUT_DOWN_TO_KEEP",
    "LONGEST_NIGHT",
)


def fail(message: str) -> None:
    raise SystemExit(message)


def read_object(path: Path, label: str) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exception:
        fail(f"{label} is unreadable: {exception}")
    if not isinstance(value, dict):
        fail(f"{label} must be a JSON object")
    return value


def require_exact_keys(value: dict[str, Any], expected: set[str], label: str) -> None:
    actual = set(value)
    if actual != expected:
        missing = sorted(expected - actual)
        extra = sorted(actual - expected)
        fail(f"{label} keys mismatch: missing={missing}, extra={extra}")


def require_nonempty_string(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value.strip():
        fail(f"{label} must be a nonempty string")
    return value


def validate_context(context: dict[str, Any]) -> None:
    require_exact_keys(
        context,
        {"schemaVersion", "campaignId", "premise", "playerRole", "chapters"},
        "context",
    )
    if context["schemaVersion"] != CONTEXT_SCHEMA:
        fail(f"context schemaVersion must be {CONTEXT_SCHEMA}")
    require_nonempty_string(context["campaignId"], "context.campaignId")
    require_nonempty_string(context["premise"], "context.premise")
    require_nonempty_string(context["playerRole"], "context.playerRole")
    chapters = context["chapters"]
    if not isinstance(chapters, list) or len(chapters) != len(EXPECTED_CHAPTER_IDS):
        fail("context.chapters must contain exactly eight ordered chapters")
    actual_ids: list[str] = []
    for index, chapter in enumerate(chapters, start=1):
        if not isinstance(chapter, dict):
            fail(f"context.chapters[{index - 1}] must be an object")
        require_exact_keys(
            chapter,
            {
                "order",
                "chapterId",
                "displayName",
                "phase",
                "learningIntent",
                "crisisIntent",
                "choiceIntent",
            },
            f"context.chapters[{index - 1}]",
        )
        if type(chapter["order"]) is not int or chapter["order"] != index:
            fail(f"context chapter order must be exactly 1..8; invalid row {index}")
        actual_ids.append(require_nonempty_string(chapter["chapterId"], f"chapter {index} chapterId"))
        require_nonempty_string(chapter["displayName"], f"chapter {index} displayName")
        expected_phase = "tutorial" if index <= 3 else "main"
        if chapter["phase"] != expected_phase:
            fail(f"chapter {index} phase must be {expected_phase}")
        for field in ("learningIntent", "crisisIntent", "choiceIntent"):
            require_nonempty_string(chapter[field], f"chapter {index} {field}")
    if tuple(actual_ids) != EXPECTED_CHAPTER_IDS:
        fail(f"context chapter order mismatch: {actual_ids}")


def validate_story(part: Any, label: str) -> None:
    if not isinstance(part, dict):
        fail(f"{label} story must be an object")
    require_exact_keys(part, {"speaker", "title", "body"}, f"{label}.story")
    for field in ("speaker", "title", "body"):
        require_nonempty_string(part[field], f"{label}.story.{field}")


def validate_part(part: Any, expected: ExpectedPart, campaign_id: str, index: int) -> None:
    label = f"story manifest part {index}"
    if not isinstance(part, dict):
        fail(f"{label} must be an object")
    require_exact_keys(
        part,
        {
            "schemaVersion",
            "campaignId",
            "selector",
            "kind",
            "chapterId",
            "windowId",
            "reachable",
            "requiredPromiseBranch",
            "story",
        },
        label,
    )
    expected_values = {
        "schemaVersion": PART_SCHEMA,
        "campaignId": campaign_id,
        "selector": expected.selector,
        "kind": expected.kind,
        "chapterId": expected.chapter_id,
        "windowId": expected.window_id,
        "reachable": True,
        "requiredPromiseBranch": expected.required_promise_branch,
    }
    for field, expected_value in expected_values.items():
        if part[field] != expected_value or (
            field == "reachable" and part[field] is not True
        ):
            fail(
                f"{label} {field} mismatch: expected {expected_value!r}, "
                f"got {part[field]!r}"
            )
    validate_story(part["story"], label)


def validate_manifest(manifest: dict[str, Any], campaign_id: str) -> None:
    require_exact_keys(
        manifest,
        {"schemaVersion", "campaignId", "count", "parts"},
        "story manifest",
    )
    if manifest["schemaVersion"] != MANIFEST_SCHEMA:
        fail(f"story manifest schemaVersion must be {MANIFEST_SCHEMA}")
    if manifest["campaignId"] != campaign_id:
        fail(
            "story manifest campaignId mismatch: "
            f"expected {campaign_id!r}, got {manifest['campaignId']!r}"
        )
    if type(manifest["count"]) is not int or manifest["count"] != len(EXPECTED_PARTS):
        fail(f"story manifest count must be exactly {len(EXPECTED_PARTS)}")
    parts = manifest["parts"]
    if not isinstance(parts, list) or len(parts) != len(EXPECTED_PARTS):
        fail(f"story manifest parts must contain exactly {len(EXPECTED_PARTS)} rows")
    for index, (part, expected) in enumerate(zip(parts, EXPECTED_PARTS), start=1):
        validate_part(part, expected, campaign_id, index)


def require_field(value: dict[str, Any], field: str, label: str) -> Any:
    if field not in value:
        fail(f"{label} is missing required field {field!r}")
    return value[field]


def authority_story(value: Any, label: str) -> dict[str, str]:
    validate_story(value, label)
    assert isinstance(value, dict)
    return value


def extract_campaign_authority(
    campaign: dict[str, Any],
    expected_campaign_id: str,
) -> list[tuple[ExpectedPart, dict[str, str]]]:
    if campaign.get("schemaVersion") != CAMPAIGN_SCHEMA:
        fail(f"campaign schemaVersion must be {CAMPAIGN_SCHEMA}")
    if campaign.get("campaignId") != expected_campaign_id:
        fail(
            "campaign campaignId mismatch: "
            f"expected {expected_campaign_id!r}, got {campaign.get('campaignId')!r}"
        )
    chapters = campaign.get("chapters")
    if not isinstance(chapters, list) or len(chapters) != len(EXPECTED_CHAPTER_IDS):
        fail("campaign chapters must contain exactly eight ordered chapters")

    authority: list[tuple[ExpectedPart, dict[str, str]]] = []
    actual_chapter_ids: list[str] = []
    for chapter_index, chapter in enumerate(chapters, start=1):
        label = f"campaign chapter {chapter_index}"
        if not isinstance(chapter, dict):
            fail(f"{label} must be an object")
        chapter_id = require_nonempty_string(
            require_field(chapter, "chapterId", label),
            f"{label}.chapterId",
        )
        actual_chapter_ids.append(chapter_id)
        authority.append((
            ExpectedPart(f"{chapter_id}/briefing", "briefing", chapter_id),
            authority_story(require_field(chapter, "briefing", label), f"{label}.briefing"),
        ))

        windows = require_field(chapter, "decisionWindows", label)
        if not isinstance(windows, list):
            fail(f"{label}.decisionWindows must be an array")
        seen_window_ids: set[str] = set()
        for window_index, window in enumerate(windows, start=1):
            window_label = f"{label}.decisionWindows[{window_index - 1}]"
            if not isinstance(window, dict):
                fail(f"{window_label} must be an object")
            window_id = require_nonempty_string(
                require_field(window, "windowId", window_label),
                f"{window_label}.windowId",
            )
            if window_id in seen_window_ids:
                fail(f"{label} contains duplicate windowId {window_id!r}")
            seen_window_ids.add(window_id)
            story = require_field(window, "story", window_label)
            if story is not None:
                authority.append((
                    ExpectedPart(
                        f"{chapter_id}/window/{window_id}",
                        "window",
                        chapter_id,
                        window_id=window_id,
                    ),
                    authority_story(story, f"{window_label}.story"),
                ))

        promise = require_field(chapter, "promise", label)
        if promise is None:
            if require_field(chapter, "keptResult", label) is not None:
                fail(f"{label}.keptResult must be null without a promise")
            if require_field(chapter, "deferredResult", label) is not None:
                fail(f"{label}.deferredResult must be null without a promise")
            authority.append((
                ExpectedPart(f"{chapter_id}/result/standard", "result", chapter_id),
                authority_story(
                    require_field(chapter, "standardResult", label),
                    f"{label}.standardResult",
                ),
            ))
        else:
            if not isinstance(promise, dict):
                fail(f"{label}.promise must be an object or null")
            authority_story(
                require_field(chapter, "standardResult", label),
                f"{label}.standardResult",
            )
            authority.append((
                ExpectedPart(
                    f"{chapter_id}/result/keep",
                    "result",
                    chapter_id,
                    required_promise_branch="keep",
                ),
                authority_story(
                    require_field(chapter, "keptResult", label),
                    f"{label}.keptResult",
                ),
            ))
            authority.append((
                ExpectedPart(
                    f"{chapter_id}/result/defer",
                    "result",
                    chapter_id,
                    required_promise_branch="defer",
                ),
                authority_story(
                    require_field(chapter, "deferredResult", label),
                    f"{label}.deferredResult",
                ),
            ))

    if tuple(actual_chapter_ids) != EXPECTED_CHAPTER_IDS:
        fail(f"campaign chapter order mismatch: {actual_chapter_ids}")
    authority.append((
        ExpectedPart("campaign/epilogue", "epilogue", None),
        authority_story(
            require_field(campaign, "epilogue", "campaign"),
            "campaign.epilogue",
        ),
    ))
    actual_parts = tuple(part for part, _ in authority)
    if actual_parts != EXPECTED_PARTS:
        fail(
            "campaign reachable story order does not match the frozen 26-part contract: "
            f"{[part.selector for part in actual_parts]}"
        )
    return authority


def validate_manifest_against_campaign(
    manifest: dict[str, Any],
    campaign: dict[str, Any],
    context: dict[str, Any],
) -> None:
    campaign_id = context["campaignId"]
    validate_manifest(manifest, campaign_id)
    authority = extract_campaign_authority(campaign, campaign_id)
    campaign_chapters = campaign["chapters"]
    for index, (context_chapter, campaign_chapter) in enumerate(
        zip(context["chapters"], campaign_chapters),
        start=1,
    ):
        campaign_display_name = require_nonempty_string(
            require_field(campaign_chapter, "displayName", f"campaign chapter {index}"),
            f"campaign chapter {index}.displayName",
        )
        if context_chapter["displayName"] != campaign_display_name:
            fail(
                f"context chapter {index} displayName does not match campaign authority: "
                f"expected {campaign_display_name!r}, got {context_chapter['displayName']!r}"
            )
    for index, (manifest_part, (_, expected_story)) in enumerate(
        zip(manifest["parts"], authority),
        start=1,
    ):
        if manifest_part["story"] != expected_story:
            fail(
                f"story manifest part {index} story does not match campaign authority "
                f"for {manifest_part['selector']!r}"
            )


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def build_artifact(context: dict[str, Any], manifest: dict[str, Any]) -> dict[str, Any]:
    artifact = {
        "schemaVersion": ARTIFACT_SCHEMA,
        "campaignId": manifest["campaignId"],
        "premise": context["premise"],
        "playerRole": context["playerRole"],
        "chapters": context["chapters"],
        "storyParts": manifest["parts"],
    }
    digest = hashlib.sha256(canonical_json_bytes(artifact)).hexdigest()
    return {
        "schemaVersion": ENVELOPE_SCHEMA,
        "artifactSha256": f"sha256:{digest}",
        "artifact": artifact,
    }


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Build the blinded commercial UX text-plan input from a strict story manifest."
    )
    parser.add_argument("--story-manifest", type=Path, required=True)
    parser.add_argument("--campaign", type=Path, required=True)
    parser.add_argument(
        "--context",
        type=Path,
        default=Path(__file__).with_name("text-plan-context.json"),
    )
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    context = read_object(args.context, "context")
    manifest = read_object(args.story_manifest, "story manifest")
    campaign = read_object(args.campaign, "campaign")
    validate_context(context)
    validate_manifest_against_campaign(manifest, campaign, context)
    envelope = build_artifact(context, manifest)
    output = json.dumps(envelope, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(output, encoding="utf-8")
    print(output, end="")


if __name__ == "__main__":
    main()
