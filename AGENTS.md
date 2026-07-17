# Oracle of Ages Godot Port - Agent Guide

## Mission and authority

This repository reconstructs *The Legend of Zelda: Oracle of Ages* in Godot
4.6/.NET. The highest priority is a 1:1 clone of the supported original game,
not an approximation or reinterpretation.

Use this evidence order when behavior is uncertain:

1. Executed behavior in the supported clean US ROM.
2. The corresponding code and data in `oracles-disasm`.
3. Importer-generated typed runtime records.
4. Runtime code and headless validations in this repository.
5. Assumptions, memory, screenshots, or visual approximation.

Read [Project principles](docs/project-principles.md) before changing gameplay.
Do not approximate timing, coordinates, collision, animation, object order,
RNG consumption, transitions, audio, or state when they can be traced.

Local development paths:

```text
Project root:       E:\Stuff\Github\ooa-godot
Oracle disassembly: C:\msys64\home\timst\oracles-disasm
Supported ROM:      Legend of Zelda, The - Oracle of Ages (U) [C][!].gbc
Godot console:      E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64_console.exe
```

## Documentation map

The [documentation index](docs/README.md) is the canonical guide list. Read the
guide for the subsystem being changed:

| Work area | Required guide |
| --- | --- |
| Fidelity decisions and definition of done | [Project principles](docs/project-principles.md) |
| Setup, commands, controls, and change workflow | [Development workflow](docs/development.md) |
| ROM/disassembly parsing or generated formats | [Data import](docs/data-import.md) |
| Scene ownership, controllers, or update order | [Runtime architecture](docs/runtime-architecture.md) |
| Rooms, transitions, entities, placement, or RNG | [Rooms and entities](docs/rooms-and-entities.md) |
| Map/inventory modals, fades, or input freezing | [Menus and input](docs/menus-and-input.md) |
| WRAM fields, inventory, flags, checkpoints, or persistence | [Saves and runtime state](docs/saves-and-state.md) |
| PNG/OAM rendering, caches, palettes, sound, or audio RNG | [Graphics and audio](docs/graphics-and-audio.md) |
| Script-driven or native cutscenes | [Cutscene command runner](docs/command-runner.md) |
| Regression design and test assembly boundaries | [Validation](docs/validation.md) |

[Implementation status](docs/implementation-status.md) summarizes current
player-visible coverage. [TODO.md](TODO.md) tracks planned or tentative
engineering work.

## Repository layout

- `src/`: Godot C# production runtime.
- `scenes/`: stable Godot scene trees.
- `validation/`: separate headless validation project, scene, and runner.
- `tools/import_oracles.ps1`: stable ROM/disassembly import entry point.
- `tools/import_oracles/`: staged import implementation.
- `assets/oracle/`: generated runtime assets; never hand-edit these files.
- `docs/`: durable architecture and workflow documentation.
- `README.md`: concise project entry point and quick start.
- `TODO.md`: incomplete consolidation work and deliberate hard-maybe items.

## Working rules

1. Inspect `git status --short` before editing and preserve unrelated user
   changes. The worktree is commonly dirty between feature slices.
2. Use `rg` or `rg --files` for repository and disassembly searches. Search
   callers and data tables, not only a routine with a promising name.
3. Use `apply_patch` for source and documentation edits.
4. Change importer code when runtime data is incomplete or wrong, then
   regenerate assets. Never repair a generated file manually.
5. Preserve source ordering, original integer/fixed-point arithmetic, global
   RNG calls, and exact 60-update counter boundaries.
6. Do not encode one-room exceptions when the original tables or handlers
   describe a general rule.
7. Keep gameplay coordinates in original room/world space. HUD, dialogue,
   fades, menus, and debug overlays use screen space outside camera transforms.
8. Runtime databases consume generated assets and do not parse disassembly
   source files during play.
9. Unsupported imported behavior must fail with source-aware diagnostics or be
   represented explicitly and safely. Do not silently skip it or invent quest
   progression.
10. Add or extend a headless validation for every regression and newly
    supported gameplay system.
11. Keep audit traces and validation-only state in the validation assembly, not
    in production classes.
12. Update documentation in the same change when a durable contract, workflow,
    player-visible feature, development control, or deferred limitation changes.

## Documentation updates

Use the narrowest appropriate document:

- Update `README.md` only for top-level scope, requirements, quick-start, or
  documentation navigation.
- Update `docs/implementation-status.md` for player-visible coverage or major
  deferred systems.
- Update `docs/development.md` for controls, launch arguments, or contributor
  workflow.
- Update the subsystem guide when an ownership boundary, invariant, file format,
  or implementation rule changes.
- Update `TODO.md` for work that remains planned; cross off completed staged
  items instead of deleting their context.

Do not turn the README back into a chronological implementation log.

## Importing assets

Run the importer after changing parsing or generated formats:

```powershell
& .\tools\import_oracles.ps1
```

The importer accepts `-Rom` and `-Disassembly` overrides and verifies this clean
US ROM MD5 before generating assets:

```text
C4639CC61C049E5A085526BB6CAC03BB
```

If a generated binary format changes, update its runtime reader in the same
change and validate the exact expected byte count or format version. Generated
output must remain deterministic.

## Building and running

Build the solution, including the production and validation assemblies:

```powershell
dotnet build
```

The build must complete with zero warnings and zero errors.

Launch the normal title/file flow from the repository root:

```powershell
& 'E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe' --path .
```

Launch directly in a hexadecimal group and room by placing project arguments
after `--`:

```powershell
& 'E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe' --path . -- --group=4 --room=04
```

Direct room launches bypass normal menu and checkpoint progression and are a
development facility, not evidence of retail behavior.

## Headless validation

Use the console executable and `--quit-after` so validation runs terminate:

```powershell
$godot = 'E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64_console.exe'
& $godot --headless --path . --quit-after 10 -- --validate
```

The single `--validate` flag runs all world-data and gameplay scenarios and
selects canonical rooms automatically. Before handoff, also run:

```powershell
git diff --check
git status --short
```

Do not discard or rewrite unrelated modifications shown by `git status`.

## Disassembly starting points

- Warp sources: `data/ages/warpSources.s`
- Warp destinations: `data/ages/warpDestinations.s`
- Dungeon layouts: `data/ages/dungeonLayouts.s`
- Dungeon metadata: `data/ages/dungeonData.s`
- Transition constants: `constants/common/transitions.s`
- Link state and warp behavior: `object_code/common/specialObjects/link.s`
- Tilesets: `data/ages/tilesets.s`
- NPC/interaction tables: `data/ages/interactions.s`
- Interaction code: `object_code/ages/interactions/`

Preserve hexadecimal identifiers in importer diagnostics, source comments, and
validation failures when they correspond to original rooms, tiles, objects,
interactions, treasures, flags, transitions, or sound IDs.

## Critical runtime invariants

- Small rooms are 10 by 8 metatiles (160 by 128 pixels).
- Large rooms use a 16 by 11 storage grid and 16-byte row stride; only 15 by 11
  metatiles (240 by 176 pixels) are playable, with a padding column.
- The viewport is 160 by 144; the gameplay field is 160 by 128 and the HUD is
  the bottom 16 pixels.
- Dungeon neighbors come from imported dungeon floor layouts, not room-ID
  arithmetic.
- Ordinary destination entities and room events remain frozen during scrolling
  even though the destination is preloaded.
- Enemy placement uses one ordered object stream and shared reservation set.
  Its placement buffer is regenerated once per room parse using the game-wide
  RNG and the original 256 RNG calls.
- Live WRAM-style state is written to disk only by explicit save flows; ordinary
  mutations and application exit do not autosave.
- Script-driven cutscenes use the typed command runner only when that matches the
  original mechanism. Native state machines remain native.
