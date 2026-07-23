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

Every C# file contains at most one class or interface, and its filename matches
that implementation type; partial validation scenario files are the deliberate
exception. Narrow records and enums live in the main class/interface file for
the use case that constructs or owns them. Do not create record-only or
enum-only source files. Types remain in the shared `oracleofages` namespace so
folder moves do not change runtime identity or Godot script bindings.

## Runtime owners

| Owner | Responsibility |
| --- | --- |
| `GameRoot` | Composition, top-level update order, menu/intro handoff, and HUD synchronization |
| `RoomSession` | Active group/room, room data, layout state, and neighbor resolution |
| `RoomTransitionController` | Scrolling, warps, destination placement, fades, camera, and time portals |
| `RoomEntityManager` | Room object creation, active/outgoing lifetimes, fixed updates, contacts, and spawned effects |
| `InteractionController` | Signs, chests, NPC interaction, dialogue, and gameplay-owned submenus |
| `RoomEventController` | Multi-system room-entry and story events |
| `RoomCollision`, `TerrainController`, `PushBlockController`, `CombatController` | World collision, terrain, movable blocks, and combat effects |
| `OracleSaveData`, `InventoryState`, `DeathRespawnPointController` | WRAM-style state, typed item behavior, and saved checkpoints |
| `OracleMenuLifecycle` and menu controllers | Exclusive modal ownership, fixed-update fades, and input suspension |
| `OracleSoundEngine` | Persistent 60-update music/SFX sequencer and generated audio playback |

Keep APIs narrow: the owner of a state transition performs it, while callers
request behavior through explicit operations. Do not recreate parallel copies
of save flags, current room identity, inventory bytes, RNG state, or transition
state inside feature controllers.

## Update order

`GameRoot._Process` establishes observable ordering:

1. Title/file-select or new-game intro, when active.
2. New-game arrival presentation.
3. Debug flag menu, inventory, map, or a gameplay-owned modal menu.
4. Room transition state.
5. Death checkpoints and room entities, unless the time-warp mode freezes them.
6. Room events, or the time-warp-safe room-event subset.
7. Ordinary interactions.
8. Animated room tiles and development displays.

Changing this order is a gameplay change. Contacts can start transitions,
scripts can observe entity state, and the original disable masks take effect at
specific handler boundaries. Document and validate any intentional change.

Gameplay systems consume rendered delta through fixed-update accumulators at 60
updates per second when the original advances once per frame. A long host frame
may execute multiple original updates, but each update retains deterministic
ordering. Presentation-only interpolation must not advance gameplay counters.

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
positions remain unchanged.

## Production and validation

The production project excludes `validation/**/*.cs`. The separate validation
project references the production assembly and accesses intentional internals
through `InternalsVisibleTo`. Runtime classes may expose a narrow internal host
surface, but validation-only traces, audit counters, and compatibility state
belong in the validation assembly. See [Validation](validation.md).
