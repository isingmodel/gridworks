#!/usr/bin/env python3
"""Verify that the product entry point is the canonical live R2 scene."""

from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "game/project.godot"
DEFAULT_SCENE = "res://realtime/r2/RealtimeSliceMain.tscn"
DEFAULT_SCENE_FILE = ROOT / "game/realtime/r2/RealtimeSliceMain.tscn"


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
    print(f"PASS: canonical product entry is {DEFAULT_SCENE}")


if __name__ == "__main__":
    main()
