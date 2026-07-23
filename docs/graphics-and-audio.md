# Graphics and audio

## Graphics resource ownership

Imported PNGs are normally Godot resources. Runtime code loads them through
`ResourceLoader` with cache reuse, via `OracleGraphicsCache`. Immediately after
the stable importer adds a new generated PNG, a clean checkout may not yet have
its editor-generated `.import` sidecar; the same cache then decodes that source
file once and retains the immutable result. Callers never decode a new image
for every NPC or animation change.

`OracleGraphicsCache` owns three immutable layers:

1. Source images keyed by resource path.
2. Composite images keyed by source images and append offset.
3. OAM frame textures keyed by source identity/pixels, encoded OAM, tile base,
   palette and overrides, grayscale interpretation, and composition mode.

Callers treat returned `Image` and OAM frames as read-only. A palette or graphics
override that changes pixels must be represented in the cache key; mutating a
shared image corrupts every user and invalidates cache identity. Clear the cache
during root shutdown so native Godot resources do not remain retained.

`LoadRawPngForValidation` is for independent validation decoding only. It must
not become a production loading path.

`OracleGraphicsData` owns length-checked binary reads, GBC palette conversion,
tilemap overlays, and common two-bit shade decoding. `OracleTileRenderer` owns
shared linear/interleaved 8x8 background addressing and 8x16 OAM cell drawing.
Menus supply their source-specific VRAM maps and palette attributes to these
helpers instead of carrying private copies of the pixel loops.

## OAM and animation composition

A generated sprite sheet may represent interleaved 8x16 OBJ cells, not a simple
row of 8x8 tiles. To reproduce a frame, trace all of:

- source graphics byte offset and any appended graphics block;
- object tile base plus each OAM tile offset;
- 8x16 tile pairing/interleaving;
- X/Y flips, signed OAM positions, and frame origin;
- OBJ palette, per-object palette override, and grayscale interpretation;
- OAM priority and the original actor draw order;
- animation duration, parameter byte, and loop start.

Changing a scripted animation selects cached immutable definitions/frames; it
does not rebuild every texture for that entity. Validate assembled frame pixels
and offsets, not just parsed record counts.

An OAM composition may select several effective OBJ palettes in one frame.
Palette overrides are therefore keyed by the effective `base XOR flags`
palette, included in the immutable OAM cache key, and applied per OAM cell.
Spirit's Grave's colored cube is the canonical case: its imported PALH `$89`
installs `paletteData5908` and `paletteData5910` in OBJ slots 6 and 7 while its
rolling frames also retain ordinary palette 5. Its `spr_colored_cube` sheet is
also black-on-white, unlike the usual black-background `spr_*` sheets, so the
generated visual record explicitly selects the opposite grayscale
interpretation before OAM composition. Enemy records carry the same
source-polarity field: Giant Ghini `$70` and its children `$3f` share the
black-on-white `spr_giantghini_1/2` chain and must select white as transparent,
while the other currently imported Spirit's Grave enemies retain the ordinary
black-background interpretation.

Multipart enemies may override the base enemy-record palette per child object.
Pumpkin Head `$78` is the canonical boss case: its head retains the enemy
record's OBJ palette 3, body initialization writes OAM flags `$01`, and ghost
initialization writes `$05`. Import those explicit initialization writes and
compose three independent cached animation sets; applying the base palette to
the whole boss gives the rebuilt body and exposed ghost the wrong colors.

`INTERAC_ESSENCE $7f` similarly composes three interaction objects rather than
reusing one frame. Dungeon 1's essence overrides its base to tile `$00`, OBJ
palette 1, animation 1; the pedestal uses `$76/$00/$40` and animation 0; and
the glow uses `$76/$06/$43` and animation 3. The glow's four two-update,
eight-cell OAM frames carry parameters `0/1/0/1`; each nonzero frame toggles
only the glow's visibility. Substituting animation 0 draws a second pedestal
where the glow belongs.

Room backgrounds retain original GBC palette and attribute behavior. Dynamic
palette threads, waves, and fades advance on their original fixed updates. A
future cell renderer must preserve those effects and custom collision; the
tentative TileMapLayer migration is documented in [TODO.md](../TODO.md).

Normal-shop price digits are dynamically loaded background graphics, not HUD
tiles or object sprites. Room graphics change `$04` loads `TREE_GFXH_03`
(`gfx_inventory_hud_1`) at the `$9200` tree slot, so `$47`'s tile base `$30`
selects source tile `$10`; its tilemap writes use BG palette attribute `$06`.

Inventory treasures, the era symbol, and Heart Piece quarters pass their source
attribute bytes through `drawTreasureDisplayDataToBg`: its two increments shift
sprite palettes 0-5 into BG palette slots 2-7 while preserving flip bits. Do not
apply their table attributes directly to the inventory BG layer. The inventory
storage cells source their first item sheet from `spr_item_icons_1_spr`, while
the equipped A/B displays source `spr_item_icons_1`; these similarly named
sheets are distinct and must not share an atlas. Both retain the `spr_*`
converter's direct black/gray/white color-index mapping. The storage sheet has
black color-0 transparency; the uncompressed equipped sheet deliberately uses
opaque white color 3 to fill the icon cell with the HUD tan. In addition,
`loadEquippedItemSpriteData` changes the left palette for sprite indices below
`$86` and `$8a` (the Satchel, shooters, and slingshots); storage cells continue
to use the raw display-table palette.

Multi-level inventory items must select their dedicated display table before
either renderer sees them; their rows in `treasureDisplayData_standard` are
unused placeholders. In particular, `TREASURE_SHIELD $01` selects
`treasureDisplayData_shield[wShieldLevel-1]`: Wooden Shield uses sprite `$93`
and palette `$00`, Iron Shield `$94/$05`, and Mirror Shield `$95/$04`. The
selected row is shared by inventory storage and equipped A/B rendering; only
the destination-specific BG/OAM palette conversion differs. Its display mode
`$00` reads `wShieldLevel` for the shared `L-` plus level overlay in both
destinations.

The Shield changes Link's body graphics rather than drawing a separate child
sprite. `func_4553` adds variants `$05/$06` for an equipped-but-lowered level-1
or level-2/3 shield and `$07/$08` for the raised equivalents to ordinary walk
frames `$54/$80`. Runtime composition therefore uses the exact `spr_link`
entries `$68-$77` and `$94-$a3`, including their direction-specific source
offsets. Parent initialization requests `SND_SHIELD $76`; a supported
projectile collision requests `SND_CLINK2 $58` once and hands the projectile to
its native bounce animation.

`drawTreasureExtraTiles` mode `$01` draws a packed-BCD quantity as two HUD
digit tiles. The selected Satchel display resolves treasure `$20-$24`, so both
the gameplay status bar and inventory A/B/storage rendering read the selected
seed counter and retain its leading zero. Equipped extras use attribute `$80`:
their nonzero BG pixels have priority over item OAM, including the tens digit
which overlaps the icon's lower-right cell. Godot composition must therefore
draw equipped OAM before these digit tiles. This is separate from mode `$00`'s
`L-` plus level overlay.

The thrown Ember Seed uses object GFX header `$78`
(`spr_common_items`), tile base `$12`, palette 2, and the imported five-frame
item animation. Ignition writes OAM flags `$0a`, whose bank bit switches the
same animation clock to fixed bank-1 `spr_common_sprites`, tile base `$06`, and
palette 2. Each animation parameter selects a full OAM record; the later flame
frames are two-cell compositions with their source palette and flip overrides,
rather than a single tile advanced by that parameter. Satchel landing and
ignition request
`SND_BOMB_LAND $52` and `SND_LIGHTTORCH $72` at their native state boundaries.

Dark dungeon rooms use the Ages room-darkening palette thread, not a
screen-space translucent overlay. The `$fc` dirty/source masks apply the signed
five-bit RGB component offset only to BG palettes 2-7; BG palettes 0-1, sprite
palettes, dialogue, and the HUD are unchanged. Full darkness targets `$f0`.
With one of two torches lit, `brightenRoomLightly` advances from `$f0` through
rendered offsets `$f1-$f6` and stops before drawing target `$f7`; the final
brighten similarly stops before drawing `$00`, retaining rendered `$ff` until
a later ordinary palette refresh. Room `5:ed` couples that visual path to
`SND_LIGHTTORCH`, then its falling key requests `SND_SOLVEPUZZLE` once and
`SND_DROPESSENCE` on both landings.

The five transformation rings render Link through imported special objects
`$03-$07`, not recolored Link frames. Each disguise keeps its own sprite sheet,
tile base, OAM composition, and 2-update initial/6-update walking cadence; room
tileset flags `$40/$20`, menu-disabled events, and shop restrictions restore
ordinary Link. `ITEM_SWORD_BEAM $27` similarly uses its four imported
`spr_common_items` OAM records and alternates standard OBJ palettes 4 and 5 on
global four-update boundaries. Creation requests `SND_SWORDBEAM $5d`; its
flickering collision clink does not issue an additional sound.

Chest rewards and held treasure interactions do not use that inventory table.
Their treasure-object `graphic` byte becomes the subid of `INTERAC_TREASURE
$60`; render the first frame from its imported sprite header, tile base,
palette, animation, and OAM. This distinction is visible for items absent from
the standard inventory button table, including room `4:08`'s small-key graphic
`$42` (`spr_map_compass_keys_bookofseals`, tile base `$0c`, palette 5).
The small-key door pickup sprite reuses that graphic, first at Z `$fc` for
eight updates and then Z `$f8` for 20 updates.

In a real dungeon, unless tileset flag `$10` marks a large-indoor room, the
status bar dynamically replaces HUD tile `$04` with the dedicated `gfx_key`
tile and writes `$1b` (the X symbol) plus `$10 + current key count` beside it.
The independent rupee update still writes the ordinary bottom-row digits at
`$2a-$2c`, so keys supplement rather than replace the wallet display. Dungeon
identity and key count remain runtime state; only the selected HUD composition
changes.

An original interaction can write selected `wRoomLayout` cells without issuing
a BG redraw. Represent that split with position-specific visual overrides: the
logical metatile changes for collision and terrain queries while the composited
room texture retains the already-drawn metatile, including through later full
texture refreshes. Room initialization and explicit visual metatile replacement
remove stale visual overrides along with the other transient layout state.

`setInterleavedTile` is a different split. Shutter doors copy selected 8x8 tile
IDs and attributes from a second metatile into a position-specific eight-byte
mapping, install the destination tile in `wRoomLayout`, and retain the old
collision byte. The mapping override must survive full-room texture refreshes
for the exact six-update half-door frame. The final ordinary tile write removes
both mapping and collision overrides, renders the complete destination
metatile, and only then makes the doorway passable.

Gasha trees use a third dynamic-background path. Room-load growth installs the
first 16 tiles from `gfx_gasha_tree` at the `$a0-$af` BG destinations before
writing the solid 2-by-2 tree layout. Harvest reloads the source 4-by-4 tile
map with BG palette 4, then advances nine imported maps every eight original
updates. Each phase replaces only the active tail of the tree graphics and
fills the exposed leading tiles from `spr_grass_tuft`, `gfx_dirt`, or
`gfx_sand`. Dynamic BG tiles take precedence over ordinary tileset-animation
overrides and are cleared with the completed room replacement; they are not
OAM sprites and must remain visible while the reward object itself is hidden.

## Audio determinism and lifecycle

`OracleSoundData` imports all 223 sound/music IDs and their banked channel
programs. The persistent `OracleSoundEngine` in `main.tscn` advances the
sequencer at 60 original updates per second and owns square, wave, noise, channel
priority, music/SFX replacement, fades, envelopes, vibrato, and pitch behavior.
Like the original `playSound`, an ordinary music or SFX request cancels an
active NR50 fade and restores full master volume. This matters when a cutscene
invalidates `wActiveMusic`, issues a sound-control fade, and arms a room warp in
the same update: the destination room's music request replaces the cutscene
track during the room load instead of waiting for the fade to run to silence.

Gameplay code requests original sound IDs at the original update. If the source
chooses a variation with the global game RNG (for example sword sounds), consume
`OracleRandom`; never use a separate nondeterministic RNG because it changes
later enemies and drops.

Collecting an Essence applies `TREASURE_ESSENCE`'s collection behavior in the
same update that its get text opens, so `MUS_GET_ESSENCE $10` plays throughout
that text. Only after the text closes does
`essenceScript_essenceGetCutscene` replace it with `MUS_ESSENCE $06` and create
the inward energy swirl with `SND_ENERGYTHING`.

Breaking a metatile whose stored effect is `INTERAC_ROCKDEBRIS` `$06` or
`INTERAC_ROCKDEBRIS2` `$0c` creates the common-sprite interaction at the
metatile center. Both use four imported four-update OAM compositions that
spread four chips outward and a terminal `$ff` frame that deletes on the
following update. `$06` uses tile base `$02` and OBJ palette 3; `$0c` uses tile
base `$40` and OBJ palette 5. Their state-0 update requests `SND_BREAK_ROCK`
`$a5`; the tile-breaking or Bracelet caller does not substitute a generic puff
or sound.

`itemMimicBgTile` is not an inventory icon lookup. A Bracelet-lifted metatile
copies the four currently rendered BG quarters into an object texture before
the room layout is replaced, preserving position overrides, animation
replacement, flips, and temporary palette state. Source color 0 becomes
transparent, matching the BG-to-OBJ palette-7 copy. Link's pull, strain, held,
and throw bodies use the exact `spr_link` entries `$dc-$e3`, `$5c-$5f`,
`$88-$8b`, and `$b0-$b3`; `SND_PICKUP` `$9c` begins the lift and `SND_THROW`
`$51` begins the release.

The live rupee wallet and `wDisplayedRupees` are distinct. The wallet changes
immediately, while the status bar moves one rupee toward it on each original
update and requests `SND_RUPEE` (`$61`) for every step. A grant that exceeds the
`$0999` BCD cap also requests `SND_RUPEE`, even when the displayed count is
already full.

Health likewise remains distinct from `wDisplayedHearts`. Damage removes one
displayed quarter per update. Healing adds one quarter every four updates and
requests `SND_GAINHEART` (`$57`) whenever that fills a complete heart; attempting
to collect a heart at full health requests the sound immediately. Item drops
ignore hazards while airborne, then create `INTERAC_SPLASH`/`INTERAC_LAVASPLASH`
and request `SND_SPLASH` (`$87`) on their first ground-height update over water
or lava. Dungeon pressure buttons deliberately reuse `SND_SPLASH` for both the
pressed and released transitions; that sound is not evidence of a water effect
by itself.

Trigger-created dungeon chests request `SND_SOLVEPUZZLE` on appearance and
spawn `INTERAC_PUFF`, whose state-0 update requests `SND_POOF` (`$98`). The puff
uses the imported interaction graphics and its 6/8/4-update animation entries;
the terminal `$ff` animation parameter deletes it. A retractable chest creates
the same puff when restoring its original source tile, without replaying the
solve cue.

Opening a chest requests `SND_OPENCHEST` (`$6c`) when tile `$f1` is replaced
with `$f0`; the reward requests `SND_GETITEM` (`$4c`) when its 32-update rise
finishes and the treasure/text are handed to Link. The treasure collection
table's own nonzero sound is requested first; room `4:08` therefore requests
`SND_GETSEED` (`$5e`) for the small key immediately before `SND_GETITEM`.
Touched ground treasures use the same imported collection behavior on the
contact update, then request `SND_GETITEM` when `INTERAC_TREASURE` enters its
held pose on the following update. Room `4:1e`'s falling key consequently uses
the same `$5e`, then `$4c`, sequence rather than replacing the behavior sound
with an early generic get-item cue.
Accepted push-block movement
requests `SND_MOVEBLOCK` (`$71`) at movement start. A block or supported enemy
resolved over a hole requests `SND_FALLINHOLE` (`$59`); a block also renders
the imported falling-hole interaction, while water/lava keep their splash path.
Opening a small-key door requests `SND_GETSEED` (`$5e`) with its key sprite and
`SND_DOORCLOSE` (`$70`) at both the interleaved and final door frames. Creating
an ordinary enemy death puff requests
`SND_KILLENEMY` (`$73`); the red-Zol split puff requests the same cue without
creating an ordinary death/drop puff.

The room `4:1e` Ghini requests `SND_DAMAGE_ENEMY` (`$4e`) through the shared
combat callback at its ordinary sword collision-effect boundary, including a
lethal accepted hit. Enemy death and its later puff sound remain separate;
rejected invincibility contacts do not replay the hit cue.

Mini-boss and boss teardown render the shared imported
`PART_BOSS_DEATH_EXPLOSION` OAM at its per-frame bounds. Its largest frames are
48 by 48 pixels around the part origin; they must not be clipped through the
ordinary fixed 32-by-32 sprite compositor.

Giant Ghini child appearance and Pumpkin Head body disappearance/regeneration
call `objectCreatePuff`, so they use `INTERAC_PUFF` with `SND_POOF`, not
`INTERAC_KILLENEMYPUFF`. Live Giant Ghini children take a different path when
their parent dies: `enemyDie` creates the ordinary `PART_ENEMY_DESTROYED` puff
and `SND_KILLENEMY`. A detached child that completes its fade deletes silently,
and Pumpkin Head's ghost becomes invisible without a puff when it rejoins the
head.

Accepted enemy contact requests `SND_DAMAGE_LINK` (`$5f`) once; invincibility
rejects both the damage and a repeated request. Drowning requests that same
sound when the drowning animation and splash begin, not when respawn damage is
applied. Hole pull-in stays silent until Link enters the fall animation, which
requests `SND_LINK_FALL` (`$65`) once; respawning does not replay it.

`DialogueBox` requests `SND_TEXT` for revealed non-space glyphs with the
original four-update cooldown, applies inline `\sfx()`/`\charsfx()` commands,
and requests `SND_TEXT_2`, `SND_MENU_MOVE`, and `SND_SELECTITEM` at continuation,
choice-movement, and choice-confirmation boundaries respectively. Gameplay and
the new-game intro both inject the persistent sound engine into their dialogue
instance.

The sound engine must stop generated playback, detach buffers/signals, and clear
references during shutdown. A headless run should finish without a retained
`AudioStreamGeneratorPlayback`.

Audio regressions should verify sequencer/channel state and RNG consumption in
addition to sound-call IDs. Host speaker coloration is not part of the engine,
but Game Boy register timing and arithmetic are.
