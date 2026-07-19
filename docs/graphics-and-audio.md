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
apply their table attributes directly to the inventory BG layer.

An original interaction can write selected `wRoomLayout` cells without issuing
a BG redraw. Represent that split with position-specific visual overrides: the
logical metatile changes for collision and terrain queries while the composited
room texture retains the already-drawn metatile, including through later full
texture refreshes. Room initialization and explicit visual metatile replacement
remove stale visual overrides along with the other transient layout state.

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
or lava.

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
