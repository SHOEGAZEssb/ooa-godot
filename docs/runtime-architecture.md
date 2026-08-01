# Runtime architecture

## Composition and scenes

`scenes/main.tscn` owns the application root and persistent sound engine.
`GameRoot` presents the title/file flow and creates `scenes/gameplay.tscn` for
active play.

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

`GameSceneGraph` binds required unique nodes and rejects an incomplete scene.
Room entities, cutscene actors, drops, and effects are dynamic and are created
by their runtime owners. Dispose gameplay-scoped event subscriptions before
replacing a scene; freeing a Godot node does not detach managed handlers from a
longer-lived publisher.

## Source organization

```text
src/
|-- Application/       composition, fixed updates, input, pause ownership
|-- Features/          Audio, Combat, Graphics, Interface, Menus,
|                      Persistence, Player, Story, and World use cases
|-- Infrastructure/    generated-data and external boundaries
`-- Shared/            small behavior-neutral primitives
```

Place a type with the use case that owns its behavior. Follow original dispatch
boundaries: globally dispatched enemies and interactions remain shared even if
one dungeon first exercises them. A dungeon folder owns only genuinely
dungeon-specific placements, puzzles, events, or source dispatch.

Use one class or interface per C# file and match the filename. Narrow records
and enums may live with their constructing owner. All types remain in the
shared `oracleofages` namespace so folder moves do not change runtime identity
or Godot bindings.

## Authoritative owners

| Owner | Responsibility |
| --- | --- |
| `GameRoot` | Composition, application-owned 60 Hz schedule, input snapshot, shell/gameplay handoff |
| `RoomSession` | Active room identity, room data, layout state, dungeon neighbors |
| `OracleWorldData` | Cached imported world assets and live gameplay palettes |
| `RoomTransitionController` | Scrolls, warps, destination placement, fades, camera, time portals |
| `RoomEntityManager` | Ordered room-object creation, active/outgoing lifetimes, updates, contacts, effects |
| `InteractionController` | Ordered player interactions, dialogue, and gameplay-owned submenus |
| `RoomEventController` | Multi-system room/story events and their interaction registrations |
| World mechanic controllers | Collision, terrain, blocks, combat, and other shared mechanics |
| `OracleSaveData` / `InventoryState` | WRAM-style state and typed treasure/item transactions |
| Menu lifecycle controllers | Exclusive modal ownership, fades, and input suspension |
| `OracleSoundEngine` | Persistent 60-update music and SFX sequencing |

The owner performs a transition; callers request it through a narrow operation.
Do not keep parallel copies of save flags, inventory bytes, room identity, RNG,
transition, or modal state in feature controllers.

## Fixed-update order

`GameRoot._Process` gives elapsed host time and one buffered input sample to
`ApplicationFixedUpdateScheduler`. Each original update completes in this
observable order before the next begins:

1. Title/file selection or new-game presentation, when active.
2. Active modal menus and their fades.
3. Link movement, hazards, and item-parent behavior.
4. Moving world mechanics, then transition progression.
5. Death checkpoints and room entities, including contacts and same-update
   spawns/removals, unless the active transition masks them.
6. Combat/terrain effects, room events, and ordinary interactions.
7. Inactive edge-transition checks and the final camera sample.
8. HUD counters, room animation, development displays, and dialogue.
9. One persistent audio-sequencer tick.

Changing this order is a gameplay change. Validate it explicitly. A long host
frame may run several original updates, but update N must complete before update
N+1 starts.

`ApplicationInputBuffer` preserves a host press until one original update
consumes it. Every reader in that update sees the same immutable held/pressed
snapshot. Catch-up updates retain held state but do not receive the consumed
edge. Timing and opening-frame suppression use original-update serials, not
rendered-frame counters.

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
