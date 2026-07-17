# Project documentation

This directory records the engineering rules that should remain true as the
Oracle of Ages reconstruction grows. The original ROM and disassembly are the
authority; these guides explain how that evidence is carried through the
importer, runtime, and validation suite.

| Guide | Purpose |
| --- | --- |
| [Project principles](project-principles.md) | Fidelity priorities, evidence rules, and the definition of done |
| [Development workflow](development.md) | Setup, build, launch, controls, and the normal change workflow |
| [Data import](data-import.md) | ROM/disassembly import stages and generated-asset rules |
| [Runtime architecture](runtime-architecture.md) | Scene ownership, controller boundaries, and update order |
| [Rooms and entities](rooms-and-entities.md) | Room coordinates, transitions, object ordering, enemy placement, and entity lifetime |
| [Menus and input](menus-and-input.md) | Modal ownership, fixed-update fades, and gameplay pause leases |
| [Saves and runtime state](saves-and-state.md) | WRAM-backed state, persistence, transactions, and checkpoints |
| [Graphics and audio](graphics-and-audio.md) | Resource caching, OAM composition, palettes, and deterministic sound behavior |
| [Cutscene command runner](command-runner.md) | Choosing, importing, implementing, and validating cutscene commands |
| [Validation](validation.md) | Test assembly design, regression expectations, and commands |
| [Implementation status](implementation-status.md) | High-level coverage and intentionally deferred systems |

The root [README](../README.md) is the project entry point. [TODO.md](../TODO.md)
tracks engineering work that is planned or deliberately tentative; it should
not be used as a substitute for the rules in these documents.

## Documentation maintenance

Update the relevant guide whenever a change alters a durable contract, file
format, ownership boundary, or contributor workflow. Keep chronological
implementation notes out of the README. A player-visible feature belongs in
the implementation-status summary; a remaining engineering task belongs in
`TODO.md`.

Use repository-relative links and run a link check after moving a document.
Generated files under `assets/oracle/` are never documentation sources and
must not be edited by hand.
