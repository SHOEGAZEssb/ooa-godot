# Implementation status

This page is a durable coverage summary, not a chronological changelog. It
describes what contributors can currently exercise and which large systems are
still intentionally incomplete. Exact parity for an implemented path is still
enforced by its source trace and regression; the label "implemented" does not
claim that the entire surrounding game is complete.

## Current coverage

- Import pipeline for all 1,536 expanded Ages room layouts and 103 concrete
  tilesets, including palettes, metatiles, attributes, collision, navigation,
  warps, maps, object records, dialogue, sprites, and all 223 sound IDs.
- Original-resolution room rendering, collision, animated terrain, scrolling
  and warp transitions, time portals, dungeon neighbor resolution, and
  persistent visited/layout flags.
- Link movement, level-1 sword combat, terrain hazards, push blocks, signs,
  chests, item drops, basic bracelet interactions, and representative NPCs,
  including past Lynna room `1:48`'s pickaxe worker and story-selected cast.
- Keese, Octoroks/projectiles, Zols, and Gels using ordered room-object placement,
  original spawn restrictions, shared RNG, combat, and drop paths.
- Title/file select, three save slots, new-file name/message-speed setup, the
  new-game intro, original save image/checksum, previous-generation backups,
  death checkpoints, and explicit Save & Quit flows.
- HUD, dialogue, inventory pages, map/dungeon map, live flag editor, and shared
  fixed-update menu lifecycle.
- Typed treasure behavior for imported collection modes and WRAM-backed
  inventory fields currently consumed by the game.
- The imported sound sequencer with square, wave, and noise channels, channel
  priority, music/SFX ownership, envelopes, vibrato, pitch behavior, and room
  music assignments.
- Early-game story/cutscene paths for Impa, the Triforce stone, Ralph's portal,
  first arrival in the past, the Maku Tree disappearance, and Nayru's
  introduction/aftermath. Script-driven portions use the typed command runner;
  native transition/presentation objects retain specialized controllers.

## Deferred or partial systems

- The complete story, world interactions, NPC scripts, dungeons, bosses, enemy
  roster, and progression beyond the currently ported paths.
- Full active-item behavior, held objects, lifting/throwing, swimming/diving,
  ledges, terrain-specific Link states, and complete low-health/death handling.
- Secret entry, linked-game/Game Link behavior, ring appraisal and the wider
  ring-effect system beyond the currently represented inventory state.
- Remaining dynamic inventory text/count overlays and specialized item OAM.
- Sound calls owned by interactions and objects that have not yet been ported.
- A possible cell-based room renderer; the current full-room texture path remains
  authoritative until a staged migration proves parity.

See [TODO.md](../TODO.md) for engineering consolidation tasks. New gameplay work
should be selected from traced original interactions and should include imported
data, runtime behavior, and validation in the same slice.

The [NPC and room-event implementation guide](npcs-and-events.md) documents the
current source-to-importer-to-runtime workflow used to expand that coverage.
