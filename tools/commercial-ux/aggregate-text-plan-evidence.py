#!/usr/bin/env python3
"""Validate a blind text-plan evidence verdict and emit its deterministic gate result."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any


TOOL_DIRECTORY = Path(__file__).resolve().parent
VERIFIER_PROMPT_PATH = TOOL_DIRECTORY / "text-plan-evidence-verifier-prompt.template.txt"
VERIFIER_SCHEMA_PATH = TOOL_DIRECTORY / "text-plan-evidence-verifier.schema.json"
TEXT_PLAN_ARTIFACT_SCHEMA = "gridworks.commercial-ux.text-plan-input.v1"
EVIDENCE_INPUT_SCHEMA = "gridworks.commercial-ux.text-plan-evidence-input.v1"
EVIDENCE_ENVELOPE_SCHEMA = "gridworks.commercial-ux.text-plan-evidence-envelope.v1"
EVIDENCE_INPUT_PROTOCOL = "GRIDWORKS-COMMERCIAL-UX-TEXT-PLAN-EVIDENCE-INPUT-v1"
VERIFICATION_PROTOCOL = "GRIDWORKS-COMMERCIAL-UX-TEXT-PLAN-EVIDENCE-VERIFICATION-v1"
AGGREGATE_PROTOCOL = "GRIDWORKS-COMMERCIAL-UX-TEXT-PLAN-EVIDENCE-AGGREGATE-v1"
RESULT_SCHEMA = "gridworks.commercial-ux.text-plan-evidence-result.v1"
SUCCESS_STATUS = "VERIFIED_SUPPORTED_ONLY"
BLOCKED_STATUS = "BLOCKED_EVIDENCE_VERIFICATION"
SHA256_PATTERN = re.compile(r"^sha256:[0-9a-f]{64}$")
OBSERVATION_ID_PATTERN = re.compile(r"^OBS-[0-9]{4}$")
VERDICTS = {"SUPPORTED", "PARTIAL", "UNSUPPORTED"}


class ValidationFailure(Exception):
    pass


@dataclass(frozen=True)
class EvidenceInput:
    text_plan_sha256: str
    verification_input_sha256: str
    prompt_template_sha256: str
    verifier_schema_sha256: str
    observations: tuple[dict[str, str], ...]


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def sha256_identifier(value: Any) -> str:
    return "sha256:" + hashlib.sha256(canonical_json_bytes(value)).hexdigest()


def file_sha256_identifier(path: Path, label: str) -> str:
    try:
        content = path.read_bytes()
    except OSError as exception:
        raise ValidationFailure(f"{label} is unreadable: {exception}") from exception
    return "sha256:" + hashlib.sha256(content).hexdigest()


def exact_keys(value: dict[str, Any], expected: set[str], label: str) -> None:
    actual = set(value)
    if actual != expected:
        raise ValidationFailure(
            f"{label} keys mismatch: missing={sorted(expected - actual)}, "
            f"extra={sorted(actual - expected)}"
        )


def nonempty_string(value: Any, label: str, maximum: int | None = None) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValidationFailure(f"{label} must be a nonempty string")
    if maximum is not None and len(value) > maximum:
        raise ValidationFailure(f"{label} must contain at most {maximum} characters")
    return value


def read_json(path: Path, label: str) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except OSError as exception:
        raise ValidationFailure(f"{label} is unreadable: {exception}") from exception
    except json.JSONDecodeError as exception:
        raise ValidationFailure(
            f"{label} is not valid JSON at line {exception.lineno} column {exception.colno}"
        ) from exception


def artifact_source_refs(artifact: Any) -> set[str]:
    if not isinstance(artifact, dict):
        raise ValidationFailure("verification input artifact must be an object")
    exact_keys(
        artifact,
        {
            "schemaVersion",
            "campaignId",
            "premise",
            "playerRole",
            "chapters",
            "storyParts",
        },
        "verification input artifact",
    )
    if artifact["schemaVersion"] != TEXT_PLAN_ARTIFACT_SCHEMA:
        raise ValidationFailure(
            f"verification input artifact schemaVersion must be {TEXT_PLAN_ARTIFACT_SCHEMA}"
        )
    nonempty_string(artifact["campaignId"], "verification input artifact campaignId")
    nonempty_string(artifact["premise"], "verification input artifact premise")
    nonempty_string(artifact["playerRole"], "verification input artifact playerRole")
    refs = {"context:premise", "context:playerRole"}

    chapters = artifact["chapters"]
    if not isinstance(chapters, list) or len(chapters) != 8:
        raise ValidationFailure("verification input artifact must contain exactly eight chapters")
    seen_chapters: set[str] = set()
    chapter_fields = {
        "order",
        "chapterId",
        "displayName",
        "phase",
        "learningIntent",
        "crisisIntent",
        "choiceIntent",
    }
    for index, chapter in enumerate(chapters, start=1):
        label = f"verification input artifact chapters[{index - 1}]"
        if not isinstance(chapter, dict):
            raise ValidationFailure(f"{label} must be an object")
        exact_keys(chapter, chapter_fields, label)
        if type(chapter["order"]) is not int or chapter["order"] != index:
            raise ValidationFailure("verification input artifact chapter order must be 1..8")
        chapter_id = nonempty_string(chapter["chapterId"], f"{label}.chapterId")
        if chapter_id in seen_chapters:
            raise ValidationFailure(f"duplicate chapterId {chapter_id!r}")
        seen_chapters.add(chapter_id)
        nonempty_string(chapter["displayName"], f"{label}.displayName")
        if chapter["phase"] not in {"tutorial", "main"}:
            raise ValidationFailure(f"{label}.phase must be tutorial or main")
        for field in ("learningIntent", "crisisIntent", "choiceIntent"):
            nonempty_string(chapter[field], f"{label}.{field}")
            refs.add(f"chapter:{chapter_id}:{field}")

    story_parts = artifact["storyParts"]
    if not isinstance(story_parts, list) or len(story_parts) != 26:
        raise ValidationFailure("verification input artifact must contain exactly 26 story parts")
    seen_selectors: set[str] = set()
    part_fields = {
        "schemaVersion",
        "campaignId",
        "selector",
        "kind",
        "chapterId",
        "windowId",
        "reachable",
        "requiredPromiseBranch",
        "story",
    }
    for index, part in enumerate(story_parts):
        label = f"verification input artifact storyParts[{index}]"
        if not isinstance(part, dict):
            raise ValidationFailure(f"{label} must be an object")
        exact_keys(part, part_fields, label)
        selector = nonempty_string(part["selector"], f"{label}.selector")
        if selector in seen_selectors:
            raise ValidationFailure(f"duplicate story selector {selector!r}")
        seen_selectors.add(selector)
        story = part["story"]
        if not isinstance(story, dict):
            raise ValidationFailure(f"{label}.story must be an object")
        exact_keys(story, {"speaker", "title", "body"}, f"{label}.story")
        for field in ("speaker", "title", "body"):
            nonempty_string(story[field], f"{label}.story.{field}")
        refs.add(selector)
    return refs


def load_evidence_input(path: Path) -> EvidenceInput:
    envelope = read_json(path, "verification input envelope")
    if not isinstance(envelope, dict):
        raise ValidationFailure("verification input envelope must be an object")
    exact_keys(
        envelope,
        {"schemaVersion", "verificationInputSha256", "verificationInput"},
        "verification input envelope",
    )
    if envelope["schemaVersion"] != EVIDENCE_ENVELOPE_SCHEMA:
        raise ValidationFailure(
            f"verification input envelope schemaVersion must be {EVIDENCE_ENVELOPE_SCHEMA}"
        )
    declared_input_sha = envelope["verificationInputSha256"]
    if not isinstance(declared_input_sha, str) or SHA256_PATTERN.fullmatch(declared_input_sha) is None:
        raise ValidationFailure(
            "verification input envelope verificationInputSha256 must be a lowercase sha256 identifier"
        )
    verification_input = envelope["verificationInput"]
    if not isinstance(verification_input, dict):
        raise ValidationFailure("verification input must be an object")
    exact_keys(
        verification_input,
        {
            "schemaVersion",
            "protocol",
            "textPlanSha256",
            "promptTemplateSha256",
            "verifierSchemaSha256",
            "artifact",
            "observations",
        },
        "verification input",
    )
    if verification_input["schemaVersion"] != EVIDENCE_INPUT_SCHEMA:
        raise ValidationFailure(
            f"verification input schemaVersion must be {EVIDENCE_INPUT_SCHEMA}"
        )
    if verification_input["protocol"] != EVIDENCE_INPUT_PROTOCOL:
        raise ValidationFailure(
            f"verification input protocol must be {EVIDENCE_INPUT_PROTOCOL}"
        )
    computed_input_sha = sha256_identifier(verification_input)
    if declared_input_sha != computed_input_sha:
        raise ValidationFailure(
            "verification input hash mismatch: "
            f"declared {declared_input_sha}, computed {computed_input_sha}"
        )
    text_plan_sha = verification_input["textPlanSha256"]
    if not isinstance(text_plan_sha, str) or SHA256_PATTERN.fullmatch(text_plan_sha) is None:
        raise ValidationFailure(
            "verification input textPlanSha256 must be a lowercase sha256 identifier"
        )
    prompt_template_sha = verification_input["promptTemplateSha256"]
    verifier_schema_sha = verification_input["verifierSchemaSha256"]
    expected_file_hashes = {
        "promptTemplateSha256": file_sha256_identifier(
            VERIFIER_PROMPT_PATH,
            "text-plan evidence verifier prompt template",
        ),
        "verifierSchemaSha256": file_sha256_identifier(
            VERIFIER_SCHEMA_PATH,
            "text-plan evidence verifier schema",
        ),
    }
    actual_file_hashes = {
        "promptTemplateSha256": prompt_template_sha,
        "verifierSchemaSha256": verifier_schema_sha,
    }
    for field, expected in expected_file_hashes.items():
        actual = actual_file_hashes[field]
        if not isinstance(actual, str) or SHA256_PATTERN.fullmatch(actual) is None:
            raise ValidationFailure(f"verification input {field} is not a sha256 identifier")
        if actual != expected:
            raise ValidationFailure(
                f"verification input {field} does not match the raw checked-in file: "
                f"expected {expected}, got {actual}"
            )
    artifact = verification_input["artifact"]
    refs = artifact_source_refs(artifact)
    computed_text_plan_sha = sha256_identifier(artifact)
    if text_plan_sha != computed_text_plan_sha:
        raise ValidationFailure(
            "verification input text-plan hash mismatch: "
            f"declared {text_plan_sha}, computed {computed_text_plan_sha}"
        )

    observations = verification_input["observations"]
    if not isinstance(observations, list) or not 1 <= len(observations) <= 480:
        raise ValidationFailure("verification input observations must contain 1..480 rows")
    validated: list[dict[str, str]] = []
    seen_pairs: set[tuple[str, str]] = set()
    for index, row in enumerate(observations, start=1):
        label = f"verification input observations[{index - 1}]"
        if not isinstance(row, dict):
            raise ValidationFailure(f"{label} must be an object")
        exact_keys(row, {"observationId", "sourceRef", "observation"}, label)
        expected_id = f"OBS-{index:04d}"
        if row["observationId"] != expected_id:
            raise ValidationFailure(f"{label}.observationId must be {expected_id}")
        source_ref = nonempty_string(row["sourceRef"], f"{label}.sourceRef", 240)
        observation = nonempty_string(row["observation"], f"{label}.observation", 1200)
        if source_ref not in refs:
            raise ValidationFailure(
                f"{label}.sourceRef does not exist in the embedded artifact: {source_ref!r}"
            )
        pair = (source_ref, observation)
        if pair in seen_pairs:
            raise ValidationFailure("verification input contains duplicate evidence observations")
        seen_pairs.add(pair)
        validated.append(
            {
                "observationId": expected_id,
                "sourceRef": source_ref,
                "observation": observation,
            }
        )
    return EvidenceInput(
        text_plan_sha256=text_plan_sha,
        verification_input_sha256=declared_input_sha,
        prompt_template_sha256=prompt_template_sha,
        verifier_schema_sha256=verifier_schema_sha,
        observations=tuple(validated),
    )


def blocker(code: str, detail: str, observation_id: str | None = None) -> dict[str, Any]:
    return {
        "code": code,
        "observationId": observation_id,
        "detail": detail,
    }


def empty_result(
    evidence_input: EvidenceInput | None,
    blockers: list[dict[str, Any]],
    verifier_run_id: str | None = None,
) -> dict[str, Any]:
    return {
        "schemaVersion": RESULT_SCHEMA,
        "protocol": AGGREGATE_PROTOCOL,
        "status": BLOCKED_STATUS,
        "textPlanSha256": evidence_input.text_plan_sha256 if evidence_input else None,
        "verificationInputSha256": (
            evidence_input.verification_input_sha256 if evidence_input else None
        ),
        "promptTemplateSha256": (
            evidence_input.prompt_template_sha256 if evidence_input else None
        ),
        "verifierSchemaSha256": (
            evidence_input.verifier_schema_sha256 if evidence_input else None
        ),
        "verifierRunId": verifier_run_id,
        "observationCount": len(evidence_input.observations) if evidence_input else None,
        "supportedObservationCount": 0,
        "partialObservationCount": 0,
        "unsupportedObservationCount": 0,
        "missingObservationCount": len(evidence_input.observations) if evidence_input else None,
        "invalidObservationCount": 1,
        "formativeConclusionsAllowed": False,
        "supportedObservationIds": [],
        "blockedObservationIds": (
            [row["observationId"] for row in evidence_input.observations]
            if evidence_input
            else []
        ),
        "blockers": blockers,
    }


def aggregate_verification(
    evidence_input: EvidenceInput,
    verification: Any,
) -> dict[str, Any]:
    if not isinstance(verification, dict):
        return empty_result(
            evidence_input,
            [blocker("INVALID_VERIFICATION_OUTPUT", "verification output must be an object")],
        )
    expected_top_keys = {
        "protocol",
        "verifierRunId",
        "verifierSlot",
        "model",
        "reasoningEffort",
        "promptTemplateSha256",
        "verifierSchemaSha256",
        "textPlanSha256",
        "verificationInputSha256",
        "observations",
    }
    if set(verification) != expected_top_keys:
        missing = sorted(expected_top_keys - set(verification))
        extra = sorted(set(verification) - expected_top_keys)
        return empty_result(
            evidence_input,
            [
                blocker(
                    "INVALID_VERIFICATION_OUTPUT",
                    f"top-level keys mismatch: missing={missing}, extra={extra}",
                )
            ],
            verification.get("verifierRunId")
            if isinstance(verification.get("verifierRunId"), str)
            else None,
        )

    verifier_run_id = verification["verifierRunId"]
    identity_errors: list[str] = []
    if not isinstance(verifier_run_id, str) or not verifier_run_id.strip() or len(verifier_run_id) > 200:
        identity_errors.append("verifierRunId must be a nonempty string of at most 200 characters")
        verifier_run_id = None
    expected_identity = {
        "protocol": VERIFICATION_PROTOCOL,
        "verifierSlot": "SOL-ULTRA",
        "model": "gpt-5.6-sol",
        "reasoningEffort": "ultra",
    }
    for field, expected in expected_identity.items():
        if verification[field] != expected:
            identity_errors.append(f"{field} must be {expected!r}")
    if identity_errors:
        return empty_result(
            evidence_input,
            [blocker("INVALID_VERIFIER_IDENTITY", "; ".join(identity_errors))],
            verifier_run_id,
        )
    hash_errors: list[str] = []
    if verification["textPlanSha256"] != evidence_input.text_plan_sha256:
        hash_errors.append("textPlanSha256 does not match the verification input")
    if verification["verificationInputSha256"] != evidence_input.verification_input_sha256:
        hash_errors.append("verificationInputSha256 does not match the verification input")
    if verification["promptTemplateSha256"] != evidence_input.prompt_template_sha256:
        hash_errors.append("promptTemplateSha256 does not match the verification input")
    if verification["verifierSchemaSha256"] != evidence_input.verifier_schema_sha256:
        hash_errors.append("verifierSchemaSha256 does not match the verification input")
    if hash_errors:
        return empty_result(
            evidence_input,
            [blocker("HASH_MISMATCH", "; ".join(hash_errors))],
            verifier_run_id,
        )

    rows = verification["observations"]
    if not isinstance(rows, list):
        return empty_result(
            evidence_input,
            [blocker("INVALID_VERIFICATION_OUTPUT", "observations must be an array")],
            verifier_run_id,
        )
    expected_by_id = {row["observationId"]: row for row in evidence_input.observations}
    valid_by_id: dict[str, dict[str, str]] = {}
    invalid_known_ids: set[str] = set()
    blockers: list[dict[str, Any]] = []
    invalid_count = 0
    for index, row in enumerate(rows):
        row_location = f"observations[{index}]"
        if not isinstance(row, dict):
            invalid_count += 1
            blockers.append(
                blocker("INVALID_OBSERVATION_ROW", f"{row_location} must be an object")
            )
            continue
        row_keys = {"observationId", "verdict", "sourceRef", "rationale"}
        if set(row) != row_keys:
            invalid_count += 1
            candidate_id = row.get("observationId")
            known_id = candidate_id if candidate_id in expected_by_id else None
            if known_id:
                invalid_known_ids.add(known_id)
                valid_by_id.pop(known_id, None)
            blockers.append(
                blocker(
                    "INVALID_OBSERVATION_ROW",
                    f"{row_location} keys mismatch",
                    known_id,
                )
            )
            continue
        observation_id = row["observationId"]
        if not isinstance(observation_id, str) or OBSERVATION_ID_PATTERN.fullmatch(observation_id) is None:
            invalid_count += 1
            blockers.append(
                blocker(
                    "INVALID_OBSERVATION_ROW",
                    f"{row_location}.observationId is invalid",
                )
            )
            continue
        if observation_id not in expected_by_id:
            invalid_count += 1
            blockers.append(
                blocker(
                    "UNKNOWN_OBSERVATION",
                    f"{row_location} is not present in the verification input",
                    observation_id,
                )
            )
            continue
        if observation_id in valid_by_id or observation_id in invalid_known_ids:
            invalid_count += 1
            invalid_known_ids.add(observation_id)
            valid_by_id.pop(observation_id, None)
            blockers.append(
                blocker(
                    "DUPLICATE_OBSERVATION",
                    f"{observation_id} appears more than once",
                    observation_id,
                )
            )
            continue
        if row["sourceRef"] != expected_by_id[observation_id]["sourceRef"]:
            invalid_count += 1
            invalid_known_ids.add(observation_id)
            blockers.append(
                blocker(
                    "SOURCE_REF_MISMATCH",
                    "sourceRef does not match the anonymized observation",
                    observation_id,
                )
            )
            continue
        if row["verdict"] not in VERDICTS:
            invalid_count += 1
            invalid_known_ids.add(observation_id)
            blockers.append(
                blocker(
                    "INVALID_OBSERVATION_ROW",
                    "verdict must be SUPPORTED, PARTIAL, or UNSUPPORTED",
                    observation_id,
                )
            )
            continue
        rationale = row["rationale"]
        if not isinstance(rationale, str) or not rationale.strip() or len(rationale) > 800:
            invalid_count += 1
            invalid_known_ids.add(observation_id)
            blockers.append(
                blocker(
                    "INVALID_OBSERVATION_ROW",
                    "rationale must be a nonempty string of at most 800 characters",
                    observation_id,
                )
            )
            continue
        valid_by_id[observation_id] = {
            "observationId": observation_id,
            "verdict": row["verdict"],
            "sourceRef": row["sourceRef"],
            "rationale": rationale,
        }

    expected_ids = [row["observationId"] for row in evidence_input.observations]
    missing_ids = [
        observation_id
        for observation_id in expected_ids
        if observation_id not in valid_by_id and observation_id not in invalid_known_ids
    ]
    for observation_id in missing_ids:
        blockers.append(
            blocker(
                "MISSING_OBSERVATION",
                "verifier output omitted the required observation",
                observation_id,
            )
        )

    supported_ids: list[str] = []
    partial_ids: list[str] = []
    unsupported_ids: list[str] = []
    for observation_id in expected_ids:
        row = valid_by_id.get(observation_id)
        if row is None:
            continue
        if row["verdict"] == "SUPPORTED":
            supported_ids.append(observation_id)
        elif row["verdict"] == "PARTIAL":
            partial_ids.append(observation_id)
            blockers.append(
                blocker(
                    "PARTIAL_OBSERVATION",
                    "the complete observation is not directly supported",
                    observation_id,
                )
            )
        else:
            unsupported_ids.append(observation_id)
            blockers.append(
                blocker(
                    "UNSUPPORTED_OBSERVATION",
                    "the observation is not supported by its cited source",
                    observation_id,
                )
            )

    blocked_ids = [
        observation_id
        for observation_id in expected_ids
        if observation_id not in supported_ids
    ]
    blockers.sort(
        key=lambda row: (
            row["observationId"] is None,
            row["observationId"] or "",
            row["code"],
            row["detail"],
        )
    )
    is_verified = not blockers and len(supported_ids) == len(expected_ids)
    return {
        "schemaVersion": RESULT_SCHEMA,
        "protocol": AGGREGATE_PROTOCOL,
        "status": SUCCESS_STATUS if is_verified else BLOCKED_STATUS,
        "textPlanSha256": evidence_input.text_plan_sha256,
        "verificationInputSha256": evidence_input.verification_input_sha256,
        "promptTemplateSha256": evidence_input.prompt_template_sha256,
        "verifierSchemaSha256": evidence_input.verifier_schema_sha256,
        "verifierRunId": verifier_run_id,
        "observationCount": len(expected_ids),
        "supportedObservationCount": len(supported_ids),
        "partialObservationCount": len(partial_ids),
        "unsupportedObservationCount": len(unsupported_ids),
        "missingObservationCount": len(missing_ids),
        "invalidObservationCount": invalid_count,
        "formativeConclusionsAllowed": is_verified,
        "supportedObservationIds": supported_ids,
        "blockedObservationIds": blocked_ids,
        "blockers": blockers,
    }


def run(input_path: Path, verification_path: Path) -> dict[str, Any]:
    try:
        evidence_input = load_evidence_input(input_path)
    except ValidationFailure as exception:
        return empty_result(
            None,
            [blocker("INVALID_VERIFICATION_INPUT", str(exception))],
        )
    try:
        verification = read_json(verification_path, "verification output")
    except ValidationFailure as exception:
        return empty_result(
            evidence_input,
            [blocker("INVALID_VERIFICATION_OUTPUT", str(exception))],
        )
    return aggregate_verification(evidence_input, verification)


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Validate and aggregate one blinded text-plan evidence verification."
    )
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--verification", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    result = run(args.input, args.verification)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(result, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    raise SystemExit(0 if result["status"] == SUCCESS_STATUS else 2)


if __name__ == "__main__":
    main()
