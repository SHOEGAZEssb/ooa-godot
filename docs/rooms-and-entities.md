# Rooms and entities

## Room identity and geometry

`RoomSession` owns active room identity, data, layout state, and dungeon context.
Rooms use a group plus hexadecimal room ID. Small rooms are 10 by 8 metatiles
(160 by 128 pixels); large-room storage is 16 by 11 with a 16-byte row stride,
but only 15 columns are playable. Dungeon neighbors come from imported floor
layouts, never room-ID arithmetic. Side-view groups retain their identity even
when source tilesets and objects alias another group.

Gameplay positions use room/world coordinates. Preserve byte wrapping and 8.8
fractions where the source does; camera, height drawing, and transition offsets
must not rewrite logical positions. Geometry-only queries are read-only;
executed native movement publishes scratch values through the runtime state
owner. Preserve movement-result flags separately from displacement.

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
Cliff eligibility compares current cardinal input with the retained object angle
before velocity convergence. Landing retains the cliff's planar speed for the
next terrain update, including underwater and ice movement.

Currents and conveyors apply imported fixed-point velocity before item use and
ordinary input movement. That terrain step consumes Link's retained wall probes;
the later movement phase refreshes them. Keep the active foot tile from before
terrain displacement for the update's swimming and other terrain decisions.
Currents still move Link while an interaction supports him; conveyors reject
that riding signal. The water entry gate has its own support check.

Room caches distinguish source layout variants. Resolve destination variants
through `RoomSession` before preload, and reapply live persistent substitutions
when loading cached data. Keep logical layout, underlying terrain, collision,
and displayed tile mappings distinct.

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

Handler state zero runs in physical object order rather than during construction.
Stateless floor/cube signal consumers read the active room throughout scrolling;
initialized cube-color sources and minecart gates freeze during the scroll. A
gate samples earlier signal writes on its first dispatch, while a later signal
affects it on a subsequent eligible pass. Preserve whole-byte trigger writes
separately from masked switch-bit changes.

Water-level reconstruction resolves the effective tileset and dungeon floor
before replaying tile substitutions and room-specific platform changes in their
source order. Cached rooms must restore earlier layouts when that save byte
changes back.

Tile hints share the original per-room suppression byte in runtime memory.
Front-tile dispatch uses the imported collision-mode lookup, including aliases
and label fallthrough. Blocks, locks and hints consume one WRAM countdown;
changing the candidate does not restart it. Chest/sign idle handlers and
ineligible Link updates retain it. Ordinary/cutscene reloads clear it to zero;
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

## Native slots and update phases

Animal companions and the raft run in the shared special-object phase before
Link. Mounting can therefore start Link's jump in that same update; dismounting
resets Link and preserves his initialization update before airborne movement.
Animal rider position is copied again after interactions, so a companion
boundary clamp reaches Link before post-object camera and transition checks.
Raft dismounting instead requests Link's forced-walk state. Consuming that request
does not move Link, and the terminal countdown update returns to normal control
without another step. Recreating the waiting raft copies its whole-pixel position
into a fresh interaction and clears its fractional bytes.
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
Minecart-created door openers initialize in their first interaction update and
start the interleaved door frame on the next. Layout changes before collision;
the final tile publication releases collision after the source countdown.
Layout door controllers preserve the native script's command yields and choose
their initial track branch once. After clearance they relocate the local respawn
point, close the door, update the shared shutter count, and delete themselves.
During scrolling, layout minecart shutters facing the incoming edge allocate
before placed objects in descending layout order. Only the shutter at the
entry position receives the open-track substitution.
The mount-prohibition signal is consumed and cleared after the companion pass,
before Link, item parents, and interactions can publish it for the next update.
Companion attacks resolve contacts after item updates. Dimitri's reserved
Bracelet child owns throw physics and copies whole-pixel coordinates back after
Link; his companion handler observes that copy on the following update. Carry
poses and throws use the object's imported weight, including Dimitri's row.

Remembered companions are admitted before room presets. Their state-zero
initialization yields once for solid-object reservations, then checks the
remembered position and the shared last-mount fallback. Rejected spawns retain
the remembered bytes. These two initialization updates also run during fades
and scrolling; initialized companions remain frozen. Preset spawns clear only
the remembered ID and install their own last-mount point. Flute requests respect
both the shared live slot and retained outgoing companions.

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
Physical items share imported hazard data and side-view vertical arithmetic;
each original caller owns its response to water, holes and lava.
Bomb and Bombchu explosions share the imported collision column. Targets with
a native collision identity resolve in the ordered post-object item scan;
species handlers own specialized effects, and accepted common damage publishes
the pending hit for the next enemy update. Ordinary Bomb contacts for owners
without that identity retain their legacy dispatch; Bombchu reports an unsupported
overlap explicitly rather than borrowing a collision row.
The physical-item pass freezes initialized items during a palette thread,
independently of text and object masks; state-zero handlers remain eligible.

Biggoron's reserved weapon publishes its arc and live ring damage in the
unconditional item post pass, before ordered collision resolution. That pass
also consumes the parent's tile-probe marker while parent dispatch is frozen.

The final carried-position attachment uses Link's completed position, after
object handlers. It also runs for held bombs whose parent was frozen earlier;
parent animation offsets and child position fractions retain their own lifetimes.

## Room lifetime and transitions

`RoomTransitionController` owns scrolls, warps, destination placement, fades,
and camera writes. Preload is not room entry: counters, RNG, music, checkpoints,
events, and persistence change only at the original boundary.

The object pass samples and clamps ordinary screen boundaries before cutscene
selection. The transition owner retains an accepted exit direction while a
toggle runs; Link's normal handler and item parents wait for that request.
The next normal cutscene update consumes it after warp checks, without requiring
the player to keep holding the original direction. Room loading clears it.

During scrolling, ordinary destination and retained outgoing objects remain
frozen. Only source-eligible initialization and transition-safe handlers run.
Initialization follows category/slot order and receives the live player when
the source reads or moves Link. Initialized objects stay frozen through the
finishing update; ordinary destination gameplay resumes afterward.

Scroll setup, motion, row loading, and cleanup are separate updates. Scrolling
retains source-defined fractional positions, item state, and outgoing slots;
full loads clear transient objects at their own boundary. Warps, scrolls,
time travel, and development direct loads are distinct entry contexts.

Ordinary and delayed warp fade-outs omit the object pass, including their
terminal reload update. Destination placement leaves actors pending until the
following arrival update. Basic arrival palettes advance before objects; their
terminal update releases handlers that wait for the palette thread to finish.

Tile warps use imported behavior and exact position windows. Use the room's
active collision set to classify warp tiles, including adjacent
door tiles and arrival suppression; room group selects source records only.
Resolve requests from the final gameplay position in the original
hazard/object/exit order.
Keep air state, height, grab, modal, and native warp-disable gates separate.
Link-owned entrance handlers and direct scripted fades have different state
and sound ownership. Imported destinations are exact anchors, not suggestions
to move Link to an adjacent safe tile. Arrival and local respawn suppress a
warp at that anchor until Link leaves it.

Time travel preserves its own update masks and failed-arrival return path.
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
Gameplay drains up to four entries after object updates, subject to the scroll
gate. Full loads and commits clear the queue; preload does not. Direct writes
remain a separate operation, and not every legacy writer uses the queue yet.

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

Ordinary Feather jumps retain native enemy/part contact eligibility. Collision
masks and signed height overlap decide contact, including near-ground jump
updates; interaction callers keep their separate ground-contact requirements.
Use the live contact object's height for flying enemies and parts instead of
substituting ground height after an earlier height check.

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
