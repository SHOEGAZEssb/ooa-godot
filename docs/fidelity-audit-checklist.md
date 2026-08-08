# Fidelity audit checklist

Snapshot: August 1, 2026, repository commit
`b90a9bbd0aeb2d2c57f5c53e72ba1c84e3e2ae74` on `main`.

This is a source-and-ROM audit of the currently implemented production
surface. It is a checklist of evidence and remaining work, not a claim that a
passing headless scenario makes adjacent or later-game content complete. The
room-by-room NPC classification remains in
[NPC interaction coverage](npc-interaction-coverage.md); this document links to
that ledger instead of duplicating its 211-room inventory.

## How to read this checklist

- `[x]` means the named implemented contract was inspected against its
  disassembly inputs and focused validation, and this pass found no specific
  contradiction inside that contract.
- `[ ]` means an open fidelity item. `CONFIRMED` is a demonstrated runtime/source
  divergence, `DECLARED` is intentionally absent or partial functionality, and
  `VERIFY` is implemented behavior that still lacks enough retail-ROM evidence.
- Priorities are `P0` (blocking/corrupting), `P1` (core state, progression, or
  determinism), `P2` (player-visible behavior), and `P3` (presentation or
  verification quality). This pass found no new `P0` issue.
- A checked item is deliberately narrow. For example, checked Octorok behavior
  says nothing about an unsupported Lynel handler.

## Audit basis and reproducibility

- [x] Read and applied [Project principles](project-principles.md), the
  [documentation index](README.md), and the relevant runtime, import, world,
  NPC, menu, save, graphics/audio, and validation guides before classifying
  findings.
- [x] Inventoried 509 production C# files, 17 importer stages, 55 validation C#
  files, and all 137 scenarios registered by
  `ValidationRoot.ValidateAll`.
- [x] Used the clean US ROM
  `Legend of Zelda, The - Oracle of Ages (U) [C][!].gbc`, MD5
  `C4639CC61C049E5A085526BB6CAC03BB`, SHA-256
  `0B56B78A9E45452E98C33EDD111234931F1E034DC097F6F23082EB8DB6055474`.
- [x] Used `oracles-disasm` commit
  `aa4cb96df22a7bd452db090dd0b32eccebe42cf3` and recorded the pre-existing
  disassembly worktree changes rather than modifying them.
- [x] Ran `verify_oracle_import.ps1`. It validated the clean-ROM hash, parsed
  384 assembly sources, ran the importer twice, and reproduced 2,591 generated
  assets with manifest SHA-256
  `9e6a0ffe67de3a5679efa562aa7a3e7fcb317745c4ad299592011485b69c6ac0`.
- [x] Built the .NET solution with zero warnings and zero errors.
- [x] Ran the full Godot headless suite; all 137 registered scenarios passed.
- [x] Booted the clean ROM in PyBoy 2.7.0 with sound emulation enabled, followed
  the retail Capcom/cinematic/title/file/name/message-speed/new-game path into
  room `$0:$8a`, and loaded a checksum-valid edited save into room `$0:$48` to
  check the retail room/spawn boundary.
- [x] Traced retail title RNG directly at `hRng1=$ff94` and `hRng2=$ff95`; after
  title initialization, every title update advanced the pair exactly once.
- [x] Reconciled room `$4:$09` against the clean ROM. The upstream
  `dumpRoomLayouts.py` dictionary decompressor produced 176 bytes with `$a0`
  at offset `$79` and SHA-256
  `37C69848A1CEA8DA5106E74EBC581934EF622F847C9A21914486EBCE70FEDABE`.
  That payload matches disassembly `HEAD`; the external source and generated
  runtime asset now use it instead of the non-retail `$3e` edit.
  Determinism is proven; retail provenance of that byte is not.
- [ ] **P2 VERIFY - add a repeatable retail-ROM differential harness.** Current
  validations compare imported values and traced state machines to source
  contracts, but they do not execute the retail ROM beside Godot and compare
  update-by-update WRAM/object traces. The targeted checks in this audit do not
  replace end-to-end differentials for both completed dungeons.

## Confirmed fidelity gaps

| Priority | Open checklist item | Evidence and impact |
| --- | --- | --- |
| P1 | [ ] **CONFIRMED - complete Mermaid Suit movement and underwater transitions.** | Production now imports and ports the Flippers branch of `link.s:linkUpdateSwimming` plus normal-water `linkUpdateDiving`: `$0a`-update entry lock, shared `func_5933` inertia, 8/13/12 A-button burst, B-toggle diving with the `$78` timer and Zora Ring exception, and exact swim/dive animation data. Mermaid Suit movement, SeaWater handling, and deep-water transitions remain unsupported, so SeaWater still takes the safe drowning path. |
| P2 | [ ] **CONFIRMED - create and render side-view swim bubbles.** | On bubble-counter underflow, `link.s:linkUpdateSwimming_sidescroll` consumes RNG and creates `INTERAC_BUBBLE $91`. `Player.AdvanceSideScrollSwimming` deliberately consumes the same shared RNG call but has no bubble entity/spawn. RNG order is retained, but the visible interaction is absent. |

## Runtime foundation, ownership, and data boundaries

- [x] Application-owned 60-update scheduling, host-frame input buffering,
  original-update edge consumption, and isolation reset are covered by
  `ValidateApplicationFixedUpdateScheduler`, `ValidateGameplaySceneGraph`, and
  `ValidateMenuLifecycleFoundation` and agree with the documented ownership
  boundary.
- [x] Generated-table schema checks, unique/ordered-key semantics, source-aware
  exceptions, manifest hashing, graphics caching, and cutscene command
  default-deny behavior are present and covered by
  `ValidateGeneratedTableReader`, `ValidateGraphicsCache`,
  `ValidateCutsceneCommandSchema`, and `ValidateCutsceneDefaultDeny`.
- [x] Shared object speed, relative-angle, unsigned 8.8 position, Z/gravity,
  global RNG algorithm, and 256-byte enemy-placement permutation match the
  imported `objectSpeedTable`, `pushDirectionData`, `getRandomNumber`, and
  `generateRandomBuffer` contracts.
- [x] No production use of `System.Random`, `Random.Shared`, or Godot random
  functions was found; implemented gameplay randomness routes through the
  single `OracleRandom` owner.
- [x] Ordinary grounded and top-down-airborne Link movement maps digital input
  to the original angle and terrain speed bytes before applying the imported
  signed 8.8 vector. `ValidateLinkTopDownMovement` covers long cardinal,
  diagonal, grass, per-axis collision, retained-fraction, and rendered-high-byte
  paths through the actual player input caller.

## Frontend, dialogue, menus, and persistence

- [x] The clean-US Capcom screen; horse, castle, Triforce, and pre-title-tree
  cinematic; Start skip gate; title idle replay; source sound order; seven
  ordered bird RNG calls; one RNG call per title dispatch; source OAM,
  palettes, dynamic-GFX offsets, fixed-point actor motion, and scanline effects
  are covered by `ValidateFrontendIntro`. Title/file-select layout and behavior
  remain covered by `ValidateMenuPresentationData` and `ValidateMainMenu`.
- [x] The pregame Link/Triforce/Nayru arrival presentation, imported actor OAM,
  dialogue, music, fades/waves, global flags, and room `$0:$8a` arrival are
  covered by `ValidateNewGameIntro`; the clean-ROM smoke trace reached the same
  initial group/room.
- [x] Dialogue implements two 8x16 rows, source text colors, fixed/automatic
  textbox positions, scroll/continue states, message speeds 7/5/4/3/2,
  choices, character and one-shot sounds, trade-item glyphs, `\stop`,
  `\heartpiece`, `\slow()`, adjacent `\heart` and `\xHH` byte controls,
  symbol/button controls, and background-palette ownership.
- [x] `ValidateSigns` scans every base64-decodable generated TSV cell for text
  controls and locks the exact remaining owner-expanded `\call`, `\Child`,
  `\secret1`, and `\num1` inventory. Gasha's reachable `\jump(TX_3502)` is
  flattened by its importer; raw map-bank and deliberately unsupported NPC
  records remain classified, and `DialogueBox` now rejects any unresolved
  command that leaks through an owning runtime path instead of rendering it.
- [x] Inventory open/close, item and seed submenus, map and dungeon-map
  presentation, ring appraisal/list/box/equip, save/quit, game-over Continue,
  and explicit persistence boundaries are covered by the menu and persistence
  scenario group.
- [x] The `$550` save image, verify string, word checksum, initial values,
  treasure variables, BCD counters, room/global flags, dungeon collectibles,
  checkpoints, zero-health restore, explicit save, atomic primary/backup host
  storage, copy, and erase paths were traced and validated.
- [ ] **P1 DECLARED - implement Secret entry and real Game Link transport.**
  New-file Secret and Game Link choices currently show deterministic notices;
  Vasu's linked transfer path ends at the explicitly deferred external link
  boundary. This also leaves linked secret/file initialization unverified as a
  complete frontend flow.

## Audio and graphics

- [x] The import contains all 223 sound pointers used by the supported source,
  room-music assignments, eight logical channels, priorities, stop controls,
  square/wave/noise frequencies, envelopes, pitch slide/shift, vibrato,
  waveform selection, fades, CGB high-pass filtering, and teardown. Focused
  channel-state tests cover representative title, menu, Link, sword, and Maku
  requests.
- [x] Implemented room, sprite, OAM, animation, menu, palette, transition, and
  effect owners use generated assets and the shared immutable graphics cache;
  no production reads of `oracles-disasm` were found.
- [x] Source OAM details checked by existing pixel/hash assertions include
  signed/wrapped positions, flips, priorities, palette overrides, animation
  parameters, multi-cell composition, menu layouts, transformed Link, sword
  beams, dungeon objects, NPCs, bosses, and cutscene actors.
- [ ] **P3 VERIFY - add rendered/audio retail differentials, not only decoded
  source assertions.** Current tests strongly cover tables and representative
  pixels/channel state but do not compare complete frames or PCM/APU-register
  traces against the clean ROM over full playable routes.
- [ ] **P2 DECLARED - import and dispatch graphics/sound for the remaining
  gameplay objects when their behavior owners are implemented.** Current
  suppression is intentional and source-aware; it is still incomplete retail
  coverage.

## Rooms, terrain, transitions, and world state

- [x] All 1,536 room layouts and 103 non-stub tilesets are imported, including
  palettes, metatiles, collision, animation groups, large-room stride/padding,
  world maps, 529 warps, 42 signs, 133 chests, and dungeon floor layouts.
- [x] Implemented room loading, standard/single tile changes, animated terrain,
  world-space collision, cave/house/tile/time warps, small and large-room
  transitions, dungeon layout neighbors, destination preload/freeze, outgoing
  retention, HUD/screen-space separation, and one-frame Link scroll behavior
  are covered by the world/transition scenarios.
- [x] Implemented hazards cover holes, no-Flippers/SeaWater drowning, top-down
  Flippers swimming and normal-water diving, side-view pits/spikes, pulling,
  damage/recovery, last-safe-position respawn, ledges, grass/puddle/stair/vine
  speed selection, quicksand/bridge push, slippery side-view tiles, and
  terrain-linked drops/effects.
- [ ] **P1 DECLARED - complete terrain-specific Link state dispatch.** Besides
  the confirmed Mermaid Suit movement/underwater-transition gap, later terrain
  states remain outside the current production state machine.
- [x] Room `$4:$09` now matches clean-ROM dictionary decompression, including
  `$a0` at `wRoomLayout+$79`; the dependent dungeon regression locks that byte.
- [ ] **P1 DECLARED - implement world/event/door/controller families required
  beyond the currently supported story slices and first two dungeons.** The
  complete room pictures are available, but many placed objects are
  deliberately suppressed and therefore rooms are not equivalent merely
  because their tiles render.

## Link, items, combat, and drops

- [x] Inspected and retained source-backed behavior for sword level damage,
  slash/poke/charge/spin timing, clinks, sword beams, bush hits, airborne draw
  order, shield parent/display/collision, enemy knockback/invincibility/death,
  health/potion/game-over, and implemented ring modifiers.
- [x] Bomb placement/allocation, fuse, carrying/throwing, explosion, self-hit,
  wall and enemy damage, Bomb/Peace/Blast/Toss/Bombproof ring behavior, and
  sound/effect ordering are covered by focused validations.
- [x] Bracelet pickup, lift/hold/walk/throw, push gates, heavy/full-item denial,
  ordinary supported grabbables, thrown collisions, and strong-throw policy
  are source-traced and covered.
- [x] Shovel timing and tile effects; level-1 Roc's Feather top-down and
  side-view jump foundations; top-down Flippers swimming/normal diving; Harp
  song/submenu/time-portal foundations; Ember and Mystery Satchel projectiles;
  seed submenu; Owl/Mystery behavior; chests, ground treasures, drop producers,
  inventory-dependent drops, BCD rupee countdown, water splashes, and
  Gasha/Seed Tree behavior have focused source regressions.
- [ ] **P1 DECLARED - implement the remaining usable Ages item parents:** Cane
  of Somaria `$04`, Boomerang `$06`, Switch Hook/helper/chain `$09-$0b`,
  Biggoron Sword `$0c`, Bombchus `$0d`, companion Flute `$0e`, and Seed Shooter
  `$0f`. Inventory/treasure storage for some of these exists, but the player
  input dispatcher has no production action path.
- [ ] **P1 DECLARED - implement Scent `$21`, Pegasus `$22`, and Gale `$23` seed
  children.** The Satchel database accepts only Ember `$20` and Mystery `$24`
  and rejects unsupported children before consuming inventory. This is safe,
  not complete.
- [ ] **P1 DECLARED - complete level-2 Roc's Feather/Roc's Cape and all remaining
  liftable/grabbable species.** Current side-view Cape code is a bounded
  foundation, not full game coverage.
- [ ] **P2 VERIFY - add direct side-view Flippers, Mermaid Suit, swim-burst,
  Roc's Cape, ice, squish, and boundary regressions.** The source constants and
  state code were manually compared with `linkUpdateSwimming_sidescroll`,
  `linkState01_sidescroll`, `linkUpdateInAir_sidescroll`, and
  `parentItemCode_feather`, but the registered suite does not directly name or
  exercise the complete swim/Cape state paths.

## Rings

- [x] All 64 ring IDs, names/descriptions, appraisal, duplicate refund, box
  layouts, list scrolling, equip persistence, damage arithmetic, source
  protections, punches, transformations, sword beams, drops, Maple threshold,
  Gasha credits, and the ring policies used by currently implemented systems
  are covered by `ValidateRingFunctionality` and their consumer scenarios.
- [ ] **P1 DECLARED - connect policy-only rings when their base systems exist.**
  `BoomerangDamage`, `PegasusSeedTimerDecrement`, `ProtectsCrackedFloor`,
  `IgnoresQuicksand`, `PreventsJinx`, and `RemovesDiveTimer` have no production
  consumer. Related Holy/Luck protections also cannot activate for absent Zora
  fire, electric, beam, or blade-trap producers. Passing policy arithmetic is
  not player-visible implementation.
- [ ] **P2 VERIFY - run each active ring through its real retail producer and
  consumer.** The current broad ring test directly proves many policies, but a
  policy-only assertion cannot prove object update order, RNG consumption, or
  interaction with unimplemented species.

## Implemented enemies and bosses

- [x] The ordered handler registry explicitly covers 28 implemented ID/subid
  rows and 90 deliberately unsupported rows; placement preserves source object
  order, slot reservations, shared placement-buffer consumption, fixed/random
  spawn rules, and unsupported reservations.
- [x] Source-traced common combat covers collision modes used by the supported
  roster, sword/projectile hits, damage blink, recoil, hazards, death puffs,
  kill/drop counters, item drops, and hostile-projectile lifecycle.
- [x] Focused implementations inspected: Octorok `$09:$00-$02`, Boomerang
  Moblin `$0a:$00`, Arrow Moblin `$0c:$00`, Rope `$10:$00`, Spark `$13:$00`,
  Spiked Beetle `$14:$00`, Ghini `$17:$00`, Whisp `$19:$00`, Spiny Beetle
  `$1b:$01`, Masked Moblin `$20:$00/$01`, Arrow Shrouded Stalfos `$22:$00`,
  Wallmaster `$28:$00`, Thwomp `$2f:$00`, Stalfos `$31:$00`, Keese
  `$32:$00/$01`, Zol `$34:$00/$01`, Peahat `$3e:$00`, Crow `$41:$00`, Gel
  `$43:$00`, Color-changing Gel `$47:$00`, Sword Shrouded Stalfos `$49:$00`,
  Sword Masked Moblin `$4a:$01`, and Hardhat Beetle `$4d:$00`.
- [x] Implemented dungeon bosses/minibosses Giant Ghini, Pumpkin Head, Swoop,
  and Head Thwomp have focused state/counter/RNG/projectile/reward/room-lock
  coverage, with especially detailed Head Thwomp fidelity assertions.
- [ ] **P2 VERIFY - add retail object-slot traces for every implemented species.**
  The source validations are detailed, but no automated ROM trace currently
  compares the complete object struct and shared RNG after representative
  fights.

### Deliberately unsupported enemy registry rows

These entries are safely reserved/suppressed today. Every box remains open
until the corresponding native handler and its used subids are source-traced,
implemented, and covered.

- [ ] `ENEMY_RIVER_ZORA` `$08:$00`
- [ ] `ENEMY_LEEVER` `$0b:$00/$01`
- [ ] `ENEMY_ARROW_MOBLIN` `$0c:$01`
- [ ] `ENEMY_LYNEL` `$0d:$00/$01`
- [ ] `ENEMY_BLADE_TRAP` `$0e:$00/$01/$05`
- [ ] `ENEMY_ROPE` `$10:$01`
- [ ] `ENEMY_GIBDO` `$12:$00`
- [ ] `ENEMY_BUBBLE` `$15:$00`
- [ ] `ENEMY_BEAMOS` `$16:$00`
- [ ] `ENEMY_GHINI` `$17:$01/$02`
- [ ] `ENEMY_BUZZBLOB` `$18:$00`
- [ ] `ENEMY_SAND_CRAB` `$1a:$00`
- [ ] `ENEMY_SPINY_BEETLE` `$1b:$00/$03`
- [ ] `ENEMY_IRON_MASK` `$1c:$00`
- [ ] `ENEMY_ARROW_DARKNUT` `$21:$00/$01`
- [ ] `ENEMY_ARROW_SHROUDED_STALFOS` `$22:$01`
- [ ] `ENEMY_POLS_VOICE` `$23:$00`
- [ ] `ENEMY_LIKE_LIKE` `$24:$00`
- [ ] `ENEMY_GOPONGA_FLOWER` `$25:$00`
- [ ] `ENEMY_GIANT_BLADE_TRAP` `$2a:$03`
- [ ] `ENEMY_CHEEP_CHEEP` `$2c:$00/$01`
- [ ] `ENEMY_PODOBOO_TOWER` `$2d:$00`
- [ ] `ENEMY_TEKTITE` `$30:$00/$01/$02`
- [ ] `ENEMY_STALFOS` `$31:$02`
- [ ] `ENEMY_BABY_CUCCO` `$33:$00`
- [ ] `ENEMY_FLOORMASTER` `$35:$00`
- [ ] `ENEMY_CUCCO` `$36:$00`
- [ ] `ENEMY_GREAT_FAIRY` `$38:$00`
- [ ] `ENEMY_FIRE_KEESE` `$39:$00`
- [ ] `ENEMY_WATER_TEKTITE` `$3a:$00`
- [ ] `ENEMY_BARI` `$3c:$00/$01`
- [ ] `ENEMY_SWORD_MOBLIN` `$3d:$00/$01`
- [ ] `ENEMY_WIZZROBE` `$40:$00/$01/$02`
- [ ] `ENEMY_CROW` `$41:$01`
- [ ] `ENEMY_PINCER` `$45:$00`
- [ ] `ENEMY_SWORD_DARKNUT` `$48:$00/$01`
- [ ] `ENEMY_SWORD_SHROUDED_STALFOS` `$49:$01`
- [ ] `ENEMY_SWORD_MASKED_MOBLIN` `$4a:$00`
- [ ] `ENEMY_BALL_AND_CHAIN_SOLDIER` `$4b:$00`
- [ ] `ENEMY_ARM_MIMIC` `$4e:$00`
- [ ] `ENEMY_MOLDORM` `$4f:$00`
- [ ] `ENEMY_FIREBALL_SHOOTER` `$50:$00/$01`
- [ ] `ENEMY_FLYING_TILE` `$52:$00/$02`
- [ ] `ENEMY_AMBI_GUARD` `$54:$02-$08/$0a-$0c/$82-$8c`
- [ ] `ENEMY_CANDLE` `$55:$00`
- [ ] `ENEMY_TARGET_CART_CRYSTAL` `$63:$05-$0b`
- [ ] `ENEMY_KING_MOBLIN` `$7f:$00`

## NPCs, shops, minigames, and room interactions

- [x] The typed NPC database classifies all 383 positioned/state-derived rows:
  137 implemented, 30 partial, and 216 deliberately unsupported. The manifest
  contains 61 ordinary, 84 specialized, 22 event-owned, and 216 deliberately
  unsupported rows. The 72 conditional Bipin/Blossom family variants bring the
  typed total to 455.
- [x] Implemented ordinary NPC routing, source positions/facing/collision/OAM,
  dialogue predicates, visibility predicates, room retention, and unsupported
  suppression are covered by the NPC manifest and room scenarios.
- [x] Focused implemented interaction slices include selected Lynna villagers,
  soldiers, old ladies, stone rabbits, postman, Toilet Hand, Poe, comedian,
  Mask Salesman, depressed-boy Funny Joke trade, Troy, Shooting Gallery,
  Hardhat shovel trade, Lynna shop,
  Business Scrub, Vasu/ring shop, lower Black Tower workers/soldiers, linked
  Graveyard Ghini, Cheval/post-Cheval Ralph/Rafton, post-Rafton Ralph including
  its conditional `roomSpecificCode7` theme, Maple,
  and representative
  Bipin/Blossom/child states.
- [ ] **P1 DECLARED - complete all 36 partial positioned/state-derived records
  and all 72 partial family variants.** See the exact room rows and boundaries
  in [NPC interaction coverage](npc-interaction-coverage.md).
- [ ] **P1 DECLARED - implement the 259 deliberately unsupported positioned/
  state-derived rows.** Highest-value clusters are Tokay; Gorons and Elders;
  Zora, King Zora, Old Zora, and Jabu child; remaining soldiers; Symmetry City;
  carpenters; Mamamu/dog; Tingle; Bomb Upgrade Fairy; Syrup;
  remaining shopkeepers; and old-lady linked-secret variants.
- [ ] **P2 VERIFY - replace representative family/NPC coverage with per-variant
  retail traces where state selection has side effects.** In particular,
  `NpcDatabase.GetRoomNpcs` currently owns both family selection and progression
  mutation, a boundary that deserves explicit ROM-state comparison.

## Story and cutscenes

- [x] The typed command runner preserves source order, exact waits, native-yield
  cadence, parallel lanes, script calls/returns, actor lookup diagnostics,
  default-deny capabilities, and lifecycle cancellation for the command sets it
  accepts.
- [x] Source-traced story slices with focused scenarios include the pregame
  intro; Impa rock encounter; time portals and first past entry; Ralph portal
  departure; Graveyard gate and ghost children; Maku disappearance, rescue,
  saved/advice, and first/second Essence responses; Nayru intro; Fairies' Woods;
  Deku Forest soldier/palace; pre/lower Black Tower; Harp acquisition and remote
  Maku Harp sequence; trade-sequence events; and Wing Dungeon collapse.
- [ ] **P1 DECLARED - implement the full story graph outside those bounded
  slices.** Current imported rooms and text do not imply event ownership,
  persistent-state transitions, companions, ending, credits, or linked-game
  cutscenes.
- [ ] **P2 VERIFY - run long cutscenes against retail frame/object/RNG traces.**
  Current validations assert many exact counters and visual snapshots but are
  source simulations rather than emulator differentials.

## Dungeons and puzzles

- [x] Spirit's Grave and Wing Dungeon are covered end to end by headless route
  scenarios, including entrances, key/boss doors, shutters, push blocks,
  chests, moving platforms, side-view rooms, minecarts/gates, colored cube,
  floor-color puzzle, dark rooms, minibosses, bosses, rewards, Essences, and
  persistent re-entry substitutions.
- [x] Shared dungeon mechanisms retain imported mapping/placement ownership
  rather than room-ID arithmetic or dungeon-local clones.
- [ ] **P1 VERIFY - execute retail differential routes for both completed
  dungeons.** The current full suite is excellent source-level coverage, but
  this audit did not manually play both retail routes from entrance to Essence
  while comparing room flags, inventory, RNG, and object state.
- [ ] **P1 DECLARED - implement dungeons `$03-$08`, remaining bosses/minibosses,
  Hero's Cave, and their shared/new mechanism variants.** Their layout/map data
  may render, but they are not current playable-fidelity claims.

## Registered validation coverage reviewed in this pass

All entries below ran successfully. They are grouped for navigation; each name
is the production contract audited by that scenario, not a claim of whole-game
coverage.

- [x] **Foundation/data/audio (18):** `ValidateGameplaySceneGraph`,
  `ValidateApplicationFixedUpdateScheduler`, `ValidateGeneratedTableReader`,
  `ValidateMenuLifecycleFoundation`, `ValidateRepresentativeRooms`,
  `ValidateOracleObjectMath`, `ValidateOracleRandom`,
  `ValidateRoomEventTimeline`, `ValidateCutsceneCommandSchema`,
  `ValidateCutsceneDefaultDeny`, `ValidateRoomTileChanges`,
  `ValidateSoundEngine`, `ValidateGraphicsCache`,
  `ValidateBackgroundPaletteState`, `ValidateObjectSpeedTable`,
  `ValidateEnemyBehaviorTables`, `ValidateEnemyPlacementRules`, and
  `ValidateEnemyObjectPlacementOrder`.
- [x] **Persistence/frontend/development (17):** `ValidateSaveDataFoundation`,
  `ValidateSaveStore`, `ValidateTreasureInterpreter`,
  `ValidateDungeonCollectibles`, `ValidateExplicitSavePersistence`,
  `ValidateMenuPresentationData`, `ValidateFrontendIntro`, `ValidateMainMenu`,
  `ValidateNewGameIntro`, `ValidateDebugFlagMenu`,
  `ValidateDebugCollision`, `ValidateDebugRoomWarp`,
  `ValidateDebugMapleShortcut`, `ValidateDebugSavestates`,
  `ValidateDeathRespawnCheckpoints`, `ValidateGameOverRestart`, and
  `ValidateSaveAndQuitToTitle`.
- [x] **NPC/interaction coverage (34):** `ValidateSigns`,
  `ValidateNpcImplementationManifest`, `ValidateNpcs`,
  `ValidateRooms171And181`, `ValidateRoom173SoldierPair`,
  `ValidateRoom174PastOldLady`, `ValidateRooms182And192NpcInteractions`,
  `ValidateRoom183MiscManAndDrops`,
  `ValidateRoom184StoneRabbitsAndSoldier`,
  `ValidateRooms193And194NpcInteractions`, `ValidateRoom22fPostman`,
  `ValidateRoom23eToiletHand`, `ValidateRoom2e9ShootingGallery`,
  `ValidateRoom39eInteractions`, `ValidateRoom3aeInteractions`,
  `ValidateRoom20eNpcInteractions`, `ValidateTroyHouseRooms`,
  `ValidateRooms145And3fcNpcInteractions`,
  `ValidateRoom148NpcInteractions`, `ValidateRoom149FamilyInteractions`,
  `ValidateRoom157NpcInteractions`, `ValidateRoom158NpcInteractions`,
  `ValidateRoom175NpcInteractions`, `ValidateRoom176NpcInteractions`,
  `ValidateRoom186NpcInteractions`, `ValidateLowerBlackTowerInteractions`,
  `ValidateNpcFlagVisibility`, `ValidateBipinBlossomNaming`,
  `ValidateLynnaShopInteractions`, `ValidateVasuShopInteractions`,
  `ValidateRoom083Interactions`, `ValidateRoom180OwlStatue`,
  `ValidateGashaSpots`, and `ValidateSeedTrees`.
- [x] **Story/cutscenes (21):** `ValidateDekuForestSoldierCutscene`,
  `ValidateDekuForestPalaceCutscene`,
  `ValidateGraveyardGhostKidsCutscene`, `ValidateImpaIntroEncounter`,
  `ValidateMakuTreeDisappearanceCutscene`,
  `ValidateMakuSproutRescueCutscene`, `ValidateRoom06cMooshRescue`,
  `ValidateMakuTreeSavedCutscene`,
  `ValidateRoom056Comedian`, `ValidateRoom07cPoe`,
  `ValidateRoom22ePoe`, `ValidateRoom2e6MaskSalesman`,
  `ValidateNayruIntroCutscene`, `ValidateRalphPortalDepartureEvent`,
  `ValidateTimePortals`, `ValidateEnterPastEvent`,
  `ValidateOverworldKeyholeAndGraveyardGate`,
  `ValidateRemoteMakuFirstEssenceCutscene`,
  `ValidateRemoteMakuSecondEssenceCutscene`,
  `ValidateRemoteMakuHarpCutscene`, and `ValidateFairiesWoodsSequence`.
- [x] **Link/items/menus (20):** `ValidateAnimations`,
  `ValidateLinkItemGeneratedData`, `ValidateSwordBush`,
  `ValidateAirborneSwordRendering`, `ValidateShield`, `ValidateShovel`,
  `ValidateBombs`, `ValidateSeedSatchel`, `ValidateHarp`,
  `ValidateLinkTopDownMovement`, `ValidateLinkTerrainEffects`, `ValidateHealth`,
  `ValidatePlayerDamageAndDeath`, `ValidateInventoryFoundation`,
  `ValidateInventoryMenu`, `ValidateRingFunctionality`,
  `ValidateBraceletChestAndPushGate`, `ValidateChests`,
  `ValidatePushBlocks`, and `ValidateMapScreen`.
- [x] **Enemies/drops/terrain/world (22):**
  `ValidateHardhatAndSpinyBeetles`, `ValidateSpikedBeetles`,
  `ValidateKeese`, `ValidatePeahat`,
  `ValidateGraveyardCrowsAndDropProducers`, `ValidateOctoroks`,
  `ValidateArrowMoblins`, `ValidateHostileProjectileLifecycle`,
  `ValidateEnemySwordKnockback`, `ValidateEnemyDamageBlink`,
  `ValidateEnemyHazards`, `ValidateStalfos`, `ValidateZolsAndGels`,
  `ValidateItemDrops`, `ValidateHouseWarp`, `ValidateCaveWarps`,
  `ValidateMakuTreeSouthExitReveal`, `ValidateTerrain`,
  `ValidateStartupTransitionFromRoom011`,
  `ValidateSymmetryTransitionFromRoom022`, `ValidateDarkRoomInteractions`, and
  `ValidateMapleEvents`.
- [x] **Dungeon routes (6):** `ValidateDungeonMechanics`,
  `ValidateSpiritsGraveEntranceInteractions`, `ValidateDungeonKeyDoors`,
  `ValidateSpiritsGrave`, `ValidateWingDungeon`,
  and `ValidateHeadThwompFidelity`.

The compact grouping above intentionally does not conceal gaps: the confirmed
issues in this document are cases where the checked scenario boundary was too
narrow, tested a policy without its caller, or did not exist for the affected
path.

## Recommended correction order

1. [x] Reconcile the dirty room `$4:$09` input with the clean ROM so later
   comparisons use an uncontested source baseline.
2. [x] Move the shared RNG owner above the frontend/gameplay boundary and port
   every boot/title/cinematic RNG call before changing enemy traces.
3. [x] Route ordinary Link movement through retained 8.8 angle/speed state and
   add long cardinal/diagonal/collision path regressions.
4. [ ] Complete top-down Mermaid Suit movement/underwater transitions, add
   direct side-view swim/Cape tests, and add the missing bubble interaction
   without adding an extra RNG call. Top-down Flippers swimming and normal-
   water diving are implemented and covered.
5. [x] Fix `\slow()` and adjacent control-token parsing, then exhaustively scan
   every generated reachable message for unresolved commands.
6. [x] Port the retail boot/attract loop and exact title Start audio/fade calls.
7. [ ] Add an emulator differential tool that can load a checksum-valid retail
   save, drive deterministic inputs, and compare WRAM/object/RNG snapshots with
   Godot at selected update boundaries.
8. [ ] Continue content coverage in dependency order: missing item parents and
   terrain states, shared enemy handlers, NPC/event clusters, then later
   dungeons and story routes.
