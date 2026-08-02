# Rooms and entities

## Room identity and geometry

A room is identified by group plus hexadecimal room ID. Small rooms are 10 by
8 metatiles (160 by 128 pixels). Large-room storage is 16 by 11 metatiles with
a 16-byte row stride; only 15 by 11 are playable and the last column is
padding.

`RoomSession` owns active identity, loaded data, and dungeon-layout context.
Groups `6` and `7` retain side-scrolling identity while their source tilesets
and placed objects alias groups `4` and `5`. Dungeon neighbors come from
imported floor layouts, never room-ID arithmetic.

Gameplay positions remain original room/world coordinates. Camera and
transition offsets are presentation. Preserve byte and 8.8 fractional state
through movement and transitions where the source does.

The ordinary large-room camera moves each origin component by one high-byte
pixel per original update toward its clamped focus target. Textboxes hold that
position. Only traced room-load/reset paths place the camera at its target
immediately.

## Room lifetime and transitions

`RoomTransitionController` owns scrolls, warps, camera, destination placement,
and transition fades. `RoomEntityManager` owns the active and outgoing entity
sets.

During a scrolling transition:

- destination data and entities may be preloaded;
- ordinary destination entities and room events remain frozen until the scroll
  completes;
- retained outgoing entities also remain frozen;
- transition-safe native behavior runs only when explicitly matched to the
  original update mask;
- logical positions remain in their rooms while draw offsets move them.

Do not treat preload as room entry. Entry counters, RNG, events, music,
checkpoints, and persistent mutations occur only at their traced boundary.
Warps, scrolls, time travel, and development direct loads are different entry
contexts and require explicit coverage.

Tile-warp activation uses imported tile behavior and the original position
windows, not a generic full-metatile overlap. Screen edges use imported warp
rows or dungeon-layout neighbors as appropriate. Preserve the source order of
hazard, object, and boundary checks around a transition.

Ordinary warp destinations place Link at the exact center encoded by the
imported packed position. Warp arrivals and local hazard respawns record that
position as inactive until Link leaves it, so an anchor on a stair or doorway
cannot immediately warp him again; do not move him to an adjacent metatile.

## Ordered room objects and RNG

The importer produces one source-ordered object stream. Parse it in order and
retain a shared reservation set so conditional objects, random placements,
enemies, and later objects observe the same occupancy and RNG history as the
original.

The placement buffer is regenerated once per real room parse with the global
game RNG and the original 256 calls. Do not use `Random.Shared`, a per-enemy
generator, sorted collections, or a separate placement pass. Destination
preload and re-entry must consume RNG only when the original does.

When adding an object kind, trace:

- pointer/table aliases and surrounding source order;
- creation conditions and room/save flags;
- coordinate encoding and placement exclusions;
- state-0 versus later-update behavior;
- global RNG calls, including rejected candidates;
- whether created children update later in the same object pass;
- deletion, room-change, transition, and re-entry behavior.

## Entity ownership

Dynamic room content implements the narrow capabilities needed by its shared
systems: fixed update, presentation, collision/contact, combat, interaction,
transition offset, or explicit native hooks. Do not grow one universal entity
base class or infer behavior from a node name.

`RoomEntityManager` creates entities, preserves original update order, routes
contacts, and owns their lifetime. Shared combat, terrain, and interaction
controllers operate through explicit capabilities. Species or native-object
state stays with the entity that owns it in the original.

The live `w1Companion` slot has one runtime owner shared by rideable animal
companions and the minecart. A mounted owner, rather than Link, supplies the
screen-transition position and transfers from the outgoing entity set after
scrolling. Dismount writes the separate live remembered-companion fields;
their disk-backed copy changes only when the death-respawn checkpoint is
recorded, matching the original save boundary.

Mounted-animal Link presentation is not an independent Link animation. The
`SPECIALOBJECT_LINK_RIDING_ANIMAL` owner copies the low six bits of
`w1Companion.animParameter` every update and uses the companion direction for
both facing and the source Y offset (`-$0e` vertically, `-$10` horizontally).
Runtime companion animation order therefore remains authoritative for both
sprites, and the mounted companion owns A/B before Link's ordinary equipped
items can create a conflicting pose. A cutscene response pose is not
necessarily the mounted handoff direction: Moosh's first rescue meeting enters
the left-facing ride pose even when the preceding angle-to-Link response faced
another direction. Dismount copies the companion position,
then launches Link at `SPEED_80` along `direction*8` with the ordinary jump and
landing sounds before reopening the distance-gated mounting state. Bit 7 of
`wLinkInAir` still permits that arc through walls; if its fixed endpoint is
embedded, the runtime retains the furthest collision-free point on the same
arc so Link cannot become immobile.

Charge flashing applies OBJ palette 2 to both companion and riding-Link frames
in global-frame-counter bit-2 bands after the source 40-update threshold.
An airborne Moosh checks the original `y+$05` hazard probe before horizontal
movement. Water freezes his position and vertical speed for `$3c` updates,
creates the copied-position exclamation `$20` pixels above him with SND_CLINK,
then resumes gravity on the exact zero update. Grounded companion hazards use
the same probe. Hole/lava entry drags both mounted sprites toward the metatile
center before the falling animation; grounded water starts its drowning
animation immediately. Completion moves the mounted pair to the local safe
position (falling back to the last mount point if necessary) and applies the
companion hazard damage/invincibility state.

Room-event destination preload must consult both active and outgoing entity
sets before creating a waiting companion. The retained outgoing companion is
the authoritative live owner and transfers into the destination set when the
scroll completes; a room event must never create a second owner meanwhile.

Invisible companion-tutorial interactions retain their original two-update
initialization. They show text only when the required companion owns Link's
mounted state, then watch the imported directional boundary and set the
persistent `wCompanionTutorialTextShown` bit. Equality does not count when the
source comparison is strict, and a set bit deletes the controller on re-entry.

An actor may have separate logical and presentation state. Collision, room
flags, terrain queries, and AI read logical state; OAM/camera/transition code
derives presentation without writing it back. Same-update spawns and removals
must match their original object-slot ordering.

Player A-button routing is centralized by `InteractionController` and its
registered targets. A feature publishes an interaction target with explicit
priority and lifecycle ownership; it does not add another independent input
scan or controller override chain. See [NPCs and events](npcs-and-events.md)
for choosing ordinary NPC, linked interaction, or room-event ownership.

## Adding a room mechanic or entity

1. Find every source placement, dispatch row, handler, table, and caller.
2. Write down the original owner, update slot, creation predicate, coordinates,
   counters, arithmetic, RNG, collision, sounds, flags, and teardown.
3. Extend the owning importer stage if any of those facts are not generated.
4. Reuse shared capabilities only where semantics actually match; keep the
   source-specific state machine narrow.
5. Register creation in the ordered object path without introducing a second
   parse or reservation pass.
6. Validate the real imported room plus focused branches: first update, timing
   boundaries, contact/combat, scrolling preload, warp entry, deletion,
   re-entry, RNG aftermath, and persistence as applicable.

Preserve hexadecimal group, room, object, interaction, and sound IDs in
diagnostics and validation failures. Put one-off constants beside the relevant
importer/runtime code and regression, not in this guide.
