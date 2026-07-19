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
- Do not clear reservations between Keese, Octoroks, Zols, Gels, or another
  species group.

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

See [NPCs and room events](npcs-and-events.md) for deciding whether an imported
interaction remains an ordinary NPC, receives a specialized room-entity
adapter, or is coordinated by `RoomEventController`.

## Required regressions

Room/entity changes should cover the reported room plus a general invariant:
ordered reservations, fixed and random coexistence, terrain rejection, incoming
edge/warp exclusion, RNG consumption, transition freeze, child-update timing,
death/drop results, and re-entry after persistent flags. Use original IDs in
failure messages so a mismatch can be traced directly.
