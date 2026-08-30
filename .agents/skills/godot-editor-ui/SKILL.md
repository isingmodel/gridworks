---
name: godot-editor-ui
description: Operate Gridworks' actual Godot Editor development UI to arrange buildings, power-source campuses, and roads in the canonical visual-layout scene, save through the editor, and verify the result in the normal game. Use for requests to open or directly manipulate the Godot/Godo UI, Inspector, Scene tree, 2D view, or visual placement; do not use for code-only or headless-only changes.
---

# Gridworks Godot Editor UI

Use the real Godot Editor as the authoring surface. A running `Gridworks (DEBUG)` game window, a custom in-game overlay,
or direct text editing of the `.tscn` file does not satisfy a request to edit through the Godot UI.

## Prepare

1. Read `README.md`, `docs/ACTIVE_SCOPE.md`, and `docs/AGENT_GUIDE.md` in repository order.
2. Read the complete `computer-use:computer-use` skill before any UI action. Use its `node_repl` + `@oai/sky`
   workflow exclusively for Mac UI interaction unless the user explicitly requests another technology.
3. Read [the Gridworks editor workflow](references/gridworks-editor-workflow.md) before launching or manipulating Godot.
4. Run `git status --short`. Preserve unrelated work. Before changing project files, open one authorized result in
   `docs/ACTIVE_SCOPE.md` and commit that scope separately, as the repository guide requires.
5. Resolve the repository root with `pwd -P`; do not embed a developer-specific absolute path in project files.

If Computer Use or its required `node_repl` interface is unavailable, report that UI operation is blocked. Do not silently
substitute shell or patch edits for an explicit request to use the editor.

## Operate the actual editor

1. Check whether the canonical authoring scene is already open. Avoid duplicate editor processes.
2. Otherwise launch `./dev play layout` in a retained PTY session. This builds the game and opens
   `res://realtime/r2/RealtimeVisualLayoutAuthoring.tscn` with `--editor`.
3. Target the repository-bundled `Godot_mono.app` by its resolved full app path. Multiple Godot installations may share a
   display name or bundle identifier.
4. Confirm all three editor surfaces are visible or discoverable before editing: the `Scene` tree, the `2D` workspace, and
   the `Inspector`. Confirm the title names `RealtimeVisualLayoutAuthoring.tscn` and Godot Engine.
5. Fetch fresh app state before every decision. Prefer current accessibility `element_index` values; use the screenshot and
   coordinate actions only when Godot does not expose the needed control. Never reuse a stale element index.
6. Select the exact node in the Scene tree, edit its property in Inspector or its handles in 2D, then inspect the fresh UI
   state to confirm the intended value. Make small, reversible changes and reassess the whole composition after each group.
7. Save with `super+s`. Confirm the unsaved marker clears and that
   `game/realtime/r2/RealtimeVisualLayoutAuthoring.tscn` is the only newly changed gameplay file unless the authorized
   result requires more.

When the user asks to keep the Mac awake, verify an existing `caffeinate -dims` process or start one without duplicating it.
Report its state in the handoff. This request does not authorize unrelated system-setting changes.

## Preserve scene authority

- `Districts/*` and `Sources/*` remain exact-ID `Sprite2D` children. `Roads/*` remain exact-ID `Line2D` children. Do not
  rename, add, delete, or reparent canonical children unless a separately authorized schema change requires it.
- Change visual placement through `Position`, positive uniform `Scale`, or `Line2D` points. Do not change Core power-grid
  coordinates, supply radius, output, heat, economy, story, or save data while performing a visual-layout task.
- Keep every projected point within X `-200..3400` and Y `-200..2300`, every sprite world maximum side within
  `200..1400`, and every road at `2..12` points. Preserve required metadata and allowed road styles.
- Maintain intentional road-to-campus gate contact when moving or scaling a source. Judge buildings as a composition with
  terrain, river, bridges, roads, labels, footprints, and neighboring districts—not as isolated sprites.
- For raster creation or editing, invoke the separate `imagegen` skill when applicable; return here for actual Godot import,
  placement, and runtime verification.

## Verify and hand off

1. Inspect the scene diff for only the intended serialized property changes.
2. Run the smallest deterministic projection check first:
   `dotnet run --project tools/Gridworks.RealtimeChecks -c Release`.
3. Run `./dev check` when the active scope or repository change class requires the full integration regression.
4. Start a fresh normal process with `./dev play chapter FIRST_LIGHT`. Confirm the saved placement is reproduced in the
   game rather than merely present in the editor. Capture editor and normal-game screenshots when visual evidence is part
   of the request.
5. Close only the normal verification process. If the user asked to keep editing, leave the authoring scene open in the
   actual Godot Editor. Track the process/session you launched; do not kill every Godot process indiscriminately.
6. Distinguish deterministic projection PASS, one native-screen observation, and human visual approval. None substitutes
   for the others.
7. Update only documents that own changed facts, close `docs/ACTIVE_SCOPE.md`, commit the major unit, and do not push,
   create a PR, merge, package, or release without explicit user authorization.
