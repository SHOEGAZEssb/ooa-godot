# Project principles

## Target

Reproduce *The Legend of Zelda: Oracle of Ages* as executed by the supported
clean US ROM. A cleaner abstraction is useful only when it preserves the
original data, order, timing, arithmetic, and observable behavior. Visual
similarity in one room is not enough.

Use evidence in this order:

1. Executed behavior in the clean ROM.
2. Corresponding code and data in `oracles-disasm`.
3. Generated typed records that retain source identity.
4. Runtime behavior and headless validations in this repository.
5. Assumptions, memory, screenshots, or visual approximation.

The implementation should make the first two sources easy to trace rather than
replace them with undocumented clone-specific rules.

## Fidelity rules

- Trace handlers, callers, data tables, and object placement before coding.
- Preserve source ordering and global RNG consumption even when the immediate
  visible result seems unchanged.
- Preserve the original 60-update counter boundaries. Record whether work
  happens on the zero update or the following update.
- Preserve byte, integer, and fixed-point arithmetic where it affects behavior.
  Render interpolation must not become authoritative game state.
- Keep gameplay coordinates in room/world space. Camera transforms are
  presentation; fixed UI uses screen space.
- Import general tables instead of encoding observed room exceptions. A
  room-specific branch is correct only when the original has one.
- Use the shared game RNG for original random behavior. Do not introduce a
  private or nondeterministic generator into gameplay.
- Reject unsupported data with actionable source context or represent it
  explicitly and safely. Never convert an unknown opcode, object, variable, or
  treasure mode into a silent no-op.
- Retain original hexadecimal identifiers in diagnostics, source comments, and
  validation failures.

## Architecture consequences

Generated assets are the boundary between disassembly and runtime. Importers
retain enough source identity to explain invalid data; production code never
parses assembly files during play.

Runtime ownership follows the original mechanism where order and lifecycle
matter, without copying the Game Boy file layout class-for-class. Shared
components are appropriate for genuinely shared mechanics. Species state
machines, native cutscenes, and counter semantics stay distinct when the source
does.

Keep one authoritative owner for state. Features request changes through that
owner instead of mirroring save bytes, room identity, RNG, inventory, modal, or
transition state. Stable Godot nodes live in scenes; content-dependent actors
and effects are created by their runtime systems. Validation-only traces stay
out of production objects.

## Definition of done

For a behavioral change:

1. Record the original inputs, state, ordering, timing, arithmetic, and side
   effects.
2. Extend the importer if the runtime does not receive the required source
   information.
3. Implement the smallest general behavior that matches the evidence.
4. Add a focused headless regression for the reported case and meaningful
   branches, including re-entry or persistence where applicable.
5. Regenerate affected data, build with zero warnings, and run the full suite.
6. Update documentation only when a durable contract or broad coverage boundary
   changed.

A feature is complete when its data, runtime effects, timing, transitions,
persistence, and re-entry agree with the original for the supported paths.
