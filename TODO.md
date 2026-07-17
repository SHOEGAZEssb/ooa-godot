# Engineering TODO

The project's highest priority is a 1:1 reconstruction of *Oracle of Ages*.
Consolidation is valuable only when it makes imported original behavior easier to
validate without obscuring table order, aliases, identifiers, or game-specific
semantics.

## Consolidate generated-data parsing

Status: Planned  
Consolidation value: High  
Fidelity risk: Moderate if incremental; high as a single format migration

### Finding

Runtime code currently contains 40 `FileAccess.GetFileAsString` calls across 28
C# files. The readers repeatedly implement top-level line splitting, comment
filtering, tab-separated column indexing, hexadecimal and decimal conversion,
sentinel handling, and malformed-row checks. Representative readers include
`src/world/WarpDatabase.cs` and `src/inventory/TreasureDatabase.cs`.

The duplication produces inconsistent failure behavior. Many errors report the
row but not its asset path, line, column, or expected type. Duplicate handling is
also inconsistent: some dictionaries reject duplicates without source context,
some readers group repeated keys intentionally, and treasure-object names are
currently ignored when already present.

The importer already validates many source counts and original records. Runtime
databases also expose typed records and perform important subsystem-specific
checks. This work should consolidate only the mechanical parsing layer first,
not replace those table-specific types or semantic validations.

### Required design

- Introduce one schema-aware reader for generated tabular assets.
- Include asset path, physical line number, column name/index, offending value,
  and expected type or range in every parse error.
- Provide explicit parsers for hexadecimal bytes/words, decimal values, signed
  values, booleans, base64 text, and table-specific sentinel values.
- Require each schema to declare its exact column count and field names.
- Require each schema to declare key semantics: unique, grouped, ordered,
  aliased, or intentionally repeated.
- Preserve row order exactly. Never normalize, sort, or deduplicate data unless
  the corresponding original table and importer explicitly require it.
- Keep nested domain encodings, such as animation/OAM frame expressions, in
  their owning subsystem unless a genuinely shared original format is proven.
- Keep record construction and original-engine semantic validation in the
  existing typed databases.

### Migration plan

1. Inventory every generated text table, its importer source, runtime consumer,
   schema, key multiplicity, order requirements, expected count, and existing
   headless coverage.
2. Add the shared reader and tests for line endings, comments, empty fields,
   malformed numbers, sentinels, ranges, duplicate policies, and diagnostics.
3. Migrate one table family at a time. For each migration, compare record count,
   key sequence, field values, and ordering against the previous reader before
   removing it.
4. Start with small unique-key metadata tables, then grouped room tables, and
   migrate ordered/aliased object streams last.
5. Add an importer-generated manifest containing schema versions, record counts,
   and content checksums so incomplete or stale generated assets fail at startup.
6. Run the complete importer, build, and headless validation suite after every
   table family.

### Format decision

Do not begin with one monolithic Godot `Resource`. A single catalog would couple
unrelated original tables and create a large conversion surface where ordering
or alias semantics could be lost. Generated TSV files remain useful because they
are directly reviewable against the disassembly.

Typed binary assets may be introduced later for tables where a documented binary
layout provides a concrete benefit. Such formats must include a version, exact
byte-count validation, deterministic importer output, and a reader tested
byte-for-byte. Godot `Resource` assets should be adopted only if they provide a
specific benefit beyond replacing transparent generated data with engine
serialization.

### Acceptance criteria

- All top-level generated TSV parsing uses the shared reader; databases no
  longer hand-roll line/comment/column/primitive parsing.
- Every table has explicit schema, count, range, order, and key-multiplicity
  validation.
- Intentional repeated keys and aliases remain intact and in source order.
- Unknown columns, malformed values, duplicate unique keys, and stale schema
  versions fail with actionable path-and-line diagnostics.
- Imported record values and sequence are unchanged by the migration.
- `tools/import_oracles.ps1`, `dotnet build`, the complete headless `--validate`
  suite, and `git diff --check` all pass.

## Finish the typed cutscene command architecture

Status: Complete; shared runner and Nayru migration validated

Consolidation value: Very high

Fidelity risk: High; migrate incrementally

### Finding

`RoomEventTimeline` already preserves the original one-command-per-update
boundary and is used by several finite events. It currently expresses most
behavior through callbacks, however, rather than imported typed commands.
`NayruIntroEvent.cs` is approximately 2,700 lines and contains a local
12-command interpreter alongside extensive validation-only audit state.
`ImpaIntroEvent.cs` is approximately 1,350 lines and remains split across large
handwritten encounter, help, stone, and follower state machines.

The importer already parses the relevant interaction scripts, labels, waits,
movement constants, text IDs, signals, flags, and branches from the
disassembly. It currently flattens those values into event-specific TSV rows.
New cutscenes should not add more handwritten state machines where the original
behavior is an `interactionRunScript` command stream.

### Required design

- Define an importer-generated typed command stream with stable script and
  label identifiers from the disassembly.
- Preserve exact one-command-per-update cadence, including commands that only
  set state or intentionally yield for one update.
- Support waits, gates, text, sound, animation, movement, jumps, flags,
  signals, branches, calls/returns, actor deletion, and explicit completion.
- Support parallel actor lanes without merging their independent counters or
  changing object update order.
- Keep actor lookup typed and fail startup/import with the script label,
  command index, actor identifier, and source location when a binding is
  invalid.
- Keep bespoke native handlers for behavior that was not an interaction script,
  including palette engines, room swaps, portal effects, actor spawning, and
  other original object-code handlers.
- Add a validation trace sink for command boundaries, branch decisions, actor
  positions/facings, animations, dialogue, sounds, flags, and signals. Audit
  observations must not remain as production event booleans and bit masks.
- Do not make generic Tweens or frame-time interpolation authoritative for
  original fixed-update movement.

### Staged checklist

- [x] Preserve one-command-per-update sequencing in `RoomEventTimeline`.
- [x] Prove typed movement, parallel movement, animation, text, fade, jump, and
  effect commands in Nayru's event-local runner.
- [x] Inventory the original script command vocabulary used by implemented and
  near-term cutscenes, including command byte lengths and yield behavior.
- [x] Define shared typed command records, source script/label metadata, a
  schema-validating runtime catalog, and a fixed-update host/runner contract.
- [x] Add typed actor bindings, branch/call stacks, gates, and a parallel-lane
  scheduler to the shared runner.
- [x] Add deterministic shared-runner command tracing and migrate Ralph's
  validation assertions to trace entries.
- [x] Migrate remaining production audit fields and event-specific validation
  observations to shared trace assertions.
- [x] Extend `Import-CutsceneData.ps1` to emit Ralph's active-path command stream
  with assembly source labels/lines and startup diagnostics for malformed or
  unsupported runtime records.
- [x] Generalize assembly command parsing for subsequent scripts and reject
  unsupported assembly commands during import with source diagnostics.
- [x] Migrate Ralph's room 0:39 portal departure as the first simple event and
  compare every imported command boundary, mutation, source label, native
  flicker-loop cadence, and completion update against the previous
  implementation.
- [x] Migrate `villagerSubid0dScript`, the room 1:39 first-past arrival, including
  transition-overlapped waits, the shared jump subscript, cardinal movement,
  animation cadence, and the original counter2 zero-update command boundaries.
- [x] Migrate the Maku Tree disappearance and any other remaining simple
  `RoomEventTimeline` events; require newly added script-driven cutscenes to use
  the shared runner.
- [x] Migrate Impa's encounter script while retaining native follower-path,
  collision, room-transfer, and object-handshake handlers.
- [x] Finish migrating Impa's stone/help sequence.
  - [x] Migrate room 0:7a's native help interaction through the typed runner,
    preserving its edge gate, preloaded 30-update counter, TX_0100, room flag,
    and eight-update simulated-Link-input handoff.
  - [x] Migrate `impaScript_moveAwayFromRock` and validate its `$02/$03/$04`
    signal handshake with the native Impa approach/jumps and `linkCutscene2`
    positioning handlers.
  - [x] Migrate direction-dependent `impaScript_rockJustMoved`, including both
    branch targets, `$07`, TX_0109, movement facings, and follower restoration.
  - [x] Retain fake Octorok behavior as parallel native object handlers, with
    their staggered counters, movement, sounds, and ordering validated beside
    the imported encounter stream.
- [x] Migrate Nayru's script-driven actor lanes and remove its local command
  type/dispatcher where the shared runner has equivalent commands.
- [x] Move Nayru's validation-only counters, masks, and booleans into trace-based
  assertions in the validation assembly.
- [x] Reduce `NayruIntroEvent` to orchestration and native effect handlers, with
  imported records owning script order and command parameters.
- [x] Run importer determinism checks, `dotnet build`, the complete headless
  `--validate` suite, and `git diff --check` after each migration stage.

### Completed result

- The importer emits a 235-record Nayru command stream with original source
  scripts, labels, command indices, and line diagnostics, and rejects unknown
  assembly opcodes instead of silently flattening them.
- The shared fixed-update runner now owns typed actor validation, waits, gates,
  dialogue, sound, animation, flags/signals, branches, calls/returns, translated
  movement, deletion, native boundaries, and deterministic command traces.
- Independent lane runners retain their own counters, registers, and return
  stacks while the scheduler preserves insertion/object update order.
- Nayru's former local command type, handwritten command list, dispatcher, and
  validation-only audit masks/booleans are gone. Bespoke palette, room-swap,
  spawning, portal, possession, vignette, and aftermath object-code behavior
  remains in native handlers.

### Acceptance criteria

- Newly implemented interaction-script cutscenes are importer-generated command
  streams rather than event-specific state-machine switches.
- Imported command order, command boundaries, labels, branches, calls, actor
  update order, and fixed-update movement match the disassembly.
- Unsupported or malformed commands fail during import/startup with actionable
  script-label and source diagnostics.
- Impa and Nayru retain only native orchestration that cannot be represented by
  original script commands.
- Production cutscene classes contain no validation-only audit state.
- Trace comparisons cover complete successful paths and every supported branch.

## Consider a TileMapLayer room renderer

Status: Very hard maybe

Consolidation value: Medium if profiling proves the current renderer costly

Fidelity risk: Very high

### Finding

`OracleRoomData` currently recomposes and uploads the complete room texture
after a single metatile mutation. Animated VRAM substitutions and temporary
background-palette changes also rerender the complete texture. A
`TileMapLayer`-based renderer could update affected cells and use Godot's
rendering quadrants for batching.

This is not a routine optimization. The flattened room texture is also used for
exact room scrolling, scanline wave distortion, palette effects, validation,
and push-block image extraction. Rooms are only 160x128 or 240x176 pixels, so
the existing work may be cheaper and safer than a new rendering model. Do not
prioritize this migration without profiling evidence of a material bottleneck.

### Migration constraints

- Keep `OracleRoomData`'s custom collision and original-room-coordinate model.
- Model each metatile as four independently mapped 8x8 cells so original tile
  IDs, X/Y flips, palette attributes, and animated destination ranges remain
  exact.
- Implement animation as the original persistent, potentially overlapping VRAM
  writes. Do not replace it with generic independent tile animations.
- Keep palette source data indexed and nearest-filtered. Any shader-based
  palette lookup or blend must reproduce the current RGBA bytes exactly.
- Preserve the original animation freeze during scrolling and account for
  `TileMapLayer` deferring batched updates until the end of the frame.
- Support two independently positioned room renderers during screen scrolling.
- Preserve the 128-phase integer scanline-wave table and horizontal wrapping.
  Rendering through a room-sized intermediate texture is acceptable if needed
  for whole-room effects.
- Replace push-block sampling from the flattened room texture with an equivalent
  metatile-frame source before removing that texture.
- Disable `TileMapLayer` collision, navigation, and occlusion features unless an
  original behavior specifically requires them.

### Required validation

- Retain the current CPU renderer as the reference implementation throughout
  the staged migration.
- Compare old and new output pixel-for-pixel for every imported room, including
  large-room padding behavior, every relevant animation phase, all palette
  variants and blends, flips, and representative runtime metatile mutations.
- Validate exact update timing for tile changes, animation writes, transitions,
  palette effects, and wave phases—not just final screenshots.
- Profile the current and proposed renderers in representative animated rooms;
  adopt the new renderer only if it produces a meaningful measured benefit
  without weakening the 1:1 validation surface.
