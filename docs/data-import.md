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

`Import-MenuAssets.ps1` also copies the original appraised and unappraised ring
list maps/flags plus their ring, quest-item, inventory-HUD, and palette inputs.
`Import-NpcData.ps1` emits Vasu's complete TX `$3000-$30c1` text closure and
source constants for appraisal prices, duplicate refunds, fixed waits, ring
storage addresses, and completion flags. Runtime code must consume these
generated assets and typed values rather than parse bank 2 or text sources.
The same stage emits room `2:5e`'s reachable `$47` shop-item replacement graph,
product OAM, BG price destinations, prompts/item text, `$46` animations, WRAM
addresses/masks, and `$71:$0c` Dimitri entry constants. Shop text `\jump` and
unterminated fallthrough are flattened while `\stop`, `\col`, and `\opt`
remain runtime commands.

The same stage emits `metadata/seed_satchel.tsv` for the first Satchel's
`ITEM_EMBER_SEED $20` child. It joins `itemData.s`, `itemAttributes.s`,
`itemAnimations.s`, the item-usage/Link-animation tables, object GFX header
`$78`, `itemOamData.s`, the native Satchel/seed handlers, and the sound
constants. Animation parameter bytes are offsets into `item20OamDataPointers`,
not raw graphics-tile offsets; the importer resolves them to the complete OAM
composition for each frame. It also checks the parent allocation/decrement
order, signed directional offsets, 8.8 Z/gravity constants, flame data, loop
point, and Ember break source before writing the typed runtime record. The
ignition row retains its full OAM flags `$0a`: bit 3 selects fixed VRAM bank 1,
whose `GFXH_COMMON_SPRITES` header maps tile base `$06` to
`spr_common_sprites`, rather than back into the flying seed's
`spr_common_items` sheet. Extend this table from the corresponding native
handler when another seed effect becomes active; do not infer one seed's
behavior from the Ember row.

That item stage also emits `metadata/sword_beam.tsv` for `ITEM_SWORD_BEAM
$27`, retaining its four signed Link-relative offsets, collision/damage
attributes, `SPEED_300`, sound, tile base/palette, and directional OAM. The
world stage emits `metadata/transformed_link.tsv` from special objects
`$03-$07`, joining each transformation ring to its eight source GFX/OAM
records and the shared 2/6/6-update animation. Both importers assert their
native handler branches so runtime code does not infer disguise frames or
sword-beam constants.

`Import-MapAndItemData.ps1` joins every breakable-tile row with its room-flag
action and Gasha-maturity side effects. `Import-WorldAssets.ps1` emits all 56
rows from `singleTileChanges.s`, including the `$f0-$f2` linked/completion
predicates. `Import-NpcData.ps1` emits the complete 50-row
`standard_tile_substitutions.tsv` and all eight placed `$dc:$08` tile-change
watchers. Each watcher retains its source object order, packed layout position,
and room-flag mask, and the importer requires a matching single-tile change.
Room loading follows the original ordering: single-tile changes, standard
flag-driven substitutions, opened chest/key-door state, then room-specific
changes. A normal breakable row can persist directly through standard
substitution; room `0:48` instead uses its watcher at `$68` to set flag `$02`,
whose single-tile row restores `$3a` on later entries.

Treasure-object sprites are a different source path from those inventory BG
displays. `Import-NpcData.ps1` follows each treasure object's graphic byte into
the contiguous `INTERAC_TREASURE $60` subid, animation, and OAM pointer tables
and writes `treasure_object_visuals.tsv`. Alias labels inside those tables do
not end the ROM data: offsets may legally continue across the next label. The
imported record therefore retains the sprite sheet, tile base, palette,
default animation, and resolved OAM for every referenced treasure graphic.

Reusable dungeon mechanics are imported from their shared source tables rather
than inferred from whichever room first exposes them. `Import-NpcData.ps1`
combines `interactableTilesTable`, standard room-flag substitutions,
`_adjacentRoomsData`, and door-controller timing into
`dungeon_key_doors.tsv`. It also resolves `INTERAC_FALLDOWNHOLE $0f` to
`fall_down_hole.tsv`, including its common sprite header, `SPEED_60`, and
terminal animation. `Import-WorldAssets.ps1` copies the dedicated 8x8
`gfx_key.png` tile used when the dungeon HUD dynamically replaces tile `$04`.

Enemy species records remain separate from the ordered room-object stream.
`Import-EnemyData.ps1` resolves ordinary `ENEMY_STALFOS $31:$00` subid data,
walk/jump animation pointers, aliased OAM pointers, graphics header `$9b`, and
all random/fixed placements into `stalfos.tsv`; the runtime joins those typed
definitions back to `enemy_object_stream.tsv` in source order. Subids whose
additional state machines are not implemented are intentionally absent from
the typed species table while their ordered source records remain available as
unsupported reservations/completion evidence.

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

Within `Import-EnemyData.ps1`, all enemy, part, and interaction OAM labels pass
through one count-checked `Resolve-Oam` parser; callers still select their own
source file, pointer table, animation terminator, and parameter semantics.
`Import-NpcData.ps1` likewise resolves progress-indexed dialogue table bodies
through one label/routine/count check before its progress-1 and progress-2
exporters apply their distinct state and linked-game rules.

For concurrent native interaction scenes, `Import-CutsceneData.ps1` emits the
native parameters and dialogue rather than inventing a linear command stream.
Room `0:7b` uses `graveyard_ghost_kids_event.tsv` plus
`graveyard_ghost_kids_text.tsv`; the importer checks the complete room object
order, all three handler branches, their shared script tail, palette override,
RNG helper, jump/speed/sound constants, and automatic textbox positioning.
Runtime retains the original per-object update order when consuming that typed
record.

NPC placement, state predicates, linked native interactions, and story event
records follow the ownership and validation workflow in
[NPCs and room events](npcs-and-events.md).
