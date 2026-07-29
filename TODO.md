# TODO

The project's highest priority is a 1:1 reconstruction of *Oracle of Ages*.
Consolidation is valuable only when it makes imported original behavior easier to
validate without obscuring table order, aliases, identifiers, or game-specific
semantics.

## Small Stuff
- [x] During the first post-Essence Maku cutscene, the HUD no longer reaches
  black before the room. The cleared status strip now renders blank tile
  `$00`'s palette-0 color 2 and follows the same 32-step delayed palette fade.
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

- [x] Import the common tile-interaction fallback data instead of maintaining
  dialogue and treasure literals in `InteractionController`.
  `tile_interaction_fallbacks.tsv` now retains TX `$510d`, TX `$510e`, and the
  real Eternal Spirit TX `$0901` with their handler identities, plus
  `getChestData@chestNotFound`'s `$2800` result resolved through
  `TREASURE_OBJECT_RUPEES_00`. The typed runtime record cross-checks that
  treasure's `$28:$00` fields, source graphic `$28`, text controls, and
  one-Rupee amount. Wrong-side sign/chest reads, an unmatched sign-table
  lookup, and a missing chest row are regressed independently of ordinary
  sign/chest records.

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

- [x] Move the remaining copied source tables and native constants used by
  implemented NPC interactions across the generated-data boundary.
  `Import-NpcData.ps1` now parses and emits the Black Tower `$57:$03`,
  `$40:$0c`, and `$58:$03` animation/text selectors; the non-Japanese
  `secretXorCipher` and 64-entry `secretSymbols` sequence; and Running Bipin
  `$28:$00`'s speed, angle, bounds, reversal, and animation-toggle record.
  Their typed runtime databases reject incomplete/noncontiguous data, and
  headless validation pins every selector/table plus Bipin's movement
  boundaries without production copies.

### Ownership and consolidation

- [x] Separate Bipin/Blossom family progression from `NpcDatabase` and use one
  family-state resolver for room construction and live dialogue refresh.
  `GetRoomNpcs` currently mutates child stage/personality and the seed-tree
  refill byte while also selecting records, and the post-name `$28:$00` /
  `$2b:$00` dialogue mapping is duplicated in `NpcDatabase` and
  `InteractionController`. Preserve the family spawner's original update slot,
  essence gate, save writes, and child-name substitution while making the
  generated database read-only.
  `NpcDatabase` now exposes only the imported base and family rows.
  `BipinBlossomFamilyStateResolver`, owned once by `RoomEntityManager`, runs
  `$ac` at its room-object slot, applies the Essence gates and personality
  tables, emits the selected records in source order, clears Ages refill bit
  1, and publishes one compound save change when stage state changes. The same
  resolver refreshes `\Child` substitution and the reversible post-name
  TX `$4301/$4409` mapping from immutable `NpcCharacter.BaseRecord` values;
  `InteractionController` no longer carries a second ID-to-text switch.

- [x] Move the remaining hand-coded `interactionRunScript` talk graphs out of
  `InteractionController` into typed command lanes hosted by their owning
  interaction: linked Ghini `linkedGameNpcScript`, past Bipin `bipinScript3`,
  and hardhat `$58:$00`'s Shovel script are the first candidates. Keep secret
  generation, `giveitem`, textbox waits, live RNG, and native presentation as
  explicit host operations at their source boundaries.
  `Import-CutsceneData.ps1` now emits all three source-addressed loops, adding
  typed `showloadedtext` and `checktext` commands to the enforced vocabulary.
  Independent NPC script hosts own actor/A-button state, input leases, exact
  20/30/1-update counters, choice branches, linked-secret generation, family
  text, native facing/animation, and manager-owned caller-completed rewards.
  `InteractionController` retains only ordered delegation and scheduling.

- [x] Replace the split NPC-talk dispatch with one ordered interaction-handler
  contract. Today priority is spread across `InteractionController` record
  special cases, `INpcTalkLifecycle` scans, an override delegate, and
  `RoomEventController`'s separate nine-event `TryInteractNpc` OR-chain.
  Register handlers in explicit source/gameplay priority, retain exact
  A-sensitive geometry, and make begin/end/cancel behavior uniform without
  merging the handlers' state machines.
  `NpcInteractionRouter` now owns one 17-route registry: family naming, the
  event-owned actors in their prior gameplay order, the three typed
  `interactionRunScript` hosts, ordinary dialogue, and the no-NPC Lynna-shop
  player route. `RoomEntityManager` resolves the first strict A-button target
  and its optional `INpcTalkLifecycle` owner together; the resulting target
  token provides idempotent begin/end/cancel without later entity scans.
  Room events and script hosts retain their independent state machines and
  room-change cancellation. Headless validation pins every registered source,
  first-claim behavior, NPC/player gating, and native lifecycle cleanup.

- [x] Extract the common interactive infinite-script host used by the saved
  Maku Tree, Comedian, and Mask Salesman events. Their runner lifecycle,
  `_buttonSensitive`/`_buttonPressed`/`_inputDisabled` state, input leases,
  actor matching, cancellation, and much of `ICutsceneCommandHost` are
  duplicated; retain typed per-script operations and source-aware unsupported
  diagnostics in the individual events.
  `InteractiveInfiniteScriptHost<TActor>` now owns the single-actor runner,
  room context, initial script updates, A-button queue, exact actor binding,
  idempotent input lease, and cancellation cleanup for all three events.
  Their room predicates, dialogue, metadata checks, rewards, and native
  handlers remain explicit. Headless validation cancels each active infinite
  loop under an acquired input lease and pins runner, A-button, player-control,
  and actor-animation cleanup without adding room loads or consuming RNG.

- [x] Consolidate script-granted ground-treasure construction and lifetime
  through `RoomEntityManager`. `RoomEventContext`, Vasu, Maku Tree, Dark Rooms,
  Spirit's Grave, past Bipin, and the Black Tower Shovel path currently build
  overlapping `GroundTreasureDatabaseRecord` values; past Bipin and the Shovel
  additionally attach/free `GroundTreasurePickup` directly under the world
  instead of using the entity spawn path. Provide one source-addressed grant
  API with explicit spawn/grab mode, visual override, flag timing, inventory
  write, sound order, dialogue, and completion ownership.
  `GroundTreasureGrantRequest` now resolves every listed grant path through
  the imported treasure object and optional typed visual override. The manager
  applies ordinary or concrete-ring inventory writes, ROOMFLAG_ITEM timing,
  behavior/grab sound order, before/after-grab dialogue, and shared or
  caller-owned completion. Deferred Maku, Dark Room, and Spirit's Grave
  rewards use the same queued spawn request; Vasu, command-host grants, past
  Bipin, and the Hardhat Shovel use the immediate grant path. Bipin and the
  Shovel no longer attach or free nodes outside `RoomEntityManager`.

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

- [x] Make the ROM's signed 8.8 `bank3.objectSpeedTable` the canonical enemy
  motion primitive.
  - Replace the remaining `VectorFromAngle32` trigonometry and floating-point
    accumulation in enemy walking, flying, knockback, hole pull, bosses, and
    hostile projectiles with exact imported components and fixed-point
    positions.
  - Validate multi-update non-cardinal paths against independent source
    vectors. Current scenarios often calculate their expectations with the
    same runtime helper, so they cannot detect cumulative rounding drift.
  - Completed with one ordered 768-record table covering all 24 speeds and 32
    angles. Implemented enemy walking, flying, recoil, hole pull, bosses,
    boomerangs, and hostile projectiles consume its exact signed components;
    the former Fairy-only subset was removed. Validation pins independent
    non-cardinal vectors and a cumulative 64-update enemy path.

- [x] Move the remaining copied enemy behavior and collision data across the
  importer boundary.
  - Extend `ImportedEnemyDefinition`, or adjacent typed records, beyond
    graphics, radii, damage, health, and animations to retain source collision
    modes/flags, item-collision responses, speeds, counters, gravity, bounds,
    projectile offsets, and per-state lookup tables.
  - Completed with 177 source-addressed rows under one strict typed owner. The
    original 77 lookup rows cover Keese deceleration, Octorok and Boomerang
    Moblin counters, enemy-arrow offsets/radii, Giant Ghini child offsets, and
    Pumpkin Head walk/stomp/head/projectile tables. Another 100 rows now own
    common sword/recoil/hazard/bounce behavior and implemented enemy/projectile
    state-entry speeds, counters, gravity, bounds, radii, and damage. Collision
    modes and initial flags remain sourced through the generated handler
    registry, while native state machines retain only transition and branch
    logic. The earlier common
    `ecom_bounceOffScreenBoundary@angleTable` and
    `ecom_sideviewAdjacentWallOffsetTable` copies remain imported through their
    dedicated typed resolver. Wallmaster hand counts now also come from each
    ordered placement (`5` in `4:12`, `2` in `4:c5`) instead of a copied
    Spirit's Grave constant.

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

- [x] Represent source death outcomes separately from Godot entity lifetime.
  `IRoomEnemyOutcomeSource` now emits explicit one-shot outcomes for counted
  `enemyDie`, `enemyDie_uncounted`, room-count decrement, recent-defeat marking,
  hazard and replacement deletion, silent deletion, boss teardown, and placed
  producer consumption. Room-count retention, recent-defeat state, and the
  shared Slayer/Maple/Gasha `EnemyDefeated` counters are independent effects.
  - Counted ordinary and boss death effects retain the live room-count
    contribution until their terminal update. Hazard disposal decrements it
    without marking recent defeat or advancing kill counters.
  - Wallmaster's five hand deaths each advance the counters without changing
    room count or recent defeat; final spawner completion independently
    decrements and marks. Red Zol replacement and an escaped Crow emit no death
    event, while both spawned Gels die and finish their puffs independently.

- [x] Replace implicit enemy support tests with one source-aware handler
  registry keyed by ID/subid.
  `enemy_handler_registry.tsv` now classifies all 118 ID/subid keys referenced
  by the ordered stream. Its 816 fixed/random source rows resolve exactly once
  as 233 ordered-implemented, six `$20:$00` rows whose only implemented lane is
  the event-owned dynamic Masked Moblin handler, or 577 deliberately
  unsupported rows; the 12 parameter-enemy rows are typed separately.
  `RoomEntityFactory` uses the same resolution for source slot/reservation
  policy, construction dispatch, and dungeon shutter completeness. Headless
  validation pins the 394/9/753 instance totals, handler/source identities,
  parameter slots, and existing unsupported-row reservation/RNG scenarios.

### Ownership and consolidation

- [x] Remove the six parallel species placement stores from `EnemyDatabase`.
  Keese, Octorok, Stalfos, Zol, Gel, and Crow room records duplicate placement
  data already present in `enemy_object_stream.tsv`; production then obtains a
  definition template from one of those room records while validation still
  exercises the legacy per-species getters. Generate unique ID/subid
  definitions separately, make the ordered object stream the sole placement
  authority, and add a deterministic migration cross-check before deleting the
  duplicate getters and TSV fields. The six generated tables now contain 10
  unique definitions only; importer-local projections prove all 185 former
  placement rows and 328 instances match the ordered stream before generation.
  Runtime and validation both join those definitions to ordered records, and
  the duplicate stores, counters, getters, record type, and TSV fields are gone.

- [x] Centralize ordinary enemy combat, death, drop, and room-count policy in a
  typed descriptor consumed by `CombatEnemyRoomEntityAdapter`.
  `EnemyCombatDescriptor` now owns the shared contact-damage, sword/burn,
  death-puff/drop, completion-outcome, kill-index, and room-count plumbing.
  Its typed source descriptor combines the registry's imported raw collision
  mode with each ordered object's flags, deriving count exemption rather than
  hard-coding it. Validation covers all 233 implemented ordered records, 15
  handlers, and 46 distinct ID/subid/flag combinations. Gel attachment, red
  Zol splitting, Wallmaster capture, Giant Ghini audio, and multipart-boss
  behavior remain explicit compositions or special descriptors.

- [x] Factor the shared hostile-projectile lifecycle used by Moblin arrows and
  Octorok rocks into a data-driven component. Their adapters and most of their
  visible-boundary, Link/shield contact, terrain collision, bounce, gravity,
  and lifetime handling are parallel implementations. Preserve their distinct
  source update order, collision-pending rules, damage source, speed, and
  animation, and leave owner-returning boomerangs as an explicit specialized
  path.
  `HostileProjectileLifecycle` now consumes a typed per-part profile for
  damage source, imported speed, collision radii, terrain-probe order,
  sword window, and bounce collision policy. One generic adapter owns both
  room-entity
  lifecycles. Rocks retain their destination probe, final movement step, and
  next-update state-2 bounce; arrows retain their current-tile immediate
  bounce, direction-specific offset/radii/animation, and generic damage path.
  Both share the exact `$20`-update `SPEED_40`, speedZ `-$00e0`, gravity `$0e`
  bounce. Owner-returning Moblin boomerangs remain a separate state machine.

## Remaining repository continuity audit

The July 2026 repository-wide pass covered the remaining 377 production C#
files, 15 staged importer scripts, generated asset readers, and 30 headless
validation files: application scheduling, Link and items, rooms and
transitions, menus, persistence, graphics, audio, and the command runner. The
NPC- and enemy-specific findings remain in their sections above.

### Update order and original arithmetic

- [x] Replace the independent subsystem catch-up loops with one
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
  - Completed with `ApplicationFixedUpdateScheduler` as the sole live delta
    consumer. `GameRoot` now replays Link movement/items, transitions, room
    object lifetimes and warp dispatch, events/interactions, HUD/dialogue/tile
    animation, and one audio sequencer tick before starting the next update.
    `ApplicationInputBuffer` gives all readers one immutable sample and clears
    pressed edges after their owning update. The validation-only integration
    trace proves split and batched host calls remain identical across the
    listed portal, child, warp, modal, presentation, audio, and input
    boundaries.

- [x] Make original object movement math one game-wide service rather than an
  enemy-only repair.
  - Build the game-wide movement owner around the complete generated signed
    8.8 `bank3.objectSpeedTable` already consumed by enemy/item-drop paths.
    Port `objectGetRelativeAngle`'s integer decision path as well. Before this
    migration, `OracleObjectMath.VectorFromAngle32` and `AngleToward` remained
    in non-enemy object paths with trigonometry and rounded floating-point
    coordinates.
  - Use the service for Maple and her dropped items, scripted Impa/Nayru
    actors, Gasha nuts, tree seeds, essence beads, Running Bipin, item and
    shovel debris, cutscene command movement, and all enemy paths named above.
    Preserve signed overflow, low-byte accumulation, high-byte rendering, and
    source-specific speed scaling.
  - Validate angle-boundary decisions and long non-cardinal paths against
    independent ROM vectors, including byte wrap and cumulative subpixel
    remainders; expectations must not call the runtime helper under test.
  - Completed with `OracleObjectMovement` as the sole speed-vector and
    relative-angle owner. The importer emits all 64 ordered
    `pushDirectionData` decisions beside the 768 speed vectors; gameplay paths
    retain wrapping 8.8 words and render their high bytes. Independent
    validation pins integer ratio boundaries, the `$f8` coordinate boundary,
    source-order knockback reversal, and a 300-update non-cardinal path through
    low-byte carry and word wrap.

- [x] Move the remaining Link, item, and sword-tile tables across the
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
  - Completed with five typed metadata tables and one shared strict loader.
    `Player`, `CombatController`, and `BraceletController` now consume the
    imported records; validation pins all 236 rows, source aliases,
    terminators, action boundaries, and rendered OAM hashes.

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

- [x] Export the remaining table-shaped menu presentation data.
  - `Import-MenuPresentationData.ps1` now emits ordered typed records for
    `mapIconOamTable`, `dungeonMapFloorListStartPositions`, dungeon-blurb
    selectors, inventory slots/passive treasures/secondary cursors/Essence
    positions, and the remaining file, save/quit, and ring-menu OAM layouts.
    Source labels, ordering, and repeated-graphic aliases are retained.
  - `MenuPresentationDatabase` validates exact schemas and counts before the
    map, inventory, file, save/quit, and ring screens consume those records.
    Procedural cursor and modal state transitions remain in their controllers.
  - Validation independently checks exact count/order/alias contracts and
    representative popup, cursor, dungeon floor-list, and passive-treasure
    pixel hashes.

### Graphics, commands, and validation ownership

- [x] Model all eight live BG palette slots instead of aliasing palette 1 to a
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
  `BackgroundPaletteState` now owns all eight live gameplay BG slots.
  Room/transition tileset loads and implemented palette effects write slots
  2-7 through that owner, while `DialogueBox` loads the imported PALH
  `$0e/$0d/$bd` colors into slot 1 and leaves the final write intact on close.
  Current and incoming room textures rerender from the shared slots, Link uses
  the source's alternate-textbox top-object priority only while flag `$04` is
  active, and room `5:0b` validation pins ordinary/alternate tile colors,
  slots-2-7-only darkening, Link priority, and both retained close states.

- [x] Route all source graphics through the shared cache and consolidate the
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

- [x] Make the cutscene command vocabulary an enforced command schema.
  `script_command_vocabulary.tsv` now declares source aliases and byte shapes,
  normalized field shapes, concrete record types, runner results, ordered
  actor members, and host capabilities for all 51 typed commands.
  - The importer validates every emitted stream against this definition.
    Runtime decoding enforces the declared record type, startup reflection
    proves every concrete executor record has exactly one entry, and the
    runner validates actor bindings and block/yield/continue/end outcomes from
    the schema.
  - Command hosts now inherit source-aware default-deny capabilities instead
    of repeating unconditional unsupported implementations. Event-specific
    operand checks, native handlers, and actor registries remain explicit.

- [x] Move active validation audit bookkeeping out of production classes.
  `OracleSoundEngine`, `OracleGraphicsCache`, and `CombatController` now expose
  narrow internal observation hooks without storing request history, audit
  counters, last effects, or resettable validation state.
  - Validation-owned observers retain ordered sound requests and per-ID
    counts, structured cache build/hit operations with key detail, and spawned
    clink references. Existing timing, cache-identity, sound-order, and effect
    assertions consume those observers.
  - Production retains truthful sequencer/channel state, active music, current
    cache contents, and world-owned effect nodes. Shutdown still releases audio
    and graphics resources without retaining validation observers.

## Codebase size reduction audit

The July 2026 size audit measured approximately 83,000 lines of production C#,
33,000 lines of validation C#, and 18,000 lines of importer PowerShell. Prefer
the following reductions where they remove repeated policy or infrastructure;
do not merge source-distinct enemy species, native story state machines, update
slots, or RNG paths merely because their current implementations look similar.

- [x] Centralize deterministic importer output and the duplicated assembly
  table readers. The staged importer contains 203 explicit UTF-8-without-BOM
  `WriteAllLines` encoding selections plus repeated destination-directory
  setup. Add shared `Write-GeneratedTable` and `Write-GeneratedBytes` helpers
  that retain exact bytes, ordering, headers, and source diagnostics. Merge
  `Read-EnemyDwTables` and `Read-NpcDwTables` into one source-model helper while
  leaving domain interpretation in the owning stages. Declare every new helper
  in the stage contracts and require `verify_oracle_import.ps1` to prove
  byte-for-byte deterministic output.

- [x] Generalize the disposable Maple validation harness into a reusable
  room/entity validation fixture. Validation currently constructs
  `RoomEntityManager` 38 times and repeats temporary node creation, save/runtime
  state, database defaults, entity clearing, child removal, disposal, and
  `Free` calls. Provide narrow fixture options for the exceptional collaborators
  instead of one constructor with every possible dependency, and keep the
  harness wholly inside the validation assembly.

- [x] Extract a source-aware default-deny base for cutscene command hosts. The
  17 current hosts repeat their `ICutsceneCommandHost` surface, unsupported
  operation diagnostics, actor checks, and input-control ownership; Comedian,
  Mask Salesman, Poe, and the saved Maku Tree also repeat the same input lease
  transitions. Make unsupported commands throw with script/source identity,
  require explicit overrides for supported operations, and retain each native
  event state machine. Treat this as the shared infrastructure slice of the
  interactive infinite-script and command-schema work already listed above.

- [x] Complete the shared Game Boy tile/OAM compositor instead of retaining
  screen-local copies. Move the parallel Black Tower explanation and Nayru
  singing 32-by-18 background composition, VRAM destination mapping, 8-by-16
  OAM flips/palettes, and cell texture caching onto `OracleTileRenderer`.
  Reuse the same VRAM pixel/source resolver and monochrome-font conversion in
  the inventory and ring screens where their addressing rules agree. Keep
  scene-specific scrolling, priority, and timing in the screens and pin the
  refactor with existing and new cross-scene pixel hashes. This is the
  code-size slice of the broader graphics/cache task above.

- [x] Remove the remaining simple effect room-adapter boilerplate without
  weakening capability opt-ins. Shovel debris, rock debris, puzzle puffs,
  key-use effects, splashes, and similar short-lived effects repeat
  `Finished`, fixed-update forwarding, transition offset, and dialogue-update
  plumbing around `RoomEntityAdapter<T>`. Introduce one narrow fixed-effect
  adapter or effect contract for the truly identical lifecycle. Give
  `IUpdatesDuringDialogueRoomEntity.UpdatesDuringDialogue` a default of `true`
  so only the few state-dependent implementations override it. Do not fold NPC,
  combat, contact, or source-order behavior into this convenience layer.

- [x] Add small ordered-lookup and capability-query primitives for mechanical
  runtime repetition. Generated databases contain roughly 20 repeated
  dictionary add-or-create/list lookup blocks; use one order-preserving
  `Lookup<TKey, TValue>` while keeping schema parsing and source-specific
  validation in each typed database. Likewise, replace the six parallel
  `IPlayerRestriction` scans in `RoomEntityManager` with one internal predicate
  helper that preserves entity order and the existing movement special case.
  Do not replace the 78 typed database classes with reflection or a universal
  record binder, because their schemas and source contracts are intentionally
  explicit.

- [x] Let the structured importer parser own assembly syntax. Version the
  process protocol and expose ordered label, data-directive, macro,
  instruction, and constant node queries with operands, active-branch state,
  and source spans. Migrate stage-local label scanners, operand splitters,
  pointer-table readers, animation/OAM readers, and source-line recovery to
  those typed rows while leaving domain interpretation in the owning stage.
