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

Room caches distinguish source layout variants. Resolve destination variants
through `RoomSession` before preload, and reapply live persistent substitutions
when loading cached data. Keep logical layout, underlying terrain, collision,
and displayed tile mappings distinct.

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

`RoomEntityManager` owns creation, active/outgoing lifetimes, contacts, and
native pools. Ordinary category order is items, enemies, parts, then
interactions. Reserved controllers retain their original positions within
those phases; logical controllers do not consume native slots.

Each pool is walked live in ascending slot order. A child allocated into a
later slot can run in the same update; a reused earlier slot waits for the next
pass. Deletion frees capacity before scene-node cleanup. Do not substitute
scene insertion order, a collection snapshot, or separate incoming/outgoing
walks. Preserve checked and unchecked allocation-failure behavior explicitly.

Slot references resolve the current occupant, including deletion and reuse.
Allocation, deletion, and native replacement have distinct byte-clearing and
coordinate-copy rules. Shared bytes that outlive an actor remain with the pool
or WRAM owner; retaining a stale object reference is not equivalent.

Update eligibility is separate from input ownership. Dialogue, object freezes,
scrolling, and palette fades can admit state-zero initialization or marked
effects while freezing initialized actors. Each category samples dialogue at
entry. Trace the caller's mask as well as the handler's state checks.

Cross-object signals retain their publication and consumption phases. Link may
read the preceding enemy or interaction pass before shared signals clear;
later parts and interactions may observe writes in the current update. A pause
must preserve signals alongside the state it freezes.

Item parents, physical children, reserved-item movement, post-object handlers,
and post-object collisions have separate lifetimes. Native melee and projectile
contacts resolve after movement, in native item/target order; their signals
are consumed by later eligible handlers. The first accepted overlap can end a
scan even when its effect is a no-op. Cancellation and room replacement retire
pending requests through their owner. Legacy collision paths remain distinct
until explicitly migrated.

## Room lifetime and transitions

`RoomTransitionController` owns scrolls, warps, destination placement, fades,
and camera writes. Preload is not room entry: counters, RNG, music, checkpoints,
events, and persistence change only at the original boundary.

During scrolling, ordinary destination and retained outgoing objects remain
frozen. Only source-eligible initialization and transition-safe handlers run.
Initialization follows category/slot order and receives the live player when
the source reads or moves Link. Initialized objects stay frozen through the
finishing update; ordinary destination gameplay resumes afterward.

Scroll setup, motion, row loading, and cleanup are separate updates. Scrolling
retains source-defined fractional positions, item state, and outgoing slots;
full loads clear transient objects at their own boundary. Warps, scrolls,
time travel, and development direct loads are distinct entry contexts.

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

## Entity ownership

Dynamic entities expose only capabilities needed by shared systems: updates,
presentation, collision/contact, combat, interaction, transition offsets, or
native hooks. Species state stays with its original owner. Avoid a universal
entity base class or behavior inferred from node names.

Enemy health, status, collision enablement, room counts, drops, and defeat flags
are independent. Zero health does not universally delete an actor, and a native
replacement does not imply death. Preserve source priority, death-effect slot
lifetime, allocation failures, and RNG consumption.

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
