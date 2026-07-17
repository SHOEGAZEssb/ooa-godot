# Project principles

## Primary goal

The project exists to reproduce *The Legend of Zelda: Oracle of Ages* as
faithfully as practical in Godot 4.6/.NET. A cleaner abstraction is useful only
when it preserves the original game's data, order, timing, and observable
behavior. Visual similarity by itself is not sufficient.

When evidence conflicts, use this priority:

1. Executed behavior in the supported clean US ROM.
2. The corresponding code and data in `oracles-disasm`.
3. Importer-generated typed records that retain their source identity.
4. Runtime code and headless validations in this repository.
5. Assumptions, memories, or behavior inferred from screenshots.

The repository implementation should make the first two sources easy to trace,
not replace them with undocumented clone-specific rules.

## Fidelity rules

- Trace the relevant assembly handler and its data before implementing game
  behavior. Search for callers as well as the named routine.
- Preserve hexadecimal group, room, object, interaction, treasure, flag, and
  sound identifiers in diagnostics and source comments.
- Preserve source ordering. Object streams, RNG calls, script dispatch, and
  independent actor updates can affect later behavior even when the immediate
  result looks equivalent.
- Use the original 60-update timing and the original counter boundary. Do not
  convert a wait to a visually similar duration without checking whether the
  next operation occurs on the zero update or the following update.
- Preserve integer and fixed-point arithmetic where it affects movement,
  physics, RNG, audio, or animation. Floating-point interpolation must not
  become the authority for ROM-timed state.
- Keep gameplay coordinates in room space. Camera transforms are presentation;
  HUD, dialogue, fades, and menus use screen coordinates.
- Import general tables instead of encoding exceptions for one observed room.
  A room-specific branch is acceptable only when the original has one.
- Unsupported data must be rejected with source context or represented safely
  and explicitly. Never silently turn an imported opcode, variable, object, or
  treasure behavior into a no-op.
- Use the single game RNG for behavior that consumes the original global RNG.
  Never introduce `Random.Shared` into deterministic gameplay.

## Architecture consequences

Generated data is the boundary between the disassembly and production runtime.
The game should not parse assembly sources while it is running. Importers must
retain enough metadata to diagnose the original row, label, or handler that
produced invalid runtime data.

Runtime owners should follow original ownership where it affects behavior, but
they do not need to mimic the Game Boy's file layout class-for-class. Shared
clone-side components are appropriate for genuinely shared mechanics such as
fixed-update animation or enemy combat. Species state machines, native
cutscene objects, and counter semantics stay separate when the source does.

Stable Godot nodes belong in scenes and are referenced through unique names.
Dynamic room entities, effects, and actors are created by their owning runtime
systems. Production code contains only behavior needed by the game; audit
traces and regression orchestration belong in the validation assembly.

## Required workflow

For a behavioral change:

1. Locate the source code, tables, and all relevant callers in the disassembly.
2. Record exact inputs, state, ordering, timing, arithmetic, and side effects.
3. Extend the importer if the runtime does not already receive that information.
4. Implement the smallest general runtime behavior that matches the evidence.
5. Add a headless regression for the reported case and important branches.
6. Regenerate data when applicable, build with zero warnings, and run the full
   validation suite.
7. Update the relevant documentation when the change establishes or alters a
   durable rule.

A feature is complete only when its imported data, runtime effects, persistence,
transition behavior, re-entry behavior, and timing all agree with the source for
the supported paths. "It looks right in one room" is evidence, not completion.
