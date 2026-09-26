# Engineering documentation

These guides explain how evidence from the original game moves through the
importer, runtime, and validation suite. They are written for contributors and
coding agents; each guide should be useful without becoming an inventory of
everything already implemented.

## Start here

1. Read [Project principles](project-principles.md) for any gameplay change.
2. Read [Development](development.md) for setup, commands, controls, and the
   normal change cycle.
3. Select one subsystem guide from the task map below.
4. Use [Validation](validation.md) when designing the regression.

Do not read every guide before a small change. The task map is the intended
entry point.

## Task map

| If you are changing... | Read |
| --- | --- |
| Project fidelity, evidence, or definition of done | [Project principles](project-principles.md) |
| Setup, commands, controls, or contributor workflow | [Development](development.md) |
| Generated data, assembly parsing, schemas, or import stages | [Data import](data-import.md) |
| Composition, scene ownership, fixed updates, or input ordering | [Runtime architecture](runtime-architecture.md) |
| Rooms, transitions, terrain, entities, enemies, placement, or RNG | [Rooms and entities](rooms-and-entities.md) |
| NPCs, native interactions, linked actors, or room events | [NPCs and events](npcs-and-events.md) |
| Which imported NPC records are implemented, partial, or unsupported | [NPC interaction coverage](npc-interaction-coverage.md) |
| An `interactionRunScript` stream or a new script command | [Command runner](command-runner.md) |
| Menus, dialogue/modal input, fades, or pause ownership | [Menus and input](menus-and-input.md) |
| WRAM fields, flags, inventory, checkpoints, or disk persistence | [Saves and state](saves-and-state.md) |
| Imported graphics, OAM, palettes, caching, sound, or audio RNG | [Graphics and audio](graphics-and-audio.md) |
| Generated-asset mod manifests, ordering, and overrides | [Generated-asset mods](modding.md) |
| A regression, fixture, trace, or validation boundary | [Validation](validation.md) |
| Broad playable coverage or major missing systems | [Implementation status](implementation-status.md) |

## Documentation boundary

Keep durable evidence rules, ownership, lifecycle, ordering, format contracts,
and contributor workflow here. Put source-specific constants and branch details
beside code and focused validations. Do not append implementation diaries,
class inventories, per-feature test summaries, or unowned plans.

Fidelity audits and working findings belong in untracked `local-audits/`,
excluded through `.git/info/exclude`. Do not link tracked guides to them.

The [NPC coverage ledger](npc-interaction-coverage.md) is the deliberate
room-by-room inventory exception: update affected entries, summary counts, and
the dated snapshot when coverage changes. Keep
[implementation status](implementation-status.md) at broad playable boundaries
and major limitations.

When removing or moving documentation, update repository-relative links and
run `git diff --check`. Generated `assets/oracle/` files are never hand-edited.
