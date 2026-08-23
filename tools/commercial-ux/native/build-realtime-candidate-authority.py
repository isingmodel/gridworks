#!/usr/bin/env python3
"""Build the exact non-default Debug R2 candidate authority from Git blobs.

This producer is deliberately narrower than a sellable application packager.
The current repository can execute only the FIRST_LIGHT R2 slice from an
explicit Debug/editor scene.  ExportRelease and the default scene are V2.  The
manifest therefore binds the available bytes and typed routes while keeping
full-flow and score-bearing capture fail-closed.
"""

from __future__ import annotations

import argparse
import contextlib
import dataclasses
import hashlib
import html
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import tempfile
from typing import Any, Iterator, Sequence


SHA256_PREFIX = "sha256:"
CANONICALIZATION = "GRIDWORKS_CANONICAL_JSON_V1"
MANIFEST_SCHEMA = "gridworks.realtime-evaluator-candidate-manifest.v1"
EVALUATOR_PRODUCER_SCHEMA = (
    "gridworks.realtime-evaluator-producer-authority.v1"
)
CANDIDATE_KIND = "EDITOR_NATIVE_NONDEFAULT_DEBUG_FIRST_LIGHT"
CONFIGURATION = "Debug"
DOTNET_VERSION = "8.0.129"
GODOT_VERSION = "4.7.1"
GODOT_VERSION_OUTPUT = "4.7.1.stable.mono.official.a13da4feb"
GODOT_BANNER = (
    f"Godot Engine v{GODOT_VERSION_OUTPUT} - https://godotengine.org"
)
GIT_EXECUTABLE_PATH = Path("/usr/bin/git")
GIT_EXECUTABLE_RAW_SHA256 = (
    "sha256:44a68ddc1983d6cff3fd35ba3f9ba5f82004216f1dcde69892b3d1b06e408698"
)
GIT_EXECUTABLE_BYTE_LENGTH = 118640
GIT_VERSION_OUTPUT = "git version 2.50.1 (Apple Git-155)"
GIT_COMMAND_BINDING_SCOPE = "EXACT_USR_BIN_GIT_BYTES_AND_VERSION_OUTPUT"
GIT_REPOSITORY_LOCATION_POLICY = "EXPLICIT_RESOLVED_GIT_DIR_AND_WORK_TREE"
GIT_ENVIRONMENT_POLICY = "FRESH_ALLOWLIST_DROPS_AMBIENT_GIT_ENV"
GIT_REPLACEMENT_OBJECT_POLICY = "DISABLED_BY_CLI_AND_ENV"

SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_REPOSITORY_ROOT = SCRIPT_DIR.parents[2]
POLICY_PATH = SCRIPT_DIR / "realtime-candidate-policy.json"
EXPECTED_POLICY_RAW_SHA256 = (
    "sha256:3a648f8dd1832834af47defb4e6d8ad73e75b02527510ae967aebd4d70846818"
)

POLICY_TOP_LEVEL_KEYS = frozenset({
    "schemaVersion",
    "canonicalization",
    "candidate",
    "evaluatorProducerAuthority",
    "sourceAuthority",
    "managedBuild",
    "engineAuthority",
    "packageAuthority",
    "headlessExecutionAuthority",
    "storyAuthority",
    "sceneAuthority",
    "routeProfiles",
    "futureEventStatusBar",
    "limitations",
})

POLICY_OBJECT_KEYS: dict[str, frozenset[str]] = {
    "canonicalization": frozenset({
        "name", "rawFileHash", "fileRowOrder", "fileRowChainHash",
        "policyHashRule", "candidateHashRule",
    }),
    "candidate": frozenset({
        "candidateKind", "configuration", "officialCommercialUX",
        "scoreBearingCaptureAllowed", "sourceMaterialization",
        "candidatePackageStatus",
    }),
    "evaluatorProducerAuthority": frozenset({
        "schemaVersion", "expectedFileCount", "paths",
        "sourceMaterialization", "semanticVerifierEntryPoint",
        "semanticVerifierReexecutesHeadlessProbes", "structuralSchemaAuthority",
        "gitCommandAuthority",
    }),
    "sourceAuthority": frozenset({
        "expectedFileCount", "expectedSourceInputsSha256", "expectedRoleCounts",
        "debugCompileCounts", "debugEmbeddedResourceCount",
        "explicitCompileExclusions", "reservedSourceDirectoryNames",
        "pinnedAuthorityFiles", "r2GodotExecutableClosure",
        "excludedFromR2GodotExecutableClosure",
    }),
    "managedBuild": frozenset({
        "configuration", "dotnetAuthority", "packageInputs",
        "requiredManagedOutputs", "managedOutputRole", "godotScriptPathAuthority",
        "isolationRequirements",
    }),
    "engineAuthority": frozenset({
        "version", "appFileCount", "appFileTreeSha256", "executable",
        "pathPolicy", "versionProbeRequired",
    }),
    "packageAuthority": frozenset({
        "packageKind", "rootName", "sourceClosureFileCount",
        "managedOutputFileCount", "fileCount", "sourceFileRole",
        "managedOutputRole", "treeHashRule", "materializedFromExactBindings",
        "nativeAppBundle", "publicPackage",
    }),
    "headlessExecutionAuthority": frozenset({
        "schemaVersion", "executionKind", "positiveCheckpointIds",
        "argumentRejectionProbeIds", "freshProcessPerProbe",
        "freshExactPackageCopyPerProbe",
        "ephemeralProbeFilesystemOutputsExcludedFromPackageFileAuthority",
        "expectedEphemeralEmptyDirectorySideEffects",
        "hostRuntimeBinding", "inputFileCountPerProbe",
        "boundFileByteMutationCount", "nativePresentationObserved",
        "scoreBearingEvidence",
    }),
    "storyAuthority": frozenset({
        "expectedInputsSha256", "authoredCampaignPath",
        "declaredRealtimeCampaignPath", "harnessPaths", "storedManifestPath",
        "storedManifestRawSha256", "partCount", "partCounts",
        "authoredReachabilityOnly", "nativeReachabilityClaim",
        "deterministicRebuildMustMatchStoredBytes",
    }),
    "sceneAuthority": frozenset({
        "defaultScene", "interactiveCandidateScene", "checkpointRunnerScene",
        "runtimeFixture", "declaredNonruntimeFullV3", "checkpoints",
    }),
    "routeProfiles": frozenset({
        "interactiveFirstLight", "targetedCheckpoints", "fullFlow",
    }),
    "futureEventStatusBar": frozenset({
        "implementationPath", "scenePath", "implementationPresent", "signals",
        "headlessWiringStatus", "nativeQualityObserved", "scoreClaimAllowed",
    }),
    "limitations": frozenset({
        "defaultSceneCandidateMismatch", "runtimeCoverage",
        "fullCampaignNativeE2E", "fullFlowRoute", "saveResume",
        "finaleEpilogueNative", "fullV3RuntimeBinding",
        "futureEventStatusBarNativeQuality", "runtimeArtAuthority",
        "dotnetToolchainAuthority", "packageStatus", "nativeCaptureStatus",
        "claimsNotAuthorized",
    }),
}

GAME_PROJECT_PATH = "game/Gridworks.Game.csproj"
CORE_PROJECT_PATH = "src/Gridworks.Core/Gridworks.Core.csproj"
PROJECT_GODOT_PATH = "game/project.godot"
EXPORT_PRESETS_PATH = "game/export_presets.cfg"
GLOBAL_JSON_PATH = "global.json"
DIRECTORY_PROPS_PATH = "Directory.Build.props"
STORY_PROGRAM_PATH = "tools/Gridworks.CommercialChecks/Program.cs"
STORY_HARNESS_PATH = "tools/Gridworks.CommercialChecks/CommercialStoryPartHarness.cs"
STORED_STORY_MANIFEST_PATH = (
    "playtests/commercial-ux-87-realtime/text-plan-r0/story-manifest.json"
)
EVALUATOR_PRODUCER_PATH_ROLES = (
    (
        "tools/commercial-ux/native/build-realtime-candidate-authority.py",
        "EVALUATOR_PRODUCER_AND_SEMANTIC_VERIFIER",
    ),
    (
        "tools/commercial-ux/native/realtime-candidate-manifest.schema.json",
        "STRUCTURAL_SCHEMA_NON_AUTHORITY",
    ),
    (
        "tools/commercial-ux/native/realtime-candidate-policy.json",
        "EVALUATOR_POLICY",
    ),
    (
        "tools/commercial-ux/native/test-realtime-candidate-authority.py",
        "ADVERSARIAL_TEST_SPEC_NON_RUNTIME",
    ),
)

DEFAULT_SCENE = "res://CommercialMain.tscn"
INTERACTIVE_SCENE = "res://realtime/r2/RealtimeSliceMain.tscn"
CHECKPOINT_SCENE = "res://realtime/r2/RealtimeSliceCheckpointRunner.tscn"

BASE_WORLD_PATH = "data/release-world-v2.json"
BASE_CAMPAIGN_PATH = "data/release-campaign-v2.json"
FULL_REALTIME_WORLD_PATH = "data/release-world-v3.json"
FULL_REALTIME_CAMPAIGN_PATH = "data/release-campaign-v3.json"
FIXTURE_WORLD_PATH = (
    "tools/Gridworks.RealtimeChecks/Fixtures/stage-r1-world-realtime-v3.json"
)
FIXTURE_CAMPAIGN_PATH = (
    "tools/Gridworks.RealtimeChecks/Fixtures/stage-r1-first-light-realtime-v3.json"
)

EXPECTED_DECLARATION_HASHES = {
    GLOBAL_JSON_PATH: "sha256:9e5dddc69006a787c251be614e6db6cd4e93a104128d3e03b66a7c914acdaf55",
    DIRECTORY_PROPS_PATH: "sha256:bd3eef539e4d05bf5b67fe4a069087c210172dc668b74469ad2bbaaf9ff7d5f8",
    GAME_PROJECT_PATH: "sha256:e5bd58d77ae99f45c9d9cbe3c564695ffaf5a55a1b3f91f26510088d6dd4c229",
    CORE_PROJECT_PATH: "sha256:77cfaf78fabef64c349c54823e786811827c56aa3205019ef820049c919ee75a",
    PROJECT_GODOT_PATH: "sha256:3bb6362b2c8f7da94d6eb3df69ccae4752fa32dbf0eaa882950cde8348420586",
    EXPORT_PRESETS_PATH: "sha256:57239c5ac9cef9e21e86b8adb6e14c5ae22338908fefbe65b5151408d75d6a08",
}

EMBEDDED_RESOURCES = (
    (
        "data/product-campaign-v1.json",
        "EmbeddedData/product-campaign-v1.json",
        "Gridworks.Game.EmbeddedData.product-campaign-v1.json",
    ),
    (
        "data/product-heatwave-v1.json",
        "EmbeddedData/product-heatwave-v1.json",
        "Gridworks.Game.EmbeddedData.product-heatwave-v1.json",
    ),
    (
        FIXTURE_WORLD_PATH,
        "EmbeddedData/stage-r1-world-realtime-v3.json",
        "Gridworks.Game.EmbeddedData.stage-r1-world-realtime-v3.json",
    ),
    (
        FIXTURE_CAMPAIGN_PATH,
        "EmbeddedData/stage-r1-first-light-realtime-v3.json",
        "Gridworks.Game.EmbeddedData.stage-r1-first-light-realtime-v3.json",
    ),
    (
        "data/commercial-free-placement-slice-v1.json",
        "EmbeddedData/commercial-free-placement-slice-v1.json",
        "Gridworks.Game.EmbeddedData.commercial-free-placement-slice-v1.json",
    ),
    (
        BASE_WORLD_PATH,
        "EmbeddedData/release-world-v2.json",
        "Gridworks.Game.EmbeddedData.release-world-v2.json",
    ),
    (
        BASE_CAMPAIGN_PATH,
        "EmbeddedData/release-campaign-v2.json",
        "Gridworks.Game.EmbeddedData.release-campaign-v2.json",
    ),
    (
        "data/release-world-v1.json",
        "EmbeddedData/release-world-v1.json",
        "Gridworks.Game.EmbeddedData.release-world-v1.json",
    ),
    (
        "data/release-campaign-v1.json",
        "EmbeddedData/release-campaign-v1.json",
        "Gridworks.Game.EmbeddedData.release-campaign-v1.json",
    ),
)

RUNTIME_SCENE_FILES = (
    "game/default_bus_layout.tres",
    "game/RealtimeTheme.tres",
    "game/realtime/r2/RealtimeSliceCheckpointRunner.tscn",
    "game/realtime/r2/RealtimeSliceMain.tscn",
    "game/realtime/ui/RealtimeActionDock.tscn",
    "game/realtime/ui/RealtimeBuildShelf.tscn",
    "game/realtime/ui/RealtimeContextDock.tscn",
    "game/realtime/ui/RealtimeEventRail.tscn",
    "game/realtime/ui/RealtimeModalHost.tscn",
    "game/realtime/ui/RealtimeTopHud.tscn",
    "game/realtime/ui/RealtimeUiRoot.tscn",
)

SCENE_ATTACHED_SCRIPTS = (
    "game/realtime/r2/RealtimePlaceholderMap.cs",
    "game/realtime/r2/RealtimeSliceCheckpointRunner.cs",
    "game/realtime/r2/RealtimeSliceMain.cs",
    "game/realtime/ui/RealtimeActionDock.cs",
    "game/realtime/ui/RealtimeBuildShelf.cs",
    "game/realtime/ui/RealtimeContextDock.cs",
    "game/realtime/ui/RealtimeEventRail.cs",
    "game/realtime/ui/RealtimeFocusScope.cs",
    "game/realtime/ui/RealtimeInputRouter.cs",
    "game/realtime/ui/RealtimeModalHost.cs",
    "game/realtime/ui/RealtimeTopHud.cs",
    "game/realtime/ui/RealtimeUiRoot.cs",
)

EXPECTED_GODOT_PACKAGES: dict[str, tuple[str, int]] = {
    "godot.net.sdk.4.7.1.nupkg": (
        "sha256:1f93837c9b8df052596203a0882818381cb5d64cd7f86f9a46cb67184d8287ff",
        19_973,
    ),
    "godot.sourcegenerators.4.7.1.nupkg": (
        "sha256:6b3e98ab8e94bad4d2f65de559bdcf0637fd6ca084cdf0ac1a6d8a17542bb4f1",
        58_764,
    ),
    "godotsharp.4.7.1.nupkg": (
        "sha256:f0b366029c9859355cacc25ccc2e4f19bd2dee7e16d5c22b82d7c736ff208068",
        3_234_854,
    ),
    "godotsharpeditor.4.7.1.nupkg": (
        "sha256:8ced4bfd55968cf4f835035b7e8d8149ff535e4bf200496491f8e8d93a91b682",
        190_855,
    ),
}

PACKAGE_AUTHORITY_PATHS = {
    "godot.net.sdk.4.7.1.nupkg": (
        "godot.net.sdk/4.7.1/godot.net.sdk.4.7.1.nupkg"
    ),
    "godot.sourcegenerators.4.7.1.nupkg": (
        "godot.sourcegenerators/4.7.1/godot.sourcegenerators.4.7.1.nupkg"
    ),
    "godotsharp.4.7.1.nupkg": (
        "godotsharp/4.7.1/godotsharp.4.7.1.nupkg"
    ),
    "godotsharpeditor.4.7.1.nupkg": (
        "godotsharpeditor/4.7.1/godotsharpeditor.4.7.1.nupkg"
    ),
}

FUTURE_EVENT_SIGNALS = (
    "CURRENT_TIME",
    "NEXT_EVENT_COUNTDOWN",
    "EVENT_START_END",
    "CONSTRUCTION_COMPLETION",
    "PROMISE_DECISION_DEADLINE",
    "THERMAL_TRIP_RECOVERY",
)

CHECKPOINTS = (
    {
        "checkpointId": "A1_NORMAL_READY",
        "startMinute": 1020,
        "startCanonicalStateSha256": "sha256:7094f631c89fe072800858a205d08358be07a6e0e7341b83026ff619fc03f9a3",
        "commandReplaySchema": "gridworks.targeted-live-command-replay.v1",
        "commandReplaySha256": "sha256:4f4d3748681585f49eeb4291262db3c99676baba10913450c94d5e1eda9e1611",
        "commandCount": 0,
        "expectedEndCanonicalStateSha256": "sha256:d61217a830053e59f9c75a69eef110da2604892baf9b52ea74cb04d406ad6fec",
        "allowedInput": "HUD_SPEED_NORMAL",
        "allowedFrameCount": 60,
        "framesPerSecond": 60,
        "claimLabel": "TARGETED_LIVE_CHECKPOINT_PASS:A1_NORMAL_READY",
    },
    {
        "checkpointId": "A1_CONSTRUCTION_DUE_1M",
        "startMinute": 1259,
        "startCanonicalStateSha256": "sha256:3a00c6c937d130cc7574e3971403445cb036a26aecba6671e300e1398d4b9989",
        "commandReplaySchema": "gridworks.targeted-live-command-replay.v1",
        "commandReplaySha256": "sha256:9bd7c3226fd36396d9d9f7a8d81da25379cedb8e0e54441601bb7c89e947c65c",
        "commandCount": 3,
        "expectedEndCanonicalStateSha256": "sha256:304b96410d7652db9928613fe77443d8d50e29efcb273ff8061c064f876f37f9",
        "allowedInput": "HUD_SPEED_NORMAL",
        "allowedFrameCount": 60,
        "framesPerSecond": 60,
        "claimLabel": "TARGETED_LIVE_CHECKPOINT_PASS:A1_CONSTRUCTION_DUE_1M",
    },
)

CHECKPOINT_PRESENTATION_EXPECTATIONS: dict[str, dict[str, Any]] = {
    "A1_NORMAL_READY": {
        "construction": "none",
        "endMinute": 1021,
        "presentationRevision": 6,
        "renderedAssets": 33,
        "hudClock": "1일_17:01",
    },
    "A1_CONSTRUCTION_DUE_1M": {
        "construction": "Line,due=1260,nodes=,edges=PLAYER_EDGE_1",
        "endMinute": 1260,
        "presentationRevision": 12,
        "renderedAssets": 34,
        "hudClock": "1일_21:00",
    },
}

FIXTURE_RUNTIME_FACTS = {
    "campaignSchema": "gridworks.realtime.campaign.v3",
    "campaignId": "CHEONGRYU_RELEASE_CAMPAIGN",
    "campaignDefinitionHash": (
        "4dc4dee6a9740e6b3babf1f9b2ccf9b8d107e541c9918e33a822ef4006163519"
    ),
    "worldSchema": "gridworks.realtime.world.v3",
    "worldId": "CHEONGRYU_COMMERCIAL_WORLD",
    "worldDefinitionHash": (
        "7bc7061a5564dbbbf0d98217c60e977ed20287f6b5da71f8153b6893a0923b60"
    ),
}

HEADLESS_EXECUTION_SCHEMA = "gridworks.realtime-headless-execution-authority.v1"
HEADLESS_COMMAND_TEMPLATE = (
    "$GODOT_APP_ROOT/Contents/MacOS/Godot",
    "--headless",
    "--path",
    "$EPHEMERAL_EXACT_PACKAGE_ROOT",
    "--scene",
    CHECKPOINT_SCENE,
    "--log-file",
    "$EPHEMERAL_PROBE_LOG",
    "--",
    "--checkpoint=<EXACT_CHECKPOINT_ID>",
)

GODOT_EPHEMERAL_EMPTY_DIRECTORY_SIDE_EFFECTS = (
    "Godot",
    "Godot/app_userdata",
    "Godot/app_userdata/Gridworks",
)

READY_FIELD_ORDER = (
    "id",
    "campaignSchema",
    "campaignId",
    "campaignSourceHash",
    "campaignDefinitionHash",
    "worldSchema",
    "worldId",
    "worldSourceHash",
    "worldDefinitionHash",
    "replaySchema",
    "replayHash",
    "commandCount",
    "startMinute",
    "startHash",
    "expectedEndHash",
    "construction",
    "activeEvents",
    "activeDuty",
    "thermalAssets",
    "selection",
    "anchor",
    "surface",
    "tool",
    "simulation",
    "allowedInput",
    "allowedFrames",
)

PASS_FIELD_ORDER = (
    "startMinute",
    "startHash",
    "replayHash",
    "endMinute",
    "endHash",
    "presentationRevision",
    "renderedAssets",
    "hudClock",
)

EXACTLY_ONE_CHECKPOINT_MESSAGE = (
    "Exactly one --checkpoint=<ID> user argument is required; known IDs: "
    "A1_NORMAL_READY, A1_CONSTRUCTION_DUE_1M."
)

ARGUMENT_REJECTION_CASES = (
    {
        "probeId": "REJECT_MISSING_ARGUMENT",
        "userArguments": (),
        "message": EXACTLY_ONE_CHECKPOINT_MESSAGE,
        "stderrRawSha256": (
            "sha256:200d069a787e3549ef107524440c40f474f57b845516452159f6bfc61cc8fd53"
        ),
    },
    {
        "probeId": "REJECT_EXTRA_ARGUMENT",
        "userArguments": ("--checkpoint=A1_NORMAL_READY", "--unexpected"),
        "message": EXACTLY_ONE_CHECKPOINT_MESSAGE,
        "stderrRawSha256": (
            "sha256:200d069a787e3549ef107524440c40f474f57b845516452159f6bfc61cc8fd53"
        ),
    },
    {
        "probeId": "REJECT_FULL_FLOW_AS_CHECKPOINT",
        "userArguments": ("--checkpoint=FULL_FLOW",),
        "message": (
            "Unknown checkpoint 'FULL_FLOW'; known IDs: "
            "A1_NORMAL_READY, A1_CONSTRUCTION_DUE_1M."
        ),
        "stderrRawSha256": (
            "sha256:2cdab335f652b3d780c92a96f87b21ac87b5a4512941cb1f856aa47f5884db08"
        ),
    },
)

HEADLESS_AUTHORITY_KEYS = frozenset({
    "schemaVersion",
    "executionKind",
    "commandTemplate",
    "engineFileTreeSha256",
    "inputPackageTreeSha256",
    "freshProcessPerProbe",
    "freshExactPackageCopyPerProbe",
    "ephemeralProbeFilesystemOutputsExcludedFromPackageFileAuthority",
    "expectedEphemeralEmptyDirectorySideEffects",
    "dotnetSdkVersion",
    "hostRuntimeBinding",
    "positiveCheckpointProbes",
    "argumentRejectionProbes",
    "nativePresentationObserved",
    "scoreBearingEvidence",
    "executionSha256",
})

POSITIVE_PROBE_KEYS = frozenset({
    "probeId",
    "checkpointId",
    "userArguments",
    "inputFileCount",
    "inputTreeSha256",
    "inputCacheState",
    "exitCode",
    "readyClaimOccurrenceCount",
    "passClaimOccurrenceCount",
    "unexpectedTypedClaimCount",
    "readyClaimRawSha256",
    "passClaimRawSha256",
    "stdoutRawSha256",
    "stdoutByteLength",
    "stdoutUtf8",
    "stderrRawSha256",
    "stderrByteLength",
    "stderrUtf8",
    "logRawSha256",
    "logByteLength",
    "logUtf8",
    "observedEphemeralEmptyDirectorySideEffects",
    "readyClaim",
    "passClaim",
    "boundFileByteMutationCount",
    "nativePresentationObserved",
    "scoreBearingEvidence",
})

REJECTION_PROBE_KEYS = frozenset({
    "probeId",
    "userArguments",
    "inputFileCount",
    "inputTreeSha256",
    "inputCacheState",
    "exitCode",
    "rejectionCode",
    "rejectionMessage",
    "rejectionLineRawSha256",
    "stdoutRawSha256",
    "stdoutByteLength",
    "stdoutUtf8",
    "stderrRawSha256",
    "stderrByteLength",
    "stderrUtf8",
    "logRawSha256",
    "logByteLength",
    "logUtf8",
    "observedEphemeralEmptyDirectorySideEffects",
    "readyClaimOccurrenceCount",
    "passClaimOccurrenceCount",
    "fullFlowClaimOccurrenceCount",
    "boundFileByteMutationCount",
    "scoreBearingEvidence",
})

READY_CLAIM_KEYS = frozenset({
    "claimLabel",
    "checkpointId",
    "campaignSchema",
    "campaignId",
    "campaignSourceSha256",
    "campaignDefinitionSha256",
    "worldSchema",
    "worldId",
    "worldSourceSha256",
    "worldDefinitionSha256",
    "commandReplaySchema",
    "commandReplaySha256",
    "commandCount",
    "startMinute",
    "startCanonicalStateSha256",
    "expectedEndCanonicalStateSha256",
    "construction",
    "activeEventCount",
    "activeDuty",
    "thermalAssetCount",
    "selection",
    "timelineAnchor",
    "surface",
    "tool",
    "simulation",
    "allowedInput",
    "allowedFrameCount",
    "framesPerSecond",
})

PASS_CLAIM_KEYS = frozenset({
    "claimLabel",
    "startMinute",
    "startCanonicalStateSha256",
    "commandReplaySha256",
    "endMinute",
    "endCanonicalStateSha256",
    "presentationRevision",
    "renderedAssetCount",
    "hudClock",
})

EXPORT_RELEASE_GAME_SOURCES = (
    "game/CommercialAudio.cs",
    "game/CommercialAudioLibrary.cs",
    "game/CommercialLaunchOptions.cs",
    "game/CommercialMain.cs",
    "game/CommercialMapTransform.cs",
    "game/CommercialMapView.cs",
    "game/CommercialProductResources.cs",
    "game/CommercialShell.cs",
    "game/CommercialTaskPanel.cs",
)


class CandidateAuthorityError(RuntimeError):
    """The candidate authority could not be built or verified."""


def sha256_bytes(data: bytes) -> str:
    return SHA256_PREFIX + hashlib.sha256(data).hexdigest()


def canonical_bytes(value: Any) -> bytes:
    """GRIDWORKS_CANONICAL_JSON_V1: UTF-8, sorted keys, compact JSON, no NaN."""

    return json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def canonical_sha256(value: Any) -> str:
    return sha256_bytes(canonical_bytes(value))


def strict_typed_equal(left: Any, right: Any) -> bool:
    """Compare JSON-shaped values without Python's bool/int aliasing."""

    if type(left) is not type(right):
        return False
    if isinstance(left, dict):
        return set(left) == set(right) and all(
            strict_typed_equal(left[key], right[key]) for key in left
        )
    if isinstance(left, list):
        return len(left) == len(right) and all(
            strict_typed_equal(left_value, right_value)
            for left_value, right_value in zip(left, right)
        )
    return left == right


def require_exact_keys(
    value: dict[str, Any],
    expected: Sequence[str] | frozenset[str],
    label: str,
) -> None:
    if set(value) != set(expected):
        raise CandidateAuthorityError(f"{label} field set drift")


def strict_json_bytes(data: bytes, label: str) -> dict[str, Any]:
    def reject_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise CandidateAuthorityError(f"{label} repeats key {key}")
            result[key] = value
        return result

    def reject_nonfinite(value: str) -> None:
        raise CandidateAuthorityError(
            f"{label} contains non-JSON numeric token {value}"
        )

    try:
        value = json.loads(
            data,
            object_pairs_hook=reject_duplicates,
            parse_constant=reject_nonfinite,
        )
    except (UnicodeError, json.JSONDecodeError) as error:
        raise CandidateAuthorityError(f"{label} is not strict JSON: {error}") from error
    if not isinstance(value, dict):
        raise CandidateAuthorityError(f"{label} must contain one JSON object")
    return value


def run_command(
    arguments: Sequence[str],
    *,
    cwd: Path,
    label: str,
    env: dict[str, str] | None = None,
    timeout: int = 240,
) -> bytes:
    try:
        completed = subprocess.run(
            list(arguments),
            cwd=cwd,
            env=env,
            check=False,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=timeout,
        )
    except (OSError, subprocess.SubprocessError) as error:
        raise CandidateAuthorityError(f"{label} could not execute: {error}") from error
    if completed.returncode != 0:
        detail = (
            completed.stderr.decode("utf-8", errors="replace").strip()
            or completed.stdout.decode("utf-8", errors="replace").strip()
        )
        raise CandidateAuthorityError(f"{label} failed: {detail}")
    return completed.stdout


def read_regular_file(path: Path, label: str) -> tuple[Path, bytes]:
    try:
        resolved = path.resolve(strict=True)
    except OSError as error:
        raise CandidateAuthorityError(f"{label} cannot be opened: {error}") from error
    if path.is_symlink() or not resolved.is_file():
        raise CandidateAuthorityError(f"{label} must be a regular non-symlink file")
    flags = os.O_RDONLY
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    descriptor = os.open(resolved, flags)
    try:
        before = os.fstat(descriptor)
        if not stat.S_ISREG(before.st_mode):
            raise CandidateAuthorityError(f"{label} is not a regular file")
        chunks: list[bytes] = []
        while True:
            chunk = os.read(descriptor, 1024 * 1024)
            if not chunk:
                break
            chunks.append(chunk)
        after = os.fstat(descriptor)
        identity_before = (
            before.st_dev,
            before.st_ino,
            before.st_size,
            before.st_mtime_ns,
        )
        identity_after = (
            after.st_dev,
            after.st_ino,
            after.st_size,
            after.st_mtime_ns,
        )
        if identity_before != identity_after:
            raise CandidateAuthorityError(f"{label} changed while being read")
        data = b"".join(chunks)
        if len(data) != after.st_size:
            raise CandidateAuthorityError(f"{label} changed byte length while being read")
        return resolved, data
    finally:
        os.close(descriptor)


def git_execution_environment() -> dict[str, str]:
    """Return the complete environment used by authority-bearing Git reads."""

    return {
        "PATH": "/usr/bin:/bin",
        "LANG": "C",
        "LC_ALL": "C",
        "GIT_CONFIG_GLOBAL": "/dev/null",
        "GIT_CONFIG_NOSYSTEM": "1",
        "GIT_CONFIG_SYSTEM": "/dev/null",
        "GIT_NO_REPLACE_OBJECTS": "1",
        "GIT_OPTIONAL_LOCKS": "0",
        "GIT_TERMINAL_PROMPT": "0",
    }


def resolve_git_directory(repository_root: Path) -> Path:
    """Resolve only canonical worktree .git forms without consulting Git."""

    git_entry = repository_root / ".git"
    try:
        entry_stat = git_entry.lstat()
    except OSError as error:
        raise CandidateAuthorityError(
            f"repository .git entry cannot be opened: {error}"
        ) from error
    if stat.S_ISLNK(entry_stat.st_mode):
        raise CandidateAuthorityError("repository .git entry must not be a symlink")
    if stat.S_ISDIR(entry_stat.st_mode):
        try:
            resolved = git_entry.resolve(strict=True)
        except OSError as error:
            raise CandidateAuthorityError(
                f"repository Git directory cannot be resolved: {error}"
            ) from error
        if not resolved.is_dir():
            raise CandidateAuthorityError("repository .git entry is not a directory")
        return resolved
    if not stat.S_ISREG(entry_stat.st_mode):
        raise CandidateAuthorityError(
            "repository .git entry must be a directory or strict gitdir file"
        )
    _resolved_entry, data = read_regular_file(
        git_entry,
        "repository linked-worktree .git file",
    )
    try:
        text = data.decode("utf-8", errors="strict")
    except UnicodeError as error:
        raise CandidateAuthorityError(
            "repository linked-worktree .git file is not UTF-8"
        ) from error
    match = re.fullmatch(r"gitdir: ([^\r\n]+)\r?\n?", text)
    if match is None:
        raise CandidateAuthorityError(
            "repository linked-worktree .git file has non-canonical syntax"
        )
    raw_target = Path(match.group(1))
    target = raw_target if raw_target.is_absolute() else git_entry.parent / raw_target
    try:
        target_stat = target.lstat()
    except OSError as error:
        raise CandidateAuthorityError(
            f"repository linked-worktree Git directory cannot be opened: {error}"
        ) from error
    if stat.S_ISLNK(target_stat.st_mode) or not stat.S_ISDIR(target_stat.st_mode):
        raise CandidateAuthorityError(
            "repository linked-worktree Git directory must be a non-symlink directory"
        )
    try:
        return target.resolve(strict=True)
    except OSError as error:
        raise CandidateAuthorityError(
            f"repository linked-worktree Git directory cannot be resolved: {error}"
        ) from error


def verify_git_executable_binding() -> None:
    resolved, data = read_regular_file(
        GIT_EXECUTABLE_PATH,
        "authority Git executable",
    )
    if (
        resolved != GIT_EXECUTABLE_PATH
        or len(data) != GIT_EXECUTABLE_BYTE_LENGTH
        or sha256_bytes(data) != GIT_EXECUTABLE_RAW_SHA256
        or not os.access(resolved, os.X_OK)
    ):
        raise CandidateAuthorityError("authority Git executable binding drift")


def run_git_command(
    repository_root: Path,
    arguments: Sequence[str],
    *,
    label: str,
    timeout: int = 30,
) -> bytes:
    """Run one allowlisted read-only Git command with no ambient Git state."""

    if not arguments or arguments[0] not in {
        "--version",
        "cat-file",
        "ls-tree",
        "rev-parse",
    }:
        raise CandidateAuthorityError("authority Git subcommand is not allowlisted")
    try:
        root = repository_root.resolve(strict=True)
    except OSError as error:
        raise CandidateAuthorityError(
            f"authority Git work tree cannot be resolved: {error}"
        ) from error
    if not root.is_dir():
        raise CandidateAuthorityError("authority Git work tree must be a directory")
    git_directory = resolve_git_directory(root)
    verify_git_executable_binding()
    output = run_command(
        [
            str(GIT_EXECUTABLE_PATH),
            "--no-replace-objects",
            f"--git-dir={git_directory}",
            f"--work-tree={root}",
            "-c",
            "core.hooksPath=/dev/null",
            *arguments,
        ],
        cwd=root,
        env=git_execution_environment(),
        timeout=timeout,
        label=label,
    )
    verify_git_executable_binding()
    return output


def expected_git_command_authority() -> dict[str, Any]:
    return {
        "path": str(GIT_EXECUTABLE_PATH),
        "rawSha256": GIT_EXECUTABLE_RAW_SHA256,
        "byteLength": GIT_EXECUTABLE_BYTE_LENGTH,
        "versionOutput": GIT_VERSION_OUTPUT,
        "commandBindingScope": GIT_COMMAND_BINDING_SCOPE,
        "allowedReadOnlySubcommands": [
            "--version",
            "cat-file",
            "ls-tree",
            "rev-parse",
        ],
        "globalArguments": [
            "--no-replace-objects",
            "--git-dir=<RESOLVED_GIT_DIRECTORY>",
            "--work-tree=<RESOLVED_REPOSITORY_ROOT>",
            "-c",
            "core.hooksPath=/dev/null",
        ],
        "environment": git_execution_environment(),
        "environmentPolicy": GIT_ENVIRONMENT_POLICY,
        "repositoryLocationPolicy": GIT_REPOSITORY_LOCATION_POLICY,
        "gitDirectoryEntryPolicy": (
            "NON_SYMLINK_DIRECTORY_OR_STRICT_GITDIR_FILE_TO_NON_SYMLINK_DIRECTORY"
        ),
        "replacementObjectPolicy": GIT_REPLACEMENT_OBJECT_POLICY,
    }


def bind_git_command_authority(repository_root: Path) -> dict[str, Any]:
    version = run_git_command(
        repository_root,
        ["--version"],
        timeout=10,
        label="authority Git version probe",
    ).decode("ascii", errors="strict").strip()
    if version != GIT_VERSION_OUTPUT:
        raise CandidateAuthorityError("authority Git version output drift")
    return expected_git_command_authority()


def write_exclusive(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    descriptor = os.open(path, flags, 0o600)
    try:
        view = memoryview(data)
        while view:
            written = os.write(descriptor, view)
            if written <= 0:
                raise OSError("short write")
            view = view[written:]
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


@dataclasses.dataclass(frozen=True)
class GitBlob:
    path: str
    mode: str
    object_id: str
    role: str
    data: bytes

    def row(self) -> dict[str, Any]:
        return {
            "path": self.path,
            "role": self.role,
            "gitMode": self.mode,
            "gitObjectId": self.object_id,
            "rawSha256": sha256_bytes(self.data),
            "byteLength": len(self.data),
        }


@dataclasses.dataclass(frozen=True)
class FileBinding:
    path: Path
    role: str
    raw_sha256: str
    byte_length: int

    @classmethod
    def open(cls, path: Path, role: str, label: str) -> "FileBinding":
        resolved, data = read_regular_file(path, label)
        return cls(resolved, role, sha256_bytes(data), len(data))

    def verify(self, label: str) -> None:
        resolved, data = read_regular_file(self.path, label)
        if resolved != self.path:
            raise CandidateAuthorityError(f"{label} path changed after binding")
        if len(data) != self.byte_length or sha256_bytes(data) != self.raw_sha256:
            raise CandidateAuthorityError(f"{label} raw-byte binding mismatch")

    def row(self, path: str) -> dict[str, Any]:
        return {
            "path": path,
            "role": self.role,
            "rawSha256": self.raw_sha256,
            "byteLength": self.byte_length,
        }


@dataclasses.dataclass(frozen=True)
class SourceAuthority:
    repository_root: Path
    source_commit: str
    blobs: dict[str, GitBlob]
    game_sources: tuple[str, ...]
    core_sources: tuple[str, ...]
    embedded_resources: tuple[str, ...]

    @property
    def rows(self) -> list[dict[str, Any]]:
        return [self.blobs[path].row() for path in sorted(self.blobs)]


def resolve_repository_root(path: Path) -> Path:
    try:
        root = path.resolve(strict=True)
    except OSError as error:
        raise CandidateAuthorityError(f"repository root cannot be opened: {error}") from error
    if not root.is_dir():
        raise CandidateAuthorityError("repository root must be an existing Git worktree")
    resolve_git_directory(root)
    return root


def resolve_source_commit(repository_root: Path, revision: str) -> str:
    commit = run_git_command(
        repository_root,
        [
            "rev-parse",
            "--verify",
            "--end-of-options",
            f"{revision}^{{commit}}",
        ],
        timeout=10,
        label="candidate source commit resolution",
    ).decode("ascii").strip()
    if re.fullmatch(r"[0-9a-f]{40}", commit) is None:
        raise CandidateAuthorityError("candidate source commit is not a full SHA-1")
    return commit


def validate_git_path(path: str) -> None:
    if (
        not path
        or path.startswith("/")
        or "\\" in path
        or any(ord(character) < 0x20 for character in path)
    ):
        raise CandidateAuthorityError(f"non-canonical Git path: {path!r}")
    pure = PurePosixPath(path)
    if any(component in {"", ".", ".."} for component in pure.parts):
        raise CandidateAuthorityError(f"Git path escapes its root: {path!r}")


def git_tree_entries(
    repository_root: Path,
    commit: str,
) -> list[tuple[str, str, str, str]]:
    if re.fullmatch(r"[0-9a-f]{40}", commit) is None:
        raise CandidateAuthorityError("candidate tree commit is not a full SHA-1")
    raw = run_git_command(
        repository_root,
        ["ls-tree", "-rz", "--full-tree", commit],
        timeout=30,
        label="candidate Git tree enumeration",
    )
    result: list[tuple[str, str, str, str]] = []
    for raw_row in raw.split(b"\0"):
        if not raw_row:
            continue
        try:
            header, raw_path = raw_row.split(b"\t", 1)
            mode, object_type, object_id = header.decode("ascii").split(" ")
            path = raw_path.decode("utf-8", errors="strict")
        except (UnicodeError, ValueError) as error:
            raise CandidateAuthorityError("candidate Git tree has a malformed entry") from error
        validate_git_path(path)
        result.append((mode, object_type, object_id, path))
    return result


def _candidate_scope(path: str) -> bool:
    folded = path.casefold()
    return folded.startswith((
        "game/",
        "src/gridworks.core/",
        "data/",
        "tools/gridworks.commercialchecks/",
        "tools/gridworks.realtimechecks/fixtures/",
        "playtests/commercial-ux-87-realtime/text-plan-r0/",
    )) or folded in {GLOBAL_JSON_PATH.casefold(), DIRECTORY_PROPS_PATH.casefold()}


def validate_tree_aliases(entries: Sequence[tuple[str, str, str, str]]) -> None:
    folded_paths: dict[str, str] = {}
    for mode, object_type, _object_id, path in entries:
        if not _candidate_scope(path):
            continue
        if mode == "120000":
            raise CandidateAuthorityError(f"candidate scope contains a symlink: {path}")
        if mode == "160000" or object_type == "commit":
            raise CandidateAuthorityError(f"candidate scope contains a gitlink: {path}")
        if (
            path.casefold().startswith(("game/", "src/gridworks.core/"))
            and _is_reserved_source(path)
        ):
            raise CandidateAuthorityError(
                f"candidate source scope contains reserved output path: {path}"
            )
        folded = path.casefold()
        previous = folded_paths.get(folded)
        if previous is not None and previous != path:
            raise CandidateAuthorityError(
                f"candidate scope contains a case collision: {previous} / {path}"
            )
        folded_paths[folded] = path


def _is_reserved_source(path: str) -> bool:
    parts = PurePosixPath(path).parts
    return any(part.casefold() in {".godot", "bin", "obj"} for part in parts)


def _role_for_path(
    path: str,
    game_sources: set[str],
    core_sources: set[str],
    embedded: set[str],
    script_uids: set[str],
) -> str | None:
    if path in game_sources:
        return "GAME_DEBUG_COMPILE_SOURCE"
    if path in core_sources:
        return "CORE_DEBUG_COMPILE_SOURCE"
    if path in embedded:
        return "GAME_DEBUG_EMBEDDED_RESOURCE"
    if path in RUNTIME_SCENE_FILES:
        return "R2_RUNTIME_SCENE_RESOURCE"
    if path in script_uids:
        return "R2_SCRIPT_UID"
    if path in {FULL_REALTIME_WORLD_PATH, FULL_REALTIME_CAMPAIGN_PATH}:
        return "DECLARED_NONRUNTIME_FULL_V3_AUTHORITY"
    if path in {STORY_PROGRAM_PATH, STORY_HARNESS_PATH}:
        return "STORY_HARNESS_SOURCE"
    if path == STORED_STORY_MANIFEST_PATH:
        return "STORED_STORY_MANIFEST"
    if path == "game/CommercialMain.tscn":
        return "NEGATIVE_DEFAULT_SCENE_AUTHORITY"
    if path == EXPORT_PRESETS_PATH:
        return "NEGATIVE_EXPORT_AUTHORITY"
    if path in {
        GLOBAL_JSON_PATH,
        DIRECTORY_PROPS_PATH,
        GAME_PROJECT_PATH,
        CORE_PROJECT_PATH,
        PROJECT_GODOT_PATH,
    }:
        return "BUILD_DECLARATION"
    return None


def read_source_authority(
    repository_root: Path,
    revision: str = "HEAD",
) -> SourceAuthority:
    root = resolve_repository_root(repository_root)
    commit = resolve_source_commit(root, revision)
    entries = git_tree_entries(root, commit)
    validate_tree_aliases(entries)
    by_path = {row[3]: row for row in entries}
    game_sources = {
        path
        for _mode, object_type, _oid, path in entries
        if object_type == "blob"
        and path.startswith("game/")
        and path.endswith(".cs")
        and not path.startswith("game/realtime/world/")
        and not _is_reserved_source(path)
    }
    core_sources = {
        path
        for _mode, object_type, _oid, path in entries
        if object_type == "blob"
        and path.startswith("src/Gridworks.Core/")
        and path.endswith(".cs")
        and path != "src/Gridworks.Core/Release/V3/RealtimeCampaignPersistence.cs"
        and not _is_reserved_source(path)
    }
    embedded = {path for path, _link, _logical in EMBEDDED_RESOURCES}
    script_uids = {
        f"{path}.uid"
        for path in SCENE_ATTACHED_SCRIPTS
        if f"{path}.uid" in by_path
    }
    roles: dict[str, str] = {}
    for path in by_path:
        role = _role_for_path(path, game_sources, core_sources, embedded, script_uids)
        if role is not None:
            roles[path] = role
    required = (
        set(EXPECTED_DECLARATION_HASHES)
        | embedded
        | set(RUNTIME_SCENE_FILES)
        | {FULL_REALTIME_WORLD_PATH, FULL_REALTIME_CAMPAIGN_PATH}
        | {STORY_PROGRAM_PATH, STORY_HARNESS_PATH, STORED_STORY_MANIFEST_PATH}
        | {"game/CommercialMain.tscn"}
    )
    missing = sorted(required - set(roles))
    if missing:
        raise CandidateAuthorityError(
            "candidate Git tree lacks required authority blobs: " + ", ".join(missing)
        )
    if len(game_sources) != 60 or len(core_sources) != 67 or len(embedded) != 9:
        raise CandidateAuthorityError(
            "Debug authority drift: expected Game/Core/resources 60/67/9, observed "
            f"{len(game_sources)}/{len(core_sources)}/{len(embedded)}"
        )
    blobs: dict[str, GitBlob] = {}
    for path, role in sorted(roles.items()):
        mode, object_type, object_id, _path = by_path[path]
        if mode not in {"100644", "100755"} or object_type != "blob":
            raise CandidateAuthorityError(f"candidate input is not a regular blob: {path}")
        data = run_git_command(
            root,
            ["cat-file", "blob", object_id],
            timeout=30,
            label=f"candidate Git blob {path}",
        )
        blob = GitBlob(path, mode, object_id, role, data)
        expected_hash = EXPECTED_DECLARATION_HASHES.get(path)
        if expected_hash is not None and sha256_bytes(data) != expected_hash:
            raise CandidateAuthorityError(f"frozen declaration hash drift: {path}")
        blobs[path] = blob
    return SourceAuthority(
        repository_root=root,
        source_commit=commit,
        blobs=blobs,
        game_sources=tuple(sorted(game_sources)),
        core_sources=tuple(sorted(core_sources)),
        embedded_resources=tuple(sorted(embedded)),
    )


def bind_evaluator_producer_authority(
    source: SourceAuthority,
) -> dict[str, Any]:
    expected_script_dir = (
        source.repository_root / "tools" / "commercial-ux" / "native"
    ).resolve(strict=True)
    if SCRIPT_DIR != expected_script_dir:
        raise CandidateAuthorityError(
            "running evaluator producer is outside the candidate repository"
        )
    if Path(__file__).resolve(strict=True) != (
        expected_script_dir / "build-realtime-candidate-authority.py"
    ).resolve(strict=True):
        raise CandidateAuthorityError(
            "running evaluator producer path differs from canonical Git path"
        )
    git_command_authority = bind_git_command_authority(source.repository_root)
    entries = git_tree_entries(source.repository_root, source.source_commit)
    by_path = {path: (mode, object_type, object_id) for mode, object_type, object_id, path in entries}
    rows: list[dict[str, Any]] = []
    for path, role in EVALUATOR_PRODUCER_PATH_ROLES:
        entry = by_path.get(path)
        if entry is None:
            raise CandidateAuthorityError(
                f"source commit lacks evaluator producer authority file: {path}"
            )
        mode, object_type, object_id = entry
        if mode not in {"100644", "100755"} or object_type != "blob":
            raise CandidateAuthorityError(
                f"evaluator producer authority is not a regular Git blob: {path}"
            )
        git_data = run_git_command(
            source.repository_root,
            ["cat-file", "blob", object_id],
            timeout=30,
            label=f"evaluator producer Git blob {path}",
        )
        _resolved, running_data = read_regular_file(
            source.repository_root / path,
            f"running evaluator producer authority {path}",
        )
        if running_data != git_data:
            raise CandidateAuthorityError(
                f"running evaluator authority differs from source commit: {path}"
            )
        rows.append(GitBlob(path, mode, object_id, role, git_data).row())
    rows.sort(key=lambda row: row["path"])
    return {
        "schemaVersion": EVALUATOR_PRODUCER_SCHEMA,
        "sourceCommit": source.source_commit,
        "fileCount": len(rows),
        "files": rows,
        "filesSha256": canonical_sha256(rows),
        "runningFilesMatchGitBlobs": True,
        "gitCommandAuthority": git_command_authority,
        "semanticVerifierEntryPoint": (
            "verify_manifest_against_reconstructed_authority"
        ),
        "semanticVerifierReexecutesHeadlessProbes": True,
        "structuralSchemaAuthority": (
            "STRUCTURAL_ONLY_NOT_CANDIDATE_AUTHORITY"
        ),
    }


def materialize_blobs(source_root: Path, blobs: dict[str, GitBlob]) -> None:
    for path, blob in sorted(blobs.items()):
        write_exclusive(source_root / path, blob.data)
    observed = sorted(
        path.relative_to(source_root).as_posix()
        for path in source_root.rglob("*")
        if path.is_file()
    )
    if observed != sorted(blobs):
        raise CandidateAuthorityError("materialized source tree differs from exact Git inputs")


def _xml(value: str) -> str:
    return html.escape(value, quote=True)


def generated_core_project(core_sources: Sequence[str]) -> bytes:
    rows = "\n".join(
        f'    <Compile Include="../{_xml(path)}" '
        f'Link="{_xml(path.removeprefix("src/Gridworks.Core/"))}" />'
        for path in core_sources
    )
    return (
        '<Project Sdk="Microsoft.NET.Sdk">\n'
        "  <PropertyGroup>\n"
        "    <TargetFramework>net8.0</TargetFramework>\n"
        "    <ImplicitUsings>enable</ImplicitUsings>\n"
        "    <Nullable>enable</Nullable>\n"
        "    <LangVersion>12.0</LangVersion>\n"
        "    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>\n"
        "    <IsPackable>false</IsPackable>\n"
        "    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>\n"
        "    <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>\n"
        "    <AssemblyName>Gridworks.Core</AssemblyName>\n"
        "    <RootNamespace>Gridworks.Core</RootNamespace>\n"
        "    <DebugType>none</DebugType>\n"
        "    <DebugSymbols>false</DebugSymbols>\n"
        "    <Deterministic>true</Deterministic>\n"
        "    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>\n"
        "    <PathMap>$(CandidateSourceRoot)=/_</PathMap>\n"
        "  </PropertyGroup>\n"
        "  <ItemGroup>\n"
        f"{rows}\n"
        "  </ItemGroup>\n"
        "  <ItemGroup>\n"
        '    <SourceRoot Include="$(CandidateSourceRoot)/" />\n'
        "  </ItemGroup>\n"
        "</Project>\n"
    ).encode("utf-8")


def generated_game_project(
    game_sources: Sequence[str],
) -> bytes:
    compile_rows = "\n".join(
        f'    <Compile Include="../{_xml(path)}" '
        f'Link="{_xml(path.removeprefix("game/"))}" />'
        for path in game_sources
    )
    resource_rows = "\n".join(
        f'    <EmbeddedResource Include="../{_xml(path)}" '
        f'Link="{_xml(link)}" LogicalName="{_xml(logical)}" />'
        for path, link, logical in EMBEDDED_RESOURCES
    )
    return (
        f'<Project Sdk="Godot.NET.Sdk/{GODOT_VERSION}">\n'
        "  <PropertyGroup>\n"
        "    <TargetFramework>net8.0</TargetFramework>\n"
        "    <RootNamespace>Gridworks.Game</RootNamespace>\n"
        "    <AssemblyName>Gridworks.Game</AssemblyName>\n"
        "    <GodotProjectDir>$(CandidateSourceRoot)/game</GodotProjectDir>\n"
        "    <EnableDynamicLoading>true</EnableDynamicLoading>\n"
        "    <Nullable>enable</Nullable>\n"
        "    <LangVersion>12.0</LangVersion>\n"
        "    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>\n"
        "    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>\n"
        "    <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>\n"
        "    <DebugType>none</DebugType>\n"
        "    <DebugSymbols>false</DebugSymbols>\n"
        "    <Deterministic>true</Deterministic>\n"
        "    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>\n"
        "    <PathMap>$(CandidateSourceRoot)=/_</PathMap>\n"
        "    <DefineConstants>$(DefineConstants);DEBUG</DefineConstants>\n"
        "  </PropertyGroup>\n"
        "  <ItemGroup>\n"
        f"{compile_rows}\n"
        "  </ItemGroup>\n"
        "  <ItemGroup>\n"
        '    <ProjectReference Include="CandidateCore.csproj" />\n'
        "  </ItemGroup>\n"
        "  <ItemGroup>\n"
        f"{resource_rows}\n"
        "  </ItemGroup>\n"
        "  <ItemGroup>\n"
        '    <SourceRoot Include="$(CandidateSourceRoot)/" />\n'
        "  </ItemGroup>\n"
        "</Project>\n"
    ).encode("utf-8")


def generated_story_project() -> bytes:
    return b"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>12.0</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>
    <AssemblyName>Gridworks.CandidateStory</AssemblyName>
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
    <PathMap>$(CandidateSourceRoot)=/_</PathMap>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="../tools/Gridworks.CommercialChecks/Program.cs" Link="Program.cs" />
    <Compile Include="../tools/Gridworks.CommercialChecks/CommercialStoryPartHarness.cs" Link="CommercialStoryPartHarness.cs" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="CandidateCore.csproj" />
  </ItemGroup>
  <ItemGroup>
    <SourceRoot Include="$(CandidateSourceRoot)/" />
  </ItemGroup>
</Project>
"""


def _default_package_root() -> Path:
    override = os.environ.get("GRIDWORKS_GODOT_NUGET_ROOT")
    return Path(override) if override else Path.home() / ".nuget" / "packages"


def _find_package(package_root: Path, file_name: str) -> Path:
    direct = (
        package_root / file_name,
        package_root
        / file_name.removesuffix(f".{GODOT_VERSION}.nupkg")
        / GODOT_VERSION
        / file_name,
    )
    for candidate in direct:
        if candidate.is_file():
            return candidate
    matches = sorted(package_root.rglob(file_name)) if package_root.is_dir() else []
    if len(matches) != 1:
        raise CandidateAuthorityError(
            f"Godot package root must contain exactly one {file_name}"
        )
    return matches[0]


def read_package_bindings(
    package_root: Path | None,
) -> tuple[tuple[Path, bytes, dict[str, Any]], ...]:
    root = (package_root or _default_package_root()).resolve(strict=False)
    result: list[tuple[Path, bytes, dict[str, Any]]] = []
    for file_name, (expected_hash, expected_length) in sorted(
        EXPECTED_GODOT_PACKAGES.items()
    ):
        path = _find_package(root, file_name)
        resolved, data = read_regular_file(path, f"Godot package {file_name}")
        if sha256_bytes(data) != expected_hash or len(data) != expected_length:
            raise CandidateAuthorityError(f"Godot package authority mismatch: {file_name}")
        result.append((resolved, data, {
            "path": PACKAGE_AUTHORITY_PATHS[file_name],
            "role": "GODOT_BUILD_PACKAGE",
            "rawSha256": expected_hash,
            "byteLength": expected_length,
        }))
    return tuple(result)


def _sanitized_environment(
    *,
    dotnet_path: Path,
    cli_home: Path,
    packages_path: Path,
    temporary_path: Path,
) -> dict[str, str]:
    environment = {
        key: os.environ[key]
        for key in ("PATH", "DOTNET_ROOT", "LANG", "LC_ALL")
        if key in os.environ
    }
    environment.update({
        "DOTNET_CLI_HOME": str(cli_home),
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        "DOTNET_HOST_PATH": str(dotnet_path),
        "DOTNET_MULTILEVEL_LOOKUP": "0",
        "DOTNET_NOLOGO": "1",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
        "NUGET_PACKAGES": str(packages_path),
        "TMPDIR": str(temporary_path),
    })
    return environment


def _isolation_properties(
    *,
    source_root: Path,
    packages_path: Path,
    user_extensions_path: Path,
) -> list[str]:
    return [
        f"-p:RestorePackagesPath={packages_path}",
        f"-p:CandidateSourceRoot={source_root}",
        "-p:ImportDirectoryBuildProps=false",
        "-p:ImportDirectoryBuildTargets=false",
        "-p:ImportDirectoryPackagesProps=false",
        "-p:CustomAfterMicrosoftCommonTargets=",
        "-p:CustomBeforeMicrosoftCommonTargets=",
        f"-p:MSBuildUserExtensionsPath={user_extensions_path}",
        "-p:ContinuousIntegrationBuild=true",
        "-p:Deterministic=true",
        "-p:NuGetAudit=false",
        "-p:RestoreIgnoreFailedSources=false",
    ]


def _single_file(root: Path, name: str, label: str) -> Path:
    matches = sorted(path.resolve() for path in root.rglob(name) if path.is_file())
    if len(matches) != 1:
        raise CandidateAuthorityError(
            f"{label} requires one fresh output, observed {len(matches)}"
        )
    try:
        matches[0].relative_to(root.resolve(strict=True))
    except ValueError as error:
        raise CandidateAuthorityError(f"{label} escaped isolated root") from error
    return matches[0]


def validate_story_manifest(data: bytes) -> dict[str, Any]:
    if not data.endswith(b"\n") or data.endswith(b"\n\n"):
        raise CandidateAuthorityError("story manifest must end in exactly one newline")
    value = strict_json_bytes(data, "story manifest")
    parts = value.get("parts")
    if (
        value.get("schemaVersion") != "gridworks.commercial.story-manifest.v2"
        or value.get("count") != 34
        or not isinstance(parts, list)
        or len(parts) != 34
    ):
        raise CandidateAuthorityError("story manifest must contain exact v2 34-part topology")
    kinds: dict[str, int] = {}
    selectors: list[str] = []
    for part in parts:
        if not isinstance(part, dict):
            raise CandidateAuthorityError("story manifest part must be an object")
        selector = part.get("selector")
        kind = part.get("kind")
        if not isinstance(selector, str) or not selector or not isinstance(kind, str):
            raise CandidateAuthorityError("story manifest part identity is invalid")
        if part.get("authoredReachable") is not True:
            raise CandidateAuthorityError(f"story part is not authored reachable: {selector}")
        selectors.append(selector)
        kinds[kind] = kinds.get(kind, 0) + 1
    if len(set(selectors)) != 34 or kinds != {
        "briefing": 8,
        "window": 6,
        "result": 11,
        "epilogue-card": 3,
        "epilogue-promise-line": 6,
    }:
        raise CandidateAuthorityError("story manifest topology drift")
    return value


@dataclasses.dataclass
class IsolatedBuild:
    source: SourceAuthority
    dotnet_rows: list[dict[str, Any]]
    package_rows: list[dict[str, Any]]
    generated_rows: list[dict[str, Any]]
    output_bindings: dict[str, FileBinding]
    runtime_package_bindings: dict[str, FileBinding]
    runtime_package_root: Path
    story_bytes: bytes

    def verify_outputs(self) -> None:
        for path, binding in self.output_bindings.items():
            binding.verify(path)
        for path, binding in self.runtime_package_bindings.items():
            binding.verify(f"runtime package {path}")
        _runtime_package_rows(self.runtime_package_root, self.runtime_package_bindings)

    @property
    def deterministic_projection(self) -> dict[str, Any]:
        return {
            "sourceCommit": self.source.source_commit,
            "sourceInputsSha256": canonical_sha256(self.source.rows),
            "dotnetInputsSha256": canonical_sha256(self.dotnet_rows),
            "packageInputsSha256": canonical_sha256(self.package_rows),
            "generatedInputsSha256": canonical_sha256(self.generated_rows),
            "outputs": [
                self.output_bindings[path].row(path)
                for path in sorted(self.output_bindings)
            ],
            "runtimePackageTreeSha256": canonical_sha256([
                self.runtime_package_bindings[path].row(path)
                for path in sorted(self.runtime_package_bindings)
            ]),
            "storyManifestRawSha256": sha256_bytes(self.story_bytes),
        }


def bind_dotnet_authority(dotnet_path: Path) -> list[dict[str, Any]]:
    resolved, wrapper = read_regular_file(dotnet_path, "dotnet command wrapper")
    install_root = resolved.parent.parent
    host_path = install_root / "libexec" / "dotnet"
    _host_resolved, host = read_regular_file(host_path, "dotnet host executable")
    rows = [
        {
            "path": "bin/dotnet",
            "role": "DOTNET_COMMAND_WRAPPER",
            "rawSha256": sha256_bytes(wrapper),
            "byteLength": len(wrapper),
        },
        {
            "path": "libexec/dotnet",
            "role": "DOTNET_HOST_EXECUTABLE",
            "rawSha256": sha256_bytes(host),
            "byteLength": len(host),
        },
    ]
    rows.sort(key=lambda row: row["path"])
    return rows


@contextlib.contextmanager
def isolated_managed_build(
    repository_root: Path,
    *,
    revision: str = "HEAD",
    package_root: Path | None = None,
    scratch_parent: Path | None = None,
) -> Iterator[IsolatedBuild]:
    source = read_source_authority(repository_root, revision)
    packages = read_package_bindings(package_root)
    parent = scratch_parent.resolve(strict=True) if scratch_parent else None
    if parent is not None and not parent.is_dir():
        raise CandidateAuthorityError("scratch parent must be a directory")
    with tempfile.TemporaryDirectory(
        prefix="gridworks-realtime-candidate-",
        dir=str(parent) if parent else None,
    ) as raw_temporary:
        build_root = Path(raw_temporary).resolve(strict=True)
        source_root = build_root / "source"
        generated_root = source_root / ".candidate-build"
        feed_root = build_root / "feed"
        packages_root = build_root / "packages"
        cli_home = build_root / "dotnet-home"
        tmp_root = build_root / "tmp"
        user_extensions = build_root / "msbuild-user-extensions"
        game_output = build_root / "game-output"
        story_output = build_root / "story-output"
        runtime_package_root = build_root / "candidate-package" / "game"
        for directory in (
            source_root,
            generated_root,
            feed_root,
            packages_root,
            cli_home,
            tmp_root,
            user_extensions,
            game_output,
            story_output,
            runtime_package_root,
        ):
            directory.mkdir(parents=True, exist_ok=False)
        materialize_blobs(source_root, source.blobs)
        generated = {
            "generated/CandidateCore.csproj": generated_core_project(source.core_sources),
            "generated/CandidateGame.csproj": generated_game_project(source.game_sources),
            "generated/CandidateStory.csproj": generated_story_project(),
        }
        for manifest_path, data in generated.items():
            write_exclusive(generated_root / Path(manifest_path).name, data)
        generated_rows = [
            {
                "path": path,
                "role": "EVALUATOR_GENERATED_PROJECT",
                "rawSha256": sha256_bytes(data),
                "byteLength": len(data),
            }
            for path, data in sorted(generated.items())
        ]
        package_rows: list[dict[str, Any]] = []
        for _source_path, data, row in packages:
            write_exclusive(feed_root / Path(row["path"]).name, data)
            package_rows.append(row)
        nuget_config = build_root / "NuGet.Config"
        write_exclusive(
            nuget_config,
            (
                '<?xml version="1.0" encoding="utf-8"?>\n'
                "<configuration><packageSources><clear />"
                f'<add key="gridworks-godot-{GODOT_VERSION}" '
                f'value="{html.escape(str(feed_root), quote=True)}" />'
                "</packageSources><disabledPackageSources><clear />"
                "</disabledPackageSources></configuration>\n"
            ).encode("utf-8"),
        )
        dotnet_raw = shutil.which("dotnet")
        if dotnet_raw is None:
            raise CandidateAuthorityError("dotnet executable is unavailable")
        dotnet_path = Path(dotnet_raw).resolve(strict=True)
        dotnet_rows = bind_dotnet_authority(dotnet_path)
        version = run_command(
            [str(dotnet_path), "--version"],
            cwd=source_root,
            label="dotnet version",
        ).decode("ascii").strip()
        if version != DOTNET_VERSION:
            raise CandidateAuthorityError(
                f"dotnet SDK drift: expected {DOTNET_VERSION}, observed {version}"
            )
        environment = _sanitized_environment(
            dotnet_path=dotnet_path,
            cli_home=cli_home,
            packages_path=packages_root,
            temporary_path=tmp_root,
        )
        properties = _isolation_properties(
            source_root=source_root,
            packages_path=packages_root,
            user_extensions_path=user_extensions,
        )
        game_project = generated_root / "CandidateGame.csproj"
        story_project = generated_root / "CandidateStory.csproj"
        for project, label in ((game_project, "Game"), (story_project, "story")):
            run_command(
                [
                    str(dotnet_path), "restore", str(project),
                    "--configfile", str(nuget_config), "--no-cache", *properties,
                ],
                cwd=source_root,
                env=environment,
                label=f"isolated {label} restore",
            )
        run_command(
            [
                str(dotnet_path), "build", str(game_project), "-c", CONFIGURATION,
                "--no-restore", "-o", str(game_output), *properties,
            ],
            cwd=source_root,
            env=environment,
            label="isolated realtime Game build",
        )
        run_command(
            [
                str(dotnet_path), "build", str(story_project), "-c", "Release",
                "--no-restore", "-o", str(story_output), *properties,
            ],
            cwd=source_root,
            env=environment,
            label="isolated story build",
        )
        output_paths = {
            "managed/Gridworks.Game.dll": _single_file(
                game_output, "Gridworks.Game.dll", "Game assembly"
            ),
            "managed/Gridworks.Core.dll": game_output / "Gridworks.Core.dll",
            "managed/Gridworks.Game.deps.json": game_output / "Gridworks.Game.deps.json",
            "managed/Gridworks.Game.runtimeconfig.json": (
                game_output / "Gridworks.Game.runtimeconfig.json"
            ),
        }
        for path, candidate in output_paths.items():
            if not candidate.is_file():
                raise CandidateAuthorityError(f"fresh managed output is missing: {path}")
        story_assembly = _single_file(
            story_output, "Gridworks.CandidateStory.dll", "story assembly"
        )
        story_command = [str(dotnet_path), str(story_assembly), "--story-manifest"]
        first_story = run_command(
            story_command,
            cwd=source_root,
            env=environment,
            label="first exact story emission",
        )
        second_story = run_command(
            story_command,
            cwd=source_root,
            env=environment,
            label="second exact story emission",
        )
        if first_story != second_story:
            raise CandidateAuthorityError("story output changed across fresh processes")
        validate_story_manifest(first_story)
        stored_story = source.blobs[STORED_STORY_MANIFEST_PATH].data
        if first_story != stored_story:
            raise CandidateAuthorityError("fresh story output differs from bound UX-R0 evidence")
        bindings = {
            path: FileBinding.open(candidate, "MANAGED_DEBUG_OUTPUT", path)
            for path, candidate in output_paths.items()
        }
        closure_source_paths = (
            {PROJECT_GODOT_PATH, "game/default_bus_layout.tres", "game/RealtimeTheme.tres"}
            | set(RUNTIME_SCENE_FILES)
            | set(SCENE_ATTACHED_SCRIPTS)
            | {
                f"{path}.uid"
                for path in SCENE_ATTACHED_SCRIPTS
                if f"{path}.uid" in source.blobs
            }
        )
        if len(closure_source_paths) != 35:
            raise CandidateAuthorityError(
                "R2 Godot executable source closure must contain exactly 35 files"
            )
        runtime_bindings: dict[str, FileBinding] = {}
        for source_path in sorted(closure_source_paths):
            package_path = source_path.removeprefix("game/")
            destination = runtime_package_root / package_path
            write_exclusive(destination, source.blobs[source_path].data)
            runtime_bindings[package_path] = FileBinding.open(
                destination,
                "GODOT_PROJECT_SOURCE_FILE",
                f"runtime package source {package_path}",
            )
        managed_package_directory = ".godot/mono/temp/bin/Debug"
        for output_path, binding in sorted(bindings.items()):
            package_path = f"{managed_package_directory}/{Path(output_path).name}"
            _resolved, data = read_regular_file(binding.path, output_path)
            destination = runtime_package_root / package_path
            write_exclusive(destination, data)
            runtime_bindings[package_path] = FileBinding.open(
                destination,
                "MANAGED_DEBUG_OUTPUT",
                f"runtime package managed output {package_path}",
            )
        observed_package_paths = sorted(
            path.relative_to(runtime_package_root).as_posix()
            for path in runtime_package_root.rglob("*")
            if path.is_file()
        )
        if observed_package_paths != sorted(runtime_bindings) or len(runtime_bindings) != 39:
            raise CandidateAuthorityError(
                "materialized R2 runtime package tree differs from exact 39-file authority"
            )
        result = IsolatedBuild(
            source=source,
            dotnet_rows=dotnet_rows,
            package_rows=sorted(package_rows, key=lambda row: row["path"]),
            generated_rows=generated_rows,
            output_bindings=bindings,
            runtime_package_bindings=runtime_bindings,
            runtime_package_root=runtime_package_root,
            story_bytes=first_story,
        )
        result.verify_outputs()
        if bind_dotnet_authority(dotnet_path) != dotnet_rows:
            raise CandidateAuthorityError(
                "bound dotnet wrapper or host changed during managed build"
            )
        yield result


def default_godot_app(repository_root: Path) -> Path:
    override = os.environ.get("GRIDWORKS_GODOT_APP_ROOT")
    if override:
        return Path(override)
    return repository_root / ".tools" / "godot-4.7.1" / "Godot_mono.app"


def bind_engine_tree(app_root: Path) -> tuple[list[dict[str, Any]], str]:
    try:
        root = app_root.resolve(strict=True)
    except OSError as error:
        raise CandidateAuthorityError(f"Godot app cannot be opened: {error}") from error
    if not root.is_dir() or app_root.is_symlink():
        raise CandidateAuthorityError("Godot app root must be a non-symlink directory")
    rows: list[dict[str, Any]] = []
    for path in sorted(root.rglob("*")):
        relative = path.relative_to(root).as_posix()
        validate_git_path(relative)
        if path.is_symlink():
            raise CandidateAuthorityError(f"Godot app contains a symlink: {relative}")
        if path.is_dir():
            continue
        _resolved, data = read_regular_file(path, f"Godot app file {relative}")
        rows.append({
            "path": relative,
            "role": "GODOT_HOST_FILE",
            "rawSha256": sha256_bytes(data),
            "byteLength": len(data),
        })
    if len(rows) != 153:
        raise CandidateAuthorityError(
            f"Godot app tree drift: expected 153 files, observed {len(rows)}"
        )
    executable = root / "Contents" / "MacOS" / "Godot"
    version = run_command(
        [str(executable), "--version"],
        cwd=root,
        label="Godot version",
    ).decode("utf-8").strip()
    if version != GODOT_VERSION_OUTPUT:
        raise CandidateAuthorityError(
            f"Godot version drift: expected {GODOT_VERSION_OUTPUT}, observed {version}"
        )
    return rows, canonical_sha256(rows)


def godot_script_path_authority(build: IsolatedBuild) -> dict[str, Any]:
    binding = build.output_bindings["managed/Gridworks.Game.dll"]
    _resolved, assembly = read_regular_file(
        binding.path,
        "managed Game assembly script-path authority",
    )
    resource_paths = sorted(
        "res://" + path.removeprefix("game/")
        for path in SCENE_ATTACHED_SCRIPTS
    )
    missing = [
        resource_path
        for resource_path in resource_paths
        if resource_path.encode("utf-8") not in assembly
    ]
    if missing:
        raise CandidateAuthorityError(
            "managed Game assembly is missing exact Godot script paths: "
            + ", ".join(missing)
        )
    if b"res://../" in assembly:
        raise CandidateAuthorityError(
            "managed Game assembly contains an escaped Godot script path"
        )
    return {
        "godotProjectDir": "$CANDIDATE_SOURCE_ROOT/game",
        "pathEncoding": "GODOT_SCRIPT_PATH_ATTRIBUTE_RES_URI",
        "sceneAttachedScriptCount": len(resource_paths),
        "resourcePaths": resource_paths,
        "escapedResourcePathCount": 0,
    }


def _runtime_package_rows(
    root: Path,
    bindings: dict[str, FileBinding],
    *,
    allowed_empty_directories: Sequence[str] = (),
) -> list[dict[str, Any]]:
    observed_paths: list[str] = []
    observed_directories: list[str] = []
    for path in sorted(root.rglob("*")):
        relative = path.relative_to(root).as_posix()
        validate_git_path(relative)
        if path.is_symlink():
            raise CandidateAuthorityError(
                f"headless probe package contains a symlink: {relative}"
            )
        if path.is_dir():
            observed_directories.append(relative)
            continue
        observed_paths.append(relative)
    expected_directories = sorted({
        parent.as_posix()
        for relative in bindings
        for parent in PurePosixPath(relative).parents
        if parent.as_posix() != "."
    } | set(allowed_empty_directories))
    if observed_directories != expected_directories:
        raise CandidateAuthorityError(
            "headless probe changed the exact candidate package directory set"
        )
    if observed_paths != sorted(bindings):
        raise CandidateAuthorityError(
            "headless probe changed the exact candidate package file set"
        )
    rows: list[dict[str, Any]] = []
    for relative in observed_paths:
        _resolved, data = read_regular_file(
            root / relative,
            f"headless probe package file {relative}",
        )
        binding = bindings[relative]
        row = {
            "path": relative,
            "role": binding.role,
            "rawSha256": sha256_bytes(data),
            "byteLength": len(data),
        }
        if row != binding.row(relative):
            raise CandidateAuthorityError(
                f"headless probe mutated bound candidate file: {relative}"
            )
        rows.append(row)
    return rows


def _materialize_probe_package(build: IsolatedBuild, root: Path) -> list[dict[str, Any]]:
    for relative, binding in sorted(build.runtime_package_bindings.items()):
        _resolved, data = read_regular_file(
            binding.path,
            f"bound candidate package file {relative}",
        )
        write_exclusive(root / relative, data)
    return _runtime_package_rows(root, build.runtime_package_bindings)


def _probe_environment(dotnet_path: Path, temporary_root: Path) -> dict[str, str]:
    environment = {
        key: os.environ[key]
        for key in ("LANG", "LC_ALL")
        if key in os.environ
    }
    install_root = dotnet_path.parent.parent
    dotnet_root = install_root / "libexec"
    if not dotnet_root.is_dir():
        raise CandidateAuthorityError("headless probe dotnet root is unavailable")
    environment.update({
        "DOTNET_ROOT": str(dotnet_root),
        "DOTNET_MULTILEVEL_LOOKUP": "0",
        "PATH": os.pathsep.join((str(dotnet_path.parent), "/usr/bin", "/bin")),
        "TMPDIR": str(temporary_root),
    })
    return environment


def _execute_headless_probe(
    *,
    executable: Path,
    package_root: Path,
    log_path: Path,
    user_arguments: Sequence[str],
    environment: dict[str, str],
) -> subprocess.CompletedProcess[bytes]:
    command = [
        str(executable),
        "--headless",
        "--path",
        str(package_root),
        "--scene",
        CHECKPOINT_SCENE,
        "--log-file",
        str(log_path),
        "--",
        *user_arguments,
    ]
    try:
        return subprocess.run(
            command,
            cwd=log_path.parent,
            env=environment,
            check=False,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=30,
        )
    except (OSError, subprocess.SubprocessError) as error:
        raise CandidateAuthorityError(
            f"headless checkpoint probe could not execute: {error}"
        ) from error


def _decode_probe_output(completed: subprocess.CompletedProcess[bytes]) -> tuple[str, str]:
    try:
        return (
            completed.stdout.decode("utf-8", errors="strict"),
            completed.stderr.decode("utf-8", errors="strict"),
        )
    except UnicodeError as error:
        raise CandidateAuthorityError("headless probe output is not UTF-8") from error


def _parse_claim_fields(line: str, first_token: str, label: str) -> dict[str, str]:
    tokens = line.split(" ")
    if not tokens or tokens[0] != first_token:
        raise CandidateAuthorityError(f"{label} has the wrong claim label")
    fields: dict[str, str] = {}
    for token in tokens[1:]:
        if "=" not in token:
            raise CandidateAuthorityError(f"{label} has an untyped token")
        key, value = token.split("=", 1)
        if not key or not value or key in fields:
            raise CandidateAuthorityError(f"{label} has an invalid field")
        fields[key] = value
    return fields


def _format_claim_line(
    claim_label: str,
    fields: dict[str, str],
    field_order: Sequence[str],
) -> str:
    if set(fields) != set(field_order):
        raise CandidateAuthorityError(
            f"cannot format {claim_label}: exact field set drift"
        )
    return " ".join(
        (claim_label, *(f"{key}={fields[key]}" for key in field_order))
    )


def _expected_ready_fields(build: IsolatedBuild, checkpoint: dict[str, Any]) -> dict[str, str]:
    presentation = CHECKPOINT_PRESENTATION_EXPECTATIONS[checkpoint["checkpointId"]]
    return {
        "id": checkpoint["checkpointId"],
        **FIXTURE_RUNTIME_FACTS,
        "campaignSourceHash": sha256_bytes(
            build.source.blobs[FIXTURE_CAMPAIGN_PATH].data
        ).removeprefix(SHA256_PREFIX),
        "worldSourceHash": sha256_bytes(
            build.source.blobs[FIXTURE_WORLD_PATH].data
        ).removeprefix(SHA256_PREFIX),
        "replaySchema": checkpoint["commandReplaySchema"],
        "replayHash": checkpoint["commandReplaySha256"].removeprefix(SHA256_PREFIX),
        "commandCount": str(checkpoint["commandCount"]),
        "startMinute": str(checkpoint["startMinute"]),
        "startHash": checkpoint["startCanonicalStateSha256"].removeprefix(
            SHA256_PREFIX
        ),
        "expectedEndHash": checkpoint["expectedEndCanonicalStateSha256"].removeprefix(
            SHA256_PREFIX
        ),
        "construction": presentation["construction"],
        "activeEvents": "0",
        "activeDuty": "none",
        "thermalAssets": "26",
        "selection": "none",
        "anchor": "none",
        "surface": "World",
        "tool": "Inspect",
        "simulation": "PlayerPaused",
        "allowedInput": checkpoint["allowedInput"],
        "allowedFrames": (
            f"{checkpoint['allowedFrameCount']}/{checkpoint['framesPerSecond']}"
        ),
    }


def _expected_pass_fields(checkpoint: dict[str, Any]) -> dict[str, str]:
    presentation = CHECKPOINT_PRESENTATION_EXPECTATIONS[checkpoint["checkpointId"]]
    return {
        "startMinute": str(checkpoint["startMinute"]),
        "startHash": checkpoint["startCanonicalStateSha256"].removeprefix(
            SHA256_PREFIX
        ),
        "replayHash": checkpoint["commandReplaySha256"].removeprefix(SHA256_PREFIX),
        "endMinute": str(presentation["endMinute"]),
        "endHash": checkpoint["expectedEndCanonicalStateSha256"].removeprefix(
            SHA256_PREFIX
        ),
        "presentationRevision": str(presentation["presentationRevision"]),
        "renderedAssets": str(presentation["renderedAssets"]),
        "hudClock": presentation["hudClock"],
    }


def _typed_ready_claim(fields: dict[str, str]) -> dict[str, Any]:
    return {
        "claimLabel": "TARGETED_LIVE_CHECKPOINT_READY",
        "checkpointId": fields["id"],
        "campaignSchema": fields["campaignSchema"],
        "campaignId": fields["campaignId"],
        "campaignSourceSha256": SHA256_PREFIX + fields["campaignSourceHash"],
        "campaignDefinitionSha256": SHA256_PREFIX + fields["campaignDefinitionHash"],
        "worldSchema": fields["worldSchema"],
        "worldId": fields["worldId"],
        "worldSourceSha256": SHA256_PREFIX + fields["worldSourceHash"],
        "worldDefinitionSha256": SHA256_PREFIX + fields["worldDefinitionHash"],
        "commandReplaySchema": fields["replaySchema"],
        "commandReplaySha256": SHA256_PREFIX + fields["replayHash"],
        "commandCount": int(fields["commandCount"]),
        "startMinute": int(fields["startMinute"]),
        "startCanonicalStateSha256": SHA256_PREFIX + fields["startHash"],
        "expectedEndCanonicalStateSha256": SHA256_PREFIX + fields["expectedEndHash"],
        "construction": fields["construction"],
        "activeEventCount": int(fields["activeEvents"]),
        "activeDuty": fields["activeDuty"],
        "thermalAssetCount": int(fields["thermalAssets"]),
        "selection": fields["selection"],
        "timelineAnchor": fields["anchor"],
        "surface": fields["surface"],
        "tool": fields["tool"],
        "simulation": fields["simulation"],
        "allowedInput": fields["allowedInput"],
        "allowedFrameCount": int(fields["allowedFrames"].split("/", 1)[0]),
        "framesPerSecond": int(fields["allowedFrames"].split("/", 1)[1]),
    }


def _typed_pass_claim(
    claim_label: str,
    fields: dict[str, str],
) -> dict[str, Any]:
    return {
        "claimLabel": claim_label,
        "startMinute": int(fields["startMinute"]),
        "startCanonicalStateSha256": SHA256_PREFIX + fields["startHash"],
        "commandReplaySha256": SHA256_PREFIX + fields["replayHash"],
        "endMinute": int(fields["endMinute"]),
        "endCanonicalStateSha256": SHA256_PREFIX + fields["endHash"],
        "presentationRevision": int(fields["presentationRevision"]),
        "renderedAssetCount": int(fields["renderedAssets"]),
        "hudClock": fields["hudClock"],
    }


def _positive_checkpoint_probe(
    build: IsolatedBuild,
    executable: Path,
    executable_binding: FileBinding,
    dotnet_path: Path,
    checkpoint: dict[str, Any],
) -> dict[str, Any]:
    with tempfile.TemporaryDirectory(prefix="gridworks-headless-positive-") as raw:
        probe_root = Path(raw).resolve(strict=True)
        package_root = probe_root / "game"
        package_root.mkdir()
        before_rows = _materialize_probe_package(build, package_root)
        environment = _probe_environment(
            dotnet_path,
            probe_root,
        )
        completed = _execute_headless_probe(
            executable=executable,
            package_root=package_root,
            log_path=probe_root / "godot.log",
            user_arguments=(f"--checkpoint={checkpoint['checkpointId']}",),
            environment=environment,
        )
        stdout, stderr = _decode_probe_output(completed)
        if completed.returncode != 0:
            raise CandidateAuthorityError(
                f"headless checkpoint {checkpoint['checkpointId']} exited "
                f"{completed.returncode}: {(stderr or stdout).strip()}"
            )
        combined_lines = stdout.splitlines() + stderr.splitlines()
        ready_lines = [
            line for line in combined_lines
            if line.startswith("TARGETED_LIVE_CHECKPOINT_READY ")
        ]
        pass_lines = [
            line for line in combined_lines
            if line.startswith("TARGETED_LIVE_CHECKPOINT_PASS:")
        ]
        unexpected_typed = [
            line for line in combined_lines
            if line.startswith(("TARGETED_", "FULL_FLOW_"))
            and line not in ready_lines
            and line not in pass_lines
        ]
        error_lines = [line for line in combined_lines if line.startswith("ERROR:")]
        if (
            len(ready_lines) != 1
            or len(pass_lines) != 1
            or unexpected_typed
            or error_lines
            or stderr
        ):
            raise CandidateAuthorityError(
                f"headless checkpoint {checkpoint['checkpointId']} emitted an invalid "
                "claim or error stream"
            )
        ready = _parse_claim_fields(
            ready_lines[0],
            "TARGETED_LIVE_CHECKPOINT_READY",
            "checkpoint ready claim",
        )
        pass_claim_label = checkpoint["claimLabel"]
        passed = _parse_claim_fields(
            pass_lines[0],
            pass_claim_label,
            "checkpoint pass claim",
        )
        expected_ready_fields = _expected_ready_fields(build, checkpoint)
        expected_ready_line = _format_claim_line(
            "TARGETED_LIVE_CHECKPOINT_READY",
            expected_ready_fields,
            READY_FIELD_ORDER,
        )
        if ready != expected_ready_fields or ready_lines[0] != expected_ready_line:
            raise CandidateAuthorityError(
                f"headless checkpoint {checkpoint['checkpointId']} ready facts drift"
            )
        expected_pass_fields = _expected_pass_fields(checkpoint)
        expected_pass_line = _format_claim_line(
            pass_claim_label,
            expected_pass_fields,
            PASS_FIELD_ORDER,
        )
        if passed != expected_pass_fields or pass_lines[0] != expected_pass_line:
            raise CandidateAuthorityError(
                f"headless checkpoint {checkpoint['checkpointId']} pass facts drift"
            )
        _log_path, log_bytes = read_regular_file(
            probe_root / "godot.log",
            "positive headless probe log",
        )
        expected_stdout = (
            GODOT_BANNER + "\n\n" + ready_lines[0] + "\n" + pass_lines[0] + "\n"
        ).encode("utf-8")
        if completed.stdout != expected_stdout or completed.stderr != b"":
            raise CandidateAuthorityError(
                f"headless checkpoint {checkpoint['checkpointId']} emitted "
                "unexpected process output"
            )
        if log_bytes != expected_stdout:
            raise CandidateAuthorityError(
                f"headless checkpoint {checkpoint['checkpointId']} log differs "
                "from exact stdout"
            )
        log_text = log_bytes.decode("utf-8", errors="strict")
        after_rows = _runtime_package_rows(
            package_root,
            build.runtime_package_bindings,
            allowed_empty_directories=GODOT_EPHEMERAL_EMPTY_DIRECTORY_SIDE_EFFECTS,
        )
        if after_rows != before_rows:
            raise CandidateAuthorityError("headless checkpoint package changed after launch")
        executable_binding.verify("Godot executable after positive headless probe")
        return {
            "probeId": f"HEADLESS_{checkpoint['checkpointId']}",
            "checkpointId": checkpoint["checkpointId"],
            "userArguments": [f"--checkpoint={checkpoint['checkpointId']}"],
            "inputFileCount": len(before_rows),
            "inputTreeSha256": canonical_sha256(before_rows),
            "inputCacheState": "COLD_NO_EDITOR_OR_UID_CACHE",
            "exitCode": completed.returncode,
            "readyClaimOccurrenceCount": 1,
            "passClaimOccurrenceCount": 1,
            "unexpectedTypedClaimCount": 0,
            "readyClaimRawSha256": sha256_bytes(ready_lines[0].encode("utf-8")),
            "passClaimRawSha256": sha256_bytes(pass_lines[0].encode("utf-8")),
            "stdoutRawSha256": sha256_bytes(completed.stdout),
            "stdoutByteLength": len(completed.stdout),
            "stdoutUtf8": stdout,
            "stderrRawSha256": sha256_bytes(completed.stderr),
            "stderrByteLength": len(completed.stderr),
            "stderrUtf8": stderr,
            "logRawSha256": sha256_bytes(log_bytes),
            "logByteLength": len(log_bytes),
            "logUtf8": log_text,
            "observedEphemeralEmptyDirectorySideEffects": list(
                GODOT_EPHEMERAL_EMPTY_DIRECTORY_SIDE_EFFECTS
            ),
            "readyClaim": _typed_ready_claim(ready),
            "passClaim": _typed_pass_claim(pass_claim_label, passed),
            "boundFileByteMutationCount": 0,
            "nativePresentationObserved": False,
            "scoreBearingEvidence": False,
        }


def _argument_rejection_probe(
    build: IsolatedBuild,
    executable: Path,
    executable_binding: FileBinding,
    dotnet_path: Path,
    *,
    probe_id: str,
    user_arguments: Sequence[str],
    expected_message: str,
    expected_stderr_raw_sha256: str,
) -> dict[str, Any]:
    with tempfile.TemporaryDirectory(prefix="gridworks-headless-rejection-") as raw:
        probe_root = Path(raw).resolve(strict=True)
        package_root = probe_root / "game"
        package_root.mkdir()
        before_rows = _materialize_probe_package(build, package_root)
        environment = _probe_environment(
            dotnet_path,
            probe_root,
        )
        completed = _execute_headless_probe(
            executable=executable,
            package_root=package_root,
            log_path=probe_root / "godot.log",
            user_arguments=user_arguments,
            environment=environment,
        )
        stdout, stderr = _decode_probe_output(completed)
        combined_lines = stdout.splitlines() + stderr.splitlines()
        expected_line = "ERROR: TARGETED_LIVE_CHECKPOINT_ARGUMENT_FAIL " + expected_message
        error_lines = [line for line in combined_lines if line.startswith("ERROR:")]
        typed_success = [
            line for line in combined_lines
            if line.startswith((
                "TARGETED_LIVE_CHECKPOINT_READY",
                "TARGETED_LIVE_CHECKPOINT_PASS:",
                "FULL_FLOW_",
            ))
        ]
        expected_stdout = (GODOT_BANNER + "\n\n").encode("utf-8")
        forbidden_error_markers = (
            b"SCRIPT ERROR",
            b"Parse Error",
            b"Failed to load",
            b"Cannot instantiate",
            b"CRASH",
        )
        _log_path, log_bytes = read_regular_file(
            probe_root / "godot.log",
            "argument rejection headless probe log",
        )
        if (
            completed.returncode != 2
            or completed.stdout != expected_stdout
            or sha256_bytes(completed.stderr) != expected_stderr_raw_sha256
            or not completed.stderr.startswith((expected_line + "\n").encode("utf-8"))
            or error_lines != [expected_line]
            or typed_success
            or any(marker in completed.stderr for marker in forbidden_error_markers)
            or log_bytes != completed.stdout + completed.stderr
        ):
            raise CandidateAuthorityError(
                f"headless argument rejection {probe_id} did not fail closed"
            )
        try:
            log_text = log_bytes.decode("utf-8", errors="strict")
        except UnicodeError as error:
            raise CandidateAuthorityError(
                f"headless argument rejection {probe_id} log is not UTF-8"
            ) from error
        after_rows = _runtime_package_rows(
            package_root,
            build.runtime_package_bindings,
            allowed_empty_directories=GODOT_EPHEMERAL_EMPTY_DIRECTORY_SIDE_EFFECTS,
        )
        if after_rows != before_rows:
            raise CandidateAuthorityError("argument rejection package changed after launch")
        executable_binding.verify("Godot executable after rejection headless probe")
        return {
            "probeId": probe_id,
            "userArguments": list(user_arguments),
            "inputFileCount": len(before_rows),
            "inputTreeSha256": canonical_sha256(before_rows),
            "inputCacheState": "COLD_NO_EDITOR_OR_UID_CACHE",
            "exitCode": completed.returncode,
            "rejectionCode": "TARGETED_LIVE_CHECKPOINT_ARGUMENT_FAIL",
            "rejectionMessage": expected_message,
            "rejectionLineRawSha256": sha256_bytes(expected_line.encode("utf-8")),
            "stdoutRawSha256": sha256_bytes(completed.stdout),
            "stdoutByteLength": len(completed.stdout),
            "stdoutUtf8": stdout,
            "stderrRawSha256": sha256_bytes(completed.stderr),
            "stderrByteLength": len(completed.stderr),
            "stderrUtf8": stderr,
            "logRawSha256": sha256_bytes(log_bytes),
            "logByteLength": len(log_bytes),
            "logUtf8": log_text,
            "observedEphemeralEmptyDirectorySideEffects": list(
                GODOT_EPHEMERAL_EMPTY_DIRECTORY_SIDE_EFFECTS
            ),
            "readyClaimOccurrenceCount": 0,
            "passClaimOccurrenceCount": 0,
            "fullFlowClaimOccurrenceCount": 0,
            "boundFileByteMutationCount": 0,
            "scoreBearingEvidence": False,
        }


def run_headless_execution_authority(
    build: IsolatedBuild,
    app_root: Path,
    engine_rows: Sequence[dict[str, Any]],
    engine_tree_sha256: str,
) -> dict[str, Any]:
    root = app_root.resolve(strict=True)
    executable = root / "Contents" / "MacOS" / "Godot"
    executable_binding = FileBinding.open(
        executable,
        "GODOT_HOST_FILE",
        "Godot executable for headless probes",
    )
    executable_row = next(
        (row for row in engine_rows if row["path"] == "Contents/MacOS/Godot"),
        None,
    )
    if executable_row != executable_binding.row("Contents/MacOS/Godot"):
        raise CandidateAuthorityError(
            "headless probe Godot executable differs from engine authority"
        )
    dotnet_raw = shutil.which("dotnet")
    if dotnet_raw is None:
        raise CandidateAuthorityError("headless probe dotnet executable is unavailable")
    dotnet_path = Path(dotnet_raw).resolve(strict=True)
    if bind_dotnet_authority(dotnet_path) != build.dotnet_rows:
        raise CandidateAuthorityError("headless probe dotnet authority drift")
    package_rows = [
        build.runtime_package_bindings[path].row(path)
        for path in sorted(build.runtime_package_bindings)
    ]
    package_sha256 = canonical_sha256(package_rows)
    positives = [
        _positive_checkpoint_probe(
            build,
            executable,
            executable_binding,
            dotnet_path,
            checkpoint,
        )
        for checkpoint in CHECKPOINTS
    ]
    rejections = [
        _argument_rejection_probe(
            build,
            executable,
            executable_binding,
            dotnet_path,
            probe_id=value["probeId"],
            user_arguments=value["userArguments"],
            expected_message=value["message"],
            expected_stderr_raw_sha256=value["stderrRawSha256"],
        )
        for value in ARGUMENT_REJECTION_CASES
    ]
    executable_binding.verify("Godot executable after all headless probes")
    observed_engine_rows, observed_engine_sha256 = bind_engine_tree(root)
    if (
        observed_engine_rows != list(engine_rows)
        or observed_engine_sha256 != engine_tree_sha256
    ):
        raise CandidateAuthorityError("Godot engine tree changed during headless probes")
    if bind_dotnet_authority(dotnet_path) != build.dotnet_rows:
        raise CandidateAuthorityError(
            "bound dotnet wrapper or host changed during headless probes"
        )
    build.verify_outputs()
    authority: dict[str, Any] = {
        "schemaVersion": HEADLESS_EXECUTION_SCHEMA,
        "executionKind": "EDITOR_HEADLESS_DIAGNOSTIC_ONLY",
        "commandTemplate": list(HEADLESS_COMMAND_TEMPLATE),
        "engineFileTreeSha256": engine_tree_sha256,
        "inputPackageTreeSha256": package_sha256,
        "freshProcessPerProbe": True,
        "freshExactPackageCopyPerProbe": True,
        "ephemeralProbeFilesystemOutputsExcludedFromPackageFileAuthority": True,
        "expectedEphemeralEmptyDirectorySideEffects": list(
            GODOT_EPHEMERAL_EMPTY_DIRECTORY_SIDE_EFFECTS
        ),
        "dotnetSdkVersion": DOTNET_VERSION,
        "hostRuntimeBinding": "LOCAL_SYSTEM_DOTNET_PARTIAL_TWO_FILE_AUTHORITY",
        "positiveCheckpointProbes": positives,
        "argumentRejectionProbes": rejections,
        "nativePresentationObserved": False,
        "scoreBearingEvidence": False,
    }
    authority["executionSha256"] = canonical_sha256(authority)
    return authority


def load_policy() -> tuple[dict[str, Any], bytes]:
    _resolved, data = read_regular_file(POLICY_PATH, "realtime candidate policy")
    if sha256_bytes(data) != EXPECTED_POLICY_RAW_SHA256:
        raise CandidateAuthorityError("realtime candidate policy raw-byte drift")
    policy = strict_json_bytes(data, "realtime candidate policy")
    return policy, data


def validate_policy_authority(
    policy: dict[str, Any],
    policy_bytes: bytes | None = None,
) -> None:
    expected_policy, expected_bytes = load_policy()
    if not strict_typed_equal(policy, expected_policy):
        raise CandidateAuthorityError(
            "realtime candidate policy object differs from pinned raw authority"
        )
    if policy_bytes is None:
        return
    if policy_bytes != expected_bytes:
        raise CandidateAuthorityError(
            "realtime candidate policy bytes differ from pinned raw authority"
        )
    parsed_policy = strict_json_bytes(
        policy_bytes,
        "submitted realtime candidate policy",
    )
    if not strict_typed_equal(parsed_policy, policy):
        raise CandidateAuthorityError(
            "realtime candidate policy bytes and object disagree"
        )


def route_profiles() -> list[dict[str, Any]]:
    return [
        {
            "profileId": "INTERACTIVE_NONDEFAULT_R2",
            "routeKind": "INTERACTIVE_R2_SLICE",
            "availability": "AVAILABLE_DIAGNOSTIC_ONLY",
            "scene": INTERACTIVE_SCENE,
            "sceneOverrideRequired": True,
            "arguments": [],
            "runtimeCoverage": "FIRST_LIGHT_1_CHAPTER_3_EVENTS",
            "allowedClaimPrefix": "INTERACTIVE_R2_SLICE_OBSERVATION:",
        },
        {
            "profileId": "TARGETED_CHECKPOINT_DEBUG",
            "routeKind": "TARGETED_CHECKPOINT",
            "availability": "AVAILABLE_DIAGNOSTIC_ONLY",
            "scene": CHECKPOINT_SCENE,
            "sceneOverrideRequired": True,
            "argumentTemplate": "--checkpoint=<CANONICAL_ID>",
            "checkpoints": [dict(value) for value in CHECKPOINTS],
            "runtimeCoverage": "EXACT_BOUNDED_CHECKPOINT_ONLY",
            "allowedClaimPrefix": "TARGETED_LIVE_CHECKPOINT_PASS:",
        },
        {
            "profileId": "FULL_FLOW_EXCEPTION",
            "routeKind": "FULL_FLOW_EXCEPTION",
            "availability": "UNAVAILABLE_NOT_IMPLEMENTED",
            "scene": None,
            "sceneOverrideRequired": False,
            "arguments": None,
            "runtimeCoverage": "NONE",
            "allowedClaimPrefix": None,
            "requiredFreshBoundaries": [
                "DEFAULT_PACKAGE_ONBOARDING",
                "SAVE_TO_FRESH_PROCESS_RESTORE",
                "ACCUMULATED_EIGHT_CHAPTER_COMPLETION",
                "FINALE_AND_EPILOGUE",
            ],
            "forbiddenSubstitutes": [
                "SCENE_OVERRIDE",
                "FIXTURE_SEED",
                "CHECKPOINT_RUNNER",
                "TARGETED_LIVE_CHECKPOINT_PASS",
                "COMMERCIAL_MAIN_V2",
            ],
        },
    ]


def validate_headless_execution_authority(
    authority: dict[str, Any],
    build: IsolatedBuild,
    engine_tree_sha256: str,
) -> None:
    require_exact_keys(authority, HEADLESS_AUTHORITY_KEYS, "headless authority")
    unsigned = dict(authority)
    execution_sha256 = unsigned.pop("executionSha256", None)
    package_rows = [
        build.runtime_package_bindings[path].row(path)
        for path in sorted(build.runtime_package_bindings)
    ]
    positives = authority.get("positiveCheckpointProbes")
    rejections = authority.get("argumentRejectionProbes")
    checks = [
        execution_sha256 == canonical_sha256(unsigned),
        authority.get("schemaVersion") == HEADLESS_EXECUTION_SCHEMA,
        authority.get("executionKind") == "EDITOR_HEADLESS_DIAGNOSTIC_ONLY",
        authority.get("commandTemplate") == list(HEADLESS_COMMAND_TEMPLATE),
        authority.get("engineFileTreeSha256") == engine_tree_sha256,
        authority.get("inputPackageTreeSha256") == canonical_sha256(package_rows),
        authority.get("freshProcessPerProbe") is True,
        authority.get("freshExactPackageCopyPerProbe") is True,
        authority.get(
            "ephemeralProbeFilesystemOutputsExcludedFromPackageFileAuthority"
        ) is True,
        authority.get("expectedEphemeralEmptyDirectorySideEffects")
        == list(GODOT_EPHEMERAL_EMPTY_DIRECTORY_SIDE_EFFECTS),
        authority.get("dotnetSdkVersion") == DOTNET_VERSION,
        authority.get("hostRuntimeBinding")
        == "LOCAL_SYSTEM_DOTNET_PARTIAL_TWO_FILE_AUTHORITY",
        authority.get("nativePresentationObserved") is False,
        authority.get("scoreBearingEvidence") is False,
        isinstance(positives, list) and len(positives) == 2,
        isinstance(rejections, list) and len(rejections) == 3,
    ]
    if not all(checks):
        raise CandidateAuthorityError("headless execution authority envelope drift")
    assert isinstance(positives, list)
    by_checkpoint = {
        probe.get("checkpointId"): probe
        for probe in positives
        if isinstance(probe, dict)
    }
    if set(by_checkpoint) != {value["checkpointId"] for value in CHECKPOINTS}:
        raise CandidateAuthorityError("headless positive checkpoint set drift")
    for checkpoint in CHECKPOINTS:
        probe = by_checkpoint[checkpoint["checkpointId"]]
        require_exact_keys(
            probe,
            POSITIVE_PROBE_KEYS,
            f"positive probe {checkpoint['checkpointId']}",
        )
        if not isinstance(probe.get("readyClaim"), dict) or not isinstance(
            probe.get("passClaim"), dict
        ):
            raise CandidateAuthorityError("headless positive claim object drift")
        require_exact_keys(
            probe["readyClaim"],
            READY_CLAIM_KEYS,
            f"ready claim {checkpoint['checkpointId']}",
        )
        require_exact_keys(
            probe["passClaim"],
            PASS_CLAIM_KEYS,
            f"pass claim {checkpoint['checkpointId']}",
        )
        expected_ready_fields = _expected_ready_fields(build, checkpoint)
        expected_pass_fields = _expected_pass_fields(checkpoint)
        expected_ready_line = _format_claim_line(
            "TARGETED_LIVE_CHECKPOINT_READY",
            expected_ready_fields,
            READY_FIELD_ORDER,
        )
        expected_pass_line = _format_claim_line(
            checkpoint["claimLabel"],
            expected_pass_fields,
            PASS_FIELD_ORDER,
        )
        expected_stdout = (
            GODOT_BANNER
            + "\n\n"
            + expected_ready_line
            + "\n"
            + expected_pass_line
            + "\n"
        ).encode("utf-8")
        expected_ready = _typed_ready_claim(expected_ready_fields)
        expected_pass = _typed_pass_claim(
            checkpoint["claimLabel"],
            expected_pass_fields,
        )
        output_texts = (
            probe.get("stdoutUtf8"),
            probe.get("stderrUtf8"),
            probe.get("logUtf8"),
        )
        if not all(isinstance(value, str) for value in output_texts):
            raise CandidateAuthorityError("headless positive output receipt is not UTF-8 text")
        stdout_bytes = output_texts[0].encode("utf-8")
        stderr_bytes = output_texts[1].encode("utf-8")
        log_bytes = output_texts[2].encode("utf-8")
        if (
            probe.get("probeId") != f"HEADLESS_{checkpoint['checkpointId']}"
            or probe.get("userArguments")
            != [f"--checkpoint={checkpoint['checkpointId']}"]
            or not strict_typed_equal(
                probe.get("inputFileCount"), len(package_rows)
            )
            or probe.get("inputTreeSha256") != canonical_sha256(package_rows)
            or probe.get("inputCacheState") != "COLD_NO_EDITOR_OR_UID_CACHE"
            or probe.get("observedEphemeralEmptyDirectorySideEffects")
            != list(GODOT_EPHEMERAL_EMPTY_DIRECTORY_SIDE_EFFECTS)
            or not strict_typed_equal(probe.get("exitCode"), 0)
            or not strict_typed_equal(
                probe.get("readyClaimOccurrenceCount"), 1
            )
            or not strict_typed_equal(
                probe.get("passClaimOccurrenceCount"), 1
            )
            or not strict_typed_equal(
                probe.get("unexpectedTypedClaimCount"), 0
            )
            or not strict_typed_equal(probe.get("readyClaim"), expected_ready)
            or not strict_typed_equal(probe.get("passClaim"), expected_pass)
            or probe.get("readyClaimRawSha256")
            != sha256_bytes(expected_ready_line.encode("utf-8"))
            or probe.get("passClaimRawSha256")
            != sha256_bytes(expected_pass_line.encode("utf-8"))
            or probe.get("stdoutRawSha256") != sha256_bytes(expected_stdout)
            or not strict_typed_equal(
                probe.get("stdoutByteLength"), len(expected_stdout)
            )
            or stdout_bytes != expected_stdout
            or probe.get("stderrRawSha256") != sha256_bytes(b"")
            or not strict_typed_equal(probe.get("stderrByteLength"), 0)
            or stderr_bytes != b""
            or probe.get("logRawSha256") != sha256_bytes(expected_stdout)
            or not strict_typed_equal(
                probe.get("logByteLength"), len(expected_stdout)
            )
            or log_bytes != expected_stdout
            or not strict_typed_equal(
                probe.get("boundFileByteMutationCount"), 0
            )
            or probe.get("nativePresentationObserved") is not False
            or probe.get("scoreBearingEvidence") is not False
        ):
            raise CandidateAuthorityError(
                f"headless checkpoint authority drift: {checkpoint['checkpointId']}"
            )
    assert isinstance(rejections, list)
    if [probe.get("probeId") for probe in rejections] != [
        value["probeId"] for value in ARGUMENT_REJECTION_CASES
    ]:
        raise CandidateAuthorityError("headless argument rejection set drift")
    for probe, expected_rejection in zip(
        rejections,
        ARGUMENT_REJECTION_CASES,
    ):
        require_exact_keys(
            probe,
            REJECTION_PROBE_KEYS,
            f"rejection probe {probe.get('probeId')}",
        )
        rejection_line = (
            "ERROR: TARGETED_LIVE_CHECKPOINT_ARGUMENT_FAIL "
            + str(probe.get("rejectionMessage"))
        )
        expected_stdout = (GODOT_BANNER + "\n\n").encode("utf-8")
        output_texts = (
            probe.get("stdoutUtf8"),
            probe.get("stderrUtf8"),
            probe.get("logUtf8"),
        )
        if not all(isinstance(value, str) for value in output_texts):
            raise CandidateAuthorityError("headless rejection output receipt is not UTF-8 text")
        stdout_bytes = output_texts[0].encode("utf-8")
        stderr_bytes = output_texts[1].encode("utf-8")
        log_bytes = output_texts[2].encode("utf-8")
        combined_lines = (
            output_texts[0].splitlines() + output_texts[1].splitlines()
        )
        ready_lines = [
            line for line in combined_lines
            if line.startswith("TARGETED_LIVE_CHECKPOINT_READY")
        ]
        pass_lines = [
            line for line in combined_lines
            if line.startswith("TARGETED_LIVE_CHECKPOINT_PASS:")
        ]
        full_flow_lines = [
            line for line in combined_lines
            if line.startswith("FULL_FLOW_")
        ]
        typed_success = [
            line for line in combined_lines
            if line.startswith((
                "TARGETED_LIVE_CHECKPOINT_READY",
                "TARGETED_LIVE_CHECKPOINT_PASS:",
                "FULL_FLOW_",
            ))
        ]
        error_lines = [
            line for line in combined_lines if line.startswith("ERROR:")
        ]
        forbidden_error_markers = (
            b"SCRIPT ERROR",
            b"Parse Error",
            b"Failed to load",
            b"Cannot instantiate",
            b"CRASH",
        )
        if (
            not strict_typed_equal(
                probe.get("inputFileCount"), len(package_rows)
            )
            or probe.get("inputTreeSha256") != canonical_sha256(package_rows)
            or probe.get("inputCacheState") != "COLD_NO_EDITOR_OR_UID_CACHE"
            or probe.get("observedEphemeralEmptyDirectorySideEffects")
            != list(GODOT_EPHEMERAL_EMPTY_DIRECTORY_SIDE_EFFECTS)
            or probe.get("userArguments")
            != list(expected_rejection["userArguments"])
            or not strict_typed_equal(probe.get("exitCode"), 2)
            or probe.get("rejectionCode")
            != "TARGETED_LIVE_CHECKPOINT_ARGUMENT_FAIL"
            or probe.get("rejectionMessage") != expected_rejection["message"]
            or probe.get("rejectionLineRawSha256")
            != sha256_bytes(rejection_line.encode("utf-8"))
            or probe.get("stdoutRawSha256") != sha256_bytes(expected_stdout)
            or not strict_typed_equal(
                probe.get("stdoutByteLength"), len(expected_stdout)
            )
            or stdout_bytes != expected_stdout
            or not stderr_bytes.startswith((rejection_line + "\n").encode("utf-8"))
            or error_lines != [rejection_line]
            or typed_success
            or any(marker in stderr_bytes for marker in forbidden_error_markers)
            or probe.get("stderrRawSha256") != sha256_bytes(stderr_bytes)
            or sha256_bytes(stderr_bytes)
            != expected_rejection["stderrRawSha256"]
            or not strict_typed_equal(
                probe.get("stderrByteLength"), len(stderr_bytes)
            )
            or len(stderr_bytes) <= 0
            or log_bytes != stdout_bytes + stderr_bytes
            or probe.get("logRawSha256") != sha256_bytes(log_bytes)
            or not strict_typed_equal(
                probe.get("logByteLength"), len(log_bytes)
            )
            or not strict_typed_equal(
                probe.get("readyClaimOccurrenceCount"), len(ready_lines)
            )
            or len(ready_lines) != 0
            or not strict_typed_equal(
                probe.get("passClaimOccurrenceCount"), len(pass_lines)
            )
            or len(pass_lines) != 0
            or not strict_typed_equal(
                probe.get("fullFlowClaimOccurrenceCount"), len(full_flow_lines)
            )
            or len(full_flow_lines) != 0
            or not strict_typed_equal(
                probe.get("boundFileByteMutationCount"), 0
            )
            or probe.get("scoreBearingEvidence") is not False
        ):
            raise CandidateAuthorityError(
                f"headless rejection authority drift: {probe.get('probeId')}"
            )


def build_manifest(
    build: IsolatedBuild,
    engine_rows: list[dict[str, Any]],
    engine_tree_sha256: str,
    headless_execution_authority: dict[str, Any],
    policy: dict[str, Any],
    policy_bytes: bytes,
) -> dict[str, Any]:
    validate_policy_authority(policy, policy_bytes)
    validate_story_manifest(build.story_bytes)
    validate_headless_execution_authority(
        headless_execution_authority,
        build,
        engine_tree_sha256,
    )
    export_core_sources = [
        path
        for path in build.source.core_sources
        if path.startswith("src/Gridworks.Core/Release/V2/")
    ]
    if (
        not all(path in build.source.blobs for path in EXPORT_RELEASE_GAME_SOURCES)
        or len(export_core_sources) != 20
    ):
        raise CandidateAuthorityError("negative ExportRelease source authority drift")
    evaluator_producer_authority = bind_evaluator_producer_authority(build.source)
    manifest: dict[str, Any] = {
        "schemaVersion": MANIFEST_SCHEMA,
        "canonicalization": CANONICALIZATION,
        "policySha256": sha256_bytes(policy_bytes),
        "sourceCommit": build.source.source_commit,
        "evaluatorProducerAuthority": evaluator_producer_authority,
        "candidateKind": CANDIDATE_KIND,
        "configuration": CONFIGURATION,
        "officialCommercialUX": False,
        "scoreBearingCaptureAllowed": False,
        "sourceAuthority": {
            "files": build.source.rows,
            "sourceInputsSha256": canonical_sha256(build.source.rows),
        },
        "managedBuild": {
            "configuration": CONFIGURATION,
            "compileCounts": {"game": 60, "core": 67},
            "resourceCount": 9,
            "dotnetSdkVersion": DOTNET_VERSION,
            "dotnetAuthority": {
                "files": build.dotnet_rows,
                "filesSha256": canonical_sha256(build.dotnet_rows),
            },
            "packageInputs": build.package_rows,
            "packageInputsSha256": canonical_sha256(build.package_rows),
            "generatedInputs": build.generated_rows,
            "generatedInputsSha256": canonical_sha256(build.generated_rows),
            "godotScriptPathAuthority": godot_script_path_authority(build),
            "outputs": [
                build.output_bindings[path].row(path)
                for path in sorted(build.output_bindings)
            ],
            "negativeExportAuthority": {
                "configuration": "ExportRelease",
                "gameCompileCount": len(EXPORT_RELEASE_GAME_SOURCES),
                "coreCompileCount": len(export_core_sources),
                "resourceCount": 3,
                "realtimeR2GameCompileCount": 0,
                "realtimeUiGameCompileCount": 0,
                "realtimeV3CoreCompileCount": 0,
                "candidateKind": "NOT_R2_CANDIDATE",
            },
        },
        "engineAuthority": {
            "version": GODOT_VERSION_OUTPUT,
            "fileCount": len(engine_rows),
            "files": engine_rows,
            "fileTreeSha256": engine_tree_sha256,
        },
        "packageAuthority": {
            "packageKind": "EDITOR_NATIVE_PROJECT_TREE",
            "rootName": "game",
            "fileCount": len(build.runtime_package_bindings),
            "files": [
                build.runtime_package_bindings[path].row(path)
                for path in sorted(build.runtime_package_bindings)
            ],
            "treeSha256": canonical_sha256([
                build.runtime_package_bindings[path].row(path)
                for path in sorted(build.runtime_package_bindings)
            ]),
            "materializedFromExactBindings": True,
            "nativeAppBundle": False,
            "publicPackage": False,
        },
        "headlessExecutionAuthority": headless_execution_authority,
        "storyAuthority": {
            "schemaVersion": "gridworks.commercial.story-manifest.v2",
            "partCount": 34,
            "resultCardCount": 11,
            "storyManifestRawSha256": sha256_bytes(build.story_bytes),
            "fullRealtimeWorldPath": FULL_REALTIME_WORLD_PATH,
            "fullRealtimeCampaignPath": FULL_REALTIME_CAMPAIGN_PATH,
            "fullRealtimeChapterCount": 8,
            "fullRealtimeEventCount": 16,
            "fullReleaseV3AttachedToRuntime": False,
        },
        "sceneAuthority": {
            "projectDefaultScene": DEFAULT_SCENE,
            "evaluationTargetScene": INTERACTIVE_SCENE,
            "checkpointRunnerScene": CHECKPOINT_SCENE,
            "evaluationTargetIsDefault": False,
            "runtimeFixtureCoverage": "FIRST_LIGHT_1_CHAPTER_3_EVENTS",
        },
        "routeProfiles": route_profiles(),
        "futureEventStatusBar": {
            "sourcePath": "game/realtime/ui/RealtimeEventRail.cs",
            "scenePath": "game/realtime/ui/RealtimeEventRail.tscn",
            "requiredSignals": list(FUTURE_EVENT_SIGNALS),
            "headlessWiringStatus": "EXACT_PACKAGE_TWO_CHECKPOINT_SCENE_LOAD_PASS",
            "nativeQualityStatus": "NOT_OBSERVED",
        },
        "limitations": [
            "DEFAULT_SCENE_IS_COMMERCIAL_MAIN_V2",
            "EXPORT_RELEASE_EXCLUDES_R2_UI_AND_V3_CORE",
            "R2_RUNTIME_USES_FIRST_LIGHT_FIXTURE_NOT_FULL_RELEASE_V3",
            "FULL_CAMPAIGN_NATIVE_E2E_NOT_IMPLEMENTED",
            "SAVE_RESUME_FINALE_EPILOGUE_NATIVE_NOT_IMPLEMENTED",
            "FUTURE_EVENT_STATUS_BAR_NATIVE_QUALITY_NOT_OBSERVED",
            "MODEL_ULTRA_EXECUTION_RECEIPT_NOT_AVAILABLE",
            "HEADLESS_PROBE_USES_LOCAL_EDITOR_AND_SYSTEM_DOTNET",
            "DOTNET_TOOLCHAIN_TRANSITIVE_CLOSURE_NOT_BOUND",
            "SCORE_BEARING_CAPTURE_FORBIDDEN",
        ],
    }
    # The policy is not trusted merely because it is hashed.  Its invariant
    # projection must agree with the independently reconstructed manifest.
    verify_policy_projection(policy, manifest)
    manifest["candidateSha256"] = canonical_sha256(manifest)
    return manifest


def verify_policy_projection(policy: dict[str, Any], manifest: dict[str, Any]) -> None:
    validate_policy_authority(policy)
    require_exact_keys(policy, POLICY_TOP_LEVEL_KEYS, "candidate policy")
    if policy.get("schemaVersion") != "gridworks.realtime-evaluator-candidate-policy.v1":
        raise CandidateAuthorityError("candidate policy schema drift")
    for object_name, expected_keys in POLICY_OBJECT_KEYS.items():
        value = policy.get(object_name)
        if not isinstance(value, dict):
            raise CandidateAuthorityError(
                f"candidate policy {object_name} must be an object"
            )
        require_exact_keys(value, expected_keys, f"candidate policy {object_name}")
    candidate = policy.get("candidate")
    evaluator_producer = policy.get("evaluatorProducerAuthority")
    canonicalization = policy.get("canonicalization")
    source = policy.get("sourceAuthority")
    managed = policy.get("managedBuild")
    engine = policy.get("engineAuthority")
    package = policy.get("packageAuthority")
    headless = policy.get("headlessExecutionAuthority")
    story = policy.get("storyAuthority")
    routes = policy.get("routeProfiles")
    future = policy.get("futureEventStatusBar")
    scenes = policy.get("sceneAuthority")
    required_objects = (
        candidate,
        evaluator_producer,
        canonicalization,
        source,
        managed,
        engine,
        package,
        headless,
        story,
        routes,
        future,
        scenes,
    )
    if not all(isinstance(value, dict) for value in required_objects):
        raise CandidateAuthorityError("candidate policy required object is missing")
    role_counts: dict[str, int] = {}
    for row in manifest["sourceAuthority"]["files"]:
        role_counts[row["role"]] = role_counts.get(row["role"], 0) + 1
    rows_by_path = {
        row["path"]: row for row in manifest["sourceAuthority"]["files"]
    }
    closure_policy = source.get("r2GodotExecutableClosure", {})
    excluded_closure_policy = source.get(
        "excludedFromR2GodotExecutableClosure",
        {},
    )
    closure_paths = (
        set(closure_policy.get("projectFiles", []))
        | set(closure_policy.get("sceneFiles", []))
        | set(closure_policy.get("sceneAttachedScripts", []))
        | set(closure_policy.get("existingScriptUids", []))
    )
    closure_rows = [rows_by_path[path] for path in sorted(closure_paths)] if (
        closure_paths <= set(rows_by_path)
    ) else []
    pinned_rows = [
        {
            key: rows_by_path.get(row.get("path"), {}).get(key)
            for key in ("path", "role", "rawSha256", "byteLength")
        }
        for row in source.get("pinnedAuthorityFiles", [])
        if isinstance(row, dict)
    ]
    story_input_paths = {
        "data/release-campaign-v2.json",
        "data/release-campaign-v3.json",
        STORED_STORY_MANIFEST_PATH,
        STORY_HARNESS_PATH,
        STORY_PROGRAM_PATH,
    }
    story_input_rows = [
        rows_by_path[path] for path in sorted(story_input_paths)
    ] if story_input_paths <= set(rows_by_path) else []
    dotnet_policy = managed.get("dotnetAuthority", {})
    engine_executable_row = next(
        (
            row
            for row in manifest["engineAuthority"]["files"]
            if row["path"] == "Contents/MacOS/Godot"
        ),
        None,
    )
    script_paths = manifest["managedBuild"]["godotScriptPathAuthority"]
    execution = manifest["headlessExecutionAuthority"]
    positive_probe_ids = [
        row["checkpointId"] for row in execution["positiveCheckpointProbes"]
    ]
    rejection_probe_ids = [
        row["probeId"] for row in execution["argumentRejectionProbes"]
    ]
    checks = [
        canonicalization == {
            "name": CANONICALIZATION,
            "rawFileHash": "SHA256_RAW_BYTES",
            "fileRowOrder": "UTF8_PATH_ASCENDING",
            "fileRowChainHash": (
                "SHA256_GRIDWORKS_CANONICAL_JSON_V1_OF_ORDERED_FILE_ROWS"
            ),
            "policyHashRule": "SHA256_RAW_BYTES",
            "candidateHashRule": (
                "SHA256_GRIDWORKS_CANONICAL_JSON_V1_OF_MANIFEST_WITH_"
                "CANDIDATE_SHA256_OMITTED"
            ),
        },
        candidate.get("candidateKind") == CANDIDATE_KIND,
        candidate.get("configuration") == CONFIGURATION,
        candidate.get("officialCommercialUX") is False,
        candidate.get("scoreBearingCaptureAllowed") is False,
        candidate.get("sourceMaterialization") == "EXACT_GIT_COMMIT_BLOBS",
        candidate.get("candidatePackageStatus")
        == "EDITOR_NATIVE_NOT_PUBLIC_PACKAGE",
        evaluator_producer == {
            "schemaVersion": EVALUATOR_PRODUCER_SCHEMA,
            "expectedFileCount": 4,
            "paths": [path for path, _role in EVALUATOR_PRODUCER_PATH_ROLES],
            "sourceMaterialization": (
                "EXACT_SOURCE_COMMIT_GIT_BLOBS_MATCH_RUNNING_WORKTREE_BYTES"
            ),
            "semanticVerifierEntryPoint": (
                "verify_manifest_against_reconstructed_authority"
            ),
            "semanticVerifierReexecutesHeadlessProbes": True,
            "structuralSchemaAuthority": (
                "STRUCTURAL_ONLY_NOT_CANDIDATE_AUTHORITY"
            ),
            "gitCommandAuthority": expected_git_command_authority(),
        },
        manifest["evaluatorProducerAuthority"]["sourceCommit"]
        == manifest["sourceCommit"],
        manifest["evaluatorProducerAuthority"]["fileCount"] == 4,
        [
            row["path"]
            for row in manifest["evaluatorProducerAuthority"]["files"]
        ] == [path for path, _role in EVALUATOR_PRODUCER_PATH_ROLES],
        [
            row["role"]
            for row in manifest["evaluatorProducerAuthority"]["files"]
        ] == [role for _path, role in EVALUATOR_PRODUCER_PATH_ROLES],
        manifest["evaluatorProducerAuthority"]["filesSha256"]
        == canonical_sha256(manifest["evaluatorProducerAuthority"]["files"]),
        manifest["evaluatorProducerAuthority"]["runningFilesMatchGitBlobs"]
        is True,
        manifest["evaluatorProducerAuthority"]["gitCommandAuthority"]
        == expected_git_command_authority(),
        manifest["evaluatorProducerAuthority"]["semanticVerifierEntryPoint"]
        == "verify_manifest_against_reconstructed_authority",
        manifest["evaluatorProducerAuthority"][
            "semanticVerifierReexecutesHeadlessProbes"
        ] is True,
        manifest["evaluatorProducerAuthority"]["structuralSchemaAuthority"]
        == "STRUCTURAL_ONLY_NOT_CANDIDATE_AUTHORITY",
        source.get("expectedFileCount") == len(manifest["sourceAuthority"]["files"]),
        source.get("expectedSourceInputsSha256")
        == manifest["sourceAuthority"]["sourceInputsSha256"],
        source.get("expectedRoleCounts") == role_counts,
        source.get("debugCompileCounts") == {"game": 60, "core": 67},
        source.get("debugEmbeddedResourceCount") == 9,
        closure_policy.get("expectedFileCount") == len(closure_rows) == 35,
        closure_policy.get("expectedFilesSha256") == canonical_sha256(closure_rows),
        excluded_closure_policy == {
            "pathPrefixes": ["game/assets/", "game/realtime/world/"],
            "exactPaths": [
                "game/icon.svg",
                "game/realtime/ui/RealtimeUiLayoutHarness.tscn",
            ],
            "classificationScope": (
                "ENUMERATED_PATHS_EXCLUDED_FROM_CURRENT_35_FILE_RUNTIME_CLOSURE"
            ),
            "completenessClaim": False,
        },
        pinned_rows == source.get("pinnedAuthorityFiles"),
        dotnet_policy.get("sdkVersion") == DOTNET_VERSION,
        dotnet_policy.get("globalJsonRawSha256")
        == rows_by_path.get(GLOBAL_JSON_PATH, {}).get("rawSha256"),
        dotnet_policy.get("commandBindingScope")
        == "RESOLVED_WRAPPER_AND_HOST_TWO_FILE_BYTES_ONLY",
        dotnet_policy.get("files")
        == manifest["managedBuild"]["dotnetAuthority"]["files"],
        dotnet_policy.get("filesSha256")
        == manifest["managedBuild"]["dotnetAuthority"]["filesSha256"],
        managed.get("packageInputs", {}).get("packageInputsSha256")
        == manifest["managedBuild"]["packageInputsSha256"],
        managed.get("requiredManagedOutputs")
        == [row["path"] for row in manifest["managedBuild"]["outputs"]],
        managed.get("godotScriptPathAuthority", {}).get("godotProjectDir")
        == script_paths["godotProjectDir"],
        managed.get("godotScriptPathAuthority", {}).get("pathEncoding")
        == script_paths["pathEncoding"],
        managed.get("godotScriptPathAuthority", {}).get("sceneAttachedScriptCount")
        == script_paths["sceneAttachedScriptCount"],
        managed.get("godotScriptPathAuthority", {}).get("escapedResourcePathCount")
        == script_paths["escapedResourcePathCount"],
        managed.get("godotScriptPathAuthority", {}).get("requiredResourcePaths")
        == script_paths["resourcePaths"],
        managed.get("isolationRequirements") == {
            "freshOutputRoot": True,
            "parentBuildImportsDisabled": True,
            "networkPackageSourcesDisabled": True,
            "defaultCompileItemsDisabledInEvaluatorProjects": True,
            "defaultEmbeddedResourcesDisabledInEvaluatorProjects": True,
            "outputsMustRemainUnderFreshRoot": True,
        },
        engine.get("version") == GODOT_VERSION_OUTPUT,
        engine.get("appFileCount") == manifest["engineAuthority"]["fileCount"],
        engine.get("appFileTreeSha256")
        == manifest["engineAuthority"]["fileTreeSha256"],
        engine.get("executable") == engine_executable_row,
        engine.get("pathPolicy")
        == "CANONICAL_APP_ROOT_REGULAR_FILES_REJECT_SYMLINKS",
        engine.get("versionProbeRequired") is True,
        package.get("packageKind")
        == manifest["packageAuthority"]["packageKind"],
        package.get("rootName") == manifest["packageAuthority"]["rootName"],
        package.get("fileCount") == manifest["packageAuthority"]["fileCount"],
        package.get("sourceClosureFileCount") == 35,
        package.get("managedOutputFileCount") == 4,
        package.get("materializedFromExactBindings")
        == manifest["packageAuthority"]["materializedFromExactBindings"],
        package.get("nativeAppBundle")
        == manifest["packageAuthority"]["nativeAppBundle"],
        package.get("publicPackage")
        == manifest["packageAuthority"]["publicPackage"],
        headless.get("schemaVersion") == execution["schemaVersion"],
        headless.get("executionKind") == execution["executionKind"],
        headless.get("positiveCheckpointIds") == positive_probe_ids,
        headless.get("argumentRejectionProbeIds") == rejection_probe_ids,
        headless.get("freshProcessPerProbe")
        == execution["freshProcessPerProbe"],
        headless.get("freshExactPackageCopyPerProbe")
        == execution["freshExactPackageCopyPerProbe"],
        headless.get(
            "ephemeralProbeFilesystemOutputsExcludedFromPackageFileAuthority"
        )
        == execution[
            "ephemeralProbeFilesystemOutputsExcludedFromPackageFileAuthority"
        ],
        headless.get("expectedEphemeralEmptyDirectorySideEffects")
        == execution["expectedEphemeralEmptyDirectorySideEffects"],
        headless.get("hostRuntimeBinding") == execution["hostRuntimeBinding"],
        headless.get("inputFileCountPerProbe")
        == manifest["packageAuthority"]["fileCount"],
        headless.get("boundFileByteMutationCount") == 0,
        headless.get("nativePresentationObserved")
        == execution["nativePresentationObserved"],
        headless.get("scoreBearingEvidence")
        == execution["scoreBearingEvidence"],
        scenes.get("defaultScene", {}).get("resourcePath") == DEFAULT_SCENE,
        scenes.get("interactiveCandidateScene", {}).get("resourcePath")
        == INTERACTIVE_SCENE,
        scenes.get("checkpointRunnerScene", {}).get("resourcePath")
        == CHECKPOINT_SCENE,
        scenes.get("runtimeFixture", {}).get("chapterCount") == 1,
        scenes.get("runtimeFixture", {}).get("eventCount") == 3,
        scenes.get("declaredNonruntimeFullV3", {}).get("chapterCount") == 8,
        scenes.get("declaredNonruntimeFullV3", {}).get("eventCount") == 16,
        scenes.get("declaredNonruntimeFullV3", {}).get("loadedByCandidateRuntime")
        is False,
        scenes.get("checkpoints") == [dict(value) for value in CHECKPOINTS],
        story.get("partCount") == 34,
        story.get("expectedInputsSha256") == canonical_sha256(story_input_rows),
        story.get("authoredCampaignPath") == "data/release-campaign-v2.json",
        story.get("declaredRealtimeCampaignPath")
        == "data/release-campaign-v3.json",
        story.get("harnessPaths") == [STORY_PROGRAM_PATH, STORY_HARNESS_PATH],
        story.get("storedManifestPath") == STORED_STORY_MANIFEST_PATH,
        story.get("storedManifestRawSha256")
        == manifest["storyAuthority"]["storyManifestRawSha256"],
        story.get("partCounts") == {
            "briefing": 8,
            "window": 6,
            "result": 11,
            "epilogueCard": 3,
            "epiloguePromiseLine": 6,
        },
        story.get("authoredReachabilityOnly") is True,
        story.get("nativeReachabilityClaim") is False,
        story.get("deterministicRebuildMustMatchStoredBytes") is True,
        future.get("signals") == list(FUTURE_EVENT_SIGNALS),
        future.get("implementationPath")
        == "game/realtime/ui/RealtimeEventRail.cs",
        future.get("scenePath") == "game/realtime/ui/RealtimeEventRail.tscn",
        future.get("implementationPresent") is True,
        future.get("headlessWiringStatus")
        == manifest["futureEventStatusBar"]["headlessWiringStatus"],
        future.get("nativeQualityObserved") is False,
        future.get("scoreClaimAllowed") is False,
        routes == {
            "interactiveFirstLight": {
                "profileId": "INTERACTIVE_NONDEFAULT_FIRST_LIGHT",
                "availability": "AVAILABLE",
                "scenePath": INTERACTIVE_SCENE,
                "headless": False,
                "requiresExplicitScene": True,
                "arguments": ["--path", "game", "--scene", INTERACTIVE_SCENE],
                "checkpointIds": [],
                "coverage": "FIRST_LIGHT_RUNTIME_FIXTURE_ONLY",
            },
            "targetedCheckpoints": {
                "profileId": "TARGETED_DETERMINISTIC_CHECKPOINTS",
                "availability": "AVAILABLE",
                "scenePath": CHECKPOINT_SCENE,
                "headless": True,
                "requiresExplicitScene": True,
                "argumentsPrefix": [
                    "--headless", "--path", "game", "--scene",
                    CHECKPOINT_SCENE, "--",
                ],
                "checkpointArgumentFormat": "--checkpoint=<EXACT_CHECKPOINT_ID>",
                "checkpointIds": [
                    value["checkpointId"] for value in CHECKPOINTS
                ],
                "coverage": (
                    "ONE_NAMED_CHECKPOINT_ADVANCES_EXACTLY_ONE_CORE_MINUTE"
                ),
            },
            "fullFlow": {
                "profileId": "FULL_FLOW_E2E",
                "availability": "UNAVAILABLE",
                "scenePath": None,
                "headless": None,
                "requiresExplicitScene": None,
                "arguments": [],
                "checkpointIds": [],
                "coverage": "NOT_IMPLEMENTED",
            },
        },
        routes.get("fullFlow", {}).get("availability") == "UNAVAILABLE",
        routes.get("fullFlow", {}).get("profileId") == "FULL_FLOW_E2E",
        routes.get("fullFlow", {}).get("scenePath") is None,
        routes.get("targetedCheckpoints", {}).get("checkpointIds")
        == [value["checkpointId"] for value in CHECKPOINTS],
        policy.get("limitations") == {
            "defaultSceneCandidateMismatch": (
                "DEFAULT_COMMERCIAL_MAIN_IS_V2_EXPLICIT_REALTIME_SLICE_REQUIRED"
            ),
            "runtimeCoverage": "FIRST_LIGHT_ONLY",
            "fullCampaignNativeE2E": "NOT_IMPLEMENTED",
            "fullFlowRoute": "UNAVAILABLE",
            "saveResume": "NOT_IMPLEMENTED",
            "finaleEpilogueNative": "NOT_IMPLEMENTED",
            "fullV3RuntimeBinding": "DECLARED_NONRUNTIME_ONLY",
            "futureEventStatusBarNativeQuality": "NOT_OBSERVED",
            "runtimeArtAuthority": "NOT_ESTABLISHED",
            "dotnetToolchainAuthority": "PARTIAL_COMMAND_AND_HOST_ONLY",
            "packageStatus": "EDITOR_NATIVE_NOT_PUBLIC_PACKAGE",
            "nativeCaptureStatus": "PROHIBITED_UNTIL_UX_R1_CLOSE",
            "claimsNotAuthorized": [
                "OFFICIAL_COMMERCIAL_UX",
                "SCORE_BEARING_CAPTURE",
                "FULL_CAMPAIGN_NATIVE_E2E",
                "PUBLIC_PACKAGE",
                "RUNTIME_ART_ADOPTION",
                "FUTURE_EVENT_STATUS_BAR_NATIVE_QUALITY",
            ],
        },
    ]
    if not all(checks):
        raise CandidateAuthorityError(
            "candidate policy invariant projection disagrees with reconstructed authority"
        )
    if manifest["routeProfiles"][-1]["availability"] != "UNAVAILABLE_NOT_IMPLEMENTED":
        raise CandidateAuthorityError("full-flow route must remain unavailable")


def verify_manifest_against_reconstructed_authority(
    submitted: dict[str, Any],
    build: IsolatedBuild,
    godot_app_root: Path,
    policy: dict[str, Any],
    policy_bytes: bytes,
) -> None:
    submitted_copy = dict(submitted)
    submitted_sha = submitted_copy.pop("candidateSha256", None)
    if submitted_sha != canonical_sha256(submitted_copy):
        raise CandidateAuthorityError("candidate manifest self-hash mismatch")
    build.verify_outputs()
    engine_rows, engine_tree_sha256 = bind_engine_tree(godot_app_root)
    reconstructed_headless_execution_authority = run_headless_execution_authority(
        build,
        godot_app_root,
        engine_rows,
        engine_tree_sha256,
    )
    expected = build_manifest(
        build,
        engine_rows,
        engine_tree_sha256,
        reconstructed_headless_execution_authority,
        policy,
        policy_bytes,
    )
    build.verify_outputs()
    if set(submitted) != set(expected):
        raise CandidateAuthorityError("candidate manifest top-level fields drift")
    if not strict_typed_equal(submitted, expected):
        raise CandidateAuthorityError(
            "candidate manifest differs from independently reconstructed authority"
        )


def write_manifest(path: Path, manifest: dict[str, Any]) -> None:
    data = json.dumps(
        manifest,
        ensure_ascii=False,
        allow_nan=False,
        indent=2,
        sort_keys=True,
    ).encode("utf-8") + b"\n"
    write_exclusive(path, data)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository-root", type=Path, default=DEFAULT_REPOSITORY_ROOT)
    parser.add_argument("--source-revision", default="HEAD")
    parser.add_argument("--godot-package-root", type=Path)
    parser.add_argument("--godot-app-root", type=Path)
    parser.add_argument("--scratch-parent", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    try:
        repository_root = resolve_repository_root(args.repository_root)
        policy, policy_bytes = load_policy()
        godot_app_root = args.godot_app_root or default_godot_app(repository_root)
        engine_rows, engine_sha = bind_engine_tree(godot_app_root)
        with isolated_managed_build(
            repository_root,
            revision=args.source_revision,
            package_root=args.godot_package_root,
            scratch_parent=args.scratch_parent,
        ) as build:
            headless_execution = run_headless_execution_authority(
                build,
                godot_app_root,
                engine_rows,
                engine_sha,
            )
            manifest = build_manifest(
                build,
                engine_rows,
                engine_sha,
                headless_execution,
                policy,
                policy_bytes,
            )
            verify_manifest_against_reconstructed_authority(
                manifest,
                build,
                godot_app_root,
                policy,
                policy_bytes,
            )
            write_manifest(args.output, manifest)
        print(json.dumps({
            "status": "PASS_EDITOR_NATIVE_NONDEFAULT_DEBUG_FIRST_LIGHT_AUTHORITY",
            "candidateSha256": manifest["candidateSha256"],
            "sourceCommit": manifest["sourceCommit"],
            "scoreBearingCaptureAllowed": False,
            "fullFlow": "UNAVAILABLE_NOT_IMPLEMENTED",
        }, ensure_ascii=False, indent=2, sort_keys=True))
    except CandidateAuthorityError as error:
        print(f"FAIL realtime candidate authority: {error}", file=os.sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
