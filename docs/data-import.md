# Data import

## Boundary

The importer converts the supported ROM and `oracles-disasm` sources into
address-independent runtime assets under `assets/oracle/`. Production runtime
code consumes those assets and never opens assembly files.

The stable entry point validates the clean US ROM before doing any work:

```powershell
& .\tools\import_oracles.ps1
& .\tools\import_oracles.ps1 -Rom 'D:\roms\ages.gbc' -Disassembly 'D:\src\oracles-disasm'
```

Expected MD5: `C4639CC61C049E5A085526BB6CAC03BB`. A different ROM must fail;
bank addresses and source data are not interchangeable.

## Import organization

`tools/import_oracles.ps1` is the authoritative ordered list of import stages
and their `ImportStageContract` declarations. `tools/import_oracles/` contains
feature stages, while `tools/OracleImporter/` contains the typed assembly source
model and shared parsing infrastructure.

Each stage declares its variable and helper inputs and outputs. The entry point
checks those declarations around execution, so a stage may not depend on a
PowerShell variable or helper merely because an earlier script happened to
create it. Put new data in the stage that owns the source domain; add a stage
only when it has a distinct ownership boundary.

The assembly source repository is the only component allowed to read `.s`
files. It retains ordered nodes, aliases, source spans, and actionable parse
errors. Feature importers interpret those nodes; they should not rebuild ad hoc
regex parsers over raw lines when the shared model can represent the syntax.

## Generated-data contract

Generated outputs are deterministic build artifacts, not hand-maintained
content. Every table or binary format must have:

- one owning importer stage and one runtime reader;
- stable source order unless the original format defines another order;
- a schema version or exact length contract;
- strict parsing of IDs, widths, sentinels, aliases, and duplicate keys;
- source-aware diagnostics that identify the original path, label, row, or
  address;
- an entry in the generated-table manifest when applicable.

Use the shared generated-table writer and reader for TSV data. Preserve empty
cells and escaping; do not split rows independently in feature databases.
Binary readers validate exact expected byte counts or explicit format versions.
When a format changes, update its importer, runtime reader, manifest expectation,
and regression in the same change.

Generated paths are runtime APIs. Rename or remove one only after updating all
readers and validations. Never repair an output in `assets/oracle/` directly.

## What to import

Import source facts that the runtime needs to reproduce behavior: ordered
placements, tables, constants, animation/OAM data, state predicates, script
commands, and source identity. Keep procedural behavior in runtime code when
the original behavior is a handler or state machine rather than data.

Prefer typed records over clone-side inference. For example, import the exact
predicate operands that control an interaction instead of importing a vague
`visible` boolean derived for one save state. Conversely, do not encode a C#
state machine as a huge generated instruction table unless the original is
itself a script stream.

Unsupported source input must stop import or startup with actionable context.
Do not silently drop an unknown directive, command, object, treasure mode, or
table branch.

## Adding or changing imported behavior

1. Trace the source table, pointer aliases, consumers, and original ordering.
2. Decide which stage owns the records and whether the shared source model
   needs a new syntax capability.
3. Define the smallest typed output schema that preserves required source
   identity and semantics.
4. Add strict importer checks, including malformed and unsupported cases.
5. Update the runtime reader and reject stale versions, counts, or operands.
6. Add importer/unit coverage and a gameplay validation that consumes the real
   generated data.
7. Regenerate assets and review every unexpected output change.
8. Prove deterministic output:

```powershell
& .\tools\verify_oracle_import.ps1
```

The verification script runs ownership checks, importer tests, and two-import
byte parity. Finish with the normal build and full headless suite from
[Validation](validation.md).

## Disassembly starting points

These are useful entry points, not substitutes for following callers:

| Domain | Source |
| --- | --- |
| Warp sources and destinations | `data/ages/warpSources.s`, `data/ages/warpDestinations.s` |
| Dungeon layouts and metadata | `data/ages/dungeonLayouts.s`, `data/ages/dungeonData.s` |
| Tilesets | `data/ages/tilesets.s` |
| Room interactions | `data/ages/interactions.s` |
| Interaction behavior | `object_code/ages/interactions/` |
| Link transitions and state | `object_code/common/specialObjects/link.s` |
| Transition constants | `constants/common/transitions.s` |
