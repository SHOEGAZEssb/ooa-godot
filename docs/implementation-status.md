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
  chests, item drops, exact imported grass/bush cut debris OAM, timing, sound,
  underwater palette, and grass subid flicker, the active Shovel, and the
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
- Accepted damage uses Link's global-frame-bit-2 OBJ-palette-5 blink across
  ordinary and transformation poses. Lethal damage supports the Potion
  interception, slow music fade, post-knockback `SND_LINK_DEAD` four-loop spin,
  exact update-135 collapse and 76-update hold, then the forced game-over menu
  and checkpoint restart.
- Complete Ages Gasha Seed planting and Gasha Tree behavior across all 16
  source spots: buried-soil Discovery Ring cues, no-seed/Yes/No dialogue,
  packed-BCD consumption, persisted planted bits and kill counters, 20/40-kill
  sprout/tree/nut thresholds, source collision and nut launch, exact shared-RNG
  rank/maturity reward distributions and ring tiers, first-nut/repeated-Heart-
  Piece/Potion exceptions, all maturity sources and debit, two-hand reward
  text, displayed Heart/Rupee wait, nine shrink frames, grass/dirt/sand ground
  restoration, reusable soft soil on re-entry, and planted-spot map popups.
- The Shield is an active held-button parent item on either A or B. Wooden,
  Iron, and Mirror Shield ownership use `wShieldLevel`; equipped-but-lowered
  and raised states select the original level-aware Link frames in all four
  directions. Raising it uses the source directional collision rectangle and
  `SND_SHIELD`, while implemented Octorok rocks and masked-Moblin arrows use
  their original `COLLISIONEFFECT_$1f` clink and 32-update bounce paths.
- Sword hits select the source low/normal/high enemy-recoil profiles for
  level-1, level-2/3, held, Spin Attack, sword-beam, Fist Ring, and Expert's
  Ring collisions. Implemented vulnerable species move away from the attack at
  `SPEED_200` for the exact `$08/$0b/$0f` counter, stop on the handler's
  terrain or screen boundary, pause ordinary AI, and retain handler-specific
  hazard checks. Lethal hits disable collision, finish recoil while still
  visible, and run the death effect on the following update at the final
  position. Zols and Gels use their table-defined `$20`-invincibility no-recoil
  response; implemented bosses retain their separate no-recoil collision
  policies. Positive enemy invincibility also uses global-frame-bit-2
  four-update damage blinking through OBJ palette `$05` (or `$02` when the
  source palette is already `$05`) across both typed-sprite and generic
  imported definitions.
- Octoroks, Boomerang Moblins, Ropes, ordinary Stalfos, masked Moblins, Zols,
  and Gels use the common left-first `yh+$05,xh-$01` /
  `yh+$05,xh+$01` grounded hazard probes in
  normal movement and after recoil. Water/lava create their imported
  12/20-update splash and `SND_SPLASH` immediately. Holes disable collision
  while retaining the visible enemy for the source 60-update pull, applying a
  signed-8.8 `SPEED_80` center step every eight updates before handing off to
  imported `INTERAC_FALLDOWNHOLE` and `SND_FALLINHOLE`; Zols and Gels freeze
  their current frame while the other supported species use the accelerated
  pull animation clock.
- The Power Bracelet is an active held-button parent on either A or B. It
  requires the original paired directional wall bits, retains the button while
  searching, waits for the opposite-direction pull through
  `LINK_ANIM_MODE_LIFT_3`, and uses the exact pull/strain Link OAM. An accepted
  `BREAKABLETILESOURCE_BRACELET` metatile is removed at the 11-update boundary,
  preserves its replacement/drop/room-flag effects, and becomes a transparent
  OBJ-palette mimic of its live BG mapping, flips, animation, and palette. The
  13-update lift, temporary Link-collision disable, held walking offsets,
  either-button release, eight-update throw pose, weight-0 8.8 gravity and
  `SPEED_180` motion are implemented,
  including `SND_PICKUP`/`SND_THROW`, wall/landing/hazard results, enemy damage
  without consuming the thrown tile, and imported `$06/$0c` rock-debris
  variants. The Toss Ring selects the source `SPEED_280` path. Power Glove
  ownership remains the level-2 upgrade of the same inventory item and
  accelerates non-heavy push blocks from `SPEED_80/$20` to
  `SPEED_c0/$15`, while
  grabbable native entities such as Pumpkin Head use the shared lift/throw
  parent instead of bypassing its poses and sounds.
- Typed treasure behavior for imported collection modes and WRAM-backed
  inventory fields currently consumed by the game. Static `$dc:$07` ground
  Heart Pieces use their original two-hand pickup, text, sound, and room-item
  flag `$20` re-entry suppression in all eight source placements. The fourth
  piece's inline 2x2 diagram changes from the previous quarter count on the
  30th update, clears the piece counter, then hands off to TX `$0049` while
  granting and refilling the four-quarter Heart Container.
  The same reusable treasure entity now also supports falling spawn mode `$02`
  and one-hand grab mode `$01`, used by the adult Maku Tree's Seed Satchel.
- The complete 64-entry ring catalog, unappraised queue, obtained-ring bitset,
  Ring Box levels/slots, equipped-ring state, and appraisal count use their
  original WRAM fields. Vasu's appraisal and ring-list screens use the imported
  tilemaps, graphics, palettes, and TX `$30xx` text, including 16-ring pages,
  19-update page scrolls, cursor/marker behavior, appraisal costs, duplicate
  refunds, the hundredth-ring award flag, box transfers, equip/unequip, and
  save persistence. The active-ring dispatcher defines the source policy for
  every ring ID, but a policy is counted as implemented only when its production
  gameplay consumer is reachable; the per-ring table below records that
  distinction. Punches retain their source collision/damage/animation/sound
  paths, sword beams use their one-object cap and imported directional OAM, and
  transformations are suppressed by underwater/side-scrolling tilesets,
  menu-disabled events, and shop entities as in the original.

#### Ring-effect coverage

This table covers the effect of *wearing* every ring, independently of whether
the ring's ordinary acquisition location is currently reachable. **Implemented**
means that the effect has a production gameplay consumer. **Partial** means that
some exact behavior is connected but another required item, drop, or world
system is absent. **Deferred** means that the source policy is encoded and
validated but the base system does not call it yet. **Correct no-op** identifies
collector or achievement rings that intentionally have no worn effect; these
are not missing implementations. [`RingEffects.cs`](../src/inventory/RingEffects.cs)
remains the single runtime policy table.

| ID | Ring | Coverage | Current behavior or missing consumer |
| --- | --- | --- | --- |
| `$00` | Friendship Ring | Correct no-op | Wearing it intentionally does nothing. Vasu's initial award path is implemented. |
| `$01` | Power Ring L-1 | Implemented | Applies the level-1 sword increase and incoming-damage increase. |
| `$02` | Power Ring L-2 | Implemented | Applies the level-2 sword increase and incoming-damage increase. |
| `$03` | Power Ring L-3 | Implemented | Applies the level-3 sword increase and incoming-damage increase. |
| `$04` | Armor Ring L-1 | Implemented | Applies the level-1 incoming-damage reduction and sword reduction. |
| `$05` | Armor Ring L-2 | Implemented | Applies the level-2 incoming-damage reduction and sword reduction. |
| `$06` | Armor Ring L-3 | Implemented | Applies the level-3 incoming-damage reduction and sword reduction. |
| `$07` | Red Ring | Implemented | Doubles sword damage. |
| `$08` | Blue Ring | Implemented | Halves incoming damage using the original signed arithmetic. |
| `$09` | Green Ring | Implemented | Increases sword damage and reduces incoming damage. |
| `$0a` | Cursed Ring | Implemented | Halves sword damage and doubles incoming damage. |
| `$0b` | Expert's Ring | Implemented | Enables the stronger unarmed punch, including tile hits, collision, animation, and sound. |
| `$0c` | Blast Ring | Deferred | Bomb-damage policy exists; active Bomb behavior does not. |
| `$0d` | Rang Ring L-1 | Deferred | Level-1 boomerang-damage policy exists; active Boomerang behavior does not. |
| `$0e` | GBA Time Ring | Correct no-op | Wearing it intentionally does nothing. Its Game Link/GBA acquisition path is unavailable. |
| `$0f` | Maple's Ring | Deferred | The 15-kill meeting threshold is encoded and the kill counter advances, but Maple encounters do not consume it. |
| `$10` | Steadfast Ring | Implemented | Halves Link's enemy-contact knockback duration. |
| `$11` | Pegasus Ring | Deferred | Extended-duration policy exists; active Pegasus Seed behavior does not. |
| `$12` | Toss Ring | Implemented | Raises the implemented Bracelet weight-0 throw from `SPEED_180` to `SPEED_280`. |
| `$13` | Heart Ring L-1 | Implemented | Restores the source amount after the level-1 movement-distance threshold. |
| `$14` | Heart Ring L-2 | Implemented | Restores the source amount after the level-2 movement-distance threshold. |
| `$15` | Swimmer's Ring | Deferred | Fast-swim policy exists; swimming does not. |
| `$16` | Charge Ring | Implemented | Advances the sword charge counter four times as fast. |
| `$17` | Light Ring L-1 | Implemented | Extends sword-beam eligibility to two Hearts below full health. |
| `$18` | Light Ring L-2 | Implemented | Extends sword-beam eligibility to three Hearts below full health. |
| `$19` | Bomber's Ring | Deferred | Two-Bomb placement policy exists; active Bomb behavior does not. |
| `$1a` | Green Luck Ring | Deferred | Half-damage policy exists; blade-trap damage has no production consumer. |
| `$1b` | Blue Luck Ring | Deferred | Half-damage policy exists; enemy-beam damage has no production consumer. |
| `$1c` | Gold Luck Ring | Implemented | Halves damage from the implemented hole-fall path. |
| `$1d` | Red Luck Ring | Deferred | Half-damage policy exists; spiked-floor damage has no production consumer. |
| `$1e` | Green Holy Ring | Deferred | Electricity immunity is encoded; electricity damage has no production consumer. |
| `$1f` | Blue Holy Ring | Deferred | Zora-fire immunity is encoded; Zora fire has no production consumer. |
| `$20` | Red Holy Ring | Implemented | Prevents damage from implemented Octorok rock projectiles. |
| `$21` | Snowshoe Ring | Deferred | Ice-slip immunity is encoded; ice movement does not yet slide Link. |
| `$22` | Roc's Ring | Deferred | Cracked-floor protection is encoded; crumbling floors are absent. |
| `$23` | Quicksand Ring | Deferred | Quicksand immunity is encoded; quicksand movement is absent. |
| `$24` | Red Joy Ring | Implemented | Doubles Rupees collected from implemented enemy/item drops. |
| `$25` | Blue Joy Ring | Implemented | Doubles Hearts collected from implemented enemy/item drops. |
| `$26` | Gold Joy Ring | Partial | Doubles the currently supported Heart and Rupee drops; the other source drop kinds are not implemented. |
| `$27` | Green Joy Ring | Deferred | Ore-doubling policy exists; Ore Chunk drops do not. |
| `$28` | Discovery Ring | Implemented | Requests the source compass cue when a Gasha interaction receives its first enabled update in a room containing its buried or exposed spot. |
| `$29` | Rang Ring L-2 | Deferred | Level-2 boomerang-damage policy exists; active Boomerang behavior does not. |
| `$2a` | Octo Ring | Implemented | Uses the Octorok Link disguise and transformed-Link restrictions. |
| `$2b` | Moblin Ring | Implemented | Uses the Moblin Link disguise and transformed-Link restrictions. |
| `$2c` | Like Like Ring | Implemented | Uses the Like Like Link disguise and transformed-Link restrictions. |
| `$2d` | Subrosian Ring | Implemented | Uses the Subrosian Link disguise and transformed-Link restrictions. |
| `$2e` | First Gen Ring | Implemented | Uses the first-generation Link disguise and transformed-Link restrictions. |
| `$2f` | Spin Ring | Implemented | Extends the sword action to the source double-spin duration and collision arcs. |
| `$30` | Bombproof Ring | Deferred | Own-Bomb immunity is encoded; active Bomb behavior does not exist. |
| `$31` | Energy Ring | Implemented | Replaces the charged Spin Attack with the implemented sword beam and poke handoff. |
| `$32` | Dbl. Edged Ring | Implemented | Adds the source sword damage and hurts Link once after the first accepted hit. |
| `$33` | GBA Nature Ring | Correct no-op | Wearing it intentionally does nothing. Its Game Link/GBA acquisition path is unavailable. |
| `$34` | Slayer's Ring | Correct no-op | It is an achievement ring, not a modifier. The 1,000-kill counter and Vasu award are implemented. |
| `$35` | Rupee Ring | Correct no-op | It is an achievement ring, not a modifier. The 10,000-Rupee counter and Vasu award are implemented. |
| `$36` | Victory Ring | Correct no-op | It is an achievement ring, not a modifier. Vasu can honor its earned flag, but the Ganon victory path is absent. |
| `$37` | Sign Ring | Correct no-op | It is an achievement ring, not a modifier. The 100-broken-sign acquisition path is absent. |
| `$38` | 100th Ring | Correct no-op | It is an achievement ring, not a modifier. The appraisal counter and award are implemented. |
| `$39` | Whisp Ring | Deferred | Jinx immunity is encoded; jinx status behavior is absent. |
| `$3a` | Gasha Ring | Implemented | Enemy kills grant the source double increment to all persisted Gasha-spot counters; planting, growth, and reward consumers are active. |
| `$3b` | Peace Ring | Deferred | Held-Bomb explosion suppression is encoded; active held Bombs do not exist. |
| `$3c` | Zora Ring | Deferred | Unlimited-dive policy exists; swimming and diving do not. |
| `$3d` | Fist Ring | Implemented | Enables the ordinary unarmed punch, including collision, animation, and sound. |
| `$3e` | Whimsical Ring | Implemented | Uses the source RNG roll for usually weak and occasionally deadly sword damage and its lightning cue. |
| `$3f` | Protection Ring | Implemented | Forces implemented incoming attacks and terrain falls to one Heart of damage. |

### Dungeons

- Spirit's Grave (dungeon `$01`, rooms `4:10-$25`) is playable end to end.
  Its complete ordered room streams include Keese, Zols, ordinary Stalfos,
  Boomerang Moblins and their returning projectiles, Ropes, Ghini, and the
  delayed five-Wallmaster spawner/capture warp. Native dungeon interactions
  cover the bracelet reward, two moving-platform scripts with imported strict
  collision sizes, riding-object terrain suppression, and 8.8 Link movement,
  the two-torch staircase, falling small key, rotating colored cube/flames,
  pressure buttons, trigger and enemy shutters, small-key doors, the linked
  Ember-burnable wall and layout-only right-entry shutter in room `4:1d`, the
  retained D1 Boss Key and boss door, and all eight source chests (including
  Map, Compass, rings, Gasha Seed, keys, and Boss Key).
  Room `4:1b`'s revealed staircase follows its source warp into side-scrolling
  room `6:10`; groups `$06/$07` retain their active identity while sharing the
  original `$04/$05` tileset and object data, so the Bracelet reward and return
  warp are present.
  The cube uses PALH `$89`'s mixed OBJ palettes and source 20-update cardinal
  push test, while the Ghini/key room retains ordinary enemy-hit audio and the
  falling key's collection-behavior/get-item sound ordering. Room `4:1c`'s
  Ropes use the inclusive ten-pixel axis lock, fixed cardinal charge, collision
  release, `$40` cooldown, and source random wander reset. Its breakable rock
  metatiles use the imported four-stage `INTERAC_ROCKDEBRIS` chip animation,
  terminal-frame lifetime, and `SND_BREAK_ROCK`.
- Boomerang Moblin `$0a:$00`, Rope `$10:$00`, Ghini `$17:$00`, and
  Wallmaster `$28:$00` are shared species rather than D1-only adapters.
  Matching source placements instantiate outside rooms `4:10-$25` (including
  room `4:ed`'s six Ropes and room `4:c5`'s Wallmaster), while unsupported
  Rope/Ghini subids remain deferred. Wallmaster captures use each dungeon's
  imported destination (`$24` for dungeon `$01`, `$ce` for dungeon `$0b`).
- Giant Ghini and its three linked children implement the room `4:18`
  miniboss encounter, including the source `$16` forced Link entry before its
  crossed shutter closes, and persistent bidirectional portal. Pumpkin Head in
  room `4:13` implements the separately timed body/ghost/head ceiling entry,
  impact and boss-music handoff, body movement/stomps/projectile attack,
  exposed grabbable head, ordinary-sword and thrown-head ghost damage,
  persistent eight-point ghost health, Bracelet weight-0 throw, regeneration
  at the landed head, source body/ghost/head palettes, death phase, and Heart
  Container reward. Both encounters include the source-scaled,
  alternating boss shadow; the common 120-update death flicker; the
  78-update enemy-counting large explosion; Link-collision/menu lock; and
  Spirit's Grave music restoration before their ordered rewards. Room `4:11`
  implements the Eternal Spirit's
  approach, fall and two-hand collection, separate source essence/pedestal/
  parameter-flickering glow OAM, imported TX `$000e`, `MUS_GET_ESSENCE` text
  music, `MUS_ESSENCE` energy-bead sequence, room/essence flags, fade cues, and
  delayed white exit warp to `0:8d`.

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
- Past Lynna shop room `2:5e`, including source-ordered `$47` liftable stock,
  `$46:$00` counter collision and talk behavior, exact BG price digits,
  A/B pickup and shelf return, welcome/purchase/denial/item dialogue, Rupee and
  treasure transactions, and the shopkeeper's `SPEED_200` theft-prevention
  route. Stock follows the original Bomb, Strange Flute, linked Gasha Seed,
  bought-item, and shield-level predicates; invisible `$71:$0c` also promotes
  Dimitri state bit `$20` to `$40` on entry.
- Vasu Jewelers room `2:ee` instantiates Vasu, Blue Snake, Red Snake, and both
  help books in source order with imported sprites, palettes, OAM animations,
  bottom text, collision, and snake `$18` proximity behavior. Both books and
  the unlinked Red Snake tutorial are complete. Blue Snake supports the
  fortune setup and original 512-update no-cable failure, while linked-secret
  and transfer choices stop at their explicit Game Link boundary. Vasu
  supports his ordinary and linked introductions, Ring Box/Friendship Ring
  held-item grants, special-ring predicate priority/rewards, paid and free
  appraisal, duplicate refunds, ring-list/Ring Box management, Quit, empty-list
  responses, and the forced first appraisal/list sequence. Completion flag
  `$08` and `wObtainedRingBox` bit `$01` are committed only after the source
  menu handoffs complete.
- Keese, Octoroks/projectiles, masked Moblins `$20:$00` and their arrows,
  ordinary Stalfos `$31:$00`, Zols, and Gels using
  ordered room-object placement, original spawn restrictions, shared RNG,
  combat, common/split kill sounds, common hazard effects, and drop paths. All 34
  ordinary-Stalfos records (37 instances) use their source SPEED_80 walk,
  two-call direction/counter decision, wall/hole bounce, animation, damage,
  health, and drop path. Evasive, bone-throwing, and stomping Stalfos subids
  remain deferred.
- Graveyard rooms `0:5d` and `0:6d` include all three fixed `$41:$00` Crows,
  with their perched facing, inclusive Link trigger rectangle, six-pixel rise,
  one-call randomized charge, steering, contact damage, and silent off-screen
  deletion. Their `$fa` placements are invisible tile-change producers: the
  room `0:5d` watcher reveals Bombs and the two room `0:6d` watchers reveal
  Ember Seeds only after Link owns the corresponding treasure. Room `0:7d`
  retains its intentionally empty object stream.
- Room `0:5d` also includes the linked-game Ghini after D1. Its solid/talkable
  palette-2 actor, all five TX `$4d05-$4d09` choice branches, repeated
  explanation/secret prompts, began flag, and original five-symbol Graveyard
  secret packing, checksum, cipher, and text substitution are implemented.
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
- Shared dungeon-entry interactions are imported across all direct source
  placements: 14 whiteout-only `$12:$00` dungeon handlers, 12 `$e2:$01`
  statue-eye scanners, and 16 `$7e:$00` miniboss portals. Spirit's Grave room
  `4:24` initializes TX `$0201` and its dungeon session bytes, creates all six
  `$ee`-tile eyes in source order with the fixed animation `$04` OAM moved by
  the exact eight-direction low-nibble offsets, and
  gates its portal on room `4:18` flag `$80`. An enabled portal rejects initial
  overlap, then uses `SND_TELEPORT`, pins and spins Link for `$30` updates, and
  performs the source fadeout warp between `4:24` and `4:18` at position `$57`.
- Reusable small-key door tiles `$70-$73`, including current-dungeon key
  consumption, TX `$5100`, paired dungeon-layout room flags, the 10-update push
  threshold, key sprite, and six-update mapping-interleaved opening. Room
  `4:0a` exercises the left-facing `$73` path and persistent re-entry tile
  substitution.
- Reusable dark-room handler `$08:$00` and permanent lightable torches
  `$06:$00` in both source placements, rooms `5:a8` and `5:ed`. The handler
  scans the complete 176-byte large-room layout, creates torches in packed
  address order, accepts Ember Seed collisions, changes metatiles `$08->$09`,
  and reproduces the BG-2-through-BG-7 partial/full component-offset fades.
  Room `5:ed` also preserves the source-ordered `$dc:$00` consumer: exactly two
  lit torches create the falling, twice-bouncing Graveyard Key with its
  one-hand pickup, sounds, text, treasure grant, and room-item-bit `$20`
  suppression on re-entry. Other bits in the room byte do not suppress it.

### Story and events

- Early-game story/cutscene paths for Impa, the Triforce stone, Ralph's portal,
  first arrival in the past, the Maku Tree disappearance, and Nayru's
  introduction/aftermath. Script-driven portions use the typed command runner;
  native transition/presentation objects retain specialized controllers.
- Present room `0:7b`'s complete one-time Spirit's Grave children scene. The
  three placed `$3c:$03`, `$3c:$04`, and `$3f:$02` interactions retain their
  source object order, red/green/blue visuals, two delayed jumps, five dialogue
  boxes, shared `cfd1` handoffs, 120-update fear shake with 360 shared-RNG
  calls, three throw cues, and simultaneous `SPEED_200` escapes. Current-room
  bit `$40` suppresses all three children and the event on re-entry. The
  unrelated `$b6:$03` Gasha spot follows those actors in the same source object
  order and uses the complete shared planting/growth/reward system.
- Present room `0:5c`'s Graveyard Key gate, including the source doubled
  20-count push test (10 updates), retained named key, one-time missing-key TX
  `$5109`, rising `$18` key visual, room bit `$80`, and the typed
  60/45/60-update opening sequence. The two native phases preserve their
  interleaved tile writes, puff positions and sounds, two-axis shared-RNG
  shaking, music stop/restore, solve cue, input lock, and completed re-entry
  layout. The unrelated `$71:$05` east-boundary interaction applies only while
  riding a companion and remains dormant until mounted companion actors exist.
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
  death checkpoints, packed-BCD death count, explicit Save & Quit flows, and
  the `gfx_gameover`/`PALH_06` Continue, Save and Continue, and Save and Quit
  lifecycle.
- Top-screen HUD (including simultaneous rupee digits and the dungeon-only
  `gfx_key`/X/key-count field), dialogue, inventory pages, map/dungeon map,
  Vasu's appraisal/ring-list interface, live flag/item/ring editor, and shared
  fixed-update menu lifecycle.

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

- Remaining active items and grabbable object species (including Bombs and
  companions), swimming/diving, terrain-specific Link states, and complete
  low-health warning behavior.
- Satchel selection and the active Scent, Pegasus, Gale, and Mystery Seed
  state machines remain deferred; the first acquired Satchel's Ember path is
  implemented and unsupported selected child IDs report a source-aware error
  without consuming ammo.
- Shovel drop `$0f` consumes its third RNG value and supports the 100-Rupee
  branch; its rope/beetle branches remain suppressed until those enemies are
  implemented.
- Secret entry and actual Game Link transport/linked ring-secret menus remain
  unavailable. Room `2:ee` retains the source dialogue, predicates, no-cable
  result, and non-serial rewards around that explicit external boundary.
- The ring-effect coverage table above explicitly identifies every deferred or
  partial effect. Add each missing consumer when its base subsystem is
  implemented; do not create a second ring-effect table.

### Graphics and audio

- Remaining dynamic inventory count overlays outside the implemented selected
  Satchel seed quantity and specialized item OAM.
- Sound calls owned by interactions and objects that have not yet been ported.
- Shield responses owned by enemy-body collisions and projectile species that
  are not yet present in production remain deferred. The implemented rock and
  arrow paths are explicitly covered above and by headless regression.
- A possible cell-based room renderer; the current full-room texture path remains
  authoritative until a staged migration proves parity.

See [TODO.md](../TODO.md) for engineering consolidation tasks. New gameplay work
should be selected from traced original interactions and should include imported
data, runtime behavior, and validation in the same slice.

The [NPC and room-event implementation guide](npcs-and-events.md) documents the
current source-to-importer-to-runtime workflow used to expand that coverage.
