# TODO

The project's highest priority is a 1:1 reconstruction of *Oracle of Ages*.
Consolidation is valuable only when it makes imported original behavior easier to
validate without obscuring table order, aliases, identifiers, or game-specific
semantics.

## Small Stuff
- [ ] During the first post-essence maku cutscene the hud fades faster to black than the rest of the screen
- [x] When starting a new game, the era indicator is shown when Link spawns.
  The summon arrival now retains the original `linkSummonedCutscene` room-load
  boundary, which does not call the ordinary era-info predicate.
- [ ] Fairies (for example when dropped by a miniboss) have the same "drop" physics as a normal item drop, they should not have that
- [ ] Boomerang moblins not making the damage sound when they are hit
      - [ ] Wallmasters too
- [ ] When getting hit while carrying something, the carried object floats in the air shortly before dropping down. Should drop down instantly.
- [ ] Dungeon eyes (entry room) and portal not visible during screen transition, pop into existence once transition is complete.

## NPC and interaction continuity audit

The July 2026 audit covered the 388 generated positioned/state-derived
NPC/character records across 211 rooms, 72 generated Bipin/Blossom family
variants, ordinary visibility/dialogue/position rules, specialized native NPC
adapters, event-owned talk routes, shops, signs, chests, script-granted ground
treasures, and their headless scenarios. Preserve the original object-slot and
handler order while addressing these items. The room-by-room character record
snapshot is maintained in
[`docs/npc-interaction-coverage.md`](docs/npc-interaction-coverage.md).

### Fidelity and generated-data boundaries

- [ ] Import the common tile-interaction fallback data instead of maintaining
  dialogue and treasure literals in `InteractionController`.
  - Export TX `$510d`, TX `$510e`, and TX `$0901` with their source identities.
    The current no-match sign branch labels its literal as TX `$0901` but shows
    clone-only "Nothing is written here" text; the Ages TX `$0901` body is the
    Eternal Spirit message used by `nextToSignTile`.
  - Construct `getChestData`'s explicit missing-row default `$2800` from
    `TREASURE_OBJECT_RUPEES_00`. The current hand-built fallback selects visual
    graphic `$2b`, while the imported treasure object uses graphic `$28`, and
    it loses the source text controls.
  - Validate wrong-side sign/chest reads, an unmatched sign-table lookup, and
    the missing-chest-row default independently of the ordinary chest table.

- [x] Give every generated `NpcRecord` an explicit implementation
  classification: ordinary generic NPC, specialized native interaction,
  event-owned actor, or deliberately unsupported.
  The importer now classifies all 388 positioned/state-derived rows and all 72
  family variants. `RoomEntityFactory` dispatches ordinary, specialized, and
  event-owned records through separate typed paths and suppresses deliberately
  unsupported handlers; `NpcRoomEntity` itself rejects non-ordinary records
  with source identity. Headless validation pins the four-category totals,
  adapter boundary, event object slots, and graphics-only suppression.

- [x] Preserve an immutable base NPC state and make live dialogue refresh
  reversible, matching the existing reversible visibility and position paths.
  `NpcCharacter` now retains its imported `BaseRecord` separately from mutable
  presentation state, and every ordinary save-state refresh resolves
  visibility, dialogue, and position from that base. A dialogue table with no
  currently matching row restores the base text/facing, while ambiguous rows
  report every matching source. Canonical room `1:86` flag `$80` and room
  `0:68` progress regressions cover changes in both directions.

- [ ] Move the remaining copied source tables and native constants used by
  implemented NPC interactions across the generated-data boundary.
  - Export the Black Tower `$57:$03` eight-entry animation/text selectors,
    `$40:$0c` four-entry text selector, and `$58:$03` five-entry text selector;
    the importer currently asserts them while the runtime repeats them in
    three separate entity classes.
  - Export the linked-secret `secretXorCipher` and 64-entry `secretSymbols`
    table used by `$cb:$00`; the current database validates only their copied
    lengths.
  - Export or strictly pin the `$28:$00` Running Bipin speed, angle, bounds,
    and animation-toggle inputs instead of leaving validation as the only
    source contract.

### Ownership and consolidation

- [ ] Separate Bipin/Blossom family progression from `NpcDatabase` and use one
  family-state resolver for room construction and live dialogue refresh.
  `GetRoomNpcs` currently mutates child stage/personality and the seed-tree
  refill byte while also selecting records, and the post-name `$28:$00` /
  `$2b:$00` dialogue mapping is duplicated in `NpcDatabase` and
  `InteractionController`. Preserve the family spawner's original update slot,
  essence gate, save writes, and child-name substitution while making the
  generated database read-only.

- [ ] Move the remaining hand-coded `interactionRunScript` talk graphs out of
  `InteractionController` into typed command lanes hosted by their owning
  interaction: linked Ghini `linkedGameNpcScript`, past Bipin `bipinScript3`,
  and hardhat `$58:$00`'s Shovel script are the first candidates. Keep secret
  generation, `giveitem`, textbox waits, live RNG, and native presentation as
  explicit host operations at their source boundaries.

- [ ] Replace the split NPC-talk dispatch with one ordered interaction-handler
  contract. Today priority is spread across `InteractionController` record
  special cases, `INpcTalkLifecycle` scans, an override delegate, and
  `RoomEventController`'s separate nine-event `TryInteractNpc` OR-chain.
  Register handlers in explicit source/gameplay priority, retain exact
  A-sensitive geometry, and make begin/end/cancel behavior uniform without
  merging the handlers' state machines.

- [ ] Extract the common interactive infinite-script host used by the saved
  Maku Tree, Comedian, and Mask Salesman events. Their runner lifecycle,
  `_buttonSensitive`/`_buttonPressed`/`_inputDisabled` state, input leases,
  actor matching, cancellation, and much of `ICutsceneCommandHost` are
  duplicated; retain typed per-script operations and source-aware unsupported
  diagnostics in the individual events.

- [ ] Consolidate script-granted ground-treasure construction and lifetime
  through `RoomEntityManager`. `RoomEventContext`, Vasu, Maku Tree, Dark Rooms,
  Spirit's Grave, past Bipin, and the Black Tower Shovel path currently build
  overlapping `GroundTreasureDatabaseRecord` values; past Bipin and the Shovel
  additionally attach/free `GroundTreasurePickup` directly under the world
  instead of using the entity spawn path. Provide one source-addressed grant
  API with explicit spawn/grab mode, visual override, flag timing, inventory
  write, sound order, dialogue, and completion ownership.

- [x] Remove room-entity adapter boilerplate without erasing capabilities.
  `IRoomEntityLifetime.OnFinished` now defaults to no action; 42 empty
  production overrides were deleted, while the three entities with real
  combat, drop, hazard, or successor completion work retain explicit
  implementations.
  - A broader NPC adapter base was deliberately not added. The 11 current NPC
    adapters divide across fixed/variable update, script-sensitive geometry,
    save refresh, player restrictions, and talk lifecycle capabilities; only
    the Black Tower family shares enough policy to keep its existing narrow
    base without hiding those opt-ins.

## Enemy continuity audit

The July 2026 audit covered all 1,141 generated ordered enemy-object records:
816 fixed/random enemy placements, 270 item-drop parts, 43 reserving parts, and
12 parameter records. The current factory dispatches 233 placement records to
394 ordinary enemy instances across 15 ID/subid variants, plus the dynamic
Masked Moblin, Giant Ghini, Pumpkin Head, child, projectile, death, and drop
paths. Preserve object order, slot reservation, the shared placement buffer,
and RNG consumption while addressing these items.

### Fidelity and generated-data boundaries

- [ ] Make the ROM's signed 8.8 `bank3.objectSpeedTable` the canonical enemy
  motion primitive.
  - Replace the remaining `VectorFromAngle32` trigonometry and floating-point
    accumulation in enemy walking, flying, knockback, hole pull, bosses, and
    hostile projectiles with exact imported components and fixed-point
    positions.
  - Validate multi-update non-cardinal paths against independent source
    vectors. Current scenarios often calculate their expectations with the
    same runtime helper, so they cannot detect cumulative rounding drift.

- [ ] Move the remaining copied enemy behavior and collision data across the
  importer boundary.
  - Extend `ImportedEnemyDefinition`, or adjacent typed records, beyond
    graphics, radii, damage, health, and animations to retain source collision
    modes/flags, item-collision responses, speeds, counters, gravity, bounds,
    projectile offsets, and per-state lookup tables.
  - Remove remaining species-specific timing tables. The common
    `ecom_bounceOffScreenBoundary@angleTable` and
    `ecom_sideviewAdjacentWallOffsetTable` copies are now imported once with
    source identities, exact ordering checks, and one typed runtime owner.

- [x] Correct and consolidate adjacent-wall probing before adding more enemy
  handlers. The importer now preserves the common four-pair offset stream,
  bounce-angle table, and source identities. One typed resolver applies the
  probes cumulatively for Keese, Stalfos, and common knockback, with
  horizontal, vertical, and corner regressions for every consumer.

- [x] Restore inventory-dependent enemy drops through one availability path.
  `DecideDrop`, deterministic `ChooseDrop`, breakable drops, placed producers,
  ordinary death puffs, and boss death explosions now all use the live
  inventory/save-backed obtained-treasure predicate for Bombs and all five seed
  types. Probability and selection RNG remain ahead of that predicate.
  - Boomerang Moblin `$0a` set `$06`, Arrow Moblin `$0c` set `$0c`, Rope `$10`
    set `$01`, and Crow `$41` set `$0d` regress owned/unowned results and the
    common death-puff handoff.
  - Full Bomb/seed capacity still permits collection without exceeding the
    cap, while Red, Blue, and Gold Joy Ring pickup quantities remain doubled.

- [ ] Represent source death outcomes separately from Godot entity lifetime.
  `IRoomKillTrackedEnemy.MarksEnemyKilled` currently gates both the
  recent-defeat bit and the global `EnemyDefeated` counters, and both are
  evaluated only when an entity becomes `Finished`.
  - Model counted `enemyDie`, `enemyDie_uncounted`, room-count decrement,
    `markEnemyAsKilledInRoom`, hazard deletion, replacement/split deletion, and
    boss teardown as explicit independent outcomes.
  - Fix hazard disposal: `ecom_decNumEnemiesAndDelete` decrements the room count
    and creates the hazard effect but does not mark the recent-defeat bit or
    advance Slayer/Maple/Gasha kill counters; the current ordinary adapter does
    both.
  - Preserve five `enemyDie_uncounted` counter events for Wallmaster hands and
    one final recent-defeat mark from the spawner, rather than the current one
    combined event. Regress that red Zol replacement itself and an escaped Crow
    emit no death event, while the two spawned Gels still die independently.

- [ ] Replace implicit enemy support tests with one source-aware handler
  registry keyed by ID/subid.
  - Use the registry for `RoomEntityFactory` construction, slot/reservation
    policy, and dungeon shutter completeness. The current factory returns
    `null` for unmatched enemy rows while `DungeonEnemyCountIsComplete`
    independently duplicates the supported-ID switch.
  - Classify every imported placement as implemented, dynamic/special, or
    deliberately unsupported, and validate that all 816 source rows resolve
    exactly once without changing unsupported-row reservations or placement
    RNG.

### Ownership and consolidation

- [ ] Remove the six parallel species placement stores from `EnemyDatabase`.
  Keese, Octorok, Stalfos, Zol, Gel, and Crow room records duplicate placement
  data already present in `enemy_object_stream.tsv`; production then obtains a
  definition template from one of those room records while validation still
  exercises the legacy per-species getters. Generate unique ID/subid
  definitions separately, make the ordered object stream the sole placement
  authority, and add a deterministic migration cross-check before deleting the
  duplicate getters and TSV fields.

- [ ] Centralize ordinary enemy combat, death, drop, and room-count policy in a
  typed descriptor consumed by `CombatEnemyRoomEntityAdapter`.
  - The species adapters repeatedly construct the same contact-damage,
    sword/burn, death-puff, and transition plumbing, while common IDs `$0a`,
    `$0c`, `$10`, `$17`, and `$28` hard-code `countsAsEnemy: true` instead of
    deriving the source flag as the other ordinary species do.
  - Keep explicit overrides for genuinely different behavior such as Gel
    attachment, red Zol splitting, Wallmaster capture, Giant Ghini audio, and
    multipart bosses; validate the descriptor against every implemented
    source flag and collision mode.

- [ ] Factor the shared hostile-projectile lifecycle used by Moblin arrows and
  Octorok rocks into a data-driven component. Their adapters and most of their
  visible-boundary, Link/shield contact, terrain collision, bounce, gravity,
  and lifetime handling are parallel implementations. Preserve their distinct
  source update order, collision-pending rules, damage source, speed, and
  animation, and leave owner-returning boomerangs as an explicit specialized
  path.

## Remaining repository continuity audit

The July 2026 repository-wide pass covered the remaining 377 production C#
files, 15 staged importer scripts, generated asset readers, and 30 headless
validation files: application scheduling, Link and items, rooms and
transitions, menus, persistence, graphics, audio, and the command runner. The
NPC- and enemy-specific findings remain in their sections above.

### Update order and original arithmetic

- [ ] Replace the independent subsystem catch-up loops with one
  application-owned 60-update scheduler.
  - `GameRoot._Process` currently updates transitions, entities, room events,
    interactions, the HUD, and animated tiles in subsystem-sized batches, and
    each subsystem consumes `delta` with a separate accumulator. `Player`
    additionally splits gameplay between `_PhysicsProcess` and `_Process`,
    while `OracleSoundEngine` advances from another node callback.
  - A three-update host frame can therefore run three transition updates, then
    three entity updates, then one contact/removal/warp pass, then three event
    updates. Replay the complete documented original update order once per
    consumed update instead, including Link movement/items/hazards, entity
    contacts and pending spawns/removals, event and interaction dispatch, HUD,
    animation, and sequencer ticks. Keep variable-rate presentation separate.
  - Sample one input snapshot per original update and expose just-pressed
    edges only on the update that owns them. Add an integration regression
    proving that `N` calls at `1/60` and one call at `N/60` produce identical
    state through a portal start, child spawn/death, room warp, menu boundary,
    and held-versus-pressed input.

- [ ] Make original object movement math one game-wide service rather than an
  enemy-only repair.
  - Import the complete signed 8.8 `bank3.objectSpeedTable` by speed and angle,
    generalizing the exact `$SPEED_200` vectors already emitted only for
    Fairies' Woods. Port `objectGetRelativeAngle`'s integer decision path as
    well; `OracleObjectMath.VectorFromAngle32` and `AngleToward` currently use
    trigonometry and rounded floating-point coordinates.
  - Use the service for Maple and her dropped items, scripted Impa/Nayru
    actors, Gasha nuts, tree seeds, essence beads, Running Bipin, item and
    shovel debris, cutscene command movement, and all enemy paths named above.
    Preserve signed overflow, low-byte accumulation, high-byte rendering, and
    source-specific speed scaling.
  - Validate angle-boundary decisions and long non-cardinal paths against
    independent ROM vectors, including byte wrap and cumulative subpixel
    remainders; expectations must not call the runtime helper under test.

- [ ] Move the remaining Link, item, and sword-tile tables across the
  generated-data boundary.
  - Turn `Import-MapAndItemData.ps1`'s assertion-only shield/Link graphics and
    collision checks into typed output. Export the sword action timing,
    animation and pose offsets, collision arcs, OAM parts, slash-sound table,
    shield positions/radii and graphics selection, shovel offsets/timing, and
    Link-facing portions of the Bracelet lift path that `Player` still copies.
  - Export `tryBreakTileWithSword.@linkOffsets` and the aliased,
    zero-terminated `clinkSoundTable`. `CombatController` currently repeats
    both as `SwordTileOffsets`, `BombableWallClinkTiles`, and
    `SilentSwordClinkTiles`, separate from the imported breakable-tile data.
  - Consume the records from `Player`, `CombatController`, and the item
    controllers without merging their state machines. Validate exact row
    order, collision-mode aliases, terminal zeroes, frame boundaries, OAM
    pixels, and shield/projectile response IDs.

### State and generated-data boundaries

- [x] Make each compound inventory grant one atomic live-save transaction.
  Previously, `InventoryState.GiveTreasureCore` called
  `OracleSaveData.AddGashaMaturity`, which raised `OracleSaveData.Changed`
  before the treasure flags, quantities, health, and inventory slots were
  written; `NotifyChanged` then raised it a second time.
  - Added a scoped save mutation that records dirty state and publishes one
    notification only after every WRAM-style field and the `InventoryState`
    cache agree, while preserving the original order of the actual field
    writes, health/refill signals, and composite Seed Satchel/Heart Container
    grants.
  - Regressed maturity-bearing treasures and composite grants by counting
    callbacks and inspecting the complete save/inventory snapshot from inside
    the callback, not only after `GiveTreasure` returns.

- [x] Complete the typed boundary for named persistent map fields.
  Previously, `MapScreen` read time-portal group/room at `$c63e/$c63f` and
  animal companion at `$c610` directly, while `MapDataDatabase` repeated
  `$c610` and bypassed the existing `MakuMapTextPresent`/`MakuMapTextPast`
  properties with `$c6e6/$c6e7`.
  - Added typed `OracleSaveData` accessors and passed the existing
    `InventoryState` view through one map-presentation state resolver used by
    both map classes. Raw address access remains limited to imported generic
    bindings whose address is itself source data.
  - Validated portal absence (`$ff`), both eras, all three companion regions,
    and live Maku advice changes without reconstructing addresses in the test.

- [ ] Export the remaining table-shaped menu presentation data.
  - Replace `MapScreen`'s copied `mapIconOamTable`,
    `dungeonMapFloorListStartPositions`, and dungeon-blurb selector array with
    ordered typed records retaining source labels and aliases.
  - Do the same for `InventoryScreen`'s item slots, passive-treasure placement,
    secondary-item cursor bytes, essence positions, and for the remaining
    file/ring-menu OAM layout tables. Leave genuinely procedural cursor and
    modal state transitions in their owning controllers.
  - Add exact count/order checks plus representative cursor, popup, floor-list,
    and passive-item pixel hashes so generated layout data and renderer
    behavior are tested independently.

### Graphics, commands, and validation ownership

- [ ] Model all eight live BG palette slots instead of aliasing palette 1 to a
  tileset fallback.
  - `OracleRoomData.RenderRoom` handles raw palette 0 specially, handles 2-7
    through the tileset palette, but clamps raw palette 1 to tileset palette
    0. The source loads PALH `$0d` or `$0e` into BG slot 1 for textbox modes,
    including `TEXTBOXFLAG_ALTPALETTE1`.
  - Give room/dialogue/cutscene presentation a shared palette-state owner and
    apply or restore slot writes at their original boundaries. Preserve BG
    priority and the existing rule that fades affect slots 2-7 without
    changing slots 0-1.
  - Regress ordinary and alternate textbox palettes over a room tile that
    selects palette 1, including Link's alternate-palette draw priority and
    the exact post-textbox palette state.

- [ ] Route all source graphics through the shared cache and consolidate the
  remaining VRAM/tile/OAM compositors.
  - `MapScreen` is the lone production screen with a private
    `Image.LoadFromFile` path and duplicates common-palette loading. Extend
    `OracleGraphicsData` for its partial eight-slot palette fill and use
    `OracleGraphicsCache` for every map and dungeon-blurb image.
  - `BlackTowerExplanationScreen` and `NayruSingingScreen` separately
    implement the same 32x18 tilemap composition, VRAM destination mapping,
    8x16 OAM-cell flips/palettes, grayscale conversion, and per-cell texture
    cache. Extract one typed cutscene compositor on top of
    `OracleTileRenderer` and the OAM cache while retaining scene-specific
    scrolling, priority, white flash, and byte-wrapped coordinates.
  - Validate clean-checkout PNG loading, cache identity/build reuse, and
    cross-scene pixel/offset hashes.

- [ ] Make the cutscene command vocabulary an enforced command schema.
  `script_command_vocabulary.tsv` is generated but neither runtime nor
  validation reads it; source macro aliases/lengths, normalized opcode
  decoding in `CutsceneCommandCatalog`, execution/yield behavior in
  `CutsceneCommandRunner`, actor enumeration, and host operations are separate
  hand-maintained switches.
  - Define source aliases, byte shape, normalized operands/payload,
    block/yield/continue/end result, actor operands, and required host
    capability once, then have importer and runtime generation or validation
    consume that definition.
  - Prove every emitted command has exactly one typed decoder and executor and
    every executor has a declared schema entry. Replace the repeated
    unsupported-method boilerplate across command hosts with a source-aware
    default-deny capability adapter; keep event-specific native handlers and
    actor registries explicit.

- [ ] Move active validation audit bookkeeping out of production classes.
  `OracleSoundEngine` maintains a 256-entry request counter and last-request
  value, `OracleGraphicsCache` maintains load/build/hit audit counters, and
  `CombatController` retains the last clink plus a spawn count solely for
  headless assertions.
  - Attach validation-owned sound, cache, and effect observers/sinks instead,
    preserving request order and cache-operation detail needed by the current
    scenarios without changing production behavior or resource lifetime.
  - Retain narrow truthful views of real state such as active channels and
    current cache contents, but remove resettable counters and trace history
    that the shipped runtime would not otherwise maintain.
