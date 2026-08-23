#!/usr/bin/env python3
"""Adversarial checks for the exact Debug R2 candidate authority."""

from __future__ import annotations

import contextlib
import copy
import importlib.util
import json
from pathlib import Path
import re
import sys
import tempfile
import unittest


SCRIPT_DIR = Path(__file__).resolve().parent
REPOSITORY_ROOT = SCRIPT_DIR.parents[2]
MODULE_PATH = SCRIPT_DIR / "build-realtime-candidate-authority.py"
SPEC = importlib.util.spec_from_file_location("realtime_candidate_authority", MODULE_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot load {MODULE_PATH}")
AUTHORITY = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = AUTHORITY
SPEC.loader.exec_module(AUTHORITY)


def _resolve_local_ref(root: dict, reference: str):
    if not reference.startswith("#/"):
        raise AssertionError(f"unsupported non-local schema reference: {reference}")
    value = root
    for raw_token in reference[2:].split("/"):
        token = raw_token.replace("~1", "/").replace("~0", "~")
        value = value[token]
    return value


def validate_schema_subset(instance, schema, root, path: str = "$") -> None:
    """Validate every Draft 2020-12 keyword used by the checked-in schema."""

    if schema is False:
        raise AssertionError(f"{path}: boolean false schema")
    if schema is True:
        return
    if not isinstance(schema, dict):
        raise AssertionError(f"{path}: schema must be an object or boolean")
    if "$ref" in schema:
        validate_schema_subset(
            instance,
            _resolve_local_ref(root, schema["$ref"]),
            root,
            path,
        )
    for child in schema.get("allOf", []):
        validate_schema_subset(instance, child, root, path)
    expected_type = schema.get("type")
    type_matches = {
        "object": isinstance(instance, dict),
        "array": isinstance(instance, list),
        "string": isinstance(instance, str),
        "integer": isinstance(instance, int) and not isinstance(instance, bool),
        "boolean": isinstance(instance, bool),
        "null": instance is None,
    }
    if expected_type is not None and not type_matches.get(expected_type, False):
        raise AssertionError(f"{path}: expected {expected_type}")
    if "const" in schema and not AUTHORITY.strict_typed_equal(
        instance,
        schema["const"],
    ):
        raise AssertionError(f"{path}: const mismatch")
    if "enum" in schema and not any(
        AUTHORITY.strict_typed_equal(instance, choice)
        for choice in schema["enum"]
    ):
        raise AssertionError(f"{path}: enum mismatch")
    if isinstance(instance, dict):
        required = schema.get("required", [])
        missing = [key for key in required if key not in instance]
        if missing:
            raise AssertionError(f"{path}: missing required keys {missing}")
        properties = schema.get("properties", {})
        for key, child in properties.items():
            if key in instance:
                validate_schema_subset(instance[key], child, root, f"{path}.{key}")
        if schema.get("additionalProperties") is False:
            extras = set(instance) - set(properties)
            if extras:
                raise AssertionError(f"{path}: extra keys {sorted(extras)}")
    if isinstance(instance, list):
        if len(instance) < schema.get("minItems", 0):
            raise AssertionError(f"{path}: too few items")
        if "maxItems" in schema and len(instance) > schema["maxItems"]:
            raise AssertionError(f"{path}: too many items")
        if schema.get("uniqueItems"):
            encoded = [
                json.dumps(value, sort_keys=True, separators=(",", ":"))
                for value in instance
            ]
            if len(encoded) != len(set(encoded)):
                raise AssertionError(f"{path}: duplicate items")
        prefixes = schema.get("prefixItems", [])
        for index, child in enumerate(prefixes):
            if index < len(instance):
                validate_schema_subset(instance[index], child, root, f"{path}[{index}]")
        items = schema.get("items")
        remaining = instance[len(prefixes):]
        if items is False and remaining:
            raise AssertionError(f"{path}: additional tuple items")
        if isinstance(items, dict):
            for offset, value in enumerate(remaining, start=len(prefixes)):
                validate_schema_subset(value, items, root, f"{path}[{offset}]")
        if "contains" in schema:
            matches = 0
            for value in instance:
                try:
                    validate_schema_subset(value, schema["contains"], root, path)
                    matches += 1
                except AssertionError:
                    pass
            minimum = schema.get("minContains", 1)
            maximum = schema.get("maxContains")
            if matches < minimum or (maximum is not None and matches > maximum):
                raise AssertionError(f"{path}: contains count {matches}")
    if isinstance(instance, str):
        if len(instance) < schema.get("minLength", 0):
            raise AssertionError(f"{path}: string too short")
        if "maxLength" in schema and len(instance) > schema["maxLength"]:
            raise AssertionError(f"{path}: string too long")
        if "pattern" in schema and re.search(schema["pattern"], instance) is None:
            raise AssertionError(f"{path}: pattern mismatch")
    if isinstance(instance, int) and not isinstance(instance, bool):
        if "minimum" in schema and instance < schema["minimum"]:
            raise AssertionError(f"{path}: below minimum")


class RealtimeCandidateAuthorityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.stack = contextlib.ExitStack()
        cls.build = cls.stack.enter_context(
            AUTHORITY.isolated_managed_build(REPOSITORY_ROOT)
        )
        cls.policy, cls.policy_bytes = AUTHORITY.load_policy()
        cls.godot_app_root = AUTHORITY.default_godot_app(REPOSITORY_ROOT)
        cls.engine_rows, cls.engine_sha = AUTHORITY.bind_engine_tree(
            cls.godot_app_root
        )
        cls.headless_execution = AUTHORITY.run_headless_execution_authority(
            cls.build,
            cls.godot_app_root,
            cls.engine_rows,
            cls.engine_sha,
        )
        cls.manifest = AUTHORITY.build_manifest(
            cls.build,
            cls.engine_rows,
            cls.engine_sha,
            cls.headless_execution,
            cls.policy,
            cls.policy_bytes,
        )

    @classmethod
    def tearDownClass(cls) -> None:
        cls.stack.close()

    def assertRejected(self, callable_value, fragment: str | None = None) -> None:
        with self.assertRaises(AUTHORITY.CandidateAuthorityError) as captured:
            callable_value()
        if fragment is not None:
            self.assertIn(fragment, str(captured.exception))

    def test_exact_git_and_debug_authority(self) -> None:
        source = self.build.source
        self.assertEqual(60, len(source.game_sources))
        self.assertEqual(67, len(source.core_sources))
        self.assertEqual(9, len(source.embedded_resources))
        self.assertEqual(170, len(source.blobs))
        self.assertNotIn(
            "src/Gridworks.Core/Release/V3/RealtimeCampaignPersistence.cs",
            source.core_sources,
        )
        self.assertFalse(any(
            path.startswith("game/realtime/world/")
            for path in source.game_sources
        ))
        self.assertFalse(any(
            path.startswith("tools/commercial-ux/native/")
            for path in source.blobs
        ))
        self.assertEqual(
            "sha256:30573f95d267179bee7adccd7f2ca42dce9aea3bd9278bb10dbaf6b3c16fc141",
            AUTHORITY.canonical_sha256(source.rows),
        )
        roles = {row["role"] for row in source.rows}
        self.assertIn("NEGATIVE_EXPORT_AUTHORITY", roles)
        self.assertIn("DECLARED_NONRUNTIME_FULL_V3_AUTHORITY", roles)
        self.assertIn("STORED_STORY_MANIFEST", roles)

    def test_manifest_tells_current_product_truth(self) -> None:
        manifest = self.manifest
        self.assertEqual(
            "EDITOR_NATIVE_NONDEFAULT_DEBUG_FIRST_LIGHT",
            manifest["candidateKind"],
        )
        self.assertFalse(manifest["officialCommercialUX"])
        self.assertFalse(manifest["scoreBearingCaptureAllowed"])
        self.assertEqual("Debug", manifest["configuration"])
        producer = manifest["evaluatorProducerAuthority"]
        self.assertEqual(manifest["sourceCommit"], producer["sourceCommit"])
        self.assertEqual(4, producer["fileCount"])
        self.assertEqual(
            [path for path, _role in AUTHORITY.EVALUATOR_PRODUCER_PATH_ROLES],
            [row["path"] for row in producer["files"]],
        )
        self.assertEqual(
            [role for _path, role in AUTHORITY.EVALUATOR_PRODUCER_PATH_ROLES],
            [row["role"] for row in producer["files"]],
        )
        self.assertTrue(producer["runningFilesMatchGitBlobs"])
        self.assertEqual(
            "verify_manifest_against_reconstructed_authority",
            producer["semanticVerifierEntryPoint"],
        )
        self.assertEqual(
            "STRUCTURAL_ONLY_NOT_CANDIDATE_AUTHORITY",
            producer["structuralSchemaAuthority"],
        )
        self.assertEqual(
            "res://CommercialMain.tscn",
            manifest["sceneAuthority"]["projectDefaultScene"],
        )
        self.assertEqual(
            "res://realtime/r2/RealtimeSliceMain.tscn",
            manifest["sceneAuthority"]["evaluationTargetScene"],
        )
        self.assertFalse(manifest["sceneAuthority"]["evaluationTargetIsDefault"])
        self.assertEqual(
            "FIRST_LIGHT_1_CHAPTER_3_EVENTS",
            manifest["sceneAuthority"]["runtimeFixtureCoverage"],
        )
        self.assertFalse(
            manifest["storyAuthority"]["fullReleaseV3AttachedToRuntime"]
        )
        self.assertEqual(34, manifest["storyAuthority"]["partCount"])
        self.assertEqual(16, manifest["storyAuthority"]["fullRealtimeEventCount"])
        self.assertEqual(
            {
                "configuration": "ExportRelease",
                "gameCompileCount": 9,
                "coreCompileCount": 20,
                "resourceCount": 3,
                "realtimeR2GameCompileCount": 0,
                "realtimeUiGameCompileCount": 0,
                "realtimeV3CoreCompileCount": 0,
                "candidateKind": "NOT_R2_CANDIDATE",
            },
            manifest["managedBuild"]["negativeExportAuthority"],
        )
        self.assertEqual(
            list(AUTHORITY.FUTURE_EVENT_SIGNALS),
            manifest["futureEventStatusBar"]["requiredSignals"],
        )
        self.assertEqual(39, manifest["packageAuthority"]["fileCount"])
        self.assertFalse(manifest["packageAuthority"]["nativeAppBundle"])
        self.assertFalse(manifest["packageAuthority"]["publicPackage"])
        self.assertEqual(
            sorted(self.build.runtime_package_bindings),
            [row["path"] for row in manifest["packageAuthority"]["files"]],
        )
        self.assertEqual(
            "NOT_OBSERVED",
            manifest["futureEventStatusBar"]["nativeQualityStatus"],
        )
        self.assertFalse(
            manifest["headlessExecutionAuthority"]["nativePresentationObserved"]
        )
        self.assertFalse(
            manifest["headlessExecutionAuthority"]["scoreBearingEvidence"]
        )
        full_flow = manifest["routeProfiles"][-1]
        self.assertEqual("FULL_FLOW_EXCEPTION", full_flow["routeKind"])
        self.assertEqual("UNAVAILABLE_NOT_IMPLEMENTED", full_flow["availability"])
        self.assertIsNone(full_flow["scene"])
        self.assertIsNone(full_flow["allowedClaimPrefix"])
        AUTHORITY.verify_manifest_against_reconstructed_authority(
            manifest,
            self.build,
            self.godot_app_root,
            self.headless_execution,
            self.policy,
            self.policy_bytes,
        )

    def test_checkpoint_route_is_exact_and_cannot_claim_full_flow(self) -> None:
        targeted = self.manifest["routeProfiles"][1]
        self.assertEqual("TARGETED_CHECKPOINT", targeted["routeKind"])
        self.assertEqual(
            ["A1_NORMAL_READY", "A1_CONSTRUCTION_DUE_1M"],
            [row["checkpointId"] for row in targeted["checkpoints"]],
        )
        self.assertEqual(
            "--checkpoint=<CANONICAL_ID>",
            targeted["argumentTemplate"],
        )
        self.assertEqual(
            {0, 3},
            {row["commandCount"] for row in targeted["checkpoints"]},
        )
        self.assertTrue(all(
            row["claimLabel"].startswith("TARGETED_LIVE_CHECKPOINT_PASS:")
            for row in targeted["checkpoints"]
        ))
        self.assertNotEqual(
            targeted["allowedClaimPrefix"],
            self.manifest["routeProfiles"][-1]["allowedClaimPrefix"],
        )

    def test_recomputed_expected_manifest_rejects_forgery(self) -> None:
        mutations = []

        forged_default = copy.deepcopy(self.manifest)
        forged_default["sceneAuthority"]["evaluationTargetIsDefault"] = True
        mutations.append(forged_default)

        forged_score = copy.deepcopy(self.manifest)
        forged_score["scoreBearingCaptureAllowed"] = True
        mutations.append(forged_score)

        boolean_integer_alias = copy.deepcopy(self.manifest)
        boolean_integer_alias["officialCommercialUX"] = 0
        mutations.append(boolean_integer_alias)

        forged_full_flow = copy.deepcopy(self.manifest)
        forged_full_flow["routeProfiles"][-1]["availability"] = "AVAILABLE"
        forged_full_flow["routeProfiles"][-1]["allowedClaimPrefix"] = "PASS:"
        mutations.append(forged_full_flow)

        missing_signal = copy.deepcopy(self.manifest)
        missing_signal["futureEventStatusBar"]["requiredSignals"].pop()
        mutations.append(missing_signal)

        forged_headless_score = copy.deepcopy(self.manifest)
        forged_headless_score["headlessExecutionAuthority"][
            "scoreBearingEvidence"
        ] = True
        mutations.append(forged_headless_score)

        forged_probe_end = copy.deepcopy(self.manifest)
        forged_probe_end["headlessExecutionAuthority"][
            "positiveCheckpointProbes"
        ][0]["passClaim"]["endCanonicalStateSha256"] = "sha256:" + "2" * 64
        mutations.append(forged_probe_end)

        replaced_output = copy.deepcopy(self.manifest)
        replaced_output["managedBuild"]["outputs"][0]["rawSha256"] = (
            "sha256:" + "0" * 64
        )
        mutations.append(replaced_output)

        replaced_producer = copy.deepcopy(self.manifest)
        replaced_producer["evaluatorProducerAuthority"]["files"][0][
            "rawSha256"
        ] = "sha256:" + "9" * 64
        mutations.append(replaced_producer)

        replaced_package_file = copy.deepcopy(self.manifest)
        replaced_package_file["packageAuthority"]["files"][0]["rawSha256"] = (
            "sha256:" + "1" * 64
        )
        mutations.append(replaced_package_file)

        stale_story = copy.deepcopy(self.manifest)
        stale_story["storyAuthority"]["partCount"] = 26
        mutations.append(stale_story)

        moved_source = copy.deepcopy(self.manifest)
        moved_source["sourceAuthority"]["files"][0]["path"] = "../escape"
        mutations.append(moved_source)

        swapped_fixture = copy.deepcopy(self.manifest)
        fixture = next(
            row for row in swapped_fixture["sourceAuthority"]["files"]
            if row["path"] == AUTHORITY.FIXTURE_CAMPAIGN_PATH
        )
        full = next(
            row for row in swapped_fixture["sourceAuthority"]["files"]
            if row["path"] == AUTHORITY.FULL_REALTIME_CAMPAIGN_PATH
        )
        fixture["rawSha256"], full["rawSha256"] = (
            full["rawSha256"], fixture["rawSha256"]
        )
        mutations.append(swapped_fixture)

        unknown_checkpoint = copy.deepcopy(self.manifest)
        unknown_checkpoint["routeProfiles"][1]["checkpoints"][0][
            "checkpointId"
        ] = "FULL_FLOW"
        mutations.append(unknown_checkpoint)

        for mutation in mutations:
            with self.subTest(mutation=mutations.index(mutation)):
                unsigned = dict(mutation)
                unsigned.pop("candidateSha256", None)
                mutation["candidateSha256"] = AUTHORITY.canonical_sha256(unsigned)
                self.assertRejected(
                    lambda mutation=mutation: (
                        AUTHORITY.verify_manifest_against_reconstructed_authority(
                            mutation,
                            self.build,
                            self.godot_app_root,
                            self.headless_execution,
                            self.policy,
                            self.policy_bytes,
                        )
                    ),
                    "independently reconstructed",
                )

    def test_self_hash_and_strict_json_reject_local_relabel(self) -> None:
        changed = copy.deepcopy(self.manifest)
        changed["candidateKind"] = "SELLABLE_FULL_CAMPAIGN"
        self.assertRejected(
            lambda: AUTHORITY.verify_manifest_against_reconstructed_authority(
                changed,
                self.build,
                self.godot_app_root,
                self.headless_execution,
                self.policy,
                self.policy_bytes,
            ),
            "self-hash",
        )
        duplicate = b'{"schemaVersion":"x","schemaVersion":"y"}'
        self.assertRejected(
            lambda: AUTHORITY.strict_json_bytes(duplicate, "duplicate fixture"),
            "repeats key",
        )
        self.assertRejected(
            lambda: AUTHORITY.strict_json_bytes(b'{"score":NaN}', "nan fixture"),
            "non-JSON numeric token",
        )

    def test_tree_alias_path_and_reserved_output_attacks_are_rejected(self) -> None:
        entries = AUTHORITY.git_tree_entries(
            REPOSITORY_ROOT,
            self.build.source.source_commit,
        )
        object_id = next(row[2] for row in entries if row[3].endswith(".cs"))
        cases = (
            [("120000", "blob", object_id, "game/realtime/r2/Linked.cs")],
            [("160000", "commit", object_id, "src/Gridworks.Core/Vendor")],
            [
                ("100644", "blob", object_id, "game/realtime/r2/A.cs"),
                ("100644", "blob", object_id, "GAME/REALTIME/R2/a.cs"),
            ],
            [("100644", "blob", object_id, "game/.godot/Poison.cs")],
            [("100644", "blob", object_id, "src/Gridworks.Core/obj/Poison.cs")],
        )
        for extra in cases:
            self.assertRejected(
                lambda extra=extra: AUTHORITY.validate_tree_aliases(entries + extra)
            )
        for path in ("../escape", "/absolute", "game\\escape", "game/../escape"):
            self.assertRejected(lambda path=path: AUTHORITY.validate_git_path(path))
        self.assertTrue(AUTHORITY._is_reserved_source("game/.godot/Poison.cs"))
        self.assertTrue(AUTHORITY._is_reserved_source("game/bin/Poison.cs"))
        self.assertTrue(AUTHORITY._is_reserved_source("src/Gridworks.Core/obj/P.cs"))

    def test_post_bind_output_mutation_is_rejected(self) -> None:
        binding = self.build.output_bindings["managed/Gridworks.Game.dll"]
        original = binding.path.read_bytes()
        try:
            binding.path.write_bytes(original + b"MUTATED")
            self.assertRejected(
                self.build.verify_outputs,
                "raw-byte binding mismatch",
            )
        finally:
            binding.path.write_bytes(original)
        self.build.verify_outputs()
        package_binding = self.build.runtime_package_bindings[
            ".godot/mono/temp/bin/Debug/Gridworks.Game.dll"
        ]
        package_original = package_binding.path.read_bytes()
        try:
            package_binding.path.write_bytes(package_original + b"MUTATED")
            self.assertRejected(
                self.build.verify_outputs,
                "runtime package",
            )
        finally:
            package_binding.path.write_bytes(package_original)
        self.build.verify_outputs()

        unbound = self.build.runtime_package_root / ".godot/mono/temp/bin/Debug/UNBOUND.dll"
        try:
            unbound.write_bytes(b"unbound")
            self.assertRejected(
                self.build.verify_outputs,
                "file set",
            )
        finally:
            unbound.unlink(missing_ok=True)
        self.build.verify_outputs()

        unbound_directory = self.build.runtime_package_root / ".godot/unbound-empty"
        try:
            unbound_directory.mkdir()
            self.assertRejected(
                self.build.verify_outputs,
                "directory set",
            )
        finally:
            unbound_directory.rmdir()
        self.build.verify_outputs()

        alias = self.build.runtime_package_root / "runtime-alias"
        try:
            alias.symlink_to("project.godot")
            self.assertRejected(
                self.build.verify_outputs,
                "symlink",
            )
        finally:
            alias.unlink(missing_ok=True)
        self.build.verify_outputs()

    def test_generated_godot_script_paths_are_exact_res_uris(self) -> None:
        authority = AUTHORITY.godot_script_path_authority(self.build)
        self.assertEqual("$CANDIDATE_SOURCE_ROOT/game", authority["godotProjectDir"])
        self.assertEqual(12, authority["sceneAttachedScriptCount"])
        self.assertEqual(0, authority["escapedResourcePathCount"])
        self.assertIn(
            "res://realtime/r2/RealtimeSliceCheckpointRunner.cs",
            authority["resourcePaths"],
        )
        self.assertIn(
            "res://realtime/ui/RealtimeEventRail.cs",
            authority["resourcePaths"],
        )
        game_project = next(
            row for row in self.build.generated_rows
            if row["path"] == "generated/CandidateGame.csproj"
        )
        self.assertGreater(game_project["byteLength"], 0)

    def test_headless_execution_authority_is_exact_and_non_score(self) -> None:
        execution = self.headless_execution
        AUTHORITY.validate_headless_execution_authority(
            execution,
            self.build,
            self.engine_sha,
        )
        self.assertEqual(
            ["A1_NORMAL_READY", "A1_CONSTRUCTION_DUE_1M"],
            [
                probe["checkpointId"]
                for probe in execution["positiveCheckpointProbes"]
            ],
        )
        self.assertTrue(all(
            probe["exitCode"] == 0
            and probe["readyClaimOccurrenceCount"] == 1
            and probe["passClaimOccurrenceCount"] == 1
            and probe["unexpectedTypedClaimCount"] == 0
            and probe["boundFileByteMutationCount"] == 0
            and not probe["nativePresentationObserved"]
            and not probe["scoreBearingEvidence"]
            for probe in execution["positiveCheckpointProbes"]
        ))
        self.assertEqual(
            [
                "REJECT_MISSING_ARGUMENT",
                "REJECT_EXTRA_ARGUMENT",
                "REJECT_FULL_FLOW_AS_CHECKPOINT",
            ],
            [probe["probeId"] for probe in execution["argumentRejectionProbes"]],
        )
        self.assertTrue(all(
            probe["exitCode"] == 2
            and probe["readyClaimOccurrenceCount"] == 0
            and probe["passClaimOccurrenceCount"] == 0
            and probe["fullFlowClaimOccurrenceCount"] == 0
            for probe in execution["argumentRejectionProbes"]
        ))

    def test_headless_execution_rejects_rehashed_claim_injection(self) -> None:
        mutations = []

        extra_envelope = copy.deepcopy(self.headless_execution)
        extra_envelope["officialCommercialUX"] = True
        mutations.append(extra_envelope)

        extra_positive = copy.deepcopy(self.headless_execution)
        extra_positive["positiveCheckpointProbes"][0][
            "scoreBearingCaptureAllowed"
        ] = True
        mutations.append(extra_positive)

        replaced_rejection_hashes = copy.deepcopy(self.headless_execution)
        replaced_rejection_hashes["argumentRejectionProbes"][0][
            "stderrRawSha256"
        ] = "sha256:" + "4" * 64
        replaced_rejection_hashes["argumentRejectionProbes"][0][
            "logRawSha256"
        ] = "sha256:" + "5" * 64
        mutations.append(replaced_rejection_hashes)

        hidden_error = copy.deepcopy(self.headless_execution)
        hidden_error["positiveCheckpointProbes"][0]["stderrUtf8"] = (
            "ERROR: hidden parse failure\n"
        )
        mutations.append(hidden_error)

        forged_full_flow = copy.deepcopy(self.headless_execution)
        forged_probe = forged_full_flow["argumentRejectionProbes"][2]
        forged_probe["stderrUtf8"] += "FULL_FLOW_PASS:FORGED_UNAUTHORIZED\n"
        forged_stderr = forged_probe["stderrUtf8"].encode("utf-8")
        forged_probe["stderrRawSha256"] = AUTHORITY.sha256_bytes(forged_stderr)
        forged_probe["stderrByteLength"] = len(forged_stderr)
        forged_probe["logUtf8"] = (
            forged_probe["stdoutUtf8"] + forged_probe["stderrUtf8"]
        )
        forged_log = forged_probe["logUtf8"].encode("utf-8")
        forged_probe["logRawSha256"] = AUTHORITY.sha256_bytes(forged_log)
        forged_probe["logByteLength"] = len(forged_log)
        mutations.append(forged_full_flow)

        for injected_line in (
            "WARNING: native surface missing but exit remains 2\n",
            "OFFICIAL_COMMERCIAL_UX_PASS score=100\n",
            "  FULL_FLOW_PASS:INDENTED_FORGERY\n",
        ):
            injected_rejection = copy.deepcopy(self.headless_execution)
            injected_probe = injected_rejection["argumentRejectionProbes"][2]
            injected_probe["stderrUtf8"] += injected_line
            injected_stderr = injected_probe["stderrUtf8"].encode("utf-8")
            injected_probe["stderrRawSha256"] = AUTHORITY.sha256_bytes(
                injected_stderr
            )
            injected_probe["stderrByteLength"] = len(injected_stderr)
            injected_probe["logUtf8"] = (
                injected_probe["stdoutUtf8"] + injected_probe["stderrUtf8"]
            )
            injected_log = injected_probe["logUtf8"].encode("utf-8")
            injected_probe["logRawSha256"] = AUTHORITY.sha256_bytes(injected_log)
            injected_probe["logByteLength"] = len(injected_log)
            mutations.append(injected_rejection)

        boolean_alias_paths = (
            ("positiveCheckpointProbes", 0, "exitCode", False),
            ("positiveCheckpointProbes", 0, "readyClaimOccurrenceCount", True),
            ("positiveCheckpointProbes", 0, "boundFileByteMutationCount", False),
            (
                "positiveCheckpointProbes",
                0,
                "readyClaim",
                "commandCount",
                False,
            ),
            (
                "positiveCheckpointProbes",
                0,
                "readyClaim",
                "activeEventCount",
                False,
            ),
            ("argumentRejectionProbes", 0, "readyClaimOccurrenceCount", False),
        )
        for path in boolean_alias_paths:
            boolean_alias = copy.deepcopy(self.headless_execution)
            target = boolean_alias
            for token in path[:-2]:
                target = target[token]
            target[path[-2]] = path[-1]
            mutations.append(boolean_alias)

        for mutation in mutations:
            unsigned = dict(mutation)
            unsigned.pop("executionSha256", None)
            mutation["executionSha256"] = AUTHORITY.canonical_sha256(unsigned)
            self.assertRejected(
                lambda mutation=mutation: (
                    AUTHORITY.validate_headless_execution_authority(
                        mutation,
                        self.build,
                        self.engine_sha,
                    )
                )
            )

    def test_policy_projection_rejects_security_downgrades(self) -> None:
        mutations = []

        public_candidate = copy.deepcopy(self.policy)
        public_candidate["candidate"]["candidatePackageStatus"] = "PUBLIC_SELLABLE"
        mutations.append(public_candidate)

        network_restore = copy.deepcopy(self.policy)
        network_restore["managedBuild"]["isolationRequirements"][
            "networkPackageSourcesDisabled"
        ] = False
        mutations.append(network_restore)

        claims_erased = copy.deepcopy(self.policy)
        claims_erased["limitations"]["claimsNotAuthorized"] = []
        mutations.append(claims_erased)

        future_bar_missing = copy.deepcopy(self.policy)
        future_bar_missing["futureEventStatusBar"]["implementationPresent"] = False
        mutations.append(future_bar_missing)

        full_flow_alias = copy.deepcopy(self.policy)
        full_flow_alias["routeProfiles"]["fullFlow"]["profileId"] = (
            "TARGETED_DETERMINISTIC_CHECKPOINTS"
        )
        mutations.append(full_flow_alias)

        unknown_field = copy.deepcopy(self.policy)
        unknown_field["candidate"]["officialScore"] = 100
        mutations.append(unknown_field)

        forged_native_reachability = copy.deepcopy(self.policy)
        forged_native_reachability["storyAuthority"][
            "nativeReachabilityClaim"
        ] = True
        mutations.append(forged_native_reachability)

        disabled_rebuild = copy.deepcopy(self.policy)
        disabled_rebuild["storyAuthority"][
            "deterministicRebuildMustMatchStoredBytes"
        ] = False
        mutations.append(disabled_rebuild)

        disabled_version_probe = copy.deepcopy(self.policy)
        disabled_version_probe["engineAuthority"]["versionProbeRequired"] = False
        mutations.append(disabled_version_probe)

        forged_engine_executable = copy.deepcopy(self.policy)
        forged_engine_executable["engineAuthority"]["executable"][
            "rawSha256"
        ] = "sha256:" + "0" * 64
        mutations.append(forged_engine_executable)

        incomplete_source_closure = copy.deepcopy(self.policy)
        incomplete_source_closure["sourceAuthority"][
            "excludedFromR2GodotExecutableClosure"
        ]["allowlistIsComplete"] = False
        mutations.append(incomplete_source_closure)

        erased_package_inputs = copy.deepcopy(self.policy)
        erased_package_inputs["managedBuild"]["packageInputs"]["files"] = []
        mutations.append(erased_package_inputs)

        disabled_package_tree = copy.deepcopy(self.policy)
        disabled_package_tree["packageAuthority"]["treeHashRule"] = "NONE"
        mutations.append(disabled_package_tree)

        aliased_semantic_verifier = copy.deepcopy(self.policy)
        aliased_semantic_verifier["evaluatorProducerAuthority"][
            "semanticVerifierEntryPoint"
        ] = "verify_manifest"
        mutations.append(aliased_semantic_verifier)

        for mutation in mutations:
            self.assertRejected(
                lambda mutation=mutation: AUTHORITY.verify_policy_projection(
                    mutation,
                    self.manifest,
                )
            )

        self.assertRejected(
            lambda: AUTHORITY.build_manifest(
                self.build,
                self.engine_rows,
                self.engine_sha,
                self.headless_execution,
                self.policy,
                self.policy_bytes + b"\n",
            )
        )

    def test_engine_and_package_authority_are_exact(self) -> None:
        self.assertEqual(153, len(self.engine_rows))
        self.assertEqual(
            "sha256:68e768a771af633fb820b3939b7f24041aab0cf72157836a2448a0a8ab40c2b7",
            self.engine_sha,
        )
        executable = next(
            row for row in self.engine_rows
            if row["path"] == "Contents/MacOS/Godot"
        )
        self.assertEqual(
            "sha256:d11dc4a241ec29a347e13c8c7706e49433379ae1f9fc6a6e6819efb3891fce97",
            executable["rawSha256"],
        )
        self.assertEqual(4, len(self.build.package_rows))
        self.assertEqual(
            "sha256:cc1540b001de745eb111ed1efecc159aca828821d1297ec125e7ddbcf7f8933d",
            AUTHORITY.canonical_sha256(self.build.dotnet_rows),
        )
        self.assertEqual(
            "sha256:3328163c3829db2db44f282f48ef785a33114337b6956da4f48c6536ec590d42",
            AUTHORITY.canonical_sha256(self.build.package_rows),
        )

    def test_two_hostile_roots_have_identical_projection(self) -> None:
        with tempfile.TemporaryDirectory(prefix="realtime-candidate-two-root-") as raw:
            base = Path(raw)
            roots = (base / "hostile-a", base / "hostile-b")
            projections = []
            for index, root in enumerate(roots):
                root.mkdir()
                (root / "Directory.Build.targets").write_text(
                    '<Project><Target Name="Poison" BeforeTargets="Build">'
                    f'<Error Text="host poison {index}" /></Target></Project>',
                    encoding="utf-8",
                )
                (root / "NuGet.Config").write_text(
                    '<configuration><packageSources><clear />'
                    '<add key="poison" value="/does/not/exist" />'
                    '</packageSources></configuration>',
                    encoding="utf-8",
                )
                with AUTHORITY.isolated_managed_build(
                    REPOSITORY_ROOT,
                    scratch_parent=root,
                ) as result:
                    projections.append(result.deterministic_projection)
            self.assertEqual(projections[0], projections[1])

    def test_schema_and_policy_files_are_strict_json(self) -> None:
        schema = AUTHORITY.strict_json_bytes(
            (SCRIPT_DIR / "realtime-candidate-manifest.schema.json").read_bytes(),
            "candidate manifest schema",
        )
        self.assertFalse(schema.get("additionalProperties", True))
        self.assertIn("STRUCTURAL VALIDATION ONLY", schema.get("$comment", ""))
        self.assertIn(
            "verify_manifest_against_reconstructed_authority",
            schema.get("$comment", ""),
        )
        self.assertEqual(
            "gridworks.realtime-evaluator-candidate-policy.v1",
            self.policy["schemaVersion"],
        )
        validate_schema_subset(self.manifest, schema, schema)
        invalid_score = copy.deepcopy(self.manifest)
        invalid_score["scoreBearingCaptureAllowed"] = True
        with self.assertRaises(AssertionError):
            validate_schema_subset(invalid_score, schema, schema)
        invalid_extra = copy.deepcopy(self.manifest)
        invalid_extra["headlessExecutionAuthority"]["officialScore"] = 100
        with self.assertRaises(AssertionError):
            validate_schema_subset(invalid_extra, schema, schema)

        boolean_const_alias = copy.deepcopy(self.manifest)
        boolean_const_alias["headlessExecutionAuthority"][
            "positiveCheckpointProbes"
        ][0]["exitCode"] = False
        with self.assertRaises(AssertionError):
            validate_schema_subset(boolean_const_alias, schema, schema)

        duplicate_bound_paths = []
        for container_key in ("packageInputs", "generatedInputs", "outputs"):
            duplicate_path = copy.deepcopy(self.manifest)
            rows = duplicate_path["managedBuild"][container_key]
            rows[1]["path"] = rows[0]["path"]
            duplicate_bound_paths.append(duplicate_path)
        for duplicate_path in duplicate_bound_paths:
            with self.assertRaises(AssertionError):
                validate_schema_subset(duplicate_path, schema, schema)

        control_character_path = copy.deepcopy(self.manifest)
        control_character_path["engineAuthority"]["files"][0]["path"] = "safe\n"
        with self.assertRaises(AssertionError):
            validate_schema_subset(control_character_path, schema, schema)

        cross_bound_claims = copy.deepcopy(self.manifest)
        first_probe, second_probe = cross_bound_claims[
            "headlessExecutionAuthority"
        ]["positiveCheckpointProbes"]
        first_probe["readyClaim"], second_probe["readyClaim"] = (
            second_probe["readyClaim"],
            first_probe["readyClaim"],
        )
        first_probe["passClaim"], second_probe["passClaim"] = (
            second_probe["passClaim"],
            first_probe["passClaim"],
        )
        with self.assertRaises(AssertionError):
            validate_schema_subset(cross_bound_claims, schema, schema)
        AUTHORITY.verify_policy_projection(self.policy, self.manifest)


if __name__ == "__main__":
    unittest.main(verbosity=2)
