# Rooms and entities

## Room geometry and identity

Small room layouts are 10 by 8 metatiles, or 160 by 128 pixels. Large room
layouts use a 16 by 11-metatile storage grid with the original 16-byte row
stride; only 15 by 11 metatiles (240 by 176 pixels) are playable, and column 16
is padding.

A room is identified by group and hexadecimal room ID. Group aliases in save
tables do not by themselves make runtime rooms interchangeable. The original
tileset and object-pointer tables explicitly alias side-scrolling groups `$06`
and `$07` to source groups `$04` and `$05`; retain the active side-scrolling
identity while resolving their room tilesets and placed objects through that
source alias. Dungeon neighbors come from imported dungeon floor layouts, not
room-ID arithmetic. `RoomSession` owns the active identity and must be used for
neighbor and layout resolution.

## Transition lifetime

A scrolling transition keeps an active room/entity set and an outgoing set.
The destination room and its entities may be created before the scroll starts,
but ordinary destination entities and room events do not update until scrolling
finishes. Outgoing ordinary entities are likewise frozen while retained for
drawing. This prevents destination AI, cutscenes, drops, and counters from
fast-forwarding during the 32-update scroll.

An object may update during a transition only when the original explicitly does
so. The retained Impa follower is one such behavior: it receives a separate
transition update path instead of allowing accumulated ordinary event time to
drain after the scroll.

The transition controller supplies draw offsets and updates the room camera.
Logical room coordinates stay in their original space. At completion, rebuild
state that the original rebuilds (for example a follower path buffer) rather
than carrying stale source-room history into the destination.

## Ordered room objects and enemy reservations

Enemy placement executes one importer-generated ordered room-object stream. Do
not group records by species before creation. The original order determines
which fixed objects reserve positions before later random enemies.

Use one occupied-position set for the complete stream:

- Every applicable fixed enemy or part reserves its packed tile before later
  random placement.
- Random enemies reserve the accepted tile immediately.
- Unsupported objects that reserve space remain in the stream as explicit
  reservation-only records.
- Do not clear reservations between Keese, Octoroks, ordinary Stalfos, Zols,
  Gels, or another species group.

If a placement still appears unusual, trace the original validity routine and
entry context before changing tile filters. A visually implausible water spawn
may indicate a missed terrain restriction, an incorrect packed coordinate, a
lost reservation, or the wrong transition exclusion region.

## Placement RNG and entry exclusions

`OracleRandom` is game-wide because enemy AI, drops, sounds, and placement share
the original RNG stream. At the beginning of every room-object parse, regenerate
the placement buffer and reset its index. Regeneration deliberately consumes
the original 256 global RNG calls; reusing a previous permutation changes later
AI and drops even if the placements happen to look valid.

`EnemyPlacementContext` describes why the room was loaded:

- `Unrestricted` is used when there is no incoming exclusion.
- `Scrolling` excludes the original rows or columns near Link's incoming edge.
- `Warp` excludes the surrounding 5 by 5-metatile area around the packed
  destination.
- `ScreenWarp` retains the same incoming-edge enemy exclusion as upward
  scrolling for destination bytes `$f0`-`$ff`, while remaining distinguishable
  to whiteout-only room interactions such as dungeon-stuff `$12:$00`.

Do not infer the context from Link's eventual position after entities have
already been parsed. Pass it from the transition/load operation that owns the
entry.

## Entity contracts

`RoomEntityManager` composes behavior through small capabilities rather than a
single universal entity base class:

| Contract | Meaning |
| --- | --- |
| `IVariableRoomEntity` | Delta-driven presentation or behavior that is intentionally variable |
| `IFixedRoomEntity` | One original-engine update with deterministic spawn output |
| `ILinkContactEntity` | Post-update Link contact handling |
| `ISwordHittableRoomEntity` | Sword collision and hit response |
| `IObjectCollisionHeightRoomEntity` | Optional object `zh` for item/enemy collision; absent means ground height |
| `ISeedHittableRoomEntity` / `ISeedProjectileRoomEntity` | Active seed hit response and one-shot projectile collision ownership |
| `IPlayerProjectileRoomEntity` | Player-owned projectile bounds, damage, and accepted-hit completion |
| `IRoomBlocker` / `ITalkTarget` | Collision or interaction capability |
| `IOrdinaryNpcEntity` | A placed NPC eligible for live imported save-predicate refresh |
| `IPlayerRestriction` | Native interaction-owned sword and/or movement input suppression |
| `IRoomEntityLifetime` | Completion and final spawned effects |
| `IRoomEnemyCounterEntity` | A live combat enemy or native puzzle sentinel contributing to `wNumEnemies` |
| `IRoomKillTrackedEnemy` | A source enemy's transient `$01-$07` killed-list index and whether completion marks it |
| `IRoomSaveStateEntity` | Refresh from changed live save state |

Shared combat, terrain movement, vertical motion, and animation components may
remove mechanical duplication. Single-body enemies inherit the record-neutral
`EnemyCharacter`, which owns health/lifetime state, collision radii, the
`EnemyAnimationPlayer`, and optional invulnerability rendering. Each species
still owns its typed imported record, decisions, counters, movement, hit/death
policy, and RNG consumption. Multi-part enemies whose independently animated
parts have different health or collision state, such as Pumpkin Head, compose
those mechanics directly instead of forcing them into one character tuple.
Spawn records state whether a child updates in the creation frame; preserve
that distinction. Drawable room nodes ultimately inherit
`TransitionOffsetNode2D`; it owns only the presentation offset applied during
scrolling and never changes logical room/world coordinates.

Ordinary enemy species are not owned by the first room or dungeon that makes
them playable. Boomerang Moblin, Rope, Ghini, and Wallmaster live with the
other species and resolve their subid-0 definitions through `EnemyDatabase`
for every matching ordered room record. Unsupported Rope/Ghini subids remain
explicit reservations rather than silently receiving the wrong state machine.
Wallmaster capture resolves the active dungeon's imported
`wDungeonWallmasterDestRoom`; it does not encode Spirit's Grave room `4:24` in
the entity adapter.

Side-view terrain movement must preserve the source velocity table's exact zero
components for cardinal angles. A blocked cardinal move returns zero; it must
not test the unchanged perpendicular coordinate and report success. Rope
`$10:$00` depends on that return value: `objectCheckCenteredWithLink` accepts an
inclusive ten-pixel match on either axis, the Rope takes one fixed cardinal
`SPEED_140` lock, and only a wall/hole collision ends the charge. That collision
restores `SPEED_60`, sets `counter2` to `$40`, and calls
`rope_changeDirection`; the charge does not continuously retarget Link.

## Bracelet tile and entity ownership

`BraceletController` owns `ITEM_BRACELET $16`'s parent/child lifetime rather
than treating a lift as an immediate tile deletion. Its wall test consumes the
same paired `w1Link.adjacentWallsBitset` edge used by movement collision; a
single solid point is insufficient. The controller then owns the opposite-
direction pull gate, Link animation modes, movement/turning lock, carried
offsets, either-button throw, weight-0 8.8 flight, and interruption cleanup.
`BreakableTileDatabase` remains the authority for whether the active collision
set accepts `BREAKABLETILESOURCE_BRACELET`, the replacement tile, drop,
persistent flags, and stored impact interaction.

Build the lifted graphic before replacing the room metatile. It is a live
`itemMimicBgTile` snapshot, so it must retain position mapping overrides,
animated/dynamic BG tile sources, X/Y flips, and the active room palette while
making BG color 0 transparent in OBJ palette 7. The room layout changes at the
successful lift boundary; the stored interaction is not created until the
thrown tile lands or collides with a wall. Water, lava, and holes run the item
hazard replacement first and suppress that debris.

Native grabbable entities implement `IBraceletInteractableRoomEntity`, but do
not own a second Link item state machine. The shared controller wraps their
accepted lift and release in the same 13-update lift, eight-update throw,
offset, and sound boundaries. The entity continues to own its body-specific
motion and outcome; Pumpkin Head's head/ghost collision is the current example.
Thrown metatiles use enemy/part collision capabilities for damage and continue
flying when the original object-collision table applies damage only to the
target. Their planar collision remains centered on the item's ground-space
`yh/xh`; rendered Z never shifts that rectangle. After lateral and vertical
item motion, the enemy pass separately accepts only target/item `zh` values
within the source's strict seven-pixel range. A landing tile is replaced before
that pass and cannot apply one final airborne hit.

Link's standing-state A-button arbitration checks button-sensitive entities and
`interactWithTileBeforeLink` before allocating an equipped parent item. Chests,
signs, and keyholes therefore retain priority when the Bracelet is equipped to
A. A failed Bracelet pull against an unbreakable wall holds
`LINK_ANIM_MODE_LIFT_3`'s terminal strain frame while retrying the tile probe;
it does not restart the 11-update pull animation.

## Dungeon-specific native objects

Keep a dungeon's native handlers in a typed generated stream when its ordinary
object list contains script or interaction subids whose state machines are not
shared globally. Spirit's Grave uses `spirits_grave_objects.tsv`,
`spirits_grave_enemies.tsv` for its three native boss records,
`spirits_grave_visuals.tsv`, and
`spirits_grave_constants.tsv`. The importer resolves source object order,
predicates, enemy attributes, graphics, OAM, animation loops, text, and timing
constants; runtime code must not reconstruct those records from room IDs or
parse disassembly text.

Merge these records with shared dungeon mechanics and entrance interactions by
their imported `order`. Before-event bosses are gated by their source room flag
before they contribute to `wNumEnemies`; their reward script remains a separate
ordered controller that observes the same live enemy count. Child enemies and
projectiles use explicit spawn records so update-this-frame behavior is not
lost. Boss completion owns the persistent room flag, while the ordered reward
controller owns the Heart Container or miniboss portal.

Common boss initialization arms Link's `LINK_STATE_FORCE_MOVEMENT` only after
the boss's first enemy update. On the next update Link initializes the forced
state; its Ages `$16` countdown then performs 21 standard-speed one-pixel
updates before returning to the standing state. Run that Link-owned movement
before doors and enemies and bypass adjacent-wall collision, so the incoming
shutter observes Link fully inside before closing. Both Giant Ghini and Pumpkin
Head consume this shared entry contract; direct-room validation loads have no
scroll direction and therefore do not synthesize an entry walk.

Completed boss rooms retain a complete enemy-count source even though room flag
`$80` suppresses their before-event boss record. On re-entry, the two enemy
shutter controllers therefore observe zero enemies and run their ordinary
six-update interleaved opening animations; suppressing the boss must not leave
those doors in the fallback state used for genuinely unsupported enemy streams.

Common boss teardown is a chain, not an ordinary enemy deletion. The boss
sets the room-wide Link-collision/menu lock for its 120-update flicker, restores
the saved room music when it creates `PART_BOSS_DEATH_EXPLOSION`, and leaves
that imported 78-update part in `wNumEnemies`. The reward controller clears the
lock only after the explosion releases the enemy count and the source reward
script reaches its enable step. Do not route bosses through the ordinary death
puff/drop producer. Airborne bosses attach the reusable imported `PART_SHADOW`;
its size comes from the parent's raw Z high byte and its visibility alternates
every update.

Pumpkin Head's body and exposed ghost do not share one combat record. The body
resets to eight health each time its head is exposed, while the ghost retains
the enemy record's eight health across every regeneration. Collision mode
`$5e` applies ordinary item damage to both sword collision rows and the
Bracelet proxy; the weight-0 thrown head therefore deals the Bracelet record's
three damage and uses its separate planar radii plus strict Z test. The common
32-update enemy invincibility and `SND_BOSS_DAMAGE` response apply to either
accepted ghost hit. During state `$15`, `objectCopyPosition` copies the active
head's final X/Y into its related body before resetting body Z, so the rebuilt
boss belongs at the landed head rather than the previous body or fleeing ghost.

Spirit's Grave room `4:20` shares one transient puzzle state across its cube,
four flames, light sensor, and trigger sensor. The cube selects from all 30
imported roll/orientation animations and updates the shared color/position only
after a complete 16-pixel roll. After its one-time initialization separation,
the source handles solidity through the cube cell's `wRoomCollisions` `$0f`,
clears that byte during the roll, and restores it at the centered destination;
the runtime must not also apply a continuous entity-radius blocker, which would
stop Link before his adjacent-wall probes select the push pose. The cube's own
20-update push test reads Link's cardinal movement intent, facing, grounded and
item/button state directly. Flame actors apply the cube's current color and bit
7 visibility during construction, after the earlier ordered cube initializes
the shared state, so a room-entry render cannot expose the previous solved
appearance before the first fixed update. Room `4:16` similarly keeps its
button trigger separate from the native 30-update moving-platform spawn script.
This avoids encoding either puzzle as a room-load shortcut and preserves source
ordering.

Top-down `INTERAC_MOVING_PLATFORM` `$79` owns the shared
`wLinkRidingObject`-style support state. It tests Link's point at Y+5 against
the imported strict collision radii before advancing the platform, suppresses
Link's underlying hole/water/lava/conveyor terrain while claimed, and applies
the same `SPEED_80` 8.8 displacement to Link while his ordinary ground state
allows it. Keep the platform and Link fractional coordinates independent from
their floored draw coordinates; reconstructing either movement from a rendered
position loses or doubles half-pixel travel. Room `4:15` uses size subid `$05`
(`$10` by `$10` radii), while room `4:16` spawns subid `$09` (size `$01`,
`$08` by `$10` radii).

The Eternal Spirit remains a room entity until its exact approach predicate is
met, then hands control to `RoomEventController`. The event owns input lock,
dialogue, room/essence flags, `MUS_GET_ESSENCE` during the get text, the later
`MUS_ESSENCE`/energy-swirl cadence, full-screen fades, and the final delayed
warp. The entity owns the separately imported pedestal, animation-3 flickering
glow, collectible, and bead presentation. Clear any room-local background fade
when the transition loads the destination so a source-room effect cannot leak
into ordinary gameplay.

## Shared dungeon entrance interactions

The importer keeps `$12:$00`, `$e2:$01`, and `$7e:$00` placements in one
`dungeon_shared_placements.tsv` stream with their original room-object indices.
`RoomEntityFactory` merges that stream with `dungeon_mechanics.tsv` by the
imported `order` field; do not append either family by type, because doors,
chests, entry handlers, eyes, and portals can share a room and observe one
another's update order. Room `4:e7` retains its source NPC/handler interleave by
inserting the handler after the first construction soldier.

`INTERAC_DUNGEON_STUFF $12:$00` exists for one enabled update only when the room
was entered through the `$ff` whiteout screen warp and Link's Y is at least
`$78`. Initialization clears the Ages `wToggleBlocksState`, `wSwitchState`, and
`wSpinnerState` session bytes, then applies the imported per-dungeon spinner
value. Strict-radius contact shows the imported TX `$0200-$020f` label, records
the death checkpoint through the shared event, and deletes the interaction.

`INTERAC_STATUE_EYEBALL $e2:$01` scans large-room layout bytes from `$ae` down
through `$01` for tile `$ee`. Each child receives a same-update setup at the
tile center minus two Y pixels. Starting on its following update, it recenters,
quantizes `objectGetAngleTowardLink` to the source eight directions, applies the
imported low-nibble Y/X offset, and retains the default animation `$04` OAM.
The direction is represented by moving that one fixed eye sprite around the
statue; the other animation indices belong to `$e2:$00` and must not be selected
for the scanner's `$e2:$02` children.

`INTERAC_MINIBOSS_PORTAL $7e:$00` reads flag `$80` from the imported miniboss
room pair, not from whichever portal room is active. Its initial-overlap state
requires Link to leave before contact can trigger. Fresh contact plays
`SND_TELEPORT`, pins Link at packed position `$57`, rotates his direction every
fourth global update for exactly `$30` updates, and requests the imported basic
destination/fadeout warp to the other room in the pair. The transition
controller remains the owner of the actual room swap and fade.

Active Shovel use keeps parent-item timing in `Player` and delegates the
update-4 child probe to `ShovelController`. The controller reads
`BreakableTileDatabase` source `$06`, normalizes the hit to the metatile center,
applies replacement/drop/effect ordering, and spawns fixed-update
`ShovelDebrisEffect` through `RoomEntityManager`. Do not duplicate shovel dirt
lists or encode room-specific dig coordinates.

Sword-cut grass and bushes follow the same imported breakable-tile metadata.
`CombatController` decodes the effect byte's low nibble as the debris
interaction and bit 4 as its flicker subid, normalizes the spawn to the tile
center, and creates a fixed-update room entity. This keeps its OAM animation,
normal/underwater palette choice, sound, transition offset, scrolling freeze,
and deletion order in the same managed lifecycle as other room entities.

Active Seed Satchel use follows the same parent/child ownership boundary.
`Player` owns `LINK_ANIM_MODE_21`'s eight-update input/movement lock;
`SeedSatchelController` rejects use while a prior seed child remains active,
checks the selected BCD counter, allocates the child through
`RoomEntityManager`, and only then performs `decNumActiveSeeds`.
`EmberSeedEffect` owns `ITEM_EMBER_SEED $20` subid `$00`: the setup-only first
update, signed Link-relative offset, `SPEED_c0` motion, speedZ `-$20`, gravity
`$1c`, ground/hazard landing, item animation, and the `$3a`-update flame. On
expiry it probes `BreakableTileDatabase` with source `$0c` and applies the
imported replacement, drop, room-flag, Gasha-maturity, and solve-sound effects.
Breakable-room actions with bit 7 set use the low-nibble direction to set both
the active room's directional flag and the opposite flag in the neighbor from
the imported dungeon layout. For example, Spirit's Grave tile `$69` uses
action `$8c`: burning room `4:1d`'s left wall sets flag `$08` there and flag
`$02` in room `4:1c`, then plays `SND_SOLVEPUZZLE` and terminates the Ember
child. Do not reduce linked breakable actions to a current-room-only flag.
Cached `OracleRoomData` instances restore their source layout on every entry,
then run the original substitution order: `SingleTileChangeDatabase`,
`StandardTileSubstitutionDatabase`, chest/key-door state, and room-specific
changes. Room flag `$80` therefore restores a directly persistent burnt tree
`$cf` as `$dc`. Visually similar tree `$ce` has no direct breakable-table flag,
but may still be permanent when the room places `INTERAC_MISCELLANEOUS_2
$dc:$08`: that invisible entity snapshots its imported packed position and ORs
its imported mask into the room flags when the tile changes. Room `0:48` watches
position `$68` with mask `$02`; its matching `singleTileChanges.s` row restores
`$3a` on re-entry and after save reload. Unwatched `$ce` tiles remain transient.
Enemy adapters share their accepted hit/death path with the seed
capability; the projectile disables its collision after the first accepted hit
and changes to the flame state. Enemy contact mirrors `COLLISIONEFFECT_BURN`
and `PART_BURNING_ENEMY $12`: contact during either flight or the landed flame
adopts the related enemy, follows it, suppresses its updates and contact, and
resolves the two-damage hit after the part's 59-update counter. A lightable torch
instead consumes the Ember Seed immediately without creating that flame
animation, then lights on its following object update. Keep later Scent,
Pegasus, Gale, and Mystery state machines distinct when they are implemented.

`INTERAC_GASHA_SPOT $b6` is split between room initialization and one native
interaction entity. `GashaSpotDatabase` applies the planted `$f5` sprout below
20 kills or the solid `$4e/$4f/$5e/$5f` 2-by-2 tree from 20 kills onward.
Only an unplanted, exposed `$d2` tile becomes A-button-sensitive; the Discovery
Ring cue occurs on the interaction's first enabled update even while the spot
is still buried. At 40 kills the interaction creates the nut at the source
offset. A sword hit changes its radius, applies speed `$28`, speedZ `-$140`,
gravity `$20`, and aims at Link. From that hit until the tree is gone, Link's
movement/items/sword and ordinary menus remain disabled as by
`DISABLE_ALL_BUT_INTERACTIONS` plus `wMenuDisabled`.

Reward resolution consumes the shared RNG at exactly the source distribution
and ring-tier calls. The first nut forces a tier-3 ring without maturity debit;
later nuts select by the five maturity ranges and spot rank, debit 200, replace
a repeated Heart Piece with a tier-0 ring, and fully heal for an already-owned
Potion while retaining the Potion reward. The held two-hand reward and text
remain interaction-owned until displayed Hearts/Rupees catch up. Then the
tree makes its four metatiles walkable, runs the nine eight-update 4-by-4 BG
shrink frames over the spot-specific grass/dirt/sand source, clears the planted
bit, and writes the imported 2-by-2 ground replacement. Emit the Gasha entity
after placed actors and before the enemy stream; this preserves room `0:7b`'s
three-child-before-Gasha source order. The replacement is transient: the next
ordinary entry resets the cached room to its source layout, restoring the soft
soil and allowing the cleared spot to be planted again.

Active Shield use is a held-input parent, not a one-shot item action. `Player`
retains which equipped button allocated the parent, plays `SND_SHIELD` only on
its state-0 initialization, and writes the effective `wUsingShield` state only
while no other parent item owns Link. Scrolling temporarily lowers the shield
without deleting that parent, so a continuously held button raises it again
after the scroll without replaying the sound. Dialogue, warps, damage, and
cutscene control delete the parent. Collision uses the source per-direction
`wShieldY/X` center and radii before Link's ordinary body rectangle. Supported
enemy projectiles own their resulting bounce state; `Player` owns only the
shield predicate, overlap test, and `LINKDMG_$20` clink.

Sword beams use the same parent/child split. `Player` creates the single
object-capped `ITEM_SWORD_BEAM $27` on the sword animation's bit-5 update or
when the Energy Ring charge counter underflows. `SwordBeamEffect` owns its
setup-only first update, signed direction offset, `SPEED_300` motion, 2-by-2
collision radius, global four-update palette toggle, tile/screen termination,
and flickering `INTERAC_CLINK $81` collision result. `RoomEntityManager`
applies projectile damage before movement on each fixed update and freezes the
beam with the rest of the destination entity set during scrolling.

`PART_ITEM_DROP` spawn records distinguish ordinary stationary enemy drops from
Shovel-created drops. A dug-up drop copies Link's cardinal angle and applies
`SPEED_a0` (0.625 pixels per original update) during its airborne bounce, using
the allow-holes front/current tile probes before movement. Horizontal movement
ends with the bounce; it must not leak into ordinary drops or grounded lifetime
updates.

Object-data opcode `$fa` does not place `PART_ITEM_DROP` directly. It allocates
an invisible `ENEMY_ITEM_DROP_PRODUCER $59`, reserves the packed position and a
killable-enemy index, and snapshots the underlying metatile on its first update.
Only a later tile change deletes the producer and, when Link owns the matching
Bomb or seed treasure, creates the drop for an immediate same-update advance.
The producer never contributes to `wNumEnemies`, but a successful production
marks its transient recent-defeat index so short re-entry cannot repeat it.
Rooms `0:5d` and `0:6d` are the canonical Bomb/Ember cases.

Item drops use `objectCheckIsOnHazard`, so water, lava, and holes do not consume
them while their Z high byte is negative. On the first ground-height hazard
update, water and lava replace the drop with their corresponding splash
interaction at the drop position. Hole disposal remains distinct and must not be
routed through the splash effect.

Common push blocks request `SND_MOVEBLOCK` only after the push delay succeeds,
the source tile has been replaced, and the moving object becomes visible.
Their movement uses the imported Bracelet contract: level 0/1 uses
`SPEED_80` for `$20` updates, while the level-2 Power Glove uses `SPEED_c0`
for `$15` updates unless property bit 5 marks the block heavy.
Their completion event retains the destination hazard type: water/lava create
the splash interaction, while a hole creates `INTERAC_FALLDOWNHOLE $0f:$00`
without changing the hole tile. That interaction requests `SND_FALLINHOLE`,
moves the inherited block position toward the metatile center at `SPEED_60`,
and plays the imported 8/12/12-update terminal animation. Rejected directions,
solid destinations, and interrupted push delays request neither sound nor
effect.

Dynamic blocker collision compares the high-byte pixel coordinates of both
objects, matching `checkObjectsCollided`; fractional 8.8 position bytes must not
stop Link one rendered pixel before contact. Object-side separation helpers may
then replace only the collided coordinate's high byte while retaining its
fractional byte.

Room interaction spawners can produce reusable entities without becoming room
exceptions. `$dc:$07` ground treasures are emitted after placed NPCs and before
portals/enemies in original object order, expose collision through
`ILinkContactEntity`, and use `IRoomEntityLifetime` to disappear only after the
pickup textbox closes. Their room-item bit is checked on every room parse.
The same treasure entity supports source spawn mode `$02`: after its imported
delay, `objectGetZAboveScreen` derives Z from the current gameplay-screen Y
rather than a fixed room coordinate, then shared 8.8 gravity and bounce
metadata drive it to the floor. This is used by both event-created rewards and
room `5:ed`'s Graveyard Key.

`RoomEntityManager` owns the room-local `wActiveTriggers` equivalent and clears
all eight bits before every ordinary room parse or destination preload.
`PART_BUTTON $09` writes the bit selected by subid bits 0-2; subid bit 7 only
chooses reusable versus one-shot pressure. Trigger-door `$1e:$04-$07` records
read the bit selected by their source parameter. Trigger-chest `$20:$00` and
`$21:$17` records retain an imported exact-byte or bit-set predicate rather
than deriving it from the room at runtime. Keep buttons and their consumers as
separate ordered fixed-update entities: interactions observe the prior trigger
value before parts update it, so a pressure change affects a door or chest on
the next update. These mechanics do not depend on save/story predicates.

Small-key doors are tile interactions, not placed `$1e` room objects.
`DungeonKeyDoorController` probes imported tiles `$70-$73` through the same
front-tile push geometry as blocks, centralized in
`InteractableTilePushGeometry`. `nextToKeyDoor` initializes its shared counter
to 20 but decrements it twice per qualifying update, so the key check occurs on
the tenth continuous push. Success consumes exactly one key from the current
dungeon, spawns `INTERAC_DUNGEON_KEY_SPRITE $17`, and sets the directional room
flag on both sides of the neighbor resolved through the imported dungeon floor
layout. Opening uses the same six-update mapping-interleaved, still-solid door
frame as shutters before final tile `$a0`; room initialization substitutes
opened `$70-$73` tiles from those saved flags. A missing key shows TX `$5100`
without changing either room.

Permanent trigger chests request the solve cue and puff on their qualifying
edge, wait 15 updates, then install closed chest tile `$f1`. Retractable chests
install `$f1` immediately, and restore the source room-layout tile when their
exact trigger byte stops matching; both transitions create a puff, but only
appearance plays the solve cue. `ROOMFLAG_ITEM` prevents either controller from
running on re-entry and installs opened chest tile `$f0` at the imported
position, including rooms whose source layout never contained a chest tile.

Button pressure is tile-aware as well as Link-aware. Strict high-byte contact
uses the part's `$02/$02` radii plus Link's `$06` radius and rejects airborne
Link. Any tile other than `$0c/$0d` represents an object holding the button;
the object remains rendered while the underlying reusable button is pressed,
then tile `$0d` is revealed and its `$1c` release counter runs after the object
moves. Press and release both request `SND_SPLASH`.

Enemy-shutter door controllers `$1e:$08-$0b` query
`IRoomEnemyCounterEntity` rather than a
room-specific completion boolean. Combat adapters contribute while alive;
native sentinels such as push-block trigger `$13:$01` contribute from their
state-0 increment through their delayed reset. Enemy object flag bit `$02`
retains the original count-exempt behavior. Import every shared placement, but
allow a shutter to solve only when every active, non-exempt enemy record in that
room has a runtime entity capable of contributing to the count and its object
list has no unresolved before/after-event enemy stream. Keep the shutter
controller itself active so `replaceShutterForLinkEntering` can safely admit
Link even when that completion source is not yet implemented. Retain only the
crossed shutter as open floor so Link can backtrack instead of becoming trapped;
all other shutters remain closed, and no solve state or cue is synthesized from
the incomplete count. Standalone `$13:$01` records paired with incomplete enemy
streams remain inactive for the same reason.

Common combat death creates `PART_ENEMY_DESTROYED`; the factory requests
`SND_KILLENEMY` when that puff is allocated so every supported species shares
one ownership point. Red Zols instead request it with their special
`INTERAC_KILLENEMYPUFF` split. Hazard deaths suppress both death/drop puffs and
the kill cue; a hole is retained separately on the enemy until lifetime removal
requests `SND_FALLINHOLE`, while water/lava remain silent on that path.

`ENEMY_CROW $41:$00` is a fixed-position combat enemy with a species-specific
native state machine. While perched it has no collision and faces Link, using
the source's unsigned inclusive Y=`$30`/X=`$18` approach test. It then rises for
25 updates to Z=`-$06`, enables collision, consumes one shared RNG value for a
`+/-$04` angle offset, and charges at `SPEED_140`; for the first 90 charge
updates it steers one angle step every eight updates. A charge that crosses the
original Y=`$88` or X=`$a8` screen bounds uses `enemyDelete`, so it creates no
death puff, item drop, kill sound, or recent-defeat mark. Rooms `0:5d` and
`0:6d` provide the three currently imported subid-0 records; flock subid `$01`
remains outside this slice.

Script-created combat replacements use the same contracts as placed enemies.
Room `1:38`'s `$96` Moblin interactions are solid animated cutscene actors only
until `moblin_spawnEnemyHere`; each then deactivates and creates an
`ENEMY_MASKED_MOBLIN $20:$00` through `RoomEntitySpawn`. The replacement owns
normal contact/sword/hazard/death/drop behavior and contributes through
`IRoomEnemyCounterEntity`, so both the controller script and the sprout script
observe the shared live `wNumEnemies` equivalent. Projectile children such as
`PART_ENEMY_ARROW $1a` are separate update-this-frame spawn records and never
contribute to that count.

Scrolling placement context also carries Link's final packed destination.
Directional shutter controllers use it with the scroll direction to mirror
`replaceShutterForLinkEntering`: only the crossed shutter is preloaded as open
floor. It remains non-solid while destination entities are frozen and while
Link overlaps the door's combined radii; its shared six-update interleaved
close starts afterward and applies the closed collision only on completion.
This entry path is independent of enemy-completion support. When the count is
complete, as in room `4:06`, the crossed shutter closes only after Link clears
it. That room then counts two ordinary Stalfos plus its `$13:$01` push-block
sentinel; killing both Stalfos restores source block `$1c`, and moving that
block starts the normal delayed two-shutter solve. In an incomplete room, the
crossed route instead remains open for safe backtracking without synthesizing a
solve.

`replaceShutterForLinkEntering` is a layout substitution, not a property of
placed door-controller records. Before destination object parsing, ordinary
layout tiles `$78-$7b` must also compare their encoded direction and packed
position with the scrolling entry context. The matching tile becomes `$a0`;
the corresponding source table row has bit 7 set, so no auto-close controller
is created. Room `4:1d`'s right tile `$79` is the canonical layout-only case:
scrolling left from `4:1e` opens packed position `$5e`, while a direct room load
retains the closed source tile. Minecart shutters `$7c-$7f` use different
replacement tiles and auto-close interactions and remain a separate mechanic.

Do not infer the entry shutter from a room edge alone, because the original
substitution also requires Link's packed row or column to match that door.
After the entry overlap clears, shift a local respawn stored on the shutter
tile in the direction-specific inward offset. A later close on Link's tile uses
the original instant-respawn path (two invisible updates, one-heart damage,
then 16 recovery updates) before solid collision can strand him.

Ordinary random and fixed enemy records without object flag `$01` advance the
source `numKillableEnemies` counter before allocation. Only indices `$01-$07`
are retained. `RecentEnemyDefeats` mirrors the original eight-entry
`wEnemiesKilledList` ring by room ID; combat death marks the entity's bit,
subsequent short re-entry skips that placement before slot allocation and
random positioning, and visiting enough distinct rooms eventually evicts it.
Red Zol split children retain the parent's index while the replaced parent does
not mark it. This state is runtime-only: scrolling and dungeon warps retain it,
whereas standard warp loading to a non-dungeon destination clears it.

See [NPCs and room events](npcs-and-events.md) for deciding whether an imported
interaction remains an ordinary NPC, receives a specialized room-entity
adapter, or is coordinated by `RoomEventController`.

## Required regressions

Room/entity changes should cover the reported room plus a general invariant:
ordered reservations, fixed and random coexistence, terrain rejection, incoming
edge/warp exclusion, RNG consumption, transition freeze, child-update timing,
death/drop results, and re-entry after persistent flags. Use original IDs in
failure messages so a mismatch can be traced directly.
