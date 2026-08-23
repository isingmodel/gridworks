#!/usr/bin/env python3
"""Frozen Release.V3 text-plan topology and strict validation helpers."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any


BASE_CAMPAIGN_SCHEMA = "gridworks.release.campaign.v2"
REALTIME_CAMPAIGN_SCHEMA = "gridworks.realtime.campaign.v3"
CAMPAIGN_ID = "CHEONGRYU_RELEASE_CAMPAIGN"
CONTEXT_SCHEMA = "gridworks.commercial-ux.text-plan-context.v2"
MANIFEST_SCHEMA = "gridworks.commercial.story-manifest.v2"
PART_SCHEMA = "gridworks.commercial.story-part-output.v2"
ARTIFACT_SCHEMA = "gridworks.commercial-ux.text-plan-input.v2"
ENVELOPE_SCHEMA = "gridworks.commercial-ux.text-plan-envelope.v3"


@dataclass(frozen=True)
class ScheduledEvent:
    event_id: str
    priority: int
    start_offset_minutes: int
    duration_minutes: int
    forecast_lead_minutes: int

    def as_json(self) -> dict[str, Any]:
        return {
            "eventId": self.event_id,
            "priority": self.priority,
            "startOffsetMinutes": self.start_offset_minutes,
            "durationMinutes": self.duration_minutes,
            "forecastLeadMinutes": self.forecast_lead_minutes,
        }


@dataclass(frozen=True)
class Schedule:
    chapter_id: str
    preparation_minutes: int
    promise_deadline: int | None
    events: tuple[ScheduledEvent, ...]

    @property
    def event_ids(self) -> tuple[str, ...]:
        return tuple(event.event_id for event in self.events)

    def as_json(self) -> dict[str, Any]:
        return {
            "chapterId": self.chapter_id,
            "preparationMinutes": self.preparation_minutes,
            "promiseDecisionDeadlineOffsetMinutes": self.promise_deadline,
            "scheduledEvents": [event.as_json() for event in self.events],
        }


@dataclass(frozen=True)
class ExpectedPart:
    selector: str
    kind: str
    chapter_id: str | None
    window_id: str | None = None
    required_promise_branch: str | None = None
    content_type: str = "story-card"


EXPECTED_CHAPTER_IDS = (
    "FIRST_LIGHT",
    "SECOND_HEART",
    "SECOND_SOURCE",
    "NORTH_BANK_PROMISE",
    "WHOSE_MARGIN",
    "BEFORE_WATER_RISE",
    "SWITCH_OFF_TO_PROTECT",
    "LONGEST_NIGHT",
)

SCHEDULES = {
    "FIRST_LIGHT": Schedule(
        "FIRST_LIGHT",
        240,
        None,
        (ScheduledEvent("FIRST_LIGHT_SUPPLY", 0, 240, 60, 240),),
    ),
    "SECOND_HEART": Schedule(
        "SECOND_HEART",
        360,
        None,
        (
            ScheduledEvent("HOSPITAL_TRANSFER_TEST", 0, 360, 60, 360),
            ScheduledEvent("FLOOD_ISOLATION_TEST", 1, 480, 60, 300),
        ),
    ),
    "SECOND_SOURCE": Schedule(
        "SECOND_SOURCE",
        420,
        None,
        (
            ScheduledEvent("WEST_MAIN_COMMISSIONING_TEST", 0, 420, 60, 420),
            ScheduledEvent("SOUTH_SOURCE_COMMISSIONING_TEST", 1, 540, 60, 360),
        ),
    ),
    "NORTH_BANK_PROMISE": Schedule(
        "NORTH_BANK_PROMISE",
        480,
        420,
        (
            ScheduledEvent("NORTH_BANK_COMMISSIONING", 0, 480, 90, 480),
            ScheduledEvent("NEXT_HOT_EVENING_FORECAST", 1, 690, 120, 480),
        ),
    ),
    "WHOSE_MARGIN": Schedule(
        "WHOSE_MARGIN",
        360,
        480,
        (
            ScheduledEvent("HOT_BASE", 0, 360, 90, 360),
            ScheduledEvent("NIGHT_SHIFT", 1, 510, 120, 390),
            ScheduledEvent("LATE_NIGHT", 2, 690, 90, 360),
        ),
    ),
    "BEFORE_WATER_RISE": Schedule(
        "BEFORE_WATER_RISE",
        300,
        240,
        (ScheduledEvent("FLOOD_ARRIVAL", 0, 300, 120, 300),),
    ),
    "SWITCH_OFF_TO_PROTECT": Schedule(
        "SWITCH_OFF_TO_PROTECT",
        420,
        None,
        (
            ScheduledEvent("WEST_SOURCE_PLANNED_OUTAGE", 0, 420, 120, 420),
            ScheduledEvent("WEST_SOURCE_RETURN_SERVICE", 1, 600, 120, 420),
        ),
    ),
    "LONGEST_NIGHT": Schedule(
        "LONGEST_NIGHT",
        600,
        None,
        (
            ScheduledEvent("MAX_DEMAND", 0, 600, 120, 600),
            ScheduledEvent("HEATWAVE_PEAK", 1, 780, 120, 600),
            ScheduledEvent("PROTECTIVE_STOP_FLOOD", 2, 960, 120, 600),
        ),
    ),
}

EXPECTED_PARTS = (
    ExpectedPart("FIRST_LIGHT/briefing", "briefing", "FIRST_LIGHT"),
    ExpectedPart("FIRST_LIGHT/result/standard", "result", "FIRST_LIGHT"),
    ExpectedPart("SECOND_HEART/briefing", "briefing", "SECOND_HEART"),
    ExpectedPart("SECOND_HEART/result/standard", "result", "SECOND_HEART"),
    ExpectedPart("SECOND_SOURCE/briefing", "briefing", "SECOND_SOURCE"),
    ExpectedPart("SECOND_SOURCE/result/standard", "result", "SECOND_SOURCE"),
    ExpectedPart("NORTH_BANK_PROMISE/briefing", "briefing", "NORTH_BANK_PROMISE"),
    ExpectedPart(
        "NORTH_BANK_PROMISE/window/NORTH_BANK_PLANNING_WINDOW",
        "window",
        "NORTH_BANK_PROMISE",
        window_id="NORTH_BANK_PLANNING_WINDOW",
    ),
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
        "WHOSE_MARGIN/window/HOT_EVENING_PLANNING_WINDOW",
        "window",
        "WHOSE_MARGIN",
        window_id="HOT_EVENING_PLANNING_WINDOW",
    ),
    ExpectedPart(
        "WHOSE_MARGIN/window/LATE_NIGHT_RECOVERY_WINDOW",
        "window",
        "WHOSE_MARGIN",
        window_id="LATE_NIGHT_RECOVERY_WINDOW",
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
    ExpectedPart("BEFORE_WATER_RISE/briefing", "briefing", "BEFORE_WATER_RISE"),
    ExpectedPart(
        "BEFORE_WATER_RISE/window/BEFORE_FLOOD_WINDOW",
        "window",
        "BEFORE_WATER_RISE",
        window_id="BEFORE_FLOOD_WINDOW",
    ),
    ExpectedPart(
        "BEFORE_WATER_RISE/result/keep",
        "result",
        "BEFORE_WATER_RISE",
        required_promise_branch="keep",
    ),
    ExpectedPart(
        "BEFORE_WATER_RISE/result/defer",
        "result",
        "BEFORE_WATER_RISE",
        required_promise_branch="defer",
    ),
    ExpectedPart(
        "SWITCH_OFF_TO_PROTECT/briefing",
        "briefing",
        "SWITCH_OFF_TO_PROTECT",
    ),
    ExpectedPart(
        "SWITCH_OFF_TO_PROTECT/window/BEFORE_PLANNED_OUTAGE_WINDOW",
        "window",
        "SWITCH_OFF_TO_PROTECT",
        window_id="BEFORE_PLANNED_OUTAGE_WINDOW",
    ),
    ExpectedPart(
        "SWITCH_OFF_TO_PROTECT/result/standard",
        "result",
        "SWITCH_OFF_TO_PROTECT",
    ),
    ExpectedPart("LONGEST_NIGHT/briefing", "briefing", "LONGEST_NIGHT"),
    ExpectedPart(
        "LONGEST_NIGHT/window/FINAL_OPERATING_PLAN_WINDOW",
        "window",
        "LONGEST_NIGHT",
        window_id="FINAL_OPERATING_PLAN_WINDOW",
    ),
    ExpectedPart("LONGEST_NIGHT/result/standard", "result", "LONGEST_NIGHT"),
    ExpectedPart("campaign/epilogue/card/city-report", "epilogue-card", None),
    ExpectedPart("campaign/epilogue/card/medical-witness", "epilogue-card", None),
    ExpectedPart("campaign/epilogue/card/closing", "epilogue-card", None),
    ExpectedPart(
        "campaign/epilogue/promise/NORTH_BANK_PROMISE/keep",
        "epilogue-promise-line",
        "NORTH_BANK_PROMISE",
        required_promise_branch="keep",
        content_type="promise-line",
    ),
    ExpectedPart(
        "campaign/epilogue/promise/NORTH_BANK_PROMISE/defer",
        "epilogue-promise-line",
        "NORTH_BANK_PROMISE",
        required_promise_branch="defer",
        content_type="promise-line",
    ),
    ExpectedPart(
        "campaign/epilogue/promise/WHOSE_MARGIN/keep",
        "epilogue-promise-line",
        "WHOSE_MARGIN",
        required_promise_branch="keep",
        content_type="promise-line",
    ),
    ExpectedPart(
        "campaign/epilogue/promise/WHOSE_MARGIN/defer",
        "epilogue-promise-line",
        "WHOSE_MARGIN",
        required_promise_branch="defer",
        content_type="promise-line",
    ),
    ExpectedPart(
        "campaign/epilogue/promise/BEFORE_WATER_RISE/keep",
        "epilogue-promise-line",
        "BEFORE_WATER_RISE",
        required_promise_branch="keep",
        content_type="promise-line",
    ),
    ExpectedPart(
        "campaign/epilogue/promise/BEFORE_WATER_RISE/defer",
        "epilogue-promise-line",
        "BEFORE_WATER_RISE",
        required_promise_branch="defer",
        content_type="promise-line",
    ),
)

EXPECTED_SELECTORS = tuple(part.selector for part in EXPECTED_PARTS)


def fail(message: str) -> None:
    raise ValueError(message)


def require_exact_keys(value: dict[str, Any], expected: set[str], label: str) -> None:
    actual = set(value)
    if actual != expected:
        fail(
            f"{label} keys mismatch: missing={sorted(expected - actual)}, "
            f"extra={sorted(actual - expected)}"
        )


def nonempty(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value.strip():
        fail(f"{label} must be a nonempty string")
    return value


def validate_story_card(value: Any, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        fail(f"{label} must be an object")
    require_exact_keys(value, {"contentType", "speaker", "title", "body"}, label)
    if value["contentType"] != "story-card":
        fail(f"{label}.contentType must be story-card")
    for field in ("speaker", "title", "body"):
        nonempty(value[field], f"{label}.{field}")
    return value


def validate_promise_line(value: Any, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        fail(f"{label} must be an object")
    require_exact_keys(value, {"contentType", "promiseId", "branch", "text"}, label)
    if value["contentType"] != "promise-line":
        fail(f"{label}.contentType must be promise-line")
    nonempty(value["promiseId"], f"{label}.promiseId")
    if value["branch"] not in {"keep", "defer"}:
        fail(f"{label}.branch must be keep or defer")
    nonempty(value["text"], f"{label}.text")
    return value


def validate_content(value: Any, expected_type: str, label: str) -> dict[str, Any]:
    if expected_type == "story-card":
        return validate_story_card(value, label)
    if expected_type == "promise-line":
        return validate_promise_line(value, label)
    fail(f"unsupported expected content type {expected_type}")


def expected_schedule(part: ExpectedPart) -> Schedule | None:
    if part.chapter_id is None:
        return None
    return SCHEDULES[part.chapter_id]


def validate_schedule(value: Any, expected: Schedule | None, label: str) -> None:
    if expected is None:
        if value is not None:
            fail(f"{label} must be null")
        return
    if not isinstance(value, dict):
        fail(f"{label} must be an object")
    require_exact_keys(
        value,
        {
            "chapterId",
            "preparationMinutes",
            "promiseDecisionDeadlineOffsetMinutes",
            "scheduledEvents",
        },
        label,
    )
    if value["chapterId"] != expected.chapter_id:
        fail(f"{label}.chapterId must be {expected.chapter_id}")
    if (
        type(value["preparationMinutes"]) is not int
        or value["preparationMinutes"] != expected.preparation_minutes
    ):
        fail(
            f"{label}.preparationMinutes must be "
            f"{expected.preparation_minutes}"
        )
    deadline = value["promiseDecisionDeadlineOffsetMinutes"]
    if expected.promise_deadline is None:
        if deadline is not None:
            fail(f"{label}.promiseDecisionDeadlineOffsetMinutes must be null")
    elif type(deadline) is not int or deadline != expected.promise_deadline:
        fail(
            f"{label}.promiseDecisionDeadlineOffsetMinutes must be "
            f"{expected.promise_deadline}"
        )
    events = value["scheduledEvents"]
    if not isinstance(events, list) or len(events) != len(expected.events):
        fail(f"{label}.scheduledEvents must contain {len(expected.events)} events")
    numeric_fields = (
        ("priority", "priority"),
        ("startOffsetMinutes", "start_offset_minutes"),
        ("durationMinutes", "duration_minutes"),
        ("forecastLeadMinutes", "forecast_lead_minutes"),
    )
    for index, (event, wanted) in enumerate(zip(events, expected.events)):
        event_label = f"{label}.scheduledEvents[{index}]"
        if not isinstance(event, dict):
            fail(f"{event_label} must be an object")
        require_exact_keys(
            event,
            {
                "eventId",
                "priority",
                "startOffsetMinutes",
                "durationMinutes",
                "forecastLeadMinutes",
            },
            event_label,
        )
        if event["eventId"] != wanted.event_id:
            fail(f"{event_label}.eventId must be {wanted.event_id}")
        for json_field, attribute in numeric_fields:
            expected_number = getattr(wanted, attribute)
            if type(event[json_field]) is not int or event[json_field] != expected_number:
                fail(f"{event_label}.{json_field} must be {expected_number}")


def validate_part(part: Any, expected: ExpectedPart, index: int) -> None:
    label = f"storyParts[{index}]"
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
            "authoredReachable",
            "requiredPromiseBranch",
            "realtimeSchedule",
            "content",
        },
        label,
    )
    expected_values = {
        "schemaVersion": PART_SCHEMA,
        "campaignId": CAMPAIGN_ID,
        "selector": expected.selector,
        "kind": expected.kind,
        "chapterId": expected.chapter_id,
        "windowId": expected.window_id,
        "authoredReachable": True,
        "requiredPromiseBranch": expected.required_promise_branch,
    }
    for field, wanted in expected_values.items():
        if part[field] != wanted or (
            field == "authoredReachable" and part[field] is not True
        ):
            fail(f"{label}.{field} mismatch: expected {wanted!r}, got {part[field]!r}")
    validate_schedule(
        part["realtimeSchedule"],
        expected_schedule(expected),
        f"{label}.realtimeSchedule",
    )
    content = validate_content(part["content"], expected.content_type, f"{label}.content")
    if expected.content_type == "promise-line":
        if content["branch"] != expected.required_promise_branch:
            fail(f"{label}.content.branch does not match selector branch")


def validate_manifest(manifest: Any) -> dict[str, Any]:
    if not isinstance(manifest, dict):
        fail("story manifest must be an object")
    require_exact_keys(
        manifest,
        {
            "schemaVersion",
            "campaignId",
            "baseCampaignSchemaVersion",
            "realtimeCampaignSchemaVersion",
            "count",
            "parts",
        },
        "story manifest",
    )
    expected_root = {
        "schemaVersion": MANIFEST_SCHEMA,
        "campaignId": CAMPAIGN_ID,
        "baseCampaignSchemaVersion": BASE_CAMPAIGN_SCHEMA,
        "realtimeCampaignSchemaVersion": REALTIME_CAMPAIGN_SCHEMA,
        "count": len(EXPECTED_PARTS),
    }
    for field, wanted in expected_root.items():
        if manifest[field] != wanted or (field == "count" and type(manifest[field]) is not int):
            fail(
                f"story manifest {field} mismatch: expected {wanted!r}, "
                f"got {manifest[field]!r}"
            )
    parts = manifest["parts"]
    if not isinstance(parts, list) or len(parts) != len(EXPECTED_PARTS):
        fail(f"story manifest parts must contain exactly {len(EXPECTED_PARTS)} rows")
    for index, (part, expected) in enumerate(zip(parts, EXPECTED_PARTS)):
        validate_part(part, expected, index)
    return manifest


def validate_context(context: Any) -> dict[str, Any]:
    if not isinstance(context, dict):
        fail("context must be an object")
    require_exact_keys(
        context,
        {
            "schemaVersion",
            "campaignId",
            "premise",
            "playerRole",
            "runtimeAuthority",
            "chapters",
        },
        "context",
    )
    if context["schemaVersion"] != CONTEXT_SCHEMA:
        fail(f"context schemaVersion must be {CONTEXT_SCHEMA}")
    if context["campaignId"] != CAMPAIGN_ID:
        fail(f"context campaignId must be {CAMPAIGN_ID}")
    nonempty(context["premise"], "context.premise")
    nonempty(context["playerRole"], "context.playerRole")
    authority = context["runtimeAuthority"]
    if not isinstance(authority, dict):
        fail("context.runtimeAuthority must be an object")
    require_exact_keys(
        authority,
        {
            "ruleMode",
            "baseCampaignSchemaVersion",
            "realtimeCampaignSchemaVersion",
            "defaultScene",
            "evaluationTargetScene",
            "nativePresentationCoverage",
            "fullCampaignNativeE2EStatus",
            "futureEventStatusBar",
            "implementedCheckpointIds",
        },
        "context.runtimeAuthority",
    )
    if authority["ruleMode"] != "CONTINUOUS_REALTIME":
        fail("context.runtimeAuthority.ruleMode must be CONTINUOUS_REALTIME")
    if authority["baseCampaignSchemaVersion"] != BASE_CAMPAIGN_SCHEMA:
        fail("context base campaign schema mismatch")
    if authority["realtimeCampaignSchemaVersion"] != REALTIME_CAMPAIGN_SCHEMA:
        fail("context realtime campaign schema mismatch")
    exact_runtime_authority = {
        "defaultScene": "CommercialMain",
        "evaluationTargetScene": "RealtimeSliceMain",
        "nativePresentationCoverage": "FIRST_LIGHT_TARGETED_R2_SLICE_ONLY",
        "fullCampaignNativeE2EStatus": "NOT_IMPLEMENTED",
    }
    for field, expected in exact_runtime_authority.items():
        if authority[field] != expected:
            fail(f"context.runtimeAuthority.{field} must be {expected}")
    status_bar = authority["futureEventStatusBar"]
    if not isinstance(status_bar, dict):
        fail("context.runtimeAuthority.futureEventStatusBar must be an object")
    require_exact_keys(
        status_bar,
        {
            "required",
            "presentation",
            "requiredSignals",
            "nativeCoverage",
        },
        "context.runtimeAuthority.futureEventStatusBar",
    )
    exact_status_bar = {
        "required": True,
        "presentation": "PERSISTENT_HORIZONTAL_EVENT_RAIL",
        "requiredSignals": [
            "CURRENT_TIME",
            "NEXT_EVENT_COUNTDOWN",
            "EVENT_START_END",
            "CONSTRUCTION_COMPLETION",
            "PROMISE_DECISION_DEADLINE",
            "THERMAL_TRIP_RECOVERY",
        ],
        "nativeCoverage": "FIRST_LIGHT_TARGETED_R2_SLICE_ONLY",
    }
    if status_bar != exact_status_bar:
        fail("context.runtimeAuthority.futureEventStatusBar contract mismatch")
    checkpoints = authority["implementedCheckpointIds"]
    if checkpoints != ["A1_NORMAL_READY", "A1_CONSTRUCTION_DUE_1M"]:
        fail("context implementedCheckpointIds must match the current two-checkpoint authority")

    chapters = context["chapters"]
    if not isinstance(chapters, list) or len(chapters) != len(EXPECTED_CHAPTER_IDS):
        fail("context.chapters must contain exactly eight ordered chapters")
    for index, (chapter, chapter_id) in enumerate(zip(chapters, EXPECTED_CHAPTER_IDS), start=1):
        label = f"context.chapters[{index - 1}]"
        if not isinstance(chapter, dict):
            fail(f"{label} must be an object")
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
                "preparationMinutes",
                "promiseDecisionDeadlineOffsetMinutes",
                "scheduledEvents",
                "nativePresentationStatus",
            },
            label,
        )
        if type(chapter["order"]) is not int or chapter["order"] != index:
            fail(f"{label}.order must be {index}")
        if chapter["chapterId"] != chapter_id:
            fail(f"{label}.chapterId must be {chapter_id}")
        for field in (
            "displayName",
            "learningIntent",
            "crisisIntent",
            "choiceIntent",
            "nativePresentationStatus",
        ):
            nonempty(chapter[field], f"{label}.{field}")
        expected_phase = "tutorial" if index <= 3 else "main"
        if chapter["phase"] != expected_phase:
            fail(f"{label}.phase must be {expected_phase}")
        schedule = SCHEDULES[chapter_id]
        validate_schedule(
            {
                "chapterId": chapter["chapterId"],
                "preparationMinutes": chapter["preparationMinutes"],
                "promiseDecisionDeadlineOffsetMinutes":
                    chapter["promiseDecisionDeadlineOffsetMinutes"],
                "scheduledEvents": chapter["scheduledEvents"],
            },
            schedule,
            f"{label}.realtimeSchedule",
        )
        expected_native_status = (
            "TARGETED_R2_SLICE" if chapter_id == "FIRST_LIGHT" else "CONTENT_AND_CORE_ONLY"
        )
        if chapter["nativePresentationStatus"] != expected_native_status:
            fail(f"{label}.nativePresentationStatus must be {expected_native_status}")
    return context


def validate_artifact(artifact: Any) -> dict[str, Any]:
    if not isinstance(artifact, dict):
        fail("text-plan artifact must be an object")
    require_exact_keys(
        artifact,
        {
            "schemaVersion",
            "campaignId",
            "premise",
            "playerRole",
            "runtimeAuthority",
            "chapters",
            "storyParts",
        },
        "text-plan artifact",
    )
    if artifact["schemaVersion"] != ARTIFACT_SCHEMA:
        fail(f"text-plan artifact schemaVersion must be {ARTIFACT_SCHEMA}")
    context = {
        "schemaVersion": CONTEXT_SCHEMA,
        "campaignId": artifact["campaignId"],
        "premise": artifact["premise"],
        "playerRole": artifact["playerRole"],
        "runtimeAuthority": artifact["runtimeAuthority"],
        "chapters": artifact["chapters"],
    }
    validate_context(context)
    parts = artifact["storyParts"]
    if not isinstance(parts, list) or len(parts) != len(EXPECTED_PARTS):
        fail(f"text-plan artifact storyParts must contain exactly {len(EXPECTED_PARTS)} rows")
    for index, (part, expected) in enumerate(zip(parts, EXPECTED_PARTS)):
        validate_part(part, expected, index)
    return artifact
