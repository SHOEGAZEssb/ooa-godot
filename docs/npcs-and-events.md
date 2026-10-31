# NPCs and events

Visible characters are usually interaction objects, but their placement,
initialization, scripts, native handlers, and linked parts may have different
owners. Choose the runtime representation from those original owners, not from
the character's appearance or the apparent size of the scene.

Using the NPC presenter does not allocate an interaction slot. A native
companion retains special-object initialization, update eligibility and its
visit before Link. An interaction script's phase write is consumed on the
following eligible companion visit. A change of presenter retains the physical
slot, animation counter and motion; it neither initializes another actor nor
dispatches that slot again. Retiring the old presenter must preserve the
successor's bytes and the controller's remaining source operations.
State-zero initialization keeps its early special-object visit during scrolling,
including an outgoing allocation. Read the live destination room for its collision
and native state selection. A presenter swap retains the outgoing projection and
deletion lifetime; room-resource cancellation does not delete that allocation.
Companion script requests, pending A-button bits and shared signals use live
reserved-slot memory. A pending button bit suppresses another A-list match.
Selecting a native companion through its NPC presenter publishes that native
button byte before ordinary NPC dialogue fallback. Its interaction controller
reads the byte, and the species consumes script requests on its next eligible
special-object visit; neither keeps a private forest or pressed-button flag.
The native species-call tail still runs when state-zero initialization yields;
it can update visibility, terrain effects and warp control before the actor is
shown. Game-specific cutscene dispatch remains with the game whose source owns it.

```text
ROM/disassembly
    -> strict generated records with source identity
    -> typed runtime database
    -> room entity, linked interaction, or room event
    -> focused headless regression
```

## Choose the original owner

| Original behavior | Runtime representation |
| --- | --- |
| Positioned actor with ordinary animation, facing, solidity, and text | Imported NPC record plus ordinary room entity |
| State-0 deletes or retains an actor | Imported visibility predicate |
| Story state selects text or a static initial position | Imported dialogue or position rule |
| Native object code updates movement, counters, collision, or animation | Specialized room entity with narrow capabilities |
| Several object slots exchange signals or share a part | Linked interaction state owner, preserving one update per slot |
| Room entry coordinates Link, actors, dialogue, transitions, flags, or audio | `RoomEventController` event |
| An `interactionRunScript` stream drives behavior | Typed command runner inside the owning entity or event |
| Native cutscene, special-object, palette, or transition state machine | Specialized native controller |

Ground-treasure text and heart-container completion use the active session's
shared reward owner, independently of the game's NPC host. The native treasure
slot still owns its grant, held updates and deletion. A caller-owned grant
retains its explicit completion contract; room/session cancellation must not
grant its inventory transaction again.

Interactions that request a native Link state publish its shared request and
parameter bytes. Link consumes the request on its own eligible visit and owns
the selected state's counters and movement. The requesting actor's timer,
animation or update freeze must not advance Link a second time. Shared control
signals retain their source lifetime and are read by movement, item allocation
and vulnerability gates; existing item parents keep their own update visits.

State-zero existence checks sample live flags on the eligible object visit,
after source-order placement. A hidden pending presenter retains its physical
allocation until that visit. Native deletion retires the allocation and cancels
resources bound to that actor; later save refreshes do not repeat an
initialization-only check.
Construct the native state-zero owner before choosing a story or ordinary body
presentation. Sample the flags in that owner, then transfer the same physical
slot; construction-time flag selection cannot replace the native visit.
An initializer called from a live native state keeps that call context and
finishes within the same slot visit. Preserve the caller's state through its
initialization gates; forcing state zero or scheduling another visit can add a
cold-spawn yield that the original function call never takes.
Common animal bodies and attacks use selected-pack feature data through the
shared factory. A game's binding supplies its native initialization and scripted
states; it does not construct a second movement or collision owner.
Ricky and Moosh body/riding graphics do not load their Ages quest scripts or
placements. Native Ages departure parameters are supplied by that game's binding.
The common exclamation factory uses each pack's native effect graphics; the
sound-producing helper and Moosh's silent direct allocation remain distinct.
Typed companion phases must read the live source state union, including writes
made by scripts and item handlers. Decode dispatch parameters on the eligible
native visit; construction must preserve bytes the source caller leaves intact.
A companion weapon keeps its own item lifetime and reads the live companion
slot. A body phase change does not retire that weapon unless its source does so.

NPC script composition belongs to the selected game. The shared interaction
controller receives its ordered script handlers and live gameplay-blocking
state; it does not construct story hosts or expose game-specific menu and
transition hooks. Entity-owned script lanes retain their native slot dispatch,
while conversation lanes retain their foreground update phase. Moving their
composition must preserve handler priority, room-change cancellation and
palette-before-object ordering.

The gameplay session composes and retires the common interaction service with
its selected pack's sign, chest and fallback data. Native script hosts are an
optional content binding; entity-owned scripts do not require an ordinary NPC
host. Both player bindings route tile interactions through this service and
advance reward presentation at the interaction phase. Session retirement
detaches subscriptions and discards presentation without executing gameplay
completion writes against state retained by a successor.
The session also binds the reserved push-block, dungeon-key and overworld-keyhole
owners. Common
player contact dispatch selects the current pack's collision profile; native
game-specific tile handlers remain explicit content bindings. Their room,
message and hazard subscriptions detach when the session retires. Shop and
script chest gates publish the opened tile and shared position without granting
the ordinary chest reward or writing its room flag.

Named-key contacts share the counter, hint, retained-key and sprite mechanism.
Their entrance consequences belong to the selected game's event binding. An
unbound entrance rejects activation before persistent flags, script signals,
effects, cues or input locks are published.

An invisible interaction controller occupies its native slot just like a
visible actor. Scripts, allocation retries and retirement run from that slot;
children and effects join the same live slot walk. A room-level resource or
native-cutscene owner may retain references for cancellation, but must opt out
of the legacy room-event update phase when its work already runs from object
slots or the main-thread cutscene phase. It must not advance those children a
second time.

Remembered companions are created before the placed stream and room-entry
cancellation. Bind their content resource owner after outgoing cancellation
and before destination preload, so cancelling the outgoing room cannot retire
the incoming native body. Route both producers through the same native dispatch.
Returning companions retain their coordinate low bytes when copying respawn
anchors. Forgetting a companion clears its remembered identity only; native
return paths can still consume the retained coordinates. Keep the shared copy
mechanism separate from the game's conditions for retaining that identity.

Complexity is not the decision. A moving villager may remain an ordinary room
entity; a short sequence that owns input, room changes, or persistent story
state may require a room event.

The entity manager reads the live object-disable mask for native category and
initialization gates. Legacy room events can also expose their control through
their owning controller. Do not add a second freeze flag for script writes:
ordinary interactions and initialization exceptions retain their source gates.

The original script input commands use the shared raw control operation:
`wDisabledObjects=$81`, `wMenuDisabled=$80`, parent-item clearing, carried-item
release, and Link invincibility/knockback writes. Link keeps its normal handler;
the mask gates its update. Preserve repeated command side effects and the
enable command's zero writes. Acquiring private cutscene control is a different
operation and cannot substitute for these source writes. Cancellation releases
only control still owned by the cancelled sequence.
NPC and room script hosts use this same operation. Their cancellation tracks object-mask
and menu-byte writes separately, including identical later writes and runtime
snapshot restoration; cleanup must preserve newer control state. Native actor
retirement can hand control to its parent without clearing those shared bytes.
Menu-only commands leave the object mask untouched while retaining their own
item-clearing and invincibility effects. Keep inline native mask assignments
distinct from those commands; their values and physical effects can differ.
Scrolling handoffs retain the original live control bytes until the next source
owner changes them.
A native producer and its receiving script share that control owner through
activation, completion and cancellation; do not add a second lease for the handoff.

Link state `$08` consumes its force-state request and initialization on separate
updates, then returns to normal when `wDisabledObjects` becomes zero. It needs
no private release callback. A subsequent item-get request replaces that state
and its ownership at the native force-state boundary. Object state-zero code
must publish requests from its eligible object slot, after Link's earlier
dispatch, rather than during room preparation.
Its initialization consumes the live animation parameter; a nonzero parameter
preserves parent items. Later animation requests and forced item use also run
under the object mask. Item-get state `$04` reads that same live parameter for
its pose and release gate. Keep these bytes in the shared runtime state and
preserve the separate Link and interaction dispatch boundaries.

Native cutscene controllers advance before the object pass. When the original
cutscene selects a successor and returns without updating objects, the event
exposes `SkipsObjectUpdate`; the application still advances the global frame
counter. Room changes and event ownership must preserve the first eligible
update of newly loaded actors, including events that begin during a return fade.
When a native caller applies a requested warp after updating objects, retain
that handoff through the application's post-object cutscene phase. Releasing
the event before the object pass must not defer the warp to ordinary gameplay
or let its destination actors update during loading.

Native background-only room loads retain live actor slots, scripts and shared
signals while rebinding their terrain presentation to the new room. They do not
dispatch ordinary room-entry events. A native warp request that returns to
normal gameplay is consumed by the transition owner after the following object
pass; selecting its destination alone must not move or reveal Link.

## Evidence packet

Before implementing an interaction, record:

1. Placement and surrounding object order.
2. Interaction ID/subid dispatch, state 0, later states, and every caller.
3. Scripts, native helpers, created parts/interactions, shared temporary bytes,
   and cross-object signals.
4. Flag addresses, masks, aliases, branch precedence, and deletion conditions.
5. Per-object update order, counter initialization/decrement boundaries, and
   same-update versus next-update work.
6. Positions, collision radii, speed/angle/fixed-point arithmetic, animations,
   OAM, palettes, text, sounds, and input-disable masks.
7. Completion writes, room exit, cancellation, scrolling, re-entry, and
   save/reload behavior.

Trace retained registers through dispatch and helpers even when the target
state has no explicit assignment. A native table lookup can continue into
adjacent data; import its executed stream and graphics domain explicitly.

Keep original identifiers in generated rows, diagnostics, source comments, and
validation failures. “Room `1:49` missing interaction `$3c:$0e`” is actionable;
“missing NPC” is not.

## Data-driven NPCs

`Import-NpcData.ps1` owns ordinary placement, implementation classification,
graphics/animation inputs, visibility, dialogue, initial positions, and small
interaction-family tables. Runtime databases strictly load those records.

Use an imported predicate when initialization selects among source facts. Do
not snapshot story state at database construction; visibility and dialogue
must observe live state at the same boundary as the original. Preserve branch
order where several flags overlap.

Initialization may select visibility or position once while dialogue continues
to observe live flags. `IOrdinaryNpcEntity` exposes separate refresh gates and publishes
completion after its imported initialization rules run; the adapter retains
the source's selection lifetime without copying save state.
An event must retain an initialized actor's visibility when the source checks
its room/save predicate only in state zero. A sibling writing the completion
flag must not delete that actor before its own native handler does.

A room event may own a script's input and cancellation lifecycle while its
native entity dispatches the runner in the original interaction slot. Keep that
slot as the sole dispatch owner when later actors consume its shared signals.
Special-object cutscene movement belongs in the owner's special-object update,
before item, enemy, part and interaction consumers. Preserve incoming motion
registers when the source's ID change does not overwrite them; apply text
gates to the original dispatch category rather than the surrounding scene.
Native coordinate-high writes retain fractional bytes and motion registers;
apply them without the state resets used for a room warp.
An always-updating interaction can run native movement before its text-gated
script. Keep that movement outside script waits, and retain the original
wait and jump yields when the script polls the native mover's completion.
Actors sharing a script block still retain independent interpreter state.
Preserve each entry point's commands and their yields; do not replace their
timelines with a common countdown. A queued Link ID change includes the next
handler's initialization update before normal movement or air physics resumes.

An ordinary NPC entity owns only its actor behavior and presentation. It
registers dialogue or script interaction through the central interaction
router. It does not directly scan input, own a room-wide sequence, or duplicate
save state.

An ordinary A-button selection queues its dialogue for the actor's own update
slot. The generic script consumes the selection and opens text before that
actor's final animation update; active text freezes initialized objects from
the next pass. Room replacement discards pending selections with their actors.
A pending selection retains its talk lifecycle until its own slot opens text,
including a native actor that is still finishing a script wait.

Collision initialization and explicit radius changes are separate operations.
The Ages `initcollisions` opcode initializes both radii to `$06` only when the
actor's Y radius is zero; otherwise it preserves both existing radii. Entering
a generic conversation script must therefore retain a custom interaction range.

New NPC renderer slots start hidden, matching a cleared `Object.visible` bit 7.
Loading graphics or animation and restoring an activation flag must not publish
an unresolved actor. Ordinary imported initialization publishes its final pose
after visibility, dialogue, and position rules finish. Native owners publish at
their source show or draw-priority operation; deferred event actors remain
hidden until that operation runs. Script show/hide commands use the shared NPC
visibility owner. Scrolling preloads report this resolved presentation without
advancing the actor's script or counters.
Event owners must publish source state-zero visibility after selecting the
initial pose, before an incoming scroll or destination fade exposes the room.
Deferring the whole event update must not defer that visibility write; deliberate
source invisibility and deletion predicates still take precedence.

If an interaction classified as specialized reaches the generic path, fail
with group, room, interaction ID/subid, and source context. A placeholder actor
must never silently stand in for an unsupported native behavior.

## Native and linked interactions

Use a specialized `IRoomEntity` when object code owns per-update state that
cannot be expressed as ordinary NPC data. Give it only the capabilities its
shared systems require. Keep byte counters, fixed-point movement, animation
boundaries, collision ownership, and native signals explicit.

For linked interactions, preserve each original object slot and its update
position. A shared state owner may coordinate them, but it must not collapse
several independent counters or reorder their updates. Children created into a
later slot may run later in the same pass when the source does.

The entity lifecycle owner is also the interaction-target lifecycle owner.
Room change, natural deletion, or cancellation removes the target without
rescanning a changed collection halfway through an interaction.

## Room events

`RoomEventController` owns sequences whose original mechanism coordinates
several systems. A single ordered registration defines entry precedence and
typed event lookup. A-button routes retain their separate source order.
Events receive one fixed tick from the application scheduler; bulk stepping
belongs to validation.

An event that coordinates native PART and INTERACTION work opts into the
shared object-phase callbacks. PART work runs in its category pass; interaction
work runs before rider copying and camera/shake updates. Each category uses its
own sampled admission, and independent always-update children retain that
eligibility. The later room-event dispatch must not tick a phased owner again.

Retained state does not implicitly grant exclusive update priority. A composite
event explicitly owns its children's dispatch, including dialogue updates;
uncoordinated active owners fail with room and event diagnostics. Events expose
their own menu and transition restrictions, which the controller aggregates
independently of update ownership. Ordinary and all-transition restrictions
remain distinct.

An event owns:

- the input/pause lease it acquires;
- its actors or registrations and their original update order;
- native state plus any typed script runners;
- transition-safe behavior explicitly allowed by source masks;
- completion writes and cancellation cleanup.

Event boundaries follow the original interaction/script, not a map area. Only
actors coordinated by one source sequence share an event owner; independent
dialogues, trades, room-entry scripts, and minigames remain separate even when
they reuse one imported database or live in adjacent rooms.

Native events compose room resources; command hosts separately bind room services
or entity services. The interpreter interface does not require a room context.
Full-screen fade capture is idempotent; each caller specifies whether it owns
the layer and whether cleanup restores the captured color. Input helpers retain
the difference between repeating disable-input side effects and acquiring a
lock only once. Player owns the current control owner: a later source write
transfers cleanup responsibility, and cancelling the former owner cannot
release the successor's control. Each event releases its own resources even
when cancelled directly; controller cancellation supplies no blanket unlock.
Shared helpers must preserve each caller's native update slot,
initialization update, counter boundary, and cancellation order. Command hosts
remain default-deny; composing resources does not expose script operations.

Cancellation is safe before initialization, during a sequence, after completion,
after actor retirement and native disposal, and on repeated calls. Clear the
owner's runners, actor references, pending callbacks and restrictions even when
its nodes no longer exist. Native validity checks protect presentation cleanup;
they must not bypass input or fade release. Cleanup must not commit source
completion flags or release a successor's control. Never suppress exceptions
around the controller's cancellation loop or replace ownership with a blanket
unlock.

The entity manager publishes NPC retirement once, before native disposal.
Shared script bindings detach their actor and pending button at that boundary;
an event retaining native hosts can also detach them through the notification.
Retirement listeners only remove references and subscriptions, without mutating
the entity manager or releasing event-wide control. Hiding an actor or moving
it into the outgoing scroll set is not retirement: outgoing bindings and
presentation survive until actual deletion at scroll completion. A retired
actor cannot be rebound as a live script actor.

An original destination initializer that replaces the arrival palette fade
may take ownership on the first object update after a stationary warp load.
The transition owner completes Link's arrival before the event starts its fade;
the two owners must not run consecutive arrival fades or overwrite each other.

Ordinary destination events remain frozen during scrolling. Clear runners,
release input, detach registrations, and remove transient actors on cancellation
or room invalidation. Persistent completion is derived from authoritative save
or room flags, never an event-local boolean.

Shared minigame scratch bytes retain their original WRAM lifetime across room
loads. Clear them only at the imported reset interaction, and keep inventory
changes in the inventory owner. Event cancellation releases local resources;
it does not imply the original reset or failed-game recovery path ran.

Use [Command runner](command-runner.md) only for original script streams. Waits,
text, or animation inside a native state machine are not a reason to convert it
to script commands.

## Implementation workflow

1. Build the evidence packet and select the original owner.
2. Extend `Import-NpcData.ps1` for ordinary/state-derived actor data or
   the owning cutscene family stage for actual script/event records. Stage
   inputs, outputs, and shared functions are declared in `import_oracles.ps1`.
3. Add strict runtime loading; reject unsupported classifications and operands.
4. Implement the smallest ordinary entity, linked owner, native entity, or room
   event that preserves update order.
5. Route player interaction through the central registry.
6. Validate actor state, exact counters, source-ordered updates, dialogue and
   input boundaries, scroll/warp entry, cancellation, re-entry, and persistence.
7. Update the affected table entries and summary counts in
   [NPC interaction coverage](ages/npc-interaction-coverage.md). This is required for
   implemented, partial, suppressed, or reclassified NPC records.

Useful starting points are `data/ages/interactions.s`,
`object_code/ages/interactions/`, `tools/import_oracles/Ages/Import-NpcData.ps1`,
`tools/import_oracles/Shared/Import-CutsceneData.ps1`, `src/Features/World/Entities/`,
`src/Games/Ages/Features/Story/`, and `validation/Ages/Features/Npcs/` or
`validation/Ages/Features/Story/`.
