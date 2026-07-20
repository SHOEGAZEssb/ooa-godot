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
  chests, item drops, basic bracelet interactions, the active Shovel, and the
  first Seed Satchel's active Ember Seed path. The Satchel uses its selected
  packed-BCD counter, grants and immediately displays the original initial 20
  Ember Seeds, uses the distinct storage/equipped inventory icon sheets, exact
  four directional child offsets, eight-update Link pose,
  `SPEED_c0`/speedZ/gravity flight, hazard disposal, enemy contact, 58-update
  bank-1 `spr_common_sprites` multi-cell OAM flame, imported source-`$0c` tile
  ignition, persistent room-flag substitutions, all eight `$dc:$08` tile-change
  watchers, all 56 single-tile reload changes (including room `0:48`'s
  permanent tree removal), landing/flame sounds, and
  two-digit HUD/inventory ammo display. The
  shared chest path renders all reward objects from imported
  `INTERAC_TREASURE $60` graphics/OAM (including room `4:08`'s small key) and
  preserves the open/collection/get-item sound boundaries. Push blocks request their move
  cue only on accepted movement and distinguish the imported falling-hole
  animation/sound from splash effects. The
  Shovel uses the original 23-update Link/item animation, update-4 tile probe,
  imported breakable-tile replacements and drops, directional dirt debris,
  `SPEED_a0` cardinal drop launch, sounds, room flags, and WRAM-backed gasha
  maturity.
- Typed treasure behavior for imported collection modes and WRAM-backed
  inventory fields currently consumed by the game. Static `$dc:$07` ground
  Heart Pieces use their original two-hand pickup, text, sound, and room-item
  flag `$20` re-entry suppression in all eight source placements. The fourth
  piece's inline 2x2 diagram changes from the previous quarter count on the
  30th update, clears the piece counter, then hands off to TX `$0049` while
  granting and refilling the four-quarter Heart Container.
  The same reusable treasure entity now also supports falling spawn mode `$02`
  and one-hand grab mode `$01`, used by the adult Maku Tree's Seed Satchel.

### NPCs and enemies

- Representative NPCs, including past Lynna room `1:48`'s pickaxe worker and
  story-selected cast, room `1:49`'s linked family tableau, room `1:57`'s
  palette- and story-selected female villager, and room `1:58`'s complete
  hobo/Impa/Nayru story predicates, dialogue, facing, and placement. Room
  `1:75` includes both mutually exclusive Black Tower hardhat phases with
  exact `getBlackTowerProgress` predicates and text. Room `1:86` includes its
  entrance guard's essence/room-flag phases, dialogue, facing, and movement.
- Lower Black Tower rooms `4:e0`, `4:e1`, `4:e2`, `4:e7`, and `4:e8`, including
  the moving path-blocking villager, unconditional construction soldiers,
  per-talk random worker text, left/right pickaxe strikes and dirt chips,
  half-pixel hardhat patrols, and the exact Shovel grant/held-item sequence.
- Keese, Octoroks/projectiles, masked Moblins `$20:$00` and their arrows,
  ordinary Stalfos `$31:$00`, Zols, and Gels using
  ordered room-object placement, original spawn restrictions, shared RNG,
  combat, common/split kill sounds, hole-fall sounds, and drop paths. All 34
  ordinary-Stalfos records (37 instances) use their source SPEED_80 walk,
  two-call direction/counter decision, wall/hole bounce, animation, damage,
  health, and drop path. Evasive, bone-throwing, and stomping Stalfos subids
  remain deferred.
- Reusable dungeon buttons `$09`, trigger shutters `$1e:$04-$07`, permanent
  trigger chests `$20:$00`, retractable trigger chests `$21:$17`, push-block
  triggers `$13:$01`, and enemy shutters `$1e:$08-$0b`, with all 155 direct
  placements imported in source order: 49 buttons, 20 trigger doors, seven
  delayed chests, six retractable chests, and 73 live-enemy mechanism records.
  Room `4:08` includes its exact-`$01` button predicate, solve/puff sequence,
  15-update chest delay, and room-item re-entry state. Room `4:7a` covers the
  reusable chest's immediate appearance and restoration of its original tile
  when its exact trigger byte is released. Room `4:09` includes its one-shot bit-0 button
  and simultaneous up/right shutters. Room `4:22` covers reusable ground-only
  strict-radius pressure, `$0c/$0d` tiles, `SND_SPLASH`, release/closing, and the
  28-update object-pressure delay, including local door respawn if a shutter
  closes on Link. Button subids select trigger bits and latch mode without
  save/story predicates. Room `4:0c` includes its trigger-owned
  live enemy count, source block restoration,
  30-update release, eight-update solve wait, and six-update mapping-interleaved
  up-door animation. Room `4:0b` includes its always-active three-Gel combat
  gate, simultaneous up/left shutters, real sword-death path, and entry/re-entry
  behavior: a left scroll substitutes only the crossed shutter with non-solid
  floor, waits until Link is fully inside, then completes its six-update close;
  the original transient last-eight-room enemy bitset suppresses defeated Gels
  and reopens both doors without replaying the solve cue. Imported shutter rooms
  solve only when the implemented enemy roster can provide their complete live
  enemy count. In incomplete rooms, the crossed entry shutter still preloads as
  open and remains available only for safe backtracking; all other shutters
  stay closed. Room `4:06` covers both entry directions, delayed crossed-door
  closure, its two ordinary Stalfos, the all-direction source push block, and
  the complete 30/8/6-update block-trigger/shutter solve.
- Reusable small-key door tiles `$70-$73`, including current-dungeon key
  consumption, TX `$5100`, paired dungeon-layout room flags, the 10-update push
  threshold, key sprite, and six-update mapping-interleaved opening. Room
  `4:0a` exercises the left-facing `$73` path and persistent re-entry tile
  substitution.

### Story and events

- Early-game story/cutscene paths for Impa, the Triforce stone, Ralph's portal,
  first arrival in the past, the Maku Tree disappearance, and Nayru's
  introduction/aftermath. Script-driven portions use the typed command runner;
  native transition/presentation objects retain specialized controllers.
- Room `1:38`'s complete Maku Sprout rescue, including the exact
  `wMakuTreeState`/saved-flag predicate, synchronized jumping interaction
  Moblins, replacement by two ordinary masked-Moblin enemies, live enemy-count
  dialogue branches, Link approach and post-fight repositioning, four-phase
  interleaved gate opening with puffs/shake/sounds, transition locking, room
  music restoration, pre-display initialization of the distressed sprout and
  both Moblins, the final zero-distance DIR_UP waypoint, TX `$05d4`'s explicit
  lower textbox, advice/saved/map-text/layout writes, the active `$e1:$02` time
  portal on the bottom exit to room `1:48`, and completed TX `$05d5` re-entry
  state.
- Room `0:38`'s immediate post-rescue adult Maku Tree event, selected by
  `wMakuTreeState=$02`: the complete 68-command dialogue/NPC loop, all five
  expressions, bottom text, Yes-to-repeat/No-to-continue choice, present-map
  advice writes, Maku Tree music, and the Link-relative Seed Satchel drop. The
  Satchel waits 40 updates, falls from above the screen, bounces once with both
  landing cues, persists room bit `$80` and its selected X coordinate when
  left behind, respawns at Y `$58`, uses the one-hand item pose, and is
  suppressed by room item bit `$20` after collection.
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
- Room `4:e7`'s screen-warp-only dungeon entrance interaction, including its
  entry-side Y predicate, TX `$020f`, one-shot deletion, and death-checkpoint
  update. Direct debug loads and ordinary scrolling delete it on their first
  post-load update because the whiteout scroll-mode bit is clear.

### Interface and persistence

- Title/file select, three save slots, new-file name/message-speed setup, the
  new-game intro, original save image/checksum, previous-generation backups,
  death checkpoints, and explicit Save & Quit flows.
- HUD (including simultaneous rupee digits and the dungeon-only
  `gfx_key`/X/key-count field), dialogue, inventory pages, map/dungeon map,
  live flag editor, and shared fixed-update menu lifecycle.

### Audio

- The imported sound sequencer with square, wave, and noise channels, channel
  priority, music/SFX ownership, envelopes, vibrato, pitch behavior, and room
  music assignments.

## Deferred or partial systems

### World, story, and actors

- The complete story, world interactions, NPC scripts, dungeons, bosses, enemy
  roster, and progression beyond the currently ported paths.
- Door-controller variants for bosses, switches, minecarts, room entry, and
  torches (`$1e` subids outside `$08-$0b`) remain deferred.

### Player and inventory

- Full active-item behavior, held objects, lifting/throwing, swimming/diving,
  ledges, terrain-specific Link states, and complete low-health/death handling.
- Satchel selection and the active Scent, Pegasus, Gale, and Mystery Seed
  state machines remain deferred; the first acquired Satchel's Ember path is
  implemented and unsupported selected child IDs report a source-aware error
  without consuming ammo.
- Shovel drop `$0f` consumes its third RNG value and supports the 100-Rupee
  branch; its rope/beetle branches remain suppressed until those enemies are
  implemented.
- Secret entry, linked-game/Game Link behavior, ring appraisal and the wider
  ring-effect system beyond the currently represented inventory state.

### Graphics and audio

- Remaining dynamic inventory count overlays outside the implemented selected
  Satchel seed quantity and specialized item OAM.
- Sound calls owned by interactions and objects that have not yet been ported.
- A possible cell-based room renderer; the current full-room texture path remains
  authoritative until a staged migration proves parity.

See [TODO.md](../TODO.md) for engineering consolidation tasks. New gameplay work
should be selected from traced original interactions and should include imported
data, runtime behavior, and validation in the same slice.

The [NPC and room-event implementation guide](npcs-and-events.md) documents the
current source-to-importer-to-runtime workflow used to expand that coverage.
