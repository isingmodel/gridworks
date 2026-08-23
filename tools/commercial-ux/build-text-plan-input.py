#!/usr/bin/env python3
"""Build a hash-pinned realtime text-plan artifact from current V2/V3 authority."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import realtime_text_contract as contract


def fail(message: str) -> None:
    raise SystemExit(message)


def read_object(path: Path, label: str) -> tuple[bytes, dict[str, Any]]:
    try:
        raw = path.read_bytes()
        value = json.loads(raw)
    except (OSError, json.JSONDecodeError) as exception:
        fail(f"{label} is unreadable: {exception}")
    if not isinstance(value, dict):
        fail(f"{label} must be a JSON object")
    return raw, value


def checked(callable_: Any, label: str) -> Any:
    try:
        return callable_()
    except ValueError as exception:
        fail(f"{label}: {exception}")


def require_field(value: dict[str, Any], field: str, label: str) -> Any:
    if field not in value:
        fail(f"{label} is missing required field {field!r}")
    return value[field]


def story_content(value: Any, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        fail(f"{label} must be an object")
    if set(value) != {"speaker", "title", "body"}:
        fail(f"{label} must contain exactly speaker/title/body")
    for field in ("speaker", "title", "body"):
        checked(lambda f=field: contract.nonempty(value[f], f"{label}.{f}"), label)
    return {
        "contentType": "story-card",
        "speaker": value["speaker"],
        "title": value["title"],
        "body": value["body"],
    }


def promise_content(
    promise_id: Any,
    branch: str,
    text: Any,
    label: str,
) -> dict[str, Any]:
    promise = checked(lambda: contract.nonempty(promise_id, f"{label}.promiseId"), label)
    line = checked(lambda: contract.nonempty(text, f"{label}.{branch}"), label)
    return {
        "contentType": "promise-line",
        "promiseId": promise,
        "branch": branch,
        "text": line,
    }


def extract_base_content(campaign: dict[str, Any]) -> tuple[list[dict[str, Any]], list[str]]:
    if campaign.get("schemaVersion") != contract.BASE_CAMPAIGN_SCHEMA:
        fail(f"campaign schemaVersion must be {contract.BASE_CAMPAIGN_SCHEMA}")
    if campaign.get("campaignId") != contract.CAMPAIGN_ID:
        fail(f"campaign campaignId must be {contract.CAMPAIGN_ID}")
    chapters = campaign.get("chapters")
    if not isinstance(chapters, list) or len(chapters) != len(contract.EXPECTED_CHAPTER_IDS):
        fail("campaign chapters must contain exactly eight ordered chapters")

    contents: list[dict[str, Any]] = []
    display_names: list[str] = []
    actual_ids: list[str] = []
    promises: dict[str, str] = {}
    for index, chapter in enumerate(chapters):
        label = f"campaign.chapters[{index}]"
        if not isinstance(chapter, dict):
            fail(f"{label} must be an object")
        chapter_id = checked(
            lambda: contract.nonempty(require_field(chapter, "chapterId", label), f"{label}.chapterId"),
            label,
        )
        actual_ids.append(chapter_id)
        display_names.append(checked(
            lambda: contract.nonempty(require_field(chapter, "displayName", label), f"{label}.displayName"),
            label,
        ))
        contents.append(story_content(require_field(chapter, "briefing", label), f"{label}.briefing"))

        windows = require_field(chapter, "decisionWindows", label)
        if not isinstance(windows, list):
            fail(f"{label}.decisionWindows must be an array")
        seen_windows: set[str] = set()
        for window_index, window in enumerate(windows):
            window_label = f"{label}.decisionWindows[{window_index}]"
            if not isinstance(window, dict):
                fail(f"{window_label} must be an object")
            window_id = checked(
                lambda: contract.nonempty(
                    require_field(window, "windowId", window_label),
                    f"{window_label}.windowId",
                ),
                window_label,
            )
            if window_id in seen_windows:
                fail(f"{label} contains duplicate windowId {window_id!r}")
            seen_windows.add(window_id)
            story = require_field(window, "story", window_label)
            if story is not None:
                contents.append(story_content(story, f"{window_label}.story"))

        promise = require_field(chapter, "cityPromise", label)
        results = require_field(chapter, "resultCards", label)
        if not isinstance(results, dict) or set(results) != {"standard", "kept", "deferred"}:
            fail(f"{label}.resultCards must contain exactly standard/kept/deferred")
        if promise is None:
            if results["standard"] is None:
                fail(f"{label}.resultCards.standard must be authored without a city promise")
            if results["kept"] is not None or results["deferred"] is not None:
                fail(f"{label} cannot author kept/deferred results without a city promise")
            contents.append(story_content(results["standard"], f"{label}.resultCards.standard"))
        else:
            if not isinstance(promise, dict):
                fail(f"{label}.cityPromise must be an object or null")
            promise_id = checked(
                lambda: contract.nonempty(
                    require_field(promise, "promiseId", f"{label}.cityPromise"),
                    f"{label}.cityPromise.promiseId",
                ),
                label,
            )
            promises[chapter_id] = promise_id
            if results["kept"] is None or results["deferred"] is None:
                fail(f"{label} must author both kept and deferred result cards")
            contents.append(story_content(results["kept"], f"{label}.resultCards.kept"))
            contents.append(story_content(results["deferred"], f"{label}.resultCards.deferred"))

    if tuple(actual_ids) != contract.EXPECTED_CHAPTER_IDS:
        fail(f"campaign chapter order mismatch: {actual_ids}")

    epilogue = campaign.get("epilogue")
    if not isinstance(epilogue, dict):
        fail("campaign.epilogue must be an object")
    for field in ("cityReport", "medicalWitness", "closing"):
        contents.append(story_content(require_field(epilogue, field, "campaign.epilogue"), f"campaign.epilogue.{field}"))
    promise_lines = require_field(epilogue, "promiseLines", "campaign.epilogue")
    if not isinstance(promise_lines, list):
        fail("campaign.epilogue.promiseLines must be an array")
    expected_promise_chapters = [
        "NORTH_BANK_PROMISE",
        "WHOSE_MARGIN",
        "BEFORE_WATER_RISE",
    ]
    actual_promise_chapters: list[str] = []
    for index, line in enumerate(promise_lines):
        label = f"campaign.epilogue.promiseLines[{index}]"
        if not isinstance(line, dict):
            fail(f"{label} must be an object")
        if set(line) != {"chapterId", "promiseId", "kept", "deferred"}:
            fail(f"{label} must contain exactly chapterId/promiseId/kept/deferred")
        chapter_id = checked(lambda: contract.nonempty(line["chapterId"], f"{label}.chapterId"), label)
        actual_promise_chapters.append(chapter_id)
        if promises.get(chapter_id) != line["promiseId"]:
            fail(f"{label}.promiseId does not match its chapter cityPromise")
        contents.append(promise_content(line["promiseId"], "keep", line["kept"], label))
        contents.append(promise_content(line["promiseId"], "defer", line["deferred"], label))
    if actual_promise_chapters != expected_promise_chapters:
        fail(f"epilogue promise line order mismatch: {actual_promise_chapters}")
    if len(contents) != len(contract.EXPECTED_PARTS):
        fail(
            f"campaign narrative topology has {len(contents)} atoms; "
            f"expected {len(contract.EXPECTED_PARTS)}"
        )
    return contents, display_names


def validate_realtime_campaign(realtime: dict[str, Any]) -> None:
    if realtime.get("schemaVersion") != contract.REALTIME_CAMPAIGN_SCHEMA:
        fail(f"realtime campaign schemaVersion must be {contract.REALTIME_CAMPAIGN_SCHEMA}")
    if realtime.get("campaignId") != contract.CAMPAIGN_ID:
        fail(f"realtime campaign campaignId must be {contract.CAMPAIGN_ID}")
    chapters = realtime.get("chapters")
    if not isinstance(chapters, list) or len(chapters) != len(contract.EXPECTED_CHAPTER_IDS):
        fail("realtime campaign chapters must contain exactly eight ordered chapters")
    for index, (chapter, chapter_id) in enumerate(zip(chapters, contract.EXPECTED_CHAPTER_IDS)):
        label = f"realtime campaign.chapters[{index}]"
        if not isinstance(chapter, dict):
            fail(f"{label} must be an object")
        expected = contract.SCHEDULES[chapter_id]
        checked(
            lambda c=chapter, e=expected, l=label: contract.validate_schedule(c, e, l),
            label,
        )


def validate_all(
    context: dict[str, Any],
    manifest: dict[str, Any],
    campaign: dict[str, Any],
    realtime: dict[str, Any],
) -> None:
    checked(lambda: contract.validate_context(context), "context")
    checked(lambda: contract.validate_manifest(manifest), "story manifest")
    contents, display_names = extract_base_content(campaign)
    validate_realtime_campaign(realtime)
    for index, (chapter, expected_name) in enumerate(zip(context["chapters"], display_names)):
        if chapter["displayName"] != expected_name:
            fail(
                f"context.chapters[{index}].displayName does not match campaign: "
                f"expected {expected_name!r}, got {chapter['displayName']!r}"
            )
    for index, (part, expected_content) in enumerate(zip(manifest["parts"], contents)):
        if part["content"] != expected_content:
            fail(
                f"story manifest part {index} content does not match campaign authority "
                f"for {part['selector']!r}"
            )


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def sha256(raw: bytes) -> str:
    return "sha256:" + hashlib.sha256(raw).hexdigest()


def build_envelope(
    context: dict[str, Any],
    manifest: dict[str, Any],
    source_bytes: dict[str, bytes],
) -> dict[str, Any]:
    artifact = {
        "schemaVersion": contract.ARTIFACT_SCHEMA,
        "campaignId": manifest["campaignId"],
        "premise": context["premise"],
        "playerRole": context["playerRole"],
        "runtimeAuthority": context["runtimeAuthority"],
        "chapters": context["chapters"],
        "storyParts": manifest["parts"],
    }
    artifact_sha256 = sha256(canonical_json_bytes(artifact))
    bound_payload = {
        "artifactSha256": artifact_sha256,
        "sourceBindings": {
            name: sha256(raw) for name, raw in sorted(source_bytes.items())
        },
        "artifact": artifact,
    }
    return {
        "schemaVersion": contract.ENVELOPE_SCHEMA,
        "textPlanSha256": sha256(canonical_json_bytes(bound_payload)),
        **bound_payload,
    }


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Build the blinded realtime commercial UX text-plan input."
    )
    parser.add_argument("--story-manifest", type=Path, required=True)
    parser.add_argument("--campaign", type=Path, required=True)
    parser.add_argument("--realtime-campaign", type=Path, required=True)
    parser.add_argument(
        "--context",
        type=Path,
        default=Path(__file__).with_name("text-plan-context.json"),
    )
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    context_raw, context = read_object(args.context, "context")
    manifest_raw, manifest = read_object(args.story_manifest, "story manifest")
    campaign_raw, campaign = read_object(args.campaign, "campaign")
    realtime_raw, realtime = read_object(args.realtime_campaign, "realtime campaign")
    validate_all(context, manifest, campaign, realtime)
    envelope = build_envelope(
        context,
        manifest,
        {
            "baseCampaign": campaign_raw,
            "context": context_raw,
            "realtimeCampaign": realtime_raw,
            "storyManifest": manifest_raw,
        },
    )
    output = json.dumps(envelope, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(output, encoding="utf-8")
    print(output, end="")


if __name__ == "__main__":
    main()
