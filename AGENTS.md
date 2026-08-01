# Oracle of Ages Godot port: agent guide

## Goal and source of truth

This repository reconstructs *The Legend of Zelda: Oracle of Ages* in Godot
4.7.1/.NET. The target is the supported clean US game, not a reinterpretation.

When behavior is uncertain, use evidence in this order:

1. Executed behavior in the clean US ROM.
2. Code and data in `oracles-disasm`.
3. Generated typed data in this repository.
4. Runtime code and headless validations.
5. Assumptions, memory, screenshots, or visual approximation.

Read [Project principles](docs/project-principles.md) before changing gameplay.
Use the [documentation index](docs/README.md) to select the one subsystem guide
needed for the task. Do not read every guide by default.

Local paths:

```text
Repository:     E:\Stuff\Github\ooa-godot
Disassembly:    C:\msys64\home\timst\oracles-disasm
Godot console:  E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64_console.exe
```

## Start every task this way

1. Run `git status --short`; preserve unrelated user changes.
2. Locate the runtime owner, importer stage, generated data, and validation for
   the feature. Search callers and source tables, not only a promising label.
3. Trace the original inputs, order, counters, arithmetic, side effects, and
   persistent state before designing the change.
4. Extend the importer first if the runtime lacks source information. Never
   hand-edit `assets/oracle/`.
5. Implement the smallest general rule supported by the source. Do not add a
   room exception unless the original has one.
6. Add or extend a focused headless regression.
7. Build, run the full suite, and update only documentation whose durable
   contract or high-level coverage changed.

Use `rg` or `rg --files` for searches and `apply_patch` for edits.

## Non-negotiable implementation rules

- Preserve original object/table order, global RNG consumption, integer and
  fixed-point arithmetic, and exact 60-update counter boundaries.
- Keep gameplay state in room/world coordinates. Camera offsets are
  presentation. HUD, dialogue, fades, menus, and debug overlays use screen
  space.
- Production runtime reads generated assets, never disassembly source files.
- Unsupported imported behavior must fail with source-aware diagnostics or be
  represented explicitly and safely. Never silently skip an opcode, row, or
  state transition.
- Keep one authoritative owner for room identity, save bytes, inventory, RNG,
  transitions, modal state, and audio. Do not mirror their state in a feature.
- Stable nodes belong in scenes; content-dependent entities and effects are
  created by their runtime owner.
- Validation-only traces and orchestration stay in the validation assembly.
- Preserve hexadecimal IDs in diagnostics, validation failures, and source
  comments when they identify original rooms, objects, interactions,
  treasures, flags, transitions, or sounds.

Important world invariants:

- Small rooms are 10 by 8 metatiles (160 by 128 pixels).
- Large-room storage is 16 by 11 metatiles with a 16-byte row stride; the last
  column is padding and the playable area is 15 by 11.
- The viewport is 160 by 144. The HUD is 16 pixels high and the gameplay field
  is 160 by 128.
- Dungeon neighbors come from imported floor layouts, not room-ID arithmetic.
- Destination entities and room events remain frozen during scrolling.
- Enemy placement consumes one ordered object stream and the shared placement
  buffer generated from the game RNG.
- Live WRAM-style state is written to disk only by explicit save flows.

## Repository map

| Path | Ownership |
| --- | --- |
| `src/Application/` | Composition, fixed-update scheduling, input, pause ownership |
| `src/Features/` | Gameplay and presentation features by owning use case |
| `src/Infrastructure/` | Generated-data and external boundaries |
| `src/Shared/` | Small behavior-neutral primitives |
| `scenes/` | Stable Godot scene trees |
| `tools/import_oracles.ps1` | Import entry point and stage contracts |
| `tools/import_oracles/` | Feature import stages |
| `assets/oracle/` | Generated runtime data; never hand-edit |
| `validation/` | Separate headless validation project |
| `docs/` | Durable engineering guides and high-level status |

Place a production type with the feature that owns it. Follow the original
dispatch boundary: a shared enemy or interaction does not belong to the first
dungeon that happens to use it. Keep one class or interface per C# file, with
the filename matching the type; narrow records and enums may stay beside their
owner.

## Commands

```powershell
& .\tools\import_oracles.ps1
& .\tools\verify_oracle_import.ps1
dotnet build

$godot = 'E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64_console.exe'
& $godot --headless --path . --quit-after 10 -- --validate

git diff --check
git status --short
```

Run the importer only when import code or generated inputs changed.
`verify_oracle_import.ps1` is required for parser, stage-boundary, schema, or
determinism changes. The build must have zero warnings and errors.

For a focused validation during development:

```powershell
& $godot --headless --path . --quit-after 10 -- --validate --validate-only=ValidateMethodName
```

Run the complete suite before handoff.

## Documentation rule

Documentation explains durable decisions: evidence, ownership, invariants,
file-format contracts, and contributor workflow. It does not duplicate class
inventories, generated tables, per-room implementation notes, or validation
method contents that are easier to discover with `rg`.

The intentional exception is the navigable
`docs/npc-interaction-coverage.md` ledger. Any change that implements, extends,
partially supports, suppresses, or reclassifies an imported NPC record must
update its status, room entry, summary counts, and dated snapshot in the same
change.

- Change `README.md` only for project scope, setup, quick start, or navigation.
- Change a subsystem guide when its ownership or durable contract changes.
- Change `docs/npc-interaction-coverage.md` whenever NPC coverage changes.
- Change `docs/implementation-status.md` only for high-level player-visible
  coverage or a major limitation.
- Put source-specific detail in importer diagnostics, code comments beside the
  implementation, and focused validations.
