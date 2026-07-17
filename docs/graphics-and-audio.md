# Graphics and audio

## Graphics resource ownership

Imported PNGs are Godot resources. Runtime code loads them through
`ResourceLoader` with cache reuse, via `OracleGraphicsCache`, instead of reading
PNG bytes and decoding a new image for every NPC or animation change.

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

## Audio determinism and lifecycle

`OracleSoundData` imports all 223 sound/music IDs and their banked channel
programs. The persistent `OracleSoundEngine` in `main.tscn` advances the
sequencer at 60 original updates per second and owns square, wave, noise, channel
priority, music/SFX replacement, fades, envelopes, vibrato, and pitch behavior.

Gameplay code requests original sound IDs at the original update. If the source
chooses a variation with the global game RNG (for example sword sounds), consume
`OracleRandom`; never use a separate nondeterministic RNG because it changes
later enemies and drops.

The sound engine must stop generated playback, detach buffers/signals, and clear
references during shutdown. A headless run should finish without a retained
`AudioStreamGeneratorPlayback`.

Audio regressions should verify sequencer/channel state and RNG consumption in
addition to sound-call IDs. Host speaker coloration is not part of the engine,
but Game Boy register timing and arithmetic are.
