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

Side-view water state is owned by Link. Its terrain probes sample the current
position and eight pixels below it; `wLastActiveTileType` names the latter
probe, not a previous update. Swimming input, parent-item movement locks, and
the resulting velocity run in the original order. Water alone does not select
Mermaid Suit item graphics: the per-update equipment/tileset signal controls
that choice when the parent animation initializes. Swimming bubbles belong to
the later interaction pass, including their separate shared-RNG consumption.

The ordinary large-room camera moves each origin component by one high-byte
pixel per original update toward its clamped focus target. Textboxes hold that
position. Only traced room-load/reset paths place the camera at its target
immediately.

Temporary item helpers can own camera focus independently of Link's position.
Their snapshots and lifetime belong to the item controller; the transition
controller remains the only camera writer. A helper created in an earlier
item slot initializes in its later reserved slot during the same update, and
releases focus there when its weapon disappears.

## Room lifetime and transitions

`RoomTransitionController` owns scrolls, warps, camera, destination placement,
and transition fades. `RoomEntityManager` owns the active and outgoing entity
sets.

A native object freeze can leave the interaction phase running while holding
Link, item parents, projectiles, enemies, parts, and companions. Preserve that
distinction from an input lock: frozen item state must resume after release,
and ordinary interactions must still advance when their source mask allows it.
The text/interaction-disable dispatcher also runs interactions whose native
state remains zero. Stateless puzzle checks retain that eligibility on every
update; initialization must not silently turn them into ordinary frozen actors.

Linked mechanisms consume live shared state in interaction order. Lever pull
distance belongs to the runtime WRAM bytes and clears on room reload; lava
controllers and sliding blocks read those bytes directly. The Bracelet parent
owns the imported pull/rest animation gate and Link's held-item collision
mask. The lever applies movement through Link's wall resolver, then publishes
its high-byte distance for later interactions in the same update.

Top-down moving-platform scripts are keyed by dungeon and script index; the
same raw subid can select a different route in another dungeon. Their shared
rider is cleared after Link consumes the previous update's support, then the
first touching platform claims him in interaction order. The instrument
sentinel also supplies support while preventing a platform claim. Platform
movement carries Link through his wall resolver during ordinary airborne
updates, and the Feather can jump from a platform without treating it as a
mounted companion or minecart.

Ordinary top-down jumps retain their takeoff velocity while rising. After the
gravity update makes vertical speed nonnegative, the shared velocity helper
steers and accelerates or brakes before applying movement. Releasing direction
retains airborne momentum; item movement locks do not replace that source
input path. Forced mount and ledge trajectories retain their own rules.

Dungeon static-object lists share one imported dungeon-indexed table. Dungeon
entry replaces the shared WRAM static-object buffer; ordinary room changes
restore parked carts from that buffer. Boarding transfers a cart to the shared
companion slot, and dismounting returns it to the first free static slot. The
room factory selects these records by dungeon metadata, not room-ID ranges.

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

Vertical edge warps carry the crossed edge's direction into the source
transition. The exit handler sets Link's facing and movement from that value;
his incoming swim, attack, or ladder facing is not the transition direction.

Time travel retains its source interaction update mask, then resumes room
objects during the destination Link state machine while contact and NPC
pushing remain disabled. Its object clock stays continuous across the era
change. Landing checks use the imported invalid-tile and restricted-room
tables, the original paired wall probes, and NPC short-position reservations.
A failed arrival runs the separate return sequence, restores the source's live
terrain, preserves existing destination visit flags, and does not create a
return portal. Restricted-room arrival skips ordinary object parsing and its
RNG consumption. Successful completion updates Link's local hazard respawn;
it does not change the death-respawn checkpoint.

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

Energy swirls allocate from the shared PART pool and stop at the first failed
allocation. Their native enabled bit keeps them updating during text and
object freezes. The creating script and the parts share one WRAM deletion
signal; setting it does not delete objects until their next dispatch. Circle
placement replaces coordinate high bytes while retaining existing fractions.

An Essence's pedestal and reserved glow have separate lifetimes and drawing
priorities. The reserved glow updates before its dynamic parent and remains
eligible during dialogue. Its position therefore uses the parent's preceding
high coordinate bytes. Collecting an Essence suppresses its next appearance
and glow while retaining the pedestal's collision.
Dynamic interaction allocation scans `$d2` through `$df`; the reserved `$d0`
and `$d1` slots are not available to ordinary effects or actors.

The importer produces one source-ordered object stream. Parse it in order and
retain a shared reservation set so conditional objects, random placements,
enemies, and later objects observe the same occupancy and RNG history as the
original.

The placement buffer is regenerated on each real room parse with the global
game RNG and the original 256 calls. Native calls to `generateRandomBuffer`
also overwrite that same buffer; only room parsing resets its placement
cursor. Copies to shared WRAM scratch storage belong to `OracleRuntimeState`,
so concurrent users observe the original buffer lifetime. Do not use
`Random.Shared`, a per-enemy generator, sorted collections, or a separate
placement pass. Destination preload and re-entry must consume RNG only when
the original does.

Parameterized enemy opcode `$09` retains its `var03` byte in the ordered
stream. It allocates a counted enemy without reserving a tile, advancing the
killable-enemy index, or consulting recent defeats. Its species handler owns
the parameter's meaning; it is separate from placement flags.

Enemies created by native interactions also require imported construction
provenance in `native_enemy_spawns.tsv`. They use the shared enemy-slot allocator
and species handler without replaying room placement or consuming placement
RNG. Their absence from positioned object rows does not make their handler
unused.

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

Pegasus Satchel activation consumes its seed in the parent handler and keeps
the timer in session WRAM. Link advances that timer before ordinary item use
and captures the boosted target speed at takeoff. The reserved dust item
updates after ordinary seed children, retains its two alternating cloud slots
across scrolling, and freezes with the item phase during dialogue and scrolls.
It does not contribute an ordinary seed projectile or an item-use input lock.

Native seed collision receivers use the projectile's live collision type and
original imported attributes separately. Mystery selects a collision type at
creation and retains its own damage until the hit is resolved; only then may
it reload the selected seed's attributes and graphics. Native receivers run
in the post-object collision pass. A hit updates the enemy immediately and
leaves an item signal for the following item update; dialogue freezes that
signal, and room replacement discards it. The first overlapping item ends
that enemy's scan even when its effect changes neither object, so Link contact
is skipped for that pass only. Effects that preserve the item's collision bit
allow it to hit later enemy slots in the same pass. Legacy seed receivers
still use their existing collision phases. Boss combat descriptors retain the
room sound callback for hit effects as well as movement and death sounds.
Native receivers also use imported active-collision masks; a disabled entry
does not consume the enemy's item scan. Gale's first enemy update consumes
the collision signal before advancing its capture motion.
The native collision scan visits enemy slots before part slots. Native part
receivers test projectile and thrown-object geometry after movement; a sword
beam collision leaves a signal for its next item update. Orbs retain their
own palette while publishing toggle bits to the shared runtime state; moving
orbs also retain their script position. Stationary orb initialization changes
the logical layout and collision buffers while preserving the floor image and
underlying layout. Orb chest scripts observe those bits in the following
interaction phase and retain the source puff delay before changing the tile.

The Pegasus Shooter projectile uses its own imported collision graphics and
keeps its animation after a stun hit. Legacy stun receivers still resolve its
collision before movement and the enemy update, using the source's asymmetric
byte-height interval.
Entering the collision effect consumes that item update; the effect's first
ordinary animation update happens on the next dispatch. The enemy stun and
projectile lifetime then advance independently.

The shared enemy stun motion preserves fractional position bytes during
shaking and applies the original wrapped height gates, 16-bit gravity, and
bounce comparison. Species dispatch still owns status priority and resumes
normal movement only on the update following stun-counter zero.

Gale Seed capture stays in the item phase; Link owns his spinning pose and
input lock, the map menu owns destination selection, and the transition owner
performs the falling arrival. Enemy gale motion uses the imported collision
mode table and shared RNG. Its silent deletion does not run ordinary enemy
death drops or kill counters; native spawners retain their completion policy.

`RoomEntityManager` creates entities, preserves original update order, routes
contacts, and owns their lifetime. Shared combat, terrain, and interaction
controllers operate through explicit capabilities. Species or native-object
state stays with the entity that owns it in the original.

Enemy terrain movement preserves the source's movement-result flag separately
from displacement. A fast wall slide can change coordinates while reporting a
blocked charge; small velocity components use the original high-byte carry and
unsigned low-byte thresholds. Species consume that result to select their
recovery state and RNG calls. Coordinate additions retain wrapping 8.8 words.

Ordinary gameplay preserves the source category order: items, enemies, parts,
then interactions. A landed Scent Seed publishes its target in the item phase
before compatible enemies update, and its
zero-counter update removes that target before the same enemy pass.
Enemy dispatch visits live slots in ascending order. A child allocated into a
later slot initializes in that update; a reused earlier slot waits for the
next update. Spawners retain their slot until the source deletes them, and
multi-object encounters check the shared capacity before creating children.
Source interaction effects publish animation parameters in the interaction
pass; linked enemies observe them on the following enemy pass before the
effect deletes itself. Their allocator belongs to the room entity manager,
separately from enemy and part capacity. Native slot registration is explicit:
logical controllers sharing an update phase must not consume object slots.
Source object-page references resolve through the manager's current slot
occupant, including reuse. A cross-object health/collision write belongs to
the receiving object's state machine: zero health does not universally mean
immediate deletion. State-zero property loading can overwrite an earlier
write; initialized handlers retain their source-specific status behavior.
Each object category samples dialogue state on entry, so an enemy opening
text freezes already initialized parts and interactions later in that update.
Boss shadows and death explosions occupy part slots; an explosion's terminal
update releases the room enemy count before reward interactions run.

Items with a post-object handler run that handler after interactions and
before the camera update. This pass still runs when dialogue freezes an
initialized item's ordinary state machine. Keep parent lifetime, child
updates, post-object drawing and the later collision pass distinct: a child
deletion is observed by its parent on the following Link update.
Native melee collision owners defer overlap and height checks until that
later pass. Damage and invincibility therefore follow enemy movement, and
the sword parent consumes contact and recoil on its next update. The scan
includes enemies allocated after the weapon's ordinary update. Pending
requests belong to the entity manager and expire on cancellation or room
replacement. Legacy combat owners still use their existing collision path.

Switch Hook enemy eligibility and collision effects are separate imported
tables. The item owns exchange timing and position snapshots; a compatible
enemy owns its held substates, altitude, and release behavior. During flight,
compatible enemy contacts wait for the later weapon collision pass, where an
accepted item collision skips that enemy's Link contact. Cancelling the item
releases a held enemy through its native falling state instead of directly
restoring ordinary movement.

Dungeon switches retain pending Switch Hook contacts until their next part
update. That update advances the signed lockout before toggling the shared
switch byte, so later rail interactions see the change in the same update.
Dialogue preserves both the pending contact and its counters. Part collision
masks and effects are imported separately from enemy collision profiles.

Flying hooks and sword beams share the original item tile-passage state:
cached tile identity and accumulated cliff elevation belong to each item.
A solid movement tile can still permit item passage through its imported
passable-tile or directional cliff rule.

Enemies that react to `wLinkUsingItem1`'s high nibble observe parent-item
animation starts, not the duration of an attack. Link publishes starts using
the imported parent animation flags, clears them on the next ordinary item
update, and suppresses cancelled parents. A gameplay pause preserves that
signal together with the item and enemy states it freezes.

Sword-enemy blades occupy the shared part pool. Their enemy remains in state
zero if allocation fails. The enemy pass publishes its guarding collision
mode; the following part pass positions the invisible blade and transfers
pending blade recoil to its parent after advancing part invincibility. The
blade retains its recoil bytes; the enemy advances the copied counter.
Collision tests in the next item pass
consume that published state rather than recomputing facing during the hit.
The blade's collision gate is independent of the body's collision-enable
bit, which a Switch Hook exchange clears. Its part handler follows the
parent's high XY coordinates without inheriting the body's lifted Z.

Random breakable drops retain their unresolved part subid until the part's
first update. That update checks Maple before drawing RNG, then applies the
room-local digging restriction and shared enemy-slot allocation. A successful
enemy allocation counts toward room completion and starts updating in the
next enemy pass. Slot reservations include source-placed noncombat controllers
and explicitly unsupported placements; deletion releases the slot independently
of the later death-puff room-count decrement.

An `enemyReplaceWithID` conversion retains the original enemy slot, object
order, room-count policy and defeat index. It clears fractional position and
starts the replacement's initialization on the next enemy dispatch, without
emitting a death outcome. Linked parts validate the identity of their target
before restoring temporary state; an old burning-enemy part cannot write its
saved health into the replacement.

Enemy death puffs carry the source's room-count and item-drop policies
independently. Enemy death allocates the puff before releasing the enemy
slot, so its first animation update runs in the later part pass. The terminal
part update releases its count before room-clear interactions run. A random
drop replaces the same part slot, keeps only coordinate high bytes, and
initializes on the next part pass. A no-drop puff skips drop selection and
its RNG consumption. If the part pool is full, the source leaves the enemy's
count unreleased; the room manager retains that count until room loading,
without retrying the puff or playing its kill sound. An uncounted defeat
leaves the room counter and recent-defeat mark unchanged while still
advancing the global kill counters.

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
NPC passage also reads the shared player-world restriction view: an active
interaction or transition can permit passage for its lifetime without changing
NPC collision geometry. Releasing that owner restores ordinary blocking.

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
