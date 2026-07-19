# Rooms and entities

## Room geometry and identity

Small room layouts are 10 by 8 metatiles, or 160 by 128 pixels. Large room
layouts use a 16 by 11-metatile storage grid with the original 16-byte row
stride; only 15 by 11 metatiles (240 by 176 pixels) are playable, and column 16
is padding.

A room is identified by group and hexadecimal room ID. Group aliases in save
tables do not make their runtime rooms interchangeable. Dungeon neighbors come
from imported dungeon floor layouts, not room-ID arithmetic. `RoomSession` owns
the active identity and must be used for neighbor and layout resolution.

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
| `IRoomBlocker` / `ITalkTarget` | Collision or interaction capability |
| `IOrdinaryNpcEntity` | A placed NPC eligible for live imported save-predicate refresh |
| `IPlayerRestriction` | Native interaction-owned sword and/or movement input suppression |
| `IRoomEntityLifetime` | Completion and final spawned effects |
| `IRoomEnemyCounterEntity` | A live combat enemy or native puzzle sentinel contributing to `wNumEnemies` |
| `IRoomKillTrackedEnemy` | A source enemy's transient `$01-$07` killed-list index and whether completion marks it |
| `IRoomSaveStateEntity` | Refresh from changed live save state |

Shared combat, terrain movement, vertical motion, and animation components may
remove mechanical duplication. Species state machines stay separate so their
counter order and branch behavior remain traceable to the source. Spawn records
state whether a child updates in the creation frame; preserve that distinction.

Active Shovel use keeps parent-item timing in `Player` and delegates the
update-4 child probe to `ShovelController`. The controller reads
`BreakableTileDatabase` source `$06`, normalizes the hit to the metatile center,
applies replacement/drop/effect ordering, and spawns fixed-update
`ShovelDebrisEffect` through `RoomEntityManager`. Do not duplicate shovel dirt
lists or encode room-specific dig coordinates.

`PART_ITEM_DROP` spawn records distinguish ordinary stationary enemy drops from
Shovel-created drops. A dug-up drop copies Link's cardinal angle and applies
`SPEED_a0` (0.625 pixels per original update) during its airborne bounce, using
the allow-holes front/current tile probes before movement. Horizontal movement
ends with the bounce; it must not leak into ordinary drops or grounded lifetime
updates.

Item drops use `objectCheckIsOnHazard`, so water, lava, and holes do not consume
them while their Z high byte is negative. On the first ground-height hazard
update, water and lava replace the drop with their corresponding splash
interaction at the drop position. Hole disposal remains distinct and must not be
routed through the splash effect.

Common push blocks request `SND_MOVEBLOCK` only after the push delay succeeds,
the source tile has been replaced, and the moving object becomes visible.
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
front-tile push path as blocks. `nextToKeyDoor` initializes its shared counter
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
