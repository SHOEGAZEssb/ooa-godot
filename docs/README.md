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
| A regression, fixture, trace, or validation boundary | [Validation](validation.md) |
| Broad playable coverage or major missing systems | [Implementation status](implementation-status.md) |

## What belongs in documentation

Keep a rule here when a future contributor needs it to make the right design
choice before reading an implementation. Good documentation covers:

- source-of-truth and evidence rules;
- ownership and lifecycle boundaries;
- update order, coordinate systems, persistence, and RNG contracts;
- generated formats and failure behavior;
- normal implementation and verification workflow.

Do not copy information that is more reliably found in code or generated data:

- exhaustive class or file lists;
- room-by-room and interaction-by-interaction coverage;
- long catalogs of constants, WRAM addresses, sounds, or validation methods;
- chronological implementation notes;
- planned work without an active owner.

Source-specific details belong next to their implementation and in focused
validations, with original labels and hexadecimal IDs retained. The deliberate
exception is the navigable [NPC interaction coverage](npc-interaction-coverage.md)
ledger. Agents must update its row, summary counts, and snapshot date whenever
implemented NPC coverage or classification changes. The concise
[implementation status](implementation-status.md) records only broad coverage
and major limitations.

When moving or removing a guide, update all repository-relative links and run
`git diff --check`. Generated files under `assets/oracle/` are outputs, never
documentation sources.
