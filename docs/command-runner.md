# Command runner

## When to use it

Use `CutsceneCommandRunner` when the original behavior is an
`interactionRunScript` stream, or when an imported controller stream faithfully
represents several coordinated source scripts. Keep native cutscenes,
special-object state machines, palette threads, and transition engines in
specialized controllers.

The owner is still an ordinary entity, linked interaction, or room event. See
[NPCs and events](npcs-and-events.md) before choosing the runner.

Core files:

- `tools/import_oracles/Import-CutsceneData.ps1`
- `src/Features/Story/Commands/CutsceneCommandSchema.cs`
- `src/Features/Story/Commands/CutsceneCommandCatalog.cs`
- `src/Features/Story/Commands/CutsceneCommandRunner.cs`
- `src/Features/Story/Commands/ICutsceneCommandHost.cs`
- `src/Features/Story/Commands/CutsceneCommandLaneScheduler.cs`

## Generated commands

Generated rows retain a stable script ID, assembly label, command index, source
line, normalized opcode, optional actor, typed arguments, and payload. The
generated command vocabulary is the contract for source aliases, operand
shapes, command record type, allowed results, actor members, and required host
capabilities.

The importer rejects an undeclared or malformed command. Runtime loading checks
the schema again. Errors must identify the script, label, index, source line,
opcode, and bad operand or actor. Do not emit scene paths or transient node
names; actors use stable semantic IDs which the host binds to current runtime
objects.

## Fixed-update semantics

Every command returns an explicit source-equivalent result:

| Result | Behavior |
| --- | --- |
| `Continue` | Dispatch the next command in the same update |
| `Yield` | Save the next command and stop until the next update |
| `Block` | Keep updating the current command |
| `End` | Finish and deactivate the stream |

Determine this from the source command handler and carry behavior, not from the
command's English meaning. Similar flag writes or waits may have different
boundaries. Preserve separately:

- whether a counter dispatches on its zero update or the following update;
- script waits versus preloaded native counters;
- taken and missed branch boundaries;
- `callscript` and `retscript` yields;
- textbox open, close, and post-close boundaries;
- movement completion and restoration of actor pose;
- whether a sound, music, item, or state operation continues or yields.

The runner advances at original 60 Hz updates. Tweening and rendered delta are
never authoritative for command movement or timing.

## Hosts and actor bindings

A command host translates stable actor IDs into runtime actors and implements
only the operations owned by its event or entity. Hosts are default-deny: an
unsupported capability fails with current command source context instead of
doing nothing.

Validate all required actors before execution. The host owns actor-specific
position, animation, visibility, collision, deletion, dialogue, inventory,
input, and native-handler operations. Common forwarding may be shared, but
source-specific state changes remain explicit.

Always clear a runner when its owner is cancelled, its room invalidates actor
bindings, or a native completion path takes ownership. Input leases and actor
registrations must be released on every terminal path.

## Parallel scripts and native work

Use one command with several actors only when one source operation genuinely
owns them together. Use `CutsceneCommandLaneScheduler` when original object
slots own independent scripts. Each lane retains its own instruction pointer,
stack, counters, and registers, and lanes update in original object order.

Keep source-native work in named native handlers: palette progression, room
loading, actor/part creation, portals, fixed-point physics, follower buffers,
treasure presentation, or other object-code state. The script owns the native
handler's command boundary and parameters. Avoid untyped generic callbacks.

## Adding a command or stream

1. Trace the macro, byte representation, handler, operands, counter behavior,
   and carry/yield result.
2. Extend the shared assembly model or fail with source context; never skip the
   opcode.
3. Add or reuse one typed command record and vocabulary entry.
4. Implement strict import and catalog parsing for every operand.
5. Add the exact runner behavior and only the host capability it requires.
6. Preserve actor bindings, native handoffs, and independent lane order.
7. Regenerate assets and prove deterministic import output.
8. Validate command trace order, first/final update boundaries, branches,
   cancellation, actor effects, and resulting room/save/audio state.

Command traces are attached and stored by the validation assembly. Production
events do not retain audit-only counters or history.
