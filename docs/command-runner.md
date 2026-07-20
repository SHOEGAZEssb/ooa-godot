# Cutscene command runner usage

This guide defines when and how to use the typed cutscene command runner in
the Oracle of Ages port. The primary goal is behavioral parity with the
original ROM, not making every sequence use one abstraction.

See [NPCs and room events](npcs-and-events.md) for the preceding decision about
whether the owning behavior is an ordinary room entity, a linked interaction,
or a `RoomEventController` event.

## Decision rule

Use `CutsceneCommandRunner` when the original behavior is an
`interactionRunScript` stream, or when an imported controller stream is a
faithful representation of several coordinated interaction scripts.

Keep a specialized native controller when the original behavior is driven by
a game-state cutscene, special-object state machine, palette thread, room
transition engine, or another native object-code handler.

Current examples:

| Cutscene | Correct architecture |
| --- | --- |
| Ralph's portal departure | Typed command runner |
| First-past arrival | Typed command runner |
| Maku Tree disappearance | Typed command runner |
| Adult Maku Tree post-rescue conversation | Typed command runner with native Seed Satchel create/respawn handlers |
| Impa encounter and stone scripts | Typed runner with native follower, rock, and fake-Octorok handlers |
| Nayru introduction and aftermath | Imported command stream with native object/effect handlers |
| New-game intro | Specialized pregame timeline and native sprite presentation |
| Time-warp transition | Specialized `CUTSCENE_TIMEWARP` transition state machine |
| Nayru singing screen | Specialized presentation controlled by the surrounding event |

Do not migrate a native cutscene merely because it contains waits, text, or
animation. Match the mechanism used by the disassembly.

## Relevant files

- `src/cutscenes/CutsceneCommand.cs`: typed records, source metadata, actor
  identifiers, host contract, and trace records.
- `src/cutscenes/CutsceneCommandCatalog.cs`: schema validation and conversion
  from generated rows to typed commands.
- `src/cutscenes/CutsceneCommandRunner.cs`: fixed-update command interpreter.
- `src/cutscenes/CutsceneCommandLaneScheduler.cs`: stable-order scheduler for
  independent actor scripts.
- `tools/import_oracles/Import-CutsceneData.ps1`: disassembly parsing and
  generated command streams.
- `validation/GameRoot.Validation.cs`: complete-path and branch validation.

Generated files under `assets/oracle/` must never be hand-edited. Change the
importer and regenerate them with:

```powershell
& .\tools\import_oracles.ps1
```

## Generated command format

Runtime command catalogs use tab-separated rows with these columns:

| Column | Meaning |
| --- | --- |
| `script` | Stable source script identifier |
| `label` | Active assembly label for the command |
| `index` | Zero-based command index in the runtime stream |
| `source-line` | Original disassembly line number |
| `opcode` | Typed runtime opcode |
| `actor` | Stable actor binding, when applicable |
| `arg0` / `arg1` | Schema-specific operands |
| `payload-base64` | Text, binding, native handler, or structured payload |

Every row must retain actionable source metadata. Import or startup errors
must identify the script, label, command index, source line, and invalid
operand or actor.

The generated `script_command_vocabulary.tsv` records byte lengths and
handler behavior for the implemented and near-term original opcodes.

## Fixed-update semantics

The runner advances at 60 original-engine updates per second. Each command
must explicitly preserve the original handler result:

| Result | Meaning |
| --- | --- |
| Continue | Dispatch the next command in the same update |
| Yield | Save the next command and stop until the next update |
| Block | Keep updating the current command |
| End | Finish and deactivate the stream |

For real interaction opcodes, determine this from the assembly handler and
`interactionRunScript`, not from intuition. In the original dispatcher, a set
carry flag continues dispatch; a clear carry flag yields after saving the
script address.

For example, `scriptCmd_orRoomFlags` returns with carry clear and therefore
yields. Room 0:7a's Impa-help flag write is native object code in a linear
same-update block, so it uses a distinct continue command. Similar-looking
mutations are not automatically timing-equivalent.

Preserve the separate counter behaviors:

- `counter1` can reach zero and dispatch the next command in that same update.
- `counter2` reaches zero on an update that still returns before the next
  command.
- A script `wait`, a controller `waitframes`, and a preloaded native counter
  do not necessarily share their first or final update boundaries.
- `callscript` and `retscript` retain their own yielded boundaries.
- Dialogue commands must reproduce both opening and post-close boundaries.
- `checkabutton` blocks on the actor binding and consumes that actor's pending
  talk press when satisfied. `jumpifroomflagset` and `jumpiftextoptioneq`
  branch and continue in the same update; the latter consumes the completed
  choice result from the host. `setmusic` yields after selecting the imported
  track. Keep these distinct from generic memory gates or sound effects.

Never use `Tween` or frame-time interpolation as the authoritative source for
ROM-timed movement.

## Actor bindings

Commands refer to `CutsceneActorId` values. A host must implement
`HasActorBinding` and reject any actor that is not available to that stream.
Bindings are validated before the first command executes.

Use stable semantic names such as `Impa`, `Ralph`, `Nayru`, or `Player`. Do not
put scene paths, transient node names, or room-specific lookup logic into the
generated command rows.

The host translates the stable identifier into the current runtime actor. It
also owns actor-specific operations such as animation selection, position,
visibility, Z, or deletion.

When translated movement finishes, the runner calls
`CompleteActorTranslation` on the same fixed update. Player hosts must use
this boundary to leave the walking body pose while retaining the final
position and facing. This prevents a completed movement from leaking a walking
sprite into later waits or dialogue.

## Parallel behavior

Use `paralleltranslate` when one imported controller operation deliberately
moves a small fixed set of actors together. Each actor keeps its own frame
count, and the command completes after the longest movement.

Use `CutsceneCommandLaneScheduler` when the original objects own independent
scripts. Each lane receives its own runner, counters, registers, instruction
pointer, and call stack. Add lanes in original object-update order; the
scheduler preserves that order and must not compact or reorder active lanes.

Do not merge independent actor scripts into one counter merely because their
visible actions overlap.

When one script creates another script owner in a later original object slot,
start the new runner from the creating native command and advance it later in
the same fixed-update pass. Room `1:38` is the reference: the placed sprout is
updated before its `$6b:$04` controller, which is updated before the two
`$96` Moblin lanes it creates. Each runner keeps its own command state while
the event host exposes only the shared `wTmpcfc0`/`wccd4` bindings. A scripted
actor that replaces itself with an enemy ends its lane after requesting the
ordinary combat spawn; subsequent `wNumEnemies` gates must query the entity
manager's live enemy counter, not a cutscene-local survivor count.

## Native handlers

Keep behavior in a native handler when it comes from object code rather than a
supported interaction command. Typical examples include:

- palette engines and palette-thread completion;
- room loading, room swaps, and camera updates;
- actor, part, and effect creation;
- portal and lightning effects;
- fixed-point Z physics owned by a special object;
- follower path buffers and collision handshakes;
- native flicker loops or global-frame effects;
- treasure presentation and other multi-system handoffs.

The imported stream should still own the handler's command boundary and its
parameters. Prefer a narrowly named native handler over a generic callback.
Blocking native handlers must expose completion through
`UpdateNativeHandler`; one-update mutations should use an explicit yielding or
continuing native command as required by the source.

## Host lifecycle

A typical event owns one runner and starts an importer-generated stream:

```csharp
private readonly CutsceneCommandRunner _runner;

public ExampleEvent(RoomEventContext context)
{
    _context = context;
    _runner = new CutsceneCommandRunner(this);
}

private void Begin() => _runner.Start(_database.Commands);
private void UpdateFrame() => _runner.AdvanceFrame();
private void Cancel() => _runner.Clear();
```

The event implements `ICutsceneCommandHost`. Host methods must validate that
the requested operation is legal for the active script instead of silently
ignoring unsupported behavior.

Always clear runners when an event is cancelled, a room unload invalidates its
actors, or native completion takes ownership. Do not accumulate ordinary event
updates during scrolling unless the original object is explicitly updated
during transitions.

Room events with ordinary room-match/start behavior implement
`IRoomEntryEvent` and are registered once in `RoomEventController`'s explicit
priority list. That list owns their entry selection, fixed updates, gameplay
blocking, and cancellation order. Events that coordinate state across rooms or
with another event, such as the Nayru and Impa sequence, keep their specialized
room-load handling in the controller.

## Tracing and validation

Command tracing belongs in the validation assembly. Production event classes
must not retain audit-only booleans, counters, masks, or compatibility
accessors.

Use command traces to verify:

- every started, updated, and completed command boundary;
- script, label, index, source line, and opcode order;
- branch and call/return targets;
- exact waits and final counter updates;
- dialogue, sounds, animations, flags, and WRAM signals;
- actor positions, facings, movement completion, and deletion;
- independent lane update order;
- native handler cadence and completion;
- final gameplay, room, inventory, audio, and persistence state.

Trace observations describe externally meaningful state. They must not change
runtime behavior.

Every regression or newly migrated cutscene requires a headless validation
covering its successful path and every supported branch.

## Adding or migrating a command

1. Locate the source macro and command handler in the disassembly.
2. Record its byte length, operands, counter use, and carry/yield behavior.
3. Extend the shared assembly parser or reject the opcode with source
   diagnostics. Do not silently skip it.
4. Add or reuse a typed command record.
5. Add strict catalog parsing for every operand.
6. Implement the exact fixed-update behavior in the runner.
7. Add only the host operation needed to connect it to runtime systems.
8. Emit source labels and line numbers from the importer.
9. Regenerate assets and confirm deterministic output.
10. Validate all command boundaries and visible/runtime effects against the
    disassembly.

If a command cannot yet be implemented faithfully, fail during import or
startup and include its source context. Do not replace it with an approximate
delay, callback, or no-op.

## Required verification

Before handing off a command-runner change, run:

```powershell
& .\tools\import_oracles.ps1
dotnet build
$godot = 'E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64_console.exe'
& $godot --headless --path . --quit-after 10 -- --validate
git diff --check
git status --short
```

The build must have zero warnings and errors, the complete validation suite
must pass, and unrelated worktree changes must remain untouched.
