# Implementation status

This page is a durable coverage summary, not a chronological changelog. It
describes what contributors can currently exercise and which large systems are
still intentionally incomplete. Exact parity for an implemented path is still
enforced by its source trace and regression; the label "implemented" does not
claim that the entire surrounding game is complete.

## Current coverage

### Data and world

- Import pipeline for all 1,536 expanded Ages room layouts and 103 concrete
  tilesets, including palettes, metatiles, attributes, collision, navigation,
  warps, maps, object records, dialogue, sprites, and all 223 sound IDs.
- Original-resolution room rendering, collision, animated terrain, scrolling
  and warp transitions, time portals, dungeon neighbor resolution, and
  persistent visited/layout flags.

### Player, combat, and items

- Link movement, level-1 sword combat, terrain hazards, push blocks, signs,
  chests, item drops, and basic bracelet interactions.
- Typed treasure behavior for imported collection modes and WRAM-backed
  inventory fields currently consumed by the game. Static `$dc:$07` ground
  Heart Pieces use their original two-hand pickup, text, sound, and room-item
  flag `$20` re-entry suppression in all eight source placements. The fourth
  piece's inline 2x2 diagram changes from the previous quarter count on the
  30th update, clears the piece counter, then hands off to TX `$0049` while
  granting and refilling the four-quarter Heart Container.

### NPCs and enemies

- Representative NPCs, including past Lynna room `1:48`'s pickaxe worker and
  story-selected cast, room `1:49`'s linked family tableau, room `1:57`'s
  palette- and story-selected female villager, and room `1:58`'s complete
  hobo/Impa/Nayru story predicates, dialogue, facing, and placement. Room
  `1:75` includes both mutually exclusive Black Tower hardhat phases with
  exact `getBlackTowerProgress` predicates and text. Room `1:86` includes its
  entrance guard's essence/room-flag phases, dialogue, facing, and movement.
- Keese, Octoroks/projectiles, Zols, and Gels using ordered room-object placement,
  original spawn restrictions, shared RNG, combat, and drop paths.

### Story and events

- Early-game story/cutscene paths for Impa, the Triforce stone, Ralph's portal,
  first arrival in the past, the Maku Tree disappearance, and Nayru's
  introduction/aftermath. Script-driven portions use the typed command runner;
  native transition/presentation objects retain specialized controllers.
- Room `1:75`'s complete linked and unlinked pre-Black Tower sequences,
  including Ralph's departure, the heritage scene, coordinated Impa/Nayru/Zelda
  lanes, Link movement, spawned effects, and persistent completion state.
- Room `1:76`'s invisible `$dc:$10` Black Tower doorway handler, including its
  transient `$44/$45` tile clears, initial-overlap exit latch, strict combined
  collision radii, current-room bit `$01` destination selection (`4:e7` or
  `4:f3`), `$93/$ff/$01` entrance transition, and cave-entry sound.
- Room `1:86`'s stage-0 Black Tower explanation, including its imported
  background/OAM/palettes, shared-RNG lightning, saved Link return position and
  direction, same-room transition `$0c`, and `$40` to `$80` aftermath.

### Interface and persistence

- Title/file select, three save slots, new-file name/message-speed setup, the
  new-game intro, original save image/checksum, previous-generation backups,
  death checkpoints, and explicit Save & Quit flows.
- HUD, dialogue, inventory pages, map/dungeon map, live flag editor, and shared
  fixed-update menu lifecycle.

### Audio

- The imported sound sequencer with square, wave, and noise channels, channel
  priority, music/SFX ownership, envelopes, vibrato, pitch behavior, and room
  music assignments.

## Deferred or partial systems

### World, story, and actors

- The complete story, world interactions, NPC scripts, dungeons, bosses, enemy
  roster, and progression beyond the currently ported paths.

### Player and inventory

- Full active-item behavior, held objects, lifting/throwing, swimming/diving,
  ledges, terrain-specific Link states, and complete low-health/death handling.
- Secret entry, linked-game/Game Link behavior, ring appraisal and the wider
  ring-effect system beyond the currently represented inventory state.

### Graphics and audio

- Remaining dynamic inventory count overlays and specialized item OAM.
- Sound calls owned by interactions and objects that have not yet been ported.
- A possible cell-based room renderer; the current full-room texture path remains
  authoritative until a staged migration proves parity.

See [TODO.md](../TODO.md) for engineering consolidation tasks. New gameplay work
should be selected from traced original interactions and should include imported
data, runtime behavior, and validation in the same slice.

The [NPC and room-event implementation guide](npcs-and-events.md) documents the
current source-to-importer-to-runtime workflow used to expand that coverage.
