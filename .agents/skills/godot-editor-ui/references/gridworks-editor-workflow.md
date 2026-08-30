# Gridworks editor workflow

Use this reference together with the current repository files. If a command, scene, or validation rule has changed, the
current `README.md`, `dev`, and `game/realtime/r2/RealtimeVisualLayout.cs` take precedence over this summary.

## Launch and identify the app

- Canonical launch: `./dev play layout`
- Canonical scene: `game/realtime/r2/RealtimeVisualLayoutAuthoring.tscn`
- Bundled app relative to repository root: `.tools/godot-4.7.1/Godot_mono.app`
- Expected editor title: `RealtimeVisualLayoutAuthoring.tscn - Gridworks - Godot Engine`
- A title such as `Gridworks (DEBUG)` identifies a running game, not the editor.

Run the launch command in a retained PTY because the Godot process stays active. Resolve the app target from the output of
`pwd -P` plus the bundled app-relative path. Targeting the full app path avoids ambiguity with other Godot installations.

After importing `@oai/sky` in `node_repl`, inspect the editor with its resolved app path. Use a fresh state after every
action. Prefer AX elements and only then fall back to screenshots and coordinates. Godot's canvas and some Inspector
controls may be only partially represented in the accessibility tree, so visually confirm selection and values before save.

## Canonical editable nodes

Do not treat this list as permission to change every node. Select only what the active scope authorizes.

- Sources: `WEST_SOURCE_NODE`, `SOUTH_SOURCE_NODE`
- Districts: `WATER_TERMINAL`, `NORTH_RESIDENTIAL_TERMINAL`, `EAST_RESIDENTIAL_TERMINAL`,
  `HOSPITAL_TERMINAL`, `FACTORY_TERMINAL`
- Roads: `west_source_service`, `south_source_service`, `east_city_spine`, `waterworks_branch`,
  `north_residential_branch`, `east_residential_branch`, `hospital_branch`, `industrial_access`,
  `south_city_spine`

Use `Position` and matching X/Y `Scale` values for source and district `Sprite2D` nodes. Use the 2D point-editing tool for
road `Line2D` nodes. Preserve exact IDs, child types, metadata, and hierarchy because runtime projection is strict.

## Save and prove reproduction

Before saving, note which node and property should change. Save with `super+s`, refresh editor state, and verify the scene
title no longer indicates an unsaved change. Inspect `git diff -- game/realtime/r2/RealtimeVisualLayoutAuthoring.tscn` to
confirm serialized values match the UI and no unrelated scene property moved.

Run the Realtime check project before the full repository check. Then use a fresh `./dev play chapter FIRST_LIGHT` process
for normal-game observation. Keep editor and game sessions distinct so the game can be closed without closing the editor.
If the requested visual relationship cannot be judged at the current zoom, inspect both a composition-wide view and a
closer view of road/gate/label contacts before concluding.

## Awake state

Only when requested, check for a current `caffeinate -dims` process and start it if absent. Do not launch duplicates. The
flags keep display, idle system, disk, and system sleep assertions active while the process lives; terminate only the
specific process the task owns if the user later asks to stop it.
