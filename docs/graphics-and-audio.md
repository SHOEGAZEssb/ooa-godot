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

Room backgrounds retain original GBC palette and attribute behavior. Dynamic
palette threads, waves, and fades advance on their original fixed updates. A
future cell renderer must preserve those effects and custom collision; the
tentative TileMapLayer migration is documented in [TODO.md](../TODO.md).

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
Accepted push-block movement
requests `SND_MOVEBLOCK` (`$71`) at movement start. A block or supported enemy
resolved over a hole requests `SND_FALLINHOLE` (`$59`); a block also renders
the imported falling-hole interaction, while water/lava keep their splash path.
Opening a small-key door requests `SND_GETSEED` (`$5e`) with its key sprite and
`SND_DOORCLOSE` (`$70`) at both the interleaved and final door frames. Creating
an ordinary enemy death puff requests
`SND_KILLENEMY` (`$73`); the red-Zol split puff requests the same cue without
creating an ordinary death/drop puff.

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
