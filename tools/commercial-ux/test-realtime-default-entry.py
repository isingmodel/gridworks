#!/usr/bin/env python3
"""Verify that the product entry point owns the current R2 title flow."""

from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "game/project.godot"
DEFAULT_SCENE = "res://realtime/r2/RealtimeSliceMain.tscn"
DEFAULT_SCENE_FILE = ROOT / "game/realtime/r2/RealtimeSliceMain.tscn"
UI_ROOT_SCENE = ROOT / "game/realtime/ui/RealtimeUiRoot.tscn"
LAUNCH_CATALOG = ROOT / "game/realtime/r2/RealtimeLaunchCatalog.cs"
DEV = ROOT / "dev"


def main() -> None:
    assert PROJECT.is_file(), f"missing Godot project: {PROJECT}"
    project = PROJECT.read_text(encoding="utf-8")
    assert f'run/main_scene="{DEFAULT_SCENE}"' in project, (
        "the Godot product entry must be the live R2 scene, "
        f"expected {DEFAULT_SCENE}"
    )
    assert DEFAULT_SCENE_FILE.is_file(), f"missing default R2 scene: {DEFAULT_SCENE_FILE}"
    scene = DEFAULT_SCENE_FILE.read_text(encoding="utf-8")
    assert 'path="res://realtime/r2/RealtimeSliceMain.cs"' in scene, (
        "the canonical default scene no longer owns the RealtimeSliceMain controller"
    )
    assert 'run/main_scene="res://CommercialMain.tscn"' not in project, (
        "the frozen V2 CommercialMain scene must not remain the product entry"
    )
    ui_root = UI_ROOT_SCENE.read_text(encoding="utf-8")
    assert 'path="res://realtime/ui/RealtimeProductTitle.tscn"' in ui_root, (
        "the current R2 UI root no longer owns the product title"
    )
    launch = LAUNCH_CATALOG.read_text(encoding="utf-8")
    assert "arguments.Length == 0" in launch and (
        "RealtimeLaunchSelection.ProductTitle" in launch
    ), "no-argument boot must select the product title"
    dev = DEV.read_text(encoding="utf-8")
    assert "--technical-fixture" in dev, (
        "the development fixture must remain an explicit launch"
    )
    print(f"PASS: canonical product title entry is {DEFAULT_SCENE}")


if __name__ == "__main__":
    main()
