# Runtime architecture

## Scene ownership

`scenes/main.tscn` contains the root `GameRoot` and the persistent
`OracleSoundEngine`. `GameRoot` shows the title/file flow or instantiates
`scenes/gameplay.tscn` for active gameplay.

The gameplay scene owns the stable node tree:

```text
Gameplay
|-- World
|   |-- RoomView
|   |-- Link
|   `-- RoomCamera
`-- Interface (CanvasLayer)
    |-- Hud
    |-- RoomWarpFade
    |-- Dialogue
    |-- MapScreen
    |-- InventoryScreen
    |-- SaveQuitScreen
    |-- MenuFade
    |-- DebugFlagScreen
    `-- RoomDebug
```

`GameSceneGraph` binds these unique nodes and rejects an incomplete scene.
Stable UI/world nodes belong here so their lifecycle, draw order, and editor
layout are visible. Room entities, transient effects, cutscene actors, and other
content-dependent nodes are spawned by their owning controllers.

Freeing a Godot scene does not detach managed event handlers from longer-lived
publishers. Before replacing gameplay after Continue or returning to the title,
`GameRoot` disposes gameplay-scoped owners with those subscriptions.
`RoomEntityManager.Dispose` detaches the live save and runtime-state change
handlers before its NPC nodes are queued for deletion.

## Source organization

Production C# is organized by use case rather than by technical type:

```text
src/
|-- Application/       top-level composition and gameplay pause ownership
|-- Features/
|   |-- Menus/         inventory, map, and file/menu shell flows
|   |-- Story/         command runtime and story-event families
|   `-- World/         rooms, terrain, transitions, entities, and interactions
|-- Infrastructure/    generated-data readers and other external boundaries
`-- Shared/            small behavior-neutral primitives
```

Feature folders may contain narrower mechanic folders such as `Chests`,
`DarkRooms`, `Gasha`, or `SpiritsGrave`. Put a type with the use case that owns
its behavior; do not recreate catch-all `entities`, `interactions`, or
`cutscenes` folders at the source root.

Folder ownership follows the original dispatch boundary. A globally dispatched
enemy ID belongs under `Enemies/Species`; a globally dispatched dungeon
interaction belongs under `Interactions/Dungeons`. The first dungeon whose
route exercises that ID does not own its class, behavior table, graphics, or
runtime state. A named dungeon folder may own source-ordered placements,
room-local puzzle/state consumers, and scripts or story events whose source
dispatch is genuinely specific to that dungeon. Shared handlers receive typed
configuration or shared databases and must not depend on a dungeon database.

Every C# file contains at most one class or interface, and its filename matches
that implementation type; partial validation scenario files are the deliberate
exception. Narrow records and enums live in the main class/interface file for
the use case that constructs or owns them. Do not create record-only or
enum-only source files. Types remain in the shared `oracleofages` namespace so
folder moves do not change runtime identity or Godot script bindings.

## Runtime owners

| Owner | Responsibility |
| --- | --- |
| `GameRoot` | Composition, the application-owned 60 Hz scheduler and input snapshot, menu/intro handoff, and HUD synchronization |
| `RoomSession` | Active group/room, room data, layout state, and neighbor resolution |
| `OracleWorldData` and `BackgroundPaletteState` | Cached room assets and the eight live gameplay BG palette slots shared by rooms, dialogue, and palette effects |
| `RoomTransitionController` | Scrolling, warps, destination placement, fades, camera, and time portals |
| `RoomEntityManager` | Room object creation, active/outgoing lifetimes, fixed updates, contacts, and spawned effects |
| `InteractionController` | Ordered A-button routing, signs, chests, dialogue, and gameplay-owned submenus |
| `RoomEventController` | Multi-system room-entry/story events and their typed interaction registrations |
| `RoomCollision`, `TerrainController`, `PushBlockController`, `CombatController` | World collision, terrain, movable blocks, and combat effects |
| `OracleSaveData`, `InventoryState`, `DeathRespawnPointController` | WRAM-style state, typed item behavior, and saved checkpoints |
| `OracleMenuLifecycle` and menu controllers | Exclusive modal ownership, fixed-update fades, and input suspension |
| `OracleSoundEngine` | Persistent 60-update music/SFX sequencer and generated audio playback |

Keep APIs narrow: the owner of a state transition performs it, while callers
request behavior through explicit operations. Do not recreate parallel copies
of save flags, current room identity, inventory bytes, RNG state, or transition
state inside feature controllers.

NPC A-button dispatch is one `NpcInteractionRouter` registry assembled by
`InteractionController`. Its durable priority is family naming, event-owned
actors in explicit gameplay order, typed NPC script hosts, ordinary dialogue,
then player-only shop handling when no NPC target exists. `RoomEventController`
publishes registrations instead of an override OR-chain; it still owns and
updates each event state machine. The selected `NpcInteractionTarget` retains
the matching entity lifecycle owner so natural end and room-change cancellation
do not rescan a changed entity collection.

## Update order

`GameRoot._Process` feeds host time and one buffered input sample to
`ApplicationFixedUpdateScheduler`. Each consumed update replays the complete
observable order before another update can begin:

1. Title/file-select or new-game intro, when active.
2. New-game arrival presentation.
3. Debug flag menu, inventory, map, or a gameplay-owned modal menu.
4. Link's special-object movement/hazard pass and item-parent pass.
5. Moving blocks/dungeon key tiles, followed by active room-transition
   progression.
6. Death checkpoints and room entities, including contacts, same-update child
   spawns, removals, and pending warp dispatch, unless time-warp freezes them.
7. Scheduler-owned combat/terrain effects, room events or their time-warp-safe
   subset, then ordinary interactions.
8. The inactive `screenTransitionState2` edge check and camera sample observe
   Link's final post-interaction position.
9. Harp children, HUD counters, animated room tiles, development displays, and
   dialogue.
10. One persistent music/SFX sequencer tick.

Changing this order is a gameplay change. Contacts can start transitions,
scripts can observe entity state, and the original disable masks take effect at
specific handler boundaries. Document and validate any intentional change.

Only the application scheduler consumes rendered delta for live gameplay. A
long host frame may execute multiple original updates, but it completes the
entire order above for update N before starting update N+1. Component delta
entry points remain for focused validation and always receive one fixed update
from production. Godot callbacks on Link, dialogue, movable blocks, key tiles,
terrain/combat effects, and the sequencer are disabled or presentation-only
while application ownership is active.

`ApplicationInputBuffer` retains a host edge until an original update consumes
it. Every reader in that update sees the same immutable held/pressed sample;
later catch-up updates retain held state but cannot see the consumed
just-pressed edge. Opening-frame suppression uses the original-update serial,
not the rendered-frame counter. The buffer samples only actions currently
registered in `InputMap`: gameplay-scoped debug controllers install F1-F3
actions when gameplay is created, so title and file-select frames treat those
not-yet-registered actions as inactive without querying Godot for them.

## Coordinate and presentation boundaries

Rooms, Link, entities, collision, terrain, and transient world effects use
original room/world coordinates and follow `RoomCamera`. HUD, dialogue, menu
screens, fades, and debug overlays live under the `Interface` canvas and use
160 by 144 screen coordinates. The HUD occupies screen y=0-15 and the 160 by
128 gameplay field occupies y=16-143, matching the original LCD status-bar
split. `WorldToGameplayScreen` retains the original field-relative y=0-127
space used by object bounds and textbox-side decisions; `WorldToScreen` adds
the presentation offset and returns physical display coordinates.

Do not apply camera offsets to persistent room positions. Transition draw
offsets are presentation state supplied to entities while their logical room
positions remain unchanged. Sample the ordinary room camera only after
interaction-owned movement such as moving-platform displacement; sampling it
before that displacement makes Link's screen position lag by one update.

## Production and validation

The production project excludes `validation/**/*.cs`. The separate validation
project references the production assembly and accesses intentional internals
through `InternalsVisibleTo`. Runtime classes may expose a narrow internal host
surface, but validation-only traces, audit counters, and compatibility state
belong in the validation assembly. See [Validation](validation.md).
