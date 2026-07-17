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
160 by 144 screen coordinates. The gameplay field occupies the top 160 by 128;
the HUD occupies the bottom 16 pixels.

Do not apply camera offsets to persistent room positions. Transition draw
offsets are presentation state supplied to entities while their logical room
positions remain unchanged.

## Production and validation

The production project excludes `validation/**/*.cs`. The separate validation
project references the production assembly and accesses intentional internals
through `InternalsVisibleTo`. Runtime classes may expose a narrow internal host
surface, but validation-only traces, audit counters, and compatibility state
belong in the validation assembly. See [Validation](validation.md).
