# Runtime architecture

Boot preparation retains the production assembly's concrete Godot node script
resources, discovered from their generated script-path attributes. Loading
these resources prepares native C# script metadata before room construction.
For two explicitly audited presenter types, boot also constructs and immediately
frees a detached node to prepare the native constructor binding. These constructors
contain only local defaults; they never enter the tree, initialize an actor,
allocate an original object slot, or access save/RNG state. Extending this list
requires auditing the constructor and its base classes against those boundaries.
Preparation is
staged on the main thread, and the application releases its retained references
when it exits the tree.

Dialogue resource preparation executes the pure command parser and text
conversion once on local sample text. Outputs are discarded; it never opens a
textbox, invokes save-name providers or changes palette, audio or input state.
Live messages still resolve names and create fresh segments when they open.

## Composition and scenes

`scenes/main.tscn` owns the application root and persistent sound engine.
`GameRoot` presents the boot/attract/title/file flow. Both native gameplay scenes
inherit `scenes/gameplay.tscn`, which owns the common world, camera, interface,
menu and development nodes. Its single RNG owner spans that frontend
and gameplay boundary; replaying or dwelling on the title must advance the same
state later consumed by room objects.

Both file-entry paths bind `GameplaySession`. It constructs HUD/dialogue,
status, menus and pause ownership, entity sound bindings and the object
dispatcher against the selected room, inventory and runtime owners. Its
foreground dispatch owns the main-thread clock, debug/menu ordering, shock,
object-pass continuation, status, post-object collision dispatch and dialogue
advancement. Each native caller supplies collision eligibility around source
cutscenes and transitions; the game binding does not invoke another collision
walk. Composition requires
the same save, inventory and runtime instances; matching game or pack identifiers
alone do not admit an independent state owner. Native bindings
select source eligibility and reduced cutscene updates at explicit phases;
they do not run a second gameplay loop. Shock advances before menu eligibility
is sampled, including its terminal update. A yielded object pass resumes at
its existing continuation while the main-thread clock remains stopped.

Each session binds one shared transition owner against those same room/entity
instances before its first update. Menu transition admission, Gale travel and
cancellation, and arrival-palette handoff use that owner directly. Games supply
native room-pack, navigation and sequence rules around the ordinary lifecycle.
The active foreground session selects the current camera; the transition owner
publishes camera coordinates and their canvas transform in the same update.

The object tail advances the source screen-transition handler after interactions
and before rider/held-object copies and native item post updates. The final scroll
tick still freezes destination AI earlier in that pass. A yielded interaction
retains the pending item post pass until the same object dispatch resumes.

The common player world owns Bomb and Bracelet input and their lift/carry/throw
parents, and Satchel input with its live Pegasus counter, for both games.
Link movement, dust and item input read that same counter owner. The common entity factory constructs Bomb and physical
seed children, including attached burning effects, from the selected pack;
lifted tiles and Shovel children advance in the shared reserved-item pass.
Shovel's terrain transaction and independently colliding child also belong to
the common player world. These paths use the same
room, inventory and runtime owners. Game bindings supply additional interactions
and event notifications; they do not allocate another copy of these parents.
Room reset and interruption release them through the shared player-world contract.
Parent clearing preserves physical children until their native item lifetime
ends; explicit physical-item clearing retires them through the entity owner.
Normal Link movement uses the common native `$81` object-disable gate.
Freezing interactions, enemies or parts alone preserves Link's movement;
native event bindings can supply additional movement ownership.

Ground-treasure dialogue and heart-container completion are composed once by
the shared gameplay session against its selected inventory, room, dialogue and
transition owners. The native interaction slot retains the inventory grant,
held-item updates, mask release and deletion. NPC hosts borrow this reward
owner; they do not install another ground-treasure completion path. Room or
session retirement releases the active reward and its subscriptions.

Choosing Seasons preloads a detached `scenes/seasons/opening.tscn`, then shows
the Seasons title and file menu. File selection binds the retained scene to
the selected standard file. Existing overworld files use the common checkpoint
initializer; new files enter the native opening. The application retains its
fixed-update, input, RNG and audio owners. Seasons supplies room content,
collision and native opening controllers to the shared session. Both pregame
introductions use the application's common controller, clock, graphics-wait and
dialogue path, with their selected records and completion callbacks. Native interaction and part pools
advance in original slot order; source signals and disabled-object masks retain
their lifetime across controller, player, part and interaction phases.

Room content bindings load each game's layouts and persistent substitutions;
the shared room session owns admission, visits, minimap state and active room
identity. Entity factories supply ordered content and game-specific bindings;
the shared manager owns allocation, native slot walks, freezes, outgoing sets
and retirement. Neither owner constructs Ages content implicitly. Ages creation
helpers select their content explicitly, and both owners reject mismatched
content, save and runtime packs before binding live state.
Shared species bodies retain their physical slot and presenter when a native
state dispatches into a game binding. Game-specific handlers consume the live
state bytes and shared movement primitives. Dispatch does not reinitialize the
object or restart its animation; native animation commands keep their source
boundaries. Handler catalogs load when that native state is visited.

The shared scroll controller owns setup, fractional movement and cleanup
counters. Game composition supplies the source graphics schedule and handles
its presentation and native cutscene handoffs. The Seasons opening uses these
same room, entity and scroll owners through its ordinary aftermath room;
unsupported destinations and after-event streams fail with source context.

Startup retains a detached gameplay hierarchy, prepared UI resources, a world
data owner and save-independent room tables. The hierarchy is allocated before
the loading animation begins, then its presentation resources are built in
small main-thread steps. No gameplay node enters the tree or binds a save until
file selection. Cancellation frees the detached hierarchy.

The shared loader lives in `scenes/launcher/boot_loading.tscn`. Its background
worker reads the selected pack's bytes and tables; Godot graphics and scene
preparation remain on the main thread. Seasons retains room textures, reachable
actor animation compositions, Link's cutscene renderer, HUD, dialogue and the
pregame presenter. This work binds no save, creates no original object slots
and consumes no original updates or RNG. File entry reuses these prepared
owners instead of reconstructing them behind the title-to-game fade.

Parsed NPC, enemy and treasure definitions are shared read-only source data,
initialized once on first use and retained across sessions within their explicit
asset pack. Shared readers and presenters require a pack binding; they must not
choose Ages when a binding is omitted. Graphics-bearing enemy records retain
that binding through actor and projectile construction. Their record,
animation and palette arrays must not be modified by consumers. Session
replacement still creates fresh saves, inventory, RNG, worlds, mutable room
layouts, entity managers and event controllers; source tables never bind to
those live owners.

Game-specific boot recipes and content/controller construction belong to the
corresponding `GameRoot` partial under `src/Games/`. The shared root owns scheduling,
loading progress, input, pregame and file-store dispatch; `GameplaySession` owns
common runtime composition and foreground advancement. Native story selectors
and world construction remain game-owned. Debug savestate input, status,
serialization and storage are shared; selected-session bindings supply stable
capture eligibility and native reconstruction. Restore rejects a foreign game
or unavailable room before releasing the active session.

Replacing a bound session or exiting its scene disposes its previous owner.
Disposal cancels pending object continuation, releases modal leases without
executing menu completion callbacks, disconnects player game-over, inventory and
room observers, and releases status/entity subscriptions. A disposed session cannot advance its
clock, objects or presentation; application audio continues independently.
The gameplay menu facade ignores late game-over, update and input calls after
retirement, and the disposed pause owner rejects new leases. Replacing a session
creates fresh menu and pause owners; a retained game-over callback cannot restart
audio or change either session's save.
Application menu/development accessors resolve that session directly instead of
retaining separate controller references in a game binding.

Boot resource steps share a soft 3 ms work budget per host frame. Small steps
can finish together; asset count must not impose one rendered frame per asset.
The budget is checked between operations, so an individual resource load can
exceed it. A pending data worker yields immediately instead of busy-polling.

For Ages, the new-game intro prepares file-dependent gameplay owners incrementally across
host frames, consuming those retained resources. The prepared scene, interface layer and camera remain hidden and
disabled. Preparation reads the selected save but does not enter the room,
create room objects, advance gameplay, or consume object RNG. The original
intro completion commits room entry and enables the scene. An unfinished
preparation is drained at that boundary; it never shortens the intro.

Existing-file selection also prepares the hidden gameplay scene across host
frames. Original gameplay, playtime and object RNG remain frozen until the
single room-entry commit; audio output continues independently. The first
gameplay update follows that commit. Cancelling preparation releases the
partial session without writing it to disk. Imported Impa, remote Maku, Tokay
and ocean event definitions and warp records are immutable shared sources,
prepared by the boot worker. Event instances and command runners stay fresh
for each session.
The boot worker also prepares immutable entity definitions. Session factories
receive a fresh source catalog populated from those definitions and bind its
dungeon maps to the current room session. Later lazy cache assignments remain
local to that catalog; it contains no save, RNG or actor state.
Definitions that construct or retain Godot graphics (sword beams, Dimitri and
Gasha trees) remain session-local and prepare on the main thread.
The detached player's initial walking, shadow, terrain-effect and transformation
graphics also prepare during boot, before any world/save/RNG binding. The first
initialization consumes these resources; later initializations refresh them.

The entity factory is a manager-owned construction helper. It uses named
operations on that owner for allocation, signals, and events. Actor callbacks
read live input providers through the manager when invoked, so providers assigned
or replaced after construction remain effective.

Ordered room parsing selects placements and checks native capacity before handing
the selected imported handler to the enemy factory. Enemy construction uses the
same manager, RNG and session data as other room objects; it does not run a
separate placement pass. The session resource catalog owns database caches and
staged preparation, shared by both construction paths.

The entity factory resolves its databases on first use or staged intro
preparation and retains them for the session. Room-wide gates still evaluate
their inputs in dispatch order. Link
similarly constructs alternate-pose and item graphics on first presentation,
retaining them for the player node's lifetime. Neither optimization advances
gameplay or substitutes a different room-entry path.

Link's sprite library owns atlas construction, recoloring and texture caching.
The player selects poses from its live gameplay state and performs drawing.
The library has only imported graphics inputs, with no world, inventory, save,
RNG or update-loop access.

Native cutscene callers can omit normal menu dispatch or status-bar updates
while continuing object updates. Room events expose those phase gates to the
application loop; hiding the HUD alone does not freeze its counters or sounds.

The gameplay scene contains stable nodes whose lifecycle and draw order should
remain visible in the editor:

```text
Gameplay
|-- World
|   |-- RoomView
|   |-- Link
|   `-- RoomCamera
`-- Interface (CanvasLayer)
    |-- Hud
    |-- fades and dialogue
    |-- map, inventory, save/quit, and ring screens
    `-- development overlays
```

`GameplaySceneGraph` binds required unique nodes and rejects an incomplete scene.
It selects every common presenter's content pack before child preparation or
tree entry and rejects changing that selection afterward. Native scene variants
add story presenters, such as the Ages ending and Seasons opening. Camera
projection applies the gameplay-field origin without moving world coordinates;
HUD placement changes interface positions and the projection together.
Room entities, cutscene actors, drops, and effects are dynamic and are created
by their runtime owners. Dispose gameplay-scoped event subscriptions before
replacing a scene; freeing a Godot node does not detach managed handlers from a
longer-lived publisher.
The shared session retires its transition owner and entity pools when it closes.
Retirement abandons unfinished scroll and palette work, detaches room callbacks,
and clears allocations without completing a transition or advancing native object handlers.
A retired transition rejects new transfer requests and cannot advance its old scene.

## Source organization

```text
src/
|-- Application/       composition, fixed updates, input, pause ownership
|-- Features/          shared gameplay/presentation, including Dialogue and Scripting
|-- Games/Ages/        source-specific content, data bindings and composition
|-- Games/Seasons/     implemented Seasons content (launcher and standard opening)
|-- Infrastructure/    generated-data and external boundaries
`-- Shared/            small behavior-neutral primitives
```

Place a type with the use case that owns its behavior. Follow original dispatch
boundaries: globally dispatched enemies and interactions remain shared even if
one dungeon first exercises them. A dungeon folder owns only genuinely
dungeon-specific placements, puzzles, events, or source dispatch.

Use one class or interface per C# file and match the filename. Narrow records
and enums may live with their constructing owner. All types remain in the
neutral `Oracles` namespace. Folder ownership separates shared mechanisms from
game-specific content without duplicating the engine. Preserve script UIDs
when moving resources and update scene/retained-script paths together.

`GameRoot` keeps the single application/session state owner and fixed-update
loop. Its Ages frontend, arrival and controller composition live in the Ages
partial; the launcher and common gameplay loop remain in Application. A new
game must supply its own content/rules and preserve common ordering contracts.
Shared frontend, save and asset-loading APIs require an explicit game or asset
pack; callers must not rely on Ages as a default. Existing Ages-only content
bindings do not establish Seasons behavior. Common enemies, dialogue and command
execution stay shared.

## Authoritative owners

| Owner | Responsibility |
| --- | --- |
| `GameRoot` | Composition, original-update scheduling, input snapshot, shell/gameplay handoff |
| `RoomSession` | Active room identity, room data, layout state, dungeon neighbors |
| `AgesWorldData` | Cached imported world assets and live gameplay palettes |
| `AgesRoomTransitionController` | Scrolls, warps, destination placement, fades, camera, time portals |
| `RoomEntityManager` | Ordered room-object creation, active/outgoing lifetimes, updates, contacts, effects |
| `InteractionController` | Ordered player interactions, dialogue, and gameplay-owned submenus |
| `RoomEventController` | Multi-system room/story events and their interaction registrations |
| World mechanic controllers | Collision, terrain, blocks, combat, and other shared mechanics |
| `OracleSaveData` / `InventoryState` | WRAM-style state and typed treasure/item transactions |
| Menu lifecycle controllers | Exclusive modal ownership, fades, and input suspension |
| `OracleSoundEngine` | Persistent music/SFX queue, original sound driver and APU |

The owner performs a transition; callers request it through a narrow operation.
Do not keep parallel copies of save flags, inventory bytes, room identity, RNG,
transition, or modal state in feature controllers.

## Fixed-update order

The shared main-thread clock increments the active save's four-byte playtime
counter and publishes its low byte as `wFrameCounter` before gameplay dispatch,
including ordinary menus and native cutscenes. Room entities consume that byte;
object updates and scrolling do not advance another clock. Explicit native
scratch writes to the frame byte survive the current update, then the next main
thread publication replaces them from playtime. Game Over stops this thread;
its interface and the application audio clock continue independently.

Both production games enter the same object dispatcher. It owns Link's death
prelude, companion-before-Link dispatch, post-Link signal clearing, the shared
entity walk, held-object positioning, tile-push publication and queued tile
graphics. Native caller hooks retain transition and script phases around that
walk. A yielding interaction suspends this pass: resuming finishes interactions
and its tail without updating Link or entities again. Native event cancellation
discards the continuation before a fresh object pass. Foreground cutscene/menu
selection and game-specific service composition remain separate bindings.

The ordinary foreground caller consumes pending `wCutsceneTrigger` after the
status bar and before warp selection or collision dispatch. The shared operation
masks bit 7 into `wCutsceneIndex`, clears the request and resets
`wCutsceneState`; the selected game's native handler begins on the next update.
Reduced native callers leave an ordinary request pending, including the update
that returns to normal gameplay. A source controller can publish another
request while its first cutscene runs. Do not replace this byte with direct
event activation or clear it when that cutscene finishes.

All sessions use `ApplicationFixedUpdateScheduler` to advance complete 60 Hz
updates from host time. Gameplay counter deltas remain `1/60`; resource-loading
time never becomes a larger gameplay delta. CPU instruction costs, LCD scanout
scheduling and sound interrupt phases do not schedule application updates.

Each foreground update completes in this observable order before the next begins:

1. Title/file selection or new-game presentation, when active.
2. Active modal menus and their fades.
3. Link movement, hazards, and item-parent behavior.
4. Moving world mechanics, then transition progression.
5. Death checkpoints and room entities, including contacts and same-update
   spawns/removals, unless the active transition masks them.
6. Combat/terrain effects, room events, and ordinary interactions.
7. Inactive edge-transition checks and the final camera sample.
8. HUD counters, room animation, development displays, and dialogue.
9. Apply queued sound requests and advance the sound sequencer once.

Changing this order is a gameplay change. Validate it explicitly. A long host
frame may run several original updates, but update N must complete before update
N+1 starts.

`ApplicationInputBuffer` preserves a host press until one original update
consumes it. Physical-input readers in that update share an immutable held/pressed
snapshot. Catch-up updates retain held state but do not receive the consumed
edge. Timing and opening-frame suppression use original-update serials, not
rendered-frame counters.

The shared special-object pass publishes physical, reversed or simulated game
keys after menu dispatch and before companion/Link. The active session's WRAM
owns held keys, fresh keys and the native angle; fresh keys compare with the
previous object pass, including across menus which leave these bytes intact.
Raw writes and runtime restoration use that same history. A yielded interaction
resumes with the existing sample instead of polling again. The shared input
facade borrows these bytes only for the remainder of the owning update.
The text thread continues to read the physical snapshot, including choices
opened while Link retains a terminal simulated-input stream.
The script input owner retains native WRAM counters, palette gates and the
terminal counter's held-key behavior.

Frontend controllers publish their screen changes within the owning update.
Preserve explicit source state transitions and input gates, including
initialization updates that ignore buttons, without simulating the CPU work
performed inside those updates.

## Coordinates and presentation

Room layouts, Link, entities, collision, and world effects use original
room/world coordinates and follow `RoomCamera`. Fixed interface content uses
160 by 144 screen space. The HUD occupies screen y=0-15; the gameplay field is
y=16-143. Field-relative calculations remain y=0-127 and add the presentation
offset only at the interface boundary.

Transition draw offsets affect presentation, never stored room positions. Take
the ordinary camera sample after interaction-owned movement such as platform
displacement so the screen does not lag by one update.

## Production and validation

Production compilation excludes `validation/**/*.cs`. The validation project
references the production assembly through a narrow internal surface. Runtime
objects may expose truthful internal state or observer hooks, but audit history,
test fixtures, and regression orchestration belong in the validation assembly.
See [Validation](validation.md).
