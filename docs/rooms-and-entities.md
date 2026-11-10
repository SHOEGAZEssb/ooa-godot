# Rooms and entities

## Room identity and geometry

`RoomSession` owns active room identity, data, layout state, and dungeon context.
Dungeon layout admission publishes the imported return room to live
`wDungeonWallmasterDestRoom`. Link's grab state requests the falling return warp
after the hand's terminal animation signal; the entity manager dispatches it
after the object pass through the existing transition owner.
Rooms use a group plus hexadecimal room ID. Small rooms are 10 by 8 metatiles
(160 by 128 pixels); large-room storage is 16 by 11 with a 16-byte row stride,
but only 15 columns are playable. Dungeon neighbors come from imported floor
layouts, never room-ID arithmetic. Side-view groups retain their identity even
when source tilesets and objects alias another group.

Full room reload and scroll arrival clear their original global memory ranges
through the live runtime owner. Resolve both boundaries from the selected
game's imported symbols; equal source labels have different physical addresses
and spans in the two builds. Full reload also selects the native Link page and
camera target. Item and entity teardown remains with their respective owners.

Room-specific allocation runs through `RoomInitializing`, before the placed
object stream. `IRoomInitializationEvent` keeps that event alive through the
later entry notification without cancelling or allocating it twice. Allocation
failure preserves earlier source writes. The shared object-program policy
imports each game's load-flag initialization and completion masks; completed
ordinary object passes apply the source `wScrollMode` gate. Room parsing must
not erase a room-specific handler's digging prohibition.

Ordinary root admission also belongs to that policy. A disabled Ages root
preserves placement scratch, the recent-room list and the shared random buffer;
Seasons admits its root unconditionally. Static-object content follows the
ordinary parse and is independent of that gate. Legacy typed placements retain
compiled instruction bindings, so pointer and condition execution selects their
constructors; a room-name convention does not establish which rows execute.

Ages' Strange Force return replaces normal initialization before room code,
remembered actors, the placed stream and static parsing. Its checked `$7c`
interaction allocation can fail without admitting any of those later phases.
The interaction owns its state-zero cue and subsequent always-update wave;
the shared session binds that wave to the room presenter. This native branch
does not exist in Seasons.

Static-object state has one shared owner over the selected build's native
buffer. Dungeon-entry initialization loads the ordered generated list; ordinary
room parsing only reads the retained records, after the placed object stream.
Capacity is checked in each record's native object pool before content is
constructed. A depleted buffer is not repopulated by room parsing. Static
load/clear leaves the independent special-object slot intact; actor-side save
retains the record's final two bytes, while related-object deletion unlinks the
actor and clears all eight bytes. Native byte-wrapped parser fallthrough is
retained; a matching raw alias without a construction binding diagnoses at its
actual address.

The entity owner runs content initialization allocations before the parser clears
placement scratch and generates its random buffer. Sea effects, remembered
companions and Maple belong to that phase; placed content follows it. State zero
still belongs to the later eligible object update. Ages' companion and Maple
load masks are imported from `initializeRoom`; Seasons calls both routines
without those masks. Disabling Maple's check preserves its prior presence flag.

Remembered-companion matching and native header/coordinate writes belong to
the shared initializer. Pack data selects Seasons' saved mount-point copy and
Ages' raft interaction request. Interaction capacity is checked before content
construction; ordinary companion birth uses the independent special-object
slot. Active-body presentation restoration does not depend on the remembered
load mask. Unknown bodies and unsupported retained states diagnose at the
content binding after the native writes, without silently restarting state zero.
Saved mount-point changes affect the live file image; explicit save flows own
disk writes.

The waiting raft interaction and mounted special object are separate allocations.
Mounting keeps the interaction in state `$02` until its next eligible visit;
it does not transfer that slot to the special object. Dismounting retains D1
in retry state `$03` until a checked interaction allocation succeeds. The shared
live slot walk initializes that child later in the same update. Native animation
cursors and byte counters remain in their bound slots; a zero dismount counter
can wrap to `$ff` when the source has not executed its idle reset.

Minecarts likewise keep independent interaction, special-object and invisible
item allocations. Their source dismount deletes the special object even when
the replacement interaction cannot allocate. Collision-item creation retries
before the cart's own update gates, while its unconditional post pass follows
interactions and copies high XYZ bytes only. Enabled `$03` items retain their
physical slots through scrolling. These rules belong to the shared allocators
and phase dispatcher; presentation does not transfer an allocation between pools.

Maple admission is shared engine code backed by each pack's compiled location
bitmaps and raw companion selectors. It reads live save/runtime bytes, applies
the native group, slot, ring and kill-counter gates, and writes only the native
header and high coordinates. Ages' past group bypasses the slot check; replacing
a native identity retires its previous presentation without clearing successor
bytes. Encounter content is a separate factory binding. Unknown native selector
targets and unbound retained encounter states produce source-aware diagnostics.
Re-admission from enabled `$02` to `$01` transfers a retained presentation into
the incoming room while preserving its state and visibility through scrolling.
The shared gameplay session subscribes once to enemy-death counter publication
and removes that subscription on disposal.
Both games bind encounter and scattered-item construction through the common
factory using selected-pack assets. Their trade interactions retain separate
owners: Ages' book checks treasure allocation and updates through text, while
Seasons' egg freezes under text and grants its doll directly. Random ring tiers
load independently of Gasha placements and growth data.

Seasons world layouts, graphics programs and live palettes belong to the room
catalog; its opening sequence consumes that catalog. Admission retains the native
group and dungeon index. Rows requiring custom graphics destinations remain
explicitly unsupported until their renderer is implemented.

Native byte-indexed layout reads retain the cleared padding through `$cfbf`.
Offsets `$c0..$ff` read the live script bytes at `$cfc0..$cfff` through the
runtime state owner. Terrain drawing uses this view when an actor moves beyond
the room; validated room-storage access remains strict.

Tile types and object hazards come from each game's generated collision-mode
pointer streams. Their mode numbers and tile meanings differ between games;
the room's asset pack selects both lookups. Preserve source pair order and
first-match semantics, including duplicate tile keys. Side-view type bits and
raw hazard flags remain distinct from top-down terrain types.

Side-view Link handlers consume the active and below tile types published before
item parents, retaining those bytes through later tile writes. These published
bytes have a separate contract from position-based terrain queries. Shared bubble
allocation uses the native interaction pool and hazard-table match, including
Seasons lava rows. State-zero initialization remains eligible under text and
interaction masks; initialized bubbles retain the ordinary freeze gates.

Breakable tiles and cliffs also retain each pack's native collision-pointer
order, including flag and Gasha-list fallthrough. Underlying-tile restoration
uses the original gate: Ages checks diamonds and indoor/dungeon moving pots by
collision set; Seasons checks moving pots by active room group.
Projectile passability uses each pack's unconditional tile list and relative
directional cliff streams. Elevation is a wrapping byte; rejected crossings
invalidate the position/tile cache. Generic projectiles do not load bomb or
seed-shooter metadata to obtain this shared collision data.

Player content bindings share movement, melee combat, chest updates, sound and
tile-push publication through one world base. Its room, entity, combat and collision
services must reference the same live runtime memory; matching game IDs alone
do not establish that ownership. Story, item and transition bindings retain
their own source contracts around those services.
Boomerang construction, allocation and flight use the shared player-world and
content factory. Selected-pack data supplies the native variants. The controlled
parent retains its throw frame while it redirects Link's movement input into
the child's target angle, then resumes that animation on release or lost child
ownership. Child flight, collision and catch delay continue independently after
parent cancellation. Magic tile hits enter the common terrain/effect owner at
the native pre-movement phase.

Common drops, debris, splashes, clinks and sword beams have one factory behind the
native content bindings. The entity manager owns their allocation and retirement;
construction does not replace state-zero dispatch. Grass flicker uses physical
interaction-slot parity, and its palette samples the live terrain modifier.
Retain each cue's source phase: an allocation helper can play its cue after
checked admission, while an effect's cue can belong to its later state-zero visit.
Drop selection reads native probability, set, availability and remapping tables.
It consumes the original RNG draws before checked part allocation. Collection
uses the native treasure grant, including obtained flags, Gasha maturity and
ring-selected parameters. The entity owner creates hazard effects directly;
their availability does not depend on a game-specific terrain subscription.

Common enemy families bind their own native definitions without requiring
unrelated species or story data. Graphics and collision numbers remain selected
pack data. Their shared factory serves ordered placement, counted scripted
spawns and uncounted standalone allocation; it does not perform another
placement/RNG pass. Native state-zero
work, including visibility and species initialization draws, runs through the
same initializer for ordinary visits and transition preload. State zero may
remain eligible under text while the initialized AI is frozen.
Invisible native controllers need no graphics preload. Keep their state-zero
control and allocation work in the physical slot walk when it depends on
earlier objects in that same update; resolve their presentation as hidden.
Controllers without an enemy-character body declare physical ENEMY admission
explicitly. Contributing to `wNumEnemies` does not imply ENEMY-slot ownership:
PART effects and interaction puzzle sentinels can contribute to that count too.
Standalone construction is a separate capability from combat-source support:
an actor can allocate native projectiles without accepting ordinary hits. The
debug picker and native standalone requests use the same construction gate;
the picker additionally excludes actors requiring room-owned minigame or event
context.
Shared parents and their child PARTs resolve scoped behavior and visual data
from the selected pack through the common factory.
Native dispatch aliases use the same handler and combat adapter. Scripted
spawns supply their own identity and variant to that handler rather than
implementing another enemy state machine.
Shared PART boundary offsets belong to common projectile mechanics rather than
a wall launcher or boss bundle. Disabling collision preserves native geometry;
contact admission checks the collision-enable signal separately.
Digging's reward PART selects its enemy before checked allocation, retains the
source subid and retires on allocation failure. The selected pack supplies the
collision mode and the native Subrosia substitution. Enemy properties and
animations have a shared data reader; species behavior readers load their own
tables. Common fire projectiles use the same part factory and live runtime owner.
Enemy death puffs also use the common effect factory. Copy world XY and signed
Z separately; rendering an airborne enemy must not shift a reward's logical Y.
Native PART replacement retains the high XYZ bytes and the physical slot, and
the replacement does not receive another visit at the same slot cursor.

Object collision effects and target masks have one pack-bound data owner.
Target identity selects an enemy or PART mask; the live collision mode selects
an effect row, and the item's live collision type selects its column. Neither
mode numbers nor all item columns are interchangeable between games. Initial
enemy modes and item collision types come from their native property tables;
item state machines remain responsible for subsequent type changes. Preserve
each pack's native table extent and reject missing rows with pack/source
identity. Item-specific status behavior may be shared only after checking both
native builds; it does not own another copy of the collision matrix.

Combat descriptors retain pack and source identity, without copying shield
rows. Shield contact queries the live target type's active mask and live mode
before applying the selected common recoil profile. Binding contact-damage
callbacks requires the actor and combat source to share a pack. Validate native
row existence rather than an Ages-only mode/response whitelist: one enemy can
select different effects as its mode changes, even for the same item.

Gameplay positions use room/world coordinates. Preserve byte wrapping and 8.8
fractions where the source does; camera, height drawing, and transition offsets
must not rewrite logical positions. Geometry-only queries are read-only;
executed native movement publishes scratch values through the runtime state
owner. Preserve movement-result flags separately from displacement.
Native object position-copy helpers copy the high XYZ bytes. Keep the
recipient's fractions, including when an offset is applied; newly allocated
recipients begin with cleared low bytes. A single-coordinate high-byte write
also preserves that coordinate's low byte.
Handlers that read native camera bytes use the transition owner's current
projection. The camera samples Link after object updates and catches up at its
source-defined rate. Reconstructing an origin from Link inside a feature loses
that timing, camera holds and intermediate catch-up positions.
Original object visibility checks use projected screen coordinates with
unsigned byte wrapping and the source's edge leeway. Use the shared native
screen-boundary predicate so those lifetime checks retain that arithmetic.

Record-neutral enemy recoil, hazard handling and Scent Seed steering read the
common engine data owner. Import verifies their operands in both native builds
before publishing one shared copy. These mechanisms must not load a species,
boss or story bundle. High-bit recoil allocates its dust through the shared
interaction owner before movement; allocation failure still permits recoil.
Common hostile projectile bounce uses that same verified data owner. Profiles
retain each source's current-tile probe, pending-state handoff and movement
order; executed movement uses the native fixed-point and scratch-state owner.

Enemy targeting keeps the sampled active Link object position separate from
the Scent Seed target. The shared frame publishes both before the enemy pass;
select the sample read by the original helper. Preserve that sample through
later object writes and mounted sprite offsets. Handlers whose source reads
Link directly continue sampling those coordinates at their own visit.

Native `wScrollMode` remains shared live state. A script's `$00` write stops
the room-boundary pass and background animation without replacing the object
update loop. Preserve off-screen scripted movement until the source restores
the mode; ordinary room initialization and `disableLcdAndLoadRoom` restore
`$01`. Dormant resource preparation leaves the byte untouched until room
initialization runs. Input locks and a stopped boundary pass have separate lifetimes.

Grounded top-down movement and jumping share one planar velocity state. Ice
changes its convergence cadence, and a jump inherits the existing momentum;
current input alone cannot reconstruct takeoff velocity. Landing returns that
state to terrain handling in the original update order.
Heart Ring distance belongs to accepted input movement, before any ladder-top
clamp. Gravity, surface swimming, terrain displacement, recoil, and released ice
or underwater coasting have their own movement paths. Grounded recoil still
moves on its zero-counter update, then resumes ordinary movement in that same
update without repeating item parents. Text and scrolling retain recoil's
counter and position until normal dispatch resumes.
Airborne recoil runs before gravity and retained jump momentum, so both planar
steps can occur in one update. Its counter decrements faster in air; a borrow
cancels recoil without another step. Landing retains unfinished recoil and
resumes ground input only after it clears.
Shield recoil uses the shield hitbox center as its contact point. Its native
sideways offset participates in angle quantization independently of Link's
world position; the collision effect selects recoil and invincibility lengths.
Cliff eligibility compares current cardinal input with the retained object angle
before velocity convergence, after the item parents' movement and grab gates.
A held sword permits a cliff jump and retains its parent; state $12 freezes
that parent through landing, while its physical child follows Link's height.
Sword pokes consume retained wall probes before the new probe publication.
Landing retains the cliff's planar speed for the next terrain update, including
underwater and ice movement.
The shared player world composes terrain with its existing collision and
transition owners. Terrain jump eligibility runs before air integration.
Source-native launches that retain the air byte's high bit use the shared air
owner with unblocked movement and an optional landing target. Full Link
replacement clears that motion and target together. Seasons stump entry and
exit select their native probes and speeds from imported tables; entering a
stump and landing on one from a ledge retain their distinct coordinate rules.
Ledge crossings use the common scroll lifecycle, preserving Link's vertical
speed and animation while destination actors remain frozen.

Transformation rings select a deferred special-object identity through the
shared player dispatch. Initialization clears item parents before text and
object-mask gates; restoration returns through normal Link initialization.
Selection consumes the live suppression byte only when its native caller is
eligible. Menu, shop, grab and tileset gates read the same runtime owners in
both games. Sprite records and ring mappings belong to the selected pack.
Swimming also reads the selected pack's common Flippers and diving data;
Mermaid Suit behavior requires Ages identity as well as its treasure byte.

Currents and conveyors apply imported fixed-point velocity before item use and
ordinary input movement. That terrain step consumes Link's retained wall probes;
the later movement phase refreshes them. Keep the active foot tile from before
terrain displacement for the update's swimming and other terrain decisions.
Active position, index, type, previous type, standing counter and slippery flag
live in shared runtime memory. Publish them at the native terrain phase, then
apply handler normalization; geometry queries do not publish them. Airborne
top-down updates retain position/index/previous type while clearing type and
standing count. Side view gives the previous-type byte its native meaning of
the tile below Link. Chest collision handling consumes the retained publication
without sampling terrain again. Movement speed and stump gates read that
publication, including when a replacement Link handler retains it.
Active-tile lookup reads native layout storage, including cleared padding and
live aliases beyond the layout buffer. It wraps the original byte coordinates;
the geometry query's outside-room collision sentinel is a separate contract.

Floor breaking consumes that standing byte at its native threshold. Tile
mutation does not replace the active-tile identity within the current update;
item eligibility and later movement retain the original sample. Ordinary,
transformed, recoil and landing paths apply the same terrain handler. Cracked
floors share the queued tile write, rumble and checked silent-effect allocation
with scripted floor breaks, including each caller's behavior on queue or pool
failure. Seasons' cracked ice enters the water handler without floor-break
feedback; its native operands remain separate from the common floor profile.

Currents still move Link while an interaction supports him; conveyors reject
that riding signal. The water entry gate has its own support check.

Room caches distinguish source layout variants. Resolve destination variants
through `RoomSession` before preload, and reapply live persistent substitutions
when loading cached data. Keep logical layout, underlying terrain, collision,
and displayed tile mappings distinct.
Standard room-flag substitutions select Ages' collision profile and Seasons'
room group. Keep the native pointer aliases and flag order when importing and
applying these tables, before chest and room-specific changes. A collision
profile cannot substitute for the group selector during Seasons room reload.
Position-specific room-flag changes run before these standard substitutions and
chest restoration. Preserve their source order and aliases; Ages' linked-game
predicates do not replace Seasons' ordinary mask checks.

Opened chests are a shared layout substitution, applied before room-specific
changes. Native chest lookup selects the first matching room record; its stored
position controls room-load restoration, while the interacted tile controls the
live treasure position. Publish prepared layouts without rerunning substitutions
over their tile state. Native initialization side effects and saved-layout
restoration remain distinct phases. Missing records retain the native layout
pointer alias into script memory.

Imported room-object recipes may be merged and stably sorted during resource
preparation. Cache only immutable operands; save-dependent eligibility, native
allocation, state-zero work and RNG remain at the original dispatch boundary.
An entity's presentation node is optional. Invisible logical controllers still
occupy native slots and participate in the same ordered update, preload and
retirement paths. Scene-tree membership must not determine their lifetime.
Pending native interactions remain eligible during scrolling and use the live
slot walk across outgoing and incoming objects. Their initialization can delete
an allocation or create children before later slots run. Room resources detach
outgoing references without deleting those allocations; the native scroll
completion pass owns their eventual retirement.

Legacy collision passes retain a separate membership snapshot for the item
scan and each item's target scan. Callbacks may change live membership;
changes reach the next snapshot without invalidating one already in progress.
Scoped pooled arrays preserve these boundaries and clear references on return.
Incoming-room state-zero preparation rescans live membership after each actor,
selecting the next native category/slot while preserving ties in list order.
An object created below a category cursor waits for the next preparation pass.

Debug device builds can opt into room-loading timings with
`user://room-load-diagnostics.enabled` (or `--room-load-diagnostics` for desktop
profiling). Reports identify the original room and separate data/raster work,
live substitutions, actor construction, entry events and transition state zero.
Scope totals include managed allocation bytes and process-wide GC collection
counts. These CPU diagnostics do not measure the first displayed frame or GPU
completion; compare cold and repeated entries separately on the target device.
These CPU timings help distinguish first-use work from repeated entry; they do
not measure deferred GPU execution and do not change gameplay scheduling.

Shop stock shares one immutable set of BG price digits per room entry. Build
it from the room's current palette; a later entry creates a fresh set rather
than retaining palette-dependent textures across room activations.

Native changes to live tileset flags belong to the active room data. Room
initialization restores imported flags before native actors apply their writes.

Room-specific entry handlers run before actor allocation through the save and
sound owners. Resource preparation and native background-only loads bypass
them. Music selection is checked after parsing, before object state zero;
later actors can replace the selected song through the same sound owner.

Room initialization applies imported live WRAM writes before allocating actors;
switch-controlled tile restoration also precedes allocation. Those writes must
still occur when an object pool is full. Interleaved graphics
commands change displayed mappings independently of logical tiles. Publish a
logical tile only when the source explicitly writes it, through the shared
changed-tile queue when the source calls `setTile`.

Rotating-cube color and position are views of the shared runtime WRAM bytes.
Room reloads and scroll activation clear both before destination initialization;
outgoing controllers read those same live bytes. Shared chest handlers read item
flags through the active room identity, including handlers retained during a scroll.
Their queued tile writes also target the active room buffers using the handler's
retained position. A scroll does not redirect those writes to the old room.
Allocate unconditional placements before evaluating handler item flags; deleting
an actor after parsing cannot free its slot for later rows in that same parse.
Pattern-key controllers allocate the shared treasure interaction before deleting
themselves and retry while its pool is full. Treasure state zero runs under text
and scrolling; later spawning freezes until those gates clear. Falling treasures
check visibility using their camera-relative ground coordinates; height only
offsets the drawn sprite.

Chest opening owns reserved interaction zero and publishes the room item flag
before granting the treasure. Preserve its initialization and setup dispatches
before the rise. Its object mask lasts through pickup text and the cleanup
dispatch; inventory grants, including cross-era dungeon bits, use the shared
inventory owner.
Native chest-content interactions publish their treasure ID/subid in shared
runtime WRAM after initialization. Chest opening consumes and clears that
override before using the static chest table; room activation also clears it.
Empty contents open the chest and persist its item flag without a reward actor
or input freeze.

Handler state zero runs in physical object order rather than during construction.
Stateless floor/cube signal consumers read the active room throughout scrolling;
initialized cube-color sources and minecart gates freeze during the scroll. A
gate samples earlier signal writes on its first dispatch, while a later signal
affects it on a subsequent eligible pass. Preserve whole-byte trigger writes
separately from masked switch-bit changes.
Dungeon signal scripts preserve command carry and wait boundaries. A bridge
script publishes its completion flag and sound before its one checked allocation;
pool failure still completes that script. Keep these semantics separate from
controllers that retry allocation. Temporary button conjunctions change only
their output bit and retain the shared button release timers.
The interaction dispatcher samples its freeze gate at pass entry. A worker
clearing the shared mask later in that pass does not resume initialized
interactions until the following eligible pass.
Event-owned interaction lanes consume that same sampled result. Text opened
later in the pass does not freeze an already admitted native handler; its
script interpreter can still have a separate live text gate.
Native object masks apply separately to Link, items, enemies, parts,
interactions and companions. A cutscene can freeze initialized actors while
items and state-zero handlers continue; a single global freeze cannot represent
that dispatch.
Apply each category's gate at its actual dispatch site, including an earlier
special-object or item pass. A check in a later entity pass cannot undo an
update that already ran. Preserve that category's own raw scroll predicate
and state-zero exception rather than substituting the transition UI state.
Preserve raw speed and angle bytes until the source masks them. An unmasked
angle can read components across speed-table rows; angle bit7 produces zero
offsets. Use shared movement's byte addressing and scratch publication instead
of normalizing every input to $00-$1f in the caller.

Harp and Flute parents publish their native category mask through the shared
runtime state. Playback uses the ordinary object dispatcher, including its
initialization and always-update exceptions. An enemy's instrument reaction
runs only after enemy-category admission. The completed instrument signal
remains readable for the parent's release update; clearing it belongs to the
next eligible Link update.

Water-level reconstruction resolves the effective tileset and dungeon floor
before replaying tile substitutions and room-specific platform changes in their
source order. Cached rooms must restore earlier layouts when that save byte
changes back.

Tile hints share the original per-room suppression byte in runtime memory.
Front-tile dispatch uses the imported collision-mode lookup, including aliases
and label fallthrough. Blocks, locks and hints consume one WRAM countdown;
changing the candidate does not restart it. Chest/sign idle handlers and
ineligible Link updates retain it. Full room reloads clear it to zero;
scroll activation's shorter clear range preserves it.
Moving block/door interaction cleanup does not own this input countdown.
Use imported masks across tile handlers; several different hints consume the
same bit. Ordinary, scroll and cutscene room activation clear that byte.
Tile handlers return their source carry result to Link. A successful hint or
overworld key use ends his current handler before item parents and movement;
the late push-direction publication stays suppressed for that update.

Room graphics initialize the shared shop flag from the imported after-load
table. This flag is independent of NPC item restrictions: A-sensitive objects
accept held stock in shops, while front-tile handlers always reject grab state.

## Ordered room objects and RNG

The importer supplies one source-ordered object stream. Conditions retain their
source scope across opcode runs. Parse placements, reservations, conditional
objects, and enemies together; a separate placement pass changes occupancy and
RNG history.

Native object programs retain macro boundaries, compressed opcode runs,
pointer targets and return addresses. The shared reader owns condition and
event traversal; content factories supply construction bindings. Apply the
pointer gate before checking event flags, including its one-shot direction
clear. Preserve category-specific allocation failure: a full fixed-enemy,
ordinary-interaction or item-producer run skips its remaining records;
parameter-object and ordinary PART failure skip only that record.
Imported programs and roots do not establish that every
referenced handler is supported. Unbound construction fails with source context.

Object conditions read live `wRoomStateModifier`. Room initialization owns
publishing that byte: Ages calculates its room mode, while Seasons retains the
selected season. Parsing a supplied stream does not recalculate it from layout
flags or save data. Keep this same condition gate for placement and count-support
consumers.

The entity manager owns object placement for both games: native slot admission,
killable indices, entry exclusions, retries and position reservations. Content
factories construct the selected handler; they do not run another placement
algorithm. Spawn-tile restrictions come from the selected game's collision-mode
tables. Fixed placements reserve declared coordinates before state zero can
relocate the actor. Parameter-enemy opcodes allocate without incrementing the
room count while retaining their raw flags for later native counter behavior.
Placed PARTs reserve positions only after admission; parameter PARTs do not
reserve. Item producers retain their fixed-enemy declaration's count flag;
the dedicated item opcode allocates them uncounted. Their plain `enemyDelete`
retains a counted allocation until room reset. They retain their native
initialization even when the selected drop is unavailable. Availability decides
the later tile-change outcome, not whether the controller is allocated.

The shared placement buffer consumes the original 256 global RNG calls on a
real room parse. Other native buffer-generation calls overwrite that same
buffer, but only room parsing resets its cursor. Reservations, counters, and
aliased scratch bytes belong to `OracleRuntimeState`. Distinguish full parsing
from parsing a supplied stream: they do not imply the same clears.

Keep allocation, room-completion counting, killable indices, and recent-defeat
tracking separate. Source opcodes and native spawners have different policies;
failed placement or effect allocation does not automatically undo a count.
Native spawns use imported construction provenance and the shared allocator
without replaying room placement or consuming its RNG.

Never use a private enemy RNG, `Random.Shared`, or sorted collections in these
paths. Re-entry and preload consume RNG only at their traced boundaries.
Trace common state-zero RNG independently of species setup. Constructing or
preloading an actor must not move that draw ahead of its eligible dispatch;
failed child admission must not consume an initialization draw that the source
never reaches. Preserve source retry paths that execute initialization again.

## Native slots and update phases

The shared frame snapshots Link's current target position before ENEMY and PART
dispatch. Keep this independent from the scent target and from a parent's cached
position: a child can remain eligible while its parent is frozen. Consumers of
the original global target read the frame's Link target in their own update.

Animal companions and the raft run in the shared special-object phase before
Link. Mounting can therefore start Link's jump in that same update; dismounting
resets Link and preserves his initialization update before airborne movement.
Animal rider position is copied again after interactions, so a companion
boundary clamp reaches Link before post-object camera and transition checks.
Animal dismounts retain the live reserved slot with enabled `$01`; after a throw,
Dimitri's next eligible companion update clears its retention bit. Neither action
deletes the animal.
Native special-object bodies do not also dispatch as logical interaction
controllers. A body born after the special-object pass waits for its next
visit; construction before an object pass does not consume an update. Component
fixtures must run that shared special-object phase explicitly.
Idle motion continues to publish to native memory. Scrolling marks unretained
slots `$02` and deletes all their bytes at completion, while retained `$03`
owners survive. Waterfall riding gates read Link's active object page separately
from companion presence.
Raft dismounting instead requests Link's forced-walk state. Consuming that request
does not move Link, and the terminal countdown update returns to normal control
without another step. Recreating the waiting raft copies its whole-pixel position
into a fresh interaction and clears its fractional bytes.
The shared Link owner advances that walk independently of the requesting actor.
Pending requests wait for the normal state's palette/scroll gate; selected
forced movement continues through text and object-category masks. The raft's
own handler also has distinct admission: only riding checks text, palette,
scrolling and disabled-object mask `$81`. Initialization and its dismount
countdown do not use the ordinary companion mask `$a0`.

Boss entry publishes that same shared movement request at native initialization
or scrolling preload, with the selected game's duration and live Link direction
and angle bytes. Boss adapters do not run a second movement pass. Door counts
and the boss-entry bit share the authoritative `wcc93` byte.

Raft dismount consumes the forced-walk request before initializing Link's new
state. Existing item parents survive that request update; initialization clears
them before the first forced movement, and ordinary item allocation resumes
after the walk finishes.
Stationary minecarts clamp Link in their interaction dispatch after ordinary
movement; the boarding countdown starts only after that contact. Its boarding
gate accepts diagonal directional input through its own source checks;
do not substitute push-block eligibility. Losing contact retains an incomplete
countdown, while failed eligibility during contact resets it. Allocating the
ridden cart leaves Link's current update intact, and its special-object
initialization runs before Link on the next update. Rider synchronization copies
whole-pixel coordinates while preserving Link's fractions and keeps scrolling
offsets in presentation.
The cart and raft update before Link, but their rider copies run after
interactions. Item parents and door checks therefore see Link's preceding
sprite position, including the raft's preceding animation offset. Link retains
his normal-state dispatch while riding and can
consume a forced respawn without stopping the cart.
During raft knockback, the raft and Link each consume the shared knockback
counter in their own dispatch. Link's temporary recoil position and movement
scratch remain visible to intervening phases until the post-interaction rider
copy replaces his coordinates; synchronizing the rider does not undo either
counter update or scratch publication.
Minecart-created door openers initialize in their first interaction update and
start the interleaved door frame on the next. Layout changes before collision;
the final tile publication releases collision after the source countdown.
Layout door controllers preserve the native script's command yields and choose
their initial track branch once. After clearance they relocate the local respawn
point, close the door, update the shared shutter count, and delete themselves.
During scrolling, layout minecart shutters facing the incoming edge allocate
before placed objects in descending layout order. Only the shutter at the
entry position receives the open-track substitution.
The source caller admits this substitution in Seasons groups `4–7`. Ages
restricts it to groups `4–5` and additionally requires the dungeon tileset flag.
The mount-prohibition signal is consumed and cleared after the companion pass,
before Link, item parents, and interactions can publish it for the next update.
Companion attacks resolve contacts after item updates. Dimitri's reserved
Bracelet child owns throw physics and copies whole-pixel coordinates back after
Link, retaining the companion's coordinate fractions; his companion handler
observes that copy on the following update. Carry
poses and throws use the object's imported weight, including Dimitri's row.
New reserved Bracelet children run state-zero initialization and its throwing
tail even while ordinary item updates are frozen. Later item visits retain the
ordinary freeze gates, independently of the related object's own eligibility.

Remembered companions are admitted before room presets. Ages state-zero
initialization yields once for solid-object reservations, then checks the
remembered position and the shared last-mount fallback. Seasons checks the tile
on its first update without consulting solid-object reservations. Fallback
copies retain coordinate fractions in both games. Rejected spawns retain the
remembered bytes. Initialization also runs during fades and scrolling;
initialized companions remain frozen. Preset spawns clear only
the remembered ID and install their own last-mount point. Flute requests respect
both the shared live slot and retained outgoing companions.
Playback completion first allocates the native invisible Flute caller from the
interaction pool, then releases the parent and its control masks. Allocation
failure still completes playback. The caller performs gates, text and entrance
search on its eligible interaction visit, preserving ordinary slot order.

`RoomEntityManager` owns creation, active/outgoing lifetimes, contacts, and
native pools. Ordinary category order is items, enemies, parts, then
interactions. Reserved controllers retain their original positions within
those phases; logical controllers do not consume native slots.

Door scripts distinguish active text from completed non-exitable text. The
dialogue owner supplies the printing-completion signal; a retained completed
textbox still freezes initialized objects, while an admitted state-zero door
may run its first script command. Keep that script gate separate from object
admission and from opening/closing animation states.

Torch-controlled doors read the same room-local lit count as the torch scanner
and parts. Preserve the exact comparison and the script's selected wait even
if that count changes afterward. Entrance doors write their scratch bit through
the shared runtime state. Both variants reuse the common shutter animation and
canonical tile publication; neither owns a second tile or signal cache.
Torch hits use the ordered post-object item scan, including native height and
byte-position overlap. A collision publishes a pending hit for the next eligible
PART update, which counts, sounds, attempts the tile write, and retires the torch.

An event that runs a placed interaction's script still needs that interaction's
physical allocation and source lifetime. Keep script/modal state with the event
and allocation with the entity manager. An invisible interaction can retain its
slot after script completion; deleting it early changes later capacity and
slot-dependent drawing.

Leading companion barriers, tutorials, and keyhole controllers share their
imported placement order and allocate lazily through the native pool. A gap
occupied by an unmigrated source owner ends this supported prefix; later
companion controllers retain their logical representation. Full interaction
allocation parity requires those preceding owners to be migrated as well.

Each pool is walked live in ascending slot order. A child allocated into a
later slot can run in the same update; a reused earlier slot waits for the next
pass. Deletion frees capacity before scene-node cleanup. Do not substitute
scene insertion order, a collection snapshot, or separate incoming/outgoing
walks. Preserve checked and unchecked allocation-failure behavior explicitly.
An event-backed actor invokes its script or handler from that physical slot;
running the actor's work after the object pass loses newborn dispatch timing.
Allocation during an earlier category makes the child eligible for its later
category's walk. Keep it uninitialized until that visit; an immediate creation
callback can expose state changes to intervening objects too early.
Composite enemy representations still run in the enemy category before parts,
even while their separate native actor allocations remain unrepresented.

Allocation success is an input to the producer's continuation. A failed optional
effect can leave a flag, tile write or count intact; another producer may retain
its terminal counter and retry. Preserve ordered partial batches and atomic
multi-slot preflights as separate source policies. A managed wrapper does not
replace the physical occupancy of multiple native objects. Never keep a rejected
node as an unpooled actor. Unchecked failure writes require an explicit alias
path or a source-aware unsupported diagnostic, not a silent checked skip.

Levers allocate their graphical connection during state-zero dispatch and retry
if the pool is full. An earlier free slot becomes the parent; initialization
waits for that slot's next pass. Keep logical references attached to their roles
when exchanging physical slots.

Miniboss reward controllers preserve the native script's yields between commands,
including its zero-enemy check, flag publication and wait initialization.
Miniboss portal allocation occurs during the interaction walk; a higher slot
initializes in that update. Collision/menu release is the following script
command and occurs on its own update, even if portal allocation failed.

Essence objects own native movement, pedestal/glow allocation and energy parts
from the selected asset pack. Their shared event owns collection flags, input,
dialogue, music and the source return warp. Each game's composition binds its
services and cancels that event on room replacement; destination interactions
own the game's post-return story.

Colored-floor parents latch a changed tile before checked child allocation;
freeing capacity later does not retry that unchanged candidate. Landing children
remain in state zero, so text, interaction masks and scrolling still admit their
landing/cancellation check against the current room. Color workers generate the
shared permutation once, then read live scratch-buffer entries over their
bounded lifetime. Their initialized states freeze with ordinary interactions.
Queued logical writes and unconditional underlying-buffer writes retain the
original distinction, including queue rejection and large-room padding.

Slot references resolve the current occupant, including deletion and reuse.
Allocation, deletion, and native replacement have distinct byte-clearing and
coordinate-copy rules. Shared bytes that outlive an actor remain with the pool
or WRAM owner; retaining a stale object reference is not equivalent.

Held enemy children preserve their physical allocation across scrolling while
native enabled bit 1 is set. The room owner transfers surviving children and
rebinds their terrain at arrival; held-position copying still follows Link
while ordinary enemy updates are frozen. Throwing clears that persistence.

Update eligibility is separate from input ownership. Dialogue, object freezes,
scrolling, and palette fades can admit state-zero initialization or marked
effects while freezing initialized actors. Each category samples dialogue at
entry. Trace the caller's mask as well as the handler's state checks.
Rideable interaction objects retain interaction eligibility when enemies and
items are disabled; boarding support does not move them into another category.
Every enemy character explicitly exposes its pending source initialization
from its own live state. Palette and object-freeze gates use that eligibility;
sprite visibility and species-specific dialogue interfaces are not substitutes.

Room placement and an actor's first object update are separate boundaries.
An interaction graphics load can suspend that object pass. Resume it before
starting another main-thread iteration: earlier actors, playtime, and scroll
handling must not advance again. The Impa encounter uses this continuation
path and the native graphics-header residency bytes; other actor loaders have
not yet been migrated. Godot's texture cache does not establish native residency.

Room events that own a special Link object dispatch it before their interaction
scripts. Its source eligibility can differ from normal Link and from NPCs,
including during scrolling.

Cross-object signals retain their publication and consumption phases. Link may
read the preceding enemy or interaction pass before shared signals clear;
later parts and interactions may observe writes in the current update. A pause
must preserve signals alongside the state it freezes.
Tile push handlers consume the preceding graphics pass's pushing direction
before Link moves. The next publication follows object updates and uses Link's
retained wall probes; moving Link or changing a tile later cannot retroactively
establish that contact for the same update.

Moving platforms publish support through a shared rider owner. Link consumes
the preceding interaction pass's claim before the special-object tail clears
it, including while dialogue freezes the actors. A platform's retained local
boarding state does not itself preserve that shared support signal.
Top-down platforms occupy the shared physical interaction pool; slot order
determines which overlapping platform claims Link. Carrying tests Link's
authoritative native state `$01`, including the update when a forced state is
requested but has not yet been consumed by Link.
Carry uses Link's retained wall probes from before his input movement. Hole
pulling runs in the terrain phase before item use, including the first update
after support clears; dialogue freezes that phase. A later platform claim can
cancel a partial pull on the following update, but cannot undo a falling state
already selected by Link.

Item parents, physical children, reserved-item movement, post-object handlers,
and post-object collisions have separate lifetimes. Existing parents advance
in source slot order before Link reads movement restrictions; airborne
allocation gates still permit those parents to update. Allocation samples grab
and air state before parent updates: a later release or landing cannot make
that update eligible retroactively. Physical-child initialization consumes RNG
and plays its cues after Link's movement and standing animation. The animation
selected at item initialization retains its timing and arc parameters across
vehicle handoffs. Scroll updates skip Link's item handling, preserving the
raised Shield byte until the next eligible parent update. Reserved Pegasus
dust keeps item state zero, so its startup and cloud animation continue while
scrolling freezes Link's Pegasus counter.
Seasons' level-two Feather retains its free-pool parent until release or glide.
Its parent tests the descending speed before Link integrates gravity. Reduced
gravity belongs to the air state and survives deletion of the parent until
landing; clearing item parents alone does not cancel the arc. Both free slots
participate in allocation alongside the other free-pool item parents.
Native melee and projectile
contacts resolve after movement, in native item/target order; their signals
are consumed by later eligible handlers. The first accepted overlap can end a
scan even when its effect is a no-op. Cancellation and room replacement retire
pending requests through their owner. Legacy collision paths remain distinct
until explicitly migrated.

Boomerang and Bombchu share the imported generic throw-parent animation contract.
Parent reservations precede dispatch; lower slots initialize their physical
children first, and a newly initialized parent advances once in that update.
Bombchu retains an enemy slot reference, so homing reads its current occupant
after retirement or replacement rather than retaining an entity instance.
Its parent, child, allocation and explosion owners are shared between games;
graphics, collision identity and target eligibility come from the selected
native pack. Ages adds its native underwater and raft parent gates through
the game binding; Seasons does not inherit those gates.
Rooms, objects and physical items read the same imported hazard streams.
Keep native flags intact until the caller dispatches its water, hole or lava
response; side-view vertical motion retains those flags in the item state.
Bracelet uses the selected pack's parent timing and carried-object data.
Native stump admission, bouncing-tile behavior and Power Glove push policy
remain source differences. Lifted tiles publish their reserved-item geometry
to the ordered post-object scan; source-backed targets select the native
thrown-object column and consume its pending hit on their next eligible visit.
Native simple-collision queries read the raw quarter bit. The allow/disallow-holes
queries apply special collision masks, including water and lava. Select the
helper called by the source handler.
Bomb and Bombchu explosions share the imported collision column. Targets with
a native collision identity resolve in the ordered post-object item scan;
species handlers own specialized effects, and accepted common damage publishes
the pending hit for the next enemy update. Ordinary Bomb contacts for owners
without that identity retain their legacy dispatch; Bombchu reports an unsupported
overlap explicitly rather than borrowing a collision row.
Shovel's reserved child remains collidable for its full native lifetime.
Targets using the native item phase read that child after enemy AI and before
dynamic items; the single tile-dig attempt does not dispatch those collisions.
Source-backed targets read the selected native Shovel column and live mask;
ordinary bumps and species reactions retain their own response. Status handlers
expose their pending hit through the common collision contract so that one
publication cannot queue two separate skipped updates.
The tile transaction selects the native grass, snow, dirt or suppressed debris
effect. Stationary puffs and snow share their animation/flicker/lifetime handler;
directional Shovel debris reads each pack's native OAM and motion data and
retains its source always-update eligibility.
The physical-item pass freezes initialized items during a palette thread,
independently of text and object masks; state-zero handlers remain eligible.

Biggoron's reserved weapon publishes its arc and live ring damage in the
unconditional item post pass, before ordered collision resolution. That pass
also consumes the parent's tile-probe marker while parent dispatch is frozen.

The final carried-position attachment uses Link's completed position, after
object handlers. It also runs for held bombs whose parent was frozen earlier;
parent animation offsets and child position fractions retain their own lifetimes.

## Room lifetime and transitions

The shared scroll controller owns cardinal scroll progress, the original
setup, movement and cleanup counters, and the graphics/palette upload schedule.
It selects native records through the destination pack and tileset state,
retaining the room's post-scroll graphics bit and the loaded unique header.
Both room tilemaps use the same live graphics and palette slots throughout
the scroll. The shared transition owner combines it with ordinary warp loading,
destination placement, fades and camera writes. Ages extends that owner with
time travel and its native navigation rules; these do not construct another
ordinary transition owner. Seasons binds the same owner and extends its native
room-pack and season-selection rules. Room-pack season selection precedes the
fade; warp arrivals retain their separate source-group rules. Unimplemented room object
streams produce explicit source-aware navigation-only diagnostics. Preload is not
room entry: counters, RNG, music, checkpoints,
events, and persistence change only at the original boundary.

Native cutscene handlers that precede objects advance before Link and item
dispatch, including their release update. Their ownership of post-object work
lasts through that update. Selecting a main-thread handler does not replace
Link's object state: an existing arrival continues unless its native handler
is disabled or its object memory is cleared. Camera admission also controls
screen-shake counters and RNG. In-place layout reloads retain room identity and actor lifetime;
layout, collision, underlying terrain and background mappings follow their
separate source writes, including temporary collision-buffer scratch data.

Screen-variable/WRAM1 clearing and the broader shared-variable clear are
separate operations. A cutscene that only clears screen variables and WRAM1
retains the shared object mask while replacing actors and loading its layout.
The screen clear spans `$30` bytes at `wScreenVariables`, with loaded unique
graphics, layout and animation IDs reset to `$ff`; the loaded palette ID is
zero. Shared menu, item-parent and transition-disable bytes lie outside that
range and retain their values until an actual writer or broader clear changes
them.

Warp deactivation uses the authoritative `wEnteredWarpPosition` byte. Full
destination loading publishes the packed position before initializing entities;
typed hazard respawns and raw script writes use that same marker. The ordinary
warp check retains it while Link stands on the marked tile or its supported
two-tile doorway neighbor, then clears it to `$ff` when Link leaves.

Hazard animations consume Link's live native animation counter and marker.
Keep each source handler's order: state `$02` tests its drowning marker before
animation, while ordinary side-view drowning animates before testing it.
Side-view lava requests that shared respawn state after its final normal
movement tail. The special-object prelude clears `wcc92` before companion and
Link producers publish their bits for later phases.

The object pass samples and clamps ordinary screen boundaries before cutscene
selection. Both games use the shared boundary sampler: Y precedes X, clamps
retain fractions, and the delay uses live `wScreenTransitionDelay`. Native
map-wrap and water gates remain game-specific. Requests use the live
`wScreenTransitionDirection` and pending bit `$04` of `wScrollMode`, including
while a toggle runs; Link's normal handler and item parents wait for that request.
The next normal cutscene update consumes it after warp checks, without requiring
the player to keep holding the original direction. A forced direction's bit 7
bypasses ordinary coordinate, input, delay and disable checks; its position
is retained. Ledge requests enter this same post-object handoff. The scroll
owner publishes native phase bytes and mode `$08` through completion, then
releases mode `$01` while retaining the last direction. Full room loads clear
the request; coordinate-only background replacement retains it.

During scrolling, ordinary destination and retained outgoing objects remain
frozen. Only source-eligible initialization and transition-safe handlers run.
Initialization follows category/slot order and receives the live player when
the source reads or moves Link. Ordinary initialized objects stay frozen through
the finishing update; ordinary destination gameplay resumes afterward. PARTs
whose source enabled bit 7 survives scroll marking keep their normal handler,
including movement, counters and deletion, in physical PART-slot order.
Always-update eligibility and scroll persistence are separate: marking preserves
enabled bit 7, but cleanup still clears objects whose low enabled bits are `$02`.
Native `$03` reserved items retain their physical allocation and coordinates.
Room-pack fade reloads also retain interactions whose low enabled bits are `$03`,
including their physical slots and script cursors; ordinary warp and direct loads clear them.

Scroll setup, motion, row loading, and cleanup are separate updates. Scrolling
retains source-defined fractional positions, item state, and outgoing slots;
full loads clear transient objects at their own boundary. Warps, scrolls,
time travel, and development direct loads are distinct entry contexts.

Ordinary and delayed warp fade-outs omit the object pass, including their
terminal reload update. Destination placement leaves actors pending until the
following arrival update. Standing-arrival palettes advance before objects; their
terminal update releases handlers that wait for the palette thread to finish.
The first destination Link dispatch applies native standing-handler facing,
horizontal alignment and checkpoint writes before destination actors. Room
loading clears Link's direction; keep-facing aliases retain subsequent control
writes rather than consulting the stair-facing table. Parameter-facing uses the
live native state byte written by its control producer, independently of the
warp parameter nibble. Fade completion does not
write those checkpoints again.

Hole source mode `$09` uses Link's shared native animation before requesting
the ordinary fade. Warp selection publishes state `$0a`; Link's next eligible
visit adds `$08` to Y, centers both coordinates, clears item parents and starts
the fall cue and animation. The handler checks the previous animation marker
for `$ff` before advancing it. The main thread consumes its request after that
object pass and starts the fade at offset zero. Text and ordinary Link-disable
masks do not freeze the selected warp state. Full Link replacement or scene
retirement releases its animation ownership.

Hardcoded warp records preserve five distinct bytes: destination group, room,
Link arrival transition, packed position and main-thread request. Requests
`$03/$83` select the fade-out cutscene; they are not Link arrival transition
`$03`. An interaction's request is consumed after the object pass, while the
destination's arrival handler uses the independently imported transition byte.

Subrosia travel keeps Link's native warp state through its cutscene-owned room
load. Link owns spinning, Z motion and counters; the transition owner advances
the inverted screen wave and palette thread before the special-object pass.
Input remains disabled until the cutscene and Link's final facing both complete.

Tile warps use imported behavior and exact position windows. Use the room's
active collision set to classify warp tiles, including adjacent
door tiles and arrival suppression. Each pack retains the native ordered source
stream, group pointers, default/end markers and pointer-list fallthrough. Group
selects the small/large edge quadrant rule; this does not depend on room width.
Arrival facing also uses the selected collision mode's imported table, with
the native default facing direction. The selected native mode-dispatch table
retains aliases and game-specific handlers. A handler requiring a separate
unbound sequence diagnoses that source path before ordinary loading can mutate
the current room. Source selection retains the destination index until the
full-load boundary; only then does destination resolution sample live state,
including Seasons' Maku-growth byte and native byte arithmetic. Direct requests
retain the native season-preservation flag independently of their destination
group. Seasons uses the live source room identity when selecting the destination
season; preserving a season still updates the room-pack bytes.
Tile classification also retains each native entry's parameter.
Resolve requests from the final gameplay position in the original
hazard/object/exit order.
Keep air state, height, grab, modal, and native warp-disable gates separate.
Link-owned entrance handlers and direct scripted fades have different state
and sound ownership. Imported destinations are exact anchors, not suggestions
to move Link to an adjacent safe tile. Arrival and local respawn suppress a
warp at that anchor until Link leaves it.

Time travel preserves its own update masks and failed-arrival return path.
Ages publishes its palette selector from live tileset flags when the source
effect is created. Each effect copies that selector at birth; the arrival
effect reads the current native byte rather than a controller snapshot.
Era/season banners use the common interaction handler and selected pack's
graphics, display predicates and suppression flag. Allocation reserves a
physical interaction slot; state zero samples the live era or room-pack/season
and initializes graphics. Suppressed or capacity-rejected requests do not retry.
Local hazard respawn, death checkpoints, and remembered companion positions
are different state. See [Saves and state](saves-and-state.md).

The ordinary large-room camera advances by one high-byte pixel per axis per
original update toward its clamped target and holds during text. Only traced
load/reset paths snap it. Temporary item helpers may own focus, but the
transition controller remains the sole camera writer.

## Terrain and shared mechanics

`RoomSession` owns queued tile writes. Accepted writes change logical terrain
and collision immediately while retaining old graphics; draining applies the
stored visual values in order, including repeated writes to one position.
Gameplay drains up to four entries after object updates, using the live scroll
mode rather than a game binding's approximation. Full loads and commits clear
the queue; preload does not. Direct writes remain a separate operation, and
not every legacy writer uses the queue yet.
Native layout-only writes retain the existing collision byte and displayed
mappings; do not infer a collision refresh from the replacement tile.

Underlying terrain is live shared state. Covered floor buttons, moving blocks,
and removal/restoration paths must read and update the same buffer rather than
invent replacement tiles. Dungeon toggle preparation and live toggles likewise
share the authoritative layout and toggle state.

Shared tile-breaking owns replacement lookup, terrain mutation, persistent
flags, solve sound, and drop selection. Weapons and companions retain their
source probe order and effect creation. Shared carrying owns held/release/throw
motion; object handlers retain native fuses, landing, and destruction. Preserve
the preceding-pass pickup buffer and later held-position copy boundaries.

Do not interchange raw metatile collision, Link's movement masks, wall capture
probes, and item passage rules. Similar geometry can have different byte-wrap,
height, boundary, and side-effect contracts. Link owns forced-state requests,
item cancellation, and recovery; request consumption, initialization, terminal
animation, and return to ordinary movement may occupy separate updates.

Link and item splashes use the same checked physical interaction pool as enemy
and block splashes. Creation leaves state zero pending; that object's dispatch
owns its sound and visibility. Initialized splashes retain their native always
update eligibility and are cleared through the room entity owner.

Link's damage owner retains the original fractional damage accumulator separately
from inventory health. Ring modifiers operate on signed bytes before damage is
converted to quarter-hearts. Healing, equipment changes, and coordinate-only
room movement do not discard the fraction; potion use and fresh Link
initialization reset it.
An accepted hit may therefore apply recoil and invincibility without changing
the displayed health on that update.

Enemy and part contact publishes a pending raw damage byte after Link's update.
The next eligible Link handler consumes it using the ring equipped at that time;
source-specific contact protection is sampled when the contact occurs. Palette
and ordinary scrolling gates can defer consumption, while text and object masks
can freeze later movement or item handling after damage was consumed. The contact
signal clears after every Link dispatch independently of pending damage. Item
parents that read that signal must run before it clears; health loss alone does
not cancel every parent. Full Link replacement and explicit collision resets
clear both publications, while coordinate-only scrolling preserves them.

Attachment restrictions are publications for the next Link update. Clear their
high immobilization bits and item-use signal after Link, even when text prevents
the attaching object from updating. Republish only from the eligible attached
handler. The item-use signal suppresses new item parents; existing parents still
follow their own source handlers. Sword parents explicitly cancel on this
signal, while a shovel parent continues. Frozen parent dispatch defers those
checks and retains the existing action. Once allocation resumes, a fresh press
can replace an equal-priority parent; retaining an active action is not itself
a reason to reject that source allocation. Its physical children keep their
separate lifetime and admission checks.

Ordinary Feather jumps retain native enemy/part contact eligibility. Collision
masks and signed height overlap decide contact, including near-ground jump
updates; interaction callers keep their separate ground-contact requirements.
Use the live contact object's height for flying enemies and parts instead of
substituting ground height after an earlier height check.

## Enemy and item collision handoffs

Shared collision interfaces only expose a route. Eligibility still depends on
the live native collision identity, mode, mask, enable bits, pending status,
invincibility and XY/Z tests used by that scan. Forward imported metadata even
when a species uses a custom response. Trace active children and identity
changes through their writers; the placed parent's metadata alone is insufficient.
Adding metadata can also move an item from a legacy fallback into the native
scan, so verify every affected route rather than only the newly supported item.

Use one pending-hit publication for one source collision. Shared status handling
owns its priority against recoil, stun, death and ordinary AI; a species-specific
classification must not add a second pending update. Non-damaging effects may
notify the enemy, mark the item for removal, or accept contact without either.
Preserve the selected effect and scan termination, including any suppression of
later Link contact. Completion belongs to the next eligible consumer update;
freezing that consumer must preserve its pending signal.

## Entity ownership

Dynamic entities expose only capabilities needed by shared systems: updates,
presentation, collision/contact, combat, interaction, transition offsets, or
native hooks. Species state stays with its original owner. Avoid a universal
entity base class or behavior inferred from node names.

Enemy health, status, collision enablement, room counts, drops, and defeat flags
are independent. Zero health does not universally delete an actor, and a native
replacement does not imply death. Preserve source priority, death-effect slot
lifetime, allocation failures, and RNG consumption.

The entity manager owns the shared room enemy count, including explicit native
counter writes. Clearing that count does not delete its contributing actors;
later increments still affect the byte, while the common decrement guards zero.
Room parsing resets the count independently of retained outgoing actors.

Rideable animals, minecarts, and rafts share one live companion owner. A mounted
owner supplies transition position and transfers from the outgoing set after
scrolling. Destination events must check both sets before spawning a waiting
companion. Mounting, dismounting, riding presentation, and hazards use that
owner's source gates; unmounted recovery must not take control of Link.

The reserved special-object slot binds one actual entity reference to its live
native header. A free presentation and a free native allocation are distinct:
room reconstruction can adopt retained bytes without another birth. Checked
producers test admission before construction or counters; direct source
overwrites preserve the union and retire the predecessor without clearing the
successor. Retirement uses reference identity, including same-ID successors.
An adapter that changes from interaction to vehicle joins special-object
dispatch and drawing at that source handoff. Consumers read the current native
slot and Link page; only source-defined captured values survive replacement.

`InteractionController` centrally routes A-button targets with explicit order
and lifecycle ownership. Features register targets instead of scanning input
independently. See [NPCs and events](npcs-and-events.md) for ordinary NPC, linked
interaction, and room-event boundaries.

## Adding a room mechanic or entity

1. Trace all source placements, callers, dispatch rows, and tables, including
   initialization, update masks, counters, arithmetic, RNG, and teardown.
2. Extend the owning importer when source facts are missing. Preserve source
   order and use the existing runtime state and allocation owners.
3. Implement the smallest shared rule supported by the source.
4. Validate real imported geometry and gameplay updates: first/final updates,
   contact, batching, preload, warp entry, cancellation, re-entry, and persistence.

Keep hexadecimal source IDs in diagnostics and failures. Entity-specific
constants, branch traces, and regression findings belong beside code and tests,
not in this guide. Fidelity audits belong in untracked `local-audits/` files.
