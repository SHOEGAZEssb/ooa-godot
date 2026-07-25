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

## Import session and stages

The entry script creates one import session, starts the .NET source-model host,
and then runs these stages in dependency order:

| Stage | Responsibility |
| --- | --- |
| `Initialize-Import.ps1` | Paths, ROM validation, shared helpers, and output setup |
| `Import-WorldAssets.ps1` | Rooms, tilesets, metatiles, palettes, attributes, and collision data |
| `Import-MenuAssets.ps1` | Title, HUD, inventory, map, and menu graphics/tilemaps |
| `Import-DialogueAndIntro.ps1` | Fonts, text, and new-game introduction records |
| `Import-MapAndItemData.ps1` | Map metadata, treasure data, flags, and item tables |
| `Import-NpcData.ps1` | NPC definitions, visibility, dialogue, and animation inputs |
| `Import-GashaData.ps1` | Gasha spots, growth/reward tables, native timing, text, OAM, and disappearance graphics |
| `Import-CutsceneData.ps1` | Typed script commands and cutscene-specific records |
| `Import-EnemyData.ps1` | Ordered room objects, enemies, spawn restrictions, and drops |
| `Import-SeedTreeData.ps1` | Seed-tree controllers/parts, seed-type visuals/text, and the sixteen refill histories |
| `Import-MapleData.ps1` | Maple locations, paths, item distributions, dialogue, OAM, and Touching Book assets |
| `Import-WorldNavigation.ps1` | Warps, dungeon layouts, neighbors, and room navigation |
| `Import-AudioData.ps1` | Sound IDs, descriptors, channel programs, and room music |
| `Write-GeneratedTableManifest.ps1` | Deterministic TSV schema-version, record-count, and SHA-256 manifest |

Every stage has an `ImportStageContract` in `tools/import_oracles.ps1`. The
contract names its variable inputs, variable outputs, helper-function inputs,
and helper-function outputs. Before a stage runs, the entry script parses its
PowerShell AST and rejects undeclared cross-stage variable or helper use. It
then verifies the declared inputs, runs the stage, verifies the outputs, and
stores them in a typed `ImportStageResult`. Add a stage only when its ownership
is genuinely distinct; declare every dependency and place the stage after each
producer.

The stage scripts still execute in one PowerShell process because several
domain resolvers intentionally share large typed tables. Their contracts make
that sharing explicit and testable instead of depending on a variable or
function that happens to have been created earlier.

## Assembly source model

`tools/OracleImporter/` is the only component allowed to open an assembly
source file. `AssemblySourceRepository` canonicalizes paths beneath the
configured disassembly root, reads each `.s` file once, and caches the parsed
`AssemblySourceFile` for the complete import session. At successful shutdown,
the importer asserts that every opened source has exactly one physical read.

The source model retains:

- the exact raw text, ordered lines, and line-start offsets;
- path/line/column `SourceSpan` values;
- ordered blank, comment, label, constant, directive, data, macro,
  instruction, and unrecognized nodes;
- label aliases and duplicates rather than dictionary last-write-wins;
- indexes for labels, constants, directives, `.db`/`.dw` data, macros, and
  instructions; and
- active-branch state for the supported clean-US configuration
  (`ROM_AGES`, `REGION_US`, `AGES_ENGINE`, and `BUILD_VANILLA`).

Unknown syntax remains an ordered `Unrecognized` node. It is not discarded;
when a resolver needs that syntax, it must either interpret it explicitly or
fail with the node's source span.

Windows PowerShell 5.1 cannot load the .NET 8 tool assembly directly, so the
entry script starts the built tool once as a private redirected process.
`Read-ImportText`, `Read-ImportLines`, and `Read-AssemblyLabelBlock` expose that
single session to PowerShell. The latter resolves label bodies from the label
index and is used by the shared OAM/animation resolvers. Direct `Get-Content`
or `File.ReadAllText`/`ReadAllLines` calls for `.s` files are forbidden in
stages and covered by the importer tests.

Domain interpretation remains with the owning stage: rooms, ordered objects,
scripts, animations, OAM, palettes, sounds, and other source formats retain
their specialized checks and generated record types. The lexical model is not
an assembler and does not flatten these formats into one universal AST.

Run the focused source-model and boundary tests with:

```powershell
dotnet run --project .\tools\OracleImporter.Tests\OracleImporter.Tests.csproj
```

Run the complete deterministic-import check with:

```powershell
& .\tools\verify_oracle_import.ps1
```

The verifier runs the focused tests, imports twice, and captures a complete
manifest after each pass. The manifest ordinal-sorts every importer-generated
path except Godot `.import` cache metadata and records byte count and SHA-256;
TSV rows also retain record count and a SHA-256 of the ordered first-field key
sequence. Any added, missing, reordered, or changed output fails the check.

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

`Import-MapAndItemData.ps1` emits `metadata/bracelet.tsv` for
`ITEM_BRACELET $16`. It joins the item attributes, held-button usage table,
`bombsBraceletParent.s`, `bracelet.s`, the common throw-weight table, Link's
special-object graphics/animation entries, lifted-object offsets, object
collision table, and pickup/throw sounds. The importer asserts the paired
directional wall masks, 11-update pull boundary, 7/4/2 lift phases,
eight-update throw pose, weight-0 gravity/Z and normal/Toss Ring speeds, rather
than deriving those values from a convenient room. Bracelet break visuals
reuse `effects/rock_debris.tsv`; that table contains both source interactions
`$06` and `$0c` because liftable metatiles can retain either tile
base/palette combination for their eventual impact.

`Import-NpcData.ps1` similarly emits `effects/grass_debris.tsv` by resolving
interaction IDs `$00` and `$01` through their graphics, animation, OAM,
normal/underwater palette, and sound records. The runtime decodes a
breakable-tile effect byte's low nibble as the interaction ID and bit 4 as the
spawned interaction's flicker subid; it does not treat effect `$10` as another
interaction.

The same stage emits `effects/era_info.tsv` for
`INTERAC_ERA_OR_SEASON_INFO $e0`. It joins both present/past graphics records
and their shared positioned OAM with the native fly-in state machine, outdoor
and large-indoor tileset masks, one-shot global suppression flag, and
`wSentBackByStrangeForce` predicate. Runtime uses this record only after a full
room load; scrolling room changes do not create the display.

`Import-EnemyData.ps1` emits `effects/link_terrain_effects.tsv` beside the
universal terrain shadow from their shared `terrainEffects.s` source. The Link
record retains Ages' exact grass `$f8` and puddle `$f9` metatiles, two green
grass frames selected by `(xh XOR yh)` bit 2, four positioned puddle OAM
frames, the handler-derived eight-update visual cadence, and the
`SND_SPLASH $87` walking trigger's first-update/18-update period from Link's
animation parameters, including its six-update consumption window. The
importer also verifies terrain OAM's foreground priority plus the side-view and
`wScrollMode=$08` suppression branches; runtime must not replace these raw
compositions with generic particles.

`Import-GashaData.ps1` owns the complete Ages `INTERAC_GASHA_SPOT $b6`
closure. It emits all 16 group/room/subid placements and their source ranks,
the 25 rank/maturity probability rows, five random-ring tiers, all ten reward
treasure/text/OAM records, the nut visual, planting/growth/motion/timing
constants, and the nine 4-by-4 disappearance maps. It also copies the original
tree plus grass/dirt/sand replacement graphics and emits the four
`giveTreasure` maturity sources. These records preserve distribution and ring
table order because each random byte is consumed by subtracting weights in
source order; sorting either table changes the reward. The runtime must not
derive a rank from the room ID or substitute an inventory icon for a held
reward object.

`Import-MapleData.ps1` owns the recurring `SPECIALOBJECT_MAPLE $0e` closure.
It expands all 119 eligible present/past location bits, preserves the two
shadow and eight movement paths in source order, resolves all 32 Maple
animations and 14 `PART_ITEM_FROM_MAPLE` visuals/rewards, and emits TX
`$0700-$0713`. It also resolves Ages' `INTERAC_TOUCHING_BOOK $a5` visual and
the constants used by the kill threshold, entrance, race, departure, and
horizontal shake. Each `m_SpecialObjectGfxPointer` replaces only its declared
count of 8x8 OBJ tiles; higher slots retain the preceding frame's contents, and
a two-argument pointer replaces none. The importer must resolve that virtual
VRAM state per tile instead of applying one source offset to the whole OAM
frame. Runtime must consume these records directly: movement-path order and
each probability row affect shared RNG use, while the item table's
normal/boosted parameters retain Gold/Red/Blue Joy Ring behavior.

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

The map/item stage also emits the complete ledge-jump closure from
`checkLinkJumpingOffCliff`, `cliffTilesTable`,
`landableTileFromCliffExceptions`, and `LINK_STATE_JUMPING_DOWN_LEDGE`.
`ledge_cliff_tiles.tsv` and `ledge_landable_tiles.tsv` retain the active
collision set; `ledge_jump_directions.tsv` retains each wall mask, angle, and
two signed Link-relative probes; and `ledge_jump_speeds.tsv` plus
`ledge_jump_constants.tsv` retain the 11 length speeds, Z physics, sounds,
eight-pixel scan, and 9/9/6-update jump animation. The importer validates the
source handler branches and object-speed aliases instead of deriving those
values from the current room layout.

That stage also preserves the separate side-view Link contract.
`side_scroll_tiles.tsv` contains all 16 explicit
`tileTypesTable@sidescrolling` rows as bitwise flags, including combined
ladder-top and ladder-water values. `side_scroll_constants.tsv` retains
`sidescrollUpdateActiveTile`'s eight-pixel lower sample,
`linkUpdateInAir_sidescroll`'s `$24/$0e` gravity, `$0300` fall cap, wall masks,
high-byte landing snap, bottom boundary, sounds and 9/9/6-update animation, and
the side-view Feather launch speed from `parentItemCode_feather`. Runtime must
use those generated records; side-view tile flags are not ordinary top-down
terrain enum values and must not be reconstructed from graphics or collision.

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
The NPC stage also emits the filtered, source-ordered
`dark_room_interactions.tsv` closure for every direct
`PART_DARK_ROOM_HANDLER $08` and `$dc:$00` Graveyard Key consumer, plus
`dark_room_constants.tsv`. It verifies the handler/torch native branches,
large-room dungeon-property bits, torch tiles and collision data, treasure
object, falling motion, and sound IDs rather than deriving any of them from
room `5:ed` at runtime.

Present rooms `0:45` and `3:fb` add one native-dialogue closure to the same
stage. `Import-NpcData.ps1` emits `troy_house.tsv` only after verifying both
one-object room streams, boy `$3f:$01`'s exact game-progress gate, Troy
`$ca:$01`'s finished-game deletion branch, the complete pre-ending script,
and `troy_chooseRandomAnimalText`. Its 16 source-ordered rows preserve the
low-nibble RNG aliases, TX `$2c11/$2c12` first/repeat wrappers, substituted
TX `$2c13-$2c22` bodies, room flag `$40`, and their decoded text. Runtime
therefore consumes one shared RNG value per talk and performs the original
text substitution without parsing interaction source or collapsing duplicate
animal strings.

`Import-CutsceneData.ps1` also imports present room `0:56`'s
`comedianScript` into `comedian_commands.tsv` and its actor constants into
`comedian_event.tsv`. The stage verifies the room's two placed interactions,
the native two-run initialization and horizontal-facing wrapper, the
highest-Essence-bit helper, moustache animation bank, seven text records,
trade-item constants, and `TREASURE_OBJECT_TRADEITEM_07`. Runtime does not
parse `scriptHelper.s` or infer these operands from the visible NPC row.

Present room `2:e6`'s Mask Salesman uses the same typed cutscene stage.
`Import-CutsceneData.ps1` emits `mask_salesman_commands.tsv` and
`mask_salesman_event.tsv` only after verifying the single `$5c:$00` room
object, native always-update initialization, all 44 source commands, animation
IDs, interaction-data default animation `$00`, TX `$0b0d-$0b15/$0b45`, Tasty
Meat predicate, room-item bit `$20`, and `TREASURE_OBJECT_TRADEITEM_04`.
Runtime therefore executes the imported infinite dialogue loop and grants the
Doggie Mask without parsing interaction or script source.

Overworld named-key locks are also imported as a reusable source closure.
`Import-NpcData.ps1` emits all six keyhole locations, their treasure IDs and
per-key `$18` object visuals, the three collision-set/tile mappings, and the
shared push, flag, text, sound, Z-motion, gravity, and hold constants. This
metadata is broader than current gameplay coverage: only room `0:5c`'s
Graveyard Key consequence is active, while the other five records remain typed
inputs for their eventual room events. `Import-CutsceneData.ps1` separately
emits room `0:5c`'s `$dc:$01` placement and parsed script command stream, while
asserting the native two-phase gate helper. Keeping the reusable keyhole
predicate separate from the room-specific consequence avoids encoding a
one-room key test or pretending that the other five locks already work.

Enemy species records remain separate from the ordered room-object stream.
`Import-EnemyData.ps1` resolves ordinary `ENEMY_STALFOS $31:$00` subid data,
walk/jump animation pointers, aliased OAM pointers, graphics header `$9b`, and
all random/fixed placements into `stalfos.tsv`; the runtime joins those typed
definitions back to `enemy_object_stream.tsv` in source order. Subids whose
additional state machines are not implemented are intentionally absent from
the typed species table while their ordered source records remain available as
unsupported reservations/completion evidence.

The same shared stage emits `common_enemies.tsv` for the implemented
`ENEMY_BOOMERANG_MOBLIN $0a:$00`, `ENEMY_ARROW_MOBLIN $0c:$00`,
`ENEMY_ROPE $10:$00`, `ENEMY_GHINI $17:$00`, and
`ENEMY_WALLMASTER $28:$00` definitions, plus `moblin_boomerang.tsv` for
`PART_MOBLIN_BOOMERANG $21`. These records are not owned by Spirit's Grave:
the runtime joins them by enemy ID/subid anywhere in the ordered object stream.
Rope `$10:$01`, Arrow Moblin `$0c:$01/$02`, and Ghini `$17:$01/$02` remain
absent until their distinct attributes, native state machines, or golden-enemy
persistence behavior are implemented; they must not be routed through the
subid-0 definition.

`Import-SeedTreeData.ps1` owns the common
`ENEMY_SEEDS_ON_TREE $5a` / `PART_SEED_ON_TREE $10` closure. It retains all
ten Ages main-object placements, decodes each subid's high nibble as seed type
and low nibble as refill index, and resolves the `$78` part graphics,
type-specific tile bases/palettes, animation/OAM, TX `$0029/$002a-$002c/$0035`,
six-seed treasure parameter, motion, collision, and sound. The separate
`seed_tree_refills.tsv` preserves all sixteen group/room rows from
`seedTreeRefillData.s`, including rows shared with non-tree events and the
dummy `$000` entries. Runtime refill state must keep each eight-room list in
source order and store only room bytes, as the original banked WRAM does.

`Import-WorldNavigation.ps1` retains byte 1 of every `m_DungeonData` row as the
Wallmaster destination in `dungeon_maps.tsv`. `DungeonMapDatabase` exposes that
per-dungeon value, so a common Wallmaster capture returns Link to `$24` in
dungeon `$01`, `$ce` in dungeon `$0b`, and the corresponding imported room for
every other dungeon instead of using a first-dungeon constant.

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
string dispatch. Every production TSV consumer loads through
`GeneratedTableReader`: its schema declares the exact header and column count,
schema version, key columns, and whether keys are unique, grouped, ordered,
aliased, or intentionally repeated. Rows stay in generated source order and
typed databases retain ownership of record construction and original-engine
semantic checks. Unique schemas reject duplicate raw keys; grouped, ordered,
aliased, and repeated schemas preserve their declared multiplicity for the
owning database to interpret.

`Write-GeneratedTableManifest.ps1` is the final importer stage. It ordinal-sorts
the generated TSV paths and records manifest format version, per-table schema
version, data-row count, and SHA-256. Before the first production table is
accepted, runtime verifies the manifest itself, the exact generated TSV set,
and every declared version/count/checksum. A stale, incomplete, unexpected, or
modified generated table therefore fails startup with its asset path and
expected/actual metadata. Update the importer, runtime schema, and manifest
version together when a generated table contract changes; never edit the
manifest or its tables by hand.

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

The same stages emit two narrow records for Graveyard rooms `0:5d`-`0:7d`.
`objects/crows.tsv` resolves the fixed `$41:$00` placements together with their
source enemy attributes and four parameterized animation/OAM streams.
`objects/linked_game_ghini.tsv` retains the Ghini's five text IDs and decoded
messages plus the secret index, short-secret index, began flag, and traced
source graph. The ordinary `npc_visibility.tsv` still owns its separate linked
and D1 predicates; do not hide those conditions inside the dialogue record.

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
