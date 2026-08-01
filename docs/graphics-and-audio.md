# Graphics and audio

## Imported graphics boundary

Graphics facts come from the ROM/disassembly through generated assets. Runtime
code loads them through the shared graphics data and cache layers; it does not
decode a private copy for each actor or reconstruct source tables in C#.

`OracleGraphicsCache` owns immutable source images, composites, OAM frames, and
OAM cells. Every pixel-affecting input belongs in the cache key: source bytes,
tile base, OAM, palette, overrides, grayscale interpretation, and composition
mode. Treat cached images as read-only. Clear retained Godot resources during
root shutdown.

A newly generated PNG may not yet have a Godot `.import` sidecar in a clean
checkout. The shared loader may decode that source once and cache it; feature
code must not introduce another fallback path.

Validation observes cache operations through an attached observer. Production
cache classes retain current content, not audit history or hit counters.

## OAM and animation

An imported sprite sheet is source graphics, not necessarily a row of complete
frames. Reconstruct OAM using all original inputs:

- graphics bank and byte/tile offset, including appended blocks;
- 8-by-16 OBJ tile pairing and interleaving;
- signed cell coordinates, flips, frame origin, and hardware bias;
- OBJ palette, per-object overrides, transparency, and grayscale polarity;
- OAM priority and source object draw order;
- animation duration, parameter byte, terminal markers, and loop target.

Do not crop every actor to an assumed 16-by-16 or 32-by-32 box. Composite bounds
come from the OAM cells. Preserve byte-wrapped coordinates near screen edges.
Logical actor positions remain unchanged while camera and transition offsets
alter presentation.

Animation definitions and assembled frames are immutable shared data. Changing
an actor animation selects cached definitions; it does not rebuild textures.
Pixel-sensitive validations should assert dimensions, offsets, cell order,
palette results, and hashes from real generated data.

## Palettes and background state

`OracleWorldData` owns the live gameplay background palette slots shared by
rooms, dialogue, and palette effects. Source palette writes update those slots
at their original boundary and rerender affected presentation without replacing
logical room data.

Keep these concepts separate:

- logical room layout and collision;
- the currently rendered room texture;
- temporary palette state;
- position-specific visual, tile-mapping, or collision overrides;
- dynamic background tiles and ordinary tileset animation;
- OAM sprites.

Some original operations change one layer without immediately changing the
others. Model the traced split explicitly rather than forcing every tile change
through a generic “replace and redraw everything” operation. Clear transient
overrides at the same room/replacement boundary as the original.

Gameplay UI, dialogue, and menu presentation use shared tile/OAM composition
helpers with their imported layouts and palettes. A feature should provide
source-specific data, not copy pixel-decoding loops.

## Audio determinism and lifecycle

`OracleSoundEngine` is persistent across gameplay scenes and advances once per
original 60 Hz update. Generated sound data supplies music/SFX descriptors and
channel programs. The engine owns square, wave, and noise channels, priority,
music/SFX replacement, envelopes, fades, vibrato, pitch, and master volume.

Gameplay requests the original sound ID at the original update. Preserve the
ordering of simultaneous requests and sound-control operations. If the source
selects a variation with the global game RNG, consume `OracleRandom`; a private
audio RNG changes later gameplay.

The production engine retains real sequencer and channel state, not last-call
or request-count audit fields. Validation attaches an observer for ordered
requests and inspects sequencer state when required.

On shutdown, stop generated playback, detach buffers and signals, and release
references. Headless validation must not leave an
`AudioStreamGeneratorPlayback` retained.

## Adding graphics or sound behavior

1. Trace the graphics source, OAM/animation tables, palettes, draw order, sound
   IDs, request timing, and any RNG use.
2. Extend the owning importer stage and retain source labels/IDs.
3. Use the shared cache, renderer, palette state, and sound engine; add a shared
   primitive only when multiple source mechanisms truly match.
4. Keep logical state separate from presentation offsets and transient visual
   overrides.
5. Validate exact animation updates, OAM bounds/offsets/pixels, palette writes,
   sound request order, channel state, RNG aftermath, and cleanup.

Source-specific sprite quirks, sound sequences, and one-off constants belong
beside the importer/runtime implementation and focused validation, not in this
guide.
