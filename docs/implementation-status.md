# Implementation status

This is a high-level coverage boundary, not a changelog or exhaustive feature
inventory. “Implemented” means the named path has source-traced runtime behavior
and headless coverage; it does not imply that adjacent game content is complete.
Use the validation files and runtime/importer dispatch for exact coverage.

## Implemented foundations

- Import pipeline for the complete room and tileset data set, including world
  graphics, palettes, collision, navigation, room objects, dialogue, menus,
  sprite data, and all sound IDs used by the supported ROM.
- Original-resolution room rendering, animated terrain, top-down and
  side-scrolling room foundations, collision, scrolling/warp transitions,
  dungeon layout neighbors, time portals, and persistent room/map state.
- Application-owned 60 Hz scheduling, buffered input, global deterministic RNG,
  ordered room-object parsing, transition preload/freeze, and headless scenario
  isolation.
- Title and file-select flow, three explicit-save slots with backup recovery,
  new-game presentation, HUD, dialogue, map/dungeon map, inventory foundations,
  ring appraisal/list screens, save/quit and game-over flows, plus development
  navigation and state tools.
- Imported music/SFX sequencing with square, wave, and noise channels, channel
  priority, envelopes, fades, vibrato, and room music ownership.

## Playable gameplay coverage

- Core Link movement and collision; level-1 sword combat; common terrain,
  hazards, chests, drops, push blocks, and breakable-object interactions.
- Substantial item coverage including active Bomb, Shovel, Seed Satchel paths,
  Mystery Seed/Owl behavior, level-1 Roc's Feather, Harp/time-portal
  foundations, common treasure transactions, and many ring effects.
- A growing shared enemy and interaction roster with deterministic placement,
  combat, drops, projectiles, and native object behavior.
- Spirit's Grave (dungeon `$01`) and Wing Dungeon (dungeon `$02`) are playable
  end to end, including their principal rooms, puzzles, side-view passages,
  minibosses, bosses, rewards, and Essences.
- Selected overworld NPC families, shops/trades, Gasha and Seed Tree systems,
  Maple encounters, early-game Impa/Ralph/Nayru/Maku sequences, and additional
  traced story slices through and around the first two dungeons.

## Major incomplete areas

- The full story and world progression beyond the ported slices.
- Remaining dungeons, bosses, enemy species/subids, NPC scripts, room-event
  families, door/controller variants, and companion systems.
- Several active items and upgrades, including complete swimming/diving and
  terrain-specific Link states, Roc's Cape continuation, active Scent/Pegasus/
  Gale Seed behavior, and remaining grabbable-object species.
- Unimplemented or partial ring consumers whose base gameplay systems do not
  yet exist.
- Linked-game transport and external Game Link functionality.
- Graphics and sound requests owned by gameplay objects that have not yet been
  ported.

Unsupported imported behavior is classified and rejected or safely suppressed
with source context; it must not fall back to a graphics-only or approximate
implementation.

When a broad boundary changes, edit one bullet here. Put detailed IDs,
room-specific behavior, and exact branch coverage in the importer/runtime code
and focused validations, where they can remain synchronized with the feature.
