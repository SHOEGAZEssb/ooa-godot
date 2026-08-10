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
- Clean-US boot/attract/title replay and skip flow, file select, three
  explicit-save slots with backup recovery, new-game presentation, HUD,
  dialogue, map/dungeon map, inventory foundations, ring appraisal/list
  screens, save/quit and game-over flows, plus development navigation and
  state tools.
- Imported music/SFX sequencing with square, wave, and noise channels, channel
  priority, envelopes, fades, vibrato, and room music ownership.

## Playable gameplay coverage

- Core Link movement and collision; level-1 sword combat; common terrain,
  hazards, chests, drops, push blocks, and breakable-object interactions.
- Substantial item coverage including active Bomb, Shovel, Seed Satchel paths,
  Mystery Seed/Owl behavior, level-1 Roc's Feather, Harp/time-portal
  foundations, source-timed top-down Flippers swimming/normal-water diving,
  source-placed normal-water dive transitions, side-view Flippers swimming,
  common treasure transactions, and many ring effects.
- A growing shared enemy and interaction roster with deterministic placement,
  combat, drops, projectiles, and native object behavior.
- Spirit's Grave (dungeon `$01`) and Wing Dungeon (dungeon `$02`) are playable
  end to end, including their principal rooms, puzzles, side-view passages,
  minibosses, bosses, rewards, and Essences.
- Selected overworld NPC families, shops/trades, Gasha and Seed Tree systems,
  Maple encounters, early-game Impa/Ralph/Nayru/Maku sequences, and additional
  traced story slices through and around the first two dungeons.
- Moosh's rescue and mountable-companion core: exact ride visuals, movement,
  hover/charged stomp and charge flash, collision-safe dismount/remount memory,
  source-timed warning hover and water/hole hazard respawn, and single-owner
  scrolling retention, including room `0:5b`'s one-time flutter tutorial.
- Ricky's room `0:6a` glove handoff and mountable-companion core: source-loaded
  ride graphics, normal/hole/cliff jumps, punch/tornado charge, landing tile
  breaks, hazards, dismount/remount memory, and single-owner scrolling.
- Rafton's completed raft: source placements in rooms `1:a7`/`1:a9`, airborne
  boarding, exact water-only collision and SPEED_e0 steering, blocked-direction
  dismount timing, Link/item restrictions, directional animation, local respawn
  and remembered-position persistence, and single-owner room scrolling. The
  room `1:a8` raft-wreck sequence includes its source command timing, storm
  effects, completion flag, raft retirement, and hardcoded `1:aa` warp. Its
  destination continues into the first Tokay theft cutscene with the imported
  washed-up Link animation, exact item-loss cadence, thief movement and exits,
  completion flag, respawn update, music restoration, and input release.
- Non-dungeon Tokay Island NPCs and interactions: ordinary island dialogue,
  stolen-item recovery, linked Rosa, scent-seedling and shield rewards, the
  trading hut, and past/present Wild Tokay gameplay with imported patterns and
  prizes; all five source-placed vine sprouts retain and restore their terrain
  while pushing and persist their room positions. The three Tokay sprouts grow
  the corresponding two-room present vines at their source positions and leave
  the source withered-vine tile when misaligned. The southern entrance
  Eyeball/socket sequence places the second eye and opens the doorway with the
  original timing. Present Sand Crabs and past red Leevers use their imported
  non-dungeon placements, source RNG/counters, movement, and combat. The
  separate Dimitri mount controller and linked-secret input/return generation
  remain partial shared-system boundaries.
- Tingle in room `0:79`: balloon pop/fall, normal friendship and Island Chart
  sequence, Seed Satchel upgrade path, kooloo-limpah animation, and Ricky's
  departure. Postgame secret entry and return-secret generation remain partial.

## Major incomplete areas

- The full story and world progression beyond the ported slices.
- Remaining dungeons, bosses, enemy species/subids, NPC scripts, room-event
  families, door/controller variants, companion systems, and Moosh's
  terrain-specific cliff states.
- Several active items and upgrades, including top-down Mermaid Suit movement
  and remaining deep-water transitions, other terrain-specific Link states,
  Roc's Cape continuation, active Scent/Pegasus/Gale Seed behavior, and
  remaining grabbable-object species.
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
