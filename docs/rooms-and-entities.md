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

Present room pack `$7f` selects Nuun Highlands terrain from the saved animal
companion. The expanded assets retain the room's tileset and identity while
selecting layout group `$00` for Ricky, `$01` for Dimitri, or `$03` for Moosh
(also the unassigned fallback). Room caches distinguish these layout variants,
and destination preloads resolve them through `RoomSession` before scrolling.
For this room pack, ordered object conditions use companion ID minus `$0b`.
An unassigned companion retains the standard underwater/layout-swap condition
modifier, even though its terrain follows the fallback layout.
An object condition spans subsequent opcode runs until the next condition or
pointer/end boundary; a new enemy flag byte does not reset it.

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
Warp requests also distinguish Link's source-transition handler from a direct
scripted fade. Source handlers own the entrance sound; a direct write to
`wWarpTransition2` bypasses that sound. Destination entry handlers do not replay
it. Dungeon floor-stair lookup owns its separate sound before the direct fade.
Warps, scrolls, time travel, and development direct loads are different entry
contexts and require explicit coverage.

Scrolling has separate setup, motion, offscreen row-loading, and cleanup
updates. Clean-US unique-graphics header entry counts are imported from the
ROM because the expanded-tileset disassembly removes those loads. Their
before/after-scroll selector and retained loaded-header identity determine
the extra wait. Link retains his facing, fractional coordinates, parent items,
and damage state; the finisher changes coordinates and the local respawn
without performing a full-warp reset. Live edge checks run once after the
object pass, and destination gameplay resumes after the finishing update.

Tile-warp activation uses imported tile behavior and the original position
windows, not a generic full-metatile overlap. Screen edges use imported warp
rows or dungeon-layout neighbors as appropriate. Preserve the source order of
hazard, object, and boundary checks around a transition.

Source-placed `INTERAC_SPECIAL_WARP $1f:$00` dive routes are imported separately
from tile warps. They activate only while Link is diving inside the original
interaction-plus-Link collision window and use the handler's direct destination
room, packed position, and transition bytes.

Ordinary warp destinations place Link at the exact center encoded by the
imported packed position. Warp arrivals and local hazard respawns record that
position as inactive until Link leaves it, so an anchor on a stair or doorway
cannot immediately warp him again; do not move him to an adjacent metatile.
Full room loads also discard source-room transient effects at the original
interaction-memory clear boundary. Do not carry splashes or similar effects
into the destination; scrolling has a separate outgoing-entity lifetime.

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

The shared `tryToBreakTile` transaction owns breakable-table lookup, special
replacement selection, tile mutation, persistent flags/maturity, solve sound,
and drop selection for every source. Sword, bracelet, bombs, seeds, shovel,
and companions retain only source-specific probe order and debris/interaction
creation. Likewise, bracelet-carried objects share the parent item's held,
release, lateral throw, gravity, and bounce arithmetic while their object
handlers retain landing and destruction states.

Seed Shooter terrain collision uses imported collision-set tables for
unconditional passability, directional cliffs, and tiles that activate seeds
without bouncing. Each seed owns the source byte elevation and last-tile cache;
diagonal probes predict elevation while only the current tile commits it.
Room coordinates and collision remain independent of camera presentation.

Gale Seed capture stays in the item phase; Link owns his spinning pose and
input lock, the map menu owns destination selection, and the transition owner
performs the falling arrival. Enemy gale motion uses the imported collision
mode table and shared RNG. Its silent deletion does not run ordinary enemy
death drops or kill counters; native spawners retain their completion policy.

`RoomEntityManager` creates entities, preserves original update order, routes
contacts, and owns their lifetime. Shared combat, terrain, and interaction
controllers operate through explicit capabilities. Species or native-object
state stays with the entity that owns it in the original.

Ordinary gameplay preserves the source category order: items, enemies, parts,
then interactions. Item collisions resolve in the item phase, so a landed
Scent Seed publishes its target before compatible enemies update, and its
zero-counter update removes that target before the same enemy pass.

Random breakable drops retain their unresolved part subid until the part's
first update. That update checks Maple before drawing RNG, then applies the
room-local digging restriction and shared enemy-slot allocation. A successful
enemy allocation counts toward room completion and starts updating in the
next enemy pass. Slot reservations include source-placed noncombat controllers
and explicitly unsupported placements; deletion releases the slot independently
of the later death-puff room-count decrement.

Enemy death puffs carry the source's room-count and item-drop policies
independently. A no-drop puff skips drop selection entirely, including its
RNG consumption; an uncounted defeat leaves the room counter and recent-defeat
mark unchanged while still advancing the global kill counters.

The live `w1Companion` slot has one runtime owner shared by rideable animal
companions, the minecart, and the raft. A mounted owner, rather than Link, supplies the
screen-transition position and transfers from the outgoing entity set after
scrolling. Dismount writes the separate live remembered-companion fields;
their disk-backed copy changes only when the death-respawn checkpoint is
recorded, matching the original save boundary.

The raft's waiting interaction supplies terrain support before its smaller
mounting window allocates the special-object slot. Allocation occurs in the
interaction pass; the next update runs the raft before Link and his parent
items. The later entity pass must not advance it a second time. Link keeps
his own facing and fractional coordinates while the raft supplies the mounted
collision center and high-byte position offset. Scrolling updates the local
respawn and last mount point without changing the remembered raft position;
mounting and dismounting own that separate write.

Waiting companions retain their native animation and hazard updates. Hazard
recovery preserves whether Link was mounted; an unmounted animal cannot take
over Link or apply riding damage. Mounting uses the shared ordinary-Link
vulnerability, swimming, grabbing, airborne, and mount-lock gates.
Companion contact restrictions are read through the entity restriction owner,
including Moosh's charged-stomp protection through its recovery animation.

Mounted-animal Link presentation is not an independent Link animation. The
`SPECIALOBJECT_LINK_RIDING_ANIMAL` owner copies the low six bits of
`w1Companion.animParameter` every update and uses the companion direction for
facing. `func_410d` supplies a companion-specific object offset: Ricky uses
`$0000`, so Link and Ricky share exact XYZ coordinates and their OAM layouts
compose the visible pair; Moosh uses a Y offset of `-$0e` when facing
vertically and `-$10` when facing horizontally.
Enemy contact follows `wLinkObjectIndex`: mounted animals supply the collision
center with `$06` radii on both axes, independently of riding-Link's offset.
The enemy/part Z minus riding-Link Z must fall in the unsigned-byte window
`[-$07, +$06]`. XY overlap likewise preserves the included negative edge and
excluded positive edge of the original summed-radius comparison.
Runtime companion animation order therefore remains authoritative for both
sprites, and the mounted companion owns A/B before Link's ordinary equipped
items can create a conflicting pose. A cutscene response pose is not
necessarily the mounted handoff direction: Moosh's first rescue meeting enters
the left-facing ride pose even when the preceding angle-to-Link response faced
another direction. A B press only enters companion state `$06`; its next
update copies the companion position, records that Y/X and direction as Link's
local hazard respawn, and starts the ordinary jump/landing sequence. Although
`setLinkMountingSpeed` writes `SPEED_80` and `direction*8`,
`companionDismount` immediately overwrites Link's object angle with `$ff`, so
the dismount itself is vertical. After landing, Link must walk outside the
strict `c=$09` Manhattan radius before state `$01` permits another mount.

Charge flashing applies OBJ palette 2 to both companion and riding-Link frames
in global-frame-counter bit-2 bands after the companion's source threshold
(`$1e` for Ricky and 40 updates for Moosh).
Ricky's special-object animation importer replays the live VRAM tile map from
`specialObject0bGfxPointers`. Each OAM cell resolves to its absolute source
tile, so partial graphics loads retain untouched cells from the preceding row
and source offsets beyond `$0fff` cannot wrap to unrelated graphics.
His state `$02/$05/$07` traversal keeps the original separate counters and
wall-crossing masks: paired `$03`/vine-top probes select upward cliffs,
`cliffTilesTable` selects downward cliffs, and
`rickyStopUntilLandedOnGround` clears the screen-transition lock before the
remaining airborne landing phase.
An airborne Moosh checks the original `y+$05` hazard probe before horizontal
movement. Water freezes his position and vertical speed for `$3c` updates,
creates the copied-position exclamation `$20` pixels above him with SND_CLINK,
then resumes gravity on the exact zero update. Grounded companion hazards use
the same probe. Hole/lava entry drags both mounted sprites toward the metatile
center before the falling animation; grounded water starts its drowning
animation immediately. Completion moves the mounted pair to the local safe
position (falling back to the last mount point if necessary) and applies the
companion hazard damage/invincibility state.
The scrolling finisher stores the mounted companion's destination high-byte
coordinates as both Link's local respawn and the shared last-animal mount
point. If the local point later fails the companion collision/hazard checks,
`companionRespawn` copies that shared point without a second validity check.

Room-event destination preload must consult both active and outgoing entity
sets before creating a waiting companion. The retained outgoing companion is
the authoritative live owner and transfers into the destination set when the
scroll completes; a room event must never create a second owner meanwhile.

Invisible companion-tutorial interactions retain their original two-update
initialization. They show text only when the required companion owns Link's
mounted state, then watch the imported directional boundary and set the
persistent `wCompanionTutorialTextShown` bit. Equality does not count when the
source comparison is strict, and a set bit deletes the controller on re-entry.
Placed companion barriers remain separate fixed entities at their source
object-stream position. They wait in state zero until Link is mounted, select
the companion state byte and warning text by live companion ID, then clamp the
shared companion owner at the imported strict boundary.

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
