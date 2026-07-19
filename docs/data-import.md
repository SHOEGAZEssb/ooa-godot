# Data import

## Purpose and boundary

The importer converts the supported ROM and `oracles-disasm` sources into
address-independent runtime assets under `assets/oracle/`. Production runtime
code consumes those generated assets; it must not open or parse assembly files.

The entry point is `tools/import_oracles.ps1`. It validates the clean US ROM's
MD5 before producing output:

```powershell
& .\tools\import_oracles.ps1
& .\tools\import_oracles.ps1 -Rom 'D:\roms\ages.gbc' -Disassembly 'D:\src\oracles-disasm'
```

The expected MD5 is `C4639CC61C049E5A085526BB6CAC03BB`. A different ROM is
not a close-enough input: addresses, banks, and data may differ, so the import
must stop.

## Import stages

The entry script dot-sources these stages in dependency order:

| Stage | Responsibility |
| --- | --- |
| `Initialize-Import.ps1` | Paths, ROM validation, shared helpers, and output setup |
| `Import-WorldAssets.ps1` | Rooms, tilesets, metatiles, palettes, attributes, and collision data |
| `Import-MenuAssets.ps1` | Title, HUD, inventory, map, and menu graphics/tilemaps |
| `Import-DialogueAndIntro.ps1` | Fonts, text, and new-game introduction records |
| `Import-MapAndItemData.ps1` | Map metadata, treasure data, flags, and item tables |
| `Import-NpcData.ps1` | NPC definitions, visibility, dialogue, and animation inputs |
| `Import-CutsceneData.ps1` | Typed script commands and cutscene-specific records |
| `Import-EnemyData.ps1` | Ordered room objects, enemies, spawn restrictions, and drops |
| `Import-WorldNavigation.ps1` | Warps, dungeon layouts, neighbors, and room navigation |
| `Import-AudioData.ps1` | Sound IDs, descriptors, channel programs, and room music |

Stages share parsed state in one PowerShell process. Add a new stage only when
its ownership is genuinely distinct and place it after every stage that
provides its inputs.

`Import-DialogueAndIntro.ps1` resolves both numeric text names and the
`index: auto` `TX_09_*` CROSSITEMS rows. `Import-MapAndItemData.ps1` retains
those resolved low bytes in `treasure_display.tsv` and emits
`inventory_text.tsv`, including the 64 ring name/description pairs used by the
inventory marquee.

## Generated-data rules

- Never hand-edit `assets/oracle/`. Fix the parser or source mapping and rerun
  the importer.
- Preserve source order where the original consumer observes it. Sorting for
  cosmetic output is unsafe for object streams, scripts, and RNG-sensitive data.
- Emit stable hexadecimal identifiers and source labels. Import/startup errors
  should name the source path, line or label, field, and offending value.
- Reject duplicate keys unless the original format explicitly allows ordered
  duplicates. Do not let the last dictionary assignment silently win.
- Reject malformed rows and unsupported behavior. Do not skip a line because a
  parser does not recognize its opcode or variable.
- Use invariant numeric parsing and make hexadecimal versus decimal fields
  explicit in the schema.
- Keep output deterministic. Re-running the importer against unchanged inputs
  must produce byte-for-byte equivalent generated assets.
- If a binary format changes, update its runtime reader in the same change and
  validate its exact expected size/version.

TSV files are an intermediate runtime format, not permission for permissive
string dispatch. Readers should convert rows into typed records at startup and
fail with source-aware diagnostics. The planned catalog consolidation is tracked
in [TODO.md](../TODO.md); new formats should use a shared schema-aware reader or
a typed catalog instead of adding another ad hoc split/comment/hex parser.

## Adding imported behavior

1. Find the authoritative table and every macro that shapes it.
2. Determine bank/address interpretation, terminators, aliases, ordering, and
   state-dependent branches.
3. Add strict parsing and retain source metadata.
4. Generate the smallest typed representation that contains all runtime inputs.
5. Add or update the runtime reader in the same change.
6. Regenerate twice when practical and confirm there is no nondeterministic diff.
7. Validate representative rows plus malformed/duplicate input handling.

Graphics require tracing the complete source byte offset, tile base, OAM tile
offset, 8x16 interleaving, flips, palette flags, and priority. Parsed OAM text
alone does not prove an assembled sprite is correct. See
[Graphics and audio](graphics-and-audio.md).

NPC placement, state predicates, linked native interactions, and story event
records follow the ownership and validation workflow in
[NPCs and room events](npcs-and-events.md).
