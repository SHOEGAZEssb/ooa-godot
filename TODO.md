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
