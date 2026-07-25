# Engineering TODO

The project's highest priority is a 1:1 reconstruction of *Oracle of Ages*.
Consolidation is valuable only when it makes imported original behavior easier to
validate without obscuring table order, aliases, identifiers, or game-specific
semantics.

## Parse the disassembly once

Status: Planned

Consolidation value: Very high

Fidelity risk: High; migrate incrementally with byte-for-byte output checks

### Finding

`tools/import_oracles.ps1` dot-sources every stage into one shared PowerShell
scope and relies on their execution order. The current stage scripts contain
132 `Get-Content` references, approximately 252 regex operations, and 140
literal disassembly-path references. Several large source files are loaded and
scanned repeatedly: `objects/ages/enemyData.s` is read four times within
`Import-EnemyData.ps1`, while `scripts/ages/scriptHelper.s` and
`scripts/ages/scripts.s` are referenced eight and seven times across the NPC
and cutscene stages.

The shared scope also creates undeclared stage dependencies. For example, the
menu stage consumes `$paletteDataSource` created by the world stage, the
dialogue/intro stage consumes `$textYaml` and `Normalize-DialogueText` from the
menu stage, and the cutscene stage consumes `$interactionGraphics`,
`$npcAnimationTables`, `$interactionAnimationSource`, and
`Resolve-NpcAnimation` from the NPC stage. Reordering or independently testing
a stage can therefore break it without an explicit missing dependency.

Repeated domain-specific regex scans lose structural information such as the
original sequence of labels, directives, macro invocations, instructions,
aliases, and duplicate records. The previously lost enemy object ordering is a
concrete example of the resulting fidelity risk. New cutscenes, interactions,
and enemy families will otherwise continue increasing this parsing debt.

This task concerns import-time parsing of the disassembly. It is separate from
the generated-data parsing task above, which concerns production C# readers of
the TSV assets after import.

### Required design

- Introduce one import-session source repository that opens each assembly file
  once and retains its path, raw text, ordered lines, and line-start offsets.
- Parse assembly sources into a small ordered lexical representation with
  source spans. Represent labels, directives, macro calls, instructions,
  operands, comments, and unrecognized syntax without discarding their order.
- Build reusable indexes over that ordered representation for labels,
  constants, `.db`/`.dw` data, macro invocations, and configured conditional
  branches. Indexes must preserve aliases and intentional duplicates.
- Keep domain interpretation in typed resolvers for rooms, objects, scripts,
  animations, OAM, palettes, sounds, and other original formats. Do not flatten
  all assembly into one universal semantic record.
- Give every stage explicit typed inputs and outputs. Eliminate dependencies on
  functions or variables that happen to exist because an earlier script was
  dot-sourced first.
- Include source path, line, column, label, and offending syntax in every parse
  or resolution error.
- Preserve PowerShell as the orchestration layer. Prefer a small C# importer
  library for the source model, indexes, typed records, and unit tests, loaded
  once for the complete import session.
- Keep copied PNG/binary resources and non-assembly formats outside this model
  unless they have a separate demonstrated parsing problem.

### Migration plan

1. Produce a baseline manifest of generated file paths, byte counts, hashes,
   record counts, key sequences, and existing importer/validation results.
2. Add the source repository, source-span type, ordered lexical nodes, label
   index, and tests for line endings, comments, local labels, directives,
   macros, duplicate labels where legal, and configured conditional branches.
3. Expose the parser context to PowerShell and require new importer work to use
   it instead of adding direct assembly `Get-Content` or whole-file regex scans.
4. Migrate shared constants, labels, byte/word tables, and source-line lookup
   helpers first, comparing generated output with the baseline after each family.
5. Migrate fidelity-sensitive ordered streams next: room objects, enemy
   placement, interaction scripts, and cutscene commands. Assert record sequence
   and source spans before deleting the old parsers.
6. Migrate animation, OAM, palette, interaction, navigation, and audio table
   resolvers one family at a time.
7. Replace shared-scope stage state with explicit result objects and remove the
   corresponding legacy scans only after parity is proven.
8. Finish by rejecting direct `.s` file reads outside the source repository and
   documenting the importer library API in `docs/data-import.md`.

### Scope decision

Do not begin by implementing a complete RGBDS assembler, preprocessor, or one
monolithic AST for every source construct. The first model needs only lossless
ordering, source identity, common lexical structure, configured-US conditional
selection, and the typed table/script resolvers required by current imports.
Unsupported syntax must remain visible and fail when a resolver attempts to
consume it; it must not be silently dropped.

Do not combine this migration with a wholesale generated-asset format change.
Keep current generated outputs stable while changing how their source data is
understood. The runtime TSV-reader consolidation can then proceed independently
against proven importer output.

### Acceptance criteria

- Every assembly source is read once per import session through the shared
  source repository; stages contain no direct `Get-Content` calls for `.s`
  files and no repeated whole-file regex scans.
- Every stage declares its inputs and outputs and can be tested without relying
  on undeclared variables or functions from a previous dot-sourced stage.
- Labels, aliases, directives, macro calls, instructions, duplicate records,
  and object/script row order remain traceable to exact source spans.
- Ordered room-object, enemy-placement, and cutscene-command outputs match their
  original source sequence and retain source-aware diagnostics.
- Generated assets are byte-for-byte identical to the baseline except for
  separately reviewed, intentional corrections backed by the disassembly.
- Unsupported or malformed consumed syntax fails with actionable diagnostics
  instead of disappearing from generated output.
- `tools/import_oracles.ps1`, `dotnet build`, the complete headless `--validate`
  suite, deterministic second-import comparison, and `git diff --check` pass.
