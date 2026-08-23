#!/usr/bin/env python3
"""Focused deterministic and mutation checks for the realtime text-plan port."""

from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
MANIFEST_COMMAND = [
    "dotnet",
    "run",
    "--project",
    str(ROOT / "tools/Gridworks.CommercialChecks/Gridworks.CommercialChecks.csproj"),
    "-c",
    "Release",
    "--",
    "--story-manifest",
]


def load_module(name: str, path: Path) -> Any:
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


sys.path.insert(0, str(TOOLS))
contract = load_module("realtime_text_contract", TOOLS / "realtime_text_contract.py")
builder = load_module("build_realtime_text_plan", TOOLS / "build-text-plan-input.py")
aggregate = load_module("aggregate_realtime_text_plan", TOOLS / "aggregate-text-plan.py")


def write_json(path: Path, value: Any) -> None:
    path.write_text(
        json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def run(command: list[str], *, expect_success: bool = True) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(
        command,
        cwd=ROOT,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if expect_success and result.returncode != 0:
        raise AssertionError(
            f"command failed ({result.returncode}): {' '.join(command)}\n"
            f"stdout:\n{result.stdout}\nstderr:\n{result.stderr}"
        )
    if not expect_success and result.returncode == 0:
        raise AssertionError(f"command unexpectedly passed: {' '.join(command)}")
    return result


def build_command(
    manifest: Path,
    campaign: Path,
    realtime: Path,
    context: Path,
    output: Path,
) -> list[str]:
    return [
        sys.executable,
        str(TOOLS / "build-text-plan-input.py"),
        "--story-manifest",
        str(manifest),
        "--campaign",
        str(campaign),
        "--realtime-campaign",
        str(realtime),
        "--context",
        str(context),
        "--output",
        str(output),
    ]


def rejected_build(
    directory: Path,
    label: str,
    *,
    manifest: dict[str, Any],
    campaign: dict[str, Any],
    realtime: dict[str, Any],
    context: dict[str, Any],
) -> subprocess.CompletedProcess[str]:
    paths = {
        "manifest": directory / f"{label}-manifest.json",
        "campaign": directory / f"{label}-campaign.json",
        "realtime": directory / f"{label}-realtime.json",
        "context": directory / f"{label}-context.json",
        "output": directory / f"{label}-output.json",
    }
    write_json(paths["manifest"], manifest)
    write_json(paths["campaign"], campaign)
    write_json(paths["realtime"], realtime)
    write_json(paths["context"], context)
    return run(
        build_command(
            paths["manifest"],
            paths["campaign"],
            paths["realtime"],
            paths["context"],
            paths["output"],
        ),
        expect_success=False,
    )


def main() -> None:
    with tempfile.TemporaryDirectory(prefix="gridworks-realtime-text-") as raw_directory:
        directory = Path(raw_directory)
        manifest_path = directory / "story-manifest.json"
        manifest_result = run(MANIFEST_COMMAND)
        manifest_path.write_text(manifest_result.stdout, encoding="utf-8")
        manifest = json.loads(manifest_result.stdout)
        contract.validate_manifest(manifest)
        assert manifest["count"] == 34
        assert [part["selector"] for part in manifest["parts"]] == list(
            contract.EXPECTED_SELECTORS
        )
        assert sum(
            part["content"]["contentType"] == "promise-line"
            for part in manifest["parts"]
        ) == 6

        campaign_path = ROOT / "data/release-campaign-v2.json"
        realtime_path = ROOT / "data/release-campaign-v3.json"
        context_path = TOOLS / "text-plan-context.json"
        first_output = directory / "text-plan-1.json"
        second_output = directory / "text-plan-2.json"
        first = run(build_command(
            manifest_path,
            campaign_path,
            realtime_path,
            context_path,
            first_output,
        ))
        second = run(build_command(
            manifest_path,
            campaign_path,
            realtime_path,
            context_path,
            second_output,
        ))
        assert first.stdout == second.stdout
        assert first_output.read_bytes() == second_output.read_bytes()

        envelope = json.loads(first_output.read_text(encoding="utf-8"))
        assert envelope["schemaVersion"] == contract.ENVELOPE_SCHEMA
        contract.validate_artifact(envelope["artifact"])
        expected_artifact_hash = "sha256:" + hashlib.sha256(
            builder.canonical_json_bytes(envelope["artifact"])
        ).hexdigest()
        assert envelope["artifactSha256"] == expected_artifact_hash
        expected_sources = {
            "baseCampaign": campaign_path,
            "context": context_path,
            "realtimeCampaign": realtime_path,
            "storyManifest": manifest_path,
        }
        for name, path in expected_sources.items():
            assert envelope["sourceBindings"][name] == (
                "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()
            )
        expected_text_plan_hash = "sha256:" + hashlib.sha256(
            builder.canonical_json_bytes({
                "artifactSha256": envelope["artifactSha256"],
                "sourceBindings": envelope["sourceBindings"],
                "artifact": envelope["artifact"],
            })
        ).hexdigest()
        assert envelope["textPlanSha256"] == expected_text_plan_hash

        text_plan_hash, allowed_refs = aggregate.load_text_plan(
            first_output,
            expected_sources,
        )
        assert text_plan_hash == envelope["textPlanSha256"]
        assert set(contract.EXPECTED_SELECTORS).issubset(allowed_refs)
        assert "context:runtimeAuthority" in allowed_refs
        assert "chapter:FIRST_LIGHT:realtimeSchedule" in allowed_refs
        _, categories, _, _ = aggregate.load_text_rubric(TOOLS / "rubric.json")
        prompt_sha, schema_sha = aggregate.load_judge_contract()
        judgment_paths: list[Path] = []
        for run_index in range(3):
            judgment = {
                "protocol": aggregate.JUDGMENT_PROTOCOL,
                "judgeRunId": f"realtime-port-{run_index + 1}",
                "judgeSlot": "SOL-ULTRA",
                "model": "gpt-5.6-sol",
                "reasoningEffort": "ultra",
                "textPlanSha256": text_plan_hash,
                "promptTemplateSha256": prompt_sha,
                "judgmentSchemaSha256": schema_sha,
                "cells": [
                    {
                        "cellId": cell["id"],
                        "label": "STRONG",
                        "confidence": "HIGH",
                        "strengthEvidence": [{
                            "sourceRef": "context:runtimeAuthority",
                            "observation": "실시간 규칙과 현재 native coverage 경계가 명시돼 있다.",
                        }],
                        "gapEvidence": [],
                    }
                    for category in categories
                    for cell in category["cells"]
                ],
            }
            judgment_path = directory / f"judgment-{run_index + 1}.json"
            write_json(judgment_path, judgment)
            judgment_paths.append(judgment_path)
        aggregate_output = directory / "aggregate.json"
        run([
            sys.executable,
            str(TOOLS / "aggregate-text-plan.py"),
            *(str(path) for path in judgment_paths),
            "--text-plan",
            str(first_output),
            "--story-manifest",
            str(manifest_path),
            "--campaign",
            str(campaign_path),
            "--realtime-campaign",
            str(realtime_path),
            "--context",
            str(context_path),
            "--rubric",
            str(TOOLS / "rubric.json"),
            "--output",
            str(aggregate_output),
        ])
        aggregate_result = json.loads(aggregate_output.read_text(encoding="utf-8"))
        assert aggregate_result["protocol"] == aggregate.AGGREGATE_PROTOCOL
        assert aggregate_result["status"] == "SCORED_FORMATIVE"
        assert aggregate_result["textPlanProxy"] == 85.0
        assert aggregate_result["commercialUXProxy"] is None
        assert aggregate_result["officialCommercialUX"] is False

        replacement_rejected = run([
            sys.executable,
            str(TOOLS / "aggregate-text-plan.py"),
            *(str(path) for path in judgment_paths),
            "--text-plan",
            str(first_output),
            "--story-manifest",
            str(manifest_path),
            "--campaign",
            str(campaign_path),
            "--realtime-campaign",
            str(realtime_path),
            "--context",
            str(context_path),
            "--rubric",
            str(TOOLS / "rubric.json"),
            "--replacement-for",
            str(aggregate_output),
            "--output",
            str(directory / "replacement-disabled.json"),
        ], expect_success=False)
        assert "replacement panels are disabled" in replacement_rejected.stderr

        base_manifest = copy.deepcopy(manifest)
        base_campaign = json.loads(campaign_path.read_text(encoding="utf-8"))
        base_realtime = json.loads(realtime_path.read_text(encoding="utf-8"))
        base_context = json.loads(context_path.read_text(encoding="utf-8"))

        bad_count = copy.deepcopy(base_manifest)
        bad_count["count"] = 33
        rejected = rejected_build(
            directory,
            "bad-count",
            manifest=bad_count,
            campaign=base_campaign,
            realtime=base_realtime,
            context=base_context,
        )
        assert "count mismatch" in rejected.stderr

        bad_order = copy.deepcopy(base_manifest)
        bad_order["parts"][0], bad_order["parts"][1] = (
            bad_order["parts"][1],
            bad_order["parts"][0],
        )
        rejected = rejected_build(
            directory,
            "bad-order",
            manifest=bad_order,
            campaign=base_campaign,
            realtime=base_realtime,
            context=base_context,
        )
        assert ".selector mismatch" in rejected.stderr

        bad_schedule = copy.deepcopy(base_manifest)
        bad_schedule["parts"][0]["realtimeSchedule"]["scheduledEvents"][0][
            "startOffsetMinutes"
        ] = 999999
        rejected = rejected_build(
            directory,
            "bad-manifest-schedule",
            manifest=bad_schedule,
            campaign=base_campaign,
            realtime=base_realtime,
            context=base_context,
        )
        assert ".startOffsetMinutes must be 240" in rejected.stderr

        bad_reachable = copy.deepcopy(base_manifest)
        bad_reachable["parts"][0]["authoredReachable"] = False
        rejected = rejected_build(
            directory,
            "bad-authored-reachable",
            manifest=bad_reachable,
            campaign=base_campaign,
            realtime=base_realtime,
            context=base_context,
        )
        assert ".authoredReachable mismatch" in rejected.stderr

        bad_content = copy.deepcopy(base_manifest)
        bad_content["parts"][0]["content"]["body"] += " poison"
        rejected = rejected_build(
            directory,
            "bad-content",
            manifest=bad_content,
            campaign=base_campaign,
            realtime=base_realtime,
            context=base_context,
        )
        assert "content does not match campaign authority" in rejected.stderr

        bad_realtime = copy.deepcopy(base_realtime)
        bad_realtime["chapters"][0]["scheduledEvents"][0]["eventId"] = "POISON"
        rejected = rejected_build(
            directory,
            "bad-realtime",
            manifest=base_manifest,
            campaign=base_campaign,
            realtime=bad_realtime,
            context=base_context,
        )
        assert ".eventId must be FIRST_LIGHT_SUPPLY" in rejected.stderr

        for field, poisoned, expected in (
            ("priority", 7, 0),
            ("startOffsetMinutes", 999999, 240),
            ("durationMinutes", -5, 60),
            ("forecastLeadMinutes", -7, 240),
        ):
            bad_event_timing = copy.deepcopy(base_realtime)
            bad_event_timing["chapters"][0]["scheduledEvents"][0][field] = poisoned
            rejected = rejected_build(
                directory,
                f"bad-realtime-{field}",
                manifest=base_manifest,
                campaign=base_campaign,
                realtime=bad_event_timing,
                context=base_context,
            )
            assert f".{field} must be {expected}" in rejected.stderr

        optimistic_context = copy.deepcopy(base_context)
        optimistic_context["runtimeAuthority"]["fullCampaignNativeE2EStatus"] = "PASS"
        rejected = rejected_build(
            directory,
            "optimistic-context",
            manifest=base_manifest,
            campaign=base_campaign,
            realtime=base_realtime,
            context=optimistic_context,
        )
        assert "fullCampaignNativeE2EStatus must be NOT_IMPLEMENTED" in rejected.stderr

        bad_status_bar = copy.deepcopy(base_context)
        bad_status_bar["runtimeAuthority"]["futureEventStatusBar"][
            "requiredSignals"
        ].remove("NEXT_EVENT_COUNTDOWN")
        rejected = rejected_build(
            directory,
            "missing-future-event-countdown",
            manifest=base_manifest,
            campaign=base_campaign,
            realtime=base_realtime,
            context=bad_status_bar,
        )
        assert "futureEventStatusBar contract mismatch" in rejected.stderr

        tampered_envelope = copy.deepcopy(envelope)
        tampered_envelope["artifact"]["premise"] += " poison"
        tampered_path = directory / "tampered-envelope.json"
        write_json(tampered_path, tampered_envelope)
        try:
            aggregate.load_text_plan(tampered_path, expected_sources)
        except SystemExit as exception:
            assert "artifact hash mismatch" in str(exception)
        else:
            raise AssertionError("tampered artifact unexpectedly passed aggregate validation")

        tampered_binding = copy.deepcopy(envelope)
        tampered_binding["sourceBindings"]["realtimeCampaign"] = "sha256:" + ("0" * 64)
        tampered_binding["textPlanSha256"] = "sha256:" + hashlib.sha256(
            builder.canonical_json_bytes({
                "artifactSha256": tampered_binding["artifactSha256"],
                "sourceBindings": tampered_binding["sourceBindings"],
                "artifact": tampered_binding["artifact"],
            })
        ).hexdigest()
        tampered_binding_path = directory / "tampered-source-binding.json"
        write_json(tampered_binding_path, tampered_binding)
        try:
            aggregate.load_text_plan(tampered_binding_path, expected_sources)
        except SystemExit as exception:
            assert "source binding realtimeCampaign mismatch" in str(exception)
        else:
            raise AssertionError("tampered source binding unexpectedly passed validation")

        forged_artifact = copy.deepcopy(envelope)
        forged_artifact["artifact"]["premise"] += " poison"
        forged_artifact["artifactSha256"] = "sha256:" + hashlib.sha256(
            builder.canonical_json_bytes(forged_artifact["artifact"])
        ).hexdigest()
        forged_artifact["textPlanSha256"] = "sha256:" + hashlib.sha256(
            builder.canonical_json_bytes({
                "artifactSha256": forged_artifact["artifactSha256"],
                "sourceBindings": forged_artifact["sourceBindings"],
                "artifact": forged_artifact["artifact"],
            })
        ).hexdigest()
        forged_artifact_path = directory / "forged-derived-artifact.json"
        write_json(forged_artifact_path, forged_artifact)
        try:
            aggregate.load_text_plan(forged_artifact_path, expected_sources)
        except SystemExit as exception:
            assert "deterministic source-authority rebuild" in str(exception)
        else:
            raise AssertionError("forged derived artifact unexpectedly passed validation")

    print("Realtime commercial UX text-plan tools: PASS (34 parts, 16 mutations)")


if __name__ == "__main__":
    main()
